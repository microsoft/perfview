using System.IO;

namespace Microsoft.Diagnostics.Tracing.Ctf.Contract
{
    public interface ICtfMetadata
    {
        int TraceId { get; }

        Stream CreateReadOnlyStream();
    }
}
