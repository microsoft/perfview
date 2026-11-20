using System;
using System.IO;

using BenchmarkDotNet.Attributes;

using FastSerialization;

using Microsoft.Diagnostics.Tracing.Computers;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;

using TraceEventTests;

namespace TraceEventBenchmarks
{
    /// <summary>Separates buffer decoding, call-stack index construction, and serialization across input sizes and frame reuse.</summary>
    [MemoryDiagnoser]
    public class AsyncProfilerIndexBuildBenchmarks
    {
        /// <summary>Number of call stacks in each prepared input buffer.</summary>
        [Params(1_000, 10_000)]
        public int EpisodeCount { get; set; }

        /// <summary>Captured frame count per call stack.</summary>
        [Params(4, 16)]
        public int FrameCount { get; set; }

        /// <summary>Instrumentation family, including per-frame compiler states for state-machine (V1) async.</summary>
        [Params(AsyncProfilerIndexBuildKind.RuntimeAsync, AsyncProfilerIndexBuildKind.StateMachineAsync)]
        public AsyncProfilerIndexBuildKind Kind { get; set; }

        /// <summary>Whether each call stack has distinct frames or shares one interned frame sequence.</summary>
        [Params(false, true)]
        public bool UniqueCallstacks { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            var kind = Kind == AsyncProfilerIndexBuildKind.RuntimeAsync
                ? AsyncCallstackKind.RuntimeAsync : AsyncCallstackKind.StateMachineAsync;
            _buffer = BuildBuffer(EpisodeCount, FrameCount, kind, UniqueCallstacks, includeDispatcherInfo: false);
            _bufferWithDispatcherInfo = BuildBuffer(
                EpisodeCount, FrameCount, kind, UniqueCallstacks, includeDispatcherInfo: true);
            _manifest = new Manifest();
            _sink = new AsyncProfilerCountingSink();

            var computer = new AsyncProfilerIndexBuilder();
            computer.Process(_buffer, (ProcessIndex)1);
            computer.Finish();
            _index = computer.Index;
            ValidateIndex(computer, includeDispatcherInfo: false);

            var withDispatcherInfo = new AsyncProfilerIndexBuilder();
            withDispatcherInfo.Process(_bufferWithDispatcherInfo, (ProcessIndex)1);
            withDispatcherInfo.Finish();
            ValidateIndex(withDispatcherInfo, includeDispatcherInfo: true);
            if (Decode() != EpisodeCount * (FrameCount + 2) + 2 || _sink.ParseErrorCount != 0)
            {
                throw new InvalidOperationException("The decoder benchmark did not consume the expected events and frames.");
            }
        }

        /// <summary>Decodes one prepared buffer into a counting sink without constructing a call-stack index.</summary>
        [Benchmark]
        public int Decode()
        {
            _sink.Reset();
            AsyncProfilerTraceEventParser.ParseBuffer(_buffer, _manifest, _sink);
            return _sink.SubEventCount + _sink.FrameCount;
        }

        /// <summary>Decodes the buffer and finalizes a fresh index, including allocation and frame interning.</summary>
        [Benchmark]
        public int BuildIndex()
        {
            var computer = new AsyncProfilerIndexBuilder();
            computer.Process(_buffer, (ProcessIndex)1);
            computer.Finish();
            return computer.DistinctFramesCount;
        }

        /// <summary>Builds a fresh index from a buffer that also contains dispatcher-creation relationships.</summary>
        [Benchmark]
        public int BuildIndexWithDispatcherInfo()
        {
            var computer = new AsyncProfilerIndexBuilder();
            computer.Process(_bufferWithDispatcherInfo, (ProcessIndex)1);
            computer.Finish();
            return computer.DistinctFramesCount;
        }

        /// <summary>Serializes a prebuilt index into a new memory stream, excluding event decoding and file I/O.</summary>
        [Benchmark]
        public long SerializeIndex()
        {
            SerializationSettings settings = SerializationSettings.Default.WithStreamLabelWidth(StreamLabelWidth.EightBytes);
            using (var stream = new MemoryStream())
            {
                using (var serializer = new Serializer(new IOStreamStreamWriter(stream, settings, leaveOpen: true), _index))
                {
                }

                return stream.Length;
            }
        }

        #region private

        private void ValidateIndex(AsyncProfilerIndexBuilder computer, bool includeDispatcherInfo)
        {
            var thread = new AsyncProfilerThreadKey((ProcessIndex)1, AsyncProfilerTestData.DefaultOsThreadId);
            if (computer.ParseErrorCount != 0 ||
                computer.Index.GetCallStacks(thread).Count != EpisodeCount ||
                computer.DistinctFramesCount != (UniqueCallstacks ? EpisodeCount : 1) ||
                computer.Index.HasDispatcherRecords != includeDispatcherInfo ||
                computer.GetFrames((AsyncProfilerCallStackFramesIndex)0).FrameCount != FrameCount)
            {
                throw new InvalidOperationException("The index-build benchmark did not produce the expected call stacks and frame storage.");
            }
        }

        private static byte[] BuildBuffer(int episodeCount, int frameCount, AsyncCallstackKind kind,
            bool uniqueCallstacks, bool includeDispatcherInfo)
        {
            const long startQpc = 1_000_000;
            var builder = new AsyncProfilerBufferBuilder(startQpc: startQpc)
                .Metadata(startQpc, 10_000_000, startQpc, 1, 0, 32, Array.Empty<AsyncManifestEntry>())
                .ContextNoPayload(AsyncEventID.ResetAsyncThreadContext, startQpc);
            var methodIds = new ulong[frameCount];
            var states = kind == AsyncCallstackKind.StateMachineAsync ? new int[frameCount] : null;
            long timestampQpc = startQpc;

            for (int episode = 0; episode < episodeCount; episode++)
            {
                ulong methodBase = uniqueCallstacks ? 0x1000UL + (ulong)episode * 0x100UL : 0x1000UL;
                for (int frame = 0; frame < frameCount; frame++)
                {
                    methodIds[frame] = methodBase + (ulong)frame * 0x10UL;
                    if (states != null)
                    {
                        states[frame] = frame & 3;
                    }
                }

                if (includeDispatcherInfo)
                {
                    timestampQpc++;
                    builder.CreateContext(
                        kind == AsyncCallstackKind.RuntimeAsync
                            ? AsyncEventID.CreateRuntimeAsyncContext
                            : AsyncEventID.CreateStateMachineAsyncContext,
                        timestampQpc, parent: episode == 0 ? 0UL : (ulong)episode,
                        dispatcher: (ulong)episode + 1);
                }

                timestampQpc++;
                builder.Callstack(
                    kind == AsyncCallstackKind.RuntimeAsync
                        ? AsyncEventID.ResumeRuntimeAsyncCallstack
                        : AsyncEventID.ResumeStateMachineAsyncCallstack,
                    timestampQpc, continuationIndex: (byte)(episode & 31), parentDispatcherId: 0,
                    dispatcherId: (ulong)episode + 1, methodIds, states);

                timestampQpc++;
                builder.ContextNoPayload(
                    kind == AsyncCallstackKind.RuntimeAsync
                        ? AsyncEventID.SuspendRuntimeAsyncContext
                        : AsyncEventID.SuspendStateMachineAsyncContext,
                    timestampQpc);
            }

            return builder.Build();
        }

        private byte[] _buffer;
        private byte[] _bufferWithDispatcherInfo;
        private Manifest _manifest;
        private AsyncProfilerCountingSink _sink;
        private AsyncProfilerCallStacksIndex _index;

        #endregion
    }

    /// <summary>Async call-stack instrumentation family used by <see cref="AsyncProfilerIndexBuildBenchmarks"/>.</summary>
    public enum AsyncProfilerIndexBuildKind
    {
        RuntimeAsync,
        StateMachineAsync,
    }

    /// <summary>Measures V1 index construction when each captured stack is assembled from resume and append chunks.</summary>
    [MemoryDiagnoser]
    public class AsyncProfilerStateMachineAppendBenchmarks
    {
        /// <summary>Number of incrementally assembled call stacks in each prepared buffer.</summary>
        [Params(1_000, 10_000)]
        public int EpisodeCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            const long startQpc = 1_000_000;
            var builder = new AsyncProfilerBufferBuilder(startQpc: startQpc)
                .Metadata(startQpc, 10_000_000, startQpc, 1, 0, 32, Array.Empty<AsyncManifestEntry>())
                .ContextNoPayload(AsyncEventID.ResetAsyncThreadContext, startQpc);
            long timestampQpc = startQpc;

            for (int episode = 0; episode < EpisodeCount; episode++)
            {
                ulong dispatcherId = (ulong)episode + 1;
                for (int chunk = 0; chunk < 4; chunk++)
                {
                    var methods = new ulong[4];
                    var states = new int[4];
                    for (int frame = 0; frame < methods.Length; frame++)
                    {
                        methods[frame] = 0x1000UL + (ulong)(chunk * methods.Length + frame) * 0x10UL;
                        states[frame] = frame & 3;
                    }

                    timestampQpc++;
                    builder.Callstack(
                        chunk == 0
                            ? AsyncEventID.ResumeStateMachineAsyncCallstack
                            : AsyncEventID.AppendStateMachineAsyncCallstack,
                        timestampQpc, continuationIndex: (byte)(episode & 31), parentDispatcherId: 0,
                        dispatcherId, methods, states);
                }

                timestampQpc++;
                builder.ContextNoPayload(AsyncEventID.SuspendStateMachineAsyncContext, timestampQpc);
            }

            _buffer = builder.Build();
            var computer = new AsyncProfilerIndexBuilder();
            computer.Process(_buffer, (ProcessIndex)1);
            computer.Finish();
            var thread = new AsyncProfilerThreadKey((ProcessIndex)1, AsyncProfilerTestData.DefaultOsThreadId);
            if (computer.ParseErrorCount != 0 ||
                computer.Index.GetCallStacks(thread).Count != EpisodeCount ||
                computer.DistinctFramesCount != 1 ||
                computer.GetFrames((AsyncProfilerCallStackFramesIndex)0).FrameCount != 16)
            {
                throw new InvalidOperationException("The append benchmark did not assemble the expected sixteen-frame call stacks.");
            }
        }

        /// <summary>Decodes the buffer and finalizes a fresh index, including incremental frame assembly and interning.</summary>
        [Benchmark]
        public int BuildIndex()
        {
            var computer = new AsyncProfilerIndexBuilder();
            computer.Process(_buffer, (ProcessIndex)1);
            computer.Finish();
            return computer.DistinctFramesCount;
        }

        #region private

        private byte[] _buffer;

        #endregion
    }
}
