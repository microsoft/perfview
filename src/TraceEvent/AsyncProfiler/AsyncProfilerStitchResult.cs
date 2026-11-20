// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using Microsoft.Diagnostics.Tracing.Etlx;

namespace Microsoft.Diagnostics.Tracing.Computers
{
    /// <summary>
    /// The output range and dispatch-boundary metadata for one active async call stack.
    /// </summary>
    internal readonly struct AsyncProfilerCallStackPlacement
    {
        internal AsyncProfilerCallStackPlacement(AsyncProfilerCallStack callStack,
            int leafIndex, int rootIndexExclusive, CodeAddressIndex instrumentedDispatch = CodeAddressIndex.Invalid,
            CodeAddressIndex dispatch = CodeAddressIndex.Invalid)
        {
            CallStack = callStack;
            LeafIndex = leafIndex;
            RootIndexExclusive = rootIndexExclusive;
            InstrumentedDispatch = instrumentedDispatch;
            Dispatch = dispatch;
        }

        internal AsyncProfilerCallStack CallStack { get; }

        internal int LeafIndex { get; }

        internal int RootIndexExclusive { get; }

        // Retain the consumed V1 dispatcher or V2 pair for lazy verification, without enlarging every physical frame.
        internal CodeAddressIndex InstrumentedDispatch { get; }

        internal CodeAddressIndex Dispatch { get; }
    }

    /// <summary>The merged frames (leaf-to-root), stitching state, and diagnostics from a stitch.</summary>
    internal sealed class AsyncProfilerStitchResult
    {
        /// <summary>Creates a result retaining the supplied frames, stitching state, and diagnostics without copying them.</summary>
        public AsyncProfilerStitchResult(IReadOnlyList<AsyncProfilerStitchedFrame> frames,
            AsyncProfilerCallStackDiagnostics diagnostics, AsyncProfilerCallStackStitchingState stitchingState)
            : this(frames, diagnostics, stitchingState, Array.Empty<AsyncProfilerCallStackPlacement>())
        {
        }

        internal AsyncProfilerStitchResult(IReadOnlyList<AsyncProfilerStitchedFrame> frames,
            AsyncProfilerCallStackDiagnostics diagnostics,
            AsyncProfilerCallStackStitchingState stitchingState,
            IReadOnlyList<AsyncProfilerCallStackPlacement> segmentPlacements)
        {
            Frames = frames ?? throw new ArgumentNullException(nameof(frames));
            Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
            StitchingState = stitchingState ?? throw new ArgumentNullException(nameof(stitchingState));
            SegmentPlacements = segmentPlacements ?? throw new ArgumentNullException(nameof(segmentPlacements));
        }

        /// <summary>The stitched frames, leaf-to-root (from the native leaf to the thread/process root).</summary>
        public IReadOnlyList<AsyncProfilerStitchedFrame> Frames { get; }

        /// <summary>
        /// Counters and bounded messages for stitching, fallbacks, and any subsequent presentation transforms.
        /// </summary>
        public AsyncProfilerCallStackDiagnostics Diagnostics { get; }

        /// <summary>Matching results used by subsequent presentation passes, independent of reporting.</summary>
        internal AsyncProfilerCallStackStitchingState StitchingState { get; }

        internal IReadOnlyList<AsyncProfilerCallStackPlacement> SegmentPlacements { get; }
    }
}
