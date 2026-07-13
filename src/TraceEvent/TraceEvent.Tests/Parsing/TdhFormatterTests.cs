using Microsoft.Diagnostics.Tracing.Parsers;

using System;

using Xunit;

using FormatHint = Microsoft.Diagnostics.Tracing.Parsers.TdhFormatter.FormatHint;

namespace TraceEventTests
{
    public class TdhFormatterTests
    {
        [Fact]
        public void Format_HexHint()
        {
            // Hex values are the width of their underlying type.
            Assert.Equal("0x01", TdhFormatter.Format((Byte)1, FormatHint.Hex));
            Assert.Equal("0x14", TdhFormatter.Format((SByte)20, FormatHint.Hex));
            Assert.Equal("0x012C", TdhFormatter.Format((Int16)300, FormatHint.Hex));
            Assert.Equal("0x0FA0", TdhFormatter.Format((UInt16)4000, FormatHint.Hex));
            Assert.Equal("0x0000C350", TdhFormatter.Format((Int32)50000, FormatHint.Hex));
            Assert.Equal("0x000927C0", TdhFormatter.Format((UInt32)600000, FormatHint.Hex));
            Assert.Equal("0x00000000006ACFC0", TdhFormatter.Format((Int64)7000000, FormatHint.Hex));
            Assert.Equal("0x0000000004C4B400", TdhFormatter.Format((UInt64)80000000, FormatHint.Hex));

            // IntPtr and UIntPtr have a minimum width of 8 and use lowercase
            Assert.Equal("0x0000000a", TdhFormatter.Format((IntPtr)0xa, FormatHint.Pointer));
            Assert.Equal("0x0000000b", TdhFormatter.Format((UIntPtr)0xb, FormatHint.Pointer));


            // Negative values are rendered using 2's complement.
            Assert.Equal("0x8001", TdhFormatter.Format((Int16)(-32767), FormatHint.Hex));
            Assert.Equal("0xFFFFFFFF", TdhFormatter.Format((Int32)(-1), FormatHint.Hex));
            Assert.Equal("0xFFFFFFFFFFFFFFFF", TdhFormatter.Format((Int64)(-1), FormatHint.Hex));

            // Hex hints do not work for non-numeric values.
            Assert.Null(TdhFormatter.Format("not a number", FormatHint.Hex));
        }

        [Fact]
        public void Format_PointerHint()
        {
            // Pointers have a minimum width of 8 and use lowercase hex (for compat with existing formatting)
            Assert.Equal("0x00000001", TdhFormatter.Format((Int32)0x1, FormatHint.Pointer));
            Assert.Equal("0xfedc1234", TdhFormatter.Format((UInt32)0xFEDC1234, FormatHint.Pointer));
            Assert.Equal("0x00000002", TdhFormatter.Format((Int64)0x2, FormatHint.Pointer));
            Assert.Equal("0xfedca1234", TdhFormatter.Format((UInt64)0xF_EDCA1234, FormatHint.Pointer));
            Assert.Equal("0x00000003", TdhFormatter.Format((IntPtr)0x3, FormatHint.Pointer));
            Assert.Equal("0xf1e2d3c4", TdhFormatter.Format((UIntPtr)0xF1E2D3C4, FormatHint.Pointer));
        }

        [Fact]
        public void Format_ErrorCodeHints()
        {
            // Error-code hints hex format for generic and Win32. No zero-padding.
            Assert.Equal("0xC", TdhFormatter.Format((UInt32)12, FormatHint.GenericError));
            Assert.Equal("0x46", TdhFormatter.Format((UInt32)70, FormatHint.Win32Error));

            // Error-code hints zero pad for NTSTATUS and HRESULT.
            Assert.Equal("0x00000102", TdhFormatter.Format((Int32)258, FormatHint.NtStatus));
            Assert.Equal("0xC0000005", TdhFormatter.Format((UInt32)3221225477, FormatHint.NtStatus));
            Assert.Equal("0x00000001", TdhFormatter.Format((Int32)1, FormatHint.HResult));
            Assert.Equal("0x80004005", TdhFormatter.Format((UInt32)0x80004005, FormatHint.HResult));

            // Error-code hints do not work for types other than 32-bit integers
            Assert.Null(TdhFormatter.Format((Byte)12, FormatHint.GenericError));
            Assert.Null(TdhFormatter.Format((Int16)70, FormatHint.Win32Error));
            Assert.Null(TdhFormatter.Format((Int64)258, FormatHint.NtStatus));
            Assert.Null(TdhFormatter.Format((UInt64)0x80004005, FormatHint.HResult));
        }

        [Fact]
        public void Format_PidTidHints()
        {
            // PID/TID hints render without digit separators.
            Assert.Equal("7448", TdhFormatter.Format(7448, FormatHint.Pid));
            Assert.Equal("14764", TdhFormatter.Format(14764, FormatHint.Tid));
        }

        [Fact]
        public void Format_PortHint()
        {
            // Port expects network byte order and renders as a decimal without digit separators.
            Assert.Equal("80", TdhFormatter.Format((Int16)0x5000, FormatHint.Port));
            Assert.Equal("443", TdhFormatter.Format((UInt16)0xBB01, FormatHint.Port));
            Assert.Equal("12345", TdhFormatter.Format((Int32)0x3930, FormatHint.Port));

            // Port hint does not work for other types
            Assert.Null(TdhFormatter.Format((UInt32)0x5000, FormatHint.Port));
            Assert.Null(TdhFormatter.Format((Int64)0xBB01, FormatHint.Port));
            Assert.Null(TdhFormatter.Format((UInt64)0x3930, FormatHint.Port));
        }

        [Fact]
        public void Format_IPv4Hint()
        {
            // IPv4 hint expects network byte order and renders as a dotted-quad
            Assert.Equal("10.100.2.200", TdhFormatter.Format(unchecked((Int32)0xC802640A), FormatHint.IPv4));
            Assert.Equal("127.0.0.1", TdhFormatter.Format((UInt32)0x0100007F, FormatHint.IPv4));
        }

        [Fact]
        public void Format_IPv6Hint() {
            // IPv6 hint expects a 16-byte array in network byte order and renders as a
            // colon-separated hex string.
            Assert.Equal("2001:abcd:def0:1234:5678:9abc:def0:1234", TdhFormatter.Format(new byte[] { 0x20, 0x01, 0xab, 0xcd, 0xde, 0xf0, 0x12, 0x34, 0x56, 0x78, 0x9a, 0xbc, 0xde, 0xf0, 0x12, 0x34 }, FormatHint.IPv6));
            Assert.Equal("::1", TdhFormatter.Format(new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01 }, FormatHint.IPv6));
            Assert.Equal("fe08::1234:5678", TdhFormatter.Format(new byte[] { 0xfe, 0x08, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x12, 0x34, 0x56, 0x78 }, FormatHint.IPv6));

            // IPv6 hint does not work for non-arrays and arrays of the wrong size
            Assert.Null(TdhFormatter.Format((UInt32)0x0102030A, FormatHint.IPv6));
            Assert.Null(TdhFormatter.Format(new byte[] { 0x01, 0x00, 0x00, 0x7F }, FormatHint.IPv6));
        }

        [Fact]
        public void Format_SocketAddressHint()
        {
            // SocketAddress hint with IPv4 address
            Assert.Equal("192.168.0.1:50000", TdhFormatter.Format(
                new byte[]
                {
                    0x02, 0x00,                                     // sin_family = AF_INET
                    0xC3, 0x50,                                     // sin_port = 50000 (network byte order)
                    0xC0, 0xA8, 0x00, 0x01,                         // sin_addr = 192.168.0.1 (network byte order)
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // sin_zero
                },
                FormatHint.SocketAddress));

            // SocketAddress hint with IPv6 address
            Assert.Equal("[ff03::114]:60000", TdhFormatter.Format(
                new byte[]
                {
                    0x17, 0x00,                                     // sin6_family = AF_INET6
                    0xEA, 0x60,                                     // sin6_port = 60000 (network byte order)
                    0x00, 0x00, 0x00, 0x00,                         // sin6_flowinfo
                    0xff, 0x03, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, // sin6_addr = ff03::114 (network byte order)
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x14,
                    0x00, 0x00, 0x00, 0x00,                         // sin6_scope_id
                },
                FormatHint.SocketAddress));

            // SocketAddress hint does not work for non-array values, wrong size arrays, and unknown address families.
            Assert.Null(TdhFormatter.Format((UInt32)0x0102030A, FormatHint.SocketAddress));
            Assert.Null(TdhFormatter.Format(new byte[] { 0x02, 0x00, 0xC3, 0x50 }, FormatHint.SocketAddress));
            Assert.Null(TdhFormatter.Format(
                new byte[] { 0x17, 0x00, 0xEA, 0x60, 0x00, 0x00, 0x00, 0x00, 0xff, 0x03 },
                FormatHint.SocketAddress));
            Assert.Null(TdhFormatter.Format(
                new byte[]
                {
                    0x01, 0x00,                                     // sin_family = AF_UNIX
                    0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00,
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                },
                FormatHint.SocketAddress));
        }
    }
}
