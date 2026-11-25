using System;
using System.Collections.Generic;

using BenchmarkDotNet.Attributes;

using Microsoft.Diagnostics.Tracing.Computers;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;

using static Microsoft.Diagnostics.Tracing.Computers.AsyncProfilerRuntimeSupport;

namespace TraceEventBenchmarks
{
    /// <summary>Measures presentation of one preclassified stack, excluding structural stitching and stack-source generation.</summary>
    /// <remarks>Each configuration includes restoring the mutable input buffer and resetting per-sample diagnostics.</remarks>
    [MemoryDiagnoser]
    public class AsyncProfilerCallStackTransformPipelineBenchmarks
    {
        [GlobalSetup]
        public void Setup()
        {
            var captured = new AsyncProfilerCallStackFrames(
                AsyncCallstackKind.StateMachineAsync, new ulong[] { 200 }, new[] { 0 });
            captured.SetCodeAddressAt(0, (CodeAddressIndex)200);
            _segment = new AsyncProfilerCallStack(
                0, (AsyncProfilerCallStackFramesIndex)0, captured, 0, 32, 0, long.MaxValue,
                Array.Empty<AsyncProfilerCallStack.CompletionDelta>(),
                Array.Empty<AsyncProfilerCallStack.CompletionDelta>(), Array.Empty<long>());
            _input = new[]
            {
                Sync(10),
                Sync(11, AsyncProfilerStitchSyncFrameKind.V1StateMachineBody),
                Sync(12, RuntimeFrameKind.V1Startup),
                Sync(13, RuntimeFrameKind.V1Startup),
                Sync(14),
                Sync(15, RuntimeFrameKind.TaskContinuations),
                Sync(16, RuntimeFrameKind.TaskResult),
                Sync(17, RuntimeFrameKind.TaskCompletionSourceResult),
                Sync(18),
                AsyncProfilerStitchedFrame.CreateAsync(_segment, 0),
            };
            _frames = new List<AsyncProfilerStitchedFrame>(_input.Length);
            _diagnostics = new AsyncProfilerCallStackDiagnostics();
            _stitchingState = new AsyncProfilerCallStackStitchingState();
            _context = new AsyncProfilerCallStackTransformState(
                traceLog: null, segments: new[] { _segment }, timestampQpc: 1,
                classifyBoundary: _ => AsyncProfilerCallStackStitchBoundaryInfo.None,
                stitchingState: _stitchingState,
                diagnostics: _diagnostics, frames: _frames);
            _structural = new AsyncProfilerCallStackTransformPipeline { EnableNormalization = false };
            _normalization = new AsyncProfilerCallStackTransformPipeline();
            _cleanup = new AsyncProfilerCallStackTransformPipeline { EnableInfrastructureCleanup = true };

            Validate(_structural, expectedFrames: 10, normalized: 0, startupCollapsed: 0, infrastructureCollapsed: 0);
            Validate(_normalization, expectedFrames: 8, normalized: 1, startupCollapsed: 2, infrastructureCollapsed: 0);
            Validate(_cleanup, expectedFrames: 6, normalized: 1, startupCollapsed: 2, infrastructureCollapsed: 3);
        }

        /// <summary>Measures buffer reset and pipeline dispatch with all built-in presentation transforms disabled.</summary>
        [Benchmark(Baseline = true)]
        public int StructuralOnly() => Apply(_structural);

        /// <summary>Normalizes synchronous V1 startup frames in the same restored stack.</summary>
        [Benchmark]
        public int DefaultNormalization() => Apply(_normalization);

        /// <summary>Normalizes startup frames and groups a runtime task-completion chain in the same restored stack.</summary>
        [Benchmark]
        public int NormalizationWithInfrastructureCleanup() => Apply(_cleanup);

        #region private

        private int Apply(AsyncProfilerCallStackTransformPipeline pipeline)
        {
            // Restore the same preclassified sample for each invocation, including the no-transform baseline.
            _frames.Clear();
            _frames.AddRange(_input);
            _diagnostics.Clear();
            _stitchingState.Clear();
            pipeline.ApplyInPlace(_context);
            return _frames.Count;
        }

        private void Validate(AsyncProfilerCallStackTransformPipeline pipeline,
            int expectedFrames, int normalized, int startupCollapsed, int infrastructureCollapsed)
        {
            if (Apply(pipeline) != expectedFrames ||
                _diagnostics.V1SynchronousMoveNextFramesNormalized != normalized ||
                _diagnostics.V1SynchronousStartupFramesCollapsed != startupCollapsed ||
                _diagnostics.AsyncInfrastructureFramesCollapsed != infrastructureCollapsed)
            {
                throw new InvalidOperationException("The pipeline benchmark did not exercise the expected transforms.");
            }
        }

        private AsyncProfilerStitchedFrame Sync(int codeAddress,
            FrameClassification kind = default) =>
            AsyncProfilerStitchedFrame.CreateSync(
                new AsyncProfilerStitchSyncFrame((CodeAddressIndex)codeAddress, MethodIndex.Invalid, kind), _segment);

        private AsyncProfilerCallStack _segment;
        private AsyncProfilerStitchedFrame[] _input;
        private List<AsyncProfilerStitchedFrame> _frames;
        private AsyncProfilerCallStackDiagnostics _diagnostics;
        private AsyncProfilerCallStackStitchingState _stitchingState;
        private AsyncProfilerCallStackTransformState _context;
        private AsyncProfilerCallStackTransformPipeline _structural;
        private AsyncProfilerCallStackTransformPipeline _normalization;
        private AsyncProfilerCallStackTransformPipeline _cleanup;

        #endregion
    }
}
