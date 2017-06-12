using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing.Stacks;
using PerfView.Utilities;



namespace Diagnostics.Tracing.StackSources
{
    public class ParallelLinuxPerfScriptStackSource : LinuxPerfScriptStackSource
    {
        public ParallelLinuxPerfScriptStackSource(string path, bool doThreadTime = false) : base(path, doThreadTime)
        {
        }

        protected override void DoInterning()
        {
            int threadCount = MaxThreadCount;

            this.frames = new ConcurrentDictionary<string, StackSourceFrameIndex>();

            this.parser.SkipPreamble(masterSource);

            Task[] tasks = new Task[threadCount];

            List<BlockedTimeAnalyzer>[] threadBlockedTimeAnalyzers = null;
            if (this.doThreadTime)
            {
                threadBlockedTimeAnalyzers = new List<BlockedTimeAnalyzer>[tasks.Length];
            }

            List<StackSourceSample>[] threadSamples = new List<StackSourceSample>[tasks.Length];

            for (int i = 0; i < tasks.Length; i++)
            {
                threadSamples[i] = new List<StackSourceSample>();

                if (threadBlockedTimeAnalyzers != null)
                {
                    threadBlockedTimeAnalyzers[i] = new List<BlockedTimeAnalyzer>();
                }

                tasks[i] = new Task((object givenArrayIndex) =>
                {
                    FastStream bufferPart;
                    while ((bufferPart = this.GetNextSubStream(masterSource)) != null)
                    {
                        BlockedTimeAnalyzer blockedTimeAnalyzer = null;
                        if (threadBlockedTimeAnalyzers != null)
                        {
                            blockedTimeAnalyzer = new BlockedTimeAnalyzer();
                            threadBlockedTimeAnalyzers[(int)givenArrayIndex].Add(blockedTimeAnalyzer);
                        }

                        foreach (LinuxEvent linuxEvent in this.parser.Parse(bufferPart))
                        {
                            // If doThreadTime is true this is running on a single thread.
                            blockedTimeAnalyzer?.UpdateThreadState(linuxEvent);

                            StackSourceSample sample = this.CreateSampleFor(linuxEvent, blockedTimeAnalyzer);
                            threadSamples[(int)givenArrayIndex].Add(sample);

                            blockedTimeAnalyzer?.LinuxEventSampleAssociation(linuxEvent, sample);
                        }
                        bufferPart.Dispose();
                    }
                }, i);

                tasks[i].Start();
            }

            Task.WaitAll(tasks);

            if (threadBlockedTimeAnalyzers != null)
            {
                List<BlockedTimeAnalyzer> allBlockedTimeAnalyzers = CustomExtensions.ConcatListsOfLists(threadBlockedTimeAnalyzers).ToList();
                this.FixBlockedTimes(allBlockedTimeAnalyzers);
                foreach (var blockedTimeAnalyzer in allBlockedTimeAnalyzers)
                {
                    this.TotalBlockedTime += blockedTimeAnalyzer.TotalBlockedTime;
                }
            }
            else
            {
                this.TotalBlockedTime = -1;
            }

            IEnumerable<StackSourceSample> allSamples = CustomExtensions.ConcatListsOfLists(threadSamples);

            this.AddSamples(allSamples);
        }

        protected override StackSourceFrameIndex InternFrame(string displayName)
        {
            StackSourceFrameIndex frameIndex;
            if (!frames.TryGetValue(displayName, out frameIndex))
            {
                lock (internFrameLock)
                {
                    frameIndex = this.Interner.FrameIntern(displayName);
                    frames[displayName] = frameIndex;
                }
            }

            return frameIndex;
        }

        protected override StackSourceCallStackIndex InternCallerStack(StackSourceFrameIndex frameIndex, StackSourceCallStackIndex stackIndex)
        {
            lock (internCallStackLock)
            {
                return this.Interner.CallStackIntern(frameIndex, stackIndex);
            }
        }

        #region private
        // If the length returned is -1, then there's no more stream in the
        //   master source, otherwise, buffer should be valid with the length returned
        // Note: This needs to be thread safe
        private FastStream GetNextSubStream(FastStream source)
        {
            lock (bufferLock)
            {
                if (source.EndOfStream)
                {
                    return null;
                }

                uint startLook = (uint)this.BufferSize * 3 / 4;

                uint length;

                bool isComplete;

                isComplete = this.TryGetCompleteBuffer(source, startLook, 1, source.MaxPeek - TruncateString.Length, out length);

                FastStream subStream = source.ReadSubStream((int)length, trail: (!isComplete ? TruncateString : null));

                if (!isComplete)
                {
                    this.FindValidStartOn(source);
                }

                source.SkipWhiteSpace();

                return subStream;
            }
        }

        private const string TruncateString = "0 truncate (truncate)";

        private bool TryGetCompleteBuffer(FastStream source, uint startLook, double portion, int maxLength, out uint length)
        {
            Contract.Requires(source != null, nameof(source));

            length = (uint)(startLook * portion);

            if (source.Peek(startLook) == 0)
            {
                return true;
            }

            uint lastNewLine = length;

            for (uint i = length; i < maxLength; i++)
            {
                byte current = source.Peek(i);

                if (this.parser.IsEndOfSample(source, current, source.Peek(i + 1)))
                {
                    length = i;
                    return true;
                }

                if (current == '\n')
                {
                    lastNewLine = length;
                }
            }

            if (portion < 0.5)
            {
                length = lastNewLine;
                return false;
            }

            return this.TryGetCompleteBuffer(source, startLook, portion * 0.8, maxLength, out length);
        }

        // Assumes that source is at an invalid start position.
        private void FindValidStartOn(FastStream source)
        {
            while (!this.parser.IsEndOfSample(source))
            {
                source.MoveNext();
            }
        }

        private void FixBlockedTimes(List<BlockedTimeAnalyzer> analyzers)
        {
            analyzers.Sort((x, y) => x.TimeStamp.CompareTo(y.TimeStamp));

            double lastTimeStamp = analyzers[analyzers.Count - 1].TimeStamp;

            for (int i = 0; i < analyzers.Count; i++)
            {
                var endingStates = analyzers[i].EndingStates;

                if (i < analyzers.Count - 1)
                {
                    List<int> threadIds = endingStates.Keys.ToList();
                    foreach (int threadId in threadIds)
                    {
                        for (int j = i + 1; j < analyzers.Count; j++)
                        {
                            var beginningStates = analyzers[j].BeginningStates;

                            if (beginningStates.ContainsKey(threadId))
                            {
                                var afterEvent = beginningStates[threadId].Value;
                                analyzers[i].UpdateThreadState(afterEvent);

                                break;
                            }
                        }
                    }
                }

                analyzers[i].FinishAnalyizing(lastTimeStamp);
            }
        }

        private ConcurrentDictionary<string, StackSourceFrameIndex> frames;
        private object internFrameLock = new object();
        private object internCallStackLock = new object();

        private object bufferLock = new object();
        private const int bufferDivider = 4;

        private const int MaxThreadCount = 4;
        #endregion
    }

    public class LinuxPerfScriptStackSource : InternStackSource
    {
        public LinuxPerfScriptStackSource(string path, bool doThreadTime)
        {
            this.doThreadTime = doThreadTime;
            this.currentStackIndex = 0;

            ZipArchive archive;
            using (Stream stream = this.GetPerfScriptStream(path, out archive))
            {
                this.masterSource = new FastStream(stream, this.BufferSize);
                this.parser = new LinuxPerfScriptEventParser();
                this.parser.SetSymbolFile(archive);

                this.InternAllLinuxEvents(stream);
                stream.Close();
            }
            archive?.Dispose();
        }

        public double TotalBlockedTime { get; set; }

        /// <summary>
        /// Given a Linux event gotten from the trace, make its corresponding sample for the stack source.
        /// </summary>
        public StackSourceSample CreateSampleFor(LinuxEvent linuxEvent, BlockedTimeAnalyzer blockedTimeAnalyzer)
        {
            IEnumerable<Frame> frames = linuxEvent.CallerStacks;
            StackSourceCallStackIndex stackIndex = this.currentStackIndex;

            var sample = new StackSourceSample(this);
            sample.TimeRelativeMSec = linuxEvent.TimeMSec;
            sample.Metric = 1;

            stackIndex = this.InternFrames(frames.GetEnumerator(), stackIndex, linuxEvent.ProcessID, linuxEvent.ThreadID, blockedTimeAnalyzer);
            sample.StackIndex = stackIndex;

            return sample;
        }

        /// <summary>
        /// Takes collection of samples, sorts them by time and then stores them.
        /// </summary>
        public void AddSamples(IEnumerable<StackSourceSample> _samples)
        {
            Contract.Requires(_samples != null, nameof(_samples));

            if (!_samples.Any())
            {
                return;
            }

            List<StackSourceSample> samples = _samples.ToList();
            samples.Sort((x, y) => x.TimeRelativeMSec.CompareTo(y.TimeRelativeMSec));
            double startTime = samples[0].TimeRelativeMSec;
            foreach (var sample in samples)
            {
                sample.TimeRelativeMSec -= startTime;
                this.AddSample(sample);
            }

            this.SampleEndTime = samples.Last().TimeRelativeMSec;
        }

        protected virtual void DoInterning()
        {
            BlockedTimeAnalyzer blockedTimeAnalyzer = doThreadTime ? new BlockedTimeAnalyzer() : null;

            foreach (var linuxEvent in this.parser.ParseSkippingPreamble(this.masterSource))
            {
                blockedTimeAnalyzer?.UpdateThreadState(linuxEvent);
                this.AddSample(this.CreateSampleFor(linuxEvent, blockedTimeAnalyzer));
            }

            blockedTimeAnalyzer?.FinishAnalyizing();
            // TODO: Sort things in blocked time anaylizer
            // this.threadBlockedPeriods.Sort((x, y) => x.StartTime.CompareTo(y.StartTime));

            this.TotalBlockedTime = blockedTimeAnalyzer != null ? blockedTimeAnalyzer.TotalBlockedTime : -1;
        }

        protected virtual StackSourceCallStackIndex InternCallerStack(StackSourceFrameIndex frameIndex, StackSourceCallStackIndex stackIndex)
        {
            return this.Interner.CallStackIntern(frameIndex, stackIndex);
        }

        protected virtual StackSourceFrameIndex InternFrame(string displayName)
        {
            return this.Interner.FrameIntern(displayName);
        }

        protected readonly LinuxPerfScriptEventParser parser;
        protected readonly FastStream masterSource;
        protected readonly bool doThreadTime;
        protected readonly int BufferSize = 262144;

        #region private
        private void InternAllLinuxEvents(Stream stream)
        {
            this.DoInterning();
            this.Interner.DoneInterning();
        }

        private StackSourceCallStackIndex InternFrames(IEnumerator<Frame> frameIterator, StackSourceCallStackIndex stackIndex, int processID, int? threadid = null, BlockedTimeAnalyzer blockedTimeAnalyzer = null)
        {
            // We shouldn't advance the iterator if thread time is enabled because we need 
            //   to add an extra frame to the caller stack that is not in the frameIterator.
            //   i.e. Short-circuiting prevents the frameIterator from doing MoveNext :)
            if (blockedTimeAnalyzer == null && !frameIterator.MoveNext())
            {
                return StackSourceCallStackIndex.Invalid;
            }

            StackSourceFrameIndex frameIndex;
            string frameDisplayName;

            if (blockedTimeAnalyzer != null)
            {
                // If doThreadTime is true, then we need to make sure that threadid is not null
                Contract.Requires(threadid != null, nameof(threadid));

                if (blockedTimeAnalyzer.IsThreadBlocked((int)threadid))
                {
                    frameDisplayName = LinuxThreadState.BLOCKED_TIME.ToString();
                }
                else
                {
                    frameDisplayName = LinuxThreadState.CPU_TIME.ToString();
                }
            }
            else
            {
                frameDisplayName = frameIterator.Current.DisplayName;
            }

            frameIndex = this.InternFrame(frameDisplayName);

            stackIndex = this.InternCallerStack(frameIndex, this.InternFrames(frameIterator, stackIndex, processID));

            return stackIndex;
        }

        private Stream GetPerfScriptStream(string path, out ZipArchive archive)
        {
            archive = null;
            if (path.EndsWith(".trace.zip"))
            {
                archive = ZipFile.OpenRead(path);
                ZipArchiveEntry foundEntry = null;

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith(".data.txt"))
                    {
                        foundEntry = entry;
                        break;
                    }
                }

                return foundEntry?.Open();
            }
            else
                return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        private double? SampleEndTime;

        private StackSourceCallStackIndex currentStackIndex;
        #endregion
    }

    public enum LinuxThreadState
    {
        BLOCKED_TIME,
        CPU_TIME
    }

    /// <summary>
    /// Analyzes blocked time 
    /// </summary>
    public class BlockedTimeAnalyzer
    {
        public double TimeStamp { get; private set; }
        public Dictionary<int, KeyValuePair<LinuxThreadState, LinuxEvent>> BeginningStates { get; }
        public Dictionary<int, KeyValuePair<LinuxThreadState, LinuxEvent>> EndingStates { get; }
        public Dictionary<LinuxEvent, StackSourceSample> LinuxEventSamples { get; }
        public Dictionary<int, int> EndingCpuUsage { get; }
        public List<ThreadPeriod> BlockedThreadPeriods { get; }

        public double TotalBlockedTime
        {
            get
            {
                double totalTime = 0;
                foreach (var threadPeriod in this.BlockedThreadPeriods)
                {
                    totalTime += threadPeriod.Period;
                }

                return totalTime;
            }
        }

        public BlockedTimeAnalyzer()
        {
            this.BeginningStates = new Dictionary<int, KeyValuePair<LinuxThreadState, LinuxEvent>>();
            this.EndingStates = new Dictionary<int, KeyValuePair<LinuxThreadState, LinuxEvent>>();
            this.LinuxEventSamples = new Dictionary<LinuxEvent, StackSourceSample>();
            this.EndingCpuUsage = new Dictionary<int, int>();
            this.BlockedThreadPeriods = new List<ThreadPeriod>();
        }

        public void UpdateThreadState(LinuxEvent linuxEvent)
        {
            if (this.TimeStamp < linuxEvent.TimeMSec)
            {
                this.TimeStamp = linuxEvent.TimeMSec;
            }

            if (!this.BeginningStates.ContainsKey(linuxEvent.ThreadID))
            {
                this.BeginningStates.Add(
                    linuxEvent.ThreadID,
                    new KeyValuePair<LinuxThreadState, LinuxEvent>(LinuxThreadState.CPU_TIME, linuxEvent));

                this.EndingStates[linuxEvent.ThreadID] = this.BeginningStates[linuxEvent.ThreadID];
            }

            this.DoMetrics(linuxEvent);
        }

        public void LinuxEventSampleAssociation(LinuxEvent linuxEvent, StackSourceSample sample)
        {
            this.LinuxEventSamples[linuxEvent] = sample;
        }

        public bool IsThreadBlocked(int threadId)
        {
            return this.EndingStates.ContainsKey(threadId) && this.EndingStates[threadId].Key == LinuxThreadState.BLOCKED_TIME;
        }

        public void FinishAnalyizing(double endTime)
        {
            this.FlushBlockedThreadsAt(endTime);
            this.BlockedThreadPeriods.Sort((x, y) => x.StartTime.CompareTo(y.StartTime));
        }

        public void FinishAnalyizing()
        {
            this.FinishAnalyizing(this.TimeStamp);
        }

        public void FlushBlockedThreadsAt(double endTime)
        {
            foreach (int threadid in this.EndingStates.Keys)
            {
                if (this.EndingStates[threadid].Key == LinuxThreadState.BLOCKED_TIME)
                {
                    this.AddThreadPeriod(threadid, this.EndingStates[threadid].Value.TimeMSec, TimeStamp);
                }
            }
        }

        private void DoMetrics(LinuxEvent linuxEvent)
        {
            KeyValuePair<LinuxThreadState, LinuxEvent> sampleInfo;

            if (this.EndingStates.TryGetValue(linuxEvent.ThreadID, out sampleInfo))
            {
                linuxEvent.Period = linuxEvent.TimeMSec - sampleInfo.Value.TimeMSec;
            }

            // This is check for completed scheduler events, ones that start with prev_comm and have 
            //   corresponding next_comm.
            if (linuxEvent.Kind == EventKind.Scheduler)
            {
                SchedulerEvent schedEvent = (SchedulerEvent)linuxEvent;
                if (this.EndingStates.ContainsKey(schedEvent.Switch.PreviousThreadID) &&
                    this.EndingStates[schedEvent.Switch.PreviousThreadID].Key == LinuxThreadState.CPU_TIME) // Blocking
                {
                    sampleInfo = this.EndingStates[schedEvent.Switch.PreviousThreadID];

                    this.EndingStates[schedEvent.Switch.PreviousThreadID] =
                        new KeyValuePair<LinuxThreadState, LinuxEvent>(LinuxThreadState.BLOCKED_TIME, linuxEvent);

                    linuxEvent.Period = linuxEvent.TimeMSec - sampleInfo.Value.TimeMSec;
                }

                if (this.EndingStates.TryGetValue(schedEvent.Switch.NextThreadID, out sampleInfo) &&
                    sampleInfo.Key == LinuxThreadState.BLOCKED_TIME) // Unblocking
                {
                    this.EndingStates[schedEvent.Switch.NextThreadID] =
                        new KeyValuePair<LinuxThreadState, LinuxEvent>(LinuxThreadState.CPU_TIME, linuxEvent);

                    // sampleInfo.Value.Period = linuxEvent.Time - sampleInfo.Value.Time;
                    this.AddThreadPeriod(linuxEvent.ThreadID, sampleInfo.Value.TimeMSec, linuxEvent.TimeMSec);
                }

            }
            else if (linuxEvent.Kind == EventKind.Cpu)
            {
                int threadid;
                if (this.EndingCpuUsage.TryGetValue(linuxEvent.CpuNumber, out threadid) && threadid != linuxEvent.ThreadID) // Unblocking
                {
                    if (this.EndingStates.TryGetValue(threadid, out sampleInfo))
                    {
                        this.EndingStates[threadid] =
                            new KeyValuePair<LinuxThreadState, LinuxEvent>(LinuxThreadState.CPU_TIME, linuxEvent);
                        sampleInfo.Value.Period = linuxEvent.TimeMSec - sampleInfo.Value.TimeMSec;
                        this.AddThreadPeriod(linuxEvent.ThreadID, sampleInfo.Value.TimeMSec, linuxEvent.TimeMSec);
                    }
                }
            }

            this.EndingCpuUsage[linuxEvent.CpuNumber] = linuxEvent.ThreadID;
        }

        private void AddThreadPeriod(int threadId, double startTime, double endTime)
        {
            this.BlockedThreadPeriods.Add(new ThreadPeriod(threadId, startTime, endTime));
        }
    }

    public class ThreadPeriod
    {
        public int ThreadID { get; }
        public double StartTime { get; }
        public double EndTime { get; }
        public double Period { get { return this.EndTime - this.StartTime; } }

        internal ThreadPeriod(int threadId, double startTime, double endTime)
        {
            this.ThreadID = threadId;
            this.StartTime = startTime;
            this.EndTime = endTime;
        }
    }

    public static class CustomExtensions
    {
        public static IEnumerable<T> ConcatListsOfLists<T>(IEnumerable<T>[] objects)
        {
            Contract.Requires(objects != null, nameof(objects));

            IEnumerable<T> allObjects = null;
            foreach (var o in objects)
            {
                if (allObjects == null)
                {
                    allObjects = o;
                }
                else
                {
                    allObjects = allObjects.Concat(o);
                }
            }

            return allObjects;
        }
    }
}
