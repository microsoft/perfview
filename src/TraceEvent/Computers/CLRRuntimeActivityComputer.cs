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
    public class CLRRuntimeActivityComputer
    {
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
            public StartStopStackMingledComputer.EventUID Start;
            public string OperationType;
            public string Name;
        }

        Dictionary<IdOfIncompleteAction, IncompleteActionDesc> _incompleteJitEvents = new Dictionary<IdOfIncompleteAction, IncompleteActionDesc>();
        Dictionary<IdOfIncompleteAction, IncompleteActionDesc> _incompleteR2REvents = new Dictionary<IdOfIncompleteAction, IncompleteActionDesc>();

        public Dictionary<int, List<StartStopStackMingledComputer.StartStopThreadEventData>> StartStopEvents { get; } = new Dictionary<int, List<StartStopStackMingledComputer.StartStopThreadEventData>>();

        public CLRRuntimeActivityComputer(TraceLogEventSource source)
        {
            source.Clr.MethodJittingStarted += Clr_MethodJittingStarted;
            source.Clr.MethodR2RGetEntryPoint += Clr_MethodR2RGetEntryPoint;
            source.Clr.MethodLoadVerbose += Clr_MethodLoadVerbose;
            source.Clr.MethodLoad += Clr_MethodLoad;
            source.Clr.LoaderAssemblyLoad += Clr_LoaderAssemblyLoad;
            source.Process();
            source.Clr.MethodJittingStarted -= Clr_MethodJittingStarted;
            source.Clr.MethodR2RGetEntryPoint -= Clr_MethodR2RGetEntryPoint;
            source.Clr.MethodLoadVerbose -= Clr_MethodLoadVerbose;
            source.Clr.MethodLoad -= Clr_MethodLoad;
            source.Clr.LoaderAssemblyLoad -= Clr_LoaderAssemblyLoad;
        }

        private void AddStartStopData(int threadId, StartStopStackMingledComputer.EventUID start, StartStopStackMingledComputer.EventUID end, string name)
        {
            if (!StartStopEvents.ContainsKey(threadId))
                StartStopEvents[threadId] = new List<StartStopStackMingledComputer.StartStopThreadEventData>();

            List<StartStopStackMingledComputer.StartStopThreadEventData> startStopData = StartStopEvents[threadId];
            startStopData.Add(new StartStopStackMingledComputer.StartStopThreadEventData(start, end, name));
        }

        private void Clr_LoaderAssemblyLoad(AssemblyLoadUnloadTraceData obj)
        {
            // Since we don't have start stop data, simply treat the assembly load event as a point in time so that it is visible in the textual load view
            StartStopStackMingledComputer.EventUID eventTime = new StartStopStackMingledComputer.EventUID(obj);
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

                AddStartStopData(id.ThreadID, jitStartData.Start, new StartStopStackMingledComputer.EventUID(evt), jitStartData.OperationType + "(" + jitStartData.Name + ")");
            }
        }

        private void Clr_MethodJittingStarted(MethodJittingStartedTraceData obj)
        {
            IncompleteActionDesc incompleteDesc = new IncompleteActionDesc();
            incompleteDesc.Start = new StartStopStackMingledComputer.EventUID(obj);
            incompleteDesc.Name = JITStats.GetMethodName(obj);
            incompleteDesc.OperationType = "JIT";

            IdOfIncompleteAction id = new IdOfIncompleteAction();
            id.Identifier = obj.MethodID;
            id.ThreadID = obj.ThreadID;

            _incompleteJitEvents[id] = incompleteDesc;
        }

        private void Clr_MethodR2RGetEntryPoint(R2RGetEntryPointTraceData obj)
        {
            IdOfIncompleteAction id = new IdOfIncompleteAction();
            id.Identifier = obj.MethodID;
            id.ThreadID = obj.ThreadID;

            // If we had a R2R start lookup event, capture that start time, otherwise, use the R2REntrypoint
            // data as both start and stop
            StartStopStackMingledComputer.EventUID startUID = new StartStopStackMingledComputer.EventUID(obj);
            if (_incompleteR2REvents.TryGetValue(id, out IncompleteActionDesc r2rStartData))
            {
                startUID = r2rStartData.Start;
                _incompleteJitEvents.Remove(id);
            }

            if (obj.EntryPoint == 0)
            {
                // If Entrypoint is null then the search failed.
                AddStartStopData(id.ThreadID, startUID, new StartStopStackMingledComputer.EventUID(obj), "R2R_Failed" + "(" + JITStats.GetMethodName(obj) + ")");
            }
            else
            {
                AddStartStopData(id.ThreadID, startUID, new StartStopStackMingledComputer.EventUID(obj), "R2R_Found" + "(" + JITStats.GetMethodName(obj) + ")");
            }
        }
    }
}
