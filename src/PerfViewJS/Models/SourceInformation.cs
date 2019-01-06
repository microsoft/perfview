// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PerfViewJS
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;

    [DataContract]
    public sealed class SourceInformation
    {
        [DataMember]
        public string Type { get; set; }

        [DataMember]
        public string Url { get; set; }

        [DataMember]
        public IEnumerable<LineInformation> Lines { get; set; }

        [DataMember]
        public IEnumerable<LineInformation> Summary { get; set; }

        [DataMember]
        public string BuildTimeFilePath { get; set; }
    }
}