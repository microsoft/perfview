using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Stacks;

namespace Microsoft.Diagnostics.Tracing.Computers
{
    public class WaitHandleWaitTimeComputer
    {
        private readonly TraceLog _eventLog;
        private readonly MutableTraceEventStackSource _stackSource;
        private readonly StackSourceSample[] _samplesPerThread;

        private const double NanosInMillisecond = 1000 * 1000;

        public WaitHandleWaitTimeComputer(TraceLog eventLog, MutableTraceEventStackSource stackSource)
        {
            _eventLog = eventLog;
            _stackSource = stackSource;
            _samplesPerThread = new StackSourceSample[eventLog.Threads.Count];
        }

        public void GenerateWaitTimeStacks()
        {
            var eventSource = _eventLog.Events.GetSource();
            
            eventSource.Clr.WaitHandleWaitStart += OnWaitHandleWaitStart;
            eventSource.Clr.WaitHandleWaitStop += OnWaitHandleWaitStop;

            eventSource.Process();
            
            _stackSource.DoneAddingSamples();
        }

        private void OnWaitHandleWaitStart(WaitHandleWaitStartTraceData startData)
        {
            var threadIndex = (int) startData.Thread().ThreadIndex;
            var sample = _samplesPerThread[threadIndex];
            
            if (sample == null)
            {
                _samplesPerThread[threadIndex] = sample = new StackSourceSample(_stackSource);
                sample.Count = 1;
            }

            sample.TimeRelativeMSec = startData.TimeStampRelativeMSec;
            sample.StackIndex = _stackSource.GetCallStack(startData.CallStackIndex(), startData);
            sample.StackIndex = _stackSource.Interner.CallStackIntern(_stackSource.Interner.FrameIntern($"EventData WaitSource {startData.WaitSource}"), sample.StackIndex);
        }

        private void OnWaitHandleWaitStop(WaitHandleWaitStopTraceData stopData)
        {
            var threadIndex = (int) stopData.Thread().ThreadIndex;
            var sample = _samplesPerThread[threadIndex];
            
            if (sample == null || (sample.TimeRelativeMSec == 0 && sample.StackIndex == StackSourceCallStackIndex.Invalid))
            {
                // no corresponding start event
                // or start event was missing for this stop (we got start-stop-stop sequence for this thread)
                return;
            }

            var durationMs = stopData.TimeStampRelativeMSec - sample.TimeRelativeMSec;
            sample.Metric = (float)durationMs;
            sample.StackIndex = _stackSource.Interner.CallStackIntern(_stackSource.Interner.FrameIntern($"EventData DurationNs {durationMs*NanosInMillisecond:N0}"), sample.StackIndex);

            _stackSource.AddSample(sample);
            sample.StackIndex = StackSourceCallStackIndex.Invalid;
            sample.TimeRelativeMSec = 0;
            sample.Metric = 0;
        }
    }
}