using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.GCDynamic;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace TraceEventTests.Regression
{
    public class GCDynamicTraceEventParserTests
    {
        [Fact]
        public void GCDynamicPayloadAllowsValidPayload()
        {
            byte[] data = new byte[] { 1, 2, 3 };
            byte[] payload = CreatePayload("DynamicTraceEvent", data.Length, data, 42);

            WithGCDynamicEvent(payload, delegate (GCDynamicTraceEventImpl traceEvent)
            {
                traceEvent.FixupData();

                Assert.Equal("DynamicTraceEvent", traceEvent.Name);
                Assert.Equal(data.Length, traceEvent.DataSize);
                Assert.Equal(data, traceEvent.Data);
                Assert.Equal(42, traceEvent.ClrInstanceID);
            });
        }

        [Fact]
        public void GCDynamicPayloadRejectsNegativeDataSize()
        {
            byte[] payload = CreatePayload("DynamicTraceEvent", -1, new byte[0], 42);

            WithGCDynamicEvent(payload, delegate (GCDynamicTraceEventImpl traceEvent)
            {
                traceEvent.FixupData();
                AssertSafeDefaultsSurfaced(traceEvent);
            });
        }

        [Fact]
        public void GCDynamicPayloadRejectsOversizedDataSize()
        {
            byte[] payload = CreatePayload("DynamicTraceEvent", 4, new byte[] { 1, 2, 3 }, 42);

            WithGCDynamicEvent(payload, delegate (GCDynamicTraceEventImpl traceEvent)
            {
                traceEvent.FixupData();
                AssertSafeDefaultsSurfaced(traceEvent);
            });
        }

        [Fact]
        public void GCDynamicPayloadRejectsNameWithoutTerminator()
        {
            byte[] payload = Encoding.Unicode.GetBytes("DynamicTraceEvent");

            WithGCDynamicEvent(payload, delegate (GCDynamicTraceEventImpl traceEvent)
            {
                traceEvent.FixupData();
                AssertSafeDefaultsSurfaced(traceEvent);
            });
        }

        /// <summary>
        /// Regression test for the DoS path that the bounds-check fix could
        /// otherwise introduce.  FixupData is called from the core dispatch
        /// loops of every trace source (ETW, EventPipe, BPerf, etc.) without
        /// a surrounding try/catch.  A malformed payload that makes the
        /// validated field accessors throw must NOT cause FixupData itself
        /// to throw, or a single bad event would abort the entire trace.
        /// </summary>
        [Fact]
        public void GCDynamicFixupDataDoesNotThrowOnMalformedPayload()
        {
            // Name has no null terminator -> TryGetPayloadLayout returns false.
            byte[] payload = Encoding.Unicode.GetBytes("DynamicTraceEvent");

            WithGCDynamicEvent(payload, delegate (GCDynamicTraceEventImpl traceEvent)
            {
                Exception thrown = Record.Exception(delegate { traceEvent.FixupData(); });
                Assert.Null(thrown);
            });
        }

        /// <summary>
        /// Regression test ensuring that a payload that passes the basic
        /// bounds check but is too small for the CommittedUsage layout
        /// (DataSize &lt; 42) does NOT get dispatched as a CommittedUsage
        /// event, because the CommittedUsage accessors read at fixed
        /// offsets up to byte 41 via BitConverter and would otherwise throw
        /// ArgumentException during PayloadValue / ToXml.
        /// </summary>
        [Fact]
        public void GCDynamicCommittedUsageWithUndersizedDataStaysGeneric()
        {
            // Use the CommittedUsage opcode name but supply DataSize = 0.
            byte[] payload = CreatePayload("CommittedUsage", 0, new byte[0], 42);

            WithGCDynamicEvent(payload, delegate (GCDynamicTraceEventImpl traceEvent)
            {
                // FixupData must not throw and must NOT select the CommittedUsage template.
                traceEvent.FixupData();
                Assert.NotEqual((int)GCDynamicEventBase.CommittedUsageTemplate.ID, (int)traceEvent.ID);

                // Draining PayloadValues on the generic template must also be exception-safe
                // for this payload — Name/DataSize/Data/ClrInstanceID all read in-bounds bytes.
                foreach (KeyValuePair<string, object> kv in traceEvent.EventPayload.PayloadValues)
                {
                    Assert.NotNull(kv.Key);
                }
            });
        }

        /// <summary>
        /// Positive coverage: a properly sized CommittedUsage payload IS dispatched
        /// as a CommittedUsage event and its fields decode correctly.
        /// </summary>
        [Fact]
        public void GCDynamicCommittedUsageWithValidDataIsDispatched()
        {
            // 42-byte data: Version(2) + 5 * Int64(40).
            byte[] data = new byte[42];
            BitConverter.GetBytes((short)1).CopyTo(data, 0);
            BitConverter.GetBytes((long)100).CopyTo(data, 2);
            BitConverter.GetBytes((long)200).CopyTo(data, 10);
            BitConverter.GetBytes((long)300).CopyTo(data, 18);
            BitConverter.GetBytes((long)400).CopyTo(data, 26);
            BitConverter.GetBytes((long)500).CopyTo(data, 34);

            byte[] payload = CreatePayload("CommittedUsage", data.Length, data, 7);

            WithGCDynamicEvent(payload, delegate (GCDynamicTraceEventImpl traceEvent)
            {
                traceEvent.FixupData();
                Assert.Equal((int)GCDynamicEventBase.CommittedUsageTemplate.ID, (int)traceEvent.ID);

                CommittedUsageTraceEvent committedUsage = (CommittedUsageTraceEvent)traceEvent.EventPayload;
                Assert.Equal(1, committedUsage.Version);
                Assert.Equal(100, committedUsage.TotalCommittedInUse);
                Assert.Equal(500, committedUsage.TotalBookkeepingCommitted);
            });
        }

        /// <summary>
        /// Regression test for the propagated-exception path through
        /// PayloadValues on a malformed payload.  ToXml iterates
        /// EventPayload.PayloadValues; if any yielded accessor throws, ToXml
        /// (called from trace dump tools, the PerfView Events view, and ETW
        /// reloggers without a try/catch) would propagate the exception and
        /// abort the surrounding loop.  PayloadValues must therefore be
        /// exception-safe on malformed input.
        /// </summary>
        [Fact]
        public void GCDynamicPayloadValuesOnMalformedPayloadEmitsSafeDefaults()
        {
            // Name has no null terminator -> ReadPayloadLayout returns null.
            byte[] payload = Encoding.Unicode.GetBytes("DynamicTraceEvent");

            WithGCDynamicEvent(payload, delegate (GCDynamicTraceEventImpl traceEvent)
            {
                traceEvent.FixupData();

                List<KeyValuePair<string, object>> values = new List<KeyValuePair<string, object>>();
                Exception thrown = Record.Exception(delegate
                {
                    foreach (KeyValuePair<string, object> kv in traceEvent.EventPayload.PayloadValues)
                    {
                        values.Add(kv);
                    }
                });

                Assert.Null(thrown);
                Assert.Equal(4, values.Count);
                Assert.Equal("Name", values[0].Key);
                Assert.Equal(string.Empty, values[0].Value);
                Assert.Equal("DataSize", values[1].Key);
                Assert.Equal(0, values[1].Value);
                Assert.Equal("Data", values[2].Key);
                Assert.Equal(string.Empty, values[2].Value);
                Assert.Equal("ClrInstanceID", values[3].Key);
                Assert.Equal(0, values[3].Value);
            });
        }

        /// <summary>
        /// Regression test ensuring PayloadValue on the generic GCDynamic template
        /// returns safe placeholder values rather than throwing when the payload
        /// is malformed.  Direct PayloadValue access happens from the PerfView
        /// Events grid and from generic event-printing utilities; an unhandled
        /// exception there would crash the consumer.
        /// </summary>
        [Fact]
        public void GCDynamicPayloadValueOnMalformedPayloadReturnsSafeDefaults()
        {
            byte[] payload = Encoding.Unicode.GetBytes("DynamicTraceEvent");

            WithGCDynamicEvent(payload, delegate (GCDynamicTraceEventImpl traceEvent)
            {
                traceEvent.FixupData();

                Assert.Equal(string.Empty, traceEvent.PayloadValue(0));
                Assert.Equal(0, traceEvent.PayloadValue(1));
                Assert.Equal(Array.Empty<byte>(), (byte[])traceEvent.PayloadValue(2));
                Assert.Equal(0, traceEvent.PayloadValue(3));
            });
        }

        private static void AssertSafeDefaultsSurfaced(GCDynamicTraceEventImpl traceEvent)
        {
            // The framework-facing accessors (PayloadValue / PayloadValues / ToXml)
            // are the ones called on the dispatch hot path; they must always be
            // exception-safe and surface type-appropriate safe defaults rather
            // than throw when the underlying payload is malformed.
            Assert.Equal(string.Empty, traceEvent.PayloadValue(0));
            Assert.Equal(0, traceEvent.PayloadValue(1));
            Assert.Equal(Array.Empty<byte>(), (byte[])traceEvent.PayloadValue(2));
            Assert.Equal(0, traceEvent.PayloadValue(3));

            List<KeyValuePair<string, object>> values = new List<KeyValuePair<string, object>>();
            foreach (KeyValuePair<string, object> kv in traceEvent.EventPayload.PayloadValues)
            {
                values.Add(kv);
            }

            Assert.Equal(4, values.Count);
            Assert.Equal("Name", values[0].Key);
            Assert.Equal("DataSize", values[1].Key);
            Assert.Equal("Data", values[2].Key);
            Assert.Equal("ClrInstanceID", values[3].Key);
        }

        private static byte[] CreatePayload(string name, int dataSize, byte[] data, short clrInstanceID)
        {
            List<byte> payload = new List<byte>();
            payload.AddRange(Encoding.Unicode.GetBytes(name));
            payload.Add(0);
            payload.Add(0);
            payload.AddRange(BitConverter.GetBytes(dataSize));
            payload.AddRange(data);
            payload.AddRange(BitConverter.GetBytes(clrInstanceID));
            return payload.ToArray();
        }

        private static unsafe void WithGCDynamicEvent(byte[] payload, Action<GCDynamicTraceEventImpl> action)
        {
            fixed (byte* payloadBytes = payload)
            {
                TraceEventNativeMethods.EVENT_RECORD eventRecord = new TraceEventNativeMethods.EVENT_RECORD();
                eventRecord.EventHeader.ProviderId = GCDynamicTraceEventParser.ProviderGuid;
                eventRecord.EventHeader.Id = 39;
                eventRecord.UserDataLength = (ushort)payload.Length;
                eventRecord.UserData = (IntPtr)payloadBytes;

                GCDynamicTraceEventImpl traceEvent = new GCDynamicTraceEventImpl(null, 39, 1, "GC", Guid.Empty, 41, "DynamicTraceEvent", GCDynamicTraceEventParser.ProviderGuid, "Microsoft-Windows-DotNETRuntime");
                traceEvent.eventRecord = &eventRecord;
                traceEvent.userData = eventRecord.UserData;

                action(traceEvent);
            }
        }
    }
}
