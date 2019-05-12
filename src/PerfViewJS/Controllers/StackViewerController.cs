// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PerfViewJS
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.WebUtilities;

    public sealed class StackViewerController
    {
        private const int NumberOfDisplayedTableEntries = 100;

        private readonly IDeserializedDataCache dataCache;

        private readonly StackViewerModel model;

        public StackViewerController(IDeserializedDataCache dataCache, StackViewerModel model)
        {
            this.dataCache = dataCache;
            this.model = model;
        }

        public async ValueTask<List<StackEventTypeInfo>> EventListAPI()
        {
            var deserializedData = this.dataCache.GetData(this.model.Filename);
            return await deserializedData.GetStackEventTypesAsync();
        }

        public async ValueTask<List<ProcessInfo>> ProcessListAPI()
        {
            var deserializedData = this.dataCache.GetData(this.model.Filename);
            return await deserializedData.GetProcessListAsync();
        }

        public async ValueTask<IEnumerable<TreeNode>> HotspotsAPI()
        {
            var data = await this.GetData();
            return await data.GetSummaryTree(NumberOfDisplayedTableEntries);
        }

        public async ValueTask<TreeNode> TreeNodeAPI(string name)
        {
            var data = await this.GetData();
            return await data.GetNode(Encoding.UTF8.GetString(Base64UrlTextEncoder.Decode(name)));
        }

        public async ValueTask<TreeNode[]> CallerChildrenAPI(string name, string path)
        {
            var data = await this.GetData();
            return await data.GetCallerTree(Encoding.UTF8.GetString(Base64UrlTextEncoder.Decode(name)), '-', path);
        }

        public async ValueTask<string> DrillIntoAPI(bool exclusive, string name, string path)
        {
            var data = await this.GetData();
            var stackSource = await data.GetDrillIntoStackSource(exclusive, Encoding.UTF8.GetString(Base64UrlTextEncoder.Decode(name)), '-', path);
            string samplesKey = Guid.NewGuid().ToString();
            this.model.SetDrillIntoKey(samplesKey);
            await this.dataCache.GetData(this.model.Filename).GetCallTreeAsync(this.model, new SourceAwareStackSource(stackSource));
            return samplesKey;
        }

        public async ValueTask<bool> LookupWarmSymbolsAPI(int minCount)
        {
            var data = await this.GetData();
            var retVal = data.LookupWarmSymbols(minCount);
            this.dataCache.ClearAllCacheEntries();
            return retVal;
        }

        private ValueTask<ICallTreeData> GetData()
        {
            var deserializedData = this.dataCache.GetData(this.model.Filename);
            return deserializedData.GetCallTreeAsync(this.model);
        }
    }
}
