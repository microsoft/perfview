using System;
using System.IO;

using BenchmarkDotNet.Attributes;

using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing.Etlx;

namespace TraceEventBenchmarks
{
    /// <summary>Measures synthetic profiler-trace conversion, loading, and CPU-stack generation.</summary>
    /// <remarks>
    /// Each invocation processes one complete trace. Each benchmark group has its own targeted setup so it only creates the
    /// inputs it needs; setup performs only cheap sanity checks. Full validation of the inputs runs as a TraceEvent test, and
    /// file-size and retained-memory reporting lives in <see cref="AsyncProfilerConversionPeakMemory"/>. The matching
    /// no-profiler controls are in <see cref="AsyncProfilerEndToEndControlBenchmarks"/>.
    /// </remarks>
    [MemoryDiagnoser]
    public class AsyncProfilerEndToEndBenchmarks
    {
        /// <summary>Instrumentation families included in the profiler trace.</summary>
        [Params(AsyncProfilerKindMode.V2Only, AsyncProfilerKindMode.V1Only, AsyncProfilerKindMode.Mixed)]
        public AsyncProfilerKindMode KindMode { get; set; }

        /// <summary>Synthetic input scale; each invocation still processes a complete trace.</summary>
        [Params(
            AsyncProfilerWorkloadProfile.Realistic,
            AsyncProfilerWorkloadProfile.High,
            AsyncProfilerWorkloadProfile.Extreme)]
        public AsyncProfilerWorkloadProfile WorkloadProfile { get; set; }

        [GlobalSetup(Targets = new[] { nameof(NetTraceToEtlxWithAsync), nameof(NetTraceToEtlxWithAsyncBuffers) })]
        public void SetupConversion()
        {
            RunSetup(() =>
            {
                _fixture = AsyncProfilerEndToEndFixture.Create(WorkloadProfile, KindMode);
                _fixture.WriteNettrace(_fixture.NetTracePath, includeProfilerData: true);
            });
        }

        [GlobalSetup(Target = nameof(OpenEtlxAndLoadAsyncIndex))]
        public void SetupOpen()
        {
            RunSetup(CreateEtlx);
        }

        [GlobalSetup(Targets = new[]
        {
            nameof(GenerateCpuStacksWithAsyncIndex),
            nameof(GenerateStitchedCpuStacks),
            nameof(ReloadReleasedAsyncIndex),
        })]
        public void SetupStacks()
        {
            RunSetup(() =>
            {
                CreateEtlx();
                _traceLog = new TraceLog(_fixture.EtlxPath);
                _symbolReader = new SymbolReader(TextWriter.Null);
                _fixture.RequireAsyncIndex(_traceLog);
            });
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _traceLog?.Dispose();
            _traceLog = null;
            _symbolReader?.Dispose();
            _symbolReader = null;
            _fixture?.Dispose();
            _fixture = null;
        }

        /// <summary>Converts one profiler trace, persisting the derived index while omitting raw profiler buffers.</summary>
        [Benchmark]
        public long NetTraceToEtlxWithAsync()
        {
            TraceLog.CreateFromEventPipeDataFile(_fixture.NetTracePath, _fixture.ConversionEtlxPath);
            return new FileInfo(_fixture.ConversionEtlxPath).Length;
        }

        /// <summary>Converts the same profiler trace while retaining both the derived index and raw profiler buffers.</summary>
        [Benchmark]
        public long NetTraceToEtlxWithAsyncBuffers()
        {
            TraceLog.CreateFromEventPipeDataFile(
                _fixture.NetTracePath, _fixture.PreservedBuffersConversionEtlxPath,
                new TraceLogOptions { KeepAsyncProfilerEvents = true });
            return new FileInfo(_fixture.PreservedBuffersConversionEtlxPath).Length;
        }

        /// <summary>Opens the profiler ETLX, materializes its deferred async index, and disposes the log.</summary>
        [Benchmark]
        public int OpenEtlxAndLoadAsyncIndex()
        {
            using (var traceLog = new TraceLog(_fixture.EtlxPath))
            {
                return traceLog.EventCount + traceLog.AsyncProfilerCallStacks.DistinctFramesCount;
            }
        }

        /// <summary>Generates physical CPU stacks with an async index loaded but stitching disabled.</summary>
        /// <remarks>
        /// Compared with <see cref="AsyncProfilerEndToEndControlBenchmarks.GenerateCpuStacksWithoutAsync"/>, this guards that
        /// merely having a loaded index does not slow down stack generation when async stitching is disabled.
        /// </remarks>
        [Benchmark]
        public int GenerateCpuStacksWithAsyncIndex()
        {
            return AsyncProfilerEndToEndFixture.GenerateCpuStacks(_traceLog, _symbolReader, enableAsyncCallStacks: false);
        }

        /// <summary>Generates stitched CPU stacks from the loaded index, including stack-source frame interning.</summary>
        [Benchmark]
        public int GenerateStitchedCpuStacks()
        {
            return AsyncProfilerEndToEndFixture.GenerateCpuStacks(_traceLog, _symbolReader, enableAsyncCallStacks: true);
        }

        /// <summary>Releases and reloads the deferred index on an already-open log, without generating stacks.</summary>
        [Benchmark]
        public int ReloadReleasedAsyncIndex()
        {
            if (!_traceLog.ReleaseAsyncProfilerCallStacks())
            {
                throw new InvalidOperationException("The async-callstack index was not loaded or reloadable.");
            }
            return _traceLog.AsyncProfilerCallStacks.DistinctFramesCount;
        }

        #region private

        private void CreateEtlx()
        {
            _fixture = AsyncProfilerEndToEndFixture.Create(WorkloadProfile, KindMode);
            _fixture.WriteNettrace(_fixture.NetTracePath, includeProfilerData: true);
            TraceLog.CreateFromEventPipeDataFile(_fixture.NetTracePath, _fixture.EtlxPath);
        }

        private void RunSetup(Action setup)
        {
            try
            {
                setup();
            }
            catch
            {
                Cleanup();
                throw;
            }
        }

        private AsyncProfilerEndToEndFixture _fixture;
        private TraceLog _traceLog;
        private SymbolReader _symbolReader;

        #endregion
    }

    /// <summary>No-profiler control for <see cref="AsyncProfilerEndToEndBenchmarks"/>: the same CPU samples without async data.</summary>
    /// <remarks>
    /// The control trace contains only 1 kHz CPU samples, so its size depends only on the workload duration, not on
    /// <see cref="AsyncProfilerKindMode"/> or the context rate. <see cref="AsyncProfilerWorkloadProfile.Realistic"/> and
    /// <see cref="AsyncProfilerWorkloadProfile.High"/> share the same 30-second duration and therefore produce equivalent
    /// controls, so only <see cref="AsyncProfilerWorkloadProfile.Realistic"/> (30 s) and
    /// <see cref="AsyncProfilerWorkloadProfile.Extreme"/> (10 s) are measured.
    /// </remarks>
    [MemoryDiagnoser]
    public class AsyncProfilerEndToEndControlBenchmarks
    {
        /// <summary>Workload duration; see the class remarks for why High is omitted.</summary>
        [Params(AsyncProfilerWorkloadProfile.Realistic, AsyncProfilerWorkloadProfile.Extreme)]
        public AsyncProfilerWorkloadProfile WorkloadProfile { get; set; }

        [GlobalSetup(Target = nameof(NetTraceToEtlxWithoutAsync))]
        public void SetupConversion()
        {
            RunSetup(() =>
            {
                _fixture = CreateFixture();
                _fixture.WriteNettrace(_fixture.ControlNetTracePath, includeProfilerData: false);
            });
        }

        [GlobalSetup(Target = nameof(OpenEtlxWithoutAsync))]
        public void SetupOpen()
        {
            RunSetup(CreateEtlx);
        }

        [GlobalSetup(Target = nameof(GenerateCpuStacksWithoutAsync))]
        public void SetupStacks()
        {
            RunSetup(() =>
            {
                CreateEtlx();
                _traceLog = new TraceLog(_fixture.ControlEtlxPath);
                _symbolReader = new SymbolReader(TextWriter.Null);
                if (_traceLog.AsyncProfilerCallStacks != null)
                {
                    throw new InvalidOperationException("The no-async control ETLX unexpectedly contains an async callstack index.");
                }
            });
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            _traceLog?.Dispose();
            _traceLog = null;
            _symbolReader?.Dispose();
            _symbolReader = null;
            _fixture?.Dispose();
            _fixture = null;
        }

        /// <summary>Converts one no-profiler control trace to ETLX, including file I/O.</summary>
        [Benchmark]
        public long NetTraceToEtlxWithoutAsync()
        {
            TraceLog.CreateFromEventPipeDataFile(_fixture.ControlNetTracePath, _fixture.ControlConversionEtlxPath);
            return new FileInfo(_fixture.ControlConversionEtlxPath).Length;
        }

        /// <summary>Opens and disposes the control ETLX, without conversion or an async index.</summary>
        [Benchmark]
        public int OpenEtlxWithoutAsync()
        {
            using (var traceLog = new TraceLog(_fixture.ControlEtlxPath))
            {
                return traceLog.EventCount + (traceLog.AsyncProfilerCallStacks?.DistinctFramesCount ?? 0);
            }
        }

        /// <summary>Generates physical CPU stacks from the already-open no-profiler control.</summary>
        [Benchmark]
        public int GenerateCpuStacksWithoutAsync()
        {
            return AsyncProfilerEndToEndFixture.GenerateCpuStacks(_traceLog, _symbolReader, enableAsyncCallStacks: false);
        }

        #region private

        // KindMode only changes which stack IDs the control's CPU samples reference, not their count or size.
        private AsyncProfilerEndToEndFixture CreateFixture()
        {
            return AsyncProfilerEndToEndFixture.Create(WorkloadProfile, AsyncProfilerKindMode.V2Only);
        }

        private void CreateEtlx()
        {
            _fixture = CreateFixture();
            _fixture.WriteNettrace(_fixture.ControlNetTracePath, includeProfilerData: false);
            TraceLog.CreateFromEventPipeDataFile(_fixture.ControlNetTracePath, _fixture.ControlEtlxPath);
        }

        private void RunSetup(Action setup)
        {
            try
            {
                setup();
            }
            catch
            {
                Cleanup();
                throw;
            }
        }

        private AsyncProfilerEndToEndFixture _fixture;
        private TraceLog _traceLog;
        private SymbolReader _symbolReader;

        #endregion
    }
}
