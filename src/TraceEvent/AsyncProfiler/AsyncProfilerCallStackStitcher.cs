// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;

namespace Microsoft.Diagnostics.Tracing.Computers
{
    /// <summary>
    /// Combines a sampled physical call stack with the async call stacks active at that time.
    /// Restores async callers that are missing from the sampled stack.
    /// </summary>
    /// <remarks>
    /// Inputs and output are ordered leaf-to-root.
    /// Without usable async frames, the physical stack is kept unchanged.
    /// Presentation cleanup is handled separately by
    /// <see cref="AsyncProfilerCallStackTransformPipeline"/>.
    /// The <c>Stitch</c> overloads create new output lists and do not modify their inputs.
    /// </remarks>
    internal static class AsyncProfilerCallStackStitcher
    {
        /// <summary>
        /// Merges the physical stack with the async call stacks active at the sample QPC. Both inputs are
        /// leaf-to-root. Uses the supplied cache for boundaries and the index for completion observations and history.
        /// </summary>
        /// <param name="syncLeafToRoot">The native sync stack, leaf-to-root.</param>
        /// <param name="segmentsLeafToRoot">
        /// The async segments active at <paramref name="timestampQpc"/>, leaf-to-root.
        /// </param>
        /// <param name="timestampQpc">The sample time, in the trace's QPC domain.</param>
        /// <param name="boundaries">The per-trace boundary-method cache.</param>
        /// <param name="index">
        /// Provides per-call-stack completion observations and history for wrapper-free V2 targets.
        /// </param>
        /// <param name="processIndex">The process instance that emitted the sampled stack.</param>
        /// <param name="methodOf">
        /// Resolves code addresses to the same canonical method identities used by physical frames.
        /// </param>
        /// <param name="trace">Adds per-segment stitching details to the diagnostics when true.</param>
        public static AsyncProfilerStitchResult Stitch(IReadOnlyList<AsyncProfilerStitchSyncFrame> syncLeafToRoot,
            IReadOnlyList<AsyncProfilerCallStack> segmentsLeafToRoot, long timestampQpc,
            AsyncProfilerCallStackStitchBoundaryCache boundaries, AsyncProfilerCallStacksIndex index,
            ProcessIndex processIndex, Func<CodeAddressIndex, MethodIndex> methodOf, bool trace = false)
        {
            if (boundaries is null)
            {
                throw new ArgumentNullException(nameof(boundaries));
            }

            if (index is null)
            {
                throw new ArgumentNullException(nameof(index));
            }

            if (syncLeafToRoot is null)
            {
                throw new ArgumentNullException(nameof(syncLeafToRoot));
            }

            if (methodOf is null)
            {
                throw new ArgumentNullException(nameof(methodOf));
            }

            var diagnostics = new AsyncProfilerCallStackDiagnostics();
            var stitchingState = new AsyncProfilerCallStackStitchingState();
            var output = new List<AsyncProfilerStitchedFrame>(syncLeafToRoot.Count + 8);
            var placements = new List<AsyncProfilerCallStackPlacement>(segmentsLeafToRoot?.Count ?? 0);

            StitchInto(syncLeafToRoot, segmentsLeafToRoot, timestampQpc, boundaries.Classify, null,
                index.MethodCompletionObserved, processIndex, methodOf, trace, output, diagnostics, stitchingState, placements,
                boundaries.GetRuntimeSupport, boundaries.RuntimeResolver.ResolveProcessSupport(processIndex),
                boundaries.TraceLog, index, boundaries.RuntimeResolver);

            return new AsyncProfilerStitchResult(output, diagnostics, stitchingState, placements);
        }

        /// <summary>
        /// Merges physical and active async call stacks, both leaf-to-root, using supplied boundary and
        /// completion-observation delegates. Also supports synthetic code addresses in tests.
        /// </summary>
        /// <param name="syncLeafToRoot">The native sync stack, leaf-to-root.</param>
        /// <param name="segmentsLeafToRoot">
        /// The async segments active at <paramref name="timestampQpc"/>, leaf-to-root.
        /// </param>
        /// <param name="timestampQpc">The sample time, in the trace's QPC domain.</param>
        /// <param name="classify">
        /// Classifies a sync frame's <see cref="CodeAddressIndex"/> as an async dispatch boundary.
        /// </param>
        /// <param name="methodCompletionObserved">
        /// Reports whether normal-completion events were observed for each kind.
        /// The caller must select observations valid for the supplied call stacks' configuration epochs.
        /// V1 also uses recorded exception counts; V2 wrapper progress already includes exceptions.
        /// </param>
        /// <param name="methodOf">
        /// Resolves code addresses to the same canonical method identities used by physical frames.
        /// </param>
        /// <param name="trace">Adds per-segment stitching details to the diagnostics when true.</param>
        public static AsyncProfilerStitchResult Stitch(IReadOnlyList<AsyncProfilerStitchSyncFrame> syncLeafToRoot,
            IReadOnlyList<AsyncProfilerCallStack> segmentsLeafToRoot, long timestampQpc,
            Func<CodeAddressIndex, AsyncProfilerCallStackStitchBoundaryInfo> classify,
            Func<AsyncCallstackKind, bool> methodCompletionObserved,
            Func<CodeAddressIndex, MethodIndex> methodOf, bool trace = false)
        {
            if (syncLeafToRoot is null)
            {
                throw new ArgumentNullException(nameof(syncLeafToRoot));
            }

            if (classify is null)
            {
                throw new ArgumentNullException(nameof(classify));
            }

            if (methodCompletionObserved is null)
            {
                throw new ArgumentNullException(nameof(methodCompletionObserved));
            }

            if (methodOf is null)
            {
                throw new ArgumentNullException(nameof(methodOf));
            }

            var diagnostics = new AsyncProfilerCallStackDiagnostics();
            var stitchingState = new AsyncProfilerCallStackStitchingState();
            var output = new List<AsyncProfilerStitchedFrame>(syncLeafToRoot.Count + 8);
            var placements = new List<AsyncProfilerCallStackPlacement>(segmentsLeafToRoot?.Count ?? 0);

            StitchInto(syncLeafToRoot, segmentsLeafToRoot, timestampQpc, classify, methodCompletionObserved, null,
                ProcessIndex.Invalid, methodOf, trace, output, diagnostics, stitchingState, placements, null, null, null);

            return new AsyncProfilerStitchResult(output, diagnostics, stitchingState, placements);
        }

        internal static void StitchInto(IReadOnlyList<AsyncProfilerStitchSyncFrame> syncLeafToRoot,
            IReadOnlyList<AsyncProfilerCallStack> segmentsLeafToRoot, long timestampQpc,
            Func<CodeAddressIndex, AsyncProfilerCallStackStitchBoundaryInfo> classify,
            Func<ProcessIndex, AsyncCallstackKind, long, bool> methodCompletionObserved,
            ProcessIndex processIndex, Func<CodeAddressIndex, MethodIndex> methodOf, bool trace,
            List<AsyncProfilerStitchedFrame> output, AsyncProfilerCallStackDiagnostics diagnostics,
            AsyncProfilerCallStackStitchingState stitchingState)
        {
            StitchInto(syncLeafToRoot, segmentsLeafToRoot, timestampQpc, classify, methodCompletionObserved,
                processIndex, methodOf, trace, output, diagnostics, stitchingState, null);
        }

        internal static void StitchInto(IReadOnlyList<AsyncProfilerStitchSyncFrame> syncLeafToRoot,
            IReadOnlyList<AsyncProfilerCallStack> segmentsLeafToRoot, long timestampQpc,
            Func<CodeAddressIndex, AsyncProfilerCallStackStitchBoundaryInfo> classify,
            Func<ProcessIndex, AsyncCallstackKind, long, bool> methodCompletionObserved,
            ProcessIndex processIndex, Func<CodeAddressIndex, MethodIndex> methodOf, bool trace,
            List<AsyncProfilerStitchedFrame> output, AsyncProfilerCallStackDiagnostics diagnostics,
            AsyncProfilerCallStackStitchingState stitchingState,
            List<AsyncProfilerCallStackPlacement> placements,
            Func<CodeAddressIndex, AsyncProfilerRuntimeSupport> runtimeSupportResolver = null,
            AsyncProfilerRuntimeSupport runtimeSupport = null, TraceLog runtimeTraceLog = null,
            AsyncProfilerCallStacksIndex index = null, AsyncProfilerRuntimeResolver runtimeResolver = null)
        {
            if (syncLeafToRoot is null)
            {
                throw new ArgumentNullException(nameof(syncLeafToRoot));
            }

            if (classify is null)
            {
                throw new ArgumentNullException(nameof(classify));
            }

            if (methodCompletionObserved is null)
            {
                throw new ArgumentNullException(nameof(methodCompletionObserved));
            }

            if (methodOf is null)
            {
                throw new ArgumentNullException(nameof(methodOf));
            }

            if (output is null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (diagnostics is null)
            {
                throw new ArgumentNullException(nameof(diagnostics));
            }

            if (stitchingState is null)
            {
                throw new ArgumentNullException(nameof(stitchingState));
            }

            StitchInto(syncLeafToRoot, segmentsLeafToRoot, timestampQpc, classify, null, methodCompletionObserved,
                processIndex, methodOf, trace, output, diagnostics, stitchingState, placements,
                runtimeSupportResolver, runtimeSupport, runtimeTraceLog, index, runtimeResolver);
        }

        /// <summary>Checks that the frames are the expected V2 dispatch methods from the same runtime.</summary>
        internal static bool IsV2DispatchPair(TraceLog traceLog, AsyncProfilerRuntimeResolver runtimeResolver,
            CodeAddressIndex instrumented, CodeAddressIndex dispatch)
        {
            if (instrumented == CodeAddressIndex.Invalid || dispatch == CodeAddressIndex.Invalid)
            {
                return false;
            }

            TraceCodeAddresses addresses = traceLog.CodeAddresses;
            MethodIndex instrumentedMethod = addresses.MethodIndex(instrumented);
            MethodIndex dispatchMethod = addresses.MethodIndex(dispatch);
            if (instrumentedMethod == MethodIndex.Invalid || dispatchMethod == MethodIndex.Invalid || instrumentedMethod == dispatchMethod)
            {
                return false;
            }

            ModuleFileIndex instrumentedModule = addresses.ModuleFileIndex(instrumented);
            ModuleFileIndex dispatchModule = addresses.ModuleFileIndex(dispatch);
            ModuleFileIndex owner = runtimeResolver.GetHostModuleOwner(instrumentedModule);
            if (owner == ModuleFileIndex.Invalid || owner != runtimeResolver.GetHostModuleOwner(dispatchModule))
            {
                return false;
            }

            AsyncProfilerRuntimeSupport instrumentedSupport = runtimeResolver.ResolveModuleSupport(instrumentedModule);
            AsyncProfilerRuntimeSupport dispatchSupport = runtimeResolver.ResolveModuleSupport(dispatchModule);
            if (!ReferenceEquals(instrumentedSupport, dispatchSupport))
            {
                return false;
            }

            string instrumentedName = AsyncProfilerMethodNames.NormalizeMethodName(addresses[instrumented].FullMethodName);
            string dispatchName = AsyncProfilerMethodNames.NormalizeMethodName(addresses[dispatch].FullMethodName);

            string instrumentedMethodName = AsyncProfilerMethodNames.GetBareMethodName(instrumentedName);
            string dispatchMethodName = AsyncProfilerMethodNames.GetBareMethodName(dispatchName);
            if (instrumentedMethodName != AsyncProfilerCallStackStitchBoundary.InstrumentedDispatchContinuationsName ||
                dispatchMethodName != AsyncProfilerCallStackStitchBoundary.DispatchContinuationsName)
            {
                return false;
            }

            return AsyncProfilerMethodNames.GetDeclaringTypeName(instrumentedName) == AsyncProfilerMethodNames.GetDeclaringTypeName(dispatchName);
        }

        /// <summary>True when the list is nonempty and every call stack has captured frames.</summary>
        internal static bool HasUsableSegments(IReadOnlyList<AsyncProfilerCallStack> segments)
        {
            if (segments == null || segments.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < segments.Count; i++)
            {
                if (segments[i].Frames.FrameCount == 0)
                {
                    return false;
                }
            }

            return true;
        }

        #region private

        /// <summary>
        /// Builds the stitched call stack for one sample, reusing the supplied output buffers.
        /// Matches each active async call stack to its physical frames, then adds its missing async callers.
        /// </summary>
        private static void StitchInto(IReadOnlyList<AsyncProfilerStitchSyncFrame> syncLeafToRoot,
            IReadOnlyList<AsyncProfilerCallStack> segmentsLeafToRoot, long timestampQpc,
            Func<CodeAddressIndex, AsyncProfilerCallStackStitchBoundaryInfo> classify,
            Func<AsyncCallstackKind, bool> methodCompletionObserved,
            Func<ProcessIndex, AsyncCallstackKind, long, bool> methodCompletionObservedByProcess,
            ProcessIndex processIndex, Func<CodeAddressIndex, MethodIndex> methodOf, bool trace,
            List<AsyncProfilerStitchedFrame> output, AsyncProfilerCallStackDiagnostics diagnostics,
            AsyncProfilerCallStackStitchingState stitchingState,
            List<AsyncProfilerCallStackPlacement> placements,
            Func<CodeAddressIndex, AsyncProfilerRuntimeSupport> runtimeSupportResolver,
            AsyncProfilerRuntimeSupport runtimeSupport, TraceLog runtimeTraceLog,
            AsyncProfilerCallStacksIndex index = null, AsyncProfilerRuntimeResolver runtimeResolver = null)
        {
            output.Clear();
            diagnostics.Clear();
            stitchingState.Clear();
            placements?.Clear();

            var context = new StitchContext(syncLeafToRoot, segmentsLeafToRoot, timestampQpc, classify,
                methodCompletionObserved, methodCompletionObservedByProcess, processIndex, methodOf, trace,
                output, diagnostics, stitchingState, placements, runtimeSupportResolver,
                runtimeSupport ?? AsyncProfilerRuntimeSupport.Common, runtimeTraceLog, index,
                runtimeResolver ?? (runtimeTraceLog == null ? null : new AsyncProfilerRuntimeResolver(runtimeTraceLog.ModuleFiles)));

            // Without usable logical frames, retain every physical frame so coverage gaps remain visible.
            if (!HasUsableSegments(segmentsLeafToRoot))
            {
                context.AddSyncFrames(0, syncLeafToRoot.Count, null);
                return;
            }

            stitchingState.HasConsistentPhysicalAlignment = true;

            // Both inputs run leaf-to-root. syncPosition is the first physical frame not yet handled.
            int syncPosition = 0;
            int firstSegmentIndex = 0;
            if (segmentsLeafToRoot[0].Frames.Kind == AsyncCallstackKind.RuntimeAsync &&
                StitchSampledV2Dispatch(in context, ref syncPosition, ref firstSegmentIndex))
            {
                return;
            }

            for (int segmentIndex = firstSegmentIndex; segmentIndex < segmentsLeafToRoot.Count; segmentIndex++)
            {
                syncPosition = StitchCallStack(in context, segmentIndex, syncPosition);
            }

            // Keep the physical callers left after the last async call stack.
            context.AddSyncFrames(syncPosition, syncLeafToRoot.Count, null);
        }

        /// <summary>
        /// Handles a sample taken inside V2 dispatch code rather than an application method.
        /// A dispatch entry may identify the next async method without showing that its body ran.
        /// Returns true when the whole sample has been stitched.
        /// </summary>
        private static bool StitchSampledV2Dispatch(in StitchContext context, ref int syncPosition, ref int firstSegmentIndex)
        {
            IReadOnlyList<AsyncProfilerStitchSyncFrame> sync = context.Sync;
            int wrapperPosition = FindFirstV2Wrapper(sync, context.Classify, out bool hasDispatchContinuation);
            if (wrapperPosition == sync.Count)
            {
                if (!hasDispatchContinuation)
                {
                    return false;
                }

                StitchV2DispatchWithoutWrapper(in context);
                return true;
            }

            if (!IsSampledV2WrapperEntry(sync, wrapperPosition) ||
                HasV2DispatchTarget(in context, context.Segments[0], context.Classify(sync[wrapperPosition].CodeAddress).WrapperIndex))
            {
                return false;
            }

            // This entry has no known async target. Keep its physical frames, and continue only if
            // the next enclosing call stack has a verified physical match.
            int physicalEnd = sync.Count;
            if (context.Segments.Count > 1)
            {
                AsyncProfilerRuntimeSupport entrySupport = context.GetRuntimeSupport(sync[wrapperPosition].CodeAddress);
                int entryEnd = wrapperPosition + 1;
                entryEnd += MatchDispatchInfrastructure(in context, entrySupport, AsyncCallstackKind.RuntimeAsync, wrapperPosition, wrapperPosition + 1);

                AsyncProfilerCallStack outer = context.Segments[1];
                bool outerVerified = outer.Frames.Kind == AsyncCallstackKind.RuntimeAsync
                    ? FindEnclosingV2CurrentSyncFrame(in context, outer, entryEnd) >= 0
                    : FindBoundary(sync, entryEnd, outer.Frames.Kind, context.Classify, out _) < sync.Count;
                if (outerVerified)
                {
                    physicalEnd = entryEnd;
                }
            }

            bool stitchedWholeSample = physicalEnd == sync.Count;
            UseV2SyncLayout(in context, physicalEnd);
            if (context.Trace)
            {
                context.Diagnostics.Note(
                    stitchedWholeSample
                    ? "V2 not stitched: sampled dispatch entry has no identifiable target; used the sync layout."
                    : "Inner V2 not stitched: sampled dispatch entry has no identifiable target; retained its entry slice and continued with the enclosing call stack.");
            }

            if (stitchedWholeSample)
            {
                return true;
            }

            // The entry work has no async target, so do not record a placement for it.
            context.CountProcessedCallStack();
            syncPosition = physicalEnd;
            firstSegmentIndex = 1;
            return false;
        }

        /// <summary>
        /// Handles a sample inside V2 dispatch infrastructure with no continuation wrapper. Only a verified
        /// dispatch pair and recorded progress can identify a target; otherwise the sync layout is kept.
        /// </summary>
        private static void StitchV2DispatchWithoutWrapper(in StitchContext context)
        {
            IReadOnlyList<AsyncProfilerStitchSyncFrame> sync = context.Sync;
            AsyncProfilerCallStack segment = context.Segments[0];
            if (context.Segments.Count != 1 ||
                !TryFindV2DispatchTargetWithoutWrapper(in context, segment,
                    out int dispatchStart, out int dispatchEnd, out int completedFrameCount))
            {
                UseV2SyncLayout(in context, sync.Count);
                if (context.Trace)
                {
                    context.Diagnostics.Note("V2 not stitched: no continuation-wrapper root-ward of the leaf; used the sync layout.");
                }

                return;
            }

            // Keep sampled callees, represent the dispatch pair, and add the target's async callers.
            List<AsyncProfilerStitchedFrame> output = context.Output;
            context.AddSyncFrames(0, dispatchStart, segment);
            output.Add(AsyncProfilerStitchedFrame.CreateAsyncDispatcherFrames(sync[dispatchStart], segment));
            output.Add(AsyncProfilerStitchedFrame.CreateAsyncDispatchTarget(segment, completedFrameCount));
            for (int k = completedFrameCount + 1; k < segment.Frames.FrameCount; k++)
            {
                output.Add(AsyncProfilerStitchedFrame.CreateAsync(segment, k));
            }

            context.Placements?.Add(new AsyncProfilerCallStackPlacement(segment, 0, output.Count, sync[dispatchStart].CodeAddress, sync[dispatchStart + 1].CodeAddress));
            context.AddSyncFrames(dispatchEnd, sync.Count, null);

            context.CountProcessedCallStack();
            context.Diagnostics.V2InfrastructureFramesCollapsed += dispatchEnd - dispatchStart;
            if (context.Trace)
            {
                context.Diagnostics.Note(
                    $"V2 dispatch target without wrapper: completed={completedFrameCount}; " +
                    "represented the verified dispatch pair with an async dispatcher marker.");
            }
        }

        /// <summary>Keeps the leading physical frames unstitched because no V2 target could be identified.</summary>
        private static void UseV2SyncLayout(in StitchContext context, int physicalEnd)
        {
            context.State.UsesV2SynchronousLayout = true;
            context.Diagnostics.V2SyncLayoutUsed++;
            context.AddSyncFrames(0, physicalEnd, null);
        }

        /// <summary>
        /// Stitches one active async call stack against the physical frames starting at <paramref name="syncPosition"/>.
        /// Returns the first physical frame left for the enclosing call stack.
        /// </summary>
        private static int StitchCallStack(in StitchContext context, int segmentIndex, int syncPosition)
        {
            var match = new CallStackMatch(context.Segments[segmentIndex], segmentIndex,
                segmentIndex == context.Segments.Count - 1, syncPosition, context.Output.Count);
            context.CountProcessedCallStack();

            MatchDispatchBoundary(in context, ref match);
            MatchCompletedFrames(in context, ref match);
            MatchCurrentFrame(in context, ref match);
            ChoosePhysicalEnd(in context, ref match);

            AddPhysicalFrames(in context, in match);
            AddAsyncFrames(in context, in match);
            NoteStitchedCallStack(in context, in match);

            return AdvancePastCallStack(in context, ref match);
        }

        /// <summary>Finds the call stack's dispatcher and the end of the physical frames it can claim.</summary>
        private static void MatchDispatchBoundary(in StitchContext context, ref CallStackMatch match)
        {
            IReadOnlyList<AsyncProfilerStitchSyncFrame> sync = context.Sync;
            match.BoundaryPosition = FindBoundary(sync, match.SyncStart, match.Kind, context.Classify, out match.BoundaryInfo);
            match.BoundaryFound = match.BoundaryPosition < sync.Count;
            match.Support = match.BoundaryFound
                ? context.GetRuntimeSupport(sync[match.BoundaryPosition].CodeAddress)
                : context.RuntimeSupport;

            if (!match.BoundaryFound)
            {
                context.State.HasConsistentPhysicalAlignment = false;
                context.Diagnostics.BoundariesNotFound++;
                if (context.Diagnostics.CanRetainMessage)
                {
                    context.Diagnostics.Note(
                        $"No {match.Kind} dispatch boundary found at or root-ward of sync frame {match.SyncStart}; " +
                        "recovering a unique physical alignment when available, " +
                        "otherwise appending logical async frames.");
                }
                else
                {
                    context.Diagnostics.MessagesDropped++;
                }
            }

            // Stop the physical slice just before its dispatcher. If that boundary is missing,
            // do not take frames belonging to a verified enclosing V2 call stack.
            match.SyncEnd = match.BoundaryPosition;
            if (!match.BoundaryFound && match.IsV1)
            {
                for (int outerIndex = match.SegmentIndex + 1; outerIndex < context.Segments.Count; outerIndex++)
                {
                    AsyncProfilerCallStack outer = context.Segments[outerIndex];
                    if (outer.Frames.Kind != AsyncCallstackKind.RuntimeAsync)
                    {
                        continue;
                    }

                    int outerCurrentSyncPosition = FindEnclosingV2CurrentSyncFrame(in context, outer, match.SyncStart);
                    if (outerCurrentSyncPosition >= 0)
                    {
                        match.SyncEnd = outerCurrentSyncPosition;
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// Counts finished async frames. This is also the current captured-frame index, if one remains.
        /// Then decides whether a V2 dispatch entry identifies the target instead of a sampled body.
        /// </summary>
        private static void MatchCompletedFrames(in StitchContext context, ref CallStackMatch match)
        {
            // An enclosing call stack may be fully complete while its dispatcher is still on the stack.
            // The leaf call stack can instead retain its final physical method while it returns.
            bool allowFullyCompleted = match.BoundaryFound && match.SegmentIndex > 0;
            match.CompletedFrameCount = ComputeCompletedFrameCount(in context, ref match, allowFullyCompleted);
            match.FullyCompleted = allowFullyCompleted && match.CompletedFrameCount == match.Frames.FrameCount;

            if (!match.IsV2 || !match.BoundaryFound)
            {
                return;
            }

            // A zero-IP first continuation still occupies a slot; do not treat its successor as current.
            AsyncProfilerCallStackFrames frames = match.Frames;
            match.InitialV2Dispatch =
                match.BoundaryPosition > match.SyncStart &&
                match.CompletedFrameCount == 0 &&
                frames.InstructionPointerAt(0) == 0 &&
                frames.CodeAddressAt(0) == CodeAddressIndex.Invalid &&
                GetV2DispatchTargetFrameIndex(in context, match.Segment, match.BoundaryInfo.WrapperIndex) == 0;

            // A resolved native entry can identify a target without being a sampled application body.
            match.NativeV2DispatchTarget =
                !match.FullyCompleted &&
                HasV2NativeDispatchEntry(in context, in match) &&
                HasV2DispatchTarget(in context, match.Segment, match.BoundaryInfo.WrapperIndex);
        }

        /// <summary>
        /// Finds the sampled current method, or a V1 box that identifies its pending target, and any completed
        /// V1 method still returning. V2 dispatch-entry evidence alone is not treated as a sampled application body.
        /// </summary>
        private static void MatchCurrentFrame(in StitchContext context, ref CallStackMatch match)
        {
            IReadOnlyList<AsyncProfilerStitchSyncFrame> sync = context.Sync;
            if (!match.FullyCompleted && !match.V1CompletionAmbiguous && !match.NativeV2DispatchTarget && !match.InitialV2Dispatch)
            {
                match.CurrentSyncPosition = match.IsV1
                    ? FindCurrentSyncFrame(match.Frames, match.CompletedFrameCount, sync, match.SyncStart, match.SyncEnd,
                        context.MethodOf, match.BoundaryFound || match.RecoveredSyncEnd >= 0)
                    : FindCurrentV2SyncFrame(match.Frames, match.CompletedFrameCount, sync, match.SyncStart, match.BoundaryPosition,
                        context.MethodOf, match.BoundaryFound, context.Diagnostics, context.State);
            }

            // Completion counts cannot identify the current occurrence of a repeated V1 method.
            if (match.IsV1 && match.V1CompletionTailVerified && match.CurrentSyncPosition >= 0 &&
                IsCurrentMethodAlsoCompleted(match.Frames, match.CompletedFrameCount, context.MethodOf))
            {
                match.CurrentSyncPosition = -1;
                match.V1CompletionAmbiguous = true;
                context.State.UnresolvedV1CompletionCount++;
                context.Diagnostics.V1CompletionAlignmentAmbiguous = context.State.UnresolvedV1CompletionCount;
                context.Diagnostics.Note(
                    "V1 completion counts cannot distinguish repeated current/completed occurrences; " +
                    "retained the physical slice.");
            }

            // A completed V1 method can still be physically present while returning into its caller.
            bool mayBeReturning =
                match.IsV1 && match.BoundaryFound && match.V1CompletionTailVerified &&
                !match.FullyCompleted && !match.V1CompletionAmbiguous && match.CurrentSyncPosition < 0;
            if (mayBeReturning &&
                TryFindV1CompletingFrame(match.Frames, match.CompletedFrameCount, sync, match.SyncStart, match.BoundaryPosition,
                    context.MethodOf, out match.CompletingSyncPosition))
            {
                context.Diagnostics.V1CompletionTailUsed++;
            }

            // A unique physical match can recover the root call stack when its dispatcher was truncated.
            match.IncompleteRoot =
                match.IsRoot && !match.BoundaryFound && !match.V1CompletionAmbiguous &&
                TryMatchTruncatedRoot(in context, ref match);

            if (match.IsV2)
            {
                if (match.IncompleteRoot)
                {
                    context.Diagnostics.V2TruncatedSuffixFallbackUsed++;
                }
                else if (!match.CompletionObserved)
                {
                    context.Diagnostics.V2WrapperFallbackUsed++;
                }
            }
        }

        /// <summary>
        /// Keeps sampled frames through the running or finishing method, when that match is verified.
        /// Async callers added later replace the older V1 transition frames up to the dispatcher.
        /// </summary>
        private static void ChoosePhysicalEnd(in StitchContext context, ref CallStackMatch match)
        {
            IReadOnlyList<AsyncProfilerStitchSyncFrame> sync = context.Sync;
            int anchorSyncPosition = match.CurrentSyncPosition >= 0 ? match.CurrentSyncPosition : match.CompletingSyncPosition;
            match.RecoveredInner =
                !match.BoundaryFound &&
                match.SyncEnd < match.BoundaryPosition &&
                match.RecoveredSyncEnd >= 0 &&
                match.RecoveredSyncEnd <= match.SyncEnd &&
                anchorSyncPosition >= 0 &&
                !match.V1CompletionAmbiguous;

            bool anchorIsVerified = match.IncompleteRoot || match.RecoveredInner || (match.IsV1 && match.BoundaryFound);
            match.PhysicalEnd = match.SyncEnd;
            if (match.FullyCompleted)
            {
                match.PhysicalEnd = match.SyncStart;
            }
            else if (anchorSyncPosition >= 0 && anchorIsVerified)
            {
                match.PhysicalEnd = anchorSyncPosition + 1;
            }

            match.HasDispatchTarget =
                match.CompletedFrameCount < match.Frames.FrameCount &&
                match.Frames.IsCapturedFrameAt(match.CompletedFrameCount);

            if (!match.IsV2 || !match.BoundaryFound)
            {
                return;
            }

            match.SampledV2ResumeStub =
                match.BoundaryPosition == match.SyncStart + 1 &&
                IsV2ResumeStub(sync[match.SyncStart]);

            // Consume only the single stub connecting a matched body or verified entry work to its wrapper.
            bool stubConnectsMatchedWork =
                match.CurrentSyncPosition >= match.SyncStart ||
                match.NativeV2DispatchTarget ||
                (match.SampledV2ResumeStub && match.HasDispatchTarget);
            if (!match.FullyCompleted && match.BoundaryPosition > match.SyncStart &&
                IsV2ResumeStub(sync[match.BoundaryPosition - 1]) && stubConnectsMatchedWork)
            {
                match.PhysicalEnd--;
                context.Diagnostics.V2InfrastructureFramesCollapsed++;
            }
        }

        /// <summary>Adds the kept physical frames, marking the current, completing, and V1 box-carrier frames.</summary>
        private static void AddPhysicalFrames(in StitchContext context, in CallStackMatch match)
        {
            List<AsyncProfilerStitchedFrame> output = context.Output;
            AsyncProfilerCallStack segment = match.Segment;
            int completed = match.CompletedFrameCount;
            for (int i = match.SyncStart; i < match.PhysicalEnd; i++)
            {
                AsyncProfilerStitchSyncFrame physicalFrame = context.Sync[i];
                AsyncProfilerStitchedFrame frame;
                if (i == match.CurrentSyncPosition)
                {
                    frame = match.IsV1 && physicalFrame.Method != context.MethodOf(match.Frames.CodeAddressAt(completed))
                        ? AsyncProfilerStitchedFrame.CreateAsyncDispatchTarget(physicalFrame, segment, completed)
                        : AsyncProfilerStitchedFrame.CreateAsyncCurrent(physicalFrame.CodeAddress, physicalFrame.Method, segment, completed);
                }
                else if (i == match.CompletingSyncPosition)
                {
                    frame = AsyncProfilerStitchedFrame.CreateV1CompletedBody(physicalFrame.CodeAddress, physicalFrame.Method, segment, completed - 1);
                }
                else
                {
                    frame = AsyncProfilerStitchedFrame.CreateSync(physicalFrame, segment);
                }

                if (frame.IsStateMachineBoxCarrier)
                {
                    // The box identifies a pending target, not an executing application body.
                    output.Add(AsyncProfilerStitchedFrame.CreateAsyncDispatcherFrames(physicalFrame, segment));
                }

                output.Add(frame);
            }

            if (match.InitialV2Dispatch)
            {
                // Show the retained entry as dispatcher frames rather than an application method.
                output[output.Count - 1] = output[output.Count - 1].WithPresentation(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames);
            }
        }

        /// <summary>Adds a known but unsampled dispatch target, then the async callers missing from the physical stack.</summary>
        private static void AddAsyncFrames(in StitchContext context, in CallStackMatch match)
        {
            List<AsyncProfilerStitchedFrame> output = context.Output;
            AsyncProfilerCallStack segment = match.Segment;
            int boundaryPosition = match.BoundaryPosition;

            // A known V2 target replaces its wrapper/stub, after any verified native entry work.
            // V1 retains its dispatcher as physical evidence.
            // Neither target claims that the application body is physically executing.
            bool boundaryIdentifiesTarget =
                match.IsV1 || boundaryPosition == 0 || match.SampledV2ResumeStub || match.NativeV2DispatchTarget;
            bool targetIsUnsampled = !match.FullyCompleted && match.CurrentSyncPosition < 0 && match.HasDispatchTarget;
            if (match.BoundaryFound && boundaryIdentifiesTarget && targetIsUnsampled)
            {
                if (match.IsV2 && boundaryPosition == 0)
                {
                    context.Diagnostics.V2LeafWrapperDropped++;
                }
                else if (match.IsV1 && match.CompletingSyncPosition < 0)
                {
                    output.Add(AsyncProfilerStitchedFrame.CreateSync(context.Sync[boundaryPosition], segment));
                }
                else if (match.NativeV2DispatchTarget)
                {
                    int entry = IsV2ResumeStub(context.Sync[boundaryPosition - 1]) ? boundaryPosition - 1 : boundaryPosition;
                    output.Add(AsyncProfilerStitchedFrame.CreateAsyncDispatcherFrames(context.Sync[entry], segment));
                }

                output.Add(AsyncProfilerStitchedFrame.CreateAsyncDispatchTarget(segment, match.CompletedFrameCount));
            }

            for (int k = match.RemainingStart; k < match.Frames.FrameCount; k++)
            {
                output.Add(AsyncProfilerStitchedFrame.CreateAsync(segment, k));
            }
        }

        private static void NoteStitchedCallStack(in StitchContext context, in CallStackMatch match)
        {
            if (!context.Trace)
            {
                return;
            }

            AsyncProfilerCallStackDiagnostics diagnostics = context.Diagnostics;
            if (!diagnostics.CanRetainMessage)
            {
                diagnostics.MessagesDropped++;
                return;
            }

            string boundaryDesc;
            if (!match.BoundaryFound)
            {
                boundaryDesc = "no-boundary";
            }
            else if (match.BoundaryInfo.Kind == AsyncProfilerCallStackStitchBoundaryKind.V2Dispatcher)
            {
                boundaryDesc = $"{match.BoundaryInfo.Kind}[{match.BoundaryInfo.WrapperIndex}]@sync{match.BoundaryPosition}";
            }
            else
            {
                boundaryDesc = $"{match.BoundaryInfo.Kind}@sync{match.BoundaryPosition}";
            }

            int frameCount = match.Frames.FrameCount;
            int remainingStart = match.RemainingStart;
            int splicedCount = Math.Max(0, frameCount - remainingStart);
            string emittedSync = match.FullyCompleted ? "none (fully completed)" : $"[{match.SyncStart}..{match.BoundaryPosition})";
            diagnostics.Note(
                $"segment[{context.State.ProcessedCallStackCount - 1}] kind={match.Kind} boundary={boundaryDesc} " +
                $"emittedSync={emittedSync} " +
                $"completed={match.CompletedFrameCount} spliced=[{remainingStart}..{frameCount}) " +
                $"({splicedCount} frame(s)) frameCount={frameCount}");
        }

        /// <summary>
        /// Moves past the call stack's physical slice, dispatcher, and adjacent runtime frames, then records its placement.
        /// Returns the first physical frame left for the enclosing call stack.
        /// </summary>
        private static int AdvancePastCallStack(in StitchContext context, ref CallStackMatch match)
        {
            IReadOnlyList<AsyncProfilerStitchSyncFrame> sync = context.Sync;
            int syncPosition;
            if (match.RecoveredInner)
            {
                syncPosition = match.RecoveredSyncEnd;
            }
            else if (match.BoundaryFound)
            {
                syncPosition = match.BoundaryPosition + 1;
            }
            else
            {
                syncPosition = match.SyncEnd;
            }

            CodeAddressIndex instrumentedDispatch = CodeAddressIndex.Invalid;
            CodeAddressIndex dispatch = CodeAddressIndex.Invalid;
            if (match.BoundaryFound)
            {
                if (match.IsV1)
                {
                    dispatch = sync[match.BoundaryPosition].CodeAddress;
                }

                // Consume verified runtime frames next to this dispatcher before matching the enclosing call stack.
                // Otherwise they could be mistaken for physical work between the call stacks.
                int infrastructureCount = MatchDispatchInfrastructure(in context, match.Support, match.Kind, match.BoundaryPosition, syncPosition);
                if (match.IsV2 && infrastructureCount == 2)
                {
                    instrumentedDispatch = sync[syncPosition].CodeAddress;
                    dispatch = sync[syncPosition + 1].CodeAddress;
                }

                syncPosition += infrastructureCount;
                if (match.IsV1)
                {
                    context.Diagnostics.V1InfrastructureFramesCollapsed += infrastructureCount;
                }
                else
                {
                    context.Diagnostics.V2InfrastructureFramesCollapsed += infrastructureCount;
                }

                // A surviving dispatcher can still have its required callers cut off inside the
                // runtime infrastructure.
                match.IncompleteRoot =
                    match.IsRoot && match.IsV1 && syncPosition == sync.Count &&
                    AreDispatcherAndCallersResolved(in context, match.BoundaryPosition);
            }

            // Record the async span before adding BROKEN, which marks missing physical callers.
            if (!match.InitialV2Dispatch)
            {
                context.Placements?.Add(new AsyncProfilerCallStackPlacement(match.Segment, match.OutputStart, context.Output.Count, instrumentedDispatch, dispatch));
            }

            if (match.IncompleteRoot)
            {
                context.Output.Add(AsyncProfilerStitchedFrame.CreateIncompletePhysicalRoot());
                context.Diagnostics.IncompleteAsyncRootsNormalized++;
            }

            return syncPosition;
        }

        /// <summary>
        /// The inputs and output buffers shared by every step of one stitch. Passed by <c>in</c> so the per-sample
        /// path does not allocate.
        /// </summary>
        private readonly struct StitchContext
        {
            public StitchContext(IReadOnlyList<AsyncProfilerStitchSyncFrame> sync,
                IReadOnlyList<AsyncProfilerCallStack> segments, long timestampQpc,
                Func<CodeAddressIndex, AsyncProfilerCallStackStitchBoundaryInfo> classify,
                Func<AsyncCallstackKind, bool> methodCompletionObserved,
                Func<ProcessIndex, AsyncCallstackKind, long, bool> methodCompletionObservedByProcess,
                ProcessIndex processIndex, Func<CodeAddressIndex, MethodIndex> methodOf, bool trace,
                List<AsyncProfilerStitchedFrame> output, AsyncProfilerCallStackDiagnostics diagnostics,
                AsyncProfilerCallStackStitchingState state, List<AsyncProfilerCallStackPlacement> placements,
                Func<CodeAddressIndex, AsyncProfilerRuntimeSupport> runtimeSupportResolver,
                AsyncProfilerRuntimeSupport runtimeSupport, TraceLog traceLog,
                AsyncProfilerCallStacksIndex index, AsyncProfilerRuntimeResolver runtimeResolver)
            {
                Sync = sync;
                Segments = segments;
                TimestampQpc = timestampQpc;
                Classify = classify;
                MethodOf = methodOf;
                Trace = trace;
                Output = output;
                Diagnostics = diagnostics;
                State = state;
                Placements = placements;
                RuntimeSupportResolver = runtimeSupportResolver;
                RuntimeSupport = runtimeSupport;
                TraceLog = traceLog;
                Index = index;
                RuntimeResolver = runtimeResolver;
                m_methodCompletionObserved = methodCompletionObserved;
                m_methodCompletionObservedByProcess = methodCompletionObservedByProcess;
                m_processIndex = processIndex;
            }

            /// <summary>The physical stack, leaf-to-root.</summary>
            public IReadOnlyList<AsyncProfilerStitchSyncFrame> Sync { get; }

            /// <summary>The active async call stacks, leaf-to-root.</summary>
            public IReadOnlyList<AsyncProfilerCallStack> Segments { get; }

            public long TimestampQpc { get; }
            public Func<CodeAddressIndex, AsyncProfilerCallStackStitchBoundaryInfo> Classify { get; }
            public Func<CodeAddressIndex, MethodIndex> MethodOf { get; }
            public bool Trace { get; }
            public List<AsyncProfilerStitchedFrame> Output { get; }
            public AsyncProfilerCallStackDiagnostics Diagnostics { get; }
            public AsyncProfilerCallStackStitchingState State { get; }
            public List<AsyncProfilerCallStackPlacement> Placements { get; }

            /// <summary>Selects the runtime support for a frame, or null to use <see cref="RuntimeSupport"/> everywhere.</summary>
            public Func<CodeAddressIndex, AsyncProfilerRuntimeSupport> RuntimeSupportResolver { get; }

            public AsyncProfilerRuntimeSupport RuntimeSupport { get; }

            /// <summary>The trace that resolves modules, or null for synthetic code addresses.</summary>
            public TraceLog TraceLog { get; }

            public AsyncProfilerCallStacksIndex Index { get; }
            public AsyncProfilerRuntimeResolver RuntimeResolver { get; }

            /// <summary>Whether normal-completion events were observed for the call stack's kind and configuration.</summary>
            public bool IsCompletionObserved(AsyncProfilerCallStack segment) =>
                m_methodCompletionObserved != null
                ? m_methodCompletionObserved(segment.Frames.Kind)
                : m_methodCompletionObservedByProcess(m_processIndex, segment.Frames.Kind, segment.StartQpc);

            public AsyncProfilerRuntimeSupport GetRuntimeSupport(CodeAddressIndex codeAddress) =>
                RuntimeSupportResolver == null ? RuntimeSupport : RuntimeSupportResolver(codeAddress);

            public void AddSyncFrames(int start, int end, AsyncProfilerCallStack callStack)
            {
                for (int i = start; i < end; i++)
                {
                    Output.Add(AsyncProfilerStitchedFrame.CreateSync(Sync[i], callStack));
                }
            }

            public void CountProcessedCallStack()
            {
                State.ProcessedCallStackCount++;
                Diagnostics.SegmentsProcessed = State.ProcessedCallStackCount;
            }

            private readonly Func<AsyncCallstackKind, bool> m_methodCompletionObserved;
            private readonly Func<ProcessIndex, AsyncCallstackKind, long, bool> m_methodCompletionObservedByProcess;
            private readonly ProcessIndex m_processIndex;
        }

        /// <summary>
        /// How one async call stack matches the physical stack. Each stitch step fills in the fields it decides.
        /// </summary>
        private struct CallStackMatch
        {
            public CallStackMatch(AsyncProfilerCallStack segment, int segmentIndex, bool isRoot, int syncStart, int outputStart)
            {
                this = default;
                Segment = segment;
                Frames = segment.Frames;
                Kind = Frames.Kind;
                SegmentIndex = segmentIndex;
                IsRoot = isRoot;
                SyncStart = syncStart;
                OutputStart = outputStart;
                CurrentSyncPosition = -1;
                CompletingSyncPosition = -1;
            }

            public readonly AsyncProfilerCallStack Segment;
            public readonly AsyncProfilerCallStackFrames Frames;
            public readonly AsyncCallstackKind Kind;
            public readonly int SegmentIndex;

            /// <summary>Whether this is the root-most active call stack.</summary>
            public readonly bool IsRoot;

            /// <summary>The first physical frame not claimed by an inner call stack.</summary>
            public readonly int SyncStart;

            /// <summary>The first output frame added for this call stack.</summary>
            public readonly int OutputStart;

            /// <summary>The dispatcher position, or the physical frame count when it was not found.</summary>
            public int BoundaryPosition;
            public bool BoundaryFound;
            public AsyncProfilerCallStackStitchBoundaryInfo BoundaryInfo;
            public AsyncProfilerRuntimeSupport Support;

            /// <summary>The end of the physical frames this call stack can claim.</summary>
            public int SyncEnd;

            public int CompletedFrameCount;
            public bool CompletionObserved;
            public bool FullyCompleted;

            /// <summary>Where a truncated V1 match ended, or -1.</summary>
            public int RecoveredSyncEnd;
            public bool V1CompletionAmbiguous;
            public bool V1CompletionTailVerified;

            /// <summary>The sampled entry is the first V2 dispatch, before any continuation has resumed.</summary>
            public bool InitialV2Dispatch;

            /// <summary>A native runtime entry identifies the V2 target without a sampled body.</summary>
            public bool NativeV2DispatchTarget;

            /// <summary>The sampled current method, or -1.</summary>
            public int CurrentSyncPosition;

            /// <summary>A completed V1 method still returning into its caller, or -1.</summary>
            public int CompletingSyncPosition;

            /// <summary>The root call stack's physical callers are missing.</summary>
            public bool IncompleteRoot;
            public bool RecoveredInner;

            /// <summary>The end of the physical frames kept for this call stack.</summary>
            public int PhysicalEnd;
            public bool HasDispatchTarget;

            /// <summary>The sample is in the V2 resume stub directly below the wrapper.</summary>
            public bool SampledV2ResumeStub;

            public bool IsV1 => Kind == AsyncCallstackKind.StateMachineAsync;
            public bool IsV2 => Kind == AsyncCallstackKind.RuntimeAsync;

            /// <summary>
            /// The first async frame to append. Dispatch-only entry work does not establish that the pending
            /// logical continuation has resumed.
            /// </summary>
            public int RemainingStart => InitialV2Dispatch ? Frames.FrameCount : CompletedFrameCount + 1;
        }

        /// <summary>
        /// Whether every frame from the dispatcher to the captured root is resolved, and the dispatcher is in a
        /// host runtime module.
        /// </summary>
        private static bool AreDispatcherAndCallersResolved(in StitchContext context, int boundaryIndex)
        {
            IReadOnlyList<AsyncProfilerStitchSyncFrame> frames = context.Sync;
            for (int i = boundaryIndex; i < frames.Count; i++)
            {
                if (frames[i].CodeAddress == CodeAddressIndex.Invalid || frames[i].Method == MethodIndex.Invalid)
                {
                    return false;
                }
            }

            TraceLog traceLog = context.TraceLog;
            return traceLog == null || context.RuntimeResolver.IsHostModule(traceLog.CodeAddresses.ModuleFileIndex(frames[boundaryIndex].CodeAddress));
        }

        /// <summary>
        /// Returns how many runtime frames from <paramref name="start"/> are the dispatcher's verified infrastructure,
        /// owned by the same runtime as the dispatcher, or 0.
        /// </summary>
        private static int MatchDispatchInfrastructure(in StitchContext context, AsyncProfilerRuntimeSupport support,
            AsyncCallstackKind kind, int boundaryIndex, int start)
        {
            IReadOnlyList<AsyncProfilerStitchSyncFrame> frames = context.Sync;
            TraceLog traceLog = context.TraceLog;
            AsyncProfilerRuntimeSequenceMatch match = support.MatchDispatchInfrastructure(kind, frames, start, frames.Count, context.Classify);
            if (match.Length > frames.Count - start)
            {
                throw new InvalidOperationException("A runtime infrastructure matcher returned a span outside its candidate range.");
            }

            if (!match.IsMatch)
            {
                return 0;
            }

            ModuleFileIndex owner =
                traceLog == null
                ? ModuleFileIndex.Invalid
                : context.RuntimeResolver.GetHostModuleOwner(traceLog.CodeAddresses.ModuleFileIndex(frames[boundaryIndex].CodeAddress));

            for (int i = start; i < start + match.Length; i++)
            {
                CodeAddressIndex codeAddress = frames[i].CodeAddress;
                AsyncProfilerCallStackStitchBoundaryInfo boundary = context.Classify(codeAddress);
                bool isOtherDispatchMarker = boundary.IsDispatchMarker &&
                    (kind != AsyncCallstackKind.RuntimeAsync || boundary.Kind != AsyncProfilerCallStackStitchBoundaryKind.V2DispatcherInfrastructure);
                bool isOtherRuntime = context.RuntimeSupportResolver != null && !ReferenceEquals(support, context.RuntimeSupportResolver(codeAddress));
                if (isOtherDispatchMarker || isOtherRuntime)
                {
                    return 0;
                }

                if (traceLog != null &&
                    (frames[i].Method == MethodIndex.Invalid || owner == ModuleFileIndex.Invalid ||
                     owner != context.RuntimeResolver.GetHostModuleOwner(traceLog.CodeAddresses.ModuleFileIndex(codeAddress))))
                {
                    return 0;
                }
            }

            return match.Length;
        }

        /// <summary>
        /// Whether the frame next to the V2 wrapper is a native runtime entry that dispatches the pending target,
        /// with no sampled body or other dispatch entry that would make the attribution ambiguous.
        /// </summary>
        private static bool HasV2NativeDispatchEntry(in StitchContext context, in CallStackMatch match)
        {
            AsyncProfilerCallStackFrames frames = match.Frames;
            int completed = match.CompletedFrameCount;
            IReadOnlyList<AsyncProfilerStitchSyncFrame> sync = context.Sync;
            int pSync = match.SyncStart;
            int boundaryPos = match.BoundaryPosition;
            TraceLog traceLog = context.TraceLog;
            if (traceLog == null || completed < 0 || completed >= frames.FrameCount)
            {
                return false;
            }

            if (boundaryPos <= pSync || sync[boundaryPos].Method == MethodIndex.Invalid)
            {
                return false;
            }

            int entryPos = GetV2CurrentSyncCandidateIndex(sync, pSync, boundaryPos);
            if (entryPos < pSync || sync[entryPos].CodeAddress == CodeAddressIndex.Invalid)
            {
                return false;
            }

            ModuleFileIndex module = traceLog.CodeAddresses.ModuleFileIndex(sync[entryPos].CodeAddress);
            if (module == ModuleFileIndex.Invalid)
            {
                return false;
            }

            CodeAddressIndex target = frames.CodeAddressAt(completed);
            MethodIndex targetMethod = target == CodeAddressIndex.Invalid ? MethodIndex.Invalid : context.MethodOf(target);
            ModuleFileIndex owner = context.RuntimeResolver.GetHostModuleOwner(traceLog.CodeAddresses.ModuleFileIndex(sync[boundaryPos].CodeAddress));
            if (targetMethod == MethodIndex.Invalid || owner == ModuleFileIndex.Invalid)
            {
                return false;
            }

            if (!match.Support.IsNativeRuntimeModule(traceLog.ModuleFiles[module], traceLog.ModuleFiles[owner]))
            {
                return false;
            }

            // Only the adjacent entry needs native-module evidence. Its callees remain physical work,
            // unless a body occurrence or another dispatch entry makes this target attribution ambiguous.
            for (int i = pSync; i <= entryPos; i++)
            {
                if (sync[i].Method == targetMethod ||
                    sync[i].Kind == AsyncProfilerStitchSyncFrameKind.V2ResumeStub ||
                    context.Classify(sync[i].CodeAddress).Kind != AsyncProfilerCallStackStitchBoundaryKind.None)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool HasV2DispatchTarget(in StitchContext context, AsyncProfilerCallStack segment, int wrapperIndex)
        {
            int completed = GetV2DispatchTargetFrameIndex(in context, segment, wrapperIndex);
            return completed >= 0 && segment.Frames.IsCapturedFrameAt(completed);
        }

        /// <summary>Whether the current frame's method also appears among the completed frames (recursion).</summary>
        private static bool IsCurrentMethodAlsoCompleted(AsyncProfilerCallStackFrames frames, int completed, Func<CodeAddressIndex, MethodIndex> methodOf)
        {
            MethodIndex current = methodOf(frames.CodeAddressAt(completed));
            for (int k = 0; k < completed; k++)
            {
                if (methodOf(frames.CodeAddressAt(k)) == current)
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetV2DispatchTargetFrameIndex(in StitchContext context, AsyncProfilerCallStack segment, int wrapperIndex)
        {
            long timestampQpc = context.TimestampQpc;
            bool completionObserved = context.IsCompletionObserved(segment);
            if (!completionObserved && (segment.WrapperCount == 0 || wrapperIndex < 0 || wrapperIndex >= segment.WrapperCount))
            {
                return -1;
            }

            // Do not clamp an out-of-range position into a plausible target at either end of the call stack.
            long completed = completionObserved
                ? segment.GetCompletedFrameCount(timestampQpc)
                : (long)segment.GetWrapperResetCount(timestampQpc) * segment.WrapperCount + wrapperIndex - segment.ContinuationIndexBase;
            return completed >= 0 && completed < segment.Frames.FrameCount ? (int)completed : -1;
        }

        private static bool TryFindV2DispatchTargetWithoutWrapper(in StitchContext context, AsyncProfilerCallStack segment,
            out int dispatchStart, out int dispatchEnd, out int completed)
        {
            IReadOnlyList<AsyncProfilerStitchSyncFrame> sync = context.Sync;
            long timestampQpc = context.TimestampQpc;
            Func<CodeAddressIndex, AsyncProfilerCallStackStitchBoundaryInfo> classify = context.Classify;
            Func<CodeAddressIndex, MethodIndex> methodOf = context.MethodOf;
            AsyncProfilerCallStacksIndex index = context.Index;
            dispatchStart = -1;
            dispatchEnd = -1;
            completed = segment.GetCompletedFrameCount(timestampQpc);
            bool completionObserved = context.IsCompletionObserved(segment);

            bool sampleInsideCallStack = segment.Depth == 0 && timestampQpc >= segment.StartQpc && timestampQpc < segment.EndQpc;
            if (!completionObserved || !sampleInsideCallStack || !HasResolvedAsyncFrame(segment.Frames, completed, methodOf))
            {
                return false;
            }

            if (index == null || !index.IsLatestCallStack(segment, timestampQpc))
            {
                return false;
            }

            // The history guard rules out a later, popped inner context whose dispatcher could still be above
            // this call stack. Earlier, popped dispatchers may remain among its callers and are left unassociated.
            for (int i = 0; i < sync.Count; i++)
            {
                AsyncProfilerCallStackStitchBoundaryKind kind = classify(sync[i].CodeAddress).Kind;
                if (kind == AsyncProfilerCallStackStitchBoundaryKind.V1Dispatcher || kind == AsyncProfilerCallStackStitchBoundaryKind.V2Dispatcher)
                {
                    return false;
                }

                if (dispatchEnd < 0 && kind == AsyncProfilerCallStackStitchBoundaryKind.V2DispatcherInfrastructure)
                {
                    dispatchStart = i;
                    dispatchEnd = i + 2;
                    if (!IsV2DispatchPairCandidate(sync, dispatchStart, classify))
                    {
                        return false;
                    }

                    if (context.TraceLog != null && !IsV2DispatchPair(context.TraceLog, context.RuntimeResolver, sync[i].CodeAddress, sync[i + 1].CodeAddress))
                    {
                        return false;
                    }
                }

                // A physical occurrence of a remaining method needs its own body/call-stack alignment,
                // rather than a second representation inferred solely from completion records.
                if (sync[i].Method != MethodIndex.Invalid)
                {
                    for (int k = completed; k < segment.Frames.FrameCount; k++)
                    {
                        if (sync[i].Method == methodOf(segment.Frames.CodeAddressAt(k)))
                        {
                            return false;
                        }
                    }
                }
            }

            return dispatchEnd >= 0;
        }

        private static bool HasResolvedAsyncFrame(AsyncProfilerCallStackFrames frames, int frameIndex, Func<CodeAddressIndex, MethodIndex> methodOf) =>
            frameIndex >= 0 && frameIndex < frames.FrameCount &&
            frames.CodeAddressAt(frameIndex) != CodeAddressIndex.Invalid &&
            methodOf(frames.CodeAddressAt(frameIndex)) != MethodIndex.Invalid;

        private static bool IsV2DispatchPairCandidate(IReadOnlyList<AsyncProfilerStitchSyncFrame> sync,
            int start, Func<CodeAddressIndex, AsyncProfilerCallStackStitchBoundaryInfo> classify)
        {
            // The caller has already classified the first frame as V2 dispatch infrastructure.
            int end = start + 2;
            return end < sync.Count &&
                classify(sync[start + 1].CodeAddress).Kind == AsyncProfilerCallStackStitchBoundaryKind.V2DispatcherInfrastructure &&
                classify(sync[end].CodeAddress).Kind != AsyncProfilerCallStackStitchBoundaryKind.V2DispatcherInfrastructure &&
                sync[start].Method != MethodIndex.Invalid &&
                sync[start + 1].Method != MethodIndex.Invalid &&
                sync[start].Method != sync[start + 1].Method;
        }

        private static int FindEnclosingV2CurrentSyncFrame(in StitchContext context, AsyncProfilerCallStack segment, int pSync)
        {
            IReadOnlyList<AsyncProfilerStitchSyncFrame> sync = context.Sync;
            long timestampQpc = context.TimestampQpc;
            AsyncProfilerCallStackFrames frames = segment.Frames;
            if (frames.Kind != AsyncCallstackKind.RuntimeAsync)
            {
                return -1;
            }

            int boundaryPos = FindBoundary(sync, pSync, frames.Kind, context.Classify, out AsyncProfilerCallStackStitchBoundaryInfo boundaryInfo);
            int currentSyncPos = GetV2CurrentSyncCandidateIndex(sync, pSync, boundaryPos);
            if (boundaryPos == sync.Count || currentSyncPos < pSync || sync[currentSyncPos].Method == MethodIndex.Invalid)
            {
                return -1;
            }

            int completed = context.IsCompletionObserved(segment)
                ? segment.GetCompletedFrameCount(timestampQpc)
                : segment.GetCompletedFrameCount(timestampQpc, boundaryInfo.WrapperIndex);
            if (completed < 0 || completed >= frames.FrameCount)
            {
                return -1;
            }

            return sync[currentSyncPos].Method == context.MethodOf(frames.CodeAddressAt(completed))
                ? currentSyncPos
                : -1;
        }

        /// <summary>
        /// Recovers the completed count and current sync frame of a root call stack whose dispatcher was truncated.
        /// Updates <paramref name="match"/> only on success.
        /// </summary>
        private static bool TryMatchTruncatedRoot(in StitchContext context, ref CallStackMatch match)
        {
            AsyncProfilerCallStack segment = match.Segment;
            IReadOnlyList<AsyncProfilerStitchSyncFrame> sync = context.Sync;
            int pSync = match.SyncStart;
            long timestampQpc = context.TimestampQpc;
            int completed = match.CompletedFrameCount;
            int currentSyncPos = match.CurrentSyncPosition;
            Func<CodeAddressIndex, MethodIndex> methodOf = context.MethodOf;
            AsyncProfilerCallStackFrames frames = segment.Frames;
            int recoveredCompleted = completed;
            if (match.RecoveredSyncEnd < 0 && !TryRecoverTruncatedCompletionCount(frames, sync, pSync, sync.Count, methodOf, out recoveredCompleted, out _))
            {
                return false;
            }

            if (match.CompletionObserved)
            {
                if (recoveredCompleted != segment.GetCompletedFrameCount(timestampQpc))
                {
                    return false;
                }
            }
            else if (frames.Kind == AsyncCallstackKind.RuntimeAsync)
            {
                if (recoveredCompleted < segment.GetCompletedFrameCount(timestampQpc, 0) ||
                    recoveredCompleted < segment.GetExceptionCompletedFrameCount(timestampQpc))
                {
                    return false;
                }
            }
            else if (recoveredCompleted != completed || currentSyncPos < 0)
            {
                return false;
            }

            int recoveredCurrentSyncPos = FindCurrentSyncFrame(frames, recoveredCompleted, sync, pSync, sync.Count, methodOf, frames.Kind == AsyncCallstackKind.StateMachineAsync);
            if (recoveredCurrentSyncPos < 0 || (frames.Kind == AsyncCallstackKind.StateMachineAsync && recoveredCurrentSyncPos != currentSyncPos))
            {
                return false;
            }

            // The required non-inlined dispatcher must be below this uniquely matched invocation. Its absence
            // places the captured root-side span inside the async call stack, not in its host. An unresolved frame
            // could conceal that boundary; an observed boundary could belong to an intact outer segment.
            for (int i = recoveredCurrentSyncPos + 1; i < sync.Count; i++)
            {
                if (sync[i].Method == MethodIndex.Invalid)
                {
                    return false;
                }

                AsyncProfilerCallStackStitchBoundaryKind kind = context.Classify(sync[i].CodeAddress).Kind;
                if (kind == AsyncProfilerCallStackStitchBoundaryKind.V1Dispatcher ||
                    kind == AsyncProfilerCallStackStitchBoundaryKind.V2Dispatcher ||
                    kind == AsyncProfilerCallStackStitchBoundaryKind.V2DispatcherInfrastructure)
                {
                    return false;
                }
            }

            match.CompletedFrameCount = recoveredCompleted;
            match.CurrentSyncPosition = recoveredCurrentSyncPos;
            return true;
        }

        /// <summary>
        /// Returns the first V2 dispatcher's position (its continuation wrapper), or <c>sync.Count</c> when absent.
        /// Also reports whether V2 dispatcher infrastructure was encountered before it.
        /// </summary>
        private static int FindFirstV2Wrapper(IReadOnlyList<AsyncProfilerStitchSyncFrame> sync,
            Func<CodeAddressIndex, AsyncProfilerCallStackStitchBoundaryInfo> classify,
            out bool hasDispatchContinuation)
        {
            hasDispatchContinuation = false;
            for (int i = 0; i < sync.Count; i++)
            {
                AsyncProfilerCallStackStitchBoundaryKind kind = classify(sync[i].CodeAddress).Kind;
                if (kind == AsyncProfilerCallStackStitchBoundaryKind.V2Dispatcher)
                {
                    return i;
                }

                if (kind == AsyncProfilerCallStackStitchBoundaryKind.V2DispatcherInfrastructure)
                {
                    hasDispatchContinuation = true;
                }
            }

            return sync.Count;
        }

        private static bool IsSampledV2WrapperEntry(IReadOnlyList<AsyncProfilerStitchSyncFrame> sync, int wrapperPosition) =>
            wrapperPosition == 0 || (wrapperPosition == 1 && IsV2ResumeStub(sync[0]));

        /// <summary>
        /// Finds the first sync frame at or after <paramref name="pSync"/> that is the dispatch boundary matching
        /// <paramref name="segmentKind"/>. Returns its position, or <c>sync.Count</c> if none is found.
        /// </summary>
        private static int FindBoundary(IReadOnlyList<AsyncProfilerStitchSyncFrame> sync,
            int pSync, AsyncCallstackKind segmentKind,
            Func<CodeAddressIndex, AsyncProfilerCallStackStitchBoundaryInfo> classify,
            out AsyncProfilerCallStackStitchBoundaryInfo boundaryInfo)
        {
            for (int i = pSync; i < sync.Count; i++)
            {
                AsyncProfilerCallStackStitchBoundaryInfo info = classify(sync[i].CodeAddress);
                if (BoundaryMatchesCallStackKind(info.Kind, segmentKind))
                {
                    boundaryInfo = info;
                    return i;
                }
            }

            boundaryInfo = AsyncProfilerCallStackStitchBoundaryInfo.None;
            return sync.Count;
        }

        /// <summary>Whether a dispatcher boundary matches an async segment's kind (V2 vs V1).</summary>
        private static bool BoundaryMatchesCallStackKind(AsyncProfilerCallStackStitchBoundaryKind boundaryKind,
            AsyncCallstackKind segmentKind)
        {
            switch (segmentKind)
            {
                case AsyncCallstackKind.RuntimeAsync: // V2
                    // The V2 dispatcher boundary is its continuation wrapper, which identifies the resumption position.
                    // A sampled-leaf wrapper identifies a dispatch target, not a physical body. Dispatch-continuation
                    // infrastructure alone is not a splice boundary.
                    return boundaryKind == AsyncProfilerCallStackStitchBoundaryKind.V2Dispatcher;

                case AsyncCallstackKind.StateMachineAsync: // V1
                    return boundaryKind == AsyncProfilerCallStackStitchBoundaryKind.V1Dispatcher;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Selects recorded completion counts when normal-completion events were observed for this call stack.
        /// Otherwise V1 reconciles ordered physical matches with unwind counts, while V2 uses wrapper progress.
        /// Wrapper progress already includes exceptions; V1 physical and unwind evidence must not be double-counted.
        /// </summary>
        private static int ComputeCompletedFrameCount(in StitchContext context, ref CallStackMatch match, bool allowFullyCompleted)
        {
            AsyncProfilerCallStack segment = match.Segment;
            AsyncProfilerCallStackFrames frames = match.Frames;
            AsyncCallstackKind kind = match.Kind;
            AsyncProfilerCallStackStitchBoundaryInfo boundaryInfo = match.BoundaryInfo;
            bool boundaryFound = match.BoundaryFound;
            long timestampQpc = context.TimestampQpc;
            IReadOnlyList<AsyncProfilerStitchSyncFrame> sync = context.Sync;
            int pSync = match.SyncStart;
            int boundaryPos = match.SyncEnd;
            Func<CodeAddressIndex, MethodIndex> methodOf = context.MethodOf;
            AsyncProfilerCallStackDiagnostics diagnostics = context.Diagnostics;
            int recoveredTailPos = -1;
            bool v1CompletionAmbiguous = false;
            bool v1CompletionTailVerified = false;
            bool completionObserved = context.IsCompletionObserved(segment);
            int completed;
            if (kind == AsyncCallstackKind.RuntimeAsync) // V2
            {
                if (completionObserved)
                {
                    // CompleteMethod events observed: the summed deltas (normal + any exceptional unwind) are the
                    // authoritative completed count.
                    completed = segment.GetCompletedFrameCount(timestampQpc);
                }
                else
                {
                    // Derive from the continuation-wrapper slot; the wrapper index advances on every completion,
                    // including exceptional unwinds, so no separate exception term is added.
                    int slot = boundaryInfo.Kind == AsyncProfilerCallStackStitchBoundaryKind.V2Dispatcher
                        ? boundaryInfo.WrapperIndex
                        : 0;
                    completed = segment.GetCompletedFrameCount(timestampQpc, slot);
                }
            }
            else // V1: reconcile physical inline progress with recorded completions.
            {
                if (completionObserved)
                {
                    completed = segment.GetCompletedFrameCount(timestampQpc);
                    v1CompletionTailVerified = completed > 0 && completed <= frames.FrameCount;
                    if (!boundaryFound &&
                        TryRecoverTruncatedCompletionCount(frames, sync, pSync, boundaryPos, methodOf, out int recoveredCompleted, out int tailPos) &&
                        recoveredCompleted == completed)
                    {
                        recoveredTailPos = tailPos;
                    }
                }
                else
                {
                    // No CompleteMethod events: the normal completions are the inline-resumed MoveNext frames
                    // physically on the sync stack.
                    int exceptionCompleted = segment.GetExceptionCompletedFrameCount(timestampQpc);
                    if (!boundaryFound && exceptionCompleted == 0 &&
                        TryRecoverTruncatedCompletionCount(frames, sync, pSync, boundaryPos, methodOf, out completed, out recoveredTailPos))
                    {
                        diagnostics.V1TruncatedSuffixFallbackUsed++;
                    }
                    else
                    {
                        completed = InferV1CompletedFrameCount(frames, sync, pSync, boundaryPos, exceptionCompleted,
                            boundaryFound, allowFullyCompleted, methodOf, out v1CompletionAmbiguous);
                    }

                    diagnostics.V1InlineFallbackUsed++;
                    v1CompletionTailVerified =
                        !v1CompletionAmbiguous && exceptionCompleted > 0 &&
                        completed == exceptionCompleted && completed <= frames.FrameCount;
                }

                if (v1CompletionAmbiguous)
                {
                    context.State.UnresolvedV1CompletionCount++;
                    diagnostics.V1CompletionAlignmentAmbiguous = context.State.UnresolvedV1CompletionCount;
                    diagnostics.Note("V1 completion records overlap physical matches ambiguously; retained the physical slice.");
                }
            }

            int maxIndex = allowFullyCompleted || kind == AsyncCallstackKind.StateMachineAsync
                ? frames.FrameCount
                : frames.FrameCount - 1;

            if (completed < 0)
            {
                completed = 0;
            }
            else if (completed > maxIndex)
            {
                completed = maxIndex;
            }

            match.RecoveredSyncEnd = recoveredTailPos;
            match.V1CompletionAmbiguous = v1CompletionAmbiguous;
            match.V1CompletionTailVerified = v1CompletionTailVerified;
            match.CompletionObserved = completionObserved;
            return completed;
        }

        /// <summary>Reused by <see cref="TryRecoverTruncatedCompletionCount"/>; only the leading entries it writes are read.</summary>
        [ThreadStatic]
        private static MethodIndex[] t_truncatedRecoveryMethods;

        /// <summary>
        /// Recovers a current-frame position when the root side of the synchronous sample was truncated before
        /// the dispatcher boundary. Only resolved methods present in the indexed async segment participate.
        /// The retained physical methods must form one uniquely positioned contiguous sequence of at least two
        /// frames in the async segment; recursive/repeated or otherwise ambiguous layouts are rejected.
        /// </summary>
        private static bool TryRecoverTruncatedCompletionCount(AsyncProfilerCallStackFrames frames,
            IReadOnlyList<AsyncProfilerStitchSyncFrame> sync, int pSync, int boundaryPos,
            Func<CodeAddressIndex, MethodIndex> methodOf,
            out int completed, out int rootTailPos)
        {
            completed = 0;
            rootTailPos = -1;
            int frameCount = frames.FrameCount;
            if (frameCount < 2)
            {
                return false;
            }

            // One buffer: segment methods in [0, frameCount), retained physical methods after them.
            MethodIndex[] methods = t_truncatedRecoveryMethods;
            if (methods == null || methods.Length < frameCount * 2)
            {
                methods = new MethodIndex[frameCount * 2];
                t_truncatedRecoveryMethods = methods;
            }

            for (int i = 0; i < frameCount; i++)
            {
                MethodIndex method = methodOf(frames.CodeAddressAt(i));
                if (method == MethodIndex.Invalid)
                {
                    return false;
                }

                methods[i] = method;
            }

            int physicalCount = 0;
            int rootmostPhysicalMethodPos = -1;
            for (int i = boundaryPos - 1; i >= pSync; i--)
            {
                MethodIndex method = sync[i].Method;
                if (!ContainsMethod(methods, frameCount, method))
                {
                    continue;
                }

                if (physicalCount == frameCount)
                {
                    return false;
                }

                if (rootmostPhysicalMethodPos < 0)
                {
                    rootmostPhysicalMethodPos = i;
                }

                methods[frameCount + physicalCount++] = method;
            }

            if (physicalCount < 2)
            {
                return false;
            }

            int matchStart = -1;
            int maxStart = frameCount - physicalCount;
            for (int start = 0; start <= maxStart; start++)
            {
                int physicalIndex = 0;
                while (physicalIndex < physicalCount && methods[start + physicalIndex] == methods[frameCount + physicalIndex])
                {
                    physicalIndex++;
                }

                if (physicalIndex != physicalCount)
                {
                    continue;
                }

                if (matchStart >= 0)
                {
                    return false;
                }

                matchStart = start;
            }

            if (matchStart < 0)
            {
                return false;
            }

            completed = matchStart + physicalCount - 1;
            rootTailPos = rootmostPhysicalMethodPos + 1;
            if (frames.Kind == AsyncCallstackKind.StateMachineAsync && completed + 1 < frameCount)
            {
                int carrier = FindV1BoxCarrier(frames, sync, pSync, boundaryPos, methodOf);
                if (carrier >= 0 && sync[carrier].BoxedStateMachineMethod == methods[completed + 1])
                {
                    // The unique physical body sequence independently proves the truncated call stack.
                    // Its next inline-resumed method can be represented by one leaf-side execution carrier.
                    completed++;
                }
            }

            return true;
        }

        private static bool ContainsMethod(MethodIndex[] methods, int count, MethodIndex method)
        {
            for (int i = 0; i < count; i++)
            {
                if (methods[i] == method)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Matches V1 methods from the dispatcher toward the physical leaf, following captured frame order.
        /// The final match is normally current; earlier matches have completed. Recorded exception counts
        /// bound how many missing async frames may be skipped.
        /// Only a uniquely resolved CoreLib box carrier before any physical async body can identify the final
        /// resumption target inside a verified dispatcher boundary. Other box frames are not counted.
        /// Unresolved or ambiguous matches do not justify additional completion guesses.
        /// </summary>
        private static int InferV1CompletedFrameCount(AsyncProfilerCallStackFrames frames,
            IReadOnlyList<AsyncProfilerStitchSyncFrame> sync, int pSync, int boundaryPos,
            int exceptionCompleted, bool boundaryFound, bool allowFullyCompleted,
            Func<CodeAddressIndex, MethodIndex> methodOf,
            out bool ambiguous)
        {
            ambiguous = false;
            int frameCount = frames.FrameCount;
            int nextSegmentFrame = 0;
            int matched = 0;
            int exceptionsSkipped = 0;
            int leafmostMatchedSyncPos = -1;
            int resolvedCandidate = -1;
            MethodIndex expected = MethodIndex.Invalid;
            int carrier = boundaryFound
                ? FindV1BoxCarrier(frames, sync, pSync, boundaryPos, methodOf)
                : -1;

            for (int i = boundaryPos - 1; i >= pSync && nextSegmentFrame < frameCount; i--)
            {
                int candidate = nextSegmentFrame;
                int candidateExceptions = 0;
                int availableExceptions = exceptionCompleted - exceptionsSkipped;
                while (candidate < frameCount && candidateExceptions <= availableExceptions)
                {
                    if (candidate != resolvedCandidate)
                    {
                        expected = methodOf(frames.CodeAddressAt(candidate));
                        resolvedCandidate = candidate;
                    }

                    if (expected == MethodIndex.Invalid)
                    {
                        if (candidateExceptions < availableExceptions)
                        {
                            candidate++;
                            candidateExceptions++;
                            continue;
                        }

                        // The next verifiable physical frame cannot identify this async frame. Every previously
                        // matched frame completed; preserve the physical slice because the current frame is unknown.
                        ambiguous = exceptionCompleted > nextSegmentFrame;
                        return Math.Max(nextSegmentFrame, exceptionCompleted);
                    }

                    if (MatchesV1Method(sync[i], expected, i == carrier))
                    {
                        exceptionsSkipped += candidateExceptions;
                        nextSegmentFrame = candidate + 1;
                        matched++;
                        leafmostMatchedSyncPos = i;
                        break;
                    }

                    candidate++;
                    candidateExceptions++;
                }
            }

            // Normally the final matched physical state-machine frame is current. For a non-leaf segment, however,
            // its final MoveNext may have completed and synchronously resumed the inner segment while its dispatcher
            // invocation remains on the physical stack. A CoreLib method-builder completion frame just before that
            // final MoveNext proves this shape without requiring CompleteMethod events.
            bool finalMoveNextCompleted =
                exceptionCompleted == exceptionsSkipped && allowFullyCompleted && nextSegmentFrame == frameCount &&
                leafmostMatchedSyncPos > pSync && HasV1CompletionEvidence(sync, pSync, leafmostMatchedSyncPos);
            if (finalMoveNextCompleted)
            {
                return nextSegmentFrame;
            }

            if (matched == 0)
            {
                return exceptionCompleted;
            }

            if (exceptionCompleted == exceptionsSkipped)
            {
                return nextSegmentFrame - 1;
            }

            // Retain the existing physical-current inference unless records cover the complete matched prefix.
            // Exceptions already accounted for by that physical position must not advance it a second time.
            if (exceptionCompleted < nextSegmentFrame)
            {
                return nextSegmentFrame - 1;
            }

            // Counts covering the entire prefix establish completion of the final physical match too.
            if (exceptionCompleted == nextSegmentFrame)
            {
                return exceptionCompleted;
            }

            ambiguous = true;
            return exceptionCompleted;
        }

        private static bool HasV1CompletionEvidence(IReadOnlyList<AsyncProfilerStitchSyncFrame> frames, int start, int end)
        {
            for (int i = end - 1; i >= start; i--)
            {
                if (frames[i].Kind == AsyncProfilerStitchSyncFrameKind.V1MethodCompletion)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryFindV1CompletingFrame(AsyncProfilerCallStackFrames frames, int completed,
            IReadOnlyList<AsyncProfilerStitchSyncFrame> sync, int pSync, int boundaryPos,
            Func<CodeAddressIndex, MethodIndex> methodOf,
            out int completingSyncPos)
        {
            completingSyncPos = -1;
            if (completed <= 0 || completed > frames.FrameCount)
            {
                return false;
            }

            MethodIndex anchor = methodOf(frames.CodeAddressAt(completed - 1));
            if (anchor == MethodIndex.Invalid)
            {
                return false;
            }

            // Counts do not identify recursive occurrences. Require unique identities for every matched method,
            // and ordered completed matches, so no physical copy is assigned an invented completion status.
            int previousFrame = -1;
            int anchorSyncPos = -1;
            for (int i = boundaryPos - 1; i >= pSync; i--)
            {
                MethodIndex method = sync[i].Method;
                if (method == MethodIndex.Invalid)
                {
                    continue;
                }

                int match = -1;
                for (int k = 0; k < frames.FrameCount; k++)
                {
                    if (methodOf(frames.CodeAddressAt(k)) != method)
                    {
                        continue;
                    }

                    if (match >= 0)
                    {
                        return false;
                    }

                    match = k;
                }

                if (match < 0)
                {
                    continue;
                }

                if (match <= previousFrame || match >= completed || sync[i].CodeAddress == CodeAddressIndex.Invalid)
                {
                    return false;
                }

                previousFrame = match;
                if (method == anchor)
                {
                    anchorSyncPos = i;
                }
            }

            completingSyncPos = anchorSyncPos;
            return completingSyncPos >= 0;
        }

        private static int FindCurrentSyncFrame(AsyncProfilerCallStackFrames frames, int completed,
            IReadOnlyList<AsyncProfilerStitchSyncFrame> sync, int pSync, int boundaryPos,
            Func<CodeAddressIndex, MethodIndex> methodOf, bool allowBoxCarrier)
        {
            if (completed < 0 || completed >= frames.FrameCount)
            {
                return -1;
            }

            MethodIndex current = methodOf(frames.CodeAddressAt(completed));
            if (current == MethodIndex.Invalid)
            {
                // Without a current-method identity we cannot distinguish synchronous leaf calls from the V1
                // transition chain. Preserve the complete physical slice rather than risk deleting user frames.
                return -1;
            }

            // Pick the leaf-most matching physical frame. Completed copies of the same state machine, if any,
            // occur among its callers during an inline-resume cascade.
            int carrier = allowBoxCarrier
                ? FindV1BoxCarrier(frames, sync, pSync, boundaryPos, methodOf)
                : -1;

            for (int i = pSync; i < boundaryPos; i++)
            {
                if (MatchesV1Method(sync[i], current, i == carrier))
                {
                    return i;
                }
            }

            return -1;
        }

        private static int FindV1BoxCarrier(AsyncProfilerCallStackFrames frames,
            IReadOnlyList<AsyncProfilerStitchSyncFrame> sync, int pSync, int boundaryPos,
            Func<CodeAddressIndex, MethodIndex> methodOf)
        {
            int carrier = -1;
            for (int i = pSync; i < boundaryPos; i++)
            {
                AsyncProfilerStitchSyncFrame frame = sync[i];
                if (frame.BoxedStateMachineMethod != MethodIndex.Invalid && frame.Kind != AsyncProfilerStitchSyncFrameKind.V1BoxCallback)
                {
                    if (frame.CodeAddress == CodeAddressIndex.Invalid || frame.Method == MethodIndex.Invalid)
                    {
                        return -1;
                    }

                    carrier = i;
                    break;
                }
            }

            for (int i = pSync; i < carrier; i++)
            {
                AsyncProfilerStitchSyncFrame frame = sync[i];
                if (frame.Method != MethodIndex.Invalid)
                {
                    for (int k = 0; k < frames.FrameCount; k++)
                    {
                        if (frame.Method == methodOf(frames.CodeAddressAt(k)))
                        {
                            // A visible application body takes precedence over its caller's box wrappers.
                            return -1;
                        }
                    }
                }
            }

            return carrier;
        }

        private static bool MatchesV1Method(AsyncProfilerStitchSyncFrame frame, MethodIndex expected, bool allowBoxCarrier) =>
            frame.Method == expected ||
            (allowBoxCarrier && IsResolvedSyncFrame(frame) && frame.BoxedStateMachineMethod == expected);

        private static bool IsResolvedSyncFrame(AsyncProfilerStitchSyncFrame frame) =>
            frame.CodeAddress != CodeAddressIndex.Invalid && frame.Method != MethodIndex.Invalid;

        private static int FindCurrentV2SyncFrame(AsyncProfilerCallStackFrames frames, int completed,
            IReadOnlyList<AsyncProfilerStitchSyncFrame> sync, int pSync, int boundaryPos,
            Func<CodeAddressIndex, MethodIndex> methodOf, bool boundaryFound,
            AsyncProfilerCallStackDiagnostics diagnostics, AsyncProfilerCallStackStitchingState stitchingState)
        {
            int currentSyncPos = GetV2CurrentSyncCandidateIndex(sync, pSync, boundaryPos);
            if (currentSyncPos < pSync || completed < 0 || completed >= frames.FrameCount)
            {
                return -1;
            }

            MethodIndex expected = methodOf(frames.CodeAddressAt(completed));
            MethodIndex actual = sync[currentSyncPos].Method;
            if (boundaryFound &&
                expected != MethodIndex.Invalid && actual != MethodIndex.Invalid && expected != actual)
            {
                stitchingState.HasConsistentPhysicalAlignment = false;
                diagnostics.AdjacencyMismatches++;
                if (diagnostics.CanRetainMessage)
                {
                    diagnostics.Note(
                        $"V2 current async method (segment frame {completed}) did not match the sync frame " +
                        $"leaf-ward of the boundary and any recognized resume stub (frame {currentSyncPos}).");
                }
                else
                {
                    diagnostics.MessagesDropped++;
                }
            }

            return expected != MethodIndex.Invalid && expected == actual ? currentSyncPos : -1;
        }

        private static int GetV2CurrentSyncCandidateIndex(IReadOnlyList<AsyncProfilerStitchSyncFrame> sync, int pSync, int boundaryPos)
        {
            int position = boundaryPos - 1;
            if (boundaryPos < sync.Count && position >= pSync && IsV2ResumeStub(sync[position]))
            {
                position--;
            }

            return position;
        }

        private static bool IsV2ResumeStub(AsyncProfilerStitchSyncFrame frame) =>
            frame.Kind == AsyncProfilerStitchSyncFrameKind.V2ResumeStub &&
            frame.CodeAddress != CodeAddressIndex.Invalid &&
            frame.Method != MethodIndex.Invalid;

        #endregion
    }
}
