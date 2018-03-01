using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Microsoft.Diagnostics.Tracing.Ctf;
using Microsoft.Diagnostics.Tracing.Ctf.Contract;
using Microsoft.Diagnostics.Tracing.Ctf.ZippedEvent;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Tracing
{
    public sealed class CtfTraceEventSource : TraceEventDispatcher
    {
        private readonly CtfEventConverter _ctfEventsConverter;
        private readonly Dictionary<int, string> _processNames = new Dictionary<int, string>();
        private readonly ICtfTraceProvider _provider;
        private readonly Dictionary<int, CtfMetadata> _traceIdToMetadata;
        private bool _isDisposed;

#if DEBUG
        private StreamWriter _debugOut;
#endif

        public CtfTraceEventSource(string fileName)
            : this(new ZippedCtfTraceProvider(fileName))
        {
        }

        public CtfTraceEventSource(ICtfTraceProvider provider)
        {
            _isDisposed = false;
            _traceIdToMetadata = new Dictionary<int, CtfMetadata>();
            _provider = provider;
            _ctfEventsConverter = new CtfEventConverter(_provider.PointerSize);
            _provider.NewCtfMetadata += OnNewMetadata;
            _provider.NewCtfEventTraces += OnNewCtfTraces;

#if DEBUG
//// Uncomment for debug output.
//_debugOut = File.CreateText("debug.txt");
//_debugOut.AutoFlush = true;
#endif
        }

        ~CtfTraceEventSource()
        {
            Dispose(false);
        }

        public override int EventsLost => 0;

        public override void StopProcessing()
        {
            _provider.StopProcessing();
            _provider.NewCtfEventTraces -= OnNewCtfTraces;
            _provider.NewCtfMetadata -= OnNewMetadata;
            base.StopProcessing();
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

        public override bool Process()
        {
            _provider.Process();

            return true;
        }

        internal override string ProcessName(int processID, long timeQPC)
        {
            if (_processNames.TryGetValue(processID, out var result))
                return result;

            return base.ProcessName(processID, timeQPC);
        }

        protected override void Dispose(bool disposing)
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            if (disposing)
            {
                _provider.Dispose();
                _provider.NewCtfEventTraces -= OnNewCtfTraces;
                _provider.NewCtfMetadata -= OnNewMetadata;
                _ctfEventsConverter.Dispose();
            }

            base.Dispose(disposing);
        }

        private void OnNewMetadata(ICtfMetadata metadata)
        {
            var parser = new CtfMetadataLegacyParser(metadata.CreateReadOnlyStream());
            if (_traceIdToMetadata.TryGetValue(metadata.TraceId, out var parsedMetadata))
            {
                parsedMetadata.Load(parser);
            }
            else
            {
                parsedMetadata = new CtfMetadata(parser);
                _traceIdToMetadata[metadata.TraceId] = parsedMetadata;
            }
        }

        private unsafe void OnNewCtfTraces(IEnumerable<ICtfEventTrace> ctfTraces)
        {
            ulong lastTimestamp = 0;
#if DEBUG
            int events = 0;
#endif
            var channelEntries = BuildChannelEntriesFromTraces(ctfTraces);
            foreach (var entry in channelEntries)
            {
                if (stopProcessing)
                    break;

                var header = entry.Current;
                var evt = header.Event;
                if (IsFirstEvent()) Initialize(entry, header);

                lastTimestamp = header.Timestamp;

                entry.Reader.ReadEventIntoBuffer(evt);

#if DEBUG
                events++;
                if (_debugOut != null)
                {
                    _debugOut.WriteLine($"[{evt.Name}]");
                    _debugOut.WriteLine($"    Process: {header.ProcessName}");
                    _debugOut.WriteLine($"    File Offset: {entry.Channel.FileOffset}");
                    _debugOut.WriteLine($"    Event #{events}: {evt.Name}");
                }
#endif

                var eventRecord = _ctfEventsConverter.ToEventRecord(header, entry.Reader);
                if (eventRecord == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(header.ProcessName))
                    _processNames[header.Pid] = header.ProcessName;

                var traceEvent = Lookup(eventRecord);
                traceEvent.eventRecord = eventRecord;
                traceEvent.userData = entry.Reader.BufferPtr;
                traceEvent.EventTypeUserData = evt;

                traceEvent.DebugValidate();
                Dispatch(traceEvent);
            }

            sessionEndTimeQPC = (long)lastTimestamp;
        }

        private ChannelList BuildChannelEntriesFromTraces(IEnumerable<ICtfEventTrace> ctfTraces)
        {
            var list = new List<ChannelEntry>();
            foreach (var ctfEventTrace in ctfTraces)
            {
                if (!_traceIdToMetadata.TryGetValue(ctfEventTrace.TraceId, out var currentMetadata))
                    throw new Exception($"Metadata for trace {ctfEventTrace.TraceId} does not exist");

                list.AddRange(ctfEventTrace.EventPackets.Select(s => new ChannelEntry(s, currentMetadata)));
            }

            return new ChannelList(list);
        }

        private void Initialize(ChannelEntry entry, CtfEventHeader header)
        {
            var currentMetadata = entry.Metadata;
            // TODO: Need to cleanly separate clocks, but in practice there's only the one clock.
            var clock = currentMetadata.Clocks.First();
            var time = ConvertEventTimestampToDateTime(header, clock);

            var firstEventTimestamp = (long)header.Timestamp;

            sessionStartTimeQPC = firstEventTimestamp;
            sessionEndTimeQPC = long.MaxValue;
            _syncTimeQPC = firstEventTimestamp;
            _syncTimeUTC = time;
            _QPCFreq = (long)clock.Frequency;

            pointerSize = _provider.PointerSize;
            numberOfProcessors = _provider.ProcessorCount;
        }

        private bool IsFirstEvent()
        {
            return _syncTimeQPC == 0;
        }

        private static DateTime ConvertEventTimestampToDateTime(CtfEventHeader header, CtfClock clock)
        {
            var offset =
                new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds((clock.Offset - 1) / clock.Frequency);
            var ticks = (long)(header.Timestamp * 10000000.0 / clock.Frequency);

            return new DateTime(offset.Ticks + ticks, DateTimeKind.Utc);
        }

        // Each file has streams which have sets of events.  These classes help merge those channels
        // into one chronological stream of events.

        #region Enumeration Helper

        private class ChannelList : IEnumerable<ChannelEntry>
        {
            private readonly IEnumerable<ChannelEntry> _channels;

            public ChannelList(IEnumerable<ChannelEntry> channels)
            {
                _channels = channels;
            }

            public IEnumerator<ChannelEntry> GetEnumerator()
            {
                return new ChannelListEnumerator(_channels);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return new ChannelListEnumerator(_channels);
            }
        }

        private class ChannelListEnumerator : IEnumerator<ChannelEntry>
        {
            private List<ChannelEntry> _channels;
            private int _current;
            private bool _first = true;

            public ChannelListEnumerator(IEnumerable<ChannelEntry> channels)
            {
                _channels = channels.Where(channel => channel.MoveNext()).ToList();
                _current = GetCurrent();
            }

            public ChannelEntry Current => _current != -1 ? _channels[_current] : null;

            public void Dispose()
            {
                foreach (var channel in _channels)
                    channel.Dispose();

                _channels = null;
            }

            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                if (_current == -1)
                    return false;

                if (_first)
                {
                    _first = false;
                    return _channels.Count > 0;
                }

                var hasMore = _channels[_current].MoveNext();
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

            private int GetCurrent()
            {
                if (_channels.Count == 0)
                    return -1;

                var min = 0;

                for (var i = 1; i < _channels.Count; i++)
                    if (_channels[i].Current.Timestamp < _channels[min].Current.Timestamp)
                        min = i;

                return min;
            }
        }

        private class ChannelEntry : IDisposable
        {
            private readonly IEnumerator<CtfEventHeader> _events;

            public ChannelEntry(ICtfEventPacket ctfEventPacket, CtfMetadata metadata)
            {
                Channel = new CtfChannel(ctfEventPacket.CreateReadOnlyStream(), metadata);
                Reader = new CtfReader(Channel, metadata, Channel.CtfStream);
                _events = Reader.EnumerateEventHeaders().GetEnumerator();
                Metadata = metadata;
            }

            public CtfChannel Channel { get; }
            public CtfReader Reader { get; }
            public CtfEventHeader Current => _events.Current;
            public CtfMetadata Metadata { get; }

            public void Dispose()
            {
                Reader.Dispose();
                Channel.Dispose();

                var enumerator = _events;
                if (enumerator != null)
                    enumerator.Dispose();
            }

            public bool MoveNext()
            {
                return _events.MoveNext();
            }
        }

        #endregion
    }
}