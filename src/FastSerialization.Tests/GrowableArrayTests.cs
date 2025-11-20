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
    }
}
