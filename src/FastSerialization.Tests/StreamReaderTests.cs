using System;
using System.IO;
using System.Reflection;
using System.Text;
using FastSerialization;
using Xunit;

namespace FastSerializationTests
{
    /// <summary>
    /// Tests string length validation and allocation across the memory and stream readers.
    /// </summary>
    public class StreamReaderTests
    {
        [Theory]
        [InlineData("Memory", false)]
        [InlineData("Memory", true)]
        [InlineData("Seekable", false)]
        [InlineData("Seekable", true)]
        [InlineData("Pinned", false)]
        [InlineData("Pinned", true)]
        public void ReadStringRejectsLengthBeyondRemainingBytesBeforeChangingBuilder(string readerKind, bool reuseBuilder)
        {
            string previous = new string('a', 8192);
            byte[] data = Encode(writer =>
            {
                if (reuseBuilder)
                {
                    writer.Write(previous);
                }
                writer.Write(4096);
                writer.Write((byte)'x');
            });

            using (MemoryStreamReader reader = CreateReader(readerKind, data))
            {
                if (reuseBuilder)
                {
                    Assert.Equal(previous, reader.ReadString());
                }
                StringBuilder builder = GetStringBuilder(reader);
                long prefixEnd = (long)reader.Current + sizeof(int);

                Assert.Throws<SerializationException>(() => reader.ReadString());

                Assert.Equal(prefixEnd, (long)reader.Current);
                Assert.Same(builder, GetStringBuilder(reader));
                if (builder != null)
                {
                    Assert.Equal(previous, builder.ToString());
                }
            }
        }

        [Fact]
        public void ReadStringDoesNotAllocateFromNonSeekableLengthPrefix()
        {
            byte[] data = Encode(writer =>
            {
                writer.Write(4096);
                writer.Write((byte)'x');
            });

            using (MemoryStreamReader reader = CreateReader("Streaming", data))
            {
                Assert.False(reader.HasLength);
                Assert.Throws<Exception>(() => reader.ReadString());

                // Check capacity directly so the regression is deterministic on every target runtime.
                StringBuilder builder = GetStringBuilder(reader);
                Assert.NotNull(builder);
                Assert.InRange(builder.Capacity, 1, 256);
                Assert.Equal("x", builder.ToString());
            }
        }

        [Theory]
        [InlineData("Memory")]
        [InlineData("Seekable")]
        [InlineData("Pinned")]
        [InlineData("Streaming")]
        public void ReadStringRejectsMaximumLengthWithoutLargeAllocation(string readerKind)
        {
            byte[] data = Encode(writer => writer.Write(int.MaxValue));
            using (MemoryStreamReader reader = CreateReader(readerKind, data))
            {
                if (reader.HasLength)
                {
                    Assert.Throws<SerializationException>(() => reader.ReadString());
                    Assert.Null(GetStringBuilder(reader));
                }
                else
                {
                    Assert.Throws<Exception>(() => reader.ReadString());
                    Assert.InRange(GetStringBuilder(reader).Capacity, 1, 256);
                }
            }
        }

        [Theory]
        [InlineData("Memory")]
        [InlineData("Seekable")]
        [InlineData("Pinned")]
        [InlineData("Streaming")]
        public void ReadStringRejectsInvalidNegativeLengths(string readerKind)
        {
            foreach (int length in new[] { -2, int.MinValue })
            {
                using (MemoryStreamReader reader = CreateReader(readerKind, Encode(writer => writer.Write(length))))
                {
                    Assert.Throws<SerializationException>(() => reader.ReadString());
                    Assert.Null(GetStringBuilder(reader));
                }
            }
        }

        [Theory]
        [InlineData("Memory")]
        [InlineData("Seekable")]
        [InlineData("Pinned")]
        [InlineData("Streaming")]
        public void ReadStringPreservesWireFormatAndLargeStrings(string readerKind)
        {
            string[] values =
            {
                null,
                "",
                "ASCII",
                "\u007f\u0080\u07ff\u0800\uffff",
                "\ud83d\ude80\ud800X\udc00",
                new string('a', 40000),
                new string('\u0800', 40000),
                null,
                ""
            };
            byte[] data = Encode(writer =>
            {
                foreach (string value in values)
                {
                    writer.Write(value);
                }
            });

            using (MemoryStreamReader reader = CreateReader(readerKind, data))
            {
                foreach (string value in values)
                {
                    Assert.Equal(value, reader.ReadString());
                }
                Assert.Equal(data.Length, (long)reader.Current);
            }
        }

        [Theory]
        [InlineData("Memory")]
        [InlineData("Seekable")]
        [InlineData("Pinned")]
        [InlineData("Streaming")]
        public void ReadStringStillRejectsTruncatedMultibyteCharacters(string readerKind)
        {
            foreach (byte[] content in new[] { new byte[] { 0xc2 }, new byte[] { 0xe0, 0xa0 } })
            {
                byte[] data = Encode(writer =>
                {
                    writer.Write(1);
                    foreach (byte value in content)
                    {
                        writer.Write(value);
                    }
                });
                using (MemoryStreamReader reader = CreateReader(readerKind, data))
                {
                    Assert.Throws<Exception>(() => reader.ReadString());
                    Assert.Equal(data.Length, (long)reader.Current);
                }
            }
        }

        [Theory]
        [InlineData("Memory")]
        [InlineData("Seekable")]
        [InlineData("Pinned")]
        [InlineData("Streaming")]
        public void DeserializerRejectsOversizedTypeNameBeforeFactoryLookup(string readerKind)
        {
            byte[] serialized;
            using (var stream = new MemoryStream())
            {
                using (var serializer = new Serializer(stream, new PrimitiveTypes(), leaveOpen: true))
                {
                }
                serialized = stream.ToArray();
            }

            string typeName = typeof(PrimitiveTypes).FullName;
            int typeNameOffset = Encoding.ASCII.GetString(serialized).IndexOf(typeName, StringComparison.Ordinal);
            Assert.True(typeNameOffset >= sizeof(int));
            int prefixOffset = typeNameOffset - sizeof(int);
            Assert.Equal(typeName.Length, BitConverter.ToInt32(serialized, prefixOffset));

            byte[] data = new byte[typeNameOffset];
            Array.Copy(serialized, data, data.Length);
            Array.Copy(BitConverter.GetBytes(4096), 0, data, prefixOffset, sizeof(int));
            using (MemoryStreamReader reader = CreateReader(readerKind, data))
            using (var deserializer = new Deserializer(reader, "oversized type name"))
            {
                bool factoryCalled = false;
                deserializer.RegisterFactory(typeof(PrimitiveTypes), () =>
                {
                    factoryCalled = true;
                    return new PrimitiveTypes();
                });

                if (reader.HasLength)
                {
                    Assert.Throws<SerializationException>(() => deserializer.GetEntryObject<PrimitiveTypes>(out _));
                    Assert.Null(GetStringBuilder(reader));
                }
                else
                {
                    Assert.Throws<Exception>(() => deserializer.GetEntryObject<PrimitiveTypes>(out _));
                    Assert.InRange(GetStringBuilder(reader).Capacity, 1, 256);
                }
                Assert.False(factoryCalled);
                Assert.Equal(data.Length, (long)reader.Current);
            }
        }

        #region private
        private static byte[] Encode(Action<MemoryStreamWriter> write)
        {
            using (var writer = new MemoryStreamWriter(SerializationSettings.Default))
            {
                write(writer);
                byte[] data = new byte[(int)writer.Length];
                Array.Copy(writer.GetBytes(), data, data.Length);
                return data;
            }
        }

        private static MemoryStreamReader CreateReader(string readerKind, byte[] data)
        {
            switch (readerKind)
            {
                case "Memory":
                    return new MemoryStreamReader(data, SerializationSettings.Default);
                case "Seekable":
                    return new IOStreamStreamReader(new MemoryStream(data), SerializationSettings.Default);
                case "Pinned":
                    return new PinnedStreamReader(new MemoryStream(data), SerializationSettings.Default);
                case "Streaming":
                    return new IOStreamStreamReader(new NonSeekableStream(data), SerializationSettings.Default);
                default:
                    throw new ArgumentException("Unknown reader kind.", nameof(readerKind));
            }
        }

        private static StringBuilder GetStringBuilder(MemoryStreamReader reader)
        {
            return (StringBuilder)typeof(MemoryStreamReader).GetField("sb", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(reader);
        }

        private sealed class NonSeekableStream : MemoryStream
        {
            public NonSeekableStream(byte[] data) : base(data) { }

            public override bool CanSeek => false;
            public override long Length => throw new NotSupportedException();
            public override long Position
            {
                get => throw new NotSupportedException();
                set => throw new NotSupportedException();
            }

            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override int Read(byte[] buffer, int offset, int count) => base.Read(buffer, offset, Math.Min(count, 3));
        }
        #endregion
    }
}
