using Microsoft.Diagnostics.Tracing;
using System;
using System.IO;
using Xunit;

namespace TraceEventTests.Regression
{
    /// <summary>
    /// Regression tests for malformed BPerf event records. Each test constructs an
    /// uncompressed buffer that claims more space than is actually present (truncated
    /// header, oversized UserDataLength, oversized ExtendedDataCount, oversized extended
    /// data payload, or trailing alignment that spills past the buffer) and verifies that
    /// BPerfEventSource rejects it with InvalidDataException instead of reading past the
    /// end of the decompressed buffer.
    /// </summary>
    public class BPerfEventSourceTests
    {
        // Sizes and offsets within the native EVENT_RECORD layout that BPerf deserializes.
        // Held as constants so the tests can poke specific fields without depending on
        // PerfView's internal types.
        private const int EventRecordSize = 112;
        private const int ExtendedDataCountOffset = 84;
        private const int UserDataLengthOffset = 86;
        private const int ExtendedDataItemSize = 16;
        private const int ExtendedDataItemDataSizeOffset = 6;

        [Fact]
        public void ProcessThrowsInvalidDataExceptionForTruncatedEventRecordHeader()
        {
            // Buffer is one byte shorter than the EVENT_RECORD header, so even the
            // initial header read should fail bounds checking.
            AssertMalformedBtlThrows(new byte[EventRecordSize - 1]);
        }

        [Fact]
        public void ProcessThrowsInvalidDataExceptionWhenUserDataExtendsPastBuffer()
        {
            // Buffer is exactly the header size, but UserDataLength = 1 claims one more
            // byte of user data that does not exist in the buffer.
            byte[] record = new byte[EventRecordSize];
            WriteUInt16(record, UserDataLengthOffset, 1);

            AssertMalformedBtlThrows(record);
        }

        [Fact]
        public void ProcessThrowsInvalidDataExceptionWhenExtendedDataArrayExtendsPastBuffer()
        {
            // Buffer is exactly the header size, but ExtendedDataCount = 1 claims a
            // 16-byte extended-data item that does not fit in the buffer.
            byte[] record = new byte[EventRecordSize];
            WriteUInt16(record, ExtendedDataCountOffset, 1);

            AssertMalformedBtlThrows(record);
        }

        [Fact]
        public void ProcessThrowsInvalidDataExceptionWhenExtendedDataPayloadExtendsPastBuffer()
        {
            // Buffer holds the header and one extended-data item, but that item's
            // DataSize = 1 claims a payload byte that lies past the end of the buffer.
            byte[] record = new byte[EventRecordSize + ExtendedDataItemSize];
            WriteUInt16(record, ExtendedDataCountOffset, 1);
            WriteUInt16(record, EventRecordSize + ExtendedDataItemDataSizeOffset, 1);

            AssertMalformedBtlThrows(record);
        }

        [Fact]
        public void ProcessThrowsInvalidDataExceptionWhenFinalRecordAlignmentExtendsPastBuffer()
        {
            // Buffer is 8 bytes larger than the header and UserDataLength = 1 fits within
            // the buffer, but the final 16-byte alignment of the record's end would push
            // past the buffer.
            byte[] record = new byte[EventRecordSize + 8];
            WriteUInt16(record, UserDataLengthOffset, 1);

            AssertMalformedBtlThrows(record);
        }

        /// <summary>
        /// Wraps <paramref name="uncompressedData"/> in a minimal BTL chunk on disk, then
        /// constructs a BPerfEventSource over it and asserts that consuming the source
        /// throws <see cref="InvalidDataException"/>.
        /// </summary>
        private static void AssertMalformedBtlThrows(byte[] uncompressedData)
        {
            string btlPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".btl");

            try
            {
                File.WriteAllBytes(btlPath, CreateBtlChunk(uncompressedData));

                Assert.Throws<InvalidDataException>(
                    () => new BPerfEventSource(
                        btlPath,
                        new TraceEventDispatcherOptions(),
                        new byte[1024],
                        new byte[1024],
                        decompressDelegate: CopyDecompress));
            }
            finally
            {
                if (File.Exists(btlPath))
                {
                    File.Delete(btlPath);
                }
            }
        }

        /// <summary>
        /// Builds the minimal BTL chunk header that BPerfEventSource expects: a compressed
        /// length and an uncompressed length followed by the (uncompressed) payload bytes.
        /// Because the test uses <see cref="CopyDecompress"/>, both lengths are equal.
        /// </summary>
        private static byte[] CreateBtlChunk(byte[] uncompressedData)
        {
            byte[] btl = new byte[sizeof(int) + sizeof(int) + uncompressedData.Length];
            BitConverter.GetBytes(uncompressedData.Length).CopyTo(btl, 0);
            BitConverter.GetBytes(uncompressedData.Length).CopyTo(btl, sizeof(int));
            uncompressedData.CopyTo(btl, sizeof(int) + sizeof(int));
            return btl;
        }

        // Identity "decompression" delegate so the tests directly control the bytes that
        // BPerf's record deserializer sees. The real ULZ777 decompressor is exercised by
        // other tests.
        private static int CopyDecompress(byte[] input, int inputOffset, int inputLength, byte[] output, int outputLength)
        {
            Array.Copy(input, inputOffset, output, 0, inputLength);
            return inputLength;
        }

        private static void WriteUInt16(byte[] buffer, int offset, ushort value)
        {
            byte[] valueBytes = BitConverter.GetBytes(value);
            valueBytes.CopyTo(buffer, offset);
        }
    }
}
