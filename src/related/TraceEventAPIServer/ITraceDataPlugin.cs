namespace TraceEventAPIServer
{
    using System;
    using Microsoft.Diagnostics.Tracing.Etlx;
    using Microsoft.Diagnostics.Tracing.Stacks;

    public interface ITraceDataPlugin
    {
        string Type { get; }

        StackSource GetStackSource(TraceEvents eventSource);

        Func<CallTreeNodeBase, bool> SummaryPredicate { get; }
    }
}