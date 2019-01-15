// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PerfViewJS
{
    using System.Runtime.Serialization;

    [DataContract]
    public sealed class EventData
    {
        [DataMember]
        public int EventIndex { get; set; }

        [DataMember]
        public bool HasStack { get; set; }

        [DataMember]
        public string Timestamp { get; set; }

        [DataMember]
        public string EventName { get; set; }

        [DataMember]
        public string ProcessName { get; set; }

        [DataMember]
        public string Rest { get; set; }
    }
}
