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
        private Dictionary<TraceProcess, ProcessMappingState> _mappingStates = new Dictionary<TraceProcess, ProcessMappingState>();
        private bool _trackRecentStacks;

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
            // Extract system page size for ELF RVA calculations.
            if (source is EventPipeEventSource eventPipeSource)
            {
                _trackRecentStacks = true;
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

                ProcessMappingState state = GetOrCreateMappingState(process);
                MappingGeneration generation = new MappingGeneration(data.Id, data.StartAddress, data.EndAddress);
                if (state.Overlaps(generation))
                {
                    // The address range is being reused.  Code addresses seen so far belong to the older mappings.
                    state.HasOverlappingGenerations = true;
                    SeparateCodeAddressesForNewMapping(traceLog, process, state, generation, data.TimeStampQPC);
                }
                state.Generations.Add(generation);

                string fileName = data.FileName;
                if (!string.IsNullOrEmpty(fileName) && fileName.StartsWith(DotnetJittedCodeMappingName, StringComparison.Ordinal))
                {
                    // Don't create a module for jitted code.
                    // These will be created for each jitted code symbol.
                    return;
                }

                _mappingMetadata.TryGetValue(data.MetadataId, out ProcessMappingMetadataTraceData metadata);
                generation.ModuleFile = process.LoadedModules.UniversalMapping(fileName, data.StartAddress, data.EndAddress, data.TimeStampQPC, metadata);
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
            // In processes where mappings reused address ranges, the remaining code addresses belong to the newest
            // mapping that contains them.
            foreach (KeyValuePair<TraceProcess, ProcessMappingState> processState in _mappingStates)
            {
                ProcessMappingState state = processState.Value;
                if (!state.HasOverlappingGenerations)
                {
                    continue;
                }

                var taken = new List<CodeAddressIndex>();
                for (int i = state.Generations.Count - 1; 0 <= i; --i)
                {
                    MappingGeneration generation = state.Generations[i];
                    taken.Clear();
                    traceLog.CodeAddresses.TakeUnresolvedCodeAddressesInRange(processState.Key, generation.StartAddress, generation.EndAddress, taken);
                    generation.CodeAddresses.AddRange(taken);
                }

                foreach (MappingGeneration generation in state.Generations)
                {
                    generation.CodeAddresses.Sort((x, y) => traceLog.CodeAddresses.Address(x).CompareTo(traceLog.CodeAddresses.Address(y)));
                }
            }

            foreach (var universalProcessSymbol in _dynamicSymbols)
            {
                if (_mappingIdToProcesses.TryGetValue(universalProcessSymbol.MappingId, out HashSet<TraceProcess> processes))
                {
                    foreach (TraceProcess process in processes)
                    {
                        if (_mappingStates.TryGetValue(process, out ProcessMappingState state) && state.HasOverlappingGenerations)
                        {
                            // Only apply the symbol to code addresses that were seen while its mapping was current.
                            foreach (MappingGeneration generation in state.Generations)
                            {
                                if (generation.Id == universalProcessSymbol.MappingId)
                                {
                                    traceLog.CodeAddresses.AddUniversalDynamicSymbol(universalProcessSymbol, process, generation.CodeAddresses);
                                }
                            }
                        }
                        else
                        {
                            traceLog.CodeAddresses.AddUniversalDynamicSymbol(universalProcessSymbol, process);
                        }
                    }
                }
            }

            // Code addresses without a symbol resolve to the module of their own mapping (e.g. an unload placeholder),
            // not to another mapping that used the same addresses at a different time.
            foreach (ProcessMappingState state in _mappingStates.Values)
            {
                if (!state.HasOverlappingGenerations)
                {
                    continue;
                }

                foreach (MappingGeneration generation in state.Generations)
                {
                    if (generation.ModuleFile == null)
                    {
                        continue;
                    }

                    foreach (CodeAddressIndex codeAddressIndex in generation.CodeAddresses)
                    {
                        if (traceLog.CodeAddresses.MethodIndex(codeAddressIndex) == MethodIndex.Invalid)
                        {
                            traceLog.CodeAddresses.codeAddresses.UnderlyingArray[(int)codeAddressIndex].SetModuleFileIndex(generation.ModuleFile);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Called by TraceLog whenever a stack is associated with an event.
        /// </summary>
        internal void OnStackAddedToEvent(TraceProcess process, EventIndex eventIndex, long timeStampQPC, CallStackIndex callStackIndex)
        {
            if (!_trackRecentStacks || process == null)
            {
                return;
            }

            // Remember the stacks of the events at the latest timestamp, see SeparateCodeAddressesForNewMapping.
            ProcessMappingState state = GetOrCreateMappingState(process);
            if (state.RecentStacksQPC != timeStampQPC)
            {
                state.RecentStacks.Clear();
                state.RecentStacksQPC = timeStampQPC;
            }
            state.RecentStacks.Add(new KeyValuePair<EventIndex, CallStackIndex>(eventIndex, callStackIndex));
        }

        /// <summary>
        /// 'newGeneration' overlaps existing mappings in 'process'.   Hand the code addresses seen so far in its range to the
        /// newest existing mapping that contains them (or none if they were seen in a gap), so that later uses of the same
        /// addresses get new code addresses which resolve against 'newGeneration'.
        /// </summary>
        private static void SeparateCodeAddressesForNewMapping(TraceLog traceLog, TraceProcess process, ProcessMappingState state,
            MappingGeneration newGeneration, long mappingTimeStampQPC)
        {
            var taken = new List<CodeAddressIndex>();
            traceLog.CodeAddresses.TakeUnresolvedCodeAddressesInRange(process, newGeneration.StartAddress, newGeneration.EndAddress, taken);
            if (taken.Count == 0)
            {
                return;
            }

            foreach (CodeAddressIndex codeAddressIndex in taken)
            {
                MappingGeneration owner = state.FindNewestGenerationContaining(traceLog.CodeAddresses.Address(codeAddressIndex));
                owner?.CodeAddresses.Add(codeAddressIndex);
            }

            // Events at the same timestamp as the mapping belong to it, but may have been written before it.
            // Move their stacks to new code addresses so they resolve against the new mapping.
            if (state.RecentStacks.Count != 0 && mappingTimeStampQPC <= state.RecentStacksQPC)
            {
                var takenSet = new HashSet<CodeAddressIndex>(taken);
                for (int i = 0; i < state.RecentStacks.Count; i++)
                {
                    KeyValuePair<EventIndex, CallStackIndex> recent = state.RecentStacks[i];
                    CallStackIndex newStack = traceLog.CallStacks.ReplaceTakenCodeAddresses(recent.Value, process, takenSet);
                    if (newStack != recent.Value)
                    {
                        traceLog.ReplaceStackForEvent(recent.Key, newStack);
                        state.RecentStacks[i] = new KeyValuePair<EventIndex, CallStackIndex>(recent.Key, newStack);
                    }
                }
            }
        }

        private ProcessMappingState GetOrCreateMappingState(TraceProcess process)
        {
            if (!_mappingStates.TryGetValue(process, out ProcessMappingState state))
            {
                state = new ProcessMappingState();
                _mappingStates[process] = state;
            }
            return state;
        }

        /// <summary>
        /// One ProcessMapping event in a process.  The same address range can be mapped multiple times over the
        /// lifetime of a process (e.g. BPF programs are loaded, unloaded and replaced), and each mapping only
        /// applies to the code addresses that were seen while it was the current mapping for the address.
        /// </summary>
        private sealed class MappingGeneration
        {
            public MappingGeneration(ulong id, ulong startAddress, ulong endAddress)
            {
                Id = id;
                StartAddress = startAddress;
                EndAddress = endAddress;
            }

            public bool Contains(ulong address) { return StartAddress <= address && address <= EndAddress; }

            public readonly ulong Id;
            public readonly ulong StartAddress;
            public readonly ulong EndAddress;               // Inclusive
            public TraceModuleFile ModuleFile;              // Null for jitted code mappings.
            public readonly List<CodeAddressIndex> CodeAddresses = new List<CodeAddressIndex>();     // Only used when HasOverlappingGenerations.
        }

        private sealed class ProcessMappingState
        {
            public bool Overlaps(MappingGeneration newGeneration)
            {
                foreach (MappingGeneration generation in Generations)
                {
                    if (generation.StartAddress <= newGeneration.EndAddress && newGeneration.StartAddress <= generation.EndAddress)
                    {
                        return true;
                    }
                }
                return false;
            }

            public MappingGeneration FindNewestGenerationContaining(ulong address)
            {
                for (int i = Generations.Count - 1; 0 <= i; --i)
                {
                    if (Generations[i].Contains(address))
                    {
                        return Generations[i];
                    }
                }
                return null;
            }

            public readonly List<MappingGeneration> Generations = new List<MappingGeneration>();     // In event order.
            public bool HasOverlappingGenerations;                                                    // True if any address range was mapped more than once.
            public long RecentStacksQPC = long.MinValue;
            public readonly List<KeyValuePair<EventIndex, CallStackIndex>> RecentStacks = new List<KeyValuePair<EventIndex, CallStackIndex>>();    // Stacks of events at RecentStacksQPC.
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
