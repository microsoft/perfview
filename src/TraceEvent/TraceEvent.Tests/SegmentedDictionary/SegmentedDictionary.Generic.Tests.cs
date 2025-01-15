// Tests copied from dotnet/runtime repo. Original source code can be found here:
// https://github.com/dotnet/runtime/blob/main/src/libraries/System.Collections/tests/Generic/Dictionary/Dictionary.Generic.cs

using System;
using System.Collections.Generic;
using Xunit;

namespace PerfView.Collections.Tests
{
    /// <summary>
    /// Contains tests that ensure the correctness of the Dictionary class.
    /// </summary>
    public abstract class SegmentedDictionary_Generic_Tests<TKey, TValue> : IDictionary_Generic_Tests<TKey, TValue>
    {
        protected override ModifyOperation ModifyEnumeratorThrows => ModifyOperation.Add | ModifyOperation.Insert;

        protected override ModifyOperation ModifyEnumeratorAllowed => ModifyOperation.Overwrite | ModifyOperation.Remove | ModifyOperation.Clear;

        #region IDictionary<TKey, TValue Helper Methods

        protected override IDictionary<TKey, TValue> GenericIDictionaryFactory() => new SegmentedDictionary<TKey, TValue>();

        protected override IDictionary<TKey, TValue> GenericIDictionaryFactory(IEqualityComparer<TKey> comparer) => new SegmentedDictionary<TKey, TValue>(comparer);

        protected override Type ICollection_Generic_CopyTo_IndexLargerThanArrayCount_ThrowType => typeof(ArgumentOutOfRangeException);

        #endregion

        #region Constructors

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void Dictionary_Generic_Constructor_IDictionary(int count)
        {
            IDictionary<TKey, TValue> source = GenericIDictionaryFactory(count);
            IDictionary<TKey, TValue> copied = new SegmentedDictionary<TKey, TValue>(source);
            Assert.Equal(source, copied);
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void Dictionary_Generic_Constructor_IDictionary_IEqualityComparer(int count)
        {
            IEqualityComparer<TKey> comparer = GetKeyIEqualityComparer();
            IDictionary<TKey, TValue> source = GenericIDictionaryFactory(count);
            SegmentedDictionary<TKey, TValue> copied = new SegmentedDictionary<TKey, TValue>(source, comparer);
            Assert.Equal(source, copied);
            Assert.Equal(comparer, copied.Comparer);
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void Dictionary_Generic_Constructor_IEqualityComparer(int count)
        {
            IEqualityComparer<TKey> comparer = GetKeyIEqualityComparer();
            IDictionary<TKey, TValue> source = GenericIDictionaryFactory(count);
            SegmentedDictionary<TKey, TValue> copied = new SegmentedDictionary<TKey, TValue>(source, comparer);
            Assert.Equal(source, copied);
            Assert.Equal(comparer, copied.Comparer);
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void Dictionary_Generic_Constructor_int(int count)
        {
            IDictionary<TKey, TValue> dictionary = new SegmentedDictionary<TKey, TValue>(count);
            Assert.Empty(dictionary);
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void Dictionary_Generic_Constructor_int_IEqualityComparer(int count)
        {
            IEqualityComparer<TKey> comparer = GetKeyIEqualityComparer();
            SegmentedDictionary<TKey, TValue> dictionary = new SegmentedDictionary<TKey, TValue>(count, comparer);
            Assert.Empty(dictionary);
            Assert.Equal(comparer, dictionary.Comparer);
        }

        #endregion

        #region ContainsValue

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void Dictionary_Generic_ContainsValue_NotPresent(int count)
        {
            SegmentedDictionary<TKey, TValue> dictionary = (SegmentedDictionary<TKey, TValue>)GenericIDictionaryFactory(count);
            int seed = 4315;
            TValue notPresent = CreateTValue(seed++);
            while (dictionary.Values.Contains(notPresent))
                notPresent = CreateTValue(seed++);
            Assert.False(dictionary.ContainsValue(notPresent));
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void Dictionary_Generic_ContainsValue_Present(int count)
        {
            SegmentedDictionary<TKey, TValue> dictionary = (SegmentedDictionary<TKey, TValue>)GenericIDictionaryFactory(count);
            int seed = 4315;
            KeyValuePair<TKey, TValue> notPresent = CreateT(seed++);
            while (dictionary.Contains(notPresent))
                notPresent = CreateT(seed++);
            dictionary.Add(notPresent.Key, notPresent.Value);
            Assert.True(dictionary.ContainsValue(notPresent.Value));
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void Dictionary_Generic_ContainsValue_DefaultValueNotPresent(int count)
        {
            SegmentedDictionary<TKey, TValue> dictionary = (SegmentedDictionary<TKey, TValue>)GenericIDictionaryFactory(count);
            Assert.False(dictionary.ContainsValue(default(TValue)));
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void Dictionary_Generic_ContainsValue_DefaultValuePresent(int count)
        {
            SegmentedDictionary<TKey, TValue> dictionary = (SegmentedDictionary<TKey, TValue>)GenericIDictionaryFactory(count);
            int seed = 4315;
            TKey notPresent = CreateTKey(seed++);
            while (dictionary.ContainsKey(notPresent))
                notPresent = CreateTKey(seed++);
            dictionary.Add(notPresent, default(TValue));
            Assert.True(dictionary.ContainsValue(default(TValue)));
        }

        #endregion

        #region Remove(TKey)

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void Dictionary_Generic_RemoveKey_ValidKeyNotContainedInDictionary(int count)
        {
            SegmentedDictionary<TKey, TValue> dictionary = (SegmentedDictionary<TKey, TValue>)GenericIDictionaryFactory(count);
            TValue value;
            TKey missingKey = GetNewKey(dictionary);

            Assert.False(dictionary.Remove(missingKey, out value));
            Assert.Equal(count, dictionary.Count);
            Assert.Equal(default(TValue), value);
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void Dictionary_Generic_RemoveKey_ValidKeyContainedInDictionary(int count)
        {
            SegmentedDictionary<TKey, TValue> dictionary = (SegmentedDictionary<TKey, TValue>)GenericIDictionaryFactory(count);
            TKey missingKey = GetNewKey(dictionary);
            TValue outValue;
            TValue inValue = CreateTValue(count);

            dictionary.Add(missingKey, inValue);
            Assert.True(dictionary.Remove(missingKey, out outValue));
            Assert.Equal(count, dictionary.Count);
            Assert.Equal(inValue, outValue);
            Assert.False(dictionary.TryGetValue(missingKey, out outValue));
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void Dictionary_Generic_RemoveKey_DefaultKeyNotContainedInDictionary(int count)
        {
            SegmentedDictionary<TKey, TValue> dictionary = (SegmentedDictionary<TKey, TValue>)GenericIDictionaryFactory(count);
            TValue outValue;

            if (DefaultValueAllowed)
            {
                TKey missingKey = default(TKey);
                while (dictionary.ContainsKey(missingKey))
                    dictionary.Remove(missingKey);
                Assert.False(dictionary.Remove(missingKey, out outValue));
                Assert.Equal(default(TValue), outValue);
            }
            else
            {
                TValue initValue = CreateTValue(count);
                outValue = initValue;
                Assert.Throws<ArgumentNullException>(() => dictionary.Remove(default(TKey), out outValue));
                Assert.Equal(initValue, outValue);
            }
        }

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void Dictionary_Generic_RemoveKey_DefaultKeyContainedInDictionary(int count)
        {
            if (DefaultValueAllowed)
            {
                SegmentedDictionary<TKey, TValue> dictionary = (SegmentedDictionary<TKey, TValue>)(GenericIDictionaryFactory(count));
                TKey missingKey = default(TKey);
                TValue value;

                dictionary.TryAdd(missingKey, default(TValue));
                Assert.True(dictionary.Remove(missingKey, out value));
            }
        }

        [Fact]
        public void Dictionary_Generic_Remove_RemoveFirstEnumerationContinues()
        {
            SegmentedDictionary<TKey, TValue> dict = (SegmentedDictionary<TKey, TValue>)GenericIDictionaryFactory(3);
            using (var enumerator = dict.GetEnumerator())
            {
                enumerator.MoveNext();
                TKey key = enumerator.Current.Key;
                enumerator.MoveNext();
                dict.Remove(key);
                Assert.True(enumerator.MoveNext());
                Assert.False(enumerator.MoveNext());
            }
        }

        [Fact]
        public void Dictionary_Generic_Remove_RemoveCurrentEnumerationContinues()
        {
            SegmentedDictionary<TKey, TValue> dict = (SegmentedDictionary<TKey, TValue>)GenericIDictionaryFactory(3);
            using (var enumerator = dict.GetEnumerator())
            {
                enumerator.MoveNext();
                enumerator.MoveNext();
                dict.Remove(enumerator.Current.Key);
                Assert.True(enumerator.MoveNext());
                Assert.False(enumerator.MoveNext());
            }
        }

        [Fact]
        public void Dictionary_Generic_Remove_RemoveLastEnumerationFinishes()
        {
            SegmentedDictionary<TKey, TValue> dict = (SegmentedDictionary<TKey, TValue>)GenericIDictionaryFactory(3);
            TKey key = default;
            using (var enumerator = dict.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    key = enumerator.Current.Key;
                }
            }
            using (var enumerator = dict.GetEnumerator())
            {
                enumerator.MoveNext();
                enumerator.MoveNext();
                dict.Remove(key);
                Assert.False(enumerator.MoveNext());
            }
        }

        #endregion

        #region EnsureCapacity

        [Theory]
        [MemberData(nameof(ValidCollectionSizes))]
        public void EnsureCapacity_Generic_RequestingLargerCapacity_DoesInvalidateEnumeration(int count)
        {
            var dictionary = (SegmentedDictionary<TKey, TValue>)(GenericIDictionaryFactory(count));
            var capacity = dictionary.EnsureCapacity(0);
            var enumerator = dictionary.GetEnumerator();

            dictionary.EnsureCapacity(capacity + 1); // Verify EnsureCapacity does invalidate enumeration

            Assert.Throws<InvalidOperationException>(() => enumerator.MoveNext());
        }

        [Fact]
        public void EnsureCapacity_Generic_NegativeCapacityRequested_Throws()
        {
            var dictionary = new SegmentedDictionary<TKey, TValue>();
            AssertExtensions.Throws<ArgumentOutOfRangeException>("capacity", () => dictionary.EnsureCapacity(-1));
        }

        [Fact]
        public void EnsureCapacity_Generic_DictionaryNotInitialized_RequestedZero_ReturnsZero()
        {
            var dictionary = new SegmentedDictionary<TKey, TValue>();
            Assert.Equal(0, dictionary.EnsureCapacity(0));
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void EnsureCapacity_Generic_DictionaryNotInitialized_RequestedNonZero_CapacityIsSetToAtLeastTheRequested(int requestedCapacity)
        {
            var dictionary = new SegmentedDictionary<TKey, TValue>();
            Assert.InRange(dictionary.EnsureCapacity(requestedCapacity), requestedCapacity, int.MaxValue);
        }

        [Theory]
        [InlineData(3)]
        [InlineData(7)]
        public void EnsureCapacity_Generic_RequestedCapacitySmallerThanCurrent_CapacityUnchanged(int currentCapacity)
        {
            SegmentedDictionary<TKey, TValue> dictionary;

            // assert capacity remains the same when ensuring a capacity smaller or equal than existing
            for (int i = 0; i <= currentCapacity; i++)
            {
                dictionary = new SegmentedDictionary<TKey, TValue>(currentCapacity);
                Assert.True(dictionary.EnsureCapacity(i) > currentCapacity);
            }
        }

        [Theory]
        [InlineData(7)]
        public void EnsureCapacity_Generic_ExistingCapacityRequested_SameValueReturned(int capacity)
        {
            var dictionary = new SegmentedDictionary<TKey, TValue>(capacity);
            Assert.True(dictionary.EnsureCapacity(capacity) > capacity);

            dictionary = (SegmentedDictionary<TKey, TValue>)GenericIDictionaryFactory(capacity);
            Assert.True(dictionary.EnsureCapacity(capacity) > capacity);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void EnsureCapacity_Generic_EnsureCapacityCalledTwice_ReturnsSameValue(int count)
        {
            var dictionary = (SegmentedDictionary<TKey, TValue>)GenericIDictionaryFactory(count);
            int capacity = dictionary.EnsureCapacity(0);
            Assert.Equal(capacity, dictionary.EnsureCapacity(0));

            dictionary = (SegmentedDictionary<TKey, TValue>)GenericIDictionaryFactory(count);
            capacity = dictionary.EnsureCapacity(count);
            Assert.True(dictionary.EnsureCapacity(count) >= capacity);

            dictionary = (SegmentedDictionary<TKey, TValue>)GenericIDictionaryFactory(count);
            capacity = dictionary.EnsureCapacity(count + 1);
            Assert.True(dictionary.EnsureCapacity(count + 1) >= capacity);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(5)]
        [InlineData(7)]
        public void EnsureCapacity_Generic_DictionaryNotEmpty_RequestedSmallerThanCount_ReturnsAtLeastSizeOfCount(int count)
        {
            var dictionary = (SegmentedDictionary<TKey, TValue>)GenericIDictionaryFactory(count);
            Assert.InRange(dictionary.EnsureCapacity(count - 1), count, int.MaxValue);
        }

        [Theory]
        [InlineData(7)]
        [InlineData(20)]
        public void EnsureCapacity_Generic_DictionaryNotEmpty_SetsToAtLeastTheRequested(int count)
        {
            var dictionary = (SegmentedDictionary<TKey, TValue>)GenericIDictionaryFactory(count);

            // get current capacity
            int currentCapacity = dictionary.EnsureCapacity(0);

            // assert we can update to a larger capacity
            int newCapacity = dictionary.EnsureCapacity(currentCapacity * 2);
            Assert.InRange(newCapacity, currentCapacity * 2, int.MaxValue);
        }

        #endregion
    }
}