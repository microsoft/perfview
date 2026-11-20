using Microsoft.Diagnostics.Tracing;
using System;
using System.IO;
using System.Reflection;
using Xunit;

namespace TraceEventTests.Regression
{
    public class RawReaderBoundsTests
    {
        [Fact]
        public void ReadAndSkipEntryPointsRequireBufferLengths()
        {
            foreach (var method in typeof(TraceEventRawReaders).GetMethods(BindingFlags.Static | BindingFlags.NonPublic))
            {
                if (!method.IsPrivate && (method.Name.StartsWith("Read") || method.Name.StartsWith("Skip")))
                {
                    Assert.Contains(method.GetParameters(),
                        parameter => parameter.Name == "bufferLength" && parameter.ParameterType == typeof(int));
                }
            }
        }

        [Fact]
        public void ScalarsValidateTheirCompleteWidth()
        {
            IsolatedTest.Run(() =>
            {
                var readers = new (int Width, Func<IntPtr, int, int, object> Read)[]
                {
                    (1, (p, o, l) => TraceEventRawReaders.ReadByte(p, o, l)),
                    (2, (p, o, l) => TraceEventRawReaders.ReadInt16(p, o, l)),
                    (4, (p, o, l) => TraceEventRawReaders.ReadInt32(p, o, l)),
                    (8, (p, o, l) => TraceEventRawReaders.ReadInt64(p, o, l)),
                    (4, (p, o, l) => TraceEventRawReaders.ReadSingle(p, o, l)),
                    (8, (p, o, l) => TraceEventRawReaders.ReadDouble(p, o, l)),
                    (16, (p, o, l) => TraceEventRawReaders.ReadGuid(p, o, l)),
                    (4, (p, o, l) => TraceEventRawReaders.ReadAddress(p, o, l, 4)),
                    (8, (p, o, l) => TraceEventRawReaders.ReadAddress(p, o, l, 8))
                };
                WithBuffer(new byte[32], pointer =>
                {
                    foreach (var reader in readers)
                    foreach (int offset in new[] { 0, 3 })
                    {
                        for (int remaining = 0; remaining < reader.Width; remaining++)
                            Assert.Throws<InvalidDataException>(() => reader.Read(pointer, offset, offset + remaining));
                        reader.Read(pointer, offset, offset + reader.Width);
                        Assert.Throws<InvalidDataException>(() => reader.Read(pointer, -1, 32));
                        Assert.Throws<InvalidDataException>(() => reader.Read(pointer, int.MaxValue, 32));
                        Assert.Throws<InvalidDataException>(() => reader.Read(IntPtr.Zero, 0, -1));
                    }
                    Assert.Throws<ArgumentOutOfRangeException>(() => TraceEventRawReaders.ReadAddress(pointer, 0, 32, 3));
                });
            }, GetType());
        }

        [Fact]
        public void ScalarsPreserveUnalignedValues()
        {
            IsolatedTest.Run(() =>
            {
                byte[] payload = new byte[24];
                for (int i = 0; i < payload.Length; i++)
                    payload[i] = (byte)(i * 13 + 129);
                byte[] guid = new byte[16];
                Array.Copy(payload, 3, guid, 0, guid.Length);
                WithBuffer(payload, pointer =>
                {
                    Assert.Equal(payload[3], TraceEventRawReaders.ReadByte(pointer, 3, payload.Length));
                    Assert.Equal(BitConverter.ToInt16(payload, 3), TraceEventRawReaders.ReadInt16(pointer, 3, payload.Length));
                    Assert.Equal(BitConverter.ToInt32(payload, 3), TraceEventRawReaders.ReadInt32(pointer, 3, payload.Length));
                    Assert.Equal(BitConverter.ToInt64(payload, 3), TraceEventRawReaders.ReadInt64(pointer, 3, payload.Length));
                    Assert.Equal(BitConverter.ToSingle(payload, 3), TraceEventRawReaders.ReadSingle(pointer, 3, payload.Length));
                    Assert.Equal(BitConverter.ToDouble(payload, 3), TraceEventRawReaders.ReadDouble(pointer, 3, payload.Length));
                    Assert.Equal(new Guid(guid), TraceEventRawReaders.ReadGuid(pointer, 3, payload.Length));
                    Assert.Equal((uint)BitConverter.ToInt32(payload, 3), TraceEventRawReaders.ReadAddress(pointer, 3, payload.Length, 4));
                    Assert.Equal((ulong)BitConverter.ToInt64(payload, 3), TraceEventRawReaders.ReadAddress(pointer, 3, payload.Length, 8));
                });
            }, GetType());
        }

        [Fact]
        public void BulkReadsValidateBeforeAllocating()
        {
            IsolatedTest.Run(() => WithBuffer(new byte[] { 99, 1, 0, 0, 0, 2, 0, 0, 0 }, pointer =>
            {
                foreach (int offset in new[] { -1, 10, int.MaxValue })
                {
                    Assert.Throws<InvalidDataException>(() => TraceEventRawReaders.ReadByteArray(pointer, offset, 9, 0));
                    Assert.Throws<InvalidDataException>(() => TraceEventRawReaders.ReadInt32Array(pointer, offset, 9, 0));
                }
                foreach (int count in new[] { -1, 10, int.MaxValue })
                {
                    Assert.Throws<InvalidDataException>(() => TraceEventRawReaders.ReadByteArray(pointer, 0, 9, count));
                    Assert.Throws<InvalidDataException>(() => TraceEventRawReaders.ReadInt32Array(pointer, 0, 9, count));
                    Assert.Throws<InvalidDataException>(() => TraceEventRawReaders.ReadFixedAnsiString(pointer, 0, 9, count));
                    Assert.Throws<InvalidDataException>(() => TraceEventRawReaders.ReadFixedUnicodeString(pointer, 0, 9, count));
                }
                Assert.Equal(new byte[] { 1, 0, 0, 0 }, TraceEventRawReaders.ReadByteArray(pointer, 1, 9, 4));
                Assert.Equal(new[] { 1, 2 }, TraceEventRawReaders.ReadInt32Array(pointer, 1, 9, 2));
                Assert.Empty(TraceEventRawReaders.ReadByteArray(pointer, 9, 9, 0));
                Assert.Empty(TraceEventRawReaders.ReadInt32Array(pointer, 9, 9, 0));
                Assert.Throws<ArgumentOutOfRangeException>(() => TraceEventRawReaders.ReadByteArray(pointer, 0, 9, 1, null, 1));
                byte[] target = new byte[3];
                Assert.Same(target, TraceEventRawReaders.ReadByteArray(pointer, 1, 9, 1, target, 2));
                Assert.Equal(new byte[] { 0, 0, 1 }, target);
            }), GetType());
        }

        [Fact]
        public void StringsRemainBoundedButSkipsRequireTerminators()
        {
            IsolatedTest.Run(() =>
            {
                WithBuffer(new byte[] { 99, 65, 66, 0 }, pointer =>
                {
                    Assert.Equal("AB", TraceEventRawReaders.ReadUTF8String(pointer, 1, 3));
                    Assert.Equal("AB", TraceEventRawReaders.ReadFixedAnsiString(pointer, 1, 3, 2));
                    Assert.Throws<InvalidDataException>(() => TraceEventRawReaders.SkipUTF8String(pointer, 1, 3));
                    Assert.Equal(4, TraceEventRawReaders.SkipUTF8String(pointer, 1, 4));
                    Assert.Equal("AB", TraceEventRawReaders.ReadFixedAnsiString(pointer, 1, 4, 3));
                    Assert.Throws<InvalidDataException>(() => TraceEventRawReaders.ReadFixedAnsiString(pointer, 1, 4, 4));
                    Assert.Throws<InvalidDataException>(() => TraceEventRawReaders.ReadUTF8String(pointer, -1, 4));
                });
                WithBuffer(new byte[] { 99, 65, 0, 66, 0, 0, 0 }, pointer =>
                {
                    Assert.Equal("A", TraceEventRawReaders.ReadUnicodeString(pointer, 1, 4));
                    Assert.Equal("AB", TraceEventRawReaders.ReadFixedUnicodeString(pointer, 1, 5, 2));
                    Assert.Throws<InvalidDataException>(() => TraceEventRawReaders.SkipUnicodeString(pointer, 1, 4));
                    Assert.Equal(7, TraceEventRawReaders.SkipUnicodeString(pointer, 1, 7));
                    Assert.Throws<InvalidDataException>(() => TraceEventRawReaders.ReadFixedUnicodeString(pointer, 1, 5, 3));
                    Assert.Throws<InvalidDataException>(() => TraceEventRawReaders.ReadUnicodeString(pointer, 8, 7));
                });
                WithBuffer(new byte[] { 99, 2, 0, 65, 66 }, pointer =>
                {
                    Assert.Equal("AB", TraceEventRawReaders.ReadShortUTF8String(pointer, 1, 5));
                    Assert.Equal(5, TraceEventRawReaders.SkipShortUTF8String(pointer, 1, 5));
                    foreach (int length in new[] { 1, 2, 3, 4 })
                    {
                        Assert.Throws<InvalidDataException>(() => TraceEventRawReaders.ReadShortUTF8String(pointer, 1, length));
                        Assert.Throws<InvalidDataException>(() => TraceEventRawReaders.SkipShortUTF8String(pointer, 1, length));
                    }
                });
            }, GetType());
        }

        [Fact]
        public void UnicodeSkipCountsPreserveNoOpAndCompleteExtents()
        {
            foreach (int offset in new[] { -1, 0, 1, int.MaxValue })
                Assert.Equal(offset, TraceEventRawReaders.SkipUnicodeString(IntPtr.Zero, offset, 0, 0));
            Assert.Throws<InvalidDataException>(() => TraceEventRawReaders.SkipUnicodeString(IntPtr.Zero, 0, 0, -1));
            IsolatedTest.Run(() => WithBuffer(new byte[] { 65, 0, 0, 0, 66, 0, 0, 0 }, pointer =>
            {
                Assert.Equal(4, TraceEventRawReaders.SkipUnicodeString(pointer, 0, 8, 1));
                Assert.Equal(8, TraceEventRawReaders.SkipUnicodeString(pointer, 0, 8, 2));
                Assert.Throws<InvalidDataException>(() => TraceEventRawReaders.SkipUnicodeString(pointer, 0, 7, 2));
                Assert.Throws<InvalidDataException>(() => TraceEventRawReaders.SkipUnicodeString(pointer, 0, 8, 3));
            }), GetType());
        }

        [Fact]
        public void SidSkipsValidateCountBytesAndCompleteExtents()
        {
            IsolatedTest.Run(() =>
            {
                foreach (int pointerSize in new[] { 4, 8 })
                foreach (byte count in new byte[] { 0, 1, 255 })
                {
                    int end = 3 + 2 * pointerSize + 8 + 4 * count;
                    byte[] payload = new byte[end];
                    payload[3] = 1;
                    payload[3 + 2 * pointerSize + 1] = count;
                    WithBuffer(payload, pointer =>
                    {
                        Assert.Equal(end, TraceEventRawReaders.SkipSID(pointer, 3, end, pointerSize));
                        Assert.Throws<InvalidDataException>(() => TraceEventRawReaders.SkipSID(pointer, 3, end - 1, pointerSize));
                        Assert.Throws<InvalidDataException>(() => TraceEventRawReaders.SkipSID(pointer, 3, 3 + 2 * pointerSize + 2, pointerSize));
                        Assert.Throws<InvalidDataException>(() => TraceEventRawReaders.SkipSID(pointer, -1, end, pointerSize));
                    });
                }
                WithBuffer(new byte[4], pointer =>
                {
                    Assert.Equal(4, TraceEventRawReaders.SkipSID(pointer, 0, 4, 4));
                    Assert.Throws<InvalidDataException>(() => TraceEventRawReaders.SkipSID(pointer, 0, 3, 4));
                });
            }, GetType());
        }

        [Fact]
        public void VariableIntegersAndTheirSkipsStayBounded()
        {
            IsolatedTest.Run(() =>
            {
                WithBuffer(new byte[] { 0x81, 1, 3 }, pointer =>
                {
                    Assert.Equal(129UL, TraceEventRawReaders.ReadVarUInt(pointer, 0, 3));
                    Assert.Equal(-2L, TraceEventRawReaders.ReadVarInt(pointer, 2, 3));
                    Assert.Equal(2, TraceEventRawReaders.SkipVarInt(pointer, 0, 3));
                    Assert.Throws<FormatException>(() => TraceEventRawReaders.ReadVarUInt(pointer, 0, 1));
                    Assert.Throws<InvalidDataException>(() => TraceEventRawReaders.SkipVarInt(pointer, 0, 1));
                    Assert.Throws<InvalidDataException>(() => TraceEventRawReaders.ReadVarUInt(pointer, -1, 3));
                    Assert.Throws<InvalidDataException>(() => TraceEventRawReaders.ReadVarInt(pointer, 4, 3));
                });
                byte[] unterminated = new byte[10];
                for (int i = 0; i < unterminated.Length; i++)
                    unterminated[i] = 0x80;
                WithBuffer(unterminated, pointer =>
                {
                    Assert.Throws<FormatException>(() => TraceEventRawReaders.ReadVarUInt(pointer, 0, 10));
                    Assert.Throws<InvalidDataException>(() => TraceEventRawReaders.SkipVarInt(pointer, 0, 10));
                });
            }, GetType());
        }

        #region private
        private static unsafe void WithBuffer(byte[] payload, Action<IntPtr> test)
        {
            byte[] storage = new byte[payload.Length + 32];
            payload.CopyTo(storage, 16);
            fixed (byte* pointer = storage)
                test((IntPtr)(pointer + 16));
        }
        #endregion
    }
}
