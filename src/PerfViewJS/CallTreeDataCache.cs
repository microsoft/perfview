// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
