using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing.Computers;
using Microsoft.Diagnostics.Tracing.Etlx;

namespace TraceEventBenchmarks
{
    /// <summary>Reports memory use and file sizes of the synthetic V2 async-profiler trace, outside BenchmarkDotNet.</summary>
    /// <remarks>
    /// Run with <c>dotnet run -c Release -f net10.0 --project TraceEvent.Benchmarks -- --async-profiler-peak-memory
    /// &lt;Realistic|High|Extreme&gt;</c>. The tool samples heap and process-memory peaks during one conversion (input
    /// generation excluded), then reports trace and ETLX size overheads against the no-profiler control and the managed
    /// memory retained by the async index after load, plus what the TraceLog retains after stitched stack
    /// generation. Peaks are
    /// approximate, not allocation totals. Run each profile in a fresh process for comparable baselines.
    /// </remarks>
    internal static class AsyncProfilerConversionPeakMemory
    {
        public static void Measure(AsyncProfilerWorkloadProfile profile)
        {
            using (AsyncProfilerEndToEndFixture fixture =
                AsyncProfilerEndToEndFixture.Create(profile, AsyncProfilerKindMode.V2Only))
            {
                MeasureConversionPeak(profile, fixture);

                fixture.WriteNettrace(fixture.ControlNetTracePath, includeProfilerData: false);
                TraceLog.CreateFromEventPipeDataFile(fixture.NetTracePath, fixture.EtlxPath);
                TraceLog.CreateFromEventPipeDataFile(fixture.ControlNetTracePath, fixture.ControlEtlxPath);
                TraceLog.CreateFromEventPipeDataFile(
                    fixture.NetTracePath, fixture.PreservedBuffersEtlxPath,
                    new TraceLogOptions { KeepAllEvents = true });
                Console.WriteLine(fixture.DescribeTraceSizes());

                MeasureAsyncIndexMemory(fixture);
            }
        }

        #region private

        private static void MeasureConversionPeak(AsyncProfilerWorkloadProfile profile, AsyncProfilerEndToEndFixture fixture)
        {
            fixture.WriteNettrace(fixture.NetTracePath, includeProfilerData: true);
            CollectGarbage();

            using (var process = Process.GetCurrentProcess())
            using (var stop = new ManualResetEventSlim())
            {
                process.Refresh();
                var sampler = new PeakMemorySampler(
                    GC.GetTotalMemory(forceFullCollection: false),
                    process.PrivateMemorySize64,
                    process.WorkingSet64);
                var thread = new Thread(() => sampler.SampleUntilStopped(process, stop))
                {
                    IsBackground = true,
                    Name = "AsyncProfilerConversionPeakMemory",
                };

                var stopwatch = Stopwatch.StartNew();
                thread.Start();
                try
                {
                    TraceLog.CreateFromEventPipeDataFile(fixture.NetTracePath, fixture.ConversionEtlxPath);
                }
                finally
                {
                    stopwatch.Stop();
                    stop.Set();
                    thread.Join();
                }

                Console.WriteLine(
                    $"Async conversion peak memory: profile={profile}, contexts={fixture.ContextCount:N0}, " +
                    $"elapsed={stopwatch.Elapsed.TotalSeconds:F3}s, " +
                    $"managedHeap={sampler.ManagedHeapBaseline:N0}->{sampler.ManagedHeapPeak:N0} bytes " +
                    $"(delta={sampler.ManagedHeapPeak - sampler.ManagedHeapBaseline:N0}), " +
                    $"privateBytes={sampler.PrivateBytesBaseline:N0}->{sampler.PrivateBytesPeak:N0} bytes " +
                    $"(delta={sampler.PrivateBytesPeak - sampler.PrivateBytesBaseline:N0}), " +
                    $"workingSet={sampler.WorkingSetBaseline:N0}->{sampler.WorkingSetPeak:N0} bytes " +
                    $"(delta={sampler.WorkingSetPeak - sampler.WorkingSetBaseline:N0}), " +
                    $"etlx={new FileInfo(fixture.ConversionEtlxPath).Length:N0} bytes.");
            }
        }

        // Uses a TraceLog owned by this tool, so loading the index does not affect any benchmark state.
        private static void MeasureAsyncIndexMemory(AsyncProfilerEndToEndFixture fixture)
        {
            using (var traceLog = new TraceLog(fixture.EtlxPath))
            using (var symbolReader = new SymbolReader(TextWriter.Null))
            {
                long managedHeapBeforeLoad = ReportAsyncIndexMemoryAfterLoad(traceLog, fixture.ContextCount);

                AsyncProfilerEndToEndFixture.GenerateCpuStacks(traceLog, symbolReader, enableAsyncCallStacks: true);
                CollectGarbage();
                long delta = GC.GetTotalMemory(forceFullCollection: false) - managedHeapBeforeLoad;
                Console.WriteLine(
                    $"TraceLog retained memory after stitched stack generation (async index plus TraceLog caches): " +
                    $"managedHeapDelta={delta:N0} bytes ({delta / (double)fixture.ContextCount:F2}/context).");
            }
        }

        private static long ReportAsyncIndexMemoryAfterLoad(TraceLog traceLog, int contextCount)
        {
            using (Process process = Process.GetCurrentProcess())
            {
                CollectGarbage();
                long managedHeapBefore = GC.GetTotalMemory(forceFullCollection: false);
                process.Refresh();
                long privateBytesBefore = process.PrivateMemorySize64;
                long workingSetBefore = process.WorkingSet64;

                AsyncProfilerCallStacksIndex index = traceLog.AsyncProfilerCallStacks;
                if (index == null)
                {
                    throw new InvalidOperationException("The generated ETLX does not contain an async callstack index.");
                }

                CollectGarbage();
                long managedHeapAfter = GC.GetTotalMemory(forceFullCollection: false);
                process.Refresh();
                GC.KeepAlive(index);

                long delta = managedHeapAfter - managedHeapBefore;
                Console.WriteLine(
                    $"Async index retained memory after full GC: " +
                    $"managedHeap={managedHeapBefore:N0}->{managedHeapAfter:N0} bytes, " +
                    $"delta={delta:N0} ({delta / (double)contextCount:F2}/context); " +
                    $"processAfterLoad: privateBytes={process.PrivateMemorySize64:N0} bytes, " +
                    $"workingSet={process.WorkingSet64:N0} bytes " +
                    $"(before load: privateBytes={privateBytesBefore:N0}, workingSet={workingSetBefore:N0}).");
                return managedHeapBefore;
            }
        }

        private static void CollectGarbage()
        {
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        }

        private sealed class PeakMemorySampler
        {
            public PeakMemorySampler(long managedHeapBaseline, long privateBytesBaseline, long workingSetBaseline)
            {
                ManagedHeapBaseline = ManagedHeapPeak = managedHeapBaseline;
                PrivateBytesBaseline = PrivateBytesPeak = privateBytesBaseline;
                WorkingSetBaseline = WorkingSetPeak = workingSetBaseline;
            }

            public long ManagedHeapBaseline { get; }
            public long ManagedHeapPeak { get; private set; }
            public long PrivateBytesBaseline { get; }
            public long PrivateBytesPeak { get; private set; }
            public long WorkingSetBaseline { get; }
            public long WorkingSetPeak { get; private set; }

            public void SampleUntilStopped(Process process, ManualResetEventSlim stop)
            {
                while (!stop.IsSet)
                {
                    Sample(process);
                    stop.Wait(5);
                }
                Sample(process);
            }

            #region private

            private void Sample(Process process)
            {
                long managedHeap = GC.GetTotalMemory(forceFullCollection: false);
                process.Refresh();
                long privateBytes = process.PrivateMemorySize64;
                long workingSet = process.WorkingSet64;

                if (managedHeap > ManagedHeapPeak)
                {
                    ManagedHeapPeak = managedHeap;
                }
                if (privateBytes > PrivateBytesPeak)
                {
                    PrivateBytesPeak = privateBytes;
                }
                if (workingSet > WorkingSetPeak)
                {
                    WorkingSetPeak = workingSet;
                }
            }

            #endregion
        }

        #endregion
    }
}
