namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    public abstract class AutomatedAnalysisAnalyzer
    {
        protected abstract void Execute(AutomatedAnalysisExecutionContext executionContext);

        internal virtual void RunAnalyzer(AutomatedAnalysisExecutionContext executionContext, ProcessContext processContext)
        {
            Execute(executionContext);
        }
    }
}