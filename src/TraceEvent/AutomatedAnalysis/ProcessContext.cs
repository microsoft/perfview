using System.Collections.Generic;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    /// <summary>
    /// The process-specific context associated with execution of ProcessAnalyzers.
    /// </summary>
    public sealed class ProcessContext
    {
        private StackView _cpuStacks;
        private AnalyzerExecutionContext _executionContext;

        public ProcessContext(AnalyzerExecutionContext executionContext, AnalyzerTraceProcess process)
        {
            _executionContext = executionContext;
            Process = process;
            Issues = executionContext.Issues[process];
        }

        /// <summary>
        /// The process being analyzed.
        /// </summary>
        public AnalyzerTraceProcess Process { get; }

        public List<AnalyzerIssue> Issues { get; }

        /// <summary>
        /// The CPU stacks for the process being analyzed.
        /// </summary>
        public StackView CPUStacks
        {
            get
            {
                if (_cpuStacks == null)
                {
                    _cpuStacks = _executionContext.Trace.GetStacks(Process, StackTypes.CPU);
                }
                return _cpuStacks;
            }
        }
    }
}
