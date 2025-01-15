using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Stacks;

namespace Microsoft.Diagnostics.Tracing.Computers
{
    public class ContentionLatencyComputer : StartStopLatencyComputer
    {
        public ContentionLatencyComputer(TraceLog eventLog, MutableTraceEventStackSource stackSource) : base(eventLog, stackSource)
        {
        }

        protected override void Subscribe(TraceLogEventSource eventSource)
        {
            eventSource.Clr.ContentionStart += OnStart;
            eventSource.Clr.ContentionStop += OnStop;
        }

        protected override void RecordAdditionalDataOnStopWithoutStart(StackSourceSample sample)
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
        
        protected override void RecordAdditionalDataOnStartWithoutStop(StackSourceSample sample)
        {
            sample.StackIndex = _interner.CallStackIntern(_interner.FrameIntern($"EventData DurationNs {sample.Metric*NanosInMillisecond:N0}"), sample.StackIndex);
        }
        
        protected override void RecordAdditionalStopData(StackSourceSample sample, TraceEvent data)
        {
            var stopData = (ContentionStopTraceData) data;
            if (stopData.DurationNs > 0)
            {
                sample.Metric = (float)(stopData.DurationNs / NanosInMillisecond);
            }
            sample.StackIndex = _interner.CallStackIntern(_interner.FrameIntern($"EventData DurationNs {stopData.DurationNs:N0}"), sample.StackIndex);
        }
    }
    
    public class WaitHandleWaitLatencyComputer : StartStopLatencyComputer
    {
        public WaitHandleWaitLatencyComputer(TraceLog eventLog, MutableTraceEventStackSource stackSource) : base(eventLog, stackSource)
        {}

        protected override void Subscribe(TraceLogEventSource eventSource)
        {
            eventSource.Clr.WaitHandleWaitStart += OnStart;
            eventSource.Clr.WaitHandleWaitStop += OnStop;
        }

        protected override void RecordAdditionalDataOnStopWithoutStart(StackSourceSample sample)
        {
            sample.StackIndex = _stackSource.Interner.CallStackIntern(_stackSource.Interner.FrameIntern($"EventData DurationNs {sample.Metric*NanosInMillisecond:N0}"), sample.StackIndex);
        }

        protected override void RecordAdditionalStartData(StackSourceSample sample, TraceEvent data)
        {
            var startData = (WaitHandleWaitStartTraceData) data;
            sample.StackIndex = _interner.CallStackIntern(_interner.FrameIntern($"EventData WaitSource {startData.WaitSource}"), sample.StackIndex);
            sample.StackIndex = _interner.CallStackIntern(_interner.FrameIntern($"EventData AssociatedObjectID {startData.AssociatedObjectID}"), sample.StackIndex);
        }

        protected override void RecordAdditionalDataOnStartWithoutStop(StackSourceSample sample)
        {
            sample.StackIndex = _interner.CallStackIntern(_interner.FrameIntern($"EventData DurationNs {sample.Metric*NanosInMillisecond:N0}"), sample.StackIndex);
        }

        protected override void RecordAdditionalStopData(StackSourceSample sample, TraceEvent data)
        {
            var stopData = (WaitHandleWaitStopTraceData) data;
            var durationMs = stopData.TimeStampRelativeMSec - sample.TimeRelativeMSec; // recompute the duration to get better precision than float
            sample.StackIndex = _interner.CallStackIntern(_interner.FrameIntern($"EventData DurationNs {durationMs*NanosInMillisecond:N0}"), sample.StackIndex);
        }
    }
    
    /// <summary>
    /// Computes the latency view of Start/Stop event pairs that happen on the same thread.
    /// Think contention or other "blocking" events, where Start and Stop have the same callstack.
    /// Inherit this class for each type of blocking events and
    /// look at <see cref="ContentionLatencyComputer"/> and <see cref="WaitHandleWaitLatencyComputer"/> for reference.
    /// </summary>
    public abstract class StartStopLatencyComputer
    {
        protected readonly TraceLog _eventLog;
        protected readonly MutableTraceEventStackSource _stackSource;
        /// <summary>
        /// A shortcut to <c>_stackSource.Interner</c>
        /// </summary>
        protected readonly StackSourceInterner _interner;
        
        private readonly StackSourceSample[] _samplesPerThread;

        protected const double NanosInMillisecond = 1000 * 1000;
        protected const string BrokenEventNoStart = "BROKEN_EVENT NO_CORRESPONDING_START";
        protected const string BrokenEventNoStop = "BROKEN_EVENT NO_CORRESPONDING_STOP";

        protected StartStopLatencyComputer(TraceLog eventLog, MutableTraceEventStackSource stackSource)
        {
            _eventLog = eventLog;
            _stackSource = stackSource;
            _interner = stackSource.Interner;
            _samplesPerThread = new StackSourceSample[eventLog.Threads.Count];
        }

        /// <summary>
        /// Implementations should subscribe <see cref="OnStart"/> and <see cref="OnStop"/> methods to the corresponding events
        /// The callstack of the Start event will be used in the resulting display. 
        /// </summary>
        protected abstract void Subscribe(TraceLogEventSource eventSource);
        /// <summary>
        /// Append custom data from Start event to the sample.
        /// Cast the TraceEvent to the concrete type of Start event you subscribed to
        /// and populate the sample like this:
        /// <code>
        /// var startData = (ContentionStartTraceData) data;
        /// var frame = _interner.FrameIntern($"EventData LockID {startData.LockID}");
        /// sample.StackIndex = _interner.CallStackIntern(frame, sample.StackIndex);
        /// </code>
        /// </summary>
        protected abstract void RecordAdditionalStartData(StackSourceSample sample, TraceEvent data);
        /// <summary>
        /// Append custom data from Stop event to the sample.
        /// Cast the TraceEvent to the concrete type of Stop event you subscribed to
        /// and populate the sample like below.
        /// <para>
        /// The implementations should include DurationNs event data to allow the user to distinguish
        /// between individual events with the same callstack.
        /// </para>
        /// <para>
        /// Note that you can also change <c>sample.Metric</c>:
        /// by default it will be set to <c>stop.TimeStampRelativeMSec - start.TimeStampRelativeMSec</c>
        /// </para>
        /// <code>
        /// var stopData = (ContentionStopTraceData) data;
        /// var frame = _interner.FrameIntern($"EventData DurationNs {stopData.DurationNs:N0}");
        /// sample.StackIndex = _interner.CallStackIntern(frame, sample.StackIndex);
        /// sample.Metric = stopData.DurationNs / NanosInMillisecond;
        /// </code>
        /// </summary>
        protected abstract void RecordAdditionalStopData(StackSourceSample sample, TraceEvent data);
        /// <summary>
        /// If Stop arrives before Start it will be signalled by <see cref="BrokenEventNoStart"/> frame.
        /// <para>
        /// However, there's a special case at the beginning of the trace where we could miss the Start because
        /// it happened before we started the recording.
        /// </para>
        /// In this case, <c>sample.Metric</c> will be set to <c>stop.TimeStampRelativeMSec</c>
        /// like if the Start arrived at the beginning of the trace
        /// </summary>
        protected abstract void RecordAdditionalDataOnStopWithoutStart(StackSourceSample sample);
        /// <summary>
        /// If Start arrives but there's no Stop it will be signalled by <see cref="BrokenEventNoStop"/> frame.
        /// <para>
        /// However, there's a special case at the end of the trace where we could miss the Stop because
        /// it happened after we finished the recording.
        /// </para>
        /// In this case, <c>sample.Metric</c> will be set to <c>_eventLog.SessionEndTimeRelativeMSec - start.TimeStampRelativeMSec</c>
        /// like if the Stop happened at the end of the trace.
        /// </summary>
        protected abstract void RecordAdditionalDataOnStartWithoutStop(StackSourceSample sample);

        public void GenerateStacks()
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
                    RecordAdditionalDataOnStartWithoutStop(sample);
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
                RecordAdditionalDataOnStopWithoutStart(sample);
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