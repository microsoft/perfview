using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Microsoft.Diagnostics.Tracing.Ctf.Contract;

namespace Microsoft.Diagnostics.Tracing.Ctf.ZippedEvent
{
    internal class ZippedCtfTraceProvider : ICtfTraceProvider
    {
        private readonly List<ZippedCtfEventTrace> _ctfEventTraces = new List<ZippedCtfEventTrace>();
        private readonly List<ZippedCtfMetadata> _metadataStreams;
        private readonly ZipArchive _zip;

        public ZippedCtfTraceProvider(string fileName)
        {
            _metadataStreams = new List<ZippedCtfMetadata>();
            _ctfEventTraces = new List<ZippedCtfEventTrace>();
            _zip = ZipFile.Open(fileName, ZipArchiveMode.Read);
            var success = false;
            var processors = 0;
            try
            {
                var traceId = 0;
                foreach (ZipArchiveEntry metadataArchive in _zip.Entries.Where(p => Path.GetFileName(p.FullName) == "metadata"))
                {
                    _metadataStreams.Add(new ZippedCtfMetadata(metadataArchive, traceId));

                    var path = Path.GetDirectoryName(metadataArchive.FullName);
                    var channelForCurrentMetadata = new List<ZippedCtfEventPacket>();
                    foreach (var entry in _zip.Entries)
                    {
                        if (Path.GetDirectoryName(entry.FullName) != path || !Path.GetFileName(entry.FullName).StartsWith("channel"))
                            continue;
                        channelForCurrentMetadata.Add(new ZippedCtfEventPacket(entry, traceId));
                    }

                    _ctfEventTraces.Add(new ZippedCtfEventTrace(traceId, channelForCurrentMetadata));

                    PointerSize = Path.GetDirectoryName(metadataArchive.FullName).EndsWith("64-bit") ? 8 : 4;
                    processors = Math.Max(processors, channelForCurrentMetadata.Select(s => GetProcessorNumber(s.Filename)).Max() + 1);
                    traceId++;
                }

                ProcessorCount = processors;

                success = true;
            }
            finally
            {
                if (!success)
                    Dispose(); // This closes the ZIP file we opened.  We don't want to leave it dangling.  
            }
        }

        public event NewMetadataEventHandler NewCtfMetadata;
        public event NewEventTracesEventHandler NewCtfEventTraces;

        public int PointerSize { get; }
        public int ProcessorCount { get; }

        public void Process()
        {
            foreach (var metadataStream in _metadataStreams)
                NewCtfMetadata?.Invoke(metadataStream);

            NewCtfEventTraces?.Invoke(_ctfEventTraces);
        }

        public void StopProcessing()
        {
        }

        public void Dispose()
        {
            try
            {
                foreach (var eventTrace in _ctfEventTraces)
                foreach (var eventPacket in eventTrace.EventPackets)
                {
                    (eventPacket as IDisposable)?.Dispose();
                }
            }
            finally
            {
                _ctfEventTraces.Clear();
            }

            try
            {
                foreach (var metadaStream in _metadataStreams)
                    metadaStream.Dispose();
            }
            finally
            {
                _metadataStreams.Clear();
            }

            _zip?.Dispose();
        }

        private static int GetProcessorNumber(string filename)
        {
            var idx = filename.IndexOf('_');
            if (idx == -1) return 0;

            var processor = filename.Substring(idx + 1);
            return int.TryParse(processor, out var processorNumber) ? processorNumber : 0;
        }
    }
}