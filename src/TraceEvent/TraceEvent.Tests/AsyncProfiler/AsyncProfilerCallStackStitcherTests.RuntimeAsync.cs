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
    /// Tests runtime-async (V2) stitching at a resumed body: completion counting, resume stubs and adjacency checks.
    /// </summary>
    public partial class AsyncProfilerCallStackStitcherTests
    {
        [Fact]
        public void V2_CompletionEvents_SplicesRemainingAsyncFramesAndMarksCurrentAsync()
        {
            // Segment (leaf-to-root): [C0, C1, Mcur, A1, A2]; completed=2 -> current=Mcur (frames[2]); callers=A1,A2.
            var s = new Scenario();
            s.MarkMethodCompletionObserved(AsyncCallstackKind.RuntimeAsync);
            AsyncProfilerCallStack seg = s.Segment(AsyncCallstackKind.RuntimeAsync,
                frameCodeAddrs: new[] { 200, 201, 202, 203, 204 },
                frameMethods:   new[] {  50,  51,  52,  53,  54 },
                completions: Completed(2));

            // Sync (leaf-to-root): Mcur is adjacent above the wrapper boundary; completed frames already returned.
            var sync = new[]
            {
                s.Sync(90, 52),                     // Mcur (current, running) -> method 52 matches frames[2]
                s.Boundary(91, Wrapper(3)),         // V2 dispatcher boundary (continuation wrapper)
                s.Sync(92, 99),                     // root
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { seg });

            // Expected: [Mcur(AsyncCurrent), A1(Async), A2(Async), root(Sync)].
            Assert.Equal(4, result.Frames.Count);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCurrent, result.Frames[0].Origin);
            Assert.Equal(Scenario.CA(90), result.Frames[0].CodeAddress);           // retain sampled code address
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCaller, result.Frames[1].Origin);
            Assert.Equal(Scenario.CA(203), result.Frames[1].CodeAddress);          // A1
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCaller, result.Frames[2].Origin);
            Assert.Equal(Scenario.CA(204), result.Frames[2].CodeAddress);          // A2
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, result.Frames[3].Origin);
            Assert.Equal(Scenario.CA(92), result.Frames[3].CodeAddress);           // root

            Assert.Equal(1, result.Diagnostics.SegmentsProcessed);
            Assert.Equal(0, result.Diagnostics.BoundariesNotFound);
            Assert.Equal(0, result.Diagnostics.AdjacencyMismatches);
            Assert.Equal(0, result.Diagnostics.V2WrapperFallbackUsed); // completion events used, not the fallback
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void V2_NoMethodCompletions_UsesWrapperSlotWithoutAddingUnwinds(bool unwind)
        {
            // The wrapper slot already includes exceptional progress; adding the unwind again would skip a caller.
            var s = new Scenario();
            AsyncProfilerCallStack seg = s.Segment(AsyncCallstackKind.RuntimeAsync,
                frameCodeAddrs: new[] { 300, 301, 302 },
                frameMethods:   new[] {  60,  61,  62 },
                exceptionCompletions: unwind ? Unwound(1) : null);

            var sync = new[]
            {
                s.Sync(80, 61),                 // current (frames[1]) adjacent above the wrapper
                s.Boundary(81, Wrapper(1)),     // wrapper slot 1 -> completed = 1
                s.Sync(82, 99),                 // root
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { seg });

            // completed=1 -> current=frames[1]; splice frames[2]=302.
            Assert.Equal(3, result.Frames.Count);
            Assert.Equal(Scenario.CA(80), result.Frames[0].CodeAddress);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCaller, result.Frames[1].Origin);
            Assert.Equal(Scenario.CA(302), result.Frames[1].CodeAddress);
            Assert.Equal(Scenario.CA(82), result.Frames[2].CodeAddress);
            Assert.Equal(1, result.Diagnostics.V2WrapperFallbackUsed);
            Assert.Equal(0, result.Diagnostics.AdjacencyMismatches);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void V2_ResumeStub_MatchesBodyAndConsumesOnlyTheVerifiedStub(bool methodEvents)
        {
            var s = new Scenario();
            if (methodEvents)
            {
                s.MarkMethodCompletionObserved(AsyncCallstackKind.RuntimeAsync);
            }
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.RuntimeAsync,
                new[] { 500, 501, 502 }, new[] { 30, 31, 32 },
                completions: methodEvents ? Completed(1) : null);
            var sync = new[]
            {
                s.Sync(40, 90),
                s.Sync(41, 31),
                s.Sync(42, 91, AsyncProfilerStitchSyncFrameKind.V2ResumeStub),
                s.Boundary(43, Wrapper(1)),
                s.Sync(44, 99),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { segment });

            Assert.Equal(new[] { 40, 41, 502, 44 }.Select(Scenario.CA), result.Frames.Select(frame => frame.CodeAddress));
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, result.Frames[0].Origin);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCurrent, result.Frames[1].Origin);
            Assert.Equal(1, result.Frames[1].SegmentFrameIndex);
            Assert.Same(segment, result.Frames[1].Context);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCaller, result.Frames[2].Origin);
            Assert.Equal(0, result.Diagnostics.AdjacencyMismatches);
            Assert.Equal(1, result.Diagnostics.V2InfrastructureFramesCollapsed);
        }

        [Theory]
        [InlineData("unclassified")]
        [InlineData("unresolved-stub")]
        [InlineData("wrong-method")]
        [InlineData("intervening-frame")]
        [InlineData("second-stub")]
        [InlineData("no-wrapper")]
        [InlineData("no-segment")]
        public void V2_ResumeStub_UnverifiedAlignmentPreservesPhysicalFrames(string obstruction)
        {
            var s = new Scenario();
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.RuntimeAsync,
                new[] { 500, 501 }, new[] { 30, 31 });
            var sync = new List<AsyncProfilerStitchSyncFrame>
            {
                s.Sync(40, obstruction == "wrong-method" ? 90 : 30),
            };
            if (obstruction == "intervening-frame" || obstruction == "second-stub")
            {
                sync.Add(s.Sync(41, 91, obstruction == "second-stub"
                    ? AsyncProfilerStitchSyncFrameKind.V2ResumeStub : AsyncProfilerStitchSyncFrameKind.None));
            }
            sync.Add(s.Sync(42, obstruction == "unresolved-stub" ? -1 : 92, obstruction == "unclassified"
                ? AsyncProfilerStitchSyncFrameKind.None : AsyncProfilerStitchSyncFrameKind.V2ResumeStub));
            if (obstruction != "no-wrapper")
            {
                sync.Add(s.Boundary(43, Wrapper(0)));
            }
            sync.Add(s.Sync(44, 99));

            AsyncProfilerStitchResult result = s.Run(sync, obstruction == "no-segment"
                ? Array.Empty<AsyncProfilerCallStack>() : new[] { segment });

            Assert.DoesNotContain(result.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
            Assert.Equal(0, result.Diagnostics.V2InfrastructureFramesCollapsed);
            Assert.Equal(sync.Where(frame => frame.CodeAddress != Scenario.CA(43)).Select(frame => frame.CodeAddress),
                result.Frames.Where(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
                    frame.CodeAddress != Scenario.CA(43)).Select(frame => frame.CodeAddress));
            Assert.Equal(obstruction == "unresolved-stub" || obstruction == "no-wrapper" || obstruction == "no-segment" ? 0 : 1,
                result.Diagnostics.AdjacencyMismatches);
            if (obstruction == "no-segment")
            {
                Assert.Equal(sync.Select(frame => frame.CodeAddress), result.Frames.Select(frame => frame.CodeAddress));
            }
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void V2_ResumeStubIsSampledLeaf_RequiresDispatchTargetAndNeverClaimsBodyExecution(
            bool methodEvents, bool knownTarget)
        {
            var s = new Scenario();
            if (methodEvents)
            {
                s.MarkMethodCompletionObserved(AsyncCallstackKind.RuntimeAsync);
            }
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.RuntimeAsync,
                new[] { knownTarget ? 500 : -1, 501 }, new[] { 30, 31 },
                completions: methodEvents ? Completed(0) : null);
            var sync = new[]
            {
                s.Sync(40, 90, AsyncProfilerStitchSyncFrameKind.V2ResumeStub),
                s.Boundary(41, Wrapper(0)),
                s.Boundary(42, s_dispatchContinuation),
                s.Sync(43, 99),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { segment });

            Assert.Equal((knownTarget ? new[] { 500, 501, 43 } : new[] { 40, 41, 42, 43 }).Select(Scenario.CA),
                result.Frames.Select(frame => frame.CodeAddress));
            Assert.Equal(knownTarget ? AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget : AsyncProfilerStitchedFrameOrigin.Sync,
                result.Frames[0].Origin);
            Assert.DoesNotContain(result.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
            Assert.Equal(0, result.Diagnostics.AdjacencyMismatches);
            Assert.Equal(knownTarget ? 2 : 0, result.Diagnostics.V2InfrastructureFramesCollapsed);
            Assert.Equal(knownTarget ? 0 : 1, result.Diagnostics.V2SyncLayoutUsed);
        }

        [Fact]
        public void V2_InnerSampledResumeStubWithoutTarget_PreservesEntryAndStitchesOuterBody()
        {
            var s = new Scenario();
            AsyncProfilerCallStack inner = s.Segment(AsyncCallstackKind.RuntimeAsync,
                new[] { -1, 501 }, new[] { -1, 31 });
            AsyncProfilerCallStack outer = s.Segment(AsyncCallstackKind.RuntimeAsync,
                new[] { 600, 601 }, new[] { 40, 41 });
            var sync = new[]
            {
                s.Sync(10, 90, AsyncProfilerStitchSyncFrameKind.V2ResumeStub),
                s.Boundary(11, Wrapper(0)),
                s.Boundary(12, s_dispatchContinuation),
                s.Sync(13, 91),
                s.Sync(14, 40),
                s.Sync(15, 92, AsyncProfilerStitchSyncFrameKind.V2ResumeStub),
                s.Boundary(16, Wrapper(0)),
                s.Sync(17, 99),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { inner, outer });

            Assert.Equal(new[] { 10, 11, 12, 13, 14, 601, 17 }.Select(Scenario.CA),
                result.Frames.Select(frame => frame.CodeAddress));
            Assert.Same(outer, Assert.Single(result.Frames.Where(frame =>
                frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent)).Context);
            Assert.DoesNotContain(result.Frames, frame => ReferenceEquals(frame.Context, inner));
            Assert.Equal(0, result.Diagnostics.AdjacencyMismatches);
            Assert.Equal(1, result.Diagnostics.V2SyncLayoutUsed);
            Assert.Equal(1, result.Diagnostics.V2InfrastructureFramesCollapsed);
        }

        [Theory]
        [InlineData(30, true, 0)]
        [InlineData(31, true, 1)]
        [InlineData(-1, true, 0)]
        [InlineData(30, false, 0)]
        public void V2_CurrentMatchingAndAdjacency_ResolveTheIndexedMethodOnce(
            int physicalMethod, bool resolved, int mismatches)
        {
            var s = new Scenario();
            AsyncProfilerCallStack segment = s.Segment(
                AsyncCallstackKind.RuntimeAsync, new[] { 500, 501 }, new[] { 30, 32 });
            var sync = new[]
            {
                s.Sync(40, physicalMethod),
                s.Boundary(41, Wrapper(0)),
                s.Sync(42, 99),
            };
            int resolutionCount = 0;
            Func<CodeAddressIndex, MethodIndex> methodOf = s.MethodOf;
            AsyncProfilerStitchResult result = AsyncProfilerCallStackStitcher.Stitch(
                sync, new[] { segment }, Qpc, s.Classify, s.MethodCompletionObserved,
                codeAddress =>
                {
                    resolutionCount++;
                    return resolved ? methodOf(codeAddress) : MethodIndex.Invalid;
                });

            Assert.Equal(1, resolutionCount);
            Assert.Equal(mismatches, result.Diagnostics.AdjacencyMismatches);
            Assert.Equal(resolved && physicalMethod == 30,
                result.Frames[0].Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
        }

        [Fact]
        public void V2_AdjacencyMismatch_IsRecordedAsSoftDiagnostic()
        {
            var s = new Scenario();
            s.MarkMethodCompletionObserved(AsyncCallstackKind.RuntimeAsync);
            AsyncProfilerCallStack seg = s.Segment(AsyncCallstackKind.RuntimeAsync,
                frameCodeAddrs: new[] { 400, 401 },
                frameMethods:   new[] {  70,  71 },
                completions: Completed(0)); // current = frames[0] (method 70)

            var sync = new[]
            {
                s.Sync(70, 999),                 // adjacent-above method 999 != frames[0] method 70 -> mismatch
                s.Boundary(71, Wrapper(0)),
                s.Sync(72, 99),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { seg });

            Assert.Equal(1, result.Diagnostics.AdjacencyMismatches);
            Assert.Equal(new[] { 70, 401, 72 }.Select(Scenario.CA), result.Frames.Select(frame => frame.CodeAddress));
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, result.Frames[0].Origin);
            Assert.Equal(Scenario.MI(999), result.Frames[0].Method);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCaller, result.Frames[1].Origin);
        }
    }
}
