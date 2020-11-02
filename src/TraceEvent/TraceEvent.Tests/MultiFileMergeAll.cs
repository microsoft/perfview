using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Xunit;
using Xunit.Abstractions;

namespace TraceEventTests
{
    public class MultiFileMergeAll : EtlTestBase
    {
        public MultiFileMergeAll(ITestOutputHelper output)
            : base(output)
        {
        }

        /// <summary>
        /// This test unzips a zip file containing 4 etls files, open them as 1 trace
        /// and asserts the correct TraceLog size and event count
        /// </summary>
        [Fact]
        public void ETW_MultiFileMergeAll_Basic()
        {
            PrepareTestData();
            IEnumerable<string> fileNames = Directory.EnumerateFiles(UnZippedDataDir + "\\diaghub-etls", "*.etl");
            Output.WriteLine($"In {nameof(ETW_MultiFileMergeAll_Basic)}(\"{string.Join(", ", fileNames)}\")");

            string etlFilePath = "diaghub-etls";
            Output.WriteLine(string.Format("Processing the file {0}, Making ETLX and scanning.", etlFilePath));
            string eltxFilePath = Path.ChangeExtension(etlFilePath, ".etlx");

            TraceEventDispatcher source = new ETWTraceEventSource(fileNames, TraceEventSourceType.MergeAll);
            TraceLog traceLog = new TraceLog(TraceLog.CreateFromEventTraceLogFile(source, eltxFilePath));

            Assert.Equal(95506, traceLog.EventCount);
            var stopEvents = traceLog.Events.Filter(e => e.EventName == "Activity2Stop/Stop");
            Assert.Equal(55, stopEvents.Count());
            Assert.Equal((uint)13205, (uint)stopEvents.Last().EventIndex);

            IEnumerable<string> dbEvents = traceLog.Events.Filter(e => e.EventName.Contains("Activity")).Select(e => e.EventName);

            using (var file = new StreamReader(UnZippedDataDir + "\\diaghub-etls\\diaghub-etls.txt"))
            {
                foreach (var evt in dbEvents)
                {
                    var line = file.ReadLine();
                    Assert.Equal(line, evt);
                }
            }
        }
    }
}
