using System;
using System.Collections.Generic;
using Xunit;
using Microsoft.Diagnostics.Tracing.Ctf;
using Microsoft.Diagnostics.Tracing.Ctf.CtfMetadataTypes;

namespace Tests.CtfMetadataTypes
{
    public class CtfIntegerTests
    {
        public static IEnumerable<object[]> UnsignedByteReadingDataSource
        {
            get
            {
                yield return new object[] { byte.MinValue, 0, 8 };

                yield return new object[] { byte.MaxValue, 0, 8 };
                yield return new object[] { byte.MaxValue, 3, 8 };
                yield return new object[] { byte.MaxValue, 8, 8 };

                yield return new object[] { 31, 0, 5 };
                yield return new object[] { 31, 2, 5 };
                yield return new object[] { 31, 6, 5 };
                yield return new object[] { 31, 8, 5 };
            }
        }

        [Theory]
        [MemberData(nameof(UnsignedByteReadingDataSource))]
        public void TestReadUnsignedByte(byte value, int bitOffset, int sizeInBit)
        {
            var valueStoredIn64Bitfields = ((ulong)value) << bitOffset;
            var buffer = BitConverter.GetBytes(valueStoredIn64Bitfields);

            var integer = CreateInteger(sizeInBit, isSigned: false);

            Assert.Equal(value, CtfInteger.ReadInt<byte>(integer, buffer, bitOffset));
        }

        public static IEnumerable<object[]> SignedByteReadingDataSource
        {
            get
            {
                yield return new object[] { sbyte.MaxValue, 0, 8 };
                yield return new object[] { sbyte.MaxValue, 3, 8 };
                yield return new object[] { sbyte.MaxValue, 8, 8 };

                yield return new object[] { sbyte.MinValue, 0, 8 };
                yield return new object[] { sbyte.MinValue, 3, 8 };
                yield return new object[] { sbyte.MinValue, 8, 8 };

                yield return new object[] { 31, 0, 6 };
                yield return new object[] { 31, 1, 6 };
                yield return new object[] { 31, 5, 6 };
                yield return new object[] { 31, 8, 6 };

                yield return new object[] { -15, 0, 5 };
                yield return new object[] { -15, 2, 5 };
                yield return new object[] { -15, 6, 5 };
                yield return new object[] { -15, 8, 5 };
            }
        }

        [Theory]
        [MemberData(nameof(SignedByteReadingDataSource))]
        public void TestReadSignedByte(sbyte value, int bitOffset, int sizeInBit)
        {
            var valueStoredIn64Bitfields = ((ulong)value) << bitOffset;
            var buffer = BitConverter.GetBytes(valueStoredIn64Bitfields);

            var integer = CreateInteger(sizeInBit, isSigned: true);

            Assert.Equal(value, CtfInteger.ReadInt<sbyte>(integer, buffer, bitOffset));
        }

        public static IEnumerable<object[]> UnsignedShortReadingDataSource
        {
            get
            {
                yield return new object[] { ushort.MinValue, 0, sizeof(ushort) * 8 };

                yield return new object[] { ushort.MaxValue, 0, sizeof(ushort) * 8 };
                yield return new object[] { ushort.MaxValue, 3, sizeof(ushort) * 8 };
                yield return new object[] { ushort.MaxValue, 8, sizeof(ushort) * 8 };

                yield return new object[] { 2047, 0, 11 };
                yield return new object[] { 2047, 4, 11 };
                yield return new object[] { 2047, 16, 11 };
            }
        }

        [Theory]
        [MemberData(nameof(UnsignedShortReadingDataSource))]
        public void TestReadUnsignedShort(ushort value, int bitOffset, int sizeInBit)
        {
            var valueStoredIn64Bitfields = ((ulong)value) << bitOffset;
            var buffer = BitConverter.GetBytes(valueStoredIn64Bitfields);

            var integer = CreateInteger(sizeInBit, isSigned: false);

            Assert.Equal(value, CtfInteger.ReadInt<ushort>(integer, buffer, bitOffset));
        }

        public static IEnumerable<object[]> SignedShortReadingDataSource
        {
            get
            {
                yield return new object[] { short.MaxValue, 0, sizeof(short) * 8 };
                yield return new object[] { short.MaxValue, 3, sizeof(short) * 8 };
                yield return new object[] { short.MaxValue, 8, sizeof(short) * 8 };

                yield return new object[] { short.MinValue, 0, sizeof(short) * 8 };
                yield return new object[] { short.MinValue, 3, sizeof(short) * 8 };
                yield return new object[] { short.MinValue, 8, sizeof(short) * 8 };


                yield return new object[] { 2047, 0, 12 };
                yield return new object[] { 2047, 4, 12 };
                yield return new object[] { 2047, 16, 12 };

                yield return new object[] { -511, 0, 10 };
                yield return new object[] { -511, 2, 10 };
                yield return new object[] { -511, 5, 10 };
                yield return new object[] { -511, 6, 10 };
                yield return new object[] { -511, 7, 10 };
            }
        }

        [Theory]
        [MemberData(nameof(SignedShortReadingDataSource))]
        public void TestReadSignedShort(short value, int bitOffset, int sizeInBit)
        {
            var valueStoredIn64Bitfields = ((ulong)value) << bitOffset;
            var buffer = BitConverter.GetBytes(valueStoredIn64Bitfields);

            var integer = CreateInteger(sizeInBit, isSigned: true);

            Assert.Equal(value, CtfInteger.ReadInt<short>(integer, buffer, bitOffset));
        }

        public static IEnumerable<object[]> UnsignedIntReadingDataSource
        {
            get
            {
                yield return new object[] { uint.MinValue, 0, sizeof(uint) * 8 };

                yield return new object[] { uint.MaxValue, 0, sizeof(uint) * 8 };
                yield return new object[] { uint.MaxValue, 3, sizeof(uint) * 8 };
                yield return new object[] { uint.MaxValue, 8, sizeof(uint) * 8 };

                yield return new object[] { 2 ^ 23 - 1, 0, 23 };
                yield return new object[] { 2 ^ 23 - 1, 4, 23 };
                yield return new object[] { 2 ^ 23 - 1, 16, 23 };
            }
        }

        [Theory]
        [MemberData(nameof(UnsignedIntReadingDataSource))]
        public void TestReadUnsignedInt(uint value, int bitOffset, int sizeInBit)
        {
            var valueStoredIn64Bitfields = ((ulong)value) << bitOffset;
            var buffer = BitConverter.GetBytes(valueStoredIn64Bitfields);

            var integer = CreateInteger(sizeInBit, isSigned: false);

            Assert.Equal(value, CtfInteger.ReadInt<uint>(integer, buffer, bitOffset));
        }

        public static IEnumerable<object[]> SignedIntReadingDataSource
        {
            get
            {
                yield return new object[] { int.MaxValue, 0, sizeof(int) * 8 };
                yield return new object[] { int.MaxValue, 3, sizeof(int) * 8 };
                yield return new object[] { int.MaxValue, 8, sizeof(int) * 8 };

                yield return new object[] { int.MinValue, 0, sizeof(int) * 8 };
                yield return new object[] { int.MinValue, 3, sizeof(int) * 8 };
                yield return new object[] { int.MinValue, 8, sizeof(int) * 8 };


                yield return new object[] { 2 ^ 23 - 1, 0, 23 };
                yield return new object[] { 2 ^ 23 - 1, 4, 23 };
                yield return new object[] { 2 ^ 23 - 1, 16, 23 };

                yield return new object[] { -(2 ^ 27 - 1), 0, 28 };
                yield return new object[] { -(2 ^ 27 - 1), 2, 28 };
                yield return new object[] { -(2 ^ 27 - 1), 5, 28 };
                yield return new object[] { -(2 ^ 27 - 1), 6, 28 };
                yield return new object[] { -(2 ^ 27 - 1), 8, 28 };
            }
        }

        [Theory]
        [MemberData(nameof(SignedIntReadingDataSource))]
        public void TestReadSignedInt(int value, int bitOffset, int sizeInBit)
        {
            var valueStoredIn64Bitfields = ((ulong)value) << bitOffset;
            var buffer = BitConverter.GetBytes(valueStoredIn64Bitfields);

            var integer = CreateInteger(sizeInBit, isSigned: true);

            Assert.Equal(value, CtfInteger.ReadInt<int>(integer, buffer, bitOffset));
        }

        public static IEnumerable<object[]> UnsignedLongReadingDataSource
        {
            get
            {
                yield return new object[] { ulong.MinValue, 0, sizeof(ulong) * 8 };

                yield return new object[] { ulong.MaxValue, 0, sizeof(ulong) * 8 };

                yield return new object[] { 2 ^ 41 - 1, 0, 41 };
                yield return new object[] { 2 ^ 41 - 1, 4, 41 };
                yield return new object[] { 2 ^ 41 - 1, 16, 41 };
            }
        }

        [Theory]
        [MemberData(nameof(UnsignedLongReadingDataSource))]
        public void TestReadUnsignedLong(ulong value, int bitOffset, int sizeInBit)
        {
            var valueStoredIn64Bitfields = value << bitOffset;
            var buffer = BitConverter.GetBytes(valueStoredIn64Bitfields);

            var integer = CreateInteger(sizeInBit, isSigned: false);

            Assert.Equal(value, CtfInteger.ReadInt<ulong>(integer, buffer, bitOffset));
        }

        public static IEnumerable<object[]> SignedLongReadingDataSource
        {
            get
            {
                yield return new object[] { long.MaxValue, 0, sizeof(long) * 8 };

                yield return new object[] { long.MinValue, 0, sizeof(long) * 8 };


                yield return new object[] { 2 ^ 41 - 1, 0, 42 };
                yield return new object[] { 2 ^ 41 - 1, 4, 42 };
                yield return new object[] { 2 ^ 41 - 1, 16, 42 };

                yield return new object[] { -(2 ^ 55 - 1), 0, 56 };
                yield return new object[] { -(2 ^ 55 - 1), 2, 56 };
                yield return new object[] { -(2 ^ 55 - 1), 5, 56 };
                yield return new object[] { -(2 ^ 55 - 1), 6, 56 };
                yield return new object[] { -(2 ^ 55 - 1), 7, 56 };
            }
        }
        [Theory]
        [MemberData(nameof(SignedLongReadingDataSource))]
        public void TestReadSignedLong(long value, int bitOffset, int sizeInBit)
        {
            var valueStoredIn64Bitfields = value << bitOffset;
            var buffer = BitConverter.GetBytes(valueStoredIn64Bitfields);

            var integer = CreateInteger(sizeInBit, isSigned: true);

            Assert.Equal(value, CtfInteger.ReadInt<long>(integer, buffer, bitOffset));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void MustThrowIfSizeInBitsGreaterThan64Bits(bool isSigned)
        {
            Assert.Throws<NotImplementedException>(() => CreateInteger(74, isSigned));
        }

        [Fact]
        public void EnsureWeExtractOnlyTheBitsInvolvedInTheNumber()
        {
            ulong bitMask = 0xffffffffffffff05; // == expectedValue (0x41) starting at offset 2 and 1's all around
            const int offset = 2;
            const byte expectedValue = 0x41;

            var buffer = BitConverter.GetBytes(bitMask);
            var integer = CreateInteger(7, isSigned: false);

            Assert.Equal(expectedValue, CtfInteger.ReadInt<short>(integer, buffer, offset));
        }

        private static CtfInteger CreateInteger(int sizeInBits, bool isSigned)
        {
            var integerDefinition = new CtfPropertyBag();
            integerDefinition.AddValue("size", sizeInBits.ToString());
            integerDefinition.AddValue("signed", isSigned.ToString());
            return new CtfInteger(integerDefinition);
        }
    }
}
