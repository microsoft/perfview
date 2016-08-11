namespace PerfDataService
{
    using System;

    public interface ICacheExpirationTimeProvider
    {
        DateTimeOffset Expiration { get; }
    }
}