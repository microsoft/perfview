using Microsoft.Diagnostics.Tracing.StackSources;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Diagnostics.Tracing.Stacks.Linux
{
    internal sealed class LinuxPerfScriptThreadStateComputer
    {
        private readonly Dictionary<int, LinuxPerfScriptThreadState> _beginningStates = new Dictionary<int, LinuxPerfScriptThreadState>();
        private readonly Dictionary<int, LinuxPerfScriptThreadState> _endingStates = new Dictionary<int, LinuxPerfScriptThreadState>();

        internal void PrimeThreadState(LinuxEvent linuxEvent)
        {
            if (!_beginningStates.ContainsKey(linuxEvent.ThreadID))
            {
                _beginningStates.Add(
                    linuxEvent.ThreadID,
                    new LinuxPerfScriptThreadState()
                    {
                        ThreadState = LinuxThreadState.CPU_TIME,
                        Event = linuxEvent
                    });

                _endingStates[linuxEvent.ThreadID] = _beginningStates[linuxEvent.ThreadID];
            }

            // In container scenarios, scheduler and thread exit events have two different IDs for the same thread.
            // One is from the container namespace and one is from the global namespace.  They are logically the same thread.
            if (linuxEvent.Kind == EventKind.Scheduler)
            {
                SchedulerEvent schedulerEvent = (SchedulerEvent)linuxEvent;
                if (!_beginningStates.ContainsKey(schedulerEvent.Switch.PreviousThreadID))
                {
                    _beginningStates.Add(
                        schedulerEvent.Switch.PreviousThreadID,
                        _beginningStates[linuxEvent.ThreadID]);

                    _endingStates[schedulerEvent.Switch.PreviousThreadID] = _beginningStates[linuxEvent.ThreadID];
                }

            }
            else if (linuxEvent.Kind == EventKind.ThreadExit)
            {
                ThreadExitEvent threadExitEvent = (ThreadExitEvent)linuxEvent;
                if (!_beginningStates.ContainsKey(threadExitEvent.Exit.ThreadID))
                {
                    _beginningStates.Add(
                        threadExitEvent.Exit.ThreadID,
                        _beginningStates[linuxEvent.ThreadID]);

                    _endingStates[threadExitEvent.Exit.ThreadID] = _beginningStates[linuxEvent.ThreadID];
                }
            }
        }

        internal void RemoveThread(ThreadExitEvent exitEvent)
        {
            _endingStates.Remove(exitEvent.ThreadID);
            _endingStates.Remove(exitEvent.Exit.ThreadID);
        }

        internal LinuxPerfScriptThreadState GetBeginningState(int threadID)
        {
            _beginningStates.TryGetValue(threadID, out LinuxPerfScriptThreadState value);
            return value;
        }

        internal LinuxPerfScriptThreadState GetEndingStateState(int threadID)
        {
            _endingStates.TryGetValue(threadID, out LinuxPerfScriptThreadState value);
            return value;
        }

        internal IReadOnlyDictionary<int, LinuxPerfScriptThreadState> BeginningStates
        {
            get { return _beginningStates; }
        }

        internal IReadOnlyDictionary<int, LinuxPerfScriptThreadState> EndingStates
        {
            get { return _endingStates; }
        }
    }

    internal sealed class LinuxPerfScriptThreadState
    {
        // Only allow the thread state computer to create instances.
        // This is important because we're going to use the same instance 
        // across multiple thread IDs.
        //
        // This is required to handle cases when the capture took place inside
        // of a container and the thread ID is from inside the container,
        // but the prev_pid is from outside the container.
        internal LinuxPerfScriptThreadState()
        {
        }

        public LinuxThreadState ThreadState { get; set; }
        public LinuxEvent Event { get; set; }
    }
}
