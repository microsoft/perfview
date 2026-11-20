using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Tracing.Ctf
{
    internal class CtfEventHeader
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

    internal sealed class CtfReader : IDisposable
    {
        private Stream _stream;
        private byte[] _buffer = new byte[1024];
        private CtfMetadata _metadata;
        private CtfStream _streamDefinition;
        private CtfEventHeader _header = new CtfEventHeader();

        private bool _eof;
        private GCHandle _handle;
        private int _bitOffset;
        private long _bitOffsetBase;
        private int _bufferLength;
        private int _bufferStartOffset;
        private bool _readHeader;

        public int BufferLength
        {
            get
            {
                if ((uint)_bufferStartOffset > (uint)_bufferLength || (uint)_bufferLength > (uint)_buffer.Length)
                    throw new InvalidDataException("Invalid CTF buffer extent.");
                return _bufferLength - _bufferStartOffset;
            }
        }
        public IntPtr BufferPtr
        {
            get
            {
                _ = BufferLength;
                return IntPtr.Add(_handle.AddrOfPinnedObject(), _bufferStartOffset);
            }
        }

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

        private byte[] ReallocateBuffer(int size)
        {
            Debug.Assert(_buffer.Length < size);

            if (size <= 0 || size > int.MaxValue - 7)
                throw new InvalidDataException("Invalid CTF buffer size.");
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

                if (_metadata.Environment.Domain == "kernel")
                {
                    pid = (CtfInteger)eventContext.GetField("_pid")?.Type;
                    tid = (CtfInteger)eventContext.GetField("_tid")?.Type;
                }
                else if (_metadata.Environment.Domain == "ust")
                {
                    pid = (CtfInteger)eventContext.GetField("_vpid")?.Type;
                    tid = (CtfInteger)eventContext.GetField("_vtid")?.Type;
                }
                else
                {
                    Debug.Fail("Other domains not supported.");
                }


                processName = (CtfArray)eventContext.GetField("_procname")?.Type;

                // We only handle ascii process names, which seems to be the only thing lttng provides.
                if (processName != null)
                {
                    processLen = int.Parse(processName.Index);
                    Debug.Assert(processName.Type.GetSize() == 8);

                    if (processName.Type.GetSize() != 8)
                    {
                        processName = null;
                    }
                }
            }


            uint extendedIdValue = (uint)id.GetValue("extended").End;

            ulong lowMask = 0, highMask = 0, overflowBit = 0;
            ulong lastTimestamp = 0;

            while (!_eof)
            {
                if (_readHeader)
                {
                    throw new InvalidOperationException("Must read an events data before reading the header again.");
                }

                _header.Clear();
                ResetBuffer();
                try
                {
                    ReadStruct(header);
                }
                catch (EndOfStreamException) when (_eof && _bufferLength == 0)
                {
                    break;
                }

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
                    {
                        _header.Pid = CtfInteger.ReadInt<int>(pid, _buffer, pid.BitOffset);
                    }

                    if (tid != null)
                    {
                        _header.Tid = CtfInteger.ReadInt<int>(tid, _buffer, tid.BitOffset);
                    }

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
                            {
                                matches = false;
                            }
                            else
                            {
                                matches &= lastProcessName[len] == _buffer[processNameOffset + len];
                            }
                        }

                        if (!matches || len != lastProcessName.Length)
                        {
                            lastProcessName = Encoding.UTF8.GetString(_buffer, processName.BitOffset >> 3, len);
                        }
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
            _bitOffsetBase = checked(_stream.Position * 8);
            _bufferLength = 0;
            _bufferStartOffset = 0;
        }

        internal void ReadEventIntoBuffer(CtfEvent evt)
        {
            if (!_readHeader)
            {
                throw new InvalidOperationException("Must read an event's header before reading an event's data.");
            }

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
            if (len < 0 || len > int.MaxValue / 8)
                throw new InvalidDataException("Invalid packed CTF payload length.");

            ResetBuffer();
            ReadBits(8 * len);
        }

        public void ReadStruct(CtfStruct strct)
        {
            var fields = strct.Fields;

            for (int i = 0; i < fields.Length; i++)
            {
                ReadTypeIntoBuffer(strct, fields[i].Type);
            }
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
                    if (len < 0)
                        throw new InvalidDataException("Negative CTF array length.");

                    int elemSize = array.Type.GetSize();
                    if (elemSize == CtfEvent.SizeIndeterminate)
                    {
                        for (int j = 0; j < len; j++)
                        {
                            ReadTypeIntoBuffer(null, array.Type);
                        }
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
                        {
                            ReadBits(8);
                        }
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

                    Encoding encoding = ascii ? Encoding.ASCII : Encoding.UTF8;

                    byte[] newArr = Encoding.Convert(encoding, Encoding.Unicode, _buffer, startOffset, bufferLen);
                    ((CtfString)type).Length = bufferLen;

                    if (_buffer.Length < _bufferLength + newArr.Length)
                    {
                        byte[] buffer = ReallocateBuffer(_bufferLength + newArr.Length);
                        System.Buffer.BlockCopy(buffer, 0, _buffer, 0, _bufferLength);
                    }

                    System.Buffer.BlockCopy(newArr, 0, _buffer, startOffset, newArr.Length);
                    _bufferLength = startOffset + newArr.Length;
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

        #region IDisposable
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _stream.Dispose();
            }

            if (_handle.IsAllocated)
            {
                _handle.Free();
            }
        }
        #endregion

        private void Align(int bits)
        {
            if (bits <= 0 || (bits & (bits - 1)) != 0)
                throw new InvalidDataException("Invalid CTF alignment.");

            if (bits == 1)
            {
                return;
            }

            long offset = checked(_bitOffsetBase + _bitOffset);
            int amount = (int)((-offset) & (bits - 1));
            if (amount != 0)
            {
                bool startsBuffer = _bitOffset == 0;
                ReadBits(amount);
                if (startsBuffer)
                {
                    _bufferStartOffset = amount / 8;
                }
            }
        }

        private int ReadBits(int bits)
        {
            int currentOffset = _bitOffset;
            if (bits < 0 || bits > int.MaxValue - currentOffset)
                throw new InvalidDataException("Invalid CTF field size.");
            int nextOffset = currentOffset + bits;
            int requiredBytes = (int)(((long)nextOffset + 7) / 8);
            if (requiredBytes > _bufferLength)
            {
                FillBuffer(requiredBytes - _bufferLength);
            }
            _bitOffset = nextOffset;
            return currentOffset;
        }

        private void FillBuffer(int count)
        {
            if (_bufferLength < 0 || count < 0 || count > int.MaxValue - _bufferLength)
                throw new InvalidDataException("Invalid CTF read extent.");

            if (count == 0)
            {
                return;
            }

            if (_eof)
                throw new EndOfStreamException("Truncated CTF event.");

            int required = _bufferLength + count;
            if (required > _buffer.Length)
            {
                int capacity = (int)Math.Min(int.MaxValue - 7L, Math.Max((long)required, (long)_buffer.Length * 3 / 2));
                byte[] buffer = ReallocateBuffer(capacity);
                System.Buffer.BlockCopy(buffer, 0, _buffer, 0, _bufferLength);
            }

            while (_bufferLength < required)
            {
                int remaining = required - _bufferLength;
                int read;
                if (remaining == 1)
                {
                    int value = _stream.ReadByte();
                    read = value < 0 ? 0 : 1;
                    if (read != 0)
                        _buffer[_bufferLength] = (byte)value;
                }
                else
                {
                    read = _stream.Read(_buffer, _bufferLength, remaining);
                }
                if (read == 0)
                {
                    _eof = true;
                    throw new EndOfStreamException("Truncated CTF event.");
                }
                _bufferLength += read;
            }
        }
    }
}
