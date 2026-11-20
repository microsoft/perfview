using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Computers;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.EventPipe;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;
using Microsoft.Diagnostics.Tracing.Stacks;

using TraceEventTests;

namespace TraceEventBenchmarks
{
    /// <summary>Async instrumentation families emitted by the synthetic end-to-end traces.</summary>
    public enum AsyncProfilerKindMode
    {
        V2Only,
        V1Only,
        Mixed,
    }

    /// <summary>Call-stack rates and trace durations used to compare end-to-end scaling.</summary>
    /// <remarks>
    /// Realistic and High both span 30 seconds (10,000 and 100,000 contexts per second); Extreme spans 10 seconds at
    /// 1,000,000 contexts per second. CPU samples are emitted at 1 kHz, so the no-profiler control trace depends only
    /// on the duration.
    /// </remarks>
    public enum AsyncProfilerWorkloadProfile
    {
        Realistic,
        High,
        Extreme,
    }

    /// <summary>Creates synthetic EventPipe traces with controlled async coverage and matching no-profiler controls.</summary>
    /// <remarks>
    /// Shared by the end-to-end benchmarks, the peak-memory tool, and the TraceEvent tests, which run
    /// <see cref="Validate"/> at <see cref="AsyncProfilerWorkloadProfile.Realistic"/> scale.
    /// </remarks>
    internal sealed class AsyncProfilerEndToEndFixture : IDisposable
    {
        public static AsyncProfilerEndToEndFixture Create(
            AsyncProfilerWorkloadProfile workloadProfile, AsyncProfilerKindMode kindMode)
        {
            int contextRatePerSecond;
            int durationSeconds;
            switch (workloadProfile)
            {
                case AsyncProfilerWorkloadProfile.Realistic:
                    contextRatePerSecond = 10_000;
                    durationSeconds = 30;
                    break;
                case AsyncProfilerWorkloadProfile.High:
                    contextRatePerSecond = 100_000;
                    durationSeconds = 30;
                    break;
                case AsyncProfilerWorkloadProfile.Extreme:
                    contextRatePerSecond = 1_000_000;
                    durationSeconds = 10;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(workloadProfile));
            }
            string directory = Path.Combine(Path.GetTempPath(), "TraceEventAsyncBenchmark_" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);
            return new AsyncProfilerEndToEndFixture(directory, workloadProfile, kindMode, contextRatePerSecond, durationSeconds);
        }

        public AsyncProfilerKindMode KindMode { get; }
        public string Directory { get; }
        public string NetTracePath { get; }
        public string ControlNetTracePath { get; }
        public string EtlxPath { get; }
        public string ControlEtlxPath { get; }
        public string PreservedBuffersEtlxPath { get; }
        public string ConversionEtlxPath { get; }
        public string ControlConversionEtlxPath { get; }
        public string PreservedBuffersConversionEtlxPath { get; }

        public int ContextCount => _contextRatePerSecond * _durationSeconds;

        /// <summary>Creates a CPU thread-time computer configured like the benchmarks, without event-source or activity grouping.</summary>
        public static SampleProfilerThreadTimeComputer CreateComputer(
            TraceLog traceLog, SymbolReader symbolReader, bool enableAsyncCallStacks)
        {
            return new SampleProfilerThreadTimeComputer(traceLog, symbolReader)
            {
                EnableAsyncCallStacks = enableAsyncCallStacks,
                IncludeEventSourceEvents = false,
                GroupByStartStopActivity = false,
            };
        }

        /// <summary>Generates CPU stacks for every sample in <paramref name="traceLog"/> and returns the sample count.</summary>
        public static int GenerateCpuStacks(TraceLog traceLog, SymbolReader symbolReader, bool enableAsyncCallStacks)
        {
            var stackSource = new MutableTraceEventStackSource(traceLog);
            CreateComputer(traceLog, symbolReader, enableAsyncCallStacks).GenerateThreadTimeStacks(stackSource);
            int count = 0;
            stackSource.ForEach(_ => count++);
            return count;
        }

        /// <summary>Writes both nettraces and converts them to the index-only, control, and preserved-buffer ETLX files.</summary>
        public void CreateAllInputs()
        {
            WriteNettrace(NetTracePath, includeProfilerData: true);
            WriteNettrace(ControlNetTracePath, includeProfilerData: false);
            TraceLog.CreateFromEventPipeDataFile(NetTracePath, EtlxPath);
            TraceLog.CreateFromEventPipeDataFile(ControlNetTracePath, ControlEtlxPath);
            TraceLog.CreateFromEventPipeDataFile(
                NetTracePath, PreservedBuffersEtlxPath, new TraceLogOptions { KeepAsyncProfilerEvents = true });
        }

        public void WriteNettrace(string path, bool includeProfilerData)
        {
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var writer = new EventPipeWriterV6(stream);
                writer.WriteHeaders(keyValues: null, syncTimeQpc: 1, qpcFrequency: QpcFrequency);
                writer.WriteMetadataBlock(
                    new EventMetadata(
                        1, AsyncProfilerTraceEventParser.ProviderName, "AsyncEvents",
                        AsyncProfilerTraceEventParser.AsyncEventsEventId)
                    {
                        ProviderId = AsyncProfilerTraceEventParser.ProviderGuid,
                    },
                    new EventMetadata(
                        2, SampleProfilerTraceEventParser.ProviderName, "Sample", 0,
                        new MetadataParameter("Type", MetadataTypeCode.Int32))
                    {
                        ProviderId = SampleProfilerTraceEventParser.ProviderGuid,
                    },
                    new EventMetadata(3, "Universal.System", "ProcessMapping", 3)
                    {
                        ProviderId = UniversalSystemTraceEventParser.ProviderGuid,
                    },
                    new EventMetadata(4, "Universal.System", "ProcessSymbol", 4)
                    {
                        ProviderId = UniversalSystemTraceEventParser.ProviderGuid,
                    });

                writer.WriteThreadBlock(w => w.WriteThreadEntry(ThreadStreamIndex, OsThreadId, ProcessId));
                writer.WriteStackBlock(1, (DistinctAsyncStackCount * 2) + 1, w =>
                {
                    for (int stackIndex = 0; stackIndex < DistinctAsyncStackCount; stackIndex++)
                    {
                        w.WriteStackPayload(
                            MethodAddress(FirstMethodIndex(AsyncCallstackKind.RuntimeAsync, stackIndex)),
                            WrapperAddress, DispatchAddress);
                    }
                    for (int stackIndex = 0; stackIndex < DistinctAsyncStackCount; stackIndex++)
                    {
                        w.WriteStackPayload(
                            MethodAddress(FirstMethodIndex(AsyncCallstackKind.StateMachineAsync, stackIndex)),
                            V1DispatcherAddress, V1InfrastructureAddress);
                    }
                    w.WriteStackPayload(MethodAddress(UnmatchedMethodIndex), WrapperAddress, DispatchAddress);
                });

                int sequence = WriteSymbols(writer);
                WriteWorkload(writer, sequence, includeProfilerData);
                writer.WriteEndBlock();
                writer.Flush();
            }
        }

        /// <summary>Cheap setup check that <paramref name="traceLog"/> exposes the expected async index; loads a deferred index.</summary>
        public void RequireAsyncIndex(TraceLog traceLog)
        {
            AsyncProfilerCallStacksIndex index = traceLog.AsyncProfilerCallStacks;
            Require(index != null, "The generated ETLX does not contain an async callstack index.");
            Require(index.DistinctFramesCount == ExpectedDistinctFrames,
                $"Expected {ExpectedDistinctFrames} shared async frame sequences, found {index.DistinctFramesCount}.");
        }

        /// <summary>
        /// Fully validates the raw input, all three ETLX files created by <see cref="CreateAllInputs"/>, and every generated
        /// sync and stitched CPU stack. This is O(trace size) and is run by the tests, not by benchmark setup.
        /// </summary>
        public void Validate(TraceLog traceLog, TraceLog controlTraceLog, SymbolReader symbolReader)
        {
            ValidateRawInput();

            Require(controlTraceLog.AsyncProfilerCallStacks == null,
                "The no-async control ETLX unexpectedly contains an async callstack index.");
            ValidatePreservedBuffersEtlx();
            Require(CountAsyncBuffers(traceLog) == 0,
                "The default ETLX unexpectedly retained raw async-profiler buffers.");

            RequireAsyncIndex(traceLog);
            AsyncProfilerCallStacksIndex index = traceLog.AsyncProfilerCallStacks;
            int intervalCount = 0;
            int threadCount = 0;
            foreach (AsyncProfilerThreadKey thread in index.Threads)
            {
                threadCount++;
                intervalCount += index.GetCallStacks(thread).Count;
            }
            Require(threadCount == 1, $"Expected one async thread, found {threadCount}.");
            Require(intervalCount == ContextCount, $"Expected {ContextCount} indexed contexts, found {intervalCount}.");

            var controlStackSource = new MutableTraceEventStackSource(controlTraceLog);
            CreateComputer(controlTraceLog, symbolReader, enableAsyncCallStacks: false)
                .GenerateThreadTimeStacks(controlStackSource);
            ValidateStacks(controlStackSource, StackValidationMode.Sync);

            var syncStackSource = new MutableTraceEventStackSource(traceLog);
            CreateComputer(traceLog, symbolReader, enableAsyncCallStacks: false)
                .GenerateThreadTimeStacks(syncStackSource);
            ValidateStacks(syncStackSource, StackValidationMode.Sync);

            var stitchedStackSource = new MutableTraceEventStackSource(traceLog);
            SampleProfilerThreadTimeComputer stitchedComputer =
                CreateComputer(traceLog, symbolReader, enableAsyncCallStacks: true);
            stitchedComputer.GenerateThreadTimeStacks(stitchedStackSource);
            ValidateStacks(stitchedStackSource, StackValidationMode.Stitched);
            AsyncProfilerCallStackDiagnostics diagnostics = stitchedComputer.AsyncProfilerCallStackDiagnostics;
            Require(diagnostics.SegmentsProcessed == MatchedSampleCount,
                $"Expected {MatchedSampleCount} matched samples, found {diagnostics.SegmentsProcessed}.");
            Require(diagnostics.BoundariesNotFound == 0,
                "A generated CPU sample did not contain the expected V2 continuation wrapper.");
            Require(diagnostics.AdjacencyMismatches == 0,
                "A generated CPU sample did not align with the expected current async method.");
            Require(diagnostics.V2SyncLayoutUsed == 0,
                "A matched generated CPU sample unexpectedly used the synchronous layout.");
        }

        /// <summary>Describes the trace shape and per-context file-size overheads of the files created by <see cref="CreateAllInputs"/>.</summary>
        public string DescribeTraceSizes()
        {
            long asyncNettraceBytes = new FileInfo(NetTracePath).Length;
            long controlNettraceBytes = new FileInfo(ControlNetTracePath).Length;
            long asyncEtlxBytes = new FileInfo(EtlxPath).Length;
            long controlEtlxBytes = new FileInfo(ControlEtlxPath).Length;
            long preservedEtlxBytes = new FileInfo(PreservedBuffersEtlxPath).Length;
            return
                $"Async fixture: contexts={ContextCount:N0}, subEvents={(ContextCount * 2) + 2:N0}, " +
                $"samples={CpuSampleCount:N0}, matched={MatchedSampleCount:N0}, unmatched={UnmatchedSampleCount:N0}, " +
                $"nettraceDelta={asyncNettraceBytes - controlNettraceBytes:N0} bytes " +
                $"({(asyncNettraceBytes - controlNettraceBytes) / (double)ContextCount:F2}/context), " +
                $"indexOnlyEtlxDelta={asyncEtlxBytes - controlEtlxBytes:N0} bytes " +
                $"({(asyncEtlxBytes - controlEtlxBytes) / (double)ContextCount:F2}/context), " +
                $"preservedBufferEtlxDelta={preservedEtlxBytes - controlEtlxBytes:N0} bytes " +
                $"({(preservedEtlxBytes - controlEtlxBytes) / (double)ContextCount:F2}/context).";
        }

        public void Dispose()
        {
            if (System.IO.Directory.Exists(Directory))
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }
        }

        #region private

        private AsyncProfilerEndToEndFixture(
            string directory, AsyncProfilerWorkloadProfile workloadProfile, AsyncProfilerKindMode kindMode,
            int contextRatePerSecond, int durationSeconds)
        {
            KindMode = kindMode;
            _contextRatePerSecond = contextRatePerSecond;
            _durationSeconds = durationSeconds;
            Directory = directory;
            string profile = workloadProfile.ToString().ToLowerInvariant();
            string mode = kindMode.ToString().ToLowerInvariant();
            string name = "async-" + profile + "-" + mode;
            NetTracePath = Path.Combine(directory, name + ".nettrace");
            ControlNetTracePath = Path.Combine(directory, name + "-control.nettrace");
            EtlxPath = Path.Combine(directory, name + ".etlx");
            ControlEtlxPath = Path.Combine(directory, name + "-control.etlx");
            PreservedBuffersEtlxPath = Path.Combine(directory, name + "-preserved.etlx");
            ConversionEtlxPath = Path.Combine(directory, name + "-conversion.etlx");
            ControlConversionEtlxPath = Path.Combine(directory, name + "-control-conversion.etlx");
            PreservedBuffersConversionEtlxPath = Path.Combine(directory, name + "-preserved-conversion.etlx");
        }

        private int CpuSampleCount => _durationSeconds * 1_000;
        private int UnmatchedSampleCount => CpuSampleCount / UnmatchedSamplePeriod;
        private int MatchedSampleCount => CpuSampleCount - UnmatchedSampleCount;
        private int ContextsPerSample => _contextRatePerSecond / 1_000;
        private int ExpectedDistinctFrames => KindMode == AsyncProfilerKindMode.Mixed
            ? DistinctAsyncStackCount * 2
            : DistinctAsyncStackCount;

        private static int WriteSymbols(EventPipeWriterV6 writer)
        {
            int sequence = 1;
            writer.WriteEventBlock(block =>
            {
                block.WriteEventBlob(EventOptions(3, sequence++, StartQpc - 3), w =>
                    w.WriteProcessMappingPayload(
                        AppMappingId, AppMappingStart,
                        AppMappingStart + ((ulong)ManagedMethodCount * AddressStride), "TestApp"));
                block.WriteEventBlob(EventOptions(3, sequence++, StartQpc - 2), w =>
                    w.WriteProcessMappingPayload(
                        RuntimeMappingId, RuntimeMappingStart,
                        RuntimeMappingStart + (4 * AddressStride), AsyncProfilerRuntimeSupport.HostModuleName));

                ulong symbolId = 1;
                for (int methodIndex = 0; methodIndex < ManagedMethodCount; methodIndex++)
                {
                    int capturedMethodIndex = methodIndex;
                    ulong capturedSymbolId = symbolId++;
                    block.WriteEventBlob(EventOptions(4, sequence++, StartQpc - 1), w =>
                        w.WriteProcessSymbolPayload(
                            capturedSymbolId, AppMappingId,
                            MethodAddress(capturedMethodIndex), SymbolName(capturedMethodIndex)));
                }

                block.WriteEventBlob(EventOptions(4, sequence++, StartQpc - 1), w =>
                    w.WriteProcessSymbolPayload(symbolId++, RuntimeMappingId, WrapperAddress, WrapperName));
                block.WriteEventBlob(EventOptions(4, sequence++, StartQpc - 1), w =>
                    w.WriteProcessSymbolPayload(symbolId++, RuntimeMappingId, DispatchAddress, DispatchName));
                block.WriteEventBlob(EventOptions(4, sequence++, StartQpc - 1), w =>
                    w.WriteProcessSymbolPayload(
                        symbolId++, RuntimeMappingId, V1DispatcherAddress,
                        "System.Runtime.CompilerServices.AsyncProfilerAsyncStateMachineBox.MoveNextAsDispatcher"));
                block.WriteEventBlob(EventOptions(4, sequence++, StartQpc - 1), w =>
                    w.WriteProcessSymbolPayload(
                        symbolId, RuntimeMappingId, V1InfrastructureAddress,
                        "System.Runtime.CompilerServices.AsyncProfilerAsyncStateMachineBox.InstrumentedMoveNext"));
            });
            return sequence;
        }

        private void WriteWorkload(EventPipeWriterV6 writer, int sequence, bool includeProfilerData)
        {
            ulong[][] v2Frames = CreateAsyncFrames(AsyncCallstackKind.RuntimeAsync);
            ulong[][] v1Frames = CreateAsyncFrames(AsyncCallstackKind.StateMachineAsync);
            int[] v1States = new int[AsyncFrameCount];

            int contextIndex = 0;
            int sampleIndex = 0;
            int bufferCount = ContextCount / ContextsPerBuffer;
            for (int bufferIndex = 0; bufferIndex < bufferCount; bufferIndex++)
            {
                int firstContext = contextIndex;
                long bufferStartQpc = ContextStartQpc(firstContext);
                var asyncBuffer = new AsyncProfilerBufferBuilder(OsThreadId, 0x0BADF00D, bufferStartQpc);
                if (bufferIndex == 0)
                {
                    asyncBuffer.Metadata(
                        bufferStartQpc, (ulong)QpcFrequency,
                        qpcSync: 1, utcSync: 1, eventBufferSize: 0, wrapperCount: 32,
                        new AsyncManifestEntry[0]);
                    asyncBuffer.ContextNoPayload(AsyncEventID.ResetAsyncThreadContext, bufferStartQpc);
                }

                for (int i = 0; i < ContextsPerBuffer; i++, contextIndex++)
                {
                    long contextStartQpc = ContextStartQpc(contextIndex) + 1;
                    long contextEndQpc = ContextStartQpc(contextIndex + 1);
                    int stackIndex = AsyncStackIndex(contextIndex);
                    AsyncCallstackKind kind = KindForContext(contextIndex);
                    if (kind == AsyncCallstackKind.RuntimeAsync)
                    {
                        asyncBuffer.Callstack(
                            AsyncEventID.ResumeRuntimeAsyncCallstack, contextStartQpc,
                            continuationIndex: 0, parentDispatcherId: 0, dispatcherId: (ulong)contextIndex + 1,
                            v2Frames[stackIndex], states: null);
                        asyncBuffer.ContextNoPayload(AsyncEventID.SuspendRuntimeAsyncContext, contextEndQpc);
                    }
                    else
                    {
                        asyncBuffer.Callstack(
                            AsyncEventID.ResumeStateMachineAsyncCallstack, contextStartQpc,
                            continuationIndex: 0, parentDispatcherId: 0, dispatcherId: (ulong)contextIndex + 1,
                            v1Frames[stackIndex], v1States);
                        asyncBuffer.ContextNoPayload(AsyncEventID.SuspendStateMachineAsyncContext, contextEndQpc);
                    }
                }

                byte[] outerPayload = AsyncProfilerTestData.AsyncEventsPayload(asyncBuffer.Build());
                long bufferEndQpc = ContextStartQpc(contextIndex);
                writer.WriteEventBlock(block =>
                {
                    long nextBufferStartQpc = bufferEndQpc;
                    while (sampleIndex < CpuSampleCount)
                    {
                        bool unmatched = IsUnmatchedSample(sampleIndex);
                        long sampleQpc = StartQpc + ((long)sampleIndex * QpcFrequency / 1_000);
                        if (!unmatched)
                        {
                            sampleQpc++;
                        }
                        if (sampleQpc >= nextBufferStartQpc)
                        {
                            break;
                        }

                        int contextAtSample = sampleIndex * ContextsPerSample;
                        int stackId = unmatched
                            ? UnmatchedStackId
                            : StackId(KindForContext(contextAtSample), AsyncStackIndex(contextAtSample));
                        block.WriteEventBlob(EventOptions(2, sequence++, sampleQpc, stackId),
                            w => w.Write((int)ClrThreadSampleType.Managed));
                        sampleIndex++;
                    }
                    if (includeProfilerData)
                    {
                        block.WriteEventBlob(EventOptions(1, sequence++, bufferEndQpc), w => w.Write(outerPayload));
                    }
                });
            }

            writer.WriteEventBlock(block =>
            {
                long sentinelQpc = StartQpc + ((long)_durationSeconds * QpcFrequency);
                block.WriteEventBlob(EventOptions(2, sequence, sentinelQpc, stackId: 1),
                    w => w.Write((int)ClrThreadSampleType.Managed));
            });
        }

        private void ValidateRawInput()
        {
            var sink = new AsyncProfilerCountingSink();
            int outerBufferCount = 0;
            using (var source = new EventPipeEventSource(NetTracePath))
            {
                var parser = new AsyncProfilerTraceEventParser(source);
                var manifest = new Manifest();
                parser.AsyncEvents += data =>
                {
                    outerBufferCount++;
                    AsyncProfilerTraceEventParser.ParseBuffer(data.Buffer, manifest, sink);
                };
                source.Process();
            }

            Require(outerBufferCount == ContextCount / ContextsPerBuffer,
                $"Expected {ContextCount / ContextsPerBuffer} outer async buffers, found {outerBufferCount}.");
            Require(sink.SubEventCount == (ContextCount * 2) + 2,
                $"Expected {(ContextCount * 2) + 2} logical async sub-events, found {sink.SubEventCount}.");
            int expectedRuntimeCount = KindMode == AsyncProfilerKindMode.V2Only
                ? ContextCount
                : KindMode == AsyncProfilerKindMode.V1Only ? 0 : ContextCount / 2;
            int expectedStateMachineCount = ContextCount - expectedRuntimeCount;
            Require(sink.RuntimeResumeCount == expectedRuntimeCount,
                $"Expected {expectedRuntimeCount} V2 resume callstacks, found {sink.RuntimeResumeCount}.");
            Require(sink.RuntimeSuspendCount == expectedRuntimeCount,
                $"Expected {expectedRuntimeCount} V2 suspends, found {sink.RuntimeSuspendCount}.");
            Require(sink.StateMachineResumeCount == expectedStateMachineCount,
                $"Expected {expectedStateMachineCount} V1 resume callstacks, found {sink.StateMachineResumeCount}.");
            Require(sink.StateMachineSuspendCount == expectedStateMachineCount,
                $"Expected {expectedStateMachineCount} V1 suspends, found {sink.StateMachineSuspendCount}.");
            Require(sink.ParseErrorCount == 0, $"The generated async buffers produced {sink.ParseErrorCount} parse errors.");
        }

        private void ValidatePreservedBuffersEtlx()
        {
            using (var traceLog = new TraceLog(PreservedBuffersEtlxPath))
            {
                int outerBufferCount = CountAsyncBuffers(traceLog);

                Require(outerBufferCount == ContextCount / ContextsPerBuffer,
                    $"Expected {ContextCount / ContextsPerBuffer} preserved async buffers, found {outerBufferCount}.");
                Require(traceLog.AsyncProfilerCallStacks != null,
                    "Preserving raw async buffers unexpectedly removed the derived async index.");
            }
        }

        private static int CountAsyncBuffers(TraceLog traceLog)
        {
            int count = 0;
            foreach (TraceEvent e in traceLog.Events)
            {
                if (e.ProviderGuid == AsyncProfilerTraceEventParser.ProviderGuid &&
                    e.ID == (TraceEventID)AsyncProfilerTraceEventParser.AsyncEventsEventId)
                {
                    count++;
                }
            }
            return count;
        }

        private void ValidateStacks(MutableTraceEventStackSource stackSource, StackValidationMode mode)
        {
            int sampleIndex = 0;
            int matchedCount = 0;
            int unmatchedCount = 0;
            stackSource.ForEach(sample =>
            {
                bool unmatched = IsUnmatchedSample(sampleIndex);
                int contextAtSample = sampleIndex * ContextsPerSample;
                int asyncStackIndex = AsyncStackIndex(contextAtSample);
                AsyncCallstackKind kind = KindForContext(contextAtSample);
                var actual = new List<string>();
                for (StackSourceCallStackIndex stack = sample.StackIndex;
                    stack != StackSourceCallStackIndex.Invalid;
                    stack = stackSource.GetCallerIndex(stack))
                {
                    string name = stackSource.GetFrameName(stackSource.GetFrameIndex(stack), false);
                    string scenarioFrame = GetScenarioFrame(name);
                    if (scenarioFrame != null)
                    {
                        actual.Add(scenarioFrame);
                    }
                }

                string[] expected;
                if (unmatched || mode == StackValidationMode.Sync)
                {
                    int methodIndex = unmatched
                        ? UnmatchedMethodIndex
                        : FirstMethodIndex(kind, asyncStackIndex);
                    expected = new[]
                    {
                        MethodName(methodIndex),
                        unmatched || kind == AsyncCallstackKind.RuntimeAsync
                            ? "Wrapper"
                            : AsyncProfilerCallStackStitchBoundary.MoveNextAsDispatcherName,
                        unmatched || kind == AsyncCallstackKind.RuntimeAsync
                            ? "DispatchContinuations"
                            : "V1Infrastructure",
                    };
                }
                else
                {
                    expected = new string[AsyncFrameCount];
                    int firstMethodIndex = FirstMethodIndex(kind, asyncStackIndex);
                    for (int frameIndex = 0; frameIndex < expected.Length; frameIndex++)
                    {
                        expected[frameIndex] = MethodName(firstMethodIndex + frameIndex);
                    }
                }

                Require(actual.Count == expected.Length,
                    $"Sample {sampleIndex} produced {actual.Count} scenario frames; expected {expected.Length}. " +
                    $"Actual: {string.Join(" -> ", actual)}.");
                for (int frameIndex = 0; frameIndex < expected.Length; frameIndex++)
                {
                    Require(actual[frameIndex] == expected[frameIndex],
                        $"Sample {sampleIndex}, frame {frameIndex}: expected '{expected[frameIndex]}', " +
                        $"found '{actual[frameIndex]}'. Full scenario stack: {string.Join(" -> ", actual)}.");
                }

                if (unmatched)
                {
                    unmatchedCount++;
                }
                else
                {
                    matchedCount++;
                }
                sampleIndex++;
            });

            Require(sampleIndex == CpuSampleCount,
                $"Expected {CpuSampleCount} CPU samples, found {sampleIndex}.");
            Require(matchedCount == MatchedSampleCount,
                $"Expected {MatchedSampleCount} matched samples, found {matchedCount}.");
            Require(unmatchedCount == UnmatchedSampleCount,
                $"Expected {UnmatchedSampleCount} unmatched samples, found {unmatchedCount}.");
        }

        private static string GetScenarioFrame(string frameName)
        {
            const string methodPrefix = "TestApp.Workload.Method";
            int methodStart = frameName.IndexOf(methodPrefix, StringComparison.Ordinal);
            if (methodStart >= 0)
            {
                int methodEnd = methodStart + methodPrefix.Length + 4;
                return frameName.Substring(methodStart, methodEnd - methodStart);
            }
            const string stateMachineMethodPrefix = "<Method";
            methodStart = frameName.IndexOf(stateMachineMethodPrefix, StringComparison.Ordinal);
            if (methodStart >= 0)
            {
                int digitsStart = methodStart + stateMachineMethodPrefix.Length;
                return "TestApp.Workload.Method" + frameName.Substring(digitsStart, 4);
            }
            if (frameName.IndexOf(AsyncProfilerCallStackStitchBoundary.ContinuationWrapperPrefix, StringComparison.Ordinal) >= 0)
            {
                return "Wrapper";
            }
            if (frameName.IndexOf(AsyncProfilerCallStackStitchBoundary.DispatchContinuationsName, StringComparison.Ordinal) >= 0)
            {
                return "DispatchContinuations";
            }
            if (frameName.IndexOf(AsyncProfilerCallStackStitchBoundary.MoveNextAsDispatcherName, StringComparison.Ordinal) >= 0)
            {
                return AsyncProfilerCallStackStitchBoundary.MoveNextAsDispatcherName;
            }
            if (frameName.IndexOf("InstrumentedMoveNext", StringComparison.Ordinal) >= 0)
            {
                return "V1Infrastructure";
            }
            return null;
        }

        private static WriteEventOptions EventOptions(int metadataId, int sequence, long timestamp, int stackId = 0)
        {
            return new WriteEventOptions
            {
                MetadataId = metadataId,
                ThreadIndexOrId = ThreadStreamIndex,
                CaptureThreadIndexOrId = ThreadStreamIndex,
                SequenceNumber = sequence,
                StackId = stackId,
                Timestamp = timestamp,
                IsSorted = true,
            };
        }

        private long ContextStartQpc(int contextIndex) =>
            StartQpc + ((long)contextIndex * QpcFrequency / _contextRatePerSecond);

        private static int AsyncStackIndex(int contextIndex) => contextIndex % DistinctAsyncStackCount;

        private static ulong[][] CreateAsyncFrames(AsyncCallstackKind kind)
        {
            var result = new ulong[DistinctAsyncStackCount][];
            for (int stackIndex = 0; stackIndex < result.Length; stackIndex++)
            {
                result[stackIndex] = new ulong[AsyncFrameCount];
                int firstMethodIndex = FirstMethodIndex(kind, stackIndex);
                for (int frameIndex = 0; frameIndex < AsyncFrameCount; frameIndex++)
                {
                    result[stackIndex][frameIndex] = MethodAddress(firstMethodIndex + frameIndex);
                }
            }
            return result;
        }

        private AsyncCallstackKind KindForContext(int contextIndex)
        {
            switch (KindMode)
            {
                case AsyncProfilerKindMode.V1Only:
                    return AsyncCallstackKind.StateMachineAsync;
                case AsyncProfilerKindMode.Mixed:
                    return ((contextIndex / ContextsPerSample) & 1) == 0
                        ? AsyncCallstackKind.RuntimeAsync
                        : AsyncCallstackKind.StateMachineAsync;
                default:
                    return AsyncCallstackKind.RuntimeAsync;
            }
        }

        private static int FirstMethodIndex(AsyncCallstackKind kind, int asyncStackIndex) =>
            (kind == AsyncCallstackKind.StateMachineAsync ? V1MethodIndexOffset : 0) +
            (asyncStackIndex * AsyncFrameCount);

        private static int StackId(AsyncCallstackKind kind, int asyncStackIndex) =>
            (kind == AsyncCallstackKind.StateMachineAsync ? V1StackIdOffset : 0) + asyncStackIndex + 1;

        private static bool IsUnmatchedSample(int sampleIndex) =>
            (sampleIndex + 1) % UnmatchedSamplePeriod == 0;

        private static string MethodName(int methodIndex) =>
            "TestApp.Workload.Method" + methodIndex.ToString("D4");

        private static string SymbolName(int methodIndex)
        {
            if (methodIndex >= V1MethodIndexOffset &&
                methodIndex < V1MethodIndexOffset + (DistinctAsyncStackCount * AsyncFrameCount))
            {
                return "TestApp.Workload+<Method" + methodIndex.ToString("D4") + ">d__" +
                    methodIndex.ToString() + ".MoveNext";
            }
            return MethodName(methodIndex);
        }

        private static ulong MethodAddress(int methodIndex) => AppMappingStart + ((ulong)methodIndex * AddressStride);

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private enum StackValidationMode
        {
            Sync,
            Stitched,
        }

        private const int ProcessId = 42;
        private const int OsThreadId = 43;
        private const long ThreadStreamIndex = 1;
        private const long QpcFrequency = 10_000_000;
        private const long StartQpc = 1_000_000;
        private const int ContextsPerBuffer = 1_000;
        private const int UnmatchedSamplePeriod = 20;
        private const int ManagedMethodCount = 1_000;
        private const int AsyncFrameCount = 8;
        private const int DistinctAsyncStackCount = 17;
        private const int V1MethodIndexOffset = 200;
        private const int UnmatchedMethodIndex = 900;
        private const int V1StackIdOffset = DistinctAsyncStackCount;
        private const int UnmatchedStackId = (DistinctAsyncStackCount * 2) + 1;
        private const ulong AppMappingStart = 0x100000;
        private const ulong RuntimeMappingStart = 0x200000;
        private const ulong AddressStride = 0x100;
        private const ulong AppMappingId = 1;
        private const ulong RuntimeMappingId = 2;
        private const ulong WrapperAddress = RuntimeMappingStart;
        private const ulong DispatchAddress = RuntimeMappingStart + AddressStride;
        private const ulong V1DispatcherAddress = RuntimeMappingStart + (2 * AddressStride);
        private const ulong V1InfrastructureAddress = RuntimeMappingStart + (3 * AddressStride);
        private const string WrapperName =
            "System.Runtime.CompilerServices.AsyncProfiler+ContinuationWrapper.Continuation_Wrapper_0";
        private const string DispatchName =
            "System.Runtime.CompilerServices.RuntimeAsyncTask.DispatchContinuations";

        private readonly int _contextRatePerSecond;
        private readonly int _durationSeconds;

        #endregion
    }
}
