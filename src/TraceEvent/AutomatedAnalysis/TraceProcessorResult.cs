using System.Collections.Generic;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
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

        public IEnumerable<Analyzer> ExecutedAnalyzers
        {
            get { return _executedAnalyzers; }
        }

        public AnalyzerIssueCollection Issues
        {
            get { return _executionContext.Issues; }
        }
    }
}
