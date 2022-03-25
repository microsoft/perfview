using System;
using System.Collections.Generic;
using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Stacks;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    public sealed class AutomatedAnalysisTraceLog : ITrace
    {
        public AutomatedAnalysisTraceLog(TraceLog traceLog, SymbolReader symbolReader)
        {
            TraceLog = traceLog;
            UnderlyingSource = traceLog;
            SymbolReader = symbolReader;
        }

        [Obsolete]
        public TraceLog TraceLog { get; }

        public TraceLog UnderlyingSource { get; }

        internal SymbolReader SymbolReader { get; }

        IEnumerable<AnalyzerTraceProcess> ITrace.Processes
        {
            get
            {
                foreach(TraceProcess traceProcess in UnderlyingSource.Processes)
                {
                    yield return new AnalyzerTraceProcess((int)traceProcess.ProcessIndex, traceProcess.ProcessID, traceProcess.CommandLine, traceProcess.ManagedProcess());
                }
            }
        }

        [Obsolete]
        StackView ITrace.GetCPUStacks(AnalyzerTraceProcess process)
        {
            return GetCPUStacks(process);
        }

        StackView ITrace.GetStacks(AnalyzerTraceProcess process, string stackType)
        {
            if(StackTypes.CPU.Equals(stackType))
            {
                return GetCPUStacks(process);
            }

            return null;
        }

        private StackView GetCPUStacks(AnalyzerTraceProcess process)
        {
            StackView stackView = null;
            TraceProcess traceProcess = UnderlyingSource.Processes[(ProcessIndex)process.UniqueID];
            if (traceProcess != null)
            {
                StackSource stackSource = UnderlyingSource.CPUStacks(traceProcess);
                stackView = new StackView(traceProcess.Log, stackSource, SymbolReader);
            }
            return stackView;
        }
    }
}
