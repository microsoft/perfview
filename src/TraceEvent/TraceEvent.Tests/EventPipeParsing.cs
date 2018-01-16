using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.EventPipe;
using System;
using System.Collections.Generic;
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

        [Fact]
        public void CanReadInitialDataFrom10File()
        {
            PrepareTestData();

            const string eventPipeFileName = "eventpipe-dotnetcore2.0-linux-objver1.netperf";

            string eventPipeFilePath = Path.Combine(UnZippedDataDir, eventPipeFileName);

            var deserializer = new FastSerialization.Deserializer(eventPipeFilePath);

            var initialData = EventPipeEventSourceFactory.Read(deserializer, eventPipeFileName);

            Assert.Equal(1, initialData.Version);
            Assert.Equal(0, initialData.ReaderVersion);

            Assert.Equal(8, initialData.PointerSize);
            Assert.Equal(10, initialData.CpuSpeedMHz);

            Assert.Equal(75399820, (int)initialData.EndOfStream);
            Assert.Equal(636414354195130000, initialData.CreationTime.Ticks);
            Assert.Equal(1477613380157300, initialData.StartTimeStamp);
            Assert.Equal(1000000000, initialData.ClockFrequency);
        }

        [Fact]
        public void CanReadInitialDataFrom21File()
        {
            PrepareTestData();

            const string eventPipeFileName = "eventpipe-dotnetcore2.0-win-x86.netperf";

            string eventPipeFilePath = Path.Combine(UnZippedDataDir, eventPipeFileName);

            var deserializer = new FastSerialization.Deserializer(eventPipeFilePath);

            var initialData = EventPipeEventSourceFactory.Read(deserializer, eventPipeFileName);

            Assert.Equal(2, initialData.Version);
            Assert.Equal(1, initialData.ReaderVersion);

            Assert.Equal(4, initialData.PointerSize);
            Assert.Equal(4, initialData.NumberOfProcessors);
            
            Assert.Equal(7262328, (int)initialData.EndOfStream);
            Assert.Equal(636517443804970000, initialData.CreationTime.Ticks);
            Assert.Equal(58178912802, initialData.StartTimeStamp);
            Assert.Equal(2533310, initialData.ClockFrequency);

            Assert.Equal(10, initialData.CpuSpeedMHz);
        }

        [Theory]
        [MemberData(nameof(TestEventPipeFiles))]
        public void Basic(string eventPipeFileName)
        {
            // Initialize
            PrepareTestData();

            string eventPipeFilePath = Path.Combine(UnZippedDataDir, eventPipeFileName);

            Output.WriteLine(string.Format("Processing the file {0}, Making ETLX and scanning.", Path.GetFullPath(eventPipeFilePath)));

            var traceSource = new TraceLog(TraceLog.CreateFromEventPipeDataFile(eventPipeFilePath)).Events.GetSource();
            var eventStatistics = new SortedDictionary<string, EventRecord>(StringComparer.Ordinal);
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

            // Validate
            ValidateEventStatistics(eventStatistics, eventPipeFileName);
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

                Assert.True(false, $"The event statistics doesn't match {Path.GetFullPath(baselineFile)}. It's saved in {Path.GetFullPath(eventStatisticsFile)}.");
            }
        }
    }
}
