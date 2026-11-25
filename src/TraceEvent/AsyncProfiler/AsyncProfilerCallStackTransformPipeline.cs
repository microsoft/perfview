// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;

using static Microsoft.Diagnostics.Tracing.Computers.AsyncProfilerCallStackTransformHelpers;

namespace Microsoft.Diagnostics.Tracing.Computers
{
    /// <summary>
    /// State shared by presentation transforms for one sample, after structural stitching.
    /// <see cref="Frames"/> contains the complete stitched stack in leaf-to-root order.
    /// </summary>
    internal readonly struct AsyncProfilerCallStackTransformState
    {
        /// <summary>Creates the sample's shared state and selects its runtime support.</summary>
        internal AsyncProfilerCallStackTransformState(
            TraceLog traceLog,
            IReadOnlyList<AsyncProfilerCallStack> segments,
            long timestampQpc,
            Func<CodeAddressIndex, AsyncProfilerCallStackStitchBoundaryInfo> classifyBoundary,
            AsyncProfilerCallStackStitchingState stitchingState,
            AsyncProfilerCallStackDiagnostics diagnostics,
            List<AsyncProfilerStitchedFrame> frames,
            Func<CodeAddressIndex, AsyncProfilerRuntimeSupport> runtimeSupportResolver = null,
            AsyncProfilerRuntimeSupport runtimeSupport = null,
            AsyncProfilerRuntimeResolver runtimeResolver = null,
            IReadOnlyList<AsyncProfilerCallStackPlacement> segmentPlacements = null,
            Func<CodeAddressIndex, MethodIndex> methodOf = null)
        {
            TraceLog = traceLog;
            Segments = segments;
            TimestampQpc = timestampQpc;
            m_classifyBoundary = classifyBoundary;
            StitchingState = stitchingState ?? throw new ArgumentNullException(nameof(stitchingState));
            Diagnostics = diagnostics;
            Frames = frames;
            m_segmentPlacements = segmentPlacements;
            m_methodOf = methodOf;
            m_runtimeResolver = runtimeResolver ??
                (traceLog == null ? null : new AsyncProfilerRuntimeResolver(traceLog.ModuleFiles));
            m_runtimeSupportResolver = runtimeSupportResolver ??
                CreateRuntimeSupportResolver(traceLog, m_runtimeResolver);
            RuntimeSupport = runtimeSupport ?? ResolveRuntimeSupport(
                traceLog, segments, frames, m_runtimeSupportResolver, m_runtimeResolver,
                useProcessSupport: runtimeSupportResolver == null);
        }

        /// <summary>
        /// The trace referenced by <see cref="Frames"/>, or null for a standalone transform without a trace.
        /// Rules needing module or symbol metadata leave unverified frames unchanged when this is null.
        /// </summary>
        public TraceLog TraceLog { get; }

        /// <summary>
        /// Active async call stacks used by structural stitching, in leaf-to-root order.
        /// During computer callbacks, the list and its call-stack objects are reused and must not be retained.
        /// </summary>
        public IReadOnlyList<AsyncProfilerCallStack> Segments { get; }

        /// <summary>The sample timestamp in the trace QPC domain.</summary>
        public long TimestampQpc { get; }

        /// <summary>Matching state shared by structural stitching, pipeline helpers, and all passes.</summary>
        public AsyncProfilerCallStackStitchingState StitchingState { get; }

        /// <summary>
        /// Diagnostics for this sample.
        /// </summary>
        public AsyncProfilerCallStackDiagnostics Diagnostics { get; }

        /// <summary>The complete mutable stitched stack, leaf-to-root.</summary>
        public List<AsyncProfilerStitchedFrame> Frames { get; }

        /// <summary>
        /// Runtime support selected for this sample before presentation transforms modify its frames.
        /// </summary>
        internal AsyncProfilerRuntimeSupport RuntimeSupport { get; }

        /// <summary>Classifies a frame using the runtime boundary-name contract for this trace.</summary>
        public AsyncProfilerCallStackStitchBoundaryInfo ClassifyBoundary(CodeAddressIndex codeAddress) =>
            m_classifyBoundary(codeAddress);

        /// <summary>Gets the runtime support for a captured code address.</summary>
        internal AsyncProfilerRuntimeSupport GetRuntimeSupport(CodeAddressIndex codeAddress) =>
            m_runtimeSupportResolver(codeAddress) ??
            throw new InvalidOperationException("The runtime support resolver returned null.");

        /// <summary>Gets the captured CoreLib owner, or Invalid when it cannot be identified.</summary>
        internal ModuleFileIndex GetHostModuleOwner(CodeAddressIndex codeAddress) =>
            TraceLog == null || codeAddress == CodeAddressIndex.Invalid
                ? ModuleFileIndex.Invalid
                : m_runtimeResolver.GetHostModuleOwner(TraceLog.CodeAddresses.ModuleFileIndex(codeAddress));

        /// <summary>
        /// Uses the supplied method resolver, or the trace's method index when no resolver was supplied.
        /// </summary>
        internal MethodIndex GetMethod(CodeAddressIndex codeAddress)
        {
            if (codeAddress == CodeAddressIndex.Invalid)
            {
                return MethodIndex.Invalid;
            }

            if (m_methodOf != null)
            {
                return m_methodOf(codeAddress);
            }

            return TraceLog == null ? MethodIndex.Invalid : TraceLog.CodeAddresses.MethodIndex(codeAddress);
        }

        /// <summary>Checks that two physical addresses form a V2 dispatch pair from the same runtime.</summary>
        internal bool IsV2DispatchPair(CodeAddressIndex instrumented, CodeAddressIndex dispatch) =>
            TraceLog != null &&
            AsyncProfilerCallStackStitcher.IsV2DispatchPair(TraceLog, m_runtimeResolver, instrumented, dispatch);

        /// <summary>Checks the bridge's runtime against a captured V2 dispatch pair for this call stack.</summary>
        internal bool HasVerifiedV2DispatchPair(AsyncProfilerCallStack callStack, CodeAddressIndex bridge)
        {
            if (TraceLog == null || m_segmentPlacements == null)
            {
                return false;
            }

            ModuleFileIndex owner = GetHostModuleOwner(bridge);
            for (int i = 0; i < m_segmentPlacements.Count; i++)
            {
                AsyncProfilerCallStackPlacement placement = m_segmentPlacements[i];
                bool hasMatchingPlacementOwner =
                    ReferenceEquals(placement.CallStack, callStack) &&
                    owner != ModuleFileIndex.Invalid &&
                    owner == GetHostModuleOwner(placement.InstrumentedDispatch);
                bool hasMatchingRuntimeSupport =
                    hasMatchingPlacementOwner &&
                    ReferenceEquals(GetRuntimeSupport(bridge), GetRuntimeSupport(placement.InstrumentedDispatch));
                bool hasVerifiedPair =
                    hasMatchingRuntimeSupport &&
                    AsyncProfilerCallStackStitcher.IsV2DispatchPair(
                        TraceLog, m_runtimeResolver, placement.InstrumentedDispatch, placement.Dispatch);
                if (hasVerifiedPair)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Verifies a captured V1 dispatcher or V2 dispatch pair for the bridge's runtime.</summary>
        /// <remarks>
        /// V1 also needs a verified current method or dispatch target when
        /// <paramref name="requireAsyncAnchor"/> is true.
        /// </remarks>
        internal bool HasVerifiedDispatchEntry(
            AsyncProfilerCallStack callStack, CodeAddressIndex bridge, bool requireAsyncAnchor = true)
        {
            if (callStack.Frames.Kind == AsyncCallstackKind.RuntimeAsync)
            {
                return HasVerifiedV2DispatchPair(callStack, bridge);
            }

            if (TraceLog == null || m_segmentPlacements == null)
            {
                return false;
            }

            ModuleFileIndex owner = GetHostModuleOwner(bridge);
            for (int i = 0; i < m_segmentPlacements.Count; i++)
            {
                AsyncProfilerCallStackPlacement placement = m_segmentPlacements[i];
                bool hasMatchingPlacementOwner =
                    ReferenceEquals(placement.CallStack, callStack) &&
                    owner != ModuleFileIndex.Invalid &&
                    owner == GetHostModuleOwner(placement.Dispatch);
                bool hasMatchingRuntimeSupport =
                    hasMatchingPlacementOwner &&
                    ReferenceEquals(GetRuntimeSupport(bridge), GetRuntimeSupport(placement.Dispatch));
                bool hasResolvedV1Dispatcher =
                    hasMatchingRuntimeSupport &&
                    ClassifyBoundary(placement.Dispatch).Kind == AsyncProfilerCallStackStitchBoundaryKind.V1Dispatcher &&
                    TraceLog.CodeAddresses.MethodIndex(placement.Dispatch) != MethodIndex.Invalid;
                if (!hasResolvedV1Dispatcher)
                {
                    continue;
                }

                // Grouping dispatch machinery does not require the pending application target to have started.
                if (!requireAsyncAnchor)
                {
                    return true;
                }

                // Verify the first matching span; do not combine anchors from separate spans.
                for (int frame = Frames.Count - 1; frame >= 0; frame--)
                {
                    if (ReferenceEquals(Frames[frame].Context, callStack))
                    {
                        return HasVerifiedAsyncAnchor(this, frame, callStack);
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Verifies V1 completion ownership for helper cleanup, without claiming the method is running.
        /// </summary>
        /// <remarks>
        /// If <paramref name="allowCompletionUnwind"/> is true, also accepts a normalized return unwind
        /// after the entire call stack has completed.
        /// </remarks>
        internal bool HasVerifiedV1CompletionAnchor(
            AsyncProfilerCallStack callStack, CodeAddressIndex bridge, bool allowCompletionUnwind = false)
        {
            bool hasUnambiguousV1CallStack =
                callStack.Frames.Kind == AsyncCallstackKind.StateMachineAsync &&
                StitchingState.ProcessedCallStackCount == Segments.Count &&
                StitchingState.HasConsistentPhysicalAlignment &&
                StitchingState.UnresolvedV1CompletionCount == 0;
            bool hasVerifiedDispatcher =
                hasUnambiguousV1CallStack &&
                HasVerifiedDispatchEntry(callStack, bridge, requireAsyncAnchor: false);
            if (!hasVerifiedDispatcher)
            {
                return false;
            }

            int completed = callStack.GetCompletedFrameCount(TimestampQpc);
            bool hasCapturedCompletion = completed > 0 && completed <= callStack.Frames.FrameCount;
            if (!hasCapturedCompletion)
            {
                return false;
            }

            CodeAddressIndex capturedAddress = callStack.Frames.CodeAddressAt(completed - 1);
            if (capturedAddress == CodeAddressIndex.Invalid)
            {
                return false;
            }

            foreach (AsyncProfilerStitchedFrame frame in Frames)
            {
                // A normalized return can prove ownership after full completion; it is not a running body.
                bool isCompletionReturn =
                    allowCompletionUnwind &&
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletionReturn &&
                    !frame.IsDispatcherTailUnwind;
                bool hasCompletedReturnRoot =
                    isCompletionReturn &&
                    frame.ContextKind == AsyncProfilerStitchedFrameContextKind.Active &&
                    ReferenceEquals(frame.Context, callStack) &&
                    completed == callStack.Frames.FrameCount;
                bool hasMatchingReturnIdentity =
                    hasCompletedReturnRoot &&
                    frame.SegmentFrameIndex == completed - 1 &&
                    frame.CodeAddress == capturedAddress && frame.Method == MethodIndex.Invalid;
                bool hasVerifiedReturnCompletion =
                    hasMatchingReturnIdentity &&
                    GetMethod(capturedAddress) != MethodIndex.Invalid &&
                    TraceLog.AsyncProfilerCallStacks?.MethodCompletionObserved(callStack) == true &&
                    callStack.GetMethodCompletedFrameCount(TimestampQpc) > 0;
                if (hasVerifiedReturnCompletion)
                {
                    return true;
                }

                // A retained completed body must match the captured method through the same resolver.
                bool hasOwnedCompletedBody =
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody &&
                    frame.ContextKind == AsyncProfilerStitchedFrameContextKind.Active &&
                    ReferenceEquals(frame.Context, callStack) &&
                    frame.SegmentFrameIndex == completed - 1;
                bool hasResolvedCompletedBody =
                    hasOwnedCompletedBody &&
                    frame.CodeAddress != CodeAddressIndex.Invalid && frame.Method != MethodIndex.Invalid;
                bool hasMatchingCompletedMethod =
                    hasResolvedCompletedBody &&
                    GetMethod(frame.CodeAddress) == frame.Method &&
                    GetMethod(capturedAddress) == frame.Method;
                if (hasMatchingCompletedMethod)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Verifies ownership of runtime helpers after a V1 root call stack has fully completed
        /// and has no frames left in the stitched result. Uses saved dispatcher evidence for
        /// the root and its immediate nested call stack instead of an active method frame.
        /// </summary>
        internal bool HasVerifiedCompletedV1OuterDispatch(AsyncProfilerCallStack callStack, CodeAddressIndex bridge)
        {
            bool hasRootCallStack =
                TraceLog != null &&
                m_segmentPlacements != null &&
                Segments.Count >= 2 &&
                ReferenceEquals(callStack, Segments[Segments.Count - 1]);
            bool hasNonemptyV1Root =
                hasRootCallStack &&
                callStack.Frames.Kind == AsyncCallstackKind.StateMachineAsync &&
                callStack.Frames.FrameCount != 0;
            bool hasUnambiguousStitching =
                hasNonemptyV1Root &&
                StitchingState.ProcessedCallStackCount == Segments.Count &&
                StitchingState.HasConsistentPhysicalAlignment &&
                StitchingState.UnresolvedV1CompletionCount == 0;
            bool hasFullNormalCompletion =
                hasUnambiguousStitching &&
                TraceLog.AsyncProfilerCallStacks?.MethodCompletionObserved(callStack) == true &&
                callStack.GetMethodCompletedFrameCount(TimestampQpc) == callStack.Frames.FrameCount &&
                callStack.GetExceptionCompletedFrameCount(TimestampQpc) == 0;
            if (!hasFullNormalCompletion)
            {
                return false;
            }

            AsyncProfilerCallStack nestedCallStack = Segments[Segments.Count - 2];
            bool hasImmediateNestedCallStack = nestedCallStack.Depth == callStack.Depth + 1;
            bool hasSharedVerifiedDispatch =
                hasImmediateNestedCallStack &&
                HasVerifiedDispatchEntry(callStack, bridge, requireAsyncAnchor: false) &&
                HasVerifiedDispatchEntry(nestedCallStack, bridge, requireAsyncAnchor: false);
            if (!hasSharedVerifiedDispatch)
            {
                return false;
            }

            // The completed root must no longer own any output frames.
            foreach (AsyncProfilerStitchedFrame frame in Frames)
            {
                if (ReferenceEquals(frame.Context, callStack))
                {
                    return false;
                }
            }

            return HasEmptyRootSpanAfterNestedSpan(callStack, nestedCallStack);
        }

        /// <summary>Requires an empty root span immediately after the nested dispatch's physical span.</summary>
        private bool HasEmptyRootSpanAfterNestedSpan(AsyncProfilerCallStack callStack, AsyncProfilerCallStack nestedCallStack)
        {
            for (int i = 0; i < m_segmentPlacements.Count; i++)
            {
                AsyncProfilerCallStackPlacement rootPlacement = m_segmentPlacements[i];
                bool hasEmptyRootSpan =
                    ReferenceEquals(rootPlacement.CallStack, callStack) &&
                    rootPlacement.LeafIndex == rootPlacement.RootIndexExclusive;
                if (!hasEmptyRootSpan)
                {
                    continue;
                }

                for (int j = 0; j < m_segmentPlacements.Count; j++)
                {
                    AsyncProfilerCallStackPlacement nestedPlacement = m_segmentPlacements[j];
                    bool hasAdjacentNestedSpan =
                        ReferenceEquals(nestedPlacement.CallStack, nestedCallStack) &&
                        nestedPlacement.LeafIndex < nestedPlacement.RootIndexExclusive &&
                        nestedPlacement.RootIndexExclusive == rootPlacement.LeafIndex;
                    if (hasAdjacentNestedSpan)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Matches a runtime sequence and verifies its physical frames belong to the same runtime.
        /// V1 startup begins in application code, so it uses the sample's support and checks only its helpers.
        /// </summary>
        internal AsyncProfilerRuntimeSequenceMatch MatchRuntimeSequence(
            AsyncProfilerRuntimeSequenceKind kind, int start, int end, bool allowRuntimeInteriors = false)
        {
            bool hasValidRange = start >= 0 && end >= start && end <= Frames.Count;
            if (!hasValidRange)
            {
                throw new ArgumentOutOfRangeException(nameof(start));
            }

            if (start == end)
            {
                return default;
            }

            bool isV1Startup = kind == AsyncProfilerRuntimeSequenceKind.V1Startup;
            AsyncProfilerRuntimeSupport support = isV1Startup ? RuntimeSupport : GetRuntimeSupport(Frames[start].CodeAddress);
            AsyncProfilerRuntimeSequenceMatch match = support.MatchSequence(kind, this, start, end, allowRuntimeInteriors);
            if (match.Length > end - start)
            {
                throw new InvalidOperationException(
                    "A runtime sequence matcher returned a span outside its candidate range.");
            }

            // Preserve rejected candidates so later cleanup can protect the whole sequence.
            if (!match.IsMatch)
            {
                return match;
            }

            bool hasOwnedRuntimeSpan;
            if (isV1Startup)
            {
                bool hasStateMachineBody = IsSyncFrameKind(Frames[start], AsyncProfilerStitchSyncFrameKind.V1StateMachineBody);
                hasOwnedRuntimeSpan = hasStateMachineBody && IsSameRuntimeSpan(support, start + 1, match.Length - 1);
            }
            else
            {
                hasOwnedRuntimeSpan = IsSameRuntimeSpan(support, start, match.Length);
            }

            if (!hasOwnedRuntimeSpan)
            {
                return AsyncProfilerRuntimeSequenceMatch.Reject(match.Length);
            }

            return match;
        }

        #region private

        /// <summary>Selects one runtime-support implementation for the sample.</summary>
        private static AsyncProfilerRuntimeSupport ResolveRuntimeSupport(
            TraceLog traceLog, IReadOnlyList<AsyncProfilerCallStack> segments, List<AsyncProfilerStitchedFrame> frames,
            Func<CodeAddressIndex, AsyncProfilerRuntimeSupport> runtimeSupportResolver,
            AsyncProfilerRuntimeResolver runtimeResolver, bool useProcessSupport)
        {
            // A custom address resolver must not be bypassed by the process-level cache.
            bool hasCapturedProcess =
                useProcessSupport && runtimeResolver != null && segments.Count != 0 &&
                segments[0].Thread.ProcessIndex != ProcessIndex.Invalid;
            if (hasCapturedProcess)
            {
                ProcessIndex process = segments[0].Thread.ProcessIndex;
                for (int i = 1; i < segments.Count; i++)
                {
                    if (segments[i].Thread.ProcessIndex != process)
                    {
                        return AsyncProfilerRuntimeSupport.Common;
                    }
                }

                return runtimeResolver.ResolveProcessSupport(process);
            }

            // Without a process-level choice, compare the support selected for retained runtime frames.
            AsyncProfilerRuntimeSupport selectedSupport = null;
            foreach (AsyncProfilerStitchedFrame frame in frames)
            {
                if (traceLog != null)
                {
                    bool isRuntimeFrame =
                        frame.CodeAddress != CodeAddressIndex.Invalid &&
                        runtimeResolver.IsHostModule(traceLog.CodeAddresses.ModuleFileIndex(frame.CodeAddress));
                    if (!isRuntimeFrame)
                    {
                        continue;
                    }
                }

                AsyncProfilerRuntimeSupport frameSupport = runtimeSupportResolver(frame.CodeAddress) ??
                    throw new InvalidOperationException("The runtime support resolver returned null.");
                if (selectedSupport != null && !ReferenceEquals(selectedSupport, frameSupport))
                {
                    return AsyncProfilerRuntimeSupport.Common;
                }

                selectedSupport = frameSupport;
            }

            return selectedSupport ?? AsyncProfilerRuntimeSupport.Common;
        }

        /// <summary>
        /// Checks that a physical span uses the same runtime-support implementation.
        /// With trace metadata, also requires resolved methods and a shared runtime owner.
        /// </summary>
        private bool IsSameRuntimeSpan(AsyncProfilerRuntimeSupport support, int start, int length)
        {
            ModuleFileIndex runtimeOwner = ModuleFileIndex.Invalid;
            for (int i = start; i < start + length; i++)
            {
                AsyncProfilerStitchedFrame frame = Frames[i];
                bool hasMatchingPhysicalSupport =
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
                    ReferenceEquals(support, GetRuntimeSupport(frame.CodeAddress));
                if (!hasMatchingPhysicalSupport)
                {
                    return false;
                }

                if (TraceLog != null)
                {
                    bool hasResolvedFrame = frame.CodeAddress != CodeAddressIndex.Invalid && frame.Method != MethodIndex.Invalid;
                    if (!hasResolvedFrame)
                    {
                        return false;
                    }

                    ModuleFileIndex frameOwner = GetHostModuleOwner(frame.CodeAddress);
                    if (frameOwner == ModuleFileIndex.Invalid)
                    {
                        return false;
                    }

                    bool hasConflictingOwner = i != start && frameOwner != runtimeOwner;
                    if (hasConflictingOwner)
                    {
                        return false;
                    }

                    runtimeOwner = frameOwner;
                }
            }

            return true;
        }

        /// <summary>Creates an address resolver.</summary>
        private static Func<CodeAddressIndex, AsyncProfilerRuntimeSupport> CreateRuntimeSupportResolver(
            TraceLog traceLog, AsyncProfilerRuntimeResolver runtimeResolver)
        {
            if (traceLog == null)
            {
                return _ => AsyncProfilerRuntimeSupport.Common;
            }

            return codeAddress => codeAddress == CodeAddressIndex.Invalid
                ? AsyncProfilerRuntimeSupport.Common
                : runtimeResolver.ResolveModuleSupport(traceLog.CodeAddresses.ModuleFileIndex(codeAddress));
        }

        private readonly Func<CodeAddressIndex, AsyncProfilerCallStackStitchBoundaryInfo> m_classifyBoundary;
        private readonly Func<CodeAddressIndex, AsyncProfilerRuntimeSupport> m_runtimeSupportResolver;
        private readonly AsyncProfilerRuntimeResolver m_runtimeResolver;
        private readonly IReadOnlyList<AsyncProfilerCallStackPlacement> m_segmentPlacements;
        private readonly Func<CodeAddressIndex, MethodIndex> m_methodOf;

        #endregion
    }

    /// <summary>A presentation transform applied to a structurally stitched async call stack.</summary>
    /// <remarks>
    /// Do not retain the context, its lists, or its call-stack objects after the callback; the host reuses them.
    /// Copy identity values instead. A copied frame still refers to the same reusable
    /// <see cref="AsyncProfilerStitchedFrame.Context"/> object.
    /// </remarks>
    internal delegate void AsyncProfilerCallStackTransform(AsyncProfilerCallStackTransformState context);

    /// <summary>
    /// Runs presentation passes after structural async stack stitching.
    /// Normalization is enabled by default; runtime-helper cleanup is opt-in.
    /// Custom transforms run last, in registration order.
    /// </summary>
    /// <remarks>
    /// Configure before stack generation. Runtime support implementations choose built-in implementations without changing
    /// pass order or enablement. Parent contexts and frame names are handled by the host, not by
    /// standalone <see cref="Transform"/> calls.
    /// </remarks>
    internal sealed class AsyncProfilerCallStackTransformPipeline
    {
        /// <summary>Creates the default pipeline with conservative presentation normalization enabled.</summary>
        public AsyncProfilerCallStackTransformPipeline()
        {
            m_customTransforms = new List<AsyncProfilerCallStackTransform>();
        }

        /// <summary>
        /// Enables verified startup, completion-return, handoff, reentrant, and sampled-wrapper cleanup.
        /// Enabled by default. Frames without sufficient evidence are kept.
        /// Disabling this does not change structural stitching or verified incomplete-root <c>BROKEN</c> markers.
        /// </summary>
        public bool EnableNormalization
        {
            get => m_enableNormalization;
            set
            {
                if (m_enableNormalization != value)
                {
                    m_enableNormalization = value;
                    m_transforms = null;
                }
            }
        }

        /// <summary>
        /// Includes missing historical creation-parent call stacks before presentation. Disabled by default.
        /// Requires recorded creation relationships and parent resume intervals; missing parents are not inferred.
        /// Trims completed frames at child creation only when both method and exception completion observations
        /// exist for the parent's process, kind, and configuration epoch. Otherwise, replays the captured frames.
        /// When trimming is available, the first remaining V1 parent frame displays inferred state <c>-1</c>.
        /// Captured states and IPs are unchanged. Applied by the host, not by standalone <see cref="Transform"/> calls.
        /// </summary>
        public bool EnableParentContexts { get; set; }

        /// <summary>
        /// Adds <c>[Async V1]</c> or <c>[Async V2]</c> prefixes and V1 state labels to active async frames.
        /// Disabled by default. Synchronous frames keep their normal names; no transition frames are added.
        /// Current V1 frames display inferred state <c>-1</c>. Verified completing bodies and return unwinds
        /// display <c>-2</c>; other V1 frames display captured state.
        /// Dispatch targets use ordinary active-async labels; their dispatch role remains in metadata.
        /// Captured states are unchanged. The host applies these names; standalone
        /// <see cref="Transform"/> calls do not produce names.
        /// </summary>
        public bool EnableFrameAnnotations { get; set; }

        /// <summary>
        /// Maximum parent contexts inserted per active call stack when <see cref="EnableParentContexts"/>
        /// is enabled. Defaults to one; does not limit frames within a call stack or active nesting depth.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">The value is not positive.</exception>
        public int MaximumParentContextDepth
        {
            get => m_maximumParentContextDepth;
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(MaximumParentContextDepth));
                }

                m_maximumParentContextDepth = value;
            }
        }

        /// <summary>
        /// Groups verified V1/V2 runtime helpers under a dispatcher marker or removes them.
        /// Disabled as default because helper layouts vary by runtime. Unverified frames and rejected spans are kept.
        /// This is not general framework filtering, it targets async infrastructure machinery.
        /// Independent of structural stitching and <see cref="EnableNormalization"/>.
        /// </summary>
        public bool EnableInfrastructureCleanup
        {
            get => m_enableInfrastructureCleanup;
            set
            {
                if (m_enableInfrastructureCleanup != value)
                {
                    m_enableInfrastructureCleanup = value;
                    m_transforms = null;
                }
            }
        }

        /// <summary>Adds a custom transform that runs after all enabled built-in transforms.</summary>
        /// <remarks>
        /// When <paramref name="requiresCompletionObservations"/> is true, completion metadata is prepared
        /// once per sample before the first custom transform that requests it.
        /// </remarks>
        public void AddCustomTransform(
            AsyncProfilerCallStackTransform transform, bool requiresCompletionObservations = false)
        {
            if (transform is null)
            {
                throw new ArgumentNullException(nameof(transform));
            }

            // Insert preparation only before the first transform that needs completion metadata.
            if (requiresCompletionObservations && !m_hasCompletionObservationTransform)
            {
                m_customTransforms.Add(PrepareCompletionObservations);
                m_hasCompletionObservationTransform = true;
            }

            m_customTransforms.Add(transform);
            m_transforms = null;
        }

        /// <summary>
        /// Removes custom transforms and their metadata-preparation step. Built-in settings are unchanged.
        /// </summary>
        public void ClearCustomTransforms()
        {
            m_customTransforms.Clear();
            m_hasCompletionObservationTransform = false;
            m_transforms = null;
        }

        /// <summary>
        /// Applies the configured presentation transforms to a structural stitching result.
        /// </summary>
        /// <remarks>
        /// Copies the frame list and stitching state before applying transforms.
        /// Diagnostics and captured call-stack objects are shared with the input result.
        /// Does not add parent contexts or generate frame names.
        /// </remarks>
        /// <param name="structuralResult">The result produced by <see cref="AsyncProfilerCallStackStitcher"/>.</param>
        /// <param name="segments">The active async segments used to produce the structural result.</param>
        /// <param name="timestampQpc">The sample timestamp in the trace QPC domain.</param>
        /// <param name="classifyBoundary">Classifies runtime boundary methods for the trace.</param>
        /// <param name="traceLog">
        /// Optional trace metadata for custom transforms and built-in rules.
        /// Rules needing module or symbol metadata leave unverified frames unchanged when it is null.
        /// </param>
        public AsyncProfilerStitchResult Transform(
            AsyncProfilerStitchResult structuralResult,
            IReadOnlyList<AsyncProfilerCallStack> segments,
            long timestampQpc,
            Func<CodeAddressIndex, AsyncProfilerCallStackStitchBoundaryInfo> classifyBoundary,
            TraceLog traceLog = null)
        {
            if (structuralResult is null)
            {
                throw new ArgumentNullException(nameof(structuralResult));
            }

            if (segments is null)
            {
                throw new ArgumentNullException(nameof(segments));
            }

            if (classifyBoundary is null)
            {
                throw new ArgumentNullException(nameof(classifyBoundary));
            }

            var frames = new List<AsyncProfilerStitchedFrame>(structuralResult.Frames);
            AsyncProfilerCallStackStitchingState stitchingState = structuralResult.StitchingState.Copy();
            var context = new AsyncProfilerCallStackTransformState(
                traceLog,
                segments,
                timestampQpc,
                classifyBoundary,
                stitchingState,
                structuralResult.Diagnostics,
                frames,
                segmentPlacements: structuralResult.SegmentPlacements);
            ApplyInPlace(context);
            return new AsyncProfilerStitchResult(frames, structuralResult.Diagnostics, stitchingState);
        }

        /// <summary>Applies the cached pass order to the sample's mutable frames.</summary>
        internal void ApplyInPlace(AsyncProfilerCallStackTransformState context)
        {
            AsyncProfilerCallStackTransform[] transforms = m_transforms ?? (m_transforms = CreateTransforms());
            for (int i = 0; i < transforms.Length; i++)
            {
                transforms[i](context);
            }
        }

        #region private

        /// <summary>Builds the enabled pass order, followed by custom transforms in registration order.</summary>
        private AsyncProfilerCallStackTransform[] CreateTransforms()
        {
            var transforms = new List<AsyncProfilerCallStackTransform>(4 + m_customTransforms.Count);
            if (EnableNormalization)
            {
                transforms.Add(context => ApplyRuntimeTransform(context, AsyncProfilerRuntimeTransformKind.SampledV2Wrapper));
                transforms.Add(context => ApplyRuntimeTransform(context, AsyncProfilerRuntimeTransformKind.ReentrantHandoff));
                transforms.Add(context => ApplyRuntimeTransform(context, AsyncProfilerRuntimeTransformKind.V1Presentation));
            }

            if (EnableInfrastructureCleanup)
            {
                transforms.Add(context => ApplyRuntimeTransform(context, AsyncProfilerRuntimeTransformKind.InfrastructureCleanup));
            }

            transforms.AddRange(m_customTransforms);
            return transforms.ToArray();
        }

        /// <summary>Loads per-call-stack completion observations before completion-aware custom transforms.</summary>
        private static void PrepareCompletionObservations(AsyncProfilerCallStackTransformState context)
        {
            AsyncProfilerCallStacksIndex index = context.TraceLog?.AsyncProfilerCallStacks;
            if (index == null)
            {
                return;
            }

            for (int i = 0; i < context.Segments.Count; i++)
            {
                index.PopulateCompletionObservations(context.Segments[i]);
            }
        }

        /// <summary>
        /// Runs the built-in transform identified by <paramref name="kind"/>.
        /// The sample's runtime support supplies the implementation.
        /// </summary>
        private static void ApplyRuntimeTransform(
            AsyncProfilerCallStackTransformState context, AsyncProfilerRuntimeTransformKind kind)
        {
            AsyncProfilerCallStackTransform transform = context.RuntimeSupport.GetTransform(kind) ??
                throw new InvalidOperationException("A runtime support returned a null presentation transform.");
            transform(context);
        }

        private int m_maximumParentContextDepth = AsyncProfilerParentContextBuilder.DefaultMaximumDepth;
        private bool m_enableNormalization = true;
        private bool m_enableInfrastructureCleanup;
        private bool m_hasCompletionObservationTransform;
        private readonly List<AsyncProfilerCallStackTransform> m_customTransforms;
        private AsyncProfilerCallStackTransform[] m_transforms;

        #endregion
    }
}
