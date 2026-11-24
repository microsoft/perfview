using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.MicrosoftWindowsNDISPacketCapture;
using System;
using System.IO;
using System.Text;
using Xunit;

namespace TraceEventTests.Regression
{
    public unsafe class PacketBoundsTests
    {
        [Fact]
        public void RawPointersRequireCompleteDeclaredData()
        {
            IsolatedTest.Run(() =>
            {
                foreach (int length in new[] { 0, 8, 11, 12, 16 })
                foreach (int size in new[] { -1, 5, int.MaxValue })
                {
                    WithPacket(new byte[4], length, size, data =>
                        Assert.Throws<InvalidDataException>(() => (IntPtr)data.Fragment));
                    var metadata = new PacketMetaDataArgs(null, 1, 0, "", Guid.Empty, 0, "", Guid.Empty, "", null);
                    WithData(metadata, new byte[4], length, size, () =>
                        Assert.Throws<InvalidDataException>(() => (IntPtr)metadata.Metadata));
                }
            }, GetType());
        }

        [Fact]
        public void NestedHeadersAreBoundedByCaptureAndIpLength()
        {
            IsolatedTest.Run(() =>
            {
                foreach (int captured in new[] { 20, 39 })
                {
                    byte[] packet = TcpPacket(40);
                    WithPacket(packet, 12 + captured, captured,
                        data => Assert.Throws<InvalidDataException>(() => data.ParsedPacket));
                }
                foreach (int headerWords in new[] { 0, 4, 15 })
                {
                    byte[] packet = TcpPacket(40);
                    packet[32] = (byte)(headerWords << 4);
                    WithPacket(packet, 52, 40,
                        data => Assert.Throws<InvalidDataException>(() => data.TcpStreamSample));
                }
                byte[] shortIp = TcpPacket(40);
                shortIp[3] = 20;
                WithPacket(shortIp, 52, 40,
                    data => Assert.Throws<InvalidDataException>(() => data.ParsedPacket));
                byte[] shortUdp = TcpPacket(27);
                shortUdp[9] = 17;
                WithPacket(shortUdp, 39, 27,
                    data => Assert.Throws<InvalidDataException>(() => data.ParsedPacket));
            }, GetType());
        }

        [Fact]
        public void ArpPacketsPreserveNonIpFallback()
        {
            IsolatedTest.Run(() =>
            {
                foreach (int length in new[] { 42, 60 })
                {
                    byte[] packet = ArpPacket(length);
                    WithPacket(packet, 12 + length, length, data =>
                    {
                        Assert.StartsWith("NON-IP FRAGMENT ", data.ParsedPacket);
                        Assert.Empty(data.TcpStreamSample);
                        Assert.Contains("NON-IP FRAGMENT ", data.ToXml(new StringBuilder()).ToString());
                    });
                }
            }, GetType());
        }

        [Fact]
        public void IncompleteIpCandidatesPreserveNonIpFallback()
        {
            IsolatedTest.Run(() =>
            {
                foreach (int captured in new[] { 1, 19 })
                {
                    byte[] packet = TcpPacket(40);
                    WithPacket(packet, 12 + captured, captured, data =>
                    {
                        Assert.StartsWith("NON-IP FRAGMENT ", data.ParsedPacket);
                        Assert.Empty(data.TcpStreamSample);
                    });
                }
            }, GetType());
        }

        [Fact]
        public void NoninitialIpFragmentsSkipTransportHeaders()
        {
            IsolatedTest.Run(() =>
            {
                foreach (byte protocol in new byte[] { 6, 17 })
                foreach (int offset in new[] { 1, 256 })
                foreach (int length in new[] { 4, 8, 40 })
                {
                    byte[] payload = new byte[length];
                    if (length == 40)
                    {
                        payload[3] = 80;
                        payload[5] = 40;
                        payload[12] = 0x50;
                        Encoding.ASCII.GetBytes("not a TCP stream    ").CopyTo(payload, 20);
                    }
                    int fragment = offset | (length == 8 ? 0x2000 : 0);
                    byte[] packet = IpPacket(protocol, fragment, payload);
                    WithPacket(packet, 12 + packet.Length, packet.Length, data =>
                    {
                        string parsed = data.ParsedPacket;
                        Assert.Contains("Source=192.0.2.1 Dest=198.51.100.2", parsed);
                        Assert.Contains("IP(" + protocol + ")", parsed);
                        Assert.Contains(" Len=" + length, parsed);
                        Assert.DoesNotContain(" SPort=", parsed);
                        Assert.Empty(data.TcpStreamSample);
                        Assert.DoesNotContain("<Printable>", data.ToXml(new StringBuilder()).ToString());
                    });
                }
            }, GetType());
        }

        [Fact]
        public void InitialIpFragmentsStillParseTransportHeaders()
        {
            IsolatedTest.Run(() =>
            {
                foreach (byte protocol in new byte[] { 6, 17 })
                foreach (int flags in new[] { 0, 0x2000 })
                {
                    byte[] payload = new byte[protocol == 6 ? 40 : 16];
                    payload[0] = 4;
                    payload[1] = 0xd2;
                    payload[3] = (byte)(protocol == 6 ? 80 : 53);
                    if (protocol == 6)
                    {
                        payload[12] = 0x50;
                        Encoding.ASCII.GetBytes("first fragment data ").CopyTo(payload, 20);
                    }
                    else
                    {
                        payload[5] = (byte)(flags == 0 ? 16 : 24);
                    }
                    byte[] packet = IpPacket(protocol, flags, payload);
                    WithPacket(packet, 12 + packet.Length, packet.Length, data =>
                    {
                        string parsed = data.ParsedPacket;
                        Assert.Contains(protocol == 6 ? " TCP" : " UDP", parsed);
                        Assert.Contains(" SPort=1234", parsed);
                        Assert.Contains(protocol == 6 ? " DPort=80" : " DPort=53", parsed);
                        if (protocol == 6)
                        {
                            Assert.Equal("first fragment data ", data.TcpStreamSample);
                        }
                        else
                        {
                            Assert.Empty(data.TcpStreamSample);
                        }
                    });
                }
            }, GetType());
        }

        [Fact]
        public void TcpOptionsRequireCompletePrefixesAndValues()
        {
            IsolatedTest.Run(() =>
            {
                foreach (byte[] options in new[]
                {
                    new byte[] { 1, 1, 1, 2 }, new byte[] { 2, 0, 0, 0 },
                    new byte[] { 2, 1, 0, 0 }, new byte[] { 2, 5, 0, 0 }
                })
                {
                    byte[] packet = TcpPacket(44);
                    packet[32] = 0x60;
                    options.CopyTo(packet, 40);
                    WithPacket(packet, 56, 44,
                        data => Assert.Throws<InvalidDataException>(() => data.ParsedPacket));
                }
            }, GetType());
        }

        [Fact]
        public void PrintableDataAndSavedHeadersStayWithinTheirOwnFragments()
        {
            IsolatedTest.Run(() =>
            {
                byte[] body = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n    ");
                byte[] packet = TcpPacket(40 + body.Length);
                body.CopyTo(packet, 40);
                WithPacket(packet, 12 + packet.Length, packet.Length, data =>
                {
                    Assert.Contains("HTTP", data.ParsedPacket);
                    Assert.StartsWith("HTTP/1.1", data.TcpStreamSample);
                });
                var continued = new PacketFragmentArgs(null, 1, 0, "", Guid.Empty, 0, "", Guid.Empty, "", null);
                byte[] header = TcpPacket(40);
                header[3] = 100;
                WithData(continued, header, 52, 40, () => Assert.Contains("TCP", continued.ParsedPacket));
                continued.eventIndex = (EventIndex)1;
                byte[] text = Encoding.ASCII.GetBytes("GET / HTTP/1.1\r\n");
                WithData(continued, text, 12 + text.Length, text.Length,
                    () => Assert.Contains("Stream=GET / HTTP/1.1", continued.ParsedPacket));
                continued.eventIndex = (EventIndex)2;
                WithData(continued, text, 12 + text.Length, text.Length + 1,
                    () => Assert.Throws<InvalidDataException>(() => continued.ParsedPacket));
            }, GetType());
        }

        [Fact]
        public void CompleteHeadersPermitPartialPacketCaptures()
        {
            IsolatedTest.Run(() =>
            {
                byte[] packet = TcpPacket(40);
                packet[3] = 100;
                WithPacket(packet, 52, 40, data =>
                {
                    Assert.Contains("TCP", data.ParsedPacket);
                    Assert.Equal("", data.TcpStreamSample);
                });
                byte[] options = TcpPacket(44);
                options[32] = 0x60;
                options[40] = 2;
                options[41] = 4;
                options[42] = 5;
                options[43] = 0xb4;
                WithPacket(options, 56, 44, data => Assert.Contains("MaxSeg=1460", data.ParsedPacket));
                byte[] ipOptions = TcpPacket(44);
                ipOptions[0] = 0x46;
                ipOptions[23] = 0;
                ipOptions[27] = 80;
                ipOptions[36] = 0x50;
                WithPacket(ipOptions, 56, 44, data => Assert.Contains("TCP", data.ParsedPacket));
                byte[] extraCapture = TcpPacket(60);
                extraCapture[3] = 40;
                Encoding.ASCII.GetBytes("not packet data     ").CopyTo(extraCapture, 40);
                WithPacket(extraCapture, 72, 60, data => Assert.Empty(data.TcpStreamSample));
            }, GetType());
        }

        #region private
        private static byte[] TcpPacket(int length)
        {
            byte[] result = new byte[Math.Max(length, 40)];
            result[0] = 0x45;
            result[3] = (byte)length;
            result[9] = 6;
            result[23] = 80;
            result[32] = 0x50;
            return result;
        }

        private static byte[] ArpPacket(int length)
        {
            // Ethernet/IPv4 ARP request targeting 69.0.0.1 (0x45 in the target address).
            byte[] packet = new byte[length];
            for (int i = 0; i < 6; i++)
                packet[i] = 0xff;
            packet[6] = 2;
            packet[11] = 1;
            packet[12] = 8;
            packet[13] = 6;
            packet[15] = 1;
            packet[16] = 8;
            packet[18] = 6;
            packet[19] = 4;
            packet[21] = 1;
            Array.Copy(packet, 6, packet, 22, 6);
            new byte[] { 192, 0, 2, 1 }.CopyTo(packet, 28);
            new byte[] { 69, 0, 0, 1 }.CopyTo(packet, 38);
            return packet;
        }

        private static byte[] IpPacket(byte protocol, int fragment, byte[] payload)
        {
            byte[] packet = new byte[20 + payload.Length];
            packet[0] = 0x45;
            packet[2] = (byte)(packet.Length >> 8);
            packet[3] = (byte)packet.Length;
            packet[4] = 0x12;
            packet[5] = 0x34;
            packet[6] = (byte)(fragment >> 8);
            packet[7] = (byte)fragment;
            packet[8] = 64;
            packet[9] = protocol;
            new byte[] { 192, 0, 2, 1 }.CopyTo(packet, 12);
            new byte[] { 198, 51, 100, 2 }.CopyTo(packet, 16);
            uint sum = 0;
            for (int i = 0; i < 20; i += 2)
                sum += (uint)((packet[i] << 8) | packet[i + 1]);
            while (sum > 0xffff)
                sum = (sum & 0xffff) + (sum >> 16);
            ushort checksum = (ushort)~sum;
            packet[10] = (byte)(checksum >> 8);
            packet[11] = (byte)checksum;
            payload.CopyTo(packet, 20);
            return packet;
        }

        private static void WithPacket(byte[] packet, int length, int size, Action<PacketFragmentArgs> test)
        {
            using (var source = (TraceLog)Activator.CreateInstance(typeof(TraceLog), true))
            {
                source._QPCFreq = 1;
                source._syncTimeQPC = 1;
                source._syncTimeUTC = DateTime.UtcNow;
                source.sessionStartTimeQPC = 1;
                var data = new PacketFragmentArgs(null, 1, 0, "PacketFragment", Guid.Empty, 0, "", Guid.Empty, "", null);
                data.eventIndex = (EventIndex)1;
                data.traceEventSource = source;
                WithData(data, packet, length, size, () => test(data));
            }
        }

        private static void WithData(TraceEvent data, byte[] packet, int length, int size, Action test)
        {
            byte[] storage = new byte[packet.Length + 128];
            BitConverter.GetBytes(size).CopyTo(storage, 8);
            packet.CopyTo(storage, 12);
            fixed (byte* pointer = storage)
            {
                var record = new TraceEventNativeMethods.EVENT_RECORD();
                record.EventHeader.Id = 1;
                record.UserDataLength = (ushort)length;
                data.eventRecord = &record;
                data.userData = (IntPtr)pointer;
                try { test(); }
                finally { data.eventRecord = null; data.userData = IntPtr.Zero; }
            }
        }
        #endregion
    }
}
