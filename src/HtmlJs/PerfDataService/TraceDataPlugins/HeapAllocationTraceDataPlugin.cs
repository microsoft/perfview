namespace PerfDataService.TraceDataPlugins
{
    using System;
    using Microsoft.Diagnostics.Tracing.Etlx;
    using Microsoft.Diagnostics.Tracing.Parsers;
    using Microsoft.Diagnostics.Tracing.Parsers.Clr;
    using Microsoft.Diagnostics.Tracing.Stacks;

    public sealed class HeapAllocationTraceDataPlugin : ITraceDataPlugin
    {
        public string Type => "Memory";

        public Func<CallTreeNodeBase, bool> SummaryPredicate => SummaryPredicateFunc;

        public StackSource GetStackSource(TraceEvents events)
        {
            if (events == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(events));
            }

            var stackSource = new MutableTraceEventStackSource(events.Log);
            var eventSource = events.GetSource();

            var sample = new StackSourceSample(stackSource);

            var clrTraceEventParser = new ClrTraceEventParser(eventSource);

            clrTraceEventParser.GCAllocationTick += delegate (GCAllocationTickTraceData data)
            {
                var size = data.AllocationAmount64;
                sample.Metric = size;
                sample.Count = 1;
                sample.TimeRelativeMSec = data.TimeStampRelativeMSec;

                sample.StackIndex = stackSource.Interner.CallStackIntern(stackSource.Interner.FrameIntern("Type " + data.TypeName), stackSource.GetCallStack(data.CallStackIndex(), data));
                if (data.AllocationKind == GCAllocationKind.Large)
                {
                    sample.StackIndex = stackSource.Interner.CallStackIntern(stackSource.Interner.FrameIntern("LargeObject"), sample.StackIndex);
                }

                stackSource.AddSample(sample);
            };

            eventSource.Process();
            return stackSource;
        }

        private static bool SummaryPredicateFunc(CallTreeNodeBase node)
        {
            return node.Name.StartsWith("Type ");
        }
    }
}