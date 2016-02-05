using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Tracing.Ctf
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    struct CtfPacketContext
    {
        public ulong TimestampBegin;
        public ulong TimestampEnd;
        public ulong ContextSize;
        public ulong PacketSize;
        public ulong EventsDiscarded;
        public uint CpuId;
    }

    struct CtfEventHeader
    {
        public CtfEvent Event;
        public ulong Timestamp;

        public CtfEventHeader(CtfEvent evt, ulong timestamp)
        {
            Event = evt;
            Timestamp = timestamp;
        }
    }

    sealed class CtfDataStream : IDisposable
    {
        private Stream _stream;
        private byte[] _buffer = new byte[1204];
        private long _fileOffset;
        private CtfMetadata _metadata;
        private CtfStream _streamDefinition;

        private long _contentSize = int.MinValue;
        private long _packetSize;
        private int _cpu;
        private bool _eof;
        private GCHandle _handle;
        private int _bitOffset;
        private int _bufferLength;

        public long Offset
        {
            get
            {
                return _fileOffset * 8;
            }
        }

        public int BufferLength { get { return _bufferLength; } }
        public byte[] Buffer { get { return _buffer; } }
        public IntPtr BufferPtr { get { return _handle.AddrOfPinnedObject(); } }

        public CtfDataStream(Stream stream, CtfMetadata metadata)
        {
            _handle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
            _stream = stream;
            _metadata = metadata;

            ReadHeader();
            //ReadPacketContext();
        }

        ~CtfDataStream()
        {
            Dispose(false);
        }

        byte[] ReallocateBuffer(int size)
        {
            Debug.Assert(_buffer.Length < size);

            // We'll make this a nice round number.
            size = IntHelpers.AlignUp(size, 8);

            byte[] old = _buffer;
            _buffer = new byte[size];

            _handle.Free();
            _handle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);

            return old;
        }

        public IEnumerable<CtfEventHeader> EnumerateEventHeaders()
        {
            CtfStruct header = _streamDefinition.EventHeader;
            CtfVariant v = (CtfVariant)header.GetField("v").Type;
            CtfStruct extended = (CtfStruct)v.GetVariant("extended").Type;
            CtfStruct compact = (CtfStruct)v.GetVariant("compact").Type;

            while (!_eof)
            {
                ResetBuffer();

                object[] result = ReadStruct(header);
                ulong timestamp;
                CtfEnum en = (CtfEnum)header.GetField("id").Type;
                uint event_id = header.GetFieldValue<uint>(result, "id");

                result = header.GetFieldValue<object[]>(result, "v");
                if (en.GetName((int)event_id) == "extended")
                {
                    event_id = extended.GetFieldValue<uint>(result, "id");
                    timestamp = extended.GetFieldValue<ulong>(result, "timestamp");
                }
                else
                {
                    timestamp = compact.GetFieldValue<ulong>(result, "timestamp");
                }
                
                CtfEvent evt = _streamDefinition.Events[(int)event_id];

                CtfStruct eventContext = _streamDefinition.EventContext;
                if (eventContext != null)
                    result = ReadStruct(eventContext);


                yield return new CtfEventHeader(evt, timestamp);
            }
        }

        private void ResetBuffer()
        {
            _bitOffset = 0;
            _bufferLength = 0;
        }

        private void ReadPacketContext()
        {
            CtfStruct packetContext = _streamDefinition.PacketContext;

            bool matchesDefined = true;
            if (matchesDefined)
            {
                CtfPacketContext context = new CtfPacketContext();
                ReadStruct(ref context, packetContext.Align);

                _packetSize = (long)context.PacketSize;
                _contentSize = (long)context.ContextSize;
                _cpu = (int)context.CpuId;
            }
            else
            {
                object[] result = ReadStruct(packetContext);

                _packetSize = packetContext.GetFieldValue<long>(result, "packet_size");
                _contentSize = packetContext.GetFieldValue<long>(result, "content_size");
                _cpu = packetContext.GetFieldValue<int>(result, "cpu_id");
            }
        }

        private void ReadStruct<T>(ref T context, int align) where T : struct
        {
            //TODO: Respect align
            int size = Marshal.SizeOf(typeof(T));

            if (size > _buffer.Length)
                ReallocateBuffer(size);

            _stream.Read(_buffer, 0, size);
            _fileOffset += size;

            context = (T)Marshal.PtrToStructure(_handle.AddrOfPinnedObject(), typeof(T));
        }

        private void ReadHeader()
        {
            CtfStruct traceHeader = _metadata.Trace.Header;

            object[] result = ReadStruct(traceHeader);
            int stream_id = traceHeader.GetFieldValue<int>(result, "stream_id");

            _streamDefinition = _metadata.Streams[stream_id];
            _contentSize = 0;
        }
        
        
        public object[] ReadEvent(CtfEvent evt)
        {
            ResetBuffer();
            return ReadStruct(evt.Fields);
        }

        internal void ReadEventIntoBuffer(CtfEvent evt)
        {
            ResetBuffer();

            if (evt.IsFixedSize)
                ReadBits(evt.Size);
            else
                ReadEvent(evt);
        }

        public object[] ReadStruct(CtfStruct strct)
        {
            var fields = strct.Fields;

            object[] result = new object[fields.Length];

            for (int i = 0; i < fields.Length; i++)
                result[i] = ReadType(strct, result, fields[i].Type);

            return result;
        }

        private object ReadType(CtfStruct strct, object[] result, CtfMetadataType type)
        {
            Align(type.Align);

            switch (type.CtfType)
            {
                case CtfTypes.Array:
                    CtfArray array = (CtfArray)type;
                    int len = array.GetLength(strct, result);

                    object[] ret = new object[len];

                    for (int j = 0; j < len; j++)
                        ret[j] = ReadType(null, null, array.Type);

                    return ret;

                case CtfTypes.Enum:
                    return ReadType(strct, result, ((CtfEnum)type).Type);

                case CtfTypes.Float:
                    CtfFloat flt = (CtfFloat)type;
                    ReadBits(flt.Exp + flt.Mant);
                    return 0f;  // TODO:  Not implemented.
                    
                case CtfTypes.Integer:
                    CtfInteger ctfInt = (CtfInteger)type;
                    return ReadInteger(ctfInt);
                    
                case CtfTypes.String:
                    bool ascii = ((CtfString)type).IsAscii;
                    return ReadString(ascii);

                case CtfTypes.Struct:
                    return ReadStruct((CtfStruct)type);

                case CtfTypes.Variant:
                    CtfVariant var = (CtfVariant)type;

                    int i = strct.GetFieldIndex(var.Switch);
                    CtfField field = strct.Fields[i];
                    CtfEnum enumType = (CtfEnum)field.Type;

                    int value = strct.GetFieldValue<int>(result, i);
                    string name = enumType.GetName(value);

                    field = var.Union.Where(f => f.Name == name).Single();
                    return ReadType(strct, result, field.Type);

                default:
                    throw new InvalidOperationException();
            }
        }

        private object ReadInteger(CtfInteger ctfInt)
        {
            if (ctfInt.Size > 64)
                throw new NotImplementedException();

            Align(ctfInt.Align);
            int bitOffset = ReadBits(ctfInt.Size);
            int byteOffset = bitOffset / 8;

            bool fastPath = (_bitOffset % 8) == 0  && (ctfInt.Size % 8) == 0;
            if (fastPath)
            {
                if (ctfInt.Size == 32)
                {
                    if (ctfInt.Signed)
                        return BitConverter.ToInt32(_buffer, byteOffset);

                    return BitConverter.ToUInt32(_buffer, byteOffset);
                }

                if (ctfInt.Size == 8)
                {
                    if (ctfInt.Signed)
                        return (sbyte)_buffer[byteOffset];

                    return _buffer[byteOffset];
                }

                if (ctfInt.Size == 64)
                {
                    if (ctfInt.Signed)
                        return BitConverter.ToInt64(_buffer, byteOffset);

                    return BitConverter.ToUInt64(_buffer, byteOffset);
                }

                Debug.Assert(ctfInt.Size == 16);
                if (ctfInt.Signed)
                    return BitConverter.ToInt16(_buffer, byteOffset);

                return BitConverter.ToUInt16(_buffer, byteOffset);
            }

            // Sloooow path for misaligned integers
            int bits = ctfInt.Size;
            int end = bitOffset + bits;
            int leading= end - IntHelpers.AlignDown(end, 8);
            int trailing = IntHelpers.AlignUp(bitOffset, 8) - bitOffset;

            ulong value = 0;
            if (trailing > 0)
            {
                byte endByte = _buffer[IntHelpers.AlignDown(trailing, 8) / 8];

                if (trailing < bits)
                    trailing = bits;

                value = (ulong)(endByte >> (8 - trailing));
            }

            Debug.Assert((bits - trailing - leading) % 8 == 0);

            int len = (bits - trailing - leading) / 8;
            for (int i = 0; i < len / 8; i++)
                value = unchecked((value << 8) | _buffer[len - i - 1]);

            if (leading != 0)
            {
                byte leadingByte = _buffer[IntHelpers.AlignDown(bitOffset, 8) / 8];
                value = (value << leading) | (uint)(leadingByte & ((1 << leading) - 1));
            }

            if (ctfInt.Signed)
            {
                ulong signBit = (1u << (bits - 1));

                if ((value & signBit) != 0)
                    value |= ulong.MaxValue << bits;
            }


            if (ctfInt.Size > 32)
            {
                if (ctfInt.Signed)
                    return (long)value;

                return value;
            }

            if (ctfInt.Size > 16)
            {
                if (ctfInt.Signed)
                    return (int)value;

                return (uint)value;
            }

            if (ctfInt.Size > 8)
            {
                if (ctfInt.Signed)
                    return (short)value;

                return (ushort)value;
            }

            if (ctfInt.Signed)
                return (sbyte)value;

            return (byte)value;
        }


        #region IDisposable
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing)
        {
            if (disposing)
                _stream.Dispose();

            if (_handle.IsAllocated)
                _handle.Free();
        }
        #endregion
        
        private void Align(int bits)
        {
            Debug.Assert(bits > 0);

            if (bits == 1)
                return;

            int amount = (int)(IntHelpers.AlignUp(_bitOffset, bits) - _bitOffset);
            if (amount != 0)
                ReadBits(amount);
        }

        private int ReadBits(int bits)
        {
            int currentOffset = _bitOffset;
            _bitOffset += bits;

            int bufferLength = _bufferLength;
            int bitLength = bufferLength * 8;
            if (_bitOffset > bitLength)
            {
                int newBufferLength = IntHelpers.AlignUp(_bitOffset, 8) / 8;

                FillBuffer(bufferLength, newBufferLength - bufferLength);
            }
            
            return currentOffset;
        }

        private void FillBuffer(int offset, int count)
        {
            Debug.Assert(count >= 0);

            if (_eof)
                return;

            if (count == 0)
                return;

            if (offset + count > _buffer.Length)
            {
                byte[] buffer = ReallocateBuffer((int)((offset + count) * 1.5));
                System.Buffer.BlockCopy(buffer, 0, _buffer, 0, offset);
            }

            CheckPacketContext();

            int read = _stream.Read(_buffer, offset, count);
            if (read == count)
            {
                _bufferLength = offset + count;
                _fileOffset += count;

                if (_contentSize != int.MinValue)
                {
                    _contentSize -= read;
                    _packetSize -= read;
                }
            }
            else
            {
                _eof = true;
                _contentSize = 0;
            }
        }

        private void CheckPacketContext()
        {
            if (_contentSize == int.MinValue || _contentSize > 0)
                return;

            Debug.Assert(_contentSize >= -1);
            if (_packetSize > 0)
            {
                byte[] tmp = new byte[(int)_packetSize];
                int tmpread = _stream.Read(tmp, 0, tmp.Length);
                if (tmpread != tmp.Length)
                    _eof = true;
            }

            _contentSize = int.MinValue;  // Ensure we don't recurse
            ReadPacketContext();
        }
        

        byte[] _bytes = new byte[1024];
        internal string ReadString(bool ascii)
        {
            byte b = ReadByte();
            if (b == 0)
            {
                if (_buffer.Length < _bufferLength + 2)
                {
                    byte[] buffer = ReallocateBuffer(_bufferLength + 2);
                    System.Buffer.BlockCopy(buffer, 0, _buffer, 0, _bufferLength);
                }

                _buffer[_bufferLength++] = 0;
                _buffer[_bufferLength++] = 0;
                _bitOffset += 16;

                return "";
            }

            int i = 0;
            _bytes[i++] = b;
            if (ascii)
            {
                while (b != 0)
                {
                    if (i >= _bytes.Length - 4)
                    {
                        byte[] tmp = _bytes;
                        _bytes = new byte[_bytes.Length + 1024];
                        System.Buffer.BlockCopy(tmp, 0, _bytes, 0, tmp.Length);
                    }

                    b = _bytes[i++] = ReadByte();
                }
            }
            else
            {
                while (b != 0)
                {
                    if (i >= _bytes.Length - 4)
                    {
                        byte[] tmp = _bytes;
                        _bytes = new byte[_bytes.Length + 1024];
                        System.Buffer.BlockCopy(tmp, 0, _bytes, 0, tmp.Length);
                    }

                    b >>= 4;

                    switch (b)
                    {
                        default:
                            break;

                        case 0xc:
                        case 0xd:
                            _bytes[i++] = ReadByte();
                            break;

                        case 0xe:
                            _bytes[i++] = ReadByte();
                            _bytes[i++] = ReadByte();
                            break;

                        case 0xf:
                            _bytes[i++] = ReadByte();
                            _bytes[i++] = ReadByte();
                            _bytes[i++] = ReadByte();
                            break;
                    }

                    b = _bytes[i++] = ReadByte();
                }
            }

            Encoding encoding = ascii ? Encoding.ASCII : Encoding.UTF8;

            string s = encoding.GetString(_bytes, 0, i - 1);
            byte[] newArr = Encoding.Convert(encoding, Encoding.Unicode, _bytes, 0, i);

            if (_buffer.Length < _bufferLength + newArr.Length)
            {
                byte[] buffer = ReallocateBuffer(_bufferLength + newArr.Length);
                System.Buffer.BlockCopy(buffer, 0, _buffer, 0, _bufferLength);
            }

            System.Buffer.BlockCopy(newArr, 0, _buffer, _bufferLength, newArr.Length);
            _bufferLength += newArr.Length;
            _bitOffset += newArr.Length * 8;

            return s;
        }

        private byte ReadByte()
        {
            CheckPacketContext();

            int c = _stream.ReadByte();
            if (c == -1)
            {
                _eof = true;
                return 0;
            }

            _fileOffset++;

            if (_contentSize != int.MinValue)
            {
                _packetSize--;
                _contentSize--;
            }

            return (byte)c;
        }
    }
}
