// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;

using Xunit;

namespace TraceEventTests
{
    /// <summary>
    /// Unit tests for <see cref="AsyncProfilerTraceEventParser.ParseBuffer(byte[], ISubEventSink)"/>. Each test synthesizes an
    /// <c>AsyncEvents</c> buffer in the exact runtime wire format (see <see cref="AsyncProfilerBufferBuilder"/>)
    /// and asserts the decoded sub-events. These exercise the pure decoder without a live TraceEventSource.
    /// </summary>
    public class AsyncProfilerParsingTests
    {
        [Fact]
        public void Header_TooShort_ReportsParseError()
        {
            var sink = new AsyncProfilerCollectingSink();
            AsyncProfilerTraceEventParser.ParseBuffer(new byte[8], sink);

            ParseError error = Assert.Single(sink.Errors);
            Assert.Null(error.BufferHeader);
            Assert.Null(error.TimestampQpc);
            Assert.Empty(sink.Order);
        }

        [Fact]
        public void Header_UnsupportedVersion_ReportsParseError()
        {
            var buffer = new AsyncProfilerBufferBuilder().Build();
            buffer[0] = 2; // corrupt the version

            var sink = new AsyncProfilerCollectingSink();
            AsyncProfilerTraceEventParser.ParseBuffer(buffer, sink);

            Assert.Single(sink.Errors);
        }

        [Fact]
        public void EmptyBuffer_NoSubEvents_NoErrors()
        {
            var buffer = new AsyncProfilerBufferBuilder().Build();

            var sink = new AsyncProfilerCollectingSink();
            AsyncProfilerTraceEventParser.ParseBuffer(buffer, sink);

            Assert.Empty(sink.Order);
            Assert.Empty(sink.Errors);
        }

        [Theory]
        [InlineData(false)] // V2 RuntimeAsync
        [InlineData(true)]  // V1 StateMachineAsync
        public void ContextLifecycle_DecodesAllTransitions(bool stateMachine)
        {
            AsyncEventID create = stateMachine ? AsyncEventID.CreateStateMachineAsyncContext : AsyncEventID.CreateRuntimeAsyncContext;
            AsyncEventID resume = stateMachine ? AsyncEventID.ResumeStateMachineAsyncContext : AsyncEventID.ResumeRuntimeAsyncContext;
            AsyncEventID suspend = stateMachine ? AsyncEventID.SuspendStateMachineAsyncContext : AsyncEventID.SuspendRuntimeAsyncContext;
            AsyncEventID complete = stateMachine ? AsyncEventID.CompleteStateMachineAsyncContext : AsyncEventID.CompleteRuntimeAsyncContext;

            const ulong parent = 0x10, dispatcher = 0x20;

            var buffer = new AsyncProfilerBufferBuilder()
                .CreateContext(create, StartQpc + 10, parent, dispatcher)
                .ResumeContext(resume, StartQpc + 20, dispatcher)
                .ContextNoPayload(suspend, StartQpc + 30)
                .ContextNoPayload(complete, StartQpc + 40)
                .Build();

            var sink = new AsyncProfilerCollectingSink();
            AsyncProfilerTraceEventParser.ParseBuffer(buffer, sink);

            Assert.Empty(sink.Errors);
            Assert.Equal(new[] { create, resume, suspend, complete }, sink.Order);

            AsyncContextEvent created = Assert.Single(sink.ContextCreates);
            Assert.Equal(parent, created.ParentDispatcherId);
            Assert.Equal(dispatcher, created.DispatcherId);
            Assert.Equal(stateMachine, created.IsStateMachine);
            Assert.Equal(OsThreadId, created.OsThreadId);
            Assert.Equal(AsyncThreadContextId, created.AsyncThreadContextId);

            Assert.Equal(dispatcher, Assert.Single(sink.ContextResumes).DispatcherId);
            Assert.Single(sink.ContextSuspends);
            Assert.Single(sink.ContextCompletes);
        }

        [Fact]
        public void TimestampDeltas_AccumulateFromStart()
        {
            var buffer = new AsyncProfilerBufferBuilder()
                .Method(AsyncEventID.ResumeRuntimeAsyncMethod, StartQpc + 5)
                .Method(AsyncEventID.CompleteRuntimeAsyncMethod, StartQpc + 12)
                .Method(AsyncEventID.ResumeStateMachineAsyncMethod, StartQpc + 100)
                .Build();

            var sink = new AsyncProfilerCollectingSink();
            AsyncProfilerTraceEventParser.ParseBuffer(buffer, sink);

            Assert.Empty(sink.Errors);
            Assert.Equal(new long[] { StartQpc + 5, StartQpc + 12, StartQpc + 100 },
                sink.Methods.ConvertAll(m => m.TimestampQpc).ToArray());
        }

        [Fact]
        public void RuntimeCallstack_MultiFrame_ReconstructsDeltaIps_NoState()
        {
            ulong[] ips = { 0x7FFF_0000_1000, 0x7FFF_0000_1800, 0x7FFF_0000_0900 };

            var buffer = new AsyncProfilerBufferBuilder()
                .Callstack(AsyncEventID.CreateRuntimeAsyncCallstack, StartQpc + 1, continuationIndex: 3,
                    parentDispatcherId: 0x99, dispatcherId: 0xAA, ips, states: null)
                .Build();

            var sink = new AsyncProfilerCollectingSink();
            AsyncProfilerTraceEventParser.ParseBuffer(buffer, sink);

            Assert.Empty(sink.Errors);
            AsyncCallstackEvent cs = Assert.Single(sink.Callstacks);
            Assert.Equal(AsyncCallstackKind.RuntimeAsync, cs.Kind);
            Assert.False(cs.IsCached);
            Assert.Equal(3, cs.ContinuationIndex);
            Assert.Equal((byte)ips.Length, cs.FrameCount);
            Assert.Equal(0x99UL, cs.ParentDispatcherId);
            Assert.Equal(0xAAUL, cs.DispatcherId);
            Assert.Equal(ips, cs.MethodIds);
            Assert.Null(cs.FrameStates);
        }

        [Fact]
        public void RuntimeResumeCallstack_HasNoParentDispatcher()
        {
            ulong[] ips = { 0x4000, 0x4100 };

            var buffer = new AsyncProfilerBufferBuilder()
                .Callstack(AsyncEventID.ResumeRuntimeAsyncCallstack, StartQpc + 1, continuationIndex: 0,
                    parentDispatcherId: 0 /* not emitted for non-Create */, dispatcherId: 0x55, ips, states: null)
                .Build();

            var sink = new AsyncProfilerCollectingSink();
            AsyncProfilerTraceEventParser.ParseBuffer(buffer, sink);

            Assert.Empty(sink.Errors);
            AsyncCallstackEvent cs = Assert.Single(sink.Callstacks);
            Assert.Equal(0UL, cs.ParentDispatcherId);
            Assert.Equal(0x55UL, cs.DispatcherId);
            Assert.Equal(ips, cs.MethodIds);
        }

        [Fact]
        public void StateMachineCallstack_CarriesPerFrameState()
        {
            ulong[] ips = { 0x8000, 0x8200, 0x8100 };
            int[] states = { 0, 2, -1 };

            var buffer = new AsyncProfilerBufferBuilder()
                .Callstack(AsyncEventID.ResumeStateMachineAsyncCallstack, StartQpc + 1, continuationIndex: 7,
                    parentDispatcherId: 0, dispatcherId: 0x77, ips, states)
                .Build();

            var sink = new AsyncProfilerCollectingSink();
            AsyncProfilerTraceEventParser.ParseBuffer(buffer, sink);

            Assert.Empty(sink.Errors);
            AsyncCallstackEvent cs = Assert.Single(sink.Callstacks);
            Assert.Equal(AsyncCallstackKind.StateMachineAsync, cs.Kind);
            Assert.Equal(ips, cs.MethodIds);
            Assert.Equal(states, cs.FrameStates);
        }

        [Fact]
        public void ParseBuffer_CallstackArraysAreIndependentlyOwned()
        {
            ulong[] methods = { 0x8000, 0x8100 };
            int[] states = { 1, 2 };
            var buffer = new AsyncProfilerBufferBuilder()
                .Callstack(AsyncEventID.ResumeStateMachineAsyncCallstack, StartQpc + 1, continuationIndex: 0,
                    parentDispatcherId: 0, dispatcherId: 1, methods, states)
                .Callstack(AsyncEventID.ResumeStateMachineAsyncCallstack, StartQpc + 2, continuationIndex: 0,
                    parentDispatcherId: 0, dispatcherId: 2, methods, states)
                .Build();

            var sink = new AsyncProfilerCollectingSink();
            AsyncProfilerTraceEventParser.ParseBuffer(buffer, sink);

            Assert.Empty(sink.Errors);
            Assert.Equal(2, sink.Callstacks.Count);
            Assert.NotSame(sink.Callstacks[0].MethodIds, sink.Callstacks[1].MethodIds);
            Assert.NotSame(sink.Callstacks[0].FrameStates, sink.Callstacks[1].FrameStates);

            sink.Callstacks[0].MethodIds[0] = 0;
            sink.Callstacks[0].FrameStates[0] = 0;
            Assert.Equal(0x8000UL, sink.Callstacks[1].MethodIds[0]);
            Assert.Equal(1, sink.Callstacks[1].FrameStates[0]);
        }

        [Fact]
        public void CachedCallstack_HasNoFrames()
        {
            var buffer = new AsyncProfilerBufferBuilder()
                .Callstack(AsyncEventID.ResumeRuntimeAsyncCallstack, StartQpc + 1, continuationIndex: 1,
                    parentDispatcherId: 0, dispatcherId: 0xC0FFEE, methodIds: Array.Empty<ulong>(), states: null)
                .Build();

            var sink = new AsyncProfilerCollectingSink();
            AsyncProfilerTraceEventParser.ParseBuffer(buffer, sink);

            Assert.Empty(sink.Errors);
            AsyncCallstackEvent cs = Assert.Single(sink.Callstacks);
            Assert.True(cs.IsCached);
            Assert.Equal(0, cs.FrameCount);
            Assert.Empty(cs.MethodIds);
            Assert.Equal(0xC0FFEEUL, cs.DispatcherId);
        }

        [Fact]
        public void Unwind_DecodesFrameCount()
        {
            var buffer = new AsyncProfilerBufferBuilder()
                .Unwind(AsyncEventID.UnwindStateMachineAsyncException, StartQpc + 2, unwoundFrames: 42)
                .Build();

            var sink = new AsyncProfilerCollectingSink();
            AsyncProfilerTraceEventParser.ParseBuffer(buffer, sink);

            Assert.Empty(sink.Errors);
            Assert.Equal(42u, Assert.Single(sink.Unwinds).UnwoundFrameCount);
        }

        [Fact]
        public void Metadata_DecodesClockAndManifest()
        {
            var manifest = new[]
            {
                new AsyncManifestEntry(AsyncEventID.CreateRuntimeAsyncContext, 1, PayloadLengthFieldSize.Byte),
                new AsyncManifestEntry(AsyncEventID.AsyncProfilerMetadata, 2, PayloadLengthFieldSize.UShort),
            };
            long utcFileTime = new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc).ToFileTimeUtc();

            var buffer = new AsyncProfilerBufferBuilder()
                .Metadata(StartQpc + 3, qpcFrequency: 10_000_000, qpcSync: 5_000, utcSync: (ulong)utcFileTime,
                    eventBufferSize: 4096, wrapperCount: 32, manifest)
                .Build();

            var sink = new AsyncProfilerCollectingSink();
            AsyncProfilerTraceEventParser.ParseBuffer(buffer, sink);

            Assert.Empty(sink.Errors);
            AsyncMetadataEvent md = Assert.Single(sink.Metadatas);
            Assert.Equal(10_000_000UL, md.QpcFrequency);
            Assert.Equal(5_000UL, md.QpcSync);
            Assert.Equal((ulong)utcFileTime, md.UtcSync);
            Assert.Equal(4096u, md.EventBufferSize);
            Assert.Equal((byte)32, md.WrapperCount);
            Assert.Equal(manifest.Length, md.Manifest.Length);
            for (int i = 0; i < manifest.Length; i++)
            {
                Assert.Equal(manifest[i].EventId, md.Manifest[i].EventId);
                Assert.Equal(manifest[i].Version, md.Manifest[i].Version);
                Assert.Equal(manifest[i].PayloadLengthFieldSize, md.Manifest[i].PayloadLengthFieldSize);
            }
            Assert.Equal(new DateTime(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc), md.SyncUtc);
        }

        [Fact]
        public void SyncClock_DecodesQpcAndUtc()
        {
            var buffer = new AsyncProfilerBufferBuilder()
                .SyncClock(StartQpc + 4, qpcSync: 777, utcSync: 888)
                .Build();

            var sink = new AsyncProfilerCollectingSink();
            AsyncProfilerTraceEventParser.ParseBuffer(buffer, sink);

            Assert.Empty(sink.Errors);
            AsyncSyncClockEvent sc = Assert.Single(sink.SyncClocks);
            Assert.Equal(777UL, sc.QpcSync);
            Assert.Equal(888UL, sc.UtcSync);
        }

        [Fact]
        public void ResetEvents_Decode()
        {
            var buffer = new AsyncProfilerBufferBuilder()
                .ContextNoPayload(AsyncEventID.ResetAsyncThreadContext, StartQpc + 1)
                .ContextNoPayload(AsyncEventID.ResetAsyncContinuationWrapperIndex, StartQpc + 2)
                .Build();

            var sink = new AsyncProfilerCollectingSink();
            AsyncProfilerTraceEventParser.ParseBuffer(buffer, sink);

            Assert.Empty(sink.Errors);
            Assert.Equal(AsyncEventID.ResetAsyncThreadContext, Assert.Single(sink.ResetThreadContexts).EventId);
            Assert.Equal(AsyncEventID.ResetAsyncContinuationWrapperIndex, Assert.Single(sink.ResetWrapperIndexes).EventId);
        }

        [Fact]
        public void ForwardCompat_ExtraTrailingPayloadBytes_AreSkippedViaLengthPrefix()
        {
            // Appended fields must not change the known dispatcher or the next event's framing.
            var buffer = new AsyncProfilerBufferBuilder()
                .ResumeContextWithExtra(AsyncEventID.ResumeRuntimeAsyncContext, StartQpc + 1, dispatcher: 0x42, extraBytes: 3)
                .Method(AsyncEventID.CompleteRuntimeAsyncMethod, StartQpc + 2)
                .Build();

            var sink = new AsyncProfilerCollectingSink();
            AsyncProfilerTraceEventParser.ParseBuffer(buffer, sink);

            Assert.Empty(sink.Errors);
            Assert.Equal(new[] { AsyncEventID.ResumeRuntimeAsyncContext, AsyncEventID.CompleteRuntimeAsyncMethod }, sink.Order);
            Assert.Equal(0x42UL, Assert.Single(sink.ContextResumes).DispatcherId);
        }

        [Fact]
        public void UnknownEventIdWithoutPayload_ReportedAsUnknown()
        {
            var buffer = new AsyncProfilerBufferBuilder()
                .UnknownNoPayload(eventId: 250, StartQpc + 1)
                .Method(AsyncEventID.ResumeRuntimeAsyncMethod, StartQpc + 2)
                .Build();

            var sink = new AsyncProfilerCollectingSink();
            AsyncProfilerTraceEventParser.ParseBuffer(buffer, sink);

            Assert.Empty(sink.Errors);
            Assert.Single(sink.Unknowns);
            Assert.Equal((AsyncEventID)250, sink.Unknowns[0].EventId);
            Assert.Single(sink.Methods);
        }

        [Fact]
        public void Manifest_DrivesFramingForUnknownFutureEvent()
        {
            // Unknown ids default to no prefix; metadata supplies the future event's UShort framing.
            var manifestEntries = new[]
            {
                new AsyncManifestEntry((AsyncEventID)24, 1, PayloadLengthFieldSize.UShort),
            };
            var futurePayload = new byte[] { 1, 2, 3, 4, 5 };

            var buffer = new AsyncProfilerBufferBuilder()
                .Metadata(StartQpc + 1, qpcFrequency: 1, qpcSync: 1, utcSync: 1, eventBufferSize: 0, wrapperCount: 0, manifestEntries)
                .RawEvent(24, StartQpc + 2, PayloadLengthFieldSize.UShort, futurePayload)
                .Method(AsyncEventID.ResumeRuntimeAsyncMethod, StartQpc + 3)
                .Build();

            var sink = new AsyncProfilerCollectingSink();
            AsyncProfilerTraceEventParser.ParseBuffer(buffer, sink);

            Assert.Empty(sink.Errors);
            Assert.Single(sink.Metadatas);
            AsyncUnknownEvent unknown = Assert.Single(sink.Unknowns);
            Assert.Equal((AsyncEventID)24, unknown.EventId);
            Assert.Equal(futurePayload.Length, unknown.PayloadLength);
            Assert.Single(sink.Methods);
        }

        [Fact]
        public void Manifest_UpgradesKnownEventFieldSize_ReadsV1FieldsAndSkipsAppended()
        {
            // The upgraded payload exceeds a byte-sized length but retains the v1 dispatcher field.
            var entries = new[]
            {
                new AsyncManifestEntry(AsyncEventID.ResumeRuntimeAsyncContext, 2, PayloadLengthFieldSize.UShort),
            };
            var payload = new byte[byte.MaxValue + 2];
            payload[0] = 0x42;
            payload[1] = 0xAA;
            payload[payload.Length - 1] = 0xBB;

            var buffer = new AsyncProfilerBufferBuilder()
                .Metadata(StartQpc + 1, qpcFrequency: 1, qpcSync: 1, utcSync: 1, eventBufferSize: 0, wrapperCount: 0, entries)
                .RawEvent((byte)AsyncEventID.ResumeRuntimeAsyncContext, StartQpc + 2, PayloadLengthFieldSize.UShort, payload)
                .Method(AsyncEventID.CompleteRuntimeAsyncMethod, StartQpc + 3)
                .Build();

            var sink = new AsyncProfilerCollectingSink();
            AsyncProfilerTraceEventParser.ParseBuffer(buffer, sink);

            Assert.Empty(sink.Errors);
            Assert.Equal(0x42UL, Assert.Single(sink.ContextResumes).DispatcherId);
            Assert.Single(sink.Methods);
        }

        [Fact]
        public void Manifest_PersistsAcrossBuffers()
        {
            // Metadata need not be repeated in later buffers, so the caller retains the manifest.
            var manifest = new Manifest();
            var entries = new[]
            {
                new AsyncManifestEntry((AsyncEventID)24, 3, PayloadLengthFieldSize.UShort),
            };

            var metadataBuffer = new AsyncProfilerBufferBuilder()
                .Metadata(StartQpc + 1, qpcFrequency: 1, qpcSync: 1, utcSync: 1, eventBufferSize: 0, wrapperCount: 0, entries)
                .Build();

            var dataBuffer = new AsyncProfilerBufferBuilder()
                .RawEvent(24, StartQpc + 2, PayloadLengthFieldSize.UShort, new byte[] { 9, 9, 9 })
                .Method(AsyncEventID.ResumeRuntimeAsyncMethod, StartQpc + 3)
                .Build();

            var sink = new AsyncProfilerCollectingSink();
            AsyncProfilerTraceEventParser.ParseBuffer(metadataBuffer, manifest, sink);
            AsyncProfilerTraceEventParser.ParseBuffer(dataBuffer, manifest, sink);

            Assert.Empty(sink.Errors);
            Assert.Equal(PayloadLengthFieldSize.UShort, manifest.GetPayloadLengthFieldSize((AsyncEventID)24));
            Assert.Equal((byte)3, manifest.GetVersion((AsyncEventID)24));
            Assert.Single(sink.Unknowns);
            Assert.Single(sink.Methods);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Header_TotalSizeOutsideValidRange_ReportsParseError(bool largerThanPhysicalBuffer)
        {
            byte[] buffer = new AsyncProfilerBufferBuilder().Build();
            WriteUInt32At(buffer, 1, largerThanPhysicalBuffer ? (uint)buffer.Length + 1 : BufferHeader.Size - 1);

            var sink = new AsyncProfilerCollectingSink();
            AsyncProfilerTraceEventParser.ParseBuffer(buffer, sink);

            Assert.Single(sink.Errors);
            Assert.Empty(sink.Order);
        }

        [Fact]
        public void PhysicalPaddingAfterTotalSize_IsIgnored()
        {
            byte[] logical = new AsyncProfilerBufferBuilder()
                .Method(AsyncEventID.ResumeRuntimeAsyncMethod, StartQpc + 1)
                .Build();
            var padded = new byte[logical.Length + 4];
            Array.Copy(logical, padded, logical.Length);
            padded[logical.Length] = 0xFF;
            padded[logical.Length + 1] = 0xEE;

            var sink = new AsyncProfilerCollectingSink();
            AsyncProfilerTraceEventParser.ParseBuffer(padded, sink);

            Assert.Empty(sink.Errors);
            Assert.Single(sink.Methods);
        }

        [Fact]
        public void EventCountLargerThanContents_ReportsParseError()
        {
            byte[] buffer = new AsyncProfilerBufferBuilder()
                .Method(AsyncEventID.ResumeRuntimeAsyncMethod, StartQpc + 1)
                .Build();
            WriteUInt32At(buffer, 17, 2);

            var sink = new AsyncProfilerCollectingSink();
            AsyncProfilerTraceEventParser.ParseBuffer(buffer, sink);

            Assert.Single(sink.Errors);
            Assert.Single(sink.Methods);
        }

        [Fact]
        public void EventCountSmallerThanContents_ReportsParseErrorWithoutDecodingTrailingEvent()
        {
            byte[] buffer = new AsyncProfilerBufferBuilder()
                .Method(AsyncEventID.ResumeRuntimeAsyncMethod, StartQpc + 1)
                .Method(AsyncEventID.CompleteRuntimeAsyncMethod, StartQpc + 2)
                .Build();
            WriteUInt32At(buffer, 17, 1);

            var sink = new AsyncProfilerCollectingSink();
            AsyncProfilerTraceEventParser.ParseBuffer(buffer, sink);

            Assert.Single(sink.Errors);
            AsyncMethodEvent method = Assert.Single(sink.Methods);
            Assert.Equal(AsyncEventID.ResumeRuntimeAsyncMethod, method.EventId);
        }

        [Fact]
        public void TruncatedTimestampVarint_ReportsParseError()
        {
            byte[] buffer = new AsyncProfilerBufferBuilder()
                .Method(AsyncEventID.ResumeRuntimeAsyncMethod, StartQpc + 1)
                .Build();
            buffer[BufferHeader.Size + 1] = 0x80;

            var sink = new AsyncProfilerCollectingSink();
            AsyncProfilerTraceEventParser.ParseBuffer(buffer, sink);

            ParseError error = Assert.Single(sink.Errors);
            Assert.Equal(OsThreadId, error.BufferHeader.Value.OsThreadId);
            Assert.Equal(StartQpc, error.TimestampQpc);
            Assert.Equal(AsyncEventID.ResumeRuntimeAsyncMethod, error.EventId);
            Assert.Empty(sink.Order);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void TruncatedPayloadLength_ReportsParseError(bool ushortLength)
        {
            byte[] complete = new AsyncProfilerBufferBuilder()
                .RawEvent(
                    (byte)(ushortLength ? AsyncEventID.AsyncProfilerMetadata : AsyncEventID.ResumeRuntimeAsyncContext),
                    StartQpc + 1,
                    ushortLength ? PayloadLengthFieldSize.UShort : PayloadLengthFieldSize.Byte,
                    Array.Empty<byte>())
                .Build();
            var truncated = new byte[complete.Length - 1];
            Array.Copy(complete, truncated, truncated.Length);
            WriteUInt32At(truncated, 1, (uint)truncated.Length);

            var sink = new AsyncProfilerCollectingSink();
            AsyncProfilerTraceEventParser.ParseBuffer(truncated, sink);

            Assert.Single(sink.Errors);
            Assert.Empty(sink.Order);
        }

        [Fact]
        public void PayloadExtendsBeyondTotalSize_ReportsParseErrorWithoutDispatch()
        {
            byte[] buffer = new AsyncProfilerBufferBuilder()
                .ResumeContext(AsyncEventID.ResumeRuntimeAsyncContext, StartQpc + 1, dispatcher: 0x42)
                .Build();
            WriteUInt32At(buffer, 1, BufferHeader.Size + 3); // id + delta + length prefix, no payload

            var sink = new AsyncProfilerCollectingSink();
            AsyncProfilerTraceEventParser.ParseBuffer(buffer, sink);

            Assert.Single(sink.Errors);
            Assert.Empty(sink.ContextResumes);
        }

        [Theory]
        [InlineData((int)AsyncEventID.ResumeRuntimeAsyncContext)]
        [InlineData((int)AsyncEventID.UnwindRuntimeAsyncException)]
        [InlineData((int)AsyncEventID.ResumeRuntimeAsyncCallstack)]
        [InlineData((int)AsyncEventID.AsyncProfilerSyncClock)]
        public void UndersizedTypedPayload_ReportsErrorAndContinuesAtDeclaredEnd(int eventIdValue)
        {
            var eventId = (AsyncEventID)eventIdValue;
            byte[] payload = eventId == AsyncEventID.ResumeRuntimeAsyncCallstack ? new byte[] { 0 } : Array.Empty<byte>();
            byte[] buffer = new AsyncProfilerBufferBuilder()
                .RawEvent((byte)eventId, StartQpc + 1, AsyncEventInfo.GetPayloadLengthFieldSize(eventId), payload)
                .Method(AsyncEventID.CompleteRuntimeAsyncMethod, StartQpc + 2)
                .Build();

            var sink = new AsyncProfilerCollectingSink();
            AsyncProfilerTraceEventParser.ParseBuffer(buffer, sink);

            Assert.Single(sink.Errors);
            AsyncMethodEvent method = Assert.Single(sink.Methods);
            Assert.Equal(AsyncEventID.CompleteRuntimeAsyncMethod, method.EventId);
            Assert.Empty(sink.ContextResumes);
            Assert.Empty(sink.Unwinds);
            Assert.Empty(sink.Callstacks);
            Assert.Empty(sink.SyncClocks);
        }

        [Fact]
        public void UndersizedMetadata_DoesNotMutateManifest_AndContinuesAtDeclaredEnd()
        {
            var manifest = new Manifest();
            manifest.Apply(new[]
            {
                new AsyncManifestEntry((AsyncEventID)24, 3, PayloadLengthFieldSize.Byte),
            });

            // The metadata declares only qpcFrequency=1. Without payload bounds, the following context event's
            // framing bytes form a valid-looking remainder of the metadata, including a manifest entry that
            // changes event 24 to a UShort prefix.
            byte[] buffer = new AsyncProfilerBufferBuilder()
                .RawEvent((byte)AsyncEventID.AsyncProfilerMetadata, StartQpc + 1, PayloadLengthFieldSize.UShort, new byte[] { 1 })
                .RawEvent((byte)AsyncEventID.CreateRuntimeAsyncContext, StartQpc + 2, PayloadLengthFieldSize.Byte,
                    new byte[] { 32, 1, 24, 9, (byte)PayloadLengthFieldSize.UShort })
                .RawEvent(24, StartQpc + 3, PayloadLengthFieldSize.Byte, new byte[] { 0xAA })
                .Method(AsyncEventID.CompleteRuntimeAsyncMethod, StartQpc + 4)
                .Build();

            var sink = new AsyncProfilerCollectingSink();
            AsyncProfilerTraceEventParser.ParseBuffer(buffer, manifest, sink);

            Assert.Single(sink.Errors);
            Assert.Empty(sink.Metadatas);
            Assert.Single(sink.ContextCreates);
            Assert.Single(sink.Unknowns);
            Assert.Single(sink.Methods);
            Assert.Equal((byte)3, manifest.GetVersion((AsyncEventID)24));
            Assert.Equal(PayloadLengthFieldSize.Byte, manifest.GetPayloadLengthFieldSize((AsyncEventID)24));
        }

        [Fact]
        public void Metadata_InvalidPayloadLengthFieldSize_DoesNotMutateManifest()
        {
            var manifest = new Manifest();
            manifest.Apply(new[]
            {
                new AsyncManifestEntry((AsyncEventID)24, 3, PayloadLengthFieldSize.Byte),
            });

            byte[] buffer = new AsyncProfilerBufferBuilder()
                .Metadata(StartQpc + 1, qpcFrequency: 1, qpcSync: 1, utcSync: 1, eventBufferSize: 0,
                    wrapperCount: 0, new[]
                    {
                        new AsyncManifestEntry((AsyncEventID)24, 9, PayloadLengthFieldSize.UShort),
                        new AsyncManifestEntry((AsyncEventID)25, 1, (PayloadLengthFieldSize)3),
                    })
                .RawEvent(24, StartQpc + 2, PayloadLengthFieldSize.Byte, new byte[] { 0xAA })
                .Method(AsyncEventID.CompleteRuntimeAsyncMethod, StartQpc + 3)
                .Build();

            var sink = new AsyncProfilerCollectingSink();
            AsyncProfilerTraceEventParser.ParseBuffer(buffer, manifest, sink);

            Assert.Single(sink.Errors);
            Assert.Empty(sink.Metadatas);
            Assert.Single(sink.Unknowns);
            Assert.Single(sink.Methods);
            Assert.Equal((byte)3, manifest.GetVersion((AsyncEventID)24));
            Assert.Equal(PayloadLengthFieldSize.Byte, manifest.GetPayloadLengthFieldSize((AsyncEventID)24));
        }

        #region private

        private static void WriteUInt32At(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        private const uint AsyncThreadContextId = AsyncProfilerTestData.DefaultAsyncThreadContextId;
        private const ulong OsThreadId = AsyncProfilerTestData.DefaultOsThreadId;
        private const long StartQpc = AsyncProfilerTestData.DefaultStartQpc;

        #endregion
    }
}
