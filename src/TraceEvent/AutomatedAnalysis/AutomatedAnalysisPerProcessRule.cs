using System;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tracing.Etlx;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    public abstract class AutomatedAnalysisPerProcessRule : AutomatedAnalysisRule
    {
        protected abstract void Execute(AutomatedAnalysisExecutionContext executionContext, ProcessContext processContext);

        internal override void RunRule(AutomatedAnalysisExecutionContext executionContext, ProcessContext processContext)
        {
            Execute(executionContext, processContext);
        }

        protected override void Execute(AutomatedAnalysisExecutionContext executionContext)
        {
            throw new InvalidOperationException();
        }
    }
}
