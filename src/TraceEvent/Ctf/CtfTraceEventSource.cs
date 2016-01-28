using System;
using System.Linq;
using System.IO;
using System.IO.Compression;

//using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Ctf;
using System.Collections.Generic;
using System.Diagnostics;
//using Microsoft.Diagnostics.Tracing.Parsers;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Tracing
{
    //using Microsoft.Diagnostics.Tracing.Parsers.DynamicTraceEventData;

    public unsafe sealed class CtfTraceEventSource : TraceEventDispatcher, IDisposable
    {
        private string _filename;
        private ZipArchive _zip;
        private CtfMetadata _metadata;
        private ZipArchiveEntry[] _channels;
        private StreamWriter _debugOutput = File.CreateText("debug.txt");

        public CtfTraceEventSource(string fileName)
        {
            _debugOutput.AutoFlush = true;
            _filename = fileName;
            _zip = ZipFile.Open(fileName, ZipArchiveMode.Read);
            ZipArchiveEntry metadataArchive = _zip.Entries.Where(p => Path.GetFileName(p.FullName) == "metadata").Single();
            
            string path = Path.GetDirectoryName(metadataArchive.FullName);
            _channels = (from entry in _zip.Entries
                         where Path.GetDirectoryName(entry.FullName) == path && Path.GetFileName(entry.FullName).StartsWith("channel")
                         orderby entry.Length descending
                         select entry).Take(1).ToArray();
                
            CtfMetadataLegacyParser parser = new CtfMetadataLegacyParser(metadataArchive.Open());
            _metadata = new CtfMetadata(parser);
            _metadata.Load();

            pointerSize = Path.GetDirectoryName(metadataArchive.FullName).EndsWith("64-bit") ? 8 : 4;
        }

        public override int EventsLost
        {
            get { return 0; }
        }

        public override bool Process()
        {
            foreach (ZipArchiveEntry channelZip in _channels)
            {
                using (Stream stream = channelZip.Open())
                    using (CtfDataStream dataStream = new CtfDataStream(stream, _metadata))
                        ProcessOneChannel(dataStream);
            }

            return true;
        }

        private void ProcessOneChannel(CtfDataStream stream)
        {
            foreach (CtfEventHeader header in stream.EnumerateEventHeaders())
            {
                CtfEvent evt = header.Event;
                _debugOutput.WriteLine("[" + evt.Name + "]");


                if (evt.IsFixedSize)
                {
                    stream.ReadEventIntoBuffer(evt);
                    _debugOutput.WriteLine($"   {stream.BufferLength} bytes read");
                    _debugOutput.WriteLine();
                }
                else
                {
                    object[] result = stream.ReadEvent(evt);
                    evt.WriteLine(_debugOutput, result, 0);
                    _debugOutput.WriteLine();
                }
            }
        }

        public void ParseMetadata()
        {
            // We don't get this data in LTTng traces (unless we decide to emit them as events later).
            osVersion = new Version("0.0.0.0");
            cpuSpeedMHz = 10;

            if (!_metadata.IsLoaded)
            {
                _metadata.Load();

                int processors = (from entry in _channels
                                  let filename = entry.FullName
                                  let i = filename.LastIndexOf('_')
                                  let processor = filename.Substring(i + 1)
                                  select int.Parse(processor)
                                  ).Max() + 1;

                numberOfProcessors = processors;

                var env = _metadata.Environment;
                var trace = _metadata.Trace;
                userData["hostname"] = env.HostName;
                userData["tracer_name"] = env.TracerName;
                userData["tracer_version"] = env.TracerMajor + "." + env.TracerMinor;
                userData["uuid"] = trace.UUID;
                userData["ctf version"] = trace.Major + "." + trace.Minor;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _zip.Dispose();

            base.Dispose(disposing);
        }
    }
}
