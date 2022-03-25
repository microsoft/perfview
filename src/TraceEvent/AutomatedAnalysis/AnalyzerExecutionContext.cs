namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    /// <summary>
    /// The top-level object used to store contextual information during Analyzer execution.
    /// </summary>
    public sealed class AnalyzerExecutionContext
    {
        internal AnalyzerExecutionContext(Configuration configuration, ITrace trace)
        {
            Configuration = configuration;
            Trace = trace;
        }

        public Configuration Configuration { get; }

        /// <summary>
        /// The trace to be analyzed.
        /// </summary>
        public ITrace Trace { get; }

        public AnalyzerIssueCollection Issues { get; } = new AnalyzerIssueCollection();
    }
}
