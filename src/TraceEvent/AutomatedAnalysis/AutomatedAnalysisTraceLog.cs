using System.Collections.Generic;
using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Stacks;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    internal sealed class AutomatedAnalysisTraceLog : IAutomatedAnalysisTrace
    {
        internal AutomatedAnalysisTraceLog(TraceLog traceLog, SymbolReader symbolReader)
        {
            TraceLog = traceLog;
            SymbolReader = symbolReader;
        }

        internal TraceLog TraceLog { get; }
        internal SymbolReader SymbolReader { get; }

        IEnumerable<AutomatedAnalysisTraceProcess> IAutomatedAnalysisTrace.Processes
        {
            get
            {
                foreach(TraceProcess traceProcess in TraceLog.Processes)
                {
                    yield return new AutomatedAnalysisTraceProcess((int)traceProcess.ProcessIndex, traceProcess.ProcessID, traceProcess.CommandLine, traceProcess.ManagedProcess());
                }
            }
        }

        StackView IAutomatedAnalysisTrace.GetCPUStacks(AutomatedAnalysisTraceProcess process)
        {
            StackView stackView = null;
            TraceProcess traceProcess = TraceLog.Processes[(ProcessIndex)process.UniqueID];
            if(traceProcess != null)
            {
                StackSource stackSource = TraceLog.CPUStacks(traceProcess);
                stackView = new StackView(traceProcess.Log, stackSource, SymbolReader);
            }
            return stackView;
        }
    }
}
