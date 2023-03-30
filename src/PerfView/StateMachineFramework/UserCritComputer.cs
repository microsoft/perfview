using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Stacks;

namespace PerfView
{
    /// <summary>
    /// UserCritScenarioConfiguration is an implementation of the ScenarioConfiguration abstract class that allows
    /// ComputingResourceStateMachine to track resources according to UserCrit acquisition status.
    /// </summary>
    public sealed class UserCritScenarioConfiguration : ScenarioConfiguration
    {
        public UserCritScenarioConfiguration(TraceLog traceLog)
            : base(traceLog)
        {
            // The thread state map must be created before the request computer because
            // a reference to the thread state map is held by the request computer.
            m_ThreadStateMap = new UserCritThreadStateMap(traceLog);
            m_UserCritComputer = new UserCritComputer(this);
        }

        public override ScenarioStateMachine ScenarioStateMachine
        {
            get { return m_UserCritComputer; }
        }

        public override ScenarioThreadState[] ScenarioThreadState
        {
            get { return m_ThreadStateMap.ThreadState; }
        }

        internal UserCritThreadStateMap ThreadStateMap
        {
            get { return m_ThreadStateMap; }
        }

        #region Private
        private UserCritComputer m_UserCritComputer;

        private UserCritThreadStateMap m_ThreadStateMap;
        #endregion
    }

    /// <summary>
    /// Represents the state machine that tracks the UserCrit.
    /// </summary>
    public sealed class UserCritComputer : ScenarioStateMachine
    {
        private const string Win32kProviderName = "";

        public UserCritComputer(UserCritScenarioConfiguration configuration)
            : base(configuration)
        {
            m_ThreadStateMap = configuration.ThreadStateMap;
        }

        /// <summary>
        /// Execute the UserCrit computer.
        /// </summary>
        public override void RegisterEventHandlers(TraceEventDispatcher eventDispatcher)
        {
            DynamicTraceEventParser dynamicParser = new DynamicTraceEventParser(eventDispatcher);
            dynamicParser.AddCallbackForProviderEvent("Microsoft-Windows-Win32k", "ExclusiveUserCrit", (data) =>
            {
                TraceThread thread = data.Thread();
                if (null == thread)
                {
                    return;
                }

                m_ThreadStateMap[thread.ThreadIndex].AcquisitionType = UserCritAcquisitionType.Exclusive;
            });
            dynamicParser.AddCallbackForProviderEvent("Microsoft-Windows-Win32k", "SharedUserCrit", (data) =>
            {
                TraceThread thread = data.Thread();
                if (null == thread)
                {
                    return;
                }

                m_ThreadStateMap[thread.ThreadIndex].AcquisitionType = UserCritAcquisitionType.Shared;
            });
            dynamicParser.AddCallbackForProviderEvent("Microsoft-Windows-Win32k", "ReleaseUserCrit", (data) =>
            {
                TraceThread thread = data.Thread();
                if (null == thread)
                {
                    return;
                }

                m_ThreadStateMap[thread.ThreadIndex].AcquisitionType = UserCritAcquisitionType.NotHeld;
            });
        }

        /// <summary>
        /// Maps thread Indexes to their status.
        /// </summary>
        private UserCritThreadStateMap m_ThreadStateMap;
    }

    internal enum UserCritAcquisitionType
    {
        NotHeld = 0,
        Exclusive,
        Shared
    }

    /// <summary>
    /// The base class for thread state associated with a thread.
    /// </summary>
    internal class UserCritThreadState : ScenarioThreadState
    {
        public UserCritAcquisitionType AcquisitionType { get; set; }

        /// <summary>
        /// Computes that part of the frame from the root of app processes up to (but not including) the thread frame.
        /// </summary>
        public override StackSourceCallStackIndex GetCallStackIndex(MutableTraceEventStackSource stackSource, TraceThread thread, TraceEvent data)
        {
            // Get the call stack index for the process and root of all processes
            StackSourceCallStackIndex callStackIndex = stackSource.GetCallStackForProcess(thread.Process);

            // Add the UserCrit acquisition status.
            string acquisitionType = $"UserCrit Status: {AcquisitionType}";
            StackSourceFrameIndex requestsFrameIndex = stackSource.Interner.FrameIntern(acquisitionType);
            callStackIndex = stackSource.Interner.CallStackIntern(requestsFrameIndex, callStackIndex);

            return callStackIndex;
        }
    }

    #region private classes

    /// <summary>
    /// A simple class that implements lookup by Thread index
    /// </summary>
    internal sealed class UserCritThreadStateMap
    {
        internal UserCritThreadStateMap(TraceLog traceLog)
        {
            m_traceLog = traceLog;
            m_ThreadState = new UserCritThreadState[traceLog.Threads.Count];
            for (int i = 0; i < m_ThreadState.Length; ++i)
            {
                m_ThreadState[i] = new UserCritThreadState();
            }
        }

        internal UserCritThreadState this[ThreadIndex index]
        {
            get { return m_ThreadState[(int)index]; }
        }

        internal UserCritThreadState[] ThreadState
        {
            get { return m_ThreadState; }
        }

        private UserCritThreadState[] m_ThreadState;
        private TraceLog m_traceLog;
    }
    #endregion
}
