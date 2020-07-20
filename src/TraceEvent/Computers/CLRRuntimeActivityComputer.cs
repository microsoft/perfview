// Copyright (c) Microsoft Corporation.  All rights reserved
// This file is best viewed using outline mode (Ctrl-M Ctrl-O)
//
// This program uses code hyperlinks available as part of the HyperAddin Visual Studio plug-in.
// It is available from http://www.codeplex.com/hyperAddin 
// using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.ApplicationServer;
using Microsoft.Diagnostics.Tracing.Parsers.AspNet;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Diagnostics.Tracing.Stacks;
using Microsoft.Diagnostics.Tracing.Analysis.JIT;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using StartStopKey = System.Guid;   // The start-stop key is unique in the trace.  We incorperate the process as well as activity ID to achieve this.

namespace Microsoft.Diagnostics.Tracing
{
    public class RuntimeLoaderStats : Dictionary<int, CLRRuntimeActivityComputer.PerThreadStartStopData>
    {
        public TraceLogEventSource EventSource;
    }


    public class CLRRuntimeActivityComputer
    {
        public struct EventUID : IComparable<EventUID>, IEquatable<EventUID>
        {
            public EventUID(TraceEvent evt) : this(evt.EventIndex, evt.TimeStampRelativeMSec) { }

            public EventUID(EventIndex eventId, double time)
            {
                EventId = eventId;
                Time = time;
            }

            public readonly EventIndex EventId;
            public readonly double Time;

            public int CompareTo(EventUID other)
            {
                int timeCompare = Time.CompareTo(other.Time);
                if (timeCompare != 0)
                    return timeCompare;

                return EventId.CompareTo(other.EventId);
            }

            public override int GetHashCode()
            {
                return (int)EventId;
            }

            public override bool Equals(object obj)
            {
                if (!(obj is EventUID))
                    return false;
                return this.CompareTo((EventUID)obj) == 0;
            }

            public bool Equals(EventUID other)
            {
                return this.CompareTo(other) == 0;
            }
        }

        public struct StartStopThreadEventData : IComparable<StartStopThreadEventData>
        {
            public StartStopThreadEventData(EventUID start, EventUID end, string name)
            {
                Start = start;
                End = end;
                Name = name;
                StackDepth = 0;
            }

            public EventUID Start;
            public EventUID End;
            public int StackDepth;
            public string Name;

            int IComparable<StartStopThreadEventData>.CompareTo(StartStopThreadEventData other)
            {
                return Start.CompareTo(other.Start);
            }
        }


        struct IdOfIncompleteAction : IEquatable<IdOfIncompleteAction>
        {
            public long Identifier;
            public int ThreadID;

            public override int GetHashCode()
            {
                return Identifier.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                if (!(obj is IdOfIncompleteAction))
                    return false;

                return Equals((IdOfIncompleteAction)obj);
            }

            public bool Equals(IdOfIncompleteAction other)
            {
                return (Identifier == other.Identifier) && (ThreadID == other.ThreadID);
            }
        }

        struct IncompleteActionDesc
        {
            public EventUID Start;
            public string OperationType;
            public string Name;
        }

        public class PerThreadStartStopData
        {
            public int Offset;

            public StartStopThreadEventData[] Data;
            public StartStopThreadEventData[] SplitUpData;
            public double[] SplitUpDataStarts;
        }

        RuntimeLoaderStats _startStopData = new RuntimeLoaderStats();
        public RuntimeLoaderStats StartStopData => _startStopData;

        Dictionary<IdOfIncompleteAction, IncompleteActionDesc> _incompleteJitEvents = new Dictionary<IdOfIncompleteAction, IncompleteActionDesc>();
        Dictionary<IdOfIncompleteAction, IncompleteActionDesc> _incompleteR2REvents = new Dictionary<IdOfIncompleteAction, IncompleteActionDesc>();
        Dictionary<IdOfIncompleteAction, IncompleteActionDesc> _incompleteTypeLoadEvents = new Dictionary<IdOfIncompleteAction, IncompleteActionDesc>();

        public Dictionary<int, List<StartStopThreadEventData>> StartStopEvents { get; } = new Dictionary<int, List<StartStopThreadEventData>>();

        public CLRRuntimeActivityComputer(TraceLogEventSource source)
        {
            _startStopData.EventSource = source;
            source.Clr.MethodJittingStarted += Clr_MethodJittingStarted;
            source.Clr.MethodR2RGetEntryPoint += Clr_MethodR2RGetEntryPoint;
            source.Clr.MethodLoadVerbose += Clr_MethodLoadVerbose;
            source.Clr.MethodLoad += Clr_MethodLoad;
            source.Clr.LoaderAssemblyLoad += Clr_LoaderAssemblyLoad;
            source.Clr.MethodR2RGetEntryPointStarted += Clr_R2RGetEntryPointStarted;
            source.Clr.TypeLoadStart += Clr_TypeLoadStart;
            source.Clr.TypeLoadStop += Clr_TypeLoadStop;
            source.Process();
            source.Clr.MethodJittingStarted -= Clr_MethodJittingStarted;
            source.Clr.MethodR2RGetEntryPointStarted -= Clr_R2RGetEntryPointStarted;
            source.Clr.MethodR2RGetEntryPoint -= Clr_MethodR2RGetEntryPoint;
            source.Clr.MethodLoadVerbose -= Clr_MethodLoadVerbose;
            source.Clr.MethodLoad -= Clr_MethodLoad;
            source.Clr.LoaderAssemblyLoad -= Clr_LoaderAssemblyLoad;
            source.Clr.TypeLoadStart -= Clr_TypeLoadStart;
            source.Clr.TypeLoadStop -= Clr_TypeLoadStop;

            HashSet<EventUID> interestingEvents = new HashSet<EventUID>();
            foreach (var entry in StartStopEvents)
            {
                StartStopThreadEventData[] data = entry.Value.ToArray();
                Array.Sort(data);
                PerThreadStartStopData perThread = new PerThreadStartStopData();
                perThread.Data = data;
                for (int i = 0; i < data.Length; i++)
                {
                    interestingEvents.Add(data[i].Start);
                    interestingEvents.Add(data[i].End);
                }
                _startStopData.Add(entry.Key, perThread);
            }

            foreach (var entry in _startStopData)
            {
                List<StartStopThreadEventData> splitUpStartStopData = new List<StartStopThreadEventData>();

                Stack<StartStopThreadEventData> currentPerThreadProcessingState = new Stack<StartStopThreadEventData>();
                for (int i = 0; i < entry.Value.Data.Length; i++)
                {
                    var startStop = entry.Value.Data[i];

                    if (currentPerThreadProcessingState.Count > 0)
                    {
                        while ((currentPerThreadProcessingState.Count > 0) && (currentPerThreadProcessingState.Peek().End.CompareTo(startStop.Start) < 0))
                        {
                            // Current stack top event finished before this event happened.
                            var poppedEvent = currentPerThreadProcessingState.Pop();
                            EventUID lastEventProcessedEnd = poppedEvent.End;
                            if (currentPerThreadProcessingState.Count > 0)
                            {
                                var tempPoppedEvent = currentPerThreadProcessingState.Pop();
                                tempPoppedEvent.Start = lastEventProcessedEnd;
                                splitUpStartStopData.Add(tempPoppedEvent);
                                currentPerThreadProcessingState.Push(tempPoppedEvent);
                            }
                        }
                    }

                    startStop.StackDepth = currentPerThreadProcessingState.Count;
                    splitUpStartStopData.Add(startStop);
                    currentPerThreadProcessingState.Push(startStop);
                }
                entry.Value.SplitUpData = splitUpStartStopData.ToArray();
                entry.Value.SplitUpDataStarts = new double[entry.Value.SplitUpData.Length];
                for (int i = 0; i < entry.Value.SplitUpDataStarts.Length; i++)
                {
                    entry.Value.SplitUpDataStarts[i] = entry.Value.SplitUpData[i].Start.Time;
                }
            }
        }

        private void AddStartStopData(int threadId, EventUID start, EventUID end, string name)
        {
            if (!StartStopEvents.ContainsKey(threadId))
                StartStopEvents[threadId] = new List<StartStopThreadEventData>();

            List<StartStopThreadEventData> startStopData = StartStopEvents[threadId];
            startStopData.Add(new StartStopThreadEventData(start, end, name));
        }

        private void Clr_LoaderAssemblyLoad(AssemblyLoadUnloadTraceData obj)
        {
            // Since we don't have start stop data, simply treat the assembly load event as a point in time so that it is visible in the textual load view
            EventUID eventTime = new EventUID(obj);
            AddStartStopData(obj.ThreadID, eventTime, eventTime, $"ASMLOAD({obj.FullyQualifiedAssemblyName},{obj.AssemblyID})");
        }

        private void Clr_MethodLoad(MethodLoadUnloadTraceData obj)
        {
            MethodJittedEvent(obj, obj.MethodID);
        }

        private void Clr_MethodLoadVerbose(MethodLoadUnloadVerboseTraceData obj)
        {
            MethodJittedEvent(obj, obj.MethodID);
        }

        private void MethodJittedEvent(TraceEvent evt, long methodID)
        {
            IdOfIncompleteAction id = new IdOfIncompleteAction();
            id.Identifier = methodID;
            id.ThreadID = evt.ThreadID;
            if (_incompleteJitEvents.TryGetValue(id, out IncompleteActionDesc jitStartData))
            {
                // JitStart is processed, don't process it again.
                _incompleteJitEvents.Remove(id);

                AddStartStopData(id.ThreadID, jitStartData.Start, new EventUID(evt), jitStartData.OperationType + "(" + jitStartData.Name + ")");
            }
        }

        private void Clr_MethodJittingStarted(MethodJittingStartedTraceData obj)
        {
            IncompleteActionDesc incompleteDesc = new IncompleteActionDesc();
            incompleteDesc.Start = new EventUID(obj);
            incompleteDesc.Name = JITStats.GetMethodName(obj);
            incompleteDesc.OperationType = "JIT";

            IdOfIncompleteAction id = new IdOfIncompleteAction();
            id.Identifier = obj.MethodID;
            id.ThreadID = obj.ThreadID;

            _incompleteJitEvents[id] = incompleteDesc;
        }

        private void Clr_R2RGetEntryPointStarted(R2RGetEntryPointStartedTraceData obj)
        {
            IncompleteActionDesc incompleteDesc = new IncompleteActionDesc();
            incompleteDesc.Start = new EventUID(obj);
            incompleteDesc.Name = "";
            incompleteDesc.OperationType = "R2R";

            IdOfIncompleteAction id = new IdOfIncompleteAction();
            id.Identifier = obj.MethodID;
            id.ThreadID = obj.ThreadID;

            _incompleteR2REvents[id] = incompleteDesc;
        }

        private void Clr_MethodR2RGetEntryPoint(R2RGetEntryPointTraceData obj)
        {
            IdOfIncompleteAction id = new IdOfIncompleteAction();
            id.Identifier = obj.MethodID;
            id.ThreadID = obj.ThreadID;

            // If we had a R2R start lookup event, capture that start time, otherwise, use the R2REntrypoint
            // data as both start and stop
            EventUID startUID = new EventUID(obj);
            if (_incompleteR2REvents.TryGetValue(id, out IncompleteActionDesc r2rStartData))
            {
                startUID = r2rStartData.Start;
                _incompleteR2REvents.Remove(id);
            }

            if (obj.EntryPoint == 0)
            {
                // If Entrypoint is null then the search failed.
                AddStartStopData(id.ThreadID, startUID, new EventUID(obj), "R2R_Failed" + "(" + JITStats.GetMethodName(obj) + ")");
            }
            else
            {
                AddStartStopData(id.ThreadID, startUID, new EventUID(obj), "R2R_Found" + "(" + JITStats.GetMethodName(obj) + ")");
            }
        }

        private void Clr_TypeLoadStart(TypeLoadStartTraceData obj)
        {
            IncompleteActionDesc incompleteDesc = new IncompleteActionDesc();
            incompleteDesc.Start = new EventUID(obj);
            incompleteDesc.Name = "";
            incompleteDesc.OperationType = "TypeLoad";

            IdOfIncompleteAction id = new IdOfIncompleteAction();
            id.Identifier = obj.TypeLoadStartID;
            id.ThreadID = obj.ThreadID;

            _incompleteTypeLoadEvents[id] = incompleteDesc;
        }

        private void Clr_TypeLoadStop(TypeLoadStopTraceData obj)
        {
            IdOfIncompleteAction id = new IdOfIncompleteAction();
            id.Identifier = obj.TypeLoadStartID;
            id.ThreadID = obj.ThreadID;

            // If we had a TypeLoad start lookup event, capture that start time, otherwise, use the TypeLoadStop
            // data as both start and stop
            EventUID startUID = new EventUID(obj);
            if (_incompleteTypeLoadEvents.TryGetValue(id, out IncompleteActionDesc typeLoadStartData))
            {
                startUID = typeLoadStartData.Start;
                _incompleteTypeLoadEvents.Remove(id);
            }

            AddStartStopData(id.ThreadID, startUID, new EventUID(obj), $"TypeLoad ({obj.TypeName}, {obj.LoadLevel})");
        }
    }
}
