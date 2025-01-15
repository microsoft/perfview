using System.Diagnostics;
using System.Runtime.CompilerServices;
using Microsoft.Diagnostics.FastSerialization;

namespace System.Collections.Generic
{
    /// <summary>
    /// Represents a collection of keys and values.
    /// </summary>
    /// <remarks>
    /// <para>This collection has the similar performance characteristics as <see cref="Dictionary{TKey, TValue}"/>, but
    /// uses segmented lists to avoid allocations in the Large Object Heap.</para>
    /// 
    /// <para>
    /// This implementation was based on the SegmentedDictionary implementation made for dotnet/roslyn. Original source code:
    /// https://github.com/dotnet/roslyn/blob/release/dev17.0/src/Dependencies/Collections/SegmentedDictionary%602.cs
    /// </para>
    /// </remarks>
    /// <typeparam name="TKey">The type of the keys in the dictionary.</typeparam>
    /// <typeparam name="TValue">The type of the values in the dictionary.</typeparam>
    public sealed class SegmentedDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IDictionary
    {
        #region Private Fields
        private static Entry EntryPlaceholder = new Entry();

        private SegmentedList<int> _buckets = new SegmentedList<int>(defaultSegmentSize);
        private SegmentedList<Entry> _entries = new SegmentedList<Entry>(defaultSegmentSize);

        private const int defaultSegmentSize = 8_192;

        private int _count;
        private int _freeList;
        private int _freeCount;
        private ulong _fastModMultiplier;
        private int _version;

        private readonly IEqualityComparer<TKey> _comparer;

        private KeyCollection _keys = null;
        private ValueCollection _values = null;
        private const int StartOfFreeList = -3;

        private enum InsertionBehavior
        {
            None, OverwriteExisting, ThrowOnExisting
        }

        private struct Entry
        {
            public uint _hashCode;
            /// <summary>
            /// 0-based index of next entry in chain: -1 means end of chain
            /// also encodes whether this entry _itself_ is part of the free list by changing sign and subtracting 3,
            /// so -2 means end of free list, -3 means index 0 but on free list, -4 means index 1 but on free list, etc.
            /// </summary>
            public int _next;
            public TKey _key;     // Key of entry
            public TValue _value; // Value of entry
        }

        #endregion

        #region Helper Methods

        private int Initialize(int capacity)
        {
            var size = HashHelpers.GetPrime(capacity);
            var buckets = new SegmentedList<int>(defaultSegmentSize, size);
            var entries = new SegmentedList<Entry>(defaultSegmentSize, size);

            // Assign member variables after both arrays allocated to guard against corruption from OOM if second fails
            _freeList = -1;
            _fastModMultiplier = HashHelpers.GetFastModMultiplier((uint)buckets.Capacity);
            _buckets = buckets;
            _entries = entries;

            return size;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private ref int GetBucket(uint hashCode)
        {
            var buckets = _buckets;
            return ref buckets.GetElementByReference((int)HashHelpers.FastMod(hashCode, (uint)buckets.Capacity, _fastModMultiplier));
        }

        private bool FindEntry(TKey key, out Entry entry)
        {
            entry = EntryPlaceholder;

            if (key == null)
            {
                throw new ArgumentNullException("Key cannot be null.");
            }

            if (_buckets.Capacity > 0)
            {
                Debug.Assert(_entries.Capacity > 0, "expected entries to be non-empty");
                var comparer = _comparer;
                
                var hashCode = (uint)comparer.GetHashCode(key);
                var i = GetBucket(hashCode) - 1; // Value in _buckets is 1-based; subtract 1 from i. We do it here so it fuses with the following conditional.
                var entries = _entries;
                uint collisionCount = 0;
                
                do
                {
                    // Should be a while loop https://github.com/dotnet/runtime/issues/9422
                    // Test in if to drop range check for following array access
                    if ((uint)i >= (uint)entries.Capacity)
                    {
                        return false;
                    }

                    ref var currentEntry = ref entries.GetElementByReference(i);
                    if (currentEntry._hashCode == hashCode && comparer.Equals(currentEntry._key, key))
                    {
                        entry = currentEntry;
                        return true;
                    }

                    i = currentEntry._next;

                    collisionCount++;
                } while (collisionCount <= (uint)entries.Capacity);

                // The chain of entries forms a loop; which means a concurrent update has happened.
                // Break out of the loop and throw, rather than looping forever.
                throw new InvalidOperationException("Dictionary does not support concurrent operations.");
            }

            return false;
        }

        private bool TryInsert(TKey key, TValue value, InsertionBehavior behavior)
        {
            if (key == null)
            {
                throw new ArgumentNullException("Key cannot be null.");
            }

            if (_buckets.Capacity == 0)
            {
                Initialize(0);
            }
            Debug.Assert(_buckets.Capacity > 0);

            var entries = _entries;
            Debug.Assert(entries.Capacity > 0, "expected entries to be non-empty");

            var comparer = _comparer;
            var hashCode = (uint)comparer.GetHashCode(key);

            uint collisionCount = 0;
            ref var bucket = ref GetBucket(hashCode);
            var i = bucket - 1; // Value in _buckets is 1-based

            while (true)
            {
                // Should be a while loop https://github.com/dotnet/runtime/issues/9422
                // Test uint in if rather than loop condition to drop range check for following array access
                if ((uint)i >= (uint)entries.Capacity)
                {
                    break;
                }

                if (entries[i]._hashCode == hashCode && comparer.Equals(entries[i]._key, key))
                {
                    if (behavior == InsertionBehavior.OverwriteExisting)
                    {
                        entries.GetElementByReference(i)._value = value;
                        return true;
                    }

                    if (behavior == InsertionBehavior.ThrowOnExisting)
                    {
                        throw new ArgumentException($"The key with value {key} is already present in the dictionary.");
                    }

                    return false;
                }

                i = entries[i]._next;

                collisionCount++;
                if (collisionCount > (uint)entries.Capacity)
                {
                    // The chain of entries forms a loop; which means a concurrent update has happened.
                    // Break out of the loop and throw, rather than looping forever.
                    throw new InvalidOperationException("Dictionary does not support concurrent operations.");
                }
            }
            

            int index;
            if (_freeCount > 0)
            {
                index = _freeList;
                Debug.Assert((StartOfFreeList - entries[_freeList]._next) >= -1, "shouldn't overflow because `next` cannot underflow");
                _freeList = StartOfFreeList - entries[_freeList]._next;
                _freeCount--;
            }
            else
            {
                var count = _count;
                if (count == entries.Capacity)
                {
                    Resize();
                    bucket = ref GetBucket(hashCode);
                }
                index = count;
                _count = count + 1;
                entries = _entries;
            }

            ref var entry = ref entries.GetElementByReference(index);
            entry._hashCode = hashCode;
            entry._next = bucket - 1; // Value in _buckets is 1-based
            entry._key = key;
            entry._value = value; // Value in _buckets is 1-based
            bucket = index + 1;
            _version++;
            return true;
        }

        private void Resize()
            => Resize(HashHelpers.ExpandPrime(_count));

        private void Resize(int newSize)
        {
            Debug.Assert(_entries.Capacity > 0, "_entries should be non-empty");
            Debug.Assert(newSize >= _entries.Capacity);

            var entries = new SegmentedList<Entry>(defaultSegmentSize, newSize);

            var count = _count;

            entries.AppendFrom(_entries, 0, count);

            // Assign member variables after both arrays allocated to guard against corruption from OOM if second fails
            _buckets = new SegmentedList<int>(defaultSegmentSize, newSize);
            _fastModMultiplier = HashHelpers.GetFastModMultiplier((uint)_buckets.Capacity);
            for (var i = 0; i < count; i++)
            {
                if (entries[i]._next >= -1)
                {
                    ref var bucket = ref GetBucket(entries[i]._hashCode);
                    entries.GetElementByReference(i)._next = bucket - 1; // Value in _buckets is 1-based
                    bucket = i + 1;
                }
            }

            _entries = entries;
        }

        private static bool IsCompatibleKey(object key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
            return key is TKey;
        }

        #endregion

        #region Constructors

        public SegmentedDictionary()
            : this(0, null)
        {
        }

        public SegmentedDictionary(int capacity)
            : this(capacity, null)
        {
        }

        public SegmentedDictionary(IEqualityComparer<TKey> comparer)
            : this(0, comparer)
        {
        }

        public SegmentedDictionary(int capacity, IEqualityComparer<TKey> comparer)
        {
            if (capacity < 0)
            {
                throw new ArgumentException(nameof(capacity));
            }

            if (capacity > 0)
            {
                Initialize(capacity);
            }

            if (comparer != null && comparer != EqualityComparer<TKey>.Default) // first check for null to avoid forcing default comparer instantiation unnecessarily
            {
                _comparer = comparer;
            }
            else
            {
                _comparer = EqualityComparer<TKey>.Default;
            }
        }

        public SegmentedDictionary(IDictionary<TKey, TValue> dictionary)
            : this(dictionary, null)
        {
        }

        public SegmentedDictionary(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer)
            : this(dictionary != null ? dictionary.Count : 0, comparer)
        {
            if (dictionary == null)
            {
                throw new ArgumentNullException(nameof(dictionary));
            }

            // It is likely that the passed-in dictionary is SegmentedDictionary<TKey,TValue>. When this is the case,
            // avoid the enumerator allocation and overhead by looping through the entries array directly.
            // We only do this when dictionary is SegmentedDictionary<TKey,TValue> and not a subclass, to maintain
            // back-compat with subclasses that may have overridden the enumerator behavior.
            if (dictionary.GetType() == typeof(SegmentedDictionary<TKey, TValue>))
            {
                var d = (SegmentedDictionary<TKey, TValue>)dictionary;
                var count = d._count;
                var entries = d._entries;
                for (var i = 0; i < count; i++)
                {
                    if (entries[i]._next >= -1)
                    {
                        Add(entries[i]._key, entries[i]._value);
                    }
                }
                return;
            }

            foreach (var pair in dictionary)
            {
                Add(pair.Key, pair.Value);
            }
        }

        #endregion

        #region IDictionary<TKey, TValue> Implementation

        public TValue this[TKey key]
        {
            get
            {
                if (FindEntry(key, out Entry entry))
                {
                    return entry._value;
                }

                ThrowHelper.ThrowKeyNotFoundException(key);
                return default;
            }
            set
            {
                var modified = TryInsert(key, value, InsertionBehavior.OverwriteExisting);
                Debug.Assert(modified);
            }
        }

        ICollection<TKey> IDictionary<TKey, TValue>.Keys => Keys;

        ICollection<TValue> IDictionary<TKey, TValue>.Values => Values;

        public void Add(TKey key, TValue value)
        {
            var modified = TryInsert(key, value, InsertionBehavior.ThrowOnExisting);
            Debug.Assert(modified); // If there was an existing key and the Add failed, an exception will already have been thrown.
        }

        public bool ContainsKey(TKey key)
        {
            return FindEntry(key, out Entry entry);
        }

        public bool Remove(TKey key)
        {
            return Remove(key, out TValue _);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            bool entryFound = FindEntry(key, out Entry entry);
            if (entryFound)
            {
                value = entry._value;
                return true;
            }

            value = default;
            return false;
        }

        #endregion

        #region ICollection<KeyValuePair<TKey, TValue>> Implementation

        public int Count => _count - _freeCount;

        public bool IsReadOnly => false;

        public void Add(KeyValuePair<TKey, TValue> item) =>
            Add(item.Key, item.Value);

        public void Clear()
        {
            var count = _count;
            if (count > 0)
            {
                Debug.Assert(_buckets.Capacity > 0, "_buckets should be non-empty");
                Debug.Assert(_entries.Capacity > 0, "_entries should be non-empty");

                _buckets.Clear();

                _count = 0;
                _freeList = -1;
                _freeCount = 0;
                _entries.Clear();
            }
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            bool valueFound = FindEntry(item.Key, out Entry entry);
            if (valueFound && EqualityComparer<TValue>.Default.Equals(entry._value, item.Value))
            {
                return true;
            }

            return false;
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int index)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            if ((uint)index > (uint)array.Length)
            {
                ThrowHelper.ThrowIndexArgumentOutOfRange_NeedNonNegNumException();
            }

            if (array.Length - index < Count)
            {
                throw new ArgumentException(ThrowHelper.CommonStrings.Arg_ArrayPlusOffTooSmall);
            }

            var count = _count;
            var entries = _entries;
            for (var i = 0; i < count; i++)
            {
                if (entries[i]._next >= -1)
                {
                    array[index++] = new KeyValuePair<TKey, TValue>(entries[i]._key, entries[i]._value);
                }
            }
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            if (FindEntry(item.Key, out Entry entry) && EqualityComparer<TValue>.Default.Equals(item.Value, entry._value))
            {
                return Remove(item.Key, out TValue _);
            }

            return false;
        }

        #endregion

        #region IEnumerable<KeyValuePair<TKey, TValue>> Implementation

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() =>
            new Enumerator(this, Enumerator.KeyValuePair);

        #endregion

        #region IEnumerable Implementation

        IEnumerator IEnumerable.GetEnumerator() =>
            new Enumerator(this, Enumerator.KeyValuePair);

        #endregion

        #region IDictionary Implementation

        public object this[object key]
        {
            get
            {
                if (IsCompatibleKey(key))
                {
                    if (FindEntry((TKey)key, out Entry entry))
                    {
                        return entry._value;
                    }
                }

                return null;
            }
            set
            {
                if (key == null)
                {
                    throw new ArgumentNullException(nameof(key));
                }

                ThrowHelper.IfNullAndNullsAreIllegalThenThrow<TValue>(value, nameof(value));

                try
                {
                    var tempKey = (TKey)key;
                    try
                    {
                        this[tempKey] = (TValue)value;
                    }
                    catch (InvalidCastException)
                    {
                        ThrowHelper.ThrowWrongTypeArgumentException(value, typeof(TValue));
                    }
                }
                catch (InvalidCastException)
                {
                    ThrowHelper.ThrowWrongTypeArgumentException(key, typeof(TKey));
                }
            }
        }

        ICollection IDictionary.Keys => Keys;

        ICollection IDictionary.Values => Values;

        public bool IsFixedSize => false;

        public void Add(object key, object value)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            ThrowHelper.IfNullAndNullsAreIllegalThenThrow<TValue>(value, nameof(value));

            try
            {
                var tempKey = (TKey)key;

                try
                {
                    Add(tempKey, (TValue)value);
                }
                catch (InvalidCastException)
                {
                    ThrowHelper.ThrowWrongTypeArgumentException(value, typeof(TValue));
                }
            }
            catch (InvalidCastException)
            {
                ThrowHelper.ThrowWrongTypeArgumentException(key, typeof(TKey));
            }
        }

        public bool Contains(object key)
        {
            if (IsCompatibleKey(key))
            {
                return ContainsKey((TKey)key);
            }

            return false;
        }

        IDictionaryEnumerator IDictionary.GetEnumerator() =>
            new Enumerator(this, Enumerator.DictEntry);

        public void Remove(object key)
        {
            if (IsCompatibleKey(key))
            {
                Remove((TKey)key);
            }
        }

        #endregion

        #region ICollection Implementation

        public object SyncRoot => this;

        public bool IsSynchronized => false;

        public void CopyTo(Array array, int index)
        {
            if (array == null)
            {
                throw new ArgumentNullException(nameof(array));
            }

            if (array.Rank != 1)
            {
                throw new ArgumentException(ThrowHelper.CommonStrings.Arg_RankMultiDimNotSupported);
            }

            if (array.GetLowerBound(0) != 0)
            {
                throw new ArgumentException(ThrowHelper.CommonStrings.Arg_NonZeroLowerBound);
            }

            if ((uint)index > (uint)array.Length)
            {
                ThrowHelper.ThrowIndexArgumentOutOfRange_NeedNonNegNumException();
            }

            if (array.Length - index < Count)
            {
                throw new ArgumentException(ThrowHelper.CommonStrings.Arg_ArrayPlusOffTooSmall);
            }

            if (array is KeyValuePair<TKey, TValue>[] pairs)
            {
                CopyTo(pairs, index);
            }
            else if (array is DictionaryEntry[] dictEntryArray)
            {
                var entries = _entries;
                for (var i = 0; i < _count; i++)
                {
                    if (entries[i]._next >= -1)
                    {
                        dictEntryArray[index++] = new DictionaryEntry(entries[i]._key, entries[i]._value);
                    }
                }
            }
            else
            {
                var objects = array as object[];
                if (objects == null)
                {
                    throw new ArgumentException(ThrowHelper.CommonStrings.Argument_InvalidArrayType);
                }

                try
                {
                    var count = _count;
                    var entries = _entries;
                    for (var i = 0; i < count; i++)
                    {
                        if (entries[i]._next >= -1)
                        {
                            objects[index++] = new KeyValuePair<TKey, TValue>(entries[i]._key, entries[i]._value);
                        }
                    }
                }
                catch (ArrayTypeMismatchException)
                {
                    throw new ArgumentException(ThrowHelper.CommonStrings.Argument_InvalidArrayType);
                }
            }
        }

        #endregion

        #region Public Properties

        public IEqualityComparer<TKey> Comparer
        {
            get
            {
                return _comparer ?? EqualityComparer<TKey>.Default;
            }
        }

        public KeyCollection Keys
        {
            get
            {
                if (_keys == null)
                {
                    _keys = new KeyCollection(this);
                }

                return _keys;
            }
        }

        public ValueCollection Values
        {
            get
            {
                if (_values == null)
                {
                    _values = new ValueCollection(this);
                }

                return _values;
            }
        }

        #endregion

        #region Public Methods

        public bool TryAdd(TKey key, TValue value) =>
            TryInsert(key, value, InsertionBehavior.None);

        public bool Remove(TKey key, out TValue value)
        {
            // If perfomarnce becomes an issue, you can copy this implementation over to the other Remove method overloads.

            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            if (_buckets.Capacity > 0)
            {
                Debug.Assert(_entries.Capacity > 0, "entries should be non-empty");
                uint collisionCount = 0;
                var hashCode = (uint)(_comparer?.GetHashCode(key) ?? key.GetHashCode());
                ref var bucket = ref GetBucket(hashCode);
                var entries = _entries;
                var last = -1;
                var i = bucket - 1; // Value in buckets is 1-based
                while (i >= 0)
                {
                    ref var entry = ref entries.GetElementByReference(i);

                    if (entry._hashCode == hashCode && (_comparer?.Equals(entry._key, key) ?? EqualityComparer<TKey>.Default.Equals(entry._key, key)))
                    {
                        if (last < 0)
                        {
                            bucket = entry._next + 1; // Value in buckets is 1-based
                        }
                        else
                        {
                            entries.GetElementByReference(last)._next = entry._next;
                        }

                        value = entry._value;

                        Debug.Assert((StartOfFreeList - _freeList) < 0, "shouldn't underflow because max hashtable length is MaxPrimeArrayLength = 0x7FEFFFFD(2146435069) _freelist underflow threshold 2147483646");
                        entry._next = StartOfFreeList - _freeList;

                        entry._key = default;
                        entry._value = default;

                        _freeList = i;
                        _freeCount++;
                        return true;
                    }

                    last = i;
                    i = entry._next;

                    collisionCount++;
                    if (collisionCount > (uint)entries.Capacity)
                    {
                        // The chain of entries forms a loop; which means a concurrent update has happened.
                        // Break out of the loop and throw, rather than looping forever.
                        throw new InvalidOperationException(ThrowHelper.CommonStrings.InvalidOperation_ConcurrentOperationsNotSupported);
                    }
                }
            }

            value = default;
            return false;
        }

        public int EnsureCapacity(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            // Normal usage of a dictionary should never ask for a capacity that exceeds int32.MaxValue.
            var currentCapacity = (int)_entries.Capacity;
            if (currentCapacity >= capacity)
            {
                return currentCapacity;
            }

            _version++;

            if (_buckets.Capacity == 0)
            {
                return Initialize(capacity);
            }

            var newSize = HashHelpers.GetPrime(capacity);
            Resize(newSize);
            return newSize;
        }

        public bool ContainsValue(TValue value)
        {
            var entries = _entries;
            if (value == null)
            {
                for (var i = 0; i < _count; i++)
                {
                    if (entries[i]._next >= -1 && entries[i]._value == null)
                    {
                        return true;
                    }
                }
            }
            else
            {
                // Object type: Shared Generic, EqualityComparer<TValue>.Default won't devirtualize
                // https://github.com/dotnet/runtime/issues/10050
                // So cache in a local rather than get EqualityComparer per loop iteration
                var defaultComparer = EqualityComparer<TValue>.Default;
                for (var i = 0; i < _count; i++)
                {
                    if (entries[i]._next >= -1 && defaultComparer.Equals(entries[i]._value, value))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        #endregion

        public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>, IDictionaryEnumerator
        {
            private readonly SegmentedDictionary<TKey, TValue> _dictionary;
            private readonly int _version;
            private int _index;
            private KeyValuePair<TKey, TValue> _current;
            private readonly int _getEnumeratorRetType;  // What should Enumerator.Current return?

            internal const int DictEntry = 1;
            internal const int KeyValuePair = 2;

            internal Enumerator(SegmentedDictionary<TKey, TValue> dictionary, int getEnumeratorRetType)
            {
                _dictionary = dictionary;
                _version = dictionary._version;
                _index = 0;
                _getEnumeratorRetType = getEnumeratorRetType;
                _current = default;
            }

            public bool MoveNext()
            {
                if (_version != _dictionary._version)
                {
                    throw new InvalidOperationException(ThrowHelper.CommonStrings.InvalidOperation_EnumFailedVersion);
                }

                // Use unsigned comparison since we set index to dictionary.count+1 when the enumeration ends.
                // dictionary.count+1 could be negative if dictionary.count is int.MaxValue
                while ((uint)_index < (uint)_dictionary._count)
                {
                    ref var entry = ref _dictionary._entries.GetElementByReference(_index++);

                    if (entry._next >= -1)
                    {
                        _current = new KeyValuePair<TKey, TValue>(entry._key, entry._value);
                        return true;
                    }
                }

                _index = _dictionary._count + 1;
                _current = default;
                return false;
            }

            public KeyValuePair<TKey, TValue> Current => _current;

            public void Dispose()
            {
            }

            object IEnumerator.Current
            {
                get
                {
                    if (_index == 0 || (_index == _dictionary._count + 1))
                    {
                        throw new InvalidOperationException(ThrowHelper.CommonStrings.InvalidOperation_EnumOpCantHappen);
                    }

                    if (_getEnumeratorRetType == DictEntry)
                    {
                        return new DictionaryEntry(_current.Key, _current.Value);
                    }

                    return new KeyValuePair<TKey, TValue>(_current.Key, _current.Value);
                }
            }

            void IEnumerator.Reset()
            {
                if (_version != _dictionary._version)
                {
                    throw new InvalidOperationException(ThrowHelper.CommonStrings.InvalidOperation_EnumFailedVersion);
                }

                _index = 0;
                _current = default;
            }

            DictionaryEntry IDictionaryEnumerator.Entry
            {
                get
                {
                    if (_index == 0 || (_index == _dictionary._count + 1))
                    {
                        throw new InvalidOperationException(ThrowHelper.CommonStrings.InvalidOperation_EnumOpCantHappen);
                    }

                    return new DictionaryEntry(_current.Key, _current.Value);
                }
            }

            object IDictionaryEnumerator.Key
            {
                get
                {
                    if (_index == 0 || (_index == _dictionary._count + 1))
                    {
                        throw new InvalidOperationException(ThrowHelper.CommonStrings.InvalidOperation_EnumOpCantHappen);
                    }

                    return _current.Key;
                }
            }

            object IDictionaryEnumerator.Value
            {
                get
                {
                    if (_index == 0 || (_index == _dictionary._count + 1))
                    {
                        throw new InvalidOperationException(ThrowHelper.CommonStrings.InvalidOperation_EnumOpCantHappen);
                    }

                    return _current.Value;
                }
            }
        }

        public sealed class KeyCollection : ICollection<TKey>, ICollection, IReadOnlyCollection<TKey>
        {
            private readonly SegmentedDictionary<TKey, TValue> _dictionary;

            public KeyCollection(SegmentedDictionary<TKey, TValue> dictionary)
            {
                if (dictionary == null)
                {
                    throw new ArgumentNullException(nameof(dictionary));
                }

                _dictionary = dictionary;
            }

            public Enumerator GetEnumerator()
                => new Enumerator(_dictionary);

            public void CopyTo(TKey[] array, int index)
            {
                if (array == null)
                {
                    throw new ArgumentNullException(nameof(array));
                }

                if (index < 0 || index > array.Length)
                {
                    ThrowHelper.ThrowIndexArgumentOutOfRange_NeedNonNegNumException();
                }

                if (array.Length - index < _dictionary.Count)
                {
                    throw new ArgumentException(ThrowHelper.CommonStrings.Arg_ArrayPlusOffTooSmall);
                }

                var count = _dictionary._count;
                var entries = _dictionary._entries;
                for (var i = 0; i < count; i++)
                {
                    if (entries[i]._next >= -1)
                        array[index++] = entries[i]._key;
                }
            }

            public int Count => _dictionary.Count;

            bool ICollection<TKey>.IsReadOnly => true;

            void ICollection<TKey>.Add(TKey item)
                => throw new NotSupportedException();

            void ICollection<TKey>.Clear()
                => throw new NotSupportedException();

            public bool Contains(TKey item)
                => _dictionary.ContainsKey(item);

            bool ICollection<TKey>.Remove(TKey item)
            {
                throw new NotSupportedException();
            }

            IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator()
                => new Enumerator(_dictionary);

            IEnumerator IEnumerable.GetEnumerator()
                => new Enumerator(_dictionary);

            void ICollection.CopyTo(Array array, int index)
            {
                if (array == null)
                {
                    throw new ArgumentNullException(nameof(array));
                }

                if (array.Rank != 1)
                {
                    throw new ArgumentException(ThrowHelper.CommonStrings.Arg_RankMultiDimNotSupported);
                }

                if (array.GetLowerBound(0) != 0)
                {
                    throw new ArgumentException(ThrowHelper.CommonStrings.Arg_NonZeroLowerBound);
                }

                if ((uint)index > (uint)array.Length)
                {
                    ThrowHelper.ThrowIndexArgumentOutOfRange_NeedNonNegNumException();
                }

                if (array.Length - index < _dictionary.Count)
                {
                    throw new ArgumentException(ThrowHelper.CommonStrings.Arg_ArrayPlusOffTooSmall);
                }

                if (array is TKey[] keys)
                {
                    CopyTo(keys, index);
                }
                else
                {
                    var objects = array as object[];
                    if (objects == null)
                    {
                        throw new ArgumentException(ThrowHelper.CommonStrings.Argument_InvalidArrayType);
                    }

                    var count = _dictionary._count;
                    var entries = _dictionary._entries;
                    try
                    {
                        for (var i = 0; i < count; i++)
                        {
                            if (entries[i]._next >= -1)
                                objects[index++] = entries[i]._key;
                        }
                    }
                    catch (ArrayTypeMismatchException)
                    {
                        throw new ArgumentException(ThrowHelper.CommonStrings.Argument_InvalidArrayType);
                    }
                }
            }

            bool ICollection.IsSynchronized => false;

            object ICollection.SyncRoot => ((ICollection)_dictionary).SyncRoot;

            public struct Enumerator : IEnumerator<TKey>, IEnumerator
            {
                private readonly SegmentedDictionary<TKey, TValue> _dictionary;
                private int _index;
                private readonly int _version;
                private TKey _currentKey;

                internal Enumerator(SegmentedDictionary<TKey, TValue> dictionary)
                {
                    _dictionary = dictionary;
                    _version = dictionary._version;
                    _index = 0;
                    _currentKey = default;
                }

                public void Dispose()
                {
                }

                public bool MoveNext()
                {
                    if (_version != _dictionary._version)
                    {
                        throw new InvalidOperationException(ThrowHelper.CommonStrings.InvalidOperation_EnumFailedVersion);
                    }

                    while ((uint)_index < (uint)_dictionary._count)
                    {
                        ref var entry = ref _dictionary._entries.GetElementByReference(_index++);

                        if (entry._next >= -1)
                        {
                            _currentKey = entry._key;
                            return true;
                        }
                    }

                    _index = _dictionary._count + 1;
                    _currentKey = default;
                    return false;
                }

                public TKey Current => _currentKey;

                object IEnumerator.Current
                {
                    get
                    {
                        if (_index == 0 || (_index == _dictionary._count + 1))
                        {
                            throw new InvalidOperationException(ThrowHelper.CommonStrings.InvalidOperation_EnumOpCantHappen);
                        }

                        return _currentKey;
                    }
                }

                void IEnumerator.Reset()
                {
                    if (_version != _dictionary._version)
                    {
                        throw new InvalidOperationException(ThrowHelper.CommonStrings.InvalidOperation_EnumFailedVersion);
                    }

                    _index = 0;
                    _currentKey = default;
                }
            }
        }

        public sealed class ValueCollection : ICollection<TValue>, ICollection, IReadOnlyCollection<TValue>
        {
            private readonly SegmentedDictionary<TKey, TValue> _dictionary;

            public ValueCollection(SegmentedDictionary<TKey, TValue> dictionary)
            {
                if (dictionary == null)
                {
                    throw new ArgumentNullException(nameof(dictionary));
                }

                _dictionary = dictionary;
            }

            public Enumerator GetEnumerator()
                => new Enumerator(_dictionary);

            public void CopyTo(TValue[] array, int index)
            {
                if (array == null)
                {
                    throw new ArgumentNullException(nameof(array));
                }

                if ((uint)index > array.Length)
                {
                    ThrowHelper.ThrowIndexArgumentOutOfRange_NeedNonNegNumException();
                }

                if (array.Length - index < _dictionary.Count)
                {
                    throw new ArgumentException(ThrowHelper.CommonStrings.Arg_ArrayPlusOffTooSmall);
                }

                var count = _dictionary._count;
                var entries = _dictionary._entries;
                for (var i = 0; i < count; i++)
                {
                    if (entries[i]._next >= -1)
                        array[index++] = entries[i]._value;
                }
            }

            public int Count => _dictionary.Count;

            bool ICollection<TValue>.IsReadOnly => true;

            void ICollection<TValue>.Add(TValue item)
                => throw new NotSupportedException();

            bool ICollection<TValue>.Remove(TValue item)
                => throw new NotSupportedException();

            void ICollection<TValue>.Clear()
                => throw new NotSupportedException();

            public bool Contains(TValue item)
                => _dictionary.ContainsValue(item);

            IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator()
                => new Enumerator(_dictionary);

            IEnumerator IEnumerable.GetEnumerator()
                => new Enumerator(_dictionary);

            void ICollection.CopyTo(Array array, int index)
            {
                if (array == null)
                {
                    throw new ArgumentNullException(nameof(array));
                }

                if (array.Rank != 1)
                {
                    throw new ArgumentException(ThrowHelper.CommonStrings.Arg_RankMultiDimNotSupported);
                }

                if (array.GetLowerBound(0) != 0)
                {
                    throw new ArgumentException(ThrowHelper.CommonStrings.Arg_NonZeroLowerBound);
                }

                if ((uint)index > (uint)array.Length)
                {
                    ThrowHelper.ThrowIndexArgumentOutOfRange_NeedNonNegNumException();
                }

                if (array.Length - index < _dictionary.Count)
                {
                    throw new ArgumentException(ThrowHelper.CommonStrings.Arg_ArrayPlusOffTooSmall);
                }

                if (array is TValue[] values)
                {
                    CopyTo(values, index);
                }
                else
                {
                    var objects = array as object[];
                    if (objects == null)
                    {
                        throw new ArgumentException(ThrowHelper.CommonStrings.Argument_InvalidArrayType);
                    }

                    var count = _dictionary._count;
                    var entries = _dictionary._entries;
                    try
                    {
                        for (var i = 0; i < count; i++)
                        {
                            if (entries[i]._next >= -1)
                                objects[index++] = entries[i]._value;
                        }
                    }
                    catch (ArrayTypeMismatchException)
                    {
                        throw new ArgumentException(ThrowHelper.CommonStrings.Argument_InvalidArrayType);
                    }
                }
            }

            bool ICollection.IsSynchronized => false;

            object ICollection.SyncRoot => ((ICollection)_dictionary).SyncRoot;

            public struct Enumerator : IEnumerator<TValue>, IEnumerator
            {
                private readonly SegmentedDictionary<TKey, TValue> _dictionary;
                private int _index;
                private readonly int _version;
                private TValue _currentValue;

                internal Enumerator(SegmentedDictionary<TKey, TValue> dictionary)
                {
                    _dictionary = dictionary;
                    _version = dictionary._version;
                    _index = 0;
                    _currentValue = default;
                }

                public void Dispose()
                {
                }

                public bool MoveNext()
                {
                    if (_version != _dictionary._version)
                    {
                        throw new InvalidOperationException(ThrowHelper.CommonStrings.InvalidOperation_EnumFailedVersion);
                    }

                    while ((uint)_index < (uint)_dictionary._count)
                    {
                        ref var entry = ref _dictionary._entries.GetElementByReference(_index++);

                        if (entry._next >= -1)
                        {
                            _currentValue = entry._value;
                            return true;
                        }
                    }
                    _index = _dictionary._count + 1;
                    _currentValue = default;
                    return false;
                }

                public TValue Current => _currentValue;

                object IEnumerator.Current
                {
                    get
                    {
                        if (_index == 0 || (_index == _dictionary._count + 1))
                        {
                            throw new InvalidOperationException(ThrowHelper.CommonStrings.InvalidOperation_EnumOpCantHappen);
                        }

                        return _currentValue;
                    }
                }

                void IEnumerator.Reset()
                {
                    if (_version != _dictionary._version)
                    {
                        throw new InvalidOperationException(ThrowHelper.CommonStrings.InvalidOperation_EnumFailedVersion);
                    }

                    _index = 0;
                    _currentValue = default;
                }
            }
        }
    }
}
