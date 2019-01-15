//     Copyright (c) Microsoft Corporation.  All rights reserved.
// This file is best viewed using outline mode (Ctrl-M Ctrl-O)
//
// This program uses code hyperlinks available as part of the HyperAddin Visual Studio plug-in.
// It is available from http://www.codeplex.com/hyperAddin 
// 
using System;
using System.Diagnostics;
using System.IO;
using System.Text;      // For StringBuilder.
using System.Threading;
using System.IO.MemoryMappedFiles;
using DeferedStreamLabel = FastSerialization.StreamLabel;

#if NETSTANDARD1_3
using System.Runtime.InteropServices;
#else
using System.Runtime.CompilerServices;
#endif

namespace FastSerialization
{
    /// <summary>
    /// A MemoryStreamReader is an implementation of the IStreamReader interface that works over a given byte[] array.  
    /// </summary>
#if STREAMREADER_PUBLIC
    public
#endif
    class MemoryStreamReader : IStreamReader
    {
        /// <summary>
        /// Create a IStreamReader (reads binary data) from a given byte buffer
        /// </summary>
        public MemoryStreamReader(byte[] data) : this(data, 0, data.Length) { }
        /// <summary>
        /// Create a IStreamReader (reads binary data) from a given subregion of a byte buffer 
        /// </summary>
        public MemoryStreamReader(byte[] data, int start, int length)
        {
            bytes = data;
            position = start;
            endPosition = length;
        }

        /// <summary>
        /// The total length of bytes that this reader can read.  
        /// </summary>
        public virtual long Length { get { return endPosition; } }

        #region implemenation of IStreamReader
        /// <summary>
        /// Implementation of IStreamReader
        /// </summary>
        public byte ReadByte()
        {
            if (position >= endPosition)
            {
                Fill(1);
            }

            return bytes[position++];
        }
        /// <summary>
        /// Implementation of IStreamReader
        /// </summary>
        public short ReadInt16()
        {
            if (position + sizeof(short) > endPosition)
            {
                Fill(sizeof(short));
            }

            int ret = bytes[position] + (bytes[position + 1] << 8);
            position += sizeof(short);
            return unchecked((short)ret);
        }
        /// <summary>
        /// Implementation of IStreamReader
        /// </summary>
        public int ReadInt32()
        {
            if (position + sizeof(int) > endPosition)
            {
                Fill(sizeof(int));
            }

            int ret = bytes[position] + ((bytes[position + 1] + ((bytes[position + 2] + (bytes[position + 3] << 8)) << 8)) << 8);
            position += sizeof(int);
            return ret;
        }
        /// <summary>
        /// Implementation of IStreamReader
        /// </summary>
        public long ReadInt64()
        {
            uint low = unchecked((uint)ReadInt32());
            uint high = unchecked((uint)ReadInt32());
            return unchecked(((long)high << 32) + low);        // TODO find the most efficient way of doing this. 
        }
        /// <summary>
        /// Implementation of IStreamReader
        /// </summary>
        public string ReadString()
        {
            int len = ReadInt32();          // Expect first a character inclusiveCountRet.  -1 means null.
            if (len < 0)
            {
                Debug.Assert(len == -1);
                return null;
            }

            if (sb == null)
            {
                sb = new StringBuilder(len);
            }

            sb.Length = 0;

            Debug.Assert(len < Length);
            while (len > 0)
            {
                int b = ReadByte();
                if (b < 0x80)
                {
                    sb.Append((char)b);
                }
                else if (b < 0xE0)
                {
                    // TODO test this for correctness
                    b = (b & 0x1F);
                    b = b << 6 | (ReadByte() & 0x3F);
                    sb.Append((char)b);
                }
                else
                {
                    // TODO test this for correctness
                    b = (b & 0xF);
                    b = b << 6 | (ReadByte() & 0x3F);
                    b = b << 6 | (ReadByte() & 0x3F);

                    sb.Append((char)b);
                }
                --len;
            }
            return sb.ToString();
        }
        /// <summary>
        /// Implementation of IStreamReader
        /// </summary>
        public StreamLabel ReadLabel()
        {
            return (StreamLabel)ReadInt32();
        }
        /// <summary>
        /// Implementation of IStreamReader
        /// </summary>
        public virtual void Goto(StreamLabel label)
        {
            Debug.Assert(label != StreamLabel.Invalid);
            position = (int)label;
        }
        /// <summary>
        /// Implementation of IStreamReader
        /// </summary>
        public virtual StreamLabel Current
        {
            get
            {
                return (StreamLabel)position;
            }
        }
        /// <summary>
        /// Implementation of IStreamReader
        /// </summary>
        public virtual void GotoSuffixLabel()
        {
            Goto((StreamLabel)(Length - sizeof(StreamLabel)));
            Goto(ReadLabel());
        }
        /// <summary>
        /// Dispose pattern
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        /// <summary>
        /// Dispose pattern
        /// </summary>
        protected virtual void Dispose(bool disposing) { }
        #endregion

        #region private 
        internal /*protected*/ virtual void Fill(int minBytes)
        {
            throw new Exception("Streamreader read past end of buffer");
        }
        internal /*protected*/  byte[] bytes;
        internal /*protected*/  int position;
        internal /*protected*/  int endPosition;
        private StringBuilder sb;
        #endregion
    }

    // TODO is unsafe code worth it?
#if true
    /// <summary>
    /// A StreamWriter is an implementation of the IStreamWriter interface that generates a byte[] array. 
    /// </summary>
#if STREAMREADER_PUBLIC
    public
#endif
    class MemoryStreamWriter : IStreamWriter
    {
        /// <summary>
        /// Create IStreamWriter that writes its data to an internal byte[] buffer.  It will grow as needed. 
        /// Call 'GetReader' to get a IStreamReader for the written bytes. 
        /// 
        /// Call 'GetBytes' call to get the raw array.  Only the first 'Length' bytes are valid
        /// </summary>
        public MemoryStreamWriter(int initialSize = 64)
        {
            bytes = new byte[initialSize];
        }

        /// <summary>
        /// Returns a IStreamReader that will read the written bytes.  You cannot write additional bytes to the stream after making this call. 
        /// </summary>
        /// <returns></returns>
        public virtual MemoryStreamReader GetReader()
        {
            var readerBytes = bytes;
            if (bytes.Length - endPosition > 500000)
            {
                readerBytes = new byte[endPosition];
                Array.Copy(bytes, readerBytes, endPosition);
            }
            return new MemoryStreamReader(readerBytes, 0, endPosition);
        }
        /// <summary>
        /// The number of bytes written so far.  
        /// </summary>
        public virtual long Length { get { return endPosition; } }
        /// <summary>
        /// The the array that holds the serialized data.   
        /// </summary>
        /// <returns></returns>
        public virtual byte[] GetBytes() { return bytes; }

        /// <summary>
        /// Clears any data that was previously written.  
        /// </summary>
        public virtual void Clear() { endPosition = 0; }

        #region Implementation of IStreamWriter
        /// <summary>
        /// Implementation of IStreamWriter
        /// </summary>
        public void Write(byte value)
        {
            if (endPosition >= bytes.Length)
            {
                MakeSpace();
            }

            bytes[endPosition++] = value;
        }
        /// <summary>
        /// Implementation of IStreamWriter
        /// </summary>
        public unsafe void Write(short value)
        {
            if (endPosition + sizeof(short) > bytes.Length)
            {
                MakeSpace();
            }

            fixed (byte* data = bytes)
            {
                *(short*)(data + endPosition) = value;
            }

            endPosition += sizeof(short);
        }
        /// <summary>
        /// Implementation of IStreamWriter
        /// </summary>
        public unsafe void Write(int value)
        {
            if (endPosition + sizeof(int) > bytes.Length)
            {
                MakeSpace();
            }

            fixed (byte* data = bytes)
            {
                *(int*)(data + endPosition) = value;
            }

            endPosition += sizeof(int);
        }
        /// <summary>
        /// Implementation of IStreamWriter
        /// </summary>
        public unsafe void Write(long value)
        {
            if (endPosition + sizeof(long) > bytes.Length)
            {
                MakeSpace();
            }

            fixed (byte* data = bytes)
            {
                *(long*)(data + endPosition) = value;
            }

            endPosition += sizeof(long);
        }
        /// <summary>
        /// Implementation of IStreamWriter
        /// </summary>
        public void Write(StreamLabel value)
        {
            Write((int)value);
        }
        /// <summary>
        /// Implementation of IStreamWriter
        /// </summary>
        public void Write(string value)
        {
            if (value == null)
            {
                Write(-1);          // negative charCount means null. 
            }
            else
            {
                Write(value.Length);
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    if (c <= 0x7F)
                    {
                        Write((byte)value[i]);                 // Only need one byte for UTF8
                    }
                    else if (c <= 0x7FF)
                    {
                        // TODO confirm that this is correct!
                        Write((byte)(0xC0 | (c >> 6)));                // Encode 2 byte UTF8
                        Write((byte)(0x80 | (c & 0x3F)));
                    }
                    else
                    {
                        // TODO confirm that this is correct!
                        Write((byte)(0xE0 | ((c >> 12) & 0xF)));        // Encode 3 byte UTF8
                        Write((byte)(0x80 | ((c >> 6) & 0x3F)));
                        Write((byte)(0x80 | (c & 0x3F)));
                    }
                }
            }
        }
        /// <summary>
        /// Implementation of IStreamWriter
        /// </summary>
        public virtual StreamLabel GetLabel()
        {
            return (StreamLabel)Length;
        }
        /// <summary>
        /// Implementation of IStreamWriter
        /// </summary>
        public void WriteSuffixLabel(StreamLabel value)
        {
            // This is guaranteed to be uncompressed, but since we are not compressing anything, we can
            // simply write the value.  
            Write(value);
        }

        /// <summary>
        /// Dispose pattern
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        /// <summary>
        /// Dispose pattern
        /// </summary>
        protected virtual void Dispose(bool disposing) { }
        #endregion 
        #region private

        /// <summary>
        /// Makespace makes at least sizeof(long) bytes available (or throws OutOfMemory)
        /// </summary>
        internal /* protected */ virtual void MakeSpace()
        {
            const int maxLength = 0x7FFFFFC7; // Max array length for byte[]
            int newLength = 0;

            // Make sure we don't exceed max possible size
            if (bytes.Length < (maxLength / 3) * 2)
            {
                newLength = (bytes.Length / 2) * 3;
            }
            else if (bytes.Length < maxLength - sizeof(long))      // Write(long) expects Makespace to make at 8 bytes of space.   
            {
                newLength = maxLength;                             // If we can do this, use up the last available length 
            }
            else
            {
                throw new OutOfMemoryException();                  // Can't make space anymore
            }

            Debug.Assert(bytes.Length + sizeof(long) <= newLength);
            byte[] newBytes = new byte[newLength];
            Array.Copy(bytes, newBytes, bytes.Length);
            bytes = newBytes;
        }
        internal /* protected */ byte[] bytes;
        internal /* protected */ int endPosition;
        #endregion
    }
#else
    /// <summary>
    /// A StreamWriter is an implementation of the IStreamWriter interface that generates a byte[] array. 
    /// </summary>
    unsafe class MemoryStreamWriter : IStreamWriter
    {
        public MemoryStreamWriter() : this(64) { }
        public MemoryStreamWriter(int size)
        {
            bytes = new byte[size];
            pinningHandle = System.Runtime.InteropServices.GCHandle.Alloc(bytes, System.Runtime.InteropServices.GCHandleType.Pinned);
            fixed (byte* bytesAsPtr = &bytes[0])
                bufferStart = bytesAsPtr;
            bufferCur = bufferStart;
            bufferEnd = &bufferStart[bytes.Length];
        }

        // TODO 
        public virtual long Length { get { return (int) (bufferCur - bufferStart); } }
        public virtual void Clear() { throw new Exception(); }

        public void Write(byte value)
        {
            if (bufferCur + sizeof(byte) > bufferEnd)
                DoMakeSpace();
            *((byte*)(bufferCur)) = value;
            bufferCur += sizeof(byte);
        }
        public void Write(short value)
        {
            if (bufferCur + sizeof(short) > bufferEnd)
                DoMakeSpace();
            *((short*)(bufferCur)) = value;
            bufferCur += sizeof(short);
        }
        public void Write(int value)
        {
            if (bufferCur + sizeof(int) <= bufferEnd)
            {
                *((int*)(bufferCur)) = value;
                bufferCur += sizeof(int);
            }
            else 
                WriteSlow(value);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void WriteSlow(int value)
        {
            DoMakeSpace();
            Debug.Assert(bufferCur + 8 < bufferEnd);
            Write(value);
        }

        public void Write(long value)
        {
            if (bufferCur + sizeof(long) > bufferEnd)
                DoMakeSpace();
            *((long*)(bufferCur)) = value;
            bufferCur += sizeof(long);
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private void DoMakeSpace()
        {
            endPosition = (int)(bufferCur - bufferStart);
            MakeSpace();
            bufferCur = &bufferStart[endPosition];
            bufferEnd = &bufferStart[bytes.Length];
        }

        public void Write(StreamLabel value)
        {
            Write((int)value);
        }
        public void Write(string value)
        {
            if (value == null)
            {
                Write(-1);          // negative bufferSize means null. 
            }
            else
            {
                Write(value.Length);
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    if (c < 128)
                        Write((byte)value[i]);                 // Only need one byte for UTF8
                    else if (c < 2048)
                    {
                        // TODO confirm that this is correct!
                        Write((byte) (0xC0 | (c >> 6)));                // Encode 2 byte UTF8
                        Write((byte) (0x80 | (c & 0x3F)));
                    }
                    else
                    {
                        // TODO confirm that this is correct!
                        Write((byte) (0xE0 | ((c >> 12) & 0xF)));        // Encode 3 byte UTF8
                        Write((byte) (0x80 | ((c >> 6) & 0x3F)));
                        Write((byte) (0x80 | (c & 0x3F)));
                    }
                }
            }
        }
        public virtual StreamLabel GetLabel()
        {
            return (StreamLabel)Length;
        }
        public void WriteSuffixLabel(StreamLabel value)
        {
            // This is guaranteed to be uncompressed, but since we are not compressing anything, we can
            // simply write the value.  
            Write(value);
        }

        public void WriteToStream(Stream outputStream)
        {
            // TODO really big streams will overflow;
            outputStream.Write(bytes, 0, (int)Length);
        }
        // Note that the returned MemoryStreamReader is not valid if more writes are done.  
        public MemoryStreamReader GetReader() { return new MemoryStreamReader(bytes); }

    #region private
        ~MemoryStreamWriter()
        {
            this.Dispose(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (pinningHandle.IsAllocated)
                pinningHandle.Free();
        }

        internal /*protected*/  virtual void MakeSpace()
        {
            byte[] newBytes = new byte[bytes.Length * 3 / 2];
            Array.Copy(bytes, newBytes, bytes.Length);
            bytes = newBytes;
            fixed (byte* bytesAsPtr = &bytes[0])
                bufferStart = bytesAsPtr;
            bufferCur = &bufferStart[endPosition];
            bufferEnd = &bufferStart[bytes.Length];
        }
        internal /*protected*/  byte[] bytes;
        internal /*protected*/  int endPosition;

        private System.Runtime.InteropServices.GCHandle pinningHandle;
        byte* bufferStart;
        byte* bufferCur;
        byte* bufferEnd;
    #endregion
    }
#endif

    public class MemoryMappedFileStreamReader : IStreamReader
    {
        private MemoryMappedFile _file;
        private long _fileLength;
        private bool _leaveOpen;

        private MemoryMappedViewAccessor _view;
        private IntPtr _viewAddress;
        private long _viewOffset;
        private long _capacity;
        private long _offset;

        public MemoryMappedFileStreamReader(string mapName, long length)
            : this(MemoryMappedFile.OpenExisting(mapName, MemoryMappedFileRights.Read), length, leaveOpen: false)
        {
        }

        public MemoryMappedFileStreamReader(MemoryMappedFile file, long length, bool leaveOpen)
        {
            _file = file;
            _fileLength = length;
            _leaveOpen = leaveOpen;

            if (IntPtr.Size == 4)
            {
                _capacity = Math.Min(_fileLength, MemoryMappedFileStreamWriter.BlockCopyCapacity);
            }
            else
            {
                _capacity = _fileLength;
            }

            _view = File.CreateViewAccessor(0, _capacity, MemoryMappedFileAccess.Read);
            _viewAddress = _view.SafeMemoryMappedViewHandle.DangerousGetHandle();
        }

        public static MemoryMappedFileStreamReader CreateFromFile(string path)
        {
            long capacity = new FileInfo(path).Length;
            MemoryMappedFile file = MemoryMappedFile.CreateFromFile(path, FileMode.Open, Guid.NewGuid().ToString("N"), capacity, MemoryMappedFileAccess.Read);
            return new MemoryMappedFileStreamReader(file, capacity, leaveOpen: false);
        }

        public DeferedStreamLabel Current
        {
            get
            {
                return checked((DeferedStreamLabel)(_viewOffset + _offset));
            }
        }

        public long Length => _fileLength;

        protected MemoryMappedFile File
        {
            get
            {
                var result = _file;
                if (result == null)
                {
                    throw new ObjectDisposedException(nameof(MemoryMappedFileStreamReader));
                }

                return result;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Seek(long offset)
        {
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            // see if we can just change the offset
            if (offset >= _viewOffset && offset < _viewOffset + _capacity)
            {
                _offset = offset - _viewOffset;
                return;
            }

            // Have to move the view
            _view.Dispose();
            long availableInFile = _fileLength - offset;
            long viewOffset = offset & ~0xFFFF;
            long offsetInView = offset - viewOffset;
            long viewLength = Math.Min(MemoryMappedFileStreamWriter.BlockCopyCapacity, availableInFile + offsetInView);
            _view = _file.CreateViewAccessor(viewOffset, viewLength, MemoryMappedFileAccess.Read);
            _viewAddress = _view.SafeMemoryMappedViewHandle.DangerousGetHandle();
            _viewOffset = viewOffset;
            _capacity = viewLength;
            _offset = offsetInView;
        }

        public void Goto(DeferedStreamLabel label)
        {
            if (label < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(label));
            }

            // see if we can just change the offset
            int absoluteOffset = (int)label;
            if (absoluteOffset >= _viewOffset && absoluteOffset < _viewOffset + _capacity)
            {
                _offset = (int)(absoluteOffset - _viewOffset);
                return;
            }

            // Have to move the view
            _view.Dispose();
            long availableInFile = _fileLength - absoluteOffset;
            long viewOffset = absoluteOffset & ~0xFFFF;
            long offset = absoluteOffset - viewOffset;
            long viewLength = Math.Min(MemoryMappedFileStreamWriter.BlockCopyCapacity, availableInFile + offset);
            _view = _file.CreateViewAccessor(viewOffset, viewLength, MemoryMappedFileAccess.Read);
            _viewAddress = _view.SafeMemoryMappedViewHandle.DangerousGetHandle();
            _viewOffset = viewOffset;
            _capacity = viewLength;
            _offset = offset;
        }

        public void GotoSuffixLabel()
        {
            Goto((DeferedStreamLabel)(Length - sizeof(DeferedStreamLabel)));
            Goto(ReadLabel());
        }

        public unsafe byte ReadByte()
        {
            if (_offset + sizeof(byte) > _capacity)
            {
                Resize(sizeof(byte));
            }

            var result = *((byte*)_viewAddress + _offset);
            _offset += sizeof(byte);
            return result;
        }

        public unsafe short ReadInt16()
        {
            if (_offset + sizeof(short) > _capacity)
            {
                Resize(sizeof(short));
            }

            var result = *(short*)((byte*)_viewAddress + _offset);
            _offset += sizeof(short);
            return result;
        }

        public unsafe int ReadInt32()
        {
            if (_offset + sizeof(int) > _capacity)
            {
                Resize(sizeof(int));
            }

            var result = *(int*)((byte*)_viewAddress + _offset);
            _offset += sizeof(int);
            return result;
        }

        public unsafe long ReadInt64()
        {
            if (_offset + sizeof(long) > _capacity)
            {
                Resize(sizeof(long));
            }

            var result = *(long*)((byte*)_viewAddress + _offset);
            _offset += sizeof(long);
            return result;
        }

        public DeferedStreamLabel ReadLabel()
        {
            return (DeferedStreamLabel)ReadInt32();
        }

        public unsafe string ReadString()
        {
            int charCount = ReadInt32();
            if (charCount == -1)
            {
                return null;
            }

            string result = new string('\0', charCount);
            fixed (char* chars = result)
            {
                Decoder decoder = Encoding.UTF8.GetDecoder();

                int bytesUsed;
                int charsUsed;
                bool completed;
#if NETSTANDARD1_3
                byte[] bytes = new byte[(int)Math.Min(int.MaxValue - 50, _capacity - _offset)];
                Marshal.Copy(_viewAddress, bytes, 0, bytes.Length);
                char[] charArray = new char[charCount];
                decoder.Convert(bytes, 0, bytes.Length, charArray, 0, charArray.Length, false, out bytesUsed, out charsUsed, out completed);
                Marshal.Copy(charArray, 0, (IntPtr)chars, charsUsed * sizeof(char));
#else
                decoder.Convert((byte*)_viewAddress, (int)Math.Min(int.MaxValue - 50, _capacity - _offset), chars, charCount, false, out bytesUsed, out charsUsed, out completed);
#endif
                _offset += bytesUsed;

                if (!completed)
                {
                    long availableInFile = _fileLength - _viewOffset - _offset;
                    Resize(checked((int)Math.Min(availableInFile, Encoding.UTF8.GetMaxByteCount(charCount - charsUsed))));

                    int finalBytesUsed;
                    int finalCharsUsed;
#if NETSTANDARD1_3
                    bytes = new byte[(int)Math.Min(int.MaxValue - 50, _capacity - _offset)];
                    Marshal.Copy(_viewAddress + bytesUsed, bytes, 0, bytes.Length);
                    charArray = new char[charCount - charsUsed];
                    decoder.Convert(bytes, 0, bytes.Length, charArray, 0, charArray.Length, true, out finalBytesUsed, out finalCharsUsed, out completed);
                    Marshal.Copy(charArray, 0, (IntPtr)(chars + charsUsed), finalCharsUsed * sizeof(char));
#else
                    decoder.Convert((byte*)_viewAddress + bytesUsed, (int)Math.Min(int.MaxValue - 50, _capacity - _offset), chars + charsUsed, charCount - charsUsed, true, out finalBytesUsed, out finalCharsUsed, out completed);
#endif
                }
            }

            return result;
        }

        protected void Resize(int capacity)
        {
            // See if we can do nothing
            long available = _capacity - _offset;
            if (available >= capacity)
            {
                return;
            }

            // We can no longer use the current view, so go ahead and dispose of it
            _view.Dispose();

            // See if the underlying file is large enough
            long availableInFile = _fileLength - _viewOffset - _offset;
            if (availableInFile < capacity)
            {
                throw new InvalidOperationException("Cannot create a view outside the bounds of the file.");
            }

            long viewOffset = (_viewOffset + _offset) & ~0xFFFF;
            long offset = (_viewOffset + _offset) - viewOffset;
            long viewLength = Math.Max(Math.Min(MemoryMappedFileStreamWriter.BlockCopyCapacity, availableInFile + offset), capacity + offset);
            _view = _file.CreateViewAccessor(viewOffset, viewLength, MemoryMappedFileAccess.Read);
            _viewAddress = _view.SafeMemoryMappedViewHandle.DangerousGetHandle();
            _viewOffset = viewOffset;
            _capacity = viewLength;
            _offset = offset;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _viewAddress = IntPtr.Zero;

                Interlocked.Exchange(ref _view, null)?.Dispose();

                var file = Interlocked.Exchange(ref _file, null);
                if (!_leaveOpen)
                {
                    file?.Dispose();
                }
            }
        }
    }

    public class MemoryMappedFileStreamWriter : IStreamWriter
    {
        internal const long PageSize = 64 * 1024;
        internal const long InitialCapacity = 64 * 1024;
        internal const int BlockCopyCapacity = 10 * 1024 * 1024;

        private MemoryMappedFile _file;
        private string _mapName;
        private long _fileCapacity;

        private MemoryMappedViewAccessor _view;
        private long _viewOffset;
        private int _capacity;
        private int _offset;

        public MemoryMappedFileStreamWriter(long initialCapacity = InitialCapacity)
        {
            long subPageSize = initialCapacity % PageSize;
            if (subPageSize != 0)
            {
                initialCapacity += PageSize - subPageSize;
            }

            _mapName = Guid.NewGuid().ToString("N");
            _file = MemoryMappedFile.CreateNew(_mapName, InitialCapacity);
            _fileCapacity = InitialCapacity;

            _capacity = (int)Math.Min(_fileCapacity, BlockCopyCapacity);
            _view = File.CreateViewAccessor(0, _capacity, MemoryMappedFileAccess.Write);

        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected MemoryMappedFile File
        {
            get
            {
                var result = _file;
                if (result == null)
                {
                    throw new ObjectDisposedException(nameof(MemoryMappedFileStreamWriter));
                }

                return result;
            }
        }

        public MemoryMappedFileStreamReader GetReader()
        {
            return new MemoryMappedFileStreamReader(_mapName, Length);
        }

        public void Clear()
        {
            if (_viewOffset == 0 && _offset == 0)
            {
                return;
            }

            _view.Dispose();

            _capacity = (int)Math.Min(_fileCapacity, BlockCopyCapacity);
            _view = File.CreateViewAccessor(0, _capacity, MemoryMappedFileAccess.Write);
            _viewOffset = 0;
            _offset = 0;
        }

        public long Length
            => _viewOffset + _offset;

        public DeferedStreamLabel GetLabel()
        {
            return checked((DeferedStreamLabel)Length);
        }

        public void Write(byte value)
        {
            if (_offset + sizeof(byte) > _capacity)
            {
                Resize(sizeof(byte));
            }

            _view.Write(_offset, value);
            _offset += sizeof(byte);
        }

        public void Write(short value)
        {
            if (_offset + sizeof(short) > _capacity)
            {
                Resize(sizeof(short));
            }

            _view.Write(_offset, value);
            _offset += sizeof(short);
        }

        public void Write(int value)
        {
            if (_offset + sizeof(int) > _capacity)
            {
                Resize(sizeof(int));
            }

            _view.Write(_offset, value);
            _offset += sizeof(int);
        }

        public void Write(long value)
        {
            if (_offset + sizeof(long) > _capacity)
            {
                Resize(sizeof(long));
            }

            _view.Write(_offset, value);
            _offset += sizeof(long);
        }

        public void Write(DeferedStreamLabel value)
        {
            Write((int)value);
        }

        public unsafe void Write(string value)
        {
            if (value == null)
            {
                Write(-1);
            }
            else
            {
                Write(value.Length);

                fixed (char* chars = value)
                {
                    byte* pointer = null;

#if !NETSTANDARD1_3
                    RuntimeHelpers.PrepareConstrainedRegions();
#endif
                    try
                    {
                        _view.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
                        Encoder encoder = Encoding.UTF8.GetEncoder();

                        int charsUsed;
                        int bytesUsed;
                        bool completed;
#if NETSTANDARD1_3
                        char[] charArray = value.ToCharArray();
                        byte[] bytes = new byte[_capacity - _offset];
                        encoder.Convert(charArray, 0, charArray.Length, bytes, 0, bytes.Length, false, out charsUsed, out bytesUsed, out completed);
                        Marshal.Copy(bytes, 0, (IntPtr)pointer, bytesUsed);
#else
                        encoder.Convert(chars, value.Length, pointer, _capacity - _offset, false, out charsUsed, out bytesUsed, out completed);
#endif
                        _offset += bytesUsed;

                        if (!completed)
                        {
                            Resize(Encoding.UTF8.GetMaxByteCount(value.Length - charsUsed));

                            int finalCharsUsed;
                            int finalBytesUsed;
#if NETSTANDARD1_3
                            bytes = new byte[_capacity - _offset];
                            encoder.Convert(charArray, charsUsed, charArray.Length - charsUsed, bytes, 0, bytes.Length, true, out finalCharsUsed, out finalBytesUsed, out completed);
                            Marshal.Copy(bytes, 0, (IntPtr)(pointer + bytesUsed), finalBytesUsed);
#else
                            encoder.Convert(chars + charsUsed, value.Length - charsUsed, pointer + bytesUsed, _capacity - _offset, true, out finalCharsUsed, out finalBytesUsed, out completed);
#endif
                        }
                    }
                    finally
                    {
                        if (pointer != null)
                        {
                            _view.SafeMemoryMappedViewHandle.ReleasePointer();
                        }
                    }
                }
            }
        }

        public void WriteSuffixLabel(DeferedStreamLabel value)
        {
            // This is guaranteed to be uncompressed, but since we are not compressing anything, we can
            // simply write the value.  
            Write(value);
        }

        protected void Resize(int capacity)
        {
            // See if we can do nothing
            int available = _capacity - _offset;
            if (available >= capacity)
            {
                return;
            }

            // We can no longer use the current view, so go ahead and dispose of it
            _view.Dispose();

            // See if we need to resize the underlying file
            long availableInFile = _fileCapacity - _viewOffset - _offset;
            if (availableInFile < capacity)
            {
                long minimumFileSize = _fileCapacity - availableInFile + capacity;
                long newFileSize = _fileCapacity;
                while (newFileSize < minimumFileSize)
                {
                    newFileSize = (newFileSize / 2) * 3;
                }

                var newMapName = Guid.NewGuid().ToString("N");
                var newFile = MemoryMappedFile.CreateNew(newMapName, newFileSize);
                long currentCopyOffset = 0;
                long remainingSizeToCopy = _fileCapacity - availableInFile;
                while (remainingSizeToCopy > 0)
                {
                    int chunkSize = (int)Math.Min(BlockCopyCapacity, remainingSizeToCopy);
                    using (var readStream = _file.CreateViewStream(currentCopyOffset, chunkSize, MemoryMappedFileAccess.Read))
                    using (var writeStream = newFile.CreateViewStream(currentCopyOffset, chunkSize, MemoryMappedFileAccess.Write))
                    {
                        readStream.CopyTo(writeStream, chunkSize);
                    }

                    currentCopyOffset += chunkSize;
                    remainingSizeToCopy -= chunkSize;
                }

                _file.Dispose();
                _file = newFile;
                _mapName = newMapName;
                availableInFile += newFileSize - _fileCapacity;
                _fileCapacity = newFileSize;
            }

            long viewOffset = (_viewOffset + _offset) & ~0xFFFF;
            long offset = (_viewOffset + _offset) - viewOffset;
            long viewLength = Math.Max(Math.Min(BlockCopyCapacity, availableInFile + offset), capacity + offset);
            _view = _file.CreateViewAccessor(viewOffset, viewLength, MemoryMappedFileAccess.Write);
            _viewOffset = viewOffset;
            _capacity = (int)viewLength;
            _offset = (int)offset;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                Interlocked.Exchange(ref _view, null)?.Dispose();
                Interlocked.Exchange(ref _file, null)?.Dispose();
            }
        }
    }

    /// <summary>
    /// A IOStreamStreamReader hooks a MemoryStreamReader up to an input System.IO.Stream.  
    /// </summary>
#if STREAMREADER_PUBLIC
    public
#endif
    class IOStreamStreamReader : MemoryStreamReader, IDisposable
    {
        /// <summary>
        /// Create a new IOStreamStreamReader from the given file.  
        /// </summary>
        /// <param name="fileName"></param>
        public IOStreamStreamReader(string fileName)
            : this(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete)) { }

        /// <summary>
        /// Create a new IOStreamStreamReader from the given System.IO.Stream.   Optionally you can specify the size of the read buffer
        /// The stream will be closed by the IOStreamStreamReader when it is closed.  
        /// </summary>
        public IOStreamStreamReader(Stream inputStream, int bufferSize = defaultBufferSize, bool leaveOpen = false)
            : base(new byte[bufferSize + align], 0, 0)
        {
            Debug.Assert(bufferSize % align == 0);
            this.inputStream = inputStream;
            this.leaveOpen = leaveOpen;
        }

        /// <summary>
        /// close the file or underlying stream and clean up 
        /// </summary>
        public void Close()
        {
            Dispose(true);
        }

        #region overrides 
        /// <summary>
        /// Implementation of IStreamReader
        /// </summary>
        public override StreamLabel Current
        {
            get
            {
                return (StreamLabel)(positionInStream + position);
            }
        }
        /// <summary>
        /// Implementation of IStreamReader
        /// </summary>
        public override void Goto(StreamLabel label)
        {
            uint offset = unchecked((uint)label - positionInStream);
            if (offset > (uint)endPosition)
            {
                positionInStream = (uint)label;
                position = endPosition = 0;
            }
            else
            {
                position = (int)offset;
            }
        }
        /// <summary>
        /// Implementation of MemoryStreamReader
        /// </summary>
        public override long Length { get { return inputStream.Length; } }
        #endregion 

        #region private
        /// <summary>
        /// Dispose pattern
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (!leaveOpen)
                {
                    inputStream.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        internal /*protected*/  const int align = 8;        // Needs to be a power of 2
        internal /*protected*/  const int defaultBufferSize = 0x4000;  // 16K 

        /// <summary>
        /// Fill the buffer, making sure at least 'minimum' byte are available to read.  Throw an exception
        /// if there are not that many bytes.  
        /// </summary>
        /// <param name="minimum"></param>
        internal /*protected*/  override void Fill(int minimum)
        {
            if (endPosition != position)
            {
                int slideAmount = position & ~(align - 1);             // round down to stay aligned.  
                for (int i = slideAmount; i < endPosition; i++)        // Slide everything down.  
                {
                    bytes[i - slideAmount] = bytes[i];
                }

                endPosition -= slideAmount;
                position -= slideAmount;
                positionInStream += (uint)slideAmount;
            }
            else
            {
                positionInStream += (uint)position;
                endPosition = 0;
                position = 0;
                // if you are within one read of the end of file, go backward to read the whole block.  
                uint lastBlock = (uint)(((int)inputStream.Length - bytes.Length + align) & ~(align - 1));
                if (positionInStream >= lastBlock)
                {
                    position = (int)(positionInStream - lastBlock);
                }
                else
                {
                    position = (int)positionInStream & (align - 1);
                }

                positionInStream -= (uint)position;
            }

            Debug.Assert(positionInStream % align == 0);
            lock (inputStream)
            {
                inputStream.Seek(positionInStream + endPosition, SeekOrigin.Begin);
                for (; ; )
                {
                    System.Threading.Thread.Sleep(0);       // allow for Thread.Interrupt
                    int count = inputStream.Read(bytes, endPosition, bytes.Length - endPosition);
                    if (count == 0)
                    {
                        break;
                    }

                    endPosition += count;
                    if (endPosition == bytes.Length)
                    {
                        break;
                    }
                }
            }
            if (endPosition - position < minimum)
            {
                throw new Exception("Read past end of stream.");
            }
        }
        internal /*protected*/  Stream inputStream;
        private bool leaveOpen;
        internal /*protected*/  uint positionInStream;
        #endregion
    }

    /// <summary>
    /// A PinnedStreamReader is an IOStream reader that will pin its read buffer.
    /// This allows it it support a 'GetPointer' API efficiently.   The 
    /// GetPointer API lets you access data from the stream as raw byte 
    /// blobs without having to copy the data.  
    /// </summary>
#if STREAMREADER_PUBLIC
    public
#endif
    sealed unsafe class PinnedStreamReader : IOStreamStreamReader
    {
        /// <summary>
        /// Create a new PinnedStreamReader that gets its data from a given file.  You can optionally set the size of the read buffer.  
        /// </summary>
        public PinnedStreamReader(string fileName, int bufferSize = defaultBufferSize)
            : this(new FileStream(fileName, FileMode.Open, FileAccess.Read,
            FileShare.Read | FileShare.Delete), bufferSize)
        { }

        /// <summary>
        /// Create a new PinnedStreamReader that gets its data from a given System.IO.Stream.  You can optionally set the size of the read buffer.  
        /// The stream will be closed by the PinnedStreamReader when it is closed.  
        /// </summary>
        public PinnedStreamReader(Stream inputStream, int bufferSize = defaultBufferSize)
            : base(inputStream, bufferSize)
        {
            // Pin the array
            pinningHandle = System.Runtime.InteropServices.GCHandle.Alloc(bytes, System.Runtime.InteropServices.GCHandleType.Pinned);
            fixed (byte* bytesAsPtr = &bytes[0])
            {
                bufferStart = bytesAsPtr;
            }
        }

        /// <summary>
        /// Clone the PinnnedStreamReader so that it reads from the same stream as this one.   They will share the same
        /// System.IO.Stream, but each will lock and seek when accessing that stream so they can both safely share it.  
        /// </summary>
        /// <returns></returns>
        public PinnedStreamReader Clone()
        {
            PinnedStreamReader ret = new PinnedStreamReader(inputStream, bytes.Length - align);
            return ret;
        }

        /// <summary>
        /// Get a byte* pointer to the input buffer at 'Position' in the IReadStream that is at least 'length' bytes long.  
        /// (thus ptr to ptr+len is valid).   Note that length cannot be larger than the buffer size passed to the reader
        /// when it was constructed.  
        /// </summary>
        public unsafe byte* GetPointer(StreamLabel Position, int length)
        {
            Goto(Position);
            return GetPointer(length);
        }

        /// <summary>
        /// Get a byte* pointer to the input buffer at the current read position is at least 'length' bytes long.  
        /// (thus ptr to ptr+len is valid).   Note that length cannot be larger than the buffer size passed to the reader
        /// when it was constructed.  
        /// </summary>
        public unsafe byte* GetPointer(int length)
        {
            if (position + length > endPosition)
            {
                Fill(length);
            }
#if DEBUG
            fixed (byte* bytesAsPtr = &bytes[0])
                Debug.Assert(bytesAsPtr == bufferStart, "Error, buffer not pinnned");
            Debug.Assert(position < bytes.Length);
#endif
            return (byte*)(&bufferStart[position]);
        }

        #region private
        ~PinnedStreamReader()
        {
            Dispose(false);
        }

        protected override void Dispose(bool disposing)
        {
            if (pinningHandle.IsAllocated)
            {
                pinningHandle.Free();
            }

            base.Dispose(disposing);
        }

        private System.Runtime.InteropServices.GCHandle pinningHandle;
        private byte* bufferStart;
        #endregion
    }

#if PINNEDSTREAMREADER_TESTS
    public static class PinnedStreamTests
    {
        public static void Tests()
        {
            string testOrig = "text.orig";

            Random r = new Random(23);

            for (int j = 0; j < 10; j++)
            {
                for (int fileSize = 1023; fileSize <= 1025; fileSize++)
                {
                    CreateDataFile(testOrig, fileSize);
                    byte[] origData = File.ReadAllBytes(testOrig);

                    for (int bufferSize = 16; bufferSize < 300; bufferSize += 24)
                    {
                        FileStream testData = File.OpenRead(testOrig);
                        PinnedStreamReader reader = new PinnedStreamReader(testData, bufferSize);

                        // Try reading back in various seek positions. 
                        for (int i = 0; i < 100; i++)
                        {
                            int position = r.Next(0, origData.Length);
                            int size = r.Next(0, bufferSize) + 1;

                            reader.Goto((StreamLabel)position);
                            Compare(reader, origData, position, size);
                        }
                        reader.Close();
                    }
                }
                Console.WriteLine("Finished Round " + j);
            }
        }

        static int compareCount = 0;

        private static void Compare(PinnedStreamReader reader, byte[] buffer, int offset, int chunkSize)
        {
            compareCount++;
            if (compareCount == -1)
                Debugger.Break();

            for (int pos = offset; pos < buffer.Length; pos += chunkSize)
            {
                if (pos + chunkSize > buffer.Length)
                    chunkSize = buffer.Length - pos;
                CompareBuffer(reader.GetPointer(chunkSize), buffer, pos, chunkSize);
                reader.Skip(chunkSize);
            }
        }

        private unsafe static bool CompareBuffer(IntPtr ptr, byte[] buffer, int offset, int size)
        {
            byte* bytePtr = (byte*)ptr;

            for (int i = 0; i < size; i++)
            {
                if (buffer[i + offset] != bytePtr[i])
                {
                    Debug.Assert(false);
                    return false;
                }
            }
            return true;
        }
        private static void CreateDataFile(string name, int length)
        {
            FileStream stream = File.Open(name, FileMode.Create);
            byte val = 0;
            for (int i = 0; i < length; i++)
                stream.WriteByte(val++);
            stream.Close();
        }

    }
#endif

    /// <summary>
    /// A IOStreamStreamWriter hooks a MemoryStreamWriter up to an output System.IO.Stream
    /// </summary>
#if STREAMREADER_PUBLIC
    public
#endif
    class IOStreamStreamWriter : MemoryStreamWriter, IDisposable
    {
        /// <summary>
        /// Create a IOStreamStreamWriter that writes its data to a given file that it creates
        /// </summary>
        /// <param name="fileName"></param>
        public IOStreamStreamWriter(string fileName) : this(new FileStream(fileName, FileMode.Create)) { }

        /// <summary>
        /// Create a IOStreamStreamWriter that writes its data to a System.IO.Stream
        /// </summary>
        public IOStreamStreamWriter(Stream outputStream, int bufferSize = defaultBufferSize + sizeof(long), bool leaveOpen = false)
            : base(bufferSize)
        {
            this.outputStream = outputStream;
            this.leaveOpen = leaveOpen;
            streamLength = outputStream.Length;
        }

        /// <summary>
        /// Flush any written data to the underlying System.IO.Stream
        /// </summary>
        public void Flush()
        {
            outputStream.Write(bytes, 0, endPosition);
            streamLength += endPosition;
            endPosition = 0;
            outputStream.Flush();
        }

        /// <summary>
        /// Insures the bytes in the stream are written to the stream and cleans up resources.  
        /// </summary>
        public void Close()
        {
            Dispose();
        }

        /// <summary>
        /// Access the underlying System.IO.Stream.   You should avoid using this if at all possible.  
        /// </summary>
        public Stream RawStream { get { return outputStream; } }

        #region overrides
        /// <summary>
        /// Implementation of the MemoryStreamWriter interface 
        /// </summary>
        public override long Length
        {
            get
            {
                Debug.Assert(streamLength == outputStream.Length);
                return base.Length + streamLength;
            }
        }
        /// <summary>
        /// Implementation of the IStreamWriter interface 
        /// </summary>
        public override StreamLabel GetLabel()
        {
            long len = Length;
            if (len != (uint)len)
            {
                throw new NotSupportedException("Streams larger than 4 GB.  You need to use /MaxEventCount to limit the size.");
            }

            return (StreamLabel)len;
        }
        /// <summary>
        /// Implementation of the MemoryStreamWriter interface 
        /// </summary>
        public override void Clear()
        {
            outputStream.SetLength(0);
            streamLength = 0;
        }
        /// <summary>
        /// Implementation of the MemoryStreamWriter interface 
        /// </summary>
        public override MemoryStreamReader GetReader() { throw new InvalidOperationException(); }
        /// <summary>
        /// Implementation of the MemoryStreamWriter interface 
        /// </summary>
        public override byte[] GetBytes() { throw new InvalidOperationException(); }
        #endregion 
        #region private
        internal /*protected*/  override void MakeSpace()
        {
            Debug.Assert(endPosition > bytes.Length - sizeof(long));
            outputStream.Write(bytes, 0, endPosition);
            streamLength += endPosition;
            endPosition = 0;
        }

        /// <summary>
        /// Dispose pattern
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Flush();
                if (!leaveOpen)
                {
                    outputStream.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        private const int defaultBufferSize = 1024 * 8 - sizeof(long);
        private Stream outputStream;
        private bool leaveOpen;
        private long streamLength;

        #endregion
    }
}
