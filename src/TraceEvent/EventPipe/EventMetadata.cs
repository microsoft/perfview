using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.IO;

namespace Microsoft.Diagnostics.Tracing.EventPipe
{
    public class EventMetadata
    {
        public Guid ProviderId;
        public uint EventId;
        public uint Version;
        public string EventName;
        public ulong Keywords;
        public uint Level;
        // An array of event parameter definition consist of parameter type and name
        public Tuple<TypeCode, string>[] ParameterDefinitions;
    }
}