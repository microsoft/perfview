// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler
{
    /// <summary>
    /// Event IDs inside an <c>AsyncEvents</c> buffer. Values match the runtime's event IDs.
    /// IDs 1-10 are V2 (RuntimeAsync), IDs 11-19 are V1 (StateMachineAsync), and IDs 20-23 are shared events.
    /// </summary>
    internal enum AsyncEventID : byte
    {
        // V2 (RuntimeAsync) events.
        CreateRuntimeAsyncContext = 1,
        ResumeRuntimeAsyncContext = 2,
        SuspendRuntimeAsyncContext = 3,
        CompleteRuntimeAsyncContext = 4,
        UnwindRuntimeAsyncException = 5,
        CreateRuntimeAsyncCallstack = 6,
        ResumeRuntimeAsyncCallstack = 7,
        SuspendRuntimeAsyncCallstack = 8,
        ResumeRuntimeAsyncMethod = 9,
        CompleteRuntimeAsyncMethod = 10,

        // V1 (StateMachineAsync) events.
        CreateStateMachineAsyncContext = 11,
        ResumeStateMachineAsyncContext = 12,
        SuspendStateMachineAsyncContext = 13,
        CompleteStateMachineAsyncContext = 14,
        UnwindStateMachineAsyncException = 15,
        ResumeStateMachineAsyncCallstack = 16,
        ResumeStateMachineAsyncMethod = 17,
        CompleteStateMachineAsyncMethod = 18,
        AppendStateMachineAsyncCallstack = 19,

        // Events shared by both instrumentation families.
        ResetAsyncThreadContext = 20,
        ResetAsyncContinuationWrapperIndex = 21,
        AsyncProfilerMetadata = 22,
        AsyncProfilerSyncClock = 23,
    }

    /// <summary>
    /// Payload-length prefix width in bytes. Values match the runtime event manifest.
    /// None means no prefix and no payload.
    /// </summary>
    internal enum PayloadLengthFieldSize : byte
    {
        None = 0,
        Byte = 1,
        UShort = 2,
    }

    /// <summary>
    /// The instrumentation family that produced an async call stack.
    /// V2 records continuation IPs; V1 records state-machine <c>MoveNext</c> IPs and signed compiler states.
    /// Both use native instruction pointers. V1/V2 is separate from the buffer and event schema versions.
    /// </summary>
    internal enum AsyncCallstackKind : byte
    {
        /// <summary>V2 runtime-async instrumentation.</summary>
        RuntimeAsync = 0,

        /// <summary>V1 compiler-generated state-machine instrumentation.</summary>
        StateMachineAsync = 1,
    }

    /// <summary>
    /// Default payload-length widths and event categories for known event IDs.
    /// The widths describe schema version 1; metadata can replace them through <see cref="Manifest"/>.
    /// </summary>
    internal static class AsyncEventInfo
    {
        /// <summary>Returns the default payload-length prefix width, or None for unknown IDs.</summary>
        public static PayloadLengthFieldSize GetPayloadLengthFieldSize(AsyncEventID eventId)
        {
            int index = (byte)eventId - 1;
            // The unsigned comparison also rejects event ID 0.
            return (uint)index < (uint)s_payloadLengthFieldSizes.Length
                ? s_payloadLengthFieldSizes[index]
                : PayloadLengthFieldSize.None;
        }

        /// <summary>True if <paramref name="eventId"/> is a V1 (StateMachineAsync) event (ids 11-19).</summary>
        public static bool IsStateMachine(AsyncEventID eventId) =>
            eventId >= AsyncEventID.CreateStateMachineAsyncContext &&
            eventId <= AsyncEventID.AppendStateMachineAsyncCallstack;

        /// <summary>True if <paramref name="eventId"/> is a V2 (RuntimeAsync) event (ids 1-10).</summary>
        public static bool IsRuntime(AsyncEventID eventId) =>
            eventId >= AsyncEventID.CreateRuntimeAsyncContext && eventId <= AsyncEventID.CompleteRuntimeAsyncMethod;

        /// <summary>True if <paramref name="eventId"/> carries a call-stack payload (IDs 6, 7, 8, 16, 19).</summary>
        public static bool IsCallstack(AsyncEventID eventId) =>
            eventId == AsyncEventID.CreateRuntimeAsyncCallstack ||
            eventId == AsyncEventID.ResumeRuntimeAsyncCallstack ||
            eventId == AsyncEventID.SuspendRuntimeAsyncCallstack ||
            eventId == AsyncEventID.ResumeStateMachineAsyncCallstack ||
            eventId == AsyncEventID.AppendStateMachineAsyncCallstack;

        /// <summary>
        /// Returns the frame format for a call-stack event.
        /// V1 frames include compiler states; V2 frames contain only continuation IPs.
        /// </summary>
        public static AsyncCallstackKind GetCallstackKind(AsyncEventID eventId) =>
            eventId == AsyncEventID.ResumeStateMachineAsyncCallstack ||
            eventId == AsyncEventID.AppendStateMachineAsyncCallstack
                ? AsyncCallstackKind.StateMachineAsync
                : AsyncCallstackKind.RuntimeAsync;

        #region private

        // Indexed by (byte)AsyncEventID - 1, in ascending event-ID order.
        private static readonly PayloadLengthFieldSize[] s_payloadLengthFieldSizes =
        {
            PayloadLengthFieldSize.Byte,    // 1  CreateRuntimeAsyncContext
            PayloadLengthFieldSize.Byte,    // 2  ResumeRuntimeAsyncContext
            PayloadLengthFieldSize.None,    // 3  SuspendRuntimeAsyncContext
            PayloadLengthFieldSize.None,    // 4  CompleteRuntimeAsyncContext
            PayloadLengthFieldSize.Byte,    // 5  UnwindRuntimeAsyncException
            PayloadLengthFieldSize.UShort,  // 6  CreateRuntimeAsyncCallstack
            PayloadLengthFieldSize.UShort,  // 7  ResumeRuntimeAsyncCallstack
            PayloadLengthFieldSize.UShort,  // 8  SuspendRuntimeAsyncCallstack
            PayloadLengthFieldSize.None,    // 9  ResumeRuntimeAsyncMethod
            PayloadLengthFieldSize.None,    // 10 CompleteRuntimeAsyncMethod
            PayloadLengthFieldSize.Byte,    // 11 CreateStateMachineAsyncContext
            PayloadLengthFieldSize.Byte,    // 12 ResumeStateMachineAsyncContext
            PayloadLengthFieldSize.None,    // 13 SuspendStateMachineAsyncContext
            PayloadLengthFieldSize.None,    // 14 CompleteStateMachineAsyncContext
            PayloadLengthFieldSize.Byte,    // 15 UnwindStateMachineAsyncException
            PayloadLengthFieldSize.UShort,  // 16 ResumeStateMachineAsyncCallstack
            PayloadLengthFieldSize.None,    // 17 ResumeStateMachineAsyncMethod
            PayloadLengthFieldSize.None,    // 18 CompleteStateMachineAsyncMethod
            PayloadLengthFieldSize.UShort,  // 19 AppendStateMachineAsyncCallstack
            PayloadLengthFieldSize.None,    // 20 ResetAsyncThreadContext
            PayloadLengthFieldSize.None,    // 21 ResetAsyncContinuationWrapperIndex
            PayloadLengthFieldSize.UShort,  // 22 AsyncProfilerMetadata
            PayloadLengthFieldSize.Byte,    // 23 AsyncProfilerSyncClock
        };

        #endregion
    }

    /// <summary>
    /// Payload-length prefix widths and schema versions for one process instance.
    /// Starts with the built-in version-1 layouts; metadata replaces the entries it lists.
    /// </summary>
    /// <remarks>
    /// Retain the manifest across buffers from the same process instance; do not share it between processes.
    /// <see cref="Computers.AsyncProfilerIndexBuilder"/> manages this state. Direct decoder callers pass
    /// their retained manifest to
    /// <see cref="AsyncProfilerTraceEventParser.ParseBuffer(byte[], Manifest, ISubEventSink)"/>.
    /// The overload without a manifest creates fresh defaults for each call.
    /// </remarks>
    internal sealed class Manifest
    {
        /// <summary>Creates a manifest seeded with the known version-1 event layouts.</summary>
        public Manifest()
        {
            // Unknown IDs keep no prefix and version 0 until metadata supplies their entries.
            for (int id = 1; id <= 23; id++)
            {
                _fieldSize[id] = AsyncEventInfo.GetPayloadLengthFieldSize((AsyncEventID)id);
                _version[id] = 1;
            }
        }

        /// <summary>The payload-length prefix width to use when framing <paramref name="eventId"/>.</summary>
        public PayloadLengthFieldSize GetPayloadLengthFieldSize(AsyncEventID eventId) => _fieldSize[(byte)eventId];

        /// <summary>
        /// The most recently advertised schema version of <paramref name="eventId"/>, or the built-in version 1
        /// for a known event before metadata is seen. Unknown events default to 0.
        /// </summary>
        public byte GetVersion(AsyncEventID eventId) => _version[(byte)eventId];

        /// <summary>Applies the manifest carried by an <c>AsyncProfilerMetadata</c> sub-event.</summary>
        public void Apply(in AsyncMetadataEvent metadata) => Apply(metadata.Manifest);

        /// <summary>
        /// Applies a set of manifest entries, overriding the framing width and version for each listed id.
        /// Unlisted ids retain their previous entries; null or empty input leaves the manifest unchanged.
        /// </summary>
        public void Apply(AsyncManifestEntry[] entries)
        {
            if (entries == null)
            {
                return;
            }

            foreach (AsyncManifestEntry entry in entries)
            {
                byte id = (byte)entry.EventId;
                _fieldSize[id] = entry.PayloadLengthFieldSize;
                _version[id] = entry.Version;
            }
        }

        #region private

        // Indexed by the raw byte event ID, including IDs not yet known to the decoder.
        private readonly PayloadLengthFieldSize[] _fieldSize = new PayloadLengthFieldSize[256];
        private readonly byte[] _version = new byte[256];

        #endregion
    }

    /// <summary>
    /// The fixed-size header at the start of every <c>AsyncEvents</c> buffer (37 bytes, little-endian).
    /// Includes the emitting OS thread and the batch's start/end QPC timestamps.
    /// </summary>
    internal readonly struct BufferHeader
    {
        /// <summary>The size (in bytes) of the serialized header.</summary>
        public const int Size = 37;

        public readonly byte Version;
        public readonly uint TotalSize;
        public readonly uint AsyncThreadContextId;
        public readonly ulong OsThreadId;
        public readonly uint EventCount;
        public readonly long StartTimestampQpc;
        public readonly long EndTimestampQpc;

        public BufferHeader(
            byte version, uint totalSize, uint asyncThreadContextId, ulong osThreadId,
            uint eventCount, long startTimestampQpc, long endTimestampQpc)
        {
            Version = version;
            TotalSize = totalSize;
            AsyncThreadContextId = asyncThreadContextId;
            OsThreadId = osThreadId;
            EventCount = eventCount;
            StartTimestampQpc = startTimestampQpc;
            EndTimestampQpc = endTimestampQpc;
        }

        /// <summary>
        /// Reads a version-1 header. Returns false if the buffer is null, too short, or uses another version.
        /// </summary>
        public static bool TryRead(byte[] buffer, out BufferHeader header)
        {
            header = default;
            bool hasCompleteHeader = buffer != null && buffer.Length >= Size;
            if (!hasCompleteHeader || buffer[0] != 1)
            {
                return false;
            }

            int index = 1;
            uint totalSize = AsyncProfilerBufferReader.ReadUInt32LE(buffer, ref index);
            uint asyncThreadContextId = AsyncProfilerBufferReader.ReadUInt32LE(buffer, ref index);
            ulong osThreadId = AsyncProfilerBufferReader.ReadUInt64LE(buffer, ref index);
            uint eventCount = AsyncProfilerBufferReader.ReadUInt32LE(buffer, ref index);
            ulong startTimestampQpc = AsyncProfilerBufferReader.ReadUInt64LE(buffer, ref index);
            ulong endTimestampQpc = AsyncProfilerBufferReader.ReadUInt64LE(buffer, ref index);

            header = new BufferHeader(
                buffer[0], totalSize, asyncThreadContextId, osThreadId, eventCount,
                (long)startTimestampQpc, (long)endTimestampQpc);
            return true;
        }
    }

    /// <summary>An async-context create, resume, suspend, or complete event.</summary>
    internal readonly struct AsyncContextEvent
    {
        public readonly AsyncEventID EventId;
        public readonly long TimestampQpc;
        public readonly BufferHeader Header;

        /// <summary>The parent dispatcher ID for Create events; zero otherwise.</summary>
        public readonly ulong ParentDispatcherId;

        /// <summary>
        /// The dispatcher ID for Create/Resume events.
        /// Suspend/Complete apply to the thread's current context and store zero here.
        /// </summary>
        public readonly ulong DispatcherId;

        public AsyncContextEvent(
            AsyncEventID eventId, long timestampQpc, in BufferHeader header,
            ulong parentDispatcherId, ulong dispatcherId)
        {
            EventId = eventId;
            TimestampQpc = timestampQpc;
            Header = header;
            ParentDispatcherId = parentDispatcherId;
            DispatcherId = dispatcherId;
        }

        public ulong OsThreadId => Header.OsThreadId;
        public uint AsyncThreadContextId => Header.AsyncThreadContextId;
        public bool IsStateMachine => AsyncEventInfo.IsStateMachine(EventId);
    }

    /// <summary>An async method resume or normal completion event.</summary>
    internal readonly struct AsyncMethodEvent
    {
        public readonly AsyncEventID EventId;
        public readonly long TimestampQpc;
        public readonly BufferHeader Header;

        public AsyncMethodEvent(AsyncEventID eventId, long timestampQpc, in BufferHeader header)
        {
            EventId = eventId;
            TimestampQpc = timestampQpc;
            Header = header;
        }

        public ulong OsThreadId => Header.OsThreadId;
        public uint AsyncThreadContextId => Header.AsyncThreadContextId;
        public bool IsStateMachine => AsyncEventInfo.IsStateMachine(EventId);
    }

    /// <summary>A sub-event recording that an exception unwound some number of async frames.</summary>
    internal readonly struct AsyncUnwindEvent
    {
        public readonly AsyncEventID EventId;
        public readonly long TimestampQpc;
        public readonly BufferHeader Header;
        public readonly uint UnwoundFrameCount;

        public AsyncUnwindEvent(AsyncEventID eventId, long timestampQpc, in BufferHeader header, uint unwoundFrameCount)
        {
            EventId = eventId;
            TimestampQpc = timestampQpc;
            Header = header;
            UnwoundFrameCount = unwoundFrameCount;
        }

        public ulong OsThreadId => Header.OsThreadId;
        public bool IsStateMachine => AsyncEventInfo.IsStateMachine(EventId);
    }

    /// <summary>A thread-context or continuation-wrapper-index reset shared by V1 and V2.</summary>
    internal readonly struct AsyncNeutralEvent
    {
        public readonly AsyncEventID EventId;
        public readonly long TimestampQpc;
        public readonly BufferHeader Header;

        public AsyncNeutralEvent(AsyncEventID eventId, long timestampQpc, in BufferHeader header)
        {
            EventId = eventId;
            TimestampQpc = timestampQpc;
            Header = header;
        }

        public ulong OsThreadId => Header.OsThreadId;
    }

    /// <summary>An unrecognized sub-event whose payload contents are not decoded.</summary>
    internal readonly struct AsyncUnknownEvent
    {
        public readonly AsyncEventID EventId;
        public readonly long TimestampQpc;
        public readonly BufferHeader Header;
        public readonly int PayloadLength;

        public AsyncUnknownEvent(AsyncEventID eventId, long timestampQpc, in BufferHeader header, int payloadLength)
        {
            EventId = eventId;
            TimestampQpc = timestampQpc;
            Header = header;
            PayloadLength = payloadLength;
        }
    }

    /// <summary>
    /// A call-stack sub-event with native IPs in leaf-to-root order for both V1 and V2.
    /// <see cref="FrameStates"/> contains signed compiler states for V1 and is null for V2.
    /// A zero-frame payload has no inline frames; this decoder does not resolve cached frame data.
    /// </summary>
    internal readonly struct AsyncCallstackEvent
    {
        public readonly AsyncEventID EventId;
        public readonly long TimestampQpc;
        public readonly BufferHeader Header;
        public readonly byte ContinuationIndex;
        public readonly byte FrameCount;
        public readonly ulong ParentDispatcherId;
        public readonly ulong DispatcherId;

        /// <summary>Native continuation IPs for V2, or state-machine <c>MoveNext</c> body IPs for V1.</summary>
        public readonly ulong[] MethodIds;

        /// <summary>Raw V1 compiler states, not IL/native offsets; null for V2.</summary>
        public readonly int[] FrameStates;

        public ulong OsThreadId => Header.OsThreadId;
        public uint AsyncThreadContextId => Header.AsyncThreadContextId;
        public AsyncCallstackKind Kind => AsyncEventInfo.GetCallstackKind(EventId);

        /// <summary>True when the payload contains no inline frames.</summary>
        public bool IsCached => FrameCount == 0;

        /// <summary>Decodes a call stack, allocating new frame arrays for a nonempty result.</summary>
        internal static bool TryRead(
            AsyncEventID eventId, long timestampQpc, in BufferHeader header,
            byte[] buffer, ref int index, int payloadEnd, out AsyncCallstackEvent result)
        {
            result = default;
            if (!TryReadHeader(eventId, buffer, ref index, payloadEnd, out byte continuationIndex,
                out byte frameCount, out ulong parentDispatcherId, out ulong dispatcherId))
            {
                return false;
            }

            if (frameCount == 0)
            {
                // No inline frames; cached frame data is not resolved here.
                result = new AsyncCallstackEvent(
                    eventId, timestampQpc, header, continuationIndex, 0,
                    parentDispatcherId, dispatcherId, Array.Empty<ulong>(), null);
                return true;
            }

            bool hasFrameStates = AsyncEventInfo.GetCallstackKind(eventId) == AsyncCallstackKind.StateMachineAsync;
            var methodIds = new ulong[frameCount];
            int[] frameStates = hasFrameStates ? new int[frameCount] : null;

            if (!TryReadFrames(buffer, ref index, payloadEnd, frameCount, methodIds, frameStates))
            {
                return false;
            }

            result = new AsyncCallstackEvent(
                eventId, timestampQpc, header, continuationIndex, frameCount,
                parentDispatcherId, dispatcherId, methodIds, frameStates);
            return true;
        }

        /// <summary>Decodes a call stack into caller-provided frame buffers.</summary>
        /// <remarks>
        /// A nonempty result references these buffers; only its first <see cref="FrameCount"/> entries are valid.
        /// Consume or copy those entries before reusing the buffers. V2 results have no state buffer.
        /// </remarks>
        internal static bool TryReadInto(
            AsyncEventID eventId, long timestampQpc, in BufferHeader header,
            byte[] buffer, ref int index, int payloadEnd,
            ulong[] methodIds, int[] frameStates, out AsyncCallstackEvent result)
        {
            result = default;
            if (!TryReadHeader(eventId, buffer, ref index, payloadEnd, out byte continuationIndex,
                out byte frameCount, out ulong parentDispatcherId, out ulong dispatcherId))
            {
                return false;
            }

            if (frameCount == 0)
            {
                result = new AsyncCallstackEvent(
                    eventId, timestampQpc, header, continuationIndex, 0,
                    parentDispatcherId, dispatcherId, Array.Empty<ulong>(), null);
                return true;
            }

            bool hasFrameStates = AsyncEventInfo.GetCallstackKind(eventId) == AsyncCallstackKind.StateMachineAsync;
            bool hasFrameBufferCapacity = methodIds != null && methodIds.Length >= frameCount;
            bool hasStateBufferCapacity = frameStates != null && frameStates.Length >= frameCount;
            if (!hasFrameBufferCapacity || (hasFrameStates && !hasStateBufferCapacity))
            {
                return false;
            }

            int[] states = hasFrameStates ? frameStates : null;
            if (!TryReadFrames(buffer, ref index, payloadEnd, frameCount, methodIds, states))
            {
                return false;
            }

            result = new AsyncCallstackEvent(
                eventId, timestampQpc, header, continuationIndex, frameCount,
                parentDispatcherId, dispatcherId, methodIds, states);
            return true;
        }

        #region private

        private AsyncCallstackEvent(
            AsyncEventID eventId, long timestampQpc, in BufferHeader header,
            byte continuationIndex, byte frameCount, ulong parentDispatcherId,
            ulong dispatcherId, ulong[] methodIds, int[] frameStates)
        {
            EventId = eventId;
            TimestampQpc = timestampQpc;
            Header = header;
            ContinuationIndex = continuationIndex;
            FrameCount = frameCount;
            ParentDispatcherId = parentDispatcherId;
            DispatcherId = dispatcherId;
            MethodIds = methodIds;
            FrameStates = frameStates;
        }

        /// <summary>Reads the fixed call-stack fields and dispatcher IDs before the frame data.</summary>
        private static bool TryReadHeader(
            AsyncEventID eventId, byte[] buffer, ref int index, int payloadEnd,
            out byte continuationIndex, out byte frameCount, out ulong parentDispatcherId, out ulong dispatcherId)
        {
            continuationIndex = 0;
            frameCount = 0;
            parentDispatcherId = 0;
            dispatcherId = 0;
            if (payloadEnd - index < 3)
            {
                return false;
            }

            index++; // Skip the reserved call-stack ID.
            continuationIndex = buffer[index++];
            frameCount = buffer[index++];

            if (eventId == AsyncEventID.CreateRuntimeAsyncCallstack &&
                !AsyncProfilerBufferReader.TryReadCompressedUInt64(buffer, ref index, payloadEnd, out parentDispatcherId))
            {
                return false;
            }

            return AsyncProfilerBufferReader.TryReadCompressedUInt64(buffer, ref index, payloadEnd, out dispatcherId);
        }

        /// <summary>Reads the first IP, signed IP deltas, and any V1 compiler states.</summary>
        private static bool TryReadFrames(
            byte[] buffer, ref int index, int payloadEnd, int frameCount,
            ulong[] methodIds, int[] frameStates)
        {
            if (!AsyncProfilerBufferReader.TryReadCompressedUInt64(buffer, ref index, payloadEnd, out ulong currentMethodId))
            {
                return false;
            }

            methodIds[0] = currentMethodId;
            if (frameStates != null)
            {
                if (!AsyncProfilerBufferReader.TryReadCompressedInt32(buffer, ref index, payloadEnd, out int state0))
                {
                    return false;
                }

                frameStates[0] = state0;
            }

            for (int frameIndex = 1; frameIndex < frameCount; frameIndex++)
            {
                if (!AsyncProfilerBufferReader.TryReadCompressedInt64(buffer, ref index, payloadEnd, out long delta))
                {
                    return false;
                }

                currentMethodId = (ulong)((long)currentMethodId + delta);
                methodIds[frameIndex] = currentMethodId;

                if (frameStates != null)
                {
                    if (!AsyncProfilerBufferReader.TryReadCompressedInt32(buffer, ref index, payloadEnd, out int state))
                    {
                        return false;
                    }

                    frameStates[frameIndex] = state;
                }
            }

            return true;
        }

        #endregion
    }

    /// <summary>A single entry of the per-event manifest carried by an <see cref="AsyncMetadataEvent"/>.</summary>
    internal readonly struct AsyncManifestEntry
    {
        public readonly AsyncEventID EventId;
        public readonly byte Version;
        public readonly PayloadLengthFieldSize PayloadLengthFieldSize;

        public AsyncManifestEntry(AsyncEventID eventId, byte version, PayloadLengthFieldSize payloadLengthFieldSize)
        {
            EventId = eventId;
            Version = version;
            PayloadLengthFieldSize = payloadLengthFieldSize;
        }
    }

    /// <summary>
    /// Profiler clock values, buffer/wrapper configuration, and per-event manifest entries.
    /// </summary>
    internal readonly struct AsyncMetadataEvent
    {
        public readonly long TimestampQpc;
        public readonly BufferHeader Header;
        public readonly ulong QpcFrequency;
        public readonly ulong QpcSync;
        public readonly ulong UtcSync;
        public readonly uint EventBufferSize;
        public readonly byte WrapperCount;
        public readonly AsyncManifestEntry[] Manifest;

        public ulong OsThreadId => Header.OsThreadId;

        /// <summary>Converts the synchronization point's FILETIME value to UTC.</summary>
        public DateTime SyncUtc => DateTime.FromFileTimeUtc((long)UtcSync);

        /// <summary>Decodes clock, configuration, and manifest fields without passing the payload end.</summary>
        internal static bool TryRead(
            long timestampQpc, in BufferHeader header, byte[] buffer,
            ref int index, int payloadEnd, out AsyncMetadataEvent result)
        {
            result = default;
            if (!AsyncProfilerBufferReader.TryReadCompressedUInt64(buffer, ref index, payloadEnd, out ulong qpcFrequency) ||
                !AsyncProfilerBufferReader.TryReadCompressedUInt64(buffer, ref index, payloadEnd, out ulong qpcSync) ||
                !AsyncProfilerBufferReader.TryReadCompressedUInt64(buffer, ref index, payloadEnd, out ulong utcSync) ||
                !AsyncProfilerBufferReader.TryReadCompressedUInt32(buffer, ref index, payloadEnd, out uint eventBufferSize))
            {
                return false;
            }

            if (index >= payloadEnd)
            {
                return false;
            }

            byte wrapperCount = buffer[index++];
            if (index >= payloadEnd)
            {
                return false;
            }

            byte manifestCount = buffer[index++];
            AsyncManifestEntry[] manifest = manifestCount == 0
                ? Array.Empty<AsyncManifestEntry>()
                : new AsyncManifestEntry[manifestCount];
            for (int i = 0; i < manifestCount; i++)
            {
                if (payloadEnd - index < 3)
                {
                    return false;
                }

                var payloadLengthFieldSize = (PayloadLengthFieldSize)buffer[index + 2];
                bool hasSupportedFieldSize =
                    payloadLengthFieldSize == PayloadLengthFieldSize.None ||
                    payloadLengthFieldSize == PayloadLengthFieldSize.Byte ||
                    payloadLengthFieldSize == PayloadLengthFieldSize.UShort;
                if (!hasSupportedFieldSize)
                {
                    return false;
                }

                manifest[i] = new AsyncManifestEntry(
                    (AsyncEventID)buffer[index], buffer[index + 1], payloadLengthFieldSize);
                index += 3;
            }

            result = new AsyncMetadataEvent(
                timestampQpc, header, qpcFrequency, qpcSync, utcSync, eventBufferSize, wrapperCount, manifest);
            return true;
        }

        #region private

        private AsyncMetadataEvent(
            long timestampQpc, in BufferHeader header, ulong qpcFrequency, ulong qpcSync,
            ulong utcSync, uint eventBufferSize, byte wrapperCount, AsyncManifestEntry[] manifest)
        {
            TimestampQpc = timestampQpc;
            Header = header;
            QpcFrequency = qpcFrequency;
            QpcSync = qpcSync;
            UtcSync = utcSync;
            EventBufferSize = eventBufferSize;
            WrapperCount = wrapperCount;
            Manifest = manifest;
        }

        #endregion
    }

    /// <summary>A QPC/UTC synchronization point without a new manifest.</summary>
    internal readonly struct AsyncSyncClockEvent
    {
        public readonly long TimestampQpc;
        public readonly BufferHeader Header;
        public readonly ulong QpcSync;
        public readonly ulong UtcSync;

        public AsyncSyncClockEvent(long timestampQpc, in BufferHeader header, ulong qpcSync, ulong utcSync)
        {
            TimestampQpc = timestampQpc;
            Header = header;
            QpcSync = qpcSync;
            UtcSync = utcSync;
        }

        public ulong OsThreadId => Header.OsThreadId;

        /// <summary>Converts the synchronization point's FILETIME value to UTC.</summary>
        public DateTime SyncUtc => DateTime.FromFileTimeUtc((long)UtcSync);
    }

    /// <summary>A decoding failure with any available buffer and sub-event context.</summary>
    internal readonly struct ParseError
    {
        /// <summary>The decoding failure.</summary>
        public readonly string Message;
        /// <summary>The byte offset of the failure within the buffer.</summary>
        public readonly int ByteOffset;
        /// <summary>The decoded header, or null when the header could not be read.</summary>
        public readonly BufferHeader? BufferHeader;
        /// <summary>
        /// The failing sub-event's QPC, or the last decoded QPC when its timestamp is unavailable.
        /// Null when the header could not be read.
        /// </summary>
        public readonly long? TimestampQpc;
        /// <summary>The failing sub-event's id, or null for a buffer-level failure.</summary>
        public readonly AsyncEventID? EventId;

        /// <summary>Creates a decode diagnostic, with any available buffer and sub-event context.</summary>
        public ParseError(string message, int byteOffset,
            BufferHeader? bufferHeader = null, long? timestampQpc = null, AsyncEventID? eventId = null)
        {
            Message = message;
            ByteOffset = byteOffset;
            BufferHeader = bufferHeader;
            TimestampQpc = timestampQpc;
            EventId = eventId;
        }

        public override string ToString() => "AsyncProfiler parse error @" + ByteOffset + ": " + Message;
    }

    /// <summary>
    /// Receives decoded V1 and V2 sub-events synchronously during
    /// <see cref="AsyncProfilerTraceEventParser.ParseBuffer(byte[], ISubEventSink)"/>.
    /// Metadata and reset notifications are shared by both instrumentation families.
    /// </summary>
    internal interface ISubEventSink
    {
        void OnContextCreate(in AsyncContextEvent e);
        void OnContextResume(in AsyncContextEvent e);
        void OnContextSuspend(in AsyncContextEvent e);
        void OnContextComplete(in AsyncContextEvent e);
        void OnException(in AsyncUnwindEvent e);
        void OnCallstack(in AsyncCallstackEvent e);
        void OnMethodResume(in AsyncMethodEvent e);
        void OnMethodComplete(in AsyncMethodEvent e);
        void OnResetThreadContext(in AsyncNeutralEvent e);
        void OnResetContinuationWrapperIndex(in AsyncNeutralEvent e);
        void OnMetadata(in AsyncMetadataEvent e);
        void OnSyncClock(in AsyncSyncClockEvent e);
        void OnUnknown(in AsyncUnknownEvent e);
        void OnParseError(in ParseError e);
    }

    /// <summary>
    /// Optional callback used instead of <see cref="ISubEventSink.OnCallstack"/>.
    /// Allows a sink to decode call-stack payloads into reusable buffers.
    /// </summary>
    internal interface ICallstackPayloadSink
    {
        /// <summary>
        /// Decodes and handles one call-stack payload without reading past <paramref name="payloadEnd"/>.
        /// Returns false if the payload could not be decoded.
        /// </summary>
        bool TryOnCallstack(
            AsyncEventID eventId, long timestampQpc, in BufferHeader header,
            byte[] buffer, ref int index, int payloadEnd);
    }
}
