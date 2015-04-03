using Microsoft.Diagnostics.Runtime.Desktop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Address = System.UInt64;

namespace Microsoft.Diagnostics.Runtime
{
    abstract class HeapBase : ClrHeap
    {
        Address m_minAddr;          // Smallest and largest segment in the GC heap.  Used to make SegmentForObject faster.  
        Address m_maxAddr;
        protected ClrSegment[] m_segments;
        ulong[] m_sizeByGen = new Address[4];
        ulong m_totalHeapSize;
        protected int m_lastSegmentIdx;       // The last segment we looked at.
        private bool m_canWalkHeap;
        int m_pointerSize;

        public HeapBase(RuntimeBase runtime)
        {
            m_canWalkHeap = runtime.CanWalkHeap;
            if (runtime.DataReader.CanReadAsync)
                MemoryReader = new AsyncMemoryReader(runtime.DataReader, 0x10000);
            else
                MemoryReader = new MemoryReader(runtime.DataReader, 0x10000);
            m_pointerSize = runtime.PointerSize;

        }

        public override bool ReadPointer(Address addr, out Address value)
        {

            if (MemoryReader.Contains(addr))
                return MemoryReader.ReadPtr(addr, out value);

            return GetRuntime().ReadPointer(addr, out value);
        }

        internal int Revision { get; set; }

        protected abstract int GetRuntimeRevision();

        public override int PointerSize
        {
            get
            {
                return m_pointerSize;
            }
        }

        public override bool CanWalkHeap
        {
            get
            {
                return m_canWalkHeap;
            }
        }

        public override IList<ClrSegment> Segments
        {
            get
            {
                if (Revision != GetRuntimeRevision())
                    ClrDiagnosticsException.ThrowRevisionError(Revision, GetRuntimeRevision());
                return m_segments;
            }
        }
        public override Address TotalHeapSize
        {
            get { return m_totalHeapSize; }
        }

        public override Address GetSizeByGen(int gen)
        {
            Debug.Assert(gen >= 0 && gen < 4);
            return m_sizeByGen[gen];
        }

        public override ClrType GetTypeByName(string name)
        {
            foreach (var module in GetRuntime().EnumerateModules())
            {
                var type = module.GetTypeByName(name);
                if (type != null)
                    return type;
            }

            return null;
        }

        internal MemoryReader MemoryReader { get; private set; }

        protected void UpdateSegmentData(HeapSegment segment)
        {
            m_totalHeapSize += segment.Length;
            m_sizeByGen[0] += segment.Gen0Length;
            m_sizeByGen[1] += segment.Gen1Length;
            if (!segment.Large)
                m_sizeByGen[2] += segment.Gen2Length;
            else
                m_sizeByGen[3] += segment.Gen2Length;
        }

        protected void InitSegments(RuntimeBase runtime)
        {
            // Populate segments
            SubHeap[] heaps;
            if (runtime.GetHeaps(out heaps))
            {
                var segments = new List<HeapSegment>();
                foreach (var heap in heaps)
                {
                    if (heap != null)
                    {
                        ISegmentData seg = runtime.GetSegmentData(heap.FirstLargeSegment);
                        while (seg != null)
                        {
                            var segment = new HeapSegment(runtime, seg, heap, true, this);
                            segments.Add(segment);

                            UpdateSegmentData(segment);
                            seg = runtime.GetSegmentData(seg.Next);
                        }

                        seg = runtime.GetSegmentData(heap.FirstSegment);
                        while (seg != null)
                        {
                            var segment = new HeapSegment(runtime, seg, heap, false, this);
                            segments.Add(segment);

                            UpdateSegmentData(segment);
                            seg = runtime.GetSegmentData(seg.Next);
                        }
                    }
                }

                UpdateSegments(segments.ToArray());
            }
            else
            {
                m_segments = new ClrSegment[0];
            }
        }

        void UpdateSegments(ClrSegment[] segments)
        {
            // sort the segments.  
            Array.Sort(segments, delegate(ClrSegment x, ClrSegment y) { return x.Start.CompareTo(y.Start); });
            m_segments = segments;

            m_minAddr = Address.MaxValue;
            m_maxAddr = Address.MinValue;
            m_totalHeapSize = 0;
            m_sizeByGen = new ulong[4];
            foreach (var gcSegment in m_segments)
            {
                if (gcSegment.Start < m_minAddr)
                    m_minAddr = gcSegment.Start;
                if (m_maxAddr < gcSegment.End)
                    m_maxAddr = gcSegment.End;

                m_totalHeapSize += gcSegment.Length;
                if (gcSegment.Large)
                    m_sizeByGen[3] += gcSegment.Length;
                else
                {
                    m_sizeByGen[2] += gcSegment.Gen2Length;
                    m_sizeByGen[1] += gcSegment.Gen1Length;
                    m_sizeByGen[0] += gcSegment.Gen0Length;
                }
            }
        }


        public override IEnumerable<Address> EnumerateObjects()
        {
            if (Revision != GetRuntimeRevision())
                ClrDiagnosticsException.ThrowRevisionError(Revision, GetRuntimeRevision());

            for (int i = 0; i < m_segments.Length; ++i)
            {
                var seg = m_segments[i];
                for (ulong obj = seg.FirstObject; obj != 0; obj = seg.NextObject(obj))
                {
                    m_lastSegmentIdx = i;
                    yield return obj;
                }
            }
        }

        public override ClrSegment GetSegmentByAddress(Address objRef)
        {
            if (m_minAddr <= objRef && objRef < m_maxAddr)
            {
                // Start the segment search where you where last
                int curIdx = m_lastSegmentIdx;
                for (; ; )
                {
                    var segment = m_segments[curIdx];
                    var offsetInSegment = (long)(objRef - segment.Start);
                    if (0 <= offsetInSegment)
                    {
                        var intOffsetInSegment = (long)offsetInSegment;
                        if (intOffsetInSegment < (long)segment.Length)
                        {
                            m_lastSegmentIdx = curIdx;
                            return segment;
                        }
                    }

                    // Get the next segment loop until you come back to where you started.  
                    curIdx++;
                    if (curIdx >= Segments.Count)
                        curIdx = 0;
                    if (curIdx == m_lastSegmentIdx)
                        break;
                }
            }
            return null;
        }
    }


    class HeapSegment : ClrSegment
    {
        public override int ProcessorAffinity
        {
            get { return m_subHeap.HeapNum; }
        }
        public override Address Start { get { return m_segment.Start; } }
        public override Address End { get { return m_subHeap.EphemeralSegment == m_segment.Address ? m_subHeap.EphemeralEnd : m_segment.End; } }
        public override ClrHeap Heap { get { return m_heap; } }

        public override bool Large { get { return m_large; } }

        public override Address ReservedEnd { get { return m_segment.Reserved; } }
        public override Address CommittedEnd { get { return m_segment.Committed; } }

        public override Address Gen0Start
        {
            get
            {
                if (Ephemeral)
                    return m_subHeap.Gen0Start;
                else
                    return End;
            }
        }
        public override Address Gen0Length { get { return End - Gen0Start; } }
        public override Address Gen1Start
        {
            get
            {
                if (Ephemeral)
                    return m_subHeap.Gen1Start;
                else
                    return End;
            }
        }
        public override Address Gen1Length { get { return Gen0Start - Gen1Start; } }
        public override Address Gen2Start { get { return Start; } }
        public override Address Gen2Length { get { return Gen1Start - Start; } }


        public override IEnumerable<Address> EnumerateObjects()
        {
            for (ulong obj = FirstObject; obj != 0; obj = NextObject(obj))
                yield return obj;
        }

        public override Address FirstObject
        {
            get
            {
                if (Gen2Start == End)
                    return 0;
                m_heap.MemoryReader.EnsureRangeInCache(Gen2Start);
                return Gen2Start;
            }
        }

        public override Address NextObject(Address addr)
        {
            if (addr >= CommittedEnd)
                return 0;

            uint minObjSize = (uint)m_clr.PointerSize * 3;

            ClrType type = m_heap.GetObjectType(addr);
            if (type == null)
                return 0;

            ulong size = type.GetSize(addr);
            size = Align(size, m_large);
            if (size < minObjSize)
                size = minObjSize;

            // Move to the next object
            addr += size;

            // Check to make sure a GC didn't cause "count" to be invalid, leading to too large
            // of an object
            if (addr >= End)
                return 0;

            // Ensure we aren't at the start of an alloc context
            ulong tmp;
            while (!Large && m_subHeap.AllocPointers.TryGetValue(addr, out tmp))
            {
                tmp += Align(minObjSize, m_large);

                // Only if there's data corruption:
                if (addr >= tmp)
                    return 0;

                // Otherwise:
                addr = tmp;

                if (addr >= End)
                    return 0;
            }

            return addr;
        }

        #region private
        internal static Address Align(ulong size, bool large)
        {
            Address AlignConst;
            Address AlignLargeConst = 7;

            if (IntPtr.Size == 4)
                AlignConst = 3;
            else
                AlignConst = 7;

            if (large)
                return (size + AlignLargeConst) & ~(AlignLargeConst);

            return (size + AlignConst) & ~(AlignConst);
        }

        public override bool Ephemeral { get { return m_segment.Address == m_subHeap.EphemeralSegment; ; } }
        internal HeapSegment(RuntimeBase clr, ISegmentData segment, SubHeap subHeap, bool large, HeapBase heap)
        {
            m_clr = clr;
            m_large = large;
            m_segment = segment;
            m_heap = heap;
            m_subHeap = subHeap;
        }

        private bool m_large;
        private RuntimeBase m_clr;
        private ISegmentData m_segment;
        private SubHeap m_subHeap;
        private HeapBase m_heap;
        #endregion
    }

}
