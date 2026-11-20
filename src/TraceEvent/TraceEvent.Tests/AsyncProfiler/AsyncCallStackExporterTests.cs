// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.Diagnostics.Tracing.Computers;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;
using Microsoft.Diagnostics.Tracing.Stacks.Formats;
using Xunit;

using static Microsoft.Diagnostics.Tracing.Stacks.StackSourceWriterHelper;

namespace TraceEventTests
{
    public class AsyncCallStackExporterTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RecordedProgress_CombinesSimultaneousCompletionsAndClipsOpenContext(bool runtime)
        {
            var context = Context(0, long.MaxValue, new ulong[] { 1, 2, 3, 4, 5 }, runtime: runtime,
                methods: new[] { Delta(5), Delta(20), Delta(20) }, exceptions: new[] { Delta(20) });
            var events = Export(new[] { context }, 10, 40);
            AssertEvents(events,
                (ProfileEventType.Open, 5, 10, 0),
                (ProfileEventType.Open, 4, 10, 1),
                (ProfileEventType.Open, 3, 10, 2),
                (ProfileEventType.Open, 2, 10, 3),
                (ProfileEventType.Close, 2, 20, 3),
                (ProfileEventType.Close, 3, 20, 2),
                (ProfileEventType.Close, 4, 20, 1),
                (ProfileEventType.Close, 5, 40, 0));
        }

        [Fact]
        public void NestedContexts_KeepParentAndApplyProgressAtSharedBoundary()
        {
            var outer = Context(10, 50, new ulong[] { 1, 2 }, methods: new[] { Delta(20) });
            var firstChild = Context(15, 20, new ulong[] { 3 }, depth: 1);
            var secondChild = Context(20, 25, new ulong[] { 4 }, depth: 1);
            var events = Export(new[] { firstChild, outer, secondChild });
            AssertEvents(events,
                (ProfileEventType.Open, 2, 10, 0),
                (ProfileEventType.Open, 1, 10, 1),
                (ProfileEventType.Open, 3, 15, 2),
                (ProfileEventType.Close, 3, 20, 2),
                (ProfileEventType.Close, 1, 20, 1),
                (ProfileEventType.Open, 4, 20, 1),
                (ProfileEventType.Close, 4, 25, 1),
                (ProfileEventType.Close, 2, 50, 0));
        }

        [Fact]
        public void AdjacentIdenticalContexts_KeepOccurrenceBoundariesAndGaps()
        {
            var events = Export(new[]
            {
                Context(10, 20, new ulong[] { 1 }),
                Context(20, 30, new ulong[] { 1 }),
                Context(40, 50, new ulong[] { 1 }),
                Context(60, 60, new ulong[] { 2 }),
                Context(100, 110, new ulong[] { 3 })
            });
            AssertEvents(events,
                (ProfileEventType.Open, 1, 10, 0),
                (ProfileEventType.Close, 1, 20, 0),
                (ProfileEventType.Open, 1, 20, 0),
                (ProfileEventType.Close, 1, 30, 0),
                (ProfileEventType.Open, 1, 40, 0),
                (ProfileEventType.Close, 1, 50, 0));
        }

        [Fact]
        public void V1FinalizedAppends_AreVisibleFromResumeWithoutInventedWrapperProgress()
        {
            var builder = new AsyncProfilerIndexBuilder();
            builder.Process(new AsyncProfilerBufferBuilder(123, startQpc: 1).Armed(1)
                .ResumeStack(10, 1, new ulong[] { 1 })
                .WrapperReset(15)
                .AppendStack(20, 1, new ulong[] { 2, 0 })
                .Suspend(30).Build());
            var contexts = builder.Index.GetCallStacks(new AsyncProfilerThreadKey(0, 123));
            AssertEvents(Export(contexts),
                (ProfileEventType.Open, 2, 10, 0),
                (ProfileEventType.Open, 1, 10, 1),
                (ProfileEventType.Close, 1, 30, 1),
                (ProfileEventType.Close, 2, 30, 0));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void HiddenZeroIPs_PreserveOriginalSlotsForMethodAndUnwindProgress(bool runtime)
        {
            var context = Context(10, 50, new ulong[] { 0, 1, 0, 2, 0 }, runtime: runtime,
                methods: new[] { Delta(20), Delta(40) }, exceptions: new[] { Delta(30, 2) });
            AssertEvents(Export(new[] { context }),
                (ProfileEventType.Open, 2, 10, 0),
                (ProfileEventType.Open, 1, 10, 1),
                (ProfileEventType.Close, 1, 30, 1),
                (ProfileEventType.Close, 2, 40, 0));
            Assert.Equal(5, context.CapturedFrames.Count);
            Assert.Equal(1, context.GetCompletedFrameCount(20));
            Assert.Equal(3, context.GetCompletedFrameCount(30));
            Assert.Equal(4, context.GetCompletedFrameCount(40));
        }

        [Fact]
        public void ZeroIPOnlyContexts_DoNotEmitFramesOrHideNestedNonzeroFrames()
        {
            var outer = Context(10, 50, new ulong[] { 0, 0 });
            Assert.Empty(Export(new[] { outer }));
            AssertEvents(Export(new[] { outer, Context(20, 30, new ulong[] { 1 }, depth: 1) }),
                (ProfileEventType.Open, 1, 20, 0),
                (ProfileEventType.Close, 1, 30, 0));
        }

        [Fact]
        public void ExcessRecordedCompletions_DoNotInventFrames()
        {
            var context = Context(10, 50, new ulong[] { 1 }, exceptions: new[] { Delta(20, 3) });
            AssertEvents(Export(new[] { context }),
                (ProfileEventType.Open, 1, 10, 0),
                (ProfileEventType.Close, 1, 20, 0));
        }

        [Fact]
        public void OverlappingSameDepthContexts_AreRejected()
        {
            Assert.Throws<InvalidDataException>(() => Export(new[]
            {
                Context(10, 30, new ulong[] { 1 }),
                Context(20, 40, new ulong[] { 2 })
            }));
        }

        [Fact]
        public void VirtualSampling_UsesSharedTraceGridSkipsShortContextsAndClipsFinalInterval()
        {
            AssertEvents(Export(new[]
            {
                Context(104, 108, new ulong[] { 1 }),
                Context(109, 113, new ulong[] { 2 }),
                Context(113, 114, new ulong[] { 3 }),
                Context(132, long.MaxValue, new ulong[] { 4 })
            }, start: 103, end: 138, interval: 10),
                (ProfileEventType.Open, 3, 113, 0),
                (ProfileEventType.Close, 3, 123, 0),
                (ProfileEventType.Open, 4, 133, 0),
                (ProfileEventType.Close, 4, 138, 0));
            AssertEvents(Export(new[] { Context(114, 124, new ulong[] { 5 }) },
                start: 103, end: 138, interval: 10),
                (ProfileEventType.Open, 5, 123, 0),
                (ProfileEventType.Close, 5, 133, 0));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void VirtualSampling_AppliesRecordedProgressAndNestingAtSampleInstant(bool runtime)
        {
            var outer = Context(0, 95, new ulong[] { 0, 1, 0, 2, 3 }, runtime: runtime,
                methods: new[] { Delta(15), Delta(30) }, exceptions: new[] { Delta(25, 2) });
            var child = Context(22, 35, new ulong[] { 4 }, depth: 1, runtime: runtime);
            AssertEvents(Export(new[] { outer, child }, end: 47, interval: 10),
                (ProfileEventType.Open, 3, 0, 0),
                (ProfileEventType.Open, 2, 0, 1),
                (ProfileEventType.Open, 1, 0, 2),
                (ProfileEventType.Close, 1, 30, 2),
                (ProfileEventType.Close, 2, 30, 1),
                (ProfileEventType.Open, 4, 30, 1),
                (ProfileEventType.Close, 4, 40, 1),
                (ProfileEventType.Close, 3, 47, 0));
        }

        [Fact]
        public void VirtualSampling_IntervalLongerThanTraceObservesOnlyTraceStart()
        {
            Assert.Empty(Export(new[] { Context(1, 4, new ulong[] { 1 }) }, end: 5, interval: 20));
            AssertEvents(Export(new[] { Context(0, 4, new ulong[] { 1 }) }, end: 5, interval: 20),
                (ProfileEventType.Open, 1, 0, 0),
                (ProfileEventType.Close, 1, 5, 0));
            Assert.Empty(Export(new[] { Context(0, 4, new ulong[] { 0 }) }, end: 5, interval: 20));
        }

        [Fact]
        public void VirtualSampling_UsesIntegerQpcGridNearClockLimit()
        {
            long start = long.MaxValue - 50;
            var events = SpeedScopeStackSourceWriter.GetAsyncProfileEvents(
                new[] { Context(start + 1, long.MaxValue, new ulong[] { 1 }) },
                start, start + 47, qpc => qpc - start,
                (context, slot) => (int)context.Frames.InstructionPointerAt(slot), sampleIntervalQpc: 20);
            AssertEvents(events,
                (ProfileEventType.Open, 1, 20, 0),
                (ProfileEventType.Close, 1, 47, 0));
        }

        private const long Start = 100_000;
        private const int Process = 1234;
        private const int OtherProcess = 5678;
        private const ulong GhostThread = 6000;
        private const string MoveNext = "Namespace.Scenarios+<DeepLevel1Async>d__8::MoveNext()";
        private const string GenericMoveNext = "System.Void Namespace.Outer`1+Nested+<RunAsync>d_4`1[System.Int64]::MoveNext()";
        private const string LogicalName = "[Async V1] testapp!Namespace.Scenarios.DeepLevel1Async [ordinal 8, state 4]";
        private const string GenericLogicalName = "[Async V1] testapp!Namespace.Outer`1.Nested.RunAsync [ordinal 4`1[System.Int64], state 3]";

        [Fact]
        public void PersistedIndex_ExportsAllProcessesWithoutSamplesOrRawEvents()
        {
            using (var trace = new ExportTrace())
            {
                TraceLog log = trace.Log;
                Assert.True(log.HasAsyncProfilerEvents);
                Assert.False(log.IsAsyncProfilerCallStacksLoaded);
                Assert.Single(log.Events.Where(e => e.ProviderName == "TimelineAnchor"));
                Assert.DoesNotContain(log.Threads, t => (ulong)t.ThreadID == GhostThread);
                SpeedScopeStackSourceWriter.WriteAsyncCallStacksAsJson(log, trace.Output);
                using (JsonDocument json = trace.ReadJson())
                {
                    Assert.Equal(3, json.RootElement.GetProperty("profiles").GetArrayLength());
                    ValidateJson(json);
                }
            }
        }

        [Fact]
        public void PersistedIndex_PerProcessExportResolvesLogicalNamesAndCompletions()
        {
            using (var trace = new ExportTrace())
            {
                TraceLog log = trace.Log;
                SpeedScopeStackSourceWriter.WriteAsyncCallStacksAsJson(log, trace.Output, Process);
                using (JsonDocument json = trace.ReadJson())
                {
                    ValidateJson(json);
                    var root = json.RootElement;
                    var profiles = root.GetProperty("profiles").EnumerateArray().ToArray();
                    Assert.Equal(2, profiles.Length);
                    Assert.All(profiles, p => Assert.Contains($"({Process}, instance ", p.GetProperty("name").GetString()));
                    var ghost = profiles.Single(p => p.GetProperty("name").GetString().Contains("Thread (6000)"));
                    var frames = root.GetProperty("shared").GetProperty("frames");
                    string Name(JsonElement e) => frames[e.GetProperty("frame").GetInt32()].GetProperty("name").GetString();
                    var events = ghost.GetProperty("events").EnumerateArray().ToArray();
                    Assert.Equal(LogicalName, Name(events[0]));
                    Assert.Equal(GenericLogicalName, Name(events[1]));
                    Assert.Equal(log.QPCTimeToRelMSec(Start + 10),
                        events.Single(e => e.GetProperty("type").GetString() == "O" &&
                            Name(e) == LogicalName).GetProperty("at").GetDouble());
                    Assert.DoesNotContain(events, e => e.GetProperty("at").GetDouble() ==
                        log.QPCTimeToRelMSec(Start + 20));
                    Assert.Equal(log.QPCTimeToRelMSec(Start + 30),
                        events.Single(e => e.GetProperty("type").GetString() == "C" &&
                            Name(e) == GenericLogicalName).GetProperty("at").GetDouble());
                    Assert.Contains("[Async V1] AsyncFrame(0xd0) [captured state 0]",
                        frames.EnumerateArray().Select(f => f.GetProperty("name").GetString()));
                    Assert.Contains("[Async V2] testapp!" + MoveNext,
                        frames.EnumerateArray().Select(f => f.GetProperty("name").GetString()));
                    Assert.Equal(log.QPCTimeToRelMSec(log.sessionEndTimeQPC),
                        events.Last().GetProperty("at").GetDouble());
                }
            }
        }

        [Fact]
        public void PersistedIndex_VirtualIntervalExportSamplesActiveFramesWithoutChangingExactExport()
        {
            using (var trace = new ExportTrace())
            {
                TraceLog log = trace.Log;
                SpeedScopeStackSourceWriter.WriteAsyncCallStacksAsJson(log, trace.Output, Process);
                string exact = File.ReadAllText(trace.Output);

                var interval = TimeSpan.FromSeconds(20.25 / log.QPCFreq);
                SpeedScopeStackSourceWriter.WriteAsyncCallStacksAsJson(log, trace.Output, interval, Process);
                using (JsonDocument json = trace.ReadJson())
                {
                    ValidateJson(json);
                    var root = json.RootElement;
                    var profiles = root.GetProperty("profiles").EnumerateArray().ToArray();
                    Assert.Equal(2, profiles.Length);
                    Assert.All(profiles, p => Assert.Contains("(virtual 20 ms)", p.GetProperty("name").GetString()));
                    var ghost = profiles.Single(p => p.GetProperty("name").GetString().Contains("Thread (6000)"));
                    var frames = root.GetProperty("shared").GetProperty("frames");
                    string Name(JsonElement e) => frames[e.GetProperty("frame").GetInt32()].GetProperty("name").GetString();
                    var events = ghost.GetProperty("events").EnumerateArray().ToArray();
                    Assert.Equal(new[]
                    {
                        log.QPCTimeToRelMSec(Start + 20), log.QPCTimeToRelMSec(Start + 20),
                        log.QPCTimeToRelMSec(Start + 40), log.QPCTimeToRelMSec(Start + 40),
                        log.QPCTimeToRelMSec(Start + 60), log.QPCTimeToRelMSec(Start + 90)
                    }, events.Select(e => e.GetProperty("at").GetDouble()));
                    Assert.Equal(LogicalName, Name(events[0]));
                    Assert.Equal(GenericLogicalName, Name(events[1]));
                    Assert.Contains("AsyncFrame(0xd0)", Name(events[4]));
                    var normal = profiles.Single(p => p.GetProperty("name").GetString().Contains("Thread (5000)"));
                    Assert.Equal(new[] { log.QPCTimeToRelMSec(Start + 20), log.QPCTimeToRelMSec(Start + 60) },
                        normal.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("at").GetDouble()));
                }

                // An interval longer than the trace samples nothing.
                SpeedScopeStackSourceWriter.WriteAsyncCallStacksAsJson(log, trace.Output, TimeSpan.FromSeconds(1), Process);
                using (JsonDocument json = trace.ReadJson())
                {
                    Assert.Empty(json.RootElement.GetProperty("profiles").EnumerateArray());
                }

                SpeedScopeStackSourceWriter.WriteAsyncCallStacksAsJson(log, trace.Output, Process);
                Assert.Equal(exact, File.ReadAllText(trace.Output));
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-TimeSpan.TicksPerSecond)]
        [InlineData(1)]
        public void PersistedIndex_VirtualIntervalExportRejectsInvalidIntervals(long intervalTicks)
        {
            using (var trace = new ExportTrace())
            {
                Assert.Throws<ArgumentOutOfRangeException>(() =>
                    SpeedScopeStackSourceWriter.WriteAsyncCallStacksAsJson(trace.Log, trace.Output, TimeSpan.FromTicks(intervalTicks)));
            }
        }

        [Fact]
        public void PersistedIndex_ExportReloadsReleasedIndexAndFiltersByProcess()
        {
            using (var trace = new ExportTrace())
            {
                TraceLog log = trace.Log;
                SpeedScopeStackSourceWriter.WriteAsyncCallStacksAsJson(log, trace.Output);
                Assert.True(log.ReleaseAsyncProfilerCallStacks());
                Assert.False(log.IsAsyncProfilerCallStacksLoaded);

                SpeedScopeStackSourceWriter.WriteAsyncCallStacksAsJson(log, trace.Output, OtherProcess);
                using (JsonDocument json = trace.ReadJson())
                {
                    var profile = Assert.Single(json.RootElement.GetProperty("profiles").EnumerateArray());
                    Assert.Contains($"({OtherProcess}, instance ", profile.GetProperty("name").GetString());
                    Assert.Contains("Thread (6000)", profile.GetProperty("name").GetString());
                    var frames = json.RootElement.GetProperty("shared").GetProperty("frames");
                    Assert.Equal(new[]
                    {
                        log.QPCTimeToRelMSec(Start + 15), log.QPCTimeToRelMSec(Start + 25),
                    }, profile.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("at").GetDouble()));
                    Assert.All(profile.GetProperty("events").EnumerateArray(), e =>
                        Assert.Contains("AsyncFrame(0xe0)", frames[e.GetProperty("frame").GetInt32()].GetProperty("name").GetString()));
                    ValidateJson(json);
                }

                SpeedScopeStackSourceWriter.WriteAsyncCallStacksAsJson(log, trace.Output, 9999);
                using (JsonDocument json = trace.ReadJson())
                {
                    Assert.Empty(json.RootElement.GetProperty("profiles").EnumerateArray());
                }
            }
        }

        #region private

        /// <summary>
        /// Converts a nettrace containing only async-profiler buffers, symbol metadata and a timeline anchor:
        /// process <see cref="Process"/> thread 5000 (runtime async), ghost thread 6000 without a TraceThread
        /// (V1 frames with completions, exception unwind and an unresolved frame), and thread 6000 in
        /// <see cref="OtherProcess"/>.
        /// </summary>
        private sealed class ExportTrace : IDisposable
        {
            private readonly string m_input;
            private readonly string m_etlx;

            public ExportTrace()
            {
                var universalSystemProvider = new Guid("8c107b6c-79f8-5231-4de6-2a0e20a3f562");
                var buffers = new[]
                {
                    new AsyncProfilerBufferBuilder(5000, startQpc: Start).Armed(Start)
                        .ResumeRuntimeStack(Start + 12, 1, new ulong[] { 0xA0 })
                        .SuspendRuntime(Start + 50).Build(),
                    new AsyncProfilerBufferBuilder(GhostThread, startQpc: Start).Armed(Start)
                        .ResumeStack(Start + 10, 1, new ulong[] { 0, 0xB0 }, new[] { 2, 3 })
                        .CompleteMethod(Start + 20)
                        .AppendStack(Start + 25, 1, new ulong[] { 0xC0 }, new[] { 4 })
                        .UnwindException(Start + 30, 1)
                        .Suspend(Start + 40)
                        .ResumeStack(Start + 45, 2, new ulong[] { 0xD0 }).Build(),
                    new AsyncProfilerBufferBuilder(GhostThread, startQpc: Start).Armed(Start)
                        .ResumeRuntimeStack(Start + 15, 1, new ulong[] { 0xE0 })
                        .SuspendRuntime(Start + 25).Build()
                };
                var writer = new EventPipeWriterV6();
                writer.WriteHeaders(null, syncTimeQpc: Start);
                writer.WriteMetadataBlock(
                    new EventMetadata(1, AsyncProfilerTraceEventParser.ProviderName,
                        "AsyncEvents", AsyncProfilerTraceEventParser.AsyncEventsEventId),
                    new EventMetadata(2, "TimelineAnchor", "End", 1),
                    new EventMetadata(3, "Universal.System", "ProcessMapping", 3)
                    {
                        ProviderId = universalSystemProvider,
                    },
                    new EventMetadata(4, "Universal.System", "ProcessSymbol", 4)
                    {
                        ProviderId = universalSystemProvider,
                    });
                writer.WriteThreadBlock(w =>
                {
                    w.WriteThreadEntry(7, threadId: 5000, processId: Process);
                    w.WriteThreadEntry(8, threadId: 7000, processId: OtherProcess);
                });
                writer.WriteEventBlock(w =>
                {
                    w.WriteEventBlob(new WriteEventOptions
                    {
                        MetadataId = 3, ThreadIndexOrId = 7, CaptureThreadIndexOrId = 7,
                        SequenceNumber = 1, Timestamp = Start + 1, IsSorted = true
                    }, p => p.WriteProcessMappingPayload(1, 1, 0x100, "testapp.dll"));
                    for (int i = 0; i < buffers.Length; i++)
                    {
                        int slot = i;
                        w.WriteEventBlob(new WriteEventOptions
                        {
                            MetadataId = 1,
                            ThreadIndexOrId = i == 2 ? 8 : 7,
                            CaptureThreadIndexOrId = i == 2 ? 8 : 7,
                            SequenceNumber = i + 2,
                            Timestamp = Start + 60 + i * 10,
                            IsSorted = true
                        }, p => p.Write(AsyncProfilerTestData.AsyncEventsPayload(buffers[slot])));
                    }
                    var symbols = new (ulong Address, string Name)[]
                    {
                        (0xA0, MoveNext), (0xB0, GenericMoveNext), (0xC0, MoveNext)
                    };
                    for (int i = 0; i < symbols.Length; i++)
                    {
                        var symbol = symbols[i];
                        w.WriteEventBlob(new WriteEventOptions
                        {
                            MetadataId = 4, ThreadIndexOrId = 7, CaptureThreadIndexOrId = 7,
                            SequenceNumber = 5 + i, Timestamp = Start + 81 + i, IsSorted = true
                        }, p => p.WriteProcessSymbolPayload(symbol.Address, 1, symbol.Address, symbol.Name));
                    }
                    w.WriteEventBlob(new WriteEventOptions
                    {
                        MetadataId = 2,
                        ThreadIndexOrId = 7,
                        CaptureThreadIndexOrId = 7,
                        SequenceNumber = 8,
                        Timestamp = Start + 90,
                        IsSorted = true
                    }, p => { });
                });
                writer.WriteEndBlock();

                m_input = Path.Combine(Path.GetTempPath(), $"async_export_{Guid.NewGuid():N}.nettrace");
                Output = Path.ChangeExtension(m_input, ".speedscope.json");
                File.WriteAllBytes(m_input, writer.ToArray());
                m_etlx = TraceLog.CreateFromEventPipeDataFile(m_input);
                Log = new TraceLog(m_etlx);
            }

            public TraceLog Log { get; }

            public string Output { get; }

            public JsonDocument ReadJson() => JsonDocument.Parse(File.ReadAllText(Output));

            public void Dispose()
            {
                Log.Dispose();
                File.Delete(m_input);
                File.Delete(Output);
                File.Delete(m_etlx);
            }
        }

        private static AsyncProfilerCallStack Context(long start, long end, ulong[] frames, int depth = 0,
            bool runtime = false, AsyncProfilerCallStack.CompletionDelta[] methods = null,
            AsyncProfilerCallStack.CompletionDelta[] exceptions = null) =>
            AsyncProfilerStackTestData.CallStack(
                new AsyncProfilerCallStackFrames(runtime ? AsyncCallstackKind.RuntimeAsync : AsyncCallstackKind.StateMachineAsync,
                    frames, runtime ? null : new int[frames.Length]),
                depth, start, end, methods, exceptions);

        private static AsyncProfilerCallStack.CompletionDelta Delta(long qpc, int count = 1) =>
            new AsyncProfilerCallStack.CompletionDelta(qpc, count);

        private static IReadOnlyList<ProfileEvent> Export(IReadOnlyList<AsyncProfilerCallStack> contexts,
            long start = 0, long end = 100, long interval = 0) =>
            SpeedScopeStackSourceWriter.GetAsyncProfileEvents(contexts, start, end,
                qpc => qpc, (context, slot) => (int)context.Frames.InstructionPointerAt(slot), interval);

        private static void AssertEvents(IReadOnlyList<ProfileEvent> actual,
            params (ProfileEventType Type, int Frame, double Time, int Depth)[] expected)
        {
            Assert.True(Validate(actual));
            Assert.Equal(expected, actual.Select(e => (e.Type, e.FrameId, e.RelativeTime, e.Depth)));
        }

        private static void ValidateJson(JsonDocument document)
        {
            var frames = document.RootElement.GetProperty("shared").GetProperty("frames");
            int frameCount = frames.GetArrayLength();
            Assert.DoesNotContain(frames.EnumerateArray(),
                frame => frame.GetProperty("name").GetString().Contains("AsyncFrame(0x0)"));
            var profiles = document.RootElement.GetProperty("profiles").EnumerateArray().ToArray();
            Assert.Equal(profiles.Length, profiles.Select(p => p.GetProperty("name").GetString()).Distinct().Count());
            foreach (var profile in profiles)
            {
                double previous = profile.GetProperty("startValue").GetDouble();
                double end = profile.GetProperty("endValue").GetDouble();
                var stack = new Stack<int>();
                var events = profile.GetProperty("events").EnumerateArray().ToArray();
                Assert.NotEmpty(events);
                foreach (var e in events)
                {
                    int frame = e.GetProperty("frame").GetInt32();
                    double time = e.GetProperty("at").GetDouble();
                    Assert.InRange(frame, 0, frameCount - 1);
                    Assert.InRange(time, previous, end);
                    previous = time;
                    if (e.GetProperty("type").GetString() == "O")
                    {
                        stack.Push(frame);
                    }
                    else
                    {
                        Assert.Equal("C", e.GetProperty("type").GetString());
                        Assert.NotEmpty(stack);
                        Assert.Equal(frame, stack.Pop());
                    }
                }
                Assert.Empty(stack);
            }
        }

        #endregion
    }
}
