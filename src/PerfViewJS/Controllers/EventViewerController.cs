// (c) Microsoft Corporation.All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PerfViewJS
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public sealed class EventViewerController
    {
        private readonly IDeserializedDataCache dataCache;

        private readonly EventViewerModel eventViewerModel;

        public EventViewerController(IDeserializedDataCache dataCache, EventViewerModel eventViewerModel)
        {
            this.dataCache = dataCache;
            this.eventViewerModel = eventViewerModel;
        }

        public async ValueTask<List<EventData>> EventsAPI()
        {
            return await this.dataCache.GetData(this.eventViewerModel.Filename).GetEvents(this.eventViewerModel);
        }
    }
}
