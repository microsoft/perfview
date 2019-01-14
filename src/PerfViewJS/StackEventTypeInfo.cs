// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PerfViewJS
{
    public sealed class StackEventTypeInfo
    {
        public StackEventTypeInfo(int eventId, string eventName, int eventCount, int stackEventCount)
        {
            this.EventId = eventId;
            this.EventName = eventName;
            this.EventCount = eventCount;
            this.StackEventCount = stackEventCount;
        }

        public int EventId { get; }

        public string EventName { get; }

        public int EventCount { get; }

        public int StackEventCount { get; }
    }
}
