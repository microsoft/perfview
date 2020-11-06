// <copyright file="EventData.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

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
