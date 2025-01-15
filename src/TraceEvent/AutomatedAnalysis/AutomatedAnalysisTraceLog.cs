using System.Collections.Generic;
using Diagnostics.Tracing.StackSources;
using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Stacks;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    /// <summary>
    /// Encapsulates a TraceEvent TraceLog within the AutomatedAnalysis system.
    /// </summary>
    public sealed class AutomatedAnalysisTraceLog : ITrace
    {
        private MutableTraceEventStackSource _blockedTimeStacks;

        /// <summary>
        /// Create a new instance for the specified TraceLog and SymbolReader.
        /// </summary>
        /// <param name="traceLog">The TraceLog instance.</param>
        /// <param name="symbolReader">A SymbolReader that can be used to resolve symbols within the TraceLog.</param>
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

        internal HashSet<ModuleFileIndex> ResolvedModules { get; set; } = new HashSet<ModuleFileIndex>();

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
            if (StackTypes.CPU.Equals(stackType))
            {
                return GetCPUStacks(process);
            }
            else if (StackTypes.Blocked.Equals(stackType))
            {
                return GetBlockedTimeStacks(process);
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
                stackView = new StackView(this, stackSource, traceProcess.ProcessIndex, SymbolReader);
            }
            return stackView;
        }

        private StackView GetBlockedTimeStacks(Process process)
        {
            StackView stackView = null;
            TraceProcess traceProcess = UnderlyingSource.Processes[(ProcessIndex)process.UniqueID];
            if (traceProcess != null)
            {
                if (_blockedTimeStacks == null)
                {
                    _blockedTimeStacks = UnderlyingSource.BlockedTimeStacks(SymbolReader);
                }

                stackView = new StackView(this, _blockedTimeStacks, traceProcess.ProcessIndex, SymbolReader,
                    (stackSource, processIndex) =>
                    {
                        return new FilterStackSource(
                            new FilterParams()
                            {
                                IncludeRegExs = $"Process% {traceProcess.Name} ({traceProcess.ProcessID})"
                            },
                            stackSource,
                            ScalingPolicyKind.ScaleToData);
                    });
            }
            return stackView;
        }
    }
}
