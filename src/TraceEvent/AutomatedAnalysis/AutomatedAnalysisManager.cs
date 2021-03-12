using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Symbols;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    public sealed class AutomatedAnalysisManager
    {
        private ITrace _trace;
        private TextWriter _textLog;

        public AutomatedAnalysisManager(TraceLog traceLog, TextWriter textLog, SymbolReader symbolReader)
        {
            _trace = new AutomatedAnalysisTraceLog(traceLog, symbolReader);
            _textLog = textLog;
        }

        public AutomatedAnalysisManager(ITrace trace, TextWriter textLog)
        {
            _trace = trace;
            _textLog = textLog;
        }

        public AnalyzerIssueCollection Issues { get; private set; }

        public List<Analyzer> ExecuteAnalyzers()
        {
            Issues = new AnalyzerIssueCollection();

            List<Analyzer> allAnalyzers = new List<Analyzer>();
            List<PerProcessAnalyzer> perProcessAnalyzers = new List<PerProcessAnalyzer>();

            // Run global analyzers, deferring per-process analyzers.
            AnalyzerExecutionContext executionContext = new AnalyzerExecutionContext(_trace, _textLog, Issues);
            foreach (Analyzer analyzer in AnalyzerResolver.GetAnalyzers())
            {
                // Create a list of all executed analyzers so that they can be written into the report.
                allAnalyzers.Add(analyzer);

                if (analyzer is PerProcessAnalyzer)
                {
                    // Defer per-process analyzers.
                    perProcessAnalyzers.Add((PerProcessAnalyzer)analyzer);
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
            foreach (AnalyzerTraceProcess process in executionContext.Trace.Processes)
            {
                if (process.ContainsManagedCode)
                {
                    // Create the process context.
                    ProcessContext processContext = new ProcessContext(executionContext, process);

                    foreach (PerProcessAnalyzer analyzer in perProcessAnalyzers)
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
                List<Analyzer> allAnalyzers = ExecuteAnalyzers();

                // Write out issues.
                foreach (KeyValuePair<AnalyzerTraceProcess, List<AnalyzerIssue>> pair in Issues)
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