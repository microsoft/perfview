using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using PerfView.TestUtilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace TraceEventTests
{
    [UseCulture("en-US")]
    public sealed class BPerfTest : TestBase
    {
        private class EventRecord
        {
            public int TotalCount;
            public string FirstSeriazliedSample;
        }

        public BPerfTest(ITestOutputHelper output)
            : base(output)
        {
        }

        public static IEnumerable<object[]> TestBPerfFiles => Directory.EnumerateFiles(TestDataDir, "*.btl").Select(file => new[] { file });

        [MethodImpl(MethodImplOptions.Synchronized)]
        protected void PrepareTestData()
        {
            Assert.True(Directory.Exists(TestDataDir));
            TestDataDir = Path.GetFullPath(TestDataDir);
            Assert.True(Directory.Exists(OriginalBaselineDir));
            OriginalBaselineDir = Path.GetFullPath(OriginalBaselineDir);

            if (Directory.Exists(OutputDir))
            {
                Directory.Delete(OutputDir, true);
            }

            Directory.CreateDirectory(OutputDir);
            Output.WriteLine(string.Format("OutputDir: {0}", Path.GetFullPath(OutputDir)));
            Assert.True(Directory.Exists(OutputDir));

            Directory.CreateDirectory(NewBaselineDir);
            NewBaselineDir = Path.GetFullPath(NewBaselineDir);
            Assert.True(Directory.Exists(NewBaselineDir));
            Output.WriteLine(string.Format("NewBaselineDir: {0}", NewBaselineDir));

            Assert.True(Directory.Exists(BaseOutputDir));
            BaseOutputDir = Path.GetFullPath(BaseOutputDir);
        }

        [Theory()]
        [MemberData(nameof(TestBPerfFiles))]
        public void Basic(string bperfFileName)
        {
            // Initialize
            PrepareTestData();

            Output.WriteLine(string.Format("Processing the file {0}, Making ETLX and scanning.", Path.GetFullPath(bperfFileName)));
            var sb = new StringBuilder(1024 * 1024);
            var traceEventDispatcherOptions = new TraceEventDispatcherOptions();

            using (var traceLog = new TraceLog(TraceLog.CreateFromEventTraceLogFile(Path.GetFullPath(bperfFileName), traceEventDispatcherOptions: traceEventDispatcherOptions)))
            {
                var traceSource = traceLog.Events.GetSource();

                traceSource.AllEvents += delegate (TraceEvent data)
                {
                    sb.AppendLine(Parse(data));
                };

                // Process
                traceSource.Process();
            }

            // Validate
            ValidateEventStatistics(sb.ToString(0, 1024 * 1024), Path.GetFileNameWithoutExtension(bperfFileName));
        }

        private void ValidateEventStatistics(string actual, string bperfFileName)
        {
            string baselineFile = Path.Combine(TestDataDir, bperfFileName + ".baseline.txt");
            string expected = File.ReadAllText(baselineFile);

            if (!string.Equals(actual, expected, StringComparison.Ordinal))
            {
                string eventStatisticsFile = Path.Combine(TestDataDir, bperfFileName + ".actual.txt");
                File.WriteAllText(eventStatisticsFile, actual, Encoding.UTF8);

                Output.WriteLine("Actual: " + actual);

                Output.WriteLine($"Baseline File: {baselineFile}");
                Output.WriteLine($"Actual File: {eventStatisticsFile}");
                Output.WriteLine($"To Diff: windiff {baselineFile} {eventStatisticsFile}");
                Assert.True(false, $"The event statistics doesn't match {Path.GetFullPath(baselineFile)}. It's saved in {Path.GetFullPath(eventStatisticsFile)}.");
            }
        }

        private static string Parse(TraceEvent data)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("EVENT ");
            sb.Append(data.TimeStampRelativeMSec.ToString("n3")).Append(": ");
            sb.Append(data.ProviderName).Append("/").Append(data.EventName).Append(" ");

            sb.Append("PID=").Append(data.ProcessID).Append("; ");
            sb.Append("TID=").Append(data.ThreadID).Append("; ");
            sb.Append("PName=").Append(data.ProcessName).Append("; ");
            sb.Append("ProceNum=").Append(data.ProcessorNumber).Append("; ");
            sb.Append("DataLen=").Append(data.EventDataLength).Append("; ");

            string[] payloadNames = data.PayloadNames;
            for (int i = 0; i < payloadNames.Length; i++)
            {
                // Normalize DateTime to UTC so tests work in any timezone. 
                object value = data.PayloadValue(i);
                string valueStr;
                if (value is DateTime)
                {
                    valueStr = ((DateTime)value).ToUniversalTime().ToString("yy/MM/dd HH:mm:ss.ffffff");
                }
                else
                {
                    valueStr = (data.PayloadString(i));
                }

                // To debug this set first chance exeption handing before calling PayloadString above.
                Assert.False(valueStr.Contains("EXCEPTION_DURING_VALUE_LOOKUP"), "Exception during event Payload Processing");

                // Keep the value size under control and remove newlines.  
                if (valueStr.Length > 20)
                {
                    valueStr = valueStr.Substring(0, 20) + "...";
                }

                valueStr = valueStr.Replace("\n", "\\n").Replace("\r", "\\r");

                sb.Append(payloadNames[i]).Append('=').Append(valueStr).Append("; ");
            }

            return sb.ToString();
        }
    }
}
