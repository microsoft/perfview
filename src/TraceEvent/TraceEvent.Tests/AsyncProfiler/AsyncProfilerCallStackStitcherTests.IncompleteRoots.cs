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
    /// Tests truncated physical stacks: suffix recovery and normalization to a single incomplete root.
    /// </summary>
    public partial class AsyncProfilerCallStackStitcherTests
    {
        [Fact]
        public void V1_MissingBoundary_UniqueTruncatedSuffix_RecoversCompletedCount()
        {
            // The root side of the CPU sample was truncated before the dispatcher and the first two async frames.
            // The retained physical sequence C,D,E uniquely aligns with segment[2..4], so E is current and four
            // leaf frames have completed.
            var s = new Scenario();
            AsyncProfilerCallStack seg = s.Segment(AsyncCallstackKind.StateMachineAsync,
                frameCodeAddrs: new[] { 500, 501, 502, 503, 504 },
                frameMethods: new[] { 30, 31, 32, 33, 34 });

            var sync = new[]
            {
                s.Sync(40, 70), // CPU work
                s.Sync(41, 34), // E current
                s.Sync(42, 90), // transition infrastructure
                s.Sync(43, 33), // D completed
                s.Sync(44, 91), // transition infrastructure
                s.Sync(45, 32), // C completed; A/B and dispatcher were truncated
                s.Sync(46, 99), // retained root-side frame
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { seg }, trace: true);

            Assert.Equal(
                new[] { Scenario.CA(40), Scenario.CA(41), CodeAddressIndex.Invalid },
                result.Frames.Select(frame => frame.CodeAddress));
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCurrent, result.Frames[1].Origin);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot, result.Frames[2].Origin);
            Assert.Null(result.Frames[2].Context);
            Assert.Equal(1, result.Diagnostics.BoundariesNotFound);
            Assert.Equal(1, result.Diagnostics.V1InlineFallbackUsed);
            Assert.Equal(1, result.Diagnostics.V1TruncatedSuffixFallbackUsed);
            Assert.Equal(1, result.Diagnostics.IncompleteAsyncRootsNormalized);
            Assert.Contains(result.Diagnostics.Messages, message => message.Contains("completed=4"));
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 0)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 1)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 3)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 0)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 1)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 3)]
        public void IncompleteRoot_DifferentCutDepthsHaveTheSameStructuralRoot(
            int kindValue, int fragmentLength)
        {
            var kind = (AsyncCallstackKind)kindValue;
            var s = new Scenario();
            AsyncProfilerCallStack seg = s.Segment(kind,
                frameCodeAddrs: new[] { 500, 501, 502, 503, 504, 505, 506 },
                frameMethods: new[] { 30, 31, 32, 33, 34, 35, 36 });
            var sync = new List<AsyncProfilerStitchSyncFrame>
            {
                s.Sync(40, 70),
                s.Sync(41, 34),
                s.Sync(42, 90),
                s.Sync(43, 33),
                s.Sync(44, 91),
                s.Sync(45, 32),
            };
            for (int i = 0; i < fragmentLength; i++)
            {
                // Opaque resolved frames: normalization must not depend on helper names or classifications.
                sync.Add(s.Sync(90 + i, 100 + i));
            }

            AsyncProfilerStitchResult result = s.Run(sync, new[] { seg });

            Assert.Equal(
                new[] { Scenario.CA(40), Scenario.CA(41), Scenario.CA(505), Scenario.CA(506), CodeAddressIndex.Invalid },
                result.Frames.Select(frame => frame.CodeAddress));
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCurrent, result.Frames[1].Origin);
            Assert.Equal(4, result.Frames[1].SegmentFrameIndex);
            Assert.Equal(Scenario.MI(34), result.Frames[1].Method);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot, result.Frames[4].Origin);
            Assert.Equal(AsyncProfilerStitchedFrameContextKind.None, result.Frames[4].ContextKind);
            Assert.Equal(1, result.Diagnostics.IncompleteAsyncRootsNormalized);
            Assert.Equal(kind == AsyncCallstackKind.RuntimeAsync ? 1 : 0,
                result.Diagnostics.V2TruncatedSuffixFallbackUsed);
            Assert.Equal(0, result.Diagnostics.V2WrapperFallbackUsed);
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 4, true)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 2, false)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 7, false)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 4, true)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 2, false)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 7, false)]
        public void IncompleteRoot_CompletionRecordsMustAgreeWithPhysicalAlignment(
            int kindValue, int completed, bool normalize)
        {
            var kind = (AsyncCallstackKind)kindValue;
            var s = new Scenario();
            s.MarkMethodCompletionObserved(kind);
            AsyncProfilerCallStack seg = s.Segment(kind,
                frameCodeAddrs: new[] { 500, 501, 502, 503, 504, 505, 506 },
                frameMethods: new[] { 30, 31, 32, 33, 34, 35, 36 },
                completions: Completed(completed));
            var sync = new[]
            {
                s.Sync(40, 70),
                s.Sync(41, 34),
                s.Sync(42, 33),
                s.Sync(43, 32),
                s.Sync(44, 99),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { seg });

            Assert.Equal(normalize ? 1 : 0, result.Diagnostics.IncompleteAsyncRootsNormalized);
            if (normalize)
            {
                Assert.Equal(AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot, result.Frames[result.Frames.Count - 1].Origin);
            }
            else
            {
                Assert.DoesNotContain(result.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot);
                Assert.Contains(result.Frames,
                    frame => frame.CodeAddress == Scenario.CA(44) && frame.Origin == AsyncProfilerStitchedFrameOrigin.Sync);
            }
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync)]
        public void IncompleteRoot_UnresolvedCallerCouldConcealABoundary(int kindValue)
        {
            var kind = (AsyncCallstackKind)kindValue;
            var s = new Scenario();
            AsyncProfilerCallStack seg = s.Segment(kind,
                frameCodeAddrs: new[] { 500, 501, 502, 503, 504 },
                frameMethods: new[] { 30, 31, 32, 33, 34 });
            var sync = new[]
            {
                s.Sync(40, 70),
                s.Sync(41, 34),
                s.Sync(42, 33),
                s.Sync(43, 32),
                new AsyncProfilerStitchSyncFrame(Scenario.CA(44), MethodIndex.Invalid),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { seg });

            Assert.Equal(kind == AsyncCallstackKind.StateMachineAsync ? 1 : 0,
                result.Diagnostics.V1TruncatedSuffixFallbackUsed);
            Assert.Equal(0, result.Diagnostics.IncompleteAsyncRootsNormalized);
            Assert.Contains(result.Frames, frame => frame.CodeAddress == Scenario.CA(44));
            Assert.Equal(sync.Select(frame => frame.CodeAddress),
                result.Frames.Take(sync.Length).Select(frame => frame.CodeAddress));
            Assert.DoesNotContain(result.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot);
        }

        [Theory]
        [InlineData((int)AsyncProfilerCallStackStitchBoundaryKind.V2Dispatcher)]
        [InlineData((int)AsyncProfilerCallStackStitchBoundaryKind.V2DispatcherInfrastructure)]
        public void V1_IncompleteRoot_IntactForeignBoundaryAndHostArePreserved(int kindValue)
        {
            var kind = (AsyncProfilerCallStackStitchBoundaryKind)kindValue;
            var s = new Scenario();
            AsyncProfilerCallStack seg = s.Segment(AsyncCallstackKind.StateMachineAsync,
                frameCodeAddrs: new[] { 500, 501, 502, 503, 504 },
                frameMethods: new[] { 30, 31, 32, 33, 34 });
            var sync = new[]
            {
                s.Sync(40, 70),
                s.Sync(41, 34),
                s.Sync(42, 33),
                s.Sync(43, 32),
                s.Boundary(44, new AsyncProfilerCallStackStitchBoundaryInfo(kind, 0)),
                s.Sync(45, 99),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { seg });

            Assert.Equal(0, result.Diagnostics.IncompleteAsyncRootsNormalized);
            Assert.Equal(
                new[] { Scenario.CA(44), Scenario.CA(45) },
                result.Frames.Skip(result.Frames.Count - 2).Select(frame => frame.CodeAddress));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(3)]
        public void V1_SurvivingDispatcher_MarksCallerTailCutInsideRuntimeInfrastructure(int infrastructureLength)
        {
            var s = new Scenario();
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.StateMachineAsync,
                new[] { 500, 501 }, new[] { 30, 31 });
            var sync = new List<AsyncProfilerStitchSyncFrame>
            {
                s.Sync(40, 70),
                s.Sync(41, 30),
                s.Boundary(42, s_v1Dispatcher),
            };
            sync.AddRange(Enumerable.Range(0, infrastructureLength).Select(i => s.Boundary(43 + i, s_v1Infrastructure)));

            AsyncProfilerStitchResult result = s.Run(sync, new[] { segment });

            Assert.Equal(new[] { Scenario.CA(40), Scenario.CA(41), Scenario.CA(501), CodeAddressIndex.Invalid },
                result.Frames.Select(frame => frame.CodeAddress));
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot, result.Frames.Last().Origin);
            Assert.Null(result.Frames.Last().Context);
            Assert.Equal(0, result.Diagnostics.BoundariesNotFound);
            Assert.Equal(infrastructureLength, result.Diagnostics.V1InfrastructureFramesCollapsed);
            Assert.Equal(1, result.Diagnostics.IncompleteAsyncRootsNormalized);
        }

        [Theory]
        [InlineData("unknown")]
        [InlineData("foreign-profile")]
        [InlineData("foreign-boundary")]
        public void V1_SurvivingDispatcher_PreservesUnverifiedCallerTail(string obstruction)
        {
            var s = new Scenario();
            var support = new StructuralRulesSupport(0);
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.StateMachineAsync,
                new[] { 500, 501 }, new[] { 30, 31 });
            AsyncProfilerStitchSyncFrame tail = obstruction == "unknown" ? s.Sync(43, 90) :
                s.Boundary(43, obstruction == "foreign-boundary" ? Wrapper(0) : s_v1Infrastructure);
            var sync = new[] { s.Sync(40, 30), s.Boundary(42, s_v1Dispatcher), tail };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { segment }, support,
                ca => obstruction == "foreign-profile" && ca == Scenario.CA(43)
                    ? AsyncProfilerRuntimeSupport.Common : support);

            Assert.Equal(Scenario.CA(43), result.Frames.Last().CodeAddress);
            Assert.DoesNotContain(result.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot);
        }

        [Fact]
        public void V1_SurvivingDispatcher_UnresolvedBoundaryDoesNotProveAnIncompleteRoot()
        {
            var s = new Scenario();
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.StateMachineAsync,
                new[] { 500, 501 }, new[] { 30, 31 });
            s.Boundary(42, s_v1Dispatcher);
            var sync = new[]
            {
                s.Sync(40, 30),
                new AsyncProfilerStitchSyncFrame(Scenario.CA(42), MethodIndex.Invalid),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { segment });

            Assert.DoesNotContain(result.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        public void V1_SurvivingDispatcher_NestedContextsHaveOneOutermostIncompleteRoot(int infrastructureLength)
        {
            var s = new Scenario();
            AsyncProfilerCallStack outer = s.Segment(AsyncCallstackKind.StateMachineAsync,
                new[] { 500, 501 }, new[] { 30, 31 });
            AsyncProfilerCallStack inner = s.Segment(AsyncCallstackKind.StateMachineAsync,
                new[] { 600, 601 }, new[] { 40, 41 });
            var sync = new List<AsyncProfilerStitchSyncFrame>
            {
                s.Sync(40, 40),
                s.Boundary(41, s_v1Dispatcher),
                s.Boundary(42, s_v1Infrastructure),
                s.Sync(43, 70),
                s.Sync(44, 30),
                s.Boundary(45, s_v1Dispatcher),
            };
            sync.AddRange(Enumerable.Range(0, infrastructureLength).Select(i => s.Boundary(46 + i, s_v1Infrastructure)));

            AsyncProfilerStitchResult result = s.Run(sync, new[] { inner, outer });

            Assert.Equal(new[]
                {
                    Scenario.CA(40), Scenario.CA(601), Scenario.CA(43),
                    Scenario.CA(44), Scenario.CA(501), CodeAddressIndex.Invalid,
                }, result.Frames.Select(frame => frame.CodeAddress));
            Assert.Single(result.Frames.Where(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot));
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot, result.Frames.Last().Origin);
            Assert.Equal(2, result.Diagnostics.SegmentsProcessed);
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, false)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, true)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, false)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, true)]
        public void CompleteBoundary_PreservesArbitraryManagedAndNativeHosts(
            int kindValue, bool unresolvedHost)
        {
            var kind = (AsyncCallstackKind)kindValue;
            var s = new Scenario();
            AsyncProfilerCallStack seg = s.Segment(kind,
                frameCodeAddrs: new[] { 500, 501 },
                frameMethods: new[] { 30, 31 });
            var sync = new[]
            {
                s.Sync(40, 70),
                s.Sync(41, 30),
                s.Boundary(42, kind == AsyncCallstackKind.StateMachineAsync ? s_v1Dispatcher : Wrapper(0)),
                s.Sync(43, 99),
                new AsyncProfilerStitchSyncFrame(Scenario.CA(44), unresolvedHost ? MethodIndex.Invalid : Scenario.MI(100)),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { seg });

            Assert.Equal(
                new[] { Scenario.CA(40), Scenario.CA(41), Scenario.CA(501), Scenario.CA(43), Scenario.CA(44) },
                result.Frames.Select(frame => frame.CodeAddress));
            Assert.Equal(0, result.Diagnostics.IncompleteAsyncRootsNormalized);
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, (int)AsyncCallstackKind.RuntimeAsync)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, (int)AsyncCallstackKind.StateMachineAsync)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, (int)AsyncCallstackKind.RuntimeAsync)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, (int)AsyncCallstackKind.StateMachineAsync)]
        public void NestedIncompleteRoot_IsOnlyAttachedToTheOutermostCallStack(
            int outerKindValue, int innerKindValue)
        {
            var outerKind = (AsyncCallstackKind)outerKindValue;
            var innerKind = (AsyncCallstackKind)innerKindValue;
            var s = new Scenario();
            AsyncProfilerCallStack outer = s.Segment(outerKind,
                frameCodeAddrs: new[] { 500, 501, 502, 503, 504, 505, 506 },
                frameMethods: new[] { 30, 31, 32, 33, 34, 35, 36 });
            AsyncProfilerCallStack inner = s.Segment(innerKind,
                frameCodeAddrs: new[] { 200, 201 },
                frameMethods: new[] { 50, 51 });
            var sync = new[]
            {
                s.Sync(40, 70),
                s.Sync(41, 50),
                s.Boundary(42, innerKind == AsyncCallstackKind.RuntimeAsync ? Wrapper(0) : s_v1Dispatcher),
                s.Sync(43, 90),
                s.Sync(44, 34),
                s.Sync(45, 33),
                s.Sync(46, 32),
                s.Sync(47, 99),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { inner, outer });

            Assert.Equal(
                new[]
                {
                    Scenario.CA(40), Scenario.CA(41), Scenario.CA(201), Scenario.CA(43),
                    Scenario.CA(44), Scenario.CA(505), Scenario.CA(506), CodeAddressIndex.Invalid,
                },
                result.Frames.Select(frame => frame.CodeAddress));
            Assert.Equal(2, result.Diagnostics.SegmentsProcessed);
            Assert.Equal(1, result.Diagnostics.IncompleteAsyncRootsNormalized);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot, result.Frames[result.Frames.Count - 1].Origin);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void V2_IncompleteRoot_CannotRecoverBeforeRecordedCompletionLowerBounds(bool exception)
        {
            var s = new Scenario();
            AsyncProfilerCallStack seg = s.Segment(AsyncCallstackKind.RuntimeAsync,
                frameCodeAddrs: Enumerable.Range(500, 41).ToArray(),
                frameMethods: Enumerable.Range(30, 41).ToArray(),
                exceptionCompletions: exception ? Unwound(5) : null,
                wrapperResets: exception ? null : new[] { Qpc });
            var sync = new[]
            {
                s.Sync(40, 90),
                s.Sync(41, 34),
                s.Sync(42, 33),
                s.Sync(43, 32),
                s.Sync(44, 99),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { seg });

            Assert.Equal(0, result.Diagnostics.IncompleteAsyncRootsNormalized);
            Assert.Equal(0, result.Diagnostics.V2TruncatedSuffixFallbackUsed);
            Assert.Equal(1, result.Diagnostics.V2WrapperFallbackUsed);
            Assert.Contains(result.Frames, frame => frame.CodeAddress == Scenario.CA(44));
        }

        [Fact]
        public void V1_MissingBoundary_AmbiguousRepeatedSuffix_IsNotRecovered()
        {
            // A recursive state machine produces the same method identity at every logical depth. The retained
            // physical sequence can align at several positions, so truncated-suffix recovery must not guess.
            var s = new Scenario();
            AsyncProfilerCallStack seg = s.Segment(AsyncCallstackKind.StateMachineAsync,
                frameCodeAddrs: new[] { 500, 501, 502, 503, 504 },
                frameMethods: new[] { 30, 30, 30, 30, 30 });

            var sync = new[]
            {
                s.Sync(40, 70),
                s.Sync(41, 30),
                s.Sync(42, 90),
                s.Sync(43, 30),
                s.Sync(44, 91),
                s.Sync(45, 30),
                s.Sync(46, 99),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { seg });

            Assert.Equal(0, result.Diagnostics.V1TruncatedSuffixFallbackUsed);
            Assert.Equal(0, result.Diagnostics.IncompleteAsyncRootsNormalized);
            Assert.DoesNotContain(result.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot);
            Assert.Equal(1, result.Diagnostics.V1InlineFallbackUsed);
            Assert.Contains(result.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCaller);
        }
    }
}
