//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Net;
using System.Net.Sockets;

namespace Microsoft.Diagnostics.Utilities
{
    internal static class FormatUtils
    {
        public static string FormatIpV4Address(UInt32 address)
        {
            try
            {
                return new IPAddress(address).ToString();
            }
            catch
            {
                return null;
            }
        }

        public static string FormatIpV6Address(byte[] address)
        {
            try
            {
                // IPAddress ctor handles length check, so we don't need to.
                return new IPAddress(address).ToString();
            }
            catch
            {
                return null;
            }
        }

        public static string FormatSockaddr(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length < 16)
            {
                return null;
            }

            var family = (AddressFamily)(bytes[0] | (bytes[1] << 8));

            if (family == AddressFamily.InterNetwork)
            {
                // Both the port and address are in network byte order in the sockaddr struct.
                // IPEndPoint takes the port in HOST order.
                int port = NetworkToHostUInt16(bytes.Slice(start: 2, length: 2));
                // IPAddress ctor takes the address in NETWORK byte order.
                var ipv4 = new byte[4];
                bytes.Slice(start: 4, length: 4).CopyTo(ipv4);

                try
                {
                    return new IPEndPoint(new IPAddress(ipv4), port).ToString();
                }
                catch
                {
                    return null;
                }
            }
            else if (family == AddressFamily.InterNetworkV6 && bytes.Length >= 28)
            {
                // The port, address, and scope are in network byte order in the sockaddr struct.
                // IPEndPoint ctor takes the port in HOST order.
                int port = NetworkToHostUInt16(bytes.Slice(start: 2, length: 2));
                // IPAddress ctor takes the address in NETWORK byte order and the scope in HOST order.
                var ipv6 = new byte[16];
                bytes.Slice(start: 8, length: 16).CopyTo(ipv6);
                UInt32 scopeId = NetworkToHostUInt32(bytes.Slice(start: 24, length: 4));

                try
                {
                    return new IPEndPoint(new IPAddress(ipv6, scopeId), port).ToString();
                }
                catch
                {
                    return null;
                }

            }

            return null;
        }

        private static UInt16 NetworkToHostUInt16(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length != sizeof(UInt16))
            {
                throw new ArgumentException("A UInt16 requires exactly 2 bytes.", nameof(bytes));
            }

            return (UInt16)(
                  ((UInt16)bytes[0] << 8)
                | ((UInt16)bytes[1]));
        }

        private static UInt32 NetworkToHostUInt32(ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length != sizeof(UInt32))
            {
                throw new ArgumentException("A UInt32 requires exactly 4 bytes.", nameof(bytes));
            }

            return (UInt32)(
                  ((UInt32)bytes[0] << 24)
                | ((UInt32)bytes[1] << 16)
                | ((UInt32)bytes[2] << 8)
                | ((UInt32)bytes[3]));
        }
    }
}
