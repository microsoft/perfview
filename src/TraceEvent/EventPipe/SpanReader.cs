using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Buffers.Binary;
namespace Microsoft.Diagnostics.Tracing.EventPipe
{
    ref struct SpanReader
    {
        long _spanEndStreamOffset;
        ReadOnlySpan<byte> _buffer;

        public SpanReader(ReadOnlySpan<byte> buffer, long spanStartStreamOffset)
        {
            _buffer = buffer;
            _spanEndStreamOffset = spanStartStreamOffset + buffer.Length;
        }

        public ReadOnlySpan<byte> RemainingBytes => _buffer;
        public long StreamOffset => _spanEndStreamOffset - _buffer.Length;

        public sbyte ReadInt8() => Read<sbyte>();
        public byte ReadUInt8() => Read<byte>();

        public short ReadInt16()
        {
            short val;
            if (BinaryPrimitives.TryReadInt16LittleEndian(_buffer, out val))
            {
            	_buffer = _buffer.Slice(sizeof(short));
            }
            else
            {
            	ThrowFormatException<short>();
            }
            return val;
        }

        public ushort ReadUInt16()
        {
            ushort val;
            if (BinaryPrimitives.TryReadUInt16LittleEndian(_buffer, out val))
            {
                    _buffer = _buffer.Slice(sizeof(ushort));
            }
            else
            {
                    ThrowFormatException<ushort>();
            }
            return val;
        }
	
         public int ReadInt32()
         {
             int val;
             if (BinaryPrimitives.TryReadInt32LittleEndian(_buffer, out val))
             {
                 _buffer = _buffer.Slice(sizeof(int));
             }
             else
             {
                 ThrowFormatException<int>();
             }
             return val;
         }

         public uint ReadUInt32()
         {
              uint val;
              if (BinaryPrimitives.TryReadUInt32LittleEndian(_buffer, out val))
              {
                  _buffer = _buffer.Slice(sizeof(uint));
              }
              else
              {
                  ThrowFormatException<uint>();
              }
              return val;
         }

         public long ReadInt64()
         {
             long val;
             if (BinaryPrimitives.TryReadInt64LittleEndian(_buffer, out val))
             {
                 _buffer = _buffer.Slice(sizeof(long));
             }
             else
             {
                 ThrowFormatException<long>();
             }
             return val;
         }

         public ulong ReadUInt64()
         {
             ulong val;
             if (BinaryPrimitives.TryReadUInt64LittleEndian(_buffer, out val))
             {
                 _buffer = _buffer.Slice(sizeof(ulong));
             }
             else
             {
                 ThrowFormatException<ulong>();
             }
             return val;
         }

        public T Read<T>() where T : struct
        {
            T value;
            if (MemoryMarshal.TryRead(_buffer, out value))
            {
                _buffer = _buffer.Slice(Unsafe.SizeOf<T>());
            }
            else
            {
                ThrowFormatException<T>();
            }
            return value;
        }

        public ref readonly T ReadRef<T>() where T : struct
        {
            
            if (_buffer.Length >= Unsafe.SizeOf<T>())
            {
                ref readonly T ret = ref MemoryMarshal.Cast<byte, T>(_buffer)[0];
                _buffer = _buffer.Slice(Unsafe.SizeOf<T>());
                return ref ret;
            }
            else
            {
                ThrowFormatException<T>();
                throw new Exception(); // unreachable
            }
        }

        /// <summary>
        /// This reader skips ahead length bytes and returns a new reader that can read the skipped bytes
        /// </summary>
        public SpanReader CreateChildReader(int length)
        {
            long offset = StreamOffset;
            return new SpanReader(ReadBytes(length), offset);
        }

        /// <summary>
        /// Returns a pointer to the start of the span. This is only safe if you know the Span is backed by fixed memory.
        /// </summary>
        public unsafe IntPtr UnsafeGetFixedReadPointer()
        {
            return (IntPtr)Unsafe.AsPointer<byte>(ref MemoryMarshal.GetReference(RemainingBytes));
        }

        public ReadOnlySpan<byte> ReadBytes(int length)
        {
            if(_buffer.Length >= length)
            {
                ReadOnlySpan<byte> ret = _buffer.Slice(0,length);
                _buffer = _buffer.Slice(length);
                return ret;
            }
            else
            {
                ThrowFormatException(_buffer.Length, $"byte[{length}]");
                return default; // unreachable
            }
        }

        public uint ReadVarUInt32()
        {
            int initialLength = _buffer.Length;
            uint val = 0;
            int shift = 0;
            byte b;
            do
            {
                if (_buffer.Length == 0 || shift == 5 * 7)
                {
                    ThrowFormatException(initialLength, "VarUInt32");
                }
                b = _buffer[0];
                _buffer = _buffer.Slice(1);
                val |= (uint)(b & 0x7f) << shift;
                shift += 7;
            } while ((b & 0x80) != 0);
            return val;
        }

        public ulong ReadVarUInt64()
        {
            int initialLength = _buffer.Length;
            ulong val = 0;
            int shift = 0;
            byte b;
            do
            {
                if (_buffer.Length == 0 || shift == 10 * 7)
                {
                    ThrowFormatException(initialLength, "VarUInt64");
                }
                b = _buffer[0];
                _buffer = _buffer.Slice(1);
                val |= (ulong)(b & 0x7f) << shift;
                shift += 7;
            } while ((b & 0x80) != 0);
            return val;
        }

        public long ReadVarInt64()
        {
            ulong zigzag = ReadVarUInt64();
            return (long)(zigzag >> 1) ^ -(long)(zigzag & 1);
        }

        public string ReadVarUIntUTF8String()
        {
            int initialLength = _buffer.Length;
            int length = (int)ReadVarUInt32();
            if (length == 0)
            {
                return string.Empty;
            }
            if (_buffer.Length < length)
            {
                ThrowFormatException(initialLength, "VarUIntUTF8String");
            }
            byte[] textBytes = _buffer.Slice(0, length).ToArray();
            string result = Encoding.UTF8.GetString(textBytes);
            _buffer = _buffer.Slice(length);
            return result;
        }

        public string ReadNullTerminatedUTF16String()
        {
	  
            ReadOnlySpan<char> Buffer = MemoryMarshal.Cast<byte, char>(_buffer);
            Span<char> charBuffer = stackalloc char [Buffer.Length];
            Buffer.CopyTo(charBuffer);
            if (!BitConverter.IsLittleEndian)
            {
                for(int ii = 0; ii < charBuffer.Length; ii++)
                {
                    charBuffer[ii] = (char)BinaryPrimitives.ReverseEndianness((ushort)charBuffer[ii]);
                }
            }
            for(int i = 0; i < charBuffer.Length; i++)
            {
                if (charBuffer[i] == 0)
                {
                    string ret = new string(charBuffer.Slice(0, i).ToArray());
                    _buffer = _buffer.Slice((i + 1) * 2);
                    return ret;
                }
            }
            ThrowFormatException(_buffer.Length, "NullTerminatedUTF16String");
            return null; // unreachable
        }

        private void ThrowFormatException<T>()
        {
            ThrowFormatException(_buffer.Length, typeof(T).Name);
        }

        private void ThrowFormatException(int spanLength, string typeName)
        {
            long streamReadOffset = _spanEndStreamOffset - spanLength;
            throw new FormatException($"Failed to read {typeName} at stream offset 0x{streamReadOffset:x}");
        }
    }
}
