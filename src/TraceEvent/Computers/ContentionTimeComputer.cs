using System.Collections.Generic;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Stacks;

namespace Microsoft.Diagnostics.Tracing.Computers
{
    public class ContentionTimeComputer
    {
        private readonly TraceLog _eventLog;
        private readonly MutableTraceEventStackSource _stackSource;
        private readonly Dictionary<int, StackSourceSample> _samplesPerThread;

        private const double NanosInMillisecond = 1000 * 1000;

        public ContentionTimeComputer(TraceLog eventLog, MutableTraceEventStackSource stackSource)
        {
            _eventLog = eventLog;
            _stackSource = stackSource;
            _samplesPerThread = new Dictionary<int, StackSourceSample>();
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
            if (!_samplesPerThread.TryGetValue(startData.ThreadID, out var sample))
            {
                _samplesPerThread[startData.ThreadID] = sample = new StackSourceSample(_stackSource);
                sample.Count = 1;
            }

            sample.TimeRelativeMSec = startData.TimeStampRelativeMSec;
            var callStackIdx = startData.CallStackIndex();
            var stackIndex = _stackSource.GetCallStack(callStackIdx, startData);
            sample.StackIndex = stackIndex;
        }

        private void OnContentionStop(ContentionStopTraceData stopData)
        {
            if (!_samplesPerThread.TryGetValue(stopData.ThreadID, out var sample))
            {
                // no corresponding start event
                return;
            }

            sample.Metric = (float) (stopData.DurationNs / NanosInMillisecond);
            sample.StackIndex = _stackSource.Interner.CallStackIntern(_stackSource.Interner.FrameIntern($"EventData DurationNs {stopData.DurationNs:N0}"), sample.StackIndex);

            _stackSource.AddSample(sample);
        }
    }
}