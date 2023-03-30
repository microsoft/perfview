using System;
using System.Collections.Generic;
using System.IO;

namespace ETLStackBrowse
{
    public partial class ETLTrace
    {
        private ITraceParameters itparms;
        private ITraceParameters initial_parms;
        private ITraceUINotify itnotify;
        private string filename;
        private string traceinfo;
        private List<ThreadInfo> threads = new List<ThreadInfo>();
        private List<ProcessInfo> processes = new List<ProcessInfo>();
        private ByteAtomTable atomsProcesses = null;
        private ByteAtomTable atomsFields = null;
        private ByteAtomTable atomsRecords = null;
        private List<List<int>> listEventFields = null;
        private int[] sortedThreads = null;
        private int[] sortedProcesses = null;
        private RecordInfo[] recordInfo = null;
        private long tmax = 0;

        public int[] SortedThreads { get { return sortedThreads; } }
        public int[] SortedProcesses { get { return sortedProcesses; } }
        public ByteAtomTable ProcessAtoms { get { return atomsProcesses; } }
        public ByteAtomTable FieldAtoms { get { return atomsFields; } }
        public ByteAtomTable RecordAtoms { get { return atomsRecords; } }
        public List<ThreadInfo> Threads { get { return threads; } }
        public List<ProcessInfo> Processes { get { return processes; } }
        public List<List<int>> EventFields { get { return listEventFields; } }
        public string Info { get { return traceinfo; } }
        public string FileName { get { return filename; } }
        public ITraceParameters Parameters { get { return itparms; } set { itparms = value; } }
        public long TMax { get { return tmax; } }
        public bool[] StackTypes { get { return stackTypes; } }
        public RecordInfo[] CommonFieldIds { get { return recordInfo; } }
        public int MaxCPU { get { return maxCPU; } }
        public bool[] StackIgnoreEvents { get { return stackIgnoreEvents; } }

        public int IdleThreadIndex { get { return FindThreadInfoIndex(0, 0); } }

        private string[] bars = { "     ", "*    ", "**   ", "***  ", "**** ", "*****" };
        private bool[] stackIgnoreEvents = null;
        private string[] stackIgnoreEventStrings = { "DPC", "DPCTmr", "Interrupt", "FileNameRundown" };
        private BigStream stm = null;
        private List<long> offsets = new List<long>();
        private Dictionary<int, int> mp_tid_firstindex = null;
        private bool[] stackTypes = null;
        private const int fldTStartProcess = 2;
        private const int fldTStartThreadProc = 10;
        private const int fldTStartThreadId = 3;
        private const int typeLen = 23;  // standard record length, the type field is 23 characters

        [Serializable]
        public class ProcessInfo
        {
            public byte[] processPid;

            public string ProcessName
            {
                get
                {
                    if (processPid == null || processPid.Length == 0)
                    {
                        return "";
                    }

                    return ByteWindow.MakeString(processPid);
                }
            }
        }

        [Serializable]
        public struct RecordInfo
        {
            public int threadField;
            public int sizeField;
            public int elapsedTimeField;
            public int goodNameField;
            public int count;
        }

        public ITraceParameters NewParameters()
        {
            return new TraceParameters(this);
        }

        public ITraceParameters UIParameters
        {
            get { return initial_parms; }
        }

        [Serializable]
        public class ThreadInfo : IComparable<ThreadInfo>
        {
            public long timestamp;
            public int threadid;
            public byte[] processPid;
            public byte[] processNopid;
            public byte[] threadproc;

            int IComparable<ThreadInfo>.CompareTo(ThreadInfo other)
            {
                if (threadid < other.threadid)
                {
                    return -1;
                }

                if (threadid > other.threadid)
                {
                    return 1;
                }

                if (timestamp < other.timestamp)
                {
                    return -1;
                }

                if (timestamp > other.timestamp)
                {
                    return 1;
                }

                return 0;
            }

            public string ProcessName
            {
                get
                {
                    if (processPid == null || processPid.Length == 0)
                    {
                        return "";
                    }

                    return ByteWindow.MakeString(processPid);
                }
            }

            public string ProcessNameNoPid
            {
                get
                {
                    if (processNopid == null || processNopid.Length == 0)
                    {
                        return "";
                    }

                    return ByteWindow.MakeString(processNopid);
                }
            }

            public string ThreadProc
            {
                get
                {
                    if (threadproc == null || threadproc.Length == 0)
                    {
                        return "";
                    }

                    return ByteWindow.MakeString(threadproc);
                }
            }
        }

        private struct CPUState
        {
            public long time;
            public int tid;
            public bool active;
            public int usage;
        }

        private struct PreviousEvent
        {
            public int filenameId;
            public long time;
            public int eventId;
            public int tid;
            public ulong weight;
        }

        public class ETWLineReader
        {
            private ETLTrace trace;
            private BigStream stm;
            private ByteWindow b;
            private ByteWindow bRecord;
            private ByteWindow bAll;

            public long t0;
            public long t1;
            public long t;
            public int idType;

            private int idThreadId;
            private int idNewTID;
            private int idOldTID;
            private int idProcessPid;
            private int idBaseAddr;
            private int idEndAddr;
            private int idVirtualAddr;

            private bool[] threadFilters;
            private bool[] processFilters;
            private byte[] byFilterText;

            private List<ulong> listMemRanges;

            public ETWLineReader(ETLTrace trace)
            {
                this.trace = trace;
                t0 = trace.itparms.T0;
                t1 = trace.itparms.T1;
                int slot = (int)(t0 / 100000);
                long offset = trace.offsets[slot];

                stm = trace.stm;
                stm.Position = offset;

                idThreadId = trace.atomsFields.Lookup("ThreadID");
                idNewTID = trace.atomsFields.Lookup("New TID");
                idOldTID = trace.atomsFields.Lookup("Old TID");
                idBaseAddr = trace.atomsFields.Lookup("BaseAddr");
                idEndAddr = trace.atomsFields.Lookup("EndAddr");
                idVirtualAddr = trace.atomsFields.Lookup("VirtualAddr");
                idProcessPid = trace.atomsFields.Lookup("Process Name ( PID)");
                threadFilters = trace.itparms.GetThreadFilters();
                processFilters = trace.itparms.GetProcessFilters();
                byFilterText = new byte[0]; // trace.itparms.FilterText;

                idType = -1;

                b = new ByteWindow();
                bRecord = new ByteWindow();
                bAll = new ByteWindow();

                ComputeMemoryRanges();
            }

            public void ComputeMemoryRanges()
            {
                listMemRanges = new List<ulong>();

                var str = trace.itparms.MemoryFilters;

                if (str == null)
                {
                    return;
                }

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

                    ulong start = ParseHex(line, 0);
                    ulong end = ParseHex(line, ich);

                    if (start >= end)
                    {
                        continue;
                    }

                    // we always add pairs
                    listMemRanges.Add(start);
                    listMemRanges.Add(end);
                }
            }

            public IEnumerable<ByteWindow> Lines()
            {
                while (stm.ReadLine(b))
                {
                    if (b.len < typeLen)
                    {
                        continue;
                    }

                    // strip the newline but leave the CR so there is some kind of delimeter left
                    // this assists in parsing (a non-numeric trail character is always at the end)
                    b.len--;

                    t = b.GetLong(1);
                    if (t < t0)
                    {
                        continue;
                    }

                    bAll.Assign(b);

                    bRecord.Assign(b, 0).Trim();

                    idType = trace.atomsRecords.Lookup(bRecord);

                    // unknown record
                    if (idType < 0)
                    {
                        continue;
                    }

                    // ignoreable record
                    if (trace.recordInfo[idType].count <= 0)
                    {
                        continue;
                    }

                    // wait to get a little past the desired area
                    // because lines are sometimes slightly out of order
                    // 1ms cushion is all we do
                    if (t > t1 + 1000)
                    {
                        break;
                    }

                    // don't use any line that is beyond the desired region
                    if (t > t1)
                    {
                        continue;
                    }

                    yield return b;
                }
            }

            public bool MatchingRecordType(bool[] filters)
            {
                if (filters[idType])
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            public bool MatchingTextFilter()
            {
                return bAll.Contains(byFilterText);
            }

            public bool MatchingMemory()
            {
                if (listMemRanges.Count == 0)
                {
                    return true;
                }

                List<int> fieldList = trace.listEventFields[idType];
                int fld = -1;

                fld = fieldList.IndexOf(idVirtualAddr);
                if (fld >= 0)
                {
                    long addr = b.GetLong(fld);
                    return CheckMemRange(addr, addr + 1);
                }

                int fStart = fieldList.IndexOf(idBaseAddr);
                if (fStart >= 0)
                {
                    int fEnd = fieldList.IndexOf(idEndAddr);
                    if (fEnd >= 0)
                    {
                        long start = b.GetLong(fStart);
                        long end = b.GetLong(fEnd);
                        return CheckMemRange(start, end);
                    }
                }

                return false;
            }

            private bool CheckMemRange(long _start, long _end)
            {
                ulong start = (ulong)_start;
                ulong end = (ulong)_end;
                for (int i = 0; i < listMemRanges.Count; i += 2)
                {
                    ulong membase = listMemRanges[i];
                    ulong memend = listMemRanges[i + 1];

                    // if the memory range for the record overlaps with the indicated range, then we keep it
                    if (start < memend && end > membase)
                    {
                        return true;
                    }
                }

                return false;
            }

            public bool MatchingThread()
            {
                List<int> fieldList = trace.listEventFields[idType];

                int fld = -1;

                fld = fieldList.IndexOf(idThreadId);
                if (fld < 0)
                {
                    fld = fieldList.IndexOf(idNewTID);
                }

                if (fld < 0)
                {
                    fld = fieldList.IndexOf(idOldTID);
                }

                if (fld < 0)
                {
                    return false;
                }

                int tid = b.GetInt(fld);
                int idx = trace.FindThreadInfoIndex(t, tid);
                return threadFilters[idx];
            }

            public bool MatchingProcess()
            {
                List<int> fieldList = trace.listEventFields[idType];

                int fld = -1;

                fld = fieldList.IndexOf(idProcessPid);
                if (fld < 0)
                {
                    return false;
                }

                bRecord.Assign(b, fld).Trim();
                int idx = trace.atomsProcesses.Lookup(bRecord);

                if (idx == -1)
                {
                    return false;
                }

                return processFilters[idx];
            }
        }

        public static ulong ParseHex(string str, int index)
        {
            while (index < str.Length)
            {
                if (str[index] != ' ' && str[index] != '\t')
                {
                    break;
                }

                index++;
            }

            if (index + 2 < str.Length && str[index] == '0' && str[index + 1] == 'x')
            {
                index += 2;
            }

            ulong val = 0;

            while (index < str.Length)
            {
                char ch = str[index++];
                if (ch >= '0' && ch <= '9')
                {
                    val = val * 16 + ch - '0';
                }
                else if (ch >= 'a' && ch <= 'f')
                {
                    val = val * 16 + 10 + ch - 'a';
                }
                else if (ch >= 'A' && ch <= 'F')
                {
                    val = val * 16 + 10 + ch - 'A';
                }
                else
                {
                    break;
                }
            }

            return val;
        }

        public const string cacheSuffix = ".cache.v9A";

        public ETLTrace(ITraceParameters itparms, ITraceUINotify itnotify, string filename)
        {
            this.filename = filename;
            initial_parms = itparms; // the original parameters
            this.itparms = itparms;
            this.itnotify = itnotify;

            stm = new BigStream(filename);

            InitializeFromPrimary();

            for (int i = 0; i < atomsRecords.Count; i++)
            {
                if (recordInfo[i].count > 0)
                {
                    itnotify.AddEventToEventList(atomsRecords.MakeString(i));
                }
            }

            for (int i = 0; i < sortedThreads.Length; i++)
            {
                ThreadInfo ti = threads[sortedThreads[i]];
                itnotify.AddThreadToThreadList(String.Format("{0,20} {1,5} {2}", ByteWindow.MakeString(ti.processPid), ti.threadid, ByteWindow.MakeString(ti.threadproc)));
            }

            for (int i = 0; i < processes.Count; i++)
            {
                ProcessInfo pi = processes[sortedProcesses[i]];
                itnotify.AddProcessToProcessList(ByteWindow.MakeString(pi.processPid));
            }

            for (int i = 0; i < stackTypes.Length; i++)
            {
                if (stackTypes[i])
                {
                    itnotify.AddEventToStackEventList(atomsRecords.MakeString(i));
                }
            }
        }

        private int idRecordEscape;
        private int idTimeEscape;
        private int idTimeOffsetEscape;
        private int idWhenEscape;
        private int idFirstEscape;
        private int idLastEscape;

        private void InitializeFromPrimary()
        {
            atomsFields = new ByteAtomTable();
            atomsRecords = new ByteAtomTable();
            atomsProcesses = new ByteAtomTable();
            listEventFields = new List<List<int>>();

            byte[] byRecordEscape = ByteWindow.MakeBytes("$R");
            idRecordEscape = atomsFields.EnsureContains(byRecordEscape);

            byte[] byTimeEscape = ByteWindow.MakeBytes("$T");
            idTimeEscape = atomsFields.EnsureContains(byTimeEscape);

            byte[] byTimeOffsetEscape = ByteWindow.MakeBytes("$TimeOffset");
            idTimeOffsetEscape = atomsFields.EnsureContains(byTimeOffsetEscape);

            byte[] byWhenEscape = ByteWindow.MakeBytes("$When");
            idWhenEscape = atomsFields.EnsureContains(byWhenEscape);

            byte[] byFirstEscape = ByteWindow.MakeBytes("$First");
            idFirstEscape = atomsFields.EnsureContains(byFirstEscape);

            byte[] byLastEscape = ByteWindow.MakeBytes("$Last");
            idLastEscape = atomsFields.EnsureContains(byLastEscape);

            byte[] byBeginHeader = ByteWindow.MakeBytes("BeginHeader");
            byte[] byEndHeader = ByteWindow.MakeBytes("EndHeader");

            ByteWindow b = new ByteWindow();

            threads.Clear();
            offsets.Clear();

            while (stm.ReadLine(b))
            {
                if (b.StartsWith(byBeginHeader))
                {
                    break;
                }
            }

            while (stm.ReadLine(b))
            {
                if (b.StartsWith(byEndHeader))
                {
                    break;
                }

                b.Field(0).Trim();

                int iCountBefore = atomsRecords.Count;
                atomsRecords.EnsureContains(b);
                if (atomsRecords.Count == iCountBefore)
                {
                    continue;
                }

                List<int> listFields = new List<int>();
                listEventFields.Add(listFields);

                listFields.Add(0); // the ID for $R, the record escape field

                // start from field 1 -- that skips only field 0 which is already mapped to $R
                for (int i = 1; i < b.fieldsLen; i++)
                {
                    b.Field(i).Trim();
                    int id = atomsFields.EnsureContains(b);
                    listFields.Add(id);
                }
            }

            stackIgnoreEvents = new bool[atomsRecords.Count];

            foreach (string strEvent in stackIgnoreEventStrings)
            {
                int id = atomsRecords.Lookup(strEvent);
                if (id >= 0)
                {
                    stackIgnoreEvents[id] = true;
                }
            }

            recordInfo = new RecordInfo[atomsRecords.Count];

            int idThreadField = atomsFields.Lookup("ThreadID");
            int idFileNameField = atomsFields.Lookup("FileName");
            int idTypeField = atomsFields.Lookup("Type");
            int idSizeField = atomsFields.Lookup("Size");
            int idIOSizeField = atomsFields.Lookup("IOSize");
            int idElapsedTimeField = atomsFields.Lookup("ElapsedTime");

            for (int i = 0; i < recordInfo.Length; i++)
            {
                List<int> fieldList = listEventFields[i];
                recordInfo[i].threadField = fieldList.IndexOf(idThreadField);
                recordInfo[i].sizeField = fieldList.IndexOf(idSizeField);

                if (-1 == recordInfo[i].sizeField)
                {
                    recordInfo[i].sizeField = fieldList.IndexOf(idIOSizeField);
                }

                recordInfo[i].goodNameField = fieldList.IndexOf(idFileNameField);
                if (-1 == recordInfo[i].goodNameField)
                {
                    recordInfo[i].goodNameField = fieldList.IndexOf(idTypeField);
                }

                recordInfo[i].elapsedTimeField = fieldList.IndexOf(idElapsedTimeField);
            }

            int idT_DCEnd = atomsRecords.Lookup("T-DCEnd");
            int idT_DCStart = atomsRecords.Lookup("T-DCStart");
            int idT_Start = atomsRecords.Lookup("T-Start");
            int idT_End = atomsRecords.Lookup("T-End");
            int idCSwitch = atomsRecords.Lookup("CSwitch");
            int idStack = atomsRecords.Lookup("Stack");
            int idAlloc = atomsRecords.Lookup("Allocation");

            int idFirstReliableEventTimeStamp = atomsRecords.Lookup("FirstReliableEventTimeStamp");
            int idFirstReliableCSwitchEventTimeStamp = atomsRecords.Lookup("FirstReliableCSwitchEventTimeStamp");

            long tNext = 100000;
            long t = 0;

            // seed offsets for the 0th record
            offsets.Add(stm.Position);

            listInitialTime = new List<TimeMark>();

            maxCPU = 64;
            CPUState[] state = new CPUState[maxCPU];

            const int maxEvent = 16;
            PreviousEvent[] prev = new PreviousEvent[maxEvent];
            int iNextEvent = 0;

            stackTypes = new bool[atomsRecords.Count];

            Dictionary<int, int> dictStartedThreads = new Dictionary<int, int>();

            ByteWindow record = new ByteWindow();
            ByteWindow bThreadProc = new ByteWindow();
            ByteWindow bProcess = new ByteWindow();

            // the first line of the trace is the trace info, that, importantly, has the OSVersion tag
            if (stm.ReadLine(b))
            {
                traceinfo = b.GetString();
            }
            else
            {
                traceinfo = "None";
            }

            while (stm.ReadLine(b))
            {
                if (b.len < typeLen)
                {
                    continue;
                }

                record.Assign(b, 0).Trim();
                int idrec = atomsRecords.Lookup(record);

                if (idrec < 0)
                {
                    continue;
                }

                if (idrec == idFirstReliableCSwitchEventTimeStamp ||
                    idrec == idFirstReliableEventTimeStamp)
                {
                    continue;
                }

                recordInfo[idrec].count++;

                t = b.GetLong(1);
                while (t >= tNext)
                {
                    tNext = AddTimeRow(tNext, state);
                }

                if (idrec != idStack && !stackIgnoreEvents[idrec])
                {
                    prev[iNextEvent].time = t;
                    prev[iNextEvent].eventId = idrec;
                    iNextEvent = (iNextEvent + 1) % maxEvent;
                }

                if (idrec == idStack)
                {
                    int i = iNextEvent;
                    for (; ; )
                    {
                        if (--i < 0)
                        {
                            i = maxEvent - 1;
                        }

                        if (i == iNextEvent)
                        {
                            break;
                        }

                        if (prev[i].time == t)
                        {
                            stackTypes[prev[i].eventId] = true;
                            break;
                        }
                    }
                }
                else if (idrec == idT_Start ||
                    idrec == idT_End ||
                    idrec == idT_DCStart ||
                    idrec == idT_DCEnd)
                {
                    ThreadInfo ti = new ThreadInfo();

                    ti.threadid = b.GetInt(fldTStartThreadId);

                    bool fStarted = dictStartedThreads.ContainsKey(ti.threadid);

                    if (idrec == idT_Start)
                    {
                        ti.timestamp = t;
                    }
                    else if (idrec == idT_DCStart)
                    {
                        ti.timestamp = 0;
                    }
                    else
                    {
                        if (fStarted)
                        {
                            continue;
                        }

                        ti.timestamp = 0;
                    }

                    if (!fStarted)
                    {
                        dictStartedThreads.Add(ti.threadid, 0);
                    }

                    bThreadProc.Assign(b, fldTStartThreadProc).Trim();
                    bProcess.Assign(b, fldTStartProcess).Trim();

                    ti.processPid = bProcess.Clone();

                    if (atomsProcesses.Lookup(bProcess) == -1)
                    {
                        atomsProcesses.EnsureContains(ti.processPid);
                        ProcessInfo pi = new ProcessInfo();
                        pi.processPid = ti.processPid;
                        processes.Add(pi);
                    }

                    bProcess.Truncate((byte)'(').Trim();

                    ti.processNopid = bProcess.Clone();
                    ti.threadproc = bThreadProc.Clone();

                    threads.Add(ti);
                }
                else if (idrec == idCSwitch)
                {
                    int newTid = b.GetInt(fldCSwitchNewTID);
                    int oldTid = b.GetInt(fldCSwitchOldTID);
                    int cpu = b.GetInt(fldCSwitchCPU);

                    if (cpu < 0 || cpu > state.Length)
                    {
                        continue;
                    }

                    int tusage = (int)(t - state[cpu].time);

                    if (state[cpu].tid != 0 && tusage > 0)
                    {
                        state[cpu].usage += tusage;
                    }

                    state[cpu].time = t;
                    state[cpu].tid = newTid;
                    state[cpu].active = true;
                }
            }

            AddTimeRow(t, state);

            threads.Sort();
            int tid = -1;

            mp_tid_firstindex = new Dictionary<int, int>();

            for (int i = 0; i < threads.Count; i++)
            {
                if (tid != threads[i].threadid)
                {
                    tid = threads[i].threadid;
                    mp_tid_firstindex.Add(tid, i);
                }
            }

            sortedThreads = new int[threads.Count];
            for (int i = 0; i < sortedThreads.Length; i++)
            {
                sortedThreads[i] = i;
            }

            Array.Sort(sortedThreads,
                delegate (int id1, int id2)
                {
                    byte[] b1 = threads[id1].processPid;
                    byte[] b2 = threads[id2].processPid;

                    int cmp = ByteWindow.CompareBytes(b1, b2, true);
                    if (cmp != 0)
                    {
                        return cmp;
                    }

                    if (threads[id1].threadid < threads[id2].threadid)
                    {
                        return -1;
                    }

                    if (threads[id1].threadid > threads[id2].threadid)
                    {
                        return 1;
                    }

                    return 0;
                }
            );


            sortedProcesses = new int[processes.Count];
            for (int i = 0; i < sortedProcesses.Length; i++)
            {
                sortedProcesses[i] = i;
            }

            Array.Sort(sortedProcesses,
                delegate (int id1, int id2)
                {
                    byte[] b1 = processes[id1].processPid;
                    byte[] b2 = processes[id2].processPid;

                    return ByteWindow.CompareBytes(b1, b2, true);
                }
            );

            tmax = t;
        }

        private long AddTimeRow(long tNext, CPUState[] state)
        {
            TimeMark tm = new TimeMark();
            tm.t0 = tNext - 100000;
            tm.t1 = tNext;

            string line = ComputeTimeRow(tNext - 100000, tNext, state);
            tm.desc = line;

            itnotify.AddTimeToTimeList(line);
            tNext += 100000;
            offsets.Add(stm.Position);

            listInitialTime.Add(tm);
            return tNext;
        }

        public ThreadInfo FindThreadInfo(long t, int tid)
        {
            int iBest = FindThreadInfoIndex(t, tid);

            if (iBest == -1)
            {
                ThreadInfo ti = new ThreadInfo();
                ti.threadproc = ByteWindow.MakeBytes("Thread: " + tid.ToString());
                ti.processNopid = ByteWindow.MakeBytes("Unknown");
                ti.processPid = ti.processNopid;

                return ti;
            }

            return threads[iBest];
        }

        public int FindThreadInfoIndex(long t, int tid)
        {
            // sometimes there are events in the stream that happen that use
            // the thread id before the thread has actually be created in the
            // event stream -- if we can't find a thread id that comes before
            // the event we use the first one after the event, the closest one.

            int i = 0;
            mp_tid_firstindex.TryGetValue(tid, out i);
            int iBest = i;

            for (; i < threads.Count; i++)
            {
                if (threads[i].timestamp > t)
                {
                    break;
                }

                if (threads[i].threadid != tid)
                {
                    break;
                }

                iBest = i;
            }

            return iBest;
        }

        private void AddZoomedTimeRow(long timeStart, long timeEnd, CPUState[] state)
        {
            string row = ComputeTimeRow(timeStart, timeEnd, state);
            AddZoomedTimeRow(timeStart, timeEnd, row);
        }

        private void AddZoomedTimeRow(long timeStart, long timeEnd, string row)
        {
            itnotify.AddTimeToZoomedTimeList(row);

            TimeMark tm = new TimeMark();
            tm.t0 = timeStart;
            tm.t1 = timeEnd;
            tm.desc = row;

            listMark.Add(tm);
        }

        private string ComputeTimeRow(long timeStart, long timeEnd, CPUState[] state)
        {
            long usage = 0;
            int cpu;

            for (cpu = 0; cpu < state.Length; cpu++)
            {
                if (!state[cpu].active)
                {
                    break;
                }

                if (state[cpu].tid != 0)
                {
                    long cpuTime = timeEnd - state[cpu].time;
                    if (cpuTime > 0)
                    {
                        usage += cpuTime;
                    }
                }

                if (state[cpu].usage > 0)
                {
                    usage += state[cpu].usage;
                }

                state[cpu].usage = 0;
                state[cpu].time = timeEnd;
                maxCPU = cpu + 1;
            }

            long maxusage = cpu * (timeEnd - timeStart);
            double pct;

            if (usage > maxusage)
            {
                usage = maxusage;
            }

            if (maxusage == 0)
            {
                pct = 0.0;
            }
            else
            {
                pct = (usage * 100.0 / maxusage);
            }

            int ibar = (int)Math.Floor(pct / 100 * 6);
            if (ibar >= bars.Length)
            {
                ibar = bars.Length - 1;
            }

            return String.Format("{0,12:n0} {1,5:f1}% {2}", timeStart, pct, bars[ibar]);
        }

        private void ClearZoomedTimes()
        {
            itnotify.ClearZoomedTimes();
            listMark = new List<TimeMark>(50);
        }

        [Serializable]
        public struct TimeMark
        {
            public long t0;
            public long t1;
            public string desc;
        }

        private List<TimeMark> listMark;
        private List<TimeMark> listInitialTime;

        public void ZoomTimeWindow()
        {
            ClearZoomedTimes();

            long zoomed_t0 = itparms.T0;
            long zoomed_t1 = itparms.T1;
            const int zoomed_splits = 50;

            int idCSwitch = atomsRecords.Lookup("CSwitch");

            int i = 1;

            long timeStart = zoomed_t0;
            long timeEnd = zoomed_t0 + (zoomed_t1 - zoomed_t0) * i / zoomed_splits;

            CPUState[] state = new CPUState[maxCPU];

            InitializeStartingCPUStates(state, idCSwitch);

            ETWLineReader l = StandardLineReader();
            foreach (ByteWindow b in l.Lines())
            {
                while (l.t >= timeEnd && i <= zoomed_splits)
                {
                    AddZoomedTimeRow(timeStart, timeEnd, state);
                    i++;
                    timeStart = timeEnd;
                    timeEnd = zoomed_t0 + (zoomed_t1 - zoomed_t0) * i / zoomed_splits;
                }

                if (l.idType != idCSwitch)
                {
                    continue;
                }

                int newTid = b.GetInt(fldCSwitchNewTID);
                int oldTid = b.GetInt(fldCSwitchOldTID);
                int cpu = b.GetInt(fldCSwitchCPU);

                int tusage = (int)(l.t - state[cpu].time);

                if (state[cpu].tid != 0)
                {
                    state[cpu].usage += tusage;
                }

                state[cpu].time = l.t;
                state[cpu].tid = newTid;
                state[cpu].active = true;
            }

            while (i <= zoomed_splits)
            {
                AddZoomedTimeRow(timeStart, timeEnd, state);
                i++;
                timeStart = timeEnd;
                timeEnd = zoomed_t0 + (zoomed_t1 - zoomed_t0) * i / zoomed_splits;
            }
        }

        public void ComputeFieldsForRecords()
        {
            bool[] events = itparms.EventFilters.GetFilters();
            bool[] fields = new bool[atomsFields.Count];

            itnotify.ClearEventFields();

            for (int eventid = 0; eventid < events.Length; eventid++)
            {
                if (!events[eventid])
                {
                    continue;
                }

                foreach (int fid in listEventFields[eventid])
                {
                    fields[fid] = true;
                }
            }

            fields[idRecordEscape] = true; // always add $R
            fields[idTimeEscape] = true; // always add $T
            fields[idTimeOffsetEscape] = true; // always add $TimeOffset
            fields[idWhenEscape] = true; // always add $When
            fields[idFirstEscape] = true; // always add $When
            fields[idLastEscape] = true; // always add $When

            for (int fid = 0; fid < fields.Length; fid++)
            {
                if (fields[fid])
                {
                    itnotify.AddEventField(atomsFields.MakeString(fid));
                }
            }
        }

        public string ComputeMatches()
        {
            bool fFilterThreads = itparms.EnableThreadFilter;
            bool fFilterProcesses = itparms.EnableProcessFilter;

            bool[] filters = itparms.EventFilters.GetFilters();

            if (filters == null)
            {
                return "No filter specified";
            }

            int countLimit = 10000;
            int count = 0;

            StringWriter sw = new StringWriter();

            ETWLineReader l = StandardLineReader();
            foreach (ByteWindow b in l.Lines())
            {
                if (!l.MatchingRecordType(filters))
                {
                    continue;
                }

                if (fFilterThreads && !l.MatchingThread())
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

                sw.WriteLine(b.GetString());

                count++;
                if (count >= countLimit)
                {
                    sw.WriteLine("Output truncated at {0} lines", countLimit);
                    break;
                }
            }

            return sw.ToString();
        }

        public string ComputeSchema()
        {
            bool[] filters = itparms.EventFilters.GetFilters();

            StringWriter sw = new StringWriter();
            sw.WriteLine();

            for (int recordid = 0; recordid < filters.Length; recordid++)
            {
                if (!filters[recordid])
                {
                    continue;
                }

                sw.Write("{0}", atomsRecords.MakeString(recordid));

                List<int> listFields = listEventFields[recordid];

                bool fFirst = true;

                foreach (int fid in listFields)
                {
                    if (fFirst) // suppress internal $T field
                    {
                        fFirst = false;
                        continue;
                    }
                    sw.Write(", {0}", atomsFields.MakeString(fid));
                }

                sw.WriteLine();
            }

            return sw.ToString();
        }

        public const int standardPlotRows = 25;
        public const int standardPlotColumns = 50;
#if false 
        public string ComputeDistribution()
        {
            IRollupParameters rollupParameters = itparms.RollupParameters;

            string savedCommand = rollupParameters.RollupCommand;
            int savedIntervals = rollupParameters.RollupTimeIntervals;

            rollupParameters.RollupTimeIntervals = standardPlotColumns;
            rollupParameters.RollupCommand = ">/$R */$T";

            RollupResults r = ComputeRollupRaw();

            rollupParameters.RollupTimeIntervals = savedIntervals;
            rollupParameters.RollupCommand = savedCommand;

            return PlotRollup(standardPlotRows, standardPlotColumns, r);
        }

        public string PlotRollup(int rows, int cols, RollupResults r)
        {
            if (r.rollupRoot == null || r.rollupRoot.children == null || r.rollupRoot.children.Count == 0)
                return "No Results";

            StringWriter sw = new StringWriter();

            sw.WriteLine("Time: {0:n0} to {1:n0} ({2:n0} usec)", itparms.T0, itparms.T1, itparms.T1 - itparms.T0);
            sw.WriteLine("Total Matching Events: {0:n0}", r.rollupRoot.count);

            string yLabel = "Event Number";

            long[] counts = new long[cols]; 

            foreach (RollupNode child in r.rollupRoot.children)
            {
                sw.WriteLine();
                sw.WriteLine();

                string xLabel = String.Format("{0} Event Counts", r.atoms.MakeString(child.id));

                for (int i = 0; i < counts.Length; i++)
                    counts[i] = 0;

                for (int i = 0; i < child.children.Count && i < cols; i++)
                    counts[i] = child.children[i].count;

                sw.WriteLine(FormatOnePlot(rows, cols, xLabel, yLabel, counts));
            }

            return sw.ToString();
        }
#endif

        public static string FormatOnePlot(int rows, int cols, string xLabel, string yLabel, long[] counts)
        {
            StringWriter sw = new StringWriter();

            char[][] matrix = new char[rows][];

            for (int i = 0; i < rows; i++)
            {
                matrix[i] = new char[cols];
                for (int j = 0; j < cols; j++)
                {
                    matrix[i][j] = ' ';
                }
            }

            long min = 0;
            long max = 0;
            long total = 0;
            for (int i = 0; i < counts.Length; i++)
            {
                total += counts[i];
                if (total < min)
                {
                    min = total;
                }

                if (total > max)
                {
                    max = total;
                }
            }

            // recalibrate for the new zero point if the zero point is negative
            long current = -min;
            total = (max - min);

            if (min < 0)
            {
                int row = (int)((current + 1) * rows / total);
                for (int i = 0; i < cols; i += 4)
                {
                    matrix[row][i] = '-';
                }
            }

            for (int i = 0; i < counts.Length; i++)
            {
                long c = counts[i];
                if (c == 0)
                {
                    continue;
                }

                int row0 = (int)((current + 1) * rows / total);
                current += c;
                int row1 = (int)((current) * rows / total);

                if (row0 >= rows)
                {
                    row0 = rows - 1;
                }

                if (row1 >= rows)
                {
                    row1 = rows - 1;
                }

                int col = i;
                if (col >= cols)
                {
                    col = cols - 1;
                }

                if (c < row1 - row0)
                {
                    for (int j = 1; j <= c; j++)
                    {
                        int row = row0 + (int)((ulong)(row1 - row0) * (ulong)j / (ulong)c);
                        matrix[row][col] = '*';
                    }
                }
                else
                {
                    for (int row = row0; row <= row1; row++)
                    {
                        matrix[row][col] = '*';
                    }
                }
            }

            for (int i = rows; --i >= 0;)
            {
                int ich = rows - i - 1;
                char ch = ' ';
                if (ich < yLabel.Length)
                {
                    ch = yLabel[ich];
                }

                sw.WriteLine("{0}|{1}", ch, new String(matrix[i]));
            }

            for (int i = cols; --i >= 0;)
            {
                matrix[0][i] = '-';
            }

            sw.WriteLine(" \\{0}", new String(matrix[0]));
            sw.WriteLine(" {0} {1:n0} to {2:n0}", xLabel, min, max);

            return sw.ToString();
        }

        public void TimeSelect(int ilo, int ihi)
        {
            ihi++;

            long t0 = 100000L * ilo;
            long t1 = 100000L * ihi;

            itparms.T0 = t0;
            itparms.T1 = t1;
        }

        public void ZoomedTimeSelect(int ilo, int ihi)
        {
            if (ilo < 0 || ihi < 0)
            {
                return;
            }

            if (ilo >= listMark.Count || ihi >= listMark.Count)
            {
                return;
            }

            itparms.T0 = listMark[ilo].t0;
            itparms.T1 = listMark[ihi].t1;
        }
    }
}