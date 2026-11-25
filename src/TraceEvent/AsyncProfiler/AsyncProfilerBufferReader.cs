// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler
{
    /// <summary>
    /// Reads little-endian fixed-width values and LEB128/zigzag compressed integers.
    /// Fixed-width reads require enough input bytes. Compressed reads stop at their limit and may advance
    /// the index even when they return false.
    /// </summary>
    internal static class AsyncProfilerBufferReader
    {
        public static ushort ReadUInt16LE(byte[] buffer, ref int index)
        {
            ushort value = (ushort)(buffer[index] | (buffer[index + 1] << 8));
            index += 2;
            return value;
        }

        public static uint ReadUInt32LE(byte[] buffer, ref int index)
        {
            uint value = (uint)(
                buffer[index] | (buffer[index + 1] << 8) |
                (buffer[index + 2] << 16) | (buffer[index + 3] << 24));
            index += 4;
            return value;
        }

        public static ulong ReadUInt64LE(byte[] buffer, ref int index)
        {
            ulong lo = ReadUInt32LE(buffer, ref index);
            ulong hi = ReadUInt32LE(buffer, ref index);
            return lo | (hi << 32);
        }

        /// <summary>
        /// Reads an unsigned LEB128 (7-bit, least-significant group first) value without advancing beyond
        /// <paramref name="limit"/>.
        /// </summary>
        public static bool TryReadCompressedUInt64(byte[] buffer, ref int index, int limit, out ulong value)
        {
            value = 0;
            for (int byteIndex = 0; byteIndex < 10; byteIndex++)
            {
                if (index >= limit)
                {
                    return false;
                }

                byte b = buffer[index++];
                // Only bit 0 may be set in the tenth byte of a 64-bit varint.
                if (byteIndex == 9 && (b & 0xFE) != 0)
                {
                    return false;
                }

                value |= (ulong)(b & 0x7F) << (byteIndex * 7);
                if ((b & 0x80) == 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Reads an unsigned LEB128 value into a 32-bit result.</summary>
        public static bool TryReadCompressedUInt32(byte[] buffer, ref int index, int limit, out uint value)
        {
            if (TryReadCompressedUInt64(buffer, ref index, limit, out ulong wide) && wide <= uint.MaxValue)
            {
                value = (uint)wide;
                return true;
            }

            value = 0;
            return false;
        }

        /// <summary>Reads a zigzag-encoded signed LEB128 value (64-bit).</summary>
        public static bool TryReadCompressedInt64(byte[] buffer, ref int index, int limit, out long value)
        {
            if (TryReadCompressedUInt64(buffer, ref index, limit, out ulong encoded))
            {
                value = (long)(encoded >> 1) ^ -(long)(encoded & 1);
                return true;
            }

            value = 0;
            return false;
        }

        /// <summary>Reads a zigzag-encoded signed LEB128 value (32-bit).</summary>
        public static bool TryReadCompressedInt32(byte[] buffer, ref int index, int limit, out int value)
        {
            if (TryReadCompressedUInt32(buffer, ref index, limit, out uint encoded))
            {
                value = (int)(encoded >> 1) ^ -(int)(encoded & 1);
                return true;
            }

            value = 0;
            return false;
        }
    }
}
