using System;
using Diagnostics.Tracing;
using Diagnostics.Tracing.Parsers;
using Diagnostics.Tracing.StackSources;

namespace PerfView
{
    /// <summary>
    /// Connects the two state machines together.
    /// </summary>
    /// <remarks>
    /// Responsible for creation of the scenario state machine and scenario thread state.
    /// Tells the computing resource state machine about the scenario state machine.
    /// Tells the computing resource about the custom data that must be stored in the thread state.
    /// Stores and manages the scenario thread state for every thread in the trace.
    ///      (A given scenario thread state needs unfettered access to scenario thread state on other threads to e.g. clear a request off another thread.)
    /// </remarks>
    public abstract class ScenarioConfiguration
    {
        private TraceLog m_TraceLog;

        public ScenarioConfiguration(
            TraceLog traceLog)
        {
            m_TraceLog = traceLog;
        }

        /// <summary>
        /// The trace log to be consumed.
        /// </summary>
        public TraceLog TraceLog
        {
            get { return m_TraceLog; }
        }

        /// <summary>
        /// Get the scenario state machine.
        /// </summary>
        /// <remarks>
        /// This member is responsible for storage of the state machine, and must not create a new one on each call.
        /// </remarks>
        public abstract ScenarioStateMachine ScenarioStateMachine { get; }

        /// <summary>
        /// Get the thread state associated with the scenario.
        /// </summary>
        /// <remarks>
        /// This class is responsible for initialization of the thread state, based on input to the constructor (e.g. the trace log is required to know how many threads exist).
        /// Index into this array by using the ThreadIndex.
        /// </remarks>
        public abstract ScenarioThreadState[] ScenarioThreadState { get; }
    }
}