// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Diagnostics.Tracing.Computers;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;
using Microsoft.Diagnostics.Tracing.Stacks;

using Xunit;

using static Microsoft.Diagnostics.Tracing.Computers.AsyncProfilerRuntimeSupport;

namespace TraceEventTests
{
    public partial class AsyncProfilerStitchIntegrationTests
    {
        [Theory]
        [InlineData(false, false, true, false)]
        [InlineData(false, true, true, false)]
        [InlineData(true, false, true, false)]
        [InlineData(true, true, true, false)]
        [InlineData(true, false, false, false)]
        [InlineData(true, false, true, true)]
        [InlineData(false, false, true, true)]
        [InlineData(false, true, true, true)]
        [InlineData(true, true, true, true)]
        public void StateMachineAsync_CompletedUnwind_AttributesVerifiedSpanToLogicalRoot(
            bool truncated, bool enableInfrastructureCleanup, bool enableFrameAnnotations, bool preservePrefix)
        {
            StitchScenario scenario = CreateV1CompletedUnwindScenario(truncated);
            int unwindLength = scenario.Sync.Length - (truncated ? 0 : 3);
            Frame root = scenario.Async.Last().Logical("Scenario.RootAsync", enableFrameAnnotations ? -2 : (int?)null);
            scenario.EnableInfrastructureCleanup = enableInfrastructureCleanup;
            scenario.EnableFrameAnnotations = enableFrameAnnotations;
            scenario.ExpectedStitched = new[] { root }
                .Concat(truncated ? Array.Empty<Frame>() : new[] { Frame.ThreadPoolDispatch, Frame.WorkerThreadStart })
                .ToArray();
            if (preservePrefix)
            {
                Frame observer = Frame.App("Observer.ArbitraryCallee");
                Frame marker = scenario.Sync[0].DispatcherFrames();
                scenario.Sync = new[] { observer }.Concat(scenario.Sync).ToArray();
                scenario.ExpectedStitched = new[] { observer, marker }.Concat(scenario.ExpectedStitched).ToArray();
            }
            scenario.ExpectedWithoutNormalization =
                scenario.Sync.Where(frame => frame != Frame.V1MoveNextAsDispatcher).ToArray();
            scenario.ExpectedDiagnostics = new CallStackDiagnosticsExpectation
            {
                SegmentsProcessed = 2,
                BoundariesNotFound = truncated ? 2 : 0,
                IncompleteAsyncRootsNormalized = truncated ? 2 : 0,
                V1CompletionUnwindFramesCollapsed = unwindLength * 2,
            };
            scenario.AssertTransformContext = context =>
            {
                AsyncProfilerStitchedFrame unwind = Assert.Single(context.Frames.Where(
                    frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletionReturn));
                Assert.Equal(3, unwind.SegmentFrameIndex);
                Assert.Equal(7, unwind.Segment.FrameStateAt(unwind.SegmentFrameIndex));
                Assert.Equal(MethodIndex.Invalid, unwind.Method);
                Assert.Equal(unwind.Segment.CodeAddressAt(3), unwind.CodeAddress);
                Assert.False(unwind.IsStateMachineBoxCarrier);
                AsyncProfilerStitchedFrame[] markers = context.Frames.Where(frame =>
                    frame.Presentation == AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames).ToArray();
                Assert.Equal(preservePrefix ? 1 : 0, markers.Length);
                if (preservePrefix)
                {
                    Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, markers[0].Origin);
                    Assert.Same(unwind.Context, markers[0].Context);
                    Assert.Contains("AsyncStateMachineBox", context.TraceLog.CodeAddresses[markers[0].CodeAddress].FullMethodName);
                    Assert.Equal(2, context.Frames.IndexOf(unwind));
                }
                Assert.DoesNotContain(context.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody);
                Assert.Equal(truncated ? 1 : 0,
                    context.Frames.Count(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot));
            };
            scenario.AssertStitchedStackSource = source =>
            {
                int unwinds = 0;
                for (int i = 0; i < source.CallFrameIndexLimit; i++)
                {
                    var frameIndex = (StackSourceFrameIndex)i;
                    if (!source.TryGetAsyncProfilerCallStackFrameInfo(frameIndex, out AsyncProfilerCallStackSourceFrameInfo info))
                    {
                        continue;
                    }
                    unwinds++;
                    Assert.True(info.IsCompletionUnwind);
                    Assert.False(info.IsCompleting);
                    Assert.False(info.IsCurrent);
                    Assert.False(info.IsDispatchTarget);
                    Assert.Equal(7, info.State);
                    Assert.Contains("Scenario+<RootAsync>d__4.MoveNext",
                        source.TraceLog.CodeAddresses[info.CodeAddress].FullMethodName);
                }
                Assert.Equal(1, unwinds);
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void StateMachineAsync_ValueTaskCompletedUnwind_PreservesNativeWorkAndLogicalRoot(
            bool genericBuilder, bool cleanup)
        {
            Frame yield = Frame.CoreLib(
                "System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter+<>c.<OutputCorrelationEtwEvent>b__6_0(System.Action,System.Threading.Tasks.Task)",
                "Yield.Correlation");
            StitchScenario scenario = CreateV1CompletedUnwindYieldBridgeScenario(yield);
            Frame wrapper = Frame.CoreLib("System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder" +
                (genericBuilder ? "`1[System.Int64].SetResult(!0)" : ".SetResult()"), "ValueTaskBuilder.SetResult");
            int body = Array.IndexOf(scenario.Sync, scenario.Async[1]);
            scenario.Sync = scenario.Sync.Take(body).Concat(new[] { wrapper }).Concat(scenario.Sync.Skip(body)).ToArray();
            Frame jit = Frame.Library("clrjit", "JitCompiler.compileMethod", "JIT work");
            scenario.Sync = new[] { jit }.Concat(scenario.Sync).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = new[] { jit }
                .Concat(scenario.ExpectedWithoutInfrastructureCleanup).ToArray();
            scenario.ExpectedStitched = cleanup ? new[] { jit }.Concat(scenario.ExpectedStitched).ToArray()
                : scenario.ExpectedWithoutInfrastructureCleanup;
            scenario.ExpectedWithoutNormalization = scenario.Sync.Where(frame => frame != Frame.V1MoveNextAsDispatcher).ToArray();
            scenario.ExpectedDiagnostics.V1CompletionUnwindFramesCollapsed += 2;
            scenario.EnableInfrastructureCleanup = cleanup;
            if (!cleanup)
            {
                scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
                scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;
            }
            scenario.AssertTransformContext = context =>
            {
                AsyncProfilerStitchedFrame unwind = Assert.Single(context.Frames.Where(
                    frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletionReturn));
                Assert.Equal(3, unwind.SegmentFrameIndex);
                Assert.Equal(7, unwind.Segment.FrameStateAt(3));
                Assert.DoesNotContain(context.Frames, frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody);
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void StateMachineAsync_CompletedDispatcherTail_AttributesRootWithoutOwningExternalCompletion(
            bool preservePrefix, bool cleanup)
        {
            StitchScenario scenario = CreateV1CompletedDispatcherTailScenario(preservePrefix);
            scenario.EnableInfrastructureCleanup = cleanup;
            scenario.AssertTransformContext = context =>
            {
                AsyncProfilerStitchedFrame unwind = Assert.Single(context.Frames.Where(
                    frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletionReturn));
                Assert.True(unwind.IsDispatcherTailUnwind);
                Assert.Equal(3, unwind.SegmentFrameIndex);
                Assert.Equal(7, unwind.Segment.FrameStateAt(3));
                Assert.Equal(MethodIndex.Invalid, unwind.Method);
                Assert.DoesNotContain(context.Frames, frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget);
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("foreign-box")]
        [InlineData("unresolved-box")]
        [InlineData("wrong-box")]
        [InlineData("callback-box")]
        [InlineData("interrupted-boxes")]
        [InlineData("missing-boundary")]
        [InlineData("unresolved-root")]
        public void StateMachineAsync_CompletedDispatcherTail_UnverifiedEvidenceRemainsPhysical(string obstruction)
        {
            StitchScenario scenario = CreateV1CompletedDispatcherTailScenario();
            Frame box = scenario.Sync[3];
            if (obstruction == "foreign-box")
            {
                scenario.Sync[3] = Frame.Library("OtherRuntime", box.SymbolName, "Foreign.Box");
            }
            else if (obstruction == "unresolved-box")
            {
                scenario.OmitSymbolFor = box;
            }
            else if (obstruction == "wrong-box")
            {
                scenario.Sync[3] = Frame.CoreLib(box.SymbolName.Replace("<DeepAsync>d__1", "<RootAsync>d__4"), "Wrong.Box");
            }
            else if (obstruction == "callback-box")
            {
                scenario.Sync[3] = Frame.CoreLib(box.SymbolName.Replace("InstrumentedMoveNext", "ExecutionContextCallback"), "Box.Callback");
            }
            else if (obstruction == "interrupted-boxes")
            {
                scenario.Sync = scenario.Sync.Take(3).Concat(new[] { Frame.App("Observer.Callback") })
                    .Concat(scenario.Sync.Skip(3)).ToArray();
            }
            else if (obstruction == "missing-boundary")
            {
                scenario.Sync = scenario.Sync.Where(frame => frame != Frame.V1MoveNextAsDispatcher).ToArray();
            }
            else
            {
                scenario.OmitSymbolFor = scenario.Async.Last();
            }
            scenario.ExpectedStitched = scenario.Sync.Where(frame => frame != Frame.V1MoveNextAsDispatcher).ToArray();
            scenario.ExpectedWithoutNormalization = scenario.ExpectedStitched;
            scenario.ExpectedDiagnostics = new CallStackDiagnosticsExpectation
            {
                SegmentsProcessed = 2,
                BoundariesNotFound = obstruction == "missing-boundary" ? 2 : 0,
            };
            scenario.AssertTransformContext = context => Assert.DoesNotContain(context.Frames,
                frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletionReturn);

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void StateMachineAsync_CompletedDispatcherTail_MissingRootAddressRemainsPhysical()
        {
            StitchScenario scenario = CreateV1CompletedDispatcherTailScenario();
            scenario.EnableNormalization = false;
            scenario.ExpectedStitched = scenario.ExpectedWithoutNormalization;
            scenario.ExpectedDiagnostics = new CallStackDiagnosticsExpectation { SegmentsProcessed = 2 };
            scenario.AssertTransformContext = context =>
            {
                AsyncProfilerCallStack callStack = Assert.Single(context.Segments);
                int rootIndex = callStack.Frames.FrameCount - 1;
                CodeAddressIndex rootAddress = callStack.Frames.CodeAddressAt(rootIndex);
                var frames = context.Frames.ToList();
                var diagnostics = new AsyncProfilerCallStackDiagnostics();
                try
                {
                    callStack.Frames.SetCodeAddressAt(rootIndex, CodeAddressIndex.Invalid);
                    new AsyncProfilerV1PresentationTransformPass().Apply(new AsyncProfilerCallStackTransformState(
                        context.TraceLog, context.Segments, context.TimestampQpc, context.ClassifyBoundary,
                        context.StitchingState.Copy(), diagnostics, frames, methodOf: context.GetMethod));

                    Assert.Equal(context.Frames, frames);
                    Assert.Equal(0, diagnostics.V1CompletionUnwindFramesCollapsed);
                }
                finally
                {
                    callStack.Frames.SetCodeAddressAt(rootIndex, rootAddress);
                }
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(5, 0)]
        [InlineData(3, 1)]
        [InlineData(4, 1)]
        public void StateMachineAsync_CompletedDispatcherTail_RequiresExactFullNormalCompletion(int normal, int exceptional)
        {
            StitchScenario scenario = CreateV1CompletedDispatcherTailScenario();
            scenario.MethodCompletions = normal;
            scenario.ExceptionCompletions = exceptional;
            scenario.ExpectedStitched = scenario.ExpectedWithoutNormalization;
            scenario.ExpectedDiagnostics = new CallStackDiagnosticsExpectation { SegmentsProcessed = 2 };
            scenario.AssertTransformContext = context => Assert.DoesNotContain(context.Frames,
                frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletionReturn);

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, false, 2)]
        [InlineData(true, false, 2)]
        [InlineData(false, true, 3)]
        [InlineData(true, true, 3)]
        public void FullyCompletedRecursiveV1Unwind_AttributesTheCohortWithoutInventingPhysicalCopies(
            bool cleanup, bool trailingHelpers, int bodies)
        {
            StitchScenario scenario = CreateFullyCompletedRecursiveV1UnwindScenario(bodies, trailingHelpers);
            scenario.EnableInfrastructureCleanup = cleanup;
            scenario.AssertTransformContext = context =>
            {
                AsyncProfilerStitchedFrame unwind = Assert.Single(context.Frames.Where(frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletionReturn));
                Assert.Equal(unwind.Segment.FrameCount - 1, unwind.SegmentFrameIndex);
                Assert.Equal(7, unwind.Segment.FrameStateAt(unwind.SegmentFrameIndex));
                Assert.Equal(MethodIndex.Invalid, unwind.Method);
                Assert.DoesNotContain(context.Frames, frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCaller);
                Assert.Single(context.Frames.Where(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot));
                Assert.Equal(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames, context.Frames[2].Presentation);
                Assert.Equal(1, context.Diagnostics.BoundariesNotFound);
            };
            scenario.AssertStitchedStackSource = source =>
            {
                var unwinds = new List<AsyncProfilerCallStackSourceFrameInfo>();
                for (int i = 0; i < source.CallFrameIndexLimit; i++)
                {
                    if (source.TryGetAsyncProfilerCallStackFrameInfo((StackSourceFrameIndex)i, out var info))
                    {
                        unwinds.Add(info);
                        Assert.True(info.IsCompletionUnwind);
                        Assert.False(info.IsCurrent);
                        Assert.False(info.IsCompleting);
                        Assert.False(info.IsDispatchTarget);
                        Assert.Equal(7, info.State);
                    }
                }
                Assert.Single(unwinds);
                AssertScenarioBrokenRoots(source, 1);
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("interrupted")]
        [InlineData("missing-builder")]
        [InlineData("missing-pair")]
        [InlineData("missing-entry")]
        [InlineData("foreign-helper")]
        [InlineData("unresolved-helper")]
        [InlineData("wrong-box")]
        [InlineData("wrong-context")]
        [InlineData("profile-veto")]
        [InlineData("future-completions")]
        [InlineData("single-body")]
        public void FullyCompletedRecursiveV1Unwind_UnprovenSpinesStayPhysical(string obstruction)
        {
            StitchScenario scenario = CreateFullyCompletedRecursiveV1UnwindScenario(2, true);
            scenario.EnableNormalization = false;
            scenario.EnableInfrastructureCleanup = false;
            scenario.ExpectedStitched = scenario.Sync;
            scenario.ExpectedWithoutInfrastructureCleanup = null;
            scenario.ExpectedDiagnostics = new CallStackDiagnosticsExpectation
            {
                SegmentsProcessed = 2,
                BoundariesNotFound = 2,
            };
            scenario.AssertTransformContext = context =>
            {
                var frames = context.Frames.ToList();
                int body = frames.FindLastIndex(frame => frame.SyncFrameKind == AsyncProfilerStitchSyncFrameKind.V1StateMachineBody);
                int entry = body - 6;
                if (obstruction == "interrupted")
                {
                    frames.Insert(entry, frames[0]);
                }
                else if (obstruction == "missing-builder")
                {
                    frames.RemoveAt(body - 1);
                }
                else if (obstruction == "missing-pair")
                {
                    frames.RemoveAt(body - 2);
                }
                else if (obstruction == "missing-entry")
                {
                    frames.RemoveAt(entry);
                }
                else if (obstruction == "single-body")
                {
                    frames.RemoveRange(2, 9);
                }
                else if (obstruction == "foreign-helper" || obstruction == "unresolved-helper" ||
                    obstruction == "wrong-box" || obstruction == "wrong-context")
                {
                    AsyncProfilerStitchedFrame original = frames[entry];
                    frames[entry] = AsyncProfilerStitchedFrame.CreateSync(new AsyncProfilerStitchSyncFrame(
                        obstruction == "foreign-helper" ? frames[0].CodeAddress : original.CodeAddress,
                        obstruction == "unresolved-helper" ? MethodIndex.Invalid : original.Method,
                        original.SyncClassification, obstruction == "wrong-box" ? frames[0].Method : original.BoxedStateMachineMethod),
                        obstruction == "wrong-context" ? null : original.Context);
                }
                var veto = new RejectingCompletionChainSupport();
                var diagnostics = new AsyncProfilerCallStackDiagnostics();
                var stitchingState = new AsyncProfilerCallStackStitchingState { ProcessedCallStackCount = 1 };
                AsyncProfilerStitchedFrame[] originalFrames = frames.ToArray();
                new AsyncProfilerV1PresentationTransformPass().Apply(new AsyncProfilerCallStackTransformState(
                    context.TraceLog, context.Segments,
                    context.TimestampQpc - (obstruction == "future-completions" ? 10 : 0),
                    context.ClassifyBoundary, stitchingState, diagnostics, frames, methodOf: context.GetMethod,
                    runtimeSupportResolver: address => obstruction == "profile-veto" ? veto : context.GetRuntimeSupport(address)));
                Assert.Equal(
                    originalFrames.Select(frame => (frame.CodeAddress, frame.Method, frame.Context, frame.SegmentFrameIndex)),
                    frames.Select(frame => (frame.CodeAddress, frame.Method, frame.Context, frame.SegmentFrameIndex)));
                Assert.Equal(0, diagnostics.V1CompletionUnwindFramesCollapsed);
                Assert.DoesNotContain(frames, frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletionReturn ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot);
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, false, 3)]
        [InlineData(true, false, 3)]
        [InlineData(false, true, 5)]
        [InlineData(true, true, 5)]
        public void PartiallyCompletedRecursiveV1Unwind_PreservesPhysicalLeafAndAllPendingAncestry(
            bool cleanup, bool trailingHelpers, int bodies)
        {
            StitchScenario scenario = CreatePartiallyCompletedRecursiveV1UnwindScenario(bodies, trailingHelpers, cleanup);
            scenario.AssertTransformContext = context =>
            {
                AsyncProfilerStitchedFrame cohort = Assert.Single(context.Frames.Where(frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.Synthetic));
                Assert.Equal("[Async completion unwind]", cohort.DisplayName);
                Assert.Same(context.Segments[0], cohort.Context);
                Assert.Null(cohort.Segment);
                Assert.Equal(-1, cohort.SegmentFrameIndex);
                Assert.Equal(MethodIndex.Invalid, cohort.Method);
                Assert.Equal(CodeAddressIndex.Invalid, cohort.CodeAddress);
                AsyncProfilerStitchedFrame physical = Assert.Single(context.Frames.Where(frame =>
                    frame.SyncFrameKind == AsyncProfilerStitchSyncFrameKind.V1StateMachineBody));
                Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, physical.Origin);
                Assert.Equal(context.GetMethod(physical.CodeAddress), physical.Method);
                for (int i = 0; i < 3; i++)
                {
                    Assert.Contains(scenario.Sync[i].SymbolName,
                        context.TraceLog.CodeAddresses[context.Frames[i].CodeAddress].FullMethodName);
                }
                Assert.Equal(Enumerable.Range(scenario.MethodCompletions, scenario.Async.Length - scenario.MethodCompletions),
                    context.Frames.Where(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCaller)
                        .Select(frame => frame.SegmentFrameIndex));
                Assert.DoesNotContain(context.Frames, frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletionReturn ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget);
                Assert.False(context.HasVerifiedV1CompletionAnchor(
                    cohort.Context, context.Frames[3].CodeAddress, allowCompletionUnwind: true));
                Assert.Single(context.Frames.Where(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot));
                Assert.Equal(1, context.Diagnostics.V1CompletionAlignmentAmbiguous);
            };
            scenario.AssertOutputs = (_, stitched) => Assert.Contains("[Async completion unwind]", stitched.GroupingFrames);
            scenario.AssertStitchedStackSource = source =>
            {
                AssertScenarioBrokenRoots(source, 1);
                for (int i = 0; i < source.CallFrameIndexLimit; i++)
                {
                    if (source.TryGetAsyncProfilerCallStackFrameInfo((StackSourceFrameIndex)i, out var info))
                    {
                        Assert.False(info.IsCurrent);
                        Assert.False(info.IsCompleting);
                        Assert.False(info.IsCompletionUnwind);
                        Assert.False(info.IsDispatchTarget);
                        Assert.Contains(info.State, new int?[] { 1, 7 });
                    }
                }
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("interrupted")]
        [InlineData("missing-builder")]
        [InlineData("missing-pair")]
        [InlineData("missing-entry")]
        [InlineData("foreign-helper")]
        [InlineData("unresolved-helper")]
        [InlineData("wrong-box")]
        [InlineData("wrong-context")]
        [InlineData("profile-veto")]
        [InlineData("future-completions")]
        [InlineData("insufficient-bodies")]
        [InlineData("too-many-bodies")]
        [InlineData("exception")]
        [InlineData("mixed-captured-methods")]
        [InlineData("missing-caller")]
        public void PartiallyCompletedRecursiveV1Unwind_UnprovenCohortsStayPhysical(string obstruction)
        {
            StitchScenario scenario = CreatePartiallyCompletedRecursiveV1UnwindScenario(4, true, false);
            if (obstruction == "too-many-bodies")
            {
                scenario.MethodCompletions = 2;
            }
            if (obstruction == "exception")
            {
                scenario.ExceptionCompletions = 1;
            }
            if (obstruction == "mixed-captured-methods")
            {
                scenario.Async[scenario.Async.Length - 1] = Frame.App("Scenario+<OtherAsync>d__21.MoveNext");
            }
            scenario.EnableNormalization = false;
            int next = scenario.MethodCompletions + scenario.ExceptionCompletions + 1;
            scenario.ExpectedStitched = scenario.Sync.Concat(
                scenario.Async.Skip(next).Select((frame, i) => frame.Logical(
                    obstruction == "mixed-captured-methods" ? "Scenario.OtherAsync" : "Scenario.RecursiveReturnAsync",
                    scenario.AsyncStates[next + i]))).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = null;
            scenario.ExpectedWithoutNormalization = null;
            scenario.ExpectedDiagnostics = new CallStackDiagnosticsExpectation
            {
                SegmentsProcessed = 2,
                BoundariesNotFound = 2,
            };
            scenario.AssertTransformContext = context =>
            {
                var frames = context.Frames.ToList();
                int body = frames.FindLastIndex(frame => frame.SyncFrameKind == AsyncProfilerStitchSyncFrameKind.V1StateMachineBody);
                int entry = body - 6;
                if (obstruction == "interrupted")
                {
                    frames.Insert(entry, frames[0]);
                }
                else if (obstruction == "missing-builder")
                {
                    frames.RemoveAt(body - 1);
                }
                else if (obstruction == "missing-pair")
                {
                    frames.RemoveAt(body - 2);
                }
                else if (obstruction == "missing-entry")
                {
                    frames.RemoveAt(entry);
                }
                else if (obstruction == "insufficient-bodies")
                {
                    frames.RemoveRange(5, 18);
                }
                else if (obstruction == "missing-caller")
                {
                    frames.RemoveAt(frames.Count - 1);
                }
                else if (obstruction == "foreign-helper" || obstruction == "unresolved-helper" ||
                    obstruction == "wrong-box" || obstruction == "wrong-context")
                {
                    AsyncProfilerStitchedFrame original = frames[entry];
                    frames[entry] = AsyncProfilerStitchedFrame.CreateSync(new AsyncProfilerStitchSyncFrame(
                        obstruction == "foreign-helper" ? frames[0].CodeAddress : original.CodeAddress,
                        obstruction == "unresolved-helper" ? MethodIndex.Invalid : original.Method,
                        original.SyncClassification, obstruction == "wrong-box" ? frames[0].Method : original.BoxedStateMachineMethod),
                        obstruction == "wrong-context" ? null : original.Context);
                }
                var veto = new RejectingCompletionChainSupport();
                var diagnostics = new AsyncProfilerCallStackDiagnostics();
                var stitchingState = new AsyncProfilerCallStackStitchingState { ProcessedCallStackCount = 1 };
                AsyncProfilerStitchedFrame[] originalFrames = frames.ToArray();
                int[] callers = frames.Where(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCaller)
                    .Select(frame => frame.SegmentFrameIndex).ToArray();
                new AsyncProfilerV1PresentationTransformPass().Apply(new AsyncProfilerCallStackTransformState(
                    context.TraceLog, context.Segments,
                    context.TimestampQpc - (obstruction == "future-completions" ? 10 : 0),
                    context.ClassifyBoundary, stitchingState, diagnostics, frames, methodOf: context.GetMethod,
                    runtimeSupportResolver: address => obstruction == "profile-veto" ? veto : context.GetRuntimeSupport(address)));
                Assert.Equal(
                    originalFrames.Select(frame => (frame.CodeAddress, frame.Method, frame.Context, frame.SegmentFrameIndex)),
                    frames.Select(frame => (frame.CodeAddress, frame.Method, frame.Context, frame.SegmentFrameIndex)));
                Assert.Equal(callers, frames.Where(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCaller)
                    .Select(frame => frame.SegmentFrameIndex).ToArray());
                Assert.Equal(0, diagnostics.V1CompletionUnwindFramesCollapsed);
                Assert.DoesNotContain(frames, frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.Synthetic ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletionReturn ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot);
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(3, 0)]
        [InlineData(5, 0)]
        [InlineData(3, 1)]
        public void FullyCompletedRecursiveV1Unwind_RequiresExactFullNormalCompletion(int normal, int exceptional)
        {
            StitchScenario scenario = CreateFullyCompletedRecursiveV1UnwindScenario(2, false);
            scenario.MethodCompletions = normal;
            scenario.ExceptionCompletions = exceptional;
            scenario.EnableInfrastructureCleanup = false;
            Frame body = scenario.Async[0];
            scenario.ExpectedStitched = scenario.Sync.Select(frame => frame == body
                ? body.Logical("Scenario.RecursiveReturnAsync") : frame).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = null;
            scenario.ExpectedDiagnostics = new CallStackDiagnosticsExpectation
            {
                SegmentsProcessed = 2,
                BoundariesNotFound = 2,
                V1SynchronousMoveNextFramesNormalized = 4,
            };
            scenario.AssertTransformContext = context => Assert.DoesNotContain(context.Frames, frame =>
                frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletionReturn ||
                frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody ||
                frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void NestedCompletedV1Outer_YieldBridgeKeepsTheInnerTargetPending(
            bool enableNormalization, bool enableInfrastructureCleanup)
        {
            StitchScenario scenario = CreateNestedCompletedV1OuterYieldScenario();
            scenario.EnableNormalization = enableNormalization;
            scenario.EnableInfrastructureCleanup = enableInfrastructureCleanup;
            if (!enableInfrastructureCleanup)
            {
                scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup;
                scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
                scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = 0;
                scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;
            }
            scenario.AssertTransformContext = context =>
            {
                AsyncProfilerCallStack outer = context.Segments[1];
                Assert.DoesNotContain(context.Frames, frame => ReferenceEquals(frame.Context, outer));
                AsyncProfilerStitchedFrame target = Assert.Single(context.Frames.Where(frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget));
                Assert.Same(context.Segments[0], target.Context);
                Assert.Equal(0, target.Segment.FrameStateAt(target.SegmentFrameIndex));
                Assert.DoesNotContain(context.Frames, frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletionReturn);
                CodeAddressIndex anchor = context.Frames.Single(frame =>
                    frame.SyncClassification.RuntimeKind == RuntimeFrameKind.SchedulerEntry).CodeAddress;
                Assert.False(context.HasVerifiedDispatchEntry(outer, anchor));
                Assert.False(context.HasVerifiedV1CompletionAnchor(outer, anchor, allowCompletionUnwind: true));
                Assert.Equal(enableNormalization || enableInfrastructureCleanup,
                    context.HasVerifiedCompletedV1OuterDispatch(outer, anchor));
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("foreign-yield")]
        [InlineData("unresolved-yield")]
        [InlineData("interrupted")]
        [InlineData("foreign-anchor")]
        [InlineData("excess-completions")]
        [InlineData("exception-completion")]
        public void NestedCompletedV1Outer_YieldBridgeRejectsUnprovenOwnership(string obstruction)
        {
            StitchScenario scenario = CreateNestedCompletedV1OuterYieldScenario();
            Frame yield = scenario.Sync[6];
            Frame replacement = obstruction == "foreign-yield"
                ? Frame.Library("OtherRuntime", yield.SymbolName, "ForeignYield") : yield;
            if (obstruction == "unresolved-yield")
            {
                scenario.OmitSymbolFor = yield;
            }
            if (obstruction == "foreign-anchor")
            {
                replacement = Frame.Library("OtherRuntime", Frame.ThreadPoolDispatch.SymbolName, "ForeignDispatch");
                scenario.Sync = scenario.Sync.Select(frame => frame == Frame.ThreadPoolDispatch ? replacement : frame).ToArray();
                scenario.ExpectedStitched = scenario.ExpectedStitched.Select(frame => frame == Frame.ThreadPoolDispatch ? replacement : frame).ToArray();
                scenario.ExpectedWithoutInfrastructureCleanup = scenario.ExpectedWithoutInfrastructureCleanup.Select(
                    frame => frame == Frame.ThreadPoolDispatch ? replacement : frame).ToArray();
                replacement = yield;
            }
            if (obstruction == "excess-completions")
            {
                scenario.AsyncSegments[0].MethodCompletions = 3;
            }
            if (obstruction == "exception-completion")
            {
                scenario.AsyncSegments[0].MethodCompletions = 1;
                scenario.AsyncSegments[0].ExceptionCompletions = 1;
            }
            Frame[] helpers = obstruction == "interrupted"
                ? new[] { yield, Frame.App("Observer.Callback"), yield } : new[] { replacement };
            scenario.Sync = scenario.Sync.Take(6).Concat(helpers).Concat(scenario.Sync.Skip(7)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = scenario.ExpectedWithoutInfrastructureCleanup.Take(4)
                .Concat(helpers).Concat(scenario.ExpectedWithoutInfrastructureCleanup.Skip(5)).ToArray();
            scenario.ExpectedStitched = scenario.ExpectedStitched.Take(3).Concat(helpers)
                .Concat(scenario.ExpectedStitched.Skip(3)).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 4;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("missing-placement")]
        [InlineData("nonempty-outer")]
        [InlineData("disconnected-inner")]
        [InlineData("missing-inner-dispatch")]
        [InlineData("missing-boundary")]
        [InlineData("adjacency")]
        [InlineData("ambiguous")]
        [InlineData("future-completion")]
        [InlineData("profile-veto")]
        public void NestedCompletedV1Outer_YieldBridgeRequiresConsumedNestedDispatchEvidence(string obstruction)
        {
            StitchScenario scenario = CreateNestedCompletedV1OuterYieldScenario();
            scenario.EnableInfrastructureCleanup = false;
            scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;
            scenario.AssertTransformContext = context =>
            {
                var frames = context.Frames.ToList();
                int bridgeStart = frames.FindIndex(frame => frame.Context == null);
                CodeAddressIndex innerDispatch = frames.Single(frame =>
                    context.ClassifyBoundary(frame.CodeAddress).Kind ==
                        AsyncProfilerCallStackStitchBoundaryKind.V1Dispatcher).CodeAddress;
                CodeAddressIndex outerDispatch = Enumerable.Range(0, context.TraceLog.CodeAddresses.Count)
                    .Select(i => (CodeAddressIndex)i).Single(ca => ca != innerDispatch &&
                        context.ClassifyBoundary(ca).Kind == AsyncProfilerCallStackStitchBoundaryKind.V1Dispatcher);
                var placements = new[]
                {
                    new AsyncProfilerCallStackPlacement(context.Segments[1], bridgeStart,
                        obstruction == "nonempty-outer" ? bridgeStart + 1 : bridgeStart, dispatch: outerDispatch),
                    new AsyncProfilerCallStackPlacement(context.Segments[0], 0,
                        obstruction == "disconnected-inner" ? bridgeStart - 1 : bridgeStart,
                        dispatch: obstruction == "missing-inner-dispatch" ? CodeAddressIndex.Invalid : innerDispatch),
                };
                var diagnostics = new AsyncProfilerCallStackDiagnostics();
                var stitchingState = new AsyncProfilerCallStackStitchingState
                {
                    ProcessedCallStackCount = 2,
                    HasConsistentPhysicalAlignment = obstruction != "missing-boundary" && obstruction != "adjacency",
                    UnresolvedV1CompletionCount = obstruction == "ambiguous" ? 1 : 0,
                };
                var support = new AsyncProfilerRejectRuntimeBridgeSupport();
                var state = new AsyncProfilerCallStackTransformState(
                    context.TraceLog, context.Segments,
                    obstruction == "future-completion" ? context.Segments[1].StartQpc - 1 : context.TimestampQpc,
                    context.ClassifyBoundary, stitchingState, diagnostics, frames,
                    runtimeSupportResolver: obstruction == "profile-veto"
                        ? (Func<CodeAddressIndex, AsyncProfilerRuntimeSupport>)(ca => support) : null,
                    segmentPlacements: obstruction == "missing-placement" ? null : placements);
                CodeAddressIndex bridge = frames[bridgeStart].CodeAddress;
                Assert.Equal(obstruction == "profile-veto",
                    state.HasVerifiedCompletedV1OuterDispatch(context.Segments[1], bridge));
                new AsyncProfilerInfrastructureCleanupTransformPass().Apply(state);
                Assert.Contains(frames, frame => frame.CodeAddress == bridge &&
                    frame.Presentation == AsyncProfilerStitchedFramePresentation.Native);
                Assert.Equal(0, diagnostics.RuntimeResumptionBridgeFramesCollapsed);
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(true, 1)]
        [InlineData(true, 3)]
        [InlineData(false, 1)]
        [InlineData(false, 3)]
        public void StateMachineAsync_CompletedUnwind_YieldBridgePreservesCompletionRoles(
            bool enableInfrastructureCleanup, int helperCount)
        {
            Frame[] helpers =
            {
                Frame.CoreLib(
                    "System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter+<>c::<OutputCorrelationEtwEvent>b__6_0(System.Action,System.Threading.Tasks.Task)",
                    "Yield.Correlation"),
                Frame.CoreLib("System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter+Helper`1[System.Int64].Invoke()",
                    "Yield.Helper"),
                Frame.CoreLib("System.Runtime.CompilerServices.YieldAwaitable.GetAwaiter()", "Yield.GetAwaiter"),
            };
            StitchScenario scenario = CreateV1CompletedUnwindYieldBridgeScenario(helpers.Take(helperCount).ToArray());
            scenario.EnableInfrastructureCleanup = enableInfrastructureCleanup;
            if (!enableInfrastructureCleanup)
            {
                scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup;
                scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
                scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;
            }
            scenario.AssertTransformContext = context =>
            {
                AsyncProfilerStitchedFrame unwind = Assert.Single(context.Frames.Where(
                    frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletionReturn));
                CodeAddressIndex bridge = context.Frames.Single(frame =>
                    frame.SyncClassification.RuntimeKind == RuntimeFrameKind.SchedulerEntry).CodeAddress;
                Assert.False(context.HasVerifiedDispatchEntry(unwind.Context, bridge));
                Assert.False(context.HasVerifiedV1CompletionAnchor(unwind.Context, bridge));
                Assert.True(context.HasVerifiedV1CompletionAnchor(unwind.Context, bridge, allowCompletionUnwind: true));
                Assert.False(AsyncProfilerCallStackTransformHelpers.HasVerifiedAsyncAnchor(
                    context, context.Frames.IndexOf(unwind), unwind.Context));
                Assert.Equal(MethodIndex.Invalid, unwind.Method);
                Assert.Equal(7, unwind.Segment.FrameStateAt(unwind.SegmentFrameIndex));
                Assert.DoesNotContain(context.Frames, frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget);
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("foreign-yield")]
        [InlineData("unresolved-yield")]
        [InlineData("interrupted")]
        [InlineData("other-runtime-bridge")]
        [InlineData("unresolved-anchor")]
        public void StateMachineAsync_CompletedUnwind_YieldBridgeRejectsUnownedOrInterruptedSpans(string obstruction)
        {
            Frame yield = Frame.CoreLib(
                "System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter+<>c.Callback()", "Yield.Callback");
            Frame[] helpers = obstruction == "interrupted"
                ? new[] { yield, Frame.App("Observer.Callback"), yield }
                : new[] { obstruction == "foreign-yield"
                    ? Frame.Library("OtherRuntime", yield.SymbolName, "ForeignYield") : yield };
            StitchScenario scenario = CreateV1CompletedUnwindYieldBridgeScenario(helpers);
            if (obstruction == "unresolved-yield")
            {
                scenario.OmitSymbolFor = yield;
            }
            else if (obstruction == "unresolved-anchor")
            {
                scenario.OmitSymbolFor = Frame.ThreadPoolDispatch;
            }
            else if (obstruction == "other-runtime-bridge")
            {
                Frame Replace(Frame frame) => frame == yield || frame == Frame.ThreadPoolDispatch
                    ? Frame.Library(@"C:\other\System.Private.CoreLib.dll", frame.SymbolName, "OtherRuntime." + frame.Label)
                    : frame;
                scenario.Sync = scenario.Sync.Select(Replace).ToArray();
                scenario.ExpectedWithoutInfrastructureCleanup = scenario.ExpectedWithoutInfrastructureCleanup.Select(Replace).ToArray();
                scenario.ExpectedWithoutNormalization = scenario.ExpectedWithoutNormalization.Select(Replace).ToArray();
            }
            scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, "missing-placement")]
        [InlineData(false, "missing-boundary")]
        [InlineData(false, "ambiguous")]
        [InlineData(false, "profile-veto")]
        [InlineData(true, "missing-placement")]
        [InlineData(true, "missing-boundary")]
        [InlineData(true, "ambiguous")]
        [InlineData(true, "profile-veto")]
        public void StateMachineAsync_CompletedUnwind_RuntimeBridgeRequiresConsistentStructuralEvidence(
            bool callbackChain, string obstruction)
        {
            Frame[] helpers;
            if (callbackChain)
            {
                StitchScenario callback = CreateCallbackCompletionBridgeScenario(
                    AsyncCallstackKind.StateMachineAsync, includeAwaiterHelpers: true);
                helpers = callback.Sync.Skip(callback.Sync.Length - 9).Take(7).ToArray();
            }
            else
            {
                helpers = new[] { Frame.CoreLib(
                    "System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter+<>c.Callback()", "Yield.Callback") };
            }
            StitchScenario scenario = CreateV1CompletedUnwindYieldBridgeScenario(helpers);
            scenario.EnableInfrastructureCleanup = false;
            scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;
            scenario.AssertTransformContext = context =>
            {
                var frames = context.Frames.ToList();
                AsyncProfilerCallStack callStack = Assert.Single(context.Segments);
                CodeAddressIndex dispatch = Enumerable.Range(0, context.TraceLog.CodeAddresses.Count)
                    .Select(i => (CodeAddressIndex)i).Single(ca =>
                        context.ClassifyBoundary(ca).Kind == AsyncProfilerCallStackStitchBoundaryKind.V1Dispatcher);
                var placements = new[] { new AsyncProfilerCallStackPlacement(callStack, 0, frames.Count, dispatch: dispatch) };
                var diagnostics = new AsyncProfilerCallStackDiagnostics();
                var stitchingState = new AsyncProfilerCallStackStitchingState
                {
                    ProcessedCallStackCount = 1,
                    HasConsistentPhysicalAlignment = obstruction != "missing-boundary",
                    UnresolvedV1CompletionCount = obstruction == "ambiguous" ? 1 : 0,
                };
                var support = new AsyncProfilerRejectRuntimeBridgeSupport();
                var state = new AsyncProfilerCallStackTransformState(
                    context.TraceLog, context.Segments, context.TimestampQpc, context.ClassifyBoundary, stitchingState, diagnostics, frames,
                    runtimeSupportResolver: obstruction == "profile-veto"
                        ? (Func<CodeAddressIndex, AsyncProfilerRuntimeSupport>)(ca => support) : null,
                    segmentPlacements: obstruction == "missing-placement" ? null : placements);
                CodeAddressIndex bridge = frames.First(frame => frame.Context == null).CodeAddress;
                Assert.Equal(obstruction == "profile-veto",
                    state.HasVerifiedV1CompletionAnchor(callStack, bridge, allowCompletionUnwind: true));
                new AsyncProfilerInfrastructureCleanupTransformPass().Apply(state);
                Assert.Equal(context.Frames, frames);
                Assert.Equal(0, diagnostics.RuntimeResumptionBridgeFramesCollapsed);
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("interrupted")]
        [InlineData("unresolved")]
        [InlineData("foreign")]
        [InlineData("recursive")]
        [InlineData("unordered")]
        [InlineData("unresolved-root")]
        [InlineData("no-completion-spine")]
        [InlineData("foreign-callback")]
        [InlineData("single-identity")]
        [InlineData("valuetask-foreign")]
        [InlineData("valuetask-unresolved")]
        [InlineData("valuetask-other-method")]
        public void StateMachineAsync_CompletedUnwind_UnverifiedSpansRemainPhysical(string obstruction)
        {
            StitchScenario scenario = CreateV1CompletedUnwindScenario(false);
            if (obstruction.StartsWith("valuetask-", StringComparison.Ordinal))
            {
                string method = obstruction == "valuetask-other-method" ? "SetException" : "SetResult";
                string symbol = "System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder`1[System.Int64]." + method;
                Frame wrapper = obstruction == "valuetask-foreign" ? Frame.Library("OtherRuntime", symbol, "Foreign.ValueTask")
                    : Frame.CoreLib(symbol, "ValueTaskBuilder." + method);
                int body = Array.IndexOf(scenario.Sync, scenario.Async[1]);
                scenario.Sync = scenario.Sync.Take(body).Concat(new[] { wrapper }).Concat(scenario.Sync.Skip(body)).ToArray();
                if (obstruction == "valuetask-unresolved")
                {
                    scenario.OmitSymbolFor = wrapper;
                }
            }
            else if (obstruction == "interrupted")
            {
                scenario.Sync = scenario.Sync.Take(7).Concat(new[] { Frame.App("Scenario.ArbitraryCallback") })
                    .Concat(scenario.Sync.Skip(7)).ToArray();
            }
            else if (obstruction == "unresolved")
            {
                scenario.OmitSymbolFor = scenario.Sync[7];
            }
            else if (obstruction == "foreign")
            {
                Frame original = scenario.Sync[7];
                scenario.Sync[7] = Frame.Library("OtherRuntime", original.SymbolName, original.Label);
            }
            else if (obstruction == "recursive")
            {
                scenario.Async[3] = scenario.Async[0];
            }
            else if (obstruction == "unordered")
            {
                Frame first = scenario.Async[0];
                scenario.Async[0] = scenario.Async[1];
                scenario.Async[1] = first;
            }
            else if (obstruction == "unresolved-root")
            {
                scenario.OmitSymbolFor = scenario.Async.Last();
            }
            else if (obstruction == "foreign-callback")
            {
                Frame callback = scenario.Sync[6];
                scenario.Sync[6] = Frame.CoreLib(
                    callback.SymbolName.Replace("<ChildAsync>d__2", "<OtherAsync>d__5"), "ForeignCallback");
            }
            else if (obstruction == "single-identity")
            {
                scenario.Sync = scenario.Sync.Skip(4).Take(4)
                    .Concat(new[] { Frame.V1MoveNextAsDispatcher, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart })
                    .ToArray();
            }
            else
            {
                scenario.Sync = scenario.Sync.Where(frame =>
                    !frame.SymbolName.Contains("SetExistingTaskResult")).ToArray();
            }
            scenario.ExpectedStitched = scenario.Sync.Where(frame => frame != Frame.V1MoveNextAsDispatcher)
                .Select(frame => frame.ModuleName == Frame.ApplicationModule && AsyncProfilerMethodNames.TryGetLogicalStateMachineMethodName(
                    frame.SymbolName, out string name) ? frame.Logical(name) : frame).ToArray();
            scenario.ExpectedDiagnostics = new CallStackDiagnosticsExpectation
            {
                SegmentsProcessed = 2,
                V1SynchronousMoveNextFramesNormalized = obstruction == "single-identity" ? 2 : 4,
            };
            scenario.AssertTransformContext = context =>
                Assert.DoesNotContain(context.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletionReturn);

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        [InlineData(5)]
        public void StateMachineAsync_CompletedUnwind_RequiresExactRecordedFullCompletion(int completions)
        {
            StitchScenario scenario = CreateV1CompletedUnwindScenario(false);
            scenario.MethodCompletions = completions;
            scenario.ExpectedDiagnostics = new CallStackDiagnosticsExpectation
            {
                SegmentsProcessed = 2,
                V1InlineFallbackUsed = completions == 0 ? 2 : 0,
                V1SynchronousMoveNextFramesNormalized = completions == 0 ? 0 : 4,
            };
            if (completions == 0)
            {
                scenario.ExpectedStitched = new[]
                {
                    scenario.Async[2].Logical("Scenario.CurrentAsync", 0),
                    scenario.Async[3].Logical("Scenario.RootAsync", 7),
                    Frame.ThreadPoolDispatch, Frame.WorkerThreadStart,
                };
            }
            else
            {
                scenario.ExpectedStitched = scenario.Sync.Where(frame => frame != Frame.V1MoveNextAsDispatcher)
                    .Select(frame => frame.ModuleName == Frame.ApplicationModule && AsyncProfilerMethodNames.TryGetLogicalStateMachineMethodName(
                        frame.SymbolName, out string name) ? frame.Logical(name) : frame).ToArray();
                if (completions == 3)
                {
                    int hostStart = scenario.ExpectedStitched.Length - 2;
                    scenario.ExpectedStitched = scenario.ExpectedStitched.Take(hostStart)
                        .Concat(new[] { Frame.V1MoveNextAsDispatcher, scenario.Async[3].Logical("Scenario.RootAsync", 7) })
                        .Concat(scenario.ExpectedStitched.Skip(hostStart)).ToArray();
                }
            }
            scenario.AssertTransformContext = context =>
                Assert.DoesNotContain(context.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletionReturn);

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, false, true)]
        [InlineData(false, true, true)]
        [InlineData(true, false, true)]
        [InlineData(true, true, false)]
        public void StateMachineAsync_CompletedUnwind_TplCallbacksRetainVerifiedCompletionOwnership(
            bool truncated, bool enableInfrastructureCleanup, bool onlyManagedCodeStacks)
        {
            Frame callback = Frame.CoreLib(
                "System.Runtime.CompilerServices.TaskAwaiter+<>c.<OutputWaitEtwEvents>b__12_0(System.Action,System.Threading.Tasks.Task)",
                "TaskAwaiter.Correlation");
            StitchScenario scenario = CreateV1CompletedUnwindScenario(truncated, callback);
            scenario.OnlyManagedCodeStacks = onlyManagedCodeStacks;
            scenario.EnableInfrastructureCleanup = enableInfrastructureCleanup;
            int unwindLength = scenario.Sync.Length - (truncated ? 0 : 3);
            scenario.ExpectedStitched = new[] { scenario.Async.Last().Logical("Scenario.RootAsync", -2) }
                .Concat(truncated ? Array.Empty<Frame>() : new[] { Frame.ThreadPoolDispatch, Frame.WorkerThreadStart }).ToArray();
            scenario.ExpectedWithoutNormalization = scenario.Sync.Where(frame => frame != Frame.V1MoveNextAsDispatcher).ToArray();
            scenario.ExpectedDiagnostics = new CallStackDiagnosticsExpectation
            {
                SegmentsProcessed = 2,
                BoundariesNotFound = truncated ? 2 : 0,
                IncompleteAsyncRootsNormalized = truncated ? 2 : 0,
                V1CompletionUnwindFramesCollapsed = unwindLength * 2,
            };
            scenario.AssertStitchedStackSource = source => AssertScenarioBrokenRoots(source, truncated ? 1 : 0);
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("application", false)]
        [InlineData("foreign", false)]
        [InlineData("unresolved", true)]
        [InlineData("lookalike", true)]
        public void StateMachineAsync_CompletedUnwind_UnverifiedTplCallbacksRemainPhysical(string obstruction, bool truncated)
        {
            const string name = "System.Runtime.CompilerServices.TaskAwaiter+<>c.<OutputWaitEtwEvents>b__12_0";
            Frame callback = obstruction == "application" ? Frame.App("Observer.Callback") :
                obstruction == "foreign" ? Frame.Library("OtherRuntime", name, "ForeignAwaiter") :
                Frame.CoreLib(obstruction == "lookalike" ? name.Replace("TaskAwaiter+", "TaskAwaiterLookalike+") : name, "Awaiter");
            StitchScenario scenario = CreateV1CompletedUnwindScenario(truncated, callback);
            if (obstruction == "unresolved")
            {
                scenario.OmitSymbolFor = callback;
            }
            scenario.ExpectedStitched = scenario.Sync.Where(frame => frame != Frame.V1MoveNextAsDispatcher)
                .Select(frame => frame.ModuleName == Frame.ApplicationModule &&
                    AsyncProfilerMethodNames.TryGetLogicalStateMachineMethodName(frame.SymbolName, out string logical)
                        ? frame.Logical(logical) : frame).ToArray();
            scenario.ExpectedDiagnostics = new CallStackDiagnosticsExpectation
            {
                SegmentsProcessed = 2,
                BoundariesNotFound = truncated ? 2 : 0,
                V1SynchronousMoveNextFramesNormalized = 4,
            };
            scenario.AssertTransformContext = context =>
                Assert.DoesNotContain(context.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletionReturn);
            scenario.AssertStitchedStackSource = source => AssertScenarioBrokenRoots(source, 1);
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(0, false, true, true, false)]
        [InlineData(1, true, true, true, false)]
        [InlineData(2, false, false, true, false)]
        [InlineData(4, true, false, true, false)]
        [InlineData(7, false, true, false, false)]
        [InlineData(9, true, true, false, false)]
        [InlineData(0, true, true, true, true)]
        public void StateMachineAsync_TruncatedReturn_RetainsFinishingBodyAndCapturedAncestry(
            int rootCut, bool enableInfrastructureCleanup, bool enableFrameAnnotations, bool preservePrefix, bool exceptional)
        {
            StitchScenario scenario = CreateV1TruncatedReturnScenario(rootCut, preservePrefix);
            scenario.EnableInfrastructureCleanup = enableInfrastructureCleanup;
            scenario.EnableFrameAnnotations = enableFrameAnnotations;
            if (exceptional)
            {
                scenario.MethodCompletions--;
                scenario.ExceptionCompletions = 1;
            }
            Frame finishing = scenario.Async[2].Logical("Scenario.CurrentAsync", enableFrameAnnotations ? -2 : (int?)null);
            Frame parent = scenario.Async[3].Logical("Scenario.RootAsync", enableFrameAnnotations ? 7 : (int?)null);
            Frame ancestor = scenario.Async[4].Logical("Scenario.AncestorAsync", enableFrameAnnotations ? 9 : (int?)null);
            int prefixLength = preservePrefix ? 2 : 0;
            scenario.ExpectedStitched = scenario.Sync.Take(prefixLength)
                .Concat(preservePrefix ? new[] { scenario.Sync[prefixLength].DispatcherFrames() } : Array.Empty<Frame>())
                .Concat(new[] { finishing, parent, ancestor }).ToArray();
            if (!enableFrameAnnotations)
            {
                scenario.ExpectedWithoutNormalization =
                    scenario.Sync.Concat(new[] { scenario.Async[4].Logical("Scenario.AncestorAsync") }).ToArray();
            }
            scenario.ExpectedDiagnostics = new CallStackDiagnosticsExpectation
            {
                SegmentsProcessed = 2,
                BoundariesNotFound = 2,
                IncompleteAsyncRootsNormalized = 2,
                V1CompletionUnwindFramesCollapsed = (scenario.Sync.Length - prefixLength) * 2,
            };
            scenario.AssertTransformContext = context =>
            {
                AsyncProfilerStitchedFrame completed = Assert.Single(context.Frames.Where(
                    frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody));
                Assert.Equal(2, completed.SegmentFrameIndex);
                Assert.Equal(0, completed.Segment.FrameStateAt(2));
                Assert.NotEqual(MethodIndex.Invalid, completed.Method);
                Assert.Equal(completed.Method, context.TraceLog.CodeAddresses.MethodIndex(completed.CodeAddress));
                Assert.Contains("Scenario+<CurrentAsync>d__3.MoveNext",
                    context.TraceLog.CodeAddresses[completed.CodeAddress].FullMethodName);
                Assert.Equal(new[] { 3, 4 }, context.Frames.Where(
                    frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCaller)
                    .Select(frame => frame.SegmentFrameIndex).ToArray());
                Assert.DoesNotContain(context.Frames, frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletionReturn);
                Assert.Single(context.Frames.Where(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot));
            };
            scenario.AssertStitchedStackSource = source =>
            {
                AssertScenarioBrokenRoots(source, 1);
                var completing = new List<AsyncProfilerCallStackSourceFrameInfo>();
                for (int i = 0; i < source.CallFrameIndexLimit; i++)
                {
                    if (source.TryGetAsyncProfilerCallStackFrameInfo((StackSourceFrameIndex)i, out AsyncProfilerCallStackSourceFrameInfo info))
                    {
                        Assert.False(info.IsCurrent);
                        Assert.False(info.IsDispatchTarget);
                        Assert.False(info.IsCompletionUnwind);
                        if (info.IsCompleting)
                        {
                            completing.Add(info);
                            Assert.Equal(info.CodeAddress, source.GetFrameCodeAddress((StackSourceFrameIndex)i));
                        }
                    }
                }
                Assert.Equal(0, Assert.Single(completing).State);
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("application")]
        [InlineData("foreign")]
        [InlineData("unresolved")]
        [InlineData("unordered")]
        [InlineData("recursive")]
        [InlineData("single-identity")]
        [InlineData("missing-finishing-body")]
        [InlineData("no-completion-spine")]
        [InlineData("records-ahead")]
        [InlineData("excess-records")]
        public void StateMachineAsync_TruncatedReturn_UnverifiedSpansRemainPhysical(string obstruction)
        {
            StitchScenario scenario = CreateV1TruncatedReturnScenario(0, true);
            int finishing = Array.IndexOf(scenario.Sync, scenario.Async[2]);
            int helper = finishing + 2;
            if (obstruction == "application")
            {
                scenario.Sync = scenario.Sync.Take(helper).Concat(new[] { Frame.App("Observer.InterveningCallback") })
                    .Concat(scenario.Sync.Skip(helper)).ToArray();
            }
            else if (obstruction == "foreign")
            {
                Frame original = scenario.Sync[helper];
                scenario.Sync[helper] = Frame.Library("OtherRuntime", original.SymbolName, original.Label);
            }
            else if (obstruction == "unresolved")
            {
                scenario.OmitSymbolFor = scenario.Sync[helper];
            }
            else if (obstruction == "unordered")
            {
                Frame original = scenario.Async[0];
                scenario.Async[0] = scenario.Async[1];
                scenario.Async[1] = original;
            }
            else if (obstruction == "recursive")
            {
                scenario.Async[4] = scenario.Async[0];
            }
            else if (obstruction == "single-identity")
            {
                scenario.Sync = scenario.Sync.Take(finishing + 2).ToArray();
            }
            else if (obstruction == "missing-finishing-body")
            {
                scenario.Sync = scenario.Sync.Where(frame => frame != scenario.Async[2]).ToArray();
            }
            else if (obstruction == "no-completion-spine")
            {
                scenario.Sync = scenario.Sync.Where(frame => !frame.SymbolName.Contains("SetExistingTaskResult")).ToArray();
            }
            else
            {
                scenario.MethodCompletions = obstruction == "records-ahead" ? 4 : 6;
            }
            Frame[] callers = scenario.MethodCompletions == 3
                ? new[] { scenario.Async[4].Logical(obstruction == "recursive" ? "Scenario.DeepAsync" : "Scenario.AncestorAsync", 9) }
                : Array.Empty<Frame>();
            scenario.ExpectedStitched = scenario.Sync.Select(frame =>
                frame.ModuleName == Frame.ApplicationModule &&
                AsyncProfilerMethodNames.TryGetLogicalStateMachineMethodName(frame.SymbolName, out string logical)
                    ? frame.Logical(logical) : frame).Concat(callers).ToArray();
            scenario.ExpectedDiagnostics = new CallStackDiagnosticsExpectation
            {
                SegmentsProcessed = 2,
                BoundariesNotFound = 2,
                V1SynchronousMoveNextFramesNormalized = scenario.Sync.Count(frame => scenario.Async.Contains(frame)) * 2,
            };
            scenario.AssertTransformContext = context =>
                Assert.DoesNotContain(context.Frames, frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletionReturn ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot);
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        [InlineData(9)]
        public void StateMachineAsync_TruncatedReturn_WithoutCompletionRecordsStillRecoversPhysicalProgress(int rootCut)
        {
            StitchScenario scenario = CreateV1TruncatedReturnScenario(rootCut, true);
            scenario.MethodCompletions = 0;
            scenario.ExpectedStitched = scenario.Sync.TakeWhile(frame => frame != scenario.Async[2]).Concat(new[]
            {
                scenario.Async[2].Logical("Scenario.CurrentAsync", -1),
                scenario.Async[3].Logical("Scenario.RootAsync", 7),
                scenario.Async[4].Logical("Scenario.AncestorAsync", 9),
            }).ToArray();
            scenario.ExpectedDiagnostics = new CallStackDiagnosticsExpectation
            {
                SegmentsProcessed = 2,
                BoundariesNotFound = 2,
                IncompleteAsyncRootsNormalized = 2,
                V1InlineFallbackUsed = 2,
            };
            scenario.AssertTransformContext = context =>
            {
                Assert.Equal(1, context.Diagnostics.V1TruncatedSuffixFallbackUsed);
                AsyncProfilerStitchedFrame current = Assert.Single(context.Frames.Where(
                    frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent));
                Assert.Equal(2, current.SegmentFrameIndex);
                Assert.Equal(new[] { 3, 4 }, context.Frames.Where(
                    frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCaller)
                    .Select(frame => frame.SegmentFrameIndex).ToArray());
                Assert.DoesNotContain(context.Frames, frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletionReturn);
            };
            scenario.AssertStitchedStackSource = source => AssertScenarioBrokenRoots(source, 1);
            scenario.AssertProductionStitch();
        }

        [Fact]
        public void StateMachineAsync_TruncatedReturn_FutureCompletionsDoNotMarkBodiesCompleted()
        {
            StitchScenario scenario = CreateV1TruncatedReturnScenario(0, true);
            scenario.MethodCompletionQpc = 100_030;
            scenario.ExpectedStitched = scenario.Sync.Select(frame =>
                frame.ModuleName == Frame.ApplicationModule &&
                AsyncProfilerMethodNames.TryGetLogicalStateMachineMethodName(frame.SymbolName, out string logical)
                    ? frame.Logical(logical, frame == scenario.Async[0] ? -1 : (int?)null) : frame)
                .Concat(new[]
                {
                    scenario.Async[1].Logical("Scenario.ChildAsync", 0),
                    scenario.Async[2].Logical("Scenario.CurrentAsync", 0),
                    scenario.Async[3].Logical("Scenario.RootAsync", 7),
                    scenario.Async[4].Logical("Scenario.AncestorAsync", 9),
                }).ToArray();
            scenario.ExpectedDiagnostics = new CallStackDiagnosticsExpectation
            {
                SegmentsProcessed = 2,
                BoundariesNotFound = 2,
                V1SynchronousMoveNextFramesNormalized = 4,
            };
            scenario.AssertTransformContext = context =>
                Assert.DoesNotContain(context.Frames, frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletionReturn);
            scenario.AssertProductionStitch();
        }

        [Fact]
        public void StateMachineAsync_CompletedUnwind_FutureCompletionsDoNotChangeEarlierSamples()
        {
            StitchScenario scenario = CreateV1CompletedUnwindScenario(false);
            scenario.MethodCompletionQpc = 100_030;
            scenario.ExpectedStitched = scenario.Sync.TakeWhile(frame => frame != scenario.Async[0])
                .Select(frame => frame == scenario.Async[1] ? frame.Logical("Scenario.ChildAsync") : frame)
                .Concat(new[]
                {
                    scenario.Async[0].Logical("Scenario.DeepAsync", -1),
                    scenario.Async[1].Logical("Scenario.ChildAsync", 0),
                    scenario.Async[2].Logical("Scenario.CurrentAsync", 0),
                    scenario.Async[3].Logical("Scenario.RootAsync", 7),
                    Frame.ThreadPoolDispatch, Frame.WorkerThreadStart,
                }).ToArray();
            scenario.ExpectedDiagnostics = new CallStackDiagnosticsExpectation
            {
                SegmentsProcessed = 2,
                V1SynchronousMoveNextFramesNormalized = 2,
            };
            scenario.AssertTransformContext = context =>
                Assert.DoesNotContain(context.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletionReturn);

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void StateMachineAsync_CompletedUnwind_PreservesEnclosingLiveContextAndBridge()
        {
            StitchScenario scenario = CreateV1CompletedUnwindScenario(false);
            Frame outer = Frame.App("Scenario.OuterCurrent");
            Frame parent = Frame.App("Scenario.OuterParent");
            Frame bridge = Frame.App("Scenario.SynchronousBridge");
            scenario.AsyncSegments = new[]
            {
                AsyncSegment.Runtime(outer, parent),
                AsyncSegment.StateMachine(scenario.Async),
            };
            scenario.Sync = scenario.Sync.Take(scenario.Sync.Length - 2)
                .Concat(new[]
                {
                    bridge, outer, Frame.V2Wrapper(0), Frame.V2InstrumentedDispatch, Frame.V2Dispatch,
                    Frame.ThreadPoolDispatch, Frame.WorkerThreadStart,
                }).ToArray();
            scenario.ExpectedStitched = new[]
            {
                scenario.Async.Last().Logical("Scenario.RootAsync", -2),
                bridge, outer, parent, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart,
            };
            scenario.ExpectedDiagnostics = new CallStackDiagnosticsExpectation
            {
                SegmentsProcessed = 4,
                V2InfrastructureFramesCollapsed = 4,
                V1CompletionUnwindFramesCollapsed = (scenario.Sync.Length - 8) * 2,
            };
            scenario.AssertTransformContext = context =>
            {
                AsyncProfilerStitchedFrame unwind = Assert.Single(context.Frames.Where(
                    frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletionReturn));
                AsyncProfilerStitchedFrame current = Assert.Single(context.Frames.Where(
                    frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent));
                Assert.Equal(unwind.Context.Depth - 1, current.Context.Depth);
                Assert.DoesNotContain(context.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot);
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(0, 2, true, false)]
        [InlineData(0, 1, true, false)]
        [InlineData(2, 0, true, false)]
        [InlineData(1, 1, true, false)]
        [InlineData(0, 2, false, false)]
        [InlineData(0, 1, false, false)]
        [InlineData(2, 0, false, false)]
        [InlineData(0, 2, true, true)]
        [InlineData(2, 0, true, true)]
        public void StateMachineAsync_CompletionTail_PresentsFinishingStateInBothHosts(
            int normal, int exceptional, bool enableFrameAnnotations, bool enableInfrastructureCleanup)
        {
            Frame child = Frame.App("Scenario+<ChildAsync>d__12.MoveNext");
            Frame finishing = Frame.App("Scenario+<FinishingAsync>d__11.MoveNext");
            Frame parent = Frame.App("Scenario+<ParentAsync>d__10.MoveNext");
            Frame ancestor = Frame.App("Scenario+<AncestorAsync>d__9.MoveNext");
            bool completed = normal + exceptional == 2;
            Frame logicalFinishing = finishing.Logical("Scenario.FinishingAsync", enableFrameAnnotations ? (completed ? -2 : -1) : (int?)null);
            Frame logicalParent = parent.Logical("Scenario.ParentAsync", enableFrameAnnotations ? 0 : (int?)null);
            Frame logicalAncestor = ancestor.Logical("Scenario.AncestorAsync", enableFrameAnnotations ? 2 : (int?)null);
            Frame observer = Frame.App("CompletionObserver.Record");
            string completionMember = exceptional > 0 ? "SetException" : "SetExistingTaskResult";
            Frame completion = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64]." + completionMember + "(class System.Object)",
                "CompletionHelper");
            Frame bridge = Frame.App("EarlierCompletionBridge");
            var scenario = new StitchScenario
            {
                Kind = AsyncCallstackKind.StateMachineAsync,
                Sync = new[]
                {
                    observer, completion, finishing, bridge, child,
                    Frame.V1MoveNextAsDispatcher, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart,
                },
                Async = new[]
                {
                    child.Logical("Scenario.ChildAsync"),
                    finishing.Logical("Scenario.FinishingAsync", enableFrameAnnotations ? 1 : (int?)null),
                    logicalParent, logicalAncestor,
                },
                AsyncStates = new[] { 1, 1, 0, 2 },
                MethodCompletions = normal,
                ExceptionCompletions = exceptional,
                EnableFrameAnnotations = enableFrameAnnotations,
                EnableInfrastructureCleanup = enableInfrastructureCleanup,
                SkipNormalStackAssertion = true,
                VerifyPublicBuilder = true,
                VerifyThreadTimeComputer = true,
                IncludeEventSourceEvents = false,
                AllowSupplementalSamples = true,
                VerifySampleWeights = true,
                ExpectedStitched = (enableInfrastructureCleanup
                    ? new[] { observer, logicalFinishing, logicalParent, logicalAncestor, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart }
                    : new[] { observer, completion, logicalFinishing, logicalParent, logicalAncestor, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart }),
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V1InlineFallbackUsed = normal > 0 ? 0 : 2,
                    V1CompletionTailUsed = completed ? 2 : 0,
                    AsyncInfrastructureFramesCollapsed = enableInfrastructureCleanup ? 2 : 0,
                },
                AssertTransformContext = context =>
                {
                    AsyncProfilerStitchedFrame frame = Assert.Single(context.Frames.Where(candidate =>
                        candidate.Origin == (completed ? AsyncProfilerStitchedFrameOrigin.V1CompletedBody : AsyncProfilerStitchedFrameOrigin.AsyncCurrent)));
                    Assert.Equal(1, frame.SegmentFrameIndex);
                    Assert.Equal(1, frame.Segment.FrameStateAt(frame.SegmentFrameIndex));
                    Assert.False(frame.IsStateMachineBoxCarrier);
                    Assert.DoesNotContain(context.Frames, candidate => candidate.Origin ==
                        (completed ? AsyncProfilerStitchedFrameOrigin.AsyncCurrent : AsyncProfilerStitchedFrameOrigin.V1CompletedBody));
                },
                AssertStitchedStackSource = stackSource =>
                {
                    var frames = new List<(StackSourceFrameIndex Index, string Name, AsyncProfilerCallStackSourceFrameInfo Info)>();
                    for (int i = 0; i < stackSource.CallFrameIndexLimit; i++)
                    {
                        var index = (StackSourceFrameIndex)i;
                        if (stackSource.TryGetAsyncProfilerCallStackFrameInfo(index, out AsyncProfilerCallStackSourceFrameInfo info))
                        {
                            frames.Add((index, stackSource.GetFrameName(index, false), info));
                        }
                    }
                    var completing = Assert.Single(frames.Where(frame => frame.Info.IsCompleting || frame.Info.IsCurrent));
                    Assert.Equal(completed, completing.Info.IsCompleting);
                    Assert.Equal(!completed, completing.Info.IsCurrent);
                    Assert.False(completing.Info.IsDispatchTarget);
                    Assert.Equal(1, completing.Info.State);
                    Assert.Equal(completing.Info.CodeAddress, stackSource.GetFrameCodeAddress(completing.Index));
                    Assert.Contains(finishing.SymbolName,
                        stackSource.TraceLog.CodeAddresses[completing.Info.CodeAddress].FullMethodName);
                    var target = Assert.Single(frames.Where(frame => frame.Name.Contains("Scenario.ParentAsync")));
                    Assert.Equal(completed, target.Info.IsDispatchTarget);
                    Assert.False(target.Info.IsCompleting);
                    Assert.False(target.Info.IsCurrent);
                    Assert.Equal(0, target.Info.State);
                    if (enableFrameAnnotations)
                    {
                        Assert.StartsWith("[Async V1] ", completing.Name);
                        Assert.EndsWith(completed ? "[ordinal 11, state -2]" : "[ordinal 11, state -1]", completing.Name);
                        Assert.EndsWith("[ordinal 10, state 0]", target.Name);
                    }
                    else
                    {
                        Assert.DoesNotContain("[Async V1]", completing.Name);
                        Assert.DoesNotContain("state ", completing.Name);
                    }
                },
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void StateMachineAsync_CompletionTail_AnnotationsDoNotLeakAcrossIdenticalRawIdentities(bool enableFrameAnnotations)
        {
            Frame method = Frame.App("Scenario+<IdentityAsync>d__1.MoveNext");
            Frame parent = Frame.App("Scenario+<ParentAsync>d__2.MoveNext");
            Frame completing = method.Logical("Scenario.IdentityAsync", enableFrameAnnotations ? -2 : (int?)null);
            Frame running = method.Logical("Scenario.IdentityAsync", enableFrameAnnotations ? -1 : (int?)null);
            Frame captured = method.Logical("Scenario.IdentityAsync", enableFrameAnnotations ? 7 : (int?)null);
            Frame target = parent.Logical("Scenario.ParentAsync", enableFrameAnnotations ? 0 : (int?)null);
            var scenario = new StitchScenario
            {
                Kind = AsyncCallstackKind.StateMachineAsync,
                Sync = new[] { method, Frame.V1MoveNextAsDispatcher, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart },
                Async = new[] { captured, target },
                AsyncStates = new[] { 7, 0 },
                MethodCompletions = 1,
                EnableFrameAnnotations = enableFrameAnnotations,
                SkipNormalStackAssertion = true,
                ExpectedStitched = new[] { completing, running, captured, target, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation { SegmentsProcessed = 2, V1CompletionTailUsed = 2 },
                AssertTransformContext = context =>
                {
                    // Exercise emitter cache separation with consumer-added roles sharing the same raw identity.
                    AsyncProfilerStitchedFrame frame = context.Frames[0];
                    Assert.Equal(AsyncProfilerStitchedFrameOrigin.V1CompletedBody, frame.Origin);
                    context.Frames.Insert(1, AsyncProfilerStitchedFrame.CreateAsyncCurrent(
                        frame.CodeAddress, frame.Method, frame.Context, frame.SegmentFrameIndex));
                    context.Frames.Insert(2, AsyncProfilerStitchedFrame.CreateAsync(frame.Context, frame.SegmentFrameIndex));
                },
                AssertStitchedStackSource = stackSource =>
                {
                    var frames = new List<(StackSourceFrameIndex Index, string Name, AsyncProfilerCallStackSourceFrameInfo Info)>();
                    for (int i = 0; i < stackSource.CallFrameIndexLimit; i++)
                    {
                        var index = (StackSourceFrameIndex)i;
                        if (stackSource.TryGetAsyncProfilerCallStackFrameInfo(index, out AsyncProfilerCallStackSourceFrameInfo info) && info.State == 7)
                        {
                            frames.Add((index, stackSource.GetFrameName(index, false), info));
                        }
                    }
                    Assert.Equal(3, frames.Count);
                    Assert.Equal(3, frames.Select(frame => frame.Index).Distinct().Count());
                    Assert.Single(frames.Select(frame => frame.Info.CodeAddress).Distinct());
                    Assert.Single(frames.Select(frame => frame.Info.MethodId).Distinct());
                    Assert.Single(frames.Where(frame => frame.Info.IsCompleting && !frame.Info.IsCurrent));
                    Assert.Single(frames.Where(frame => frame.Info.IsCurrent && !frame.Info.IsCompleting));
                    Assert.Single(frames.Where(frame => !frame.Info.IsCurrent && !frame.Info.IsCompleting));
                    Assert.Equal(enableFrameAnnotations ? 3 : 1, frames.Select(frame => frame.Name).Distinct().Count());
                    if (enableFrameAnnotations)
                    {
                        Assert.EndsWith("state -2]", frames.Single(frame => frame.Info.IsCompleting).Name);
                        Assert.EndsWith("state -1]", frames.Single(frame => frame.Info.IsCurrent).Name);
                        Assert.EndsWith("state 7]", frames.Single(frame => !frame.Info.IsCompleting && !frame.Info.IsCurrent).Name);
                    }
                },
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, 1)]
        [InlineData(true, 1)]
        [InlineData(false, 3)]
        [InlineData(true, 3)]
        public void RecursiveV1Completion_OwnedEntryIdentifiesOnlyTheFinishingChild(bool cleanup, int boxCount)
        {
            StitchScenario scenario = CreateRecursiveV1CompletionScenario(boxCount);
            scenario.EnableInfrastructureCleanup = cleanup;
            scenario.ExpectedWithoutNormalization = null;
            if (!cleanup)
            {
                scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup;
                scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
                scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = 0;
                scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;
            }
            scenario.AssertTransformContext = context =>
            {
                Assert.Equal(0, context.Diagnostics.V1CompletionAlignmentAmbiguous);
                AsyncProfilerStitchedFrame completing = Assert.Single(context.Frames.Where(frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody));
                Assert.Equal(0, completing.SegmentFrameIndex);
                Assert.Equal(0, completing.Segment.FrameStateAt(0));
                Assert.Equal(context.GetMethod(completing.Segment.CodeAddressAt(0)), completing.Method);
                AsyncProfilerStitchedFrame target = Assert.Single(context.Frames.Where(frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget));
                Assert.Equal(1, target.SegmentFrameIndex);
                Assert.False(target.IsStateMachineBoxCarrier);
                Assert.DoesNotContain(context.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
                Assert.False(AsyncProfilerCallStackTransformHelpers.HasVerifiedAsyncAnchor(
                    context, context.Frames.IndexOf(completing), completing.Context));
                Assert.Equal(2, context.Frames.Count(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCaller));
            };
            scenario.AssertStitchedStackSource = source =>
            {
                var completing = new List<AsyncProfilerCallStackSourceFrameInfo>();
                for (int i = 0; i < source.CallFrameIndexLimit; i++)
                {
                    if (source.TryGetAsyncProfilerCallStackFrameInfo((StackSourceFrameIndex)i, out var info) && info.IsCompleting)
                    {
                        completing.Add(info);
                        Assert.False(info.IsCurrent);
                        Assert.False(info.IsDispatchTarget);
                        Assert.Equal(0, info.State);
                        Assert.Equal(info.CodeAddress, source.GetFrameCodeAddress((StackSourceFrameIndex)i));
                        Assert.EndsWith("state -2]", source.GetFrameName((StackSourceFrameIndex)i, false));
                    }
                }
                Assert.Single(completing);
            };
            scenario.AssertProductionStitch();
        }

        [Fact]
        public void RecursiveV1Completion_StandaloneTransformsIgnoreDiagnosticsAndKeepIndependentState()
        {
            StitchScenario scenario = CreateRecursiveV1CompletionScenario(3);
            scenario.EnableNormalization = false;
            scenario.EnableInfrastructureCleanup = false;
            scenario.ExpectedStitched = scenario.ExpectedWithoutNormalization;
            scenario.ExpectedWithoutNormalization = null;
            scenario.ExpectedWithoutInfrastructureCleanup = null;
            scenario.ExpectedDiagnostics = new CallStackDiagnosticsExpectation
            {
                SegmentsProcessed = 2,
                V1InfrastructureFramesCollapsed = 6,
            };
            scenario.AssertTransformContext = context =>
            {
                Assert.Equal(1, context.StitchingState.UnresolvedV1CompletionCount);
                var diagnostics = new AsyncProfilerCallStackDiagnostics();
                var structural = new AsyncProfilerStitchResult(
                    context.Frames.ToArray(), diagnostics, context.StitchingState.Copy());
                var pipeline = new AsyncProfilerCallStackTransformPipeline();
                AsyncProfilerStitchedFrame[] expected = null;

                for (int diagnosticMode = 0; diagnosticMode < 3; diagnosticMode++)
                {
                    if (diagnosticMode == 0)
                    {
                        diagnostics.SegmentsProcessed = 1;
                        diagnostics.V1CompletionAlignmentAmbiguous = 1;
                    }
                    else
                    {
                        diagnostics.Clear();
                        if (diagnosticMode == 2)
                        {
                            diagnostics.SegmentsProcessed = 50;
                            diagnostics.BoundariesNotFound = 50;
                            diagnostics.AdjacencyMismatches = 50;
                            diagnostics.V1CompletionAlignmentAmbiguous = 50;
                            diagnostics.V2SyncLayoutUsed = 50;
                        }
                    }

                    AsyncProfilerStitchResult result = pipeline.Transform(structural,
                        context.Segments, context.TimestampQpc, context.ClassifyBoundary, context.TraceLog);

                    Assert.Single(result.Frames.Where(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody));
                    Assert.Equal(0, result.StitchingState.UnresolvedV1CompletionCount);
                    Assert.Equal(0, result.Diagnostics.V1CompletionAlignmentAmbiguous);
                    Assert.NotSame(structural.StitchingState, result.StitchingState);
                    Assert.Equal(1, structural.StitchingState.UnresolvedV1CompletionCount);
                    Assert.Equal(context.Frames, structural.Frames);
                    if (expected == null)
                    {
                        expected = result.Frames.ToArray();
                        Assert.True(expected.Length < structural.Frames.Count);
                    }
                    else
                    {
                        Assert.Equal(expected, result.Frames);
                    }
                }
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("missing-builder")]
        [InlineData("missing-pair")]
        [InlineData("missing-entry")]
        [InlineData("missing-return-box")]
        [InlineData("interrupted-entry")]
        [InlineData("interrupted-return")]
        [InlineData("second-body")]
        [InlineData("foreign-builder")]
        [InlineData("unresolved-box")]
        [InlineData("wrong-box")]
        [InlineData("wrong-context")]
        [InlineData("cross-profile")]
        [InlineData("profile-veto")]
        [InlineData("missing-boundary")]
        [InlineData("adjacency")]
        public void RecursiveV1Completion_UnprovenLayoutsKeepTheOriginalAmbiguity(string obstruction)
        {
            StitchScenario scenario = CreateRecursiveV1CompletionScenario(3);
            scenario.EnableNormalization = false;
            scenario.EnableInfrastructureCleanup = false;
            scenario.ExpectedStitched = scenario.ExpectedWithoutNormalization;
            scenario.ExpectedWithoutNormalization = null;
            scenario.ExpectedWithoutInfrastructureCleanup = null;
            scenario.ExpectedDiagnostics = new CallStackDiagnosticsExpectation
            {
                SegmentsProcessed = 2,
                V1InfrastructureFramesCollapsed = 6,
            };
            scenario.AssertTransformContext = context =>
            {
                Assert.Equal(1, context.Diagnostics.V1CompletionAlignmentAmbiguous);
                var frames = context.Frames.ToList();
                int body = frames.FindIndex(frame => frame.SyncFrameKind == AsyncProfilerStitchSyncFrameKind.V1StateMachineBody);
                int builder = body - 1;
                int callback = frames.FindIndex(frame =>
                    frame.SyncFrameKind == AsyncProfilerStitchSyncFrameKind.AwaiterHelper);
                int target = frames.FindIndex(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget);
                AsyncProfilerStitchedFrame originalBody = frames[body];
                if (obstruction == "missing-builder")
                {
                    frames.RemoveAt(builder);
                }
                else if (obstruction == "missing-pair")
                {
                    frames.RemoveAt(builder - 1);
                }
                else if (obstruction == "missing-entry")
                {
                    frames.RemoveRange(2, callback - 2);
                }
                else if (obstruction == "missing-return-box")
                {
                    frames.RemoveRange(body + 1, target - body - 2);
                }
                else if (obstruction == "interrupted-entry")
                {
                    frames.Insert(callback, frames[0]);
                }
                else if (obstruction == "interrupted-return")
                {
                    frames.Insert(body + 1, frames[0]);
                }
                else if (obstruction == "second-body")
                {
                    frames.Insert(2, originalBody);
                }
                else if (obstruction == "foreign-builder")
                {
                    frames[builder] = AsyncProfilerStitchedFrame.CreateSync(new AsyncProfilerStitchSyncFrame(
                        frames[0].CodeAddress, frames[0].Method, frames[builder].SyncClassification), originalBody.Context);
                }
                else if (obstruction == "unresolved-box" || obstruction == "wrong-box" || obstruction == "wrong-context")
                {
                    AsyncProfilerStitchedFrame box = frames[callback - 1];
                    frames[callback - 1] = AsyncProfilerStitchedFrame.CreateSync(new AsyncProfilerStitchSyncFrame(
                        box.CodeAddress, obstruction == "unresolved-box" ? MethodIndex.Invalid : box.Method,
                        box.SyncClassification, obstruction == "wrong-box" ? frames[0].Method : box.BoxedStateMachineMethod),
                        obstruction == "wrong-context" ? null : box.Context);
                }
                else if (obstruction == "missing-boundary")
                {
                    frames.RemoveAt(target - 1);
                }
                var diagnostics = new AsyncProfilerCallStackDiagnostics
                {
                    V1CompletionAlignmentAmbiguous = 1,
                };
                var stitchingState = new AsyncProfilerCallStackStitchingState
                {
                    ProcessedCallStackCount = 1,
                    UnresolvedV1CompletionCount = 1,
                    HasConsistentPhysicalAlignment = obstruction != "adjacency",
                };
                var veto = new RejectingCompletionChainSupport();
                AsyncProfilerStitchedFrame[] originalFrames = frames.ToArray();
                new AsyncProfilerV1PresentationTransformPass().Apply(new AsyncProfilerCallStackTransformState(
                    context.TraceLog, context.Segments, context.TimestampQpc, context.ClassifyBoundary,
                    stitchingState, diagnostics, frames, methodOf: context.GetMethod,
                    runtimeSupportResolver: address =>
                        obstruction == "profile-veto" ? veto :
                        obstruction == "cross-profile" && address == context.Frames[builder].CodeAddress
                            ? AsyncProfilerRuntimeSupport.Common : context.GetRuntimeSupport(address)));
                Assert.Equal(1, diagnostics.V1CompletionAlignmentAmbiguous);
                Assert.Equal(
                    originalFrames.Select(frame => (frame.CodeAddress, frame.Method, frame.Context, frame.SegmentFrameIndex)),
                    frames.Select(frame => (frame.CodeAddress, frame.Method, frame.Context, frame.SegmentFrameIndex)));
                Assert.Contains(frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
                    frame.CodeAddress == originalBody.CodeAddress);
                Assert.DoesNotContain(frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody);
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(2, 0)]
        [InlineData(1, 1)]
        public void RecursiveV1Completion_AdditionalProgressCannotIdentifyTheSurvivingCopy(int normal, int exceptional)
        {
            StitchScenario scenario = CreateRecursiveV1CompletionScenario(3);
            scenario.MethodCompletions = normal;
            scenario.ExceptionCompletions = exceptional;
            scenario.EnableInfrastructureCleanup = false;
            Frame body = scenario.Sync.Single(frame => frame.ModuleName == Frame.ApplicationModule);
            scenario.ExpectedStitched = scenario.Sync.Take(15)
                .Select(frame => frame == body ? body.Logical("Scenario.RecursiveAsync") : frame)
                .Concat(scenario.Async.Skip(2).Select(frame => frame.Logical("Scenario.RecursiveAsync", 1)))
                .Concat(scenario.Sync.Skip(18)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = null;
            scenario.ExpectedWithoutNormalization = null;
            scenario.ExpectedDiagnostics = new CallStackDiagnosticsExpectation
            {
                SegmentsProcessed = 2,
                V1InfrastructureFramesCollapsed = 6,
                V1SynchronousMoveNextFramesNormalized = 2,
            };
            scenario.AssertTransformContext = context =>
            {
                Assert.Equal(1, context.Diagnostics.V1CompletionAlignmentAmbiguous);
                Assert.DoesNotContain(context.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody);
            };
            scenario.AssertProductionStitch();
        }

        #region private

        private static StitchScenario CreateFullyCompletedRecursiveV1UnwindScenario(int bodies, bool trailingHelpers)
        {
            Frame method = Frame.App("Scenario+<RecursiveReturnAsync>d__20.MoveNext");
            const string box = "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncStateMachineBox`1[System.Int64,Scenario+<RecursiveReturnAsync>d__20]";
            Frame[] unit =
            {
                Frame.CoreLib(box + ".MoveNext(class System.Threading.Thread,value class Flags)", "Recursive entry"),
                Frame.CoreLib("System.Runtime.CompilerServices.TaskAwaiter+<>c.<OutputWaitEtwEvents>b__12_0(System.Action,System.Threading.Tasks.Task)",
                    "Awaiter correlation"),
                Frame.CoreLib("System.Threading.Tasks.AwaitTaskContinuation.RunOrScheduleAction", "Continuation bridge"),
                Frame.CoreLib("System.Threading.Tasks.Task.RunContinuations", "Run continuations"),
                Frame.CoreLib("System.Threading.Tasks.Task`1[System.Int64].TrySetResult", "Set task result"),
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].SetExistingTaskResult", "Complete builder"),
                method,
                Frame.CoreLib(box + ".ExecutionContextCallback(class System.Object)", "Recursive callback"),
                Frame.CoreLib("System.Threading.ExecutionContext.RunInternal", "Execution-context bridge"),
            };
            Frame[] spine = Enumerable.Range(0, bodies).SelectMany(_ => unit).ToArray();
            if (!trailingHelpers)
            {
                spine = spine.Take(spine.Length - 2).ToArray();
            }
            Frame[] native =
            {
                Frame.Library("coreclr", "Native.ReturnWork", "Runtime return work"),
                Frame.Library("ntdll", "Native.ReturnTransition", "Native return transition"),
            };
            int captured = bodies + 2;
            int[] states = Enumerable.Repeat(1, captured).ToArray();
            states[0] = 0;
            states[captured - 1] = 7;
            Frame[] output = native.Concat(new[]
            {
                unit[0].DispatcherFrames(),
                method.Logical("Scenario.RecursiveReturnAsync", -2),
            }).ToArray();
            return new StitchScenario
            {
                Kind = AsyncCallstackKind.StateMachineAsync,
                Sync = native.Concat(spine).ToArray(),
                Async = Enumerable.Repeat(method, captured).ToArray(),
                AsyncStates = states,
                MethodCompletions = captured,
                EnableFrameAnnotations = true,
                SkipNormalStackAssertion = true,
                VerifySampleWeights = true,
                ExpectedStitched = output,
                ExpectedWithoutInfrastructureCleanup = output,
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    BoundariesNotFound = 2,
                    IncompleteAsyncRootsNormalized = 2,
                    V1CompletionUnwindFramesCollapsed = spine.Length * 2,
                },
            };
        }

        private static StitchScenario CreatePartiallyCompletedRecursiveV1UnwindScenario(
            int bodies, bool trailingHelpers, bool cleanup)
        {
            StitchScenario scenario = CreateFullyCompletedRecursiveV1UnwindScenario(bodies, trailingHelpers);
            Frame method = scenario.Async[0];
            int firstBody = Array.IndexOf(scenario.Sync, method);
            Frame builder = scenario.Sync[firstBody - 1];
            Frame eventWrite = Frame.CoreLib(
                "System.Diagnostics.Tracing.EventSource.WriteEvent", "Event-source write");
            scenario.Sync = scenario.Sync.Take(2).Concat(new[] { eventWrite, builder, method })
                .Concat(scenario.Sync.Skip(firstBody + 1)).ToArray();
            scenario.MethodCompletions = bodies;
            scenario.EnableInfrastructureCleanup = cleanup;
            Frame[] callers = scenario.Async.Skip(bodies).Select((frame, i) =>
                frame.Logical("Scenario.RecursiveReturnAsync", scenario.AsyncStates[bodies + i])).ToArray();
            Frame[] prefix = scenario.Sync.Take(3).Concat(new[]
            {
                builder, method.Logical("Scenario.RecursiveReturnAsync"),
            }).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = prefix.Concat(callers).ToArray();
            if (cleanup)
            {
                prefix[3] = builder.DispatcherFrames();
            }
            scenario.ExpectedStitched = prefix.Concat(callers).ToArray();
            scenario.ExpectedWithoutNormalization = scenario.Sync.Concat(scenario.Async.Skip(bodies + 1)
                .Select(frame => frame.Logical("Scenario.RecursiveReturnAsync"))).ToArray();
            scenario.ExpectedDiagnostics = new CallStackDiagnosticsExpectation
            {
                SegmentsProcessed = 2,
                BoundariesNotFound = 2,
                IncompleteAsyncRootsNormalized = 2,
                V1SynchronousMoveNextFramesNormalized = 2,
                V1CompletionUnwindFramesCollapsed = (scenario.Sync.Length - prefix.Length) * 2,
                AsyncInfrastructureFramesCollapsed = cleanup ? 2 : 0,
            };
            return scenario;
        }

        private static StitchScenario CreateV1CompletedDispatcherTailScenario(bool preservePrefix = true)
        {
            StitchScenario scenario = CreateV1CompletedUnwindScenario(false);
            const string boxType = "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncStateMachineBox`1[System.Int64,Scenario+<DeepAsync>d__1].";
            Frame box = Frame.CoreLib(boxType + "MoveNext(Thread,Flags)", "LeafBox.MoveNext");
            Frame instrumented = Frame.CoreLib(boxType + "InstrumentedMoveNext(Thread,Flags)", "LeafBox.InstrumentedMoveNext");
            Frame[] prefix = preservePrefix ? new[]
            {
                Frame.Library("clrjit", "Compiler.Compile", "JIT work"),
                Frame.Library("coreclr", "MethodDesc.PrepareCode", "Native work"),
            } : Array.Empty<Frame>();
            Frame[] tail =
            {
                Frame.CoreLib("System.Runtime.CompilerServices.TaskAwaiter+<>c.<OutputWaitEtwEvents>b__12_0(System.Action,System.Threading.Tasks.Task)", "Awaiter.Callback"),
                Frame.CoreLib("System.Threading.Tasks.AwaitTaskContinuation.RunOrScheduleAction(System.Action,System.Boolean)", "Continuation.Run"),
                Frame.CoreLib("System.Threading.Tasks.Task.RunContinuations(System.Object)", "Task.RunContinuations"),
                Frame.CoreLib("System.Threading.Tasks.Task`1[System.Boolean].TrySetResult(System.Boolean)", "Task.TrySetResult"),
                Frame.CoreLib("System.Threading.Tasks.TaskCompletionSource`1[System.Boolean].SetResult(System.Boolean)", "Source.SetResult"),
                Frame.App("Scenario.CompletionCallback"),
                Frame.CoreLib("System.Threading.QueueUserWorkItemCallback.Execute()", "WorkItem.Execute"),
                Frame.ThreadPoolDispatch,
                Frame.WorkerThreadStart,
            };
            scenario.Sync = prefix.Concat(new[] { box, instrumented, Frame.V1MoveNextAsDispatcher }).Concat(tail).ToArray();
            Frame root = scenario.Async.Last().Logical("Scenario.RootAsync", -2);
            scenario.ExpectedStitched = prefix.Concat(preservePrefix ? new[] { box.DispatcherFrames(), root } : new[] { root })
                .Concat(tail).ToArray();
            scenario.ExpectedWithoutNormalization = scenario.Sync.Where(frame => frame != Frame.V1MoveNextAsDispatcher).ToArray();
            scenario.ExpectedDiagnostics = new CallStackDiagnosticsExpectation
            {
                SegmentsProcessed = 2,
                V1CompletionUnwindFramesCollapsed = 4,
            };
            return scenario;
        }

        private static StitchScenario CreateV1TruncatedReturnScenario(int rootCut, bool preservePrefix)
        {
            Frame callback = Frame.CoreLib(
                "System.Runtime.CompilerServices.TaskAwaiter+<>c.<OutputWaitEtwEvents>b__12_0(System.Action,System.Threading.Tasks.Task)",
                "TaskAwaiter.Correlation");
            StitchScenario scenario = CreateV1CompletedUnwindScenario(true, callback);
            int firstBody = Array.IndexOf(scenario.Sync, scenario.Async[1]);
            Frame[] prefix = preservePrefix ? new[]
            {
                Frame.Library("coreclr", "JIT_CompileMethod", "Native.Jit"),
                Frame.Library("clrjit", "Compiler.Compile", "Native.Compiler"),
            } : Array.Empty<Frame>();
            scenario.Sync = prefix.Concat(scenario.Sync.Skip(1).Take(firstBody - 1))
                .Concat(new[] { scenario.Async[2] })
                .Concat(scenario.Sync.Take(scenario.Sync.Length - rootCut)).ToArray();
            scenario.Async = scenario.Async.Concat(new[] { Frame.App("Scenario+<AncestorAsync>d__5.MoveNext") }).ToArray();
            scenario.AsyncStates = new[] { 0, 0, 0, 7, 9 };
            scenario.MethodCompletions = 3;
            return scenario;
        }

        private static StitchScenario CreateRecursiveV1CompletionScenario(int boxCount)
        {
            Frame method = Frame.App("Scenario+<RecursiveAsync>d__1.MoveNext()");
            Frame captured = method.Logical("Scenario.RecursiveAsync", 1);
            const string boxType = "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncStateMachineBox`1[System.Int64,Scenario+<RecursiveAsync>d__1]";
            Frame[] boxes =
            {
                Frame.CoreLib(boxType + ".InstrumentedMoveNext(System.Threading.Thread,Flags)", "Entry wrapper"),
                Frame.CoreLib(boxType + ".MoveNext(System.Threading.Thread)", "Entry with thread"),
                Frame.CoreLib(boxType + ".MoveNext()", "Entry without thread"),
            };
            Frame[] prefix = new[]
            {
                Frame.Library("coreclr", "MethodDesc.PrepareCode", "Runtime work"),
                Frame.Library("clrjit", "JitCompiler.compileMethod", "JIT work"),
            }.Concat(boxes.Take(boxCount)).Concat(new[]
            {
                Frame.CoreLib("System.Runtime.CompilerServices.TaskAwaiter+<>c.<OutputWaitEtwEvents>b__12_0(System.Action,System.Threading.Tasks.Task)",
                    "TaskAwaiter.Correlation"),
                Frame.CoreLib("System.Threading.Tasks.AwaitTaskContinuation.RunOrScheduleAction", "Continuation.RunOrScheduleAction"),
                Frame.CoreLib("System.Threading.Tasks.Task.RunContinuations", "Task.RunContinuations"),
                Frame.CoreLib("System.Threading.Tasks.Task`1[System.Int64].TrySetResult(!0)", "Task.TrySetResult"),
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].SetExistingTaskResult(class System.Object)",
                    "Builder.SetExistingTaskResult"),
            }).ToArray();
            Frame[] returnTail =
            {
                Frame.CoreLib(boxType + ".ExecutionContextCallback(System.Object)", "Return callback"),
                Frame.CoreLib("System.Threading.ExecutionContext.RunInternal", "ExecutionContext.RunInternal"),
                Frame.CoreLib(boxType + ".MoveNext(System.Threading.Thread,Flags)", "Return with flags"),
                Frame.V1MoveNextAsDispatcher,
            };
            Frame yield = Frame.CoreLib(
                "System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter+<>c.<OutputCorrelationEtwEvent>b__6_0(System.Action,System.Threading.Tasks.Task)",
                "Yield.Correlation");
            Frame[] logical = Enumerable.Repeat(captured, 3).ToArray();
            Frame[] host = { Frame.ThreadPoolDispatch, Frame.WorkerThreadStart };
            return new StitchScenario
            {
                Kind = AsyncCallstackKind.StateMachineAsync,
                Sync = prefix.Concat(new[] { method }).Concat(returnTail).Concat(boxes)
                    .Concat(new[] { yield }).Concat(host).ToArray(),
                Async = Enumerable.Repeat(captured, 4).ToArray(),
                AsyncStates = new[] { 0, 1, 1, 1 },
                MethodCompletions = 1,
                EnableInfrastructureCleanup = true,
                EnableFrameAnnotations = true,
                SkipNormalStackAssertion = true,
                VerifySampleWeights = true,
                ExpectedStitched = prefix.Take(2).Concat(new[] { prefix[2].DispatcherFrames(),
                    method.Logical("Scenario.RecursiveAsync", -2) }).Concat(logical).Concat(host).ToArray(),
                ExpectedWithoutInfrastructureCleanup = prefix.Concat(new[] { method.Logical("Scenario.RecursiveAsync", -2) })
                    .Concat(logical).Concat(new[] { yield }).Concat(host).ToArray(),
                ExpectedWithoutNormalization = prefix.Concat(new[] { method }).Concat(returnTail)
                    .Concat(logical).Concat(new[] { yield }).Concat(host).ToArray(),
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V1CompletionTailUsed = 2,
                    V1InfrastructureFramesCollapsed = 14,
                    AsyncInfrastructureFramesCollapsed = 2 * (boxCount + 6),
                    ProfilerEnvelopeFramesCollapsed = 2 * (boxCount + 5),
                    RuntimeResumptionBridgeFramesCollapsed = 2,
                },
            };
        }

        private static StitchScenario CreateNestedCompletedV1OuterYieldScenario()
        {
            Frame native = Frame.Library("clrjit", "JitCompiler.compileMethod", "JIT work");
            Frame resume = Frame.CoreLib("System.Runtime.CompilerServices.AsyncProfiler+ResumeAsyncContext.Resume", "Profiler.Resume");
            Frame dispatch = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncProfilerAsyncStateMachineBox`1[System.Int64,Scenario+<ParallelWhenAllAsync>d__12].MoveNextAsDispatcher(class System.Threading.Thread,value class Flags)",
                "Nested dispatch");
            Frame inner = Frame.App("Scenario+<ParallelWhenAllAsync>d__12.MoveNext")
                .Logical("Scenario.ParallelWhenAllAsync", state: 0);
            Frame outerLeaf = Frame.App("Scenario+<BurnCpuAsync>d__115.MoveNext");
            Frame outerRoot = Frame.App("Scenario+<ParallelBranchAsync>d__13.MoveNext");
            Frame yield = Frame.CoreLib(
                "System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter+<>c.<OutputCorrelationEtwEvent>b__6_0(System.Action,System.Threading.Tasks.Task)",
                "Yield.Correlation");
            Frame[] structural = { native, resume, dispatch, inner, yield, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart };
            return new StitchScenario
            {
                Kind = AsyncCallstackKind.StateMachineAsync,
                Sync = new[] { native, resume, dispatch, outerRoot, outerLeaf, Frame.V1MoveNextAsDispatcher,
                    yield, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart },
                AsyncSegments = new[]
                {
                    new AsyncSegment(AsyncCallstackKind.StateMachineAsync, new[] { outerLeaf, outerRoot }, new[] { 0, 1 })
                        { MethodCompletions = 2 },
                    AsyncSegment.StateMachine(inner),
                },
                ExpectedStitched = new[] { native, resume.DispatcherFrames(), inner, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart },
                ExpectedWithoutInfrastructureCleanup = structural,
                EnableFrameAnnotations = true,
                EnableInfrastructureCleanup = true,
                VerifySampleWeights = true,
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 4,
                    AsyncInfrastructureFramesCollapsed = 6,
                    ProfilerEnvelopeFramesCollapsed = 4,
                    RuntimeResumptionBridgeFramesCollapsed = 2,
                },
            };
        }

        #endregion
    }
}
