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
        /// This test simply scans all the events in the ETL.ZIP files in TestDataDir
        /// and scans them (so you should get asserts if there is parsing problem)
        /// and insures that no more than .1% of the events are 
        /// </summary>
        [Fact]
        public void ETW_MultiFileMergeAll_Basic()
        {
            PrepareTestData();
            var fileNames = Directory.EnumerateFiles(UnZippedDataDir + "\\diaghub-etls", "*.etl");
            Output.WriteLine($"In {nameof(ETW_MultiFileMergeAll_Basic)}(\"{string.Join(", ", fileNames)}\")");

            string etlFilePath = "diaghub";
            Output.WriteLine(string.Format("Processing the file {0}, Making ETLX and scanning.", etlFilePath));
            string eltxFilePath = Path.ChangeExtension(etlFilePath, ".etlx");

            // See if we have a cooresponding baseline file 
            string baselineName = Path.Combine(TestDataDir,
                Path.GetFileNameWithoutExtension(etlFilePath) + ".baseline.txt");

            string newBaselineName = Path.Combine(NewBaselineDir,
                Path.GetFileNameWithoutExtension(etlFilePath) + ".baseline.txt");
            string outputName = Path.Combine(OutputDir,
                Path.GetFileNameWithoutExtension(etlFilePath) + ".txt");
            TextWriter outputFile = File.CreateText(outputName);

            StreamReader baselineFile = null;
            if (File.Exists(baselineName))
            {
                baselineFile = File.OpenText(baselineName);
            }
            else
            {
                Output.WriteLine("WARNING: No baseline file");
                Output.WriteLine(string.Format("    ETL FILE: {0}", etlFilePath));
                Output.WriteLine(string.Format("    NonExistant Baseline File: {0}", baselineName));
                Output.WriteLine("To Create a baseline file");
                Output.WriteLine(string.Format("    copy /y \"{0}\" \"{1}\"", newBaselineName, baselineName));
            }

            // TraceLog traceLog = TraceLog.OpenOrConvert(etlFilePath);    // This one can be used during development of test itself
            TraceLog traceLog = new TraceLog(TraceLog.CreateFromEventTraceLogFile(new ETWTraceEventSource(fileNames, TraceEventSourceType.MergeAll), eltxFilePath));

            var traceSource = traceLog.Events.GetSource();

            Assert.Equal(25898695, traceLog.Size);
            Assert.Equal(95506, traceLog.EventCount);
        }
    }
}
