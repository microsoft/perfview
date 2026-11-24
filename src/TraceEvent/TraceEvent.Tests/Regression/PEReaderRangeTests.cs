using PEFile;
using System;
using System.IO;
using Xunit;

namespace TraceEventTests.Regression
{
    public unsafe class PEReaderRangeTests
    {
        [Fact]
        public void InvalidRangesNeverReturnPointers()
        {
            IsolatedTest.Run(() =>
            {
                using (var stream = new MemoryStream(new byte[32]))
                using (var reader = new PEBufferedReader(stream, 16))
                {
                    reader.Fetch(0, 16);
                    Assert.Throws<EndOfStreamException>(() => (IntPtr)reader.Fetch(int.MaxValue - 3, 8));
                    Assert.Throws<EndOfStreamException>(() => (IntPtr)reader.Fetch(0, int.MaxValue));
                    foreach (var range in new[] { new[] { -1, 1 }, new[] { 0, -1 }, new[] { int.MinValue, 0 } })
                    {
                        Assert.Throws<InvalidDataException>(() => (IntPtr)reader.Fetch(range[0], range[1]));
                        Assert.Throws<InvalidDataException>(() => reader.FetchSpan(range[0], range[1]).Length);
                    }
                }
            }, GetType());
        }

        [Fact]
        public void PointerReadsRequireCompleteBytesButSpansMayBePartial()
        {
            IsolatedTest.Run(() =>
            {
                using (var stream = new MemoryStream(new byte[] { 1, 2, 3, 4 }))
                using (var reader = new PEBufferedReader(stream, 8))
                {
                    Assert.Equal(new byte[] { 1, 2, 3, 4 }, reader.FetchSpan(0, 1024).ToArray());
                    Assert.Throws<EndOfStreamException>(() => (IntPtr)reader.Fetch(2, 4));
                    Assert.Equal(new byte[] { 3, 4 }, reader.FetchSpan(2, 4).ToArray());
                    Assert.Equal(4, *reader.Fetch(3, 1));
                    Assert.Equal(0, reader.FetchSpan(4, 1).Length);
                }
            }, GetType());
        }

        [Fact]
        public void CacheHitsReplacementAndShortReadsPreserveOffsets()
        {
            IsolatedTest.Run(() =>
            {
                byte[] bytes = { 0, 1, 2, 3, 4, 5, 6, 7 };
                using (var stream = new ShortReadStream(bytes))
                using (var reader = new PEBufferedReader(stream, 4))
                {
                    Assert.Equal(2, reader.Fetch(1, 3)[1]);
                    Assert.Equal(new byte[] { 2, 3 }, reader.FetchSpan(2, 2).ToArray());
                    var slice = reader.EnsureRead(2, 2);
                    Assert.Equal(new byte[] { 2, 3 }, slice.AsSpan().ToArray());
                    Assert.Equal(new byte[] { 4, 5, 6, 7 }, reader.FetchSpan(4, 16).ToArray());
                    Assert.Equal(7, *reader.Fetch(7, 1));
                    Assert.Throws<EndOfStreamException>(() => (IntPtr)reader.Fetch(7, 2));
                }
                using (var stream = new ShortReadStream(bytes, 64))
                using (var reader = new PEBufferedReader(stream))
                {
                    Assert.Throws<EndOfStreamException>(() => (IntPtr)reader.Fetch(0, 16));
                }
            }, GetType());
        }

        #region private
        private sealed class ShortReadStream : MemoryStream
        {
            public ShortReadStream(byte[] bytes, long? reportedLength = null) : base(bytes)
            {
                m_reportedLength = reportedLength;
            }

            public override long Length => m_reportedLength ?? base.Length;
            public override int Read(byte[] buffer, int offset, int count) =>
                base.Read(buffer, offset, Math.Min(2, count));

            private readonly long? m_reportedLength;
        }
        #endregion
    }
}
