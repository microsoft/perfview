// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PerfViewJS
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq.Expressions;
    using System.Text;
    using System.Text.RegularExpressions;
    using Microsoft.Diagnostics.Tracing;
    using Microsoft.Diagnostics.Tracing.Etlx;

    public sealed class TraceLogDeserializer
    {
        private readonly TraceLog traceLog;

        private static readonly Func<TraceEvent, Guid> TaskGuidFetcher = SetupTaskGuidFetcher();

        private readonly Dictionary<EtwProviderInfo, int> internalEventMapping = new Dictionary<EtwProviderInfo, int>();

        private static Func<TraceEvent, Guid> SetupTaskGuidFetcher()
        {
            var param = Expression.Parameter(typeof(TraceEvent), "arg");
            var member = Expression.Field(param, "taskGuid");
            LambdaExpression lambda = Expression.Lambda(typeof(Func<TraceEvent, Guid>), member, param);
            return (Func<TraceEvent, Guid>)lambda.Compile();
        }

        public TraceLogDeserializer(string etlFileName)
        {
            this.traceLog = new TraceLog(TraceLog.CreateFromEventTraceLogFile(etlFileName));
            this.EventStats = new Dictionary<int, TraceEventCounts>(this.traceLog.Stats.Count);
            this.TraceProcesses = this.traceLog.Processes;

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

        public GenericStackSource GetStackSource(ProcessIndex processIndex, int stackType)
        {
            TraceProcess process = null;
            if (processIndex != ProcessIndex.Invalid)
            {
                process = this.TraceProcesses[processIndex];
            }

            if (this.EventStats.TryGetValue(stackType, out var value))
            {
                if (value.IsClassic && value.TaskGuid == new Guid("{ce1dbfb4-137e-4da6-87b0-3f59aa102cbc}"))
                {
                    return new SourceAwareStackSource(this.traceLog.CPUStacks(process));
                }

                if (!value.IsClassic && value.ProviderGuid == new Guid("{e13c0d23-ccbc-4e12-931b-d9cc2eee27e4}") && value.EventID == (TraceEventID)80)
                {
                    return new SourceAwareStackSource(this.traceLog.Exceptions(process));
                }
            }

            return new SourceAwareStackSource(this.traceLog.AnyStacks(process));
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
                    Guid providerGuid = @event.IsClassicProvider ? TaskGuidFetcher(@event) : @event.ProviderGuid;
                    if (!this.internalEventMapping.TryGetValue(new EtwProviderInfo(providerGuid, @event.IsClassicProvider ? (int)@event.Opcode : (int)@event.ID), out var mapId))
                    {
                        continue;
                    }

                    if (eventTypes.Count < 1 || eventTypes.Contains(mapId))
                    {
                        var eventData = new EventData
                        {
                            Timestamp = @event.TimeStampRelativeMSec.ToString("N3"),
                            EventName = @event.ProviderName + "/" + @event.EventName,
                            ProcessName = @event.ProcessName + $" ({@event.ProcessID})",
                            EventIndex = (int)@event.EventIndex
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

        private struct EtwProviderInfo : IEquatable<EtwProviderInfo>
        {
            public readonly Guid providerId;

            public readonly int eventId;

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
                return this.Equals((EtwProviderInfo)other);
            }
        }
    }
}
