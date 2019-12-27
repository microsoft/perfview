// <copyright file="EventViewerController.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

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
