// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

using static Microsoft.Diagnostics.Tracing.Computers.AsyncProfilerCallStackTransformHelpers;
using static Microsoft.Diagnostics.Tracing.Computers.AsyncProfilerCallStackStitchBoundary;
using static Microsoft.Diagnostics.Tracing.Computers.AsyncProfilerMethodNames;

using RuntimeNames = Microsoft.Diagnostics.Tracing.Computers.AsyncProfilerRuntimeNames;

namespace Microsoft.Diagnostics.Tracing.Computers
{
    /// <summary>
    /// Classifies runtime frames, matches runtime frame sequences, and supplies presentation transforms.
    /// Selection uses the captured runtime, not the runtime running the analysis.
    /// </summary>
    /// <remarks>
    /// Unknown or conflicting runtime metadata selects <see cref="Common"/>.
    /// A selected implementation does not prove a match: callers still verify captured module ownership,
    /// method identity, and call-stack context. Overrides must preserve those checks.
    /// Generated application names follow compiler conventions, independently of the captured CoreLib version.
    /// </remarks>
    internal class AsyncProfilerRuntimeSupport
    {
        protected AsyncProfilerRuntimeSupport(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Selects support from captured CoreLib versions and runtime flavor.
        /// Later versions reuse the newest applicable baseline until another implementation is added.
        /// </summary>
        public static AsyncProfilerRuntimeSupport Select(
            string fileVersion, string productVersion, RuntimeSku runtimeFlavor)
        {
            Version file = ParseVersion(fileVersion);
            Version product = ParseVersion(productVersion);
            if (file != null && product != null && (file.Major != product.Major || file.Minor != product.Minor))
            {
                return Common;
            }

            Version version = product ?? file;
            return runtimeFlavor == RuntimeSku.CoreClr && version != null && version.Major >= 11
                ? CoreClr11
                : Common;
        }

        /// <summary>The short CoreLib module name used in symbols and captured-module lookup.</summary>
        public const string HostModuleName = RuntimeNames.CoreLib;

        /// <summary>Fallback support for unknown runtimes; not an alias for the newest supported runtime.</summary>
        public static AsyncProfilerRuntimeSupport Common => s_common;

        /// <summary>
        /// Support for CoreCLR 11.0 and later unless a newer implementation is selected.
        /// Currently shares the common classifiers, layouts, and transforms.
        /// </summary>
        public static AsyncProfilerRuntimeSupport CoreClr11 => s_coreClr11;

        /// <summary>A diagnostic name for the selected runtime support.</summary>
        public string Name { get; }

        /// <summary>Classifies a physical stack frame for async stitching and cleanup.</summary>
        /// <remarks>
        /// Kind is the shared stitching kind; RuntimeKind adds runtime-specific detail.
        /// Capture, emission, and resume frames share Kind = ProfilerEntry but have different RuntimeKind values.
        /// A frame can also have a RuntimeKind without a shared Kind.
        /// </remarks>
        internal readonly struct FrameClassification : IEquatable<FrameClassification>
        {
            public FrameClassification(AsyncProfilerStitchSyncFrameKind kind)
                : this(kind, RuntimeFrameKind.None)
            {
            }

            internal FrameClassification(AsyncProfilerStitchSyncFrameKind kind, RuntimeFrameKind runtimeKind)
            {
                bool isProfilerEntry =
                    runtimeKind == RuntimeFrameKind.ProfilerCapture ||
                    runtimeKind == RuntimeFrameKind.ProfilerEmission ||
                    runtimeKind == RuntimeFrameKind.ProfilerResume;
                if (kind == AsyncProfilerStitchSyncFrameKind.None && isProfilerEntry)
                {
                    kind = AsyncProfilerStitchSyncFrameKind.ProfilerEntry;
                }

                Kind = kind;
                RuntimeKind = runtimeKind;
            }

            /// <summary>The shared frame kind used by stitching and presentation.</summary>
            public AsyncProfilerStitchSyncFrameKind Kind { get; }

            /// <summary>The runtime-specific frame kind used by sequence matching and cleanup.</summary>
            internal RuntimeFrameKind RuntimeKind { get; }

            /// <summary>Opaque runtime classification data for compact frame storage.</summary>
            internal byte RuntimeData => (byte)RuntimeKind;

            /// <summary>True when either frame kind was identified.</summary>
            internal bool IsRecognized =>
                Kind != AsyncProfilerStitchSyncFrameKind.None || RuntimeKind != RuntimeFrameKind.None;

            /// <summary>Restores a classification from its shared kind and opaque runtime data.</summary>
            internal static FrameClassification FromRuntimeData(AsyncProfilerStitchSyncFrameKind kind, byte runtimeData) =>
                new FrameClassification(kind, (RuntimeFrameKind)runtimeData);

            public static implicit operator FrameClassification(AsyncProfilerStitchSyncFrameKind kind) =>
                new FrameClassification(kind);

            public static implicit operator FrameClassification(RuntimeFrameKind runtimeKind) =>
                new FrameClassification(AsyncProfilerStitchSyncFrameKind.None, runtimeKind);

            public bool Equals(FrameClassification other) => Kind == other.Kind && RuntimeKind == other.RuntimeKind;
            public override bool Equals(object obj) => obj is FrameClassification other && Equals(other);
            public override int GetHashCode() => ((int)Kind * 397) ^ (int)RuntimeKind;

            public static bool operator ==(FrameClassification left, FrameClassification right) => left.Equals(right);
            public static bool operator !=(FrameClassification left, FrameClassification right) => !left.Equals(right);
        }

        /// <summary>Runtime-specific frame kinds that supplement the shared stitching kinds.</summary>
        internal enum RuntimeFrameKind : byte
        {
            None,
            V1Startup,
            V1BuilderAwaitRegistration,
            V1AwaiterRegistration,
            V1AwaiterReadinessCheck,
            TaskContinuations,
            TaskResult,
            TaskCompletionSourceResult,
            SchedulerEntry,
            RuntimeInitialization,
            ProfilerCapture,
            ProfilerEmission,
            ProfilerResume,
        }

        /// <summary>Checks the CoreLib symbol prefix, not captured module ownership.</summary>
        internal static bool IsHostModuleQualifiedFrame(string frameName)
        {
            return AsyncProfilerMethodNames.IsModuleQualifiedFrame(frameName, HostModuleName);
        }

        /// <summary>Recognizes shared boundary contracts and runtime-specific V1 dispatcher helpers.</summary>
        public AsyncProfilerCallStackStitchBoundaryInfo ClassifyBoundary(string frameName)
        {
            AsyncProfilerCallStackStitchBoundaryKind kind = AsyncProfilerCallStackStitchBoundary.ClassifyContractBoundary(frameName, out int wrapperIndex);
            if (kind == AsyncProfilerCallStackStitchBoundaryKind.None && IsV1DispatcherInfrastructure(frameName))
            {
                kind = AsyncProfilerCallStackStitchBoundaryKind.V1DispatcherInfrastructure;
            }

            return new AsyncProfilerCallStackStitchBoundaryInfo(kind, wrapperIndex);
        }

        /// <summary>Checks for a native CoreCLR or JIT module beside the captured CoreLib.</summary>
        /// <summary>Checks for a native CoreCLR or JIT module beside the captured CoreLib.</summary>
        public virtual bool IsNativeRuntimeModule(TraceModuleFile module, TraceModuleFile hostModule)
        {
            if (module == null || hostModule == null || module.ImageBase == 0 || module.ManagedModule != null)
            {
                return false;
            }

            if (!IsNativeRuntimeModuleName(module.Name))
            {
                return false;
            }

            // Use captured paths, including when a trace is opened on a different operating system.
            string path = module.FilePath;
            string hostPath = hostModule.FilePath;
            int directoryEnd = GetDirectoryEnd(path);
            if (directoryEnd <= 0 || directoryEnd != GetDirectoryEnd(hostPath))
            {
                return false;
            }

            return string.Compare(path, 0, hostPath, 0, directoryEnd + 1, StringComparison.OrdinalIgnoreCase) == 0;
        }

        private static bool IsNativeRuntimeModuleName(string name)
        {
            return string.Equals(name, RuntimeNames.NativeModules.CoreClr, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, RuntimeNames.NativeModules.Jit, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, RuntimeNames.NativeModules.LinuxCoreClr, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, RuntimeNames.NativeModules.LinuxJit, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, RuntimeNames.NativeModules.MacCoreClr, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, RuntimeNames.NativeModules.MacJit, StringComparison.OrdinalIgnoreCase);
        }

        private static int GetDirectoryEnd(string path)
        {
            return Math.Max(path.LastIndexOf('\\'), path.LastIndexOf('/'));
        }

        /// <summary>Classifies generated async methods and runtime helpers, keeping specialized helper roles.</summary>
        /// <param name="frameName">The full method name, normalized for matching.</param>
        /// <param name="isHostModule">True when the method belongs to the captured CoreLib.</param>
        public virtual FrameClassification ClassifySynchronousFrame(string frameName, bool isHostModule)
        {
            if (string.IsNullOrEmpty(frameName))
            {
                return AsyncProfilerStitchSyncFrameKind.None;
            }

            string method = GetBareMethodName(frameName);
            if (IsStateMachineMoveNext(frameName, method))
            {
                return AsyncProfilerStitchSyncFrameKind.V1StateMachineBody;
            }

            // Generated resume stubs can belong to the application's module rather than CoreLib.
            if (method == RuntimeNames.DynamicMethods.AsyncResume && IsMethodOnType(frameName, RuntimeNames.DynamicMethods.Type, method))
            {
                return AsyncProfilerStitchSyncFrameKind.V2ResumeStub;
            }

            if (!isHostModule)
            {
                return AsyncProfilerStitchSyncFrameKind.None;
            }

            if (method == RuntimeNames.StateMachineBoxes.ExecutionContextCallback && IsCommonV1DispatcherInfrastructure(frameName))
            {
                return AsyncProfilerStitchSyncFrameKind.V1BoxCallback;
            }

            FrameClassification profiler = ClassifyProfilerInfrastructure(frameName, method);
            if (profiler.IsRecognized)
            {
                return profiler;
            }

            FrameClassification bridge = ClassifyTaskAndThreadingInfrastructure(frameName, method);
            if (bridge.IsRecognized)
            {
                return bridge;
            }

            if (!IsRuntimeCompilerServicesMethod(frameName))
            {
                return AsyncProfilerStitchSyncFrameKind.None;
            }

            string declaringType = GetDeclaringTypeName(frameName);
            if (declaringType is null)
            {
                return AsyncProfilerStitchSyncFrameKind.None;
            }

            string typeName = GetTypeNameWithoutArity(declaringType);
            FrameClassification awaiter = ClassifyAwaiterFrame(frameName, method, typeName);
            if (awaiter.IsRecognized)
            {
                return awaiter;
            }

            return ClassifyMethodBuilderFrame(frameName, method, typeName);
        }

        /// <summary>
        /// Extracts the state-machine type from a constructed V1 box's execution or callback method.
        /// The caller still resolves its identity and decides whether it can represent a resumed method.
        /// </summary>
        public virtual bool TryGetBoxedStateMachineTypeName(string frameName, bool isHostModule, out string stateMachineType)
        {
            stateMachineType = null;
            if (!isHostModule)
            {
                return false;
            }

            string method = GetBareMethodName(frameName);
            bool isBoxExecutionMethod =
                method == RuntimeNames.StateMachineBoxes.MoveNext ||
                method == RuntimeNames.StateMachineBoxes.InstrumentedMoveNext ||
                method == RuntimeNames.StateMachineBoxes.ExecutionContextCallback;
            if (!isBoxExecutionMethod || !IsCommonV1DispatcherInfrastructure(frameName))
            {
                return false;
            }

            string declaringType = GetDeclaringTypeName(frameName);
            int argumentsStart = declaringType.IndexOf('[');
            if (argumentsStart < 0)
            {
                return false;
            }

            string boxTypeName = declaringType.Substring(0, argumentsStart);
            if (!boxTypeName.EndsWith(RuntimeNames.StateMachineBoxes.GenericTypeSuffix, StringComparison.Ordinal))
            {
                return false;
            }

            // Nested boxes include their builder's arguments; TStateMachine is the final argument.
            return TryGetLastTypeArgument(declaringType, out stateMachineType);
        }

        /// <summary>
        /// Supplies the implementation of one built-in presentation pass.
        /// Overrides should cache delegates and preserve shared ownership, identity, and context checks.
        /// The pipeline controls pass order, enablement, and custom callbacks.
        /// </summary>
        public virtual AsyncProfilerCallStackTransform GetTransform(AsyncProfilerRuntimeTransformKind kind)
        {
            switch (kind)
            {
                case AsyncProfilerRuntimeTransformKind.SampledV2Wrapper: return s_sampledV2Wrapper;
                case AsyncProfilerRuntimeTransformKind.ReentrantHandoff: return s_reentrantHandoff;
                case AsyncProfilerRuntimeTransformKind.V1Presentation: return s_v1Presentation;
                case AsyncProfilerRuntimeTransformKind.InfrastructureCleanup: return s_infrastructureCleanup;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        /// <summary>
        /// Matches consecutive dispatcher helper frames after a matched boundary.
        /// The match must not include another boundary. The stitcher checks bounds and module ownership.
        /// </summary>
        public virtual AsyncProfilerRuntimeSequenceMatch MatchDispatchInfrastructure(
            AsyncCallstackKind kind, IReadOnlyList<AsyncProfilerStitchSyncFrame> frames, int start, int end,
            Func<CodeAddressIndex, AsyncProfilerCallStackStitchBoundaryInfo> classify)
        {
            AsyncProfilerCallStackStitchBoundaryKind expected =
                kind == AsyncCallstackKind.StateMachineAsync
                ? AsyncProfilerCallStackStitchBoundaryKind.V1DispatcherInfrastructure
                : AsyncProfilerCallStackStitchBoundaryKind.V2DispatcherInfrastructure;

            int next = start;
            while (next < end && classify(frames[next].CodeAddress).Kind == expected)
            {
                next++;
            }

            return next == start ? default : AsyncProfilerRuntimeSequenceMatch.Match(next - start);
        }

        /// <summary>
        /// Identifies helpers eligible for V1 return-unwind cleanup after completion is recorded.
        /// The transform still checks captured method order, box identity, and runtime ownership.
        /// </summary>
        public virtual bool IsV1CompletionUnwindInfrastructure(
            AsyncProfilerStitchedFrame frame, AsyncProfilerCallStackStitchBoundaryKind boundary) =>
            (boundary == AsyncProfilerCallStackStitchBoundaryKind.V1DispatcherInfrastructure && frame.BoxedStateMachineMethod != MethodIndex.Invalid) ||
            frame.SyncFrameKind == AsyncProfilerStitchSyncFrameKind.ContinuationBridge ||
            frame.SyncClassification.RuntimeKind == RuntimeFrameKind.TaskContinuations ||
            frame.SyncClassification.RuntimeKind == RuntimeFrameKind.TaskResult ||
            frame.SyncFrameKind == AsyncProfilerStitchSyncFrameKind.V1MethodCompletion ||
            frame.SyncFrameKind == AsyncProfilerStitchSyncFrameKind.AwaiterHelper;

        /// <summary>Identifies the scheduler frame retained root-ward of an optional resumption bridge.</summary>
        public virtual bool IsRuntimeResumptionAnchor(AsyncProfilerStitchedFrame frame) =>
            IsHelperFrame(frame, RuntimeFrameKind.SchedulerEntry);

        /// <summary>
        /// Identifies helpers that may extend an already verified dispatcher span.
        /// Cleanup still requires contiguous frames with the same captured runtime and call-stack context.
        /// </summary>
        public virtual bool IsProfilerDispatchInfrastructure(FrameClassification classification) =>
            classification.Kind == AsyncProfilerStitchSyncFrameKind.ProfilerEntry ||
            classification.Kind == AsyncProfilerStitchSyncFrameKind.ProfilerHelper ||
            classification.Kind == AsyncProfilerStitchSyncFrameKind.V1BoxCallback ||
            classification.RuntimeKind == RuntimeFrameKind.V1AwaiterRegistration ||
            classification.Kind == AsyncProfilerStitchSyncFrameKind.AwaiterHelper ||
            classification.Kind == AsyncProfilerStitchSyncFrameKind.FaultHandling;

        /// <summary>Identifies individually removable helpers outside protected or rejected sequences.</summary>
        public virtual bool IsOptionalInfrastructureFrame(
            AsyncProfilerStitchedFrame frame, AsyncProfilerCallStackStitchBoundaryKind boundary) =>
            boundary == AsyncProfilerCallStackStitchBoundaryKind.V1Dispatcher ||
            boundary == AsyncProfilerCallStackStitchBoundaryKind.V1DispatcherInfrastructure ||
            boundary == AsyncProfilerCallStackStitchBoundaryKind.V2Dispatcher ||
            boundary == AsyncProfilerCallStackStitchBoundaryKind.V2DispatcherInfrastructure ||
            frame.SyncClassification.RuntimeKind == RuntimeFrameKind.V1Startup ||
            frame.SyncClassification.RuntimeKind == RuntimeFrameKind.V1BuilderAwaitRegistration ||
            frame.SyncFrameKind == AsyncProfilerStitchSyncFrameKind.V1MethodHelper ||
            frame.SyncFrameKind == AsyncProfilerStitchSyncFrameKind.V1MethodCompletion ||
            frame.SyncClassification.RuntimeKind == RuntimeFrameKind.V1AwaiterRegistration ||
            frame.SyncClassification.RuntimeKind == RuntimeFrameKind.V1AwaiterReadinessCheck ||
            frame.SyncFrameKind == AsyncProfilerStitchSyncFrameKind.V1DispatchSetup ||
            frame.SyncFrameKind == AsyncProfilerStitchSyncFrameKind.ContinuationBridge ||
            frame.SyncClassification.RuntimeKind == RuntimeFrameKind.TaskContinuations ||
            frame.SyncClassification.RuntimeKind == RuntimeFrameKind.TaskResult;

        /// <summary>
        /// Matches a helper sequence in [start, end).
        /// Optional matching allows extra runtime helpers for presentation only.
        /// Rejected sequences are protected from individual cleanup.
        /// The caller still verifies runtime and call-stack ownership.
        /// </summary>
        public AsyncProfilerRuntimeSequenceMatch MatchSequence(
            AsyncProfilerRuntimeSequenceKind kind, AsyncProfilerCallStackTransformState context, int start, int end,
            bool allowRuntimeInteriors = false)
        {
            AsyncProfilerRuntimeSequenceMatch match;
            switch (kind)
            {
                case AsyncProfilerRuntimeSequenceKind.TaskCompletion:
                    match = MatchTaskCompletion(context, start, end);
                    break;
                case AsyncProfilerRuntimeSequenceKind.TaskExceptionCompletion:
                    match = default;
                    break;
                case AsyncProfilerRuntimeSequenceKind.TaskCompletionChain:
                    match = MatchTaskCompletionChain(context, start, end);
                    break;
                case AsyncProfilerRuntimeSequenceKind.V2CompletionHandoff:
                    match = MatchV2CompletionHandoff(context, start, end);
                    break;
                case AsyncProfilerRuntimeSequenceKind.V1Startup:
                    match = MatchV1Startup(context, start, end);
                    break;
                case AsyncProfilerRuntimeSequenceKind.V1AwaitRegistration:
                    match = MatchV1AwaitRegistration(context, start, end);
                    break;
                case AsyncProfilerRuntimeSequenceKind.ProfilerEntry:
                    match = MatchProfilerEntry(context, start, end);
                    break;
                case AsyncProfilerRuntimeSequenceKind.RuntimeResumptionBridge:
                    match = MatchRuntimeResumptionBridge(context, start, end);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind));
            }

            // Both matches and rejections have a length; only an empty result may try optional matching.
            if (!allowRuntimeInteriors || match.Length != 0)
            {
                return match;
            }

            switch (kind)
            {
                case AsyncProfilerRuntimeSequenceKind.TaskCompletion:
                    return MatchOptionalTaskCompletion(context, start, end);
                case AsyncProfilerRuntimeSequenceKind.TaskExceptionCompletion:
                    return MatchOptionalTaskExceptionCompletion(context, start, end);
                case AsyncProfilerRuntimeSequenceKind.TaskCompletionChain:
                    return MatchOptionalTaskCompletionChain(context, start, end);
                case AsyncProfilerRuntimeSequenceKind.V1AwaitRegistration:
                    return MatchOptionalV1AwaitRegistration(context, start, end);
                default:
                    return match;
            }
        }

        /// <summary>
        /// Identifies which optional helper sequence to try for this root frame.
        /// The caller must still match the sequence and verify ownership before cleanup.
        /// </summary>
        public virtual bool TryGetOptionalSequenceKind(
            string rootName, FrameClassification classification, out AsyncProfilerRuntimeSequenceKind sequence)
        {
            sequence = default;
            string method = GetBareMethodName(rootName);
            bool isSetResult = method == RuntimeNames.ValueTaskSources.SetResult;
            bool isSetException = method == RuntimeNames.ValueTaskSources.SetException;
            bool isValueTaskSourceCompletion = false;
            if (isSetResult || isSetException)
            {
                isValueTaskSourceCompletion =
                    IsMethodOnType(rootName, RuntimeNames.ValueTaskSources.CoreType, method) ||
                    IsPooledValueTaskBoxMethod(rootName, method);
            }

            if (classification.RuntimeKind == RuntimeFrameKind.TaskResult || (isSetResult && isValueTaskSourceCompletion))
            {
                sequence = AsyncProfilerRuntimeSequenceKind.TaskCompletion;
                return true;
            }

            bool isBuilderCompletion = classification.Kind == AsyncProfilerStitchSyncFrameKind.V1MethodHelper;
            if (isSetException && (isBuilderCompletion || isValueTaskSourceCompletion))
            {
                sequence = AsyncProfilerRuntimeSequenceKind.TaskExceptionCompletion;
                return true;
            }

            if (classification.RuntimeKind == RuntimeFrameKind.TaskCompletionSourceResult)
            {
                sequence = AsyncProfilerRuntimeSequenceKind.TaskCompletionChain;
                return true;
            }

            bool isTaskAwaiterRegistration =
                IsMethodOnType(rootName, RuntimeNames.Awaiters.TaskType, RuntimeNames.Awaiters.UnsafeOnCompletedInternal) ||
                IsMethodOnType(rootName, RuntimeNames.Awaiters.TaskType, RuntimeNames.Awaiters.OnCompletedInternal);

            bool isBoxAwareAwaiterRegistration =
                classification.RuntimeKind == RuntimeFrameKind.V1AwaiterRegistration &&
                IsKnownStateMachineBoxAwareAwaiter(rootName);
            if (isTaskAwaiterRegistration || isBoxAwareAwaiterRegistration)
            {
                sequence = AsyncProfilerRuntimeSequenceKind.V1AwaitRegistration;
                return true;
            }

            return false;
        }

        /// <summary>Recognizes V1 box/dispatcher helpers separately from shared splice-boundary contracts.</summary>
        protected virtual bool IsV1DispatcherInfrastructure(string frameName) =>
            IsCommonV1DispatcherInfrastructure(frameName);

        /// <summary>
        /// Matches a generated MoveNext followed by builder startup helpers, stopping before the application caller.
        /// </summary>
        protected virtual AsyncProfilerRuntimeSequenceMatch MatchV1Startup(
            AsyncProfilerCallStackTransformState context, int start, int end)
        {
            if (start == end || !IsSyncFrameKind(context.Frames[start], AsyncProfilerStitchSyncFrameKind.V1StateMachineBody))
            {
                return default;
            }

            int next = start + 1;
            while (next < end && IsHelperFrame(context.Frames[next], RuntimeFrameKind.V1Startup))
            {
                next++;
            }

            return AsyncProfilerRuntimeSequenceMatch.Match(next - start);
        }

        /// <summary>Matches contiguous builder and awaiter registration helpers.</summary>
        protected virtual AsyncProfilerRuntimeSequenceMatch MatchV1AwaitRegistration(
            AsyncProfilerCallStackTransformState context, int start, int end)
        {
            int next = start;
            while (next < end)
            {
                AsyncProfilerStitchedFrame frame = context.Frames[next];
                bool isRegistration =
                    IsHelperFrame(frame, RuntimeFrameKind.V1BuilderAwaitRegistration) ||
                    IsHelperFrame(frame, RuntimeFrameKind.V1AwaiterRegistration);
                if (!isRegistration)
                {
                    break;
                }

                next++;
            }

            return next == start ? default : AsyncProfilerRuntimeSequenceMatch.Match(next - start);
        }

        /// <summary>Matches profiler-entry helpers immediately before a physical or logical target.</summary>
        protected virtual AsyncProfilerRuntimeSequenceMatch MatchProfilerEntry(
            AsyncProfilerCallStackTransformState context, int start, int end)
        {
            if (start == end || end == context.Frames.Count)
            {
                return default;
            }

            AsyncProfilerStitchedFrame target = context.Frames[end];
            if (IsSyncFrameKind(context.Frames[start], AsyncProfilerStitchSyncFrameKind.V1DispatchSetup))
            {
                bool hasV1Target =
                    target.Origin == AsyncProfilerStitchedFrameOrigin.Sync ||
                    (target.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent &&
                     target.Segment.Kind == AsyncCallstackKind.StateMachineAsync);
                if (!hasV1Target)
                {
                    return default;
                }

                AsyncProfilerRuntimeSequenceMatch registration = context.MatchRuntimeSequence(AsyncProfilerRuntimeSequenceKind.V1AwaitRegistration, start + 1, end);
                return registration.IsMatch && registration.Length == end - start - 1
                    ? AsyncProfilerRuntimeSequenceMatch.Match(end - start)
                    : AsyncProfilerRuntimeSequenceMatch.Reject(end - start);
            }

            bool isDispatchTarget = target.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget && !target.IsStateMachineBoxCarrier;
            bool hasProfilerEntry =
                IsHelperFrame(context.Frames[start], RuntimeFrameKind.ProfilerResume) ||
                (isDispatchTarget && IsHelperFrame(context.Frames[start], RuntimeFrameKind.ProfilerCapture));
            if (!hasProfilerEntry)
            {
                return default;
            }

            if (target.IsStateMachineBoxCarrier && end - start == 2)
            {
                AsyncProfilerStitchedFrame carrier = context.Frames[start + 1];
                bool hasMatchingCarrier =
                    carrier.Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
                    carrier.Presentation == AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames &&
                    carrier.CodeAddress == target.CodeAddress &&
                    carrier.BoxedStateMachineMethod == target.BoxedStateMachineMethod &&
                    ReferenceEquals(carrier.Context, target.Context);
                return hasMatchingCarrier ? AsyncProfilerRuntimeSequenceMatch.Match(2) : default;
            }

            int length = isDispatchTarget ? 2 : 1;
            if (end - start != length)
            {
                return default;
            }

            bool hasExpectedTarget = !isDispatchTarget ||
                (target.Segment.Kind == AsyncCallstackKind.StateMachineAsync &&
                 context.ClassifyBoundary(context.Frames[start + 1].CodeAddress).Kind ==
                    AsyncProfilerCallStackStitchBoundaryKind.V1Dispatcher);

            return hasExpectedTarget ? AsyncProfilerRuntimeSequenceMatch.Match(length) : default;
        }

        /// <summary>
        /// Matches task-completion or resumption helpers followed by a scheduler frame.
        /// A completion match can stop before a caller outside CoreLib.
        /// Unverified helper sequences are rejected so cleanup leaves them unchanged.
        /// </summary>
        protected virtual AsyncProfilerRuntimeSequenceMatch MatchRuntimeResumptionBridge(
            AsyncProfilerCallStackTransformState context, int start, int end)
        {
            bool hasSchedulerAnchor = start < end && IsRuntimeResumptionAnchor(context.Frames[end - 1]);
            int helpersEnd = hasSchedulerAnchor ? end - 1 : end;
            bool hasCandidate = false;
            bool hasMatchedCompletion = false;
            bool hasRejectedCompletion = false;
            for (int i = start; i < helpersEnd; i++)
            {
                AsyncProfilerStitchedFrame frame = context.Frames[i];

                hasCandidate |=
                    frame.SyncFrameKind == AsyncProfilerStitchSyncFrameKind.ContinuationBridge ||
                    frame.SyncClassification.RuntimeKind == RuntimeFrameKind.TaskContinuations ||
                    frame.SyncClassification.RuntimeKind == RuntimeFrameKind.TaskResult;

                AsyncProfilerRuntimeSequenceMatch pair = context.MatchRuntimeSequence(AsyncProfilerRuntimeSequenceKind.TaskCompletion, i, end);

                hasCandidate |= pair.IsMatch || pair.IsRejected;
                hasMatchedCompletion |= pair.IsMatch;
                hasRejectedCompletion |= pair.IsRejected;
            }

            if (hasSchedulerAnchor && hasMatchedCompletion && !hasRejectedCompletion)
            {
                int completionStart = start;
                while (completionStart < helpersEnd)
                {
                    AsyncProfilerStitchedFrame frame = context.Frames[completionStart];
                    bool isAwaiterOrBridge =
                        IsSyncFrameKind(frame, AsyncProfilerStitchSyncFrameKind.AwaiterHelper) ||
                        IsSyncFrameKind(frame, AsyncProfilerStitchSyncFrameKind.ContinuationBridge);
                    if (!isAwaiterOrBridge)
                    {
                        break;
                    }

                    completionStart++;
                }

                AsyncProfilerRuntimeSequenceMatch chain = context.MatchRuntimeSequence(AsyncProfilerRuntimeSequenceKind.TaskCompletionChain, completionStart, helpersEnd);
                int caller = completionStart + chain.Length;
                bool hasCaller = chain.IsMatch && caller < helpersEnd && context.TraceLog != null;
                if (hasCaller && context.GetHostModuleOwner(context.Frames[caller].CodeAddress) == ModuleFileIndex.Invalid)
                {
                    return AsyncProfilerRuntimeSequenceMatch.Match(caller - start);
                }

                return AsyncProfilerRuntimeSequenceMatch.Match(end - start);
            }

            if (context.TraceLog != null && context.Segments.Count != 0)
            {
                AsyncProfilerRuntimeSequenceMatch helpers = MatchDispatchResumptionHelpers(context, start, helpersEnd);
                if (helpers.Length != 0)
                {
                    AsyncProfilerCallStack rootContext = context.Segments[context.Segments.Count - 1];
                    bool hasCompleteHelperSpan =
                        hasSchedulerAnchor && !hasRejectedCompletion &&
                        helpers.IsMatch && helpers.Length == helpersEnd - start;
                    bool hasVerifiedRoot =
                        hasCompleteHelperSpan &&
                        (context.HasVerifiedDispatchEntry(rootContext, context.Frames[start].CodeAddress) ||
                         context.HasVerifiedV1CompletionAnchor(rootContext, context.Frames[start].CodeAddress, allowCompletionUnwind: true) ||
                         context.HasVerifiedCompletedV1OuterDispatch(rootContext, context.Frames[start].CodeAddress));

                    return hasVerifiedRoot ? AsyncProfilerRuntimeSequenceMatch.Match(end - start) : AsyncProfilerRuntimeSequenceMatch.Reject(end - start);
                }
            }

            return hasCandidate ? AsyncProfilerRuntimeSequenceMatch.Reject(end - start) : default;
        }

        /// <summary>
        /// Matches a span made entirely of YieldAwaitable-family helpers before the scheduler frame.
        /// The caller verifies the root call stack's V1 dispatcher or V2 dispatch pair.
        /// </summary>
        protected virtual AsyncProfilerRuntimeSequenceMatch MatchDispatchResumptionHelpers(
            AsyncProfilerCallStackTransformState context, int start, int end)
        {
            int matched = 0;
            for (int i = start; i < end; i++)
            {
                CodeAddressIndex address = context.Frames[i].CodeAddress;
                if (address != CodeAddressIndex.Invalid && IsYieldAwaitableMethod(context.TraceLog.CodeAddresses[address].FullMethodName))
                {
                    matched++;
                }
            }

            if (matched == 0)
            {
                return default;
            }

            return matched == end - start ? AsyncProfilerRuntimeSequenceMatch.Match(matched) : AsyncProfilerRuntimeSequenceMatch.Reject(end - start);
        }

        /// <summary>
        /// Matches RunContinuations followed by TrySetResult in leaf-to-root order.
        /// Overrides may recognize a longer sequence; completion chains and V2 handoffs reuse its length.
        /// </summary>
        protected virtual AsyncProfilerRuntimeSequenceMatch MatchTaskCompletion(
            AsyncProfilerCallStackTransformState context, int start, int end) =>
            end - start >= 2 &&
            IsHelperFrame(context.Frames[start], RuntimeFrameKind.TaskContinuations) &&
            IsHelperFrame(context.Frames[start + 1], RuntimeFrameKind.TaskResult)
                ? AsyncProfilerRuntimeSequenceMatch.Match(2)
                : default;

        /// <summary>Matches a completion sequence followed by TaskCompletionSource.SetResult.</summary>
        protected virtual AsyncProfilerRuntimeSequenceMatch MatchTaskCompletionChain(
            AsyncProfilerCallStackTransformState context, int start, int end)
        {
            AsyncProfilerRuntimeSequenceMatch pair = MatchTaskCompletion(context, start, end);
            if (!pair.IsMatch)
            {
                return pair.IsRejected ? AsyncProfilerRuntimeSequenceMatch.Reject(Math.Min(end - start, pair.Length + 1)) : default;
            }

            int trigger = start + pair.Length;
            return trigger < end && IsHelperFrame(context.Frames[trigger], RuntimeFrameKind.TaskCompletionSourceResult)
                ? AsyncProfilerRuntimeSequenceMatch.Match(pair.Length + 1)
                : default;
        }

        /// <summary>
        /// Matches a continuation bridge, a completion sequence, and two distinct V2 dispatch methods.
        /// This completed V2 layout does not prove ownership of an active V1 call stack or its caller.
        /// </summary>
        protected virtual AsyncProfilerRuntimeSequenceMatch MatchV2CompletionHandoff(
            AsyncProfilerCallStackTransformState context, int start, int end)
        {
            if (start == end || !IsSyncFrameKind(context.Frames[start], AsyncProfilerStitchSyncFrameKind.ContinuationBridge))
            {
                return default;
            }

            AsyncProfilerRuntimeSequenceMatch pair = MatchTaskCompletion(context, start + 1, end);
            if (!pair.IsMatch)
            {
                if (pair.IsRejected)
                {
                    return AsyncProfilerRuntimeSequenceMatch.Reject(Math.Min(end - start, pair.Length + 3));
                }

                return default;
            }

            int dispatch = start + 1 + pair.Length;
            if (end - dispatch < 2)
            {
                return default;
            }

            AsyncProfilerStitchedFrame firstDispatch = context.Frames[dispatch];
            AsyncProfilerStitchedFrame secondDispatch = context.Frames[dispatch + 1];
            if (firstDispatch.Origin != AsyncProfilerStitchedFrameOrigin.Sync || secondDispatch.Origin != AsyncProfilerStitchedFrameOrigin.Sync)
            {
                return default;
            }

            if (context.ClassifyBoundary(firstDispatch.CodeAddress).Kind != AsyncProfilerCallStackStitchBoundaryKind.V2DispatcherInfrastructure ||
                context.ClassifyBoundary(secondDispatch.CodeAddress).Kind != AsyncProfilerCallStackStitchBoundaryKind.V2DispatcherInfrastructure)
            {
                return default;
            }

            if (firstDispatch.Method == secondDispatch.Method)
            {
                return default;
            }

            return AsyncProfilerRuntimeSequenceMatch.Match(pair.Length + 3);
        }

        /// <summary>Matches Task or value-task-source completion with optional intervening runtime frames.</summary>
        protected virtual AsyncProfilerRuntimeSequenceMatch MatchOptionalTaskCompletion(
            AsyncProfilerCallStackTransformState context, int start, int end)
        {
            if (context.TraceLog == null || end - start < 2)
            {
                return default;
            }

            if (IsHelperFrame(context.Frames[end - 1], RuntimeFrameKind.TaskResult))
            {
                if (!IsHelperFrame(context.Frames[start], RuntimeFrameKind.TaskContinuations))
                {
                    return default;
                }

                return AsyncProfilerRuntimeSequenceMatch.Match(end - start);
            }

            return MatchOptionalValueTaskSourceCompletion(context, start, end, RuntimeNames.ValueTaskSources.SetResult);
        }

        /// <summary>Matches fault/cancellation helpers for presentation, not structural completion evidence.</summary>
        protected virtual AsyncProfilerRuntimeSequenceMatch MatchOptionalTaskExceptionCompletion(
            AsyncProfilerCallStackTransformState context, int start, int end)
        {
            if (context.TraceLog == null || end - start < 2)
            {
                return default;
            }

            AsyncProfilerRuntimeSequenceMatch source = MatchOptionalValueTaskSourceCompletion(context, start, end, RuntimeNames.ValueTaskSources.SetException);
            if (source.IsMatch)
            {
                return source;
            }

            bool isBuildHelper = IsSyncFrameKind(context.Frames[end - 1], AsyncProfilerStitchSyncFrameKind.V1MethodHelper);
            bool isSetException = GetBareMethodName(GetFrameName(context, end - 1)) == RuntimeNames.Builders.SetException;
            if (end - start < 3 || !isBuildHelper || !isSetException)
            {
                return default;
            }

            string leaf = GetFrameName(context, start);
            bool continuation = IsHelperFrame(context.Frames[start], RuntimeFrameKind.TaskContinuations);
            bool canceled = IsMethodOnType(leaf, RuntimeNames.TaskExceptionHolder.Type, RuntimeNames.TaskExceptionHolder.SetCancellationException);
            if (!continuation && !canceled)
            {
                bool faulted = IsMethodOnType(leaf, RuntimeNames.TaskExceptionHolder.Type, RuntimeNames.TaskExceptionHolder.AddFaultException);
                if (!faulted)
                {
                    return default;
                }
            }

            string completionMethod = canceled ? RuntimeNames.Task.TrySetCanceled : RuntimeNames.Task.TrySetException;
            for (int i = start + 1; i < end - 1; i++)
            {
                string name = GetFrameName(context, i);
                bool isCompletion = IsTaskMethod(name, completionMethod);
                bool isContinuationCancel = continuation && IsTaskMethod(name, RuntimeNames.Task.TrySetCanceled);
                if (isCompletion || isContinuationCancel)
                {
                    return AsyncProfilerRuntimeSequenceMatch.Match(end - start);
                }
            }

            return default;
        }

        /// <summary>Matches a wider Task completion sequence ending at TaskCompletionSource.SetResult.</summary>
        protected virtual AsyncProfilerRuntimeSequenceMatch MatchOptionalTaskCompletionChain(
            AsyncProfilerCallStackTransformState context, int start, int end)
        {
            if (end - start < 4 ||
                !IsHelperFrame(context.Frames[end - 1], RuntimeFrameKind.TaskCompletionSourceResult) ||
                !IsHelperFrame(context.Frames[start], RuntimeFrameKind.TaskContinuations) ||
                !IsHelperFrame(context.Frames[end - 2], RuntimeFrameKind.TaskResult))
            {
                return default;
            }

            AsyncProfilerRuntimeSequenceMatch pair = context.MatchRuntimeSequence(AsyncProfilerRuntimeSequenceKind.TaskCompletion, start, end - 1, allowRuntimeInteriors: true);
            if (pair.IsRejected)
            {
                return AsyncProfilerRuntimeSequenceMatch.Reject(end - start);
            }

            bool hasCompletePair = pair.IsMatch && pair.Length == end - start - 1;
            return hasCompletePair ? AsyncProfilerRuntimeSequenceMatch.Match(end - start) : default;
        }

        /// <summary>
        /// Matches CoreLib await-registration helpers for Task and pooled ValueTask.
        /// Does not match custom value-task sources.
        /// </summary>
        protected virtual AsyncProfilerRuntimeSequenceMatch MatchOptionalV1AwaitRegistration(
            AsyncProfilerCallStackTransformState context, int start, int end)
        {
            if (context.TraceLog == null || end - start < 3)
            {
                return default;
            }

            if (MatchV1AwaitRegistration(context, end - 1, end).IsRejected)
            {
                return AsyncProfilerRuntimeSequenceMatch.Reject(end - start);
            }

            string leaf = GetFrameName(context, start);
            string root = GetFrameName(context, end - 1);
            bool isTaskContinuation = IsTaskMethod(leaf, RuntimeNames.Task.AddTaskContinuation);
            bool isTaskAwaiterRegistration =
                IsMethodOnType(root, RuntimeNames.Awaiters.TaskType, RuntimeNames.Awaiters.UnsafeOnCompletedInternal) ||
                IsMethodOnType(root, RuntimeNames.Awaiters.TaskType, RuntimeNames.Awaiters.OnCompletedInternal);
            bool isTaskRegistration = isTaskContinuation && isTaskAwaiterRegistration;

            bool isValueTaskSourceRegistration = IsMethodOnType(leaf, RuntimeNames.ValueTaskSources.CoreType, RuntimeNames.ValueTaskSources.OnCompleted);
            bool isPooledRegistration = isValueTaskSourceRegistration && IsKnownStateMachineBoxAwareAwaiter(root);
            if (!isTaskRegistration && !isPooledRegistration)
            {
                return default;
            }

            for (int i = start + 1; i < end - 1; i++)
            {
                string name = GetFrameName(context, i);
                if (isTaskRegistration)
                {
                    bool isContinuationHelper =
                        IsTaskMethod(name, RuntimeNames.Task.SetContinuationForAwait) ||
                        IsTaskMethod(name, RuntimeNames.Task.UnsafeSetContinuationForAwait);
                    if (isContinuationHelper)
                    {
                        return AsyncProfilerRuntimeSequenceMatch.Match(end - start);
                    }
                }

                if (isPooledRegistration && IsPooledValueTaskBoxMethod(name, RuntimeNames.ValueTaskSources.OnCompleted))
                {
                    return AsyncProfilerRuntimeSequenceMatch.Match(end - start);
                }
            }

            return default;
        }

        #region private

        /// <summary>Classifies registration and readiness checks before general TaskAwaiter helpers.</summary>
        private static FrameClassification ClassifyAwaiterFrame(string frameName, string method, string typeName)
        {
            bool isKnownAwaiter = IsKnownV1AwaiterType(typeName) || IsKnownStateMachineBoxAwareAwaiter(frameName);
            bool isAwaiterRegistration = isKnownAwaiter && IsAwaiterRegistrationMethod(method);
            if (isAwaiterRegistration)
            {
                return RuntimeFrameKind.V1AwaiterRegistration;
            }

            bool isYieldRegistration = (method == RuntimeNames.Awaiters.QueueContinuation || method == RuntimeNames.Awaiters.OutputCorrelationEtwEvent) &&
                IsMethodOnType(frameName, RuntimeNames.Awaiters.YieldAwaiterType, method);
            if (isYieldRegistration)
            {
                return RuntimeFrameKind.V1AwaiterRegistration;
            }

            if (IsAwaiterReadinessCheck(frameName, method))
            {
                return RuntimeFrameKind.V1AwaiterReadinessCheck;
            }

            return ClassifyAwaiterInfrastructure(frameName);
        }

        private static bool IsAwaiterReadinessCheck(string frameName, string method) =>
            method == RuntimeNames.Awaiters.IsCompleted &&
            (IsMethodOnType(frameName, RuntimeNames.Awaiters.TaskType, method) ||
             IsMethodOnType(frameName, RuntimeNames.Awaiters.GenericTaskType, method) ||
             IsMethodOnType(frameName, RuntimeNames.Awaiters.ValueTaskType, method) ||
             IsMethodOnType(frameName, RuntimeNames.Awaiters.GenericValueTaskType, method) ||
             IsMethodOnType(frameName, RuntimeNames.Awaiters.ConfiguredTaskType, method) ||
             IsMethodOnType(frameName, RuntimeNames.Awaiters.GenericConfiguredTaskType, method) ||
             IsMethodOnType(frameName, RuntimeNames.Awaiters.GenericTaskAwaitableType, method, RuntimeNames.Awaiters.ConfiguredTask) ||
             IsMethodOnType(frameName, RuntimeNames.Awaiters.ConfiguredValueTaskType, method) ||
             IsMethodOnType(frameName, RuntimeNames.Awaiters.GenericConfiguredValueTaskType, method) ||
             IsMethodOnType(frameName, RuntimeNames.Awaiters.GenericValueTaskAwaitableType, method, RuntimeNames.Awaiters.ConfiguredValueTask));

        /// <summary>Separates builder completion, await registration, startup, and other builder helpers.</summary>
        private static FrameClassification ClassifyMethodBuilderFrame(
            string frameName, string method, string typeName)
        {
            bool isTaskCompletion = method == RuntimeNames.Builders.SetExistingTaskResult && typeName == RuntimeNames.Builders.Task;
            bool isValueTaskCompletion = method == RuntimeNames.Builders.SetResult &&
                (IsMethodOnType(frameName, RuntimeNames.Builders.ValueTaskType, method) ||
                 IsMethodOnType(frameName, RuntimeNames.Builders.GenericValueTaskType, method));
            if (isTaskCompletion || isValueTaskCompletion)
            {
                return AsyncProfilerStitchSyncFrameKind.V1MethodCompletion;
            }

            if (!IsKnownV1MethodBuilderType(typeName))
            {
                return AsyncProfilerStitchSyncFrameKind.None;
            }

            if (method == RuntimeNames.Builders.AwaitUnsafeOnCompleted)
            {
                return RuntimeFrameKind.V1BuilderAwaitRegistration;
            }

            return method == RuntimeNames.Builders.Start ? (FrameClassification)RuntimeFrameKind.V1Startup : AsyncProfilerStitchSyncFrameKind.V1MethodHelper;
        }

        private static bool IsHelperFrame(AsyncProfilerStitchedFrame frame, RuntimeFrameKind helper) =>
            frame.Origin == AsyncProfilerStitchedFrameOrigin.Sync && frame.SyncClassification.RuntimeKind == helper;

        private static AsyncProfilerRuntimeSequenceMatch MatchOptionalValueTaskSourceCompletion(
            AsyncProfilerCallStackTransformState context, int start, int end, string method)
        {
            string root = GetFrameName(context, end - 1);
            bool source = IsMethodOnType(root, RuntimeNames.ValueTaskSources.CoreType, method);
            if (!source && !IsPooledValueTaskBoxMethod(root, method))
            {
                return default;
            }

            string leaf = GetFrameName(context, start);
            if (!IsMethodOnType(leaf, RuntimeNames.ValueTaskSources.CoreType, RuntimeNames.ValueTaskSources.SignalCompletion) &&
                !IsMethodOnType(leaf, RuntimeNames.ValueTaskSources.SharedType, RuntimeNames.ValueTaskSources.InvokeContinuationWithContext))
            {
                return default;
            }

            if (source)
            {
                return AsyncProfilerRuntimeSequenceMatch.Match(end - start);
            }

            for (int i = start + 1; i < end - 1; i++)
            {
                if (IsMethodOnType(GetFrameName(context, i), RuntimeNames.ValueTaskSources.CoreType, method))
                {
                    return AsyncProfilerRuntimeSequenceMatch.Match(end - start);
                }
            }

            return default;
        }

        private static string GetFrameName(AsyncProfilerCallStackTransformState context, int index) =>
            AsyncProfilerMethodNames.NormalizeMethodName(context.TraceLog.CodeAddresses[context.Frames[index].CodeAddress].FullMethodName);

        private static bool IsTaskMethod(string frameName, string method) =>
            IsMethodOnType(frameName, RuntimeNames.Task.Type, method) ||
            IsMethodOnType(frameName, RuntimeNames.Task.GenericType, method);

        private static bool IsPooledValueTaskBoxMethod(string frameName, string method) =>
            IsMethodOnType(frameName, RuntimeNames.Builders.PooledBoxType, method) ||
            IsMethodOnType(frameName, RuntimeNames.Builders.GenericPoolingValueTaskType, method, RuntimeNames.Builders.PooledBoxName);

        private static bool IsYieldAwaitableMethod(string methodName) =>
            IsMethodOnTypeOrNestedType(NormalizeMethodName(methodName), RuntimeNames.Awaiters.YieldAwaitableType);

        // Carrier/helper defaults remain independent of overrides to boundary-infrastructure recognition.
        private static bool IsCommonV1DispatcherInfrastructure(string frameName)
        {
            if (string.IsNullOrEmpty(frameName))
            {
                return false;
            }

            int qualifiedStart = frameName.IndexOf('!');
            qualifiedStart = qualifiedStart >= 0 ? qualifiedStart + 1 : 0;
            if (!HasCompilerServicesNamespaceAt(frameName, qualifiedStart))
            {
                return false;
            }

            string declaringType = GetDeclaringTypeName(frameName);
            if (declaringType is null)
            {
                return false;
            }

            return IsV1DispatcherTypeName(declaringType);
        }

        private static bool IsV1DispatcherImplementingType(string frameName)
        {
            if (!IsRuntimeCompilerServicesMethod(frameName))
            {
                return false;
            }

            int typeStart = (IsHostModuleQualifiedFrame(frameName) ? HostModuleName.Length + 1 : 0) + RuntimeNames.CompilerServicesNamespace.Length;
            string implementingType = GetImplementingTypeName(frameName, typeStart);
            return implementingType != null && IsV1DispatcherTypeName(implementingType);
        }

        private static bool IsV1DispatcherTypeName(string declaringType)
        {
            declaringType = GetTypeNameWithoutArity(declaringType);
            return declaringType.EndsWith(RuntimeNames.StateMachineBoxes.TypeSuffix, StringComparison.Ordinal) ||
                declaringType == AsyncStateMachineDispatcherTypeName;
        }

        private static bool IsKnownV1MethodBuilderType(string builderType)
        {
            switch (builderType)
            {
                case RuntimeNames.Builders.Core:
                case RuntimeNames.Builders.Task:
                case RuntimeNames.Builders.ValueTask:
                case RuntimeNames.Builders.PoolingValueTask:
                case RuntimeNames.Builders.Void:
                case RuntimeNames.Builders.Iterator:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsKnownV1AwaiterType(string awaiterType)
        {
            switch (awaiterType)
            {
                case RuntimeNames.Awaiters.Task:
                case RuntimeNames.Awaiters.ConfiguredTask:
                case RuntimeNames.Awaiters.ValueTask:
                case RuntimeNames.Awaiters.ConfiguredValueTask:
                case RuntimeNames.Awaiters.Yield:
                case RuntimeNames.Awaiters.ForceYielding:
                    return true;
                default:
                    return false;
            }
        }

        private static FrameClassification ClassifyAwaiterInfrastructure(string frameName)
        {
            const string typeName = RuntimeNames.Awaiters.TaskType;
            string method = GetBareMethodName(frameName);
            bool isConstructor =
                method == RuntimeNames.Constructors.Instance || method == RuntimeNames.Constructors.Static;
            string member = isConstructor ? "." + method : method;
            if (IsMethodOnType(frameName, typeName, member) || IsMethodOnType(frameName, RuntimeNames.Awaiters.GenericTaskType, member))
            {
                return AsyncProfilerStitchSyncFrameKind.AwaiterHelper;
            }

            int typeStart = IsHostModuleQualifiedFrame(frameName) ? HostModuleName.Length + 1 : 0;
            int typeEnd = typeStart + typeName.Length;
            if (frameName.Length <= typeEnd || frameName[typeEnd] != '+')
            {
                return AsyncProfilerStitchSyncFrameKind.None;
            }

            if (string.CompareOrdinal(frameName, typeStart, typeName, 0, typeName.Length) != 0)
            {
                return AsyncProfilerStitchSyncFrameKind.None;
            }

            return AsyncProfilerStitchSyncFrameKind.AwaiterHelper;
        }

        private static bool IsAwaiterRegistrationMethod(string method) =>
            method == RuntimeNames.Awaiters.OnCompleted ||
            method == RuntimeNames.Awaiters.UnsafeOnCompleted ||
            method == RuntimeNames.Awaiters.UnsafeOnCompletedInternal ||
            method == RuntimeNames.Awaiters.AwaitUnsafeOnCompleted;

        private static bool IsKnownStateMachineBoxAwareAwaiter(string frameName)
        {
            const string qualification = RuntimeNames.Awaiters.BoxAwareInterface;
            int interfaceStart = frameName.IndexOf(qualification, StringComparison.Ordinal);
            if (interfaceStart < 0)
            {
                return false;
            }

            // Explicit-interface qualification is part of the member name, not the declaring awaiter type.
            string awaiterType = GetDeclaringTypeName(frameName.Remove(interfaceStart, qualification.Length - 1));
            return IsKnownV1AwaiterType(GetTypeNameWithoutArity(awaiterType));
        }

        private static FrameClassification ClassifyTaskAndThreadingInfrastructure(string frameName, string method)
        {
            if (method == RuntimeNames.ThreadPool.Dispatch && IsMethodOnType(frameName, RuntimeNames.ThreadPool.WorkQueueType, method))
            {
                return RuntimeFrameKind.SchedulerEntry;
            }

            if (IsMethodOnType(frameName, RuntimeNames.Task.Type, method) || IsMethodOnType(frameName, RuntimeNames.Task.GenericType, method))
            {
                if (method == RuntimeNames.Task.RunContinuations)
                {
                    return RuntimeFrameKind.TaskContinuations;
                }

                if (method == RuntimeNames.Task.TrySetResult)
                {
                    return RuntimeFrameKind.TaskResult;
                }

                if (method == RuntimeNames.Task.TrySetException || method == RuntimeNames.Task.AddException)
                {
                    return AsyncProfilerStitchSyncFrameKind.FaultHandling;
                }
            }

            if (method == RuntimeNames.TaskExceptionHolder.AddFaultException && IsMethodOnType(frameName, RuntimeNames.TaskExceptionHolder.Type, method))
            {
                return AsyncProfilerStitchSyncFrameKind.FaultHandling;
            }

            bool isTaskCompletionSourceSetResult =
                method == RuntimeNames.TaskCompletionSource.SetResult &&
                (IsMethodOnType(frameName, RuntimeNames.TaskCompletionSource.Type, method) ||
                 IsMethodOnType(frameName, RuntimeNames.TaskCompletionSource.GenericType, method));
            if (isTaskCompletionSourceSetResult)
            {
                return RuntimeFrameKind.TaskCompletionSourceResult;
            }

            if (IsMethodOnType(frameName, RuntimeNames.AwaitTaskContinuation.Type, method))
            {
                return method == RuntimeNames.AwaitTaskContinuation.RunOrScheduleAction
                    ? AsyncProfilerStitchSyncFrameKind.ContinuationBridge
                    : AsyncProfilerStitchSyncFrameKind.None;
            }

            bool isResumeTaskContinuation =
                method == RuntimeNames.RuntimeAsyncTaskContinuation.ResumeTaskContinuation &&
                IsMethodOnType(frameName, RuntimeNames.RuntimeAsyncTaskContinuation.ResumeType, method);
            if (isResumeTaskContinuation)
            {
                return AsyncProfilerStitchSyncFrameKind.ContinuationBridge;
            }

            if (IsMethodOnType(frameName, RuntimeNames.ExecutionContext.Type, method))
            {
                return method == RuntimeNames.ExecutionContext.RunInternal || method == RuntimeNames.ExecutionContext.RunFromThreadPoolDispatchLoop
                    ? AsyncProfilerStitchSyncFrameKind.ContinuationBridge
                    : AsyncProfilerStitchSyncFrameKind.None;
            }

            return AsyncProfilerStitchSyncFrameKind.None;
        }

        private static FrameClassification ClassifyProfilerInfrastructure(string frameName, string method)
        {
            if (method == RuntimeNames.Profiler.CreateDispatcher && IsMethodOnType(frameName, RuntimeNames.Profiler.DispatcherInfoType, method))
            {
                return AsyncProfilerStitchSyncFrameKind.V1DispatchSetup;
            }

            const string callstackType = RuntimeNames.Profiler.CallstackType;
            if (method == RuntimeNames.Profiler.CaptureStateMachineCallstack && IsMethodOnType(frameName, callstackType, method))
            {
                return RuntimeFrameKind.ProfilerCapture;
            }

            bool isEmissionMethod = method == RuntimeNames.Profiler.EmitCallstack || method == RuntimeNames.Profiler.EmitEvent;
            if (isEmissionMethod && IsMethodOnType(frameName, callstackType, method))
            {
                return RuntimeFrameKind.ProfilerEmission;
            }

            if (method == RuntimeNames.Profiler.Resume && IsMethodOnType(frameName, RuntimeNames.Profiler.ResumeContextType, method))
            {
                return RuntimeFrameKind.ProfilerResume;
            }

            bool isClassInitialization =
                (method == RuntimeNames.Initialization.CallClassConstructor ||
                 method == RuntimeNames.Initialization.InitClassHelper ||
                 method == RuntimeNames.Initialization.InitClassSlow) &&
                IsMethodOnType(frameName, RuntimeNames.Initialization.InitHelpersType, method);
            bool isStaticBaseInitialization =
                method == RuntimeNames.Initialization.GetNonGCStaticBaseSlow &&
                IsMethodOnType(frameName, RuntimeNames.Initialization.StaticsHelpersType, method);
            if (isClassInitialization || isStaticBaseInitialization)
            {
                return RuntimeFrameKind.RuntimeInitialization;
            }

            bool isConstructor = method == RuntimeNames.Constructors.Instance || method == RuntimeNames.Constructors.Static;
            string member = isConstructor ? "." + method : method;
            if (IsCommonV1DispatcherInfrastructure(frameName) ||
                IsV1DispatcherImplementingType(frameName) ||
                IsMethodOnType(frameName, RuntimeNames.Profiler.DiagnosticsType, member) ||
                IsMethodOnType(frameName, RuntimeNames.Profiler.EventSourceType, member) ||
                IsMethodOnType(frameName, RuntimeNames.Profiler.Type, member) ||
                IsNestedProfilerTypeFrame(frameName))
            {
                return AsyncProfilerStitchSyncFrameKind.ProfilerHelper;
            }

            return AsyncProfilerStitchSyncFrameKind.None;
        }

        private static bool IsNestedProfilerTypeFrame(string frameName)
        {
            const string profilerType = RuntimeNames.Profiler.Type;
            int typeStart = IsHostModuleQualifiedFrame(frameName) ? HostModuleName.Length + 1 : 0;
            int typeEnd = typeStart + profilerType.Length;
            return frameName.Length > typeEnd &&
                frameName[typeEnd] == '+' &&
                string.CompareOrdinal(frameName, typeStart, profilerType, 0, profilerType.Length) == 0;
        }

        private static bool IsMethodOnType(string frameName, string fullTypeName, string method, string nestedTypeName = null) =>
            AsyncProfilerMethodNames.IsMethodOnType(frameName, fullTypeName, method, HostModuleName, nestedTypeName);

        private static bool IsRuntimeCompilerServicesMethod(string frameName)
        {
            int qualifiedStart = IsHostModuleQualifiedFrame(frameName) ? HostModuleName.Length + 1 : 0;
            return HasCompilerServicesNamespaceAt(frameName, qualifiedStart);
        }

        private static bool HasCompilerServicesNamespaceAt(string frameName, int start)
        {
            const string namespacePrefix = RuntimeNames.CompilerServicesNamespace;
            return frameName.Length - start >= namespacePrefix.Length &&
                string.CompareOrdinal(frameName, start, namespacePrefix, 0, namespacePrefix.Length) == 0;
        }

        private static Version ParseVersion(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            // Product versions may append a prerelease identifier, source hash, or "Commit Hash: ...".
            text = text.Trim();
            int suffix = text.IndexOfAny(s_versionSuffixes);
            if (suffix >= 0)
            {
                text = text.Substring(0, suffix);
            }

            return Version.TryParse(text, out Version version) ? version : null;
        }

        private static readonly char[] s_versionSuffixes = { '-', '+', ' ' };
        private static readonly AsyncProfilerRuntimeSupport s_common = new AsyncProfilerRuntimeSupport("Common");
        private static readonly AsyncProfilerRuntimeSupport s_coreClr11 = new AsyncProfilerRuntimeSupport("CoreCLR 11.0");
        private static readonly AsyncProfilerCallStackTransform s_sampledV2Wrapper = new AsyncProfilerSampledV2WrapperTransformPass().Apply;
        private static readonly AsyncProfilerCallStackTransform s_reentrantHandoff = new AsyncProfilerReentrantHandoffTransformPass().Apply;
        private static readonly AsyncProfilerCallStackTransform s_v1Presentation = new AsyncProfilerV1PresentationTransformPass().Apply;
        private static readonly AsyncProfilerCallStackTransform s_infrastructureCleanup = new AsyncProfilerInfrastructureCleanupTransformPass().Apply;

        #endregion
    }
}
