// <copyright file="MemoryCacheOptionsConfig.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

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
                Clock = new SystemClock(),
            };
        }

        public MemoryCacheOptions Value { get; }
    }
}
