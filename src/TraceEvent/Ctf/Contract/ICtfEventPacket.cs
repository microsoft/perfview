using System.IO;

namespace Microsoft.Diagnostics.Tracing.Ctf.Contract
{
    public interface ICtfEventPacket
    {
        Stream CreateReadOnlyStream();
    }
}
