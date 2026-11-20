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
    /// Tests runtime-async (V2) dispatcher layouts: leaf wrappers, missing wrappers, zero-IP entries and dispatch targets.
    /// </summary>
    public partial class AsyncProfilerCallStackStitcherTests
    {
        [Fact]
        public void V2_StitchCollapsesDispatchContinuationInfrastructure()
        {
            // Stitch at the wrapper and collapse only its contiguous dispatch infrastructure, retaining the physical anchor.
            var s = new Scenario();
            s.MarkMethodCompletionObserved(AsyncCallstackKind.RuntimeAsync);
            AsyncProfilerCallStack seg = s.Segment(AsyncCallstackKind.RuntimeAsync,
                frameCodeAddrs: new[] { 200, 201 },   // current (leaf), suspended async caller
                frameMethods:   new[] {  50,  51 },
                completions: Completed(0));           // current = frames[0]; splice frames[1]=201

            var sync = new[]
            {
                s.Sync(90, 50),                       // current (frames[0]), adjacent above the wrapper
                s.Boundary(91, Wrapper(0)),           // continuation wrapper (stitched -> dropped)
                s.Boundary(92, s_dispatchContinuation), // InstrumentedDispatchContinuations (collapsed)
                s.Boundary(93, s_dispatchContinuation), // DispatchContinuations (collapsed)
                s.Sync(94, 99),                       // Dispatch anchor
                s.Sync(95, 98),                       // root
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { seg });

            // Expected: [current(AsyncCurrent,90), caller(Async,201), Dispatch(Sync,94), root(Sync,95)].
            Assert.Equal(4, result.Frames.Count);
            Assert.Equal(Scenario.CA(90), result.Frames[0].CodeAddress);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCurrent, result.Frames[0].Origin);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCaller, result.Frames[1].Origin);
            Assert.Equal(Scenario.CA(201), result.Frames[1].CodeAddress);
            Assert.Equal(Scenario.CA(94), result.Frames[2].CodeAddress);
            Assert.Equal(Scenario.CA(95), result.Frames[3].CodeAddress);
            Assert.DoesNotContain(result.Frames, f => f.CodeAddress == Scenario.CA(92) || f.CodeAddress == Scenario.CA(93));
            Assert.Equal(2, result.Diagnostics.V2InfrastructureFramesCollapsed);
            Assert.Equal(0, result.Diagnostics.V2SyncLayoutUsed);
        }

        [Theory]
        [InlineData(false, 0)]
        [InlineData(false, 7)]
        [InlineData(true, 0)]
        [InlineData(true, 7)]
        public void V2_ZeroIpInitialEntry_PreservesPhysicalWorkWithoutProjectingPendingFrames(bool methodEvents, byte slot)
        {
            var s = new Scenario();
            if (methodEvents)
            {
                s.MarkMethodCompletionObserved(AsyncCallstackKind.RuntimeAsync);
            }
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.RuntimeAsync,
                new[] { -1, 301, 302 }, new[] { -1, 61, 62 },
                completions: new[] { new AsyncProfilerCallStack.CompletionDelta(Qpc + 1, 1) },
                continuationIndexBase: slot, frameMethodIds: new ulong[] { 0, 301, 302 });
            var sync = new[]
            {
                s.Sync(70, -1),
                s.Sync(71, 90),
                s.Boundary(80, Wrapper(slot)),
                s.Boundary(81, s_dispatchContinuation),
                s.Boundary(82, s_dispatchContinuation),
                s.Sync(83, 91),
                s.Boundary(84, s_dispatchContinuation),
                s.Boundary(85, s_dispatchContinuation),
                s.Sync(86, 99),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { segment });

            Assert.Equal(new[] { 70, 71, 83, 84, 85, 86 }.Select(Scenario.CA),
                result.Frames.Select(frame => frame.CodeAddress));
            Assert.All(result.Frames, frame => Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, frame.Origin));
            AsyncProfilerStitchedFrame marker = result.Frames[1];
            Assert.Equal(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames, marker.Presentation);
            Assert.Equal(Scenario.MI(90), marker.Method);
            Assert.Same(segment, marker.Context);
            Assert.Equal(AsyncProfilerStitchedFramePresentation.Native, result.Frames[0].Presentation);
            Assert.All(result.Frames.Skip(2), frame => Assert.Null(frame.Context));
            Assert.Empty(result.SegmentPlacements);
            Assert.Equal(0, segment.GetCompletedFrameCount(Qpc));
            Assert.Equal(1, result.Diagnostics.SegmentsProcessed);
            Assert.Equal(2, result.Diagnostics.V2InfrastructureFramesCollapsed);
            Assert.Equal(0, result.Diagnostics.AdjacencyMismatches);
        }

        [Fact]
        public void V2_ZeroIpInitialEntry_PreservesVerifiedEnclosingCallStack()
        {
            var s = new Scenario();
            AsyncProfilerCallStack inner = s.Segment(AsyncCallstackKind.RuntimeAsync,
                new[] { -1, 301 }, new[] { -1, 61 }, frameMethodIds: new ulong[] { 0, 301 });
            AsyncProfilerCallStack outer = s.Segment(AsyncCallstackKind.RuntimeAsync,
                new[] { 400, 401 }, new[] { 70, 71 });
            var sync = new[]
            {
                s.Sync(70, -1),
                s.Sync(71, 90),
                s.Boundary(80, Wrapper(0)),
                s.Boundary(81, s_dispatchContinuation),
                s.Boundary(82, s_dispatchContinuation),
                s.Sync(83, 91),
                s.Sync(84, 70),
                s.Boundary(85, Wrapper(0)),
                s.Boundary(86, s_dispatchContinuation),
                s.Boundary(87, s_dispatchContinuation),
                s.Sync(88, 99),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { inner, outer });

            Assert.Equal(new[] { 70, 71, 83, 84, 401, 88 }.Select(Scenario.CA),
                result.Frames.Select(frame => frame.CodeAddress));
            Assert.Equal(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames, result.Frames[1].Presentation);
            Assert.Same(inner, result.Frames[1].Context);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCurrent, result.Frames[3].Origin);
            Assert.Same(outer, result.Frames[3].Context);
            Assert.Same(outer, Assert.Single(result.SegmentPlacements).CallStack);
        }

        [Theory]
        [InlineData("later-zero")]
        [InlineData("unresolved-ip")]
        [InlineData("before-base")]
        [InlineData("unknown-pool")]
        [InlineData("invalid-slot")]
        public void V2_ZeroIpInitialEntry_OtherPositionsAndUnverifiedEntriesKeepExistingProjection(string obstruction)
        {
            var s = new Scenario();
            bool laterZero = obstruction == "later-zero";
            if (laterZero)
            {
                s.MarkMethodCompletionObserved(AsyncCallstackKind.RuntimeAsync);
            }
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.RuntimeAsync,
                new[] { -1, laterZero ? -1 : 301, 302 }, new[] { -1, -1, 62 },
                completions: laterZero ? Completed(1) : null,
                continuationIndexBase: obstruction == "before-base" ? (byte)1 : (byte)0,
                wrapperCount: obstruction == "unknown-pool" ? (byte)0 : (byte)32,
                frameMethodIds: new ulong[] { obstruction == "unresolved-ip" ? 300UL : 0, 0, 302 });
            var sync = new[]
            {
                s.Sync(71, 90),
                s.Boundary(80, Wrapper(obstruction == "invalid-slot" ? 32 : 0)),
                s.Boundary(81, s_dispatchContinuation),
                s.Boundary(82, s_dispatchContinuation),
                s.Sync(86, 99),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { segment });

            Assert.DoesNotContain(result.Frames, frame =>
                frame.Presentation == AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames);
            Assert.Equal(obstruction != "invalid-slot",
                result.Frames.Any(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCaller));
            Assert.Single(result.SegmentPlacements);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void V2_WrapperIsLeaf_ConsumesWrapperAndProjectsDispatchTarget(bool methodEvents)
        {
            var s = new Scenario();
            if (methodEvents)
            {
                s.MarkMethodCompletionObserved(AsyncCallstackKind.RuntimeAsync);
            }
            AsyncProfilerCallStack seg = s.Segment(AsyncCallstackKind.RuntimeAsync,
                frameCodeAddrs: new[] { 300, 301 }, frameMethods: new[] { 60, 61 },
                completions: methodEvents ? Completed(0) : null);

            var sync = new[]
            {
                s.Boundary(80, Wrapper(0)),
                s.Boundary(81, s_dispatchContinuation),
                s.Sync(82, 99),                       // Dispatch anchor
                s.Sync(83, 98),                       // root
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { seg });

            Assert.Equal(
                new[] { Scenario.CA(300), Scenario.CA(301), Scenario.CA(82), Scenario.CA(83) },
                result.Frames.Select(f => f.CodeAddress));
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget, result.Frames[0].Origin);
            Assert.Same(seg, result.Frames[0].Context);
            Assert.Equal(AsyncProfilerStitchedFramePresentation.Native, result.Frames[0].Presentation);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCaller, result.Frames[1].Origin);
            Assert.DoesNotContain(result.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
            Assert.Equal(0, result.Diagnostics.V2SyncLayoutUsed);
            Assert.Equal(1, result.Diagnostics.V2LeafWrapperDropped);
            Assert.Equal(1, result.Diagnostics.SegmentsProcessed);
            Assert.Equal(1, result.Diagnostics.V2InfrastructureFramesCollapsed);
            AsyncProfilerCallStackPlacement placement = Assert.Single(result.SegmentPlacements);
            Assert.Same(seg, placement.CallStack);
            Assert.Equal(0, placement.LeafIndex);
            Assert.Equal(2, placement.RootIndexExclusive);

            AsyncProfilerStitchResult presented = new AsyncProfilerCallStackTransformPipeline().Transform(result, new[] { seg }, Qpc, s.Classify);
            Assert.Equal(result.Frames, presented.Frames);
            Assert.Equal(1, presented.Diagnostics.V2LeafWrapperDropped);
        }

        [Theory]
        [InlineData("slot", 1)]
        [InlineData("base", 1)]
        [InlineData("reset", 33)]
        [InlineData("method", 1)]
        [InlineData("method-and-unwind", 3)]
        [InlineData("wrapper-and-unwind", 3)]
        [InlineData("raw-ip", 1)]
        public void V2_WrapperIsLeaf_UsesVerifiedCallStackPosition(string evidence, int expectedIndex)
        {
            var s = new Scenario();
            bool methodEvents = evidence == "method" || evidence == "method-and-unwind";
            if (methodEvents)
            {
                s.MarkMethodCompletionObserved(AsyncCallstackKind.RuntimeAsync);
            }
            int slot = methodEvents ? 0 : evidence == "base" || evidence == "wrapper-and-unwind" ? 3 : 1;
            int[] addresses = Enumerable.Range(300, 36).ToArray();
            if (evidence == "raw-ip")
            {
                addresses[expectedIndex] = -1;
            }
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.RuntimeAsync,
                addresses, Enumerable.Range(60, addresses.Length).ToArray(),
                completions: methodEvents ? Completed(1) : null,
                exceptionCompletions: evidence.EndsWith("-and-unwind", StringComparison.Ordinal) ? Unwound(2) : null,
                continuationIndexBase: evidence == "base" ? (byte)2 : (byte)0,
                wrapperResets: evidence == "reset" ? new[] { Qpc } : null,
                frameMethodIds: Enumerable.Range(1000, addresses.Length).Select(id => (ulong)id).ToArray());
            var sync = new[]
            {
                s.Boundary(80, Wrapper(slot)),
                s.Boundary(81, s_dispatchContinuation),
                s.Boundary(82, s_dispatchContinuation),
                s.Sync(999, 99),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { segment });

            AsyncProfilerStitchedFrame target = Assert.Single(result.Frames.Where(frame =>
                frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget));
            Assert.Equal(expectedIndex, target.SegmentFrameIndex);
            Assert.Equal(AsyncProfilerStitchedFramePresentation.Native, target.Presentation);
            Assert.Equal(MethodIndex.Invalid, target.Method);
            Assert.False(target.IsStateMachineBoxCarrier);
            Assert.Same(segment, target.Context);
            Assert.Equal(
                addresses.Skip(expectedIndex).Select(address =>
                        address < 0 ? CodeAddressIndex.Invalid : Scenario.CA(address))
                    .Concat(new[] { Scenario.CA(999) }),
                result.Frames.Select(frame => frame.CodeAddress));
            Assert.All(result.Frames.Skip(1).Take(addresses.Length - expectedIndex - 1),
                frame => Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCaller, frame.Origin));
            Assert.DoesNotContain(result.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
            Assert.Equal(1, result.Diagnostics.SegmentsProcessed);
            Assert.Equal(methodEvents ? 0 : 1, result.Diagnostics.V2WrapperFallbackUsed);
            Assert.Equal(0, result.Diagnostics.AdjacencyMismatches);
            Assert.Equal(0, result.Diagnostics.V2SyncLayoutUsed);
            Assert.Equal(2, result.Diagnostics.V2InfrastructureFramesCollapsed);
        }

        [Theory]
        [InlineData("unknown-target")]
        [InlineData("past-end")]
        [InlineData("before-base")]
        [InlineData("unknown-pool")]
        [InlineData("invalid-slot")]
        [InlineData("completed")]
        [InlineData("over-completed")]
        public void V2_WrapperIsLeaf_UnavailableTargetPreservesSyncLayout(string obstruction)
        {
            var s = new Scenario();
            bool methodEvents = obstruction == "completed" || obstruction == "over-completed";
            if (methodEvents)
            {
                s.MarkMethodCompletionObserved(AsyncCallstackKind.RuntimeAsync);
            }
            int[] addresses = obstruction == "invalid-slot"
                ? Enumerable.Range(300, 36).ToArray() : new[] { 300, 301 };
            if (obstruction == "unknown-target")
            {
                addresses[0] = -1;
            }
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.RuntimeAsync,
                addresses, Enumerable.Range(60, addresses.Length).ToArray(),
                completions: methodEvents ? Completed(obstruction == "completed" ? 2 : 3) : null,
                continuationIndexBase: obstruction == "before-base" ? (byte)1 : (byte)0,
                wrapperCount: obstruction == "unknown-pool" ? (byte)0 : (byte)32);
            var sync = new[]
            {
                s.Boundary(80, Wrapper(obstruction == "past-end" ? 2 : obstruction == "invalid-slot" ? 32 : 0)),
                s.Boundary(81, s_dispatchContinuation),
                s.Sync(999, 99),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { segment });

            Assert.Equal(sync.Select(frame => frame.CodeAddress), result.Frames.Select(frame => frame.CodeAddress));
            Assert.All(result.Frames, frame =>
            {
                Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, frame.Origin);
                Assert.Null(frame.Context);
            });
            Assert.Empty(result.SegmentPlacements);
            Assert.Equal(1, result.Diagnostics.V2SyncLayoutUsed);
            Assert.Equal(0, result.Diagnostics.SegmentsProcessed);
        }

        [Fact]
        public void V2_NoWrapperWithoutIndexHistory_UsesSyncLayout()
        {
            // Completion counting alone cannot establish dispatcher ownership without call-stack history.
            var s = new Scenario();
            s.MarkMethodCompletionObserved(AsyncCallstackKind.RuntimeAsync);
            AsyncProfilerCallStack seg = s.Segment(AsyncCallstackKind.RuntimeAsync,
                frameCodeAddrs: new[] { 400, 401 }, frameMethods: new[] { 70, 71 }, completions: Completed(0));

            var sync = new[]
            {
                s.Boundary(84, s_dispatchContinuation), // InstrumentedDispatchContinuations (leaf)
                s.Boundary(85, s_dispatchContinuation), // DispatchContinuations
                s.Sync(86, 99),                       // Dispatch anchor
                s.Sync(87, 98),                       // root
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { seg });

            Assert.Equal(4, result.Frames.Count);
            Assert.All(result.Frames, f => Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, f.Origin));
            Assert.Equal(new[] { Scenario.CA(84), Scenario.CA(85), Scenario.CA(86), Scenario.CA(87) },
                result.Frames.Select(f => f.CodeAddress));
            Assert.Equal(1, result.Diagnostics.V2SyncLayoutUsed);
            Assert.Equal(0, result.Diagnostics.V2LeafWrapperDropped);
            Assert.Equal(0, result.Diagnostics.SegmentsProcessed);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void V2_NoWrapperWithCompletions_ProjectsRemainingSuffixAndPreservesPhysicalWork(
            bool exception, bool olderDispatcher)
        {
            var s = new Scenario();
            s.MarkMethodCompletionObserved(AsyncCallstackKind.RuntimeAsync);
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.RuntimeAsync,
                new[] { -1, -1, 401, 402, 403 }, new[] { -1, -1, 70, 71, 72 },
                completions: Completed(exception ? 1 : 2),
                exceptionCompletions: exception ? Unwound(1) : null,
                frameMethodIds: new ulong[] { 0, 0, 0x401, 0x402, 0x403 });
            AsyncProfilerCallStacksIndex index = CreateHistory(segment);
            var sync = new List<AsyncProfilerStitchSyncFrame>
            {
                s.Sync(83, 90),
                s.Boundary(84, s_dispatchContinuation),
                s.Boundary(85, s_dispatchContinuation),
            };
            if (olderDispatcher)
            {
                index.Add(segment.Thread, segment.FramesIndex, 99, 0, 0, 32, -20, -10,
                    Array.Empty<AsyncProfilerCallStack.CompletionDelta>(),
                    Array.Empty<AsyncProfilerCallStack.CompletionDelta>(), Array.Empty<long>());
                sync.Add(s.Sync(86, 91));
                sync.Add(s.Boundary(87, s_dispatchContinuation));
                sync.Add(s.Boundary(88, s_dispatchContinuation));
            }
            // A later call stack must not affect an earlier sample.
            index.Add(segment.Thread, segment.FramesIndex, 100, 0, 0, 32, Qpc + 1, Qpc + 2,
                Array.Empty<AsyncProfilerCallStack.CompletionDelta>(),
                Array.Empty<AsyncProfilerCallStack.CompletionDelta>(), Array.Empty<long>());
            sync.Add(s.Sync(89, 99));

            AsyncProfilerStitchResult result = s.RunWithHistory(sync, new[] { segment }, index);

            Assert.Equal(sync.Take(2).Select(frame => frame.CodeAddress)
                .Concat(new[] { Scenario.CA(401), Scenario.CA(402), Scenario.CA(403) })
                .Concat(sync.Skip(3).Select(frame => frame.CodeAddress)),
                result.Frames.Select(frame => frame.CodeAddress));
            Assert.All(result.Frames.Take(2), frame =>
            {
                Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, frame.Origin);
                Assert.Same(segment, frame.Context);
            });
            Assert.Equal(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames, result.Frames[1].Presentation);
            AsyncProfilerStitchedFrame target = result.Frames[2];
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget, target.Origin);
            Assert.Equal(2, target.SegmentFrameIndex);
            Assert.Equal(segment.Frames.CodeAddressAt(2), target.CodeAddress);
            Assert.All(result.Frames.Skip(3).Take(2), frame =>
                Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCaller, frame.Origin));
            Assert.All(result.Frames.Skip(5), frame => Assert.Null(frame.Context));
            Assert.DoesNotContain(result.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
            AsyncProfilerCallStackPlacement placement = Assert.Single(result.SegmentPlacements);
            Assert.Same(segment, placement.CallStack);
            Assert.Equal(0, placement.LeafIndex);
            Assert.Equal(5, placement.RootIndexExclusive);
            Assert.Equal(1, result.Diagnostics.SegmentsProcessed);
            Assert.Equal(0, result.Diagnostics.V2SyncLayoutUsed);
            Assert.Equal(2, result.Diagnostics.V2InfrastructureFramesCollapsed);
            Assert.Equal(0, result.Diagnostics.V2LeafWrapperDropped);
        }

        [Theory]
        [InlineData(1, 0, false, true)]
        [InlineData(3, 0, false, true)]
        [InlineData(3, 1, false, true)]
        [InlineData(1, 0, true, true)]
        [InlineData(1, 0, false, false)]
        [InlineData(3, 1, false, false)]
        public void V2_NoWrapperWithAvailableCompletionCounting_ProjectsUncompletedTarget(
            int frameCount, int exceptional, bool futureCompletion, bool sampledCallee)
        {
            var s = new Scenario();
            s.MarkMethodCompletionObserved(AsyncCallstackKind.RuntimeAsync);
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.RuntimeAsync,
                Enumerable.Range(400, frameCount).ToArray(), Enumerable.Range(70, frameCount).ToArray(),
                completions: futureCompletion
                    ? new[] { new AsyncProfilerCallStack.CompletionDelta(Qpc + 1, 1) }
                    : null,
                exceptionCompletions: exceptional > 0 ? Unwound(exceptional) : null,
                frameMethodIds: Enumerable.Range(400, frameCount).Select(i => (ulong)i).ToArray());
            var sync = new List<AsyncProfilerStitchSyncFrame>
            {
                s.Sync(83, 90),
                s.Boundary(84, s_dispatchContinuation),
                s.Boundary(85, s_dispatchContinuation),
                s.Sync(86, 99),
            };
            if (!sampledCallee)
            {
                sync.RemoveAt(0);
            }
            int physicalPrefixCount = sampledCallee ? 1 : 0;

            AsyncProfilerStitchResult result = s.RunWithHistory(sync, new[] { segment }, CreateHistory(segment));

            Assert.Equal(sync.Take(physicalPrefixCount + 1).Select(frame => frame.CodeAddress)
                .Concat(Enumerable.Range(exceptional, frameCount - exceptional).Select(segment.Frames.CodeAddressAt))
                .Concat(sync.Skip(physicalPrefixCount + 2).Select(frame => frame.CodeAddress)),
                result.Frames.Select(frame => frame.CodeAddress));
            AsyncProfilerStitchedFrame marker = result.Frames[physicalPrefixCount];
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, marker.Origin);
            Assert.Equal(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames, marker.Presentation);
            Assert.Same(segment, marker.Context);
            AsyncProfilerStitchedFrame target = result.Frames[physicalPrefixCount + 1];
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget, target.Origin);
            Assert.Equal(exceptional, target.SegmentFrameIndex);
            Assert.Same(segment, target.Context);
            Assert.Equal(frameCount - exceptional - 1,
                result.Frames.Count(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCaller));
            Assert.DoesNotContain(result.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
            AsyncProfilerCallStackPlacement placement = Assert.Single(result.SegmentPlacements);
            Assert.Equal(0, placement.LeafIndex);
            Assert.Equal(physicalPrefixCount + 1 + frameCount - exceptional, placement.RootIndexExclusive);
            Assert.Null(result.Frames.Last().Context);
            Assert.Equal(1, result.Diagnostics.SegmentsProcessed);
            Assert.Equal(0, result.Diagnostics.V2SyncLayoutUsed);
            Assert.Equal(2, result.Diagnostics.V2InfrastructureFramesCollapsed);
            Assert.Equal(0, result.Diagnostics.V2LeafWrapperDropped);
        }

        [Theory]
        [InlineData("no-events")]
        [InlineData("no-history")]
        [InlineData("zero-ip-first-target")]
        [InlineData("future-records")]
        [InlineData("fully-completed")]
        [InlineData("over-completed")]
        [InlineData("unknown-target")]
        [InlineData("raw-ip-target")]
        [InlineData("popped-inner")]
        [InlineData("same-qpc")]
        [InlineData("multiple-active")]
        [InlineData("mixed-boundary")]
        [InlineData("short-pair")]
        [InlineData("repeated-pair")]
        [InlineData("physical-body")]
        public void V2_NoWrapperWithUncertainProgressOrOwnership_PreservesSyncLayout(string obstruction)
        {
            var s = new Scenario();
            if (obstruction != "no-events")
            {
                s.MarkMethodCompletionObserved(AsyncCallstackKind.RuntimeAsync);
            }
            int normal = obstruction == "zero-ip-first-target" ? 0 :
                obstruction == "fully-completed" ? 3 : obstruction == "over-completed" ? 4 : 1;
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.RuntimeAsync,
                new[] { -1, obstruction == "raw-ip-target" ? -1 : 401, 402 },
                new[] { -1, obstruction == "unknown-target" ? -1 : 70, 71 },
                completions: new[] { new AsyncProfilerCallStack.CompletionDelta(
                    obstruction == "future-records" ? Qpc + 1 : Qpc, normal) },
                frameMethodIds: new ulong[] { 0, 0x401, 0x402 });
            AsyncProfilerCallStacksIndex index = CreateHistory(segment);
            if (obstruction == "popped-inner" || obstruction == "same-qpc")
            {
                index.Add(segment.Thread, segment.FramesIndex, 99, 1, 0, 32,
                    obstruction == "same-qpc" ? segment.StartQpc : Qpc - 2, Qpc - 1,
                    Array.Empty<AsyncProfilerCallStack.CompletionDelta>(),
                    Array.Empty<AsyncProfilerCallStack.CompletionDelta>(), Array.Empty<long>());
            }
            var sync = new List<AsyncProfilerStitchSyncFrame>
            {
                s.Sync(83, obstruction == "physical-body" ? 70 : 90),
                s.Boundary(84, s_dispatchContinuation),
                s.Boundary(85, s_dispatchContinuation),
                s.Sync(86, 99),
            };
            if (obstruction == "short-pair")
            {
                sync.RemoveAt(2);
            }
            else if (obstruction == "repeated-pair")
            {
                sync[2] = s.Sync(85, 84);
            }
            else if (obstruction == "mixed-boundary")
            {
                sync.Add(s.Boundary(87, s_v1Dispatcher));
            }

            AsyncProfilerStitchResult result = s.RunWithHistory(sync,
                obstruction == "multiple-active" ? new[] { segment, segment } : new[] { segment },
                obstruction == "no-history" ? null : index);

            Assert.Equal(sync.Select(frame => frame.CodeAddress), result.Frames.Select(frame => frame.CodeAddress));
            Assert.All(result.Frames, frame =>
            {
                Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, frame.Origin);
                Assert.Null(frame.Context);
            });
            Assert.Empty(result.SegmentPlacements);
            Assert.Equal(0, result.Diagnostics.SegmentsProcessed);
            Assert.Equal(1, result.Diagnostics.V2SyncLayoutUsed);
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, false)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, true)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, false)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, true)]
        public void V2_InnerWrapperIsLeaf_PreservesEntryAndBridgeWhileStitchingOuterCallStack(
            int outerKindValue, bool methodEvents)
        {
            var outerKind = (AsyncCallstackKind)outerKindValue;
            var s = new Scenario();
            if (methodEvents)
            {
                s.MarkMethodCompletionObserved(outerKind);
            }
            AsyncProfilerCallStack outer = s.Segment(outerKind,
                new[] { 500, 501, 502 }, new[] { 20, 21, 22 },
                completions: methodEvents ? Completed(1) : null);
            AsyncProfilerCallStack inner = s.Segment(AsyncCallstackKind.RuntimeAsync,
                new[] { -1, -1, 602 }, new[] { -1, -1, 32 });
            var sync = new List<AsyncProfilerStitchSyncFrame>
            {
                s.Boundary(80, Wrapper(1)),
                s.Boundary(81, s_dispatchContinuation),
                s.Boundary(82, s_dispatchContinuation),
                s.Sync(83, 90),
                s.Sync(84, 91),
                s.Sync(85, 92),
                s.Sync(86, 21),
            };
            if (outerKind == AsyncCallstackKind.StateMachineAsync)
            {
                sync.Add(s.Sync(87, 20));
            }
            sync.Add(s.Boundary(88, outerKind == AsyncCallstackKind.RuntimeAsync
                ? Wrapper(methodEvents ? 0 : 1) : s_v1Dispatcher));
            if (outerKind == AsyncCallstackKind.RuntimeAsync)
            {
                sync.Add(s.Boundary(89, s_dispatchContinuation));
                sync.Add(s.Boundary(90, s_dispatchContinuation));
            }
            sync.Add(s.Sync(91, 99));

            AsyncProfilerStitchResult result = s.Run(sync, new[] { inner, outer }, trace: true);

            Assert.Equal(
                new[] { 80, 81, 82, 83, 84, 85, 86, 502, 91 }.Select(Scenario.CA),
                result.Frames.Select(frame => frame.CodeAddress));
            Assert.All(result.Frames.Take(3), frame => Assert.Null(frame.Context));
            Assert.All(result.Frames.Skip(3).Take(3), frame =>
            {
                Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, frame.Origin);
                Assert.Same(outer, frame.Context);
            });
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCurrent, result.Frames[6].Origin);
            Assert.Equal(1, result.Frames[6].SegmentFrameIndex);
            Assert.Same(outer, result.Frames[6].Context);
            Assert.DoesNotContain(result.Frames, frame => ReferenceEquals(frame.Context, inner));
            Assert.Equal(2, result.Diagnostics.SegmentsProcessed);
            Assert.Equal(1, result.Diagnostics.V2SyncLayoutUsed);
            Assert.Equal(outerKind == AsyncCallstackKind.RuntimeAsync ? 2 : 0,
                result.Diagnostics.V2InfrastructureFramesCollapsed);
            Assert.Equal(0, result.Diagnostics.BoundariesNotFound);
            AsyncProfilerCallStackPlacement placement = Assert.Single(result.SegmentPlacements);
            Assert.Same(outer, placement.CallStack);
            Assert.Equal(3, placement.LeafIndex);
            Assert.Equal(8, placement.RootIndexExclusive);

            var pipeline = new AsyncProfilerCallStackTransformPipeline();
            AsyncProfilerStitchResult presented = pipeline.Transform(result, new[] { inner, outer }, Qpc, s.Classify);
            Assert.Equal(result.Frames.Skip(1), presented.Frames);
            Assert.Equal(1, presented.Diagnostics.V2LeafWrapperDropped);
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(0, true)]
        [InlineData(1, false)]
        [InlineData(1, true)]
        [InlineData(2, false)]
        [InlineData(2, true)]
        [InlineData(3, false)]
        [InlineData(3, true)]
        [InlineData(4, false)]
        [InlineData(4, true)]
        [InlineData(5, false)]
        [InlineData(5, true)]
        [InlineData(6, false)]
        [InlineData(6, true)]
        [InlineData(7, false)]
        [InlineData(7, true)]
        public void V2_InnerWrapperIsLeaf_StitchesMultipleMixedOuterCallStacks(int protocols, bool methodEvents)
        {
            var s = new Scenario();
            var segments = new AsyncProfilerCallStack[4];
            if (methodEvents)
            {
                s.MarkMethodCompletionObserved(AsyncCallstackKind.RuntimeAsync);
            }
            int v1Count = 0;
            // Each bit chooses V1 rather than V2 for one enclosing call stack.
            for (int i = 0; i < 3; i++)
            {
                AsyncCallstackKind kind = (protocols & (1 << i)) == 0
                    ? AsyncCallstackKind.RuntimeAsync : AsyncCallstackKind.StateMachineAsync;
                if (methodEvents)
                {
                    s.MarkMethodCompletionObserved(kind);
                }
                if (kind == AsyncCallstackKind.StateMachineAsync)
                {
                    v1Count++;
                }
                int address = 500 + i * 10;
                int method = 20 + i * 10;
                segments[i] = s.Segment(kind,
                    new[] { address, address + 1, address + 2 }, new[] { method, method + 1, method + 2 },
                    completions: methodEvents ? Completed(1) : null);
            }
            segments[3] = s.Segment(AsyncCallstackKind.RuntimeAsync,
                new[] { -1, 901, 902 }, new[] { -1, 200, 201 },
                completions: methodEvents ? Completed(1) : null);
            var sync = new List<AsyncProfilerStitchSyncFrame>
            {
                s.Boundary(80, Wrapper(methodEvents ? 0 : 1)),
                s.Boundary(81, s_dispatchContinuation),
                s.Boundary(82, s_dispatchContinuation),
            };
            var expected = new List<CodeAddressIndex> { Scenario.CA(901), Scenario.CA(902) };
            for (int i = 2; i >= 0; i--)
            {
                int address = 100 + i * 10;
                int method = 20 + i * 10;
                sync.Add(s.Sync(address, 90 + i));
                sync.Add(s.Sync(address + 1, method + 1));
                bool v1 = segments[i].Frames.Kind == AsyncCallstackKind.StateMachineAsync;
                if (v1)
                {
                    sync.Add(s.Sync(address + 2, method));
                }
                sync.Add(s.Boundary(address + 3, v1 ? s_v1Dispatcher : Wrapper(methodEvents ? 0 : 1)));
                if (!v1)
                {
                    sync.Add(s.Boundary(address + 4, s_dispatchContinuation));
                    sync.Add(s.Boundary(address + 5, s_dispatchContinuation));
                }
                expected.Add(Scenario.CA(address));
                expected.Add(Scenario.CA(address + 1));
                expected.Add(Scenario.CA(502 + i * 10));
            }
            sync.Add(s.Sync(999, 400));
            expected.Add(Scenario.CA(999));

            AsyncProfilerStitchResult result = s.Run(sync, segments.Reverse().ToArray());

            Assert.Equal(expected, result.Frames.Select(frame => frame.CodeAddress));
            Assert.Equal(4, result.Diagnostics.SegmentsProcessed);
            Assert.Equal(0, result.Diagnostics.V2SyncLayoutUsed);
            Assert.Equal((4 - v1Count) * 2, result.Diagnostics.V2InfrastructureFramesCollapsed);
            Assert.Equal(methodEvents ? 0 : v1Count, result.Diagnostics.V1InlineFallbackUsed);
            Assert.Equal(0, result.Diagnostics.BoundariesNotFound);
            Assert.Equal(0, result.Diagnostics.AdjacencyMismatches);
            Assert.Equal(0, result.Diagnostics.IncompleteAsyncRootsNormalized);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget, result.Frames[0].Origin);
            Assert.Equal(1, result.Frames[0].SegmentFrameIndex);
            Assert.Same(segments[3], result.Frames[0].Context);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCaller, result.Frames[1].Origin);
            Assert.Equal(4, result.SegmentPlacements.Count);
            Assert.Same(segments[3], result.SegmentPlacements[0].CallStack);
            Assert.Equal(0, result.SegmentPlacements[0].LeafIndex);
            Assert.Equal(2, result.SegmentPlacements[0].RootIndexExclusive);
            for (int i = 0; i < 3; i++)
            {
                AsyncProfilerCallStack callStack = segments[2 - i];
                AsyncProfilerStitchedFrame current = result.Frames[3 + i * 3];
                Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCurrent, current.Origin);
                Assert.Equal(1, current.SegmentFrameIndex);
                Assert.Same(callStack, current.Context);
                AsyncProfilerCallStackPlacement placement = result.SegmentPlacements[i + 1];
                Assert.Same(callStack, placement.CallStack);
                Assert.Equal(2 + i * 3, placement.LeafIndex);
                Assert.Equal(5 + i * 3, placement.RootIndexExclusive);
            }
        }

        [Theory]
        [InlineData("missing-wrapper")]
        [InlineData("unresolved-current")]
        [InlineData("mismatched-current")]
        [InlineData("completed-outer")]
        public void V2_InnerWrapperIsLeaf_UnverifiedOuterCallStackPreservesSyncLayout(string obstruction)
        {
            var s = new Scenario();
            bool completed = obstruction == "completed-outer";
            if (completed)
            {
                s.MarkMethodCompletionObserved(AsyncCallstackKind.RuntimeAsync);
            }
            AsyncProfilerCallStack outer = s.Segment(AsyncCallstackKind.RuntimeAsync,
                new[] { 500, 501 }, new[] { 20, 21 },
                completions: completed ? Completed(2) : null);
            AsyncProfilerCallStack inner = s.Segment(AsyncCallstackKind.RuntimeAsync,
                new[] { -1, -1 }, new[] { -1, -1 });
            var sync = new List<AsyncProfilerStitchSyncFrame>
            {
                s.Boundary(80, Wrapper(1)),
                s.Boundary(81, s_dispatchContinuation),
                s.Sync(82, 90),
                new AsyncProfilerStitchSyncFrame(Scenario.CA(83), obstruction == "unresolved-current"
                    ? MethodIndex.Invalid : Scenario.MI(obstruction == "mismatched-current" ? 99 : 20)),
            };
            if (obstruction != "missing-wrapper")
            {
                sync.Add(s.Boundary(84, Wrapper(0)));
            }
            sync.Add(s.Sync(85, 98));

            AsyncProfilerStitchResult result = s.Run(sync, new[] { inner, outer });

            Assert.Equal(sync.Select(frame => frame.CodeAddress), result.Frames.Select(frame => frame.CodeAddress));
            Assert.All(result.Frames, frame => Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, frame.Origin));
            Assert.Equal(1, result.Diagnostics.V2SyncLayoutUsed);
            Assert.Equal(0, result.Diagnostics.SegmentsProcessed);
        }
    }
}
