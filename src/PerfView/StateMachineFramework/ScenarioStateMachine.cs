using System;
using Diagnostics.Tracing;
using Diagnostics.Tracing.Parsers;
using Diagnostics.Tracing.StackSources;

namespace PerfView
{
    /// <summary>
    /// The state machine that represents the actual scenario.
    /// </summary>
    /// <remarks>
    /// Understands the scenario that the data is used for.
    /// Knows how to slice the data to make it useful (e.g. into requests).
    /// Determines whether samples should be attributed or thrown out (?)
    /// </remarks>
    public abstract class ScenarioStateMachine
    {
        private ScenarioConfiguration m_Configuration;

        public ScenarioStateMachine(
            ScenarioConfiguration configuration)
        {
            m_Configuration = configuration;
        }

        /// <summary>
        /// Get the configuration associated with the state machine.
        /// </summary>
        /// <remarks>
        /// For performance reasons, it is recommended that state machines keep a reference to the scenario thread state, so that it does not need to be casted to the actual type multiple times.
        /// </remarks>
        protected ScenarioConfiguration Configuration
        {
            get { return m_Configuration; }
        }

        /// <summary>
        /// Register event handlers before data is processed.
        /// </summary>
        internal abstract void RegisterEventHandlers(
            TraceEventDispatcher eventDispatcher);
    }
}