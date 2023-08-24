using Microsoft.Diagnostics.Tracing.Ctf;
using Microsoft.Diagnostics.Tracing.Parsers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Tracing
{
    public sealed class CtfEventMapping
    {
        public string EventName { get; }
        public Guid Guid { get; }
        public TraceEventOpcode Opcode { get; }
        public TraceEventID Id { get; }
        public byte Version { get; }

        public CtfEventMapping(string eventName, Guid guid, int opcode, int id, int version)
        {
            EventName = eventName;
            Guid = guid;
            Opcode = (TraceEventOpcode)opcode;
            Id = (TraceEventID)id;
            Version = (byte)version;
        }
    }

    public sealed unsafe class CtfTraceEventSource : TraceEventDispatcher, IDisposable
    {
        private string _filename;
        private ZipArchive _zip;
        private List<Tuple<ZipArchiveEntry, CtfMetadata>> _channels;
        private TraceEventNativeMethods.EVENT_RECORD* _header;
        private Dictionary<string, CtfEventMapping> _eventMapping;
        private Dictionary<int, string> _processNames = new Dictionary<int, string>();

#if DEBUG
        private StreamWriter _debugOut;
#endif

        public CtfTraceEventSource(string fileName)
        {
            _filename = fileName;
            _zip = ZipFile.Open(fileName, ZipArchiveMode.Read);
            bool success = false;
            try
            {

                _channels = new List<Tuple<ZipArchiveEntry, CtfMetadata>>();
                foreach (ZipArchiveEntry metadataArchive in _zip.Entries.Where(p => Path.GetFileName(p.FullName) == "metadata"))
                {
                    CtfMetadataLegacyParser parser = new CtfMetadataLegacyParser(metadataArchive.Open());
                    CtfMetadata metadata = new CtfMetadata(parser);

                    string path = Path.GetDirectoryName(metadataArchive.FullName);
                    _channels.AddRange(from entry in _zip.Entries
                                       where Path.GetDirectoryName(entry.FullName) == path && Path.GetFileName(entry.FullName).StartsWith("channel")
                                       select new Tuple<ZipArchiveEntry, CtfMetadata>(entry, metadata));

                    pointerSize = Path.GetDirectoryName(metadataArchive.FullName).EndsWith("64-bit") ? 8 : 4;
                }


                var mem = (TraceEventNativeMethods.EVENT_RECORD*)Marshal.AllocHGlobal(sizeof(TraceEventNativeMethods.EVENT_RECORD));
                *mem = default(TraceEventNativeMethods.EVENT_RECORD);
                _header = mem;

                int processors = (from entry in _channels
                                  let filename = entry.Item1.FullName
                                  let i = filename.LastIndexOf('_')
                                  let processor = filename.Substring(i + 1)
                                  select int.Parse(processor)
                                 ).Max() + 1;

                numberOfProcessors = processors;

                // TODO: Need to cleanly separate clocks, but in practice there's only the one clock.
                CtfClock clock = _channels.First().Item2.Clocks.First();

                var firstChannel = (new ChannelList(_channels)).FirstOrDefault();
                if (firstChannel == null)
                {
                    throw new EndOfStreamException("No CTF Information found in ZIP file.");
                }

                long firstEventTimestamp = (long)firstChannel.Current.Timestamp;

                _QPCFreq = (long)clock.Frequency;
                sessionStartTimeQPC = firstEventTimestamp;
                _syncTimeQPC = firstEventTimestamp;
                _syncTimeUTC = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds((clock.Offset - 1) / clock.Frequency);

                _eventMapping = new Dictionary<string, CtfEventMapping>();
                // initialize all parsers that are fundamentally needed for CtfTraceEventSource to work
                GC.KeepAlive(Clr);
                GC.KeepAlive(new ClrPrivateTraceEventParser(this));
                GC.KeepAlive(new LinuxKernelEventParser(this));
                success = true;
#if DEBUG
            //// Uncomment for debug output.
            //_debugOut = File.CreateText("debug.txt");
            //_debugOut.AutoFlush = true;
#endif
            }
            finally
            {
                if (!success)
                {
                    Dispose();      // This closes the ZIP file we opened.  We don't want to leave it dangling.  
                }
            }

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
            ulong lastTimestamp = 0;
            int events = 0;
            ChannelList list = new ChannelList(_channels);
            foreach (ChannelEntry entry in list)
            {
                if (stopProcessing)
                {
                    break;
                }

                CtfEventHeader header = entry.Current;
                CtfEvent evt = header.Event;
                lastTimestamp = header.Timestamp;

                entry.Reader.ReadEventIntoBuffer(evt);
                events++;

#if DEBUG
                if (_debugOut != null)
                {
                    _debugOut.WriteLine($"[{evt.Name}]");
                    _debugOut.WriteLine($"    Process: {header.ProcessName}");
                    _debugOut.WriteLine($"    File: {entry.FileName}");
                    _debugOut.WriteLine($"    File Offset: {entry.Channel.FileOffset}");
                    _debugOut.WriteLine($"    Event #{events}: {evt.Name}");
                }
#endif

                if (!TryGetEventMapping(evt, out CtfEventMapping mapping))
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(header.ProcessName))
                {
                    _processNames[header.Pid] = header.ProcessName;
                }

                var hdr = InitEventRecord(header, entry.Reader, mapping);
                TraceEvent traceEvent = Lookup(hdr);
                traceEvent.eventRecord = hdr;
                traceEvent.userData = entry.Reader.BufferPtr;
                traceEvent.EventTypeUserData = evt;

                traceEvent.DebugValidate();
                Dispatch(traceEvent);
            }

            sessionEndTimeQPC = (long)lastTimestamp;

            return true;
        }

        internal override string ProcessName(int processID, long timeQPC)
        {
            string result;

            if (_processNames.TryGetValue(processID, out result))
            {
                return result;
            }

            return base.ProcessName(processID, timeQPC);
        }

        internal override void RegisterParserImpl(TraceEventParser parser)
        {
            base.RegisterParserImpl(parser);
            foreach (var mapping in parser.EnumerateCtfEventMappings())
            {
                _eventMapping[mapping.EventName] = mapping;
            }
        }

        private TraceEventNativeMethods.EVENT_RECORD* InitEventRecord(CtfEventHeader header, CtfReader stream, CtfEventMapping mapping)
        {
            _header->EventHeader.Size = (ushort)sizeof(TraceEventNativeMethods.EVENT_TRACE_HEADER);
            _header->EventHeader.Flags = 0;
            if (pointerSize == 8)
            {
                _header->EventHeader.Flags |= TraceEventNativeMethods.EVENT_HEADER_FLAG_64_BIT_HEADER;
            }
            else
            {
                _header->EventHeader.Flags |= TraceEventNativeMethods.EVENT_HEADER_FLAG_32_BIT_HEADER;
            }

            _header->EventHeader.TimeStamp = (long)header.Timestamp;
            _header->EventHeader.ProviderId = mapping.Guid;
            _header->EventHeader.Version = mapping.Version;
            _header->EventHeader.Level = 0;
            _header->EventHeader.Opcode = (byte)mapping.Opcode;
            _header->EventHeader.Id = (ushort)mapping.Id;

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

        private bool TryGetEventMapping(CtfEvent evt, out CtfEventMapping mapping)
        {
            var found = _eventMapping.TryGetValue(evt.Name, out mapping);

            Debug.Assert(evt.Name.StartsWith("lttng") || found, evt.Name);

            return found;
        }

        public void ParseMetadata()
        {
            // We don't get this data in LTTng traces (unless we decide to emit them as events later).
            osVersion = new Version("0.0.0.0");
            cpuSpeedMHz = 10;

            // TODO:  This is not IFastSerializable
            /*
            var env = _metadata.Environment;
            var trace = _metadata.Trace;
            userData["hostname"] = env.HostName;
            userData["tracer_name"] = env.TracerName;
            userData["tracer_version"] = env.TracerMajor + "." + env.TracerMinor;
            userData["uuid"] = trace.UUID;
            userData["ctf version"] = trace.Major + "." + trace.Minor;
            */
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_zip != null)
                {
                    _zip.Dispose();
                    _zip = null;
                }
            }

            // TODO
            //Marshal.FreeHGlobal(new IntPtr(_header));
            base.Dispose(disposing);

            GC.SuppressFinalize(this);
        }

        // Each file has streams which have sets of events.  These classes help merge those channels
        // into one chronological stream of events.
        #region Enumeration Helper

        private class ChannelList : IEnumerable<ChannelEntry>
        {
            private List<Tuple<ZipArchiveEntry, CtfMetadata>> _channels;

            public ChannelList(List<Tuple<ZipArchiveEntry, CtfMetadata>> channels)
            {
                _channels = channels;
            }

            public IEnumerator<ChannelEntry> GetEnumerator()
            {
                return new ChannelListEnumerator(_channels);
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return new ChannelListEnumerator(_channels);
            }
        }

        private class ChannelListEnumerator : IEnumerator<ChannelEntry>
        {
            private bool _first = true;
            private List<ChannelEntry> _channels;
            private int _current;

            public ChannelListEnumerator(List<Tuple<ZipArchiveEntry, CtfMetadata>> channels)
            {
                _channels = new List<ChannelEntry>(channels.Select(tuple => new ChannelEntry(tuple.Item1, tuple.Item2)).Where(channel => channel.MoveNext()));
                _current = GetCurrent();
            }

            private int GetCurrent()
            {
                if (_channels.Count == 0)
                {
                    return -1;
                }

                int min = 0;

                for (int i = 1; i < _channels.Count; i++)
                {
                    if (_channels[i].Current.Timestamp < _channels[min].Current.Timestamp)
                    {
                        min = i;
                    }
                }

                return min;
            }

            public ChannelEntry Current
            {
                get { return _current != -1 ? _channels[_current] : null; }
            }

            public void Dispose()
            {
                foreach (var channel in _channels)
                {
                    channel.Dispose();
                }

                _channels = null;
            }

            object System.Collections.IEnumerator.Current
            {
                get { return Current; }
            }

            public bool MoveNext()
            {
                if (_current == -1)
                {
                    return false;
                }

                if (_first)
                {
                    _first = false;
                    return _channels.Count > 0;
                }

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

        private class ChannelEntry : IDisposable
        {
            public string FileName { get; private set; }
            public CtfChannel Channel { get; private set; }
            public CtfReader Reader { get; private set; }
            public CtfEventHeader Current { get { return _events.Current; } }

            private Stream _stream;
            private IEnumerator<CtfEventHeader> _events;

            public ChannelEntry(ZipArchiveEntry zip, CtfMetadata metadata)
            {
                FileName = zip.FullName;
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

                IDisposable enumerator = _events as IDisposable;
                if (enumerator != null)
                {
                    enumerator.Dispose();
                }
            }

            public bool MoveNext()
            {
                return _events.MoveNext();
            }
        }
        #endregion
    }
}
