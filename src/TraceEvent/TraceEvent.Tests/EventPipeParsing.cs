using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
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
                Assert.Equal(11376, eventPipeSource._processId);
                Assert.Equal(4, eventPipeSource.NumberOfProcessors);
                Assert.Equal(1000000, eventPipeSource._expectedCPUSamplingRate);

                Assert.Equal(636522350205880000, eventPipeSource._syncTimeUTC.Ticks);
                Assert.Equal(44518740604, eventPipeSource._syncTimeQPC);
                Assert.Equal(2533308, eventPipeSource._QPCFreq);

                Assert.Equal(10, eventPipeSource.CpuSpeedMHz);
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

                Assert.True(false, $"The event statistics doesn't match {Path.GetFullPath(baselineFile)}. It's saved in {Path.GetFullPath(eventStatisticsFile)}.");
            }
        }
    }
}
