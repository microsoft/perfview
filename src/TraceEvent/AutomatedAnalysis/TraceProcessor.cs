using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    /// <summary>
    /// Processes traces by running a set of Analyzers against the trace data.
    /// </summary>
    public sealed class TraceProcessor
    {
        private IEnumerable<Analyzer> _analyzers;
        private Configuration _configuration;

        /// <summary>
        /// Creates a new instance of TraceProcessor with the specified AnalyzerResolver.
        /// </summary>
        /// <param name="analyzerResolver">The resolver that will be used to discover Analyzers for execution.</param>
        public TraceProcessor(AnalyzerResolver analyzerResolver)
        {
            // Resolve the set of analyzers and configuration.
            analyzerResolver.Resolve();

            // Save the results.
            _analyzers = analyzerResolver.ResolvedAnalyzers;
            _configuration = analyzerResolver.Configuration;

        }

        /// <summary>
        /// Process a single trace.
        /// </summary>
        /// <param name="trace">The trace.</param>
        /// <returns>The result of processing the trace.</returns>
        public TraceProcessorResult ProcessTrace(ITrace trace)
        {
            List<ProcessAnalyzer> processAnalyzers = new List<ProcessAnalyzer>();

            // Run global analyzers, deferring per-process analyzers.
            AnalyzerExecutionContext executionContext = new AnalyzerExecutionContext(_configuration, trace);
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
                        AutomatedAnalysisEventSource.Log.Error($"Error while executing analyzer '{analyzer.GetType().FullName}': {ex}");
                    }
                }
            }

            // Run per-process analyzers.
            foreach (Process process in executionContext.Trace.Processes)
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
                            AutomatedAnalysisEventSource.Log.Error($"Error while executing analyzer '{analyzer.GetType().FullName}': {ex}");
                        }
                    }
                }
            }

            return new TraceProcessorResult(_analyzers, executionContext);
        }
    }
}