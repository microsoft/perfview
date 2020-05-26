// <copyright file="ICacheExpirationTimeProvider.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace PerfViewJS
{
    using System;

    public interface ICacheExpirationTimeProvider
    {
        TimeSpan Expiration { get; }
    }
}
