namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    /// <summary>
    /// The base class for all analyzers.
    /// </summary>
    public abstract class Analyzer
    {
        /// <summary>
        /// Called by the platform to execute the Analyzer.
        /// </summary>
        /// <param name="executionContext">The context associated with this execution of the Analyzer.</param>
        /// <returns>The result of the execution.</returns>
        protected abstract AnalyzerExecutionResult Execute(AnalyzerExecutionContext executionContext);

        internal virtual AnalyzerExecutionResult RunAnalyzer(AnalyzerExecutionContext executionContext, ProcessContext processContext)
        {
            return Execute(executionContext);
        }
    }
}