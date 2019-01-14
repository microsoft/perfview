// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PerfViewJS
{
    using System.Collections.Generic;
    using System.Text;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.WebUtilities;

    public sealed class EventViewerModel
    {
        public EventViewerModel(HttpContext httpContext)
        {
            var filename = (string)httpContext.Request.Query["Filename"] ?? string.Empty;
            this.Filename = Encoding.UTF8.GetString(Base64UrlTextEncoder.Decode(filename));

            var start = (string)httpContext.Request.Query["Start"] ?? string.Empty;
            this.Start = string.IsNullOrEmpty(start) ? 0.0 : double.Parse(start);

            var end = (string)httpContext.Request.Query["End"] ?? string.Empty;
            this.End = string.IsNullOrEmpty(end) ? 0.0 : double.Parse(end);

            var maxEventCount = (string)httpContext.Request.Query["MaxEventCount"] ?? string.Empty;
            this.MaxEventCount = string.IsNullOrEmpty(maxEventCount) ? 10000 : int.Parse(maxEventCount);

            var textFilter = (string)httpContext.Request.Query["Filter"] ?? string.Empty;
            this.TextFilter = Encoding.UTF8.GetString(Base64UrlTextEncoder.Decode(textFilter));

            var eventTypesString = (string)httpContext.Request.Query["EventTypes"] ?? string.Empty;
            if (string.IsNullOrEmpty(eventTypesString))
            {
                this.EventTypes = new HashSet<int>(0);
            }
            else
            {
                var arr = ((string)httpContext.Request.Query["EventTypes"] ?? string.Empty).Split(',');
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
