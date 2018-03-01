using System.Collections.Generic;
using Microsoft.Diagnostics.Tracing.Ctf.Contract;

namespace Microsoft.Diagnostics.Tracing.Ctf.ZippedEvent
{
    internal class ZippedCtfEventTrace : ICtfEventTrace
    {
        public ZippedCtfEventTrace(int traceId, IEnumerable<ICtfEventPacket> eventPackets)
        {
            TraceId = traceId;
            EventPackets = eventPackets;
        }

        public int TraceId { get; }
        public IEnumerable<ICtfEventPacket> EventPackets { get; }
    }
}