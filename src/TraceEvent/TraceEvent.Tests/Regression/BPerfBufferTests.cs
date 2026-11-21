using Microsoft.Diagnostics.Tracing;
using System;
using System.IO;
using Xunit;

namespace TraceEventTests.Regression
{
    public class BPerfBufferTests
    {
        [Fact]
        public void DelegateReceivesActualCapacity()
        {
            bool called = false;
            WithFile(Block(112, 112), path =>
            {
                using (new BPerfEventSource(path, null, new byte[112], new byte[128],
                    decompressDelegate: (input, offset, length, output, capacity) =>
                    {
                        called = true;
                        Assert.Equal(output.Length, capacity);
                        Array.Copy(input, offset, output, 0, length);
                        return length;
                    })) { }
            });
            Assert.True(called);
        }

        [Fact]
        public void CompleteBlocksSurviveReadAheadBoundaries()
        {
            byte[] block = Block(112, 112);
            byte[] bytes = new byte[block.Length * 3];
            for (int i = 0; i < 3; i++)
            {
                block.CopyTo(bytes, i * block.Length);
            }
            int calls = 0;
            WithFile(bytes, path =>
            {
                using (new BPerfEventSource(path, null, new byte[112], new byte[128],
                    decompressDelegate: (input, offset, length, output, capacity) =>
                    {
                        calls++;
                        Array.Copy(input, offset, output, 0, length);
                        return length;
                    })) { }
            });
            Assert.Equal(3, calls);
        }

        [Fact]
        public void InvalidBlockLengthsAreRejectedBeforeDecompression()
        {
            foreach (var lengths in new[] { new[] { -1, 0 }, new[] { 0, -1 }, new[] { 0, 113 } })
            {
                WithFile(Block(lengths[0], lengths[1]), path =>
                    Assert.Throws<InvalidDataException>(() => new BPerfEventSource(path, null,
                        new byte[112], new byte[128],
                        decompressDelegate: (input, offset, length, output, capacity) =>
                            throw new InvalidOperationException("Delegate must not be called."))));
            }
        }

        [Fact]
        public void IncompleteBlocksAreRejectedAtEndOfFile()
        {
            foreach (byte[] bytes in new[] { new byte[1], new byte[7], Block(1, 0), Block(128, 112, 112) })
            {
                WithFile(bytes, path => Assert.Throws<InvalidDataException>(() =>
                    new BPerfEventSource(path, null, new byte[112], new byte[128],
                        decompressDelegate: (input, offset, length, output, capacity) => 0)));
            }
        }

        [Fact]
        public void InvalidDelegateResultsAreRejected()
        {
            foreach (int result in new[] { -1, 113, int.MaxValue, 0 })
            {
                WithFile(Block(1, 112, 1), path => Assert.Throws<InvalidDataException>(() =>
                    new BPerfEventSource(path, null, new byte[112], new byte[128],
                        decompressDelegate: (input, offset, length, output, capacity) => result)));
            }
        }

        [Fact]
        public void CallerBuffersAreValidated()
        {
            WithFile(new byte[0], path =>
            {
                Assert.Throws<ArgumentNullException>(() => new BPerfEventSource(path, null, null, new byte[8]));
                Assert.Throws<ArgumentNullException>(() => new BPerfEventSource(path, null, new byte[1], null));
                Assert.Throws<ArgumentException>(() => new BPerfEventSource(path, null, new byte[0], new byte[8]));
                Assert.Throws<ArgumentException>(() => new BPerfEventSource(path, null, new byte[1], new byte[7]));
            });
        }

        [Fact]
        public void DecompressorValidatesSlicesAndAcceptsExactFit()
        {
            IsolatedTest.Run(() =>
            {
                byte[] input = { 0x20, 0x42 };
                byte[] output = new byte[1];
                Assert.Throws<ArgumentNullException>(() => BPerfEventSource.ULZ777Decompress(null, 0, 0, output, 1));
                Assert.Throws<ArgumentNullException>(() => BPerfEventSource.ULZ777Decompress(input, 0, 2, null, 0));
                Assert.Throws<ArgumentOutOfRangeException>(() => BPerfEventSource.ULZ777Decompress(input, 0, 2, output, 2));
                Assert.Throws<ArgumentOutOfRangeException>(() => BPerfEventSource.ULZ777Decompress(input, 1, int.MaxValue, output, 1));
                Assert.Throws<ArgumentOutOfRangeException>(() => BPerfEventSource.ULZ777Decompress(input, -1, 1, output, 1));
                Assert.Equal(1, BPerfEventSource.ULZ777Decompress(input, 0, 2, output, 1));
                Assert.Equal(0x42, output[0]);
                Assert.Equal(0, BPerfEventSource.ULZ777Decompress(input, input.Length, 0, new byte[0], 0));
                foreach (byte[] truncated in new[] { new byte[] { 0 }, new byte[] { 0xe0 }, new byte[] { 0x0f } })
                {
                    Assert.Equal(-1, BPerfEventSource.ULZ777Decompress(truncated, 0, truncated.Length, new byte[32], 32));
                }
            }, GetType());
        }

        #region private
        private static byte[] Block(int compressed, int uncompressed, int bytes = 0)
        {
            if (bytes == 0 && compressed > 0 && compressed == uncompressed)
            {
                bytes = compressed;
            }
            byte[] result = new byte[8 + bytes];
            BitConverter.GetBytes(compressed).CopyTo(result, 0);
            BitConverter.GetBytes(uncompressed).CopyTo(result, 4);
            return result;
        }

        private static void WithFile(byte[] bytes, Action<string> test)
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".btl");
            try
            {
                File.WriteAllBytes(path, bytes);
                test(path);
            }
            finally
            {
                File.Delete(path);
            }
        }
        #endregion
    }
}
