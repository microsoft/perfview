using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Universal.Events;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Microsoft.Diagnostics.Tracing.SourceConverters
{
    internal sealed class NettraceUniversalConverter
    {
        private const string DotnetJittedCodeMappingName = "/memfd:doublemapper";

        private List<ProcessSymbolTraceData> _dynamicSymbols = new List<ProcessSymbolTraceData>();
        private Dictionary<ulong, HashSet<TraceProcess>> _mappingIdToProcesses = new Dictionary<ulong, HashSet<TraceProcess>>();
        private Dictionary<ulong, ProcessMappingMetadataTraceData> _mappingMetadata = new Dictionary<ulong, ProcessMappingMetadataTraceData>();

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
            // Extract EventPipe-specific header values (e.g., SystemPageSize for ELF RVA calculations).
            if (source is EventPipeEventSource eventPipeSource)
            {
                eventPipeSource.HeadersDeserialized += delegate ()
                {
                    traceLog.systemPageSize = eventPipeSource._systemPageSize;
                };
            }

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
                if (!_mappingIdToProcesses.TryGetValue(data.Id, out HashSet<TraceProcess> processes))
                {
                    processes = new HashSet<TraceProcess>();
                    _mappingIdToProcesses[data.Id] = processes;
                }
                processes.Add(process);

                if (!string.IsNullOrEmpty(data.FileName) && data.FileName.StartsWith(DotnetJittedCodeMappingName, StringComparison.Ordinal))
                {
                    // Don't create a module for jitted code.
                    // These will be created for each jitted code symbol.
                    return;
                }

                _mappingMetadata.TryGetValue(data.MetadataId, out ProcessMappingMetadataTraceData metadata);
                TraceModuleFile moduleFile = process.LoadedModules.UniversalMapping(data, metadata);
            };
            universalSystemParser.ProcessMappingMetadata += delegate (ProcessMappingMetadataTraceData data)
            {
                _mappingMetadata[data.Id] = (ProcessMappingMetadataTraceData)data.Clone();
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
                if (_mappingIdToProcesses.TryGetValue(universalProcessSymbol.MappingId, out HashSet<TraceProcess> processes))
                {
                    foreach (TraceProcess process in processes)
                    {
                        traceLog.CodeAddresses.AddUniversalDynamicSymbol(universalProcessSymbol, process);
                    }
                }
            }
        }

        /// <summary>
        /// Regular expression for parsing dotnet jitted symbol names from universal traces.
        /// Format: "returnType [module] Namespace.Class::Method(args...)[OptimizationLevel]"
        /// The return type can be multi-word (e.g., "instance void", "valuetype [Type]Type").
        /// </summary>
        private static readonly Regex s_jittedSymbolRegex =
            new Regex(@"^(?<returnType>.+?)\s+\[(?<module>[^\]]+)\]\s+(?<methodSignature>.+?)\[(?<optimizationLevel>[^\]]+)\]$",
                RegexOptions.Compiled);

        /// <summary>
        /// Parses a dotnet jitted symbol name from universal traces with format: "returnType [module] Namespace.Class::Method(args...)[OptimizationLevel]"
        /// and returns the module name and method signature.
        /// </summary>
        internal static (string moduleName, string methodSignature)? ParseDotnetJittedSymbolName(string symbolName)
        {
            if (!string.IsNullOrEmpty(symbolName))
            {
                var match = s_jittedSymbolRegex.Match(symbolName);

                if (match.Success)
                {
                    string module = match.Groups["module"].Value;
                    string methodSignature = match.Groups["methodSignature"].Value.Trim();
                    return (module, methodSignature);
                }
            }

            return null;
        }
    }
}
