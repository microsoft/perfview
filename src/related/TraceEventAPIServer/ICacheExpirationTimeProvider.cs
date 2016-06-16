namespace TraceEventAPIServer
{
    using System;

    public interface ICacheExpirationTimeProvider
    {
        DateTimeOffset Expiration { get; }
    }
}