namespace PerfDataService.Models
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
    }
}