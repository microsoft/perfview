using FastSerialization;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.EventPipe;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using static Microsoft.Diagnostics.Tracing.Etlx.TraceLog;

namespace TraceEventTests
{
    public class EventPipeParsing : EventPipeTestBase
    {
        private class EventRecord
        {
            public int TotalCount;
            public string FirstSerializedSample;
        }

        private class EventStatistics
        {
            public SortedDictionary<string, EventRecord> Records = new SortedDictionary<string, EventRecord>(StringComparer.Ordinal);

            public void Record(TraceEvent data) => Record(data.ProviderName + "/" + data.EventName, data);

            public void Record(string eventName, TraceEvent data)
            {
                if (Records.ContainsKey(eventName))
                {
                    Records[eventName].TotalCount++;
                }
                else
                {
                    Records[eventName] = new EventRecord()
                    {
                        TotalCount = 1,
                        FirstSerializedSample = new String(data.ToString().Replace("\n", "\\n").Replace("\r", "\\r").Take(1000).ToArray())
                    };
                }
            }

            override
            public string ToString()
            {
                StringBuilder sb = new StringBuilder(1024 * 1024);
                foreach (var item in Records)
                {
                    sb.AppendLine($"{item.Key}, {item.Value.TotalCount}, {item.Value.FirstSerializedSample}");
                }

                return sb.ToString();
            }
        }

        public EventPipeParsing(ITestOutputHelper output)
            : base(output)
        {
        }

#if NETCOREAPP3_0_OR_GREATER
        [Theory(Skip = "Snapshot difs due to increased float accuracy on newer .NET versions.")]
#else
        [Theory]
#endif
        [MemberData(nameof(TestEventPipeFiles))]
        public void Basic(string eventPipeFileName)
        {
            // Initialize
            PrepareTestData();

            string eventPipeFilePath = Path.Combine(UnZippedDataDir, eventPipeFileName);

            Output.WriteLine(string.Format("Processing the file {0}, Making ETLX and scanning.", Path.GetFullPath(eventPipeFilePath)));
            var eventStatistics = new EventStatistics();

            using (var traceLog = new TraceLog(TraceLog.CreateFromEventPipeDataFile(eventPipeFilePath)))
            {
                var traceSource = traceLog.Events.GetSource();

                traceSource.AllEvents += eventStatistics.Record;

                // Process
                traceSource.Process();
            }
            // Validate
            ValidateEventStatistics(eventStatistics, eventPipeFileName);
        }

        [Theory()]
        [MemberData(nameof(StreamableTestEventPipeFiles))]
        public void Streaming(string eventPipeFileName)
        {
            // Initialize
            PrepareTestData();

            string eventPipeFilePath = Path.Combine(UnZippedDataDir, eventPipeFileName);
            Output.WriteLine(string.Format("Processing the file {0}", Path.GetFullPath(eventPipeFilePath)));
            var eventStatistics = new EventStatistics();

            long curStreamPosition = 0;
            using (MockStreamingOnlyStream s = new MockStreamingOnlyStream(new FileStream(eventPipeFilePath, FileMode.Open, FileAccess.Read, FileShare.Read)))
            {
                using (var traceSource = new EventPipeEventSource(s))
                {
                    Action<TraceEvent> handler = delegate (TraceEvent data)
                    {
                        long newStreamPosition = s.TestOnlyPosition;
                        // Empirically these files have event blocks of no more than 103K bytes each
                        // The streaming code should never need to read ahead beyond the end of the current
                        // block to read the events
                        Assert.InRange(newStreamPosition, curStreamPosition, curStreamPosition + 103_000);
                        curStreamPosition = newStreamPosition;

                        string eventName = data.ProviderName + "/" + data.EventName;

                        // For whatever reason the parse filtering below produces a couple extra events
                        // that TraceLog managed to filter out:
                        //    Microsoft-Windows-DotNETRuntime/Method, 2,
                        //    Microsoft-Windows-DotNETRuntimeRundown/Method, 26103, ...
                        // I haven't had an opportunity to investigate and its probably not a big
                        // deal so just hacking around it for the moment
                        if (eventName == "Microsoft-Windows-DotNETRuntimeRundown/Method" ||
                            eventName == "Microsoft-Windows-DotNETRuntime/Method")
                            return;

                        eventStatistics.Record(eventName, data);
                    };

                    // this is somewhat arbitrary looking set of parser event callbacks empirically
                    // produces the same set of events as TraceLog.Events.GetSource().AllEvents so
                    // that the baseline files can be reused from the Basic test
                    var rundown = new ClrRundownTraceEventParser(traceSource);
                    rundown.LoaderAppDomainDCStop += handler;
                    rundown.LoaderAssemblyDCStop += handler;
                    rundown.LoaderDomainModuleDCStop += handler;
                    rundown.LoaderModuleDCStop += handler;
                    rundown.MethodDCStopComplete += handler;
                    rundown.MethodDCStopInit += handler;
                    var sampleProfiler = new SampleProfilerTraceEventParser(traceSource);
                    sampleProfiler.All += handler;
                    var privateClr = new ClrPrivateTraceEventParser(traceSource);
                    privateClr.All += handler;
                    traceSource.Clr.All += handler;
                    traceSource.Clr.MethodILToNativeMap -= handler;
                    traceSource.Dynamic.All += handler;

                    // Process
                    traceSource.Process();
                }
            }
            // Validate
            ValidateEventStatistics(eventStatistics, eventPipeFileName);
        }

#if NETCOREAPP3_0_OR_GREATER
        [Theory]
#else
        [Theory(Skip = "EventPipeSession connection is only available to target apps on .NET Core 3.0 or later")]
#endif
        [InlineData(true)]
        [InlineData(false)]
        public async Task SessionStreaming(bool initialRundown)
        {
            var client = new DiagnosticsClient(Process.GetCurrentProcess().Id);
            var rundownConfig = initialRundown ? EventPipeRundownConfiguration.Enable(client) : EventPipeRundownConfiguration.None();
            var providers = new[]
            {
                new EventPipeProvider(SampleProfilerTraceEventParser.ProviderName, EventLevel.Informational),
            };
            using (var session = client.StartEventPipeSession(providers, requestRundown: false))
            {
                using (var traceSource = CreateFromEventPipeSession(session, rundownConfig))
                {
                    var sampleEventParser = new SampleProfilerTraceEventParser(traceSource);

                    // Signal that we have received the first event.
                    var eventCallStackIndex = new TaskCompletionSource<CallStackIndex>();
                    sampleEventParser.ThreadSample += delegate (ClrThreadSampleTraceData e)
                    {
                        eventCallStackIndex.TrySetResult(e.CallStackIndex());
                    };

                    // Process in the background (this is blocking).
                    var processingTask = Task.Run(traceSource.Process);

                    // Verify the event can be symbolicated on the fly if (initialRundown == true).
                    var callStackIndex = await eventCallStackIndex.Task;
                    Assert.NotEqual(CallStackIndex.Invalid, callStackIndex);
                    var codeAddressIndex = traceSource.TraceLog.CallStacks.CodeAddressIndex(callStackIndex);
                    Assert.NotEqual(CodeAddressIndex.Invalid, codeAddressIndex);
                    var methodIndex = traceSource.TraceLog.CodeAddresses.MethodIndex(codeAddressIndex);
                    if (initialRundown)
                    {
                        Assert.NotEqual(MethodIndex.Invalid, methodIndex);
                        var method = traceSource.TraceLog.CodeAddresses.Methods[methodIndex];
                        Assert.NotEmpty(method.FullMethodName);
                    }
                    else
                    {
                        Assert.Equal(MethodIndex.Invalid, methodIndex);
                    }

                    // Stop after receiving the first event.
                    session.Stop();
                    await processingTask;
                }
            }
        }

        [Fact]
        public void V1IsUnsupported()
        {
            PrepareTestData();
            const string eventPipeFileName = "eventpipe-dotnetcore2.0-linux-objver1.netperf";
            string eventPipeFilePath = Path.Combine(UnZippedDataDir, eventPipeFileName);

            Assert.Throws<UnsupportedFormatVersionException>(() =>
            {
                try
                {
                    using (var eventPipeSource = new EventPipeEventSource(eventPipeFilePath)) { }
                }
                catch (UnsupportedFormatVersionException ex)
                {
                    Assert.Equal(1, ex.RequestedVersion);
                    Assert.Equal(3, ex.MinSupportedVersion);
                    Assert.Equal(6, ex.MaxSupportedVersion);
                    throw;
                }
            });
        }

        [Fact]
        public void V2IsUnsupported()
        {
            PrepareTestData();
            const string eventPipeFileName = "eventpipe-dotnetcore2.0-linux-objver2.netperf";
            string eventPipeFilePath = Path.Combine(UnZippedDataDir, eventPipeFileName);

            Assert.Throws<UnsupportedFormatVersionException>(() =>
            {
                try
                {
                    using (var eventPipeSource = new EventPipeEventSource(eventPipeFilePath)) { }
                }
                catch (UnsupportedFormatVersionException ex)
                {
                    Assert.Equal(2, ex.RequestedVersion);
                    Assert.Equal(3, ex.MinSupportedVersion);
                    Assert.Equal(6, ex.MaxSupportedVersion);
                    throw;
                }
            });
        }

        [Fact]
        public void CanParseHeaderOfV3EventPipeFile()
        {
            PrepareTestData();

            const string eventPipeFileName = "eventpipe-dotnetcore2.1-win-x86-objver3.netperf";

            string eventPipeFilePath = Path.Combine(UnZippedDataDir, eventPipeFileName);

            using (var eventPipeSource = new EventPipeEventSource(eventPipeFilePath))
            {
                Assert.Equal(4, eventPipeSource.PointerSize);
                Assert.Equal(3312, eventPipeSource._processId);
                Assert.Equal(4, eventPipeSource.NumberOfProcessors);
                Assert.Equal(1000000, eventPipeSource._expectedCPUSamplingRate);

                Assert.Equal(636531024984420000, eventPipeSource._syncTimeUTC.Ticks);
                Assert.Equal(20461004832, eventPipeSource._syncTimeQPC);
                Assert.Equal(2533315, eventPipeSource._QPCFreq);

                Assert.Equal(10, eventPipeSource.CpuSpeedMHz);
            }
        }

        [Fact]
        public void CanParseHeaderOfV4EventPipeFile()
        {
            PrepareTestData();

            const string eventPipeFileName = "eventpipe-dotnetcore3.0-win-x64-objver4.nettrace";

            string eventPipeFilePath = Path.Combine(UnZippedDataDir, eventPipeFileName);

            using (var eventPipeSource = new EventPipeEventSource(eventPipeFilePath))
            {
                Assert.Equal(8, eventPipeSource.PointerSize);
                Assert.Equal(10064, eventPipeSource._processId);
                Assert.Equal(4, eventPipeSource.NumberOfProcessors);
                Assert.Equal(1000000, eventPipeSource._expectedCPUSamplingRate);

                Assert.Equal(636962398197540000, eventPipeSource._syncTimeUTC.Ticks);
                Assert.Equal(100935252313, eventPipeSource._syncTimeQPC);
                Assert.Equal(3124099, eventPipeSource._QPCFreq);

                Assert.Equal(10, eventPipeSource.CpuSpeedMHz);
            }
        }

        [Fact]
        public void V4EventPipeFileHasProcNumbers()
        {
            PrepareTestData();

            const string eventPipeFileName = "eventpipe-dotnetcore3.0-win-x64-objver4.nettrace";

            string eventPipeFilePath = Path.Combine(UnZippedDataDir, eventPipeFileName);

            using (var traceSource = new EventPipeEventSource(eventPipeFilePath))
            {
                Assert.Equal(4, traceSource.NumberOfProcessors);
                int[] counts = new int[4];

                Action<TraceEvent> handler = delegate (TraceEvent data)
                {
                    Assert.True(data.ProcessorNumber >= 0 && data.ProcessorNumber < traceSource.NumberOfProcessors);
                    counts[data.ProcessorNumber]++;
                };

                var privateClr = new ClrPrivateTraceEventParser(traceSource);
                privateClr.All += handler;
                traceSource.Clr.All += handler;
                traceSource.Clr.MethodILToNativeMap -= handler;
                traceSource.Dynamic.All += handler;

                // Process
                traceSource.Process();

                for (int i = 0; i < traceSource.NumberOfProcessors; i++)
                {
                    Assert.NotEqual(0, counts[i]);
                }
            }
        }

        [Fact]
        public void GotoWorksForPositionsGreaterThanAlignment()
        {
            using (var reader = new PinnedStreamReader(new MockHugeStream((long)uint.MaxValue + 5_000_000), settings: SerializationSettings.Default.WithStreamReaderAlignment(StreamReaderAlignment.EightBytes), bufferSize: 0x4000 /* 16KB */))
            {
                reader.Goto((StreamLabel)0x148);
                var buf = new byte[100_000];
                // Specifically doing a read larger than the default buffer size (16KB) to avoid caching path.
                reader.Read(buf, 0, buf.Length);
                // 0x14 is the 0x148th byte of the MockHugeStream (it is deterministic).
                // If MockHugeStream changes, then this should be changed as well.
                Assert.Equal(0x14, buf[0]);
            }
        }

        [Fact]
        public void CanReadV4EventPipeTraceBiggerThan4GB()
        {
            // Generate a mock stream of events larger than 4GB in size and parse it all
            using (var traceSource = new EventPipeEventSource(new MockHugeStream((long)uint.MaxValue + 5_000_000)))
            {
                int eventCount = 0;

                Action<TraceEvent> handler = delegate (TraceEvent data)
                {
                    eventCount++;
                };

                traceSource.AllEvents += handler;

                // Process
                traceSource.Process();

                Assert.Equal(71600, eventCount);
            }
        }

        [Fact]
        public void CanReadV5EventPipeArrayTypes()
        {
            PrepareTestData();

            const string eventPipeFileName = "eventpipe-dotnetcore5.0-win-x64-arraytypes.nettrace";

            string eventPipeFilePath = Path.Combine(UnZippedDataDir, eventPipeFileName);

            using (var traceSource = new EventPipeEventSource(eventPipeFilePath))
            {
                int successCount = 0;
                Action<TraceEvent> handler = delegate (TraceEvent traceEvent)
                {
                    string traceLevelValidationMessage = "Expected level {0} but got level {1} for EventSource={2} Event={3}";
                    string traceKeywordValidationMessage = "Expected keywords {0} but got keywords{1} for EventSource={2} Event={3}";
                    string traceOpcodeValidationMessage = "Expected opcode {0} but got opcode {1} for EventSource={2} Event={3}";
                    string tracePayloadValidationMessage = "Expected {0} payload items but got {1} items for EventSource={2} Event={3}";
                    string tracePayloadNamesValidationMessage = "Expected argument name {0} but got name {1} for EventSource={2} Event={3}";
                    string tracePayloadTypeValidationMessage = "Expected type {0} but got type {1} for EventSource={2} Event={3} Argument={4}";
                    string tracePayloadValueValidationMessage = "Expected argument value {0} but got value {1} for EventSource={2} Event={3} Argument={4}";

                    if (traceEvent.ProviderName == "TestEventSource0")
                    {
                        if (traceEvent.EventName == "TestEvent1")
                        {
                            if ((int)traceEvent.Level != 2) { throw new Exception(String.Format(traceLevelValidationMessage, 2, (int)traceEvent.Level, "TestEventSource0", "TestEvent1")); }
                            if ((int)traceEvent.Keywords != 0) { throw new Exception(String.Format(traceKeywordValidationMessage, 0, (int)traceEvent.Keywords, "TestEventSource0", "TestEvent1")); }
                            if ((int)traceEvent.Opcode != 0) { throw new Exception(String.Format(traceOpcodeValidationMessage, 0, (int)traceEvent.Opcode, "TestEventSource0", "TestEvent1")); }
                            if (traceEvent.PayloadNames.Count() != 3) { throw new Exception(String.Format(tracePayloadValidationMessage, 3, traceEvent.PayloadNames.Count(), "TestEventSource0", "TestEvent1")); }
                            uint[] testEvent1Array0 = new uint[] { (uint)2032854139, (uint)1608221689, (uint)1470019200, (uint)199494339, (uint)1238846140, (uint)1366609043 };
                            if (traceEvent.PayloadNames[0] != "arg0") { throw new Exception(String.Format(tracePayloadNamesValidationMessage, "arg0", traceEvent.PayloadNames[0], "TestEventSource0", "TestEvent1")); }
                            if (traceEvent.PayloadValue(0).GetType() != typeof(uint[])) { throw new Exception(String.Format(tracePayloadTypeValidationMessage, "uint[]", traceEvent.PayloadValue(0).GetType(), "TestEventSource0", "TestEvent1", "arg0")); }
                            if (!Enumerable.SequenceEqual((uint[])traceEvent.PayloadValue(0), testEvent1Array0)) { throw new Exception(String.Format(tracePayloadValueValidationMessage, testEvent1Array0, traceEvent.PayloadValue(0), "TestEventSource0", "TestEvent1", "arg0")); }
                            short[] testEvent1Array1 = new short[] { (short)26494, (short)17115, (short)-7229, (short)18850, (short)27931 };
                            if (traceEvent.PayloadNames[1] != "arg1") { throw new Exception(String.Format(tracePayloadNamesValidationMessage, "arg1", traceEvent.PayloadNames[1], "TestEventSource0", "TestEvent1")); }
                            if (traceEvent.PayloadValue(1).GetType() != typeof(short[])) { throw new Exception(String.Format(tracePayloadTypeValidationMessage, "short[]", traceEvent.PayloadValue(1).GetType(), "TestEventSource0", "TestEvent1", "arg1")); }
                            if (!Enumerable.SequenceEqual((short[])traceEvent.PayloadValue(1), testEvent1Array1)) { throw new Exception(String.Format(tracePayloadValueValidationMessage, testEvent1Array1, traceEvent.PayloadValue(1), "TestEventSource0", "TestEvent1", "arg1")); }
                            if (traceEvent.PayloadNames[2] != "arg2") { throw new Exception(String.Format(tracePayloadNamesValidationMessage, "arg2", traceEvent.PayloadNames[2], "TestEventSource0", "TestEvent1")); }
                            if (traceEvent.PayloadValue(2).GetType() != typeof(long)) { throw new Exception(String.Format(tracePayloadTypeValidationMessage, "long", traceEvent.PayloadValue(2).GetType(), "TestEventSource0", "TestEvent1", "arg2")); }
                            if ((long)traceEvent.PayloadValue(2) != 2033417279) { throw new Exception(String.Format(tracePayloadValueValidationMessage, 2033417279, traceEvent.PayloadValue(2), "TestEventSource0", "TestEvent1", "arg2")); }

                            ++successCount;
                            return;
                        }
                        if (traceEvent.EventName == "TestEvent2")
                        {
                            if ((int)traceEvent.Level != 4) { throw new Exception(String.Format(traceLevelValidationMessage, 4, (int)traceEvent.Level, "TestEventSource0", "TestEvent2")); }
                            if ((int)traceEvent.Keywords != 1231036291) { throw new Exception(String.Format(traceKeywordValidationMessage, 1231036291, (int)traceEvent.Keywords, "TestEventSource0", "TestEvent2")); }
                            if ((int)traceEvent.Opcode != 0) { throw new Exception(String.Format(traceOpcodeValidationMessage, 0, (int)traceEvent.Opcode, "TestEventSource0", "TestEvent2")); }
                            if (traceEvent.PayloadNames.Count() != 4) { throw new Exception(String.Format(tracePayloadValidationMessage, 4, traceEvent.PayloadNames.Count(), "TestEventSource0", "TestEvent2")); }
                            if (traceEvent.PayloadNames[0] != "arg0") { throw new Exception(String.Format(tracePayloadNamesValidationMessage, "arg0", traceEvent.PayloadNames[0], "TestEventSource0", "TestEvent2")); }
                            if (traceEvent.PayloadValue(0).GetType() != typeof(long)) { throw new Exception(String.Format(tracePayloadTypeValidationMessage, "long", traceEvent.PayloadValue(0).GetType(), "TestEventSource0", "TestEvent2", "arg0")); }
                            if ((long)traceEvent.PayloadValue(0) != -1001479330) { throw new Exception(String.Format(tracePayloadValueValidationMessage, -1001479330, traceEvent.PayloadValue(0), "TestEventSource0", "TestEvent2", "arg0")); }
                            if (traceEvent.PayloadNames[1] != "arg1") { throw new Exception(String.Format(tracePayloadNamesValidationMessage, "arg1", traceEvent.PayloadNames[1], "TestEventSource0", "TestEvent2")); }
                            if (traceEvent.PayloadValue(1).GetType() != typeof(int)) { throw new Exception(String.Format(tracePayloadTypeValidationMessage, "int", traceEvent.PayloadValue(1).GetType(), "TestEventSource0", "TestEvent2", "arg1")); }
                            if ((int)traceEvent.PayloadValue(1) != -1397908228) { throw new Exception(String.Format(tracePayloadValueValidationMessage, -1397908228, traceEvent.PayloadValue(1), "TestEventSource0", "TestEvent2", "arg1")); }
                            int[] testEvent2Array0 = new int[] { 1470938110, 172564262, 1133558854, -1572049829 };
                            if (traceEvent.PayloadNames[2] != "arg2") { throw new Exception(String.Format(tracePayloadNamesValidationMessage, "arg2", traceEvent.PayloadNames[2], "TestEventSource0", "TestEvent2")); }
                            if (traceEvent.PayloadValue(2).GetType() != typeof(int[])) { throw new Exception(String.Format(tracePayloadTypeValidationMessage, "int[]", traceEvent.PayloadValue(2).GetType(), "TestEventSource0", "TestEvent2", "arg2")); }
                            if (!Enumerable.SequenceEqual((int[])traceEvent.PayloadValue(2), testEvent2Array0)) { throw new Exception(String.Format(tracePayloadValueValidationMessage, testEvent2Array0, traceEvent.PayloadValue(2), "TestEventSource0", "TestEvent2", "arg2")); }
                            ulong[] testEvent2Array1 = new ulong[] { (ulong)2055126903, (ulong)593325874, (ulong)2130052527, (ulong)162795177 };
                            if (traceEvent.PayloadNames[3] != "arg3") { throw new Exception(String.Format(tracePayloadNamesValidationMessage, "arg3", traceEvent.PayloadNames[3], "TestEventSource0", "TestEvent2")); }
                            if (traceEvent.PayloadValue(3).GetType() != typeof(ulong[])) { throw new Exception(String.Format(tracePayloadTypeValidationMessage, "ulong[]", traceEvent.PayloadValue(3).GetType(), "TestEventSource0", "TestEvent2", "arg3")); }
                            if (!Enumerable.SequenceEqual((ulong[])traceEvent.PayloadValue(3), testEvent2Array1)) { throw new Exception(String.Format(tracePayloadValueValidationMessage, testEvent2Array1, traceEvent.PayloadValue(3), "TestEventSource0", "TestEvent2", "arg3")); }

                            ++successCount;
                            return;
                        }
                        if (traceEvent.EventName == "TestEvent3/24")
                        {
                            if ((int)traceEvent.Level != 2) { throw new Exception(String.Format(traceLevelValidationMessage, 2, (int)traceEvent.Level, "TestEventSource0", "TestEvent3")); }
                            if ((int)traceEvent.Keywords != 0) { throw new Exception(String.Format(traceKeywordValidationMessage, 0, (int)traceEvent.Keywords, "TestEventSource0", "TestEvent3")); }
                            if ((int)traceEvent.Opcode != 24) { throw new Exception(String.Format(traceOpcodeValidationMessage, 24, (int)traceEvent.Opcode, "TestEventSource0", "TestEvent3")); }
                            if (traceEvent.PayloadNames.Count() != 1) { throw new Exception(String.Format(tracePayloadValidationMessage, 1, traceEvent.PayloadNames.Count(), "TestEventSource0", "TestEvent3")); }
                            if (traceEvent.PayloadNames[0] != "arg0") { throw new Exception(String.Format(tracePayloadNamesValidationMessage, "arg0", traceEvent.PayloadNames[0], "TestEventSource0", "TestEvent3")); }
                            if (traceEvent.PayloadValue(0).GetType() != typeof(string)) { throw new Exception(String.Format(tracePayloadTypeValidationMessage, "string", traceEvent.PayloadValue(0).GetType(), "TestEventSource0", "TestEvent3", "arg0")); }
                            char[] testEvent3Array = new char[] { (char)59299, (char)13231, (char)38541, (char)7407, (char)35812 };
                            string actualPayload0 = (string)traceEvent.PayloadValue(0);
                            if (!Enumerable.SequenceEqual(actualPayload0.ToCharArray(), testEvent3Array)) { throw new Exception(String.Format(tracePayloadValueValidationMessage, testEvent3Array, traceEvent.PayloadValue(0), "TestEventSource0", "TestEvent3", "arg0")); }

                            ++successCount;
                            return;
                        }
                    }
                };

                traceSource.Dynamic.All += handler;

                // Process
                traceSource.Process();

                Assert.Equal(3, successCount);
            }
        }

        [Fact]
        public void AllEventsLeavesNoUnhandledEvents()
        {
            PrepareTestData();

            const string eventPipeFileName = "eventpipe-dotnetcore2.1-win-x86-objver3.netperf";

            string eventPipeFilePath = Path.Combine(UnZippedDataDir, eventPipeFileName);

            using (var traceSource = new TraceLog(TraceLog.CreateFromEventPipeDataFile(eventPipeFilePath)).Events.GetSource())
            {
                int dynamicAllInvocationCount = 0;
                int unhandledEvents = 0;

                traceSource.AllEvents += _ => dynamicAllInvocationCount++;

                traceSource.UnhandledEvents += _ => unhandledEvents++;

                traceSource.Process();

                Assert.NotEqual(0, dynamicAllInvocationCount);
                Assert.Equal(0, unhandledEvents);
            }
        }

        [Fact]
        public void GuidsInMetadataAreSupported()
        {
            PrepareTestData();

            const string eventPipeFileName = "eventpipe-dotnetcore2.1-linux-x64-objver3.netperf";
            Guid ExpectedActivityId = new Guid("10000000-0000-0000-0000-000000000001");

            string eventPipeFilePath = Path.Combine(UnZippedDataDir, eventPipeFileName);

            bool activityIdHasBeenSet = false;
            using (var traceSource = new TraceLog(TraceLog.CreateFromEventPipeDataFile(eventPipeFilePath)).Events.GetSource())
            {
                traceSource.AllEvents += (TraceEvent @event) =>
                {
                    // before the activity Id is set, it's empty
                    // after the activity Id is set, all the events should have it (here we had only 1 thread)
                    Assert.Equal(activityIdHasBeenSet ? ExpectedActivityId : Guid.Empty, @event.ActivityID);

                    // the EVENTID for SetActivityId is 25 https://github.com/dotnet/coreclr/blob/c67c29d6e226e4cca1f1efb4d57b7f498d58b534/src/mscorlib/src/System/Threading/Tasks/TPLETWProvider.cs#L524
                    if (@event.ProviderName != TplEtwProviderTraceEventParser.ProviderName || @event.ID != (TraceEventID)25)
                    {
                        return;
                    }

                    Assert.False(activityIdHasBeenSet); // make sure the event comes only once

                    // Sneak in a test of the DLR support here (casting to 'dynamic'
                    // instead of using PayloadByName("NewId")):
                    if (((dynamic)@event).NewId.Equals(ExpectedActivityId))
                    {
                        activityIdHasBeenSet = true;
                    }
                };

                traceSource.Process();

                Assert.True(activityIdHasBeenSet);
            }
        }

        [Fact]
        public void WellKnownDiagnosticSourceEventsHavePayloads()
        {
            // NetPerf and NetTrace v1 format don't support serializing array typed parameters.
            // DiagnosticSourceEventSource declares well known events that use array typed parameters
            // and the runtime serializes EventPipe metadata which claims the events have no parameters.
            // We made a targeted fix where EventPipeEventSource will recognize these well-known events
            // and ignore the empty parameter metadata provided in the stream, treating the events
            // as if the runtime had provided the correct parameter schema.
            //
            // I am concurrently working on a runtime fix and updated file format revision which can
            // correctly encode these parameter types. However for back-compat with older runtimes we
            // need this.


            // Serialize an EventPipe stream containing the parameterless metadata for
            // DiagnosticSourceEventSource events...
            EventPipeWriterV5 writer = new EventPipeWriterV5();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(
                new EventMetadata(1, "Microsoft-Diagnostics-DiagnosticSource", "Event", 3),
                new EventMetadata(2, "Microsoft-Diagnostics-DiagnosticSource", "Activity1Start", 4),
                new EventMetadata(3, "Microsoft-Diagnostics-DiagnosticSource", "Activity1Stop", 5),
                new EventMetadata(4, "Microsoft-Diagnostics-DiagnosticSource", "Activity2Start", 6),
                new EventMetadata(5, "Microsoft-Diagnostics-DiagnosticSource", "Activity2Stop", 7),
                new EventMetadata(6, "Microsoft-Diagnostics-DiagnosticSource", "RecursiveActivity1Start", 8),
                new EventMetadata(7, "Microsoft-Diagnostics-DiagnosticSource", "RecursiveActivity1Stop", 9));

            EventPayloadWriter payload = new EventPayloadWriter();
            payload.WriteNullTerminatedUTF16String("FakeProviderName");
            payload.WriteNullTerminatedUTF16String("FakeEventName");
            payload.WriteArray(new KeyValuePair<string, string>[]
            {
                new KeyValuePair<string,string>("key1", "val1"),
                new KeyValuePair<string, string>("key2", "val2")
            },
            kv =>
            {
                payload.WriteNullTerminatedUTF16String(kv.Key);
                payload.WriteNullTerminatedUTF16String(kv.Value);
            });
            byte[] payloadBytes = payload.ToArray();

            int sequenceNumber = 1;
            writer.WriteEventBlock(
                w =>
                {
                    // write one of each of the 7 well-known DiagnosticSourceEventSource events.
                    for (int metadataId = 1; metadataId <= 7; metadataId++)
                    {
                        w.WriteEventBlobV4Or5(metadataId, 999, sequenceNumber++, payloadBytes);
                    }
                });
            writer.WriteEndObject();
            MemoryStream stream = new MemoryStream(writer.ToArray());

            // Confirm we can parse the event payloads even though the parameters were not described in
            // the metadata.
            EventPipeEventSource source = new EventPipeEventSource(stream);
            int diagSourceEventCount = 0;
            source.Dynamic.All += e =>
            {
                Assert.Equal("FakeProviderName", e.PayloadByName("SourceName"));
                Assert.Equal("FakeEventName", e.PayloadByName("EventName"));
                IDictionary<string, object>[] args = (IDictionary<string, object>[])e.PayloadByName("Arguments");
                Assert.Equal(2, args.Length);
                Assert.Equal("key1", args[0]["Key"]);
                Assert.Equal("val1", args[0]["Value"]);
                Assert.Equal("key2", args[1]["Key"]);
                Assert.Equal("val2", args[1]["Value"]);
                diagSourceEventCount++;
            };
            source.Process();
            Assert.Equal(7, diagSourceEventCount);
        }

        [Fact]
        public void ExecutionCheckpointEventsAndTimeStamping()
        {
            PrepareTestData();

            const string eventPipeFileName = "eventpipe-dotnetcore6.0-win-x64-executioncheckpoints.nettrace";

            string eventPipeFilePath = Path.Combine(UnZippedDataDir, eventPipeFileName);

            var checkpoints = new Dictionary<string, double>();
            checkpoints.Add("RuntimeInit", -15.97);
            checkpoints.Add("RuntimeSuspend", 11.91);
            checkpoints.Add("RuntimeResumed", 12.20);

            UnicodeEncoding unicode = new UnicodeEncoding();
            using (var source = new EventPipeEventSource(eventPipeFilePath))
            {
                var rundown = new ClrRundownTraceEventParser(source);
                rundown.ExecutionCheckpointRundownExecutionCheckpointDCEnd += delegate (ExecutionCheckpointTraceData data)
                {
                    var timestamp = source.QPCTimeToTimeStamp(data.CheckpointTimestamp);
                    var diff = Math.Round((timestamp - source.SessionStartTime).TotalMilliseconds, 2);

                    // Asserts
                    Assert.True(checkpoints.ContainsKey(data.CheckpointName));
                    Assert.True(checkpoints[data.CheckpointName] == diff);

                    checkpoints.Remove(data.CheckpointName);
                };

                source.Process();

                Assert.True(checkpoints.Count == 0);
            }
        }

        private void Dynamic_All(TraceEvent obj)
        {
            throw new NotImplementedException();
        }

        private void ValidateEventStatistics(EventStatistics eventStatistics, string eventPipeFileName)
        {
            string actual = eventStatistics.ToString();
            string baselineFile = Path.Combine(TestDataDir, eventPipeFileName + ".baseline.txt");
            string expected = File.ReadAllText(baselineFile);

            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                string eventStatisticsFile = Path.Combine(TestDataDir, eventPipeFileName + ".actual.txt");
                File.WriteAllText(eventStatisticsFile, actual, Encoding.UTF8);

                Output.WriteLine($"Baseline File: {baselineFile}");
                Output.WriteLine($"Actual File: {eventStatisticsFile}");
                Output.WriteLine($"To Diff: windiff {baselineFile} {eventStatisticsFile}");
                Assert.Fail($"The event statistics doesn't match {Path.GetFullPath(baselineFile)}. It's saved in {Path.GetFullPath(eventStatisticsFile)}.");
            }
        }

        // In the V5 format the event block and metadata block both have a variable size header. Readers are expected to skip over extra content if it is present.
        [Fact]
        public void SkipExtraBlockHeaderSpaceV5()
        {
            EventPipeWriterV5 writer = new EventPipeWriterV5();
            writer.WriteHeaders();
            writer.WriteBlock("MetadataBlock", w =>
            {
                // header
                w.Write((short)28); // header size
                w.Write((short)0); // flags
                w.Write((long)0);  // min timestamp
                w.Write((long)0);  // max timestamp
                w.Write((int)99);   // extra bytes
                w.Write((int)99);   // extra bytes
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(1, "TestProvider", "TestEvent", 15));
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(2, "TestProvider", "TestEvent2", 16));
            });
            writer.WriteBlock("EventBlock", w =>
            {
                // header
                w.Write((short)28); // header size
                w.Write((short)0);  // flags
                w.Write((long)0);   // min timestamp
                w.Write((long)0);   // max timestamp
                w.Write((int)99);   // extra bytes
                w.Write((int)99);   // extra bytes
                w.WriteEventBlobV4Or5(2, 999, 1, new byte[0]);
            });
            writer.WriteEndObject();

            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);
            int eventCount = 0;
            source.Dynamic.All += e =>
            {
                Assert.Equal("TestEvent2", e.EventName);
                Assert.Equal("TestProvider", e.ProviderName);
                eventCount++;
            };
            source.Process();
            Assert.Equal(1, eventCount);
        }

        // In the V5 format each trailing metadata tag has a variable size. Readers are expected to skip over extra content if it is present.
        [Fact]
        public void SkipExtraMetadataTagSpaceV5()
        {
            EventPipeWriterV5 writer = new EventPipeWriterV5();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(w =>
            {
                w.WriteMetadataEventBlobV5OrLess(payloadWriter =>
                {
                    payloadWriter.WriteV5InitialMetadataBlob(1, "TestProvider", "TestEvent1", 15);
                    payloadWriter.WriteV5MetadataParameterList();
                    payloadWriter.WriteV5MetadataTagBytes(1, tagWriter => // An opcode tag with extra bytes
                    {
                        tagWriter.Write((int)0);
                        tagWriter.Write((int)99);
                    });
                });
                w.WriteMetadataEventBlobV5OrLess(payloadWriter =>
                {
                    payloadWriter.WriteV5InitialMetadataBlob(2, "TestProvider", "TestEvent2", 16);
                    payloadWriter.WriteV5MetadataParameterList();
                    payloadWriter.WriteV5MetadataTagBytes(2, tagWriter => // A V2 param list tag with extra bytes
                    {
                        tagWriter.WriteV5MetadataParameterList();
                        tagWriter.Write((int)99);
                    });
                    payloadWriter.WriteV5MetadataTagBytes(3, tagWriter => // An unknown tag with some bytes
                    {
                        tagWriter.Write((int)99);
                        tagWriter.Write((int)99);
                    });
                });
                w.WriteMetadataEventBlobV5OrLess(payloadWriter =>
                {
                    payloadWriter.WriteV5InitialMetadataBlob(3, "TestProvider", "TestEvent3", 17);
                    payloadWriter.WriteV5MetadataParameterList();
                });
            });
            writer.WriteEventBlock(w =>
            {
                w.WriteEventBlobV4Or5(1, 999, 1, new byte[0]);
                w.WriteEventBlobV4Or5(2, 999, 2, new byte[0]);
                w.WriteEventBlobV4Or5(3, 999, 3, new byte[0]);
            });
            writer.WriteEndObject();

            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);
            int eventCount = 0;
            source.Dynamic.All += e =>
            {
                eventCount++;
                Assert.Equal($"TestEvent{eventCount}", e.EventName);
                Assert.Equal("TestProvider", e.ProviderName);
            };
            source.Process();
            Assert.Equal(3, eventCount);
        }

        // In the V5 format each parameter metadata has a variable size. Readers are expected to skip over extra content if it is present.
        [Fact]
        public void SkipExtraMetadataParameterSpaceV5()
        {
            EventPipeWriterV5 writer = new EventPipeWriterV5();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(w =>
            {
                w.WriteMetadataEventBlobV5OrLess(payloadWriter =>
                {
                    payloadWriter.WriteV5InitialMetadataBlob(1, "TestProvider", "TestEvent1", 15);
                    payloadWriter.WriteV5MetadataParameterList();
                    payloadWriter.WriteV5MetadataV2ParamTag(fieldCount: 2, paramsWriter =>
                    {
                        paramsWriter.WriteFieldLayoutV2MetadataParameter("Param1", paramWriter =>
                        {
                            paramWriter.Write((int)TypeCode.Int32);
                            paramWriter.Write((long)199); // extra bytes on the type that should be skipped
                        });
                        paramsWriter.WriteFieldLayoutV2MetadataParameter("Param2", paramWriter =>
                        {
                            paramWriter.Write((int)TypeCode.Int16);
                            paramWriter.Write((byte)199); // extra bytes on the type that should be skipped
                        });
                    });
                });
            });
            writer.WriteEventBlock(w =>
            {
                w.WriteEventBlobV4Or5(1, 999, 1, new byte[] { 12, 0, 0, 0, 17, 0 });
            });
            writer.WriteEndObject();

            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);
            int eventCount = 0;
            source.Dynamic.All += e =>
            {
                eventCount++;
                Assert.Equal($"TestEvent1", e.EventName);
                Assert.Equal("TestProvider", e.ProviderName);
                Assert.Equal(2, e.PayloadNames.Length);
                Assert.Equal("Param1", e.PayloadNames[0]);
                Assert.Equal("Param2", e.PayloadNames[1]);
                Assert.Equal(12, e.PayloadValue(0));
                Assert.Equal(17, e.PayloadValue(1));
            };
            source.Process();
            Assert.Equal(1, eventCount);
        }

        [Fact] //V6
        public void ParseMinimalTraceV6()
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());

            // Confirm we can parse the event payloads even though the parameters were not described in
            // the metadata.
            EventPipeEventSource source = new EventPipeEventSource(stream);
            source.Process();
            Assert.Equal(6, source.FileFormatVersionNumber);
        }

        [Fact]
        public void ThrowsExceptionOnUnsupportedMajorVersion()
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders(new Dictionary<string, string>(), 7, 0);
            MemoryStream stream = new MemoryStream(writer.ToArray());

            using (EventPipeEventSource source = new EventPipeEventSource(stream))
            {
                Assert.Throws<UnsupportedFormatVersionException>(() =>
                {
                    try
                    {
                        source.Process();
                    }
                    catch (UnsupportedFormatVersionException e)
                    {
                        Assert.Equal(7, e.RequestedVersion);
                        Assert.Equal(3, e.MinSupportedVersion);
                        Assert.Equal(6, e.MaxSupportedVersion);
                        throw;
                    }
                });
            }
        }

        [Fact] //V6
        public void MinorVersionIncrementsAreSupported()
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders(new Dictionary<string, string>(), majorVersion: 6, minorVersion: 25);
            writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);
            source.Process();
            Assert.Equal(6, source.FileFormatVersionNumber);
        }

        [Fact] //V6
        public void ParseV6TraceBlockStandardFields()
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);
            source.Process();
            Assert.Equal(new DateTime(2025, 2, 3, 4, 5, 6), source._syncTimeUTC);
            Assert.Equal(0, source._syncTimeQPC);
            Assert.Equal(1_000, source._QPCFreq);
            Assert.Equal(8, source.PointerSize);
            Assert.Equal(0, source._processId);
            Assert.Equal(0, source.NumberOfProcessors);
        }

        [Fact] //V6
        public void ParseV6TraceBlockKeyValuePairs()
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders(new Dictionary<string, string>()
            {
                { "ProcessId", "1234" },
                { "HardwareThreadCount", "16" },
                { "ExpectedCPUSamplingRate", "1790" },
                { "CpuSpeedMHz", "3000" },
                { "Ponies", "LotsAndLots!" }
            });
            writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);
            source.Process();
            Assert.Equal(1234, source._processId);
            Assert.Equal(16, source.NumberOfProcessors);
            Assert.Equal(1790, source._expectedCPUSamplingRate);
            Assert.Collection(source.HeaderKeyValuePairs,
                kvp =>
                {
                    Assert.Equal("ProcessId", kvp.Key);
                    Assert.Equal("1234", kvp.Value);
                },
                kvp =>
                {
                    Assert.Equal("HardwareThreadCount", kvp.Key);
                    Assert.Equal("16", kvp.Value);
                },
                kvp =>
                {
                    Assert.Equal("ExpectedCPUSamplingRate", kvp.Key);
                    Assert.Equal("1790", kvp.Value);
                },
                kvp =>
                {
                    Assert.Equal("CpuSpeedMHz", kvp.Key);
                    Assert.Equal("3000", kvp.Value);
                },
                kvp =>
                {
                    Assert.Equal("Ponies", kvp.Key);
                    Assert.Equal("LotsAndLots!", kvp.Value);
                });
        }

        // In the V6 format readers are expected to skip over any block types they don't recognize. This allows future extensibility.
        [Fact] //V6
        public void V6UnrecognizedBlockTypesAreSkipped()
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteBlock(99, w =>
            {
                w.Write((int)22);
            });
            writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());

            // Confirm we can parse the event payloads even though the parameters were not described in
            // the metadata.
            EventPipeEventSource source = new EventPipeEventSource(stream);
            source.Process();
            Assert.Equal(6, source.FileFormatVersionNumber);
        }

        [Fact] //V6
        public void ParseV6Metadata()
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(new EventMetadata(1, "TestProvider", "TestEvent1", 15,
                                          new MetadataParameter("Param1", MetadataTypeCode.Int16),
                                          new MetadataParameter("Param2", MetadataTypeCode.Boolean)),
                                      new EventMetadata(2, "TestProvider", "TestEvent2", 16),
                                      new EventMetadata(3, "TestProvider", "TestEvent3", 17));
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(999, 0, 0);
            });
            writer.WriteEventBlock(w =>
            {
                w.WriteEventBlob(1, 999, 1, new byte[] { 12, 0, 1, 0, 0, 0 });
                w.WriteEventBlob(2, 999, 2, new byte[0]);
                w.WriteEventBlob(3, 999, 3, new byte[0]);
            });
            writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);
            int eventCount = 0;
            source.Dynamic.All += e =>
            {
                eventCount++;
                Assert.Equal($"TestEvent{eventCount}", e.EventName);
                Assert.Equal("TestProvider", e.ProviderName);
                if (eventCount == 1)
                {
                    Assert.Equal(2, e.PayloadNames.Length);
                    Assert.Equal("Param1", e.PayloadNames[0]);
                    Assert.Equal("Param2", e.PayloadNames[1]);
                    Assert.Equal(12, e.PayloadValue(0));
                    Assert.Equal(true, e.PayloadValue(1));
                }
            };
            source.Process();
            Assert.Equal(3, eventCount);
        }



        [Fact] //V6
        public void ParseV6UTF8Char()
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(new EventMetadata(1, "TestProvider", "TestEvent1", 15,
                                          new MetadataParameter("UTF8Char", MetadataTypeCode.UTF8CodeUnit),
                                          new MetadataParameter("UTF8CharArray", new ArrayMetadataType(new MetadataType(MetadataTypeCode.UTF8CodeUnit)))));
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(999, 0, 0);
            });
            writer.WriteEventBlock(w =>
            {
                w.WriteEventBlob(1, 999, 1, p =>
                {
                    // UTF8 char 'A'
                    p.Write((byte)'A');
                    
                    // UTF8 char array "Hello"
                    p.WriteLengthPrefixedUTF8String("Hello");
                });
            });
            writer.WriteEndBlock();
            
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);
            int eventCount = 0;
            
            source.Dynamic.All += e =>
            {
                eventCount++;
                Assert.Equal("TestEvent1", e.EventName);
                Assert.Equal("TestProvider", e.ProviderName);
                Assert.Equal(2, e.PayloadNames.Length);
                
                // UTF8Char
                Assert.Equal("UTF8Char", e.PayloadNames[0]);
                Assert.Equal(typeof(char), e.PayloadValue(0).GetType());
                Assert.Equal('A', (char)e.PayloadValue(0));
                
                // UTF8CharArray - should convert to string
                Assert.Equal("UTF8CharArray", e.PayloadNames[1]);
                Assert.Equal(typeof(string), e.PayloadValue(1).GetType());
                Assert.Equal("Hello", e.PayloadValue(1));
            };
            
            source.Process();
            Assert.Equal(1, eventCount);
        }

        [Theory] //V6
        [InlineData(true)]
        [InlineData(false)]
        public void ParseV6MetadataArrayParam(bool roundTripThroughEtlx)
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(new EventMetadata(1, "TestProvider", "TestEvent1", 15,
                                          new MetadataParameter("IntArray", new ArrayMetadataType(new MetadataType(MetadataTypeCode.Int32))),
                                          new MetadataParameter("UTF8CharArray", new ArrayMetadataType(new MetadataType(MetadataTypeCode.UTF8CodeUnit))),
                                          new MetadataParameter("UTF16CharArray", new ArrayMetadataType(new MetadataType(MetadataTypeCode.UTF16CodeUnit))),
                                          new MetadataParameter("StringArray", new ArrayMetadataType(new ArrayMetadataType(new MetadataType(MetadataTypeCode.UTF16CodeUnit))))));
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(999, 0, 0);
            });
            writer.WriteEventBlock(w =>
            {
                w.WriteEventBlob(1, 999, 1, p =>
                {
                    // int32[] [10, 20, 30]
                    p.Write((ushort)3);
                    p.Write(10);
                    p.Write(20);
                    p.Write(30);

                    // UTF8 char array [A, B, C]
                    p.WriteLengthPrefixedUTF8String("ABC");
                    
                    // UTF16 char array [x, y, z]
                    p.WriteLengthPrefixedUTF16String("xyz");

                    // string[] ["Hi", "Bye"]
                    p.Write((ushort)2);
                    p.WriteLengthPrefixedUTF16String("Hi");
                    p.WriteLengthPrefixedUTF16String("Bye");
                });
            });
            writer.WriteEndBlock();
            
            MemoryStream stream = new MemoryStream(writer.ToArray());
            TraceEventDispatcher source = new EventPipeEventSource(stream);
            if (roundTripThroughEtlx)
            {
                MemoryStream etlxStream = new MemoryStream();
                TraceLog.CreateFromEventPipeEventSources(source, new IOStreamStreamWriter(etlxStream, SerializationSettings.Default, leaveOpen: true), null);
                etlxStream.Position = 0;
                source = new TraceLog(etlxStream).Events.GetSource();
            }
            int eventCount = 0;
            
            source.Dynamic.All += e =>
            {
                eventCount++;
                Assert.Equal("TestEvent1", e.EventName);
                Assert.Equal("TestProvider", e.ProviderName);
                Assert.Equal(4, e.PayloadNames.Length);
                
                // ints
                Assert.Equal(typeof(int[]), e.PayloadValue(0).GetType());
                int[] array = (int[])e.PayloadValue(0);
                Assert.Equal(3, array.Length);
                Assert.Equal(10, array[0]);
                Assert.Equal(20, array[1]);
                Assert.Equal(30, array[2]);

                // UTF8 chars
                Assert.Equal(typeof(string), e.PayloadValue(1).GetType());
                Assert.Equal("ABC", (string)e.PayloadValue(1));
                
                // UTF16 chars
                Assert.Equal(typeof(string), e.PayloadValue(2).GetType());
                Assert.Equal("xyz", e.PayloadValue(2));

                // strings
                Assert.Equal(typeof(string[]), e.PayloadValue(3).GetType());
                string[] stringArray = (string[])e.PayloadValue(3);
                Assert.Equal(2, stringArray.Length);
                Assert.Equal("Hi", stringArray[0]);
                Assert.Equal("Bye", stringArray[1]);
            };
            
            source.Process();
            Assert.Equal(1, eventCount);
        }

        [Theory] //V6
        [InlineData(true)]
        [InlineData(false)]
        public void ParseV6FixedLengthArray(bool roundTripThroughEtlx)
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(new EventMetadata(1, "TestProvider", "TestEvent1", 15,
                                          new MetadataParameter("IntArray", new FixedLengthArrayMetadataType(3, new MetadataType(MetadataTypeCode.Int32))),
                                          new MetadataParameter("UTF8CharArray", new FixedLengthArrayMetadataType(3, new MetadataType(MetadataTypeCode.UTF8CodeUnit))),
                                          new MetadataParameter("UTF16CharArray", new FixedLengthArrayMetadataType(3, new MetadataType(MetadataTypeCode.UTF16CodeUnit))),
                                          new MetadataParameter("StringArray", new FixedLengthArrayMetadataType(2, new ArrayMetadataType(new MetadataType(MetadataTypeCode.UTF16CodeUnit))))));
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(999, 0, 0);
            });
            writer.WriteEventBlock(w =>
            {
                w.WriteEventBlob(1, 999, 1, p =>
                {
                    // ints
                    p.Write(10);
                    p.Write(20);
                    p.Write(30);

                    // UTF8 chars
                    p.Write(Encoding.UTF8.GetBytes("ABC"));
                    
                    // UTF16 chars
                    p.Write(Encoding.Unicode.GetBytes("xyz"));

                    // string[] ["Hi", "Bye"]
                    p.Write((ushort)2);
                    p.Write(Encoding.Unicode.GetBytes("Hi"));
                    p.Write((ushort)3);
                    p.Write(Encoding.Unicode.GetBytes("Bye"));
                });
            });
            writer.WriteEndBlock();
            
            MemoryStream stream = new MemoryStream(writer.ToArray());
            TraceEventDispatcher source = new EventPipeEventSource(stream);
            if (roundTripThroughEtlx)
            {
                MemoryStream etlxStream = new MemoryStream();
                TraceLog.CreateFromEventPipeEventSources(source, new IOStreamStreamWriter(etlxStream, SerializationSettings.Default, leaveOpen: true), null);
                etlxStream.Position = 0;
                source = new TraceLog(etlxStream).Events.GetSource();
            }
            int eventCount = 0;
            
            source.Dynamic.All += e =>
            {
                eventCount++;
                Assert.Equal("TestEvent1", e.EventName);
                Assert.Equal("TestProvider", e.ProviderName);
                Assert.Equal(4, e.PayloadNames.Length);
                
                // FixedArray
                Assert.Equal(typeof(int[]), e.PayloadValue(0).GetType());
                int[] array = (int[])e.PayloadValue(0);
                Assert.Equal(3, array.Length);
                Assert.Equal(10, array[0]);
                Assert.Equal(20, array[1]);
                Assert.Equal(30, array[2]);

                // UTF8
                Assert.Equal(typeof(string), e.PayloadValue(1).GetType());
                Assert.Equal("ABC", (string)e.PayloadValue(1));
                
                // UTF16
                Assert.Equal(typeof(string), e.PayloadValue(2).GetType());
                Assert.Equal("xyz", e.PayloadValue(2));

                // strings
                Assert.Equal(typeof(string[]), e.PayloadValue(3).GetType());
                string[] stringArray = (string[])e.PayloadValue(3);
                Assert.Equal(2, stringArray.Length);
                Assert.Equal("Hi", stringArray[0]);
                Assert.Equal("Bye", stringArray[1]);
            };
            
            source.Process();
            Assert.Equal(1, eventCount);
        }

        [Theory] //V6
        [InlineData(true)]
        [InlineData(false)]
        public void ParseV6RelLocAndDataLoc(bool roundTripThroughEtlx)
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(new EventMetadata(1, "TestProvider", "TestEvent1", 15,
                                      new MetadataParameter("RelLocArray", new RelLocMetadataType(new MetadataType(MetadataTypeCode.Int32))),
                                      new MetadataParameter("DataLocArray", new DataLocMetadataType(new MetadataType(MetadataTypeCode.Int32))),
                                      new MetadataParameter("RelLocString", new RelLocMetadataType(new MetadataType(MetadataTypeCode.UTF8CodeUnit))),
                                      new MetadataParameter("DataLocString", new DataLocMetadataType(new MetadataType(MetadataTypeCode.UTF16CodeUnit)))));
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(999, 0, 0);
            });
            writer.WriteEventBlock(w =>
            {
                w.WriteEventBlob(1, 999, 1, p =>
                {
                    // RelLoc layout: 4 bytes where the high 16 bits are size and low 16 bits are position
                    // Size = 12 (3 ints * 4 bytes each), position = 16 (after itself 4+16 = 20)
                    p.Write((12 << 16) | 16);

                    // DataLoc layout: 4 bytes where the high 16 bits are size and low 16 bits are position
                    // Size = 8 (2 ints * 4 bytes each), position = 32 (starting from beginning)
                    p.Write((8 << 16) | 32);

                    // RelLoc layout: 4 bytes where the high 16 bits are size and low 16 bits are position
                    // Size = 5 (5 UTF8 chars * 1 byte each), position = 28 (after itself 12+28 = 40)
                    p.Write((5 << 16) | 28);

                    // DataLoc layout: 4 bytes where the high 16 bits are size and low 16 bits are position
                    // Size = 10 (5 UTF16 chars * 2 byte each), position = 45 (starting from beginning)
                    p.Write((10 << 16) | 45);

                    // padding (offset 16)
                    p.Write(42);

                    // RelLoc array [1, 2, 3] located here (offset 20)
                    p.Write(1);
                    p.Write(2);
                    p.Write(3);

                    // DataLoc array [10, 20] located here (offset 32)
                    p.Write(10);
                    p.Write(20);

                    // RelLoc string "Hello" located here (offset 40)
                    p.Write(Encoding.UTF8.GetBytes("Hello"));

                    // DataLoc string "World" located here (offset 45)
                    p.Write(Encoding.Unicode.GetBytes("World"));
                });
            });
            writer.WriteEndBlock();

            MemoryStream stream = new MemoryStream(writer.ToArray());
            TraceEventDispatcher source = new EventPipeEventSource(stream);
            if(roundTripThroughEtlx)
            {
                MemoryStream etlxStream = new MemoryStream();
                TraceLog.CreateFromEventPipeEventSources(source, new IOStreamStreamWriter(etlxStream, SerializationSettings.Default, leaveOpen:true), null);
                etlxStream.Position = 0;
                source = new TraceLog(etlxStream).Events.GetSource();
            }

            int eventCount = 0;

            source.Dynamic.All += e =>
            {
                eventCount++;
                Assert.Equal("TestEvent1", e.EventName);
                Assert.Equal("TestProvider", e.ProviderName);
                Assert.Equal(4, e.PayloadNames.Length);

                // RelLoc array
                Assert.Equal(typeof(int[]), e.PayloadValue(0).GetType());
                int[] relLocArray = (int[])e.PayloadValue(0);
                Assert.Equal(3, relLocArray.Length);
                Assert.Equal(1, relLocArray[0]);
                Assert.Equal(2, relLocArray[1]);
                Assert.Equal(3, relLocArray[2]);

                // DataLoc array
                Assert.Equal(typeof(int[]), e.PayloadValue(1).GetType());
                int[] dataLocArray = (int[])e.PayloadValue(1);
                Assert.Equal(2, dataLocArray.Length);
                Assert.Equal(10, dataLocArray[0]);
                Assert.Equal(20, dataLocArray[1]);

                // RelLoc string
                Assert.Equal(typeof(string), e.PayloadValue(2).GetType());
                Assert.Equal("Hello", e.PayloadValue(2));

                // DataLoc string
                Assert.Equal(typeof(string), e.PayloadValue(3).GetType());
                Assert.Equal("World", e.PayloadValue(3));
            };

            source.Process();
            Assert.Equal(1, eventCount);
        }

        [Fact] //V6
        public void V6RelLocThrowsOnVariableSizedElementType()
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(new EventMetadata(1, "TestProvider", "TestEvent1", 15,
                                      new MetadataParameter("RelLocString", new RelLocMetadataType(new ArrayMetadataType(new MetadataType(MetadataTypeCode.UTF8CodeUnit))))));
            writer.WriteEndBlock();

            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);

            Assert.Throws<FormatException>(() => source.Process());
        }

        [Fact] //V6
        public void V6DataLocThrowsOnVariableSizedElementType()
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(new EventMetadata(1, "TestProvider", "TestEvent1", 15,
                                      new MetadataParameter("DataLocString", new DataLocMetadataType(new ArrayMetadataType(new MetadataType(MetadataTypeCode.UTF16CodeUnit))))));
            writer.WriteEndBlock();

            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);

            Assert.Throws<FormatException>(() => source.Process());
        }

        [Fact] //V6
        public void ParseV6MetadataObjectParam()
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(new EventMetadata(1, "TestProvider", "TestEvent1", 15,
                                          new MetadataParameter("Param1", new ObjectMetadataType(
                                              new MetadataParameter("NestedParam1", MetadataTypeCode.Int32),
                                              new MetadataParameter("NestedParam2", MetadataTypeCode.Byte))),
                                          new MetadataParameter("Param2", MetadataTypeCode.Boolean)));
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(999, 0, 0);
            });
            writer.WriteEventBlock(w =>
            {
                w.WriteEventBlob(1, 999, 1, new byte[] { 3, 0, 0, 0, 19, 1, 0, 0, 0 });
            });
            writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);
            int eventCount = 0;
            source.Dynamic.All += e =>
            {
                eventCount++;
                Assert.Equal($"TestEvent1", e.EventName);
                Assert.Equal("TestProvider", e.ProviderName);
                Assert.Equal(2, e.PayloadNames.Length);
                Assert.Equal("Param1", e.PayloadNames[0]);
                Assert.Equal("Param2", e.PayloadNames[1]);
                var o = (DynamicTraceEventData.StructValue)e.PayloadValue(0);
                Assert.Equal(2, o.Count);
                Assert.Equal(3, o["NestedParam1"]);
                Assert.Equal((byte)19, o["NestedParam2"]);
                Assert.Equal(true, e.PayloadValue(1));
            };
            source.Process();
            Assert.Equal(1, eventCount);
        }

        [Fact] //V6
        public void ParseV6OptionalMetadata()
        {
            Guid testGuid = Guid.Parse("CA0A7B93-622D-42C9-AFF8-7A09FDA2E30C");
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(new EventMetadata(1, "TestProvider", "TestEvent1", 15)
            {
                OpCode = 20,
                Keywords = 0x00ff00ff00ff00ff,
                Level = 4,
                Version = 22,
                MessageTemplate = "Thing happened param1={param1} param2={param2}",
                Description = "Yet another test event",
                Attributes =
                {
                    { "Animal", "Pony"},
                    { "Color", "Red" }
                },
                ProviderId = testGuid
            });
            writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);
            source.Process();

            Assert.True(source.TryGetMetadata(1, out EventPipeMetadata metadata));
            Assert.Equal((ulong)0x00ff00ff00ff00ff, metadata.Keywords);
            Assert.Equal(4, metadata.Level);
            Assert.Equal(22, metadata.EventVersion);
            Assert.Equal(20, metadata.Opcode.Value);
            Assert.Equal("Thing happened param1={param1} param2={param2}", metadata.MessageTemplate);
            Assert.Equal("Yet another test event", metadata.Description);
            Assert.Equal(2, metadata.Attributes.Count);
            Assert.Equal("Pony", metadata.Attributes["Animal"]);
            Assert.Equal("Red", metadata.Attributes["Color"]);
            Assert.Equal(testGuid, metadata.ProviderId);
        }

        // Ensure that we can add extra bytes into the metadata encoding everywhere the format says we should be allowed to
        [Fact] //V6
        public void SkipExtraMetadataSpaceV6()
        {
            Guid testGuid = Guid.Parse("CA0A7B93-622D-42C9-AFF8-7A09FDA2E30C");
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteBlock(3 /* Metadata */, blockWriter =>
            {
                // Add a metadata block header with some extra bytes
                blockWriter.Write((UInt16)17);
                for (int i = 0; i < 17; i++) blockWriter.Write((byte)i);

                blockWriter.WriteMetadataBlobV6OrGreater(metadataWriter =>
                {
                    metadataWriter.WriteV6InitialMetadataBlob(1, "TestProvider", "TestEvent", 15);
                    metadataWriter.WriteV6MetadataParameterList(2, parametersWriter =>
                    {
                        parametersWriter.WriteV6MetadataParameter("Param1", p1Writer =>
                        {
                            p1Writer.WriteV6MetadataType(new MetadataType(MetadataTypeCode.Int16));
                            p1Writer.Write(99); // extra bytes after the type are allowed
                        });
                        parametersWriter.WriteV6MetadataParameter("Param2", p2Writer =>
                        {
                            p2Writer.WriteV6MetadataType(new MetadataType(MetadataTypeCode.UInt32));
                            p2Writer.Write(0); // extra bytes after the type are allowed
                            p2Writer.Write(-14);
                        });
                    });
                    metadataWriter.WriteV6OptionalMetadataList(optionalMetadataWriter =>
                    {
                        optionalMetadataWriter.WriteV6OptionalMetadataDescription("Food!");
                    });
                    // extra bytes after the optional metadata are allowed
                    metadataWriter.Write(100);
                });
                blockWriter.WriteMetadataBlobV6OrGreater(new EventMetadata(2, "TestProvider", "TestEvent2", 16));
            });
            writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);
            source.Process();

            Assert.True(source.TryGetMetadata(1, out EventPipeMetadata metadata));
            Assert.Equal("Food!", metadata.Description);
            Assert.Equal(2, metadata.ParameterNames.Length);
            Assert.Equal("Param1", metadata.ParameterNames[0]);
            Assert.Equal("Param2", metadata.ParameterNames[1]);
            Assert.True(source.TryGetMetadata(2, out EventPipeMetadata metadata2));
            Assert.Equal("TestEvent2", metadata2.EventName);
        }


        [Fact] //V6
        public void ParseV6VarInts()
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(new EventMetadata(1, "TestProvider", "TestEvent1", 15,
                                          new MetadataParameter("VarInt1", MetadataTypeCode.VarInt),
                                          new MetadataParameter("VarInt2", MetadataTypeCode.VarInt)),
                                      new EventMetadata(2, "TestProvider", "TestEvent2", 16,
                                          new MetadataParameter("VarUInt1", MetadataTypeCode.VarUInt),
                                          new MetadataParameter("VarUInt2", MetadataTypeCode.VarUInt)));
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(999, 0, 0);
            });
            writer.WriteEventBlock(w =>
            {
                w.WriteEventBlob(1, 999, 1, p =>
                {
                    p.WriteVarInt(long.MinValue);
                    p.WriteVarInt(long.MaxValue);
                });
                w.WriteEventBlob(1, 999, 2, p =>
                {
                    p.WriteVarInt(0);
                    p.WriteVarInt(-17982);
                });
                w.WriteEventBlob(2, 999, 3, p =>
                {
                    p.WriteVarUInt(ulong.MinValue);
                    p.WriteVarUInt(ulong.MaxValue);
                });
                w.WriteEventBlob(2, 999, 4, p =>
                {
                    p.WriteVarUInt(0);
                    p.WriteVarUInt(17982);
                });
            });
            writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);
            int eventCount = 0;
            source.Dynamic.All += e =>
            {
                eventCount++;
                if (eventCount == 1)
                {
                    // reading the payloads out of order forces the parser to recompute the offsets from scratch instead of using a cache
                    Assert.Equal(long.MaxValue, e.PayloadValue(1));
                    Assert.Equal(long.MinValue, e.PayloadValue(0));
                }
                else if (eventCount == 2)
                {
                    Assert.Equal((long)0, e.PayloadValue(0));
                    Assert.Equal((long)-17982, e.PayloadValue(1));
                }
                else if (eventCount == 3)
                {
                    Assert.Equal(ulong.MaxValue, e.PayloadValue(1));
                    Assert.Equal(ulong.MinValue, e.PayloadValue(0));
                }
                else if (eventCount == 4)
                {
                    Assert.Equal((ulong)0, e.PayloadValue(0));
                    Assert.Equal((ulong)17982, e.PayloadValue(1));
                }
            };
            source.Process();
            Assert.Equal(4, eventCount);
        }

        // Ensure that the new string and varint types still work properly nested inside a struct
        [Fact] //V6
        public void ParseV6NestedVarIntsAndStrings()
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(new EventMetadata(1, "TestProvider", "TestEvent1", 15,
                                          new MetadataParameter("VarInt", MetadataTypeCode.VarInt),
                                          new MetadataParameter("Struct", new ObjectMetadataType(
                                              new MetadataParameter("UTF8String", new ArrayMetadataType(new MetadataType(MetadataTypeCode.UTF8CodeUnit))),
                                              new MetadataParameter("VarInt", MetadataTypeCode.VarInt),
                                              new MetadataParameter("UTF16String", new ArrayMetadataType(new MetadataType(MetadataTypeCode.UTF16CodeUnit))),
                                              new MetadataParameter("VarUInt", MetadataTypeCode.VarUInt)))));
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(999, 0, 0);
            });
            writer.WriteEventBlock(w =>
            {
                w.WriteEventBlob(1, 999, 1, p =>
                {
                    p.WriteVarInt(long.MinValue);
                    p.WriteLengthPrefixedUTF8String("UTF8");
                    p.WriteVarInt(long.MaxValue);
                    p.WriteLengthPrefixedUTF16String("UTF16");
                    p.WriteVarUInt(ulong.MaxValue);
                });
            });
            writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);
            int eventCount = 0;
            source.Dynamic.All += e =>
            {
                eventCount++;
                var o = (DynamicTraceEventData.StructValue)e.PayloadValue(1);
                Assert.Equal(ulong.MaxValue, (ulong)o["VarUInt"]);
                Assert.Equal("UTF16", (string)o["UTF16String"]);
                Assert.Equal(long.MaxValue, (long)o["VarInt"]);
                Assert.Equal("UTF8", (string)o["UTF8String"]);
            };
            source.Process();
            Assert.Equal(1, eventCount);
        }

        [Fact] //V6
        public void V6MissingThreadBlockThrowsException()
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(new EventMetadata(1, "TestProvider", "TestEvent1", 15));
            writer.WriteEventBlock(w =>
            {
                w.WriteEventBlob(1, 999, 1, p => { });
            });
            writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);
            Assert.Throws<FormatException>(() => source.Process());
        }

        [Fact] //V6
        public void V6MismatchedRemoveThreadBlockThrowsException()
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(new EventMetadata(1, "TestProvider", "TestEvent1", 15));
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(999, 0, 0);
            });
            writer.WriteEventBlock(w =>
            {
                w.WriteEventBlob(1, 999, 1, p => { });
            });
            writer.WriteRemoveThreadBlock(w =>
            {
                w.WriteRemoveThreadEntry(900, 0);
            });
            writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);
            Assert.Throws<FormatException>(() => source.Process());
        }

        [Fact] //V6
        public void V6RefAfterRemoveThreadBlockThrowsException()
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(new EventMetadata(1, "TestProvider", "TestEvent1", 15));
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(999, 0, 0);
            });
            writer.WriteRemoveThreadBlock(w =>
            {
                w.WriteRemoveThreadEntry(999, 0);
            });
            writer.WriteEventBlock(w =>
            {
                w.WriteEventBlob(1, 999, 1, p => { });
            });
            writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);
            Assert.Throws<FormatException>(() => source.Process());
        }

        [Fact] //V6
        public void V6DoubleRemoveThreadBlockThrowsException()
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(new EventMetadata(1, "TestProvider", "TestEvent1", 15));
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(999, 0, 0);
            });
            writer.WriteEventBlock(w =>
            {
                w.WriteEventBlob(1, 999, 1, p => { });
            });
            writer.WriteRemoveThreadBlock(w =>
            {
                w.WriteRemoveThreadEntry(999, 0);
            });
            writer.WriteRemoveThreadBlock(w =>
            {
                w.WriteRemoveThreadEntry(999, 0);
            });
            writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);
            Assert.Throws<FormatException>(() => source.Process());
        }

        [Fact] //V6
        public void V6ParseSimpleThreadBlock()
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(new EventMetadata(1, "TestProvider", "TestEvent1", 15));
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(998, threadId: 5, processId: 7);
                w.WriteThreadEntry(999, threadId: 12, processId: 84);
            });
            writer.WriteEventBlock(w =>
            {
                w.WriteEventBlob(1, 999, 1, p => { });
                w.WriteEventBlob(1, 998, 1, p => { });
                w.WriteEventBlob(1, 999, 2, p => { });
            });
            writer.WriteRemoveThreadBlock(w =>
            {
                w.WriteRemoveThreadEntry(998, 1);
                w.WriteRemoveThreadEntry(999, 2);
            });
            writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);
            int eventCount = 0;
            source.Dynamic.All += e =>
            {
                eventCount++;
                if (eventCount == 2)
                {
                    Assert.Equal(5, e.ThreadID);
                    Assert.Equal(7, e.ProcessID);
                }
                else
                {
                    Assert.Equal(12, e.ThreadID);
                    Assert.Equal(84, e.ProcessID);
                }
            };
            source.Process();
            Assert.Equal(3, eventCount);
        }

        [Fact] //V6
        public void V6ParseMultipleThreadBlocks()
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(new EventMetadata(1, "TestProvider", "TestEvent1", 15));
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(998, threadId: 5, processId: 7);
                w.WriteThreadEntry(999, threadId: 12, processId: 84);
            });
            writer.WriteEventBlock(w =>
            {
                w.WriteEventBlob(1, 999, 1, p => { });
                w.WriteEventBlob(1, 998, 1, p => { });
                w.WriteEventBlob(1, 999, 2, p => { });
            });
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(1000, threadId: 22, processId: 7);
            });
            writer.WriteEventBlock(w =>
            {
                w.WriteEventBlob(1, 1000, 1, p => { });
                w.WriteEventBlob(1, 1000, 2, p => { });
                w.WriteEventBlob(1, 999, 3, p => { });
            });
            writer.WriteRemoveThreadBlock(w =>
            {
                w.WriteRemoveThreadEntry(999, 3);
            });
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(1001, threadId: 79, processId: 7);
            });
            writer.WriteEventBlock(w =>
            {
                w.WriteEventBlob(1, 1000, 3, p => { });
                w.WriteEventBlob(1, 1001, 1, p => { });
            });
            writer.WriteRemoveThreadBlock(w =>
            {
                w.WriteRemoveThreadEntry(998, 1);
                w.WriteRemoveThreadEntry(1000, 3);
                w.WriteRemoveThreadEntry(1001, 1);
            });
            writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);
            int eventCount = 0;
            source.Dynamic.All += e =>
            {
                eventCount++;
                switch (eventCount)
                {
                    case 1:
                    case 3:
                    case 6:
                        {
                            Assert.Equal(12, e.ThreadID);
                            Assert.Equal(84, e.ProcessID);
                            break;
                        }
                    case 2:
                        {
                            Assert.Equal(5, e.ThreadID);
                            Assert.Equal(7, e.ProcessID);
                            break;
                        }
                    case 4:
                    case 5:
                    case 7:
                        {
                            Assert.Equal(22, e.ThreadID);
                            Assert.Equal(7, e.ProcessID);
                            break;
                        }
                    case 8:
                        {
                            Assert.Equal(79, e.ThreadID);
                            Assert.Equal(7, e.ProcessID);
                            break;
                        }
                }
            };
            source.Process();
            Assert.Equal(8, eventCount);
        }

        [Fact] //V6
        public void V6ParseOptionalThreadData()
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(new EventMetadata(1, "TestProvider", "TestEvent1", 15));
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(999, t =>
                {
                    t.WriteThreadEntryName("ThreadName");
                    t.WriteThreadEntryProcessId(124);
                    t.WriteThreadEntryKeyValue("Key1", "Value1");
                    t.WriteThreadEntryThreadId(123);
                    t.WriteThreadEntryKeyValue("Key2", "Value2");
                });
            });
            writer.WriteEventBlock(w =>
            {
                w.WriteEventBlob(1, 999, 1, p => { });
            });
            writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);
            int eventCount = 0;
            source.Dynamic.All += e =>
            {
                eventCount++;
                source.TryGetThread(999, out EventPipeThread thread);
                Assert.NotNull(thread);
                Assert.Equal("ThreadName", thread.Name);
                Assert.Equal(124, thread.ProcessId);
                Assert.Equal(123, thread.ThreadId);
                Assert.Equal("Value1", thread.Attributes["Key1"]);
                Assert.Equal("Value2", thread.Attributes["Key2"]);
            };
            source.Process();
            Assert.Equal(1, eventCount);
        }

        [Theory] //V6
        [InlineData(true)]
        [InlineData(false)]
        public void V6ParseEventLabelLists(bool useCompressedEventHeaders)
        {
            // these are arbitrary random constants
            Guid activityId1 = new Guid("26F353D1-C0C8-45C1-A0CF-7C29EAE6DC7F");
            Guid relatedActivityId1 = new Guid("E1D6B9EB-84F8-4908-8FA7-FA63D5F02849");
            Guid activityId2 = new Guid("0BBEAA1B-35B4-4FF3-BA34-283EBE589A9A");
            Guid relatedActivityId2 = new Guid("AF148B25-6675-4AD8-ADB8-C247F8FB6AFA");
            Guid traceId = new Guid("7C0D2E78-70A5-4233-A0AD-ACCFD3E1EF6E");
            ulong spanId = 0x123456789abcdef0;

            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(new EventMetadata(1, "TestProvider", "TestEvent1", 15));
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(999, threadId: 12, processId: 84);
            });
            writer.WriteLabelListBlock(99, 2, w =>
            {
                w.WriteActivityIdLabel(activityId1);
                w.WriteRelatedActivityIdLabel(relatedActivityId1, isLastLabel: true);
                w.WriteNameValueStringLabel("Key1", "Value1");
                w.WriteNameValueVarIntLabel("Key2", 123, isLastLabel: true);
            });
            writer.WriteLabelListBlock(7, 2, w =>
            {
                w.WriteNameValueStringLabel("Key3", "Value3");
                w.WriteActivityIdLabel(activityId2);
                w.WriteRelatedActivityIdLabel(relatedActivityId2, isLastLabel: true);
                w.WriteTraceIdLabel(traceId.ToByteArray());
                w.WriteSpanIdLabel(spanId, isLastLabel: true);
            });
            writer.WriteEventBlock(useCompressedEventHeaders, w =>
            {
                w.WriteEventBlob(new WriteEventOptions() { MetadataId = 1, SequenceNumber = 1, ThreadIndexOrId = 999, CaptureThreadIndexOrId = 999, LabelListId = 7 }, p => { });
                w.WriteEventBlob(new WriteEventOptions() { MetadataId = 1, SequenceNumber = 2, ThreadIndexOrId = 999, CaptureThreadIndexOrId = 999, LabelListId = 8 }, p => { });
                w.WriteEventBlob(new WriteEventOptions() { MetadataId = 1, SequenceNumber = 3, ThreadIndexOrId = 999, CaptureThreadIndexOrId = 999, LabelListId = 99 }, p => { });
                w.WriteEventBlob(new WriteEventOptions() { MetadataId = 1, SequenceNumber = 4, ThreadIndexOrId = 999, CaptureThreadIndexOrId = 999, LabelListId = 100 }, p => { });
                w.WriteEventBlob(new WriteEventOptions() { MetadataId = 1, SequenceNumber = 5, ThreadIndexOrId = 999, CaptureThreadIndexOrId = 999, LabelListId = 0 }, p => { });
                w.WriteEventBlob(new WriteEventOptions() { MetadataId = 1, SequenceNumber = 6, ThreadIndexOrId = 999, CaptureThreadIndexOrId = 999, LabelListId = 7 }, p => { });
            });
            writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);
            int eventCount = 0;
            source.Dynamic.All += e =>
            {
                eventCount++;
                if (eventCount == 1 || eventCount == 6)
                {
                    Assert.Equal(activityId2, e.ActivityID);
                    Assert.Equal(relatedActivityId2, e.RelatedActivityID);
                    LabelList labels = source.GetLastLabelList();
                    KeyValuePair<string, object>[] labelArray = labels.AllLabels.ToArray();
                    Assert.Equal(3, labelArray.Length);
                    Assert.Equal("ActivityId", labelArray[0].Key);
                    Assert.Equal(activityId2.ToString(), labelArray[0].Value);
                    Assert.Equal("RelatedActivityId", labelArray[1].Key);
                    Assert.Equal(relatedActivityId2.ToString(), labelArray[1].Value);
                    Assert.Equal("Key3", labelArray[2].Key);
                    Assert.Equal("Value3", labelArray[2].Value);
                    Assert.Equal(activityId2, labels.ActivityId);
                    Assert.Equal(relatedActivityId2, labels.RelatedActivityId);
                }
                else if (eventCount == 2)
                {
                    Assert.Equal(Guid.Empty, e.ActivityID);
                    Assert.Equal(Guid.Empty, e.RelatedActivityID);
                    LabelList labels = source.GetLastLabelList();
                    KeyValuePair<string, object>[] labelArray = labels.AllLabels.ToArray();
                    Assert.Equal(2, labelArray.Length);
                    Assert.Equal("TraceId", labelArray[0].Key);
                    Assert.Equal(traceId.ToString(), labelArray[0].Value);
                    Assert.Equal(traceId, labels.TraceId);
                    Assert.Equal(spanId, labels.SpanId);
                }
                else if (eventCount == 3)
                {
                    Assert.Equal(activityId1, e.ActivityID);
                    Assert.Equal(relatedActivityId1, e.RelatedActivityID);
                    LabelList labels = source.GetLastLabelList();
                    KeyValuePair<string, object>[] labelArray = labels.AllLabels.ToArray();
                    Assert.Equal(2, labelArray.Length);
                    Assert.Equal("ActivityId", labelArray[0].Key);
                    Assert.Equal(activityId1.ToString(), labelArray[0].Value);
                    Assert.Equal("RelatedActivityId", labelArray[1].Key);
                    Assert.Equal(relatedActivityId1.ToString(), labelArray[1].Value);
                    Assert.Equal(activityId1, labels.ActivityId);
                    Assert.Equal(relatedActivityId1, labels.RelatedActivityId);
                }
                else if (eventCount == 4)
                {
                    Assert.Equal(Guid.Empty, e.ActivityID);
                    Assert.Equal(Guid.Empty, e.RelatedActivityID);
                    LabelList labels = source.GetLastLabelList();
                    KeyValuePair<string, object>[] labelArray = labels.AllLabels.ToArray();
                    Assert.Equal(2, labelArray.Length);
                    Assert.Equal("Key1", labelArray[0].Key);
                    Assert.Equal("Value1", labelArray[0].Value);
                    Assert.Equal("Key2", labelArray[1].Key);
                    Assert.Equal(123, (long)labelArray[1].Value);
                }
                else if (eventCount == 5)
                {
                    Assert.Equal(Guid.Empty, e.ActivityID);
                    Assert.Equal(Guid.Empty, e.RelatedActivityID);
                    LabelList labels = source.GetLastLabelList();
                    Assert.Empty(labels.AllLabels);
                }
            };
            source.Process();
            Assert.Equal(6, eventCount);
        }

        [Fact]
        public void V6LabelListCanOverrideTemplate()
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(new EventMetadata(1, "Microsoft-Windows-DotNETRuntime", "GCEnd", 2));
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(999, threadId: 12, processId: 84);
            });
            writer.WriteLabelListBlock(7, 2, w =>
            {
                w.WriteOpCodeLabel(8);
                w.WriteKeywordsLabel(0x123456789abcef);
                w.WriteLevelLabel(19);
                w.WriteVersionLabel(4, isLastLabel: true);
                w.WriteOpCodeLabel(0);
                w.WriteKeywordsLabel(0);
                w.WriteLevelLabel(0);
                w.WriteVersionLabel(0, isLastLabel: true);
            });
            writer.WriteEventBlock(w =>
            {
                w.WriteEventBlob(new WriteEventOptions() { MetadataId = 1, SequenceNumber = 1, ThreadIndexOrId = 999, CaptureThreadIndexOrId = 999, LabelListId = 7 }, p => { });
                w.WriteEventBlob(new WriteEventOptions() { MetadataId = 1, SequenceNumber = 2, ThreadIndexOrId = 999, CaptureThreadIndexOrId = 999, LabelListId = 8 }, p => { });
                w.WriteEventBlob(new WriteEventOptions() { MetadataId = 1, SequenceNumber = 3, ThreadIndexOrId = 999, CaptureThreadIndexOrId = 999, LabelListId = 0 }, p => { });
            });
            writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);
            int eventCount = 0;
            source.Clr.GCStop += e =>
            {
                eventCount++;
                if (eventCount == 1)
                {
                    // Suspend is opcode 8
                    Assert.Equal("GC/Suspend", e.EventName);
                    Assert.Equal("Microsoft-Windows-DotNETRuntime", e.ProviderName);
                    Assert.Equal(8, (int)e.Opcode);
                    Assert.Equal(0x123456789abcefUL, (ulong)e.Keywords);
                    Assert.Equal(19, (int)e.Level);
                    Assert.Equal(4, e.Version);
                }
                else if (eventCount == 2)
                {
                    Assert.Equal("GC", e.EventName);
                    Assert.Equal("Microsoft-Windows-DotNETRuntime", e.ProviderName);
                    Assert.Equal(0, (int)e.Opcode);
                    Assert.Equal(0UL, (ulong)e.Keywords);
                    Assert.Equal(0, (int)e.Level);
                    Assert.Equal(0, e.Version);
                }
                else if (eventCount == 3)
                {
                    Assert.Equal("GC/Stop", e.EventName);
                    Assert.Equal("Microsoft-Windows-DotNETRuntime", e.ProviderName);
                    Assert.Equal(2, (int)e.Opcode);
                    Assert.Equal(0UL, (ulong)e.Keywords);
                    Assert.Equal(0, (int)e.Level);
                    Assert.Equal(0, e.Version);
                }
            };
            source.Process();
            Assert.Equal(3, eventCount);
        }

        [Fact] //V6
        public void V6LabelListCanOverrideMetadata()
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(new EventMetadata(1, "TestProvider", "TestEvent1", 15));
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(999, threadId: 12, processId: 84);
            });
            writer.WriteLabelListBlock(7, 2, w =>
            {
                w.WriteOpCodeLabel(8);
                w.WriteKeywordsLabel(0x123456789abcef);
                w.WriteLevelLabel(19);
                w.WriteVersionLabel(4, isLastLabel:true);
                w.WriteOpCodeLabel(18);
                w.WriteKeywordsLabel(0x234);
                w.WriteLevelLabel(2);
                w.WriteVersionLabel(3, isLastLabel: true);
            });
            writer.WriteEventBlock(w =>
            {
                w.WriteEventBlob(new WriteEventOptions() { MetadataId = 1, SequenceNumber = 1, ThreadIndexOrId = 999, CaptureThreadIndexOrId = 999, LabelListId = 7 }, p => { });
                w.WriteEventBlob(new WriteEventOptions() { MetadataId = 1, SequenceNumber = 2, ThreadIndexOrId = 999, CaptureThreadIndexOrId = 999, LabelListId = 8 }, p => { });
                w.WriteEventBlob(new WriteEventOptions() { MetadataId = 1, SequenceNumber = 3, ThreadIndexOrId = 999, CaptureThreadIndexOrId = 999, LabelListId = 0 }, p => { });
            });
            writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);
            int eventCount = 0;
            source.Dynamic.All += e =>
            {
                eventCount++;
                if (eventCount == 1)
                {
                    // Suspend is opcode 8
                    Assert.Equal("TestEvent1/Suspend", e.EventName);
                    Assert.Equal("TestProvider", e.ProviderName);
                    Assert.Equal(8, (int)e.Opcode);
                    Assert.Equal(0x123456789abcefUL, (ulong)e.Keywords);
                    Assert.Equal(19, (int)e.Level);
                    Assert.Equal(4, e.Version);
                }
                else if (eventCount == 2)
                {
                    Assert.Equal("TestEvent1/Opcode(18)", e.EventName);
                    Assert.Equal("TestProvider", e.ProviderName);
                    Assert.Equal(18, (int)e.Opcode);
                    Assert.Equal(0x234UL, (ulong)e.Keywords);
                    Assert.Equal(2, (int)e.Level);
                    Assert.Equal(3, e.Version);
                }
                else if (eventCount == 3)
                {
                    Assert.Equal("TestEvent1", e.EventName);
                    Assert.Equal("TestProvider", e.ProviderName);
                    Assert.Equal(0, (int)e.Opcode);
                    Assert.Equal(0UL, (ulong)e.Keywords);
                    Assert.Equal(0, (int)e.Level);
                    Assert.Equal(0, e.Version);
                }
            };
            source.Process();
            Assert.Equal(3, eventCount);
        }


        [Fact] //V6
        public void ParseV6CompressedEventHeaders()
        {
            // these are arbitrary random constants
            Guid activityId1 = new Guid("26F353D1-C0C8-45C1-A0CF-7C29EAE6DC7F");
            Guid activityId2 = new Guid("0BBEAA1B-35B4-4FF3-BA34-283EBE589A9A");

            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(new EventMetadata(1, "TestProvider", "TestEvent1", 15),
                                      new EventMetadata(2, "TestProvider", "TestEvent2", 16));
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(999, threadId: 12, processId: 84);
                w.WriteThreadEntry(1000, threadId: 13, processId: 84);
                w.WriteThreadEntry(1001, threadId: 14, processId: 84);
            });
            writer.WriteLabelListBlock(99, 2, w =>
            {
                w.WriteActivityIdLabel(activityId1, isLastLabel: true);
                w.WriteActivityIdLabel(activityId2, isLastLabel: true);
            });
            writer.WriteEventBlock(true, w =>
            {
                w.WriteEventBlob(new WriteEventOptions() { MetadataId = 1, SequenceNumber = 1, ThreadIndexOrId = 999, CaptureThreadIndexOrId = 999, LabelListId = 100, Timestamp = 10_000, IsSorted = false }, p => { });
                w.WriteEventBlob(new WriteEventOptions() { MetadataId = 1, SequenceNumber = 2, ThreadIndexOrId = 1000, CaptureThreadIndexOrId = 999, LabelListId = 99, Timestamp = 10_500, IsSorted = false }, p => { });
                w.WriteEventBlob(new WriteEventOptions() { MetadataId = 2, SequenceNumber = 3, ThreadIndexOrId = 1000, CaptureThreadIndexOrId = 1000, LabelListId = 99, Timestamp = 11_123, IsSorted = true }, p => { });
                w.WriteEventBlob(new WriteEventOptions() { MetadataId = 2, SequenceNumber = 4, ThreadIndexOrId = 1000, CaptureThreadIndexOrId = 1000, LabelListId = 99, Timestamp = 17_000, IsSorted = false }, p => { });
                w.WriteEventBlob(new WriteEventOptions() { MetadataId = 2, SequenceNumber = 5, ThreadIndexOrId = 999, CaptureThreadIndexOrId = 999, LabelListId = 0, Timestamp = 15_000, IsSorted = true }, p => { });
                w.WriteEventBlob(new WriteEventOptions() { MetadataId = 2, SequenceNumber = 6, ThreadIndexOrId = 1001, CaptureThreadIndexOrId = 1000, LabelListId = 0, Timestamp = 20_000, IsSorted = true }, p => { });
                w.WriteEventBlob(new WriteEventOptions() { MetadataId = 1, SequenceNumber = 2, ThreadIndexOrId = 999, CaptureThreadIndexOrId = 1001, LabelListId = 100, Timestamp = 22_000, IsSorted = true }, p => { });
            });
            writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);
            int eventCount = 0;
            source.Dynamic.All += e =>
            {
                eventCount++;
#pragma warning disable CS0618 // TimeStampQPC is obsolete
                if (eventCount == 1)
                {
                    Assert.Equal("TestEvent1", e.EventName);
                    Assert.Equal(12, e.ThreadID);
                    Assert.Equal(activityId2, e.ActivityID);
                    Assert.Equal(10_000, e.TimeStampQPC);

                }
                else if (eventCount == 2)
                {
                    Assert.Equal("TestEvent1", e.EventName);
                    Assert.Equal(13, e.ThreadID);
                    Assert.Equal(activityId1, e.ActivityID);
                    Assert.Equal(10_500, e.TimeStampQPC);
                }
                else if (eventCount == 3)
                {
                    Assert.Equal("TestEvent2", e.EventName);
                    Assert.Equal(13, e.ThreadID);
                    Assert.Equal(activityId1, e.ActivityID);
                    Assert.Equal(11_123, e.TimeStampQPC);
                }
                else if (eventCount == 4)
                {
                    Assert.Equal("TestEvent2", e.EventName);
                    Assert.Equal(12, e.ThreadID);
                    Assert.Equal(Guid.Empty, e.ActivityID);
                    Assert.Equal(15_000, e.TimeStampQPC);
                }
                else if (eventCount == 5)
                {
                    Assert.Equal("TestEvent2", e.EventName);
                    Assert.Equal(13, e.ThreadID);
                    Assert.Equal(activityId1, e.ActivityID);
                    Assert.Equal(17_000, e.TimeStampQPC);
                }
                else if (eventCount == 6)
                {
                    Assert.Equal("TestEvent2", e.EventName);
                    Assert.Equal(14, e.ThreadID);
                    Assert.Equal(Guid.Empty, e.ActivityID);
                    Assert.Equal(20_000, e.TimeStampQPC);
                }
                else if (eventCount == 7)
                {
                    Assert.Equal("TestEvent1", e.EventName);
                    Assert.Equal(12, e.ThreadID);
                    Assert.Equal(activityId2, e.ActivityID);
                    Assert.Equal(22_000, e.TimeStampQPC);
                }
#pragma warning restore CS0618 // Type or member is obsolete
            };
            source.Process();
            Assert.Equal(7, eventCount);
        }

        [Fact] //V6
        public void V6SequencePointDoesNotFlushThreadsByDefault()
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(new EventMetadata(1, "TestProvider", "TestEvent1", 15));
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(999, threadId: 12, processId: 84);
            });
            writer.WriteSequencePointBlock(0, resetThreadIndicies: false, resetMetadataIndices: false);
            writer.WriteEventBlock(true, w =>
            {
                w.WriteEventBlob(1, 999, 1, p => { });
            });
            writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);

            int eventCount = 0;
            source.Dynamic.All += e =>
            {
                eventCount++;
                Assert.Equal("TestEvent1", e.EventName);
                Assert.Equal(12, e.ThreadID);
            };
            source.Process();
            Assert.Equal(1, eventCount);
        }

        [Fact] //V6
        public void V6RedefinedThreadIndexThrowsFormatException()
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(new EventMetadata(1, "TestProvider", "TestEvent1", 15));
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(999, threadId: 12, processId: 84);
            });
            writer.WriteSequencePointBlock(0, resetThreadIndicies: false, resetMetadataIndices: false);
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(999, threadId: 15, processId: 84);
            });
            writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);

            Assert.Throws<FormatException>(() => source.Process());
        }

        [Fact] //V6
        public void V6SequencePointCanFlushThreadsOnDemand()
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(new EventMetadata(1, "TestProvider", "TestEvent1", 15));
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(999, threadId: 12, processId: 84);
            });
            writer.WriteSequencePointBlock(0, resetThreadIndicies: true, resetMetadataIndices: false);
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(999, threadId: 15, processId: 84);
            });
            writer.WriteEventBlock(true, w =>
            {
                w.WriteEventBlob(1, 999, 1, p => { });
            });
            writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);

            int eventCount = 0;
            source.Dynamic.All += e =>
            {
                eventCount++;
                Assert.Equal("TestEvent1", e.EventName);
                Assert.Equal(15, e.ThreadID);
            };
            source.Process();
            Assert.Equal(1, eventCount);
        }

        [Fact] //V6
        public void V6SequencePointDoesNotFlushMetadataByDefault()
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(new EventMetadata(1, "TestProvider", "TestEvent1", 15));
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(999, threadId: 12, processId: 84);
            });
            writer.WriteSequencePointBlock(0, resetThreadIndicies: false, resetMetadataIndices: false);
            writer.WriteEventBlock(true, w =>
            {
                w.WriteEventBlob(1, 999, 1, p => { });
            });
            writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);

            int eventCount = 0;
            source.Dynamic.All += e =>
            {
                eventCount++;
                Assert.Equal("TestEvent1", e.EventName);
                Assert.Equal(12, e.ThreadID);
            };
            source.Process();
            Assert.Equal(1, eventCount);
        }

        [Fact] //V6
        public void V6RedefinedMetadataIndexThrowsFormatException()
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(new EventMetadata(1, "TestProvider", "TestEvent1", 15));
            writer.WriteSequencePointBlock(0, resetThreadIndicies: false, resetMetadataIndices: false);
            writer.WriteMetadataBlock(new EventMetadata(1, "TestProvider", "TestEvent2", 19));
            writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);

            Assert.Throws<FormatException>(() => source.Process());
        }

        [Fact] //V6
        public void V6SequencePointCanFlushMetadataOnDemand()
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(new EventMetadata(1, "TestProvider", "TestEvent1", 15));
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(999, threadId: 12, processId: 84);
            });
            writer.WriteSequencePointBlock(0, resetThreadIndicies: false, resetMetadataIndices: true);
            writer.WriteMetadataBlock(new EventMetadata(1, "TestProvider", "TestEvent2", 19));
            writer.WriteEventBlock(true, w =>
            {
                w.WriteEventBlob(1, 999, 1, p => { });
            });
            writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);

            int eventCount = 0;
            source.Dynamic.All += e =>
            {
                eventCount++;
                Assert.Equal("TestEvent2", e.EventName);
            };
            source.Process();
            Assert.Equal(1, eventCount);
        }

        [Fact] //V6
        public void V6SequencePointDetectsDroppedEvents()
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(new EventMetadata(1, "TestProvider", "TestEvent1", 15));
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(999, threadId: 12, processId: 84);
            });
            writer.WriteSequencePointBlock(0, resetThreadIndicies: true, resetMetadataIndices: false, new V6ThreadSequencePoint(999, 5));
            writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);
            source.Process();
            Assert.Equal(5, source.EventsLost);
        }

        [Fact] //V6
        public void V6IncompleteTraceThrowsException()
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            // deliberately missing writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);

            // Past versions of TraceEvent threw System.Exception instead of FormatException
            // Now we are trying to hold this behavior consistent
            Assert.Throws<FormatException>(() => source.Process()); 
        }

        [Fact]
        public void V5IncompleteTraceThrowsException()
        {
            EventPipeWriterV5 writer = new EventPipeWriterV5();
            writer.WriteHeaders();
            // deliberately missing writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);

            // Past versions of TraceEvent threw System.Exception instead of FormatException
            // Now we are trying to hold this behavior consistent
            Assert.Throws<FormatException>(() => source.Process());
        }

#if NETCOREAPP
        [Fact]
        public void StreamExtensionsUsesFastPath()
        {
            Assert.True(StreamExtensions.IsFastSpanReadAvailable);
        }
#else
        [Fact]
        public void StreamExtensionsDoesNotUseFastPath()
        {
            Assert.False(StreamExtensions.IsFastSpanReadAvailable);
        }
#endif

        [Fact]
        void V5StartStopOpcodeRemovedFromEventNames()
        {
            EventPipeWriterV5 writer = new EventPipeWriterV5();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(
                new EventMetadata(1, "TestProvider", "ActivityStart", 15) { OpCode = (byte)EventOpcode.Start },
                new EventMetadata(2, "TestProvider", "ActivityStop", 16) { OpCode = (byte)EventOpcode.Stop },
                new EventMetadata(3, "TestProvider2", "ActivityStart", 15),
                new EventMetadata(4, "TestProvider2", "ActivityStop", 16));
            writer.WriteEventBlock(w =>
            {
                w.WriteEventBlobV4Or5(1, 999, 1, Array.Empty<byte>());
                w.WriteEventBlobV4Or5(2, 999, 2, Array.Empty<byte>());
                w.WriteEventBlobV4Or5(3, 999, 3, Array.Empty<byte>());
                w.WriteEventBlobV4Or5(4, 999, 4, Array.Empty<byte>());
            });
            writer.WriteEndObject();
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);
            int eventCount = 0;
            source.Dynamic.All += e =>
            {
                eventCount++;
                if (eventCount == 1)
                {
                    Assert.Equal("Activity/Start", e.EventName);
                }
                else if (eventCount == 2)
                {
                    Assert.Equal("Activity/Stop", e.EventName);
                }
                else if (eventCount == 3)
                {
                    Assert.Equal("Activity/Start", e.EventName);
                }
                else if (eventCount == 4)
                {
                    Assert.Equal("Activity/Stop", e.EventName);
                }
            };
            source.Process();
            Assert.Equal(4, eventCount);

        }

        [Fact]
        void V6StartStopOpcodeRemovedFromEventNames()
        {
            EventPipeWriterV6 writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(
                new EventMetadata(1, "TestProvider", "ActivityStart", 15) {  OpCode = (byte)EventOpcode.Start},
                new EventMetadata(2, "TestProvider", "ActivityStop", 16) { OpCode = (byte)EventOpcode.Stop },
                new EventMetadata(3, "TestProvider2", "ActivityStart", 15),
                new EventMetadata(4, "TestProvider2", "ActivityStop", 16));
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(999, threadId: 12, processId: 84);
            });
            writer.WriteEventBlock(w =>
            {
                w.WriteEventBlob(1, 999, 1, Array.Empty<byte>());
                w.WriteEventBlob(2, 999, 2, Array.Empty<byte>());
                w.WriteEventBlob(3, 999, 3, Array.Empty<byte>());
                w.WriteEventBlob(4, 999, 4, Array.Empty<byte>());
            });
            writer.WriteEndBlock();
            MemoryStream stream = new MemoryStream(writer.ToArray());
            EventPipeEventSource source = new EventPipeEventSource(stream);
            int eventCount = 0;
            source.Dynamic.All += e =>
            {
                eventCount++;
                if (eventCount == 1)
                {
                    Assert.Equal("Activity/Start", e.EventName);
                }
                else if(eventCount == 2)
                {
                    Assert.Equal("Activity/Stop", e.EventName);
                }
                else if (eventCount == 3)
                {
                    Assert.Equal("Activity/Start", e.EventName);
                }
                else if (eventCount == 4)
                {
                    Assert.Equal("Activity/Stop", e.EventName);
                }
            };
            source.Process();
            Assert.Equal(4, eventCount);

        }
    }


    class MockStreamingOnlyStream : Stream
    {
        Stream _innerStream;
        public MockStreamingOnlyStream(Stream innerStream)
        {
            _innerStream = innerStream;
        }
        public long TestOnlyPosition { get { return _innerStream.Position; } }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotImplementedException();
        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override void Flush()
        {
            throw new NotImplementedException();
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            return _innerStream.Read(buffer, offset, count);
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }
        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }


    public class EventMetadata
    {
        public EventMetadata(int metadataId, string providerName, string eventName, int eventId, params MetadataParameter[] parameters)
        {
            MetadataId = metadataId;
            ProviderName = providerName;
            EventName = eventName;
            EventId = eventId;
            Parameters = parameters;
        }

        public int MetadataId { get; set; }
        public string ProviderName { get; set; }
        public string EventName { get; set; }
        public int EventId { get; set; }
        public MetadataParameter[] Parameters { get; set; }

        // V6 Optional metadata
        public byte OpCode { get; set; }
        public string Description { get; set; }
        public string MessageTemplate { get; set; }
        public Guid ProviderId { get; set; }
        public long Keywords { get; set; }
        public byte Level { get; set; }
        public byte Version { get; set; }
        public Dictionary<string, string> Attributes { get; set; } = new Dictionary<string, string>();
        
    }

    public class MetadataParameter
    {
        public MetadataParameter(string name, MetadataType type)
        {
            Name = name;
            Type = type;
        }
        public MetadataParameter(string name, MetadataTypeCode typeCode)
        {
            Name = name;
            Type = new MetadataType(typeCode);
        }
        public string Name { get; set; }
        public MetadataType Type { get; set; }
    }

    public class MetadataType
    {
        public MetadataType(MetadataTypeCode typeCode)
        {
            TypeCode = typeCode;
        }
        public MetadataTypeCode TypeCode { get; set; }
    }

    public class ArrayMetadataType : MetadataType
    {
        public ArrayMetadataType(MetadataType elementType) : base(MetadataTypeCode.Array)
        {
            ElementType = elementType;
        }
        public MetadataType ElementType { get; set; }
    }

    public class ObjectMetadataType : MetadataType
    {
        public ObjectMetadataType(params MetadataParameter[] parameters) : base(MetadataTypeCode.Object)
        {
            Parameters = parameters;
        }
        public MetadataParameter[] Parameters { get; set; }
    }

    public class FixedLengthArrayMetadataType : MetadataType
    {
        public FixedLengthArrayMetadataType(int elementCount, MetadataType elementType) : base(MetadataTypeCode.FixedLengthArray)
        {
            ElementType = elementType;
            ElementCount = elementCount;
        }
        public MetadataType ElementType { get; set; }
        public int ElementCount { get; set; }
    }

    public class RelLocMetadataType : MetadataType
    {
        public RelLocMetadataType(MetadataType elementType) : base(MetadataTypeCode.RelLoc)
        {
            ElementType = elementType;
        }
        public MetadataType ElementType { get; set; }
    }

    public class DataLocMetadataType : MetadataType
    {
        public DataLocMetadataType(MetadataType elementType) : base(MetadataTypeCode.DataLoc)
        {
            ElementType = elementType;
        }
        public MetadataType ElementType { get; set; }
    }

    public enum MetadataTypeCode
    {
        Object = 1,                        // Concatenate together all of the encoded fields
        Boolean = 3,                       // A 4-byte LE integer with value 0=false and 1=true.  
        UTF16CodeUnit = 4,                 // a 2-byte UTF16 code unit
        SByte = 5,                         // 1-byte signed integer
        Byte = 6,                          // 1-byte unsigned integer
        Int16 = 7,                         // 2-byte signed LE integer
        UInt16 = 8,                        // 2-byte unsigned LE integer
        Int32 = 9,                         // 4-byte signed LE integer
        UInt32 = 10,                       // 4-byte unsigned LE integer
        Int64 = 11,                        // 8-byte signed LE integer
        UInt64 = 12,                       // 8-byte unsigned LE integer
        Single = 13,                       // 4-byte single-precision IEEE754 floating point value
        Double = 14,                       // 8-byte double-precision IEEE754 floating point value
        DateTime = 16,                     // Encoded as 8 concatenated Int16s representing year, month, dayOfWeek, day, hour, minute, second, and milliseconds.
        Guid = 17,                         // A 16-byte guid encoded as the concatenation of an Int32, 2 Int16s, and 8 Uint8s
        NullTerminatedUTF16String = 18,    // A string encoded with UTF16 characters and a 2-byte null terminator
        Array = 19,                        // New in V5 optional params: a UInt16 length-prefixed variable-sized array. Elements are encoded depending on the ElementType.
        VarInt = 20,                       // New in V6: variable-length signed integer with zig-zag encoding (defined the same as in Protobuf)
        VarUInt = 21,                      // New in V6: variable-length unsigned integer (ULEB128)
        FixedLengthArray = 22,             // New in V6: A fixed-length array of elements. The size is determined by the metadata.
        UTF8CodeUnit = 23,                 // New in V6: A single UTF8 code unit (1 byte).
        RelLoc = 24,                       // New in V6: An array at a relative location within the payload.
        DataLoc = 25                       // New in V6: An absolute data location within the payload.
    }

    public class EventPayloadWriter
    {
        BinaryWriter _writer = new BinaryWriter(new MemoryStream());

        public void WriteNullTerminatedUTF16String(string arg)
        {
            _writer.Write(Encoding.Unicode.GetBytes(arg));
            _writer.Write((ushort)0);
        }

        public void WriteArray<T>(T[] elements, Action<T> writeElement)
        {
            WriteArrayLength(elements.Length);
            for (int i = 0; i < elements.Length; i++)
            {
                writeElement(elements[i]);
            }
        }

        public void WriteArrayLength(int length)
        {
            _writer.Write((ushort)length);
        }

        public byte[] ToArray()
        {
            return (_writer.BaseStream as MemoryStream).ToArray();
        }
    }

    abstract class EventPipeWriter
    {
        protected BinaryWriter _writer;

        public EventPipeWriter()
        {
            _writer = new BinaryWriter(new MemoryStream());
        }

        public byte[] ToArray()
        {
            return (_writer.BaseStream as MemoryStream).ToArray();
        }

        abstract public void WriteHeaders();
        abstract public void WriteMetadataBlock(params EventMetadata[] metadataBlobs);
    }

    class EventPipeWriterV5 : EventPipeWriter
    {
        public override void WriteHeaders()
        {
            _writer.WriteNetTraceHeaderV5();
            _writer.WriteFastSerializationHeader();
            _writer.WriteTraceObjectV5();
        }
        public override void WriteMetadataBlock(params EventMetadata[] metadataBlobs)
        {
            _writer.WriteMetadataBlockV5OrLess(metadataBlobs);
        }
        public void WriteMetadataBlock(Action<BinaryWriter> writeMetadataEventBlobs)
        {
            _writer.WriteMetadataBlockV5OrLess(writeMetadataEventBlobs);
        }

        public void WriteEventBlock(Action<BinaryWriter> writeEventBlobs)
        {
            _writer.WriteEventBlockV5OrLess(writeEventBlobs);
        }

        public void WriteEndObject()
        {
            _writer.WriteEndObject();
        }

        public void WriteBlock(string name, Action<BinaryWriter> writeBlockData, long previousBytesWritten = 0)
        {
            _writer.WriteBlockV5OrLess(name, writeBlockData, previousBytesWritten);
        }
    }

    public struct V6ThreadSequencePoint
    {
        public V6ThreadSequencePoint(ulong threadIndex, uint sequenceNumber)
        {
            ThreadIndex = threadIndex;
            SequenceNumber = sequenceNumber;
        }
        public ulong ThreadIndex;
        public uint SequenceNumber;
    }

    class EventPipeWriterV6 : EventPipeWriter
    {
        public override void WriteHeaders() => WriteHeaders(null, 6, 0);

        public void WriteHeaders(Dictionary<string,string> keyValues, int majorVersion = 6, int minorVersion = 0)
        {
            if(keyValues == null)
            {
                keyValues = new Dictionary<string, string>();
            }
            _writer.WriteNetTraceHeaderV6OrGreater(majorVersion, minorVersion);
            _writer.WriteTraceBlockV6OrGreater(keyValues);
        }

        public override void WriteMetadataBlock(params EventMetadata[] metadataBlobs)
        {
            _writer.WriteMetadataBlockV6OrGreater(metadataBlobs);
        }

        public void WriteMetadataBlock(Action<BinaryWriter> writeMetadataBlobs)
        {
            _writer.WriteMetadataBlockV6OrGreater(writeMetadataBlobs);
        }

        public void WriteEventBlock(Action<V6EventBlockWriter> writeEventBlobs) => WriteEventBlock(false, writeEventBlobs);

        public void WriteEventBlock(bool useCompressedHeader, Action<V6EventBlockWriter> writeEventBlobs)
        {
            WriteBlock(2 /* Event */, w =>
            {
                V6EventBlockWriter blockWriter = new V6EventBlockWriter(w, useCompressedHeader);
                blockWriter.WriteHeader();
                writeEventBlobs(blockWriter);
            });
        }

        public void WriteThreadBlock(Action<BinaryWriter> writeThreadEntries)
        {
            _writer.WriteThreadBlock(writeThreadEntries);
        }

        public void WriteRemoveThreadBlock(Action<BinaryWriter> writeThreadEntries)
        {
            _writer.WriteRemoveThreadBlock(writeThreadEntries);
        }

        public void WriteSequencePointBlock(long timestamp, bool resetThreadIndicies, bool resetMetadataIndices, params V6ThreadSequencePoint[] sequencePoints)
        {
            WriteBlock(4 /* BlockKind.SequencePoint */, w =>
            {
                w.Write(timestamp);
                int flags = 0;
                if (resetThreadIndicies)
                {
                    flags |= 1;
                }
                if (resetMetadataIndices)
                {
                    flags |= 2;
                }
                
                w.Write(flags);
                w.Write(sequencePoints.Length);
                foreach (var sequencePoint in sequencePoints)
                {
                    w.WriteVarUInt(sequencePoint.ThreadIndex);
                    w.WriteVarUInt(sequencePoint.SequenceNumber);
                }
            });
        }

        public void WriteLabelListBlock(int firstIndex, int count, Action<V6LabelListBlockWriter> writeLabelListEntries)
        {
            _writer.WriteV6LabelListBlock(firstIndex, count, writeLabelListEntries);
        }

        public void WriteEndBlock() => WriteBlock(0 /* BLockKind.EndOfStream */, w => { });

        public void WriteBlock(byte blockKind, Action<BinaryWriter> writePayload)
        {
            _writer.WriteBlockV6OrGreater(blockKind, writePayload);
        }
    }

    public class WriteEventOptions
    {
        public int MetadataId { get; set; }
        public long ThreadIndexOrId { get; set; }
        public long CaptureThreadIndexOrId { get; set; }
        public int SequenceNumber { get; set; }
        public int ProcNumber { get; set; }
        public int StackId { get; set; }
        public long Timestamp { get; set; }
        public int LabelListId { get; set; }
        public Guid ActivityId { get; set; }
        public Guid RelatedActivityId { get; set; }
        public bool IsSorted { get; set; }

    }

    public static class BinaryWriterExtensions
    {
        public static void WriteNetTraceHeaderV5(this BinaryWriter writer)
        {
            writer.Write(Encoding.UTF8.GetBytes("Nettrace"));
        }

        public static void WriteNetTraceHeaderV6OrGreater(this BinaryWriter writer, int majorVersion, int minorVersion)
        {
            writer.Write(Encoding.UTF8.GetBytes("Nettrace"));
            writer.Write(0); // reserved
            writer.Write(majorVersion);
            writer.Write(minorVersion);
        }

        public static void WriteFastSerializationHeader(this BinaryWriter writer)
        {
            WriteInt32PrefixedUTF8String(writer, "!FastSerialization.1");
        }

        public static void WriteInt32PrefixedUTF8String(this BinaryWriter writer, string val)
        {
            writer.Write(val.Length);
            writer.Write(Encoding.UTF8.GetBytes(val));
        }

        public static void WriteNullTerminatedUTF16String(this BinaryWriter writer, string val)
        {
            writer.Write(Encoding.Unicode.GetBytes(val));
            writer.Write((short)0);
        }

        public static void WriteLengthPrefixedUTF16String(this BinaryWriter writer, string val)
        {
            byte[] utf16Bytes = Encoding.Unicode.GetBytes(val);
            writer.Write((ushort)val.Length);
            writer.Write(utf16Bytes);
        }

        public static void WriteLengthPrefixedUTF8String(this BinaryWriter writer, string val)
        {
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(val);
            writer.Write((ushort)utf8Bytes.Length);
            writer.Write(utf8Bytes);
        }


        public static void WriteVarUInt(this BinaryWriter writer, ulong val)
        {
            while (true)
            {
                byte low7 = (byte)(val & 0x7F);
                val >>= 7;
                if (val == 0)
                {
                    writer.Write(low7);
                    break;
                }
                else
                {
                    writer.Write((byte)(low7 | 0x80));
                }
            }
        }

        public static void WriteVarInt(this BinaryWriter writer, long val)
        {
            if(val < 0)
            {
                writer.WriteVarUInt((ulong)(~val << 1) | 0x1);
            }
            else
            {
                writer.WriteVarUInt((ulong)(val << 1));
            }
        }

        public static void WriteVarUIntPrefixedUTF8String(this BinaryWriter writer, string val)
        {
            byte[] utf8Bytes = Encoding.UTF8.GetBytes(val);
            WriteVarUInt(writer, (ulong)utf8Bytes.Length);
            writer.Write(utf8Bytes);
        }

        public static void Write(this BinaryWriter writer, Guid val)
        {
            writer.Write(val.ToByteArray());
        }

        // used in versions <= 5
        public static void WriteObject(this BinaryWriter writer, string name, int version, int minVersion,
    Action writePayload)
        {
            writer.Write((byte)5); // begin private object
            writer.Write((byte)5); // begin private object - type
            writer.Write((byte)1); // type of type
            writer.Write(version);
            writer.Write(minVersion);
            WriteInt32PrefixedUTF8String(writer, name);
            writer.Write((byte)6); // end object
            writePayload();
            writer.Write((byte)6); // end object
        }

        public static void WriteBlockV6OrGreater(this BinaryWriter writer, byte blockKind, Action<BinaryWriter> writePayload)
        {
            long blockHeaderPos = writer.BaseStream.Position;
            writer.Write((uint)0);
            writePayload(writer);
            long endBlockPos = writer.BaseStream.Position;

            // backup and fill in the block header now that the length is known
            writer.Seek((int)blockHeaderPos, SeekOrigin.Begin);
            uint size = (uint)(endBlockPos - blockHeaderPos - 4);
            uint header = size | ((uint)blockKind << 24);
            writer.Write(header);
            writer.Seek((int)endBlockPos, SeekOrigin.Begin);
        }

        public static void WriteTraceObjectV5(this BinaryWriter writer)
        {
            WriteObject(writer, "Trace", 4, 4, () =>
            {
                DateTime now = DateTime.Now;
                writer.Write((short)now.Year);
                writer.Write((short)now.Month);
                writer.Write((short)now.DayOfWeek);
                writer.Write((short)now.Day);
                writer.Write((short)now.Hour);
                writer.Write((short)now.Minute);
                writer.Write((short)now.Second);
                writer.Write((short)now.Millisecond);
                writer.Write((long)1_000_000); // syncTimeQPC
                writer.Write((long)1000); // qpcFreq
                writer.Write(8); // pointer size
                writer.Write(1); // pid
                writer.Write(4); // num procs
                writer.Write(1000); // sampling rate
            });
        }

        public static void WriteTraceBlockV6OrGreater(this BinaryWriter writer, Dictionary<string,string> keyValues)
        {
            WriteBlockV6OrGreater(writer, 1 /* BlockKind.Trace */, w =>
            {
                DateTime now = new DateTime(2025, 2, 3, 4, 5, 6);
                w.Write((short)now.Year);
                w.Write((short)now.Month);
                w.Write((short)now.DayOfWeek);
                w.Write((short)now.Day);
                w.Write((short)now.Hour);
                w.Write((short)now.Minute);
                w.Write((short)now.Second);
                w.Write((short)now.Millisecond);
                w.Write((long)0);                 // syncTimeQPC
                w.Write((long)1000);              // qpcFreq
                w.Write(8);                       // pointer size
                w.Write(keyValues.Count);
                foreach(var kv in keyValues)
                {
                    w.WriteVarUIntPrefixedUTF8String(kv.Key);
                    w.WriteVarUIntPrefixedUTF8String(kv.Value);
                }
            });
        }

        private static void Align(BinaryWriter writer, long previousBytesWritten)
        {
            int offset = (int)((writer.BaseStream.Position + previousBytesWritten) % 4);
            if (offset != 0)
            {
                for (int i = offset; i < 4; i++)
                {
                    writer.Write((byte)0);
                }
            }
        }

        public static void WriteBlockV5OrLess(this BinaryWriter writer, string name, Action<BinaryWriter> writeBlockData,
            long previousBytesWritten = 0)
        {
            Debug.WriteLine($"Starting block {name} position: {writer.BaseStream.Position + previousBytesWritten}");
            MemoryStream block = new MemoryStream();
            BinaryWriter blockWriter = new BinaryWriter(block);
            writeBlockData(blockWriter);
            WriteObject(writer, name, 2, 0, () =>
            {
                writer.Write((int)block.Length);
                Align(writer, previousBytesWritten);
                writer.Write(block.GetBuffer(), 0, (int)block.Length);
            });
        }

        public static void WriteMetadataBlockV6OrGreater(this BinaryWriter writer, params EventMetadata[] metadataBlobs)
        {
            WriteMetadataBlockV6OrGreater(writer, w =>
            {
                foreach (EventMetadata metadata in metadataBlobs)
                {
                    w.WriteMetadataBlobV6OrGreater(metadata);
                }
            });
        }

        public static void WriteMetadataBlockV6OrGreater(this BinaryWriter writer, Action<BinaryWriter> writeMetadataBlobs)
        {
            WriteBlockV6OrGreater(writer, 3 /* Metadata */, w =>
            {
                w.Write((UInt16)0);    // header size
                writeMetadataBlobs(w);
            });
        }

        public static void WriteMetadataBlobV6OrGreater(this BinaryWriter writer, EventMetadata metadata)
        {
            writer.WriteMetadataBlobV6OrGreater(w =>
            {
                w.WriteV6InitialMetadataBlob(metadata.MetadataId, metadata.ProviderName, metadata.EventName, metadata.EventId);
                w.WriteV6MetadataParameterList(metadata.Parameters);
                w.WriteV6OptionalMetadataList(metadata);
            });
        }

        public static void WriteMetadataBlobV6OrGreater(this BinaryWriter writer, Action<BinaryWriter> writeMetadataPayload)
        {
            MemoryStream payloadBlob = new MemoryStream();
            BinaryWriter payloadWriter = new BinaryWriter(payloadBlob);
            writeMetadataPayload(payloadWriter);
            writer.Write((UInt16)payloadBlob.Length);
            writer.Write(payloadBlob.GetBuffer(), 0, (int)payloadBlob.Length);
        }

        public static void WriteV6InitialMetadataBlob(this BinaryWriter writer, int metadataId, string providerName, string eventName, int eventId)
        {
            writer.WriteVarUInt((uint)metadataId);                // metadata id
            writer.WriteVarUIntPrefixedUTF8String(providerName);  // provider name
            writer.WriteVarUInt((uint)eventId);                   // event id
            writer.WriteVarUIntPrefixedUTF8String(eventName);     // event name
        }

        public static void WriteV6MetadataParameterList(this BinaryWriter writer, params MetadataParameter[] parameters)
        {
            writer.WriteV6MetadataParameterList(parameters.Length, w =>
            {
                foreach (var parameter in parameters)
                {
                    w.WriteV6MetadataParameter(parameter);
                }
            });
        }

        public static void WriteV6MetadataParameterList(this BinaryWriter writer, int parameterCount, Action<BinaryWriter> writeParameters)
        {
            writer.Write((UInt16)parameterCount);
            writeParameters(writer);
        }

        public static void WriteV6MetadataParameter(this BinaryWriter writer, MetadataParameter parameter)
        {
            writer.WriteV6MetadataParameter(parameter.Name, w => { w.WriteV6MetadataType(parameter.Type); });
        }

        public static void WriteV6MetadataParameter(this BinaryWriter writer, string parameterName, Action<BinaryWriter> writeType)
        {
            MemoryStream paramStream = new MemoryStream();
            BinaryWriter paramWriter = new BinaryWriter(paramStream);
            paramWriter.WriteVarUIntPrefixedUTF8String(parameterName);
            writeType(paramWriter);

            writer.Write((UInt16)paramStream.Length);
            writer.Write(paramStream.GetBuffer(), 0, (int)paramStream.Length);
        }

        public static void WriteV6MetadataType(this BinaryWriter writer, MetadataType type)
        {
            writer.Write((byte)type.TypeCode);
            if(type.TypeCode == MetadataTypeCode.Array)
            {
                writer.WriteV6MetadataType((type as ArrayMetadataType).ElementType);
            }
            else if (type.TypeCode == MetadataTypeCode.FixedLengthArray)
            {
                writer.WriteV6MetadataType((type as FixedLengthArrayMetadataType).ElementType);
                writer.Write((ushort)(type as FixedLengthArrayMetadataType).ElementCount);
            }
            else if(type.TypeCode == MetadataTypeCode.RelLoc)
            {
                writer.WriteV6MetadataType((type as RelLocMetadataType).ElementType);
            } 
            else if(type.TypeCode == MetadataTypeCode.DataLoc)
            {
                writer.WriteV6MetadataType((type as DataLocMetadataType).ElementType);
            }
            else if(type.TypeCode == MetadataTypeCode.Object)
            {
                writer.WriteV6MetadataParameterList((type as ObjectMetadataType).Parameters);
            }
        }

        public static void WriteV6OptionalMetadataList(this BinaryWriter writer, EventMetadata metadata)
        {
            writer.WriteV6OptionalMetadataList(w =>
            {
                if (metadata.OpCode != 0)
                {
                    w.WriteV6OptionalMetadataOpcode(metadata.OpCode);
                }
                if (metadata.Keywords != 0)
                {
                    w.WriteV6OptionalMetadataKeyword(metadata.Keywords);
                }
                if (metadata.MessageTemplate != null)
                {
                    w.WriteV6OptionalMetadataMessageTemplate(metadata.MessageTemplate);
                }
                if (metadata.Description != null)
                {
                    w.WriteV6OptionalMetadataDescription(metadata.Description);
                }
                foreach (var kv in metadata.Attributes)
                {
                    w.WriteV6OptionalMetadataAttribute(kv.Key, kv.Value);
                }
                if (metadata.ProviderId != default)
                {
                    w.WriteV6OptionalMetadataProviderGuid(metadata.ProviderId);
                }
                if (metadata.Level != 0)
                {
                    w.WriteV6OptionalMetadataLevel(metadata.Level);
                }
                if (metadata.Version != 0)
                {
                    w.WriteV6OptionalMetadataVersion(metadata.Version);
                }
            });
        }

        public static void WriteV6OptionalMetadataList(this BinaryWriter writer, Action<BinaryWriter> writeOptionalMetadata)
        {
            MemoryStream optionalMetadata = new MemoryStream();
            BinaryWriter optionalMetadataWriter = new BinaryWriter(optionalMetadata);
            writeOptionalMetadata(optionalMetadataWriter);

            writer.Write((ushort)optionalMetadata.Length);
            writer.Write(optionalMetadata.GetBuffer(), 0, (int)optionalMetadata.Length);
        }

        public static void WriteV6OptionalMetadataOpcode(this BinaryWriter writer, byte opcode)
        {
            writer.Write((byte)1);       // OptionalMetadataKind.Opcode
            writer.Write((byte)opcode);
        }

        public static void WriteV6OptionalMetadataKeyword(this BinaryWriter writer, long keyword)
        {
            writer.Write((byte)3);       // OptionalMetadataKind.Keyword
            writer.Write(keyword);
        }

        public static void WriteV6OptionalMetadataMessageTemplate(this BinaryWriter writer, string template)
        {
            writer.Write((byte)4);       // OptionalMetadataKind.MessageTemplate
            writer.WriteVarUIntPrefixedUTF8String(template);
        }

        public static void WriteV6OptionalMetadataDescription(this BinaryWriter writer, string description)
        {
            writer.Write((byte)5);       // OptionalMetadataKind.Description
            writer.WriteVarUIntPrefixedUTF8String(description);
        }

        public static void WriteV6OptionalMetadataAttribute(this BinaryWriter writer, string key, string value)
        {
            writer.Write((byte)6);       // OptionalMetadataKind.KeyValuePair
            writer.WriteVarUIntPrefixedUTF8String(key);
            writer.WriteVarUIntPrefixedUTF8String(value);
        }

        public static void WriteV6OptionalMetadataProviderGuid(this BinaryWriter writer, Guid providerId)
        {
            writer.Write((byte)7);       // OptionalMetadataKind.ProviderGuid
            writer.Write(providerId);
        }

        public static void WriteV6OptionalMetadataLevel(this BinaryWriter writer, byte level)
        {
            writer.Write((byte)8);       // OptionalMetadataKind.Level
            writer.Write(level);
        }

        public static void WriteV6OptionalMetadataVersion(this BinaryWriter writer, byte version)
        {
            writer.Write((byte)9);       // OptionalMetadataKind.Version
            writer.Write(version);
        }

        public static void WriteMetadataBlockV5OrLess(this BinaryWriter writer, Action<BinaryWriter> writeMetadataEventBlobs, long previousBytesWritten = 0)
        {
            WriteBlockV5OrLess(writer, "MetadataBlock", w =>
            {
                // header
                w.Write((short)20); // header size
                w.Write((short)0); // flags
                w.Write((long)0);  // min timestamp
                w.Write((long)0);  // max timestamp
                writeMetadataEventBlobs(w);
            },
            previousBytesWritten);
        }

        public static void WriteMetadataBlockV5OrLess(this BinaryWriter writer, EventMetadata[] metadataBlobs, long previousBytesWritten = 0)
        {
            WriteMetadataBlockV5OrLess(writer,
                w =>
                {
                    foreach (EventMetadata blob in metadataBlobs)
                    {
                        WriteMetadataEventBlobV5OrLess(w, blob);
                    }
                },
                previousBytesWritten);
        }

        public static void WriteMetadataBlockV5OrLess(this BinaryWriter writer, params EventMetadata[] metadataBlobs)
        {
            WriteMetadataBlockV5OrLess(writer, metadataBlobs, 0);
        }

        public static void WriteMetadataEventBlobV5OrLess(this BinaryWriter writer, EventMetadata eventMetadataBlob)
        {
            writer.WriteMetadataEventBlobV5OrLess(w =>
            {
                w.WriteV5InitialMetadataBlob(eventMetadataBlob.MetadataId, eventMetadataBlob.ProviderName, eventMetadataBlob.EventName, eventMetadataBlob.EventId);
                w.WriteV5MetadataParameterList();
                if(eventMetadataBlob.OpCode != 0)
                {
                    w.WriteV5OpcodeMetadataTag(eventMetadataBlob.OpCode);
                }
            });
        }

        public static void WriteMetadataEventBlobV5OrLess(this BinaryWriter writer, Action<BinaryWriter> writeMetadataEventPayload)
        {
            writer.WriteEventBlobV4Or5(metadataId: 0, threadIndex:0, sequenceNumber: 0, w =>
            {
                writeMetadataEventPayload(w);
            });
        }

        public static void WriteV5InitialMetadataBlob(this BinaryWriter writer, int metadataId, string providerName, string eventName, int eventId)
        {
            writer.Write(metadataId);                             // metadata id
            writer.WriteNullTerminatedUTF16String(providerName);  // provider name
            writer.Write(eventId);                                // event id
            writer.WriteNullTerminatedUTF16String(eventName);     // event name
            writer.Write((long)0);                                // keywords
            writer.Write(1);                                      // version
            writer.Write(5);                                      // level
        }

        public static void WriteV5MetadataParameterList(this BinaryWriter writer)
        {
            writer.Write(0); // fieldcount
        }

        public static void WriteV5MetadataParameterList(this BinaryWriter writer, int fieldCount, Action<BinaryWriter> writeParameters)
        {
            writer.Write(fieldCount);
            writeParameters(writer);
        }

        /// <summary>
        /// The V2 here refers to fieldLayout V2, which is used in the V2Params tag area of the V5 format
        /// </summary>
        public static void WriteFieldLayoutV2MetadataParameter(this BinaryWriter writer, string parameterName, Action<BinaryWriter> writeType)
        {
            MemoryStream parameterBlob = new MemoryStream();
            BinaryWriter parameterWriter = new BinaryWriter(parameterBlob);
            parameterWriter.WriteNullTerminatedUTF16String(parameterName);
            writeType(parameterWriter);
            int payloadSize = (int)parameterBlob.Length;                   

            writer.Write((int)(payloadSize + 4));                              // parameter size includes the leading size field
            writer.Write(parameterBlob.GetBuffer(), 0, payloadSize);
        }

        public static void WriteV5MetadataTagBytes(this BinaryWriter writer, byte tag, Action<BinaryWriter> writeTagPayload)
        {
            MemoryStream payloadBlob = new MemoryStream();
            BinaryWriter payloadWriter = new BinaryWriter(payloadBlob);
            writeTagPayload(payloadWriter);
            int payloadSize = (int)payloadBlob.Length;

            writer.Write((int)payloadSize);
            writer.Write((byte)tag);
            writer.Write(payloadBlob.GetBuffer(), 0, payloadSize);
        }

        public static void WriteV5OpcodeMetadataTag(this BinaryWriter writer, byte opcode)
        {
            WriteV5MetadataTagBytes(writer, 1 /* OpcodeTag */, w =>
            {
                w.Write((byte)opcode);
            });
        }

        public static void WriteV5MetadataV2ParamTag(this BinaryWriter writer, int fieldCount, Action<BinaryWriter> writeFields)
        {
            WriteV5MetadataTagBytes(writer, 2 /* V2ParamTag */, w =>
            {
                w.WriteV5MetadataParameterList(fieldCount, writeFields);
            });
        }



        public static void WriteV6LabelListBlock(this BinaryWriter writer, int firstIndex, int count, Action<V6LabelListBlockWriter> writeLabelLists)
        {
            WriteBlockV6OrGreater(writer, 8 /* BlockKind.LabelList */, w =>
            {
                w.Write(firstIndex);
                w.Write(count);
                V6LabelListBlockWriter labelListWriter = new V6LabelListBlockWriter(w);
                writeLabelLists(labelListWriter);
            });
        }

        public static void WriteEventBlockV5OrLess(this BinaryWriter writer, Action<BinaryWriter> writeEventBlobs, long previousBytesWritten = 0)
        {
            WriteBlockV5OrLess(writer, "EventBlock", w =>
            {
                // header
                w.Write((short)20); // header size
                w.Write((short)0);  // flags
                w.Write((long)0);   // min timestamp
                w.Write((long)0);   // max timestamp
                writeEventBlobs(w);
            },
            previousBytesWritten);
        }


        public static void WriteEventBlobV4Or5(this BinaryWriter writer, int metadataId, long threadIndex, int sequenceNumber, byte[] payloadBytes)
        {
            WriteEventBlobV4Or5(writer, metadataId, threadIndex, sequenceNumber, w => w.Write(payloadBytes));
        }

        public static void WriteEventBlobV4Or5(this BinaryWriter writer, int metadataId, long threadIndex, int sequenceNumber, Action<BinaryWriter> writeEventPayload)
        {
            writer.WriteEventBlobV4Or5(new WriteEventOptions { MetadataId = metadataId, CaptureThreadIndexOrId = threadIndex, ThreadIndexOrId = threadIndex, SequenceNumber = sequenceNumber }, writeEventPayload);
        }

        public static void WriteEventBlobV4Or5(this BinaryWriter writer, WriteEventOptions options, Action<BinaryWriter> writeEventPayload)
        {
            MemoryStream payloadBlob = new MemoryStream();
            BinaryWriter payloadWriter = new BinaryWriter(payloadBlob);
            writeEventPayload(payloadWriter);
            int payloadSize = (int)payloadBlob.Length;

            MemoryStream eventBlob = new MemoryStream();
            BinaryWriter eventWriter = new BinaryWriter(eventBlob);
            eventWriter.Write(options.MetadataId | (int)(options.IsSorted ? 0 : 0x80000000));
            eventWriter.Write(options.SequenceNumber);
            eventWriter.Write(options.ThreadIndexOrId);
            eventWriter.Write(options.CaptureThreadIndexOrId);
            eventWriter.Write(options.ProcNumber);
            eventWriter.Write(options.StackId);
            eventWriter.Write(options.Timestamp);
            eventWriter.Write(options.ActivityId.ToByteArray());
            eventWriter.Write(options.RelatedActivityId.ToByteArray());
            eventWriter.Write(payloadSize);

            writer.Write((int)eventBlob.Length + payloadSize);
            writer.Write(eventBlob.GetBuffer(), 0, (int)eventBlob.Length);
            writer.Write(payloadBlob.GetBuffer(), 0, payloadSize);
        }

        public static void WriteThreadBlock(this BinaryWriter writer, Action<BinaryWriter> writeThreadEntries)
        {
            writer.WriteBlockV6OrGreater(6 /* Thread */, writeThreadEntries);
        }

        public static void WriteThreadEntry(this BinaryWriter writer, long threadIndex, int threadId, int processId)
        {
            writer.WriteThreadEntry(threadIndex, w =>
            {
                w.WriteThreadEntryThreadId(threadId);
                w.WriteThreadEntryProcessId(processId);
            });
        }

        public static void WriteThreadEntry(this BinaryWriter writer, long threadIndex, Action<BinaryWriter> writeThreadOptionalData)
        {
            MemoryStream threadEntry = new MemoryStream();
            BinaryWriter threadWriter = new BinaryWriter(threadEntry);
            threadWriter.WriteVarUInt((ulong)threadIndex);
            writeThreadOptionalData(threadWriter);

            writer.Write((ushort)threadEntry.Length);
            writer.Write(threadEntry.GetBuffer(), 0, (int)threadEntry.Length);
        }

        public static void WriteThreadEntryName(this BinaryWriter writer, string name)
        {
            writer.Write((byte)1 /* Name */);
            writer.WriteVarUIntPrefixedUTF8String(name);
        }

        public static void WriteThreadEntryProcessId(this BinaryWriter writer, long processId)
        {
            writer.Write((byte)2 /* OSProcessId */);
            writer.WriteVarUInt((ulong)processId);
        }

        public static void WriteThreadEntryThreadId(this BinaryWriter writer, long threadId)
        {
            writer.Write((byte)3 /* ThreadId */);
            writer.WriteVarUInt((ulong)threadId);
        }

        public static void WriteThreadEntryKeyValue(this BinaryWriter writer, string key, string value)
        {
            writer.Write((byte)4 /* KeyValue */);
            writer.WriteVarUIntPrefixedUTF8String(key);
            writer.WriteVarUIntPrefixedUTF8String(value);
        }

        public static void WriteRemoveThreadBlock(this BinaryWriter writer, Action<BinaryWriter> writeThreadEntries)
        {
            writer.WriteBlockV6OrGreater(7 /* RemoveThread */, writeThreadEntries);
        }

        

        public static void WriteRemoveThreadEntry(this BinaryWriter writer, long threadIndex, int sequenceNumber)
        {
            writer.WriteVarUInt((ulong)threadIndex);
            writer.WriteVarUInt((uint)sequenceNumber);
        }

        public static void WriteEndObject(this BinaryWriter writer)
        {
            writer.Write(1); // null tag
        }
    }

    public class V6LabelListBlockWriter
    {
        BinaryWriter _writer;

        public V6LabelListBlockWriter(BinaryWriter writer)
        {
            _writer = writer;
        }

        public void WriteActivityIdLabel(Guid activityId, bool isLastLabel = false)
        {
            byte kind = 1; // ActivityId
            if (isLastLabel)
            {
                kind |= 0x80;
            }
            _writer.Write(kind);
            _writer.Write(activityId);
        }

        public void WriteRelatedActivityIdLabel(Guid relatedActivityId, bool isLastLabel = false)
        {
            byte kind = 2; // RelatedActivityId
            if (isLastLabel)
            {
                kind |= 0x80;
            }
            _writer.Write(kind);
            _writer.Write(relatedActivityId);
        }

        public void WriteTraceIdLabel(byte[] traceId, bool isLastLabel = false)
        {
            Debug.Assert(traceId.Length == 16);
            byte kind = 3; // TraceId
            if (isLastLabel)
            {
                kind |= 0x80;
            }
            _writer.Write(kind);
            _writer.Write(traceId);
        }

        public void WriteSpanIdLabel(ulong spanId, bool isLastLabel = false)
        {
            byte kind = 4; // SpanId
            if (isLastLabel)
            {
                kind |= 0x80;
            }
            _writer.Write(kind);
            _writer.Write(spanId);
        }

        public void WriteNameValueStringLabel(string name, string value, bool isLastLabel = false)
        {
            byte kind = 5; // NameValueString
            if (isLastLabel)
            {
                kind |= 0x80;
            }
            _writer.Write(kind);
            _writer.WriteVarUIntPrefixedUTF8String(name);
            _writer.WriteVarUIntPrefixedUTF8String(value);
        }

        public void WriteNameValueVarIntLabel(string name, long value, bool isLastLabel = false)
        {
            byte kind = 6; // NameValueVarint
            if (isLastLabel)
            {
                kind |= 0x80;
            }
            _writer.Write(kind);
            _writer.WriteVarUIntPrefixedUTF8String(name);
            _writer.WriteVarInt(value);
        }

        public void WriteOpCodeLabel(byte opcode, bool isLastLabel = false)
        {
            byte kind = 7; // OpCode
            if (isLastLabel)
            {
                kind |= 0x80;
            }
            _writer.Write(kind);
            _writer.Write(opcode);
        }

        public void WriteKeywordsLabel(ulong keywords, bool isLastLabel = false)
        {
            byte kind = 8; // Keyword
            if (isLastLabel)
            {
                kind |= 0x80;
            }
            _writer.Write(kind);
            _writer.Write(keywords);
        }

        public void WriteLevelLabel(byte level, bool isLastLabel = false)
        {
            byte kind = 9; // Level
            if (isLastLabel)
            {
                kind |= 0x80;
            }
            _writer.Write(kind);
            _writer.Write(level);
        }

        public void WriteVersionLabel(byte version, bool isLastLabel = false)
        {
            byte kind = 10; // Version
            if (isLastLabel)
            {
                kind |= 0x80;
            }
            _writer.Write(kind);
            _writer.Write(version);
        }
    }

    class V6EventBlockWriter
    {
        BinaryWriter _writer;
        bool _useCompressedHeaders;
        WriteEventOptions _lastEventOptions = new WriteEventOptions();
        int _lastPayloadLength;

        public V6EventBlockWriter(BinaryWriter writer, bool useCompressedHeaders)
        {
            _writer = writer;
            _useCompressedHeaders = useCompressedHeaders;
        }

        public void WriteHeader(long minTimestamp = 0, long maxTimestamp = 0)
        {
            _writer.Write((short)20);                               // header size
            _writer.Write((short)(_useCompressedHeaders ? 1 : 0));  // flags
            _writer.Write(minTimestamp);
            _writer.Write(maxTimestamp);
        }

        public void WriteEventBlob(int metadataId, long threadIndex, int sequenceNumber, byte[] payloadBytes)
        {
            WriteEventBlob(metadataId, threadIndex, sequenceNumber, w => w.Write(payloadBytes));
        }

        public void WriteEventBlob(int metadataId, long threadIndex, int sequenceNumber, Action<BinaryWriter> writeEventPayload)
        {
            WriteEventBlob(new WriteEventOptions { MetadataId = metadataId, CaptureThreadIndexOrId = threadIndex, ThreadIndexOrId = threadIndex, SequenceNumber = sequenceNumber }, writeEventPayload);
        }

        public void WriteEventBlob(WriteEventOptions options, Action<BinaryWriter> writeEventPayload)
        {
            MemoryStream payloadBlob = new MemoryStream();
            BinaryWriter payloadWriter = new BinaryWriter(payloadBlob);
            writeEventPayload(payloadWriter);
            int payloadSize = (int)payloadBlob.Length;
            WriteEventHeader(options, payloadSize);
            _writer.Write(payloadBlob.GetBuffer(), 0, payloadSize);
        }

        public void WriteEventHeader(WriteEventOptions options, int payloadLength)
        {
            if(_useCompressedHeaders)
            {
                WriteCompressedEventHeader(options, payloadLength);
            }
            else
            {
                WriteUncompressedEventHeader(options, payloadLength);
            }
        }

        public void WriteUncompressedEventHeader(WriteEventOptions options, int payloadLength)
        {
            _writer.Write(48 /* header size not including this field */ + payloadLength);
            _writer.Write(options.MetadataId | (int)(options.IsSorted ? 0 : 0x80000000));
            _writer.Write(options.SequenceNumber);
            _writer.Write(options.ThreadIndexOrId);
            _writer.Write(options.CaptureThreadIndexOrId);
            _writer.Write(options.ProcNumber);
            _writer.Write(options.StackId);
            _writer.Write(options.Timestamp);
            _writer.Write(options.LabelListId);
            _writer.Write(payloadLength);
        }

        public void WriteCompressedEventHeader(WriteEventOptions options, int payloadLength)
        {
            byte header = 0;
            if (options.MetadataId != _lastEventOptions.MetadataId)
            {
                header |= 0x01;
            }
            if (options.CaptureThreadIndexOrId != _lastEventOptions.CaptureThreadIndexOrId ||
                options.SequenceNumber != _lastEventOptions.SequenceNumber + 1 ||
                options.ProcNumber != _lastEventOptions.ProcNumber)
            {
                header |= 0x02;
            }
            if (options.ThreadIndexOrId != _lastEventOptions.ThreadIndexOrId)
            {
                header |= 0x04;
            }
            if (options.StackId != _lastEventOptions.StackId)
            {
                header |= 0x08;
            }
            if (options.LabelListId != _lastEventOptions.LabelListId)
            {
                header |= 0x10;
            }
            if (options.IsSorted)
            {
                header |= 0x40;
            }
            if( (payloadLength != _lastPayloadLength))
            {
                header |= 0x80;
            }
            _writer.Write(header);
            if ((header & 0x01) != 0)
            {
                _writer.WriteVarUInt((ulong)options.MetadataId);
            }
            if ((header & 0x02) != 0)
            {
                // the cast to uint here is deliberate to force underflow to wrap up to 2^32 rather than 2^64
                _writer.WriteVarUInt((uint)(options.SequenceNumber - _lastEventOptions.SequenceNumber - 1));
                _writer.WriteVarUInt((ulong)options.CaptureThreadIndexOrId);
                _writer.WriteVarUInt((ulong)options.ProcNumber);
            }
            if ((header & 0x04) != 0)
            {
                _writer.WriteVarUInt((ulong)options.ThreadIndexOrId);
            }
            if ((header & 0x08) != 0)
            {
                _writer.WriteVarUInt((ulong)options.StackId);
            }
            _writer.WriteVarUInt((ulong)(options.Timestamp - _lastEventOptions.Timestamp));

            if ((header & 0x10) != 0)
            {
                _writer.WriteVarUInt((ulong)options.LabelListId);
            }
            if ((header & 0x80) != 0)
            {
                _writer.WriteVarUInt((ulong)payloadLength);
            }
            _lastEventOptions = options;
            _lastPayloadLength = payloadLength;
        }
    }

    class MockHugeStream : Stream
    {
        // the events are big to make the stream grow fast
        const int payloadSize = 60000;

        MemoryStream _currentChunk = new MemoryStream();
        long _minSize;
        long _bytesWritten;
        int _sequenceNumber = 1;

        public MockHugeStream(long minSize)
        {
            _minSize = minSize;
            _currentChunk = GetFirstChunk();
            _bytesWritten = _currentChunk.Length;
        }

        MemoryStream GetFirstChunk()
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(ms);
            writer.WriteNetTraceHeaderV5();
            writer.WriteFastSerializationHeader();
            writer.WriteTraceObjectV5();
            writer.WriteMetadataBlockV5OrLess(new EventMetadata(1, "Provider", "Event", 1));
            ms.Position = 0;
            return ms;
        }



        MemoryStream GetNextChunk()
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(ms);
            if (_bytesWritten > _minSize)
            {
                writer.WriteEndObject();
            }
            else
            {
                // 20 blocks, each with 20 events in them
                for (int i = 0; i < 20; i++)
                {
                    writer.WriteEventBlockV5OrLess(
                        w =>
                        {
                            for (int j = 0; j < 20; j++)
                            {
                                w.WriteEventBlobV4Or5(1, 999, _sequenceNumber++, WriteEventPayload);
                            }
                        },
                        _bytesWritten);
                }
            }
            ms.Position = 0;
            return ms;
        }

        static void WriteEventPayload(BinaryWriter writer)
        {
            for (int i = 0; i < payloadSize / 8; i++)
            {
                writer.Write((long)i);
            }
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotImplementedException();
        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override void Flush()
        {
            throw new NotImplementedException();
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            int ret = _currentChunk.Read(buffer, offset, count);
            if (ret == 0)
            {
                _currentChunk = GetNextChunk();
                _bytesWritten += _currentChunk.Length;
                ret = _currentChunk.Read(buffer, offset, count);
            }
            return ret;
        }
        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }
        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
    }
}
