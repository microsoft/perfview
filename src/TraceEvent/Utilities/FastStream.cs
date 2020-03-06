using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Microsoft.Diagnostics.Tracing.Utilities
{
    /// <summary>
    /// The is really what BinaryReader should have been... (sigh)
    /// 
    /// We need really fast, byte-by-byte streaming. ReadChar needs to be inliable .... All the routines that
    /// give back characters assume the bytes are ASCII (The translations from bytes to chars is simply a
    /// cast).
    /// 
    /// The basic model is that of a Enumerator. There is a 'Current' property that represents the current
    /// byte, and 'MoveNext' that moves to the next byte and returns false if there are no more bytes. Like
    /// Enumerators 'MoveNext' needs to be called at least once before 'Current' is valid.
    /// 
    /// Unlike standard Enumerators, FastStream does NOT consider it an error to read 'Current' is read when
    /// there are no more characters.  Instead Current returns a Sentinal value (by default this is 0, but
    /// the 'Sentinal' property allow you to choose it).   This is often more convenient and efficient to
    /// allow checking end-of-file (which is rare), to happen only at certain points in the parsing logic.  
    /// 
    /// Another really useful feature of this stream is that you can peek ahead efficiently a large number
    /// of bytes (since you read ahead into a buffer anyway).
    /// </summary>
    public sealed class FastStream : IDisposable
    {
        // construction 
        public FastStream(string filePath)
            : this(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
        {
        }
        public FastStream(Stream stream, int bufferSize = 262144, bool closeStream = false)
        {
            this.stream = stream;
            this.closeStream = closeStream;
            buffer = new byte[bufferSize];
            bufferFillPos = 1;
            bufferIndex = 0;
            streamReadIn = 1;
            streamPosition = 0;
            IsDisposed = false;
        }
        public FastStream(byte[] buffer, int start, int length)
        {
            stream = Stream.Null;
            streamReadIn = (uint)length;

            bool usingGivenBuffer = buffer.Length > MaxRestoreLength && start > 0;

            if (usingGivenBuffer)
            {
                bufferIndex = (uint)start - 1;
                bufferFillPos = MaxRestoreLength + streamReadIn;
                this.buffer = buffer;
            }
            else
            {
                bufferFillPos = MaxRestoreLength + 1 + streamReadIn;
                this.buffer = new byte[bufferFillPos];
                bufferIndex = MaxRestoreLength;
                Buffer.BlockCopy(buffer, start, this.buffer, (int)bufferIndex + 1, length);
            }

            streamPosition = bufferFillPos;
            this.buffer[bufferIndex] = 0;
            streamPosition = streamReadIn;
            IsDisposed = false;
        }

        // reading
        /// <summary>
        /// For efficient reads, we allow you to read Current past the end of the stream.  You will
        /// get the 'Sentinal' value in that case.  This defaults to 0, but you can change it if 
        /// there is a better 'rare' value to use as an end of stream marker.  
        /// </summary>
        public byte Sentinal = 0;
        public byte ReadByte()
        {
            MoveNext();
            return Current;
        }
        public int ReadInt()
        {
            byte c = Current;
            while (c == ' ')
            {
                c = ReadByte();
            }

            bool negative = false;
            if (c == '-')
            {
                negative = true;
                c = ReadByte();
            }
            if (c >= '0' && c <= '9')
            {
                int value = 0;
                if (c == '0')
                {
                    c = ReadByte();
                    if (c == 'x' || c == 'X')
                    {
                        MoveNext();
                        value = ReadIntHex();
                    }
                }
                while (c >= '0' && c <= '9')
                {
                    value = value * 10 + c - '0';
                    c = ReadByte();
                }

                if (negative)
                {
                    value = -value;
                }

                return value;
            }
            else
            {
                return -1;
            }
        }
        public int ReadIntHex()
        {
            int value = 0;
            while (true)
            {
                int digit = Current;
                if (digit >= '0' && digit <= '9')
                {
                    digit -= '0';
                }
                else if (digit >= 'a' && digit <= 'f')
                {
                    digit -= 'a' - 10;
                }
                else if (digit >= 'A' && digit <= 'F')
                {
                    digit -= 'A' - 10;
                }
                else
                {
                    return value;
                }

                MoveNext();
                value = value * 16 + digit;
            }
        }
        public uint ReadUInt()
        {
            return (uint)ReadInt();
        }
        public long ReadLong()
        {
            byte c = Current;
            while (c == ' ')
            {
                c = ReadByte();
            }

            bool negative = false;
            if (c == '-')
            {
                negative = true;
                c = ReadByte();
            }
            if (c >= '0' && c <= '9')
            {
                long value = 0;
                if (c == '0')
                {
                    c = ReadByte();
                    if (c == 'x' || c == 'X')
                    {
                        MoveNext();
                        value = ReadLongHex();
                    }
                }
                while (c >= '0' && c <= '9')
                {
                    value = value * 10 + c - '0';
                    c = ReadByte();
                }

                if (negative)
                {
                    value = -value;
                }

                return value;
            }
            else
            {
                return -1;
            }
        }
        public long ReadLongHex()
        {
            long value = 0;
            while (true)
            {
                int digit = Current;
                if (digit >= '0' && digit <= '9')
                {
                    digit -= '0';
                }
                else if (digit >= 'a' && digit <= 'f')
                {
                    digit -= 'a' - 10;
                }
                else if (digit >= 'A' && digit <= 'F')
                {
                    digit -= 'A' - 10;
                }
                else
                {
                    return value;
                }

                MoveNext();
                value = value * 16 + digit;
            }
        }
        public ulong ReadULong()
        {
            return (ulong)ReadLong();
        }

        /// <summary>
        /// Reads 'charCount' characters into the string build sb from the source.  
        /// </summary>
        public void ReadFixedString(int charCount, StringBuilder sb)
        {
            while (charCount > 0)
            {
                sb.Append((char)Current);
                if (!MoveNext())
                {
                    break;
                }

                --charCount;
            }
        }

        public void ReadAsciiStringUpTo(char endMarker, StringBuilder sb)
        {
            for (; ; )
            {
                byte c = Current;
                if (c == endMarker)
                {
                    break;
                }

                sb.Append((char)c);
                if (!MoveNext())
                {
                    break;
                }
            }
        }
        public void ReadAsciiStringUpTo(string endMarker, StringBuilder sb)
        {
            Debug.Assert(0 < endMarker.Length);
            for (; ; )
            {
                ReadAsciiStringUpTo(endMarker[0], sb);
                uint markerIdx = 1;
                for (; ; )
                {
                    if (markerIdx >= endMarker.Length)
                    {
                        return;
                    }

                    if (Peek(markerIdx) != endMarker[(int)markerIdx])
                    {
                        break;
                    }

                    markerIdx++;
                }
                MoveNext();
            }
        }
        /// <summary>
        /// Reads the string into the stringBuilder until a byte is read that
        /// is one of the characters in 'endMarkers'.  
        /// </summary>
        public void ReadAsciiStringUpToAny(string endMarkers, StringBuilder sb)
        {
            for (; ; )
            {
                byte c = Current;
                for (int i = 0; i < endMarkers.Length; i++)
                {
                    if (c == endMarkers[i])
                    {
                        return;
                    }
                }

                sb.Append((char)c);
                if (!MoveNext())
                {
                    break;
                }
            }
        }
        /// <summary>
        /// Reads the stream into the string builder until the last end marker on the line is hit.
        /// </summary>
        public void ReadAsciiStringUpToLastBeforeTrue(char endMarker, StringBuilder sb, Func<byte, bool> predicate)
        {
            StringBuilder buffer = new StringBuilder();
            MarkedPosition mp = MarkPosition();

            while (predicate(Current) && !EndOfStream)
            {
                if (Current == endMarker)
                {
                    sb.Append(buffer);
                    buffer.Clear();
                    mp = MarkPosition();
                }

                buffer.Append((char)Current);
                MoveNext();
            }

            RestoreToMark(mp);
        }
        /// <summary>
        /// Reads the stream in the string builder until the given predicate function is false.
        /// </summary>
        public void ReadAsciiStringUpToTrue(StringBuilder sb, Func<byte, bool> predicate)
        {
            while (predicate(Current))
            {
                sb.Append((char)Current);
                if (!MoveNext())
                {
                    break;
                }
            }
        }

        // peeking (not moving the read cursor)
        /// <summary>
        /// Returns a number of bytes ahead without advancing the pointer. 
        /// Peek(0) is the same as calling Current.  
        /// </summary>
        /// <param name="bytesAhead"></param>
        /// <returns></returns>
        public byte Peek(uint bytesAhead)
        {
            uint peekIndex = bytesAhead + bufferIndex;
            if (peekIndex >= bufferFillPos)
            {
                peekIndex = PeekHelper(bytesAhead);
            }

            return buffer[peekIndex];
        }
        public int MaxPeek => buffer.Length - (int)MaxRestoreLength;

        // skipping 

        /// <summary>
        /// Moves through the FastStream without actually reading data.
        /// </summary>

        // Skip by a number amount
        public void Skip(uint amount)
        {
            while (amount >= bufferFillPos - bufferIndex)
            {
                if (EndOfStream)
                {
                    return;
                }
                amount -= bufferFillPos - bufferIndex;
                bufferIndex = FillBufferFromStreamPosition();
            }

            bufferIndex += amount;
        }

        public void SkipUpTo(char endMarker)
        {
            while (Current != endMarker)
            {
                if (!MoveNext())
                {
                    break;
                }
            }
        }
        public void SkipSpace()
        {
            while (Current == ' ')
            {
                MoveNext();
            }
        }
        public void SkipWhiteSpace()
        {
            while (Char.IsWhiteSpace((char)Current))
            {
                MoveNext();
            }
        }
        public void SkipUpToFalse(Func<byte, bool> predicate)
        {
            while (predicate(Current))
            {
                if (!MoveNext())
                {
                    break;
                }
            }
        }

        // Substreams

        /// <summary>
        /// Creates a FastStream with the read in stream with the given length.
        /// The "trail" is am ASCII string that is attached to the end of the returned FastStream.
        /// </summary>
        public FastStream ReadSubStream(int length, string trail = null)
        {
            if (bufferFillPos - bufferIndex < length)
            {
                bufferIndex = FillBufferFromStreamPosition(keepLast: bufferFillPos - bufferIndex);
            }

            length = (int)Math.Min(bufferFillPos - bufferIndex, length);

            streamReadIn = (uint)(bufferFillPos - (bufferIndex + length));

            byte[] newBuffer = GetUsedBuffer();
            int newStart = (int)(bufferIndex + length);
            int restoreAmount = (int)Math.Min(newStart, MaxRestoreLength);

            Buffer.BlockCopy(
                buffer, newStart - restoreAmount,
                newBuffer, (int)(MaxRestoreLength - restoreAmount),
                (int)streamReadIn + restoreAmount);

            if (trail != null)
            {
                Buffer.BlockCopy(
                    Encoding.ASCII.GetBytes(trail), 0,
                    buffer, newStart,
                    Math.Min(trail.Length, buffer.Length - newStart));

                length += trail.Length;
            }

            FastStream subStream = new FastStream(buffer, (int)bufferIndex, length);

            AddChild(subStream);

            buffer = newBuffer;
            bufferIndex = MaxRestoreLength;
            bufferFillPos = streamReadIn + MaxRestoreLength;

            return subStream;
        }

        // Foreach support.  
        public byte Current { get { return buffer[bufferIndex]; } }
        public bool MoveNext()
        {
            bufferIndex++;
            bool ret = true;
            if (bufferIndex >= bufferFillPos)
            {
                ret = MoveNextHelper();
            }

            return ret;
        }
        public bool EndOfStream { get { return streamReadIn == 0; } }

        // Mark and restore 
        public const uint MaxRestoreLength = 256;
        public struct MarkedPosition
        {
            internal long streamPos;

            public MarkedPosition(long streamPos)
            {
                this.streamPos = streamPos;
            }
        }
        public MarkedPosition MarkPosition()
        {
            return new MarkedPosition(Position);
        }
        public void RestoreToMark(MarkedPosition position)
        {
            long delta = Position - position.streamPos;
            if (delta > MaxRestoreLength)
            {
                stream.Position = streamPosition = position.streamPos;
                FillBufferFromStreamPosition();
            }
            else
            {
                bufferIndex -= (uint)delta;
            }
        }

        // Misc
        public long Position
        {
            get
            {
                return streamPosition - (streamReadIn - (bufferIndex - MaxRestoreLength));
            }
        }

        public void Dispose()
        {
            if (closeStream)
            {
                stream?.Dispose();
                stream = null;
            }

            IsDisposed = true;
        }

        #region privateMethods
        /// <summary>
        /// Gets a string from the position to the length indicated (for debugging)
        /// </summary>
        internal string PeekString(int length)
        {
            return PeekString(0, length);
        }

        internal string PeekString(int start, int length)
        {
            StringBuilder sb = new StringBuilder();
            for (uint i = bufferIndex + (uint)start; i < bufferIndex + length + start && i < bufferFillPos - 1; i++)
            {
                sb.Append((char)Peek(i + (uint)start - bufferIndex));
            }

            return sb.ToString();
        }

        private void AddChild(FastStream child)
        {
            if (next != null)
            {
                child.next = next;
            }

            next = child;
        }

        // Will later be changed to find a used buffer from faststream children
        private byte[] GetUsedBuffer()
        {
            FastStream prev = null;
            FastStream next = this.next;

            while (next != null)
            {
                if (next.IsDisposed)
                {
                    if (prev == null)
                    {
                        this.next = next.next;
                    }
                    else
                    {
                        prev.next = next.next;
                    }

                    return next.buffer;
                }

                prev = next;
                next = next.next;
            }

            return new byte[buffer.Length];
        }

        private uint FillBufferFromStreamPosition(uint keepLast = 0)
        {
            // This is so the first 'keepFromBack' integers are read in again.
            uint preamble = MaxRestoreLength + keepLast;
            for (int i = 0; i < preamble; i++)
            {
                if (bufferFillPos - (preamble - i) < 0)
                {
                    buffer[i] = 0;
                    continue;
                }

                buffer[i] = buffer[bufferFillPos - (preamble - i)];
            }

            streamReadIn = (uint)stream.Read(buffer, (int)preamble, buffer.Length - (int)preamble);
            bufferFillPos = streamReadIn + preamble;
            streamReadIn += keepLast;
            streamPosition += streamReadIn > 0 ? streamReadIn : 1;
            if (bufferFillPos < buffer.Length)
            {
                buffer[bufferFillPos] = Sentinal;    // we define 0 as the value you get after EOS.
            }

            return MaxRestoreLength;
        }

        private bool MoveNextHelper()
        {
            bufferIndex = FillBufferFromStreamPosition();
            return (streamReadIn > 0);
        }

        private uint PeekHelper(uint bytesAhead)
        {
            if (bytesAhead >= buffer.Length - MaxRestoreLength)
            {
                throw new Exception("Can only peek ahead the length of the buffer");
            }

            // We keep everything above the index.
            bufferIndex = FillBufferFromStreamPosition(keepLast: bufferFillPos - bufferIndex);

            return bytesAhead + bufferIndex;
        }

        #endregion
        #region privateState
        private byte[] buffer;
        private uint bufferFillPos;
        private uint streamReadIn;
        private Stream stream;
        private uint bufferIndex;      // The next character to read
        private long streamPosition;
        private bool closeStream;
        private FastStream next;
        private bool IsDisposed;


        // Use PeekString(int) to get more characters.  
        public override string ToString()
        {
            return Encoding.Default.GetString(buffer, (int)bufferIndex, Math.Min(80, buffer.Length - (int)bufferIndex));
        }
        #endregion
    }
}
