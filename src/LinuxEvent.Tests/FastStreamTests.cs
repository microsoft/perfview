using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClrProfiler;
using LinuxTracing.Tests;
using PerfView.Utilities;
using Xunit;

namespace LinuxTracing.Tests
{
	public class FastStreamTests
	{
		private FastStream GetTestStream()
		{
			return new FastStream(Constants.GetTestingFilePath("faststream.txt"));
		}

		[Fact]
		public void BasicRead()
		{
			FastStream stream = this.GetTestStream();
			SkipBOM(stream);
			Assert.Equal("1", ((char)stream.Current).ToString());
			
		}

		[Fact]
		public void BasicReadLine()
		{
			FastStream stream = this.GetTestStream();
			SkipBOM(stream);
			StringBuilder sb = new StringBuilder();
			stream.ReadAsciiStringUpTo('\n', sb);
			Assert.Equal("12345\r", sb.ToString());
		}

		[Fact]
		public void ShortRestore()
		{
			FastStream stream = this.GetTestStream();
			SkipBOM(stream);
			var mp = stream.MarkPosition();
			stream.MoveNext();
			stream.RestoreToMark(mp);
			Assert.Equal("1", ((char)stream.Current).ToString());
		}

		[Fact]
		public void LongerRestore()
		{
			FastStream stream = this.GetTestStream();
			SkipBOM(stream);
			var mp = stream.MarkPosition();
			stream.SkipUpTo('a');
			stream.RestoreToMark(mp);
			Assert.Equal("1", ((char)stream.Current).ToString());
		}

		[Fact]
		public void LongRestoreAndMove()
		{
			FastStream stream = this.GetTestStream();
			SkipBOM(stream);
			var mp = stream.MarkPosition();
			stream.SkipUpTo('a');
			stream.RestoreToMark(mp);
			stream.MoveNext(); stream.MoveNext(); stream.MoveNext();
			Assert.Equal("4", ((char)stream.Current).ToString());
		}

		[Fact]
		public void PeekingWhileEnough()
		{
			FastStream stream = this.GetTestStream();
			SkipBOM(stream);
			Assert.Equal("2", ((char)stream.Peek(1)).ToString());
		}

		[Fact]
		public void PeekingWithNoBuffer()
		{
			FastStream stream = this.GetTestStream();
			Assert.Equal("1", ((char)stream.Peek(4)).ToString());
		}

		[Fact]
		public void LongRestoreAndMovePastHistory()
		{
			FastStream stream = this.GetTestStream();
			SkipBOM(stream);
			var mp = stream.MarkPosition();
			stream.SkipUpTo('a');
			stream.RestoreToMark(mp);
			StringBuilder sb = new StringBuilder();
			stream.ReadAsciiStringUpTo('b', sb);
			Assert.Equal("12345\r\n6789a\r\n", sb.ToString());
		}

		[Fact]
		public void LongRestoreAndRestoreBeforeOut()
		{
			FastStream stream = this.GetTestStream();
			SkipBOM(stream);
			var mp = stream.MarkPosition();
			stream.SkipUpTo('a');
			stream.RestoreToMark(mp);
			mp = stream.MarkPosition();
			stream.SkipUpTo('7');
			stream.RestoreToMark(mp);
			StringBuilder sb = new StringBuilder();
			stream.ReadAsciiStringUpTo('b', sb);
			Assert.Equal("12345\r\n6789a\r\n", sb.ToString());
		}

		private static void SkipBOM(FastStream stream)
		{
			stream.MoveNext();
			stream.MoveNext();
			stream.MoveNext();
			stream.MoveNext();
		}
	}
}
