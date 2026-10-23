using System;
using System.Collections.Generic;
using Xunit;

namespace FastSerializationTests
{
    /// <summary>
    /// Tests for GrowableArray&lt;T&gt;
    /// </summary>
    public class GrowableArrayTests
    {
        [Fact]
        public void DefaultConstructor()
        {
            var array = new GrowableArray<int>();
            Assert.Equal(0, array.Count);
        }

        [Fact]
        public void InitialSizeConstructor()
        {
            var array = new GrowableArray<int>(10);
            Assert.Equal(0, array.Count);
        }

        [Fact]
        public void AddItems()
        {
            var array = new GrowableArray<int>();
            array.Add(1);
            array.Add(2);
            array.Add(3);
            
            Assert.Equal(3, array.Count);
            Assert.Equal(1, array[0]);
            Assert.Equal(2, array[1]);
            Assert.Equal(3, array[2]);
        }

        [Fact]
        public void AddManyItems()
        {
            var array = new GrowableArray<int>();
            for (int i = 0; i < 100; i++)
            {
                array.Add(i);
            }
            
            Assert.Equal(100, array.Count);
            for (int i = 0; i < 100; i++)
            {
                Assert.Equal(i, array[i]);
            }
        }

        [Fact]
        public void SetItem()
        {
            var array = new GrowableArray<string>();
            array.Add("First");
            array.Add("Second");
            
            array[0] = "Modified";
            Assert.Equal("Modified", array[0]);
            Assert.Equal("Second", array[1]);
        }

        [Fact]
        public void Clear()
        {
            var array = new GrowableArray<int>();
            array.Add(1);
            array.Add(2);
            array.Add(3);
            
            array.Clear();
            Assert.Equal(0, array.Count);
        }

        [Fact]
        public void SetCountLarger()
        {
            var array = new GrowableArray<int>();
            array.Add(1);
            array.Add(2);
            
            array.Count = 5;
            Assert.Equal(5, array.Count);
            Assert.Equal(1, array[0]);
            Assert.Equal(2, array[1]);
            Assert.Equal(0, array[2]);
            Assert.Equal(0, array[3]);
            Assert.Equal(0, array[4]);
        }

        [Fact]
        public void SetCountSmaller()
        {
            var array = new GrowableArray<int>();
            array.Add(1);
            array.Add(2);
            array.Add(3);
            array.Add(4);
            
            array.Count = 2;
            Assert.Equal(2, array.Count);
            Assert.Equal(1, array[0]);
            Assert.Equal(2, array[1]);
        }

        [Fact]
        public void AddRangeFromArray()
        {
            var array = new GrowableArray<int>();
            array.Add(1);
            
            int[] toAdd = new int[] { 2, 3, 4, 5 };
            array.AddRange(toAdd);
            
            Assert.Equal(5, array.Count);
            for (int i = 0; i < 5; i++)
            {
                Assert.Equal(i + 1, array[i]);
            }
        }

        [Fact]
        public void AddRangeFromGrowableArray()
        {
            var array1 = new GrowableArray<string>();
            array1.Add("A");
            array1.Add("B");
            
            var array2 = new GrowableArray<string>();
            array2.Add("C");
            array2.Add("D");
            
            // Convert to array to add
            string[] toAdd = new string[array2.Count];
            for (int i = 0; i < array2.Count; i++)
            {
                toAdd[i] = array2[i];
            }
            array1.AddRange(toAdd);
            
            Assert.Equal(4, array1.Count);
            Assert.Equal("A", array1[0]);
            Assert.Equal("B", array1[1]);
            Assert.Equal("C", array1[2]);
            Assert.Equal("D", array1[3]);
        }

        [Fact]
        public void WorksWithReferenceTypes()
        {
            var array = new GrowableArray<string>();
            array.Add("Hello");
            array.Add(null);
            array.Add("World");
            
            Assert.Equal(3, array.Count);
            Assert.Equal("Hello", array[0]);
            Assert.Null(array[1]);
            Assert.Equal("World", array[2]);
        }

        [Fact]
        public void WorksWithValueTypes()
        {
            var array = new GrowableArray<double>();
            array.Add(1.5);
            array.Add(2.5);
            array.Add(3.5);
            
            Assert.Equal(3, array.Count);
            Assert.Equal(1.5, array[0]);
            Assert.Equal(2.5, array[1]);
            Assert.Equal(3.5, array[2]);
        }

        /// <summary>
        /// Checks every valid range across empty, small and multi-cache-line value arrays.
        /// </summary>
        [Fact]
        public void ValueRangesPreserveLogicalAndBackingContents()
        {
            for (int length = 0; length <= 64; length++)
            {
                for (int index = 0; index <= length; index++)
                {
                    for (int count = 0; count <= length - index; count++)
                    {
                        var actual = new GrowableArray<int>(length + 4);
                        for (int i = 0; i < length; i++)
                        {
                            actual.Add(i + 1);
                        }
                        int[] expected = (int[])actual.UnderlyingArray.Clone();
                        int expectedCount = ScalarRemove(expected, length, index, count);
                        int[] backing = actual.UnderlyingArray;

                        actual.RemoveRange(index, count);

                        Assert.Equal(expectedCount, actual.Count);
                        Assert.Same(backing, actual.UnderlyingArray);
                        Assert.Equal(expected, actual.UnderlyingArray);
                    }
                }
            }
        }

        /// <summary>
        /// Verifies overlapping reference moves preserve identity and the unchanged trailing slots.
        /// </summary>
        [Theory]
        [InlineData(0, 1)]
        [InlineData(1, 3)]
        [InlineData(3, 2)]
        [InlineData(0, 5)]
        [InlineData(5, 0)]
        public void ReferenceRangesPreserveIdentityAndBackingContents(int index, int count)
        {
            var actual = new GrowableArray<object>(8);
            actual.Add(new object());
            actual.Add(null);
            actual.Add(new object());
            actual.Add(new object());
            actual.Add(new object());
            object[] expected = (object[])actual.UnderlyingArray.Clone();
            int expectedCount = ScalarRemove(expected, actual.Count, index, count);

            actual.RemoveRange(index, count);

            Assert.Equal(expectedCount, actual.Count);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Same(expected[i], actual.UnderlyingArray[i]);
            }
        }

        /// <summary>
        /// Checks a large overlapping move without changing allocation or capacity.
        /// </summary>
        [Fact]
        public void LargeRangeRetainsStorage()
        {
            var actual = new GrowableArray<int>(1_000_000);
            for (int i = 0; i < 1_000_000; i++)
            {
                actual.Add(i);
            }
            int[] backing = actual.UnderlyingArray;
#if NETCOREAPP
            long before = GC.GetAllocatedBytesForCurrentThread();
#endif

            actual.RemoveRange(123, 456);

#if NETCOREAPP
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(0, allocated);
#endif
            Assert.Same(backing, actual.UnderlyingArray);
            Assert.Equal(999_544, actual.Count);
            Assert.Equal(122, actual[122]);
            Assert.Equal(579, actual[123]);
            Assert.Equal(999_999, actual[actual.Count - 1]);
        }

        /// <summary>
        /// Preserves the existing zero-count early return and invalid-input exception types.
        /// </summary>
        [Fact]
        public void InvalidInputsPreserveExceptions()
        {
            var actual = new GrowableArray<int>();
            actual.RemoveRange(int.MinValue, 0);
            actual.Add(1);
            Assert.Throws<ArgumentException>(() => actual.RemoveRange(0, -1));
            Assert.Throws<IndexOutOfRangeException>(() => actual.RemoveRange(-1, 1));
            Assert.Throws<IndexOutOfRangeException>(() => actual.RemoveRange(1, 1));
            Assert.Equal(1, actual.Count);
            Assert.Equal(1, actual[0]);
            actual.Add(2);
            Assert.Throws<IndexOutOfRangeException>(() => actual.RemoveRange(1, int.MaxValue));
            Assert.Equal(2, actual.Count);
            Assert.Equal(2, actual[1]);
        }

#if !DEBUG
        /// <summary>
        /// Verifies existing truncation behavior in builds without the range assertion.
        /// </summary>
        [Theory]
        [InlineData(2, 4)]
        [InlineData(4, 2)]
        [InlineData(0, int.MaxValue)]
        public void RangesPastEndTruncateAtIndex(int index, int count)
        {
            int[] expected = [1, 2, 3, 4, 5];
            var actual = new GrowableArray<int>(5);
            actual.AddRange(expected);
            int[] backing = actual.UnderlyingArray;

            actual.RemoveRange(index, count);

            Assert.Equal(index, actual.Count);
            Assert.Same(backing, actual.UnderlyingArray);
            Assert.Equal(expected, actual.UnderlyingArray);
        }
#endif

        #region private
        private static int ScalarRemove<T>(T[] values, int length, int index, int count)
        {
            if (count == 0)
            {
                return length;
            }
            for (int endIndex = index + count; endIndex < length; endIndex++)
            {
                values[index++] = values[endIndex];
            }
            return index;
        }
        #endregion
    }
}
