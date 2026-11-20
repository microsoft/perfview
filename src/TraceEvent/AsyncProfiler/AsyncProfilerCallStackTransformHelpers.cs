// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;

namespace Microsoft.Diagnostics.Tracing.Computers
{
    /// <summary>
    /// Shared frame checks and dispatcher-marker edits for the transform passes.
    /// </summary>
    internal static class AsyncProfilerCallStackTransformHelpers
    {
        /// <summary>
        /// Replaces a verified helper span with one dispatcher marker, keeping the first frame's identity and ownership.
        /// </summary>
        /// <returns>The number of removed list entries; one entry remains as the dispatcher marker.</returns>
        internal static int CollapseMachinerySpan(List<AsyncProfilerStitchedFrame> frames, int start, int count)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            frames[start] = frames[start].WithPresentation(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames);
            frames.RemoveRange(start + 1, count - 1);
            return count - 1;
        }

        /// <summary>
        /// Checks that physical frames share a call-stack context and runtime support.
        /// Also requires the same resolved runtime owner when trace metadata is available.
        /// </summary>
        internal static bool HasSameMachineryOwner(AsyncProfilerCallStackTransformState context, int start, int count)
        {
            AsyncProfilerStitchedFrame first = context.Frames[start];
            AsyncProfilerRuntimeSupport support = context.GetRuntimeSupport(first.CodeAddress);
            ModuleFileIndex owner = context.GetHostModuleOwner(first.CodeAddress);
            for (int i = start; i < start + count; i++)
            {
                AsyncProfilerStitchedFrame frame = context.Frames[i];
                bool hasMatchingPhysicalContext =
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
                    ReferenceEquals(first.Context, frame.Context);
                bool hasMatchingRuntimeSupport =
                    hasMatchingPhysicalContext &&
                    ReferenceEquals(support, context.GetRuntimeSupport(frame.CodeAddress));
                if (!hasMatchingRuntimeSupport)
                {
                    return false;
                }

                // Without trace metadata, only the context and support can be checked.
                if (context.TraceLog != null)
                {
                    bool hasMatchingRuntimeOwner =
                        owner != ModuleFileIndex.Invalid &&
                        owner == context.GetHostModuleOwner(frame.CodeAddress);
                    if (!hasMatchingRuntimeOwner)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Checks for a verified current method or dispatch target in the call stack's contiguous frame span.
        /// </summary>
        internal static bool HasVerifiedAsyncAnchor(AsyncProfilerCallStackTransformState context,
            int contextEnd, AsyncProfilerCallStack callStack)
        {
            List<AsyncProfilerStitchedFrame> frames = context.Frames;
            bool hasVerifiedAnchor = false;

            // Scan the full span: a completed V1 body invalidates any anchor already found.
            for (int i = contextEnd; i >= 0 && ReferenceEquals(frames[i].Context, callStack); i--)
            {
                AsyncProfilerStitchedFrame frame = frames[i];
                if (frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody)
                {
                    return false;
                }

                bool hasResolvedCurrentMethod =
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent &&
                    frame.Method != MethodIndex.Invalid && frame.CodeAddress != CodeAddressIndex.Invalid;
                hasVerifiedAnchor |= hasResolvedCurrentMethod;
                if (frame.Origin != AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget)
                {
                    continue;
                }

                // Dispatch targets need a resolved captured method, not just a physical helper address.
                CodeAddressIndex targetAddress = frame.Segment.CodeAddressAt(frame.SegmentFrameIndex);
                bool hasResolvedDispatchTarget =
                    targetAddress != CodeAddressIndex.Invalid &&
                    context.TraceLog.CodeAddresses.MethodIndex(targetAddress) != MethodIndex.Invalid;
                if (!hasResolvedDispatchTarget)
                {
                    continue;
                }

                if (callStack.Frames.Kind == AsyncCallstackKind.RuntimeAsync)
                {
                    // V2 identifies the target directly through its captured native address.
                    hasVerifiedAnchor = true;
                }
                else if (frame.IsStateMachineBoxCarrier)
                {
                    // A V1 box must also be a resolved runtime frame.
                    hasVerifiedAnchor |= IsResolvedRuntimeSpan(context, frames, i, 1);
                }
                else
                {
                    // Other V1 targets need matching completion progress and an adjacent owned dispatcher.
                    bool hasMatchingCompletionProgress =
                        context.TraceLog.AsyncProfilerCallStacks?.MethodCompletionObserved(callStack) == true &&
                        callStack.GetCompletedFrameCount(context.TimestampQpc) == frame.SegmentFrameIndex;
                    bool hasMatchingDispatcherContext =
                        hasMatchingCompletionProgress && i > 0 &&
                        frames[i - 1].Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
                        ReferenceEquals(frames[i - 1].Context, callStack);
                    bool hasPrecedingDispatcher =
                        hasMatchingDispatcherContext &&
                        context.ClassifyBoundary(frames[i - 1].CodeAddress).Kind == AsyncProfilerCallStackStitchBoundaryKind.V1Dispatcher;
                    if (hasPrecedingDispatcher)
                    {
                        hasVerifiedAnchor |= IsResolvedRuntimeSpan(context, frames, i - 1, 1);
                    }
                }
            }

            return hasVerifiedAnchor;
        }

        /// <summary>
        /// Checks that every frame has a resolved method and the same runtime owner and support.
        /// </summary>
        internal static bool IsResolvedRuntimeSpan(AsyncProfilerCallStackTransformState context,
            List<AsyncProfilerStitchedFrame> frames, int start, int count)
        {
            if (context.TraceLog == null)
            {
                return false;
            }

            ModuleFileIndex owner = context.GetHostModuleOwner(frames[start].CodeAddress);
            AsyncProfilerRuntimeSupport support = context.GetRuntimeSupport(frames[start].CodeAddress);
            int end = start + count;
            for (int i = start; i < end; i++)
            {
                CodeAddressIndex codeAddressIndex = frames[i].CodeAddress;
                bool hasResolvedRuntimeFrame =
                    frames[i].Method != MethodIndex.Invalid &&
                    codeAddressIndex != CodeAddressIndex.Invalid &&
                    owner != ModuleFileIndex.Invalid;
                bool hasMatchingRuntime =
                    hasResolvedRuntimeFrame &&
                    context.GetHostModuleOwner(codeAddressIndex) == owner &&
                    ReferenceEquals(context.GetRuntimeSupport(codeAddressIndex), support);
                if (!hasMatchingRuntime)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Checks that a physical frame has the requested classification.</summary>
        internal static bool IsSyncFrameKind(AsyncProfilerStitchedFrame frame, AsyncProfilerStitchSyncFrameKind kind) =>
            frame.Origin == AsyncProfilerStitchedFrameOrigin.Sync && frame.SyncFrameKind == kind;
    }
}
