using System;
using System.Collections.Generic;

namespace ETLStackBrowse
{
    [Serializable]
    public class ByteAtomTable
    {
        [Serializable]
        private struct Bucket
        {
            public int id;
            public byte[] bytes;
        }

        private Bucket[] buckets = new Bucket[128];
        private int count;

        private List<byte[]> idMap = new List<byte[]>();

        public int Count
        {
            get
            {
                return count;
            }
        }

        public byte[] GetBytes(int id)
        {
            return idMap[id];
        }

        public string MakeString(int id)
        {
            return ByteWindow.MakeString(GetBytes(id));
        }

        private uint Hash(byte[] bytes)
        {
            uint hash = 0;

            foreach (byte b in bytes)
            {
                hash = ((hash << 2) | (hash >> 30)) ^ b;
            }

            return hash;
        }

        private uint Hash(ref ByteWindow by)
        {
            int len = by.len;
            int ib = by.ib;
            byte[] buffer = by.buffer;

            uint hash = 0;

            for (int i = 0; i < len; i++)
            {
                hash = ((hash << 2) | (hash >> 30)) ^ buffer[ib + i];
            }

            return hash;
        }

        private bool Equals(ByteWindow by, byte[] bytes)
        {
            if (by.len != bytes.Length)
            {
                return false;
            }

            int len = by.len;
            int ib = by.ib;
            byte[] buffer = by.buffer;

            for (int i = len; --i >= 0;)
            {
                if (bytes[i] != buffer[ib + i])
                {
                    return false;
                }
            }

            return true;

        }

        private ByteWindow bT = new ByteWindow();

        public int Lookup(string s)
        {
            return Lookup(bT.Assign(s));
        }

        public int Lookup(ByteWindow by)
        {
            uint hash = Hash(ref by);
            int i = (int)(hash % buckets.Length);

            for (; ; )
            {
                if (buckets[i].bytes == null)
                {
                    return -1;
                }

                if (Equals(by, buckets[i].bytes))
                {
                    return buckets[i].id;
                }

                i++;
                if (i == buckets.Length)
                {
                    i = 0;
                }
            }
        }

        public int EnsureContains(byte[] b)
        {
            bT.Assign(b);
            return EnsureContains(bT);
        }

        public int EnsureContains(ByteWindow by)
        {
            if (count >= buckets.Length / 10 * 7)
            {
                Rehash();
            }

            uint hash = Hash(ref by);
            int i = (int)(hash % buckets.Length);

            for (; ; )
            {
                if (buckets[i].bytes == null)
                {
                    buckets[i].id = count;
                    buckets[i].bytes = by.Clone();

                    idMap.Add(buckets[i].bytes);
                    return count++;
                }

                if (Equals(by, buckets[i].bytes))
                {
                    return buckets[i].id;
                }

                i++;
                if (i == buckets.Length)
                {
                    i = 0;
                }
            }
        }

        private void Rehash()
        {
            Bucket[] bucketsNew = new Bucket[buckets.Length * 2];

            for (int j = 0; j < buckets.Length; j++)
            {
                if (buckets[j].bytes == null)
                {
                    continue;
                }

                int i = (int)(Hash(buckets[j].bytes) % bucketsNew.Length);

                for (; ; )
                {
                    if (bucketsNew[i].bytes == null)
                    {
                        bucketsNew[i] = buckets[j];
                        break;
                    }

                    i++;
                    if (i == bucketsNew.Length)
                    {
                        i = 0;
                    }
                }
            }

            buckets = bucketsNew;
        }
    }
}
