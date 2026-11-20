// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;

using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;

namespace Microsoft.Diagnostics.Tracing.Parsers
{
    /// <summary>
    /// Parser for the runtime's <c>System.Runtime.CompilerServices.AsyncProfilerEventSource</c>.
    /// <para>
    /// The provider emits one <c>AsyncEvents</c> event (id 1) containing a batch of encoded sub-events.
    /// Subscribe to <see cref="AsyncEvents"/> for the raw buffer, or use
    /// <see cref="ParseBuffer(byte[], ISubEventSink)"/> to decode V1 (StateMachineAsync) and V2 (RuntimeAsync)
    /// sub-events. Decoded sub-events are delivered to a sink, not exposed as separate TraceEvents.
    /// TraceLog saves an async call-stack index and omits the raw batches by default. Set
    /// <see cref="Etlx.TraceLogOptions.KeepAsyncProfilerEvents"/> or
    /// <see cref="Etlx.TraceLogOptions.KeepAllEvents"/> to retain them in ETLX for raw-event inspection.
    /// </para>
    /// <para>
    /// The wire format is defined by <c>AsyncProfiler.cs</c> and <c>AsyncProfilerEventSource.cs</c>
    /// in dotnet/runtime. Each manifest entry specifies whether an event's payload-length prefix is absent,
    /// one byte, or two bytes. Declared payload lengths let the decoder skip unknown events and appended fields.
    /// </para>
    /// </summary>
    internal sealed class AsyncProfilerTraceEventParser : TraceEventParser
    {
        /// <summary>The EventSource provider name.</summary>
        public const string ProviderName = "System.Runtime.CompilerServices.AsyncProfilerEventSource";

        /// <summary>
        /// Provider GUID derived from <see cref="ProviderName"/> using the EventSource name-to-GUID algorithm.
        /// </summary>
        public static readonly Guid ProviderGuid = new Guid("7892f2f8-f81d-50d6-aea7-594d846c2274");

        /// <summary>The event id of the single raw <c>AsyncEvents</c> event.</summary>
        public const int AsyncEventsEventId = 1;

        /// <summary>The supported async-profiler buffer-header version.</summary>
        public const byte SupportedBufferVersion = 1;

        /// <summary>Creates the raw-event parser for the specified trace source.</summary>
        public AsyncProfilerTraceEventParser(TraceEventSource source) : base(source)
        {
        }

        /// <summary>
        /// Receives raw <c>AsyncEvents</c> events containing encoded buffers.
        /// </summary>
        public event Action<AsyncProfilerTraceEvent> AsyncEvents
        {
            add
            {
                source.RegisterEventTemplate(AsyncEventsTemplate(value));
            }
            remove
            {
                source.UnregisterEventTemplate(value, AsyncEventsEventId, ProviderGuid);
            }
        }

        /// <summary>
        /// Decodes an <c>AsyncEvents</c> buffer and sends its sub-events to <paramref name="sink"/>.
        /// Does not require a <see cref="TraceEventSource"/>.
        /// </summary>
        /// <param name="buffer">The encoded buffer from <see cref="AsyncProfilerTraceEvent.Buffer"/>.</param>
        /// <param name="sink">Receives the decoded sub-events and any parse errors.</param>
        /// <remarks>
        /// Starts with the built-in version-1 event layouts and applies metadata from this buffer.
        /// To retain metadata from earlier buffers, use
        /// <see cref="ParseBuffer(byte[], Manifest, ISubEventSink)"/> with the same manifest across calls.
        /// </remarks>
        public static void ParseBuffer(byte[] buffer, ISubEventSink sink)
        {
            ParseBuffer(buffer, new Manifest(), sink);
        }

        /// <summary>
        /// Decodes an <c>AsyncEvents</c> buffer using <paramref name="manifest"/> to read payload lengths.
        /// Sends decoded sub-events to <paramref name="sink"/> and updates the manifest when metadata is decoded.
        /// Reuse the same manifest across buffers from one process instance so earlier metadata remains available.
        /// </summary>
        /// <param name="buffer">The encoded buffer from <see cref="AsyncProfilerTraceEvent.Buffer"/>.</param>
        /// <param name="manifest">Per-event payload-length widths and versions, updated in place from metadata.</param>
        /// <param name="sink">Receives the decoded sub-events and any parse errors.</param>
        /// <remarks>
        /// Decoding is synchronous. Reads known fields and skips appended fields and advertised unknown events
        /// using their declared payload lengths. New schemas must preserve the known layout and only append fields.
        /// Malformed payloads are reported to <see cref="ISubEventSink.OnParseError"/>; decoding then continues
        /// at the declared payload end. Invalid headers or event framing stop decoding this buffer.
        /// This decoder does not track open call stacks. <see cref="Computers.AsyncProfilerIndexBuilder"/>
        /// maintains that state, handles error recovery, and keeps each process's manifest separate.
        /// </remarks>
        public static void ParseBuffer(byte[] buffer, Manifest manifest, ISubEventSink sink)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            ParseBuffer(buffer, buffer.Length, manifest, sink);
        }

        /// <summary>Decodes the first <paramref name="length"/> bytes of a possibly larger, reused buffer.</summary>
        internal static void ParseBuffer(byte[] buffer, int length, Manifest manifest, ISubEventSink sink)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if ((uint)length > (uint)buffer.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            if (sink == null)
            {
                throw new ArgumentNullException(nameof(sink));
            }

            if (!TryReadValidHeader(buffer, length, sink, out BufferHeader header))
            {
                return;
            }

            int limit = (int)header.TotalSize;
            int index = BufferHeader.Size;
            long timestampQpc = header.StartTimestampQpc;
            for (uint eventIndex = 0; eventIndex < header.EventCount; eventIndex++)
            {
                if (!TryReadSubEventFrame(buffer, header, manifest, sink, ref index, ref timestampQpc,
                    out AsyncEventID eventId, out int payloadEnd))
                {
                    return;
                }

                int payloadStart = index;
                if (!TryDispatchSubEvent(eventId, timestampQpc, header, buffer, payloadStart, payloadEnd, manifest, sink))
                {
                    ReportMalformedPayload(eventId, timestampQpc, header, payloadStart, sink);
                }

                // Skip any bytes not read by the decoder, including unknown events and appended fields.
                index = payloadEnd;
            }

            if (index != limit)
            {
                const string message = "Async-profiler buffer contains logical bytes beyond its declared event count";
                sink.OnParseError(new ParseError(message, index, header, timestampQpc));
            }
        }
        #region private

        protected override string GetProviderName() => ProviderName;

        protected internal override void EnumerateTemplates(
            Func<string, string, EventFilterResponse> eventsToObserve, Action<TraceEvent> callback)
        {
            if (s_templates == null)
            {
                var templates = new TraceEvent[1];
                templates[0] = AsyncEventsTemplate(null);
                s_templates = templates;
            }

            foreach (var template in s_templates)
            {
                bool shouldObserve =
                    eventsToObserve == null ||
                    eventsToObserve(template.ProviderName, template.EventName) == EventFilterResponse.AcceptEvent;
                if (shouldObserve)
                {
                    callback(template);
                }
            }
        }

        private static AsyncProfilerTraceEvent AsyncEventsTemplate(Action<AsyncProfilerTraceEvent> action)
        {
            // AsyncEvents uses task 1 and the Info opcode (0).
            return new AsyncProfilerTraceEvent(
                action, AsyncEventsEventId, 1, "AsyncEvents", Guid.Empty, 0, "Info", ProviderGuid, ProviderName);
        }

        /// <summary>
        /// Reads the manifest-defined payload length without passing the buffer limit.
        /// An absent prefix means the event has no payload; two-byte lengths are little-endian.
        /// </summary>
        private static bool TryReadPayloadLengthPrefix(
            byte[] buffer, AsyncEventID eventId, Manifest manifest,
            ref int index, int limit, out int payloadLength)
        {
            switch (manifest.GetPayloadLengthFieldSize(eventId))
            {
                case PayloadLengthFieldSize.None:
                    payloadLength = 0;
                    return true;
                case PayloadLengthFieldSize.Byte:
                    if (index >= limit)
                    {
                        payloadLength = 0;
                        return false;
                    }

                    payloadLength = buffer[index++];
                    return true;
                case PayloadLengthFieldSize.UShort:
                    if (limit - index < 2)
                    {
                        payloadLength = 0;
                        return false;
                    }

                    payloadLength = buffer[index] | (buffer[index + 1] << 8);
                    index += 2;
                    return true;
                default:
                    payloadLength = 0;
                    return false;
            }
        }

        /// <summary>Validates the buffer header and its declared size, reporting the first problem found.</summary>
        private static bool TryReadValidHeader(byte[] buffer, int length, ISubEventSink sink, out BufferHeader header)
        {
            if (!BufferHeader.TryRead(buffer, out header) || length < BufferHeader.Size)
            {
                sink.OnParseError(new ParseError("Invalid or truncated async-profiler buffer header", 0));
                return false;
            }

            string message;
            int offset;
            if (header.TotalSize < BufferHeader.Size)
            {
                message = "Async-profiler buffer total size is smaller than its header";
                offset = 1;
            }
            else if (header.TotalSize > length)
            {
                message = "Async-profiler buffer is truncated before its declared total size";
                offset = length;
            }
            else
            {
                return true;
            }

            sink.OnParseError(new ParseError(message, offset, header, header.StartTimestampQpc));
            return false;
        }

        /// <summary>
        /// Reads one sub-event's ID, timestamp delta, and payload length, leaving <paramref name="index"/> at the
        /// payload start. Reports invalid framing and returns false, which stops decoding the buffer.
        /// </summary>
        private static bool TryReadSubEventFrame(
            byte[] buffer, in BufferHeader header, Manifest manifest, ISubEventSink sink,
            ref int index, ref long timestampQpc, out AsyncEventID eventId, out int payloadEnd)
        {
            int limit = (int)header.TotalSize;
            eventId = default;
            payloadEnd = 0;
            if (index >= limit)
            {
                const string message = "Async-profiler buffer ended before its declared event count was reached";
                sink.OnParseError(new ParseError(message, index, header, timestampQpc));
                return false;
            }

            eventId = (AsyncEventID)buffer[index++];

            bool hasTimestampDelta =
                AsyncProfilerBufferReader.TryReadCompressedUInt64(buffer, ref index, limit, out ulong deltaTicks);
            bool timestampWouldOverflow =
                deltaTicks > long.MaxValue ||
                timestampQpc > long.MaxValue - (long)deltaTicks;
            if (!hasTimestampDelta || timestampWouldOverflow)
            {
                const string message = "Invalid or truncated sub-event timestamp delta";
                sink.OnParseError(new ParseError(message, index, header, timestampQpc, eventId));
                return false;
            }

            timestampQpc += (long)deltaTicks;

            if (!TryReadPayloadLengthPrefix(buffer, eventId, manifest, ref index, limit, out int payloadLength))
            {
                const string message = "Invalid or truncated sub-event payload-length prefix";
                sink.OnParseError(new ParseError(message, index, header, timestampQpc, eventId));
                return false;
            }

            if (payloadLength > limit - index)
            {
                const string message = "Sub-event payload extends beyond the async-profiler buffer";
                sink.OnParseError(new ParseError(message, index, header, timestampQpc, eventId));
                return false;
            }

            payloadEnd = index + payloadLength;
            return true;
        }

        /// <summary>
        /// Decodes one payload within its declared bounds and calls the matching sink method.
        /// Returns false if the payload is malformed.
        /// </summary>
        private static bool TryDispatchSubEvent(
            AsyncEventID eventId, long timestampQpc, in BufferHeader header,
            byte[] buffer, int payloadStart, int payloadEnd, Manifest manifest, ISubEventSink sink)
        {
            int index = payloadStart;
            switch (eventId)
            {
                case AsyncEventID.CreateRuntimeAsyncContext:
                case AsyncEventID.CreateStateMachineAsyncContext:
                    {
                        if (!AsyncProfilerBufferReader.TryReadCompressedUInt64(buffer, ref index, payloadEnd, out ulong parentId) ||
                            !AsyncProfilerBufferReader.TryReadCompressedUInt64(buffer, ref index, payloadEnd, out ulong dispatcherId))
                        {
                            return false;
                        }

                        sink.OnContextCreate(new AsyncContextEvent(eventId, timestampQpc, header, parentId, dispatcherId));
                        return true;
                    }
                case AsyncEventID.ResumeRuntimeAsyncContext:
                case AsyncEventID.ResumeStateMachineAsyncContext:
                    {
                        if (!AsyncProfilerBufferReader.TryReadCompressedUInt64(buffer, ref index, payloadEnd, out ulong dispatcherId))
                        {
                            return false;
                        }

                        sink.OnContextResume(new AsyncContextEvent(eventId, timestampQpc, header, 0, dispatcherId));
                        return true;
                    }
                case AsyncEventID.SuspendRuntimeAsyncContext:
                case AsyncEventID.SuspendStateMachineAsyncContext:
                    sink.OnContextSuspend(new AsyncContextEvent(eventId, timestampQpc, header, 0, 0));
                    return true;
                case AsyncEventID.CompleteRuntimeAsyncContext:
                case AsyncEventID.CompleteStateMachineAsyncContext:
                    sink.OnContextComplete(new AsyncContextEvent(eventId, timestampQpc, header, 0, 0));
                    return true;
                case AsyncEventID.UnwindRuntimeAsyncException:
                case AsyncEventID.UnwindStateMachineAsyncException:
                    {
                        if (!AsyncProfilerBufferReader.TryReadCompressedUInt32(buffer, ref index, payloadEnd, out uint unwoundFrames))
                        {
                            return false;
                        }

                        sink.OnException(new AsyncUnwindEvent(eventId, timestampQpc, header, unwoundFrames));
                        return true;
                    }
                case AsyncEventID.CreateRuntimeAsyncCallstack:
                case AsyncEventID.ResumeRuntimeAsyncCallstack:
                case AsyncEventID.SuspendRuntimeAsyncCallstack:
                case AsyncEventID.ResumeStateMachineAsyncCallstack:
                case AsyncEventID.AppendStateMachineAsyncCallstack:
                    return TryDispatchCallstack(eventId, timestampQpc, header, buffer, index, payloadEnd, sink);
                case AsyncEventID.ResumeRuntimeAsyncMethod:
                case AsyncEventID.ResumeStateMachineAsyncMethod:
                    sink.OnMethodResume(new AsyncMethodEvent(eventId, timestampQpc, header));
                    return true;
                case AsyncEventID.CompleteRuntimeAsyncMethod:
                case AsyncEventID.CompleteStateMachineAsyncMethod:
                    sink.OnMethodComplete(new AsyncMethodEvent(eventId, timestampQpc, header));
                    return true;
                case AsyncEventID.ResetAsyncThreadContext:
                    sink.OnResetThreadContext(new AsyncNeutralEvent(eventId, timestampQpc, header));
                    return true;
                case AsyncEventID.ResetAsyncContinuationWrapperIndex:
                    sink.OnResetContinuationWrapperIndex(new AsyncNeutralEvent(eventId, timestampQpc, header));
                    return true;
                case AsyncEventID.AsyncProfilerMetadata:
                    {
                        if (!AsyncMetadataEvent.TryRead(
                            timestampQpc, header, buffer, ref index, payloadEnd, out AsyncMetadataEvent metadata))
                        {
                            return false;
                        }

                        // Apply metadata before decoding subsequent events.
                        manifest.Apply(metadata);
                        sink.OnMetadata(metadata);
                        return true;
                    }
                case AsyncEventID.AsyncProfilerSyncClock:
                    {
                        if (!AsyncProfilerBufferReader.TryReadCompressedUInt64(buffer, ref index, payloadEnd, out ulong qpcSync) ||
                            !AsyncProfilerBufferReader.TryReadCompressedUInt64(buffer, ref index, payloadEnd, out ulong utcSync))
                        {
                            return false;
                        }

                        sink.OnSyncClock(new AsyncSyncClockEvent(timestampQpc, header, qpcSync, utcSync));
                        return true;
                    }
                default:
                    // Report the unknown event; ParseBuffer skips its declared payload.
                    sink.OnUnknown(new AsyncUnknownEvent(eventId, timestampQpc, header, payloadEnd - payloadStart));
                    return true;
            }
        }

        /// <summary>
        /// Decodes a call-stack payload. Sinks with reusable buffers decode the payload themselves.
        /// </summary>
        private static bool TryDispatchCallstack(
            AsyncEventID eventId, long timestampQpc, in BufferHeader header,
            byte[] buffer, int index, int payloadEnd, ISubEventSink sink)
        {
            if (sink is ICallstackPayloadSink payloadSink)
            {
                return payloadSink.TryOnCallstack(eventId, timestampQpc, header, buffer, ref index, payloadEnd);
            }

            if (!AsyncCallstackEvent.TryRead(
                eventId, timestampQpc, header, buffer, ref index, payloadEnd, out AsyncCallstackEvent callstack))
            {
                return false;
            }

            sink.OnCallstack(callstack);
            return true;
        }

        /// <summary>Reports a payload error at the start of the affected event's payload.</summary>
        private static void ReportMalformedPayload(
            AsyncEventID eventId, long timestampQpc,
            in BufferHeader header, int payloadStart, ISubEventSink sink)
        {
            string message = "Invalid or truncated payload for " + eventId;
            sink.OnParseError(new ParseError(message, payloadStart, header, timestampQpc, eventId));
        }
        private static volatile TraceEvent[] s_templates;

        #endregion
    }

    /// <summary>
    /// Raw <c>AsyncEvents</c> event from ETW or EventPipe.
    /// Use <see cref="AsyncProfilerTraceEventParser.ParseBuffer(byte[], ISubEventSink)"/>
    /// to decode <see cref="Buffer"/>.
    /// </summary>
    internal sealed class AsyncProfilerTraceEvent : TraceEvent
    {
        internal AsyncProfilerTraceEvent(
            Action<AsyncProfilerTraceEvent> action, int eventID, int task, string taskName,
            Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            m_target = action;
        }

        /// <summary>The declared buffer length, in bytes, stored at the start of the event payload.</summary>
        public int Length
        {
            get
            {
                if (EventDataLength < sizeof(int))
                {
                    throw new InvalidDataException("AsyncEvents payload is missing its length prefix.");
                }

                return GetInt32At(0);
            }
        }

        /// <summary>The encoded async-profiler buffer, excluding the event's length prefix.</summary>
        public byte[] Buffer
        {
            get
            {
                if (!TryGetBuffer(out byte[] buffer))
                {
                    throw new InvalidDataException("Invalid or truncated AsyncEvents envelope.");
                }

                return buffer;
            }
        }

        public override string[] PayloadNames { get { return s_payloadNames; } }

        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            XmlAttrib(sb, "Length", Length);
            sb.Append("/>");
            return sb;
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Length;
                case 1:
                    return Buffer;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        #region private

        /// <summary>Copies the encoded buffer if the event payload contains its complete declared length.</summary>
        internal bool TryGetBuffer(out byte[] buffer)
        {
            buffer = null;
            return TryGetBuffer(ref buffer, out _);
        }

        /// <summary>
        /// Copies the encoded buffer into <paramref name="buffer"/>, replacing it only when it is too small.
        /// Only the first <paramref name="length"/> bytes belong to this event.
        /// </summary>
        internal bool TryGetBuffer(ref byte[] buffer, out int length)
        {
            // Let the caller handle malformed payloads instead of asserting during event dispatch.
            length = 0;
            if (EventDataLength < sizeof(int))
            {
                return false;
            }

            int declaredLength = GetInt32At(0);
            bool hasValidLength = declaredLength >= 0 && declaredLength <= EventDataLength - sizeof(int);
            if (!hasValidLength)
            {
                return false;
            }

            if (buffer == null || buffer.Length < declaredLength)
            {
                buffer = new byte[declaredLength];
            }

            EventData(buffer, 0, sizeof(int), declaredLength);
            length = declaredLength;
            return true;
        }

        protected internal override void Dispatch()
        {
            m_target?.Invoke(this);
        }

        protected internal override Delegate Target
        {
            get { return m_target; }
            set { m_target = (Action<AsyncProfilerTraceEvent>)value; }
        }

        private static readonly string[] s_payloadNames = new[] { "Length", "Buffer" };
        private event Action<AsyncProfilerTraceEvent> m_target;

        #endregion
    }
}
