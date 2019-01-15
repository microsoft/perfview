// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PerfViewJS
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Diagnostics.Symbols;
    using Microsoft.Diagnostics.Tracing.Etlx;

    public sealed class DeserializedData : IDeserializedData
    {
        private readonly string filename;

        private readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

        private readonly List<StackEventTypeInfo> stackEventTypes = new List<StackEventTypeInfo>();

        private readonly List<ProcessInfo> processList = new List<ProcessInfo>();

        private readonly Dictionary<StackViewerModel, ICallTreeData> callTreeDataCache = new Dictionary<StackViewerModel, ICallTreeData>();

        private readonly SymbolReader symbolReader;

        private int initialized;

        private TraceLogDeserializer deserializer;

        public DeserializedData(string filename, SymbolReader symbolReader)
        {
            this.filename = filename;
            this.symbolReader = symbolReader;
        }

        public async ValueTask<List<StackEventTypeInfo>> GetStackEventTypesAsync()
        {
            await this.EnsureInitialized();
            return this.stackEventTypes;
        }

        public async ValueTask<ICallTreeData> GetCallTreeAsync(StackViewerModel model, GenericStackSource stackSource = null)
        {
            await this.EnsureInitialized();

            lock (this.callTreeDataCache)
            {
                if (!this.callTreeDataCache.TryGetValue(model, out var value))
                {
                    value = new CallTreeData(stackSource ?? this.deserializer.GetStackSource((ProcessIndex)int.Parse(model.Pid), int.Parse(model.StackType)), model, this.symbolReader);
                    this.callTreeDataCache.Add(model, value);
                }

                return value;
            }
        }

        public async ValueTask<List<ProcessInfo>> GetProcessListAsync()
        {
            await this.EnsureInitialized();
            return this.processList;
        }

        public async ValueTask<List<EventData>> GetEvents(EventViewerModel model)
        {
            await this.EnsureInitialized();
            return this.deserializer.GetEvents(model.EventTypes, model.TextFilter, model.MaxEventCount, model.Start, model.End);
        }

        private async Task EnsureInitialized()
        {
            if (Interlocked.CompareExchange(ref this.initialized, 1, comparand: -1) == 0)
            {
                await this.Initialize();
            }
        }

        private async Task Initialize()
        {
            await this.semaphoreSlim.WaitAsync();

            try
            {
                if (this.initialized == 1)
                {
                    return;
                }

                this.deserializer = new TraceLogDeserializer(this.filename);

                this.stackEventTypes.Add(new StackEventTypeInfo(0, "All Events", this.deserializer.TotalEventCount, this.deserializer.TotalStackCount));

                foreach (var pair in this.deserializer.EventStats.OrderByDescending(t => t.Value.StackCount))
                {
                    this.stackEventTypes.Add(new StackEventTypeInfo(pair.Key, pair.Value.EventName, pair.Value.Count, pair.Value.StackCount));
                }

                float totalmsec = 0;
                foreach (var traceProcess in this.deserializer.TraceProcesses)
                {
                    totalmsec += traceProcess.CPUMSec;
                }

                this.processList.Add(new ProcessInfo("All Processes", (int)ProcessIndex.Invalid, totalmsec));

                foreach (var pair in this.deserializer.TraceProcesses.OrderByDescending(t => t.CPUMSec))
                {
                    this.processList.Add(new ProcessInfo(pair.Name + $" ({pair.ProcessID})", (int)pair.ProcessIndex, pair.CPUMSec));
                }

                this.initialized = 1;
            }
            finally
            {
                this.semaphoreSlim.Release();
            }
        }
    }
}
