using System;
using Diagnostics.Tracing;
using Diagnostics.Tracing.Parsers;
using Diagnostics.Tracing.StackSources;

namespace PerfView
{
    /// <summary>
    /// Contains the thread state associated with all computing resources.
    /// </summary>
    internal sealed class ComputingResourceThreadState
    {
        private int m_ThreadIndex;

        public ComputingResourceThreadState(
            int threadIndex)
        {
            m_ThreadIndex = threadIndex;
        }

        /// <summary>
        /// The thread index associated with this thread.
        /// </summary>
        public int ThreadIndex
        {
            get { return m_ThreadIndex; }
        }

        /// <summary>
        /// The start time associated with a blocked thread.
        /// </summary>
        public double BlockTimeStartRelMsec { get; set; }

        /// <summary>
        /// True iff the thread is dead.
        /// </summary>
        public bool ThreadDead
        {
            get { return double.IsNegativeInfinity(BlockTimeStartRelMsec); }
        }

        /// <summary>
        /// True iff the threadd isrunning.
        /// </summary>
        public bool ThreadRunning
        {
            get { return BlockTimeStartRelMsec < 0 && !ThreadDead; }
        }

        /// <summary>
        /// True iff the thread is blocked.
        /// </summary>
        public bool ThreadBlocked
        {
            get { return 0 < BlockTimeStartRelMsec; }
        }

        /// <summary>
        /// True iff the thread is unitialized.
        /// </summary>
        public bool ThreadUninitialized
        {
            get { return BlockTimeStartRelMsec == 0; }
        }

        /// <summary>
        /// Mark the thread as blocked.
        /// </summary>
        public void LogBlockingStart(
            TraceThread thread,
            TraceEvent data)
        {
            if ((null == thread) || (null == data))
            {
                return;
            }

            if (!ThreadDead)
            {
                // TODO: Fix (we'll need the last CPU stack as well).
                // AddCPUSample(timeRelMSec, thread, computer);

                BlockTimeStartRelMsec = data.TimeStampRelativeMSec;
            }
        }

        /// <summary>
        /// Mark the thread as unblocked.
        /// </summary>
        public void LogBlockingStop(
            ComputingResourceStateMachine stateMachine,
            TraceThread thread,
            TraceEvent data)
        {
            if ((null == stateMachine) || (null == thread) || (null == data))
            {
                return;
            }

            // Only add a sample if the thread was blocked.
            if (ThreadBlocked)
            {
                StackSourceSample sample = stateMachine.Sample;
                MutableTraceEventStackSource stackSource = stateMachine.StackSource;

                // Set the time and metric.
                sample.TimeRelMSec = this.BlockTimeStartRelMsec;
                sample.Metric = (float)(data.TimeStampRelativeMSec - this.BlockTimeStartRelMsec);

                // Generate the stack trace.
                ScenarioThreadState scenarioThreadState = GetScenarioThreadState(stateMachine);
                StackSourceCallStackIndex callStackIndex = scenarioThreadState.GetCallStackIndex(stackSource, thread);

                // Add the thread.
                StackSourceFrameIndex threadFrameIndex = stackSource.GetFrameIndexForName(thread.VerboseThreadName);
                callStackIndex = stackSource.GetCallStack(threadFrameIndex, callStackIndex);

                // Add the full call stack.
                callStackIndex = stackSource.GetCallStack(data.CallStackIndex(), callStackIndex, null);

                StackSourceFrameIndex frameIndex = stackSource.GetFrameIndexForName("BLOCKED TIME");
                callStackIndex = stackSource.GetCallStack(frameIndex, callStackIndex);

                // Add the call stack to the sample.
                sample.StackIndex = callStackIndex;

                // Add the sample.
                stackSource.AddSample(sample);

                // Mark the thread as executing.
                BlockTimeStartRelMsec = -1;
            }
        }

        /// <summary>
        /// Log a CPU sample on this thread.
        /// </summary>
        public void LogCPUSample(
            ComputingResourceStateMachine stateMachine,
            TraceThread thread,
            TraceEvent data)
        {
            if ((null == stateMachine) || (null == thread) || (null == data))
            {
                return;
            }

            StackSourceSample sample = stateMachine.Sample;
            MutableTraceEventStackSource stackSource = stateMachine.StackSource;


            // Attempt to charge the CPU to a request.
            ScenarioThreadState scenarioThreadState = GetScenarioThreadState(stateMachine);
            StackSourceCallStackIndex callStackIndex = scenarioThreadState.GetCallStackIndex(stackSource, thread);

            // Add the thread.
            StackSourceFrameIndex threadFrameIndex = stackSource.GetFrameIndexForName(thread.VerboseThreadName);
            callStackIndex = stackSource.GetCallStack(threadFrameIndex, callStackIndex);

            // Rest of the stack.
            // NOTE: Do not pass a call stack map into this method, as it will skew results.
            callStackIndex = stackSource.GetCallStack(data.CallStackIndex(), callStackIndex, null);

            // Add the CPU frame.
            StackSourceFrameIndex cpuFrameIndex = stackSource.GetFrameIndexForName("CPU");
            callStackIndex = stackSource.GetCallStack(cpuFrameIndex, callStackIndex);

            // Add the sample.
            sample.StackIndex = callStackIndex;
            sample.Metric = 1;
            sample.TimeRelMSec = data.TimeStampRelativeMSec;
            stackSource.AddSample(sample);
        }

        /// <summary>
        /// Get the scenario thread state for this thread.
        /// </summary>
        private ScenarioThreadState GetScenarioThreadState(
            ComputingResourceStateMachine stateMachine)
        {
            return stateMachine.Configuration.ScenarioThreadState[ThreadIndex];
        }
    }
}