// Copyright (c) Microsoft Corporation.  All rights reserved
// This file is best viewed using outline mode (Ctrl-M Ctrl-O)
//
// This program uses code hyperlinks available as part of the HyperAddin Visual Studio plug-in.
// It is available from http://www.codeplex.com/hyperAddin 
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Analysis;
using Microsoft.Diagnostics.Tracing.Analysis.JIT;
using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Tracing
{
    public class RuntimeLoaderProcessData
    {
        public Dictionary<int, CLRRuntimeActivityComputer.PerThreadStartStopData> ThreadData { get; } = new Dictionary<int, CLRRuntimeActivityComputer.PerThreadStartStopData>();
        public double FirstEventTimestamp { get; }
        public int ProcessID { get; }

        public double FinalTimestamp
        {
            get
            {
                if (_finalTimestamp.HasValue)
                    return _finalTimestamp.Value;
                
                throw new Exception();
            }
        }

        public double? _finalTimestamp;

        internal Dictionary<int, List<CLRRuntimeActivityComputer.StartStopThreadEventData>> StartStopEvents { get; private set; } = new Dictionary<int, List<CLRRuntimeActivityComputer.StartStopThreadEventData>>();

        internal void FinishData(double finalTimestamp)
        {
            foreach (var entry in StartStopEvents)
            {
                var data = entry.Value.ToArray();
                Array.Sort(data);
                var perThread = new CLRRuntimeActivityComputer.PerThreadStartStopData();
                perThread.Data = data;
                ThreadData.Add(entry.Key, perThread);
            }
            StartStopEvents = null;
            _finalTimestamp = finalTimestamp;
        }

        public RuntimeLoaderProcessData(double timestamp, int processID)
        {
            ProcessID = processID;
            FirstEventTimestamp = timestamp;
        }

        internal static RuntimeLoaderProcessData EmptyData(int processID)
        {
            RuntimeLoaderProcessData emptyData = new RuntimeLoaderProcessData(0, processID);
            emptyData.FinishData(Double.MaxValue);
            return emptyData;
        }

        public override string ToString()
        {
            return $"PID={ProcessID} FirstTimestamp {FirstEventTimestamp} Finished {_finalTimestamp.HasValue}";
        }
    }

    public class RuntimeLoaderStatsData
    {
        List<RuntimeLoaderProcessData> _processData = new List<RuntimeLoaderProcessData>();

        public IEnumerable<RuntimeLoaderProcessData> GetData()
        {
            return _processData.ToArray();
        }

        public RuntimeLoaderProcessData GetProcessDataFromProcessIDAndTimestamp(int processID, double timestamp)
        {
            foreach (var procData in _processData)
            {
                if (procData.ProcessID == processID)
                {
                    if (timestamp < procData.FinalTimestamp)
                        return procData;
                }
            }

            return RuntimeLoaderProcessData.EmptyData(processID);
        }

        public RuntimeLoaderProcessData GetProcessDataFromAnalysisProcess(TraceProcess process)
        {
            return GetProcessDataFromProcessIDAndTimestamp(process.ProcessID, process.StartTimeRelativeMsec);
        }

        internal void AddProcessData(RuntimeLoaderProcessData processData, double finalTimestamp)
        {
            processData.FinishData(finalTimestamp);
            _processData.Add(processData);
        }
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

            public static IEnumerable<StartStopThreadEventData> FilterData(string[] filters, IEnumerable<StartStopThreadEventData> inputStream)
            {
                foreach (var input in inputStream)
                {
                    foreach (var filter in filters)
                    {
                        if (input.Name.StartsWith(filter))
                            yield return input;
                    }
                }
            }

            public static IEnumerable<StartStopThreadEventData> Stackify(IEnumerable<StartStopThreadEventData> inputStream)
            {
                Stack<StartStopThreadEventData> currentPerThreadProcessingState = new Stack<StartStopThreadEventData>();
                foreach (var startStopIn in inputStream)
                {
                    var startStop = startStopIn;
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
                                yield return tempPoppedEvent;
                                currentPerThreadProcessingState.Push(tempPoppedEvent);
                            }
                        }
                    }

                    startStop.StackDepth = currentPerThreadProcessingState.Count;
                    yield return startStop;
                    currentPerThreadProcessingState.Push(startStop);
                }
            }
        }

        RuntimeLoaderStatsData _startStopData = new RuntimeLoaderStatsData();
        public RuntimeLoaderStatsData RuntimeLoaderData => _startStopData;

        Dictionary<IdOfIncompleteAction, IncompleteActionDesc> _incompleteJitEvents = new Dictionary<IdOfIncompleteAction, IncompleteActionDesc>();
        Dictionary<IdOfIncompleteAction, IncompleteActionDesc> _incompleteR2REvents = new Dictionary<IdOfIncompleteAction, IncompleteActionDesc>();
        Dictionary<IdOfIncompleteAction, IncompleteActionDesc> _incompleteTypeLoadEvents = new Dictionary<IdOfIncompleteAction, IncompleteActionDesc>();

        Dictionary<int, RuntimeLoaderProcessData> _processSpecificDataInProgress = new Dictionary<int, RuntimeLoaderProcessData>();

        public CLRRuntimeActivityComputer(TraceEventDispatcher source)
        {
            source.Clr.MethodJittingStarted += Clr_MethodJittingStarted;
            source.Clr.MethodR2RGetEntryPoint += Clr_MethodR2RGetEntryPoint;
            source.Clr.MethodLoadVerbose += Clr_MethodLoadVerbose;
            source.Clr.MethodLoad += Clr_MethodLoad;
            source.Clr.LoaderAssemblyLoad += Clr_LoaderAssemblyLoad;
            source.Clr.MethodR2RGetEntryPointStart += Clr_R2RGetEntryPointStart;
            source.Clr.TypeLoadStart += Clr_TypeLoadStart;
            source.Clr.TypeLoadStop += Clr_TypeLoadStop;
            source.Kernel.ProcessStop += Kernel_ProcessStop;
            source.Completed += ProcessingComplete;
        }

        private void ProcessingComplete()
        {
            foreach (var entry in _processSpecificDataInProgress.Values)
            {
                _startStopData.AddProcessData(entry, Double.MaxValue);
            }

            _processSpecificDataInProgress.Clear();
        }

        private void AddStartStopData(int processID, int threadId, EventUID start, EventUID end, string name)
        {
            if (!_processSpecificDataInProgress.TryGetValue(processID, out RuntimeLoaderProcessData processData))
            {
                processData = new RuntimeLoaderProcessData(end.Time, processID);
                _processSpecificDataInProgress[processID] = processData;
            }

            if (!processData.StartStopEvents.ContainsKey(threadId))
                processData.StartStopEvents[threadId] = new List<StartStopThreadEventData>();

            List<StartStopThreadEventData> startStopData = processData.StartStopEvents[threadId];
            startStopData.Add(new StartStopThreadEventData(start, end, name));
        }

        private void Kernel_ProcessStop(Parsers.Kernel.ProcessTraceData traceData)
        {
            if (_processSpecificDataInProgress.TryGetValue(traceData.ProcessID, out var processData))
            {
                _startStopData.AddProcessData(processData, traceData.TimeStampRelativeMSec);
                _processSpecificDataInProgress.Remove(traceData.ProcessID);
            }
        }

        private void Clr_LoaderAssemblyLoad(AssemblyLoadUnloadTraceData obj)
        {
            // Since we don't have start stop data, simply treat the assembly load event as a point in time so that it is visible in the textual load view
            EventUID eventTime = new EventUID(obj);

            AddStartStopData(obj.ProcessID, obj.ThreadID, eventTime, eventTime, $"AssemblyLoad({obj.FullyQualifiedAssemblyName},{obj.AssemblyID})");
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

                AddStartStopData(evt.ProcessID, id.ThreadID, jitStartData.Start, new EventUID(evt), jitStartData.OperationType + "(" + jitStartData.Name + ")");
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

        private void Clr_R2RGetEntryPointStart(R2RGetEntryPointStartTraceData obj)
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
                AddStartStopData(obj.ProcessID, id.ThreadID, startUID, new EventUID(obj), "R2R_Failed" + "(" + JITStats.GetMethodName(obj) + ")");
            }
            else
            {
                AddStartStopData(obj.ProcessID, id.ThreadID, startUID, new EventUID(obj), "R2R_Found" + "(" + JITStats.GetMethodName(obj) + ")");
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

            AddStartStopData(obj.ProcessID, id.ThreadID, startUID, new EventUID(obj), $"TypeLoad ({obj.TypeName}, {obj.LoadLevel})");
        }
    }
}
