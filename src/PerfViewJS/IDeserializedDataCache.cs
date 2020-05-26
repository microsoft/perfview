// <copyright file="IDeserializedDataCache.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace PerfViewJS
{
    public interface IDeserializedDataCache
    {
        IDeserializedData GetData(string cacheKey);

        void ClearAllCacheEntries();
    }
}
