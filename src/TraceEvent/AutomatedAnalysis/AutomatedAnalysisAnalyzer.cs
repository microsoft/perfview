namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    public abstract class AutomatedAnalysisAnalyzer
    {
        protected abstract ExecutionResult Execute(AutomatedAnalysisExecutionContext executionContext);

        internal virtual ExecutionResult RunAnalyzer(AutomatedAnalysisExecutionContext executionContext, ProcessContext processContext)
        {
            return Execute(executionContext);
        }
    }
}