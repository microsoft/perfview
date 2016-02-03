using System;
using System.Collections.Generic;
using System.Diagnostics;
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
	public sealed class FastStream
	{
		public FastStream(string filePath)
			: this(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
		{
		}
		public FastStream(Stream stream)
		{
			buffer = new byte[16384];
			bufferReadPos = 1;          // We make this 1, 1 initially so that EndOfStream works before MoveNext is called
			bufferFillPos = 1;
			this.stream = stream;
		}

		public long Position { get; private set; }

		public struct MarkedPosition
		{
			internal byte[] buffer;
			internal uint bufferReadPos;
			internal uint bufferFillPos;
			internal long streamPos;

			public MarkedPosition(byte[] buffer, uint readPos, uint fillPos, long streamPos)
			{
				this.buffer = buffer;
				this.bufferReadPos = readPos;
				this.bufferFillPos = fillPos;
				this.streamPos = streamPos;
			}
		}

		public MarkedPosition MarkPosition()
		{
			byte[] tempBuffer = null;
			if (!markBufferUsed)
			{
				markBufferUsed = true;
				if (markBuffer == null)
					markBuffer = new byte[buffer.Length];
				tempBuffer = markBuffer;
			}
			else
			{
				tempBuffer = new byte[bufferFillPos];
			}
			Array.Copy(buffer, tempBuffer, bufferFillPos);
			return new MarkedPosition(tempBuffer, bufferReadPos, bufferFillPos, this.Position);
		}

		public void RestoreToMark(MarkedPosition position)
		{
			long delta = this.Position - position.streamPos;
			if (delta > MaxRestoreLength)
			{
				this.stream.Position = position.streamPos;
				bufferFillPos = position.bufferFillPos;
				bufferReadPos = position.bufferReadPos;
				Array.Copy(position.buffer, buffer, bufferFillPos);
				if (markBufferUsed)
				{
					if (Object.ReferenceEquals(position.buffer, buffer))
						markBufferUsed = false;
				}
			}
			else
			{
				this.bufferReadPos -= (uint)delta;
			}

			this.Position = position.streamPos;
		}

		public byte Current { get { return buffer[bufferReadPos]; } }

		public const int MaxRestoreLength = 256;

		public bool MoveNext()
		{
			IncReadPos();
			bool ret = true;
			if (bufferReadPos >= bufferFillPos)
				ret = MoveNextHelper();

#if DEBUG
            nextChars = Encoding.Default.GetString(buffer, (int)bufferReadPos, Math.Min(40, buffer.Length - (int)bufferReadPos));
#endif
			return ret;
		}
		public byte ReadChar()
		{
			MoveNext();
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
		public bool EndOfStream { get { return bufferFillPosReadIn == 0; } }
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
		public void ReadAsciiStringUpToLastOnLine(char endMarker, StringBuilder sb)
		{
			StringBuilder buffer = new StringBuilder();
			MarkedPosition mp = this.MarkPosition();

			while (this.Current != '\n' && !this.EndOfStream)
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
			while (!predicate(this.Current))
			{
				sb.Append((char)this.Current);
				if (!this.MoveNext())
				{
					break;
				}
			}
		}

		/// <summary>
		/// Returns a number of bytes ahead without advancing the pointer. 
		/// Peek(0) is the same as calling Current.  
		/// </summary>
		/// <param name="bytesAhead"></param>
		/// <returns></returns>
		public byte Peek(uint bytesAhead)
		{
			uint index = bytesAhead + bufferReadPos;
			if (index >= bufferFillPos)
				index = PeekHelper(bytesAhead);

			return buffer[index];
		}

		public Stream BaseStream { get { return stream; } }
		/// <summary>
		/// For efficient reads, we allow you to read Current past the end of the stream.  You will
		/// get the 'Sentinal' value in that case.  This defaults to 0, but you can change it if 
		/// there is a better 'rare' value to use as an end of stream marker.  
		/// </summary>
		public byte Sentinal = 0;

		#region privateMethods
		private bool MoveNextHelper()
		{
			bufferReadPos = MaxRestoreLength;
			for (int i = 0; i < MaxRestoreLength; i++)
			{
				if (bufferFillPos - (MaxRestoreLength - i) < 0)
				{
					buffer[i] = 0;
					continue;
				}

				buffer[i] = buffer[bufferFillPos - (MaxRestoreLength - i)];
			}
			bufferFillPosReadIn = (uint)stream.Read(buffer, MaxRestoreLength, buffer.Length - MaxRestoreLength);
			bufferFillPos = bufferFillPosReadIn + MaxRestoreLength;
			if (bufferFillPos < buffer.Length)
				buffer[bufferFillPos] = Sentinal;       // we define 0 as the value you get after EOS.  
			return (bufferFillPosReadIn > 0);
		}

		private uint PeekHelper(uint bytesAhead)
		{
			if (bytesAhead >= buffer.Length)
				throw new Exception("Can only peek ahead the length of the buffer");

			// Copy down the remaining characters. 
			bufferFillPos = bufferFillPos - bufferReadPos;
			for (uint i = 0; i < bufferFillPos; i++)
				buffer[i] = buffer[bufferReadPos + i];
			bufferReadPos = 0;

			// Fill up the buffer as much as we can.  
			for (;;)
			{
				uint count = (uint)stream.Read(buffer, (int)bufferFillPos, buffer.Length - (int)bufferFillPos);
				bufferFillPos += count;
				if (bufferFillPos < buffer.Length)
					buffer[bufferFillPos] = Sentinal;

				if (bufferFillPos > bytesAhead)
					break;
				if (count == 0)
					break;
			}
			return bytesAhead;
		}

		// Only here to 'trick' the JIT compiler into inlining MoveNext.  (we were a bit over the 32 byte IL limit). 
		private void IncReadPos()
		{
			bufferReadPos++;
			this.Position++;
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

		#endregion
		#region privateState
		byte[] markBuffer;
		bool markBufferUsed;
		readonly byte[] buffer;
		uint bufferReadPos;      // The next character to read
		uint bufferFillPos;      // The last character in the buffer that is valid
		uint bufferFillPosReadIn;
		Stream stream;
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
