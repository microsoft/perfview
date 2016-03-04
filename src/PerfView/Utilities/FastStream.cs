using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;

namespace PerfView.Utilities
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
		public const uint MaxRestoreLength = 256;

		public FastStream(string filePath)
			: this(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
		{
		}

		// Allows for a byte array while keeping a stream
		public FastStream(byte[] buffer, int length) :
			this(buffer, 0, length)
		{
		}

		public FastStream(byte[] buffer, int start, int length)
		{
			this.stream = Stream.Null;
			this.streamReadIn = (uint)length;
			this.bufferFillPos = MaxRestoreLength + 1 + this.streamReadIn;
			this.buffer = new byte[this.bufferFillPos];
			this.bufferIndex = MaxRestoreLength;
			Buffer.BlockCopy(src: buffer, srcOffset: start, dst: this.buffer, dstOffset: (int)this.bufferIndex + 1, count: length);
			this.buffer[this.bufferIndex] = 0;
			this.streamPosition = this.streamReadIn;
		}

		public FastStream(Stream stream, int bufferSize = 262144, bool closeStream = false)
		{
			this.stream = stream;
			this.closeStream = closeStream;
			this.buffer = new byte[bufferSize];
			this.bufferFillPos = 1;
			this.bufferIndex = 0;
			this.streamReadIn = 1;
			this.streamPosition = 0;
		}

		public int MaxPeek => this.buffer.Length - (int)MaxRestoreLength;

		/// <summary>
		/// For efficient reads, we allow you to read Current past the end of the stream.  You will
		/// get the 'Sentinal' value in that case.  This defaults to 0, but you can change it if 
		/// there is a better 'rare' value to use as an end of stream marker.  
		/// </summary>
		public byte Sentinal = 0;
		public long Position
		{
			get
			{
				return this.streamPosition - (this.streamReadIn - (this.bufferIndex - MaxRestoreLength));
			}
		}

		public bool MoveNext()
		{
			bufferIndex++;
			bool ret = true;
			if (this.bufferIndex >= this.bufferFillPos)
			{
				ret = this.MoveNextHelper();
			}

#if DEBUG
            nextChars = Encoding.Default.GetString(buffer, (int)bufferReadPos, Math.Min(40, buffer.Length - (int)bufferReadPos));
#endif
			return ret;
		}

		/// <summary>
		/// Returns a number of bytes ahead without advancing the pointer. 
		/// Peek(0) is the same as calling Current.  
		/// </summary>
		/// <param name="bytesAhead"></param>
		/// <returns></returns>
		public byte Peek(uint bytesAhead)
		{
			uint peekIndex = bytesAhead + this.bufferIndex;
			if (peekIndex >= this.bufferFillPos)
			{
				peekIndex = this.PeekHelper(bytesAhead);
			}

			return this.buffer[peekIndex];
		}

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
			return new MarkedPosition(this.Position);
		}

		public void RestoreToMark(MarkedPosition position)
		{
			long delta = this.Position - position.streamPos;
			if (delta > MaxRestoreLength)
			{
				this.stream.Position = this.streamPosition = position.streamPos;
				this.FillBufferFromStreamPosition();
			}
			else
			{
				this.bufferIndex -= (uint)delta;
			}
		}

		public byte Current { get { return buffer[this.bufferIndex]; } }

		public byte ReadChar()
		{
			this.MoveNext();
			return Current;
		}
		public int ReadInt()
		{
			byte c = Current;
			while (c == ' ')
				c = ReadChar();
			bool negative = false;
			if (c == '-')
			{
				negative = true;
				c = ReadChar();
			}
			if (c >= '0' && c <= '9')
			{
				int value = 0;
				if (c == '0')
				{
					c = ReadChar();
					if (c == 'x' || c == 'X')
					{
						MoveNext();
						value = ReadHex();
					}
				}
				while (c >= '0' && c <= '9')
				{
					value = value * 10 + c - '0';
					c = ReadChar();
				}

				if (negative)
					value = -value;
				return value;
			}
			else
			{
				return -1;
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
				c = ReadChar();
			bool negative = false;
			if (c == '-')
			{
				negative = true;
				c = ReadChar();
			}
			if (c >= '0' && c <= '9')
			{
				long value = 0;
				if (c == '0')
				{
					c = ReadChar();
					if (c == 'x' || c == 'X')
					{
						MoveNext();
						value = ReadLongHex();
					}
				}
				while (c >= '0' && c <= '9')
				{
					value = value * 10 + c - '0';
					c = ReadChar();
				}

				if (negative)
					value = -value;
				return value;
			}
			else
			{
				return -1;
			}
		}
		public ulong ReadULong()
		{
			return (ulong)ReadLong();
		}
		public bool EndOfStream { get { return this.streamReadIn == 0; } }
		public void ReadAsciiStringUpTo(char endMarker, StringBuilder sb)
		{
			for (;;)
			{
				byte c = Current;
				if (c == endMarker)
					break;
				sb.Append((char)c);
				if (!MoveNext())
					break;
			}
		}
		public void ReadAsciiStringUpTo(string endMarker, StringBuilder sb)
		{
			Debug.Assert(0 < endMarker.Length);
			for (;;)
			{
				ReadAsciiStringUpTo(endMarker[0], sb);
				uint markerIdx = 1;
				for (;;)
				{
					if (markerIdx >= endMarker.Length)
						return;
					if (Peek(markerIdx) != endMarker[(int)markerIdx])
						break;
					markerIdx++;
				}
				MoveNext();
			}
		}
		public void SkipUpTo(char endMarker)
		{
			while (Current != endMarker)
			{
				if (!MoveNext())
					break;
			}
		}
		public void SkipSpace()
		{
			while (Current == ' ')
				MoveNext();
		}
		public void SkipWhiteSpace()
		{
			while (Char.IsWhiteSpace((char)Current))
				MoveNext();
		}

		public void SkipUpToFalse(Func<byte, bool> predicate)
		{
			while (predicate(this.Current))
			{
				if (!MoveNext())
				{
					break;
				}
			}
		}

		/// <summary>
		/// Reads the string into the stringBuilder until a byte is read that
		/// is one of the characters in 'endMarkers'.  
		/// </summary>
		public void ReadAsciiStringUpToAny(string endMarkers, StringBuilder sb)
		{
			for (;;)
			{
				byte c = Current;
				for (int i = 0; i < endMarkers.Length; i++)
					if (c == endMarkers[i])
						return;
				sb.Append((char)c);
				if (!MoveNext())
					break;
			}
		}

		/// <summary>
		/// Reads the stream into the string builder until the last end marker on the line is hit.
		/// </summary>
		public void ReadAsciiStringUpToLastBeforeTrue(char endMarker, StringBuilder sb, Func<byte, bool> predicate)
		{
			StringBuilder buffer = new StringBuilder();
			MarkedPosition mp = this.MarkPosition();

			while (predicate(this.Current) && !this.EndOfStream)
			{
				if (this.Current == endMarker)
				{
					sb.Append(buffer);
					buffer.Clear();
					mp = this.MarkPosition();
				}

				buffer.Append((char)this.Current);
				this.MoveNext();
			}

			this.RestoreToMark(mp);
		}

		/// <summary>
		/// Reads the stream in the string builder until the given predicate function is false.
		/// </summary>
		public void ReadAsciiStringUpToTrue(StringBuilder sb, Func<byte, bool> predicate)
		{
			while (predicate(this.Current))
			{
				sb.Append((char)this.Current);
				if (!this.MoveNext())
				{
					break;
				}
			}
		}

		public void Skip(uint amount)
		{
			while (amount >= this.bufferFillPos - this.bufferIndex)
			{
				if (this.EndOfStream)
				{
					return;
				}
				amount -= this.bufferFillPos - this.bufferIndex;
				this.bufferIndex = this.FillBufferFromStreamPosition();
			}

			this.bufferIndex += amount;
		}

		public int CopyBytes(int length, byte[] buffer)
		{
			return this.CopyBytes(0, length, buffer);
		}

		public int CopyBytes(int start, int length, byte[] buffer)
		{
			if (this.bufferIndex + start + length >= this.bufferFillPos)
			{
				this.bufferIndex = this.FillBufferFromStreamPosition(keepLast: this.bufferFillPos - this.bufferIndex);
			}

			if (this.bufferFillPos - (this.bufferIndex + start) < length)
			{
				length = (int)(this.bufferFillPos - (this.bufferIndex + start));
			}

			Buffer.BlockCopy(this.buffer, (int)this.bufferIndex + start, buffer, 0, length);

			return length;
		}

		public int ReadHex()
		{
			int value = 0;
			while (true)
			{
				int digit = Current;
				if (digit >= '0' && digit <= '9')
					digit -= '0';
				else if (digit >= 'a' && digit <= 'f')
					digit -= 'a' - 10;
				else if (digit >= 'A' && digit <= 'F')
					digit -= 'A' - 10;
				else
					return value;
				MoveNext();
				value = value * 16 + digit;
			}
		}

		public long ReadLongHex()
		{
			long value = 0;
			while (true)
			{
				int digit = Current;
				if (digit >= '0' && digit <= '9')
					digit -= '0';
				else if (digit >= 'a' && digit <= 'f')
					digit -= 'a' - 10;
				else if (digit >= 'A' && digit <= 'F')
					digit -= 'A' - 10;
				else
					return value;
				MoveNext();
				value = value * 16 + digit;
			}
		}

		public void Dispose()
		{
			if (this.closeStream)
			{
				this.stream?.Dispose();
				this.stream = null;
			}
		}

		/// <summary>
		/// Gets a string from the position to the length indicated (for debugging)
		/// </summary>
		internal string PeekString(int length)
		{
			return this.PeekString(0, length);
		}

		internal string PeekString(int start, int length)
		{
			StringBuilder sb = new StringBuilder();
			for (uint i = this.bufferIndex + (uint)start; i < this.bufferIndex + length + start && i < this.bufferFillPos - 1; i++)
			{
				sb.Append((char)this.Peek(i + (uint)start - this.bufferIndex));
			}

			return sb.ToString();
		}

		#region privateMethods
		private uint FillBufferFromStreamPosition(uint keepLast = 0)
		{
			// This is so the first 'keepFromBack' integers are read in again.
			uint preamble = MaxRestoreLength + keepLast;
			for (int i = 0; i < preamble; i++)
			{
				if (this.bufferFillPos - (preamble - i) < 0)
				{
					buffer[i] = 0;
					continue;
				}

				this.buffer[i] = this.buffer[bufferFillPos - (preamble - i)];
			}

			this.streamReadIn = (uint)stream.Read(this.buffer, (int)preamble, this.buffer.Length - (int)preamble);
			this.bufferFillPos = this.streamReadIn + preamble;
			this.streamReadIn += keepLast;
			this.streamPosition += this.streamReadIn > 0 ? this.streamReadIn : 1;
			if (this.bufferFillPos < this.buffer.Length)
				this.buffer[this.bufferFillPos] = this.Sentinal;    // we define 0 as the value you get after EOS.

			return MaxRestoreLength;
		}

		private bool MoveNextHelper()
		{
			this.bufferIndex = this.FillBufferFromStreamPosition();
			return (this.streamReadIn > 0);
		}

		private uint PeekHelper(uint bytesAhead)
		{
			if (bytesAhead >= this.buffer.Length - MaxRestoreLength)
				throw new Exception("Can only peek ahead the length of the buffer");

			// We keep everything above the index.
			this.bufferIndex = this.FillBufferFromStreamPosition(keepLast: this.bufferFillPos - this.bufferIndex);

			return bytesAhead + this.bufferIndex;
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

#if DEBUG
        string nextChars;
        public override string ToString()
        {
            return nextChars;
        }
#endif
		#endregion
	}
}
