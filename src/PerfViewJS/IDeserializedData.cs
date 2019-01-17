// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PerfViewJS
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IDeserializedData
    {
        ValueTask<List<StackEventTypeInfo>> GetStackEventTypesAsync();

        ValueTask<List<ProcessInfo>> GetProcessListAsync();

        ValueTask<ICallTreeData> GetCallTreeAsync(StackViewerModel model, GenericStackSource stackSource = null);

        ValueTask<List<EventData>> GetEvents(EventViewerModel model);
    }
}
