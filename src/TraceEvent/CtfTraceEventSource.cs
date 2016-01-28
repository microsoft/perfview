using Microsoft.Diagnostics.Tracing.Ctf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Tracing
{
    internal struct ETWMapping
    {
        public bool IsNull { get { return Guid == new Guid(); } }

        public Guid Guid;
        public TraceEventOpcode Opcode;
        public TraceEventID Id;
        public Byte Version;

        public ETWMapping(Guid guid, TraceEventOpcode opcode, TraceEventID id, int version)
        {
            Guid = guid;
            Opcode = opcode;
            Id = id;
            Version = (byte)version;
        }
    }

    public unsafe sealed class CtfTraceEventSource : TraceEventDispatcher, IDisposable
    {
        private string _filename;
        private ZipArchive _zip;
        private CtfMetadata _metadata;
        private ZipArchiveEntry[] _channels;
        private StreamWriter _debugOutput = File.CreateText("debug.txt");
        private TraceEventNativeMethods.EVENT_RECORD* _header;
        private Dictionary<string, TraceEvent> _privateEvents = new Dictionary<string, TraceEvent>();
        private Dictionary<string, TraceEvent> _publicEvents = new Dictionary<string, TraceEvent>();


        const string DotNetPrivate = "DotNETRuntimePrivate";
        const string DotNetPublic = "DotNETRuntime";
        const string PrivateProviderName = "Microsoft-Windows-DotNETRuntimePrivate";
        const string PublicProviderName = "Microsoft-Windows-DotNETRuntime";
        

        public CtfTraceEventSource(string fileName)
        {
            _debugOutput.AutoFlush = true;
            _filename = fileName;
            _zip = ZipFile.Open(fileName, ZipArchiveMode.Read);
            ZipArchiveEntry metadataArchive = _zip.Entries.Where(p => Path.GetFileName(p.FullName) == "metadata").Single();
            
            string path = Path.GetDirectoryName(metadataArchive.FullName);
            _channels = (from entry in _zip.Entries
                         where Path.GetDirectoryName(entry.FullName) == path && Path.GetFileName(entry.FullName).StartsWith("channel")
                         select entry).ToArray();
                
            CtfMetadataLegacyParser parser = new CtfMetadataLegacyParser(metadataArchive.Open());
            _metadata = new CtfMetadata(parser);
            _metadata.Load();

            pointerSize = Path.GetDirectoryName(metadataArchive.FullName).EndsWith("64-bit") ? 8 : 4;


            IntPtr mem = Marshal.AllocHGlobal(sizeof(TraceEventNativeMethods.EVENT_RECORD));
            TraceEventNativeMethods.ZeroMemory(mem, sizeof(TraceEventNativeMethods.EVENT_RECORD));
            _header = (TraceEventNativeMethods.EVENT_RECORD*)mem;

            numberOfProcessors = _channels.Length;

            CtfClock clock = _metadata.Clocks.First();

            _QPCFreq = (long)clock.Frequency;
            sessionStartTimeQPC = 1;
            _syncTimeQPC = 1;
            _syncTimeUTC = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds((clock.Offset - 1) / clock.Frequency);

            InitEvents();
        }

        private void InitEvents()
        {
            var parser = new Parsers.ClrPrivateTraceEventParser(this);
            parser.EnumerateTemplates(null, delegate(TraceEvent evt)
            {
                Debug.Assert(evt.ProviderName == PrivateProviderName);
                _privateEvents[evt.OpcodeName] = evt;
            });

            var parser2 = new Parsers.ClrTraceEventParser(this);
            parser2.EnumerateTemplates(null, delegate(TraceEvent evt)
            {
                Debug.Assert(evt.ProviderName == PublicProviderName);
                _publicEvents[evt.OpcodeName] = evt;
            });
            
            _privateEvents["PrvSetGCHandle"] = _privateEvents["SetGCHandle"];
            _publicEvents["AppDomainLoad_V1"] = _publicEvents["AppDomainLoad"];
            _publicEvents["GCCreateSegment_V1"] = _publicEvents["CreateSegment"];
            _privateEvents["SecurityCatchCall_V1"] = _privateEvents["SecurityCatchCallStart"];
            _publicEvents["AssemblyLoad_V1"] = _publicEvents["AssemblyLoad"];
            _publicEvents["MethodJittingStarted_V1"] = _publicEvents["JittingStarted"];
            _privateEvents["PrvFinalizeObject"] = _privateEvents["FinalizeObject"];
            _publicEvents["MethodJitInliningSucceeded"] = _publicEvents["InliningSucceeded"];
            _publicEvents["MethodLoadVerbose_V1"] = _publicEvents["LoadVerbose"];
            _publicEvents["MethodILToNativeMap"] = _publicEvents["ILToNativeMap"];
            _publicEvents["GCAllocationTick_V3"] = _publicEvents["AllocationTick"];
            _publicEvents["DomainModuleLoad_V1"] = _publicEvents["DomainModuleLoad"];
            _publicEvents["GCSuspendEEBegin_V1"] = _publicEvents["SuspendEEStart"];
            _publicEvents["GCSuspendEEEnd_V1"] = _publicEvents["SuspendEEStop"];
            _publicEvents["GCRestartEEBegin_V1"] = _publicEvents["RestartEEStart"];
            _publicEvents["GCRestartEEEnd_V1"] = _publicEvents["RestartEEStop"];
            _publicEvents["GCFinalizersBegin_V1"] = _publicEvents["FinalizersStart"];
            _publicEvents["GCFinalizersEnd_V1"] = _publicEvents["FinalizersStop"];
        }

        ~CtfTraceEventSource()
        {
            Dispose(false);
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
                {
                    using (CtfDataStream dataStream = new CtfDataStream(stream, _metadata))
                    {
                        if (stopProcessing)
                            return false;

                        ProcessOneChannel(dataStream);
                    }
                }
            }

            return true;
        }

        private void ProcessOneChannel(CtfDataStream stream)
        {
            foreach (CtfEventHeader header in stream.EnumerateEventHeaders())
            {
                if (stopProcessing)
                    return;

                CtfEvent evt = header.Event;
                stream.ReadEventIntoBuffer(evt);

                //TraceEvent traceEvent = Lookup(eventRecord);
                ETWMapping etw = GetTraceEvent(evt);
                if (etw.IsNull)
                    continue;

                var hdr = InitEventRecord(header, stream, etw);
                TraceEvent traceEvent = Lookup(hdr);
                traceEvent.eventRecord = hdr;
                traceEvent.userData = stream.BufferPtr;

                //traceEvent.DebugValidate();
                Dispatch(traceEvent);
            }
        }

        private TraceEventNativeMethods.EVENT_RECORD* InitEventRecord(CtfEventHeader header, CtfDataStream stream, ETWMapping etw)
        {
            _header->EventHeader.Size = (ushort)sizeof(TraceEventNativeMethods.EVENT_TRACE_HEADER);
            _header->EventHeader.Flags = 0;
            if (pointerSize == 8)
                _header->EventHeader.Flags |= TraceEventNativeMethods.EVENT_HEADER_FLAG_64_BIT_HEADER;
            else
                _header->EventHeader.Flags |= TraceEventNativeMethods.EVENT_HEADER_FLAG_32_BIT_HEADER;

            _header->EventHeader.ThreadId = 0;
            _header->EventHeader.ProcessId = 0;
            _header->EventHeader.TimeStamp = 0;
            _header->EventHeader.ProviderId = etw.Guid;
            _header->EventHeader.Version = etw.Version;
            _header->EventHeader.Level = 0;
            _header->EventHeader.Opcode = (byte)etw.Opcode;
            _header->EventHeader.Id = (ushort)etw.Id;

            _header->EventHeader.KernelTime = 0;
            _header->EventHeader.UserTime = 0;
            _header->EventHeader.TimeStamp = (long)header.Timestamp;

            _header->BufferContext = new TraceEventNativeMethods.ETW_BUFFER_CONTEXT();
            _header->BufferContext.ProcessorNumber = 0; // todo

            // ExtendedDataCount
            _header->UserDataLength = (ushort)stream.BufferLength;
            _header->UserData = stream.BufferPtr;
            return _header;
        }

        Dictionary<CtfEvent, ETWMapping> _traceEvents = new Dictionary<CtfEvent, ETWMapping>();
        private ETWMapping GetTraceEvent(CtfEvent evt)
        {
            ETWMapping result;
            if (_traceEvents.TryGetValue(evt, out result))
                return result;

            int i = evt.Name.IndexOf(':');
            if (i != -1)
            {
                string provider = evt.Name.Substring(0, i);
                string eventName = evt.Name.Substring(i + 1);

                Dictionary<string, TraceEvent> lookup = (provider == DotNetPrivate) ? _privateEvents : _publicEvents;
                TraceEvent traceEvent;
                if (lookup.TryGetValue(eventName, out traceEvent))
                    result = new ETWMapping(traceEvent.ProviderGuid, traceEvent.Opcode, traceEvent.ID, 0);
            }

            _traceEvents[evt] = result;
            return result;
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

            //Marshal.FreeHGlobal(new IntPtr(_header));
            base.Dispose(disposing);

            GC.SuppressFinalize(this);
        }
    }
}
