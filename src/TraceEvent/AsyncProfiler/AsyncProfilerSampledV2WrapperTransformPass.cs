// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.Diagnostics.Tracing.Computers
{
    /// <summary>
    /// Removes a sampled V2 continuation wrapper when stitching kept the physical stack
    /// or the next frame is a dispatch target from the same async call stack.
    /// Other frames are left unchanged.
    /// </summary>
    internal sealed class AsyncProfilerSampledV2WrapperTransformPass
    {
        public void Apply(AsyncProfilerCallStackTransformState context)
        {
            List<AsyncProfilerStitchedFrame> frames = context.Frames;
            if (frames.Count == 0)
            {
                return;
            }

            bool isSampledV2Wrapper =
                frames[0].Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
                context.ClassifyBoundary(frames[0].CodeAddress).Kind == AsyncProfilerCallStackStitchBoundaryKind.V2Dispatcher;

            bool hasMatchingDispatchTarget =
                frames.Count > 1 && frames[1].Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget &&
                frames[0].Context != null && ReferenceEquals(frames[0].Context, frames[1].Context);

            if (isSampledV2Wrapper && (context.StitchingState.UsesV2SynchronousLayout || hasMatchingDispatchTarget))
            {
                frames.RemoveAt(0);
                context.Diagnostics.V2LeafWrapperDropped++;
            }
        }
    }
}
