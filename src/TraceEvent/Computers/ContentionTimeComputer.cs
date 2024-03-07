using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Stacks;

namespace Microsoft.Diagnostics.Tracing.Computers
{
    public class ContentionTimeComputer
    {
        private readonly TraceLog _eventLog;
        private readonly MutableTraceEventStackSource _stackSource;
        private readonly StackSourceSample[] _samplesPerThread;

        private const double NanosInMillisecond = 1000 * 1000;

        public ContentionTimeComputer(TraceLog eventLog, MutableTraceEventStackSource stackSource)
        {
            _eventLog = eventLog;
            _stackSource = stackSource;
            _samplesPerThread = new StackSourceSample[eventLog.Threads.Count];
        }

        public void GenerateContentionTimeStacks()
        {
            var eventSource = _eventLog.Events.GetSource();
            
            eventSource.Clr.ContentionStart += OnContentionStart;
            eventSource.Clr.ContentionStop += OnContentionStop;

            eventSource.Process();
            
            _stackSource.DoneAddingSamples();
        }

        private void OnContentionStart(ContentionStartTraceData startData)
        {
            var threadIndex = (int) startData.Thread().ThreadIndex;
            var sample = _samplesPerThread[threadIndex];
            
            if (sample == null)
            {
                _samplesPerThread[threadIndex] = sample = new StackSourceSample(_stackSource);
                sample.Count = 1;
            }

            sample.TimeRelativeMSec = startData.TimeStampRelativeMSec;
            var callStackIdx = startData.CallStackIndex();
            var stackIndex = _stackSource.GetCallStack(callStackIdx, startData);
            sample.StackIndex = stackIndex;
        }

        private void OnContentionStop(ContentionStopTraceData stopData)
        {
            var threadIndex = (int) stopData.Thread().ThreadIndex;
            var sample = _samplesPerThread[threadIndex];
            
            if (sample == null || (sample.TimeRelativeMSec == 0 && sample.StackIndex == StackSourceCallStackIndex.Invalid))
            {
                // no corresponding start event
                // or start event was missing for this stop (we got start-stop-stop sequence for this thread)
                return;
            }

            sample.Metric = (float) (stopData.DurationNs / NanosInMillisecond);
            sample.StackIndex = _stackSource.Interner.CallStackIntern(_stackSource.Interner.FrameIntern($"EventData DurationNs {stopData.DurationNs:N0}"), sample.StackIndex);

            _stackSource.AddSample(sample);
            sample.StackIndex = StackSourceCallStackIndex.Invalid;
            sample.TimeRelativeMSec = 0;
            sample.Metric = 0;
        }
    }
}