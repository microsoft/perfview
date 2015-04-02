using System;
using Diagnostics.Tracing;
using Diagnostics.Tracing.Parsers;
using Diagnostics.Tracing.StackSources;

namespace PerfView
{
    /// <summary>
    /// Contains the thread state associated with the scenario.
    /// </summary>
    public class ScenarioThreadState
    {
        /// <summary>
        /// Get the call stack index for the current thread in its current state.
        /// </summary>
        /// <remarks>
        /// The return value will be used to append the actual call stack.  This is how real call stacks get stitched together with grouping mechanisms.
        /// </remarks>
        public virtual StackSourceCallStackIndex GetCallStackIndex(
            MutableTraceEventStackSource stackSource,
            TraceThread thread)
        {
            if (null == stackSource)
            {
                throw new ArgumentNullException("stackSource");
            }
            if (null == thread)
            {
                throw new ArgumentNullException("thread");
            }

            // By default, return the call stack for the process.
            return stackSource.GetCallStackForProcess(thread.Process);
        }
    }
}