// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;

using static Microsoft.Diagnostics.Tracing.Computers.AsyncProfilerCallStackTransformHelpers;

namespace Microsoft.Diagnostics.Tracing.Computers
{
    /// <summary>
    /// Removes verified runtime helpers between an enclosing async method and its nested call stack.
    /// </summary>
    /// <remarks>
    /// Supports V1 and V2. The enclosing method must be physically running; the nested
    /// call stack must have a verified running method or dispatch target.
    /// Only removes physical frames from the enclosing call stack that resolve to one
    /// runtime. Logical frames and captured states remain unchanged.
    /// </remarks>
    internal sealed class AsyncProfilerReentrantHandoffTransformPass
    {
        /// <summary>Removes verified handoff spans while preserving the async frames on both sides.</summary>
        public void Apply(AsyncProfilerCallStackTransformState context)
        {
            bool canVerifyHandoff =
                context.TraceLog != null &&
                context.StitchingState.HasConsistentPhysicalAlignment &&
                context.StitchingState.UnresolvedV1CompletionCount == 0;
            if (!canVerifyHandoff)
            {
                return;
            }

            List<AsyncProfilerStitchedFrame> frames = context.Frames;
            for (int currentIndex = 0; currentIndex < frames.Count; currentIndex++)
            {
                AsyncProfilerStitchedFrame enclosingCurrent = frames[currentIndex];
                bool hasResolvedEnclosingCurrent =
                    enclosingCurrent.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent &&
                    enclosingCurrent.Context != null &&
                    enclosingCurrent.Method != MethodIndex.Invalid &&
                    enclosingCurrent.CodeAddress != CodeAddressIndex.Invalid;
                if (!hasResolvedEnclosingCurrent)
                {
                    continue;
                }

                // Collect adjacent physical frames from the enclosing call stack.
                int handoffStart = currentIndex;
                while (handoffStart > 0 &&
                       frames[handoffStart - 1].Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
                       ReferenceEquals(frames[handoffStart - 1].Context, enclosingCurrent.Context))
                {
                    handoffStart--;
                }

                int handoffFrameCount = currentIndex - handoffStart;
                bool hasHandoffWithPrecedingFrame = handoffFrameCount != 0 && handoffStart != 0;
                if (!hasHandoffWithPrecedingFrame)
                {
                    continue;
                }

                // Require a directly nested call stack with a verified method or dispatch target.
                AsyncProfilerCallStack nestedCallStack = frames[handoffStart - 1].Context;
                bool hasDirectNestedCallStack = nestedCallStack != null && nestedCallStack.Depth == enclosingCurrent.Context.Depth + 1;
                bool hasVerifiedNestedAnchor = hasDirectNestedCallStack && HasVerifiedAsyncAnchor(context, handoffStart - 1, nestedCallStack);
                bool canRemoveHandoff = hasVerifiedNestedAnchor && IsResolvedRuntimeSpan(context, frames, handoffStart, handoffFrameCount);
                if (!canRemoveHandoff)
                {
                    continue;
                }

                frames.RemoveRange(handoffStart, handoffFrameCount);
                context.Diagnostics.AsyncReentrantHandoffFramesCollapsed += handoffFrameCount;
                bool isV2ToV2Handoff =
                    nestedCallStack.Frames.Kind == AsyncCallstackKind.RuntimeAsync &&
                    enclosingCurrent.Context.Frames.Kind == AsyncCallstackKind.RuntimeAsync;
                if (isV2ToV2Handoff)
                {
                    context.Diagnostics.V2ReentrantHandoffFramesCollapsed += handoffFrameCount;
                }

                // The enclosing method moved to handoffStart; resume with the following frame.
                currentIndex = handoffStart;
            }
        }
    }
}
