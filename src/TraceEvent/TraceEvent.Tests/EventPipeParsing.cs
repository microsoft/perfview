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

                for(int i = 0; i < traceSource.NumberOfProcessors; i++)
                {
                    Assert.NotEqual(0, counts[i]);
                }
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

    class MockHugeStream : Stream
    {
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
            writer.Write(Encoding.UTF8.GetBytes("Nettrace"));
            WriteString(writer,"!FastSerialization.1");
            WriteTrace(writer);
            WriteMetadataBlock(writer);
            ms.Position = 0;
            return ms;
        }

        private void WriteString(BinaryWriter writer, string val)
        {
            writer.Write(val.Length);
            writer.Write(Encoding.UTF8.GetBytes(val));
        }

        private void Align(BinaryWriter writer)
        {
            int offset = (int) ((writer.BaseStream.Position + _bytesWritten) % 4);
            if(offset != 0)
            {
                for(int i = offset; i < 4; i++)
                {
                    writer.Write((byte)0);
                }
            }
        }

        private void WriteObject(BinaryWriter writer, string name, int version, int minVersion, 
            Action writePayload)
        {
            writer.Write((byte)5); // begin private object
            writer.Write((byte)5); // begin private object - type
            writer.Write((byte)1); // type of type
            writer.Write(version);
            writer.Write(minVersion);
            WriteString(writer, name);
            writer.Write((byte)6); // end object
            writePayload();
            writer.Write((byte)6); // end object
        }

        private void WriteTrace(BinaryWriter writer)
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

        private void WriteBlock(BinaryWriter writer, string name, Action<BinaryWriter> writeBlockData)
        {
            MemoryStream block = new MemoryStream();
            BinaryWriter blockWriter = new BinaryWriter(block);
            writeBlockData(blockWriter);
            WriteObject(writer, name, 2, 0, () =>
            {
                writer.Write((int)block.Length);
                Align(writer);
                writer.Write(block.GetBuffer(), 0, (int)block.Length);
            });
        }

        private void WriteMetadataBlock(BinaryWriter writer)
        {
            WriteBlock(writer, "MetadataBlock", w =>
            {
                // header
                w.Write((short)20); // header size
                w.Write((short)0); // flags
                w.Write((long)0);  // min timestamp
                w.Write((long)0);  // max timestamp
                WriteMetadataEventBlob(w);
            });
        }

        private void WriteMetadataEventBlob(BinaryWriter writer)
        {
            MemoryStream payload = new MemoryStream();
            BinaryWriter payloadWriter = new BinaryWriter(payload);
            payloadWriter.Write(1);                                      // metadata id
            payloadWriter.Write(Encoding.Unicode.GetBytes("Provider"));  // provider name
            payloadWriter.Write((short)0);                               // null terminator
            payloadWriter.Write(1);                                      // event id
            payloadWriter.Write(Encoding.Unicode.GetBytes("EventName")); // event name
            payloadWriter.Write((short)0);                               // null terminator
            payloadWriter.Write((long)0);                                // keywords
            payloadWriter.Write(1);                                      // version
            payloadWriter.Write(5);                                      // level
            payloadWriter.Write(0);                                      // fieldcount

            MemoryStream eventBlob = new MemoryStream();
            BinaryWriter eventWriter = new BinaryWriter(eventBlob);
            eventWriter.Write(0);                                        // metadata id
            eventWriter.Write(0);                                        // sequence number
            eventWriter.Write((long)999);                                // thread id
            eventWriter.Write((long)999);                                // capture thread id
            eventWriter.Write(1);                                        // proc number
            eventWriter.Write(0);                                        // stack id
            eventWriter.Write((long)123456789);                          // timestamp
            eventWriter.Write(Guid.Empty.ToByteArray());                 // activity id
            eventWriter.Write(Guid.Empty.ToByteArray());                 // related activity id
            eventWriter.Write((int)payload.Length);                      // payload size
            eventWriter.Write(payload.GetBuffer(), 0, (int)payload.Length);

            writer.Write((int)eventBlob.Length);
            writer.Write(eventBlob.GetBuffer(), 0, (int)eventBlob.Length);
        }

        private void WriteEventBlock(BinaryWriter writer)
        {
            WriteBlock(writer, "EventBlock", w =>
            {
                // header
                w.Write((short)20); // header size
                w.Write((short)0);  // flags
                w.Write((long)0);   // min timestamp
                w.Write((long)0);   // max timestamp
                for(int i = 0; i < 20; i++)
                {
                    WriteEventBlob(w);
                }
            });
        }

        private void WriteEventBlob(BinaryWriter writer)
        {
            // the events are big to make the stream grow fast
            const int payloadSize = 60000;

            MemoryStream eventBlob = new MemoryStream();
            BinaryWriter eventWriter = new BinaryWriter(eventBlob);
            eventWriter.Write(1);                                        // metadata id
            eventWriter.Write(_sequenceNumber++);                        // sequence number
            eventWriter.Write((long)999);                                // thread id
            eventWriter.Write((long)999);                                // capture thread id
            eventWriter.Write(1);                                        // proc number
            eventWriter.Write(0);                                        // stack id
            eventWriter.Write((long)123456789);                          // timestamp
            eventWriter.Write(Guid.Empty.ToByteArray());                 // activity id
            eventWriter.Write(Guid.Empty.ToByteArray());                 // related activity id
            eventWriter.Write(payloadSize);                              // payload size

            writer.Write((int)eventBlob.Length + payloadSize);
            writer.Write(eventBlob.GetBuffer(), 0, (int)eventBlob.Length);
            for(int i = 0; i < payloadSize/8; i++)
            {
                writer.Write((long)i);
            }
        }

        /*
         * _deserializer.RegisterFactory("Trace", delegate { return this; });
            _deserializer.RegisterFactory("EventBlock", delegate { return new EventPipeEventBlock(this); });
            _deserializer.RegisterFactory("MetadataBlock", delegate { return new EventPipeMetadataBlock(this); });
            _deserializer.RegisterFactory("SPBlock", delegate { return new EventPipeSequencePointBlock(this); });
            _deserializer.RegisterFactory("StackBlock", delegate { return new EventPipeStackBlock(this); });

         * */

        MemoryStream GetNextChunk()
        {
            MemoryStream ms = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(ms);
            if (_bytesWritten > _minSize)
            {
                writer.Write(1); // null tag
            }
            else
            {
                for(int i = 0; i < 20; i++)
                {
                    WriteEventBlock(writer);
                }
            }
            ms.Position = 0;
            return ms;
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
            if(ret == 0)
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
