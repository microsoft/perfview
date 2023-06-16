using Microsoft.Diagnostics.Utilities;
using System;
using Xunit;

namespace TraceEventTests
{
    public class CacheTests
    {
        [Fact]
        public void ClearDisposesDisposableValues()
        {
            var cache = new Cache<int, Disposable>(maxEntries: 4);

            var one = new Disposable();
            var two = new Disposable();
            var three = new Disposable();
            var four = new Disposable();

            cache.Add(1, one);
            cache.Add(2, two);
            cache.Add(3, three);
            cache.Add(4, four);

            cache.Clear();

            Assert.True(one.IsDisposed);
            Assert.True(two.IsDisposed);
            Assert.True(three.IsDisposed);
            Assert.True(four.IsDisposed);
        }

        [Fact]
        public void AddEntryDisposesEvictedValues()
        {
            var cache = new Cache<int, Disposable>(maxEntries: 4);

            var one = new Disposable();
            var two = new Disposable();
            var three = new Disposable();
            var four = new Disposable();
            var five = new Disposable();

            cache.Add(1, one);
            cache.Add(2, two);
            cache.Add(3, three);
            cache.Add(4, four);
            cache.Add(5, five); // Evicts oldest entry

            Assert.False(cache.TryGet(1, out _));
            Assert.True(one.IsDisposed);
        }

        private class Disposable : IDisposable
        {
            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                if (IsDisposed)
                {
                    throw new ObjectDisposedException("Cannot dispose more than once.");
                }

                IsDisposed = true;
            }
        }
    }
}
