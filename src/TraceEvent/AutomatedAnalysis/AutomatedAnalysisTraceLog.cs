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
            UnderlyingSource = traceLog;
            SymbolReader = symbolReader;
        }

        /// <summary>
        /// The underlying source of the data.
        /// </summary>
        public TraceLog UnderlyingSource { get; }

        internal SymbolReader SymbolReader { get; }

        IEnumerable<Process> ITrace.Processes
        {
            get
            {
                foreach(TraceProcess traceProcess in UnderlyingSource.Processes)
                {
                    yield return new Process((int)traceProcess.ProcessIndex, traceProcess.ProcessID, traceProcess.CommandLine, traceProcess.ManagedProcess());
                }
            }
        }

        StackView ITrace.GetStacks(Process process, string stackType)
        {
            if(StackTypes.CPU.Equals(stackType))
            {
                return GetCPUStacks(process);
            }

            return null;
        }

        private StackView GetCPUStacks(Process process)
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
