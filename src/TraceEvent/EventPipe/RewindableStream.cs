using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Tracing.EventPipe
{

    /// <summary>
    /// RewindableStream provides a limited ability to rewind in a stream that doesn't support seeking. This is useful in a couple cases:
    /// - We can peek ahead in the stream interpreting data one way, then if we find that the data is actually in a different format, we can back up and call a different
    ///   code path to read it a different way
    /// - We can reduce the number of read system calls by reading a larger chunk than what we need, and then caching the extra bytes for later use.
    ///   Opportunistically we can issue one read syscall per block by trying to include the next block's header.
    /// 
    /// To rewind the caller first uses Read or ReadAtLeast to read some bytes, then call CacheUnusedBytes() with some suffix of what was read to logically
    /// rewind - returning those bytes back to the stream.
    /// </summary>
    internal class RewindableStream : IDisposable
    {
        protected Stream _stream;

        // this cache has bytes that have already been read from the stream but not yet parsed
        // it fills back to front and empties from front to back
        byte[] _cachedBytes;
        // the file position _cacheStreamOffset corresponds to _cachedBytes position _cacheArrayOffset
        int _cacheArrayOffset;
        long _cacheStreamOffset;
        // the number of bytes available to be read in the cache
        int _cacheBytesAvailable;

        public RewindableStream(Stream stream, int maxCacheSize = 64)
        {
            _stream = stream;
            _cachedBytes = new byte[maxCacheSize];
            _cacheArrayOffset = _cachedBytes.Length;
        }

        public long Position => _cacheStreamOffset;

        public int MaxCacheSize => _cachedBytes.Length;

        private int ReadFromCache(Span<byte> buffer)
        {
            int bytesRead = Math.Min(_cacheBytesAvailable, buffer.Length);
            if (bytesRead > 0)
            {
                _cachedBytes.AsSpan(_cacheArrayOffset, bytesRead).CopyTo(buffer);
                _cacheArrayOffset += bytesRead;
                _cacheStreamOffset += bytesRead;
                _cacheBytesAvailable -= bytesRead;
            }
            return bytesRead;
        }

        public T Read<T>() where T : struct
        {
            Span<byte> buffer = stackalloc byte[Unsafe.SizeOf<T>()];
            Read(buffer);
            return MemoryMarshal.Read<T>(buffer);
        }

        public void Read(Span<byte> buffer) => ReadAtLeast(buffer, buffer.Length);

        public int ReadAtLeast(Span<byte> buffer, int minLength)
        {
            // consume bytes from the cache first
            int bytesRead = ReadFromCache(buffer);

            // if the cache didn't satisfy the minimum read, read from the stream
            while (bytesRead < minLength)
            {
                Debug.Assert(_cacheBytesAvailable == 0);
                int read = _stream.Read(buffer.Slice(bytesRead));
                if (read == 0)
                {
                    throw new FormatException("Read past end of stream.");
                }
                bytesRead += read;
                _cacheStreamOffset += read;
            }
            return bytesRead;
        }

        // Readers are allowed to read more than they need, then give the extra bytes back to be stored in the cache for later.
        // We only support caching bytes from the most recent call to Read/ReadAtLeast, and only up to the maximum cache size
        // specified in the constructor.
        public void CacheUnusedBytes(ReadOnlySpan<byte> extraBytes)
        {
            if (extraBytes.Length + _cacheBytesAvailable > MaxCacheSize)
            {
                // This should never happen unless there is a bug in the code
                throw new InvalidOperationException("Cache overflow");
            }
            // copy the new data into the cache
            _cacheBytesAvailable += extraBytes.Length;
            _cacheStreamOffset -= extraBytes.Length;
            _cacheArrayOffset -= extraBytes.Length;
            extraBytes.CopyTo(_cachedBytes.AsSpan(_cacheArrayOffset));
        }


        public void Dispose()
        {
            _stream.Dispose();
        }
    }

    internal static class StreamExtensions
    {
        delegate int StreamReadDelegate(Stream stream, Span<byte> buffer);

        internal static bool IsFastSpanReadAvailable => s_streamReadDelegate != SlowStreamRead;

        private static readonly StreamReadDelegate s_streamReadDelegate = InitSpanRead();

        private static StreamReadDelegate InitSpanRead()
        {
            // Opportunistically use it if reflection shows it is available, otherwise fallback to
            // the slower Read(byte[], int, int) method + a copy.
            MethodInfo method = typeof(Stream).GetMethod("Read", new Type[] { typeof(Span<byte>) });
            if (method != null)
            {
                return (StreamReadDelegate)method.CreateDelegate(typeof(StreamReadDelegate), null);
            }
            else
            {
                return SlowStreamRead;
            }
        }

        // A slower fallback for when Stream.Read(Span<byte>) is not available.
        private static int SlowStreamRead(Stream stream, Span<byte> buffer)
        {
            byte[] arrayBuffer = ArrayPool<byte>.Shared.Rent(buffer.Length);
            int ret = stream.Read(arrayBuffer, 0, buffer.Length);
            if (ret > 0)
            {
                arrayBuffer.AsSpan(0, ret).CopyTo(buffer);
            }
            ArrayPool<byte>.Shared.Return(arrayBuffer);
            return ret;
        }

        public static int Read(this Stream stream, Span<byte> buffer)
        {
            return s_streamReadDelegate(stream, buffer);
        }
    }
}
