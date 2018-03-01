using System;
using System.IO;
using System.IO.Compression;
using Microsoft.Diagnostics.Tracing.Ctf.Contract;

namespace Microsoft.Diagnostics.Tracing.Ctf.ZippedEvent
{
    internal class ZippedCtfMetadata : ICtfMetadata, IDisposable
    {
        private readonly Stream _zippedStream;

        public ZippedCtfMetadata(ZipArchiveEntry entry, int traceId)
        {
            _zippedStream = entry.Open();
            TraceId = traceId;
        }

        public int TraceId { get; }

        public Stream CreateReadOnlyStream()
        {
            return _zippedStream;
        }

        public void Dispose()
        {
            _zippedStream?.Dispose();
        }
    }
}