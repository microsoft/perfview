using System.Collections.Generic;

namespace Microsoft.Diagnostics.Tracing.Ctf.Contract
{
    public interface ICtfEventTrace
    {
        IEnumerable<ICtfEventPacket> EventPackets { get; }

        int TraceId { get; }
    }
}
