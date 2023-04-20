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

            // TODO: If this is a sched_switch event, then connect the two thread ids.
        }

        internal void RemoveThread(ThreadExitEvent exitEvent)
        {
            // TODO: Do we need to also get the in-container thread ID?
            // TODO: Do we also need to remove from beginning states?
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
