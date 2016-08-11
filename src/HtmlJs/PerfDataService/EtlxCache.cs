namespace PerfDataService
{
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Options;

    public sealed class EtlxCache : MemoryCache
    {
        public EtlxCache(IOptions<MemoryCacheOptions> optionsAccessor)
            : base(optionsAccessor)
        {
        }
    }
}