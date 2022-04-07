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
            string etlxFileName = Path.GetFileNameWithoutExtension(inputFilePath) + "-AutoAnalysis.etlx";
            string etlxFilePath = Path.Combine(UnZippedDataDir, etlxFileName);
            etlxFilePath = TraceLog.CreateFromEventTraceLogFile(inputFilePath, etlxFilePath);
            using (TraceLog traceLog = new TraceLog(etlxFilePath))
            {
                // Create the list of TraceLog processes.
                Dictionary<int, TraceProcess> traceLogProcesses = new Dictionary<int, TraceProcess>();
                foreach (TraceProcess process in traceLog.Processes)
                {
                    traceLogProcesses.Add((int)process.ProcessIndex, process);
                }

                // Find and remove all of the AutoAnalyzer processes from the list.
                AutomatedAnalysisTraceLog autoAnalysisTraceLog = new AutomatedAnalysisTraceLog(traceLog, new SymbolReader(TextWriter.Null));
                foreach (Process process in ((ITrace)autoAnalysisTraceLog).Processes)
                {
                    TraceProcess traceProcess;
                    Assert.True(traceLogProcesses.TryGetValue(process.UniqueID, out traceProcess));

                    // Verify that details match.
                    Assert.Equal(traceProcess.ProcessID, process.DisplayID);
                    Assert.Equal(traceProcess.CommandLine, process.Description);
                    Assert.Equal(traceProcess.ManagedProcess(), process.ContainsManagedCode);
                    Assert.True(traceLogProcesses.Remove(process.UniqueID));
                }

                // Make sure we didn't miss any.
                Assert.True(traceLogProcesses.Count == 0);
            }
        }
    }
}