using System.Collections.Generic;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    /// <summary>
    /// The result of a trace processing operation.
    /// </summary>
    public sealed class TraceProcessorResult
    {
        private IEnumerable<Analyzer> _executedAnalyzers;
        private AnalyzerExecutionContext _executionContext;

        internal TraceProcessorResult(
            IEnumerable<Analyzer> executedAnalyzers,
            AnalyzerExecutionContext executionContext)
        {
            _executedAnalyzers = executedAnalyzers;
            _executionContext = executionContext;
        }

        /// <summary>
        /// The set of Analyzers that were executed.
        /// </summary>
        public IEnumerable<Analyzer> ExecutedAnalyzers
        {
            get { return _executedAnalyzers; }
        }

        /// <summary>
        /// The set of issues identified by the executed Analyzers.
        /// </summary>
        public AnalyzerIssueCollection Issues
        {
            get { return _executionContext.Issues; }
        }
    }
}
