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
    /// Groups verified async runtime helpers as <c>[Async dispatcher frames]</c> or removes them.
    /// Leaves unrelated frames and helper sequences that cannot be verified unchanged.
    /// </summary>
    /// <remarks>
    /// Optional because helper layouts vary by runtime. Each marker keeps a physical frame's
    /// identity; it does not mean an application method is executing.
    /// </remarks>
    internal sealed class AsyncProfilerInfrastructureCleanupTransformPass
    {
        public void Apply(AsyncProfilerCallStackTransformState context)
        {
            // Handle complete dispatch and resumption sequences before individual helpers.
            // Later edits stay within [cleanupStart, cleanupEnd), using the current list indices.
            CollapseProfilerDispatchSpan(context);
            int cleanupEnd = CollapseRuntimeResumptionBridge(context);
            int cleanupStart = CollapseProfilerOwnedPrefix(context, ref cleanupEnd);
            cleanupStart = CollapseBoundedAwaitRegistration(context, cleanupStart, ref cleanupEnd);

            List<(int Start, int End)> rejectedSpans = CollapseCompletionSequences(context, cleanupStart, ref cleanupEnd);
            GroupInfrastructureFrames(context, cleanupStart, cleanupEnd, rejectedSpans);
        }

        #region private

        /// <summary>
        /// Scans leaf-to-root, collapsing verified task-completion chains and pairs. Rejected sequences are kept
        /// intact and returned in scan order, or null when there are none. A later edit never extends back across
        /// a protected range, so its saved indices stay valid.
        /// </summary>
        private static List<(int Start, int End)> CollapseCompletionSequences(AsyncProfilerCallStackTransformState context,
            int cleanupStart, ref int cleanupEnd)
        {
            List<AsyncProfilerStitchedFrame> frames = context.Frames;
            List<(int Start, int End)> rejectedSpans = null;
            for (int i = cleanupStart; i < cleanupEnd;)
            {
                if (frames[i].Origin != AsyncProfilerStitchedFrameOrigin.Sync)
                {
                    i++;
                    continue;
                }

                // Include a public SetResult call only when the full completion chain is verified.
                AsyncProfilerRuntimeSequenceMatch chain =
                    context.MatchRuntimeSequence(AsyncProfilerRuntimeSequenceKind.TaskCompletionChain, i, cleanupEnd);
                if (chain.IsMatch && HasSameMachineryOwner(context, i, chain.Length))
                {
                    cleanupEnd -= CollapseMachinerySpan(frames, i, chain.Length);
                    context.Diagnostics.AsyncInfrastructureFramesCollapsed += chain.Length;
                    i++;
                    continue;
                }

                int protectedLength = GetProtectedSequenceLength(context, chain, i, cleanupEnd);
                if (protectedLength != 0)
                {
                    AddRejectedSpan(ref rejectedSpans, i, i + protectedLength);
                    i += protectedLength;
                    continue;
                }

                AsyncProfilerRuntimeSequenceMatch pair = context.MatchRuntimeSequence(AsyncProfilerRuntimeSequenceKind.TaskCompletion, i, cleanupEnd);
                AsyncProfilerStitchedFrame entry = frames[i];
                bool hasVerifiedCompletionPair =
                    pair.IsMatch && entry.Context != null &&
                    entry.ContextKind == AsyncProfilerStitchedFrameContextKind.Active &&
                    HasSameMachineryOwner(context, i, pair.Length) &&
                    context.HasVerifiedV1CompletionAnchor(entry.Context, entry.CodeAddress);
                if (!hasVerifiedCompletionPair)
                {
                    i++;
                    continue;
                }

                // Extend toward the leaf only as far as the last protected range allows.
                int minimumStart = GetMinimumStart(cleanupStart, rejectedSpans, (rejectedSpans?.Count ?? 0) - 1);
                bool hasMatchingContinuationBridge =
                    i > minimumStart && frames[i - 1].Method != MethodIndex.Invalid &&
                    IsSyncFrameKind(frames[i - 1], AsyncProfilerStitchSyncFrameKind.ContinuationBridge) &&
                    HasSameMachineryOwner(context, i - 1, pair.Length + 1);
                int spanStart = hasMatchingContinuationBridge ? i - 1 : i;
                spanStart = FindProfilerDispatchSpanStart(context, spanStart, frames[spanStart], out int rejectedStart, minimumStart);
                if (rejectedStart >= 0)
                {
                    AddRejectedSpan(ref rejectedSpans, rejectedStart, i + pair.Length);
                    i += pair.Length;
                    continue;
                }

                int matchedFrameCount = i + pair.Length - spanStart;
                cleanupEnd -= CollapseMachinerySpan(frames, spanStart, matchedFrameCount);
                context.Diagnostics.AsyncInfrastructureFramesCollapsed += matchedFrameCount;
                context.Diagnostics.ProfilerEnvelopeFramesCollapsed += i - spanStart;
                i = spanStart + 1;
            }

            return rejectedSpans;
        }

        /// <summary>
        /// Returns how many frames at <paramref name="start"/> belong to a sequence that must stay intact, or 0.
        /// A completion chain with mismatched ownership is protected even though its frame layout matched.
        /// </summary>
        private static int GetProtectedSequenceLength(AsyncProfilerCallStackTransformState context,
            AsyncProfilerRuntimeSequenceMatch chain, int start, int end)
        {
            AsyncProfilerRuntimeSequenceMatch handoff = context.MatchRuntimeSequence(AsyncProfilerRuntimeSequenceKind.V2CompletionHandoff, start, end);
            AsyncProfilerRuntimeSequenceMatch startup = context.MatchRuntimeSequence(AsyncProfilerRuntimeSequenceKind.V1Startup, start, end);
            AsyncProfilerRuntimeSequenceMatch registration = context.MatchRuntimeSequence(AsyncProfilerRuntimeSequenceKind.V1AwaitRegistration, start, end);

            int length = chain.IsRejected || chain.IsMatch ? chain.Length : 0;
            length = Math.Max(length, handoff.IsRejected ? handoff.Length : 0);
            length = Math.Max(length, startup.IsRejected ? startup.Length : 0);
            return Math.Max(length, registration.IsRejected ? registration.Length : 0);
        }

        private static void AddRejectedSpan(ref List<(int Start, int End)> rejectedSpans, int start, int end)
        {
            if (rejectedSpans == null)
            {
                rejectedSpans = new List<(int Start, int End)>();
            }

            rejectedSpans.Add((start, end));
        }

        /// <summary>The leaf-most index an edit may reach without crossing the rejected span at <paramref name="rejectedIndex"/>.</summary>
        private static int GetMinimumStart(int cleanupStart, List<(int Start, int End)> rejectedSpans, int rejectedIndex) =>
            rejectedIndex >= 0 ? Math.Max(cleanupStart, rejectedSpans[rejectedIndex].End) : cleanupStart;

        /// <summary>
        /// Scans root-to-leaf, grouping optional infrastructure and verified dispatch spans into dispatcher markers.
        /// Removals leave unvisited indices unchanged. Merging stops at protected ranges (mergeEnd), and
        /// faultSpanStart tracks a fault-handling group so its entry can join it.
        /// </summary>
        private static void GroupInfrastructureFrames(AsyncProfilerCallStackTransformState context,
            int cleanupStart, int cleanupEnd, List<(int Start, int End)> rejectedSpans)
        {
            List<AsyncProfilerStitchedFrame> frames = context.Frames;
            int rejectedIndex = (rejectedSpans?.Count ?? 0) - 1;
            int mergeEnd = cleanupEnd;
            int faultSpanStart = -1;
            for (int i = cleanupEnd - 1; i >= cleanupStart; i--)
            {
                bool isInRejectedSpan = rejectedIndex >= 0 && i < rejectedSpans[rejectedIndex].End;
                if (isInRejectedSpan)
                {
                    mergeEnd = rejectedSpans[rejectedIndex].Start;
                    i = mergeEnd;
                    rejectedIndex--;
                    continue;
                }

                AsyncProfilerStitchedFrame frame = frames[i];
                if (frame.Origin != AsyncProfilerStitchedFrameOrigin.Sync)
                {
                    continue;
                }

                bool isDispatcherMarker = frame.Presentation == AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames;
                AsyncProfilerCallStackStitchBoundaryKind boundaryKind = context.ClassifyBoundary(frame.CodeAddress).Kind;
                int minimumStart = GetMinimumStart(cleanupStart, rejectedSpans, rejectedIndex);
                int boundedStart = FindBoundedAsyncInfrastructureStart(context, i, out int rejectedStart, minimumStart);
                if (rejectedStart >= 0)
                {
                    mergeEnd = rejectedStart;
                    i = rejectedStart;
                    continue;
                }

                bool canGroupFrame =
                    isDispatcherMarker || boundedStart < i ||
                    context.GetRuntimeSupport(frame.CodeAddress).IsOptionalInfrastructureFrame(frame, boundaryKind);
                if (!canGroupFrame)
                {
                    continue;
                }

                // Merge the following marker only when it belongs to the same call stack and runtime.
                bool canMergeWithFollowingMarker =
                    i + 1 < mergeEnd &&
                    frames[i + 1].Presentation == AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames &&
                    HasSameMachineryOwner(context, i, 2);
                int trailingFrameCount = canMergeWithFollowingMarker ? 2 : 1;
                bool isFaultEntry =
                    frame.SyncFrameKind == AsyncProfilerStitchSyncFrameKind.V1MethodHelper &&
                    frame.Method != MethodIndex.Invalid && i > cleanupStart &&
                    frames[i - 1].SyncFrameKind == AsyncProfilerStitchSyncFrameKind.FaultHandling;
                bool joinsFaultSpan = trailingFrameCount == 2 && i + 1 == faultSpanStart;
                int spanStart = boundedStart;

                // Extend into profiler helpers only with verified dispatch evidence from an active call stack.
                bool canVerifyCallStack =
                    context.TraceLog != null && frame.Context != null &&
                    frame.ContextKind == AsyncProfilerStitchedFrameContextKind.Active;
                bool hasDispatchEvidence =
                    canVerifyCallStack &&
                    (isDispatcherMarker || isFaultEntry || joinsFaultSpan ||
                     context.ClassifyBoundary(frame.CodeAddress).IsDispatchMarker);
                bool canExtendDispatchSpan =
                    hasDispatchEvidence &&
                    context.HasVerifiedDispatchEntry(frame.Context, frame.CodeAddress, requireAsyncAnchor: false);
                if (canExtendDispatchSpan)
                {
                    spanStart = FindProfilerDispatchSpanStart(context, i, frame, out rejectedStart, minimumStart);
                    if (rejectedStart >= 0)
                    {
                        mergeEnd = rejectedStart;
                        i = rejectedStart;
                        continue;
                    }
                }

                int precedingHelperCount = i - spanStart;
                bool isFaultSpan =
                    isFaultEntry || joinsFaultSpan ||
                    frames[spanStart].SyncFrameKind == AsyncProfilerStitchSyncFrameKind.FaultHandling;
                faultSpanStart = isFaultSpan ? spanStart : -1;
                int removed = CollapseMachinerySpan(frames, spanStart, precedingHelperCount + trailingFrameCount);
                cleanupEnd -= removed;
                mergeEnd -= removed;
                context.Diagnostics.AsyncInfrastructureFramesCollapsed += precedingHelperCount + (isDispatcherMarker ? 0 : 1);
                context.Diagnostics.ProfilerEnvelopeFramesCollapsed += precedingHelperCount;
                i = spanStart;
            }
        }

        /// <summary>
        /// Groups a verified V1 await-registration sequence while keeping unrelated callees protected.
        /// </summary>
        /// <returns>Where later cleanup may start, with cleanupEnd adjusted for removed frames.</returns>
        private static int CollapseBoundedAwaitRegistration(AsyncProfilerCallStackTransformState context,
            int protectedEnd, ref int cleanupEnd)
        {
            if (context.TraceLog == null)
            {
                return protectedEnd;
            }

            List<AsyncProfilerStitchedFrame> frames = context.Frames;
            for (int start = 0; start < protectedEnd; start++)
            {
                AsyncProfilerStitchedFrame entry = frames[start];
                bool hasActiveSetupFrame =
                    IsSyncFrameKind(entry, AsyncProfilerStitchSyncFrameKind.V1DispatchSetup) &&
                    entry.Context != null && entry.ContextKind == AsyncProfilerStitchedFrameContextKind.Active;
                if (!hasActiveSetupFrame)
                {
                    continue;
                }

                AsyncProfilerRuntimeSequenceMatch registration = context.MatchRuntimeSequence(AsyncProfilerRuntimeSequenceKind.V1AwaitRegistration, start + 1, protectedEnd);
                if (!registration.IsMatch)
                {
                    continue;
                }

                int end = start + 1 + registration.Length;
                if (end >= protectedEnd)
                {
                    continue;
                }

                AsyncProfilerStitchedFrame caller = frames[end];
                bool belongsToCallStack =
                    caller.Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
                    caller.ContextKind == AsyncProfilerStitchedFrameContextKind.Active &&
                    ReferenceEquals(entry.Context, caller.Context);
                bool hasResolvedCaller =
                    belongsToCallStack &&
                    caller.CodeAddress != CodeAddressIndex.Invalid && caller.Method != MethodIndex.Invalid;
                bool hasApplicationCaller =
                    hasResolvedCaller &&
                    context.GetHostModuleOwner(caller.CodeAddress) == ModuleFileIndex.Invalid &&
                    !context.ClassifyBoundary(caller.CodeAddress).IsDispatchMarker;
                if (!hasApplicationCaller)
                {
                    continue;
                }

                AsyncProfilerRuntimeSequenceMatch match =
                    context.MatchRuntimeSequence(AsyncProfilerRuntimeSequenceKind.ProfilerEntry, start, end);
                bool hasCompleteOwnedEntry =
                    match.IsMatch && match.Length == end - start &&
                    HasSameMachineryOwner(context, start, match.Length);
                if (!hasCompleteOwnedEntry)
                {
                    continue;
                }

                AsyncProfilerRuntimeSupport support = context.GetRuntimeSupport(entry.CodeAddress);
                // Keep boundaries and already-grouped frames out of this optional helper sequence.
                bool hasOnlyOptionalHelpers = true;
                for (int i = start; i < end; i++)
                {
                    AsyncProfilerStitchedFrame helper = frames[i];
                    AsyncProfilerCallStackStitchBoundaryKind boundary = context.ClassifyBoundary(helper.CodeAddress).Kind;
                    bool isOptionalHelper =
                        helper.Presentation == AsyncProfilerStitchedFramePresentation.Native &&
                        helper.ContextKind == AsyncProfilerStitchedFrameContextKind.Active &&
                        boundary == AsyncProfilerCallStackStitchBoundaryKind.None &&
                        support.IsOptionalInfrastructureFrame(helper, boundary);
                    if (!isOptionalHelper)
                    {
                        hasOnlyOptionalHelpers = false;
                        break;
                    }
                }

                if (!hasOnlyOptionalHelpers)
                {
                    continue;
                }

                int spanStart = FindProfilerDispatchSpanStart(context, start, entry, out int rejectedStart);
                if (rejectedStart >= 0)
                {
                    return protectedEnd;
                }

                int matchedFrameCount = end - spanStart;
                cleanupEnd -= CollapseMachinerySpan(frames, spanStart, matchedFrameCount);
                context.Diagnostics.AsyncInfrastructureFramesCollapsed += matchedFrameCount;
                context.Diagnostics.ProfilerEnvelopeFramesCollapsed += start - spanStart;

                // Keep unrelated callees before the marker protected; resume cleanup at its caller.
                return spanStart + 1;
            }

            return protectedEnd;
        }

        /// <summary>Groups adjacent profiler helpers with the first verified V2 dispatch entry.</summary>
        private static void CollapseProfilerDispatchSpan(AsyncProfilerCallStackTransformState context)
        {
            if (context.TraceLog == null)
            {
                return;
            }

            List<AsyncProfilerStitchedFrame> frames = context.Frames;
            // Stop at the first boundary or logical frame; later dispatchers belong to other spans.
            for (int i = 0; i < frames.Count; i++)
            {
                AsyncProfilerStitchedFrame dispatcher = frames[i];
                bool isPhysicalNonParentFrame =
                    dispatcher.Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
                    dispatcher.ContextKind != AsyncProfilerStitchedFrameContextKind.ParentContext;
                if (!isPhysicalNonParentFrame)
                {
                    return;
                }

                AsyncProfilerCallStackStitchBoundaryKind boundary = context.ClassifyBoundary(dispatcher.CodeAddress).Kind;
                if (boundary == AsyncProfilerCallStackStitchBoundaryKind.None)
                {
                    if (dispatcher.Presentation != AsyncProfilerStitchedFramePresentation.Native)
                    {
                        return;
                    }

                    continue;
                }

                if (boundary != AsyncProfilerCallStackStitchBoundaryKind.V2DispatcherInfrastructure)
                {
                    return;
                }

                bool isDispatcherMarker = dispatcher.Presentation == AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames;
                if (isDispatcherMarker)
                {
                    bool hasVerifiedMarker =
                        dispatcher.Context != null &&
                        context.HasVerifiedV2DispatchPair(dispatcher.Context, dispatcher.CodeAddress);
                    if (!hasVerifiedMarker)
                    {
                        return;
                    }
                }
                else
                {
                    // A raw entry must contain exactly the expected dispatch pair.
                    bool hasFollowingPhysicalFrame =
                        i + 1 < frames.Count &&
                        frames[i + 1].Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
                        frames[i + 1].Presentation == AsyncProfilerStitchedFramePresentation.Native &&
                        ReferenceEquals(dispatcher.Context, frames[i + 1].Context);
                    bool hasVerifiedDispatchPair =
                        hasFollowingPhysicalFrame &&
                        context.IsV2DispatchPair(dispatcher.CodeAddress, frames[i + 1].CodeAddress);
                    bool hasExtraDispatchFrame =
                        hasVerifiedDispatchPair && i + 2 < frames.Count &&
                        context.ClassifyBoundary(frames[i + 2].CodeAddress).Kind == AsyncProfilerCallStackStitchBoundaryKind.V2DispatcherInfrastructure;
                    if (!hasVerifiedDispatchPair || hasExtraDispatchFrame)
                    {
                        return;
                    }
                }

                AsyncProfilerRuntimeSupport support = context.GetRuntimeSupport(dispatcher.CodeAddress);
                bool canGroupDispatcher = support.IsOptionalInfrastructureFrame(dispatcher, boundary);
                bool hasOnlyOptionalDispatchFrames =
                    canGroupDispatcher &&
                    (isDispatcherMarker || support.IsOptionalInfrastructureFrame(frames[i + 1], boundary));
                if (!hasOnlyOptionalDispatchFrames)
                {
                    return;
                }

                int start = FindProfilerDispatchSpanStart(context, i, dispatcher, out int rejectedStart);
                if (rejectedStart >= 0)
                {
                    return;
                }
                if (start < i)
                {
                    // Keep the first physical frame as the marker, without claiming application execution.
                    CollapseMachinerySpan(frames, start, i - start + (isDispatcherMarker ? 1 : 2));
                    context.Diagnostics.AsyncInfrastructureFramesCollapsed += i - start + (isDispatcherMarker ? 0 : 2);
                }

                return;
            }
        }

        /// <summary>
        /// Extends a dispatcher span toward the leaf through adjacent, owned runtime helpers.
        /// Reports rejected sequences separately so the caller can leave them untouched.
        /// </summary>
        private static int FindProfilerDispatchSpanStart(AsyncProfilerCallStackTransformState context,
            int start, AsyncProfilerStitchedFrame entry,
            out int rejectedStart, int minimumStart = 0)
        {
            rejectedStart = -1;
            AsyncProfilerRuntimeSupport support = context.GetRuntimeSupport(entry.CodeAddress);
            ModuleFileIndex owner = context.GetHostModuleOwner(entry.CodeAddress);
            while (start > minimumStart)
            {
                // Try a complete sequence before considering individual profiler helpers.
                int infrastructureStart = FindBoundedAsyncInfrastructureStart(context, start, out rejectedStart, minimumStart);
                if (rejectedStart >= 0)
                {
                    return start;
                }

                if (infrastructureStart < start)
                {
                    start = infrastructureStart;
                    continue;
                }

                AsyncProfilerStitchedFrame helper = context.Frames[start - 1];
                bool isOwnedPhysicalHelper =
                    helper.Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
                    helper.Presentation == AsyncProfilerStitchedFramePresentation.Native &&
                    helper.Method != MethodIndex.Invalid &&
                    ReferenceEquals(helper.Context, entry.Context);
                bool hasMatchingRuntime =
                    isOwnedPhysicalHelper &&
                    owner != ModuleFileIndex.Invalid && owner == context.GetHostModuleOwner(helper.CodeAddress) &&
                    ReferenceEquals(support, context.GetRuntimeSupport(helper.CodeAddress));
                bool isProfilerHelper =
                    hasMatchingRuntime &&
                    !context.ClassifyBoundary(helper.CodeAddress).IsDispatchMarker &&
                    support.IsProfilerDispatchInfrastructure(helper.SyncClassification);
                if (!isProfilerHelper)
                {
                    break;
                }

                start--;
            }

            return start;
        }

        /// <summary>
        /// Finds a complete helper sequence ending at the entry frame, without crossing minimumStart.
        /// Returns the entry index when no sequence matches and reports rejected candidates separately.
        /// </summary>
        private static int FindBoundedAsyncInfrastructureStart(AsyncProfilerCallStackTransformState context,
            int entryIndex, out int rejectedStart, int minimumStart)
        {
            rejectedStart = -1;
            AsyncProfilerStitchedFrame entry = context.Frames[entryIndex];
            bool hasPhysicalEntry =
                context.TraceLog != null && entry.Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
                entry.CodeAddress != CodeAddressIndex.Invalid &&
                entry.Presentation == AsyncProfilerStitchedFramePresentation.Native;
            bool hasResolvedActiveEntry =
                hasPhysicalEntry && entry.Context != null &&
                entry.ContextKind == AsyncProfilerStitchedFrameContextKind.Active &&
                entry.Method != MethodIndex.Invalid;
            if (!hasResolvedActiveEntry)
            {
                return entryIndex;
            }

            bool hasUnambiguousStitching =
                context.StitchingState.ProcessedCallStackCount == context.Segments.Count &&
                context.StitchingState.HasConsistentPhysicalAlignment &&
                context.StitchingState.UnresolvedV1CompletionCount == 0;
            if (!hasUnambiguousStitching)
            {
                return entryIndex;
            }

            AsyncProfilerRuntimeSupport support = context.GetRuntimeSupport(entry.CodeAddress);
            string entryMethodName = AsyncProfilerMethodNames.NormalizeMethodName(context.TraceLog.CodeAddresses[entry.CodeAddress].FullMethodName);
            bool hasSupportedSequence = support.TryGetOptionalSequenceKind(entryMethodName, entry.SyncClassification, out AsyncProfilerRuntimeSequenceKind sequence);
            bool hasVerifiedDispatchEntry =
                hasSupportedSequence &&
                context.HasVerifiedDispatchEntry(entry.Context, entry.CodeAddress, requireAsyncAnchor: false);
            if (!hasVerifiedDispatchEntry)
            {
                return entryIndex;
            }

            ModuleFileIndex owner = context.GetHostModuleOwner(entry.CodeAddress);
            bool hasSequenceOnlyHelper = false;
            int matchedStart = entryIndex;
            // Keep searching toward the leaf for the longest complete sequence.
            for (int start = entryIndex - 1; start >= minimumStart; start--)
            {
                AsyncProfilerStitchedFrame helper = context.Frames[start];
                AsyncProfilerCallStackStitchBoundaryKind boundary = context.ClassifyBoundary(helper.CodeAddress).Kind;
                bool isPhysicalHelper =
                    helper.Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
                    helper.Presentation == AsyncProfilerStitchedFramePresentation.Native &&
                    helper.ContextKind == AsyncProfilerStitchedFrameContextKind.Active;
                bool belongsToCallStack = isPhysicalHelper && ReferenceEquals(helper.Context, entry.Context) && helper.Method != MethodIndex.Invalid;
                bool hasMatchingRuntime =
                    belongsToCallStack &&
                    owner != ModuleFileIndex.Invalid && context.GetHostModuleOwner(helper.CodeAddress) == owner &&
                    ReferenceEquals(context.GetRuntimeSupport(helper.CodeAddress), support);
                bool isNonBoundaryHelper = hasMatchingRuntime && !context.ClassifyBoundary(helper.CodeAddress).IsDispatchMarker;
                bool canExtendSequence =
                    isNonBoundaryHelper &&
                    helper.SyncFrameKind != AsyncProfilerStitchSyncFrameKind.V1StateMachineBody &&
                    helper.SyncFrameKind != AsyncProfilerStitchSyncFrameKind.V2ResumeStub &&
                    helper.BoxedStateMachineMethod == MethodIndex.Invalid;
                if (!canExtendSequence)
                {
                    break;
                }

                // Leave individually recognized helpers on their usual cleanup paths.
                // Match the whole sequence only after a helper needs that extra proof.
                hasSequenceOnlyHelper |=
                    !support.IsOptionalInfrastructureFrame(helper, boundary) &&
                    !support.IsProfilerDispatchInfrastructure(helper.SyncClassification);
                if (!hasSequenceOnlyHelper)
                {
                    continue;
                }

                AsyncProfilerRuntimeSequenceMatch infrastructure = context.MatchRuntimeSequence(sequence, start, entryIndex + 1, allowRuntimeInteriors: true);
                if (infrastructure.IsRejected)
                {
                    rejectedStart = start;
                    return entryIndex;
                }

                bool hasCompleteSequence = infrastructure.IsMatch && infrastructure.Length == entryIndex + 1 - start;
                if (hasCompleteSequence)
                {
                    matchedStart = start;
                }
            }

            return matchedStart;
        }

        /// <summary>
        /// Groups owned runtime helpers before the first active logical frame.
        /// Keeps unrelated callees protected from later cleanup.
        /// </summary>
        /// <returns>Where later cleanup may start, with cleanupEnd adjusted for removed frames.</returns>
        private static int CollapseProfilerOwnedPrefix(AsyncProfilerCallStackTransformState context, ref int cleanupEnd)
        {
            if (context.TraceLog == null || context.Segments.Count == 0)
            {
                return 0;
            }

            List<AsyncProfilerStitchedFrame> frames = context.Frames;
            int targetIndex = FindActiveLogicalTarget(frames, out bool hasProfilerEntryCandidate);
            int registrationEntry = FindV1AwaitRegistrationEntry(context, targetIndex);
            int completionEntry = FindV1CompletionEntry(context, targetIndex);
            bool hasOnlyCompletionEntry = !hasProfilerEntryCandidate && registrationEntry < 0 && completionEntry >= 0;
            bool hasEntryCandidate = hasProfilerEntryCandidate || registrationEntry >= 0 || completionEntry >= 0;
            if (!hasEntryCandidate)
            {
                return 0;
            }

            // Protect the whole prefix when its target or physical alignment is unverified.
            bool hasConsistentTarget =
                targetIndex != frames.Count &&
                context.StitchingState.ProcessedCallStackCount == context.Segments.Count &&
                context.StitchingState.HasConsistentPhysicalAlignment;
            if (!hasConsistentTarget || !IsGroupablePrefixTarget(frames[targetIndex], out bool needsDispatcher))
            {
                return targetIndex;
            }

            // Prefer a complete profiler entry; registration and completion entries are fallbacks.
            int entryIndex = FindCompleteProfilerEntry(context, targetIndex, out bool isRejected);
            if (isRejected)
            {
                return targetIndex;
            }

            if (entryIndex < 0)
            {
                entryIndex = registrationEntry >= 0 ? registrationEntry : completionEntry;
                if (entryIndex < 0)
                {
                    return targetIndex;
                }
            }

            if (!HasOnlyTargetOwnedPrefix(context, targetIndex, entryIndex, needsDispatcher))
            {
                return targetIndex;
            }

            int spanStart = FindProfilerDispatchSpanStart(context, entryIndex, frames[entryIndex], out int rejectedStart);
            if (rejectedStart >= 0)
            {
                return targetIndex;
            }

            int matchedFrameCount = targetIndex - spanStart;
            cleanupEnd -= CollapseMachinerySpan(frames, spanStart, matchedFrameCount);
            context.Diagnostics.AsyncInfrastructureFramesCollapsed += matchedFrameCount;
            context.Diagnostics.ProfilerEnvelopeFramesCollapsed += matchedFrameCount;

            // A completion-only match leaves independent helper groups available to later cleanup.
            return hasOnlyCompletionEntry ? 0 : spanStart + 1;
        }

        /// <summary>
        /// Finds the first active logical frame and notes whether any profiler entry frame precedes it.
        /// </summary>
        /// <returns>The target index, or the frame count if no active logical frame exists.</returns>
        private static int FindActiveLogicalTarget(List<AsyncProfilerStitchedFrame> frames, out bool hasProfilerEntryCandidate)
        {
            hasProfilerEntryCandidate = false;
            for (int i = 0; i < frames.Count; i++)
            {
                AsyncProfilerStitchedFrame frame = frames[i];
                bool isActiveLogicalFrame =
                    frame.ContextKind == AsyncProfilerStitchedFrameContextKind.Active &&
                    (frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent ||
                     frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody ||
                     frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCaller ||
                     frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget);
                if (isActiveLogicalFrame)
                {
                    return i;
                }

                hasProfilerEntryCandidate |= frame.SyncFrameKind == AsyncProfilerStitchSyncFrameKind.ProfilerEntry;
                // A leading CreateDispatcher has individual-helper cleanup. Only a later
                // setup frame can justify grouping the callees before it.
                hasProfilerEntryCandidate |= i > 0 && frame.SyncFrameKind == AsyncProfilerStitchSyncFrameKind.V1DispatchSetup;
            }

            return frames.Count;
        }

        /// <summary>
        /// Checks that the prefix target is anchored physically, or is a pending V1 dispatch target.
        /// </summary>
        private static bool IsGroupablePrefixTarget(in AsyncProfilerStitchedFrame target, out bool needsDispatcher)
        {
            needsDispatcher = target.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget && !target.IsStateMachineBoxCarrier;
            if (needsDispatcher)
            {
                return target.Segment.Kind == AsyncCallstackKind.StateMachineAsync;
            }

            return target.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent ||
                   target.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody ||
                   target.IsStateMachineBoxCarrier;
        }

        /// <summary>Finds a profiler entry sequence that reaches the target exactly.</summary>
        /// <returns>The entry index, or -1 if none is found or a candidate was rejected.</returns>
        private static int FindCompleteProfilerEntry(AsyncProfilerCallStackTransformState context, int targetIndex, out bool isRejected)
        {
            isRejected = false;
            for (int i = targetIndex - 1; i >= 0; i--)
            {
                AsyncProfilerRuntimeSequenceMatch entry = context.MatchRuntimeSequence(AsyncProfilerRuntimeSequenceKind.ProfilerEntry, i, targetIndex);
                if (entry.IsRejected)
                {
                    isRejected = true;
                    return -1;
                }

                if (entry.IsMatch && entry.Length == targetIndex - i)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Checks that every prefix frame belongs to the target call stack.
        /// A pending V1 target also needs exactly one dispatcher at or after the entry.
        /// </summary>
        private static bool HasOnlyTargetOwnedPrefix(
            AsyncProfilerCallStackTransformState context,
            int targetIndex,
            int entryIndex,
            bool needsDispatcher)
        {
            List<AsyncProfilerStitchedFrame> frames = context.Frames;
            AsyncProfilerCallStack targetContext = frames[targetIndex].Context;
            int dispatcherCount = 0;
            for (int i = 0; i < targetIndex; i++)
            {
                AsyncProfilerStitchedFrame frame = frames[i];
                bool belongsToTargetCallStack =
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
                    frame.ContextKind == AsyncProfilerStitchedFrameContextKind.Active &&
                    ReferenceEquals(frame.Context, targetContext);
                if (!belongsToTargetCallStack)
                {
                    return false;
                }

                AsyncProfilerCallStackStitchBoundaryInfo boundary = context.ClassifyBoundary(frame.CodeAddress);
                if (!boundary.IsDispatchMarker)
                {
                    continue;
                }

                bool isExpectedDispatcher =
                    needsDispatcher &&
                    i >= entryIndex &&
                    boundary.Kind == AsyncProfilerCallStackStitchBoundaryKind.V1Dispatcher;
                dispatcherCount++;
                if (!isExpectedDispatcher || dispatcherCount != 1)
                {
                    return false;
                }
            }

            return !needsDispatcher || dispatcherCount == 1;
        }

        /// <summary>Finds the contiguous await-registration helpers immediately before a running V1 method.</summary>
        /// <returns>The first matching helper index, or -1 if the entry cannot be verified.</returns>
        private static int FindV1AwaitRegistrationEntry(AsyncProfilerCallStackTransformState context, int targetIndex)
        {
            if (targetIndex == context.Frames.Count)
            {
                return -1;
            }

            AsyncProfilerStitchedFrame target = context.Frames[targetIndex];
            bool hasV1CurrentFrame =
                target.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent &&
                target.Segment.Kind == AsyncCallstackKind.StateMachineAsync;
            bool hasResolvedCurrentMethod =
                hasV1CurrentFrame &&
                target.Method != MethodIndex.Invalid && target.CodeAddress != CodeAddressIndex.Invalid;
            if (!hasResolvedCurrentMethod)
            {
                return -1;
            }

            // Extend toward the leaf only while each candidate reaches the running method.
            int entryIndex = -1;
            for (int i = targetIndex - 1; i >= 0; i--)
            {
                AsyncProfilerRuntimeSequenceMatch registration = context.MatchRuntimeSequence(AsyncProfilerRuntimeSequenceKind.V1AwaitRegistration, i, targetIndex);
                if (registration.IsRejected)
                {
                    return -1;
                }

                bool hasFullRegistration = registration.IsMatch && registration.Length == targetIndex - i;
                if (!hasFullRegistration)
                {
                    break;
                }

                entryIndex = i;
            }

            return entryIndex;
        }

        /// <summary>
        /// Finds the task-completion sequence before a finishing V1 method.
        /// Includes an adjacent continuation bridge when it shares the same owner.
        /// </summary>
        /// <returns>The first matching helper index, or -1 if the entry cannot be verified.</returns>
        private static int FindV1CompletionEntry(AsyncProfilerCallStackTransformState context, int targetIndex)
        {
            bool hasCompletionPrefix = targetIndex >= 3 && targetIndex != context.Frames.Count;
            if (!hasCompletionPrefix)
            {
                return -1;
            }

            bool hasCompletingBody =
                context.Frames[targetIndex].Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody &&
                IsSyncFrameKind(context.Frames[targetIndex - 1], AsyncProfilerStitchSyncFrameKind.V1MethodCompletion);
            bool hasVerifiedCompletion =
                hasCompletingBody &&
                context.HasVerifiedV1CompletionAnchor(context.Frames[targetIndex].Context, context.Frames[targetIndex - 1].CodeAddress);
            if (!hasVerifiedCompletion)
            {
                return -1;
            }

            for (int i = targetIndex - 2; i >= 0; i--)
            {
                AsyncProfilerRuntimeSequenceMatch pair = context.MatchRuntimeSequence(AsyncProfilerRuntimeSequenceKind.TaskCompletion, i, targetIndex - 1);
                if (pair.IsRejected)
                {
                    return -1;
                }

                bool hasCompletePair = pair.IsMatch && pair.Length == targetIndex - 1 - i;
                if (hasCompletePair)
                {
                    AsyncProfilerRuntimeSequenceMatch chain = context.MatchRuntimeSequence(AsyncProfilerRuntimeSequenceKind.TaskCompletionChain, i, targetIndex);
                    if (chain.IsRejected)
                    {
                        return -1;
                    }

                    bool hasAdjacentBridge = i > 0 && IsSyncFrameKind(context.Frames[i - 1], AsyncProfilerStitchSyncFrameKind.ContinuationBridge);
                    int start = hasAdjacentBridge ? i - 1 : i;
                    return HasSameMachineryOwner(context, start, targetIndex - start) ? start : -1;
                }
            }

            return -1;
        }

        /// <summary>
        /// Removes verified scheduling helpers after the root async call stack.
        /// A partial match keeps a dispatcher marker and protects the remaining callers.
        /// </summary>
        /// <returns>
        /// The exclusive end for later cleanup, excluding rejected or partially grouped bridge frames.
        /// </returns>
        private static int CollapseRuntimeResumptionBridge(AsyncProfilerCallStackTransformState context)
        {
            List<AsyncProfilerStitchedFrame> frames = context.Frames;
            if (context.Segments.Count == 0)
            {
                return frames.Count;
            }

            // Find the root call stack and the runtime scheduling anchor among its callers.
            AsyncProfilerCallStack rootCallStack = context.Segments[context.Segments.Count - 1];
            int rootIndex = FindActiveRootFrame(frames, rootCallStack);
            int bridgeStart = rootIndex + 1;
            while (bridgeStart < frames.Count &&
                   frames[bridgeStart].ContextKind == AsyncProfilerStitchedFrameContextKind.ParentContext)
            {
                bridgeStart++;
            }

            int dispatchIndex = FindRuntimeResumptionAnchor(context, bridgeStart, frames.Count);
            // If no caller contains the scheduling anchor, also check the root's retained frames.
            if (dispatchIndex == frames.Count && rootIndex >= 0)
            {
                dispatchIndex = FindRuntimeResumptionAnchor(context, 0, rootIndex + 1);
            }

            // An unplaced root can identify a candidate to protect, but does not prove it may be removed.
            bool hasPlacedRoot = rootIndex >= 0 && bridgeStart <= dispatchIndex;
            if (!hasPlacedRoot)
            {
                bridgeStart = FindPhysicalSpanStart(context, dispatchIndex);
            }

            int bridgeEnd = dispatchIndex < frames.Count ? dispatchIndex + 1 : frames.Count;
            AsyncProfilerRuntimeSequenceMatch bridge = context.MatchRuntimeSequence(AsyncProfilerRuntimeSequenceKind.RuntimeResumptionBridge, bridgeStart, bridgeEnd);
            if (bridge.Length == 0)
            {
                return frames.Count;
            }

            // Accept only a completely verified bridge, or a verified completion prefix with a physical caller.
            int matchedEnd = bridgeStart + bridge.Length;
            bool hasCompletionPrefix = matchedEnd < bridgeEnd;
            bool isFullBridgeMatch = bridge.IsMatch && !hasCompletionPrefix;
            bool hasCompletedRootDispatch =
                !hasPlacedRoot &&
                isFullBridgeMatch &&
                context.HasVerifiedCompletedV1OuterDispatch(rootCallStack, frames[bridgeStart].CodeAddress);
            // Rejected bridges remain intact; individual-helper cleanup only visits frames before them.
            bool hasDispatchFrame = context.TraceLog != null && dispatchIndex != frames.Count;
            bool hasMatchedRootBridge =
                bridge.IsMatch &&
                (hasPlacedRoot || hasCompletedRootDispatch) &&
                hasDispatchFrame;
            if (!hasMatchedRootBridge)
            {
                return bridgeStart;
            }

            bool hasConsistentStitching =
                context.StitchingState.ProcessedCallStackCount == context.Segments.Count &&
                context.StitchingState.HasConsistentPhysicalAlignment;
            if (!hasConsistentStitching)
            {
                return bridgeStart;
            }

            bool hasRequiredRootAnchor =
                rootCallStack.Frames.Kind != AsyncCallstackKind.RuntimeAsync ||
                HasVerifiedAsyncAnchor(context, rootIndex, rootCallStack);
            if (!hasRequiredRootAnchor)
            {
                return bridgeStart;
            }

            if (hasCompletionPrefix && !HasVerifiedCompletionPrefix(context, rootCallStack, bridgeStart, matchedEnd, dispatchIndex))
            {
                return bridgeStart;
            }

            if (!HasOnlyUnassignedHelperFrames(context, bridgeStart, matchedEnd))
            {
                return bridgeStart;
            }

            // A full bridge keeps its scheduling anchor. A partial bridge keeps one marker
            // and leaves its remaining callers outside subsequent cleanup.
            int bridgeFrameCount = hasCompletionPrefix ? bridge.Length : dispatchIndex - bridgeStart;
            if (hasCompletionPrefix)
            {
                CollapseMachinerySpan(frames, bridgeStart, bridgeFrameCount);
            }
            else
            {
                frames.RemoveRange(bridgeStart, bridgeFrameCount);
            }

            context.Diagnostics.AsyncInfrastructureFramesCollapsed += bridgeFrameCount;
            context.Diagnostics.RuntimeResumptionBridgeFramesCollapsed += bridgeFrameCount;
            return hasCompletionPrefix ? bridgeStart : frames.Count;
        }

        /// <summary>Finds the leaf-most active frame of the root call stack.</summary>
        /// <returns>The frame index, or -1 if the root has no active frame.</returns>
        private static int FindActiveRootFrame(List<AsyncProfilerStitchedFrame> frames, AsyncProfilerCallStack rootCallStack)
        {
            for (int i = frames.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(frames[i].Context, rootCallStack) &&
                    frames[i].ContextKind == AsyncProfilerStitchedFrameContextKind.Active)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>Finds the first runtime resumption anchor in [start, end).</summary>
        /// <returns>The anchor index, or the frame count if none is found.</returns>
        private static int FindRuntimeResumptionAnchor(AsyncProfilerCallStackTransformState context, int start, int end)
        {
            List<AsyncProfilerStitchedFrame> frames = context.Frames;
            for (int i = start; i < end; i++)
            {
                if (context.GetRuntimeSupport(frames[i].CodeAddress).IsRuntimeResumptionAnchor(frames[i]))
                {
                    return i;
                }
            }

            return frames.Count;
        }

        /// <summary>Finds the start of the physical sync span that ends before the given index.</summary>
        private static int FindPhysicalSpanStart(AsyncProfilerCallStackTransformState context, int end)
        {
            List<AsyncProfilerStitchedFrame> frames = context.Frames;
            int start = 0;
            for (int i = 0; i < end; i++)
            {
                bool isPhysicalSpanBreak =
                    frames[i].Origin != AsyncProfilerStitchedFrameOrigin.Sync ||
                    context.ClassifyBoundary(frames[i].CodeAddress).IsDispatchMarker;
                if (isPhysicalSpanBreak)
                {
                    start = i + 1;
                }
            }

            return start;
        }

        /// <summary>
        /// Verifies a partial bridge: it needs a resolved application caller after the matched
        /// helpers and a scheduling anchor that shares the bridge's runtime and call-stack owner.
        /// </summary>
        private static bool HasVerifiedCompletionPrefix(
            AsyncProfilerCallStackTransformState context,
            AsyncProfilerCallStack rootCallStack,
            int bridgeStart,
            int matchedEnd,
            int dispatchIndex)
        {
            bool hasUnambiguousCompletionPrefix =
                context.StitchingState.UnresolvedV1CompletionCount == 0 &&
                matchedEnd < dispatchIndex;
            if (!hasUnambiguousCompletionPrefix)
            {
                return false;
            }

            List<AsyncProfilerStitchedFrame> frames = context.Frames;
            CodeAddressIndex bridgeAddress = frames[bridgeStart].CodeAddress;

            // Keep a resolved application caller after the partial runtime sequence.
            AsyncProfilerStitchedFrame caller = frames[matchedEnd];
            bool hasResolvedPhysicalCaller =
                IsUnassignedPhysicalFrame(caller) &&
                caller.CodeAddress != CodeAddressIndex.Invalid &&
                caller.Method != MethodIndex.Invalid;
            bool hasApplicationCaller =
                hasResolvedPhysicalCaller &&
                context.TraceLog.CodeAddresses.ModuleFileIndex(caller.CodeAddress) != ModuleFileIndex.Invalid &&
                context.GetHostModuleOwner(caller.CodeAddress) == ModuleFileIndex.Invalid &&
                !context.ClassifyBoundary(caller.CodeAddress).IsDispatchMarker;
            if (!hasApplicationCaller)
            {
                return false;
            }

            // The scheduling anchor must share the bridge's runtime and verified call-stack owner.
            AsyncProfilerStitchedFrame anchor = frames[dispatchIndex];
            bool hasResolvedPhysicalAnchor = IsUnassignedPhysicalFrame(anchor) && anchor.Method != MethodIndex.Invalid;
            bool hasMatchingAnchorRuntime =
                hasResolvedPhysicalAnchor &&
                context.GetHostModuleOwner(anchor.CodeAddress) == context.GetHostModuleOwner(bridgeAddress) &&
                ReferenceEquals(context.GetRuntimeSupport(anchor.CodeAddress), context.GetRuntimeSupport(bridgeAddress));
            if (!hasMatchingAnchorRuntime)
            {
                return false;
            }

            return context.HasVerifiedDispatchEntry(rootCallStack, bridgeAddress) ||
                   context.HasVerifiedV1CompletionAnchor(rootCallStack, bridgeAddress, allowCompletionUnwind: true);
        }

        /// <summary>Checks that [start, end) holds only unassigned physical frames and no dispatcher.</summary>
        private static bool HasOnlyUnassignedHelperFrames(AsyncProfilerCallStackTransformState context, int start, int end)
        {
            List<AsyncProfilerStitchedFrame> frames = context.Frames;
            for (int i = start; i < end; i++)
            {
                if (!IsUnassignedPhysicalFrame(frames[i]))
                {
                    return false;
                }

                AsyncProfilerCallStackStitchBoundaryKind boundary = context.ClassifyBoundary(frames[i].CodeAddress).Kind;
                bool isDispatcher =
                    boundary == AsyncProfilerCallStackStitchBoundaryKind.V1Dispatcher ||
                    boundary == AsyncProfilerCallStackStitchBoundaryKind.V2Dispatcher;
                if (isDispatcher)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsUnassignedPhysicalFrame(in AsyncProfilerStitchedFrame frame)
        {
            return frame.Origin == AsyncProfilerStitchedFrameOrigin.Sync && frame.Context == null;
        }

        #endregion
    }
}
