// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PerfViewJS
{
    using System;
    using Microsoft.Diagnostics.Tracing;
    using Microsoft.Diagnostics.Tracing.Etlx;
    using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
    using Microsoft.Diagnostics.Tracing.Stacks;

    internal static class TraceEventStackSourceExtensions
    {
        public static StackSource CPUStacks(this TraceLog eventLog, TraceProcess process = null, bool showUnknownAddresses = false, Predicate<TraceEvent> predicate = null)
        {
            TraceEvents events = process == null ? eventLog.Events.Filter(x => (predicate == null || predicate(x)) && x is SampledProfileTraceData && x.ProcessID != 0) : process.EventsInProcess.Filter(x => (predicate == null || predicate(x)) && x is SampledProfileTraceData);

            var traceStackSource = new TraceEventStackSource(events)
            {
                ShowUnknownAddresses = showUnknownAddresses
            };

            return traceStackSource;
        }

        public static StackSource AnyStacks(this TraceLog eventLog, TraceProcess process = null, bool showUnknownAddresses = false, Predicate<TraceEvent> predicate = null)
        {
            var stackSource = new MutableTraceEventStackSource(eventLog);
            var sample = new StackSourceSample(stackSource);

            TraceEvents events = process == null ? eventLog.Events.Filter(x => (predicate == null || predicate(x)) && x.ProcessID != 0) : process.EventsInProcess.Filter(x => predicate == null || predicate(x));
            var eventSource = events.GetSource();
            eventSource.AllEvents += data =>
            {
                var callStackIdx = data.CallStackIndex();
                StackSourceCallStackIndex stackIndex = callStackIdx != CallStackIndex.Invalid ? stackSource.GetCallStack(callStackIdx, data) : StackSourceCallStackIndex.Invalid;

                var eventNodeName = "Event " + data.ProviderName + "/" + data.EventName;
                stackIndex = stackSource.Interner.CallStackIntern(stackSource.Interner.FrameIntern(eventNodeName), stackIndex);
                sample.StackIndex = stackIndex;
                sample.TimeRelativeMSec = data.TimeStampRelativeMSec;
                sample.Metric = 1;
                stackSource.AddSample(sample);
            };

            eventSource.Process();

            stackSource.DoneAddingSamples();

            return stackSource;
        }

        public static StackSource Exceptions(this TraceLog eventLog, TraceProcess process = null, bool showUnknownAddresses = false, Predicate<TraceEvent> predicate = null)
        {
            var stackSource = new MutableTraceEventStackSource(eventLog);
            var sample = new StackSourceSample(stackSource);
            TraceEvents events = process == null ? eventLog.Events.Filter(x => (predicate == null || predicate(x)) && x.ProcessID != 0) : process.EventsInProcess.Filter(x => predicate == null || predicate(x));

            var eventSource = events.GetSource();

            eventSource.Clr.ExceptionStart += data =>
            {
                sample.Metric = 1;
                sample.TimeRelativeMSec = data.TimeStampRelativeMSec;

                // Create a call stack that ends with the 'throw'
                var nodeName = "Throw(" + data.ExceptionType + ") " + data.ExceptionMessage;
                var nodeIndex = stackSource.Interner.FrameIntern(nodeName);
                sample.StackIndex = stackSource.Interner.CallStackIntern(nodeIndex, stackSource.GetCallStack(data.CallStackIndex(), data));
                stackSource.AddSample(sample);
            };

            eventSource.Kernel.MemoryAccessViolation += data =>
            {
                sample.Metric = 1;
                sample.TimeRelativeMSec = data.TimeStampRelativeMSec;

                // Create a call stack that ends with the 'throw'
                var nodeName = "AccessViolation(ADDR=" + data.VirtualAddress.ToString("x") + ")";
                var nodeIndex = stackSource.Interner.FrameIntern(nodeName);
                sample.StackIndex = stackSource.Interner.CallStackIntern(nodeIndex, stackSource.GetCallStack(data.CallStackIndex(), data));
                stackSource.AddSample(sample);
            };

            eventSource.Process();

            stackSource.DoneAddingSamples();

            return stackSource;
        }
    }
}
