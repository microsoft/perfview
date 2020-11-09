// <copyright file="CacheExpirationTimeProvider.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace PerfViewJS
{
    using System;

    public sealed class CacheExpirationTimeProvider : ICacheExpirationTimeProvider
    {
        public TimeSpan Expiration => TimeSpan.FromMinutes(120); // TODO: make this configurable
    }
}
