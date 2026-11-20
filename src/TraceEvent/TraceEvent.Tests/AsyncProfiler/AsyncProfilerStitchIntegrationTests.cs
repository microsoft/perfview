// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Computers;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;
using Microsoft.Diagnostics.Tracing.Stacks;

using Xunit;

namespace TraceEventTests
{
    /// <summary>
    /// Production-path async stitching tests. Each scenario declares only its physical sync stack, profiler async
    /// stack, and expected stitched stack (all leaf to root). The shared fixture turns those declarations into a
    /// complete synthetic EventPipe trace with sample-profiler events, StackBlocks, async-profiler buffers, symbols,
    /// and process/thread records, then runs <see cref="SampleProfilerThreadTimeComputer.GenerateThreadTimeStacks"/>.
    /// </summary>
    public partial class AsyncProfilerStitchIntegrationTests
    {
        [Theory]
        [InlineData(100_015, 100_010, 100_020)]
        [InlineData(100_035, 100_030, 100_040)]
        public void BorrowedUserStack_UsesSourceTimeForReconstruction(long userQpc, long asyncStart, long asyncEnd)
        {
            Frame work = Frame.App("Scenario.Work");
            Frame current = Frame.App("Scenario.CurrentAsync");
            Frame parent = Frame.App("Scenario.ParentAsync");
            var scenario = new StitchScenario
            {
                Sync = new[] { work, current, Frame.V2Wrapper(0), Frame.V2InstrumentedDispatch,
                    Frame.V2Dispatch, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart },
                Async = new[] { current, parent },
                AsyncStartQpc = asyncStart,
                AsyncEndQpc = asyncEnd,
                ExpectedStitched = new[] { work, current, parent, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart },
                VerifyPublicBuilder = true,
                VerifyThreadTimeComputer = true,
                IncludeEventSourceEvents = false,
                AllowSupplementalSamples = true,
                VerifySampleWeights = true,
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V2InfrastructureFramesCollapsed = 4,
                },
                ConfigureTraceLog = log =>
                {
                    ConfigureCapturedRuntime(log, "11.0.0");
                    foreach (TraceEvent data in log.Events.Where(e => e.CallStackIndex() != CallStackIndex.Invalid))
                    {
                        log.AddStackUserModeTimeStampQPC(data.EventIndex, userQpc);
                    }
                },
                AssertTransformContext = context => Assert.Equal(userQpc, context.TimestampQpc),
                AssertPublicApi = (log, path) =>
                {
                    var query = new AsyncProfilerCallStackComputer(log);
                    var output = new MutableTraceEventStackSource(log);
                    var builder = new AsyncProfilerCallStackSourceBuilder(output);
                    foreach (TraceEvent data in log.Events.Where(e => e.CallStackIndex() != CallStackIndex.Invalid))
                    {
                        Assert.True(data.TryGetCallStackIndex(out var physical, out long timestamp));
                        Assert.Equal(userQpc, timestamp);
                        Assert.Empty(query.GetCallStacks(data));
                        Assert.Single(query.GetCallStacks(data.ProcessID, (ulong)data.ThreadID, timestamp));
#pragma warning disable CS0618
                        Assert.Equal(output.GetCallStackThread(physical, data.Thread()),
                            builder.GetCallStack(physical, data.TimeStampQPC));
#pragma warning restore CS0618
                        Assert.Equal(builder.GetCallStack(physical, timestamp), builder.GetCallStack(data));
                    }
                },
            };
            scenario.AssertProductionStitch();
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
        public void ContextParent_RecordedProgressPreservesCapturedMetadataInBothHosts(
            bool stateMachineParent, bool enableFrameAnnotations, bool enableInfrastructureCleanup)
        {
            Frame current = Frame.App("Scenario.ChildAsync");
            Frame ownerCurrent = Frame.App(stateMachineParent
                ? "Scenario+<OwnerCurrentAsync>d__3.MoveNext" : "Scenario.OwnerCurrentAsync");
            Frame ownerRoot = Frame.App(stateMachineParent
                ? "Scenario+<OwnerRootAsync>d__2.MoveNext" : "Scenario.OwnerRootAsync");
            Frame[] parentFrames =
            {
                Frame.App("Scenario.CompletedAsync"),
                Frame.App("Scenario.UnwoundAsync"),
                ownerCurrent,
                ownerRoot,
            };
            var parentKind = stateMachineParent
                ? AsyncCallstackKind.StateMachineAsync : AsyncCallstackKind.RuntimeAsync;
            var scenario = new StitchScenario
            {
                Sync = new[]
                {
                    current, Frame.V2Wrapper(0), Frame.V2InstrumentedDispatch, Frame.V2Dispatch,
                    Frame.ThreadPoolDispatch, Frame.WorkerThreadStart,
                },
                Async = new[] { current },
                ContextParent = new AsyncSegment(parentKind, parentFrames, new[] { 0, 1, 4, 7 }),
                ParentMethodCompletions = 1,
                ParentExceptionCompletions = 1,
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V2InfrastructureFramesCollapsed = 4,
                },
                EnableParentContexts = true,
                EnableFrameAnnotations = enableFrameAnnotations,
                EnableInfrastructureCleanup = enableInfrastructureCleanup,
                IncludeEventSourceEvents = false,
                VerifyPublicBuilder = true,
                VerifyThreadTimeComputer = true,
                VerifySampleWeights = true,
                ExpectedStitched = new[]
                {
                    current,
                    stateMachineParent ? ownerCurrent.Logical("Scenario.OwnerCurrentAsync", -1) : ownerCurrent,
                    stateMachineParent ? ownerRoot.Logical("Scenario.OwnerRootAsync", 7) : ownerRoot,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                AssertTransformContext = context =>
                {
                    AsyncProfilerStitchedFrame[] parents = context.Frames.Where(frame =>
                        frame.ContextKind == AsyncProfilerStitchedFrameContextKind.ParentContext).ToArray();
                    Assert.Equal(new[] { 2, 3 }, parents.Select(frame => frame.SegmentFrameIndex));
                    Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncContextParentCurrent, parents[0].Origin);
                    Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncContextParent, parents[1].Origin);
                },
                AssertStitchedStackSource = stackSource =>
                {
                    int parents = 0;
                    int runningParents = 0;
                    for (int i = 0; i < stackSource.CallFrameIndexLimit; i++)
                    {
                        var frameIndex = (StackSourceFrameIndex)i;
                        if (!stackSource.TryGetAsyncProfilerCallStackFrameInfo(
                            frameIndex, out AsyncProfilerCallStackSourceFrameInfo info) || !info.IsParentContext)
                        {
                            continue;
                        }
                        parents++;
                        Assert.Equal(parentKind, info.Kind);
                        Assert.False(info.IsCurrent);
                        Assert.False(info.IsCompleting);
                        Assert.False(info.IsCompletionUnwind);
                        Assert.False(info.IsDispatchTarget);
                        Assert.Equal(info.CodeAddress, stackSource.GetFrameCodeAddress(frameIndex));
                        Assert.Equal(stackSource.TraceLog.CodeAddresses[info.CodeAddress].Address, info.MethodId);
                        string name = stackSource.GetFrameName(frameIndex, false);
                        if (info.IsParentContextCurrent)
                        {
                            runningParents++;
                            Assert.Contains(ownerCurrent.SymbolName,
                                stackSource.TraceLog.CodeAddresses[info.CodeAddress].FullMethodName);
                            Assert.Equal(stateMachineParent ? 4 : 0, info.State);
                            if (stateMachineParent)
                            {
                                Assert.StartsWith("[Async V1] ", name);
                                Assert.EndsWith("[ordinal 3, state -1]", name);
                            }
                            else
                            {
                                Assert.StartsWith("[Async V2] ", name);
                                Assert.DoesNotContain("state ", name);
                            }
                        }
                        else
                        {
                            Assert.Equal(stateMachineParent ? 7 : 0, info.State);
                        }
                    }
                    Assert.Equal(2, parents);
                    Assert.Equal(1, runningParents);
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 0, false, false)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 1, false, false)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 3, false, false)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 0, false, true)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 1, false, true)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 3, false, true)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 0, true, true)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 1, true, true)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 3, true, true)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 0, false, false)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 1, false, false)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 3, false, false)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 0, false, true)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 1, false, true)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 3, false, true)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 0, true, true)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 1, true, true)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 3, true, true)]
        public void IncompletePhysicalRoot_IsStructuralAndIndependentOfCutDepth(
            int kindValue, int fragmentLength, bool enableInfrastructureCleanup, bool enableNormalization)
        {
            var kind = (AsyncCallstackKind)kindValue;
            Frame work = Frame.App("Scenario.SampledWork");
            Frame[] captured = Enumerable.Range(0, 7)
                .Select(i => Frame.App(kind == AsyncCallstackKind.StateMachineAsync
                    ? "Scenario+<Level" + i + "Async>d__" + i + ".MoveNext"
                    : "Scenario.Level" + i + "Async"))
                .ToArray();
            Frame current = kind == AsyncCallstackKind.StateMachineAsync
                ? captured[4].Logical("Scenario.Level4Async", state: -1) : captured[4];
            Frame parent = kind == AsyncCallstackKind.StateMachineAsync
                ? captured[5].Logical("Scenario.Level5Async", state: 5) : captured[5];
            Frame ancestor = kind == AsyncCallstackKind.StateMachineAsync
                ? captured[6].Logical("Scenario.Level6Async", state: 6) : captured[6];
            Frame[] fragment = Enumerable.Range(0, fragmentLength)
                .Select(i => Frame.App("OpaquePhysicalFragment.Frame" + i))
                .ToArray();
            var scenario = new StitchScenario
            {
                Kind = kind,
                Sync = new[] { work, captured[4], captured[3], captured[2] }.Concat(fragment).ToArray(),
                Async = captured,
                AsyncStates = Enumerable.Range(0, captured.Length).ToArray(),
                ExpectedStitched = new[] { work, current, parent, ancestor },
                EnableFrameAnnotations = true,
                EnableNormalization = enableNormalization,
                EnableInfrastructureCleanup = enableInfrastructureCleanup,
                OnlyManagedCodeStacks = true,
                SkipNormalStackAssertion = true,
                VerifySampleWeights = true,
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    BoundariesNotFound = 2,
                    V1InlineFallbackUsed = kind == AsyncCallstackKind.StateMachineAsync ? 2 : 0,
                    V2TruncatedSuffixFallbackUsed = kind == AsyncCallstackKind.RuntimeAsync ? 2 : 0,
                    IncompleteAsyncRootsNormalized = 2,
                },
                AssertTransformContext = context =>
                {
                    Assert.Equal(AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot, context.Frames[context.Frames.Count - 1].Origin);
                    AsyncProfilerStitchedFrame anchor = Assert.Single(context.Frames.Where(
                        frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent));
                    Assert.Equal(4, anchor.SegmentFrameIndex);
                    if (kind == AsyncCallstackKind.StateMachineAsync)
                    {
                        Assert.Equal(4, anchor.Segment.FrameStateAt(anchor.SegmentFrameIndex));
                    }
                    Assert.NotEqual(CodeAddressIndex.Invalid, anchor.CodeAddress);
                    Assert.Null(context.Frames[context.Frames.Count - 1].Context);
                },
                AssertStitchedStackSource = stackSource =>
                {
                    stackSource.ForEach(sample =>
                    {
                        int brokenRoots = 0;
                        for (StackSourceCallStackIndex stack = sample.StackIndex;
                             stack != StackSourceCallStackIndex.Invalid;
                             stack = stackSource.GetCallerIndex(stack))
                        {
                            StackSourceFrameIndex frame = stackSource.GetFrameIndex(stack);
                            if (stackSource.GetFrameName(frame, false) != "BROKEN")
                            {
                                continue;
                            }
                            brokenRoots++;
                            Assert.NotEqual(StackSourceFrameIndex.Broken, frame);
                            Assert.Equal("BROKEN", stackSource.GetFrameName(frame, false));
                            Assert.Equal(CodeAddressIndex.Invalid, stackSource.GetFrameCodeAddress(frame));
                            Assert.False(stackSource.TryGetAsyncProfilerCallStackFrameInfo(frame, out _));
                            StackSourceCallStackIndex caller = stackSource.GetCallerIndex(stack);
                            Assert.StartsWith("Thread (",
                                stackSource.GetFrameName(stackSource.GetFrameIndex(caller), false));
                        }
                        Assert.Equal(1, brokenRoots);
                    });

                    var exportedNames = new Dictionary<string, int>();
                    var exportedFrames = new Dictionary<int, StackSourceWriterHelper.FrameInfo>();
                    List<StackSourceWriterHelper.Sample> samples =
                        Assert.Single(StackSourceWriterHelper.GetSortedSamplesPerThread(stackSource)).Value;
                    IReadOnlyList<StackSourceWriterHelper.ProfileEvent> events =
                        StackSourceWriterHelper.GetProfileEvents(stackSource, samples, exportedNames, exportedFrames);
                    Assert.True(StackSourceWriterHelper.Validate(events));
                    Assert.True(exportedNames.TryGetValue("BROKEN", out int brokenId));
                    Assert.Contains(events, profileEvent =>
                        profileEvent.FrameId == brokenId &&
                        profileEvent.Type == StackSourceWriterHelper.ProfileEventType.Open);
                    Assert.Contains(events, profileEvent =>
                        profileEvent.FrameId == brokenId &&
                        profileEvent.Type == StackSourceWriterHelper.ProfileEventType.Close);
                    Assert.StartsWith("Thread (",
                        exportedNames.Single(pair => pair.Value == exportedFrames[brokenId].ParentId).Key);
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(0, false, false)]
        [InlineData(1, false, true)]
        [InlineData(3, false, true)]
        [InlineData(3, true, true)]
        public void V1_SurvivingDispatcher_IncompleteCallerTailIsStructural(
            int infrastructureLength, bool enableInfrastructureCleanup, bool enableNormalization)
        {
            StitchScenario scenario = CreateV1SurvivingDispatcherRootScenario(infrastructureLength);
            scenario.EnableNormalization = enableNormalization;
            scenario.EnableInfrastructureCleanup = enableInfrastructureCleanup;
            scenario.ExpectedDiagnostics.IncompleteAsyncRootsNormalized = 2;
            scenario.AssertTransformContext = context =>
            {
                Assert.Equal(AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot, context.Frames.Last().Origin);
                Assert.Single(context.Frames.Where(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot));
                Assert.Null(context.Frames.Last().Context);
                AsyncProfilerStitchedFrame target = Assert.Single(context.Frames.Where(frame => frame.IsStateMachineBoxCarrier));
                Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget, target.Origin);
                Assert.Equal(0, target.Segment.FrameStateAt(target.SegmentFrameIndex));
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("managed")]
        [InlineData("native")]
        [InlineData("unresolved")]
        [InlineData("foreign")]
        [InlineData("interrupted")]
        public void V1_SurvivingDispatcher_UnverifiedPhysicalCallerTailIsPreserved(string obstruction)
        {
            StitchScenario scenario = CreateV1SurvivingDispatcherRootScenario(3);
            Frame[] tail;
            if (obstruction == "managed" || obstruction == "native")
            {
                Frame host = obstruction == "managed" ? Frame.App("CustomScheduler.Execute") :
                    Frame.Library("NativeHost", "NativeHost.Enter", "NativeHost.Enter");
                scenario.Sync = scenario.Sync.Concat(new[] { host }).ToArray();
                tail = new[] { host };
            }
            else
            {
                Frame last = scenario.Sync.Last();
                if (obstruction == "unresolved")
                {
                    scenario.OmitSymbolFor = last;
                    tail = new[] { last };
                }
                else if (obstruction == "foreign")
                {
                    Frame foreign = Frame.Library("OtherRuntime", last.SymbolName, "ForeignBox");
                    scenario.Sync[scenario.Sync.Length - 1] = foreign;
                    tail = new[] { foreign };
                }
                else
                {
                    Frame interruption = Frame.App("Scenario.SynchronousCallback");
                    scenario.Sync = scenario.Sync.Take(scenario.Sync.Length - 1)
                        .Concat(new[] { interruption, last }).ToArray();
                    tail = new[] { interruption, last };
                }
                scenario.ExpectedDiagnostics.V1InfrastructureFramesCollapsed -= 2;
            }
            scenario.ExpectedStitched = scenario.ExpectedStitched.Concat(tail).ToArray();
            scenario.AssertTransformContext = context =>
                Assert.DoesNotContain(context.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot);

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, "CustomScheduler.Execute")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, "UiSynchronizationContext.Callback")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, "DedicatedThread.Run")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, "CustomScheduler.Execute")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, "UiSynchronizationContext.Callback")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, "DedicatedThread.Run")]
        public void CompletePhysicalSegment_PreservesNonThreadPoolHosts(int kindValue, string hostName)
        {
            var kind = (AsyncCallstackKind)kindValue;
            Frame work = Frame.App("Scenario.SampledWork");
            Frame current = Frame.App("Scenario+<CurrentAsync>d__1.MoveNext");
            Frame parent = Frame.App("Scenario+<ParentAsync>d__2.MoveNext");
            Frame host = Frame.App(hostName);
            Frame entry = Frame.App("Application.ThreadEntry");
            var scenario = new StitchScenario
            {
                Kind = kind,
                Sync = new[]
                {
                    work, current,
                    kind == AsyncCallstackKind.StateMachineAsync ? Frame.V1MoveNextAsDispatcher : Frame.V2Wrapper(0),
                    host, entry,
                },
                Async = new[] { current, parent },
                AsyncStates = new[] { 0, 1 },
                ExpectedStitched = new[]
                {
                    work,
                    kind == AsyncCallstackKind.StateMachineAsync ? current.Logical("Scenario.CurrentAsync", state: -1) : current,
                    kind == AsyncCallstackKind.StateMachineAsync ? parent.Logical("Scenario.ParentAsync", state: 1) : parent,
                    host, entry,
                },
                EnableFrameAnnotations = true,
                EnableInfrastructureCleanup = true,
                OnlyManagedCodeStacks = true,
                IncludeEventSourceEvents = false,
                SkipNormalStackAssertion = true,
                VerifySampleWeights = true,
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V1InlineFallbackUsed = kind == AsyncCallstackKind.StateMachineAsync ? 2 : 0,
                },
                AssertTransformContext = context =>
                    Assert.DoesNotContain(context.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot),
                AssertStitchedStackSource = stackSource =>
                {
                    stackSource.ForEach(sample =>
                    {
                        for (StackSourceCallStackIndex stack = sample.StackIndex;
                             stack != StackSourceCallStackIndex.Invalid;
                             stack = stackSource.GetCallerIndex(stack))
                        {
                            Assert.NotEqual("BROKEN",
                                stackSource.GetFrameName(stackSource.GetFrameIndex(stack), false));
                        }
                    });
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, (int)AsyncCallstackKind.RuntimeAsync)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, (int)AsyncCallstackKind.StateMachineAsync)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, (int)AsyncCallstackKind.RuntimeAsync)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, (int)AsyncCallstackKind.StateMachineAsync)]
        public void NestedIncompleteRoot_PreservesCompleteInnerSegments(
            int outerKindValue, int innerKindValue)
        {
            var outerKind = (AsyncCallstackKind)outerKindValue;
            var innerKind = (AsyncCallstackKind)innerKindValue;
            Frame work = Frame.App("Scenario.SampledWork");
            Frame bridge = Frame.App("Scenario.SynchronousBridge");
            Frame fragment = Frame.App("Scenario.IncompletePhysicalFragment");
            Frame[] outer = Enumerable.Range(0, 7)
                .Select(i => Frame.App(outerKind == AsyncCallstackKind.StateMachineAsync
                    ? "Scenario+<Outer" + i + "Async>d__" + i + ".MoveNext"
                    : "Scenario.Outer" + i + "Async"))
                .ToArray();
            Frame[] inner = Enumerable.Range(0, 2)
                .Select(i => Frame.App(innerKind == AsyncCallstackKind.StateMachineAsync
                    ? "Scenario+<Inner" + i + "Async>d__" + (10 + i) + ".MoveNext"
                    : "Scenario.Inner" + i + "Async"))
                .ToArray();
            var scenario = new StitchScenario
            {
                Sync = new[]
                {
                    work, inner[0],
                    innerKind == AsyncCallstackKind.StateMachineAsync ? Frame.V1MoveNextAsDispatcher : Frame.V2Wrapper(0),
                    bridge, outer[4], outer[3], outer[2], fragment,
                },
                AsyncSegments = new[]
                {
                    new AsyncSegment(outerKind, outer, Enumerable.Range(0, 7).ToArray()),
                    new AsyncSegment(innerKind, inner, new[] { 0, 1 }),
                },
                ExpectedStitched = new[]
                {
                    work,
                    innerKind == AsyncCallstackKind.StateMachineAsync ? inner[0].Logical("Scenario.Inner0Async", state: -1) : inner[0],
                    innerKind == AsyncCallstackKind.StateMachineAsync ? inner[1].Logical("Scenario.Inner1Async", state: 1) : inner[1],
                    bridge,
                    outerKind == AsyncCallstackKind.StateMachineAsync ? outer[4].Logical("Scenario.Outer4Async", state: -1) : outer[4],
                    outerKind == AsyncCallstackKind.StateMachineAsync ? outer[5].Logical("Scenario.Outer5Async", state: 5) : outer[5],
                    outerKind == AsyncCallstackKind.StateMachineAsync ? outer[6].Logical("Scenario.Outer6Async", state: 6) : outer[6],
                },
                EnableFrameAnnotations = true,
                EnableInfrastructureCleanup = true,
                OnlyManagedCodeStacks = true,
                SkipNormalStackAssertion = true,
                VerifySampleWeights = true,
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 4,
                    BoundariesNotFound = 2,
                    IncompleteAsyncRootsNormalized = 2,
                    V2TruncatedSuffixFallbackUsed = outerKind == AsyncCallstackKind.RuntimeAsync ? 2 : 0,
                    V1InlineFallbackUsed =
                        (outerKind == AsyncCallstackKind.StateMachineAsync ? 2 : 0) +
                        (innerKind == AsyncCallstackKind.StateMachineAsync ? 2 : 0),
                },
                AssertTransformContext = context =>
                {
                    Assert.Equal(2, context.Frames.Count(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent));
                    Assert.Equal(AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot, context.Frames[context.Frames.Count - 1].Origin);
                    Assert.Single(context.Frames.Where(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot));
                },
            };

            scenario.AssertProductionStitch();
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
        public void MissingInnerV1Boundary_PreservesOuterV2AndCapturedCarrierState(
            bool carrier, bool enableInfrastructureCleanup, bool enableNormalization)
        {
            Frame work = Frame.App("Scenario.SampledWork");
            Frame bridge = Frame.App("Scenario.InterContextCallback");
            Frame[] inner = Enumerable.Range(0, 4)
                .Select(i => Frame.App("Scenario+<Inner" + i + "Async>d__" + (10 + i) + ".MoveNext"))
                .ToArray();
            Frame[] outer =
            {
                Frame.App("Scenario.OuterV2Async"),
                Frame.App("Scenario.OuterV2ParentAsync"),
            };
            Frame physicalCurrent = carrier
                ? Frame.CoreLib(
                    "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncStateMachineBox`1" +
                    "[System.Int64,Scenario+<Inner2Async>d__12].MoveNext(class System.Threading.Thread,value class Flags)",
                    "InnerBoxCarrier")
                : inner[2];
            Frame[] expected =
            {
                work, inner[2].Logical("Scenario.Inner2Async", carrier ? 5 : -1),
                inner[3].Logical("Scenario.Inner3Async", 3),
                Frame.V1MoveNextAsDispatcher, bridge, outer[0], outer[1],
                Frame.ThreadPoolDispatch, Frame.WorkerThreadStart,
            };
            var scenario = new StitchScenario
            {
                Sync = new[]
                {
                    work, physicalCurrent, inner[1], inner[0], Frame.V1MoveNextAsDispatcher,
                    bridge, outer[0], Frame.V2Wrapper(0), Frame.V2InstrumentedDispatch, Frame.V2Dispatch,
                    Frame.ThreadPoolDispatch, Frame.WorkerThreadStart,
                },
                AsyncSegments = new[]
                {
                    AsyncSegment.Runtime(outer),
                    new AsyncSegment(AsyncCallstackKind.StateMachineAsync, inner, new[] { 0, 1, 5, 3 }),
                },
                OmitSymbolFor = Frame.V1MoveNextAsDispatcher,
                ExpectedStitched = expected,
                ExpectedWithoutInfrastructureCleanup = expected,
                EnableFrameAnnotations = true,
                EnableNormalization = enableNormalization,
                EnableInfrastructureCleanup = enableInfrastructureCleanup,
                OnlyManagedCodeStacks = true,
                SkipNormalStackAssertion = true,
                VerifySampleWeights = true,
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 4,
                    BoundariesNotFound = 2,
                    V1InlineFallbackUsed = 2,
                    V2InfrastructureFramesCollapsed = 4,
                },
                AssertTransformContext = context =>
                {
                    AsyncProfilerStitchedFrame innerAnchor = Assert.Single(context.Frames.Where(frame =>
                        ReferenceEquals(frame.Context, context.Segments[0]) &&
                        frame.Origin == (carrier
                            ? AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget : AsyncProfilerStitchedFrameOrigin.AsyncCurrent)));
                    Assert.Equal(2, innerAnchor.SegmentFrameIndex);
                    Assert.Equal(carrier, innerAnchor.IsStateMachineBoxCarrier);
                    Assert.Equal(5, innerAnchor.Segment.FrameStateAt(2));
                    AsyncProfilerStitchedFrame outerAnchor = Assert.Single(context.Frames.Where(frame =>
                        frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent &&
                        ReferenceEquals(frame.Context, context.Segments[1])));
                    Assert.Equal(0, outerAnchor.SegmentFrameIndex);
                    Assert.Equal(1, context.Diagnostics.V1TruncatedSuffixFallbackUsed);
                    Assert.DoesNotContain(context.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot);
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, false, "NativeHost", 1)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, true, "NativeHost", 0)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, false, "ntdll", 0)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, false, "NativeHost", 1)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, true, "NativeHost", 0)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, false, "libc", 0)]
        public void PhysicalRoot_BrokenMarkerMatchesSynchronousPolicy(
            int kindValue, bool onlyManagedCodeStacks, string hostModule, int expectedBroken)
        {
            StitchScenario scenario = CreateRuntimeResumptionBridgeScenario((AsyncCallstackKind)kindValue);
            Frame host = Frame.Library(hostModule, "ThreadEntry", "Host.ThreadEntry");
            scenario.Sync = scenario.Sync.Concat(new[] { host }).ToArray();
            scenario.ExpectedStitched = scenario.ExpectedStitched.Concat(new[] { host }).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = scenario.ExpectedWithoutInfrastructureCleanup.Concat(new[] { host }).ToArray();
            scenario.OnlyManagedCodeStacks = onlyManagedCodeStacks;
            scenario.IncludeEventSourceEvents = false;
            scenario.VerifyPublicBuilder = true;
            scenario.VerifyThreadTimeComputer = true;
            scenario.AssertStitchedStackSource = source => AssertScenarioBrokenRoots(source, expectedBroken);
            scenario.AssertProductionStitch();
        }

        [Fact]
        public void MixedRuntimeAndStateMachineAsync_PreservesSynchronousAndNativeBridge()
        {
            Frame innerWork = Frame.App("Scenario.InnerSynchronousWork");
            Frame innerCurrent = Frame.App("Scenario+<InnerV1Async>d__4.MoveNext");
            Frame innerParent = Frame.App("Scenario+<InnerV1ParentAsync>d__3.MoveNext");
            Frame logicalInnerCurrent = innerCurrent.Logical("Scenario.InnerV1Async");
            Frame logicalInnerParent = innerParent.Logical("Scenario.InnerV1ParentAsync");
            Frame executionContextCallback = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncStateMachineBox.ExecutionContextCallback",
                "AsyncStateMachineBox.ExecutionContextCallback");
            Frame runInternal = Frame.CoreLib(
                "System.Threading.ExecutionContext.RunInternal", "ExecutionContext.RunInternal");
            Frame boxMoveNext = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncStateMachineBox.MoveNext(Thread,Flags)",
                "AsyncStateMachineBox.MoveNext(Thread,Flags)");
            Frame knownV1Infrastructure1 = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncProfilerAsyncStateMachineBox.InstrumentedMoveNext",
                "KnownV1BoxInfrastructure1");
            Frame knownV1Infrastructure2 = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncStateMachineBox.MoveNext(Thread)",
                "KnownV1BoxInfrastructure2");
            Frame unclassifiedSchedulingFrame = Frame.CoreLib(
                "TestOnly.UnclassifiedSchedulingFrame", "UnclassifiedSchedulingFrame");
            Frame nativeBridgeFrame = Frame.Library(
                "SyntheticNativeBridge", "SyntheticNativeBridge.NativeBridgeFrame", "NativeBridgeFrame");
            Frame synchronousBridgeFrame = Frame.App("Scenario.SynchronousBridgeFrame");
            Frame outerCurrent = Frame.App("Scenario.OuterV2Async");
            Frame outerParent = Frame.App("Scenario.OuterV2ParentAsync");

            var scenario = new StitchScenario
            {
                Sync = new[]
                {
                    innerWork,
                    innerCurrent,
                    executionContextCallback,
                    runInternal,
                    boxMoveNext,
                    Frame.V1MoveNextAsDispatcher,
                    knownV1Infrastructure1,
                    knownV1Infrastructure2,
                    unclassifiedSchedulingFrame,
                    nativeBridgeFrame,
                    synchronousBridgeFrame,
                    outerCurrent,
                    Frame.V2Wrapper(0),
                    Frame.V2InstrumentedDispatch,
                    Frame.V2Dispatch,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                AsyncSegments = new[]
                {
                    AsyncSegment.Runtime(outerCurrent, outerParent),
                    AsyncSegment.StateMachine(logicalInnerCurrent, logicalInnerParent),
                },
                ExpectedStitched = new[]
                {
                    innerWork,
                    logicalInnerCurrent,
                    logicalInnerParent,
                    unclassifiedSchedulingFrame,
                    nativeBridgeFrame,
                    synchronousBridgeFrame,
                    outerCurrent,
                    outerParent,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                FilteredOut = new[]
                {
                    unclassifiedSchedulingFrame,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 4,
                    V1InlineFallbackUsed = 2,
                    V1InfrastructureFramesCollapsed = 4,
                    V2InfrastructureFramesCollapsed = 4,
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, (int)AsyncCallstackKind.StateMachineAsync)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, (int)AsyncCallstackKind.StateMachineAsync)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, (int)AsyncCallstackKind.RuntimeAsync)]
        public void NestedSegments_OtherKindOrderings_PreserveUnclassifiedBridge(
            int innerKindValue, int outerKindValue)
        {
            var innerKind = (AsyncCallstackKind)innerKindValue;
            var outerKind = (AsyncCallstackKind)outerKindValue;
            Frame innerWork = Frame.App("Scenario.InnerSynchronousWork");
            Frame innerIdentity = innerKind == AsyncCallstackKind.StateMachineAsync
                ? Frame.App("Scenario+<InnerV1Async>d__4.MoveNext")
                : Frame.App("Scenario.InnerV2Async");
            Frame innerCurrent = innerKind == AsyncCallstackKind.StateMachineAsync
                ? innerIdentity.Logical("Scenario.InnerV1Async")
                : innerIdentity;
            Frame innerParentIdentity = innerKind == AsyncCallstackKind.StateMachineAsync
                ? Frame.App("Scenario+<InnerV1ParentAsync>d__3.MoveNext")
                : Frame.App("Scenario.InnerV2ParentAsync");
            Frame innerParent = innerKind == AsyncCallstackKind.StateMachineAsync
                ? innerParentIdentity.Logical("Scenario.InnerV1ParentAsync")
                : innerParentIdentity;

            Frame outerIdentity = outerKind == AsyncCallstackKind.StateMachineAsync
                ? Frame.App("Scenario+<OuterV1Async>d__2.MoveNext")
                : Frame.App("Scenario.OuterV2Async");
            Frame outerCurrent = outerKind == AsyncCallstackKind.StateMachineAsync
                ? outerIdentity.Logical("Scenario.OuterV1Async")
                : outerIdentity;
            Frame outerParentIdentity = outerKind == AsyncCallstackKind.StateMachineAsync
                ? Frame.App("Scenario+<OuterV1ParentAsync>d__1.MoveNext")
                : Frame.App("Scenario.OuterV2ParentAsync");
            Frame outerParent = outerKind == AsyncCallstackKind.StateMachineAsync
                ? outerParentIdentity.Logical("Scenario.OuterV1ParentAsync")
                : outerParentIdentity;

            Frame v1LeafTransition = Frame.CoreLib(
                "System.Threading.ExecutionContext.RunInternal", "KnownV1LeafTransition");
            Frame v1RootInfrastructure = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncProfilerAsyncStateMachineBox.InstrumentedMoveNext",
                "KnownV1RootInfrastructure");
            Frame unclassifiedSchedulingFrame = Frame.CoreLib(
                "TestOnly.UnclassifiedSchedulingFrame", "UnclassifiedSchedulingFrame");
            Frame nativeBridgeFrame = Frame.Library(
                "SyntheticNativeBridge", "SyntheticNativeBridge.NativeBridgeFrame", "NativeBridgeFrame");
            Frame synchronousBridgeFrame = Frame.App("Scenario.SynchronousBridgeFrame");

            var sync = new List<Frame> { innerWork, innerIdentity };
            AddSyntheticDispatcher(sync, innerKind, v1LeafTransition, v1RootInfrastructure);
            sync.Add(unclassifiedSchedulingFrame);
            sync.Add(nativeBridgeFrame);
            sync.Add(synchronousBridgeFrame);
            sync.Add(outerIdentity);
            AddSyntheticDispatcher(sync, outerKind, v1LeafTransition, v1RootInfrastructure);
            sync.Add(Frame.ThreadPoolDispatch);
            sync.Add(Frame.WorkerThreadStart);

            AsyncSegment innerSegment = innerKind == AsyncCallstackKind.StateMachineAsync
                ? AsyncSegment.StateMachine(innerCurrent, innerParent)
                : AsyncSegment.Runtime(innerCurrent, innerParent);
            AsyncSegment outerSegment = outerKind == AsyncCallstackKind.StateMachineAsync
                ? AsyncSegment.StateMachine(outerCurrent, outerParent)
                : AsyncSegment.Runtime(outerCurrent, outerParent);

            var scenario = new StitchScenario
            {
                Sync = sync.ToArray(),
                AsyncSegments = new[] { outerSegment, innerSegment },
                ExpectedStitched = new[]
                {
                    innerWork,
                    innerCurrent,
                    innerParent,
                    unclassifiedSchedulingFrame,
                    nativeBridgeFrame,
                    synchronousBridgeFrame,
                    outerCurrent,
                    outerParent,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                FilteredOut = new[] { unclassifiedSchedulingFrame },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 4,
                    V1InlineFallbackUsed =
                        (innerKind == AsyncCallstackKind.StateMachineAsync ? 2 : 0) +
                        (outerKind == AsyncCallstackKind.StateMachineAsync ? 2 : 0),
                    V1InfrastructureFramesCollapsed =
                        (innerKind == AsyncCallstackKind.StateMachineAsync ? 2 : 0) +
                        (outerKind == AsyncCallstackKind.StateMachineAsync ? 2 : 0),
                    V2InfrastructureFramesCollapsed =
                        (innerKind == AsyncCallstackKind.RuntimeAsync ? 4 : 0) +
                        (outerKind == AsyncCallstackKind.RuntimeAsync ? 4 : 0),
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, (int)AsyncCallstackKind.RuntimeAsync, -1)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, (int)AsyncCallstackKind.RuntimeAsync, 0)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, (int)AsyncCallstackKind.RuntimeAsync, 1)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, (int)AsyncCallstackKind.RuntimeAsync, 2)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, (int)AsyncCallstackKind.StateMachineAsync, -1)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, (int)AsyncCallstackKind.StateMachineAsync, 0)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, (int)AsyncCallstackKind.StateMachineAsync, 1)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, (int)AsyncCallstackKind.StateMachineAsync, 2)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, (int)AsyncCallstackKind.RuntimeAsync, -1)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, (int)AsyncCallstackKind.RuntimeAsync, 0)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, (int)AsyncCallstackKind.RuntimeAsync, 1)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, (int)AsyncCallstackKind.RuntimeAsync, 2)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, (int)AsyncCallstackKind.StateMachineAsync, -1)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, (int)AsyncCallstackKind.StateMachineAsync, 0)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, (int)AsyncCallstackKind.StateMachineAsync, 1)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, (int)AsyncCallstackKind.StateMachineAsync, 2)]
        public void NestedAsync_CoreLibOnlyReentrantHandoff_RequiresResolvedMethods(
            int innerKindValue, int outerKindValue, int unresolvedFrame)
        {
            var innerKind = (AsyncCallstackKind)innerKindValue;
            var outerKind = (AsyncCallstackKind)outerKindValue;
            Frame innerWork = Frame.App("Scenario.InnerWork");
            Frame innerIdentity = Frame.App(innerKind == AsyncCallstackKind.RuntimeAsync
                ? "Scenario.InnerAsync" : "Scenario+<InnerAsync>d__1.MoveNext");
            Frame innerAncestor = Frame.App(innerKind == AsyncCallstackKind.RuntimeAsync
                ? "Scenario.InnerParentAsync" : "Scenario+<InnerParentAsync>d__2.MoveNext");
            Frame innerCurrent = innerKind == AsyncCallstackKind.RuntimeAsync
                ? innerIdentity : innerIdentity.Logical("Scenario.InnerAsync", -1);
            Frame innerParent = innerKind == AsyncCallstackKind.RuntimeAsync
                ? innerAncestor : innerAncestor.Logical("Scenario.InnerParentAsync", 5);
            Frame runContinuations = Frame.CoreLib(
                "System.Threading.Tasks.Task.RunContinuations", "Task.RunContinuations");
            Frame trySetResult = Frame.CoreLib(
                "System.Threading.Tasks.Task.TrySetResult", "Task.TrySetResult");
            Frame setResult = Frame.CoreLib(
                "System.Threading.Tasks.TaskCompletionSource`1.SetResult", "TaskCompletionSource.SetResult");
            Frame outerIdentity = Frame.App(outerKind == AsyncCallstackKind.RuntimeAsync
                ? "Scenario.OuterAsync" : "Scenario+<OuterAsync>d__3.MoveNext");
            Frame outerAncestor = Frame.App(outerKind == AsyncCallstackKind.RuntimeAsync
                ? "Scenario.OuterParentAsync" : "Scenario+<OuterParentAsync>d__4.MoveNext");
            Frame outerCurrent = outerKind == AsyncCallstackKind.RuntimeAsync
                ? outerIdentity : outerIdentity.Logical("Scenario.OuterAsync", -1);
            Frame outerParent = outerKind == AsyncCallstackKind.RuntimeAsync
                ? outerAncestor : outerAncestor.Logical("Scenario.OuterParentAsync", 5);
            Frame[] handoff = { runContinuations, trySetResult, setResult };
            Frame[] innerDispatch = innerKind == AsyncCallstackKind.RuntimeAsync
                ? new[] { Frame.V2Wrapper(0), Frame.V2InstrumentedDispatch, Frame.V2Dispatch }
                : new[] { Frame.V1MoveNextAsDispatcher };
            Frame[] outerDispatch = outerKind == AsyncCallstackKind.RuntimeAsync
                ? new[] { Frame.V2Wrapper(0), Frame.V2InstrumentedDispatch, Frame.V2Dispatch }
                : new[] { Frame.V1MoveNextAsDispatcher };

            var scenario = new StitchScenario
            {
                Sync = new[] { innerWork, innerIdentity }.Concat(innerDispatch).Concat(handoff)
                    .Concat(new[] { outerIdentity }).Concat(outerDispatch)
                    .Concat(new[] { Frame.ThreadPoolDispatch, Frame.WorkerThreadStart }).ToArray(),
                AsyncSegments = new[]
                {
                    new AsyncSegment(outerKind, new[] { outerIdentity, outerAncestor }, new[] { 0, 5 }),
                    new AsyncSegment(innerKind, new[] { innerIdentity, innerAncestor }, new[] { 0, 5 }),
                },
                OmitSymbolFor = unresolvedFrame < 0 ? null : handoff[unresolvedFrame],
                EnableFrameAnnotations = true,
                SkipNormalStackAssertion = true,
                VerifySampleWeights = true,
                ExpectedStitched = unresolvedFrame < 0 ? new[]
                {
                    innerWork,
                    innerCurrent,
                    innerParent,
                    outerCurrent,
                    outerParent,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                } : new[]
                {
                    innerWork,
                    innerCurrent,
                    innerParent,
                    runContinuations,
                    trySetResult,
                    setResult,
                    outerCurrent,
                    outerParent,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 4,
                    V2InfrastructureFramesCollapsed =
                        (innerKind == AsyncCallstackKind.RuntimeAsync ? 4 : 0) +
                        (outerKind == AsyncCallstackKind.RuntimeAsync ? 4 : 0),
                    V1InlineFallbackUsed =
                        (innerKind == AsyncCallstackKind.StateMachineAsync ? 2 : 0) +
                        (outerKind == AsyncCallstackKind.StateMachineAsync ? 2 : 0),
                    V2ReentrantHandoffFramesCollapsed = unresolvedFrame < 0 &&
                        innerKind == AsyncCallstackKind.RuntimeAsync &&
                        outerKind == AsyncCallstackKind.RuntimeAsync ? 6 : 0,
                    AsyncReentrantHandoffFramesCollapsed = unresolvedFrame < 0 ? 6 : 0,
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void NestedAsync_ReentrantHandoff_RequiresSameCapturedRuntimeOwnerAndSupport(
            bool nativeAlias, bool differentSupport)
        {
            Frame inner = Frame.App("Scenario.InnerAsync");
            Frame outer = Frame.App("Scenario.OuterAsync");
            Frame first = Frame.Library(@"C:\runtime-a\System.Private.CoreLib.dll",
                "System.Threading.Tasks.Task.RunContinuations", "RuntimeA.RunContinuations");
            Frame second = Frame.Library(nativeAlias
                ? @"C:\runtime-a\System.Private.CoreLib.ni.dll" : @"C:\runtime-b\System.Private.CoreLib.dll",
                "System.Threading.Tasks.Task.TrySetResult", "RuntimeB.TrySetResult");
            Frame[] bridge = { first, second };
            Frame[] root = { Frame.ThreadPoolDispatch, Frame.WorkerThreadStart };
            bool fold = nativeAlias && !differentSupport;
            var scenario = new StitchScenario
            {
                Sync = new[]
                {
                    inner, Frame.V2Wrapper(0), Frame.V2InstrumentedDispatch, Frame.V2Dispatch,
                    first, second, outer, Frame.V2Wrapper(0), Frame.V2InstrumentedDispatch, Frame.V2Dispatch,
                }.Concat(root).ToArray(),
                AsyncSegments = new[] { AsyncSegment.Runtime(outer), AsyncSegment.Runtime(inner) },
                ExpectedStitched = new[] { inner }.Concat(fold ? Array.Empty<Frame>() : bridge)
                    .Concat(new[] { outer }).Concat(root).ToArray(),
                ExpectedWithoutNormalization = new[] { inner }.Concat(bridge).Concat(new[] { outer }).Concat(root).ToArray(),
                EnableFrameAnnotations = true,
                VerifySampleWeights = true,
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 4,
                    V2InfrastructureFramesCollapsed = 8,
                    AsyncReentrantHandoffFramesCollapsed = fold ? 4 : 0,
                    V2ReentrantHandoffFramesCollapsed = fold ? 4 : 0,
                },
                ConfigureTraceLog = log =>
                {
                    if (nativeAlias)
                    {
                        TraceModuleFile managed = log.ModuleFiles.Single(module =>
                            string.Equals(module.FilePath, first.ModuleName, StringComparison.OrdinalIgnoreCase));
                        log.ModuleFiles.Single(module =>
                            string.Equals(module.FilePath, second.ModuleName, StringComparison.OrdinalIgnoreCase)).managedModule = managed;
                    }
                },
            };
            if (differentSupport)
            {
                scenario.RuntimeSupportForModule = module =>
                    string.Equals(module?.FilePath, second.ModuleName, StringComparison.OrdinalIgnoreCase)
                        ? AsyncProfilerRuntimeSupport.Common : AsyncProfilerRuntimeSupport.CoreClr11;
            }

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void NestedRuntimeAsync_ReentrantHandoff_TruncatedOuterBoundary_PreservesTheBridge()
        {
            Frame innerWork = Frame.App("Scenario.InnerWork");
            Frame innerCurrent = Frame.App("Scenario.InnerAsync");
            Frame innerParent = Frame.App("Scenario.InnerParentAsync");
            Frame outerCurrent = Frame.App("Scenario.OuterAsync");
            Frame outerParent = Frame.App("Scenario.OuterParentAsync");
            Frame runContinuations = Frame.CoreLib("System.Threading.Tasks.Task.RunContinuations", "Task.RunContinuations");
            Frame trySetResult = Frame.CoreLib("System.Threading.Tasks.Task.TrySetResult", "Task.TrySetResult");
            Frame setResult = Frame.CoreLib("System.Threading.Tasks.TaskCompletionSource`1.SetResult", "TaskCompletionSource.SetResult");
            var scenario = new StitchScenario
            {
                Sync = new[]
                {
                    innerWork, innerCurrent, Frame.V2Wrapper(0), Frame.V2InstrumentedDispatch, Frame.V2Dispatch,
                    runContinuations, trySetResult, setResult, outerCurrent,
                },
                AsyncSegments = new[]
                {
                    AsyncSegment.Runtime(outerCurrent, outerParent),
                    AsyncSegment.Runtime(innerCurrent, innerParent),
                },
                ExpectedStitched = new[]
                {
                    innerWork, innerCurrent, innerParent, runContinuations, trySetResult, setResult,
                    outerCurrent, outerParent,
                },
                VerifySampleWeights = true,
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 4,
                    BoundariesNotFound = 2,
                    V2InfrastructureFramesCollapsed = 4,
                },
            };
            scenario.AssertProductionStitch();
        }

        [Fact]
        public void MixedRuntimeAndStateMachineAsync_CompletionHistoryIsIndependentPerSegment()
        {
            Frame innerWork = Frame.App("Scenario.InnerSynchronousWork");
            Frame innerCompleted = Frame.App("Scenario+<InnerCompletedAsync>d__5.MoveNext");
            Frame innerCurrent = Frame.App("Scenario+<InnerCurrentAsync>d__4.MoveNext");
            Frame innerParent = Frame.App("Scenario+<InnerV1ParentAsync>d__3.MoveNext");
            Frame logicalInnerCompleted = innerCompleted.Logical("Scenario.InnerCompletedAsync");
            Frame logicalInnerCurrent = innerCurrent.Logical("Scenario.InnerCurrentAsync");
            Frame logicalInnerParent = innerParent.Logical("Scenario.InnerV1ParentAsync");
            Frame executionContextCallback = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncStateMachineBox.ExecutionContextCallback",
                "AsyncStateMachineBox.ExecutionContextCallback");
            Frame runInternal = Frame.CoreLib(
                "System.Threading.ExecutionContext.RunInternal", "ExecutionContext.RunInternal");
            Frame boxMoveNext = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncStateMachineBox.MoveNext(Thread,Flags)",
                "AsyncStateMachineBox.MoveNext(Thread,Flags)");
            Frame completedTransition = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncStateMachineBox.SetExistingTaskResult",
                "AsyncStateMachineBox.SetExistingTaskResult");
            Frame knownV1Infrastructure1 = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncProfilerAsyncStateMachineBox.InstrumentedMoveNext",
                "KnownV1BoxInfrastructure1");
            Frame knownV1Infrastructure2 = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncStateMachineBox.MoveNext(Thread)",
                "KnownV1BoxInfrastructure2");
            Frame unclassifiedSchedulingFrame = Frame.CoreLib(
                "TestOnly.UnclassifiedSchedulingFrame", "UnclassifiedSchedulingFrame");
            Frame nativeBridgeFrame = Frame.Library(
                "SyntheticNativeBridge", "SyntheticNativeBridge.NativeBridgeFrame", "NativeBridgeFrame");
            Frame synchronousBridgeFrame = Frame.App("Scenario.SynchronousBridgeFrame");
            Frame outerCompleted = Frame.App("Scenario.OuterCompletedAsync");
            Frame outerCurrent = Frame.App("Scenario.OuterCurrentAsync");
            Frame outerParent = Frame.App("Scenario.OuterV2ParentAsync");

            var scenario = new StitchScenario
            {
                Sync = new[]
                {
                    innerWork,
                    innerCurrent,
                    executionContextCallback,
                    runInternal,
                    boxMoveNext,
                    innerCompleted,
                    completedTransition,
                    Frame.V1MoveNextAsDispatcher,
                    knownV1Infrastructure1,
                    knownV1Infrastructure2,
                    unclassifiedSchedulingFrame,
                    nativeBridgeFrame,
                    synchronousBridgeFrame,
                    outerCurrent,
                    Frame.V2Wrapper(1),
                    Frame.V2InstrumentedDispatch,
                    Frame.V2Dispatch,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                AsyncSegments = new[]
                {
                    AsyncSegment.Runtime(outerCompleted, outerCurrent, outerParent),
                    AsyncSegment.StateMachine(logicalInnerCompleted, logicalInnerCurrent, logicalInnerParent),
                },
                ExpectedStitched = new[]
                {
                    innerWork,
                    logicalInnerCurrent,
                    logicalInnerParent,
                    unclassifiedSchedulingFrame,
                    nativeBridgeFrame,
                    synchronousBridgeFrame,
                    outerCurrent,
                    outerParent,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                FilteredOut = new[] { unclassifiedSchedulingFrame },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 4,
                    V1InlineFallbackUsed = 2,
                    V1InfrastructureFramesCollapsed = 4,
                    V2InfrastructureFramesCollapsed = 4,
                },
            };

            scenario.AssertProductionStitch();
        }

        #region private

        private static StitchScenario CreateV1SurvivingDispatcherRootScenario(int infrastructureLength)
        {
            Frame child = Frame.App("Scenario+<ChildAsync>d__1.MoveNext");
            Frame current = Frame.App("Scenario+<CurrentAsync>d__2.MoveNext");
            Frame parent = Frame.App("Scenario+<ParentAsync>d__3.MoveNext");
            const string box = "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncStateMachineBox`1";
            Frame carrier = Frame.CoreLib(
                box + "[System.Int64,Scenario+<CurrentAsync>d__2].MoveNext(class System.Threading.Thread)",
                "CurrentBox.MoveNext");
            Frame[] infrastructure =
            {
                Frame.CoreLib(box + "[System.Int64,Scenario+<ChildAsync>d__1].InstrumentedMoveNext(class System.Threading.Thread,value class Flags)",
                    "ChildBox.InstrumentedMoveNext"),
                Frame.CoreLib(box + "[System.Int64,Scenario+<ChildAsync>d__1].MoveNext(class System.Threading.Thread)",
                    "ChildBox.MoveNext(Thread)"),
                Frame.CoreLib(box + "[System.Int64,Scenario+<ChildAsync>d__1].MoveNext()",
                    "ChildBox.MoveNext()"),
            };
            return new StitchScenario
            {
                Kind = AsyncCallstackKind.StateMachineAsync,
                Sync = new[] { carrier, child, Frame.V1MoveNextAsDispatcher }.Concat(infrastructure.Take(infrastructureLength)).ToArray(),
                Async = new[] { child, current, parent },
                AsyncStates = new[] { 0, 0, 1 },
                ExpectedStitched = new[]
                {
                    current.Logical("Scenario.CurrentAsync", state: 0),
                    parent.Logical("Scenario.ParentAsync", state: 1),
                },
                EnableFrameAnnotations = true,
                OnlyManagedCodeStacks = true,
                SkipNormalStackAssertion = true,
                VerifySampleWeights = true,
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V1InlineFallbackUsed = 2,
                    V1InfrastructureFramesCollapsed = infrastructureLength * 2,
                },
            };
        }

        #endregion
    }
}
