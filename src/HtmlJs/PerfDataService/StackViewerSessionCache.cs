namespace PerfDataService
{
    using Microsoft.Extensions.Caching.Memory;
    using Microsoft.Extensions.Options;

    public sealed class StackViewerSessionCache : MemoryCache
    {
        public StackViewerSessionCache(IOptions<MemoryCacheOptions> optionsAccessor)
            : base(optionsAccessor)
        {
        }
    }
}