using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using System;
using System.IO;
using Xunit;

namespace TraceEventTests.Regression
{
    public class PayloadBoundsTests
    {
        [Fact]
        public void ScalarsRequireTheirEntireWidth()
        {
            IsolatedTest.Run(() =>
            {
                var readers = new (int Width, Func<TraceEvent, int, object> Read)[]
                {
                    (1, (e, o) => e.GetByteAt(o)), (2, (e, o) => e.GetInt16At(o)),
                    (4, (e, o) => e.GetInt32At(o)), (8, (e, o) => e.GetInt64At(o)),
                    (4, (e, o) => e.GetSingleAt(o)), (8, (e, o) => e.GetDoubleAt(o)),
                    (16, (e, o) => e.GetGuidAt(o)), (16, (e, o) => e.GetIPAddrV6At(o))
                };
                foreach (var reader in readers)
                foreach (int offset in new[] { 0, 3 })
                {
                    for (int remaining = 0; remaining < reader.Width; remaining++)
                    {
                        WithPayload(new UnhandledTraceEvent(), new byte[32], offset + remaining,
                            e => Assert.Throws<InvalidDataException>(() => reader.Read(e, offset)));
                    }
                    WithPayload(new UnhandledTraceEvent(), new byte[32], offset + reader.Width, e =>
                    {
                        reader.Read(e, offset);
                        Assert.Throws<InvalidDataException>(() => reader.Read(e, -1));
                        Assert.Throws<InvalidDataException>(() => reader.Read(e, int.MaxValue));
                    });
                }
            }, GetType());
        }

        [Fact]
        public void ArraysAndFixedStringsValidateCountsBeforeAllocation()
        {
            IsolatedTest.Run(() => WithPayload(new UnhandledTraceEvent(), new byte[16], 4, e =>
            {
                foreach (int count in new[] { -1, 5, int.MaxValue })
                {
                    Assert.Throws<InvalidDataException>(() => e.GetByteArrayAt(0, count));
                    Assert.Throws<InvalidDataException>(() => e.GetInt32ArrayAt(0, count));
                    Assert.Throws<InvalidDataException>(() => e.GetFixedAnsiStringAt(count, 0));
                    Assert.Throws<InvalidDataException>(() => e.GetFixedUnicodeStringAt(count, 0));
                }
                Assert.Throws<InvalidDataException>(() => e.GetInt32ArrayAt(1, 1));
                Assert.Throws<InvalidDataException>(() => e.EventData(null, 0, -1, 1));
                Assert.Throws<InvalidDataException>(() => e.EventData(null, 0, 1, int.MaxValue));
                Assert.Equal(new byte[4], e.GetByteArrayAt(0, 4));
                Assert.Equal(new int[1], e.GetInt32ArrayAt(0, 1));
                Assert.Empty(e.GetByteArrayAt(4, 0));
            }), GetType());
        }

        [Fact]
        public void EventDataRejectsNonzeroOffsetsWhenAllocating()
        {
            IsolatedTest.Run(() => WithPayload(new UnhandledTraceEvent(), new byte[] { 42 }, 1, e =>
            {
                foreach (int offset in new[] { -1, 1, 1048576, int.MaxValue })
                foreach (int length in new[] { 0, 1 })
                {
                    Assert.Throws<ArgumentOutOfRangeException>("targetStartIndex",
                        () => e.EventData(null, offset, 0, length));
                }
            }), GetType());
        }

        [Fact]
        public void EventDataPreservesValidCopyOffsets()
        {
            IsolatedTest.Run(() => WithPayload(new UnhandledTraceEvent(), new byte[] { 10, 20, 30, 40 }, 4, e =>
            {
                Assert.Equal(new byte[] { 10, 20, 30, 40 }, e.EventData());
                Assert.Equal(new byte[] { 20, 30 }, e.EventData(null, 0, 1, 2));
                Assert.Empty(e.EventData(null, 0, 4, 0));

                byte[] target = new byte[] { 1, 2, 3, 4, 5 };
                Assert.Same(target, e.EventData(target, 2, 1, 2));
                Assert.Equal(new byte[] { 1, 2, 20, 30, 5 }, target);
                Assert.Same(target, e.EventData(target, target.Length, 4, 0));
                Assert.Equal(new byte[] { 1, 2, 20, 30, 5 }, target);
            }), GetType());
        }

        [Fact]
        public void StringsUseTheRemainingPayload()
        {
            IsolatedTest.Run(() =>
            {
                WithPayload(new UnhandledTraceEvent(), new byte[] { 99, 65, 66, 67, 0 }, 3, e =>
                {
                    Assert.Equal("AB", e.GetUTF8StringAt(1));
                    Assert.Throws<InvalidDataException>(() => e.GetUTF8StringAt(-1));
                    Assert.Throws<InvalidDataException>(() => e.SkipUTF8String(1));
                    Assert.Throws<InvalidDataException>(() => e.SkipUnicodeString(1));
                });
                WithPayload(new UnhandledTraceEvent(), new byte[] { 99, 65, 0, 66, 0, 0 }, 4, e =>
                    Assert.Equal("A", e.GetUnicodeStringAt(1)));
                WithPayload(new UnhandledTraceEvent(), new byte[] { 99, 2, 0, 65, 66 }, 4, e =>
                {
                    Assert.Throws<InvalidDataException>(() => e.GetShortUTF8StringAt(1));
                    Assert.Throws<InvalidDataException>(() => e.SkipShortUTF8String(1));
                    Assert.Throws<InvalidDataException>(() => e.GetShortUTF8StringAt(3));
                });
                WithPayload(new UnhandledTraceEvent(), new byte[] { 99, 2, 0, 65, 66 }, 5, e =>
                {
                    Assert.Equal("AB", e.GetShortUTF8StringAt(1));
                    Assert.Equal(5, e.SkipShortUTF8String(1));
                });
            }, GetType());
        }

        [Fact]
        public void VariableFieldsAndSymbolChunksValidateTheirEndpoints()
        {
            IsolatedTest.Run(() =>
            {
                WithPayload(new UnhandledTraceEvent(), new byte[] { 0x80, 0x80, 0 }, 2, e =>
                {
                    Assert.Throws<InvalidDataException>(() => e.SkipVarInt(0));
                    Assert.Throws<InvalidDataException>(() => e.SkipVarInt(-1));
                    Assert.Throws<InvalidDataException>(() => e.SkipSID(0));
                });
                byte[] sid = new byte[16];
                sid[0] = 1;
                sid[9] = 255;
                WithPayload(new UnhandledTraceEvent(), sid, 10,
                    e => Assert.Throws<InvalidDataException>(() => e.SkipSID(0)));
                foreach (int length in new[] { -16, int.MaxValue, 3 })
                {
                    byte[] payload = new byte[18];
                    BitConverter.GetBytes(length).CopyTo(payload, 12);
                    var symbols = new CodeSymbolsTraceData(null, 1, 0, "", Guid.Empty, 0, "", Guid.Empty, "");
                    WithPayload(symbols, payload, payload.Length,
                        e => Assert.Throws<InvalidDataException>(() => symbols.ClrInstanceID));
                }
            }, GetType());
        }

        [Fact]
        public void VersionOneTcpReceiveLayoutsFollowPointerWidths()
        {
            IsolatedTest.Run(() =>
            {
                foreach (bool wide in new[] { false, true })
                {
                    var ipv4 = new TcpIpTraceData(e => { }, 1, 0, "", Guid.Empty, 0, "", Guid.Empty, "", null);
                    var ipv6 = new TcpIpV6TraceData(e => { }, 1, 0, "", Guid.Empty, 0, "", Guid.Empty, "", null);
                    var layouts = new (TraceEvent Data, int Offset, Func<ulong> Connection, Func<int> Sequence)[]
                    {
                        (ipv4, 20, () => ipv4.connid, () => ipv4.seqnum),
                        (ipv6, 44, () => ipv6.connid, () => ipv6.seqnum)
                    };
                    foreach (var layout in layouts)
                    {
                        int pointerSize = wide ? 8 : 4;
                        int sequenceOffset = layout.Offset + pointerSize;
                        ulong connection = wide ? 0x12345678abcdef01UL : 0xabcdef01UL;
                        const int Sequence = 0x13572468;
                        int length = sequenceOffset + 4;
                        byte[] payload = new byte[length + 4];
                        (wide ? BitConverter.GetBytes(connection) : BitConverter.GetBytes((uint)connection))
                            .CopyTo(payload, layout.Offset);
                        BitConverter.GetBytes(Sequence).CopyTo(payload, sequenceOffset);
                        foreach (int captured in new[] { length, length + 4 })
                        {
                            WithPayload(layout.Data, payload, captured, e =>
                            {
                                Assert.Equal(connection, layout.Connection());
                                Assert.Equal(Sequence, layout.Sequence());
                                e.Dispatch();
                            }, wide, version: 1);
                        }
                        for (int remaining = 0; remaining < pointerSize; remaining++)
                        {
                            WithPayload(layout.Data, payload, layout.Offset + remaining,
                                e => Assert.Throws<InvalidDataException>(() => layout.Connection()), wide, version: 1);
                        }
                        for (int remaining = 0; remaining < 4; remaining++)
                        {
                            WithPayload(layout.Data, payload, sequenceOffset + remaining,
                                e => Assert.Throws<InvalidDataException>(() => layout.Sequence()), wide, version: 1);
                        }
                    }
                }
            }, GetType());
        }

        [Fact]
        public void VersionTwoTcpLayoutsSupportNarrowAndPointerSizedConnectionIds()
        {
            IsolatedTest.Run(() =>
            {
                foreach (bool wide in new[] { false, true })
                {
                    var receive = new TcpIpTraceData(e => { }, 1, 0, "", Guid.Empty, 0, "", Guid.Empty, "", null);
                    var send = new TcpIpSendTraceData(e => { }, 1, 0, "", Guid.Empty, 0, "", Guid.Empty, "", null);
                    var connect = new TcpIpConnectTraceData(e => { }, 1, 0, "", Guid.Empty, 0, "", Guid.Empty, "", null);
                    var receiveV6 = new TcpIpV6TraceData(e => { }, 1, 0, "", Guid.Empty, 0, "", Guid.Empty, "", null);
                    var sendV6 = new TcpIpV6SendTraceData(e => { }, 1, 0, "", Guid.Empty, 0, "", Guid.Empty, "", null);
                    var connectV6 = new TcpIpV6ConnectTraceData(e => { }, 1, 0, "", Guid.Empty, 0, "", Guid.Empty, "", null);
                    var layouts = new (TraceEvent Data, int Offset, Func<ulong> Connection, Func<int> Sequence)[]
                    {
                        (receive, 24, () => receive.connid, () => receive.seqnum),
                        (send, 32, () => send.connid, () => send.seqnum),
                        (connect, 40, () => connect.connid, () => connect.seqnum),
                        (receiveV6, 48, () => receiveV6.connid, () => receiveV6.seqnum),
                        (sendV6, 56, () => sendV6.connid, () => sendV6.seqnum),
                        (connectV6, 64, () => connectV6.connid, () => connectV6.seqnum)
                    };
                    foreach (var layout in layouts)
                    {
                        int pointerSize = wide ? 8 : 4;
                        const int Sequence = 0x13572468;
                        foreach (int connectionSize in wide ? new[] { 4, 8 } : new[] { 4 })
                        {
                            ulong connection = connectionSize == 8 ? 0x12345678abcdef01UL : 0xabcdef01UL;
                            int length = layout.Offset + connectionSize;
                            byte[] payload = new byte[length + 4];
                            (connectionSize == 8 ? BitConverter.GetBytes(connection) : BitConverter.GetBytes((uint)connection))
                                .CopyTo(payload, layout.Offset);
                            BitConverter.GetBytes(Sequence).CopyTo(payload, layout.Offset - 4);
                            WithPayload(layout.Data, payload, length, e =>
                            {
                                Assert.Equal(connection, layout.Connection());
                                Assert.Equal(Sequence, layout.Sequence());
                                e.Dispatch();
                            }, wide);

                            if (connectionSize == pointerSize)
                            {
                                WithPayload(layout.Data, payload, payload.Length, e =>
                                {
                                    Assert.Equal(connection, layout.Connection());
                                    Assert.Equal(Sequence, layout.Sequence());
                                }, wide);
                            }
                            WithPayload(layout.Data, payload, length, e =>
                            {
                                if (wide && connectionSize == 4)
                                {
                                    Assert.Throws<InvalidDataException>(() => layout.Connection());
                                }
                                else
                                {
                                    Assert.Equal(connection, layout.Connection());
                                    Assert.Equal(Sequence, layout.Sequence());
                                    e.Dispatch();
                                }
                            }, wide, version: 3);
                        }
                        byte[] truncated = new byte[layout.Offset + pointerSize];
                        for (int remaining = 0; remaining < pointerSize; remaining++)
                        {
                            if (remaining == 4)
                            {
                                continue; // This extent is the complete narrow V2 layout, tested above.
                            }
                            WithPayload(layout.Data, truncated, layout.Offset + remaining,
                                e => Assert.Throws<InvalidDataException>(() => layout.Connection()), wide);
                        }
                        for (int remaining = 0; remaining < 4; remaining++)
                        {
                            WithPayload(layout.Data, truncated, layout.Offset - 4 + remaining,
                                e => Assert.Throws<InvalidDataException>(() => layout.Sequence()), wide);
                        }
                    }
                }
            }, GetType());
        }

        [Fact]
        public void VersionZeroTcpConnectionFieldsRemainAbsent()
        {
            var data = new TcpIpTraceData(null, 1, 0, "", Guid.Empty, 0, "", Guid.Empty, "", null);
            WithPayload(data, new byte[0], 0, e =>
            {
                Assert.Equal(0UL, data.connid);
                Assert.Equal(0, data.seqnum);
            }, version: 0);
        }

        [Fact]
        public void ZeroCountUnicodeSkipPreservesTheOffset()
        {
            WithPayload(new UnhandledTraceEvent(), new byte[0], 0, e =>
            {
                foreach (int offset in new[] { -1, 0, 1, int.MaxValue })
                    Assert.Equal(offset, e.SkipUnicodeString(offset, 0));
                Assert.Throws<InvalidDataException>(() => e.SkipUnicodeString(0, -1));
            });
        }

        [Fact]
        public void SecurityIdentifiersRequireTheirCompleteExtent()
        {
            IsolatedTest.Run(() =>
            {
                foreach (bool wide in new[] { false, true })
                foreach (int offset in new[] { 0, 3 })
                foreach (byte count in new byte[] { 0, 1, 255 })
                {
                    int tokenSize = wide ? 16 : 8;
                    int end = offset + tokenSize + 8 + 4 * count;
                    byte[] payload = new byte[end];
                    payload[offset] = 1;
                    payload[offset + tokenSize + 1] = count;
                    WithPayload(new UnhandledTraceEvent(), payload, end,
                        e => Assert.Equal(end, e.SkipSID(offset)), wide);
                    for (int length = offset; length < offset + tokenSize + 8; length++)
                    {
                        WithPayload(new UnhandledTraceEvent(), payload, length,
                            e => Assert.Throws<InvalidDataException>(() => e.SkipSID(offset)), wide);
                    }
                    WithPayload(new UnhandledTraceEvent(), payload, end - 1,
                        e => Assert.Throws<InvalidDataException>(() => e.SkipSID(offset)), wide);
                }
                WithPayload(new UnhandledTraceEvent(), new byte[4], 4,
                    e => Assert.Equal(4, e.SkipSID(0)));
            }, GetType());
        }

        [Fact]
        public void DynamicScalarFormattingUsesCheckedGetters()
        {
            IsolatedTest.Run(() =>
            {
                var data = new DynamicTraceEventData(null, 1, 0, "", Guid.Empty, 0, "", Guid.Empty, "");
                data.payloadFetches = new[]
                {
                    new DynamicTraceEventData.PayloadFetch(1, 8, typeof(long)),
                    new DynamicTraceEventData.PayloadFetch(7, 1, typeof(byte))
                };
                data.payloadNames = new[] { "Value", "Sentinel" };
                WithPayload(data, new byte[16], 8, e =>
                    Assert.Contains("InvalidDataException", data.PayloadString(0)));
            }, GetType());
        }

        #region private
        private static unsafe void WithPayload(TraceEvent data, byte[] payload, int length, Action<TraceEvent> test, bool wide = false, byte version = 2)
        {
            byte[] storage = new byte[payload.Length + 64];
            payload.CopyTo(storage, 16);
            fixed (byte* pointer = storage)
            {
                var record = new TraceEventNativeMethods.EVENT_RECORD();
                record.EventHeader.Id = (ushort)data.eventID;
                record.EventHeader.Version = version;
                record.UserDataLength = (ushort)length;
                record.EventHeader.Flags = wide ? TraceEventNativeMethods.EVENT_HEADER_FLAG_64_BIT_HEADER :
                    TraceEventNativeMethods.EVENT_HEADER_FLAG_32_BIT_HEADER;
                data.eventRecord = &record;
                data.userData = (IntPtr)(pointer + 16);
                try
                {
                    test(data);
                }
                finally
                {
                    data.eventRecord = null;
                    data.userData = IntPtr.Zero;
                }
            }
        }
        #endregion
    }
}
