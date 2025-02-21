using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Universal.Events;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Tracing.SourceConverters
{
    internal sealed class NettraceUniversalConverter
    {
        private List<ProcessSymbolTraceData> _dynamicSymbols = new List<ProcessSymbolTraceData>();
        private Dictionary<int, TraceProcess> _mappingIdToProcesses = new Dictionary<int, TraceProcess>();

        internal NettraceUniversalConverter()
        {
        }

        public static void RegisterParsers(TraceLog traceLog)
        {
            new UniversalEventsTraceEventParser(traceLog);
            new UniversalSystemTraceEventParser(traceLog);
        }

        public void BeforeProcess(TraceLog traceLog, TraceEventDispatcher source)
        {
            UniversalSystemTraceEventParser universalParser = new UniversalSystemTraceEventParser(source);
            universalParser.ExistingProcess += delegate (ProcessCreateTraceData data)
            {
                TraceProcess process = traceLog.Processes.GetOrCreateProcess(data.Id, data.TimeStampQPC);
                process.UniversalProcessStart(data);
            };
            universalParser.ProcessCreate += delegate (ProcessCreateTraceData data)
            {
                TraceProcess process = traceLog.Processes.GetOrCreateProcess(data.Id, data.TimeStampQPC, isProcessStartEvent: true);
                process.UniversalProcessStart(data);
            };
            universalParser.ProcessExit += delegate (ProcessExitTraceData data)
            {
                TraceProcess process = traceLog.Processes.GetOrCreateProcess(data.ProcessId, data.TimeStampQPC);
                process.UniversalProcessStop(data);
            };
            universalParser.ProcessMapping += delegate (ProcessMappingTraceData data)
            {
                // TODO: All mappings currently get dumped into a single process because CPU and CSwitch events don't have a process or thread associated with them.
                TraceProcess process = traceLog.Processes.GetOrCreateProcess(data.ProcessID, data.TimeStampQPC);
                TraceModuleFile moduleFile = process.LoadedModules.UniversalMapping(data);

                _mappingIdToProcesses[data.Id] = process;
            };
            universalParser.ProcessSymbol += delegate (ProcessSymbolTraceData data)
            {
                _dynamicSymbols.Add((ProcessSymbolTraceData)data.Clone());
            };
        }

        public void AfterProcess(TraceLog traceLog)
        {
            foreach (var universalProcessSymbol in _dynamicSymbols)
            {
                if (_mappingIdToProcesses.TryGetValue(universalProcessSymbol.MappingId, out TraceProcess process))
                {
                    traceLog.CodeAddresses.AddUniversalDynamicSymbol(universalProcessSymbol, process);
                }
            }
        }
    }
}
