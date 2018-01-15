using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FastSerialization;

namespace Microsoft.Diagnostics.Tracing.EventPipe
{
    internal unsafe class EventPipeEventSourceV2 : EventPipeEventSourceV1
    {
        public EventPipeEventSourceV2(Deserializer deserializer, string fileName, int version) : base(deserializer, fileName, version)
        {
        }

        protected override void ReadHeaderInfo()
        {
            // V1 needs to read time and frequency first (the fields are written to file in a predefined order)
            // the order of fields is defined in dotnet/coreclr/src/vm/eventpipefile.cpp
            // https://github.com/dotnet/coreclr/blob/c1bbdae7964b19b7063074d36e6af960f0cdc3a0/src/vm/eventpipefile.cpp#L49-L58
            base.ReadHeaderInfo();

            pointerSize = _deserializer.ReadInt();

            numberOfProcessors = _deserializer.ReadInt();
        }
    }
}
