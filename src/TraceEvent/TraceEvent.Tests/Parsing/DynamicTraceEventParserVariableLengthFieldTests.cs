using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;

using System;
using System.Collections.Generic;
using System.Text;

using Xunit;

namespace TraceEventTests
{
    /// <summary>
    /// Covers variable-length field traversal, lookup bounds, and offset caching.
    /// </summary>
    public class DynamicTraceEventParserVariableLengthFieldTests
    {
        private const ushort CountedUnicodeByteCount = DynamicTraceEventData.COUNTED_SIZE;
        private const ushort CountedUnicodeElemCount = DynamicTraceEventData.COUNTED_SIZE | DynamicTraceEventData.ELEM_COUNT;
        private const ushort LengthPrefixedArray = DynamicTraceEventData.COUNTED_SIZE | DynamicTraceEventData.ELEM_COUNT;
        private const string LookupError = "<<<EXCEPTION_DURING_VALUE_LOOKUP ArgumentOutOfRangeException>>>";

        public static IEnumerable<object[]> CountedStringCases()
        {
            foreach (ushort size in CountedStringEncodings())
            {
                foreach (uint count in new uint[] { 0, 2, 16384, 32765, 32766, 32767, 32768, 33932, 65532 })
                {
                    int prefixBytes = (size & DynamicTraceEventData.BIT_32) != 0 ? 4 : 2;
                    int byteCount = checked((int)count * BytesPerCount(size));
                    if (prefixBytes + byteCount <= ushort.MaxValue)
                    {
                        yield return new object[] { size, count };
                    }
                }
            }
        }

        public static IEnumerable<object[]> InvalidCountedStringCases()
        {
            foreach (ushort size in CountedStringEncodings())
            {
                yield return new object[] { size, 64u };
                if ((size & DynamicTraceEventData.BIT_32) != 0)
                {
                    foreach (uint count in new uint[] { 0x10000, 0x10001, 0x7FFFFFFF, 0x80000000, 0x80000001, uint.MaxValue })
                    {
                        yield return new object[] { size, count };
                    }
                }
            }

            yield return new object[] { CountedUnicodeElemCount, 0x8000u };
        }

        [Theory]
        [MemberData(nameof(CountedStringCases))]
        public void CountedString_TraversalAndLookupAgree(ushort size, uint count)
        {
            bool widePrefix = (size & DynamicTraceEventData.BIT_32) != 0;
            bool isAnsi = (size & DynamicTraceEventData.IS_ANSI) != 0;
            int prefixBytes = widePrefix ? 4 : 2;
            int byteCount = checked((int)count * BytesPerCount(size));
            string expected = new string('x', isAnsi ? byteCount : byteCount / 2);
            byte[] payload = new byte[prefixBytes + byteCount];
            WriteCount(payload, 0, count, widePrefix);
            byte[] text = (isAnsi ? Encoding.ASCII : Encoding.Unicode).GetBytes(expected);
            Buffer.BlockCopy(text, 0, payload, prefixBytes, text.Length);
            var fetch = StringFetch(0, size);

            WithEvent(payload, LookupFetches(fetch, payload.Length), traceEvent =>
            {
                Assert.Equal(payload.Length, traceEvent.OffsetOfNextField(ref fetch, 0, payload.Length));
                Assert.Equal(expected, traceEvent.PayloadValue(0));
            });
        }

        [Theory]
        [MemberData(nameof(InvalidCountedStringCases))]
        public void CountedString_LengthExceedsPayload_TraversalAndPayloadValueReject(ushort size, uint count)
        {
            bool widePrefix = (size & DynamicTraceEventData.BIT_32) != 0;
            byte[] payload = new byte[(widePrefix ? 4 : 2) + 4];
            WriteCount(payload, 0, count, widePrefix);
            var fetch = StringFetch(0, size);

            WithEvent(payload, LookupFetches(fetch, payload.Length), traceEvent =>
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => traceEvent.OffsetOfNextField(ref fetch, 0, payload.Length));
                Assert.Equal(LookupError, traceEvent.PayloadValue(0));
            });
        }

        [Theory]
        [InlineData(false, 0)]
        [InlineData(false, 1)]
        [InlineData(false, 32767)]
        [InlineData(false, 32768)]
        [InlineData(false, 65000)]
        [InlineData(true, 0)]
        [InlineData(true, 1)]
        [InlineData(true, 32767)]
        [InlineData(true, 32768)]
        [InlineData(true, 65000)]
        public void LengthPrefixedArray_TraversalAndLookupAgree(bool widePrefix, int elementCount)
        {
            int prefixBytes = widePrefix ? 4 : 2;
            byte[] payload = new byte[prefixBytes + elementCount];
            WriteCount(payload, 0, (uint)elementCount, widePrefix);
            if (elementCount > 0)
            {
                payload[payload.Length - 1] = 0x7A;
            }
            var fetch = ByteArrayFetch(widePrefix);

            WithEvent(payload, LookupFetches(fetch, payload.Length), traceEvent =>
            {
                Assert.Equal(payload.Length, traceEvent.OffsetOfNextField(ref fetch, 0, payload.Length));
                byte[] value = Assert.IsType<byte[]>(traceEvent.PayloadValue(0));
                Assert.Equal(elementCount, value.Length);
                if (elementCount > 0)
                {
                    Assert.Equal((byte)0x7A, value[elementCount - 1]);
                }
            });
        }

        [Theory]
        [InlineData(false, 64u)]
        [InlineData(true, 64u)]
        [InlineData(true, 0x10000u)]
        [InlineData(true, 0x7FFFFFFFu)]
        [InlineData(true, 0x80000000u)]
        [InlineData(true, 0xFFFFFFFFu)]
        public void LengthPrefixedArray_CountExceedsPayload_TraversalAndPayloadValueReject(bool widePrefix, uint count)
        {
            byte[] payload = new byte[(widePrefix ? 4 : 2) + 4];
            WriteCount(payload, 0, count, widePrefix);
            var fetch = ByteArrayFetch(widePrefix);

            WithEvent(payload, LookupFetches(fetch, payload.Length), traceEvent =>
            {
                var traversalError = Assert.Throws<ArgumentOutOfRangeException>(() => traceEvent.OffsetOfNextField(ref fetch, 0, payload.Length));
                if (widePrefix && count > int.MaxValue)
                {
                    Assert.Equal("count", traversalError.ParamName);
                }
                Assert.Equal(LookupError, traceEvent.PayloadValue(0));
            });
        }

        [Theory]
        [InlineData(false, 0, 0)]
        [InlineData(false, 1, 0)]
        [InlineData(false, 1, 3)]
        [InlineData(true, 0, 0)]
        [InlineData(true, 1, 0)]
        [InlineData(true, 2, 0)]
        [InlineData(true, 3, 0)]
        [InlineData(true, 3, 3)]
        public void LengthPrefix_Truncated_RejectsWhenReadable(bool widePrefix, int remaining, int offset)
        {
            int length = offset + remaining;
            byte[] payload = new byte[Math.Max(1, length)];
            ushort size = (ushort)(CountedUnicodeByteCount | (widePrefix ? DynamicTraceEventData.BIT_32 : 0));
            var fetches = new[] { StringFetch((ushort)offset, size), ByteArrayFetch(widePrefix, (ushort)offset) };

            foreach (var field in fetches)
            {
                var fetch = field;
                WithEvent(payload, new[] { fetch }, traceEvent =>
                {
                    Assert.Throws<ArgumentOutOfRangeException>(() => traceEvent.OffsetOfNextField(ref fetch, offset, length));
                }, length);

                if (length > offset)
                {
                    var lookupFetch = field;
                    WithEvent(payload, LookupFetches(lookupFetch, length), traceEvent =>
                    {
                        Assert.Equal(LookupError, traceEvent.PayloadValue(0));
                    }, length);
                }
            }
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(-31470)]
        public void OffsetOfNextField_NegativeOffset_Throws(int offset)
        {
            byte[] payload = new byte[64];
            var fetch = StringFetch(0, CountedUnicodeByteCount);

            WithEvent(payload, new[] { fetch }, traceEvent =>
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => traceEvent.OffsetOfNextField(ref fetch, offset, payload.Length));
            });
        }

        [Fact]
        public void OffsetOfNextField_StructAtNegativeOffset_Throws()
        {
            // The nested null-terminated field otherwise scans from the invalid starting offset.
            byte[] payload = new byte[64];
            var fetch = StructFetch(StringFetch(0, DynamicTraceEventData.NULL_TERMINATED));

            WithEvent(payload, new[] { fetch }, traceEvent =>
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => traceEvent.OffsetOfNextField(ref fetch, -31470, payload.Length));
            });
        }

        [Theory]
        [InlineData(false, "")]
        [InlineData(false, "abc")]
        [InlineData(true, "abc")]
        public void FixedString_LookupPreservesEncoding(bool isAnsi, string expected)
        {
            ushort size = (ushort)(expected.Length + (isAnsi ? 0x8000 : 0));
            byte[] text = (isAnsi ? Encoding.ASCII : Encoding.Unicode).GetBytes(expected);
            byte[] payload = text.Length == 0 ? new byte[1] : text;
            var fetch = StringFetch(0, size);
            WithEvent(payload, LookupFetches(fetch, payload.Length), traceEvent =>
            {
                Assert.Equal(expected, traceEvent.PayloadValue(0));
            });
        }

        [Theory]
        [InlineData(typeof(byte), 1)]
        [InlineData(typeof(char), 1)]
        [InlineData(typeof(char), 2)]
        [InlineData(typeof(int), 4)]
        public void FixedCountArray_ExactlyFillsPayload_Decodes(Type elementType, ushort elementSize)
        {
            const ushort offset = 3;
            var element = new DynamicTraceEventData.PayloadFetch(0, elementSize, elementType);
            var fetch = DynamicTraceEventData.PayloadFetch.FixedCountArrayPayloadFetch(offset, element, 3);
            byte[] payload = new byte[offset + 3 * elementSize];
            for (int i = 0; i < 3; i++)
            {
                payload[offset + i * elementSize] = (byte)('a' + i);
            }

            WithEvent(payload, LookupFetches(fetch, payload.Length), traceEvent =>
            {
                object value = traceEvent.PayloadValue(0);
                if (elementType == typeof(char))
                {
                    Assert.Equal("abc", value);
                }
                else if (elementType == typeof(byte))
                {
                    Assert.Equal(new byte[] { 97, 98, 99 }, Assert.IsType<byte[]>(value));
                }
                else
                {
                    Assert.Equal(new[] { 97, 98, 99 }, Assert.IsType<int[]>(value));
                }
                Assert.Equal(value, traceEvent.PayloadValue(0));
            });
        }

        [Theory]
        [InlineData(typeof(byte), 1, 0)]
        [InlineData(typeof(byte), 1, 3)]
        [InlineData(typeof(char), 1, 0)]
        [InlineData(typeof(char), 1, 3)]
        [InlineData(typeof(char), 2, 0)]
        [InlineData(typeof(char), 2, 3)]
        [InlineData(typeof(int), 4, 3)]
        public void FixedCountArray_Truncated_ReturnsError(Type elementType, ushort elementSize, ushort offset)
        {
            var element = new DynamicTraceEventData.PayloadFetch(0, elementSize, elementType);
            var fetch = DynamicTraceEventData.PayloadFetch.FixedCountArrayPayloadFetch(offset, element, 3);
            byte[] payload = new byte[offset + 3 * elementSize];

            WithEvent(payload, LookupFetches(fetch, payload.Length), traceEvent =>
            {
                Assert.Equal(LookupError, traceEvent.PayloadValue(0));
            }, payload.Length - 1);
        }

        [Fact]
        public void FixedCountArray_TruncatedNullTerminatedString_RejectsBeforeScanning()
        {
            var element = StringFetch(0, DynamicTraceEventData.NULL_TERMINATED);
            var fetch = DynamicTraceEventData.PayloadFetch.FixedCountArrayPayloadFetch(0, element, 2);
            // Two empty Unicode strings still need four bytes for their terminators.
            byte[] payload = new byte[3];
            WithEvent(payload, LookupFetches(fetch, payload.Length), traceEvent =>
            {
                var traversalError = Assert.Throws<ArgumentOutOfRangeException>(() => traceEvent.OffsetOfNextField(ref fetch, 0, payload.Length));
                Assert.Equal("arrayCount", traversalError.ParamName);
                Assert.Equal(LookupError, traceEvent.PayloadValue(0));
            });
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void FixedCountArray_VariableElements_ChecksMinimumSize(bool widePrefix, bool truncated)
        {
            ushort size = (ushort)(CountedUnicodeByteCount | (widePrefix ? DynamicTraceEventData.BIT_32 : 0));
            var fetch = DynamicTraceEventData.PayloadFetch.FixedCountArrayPayloadFetch(0, StringFetch(0, size), 2);
            byte[] payload = new byte[2 * (widePrefix ? 4 : 2)];
            int length = payload.Length - (truncated ? 1 : 0);
            WithEvent(payload, LookupFetches(fetch, length), traceEvent =>
            {
                if (truncated)
                {
                    Assert.Throws<ArgumentOutOfRangeException>(() => traceEvent.OffsetOfNextField(ref fetch, 0, length));
                    Assert.Equal(LookupError, traceEvent.PayloadValue(0));
                }
                else
                {
                    Assert.Equal(length, traceEvent.OffsetOfNextField(ref fetch, 0, length));
                    Assert.Equal(new[] { "", "" }, Assert.IsType<string[]>(traceEvent.PayloadValue(0)));
                }
            }, length);
        }

        [Fact]
        public void PayloadValue_EventWithOversizedCountedString_DecodesAllFollowingFields()
        {
            string first = new string('a', 65);
            string big = new string('b', 16966);
            string last = new string('d', 159);
            byte[] payload = BuildCountedUnicodeStringPayload(first, big, "c", last);
            Assert.Equal(34390, payload.Length);
            var fetches = StringFetches(4, CountedUnicodeByteCount);

            WithEvent(payload, fetches, traceEvent =>
            {
                object[] values = { traceEvent.PayloadValue(0), traceEvent.PayloadValue(1), traceEvent.PayloadValue(2), traceEvent.PayloadValue(3) };
                Assert.Equal(new object[] { first, big, "c", last }, values);
                Assert.Equal(34066, traceEvent.OffsetOfNextField(ref fetches[1], 132, payload.Length));
            });
        }

        [Fact]
        public void PayloadValue_EventWithOversizedCountedString_IsStableAcrossRepeatedWalks()
        {
            string first = new string('a', 65);
            string big = new string('b', 16966);
            byte[] payload = BuildCountedUnicodeStringPayload(first, big, "c", "d");

            WithEvent(payload, StringFetches(4, CountedUnicodeByteCount), traceEvent =>
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    object[] values = { traceEvent.PayloadValue(0), traceEvent.PayloadValue(1), traceEvent.PayloadValue(2), traceEvent.PayloadValue(3) };
                    Assert.Equal(new object[] { first, big, "c", "d" }, values);
                    Assert.Equal("d", traceEvent.PayloadValue(3));
                }
            });
        }

        [Fact]
        public void PayloadValue_MalformedCountedString_FollowingLookupsRemainErrors()
        {
            byte[] payload = new byte[8];
            WriteCount(payload, 0, 64, false);
            WithEvent(payload, StringFetches(3, CountedUnicodeByteCount), traceEvent =>
            {
                for (int pass = 0; pass < 2; pass++)
                {
                    Assert.Equal(LookupError, traceEvent.PayloadValue(1));
                    Assert.Equal(LookupError, traceEvent.PayloadValue(2));
                }
            });
        }

        private static IEnumerable<ushort> CountedStringEncodings()
        {
            foreach (int flags in new[] { 0, 1, 2, 3, 8, 9, 10, 11 })
            {
                yield return (ushort)(CountedUnicodeByteCount | flags);
            }
        }

        private static int BytesPerCount(ushort size)
        {
            return (size & DynamicTraceEventData.IS_ANSI) == 0 && (size & DynamicTraceEventData.ELEM_COUNT) != 0 ? 2 : 1;
        }

        private static DynamicTraceEventData.PayloadFetch StringFetch(ushort offset, ushort size)
        {
            return new DynamicTraceEventData.PayloadFetch(offset, size, typeof(string));
        }

        private static DynamicTraceEventData.PayloadFetch ByteArrayFetch(bool widePrefix, ushort offset = 0)
        {
            var element = new DynamicTraceEventData.PayloadFetch(0, 1, typeof(byte));
            ushort size = (ushort)(LengthPrefixedArray | (widePrefix ? DynamicTraceEventData.BIT_32 : 0));
            return DynamicTraceEventData.PayloadFetch.ArrayPayloadFetch(offset, element, size);
        }

        private static DynamicTraceEventData.PayloadFetch[] LookupFetches(DynamicTraceEventData.PayloadFetch target, int payloadLength)
        {
            Assert.InRange(payloadLength, 1, ushort.MaxValue);
            // A later fixed offset lets Debug validation skip traversal of the target before its lookup is tested.
            var sentinel = new DynamicTraceEventData.PayloadFetch((ushort)(payloadLength - 1), 1, typeof(byte));
            return new[] { target, sentinel };
        }

        private static DynamicTraceEventData.PayloadFetch StructFetch(DynamicTraceEventData.PayloadFetch field)
        {
            var classInfo = new DynamicTraceEventData.PayloadFetchClassInfo()
            {
                FieldFetches = new[] { field },
                FieldNames = new[] { "Inner" }
            };

            return DynamicTraceEventData.PayloadFetch.StructPayloadFetch(0, classInfo);
        }

        private static DynamicTraceEventData.PayloadFetch[] StringFetches(int count, ushort size)
        {
            var fetches = new DynamicTraceEventData.PayloadFetch[count];
            fetches[0] = StringFetch(0, size);
            for (int i = 1; i < count; i++)
            {
                fetches[i] = StringFetch(ushort.MaxValue, size);
            }

            return fetches;
        }

        private static byte[] BuildCountedUnicodeStringPayload(params string[] values)
        {
            int total = 0;
            foreach (string value in values)
            {
                total += 2 + (value.Length * 2);
            }

            byte[] payload = new byte[total];
            int offset = 0;
            foreach (string value in values)
            {
                byte[] bytes = Encoding.Unicode.GetBytes(value);
                WriteCount(payload, offset, checked((ushort)bytes.Length), false);
                offset += 2;
                Buffer.BlockCopy(bytes, 0, payload, offset, bytes.Length);
                offset += bytes.Length;
            }

            return payload;
        }

        private static void WriteCount(byte[] buffer, int offset, uint value, bool widePrefix)
        {
            int prefixBytes = widePrefix ? 4 : 2;
            Assert.True(widePrefix || value <= ushort.MaxValue);
            for (int i = 0; i < prefixBytes; i++)
            {
                buffer[offset + i] = (byte)(value >> (8 * i));
            }
        }

        private static unsafe void WithEvent(byte[] payload, DynamicTraceEventData.PayloadFetch[] fetches, Action<DynamicTraceEventData> action, int? eventDataLength = null)
        {
            int length = eventDataLength ?? payload.Length;
            Assert.InRange(payload.Length, 1, ushort.MaxValue);
            Assert.InRange(length, 0, payload.Length);
            // Padding keeps the tested regression reads inside allocated storage.
            const int padding = 32768;
            byte[] storage = new byte[padding + payload.Length + padding];
            Buffer.BlockCopy(payload, 0, storage, padding, payload.Length);

            fixed (byte* storageBytes = storage)
            {
                TraceEventNativeMethods.EVENT_RECORD eventRecord = new TraceEventNativeMethods.EVENT_RECORD();
                eventRecord.EventHeader.Id = 1;
                eventRecord.UserDataLength = checked((ushort)length);
                eventRecord.UserData = (IntPtr)(storageBytes + padding);

                var names = new string[fetches.Length];
                for (int i = 0; i < fetches.Length; i++)
                {
                    names[i] = "Field" + i;
                }

                var traceEvent = new DynamicTraceEventData(null, 1, 0, "Task", Guid.Empty, 0, "Opcode", Guid.Empty, "Provider");
                traceEvent.payloadFetches = fetches;
                traceEvent.payloadNames = names;
                traceEvent.eventRecord = &eventRecord;
                traceEvent.userData = eventRecord.UserData;

                action(traceEvent);
            }
        }
    }
}
