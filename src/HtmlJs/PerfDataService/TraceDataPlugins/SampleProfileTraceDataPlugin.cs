namespace PerfDataService.TraceDataPlugins
{
    using System;
    using Microsoft.Diagnostics.Tracing.Etlx;
    using Microsoft.Diagnostics.Tracing.Parsers;
    using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
    using Microsoft.Diagnostics.Tracing.Stacks;

    public sealed class SampleProfileTraceDataPlugin : ITraceDataPlugin
    {
        public string Type => "CPU";

        public Func<CallTreeNodeBase, bool> SummaryPredicate => SummaryPredicateFunc;

        public StackSource GetStackSource(TraceEvents events)
        {
            if (events == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(events));
            }

            TraceLog log = events.Log;
            var stackSource = new MutableTraceEventStackSource(log) { /* ShowUnknownAddresses = true */ };
            var eventSource = events.GetSource();

            var sample = new StackSourceSample(stackSource);

            var kernelTraceEventParser = new KernelTraceEventParser(eventSource);

            kernelTraceEventParser.PerfInfoSample += delegate (SampledProfileTraceData data)
            {
                sample.Metric = events.Log.SampleProfileInterval.Milliseconds;
                sample.TimeRelativeMSec = data.TimeStampRelativeMSec;
                sample.StackIndex = stackSource.GetCallStack(data.CallStackIndex(), data);
                stackSource.AddSample(sample);
            };

            eventSource.Process();
            return stackSource;
        }

        private static bool SummaryPredicateFunc(CallTreeNodeBase node)
        {
            return true;
        }
    }
}