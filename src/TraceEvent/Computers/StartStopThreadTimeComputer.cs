using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Stacks;

namespace Microsoft.Diagnostics.Tracing.Computers
{
    public class ContentionTimeComputer : StartStopThreadTimeComputer
    {
        public ContentionTimeComputer(TraceLog eventLog, MutableTraceEventStackSource stackSource) : base(eventLog, stackSource)
        {
        }

        protected override void Subscribe(TraceLogEventSource eventSource)
        {
            eventSource.Clr.ContentionStart += OnStart;
            eventSource.Clr.ContentionStop += OnStop;
        }

        protected override void RecordAdditionalDataOnMissingStartAtTraceStart(StackSourceSample sample)
        {
            sample.StackIndex = _stackSource.Interner.CallStackIntern(_stackSource.Interner.FrameIntern($"EventData DurationNs {sample.Metric*NanosInMillisecond:N0}"), sample.StackIndex);
        }
        
        protected override void RecordAdditionalStartData(StackSourceSample sample, TraceEvent data)
        {
            var startData = (ContentionStartTraceData) data;
            sample.StackIndex = _interner.CallStackIntern(_interner.FrameIntern($"EventData ContentionFlags {startData.ContentionFlags}"), sample.StackIndex);
            sample.StackIndex = _interner.CallStackIntern(_interner.FrameIntern($"EventData LockID {startData.LockID}"), sample.StackIndex);
            sample.StackIndex = _interner.CallStackIntern(_interner.FrameIntern($"EventData AssociatedObjectID {startData.AssociatedObjectID}"), sample.StackIndex);
            sample.StackIndex = _interner.CallStackIntern(_interner.FrameIntern($"EventData LockOwnerThreadID {startData.LockOwnerThreadID}"), sample.StackIndex);
        }
        
        protected override void RecordAdditionalDataOnMissingStopAtTraceEnd(StackSourceSample sample)
        {
            sample.StackIndex = _stackSource.Interner.CallStackIntern(_stackSource.Interner.FrameIntern($"EventData DurationNs {sample.Metric*NanosInMillisecond:N0}"), sample.StackIndex);
        }
        
        protected override void RecordAdditionalStopData(StackSourceSample sample, TraceEvent data)
        {
            var stopData = (ContentionStopTraceData) data;
            sample.Metric = (float) (stopData.DurationNs / NanosInMillisecond);
            sample.StackIndex = _stackSource.Interner.CallStackIntern(_stackSource.Interner.FrameIntern($"EventData DurationNs {stopData.DurationNs:N0}"), sample.StackIndex);
        }
    }
    
    public class WaitHandleWaitTimeComputer : StartStopThreadTimeComputer
    {
        public WaitHandleWaitTimeComputer(TraceLog eventLog, MutableTraceEventStackSource stackSource) : base(eventLog, stackSource)
        {}

        protected override void Subscribe(TraceLogEventSource eventSource)
        {
            eventSource.Clr.WaitHandleWaitStart += OnStart;
            eventSource.Clr.WaitHandleWaitStop += OnStop;
        }

        protected override void RecordAdditionalDataOnMissingStartAtTraceStart(StackSourceSample sample)
        {
            sample.StackIndex = _stackSource.Interner.CallStackIntern(_stackSource.Interner.FrameIntern($"EventData DurationNs {sample.Metric*NanosInMillisecond:N0}"), sample.StackIndex);
        }

        protected override void RecordAdditionalStartData(StackSourceSample sample, TraceEvent data)
        {
            var startData = (WaitHandleWaitStartTraceData) data;
            sample.StackIndex = _interner.CallStackIntern(_interner.FrameIntern($"EventData WaitSource {startData.WaitSource}"), sample.StackIndex);
            sample.StackIndex = _interner.CallStackIntern(_interner.FrameIntern($"EventData AssociatedObjectID {startData.AssociatedObjectID}"), sample.StackIndex);
        }

        protected override void RecordAdditionalDataOnMissingStopAtTraceEnd(StackSourceSample sample)
        {
            sample.StackIndex = _stackSource.Interner.CallStackIntern(_stackSource.Interner.FrameIntern($"EventData DurationNs {sample.Metric*NanosInMillisecond:N0}"), sample.StackIndex);
        }

        protected override void RecordAdditionalStopData(StackSourceSample sample, TraceEvent data)
        {
            var stopData = (WaitHandleWaitStopTraceData) data;
            var durationMs = stopData.TimeStampRelativeMSec - sample.TimeRelativeMSec; // recompute the duration to get better precision than float
            sample.StackIndex = _stackSource.Interner.CallStackIntern(_stackSource.Interner.FrameIntern($"EventData DurationNs {durationMs*NanosInMillisecond:N0}"), sample.StackIndex);
        }
    }
    
    public abstract class StartStopThreadTimeComputer
    {
        protected readonly TraceLog _eventLog;
        protected readonly MutableTraceEventStackSource _stackSource;
        protected readonly StackSourceInterner _interner;
        
        private readonly StackSourceSample[] _samplesPerThread;

        protected const double NanosInMillisecond = 1000 * 1000;
        private const string BrokenEventNoStart = "BROKEN_EVENT NO_CORRESPONDING_START";
        private const string BrokenEventNoStop = "BROKEN_EVENT NO_CORRESPONDING_STOP";

        protected StartStopThreadTimeComputer(TraceLog eventLog, MutableTraceEventStackSource stackSource)
        {
            _eventLog = eventLog;
            _stackSource = stackSource;
            _interner = stackSource.Interner;
            _samplesPerThread = new StackSourceSample[eventLog.Threads.Count];
        }

        protected abstract void Subscribe(TraceLogEventSource eventSource);
        protected abstract void RecordAdditionalDataOnMissingStartAtTraceStart(StackSourceSample sample);
        protected abstract void RecordAdditionalStartData(StackSourceSample sample, TraceEvent data);
        protected abstract void RecordAdditionalDataOnMissingStopAtTraceEnd(StackSourceSample sample);
        protected abstract void RecordAdditionalStopData(StackSourceSample sample, TraceEvent data);
        
        public void GenerateStartStopThreadTimeStacks()
        {
            var eventSource = _eventLog.Events.GetSource();
            
            Subscribe(eventSource);
            
            eventSource.Process();

            CheckForMissingStopEvents();
            
            _stackSource.DoneAddingSamples();
        }

        private void CheckForMissingStopEvents()
        {
            for (var i = 0; i < _samplesPerThread.Length; i++)
            {
                var sample = _samplesPerThread[i];
                if (sample != null && !IsSampleEmpty(sample))
                {
                    // Start event without corresponding Stop at the end of the trace
                    sample.Metric = (float) (_eventLog.SessionEndTimeRelativeMSec - sample.TimeRelativeMSec);
                    RecordAdditionalDataOnMissingStopAtTraceEnd(sample);
                    AddAndResetSample(sample);
                }
            }
        }

        protected void OnStart(TraceEvent startData)
        {
            var threadIndex = (int) startData.Thread().ThreadIndex;
            var sample = _samplesPerThread[threadIndex];
            
            if (sample == null)
            {
                _samplesPerThread[threadIndex] = sample = CreateSample();
            }
            else if (!IsSampleEmpty(sample))
            {
                // We received Start -> Start sequence, this should never happen
                // Probably there were missing events in the trace
                sample.TimeRelativeMSec = float.Epsilon;
                sample.StackIndex = _interner.CallStackIntern(_interner.FrameIntern(BrokenEventNoStop), sample.StackIndex);
                AddAndResetSample(sample);
            }
            
            sample.TimeRelativeMSec = startData.TimeStampRelativeMSec;
            sample.StackIndex = _stackSource.GetCallStack(startData.CallStackIndex(), startData);
            RecordAdditionalStartData(sample, startData);
        }

        protected void OnStop(TraceEvent stopData)
        {
            var threadIndex = (int) stopData.Thread().ThreadIndex;
            var sample = _samplesPerThread[threadIndex];
            
            if (sample == null)
            {
                // The first event for this thread is a Stop event, this could happen at the start of the trace
                // Only Start events have a call stack so we can't really include it meaningfully in the stacks window
                // However, the stack should at least contain a thread id so we do it anyway
                _samplesPerThread[threadIndex] = sample = CreateSample();
                sample.TimeRelativeMSec = 0;
                sample.Metric = (float) stopData.TimeStampRelativeMSec;
                sample.StackIndex = _stackSource.GetCallStack(stopData.CallStackIndex(), stopData);
                RecordAdditionalDataOnMissingStartAtTraceStart(sample);
                AddAndResetSample(sample);
                return;
            }

            if (IsSampleEmpty(sample))
            {
                // We received Stop -> Stop sequence, this should never happen
                // Probably there were missing events in the trace
                sample.TimeRelativeMSec = stopData.TimeStampRelativeMSec;
                sample.Metric = float.Epsilon;
                sample.StackIndex = _stackSource.GetCallStack(stopData.CallStackIndex(), stopData);
                sample.StackIndex = _interner.CallStackIntern(_interner.FrameIntern(BrokenEventNoStart), sample.StackIndex);
                AddAndResetSample(sample);
                return;
            }

            sample.Metric = (float) (stopData.TimeStampRelativeMSec - sample.TimeRelativeMSec);
            RecordAdditionalStopData(sample, stopData);
            AddAndResetSample(sample);
        }

        private bool IsSampleEmpty(StackSourceSample sample)
        {
            return sample.TimeRelativeMSec == 0 && sample.StackIndex == StackSourceCallStackIndex.Invalid && sample.Metric == 0;
        }

        private StackSourceSample CreateSample()
        {
            var sample = new StackSourceSample(_stackSource);
            ResetSample(sample);
            return sample;
        }

        private void AddAndResetSample(StackSourceSample sample)
        {
            _stackSource.AddSample(sample);
            ResetSample(sample);
        }
        
        private void ResetSample(StackSourceSample sample)
        {
            sample.Count = 1;
            sample.StackIndex = StackSourceCallStackIndex.Invalid;
            sample.TimeRelativeMSec = 0;
            sample.Metric = 0;
        }
        
        public static string[] GetDefaultFoldPatterns()
        {
            return new []
            {
                "EventData DurationNs;EventData WaitSource;EventData AssociatedObjectID;EventData ContentionFlags;EventData LockID;EventData LockOwnerThreadID;"
            };
        }
    }
}