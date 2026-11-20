// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;
using Microsoft.Diagnostics.Tracing.Stacks;

using static Microsoft.Diagnostics.Tracing.Computers.AsyncProfilerRuntimeSupport;

namespace Microsoft.Diagnostics.Tracing.Computers
{
    /// <summary>
    /// Combines physical and active async call stacks, adds any enabled parent contexts,
    /// applies configured transforms, and stores the result in a <see cref="MutableTraceEventStackSource"/>.
    /// </summary>
    internal sealed class AsyncProfilerCallStackSourceBuilderCore
    {
        /// <summary>
        /// Creates a builder when the trace contains an async-profiler index, otherwise returns null.
        /// </summary>
        /// <remarks>
        /// Event-based calls use <paramref name="rootFactory"/> for their grouping root.
        /// Callers supplying roots directly can pass null.
        /// </remarks>
        internal static AsyncProfilerCallStackSourceBuilderCore TryCreate(
            TraceLog eventLog,
            MutableTraceEventStackSource outputStackSource,
            AsyncProfilerCallStackTransformPipeline transforms,
            Func<TraceEvent, TraceThread, StackSourceCallStackIndex> rootFactory)
        {
            AsyncProfilerCallStacksIndex index = eventLog.AsyncProfilerCallStacks;
            if (index == null)
            {
                return null;
            }

            return new AsyncProfilerCallStackSourceBuilderCore(eventLog, outputStackSource, index, transforms, rootFactory);
        }

        /// <summary>Combined diagnostics from reconstructed call stacks.</summary>
        internal AsyncProfilerCallStackDiagnostics Diagnostics => m_diagnostics;

        /// <summary>Include per-segment reconstruction details in diagnostics.</summary>
        internal bool TraceSteps { get; set; }

        /// <summary>
        /// Combines the physical stack for <paramref name="data"/> with active async call stacks,
        /// applies configured transforms, and returns the final output index in <paramref name="stitchedStack"/>.
        /// Returns false when no physical stack or usable async data is available.
        /// </summary>
        /// <remarks>Uses the stack's source timestamp and the configured grouping root.</remarks>
        /// <exception cref="InvalidOperationException">The builder's resources have been cleared.</exception>
        internal bool TryGetCallStack(TraceEvent data, TraceThread thread, out StackSourceCallStackIndex stitchedStack)
        {
            if (m_index == null)
            {
                throw new InvalidOperationException("Cannot produce call stacks after resources have been cleared.");
            }

            stitchedStack = StackSourceCallStackIndex.Invalid;

            if (!data.TryGetCallStackIndex(out CallStackIndex callStackIndex, out long timestampQpc))
            {
                return false;
            }

            return TryGetCallStack(callStackIndex, timestampQpc, thread, StackSourceCallStackIndex.Invalid, out stitchedStack, data);
        }

        /// <summary>
        /// Combines <paramref name="callStackIndex"/> with async call stacks active at <paramref name="timestampQpc"/>,
        /// applies configured transforms, and returns the final output index in <paramref name="stitchedStack"/>.
        /// Returns false when the physical stack is invalid or no usable async data is available.
        /// </summary>
        /// <remarks>
        /// When <paramref name="data"/> is supplied, the root factory replaces <paramref name="root"/>.
        /// </remarks>
        /// <exception cref="InvalidOperationException">The builder's resources have been cleared.</exception>
        internal bool TryGetCallStack(
            CallStackIndex callStackIndex,
            long timestampQpc,
            TraceThread thread,
            StackSourceCallStackIndex root,
            out StackSourceCallStackIndex stitchedStack,
            TraceEvent data = null)
        {
            if (m_index == null)
            {
                throw new InvalidOperationException("Cannot produce call stacks after resources have been cleared.");
            }

            stitchedStack = StackSourceCallStackIndex.Invalid;

            if (callStackIndex == CallStackIndex.Invalid)
            {
                return false;
            }

            ProcessIndex processIndex = thread.Process.ProcessIndex;
            m_index.GetCallStacks(new AsyncProfilerThreadKey(processIndex, (ulong)thread.ThreadID), timestampQpc, m_segments);

            if (!AsyncProfilerCallStackStitcher.HasUsableSegments(m_segments))
            {
                return false;
            }

            CallStackIndex physicalRoot = MaterializeSyncLeafToRoot(callStackIndex);
            AsyncProfilerRuntimeSupport runtimeSupport = m_runtimeResolver.ResolveProcessSupport(processIndex);

            m_sampleDiagnostics.MessageLimit = m_diagnostics.RemainingMessageCapacity;
            bool parentContextsAvailable = false;
            if (m_transforms.EnableParentContexts && m_index.HasDispatcherRecords)
            {
                if (m_parentContextBuilder == null)
                {
                    m_parentContextBuilder = new AsyncProfilerParentContextBuilder();
                }

                parentContextsAvailable = m_parentContextBuilder.HasParentContextsToAdd(m_index, m_segments);
            }

            bool collectPlacements = parentContextsAvailable || m_transforms.EnableNormalization || m_transforms.EnableInfrastructureCleanup;

            List<AsyncProfilerCallStackPlacement> segmentPlacements = null;
            if (collectPlacements)
            {
                if (m_segmentPlacements == null)
                {
                    m_segmentPlacements = new List<AsyncProfilerCallStackPlacement>();
                }

                segmentPlacements = m_segmentPlacements;
            }

            AsyncProfilerCallStackStitcher.StitchInto(
                m_syncFrames,
                m_segments,
                timestampQpc,
                m_classify,
                m_methodCompletionObserved,
                processIndex,
                m_methodOf,
                TraceSteps,
                m_stitchedFrames,
                m_sampleDiagnostics,
                m_stitchingState,
                segmentPlacements,
                m_runtimeSupportResolver,
                runtimeSupport,
                m_eventLog,
                m_index,
                m_runtimeResolver);

            if (parentContextsAvailable)
            {
                m_parentContextBuilder.AddParentContexts(m_index, m_segments, segmentPlacements, m_stitchedFrames, m_transforms.MaximumParentContextDepth);
            }

            var transformContext = new AsyncProfilerCallStackTransformState(
                m_eventLog,
                m_segments,
                timestampQpc,
                m_classify,
                m_stitchingState,
                m_sampleDiagnostics,
                m_stitchedFrames,
                m_runtimeSupportResolver,
                runtimeSupport,
                m_runtimeResolver,
                segmentPlacements,
                methodOf: m_methodOf);

            m_transforms.ApplyInPlace(transformContext);
            m_sampleDiagnostics.AddTo(m_diagnostics);

            if (data != null)
            {
                root = m_rootFactory(data, thread);
            }

            stitchedStack = InternStitchedStack(root, processIndex, physicalRoot);
            return true;
        }

        /// <summary>
        /// Releases reconstruction buffers and caches while preserving diagnostics.
        /// Further call-stack production throws <see cref="InvalidOperationException"/>.
        /// </summary>
        internal void ClearResources()
        {
            m_index = null;
            m_boundaries = null;
            m_runtimeResolver = null;
            m_runtimeSupportResolver = null;
            m_classify = null;
            m_methodCompletionObserved = null;
            m_methodOf = null;

            m_canonicalMethodByMethodIndex = null;
            m_canonicalMethodByIdentity = null;

            m_segments = null;
            m_syncFrames = null;
            m_stitchedFrames = null;
            m_segmentPlacements = null;
            m_parentContextBuilder?.Clear();
            m_parentContextBuilder = null;

            m_syncFrameMetadata = null;
            m_boxedStateMachineMethods = null;
            m_stateMachineMethodByTypeName = null;
            m_sampleDiagnostics = null;
            m_stitchingState = null;
            m_logicalSyncFrameByCodeAddress = null;
            m_frameByIdentity = null;
            m_rootFactory = null;
        }

        #region private

        private AsyncProfilerCallStackSourceBuilderCore(
            TraceLog eventLog,
            MutableTraceEventStackSource outputStackSource,
            AsyncProfilerCallStacksIndex index,
            AsyncProfilerCallStackTransformPipeline transforms,
            Func<TraceEvent, TraceThread, StackSourceCallStackIndex> rootFactory)
        {
            m_eventLog = eventLog;
            m_outputStackSource = outputStackSource;
            m_index = index;
            m_transforms = transforms;
            m_rootFactory = rootFactory;

            m_runtimeResolver = new AsyncProfilerRuntimeResolver(eventLog.ModuleFiles);
            m_runtimeSupportResolver = GetRuntimeSupport;
            m_boundaries = new AsyncProfilerCallStackStitchBoundaryCache(eventLog.CodeAddresses, m_runtimeResolver);
            m_classify = m_boundaries.Classify;
            m_methodCompletionObserved = index.MethodCompletionObserved;

            m_canonicalMethodByMethodIndex = new Dictionary<MethodIndex, MethodIndex>();
            m_canonicalMethodByIdentity = new Dictionary<ManagedMethodIdentity, MethodIndex>();
            m_methodOf = GetMethod;

            m_diagnostics = new AsyncProfilerCallStackDiagnostics();
            m_sampleDiagnostics = new AsyncProfilerCallStackDiagnostics();
            m_stitchingState = new AsyncProfilerCallStackStitchingState();

            m_segments = new List<AsyncProfilerCallStack>();
            m_syncFrames = new List<AsyncProfilerStitchSyncFrame>();
            m_stitchedFrames = new List<AsyncProfilerStitchedFrame>();
            m_syncFrameMetadata = new Dictionary<CodeAddressIndex, (FrameClassification Classification, AsyncProfilerRuntimeSupport Support)>();
            m_boxedStateMachineMethods = new Dictionary<CodeAddressIndex, MethodIndex>();
            m_logicalSyncFrameByCodeAddress = new Dictionary<CodeAddressIndex, StackSourceFrameIndex>();
            m_frameByIdentity = new Dictionary<CallStackSourceFrameIdentity, StackSourceFrameIndex>();
        }

        private CallStackIndex MaterializeSyncLeafToRoot(CallStackIndex callStackIndex)
        {
            m_syncFrames.Clear();
            TraceCallStacks callStacks = m_eventLog.CallStacks;
            CallStackIndex physicalRoot = CallStackIndex.Invalid;

            for (CallStackIndex stackIndex = callStackIndex; stackIndex != CallStackIndex.Invalid; stackIndex = callStacks.Caller(stackIndex))
            {
                physicalRoot = stackIndex;
                CodeAddressIndex codeAddressIndex = callStacks.CodeAddressIndex(stackIndex);
                MethodIndex method = GetMethod(codeAddressIndex);

                if (!m_syncFrameMetadata.TryGetValue(codeAddressIndex, out var metadata))
                {
                    metadata = ClassifySyncFrame(codeAddressIndex);
                    m_syncFrameMetadata.Add(codeAddressIndex, metadata);
                }

                bool isV1DispatcherInfrastructure =
                    method != MethodIndex.Invalid &&
                    m_classify(codeAddressIndex).Kind == AsyncProfilerCallStackStitchBoundaryKind.V1DispatcherInfrastructure;
                MethodIndex boxedMethod = isV1DispatcherInfrastructure
                    ? GetBoxedStateMachineMethod(codeAddressIndex)
                    : MethodIndex.Invalid;

                m_syncFrames.Add(new AsyncProfilerStitchSyncFrame(codeAddressIndex, method, metadata.Classification, boxedMethod));
            }

            return physicalRoot;
        }

        private MethodIndex GetMethod(CodeAddressIndex codeAddressIndex)
        {
            if (codeAddressIndex == CodeAddressIndex.Invalid)
            {
                return MethodIndex.Invalid;
            }

            return NormalizeMethod(m_eventLog.CodeAddresses.MethodIndex(codeAddressIndex));
        }

        private (FrameClassification Classification, AsyncProfilerRuntimeSupport Support) ClassifySyncFrame(CodeAddressIndex codeAddressIndex)
        {
            TraceCodeAddress codeAddress = null;
            TraceModuleFile module = null;
            if (codeAddressIndex != CodeAddressIndex.Invalid)
            {
                codeAddress = m_eventLog.CodeAddresses[codeAddressIndex];
                module = m_eventLog.CodeAddresses.ModuleFile(codeAddressIndex);
            }

            string frameName = AsyncProfilerMethodNames.NormalizeMethodName(codeAddress?.FullMethodName);
            AsyncProfilerRuntimeSupport support = GetModuleSupport(module, out bool isHostModule);
            return (support.ClassifySynchronousFrame(frameName, isHostModule), support);
        }

        private AsyncProfilerRuntimeSupport GetModuleSupport(TraceModuleFile module, out bool isHostModule)
        {
            if (module == null)
            {
                isHostModule = false;
                return AsyncProfilerRuntimeSupport.Common;
            }

            isHostModule = m_runtimeResolver.IsHostModule(module.ModuleFileIndex);
            return m_runtimeResolver.ResolveModuleSupport(module.ModuleFileIndex);
        }

        private MethodIndex GetBoxedStateMachineMethod(CodeAddressIndex codeAddressIndex)
        {
            if (m_boxedStateMachineMethods.TryGetValue(codeAddressIndex, out MethodIndex cached))
            {
                return cached;
            }

            TraceCodeAddress codeAddress = m_eventLog.CodeAddresses[codeAddressIndex];
            TraceModuleFile module = m_eventLog.CodeAddresses.ModuleFile(codeAddressIndex);
            AsyncProfilerRuntimeSupport support = GetModuleSupport(module, out bool isHostModule);
            string frameName = AsyncProfilerMethodNames.NormalizeMethodName(codeAddress.FullMethodName);

            MethodIndex method = MethodIndex.Invalid;
            if (support.TryGetBoxedStateMachineTypeName(frameName, isHostModule, out string stateMachineType))
            {
                m_stateMachineMethodByTypeName ??= BuildStateMachineMethodByTypeName();
                if (!m_stateMachineMethodByTypeName.TryGetValue(stateMachineType, out method))
                {
                    method = MethodIndex.Invalid;
                }
            }

            m_boxedStateMachineMethods.Add(codeAddressIndex, method);
            return method;
        }

        private Dictionary<string, MethodIndex> BuildStateMachineMethodByTypeName()
        {
            var methodByTypeName = new Dictionary<string, MethodIndex>(StringComparer.Ordinal);
            TraceMethods methods = m_eventLog.CodeAddresses.Methods;

            for (int i = 0; i < methods.Count; i++)
            {
                string methodName = AsyncProfilerMethodNames.NormalizeMethodName(methods.FullMethodName((MethodIndex)i));
                string typeName = AsyncProfilerMethodNames.GetStateMachineTypeName(methodName);
                if (typeName == null)
                {
                    continue;
                }

                MethodIndex canonical = NormalizeMethod((MethodIndex)i);
                if (methodByTypeName.TryGetValue(typeName, out MethodIndex existing) && existing != canonical)
                {
                    // Type names alone cannot disambiguate identically named types in different modules.
                    canonical = MethodIndex.Invalid;
                }

                methodByTypeName[typeName] = canonical;
            }

            return methodByTypeName;
        }

        private AsyncProfilerRuntimeSupport GetRuntimeSupport(CodeAddressIndex codeAddressIndex)
        {
            if (codeAddressIndex == CodeAddressIndex.Invalid)
            {
                return AsyncProfilerRuntimeSupport.Common;
            }

            if (m_syncFrameMetadata.TryGetValue(codeAddressIndex, out var metadata))
            {
                return metadata.Support;
            }

            return m_runtimeResolver.ResolveModuleSupport(m_eventLog.CodeAddresses.ModuleFileIndex(codeAddressIndex));
        }

        private MethodIndex NormalizeMethod(MethodIndex method)
        {
            if (method == MethodIndex.Invalid)
            {
                return MethodIndex.Invalid;
            }

            if (m_canonicalMethodByMethodIndex.TryGetValue(method, out MethodIndex canonical))
            {
                return canonical;
            }

            TraceMethod traceMethod = m_eventLog.CodeAddresses.Methods[method];
            if (traceMethod.MethodToken == 0)
            {
                m_canonicalMethodByMethodIndex.Add(method, method);
                return method;
            }

            // Different JIT versions can have different MethodIndex values for the same managed method.
            var identity = new ManagedMethodIdentity(traceMethod.MethodModuleFileIndex, traceMethod.MethodToken, traceMethod.FullMethodName);

            if (!m_canonicalMethodByIdentity.TryGetValue(identity, out canonical))
            {
                canonical = method;
                m_canonicalMethodByIdentity.Add(identity, canonical);
            }

            m_canonicalMethodByMethodIndex.Add(method, canonical);
            return canonical;
        }

        private StackSourceCallStackIndex InternStitchedStack(StackSourceCallStackIndex root, ProcessIndex processIndex, CallStackIndex physicalRoot)
        {
            StackSourceCallStackIndex caller = root;
            if (physicalRoot != CallStackIndex.Invalid && !HasIncompletePhysicalRoot())
            {
                // Keep the synchronous root policy without duplicating a recovered BROKEN root.
                caller = m_outputStackSource.GetCallerIndex(m_outputStackSource.GetCallStack(physicalRoot, root));
            }

            AsyncProfilerStitchedFrame rootwardFrame = default;
            bool hasRootwardFrame = false;

            for (int i = m_stitchedFrames.Count - 1; i >= 0; i--)
            {
                AsyncProfilerStitchedFrame currentFrame = m_stitchedFrames[i];
                if (hasRootwardFrame && ShouldEmitContextTransition(rootwardFrame, currentFrame))
                {
                    caller = m_outputStackSource.Interner.CallStackIntern(InternAsyncContextTransitionFrame(), caller);
                }

                StackSourceFrameIndex frame = InternStitchedFrame(currentFrame, processIndex);
                caller = m_outputStackSource.Interner.CallStackIntern(frame, caller);
                rootwardFrame = currentFrame;
                hasRootwardFrame = true;
            }

            return caller;
        }

        private bool HasIncompletePhysicalRoot()
        {
            for (int i = 0; i < m_stitchedFrames.Count; i++)
            {
                if (m_stitchedFrames[i].Origin == AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot)
                {
                    return true;
                }
            }

            return false;
        }

        private StackSourceFrameIndex InternStitchedFrame(AsyncProfilerStitchedFrame frame, ProcessIndex processIndex)
        {
            if (frame.Origin == AsyncProfilerStitchedFrameOrigin.Synthetic)
            {
                return m_outputStackSource.Interner.FrameIntern(frame.DisplayName);
            }

            if (frame.Origin == AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot)
            {
                // Use a named BROKEN frame; exporters omit the reserved Broken index.
                return m_outputStackSource.Interner.FrameIntern("BROKEN", m_outputStackSource.Interner.ModuleIntern(string.Empty));
            }

            if (frame.Presentation == AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames)
            {
                if (m_asyncDispatcherFrame == StackSourceFrameIndex.Invalid)
                {
                    m_asyncDispatcherFrame = m_outputStackSource.Interner.FrameIntern("[Async dispatcher frames]");
                }

                return m_asyncDispatcherFrame;
            }

            if (frame.Origin != AsyncProfilerStitchedFrameOrigin.Sync && frame.Segment != null)
            {
                return InternAsyncFrame(frame, processIndex);
            }

            bool isLogicalSyncFrame =
                frame.Presentation == AsyncProfilerStitchedFramePresentation.LogicalStateMachineMethod &&
                frame.CodeAddress != CodeAddressIndex.Invalid;
            if (isLogicalSyncFrame && TryGetLogicalSyncFrame(frame.CodeAddress, out StackSourceFrameIndex logicalFrame))
            {
                return logicalFrame;
            }

            if (frame.CodeAddress != CodeAddressIndex.Invalid)
            {
                return m_outputStackSource.GetFrameIndex(frame.CodeAddress);
            }

            return m_outputStackSource.Interner.FrameIntern("?!?");
        }

        private bool TryGetLogicalSyncFrame(CodeAddressIndex codeAddressIndex, out StackSourceFrameIndex logicalFrame)
        {
            if (!m_logicalSyncFrameByCodeAddress.TryGetValue(codeAddressIndex, out logicalFrame))
            {
                logicalFrame = InternLogicalSyncFrame(codeAddressIndex);
                m_logicalSyncFrameByCodeAddress.Add(codeAddressIndex, logicalFrame);
            }

            return logicalFrame != StackSourceFrameIndex.Invalid;
        }

        /// <summary>Returns an invalid frame when the method has no logical state-machine name.</summary>
        private StackSourceFrameIndex InternLogicalSyncFrame(CodeAddressIndex codeAddressIndex)
        {
            TraceCodeAddress codeAddress = m_eventLog.CodeAddresses[codeAddressIndex];
            string methodName = AsyncProfilerMethodNames.NormalizeMethodName(codeAddress.FullMethodName);
            if (!AsyncProfilerMethodNames.TryGetLogicalStateMachineMethodName(methodName, out string logicalName, out string stateMachineSuffix))
            {
                return StackSourceFrameIndex.Invalid;
            }

            StackSourceModuleIndex module = m_outputStackSource.Interner.ModuleIntern(codeAddress.ModuleName);
            StackSourceFrameIndex logicalFrame = m_outputStackSource.Interner.FrameIntern(
                AsyncProfilerMethodNames.FormatLogicalStateMachineMethodName(logicalName, stateMachineSuffix),
                module,
                m_outputStackSource.NextAsyncProfilerCallStackFrameIdentityTag());

            m_outputStackSource.SetFrameCodeAddress(logicalFrame, codeAddressIndex);
            return logicalFrame;
        }

        private StackSourceFrameIndex InternAsyncFrame(AsyncProfilerStitchedFrame frame, ProcessIndex processIndex)
        {
            AsyncProfilerCallStackFrames segment = frame.Segment;
            int segmentFrameIndex = frame.SegmentFrameIndex;
            AsyncCallstackKind kind = segment.Kind;
            ulong instructionPointer = segment.InstructionPointerAt(segmentFrameIndex);
            int state = kind == AsyncCallstackKind.StateMachineAsync ? segment.FrameStateAt(segmentFrameIndex) : 0;

            bool parentContext = frame.ContextKind == AsyncProfilerStitchedFrameContextKind.ParentContext;
            bool dispatchTarget = frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget;
            bool current = frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent;
            bool parentContextCurrent = frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncContextParentCurrent;
            bool completing = frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody;
            bool completionUnwind = frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletionReturn;
            bool annotateActive = m_transforms.EnableFrameAnnotations && frame.ContextKind == AsyncProfilerStitchedFrameContextKind.Active;

            // Keep different states and origins separate even when the displayed names match.
            var identity = new CallStackSourceFrameIdentity(
                kind,
                processIndex,
                frame.CodeAddress,
                instructionPointer,
                state,
                frame.ContextKind,
                annotateActive,
                frame.Origin,
                frame.IsStateMachineBoxCarrier);

            if (m_frameByIdentity.TryGetValue(identity, out StackSourceFrameIndex existingFrame))
            {
                return existingFrame;
            }

            int identityTag = m_outputStackSource.NextAsyncProfilerCallStackFrameIdentityTag();
            bool annotate = parentContext || annotateActive;
            string prefix = annotate ? GetAnnotationPrefix(kind) : string.Empty;
            int? labelState = annotate ? GetPresentationState(frame.Origin, state) : null;
            StackSourceFrameIndex asyncFrame = InternAsyncFrameName(frame, identityTag, prefix, labelState, instructionPointer);

            m_frameByIdentity.Add(identity, asyncFrame);
            m_outputStackSource.SetAsyncProfilerCallStackFrameInfo(
                asyncFrame,
                new AsyncProfilerCallStackSourceFrameInfo(
                    kind,
                    processIndex,
                    frame.CodeAddress,
                    instructionPointer,
                    state,
                    parentContext,
                    parentContextCurrent,
                    dispatchTarget,
                    current,
                    frame.IsStateMachineBoxCarrier,
                    completing,
                    completionUnwind));

            return asyncFrame;
        }

        /// <summary>Only labels use the inferred state; frame metadata keeps the captured state.</summary>
        private static int GetPresentationState(AsyncProfilerStitchedFrameOrigin origin, int capturedState)
        {
            return origin switch
            {
                AsyncProfilerStitchedFrameOrigin.V1CompletedBody or AsyncProfilerStitchedFrameOrigin.V1CompletionReturn => -2,
                AsyncProfilerStitchedFrameOrigin.AsyncCurrent or AsyncProfilerStitchedFrameOrigin.AsyncContextParentCurrent => -1,
                _ => capturedState,
            };
        }

        private StackSourceFrameIndex InternAsyncFrameName(
            AsyncProfilerStitchedFrame frame,
            int identityTag,
            string prefix,
            int? labelState,
            ulong instructionPointer)
        {
            if (frame.CodeAddress == CodeAddressIndex.Invalid)
            {
                string name = prefix + "AsyncFrame(0x" + instructionPointer.ToString("x") + ")";
                return m_outputStackSource.Interner.FrameIntern(name, StackSourceModuleIndex.Invalid, identityTag);
            }

            bool isLogical = frame.Presentation == AsyncProfilerStitchedFramePresentation.LogicalStateMachineMethod;
            if (!isLogical && prefix.Length == 0)
            {
                return InternUnannotatedFrame(frame.CodeAddress, identityTag);
            }

            CodeAddressIndex nameAddress = frame.CodeAddress;
            if (isLogical && frame.IsStateMachineBoxCarrier)
            {
                nameAddress = frame.Segment.CodeAddressAt(frame.SegmentFrameIndex);
            }

            if (isLogical)
            {
                TraceCodeAddress codeAddress = m_eventLog.CodeAddresses[nameAddress];
                string methodName = AsyncProfilerMethodNames.NormalizeMethodName(codeAddress.FullMethodName);
                if (AsyncProfilerMethodNames.TryGetLogicalStateMachineMethodName(methodName, out string logicalName, out string stateMachineSuffix))
                {
                    string displayName = AsyncProfilerMethodNames.FormatLogicalStateMachineMethodName(logicalName, stateMachineSuffix, labelState);
                    StackSourceModuleIndex logicalModule = m_outputStackSource.Interner.ModuleIntern(codeAddress.ModuleName);
                    return m_outputStackSource.Interner.FrameIntern(displayName, logicalModule, identityTag, prefix);
                }
            }

            if (prefix.Length == 0)
            {
                return InternUnannotatedFrame(frame.CodeAddress, identityTag);
            }

            // Derive from the physical frame so unresolved names and later symbol lookups behave as they do for it.
            StackSourceFrameIndex physicalFrame = m_outputStackSource.GetFrameIndex(nameAddress);
            return m_outputStackSource.Interner.FrameIntern(physicalFrame, string.Empty, identityTag, prefix);
        }

        private StackSourceFrameIndex InternUnannotatedFrame(CodeAddressIndex codeAddress, int identityTag)
        {
            StackSourceFrameIndex physicalFrame = m_outputStackSource.GetFrameIndex(codeAddress);
            return m_outputStackSource.Interner.FrameIntern(physicalFrame, string.Empty, identityTag);
        }

        private static string GetAnnotationPrefix(AsyncCallstackKind kind)
        {
            string version = kind == AsyncCallstackKind.StateMachineAsync ? "V1" : "V2";
            return "[Async " + version + "] ";
        }

        private StackSourceFrameIndex InternAsyncContextTransitionFrame()
        {
            if (m_asyncContextTransitionFrame == StackSourceFrameIndex.Invalid)
            {
                m_asyncContextTransitionFrame = m_outputStackSource.Interner.FrameIntern("[Async resumed parent context]");
            }

            return m_asyncContextTransitionFrame;
        }

        private static bool ShouldEmitContextTransition(AsyncProfilerStitchedFrame rootwardFrame, AsyncProfilerStitchedFrame leafwardFrame)
        {
            if (SameContext(rootwardFrame, leafwardFrame))
            {
                return false;
            }

            return rootwardFrame.ContextKind == AsyncProfilerStitchedFrameContextKind.ParentContext ||
                   leafwardFrame.ContextKind == AsyncProfilerStitchedFrameContextKind.ParentContext;
        }

        private static bool SameContext(AsyncProfilerStitchedFrame left, AsyncProfilerStitchedFrame right)
        {
            if (left.ContextKind != right.ContextKind)
            {
                return false;
            }

            if (ReferenceEquals(left.Context, right.Context))
            {
                return true;
            }

            if (left.Context == null || right.Context == null)
            {
                return false;
            }

            return left.Context.Thread.Equals(right.Context.Thread) &&
                left.Context.DispatcherId == right.Context.DispatcherId &&
                left.Context.StartQpc == right.Context.StartQpc;
        }

        private readonly struct CallStackSourceFrameIdentity : IEquatable<CallStackSourceFrameIdentity>
        {
            public CallStackSourceFrameIdentity(
                AsyncCallstackKind kind,
                ProcessIndex processIndex,
                CodeAddressIndex codeAddress,
                ulong instructionPointer,
                int state,
                AsyncProfilerStitchedFrameContextKind contextKind,
                bool annotateActive,
                AsyncProfilerStitchedFrameOrigin origin,
                bool stateMachineBoxCarrier)
            {
                m_kind = kind;
                m_processIndex = processIndex;
                m_codeAddress = codeAddress;
                m_instructionPointer = instructionPointer;
                m_state = state;
                m_contextKind = contextKind;
                m_annotateActive = annotateActive;
                m_origin = origin;
                m_stateMachineBoxCarrier = stateMachineBoxCarrier;
            }

            public bool Equals(CallStackSourceFrameIdentity other) =>
                m_kind == other.m_kind &&
                m_processIndex == other.m_processIndex &&
                m_codeAddress == other.m_codeAddress &&
                m_instructionPointer == other.m_instructionPointer &&
                m_state == other.m_state &&
                m_contextKind == other.m_contextKind &&
                m_annotateActive == other.m_annotateActive &&
                m_origin == other.m_origin &&
                m_stateMachineBoxCarrier == other.m_stateMachineBoxCarrier;

            public override bool Equals(object obj) => obj is CallStackSourceFrameIdentity other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = (int)m_kind;
                    hash = (hash * 397) ^ (int)m_processIndex;
                    hash = (hash * 397) ^ (int)m_codeAddress;
                    hash = (hash * 397) ^ m_instructionPointer.GetHashCode();
                    hash = (hash * 397) ^ m_state;
                    hash = (hash * 397) ^ (int)m_contextKind;
                    hash = (hash * 397) ^ m_annotateActive.GetHashCode();
                    hash = (hash * 397) ^ (int)m_origin;
                    return (hash * 397) ^ m_stateMachineBoxCarrier.GetHashCode();
                }
            }

            #region private

            private readonly AsyncCallstackKind m_kind;
            private readonly ProcessIndex m_processIndex;
            private readonly CodeAddressIndex m_codeAddress;
            private readonly ulong m_instructionPointer;
            private readonly int m_state;
            private readonly AsyncProfilerStitchedFrameContextKind m_contextKind;
            private readonly bool m_annotateActive;
            private readonly AsyncProfilerStitchedFrameOrigin m_origin;
            private readonly bool m_stateMachineBoxCarrier;

            #endregion
        }

        private readonly struct ManagedMethodIdentity : IEquatable<ManagedMethodIdentity>
        {
            public ManagedMethodIdentity(ModuleFileIndex module, int token, string name)
            {
                m_module = module;
                m_token = token;
                m_name = name;
            }

            public bool Equals(ManagedMethodIdentity other) =>
                m_module == other.m_module && m_token == other.m_token && string.Equals(m_name, other.m_name, StringComparison.Ordinal);

            public override bool Equals(object obj) => obj is ManagedMethodIdentity other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = ((int)m_module * 397) ^ m_token;
                    return (hash * 397) ^ (m_name?.GetHashCode() ?? 0);
                }
            }

            #region private

            private readonly ModuleFileIndex m_module;
            private readonly int m_token;
            private readonly string m_name;

            #endregion
        }

        private readonly TraceLog m_eventLog;
        private readonly MutableTraceEventStackSource m_outputStackSource;
        private readonly AsyncProfilerCallStackTransformPipeline m_transforms;

        private AsyncProfilerCallStacksIndex m_index;
        private AsyncProfilerCallStackStitchBoundaryCache m_boundaries;
        private AsyncProfilerRuntimeResolver m_runtimeResolver;
        private Func<CodeAddressIndex, AsyncProfilerRuntimeSupport> m_runtimeSupportResolver;
        private Func<CodeAddressIndex, AsyncProfilerCallStackStitchBoundaryInfo> m_classify;
        private Func<ProcessIndex, AsyncCallstackKind, long, bool> m_methodCompletionObserved;
        private Func<CodeAddressIndex, MethodIndex> m_methodOf;

        private Dictionary<MethodIndex, MethodIndex> m_canonicalMethodByMethodIndex;
        private Dictionary<ManagedMethodIdentity, MethodIndex> m_canonicalMethodByIdentity;

        private readonly AsyncProfilerCallStackDiagnostics m_diagnostics;
        private AsyncProfilerCallStackDiagnostics m_sampleDiagnostics;
        private AsyncProfilerCallStackStitchingState m_stitchingState;

        private List<AsyncProfilerCallStack> m_segments;
        private List<AsyncProfilerStitchSyncFrame> m_syncFrames;
        private List<AsyncProfilerStitchedFrame> m_stitchedFrames;
        private List<AsyncProfilerCallStackPlacement> m_segmentPlacements;
        private AsyncProfilerParentContextBuilder m_parentContextBuilder;

        private Dictionary<CodeAddressIndex, (FrameClassification Classification, AsyncProfilerRuntimeSupport Support)> m_syncFrameMetadata;
        private Dictionary<CodeAddressIndex, MethodIndex> m_boxedStateMachineMethods;
        private Dictionary<string, MethodIndex> m_stateMachineMethodByTypeName;
        private Dictionary<CodeAddressIndex, StackSourceFrameIndex> m_logicalSyncFrameByCodeAddress;
        private Dictionary<CallStackSourceFrameIdentity, StackSourceFrameIndex> m_frameByIdentity;
        private StackSourceFrameIndex m_asyncDispatcherFrame = StackSourceFrameIndex.Invalid;
        private StackSourceFrameIndex m_asyncContextTransitionFrame = StackSourceFrameIndex.Invalid;
        private Func<TraceEvent, TraceThread, StackSourceCallStackIndex> m_rootFactory;

        #endregion
    }
}
