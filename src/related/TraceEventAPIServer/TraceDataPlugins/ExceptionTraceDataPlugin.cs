namespace TraceEventAPIServer.TraceDataPlugins
{
    using System;
    using Microsoft.Diagnostics.Tracing.Etlx;
    using Microsoft.Diagnostics.Tracing.Parsers;
    using Microsoft.Diagnostics.Tracing.Parsers.Clr;
    using Microsoft.Diagnostics.Tracing.Stacks;

    public sealed class ExceptionTraceDataPlugin : ITraceDataPlugin
    {
        public string Type => "Exceptions";

        public Func<CallTreeNodeBase, bool> SummaryPredicate => SummaryPredicateFunc;

        public StackSource GetStackSource(TraceEvents events)
        {
            if (events == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(events));
            }

            var stackSource = new MutableTraceEventStackSource(events.Log) { ShowUnknownAddresses = true };
            var eventSource = events.GetSource();

            var sample = new StackSourceSample(stackSource);

            var clrTraceEventParser = new ClrTraceEventParser(eventSource);

            clrTraceEventParser.ExceptionStart += delegate (ExceptionTraceData data)
            {
                sample.Metric = 1;
                sample.TimeRelativeMSec = data.TimeStampRelativeMSec;

                // Create a call stack that ends with the 'throw'
                var nodeName = "Throw(" + data.ExceptionType + ") " + data.ExceptionMessage;
                var nodeIndex = stackSource.Interner.FrameIntern(nodeName);
                sample.StackIndex = stackSource.Interner.CallStackIntern(nodeIndex, stackSource.GetCallStack(data.CallStackIndex(), data));
                stackSource.AddSample(sample);
            };

            eventSource.Process();
            return stackSource;
        }

        private static bool SummaryPredicateFunc(CallTreeNodeBase node)
        {
            return node.Name.StartsWith("Throw(");
        }
    }
}