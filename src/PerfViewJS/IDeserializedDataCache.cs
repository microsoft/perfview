// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PerfViewJS
{
    public interface IDeserializedDataCache
    {
        IDeserializedData GetData(string cacheKey);

        void ClearAllCacheEntries();
    }
}
