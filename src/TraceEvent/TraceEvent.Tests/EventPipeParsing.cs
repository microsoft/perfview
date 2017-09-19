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
        private class DebugListenerBlock : IDisposable
        {
            public DebugListenerBlock()
            {
                if (System.Diagnostics.Debug.Listeners != null && System.Diagnostics.Debug.Listeners.Count > 0)
                {
                    _blockedListeners = new System.Diagnostics.TraceListener[System.Diagnostics.Debug.Listeners.Count];
                    System.Diagnostics.Debug.Listeners.CopyTo(_blockedListeners, 0);
                    System.Diagnostics.Debug.Listeners.Clear();
                }
            }

            public void Dispose()
            {
                if (_blockedListeners != null)
                {
                    System.Diagnostics.Debug.Listeners.AddRange(_blockedListeners);
                }
            }

            private System.Diagnostics.TraceListener[] _blockedListeners;
        }

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
