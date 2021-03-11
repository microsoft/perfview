using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Symbols;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    public sealed class AutomatedAnalysisManager
    {
        private IAutomatedAnalysisTrace _trace;
        private TextWriter _textLog;

        public AutomatedAnalysisManager(TraceLog traceLog, TextWriter textLog, SymbolReader symbolReader)
        {
            _trace = new AutomatedAnalysisTraceLog(traceLog, symbolReader);
            _textLog = textLog;
        }

        public AutomatedAnalysisManager(IAutomatedAnalysisTrace trace, TextWriter textLog)
        {
            _trace = trace;
            _textLog = textLog;
        }

        public AutomatedAnalysisIssueCollection Issues { get; private set; }

        public List<AutomatedAnalysisAnalyzer> ExecuteAnalyzers()
        {
            Issues = new AutomatedAnalysisIssueCollection();

            List<AutomatedAnalysisAnalyzer> allAnalyzers = new List<AutomatedAnalysisAnalyzer>();
            List<AutomatedAnalysisPerProcessAnalyzer> perProcessAnalyzers = new List<AutomatedAnalysisPerProcessAnalyzer>();

            // Run global analyzers, deferring per-process analyzers.
            AutomatedAnalysisExecutionContext executionContext = new AutomatedAnalysisExecutionContext(_trace, _textLog, Issues);
            foreach (AutomatedAnalysisAnalyzer analyzer in AutomatedAnalysisAnalyzerResolver.GetAnalyzers())
            {
                // Create a list of all executed analyzers so that they can be written into the report.
                allAnalyzers.Add(analyzer);

                if (analyzer is AutomatedAnalysisPerProcessAnalyzer)
                {
                    // Defer per-process analyzers.
                    perProcessAnalyzers.Add((AutomatedAnalysisPerProcessAnalyzer)analyzer);
                }
                else
                {
                    // Execute the analyzer.
                    try
                    {
                        analyzer.RunAnalyzer(executionContext, null);
                    }
                    catch (Exception ex)
                    {
                        _textLog.WriteLine($"Error while executing analyzer '{analyzer.GetType().FullName}': {ex}");
                    }
                }
            }

            // Run per-process analyzers.
            foreach (AutomatedAnalysisTraceProcess process in executionContext.Trace.Processes)
            {
                if (process.ContainsManagedCode)
                {
                    // Create the process context.
                    ProcessContext processContext = new ProcessContext(executionContext, process);

                    foreach (AutomatedAnalysisPerProcessAnalyzer analyzer in perProcessAnalyzers)
                    {
                        try
                        {
                            analyzer.RunAnalyzer(executionContext, processContext);
                        }
                        catch (Exception ex)
                        {
                            _textLog.WriteLine($"Error while executing analyzer '{analyzer.GetType().FullName}': {ex}");
                        }
                    }
                }
            }

            return allAnalyzers;
        }

        public void GenerateReport(TextWriter writer)
        {
            using (AutomatedAnalysisReportGenerator reportGenerator = new AutomatedAnalysisReportGenerator(writer))
            {
                // Execute analyzers.
                List<AutomatedAnalysisAnalyzer> allAnalyzers = ExecuteAnalyzers();

                // Write out issues.
                foreach (KeyValuePair<AutomatedAnalysisTraceProcess, List<AutomatedAnalysisIssue>> pair in Issues)
                {
                    if (pair.Value.Count > 0)
                    {
                        reportGenerator.WriteIssuesForProcess(pair.Key, pair.Value);
                    }
                }

                // Write the list of executed analyzers.
               reportGenerator.WriteExecutedAnalyzerList(allAnalyzers);
            }
        }
    }
}