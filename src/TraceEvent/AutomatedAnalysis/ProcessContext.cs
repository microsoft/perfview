using System.Collections.Generic;
using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Stacks;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
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

        public AnalyzerTraceProcess Process { get; }

        public List<AnalyzerIssue> Issues { get; }

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
