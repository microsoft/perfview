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

        public ETWMapping(Guid guid, int opcode, int id, int version)
        {
            Guid = guid;
            Opcode = (TraceEventOpcode)opcode;
            Id = (TraceEventID)id;
            Version = (byte)version;
        }
    }

    public unsafe sealed class CtfTraceEventSource : TraceEventDispatcher, IDisposable
    {
        private string _filename;
        private ZipArchive _zip;
        private CtfMetadata _metadata;
        private ZipArchiveEntry[] _channels;
        private TraceEventNativeMethods.EVENT_RECORD* _header;
        private Dictionary<string, ETWMapping> _eventMapping;

        public CtfTraceEventSource(string fileName)
        {
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

            _eventMapping = InitEventMap();
        }
        
        private static Dictionary<string, ETWMapping> InitEventMap()
        {
            Dictionary<string, ETWMapping> result = new Dictionary<string, ETWMapping>();
            result["DotNETRuntime:SetGCHandle"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 33, 30, 0);
            result["DotNETRuntimePrivate:PrvSetGCHandle"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 42, 194, 0);
            result["DotNETRuntime:AppDomainLoad_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 41, 156, 1);
            result["DotNETRuntime:ThreadCreated"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 50, 85, 0);
            result["DotNETRuntime:GCCreateSegment_V1"] = new ETWMapping(new Guid("e13c0d23-ccbc-4e12-931b-d9cc2eee27e4"), 6, 5, 1);
            result["DotNETRuntimePrivate:SecurityCatchCall_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 42, 122, 1);
            result["DotNETRuntimePrivate:AllocRequest"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 97, 310, 0);
            result["DotNETRuntime:AssemblyLoad_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 37, 154, 1);
            result["DotNETRuntime:MethodJittingStarted_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 42, 145, 1);
            result["DotNETRuntime:MethodJitInliningSucceeded"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 83, 185, 0);
            result["DotNETRuntime:MethodLoadVerbose_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 37, 143, 1);
            result["DotNETRuntime:GCAllocationTick_V3"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 11, 10, 3);
            result["DotNETRuntime:DomainModuleLoad_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 45, 151, 1);
            result["DotNETRuntime:GCSuspendEEBegin_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 10, 9, 1);
            result["DotNETRuntime:GCSuspendEEEnd_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 137, 8, 1);
            result["DotNETRuntime:GCRestartEEBegin_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 136, 7, 1);
            result["DotNETRuntime:GCRestartEEEnd_V1"] = new ETWMapping(new Guid("e13c0d23-ccbc-4e12-931b-d9cc2eee27e4"), 8, 3, 1);
            result["DotNETRuntime:GCFinalizersBegin_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 19, 14, 1);
            result["DotNETRuntime:GCFinalizersEnd_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 15, 13, 1);
            result["DotNETRuntime:FinalizeObject"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 32, 29, 0);
            result["DotNETRuntime:GCTriggered"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 35, 35, 0);
            result["DotNETRuntime:GCGenerationRange"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 27, 23, 0);
            result["DotNETRuntime:GCMarkWithType"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 202, 202, 0);
            result["DotNETRuntime:GCGlobalHeapHistory_V2"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 205, 205, 2);

            // TODO: This event needs to be fixed in the linux source.
            //result["DotNETRuntime:MethodILToNativeMap"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 87, 190, 0);
            result["DotNETRuntime:MethodILToNativeMap"] = new ETWMapping();

            result["DotNETRuntime:GCJoin_V2"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 203, 203, 2);
            result["DotNETRuntime:PinObjectAtGCTime"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 44, 198, 0);
            result["DotNETRuntimePrivate:PinPlugAtGCTime"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 45, 199, 0);
            result["DotNETRuntime:GCBulkMovedObjectRanges"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 26, 22, 0);
            result["DotNETRuntime:GCBulkSurvivingObjectRanges"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 25, 21, 0);

            result["DotNETRuntime:GCStart"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 1, 0);
            result["DotNETRuntime:GCStart_V2"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 1, 2);
            result["DotNETRuntime:GCEnd_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 2, 2, 1);

            result["DotNETRuntime:GCPerHeapHistory_V3"] = new ETWMapping();
            result["DotNETRuntime:GCPerHeapHistory_V3_1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 204, 204, 3);


            result["DotNETRuntime:GCHeapStats_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 133, 4, TraceEvent.SplitEventVersion);
            result["DotNETRuntime:GCHeapStats_V1_1"] = new ETWMapping();

            result["DotNETRuntime:RuntimeInformationStart"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 187, TraceEvent.SplitEventVersion);
            result["DotNETRuntime:RuntimeInformationStart_1"] = new ETWMapping();

            result["DotNETRuntime:ModuleLoad_V2"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 33, 152, TraceEvent.SplitEventVersion);
            result["DotNETRuntime:ModuleLoad_V2_1"] = new ETWMapping();


            result["DotNETRuntime:MethodJitInliningFailed"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 84, 186, TraceEvent.SplitEventVersion);
            result["DotNETRuntime:MethodJitInliningFailed_1"] = new ETWMapping();
            
            result["DotNETRuntime:MethodJitTailCallSucceeded"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 85, 188, TraceEvent.SplitEventVersion);
            result["DotNETRuntime:MethodJitTailCallSucceeded_1"] = new ETWMapping();

            result["DotNETRuntime:MethodJitTailCallFailed"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 86, 189, TraceEvent.SplitEventVersion);
            result["DotNETRuntime:MethodJitTailCallFailed_1"] = new ETWMapping();

            // Private events

            result["DotNETRuntimePrivate:PrvFinalizeObject"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 39, 192, 0);
            result["DotNETRuntimePrivate:GCDecision"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 132, 1, 0);

            return result;
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
            int events = 0;
            ChannelList list = new ChannelList(_channels, _metadata);
            foreach (ChannelEntry entry in list)
            {
                if (stopProcessing)
                    break;


                CtfEventHeader header = entry.Current;

                // Despite the content length field in packets, LTTng still seems to put some "null value"
                // packets at the end of a data stream.  This check simply discards those null events.  There
                // never seems to be anything after them in the event stream, but we'll continue processing
                // just in case.
                if (header.Event.ID == 0 && header.Tid == 0 && header.Pid == 0 && header.Timestamp == 0)
                    continue;

                CtfEvent evt = header.Event;
                entry.Reader.ReadEventIntoBuffer(evt);
                events++;

                ETWMapping etw = GetTraceEvent(evt);

                if (etw.IsNull)
                    continue;

                var hdr = InitEventRecord(header, entry.Reader, etw);
                TraceEvent traceEvent = Lookup(hdr);
                traceEvent.eventRecord = hdr;
                traceEvent.userData = entry.Reader.BufferPtr;

                traceEvent.DebugValidate();
                Dispatch(traceEvent);
            }

            return true;
        }

        private TraceEventNativeMethods.EVENT_RECORD* InitEventRecord(CtfEventHeader header, CtfReader stream, ETWMapping etw)
        {
            _header->EventHeader.Size = (ushort)sizeof(TraceEventNativeMethods.EVENT_TRACE_HEADER);
            _header->EventHeader.Flags = 0;
            if (pointerSize == 8)
                _header->EventHeader.Flags |= TraceEventNativeMethods.EVENT_HEADER_FLAG_64_BIT_HEADER;
            else
                _header->EventHeader.Flags |= TraceEventNativeMethods.EVENT_HEADER_FLAG_32_BIT_HEADER;

            _header->EventHeader.TimeStamp = (long)header.Timestamp;
            _header->EventHeader.ProviderId = etw.Guid;
            _header->EventHeader.Version = etw.Version;
            _header->EventHeader.Level = 0;
            _header->EventHeader.Opcode = (byte)etw.Opcode;
            _header->EventHeader.Id = (ushort)etw.Id;

            _header->UserDataLength = (ushort)stream.BufferLength;
            _header->UserData = stream.BufferPtr;

            // TODO: Set these properties based on Ctf context
            _header->BufferContext = new TraceEventNativeMethods.ETW_BUFFER_CONTEXT();
            _header->BufferContext.ProcessorNumber = 0;
            _header->EventHeader.ThreadId = header.Tid;
            _header->EventHeader.ProcessId = header.Pid;
            _header->EventHeader.KernelTime = 0;
            _header->EventHeader.UserTime = 0;

            return _header;
        }

        private ETWMapping GetTraceEvent(CtfEvent evt)
        {
            ETWMapping result;
            _eventMapping.TryGetValue(evt.Name, out result);

            Debug.Assert(evt.Name.StartsWith("lttng") || _eventMapping.ContainsKey(evt.Name), evt.Name);

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

            // TODO
            //Marshal.FreeHGlobal(new IntPtr(_header));
            base.Dispose(disposing);

            GC.SuppressFinalize(this);
        }

        // Each file has streams which have sets of events.  These classes help merge those channels
        // into one chronological stream of events.
        #region Enumeration Helper

        class ChannelList : IEnumerable<ChannelEntry>
        {
            ZipArchiveEntry[] _channels;
            CtfMetadata _metadata;

            public ChannelList(ZipArchiveEntry[] channels, CtfMetadata metadata)
            {
                _channels = channels;
                _metadata = metadata;
            }

            public IEnumerator<ChannelEntry> GetEnumerator()
            {
                return new ChannelListEnumerator(_channels, _metadata);
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return new ChannelListEnumerator(_channels, _metadata);
            }
        }

        class ChannelListEnumerator : IEnumerator<ChannelEntry>
        {
            List<ChannelEntry> _channels;
            int _current;

            public ChannelListEnumerator(ZipArchiveEntry[] channels, CtfMetadata metadata)
            {
                _channels = new List<ChannelEntry>(channels.Select(channel => new ChannelEntry(channel, metadata)).Where(channel => channel.MoveNext()));
                _current = GetCurrent();
            }

            private int GetCurrent()
            {
                if (_channels.Count == 0)
                    return -1;

                int min = 0;

                for (int i = 1; i < _channels.Count; i++)
                    if (_channels[i].Current.Timestamp < _channels[min].Current.Timestamp)
                        min = i;

                return min;
            }

            public ChannelEntry Current
            {
                get { return _current != -1 ? _channels[_current] : null; }
            }

            public void Dispose()
            {
                foreach (var channel in _channels)
                    channel.Dispose();

                _channels = null;
            }

            object System.Collections.IEnumerator.Current
            {
                get { return Current; }
            }

            public bool MoveNext()
            {
                if (_current == -1)
                    return false;

                bool hasMore = _channels[_current].MoveNext();
                if (!hasMore)
                {
                    _channels[_current].Dispose();
                    _channels.RemoveAt(_current);
                }

                _current = GetCurrent();
                return _current != -1;
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }
        }

        class ChannelEntry : IDisposable
        {
            public CtfStream CtfStream { get { return Channel.CtfStream; } }
            public CtfChannel Channel { get; private set; }
            public CtfReader Reader { get; private set; }
            public CtfEventHeader Current { get { return _events.Current; } }

            private Stream _stream;
            private IEnumerator<CtfEventHeader> _events;

            public ChannelEntry(ZipArchiveEntry zip, CtfMetadata metadata)
            {
                _stream = zip.Open();
                Channel = new CtfChannel(_stream, metadata);
                Reader = new CtfReader(Channel, metadata, Channel.CtfStream);
                _events = Reader.EnumerateEventHeaders().GetEnumerator();
            }

            public void Dispose()
            {
                Reader.Dispose();
                Channel.Dispose();
                _stream.Dispose();
            }

            public bool MoveNext()
            {
                return _events.MoveNext();
            }
        }
        #endregion
    }
}
