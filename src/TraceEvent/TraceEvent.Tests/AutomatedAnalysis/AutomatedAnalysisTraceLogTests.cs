using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.AutomatedAnalysis;
using Microsoft.Diagnostics.Tracing.Etlx;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace TraceEventTests
{
    public sealed class AutomatedAnalysisTraceLogTests : EtlTestBase
    {
        public AutomatedAnalysisTraceLogTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory()]
        [MemberData(nameof(TestEtlFiles))]
        public void CompareProcesses(string inputTestFile)
        {
            PrepareTestData();

            string inputFilePath = Path.Combine(UnZippedDataDir, inputTestFile);
            using (TraceLog traceLog = TraceLog.OpenOrConvert(inputFilePath))
            {
                // Create the list of TraceLog processes.
                Dictionary<int, string> traceLogProcesses = new Dictionary<int, string>();
                foreach (TraceProcess process in traceLog.Processes)
                {
                    traceLogProcesses.Add((int)process.ProcessIndex, process.CommandLine);
                }

                // Find and remove all of the AutoAnalyzer processes from the list.
                AutomatedAnalysisTraceLog autoAnalysisTraceLog = new AutomatedAnalysisTraceLog(traceLog, new SymbolReader(TextWriter.Null));
                foreach (AnalyzerTraceProcess process in ((ITrace)autoAnalysisTraceLog).Processes)
                {
                    string commandLine;
                    Assert.True(traceLogProcesses.TryGetValue(process.UniqueID, out commandLine));
                    Assert.Equal(commandLine, process.Description);
                    Assert.True(traceLogProcesses.Remove(process.UniqueID));
                }

                Assert.True(traceLogProcesses.Count == 0);
            }
        }
    }
}