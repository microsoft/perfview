// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PerfViewJS
{
    using System;
    using System.Diagnostics.Tracing;
    using Microsoft.Diagnostics.Symbols;
    using Microsoft.Extensions.Caching.Memory;

    public class DeserializedDataCache : IDeserializedDataCache
    {
        private readonly CallTreeDataCache cache;

        private readonly ICacheExpirationTimeProvider cacheExpirationTimeProvider;

        private readonly SymbolReader symbolReader;

        public DeserializedDataCache(CallTreeDataCache cache, ICacheExpirationTimeProvider cacheExpirationTimeProvider, SymbolReader symbolReader)
        {
            this.cache = cache;
            this.cacheExpirationTimeProvider = cacheExpirationTimeProvider;
            this.symbolReader = symbolReader;
        }

        public void ClearAllCacheEntries()
        {
            lock (this.cache)
            {
                this.cache.Compact(100);
            }
        }

        public IDeserializedData GetData(string cacheKey)
        {
            lock (this.cache)
            {
                if (!this.cache.TryGetValue(cacheKey, out IDeserializedData data))
                {
                    var cacheEntryOptions = new MemoryCacheEntryOptions().SetPriority(CacheItemPriority.NeverRemove).RegisterPostEvictionCallback(callback: EvictionCallback, state: this).SetSlidingExpiration(this.cacheExpirationTimeProvider.Expiration);
                    data = new DeserializedData(cacheKey, this.symbolReader);
                    this.cache.Set(cacheKey, data, cacheEntryOptions);
                    CacheMonitorEventSource.Logger.CacheEntryAdded(Environment.MachineName, cacheKey);
                }

                return data;
            }
        }

        private static void EvictionCallback(object key, object value, EvictionReason reason, object state)
        {
            CacheMonitorEventSource.Logger.CacheEntryRemoved(Environment.MachineName, (string)key);
        }

        [EventSource(Guid = "203010e5-cae2-5761-b597-a757ae66787b")]
        private sealed class CacheMonitorEventSource : EventSource
        {
            public static CacheMonitorEventSource Logger { get; } = new CacheMonitorEventSource();

            public void CacheEntryAdded(string source, string cacheKey)
            {
                this.WriteEvent(1, source, cacheKey);
            }

            public void CacheEntryRemoved(string source, string cacheKey)
            {
                this.WriteEvent(2, source, cacheKey);
            }
        }
    }
}
