#if _REDHAWK
using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Runtime.Redhawk
{
    class RhType : ClrType
    {
        private string m_name;
        private ulong m_eeType;
        private RhHeap m_heap;
        private RhModule m_module;
        private uint m_baseSize;
        private uint m_componentSize;
        private GCDesc m_gcDesc;
        private bool m_containsPointers;
        int m_index;

        public RhType(RhHeap heap, int index, RhModule module, string name, ulong eeType, Microsoft.Diagnostics.Runtime.Desktop.IMethodTableData mtData)
        {
            m_heap = heap;
            m_module = module;
            m_name = name;
            m_eeType = eeType;
            m_index = index;

            m_baseSize = mtData.BaseSize;
            m_componentSize = mtData.ComponentSize;
            m_containsPointers = mtData.ContainsPointers;
        }

        public override int Index
        {
            get { return m_index; }
        }

        public override ClrModule Module
        {
            get
            {
                return m_module;
            }
        }

        public override string Name
        {
            get { return m_name; }
        }

        public override ulong GetSize(ulong objRef)
        {
            ulong size;
            uint pointerSize = (uint)m_heap.PointerSize;
            if (m_componentSize == 0)
            {
                size = m_baseSize;
            }
            else
            {
                uint count = 0;
                uint countOffset = pointerSize;
                ulong loc = objRef + countOffset;

                var cache = m_heap.MemoryReader;
                if (!cache.Contains(loc))
                    cache = m_heap.m_runtime.MemoryReader;

                if (!cache.ReadDword(loc, out count))
                    throw new Exception("Could not read from heap at " + objRef.ToString("x"));

                // TODO:  Strings in v4+ contain a trailing null terminator not accounted for.
                

                size = count * (ulong)m_componentSize + m_baseSize;
            }

            uint minSize = pointerSize * 3;
            if (size < minSize)
                size = minSize;
            return size;
        }

        public override void EnumerateRefsOfObject(ulong objRef, Action<ulong, int> action)
        {
            if (!m_containsPointers)
                return;

            if (m_gcDesc == null)
                if (!FillGCDesc() || m_gcDesc == null)
                    return;

            var size = GetSize(objRef);
            var cache = m_heap.MemoryReader;
            if (!cache.Contains(objRef))
                cache = m_heap.m_runtime.MemoryReader;

            m_gcDesc.WalkObject(objRef, (ulong)size, cache, action);
        }


        bool FillGCDesc()
        {
            RhRuntime runtime = m_heap.m_runtime;

            int entries;
            if (!runtime.MemoryReader.TryReadDword(m_eeType - (ulong)IntPtr.Size, out entries))
                return false;

            // Get entries in map
            if (entries < 0)
                entries = -entries;

            int read;
            int slots = 1 + entries * 2;
            byte[] buffer = new byte[slots * IntPtr.Size];
            if (!runtime.ReadMemory(m_eeType - (ulong)(slots * IntPtr.Size), buffer, buffer.Length, out read) || read != buffer.Length)
                return false;

            // Construct the gc desc
            m_gcDesc = new GCDesc(buffer);
            return true;
        }

        public override ClrHeap Heap
        {
            get { throw new NotImplementedException(); }
        }

        public override IList<ClrInterface> Interfaces
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsFinalizable
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsPublic
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsPrivate
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsInternal
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsProtected
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsAbstract
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsSealed
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsInterface
        {
            get { throw new NotImplementedException(); }
        }

        public override bool GetFieldForOffset(int fieldOffset, bool inner, out ClrInstanceField childField, out int childFieldOffset)
        {
            throw new NotImplementedException();
        }

        public override ClrInstanceField GetFieldByName(string name)
        {
            throw new NotImplementedException();
        }

        public override ClrType BaseType
        {
            get { throw new NotImplementedException(); }
        }

        public override int GetArrayLength(ulong objRef)
        {
            throw new NotImplementedException();
        }

        public override ulong GetArrayElementAddress(ulong objRef, int index)
        {
            throw new NotImplementedException();
        }

        public override object GetArrayElementValue(ulong objRef, int index)
        {
            throw new NotImplementedException();
        }

        public override int ElementSize
        {
            get { throw new NotImplementedException(); }
        }

        public override int BaseSize
        {
            get { throw new NotImplementedException(); }
        }

        public override ClrStaticField GetStaticFieldByName(string name)
        {
            throw new NotImplementedException();
        }

        public override void EnumerateRefsOfObjectCarefully(ulong objRef, Action<ulong, int> action)
        {
            EnumerateRefsOfObject(objRef, action);
        }

        public override uint MetadataToken
        {
            get { throw new NotImplementedException(); }
        }
    }
}
#endif