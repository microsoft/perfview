using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace FastSerializationTests
{
    /// <summary>
    /// Tests for SegmentedDictionary&lt;TKey, TValue&gt;
    /// </summary>
    public class SegmentedDictionaryTests
    {
        [Fact]
        public void DefaultConstructor()
        {
            var dict = new SegmentedDictionary<string, int>();
            Assert.Empty(dict);
        }

        [Fact]
        public void AddAndRetrieve()
        {
            var dict = new SegmentedDictionary<string, int>();
            dict.Add("one", 1);
            dict.Add("two", 2);
            dict.Add("three", 3);
            
            Assert.Equal(3, dict.Count);
            Assert.Equal(1, dict["one"]);
            Assert.Equal(2, dict["two"]);
            Assert.Equal(3, dict["three"]);
        }

        [Fact]
        public void TryGetValue()
        {
            var dict = new SegmentedDictionary<string, int>();
            dict.Add("key1", 100);
            
            Assert.True(dict.TryGetValue("key1", out int value));
            Assert.Equal(100, value);
            
            Assert.False(dict.TryGetValue("key2", out value));
            Assert.Equal(0, value);
        }

        [Fact]
        public void ContainsKey()
        {
            var dict = new SegmentedDictionary<string, string>();
            dict.Add("exists", "value");
            
            Assert.True(dict.ContainsKey("exists"));
            Assert.False(dict.ContainsKey("missing"));
        }

        [Fact]
        public void Remove()
        {
            var dict = new SegmentedDictionary<int, string>();
            dict.Add(1, "one");
            dict.Add(2, "two");
            dict.Add(3, "three");
            
            Assert.Equal(3, dict.Count);
            
            bool removed = dict.Remove(2);
            Assert.True(removed);
            Assert.Equal(2, dict.Count);
            Assert.False(dict.ContainsKey(2));
            
            removed = dict.Remove(99);
            Assert.False(removed);
            Assert.Equal(2, dict.Count);
        }

        [Fact]
        public void Clear()
        {
            var dict = new SegmentedDictionary<string, int>();
            dict.Add("a", 1);
            dict.Add("b", 2);
            dict.Add("c", 3);
            
            Assert.Equal(3, dict.Count);
            
            dict.Clear();
            Assert.Empty(dict);
            Assert.False(dict.ContainsKey("a"));
            Assert.False(dict.ContainsKey("b"));
            Assert.False(dict.ContainsKey("c"));
        }

        [Fact]
        public void UpdateValue()
        {
            var dict = new SegmentedDictionary<string, int>();
            dict.Add("key", 10);
            Assert.Equal(10, dict["key"]);
            
            dict["key"] = 20;
            Assert.Equal(20, dict["key"]);
        }

        [Fact]
        public void AddDuplicateKeyThrows()
        {
            var dict = new SegmentedDictionary<string, int>();
            dict.Add("duplicate", 1);
            
            Assert.Throws<ArgumentException>(() => dict.Add("duplicate", 2));
        }

        [Fact]
        public void AccessNonExistentKeyThrows()
        {
            var dict = new SegmentedDictionary<string, int>();
            
            Assert.Throws<KeyNotFoundException>(() => { var x = dict["missing"]; });
        }

        [Fact]
        public void Keys()
        {
            var dict = new SegmentedDictionary<string, int>();
            dict.Add("one", 1);
            dict.Add("two", 2);
            dict.Add("three", 3);
            
            var keys = dict.Keys.ToList();
            Assert.Equal(3, keys.Count);
            Assert.Contains("one", keys);
            Assert.Contains("two", keys);
            Assert.Contains("three", keys);
        }

        [Fact]
        public void Values()
        {
            var dict = new SegmentedDictionary<string, int>();
            dict.Add("one", 1);
            dict.Add("two", 2);
            dict.Add("three", 3);
            
            var values = dict.Values.ToList();
            Assert.Equal(3, values.Count);
            Assert.Contains(1, values);
            Assert.Contains(2, values);
            Assert.Contains(3, values);
        }

        [Fact]
        public void GetEnumerator()
        {
            var dict = new SegmentedDictionary<string, int>();
            dict.Add("one", 1);
            dict.Add("two", 2);
            dict.Add("three", 3);
            
            var foundKeys = new HashSet<string>();
            foreach (var kvp in dict)
            {
                Assert.NotNull(kvp.Key);
                foundKeys.Add(kvp.Key);
                
                // Verify the correct value for each key
                if (kvp.Key == "one")
                    Assert.Equal(1, kvp.Value);
                else if (kvp.Key == "two")
                    Assert.Equal(2, kvp.Value);
                else if (kvp.Key == "three")
                    Assert.Equal(3, kvp.Value);
                else
                    Assert.Fail($"Unexpected key: {kvp.Key}");
            }
            
            // Verify we found each key exactly once
            Assert.Equal(3, foundKeys.Count);
            Assert.Contains("one", foundKeys);
            Assert.Contains("two", foundKeys);
            Assert.Contains("three", foundKeys);
        }

        [Fact]
        public void LargeDictionary()
        {
            var dict = new SegmentedDictionary<int, string>();
            
            // Add many items to test segmentation
            for (int i = 0; i < 10000; i++)
            {
                dict.Add(i, $"Value {i}");
            }
            
            Assert.Equal(10000, dict.Count);
            
            // Verify all items
            for (int i = 0; i < 10000; i++)
            {
                Assert.True(dict.ContainsKey(i));
                Assert.Equal($"Value {i}", dict[i]);
            }
        }

        [Fact]
        public void WorksWithNullValues()
        {
            var dict = new SegmentedDictionary<string, string>();
            dict.Add("key1", null);
            dict.Add("key2", "value");
            
            Assert.Equal(2, dict.Count);
            Assert.Null(dict["key1"]);
            Assert.Equal("value", dict["key2"]);
        }

        [Fact]
        public void WorksWithValueTypes()
        {
            var dict = new SegmentedDictionary<int, double>();
            dict.Add(1, 1.5);
            dict.Add(2, 2.5);
            
            Assert.Equal(2, dict.Count);
            Assert.Equal(1.5, dict[1]);
            Assert.Equal(2.5, dict[2]);
        }

        [Fact]
        public void CopyTo()
        {
            var dict = new SegmentedDictionary<string, int>();
            dict.Add("one", 1);
            dict.Add("two", 2);
            
            var array = new KeyValuePair<string, int>[4];
            dict.CopyTo(array, 1);
            
            // First and last elements should be default (null, 0)
            Assert.Equal(new KeyValuePair<string, int>(null, 0), array[0]);
            Assert.Equal(new KeyValuePair<string, int>(null, 0), array[3]);
            
            // Middle two elements should contain our dictionary entries
            // Order is not guaranteed, so check both are present
            var copiedItems = new[] { array[1], array[2] };
            Assert.Contains(new KeyValuePair<string, int>("one", 1), copiedItems);
            Assert.Contains(new KeyValuePair<string, int>("two", 2), copiedItems);
        }
    }
}
