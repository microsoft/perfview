namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    /// <summary>
    /// The top-level object used to store contextual information during Analyzer execution.
    /// </summary>
    public sealed class AnalyzerExecutionContext
    {
        private Configuration _configuration;

        internal AnalyzerExecutionContext(Configuration configuration, ITrace trace)
        {
            _configuration = configuration;
            Trace = trace;
        }

        /// <summary>
        /// The configuration for the currently executing Analyzer.
        /// NULL if no configuration is available.
        /// </summary>
        public AnalyzerConfiguration Configuration
        {
            get
            {
                AnalyzerExecutionScope current = AnalyzerExecutionScope.Current;
                if (current != null)
                {
                    if (_configuration.TryGetAnalyzerConfiguration(current.ExecutingAnalyzer, out AnalyzerConfiguration config))
                    {
                        return config;
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// The trace to be analyzed.
        /// </summary>
        public ITrace Trace { get; }

        internal AnalyzerIssueCollection Issues { get; } = new AnalyzerIssueCollection();

        /// <summary>
        /// Add an identified issue.
        /// </summary>
        /// <param name="process">The process associated with the issue.</param>
        /// <param name="issue">The issue.</param>
        public void AddIssue(Process process, AnalyzerIssue issue)
        {
            Issues[process].Add(issue);
        }
    }
}
