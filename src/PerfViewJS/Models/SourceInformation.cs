// <copyright file="SourceInformation.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace PerfViewJS
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    [DataContract]
    public sealed class SourceInformation
    {
        [DataMember]
        public string Url { get; set; }

        [DataMember]
        public string Log { get; set; }

        [DataMember]
        public IEnumerable<LineInformation> Summary { get; set; }

        [DataMember]
        public string Data { get; set; }

        [DataMember]
        public string BuildTimeFilePath { get; set; }
    }
}