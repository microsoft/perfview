using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Tracing.Ctf
{
    class CtfEventHeader
    {
        public CtfEvent Event;
        public ulong Timestamp;
        public int Pid;
        public int Tid;
        public string ProcessName;

        public CtfEventHeader()
        {
        }

        public void Clear()
        {
            Event = null;
            Timestamp = 0;
            Pid = 0;
            Tid = 0;
            ProcessName = null;
        }
    }

    sealed class CtfReader : IDisposable
    {
        private Stream _stream;
        private byte[] _buffer = new byte[1024];
        private CtfMetadata _metadata;
        private CtfStream _streamDefinition;
        CtfEventHeader _header = new CtfEventHeader();

        private bool _eof;
        private GCHandle _handle;
        private int _bitOffset;
        private int _bufferLength;
        private bool _readHeader;

        public int BufferLength { get { return _bufferLength; } }
        public byte[] Buffer { get { return _buffer; } }
        public IntPtr BufferPtr { get { return _handle.AddrOfPinnedObject(); } }
        
        public CtfReader(Stream stream, CtfMetadata metadata, CtfStream ctfStream)
        {
            _handle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);
            _stream = stream;
            _metadata = metadata;
            _streamDefinition = ctfStream;

            ResetBuffer();
        }

        ~CtfReader()
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

            ulong lowMask = 0, highMask = 0, overflowBit = 0;
            ulong lastTimestamp = 0;

            StringBuilder processName = new StringBuilder();
            while (!_eof)
            {
                if (_readHeader)
                    throw new InvalidOperationException("Must read an events data before reading the header again.");

                _header.Clear();
                ResetBuffer();

                object[] result = ReadStruct(header);
                if (_eof)
                    break;

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
                    if (overflowBit == 0)
                    {
                        CtfInteger compactTimestamp = (CtfInteger)compact.GetField("timestamp").Type;
                        overflowBit = (1ul << compactTimestamp.Size);
                        lowMask = overflowBit - 1;
                        highMask = ~lowMask;
                    }

                    ulong uint27timestamp = compact.GetFieldValue<ulong>(result, "timestamp");
                    ulong prevLowerBits = lastTimestamp & lowMask;
                    
                    if (prevLowerBits < uint27timestamp)
                    {
                        timestamp = (lastTimestamp & highMask) | uint27timestamp;
                    }
                    else
                    {
                        timestamp = (lastTimestamp & highMask) | uint27timestamp;
                        timestamp += overflowBit;
                    }
                }

                lastTimestamp = timestamp;

                CtfEvent evt = _streamDefinition.Events[(int)event_id];
                _header.Event = evt;
                _header.Timestamp = timestamp;

                CtfStruct eventContext = _streamDefinition.EventContext;
                if (eventContext != null)
                {
                    result = ReadStruct(eventContext);
                    _header.Pid = eventContext.GetFieldValue<int>(result, "_vpid");
                    _header.Tid = eventContext.GetFieldValue<int>(result, "_vtid");

                    int procnameIndex = eventContext.GetFieldIndex("_procname");
                    object[] procname = (object[])(result[procnameIndex]);
                    processName.Clear();
                    for (int i = 0; i < 17; i++)
                    {
                        sbyte b = (sbyte)procname[i];
                        if (b == 0)
                            break;

                        processName.Append((char)b);
                    }

                    _header.ProcessName = processName.ToString();
                }

                _readHeader = true;
                yield return _header;
            }
        }

        public void ResetBuffer()
        {
            _bitOffset = 0;
            _bufferLength = 0;
        }
        

        public object[] ReadEvent(CtfEvent evt)
        {
            if (!_readHeader)
                throw new InvalidOperationException("Must read an event's header before reading an event's data.");

            object[] result = null;
            if (evt.IsPacked)
            {
                ReadPackedEvent();
            }
            else
            {
                ResetBuffer();
                result = ReadStruct(evt.Fields);
            }

            _readHeader = false;
            return result;
        }

        internal void ReadEventIntoBuffer(CtfEvent evt)
        {
            if (!_readHeader)
                throw new InvalidOperationException("Must read an event's header before reading an event's data.");

            if (evt.IsPacked)
            {
                ReadPackedEvent();
            }
            else
            {
                ResetBuffer();

                if (evt.IsFixedSize)
                    ReadBits(evt.Size);
                else
                    ReadStruct(evt.Fields);
            }

            _readHeader = false;
        }

        private void ReadPackedEvent()
        {
            Align(8);
            int offset = ReadBits(32) / 8;
            ReadBits(32);

            int len = BitConverter.ToInt32(_buffer, offset);

            ResetBuffer();
            ReadBits(8 * len);
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

            bool fastPath = (_bitOffset % 8) == 0 && (ctfInt.Size % 8) == 0;
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
            ulong value = 0;

            int byteLen = IntHelpers.AlignUp(bits, 8) / 8;

            for (int i = 0; i < byteLen; i++)
                value = unchecked((value << 8) | _buffer[byteOffset + byteLen - i - 1]);

            value >>= bitOffset;
            value &= (ulong)((1 << bits) - 1);

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


            if (count == 1)
            {
                int value = _stream.ReadByte();
                if (value != -1)
                    _buffer[_bufferLength++] = (byte)value;
                else
                    _eof = true;
            }
            else
            {

                int read = _stream.Read(_buffer, offset, count);
                if (read == count)
                    _bufferLength = offset + count;
                else
                    _eof = true;
            }
        }

        byte[] _stringBuffer = new byte[1024];
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
            _stringBuffer[i++] = b;
            if (ascii)
            {
                while (b != 0)
                {
                    if (i >= _stringBuffer.Length - 4)
                    {
                        byte[] tmp = _stringBuffer;
                        _stringBuffer = new byte[_stringBuffer.Length + 1024];
                        System.Buffer.BlockCopy(tmp, 0, _stringBuffer, 0, tmp.Length);
                    }

                    b = _stringBuffer[i++] = ReadByte();
                }
            }
            else
            {
                while (b != 0)
                {
                    if (i >= _stringBuffer.Length - 4)
                    {
                        byte[] tmp = _stringBuffer;
                        _stringBuffer = new byte[_stringBuffer.Length + 1024];
                        System.Buffer.BlockCopy(tmp, 0, _stringBuffer, 0, tmp.Length);
                    }

                    b >>= 4;

                    switch (b)
                    {
                        default:
                            break;

                        case 0xc:
                        case 0xd:
                            _stringBuffer[i++] = ReadByte();
                            break;

                        case 0xe:
                            _stringBuffer[i++] = ReadByte();
                            _stringBuffer[i++] = ReadByte();
                            break;

                        case 0xf:
                            _stringBuffer[i++] = ReadByte();
                            _stringBuffer[i++] = ReadByte();
                            _stringBuffer[i++] = ReadByte();
                            break;
                    }

                    b = _stringBuffer[i++] = ReadByte();
                }
            }

            Encoding encoding = ascii ? Encoding.ASCII : Encoding.UTF8;

            string s = encoding.GetString(_stringBuffer, 0, i - 1);
            byte[] newArr = Encoding.Convert(encoding, Encoding.Unicode, _stringBuffer, 0, i);

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
            int c = _stream.ReadByte();
            if (c == -1)
            {
                _eof = true;
                return 0;
            }

            return (byte)c;
        }
    }
}
