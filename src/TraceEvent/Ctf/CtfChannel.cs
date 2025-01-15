using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Tracing.Ctf
{
    internal sealed class CtfChannel : Stream
    {
        private CtfMetadata _metadata;
        private CtfStream _ctfStream;
        private Stream _stream;
        private long _position;
        private byte[] _buffer = new byte[256];
        private GCHandle _handle;
        private long _packetSize;
        private long _contentSize;

        public CtfStream CtfStream { get { return _ctfStream; } }

        public CtfChannel(Stream stream, CtfMetadata metadata)
        {
            _stream = stream;
            _position = 0;
            _metadata = metadata;
            _handle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);

            ReadContext();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (_handle.IsAllocated)
            {
                _handle.Free();
            }
        }

        private bool ReadContext()
        {
            do
            {
                while (_packetSize > 0)
                {
                    // Zip filestream can't seek, we have to read the rest of the packet.

                    int toRead = _buffer.Length <= _packetSize ? _buffer.Length : (int)_packetSize;
                    int read = _stream.Read(_buffer, 0, toRead);
                    _packetSize -= read;
                    _position += read;
                    if (read != toRead)
                    {
                        return false;
                    }
                }

                if (!ReadTraceHeader())
                {
                    return false;
                }

                if (!ReadPacketContext())
                {
                    return false;
                }
            } while (_contentSize == 0);

            return true;
        }

        private bool ReadTraceHeader()
        {
            // Read Trace Header
            CtfStruct traceHeader = _metadata.Trace.Header;

            int traceHeaderSize = traceHeader.GetSize();
            if (traceHeaderSize == CtfEvent.SizeIndeterminate)
            {
                throw new FormatException("Unexpected metadata format.");
            }

            int magicOffset = traceHeader.GetFieldOffset("magic");
            if (magicOffset < 0)
            {
                throw new FormatException("Unexpected metadata format: No magic field.");
            }

            int streamIdOffset = traceHeader.GetFieldOffset("stream_id");
            if (streamIdOffset < 0)
            {
                throw new FormatException("Unexpected metadata format: No stream_id field.");
            }

            // Convert to bytes instead of bits
            magicOffset /= 8;
            streamIdOffset /= 8;
            traceHeaderSize /= 8;

            if (TryReadExactlyCount(_buffer, 0, traceHeaderSize) != traceHeaderSize)
            {
                return false;
            }

            _position += traceHeaderSize;

            uint magic = BitConverter.ToUInt32(_buffer, magicOffset);
            if (magic != 0xc1fc1fc1)
            {
                throw new FormatException("Unknown magic number in trace header.");
            }

            uint streamId = BitConverter.ToUInt32(_buffer, streamIdOffset);
            _ctfStream = _metadata.Streams[streamId];

            return true;
        }

        private bool ReadPacketContext()
        {
            // Read Packet Context
            CtfStruct packetContext = _ctfStream.PacketContext;
            int packetContextSize = packetContext.GetSize();
            if (packetContextSize == CtfEvent.SizeIndeterminate)
            {
                throw new FormatException("Unexpected metadata format.");
            }

            int contentSizeOffset = packetContext.GetFieldOffset("content_size");
            if (contentSizeOffset < 0)
            {
                throw new FormatException("Unexpected metadata format: No context_size field.");
            }

            int packetSizeOffset = packetContext.GetFieldOffset("packet_size");
            if (packetSizeOffset < 0)
            {
                throw new FormatException("Unexpected metadata format: No packet_size field.");
            }

            // Convert to bytes instead of bits
            packetContextSize /= 8;
            contentSizeOffset /= 8;
            packetSizeOffset /= 8;

            if (TryReadExactlyCount(_buffer, 0, packetContextSize) != packetContextSize)
            {
                return false;
            }

            _position += packetContextSize;

            int headerSize = (_metadata.Trace.Header.GetSize() / 8) + packetContextSize;
            _contentSize = (long)BitConverter.ToUInt64(_buffer, contentSizeOffset) / 8 - headerSize;
            _packetSize = (long)BitConverter.ToUInt64(_buffer, packetSizeOffset) / 8 - headerSize;

            return true;
        }

        private void ReadHeader()
        {
            int bytes = _ctfStream.EventHeader.GetSize();
        }

        private bool ReadStruct<T>(out T result) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T));
            int read = TryReadExactlyCount(_buffer, 0, size);

            _position += read;

            if (size != read)
            {
                result = default(T);
                return false;
            }

            result = (T)Marshal.PtrToStructure(_handle.AddrOfPinnedObject(), typeof(T));
            return true;
        }

        public override bool CanRead
        {
            get
            {
                return true;
            }
        }

        public override long Position
        {
            get
            {
                return _position;
            }

            set
            {
                throw new NotSupportedException();
            }
        }

        public override int ReadByte()
        {
            if (_contentSize == 0 && !ReadContext())
            {
                return -1;
            }

            _contentSize--;
            _packetSize--;
            int value = _stream.ReadByte();
            if (value != -1)
                _position++;

            Debug.Assert(value != -1);
            return value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_contentSize == 0 && !ReadContext())
            {
                return 0;
            }

            int toRead = count > _contentSize ? (int)_contentSize : count;
            int read = TryReadExactlyCount(buffer, offset, toRead);

            _contentSize -= read;
            _packetSize -= read;
            _position += read;

            return read;
        }

        private int TryReadExactlyCount(byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                var read = _stream.Read(buffer, offset + totalRead, count - totalRead);
                if (read == 0)
                {
                    return totalRead;
                }
                totalRead += read;
            }
            return totalRead;
        }

        #region Not Implemented
        public override bool CanSeek
        {
            get
            {
                return false;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return false;
            }
        }

        public override long Length
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
