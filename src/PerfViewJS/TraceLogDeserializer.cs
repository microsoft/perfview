// <copyright file="TraceLogDeserializer.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace PerfViewJS
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Text.RegularExpressions;
    using Microsoft.Diagnostics.Symbols;
    using Microsoft.Diagnostics.Tracing;
    using Microsoft.Diagnostics.Tracing.Etlx;
    using Microsoft.Diagnostics.Tracing.Stacks;

    public sealed class TraceLogDeserializer
    {
        private readonly TraceLog traceLog;

        private readonly Dictionary<EtwProviderInfo, int> internalEventMapping = new Dictionary<EtwProviderInfo, int>();

        public TraceLogDeserializer(string etlFileNameWithTimeStamps)
        {
            var endTimeStart = etlFileNameWithTimeStamps.LastIndexOf('*');
            var e = etlFileNameWithTimeStamps.Substring(endTimeStart + 1, etlFileNameWithTimeStamps.Length - (endTimeStart + 1));
            var startTimeStart = etlFileNameWithTimeStamps.LastIndexOf('*', endTimeStart - 1);
            var s = etlFileNameWithTimeStamps.Substring(startTimeStart + 1, endTimeStart - (startTimeStart + 1));
            var etlFileName = etlFileNameWithTimeStamps.Substring(0, startTimeStart);

            if (!DateTime.TryParse(s, out var startTime))
            {
                startTime = DateTime.MinValue;
            }

            if (!DateTime.TryParse(e, out var endTime))
            {
                endTime = DateTime.MaxValue;
            }

            var etlxPath = etlFileName + "." + startTime.ToString("yyyyddMHHmmss") + "-" + endTime.ToString("yyyyddMHHmmss") + ".etlx";
            if (!File.Exists(etlxPath))
            {
                var tmp = etlxPath + ".new";
                try
                {
                    TraceLog.CreateFromEventTraceLogFile(etlFileName, tmp, new TraceLogOptions { MaxEventCount = int.MaxValue, KeepAllEvents = true }, new TraceEventDispatcherOptions { StartTime = startTime, EndTime = endTime });
                    File.Move(tmp, etlxPath);
                }
                finally
                {
                    if (File.Exists(tmp))
                    {
                        File.Delete(tmp);
                    }
                }
            }

            this.traceLog = new TraceLog(etlxPath);
            this.EventStats = new Dictionary<int, TraceEventCounts>(this.traceLog.Stats.Count);
            this.TraceProcesses = this.traceLog.Processes;
            this.TraceModuleFiles = this.traceLog.ModuleFiles;
            this.TraceInfo = new TraceInfo(this.traceLog.MachineName, this.traceLog.OSName, this.traceLog.OSBuild, this.traceLog.UTCOffsetMinutes, TimeZoneInfo.Local.BaseUtcOffset, this.traceLog.BootTime, this.traceLog.SessionStartTime, this.traceLog.SessionEndTime, this.traceLog.SessionEndTimeRelativeMSec, this.traceLog.SessionDuration, this.traceLog.CpuSpeedMHz, this.traceLog.NumberOfProcessors, this.traceLog.MemorySizeMeg, this.traceLog.PointerSize, this.traceLog.SampleProfileInterval, this.traceLog.EventCount, this.traceLog.EventsLost, this.traceLog.Size);

            int i = 1;
            this.internalEventMapping.Add(new EtwProviderInfo(new Guid("{9e814aad-3204-11d2-9a82-006008a86939}"), 0), 0); // for EventTrace Header
            foreach (var eventStat in this.traceLog.Stats)
            {
                this.EventStats.Add(i, eventStat);
                this.internalEventMapping.Add(new EtwProviderInfo(eventStat.IsClassic ? eventStat.TaskGuid : eventStat.ProviderGuid, eventStat.IsClassic ? (int)eventStat.Opcode : (int)eventStat.EventID), i);
                this.TotalStackCount += eventStat.StackCount;
                this.TotalEventCount += eventStat.Count;
                i++;
            }
        }

        public int TotalEventCount { get; }

        public int TotalStackCount { get; }

        public Dictionary<int, TraceEventCounts> EventStats { get; }

        public TraceProcesses TraceProcesses { get; }

        public TraceModuleFiles TraceModuleFiles { get; }

        public TraceInfo TraceInfo { get; }

        public StackSource GetStackSource(ProcessIndex processIndex, int stackType, double start, double end)
        {
            var sessionEndMsec = this.traceLog.SessionEndTimeRelativeMSec;
            var events = this.traceLog.Events.FilterByTime(start, Math.Abs(end) < 0.006 ? sessionEndMsec : (end > sessionEndMsec ? sessionEndMsec : end));

            TraceProcess process = null;
            if (processIndex != ProcessIndex.Invalid)
            {
                process = this.TraceProcesses[processIndex];
            }

            if (this.EventStats.TryGetValue(stackType, out var value))
            {
                if (value.IsClassic)
                {
                    if (value.TaskGuid == new Guid("{ce1dbfb4-137e-4da6-87b0-3f59aa102cbc}"))
                    {
                        return events.CPUStacks(process);
                    }

                    return events.SingleEventTypeStack(process, @event => @event.TaskGuid == value.TaskGuid && @event.Opcode == value.Opcode);
                }

                if (!value.IsClassic)
                {
                    if (value.ProviderGuid == new Guid("{e13c0d23-ccbc-4e12-931b-d9cc2eee27e4}") && value.EventID == (TraceEventID)80)
                    {
                        return events.Exceptions(process);
                    }

                    return events.SingleEventTypeStack(process, @event => @event.ProviderGuid == value.ProviderGuid && @event.ID == value.EventID);
                }
            }

            return events.AnyStacks(process);
        }

        public List<EventData> GetEvents(HashSet<int> eventTypes, string textFilter, int maxEventCount, double start, double end)
        {
            var returnEvents = new List<EventData>(maxEventCount);
            int i = 0;
            var events = this.traceLog.Events.FilterByTime(start, Math.Abs(end) < 0.006 ? this.traceLog.SessionEndTimeRelativeMSec : end);
            var regex = new Regex(textFilter, RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

            foreach (var @event in events)
            {
                if (i < maxEventCount)
                {
                    Guid providerGuid = @event.IsClassicProvider ? @event.TaskGuid : @event.ProviderGuid;
                    if (!this.internalEventMapping.TryGetValue(new EtwProviderInfo(providerGuid, @event.IsClassicProvider ? (int)@event.Opcode : (int)@event.ID), out var mapId))
                    {
                        continue;
                    }

                    if (eventTypes.Count < 1 || eventTypes.Contains(mapId))
                    {
                        var eventData = new EventData
                        {
                            Timestamp = @event.TimeStampRelativeMSec.ToString("F3"),
                            EventName = @event.ProviderName + "/" + @event.EventName,
                            ProcessName = @event.ProcessName + $" ({@event.ProcessID})",
                            EventIndex = (int)@event.EventIndex,
                        };

                        var sb = new StringBuilder();

                        if (@event.CallStackIndex() != CallStackIndex.Invalid)
                        {
                            eventData.HasStack = true;
                        }

                        sb.Append("ThreadID=\"");
                        sb.Append(@event.ThreadID);
                        sb.Append("\" ");

                        sb.Append("ProcessorNumber=\"");
                        sb.Append(@event.ProcessorNumber);
                        sb.Append("\" ");

                        if (@event.ActivityID != Guid.Empty)
                        {
                            sb.Append("ActivityID=\"");
                            sb.Append(@event.ActivityID.ToString());
                            sb.Append("\" ");
                        }

                        if (@event.RelatedActivityID != Guid.Empty)
                        {
                            sb.Append("RelatedActivityID=\"");
                            sb.Append(@event.RelatedActivityID.ToString());
                            sb.Append("\" ");
                        }

                        var payloadNames = @event.PayloadNames;
                        if (payloadNames.Length == 0 && @event.EventDataLength != 0)
                        {
                            eventData.Rest = "DataLength=\"" + @event.EventDataLength.ToString() + "\"";
                        }
                        else
                        {
                            try
                            {
                                for (int j = 0; j < payloadNames.Length; j++)
                                {
                                    sb.Append(payloadNames[j]);
                                    sb.Append("=\"");
                                    sb.Append(@event.PayloadString(j));
                                    sb.Append("\" ");
                                }

                                eventData.Rest = sb.ToString();
                            }
                            catch (Exception)
                            {
                                eventData.Rest = "Error Parsing Field. DataLength=\"" + @event.EventDataLength.ToString() + "\"";
                            }
                        }

                        var compareInfo = CultureInfo.InvariantCulture.CompareInfo;
                        if (compareInfo.IndexOf(eventData.EventName, textFilter, CompareOptions.IgnoreCase) >= 0 || compareInfo.IndexOf(eventData.ProcessName, textFilter, CompareOptions.IgnoreCase) >= 0 || compareInfo.IndexOf(eventData.Rest, textFilter, CompareOptions.IgnoreCase) >= 0 || regex.IsMatch(eventData.Rest))
                        {
                            returnEvents.Add(eventData);
                            i++;
                        }
                    }
                }
                else
                {
                    break;
                }
            }

            return returnEvents;
        }

        public DetailedProcessInfo GetDetailedProcessInfo(int processIndex)
        {
            var traceProcesses = this.traceLog.Processes;
            if (processIndex > traceProcesses.Count)
            {
                throw new ArgumentException();
            }

            var traceProcess = traceProcesses[(ProcessIndex)processIndex];

            var threadList = new List<ThreadInfo>();
            foreach (var thread in traceProcess.Threads)
            {
                threadList.Add(new ThreadInfo(thread.ThreadID, (int)thread.ThreadIndex, thread.CPUMSec, thread.StartTime, thread.StartTimeRelativeMSec, thread.EndTime, thread.EndTimeRelativeMSec));
            }

            var moduleList = new List<ModuleInfo>();
            foreach (var loadedModule in traceProcess.LoadedModules)
            {
                var moduleFile = loadedModule.ModuleFile;
                moduleList.Add(new ModuleInfo((int)moduleFile.ModuleFileIndex, moduleFile.CodeAddressesInModule, moduleFile.FilePath));
            }

            moduleList.Sort();

            var processInfo = new ProcessInfo(traceProcess.Name + $" ({traceProcess.ProcessID})", (int)traceProcess.ProcessIndex, traceProcess.CPUMSec, traceProcess.ProcessID, traceProcess.ParentID, traceProcess.CommandLine);

            return new DetailedProcessInfo(processInfo, threadList, moduleList);
        }

        public string LookupSymbol(int moduleIndex)
        {
            var moduleFiles = this.traceLog.ModuleFiles;
            if (moduleIndex > moduleFiles.Count)
            {
                return $"ModuleIndex ({moduleIndex}) is larger than possible. It is invalid.";
            }

            var moduleFile = moduleFiles[(ModuleFileIndex)moduleIndex];
            if (moduleFile != null)
            {
                var writer = new StringWriter();
                using (var symbolReader = new SymbolReader(writer))
                {
                    this.traceLog.CallStacks.CodeAddresses.LookupSymbolsForModule(symbolReader, moduleFile);
                }

                return writer.ToString();
            }

            return $"ModuleIndex ({moduleIndex}) is invalid.";
        }

        public string LookupSymbols(int[] moduleIndices)
        {
            var moduleFiles = this.traceLog.ModuleFiles;
            var writer = new StringWriter();
            using (var symbolReader = new SymbolReader(writer))
            {
                foreach (var moduleIndex in moduleIndices)
                {
                    if (moduleIndex > moduleFiles.Count)
                    {
                        return $"ModuleIndex ({moduleIndex}) is larger than possible. It is invalid.";
                    }

                    var moduleFile = moduleFiles[(ModuleFileIndex)moduleIndex];
                    if (moduleFile != null)
                    {
                        this.traceLog.CallStacks.CodeAddresses.LookupSymbolsForModule(symbolReader, moduleFile);
                    }
                }
            }

            return writer.ToString();
        }

        private struct EtwProviderInfo : IEquatable<EtwProviderInfo>
        {
            private readonly Guid providerId;

            private readonly int eventId;

            public EtwProviderInfo(Guid providerId, int eventId)
            {
                this.providerId = providerId;
                this.eventId = eventId;
            }

            public bool Equals(EtwProviderInfo other)
            {
                return this.providerId == other.providerId && this.eventId == other.eventId;
            }

            public override int GetHashCode()
            {
                int hash = 17;
                hash = (hash * 31) + this.providerId.GetHashCode();
                hash = (hash * 31) + this.eventId.GetHashCode();
                return hash;
            }

            public override bool Equals(object other)
            {
                if (other is EtwProviderInfo info)
                {
                    return this.Equals(info);
                }

                return false;
            }
        }
    }
}
