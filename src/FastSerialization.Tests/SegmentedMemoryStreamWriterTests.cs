using FastSerialization;
using Xunit;

namespace FastSerializationTests
{
    /// <summary>
    /// Tests for SegmentedMemoryStreamWriter, focusing on Clear() semantics.
    /// </summary>
    public class SegmentedMemoryStreamWriterTests
    {
        [Fact]
        public void Clear_ResetsLengthToZero()
        {
            var writer = new SegmentedMemoryStreamWriter(64, SerializationSettings.Default);

            writer.Write((byte)1);
            writer.Write((byte)2);
            writer.Write((byte)3);
            Assert.Equal(3, writer.Length);

            writer.Clear();
            Assert.Equal(0, writer.Length);
        }

        [Fact]
        public void Clear_AllowsWritingNewData()
        {
            var writer = new SegmentedMemoryStreamWriter(64, SerializationSettings.Default);

            writer.Write((int)100);
            writer.Write((int)200);

            writer.Clear();
            writer.Write((byte)42);
            writer.Write((int)999);

            var reader = writer.GetReader();
            Assert.Equal(42, reader.ReadByte());
            Assert.Equal(999, reader.ReadInt32());
        }

        [Fact]
        public void Clear_PreservesBufferAndDoesNotThrowNRE()
        {
            // This is the core scenario from issue #951: after Clear(), writing
            // large amounts of data must not throw NullReferenceException.
            var writer = new SegmentedMemoryStreamWriter(1024, SerializationSettings.Default);

            for (int i = 0; i < 100; i++)
            {
                writer.Write((byte)i);
            }

            writer.Clear();
            Assert.Equal(0, writer.Length);

            // Write enough to span multiple segments (segment size is 65_536).
            for (int i = 0; i < 200_000; i++)
            {
                writer.Write((byte)(i & 0xFF));
            }

            Assert.Equal(200_000, writer.Length);

            var reader = writer.GetReader();
            for (int i = 0; i < 200_000; i++)
            {
                Assert.Equal((byte)(i & 0xFF), reader.ReadByte());
            }
        }

        [Fact]
        public void Clear_ResetsGetLabel()
        {
            var writer = new SegmentedMemoryStreamWriter(64, SerializationSettings.Default);

            writer.Write((byte)1);
            writer.Write((int)42);
            Assert.Equal((StreamLabel)5, writer.GetLabel());

            writer.Clear();
            Assert.Equal((StreamLabel)0, writer.GetLabel());
        }

        [Fact]
        public void Clear_MultipleCycles()
        {
            var writer = new SegmentedMemoryStreamWriter(64, SerializationSettings.Default);

            for (int round = 0; round < 5; round++)
            {
                writer.Clear();
                Assert.Equal(0, writer.Length);

                writer.Write((byte)round);
                writer.Write((int)(round * 100));
                Assert.Equal(5, writer.Length);

                var reader = writer.GetReader();
                Assert.Equal((byte)round, reader.ReadByte());
                Assert.Equal(round * 100, reader.ReadInt32());
            }
        }
    }
}
