using System;
using System.Collections.Generic;
using FastSerialization;
using Xunit;

namespace FastSerializationTests
{
    /// <summary>
    /// Tests for SegmentedMemoryStreamWriter.
    /// </summary>
    public class SegmentedMemoryStreamWriterTests
    {
        [Fact]
        public void WriteAndRead_BasicData()
        {
            var settings = SerializationSettings.Default;
            var writer = new SegmentedMemoryStreamWriter(64, settings);

            writer.Write((byte)42);
            writer.Write((int)12345);
            writer.Write((long)9876543210L);

            var reader = writer.GetReader();
            Assert.Equal(42, reader.ReadByte());
            Assert.Equal(12345, reader.ReadInt32());
            Assert.Equal(9876543210L, reader.ReadInt64());
        }

        [Fact]
        public void Clear_ResetsBytesToZeroLength()
        {
            var settings = SerializationSettings.Default;
            var writer = new SegmentedMemoryStreamWriter(64, settings);

            // Write some data
            writer.Write((byte)1);
            writer.Write((byte)2);
            writer.Write((byte)3);
            Assert.Equal(3, writer.Length);

            // Clear should reset length to 0
            writer.Clear();
            Assert.Equal(0, writer.Length);
        }

        [Fact]
        public void Clear_AllowsWritingAgain()
        {
            var settings = SerializationSettings.Default;
            var writer = new SegmentedMemoryStreamWriter(64, settings);

            // Write initial data
            writer.Write((int)100);
            writer.Write((int)200);

            // Clear and write new data
            writer.Clear();
            writer.Write((byte)42);
            writer.Write((int)999);

            // Reader should only see the new data
            var reader = writer.GetReader();
            Assert.Equal(42, reader.ReadByte());
            Assert.Equal(999, reader.ReadInt32());
        }

        [Fact]
        public void Clear_PreservesPreAllocatedBuffer()
        {
            // This test verifies that after Clear(), writing data does not throw
            // NullReferenceException - the pre-allocated buffer must be reused.
            var settings = SerializationSettings.Default;
            // Use a larger initial size to ensure pre-allocation
            var writer = new SegmentedMemoryStreamWriter(1024, settings);

            // Write data to use some of the buffer
            for (int i = 0; i < 100; i++)
            {
                writer.Write((byte)i);
            }
            Assert.Equal(100, writer.Length);

            // Clear and write a large number of bytes (exercises EnsureCapacity paths)
            writer.Clear();
            Assert.Equal(0, writer.Length);

            // Write enough bytes to span multiple segments
            for (int i = 0; i < 200_000; i++)
            {
                writer.Write((byte)(i & 0xFF));
            }
            Assert.Equal(200_000, writer.Length);

            // Verify the data is readable and correct
            var reader = writer.GetReader();
            for (int i = 0; i < 200_000; i++)
            {
                Assert.Equal((byte)(i & 0xFF), reader.ReadByte());
            }
        }

        [Fact]
        public void Clear_WorksWithInitialSizeZero()
        {
            // Tests the case where the writer starts with no pre-allocation (initial size = 0/64).
            // This is the code path hit by SegmentedMemoryStreamWriter(SerializationSettings settings).
            var settings = SerializationSettings.Default;
            var writer = new SegmentedMemoryStreamWriter(settings);

            writer.Write((byte)10);
            writer.Write((byte)20);

            writer.Clear();
            Assert.Equal(0, writer.Length);

            writer.Write((byte)30);
            Assert.Equal(1, writer.Length);

            var reader = writer.GetReader();
            Assert.Equal(30, reader.ReadByte());
        }

        [Fact]
        public void Clear_MultipleTimes_WorksCorrectly()
        {
            var settings = SerializationSettings.Default;
            var writer = new SegmentedMemoryStreamWriter(64, settings);

            for (int round = 0; round < 5; round++)
            {
                writer.Clear();
                Assert.Equal(0, writer.Length);

                // Write distinct values for each round
                writer.Write((byte)round);
                writer.Write((int)(round * 100));
                Assert.Equal(5, writer.Length); // 1 byte + 4 bytes

                var reader = writer.GetReader();
                Assert.Equal((byte)round, reader.ReadByte());
                Assert.Equal(round * 100, reader.ReadInt32());
            }
        }

        [Fact]
        public void GetLabel_ReturnsCurrentPosition()
        {
            var settings = SerializationSettings.Default;
            var writer = new SegmentedMemoryStreamWriter(64, settings);

            Assert.Equal((StreamLabel)0, writer.GetLabel());

            writer.Write((byte)1);
            Assert.Equal((StreamLabel)1, writer.GetLabel());

            writer.Write((int)42);
            Assert.Equal((StreamLabel)5, writer.GetLabel());

            writer.Clear();
            Assert.Equal((StreamLabel)0, writer.GetLabel());
        }

        [Fact]
        public void WriteAcrossSegmentBoundary_WorksCorrectly()
        {
            // Uses a small initial size to force segment boundary crossings
            var settings = SerializationSettings.Default;
            var writer = new SegmentedMemoryStreamWriter(64, settings);

            // Write enough data to cross at least one segment boundary
            // SegmentedList segmentSize is 65536 for the initial writer
            const int count = 70_000;
            for (int i = 0; i < count; i++)
            {
                writer.Write((byte)(i % 251)); // use prime to vary patterns
            }

            Assert.Equal(count, writer.Length);

            var reader = writer.GetReader();
            for (int i = 0; i < count; i++)
            {
                Assert.Equal((byte)(i % 251), reader.ReadByte());
            }
        }
    }
}
