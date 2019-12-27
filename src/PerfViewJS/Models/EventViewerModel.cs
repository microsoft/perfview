// <copyright file="EventViewerModel.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace PerfViewJS
{
    using System.Collections.Generic;
    using System.IO;
    using System.Text;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.WebUtilities;

    public sealed class EventViewerModel
    {
        public EventViewerModel(string dataDirectoryListingRoot, IQueryCollection queryCollection)
        {
            var filename = (string)queryCollection["Filename"] ?? string.Empty;
            this.Filename = Path.Combine(dataDirectoryListingRoot, Encoding.UTF8.GetString(Base64UrlTextEncoder.Decode(filename)));

            var start = (string)queryCollection["Start"] ?? string.Empty;
            this.Start = string.IsNullOrEmpty(start) ? 0.0 : double.Parse(start);

            var end = (string)queryCollection["End"] ?? string.Empty;
            this.End = string.IsNullOrEmpty(end) ? 0.0 : double.Parse(end);

            var maxEventCount = (string)queryCollection["MaxEventCount"] ?? string.Empty;
            this.MaxEventCount = string.IsNullOrEmpty(maxEventCount) ? 10000 : int.Parse(maxEventCount);

            var textFilter = (string)queryCollection["Filter"] ?? string.Empty;
            this.TextFilter = Encoding.UTF8.GetString(Base64UrlTextEncoder.Decode(textFilter));

            var eventTypesString = (string)queryCollection["EventTypes"] ?? string.Empty;
            if (string.IsNullOrEmpty(eventTypesString))
            {
                this.EventTypes = new HashSet<int>(0);
            }
            else
            {
                var arr = ((string)queryCollection["EventTypes"] ?? string.Empty).Split(',');
                var eventTypes = new HashSet<int>(arr.Length);
                foreach (var e in arr)
                {
                    eventTypes.Add(int.Parse(e));
                }

                this.EventTypes = eventTypes;
            }
        }

        public string Filename { get; }

        public HashSet<int> EventTypes { get; }

        public double Start { get; }

        public double End { get; }

        public int MaxEventCount { get; }

        public string TextFilter { get; }
    }
}
