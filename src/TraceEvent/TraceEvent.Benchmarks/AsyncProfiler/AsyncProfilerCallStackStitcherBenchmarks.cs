using System;
using System.Collections.Generic;

using BenchmarkDotNet.Attributes;

using Microsoft.Diagnostics.Tracing.Computers;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;

namespace TraceEventBenchmarks
{
    /// <summary>Measures structural stitching with synthetic frame identities, without trace conversion or stack-source emission.</summary>
    /// <remarks>Call-stack lookup and output-frame interning are excluded.</remarks>
    [MemoryDiagnoser]
    public class AsyncProfilerCallStackStitcherBenchmarks
    {
        /// <summary>Boundary layout used to compare V1, V2, nested, and missing-boundary reconstruction paths.</summary>
        [Params(
            AsyncProfilerStitchBenchmarkScenario.RuntimeAsync,
            AsyncProfilerStitchBenchmarkScenario.StateMachineAsync,
            AsyncProfilerStitchBenchmarkScenario.MixedNested,
            AsyncProfilerStitchBenchmarkScenario.BoundaryNotFound)]
        public AsyncProfilerStitchBenchmarkScenario Scenario { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _input = new AsyncProfilerSyntheticStitchInput();
            _output = new List<AsyncProfilerStitchedFrame>(32);
            // Production caps diagnostic messages trace-wide (AsyncProfilerCallStackSourceBuilderCore sets MessageLimit to the
            // remaining capacity), so steady-state samples only count failures. Disable messages so BoundaryNotFound does
            // not time message formatting.
            _diagnostics = new AsyncProfilerCallStackDiagnostics { MessageLimit = 0 };
            _stitchingState = new AsyncProfilerCallStackStitchingState();

            switch (Scenario)
            {
                case AsyncProfilerStitchBenchmarkScenario.RuntimeAsync:
                    _sync = _input.RuntimeSync(100, firstAsyncMethod: 200, wrapperIndex: 3);
                    _segments = new[] { Segment(AsyncCallstackKind.RuntimeAsync, 200, 6, depth: 0) };
                    break;

                case AsyncProfilerStitchBenchmarkScenario.StateMachineAsync:
                    _sync = _input.StateMachineSync(100, 200);
                    _segments = new[] { Segment(AsyncCallstackKind.StateMachineAsync, 200, 6, depth: 0) };
                    break;

                case AsyncProfilerStitchBenchmarkScenario.MixedNested:
                    var sync = new List<AsyncProfilerStitchSyncFrame>();
                    sync.AddRange(_input.StateMachineSync(100, 200));
                    sync.Add(_input.Sync(130, 90));
                    sync.AddRange(_input.RuntimeSync(140, firstAsyncMethod: 300, wrapperIndex: 0));
                    _sync = sync;
                    _segments = new[]
                    {
                        Segment(AsyncCallstackKind.StateMachineAsync, 200, 6, depth: 1),
                        Segment(AsyncCallstackKind.RuntimeAsync, 300, 6, depth: 0),
                    };
                    break;

                case AsyncProfilerStitchBenchmarkScenario.BoundaryNotFound:
                    _sync = _input.SyncFrames(16, 100);
                    _segments = new[] { Segment(AsyncCallstackKind.RuntimeAsync, 200, 6, depth: 0) };
                    break;

                default:
                    throw new InvalidOperationException("Unknown benchmark scenario.");
            }

            StitchIntoReusableBuffers();
            int expectedMissingBoundaries = Scenario == AsyncProfilerStitchBenchmarkScenario.BoundaryNotFound ? 1 : 0;
            if (_output.Count == 0 || _diagnostics.SegmentsProcessed != _segments.Count ||
                _diagnostics.BoundariesNotFound != expectedMissingBoundaries ||
                _diagnostics.AdjacencyMismatches != 0 || _diagnostics.V1CompletionAlignmentAmbiguous != 0)
            {
                throw new InvalidOperationException("The stitcher benchmark did not exercise the expected reconstruction path.");
            }
        }

        /// <summary>Stitches one physical/async stack into reused output buffers for the selected boundary layout.</summary>
        [Benchmark]
        public int StitchIntoReusableBuffers()
        {
            AsyncProfilerCallStackStitcher.StitchInto(
                _sync, _segments, Qpc, _input.Classify, _input.MethodCompletionObserved,
                s_processIndex, _input.MethodOf, trace: false, _output, _diagnostics, _stitchingState);
            return _output.Count;
        }

        #region private

        private AsyncProfilerCallStack Segment(AsyncCallstackKind kind, int firstCodeAddress, int frameCount, int depth)
        {
            var methodIds = new ulong[frameCount];
            for (int i = 0; i < frameCount; i++)
            {
                methodIds[i] = (ulong)(firstCodeAddress + i);
            }
            var frames = new AsyncProfilerCallStackFrames(
                kind, methodIds, kind == AsyncCallstackKind.StateMachineAsync ? new int[frameCount] : null);
            _input.SetCodeAddresses(frames, firstCodeAddress);

            return new AsyncProfilerCallStack(
                depth, (AsyncProfilerCallStackFramesIndex)depth, frames,
                continuationIndexBase: 0, wrapperCount: 32, startQpc: 900, endQpc: 1_100,
                methodCompletions: Array.Empty<AsyncProfilerCallStack.CompletionDelta>(),
                exceptionCompletions: Array.Empty<AsyncProfilerCallStack.CompletionDelta>(),
                wrapperResets: Array.Empty<long>());
        }

        private const long Qpc = 1_000;

        private AsyncProfilerSyntheticStitchInput _input;
        private IReadOnlyList<AsyncProfilerStitchSyncFrame> _sync;
        private IReadOnlyList<AsyncProfilerCallStack> _segments;
        private List<AsyncProfilerStitchedFrame> _output;
        private AsyncProfilerCallStackDiagnostics _diagnostics;
        private AsyncProfilerCallStackStitchingState _stitchingState;

        private static readonly ProcessIndex s_processIndex = (ProcessIndex)1;

        #endregion
    }

    /// <summary>Physical and logical stack layouts exercised by the structural-stitching benchmark.</summary>
    public enum AsyncProfilerStitchBenchmarkScenario
    {
        RuntimeAsync,
        StateMachineAsync,
        MixedNested,
        BoundaryNotFound,
    }
}
