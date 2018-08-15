using System;
using System.Collections.Generic;
using System.IO;

namespace ETLStackBrowse
{
    public partial class ETLTrace
    {
        // common field indexes in memory records
        private const int fldProcessNamePID = 2;
        private const int fldMemBaseAddr = 3;
        private const int fldMemEndAddr = 4;
        private const int fldFlags = 5;

        // common field indexes for image start/end records
        private const int fldImageBaseAddr = 3;
        private const int fldImageEndAddr = 4;
        private const int fldImageFileName = 8;

        public const int memoryPlotColumns = 80;
        public const int memoryPlotRows = standardPlotRows;

        public class MemInfo
        {
            public uint cReserved;
            public ulong cbReserved;

            public uint cCommitted;
            public ulong cbCommitted;

            public uint cDecommitted;
            public ulong cbDecommitted;

            public uint cReleased;
            public ulong cbReleased;

            public byte[] processPid;

            public RangeSet rsCommitted = new RangeSet();
            public RangeSet rsReserved = new RangeSet();

            public long[] reservedDistribution = new long[memoryPlotColumns];
            public long[] committedDistribution = new long[memoryPlotColumns];

            internal void Reset()
            {
                cReserved = 0;
                cbReserved = 0;
                cCommitted = 0;
                cbCommitted = 0;
                cDecommitted = 0;
                cbDecommitted = 0;
                cReleased = 0;
                cbReleased = 0;
            }

            public MemInfo(byte[] processPid)
            {
                this.processPid = processPid;
            }
        }

        private MemProcessor lastMemProcessor = null;
        public MemProcessor LastMemProcessor
        {
            get { return lastMemProcessor; }
        }

        public string ComputeMemory(int intervals, bool fDumpRanges)
        {
            bool fFilterProcesses = itparms.EnableProcessFilter;

            StringWriter sw = new StringWriter();

            var mem = new MemProcessor(this);

            ETWLineReader l = StandardLineReader();

            long tDelta = (l.t1 - l.t0) / intervals;
            long tStart = l.t0;
            long tNext = l.t0 + tDelta;
            int iInterval = 0;

            bool fOut = false;

            MemEffect effect = new MemEffect();

            foreach (ByteWindow b in l.Lines())
            {
                if (l.t > tNext && iInterval < intervals - 1)
                {
                    if (!mem.IsEmpty)
                    {
                        fOut = true;
                        DumpRangeTime(sw, tStart, tNext);
                        sw.WriteLine(mem.Dump());
                    }

                    mem.Reset();
                    tStart = tNext;
                    tNext += tDelta;
                }

                if (l.idType != mem.idAlloc && l.idType != mem.idFree)
                {
                    continue;
                }

                if (fFilterProcesses && !l.MatchingProcess())
                {
                    continue;
                }

                if (!l.MatchingTextFilter())
                {
                    continue;
                }

                if (!l.MatchingMemory())
                {
                    continue;
                }

                mem.ProcessMemRecord(l, b, effect);
            }

            if (!mem.IsEmpty)
            {
                fOut = true;
                DumpRangeTime(sw, tStart, l.t1);
                sw.WriteLine(mem.Dump());
            }

            sw.WriteLine(mem.DumpMemoryPlots());

            if (fOut)
            {
                if (fDumpRanges)
                {
                    sw.WriteLine();
                    sw.WriteLine(mem.DumpRanges());
                }
            }
            else
            {
                sw.WriteLine("No activity");
            }

            lastMemProcessor = mem;
            return sw.ToString();
        }

        private void DumpRangeTime(StringWriter sw, long tStart, long tEnd)
        {
            sw.WriteLine();
            sw.WriteLine("Summary for time range {0:n0} to {1:n0}", tStart, tEnd);
            sw.WriteLine();
        }

        public class MemEffect
        {
            public ulong reserved;
            public ulong committed;
            public ulong decommitted;
            public ulong released;
        }

        public class MemProcessor
        {
            private ByteWindow bT = new ByteWindow();
            public int idAlloc;
            public int idFree;
            private byte[] byRelease = ByteWindow.MakeBytes("RELEASE");
            private byte[] byReserve = ByteWindow.MakeBytes("RESERVE");
            private byte[] byCommit = ByteWindow.MakeBytes("COMMIT");
            private byte[] byDecommit = ByteWindow.MakeBytes("DECOMMIT");
            private byte[] byReserveCommit = ByteWindow.MakeBytes("RESERVE COMMIT");
            private ETLTrace trace;

            public MemInfo[] memInfos;

            public MemProcessor(ETLTrace trace)
            {
                this.trace = trace;

                idAlloc = trace.atomsRecords.Lookup("VirtualAlloc");
                idFree = trace.atomsRecords.Lookup("VirtualFree");

                memInfos = new MemInfo[trace.atomsProcesses.Count];
                Reset();
            }

            public void Reset()
            {
                for (int i = 0; i < memInfos.Length; i++)
                {
                    if (memInfos[i] == null)
                    {
                        memInfos[i] = new MemInfo(trace.processes[i].processPid);
                    }
                    else
                    {
                        memInfos[i].Reset();
                    }
                }
            }

            public void ProcessMemRecord(ETWLineReader l, ByteWindow b, MemEffect memeffect)
            {
                // empty the net effect of this record
                memeffect.released = memeffect.reserved = memeffect.committed = memeffect.decommitted = 0;

                int interval = (int)((l.t - l.t0) / (double)(l.t1 - l.t0) * memoryPlotColumns);

                // the above can overflow because time ranges might be out of order so clamp any overflows
                // also we have to handle the case where l.t == l.t1 which would otherwise overflow
                if (interval < 0)
                {
                    interval = 0;
                }

                if (interval >= memoryPlotColumns)
                {
                    interval = memoryPlotColumns - 1;
                }

                bT.Assign(b, fldProcessNamePID).Trim();
                int idProcess = trace.atomsProcesses.Lookup(bT);

                // bogus process, disregard
                if (idProcess == -1)
                {
                    return;
                }

                MemInfo p = memInfos[idProcess];

                var rsReserved = p.rsReserved;
                var rsCommitted = p.rsCommitted;

                ulong addrBase = b.GetHex(fldMemBaseAddr);
                ulong addrEnd = b.GetHex(fldMemEndAddr);

                b.Field(fldFlags).Trim();

                if (l.idType == idAlloc)
                {
                    if (b.StartsWith(byReserveCommit))
                    {
                        memeffect.reserved = rsReserved.AddRange(addrBase, addrEnd);
                        p.cbReserved += memeffect.reserved;
                        p.reservedDistribution[interval] += (long)memeffect.reserved;
                        if (memeffect.reserved != 0)
                        {
                            p.cReserved++;
                        }

                        memeffect.committed = rsCommitted.AddRange(addrBase, addrEnd);
                        p.cbCommitted += memeffect.committed;
                        p.committedDistribution[interval] += (long)memeffect.committed;
                        if (memeffect.committed != 0)
                        {
                            p.cCommitted++;
                        }
                    }
                    else if (b.StartsWith(byReserve))
                    {
                        memeffect.reserved = rsReserved.AddRange(addrBase, addrEnd);
                        p.cbReserved += memeffect.reserved;
                        p.reservedDistribution[interval] += (long)memeffect.reserved;
                        if (memeffect.reserved != 0)
                        {
                            p.cReserved++;
                        }
                    }
                    else if (b.StartsWith(byCommit))
                    {
                        memeffect.committed = rsCommitted.AddRange(addrBase, addrEnd);
                        p.cbCommitted += memeffect.committed;
                        p.committedDistribution[interval] += (long)memeffect.committed;
                        if (memeffect.committed != 0)
                        {
                            p.cCommitted++;
                        }
                    }
                }

                if (l.idType == idFree)
                {
                    if (b.StartsWith(byRelease))
                    {
                        memeffect.decommitted = rsCommitted.RemoveRange(addrBase, addrEnd);
                        p.cbDecommitted += memeffect.decommitted;
                        p.committedDistribution[interval] -= (long)memeffect.decommitted;
                        if (memeffect.decommitted != 0)
                        {
                            p.cDecommitted++;
                        }

                        memeffect.released = rsReserved.RemoveRange(addrBase, addrEnd);
                        p.cbReleased += memeffect.released;
                        p.reservedDistribution[interval] -= (long)memeffect.released;
                        if (memeffect.released != 0)
                        {
                            p.cReleased++;
                        }
                    }
                    else if (b.StartsWith(byDecommit))
                    {
                        memeffect.decommitted = rsCommitted.RemoveRange(addrBase, addrEnd);
                        p.cbDecommitted += memeffect.decommitted;
                        p.committedDistribution[interval] -= (long)memeffect.decommitted;
                        if (memeffect.decommitted != 0)
                        {
                            p.cDecommitted++;
                        }
                    }
                }
            }

            public string DumpMemoryPlots()
            {
                if (sortedMemInfos == null || sortedMemInfos.Length < 1)
                {
                    return "";
                }

                StringWriter sw = new StringWriter();

                sw.WriteLine();
                sw.WriteLine("VM PLOTS FOR SELECTED PROCESSES");
                sw.WriteLine();

                int count = 0;

                foreach (int ipi in sortedMemInfos)
                {
                    MemInfo p = memInfos[ipi];

                    if (p.cCommitted == 0 && p.cDecommitted == 0 && p.cReleased == 0 && p.cReserved == 0)
                    {
                        continue;
                    }

                    sw.WriteLine();
                    sw.WriteLine();

                    string xLabel = String.Format("Usage from {0}", ByteWindow.MakeString(p.processPid));
                    string yLabel = "Reserved Bytes";

                    sw.WriteLine(FormatOnePlot(memoryPlotRows, memoryPlotColumns, xLabel, yLabel, p.reservedDistribution));

                    sw.WriteLine();
                    sw.WriteLine();

                    xLabel = String.Format("Usage from {0}", ByteWindow.MakeString(p.processPid));
                    yLabel = "Committed Bytes";

                    sw.WriteLine(FormatOnePlot(memoryPlotRows, memoryPlotColumns, xLabel, yLabel, p.committedDistribution));

                    count++;

                    if (count >= 3)
                    {
                        break;
                    }
                }

                return sw.ToString();
            }

            public string DumpRanges()
            {
                StringWriter sw = new StringWriter();

                foreach (int ipi in sortedMemInfos)
                {
                    MemInfo p = memInfos[ipi];

                    if (p.cCommitted == 0 && p.cDecommitted == 0 && p.cReleased == 0 && p.cReserved == 0)
                    {
                        continue;
                    }

                    sw.WriteLine("Live ranges for {0}", ByteWindow.MakeString(p.processPid));
                    sw.WriteLine();
                    sw.WriteLine("RESERVED  ({0} ranges)", p.rsReserved.RangeCount);
                    sw.WriteLine();
                    sw.WriteLine(p.rsReserved.Dump());
                    sw.WriteLine();
                    sw.WriteLine("COMMITTED ({0} ranges)", p.rsCommitted.RangeCount);
                    sw.WriteLine();
                    sw.WriteLine(p.rsCommitted.Dump());
                    sw.WriteLine();
                }

                return sw.ToString();
            }

            public int[] sortedMemInfos;

            public bool IsEmpty
            {
                get
                {
                    foreach (MemInfo p in memInfos)
                    {
                        if (p.cCommitted != 0 || p.cDecommitted != 0 || p.cReleased != 0 || p.cReserved != 0)
                        {
                            return false;
                        }
                    }
                    return true;
                }
            }

            public string Dump()
            {
                StringWriter sw = new StringWriter();

                SortMemInfos();

                sw.WriteLine("VM CHANGES IN THIS INTERVAL");
                sw.WriteLine();

                sw.WriteLine("{8,-35} {0,14} {1,14} {2,14} {3,14} {4,14} {5,14} {6,14} {7,14}",
                    "Reserved", "Count",
                    "Committed", "Count",
                    "Decomitt", "Count",
                    "Released", "Count",
                    "Process");

                sw.WriteLine("{8,-35} {0,14} {1,14} {2,14} {3,14} {4,14} {5,14} {6,14} {7,14}",
                    "---------", "-----",
                    "---------", "-----",
                    "--------", "-----",
                    "--------", "-----",
                    "-------");

                foreach (int ipi in sortedMemInfos)
                {
                    MemInfo p = memInfos[ipi];

                    if (p.cCommitted == 0 && p.cDecommitted == 0 && p.cReleased == 0 && p.cReserved == 0)
                    {
                        continue;
                    }

                    sw.WriteLine("{8,-35} {0,14:n0} {1,14:n0} {2,14:n0} {3,14:n0} {4,14:n0} {5,14:n0} {6,14:n0} {7,14:n0}"
                        , p.cbReserved
                        , p.cReserved
                        , p.cbCommitted
                        , p.cCommitted
                        , p.cbDecommitted
                        , p.cDecommitted
                        , p.cbReleased
                        , p.cReleased
                        , ByteWindow.MakeString(p.processPid)
                    );
                }

                sw.WriteLine();
                sw.WriteLine("VM ALLOCATED AT END OF THIS INTERVAL");
                sw.WriteLine();


                sw.WriteLine("{4,-35} {0,14} {1,14} {2,14} {3,14}",
                    "Reserved", "Ranges",
                    "Committed", "Ranges",
                    "Process");

                sw.WriteLine("{4,-35} {0,14} {1,14} {2,14} {3,14}",
                    "--------", "------",
                    "---------", "------",
                    "-------");

                foreach (int ipi in sortedMemInfos)
                {
                    MemInfo p = memInfos[ipi];

                    if (p.cCommitted == 0 && p.cDecommitted == 0 && p.cReleased == 0 && p.cReserved == 0)
                    {
                        continue;
                    }

                    sw.WriteLine("{4,-35} {0,14:n0} {1,14:n0} {2,14:n0} {3,14:n0}"
                        , p.rsReserved.Count
                        , p.rsReserved.RangeCount
                        , p.rsCommitted.Count
                        , p.rsCommitted.RangeCount
                        , ByteWindow.MakeString(p.processPid)
                    );
                }

                return sw.ToString();
            }

            private void SortMemInfos()
            {
                sortedMemInfos = new int[memInfos.Length];

                for (int i = 0; i < sortedMemInfos.Length; i++)
                {
                    sortedMemInfos[i] = i;
                }

                Array.Sort<int>(sortedMemInfos, delegate (int i, int j)
                {
                    MemInfo a = memInfos[i];
                    MemInfo b = memInfos[j];

                    if (a.cbCommitted > b.cbCommitted)
                    {
                        return -1;
                    }

                    if (a.cbCommitted < b.cbCommitted)
                    {
                        return 1;
                    }

                    return 0;
                }
                );
            }
        }
    }

    public struct Range
    {
        public ulong lo;
        public ulong hi;
    }

    public class RangeSet
    {
        private List<Range> ranges = new List<Range>();

        public ulong AddRange(ulong lo, ulong hi)
        {
            // start with the base count, we'll subtract the overlaps to find the net size change
            ulong count = hi - lo;
            int iExtended = -1;

            if (count == 0)
            {
                return 0;
            }

            int i = FirstAffectedRange(lo); ;
            for (; i < ranges.Count; i++)
            {
                Range rMerge = ranges[i];

                // this item is before the range
                if (rMerge.hi < lo)
                {
                    continue;
                }

                // this item is after the range, stop here
                if (rMerge.lo > hi)
                {
                    break;
                }

                // ok there is some overlap, so we have to do something

                ulong olo = Math.Max(lo, rMerge.lo);
                ulong ohi = Math.Min(hi, rMerge.hi);
                ulong overlap = ohi - olo;
                count -= overlap;

                if (iExtended < 0)
                {
                    rMerge.lo = Math.Min(lo, rMerge.lo);
                    rMerge.hi = Math.Max(hi, rMerge.hi);

                    // this is the first item that overlaps so we're going to extend it
                    ranges[i] = rMerge;
                    iExtended = i;
                }
                else
                {
                    rMerge.lo = ranges[iExtended].lo;
                    rMerge.hi = Math.Max(hi, rMerge.hi);

                    // we have already extended something to cover the range so now what we're going to do is
                    // remove this guy and further extend the previous item
                    ranges[iExtended] = rMerge;
                    ranges.RemoveAt(i);
                    i--;  // process i again, now that something has been removed
                }
            }

            // add it
            if (iExtended < 0)
            {
                var rNew = new Range();
                rNew.lo = lo;
                rNew.hi = hi;
                ranges.Insert(i, rNew);
            }
            return count;
        }

        public void AddRangesFromString(string str)
        {
            StringReader sr = new StringReader(str);

            string line = null;

            char[] spacetab = new char[] { ' ', '\t' };

            while ((line = sr.ReadLine()) != null)
            {
                line = line.Trim();
                int ich = line.IndexOfAny(spacetab);
                if (ich < 0)
                {
                    continue;
                }

                ulong start = ETLTrace.ParseHex(line, 0);
                ulong end = ETLTrace.ParseHex(line, ich);

                if (start >= end)
                {
                    continue;
                }

                AddRange(start, end);
            }
        }

        public void AddRangeSet(RangeSet merge)
        {
            int c = merge.ranges.Count;

            for (int i = 0; i < c; i++)
            {
                Range r = merge.ranges[i];
                AddRange(r.lo, r.hi);
            }
        }

        public void RemoveRangeSet(RangeSet merge)
        {
            int c = merge.ranges.Count;

            for (int i = 0; i < c; i++)
            {
                Range r = merge.ranges[i];
                RemoveRange(r.lo, r.hi);
            }
        }

        public string Dump()
        {
            StringWriter sw = new StringWriter();

            int c = ranges.Count;

            for (int i = 0; i < c; i++)
            {
                Range r = ranges[i];
                sw.WriteLine("0x{0:x} 0x{1:x}", r.lo, r.hi);
            }

            return sw.ToString();
        }

        public bool Equals(RangeSet rs)
        {
            int c = ranges.Count;

            if (c != rs.ranges.Count)
            {
                return false;
            }

            for (int i = 0; i < c; i++)
            {
                Range r1 = ranges[i];
                Range r2 = rs.ranges[i];

                if (r1.lo != r2.lo)
                {
                    return false;
                }

                if (r1.hi != r2.hi)
                {
                    return false;
                }
            }

            return true;
        }

        public RangeSet Union(RangeSet rs2)
        {
            RangeSet rOut = new RangeSet();
            RangeSet rs1 = this;

            int i1 = 0;
            int i2 = 0;

            // try to add them in order because appending is the fastest
            while (i1 < rs1.RangeCount && i2 < rs2.RangeCount)
            {
                Range r1 = rs1.ranges[i1];
                Range r2 = rs2.ranges[i2];

                if (r1.hi < r2.hi)
                {
                    rOut.AddRange(r1.lo, r1.hi);
                    i1++;
                }
                else
                {
                    rOut.AddRange(r2.lo, r2.hi);
                    i2++;
                }
            }

            // now add whatever is left, we could have just done this in the first place but we wanted them in order

            while (i1 < rs1.RangeCount)
            {
                Range r1 = rs1.ranges[i1];
                rOut.AddRange(r1.lo, r1.hi);
                i1++;
            }

            while (i2 < rs2.RangeCount)
            {
                Range r2 = rs2.ranges[i2];
                rOut.AddRange(r2.lo, r2.hi);
                i2++;
            }

            return rOut;
        }

        public RangeSet Intersection(RangeSet rs2)
        {
            RangeSet rOut = new RangeSet();
            RangeSet rs1 = this;

            int i1 = 0;
            int i2 = 0;

            while (i1 < rs1.RangeCount && i2 < rs2.RangeCount)
            {
                Range r1 = rs1.ranges[i1];
                Range r2 = rs2.ranges[i2];

                ulong olo = Math.Max(r1.lo, r2.lo);
                ulong ohi = Math.Min(r1.hi, r2.hi);

                // if there is overlap in this section then we emit it
                if (olo < ohi)
                {
                    rOut.AddRange(olo, ohi);
                }

                if (r1.hi < r2.hi)
                {
                    i1++;
                }
                else
                {
                    i2++;
                }
            }

            return rOut;
        }

        public ulong RemoveRange(ulong lo, ulong hi)
        {
            // start with zero and add overlaps to find the net size change
            ulong count = 0;

            if (lo == hi)
            {
                return 0;
            }

            int i = FirstAffectedRange(lo);
            for (; i < ranges.Count; i++)
            {
                Range rMerge = ranges[i];

                // this item is before the range
                if (rMerge.hi < lo)
                {
                    continue;
                }

                // this item is after the range, stop here
                if (rMerge.lo > hi)
                {
                    break;
                }

                // ok there is some overlap, so we have to do something

                ulong olo = Math.Max(lo, rMerge.lo);
                ulong ohi = Math.Min(hi, rMerge.hi);
                ulong overlap = ohi - olo;
                count += overlap;

                if (overlap == rMerge.hi - rMerge.lo)
                {
                    // this region is completely contained in the overlap
                    ranges.RemoveAt(i);
                    i--;
                }
                else if (overlap == (hi - lo) && rMerge.lo != lo && rMerge.hi != hi)
                {
                    // the remove region is completely contained in this range
                    var rNew = new Range();

                    rNew.hi = lo;
                    rNew.lo = rMerge.lo;
                    ranges[i] = rNew;

                    rNew.lo = hi;
                    rNew.hi = rMerge.hi;
                    ranges.Insert(i + 1, rNew);

                    break;
                }
                else if (rMerge.lo < lo)
                {
                    // partial overlap with the first item in a list of overlaps
                    rMerge.hi = lo;
                    ranges[i] = rMerge;
                }
                else
                {
                    // partial overlap with the last item in a list of overlaps
                    rMerge.lo = hi;
                    ranges[i] = rMerge;
                    break;
                }
            }

            return count;
        }

        public Range RangeAt(int i)
        {
            return ranges[i];
        }

        public int RangeCount { get { return ranges.Count; } }

        public ulong Count
        {
            get
            {
                ulong count = 0;
                int c = ranges.Count;
                for (int i = 0; i < c; i++)
                {
                    Range r = ranges[i];
                    count += r.hi - r.lo;
                }

                return count;
            }
        }

        public string CheckValid()
        {
            if (RangeCount == 0)
            {
                return null;
            }

            ulong lasthi = 0;

            for (int i = 0; i < ranges.Count; i++)
            {
                Range r = ranges[i];

                if (r.lo == r.hi)
                {
                    return "range has empty interval";
                }

                if (r.lo > r.hi)
                {
                    return "range has hi/lo out of order";
                }

                if (i > 0 && r.lo <= lasthi)
                {
                    return "range has intervals out of order or overlapping";
                }

                lasthi = r.hi;
            }

            return null;
        }

        private int FirstAffectedRange(ulong lo)
        {
            int c = ranges.Count;
            // there's 0 or 1 in the set, just start at the beginning
            if (c < 2)
            {
                return 0;
            }

            // the first one is already too big, have to start there
            if (ranges[0].hi >= lo)
            {
                return 0;
            }

            // if we're at the end or past it, then skip it all
            if (ranges[c - 1].hi < lo)
            {
                return c - 1;
            }

            int index = 0;
            int delta = c / 2;

            // keep trying to move forward, see if we can do so
            while (delta > 0)
            {
                if (ranges[index + delta].hi < lo)
                {
                    index = index + delta;
                }

                delta /= 2;
            }

            // index can be safely skipped, so we can start at index+1
            return index + 1;
        }
    }
}