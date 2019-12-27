// <copyright file="CallTreeDataEventSource.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace PerfViewJS
{
    using System.Diagnostics.Tracing;

    [EventSource(Guid = "240e729b-d191-59e3-cdd0-aa0a8abed0c3")]
    public sealed class CallTreeDataEventSource : EventSource
    {
        public static CallTreeDataEventSource Log { get; } = new CallTreeDataEventSource();

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