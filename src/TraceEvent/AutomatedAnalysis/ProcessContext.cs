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
        private AutomatedAnalysisExecutionContext _executionContext;

        public ProcessContext(AutomatedAnalysisExecutionContext executionContext, AutomatedAnalysisTraceProcess process)
        {
            _executionContext = executionContext;
            AutomatedAnalysisProcess = process;
            AutomatedAnalysisTraceLog traceLog = executionContext.Trace as AutomatedAnalysisTraceLog;
            if(traceLog != null)
            {
                Process = traceLog.TraceLog.Processes[(ProcessIndex)process.UniqueID];
            }

            Issues = executionContext.Issues[process];
            _symbolReader = executionContext.SymbolReader;
        }

        public AutomatedAnalysisTraceProcess AutomatedAnalysisProcess { get; }

        public TraceProcess Process { get; }

        public List<AutomatedAnalysisIssue> Issues { get; }

        public StackView CPUStacks
        {
            get
            {
                if (_cpuStacks == null)
                {
                    _cpuStacks = _executionContext.Trace.GetCPUStacks(AutomatedAnalysisProcess);
                }
                return _cpuStacks;
            }
        }
    }
}
