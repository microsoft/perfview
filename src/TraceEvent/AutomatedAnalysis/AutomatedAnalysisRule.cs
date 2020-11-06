using System.Collections.Generic;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    public abstract class AutomatedAnalysisRule
    {
        protected abstract void Execute(AutomatedAnalysisExecutionContext executionContext);

        internal virtual void RunRule(AutomatedAnalysisExecutionContext executionContext, ProcessContext processContext)
        {
            Execute(executionContext);
        }
    }
}