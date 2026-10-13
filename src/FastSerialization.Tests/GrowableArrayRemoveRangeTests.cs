#nullable enable

using System;
using System.Collections.Generic;
using Xunit;

namespace FastSerializationTests
{
    /// <summary>
    /// Verifies bulk removal preserves the scalar implementation's overlap and backing-storage behavior.
    /// </summary>
    public class GrowableArrayRemoveRangeTests
    {
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
            var actual = new GrowableArray<object?>(8);
            actual.Add(new object());
            actual.Add(null);
            actual.Add(new object());
            actual.Add(new object());
            actual.Add(new object());
            object?[] expected = (object?[])actual.UnderlyingArray.Clone();
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
    }
}
