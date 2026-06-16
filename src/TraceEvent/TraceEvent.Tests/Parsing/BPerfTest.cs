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
        public BPerfTest(ITestOutputHelper output)
            : base(output)
        {
        }

        public static IEnumerable<object[]> TestBPerfFiles => Directory.EnumerateFiles(TestDataDir, "*.btl").Select(file => new[] { file });

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void PrepareTestData()
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

            Guid systemTrace = new Guid("9e814aad-3204-11d2-9a82-006008a86939");

            using (var traceLog = new TraceLog(TraceLog.CreateFromEventTraceLogFile(Path.GetFullPath(bperfFileName), traceEventDispatcherOptions: traceEventDispatcherOptions)))
            {
                var traceSource = traceLog.Events.GetSource();

                traceSource.AllEvents += delegate (TraceEvent data)
                {
                    if (data.ProviderGuid != systemTrace || (int)data.Opcode != 80)
                    {
                        sb.AppendLine(Parse(data));
                    }
                };

                // Process
                traceSource.Process();
            }

            // Validate
            ValidateEventStatistics(sb.ToString(0, 1024 * 1024), Path.GetFileNameWithoutExtension(bperfFileName));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        public void ULZ777Decompress_DoesNotWritePastShortLiteralRun(int literalLength)
        {
            byte[] compressed = Enumerable.Repeat((byte)0xCC, 16).ToArray();
            compressed[0] = (byte)(literalLength << 5);
            int literalOffset = 1;
            if (literalLength == 7)
            {
                compressed[1] = 0;
                literalOffset = 2;
            }

            byte[] expected = new byte[literalLength];
            for (int i = 0; i < literalLength; i++)
            {
                expected[i] = (byte)('A' + i);
                compressed[i + literalOffset] = expected[i];
            }

            byte[] output = Enumerable.Repeat((byte)0xEE, 16).ToArray();

            int written = BPerfEventSource.ULZ777Decompress(compressed, 0, literalLength + literalOffset, output, literalLength);

            Assert.Equal(literalLength, written);
            Assert.Equal(expected, output.Take(literalLength).ToArray());
            Assert.All(output.Skip(literalLength), value => Assert.Equal(0xEE, value));
        }

        [Theory]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        public void ULZ777Decompress_DoesNotWritePastShortMatchLength(int matchLength)
        {
            byte[] compressed = new byte[12];
            compressed[0] = (byte)(0xE0 | (matchLength - 4));
            compressed[1] = 1; // Extend the 7-literal run to 8 literals.
            for (int i = 0; i < 8; i++)
            {
                compressed[i + 2] = (byte)('a' + i);
            }

            compressed[10] = 8;
            compressed[11] = 0;

            int outputLength = 8 + matchLength;
            byte[] output = Enumerable.Repeat((byte)0xEE, 24).ToArray();

            int written = BPerfEventSource.ULZ777Decompress(compressed, 0, compressed.Length, output, outputLength);

            Assert.Equal(outputLength, written);
            Assert.Equal(compressed.Skip(2).Take(8), output.Take(8));
            Assert.Equal(output.Take(matchLength), output.Skip(8).Take(matchLength));
            Assert.All(output.Skip(outputLength), value => Assert.Equal(0xEE, value));
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
                Assert.Fail($"The event statistics doesn't match {Path.GetFullPath(baselineFile)}. It's saved in {Path.GetFullPath(eventStatisticsFile)}.");
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
