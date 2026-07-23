//     Copyright (c) Microsoft Corporation.  All rights reserved.
using Microsoft.Diagnostics.Utilities;

using System;
using System.Globalization;
using System.Net;
using System.Text;

namespace Microsoft.Diagnostics.Tracing.Parsers
{
    /// <summary>
    /// Formats values according to a formatting hint (see <see
    /// cref="FormatHint"/>). The hints should be computed from a field's
    /// TDH InType/OutType.
    /// </summary>
    public static class TdhFormatter
    {
        // For compatibility with existing formatting, pointers use lower-case hex and always
        // have a minimum width of 8, even when dealing with 64-bit pointers.
        private const string PointerFormatString = "x8";

        /// <summary>
        /// A display-formatting hint for a field.
        /// </summary>
        public enum FormatHint : byte
        {
            /// <summary>
            /// Do not perform any special formatting.
            /// </summary>
            None = 0,
            /// <summary>
            /// Indicates that the value should be rendered as an unsigned
            /// hexadecimal value (e.g. "0x8001").
            /// </summary>
            /// <remarks>
            /// The following values are supported: <see cref="Byte"/>, <see
            /// cref="SByte"/>, <see cref="Int16"/>, <see cref="UInt16"/>,
            /// <see cref="Int32"/>, <see cref="UInt32"/>, <see
            /// cref="Int64"/>, <see cref="UInt64"/>, <see cref="IntPtr"/>,
            /// <see cref="UIntPtr"/>.
            /// </remarks>
            Hex,
            /// <summary>
            /// Indicates that the value should be rendered as a pointer. Pointers are rendered in
            /// hex, with a minimum width of 8.
            /// </summary>
            /// <remarks>
            /// The following values are supported:
            /// <see cref="Int32"/>, <see cref="UInt32"/>, <see
            /// cref="Int64"/>, <see cref="UInt64"/>, <see cref="IntPtr"/>,
            /// <see cref="UIntPtr"/>.
            /// </remarks>
            Pointer,
            /// <summary>
            /// Indicates that the value should be rendered as a generic
            /// error code. Presently, that is hex formatting, but that is
            /// subject to change in the future.
            /// </summary>
            /// <remarks>
            /// The following values are supported: <see cref="Int32"/>,
            /// <see cref="UInt32"/>.
            /// </remarks>
            GenericError,
            /// <summary>
            /// Indicates that the value should be rendered as a Win32 error
            /// code. Presently, that is hex formatting, but that is subject
            /// to change in the future.
            /// </summary>
            /// <remarks>
            /// The following values are supported: <see cref="Int32"/>,
            /// <see cref="UInt32"/>.
            /// </remarks>
            Win32Error,
            /// <summary>
            /// Indicates that the value should be rendered as a NTSTATUS
            /// error code. Presently, that is hex formatting, but that is
            /// subject to change in the future.
            /// </summary>
            /// <remarks>
            /// The following values are supported: <see cref="Int32"/>,
            /// <see cref="UInt32"/>.
            /// </remarks>
            NtStatus,
            /// <summary>
            /// Indicates that the value should be rendered as a HRESULT
            /// error code. Presently, that is hex formatting, but that is
            /// subject to change in the future.
            /// </summary>
            /// <remarks>
            /// The following values are supported: <see cref="Int32"/>,
            /// <see cref="UInt32"/>.
            /// </remarks>
            HResult,
            /// <summary>
            /// Indicates that the value should be rendered as a process id.
            /// Presently, that is a plain decimal with no digit separators,
            /// but that is subject to change in the future.
            /// </summary>
            /// <remarks>
            /// The following values are supported: <see cref="Byte"/>, <see
            /// cref="SByte"/>, <see cref="Int16"/>, <see cref="UInt16"/>,
            /// <see cref="Int32"/>, <see cref="UInt32"/>, <see
            /// cref="Int64"/>, <see cref="UInt64"/>, <see cref="IntPtr"/>,
            /// <see cref="UIntPtr"/>.
            /// </remarks>
            Pid,
            /// <summary>
            /// Indicates that the value should be rendered as a thread id.
            /// Presently, that is a plain decimal with no digit separators,
            /// but that is subject to change in the future.
            /// </summary>
            /// <remarks>
            /// The following values are supported: <see cref="Byte"/>, <see
            /// cref="SByte"/>, <see cref="Int16"/>, <see cref="UInt16"/>,
            /// <see cref="Int32"/>, <see cref="UInt32"/>, <see
            /// cref="Int64"/>, <see cref="UInt64"/>, <see cref="IntPtr"/>,
            /// <see cref="UIntPtr"/>.
            /// </remarks>
            Tid,
            /// <summary>
            /// Indicates that the value should be rendered as an IP port.
            /// The numeric value MUST be in network byte order.
            /// </summary>
            /// <remarks>
            /// The following values are supported: <see cref="Int16"/>,
            /// <see cref="UInt16"/>, <see cref="Int32"/>.
            /// </remarks>
            Port,
            /// <summary>
            /// Indicates that the value should be rendered as a dotted quad
            /// IPv4 address. The numeric value MUST be in network byte
            /// order.
            /// </summary>
            /// <remarks>
            /// The following values are supported: <see cref="Int32"/>,
            /// <see cref="UInt32"/>.
            /// </remarks>
            IPv4,
            /// <summary>
            /// Indicates that the value is a serialized IPv6 address and
            /// should be rendered as a colon-separated IPv6 address.
            /// </summary>
            /// <remarks>
            /// The following values are supported: a 16-byte <see
            /// cref="Byte"/> array.
            /// </remarks>
            IPv6,
            /// <summary>
            /// Indicates that the value is a serialized SOCKADDR_IN, SOCKADDR_IN6, or
            /// SOCKADDR_STORAGE struct and should be rendered as a human-readable IP address
            /// and port. Currently, only AF_INET and AF_INET6 families are supported.
            /// </summary>
            /// <remarks>
            /// The following values are supported: a <see cref="Byte"/>
            /// array containing a serialized SOCKADDR_IN, SOCKADDR_IN6, or
            /// SOCKADDR_STORAGE struct.
            /// </remarks>
            SocketAddress,
        }

        /// <summary>
        /// Applies the provided <paramref name="formatHint"/> to <paramref
        /// name="value"/>. Returns a formatted string, or null if the hint
        /// does not apply to the current value.
        /// </summary>
        /// <param name="value">The value to format. May be a scalar, a
        /// one-dimensional array, or a byte[].</param>
        /// <param name="formatHint">The hint for the field itself (for
        /// arrays this is typically <see cref="FormatHint.None"/>).</param>
        /// <param name="elementFormatHint">For arrays, the hint that lives
        /// on the array element. For non-array values, pass <see
        /// langword="null"/>.</param>
        /// <param name="formatProvider">The format provider to use, or
        /// null.</param>
        public static string Format(object value, FormatHint formatHint, FormatHint? elementFormatHint = null, IFormatProvider formatProvider = null)
        {
            if (formatHint == FormatHint.None && elementFormatHint.GetValueOrDefault(FormatHint.None) == FormatHint.None)
            {
                return null;
            }

            if (value == null)
            {
                return string.Empty;
            }

            // Format one-dimensional arrays element-by-element using the
            // element's hint. byte[] arrays are excluded as they have
            // special handling elsewhere.
            if (formatHint == FormatHint.None && value is Array array && array.Rank == 1 && !(value is byte[]))
            {
                // Estimate about 8 characters per element, plus 2 for the brackets. Don't go below
                // StringBuilder's default capacity of 16.
                var sb = new StringBuilder(Math.Max(16, 8 * array.Length + 2));
                sb.Append('[');
                bool first = true;
                foreach (object elem in array)
                {
                    if (!first)
                    {
                        sb.Append(',');
                    }
                    first = false;

                    string formatted = FormatScalar(elem, elementFormatHint ?? formatHint, formatProvider);
                    if (formatted != null)
                    {
                        sb.Append(formatted);
                    }
                    else
                    {
                        sb.Append(elem?.ToString() ?? string.Empty);
                    }
                }
                sb.Append(']');
                return sb.ToString();
            }

            return FormatScalar(value, elementFormatHint ?? formatHint, formatProvider);
        }

        /// <summary>
        /// Formats a single scalar <paramref name="value"/> according to
        /// the given display hint. Returns null if the hint does not apply
        /// to the value.
        /// </summary>
        private static string FormatScalar(object value, FormatHint format, IFormatProvider formatProvider)
        {
            if (value == null)
            {
                return null;
            }

            switch (format)
            {
                case FormatHint.Hex:
                    return ToHexString(value, formatProvider);
                case FormatHint.Pointer:
                    return ToPointerString(value, formatProvider);
                case FormatHint.GenericError:
                    return ToErrorCodeString(value, zeroPad: false, formatProvider);
                case FormatHint.Win32Error:
                    return ToErrorCodeString(value, zeroPad: false, formatProvider);
                case FormatHint.NtStatus:
                    return ToErrorCodeString(value, zeroPad: true, formatProvider);
                case FormatHint.HResult:
                    return ToErrorCodeString(value, zeroPad: true, formatProvider);
                case FormatHint.Pid:
                case FormatHint.Tid:
                    return ToPlainDecimalString(value, formatProvider);
                case FormatHint.Port:
                    return ToPortString(value, formatProvider);
                case FormatHint.IPv4:
                    return ToIPv4String(value, formatProvider);
                case FormatHint.IPv6:
                    return ToIPv6String(value, formatProvider);
                case FormatHint.SocketAddress:
                    return ToSocketAddressString(value, formatProvider);
                default:
                    return null;
            }
        }

        /// <summary>
        /// Renders an integer value as unsigned hexadecimal.
        /// </summary>
        private static string ToHexString(object value, IFormatProvider formatProvider)
        {
            string hex = value switch
            {
                Byte b => b.ToString("X2", formatProvider),
                SByte sb => sb.ToString("X2", formatProvider),
                Int16 i16 => i16.ToString("X4", formatProvider),
                UInt16 ui16 => ui16.ToString("X4", formatProvider),
                Int32 i32 => i32.ToString("X8", formatProvider),
                UInt32 ui32 => ui32.ToString("X8", formatProvider),
                Int64 i64 => i64.ToString("X16", formatProvider),
                UInt64 ui64 => ui64.ToString("X16", formatProvider),
                IntPtr ptr => ptr.ToInt64().ToString(PointerFormatString, formatProvider),
                UIntPtr ptr => ptr.ToUInt64().ToString(PointerFormatString, formatProvider),
                _ => null
            };

            if (hex is null)
            {
                return null;
            }

            return "0x" + hex;
        }

        private static string ToPointerString(object value, IFormatProvider formatProvider)
        {
            string hex = value switch
            {
                Int32 i32 => i32.ToString(PointerFormatString, formatProvider),
                UInt32 ui32 => ui32.ToString(PointerFormatString, formatProvider),
                Int64 i64 => i64.ToString(PointerFormatString, formatProvider),
                UInt64 ui64 => ui64.ToString(PointerFormatString, formatProvider),
                IntPtr ptr => ptr.ToInt64().ToString(PointerFormatString, formatProvider),
                UIntPtr ptr => ptr.ToUInt64().ToString(PointerFormatString, formatProvider),
                _ => null
            };

            if (hex is null)
            {
                return null;
            }

            return "0x" + hex;
        }

        /// <summary>
        /// Renders a 32-bit integer error code string. For now, that is a
        /// hex formatted string.
        /// </summary>
        /// <remarks>
        /// In the future, an attempt could be made to look up the message
        /// associated with Win32, NTSTATUS, or HRESULT error codes.
        /// </remarks>
        /// <param name="zeroPad">Whether to zero-pad the hexadecimal
        /// representation. NSTATUS and HRESULT error codes are typically
        /// zero-padded, which Win32 and generic errors are not.</param>
        private static string ToErrorCodeString(object value, bool zeroPad, IFormatProvider formatProvider)
        {
            string formatString = zeroPad ? "X8" : "X";

            string hex = value switch
            {
                Int32 i32 => i32.ToString(formatString, formatProvider),
                UInt32 ui32 => ui32.ToString(formatString, formatProvider),
                _ => null
            };

            if (hex is null)
            {
                return null;
            }

            return "0x" + hex;
        }

        /// <summary>
        /// Renders an integer value as plain decimal (no digit separators)
        /// </summary>
        private static string ToPlainDecimalString(object value, IFormatProvider formatProvider)
        {
            switch (value)
            {
                case Byte b: return b.ToString(CultureInfo.InvariantCulture);
                case SByte sb: return sb.ToString(CultureInfo.InvariantCulture);
                case Int16 i16: return i16.ToString(CultureInfo.InvariantCulture);
                case UInt16 ui16: return ui16.ToString(CultureInfo.InvariantCulture);
                case Int32 i32: return i32.ToString(CultureInfo.InvariantCulture);
                case UInt32 ui32: return ui32.ToString(CultureInfo.InvariantCulture);
                case Int64 i64: return i64.ToString(CultureInfo.InvariantCulture);
                case UInt64 ui64: return ui64.ToString(CultureInfo.InvariantCulture);
                case IntPtr ptr: return ptr.ToInt64().ToString(CultureInfo.InvariantCulture);
                case UIntPtr ptr: return ptr.ToUInt64().ToString(CultureInfo.InvariantCulture);
                default: return null;
            }
        }

        /// <summary>
        /// Renders an 16-bit integer as an IP port number (no digit
        /// seperators). The value is assumed to be in network byte order.
        /// </summary>
        private static string ToPortString(object value, IFormatProvider formatProvider)
        {
            // While ports are UInt16 values, NetworkToHostOrder takes an Int16.
            Int16? raw = value switch
            {
                Int16 i16 => i16,
                UInt16 ui16 => unchecked((Int16)ui16),
                // TraceEvent.GetInt16At reads Int16 values as Int32, so allow those as well.
                Int32 i32 => TryInt32ToInt16(i32),
                _ => null,
            };

            if (raw is null)
            {
                return null;
            }

            var port = unchecked((UInt16)IPAddress.NetworkToHostOrder(raw.Value));
            return port.ToString(CultureInfo.InvariantCulture);

            static Int16? TryInt32ToInt16(int v)
            {
                if (v < Int16.MinValue || v > Int16.MaxValue)
                {
                    return null;
                }
                return unchecked((Int16)v);
            }
        }

        /// <summary>
        /// Renders a 32-bit integer as a dotted-quad IPv4 address. The
        /// value is assumed to be in network byte order.
        /// </summary>
        private static string ToIPv4String(object value, IFormatProvider formatProvider)
        {
            UInt32? v = value switch
            {
                Int32 i32 => unchecked((UInt32)i32),
                UInt32 ui32 => ui32,
                _ => null,
            };

            if (v is null)
            {
                return null;
            }

            return FormatUtils.FormatIpV4Address(v.Value);
        }

        /// <summary>
        /// Renders a 16-byte binary blob as an IPv6 address. The blob must
        /// be 16 bytes long and in network byte order.
        /// </summary>
        private static string ToIPv6String(object value, IFormatProvider formatProvider)
        {
            if (value is byte[] bytes && bytes.Length == 16)
            {
                return FormatUtils.FormatIpV6Address(bytes);
            }

            return null;
        }

        /// <summary>
        /// Renders a SOCKADDR_STORAGE binary blob for AF_INET/AF_INET6
        /// families as "ipv4Address:port" or "[ipv6Address]:port".
        /// </summary>
        private static string ToSocketAddressString(object value, IFormatProvider formatProvider)
        {
            if (value is byte[] b && b.Length >= 16)
            {
                string formattedSockaddr = FormatUtils.FormatSockaddr(b);
                if (formattedSockaddr is not null)
                {
                    return formattedSockaddr;
                }
            }

            return null;
        }
    }
}
