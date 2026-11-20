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
    /// Tests state-machine (V1) alignment: inline matching, box carriers, dispatcher handling and exception unwinds.
    /// </summary>
    public partial class AsyncProfilerCallStackStitcherTests
    {
        [Fact]
        public void V1_InlineMatching_ReusesTheMethodAcrossPhysicalTransitionFrames()
        {
            var s = new Scenario();
            AsyncProfilerCallStack segment = s.Segment(
                AsyncCallstackKind.StateMachineAsync, new[] { 500, 501, 502 }, new[] { 30, 31, 32 });
            var sync = new[]
            {
                s.Sync(40, 99),
                s.Sync(41, 31),
                s.Sync(42, 98),
                s.Sync(43, 30),
                s.Sync(44, 97),
                s.Sync(45, 96),
                s.Boundary(46, s_v1Dispatcher),
                s.Sync(47, 95),
            };
            int firstMethodResolutions = 0;
            Func<CodeAddressIndex, MethodIndex> methodOf = s.MethodOf;
            AsyncProfilerStitchResult result = AsyncProfilerCallStackStitcher.Stitch(
                sync, new[] { segment }, Qpc, s.Classify, s.MethodCompletionObserved,
                codeAddress =>
                {
                    if (codeAddress == Scenario.CA(500))
                    {
                        firstMethodResolutions++;
                    }
                    return methodOf(codeAddress);
                });

            Assert.Equal(1, firstMethodResolutions);
            Assert.Equal(new[] { Scenario.CA(40), Scenario.CA(41), Scenario.CA(502), Scenario.CA(47) },
                result.Frames.Select(frame => frame.CodeAddress));
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCurrent, result.Frames[1].Origin);
            Assert.Equal(1, result.Frames[1].SegmentFrameIndex);
        }

        [Fact]
        public void V1_OrderedMatch_CountsCompletedInlineFrames()
        {
            // Inline-resume cascade. Sync (leaf-to-root): A(current), B, C, dispatcher, root.
            // Async (leaf-to-root): [C, B, A, P1]; C,B completed -> completed=2; current=A(frames[2]); callers=P1.
            var s = new Scenario();
            AsyncProfilerCallStack seg = s.Segment(AsyncCallstackKind.StateMachineAsync,
                frameCodeAddrs: new[] { 500, 501, 502, 503 },  // C, B, A, P1
                frameMethods:   new[] {  30,  31,  32,  33 });

            var sync = new[]
            {
                s.Sync(40, 32),                  // A (current, leaf) -> method 32
                s.Sync(41, 31),                  // B (completed)     -> method 31
                s.Sync(42, 30),                  // C (completed)     -> method 30
                s.Boundary(43, s_v1Dispatcher),    // MoveNextAsDispatcher
                s.Sync(44, 99),                  // root
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { seg });

            // Expected: [A(AsyncCurrent), P1(Async), root(Sync)]. Completed B/C and their physical
            // transition chain are replaced by the logical async callers.
            Assert.Equal(3, result.Frames.Count);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCurrent, result.Frames[0].Origin);
            Assert.Equal(Scenario.CA(40), result.Frames[0].CodeAddress);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCaller, result.Frames[1].Origin);
            Assert.Equal(Scenario.CA(503), result.Frames[1].CodeAddress); // P1 caller
            Assert.Equal(Scenario.CA(44), result.Frames[2].CodeAddress);  // root
            Assert.Equal(1, result.Diagnostics.V1InlineFallbackUsed);
            Assert.Equal(0, result.Diagnostics.BoundariesNotFound);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void V1_LeafBoxCarrier_ExtendsOrderedAlignmentWithoutCountingOtherBoxFrames(int completed)
        {
            var s = new Scenario();
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.StateMachineAsync,
                new[] { 500, 501, 502, 503 }, new[] { 30, 31, 32, 33 });
            var sync = new List<AsyncProfilerStitchSyncFrame>
            {
                s.BoxCarrier(40, 30 + completed),
                s.BoxCarrier(41, 30 + completed),
            };
            for (int i = completed - 1; i >= 0; i--)
            {
                sync.Add(s.Sync(42 + i, 30 + i));
            }
            sync.Add(s.Boundary(46, s_v1Dispatcher));
            sync.Add(s.Sync(47, 99));

            AsyncProfilerStitchResult result = s.Run(sync, new[] { segment });
            AsyncProfilerStitchedFrame marker = result.Frames[0];
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, marker.Origin);
            Assert.Equal(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames, marker.Presentation);
            Assert.Equal(Scenario.CA(40), marker.CodeAddress);
            Assert.Same(segment, marker.Context);
            AsyncProfilerStitchedFrame current = result.Frames[1];
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget, current.Origin);
            Assert.True(current.IsStateMachineBoxCarrier);
            Assert.Equal(Scenario.CA(40), current.CodeAddress);
            Assert.Equal(Scenario.MI(40), current.Method);
            Assert.Equal(Scenario.MI(30 + completed), current.BoxedStateMachineMethod);
            Assert.Equal(completed, current.SegmentFrameIndex);
            Assert.Same(segment, current.Context);
            Assert.True(current.WithPresentation(AsyncProfilerStitchedFramePresentation.Native).IsStateMachineBoxCarrier);
            Assert.Equal(6 - completed, result.Frames.Count);
            Assert.Equal(Scenario.CA(47), result.Frames[result.Frames.Count - 1].CodeAddress);
            Assert.Single(result.Frames.Where(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void V1_LeafBoxCarrier_RespectsMethodAndExceptionCompletionSources(bool methodEvents)
        {
            var s = new Scenario();
            if (methodEvents)
            {
                s.MarkMethodCompletionObserved(AsyncCallstackKind.StateMachineAsync);
            }
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.StateMachineAsync,
                new[] { 500, 501, 502, 503 }, new[] { 30, 31, 32, 33 },
                completions: methodEvents ? Completed(1) : null,
                exceptionCompletions: Unwound(1));
            AsyncProfilerStitchResult result = s.Run(
                new[] { s.BoxCarrier(40, 32), s.Sync(41, 31), s.Boundary(42, s_v1Dispatcher), s.Sync(43, 99) },
                new[] { segment });

            Assert.Equal(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames, result.Frames[0].Presentation);
            Assert.Equal(2, result.Frames[1].SegmentFrameIndex);
            Assert.True(result.Frames[1].IsStateMachineBoxCarrier);
            Assert.Equal(4, result.Frames.Count);
            Assert.Equal(Scenario.CA(503), result.Frames[2].CodeAddress);
            Assert.Equal(Scenario.CA(43), result.Frames[3].CodeAddress);
        }

        [Theory]
        [InlineData("no-boundary")]
        [InlineData("no-segment")]
        [InlineData("wrong-next")]
        [InlineData("unresolved-carrier")]
        [InlineData("unresolved-address")]
        [InlineData("unresolved-target")]
        public void V1_LeafBoxCarrier_GuardsPreserveExistingAlignment(string guard)
        {
            var s = new Scenario();
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.StateMachineAsync,
                new[] { 500, guard == "unresolved-target" ? -1 : 501, 502 }, new[] { 30, 31, 32 });
            var sync = new List<AsyncProfilerStitchSyncFrame>();
            sync.Add(guard == "unresolved-address"
                ? new AsyncProfilerStitchSyncFrame(CodeAddressIndex.Invalid, Scenario.MI(40), AsyncProfilerStitchSyncFrameKind.None, Scenario.MI(31))
                : guard == "unresolved-carrier"
                ? new AsyncProfilerStitchSyncFrame(Scenario.CA(40), MethodIndex.Invalid, AsyncProfilerStitchSyncFrameKind.None, Scenario.MI(31))
                : s.BoxCarrier(40, guard == "wrong-next" ? 32 : 31));
            sync.Add(s.Sync(41, 30));
            if (guard != "no-boundary")
            {
                sync.Add(s.Boundary(42, s_v1Dispatcher));
            }
            sync.Add(s.Sync(43, 99));
            AsyncProfilerCallStack[] segments = guard == "no-segment" ? Array.Empty<AsyncProfilerCallStack>() : new[] { segment };

            AsyncProfilerStitchResult result = s.Run(sync, segments);
            AsyncProfilerStitchResult baseline = s.Run(sync.Select(frame =>
                new AsyncProfilerStitchSyncFrame(frame.CodeAddress, frame.Method, frame.Kind)).ToArray(), segments);
            Assert.Equal(
                baseline.Frames.Select(frame => (frame.Origin, frame.CodeAddress, frame.SegmentFrameIndex)),
                result.Frames.Select(frame => (frame.Origin, frame.CodeAddress, frame.SegmentFrameIndex)));
            Assert.DoesNotContain(result.Frames, frame => frame.IsStateMachineBoxCarrier);
            Assert.DoesNotContain(result.Frames, frame =>
                frame.Presentation == AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void V1_BoxCarrierWithCallees_AdvancesResumptionWithoutClaimingApplicationExecution(
            bool truncated, bool methodEvents)
        {
            var s = new Scenario();
            if (methodEvents)
            {
                s.MarkMethodCompletionObserved(AsyncCallstackKind.StateMachineAsync);
            }
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.StateMachineAsync,
                new[] { 500, 501, 502, 503 }, new[] { 30, 31, 32, 33 },
                completions: methodEvents ? Completed(2) : null);
            var sync = new List<AsyncProfilerStitchSyncFrame>
            {
                s.Sync(40, 90),
                new AsyncProfilerStitchSyncFrame(Scenario.CA(41), MethodIndex.Invalid),
                s.BoxCarrier(42, 32),
                s.BoxCarrier(43, 32),
                s.Sync(44, 31),
                s.Sync(45, 30),
            };
            if (!truncated)
            {
                sync.Add(s.Boundary(46, s_v1Dispatcher));
            }
            sync.Add(s.Sync(47, 99));

            AsyncProfilerStitchResult result = s.Run(sync, new[] { segment });
            Assert.Equal(Scenario.CA(40), result.Frames[0].CodeAddress);
            Assert.Equal(Scenario.CA(41), result.Frames[1].CodeAddress);
            AsyncProfilerStitchedFrame marker = result.Frames[2];
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, marker.Origin);
            Assert.Equal(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames, marker.Presentation);
            Assert.Equal(Scenario.CA(42), marker.CodeAddress);
            Assert.Same(segment, marker.Context);
            AsyncProfilerStitchedFrame target = result.Frames[3];
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget, target.Origin);
            Assert.True(target.IsStateMachineBoxCarrier);
            Assert.Equal(2, target.SegmentFrameIndex);
            Assert.Equal(Scenario.CA(42), target.CodeAddress);
            Assert.Equal(Scenario.MI(32), target.BoxedStateMachineMethod);
            Assert.Equal(Scenario.CA(503), result.Frames[4].CodeAddress);
            Assert.Equal(truncated ? AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot : AsyncProfilerStitchedFrameOrigin.Sync,
                result.Frames[5].Origin);
            Assert.Equal(truncated ? 1 : 0, result.Diagnostics.IncompleteAsyncRootsNormalized);
            Assert.DoesNotContain(result.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
        }

        [Fact]
        public void V1_ApplicationBodyAboveBoxCarrier_TakesPrecedence()
        {
            var s = new Scenario();
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.StateMachineAsync,
                new[] { 500, 501, 502 }, new[] { 30, 31, 32 });
            AsyncProfilerStitchResult result = s.Run(new[]
            {
                s.Sync(40, 31),
                s.BoxCarrier(41, 31),
                s.BoxCarrier(42, 32),
                s.Sync(43, 30),
                s.Boundary(44, s_v1Dispatcher),
                s.Sync(45, 99),
            }, new[] { segment });

            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCurrent, result.Frames[0].Origin);
            Assert.Equal(Scenario.CA(40), result.Frames[0].CodeAddress);
            Assert.Equal(1, result.Frames[0].SegmentFrameIndex);
            Assert.False(result.Frames[0].IsStateMachineBoxCarrier);
            Assert.DoesNotContain(result.Frames, frame =>
                frame.Presentation == AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames);
        }

        [Fact]
        public void V1_CollapsesContiguousBoxInfrastructureRootwardOfDispatcher()
        {
            var s = new Scenario();
            AsyncProfilerCallStack seg = s.Segment(AsyncCallstackKind.StateMachineAsync,
                frameCodeAddrs: new[] { 500, 501 }, frameMethods: new[] { 30, 31 });

            var sync = new[]
            {
                s.Sync(40, 30),
                s.Boundary(41, s_v1Dispatcher),
                s.Boundary(42, s_v1Infrastructure),
                s.Boundary(43, s_v1Infrastructure),
                s.Sync(44, 90), // first non-box frame: preserve and stop collapsing
                s.Sync(45, 91),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { seg });

            Assert.Equal(new[] { Scenario.CA(40), Scenario.CA(501), Scenario.CA(44), Scenario.CA(45) },
                result.Frames.Select(frame => frame.CodeAddress));
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCurrent, result.Frames[0].Origin);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCaller, result.Frames[1].Origin);
            Assert.Equal(2, result.Diagnostics.V1InfrastructureFramesCollapsed);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void V1_UnmatchedCurrent_RetainsDispatcherAndIndexedDispatchTarget(bool prefix, bool unresolved)
        {
            var s = new Scenario();
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.StateMachineAsync,
                new[] { unresolved ? -1 : 500, 501 }, new[] { 30, 31 }, frameMethodIds: new ulong[] { 123, 124 });
            var sync = new List<AsyncProfilerStitchSyncFrame>();
            if (prefix)
            {
                sync.Add(s.Sync(39, 99));
            }
            sync.Add(s.Boundary(40, s_v1Dispatcher));
            sync.Add(s.Boundary(41, s_v1Infrastructure));
            sync.Add(s.Sync(42, 90));

            AsyncProfilerStitchResult result = s.Run(sync, new[] { segment });
            int dispatcher = prefix ? 1 : 0;
            Assert.Equal(sync[dispatcher].CodeAddress, result.Frames[dispatcher].CodeAddress);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, result.Frames[dispatcher].Origin);
            AsyncProfilerStitchedFrame target = result.Frames[dispatcher + 1];
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget, target.Origin);
            Assert.Equal(AsyncProfilerStitchedFrameContextKind.Active, target.ContextKind);
            Assert.Equal(0, target.SegmentFrameIndex);
            Assert.Equal(segment.Frames.CodeAddressAt(0), target.CodeAddress);
            Assert.Equal(MethodIndex.Invalid, target.Method);
            Assert.Same(segment, target.Context);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCaller, result.Frames[dispatcher + 2].Origin);
            Assert.Equal(Scenario.CA(501), result.Frames[dispatcher + 2].CodeAddress);
            Assert.Equal(Scenario.CA(42), result.Frames[dispatcher + 3].CodeAddress);
            Assert.Equal(dispatcher + 4, result.Frames.Count);
            Assert.Equal(1, result.Diagnostics.V1InfrastructureFramesCollapsed);
            Assert.DoesNotContain(result.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
        }

        [Fact]
        public void V1_DispatcherLeaf_SingleFrameSegmentIsNotLost()
        {
            var s = new Scenario();
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.StateMachineAsync,
                new[] { 500 }, new[] { 30 });
            AsyncProfilerStitchResult result = s.Run(
                new[] { s.Boundary(40, s_v1Dispatcher), s.Sync(41, 90) }, new[] { segment });

            Assert.Equal(new[] { Scenario.CA(40), Scenario.CA(500), Scenario.CA(41) },
                result.Frames.Select(frame => frame.CodeAddress));
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget, result.Frames[1].Origin);
        }

        [Fact]
        public void V1_DispatchTargetDoesNotReintroduceCompletedFrames()
        {
            var s = new Scenario();
            s.MarkMethodCompletionObserved(AsyncCallstackKind.StateMachineAsync);
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.StateMachineAsync,
                new[] { 500, 501, 502 }, new[] { 30, 31, 32 }, completions: Completed(1));
            AsyncProfilerStitchResult result = s.Run(
                new[] { s.Boundary(40, s_v1Dispatcher), s.Sync(41, 90) }, new[] { segment });

            Assert.Equal(new[] { Scenario.CA(40), Scenario.CA(501), Scenario.CA(502), Scenario.CA(41) },
                result.Frames.Select(frame => frame.CodeAddress));
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget, result.Frames[1].Origin);
            Assert.Equal(1, result.Frames[1].SegmentFrameIndex);
        }

        [Fact]
        public void V1_UnmatchedCurrentWithoutBoundary_DoesNotInventADispatchTarget()
        {
            var s = new Scenario();
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.StateMachineAsync,
                new[] { 500, 501 }, new[] { 30, 31 });
            AsyncProfilerStitchResult result = s.Run(new[] { s.Sync(40, 90) }, new[] { segment });

            Assert.Equal(new[] { Scenario.CA(40), Scenario.CA(501) },
                result.Frames.Select(frame => frame.CodeAddress));
            Assert.DoesNotContain(result.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget);
            Assert.Equal(1, result.Diagnostics.BoundariesNotFound);
        }

        [Fact]
        public void V1_MatchedMoveNext_DoesNotInsertADispatchTarget()
        {
            var s = new Scenario();
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.StateMachineAsync,
                new[] { 500, 501 }, new[] { 30, 31 });
            AsyncProfilerStitchResult result = s.Run(
                new[] { s.Sync(39, 30), s.Boundary(40, s_v1Dispatcher), s.Sync(41, 90) }, new[] { segment });

            Assert.Equal(new[] { Scenario.CA(39), Scenario.CA(501), Scenario.CA(41) },
                result.Frames.Select(frame => frame.CodeAddress));
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCurrent, result.Frames[0].Origin);
            Assert.DoesNotContain(result.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget);
        }

        [Fact]
        public void V1_StructuralStitchPreservesSynchronousStartupPattern()
        {
            // Leaf-first physical suffix:
            //   CPU work <- generated child MoveNext <- CoreLib builder Start <- logical child kickoff <- boundary.
            // Presentation normalization is a later pipeline stage, so the structural stitcher preserves this
            // synchronous sequence while still stitching the active V1 segment around it.
            var s = new Scenario();
            AsyncProfilerCallStack seg = s.Segment(
                AsyncCallstackKind.StateMachineAsync,
                frameCodeAddrs: new[] { 500, 501 },
                frameMethods: new[] { 30, 31 });

            var sync = new[]
            {
                s.Sync(40, 70), // CPU work
                s.Sync(41, 71, AsyncProfilerStitchSyncFrameKind.V1StateMachineBody),
                s.Sync(42, 72, RuntimeFrameKind.V1Startup),
                s.Sync(43, 73, RuntimeFrameKind.V1Startup),
                s.Sync(44, 30), // child kickoff/current async method
                s.Boundary(45, s_v1Dispatcher),
                s.Sync(46, 99),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { seg });

            Assert.Equal(
                new[]
                {
                    Scenario.CA(40), Scenario.CA(41), Scenario.CA(42), Scenario.CA(43),
                    Scenario.CA(44), Scenario.CA(501), Scenario.CA(46),
                },
                result.Frames.Select(frame => frame.CodeAddress));
        }

        [Fact]
        public void V1_ZeroIpCurrentFrame_PreservesPhysicalFramesWithoutDuplicatingUnknownFrame()
        {
            // The zero-IP frame cannot prove a current-method match. Preserve the physical slice without
            // duplicating that frame as an unresolved logical caller.
            var s = new Scenario();
            AsyncProfilerCallStack seg = s.Segment(AsyncCallstackKind.StateMachineAsync,
                frameCodeAddrs: new[] { 500, 501, -1, 503 },   // C, B, A(0-IP), P1
                frameMethods:   new[] {  30,  31,  0,  33 });   // A's method irrelevant (Invalid ca)

            var sync = new[]
            {
                s.Sync(40, 32),                  // A (current, leaf) -> REAL sync IP + method
                s.Sync(41, 31),                  // B (completed)
                s.Sync(42, 30),                  // C (completed)
                s.Boundary(43, s_v1Dispatcher),
                s.Sync(44, 99),                  // root
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { seg });

            // Keep the unverified current frame physical and append the known remaining caller.
            Assert.Equal(5, result.Frames.Count);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, result.Frames[0].Origin);
            Assert.Equal(Scenario.CA(40), result.Frames[0].CodeAddress);        // real sync leaf, not Invalid
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCaller, result.Frames[3].Origin);
            Assert.Equal(Scenario.CA(503), result.Frames[3].CodeAddress);       // P1
            // No spliced async frame is the Invalid 0-IP current frame.
            Assert.DoesNotContain(result.Frames.Where(f => f.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCaller),
                f => f.CodeAddress == CodeAddressIndex.Invalid);
        }

        [Fact]
        public void V1_MethodObserved_UsesEventCount_NotStackScan()
        {
            // CompleteMethod events observed for V1: the normal completed count comes from the method deltas
            // (GetMethodCompletedFrameCount), NOT the inline sync-stack scan. Segment (leaf-to-root): [Mcur, A1, A2];
            // one method completion -> completed=1 -> current=frames[1]; callers=frames[2].
            var s = new Scenario();
            s.MarkMethodCompletionObserved(AsyncCallstackKind.StateMachineAsync);
            AsyncProfilerCallStack seg = s.Segment(AsyncCallstackKind.StateMachineAsync,
                frameCodeAddrs: new[] { 500, 501, 502 },   // Mcur, A1, A2
                frameMethods:   new[] {  30,  31,  32 },
                completions: Completed(1));                 // one normal completion via events

            var sync = new[]
            {
                s.Sync(40, 31),                 // current -> frames[1] (method 31)
                s.Boundary(43, s_v1Dispatcher),
                s.Sync(44, 99),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { seg });

            // Recorded progress, not the physical-stack scan, selects the current method and remaining caller.
            Assert.Equal(3, result.Frames.Count);
            Assert.Equal(Scenario.CA(40), result.Frames[0].CodeAddress);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCaller, result.Frames[1].Origin);
            Assert.Equal(Scenario.CA(502), result.Frames[1].CodeAddress); // A2 caller
            Assert.Equal(Scenario.CA(44), result.Frames[2].CodeAddress);
            Assert.Equal(0, result.Diagnostics.V1InlineFallbackUsed);
        }

        [Fact]
        public void V1_ExceptionUnwind_BridgesMissingFramesWithoutDoubleCounting()
        {
            // The unwind count permits the missing B between physical matches C and A; it must not advance
            // the matched current position a second time.
            var s = new Scenario();
            AsyncProfilerCallStack seg = s.Segment(AsyncCallstackKind.StateMachineAsync,
                frameCodeAddrs: new[] { 500, 501, 502, 503 },  // C, B, A, P1
                frameMethods:   new[] {  30,  31,  32,  33 },
                exceptionCompletions: Unwound(1));             // B unwound off the stack

            var sync = new[]
            {
                s.Sync(40, 32),                  // A (current, leaf) -> method 32
                s.Sync(42, 30),                  // C (completed inline) -> method 30 (B is gone: unwound)
                s.Boundary(43, s_v1Dispatcher),
                s.Sync(44, 99),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { seg });

            // Expected: [A(AsyncCurrent), P1(Async), root(Sync)]. C completed inline and B unwound, so both
            // physical transition frames are replaced by the logical async callers.
            Assert.Equal(3, result.Frames.Count);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCurrent, result.Frames[0].Origin);
            Assert.Equal(Scenario.CA(40), result.Frames[0].CodeAddress);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCaller, result.Frames[1].Origin);
            Assert.Equal(Scenario.CA(503), result.Frames[1].CodeAddress); // P1 caller
            Assert.Equal(Scenario.CA(44), result.Frames[2].CodeAddress);
            Assert.Equal(1, result.Diagnostics.V1InlineFallbackUsed);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void V1_ExceptionBeforeInlineCompletion_AlignsPastUnwoundFrame(bool unresolved)
        {
            // A is absent after an unwind, with or without a resolved identity. B/C must still match in order.
            var s = new Scenario();
            AsyncProfilerCallStack seg = s.Segment(AsyncCallstackKind.StateMachineAsync,
                frameCodeAddrs: new[] { unresolved ? -1 : 500, 501, 502, 503 }, // A(unwound), B(completed), C(current), P
                frameMethods: new[] { unresolved ? 0 : 30, 31, 32, 33 },
                exceptionCompletions: Unwound(1));

            var sync = new[]
            {
                s.Sync(40, 32),                  // C current
                s.Sync(41, 31),                  // B completed inline
                s.Boundary(42, s_v1Dispatcher),
                s.Sync(43, 99),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { seg });

            Assert.Equal(new[] { Scenario.CA(40), Scenario.CA(503), Scenario.CA(43) },
                result.Frames.Select(frame => frame.CodeAddress));
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCurrent, result.Frames[0].Origin);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCaller, result.Frames[1].Origin);
            Assert.Equal(1, result.Diagnostics.V1InlineFallbackUsed);
        }

        [Fact]
        public void V1_MethodObservedPlusException_CombinesBothMechanisms()
        {
            // CompleteMethod observed AND an Unwind event: completed = method deltas + exception deltas (the total
            // GetCompletedFrameCount). Segment (leaf-to-root): [C0, C1, Mcur, A1]; 1 method + 1 unwind -> completed=2
            // -> current=frames[2]=Mcur; callers=A1. The inline stack scan is not used.
            var s = new Scenario();
            s.MarkMethodCompletionObserved(AsyncCallstackKind.StateMachineAsync);
            AsyncProfilerCallStack seg = s.Segment(AsyncCallstackKind.StateMachineAsync,
                frameCodeAddrs: new[] { 500, 501, 502, 503 },  // C0, C1, Mcur, A1
                frameMethods:   new[] {  30,  31,  32,  33 },
                completions: Completed(1),                      // 1 normal completion via events
                exceptionCompletions: Unwound(1));             // + 1 exceptional unwind

            var sync = new[]
            {
                s.Sync(40, 32),                  // current -> frames[2] (method 32)
                s.Boundary(43, s_v1Dispatcher),
                s.Sync(44, 99),
            };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { seg });

            Assert.Equal(3, result.Frames.Count);
            Assert.Equal(Scenario.CA(40), result.Frames[0].CodeAddress);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCaller, result.Frames[1].Origin);
            Assert.Equal(Scenario.CA(503), result.Frames[1].CodeAddress); // A1 caller
            Assert.Equal(Scenario.CA(44), result.Frames[2].CodeAddress);
            Assert.Equal(0, result.Diagnostics.V1InlineFallbackUsed); // events used, not the scan
        }
    }
}
