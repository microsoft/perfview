// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PerfViewJS
{
    using System;

    public sealed class CacheExpirationTimeProvider : ICacheExpirationTimeProvider
    {
        public TimeSpan Expiration => TimeSpan.FromMinutes(120); // TODO: make this configurable
    }
}
