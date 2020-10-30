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
            var fileNames = Directory.EnumerateFiles(UnZippedDataDir + "\\diaghub-etls", "*.etl");
            Output.WriteLine($"In {nameof(ETW_MultiFileMergeAll_Basic)}(\"{string.Join(", ", fileNames)}\")");

            string etlFilePath = "diaghub-etls";
            Output.WriteLine(string.Format("Processing the file {0}, Making ETLX and scanning.", etlFilePath));
            string eltxFilePath = Path.ChangeExtension(etlFilePath, ".etlx");

            TraceEventDispatcher source = new ETWTraceEventSource(fileNames, TraceEventSourceType.MergeAll);
            TraceLog traceLog = new TraceLog(TraceLog.CreateFromEventTraceLogFile(source, eltxFilePath));

            var traceSource = traceLog.Events.GetSource();

            Assert.Equal(95506, traceLog.EventCount);
        }
    }
}
