using System;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    /// <summary>
    /// An Analyzer that is invoked individually for each process in a trace.
    /// </summary>
    public abstract class ProcessAnalyzer : Analyzer
    {
        /// <summary>
        /// Called by the platform to analyze a single process.
        /// </summary>
        /// <param name="executionContext">The context associated with this execution of the Analyzer.</param>
        /// <param name="processContext">The process-specific context associated with this execution of the Analyzer.</param>
        /// <returns>The result of the execution.</returns>
        protected abstract AnalyzerExecutionResult Execute(AnalyzerExecutionContext executionContext, ProcessContext processContext);

        internal override AnalyzerExecutionResult RunAnalyzer(AnalyzerExecutionContext executionContext, ProcessContext processContext)
        {
            return Execute(executionContext, processContext);
        }

        protected override AnalyzerExecutionResult Execute(AnalyzerExecutionContext executionContext)
        {
            throw new InvalidOperationException();
        }
    }
}
