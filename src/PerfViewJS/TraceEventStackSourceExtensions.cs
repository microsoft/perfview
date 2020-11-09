// <copyright file="TraceEventStackSourceExtensions.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace PerfViewJS
{
    using System;
    using Microsoft.Diagnostics.Tracing;
    using Microsoft.Diagnostics.Tracing.Etlx;
    using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
    using Microsoft.Diagnostics.Tracing.Stacks;

    internal static class TraceEventStackSourceExtensions
    {
        public static StackSource CPUStacks(this TraceEvents events, TraceProcess process = null, Predicate<TraceEvent> predicate = null)
        {
            // optimization only
            if (process != null)
            {
                var start = Math.Max(events.StartTimeRelativeMSec, process.StartTimeRelativeMsec);
                var end = Math.Min(events.EndTimeRelativeMSec, process.EndTimeRelativeMsec);
                events = events.FilterByTime(start, end);
                events = events.Filter(x => (predicate == null || predicate(x)) && x is SampledProfileTraceData && x.ProcessID == process.ProcessID);
            }
            else
            {
                events = events.Filter(x => (predicate == null || predicate(x)) && x is SampledProfileTraceData && x.ProcessID != 0);  // TODO: Is it really correc that x.ProcessID != 0 should be there? What if we want see these?
            }

            var traceStackSource = new TraceEventStackSource(events) { ShowUnknownAddresses = true };

            return CopyStackSource.Clone(traceStackSource);
        }

        public static StackSource SingleEventTypeStack(this TraceEvents events, TraceProcess process = null, Predicate<TraceEvent> predicate = null)
        {
            // optimization only
            if (process != null)
            {
                var start = Math.Max(events.StartTimeRelativeMSec, process.StartTimeRelativeMsec);
                var end = Math.Min(events.EndTimeRelativeMSec, process.EndTimeRelativeMsec);
                events = events.FilterByTime(start, end);
                events = events.Filter(x => (predicate == null || predicate(x)) && x.ProcessID == process.ProcessID);
            }
            else
            {
                events = events.Filter(x => (predicate == null || predicate(x)) && x.ProcessID != 0); // TODO: Is it really correc that x.ProcessID != 0 should be there? What if we want see these?
            }

            var traceStackSource = new TraceEventStackSource(events) { ShowUnknownAddresses = true };

            return CopyStackSource.Clone(traceStackSource);
        }

        public static StackSource AnyStacks(this TraceEvents events, TraceProcess process = null, Predicate<TraceEvent> predicate = null)
        {
            // optimization only
            if (process != null)
            {
                var start = Math.Max(events.StartTimeRelativeMSec, process.StartTimeRelativeMsec);
                var end = Math.Min(events.EndTimeRelativeMSec, process.EndTimeRelativeMsec);
                events = events.FilterByTime(start, end);
                events = events.Filter(x => (predicate == null || predicate(x)) && x.ProcessID == process.ProcessID);
            }
            else
            {
                events = events.Filter(x => (predicate == null || predicate(x)) && x.ProcessID != 0); // TODO: Is it really correc that x.ProcessID != 0 should be there? What if we want see these?
            }

            var stackSource = new MutableTraceEventStackSource(events.Log) { ShowUnknownAddresses = true };
            var sample = new StackSourceSample(stackSource);
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

        public static StackSource Exceptions(this TraceEvents events, TraceProcess process = null, Predicate<TraceEvent> predicate = null)
        {
            // optimization only
            if (process != null)
            {
                var start = Math.Max(events.StartTimeRelativeMSec, process.StartTimeRelativeMsec);
                var end = Math.Min(events.EndTimeRelativeMSec, process.EndTimeRelativeMsec);
                events = events.FilterByTime(start, end);
                events = events.Filter(x => (predicate == null || predicate(x)) && x.ProcessID == process.ProcessID);
            }
            else
            {
                events = events.Filter(x => (predicate == null || predicate(x)) && x.ProcessID != 0); // TODO: Is it really correc that x.ProcessID != 0 should be there? What if we want see these?
            }

            var eventSource = events.GetSource();
            var stackSource = new MutableTraceEventStackSource(events.Log) { ShowUnknownAddresses = true };
            var sample = new StackSourceSample(stackSource);

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
