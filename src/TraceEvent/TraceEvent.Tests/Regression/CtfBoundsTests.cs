using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Ctf;
using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using Xunit;

namespace TraceEventTests.Regression
{
    public class CtfBoundsTests
    {
        [Fact]
        public void MissingAlignmentDoesNotPublishAnExtent()
        {
            using (var stream = new MemoryStream(new byte[1]))
            {
                stream.Position = 1;
                using (var reader = new CtfReader(stream, null, null))
                {
                    Assert.Throws<EndOfStreamException>(() => reader.ReadTypeIntoBuffer(null, Integer(16, 16)));
                    Assert.Equal(0, reader.BufferLength);
                }
            }
        }

        [Fact]
        public void PartialReadsAreCompletedAndInvalidAlignmentIsRejected()
        {
            using (var reader = new CtfReader(new ShortReadStream(new byte[8]), null, null))
            {
                reader.ReadTypeIntoBuffer(null, Integer(64, 8));
                Assert.Equal(8, reader.BufferLength);
            }
            foreach (int alignment in new[] { -8, 0, 3 })
            {
                using (var reader = new CtfReader(new MemoryStream(new byte[8]), null, null))
                    Assert.Throws<InvalidDataException>(() => reader.ReadTypeIntoBuffer(null, Integer(8, alignment)));
            }
        }

        [Fact]
        public void BufferGrowthPreservesPreviouslyReadBytes()
        {
            IsolatedTest.Run(() =>
            {
                byte[] expected = new byte[2051];
                for (int i = 0; i < expected.Length; i++)
                    expected[i] = (byte)i;
                using (var reader = new CtfReader(new ShortReadStream(expected), null, null))
                {
                    reader.ReadTypeIntoBuffer(null, Integer(64, 8));
                    reader.ReadTypeIntoBuffer(null, new CtfArray(Integer(8, 8), (expected.Length - 8).ToString()));
                    Assert.Equal(expected.Length, reader.BufferLength);
                    byte[] actual = new byte[reader.BufferLength];
                    Marshal.Copy(reader.BufferPtr, actual, 0, actual.Length);
                    Assert.Equal(expected, actual);
                }
            }, GetType());
        }

        [Theory]
        [InlineData(268435456)]
        [InlineData(536870912)]
        [InlineData(536870913)]
        public void FixedArraySizeOverflowIsRejectedBeforeReading(int length)
        {
            var array = new CtfArray(Integer(8, 8), length.ToString());
            Assert.Throws<OverflowException>(() => array.GetSize());
            using (var stream = new MemoryStream(new byte[4]))
            using (var reader = new CtfReader(stream, null, null))
            {
                Assert.Throws<OverflowException>(() => reader.ReadTypeIntoBuffer(null, array));
                Assert.Equal(0, stream.Position);
                Assert.Equal(0, reader.BufferLength);
            }
        }

        [Fact]
        public void FixedArraySizeBoundariesRemainSupported()
        {
            var element = Integer(8, 8);
            foreach (int length in new[] { 0, 3, int.MaxValue / 8 })
                Assert.Equal(length * 8, new CtfArray(element, length.ToString()).GetSize());

            var variable = new CtfArray(element, "length");
            Assert.Equal(CtfEvent.SizeIndeterminate, variable.GetSize());
            Assert.Equal(CtfEvent.SizeIndeterminate, new CtfArray(variable, "3").GetSize());
        }

        [Fact]
        public void PackedLengthsRejectNegativeAndOverflowingValues()
        {
            IsolatedTest.Run(() =>
            {
                foreach (int length in new[] { -1, int.MaxValue, int.MaxValue / 8 + 1 })
                {
                    WithTrace(true, Packed(length, 0), path =>
                    {
                        using (var source = new CtfTraceEventSource(path))
                            Assert.Throws<InvalidDataException>(() => source.Process());
                    });
                }
            }, GetType());
        }

        [Fact]
        public void IncompleteEventsAreNotDispatchedAsCompleteEvents()
        {
            IsolatedTest.Run(() =>
            {
                foreach (byte[] bytes in new[]
                {
                    new byte[] { 0, 1, 0 },
                    new byte[] { 0, 1, 0, 0, 0x42 },
                    new byte[] { 0, 1, 0, 0, 0x42, 0, 0 }
                })
                {
                    WithTrace(false, bytes, path =>
                    {
                        using (var source = new CtfTraceEventSource(path))
                            Assert.Throws<EndOfStreamException>(() => source.Process());
                    });
                }
            }, GetType());
        }

        [Fact]
        public void CleanEndOfChannelPreservesCloningAndExport()
        {
            IsolatedTest.Run(() => WithTrace(false, new byte[] { 0, 1, 0, 0, 0x42, 0 }, path =>
            {
                TraceEvent clone = null;
                using (var source = new CtfTraceEventSource(path))
                {
                    source.AllEvents += data => clone = data.Clone();
                    Assert.True(source.Process());
                }
                Assert.NotNull(clone);
                Assert.Equal(new byte[] { 0x42, 0 }, clone.EventData());
            }), GetType());
        }

        [Fact]
        public void NativePayloadLengthDoesNotWrap()
        {
            IsolatedTest.Run(() =>
            {
                foreach (int length in new[] { 65535, 65536 })
                {
                    WithTrace(true, Packed(length, length), path =>
                    {
                        using (var source = new CtfTraceEventSource(path))
                        {
                            int dispatched = 0;
                            source.AllEvents += data =>
                            {
                                dispatched++;
                                Assert.Equal(length, data.Clone().EventData().Length);
                            };
                            if (length == 65535)
                            {
                                Assert.True(source.Process());
                                Assert.Equal(1, dispatched);
                            }
                            else
                            {
                                Assert.Throws<InvalidDataException>(() => source.Process());
                                Assert.Equal(0, dispatched);
                            }
                        }
                    });
                }
            }, GetType());
        }

        [Fact]
        public void ConvertedStringPayloadsMustFitNativeLength()
        {
            IsolatedTest.Run(() =>
            {
                foreach (int characters in new[] { 32766, 32767 })
                {
                    byte[] bytes = new byte[3 + characters + 1];
                    bytes[1] = 1;
                    for (int i = 0; i < characters; i++)
                        bytes[3 + i] = (byte)'A';

                    int convertedLength = (characters + 1) * sizeof(char);
                    WithTrace(false, bytes, path =>
                    {
                        using (var source = new CtfTraceEventSource(path))
                        {
                            int dispatched = 0;
                            source.AllEvents += data =>
                            {
                                dispatched++;
                                Assert.Equal(convertedLength, data.EventDataLength);
                                byte[] payload = data.Clone().EventData();
                                Assert.Equal(new string('A', characters) + "\0", Encoding.Unicode.GetString(payload));
                            };
                            if (convertedLength <= ushort.MaxValue)
                            {
                                Assert.True(source.Process());
                                Assert.Equal(1, dispatched);
                            }
                            else
                            {
                                Assert.Throws<InvalidDataException>(() => source.Process());
                                Assert.Equal(0, dispatched);
                            }
                        }
                    }, fields: "string value;");
                }
            }, GetType());
        }

        #region private
        private static CtfInteger Integer(int size, int alignment)
        {
            var bag = new CtfPropertyBag();
            bag.AddValue("size", size.ToString());
            bag.AddValue("align", alignment.ToString());
            bag.AddValue("signed", "false");
            return new CtfInteger(bag);
        }

        private static byte[] Packed(int length, int available)
        {
            byte[] result = new byte[11 + available];
            result[1] = 1;
            BitConverter.GetBytes(length).CopyTo(result, 3);
            return result;
        }

        private static void WithTrace(bool packed, byte[] bytes, Action<string> test, string fields = null)
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ctf.zip");
            try
            {
                fields = fields ?? (packed ?
                    "uint32_t length; uint32_t reserved; uint8_t ___data__[length];" :
                    "integer { size = 16; align = 16; signed = false; } value;");
                byte[] metadata = Encoding.UTF8.GetBytes(Metadata.Replace("PAYLOAD_FIELDS", fields));
                using (var archive = new ZipArchive(File.Create(path), ZipArchiveMode.Create))
                {
                    using (var writer = new BinaryWriter(archive.CreateEntry("trace64-bit/metadata").Open()))
                    {
                        writer.Write(0x75d11d57u);
                        writer.Write(new byte[20]);
                        writer.Write((uint)(37 + metadata.Length) * 8);
                        writer.Write((uint)(37 + metadata.Length) * 8);
                        writer.Write(new byte[] { 0, 0, 0, 1, 8 });
                        writer.Write(metadata);
                    }
                    using (var writer = new BinaryWriter(archive.CreateEntry("trace64-bit/channel_0").Open()))
                    {
                        writer.Write(0xc1fc1fc1u);
                        writer.Write(0u);
                        writer.Write((ulong)(24 + bytes.Length) * 8);
                        writer.Write((ulong)(24 + bytes.Length) * 8);
                        writer.Write(bytes);
                    }
                }
                test(path);
            }
            finally { File.Delete(path); }
        }

        private sealed class ShortReadStream : MemoryStream
        {
            public ShortReadStream(byte[] bytes) : base(bytes) { }
            public override int Read(byte[] buffer, int offset, int count) =>
                base.Read(buffer, offset, Math.Min(count, 2));
        }

        private const string Metadata = @"
typealias integer { size = 8; align = 8; signed = false; } := uint8_t;
typealias integer { size = 16; align = 8; signed = false; } := uint16_t;
typealias integer { size = 32; align = 8; signed = false; } := uint32_t;
typealias integer { size = 64; align = 8; signed = false; } := uint64_t;
trace {
 major = 1; minor = 8;
 uuid = ""00000000-0000-0000-0000-000000000000"";
 byte_order = le;
 packet.header := struct { uint32_t magic; uint32_t stream_id; };
};
env { domain = ""ust""; };
clock {
 name = monotonic;
 uuid = ""00000000-0000-0000-0000-000000000000"";
 freq = 10000000; offset = 1;
};
stream {
 id = 0;
 packet.context := struct { uint64_t content_size; uint64_t packet_size; };
 event.header := struct {
  enum : uint8_t { compact = 0 ... 254, extended = 255 } id;
  variant <id> {
   struct { uint16_t timestamp; } compact;
   struct { uint32_t id; uint64_t timestamp; } extended;
  } v;
 };
};
event {
 name = ""DotNETRuntime:GCStart"";
 id = 0; stream_id = 0;
 fields := struct { PAYLOAD_FIELDS };
};
";
        #endregion
    }
}
