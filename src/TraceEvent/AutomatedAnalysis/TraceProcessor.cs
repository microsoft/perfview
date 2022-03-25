using System;
using System.IO;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    public sealed class TraceProcessor
    {
        private IEnumerable<Analyzer> _analyzers;
        private Configuration _configuration;

        public TraceProcessor(AnalyzerResolver analyzerResolver)
        {
            // Resolve the set of analyzers and configuration.
            analyzerResolver.Resolve();

            // Save the results.
            _analyzers = analyzerResolver.ResolvedAnalyzers;
            _configuration = analyzerResolver.Configuration;

        }

        public TraceProcessorResult ProcessTrace(ITrace trace, TextWriter textLog)
        {
            List<ProcessAnalyzer> processAnalyzers = new List<ProcessAnalyzer>();

            // Run global analyzers, deferring per-process analyzers.
            AnalyzerExecutionContext executionContext = new AnalyzerExecutionContext(_configuration, trace, textLog);
            foreach (Analyzer analyzer in _analyzers)
            {
                if (analyzer is ProcessAnalyzer)
                {
                    // Defer per-process analyzers.
                    processAnalyzers.Add((ProcessAnalyzer)analyzer);
                }
                else
                {
                    // Execute the analyzer.
                    try
                    {
                        using (new AnalyzerExecutionScope(analyzer))
                        {
                            analyzer.RunAnalyzer(executionContext, null);
                        }
                    }
                    catch (Exception ex)
                    {
                        textLog.WriteLine($"Error while executing analyzer '{analyzer.GetType().FullName}': {ex}");
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

                    foreach (ProcessAnalyzer analyzer in processAnalyzers)
                    {
                        try
                        {
                            using (new AnalyzerExecutionScope(analyzer))
                            {
                                analyzer.RunAnalyzer(executionContext, processContext);
                            }
                        }
                        catch (Exception ex)
                        {
                            textLog.WriteLine($"Error while executing analyzer '{analyzer.GetType().FullName}': {ex}");
                        }
                    }
                }
            }

            return new TraceProcessorResult(_analyzers, executionContext);
        }
    }
}