using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Diagnostics.Tracing.Ctf.CtfMetadataTypes;

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
        private byte[] _buffer;
        private CtfMetadata _metadata;
        private CtfStream _streamDefinition;
        CtfEventHeader _header = new CtfEventHeader();

        private bool _eof;
        private GCHandle _handle;
        private int _bitOffset;
        private int _bufferLength;
        private const int OriginalBufferSize = 4096;
        private bool _readHeader;

        public int BufferLength { get { return _bufferLength; } }
        public IntPtr BufferPtr { get { return _handle.AddrOfPinnedObject(); } }
        
        public CtfReader(Stream stream, CtfMetadata metadata, CtfStream ctfStream)
        {
            _buffer = ArrayPool<byte>.Shared.Rent(OriginalBufferSize);
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

            _buffer = ArrayPool<byte>.Shared.Rent(size);

            _handle.Free();
            _handle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);

            return old;
        }

        public IEnumerable<CtfEventHeader> EnumerateEventHeaders()
        {
            CtfStruct header = _streamDefinition.EventHeader;
            CtfEnum id = (CtfEnum)header.GetField("id").Type;
            CtfVariant v = (CtfVariant)header.GetField("v").Type;
            CtfStruct extended = (CtfStruct)v.GetVariant("extended").Type;
            CtfInteger extendedId = (CtfInteger)extended.GetField("id").Type;
            CtfInteger extendedTimestamp = (CtfInteger)extended.GetField("timestamp").Type;
            CtfInteger compactTimestamp = (CtfInteger)((CtfStruct)v.GetVariant("compact").Type).GetField("timestamp").Type;
            
            CtfInteger pid = null;
            CtfInteger tid = null;
            CtfArray processName = null;
            string lastProcessName = "";
            int processLen = 0;
            CtfStruct eventContext = _streamDefinition.EventContext;
            if (eventContext != null)
            {
                pid = (CtfInteger)eventContext.GetField("_vpid")?.Type;
                tid = (CtfInteger)eventContext.GetField("_vtid")?.Type;
                processName = (CtfArray)eventContext.GetField("_procname")?.Type;

                // We only handle ascii process names, which seems to be the only thing lttng provides.
                if (processName != null)
                {
                    processLen = int.Parse(processName.Index);
                    Debug.Assert(processName.Type.GetSize() == 8);

                    if (processName.Type.GetSize() != 8)
                        processName = null;
                }
            }

            
            uint extendedIdValue = (uint)id.GetValue("extended").End;

            ulong lowMask = 0, highMask = 0, overflowBit = 0;
            ulong lastTimestamp = 0;
            
            while (!_eof)
            {
                if (_readHeader)
                    throw new InvalidOperationException("Must read an events data before reading the header again.");

                _header.Clear();
                ResetBuffer();
                ReadStruct(header);
                if (_eof)
                    break;
                

                ulong timestamp;
                uint event_id = CtfInteger.ReadInt<uint>(id.Type, _buffer, id.BitOffset);

                if (event_id == extendedIdValue)
                {
                    event_id = CtfInteger.ReadInt<uint>(extendedId, _buffer, extendedId.BitOffset);
                    timestamp = CtfInteger.ReadInt<ulong>(extendedTimestamp, _buffer, extendedTimestamp.BitOffset);
                }
                else
                {
                    if (overflowBit == 0)
                    {
                        overflowBit = (1ul << compactTimestamp.Size);
                        lowMask = overflowBit - 1;
                        highMask = ~lowMask;
                    }
                    
                    ulong uint27timestamp = CtfInteger.ReadInt<ulong>(compactTimestamp, _buffer, compactTimestamp.BitOffset);
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
                
                if (eventContext != null)
                {
                    ReadStruct(eventContext);

                    if (pid != null)
                        _header.Pid = CtfInteger.ReadInt<int>(pid, _buffer, pid.BitOffset);

                    if (tid != null)
                        _header.Tid = CtfInteger.ReadInt<int>(tid, _buffer, tid.BitOffset);

                    bool matches = true;
                    int processNameOffset = processName.BitOffset >> 3;

                    if (_buffer[processNameOffset] == 0)
                    {
                        lastProcessName = string.Empty;
                    }
                    else
                    {
                        int len = 0;
                        for (; len < processLen && _buffer[processNameOffset + len] != 0; len++)
                        {
                            if (len >= lastProcessName.Length)
                                matches = false;
                            else
                                matches &= lastProcessName[len] == _buffer[processNameOffset + len];
                        }

                        if (!matches || len != lastProcessName.Length)
                            lastProcessName = Encoding.UTF8.GetString(_buffer, processName.BitOffset >> 3, len);
                    }

                    _header.ProcessName = lastProcessName;
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
                ReadStruct(evt.Definition);
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
        
        public void ReadStruct(CtfStruct strct)
        {
            var fields = strct.Fields;

            for (int i = 0; i < fields.Length; i++)
                ReadTypeIntoBuffer(strct, fields[i].Type);
        }

        public void ReadTypeIntoBuffer(CtfStruct context, CtfMetadataType type)
        {
            Align(type.Align);

            type.BitOffset = _bitOffset;

            if (type.CtfType == CtfTypes.Enum)
            {
                type = ((CtfEnum)type).Type;
                type.BitOffset = _bitOffset;
            }
            else if (type.CtfType != CtfTypes.Struct && type.CtfType != CtfTypes.Variant)
            {
                int size = type.GetSize();
                if (size != CtfEvent.SizeIndeterminate)
                {
                    ReadBits(size);
                    return;
                }
            }

            switch (type.CtfType)
            {
                case CtfTypes.Array:
                    CtfArray array = (CtfArray)type;

                    var indexType = context.GetField(array.Index).Type;
                    int len = CtfInteger.ReadInt<int>(indexType, _buffer, indexType.BitOffset);

                    int elemSize = array.Type.GetSize();
                    if (elemSize == CtfEvent.SizeIndeterminate)
                    {
                        for (int j = 0; j < len; j++)
                            ReadTypeIntoBuffer(null, array.Type);
                    }
                    else
                    {
                        for (int j = 0; j < len; j++)
                        {
                            Align(type.Align);
                            ReadBits(elemSize);
                        }
                    }
                    break;

                case CtfTypes.Float:
                    CtfFloat flt = (CtfFloat)type;
                    ReadBits(flt.Exp + flt.Mant);
                    break;

                case CtfTypes.Integer:
                    CtfInteger ctfInt = (CtfInteger)type;
                    ReadBits(ctfInt.Size);
                    break;

                case CtfTypes.String:
                    Debug.Assert((_bitOffset % 8) == 0);
                    int startOffset = _bitOffset >> 3;
                    int offset = startOffset;

                    ReadBits(8);
                    bool ascii = ((CtfString)type).IsAscii;
                    if (ascii)
                    {
                        while (_buffer[offset++] != 0)
                            ReadBits(8);
                    }
                    else
                    {
                        byte b = _buffer[offset];
                        while (b != 0)
                        {
                            switch (b)
                            {
                                default:
                                    break;

                                case 0xc:
                                case 0xd:
                                    ReadBits(8);
                                    break;

                                case 0xe:
                                    ReadBits(16);
                                    break;

                                case 0xf:
                                    ReadBits(24);
                                    break;
                            }

                            offset = ReadBits(8) >> 3;
                            b = _buffer[offset];
                        }
                    }

                    int bufferLen = (_bitOffset >> 3) - startOffset;
                    ((CtfString)type).Length = bufferLen;

                    var encoding = ascii ? Encoding.ASCII : Encoding.UTF8;

                    var destByteCount = EncodeStringInUnicode(encoding, startOffset, bufferLen);

                    _bufferLength = startOffset + destByteCount;
                    _bitOffset = _bufferLength * 8;

                    break;


                case CtfTypes.Struct:
                    ReadStruct((CtfStruct)type);
                    break;

                case CtfTypes.Variant:
                    CtfVariant var = (CtfVariant)type;

                    CtfField field = context.GetField(var.Switch);
                    CtfEnum enumType = (CtfEnum)field.Type;

                    int value = CtfInteger.ReadInt<int>(enumType, _buffer, enumType.BitOffset);
                    string name = enumType.GetName(value);

                    field = var.GetVariant(name);
                    ReadTypeIntoBuffer(null, field.Type);
                    break;

                default:
                    throw new InvalidOperationException();
            }
        }
        private int EncodeStringInUnicode(Encoding orginalEncoding, int startOffset, int bufferLen)
        {
            var originalCount = orginalEncoding.GetCharCount(_buffer, startOffset, bufferLen);

            var encodingBuffer = ArrayPool<char>.Shared.Rent(originalCount);
            try
            {
                orginalEncoding.GetChars(_buffer, startOffset, bufferLen, encodingBuffer, 0);

                var destByteCount = Encoding.Unicode.GetByteCount(encodingBuffer, 0, originalCount);

                if (_buffer.Length < _bufferLength + destByteCount)
                {
                    byte[] buffer = ReallocateBuffer(_bufferLength + destByteCount);
                    System.Buffer.BlockCopy(buffer, 0, _buffer, 0, _bufferLength);
                    ArrayPool<byte>.Shared.Return(buffer);
                }

                System.Buffer.BlockCopy(encodingBuffer, 0, _buffer, startOffset, destByteCount);
                return destByteCount;
            }
            finally
            {
                ArrayPool<char>.Shared.Return(encodingBuffer);
            }
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
            {
                _stream.Dispose();
                ArrayPool<byte>.Shared.Return(_buffer);
            }

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
                ArrayPool<byte>.Shared.Return(buffer);
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
    }
}
