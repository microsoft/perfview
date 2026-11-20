// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Diagnostics.Tracing.Computers;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;

using Xunit;

namespace TraceEventTests
{
    /// <summary>
    /// Tests parent-context insertion by <see cref="AsyncProfilerParentContextBuilder"/> on top of stitched frames.
    /// </summary>
    public partial class AsyncProfilerCallStackStitcherTests
    {
        [Theory]
        [InlineData(null, 1)]
        [InlineData(2, 2)]
        [InlineData(3, 3)]
        public void ParentContexts_RespectsDefaultAndExplicitParentDepth(int? maximumDepth, int expectedParents)
        {
            var s = new Scenario();
            s.MarkMethodCompletionObserved(AsyncCallstackKind.RuntimeAsync);
            var index = new AsyncProfilerCallStacksIndex();
            ProcessIndex process = (ProcessIndex)1;
            var sampledThread = new AsyncProfilerThreadKey(process, 100);
            var p3Thread = new AsyncProfilerThreadKey(process, 200);
            var p2Thread = new AsyncProfilerThreadKey(process, 300);
            var p1Thread = new AsyncProfilerThreadKey(process, 400);

            AsyncProfilerCallStack outer = AddIndexedSegment(
                s, index, sampledThread, dispatcherId: 40, depth: 0,
                startQpc: 0, endQpc: 2000, firstCodeAddress: 400);
            AsyncProfilerCallStack p3 = AddIndexedSegment(
                s, index, p3Thread, dispatcherId: 30, depth: 0,
                startQpc: 200, endQpc: 800, firstCodeAddress: 300);
            AsyncProfilerCallStack p2 = AddIndexedSegment(
                s, index, p2Thread, dispatcherId: 20, depth: 0,
                startQpc: 400, endQpc: 900, firstCodeAddress: 200);
            AsyncProfilerCallStack p1 = AddIndexedSegment(
                s, index, p1Thread, dispatcherId: 10, depth: 0,
                startQpc: 600, endQpc: 950, firstCodeAddress: 100);
            AsyncProfilerCallStack inner = AddIndexedSegment(
                s, index, sampledThread, dispatcherId: 1, depth: 1,
                startQpc: 1000, endQpc: 2000, firstCodeAddress: 10);

            index.AddDispatcherInfo(sampledThread, dispatcherId: 30, parentDispatcherId: 40, creationQpc: 100);
            index.AddDispatcherInfo(p3Thread, dispatcherId: 20, parentDispatcherId: 30, creationQpc: 300);
            index.AddDispatcherInfo(p2Thread, dispatcherId: 10, parentDispatcherId: 20, creationQpc: 500);
            index.AddDispatcherInfo(p1Thread, dispatcherId: 1, parentDispatcherId: 10, creationQpc: 800);

            var sync = new[]
            {
                s.Sync(50, 10),
                s.Boundary(51, Wrapper(0)),
                s.Sync(52, 400),
                s.Boundary(53, Wrapper(0)),
                s.Sync(54, 999),
            };

            AsyncProfilerStitchResult structural = s.Run(sync, new[] { inner, outer });
            var frames = structural.Frames.ToList();
            var pipeline = new AsyncProfilerCallStackTransformPipeline();
            if (maximumDepth is int depth)
            {
                new AsyncCallStackTransforms(pipeline).MaximumParentContextDepth = depth;
            }

            new AsyncProfilerParentContextBuilder().AddParentContexts(
                index,
                new[] { inner, outer },
                structural.SegmentPlacements,
                frames,
                pipeline.MaximumParentContextDepth);

            // Parents are inserted nearest first between the inner and outer active call stacks.
            AsyncProfilerCallStack[] contexts = new[] { inner }
                .Concat(new[] { p1, p2, p3 }.Take(expectedParents))
                .Concat(new[] { outer })
                .ToArray();
            Assert.Equal(contexts.Length * 2 + 1, frames.Count);
            for (int i = 0; i < contexts.Length; i++)
            {
                bool isParent = i != 0 && i != contexts.Length - 1;
                for (int slot = 0; slot < 2; slot++)
                {
                    AsyncProfilerStitchedFrame frame = frames[i * 2 + slot];
                    Assert.Same(contexts[i], frame.Context);
                    Assert.Equal(
                        isParent ? AsyncProfilerStitchedFrameContextKind.ParentContext : AsyncProfilerStitchedFrameContextKind.Active,
                        frame.ContextKind);
                    Assert.Equal(
                        isParent ? AsyncProfilerStitchedFrameOrigin.AsyncContextParent
                            : slot == 0 ? AsyncProfilerStitchedFrameOrigin.AsyncCurrent : AsyncProfilerStitchedFrameOrigin.AsyncCaller,
                        frame.Origin);
                }
            }
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, frames[frames.Count - 1].Origin);
            Assert.Null(frames[frames.Count - 1].Context);
            Assert.Equal(
                new[] { 100, 101, 200, 201, 300, 301 }.Take(expectedParents * 2).Select(Scenario.CA),
                frames
                    .Where(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncContextParent)
                    .Select(frame => frame.CodeAddress));
        }

        [Fact]
        public void ParentContexts_PreservesOverlappingDistinctParentCallStacks()
        {
            var s = new Scenario();
            var index = new AsyncProfilerCallStacksIndex();
            ProcessIndex process = (ProcessIndex)1;
            var childThread = new AsyncProfilerThreadKey(process, 100);
            var parentThread = new AsyncProfilerThreadKey(process, 200);
            var grandparentThread = new AsyncProfilerThreadKey(process, 300);
            AsyncProfilerCallStack child = AddIndexedSegment(
                s, index, childThread, dispatcherId: 1, depth: 0,
                startQpc: 100, endQpc: 200, firstCodeAddress: 10, frameCount: 3);
            AsyncProfilerCallStack parent = AddIndexedSegment(
                s, index, parentThread, dispatcherId: 2, depth: 0,
                startQpc: 50, endQpc: 99, firstCodeAddress: 11, frameCount: 3);
            AsyncProfilerCallStack grandparent = AddIndexedSegment(
                s, index, grandparentThread, dispatcherId: 3, depth: 0,
                startQpc: 0, endQpc: 99, firstCodeAddress: 12, frameCount: 3);
            index.AddDispatcherInfo(
                parentThread, dispatcherId: 1, parentDispatcherId: 2, creationQpc: 90);
            index.AddDispatcherInfo(
                grandparentThread, dispatcherId: 2, parentDispatcherId: 3, creationQpc: 40);

            var frames = new List<AsyncProfilerStitchedFrame>
            {
                AsyncProfilerStitchedFrame.CreateAsync(child, 0),
                AsyncProfilerStitchedFrame.CreateAsync(child, 1),
                AsyncProfilerStitchedFrame.CreateAsync(child, 2),
            };
            var placements = new[]
            {
                new AsyncProfilerCallStackPlacement(child, leafIndex: 0, rootIndexExclusive: 3),
            };

            new AsyncProfilerParentContextBuilder().AddParentContexts(
                index,
                new[] { child },
                placements,
                frames,
                maximumDepth: 2);

            AsyncProfilerStitchedFrame[] historical = frames
                .Where(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncContextParent)
                .ToArray();
            Assert.Equal(
                new[]
                {
                    Scenario.CA(11), Scenario.CA(12), Scenario.CA(13),
                    Scenario.CA(12), Scenario.CA(13), Scenario.CA(14),
                },
                historical.Select(frame => frame.CodeAddress));
            Assert.All(historical.Take(3), frame => Assert.Same(parent, frame.Context));
            Assert.All(historical.Skip(3), frame => Assert.Same(grandparent, frame.Context));
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 2)] // recursive: identical frames and states
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 3)] // same frames, different V1 state
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 0)]      // same frames, different async kind
        public void ParentContexts_DoesNotTrimParentThatSharesChildFrames(int childKindValue, int parentSecondState)
        {
            var childKind = (AsyncCallstackKind)childKindValue;
            bool v1Child = childKind == AsyncCallstackKind.StateMachineAsync;
            var s = new Scenario();
            var index = new AsyncProfilerCallStacksIndex();
            ProcessIndex process = (ProcessIndex)1;
            var childThread = new AsyncProfilerThreadKey(process, 100);
            var parentThread = new AsyncProfilerThreadKey(process, 200);
            AsyncProfilerCallStack child = AddIndexedSegment(
                s, index, childThread, dispatcherId: 1, depth: 0,
                startQpc: 100, endQpc: 200, firstCodeAddress: 10,
                kind: childKind,
                frameStates: v1Child ? new[] { 1, 2 } : null);
            AsyncProfilerCallStack parent = AddIndexedSegment(
                s, index, parentThread, dispatcherId: 2, depth: 0,
                startQpc: 0, endQpc: 90, firstCodeAddress: 10,
                kind: AsyncCallstackKind.StateMachineAsync,
                frameStates: v1Child ? new[] { 1, parentSecondState } : null);
            index.AddDispatcherInfo(
                parentThread, dispatcherId: 1, parentDispatcherId: 2, creationQpc: 50);

            var frames = new List<AsyncProfilerStitchedFrame>
            {
                AsyncProfilerStitchedFrame.CreateAsync(child, 0),
                AsyncProfilerStitchedFrame.CreateAsync(child, 1),
            };
            var placements = new[]
            {
                new AsyncProfilerCallStackPlacement(child, leafIndex: 0, rootIndexExclusive: 2),
            };

            new AsyncProfilerParentContextBuilder().AddParentContexts(
                index,
                new[] { child },
                placements,
                frames,
                AsyncProfilerParentContextBuilder.DefaultMaximumDepth);

            AsyncProfilerStitchedFrame[] historical = frames
                .Where(frame =>
                    frame.ContextKind == AsyncProfilerStitchedFrameContextKind.ParentContext)
                .ToArray();
            Assert.Equal(2, historical.Length);
            Assert.All(historical, frame => Assert.Same(parent, frame.Context));
        }

        [Fact]
        public void ParentContexts_CycleStopsBeforeRepeatingACallStack()
        {
            var s = new Scenario();
            var index = new AsyncProfilerCallStacksIndex();
            ProcessIndex process = (ProcessIndex)1;
            var aThread = new AsyncProfilerThreadKey(process, 100);
            var bThread = new AsyncProfilerThreadKey(process, 200);
            AsyncProfilerCallStack a = AddIndexedSegment(
                s, index, aThread, dispatcherId: 1, depth: 0,
                startQpc: 0, endQpc: 100, firstCodeAddress: 10);
            AsyncProfilerCallStack b = AddIndexedSegment(
                s, index, bThread, dispatcherId: 2, depth: 0,
                startQpc: 0, endQpc: 100, firstCodeAddress: 20);

            index.AddDispatcherInfo(
                bThread, dispatcherId: 1, parentDispatcherId: 2, creationQpc: 0);
            index.AddDispatcherInfo(
                aThread, dispatcherId: 2, parentDispatcherId: 1, creationQpc: 0);

            var frames = new List<AsyncProfilerStitchedFrame>
            {
                AsyncProfilerStitchedFrame.CreateAsync(a, 0),
            };
            var placements = new[]
            {
                new AsyncProfilerCallStackPlacement(a, leafIndex: 0, rootIndexExclusive: 1),
            };
            var parentContextBuilder = new AsyncProfilerParentContextBuilder();

            Assert.True(parentContextBuilder.HasParentContextsToAdd(index, new[] { a }));
            parentContextBuilder.AddParentContexts(
                index,
                new[] { a },
                placements,
                frames,
                maximumDepth: 3);

            AsyncProfilerStitchedFrame[] parents = frames
                .Where(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncContextParent)
                .ToArray();
            Assert.Equal(b.Frames.FrameCount, parents.Length);
            Assert.All(parents, frame => Assert.Same(b, frame.Context));
            Assert.DoesNotContain(parents, frame => ReferenceEquals(a, frame.Context));
        }

        [Fact]
        public void ParentContexts_SharedParentIsInsertedForEachIndependentSample()
        {
            var s = new Scenario();
            var index = new AsyncProfilerCallStacksIndex();
            ProcessIndex process = (ProcessIndex)1;
            var parentThread = new AsyncProfilerThreadKey(process, 100);
            var firstChildThread = new AsyncProfilerThreadKey(process, 200);
            var secondChildThread = new AsyncProfilerThreadKey(process, 300);
            AsyncProfilerCallStack parent = AddIndexedSegment(
                s, index, parentThread, dispatcherId: 10, depth: 0,
                startQpc: 0, endQpc: 500, firstCodeAddress: 100);
            AsyncProfilerCallStack firstChild = AddIndexedSegment(
                s, index, firstChildThread, dispatcherId: 1, depth: 0,
                startQpc: 100, endQpc: 200, firstCodeAddress: 10);
            AsyncProfilerCallStack secondChild = AddIndexedSegment(
                s, index, secondChildThread, dispatcherId: 2, depth: 0,
                startQpc: 200, endQpc: 300, firstCodeAddress: 20);

            index.AddDispatcherInfo(
                parentThread, dispatcherId: 1, parentDispatcherId: 10, creationQpc: 50);
            index.AddDispatcherInfo(
                parentThread, dispatcherId: 2, parentDispatcherId: 10, creationQpc: 150);

            var parentContextBuilder = new AsyncProfilerParentContextBuilder();
            AssertSharedParent(firstChild);
            AssertSharedParent(secondChild);

            void AssertSharedParent(AsyncProfilerCallStack child)
            {
                var frames = new List<AsyncProfilerStitchedFrame>
                {
                    AsyncProfilerStitchedFrame.CreateAsync(child, 0),
                };
                var placements = new[]
                {
                    new AsyncProfilerCallStackPlacement(child, leafIndex: 0, rootIndexExclusive: 1),
                };

                Assert.True(parentContextBuilder.HasParentContextsToAdd(index, new[] { child }));
                parentContextBuilder.AddParentContexts(
                    index,
                    new[] { child },
                    placements,
                    frames,
                    AsyncProfilerParentContextBuilder.DefaultMaximumDepth);

                AsyncProfilerStitchedFrame[] parents = frames
                    .Where(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncContextParent)
                    .ToArray();
                Assert.Equal(parent.Frames.FrameCount, parents.Length);
                Assert.All(parents, frame => Assert.Same(parent, frame.Context));
            }
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 1, 1, "normal-only")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 1, 1, "normal-only")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 3, 0, "normal-only")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 3, 0, "normal-only")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 0, 3, "normal-only")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 0, 3, "normal-only")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 1, 1, "neither")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 1, 1, "neither")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 1, 1, "unwind-only")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 1, 1, "unwind-only")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 1, 1, "other-kind")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 1, 1, "other-kind")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 1, 1, "other-process")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 1, 1, "other-process")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 1, 1, "previous-epoch")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 1, 1, "previous-epoch")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 1, 1, "later-epoch")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 1, 1, "later-epoch")]
        public void ContextParent_WithoutCompleteProgressEvidence_PreservesCapturedFramesAndStates(
            int parentKindValue,
            int methodCompleted,
            int exceptionCompleted,
            string availability)
        {
            var parentKind = (AsyncCallstackKind)parentKindValue;
            var s = new Scenario();
            s.MarkMethodCompletionObserved(AsyncCallstackKind.RuntimeAsync);
            var index = new AsyncProfilerCallStacksIndex();
            ProcessIndex process = (ProcessIndex)1;
            var parentThread = new AsyncProfilerThreadKey(process, 100);
            var childThread = new AsyncProfilerThreadKey(process, 200);
            index.StartCompletionAvailabilityEpoch(process, startQpc: 0);
            switch (availability)
            {
                case "normal-only":
                    index.MarkMethodCompletionObserved(process, parentKind, timestampQpc: 20);
                    break;
                case "neither":
                    break;
                case "unwind-only":
                    index.MarkExceptionCompletionObserved(process, parentKind, timestampQpc: 30);
                    break;
                case "other-kind":
                    index.MarkMethodCompletionObserved(process, parentKind, timestampQpc: 20);
                    index.MarkExceptionCompletionObserved(
                        process,
                        parentKind == AsyncCallstackKind.RuntimeAsync
                            ? AsyncCallstackKind.StateMachineAsync : AsyncCallstackKind.RuntimeAsync,
                        timestampQpc: 30);
                    break;
                case "other-process":
                    index.MarkMethodCompletionObserved(process, parentKind, timestampQpc: 20);
                    index.StartCompletionAvailabilityEpoch((ProcessIndex)2, startQpc: 0);
                    index.MarkExceptionCompletionObserved((ProcessIndex)2, parentKind, timestampQpc: 30);
                    break;
                case "previous-epoch":
                    index.MarkMethodCompletionObserved(process, parentKind, timestampQpc: 1);
                    index.MarkExceptionCompletionObserved(process, parentKind, timestampQpc: 1);
                    index.StartCompletionAvailabilityEpoch(process, startQpc: 5);
                    break;
                case "later-epoch":
                    index.MarkMethodCompletionObserved(process, parentKind, timestampQpc: 20);
                    index.StartCompletionAvailabilityEpoch(process, startQpc: 40);
                    index.MarkExceptionCompletionObserved(process, parentKind, timestampQpc: 45);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(availability));
            }

            AsyncProfilerCallStack parent = AddIndexedSegment(
                s, index, parentThread, dispatcherId: 10, depth: 0,
                startQpc: 10, endQpc: 90, firstCodeAddress: 100,
                frameCount: 3,
                methodCompletions: methodCompleted == 0
                    ? Array.Empty<AsyncProfilerCallStack.CompletionDelta>()
                    : new[] { new AsyncProfilerCallStack.CompletionDelta(20, methodCompleted) },
                exceptionCompletions: exceptionCompleted == 0
                    ? Array.Empty<AsyncProfilerCallStack.CompletionDelta>()
                    : new[] { new AsyncProfilerCallStack.CompletionDelta(30, exceptionCompleted) },
                kind: parentKind,
                frameStates: new[] { 0, 1, 4 });
            AsyncProfilerCallStack child = AddIndexedSegment(
                s, index, childThread, dispatcherId: 1, depth: 0,
                startQpc: 100, endQpc: 2000, firstCodeAddress: 10);
            index.AddDispatcherInfo(
                parentThread, dispatcherId: 1, parentDispatcherId: 10, creationQpc: 50);

            var sync = new[]
            {
                s.Sync(50, 10),
                s.Boundary(51, Wrapper(0)),
                s.Sync(52, 999),
            };
            AsyncProfilerStitchResult structural = s.Run(sync, new[] { child });
            var frames = structural.Frames.ToList();

            new AsyncProfilerParentContextBuilder().AddParentContexts(
                index,
                new[] { child },
                structural.SegmentPlacements,
                frames,
                AsyncProfilerParentContextBuilder.DefaultMaximumDepth);

            Assert.Equal(
                new[] { Scenario.CA(100), Scenario.CA(101), Scenario.CA(102) },
                frames
                    .Where(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncContextParent)
                    .Select(frame => frame.CodeAddress));
            Assert.Equal(
                parentKind == AsyncCallstackKind.StateMachineAsync
                    ? new[] { 0, 1, 4 }
                    : new[] { 0, 0, 0 },
                frames
                    .Where(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncContextParent)
                    .Select(frame => frame.Segment.FrameStateAt(frame.SegmentFrameIndex)));
            Assert.All(
                frames.Where(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncContextParent),
                frame =>
                {
                    Assert.Same(parent, frame.Context);
                    Assert.Equal(AsyncProfilerStitchedFrameContextKind.ParentContext, frame.ContextKind);
                });
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 0, 0)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 0, 0)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 1, 1)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 1, 1)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 3, 1)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 3, 1)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 5, 5)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 5, 5)]
        public void ContextParent_ProjectsRecordedProgressAtCreationWithBothRecordTypes(
            int parentKindValue,
            int methodCompleted,
            int exceptionCompleted)
        {
            var parentKind = (AsyncCallstackKind)parentKindValue;
            var s = new Scenario();
            s.MarkMethodCompletionObserved(AsyncCallstackKind.RuntimeAsync);
            var index = new AsyncProfilerCallStacksIndex();
            ProcessIndex process = (ProcessIndex)1;
            var parentThread = new AsyncProfilerThreadKey(process, 100);
            var childThread = new AsyncProfilerThreadKey(process, 200);
            index.StartCompletionAvailabilityEpoch(process, startQpc: 0);
            index.MarkMethodCompletionObserved(process, parentKind, timestampQpc: 20);
            index.MarkExceptionCompletionObserved(process, parentKind, timestampQpc: 50);
            int[] states = { 0, 1, 4, 7 };
            AsyncProfilerCallStack parent = AddIndexedSegment(
                s, index, parentThread, dispatcherId: 10, depth: 0,
                startQpc: 10, endQpc: 90, firstCodeAddress: 100, frameCount: states.Length,
                methodCompletions: new[]
                {
                    new AsyncProfilerCallStack.CompletionDelta(20, methodCompleted),
                    new AsyncProfilerCallStack.CompletionDelta(60, 4),
                },
                exceptionCompletions: new[]
                {
                    new AsyncProfilerCallStack.CompletionDelta(50, exceptionCompleted),
                    new AsyncProfilerCallStack.CompletionDelta(70, 1),
                },
                kind: parentKind, frameStates: states);
            AsyncProfilerCallStack child = AddIndexedSegment(
                s, index, childThread, dispatcherId: 1, depth: 0,
                startQpc: 100, endQpc: 2000, firstCodeAddress: 10);
            index.AddDispatcherInfo(
                parentThread, dispatcherId: 1, parentDispatcherId: 10, creationQpc: 50);
            var sync = new[]
            {
                s.Sync(50, 10),
                s.Boundary(51, Wrapper(0)),
                s.Sync(52, 999),
            };
            AsyncProfilerStitchResult structural = s.Run(sync, new[] { child });
            var parentContextBuilder = new AsyncProfilerParentContextBuilder();
            int completed = Math.Min(states.Length, methodCompleted + exceptionCompleted);

            for (int pass = 0; pass < 2; pass++)
            {
                Assert.True(parentContextBuilder.HasParentContextsToAdd(index, new[] { child }));
                var frames = structural.Frames.ToList();
                parentContextBuilder.AddParentContexts(
                    index, new[] { child }, structural.SegmentPlacements, frames,
                    AsyncProfilerParentContextBuilder.DefaultMaximumDepth);
                AsyncProfilerStitchedFrame[] parents = frames.Where(frame =>
                    frame.ContextKind == AsyncProfilerStitchedFrameContextKind.ParentContext).ToArray();

                Assert.Equal(
                    Enumerable.Range(completed, states.Length - completed).Select(i => Scenario.CA(100 + i)),
                    parents.Select(frame => frame.CodeAddress));
                for (int i = 0; i < parents.Length; i++)
                {
                    AsyncProfilerStitchedFrame frame = parents[i];
                    Assert.Same(parent, frame.Context);
                    Assert.Equal(completed + i, frame.SegmentFrameIndex);
                    Assert.Equal(
                        parentKind == AsyncCallstackKind.StateMachineAsync ? states[completed + i] : 0,
                        frame.Segment.FrameStateAt(frame.SegmentFrameIndex));
                    Assert.Equal(
                        i == 0 ? AsyncProfilerStitchedFrameOrigin.AsyncContextParentCurrent
                            : AsyncProfilerStitchedFrameOrigin.AsyncContextParent,
                        frame.Origin);
                }
                Assert.Equal(
                    structural.Frames.Select(frame => frame.CodeAddress),
                    frames.Where(frame => frame.ContextKind !=
                        AsyncProfilerStitchedFrameContextKind.ParentContext).Select(frame => frame.CodeAddress));
            }
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync)]
        public void ContextParent_UsesEachChildCreationQpcForDeeperHistory(int parentKindValue)
        {
            var parentKind = (AsyncCallstackKind)parentKindValue;
            var s = new Scenario();
            s.MarkMethodCompletionObserved(AsyncCallstackKind.RuntimeAsync);
            var index = new AsyncProfilerCallStacksIndex();
            ProcessIndex process = (ProcessIndex)1;
            var rootThread = new AsyncProfilerThreadKey(process, 100);
            var parentThread = new AsyncProfilerThreadKey(process, 200);
            var childThread = new AsyncProfilerThreadKey(process, 300);
            index.StartCompletionAvailabilityEpoch(process, startQpc: 0);
            index.MarkMethodCompletionObserved(process, parentKind, timestampQpc: 20);
            index.MarkExceptionCompletionObserved(process, parentKind, timestampQpc: 30);
            AddIndexedSegment(
                s, index, rootThread, dispatcherId: 20, depth: 0,
                startQpc: 0, endQpc: 40, firstCodeAddress: 100, frameCount: 4,
                methodCompletions: new[]
                {
                    new AsyncProfilerCallStack.CompletionDelta(20, 1),
                    new AsyncProfilerCallStack.CompletionDelta(35, 4),
                },
                exceptionCompletions: new[] { new AsyncProfilerCallStack.CompletionDelta(30, 1) },
                kind: parentKind);
            AddIndexedSegment(
                s, index, parentThread, dispatcherId: 10, depth: 0,
                startQpc: 50, endQpc: 90, firstCodeAddress: 200, frameCount: 3,
                methodCompletions: new[]
                {
                    new AsyncProfilerCallStack.CompletionDelta(60, 1),
                    new AsyncProfilerCallStack.CompletionDelta(85, 2),
                },
                exceptionCompletions: new[] { new AsyncProfilerCallStack.CompletionDelta(70, 1) },
                kind: parentKind);
            AsyncProfilerCallStack child = AddIndexedSegment(
                s, index, childThread, dispatcherId: 1, depth: 0,
                startQpc: 100, endQpc: 2000, firstCodeAddress: 10);
            index.AddDispatcherInfo(rootThread, dispatcherId: 10, parentDispatcherId: 20, creationQpc: 30);
            index.AddDispatcherInfo(parentThread, dispatcherId: 1, parentDispatcherId: 10, creationQpc: 80);
            AsyncProfilerStitchResult structural = s.Run(
                new[] { s.Sync(50, 10), s.Boundary(51, Wrapper(0)), s.Sync(52, 999) },
                new[] { child });
            var frames = structural.Frames.ToList();

            new AsyncProfilerParentContextBuilder().AddParentContexts(
                index, new[] { child }, structural.SegmentPlacements, frames, maximumDepth: 2);

            AsyncProfilerStitchedFrame[] parents = frames.Where(frame =>
                frame.ContextKind == AsyncProfilerStitchedFrameContextKind.ParentContext).ToArray();
            Assert.Equal(
                new[] { Scenario.CA(202), Scenario.CA(102), Scenario.CA(103) },
                parents.Select(frame => frame.CodeAddress));
            Assert.Equal(
                new[]
                {
                    AsyncProfilerStitchedFrameOrigin.AsyncContextParentCurrent,
                    AsyncProfilerStitchedFrameOrigin.AsyncContextParentCurrent,
                    AsyncProfilerStitchedFrameOrigin.AsyncContextParent,
                },
                parents.Select(frame => frame.Origin));
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, (int)AsyncCallstackKind.RuntimeAsync)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, (int)AsyncCallstackKind.StateMachineAsync)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, (int)AsyncCallstackKind.RuntimeAsync)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, (int)AsyncCallstackKind.StateMachineAsync)]
        public void ParentContexts_PreservesParentKindAcrossMixedKinds(
            int childKindValue,
            int parentKindValue)
        {
            var childKind = (AsyncCallstackKind)childKindValue;
            var parentKind = (AsyncCallstackKind)parentKindValue;
            var s = new Scenario();
            var index = new AsyncProfilerCallStacksIndex();
            ProcessIndex process = (ProcessIndex)1;
            var parentThread = new AsyncProfilerThreadKey(process, 100);
            var childThread = new AsyncProfilerThreadKey(process, 200);
            AsyncProfilerCallStack parent = AddIndexedSegment(
                s, index, parentThread, dispatcherId: 10, depth: 0,
                startQpc: 0, endQpc: 90, firstCodeAddress: 100,
                kind: parentKind);
            AsyncProfilerCallStack child = AddIndexedSegment(
                s, index, childThread, dispatcherId: 1, depth: 0,
                startQpc: 100, endQpc: 2000, firstCodeAddress: 10,
                kind: childKind);
            index.AddDispatcherInfo(
                parentThread, dispatcherId: 1, parentDispatcherId: 10, creationQpc: 50);

            var frames = new List<AsyncProfilerStitchedFrame>
            {
                AsyncProfilerStitchedFrame.CreateAsync(child, 0),
            };
            var placements = new[]
            {
                new AsyncProfilerCallStackPlacement(child, leafIndex: 0, rootIndexExclusive: 1),
            };

            new AsyncProfilerParentContextBuilder().AddParentContexts(
                index,
                new[] { child },
                placements,
                frames,
                AsyncProfilerParentContextBuilder.DefaultMaximumDepth);

            AsyncProfilerStitchedFrame[] parentContextFrames =
                frames.Where(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncContextParent).ToArray();
            Assert.Equal(parent.Frames.FrameCount, parentContextFrames.Length);
            Assert.All(parentContextFrames, frame =>
            {
                Assert.Same(parent.Frames, frame.Segment);
                Assert.Equal(
                    parentKind == AsyncCallstackKind.StateMachineAsync
                        ? AsyncProfilerStitchedFramePresentation.LogicalStateMachineMethod
                        : AsyncProfilerStitchedFramePresentation.Native,
                    frame.Presentation);
            });
        }
    }
}
