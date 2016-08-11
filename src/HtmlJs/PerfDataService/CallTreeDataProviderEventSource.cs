namespace PerfDataService
{
    using System.Diagnostics.Tracing;

    public sealed class CallTreeDataProviderEventSource : EventSource
    {
        public static CallTreeDataProviderEventSource Log = new CallTreeDataProviderEventSource();

        public void NodeCacheHit(string node)
        {
            this.WriteEvent(1, node);
        }

        public void NodeCacheMisss(string node)
        {
            this.WriteEvent(2, node);
        }

        public void NodeCacheNotFound(string node)
        {
            this.WriteEvent(3, node);
        }
    }
}