// <copyright file="CallTreeDataCache.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace PerfViewJS
{
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Options;

    public sealed class CallTreeDataCache : MemoryCache
    {
        public CallTreeDataCache(IOptions<MemoryCacheOptions> optionsAccessor)
            : base(optionsAccessor)
        {
        }
    }
}
