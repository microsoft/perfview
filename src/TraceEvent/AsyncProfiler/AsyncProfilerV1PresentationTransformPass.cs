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
    /// Presents V1 state-machine frames as logical async methods and removes redundant
    /// startup, completion, and dispatch frames. Keeps logical callers and any physical
    /// frames that cannot be safely matched.
    /// </summary>
    internal sealed class AsyncProfilerV1PresentationTransformPass
    {
        public void Apply(AsyncProfilerCallStackTransformState context)
        {
            List<AsyncProfilerStitchedFrame> frames = context.Frames;

            // Identify the finishing call when recursive V1 frames are ambiguous.
            ResolveRecursiveV1Completion(context);

            // Remove verified return helpers while preserving the logical methods.
            CollapseCompletedV1Unwinds(context);

            // Preserve the leaf-to-root interleaving of handoff, deduplication, and startup edits.
            for (int i = 0; i < frames.Count; i++)
            {
                AsyncProfilerStitchedFrame frame = frames[i];
                if (frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget)
                {
                    if (frame.Segment != null && frame.Segment.Kind == AsyncCallstackKind.StateMachineAsync)
                    {
                        if (frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent)
                        {
                            // Remove V2 completion helpers that resumed the current V1 method.
                            CollapseV2CompletionHandoff(context, i);
                        }

                        // Remove duplicate startup frames already represented by this async frame.
                        TryApplyReentrantV1Deduplication(context, i);
                    }

                    continue;
                }

                if (frame.Origin != AsyncProfilerStitchedFrameOrigin.Sync || frame.SyncFrameKind != AsyncProfilerStitchSyncFrameKind.V1StateMachineBody)
                {
                    continue;
                }

                // Display the logical method and fold its startup helpers when their layout matches.
                AsyncProfilerRuntimeSequenceMatch startup = context.MatchRuntimeSequence(AsyncProfilerRuntimeSequenceKind.V1Startup, i, frames.Count);
                if (!startup.IsMatch)
                {
                    continue;
                }

                int end = i + startup.Length;
                int builderCount = end - i - 1;
                bool canFoldStartupIntoKickoff =
                    builderCount != 0 &&
                    end < frames.Count &&
                    context.TraceLog != null &&
                    IsMatchingV1Kickoff(context.TraceLog, frame, frames[end]);

                if (canFoldStartupIntoKickoff)
                {
                    int generatedFrameCount = builderCount + 1;
                    frames.RemoveRange(i, generatedFrameCount);
                    context.Diagnostics.V1SynchronousStartupFramesCollapsed += generatedFrameCount;
                    continue;
                }

                frames[i] = frame.WithPresentation(AsyncProfilerStitchedFramePresentation.LogicalStateMachineMethod);
                context.Diagnostics.V1SynchronousMoveNextFramesNormalized++;

                if (end == i + 1)
                {
                    continue;
                }

                frames.RemoveRange(i + 1, builderCount);
                context.Diagnostics.V1SynchronousStartupFramesCollapsed += builderCount;
            }
        }

        #region private

        /// <summary>
        /// Identifies which recursive V1 call is finishing and removes its return helpers.
        /// Leaves the remaining recursive calls unchanged.
        /// </summary>
        private static void ResolveRecursiveV1Completion(AsyncProfilerCallStackTransformState context)
        {
            if (!TryGetSingleRecursiveV1Completion(context, out AsyncProfilerCallStack callStack, out MethodIndex recursiveMethod))
            {
                return;
            }

            // Locate the next recursive call and the dispatcher immediately before it.
            List<AsyncProfilerStitchedFrame> frames = context.Frames;
            int target = FindRecursiveV1DispatchTarget(frames, callStack);
            bool hasExpectedDispatcher =
                target >= 2 &&
                context.ClassifyBoundary(frames[target - 1].CodeAddress).Kind == AsyncProfilerCallStackStitchBoundaryKind.V1Dispatcher;
            if (!hasExpectedDispatcher)
            {
                return;
            }

            ModuleFileIndex owner = context.GetHostModuleOwner(frames[target - 1].CodeAddress);
            AsyncProfilerRuntimeSupport support = context.GetRuntimeSupport(frames[target - 1].CodeAddress);
            if (!IsOwnedPhysicalFrame(context, frames[target - 1], callStack, owner, support))
            {
                return;
            }

            int body = FindRecursiveV1FinishingBody(context, target, callStack, owner, support, recursiveMethod);
            if (body < 0 || !HasRecursiveV1CompletionEntry(context, body, callStack, owner, support, recursiveMethod))
            {
                return;
            }

            // Mark captured frame zero as completed, leaving the pending recursive calls in place.
            frames[body] = AsyncProfilerStitchedFrame.CreateV1CompletedBody(frames[body].CodeAddress, frames[body].Method, callStack, 0);
            int removed = target - body - 1;
            frames.RemoveRange(body + 1, removed);
            context.StitchingState.UnresolvedV1CompletionCount--;
            context.Diagnostics.V1CompletionAlignmentAmbiguous = context.StitchingState.UnresolvedV1CompletionCount;
            context.Diagnostics.V1CompletionTailUsed++;
            context.Diagnostics.V1InfrastructureFramesCollapsed += removed;
        }

        /// <summary>
        /// Checks for one ambiguous call stack whose leaf completed normally once
        /// and whose two leaf-most captured frames are the same method.
        /// </summary>
        private static bool TryGetSingleRecursiveV1Completion(
            AsyncProfilerCallStackTransformState context,
            out AsyncProfilerCallStack callStack,
            out MethodIndex recursiveMethod)
        {
            callStack = null;
            recursiveMethod = MethodIndex.Invalid;
            bool hasSingleAmbiguousCallStack =
                context.TraceLog != null &&
                context.Segments.Count == 1 &&
                context.StitchingState.ProcessedCallStackCount == 1 &&
                context.StitchingState.HasConsistentPhysicalAlignment &&
                context.StitchingState.UnresolvedV1CompletionCount == 1;
            if (!hasSingleAmbiguousCallStack)
            {
                return false;
            }

            // With several completed recursive calls, we cannot tell which copy remains.
            callStack = context.Segments[0];
            bool hasSingleNormalCompletion =
                callStack.Frames.Kind == AsyncCallstackKind.StateMachineAsync &&
                callStack.Frames.FrameCount >= 2 &&
                context.TraceLog.AsyncProfilerCallStacks?.MethodCompletionObserved(callStack) == true &&
                callStack.GetMethodCompletedFrameCount(context.TimestampQpc) == 1 &&
                callStack.GetExceptionCompletedFrameCount(context.TimestampQpc) == 0;
            if (!hasSingleNormalCompletion)
            {
                return false;
            }

            recursiveMethod = context.GetMethod(callStack.Frames.CodeAddressAt(0));
            return recursiveMethod != MethodIndex.Invalid &&
                   context.GetMethod(callStack.Frames.CodeAddressAt(1)) == recursiveMethod;
        }

        /// <summary>Finds the pending dispatch target for captured frame one of the call stack.</summary>
        /// <returns>The frame index, or -1 if none is found.</returns>
        private static int FindRecursiveV1DispatchTarget(List<AsyncProfilerStitchedFrame> frames, AsyncProfilerCallStack callStack)
        {
            for (int i = 0; i < frames.Count; i++)
            {
                AsyncProfilerStitchedFrame frame = frames[i];
                bool isRecursiveDispatchTarget =
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget &&
                    frame.ContextKind == AsyncProfilerStitchedFrameContextKind.Active &&
                    ReferenceEquals(frame.Context, callStack) &&
                    frame.SegmentFrameIndex == 1 &&
                    !frame.IsStateMachineBoxCarrier;
                if (isRecursiveDispatchTarget)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Follows the return helpers before the dispatcher back to the finishing recursive body.
        /// </summary>
        /// <returns>The body index, or -1 if the helpers or the body cannot be verified.</returns>
        private static int FindRecursiveV1FinishingBody(
            AsyncProfilerCallStackTransformState context,
            int target,
            AsyncProfilerCallStack callStack,
            ModuleFileIndex owner,
            AsyncProfilerRuntimeSupport support,
            MethodIndex recursiveMethod)
        {
            List<AsyncProfilerStitchedFrame> frames = context.Frames;
            int body = target - 2;
            bool hasMatchingReturnBox = false;
            for (; body >= 0 && frames[body].SyncFrameKind != AsyncProfilerStitchSyncFrameKind.V1StateMachineBody; body--)
            {
                AsyncProfilerStitchedFrame frame = frames[body];
                bool isOwnedReturnHelper =
                    IsOwnedPhysicalFrame(context, frame, callStack, owner, support) &&
                    (support.IsV1CompletionUnwindInfrastructure(frame, context.ClassifyBoundary(frame.CodeAddress).Kind) ||
                     frame.SyncFrameKind == AsyncProfilerStitchSyncFrameKind.V1BoxCallback);
                bool isMatchingReturnHelper =
                    isOwnedReturnHelper &&
                    (frame.BoxedStateMachineMethod == MethodIndex.Invalid || frame.BoxedStateMachineMethod == recursiveMethod);
                if (!isMatchingReturnHelper)
                {
                    return -1;
                }

                hasMatchingReturnBox |= frame.BoxedStateMachineMethod == recursiveMethod;
            }

            if (!hasMatchingReturnBox || body < 1)
            {
                return -1;
            }

            AsyncProfilerStitchedFrame bodyFrame = frames[body];
            bool isMatchingRecursiveBody =
                bodyFrame.Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
                bodyFrame.ContextKind == AsyncProfilerStitchedFrameContextKind.Active &&
                ReferenceEquals(bodyFrame.Context, callStack) &&
                bodyFrame.CodeAddress != CodeAddressIndex.Invalid &&
                bodyFrame.Method == recursiveMethod;
            bool hasOwnedCompletionHelper =
                isMatchingRecursiveBody &&
                IsOwnedPhysicalFrame(context, frames[body - 1], callStack, owner, support) &&
                frames[body - 1].SyncFrameKind == AsyncProfilerStitchSyncFrameKind.V1MethodCompletion;
            return hasOwnedCompletionHelper ? body : -1;
        }

        /// <summary>
        /// Verifies the owned completion path before the body, ending in entry boxes for the same
        /// recursive method, with no earlier body or box that would make the finishing call ambiguous.
        /// </summary>
        private static bool HasRecursiveV1CompletionEntry(
            AsyncProfilerCallStackTransformState context,
            int body,
            AsyncProfilerCallStack callStack,
            ModuleFileIndex owner,
            AsyncProfilerRuntimeSupport support,
            MethodIndex recursiveMethod)
        {
            List<AsyncProfilerStitchedFrame> frames = context.Frames;
            int entry = FindV1CompletionPairBeforeBody(context, 0, body);
            if (entry < 0)
            {
                return false;
            }

            // Require every helper in the completion span to belong to this call stack and runtime.
            for (int i = entry; i < body - 1; i++)
            {
                if (!IsOwnedPhysicalFrame(context, frames[i], callStack, owner, support))
                {
                    return false;
                }
            }

            // Check adjacent awaiter and continuation helpers before the completion entry.
            while (entry > 0 &&
                (frames[entry - 1].SyncFrameKind == AsyncProfilerStitchSyncFrameKind.AwaiterHelper ||
                 frames[entry - 1].SyncFrameKind == AsyncProfilerStitchSyncFrameKind.ContinuationBridge))
            {
                if (!IsOwnedPhysicalFrame(context, frames[--entry], callStack, owner, support))
                {
                    return false;
                }
            }

            // Verify that the preceding entry boxes identify the same recursive method.
            int boxesEnd = entry;
            while (entry > 0 && frames[entry - 1].BoxedStateMachineMethod != MethodIndex.Invalid)
            {
                AsyncProfilerStitchedFrame box = frames[--entry];
                bool isMatchingEntryBox =
                    IsOwnedPhysicalFrame(context, box, callStack, owner, support) &&
                    box.BoxedStateMachineMethod == recursiveMethod &&
                    box.SyncFrameKind == AsyncProfilerStitchSyncFrameKind.ProfilerHelper &&
                    support.IsProfilerDispatchInfrastructure(box.SyncClassification);
                if (!isMatchingEntryBox)
                {
                    return false;
                }
            }

            if (entry == boxesEnd)
            {
                return false;
            }

            for (int i = 0; i < entry; i++)
            {
                if (frames[i].SyncFrameKind == AsyncProfilerStitchSyncFrameKind.V1StateMachineBody ||
                    frames[i].BoxedStateMachineMethod != MethodIndex.Invalid)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Checks that a resolved physical frame belongs to the call stack and captured runtime.</summary>
        private static bool IsOwnedPhysicalFrame(AsyncProfilerCallStackTransformState context,
            AsyncProfilerStitchedFrame frame, AsyncProfilerCallStack callStack,
            ModuleFileIndex owner, AsyncProfilerRuntimeSupport support)
        {
            bool belongsToCallStack =
                frame.Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
                frame.ContextKind == AsyncProfilerStitchedFrameContextKind.Active &&
                ReferenceEquals(frame.Context, callStack);

            bool hasResolvedIdentity =
                belongsToCallStack &&
                frame.CodeAddress != CodeAddressIndex.Invalid && frame.Method != MethodIndex.Invalid;

            bool hasMatchingRuntimeOwner =
                hasResolvedIdentity &&
                owner != ModuleFileIndex.Invalid && context.GetHostModuleOwner(frame.CodeAddress) == owner;

            bool hasMatchingRuntimeSupport =
                hasMatchingRuntimeOwner &&
                ReferenceEquals(context.GetRuntimeSupport(frame.CodeAddress), support);

            return hasMatchingRuntimeSupport;
        }

        /// <summary>
        /// Finds the task-completion pair before the V1 method body, or returns -1 if it cannot be verified.
        /// </summary>
        private static int FindV1CompletionPairBeforeBody(AsyncProfilerCallStackTransformState context, int start, int body)
        {
            for (int i = body - 2; i >= start; i--)
            {
                AsyncProfilerRuntimeSequenceMatch pair = context.MatchRuntimeSequence(AsyncProfilerRuntimeSequenceKind.TaskCompletion, i, body - 1);
                if (pair.IsRejected)
                {
                    return -1;
                }

                if (pair.IsMatch && i + pair.Length == body - 1)
                {
                    return context.MatchRuntimeSequence(AsyncProfilerRuntimeSequenceKind.TaskCompletionChain, i, body).IsRejected ? -1 : i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Replaces verified V1 completion-return frames without treating completed methods as executing bodies.
        /// </summary>
        private static void CollapseCompletedV1Unwinds(AsyncProfilerCallStackTransformState context)
        {
            if (context.TraceLog == null)
            {
                return;
            }

            List<AsyncProfilerStitchedFrame> frames = context.Frames;
            for (int start = 0; start < frames.Count;)
            {
                // Work on one contiguous call-stack context at a time; end is exclusive.
                AsyncProfilerCallStack callStack = frames[start].Context;
                int end = start + 1;
                while (end < frames.Count && ReferenceEquals(frames[end].Context, callStack))
                {
                    end++;
                }

                bool hasObservedV1MethodCompletions =
                    callStack != null &&
                    callStack.Frames.Kind == AsyncCallstackKind.StateMachineAsync &&
                    context.TraceLog.AsyncProfilerCallStacks?.MethodCompletionObserved(callStack) == true &&
                    callStack.GetMethodCompletedFrameCount(context.TimestampQpc) > 0;
                if (hasObservedV1MethodCompletions)
                {
                    end = CollapseCompletedV1Unwind(context, start, end, callStack);
                }

                start = end;
            }
        }

        /// <summary>
        /// Replaces the completed V1 unwind at the start of one call-stack context with a completion frame.
        /// </summary>
        /// <returns>The updated exclusive end of the context.</returns>
        private static int CollapseCompletedV1Unwind(AsyncProfilerCallStackTransformState context, int start, int end, AsyncProfilerCallStack callStack)
        {
            List<AsyncProfilerStitchedFrame> frames = context.Frames;
            int completed = callStack.GetCompletedFrameCount(context.TimestampQpc);
            bool fullyCompleted = completed == callStack.Frames.FrameCount;
            bool incompleteRoot =
                end == frames.Count &&
                context.Segments.Count != 0 &&
                ReferenceEquals(context.Segments[context.Segments.Count - 1], callStack);
            int physicalEnd = start;
            while (physicalEnd < end && frames[physicalEnd].Origin == AsyncProfilerStitchedFrameOrigin.Sync)
            {
                physicalEnd++;
            }

            // Match the physical prefix before replacing it. Pending logical callers must survive the edit.
            bool partialReturn =
                incompleteRoot &&
                completed > 0 &&
                completed < callStack.Frames.FrameCount &&
                HasRemainingV1Callers(frames, physicalEnd, end, completed + 1, callStack.Frames.FrameCount);
            bool isFullyCompletedPhysicalContext = fullyCompleted && physicalEnd == end;

            int unwindStart = start;
            int completingPos = -1;
            bool hasMatchingDispatcherTail =
                isFullyCompletedPhysicalContext &&
                TryMatchFullyCompletedV1DispatcherTail(context, start, end, callStack, out unwindStart);
            bool hasMatchingRecursiveUnwind =
                partialReturn &&
                TryMatchRecursiveV1Unwind(context, start, physicalEnd, callStack, completed, preserveLeaf: true, out unwindStart);
            bool canMatchCompletedUnwind = isFullyCompletedPhysicalContext || partialReturn;
            bool hasMatchingCompletionUnwind =
                hasMatchingDispatcherTail ||
                hasMatchingRecursiveUnwind ||
                (canMatchCompletedUnwind &&
                 TryMatchCompletedV1Unwind(context, start, physicalEnd, callStack, completed, out unwindStart, out completingPos));
            if (!hasMatchingCompletionUnwind)
            {
                return end;
            }

            AsyncProfilerStitchedFrame completionFrame;
            if (hasMatchingRecursiveUnwind)
            {
                completionFrame = AsyncProfilerStitchedFrame.CreateSynthetic("[Async completion unwind]", callStack);
            }
            else if (fullyCompleted)
            {
                completionFrame = AsyncProfilerStitchedFrame.CreateV1CompletionReturn(callStack, hasMatchingDispatcherTail);
            }
            else
            {
                AsyncProfilerStitchedFrame completing = frames[completingPos];
                completionFrame = AsyncProfilerStitchedFrame.CreateV1CompletedBody(completing.CodeAddress, completing.Method, callStack, completed - 1);
            }

            int matchedFrameCount = physicalEnd - unwindStart;
            int retainedCallers = end - physicalEnd;
            int insertionIndex = unwindStart;
            bool retainMachinery =
                !hasMatchingRecursiveUnwind &&
                unwindStart > start &&
                frames[unwindStart].SyncFrameKind != AsyncProfilerStitchSyncFrameKind.V1StateMachineBody;
            if (retainMachinery)
            {
                CollapseMachinerySpan(frames, unwindStart, matchedFrameCount);
                insertionIndex++;
            }
            else
            {
                frames.RemoveRange(unwindStart, matchedFrameCount);
            }

            // Keep the exclusive context end in sync with removals and insertions.
            frames.Insert(insertionIndex, completionFrame);
            end = insertionIndex + 1 + retainedCallers;
            if (!fullyCompleted)
            {
                // Without the dispatcher or a carrier, the next captured method is ancestry,
                // not a verified dispatch target or a physically running body.
                frames.Insert(insertionIndex + 1, AsyncProfilerStitchedFrame.CreateAsync(callStack, completed));
                end++;
            }

            context.Diagnostics.V1CompletionUnwindFramesCollapsed += matchedFrameCount;
            if (incompleteRoot)
            {
                frames.Insert(end++, AsyncProfilerStitchedFrame.CreateIncompletePhysicalRoot());
                context.Diagnostics.IncompleteAsyncRootsNormalized++;
            }

            return end;
        }

        /// <summary>
        /// Checks that the span contains exactly the expected remaining V1 callers, in captured order.
        /// </summary>
        private static bool HasRemainingV1Callers(List<AsyncProfilerStitchedFrame> frames, int start, int end, int next, int frameCount)
        {
            for (int i = start; i < end; i++, next++)
            {
                if (frames[i].Origin != AsyncProfilerStitchedFrameOrigin.AsyncCaller ||
                    frames[i].SegmentFrameIndex != next)
                {
                    return false;
                }
            }

            return next == frameCount;
        }

        /// <summary>
        /// Finds a verified V1 completion-return span without changing the frames.
        /// For a partial return, also locates the finishing method body.
        /// </summary>
        private static bool TryMatchCompletedV1Unwind(AsyncProfilerCallStackTransformState context,
            int start, int end, AsyncProfilerCallStack callStack, int completed,
            out int unwindStart, out int completingPos)
        {
            unwindStart = start;
            completingPos = -1;
            // Handle repeated recursive methods before requiring unique captured methods.
            bool hasMatchingRecursiveUnwind =
                completed == callStack.Frames.FrameCount &&
                TryMatchRecursiveV1Unwind(context, start, end, callStack, completed, preserveLeaf: false, out unwindStart);
            if (hasMatchingRecursiveUnwind)
            {
                return true;
            }

            // A failed recursive match may have moved the start; restart the general match.
            unwindStart = start;
            AsyncProfilerCallStackFrames captured = callStack.Frames;
            if (!HasUniqueCapturedV1Methods(context.TraceLog, captured))
            {
                return false;
            }

            int previousCapturedIndex = -1;
            int matchedMethodCount = 0;
            bool hasBodyForMatchedMethod = false;
            bool hasBody = false;
            bool hasBox = false;
            bool hasCompletion = false;
            ModuleFileIndex runtimeModule = ModuleFileIndex.Invalid;
            AsyncProfilerRuntimeSupport runtimeSupport = null;

            // Match physical bodies and box-carried methods to the captured completed frames.
            for (int i = start; i < end; i++)
            {
                AsyncProfilerStitchedFrame frame = context.Frames[i];
                if (frame.Origin != AsyncProfilerStitchedFrameOrigin.Sync)
                {
                    return false;
                }

                bool isStateMachineBody = frame.SyncFrameKind == AsyncProfilerStitchSyncFrameKind.V1StateMachineBody;
                bool isStateMachineBox = frame.BoxedStateMachineMethod != MethodIndex.Invalid;
                MethodIndex method = MethodIndex.Invalid;
                if (isStateMachineBody)
                {
                    method = frame.Method;
                }
                else if (isStateMachineBox)
                {
                    method = frame.BoxedStateMachineMethod;
                }

                int capturedIndex = FindCapturedMethodIndex(context.TraceLog, captured, captured.FrameCount, method);

                // A method that has not completed cannot belong to the return span.
                if (capturedIndex >= completed)
                {
                    return false;
                }

                AsyncProfilerRuntimeSupport support = context.GetRuntimeSupport(frame.CodeAddress);
                AsyncProfilerCallStackStitchBoundaryKind boundaryKind = context.ClassifyBoundary(frame.CodeAddress).Kind;
                bool isCompletionHelper = support.IsV1CompletionUnwindInfrastructure(frame, boundaryKind);
                bool hasUnmatchedStateMachineMethod = (isStateMachineBody || isStateMachineBox) && capturedIndex < 0;
                bool isUnexpectedHelper = !isStateMachineBody && !isCompletionHelper;
                bool hasUnresolvedIdentity = frame.CodeAddress == CodeAddressIndex.Invalid || frame.Method == MethodIndex.Invalid;
                if (hasUnmatchedStateMachineMethod || isUnexpectedHelper || hasUnresolvedIdentity)
                {
                    // Unrelated callees may precede the span, but cannot interrupt a match.
                    if (matchedMethodCount != 0)
                    {
                        return false;
                    }

                    // Callees outside the verified completion-return sequence retain their physical identity and cost.
                    unwindStart = i + 1;
                    runtimeModule = ModuleFileIndex.Invalid;
                    runtimeSupport = null;
                    hasCompletion = false;
                    continue;
                }

                // All return helpers must belong to the same runtime module and support.
                if (!isStateMachineBody)
                {
                    ModuleFileIndex moduleIndex = context.GetHostModuleOwner(frame.CodeAddress);
                    if (moduleIndex == ModuleFileIndex.Invalid)
                    {
                        return false;
                    }

                    bool hasConflictingRuntime =
                        runtimeModule != ModuleFileIndex.Invalid &&
                        (runtimeModule != moduleIndex || !ReferenceEquals(runtimeSupport, support));
                    if (hasConflictingRuntime)
                    {
                        return false;
                    }

                    runtimeModule = moduleIndex;
                    runtimeSupport = support;
                    hasCompletion |= frame.SyncFrameKind == AsyncProfilerStitchSyncFrameKind.V1MethodCompletion;
                    hasBox |= isStateMachineBox;
                }

                if (capturedIndex < 0)
                {
                    continue;
                }

                // Visit methods in reverse captured order, allowing helpers for the same method.
                bool startsNewMethodMatch = previousCapturedIndex < 0 || capturedIndex == previousCapturedIndex - 1;
                if (startsNewMethodMatch)
                {
                    matchedMethodCount++;
                    previousCapturedIndex = capturedIndex;
                    hasBodyForMatchedMethod = false;
                }
                else if (capturedIndex != previousCapturedIndex)
                {
                    return false;
                }

                // A method may have several helpers, but only one physical body in the span.
                if (isStateMachineBody)
                {
                    if (hasBodyForMatchedMethod)
                    {
                        return false;
                    }

                    hasBodyForMatchedMethod = true;
                    hasBody = true;
                    if (capturedIndex == completed - 1)
                    {
                        completingPos = i;
                    }
                }
            }

            bool hasRequiredCompletionEvidence = matchedMethodCount >= 2 && hasBody && hasBox && hasCompletion;
            bool hasRequiredFinishingBody = completed == captured.FrameCount || completingPos >= 0;
            return hasRequiredCompletionEvidence && hasRequiredFinishingBody;
        }

        /// <summary>
        /// Checks that each captured frame resolves to a unique method and that the root is a recognized V1 state-machine method.
        /// </summary>
        private static bool HasUniqueCapturedV1Methods(TraceLog log, AsyncProfilerCallStackFrames captured)
        {
            int frameCount = captured.FrameCount;
            for (int i = 0; i < frameCount; i++)
            {
                MethodIndex method = GetCapturedMethod(log, captured, i);
                bool hasResolvedMethod = method != MethodIndex.Invalid;
                bool isUnrecognizedRootMethod =
                    hasResolvedMethod &&
                    i == frameCount - 1 &&
                    !IsRecognizedV1RootMethod(log, captured.CodeAddressAt(i));
                if (!hasResolvedMethod || isUnrecognizedRootMethod)
                {
                    return false;
                }

                // Repeated methods cannot be assigned unique captured positions in this match.
                if (FindCapturedMethodIndex(log, captured, i, method) >= 0)
                {
                    return false;
                }
            }

            return true;
        }

        private static MethodIndex GetCapturedMethod(TraceLog log, AsyncProfilerCallStackFrames captured, int index)
        {
            CodeAddressIndex address = captured.CodeAddressAt(index);
            if (address == CodeAddressIndex.Invalid)
            {
                return MethodIndex.Invalid;
            }

            return log.CodeAddresses.MethodIndex(address);
        }

        /// <summary>Finds the method among the first <paramref name="count"/> captured frames.</summary>
        /// <returns>The captured index, or -1 if the method is invalid or not found.</returns>
        private static int FindCapturedMethodIndex(TraceLog log, AsyncProfilerCallStackFrames captured, int count, MethodIndex method)
        {
            if (method == MethodIndex.Invalid)
            {
                return -1;
            }

            TraceMethods methods = log.CodeAddresses.Methods;
            for (int i = 0; i < count; i++)
            {
                if (SameMethodIdentity(methods, GetCapturedMethod(log, captured, i), method))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Finds a verified V1 dispatcher tail after all captured methods have completed normally.
        /// Unrelated physical callees before the tail are excluded from the match.
        /// </summary>
        private static bool TryMatchFullyCompletedV1DispatcherTail(AsyncProfilerCallStackTransformState context,
            int start, int end, AsyncProfilerCallStack callStack, out int unwindStart)
        {
            unwindStart = end;
            AsyncProfilerCallStackFrames captured = callStack.Frames;

            // Require exact normal completion and unambiguous stitching before matching the tail.
            bool hasFramesToMatch = start != end && captured.FrameCount != 0;
            if (!hasFramesToMatch)
            {
                return false;
            }

            bool hasFullNormalCompletion =
                callStack.GetMethodCompletedFrameCount(context.TimestampQpc) == captured.FrameCount &&
                callStack.GetExceptionCompletedFrameCount(context.TimestampQpc) == 0;
            if (!hasFullNormalCompletion)
            {
                return false;
            }

            bool hasUnambiguousStitching =
                context.StitchingState.ProcessedCallStackCount == context.Segments.Count &&
                context.StitchingState.HasConsistentPhysicalAlignment &&
                context.StitchingState.UnresolvedV1CompletionCount == 0;
            if (!hasUnambiguousStitching)
            {
                return false;
            }

            // Verify the captured methods and the dispatcher entry before inspecting its box helpers.
            MethodIndex leafMethod = context.GetMethod(captured.CodeAddressAt(0));
            CodeAddressIndex rootAddress = captured.CodeAddressAt(captured.FrameCount - 1);
            AsyncProfilerStitchedFrame tail = context.Frames[end - 1];
            bool hasResolvedMethods = leafMethod != MethodIndex.Invalid && context.GetMethod(rootAddress) != MethodIndex.Invalid;
            if (!hasResolvedMethods)
            {
                return false;
            }

            string rootNormalizedMethodName = AsyncProfilerMethodNames.NormalizeMethodName(context.TraceLog.CodeAddresses[rootAddress].FullMethodName);
            bool hasRecognizedV1Root = AsyncProfilerMethodNames.TryGetLogicalStateMachineMethodName(rootNormalizedMethodName, out _);
            bool hasMatchingDispatchEntry = hasRecognizedV1Root && context.HasVerifiedDispatchEntry(callStack, tail.CodeAddress, requireAsyncAnchor: false);
            if (!hasMatchingDispatchEntry)
            {
                return false;
            }

            ModuleFileIndex owner = context.GetHostModuleOwner(tail.CodeAddress);
            AsyncProfilerRuntimeSupport support = context.GetRuntimeSupport(tail.CodeAddress);

            // Match adjacent box helpers from this call stack, runtime, and completed leaf method.
            while (unwindStart > start && context.Frames[unwindStart - 1].BoxedStateMachineMethod != MethodIndex.Invalid)
            {
                AsyncProfilerStitchedFrame frame = context.Frames[--unwindStart];
                bool belongsToCallStack =
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
                    frame.ContextKind == AsyncProfilerStitchedFrameContextKind.Active &&
                    ReferenceEquals(frame.Context, callStack);
                bool isMatchingBoxHelper =
                    belongsToCallStack &&
                    frame.Method != MethodIndex.Invalid &&
                    frame.BoxedStateMachineMethod == leafMethod &&
                    frame.SyncFrameKind == AsyncProfilerStitchSyncFrameKind.ProfilerHelper;
                bool hasMatchingRuntime =
                    isMatchingBoxHelper &&
                    owner != ModuleFileIndex.Invalid &&
                    context.GetHostModuleOwner(frame.CodeAddress) == owner &&
                    ReferenceEquals(context.GetRuntimeSupport(frame.CodeAddress), support);
                bool isVerifiedReturnHelper =
                    hasMatchingRuntime &&
                    support.IsV1CompletionUnwindInfrastructure(frame, context.ClassifyBoundary(frame.CodeAddress).Kind);
                if (!isVerifiedReturnHelper)
                {
                    return false;
                }
            }

            // At least one matching box helper must be present.
            if (unwindStart == end)
            {
                return false;
            }

            // Another async body, box, or dispatcher before the tail would make this match ambiguous.
            for (int i = start; i < unwindStart; i++)
            {
                AsyncProfilerStitchedFrame frame = context.Frames[i];
                AsyncProfilerCallStackStitchBoundaryInfo boundary = context.ClassifyBoundary(frame.CodeAddress);
                bool hasStateMachineFrame =
                    frame.SyncFrameKind == AsyncProfilerStitchSyncFrameKind.V1StateMachineBody ||
                    frame.BoxedStateMachineMethod != MethodIndex.Invalid;
                bool hasDispatchBoundary =
                    boundary.IsDispatchMarker ||
                    boundary.Kind == AsyncProfilerCallStackStitchBoundaryKind.V1DispatcherInfrastructure;
                if (hasStateMachineFrame || hasDispatchBoundary)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Matches repeated V1 method bodies and their completion helpers as a group.
        /// Optionally preserves the leaf body and its callees without assigning individual captured slots.
        /// </summary>
        private static bool TryMatchRecursiveV1Unwind(AsyncProfilerCallStackTransformState context, int start, int end,
            AsyncProfilerCallStack callStack, int completed, bool preserveLeaf, out int unwindStart)
        {
            unwindStart = start;
            if (!TryGetRecursiveV1Method(context, callStack, completed, out MethodIndex recursiveMethod))
            {
                return false;
            }

            int matchedBodyCount = 0;
            bool hasEntryBox = false;
            ModuleFileIndex runtimeModule = ModuleFileIndex.Invalid;
            AsyncProfilerRuntimeSupport runtimeSupport = null;

            // Follow one contiguous return span belonging to this call stack.
            for (int i = start; i < end; i++)
            {
                AsyncProfilerStitchedFrame frame = context.Frames[i];
                bool belongsToCallStack =
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
                    frame.ContextKind == AsyncProfilerStitchedFrameContextKind.Active &&
                    ReferenceEquals(frame.Context, callStack);
                if (!belongsToCallStack)
                {
                    return false;
                }

                bool isStateMachineBody = frame.SyncFrameKind == AsyncProfilerStitchSyncFrameKind.V1StateMachineBody;
                AsyncProfilerRuntimeSupport support = context.GetRuntimeSupport(frame.CodeAddress);
                bool isCompletionHelper = support.IsV1CompletionUnwindInfrastructure(frame, context.ClassifyBoundary(frame.CodeAddress).Kind);
                bool isStateMachineBox = frame.BoxedStateMachineMethod != MethodIndex.Invalid;
                bool hasUnresolvedIdentity = frame.CodeAddress == CodeAddressIndex.Invalid || frame.Method == MethodIndex.Invalid;
                bool hasMismatchedStateMachineBody = isStateMachineBody && frame.Method != recursiveMethod;
                bool hasMismatchedStateMachineBox = !isStateMachineBody && isStateMachineBox && frame.BoxedStateMachineMethod != recursiveMethod;
                bool isUnexpectedHelper = !isStateMachineBody && !isCompletionHelper;
                bool isUnwindFrame =
                    !hasUnresolvedIdentity &&
                    !hasMismatchedStateMachineBody &&
                    !isUnexpectedHelper &&
                    !hasMismatchedStateMachineBox;
                if (!isUnwindFrame)
                {
                    // Preserve unrelated callees before the first body, but reject gaps after it.
                    if (matchedBodyCount != 0)
                    {
                        return false;
                    }
                    unwindStart = i + 1;
                    runtimeModule = ModuleFileIndex.Invalid;
                    runtimeSupport = null;
                    hasEntryBox = false;
                    continue;
                }

                if (isStateMachineBody)
                {
                    bool shouldPreserveLeafBody = preserveLeaf && matchedBodyCount == 0;
                    if (shouldPreserveLeafBody)
                    {
                        // Keep the leaf body and its callees; this copy need not have completed.
                        bool hasPrecedingCompletionHelper =
                            i > unwindStart &&
                            context.Frames[i - 1].SyncFrameKind == AsyncProfilerStitchSyncFrameKind.V1MethodCompletion;
                        if (!hasPrecedingCompletionHelper)
                        {
                            return false;
                        }

                        matchedBodyCount++;
                        unwindStart = i + 1;
                        hasEntryBox = false;
                        continue;
                    }

                    // Match completed copies as a group, without inferring their individual captured slots.
                    bool hasMatchingCompletionEntry =
                        hasEntryBox &&
                        i > unwindStart &&
                        context.Frames[i - 1].SyncFrameKind == AsyncProfilerStitchSyncFrameKind.V1MethodCompletion;
                    if (!hasMatchingCompletionEntry)
                    {
                        return false;
                    }

                    bool hasMatchingCompletionPair = FindV1CompletionPairBeforeBody(context, unwindStart, i) >= 0;
                    if (!hasMatchingCompletionPair)
                    {
                        return false;
                    }

                    // The preserved leaf counts as a physical body, not an additional completion.
                    matchedBodyCount++;
                    bool exceedsExpectedBodyCount = matchedBodyCount > completed + (preserveLeaf ? 1 : 0);
                    if (exceedsExpectedBodyCount)
                    {
                        return false;
                    }

                    hasEntryBox = false;
                    continue;
                }

                ModuleFileIndex owner = context.GetHostModuleOwner(frame.CodeAddress);
                bool hasResolvedRuntimeOwner = owner != ModuleFileIndex.Invalid;
                bool hasConflictingRuntime =
                    hasResolvedRuntimeOwner &&
                    runtimeModule != ModuleFileIndex.Invalid &&
                    (owner != runtimeModule || !ReferenceEquals(support, runtimeSupport));
                if (!hasResolvedRuntimeOwner || hasConflictingRuntime)
                {
                    return false;
                }

                // Helpers for every matched copy must share the same runtime owner and support.
                runtimeModule = owner;
                runtimeSupport = support;
                hasEntryBox |= isStateMachineBox && frame.SyncFrameKind == AsyncProfilerStitchSyncFrameKind.ProfilerHelper;
            }

            // Require two completed bodies, plus the preserved leaf when requested.
            int requiredBodyCount = preserveLeaf ? 3 : 2;
            return matchedBodyCount >= requiredBodyCount;
        }

        /// <summary>
        /// Checks for at least two exact normal completions in a call stack whose captured frames
        /// all identify the same method, rooted in a recognized V1 state machine.
        /// </summary>
        private static bool TryGetRecursiveV1Method(AsyncProfilerCallStackTransformState context,
            AsyncProfilerCallStack callStack, int completed, out MethodIndex recursiveMethod)
        {
            recursiveMethod = MethodIndex.Invalid;
            AsyncProfilerCallStackFrames captured = callStack.Frames;
            bool hasEnoughCompletedFrames = captured.FrameCount >= 2 && completed >= 2 && completed <= captured.FrameCount;
            if (!hasEnoughCompletedFrames)
            {
                return false;
            }

            bool hasExactNormalCompletion =
                callStack.GetMethodCompletedFrameCount(context.TimestampQpc) == completed &&
                callStack.GetExceptionCompletedFrameCount(context.TimestampQpc) == 0;
            if (!hasExactNormalCompletion)
            {
                return false;
            }

            MethodIndex method = context.GetMethod(captured.CodeAddressAt(0));
            if (method == MethodIndex.Invalid)
            {
                return false;
            }

            for (int i = 1; i < captured.FrameCount; i++)
            {
                if (context.GetMethod(captured.CodeAddressAt(i)) != method)
                {
                    return false;
                }
            }

            if (!IsRecognizedV1RootMethod(context.TraceLog, captured.CodeAddressAt(captured.FrameCount - 1)))
            {
                return false;
            }

            recursiveMethod = method;
            return true;
        }

        /// <summary>
        /// Matches valid method indices, including code versions with the same module, metadata token, and full name.
        /// </summary>
        private static bool SameMethodIdentity(TraceMethods methods, MethodIndex left, MethodIndex right)
        {
            if (left == right)
            {
                return left != MethodIndex.Invalid;
            }

            if (left == MethodIndex.Invalid || right == MethodIndex.Invalid)
            {
                return false;
            }

            TraceMethod first = methods[left];
            TraceMethod second = methods[right];

            bool hasMatchingMethodToken = first.MethodToken != 0 && first.MethodToken == second.MethodToken;
            bool hasMatchingModule = hasMatchingMethodToken && first.MethodModuleFileIndex == second.MethodModuleFileIndex;
            bool hasMatchingFullName = hasMatchingModule && string.Equals(first.FullMethodName, second.FullMethodName, StringComparison.Ordinal);

            return hasMatchingFullName;
        }

        /// <summary>
        /// Removes verified V2 completion helpers that resumed a V1 continuation inline.
        /// Preserves the V1 logical callers and the physical caller after the handoff.
        /// </summary>
        private static void CollapseV2CompletionHandoff(AsyncProfilerCallStackTransformState context, int currentIndex)
        {
            bool canVerifyHandoff = context.TraceLog != null && context.StitchingState.HasConsistentPhysicalAlignment;
            if (!canVerifyHandoff)
            {
                return;
            }

            List<AsyncProfilerStitchedFrame> frames = context.Frames;
            AsyncProfilerStitchedFrame current = frames[currentIndex];
            bool hasResolvedV1CurrentFrame =
                current.Context != null &&
                current.Context.Frames.Kind == AsyncCallstackKind.StateMachineAsync &&
                current.Method != MethodIndex.Invalid && current.CodeAddress != CodeAddressIndex.Invalid;
            if (!hasResolvedV1CurrentFrame)
            {
                return;
            }

            // Skip the V1 logical callers; the completion helpers must follow them.
            int handoffStart = currentIndex + 1;
            while (handoffStart < frames.Count &&
                   frames[handoffStart].Origin == AsyncProfilerStitchedFrameOrigin.AsyncCaller &&
                   ReferenceEquals(frames[handoffStart].Context, current.Context))
            {
                handoffStart++;
            }

            // Match the V2 completion helpers without including their caller.
            AsyncProfilerRuntimeSequenceMatch handoff = context.MatchRuntimeSequence(AsyncProfilerRuntimeSequenceKind.V2CompletionHandoff, handoffStart, frames.Count);
            int handoffCount = handoff.Length;
            bool hasHandoffWithFollowingCaller = handoff.IsMatch && handoffCount < frames.Count - handoffStart;
            if (!hasHandoffWithFollowingCaller)
            {
                return;
            }

            // The helpers and their caller must be physical and unassigned to any async call stack.
            bool isUnassignedPhysicalSpan = true;
            for (int i = handoffStart; i <= handoffStart + handoffCount; i++)
            {
                bool isUnassignedPhysicalFrame = frames[i].Origin == AsyncProfilerStitchedFrameOrigin.Sync && frames[i].Context == null;
                if (!isUnassignedPhysicalFrame)
                {
                    isUnassignedPhysicalSpan = false;
                    break;
                }
            }

            if (!isUnassignedPhysicalSpan)
            {
                return;
            }

            // Keep the following caller, and reject it if unresolved or another async boundary.
            AsyncProfilerStitchedFrame caller = frames[handoffStart + handoffCount];
            bool hasResolvedCaller = caller.Method != MethodIndex.Invalid && caller.CodeAddress != CodeAddressIndex.Invalid;
            bool hasNonBoundaryCaller =
                hasResolvedCaller &&
                context.ClassifyBoundary(caller.CodeAddress).Kind == AsyncProfilerCallStackStitchBoundaryKind.None;
            if (!hasNonBoundaryCaller)
            {
                return;
            }

            // The completed V2 dispatcher is physical only; never consume a bridge belonging to a live segment.
            frames.RemoveRange(handoffStart, handoffCount);
            context.Diagnostics.V2CompletionHandoffFramesCollapsed += handoffCount;
        }

        /// <summary>
        /// Removes duplicate V1 startup frames from a synchronously reentrant call.
        /// Keeps the resumed frame and its current or dispatch-target role.
        /// </summary>
        private static bool TryApplyReentrantV1Deduplication(AsyncProfilerCallStackTransformState context, int currentIndex)
        {
            if (context.TraceLog == null)
            {
                return false;
            }

            List<AsyncProfilerStitchedFrame> frames = context.Frames;
            AsyncProfilerStitchedFrame current = frames[currentIndex];
            bool hasCapturedV1Frames = current.Segment != null && current.Segment.Kind == AsyncCallstackKind.StateMachineAsync;
            if (!hasCapturedV1Frames)
            {
                return false;
            }

            // Targets without a box carrier need their method resolved from the captured frame.
            bool isDispatchTargetWithoutCarrier = current.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget && !current.IsStateMachineBoxCarrier;
            MethodIndex currentMethod = current.IsStateMachineBoxCarrier ? current.BoxedStateMachineMethod : current.Method;
            if (isDispatchTargetWithoutCarrier)
            {
                CodeAddressIndex targetAddress = current.Segment.CodeAddressAt(current.SegmentFrameIndex);
                currentMethod = context.GetMethod(targetAddress);
            }

            if (currentMethod == MethodIndex.Invalid)
            {
                return false;
            }

            // Find the await-registration helpers and the physical MoveNext immediately after them.
            int registrationStart = currentIndex + 1;
            AsyncProfilerRuntimeSequenceMatch registration = context.MatchRuntimeSequence(AsyncProfilerRuntimeSequenceKind.V1AwaitRegistration, registrationStart, frames.Count);
            int startupMoveNextIndex = registrationStart + registration.Length;
            bool hasRegistrationWithFollowingFrame = registration.IsMatch && startupMoveNextIndex < frames.Count;
            if (!hasRegistrationWithFollowingFrame)
            {
                return false;
            }

            AsyncProfilerStitchedFrame startupMoveNext = frames[startupMoveNextIndex];
            bool hasPhysicalV1StartupBody =
                startupMoveNext.Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
                startupMoveNext.SyncFrameKind == AsyncProfilerStitchSyncFrameKind.V1StateMachineBody &&
                startupMoveNext.CodeAddress != CodeAddressIndex.Invalid;
            if (!hasPhysicalV1StartupBody)
            {
                return false;
            }

            // Use the same resolver for captured and physical code versions when matching a target.
            MethodIndex startupMethodIdentity = isDispatchTargetWithoutCarrier ? context.GetMethod(startupMoveNext.CodeAddress) : startupMoveNext.Method;
            if (startupMethodIdentity != currentMethod)
            {
                return false;
            }

            // Match the startup helpers and the kickoff for that same V1 method.
            AsyncProfilerRuntimeSequenceMatch startup = context.MatchRuntimeSequence(AsyncProfilerRuntimeSequenceKind.V1Startup, startupMoveNextIndex, frames.Count);
            int kickoffIndex = startupMoveNextIndex + startup.Length;
            bool hasStartupWithKickoff = startup.IsMatch && startup.Length > 1 && kickoffIndex < frames.Count;
            bool hasMatchingKickoff = hasStartupWithKickoff && IsMatchingV1Kickoff(context.TraceLog, startupMoveNext, frames[kickoffIndex]);
            if (!hasMatchingKickoff)
            {
                return false;
            }

            // A target without a box carrier also needs a verified nested dispatch relationship.
            bool hasRequiredDispatchEvidence =
                !isDispatchTargetWithoutCarrier ||
                IsVerifiedReentrantDispatchTarget(context, currentIndex, kickoffIndex, startupMoveNext.Method);
            if (!hasRequiredDispatchEvidence)
            {
                return false;
            }

            // Keep the resumed representation and its current/dispatch-target role. The exact match proves that
            // the registration helpers, duplicate startup MoveNext, builder Start frames, and kickoff all belong
            // to the same reentrant call stack, so collapse that complete generated span.
            int duplicateFrameCount = kickoffIndex - registrationStart + 1;
            frames.RemoveRange(registrationStart, duplicateFrameCount);
            context.Diagnostics.V1ReentrantFramesCollapsed += duplicateFrameCount;
            return true;
        }

        /// <summary>
        /// Verifies a nested V1 dispatch target against its dispatcher and enclosing running method.
        /// Requires exactly one matching startup method in the intervening physical frames.
        /// </summary>
        private static bool IsVerifiedReentrantDispatchTarget(AsyncProfilerCallStackTransformState context,
            int targetIndex, int kickoffIndex, MethodIndex startupMethod)
        {
            bool hasSurroundingFrames = targetIndex != 0 && kickoffIndex + 1 < context.Frames.Count;
            if (!hasSurroundingFrames)
            {
                return false;
            }

            bool hasUnambiguousStitching =
                context.StitchingState.ProcessedCallStackCount == context.Segments.Count &&
                context.StitchingState.HasConsistentPhysicalAlignment &&
                context.StitchingState.UnresolvedV1CompletionCount == 0;
            if (!hasUnambiguousStitching)
            {
                return false;
            }

            List<AsyncProfilerStitchedFrame> frames = context.Frames;
            AsyncProfilerStitchedFrame target = frames[targetIndex];
            AsyncProfilerStitchedFrame dispatcher = frames[targetIndex - 1];
            AsyncProfilerStitchedFrame enclosingCurrent = frames[kickoffIndex + 1];

            // Verify that the physical dispatcher belongs to the target's call stack.
            bool hasMatchingDispatcherContext =
                target.Context != null &&
                dispatcher.Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
                ReferenceEquals(dispatcher.Context, target.Context);
            bool hasVerifiedDispatcher =
                hasMatchingDispatcherContext &&
                context.ClassifyBoundary(dispatcher.CodeAddress).Kind == AsyncProfilerCallStackStitchBoundaryKind.V1Dispatcher &&
                IsResolvedRuntimeSpan(context, frames, targetIndex - 1, 1);
            if (!hasVerifiedDispatcher)
            {
                return false;
            }

            // The startup path must belong to a different running method one nesting level above the target.
            bool hasDistinctEnclosingCurrent =
                enclosingCurrent.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent &&
                enclosingCurrent.Context != null &&
                enclosingCurrent.CodeAddress != CodeAddressIndex.Invalid &&
                enclosingCurrent.Method != MethodIndex.Invalid &&
                enclosingCurrent.Method != startupMethod;
            if (!hasDistinctEnclosingCurrent)
            {
                return false;
            }

            bool hasExpectedNesting = target.Context.Depth == enclosingCurrent.Context.Depth + 1;
            if (!hasExpectedNesting)
            {
                return false;
            }

            // Keep the full startup span in the enclosing call stack, without another dispatcher.
            int matchingStartupFrames = 0;
            for (int i = targetIndex + 1; i <= kickoffIndex; i++)
            {
                AsyncProfilerStitchedFrame frame = frames[i];
                bool belongsToEnclosingCallStack =
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
                    frame.Method != MethodIndex.Invalid &&
                    ReferenceEquals(frame.Context, enclosingCurrent.Context);
                bool hasInterveningDispatcher =
                    belongsToEnclosingCallStack &&
                    context.ClassifyBoundary(frame.CodeAddress).IsDispatchMarker;

                if (!belongsToEnclosingCallStack || hasInterveningDispatcher)
                {
                    return false;
                }

                if (frame.Method == startupMethod)
                {
                    matchingStartupFrames++;
                }
            }

            // Repeated startup-method frames would make the duplicate body ambiguous.
            return matchingStartupFrames == 1;
        }

        /// <summary>Checks that the captured root frame is a recognized V1 state-machine method.</summary>
        private static bool IsRecognizedV1RootMethod(TraceLog traceLog, CodeAddressIndex address)
        {
            string methodName = AsyncProfilerMethodNames.NormalizeMethodName(traceLog.CodeAddresses[address].FullMethodName);
            return AsyncProfilerMethodNames.TryGetLogicalStateMachineMethodName(methodName, out _);
        }

        /// <summary>
        /// Matches a physical V1 kickoff frame to its state-machine MoveNext frame.
        /// </summary>
        private static bool IsMatchingV1Kickoff(TraceLog traceLog,
            AsyncProfilerStitchedFrame stateMachineMoveNext, AsyncProfilerStitchedFrame kickoff)
        {
            bool canMatchKickoff =
                kickoff.Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
                stateMachineMoveNext.CodeAddress != CodeAddressIndex.Invalid &&
                kickoff.CodeAddress != CodeAddressIndex.Invalid;
            if (!canMatchKickoff)
            {
                return false;
            }

            TraceCodeAddress moveNextCodeAddress = traceLog.CodeAddresses[stateMachineMoveNext.CodeAddress];
            TraceCodeAddress kickoffCodeAddress = traceLog.CodeAddresses[kickoff.CodeAddress];
            bool hasMatchingModuleName = string.Equals(moveNextCodeAddress.ModuleName, kickoffCodeAddress.ModuleName, StringComparison.OrdinalIgnoreCase);
            if (!hasMatchingModuleName)
            {
                return false;
            }

            // Match the logical method name rather than the generated MoveNext name.
            string normalizedMethodName = AsyncProfilerMethodNames.NormalizeMethodName(moveNextCodeAddress.FullMethodName);
            bool hasLogicalMethodName = AsyncProfilerMethodNames.TryGetLogicalStateMachineMethodName(normalizedMethodName, out string logicalMethod);
            if (!hasLogicalMethodName)
            {
                return false;
            }

            string kickoffMethod = AsyncProfilerMethodNames.NormalizeMethodName(kickoffCodeAddress.FullMethodName);
            if (string.IsNullOrEmpty(kickoffMethod))
            {
                return false;
            }

            // Compare names without parameters, using dots for nested types.
            kickoffMethod = AsyncProfilerMethodNames.GetMethodNameWithoutParameters(kickoffMethod);
            kickoffMethod = kickoffMethod.Replace('+', '.');

            bool hasMatchingMethodName = string.Equals(logicalMethod, kickoffMethod, StringComparison.Ordinal);
            return hasMatchingMethodName;
        }

        #endregion
    }
}
