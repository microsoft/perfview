using System;
using System.Collections.Generic;
using System.IO;

namespace ETLStackBrowse
{
    public partial class ETLTrace
    {
        // const int fldCSwitchTimeStamp = 1; 
        // const int fldCSwitchNewProcessName = 2;    
        private const int fldCSwitchNewTID = 3;

        // const int fldCSwitchNPri = 4;
        // const int fldCSwitchNQnt = 5;
        // const int fldCSwitchTmSinceLast = 6;
        // const int fldCSwitchWaitTime = 7;
        // const int fldCSwitchOldProcessName = 8;
        private const int fldCSwitchOldTID = 9;

        // const int fldCSwitchOPri = 10;
        // const int fldCSwitchOQnt = 11;    
        // const int fldCSwitchOldState = 12;     
        private const int fldCSwitchWaitReason = 13;

        // const int fldCSwitchSwappable = 14;
        // const int fldCSwitchInSwitchTime = 15;
        private const int fldCSwitchCPU = 16;

        // const int fldCSwitchIdealProc = 17;

        private const int maxReasons = 32;

        public class ThreadStat
        {
            public ThreadStat(int ithread)
            {
                this.ithread = ithread;
            }

            public long time;
            public int switches;
            public int[] swapReasons;
            public UInt32 runmask;
            public int ithread;
        }

        public class ContextSwitchResult
        {
            public ThreadStat[] stats;
            public int countCPU;
            public long timeTotal;
            public long switchesTotal;
            public bool fSimulateHyperthreading;
            public bool fSortBySwitches;
            public bool reasonsComputed;
            public bool[] threadFilters;
            public int nTop;
        }

        private int maxCPU = 64;

        private long tStart;
        private long tEnd;

        public string ComputeContextSwitches()
        {
            ContextSwitchResult result = ComputeContextSwitchesRaw();
            return FormatContextSwitchResult(result);
        }

        private ByteAtomTable atomsReasons = new ByteAtomTable();

        public ContextSwitchResult ComputeContextSwitchesRaw()
        {
            IContextSwitchParameters icswitchparms = itparms.ContextSwitchParameters;

            bool fSimulateHyperthreading = icswitchparms.SimulateHyperthreading;
            bool fSortBySwitches = icswitchparms.SortBySwitches;
            bool fComputeReasons = icswitchparms.ComputeReasons;
            int nTop = icswitchparms.TopThreadCount;
            ByteWindow bT = new ByteWindow();

            long timeTotal = 0;
            int switchesTotal = 0;
            tStart = itparms.T0;
            tEnd = itparms.T1;

            ThreadStat[] stats = NewThreadStats();

            CPUState[] state = new CPUState[maxCPU];

            int idCSwitch = atomsRecords.Lookup("CSwitch");
            bool[] threadFilters = itparms.GetThreadFilters();

            InitializeStartingCPUStates(state, idCSwitch);

            // rewind

            ETWLineReader l = StandardLineReader();
            foreach (ByteWindow b in l.Lines())
            {
                if (l.idType != idCSwitch)
                {
                    continue;
                }

                int oldTid = b.GetInt(fldCSwitchOldTID);

                int cpu = b.GetInt(fldCSwitchCPU);
                timeTotal += AddCSwitchTime(fSimulateHyperthreading, l.t, stats, state);

                int newTid = b.GetInt(fldCSwitchNewTID);

                int idx = FindThreadInfoIndex(l.t, oldTid);

                stats[idx].switches++;
                switchesTotal++;

                if (fComputeReasons)
                {
                    bT.Assign(b, fldCSwitchWaitReason).Trim();
                    int id = atomsReasons.EnsureContains(bT);

                    if (stats[idx].swapReasons == null)
                    {
                        stats[idx].swapReasons = new int[maxReasons];
                    }

                    stats[idx].swapReasons[id]++;
                }

                state[cpu].active = true;
                state[cpu].tid = newTid;
                state[cpu].time = l.t;
            }

            timeTotal += AddCSwitchTime(fSimulateHyperthreading, l.t1, stats, state);

            if (fSortBySwitches)
            {
                Array.Sort(stats,
                    delegate (ThreadStat c1, ThreadStat c2)
                    {
                        if (c1.switches > c2.switches)
                        {
                            return -1;
                        }

                        if (c1.switches < c2.switches)
                        {
                            return 1;
                        }

                        return 0;
                    }
                );
            }
            else
            {
                Array.Sort(stats,
                    delegate (ThreadStat c1, ThreadStat c2)
                    {
                        if (c1.time > c2.time)
                        {
                            return -1;
                        }

                        if (c1.time < c2.time)
                        {
                            return 1;
                        }

                        return 0;
                    }
                );
            }

            var result = new ContextSwitchResult();

            result.stats = stats;
            result.switchesTotal = switchesTotal;
            result.timeTotal = timeTotal;
            result.fSortBySwitches = fSortBySwitches;
            result.reasonsComputed = fComputeReasons;
            result.threadFilters = threadFilters;
            result.countCPU = maxCPU;
            result.nTop = nTop;

            return result;
        }

        private void InitializeStartingCPUStates(CPUState[] state, int idCSwitch)
        {
            int countCPU = 0;

            // find the first thread on each proc

            ETWLineReader l = StandardLineReader();
            l.t1 = Int64.MaxValue - 10000; // keep reading until we find all cpus regardless of how far in the future we have to look
            foreach (ByteWindow b in l.Lines())
            {
                if (l.idType != idCSwitch)
                {
                    continue;
                }

                int cpu = b.GetInt(fldCSwitchCPU);
                if (state[cpu].active)
                {
                    continue;
                }

                state[cpu].active = true;
                state[cpu].tid = b.GetInt(fldCSwitchOldTID);
                state[cpu].time = l.t0;

                countCPU++;
                if (countCPU >= maxCPU)
                {
                    break;
                }
            }
        }

        public string FormatContextSwitchResult(ContextSwitchResult results)
        {
            int nTop = results.nTop;

            string[] reasonNames = new string[atomsReasons.Count];
            for (int i = 0; i < reasonNames.Length; i++)
            {
                reasonNames[i] = atomsReasons.MakeString(i);
            }

            ThreadStat[] stats = results.stats;

            int ithreadIdle = IdleThreadIndex;
            int iStatsIdle = 0;

            // find where the idle thread landed after sorting
            for (int i = 0; i < stats.Length; i++)
            {
                if (stats[i].ithread == ithreadIdle)
                {
                    iStatsIdle = i;
                    break;
                }
            }

            StringWriter sw = new StringWriter();

            sw.WriteLine("Start time: {0:n0}   End time: {1:n0}  Interval Length: {2:n0}", tStart, tEnd, tEnd - tStart);
            sw.WriteLine();

            sw.WriteLine("CPUs: {0:n0}, Total CPU Time: {1:n0} usec. Total Switches: {2:n0} Idle: {3,5:f1}%  Busy: {4,5:f1}%",
                results.countCPU,
                results.timeTotal,
                results.switchesTotal,
                stats[iStatsIdle].time * 100.0 / results.timeTotal,
                (results.timeTotal - stats[iStatsIdle].time) * 100.0 / results.timeTotal);
            sw.WriteLine();

            sw.WriteLine("{0,20} {1,17} {2,35} {3,5} {4,32} {5}", "        Time (usec)", "       Switches", "Process ( PID)", " TID", "Run Mask", "ThreadProc");
            sw.WriteLine("{0,20} {1,17} {2,35} {3,5} {4,32} {5}", "-------------------", "---------------", "--------------", "----", "--------", "----------");

            char[] maskChars = new char[32];

            for (int i = 0; i < Math.Min(threads.Count, nTop); i++)
            {
                int ithread = stats[i].ithread;

                if (stats[i].time == 0)
                {
                    continue;
                }

                if (!results.threadFilters[ithread])
                {
                    continue;
                }

                for (int bit = 0; bit < 32; bit++)
                {
                    maskChars[bit] = ((stats[i].runmask & (1 << bit)) != 0 ? 'X' : '_');
                }

                sw.WriteLine("{0,11:n0} ({1,5:f1}%) {2,8:n0} ({3,5:f1}%) {4,35} {5,5} {6} {7}",
                    stats[i].time,
                    stats[i].time * 100.0 / results.timeTotal,
                    stats[i].switches,
                    stats[i].switches * 100.0 / results.switchesTotal,
                    ByteWindow.MakeString(threads[ithread].processPid),
                    threads[ithread].threadid,
                    new String(maskChars),
                    ByteWindow.MakeString(threads[ithread].threadproc)
                    );

                if (results.reasonsComputed)
                {
                    int[] swapReasons = stats[i].swapReasons;

                    if (swapReasons != null)
                    {
                        for (int k = 0; k < swapReasons.Length; k++)
                        {
                            if (swapReasons[k] > 0)
                            {
                                sw.WriteLine("          {0,17} {1}", reasonNames[k], swapReasons[k]);
                            }
                        }
                    }

                    sw.WriteLine();
                }
            }

            if (results.fSimulateHyperthreading)
            {
                sw.WriteLine();
                sw.WriteLine("Hyperthreading Simulation was used to attribute idle cost more accurately");
            }

            return sw.ToString();
        }

        public ThreadStat[] NewThreadStats()
        {
            ThreadStat[] stats = new ThreadStat[threads.Count];

            for (int ithread = 0; ithread < threads.Count; ithread++)
            {
                stats[ithread] = new ThreadStat(ithread);
            }

            return stats;
        }

        private int AddCSwitchTime(bool fSimulateHyperthreading, long t, ThreadStat[] stats, CPUState[] state)
        {
            int timeTotal = 0;

            for (int cpu = 0; cpu < maxCPU; cpu++)
            {
                if (!state[cpu].active)
                {
                    continue;
                }

                int time = (int)(t - state[cpu].time);

                if (fSimulateHyperthreading)
                {
                    int tfull = time;
                    int th0 = time / 2;
                    int th1 = time - th0;

                    if ((cpu & 1) == 0)
                    {
                        time = th0;
                    }
                    else
                    {
                        time = th1;
                    }

                    if (state[cpu].tid != 0 && state[cpu ^ 1].tid == 0)
                    {
                        time = tfull;
                    }
                    else if (state[cpu].tid == 0 && state[cpu ^ 1].tid != 0)
                    {
                        time = 0;
                    }
                }

                int idx = FindThreadInfoIndex(state[cpu].time, state[cpu].tid);
                stats[idx].time += time;
                state[cpu].time = t;

                int bitStart = (int)((t - time - tStart) * 32 / (tEnd - tStart));
                int bitEnd = (int)((t - tStart) * 32 / (tEnd - tStart));

                if (bitEnd >= 32)
                {
                    bitEnd = 31;
                }

                for (int bit = bitStart; bit <= bitEnd; bit++)
                {
                    stats[idx].runmask |= ((uint)1 << bit);
                }

                timeTotal += time;
            }

            return timeTotal;
        }

        private byte[] GetFilterText()
        {
            return ByteWindow.MakeBytes(itparms.FilterText);
        }

        public ETWLineReader StandardLineReader()
        {
            return new ETWLineReader(this);
        }

        private List<TimeMark> listDelays = null;

        public string ComputeDelays(int delaySize)
        {
            ThreadStat[] stats = NewThreadStats();

            bool[] threadFilters = itparms.GetThreadFilters();
            int idCSwitch = atomsRecords.Lookup("CSwitch");

            StringWriter sw = new StringWriter();
            sw.WriteLine("{0,15} {1,15} {2,15} {3,30} {4,5} {5,-60}", "Delay Start", "Delay End", "Delay Duration", "Process Name ( ID )", "TID", "Threadproc");
            sw.WriteLine("{0,15} {1,15} {2,15} {3,30} {4,5} {5,-60}", "-----------", "---------", "--------------", "-------------------", "---", "----------");

            listDelays = new List<TimeMark>();

            int totalDelays = 0;
            long totalDelay = 0;

            long T0 = itparms.T0;

            for (int i = 0; i < stats.Length; i++)
            {
                stats[i].time = Math.Max(T0, threads[i].timestamp);
            }

            ETWLineReader l = StandardLineReader();
            foreach (ByteWindow b in l.Lines())
            {
                if (l.idType != idCSwitch)
                {
                    continue;
                }

                int oldTid = b.GetInt(fldCSwitchOldTID);
                int idxOld = FindThreadInfoIndex(l.t, oldTid);
                stats[idxOld].time = l.t;

                int newTid = b.GetInt(fldCSwitchNewTID);
                int idxNew = FindThreadInfoIndex(l.t, newTid);

                int waitTime = (int)(l.t - stats[idxNew].time);

                if (waitTime <= 0)
                {
                    continue;
                }

                if (!threadFilters[idxNew])
                {
                    continue;
                }

                totalDelays++;
                totalDelay += waitTime;

                if (waitTime > delaySize)
                {
                    TimeMark tm = new TimeMark();
                    tm.t0 = l.t - waitTime;
                    tm.t1 = l.t;

                    string process = ByteWindow.MakeString(threads[idxNew].processPid);
                    string threadproc = ByteWindow.MakeString(threads[idxNew].threadproc);

                    tm.desc = String.Format("{0,15:n0} {1,15:n0} {2,15:n0} {3,30} {4,5} {5,-60}", tm.t0, tm.t1, waitTime, process, newTid, threadproc);
                    sw.WriteLine(tm.desc);

                    listDelays.Add(tm);
                }
            }

            sw.WriteLine();
            sw.WriteLine("Total Delays: {0:n0}  Total Delay Time {1:n0}", totalDelays, totalDelay);

            return sw.ToString();
        }

        public void ZoomToDelays()
        {
            if (listDelays == null)
            {
                return;
            }

            if (listDelays.Count < 1)
            {
                return;
            }

            ClearZoomedTimes();

            foreach (TimeMark tm in listDelays)
            {
                AddZoomedTimeRow(tm.t0, tm.t1, tm.desc);
            }
        }

        internal void Close()
        {
            stm.Close();
            stm = null;
        }
    }
}

