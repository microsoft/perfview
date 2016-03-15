using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Tracing.Ctf
{
    sealed class CtfChannel : Stream
    {
        static readonly int s_headerSize = Marshal.SizeOf(typeof(CtfStreamHeader)) + Marshal.SizeOf(typeof(CtfPacketContext));
        private CtfMetadata _metadata;
        private CtfStream _ctfStream;
        private Stream _stream;
        private byte[] _buffer = new byte[Math.Max(128, CtfPacketContext.Size)];
        private GCHandle _handle;
        private long _packetSize;
        private long _contentSize;

#if DEBUG
        private long _fileOffset;

        public long FileOffset { get { return _fileOffset; } }
#endif

        public ulong StartTimestamp { get; private set; }
        public ulong EndTimestamp { get; private set; }

        public CtfStream CtfStream { get { return _ctfStream; } }

        public CtfChannel(Stream stream, CtfMetadata metadata)
        {
            _stream = stream;
            _metadata = metadata;
            _handle = GCHandle.Alloc(_buffer, GCHandleType.Pinned);

            ReadContext();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (_handle.IsAllocated)
                _handle.Free();
        }

        private bool ReadContext()
        {
            do
            {
                while (_packetSize > 0)
                {
                    // Zip filestream can't seek, we have to read the rest of the packet.

                    int curr = _buffer.Length < _packetSize ? _buffer.Length : (int)_packetSize;
                    _stream.Read(_buffer, 0, curr);
                    _packetSize -= curr;
#if DEBUG
                    _fileOffset += curr;
#endif
                }
                
                CtfStreamHeader header;
                CtfPacketContext context;

                if (!ReadStruct<CtfStreamHeader>(out header))
                    return false;

                Debug.Assert(header.Magic == 0xc1fc1fc1);
                _ctfStream = _metadata.Streams[(int)header.Stream];
                
                if (!ReadStruct<CtfPacketContext>(out context))
                    return false;

                _packetSize = (long)context.PacketSize / 8 - s_headerSize;
                _contentSize = (long)context.ContextSize / 8 - s_headerSize;
                StartTimestamp = context.TimestampBegin;
                EndTimestamp = context.TimestampEnd;
            } while (_contentSize == 0);

            return true;
        }

        private void ReadHeader()
        {
            int bytes = _metadata.Streams[0].EventHeader.GetSize();
        }

        private bool ReadStruct<T>(out T result) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T));
            int read = _stream.Read(_buffer, 0, size);

#if DEBUG
            _fileOffset += read;
#endif

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


        public override int ReadByte()
        {
            if (_contentSize == 0 && !ReadContext())
                return -1;

            _contentSize--;
            _packetSize--;
            int value = _stream.ReadByte();

#if DEBUG
            if (value != -1)
                _fileOffset++;
#endif

            Debug.Assert(value != -1);
            return value;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = 0;
            while (read < count)
            {
                if (_contentSize == 0 && !ReadContext())
                    break;

                int toRead = count > _contentSize ? (int)_contentSize : count;
                int curr = _stream.Read(buffer, offset + read, toRead);

                _contentSize -= curr;
                _packetSize -= curr;
                read += curr;

#if DEBUG
                _fileOffset += curr;
#endif

                if (curr != toRead)
                    break;
            }

            return read;
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

        public override long Position
        {
            get
            {
                throw new NotImplementedException();
            }

            set
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

        #region Helper Structs
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct CtfStreamHeader
        {
            public static int Size { get { return Marshal.SizeOf(typeof(CtfStreamHeader)); } }

            public uint Magic;
            public Guid Guid;
            public uint Stream;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct CtfPacketContext
        {
            public static int Size { get { return Marshal.SizeOf(typeof(CtfPacketContext)); } }

            public ulong TimestampBegin;
            public ulong TimestampEnd;
            public ulong ContextSize;
            public ulong PacketSize;
            public ulong EventsDiscarded;
            public uint CpuId;
        }
        #endregion
    }
}
