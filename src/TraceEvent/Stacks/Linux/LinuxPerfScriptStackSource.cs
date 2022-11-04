using Microsoft.Diagnostics.Tracing.Stacks;
using Microsoft.Diagnostics.Tracing.Utilities;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Tracing.StackSources
{
    public class ParallelLinuxPerfScriptStackSource : LinuxPerfScriptStackSource
    {
        public ParallelLinuxPerfScriptStackSource(string path, bool doThreadTime = false) : base(path, doThreadTime)
        {
        }

        protected override void DoInterning()
        {
            int threadCount = MaxThreadCount;

            frames = new ConcurrentDictionary<string, StackSourceFrameIndex>();

            parser.SkipPreamble(masterSource);

            Task[] tasks = new Task[threadCount];

            List<BlockedTimeAnalyzer>[] threadBlockedTimeAnalyzers = null;
            if (doThreadTime)
            {
                threadBlockedTimeAnalyzers = new List<BlockedTimeAnalyzer>[tasks.Length];
            }

            List<LinuxPerfScriptStackSourceSample>[] threadSamples = new List<LinuxPerfScriptStackSourceSample>[tasks.Length];

            for (int i = 0; i < tasks.Length; i++)
            {
                threadSamples[i] = new List<LinuxPerfScriptStackSourceSample>();

                if (threadBlockedTimeAnalyzers != null)
                {
                    threadBlockedTimeAnalyzers[i] = new List<BlockedTimeAnalyzer>();
                }

                var currentCulture = Thread.CurrentThread.CurrentCulture;
                var currentUICulture = Thread.CurrentThread.CurrentUICulture;
                tasks[i] = new Task((object givenArrayIndex) =>
                {

                    var oldCultures = Tuple.Create(Thread.CurrentThread.CurrentCulture, Thread.CurrentThread.CurrentUICulture);

                    try
                    {
                        Thread.CurrentThread.CurrentCulture = currentCulture;
                        Thread.CurrentThread.CurrentUICulture = currentUICulture;

                        FastStream bufferPart;
                        while ((bufferPart = GetNextSubStream(masterSource)) != null)
                        {
                            BlockedTimeAnalyzer blockedTimeAnalyzer = null;
                            if (threadBlockedTimeAnalyzers != null)
                            {
                                blockedTimeAnalyzer = new BlockedTimeAnalyzer(this);
                                threadBlockedTimeAnalyzers[(int)givenArrayIndex].Add(blockedTimeAnalyzer);
                            }

                            foreach (LinuxEvent linuxEvent in parser.Parse(bufferPart))
                            {
                                // If doThreadTime is true this is running on a single thread.
                                blockedTimeAnalyzer?.UpdateThreadState(linuxEvent);

                                LinuxPerfScriptStackSourceSample sample = CreateSampleFor(linuxEvent, blockedTimeAnalyzer);

                                if (linuxEvent.Kind == EventKind.Cpu)
                                {
                                    threadSamples[(int)givenArrayIndex].Add(sample);
                                }

                                blockedTimeAnalyzer?.LinuxEventSampleAssociation(linuxEvent, sample);
                            }
                            bufferPart.Dispose();
                        }
                    }
                    finally
                    {
                        Thread.CurrentThread.CurrentCulture = oldCultures.Item1;
                        Thread.CurrentThread.CurrentUICulture = oldCultures.Item2;
                    }
                }, i);

                tasks[i].Start();
            }

            Task.WaitAll(tasks);

            if (threadBlockedTimeAnalyzers != null)
            {
                List<BlockedTimeAnalyzer> allBlockedTimeAnalyzers = CustomExtensions.ConcatListsOfLists(threadBlockedTimeAnalyzers).ToList();
                FixBlockedTimes(allBlockedTimeAnalyzers);
                foreach (var blockedTimeAnalyzer in allBlockedTimeAnalyzers)
                {
                    TotalBlockedTime += blockedTimeAnalyzer.TotalBlockedTime;
                }
            }
            else
            {
                TotalBlockedTime = -1;
            }

            IEnumerable<LinuxPerfScriptStackSourceSample> allSamples = CustomExtensions.ConcatListsOfLists(threadSamples);

            AddSamples(allSamples);
        }

        protected override StackSourceFrameIndex InternFrame(string displayName)
        {
            StackSourceFrameIndex frameIndex;
            if (!frames.TryGetValue(displayName, out frameIndex))
            {
                lock (internFrameLock)
                {
                    frameIndex = Interner.FrameIntern(displayName);
                    frames[displayName] = frameIndex;
                }
            }

            return frameIndex;
        }

        protected override StackSourceCallStackIndex InternCallerStack(StackSourceFrameIndex frameIndex, StackSourceCallStackIndex stackIndex)
        {
            lock (internCallStackLock)
            {
                return Interner.CallStackIntern(frameIndex, stackIndex);
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

                uint startLook = (uint)BufferSize * 3 / 4;

                uint length;

                bool isComplete;

                isComplete = TryGetCompleteBuffer(source, startLook, 1, source.MaxPeek - TruncateString.Length, out length);

                FastStream subStream = source.ReadSubStream((int)length, trail: (!isComplete ? TruncateString : null));

                if (!isComplete)
                {
                    FindValidStartOn(source);
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

                if (parser.IsEndOfSample(source, current, source.Peek(i + 1)))
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

            return TryGetCompleteBuffer(source, startLook, portion * 0.8, maxLength, out length);
        }

        // Assumes that source is at an invalid start position.
        private void FindValidStartOn(FastStream source)
        {
            while (!parser.IsEndOfSample(source))
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

                analyzers[i].FinishAnalyzing(lastTimeStamp);
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
            currentStackIndex = 0;

            Interner.FrameNameLookup = new Func<StackSourceFrameIndex, bool, string>((frameIndex, fullModule) =>
            {
                // The frame index is reliably off by one in this lookup.
                return processNameBuilder.GetProcessName(frameIndex + 1);
            });

            ZipArchive archive;
            using (Stream stream = GetPerfScriptStream(path, out archive))
            {
                masterSource = new FastStream(stream, BufferSize);
                parser = new LinuxPerfScriptEventParser();
                parser.SetSymbolFile(archive);

                InternAllLinuxEvents(stream);
                stream.Close();
            }
            archive?.Dispose();
        }

        public double StartTimeStampMSec { get; set; }

        public double TotalBlockedTime { get; set; }

        #region private
        internal /*protected*/ GrowableArray<LinuxPerfScriptStackSourceSample> m_LinuxPerfScriptSamples;
        #endregion

        public LinuxPerfScriptStackSourceSample GetLinuxPerfScriptSampleByIndex(StackSourceSampleIndex sampleIndex)
        {
            return m_LinuxPerfScriptSamples[(int)sampleIndex];
        }

        /// <summary>
        /// Given a Linux event gotten from the trace, make its corresponding sample for the stack source.
        /// </summary>
        public LinuxPerfScriptStackSourceSample CreateSampleFor(LinuxEvent linuxEvent, BlockedTimeAnalyzer blockedTimeAnalyzer)
        {
            IEnumerable<Frame> frames = linuxEvent.CallerStacks;
            StackSourceCallStackIndex stackIndex = currentStackIndex;

            var sample = new LinuxPerfScriptStackSourceSample(this);
            sample.TimeRelativeMSec = linuxEvent.TimeMSec - StartTimeStampMSec;
            sample.Metric = (float)linuxEvent.Period;
            sample.CpuNumber = linuxEvent.CpuNumber;

            stackIndex = InternFrames(frames.GetEnumerator(), stackIndex, linuxEvent.ProcessID, linuxEvent.ThreadID, doThreadTime ? blockedTimeAnalyzer : null);
            sample.StackIndex = stackIndex;

            return sample;
        }

        /// <summary>
        /// Takes collection of samples, sorts them by time and then stores them.
        /// </summary>
        public void AddSamples(IEnumerable<LinuxPerfScriptStackSourceSample> _samples)
        {
            Contract.Requires(_samples != null, nameof(_samples));

            if (!_samples.Any())
            {
                return;
            }

            List<LinuxPerfScriptStackSourceSample> samples = _samples.ToList();
            samples.Sort((x, y) => x.TimeRelativeMSec.CompareTo(y.TimeRelativeMSec));
            double startTime = samples[0].TimeRelativeMSec;
            foreach (var sample in samples)
            {
                sample.TimeRelativeMSec -= startTime;
                AddSample(sample);
            }

            SampleEndTime = samples.Last().TimeRelativeMSec;
        }

        public void AddSample(LinuxPerfScriptStackSourceSample sample)
        {
            var baseSample = AddSample((StackSourceSample) sample);
            m_LinuxPerfScriptSamples.Add(new LinuxPerfScriptStackSourceSample(baseSample, sample.CpuNumber));
        }

        protected virtual void DoInterning()
        {
            BlockedTimeAnalyzer blockedTimeAnalyzer = new BlockedTimeAnalyzer(this);

            foreach (var linuxEvent in parser.ParseSkippingPreamble(masterSource))
            {
                // Set the start timestamp of the trace using the first event.
                if(StartTimeStampMSec == 0)
                {
                    StartTimeStampMSec = linuxEvent.TimeMSec;
                }

                // BlockedTimeAnalyzer handles all sample production.
                // Only give it the set of events that we want it to process.
                if (doThreadTime || linuxEvent.Kind == EventKind.Cpu)
                {
                    blockedTimeAnalyzer.UpdateThreadState(linuxEvent);
                }
            }

            blockedTimeAnalyzer?.FinishAnalyzing();
            // TODO: Sort things in blocked time analyzer
            // this.threadBlockedPeriods.Sort((x, y) => x.StartTime.CompareTo(y.StartTime));

            TotalBlockedTime = blockedTimeAnalyzer.TotalBlockedTime;
        }

        protected virtual StackSourceCallStackIndex InternCallerStack(StackSourceFrameIndex frameIndex, StackSourceCallStackIndex stackIndex)
        {
            return Interner.CallStackIntern(frameIndex, stackIndex);
        }

        protected virtual StackSourceFrameIndex InternFrame(string displayName)
        {
            return Interner.FrameIntern(displayName);
        }

        protected readonly LinuxPerfScriptEventParser parser;
        protected readonly FastStream masterSource;
        protected readonly bool doThreadTime;
        protected readonly int BufferSize = 262144;
        internal readonly LinuxPerfScriptProcessNameBuilder processNameBuilder = new LinuxPerfScriptProcessNameBuilder();

        #region private
        private void InternAllLinuxEvents(Stream stream)
        {
            DoInterning();
            Interner.DoneInterning();
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

            Frame currentFrame = frameIterator.Current;

            if (currentFrame != null && currentFrame.Kind == FrameKind.ProcessFrame)
            {
                ProcessFrame processFrame = (ProcessFrame)currentFrame;

                // Intern a name-agnostic frame so that all process frames for a PID intern to the same index.
                frameIndex = InternFrame($"Process {processFrame.ID}");

                // Re-intern the frame as a derived frame, so that on resolution, FrameNameLookup() gets called.
                frameIndex = Interner.FrameIntern(frameIndex, string.Empty);

                // Map the frameIndex to the candidate process name.  This gets consumed in FrameNameLookup().
                processNameBuilder.SaveProcessName(frameIndex, processFrame.Name, processFrame.ID);
            }
            else
            {
                frameIndex = InternFrame(frameDisplayName);
            }

            stackIndex = InternCallerStack(frameIndex, InternFrames(frameIterator, stackIndex, processID));

            return stackIndex;
        }

        private Stream GetPerfScriptStream(string path, out ZipArchive archive)
        {
            archive = null;
            if (path.EndsWith(".trace.zip", StringComparison.OrdinalIgnoreCase))
            {
                archive = ZipFile.OpenRead(path);
                ZipArchiveEntry foundEntry = null;

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith(".data.txt", StringComparison.OrdinalIgnoreCase))
                    {
                        foundEntry = entry;
                        break;
                    }
                }

                if (foundEntry == null)
                {
                    throw new ApplicationException($"file {path} is does not have a *.data.txt file inside.");
                }

                return foundEntry.Open();
            }
            else if (path.EndsWith(".data.txt", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".perf.data.dump", StringComparison.OrdinalIgnoreCase))
            {
                return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            else
            {
                throw new ApplicationException($"file {path} is not a *.trace.zip *.data.txt or a *.perf.data.dump suffix.");
            }
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
        public Dictionary<LinuxEvent, LinuxPerfScriptStackSourceSample> LinuxEventSamples { get; }
        public Dictionary<int, LinuxEvent> LastCpuUsage { get; }
        public List<ThreadPeriod> BlockedThreadPeriods { get; }
        public LinuxPerfScriptStackSource StackSource { get; }

        public double TotalBlockedTime
        {
            get
            {
                double totalTime = 0;
                foreach (var threadPeriod in BlockedThreadPeriods)
                {
                    totalTime += threadPeriod.Period;
                }

                return totalTime;
            }
        }

        public BlockedTimeAnalyzer(LinuxPerfScriptStackSource stackSource)
        {
            BeginningStates = new Dictionary<int, KeyValuePair<LinuxThreadState, LinuxEvent>>();
            EndingStates = new Dictionary<int, KeyValuePair<LinuxThreadState, LinuxEvent>>();
            LinuxEventSamples = new Dictionary<LinuxEvent, LinuxPerfScriptStackSourceSample>();
            LastCpuUsage = new Dictionary<int, LinuxEvent>();
            BlockedThreadPeriods = new List<ThreadPeriod>();
            StackSource = stackSource;
        }

        public void UpdateThreadState(LinuxEvent linuxEvent)
        {
            if (TimeStamp < linuxEvent.TimeMSec)
            {
                TimeStamp = linuxEvent.TimeMSec;
            }

            if (!BeginningStates.ContainsKey(linuxEvent.ThreadID))
            {
                BeginningStates.Add(
                    linuxEvent.ThreadID,
                    new KeyValuePair<LinuxThreadState, LinuxEvent>(LinuxThreadState.CPU_TIME, linuxEvent));

                EndingStates[linuxEvent.ThreadID] = BeginningStates[linuxEvent.ThreadID];
            }

            DoMetrics(linuxEvent);
        }

        public void LinuxEventSampleAssociation(LinuxEvent linuxEvent, LinuxPerfScriptStackSourceSample sample)
        {
            LinuxEventSamples[linuxEvent] = sample;
        }

        public bool IsThreadBlocked(int threadId)
        {
            return EndingStates.ContainsKey(threadId) && EndingStates[threadId].Key == LinuxThreadState.BLOCKED_TIME;
        }

        public void FinishAnalyzing(double endTime)
        {
            FlushBlockedThreadsAt(endTime);
            BlockedThreadPeriods.Sort((x, y) => x.StartTime.CompareTo(y.StartTime));
        }

        public void FinishAnalyzing()
        {
            FinishAnalyzing(TimeStamp);
        }

        public void FlushBlockedThreadsAt(double endTime)
        {
            foreach (int threadid in EndingStates.Keys)
            {
                if (EndingStates[threadid].Key == LinuxThreadState.BLOCKED_TIME)
                {
                    KeyValuePair<LinuxThreadState, LinuxEvent> sampleInfo = EndingStates[threadid];
                    sampleInfo.Value.Period = TimeStamp - sampleInfo.Value.TimeMSec;
                    AddThreadPeriod(threadid, sampleInfo.Value.TimeMSec, TimeStamp);
                    StackSource.AddSample(StackSource.CreateSampleFor(sampleInfo.Value, this));
                }
            }
        }

        private void DoMetrics(LinuxEvent linuxEvent)
        {
            KeyValuePair<LinuxThreadState, LinuxEvent> sampleInfo;

            // This is check for completed scheduler events, ones that start with prev_comm and have 
            //   corresponding next_comm.
            if (linuxEvent.Kind == EventKind.Scheduler)
            {
                SchedulerEvent schedEvent = (SchedulerEvent)linuxEvent;
                if (EndingStates.ContainsKey(schedEvent.Switch.PreviousThreadID) &&
                    EndingStates[schedEvent.Switch.PreviousThreadID].Key == LinuxThreadState.CPU_TIME) // Blocking
                {
                    // PreviousThreadID is now blocking.  linuxEvent contains its blocking stack, so save it here.
                    // When it unblocks (becomes NextThreadID below, we'll log a sample for it.)
                    EndingStates[schedEvent.Switch.PreviousThreadID] =
                        new KeyValuePair<LinuxThreadState, LinuxEvent>(LinuxThreadState.BLOCKED_TIME, linuxEvent);
                }

                if (EndingStates.TryGetValue(schedEvent.Switch.NextThreadID, out sampleInfo) &&
                    sampleInfo.Key == LinuxThreadState.BLOCKED_TIME) // Unblocking
                {
                    sampleInfo.Value.Period = linuxEvent.TimeMSec - sampleInfo.Value.TimeMSec;
                    AddThreadPeriod(sampleInfo.Value.ThreadID, sampleInfo.Value.TimeMSec, linuxEvent.TimeMSec);
                    StackSource.AddSample(StackSource.CreateSampleFor(sampleInfo.Value, this));

                    EndingStates[schedEvent.Switch.NextThreadID] =
                        new KeyValuePair<LinuxThreadState, LinuxEvent>(LinuxThreadState.CPU_TIME, linuxEvent);
                }
            }
            else if(linuxEvent.Kind == EventKind.ThreadExit)
            {
                ThreadExitEvent exitEvent = (ThreadExitEvent)linuxEvent;
                if (EndingStates.TryGetValue(exitEvent.Exit.ThreadID, out sampleInfo))
                {
                    if (sampleInfo.Key == LinuxThreadState.BLOCKED_TIME) // Blocked on exit
                    {
                        sampleInfo.Value.Period = linuxEvent.TimeMSec - sampleInfo.Value.TimeMSec;
                        AddThreadPeriod(sampleInfo.Value.ThreadID, sampleInfo.Value.TimeMSec, linuxEvent.TimeMSec);
                        StackSource.AddSample(StackSource.CreateSampleFor(sampleInfo.Value, this));
                    }
                    else // Unblocked on exit
                    {
                        linuxEvent.Period = linuxEvent.TimeMSec - sampleInfo.Value.TimeMSec;
                        StackSource.AddSample(StackSource.CreateSampleFor(linuxEvent, this));
                    }

                    // Remove the thread so that any events that might come after the exit are ignored.
                    EndingStates.Remove(exitEvent.Exit.ThreadID);
                }
            }
            else if (linuxEvent.Kind == EventKind.Cpu)
            {
                // Keep track of the last CPU sample for each CPU, and use its timestamp
                // to determine how much weight to give the sample.
                if(LastCpuUsage.TryGetValue(linuxEvent.CpuNumber, out LinuxEvent lastCpuEvent))
                {
                    lastCpuEvent.Period = linuxEvent.TimeMSec - lastCpuEvent.TimeMSec;
                    StackSource.AddSample(StackSource.CreateSampleFor(lastCpuEvent, this));
                }

                LastCpuUsage[linuxEvent.CpuNumber] = linuxEvent;
            }
        }

        private void AddThreadPeriod(int threadId, double startTime, double endTime)
        {
            BlockedThreadPeriods.Add(new ThreadPeriod(threadId, startTime, endTime));
        }
    }

    public class ThreadPeriod
    {
        public int ThreadID { get; }
        public double StartTime { get; }
        public double EndTime { get; }
        public double Period { get { return EndTime - StartTime; } }

        internal ThreadPeriod(int threadId, double startTime, double endTime)
        {
            ThreadID = threadId;
            StartTime = startTime;
            EndTime = endTime;
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
