namespace TraceEventAPIServer
{
    using System;

    public sealed class CacheExpirationTimeProvider : ICacheExpirationTimeProvider
    {
        public DateTimeOffset Expiration => DateTimeOffset.UtcNow + TimeSpan.FromHours(1); // TODO: make this config driven
    }
}