using System;
using System.IO;
using System.IO.Compression;
using Microsoft.Diagnostics.Tracing.Ctf.Contract;

namespace Microsoft.Diagnostics.Tracing.Ctf.ZippedEvent
{
    internal class ZippedCtfEventPacket : ICtfEventPacket, IDisposable
    {
        private readonly Stream _zipStream;

        public ZippedCtfEventPacket(ZipArchiveEntry entry, int traceId)
        {
            _zipStream = entry.Open();
            Filename = entry.FullName;
            TraceId = traceId;
        }

        public string Filename { get; }
        public int TraceId { get; }

        public Stream CreateReadOnlyStream()
        {
            return _zipStream;
        }

        public void Dispose()
        {
            _zipStream?.Dispose();
        }
    }
}