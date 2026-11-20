// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Diagnostics.Tracing.Computers;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;

using Xunit;

using static Microsoft.Diagnostics.Tracing.Computers.AsyncProfilerRuntimeSupport;

namespace TraceEventTests
{
    /// <summary>
    /// Tests state-machine (V1) completion tails, where a sampled method is finishing after its recorded completion.
    /// </summary>
    public partial class AsyncProfilerCallStackStitcherTests
    {
        [Theory]
        [InlineData(false, 0, 2)]
        [InlineData(true, 0, 2)]
        [InlineData(true, 2, 0)]
        [InlineData(true, 1, 1)]
        public void V1_CompletionTail_PreservesFinishingMethodAndSampledPrefix(
            bool methodEvents, int normal, int exceptional)
        {
            var s = new Scenario();
            if (methodEvents)
            {
                s.MarkMethodCompletionObserved(AsyncCallstackKind.StateMachineAsync);
            }
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.StateMachineAsync,
                new[] { 500, 501, 502, 503, 504 }, new[] { 30, 31, 32, 33, 34 },
                completions: Completed(normal), exceptionCompletions: Unwound(exceptional));
            var sync = new[]
            {
                s.Sync(40, 90), s.Sync(41, 31), s.Sync(42, 91), s.Sync(43, 30),
                s.Boundary(44, s_v1Dispatcher), s.Sync(45, 99),
            };
            AsyncProfilerStitchResult result = s.Run(sync, new[] { segment });

            Assert.Equal(new[] { Scenario.CA(40), Scenario.CA(41), Scenario.CA(502), Scenario.CA(503),
                Scenario.CA(504), Scenario.CA(45) }, result.Frames.Select(frame => frame.CodeAddress));
            AsyncProfilerStitchedFrame finishing = result.Frames[1];
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.V1CompletedBody, finishing.Origin);
            Assert.Equal(1, finishing.SegmentFrameIndex);
            Assert.Equal(Scenario.MI(31), finishing.Method);
            Assert.Same(segment, finishing.Context);
            Assert.Equal(AsyncProfilerStitchedFramePresentation.LogicalStateMachineMethod, finishing.Presentation);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget, result.Frames[2].Origin);
            Assert.Equal(1, result.Diagnostics.V1CompletionTailUsed);
            Assert.Equal(0, result.Diagnostics.V1CompletionAlignmentAmbiguous);
            Assert.Equal(methodEvents ? 0 : 1, result.Diagnostics.V1InlineFallbackUsed);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void V1_CompletionTail_LastMethodCanStillBePhysicallyFinishing(bool methodEvents)
        {
            var s = new Scenario();
            if (methodEvents)
            {
                s.MarkMethodCompletionObserved(AsyncCallstackKind.StateMachineAsync);
            }
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.StateMachineAsync,
                new[] { 500 }, new[] { 30 },
                completions: methodEvents ? Completed(1) : null,
                exceptionCompletions: methodEvents ? null : Unwound(1));
            AsyncProfilerStitchResult result = s.Run(
                new[] { s.Sync(40, 90), s.Sync(41, 30), s.Boundary(42, s_v1Dispatcher), s.Sync(43, 99) },
                new[] { segment });

            Assert.Equal(new[] { Scenario.CA(40), Scenario.CA(41), Scenario.CA(43) },
                result.Frames.Select(frame => frame.CodeAddress));
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.V1CompletedBody, result.Frames[1].Origin);
            Assert.DoesNotContain(result.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void V1_CompletionTail_NextPhysicalMethodIsRunning(bool methodEvents)
        {
            var s = new Scenario();
            if (methodEvents)
            {
                s.MarkMethodCompletionObserved(AsyncCallstackKind.StateMachineAsync);
            }
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.StateMachineAsync,
                new[] { 500, 501, 502, 503 }, new[] { 30, 31, 32, 33 },
                completions: methodEvents ? Completed(2) : null,
                exceptionCompletions: methodEvents ? null : Unwound(2));
            AsyncProfilerStitchResult result = s.Run(
                new[] { s.Sync(40, 90), s.Sync(41, 32), s.Boundary(42, s_v1Dispatcher), s.Sync(43, 99) },
                new[] { segment });

            Assert.Equal(new[] { Scenario.CA(40), Scenario.CA(41), Scenario.CA(503), Scenario.CA(43) },
                result.Frames.Select(frame => frame.CodeAddress));
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCurrent, result.Frames[1].Origin);
            Assert.Equal(0, result.Diagnostics.V1CompletionTailUsed);
        }

        [Theory]
        [InlineData("no-boundary")]
        [InlineData("repeated")]
        [InlineData("out-of-order")]
        [InlineData("unresolved")]
        [InlineData("no-records")]
        [InlineData("future-records")]
        public void V1_CompletionTail_UnverifiedLayoutsDoNotAcquireCompletingStatus(string guard)
        {
            var s = new Scenario();
            s.MarkMethodCompletionObserved(AsyncCallstackKind.StateMachineAsync);
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.StateMachineAsync,
                new[] { 500, guard == "unresolved" ? -1 : 501, 502, 503 },
                new[] { guard == "repeated" ? 31 : 30, 31, 32, 33 },
                completions: guard == "no-records" ? null :
                    new[] { new AsyncProfilerCallStack.CompletionDelta(guard == "future-records" ? Qpc + 1 : Qpc, 2) });
            var sync = new List<AsyncProfilerStitchSyncFrame>
            {
                s.Sync(40, 90), s.Sync(41, guard == "out-of-order" ? 30 : 31),
                s.Sync(42, guard == "out-of-order" ? 31 : 30),
            };
            if (guard != "no-boundary")
            {
                sync.Add(s.Boundary(43, s_v1Dispatcher));
            }
            sync.Add(s.Sync(44, 99));
            AsyncProfilerStitchResult result = s.Run(sync, new[] { segment });

            Assert.DoesNotContain(result.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody);
            Assert.Equal(0, result.Diagnostics.V1CompletionTailUsed);
        }

        [Fact]
        public void V1_CompletionTail_PartialRecordsRetainPhysicalCurrentWithoutDoubleCounting()
        {
            var s = new Scenario();
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.StateMachineAsync,
                new[] { 500, 501, 502, 503, 504 }, new[] { 30, 31, 32, 33, 34 },
                exceptionCompletions: Unwound(1));
            var sync = new[]
            {
                s.Sync(40, 90), s.Sync(41, 32), s.Sync(42, 31), s.Sync(43, 30),
                s.Boundary(44, s_v1Dispatcher), s.Sync(45, 99),
            };
            AsyncProfilerStitchResult result = s.Run(sync, new[] { segment }, trace: true);

            Assert.Equal(new[] { Scenario.CA(40), Scenario.CA(41), Scenario.CA(503), Scenario.CA(504), Scenario.CA(45) },
                result.Frames.Select(frame => frame.CodeAddress));
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCurrent, result.Frames[1].Origin);
            Assert.Equal(2, result.Frames[1].SegmentFrameIndex);
            Assert.DoesNotContain(result.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody);
            Assert.Equal(0, result.Diagnostics.V1CompletionAlignmentAmbiguous);
        }

        [Fact]
        public void V1_CompletionTail_RecordsBeyondPhysicalMatchesDoNotInventFinishingAnchor()
        {
            var s = new Scenario();
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.StateMachineAsync,
                new[] { 500, 501, 502, 503, 504 }, new[] { 30, 31, 32, 33, 34 },
                exceptionCompletions: Unwound(3));
            var sync = new[]
            {
                s.Sync(40, 90), s.Sync(41, 31), s.Sync(42, 30),
                s.Boundary(43, s_v1Dispatcher), s.Sync(44, 99),
            };
            AsyncProfilerStitchResult result = s.Run(sync, new[] { segment });

            Assert.Equal(sync.Take(4).Select(frame => frame.CodeAddress),
                result.Frames.Take(4).Select(frame => frame.CodeAddress));
            Assert.DoesNotContain(result.Frames, frame =>
                frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody || frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
            Assert.Equal(1, result.Diagnostics.V1CompletionAlignmentAmbiguous);
            Assert.Equal(3, result.Frames[4].SegmentFrameIndex);
        }

        [Fact]
        public void V1_CompletionTail_UnwindAfterSampleDoesNotRetrospectivelyMarkFrameCompleted()
        {
            var s = new Scenario();
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.StateMachineAsync,
                new[] { 500, 501, 502 }, new[] { 30, 31, 32 },
                exceptionCompletions: new[]
                {
                    new AsyncProfilerCallStack.CompletionDelta(Qpc - 1, 1),
                    new AsyncProfilerCallStack.CompletionDelta(Qpc + 1, 1),
                });
            var sync = new[]
            {
                s.Sync(40, 90), s.Sync(41, 31), s.Sync(42, 91), s.Sync(43, 30),
                s.Boundary(44, s_v1Dispatcher), s.Sync(45, 99),
            };
            AsyncProfilerStitchResult atSample = s.Run(sync, new[] { segment });
            AsyncProfilerStitchResult afterUnwind = AsyncProfilerCallStackStitcher.Stitch(
                sync, new[] { segment }, Qpc + 1, s.Classify, s.MethodCompletionObserved, s.MethodOf);

            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCurrent, atSample.Frames[1].Origin);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.V1CompletedBody, afterUnwind.Frames[1].Origin);
            Assert.Equal(Scenario.CA(41), atSample.Frames[1].CodeAddress);
            Assert.Equal(Scenario.CA(41), afterUnwind.Frames[1].CodeAddress);
            Assert.Equal(1, atSample.Frames[1].SegmentFrameIndex);
            Assert.Equal(1, afterUnwind.Frames[1].SegmentFrameIndex);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCaller, atSample.Frames[2].Origin);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget, afterUnwind.Frames[2].Origin);
            Assert.Equal(2, afterUnwind.Frames[2].SegmentFrameIndex);
        }

        [Fact]
        public void V1_CompletionTail_RecursiveCompletedCopyIsNotMistakenForRunningParent()
        {
            var s = new Scenario();
            s.MarkMethodCompletionObserved(AsyncCallstackKind.StateMachineAsync);
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.StateMachineAsync,
                new[] { 500, 501, 502 }, new[] { 30, 30, 31 }, completions: Completed(1));
            AsyncProfilerStitchResult result = s.Run(
                new[] { s.Sync(40, 90), s.Sync(41, 30), s.Boundary(42, s_v1Dispatcher), s.Sync(43, 99) },
                new[] { segment });

            Assert.DoesNotContain(result.Frames, frame =>
                frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent || frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody);
            Assert.Equal(Scenario.CA(41), result.Frames[1].CodeAddress);
            Assert.Equal(1, result.Diagnostics.V1CompletionAlignmentAmbiguous);
        }

        [Fact]
        public void V1_CompletionTail_MissingExceptionalChildStillAnchorsFinishingParent()
        {
            var s = new Scenario();
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.StateMachineAsync,
                new[] { 500, 501, 502, 503 }, new[] { 30, 31, 32, 33 },
                exceptionCompletions: Unwound(2));
            AsyncProfilerStitchResult result = s.Run(
                new[] { s.Sync(40, 90), s.Sync(41, 31), s.Boundary(42, s_v1Dispatcher), s.Sync(43, 99) },
                new[] { segment });

            Assert.Equal(AsyncProfilerStitchedFrameOrigin.V1CompletedBody, result.Frames[1].Origin);
            Assert.Equal(1, result.Frames[1].SegmentFrameIndex);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget, result.Frames[2].Origin);
            Assert.Equal(2, result.Frames[2].SegmentFrameIndex);
        }
    }
}
