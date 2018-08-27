
#if false 
namespace Microsoft.Diagnostics.Tracing
{
    class EventCounterSource : IDisposable
    {
        public EventCounterSource(string eventSourceName) { }
        public EventCounterSource(Guid eventSourceGuid) { }

        public void EnableCounters(int updateTimeSec)
        {
        }

        public void Dispose() { 
        }

        public IEnumerable<string> GetCounterNames() 
        {
            return null;
        }

        public EventCounter GetCounter(string counterName, int processID)
        {
            return null;
        }

        public event Action<EventCounter> CountersUpdated;

        private void SetupETW()
        {
            m_session = new TraceEventSession("EventCounterSource");

        }
        TraceEventSession m_session;
    }

    class EventCounter
    {
        public string Name { get; private set; }
        public int ProcessID { get; private set; }
        public double CurrnetValue { get; private set; } 

        public event Action<EventCounter> CountersUpdated;
    }
}
#endif
