using Microsoft.Diagnostics.Tracing.EventPipe;
using System;
using System.Collections.Generic;
using Xunit;

namespace TraceEventTests
{
    public class MinHeapTests
    {
        [Fact]
        public void EmptyHeap_CountIsZero()
        {
            var heap = new EventCache.MinHeap<string>();
            Assert.Equal(0, heap.Count);
        }

        [Fact]
        public void SingleElement_PeekReturnsIt()
        {
            var heap = new EventCache.MinHeap<string>();
            heap.Add(42, "only");
            heap.Build();

            Assert.Equal(1, heap.Count);
            Assert.Equal("only", heap.PeekValue);
        }

        [Fact]
        public void Build_EstablishesMinOrder()
        {
            var heap = new EventCache.MinHeap<string>();
            heap.Add(30, "thirty");
            heap.Add(10, "ten");
            heap.Add(20, "twenty");
            heap.Build();

            Assert.Equal("ten", heap.PeekValue);
        }

        [Fact]
        public void RemoveRoot_YieldsAscendingOrder()
        {
            var heap = new EventCache.MinHeap<int>();
            heap.Add(50, 50);
            heap.Add(30, 30);
            heap.Add(40, 40);
            heap.Add(10, 10);
            heap.Add(20, 20);
            heap.Build();

            var result = new List<int>();
            while (heap.Count > 0)
            {
                result.Add(heap.PeekValue);
                heap.RemoveRoot();
            }

            Assert.Equal(new[] { 10, 20, 30, 40, 50 }, result);
        }

        [Fact]
        public void RemoveRoot_SingleElement_EmptiesHeap()
        {
            var heap = new EventCache.MinHeap<string>();
            heap.Add(1, "a");
            heap.Build();

            heap.RemoveRoot();
            Assert.Equal(0, heap.Count);
        }

        [Fact]
        public void ReplaceRoot_MaintainsHeapOrder()
        {
            var heap = new EventCache.MinHeap<string>();
            heap.Add(10, "ten");
            heap.Add(20, "twenty");
            heap.Add(30, "thirty");
            heap.Build();

            Assert.Equal("ten", heap.PeekValue);

            // Replace root (10) with a larger key (25) — "twenty" (20) should become new root.
            heap.ReplaceRoot(25, "twenty-five");
            Assert.Equal("twenty", heap.PeekValue);
        }

        [Fact]
        public void ReplaceRoot_WithSmallestKey_KeepsItAtRoot()
        {
            var heap = new EventCache.MinHeap<string>();
            heap.Add(10, "ten");
            heap.Add(20, "twenty");
            heap.Add(30, "thirty");
            heap.Build();

            // Replace root with an even smaller key — it should remain the root.
            heap.ReplaceRoot(5, "five");
            Assert.Equal("five", heap.PeekValue);
        }

        [Fact]
        public void Clear_ResetsHeap()
        {
            var heap = new EventCache.MinHeap<string>();
            heap.Add(1, "a");
            heap.Add(2, "b");
            heap.Build();

            heap.Clear();
            Assert.Equal(0, heap.Count);
        }

        [Fact]
        public void DuplicateKeys_AllElementsPreserved()
        {
            var heap = new EventCache.MinHeap<string>();
            heap.Add(10, "a");
            heap.Add(10, "b");
            heap.Add(10, "c");
            heap.Build();

            var result = new List<string>();
            while (heap.Count > 0)
            {
                result.Add(heap.PeekValue);
                heap.RemoveRoot();
            }

            Assert.Equal(3, result.Count);
            result.Sort();
            Assert.Equal(new[] { "a", "b", "c" }, result);
        }

        [Fact]
        public void LargeHeap_ExtractsInOrder()
        {
            var heap = new EventCache.MinHeap<int>();
            var rng = new Random(12345);
            var expected = new List<int>();

            for (int i = 0; i < 1000; i++)
            {
                int val = rng.Next(0, 100000);
                heap.Add(val, val);
                expected.Add(val);
            }

            heap.Build();
            expected.Sort();

            var result = new List<int>();
            while (heap.Count > 0)
            {
                result.Add(heap.PeekValue);
                heap.RemoveRoot();
            }

            Assert.Equal(expected, result);
        }

        [Fact]
        public void AlreadySorted_ExtractsInOrder()
        {
            var heap = new EventCache.MinHeap<int>();
            for (int i = 1; i <= 10; i++)
            {
                heap.Add(i, i);
            }
            heap.Build();

            var result = new List<int>();
            while (heap.Count > 0)
            {
                result.Add(heap.PeekValue);
                heap.RemoveRoot();
            }

            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, result);
        }

        [Fact]
        public void ReverseSorted_ExtractsInOrder()
        {
            var heap = new EventCache.MinHeap<int>();
            for (int i = 10; i >= 1; i--)
            {
                heap.Add(i, i);
            }
            heap.Build();

            var result = new List<int>();
            while (heap.Count > 0)
            {
                result.Add(heap.PeekValue);
                heap.RemoveRoot();
            }

            Assert.Equal(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }, result);
        }

        [Fact]
        public void MixedOperations_RemoveAndReplace()
        {
            var heap = new EventCache.MinHeap<string>();
            heap.Add(10, "ten");
            heap.Add(20, "twenty");
            heap.Add(30, "thirty");
            heap.Add(40, "forty");
            heap.Build();

            // Extract min (10), then replace root with 25.
            Assert.Equal("ten", heap.PeekValue);
            heap.RemoveRoot();
            Assert.Equal("twenty", heap.PeekValue);
            heap.ReplaceRoot(25, "twenty-five");

            // Now heap has: 25, 30, 40. Min should be 25.
            Assert.Equal("twenty-five", heap.PeekValue);

            var result = new List<string>();
            while (heap.Count > 0)
            {
                result.Add(heap.PeekValue);
                heap.RemoveRoot();
            }
            Assert.Equal(new[] { "twenty-five", "thirty", "forty" }, result);
        }
    }
}
