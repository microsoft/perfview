using System;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    public abstract class ProcessAnalyzer : Analyzer
    {
        protected abstract AnalyzerExecutionResult Execute(AnalyzerExecutionContext executionContext, ProcessContext processContext);

        internal override AnalyzerExecutionResult RunAnalyzer(AnalyzerExecutionContext executionContext, ProcessContext processContext)
        {
            return Execute(executionContext, processContext);
        }

        protected override AnalyzerExecutionResult Execute(AnalyzerExecutionContext executionContext)
        {
            throw new InvalidOperationException();
        }
    }
}
