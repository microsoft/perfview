//     Copyright (c) Microsoft Corporation.  All rights reserved.

using System;
using System.Diagnostics;
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

            Debug.Assert(BitConverter.IsLittleEndian, "Bit twiddling below assumes a little-endian host machine");

            if (family == AddressFamily.InterNetwork)
            {
                // Both the address and port are in network byte order in the sockaddr struct.
                // IPEndPoint ctor wants the port in host order...
                int port = (UInt16)(bytes[2] << 8 | bytes[3]);
                // ... and the address in network byte order.
                long addr = (UInt32)(bytes[4] | (bytes[5] << 8) | (bytes[6] << 16) | (bytes[7] << 24));

                try
                {
                    return new IPEndPoint(addr, port).ToString();
                }
                catch
                {
                    return null;
                }
            }
            else if (family == AddressFamily.InterNetworkV6 && bytes.Length >= 28)
            {
                // Both the address and port are in network byte order in the sockaddr struct.
                // IPEndPoint ctor wants the port in host order...
                int port = (UInt16)(bytes[2] << 8 | bytes[3]);
                // ... and the address in network byte order and a byte[16].
                var ipv6 = new byte[16];
                bytes.Slice(start: 8, length: 16).CopyTo(ipv6);
                try
                {
                    return new IPEndPoint(new IPAddress(ipv6), port).ToString();
                }
                catch
                {
                    return null;
                }

            }

            return null;
        }
    }
}
