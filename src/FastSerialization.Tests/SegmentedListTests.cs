using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FastSerializationTests
{
    /// <summary>
    /// Tests for SegmentedList&lt;T&gt;
    /// </summary>
    public class SegmentedListTests
    {
        [Fact]
        public void ConstructorWithSegmentSize()
        {
            var list = new SegmentedList<int>(16);
            Assert.Equal(0, list.Count);
        }

        [Fact]
        public void ConstructorWithInvalidSegmentSize()
        {
            // Segment size must be power of 2 greater than 1
            Assert.Throws<ArgumentOutOfRangeException>(() => new SegmentedList<int>(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SegmentedList<int>(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SegmentedList<int>(15));
        }

        [Fact]
        public void ConstructorWithValidSegmentSizes()
        {
            // Should not throw for valid power of 2 segment sizes
            var list2 = new SegmentedList<int>(2);
            var list4 = new SegmentedList<int>(4);
            var list8 = new SegmentedList<int>(8);
            var list16 = new SegmentedList<int>(16);
            var list1024 = new SegmentedList<int>(1024);
            
            Assert.Equal(0, list2.Count);
            Assert.Equal(0, list4.Count);
            Assert.Equal(0, list8.Count);
            Assert.Equal(0, list16.Count);
            Assert.Equal(0, list1024.Count);
        }

        [Fact]
        public void AddItems()
        {
            var list = new SegmentedList<int>(4);
            list.Add(1);
            list.Add(2);
            list.Add(3);
            
            Assert.Equal(3, list.Count);
            Assert.Equal(1, list[0]);
            Assert.Equal(2, list[1]);
            Assert.Equal(3, list[2]);
        }

        [Fact]
        public void AddItemsAcrossSegments()
        {
            var list = new SegmentedList<int>(4);
            for (int i = 0; i < 10; i++)
            {
                list.Add(i);
            }
            
            Assert.Equal(10, list.Count);
            for (int i = 0; i < 10; i++)
            {
                Assert.Equal(i, list[i]);
            }
        }

        [Fact]
        public void Indexer()
        {
            var list = new SegmentedList<string>(8);
            list.Add("First");
            list.Add("Second");
            list.Add("Third");
            
            Assert.Equal("First", list[0]);
            Assert.Equal("Second", list[1]);
            Assert.Equal("Third", list[2]);
            
            list[1] = "Modified";
            Assert.Equal("First", list[0]);
            Assert.Equal("Modified", list[1]);
            Assert.Equal("Third", list[2]);
        }

        [Fact]
        public void Clear()
        {
            var list = new SegmentedList<int>(4);
            list.Add(1);
            list.Add(2);
            list.Add(3);
            
            list.Clear();
            Assert.Equal(0, list.Count);
        }

        [Fact]
        public void Contains()
        {
            var list = new SegmentedList<string>(4);
            list.Add("Apple");
            list.Add("Banana");
            list.Add("Cherry");
            
            Assert.Contains("Apple", list);
            Assert.Contains("Banana", list);
            Assert.DoesNotContain("Date", list);
        }

        [Fact]
        public void CopyTo()
        {
            var list = new SegmentedList<int>(4);
            for (int i = 0; i < 7; i++)
            {
                list.Add(i);
            }
            
            int[] array = new int[10];
            list.CopyTo(array, 2);
            
            Assert.Equal(0, array[0]);
            Assert.Equal(0, array[1]);
            for (int i = 0; i < 7; i++)
            {
                Assert.Equal(i, array[i + 2]);
            }
            Assert.Equal(0, array[9]);
        }

        [Fact]
        public void GetEnumerator()
        {
            var list = new SegmentedList<int>(4);
            for (int i = 0; i < 10; i++)
            {
                list.Add(i);
            }
            
            int count = 0;
            foreach (var item in list)
            {
                Assert.Equal(count, item);
                count++;
            }
            Assert.Equal(10, count);
        }

        [Fact]
        public void LargeList()
        {
            var list = new SegmentedList<int>(64);
            for (int i = 0; i < 1000; i++)
            {
                list.Add(i);
            }
            
            Assert.Equal(1000, list.Count);
            for (int i = 0; i < 1000; i++)
            {
                Assert.Equal(i, list[i]);
            }
        }

        [Fact]
        public void SetCount()
        {
            var list = new SegmentedList<int>(4);
            list.Add(1);
            list.Add(2);
            
            list.Count = 5;
            Assert.Equal(5, list.Count);
            
            list.Count = 1;
            Assert.Equal(1, list.Count);
        }
    }
}
