// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PerfViewJS
{
    using System;
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Internal;
    using Microsoft.Extensions.Options;

    internal sealed class MemoryCacheOptionsConfig : IOptions<MemoryCacheOptions>
    {
        public MemoryCacheOptionsConfig()
        {
            this.Value = new MemoryCacheOptions
            {
                CompactionPercentage = 0.9,
                ExpirationScanFrequency = TimeSpan.FromMinutes(60.0),
                Clock = new SystemClock()
            };
        }

        public MemoryCacheOptions Value { get; }
    }
}
