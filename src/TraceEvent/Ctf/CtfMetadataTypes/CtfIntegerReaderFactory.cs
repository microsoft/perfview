using System;

namespace Microsoft.Diagnostics.Tracing.Ctf.CtfMetadataTypes
{
    internal interface ICtfIntegerReader
    {
        TOut ReadAndConvert<TOut>(byte[] buffer, int bitOffset);
    }

    internal static class CtfIntegerReaderFactory
    {
        public static ICtfIntegerReader Create(CtfInteger integer)
        {
            if (integer.Size > 64)
                throw new NotImplementedException();

            if (integer.Size > 32)
            {
                if (integer.Signed)
                {
                    return new LongReader(integer.Size);
                }
                return new ULongReader(integer.Size);
            }

            if (integer.Size > 16)
            {
                if (integer.Signed)
                {
                    return new IntReader(integer.Size);
                }
                return new UIntReader(integer.Size);
            }

            if (integer.Size > 8)
            {
                if (integer.Signed)
                {
                    return new ShortReader(integer.Size);
                }
                return new UShortReader(integer.Size);
            }

            if (integer.Signed)
            {
                return new SByteReader(integer.Size);
            }
            return new ByteReader(integer.Size);
        }
    }

    internal abstract class AbstractIntegerReader<T> : ICtfIntegerReader where T : IConvertible
    {
        protected int Size { private get; set; }
        protected bool Signed { private get; set; }

        private static Func<T, long> ToLong => x => x.ToInt64(null);
        private static Func<T, ulong> ToULong => x => x.ToUInt64(null);
        private static Func<T, int> ToInt => x => x.ToInt32(null);
        private static Func<T, uint> ToUInt => x => x.ToUInt32(null);

        public TOut ReadAndConvert<TOut>(byte[] buffer, int bitOffset)
        {
            T result;
            if (IsReadAlign(bitOffset))
            {
                var byteOffset = bitOffset / 8;
                result = InternalRead(buffer, byteOffset);
            }
            else
            {
                result = Cast(ReadUnaligned(buffer, bitOffset));
            }

            return Convert<TOut>(result);
        }

        protected abstract T InternalRead(byte[] buffer, int byteOffset);
        protected abstract T Cast(ulong nb);

        private static TOut Convert<TOut>(T value)
        {
            if (typeof(TOut) == typeof(long))
            {
                var f = (Func<T, TOut>)(object)ToLong;
                return f(value);
            }

            if (typeof(TOut) == typeof(int))
            {
                var f = (Func<T, TOut>)(object)ToInt;
                return f(value);
            }

            if (typeof(TOut) == typeof(uint))
            {
                var f = (Func<T, TOut>)(object)ToUInt;
                return f(value);
            }

            if (typeof(TOut) == typeof(ulong))
            {
                var f = (Func<T, TOut>)(object)ToULong;
                return f(value);
            }
            return (TOut)value.ToType(typeof(TOut), null);
        }

        private ulong ReadUnaligned(byte[] buffer, int bitOffset)
        {
            int bits = Size;
            ulong value = 0;

            int byteLen = IntHelpers.AlignUp(bits + bitOffset, 8) / 8;

            for (int i = 0; i < byteLen; i++)
                value = unchecked((value << 8) | buffer[byteLen - i - 1]);

            value >>= bitOffset;
            value &= ((ulong)1 << bits) - 1;

            if (Signed)
            {
                ulong signBit = (1u << (bits - 1));

                if ((value & signBit) != 0)
                    value |= ulong.MaxValue << bits;
            }

            return value;
        }

        private bool IsReadAlign(int bitOffset)
        {
            return (bitOffset % 8 == 0) && (Size % 8 == 0);
        }
    }

    internal class LongReader : AbstractIntegerReader<long>
    {
        public LongReader(int size)
        {
            Size = size;
            Signed = true;
        }

        protected override long InternalRead(byte[] buffer, int byteOffset)
        {
            return BitConverter.ToInt64(buffer, byteOffset);
        }

        protected override long Cast(ulong nb)
        {
            return (long)nb;
        }
    }

    internal class ULongReader : AbstractIntegerReader<ulong>
    {
        public ULongReader(int size)
        {
            Size = size;
            Signed = false;
        }

        protected override ulong InternalRead(byte[] buffer, int byteOffset)
        {
            return BitConverter.ToUInt64(buffer, byteOffset);
        }

        protected override ulong Cast(ulong nb)
        {
            return nb;
        }
    }

    internal class IntReader : AbstractIntegerReader<int>
    {
        public IntReader(int size)
        {
            Size = size;
            Signed = true;
        }

        protected override int InternalRead(byte[] buffer, int byteOffset)
        {
            return BitConverter.ToInt32(buffer, byteOffset);
        }

        protected override int Cast(ulong nb)
        {
            return (int)nb;
        }
    }

    internal class UIntReader : AbstractIntegerReader<uint>
    {
        public UIntReader(int size)
        {
            Size = size;
            Signed = false;
        }

        protected override uint InternalRead(byte[] buffer, int byteOffset)
        {
            return BitConverter.ToUInt32(buffer, byteOffset);
        }

        protected override uint Cast(ulong nb)
        {
            return (uint)nb;
        }
    }

    internal class ShortReader : AbstractIntegerReader<short>
    {
        public ShortReader(int size)
        {
            Size = size;
            Signed = true;
        }

        protected override short InternalRead(byte[] buffer, int byteOffset)
        {
            return BitConverter.ToInt16(buffer, byteOffset);
        }

        protected override short Cast(ulong nb)
        {
            return (short)nb;
        }
    }

    internal class UShortReader : AbstractIntegerReader<ushort>
    {
        public UShortReader(int size)
        {
            Size = size;
            Signed = false;
        }

        protected override ushort InternalRead(byte[] buffer, int byteOffset)
        {
            return BitConverter.ToUInt16(buffer, byteOffset);
        }

        protected override ushort Cast(ulong nb)
        {
            return (ushort)nb;
        }
    }

    internal class SByteReader : AbstractIntegerReader<sbyte>
    {
        public SByteReader(int size)
        {
            Size = size;
            Signed = true;
        }

        protected override sbyte InternalRead(byte[] buffer, int byteOffset)
        {
            return (sbyte)buffer[byteOffset];
        }

        protected override sbyte Cast(ulong nb)
        {
            return (sbyte)nb;
        }
    }

    internal class ByteReader : AbstractIntegerReader<byte>
    {
        public ByteReader(int size)
        {
            Size = size;
            Signed = false;
        }

        protected override byte InternalRead(byte[] buffer, int byteOffset)
        {
            return buffer[byteOffset];
        }

        protected override byte Cast(ulong nb)
        {
            return (byte)nb;
        }
    }
}
