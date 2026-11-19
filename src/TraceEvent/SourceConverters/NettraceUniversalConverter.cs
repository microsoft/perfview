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

        private List<KeyValuePair<TraceProcess, ProcessSymbolTraceData>> _dynamicSymbols = new List<KeyValuePair<TraceProcess, ProcessSymbolTraceData>>();
        private Dictionary<ulong, ProcessMappingMetadataTraceData> _mappingMetadata = new Dictionary<ulong, ProcessMappingMetadataTraceData>();
        private Dictionary<TraceProcess, ProcessMappingState> _mappingStates = new Dictionary<TraceProcess, ProcessMappingState>();

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
                ProcessMappingState state = GetOrCreateMappingState(process);
                // Exit follows samples at its timestamp, unlike a mapping transition.
                foreach (MappingGeneration generation in state.Generations)
                {
                    foreach (KeyValuePair<ulong, ulong> range in generation.LiveRanges)
                    {
                        SeparateCodeAddressesForMappingChange(traceLog, process, state, range.Key, range.Value, data.TimeStampQPC, false);
                    }
                    generation.LiveRanges.Clear();
                }
                process.UniversalProcessStop(data);
            };
            universalSystemParser.ProcessMapping += delegate (ProcessMappingTraceData data)
            {
                TraceProcess process = traceLog.Processes.GetOrCreateProcess(data.ProcessID, data.TimeStampQPC);
                ProcessMappingState state = GetOrCreateMappingState(process);
                MappingGeneration generation = new MappingGeneration(data.StartAddress, data.EndAddress);

                // Code addresses seen so far in the range were seen before this mapping was loaded, so they belong to
                // older mappings, or to no mapping at all (an unknown gap, e.g. before the first load).
                SeparateCodeAddressesForMappingChange(traceLog, process, state, data.StartAddress, data.EndAddress, data.TimeStampQPC);
                state.Generations.Add(generation);
                // Ids are unique per process instance.
                state.GenerationsById[data.Id] = generation;

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
            universalSystemParser.ProcessMappingUnload += delegate (ProcessMappingUnloadTraceData data)
            {
                TraceProcess process = traceLog.Processes.GetOrCreateProcess(data.ProcessID, data.TimeStampQPC);
                if (!_mappingStates.TryGetValue(process, out ProcessMappingState state) ||
                    !state.GenerationsById.TryGetValue(data.Id, out MappingGeneration generation))
                {
                    return;
                }
                // Preserve the old ownership before removing any live pieces. Other mappings,
                // including overlays, remain live and are not unloaded by this event.
                foreach (KeyValuePair<ulong, ulong> range in generation.LiveRanges)
                {
                    ulong start = Math.Max(range.Key, data.StartAddress);
                    ulong end = Math.Min(range.Value, data.EndAddress);
                    if (start <= end)
                    {
                        SeparateCodeAddressesForMappingChange(traceLog, process, state, start, end, data.TimeStampQPC);
                    }
                }
                generation.Unload(data.StartAddress, data.EndAddress);
            };
            universalSystemParser.ProcessMappingMetadata += delegate (ProcessMappingMetadataTraceData data)
            {
                _mappingMetadata[data.Id] = (ProcessMappingMetadataTraceData)data.Clone();
            };
            universalSystemParser.ProcessSymbol += delegate (ProcessSymbolTraceData data)
            {
                TraceProcess process = traceLog.Processes.GetOrCreateProcess(data.ProcessID, data.TimeStampQPC);
                _dynamicSymbols.Add(new KeyValuePair<TraceProcess, ProcessSymbolTraceData>(process, (ProcessSymbolTraceData)data.Clone()));
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
            // The remaining code addresses belong to the newest still-live mapping that contains them.
            foreach (KeyValuePair<TraceProcess, ProcessMappingState> processState in _mappingStates)
            {
                ProcessMappingState state = processState.Value;
                if (state.Generations.Count == 0)
                {
                    continue;
                }
                var taken = new List<CodeAddressIndex>();
                for (int i = state.Generations.Count - 1; 0 <= i; --i)
                {
                    MappingGeneration generation = state.Generations[i];
                    foreach (KeyValuePair<ulong, ulong> range in generation.LiveRanges)
                    {
                        taken.Clear();
                        traceLog.CodeAddresses.TakeUnresolvedCodeAddressesInRange(processState.Key, range.Key, range.Value, taken);
                        generation.CodeAddresses.AddRange(taken);
                    }
                }

                // Do not let TraceLog's final module fallback attach unmapped samples to an unloaded module.
                taken.Clear();
                traceLog.CodeAddresses.TakeUnresolvedCodeAddressesInRange(processState.Key, 0, ulong.MaxValue, taken);
                foreach (MappingGeneration generation in state.Generations)
                {
                    generation.CodeAddresses.Sort((x, y) => traceLog.CodeAddresses.Address(x).CompareTo(traceLog.CodeAddresses.Address(y)));
                }
            }

            foreach (KeyValuePair<TraceProcess, ProcessSymbolTraceData> processSymbol in _dynamicSymbols)
            {
                ProcessSymbolTraceData symbol = processSymbol.Value;
                if (_mappingStates.TryGetValue(processSymbol.Key, out ProcessMappingState state) &&
                    state.GenerationsById.TryGetValue(symbol.MappingId, out MappingGeneration generation))
                {
                    traceLog.CodeAddresses.AddUniversalDynamicSymbol(symbol, processSymbol.Key, generation.CodeAddresses, generation.ModuleFile);
                }
            }

            // Code addresses without a symbol resolve to the module of their own mapping (e.g. an unload placeholder),
            // not to another mapping that used the same addresses at a different time.
            foreach (ProcessMappingState state in _mappingStates.Values)
            {
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
            if (process == null)
            {
                return;
            }

            // Remember the stacks of the events at the latest timestamp, see SeparateCodeAddressesForMappingChange.
            ProcessMappingState state = GetOrCreateMappingState(process);
            if (state.RecentStacksQPC != timeStampQPC)
            {
                state.RecentStacks.Clear();
                state.RecentStacksQPC = timeStampQPC;
            }
            state.RecentStacks.Add(new KeyValuePair<EventIndex, CallStackIndex>(eventIndex, callStackIndex));
        }

        internal void ClearRecentStacks()
        {
            foreach (ProcessMappingState state in _mappingStates.Values)
            {
                state.RecentStacks.Clear();
                state.RecentStacksQPC = long.MinValue;
            }
        }

        /// <summary>
        /// Preserve the ownership of samples seen before a mapping change. Later samples get fresh code addresses,
        /// allowing overlays and partial unloads to change ownership independently at each address.
        /// </summary>
        private static void SeparateCodeAddressesForMappingChange(TraceLog traceLog, TraceProcess process, ProcessMappingState state,
            ulong startAddress, ulong endAddress, long mappingTimeStampQPC, bool moveSameTimeSamples = true)
        {
            var taken = new List<CodeAddressIndex>();
            traceLog.CodeAddresses.TakeUnresolvedCodeAddressesInRange(process, startAddress, endAddress, taken);
            if (taken.Count == 0)
            {
                return;
            }

            foreach (CodeAddressIndex codeAddressIndex in taken)
            {
                MappingGeneration owner = state.FindNewestGenerationContaining(traceLog.CodeAddresses.Address(codeAddressIndex));
                owner?.CodeAddresses.Add(codeAddressIndex);
            }

            // A mapping change governs samples at its timestamp, even if they were written before it.
            // Move their stacks to fresh code addresses so they resolve against the updated live mappings.
            if (moveSameTimeSamples && state.RecentStacks.Count != 0 && mappingTimeStampQPC <= state.RecentStacksQPC)
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
            public MappingGeneration(ulong startAddress, ulong endAddress)
            {
                if (startAddress <= endAddress)
                {
                    LiveRanges.Add(new KeyValuePair<ulong, ulong>(startAddress, endAddress));
                }
            }

            public bool Contains(ulong address)
            {
                foreach (KeyValuePair<ulong, ulong> range in LiveRanges)
                {
                    if (range.Key <= address && address <= range.Value)
                    {
                        return true;
                    }
                }
                return false;
            }

            public void Unload(ulong start, ulong end)
            {
                if (end < start)
                {
                    return;
                }
                for (int i = LiveRanges.Count - 1; i >= 0; --i)
                {
                    KeyValuePair<ulong, ulong> range = LiveRanges[i];
                    if (end < range.Key || range.Value < start)
                    {
                        continue;
                    }
                    LiveRanges.RemoveAt(i);
                    if (end < range.Value)
                    {
                        LiveRanges.Insert(i, new KeyValuePair<ulong, ulong>(end + 1, range.Value));
                    }
                    if (range.Key < start)
                    {
                        LiveRanges.Insert(i, new KeyValuePair<ulong, ulong>(range.Key, start - 1));
                    }
                }
            }

            public TraceModuleFile ModuleFile;              // Null for jitted code mappings.
            public readonly List<CodeAddressIndex> CodeAddresses = new List<CodeAddressIndex>();
            public readonly List<KeyValuePair<ulong, ulong>> LiveRanges = new List<KeyValuePair<ulong, ulong>>();
        }

        private sealed class ProcessMappingState
        {
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
            public readonly Dictionary<ulong, MappingGeneration> GenerationsById = new Dictionary<ulong, MappingGeneration>();
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
