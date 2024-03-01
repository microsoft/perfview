using System.Collections.Generic;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Stacks;

namespace Microsoft.Diagnostics.Tracing.Computers
{
    public class WaitHandleWaitTimeComputer
    {
        private readonly TraceLog _eventLog;
        private readonly MutableTraceEventStackSource _stackSource;
        private readonly Dictionary<int, StackSourceSample> _samplesPerThread;

        private const double NanosInMillisecond = 1000 * 1000;

        public WaitHandleWaitTimeComputer(TraceLog eventLog, MutableTraceEventStackSource stackSource)
        {
            _eventLog = eventLog;
            _stackSource = stackSource;
            _samplesPerThread = new Dictionary<int, StackSourceSample>();
        }

        public void GenerateContentionTimeStacks()
        {
            var eventSource = _eventLog.Events.GetSource();
            
            eventSource.Clr.WaitHandleWaitStart += OnWaitHandleWaitStart;
            eventSource.Clr.WaitHandleWaitStop += OnWaitHandleWaitStop;

            eventSource.Process();
            
            _stackSource.DoneAddingSamples();
        }

        private void OnWaitHandleWaitStart(WaitHandleWaitStartTraceData startData)
        {
            if (!_samplesPerThread.TryGetValue(startData.ThreadID, out var sample))
            {
                _samplesPerThread[startData.ThreadID] = sample = new StackSourceSample(_stackSource);
                sample.Count = 1;
            }

            sample.TimeRelativeMSec = startData.TimeStampRelativeMSec;
            var callStackIdx = startData.CallStackIndex();
            sample.StackIndex = _stackSource.GetCallStack(callStackIdx, startData);
            sample.StackIndex = _stackSource.Interner.CallStackIntern(_stackSource.Interner.FrameIntern($"EventData WaitSource {startData.WaitSource}"), sample.StackIndex);
        }

        private void OnWaitHandleWaitStop(WaitHandleWaitStopTraceData stopData)
        {
            if (!_samplesPerThread.TryGetValue(stopData.ThreadID, out var sample))
            {
                // no corresponding start event
                return;
            }

            var durationMs = stopData.TimeStampRelativeMSec - sample.TimeRelativeMSec;
            sample.Metric = (float)durationMs;
            sample.StackIndex = _stackSource.Interner.CallStackIntern(_stackSource.Interner.FrameIntern($"EventData DurationNs {durationMs*NanosInMillisecond:N0}"), sample.StackIndex);

            _stackSource.AddSample(sample);
        }
    }
}