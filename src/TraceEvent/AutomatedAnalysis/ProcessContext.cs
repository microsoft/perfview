using System.Collections.Generic;
using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Stacks;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    public sealed class ProcessContext
    {
        private StackView _cpuStacks;
        private SymbolReader _symbolReader;

        public ProcessContext(AutomatedAnalysisExecutionContext executionContext, TraceProcess process)
        {
            Process = process;
            Issues = executionContext.Issues[process];
            _symbolReader = executionContext.SymbolReader;
        }

        public TraceProcess Process { get; }

        public List<AutomatedAnalysisIssue> Issues { get; }

        public StackView CPUStacks
        {
            get
            {
                if (_cpuStacks == null)
                {
                    StackSource stackSource = Process.Log.CPUStacks(Process);
                    _cpuStacks = new StackView(Process.Log, stackSource, _symbolReader);
                }
                return _cpuStacks;
            }
        }
    }
}
