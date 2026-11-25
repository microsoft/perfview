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
    /// Tests nested and mixed V1/V2 call stacks, including missing inner boundaries and synchronous bridges.
    /// </summary>
    public partial class AsyncProfilerCallStackStitcherTests
    {
        [Fact]
        public void NestedV2ResumeStubs_PreserveBothCurrentFramesAndTheSynchronousBridge()
        {
            var s = new Scenario();
            AsyncProfilerCallStack inner = s.Segment(AsyncCallstackKind.RuntimeAsync,
                new[] { 500, 501 }, new[] { 30, 31 });
            AsyncProfilerCallStack outer = s.Segment(AsyncCallstackKind.RuntimeAsync,
                new[] { 600, 601 }, new[] { 40, 41 });
            var sync = new[]
            {
                s.Sync(10, 30),
                s.Sync(11, 90, AsyncProfilerStitchSyncFrameKind.V2ResumeStub),
                s.Boundary(12, Wrapper(0)),
                s.Sync(13, 91),
                s.Sync(14, 40),
                s.Sync(15, 92, AsyncProfilerStitchSyncFrameKind.V2ResumeStub),
                s.Boundary(16, Wrapper(0)),
                s.Sync(17, 99),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { inner, outer });

            Assert.Equal(new[] { 10, 501, 13, 14, 601, 17 }.Select(Scenario.CA),
                result.Frames.Select(frame => frame.CodeAddress));
            Assert.Equal(new[] { inner, outer }, result.Frames
                .Where(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent).Select(frame => frame.Context));
            Assert.Equal(0, result.Diagnostics.AdjacencyMismatches);
            Assert.Equal(2, result.Diagnostics.V2InfrastructureFramesCollapsed);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void MissingInnerV1Boundary_PreservesOuterV2CurrentThroughResumeStub(bool methodEvents)
        {
            var s = new Scenario();
            if (methodEvents)
            {
                s.MarkMethodCompletionObserved(AsyncCallstackKind.StateMachineAsync);
                s.MarkMethodCompletionObserved(AsyncCallstackKind.RuntimeAsync);
            }
            AsyncProfilerCallStack outer = s.Segment(AsyncCallstackKind.RuntimeAsync,
                new[] { 500, 501 }, new[] { 30, 41 });
            AsyncProfilerCallStack inner = s.Segment(AsyncCallstackKind.StateMachineAsync,
                new[] { 600, 601, 602 }, new[] { 30, 31, 32 },
                completions: methodEvents ? Completed(1) : null);
            var sync = new[]
            {
                s.Sync(10, 90),
                s.Sync(11, 31),
                s.Sync(12, 30),
                s.Sync(13, 30),
                s.Sync(14, 91, AsyncProfilerStitchSyncFrameKind.V2ResumeStub),
                s.Boundary(15, Wrapper(0)),
                s.Sync(16, 99),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { inner, outer });

            Assert.Equal(new[] { 10, 11, 602, 13, 501, 16 }.Select(Scenario.CA),
                result.Frames.Select(frame => frame.CodeAddress));
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCurrent, result.Frames[1].Origin);
            Assert.Same(inner, result.Frames[1].Context);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCurrent, result.Frames[3].Origin);
            Assert.Same(outer, result.Frames[3].Context);
            Assert.Equal(1, result.Diagnostics.BoundariesNotFound);
            Assert.Equal(0, result.Diagnostics.AdjacencyMismatches);
            Assert.Equal(1, result.Diagnostics.V2InfrastructureFramesCollapsed);
        }

        [Fact]
        public void NestedV2Segments_PromoteBothCurrentFramesAndPreserveSynchronousBridge()
        {
            // Two stacked V2 segments (each freshly resumed: completed=0, splice one async caller frame).
            var s = new Scenario();
            s.MarkMethodCompletionObserved(AsyncCallstackKind.RuntimeAsync);
            AsyncProfilerCallStack leaf = s.Segment(AsyncCallstackKind.RuntimeAsync,
                frameCodeAddrs: new[] { 700, 701 }, frameMethods: new[] { 10, 11 }, completions: Completed(0));
            AsyncProfilerCallStack root = s.Segment(AsyncCallstackKind.RuntimeAsync,
                frameCodeAddrs: new[] { 800, 801 }, frameMethods: new[] { 20, 21 }, completions: Completed(0));

            var sync = new[]
            {
                s.Sync(50, 10),                  // L current (frames[0] of leaf seg)
                s.Boundary(51, Wrapper(0)),      // leaf boundary
                s.Sync(55, 70),                  // synchronous bridge between the nested call stacks
                s.Sync(52, 20),                  // R current (frames[0] of root seg)
                s.Boundary(53, Wrapper(0)),      // root boundary
                s.Sync(54, 99),                  // root frame
            };

            // Both the physical frames and active call stacks are supplied leaf-to-root.
            AsyncProfilerStitchResult result = s.Run(sync, new[] { leaf, root });

            // Expected: [L0(AsyncCurrent), L1(Async), bridge(Sync),
            // R0(AsyncCurrent), R1(Async), root(Sync)].
            Assert.Equal(6, result.Frames.Count);
            Assert.Equal(Scenario.CA(50), result.Frames[0].CodeAddress);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCurrent, result.Frames[0].Origin);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCaller, result.Frames[1].Origin);
            Assert.Equal(Scenario.CA(701), result.Frames[1].CodeAddress);
            Assert.Equal(Scenario.CA(55), result.Frames[2].CodeAddress);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, result.Frames[2].Origin);
            Assert.Equal(Scenario.CA(52), result.Frames[3].CodeAddress);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCurrent, result.Frames[3].Origin);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCaller, result.Frames[4].Origin);
            Assert.Equal(Scenario.CA(801), result.Frames[4].CodeAddress);
            Assert.Equal(Scenario.CA(54), result.Frames[5].CodeAddress);
            Assert.Equal(2, result.Diagnostics.SegmentsProcessed);
            Assert.Equal(0, result.Diagnostics.BoundariesNotFound);

            Assert.Equal(2, result.SegmentPlacements.Count);
            Assert.Same(leaf, result.SegmentPlacements[0].CallStack);
            Assert.Equal(0, result.SegmentPlacements[0].LeafIndex);
            Assert.Equal(2, result.SegmentPlacements[0].RootIndexExclusive);
            Assert.Same(root, result.SegmentPlacements[1].CallStack);
            Assert.Equal(2, result.SegmentPlacements[1].LeafIndex);
            Assert.Equal(5, result.SegmentPlacements[1].RootIndexExclusive);
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, false, true)]
        [InlineData(false, true, false)]
        [InlineData(false, true, true)]
        [InlineData(true, false, false)]
        [InlineData(true, false, true)]
        [InlineData(true, true, false)]
        [InlineData(true, true, true)]
        public void MissingInnerV1Boundary_PreservesVerifiedOuterV2CallStack(
            bool carrier, bool methodEvents, bool sharedMethod)
        {
            var s = new Scenario();
            if (methodEvents)
            {
                s.MarkMethodCompletionObserved(AsyncCallstackKind.StateMachineAsync);
                s.MarkMethodCompletionObserved(AsyncCallstackKind.RuntimeAsync);
            }
            int outerMethod = sharedMethod ? 30 : 40;
            AsyncProfilerCallStack outer = s.Segment(AsyncCallstackKind.RuntimeAsync,
                new[] { 500, 501, 502, 503 }, new[] { 70, 71, outerMethod, 41 },
                completions: methodEvents ? Completed(2) : null);
            AsyncProfilerCallStack inner = s.Segment(AsyncCallstackKind.StateMachineAsync,
                new[] { 600, 601, 602, 603 }, new[] { 30, 31, 32, 33 },
                completions: methodEvents ? Completed(2) : null);
            var sync = new[]
            {
                s.Sync(10, 90),
                carrier ? s.BoxCarrier(11, 32) : s.Sync(11, 32),
                s.Sync(12, 31),
                s.Sync(13, 30),
                new AsyncProfilerStitchSyncFrame(Scenario.CA(18), MethodIndex.Invalid),
                s.Sync(19, 89),
                s.Sync(14, outerMethod),
                s.Boundary(15, Wrapper(methodEvents ? 0 : 2)),
                s.Boundary(16, s_dispatchContinuation),
                s.Sync(17, 99),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { inner, outer });

            Assert.Equal(
                (carrier ? new[] { 10, 11, 11, 603, 18, 19, 14, 503, 17 } :
                    new[] { 10, 11, 603, 18, 19, 14, 503, 17 }).Select(Scenario.CA),
                result.Frames.Select(frame => frame.CodeAddress));
            int innerIndex = carrier ? 2 : 1;
            Assert.Equal(carrier ? AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget : AsyncProfilerStitchedFrameOrigin.AsyncCurrent,
                result.Frames[innerIndex].Origin);
            Assert.Equal(carrier, result.Frames[innerIndex].IsStateMachineBoxCarrier);
            Assert.Same(inner, result.Frames[innerIndex].Context);
            Assert.Equal(2, result.Frames[innerIndex].SegmentFrameIndex);
            int outerIndex = carrier ? 6 : 5;
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCurrent, result.Frames[outerIndex].Origin);
            Assert.Same(outer, result.Frames[outerIndex].Context);
            Assert.Equal(2, result.Frames[outerIndex].SegmentFrameIndex);
            Assert.Equal(2, result.Diagnostics.SegmentsProcessed);
            Assert.Equal(1, result.Diagnostics.BoundariesNotFound);
            Assert.Equal(methodEvents ? 0 : 1, result.Diagnostics.V1TruncatedSuffixFallbackUsed);
            Assert.Equal(1, result.Diagnostics.V2InfrastructureFramesCollapsed);
            Assert.Equal(0, result.Diagnostics.IncompleteAsyncRootsNormalized);
            Assert.DoesNotContain(result.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot);
            Assert.Same(inner, result.SegmentPlacements[0].CallStack);
            Assert.Equal(carrier ? 4 : 3, result.SegmentPlacements[0].RootIndexExclusive);
            Assert.Same(outer, result.SegmentPlacements[1].CallStack);
            Assert.Equal(carrier ? 4 : 3, result.SegmentPlacements[1].LeafIndex);
            Assert.Equal(carrier ? 8 : 7, result.SegmentPlacements[1].RootIndexExclusive);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void MissingInnerV1Boundary_UnverifiedOuterAnchorDoesNotTrimThePhysicalSpan(bool unresolved)
        {
            var s = new Scenario();
            AsyncProfilerCallStack outer = s.Segment(AsyncCallstackKind.RuntimeAsync,
                new[] { 500, 501 }, new[] { 40, 41 });
            AsyncProfilerCallStack inner = s.Segment(AsyncCallstackKind.StateMachineAsync,
                new[] { 600, 601, 602, 603 }, new[] { 30, 31, 32, 33 });
            var sync = new[]
            {
                s.Sync(10, 90),
                s.Sync(11, 32),
                s.Sync(12, 31),
                s.Sync(13, 30),
                new AsyncProfilerStitchSyncFrame(Scenario.CA(14), unresolved ? MethodIndex.Invalid : Scenario.MI(89)),
                s.Boundary(15, Wrapper(0)),
                s.Sync(17, 99),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { inner, outer });

            Assert.Equal(sync.Select(frame => frame.CodeAddress),
                result.Frames.Take(sync.Length).Select(frame => frame.CodeAddress));
            Assert.DoesNotContain(result.Frames, frame =>
                frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent && ReferenceEquals(frame.Context, outer));
            Assert.Equal(0, result.Diagnostics.IncompleteAsyncRootsNormalized);
        }

        [Fact]
        public void MissingInnerV1Boundaries_PreserveTheEnclosingV2AcrossMultipleCallStacks()
        {
            var s = new Scenario();
            AsyncProfilerCallStack outer = s.Segment(AsyncCallstackKind.RuntimeAsync,
                new[] { 500, 501 }, new[] { 40, 41 });
            AsyncProfilerCallStack middle = s.Segment(AsyncCallstackKind.StateMachineAsync,
                new[] { 600, 601, 602 }, new[] { 20, 21, 22 });
            AsyncProfilerCallStack inner = s.Segment(AsyncCallstackKind.StateMachineAsync,
                new[] { 700, 701, 702 }, new[] { 10, 11, 12 });
            var sync = new[]
            {
                s.Sync(50, 90),
                s.Sync(51, 11),
                s.Sync(52, 10),
                new AsyncProfilerStitchSyncFrame(Scenario.CA(53), MethodIndex.Invalid),
                s.Sync(54, 91),
                s.Sync(55, 21),
                s.Sync(56, 20),
                new AsyncProfilerStitchSyncFrame(Scenario.CA(57), MethodIndex.Invalid),
                s.Sync(58, 92),
                s.Sync(59, 40),
                s.Boundary(60, Wrapper(0)),
                s.Boundary(61, s_dispatchContinuation),
                s.Sync(62, 99),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { inner, middle, outer });

            Assert.Equal(
                new[] { 50, 51, 702, 53, 54, 55, 602, 57, 58, 59, 501, 62 }.Select(Scenario.CA),
                result.Frames.Select(frame => frame.CodeAddress));
            Assert.Equal(3, result.Diagnostics.SegmentsProcessed);
            Assert.Equal(2, result.Diagnostics.BoundariesNotFound);
            Assert.Equal(2, result.Diagnostics.V1TruncatedSuffixFallbackUsed);
            Assert.Equal(3, result.Frames.Count(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent));
            Assert.Equal(0, result.Diagnostics.IncompleteAsyncRootsNormalized);
        }

        [Fact]
        public void MissingInnerV1Boundary_CarrierStillRequiresUniquePhysicalAlignment()
        {
            var s = new Scenario();
            AsyncProfilerCallStack outer = s.Segment(AsyncCallstackKind.RuntimeAsync,
                new[] { 500, 501 }, new[] { 40, 41 });
            AsyncProfilerCallStack inner = s.Segment(AsyncCallstackKind.StateMachineAsync,
                new[] { 600, 601, 602 }, new[] { 30, 31, 32 });
            var sync = new[]
            {
                s.Sync(10, 90),
                s.BoxCarrier(11, 31),
                s.Sync(12, 30),
                new AsyncProfilerStitchSyncFrame(Scenario.CA(18), MethodIndex.Invalid),
                s.Sync(14, 40),
                s.Boundary(15, Wrapper(0)),
                s.Sync(17, 99),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { inner, outer });

            Assert.Equal(sync.Take(4).Select(frame => frame.CodeAddress),
                result.Frames.Take(4).Select(frame => frame.CodeAddress));
            Assert.DoesNotContain(result.Frames, frame => frame.IsStateMachineBoxCarrier);
            Assert.Contains(result.Frames, frame =>
                frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent && ReferenceEquals(frame.Context, outer));
            Assert.Equal(0, result.Diagnostics.V1TruncatedSuffixFallbackUsed);
            Assert.Equal(1, result.Diagnostics.BoundariesNotFound);
        }

        [Fact]
        public void MixedV2InnerAndV1Outer_PreservesUnclassifiedBridge()
        {
            var s = new Scenario();
            AsyncProfilerCallStack outer = s.Segment(AsyncCallstackKind.StateMachineAsync,
                frameCodeAddrs: new[] { 800, 801 }, frameMethods: new[] { 20, 21 });
            AsyncProfilerCallStack inner = s.Segment(AsyncCallstackKind.RuntimeAsync,
                frameCodeAddrs: new[] { 700, 701 }, frameMethods: new[] { 10, 11 });

            var sync = new[]
            {
                s.Sync(50, 90),
                s.Sync(51, 10),
                s.Boundary(52, Wrapper(0)),
                s.Boundary(53, s_dispatchContinuation),
                s.Boundary(54, s_dispatchContinuation),
                s.Sync(55, 91),                       // unclassified scheduling frame
                s.Sync(56, 92),                       // native bridge
                s.Sync(57, 93),                       // synchronous bridge
                s.Sync(58, 20),
                s.Sync(59, 94),                       // V1 leaf transition
                s.Boundary(60, s_v1Dispatcher),
                s.Boundary(61, s_v1Infrastructure),
                s.Sync(62, 95),
                s.Sync(63, 96),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { inner, outer });

            Assert.Equal(new[]
            {
                Scenario.CA(50), Scenario.CA(51), Scenario.CA(701),
                Scenario.CA(55), Scenario.CA(56), Scenario.CA(57),
                Scenario.CA(58), Scenario.CA(801), Scenario.CA(62), Scenario.CA(63),
            }, result.Frames.Select(frame => frame.CodeAddress));
            Assert.Equal(2, result.Diagnostics.V2InfrastructureFramesCollapsed);
            Assert.Equal(1, result.Diagnostics.V1InfrastructureFramesCollapsed);
            Assert.Equal(1, result.Diagnostics.V1InlineFallbackUsed);
            Assert.Equal(0, result.Diagnostics.BoundariesNotFound);
        }

        [Fact]
        public void MixedV2InnerAndFullyCompletedV1Outer_RemovesCompletionChain()
        {
            var s = new Scenario();
            s.MarkMethodCompletionObserved(AsyncCallstackKind.StateMachineAsync);
            AsyncProfilerCallStack outer = s.Segment(AsyncCallstackKind.StateMachineAsync,
                frameCodeAddrs: new[] { 800, 801 }, frameMethods: new[] { 20, 21 },
                completions: Completed(2));
            AsyncProfilerCallStack inner = s.Segment(AsyncCallstackKind.RuntimeAsync,
                frameCodeAddrs: new[] { -1, 700 }, frameMethods: new[] { 0, 10 });

            var sync = new[]
            {
                s.Sync(50, 90),                       // synchronous work in the resumed V2 parent
                s.Sync(51, 10),                       // V2 current = segment[1]
                s.Boundary(52, Wrapper(1)),
                s.Boundary(53, s_dispatchContinuation),
                s.Boundary(54, s_dispatchContinuation),
                s.Sync(55, 91),                       // Task.RunContinuations
                s.Sync(56, 21),                       // completed V1 MixedChildAsync
                s.Sync(57, 92),                       // V1 completion/dispatcher machinery
                s.Sync(58, 20),                       // completed V1 BurnCpuAsync
                s.Boundary(59, s_v1Dispatcher),
                s.Sync(60, 99),                       // ThreadPool dispatch anchor
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { inner, outer });

            Assert.Equal(new[] { Scenario.CA(50), Scenario.CA(51), Scenario.CA(60) },
                result.Frames.Select(frame => frame.CodeAddress));
            Assert.Equal(2, result.Diagnostics.SegmentsProcessed);
            Assert.Equal(2, result.Diagnostics.V2InfrastructureFramesCollapsed);
            Assert.Equal(0, result.Diagnostics.BoundariesNotFound);
        }

        [Fact]
        public void MixedV2InnerAndV1Outer_CompletionMarkerRemovesFinalV1Frame()
        {
            var s = new Scenario();
            AsyncProfilerCallStack outer = s.Segment(AsyncCallstackKind.StateMachineAsync,
                frameCodeAddrs: new[] { 800, 801 }, frameMethods: new[] { 20, 21 });
            AsyncProfilerCallStack inner = s.Segment(AsyncCallstackKind.RuntimeAsync,
                frameCodeAddrs: new[] { -1, 700 }, frameMethods: new[] { 0, 10 });

            var sync = new[]
            {
                s.Sync(50, 90),
                s.Sync(51, 10),
                s.Boundary(52, Wrapper(1)),
                s.Boundary(53, s_dispatchContinuation),
                s.Boundary(54, s_dispatchContinuation),
                s.Sync(55, 91, AsyncProfilerStitchSyncFrameKind.V1MethodCompletion),
                s.Sync(56, 21),
                s.Sync(57, 92),
                s.Sync(58, 20),
                s.Boundary(59, s_v1Dispatcher),
                s.Sync(60, 99),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { inner, outer });

            Assert.Equal(new[] { Scenario.CA(50), Scenario.CA(51), Scenario.CA(60) },
                result.Frames.Select(frame => frame.CodeAddress));
            Assert.Equal(1, result.Diagnostics.V1InlineFallbackUsed);
            Assert.Equal(0, result.Diagnostics.BoundariesNotFound);

            sync[5] = s.Sync(55, 91);
            AsyncProfilerStitchResult withoutCompletionProof = s.Run(sync, new[] { inner, outer });
            Assert.Contains(withoutCompletionProof.Frames, frame =>
                frame.CodeAddress == Scenario.CA(56) && frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
        }

        [Fact]
        public void V1ResumedInlineAfterV2ContextCompleted_PreservesPhysicalV2CompletionChain()
        {
            var s = new Scenario();
            AsyncProfilerCallStack activeV1 = s.Segment(AsyncCallstackKind.StateMachineAsync,
                frameCodeAddrs: new[] { 500, 501 }, frameMethods: new[] { 10, 11 });

            var sync = new[]
            {
                s.Sync(50, 90),                       // synchronous work in the resumed V1 parent
                s.Sync(51, 10),                       // V1 current
                s.Boundary(52, s_v1Dispatcher),
                s.Boundary(53, s_v1Infrastructure),
                s.Sync(54, 91),                       // Task.RunContinuations / TrySetResult
                s.Boundary(55, s_dispatchContinuation), // V2 InstrumentedDispatchContinuations
                s.Boundary(56, s_dispatchContinuation), // V2 DispatchContinuations
                s.Sync(57, 99),                       // ThreadPool dispatch anchor
            };

            // The V2 context emits CompleteRuntimeAsyncContext before TrySetResult resumes the
            // V1 continuation inline. Its physical dispatcher is still on the stack, but its
            // interval is already closed and therefore must not be supplied as an active segment.
            AsyncProfilerStitchResult result = s.Run(sync, new[] { activeV1 });

            Assert.Equal(new[]
            {
                Scenario.CA(50), Scenario.CA(51), Scenario.CA(501),
                Scenario.CA(54), Scenario.CA(55), Scenario.CA(56), Scenario.CA(57),
            },
                result.Frames.Select(frame => frame.CodeAddress));
            Assert.Equal(1, result.Diagnostics.SegmentsProcessed);
            Assert.Equal(1, result.Diagnostics.V1InlineFallbackUsed);
            Assert.Equal(1, result.Diagnostics.V1InfrastructureFramesCollapsed);
            Assert.Equal(0, result.Diagnostics.V2WrapperFallbackUsed);
            Assert.Equal(0, result.Diagnostics.V2InfrastructureFramesCollapsed);
            Assert.Equal(0, result.Diagnostics.BoundariesNotFound);
        }

        [Fact]
        public void NestedV1Segments_PreserveUnclassifiedBridge()
        {
            var s = new Scenario();
            AsyncProfilerCallStack outer = s.Segment(AsyncCallstackKind.StateMachineAsync,
                frameCodeAddrs: new[] { 800, 801 }, frameMethods: new[] { 20, 21 });
            AsyncProfilerCallStack inner = s.Segment(AsyncCallstackKind.StateMachineAsync,
                frameCodeAddrs: new[] { 500, 501 }, frameMethods: new[] { 10, 11 });

            var sync = new[]
            {
                s.Sync(50, 90),
                s.Sync(51, 10),
                s.Sync(52, 91),
                s.Boundary(53, s_v1Dispatcher),
                s.Boundary(54, s_v1Infrastructure),
                s.Sync(55, 92),                       // unclassified scheduling frame
                s.Sync(56, 93),                       // native/synchronous bridge
                s.Sync(57, 20),
                s.Sync(58, 94),
                s.Boundary(59, s_v1Dispatcher),
                s.Boundary(60, s_v1Infrastructure),
                s.Sync(61, 95),
                s.Sync(62, 96),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { inner, outer });

            Assert.Equal(new[]
            {
                Scenario.CA(50), Scenario.CA(51), Scenario.CA(501),
                Scenario.CA(55), Scenario.CA(56), Scenario.CA(57), Scenario.CA(801),
                Scenario.CA(61), Scenario.CA(62),
            }, result.Frames.Select(frame => frame.CodeAddress));
            Assert.Equal(2, result.Diagnostics.V1InfrastructureFramesCollapsed);
            Assert.Equal(2, result.Diagnostics.V1InlineFallbackUsed);
            Assert.Equal(0, result.Diagnostics.BoundariesNotFound);
        }

        [Fact]
        public void MixedV1AndV2Segments_ApplyCompletionHistoryIndependently()
        {
            var s = new Scenario();
            AsyncProfilerCallStack outer = s.Segment(AsyncCallstackKind.RuntimeAsync,
                frameCodeAddrs: new[] { 800, 801, 802 }, frameMethods: new[] { 20, 21, 22 });
            AsyncProfilerCallStack inner = s.Segment(AsyncCallstackKind.StateMachineAsync,
                frameCodeAddrs: new[] { 500, 501, 502 }, frameMethods: new[] { 10, 11, 12 });

            var sync = new[]
            {
                s.Sync(50, 90),                       // inner synchronous leaf work
                s.Sync(51, 11),                       // inner current = segment[1]
                s.Sync(52, 91),                       // current transition machinery
                s.Sync(53, 10),                       // inner completed = segment[0]
                s.Sync(54, 92),                       // completed transition machinery
                s.Boundary(55, s_v1Dispatcher),
                s.Boundary(56, s_v1Infrastructure),     // known V1 box infrastructure
                s.Boundary(57, s_v1Infrastructure),     // known V1 box infrastructure
                s.Sync(58, 93),                       // unclassified scheduling frame: collapse stops
                s.Sync(59, 94),                       // native bridge frame
                s.Sync(60, 95),                       // synchronous bridge frame
                s.Sync(61, 21),                       // outer current = segment[1]
                s.Boundary(62, Wrapper(1)),           // outer completed count = 1
                s.Boundary(63, s_dispatchContinuation),
                s.Boundary(64, s_dispatchContinuation),
                s.Sync(65, 96),                       // ThreadPool dispatch anchor
                s.Sync(66, 97),                       // root
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { inner, outer });

            Assert.Equal(new[]
            {
                Scenario.CA(50), Scenario.CA(51), Scenario.CA(502),
                Scenario.CA(58), Scenario.CA(59), Scenario.CA(60), Scenario.CA(61), Scenario.CA(802),
                Scenario.CA(65), Scenario.CA(66),
            }, result.Frames.Select(frame => frame.CodeAddress));
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCurrent, result.Frames[1].Origin);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCaller, result.Frames[2].Origin);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCaller, result.Frames[7].Origin);
            Assert.Equal(2, result.Diagnostics.SegmentsProcessed);
            Assert.Equal(1, result.Diagnostics.V1InlineFallbackUsed);
            Assert.Equal(2, result.Diagnostics.V2InfrastructureFramesCollapsed);
            Assert.Equal(2, result.Diagnostics.V1InfrastructureFramesCollapsed);
            Assert.Equal(0, result.Diagnostics.BoundariesNotFound);

            Assert.Equal(2, result.SegmentPlacements.Count);
            Assert.Same(inner, result.SegmentPlacements[0].CallStack);
            Assert.Equal(0, result.SegmentPlacements[0].LeafIndex);
            Assert.Equal(3, result.SegmentPlacements[0].RootIndexExclusive);
            Assert.Same(outer, result.SegmentPlacements[1].CallStack);
            Assert.Equal(3, result.SegmentPlacements[1].LeafIndex);
            Assert.Equal(8, result.SegmentPlacements[1].RootIndexExclusive);
        }
    }
}
