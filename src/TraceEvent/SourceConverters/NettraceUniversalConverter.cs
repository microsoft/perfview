using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Universal.Events;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Tracing.SourceConverters
{
    internal sealed class NettraceUniversalConverter
    {
        private List<ProcessSymbolTraceData> _dynamicSymbols = new List<ProcessSymbolTraceData>();
        private Dictionary<ulong, TraceProcess> _mappingIdToProcesses = new Dictionary<ulong, TraceProcess>();

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
            UniversalSystemTraceEventParser universalSystemParser = new UniversalSystemTraceEventParser(source);
            universalSystemParser.ExistingProcess += delegate (ProcessCreateTraceData data)
            {
                TraceProcess process = traceLog.Processes.GetOrCreateProcess(data.ProcessID, data.TimeStampQPC);
                process.UniversalProcessStart(data);
            };
            universalSystemParser.ProcessCreate += delegate (ProcessCreateTraceData data)
            {
                TraceProcess process = traceLog.Processes.GetOrCreateProcess(data.ProcessID, data.TimeStampQPC, isProcessStartEvent: true);
                process.UniversalProcessStart(data);
            };
            universalSystemParser.ProcessExit += delegate (EmptyTraceData data)
            {
                TraceProcess process = traceLog.Processes.GetOrCreateProcess(data.ProcessID, data.TimeStampQPC);
                process.UniversalProcessStop(data);
            };
            universalSystemParser.ProcessMapping += delegate (ProcessMappingTraceData data)
            {
                TraceProcess process = traceLog.Processes.GetOrCreateProcess(data.ProcessID, data.TimeStampQPC);
                TraceModuleFile moduleFile = process.LoadedModules.UniversalMapping(data);

                _mappingIdToProcesses[data.Id] = process;
            };
            universalSystemParser.ProcessSymbol += delegate (ProcessSymbolTraceData data)
            {
                _dynamicSymbols.Add((ProcessSymbolTraceData)data.Clone());
            };

            UniversalEventsTraceEventParser universalEventsParser = new UniversalEventsTraceEventParser(source);
            universalEventsParser.cpu += delegate(SampleTraceData data)
            {
                TraceProcess process = traceLog.Processes.GetOrCreateProcess(data.ProcessID, data.TimeStampQPC);
                TraceThread thread = traceLog.Threads.GetOrCreateThread(data.ThreadID, data.TimeStampQPC, process);
                thread.cpuSamples++;
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
