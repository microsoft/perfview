using System;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tracing.Etlx;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    public abstract class AutomatedAnalysisPerProcessAnalyzer : AutomatedAnalysisAnalyzer
    {
        protected abstract ExecutionResult Execute(AutomatedAnalysisExecutionContext executionContext, ProcessContext processContext);

        internal override ExecutionResult RunAnalyzer(AutomatedAnalysisExecutionContext executionContext, ProcessContext processContext)
        {
            return Execute(executionContext, processContext);
        }

        protected override ExecutionResult Execute(AutomatedAnalysisExecutionContext executionContext)
        {
            throw new InvalidOperationException();
        }
    }
}
