using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClrProfiler;

namespace LinuxPerfView.Shared
{
	internal static class FastStreamExtension
	{
		internal static void ReadBytesUpTo(this FastStream stream, char c, byte[] bytes, out int length)
		{
			int numbytes = 0;
			while (stream.Current != c && numbytes < bytes.Length)
			{
				bytes[numbytes++] = stream.Current;
				stream.MoveNext();
			}

			length = numbytes;
		}

		internal static void ReadAsciiStringUpToLastOnLine(this FastStream stream, char c, StringBuilder sb)
		{
			StringBuilder buffer = new StringBuilder();
			FastStream.MarkedPosition mp = stream.MarkPosition();

			while (stream.Current != '\n' && !stream.EndOfStream)
			{
				if (stream.Current == c)
				{
					sb.Append(buffer);
					buffer.Clear();
					mp = stream.MarkPosition();
				}

				buffer.Append((char)stream.Current);
				stream.MoveNext();
			}

			stream.RestoreToMark(mp);
		}

		internal static void ReadAsciiStringUpToWhiteSpace(this FastStream stream, StringBuilder sb)
		{
			while (!char.IsWhiteSpace((char)stream.Current))
			{
				sb.Append((char)stream.Current);
				if (!stream.MoveNext())
				{
					break;
				}
			}
		}
	}
}
