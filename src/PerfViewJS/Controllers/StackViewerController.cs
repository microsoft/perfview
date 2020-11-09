// <copyright file="StackViewerController.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

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

        public async ValueTask<StackEventTypeInfo[]> EventListAPIOrderedByName()
        {
            var deserializedData = this.dataCache.GetData(this.model.Filename);
            return await deserializedData.GetStackEventTypesAsyncOrderedByName();
        }

        public async ValueTask<StackEventTypeInfo[]> EventListAPIOrderedByStackCount()
        {
            var deserializedData = this.dataCache.GetData(this.model.Filename);
            return await deserializedData.GetStackEventTypesAsyncOrderedByStackCount();
        }

        public async ValueTask<ProcessInfo[]> ProcessChooserAPI()
        {
            var deserializedData = this.dataCache.GetData(this.model.Filename);
            return await deserializedData.GetProcessChooserAsync();
        }

        public async ValueTask<DetailedProcessInfo> DetailedProcessInfoAPI(int processIndex)
        {
            var deserializedData = this.dataCache.GetData(this.model.Filename);
            return await deserializedData.GetDetailedProcessInfoAsync(processIndex);
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

        public async ValueTask<SourceInformation> GetSourceAPI(string name, string path, string authorizationHeader)
        {
            var data = await this.GetData();
            return await data.Source(authorizationHeader, Encoding.UTF8.GetString(Base64UrlTextEncoder.Decode(name)), '-', path);
        }

        public async ValueTask<string> DrillIntoAPI(bool exclusive, string name, string path)
        {
            var data = await this.GetData();
            var stackSource = await data.GetDrillIntoStackSource(exclusive, Encoding.UTF8.GetString(Base64UrlTextEncoder.Decode(name)), '-', path);
            string samplesKey = Guid.NewGuid().ToString();
            this.model.SetDrillIntoKey(samplesKey);
            await this.dataCache.GetData(this.model.Filename).GetCallTreeAsync(this.model, stackSource);
            return samplesKey;
        }

        public async ValueTask<string> LookupWarmSymbolsAPI(int minCount)
        {
            var data = await this.GetData();
            var retVal = data.LookupWarmSymbols(minCount);
            this.dataCache.ClearAllCacheEntries();
            return retVal;
        }

        public async ValueTask<TraceInfo> GetTraceInfoAPI()
        {
            return await this.dataCache.GetData(this.model.Filename).GetTraceInfoAsync();
        }

        public async ValueTask<ModuleInfo[]> GetModulesAPI()
        {
            return await this.dataCache.GetData(this.model.Filename).GetModulesAsync();
        }

        public async ValueTask<string> LookupSymbolAPI(int moduleIndex)
        {
            return await this.dataCache.GetData(this.model.Filename).LookupSymbolAsync(moduleIndex);
        }

        public async ValueTask<string> LookupSymbolsAPI(int[] moduleIndices)
        {
            return await this.dataCache.GetData(this.model.Filename).LookupSymbolsAsync(moduleIndices);
        }

        private ValueTask<ICallTreeData> GetData()
        {
            var deserializedData = this.dataCache.GetData(this.model.Filename);
            return deserializedData.GetCallTreeAsync(this.model);
        }
    }
}
