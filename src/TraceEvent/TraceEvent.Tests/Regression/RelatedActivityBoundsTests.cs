using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers;
using System;
using System.IO;
using Xunit;

namespace TraceEventTests.Regression
{
    public unsafe class RelatedActivityBoundsTests
    {
        [Fact]
        public void ReadersRejectPresentButIncompleteDescriptors()
        {
            IsolatedTest.Run(() =>
            {
                using (var source = new TestSource())
                {
                    var writer = new EventPipeWriterV6();
                    writer.WriteHeaders();
                    writer.WriteEndBlock();
                    using (var eventPipe = new EventPipeEventSource(new MemoryStream(writer.ToArray())))
                    {
                        Assert.True(eventPipe.Process());
                        for (int length = 0; length < 16; length++)
                        {
                            WithDescriptor(length, false, data =>
                            {
                                data.traceEventSource = source;
                                Assert.Throws<InvalidDataException>(() => data.RelatedActivityID);
                                Assert.Throws<InvalidDataException>(() => data.Dump());
                                Assert.Throws<InvalidDataException>(() => eventPipe.GetRelatedActivityID(data.eventRecord));
                            });
                        }
                        WithDescriptor(16, true, data =>
                        {
                            data.traceEventSource = source;
                            Assert.Throws<InvalidDataException>(() => data.RelatedActivityID);
                            Assert.Throws<InvalidDataException>(() => eventPipe.GetRelatedActivityID(data.eventRecord));
                        });
                    }
                }
            }, GetType());
        }

        [Fact]
        public void ConversionRejectsIncompleteDescriptorsBeforeRetainingThem()
        {
            IsolatedTest.Run(() =>
            {
                using (var log = (TraceLog)Activator.CreateInstance(typeof(TraceLog), nonPublic: true))
                {
                    foreach (bool live in new[] { false, true })
                    {
                        for (int length = 0; length < 16; length++)
                            WithDescriptor(length, false, data =>
                                Assert.Throws<InvalidDataException>(() => log.ProcessExtendedData(data, 1, null, live)));
                        WithDescriptor(16, true, data =>
                            Assert.Throws<InvalidDataException>(() => log.ProcessExtendedData(data, 1, null, live)));
                    }
                }
            }, GetType());
        }

        [Fact]
        public void CompleteAndAbsentDescriptorsKeepTheirMeaning()
        {
            IsolatedTest.Run(() =>
            {
                using (var source = new TestSource())
                using (var log = (TraceLog)Activator.CreateInstance(typeof(TraceLog), nonPublic: true))
                {
                    foreach (int length in new[] { 16, 24 })
                    {
                        WithDescriptor(length, false, data =>
                        {
                            data.traceEventSource = source;
                            Assert.Equal(Expected, data.RelatedActivityID);
                            Assert.Contains("RelatedActivityID", data.Dump());
                            Assert.False(log.ProcessExtendedData(data, 1, null, false));
                            data.traceEventSource = log;
                            Assert.Equal(Expected, data.RelatedActivityID);
                        });
                    }
                    WithDescriptor(0, true, data =>
                    {
                        data.traceEventSource = source;
                        data.eventRecord->ExtendedDataCount = 0;
                        data.eventRecord->ExtendedData = null;
                        Assert.Equal(Guid.Empty, data.RelatedActivityID);
                    });
                }
            }, GetType());
        }

        [Fact]
        public void BPerfRejectsShortTypedDataBeforePublishingPointers()
        {
            IsolatedTest.Run(() =>
            {
                for (int length = 0; length < 16; length++)
                {
                    int size = length;
                    WithBPerf(size, path => Assert.Throws<InvalidDataException>(() => Open(path)));
                }
                WithBPerf(16, path => { using (Open(path)) { } });
            }, GetType());
        }

        #region private
        private static void WithDescriptor(int length, bool nullPointer, Action<TraceEvent> test)
        {
            Guid value = Expected;
            var item = new TraceEventNativeMethods.EVENT_HEADER_EXTENDED_DATA_ITEM
            {
                ExtType = TraceEventNativeMethods.EVENT_HEADER_EXT_TYPE_RELATED_ACTIVITYID,
                DataSize = (ushort)length,
                DataPtr = nullPointer ? 0 : (ulong)&value
            };
            var record = new TraceEventNativeMethods.EVENT_RECORD
            {
                ExtendedDataCount = 1,
                ExtendedData = &item
            };
            record.EventHeader.Id = 1;
            record.EventHeader.TimeStamp = 1;
            record.EventHeader.Flags = TraceEventNativeMethods.EVENT_HEADER_FLAG_32_BIT_HEADER;
            var data = new EmptyTraceData(null, 1, 0, "Task", Guid.Empty, 0, "Opcode", Guid.Empty, "Provider");
            data.eventRecord = &record;
            try { test(data); }
            finally { data.eventRecord = null; }
        }

        private static BPerfEventSource Open(string path)
        {
            return new BPerfEventSource(path, null, new byte[1024], new byte[1024],
                decompressDelegate: (input, offset, length, output, capacity) =>
                {
                    Array.Copy(input, offset, output, 0, length);
                    return length;
                });
        }

        private static void WithBPerf(int length, Action<string> test)
        {
            byte[] record = new byte[(128 + length + 15) & ~15];
            BitConverter.GetBytes((ushort)1).CopyTo(record, 84);
            BitConverter.GetBytes(TraceEventNativeMethods.EVENT_HEADER_EXT_TYPE_RELATED_ACTIVITYID).CopyTo(record, 114);
            BitConverter.GetBytes((ushort)length).CopyTo(record, 118);
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".btl");
            try
            {
                using (var writer = new BinaryWriter(File.Create(path)))
                {
                    writer.Write(record.Length);
                    writer.Write(record.Length);
                    writer.Write(record);
                }
                test(path);
            }
            finally { File.Delete(path); }
        }

        private sealed class TestSource : TraceEventDispatcher
        {
            public TestSource()
            {
                _QPCFreq = 10000000;
                _syncTimeQPC = sessionStartTimeQPC = 1;
                _syncTimeUTC = DateTime.UtcNow;
            }
            public override int EventsLost => 0;
            public override bool Process() => true;
        }

        private static readonly Guid Expected = new Guid("12345678-1234-5678-90ab-1234567890ab");
        #endregion
    }
}
