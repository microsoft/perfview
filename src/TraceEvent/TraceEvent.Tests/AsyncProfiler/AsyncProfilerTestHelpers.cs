// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tracing.Computers;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;

namespace TraceEventTests
{
    /// <summary>Default header values used by <see cref="AsyncProfilerBufferBuilder"/> and the parser tests, plus payload helpers.</summary>
    internal static class AsyncProfilerTestData
    {
        public const uint DefaultAsyncThreadContextId = 0x0BADF00D;
        public const ulong DefaultOsThreadId = 0x1234;
        public const long DefaultStartQpc = 1_000_000;

        /// <summary>Wraps a raw buffer in the <c>AsyncEvents</c> event payload: an Int32 byte length followed by the buffer.</summary>
        public static byte[] AsyncEventsPayload(byte[] buffer)
        {
            byte[] payload = new byte[sizeof(int) + buffer.Length];
            Array.Copy(BitConverter.GetBytes(buffer.Length), 0, payload, 0, sizeof(int));
            Array.Copy(buffer, 0, payload, sizeof(int), buffer.Length);
            return payload;
        }
    }

    /// <summary>
    /// Builds an <c>AsyncEvents</c> buffer byte-for-byte in the runtime wire format (37-byte header,
    /// per-sub-event framing of id + compressed delta timestamp + length prefix + payload).
    /// Used by the async-profiler tests and benchmarks.
    /// </summary>
    internal sealed class AsyncProfilerBufferBuilder
    {
        public AsyncProfilerBufferBuilder(
            ulong osThreadId = AsyncProfilerTestData.DefaultOsThreadId,
            uint asyncThreadContextId = AsyncProfilerTestData.DefaultAsyncThreadContextId,
            long startQpc = AsyncProfilerTestData.DefaultStartQpc)
        {
            _lastTs = startQpc;

            // 37-byte header; totalSize/eventCount/endTs are patched in Build().
            _bytes.Add(1);                              // version
            WriteUInt32(0);                             // totalSize (patched)
            WriteUInt32(asyncThreadContextId);
            WriteUInt64(osThreadId);
            WriteUInt32(0);                             // eventCount (patched)
            WriteUInt64((ulong)startQpc);                // startTs
            WriteUInt64(0);                             // endTs (patched)
        }

        public AsyncProfilerBufferBuilder CreateContext(AsyncEventID id, long ts, ulong parent, ulong dispatcher)
        {
            var payload = new List<byte>();
            WriteCompressedUInt64(payload, parent);
            WriteCompressedUInt64(payload, dispatcher);
            return Emit(id, ts, payload);
        }

        public AsyncProfilerBufferBuilder ResumeContext(AsyncEventID id, long ts, ulong dispatcher)
        {
            var payload = new List<byte>();
            WriteCompressedUInt64(payload, dispatcher);
            return Emit(id, ts, payload);
        }

        public AsyncProfilerBufferBuilder ResumeContextWithExtra(AsyncEventID id, long ts, ulong dispatcher, int extraBytes)
        {
            var payload = new List<byte>();
            WriteCompressedUInt64(payload, dispatcher);
            for (int i = 0; i < extraBytes; i++)
            {
                payload.Add((byte)(0xE0 + i));
            }
            return Emit(id, ts, payload);
        }

        public AsyncProfilerBufferBuilder ContextNoPayload(AsyncEventID id, long ts) => Emit(id, ts, new List<byte>());

        public AsyncProfilerBufferBuilder Method(AsyncEventID id, long ts) => Emit(id, ts, new List<byte>());

        public AsyncProfilerBufferBuilder Unwind(AsyncEventID id, long ts, uint unwoundFrames)
        {
            var payload = new List<byte>();
            WriteCompressedUInt32(payload, unwoundFrames);
            return Emit(id, ts, payload);
        }

        public AsyncProfilerBufferBuilder Callstack(AsyncEventID id, long ts, byte continuationIndex,
            ulong parentDispatcherId, ulong dispatcherId, ulong[] methodIds, int[] states)
        {
            var payload = new List<byte>();
            payload.Add(0);                             // reserved callstack id
            payload.Add(continuationIndex);
            payload.Add((byte)methodIds.Length);         // frame count
            if (id == AsyncEventID.CreateRuntimeAsyncCallstack)
            {
                WriteCompressedUInt64(payload, parentDispatcherId);
            }
            WriteCompressedUInt64(payload, dispatcherId);

            if (methodIds.Length > 0)
            {
                WriteCompressedUInt64(payload, methodIds[0]);
                if (states != null)
                {
                    WriteCompressedInt32(payload, states[0]);
                }
                for (int i = 1; i < methodIds.Length; i++)
                {
                    WriteCompressedInt64(payload, (long)methodIds[i] - (long)methodIds[i - 1]);
                    if (states != null)
                    {
                        WriteCompressedInt32(payload, states[i]);
                    }
                }
            }
            return Emit(id, ts, payload);
        }

        public AsyncProfilerBufferBuilder Metadata(long ts, ulong qpcFrequency, ulong qpcSync, ulong utcSync,
            uint eventBufferSize, byte wrapperCount, AsyncManifestEntry[] manifest)
        {
            var payload = new List<byte>();
            WriteCompressedUInt64(payload, qpcFrequency);
            WriteCompressedUInt64(payload, qpcSync);
            WriteCompressedUInt64(payload, utcSync);
            WriteCompressedUInt32(payload, eventBufferSize);
            payload.Add(wrapperCount);
            payload.Add((byte)manifest.Length);
            foreach (var entry in manifest)
            {
                payload.Add((byte)entry.EventId);
                payload.Add(entry.Version);
                payload.Add((byte)entry.PayloadLengthFieldSize);
            }
            return Emit(AsyncEventID.AsyncProfilerMetadata, ts, payload);
        }

        public AsyncProfilerBufferBuilder SyncClock(long ts, ulong qpcSync, ulong utcSync)
        {
            var payload = new List<byte>();
            WriteCompressedUInt64(payload, qpcSync);
            WriteCompressedUInt64(payload, utcSync);
            return Emit(AsyncEventID.AsyncProfilerSyncClock, ts, payload);
        }

        public AsyncProfilerBufferBuilder UnknownNoPayload(byte eventId, long ts) => Emit((AsyncEventID)eventId, ts, new List<byte>());

        /// <summary>
        /// Emits a sub-event with an explicit payload-length prefix width, independent of the static
        /// default table. Used to simulate a newer runtime whose manifest advertises a different width
        /// (or a future event id the parser does not statically know).
        /// </summary>
        public AsyncProfilerBufferBuilder RawEvent(byte eventId, long ts, PayloadLengthFieldSize prefix, byte[] payload)
        {
            _bytes.Add(eventId);
            WriteCompressedUInt64(_bytes, (ulong)(ts - _lastTs));
            _lastTs = ts;

            switch (prefix)
            {
                case PayloadLengthFieldSize.None:
                    break;
                case PayloadLengthFieldSize.Byte:
                    _bytes.Add((byte)payload.Length);
                    break;
                default: // UShort
                    _bytes.Add((byte)(payload.Length & 0xFF));
                    _bytes.Add((byte)((payload.Length >> 8) & 0xFF));
                    break;
            }

            _bytes.AddRange(payload);
            _eventCount++;
            return this;
        }

        public byte[] Build()
        {
            byte[] result = _bytes.ToArray();
            // Patch totalSize @1, eventCount @17, endTs @29.
            WriteUInt32At(result, 1, (uint)result.Length);
            WriteUInt32At(result, 17, _eventCount);
            WriteUInt64At(result, 29, (ulong)_lastTs);
            return result;
        }

        #region private

        private AsyncProfilerBufferBuilder Emit(AsyncEventID id, long ts, List<byte> payload)
        {
            _bytes.Add((byte)id);
            WriteCompressedUInt64(_bytes, (ulong)(ts - _lastTs));
            _lastTs = ts;

            switch (AsyncEventInfo.GetPayloadLengthFieldSize(id))
            {
                case PayloadLengthFieldSize.None:
                    if (payload.Count != 0)
                    {
                        throw new InvalidOperationException("An event with no payload-length field cannot carry a payload.");
                    }
                    break;
                case PayloadLengthFieldSize.Byte:
                    if (payload.Count > byte.MaxValue)
                    {
                        throw new InvalidOperationException("The payload does not fit in its one-byte length field.");
                    }
                    _bytes.Add((byte)payload.Count);
                    break;
                default: // UShort
                    _bytes.Add((byte)(payload.Count & 0xFF));
                    _bytes.Add((byte)((payload.Count >> 8) & 0xFF));
                    break;
            }

            _bytes.AddRange(payload);
            _eventCount++;
            return this;
        }

        private void WriteUInt32(uint value)
        {
            _bytes.Add((byte)value);
            _bytes.Add((byte)(value >> 8));
            _bytes.Add((byte)(value >> 16));
            _bytes.Add((byte)(value >> 24));
        }

        private void WriteUInt64(ulong value)
        {
            WriteUInt32((uint)value);
            WriteUInt32((uint)(value >> 32));
        }

        private static void WriteUInt32At(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteUInt64At(byte[] buffer, int offset, ulong value)
        {
            WriteUInt32At(buffer, offset, (uint)value);
            WriteUInt32At(buffer, offset + 4, (uint)(value >> 32));
        }

        private static void WriteCompressedUInt64(List<byte> target, ulong value)
        {
            while (value > 0x7F)
            {
                target.Add((byte)(value | 0x80));
                value >>= 7;
            }
            target.Add((byte)value);
        }

        private static void WriteCompressedUInt32(List<byte> target, uint value) => WriteCompressedUInt64(target, value);

        private static void WriteCompressedInt64(List<byte> target, long value)
        {
            ulong zigzag = (ulong)((value << 1) ^ (value >> 63));
            WriteCompressedUInt64(target, zigzag);
        }

        private static void WriteCompressedInt32(List<byte> target, int value)
        {
            uint zigzag = (uint)((value << 1) ^ (value >> 31));
            WriteCompressedUInt64(target, zigzag);
        }

        private readonly List<byte> _bytes = new List<byte>();
        private long _lastTs;
        private uint _eventCount;

        #endregion
    }

    /// <summary>Builds call stacks and stitched frames for tests that run below trace conversion.</summary>
    internal static class AsyncProfilerStackTestData
    {
        /// <summary>A single-frame state-machine call stack for tests that only need a non-null owner.</summary>
        public static AsyncProfilerCallStack CallStack() =>
            CallStack(new AsyncProfilerCallStackFrames(AsyncCallstackKind.StateMachineAsync, new ulong[] { 3 }, new[] { 0 }));

        public static AsyncProfilerCallStack CallStack(AsyncProfilerCallStackFrames frames, int depth = 0,
            long startQpc = 0, long endQpc = long.MaxValue,
            AsyncProfilerCallStack.CompletionDelta[] methodCompletions = null,
            AsyncProfilerCallStack.CompletionDelta[] exceptionCompletions = null,
            byte continuationIndexBase = 0, byte wrapperCount = 32, long[] wrapperResets = null) =>
            new AsyncProfilerCallStack(depth, (AsyncProfilerCallStackFramesIndex)0, frames,
                continuationIndexBase, wrapperCount, startQpc, endQpc,
                methodCompletions ?? Array.Empty<AsyncProfilerCallStack.CompletionDelta>(),
                exceptionCompletions ?? Array.Empty<AsyncProfilerCallStack.CompletionDelta>(),
                wrapperResets ?? Array.Empty<long>());

        /// <summary>A physical frame with an unresolved method, optionally owned by <paramref name="callStack"/>.</summary>
        public static AsyncProfilerStitchedFrame Sync(int codeAddress,
            AsyncProfilerRuntimeSupport.FrameClassification kind = default, AsyncProfilerCallStack callStack = null) =>
            AsyncProfilerStitchedFrame.CreateSync(
                new AsyncProfilerStitchSyncFrame((CodeAddressIndex)codeAddress, MethodIndex.Invalid, kind), callStack);
    }

    /// <summary>A sink that records every decoded sub-event so tests can assert on them.</summary>
    internal sealed class AsyncProfilerCollectingSink : ISubEventSink
    {
        public readonly List<AsyncEventID> Order = new List<AsyncEventID>();
        public readonly List<AsyncContextEvent> ContextCreates = new List<AsyncContextEvent>();
        public readonly List<AsyncContextEvent> ContextResumes = new List<AsyncContextEvent>();
        public readonly List<AsyncContextEvent> ContextSuspends = new List<AsyncContextEvent>();
        public readonly List<AsyncContextEvent> ContextCompletes = new List<AsyncContextEvent>();
        public readonly List<AsyncUnwindEvent> Unwinds = new List<AsyncUnwindEvent>();
        public readonly List<AsyncCallstackEvent> Callstacks = new List<AsyncCallstackEvent>();
        public readonly List<AsyncMethodEvent> Methods = new List<AsyncMethodEvent>();
        public readonly List<AsyncNeutralEvent> ResetThreadContexts = new List<AsyncNeutralEvent>();
        public readonly List<AsyncNeutralEvent> ResetWrapperIndexes = new List<AsyncNeutralEvent>();
        public readonly List<AsyncMetadataEvent> Metadatas = new List<AsyncMetadataEvent>();
        public readonly List<AsyncSyncClockEvent> SyncClocks = new List<AsyncSyncClockEvent>();
        public readonly List<AsyncUnknownEvent> Unknowns = new List<AsyncUnknownEvent>();
        public readonly List<ParseError> Errors = new List<ParseError>();

        public void OnContextCreate(in AsyncContextEvent e)
        {
            Order.Add(e.EventId);
            ContextCreates.Add(e);
        }

        public void OnContextResume(in AsyncContextEvent e)
        {
            Order.Add(e.EventId);
            ContextResumes.Add(e);
        }

        public void OnContextSuspend(in AsyncContextEvent e)
        {
            Order.Add(e.EventId);
            ContextSuspends.Add(e);
        }

        public void OnContextComplete(in AsyncContextEvent e)
        {
            Order.Add(e.EventId);
            ContextCompletes.Add(e);
        }

        public void OnException(in AsyncUnwindEvent e)
        {
            Order.Add(e.EventId);
            Unwinds.Add(e);
        }

        public void OnCallstack(in AsyncCallstackEvent e)
        {
            Order.Add(e.EventId);
            Callstacks.Add(e);
        }

        public void OnMethodResume(in AsyncMethodEvent e)
        {
            Order.Add(e.EventId);
            Methods.Add(e);
        }

        public void OnMethodComplete(in AsyncMethodEvent e)
        {
            Order.Add(e.EventId);
            Methods.Add(e);
        }

        public void OnResetThreadContext(in AsyncNeutralEvent e)
        {
            Order.Add(e.EventId);
            ResetThreadContexts.Add(e);
        }

        public void OnResetContinuationWrapperIndex(in AsyncNeutralEvent e)
        {
            Order.Add(e.EventId);
            ResetWrapperIndexes.Add(e);
        }

        public void OnMetadata(in AsyncMetadataEvent e)
        {
            Order.Add(AsyncEventID.AsyncProfilerMetadata);
            Metadatas.Add(e);
        }

        public void OnSyncClock(in AsyncSyncClockEvent e)
        {
            Order.Add(AsyncEventID.AsyncProfilerSyncClock);
            SyncClocks.Add(e);
        }

        public void OnUnknown(in AsyncUnknownEvent e)
        {
            Unknowns.Add(e);
        }

        public void OnParseError(in ParseError e)
        {
            Errors.Add(e);
        }
    }

    /// <summary>A non-allocating sink that counts decoded sub-events, frames, and resume/suspend pairs by instrumentation family.</summary>
    internal sealed class AsyncProfilerCountingSink : ISubEventSink
    {
        public int SubEventCount { get; private set; }
        public int FrameCount { get; private set; }
        public int RuntimeResumeCount { get; private set; }
        public int RuntimeSuspendCount { get; private set; }
        public int StateMachineResumeCount { get; private set; }
        public int StateMachineSuspendCount { get; private set; }
        public int ParseErrorCount { get; private set; }

        public void Reset()
        {
            SubEventCount = 0;
            FrameCount = 0;
            RuntimeResumeCount = 0;
            RuntimeSuspendCount = 0;
            StateMachineResumeCount = 0;
            StateMachineSuspendCount = 0;
            ParseErrorCount = 0;
        }

        public void OnContextCreate(in AsyncContextEvent e) => SubEventCount++;
        public void OnContextResume(in AsyncContextEvent e) => SubEventCount++;

        public void OnContextSuspend(in AsyncContextEvent e)
        {
            SubEventCount++;
            if (e.EventId == AsyncEventID.SuspendRuntimeAsyncContext)
            {
                RuntimeSuspendCount++;
            }
            else if (e.EventId == AsyncEventID.SuspendStateMachineAsyncContext)
            {
                StateMachineSuspendCount++;
            }
        }

        public void OnContextComplete(in AsyncContextEvent e) => SubEventCount++;
        public void OnException(in AsyncUnwindEvent e) => SubEventCount++;

        public void OnCallstack(in AsyncCallstackEvent e)
        {
            SubEventCount++;
            FrameCount += e.FrameCount;
            if (e.EventId == AsyncEventID.ResumeRuntimeAsyncCallstack)
            {
                RuntimeResumeCount++;
            }
            else if (e.EventId == AsyncEventID.ResumeStateMachineAsyncCallstack)
            {
                StateMachineResumeCount++;
            }
        }

        public void OnMethodResume(in AsyncMethodEvent e) => SubEventCount++;
        public void OnMethodComplete(in AsyncMethodEvent e) => SubEventCount++;
        public void OnResetThreadContext(in AsyncNeutralEvent e) => SubEventCount++;
        public void OnResetContinuationWrapperIndex(in AsyncNeutralEvent e) => SubEventCount++;
        public void OnMetadata(in AsyncMetadataEvent e) => SubEventCount++;
        public void OnSyncClock(in AsyncSyncClockEvent e) => SubEventCount++;
        public void OnUnknown(in AsyncUnknownEvent e) => SubEventCount++;
        public void OnParseError(in ParseError e) => ParseErrorCount++;
    }
}
