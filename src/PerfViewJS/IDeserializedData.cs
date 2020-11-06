// <copyright file="IDeserializedData.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace PerfViewJS
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Diagnostics.Tracing.Stacks;

    public interface IDeserializedData
    {
        ValueTask<StackEventTypeInfo[]> GetStackEventTypesAsyncOrderedByName();

        ValueTask<StackEventTypeInfo[]> GetStackEventTypesAsyncOrderedByStackCount();

        ValueTask<ProcessInfo[]> GetProcessChooserAsync();

        ValueTask<DetailedProcessInfo> GetDetailedProcessInfoAsync(int processIndex);

        ValueTask<ICallTreeData> GetCallTreeAsync(StackViewerModel model, StackSource stackSource = null);

        ValueTask<List<EventData>> GetEvents(EventViewerModel model);

        ValueTask<ModuleInfo[]> GetModulesAsync();

        ValueTask<TraceInfo> GetTraceInfoAsync();

        ValueTask<string> LookupSymbolAsync(int moduleIndex);

        ValueTask<string> LookupSymbolsAsync(int[] moduleIndices);
    }
}
