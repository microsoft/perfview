using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.EventPipe;
using System;
using System.IO;
using Xunit;

namespace TraceEventTests
{
    public unsafe class EventPipeV3StackParsingTests
    {
        private const int V3HeaderSize = 56;

        [Fact]
        public void ReadFromFormatV3AcceptsStackContainedInEvent()
        {
            byte[] payload = new byte[] { 1, 2, 3, 4 };
            byte[] stackBytes = new byte[] { 10, 11, 12, 13, 14, 15, 16, 17 };
            byte[] eventBytes = CreateV3Event(payload, stackBytes.Length, stackBytes);

            fixed (byte* eventBytesPtr = eventBytes)
            {
                SpanReader reader = CreateReader(eventBytesPtr, eventBytes.Length);
                EventPipeEventHeader header = default;

                EventPipeEventHeader.ReadFromFormatV3(ref reader, ref header);

                Assert.Equal(V3HeaderSize, reader.StreamOffset);
                Assert.Equal(V3HeaderSize, header.HeaderSize);
                Assert.Equal(payload.Length, header.PayloadSize);
                Assert.Equal(payload.Length + sizeof(int) + stackBytes.Length, header.TotalNonHeaderSize);
                Assert.Equal(stackBytes.Length, header.StackBytesSize);
                Assert.NotEqual(IntPtr.Zero, header.StackBytes);
                Assert.Equal(stackBytes, new ReadOnlySpan<byte>((void*)header.StackBytes, header.StackBytesSize).ToArray());
            }
        }

        [Fact]
        public void ReadFromFormatV3RejectsStackBytesExtendingPastEvent()
        {
            byte[] payload = new byte[] { 1, 2, 3, 4 };
            byte[] extraBytesAfterEvent = new byte[] { 10, 11, 12, 13, 14, 15, 16, 17 };
            byte[] eventBytes = CreateV3Event(payload, stackBytesSize: extraBytesAfterEvent.Length, stackBytes: null, extraBytesAfterEvent: extraBytesAfterEvent);
            EventPipeEventHeader header = CreateHeaderWithStackSentinel();

            fixed (byte* eventBytesPtr = eventBytes)
            {
                SpanReader reader = CreateReader(eventBytesPtr, eventBytes.Length);

                FormatException ex = ReadV3ExpectingFormatException(ref reader, ref header);

                Assert.Contains("stack size", ex.Message);
                Assert.Equal(V3HeaderSize, reader.StreamOffset);
                AssertStackSentinelUnchanged(header);
            }
        }

        [Fact]
        public void ReadFromFormatV3RejectsNegativePayloadSize()
        {
            byte[] eventBytes = CreateV3Event(payloadSize: -1, eventSize: V3HeaderSize + sizeof(int) - sizeof(int));
            EventPipeEventHeader header = CreateHeaderWithStackSentinel();

            fixed (byte* eventBytesPtr = eventBytes)
            {
                SpanReader reader = CreateReader(eventBytesPtr, eventBytes.Length);

                FormatException ex = ReadV3ExpectingFormatException(ref reader, ref header);

                Assert.Contains("payload size", ex.Message);
                Assert.Equal(V3HeaderSize, reader.StreamOffset);
                AssertStackSentinelUnchanged(header);
            }
        }

        [Fact]
        public void ReadFromFormatV3RejectsNegativeStackSize()
        {
            byte[] eventBytes = CreateV3Event(Array.Empty<byte>(), stackBytesSize: -1, stackBytes: null);
            EventPipeEventHeader header = CreateHeaderWithStackSentinel();

            fixed (byte* eventBytesPtr = eventBytes)
            {
                SpanReader reader = CreateReader(eventBytesPtr, eventBytes.Length);

                FormatException ex = ReadV3ExpectingFormatException(ref reader, ref header);

                Assert.Contains("stack size", ex.Message);
                Assert.Equal(V3HeaderSize, reader.StreamOffset);
                AssertStackSentinelUnchanged(header);
            }
        }

        [Fact]
        public void ReadFromFormatV3RejectsStackSizeThatOverflowsExtendedDataUShort()
        {
            // EventPipeMetadata.GetEventRecordForEventData stores (stackBytesSize + 8) in a ushort field.
            // Any stackBytesSize > ushort.MaxValue - 8 wraps that field and causes a downstream OOB read.
            int overflowingStackSize = ushort.MaxValue - sizeof(ulong) + 1; // 65528
            byte[] stackBytes = new byte[overflowingStackSize];
            byte[] eventBytes = CreateV3Event(Array.Empty<byte>(), stackBytesSize: overflowingStackSize, stackBytes: stackBytes);
            EventPipeEventHeader header = CreateHeaderWithStackSentinel();

            fixed (byte* eventBytesPtr = eventBytes)
            {
                SpanReader reader = CreateReader(eventBytesPtr, eventBytes.Length);

                FormatException ex = ReadV3ExpectingFormatException(ref reader, ref header);

                Assert.Contains("stack size", ex.Message);
                Assert.Equal(V3HeaderSize, reader.StreamOffset);
                AssertStackSentinelUnchanged(header);
            }
        }

        [Fact]
        public void ReadFromFormatV3AcceptsMaximumStackSizeUnderUShortBoundary()
        {
            // ushort.MaxValue - sizeof(ulong) == 65527 is the largest stack size that fits in the
            // downstream ExtendedData.DataSize ushort after the +8 header, and must still be accepted.
            int maxStackSize = ushort.MaxValue - sizeof(ulong); // 65527
            byte[] stackBytes = new byte[maxStackSize];
            for (int i = 0; i < stackBytes.Length; i++)
            {
                stackBytes[i] = (byte)i;
            }
            byte[] eventBytes = CreateV3Event(Array.Empty<byte>(), stackBytesSize: maxStackSize, stackBytes: stackBytes);

            fixed (byte* eventBytesPtr = eventBytes)
            {
                SpanReader reader = CreateReader(eventBytesPtr, eventBytes.Length);
                EventPipeEventHeader header = default;

                EventPipeEventHeader.ReadFromFormatV3(ref reader, ref header);

                Assert.Equal(maxStackSize, header.StackBytesSize);
            }
        }

        [Fact]
        public void ReadFromFormatV3RejectsEventSizeOverflow()
        {
            byte[] eventBytes = CreateV3Event(payloadSize: 0, eventSize: int.MaxValue);
            EventPipeEventHeader header = CreateHeaderWithStackSentinel();

            fixed (byte* eventBytesPtr = eventBytes)
            {
                SpanReader reader = CreateReader(eventBytesPtr, eventBytes.Length);

                FormatException ex = ReadV3ExpectingFormatException(ref reader, ref header);

                Assert.Contains("event size", ex.Message);
                Assert.Equal(V3HeaderSize, reader.StreamOffset);
                AssertStackSentinelUnchanged(header);
            }
        }

        private static SpanReader CreateReader(byte* bytes, int length)
        {
            return new SpanReader(new ReadOnlySpan<byte>(bytes, length), 0);
        }

        private static FormatException ReadV3ExpectingFormatException(ref SpanReader reader, ref EventPipeEventHeader header)
        {
            try
            {
                EventPipeEventHeader.ReadFromFormatV3(ref reader, ref header);
            }
            catch (FormatException ex)
            {
                return ex;
            }

            throw new Xunit.Sdk.XunitException("Expected a FormatException.");
        }

        private static EventPipeEventHeader CreateHeaderWithStackSentinel()
        {
            return new EventPipeEventHeader
            {
                StackBytesSize = 123,
                StackBytes = new IntPtr(456)
            };
        }

        private static void AssertStackSentinelUnchanged(EventPipeEventHeader header)
        {
            Assert.Equal(123, header.StackBytesSize);
            Assert.Equal(new IntPtr(456), header.StackBytes);
        }

        private static byte[] CreateV3Event(byte[] payload, int stackBytesSize, byte[] stackBytes, byte[] extraBytesAfterEvent = null)
        {
            int payloadSize = payload == null ? 0 : payload.Length;
            int stackBytesLength = stackBytes == null ? 0 : stackBytes.Length;
            int eventSize = V3HeaderSize + payloadSize + sizeof(int) + stackBytesLength - sizeof(int);
            return CreateV3Event(payloadSize, eventSize, payload, stackBytesSize, stackBytes, extraBytesAfterEvent);
        }

        private static byte[] CreateV3Event(int payloadSize, int eventSize)
        {
            return CreateV3Event(payloadSize, eventSize, payload: null, stackBytesSize: null, stackBytes: null, extraBytesAfterEvent: null);
        }

        private static byte[] CreateV3Event(int payloadSize, int eventSize, byte[] payload, int? stackBytesSize, byte[] stackBytes, byte[] extraBytesAfterEvent)
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(eventSize);
                writer.Write(1); // MetadataId
                writer.Write(2); // ThreadId
                writer.Write(3L); // TimeStamp
                writer.Write(Guid.Empty.ToByteArray());
                writer.Write(Guid.Empty.ToByteArray());
                writer.Write(payloadSize);

                if (payload != null)
                {
                    writer.Write(payload);
                }

                if (stackBytesSize.HasValue)
                {
                    writer.Write(stackBytesSize.Value);
                }

                if (stackBytes != null)
                {
                    writer.Write(stackBytes);
                }

                if (extraBytesAfterEvent != null)
                {
                    writer.Write(extraBytesAfterEvent);
                }

                return stream.ToArray();
            }
        }
    }
}
