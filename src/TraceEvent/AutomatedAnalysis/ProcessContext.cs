using System.Collections.Generic;
using System.Threading;

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
        }

        /// <summary>
        /// The process being analyzed.
        /// </summary>
        public AnalyzerTraceProcess Process { get; }

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

        /// <summary>
        /// Add an identified issue.
        /// </summary>
        /// <param name="issue">The issue.</param>
        public void AddIssue(AnalyzerIssue issue)
        {
            _executionContext.AddIssue(Process, issue);
        }
    }
}
