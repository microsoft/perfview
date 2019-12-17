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
            public string OperationType;

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
            public string Name;
        }

        Dictionary<IdOfIncompleteAction, IncompleteActionDesc> _incompleteJitEvents = new Dictionary<IdOfIncompleteAction, IncompleteActionDesc>();
        Dictionary<int, List<StartStopStackMingledComputer.StartStopThreadEventData>> _parsedData = new Dictionary<int, List<StartStopStackMingledComputer.StartStopThreadEventData>>();

        public Dictionary<int, List<StartStopStackMingledComputer.StartStopThreadEventData>> StartStopEvents => _parsedData;

        public CLRRuntimeActivityComputer(TraceLogEventSource source)
        {
            source.Clr.MethodJittingStarted += Clr_MethodJittingStarted;
            source.Clr.MethodR2RGetEntryPoint += Clr_MethodR2RGetEntryPoint;
            source.Clr.MethodLoadVerbose += Clr_MethodLoadVerbose;
            source.Clr.MethodLoad += Clr_MethodLoad;
            source.Process();
            source.Clr.MethodJittingStarted -= Clr_MethodJittingStarted;
            source.Clr.MethodLoadVerbose -= Clr_MethodLoadVerbose;
            source.Clr.MethodLoad -= Clr_MethodLoad;
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

                if (!_parsedData.ContainsKey(id.ThreadID))
                    _parsedData[id.ThreadID] = new List<StartStopStackMingledComputer.StartStopThreadEventData>();

                List<StartStopStackMingledComputer.StartStopThreadEventData> startStopData = _parsedData[id.ThreadID];
                startStopData.Add(new StartStopStackMingledComputer.StartStopThreadEventData(jitStartData.Start, new StartStopStackMingledComputer.EventUID(evt), id.OperationType + "(" +jitStartData.Name+")"));
            }
        }

        private void Clr_MethodJittingStarted(MethodJittingStartedTraceData obj)
        {
            IncompleteActionDesc incompleteDesc = new IncompleteActionDesc();
            incompleteDesc.Start = new StartStopStackMingledComputer.EventUID(obj);
            incompleteDesc.Name = JITStats.GetMethodName(obj);

            IdOfIncompleteAction id = new IdOfIncompleteAction();
            id.Identifier = obj.MethodID;
            id.ThreadID = obj.ThreadID;
            id.OperationType = "JIT";

            _incompleteJitEvents[id] = incompleteDesc;
        }

        private void Clr_MethodR2RGetEntryPoint(R2RGetEntryPointTraceData obj)
        {
            IncompleteActionDesc incompleteDesc = new IncompleteActionDesc();
            incompleteDesc.Start = new StartStopStackMingledComputer.EventUID(obj);
            incompleteDesc.Name = JITStats.GetMethodName(obj);

            IdOfIncompleteAction id = new IdOfIncompleteAction();
            id.Identifier = obj.MethodID;
            id.ThreadID = obj.ThreadID;
            id.OperationType = "R2R";

            _incompleteJitEvents[id] = incompleteDesc;
        }
    }
}
