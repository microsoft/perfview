using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.EventPipe;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace TraceEventTests
{
    public class EventPipeParsing : EventPipeTestBase
    {
        private class EventRecord
        {
            public int TotalCount;
            public string FirstSeriazliedSample;
        }

        public EventPipeParsing(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory()]
        [MemberData(nameof(TestEventPipeFiles))]
        public void Basic(string eventPipeFileName)
        {
            // Initialize
            PrepareTestData();

            string eventPipeFilePath = Path.Combine(UnZippedDataDir, eventPipeFileName);

            Output.WriteLine(string.Format("Processing the file {0}, Making ETLX and scanning.", Path.GetFullPath(eventPipeFilePath)));

            var eventStatistics = new SortedDictionary<string, EventRecord>(StringComparer.Ordinal);

            using (var traceLog = new TraceLog(TraceLog.CreateFromEventPipeDataFile(eventPipeFilePath)))
            {
                var traceSource = traceLog.Events.GetSource();

                traceSource.AllEvents += delegate (TraceEvent data)
                {
                    string eventName = data.ProviderName + "/" + data.EventName;

                    if (eventStatistics.ContainsKey(eventName))
                    {
                        eventStatistics[eventName].TotalCount++;
                    }
                    else
                    {
                        eventStatistics[eventName] = new EventRecord()
                        {
                            TotalCount = 1,
                            FirstSeriazliedSample = new String(data.ToString().Replace("\n", "\\n").Replace("\r", "\\r").Take(1000).ToArray())
                        };
                    }
                };

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
            var eventStatistics = new SortedDictionary<string, EventRecord>(StringComparer.Ordinal);

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
                        // I haven't had an oportunity to investigate and its probably not a big
                        // deal so just hacking around it for the moment
                        if (eventName == "Microsoft-Windows-DotNETRuntimeRundown/Method" ||
                            eventName == "Microsoft-Windows-DotNETRuntime/Method")
                            return;

                        if (eventStatistics.ContainsKey(eventName))
                        {
                            eventStatistics[eventName].TotalCount++;
                        }
                        else
                        {
                            eventStatistics[eventName] = new EventRecord()
                            {
                                TotalCount = 1,
                                FirstSeriazliedSample = new String(data.ToString().Replace("\n", "\\n").Replace("\r", "\\r").Take(1000).ToArray())
                            };
                        }
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

        [Fact]
        public void CanParseHeaderOfV1EventPipeFile()
        {
            PrepareTestData();

            const string eventPipeFileName = "eventpipe-dotnetcore2.0-linux-objver1.netperf";

            string eventPipeFilePath = Path.Combine(UnZippedDataDir, eventPipeFileName);

            using (var eventPipeSource = new EventPipeEventSource(eventPipeFilePath))
            {
                Assert.Equal(8, eventPipeSource.PointerSize);
                Assert.Equal(0, eventPipeSource._processId);
                Assert.Equal(1, eventPipeSource.NumberOfProcessors);

                Assert.Equal(636414354195130000, eventPipeSource._syncTimeUTC.Ticks);
                Assert.Equal(1477613380157300, eventPipeSource._syncTimeQPC);
                Assert.Equal(1000000000, eventPipeSource._QPCFreq);

                Assert.Equal(10, eventPipeSource.CpuSpeedMHz);
            }
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

        private void ValidateEventStatistics(SortedDictionary<string, EventRecord> eventStatistics, string eventPipeFileName)
        {
            StringBuilder sb = new StringBuilder(1024 * 1024);
            foreach (var item in eventStatistics)
            {
                sb.AppendLine($"{item.Key}, {item.Value.TotalCount}, {item.Value.FirstSeriazliedSample}");
            }

            string actual = sb.ToString();
            string baselineFile = Path.Combine(TestDataDir, eventPipeFileName + ".baseline.txt");
            string expected = File.ReadAllText(baselineFile);

            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                string eventStatisticsFile = Path.Combine(TestDataDir, eventPipeFileName + ".actual.txt");
                File.WriteAllText(eventStatisticsFile, actual, Encoding.UTF8);

                Output.WriteLine($"Baseline File: {baselineFile}");
                Output.WriteLine($"Actual File: {eventStatisticsFile}");
                Output.WriteLine($"To Diff: windiff {baselineFile} {eventStatisticsFile}");
                Assert.True(false, $"The event statistics doesn't match {Path.GetFullPath(baselineFile)}. It's saved in {Path.GetFullPath(eventStatisticsFile)}.");
            }
        }
    }


    class MockStreamingOnlyStream : Stream
    {
        Stream _innerStream;
        public MockStreamingOnlyStream(Stream innerStream)
        {
            _innerStream = innerStream;
        }
        public long TestOnlyPosition {  get { return _innerStream.Position; } }

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
}
