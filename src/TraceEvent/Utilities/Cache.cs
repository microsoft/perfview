using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Utilities
{
    /// <summary>
    /// A finite cache based with a least reciently used algorithm for replacement.   
    /// It is meant to be fast (fast as a hashtable), and space efficient (not much
    /// over the MaxEntry key-value pairs are stored.  (only 8 bytes per entry additional).  
    /// </summary>
    public class Cache<K, T> where T : class where K : IEquatable<K>
    {
        /// <summary>
        /// maxEntries currently is only set in the constructor.   Thus this is a finite sized cache
        /// but is otherwise very efficient.  Currently it uses ushorts internally so the number
        /// of entries is limited to 64K (it silently limits it if you give maxEntries > 64K).  
        /// </summary>
        /// <param name="maxEntries"></param>
        public Cache(int maxEntries)
        {
            // We use ushorts in the implemenation, so make sure we are in the regime that we can represent.  
            if (maxEntries > 0xFFFE)
                maxEntries = 0xFFFE;

            // The Hash table is only ushorts, so it is OK to have a bigger table.   
            var hashEntries = maxEntries * 2 + 1;

            m_hashTable = new ushort[hashEntries]; 
            m_entries = new CacheEntry[maxEntries];
            Clear();
        }

        public T Get(K key)
        {
            T retVal;
            TryGet(key, out retVal);
            return retVal;
        }

        public bool TryGet(K key, out T valueRet)
        {
            int hash = key.GetHashCode();
            uint tableIndex = (uint)((uint)hash % (uint)m_hashTable.Length);
            int entryIndex = m_hashTable[tableIndex];
            ushort entryHash = (ushort)(hash ^ (hash >> 16));
            for (;;)
            {
                if (entryIndex == End)
                {
                    valueRet = default(T);
                    return false;
                }
                CacheEntry entry = m_entries[entryIndex];
                if (entry.Hash == entryHash && entry.Key.Equals(key))
                {
                    valueRet = entry.Value;
                    return true;
                }
                entryIndex = entry.Next;
            }
        }

        public void Add(K key, T value)
        {
            int hash = key.GetHashCode();
            uint tableIndex = (uint)((uint)hash % (uint)m_hashTable.Length);
            ushort entryHash = (ushort)(hash ^ (hash >> 16));

            ushort entryIndex = GetFreeEntry();
            m_entries[entryIndex] = new CacheEntry() { Hash = entryHash, Key = key, Value = value, Next = m_hashTable[tableIndex] };
            m_hashTable[tableIndex] = entryIndex;
        }

        /// <summary>
        /// Remvoves all entries in the cache.  
        /// </summary>
        public void Clear()
        {
            // Intialize the has table to be emtpy (pointers pointing to 'End')
            for (int i = 0; i < m_hashTable.Length; i++)
                m_hashTable[i] = End;

            // Create a free list that consists of all the etnries. 
            m_freeList = 0;
            for (int i = 0; i < m_entries.Length; i++)
                m_entries[i].Next = (ushort)(i + 1);
            m_entries[m_entries.Length - 1].Next = End;
        }

        public int MaxEntries
        {
            get { return m_entries.Length; }
        }

        #region private 
        // Finds an free entry in the table and returns the index to it.  
        private ushort GetFreeEntry()
        {
            for (;;)
            {
                // Try to get it from the free list
                if (m_freeList != End)
                {
                    ushort ret = m_freeList;
                    m_freeList = m_entries[m_freeList].Next;
                    return ret;
                }
                // If freelist is empty populate it.  
                FreeUpEntries();
            }
        }

        // insures that m_freeList is not empty.  Only called from GetFreeEntry(). 
        // Ideally this would find the least-reciently used entry and put that on
        // the free list.   We only approximate this however.   
        private void FreeUpEntries()
        {
            Debug.Assert(m_freeList == End);

            // Right now we simply round robbin, taking what amounts to a random entry
            // and freeing it.   Later we will be smarter and remove older entries first, 
            // but frankly this is not bad since popular entries will come back quickly.  
            // TODO Fix so that we remember an 'age' and preferentially remove older entries. 
            for (;;)
            {
                // Continue the scan where we left off and simply free the next non-empty
                // hash chain we encounter.  
                var cur = m_hashTable[m_freeScan];
                if (cur != End)
                {
                    m_freeList = cur;
                    m_hashTable[m_freeScan] = End;
                    return;
                }
                m_freeScan++;
                if (m_freeScan >= m_hashTable.Length)
                    m_freeScan = 0;
            }
        }

        // Holds a entry in our open hash table.  
        struct CacheEntry
        {
            public ushort Next;        // index to the next entry in m_entries in the chain 
            public ushort Hash;        // the hash associated with Value
            public K Key;
            public T Value;
        }

#if DEBUG
        public override string ToString()
        {
            System.IO.StringWriter writer = new System.IO.StringWriter();

            writer.WriteLine("Cache {");
            writer.WriteLine(" MaxEntries: {0}", MaxEntries);
            writer.WriteLine(" FreeListLength: {0}", FreeListLength());
            writer.WriteLine(" values: [");
            for (int i = 0; i < m_hashTable.Length; i++)
            {
                var entryIdx = m_hashTable[i];
                while (entryIdx != End)
                {
                    writer.WriteLine("   Entry {");
                    writer.WriteLine("      HashIdx: {0}", i);
                    writer.WriteLine("      EntryIdx: {0}", entryIdx);
                    writer.WriteLine("      Hash: {0}", m_entries[entryIdx].Hash);
                    writer.WriteLine("      Key: {0}", m_entries[entryIdx].Key.ToString());
                    writer.WriteLine("      Value: {0}", m_entries[entryIdx].Value.ToString());
                    writer.WriteLine("   },");
                    entryIdx = m_entries[entryIdx].Next;
                }
            }

            writer.WriteLine(" ],");
            writer.WriteLine("}");
            return writer.ToString();
        }

        private int FreeListLength()
        {
            int ret = 0;
            var cur = m_freeList;
            while (cur != End)
            {
                ret++;
                cur = m_entries[cur].Next;
            }
            return ret;
        }
#endif

        /// <summary>
        /// Reprenents a null pointer (end of a linked list)
        /// </summary>
        const ushort End = 0xFFFF;     

        // fields...
        ushort[] m_hashTable;         // We hash here, which returns an index 
        CacheEntry[] m_entries;       // which point here.  Effectively this is open hashing, but is more efficient than using pointers.  
        ushort m_freeList;            // A linked list of entries that are free.  
        int m_freeScan;               // Indicates where in m_hashTable we should look for entries to kill to make room for new entries.  (see FreeUpEntries)
        #endregion
    }
}
