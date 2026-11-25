// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Computers;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.EventPipe;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;
using Microsoft.Diagnostics.Tracing.Stacks;

using Xunit;

namespace TraceEventTests
{
    public partial class AsyncProfilerStitchIntegrationTests
    {
        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void StateMachineAsync_DispatcherCallee_PreservesCallerContext(bool enableInfrastructureCleanup, bool standalone)
        {
            Frame pollGC = Frame.CoreLib("System.Threading.Thread.PollGCWorker", "Thread.PollGCWorker");
            Frame counter = Frame.CoreLib("Interop+Kernel32.QueryPerformanceCounter", "QueryPerformanceCounter");
            Frame dispatcher = standalone
                ? Frame.CoreLib("System.Runtime.CompilerServices.AsyncStateMachineDispatcher.MoveNext()", "AsyncStateMachineDispatcher.MoveNext")
                : Frame.V1MoveNextAsDispatcher;
            Frame[] logical =
            {
                Frame.App("Scenario.Level3Async"),
                Frame.App("Scenario.Level2Async"),
                Frame.App("Program.Main"),
            };
            var scenario = new StitchScenario
            {
                Kind = AsyncCallstackKind.StateMachineAsync,
                Sync = new[] { pollGC, counter, dispatcher, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart },
                Async = logical,
                ExpectedStitched = new[] { pollGC, counter, enableInfrastructureCleanup ? dispatcher.DispatcherFrames() : dispatcher }
                    .Concat(logical).Concat(new[] { Frame.ThreadPoolDispatch, Frame.WorkerThreadStart }).ToArray(),
                EnableInfrastructureCleanup = enableInfrastructureCleanup,
                VerifySampleWeights = true,
                AssertTransformContext = context =>
                {
                    AsyncProfilerStitchedFrame target = Assert.Single(context.Frames.Where(frame =>
                        frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget));
                    Assert.DoesNotContain(context.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
                    AsyncProfilerStitchedFrame physicalDispatcher = context.Frames[2];
                    Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, physicalDispatcher.Origin);
                    Assert.Same(target.Context, physicalDispatcher.Context);
                    Assert.Equal(enableInfrastructureCleanup ? AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames :
                        AsyncProfilerStitchedFramePresentation.Native, physicalDispatcher.Presentation);
                },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V1InlineFallbackUsed = 2,
                    AsyncInfrastructureFramesCollapsed = enableInfrastructureCleanup ? 2 : 0,
                },
            };

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void StateMachineAsync_Scenario3Resume_RemovesSchedulingFrames()
        {
            // Captured from AsyncProfilerScenarios scenario 3 after AsyncFoo resumes from Task.Delay and blocks in
            // Console.ReadKey. The profiler segment contains only AsyncFoo, so there are no suspended async callers to add.
            Frame asyncFoo = Frame.App("TestCase3+<AsyncFoo>d__3.MoveNext");
            Frame logicalAsyncFoo = asyncFoo.Logical("TestCase3.AsyncFoo");
            Frame[] sync =
            {
                Frame.Library("System.Console", "Interop+Kernel32.ReadConsoleInput", "Interop.Kernel32.ReadConsoleInput"),
                Frame.Library("System.Console", "System.ConsolePal.ReadKey", "ConsolePal.ReadKey"),
                Frame.Library("System.Console", "System.Console.ReadKey", "Console.ReadKey"),
                asyncFoo,
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncStateMachineBox.ExecutionContextCallback",
                    "AsyncStateMachineBox.ExecutionContextCallback"),
                Frame.CoreLib("System.Threading.ExecutionContext.RunInternal", "ExecutionContext.RunInternal"),
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncStateMachineBox.MoveNext(Thread,Flags)",
                    "AsyncStateMachineBox.MoveNext(Thread,Flags)"),
                Frame.V1MoveNextAsDispatcher,
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncProfilerAsyncStateMachineBox.InstrumentedMoveNext",
                    "AsyncProfilerAsyncStateMachineBox.InstrumentedMoveNext"),
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncStateMachineBox.MoveNext(Thread)",
                    "AsyncStateMachineBox.MoveNext(Thread)"),
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncStateMachineBox.MoveNext()",
                    "AsyncStateMachineBox.MoveNext()"),
                Frame.CoreLib("System.Threading.Tasks.AwaitTaskContinuation.RunOrScheduleAction",
                    "AwaitTaskContinuation.RunOrScheduleAction"),
                Frame.CoreLib("System.Threading.Tasks.Task.RunContinuations", "Task.RunContinuations"),
                Frame.CoreLib("System.Threading.Tasks.Task.TrySetResult", "Task.TrySetResult"),
                Frame.CoreLib("System.Threading.Tasks.Task+DelayPromise.CompleteTimedOut", "DelayPromise.CompleteTimedOut"),
                Frame.CoreLib("System.Threading.TimerQueueTimer.Fire", "TimerQueueTimer.Fire"),
                Frame.ThreadPoolDispatch,
                Frame.CoreLib("System.Threading.PortableThreadPool+WorkerThread.WorkerDoWork", "WorkerThread.WorkerDoWork"),
                Frame.WorkerThreadStart,
                Frame.CoreLib("System.Threading.Thread.StartCallback", "Thread.StartCallback"),
            };

            var scenario = new StitchScenario
            {
                Kind = AsyncCallstackKind.StateMachineAsync,
                Sync = sync,
                Async = new[]
                {
                    logicalAsyncFoo,
                },
                ExpectedStitched = new[]
                {
                    sync[0],
                    sync[1],
                    sync[2],
                    logicalAsyncFoo,
                    sync[11],
                    sync[12],
                    sync[13],
                    sync[14],
                    sync[15],
                    sync[16],
                    sync[17],
                    sync[18],
                    sync[19],
                },
                FilteredOut = sync.Skip(11).Take(5).ToArray(),
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V1InlineFallbackUsed = 2,
                    V1InfrastructureFramesCollapsed = 6,
                },
            };

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void StateMachineAsync_SameMethodDifferentStates_RetainsDistinctFrameIdentity()
        {
            Frame moveNext = Frame.App("Scenario+<RepeatedAsync>d__1.MoveNext");
            Frame logicalState3 = moveNext.Logical("Scenario.RepeatedAsync", state: 3);
            Frame logicalRunning = moveNext.Logical("Scenario.RepeatedAsync", state: -1);
            Frame logicalStateMinus2 = moveNext.Logical("Scenario.RepeatedAsync", state: -2);
            var scenario = new StitchScenario
            {
                Kind = AsyncCallstackKind.StateMachineAsync,
                Sync = new[]
                {
                    moveNext,
                    Frame.V1MoveNextAsDispatcher,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                AsyncSegments = new[]
                {
                    new AsyncSegment(
                        AsyncCallstackKind.StateMachineAsync,
                        new[] { logicalState3, logicalStateMinus2 },
                        new[] { 3, -2 }),
                },
                EnableFrameAnnotations = true,
                SkipNormalStackAssertion = true,
                ExpectedStitched = new[]
                {
                    logicalRunning,
                    logicalStateMinus2,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V1InlineFallbackUsed = 2,
                },
                AssertStitchedStackSource = stackSource =>
                {
                    var asyncFrames = new List<(StackSourceFrameIndex Index, string Name, AsyncProfilerCallStackSourceFrameInfo Info)>();
                    stackSource.ForEach(sample =>
                    {
                        StackSourceCallStackIndex stackIndex = sample.StackIndex;
                        while (stackIndex != StackSourceCallStackIndex.Invalid)
                        {
                            StackSourceFrameIndex frameIndex = stackSource.GetFrameIndex(stackIndex);
                            if (stackSource.TryGetAsyncProfilerCallStackFrameInfo(frameIndex, out AsyncProfilerCallStackSourceFrameInfo info))
                            {
                                asyncFrames.Add((frameIndex, stackSource.GetFrameName(frameIndex, false), info));
                            }
                            stackIndex = stackSource.GetCallerIndex(stackIndex);
                        }
                    });

                    Assert.Equal(2, asyncFrames.Count);
                    Assert.NotEqual(asyncFrames[0].Index, asyncFrames[1].Index);
                    Assert.NotEqual(asyncFrames[0].Name, asyncFrames[1].Name);
                    Assert.All(asyncFrames, frame =>
                    {
                        Assert.StartsWith("[Async V1] ", frame.Name);
                        Assert.DoesNotContain("![Async V1] ", frame.Name);
                    });
                    Assert.Contains(
                        asyncFrames,
                        frame => frame.Info.State == 3 &&
                                 frame.Info.IsCurrent &&
                                 frame.Name.EndsWith("[ordinal 1, state -1]"));
                    Assert.Contains(
                        asyncFrames,
                        frame => frame.Info.State == -2 &&
                                 frame.Name.EndsWith("[ordinal 1, state -2]"));
                    Assert.Equal(AsyncCallstackKind.StateMachineAsync, asyncFrames[0].Info.Kind);
                    Assert.Equal(AsyncCallstackKind.StateMachineAsync, asyncFrames[1].Info.Kind);
                    Assert.Equal(new[] { -2, 3 }, asyncFrames.Select(frame => frame.Info.State).OrderBy(state => state));
                    Assert.Equal(asyncFrames[0].Info.MethodId, asyncFrames[1].Info.MethodId);
                    Assert.Equal(asyncFrames[0].Info.CodeAddress, asyncFrames[1].Info.CodeAddress);
                    Assert.Equal(
                        asyncFrames[0].Info.CodeAddress,
                        stackSource.GetFrameCodeAddress(asyncFrames[0].Index));
                    Assert.Equal(
                        asyncFrames[1].Info.CodeAddress,
                        stackSource.GetFrameCodeAddress(asyncFrames[1].Index));
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void StateMachineAsync_CurrentAnnotations_DoNotLeakIntoIdenticalCapturedAncestor(bool enableFrameAnnotations)
        {
            Frame method = Frame.App("Scenario+<RecursiveAsync>d__1.MoveNext");
            Frame captured = method.Logical("Scenario.RecursiveAsync", enableFrameAnnotations ? 1 : (int?)null);
            Frame running = method.Logical("Scenario.RecursiveAsync", enableFrameAnnotations ? -1 : (int?)null);
            var scenario = new StitchScenario
            {
                Kind = AsyncCallstackKind.StateMachineAsync,
                Sync = new[] { method, Frame.V1MoveNextAsDispatcher, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart },
                Async = new[] { captured, captured },
                AsyncStates = new[] { 1, 1 },
                EnableFrameAnnotations = enableFrameAnnotations,
                SkipNormalStackAssertion = enableFrameAnnotations,
                ExpectedStitched = new[] { running, captured, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation { SegmentsProcessed = 2, V1InlineFallbackUsed = 2 },
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
                    var current = Assert.Single(frames.Where(frame => frame.Info.IsCurrent));
                    var ancestor = Assert.Single(frames.Where(frame => !frame.Info.IsCurrent));
                    Assert.NotEqual(current.Index, ancestor.Index);
                    Assert.Equal(current.Info.CodeAddress, ancestor.Info.CodeAddress);
                    Assert.Equal(current.Info.MethodId, ancestor.Info.MethodId);
                    Assert.Equal(1, current.Info.State);
                    Assert.Equal(1, ancestor.Info.State);
                    Assert.False(current.Info.IsStateMachineBoxCarrier);
                    if (enableFrameAnnotations)
                    {
                        Assert.EndsWith("[ordinal 1, state -1]", current.Name);
                        Assert.EndsWith("[ordinal 1, state 1]", ancestor.Name);
                    }
                    else
                    {
                        Assert.Equal(current.Name, ancestor.Name);
                        Assert.DoesNotContain("state ", current.Name);
                    }
                },
            };
            scenario.AssertProductionStitch();
        }

        [Fact]
        public void StateMachineAsync_SameLogicalNameDifferentStateMachines_HaveDistinctDisplayNames()
        {
            Frame firstMoveNext = Frame.App("Scenario+<OverloadedAsync>d__1.MoveNext");
            Frame secondMoveNext = Frame.App("Scenario+<OverloadedAsync>d__2.MoveNext");
            Frame firstLogical = firstMoveNext.Logical("Scenario.OverloadedAsync");
            Frame secondLogical = secondMoveNext.Logical("Scenario.OverloadedAsync");
            var scenario = new StitchScenario
            {
                Kind = AsyncCallstackKind.StateMachineAsync,
                Sync = new[]
                {
                    firstMoveNext,
                    Frame.V1MoveNextAsDispatcher,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                AsyncSegments = new[]
                {
                    AsyncSegment.StateMachine(firstLogical, secondLogical),
                },
                ExpectedStitched = new[]
                {
                    firstLogical,
                    secondLogical,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V1InlineFallbackUsed = 2,
                },
                AssertStitchedStackSource = stackSource =>
                {
                    var names = new HashSet<string>();
                    stackSource.ForEach(sample =>
                    {
                        StackSourceCallStackIndex stackIndex = sample.StackIndex;
                        while (stackIndex != StackSourceCallStackIndex.Invalid)
                        {
                            StackSourceFrameIndex frameIndex = stackSource.GetFrameIndex(stackIndex);
                            if (stackSource.TryGetAsyncProfilerCallStackFrameInfo(frameIndex, out AsyncProfilerCallStackSourceFrameInfo info) &&
                                info.Kind == AsyncCallstackKind.StateMachineAsync)
                            {
                                names.Add(stackSource.GetFrameName(frameIndex, false));
                            }
                            stackIndex = stackSource.GetCallerIndex(stackIndex);
                        }
                    });

                    Assert.Contains(names, name => name.EndsWith("Scenario.OverloadedAsync [ordinal 1]"));
                    Assert.Contains(names, name => name.EndsWith("Scenario.OverloadedAsync [ordinal 2]"));
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, false, "none")]
        [InlineData(true, false, "none")]
        [InlineData(true, true, "none")]
        [InlineData(false, true, "none")]
        [InlineData(true, true, "pdb")]
        [InlineData(true, true, "foreign")]
        [InlineData(true, true, "unresolved")]
        [InlineData(true, true, "interrupted")]
        public void StateMachineAsync_BoxAllocation_GroupsConstructorsWithoutHidingCallees(
            bool enableNormalization, bool enableInfrastructureCleanup, string layout)
        {
            Frame[] native =
            {
                Frame.Library("clrjit", "Native.JitCompile", "JitFrame"),
                Frame.Library("coreclr", "Native.PrepareMethod", "RuntimeFrame"),
            };
            const string box = "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+";
            const string arguments = "`1[System.Int64,Scenario+<DeepLevel2Async>d__9]";
            string constructor = layout == "pdb" ? "::.ctor()" : "..ctor()";
            string profilerConstructor = box + "AsyncProfilerAsyncStateMachineBox" + arguments + constructor;
            Frame profiler = layout == "foreign"
                ? Frame.Library("OtherLibrary", profilerConstructor, "ProfilerBoxConstructor")
                : Frame.CoreLib(profilerConstructor, "ProfilerBoxConstructor");
            Frame finalizable = Frame.CoreLib(
                box + "DebugFinalizableAsyncStateMachineBox" + arguments + constructor, "FinalizableBoxConstructor");
            Frame callback = Frame.App("Scenario.AllocationCallback");
            Frame[] constructors = layout == "interrupted"
                ? new[] { profiler, callback, finalizable } : new[] { profiler, finalizable };
            Frame[] helpers =
            {
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].CreateDebugFinalizableAsyncStateMachineBox()", "CreateFinalizableBox"),
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].GetStateMachineBox(!!0&,class System.Threading.Tasks.Task`1<!0>&)", "GetBox"),
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].AwaitUnsafeOnCompleted(!!0&,!!1&,class System.Threading.Tasks.Task`1<!0>&)", "RegisterWithTask"),
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].AwaitUnsafeOnCompleted(!!0&,!!1&)", "RegisterAwait"),
            };
            Frame child = Frame.App("Scenario+<DeepLevel2Async>d__9.MoveNext");
            Frame[] startup =
            {
                child,
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start(!!0&)", "MethodBuilderCore.Start"),
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].Start(!!0&)", "MethodBuilder.Start"),
            };
            Frame kickoff = Frame.App("Scenario.DeepLevel2Async()");
            Frame parent = Frame.App("Scenario+<DeepLevel1Async>d__8.MoveNext");
            Frame current = parent.Logical("Scenario.DeepLevel1Async", -1);
            Frame yield = Frame.CoreLib(
                "System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter+<>c.<OutputCorrelationEtwEvent>b__6_0", "YieldCallback");
            Frame[] root = { yield, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart };
            Frame[] displayedRoot = enableInfrastructureCleanup ? root.Skip(1).ToArray() : root;
            Frame[] prefix = native.Concat(constructors).Concat(helpers).ToArray();
            Frame[] remainingStartup = enableNormalization ? Array.Empty<Frame>() : startup;
            Frame[] tail = remainingStartup.Concat(new[] { kickoff, current }).Concat(displayedRoot).ToArray();
            Frame[] grouped = layout == "interrupted"
                ? new[] { profiler.DispatcherFrames(), callback, finalizable.DispatcherFrames() }
                : layout == "foreign" || layout == "unresolved"
                    ? new[] { profiler, finalizable.DispatcherFrames() }
                    : new[] { profiler.DispatcherFrames() };
            if (!enableNormalization && enableInfrastructureCleanup)
            {
                tail = new[] { child, startup[1].DispatcherFrames(), kickoff, current }.Concat(displayedRoot).ToArray();
            }
            int recognizedHelpers = layout == "foreign" || layout == "unresolved" ? 5 : 6;
            var scenario = new StitchScenario
            {
                Kind = AsyncCallstackKind.StateMachineAsync,
                Sync = prefix.Concat(startup).Concat(new[] { kickoff, current, Frame.V1MoveNextAsDispatcher }).Concat(root).ToArray(),
                Async = new[] { parent },
                AsyncStates = new[] { 0 },
                OmitSymbolFor = layout == "unresolved" ? profiler : null,
                EnableFrameAnnotations = true,
                EnableNormalization = enableNormalization,
                EnableInfrastructureCleanup = enableInfrastructureCleanup,
                VerifySampleWeights = true,
                ExpectedStitched = enableInfrastructureCleanup ? native.Concat(grouped).Concat(tail).ToArray() : prefix.Concat(tail).ToArray(),
                ExpectedWithoutInfrastructureCleanup = prefix.Concat(remainingStartup).Concat(new[] { kickoff, current }).Concat(root).ToArray(),
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V1InlineFallbackUsed = 2,
                    V1SynchronousStartupFramesCollapsed = enableNormalization ? 6 : 0,
                    AsyncInfrastructureFramesCollapsed = enableInfrastructureCleanup
                        ? (recognizedHelpers + 1 + (enableNormalization ? 0 : 2)) * 2 : 0,
                    RuntimeResumptionBridgeFramesCollapsed = enableInfrastructureCleanup ? 2 : 0,
                },
                AssertTransformContext = context =>
                {
                    AsyncProfilerStitchedFrame active = Assert.Single(context.Frames.Where(frame =>
                        frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent));
                    Assert.False(active.IsStateMachineBoxCarrier);
                    Assert.DoesNotContain(context.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget);
                    AsyncProfilerStitchedFrame[] markers = context.Frames.Where(frame =>
                        frame.Presentation == AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames).ToArray();
                    Assert.Equal(enableInfrastructureCleanup
                        ? (layout == "interrupted" ? 2 : 1) + (enableNormalization ? 0 : 1) : 0, markers.Length);
                    Assert.All(markers, marker =>
                    {
                        Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, marker.Origin);
                        Assert.Same(active.Context, marker.Context);
                        Assert.Equal(MethodIndex.Invalid, marker.BoxedStateMachineMethod);
                    });
                    Assert.Equal(native.Select(frame => frame.SymbolName),
                        context.Frames.Take(native.Length).Select(frame =>
                            context.TraceLog.CodeAddresses[frame.CodeAddress].FullMethodName));
                },
            };

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void StateMachineAsync_ReentrantPatternInterruptedByUnknownFrame_IsPreserved()
        {
            Frame work = Frame.App("Scenario.ReentrantWork");
            Frame innerStateMachine = Frame.App("Scenario+<NestedDispatcherInnerAsync>d__15.MoveNext");
            Frame logicalInner = innerStateMachine.Logical("Scenario.NestedDispatcherInnerAsync");
            Frame innerKickoff = Frame.App("Scenario.NestedDispatcherInnerAsync");
            Frame outerStateMachine = Frame.App("Scenario+<NestedDispatcherOuterAsync>d__14.MoveNext");
            Frame logicalOuter = outerStateMachine.Logical("Scenario.NestedDispatcherOuterAsync");
            Frame infrastructure = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncProfilerAsyncStateMachineBox.InstrumentedMoveNext",
                "InstrumentedMoveNext");
            Frame awaitRegistration = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].AwaitUnsafeOnCompleted(!!0&,!!1&)",
                "AwaitUnsafeOnCompleted");
            Frame unknown = Frame.CoreLib("TestOnly.UnknownRegistrationBridge", "UnknownRegistrationBridge");
            Frame builderStart = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].Start(!!0&)",
                "AsyncTaskMethodBuilder.Start");

            var scenario = new StitchScenario
            {
                Kind = AsyncCallstackKind.StateMachineAsync,
                Sync = new[]
                {
                    work,
                    innerStateMachine,
                    Frame.V1MoveNextAsDispatcher,
                    infrastructure,
                    awaitRegistration,
                    unknown,
                    innerStateMachine,
                    builderStart,
                    innerKickoff,
                    outerStateMachine,
                    Frame.V1MoveNextAsDispatcher,
                    infrastructure,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                AsyncSegments = new[]
                {
                    AsyncSegment.StateMachine(logicalOuter),
                    AsyncSegment.StateMachine(logicalInner),
                },
                ExpectedStitched = new[]
                {
                    work,
                    logicalInner,
                    awaitRegistration,
                    unknown,
                    innerKickoff,
                    logicalOuter,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 4,
                    V1InlineFallbackUsed = 4,
                    V1InfrastructureFramesCollapsed = 4,
                    V1SynchronousStartupFramesCollapsed = 4,
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("common", false)]
        [InlineData("common", true)]
        [InlineData("reordered", false)]
        [InlineData("reordered", true)]
        [InlineData("extended", false)]
        [InlineData("extended", true)]
        public void StateMachineAsync_ReentrantDispatchEntry_DeduplicatesStartupWithoutClaimingBodyExecution(
            string layout, bool enableInfrastructureCleanup)
        {
            StitchScenario scenario = CreateReentrantDispatchEntryScenario();
            if (layout == "extended")
            {
                UseExtendedRuntimeLayouts(scenario);
                Frame helper = Frame.CoreLib(AsyncProfilerExtendedRuntimeLayoutSupport.HelperName, "LayoutHelper");
                Func<Frame[], Frame[]> extend = frames => frames.SelectMany(frame =>
                    frame.Label == "AwaitUnsafeOnCompleted(generic)" ? new[] { frame, helper } :
                    frame.Label == "AsyncMethodBuilderCore.Start" ? new[] { helper, frame } : new[] { frame }).ToArray();
                scenario.Sync = extend(scenario.Sync);
                scenario.ExpectedWithoutNormalization = extend(scenario.ExpectedWithoutNormalization);
                scenario.ExpectedDiagnostics.V1ReentrantFramesCollapsed += 4;
            }
            else if (layout == "reordered")
            {
                Array.Reverse(scenario.Sync, 3, 3);
                Array.Reverse(scenario.Sync, 7, 2);
                Array.Reverse(scenario.ExpectedWithoutNormalization, 3, 3);
                Array.Reverse(scenario.ExpectedWithoutNormalization, 7, 2);
            }
            if (enableInfrastructureCleanup)
            {
                scenario.EnableInfrastructureCleanup = true;
                scenario.ExpectedWithoutInfrastructureCleanup = scenario.ExpectedStitched;
                scenario.ExpectedStitched = scenario.ExpectedStitched.Skip(2).ToArray();
                scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 4;
                scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = 4;
            }
            scenario.AssertTransformContext = context =>
            {
                AsyncProfilerStitchedFrame target = Assert.Single(context.Frames.Where(frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget));
                Assert.Same(context.Segments[0], target.Context);
                Assert.Equal(0, target.Segment.FrameStateAt(target.SegmentFrameIndex));
                Assert.DoesNotContain(context.Frames, frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent &&
                    ReferenceEquals(frame.Context, target.Context));
                Assert.DoesNotContain(context.Frames, frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
                    frame.SyncFrameKind == AsyncProfilerStitchSyncFrameKind.V1StateMachineBody);
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void StateMachineAsync_ReentrantDispatchEntry_UsesCanonicalIdentityAcrossCodeVersions(
            bool capturedVersionIsCanonical)
        {
            StitchScenario scenario = CreateReentrantDispatchEntryScenario();
            scenario.EnableNormalization = false;
            scenario.ExpectedStitched = scenario.ExpectedWithoutNormalization;
            scenario.ExpectedDiagnostics.V1ReentrantFramesCollapsed = 0;
            CodeAddressIndex sampledAddress = CodeAddressIndex.Invalid;
            scenario.ConfigureTraceLog = log =>
            {
                ConfigureCapturedRuntime(log, "11.0.0");
                TraceEvent sample = log.Events.First(data => data.ProviderGuid == SampleProfilerTraceEventParser.ProviderGuid);
                AsyncProfilerCallStack inner = new AsyncProfilerCallStackComputer(log).GetCallStacks(sample)[0];
                TraceCodeAddresses addresses = log.CodeAddresses;
                TraceMethod method = addresses.Methods[addresses.MethodIndex(inner.Frames.CodeAddressAt(0))];

                // Add the sampled code version before stack-source frame index ranges are fixed.
                MethodIndex sampledMethod = addresses.Methods.NewMethod(
                    method.FullMethodName, method.MethodModuleFileIndex, method.MethodToken);
                var sampledInfo = new TraceCodeAddresses.CodeAddressInfo(
                    addresses[inner.Frames.CodeAddressAt(0)].Address + 0x100000, inner.ProcessIndex);
                sampledInfo.SetMethodIndex(addresses, sampledMethod);
                sampledAddress = (CodeAddressIndex)addresses.Count;
                addresses.codeAddresses.Add(sampledInfo);
            };
            scenario.AssertTransformContext = context =>
            {
                var frames = context.Frames.ToList();
                int startupIndex = frames.FindIndex(frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
                    frame.SyncFrameKind == AsyncProfilerStitchSyncFrameKind.V1StateMachineBody);
                AsyncProfilerStitchedFrame startup = frames[startupIndex];
                TraceCodeAddresses addresses = context.TraceLog.CodeAddresses;
                MethodIndex capturedMethod = addresses.MethodIndex(startup.CodeAddress);
                MethodIndex sampledMethod = addresses.MethodIndex(sampledAddress);
                Assert.NotEqual(capturedMethod, sampledMethod);

                MethodIndex canonical = capturedVersionIsCanonical ? capturedMethod : sampledMethod;
                frames[startupIndex] = AsyncProfilerStitchedFrame.CreateSync(
                    new AsyncProfilerStitchSyncFrame(sampledAddress, canonical, startup.SyncClassification),
                    startup.Context);
                var diagnostics = new AsyncProfilerCallStackDiagnostics { SegmentsProcessed = context.Segments.Count };
                new AsyncProfilerV1PresentationTransformPass().Apply(new AsyncProfilerCallStackTransformState(
                    context.TraceLog, context.Segments, context.TimestampQpc, context.ClassifyBoundary,
                    context.StitchingState.Copy(), diagnostics, frames, methodOf: address =>
                        address == startup.CodeAddress || address == sampledAddress ? canonical : context.GetMethod(address)));

                Assert.Equal(7, diagnostics.V1ReentrantFramesCollapsed);
                Assert.Single(frames.Where(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget));
                Assert.DoesNotContain(frames, frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.Sync && frame.CodeAddress == sampledAddress);
                Assert.DoesNotContain(frames, frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent &&
                    ReferenceEquals(frame.Context, context.Segments[0]));
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("different-context")]
        [InlineData("missing-dispatcher")]
        [InlineData("recursive-outer")]
        [InlineData("interrupted-registration")]
        [InlineData("duplicate-startup")]
        public void StateMachineAsync_ReentrantDispatchEntry_PreservesUnverifiedStartup(string obstruction)
        {
            StitchScenario scenario = CreateReentrantDispatchEntryScenario();
            scenario.EnableNormalization = false;
            scenario.ExpectedStitched = scenario.ExpectedWithoutNormalization;
            scenario.ExpectedDiagnostics.V1ReentrantFramesCollapsed = 0;
            scenario.AssertTransformContext = context =>
            {
                var frames = context.Frames.ToList();
                AsyncProfilerStitchedFrame startup = frames[6];
                AsyncProfilerStitchedFrame outer = frames[10];
                if (obstruction == "different-context")
                {
                    AsyncProfilerStitchedFrame registration = frames[3];
                    frames[3] = AsyncProfilerStitchedFrame.CreateSync(
                        new AsyncProfilerStitchSyncFrame(registration.CodeAddress, registration.Method, registration.SyncClassification),
                        frames[2].Context);
                }
                else if (obstruction == "missing-dispatcher")
                {
                    frames.RemoveAt(1);
                }
                else if (obstruction == "recursive-outer")
                {
                    frames[10] = AsyncProfilerStitchedFrame.CreateAsyncCurrent(
                        startup.CodeAddress, startup.Method, outer.Context, 0);
                }
                else if (obstruction == "interrupted-registration")
                {
                    frames.Insert(4, AsyncProfilerStitchedFrame.CreateSync(
                        new AsyncProfilerStitchSyncFrame(frames[3].CodeAddress, frames[3].Method), outer.Context));
                }
                else
                {
                    frames.InsertRange(10, frames.Skip(6).Take(4).ToArray());
                }
                var diagnostics = new AsyncProfilerCallStackDiagnostics { SegmentsProcessed = context.Segments.Count };
                new AsyncProfilerCallStackTransformPipeline().ApplyInPlace(new AsyncProfilerCallStackTransformState(
                    context.TraceLog, context.Segments, context.TimestampQpc, context.ClassifyBoundary,
                    context.StitchingState.Copy(), diagnostics, frames));

                Assert.Equal(0, diagnostics.V1ReentrantFramesCollapsed);
                Assert.Single(frames.Where(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget));
                Assert.Contains(frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
                    frame.CodeAddress == context.Frames[9].CodeAddress);
            };

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void StateMachineAsync_DeepResume_StitchesSuspendedCallersAndRemovesSchedulingFrames()
        {
            Frame readConsoleInput = Frame.Library("System.Console", "Interop+Kernel32.ReadConsoleInput", "Interop.Kernel32.ReadConsoleInput");
            Frame consolePalReadKey = Frame.Library("System.Console", "System.ConsolePal.ReadKey", "ConsolePal.ReadKey");
            Frame consoleReadKey = Frame.Library("System.Console", "System.Console.ReadKey", "Console.ReadKey");
            Frame level4 = Frame.App("TestCase8+<Level4Async>d__6.MoveNext");
            Frame level3 = Frame.App("TestCase8+<Level3Async>d__5.MoveNext");
            Frame level2 = Frame.App("TestCase8+<Level2Async>d__4.MoveNext");
            Frame level1 = Frame.App("TestCase8+<Level1Async>d__3.MoveNext");
            Frame run = Frame.App("TestCase8+<Run>d__2.MoveNext");
            Frame main = Frame.App("Program+<Main>d__0.MoveNext");
            Frame logicalLevel4 = level4.Logical("TestCase8.Level4Async");
            Frame logicalLevel3 = level3.Logical("TestCase8.Level3Async");
            Frame logicalLevel2 = level2.Logical("TestCase8.Level2Async");
            Frame logicalLevel1 = level1.Logical("TestCase8.Level1Async");
            Frame logicalRun = run.Logical("TestCase8.Run");
            Frame logicalMain = main.Logical("Program.Main");
            Frame executionContextCallback = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncStateMachineBox.ExecutionContextCallback",
                "AsyncStateMachineBox.ExecutionContextCallback");
            Frame runFromThreadPool = Frame.CoreLib(
                "System.Threading.ExecutionContext.RunFromThreadPoolDispatchLoop",
                "ExecutionContext.RunFromThreadPoolDispatchLoop");
            Frame boxMoveNextThreadFlags = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncStateMachineBox.MoveNext(Thread,Flags)",
                "AsyncStateMachineBox.MoveNext(Thread,Flags)");
            Frame instrumentedMoveNext = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncProfilerAsyncStateMachineBox.InstrumentedMoveNext",
                "AsyncProfilerAsyncStateMachineBox.InstrumentedMoveNext");
            Frame boxMoveNextThread = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncStateMachineBox.MoveNext(Thread)",
                "AsyncStateMachineBox.MoveNext(Thread)");
            Frame executeDirectly = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncStateMachineBox.ExecuteDirectly",
                "AsyncStateMachineBox.ExecuteDirectly");
            Frame workerDoWork = Frame.CoreLib(
                "System.Threading.PortableThreadPool+WorkerThread.WorkerDoWork", "WorkerThread.WorkerDoWork");
            Frame threadStartCallback = Frame.CoreLib("System.Threading.Thread.StartCallback", "Thread.StartCallback");
            var scenario = new StitchScenario
            {
                Kind = AsyncCallstackKind.StateMachineAsync,
                Sync = new[]
                {
                    readConsoleInput,
                    consolePalReadKey,
                    consoleReadKey,
                    level4,
                    executionContextCallback,
                    runFromThreadPool,
                    boxMoveNextThreadFlags,
                    Frame.V1MoveNextAsDispatcher,
                    instrumentedMoveNext,
                    boxMoveNextThread,
                    executeDirectly,
                    Frame.ThreadPoolDispatch,
                    workerDoWork,
                    Frame.WorkerThreadStart,
                    threadStartCallback,
                },
                Async = new[]
                {
                    logicalLevel4,
                    logicalLevel3,
                    logicalLevel2,
                    logicalLevel1,
                    logicalRun,
                    logicalMain,
                },
                AsyncStates = new[] { 0, 0, 0, 0, 0, 10 },
                ExpectedStitched = new[]
                {
                    readConsoleInput,
                    consolePalReadKey,
                    consoleReadKey,
                    logicalLevel4,
                    logicalLevel3,
                    logicalLevel2,
                    logicalLevel1,
                    logicalRun,
                    logicalMain,
                    Frame.ThreadPoolDispatch,
                    workerDoWork,
                    Frame.WorkerThreadStart,
                    threadStartCallback,
                },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V1InlineFallbackUsed = 2,
                    V1InfrastructureFramesCollapsed = 6,
                },
            };

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void StateMachineAsync_InlineCompletion_ExcludesCompletedFrames()
        {
            Frame readConsoleInput = Frame.Library("System.Console", "Interop+Kernel32.ReadConsoleInput", "Interop.Kernel32.ReadConsoleInput");
            Frame consolePalReadKey = Frame.Library("System.Console", "System.ConsolePal.ReadKey", "ConsolePal.ReadKey");
            Frame consoleReadKey = Frame.Library("System.Console", "System.Console.ReadKey", "Console.ReadKey");
            Frame level4 = Frame.App("TestCase8+<Level4Async>d__6.MoveNext");
            Frame level3 = Frame.App("TestCase8+<Level3Async>d__5.MoveNext");
            Frame level2 = Frame.App("TestCase8+<Level2Async>d__4.MoveNext");
            Frame level1 = Frame.App("TestCase8+<Level1Async>d__3.MoveNext");
            Frame run = Frame.App("TestCase8+<Run>d__2.MoveNext");
            Frame main = Frame.App("Program+<Main>d__0.MoveNext");
            Frame logicalLevel4 = level4.Logical("TestCase8.Level4Async");
            Frame logicalLevel3 = level3.Logical("TestCase8.Level3Async");
            Frame logicalLevel2 = level2.Logical("TestCase8.Level2Async");
            Frame logicalLevel1 = level1.Logical("TestCase8.Level1Async");
            Frame logicalRun = run.Logical("TestCase8.Run");
            Frame logicalMain = main.Logical("Program.Main");
            Frame workerDoWork = Frame.CoreLib(
                "System.Threading.PortableThreadPool+WorkerThread.WorkerDoWork", "WorkerThread.WorkerDoWork");
            Frame threadStartCallback = Frame.CoreLib("System.Threading.Thread.StartCallback", "Thread.StartCallback");
            Frame profilerInstrumentedMoveNext = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncProfilerAsyncStateMachineBox.ProfilerInstrumentedMoveNext",
                "AsyncProfilerAsyncStateMachineBox.ProfilerInstrumentedMoveNext");
            Frame boxMoveNextThread = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncStateMachineBox.MoveNext(Thread)",
                "AsyncStateMachineBox.MoveNext(Thread)");
            Frame executeDirectly = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncStateMachineBox.ExecuteDirectly",
                "AsyncStateMachineBox.ExecuteDirectly");

            var scenario = new StitchScenario
            {
                Kind = AsyncCallstackKind.StateMachineAsync,
                Sync = new[]
                {
                    readConsoleInput,
                    consolePalReadKey,
                    consoleReadKey,
                    level1,
                    Frame.V1InlineInfrastructure("Level1", "ExecutionContextCallback"),
                    Frame.V1InlineInfrastructure("Level1", "ExecutionContext.RunInternal"),
                    Frame.V1InlineInfrastructure("Level1", "MoveNext(Thread,Flags)"),
                    Frame.V1InlineInfrastructure("Level1", "InstrumentedMoveNext"),
                    Frame.V1InlineInfrastructure("Level1", "ProfilerInstrumentedMoveNext"),
                    Frame.V1InlineInfrastructure("Level1", "MoveNext(Thread)"),
                    Frame.V1InlineInfrastructure("Level1", "MoveNext()"),
                    Frame.V1InlineInfrastructure("Level1", "AwaitTaskContinuation.RunOrScheduleAction"),
                    Frame.V1InlineInfrastructure("Level1", "Task.RunContinuations"),
                    Frame.V1InlineInfrastructure("Level1", "Task.TrySetResult"),
                    Frame.V1InlineInfrastructure("Level1", "SetExistingTaskResult"),
                    level2,
                    Frame.V1InlineInfrastructure("Level2", "ExecutionContextCallback"),
                    Frame.V1InlineInfrastructure("Level2", "ExecutionContext.RunInternal"),
                    Frame.V1InlineInfrastructure("Level2", "MoveNext(Thread,Flags)"),
                    Frame.V1InlineInfrastructure("Level2", "InstrumentedMoveNext"),
                    Frame.V1InlineInfrastructure("Level2", "ProfilerInstrumentedMoveNext"),
                    Frame.V1InlineInfrastructure("Level2", "MoveNext(Thread)"),
                    Frame.V1InlineInfrastructure("Level2", "MoveNext()"),
                    Frame.V1InlineInfrastructure("Level2", "AwaitTaskContinuation.RunOrScheduleAction"),
                    Frame.V1InlineInfrastructure("Level2", "Task.RunContinuations"),
                    Frame.V1InlineInfrastructure("Level2", "Task.TrySetResult"),
                    Frame.V1InlineInfrastructure("Level2", "SetExistingTaskResult"),
                    level3,
                    Frame.V1InlineInfrastructure("Level3", "ExecutionContextCallback"),
                    Frame.V1InlineInfrastructure("Level3", "ExecutionContext.RunInternal"),
                    Frame.V1InlineInfrastructure("Level3", "MoveNext(Thread,Flags)"),
                    Frame.V1InlineInfrastructure("Level3", "InstrumentedMoveNext"),
                    Frame.V1InlineInfrastructure("Level3", "ProfilerInstrumentedMoveNext"),
                    Frame.V1InlineInfrastructure("Level3", "MoveNext(Thread)"),
                    Frame.V1InlineInfrastructure("Level3", "MoveNext()"),
                    Frame.V1InlineInfrastructure("Level3", "AwaitTaskContinuation.RunOrScheduleAction"),
                    Frame.V1InlineInfrastructure("Level3", "Task.RunContinuations"),
                    Frame.V1InlineInfrastructure("Level3", "Task.TrySetResult"),
                    Frame.V1InlineInfrastructure("Level3", "SetExistingTaskResult"),
                    level4,
                    Frame.V1InlineInfrastructure("Level4", "ExecutionContextCallback"),
                    Frame.V1InlineInfrastructure("Level4", "ExecutionContext.RunFromThreadPoolDispatchLoop"),
                    Frame.V1InlineInfrastructure("Level4", "MoveNext(Thread,Flags)"),
                    Frame.V1MoveNextAsDispatcher,
                    profilerInstrumentedMoveNext,
                    boxMoveNextThread,
                    executeDirectly,
                    Frame.ThreadPoolDispatch,
                    workerDoWork,
                    Frame.WorkerThreadStart,
                    threadStartCallback,
                },
                Async = new[]
                {
                    logicalLevel4,
                    logicalLevel3,
                    logicalLevel2,
                    logicalLevel1,
                    logicalRun,
                    logicalMain,
                },
                AsyncStates = new[] { 0, 0, 0, 0, 0, 10 },
                ExpectedStitched = new[]
                {
                    readConsoleInput,
                    consolePalReadKey,
                    consoleReadKey,
                    logicalLevel1,
                    logicalRun,
                    logicalMain,
                    Frame.ThreadPoolDispatch,
                    workerDoWork,
                    Frame.WorkerThreadStart,
                    threadStartCallback,
                },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V1InlineFallbackUsed = 2,
                    V1InfrastructureFramesCollapsed = 6,
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("AsyncStateMachineBox", "MoveNext()", false, false, true, null)]
        [InlineData("AsyncStateMachineBox", "MoveNext(class System.Threading.Thread)", true, false, true, null)]
        [InlineData("AsyncStateMachineBox", "InstrumentedMoveNext(class System.Threading.Thread)", true, true, true, null)]
        [InlineData("AsyncProfilerAsyncStateMachineBox", "InstrumentedMoveNext(class System.Threading.Thread)", true, false, false, null)]
        [InlineData("ProfilerAsyncStateMachineBox", "MoveNext()", true, false, true, null)]
        [InlineData("DebugFinalizableAsyncStateMachineBox", "MoveNext(class System.Threading.Thread)", true, false, true, null)]
        [InlineData("AsyncStateMachineBox", "InstrumentedMoveNext(class System.Threading.Thread)", false, true, false, null)]
        [InlineData("AsyncStateMachineBox", "MoveNext()", false, false, true, "11.0.0")]
        [InlineData("AsyncStateMachineBox", "InstrumentedMoveNext(class System.Threading.Thread)", true, true, true, "11.0.0")]
        [InlineData("AsyncStateMachineBox", "InstrumentedMoveNext(class System.Threading.Thread)", false, true, false, "11.0.0")]
        [InlineData("AsyncProfilerAsyncStateMachineBox", "InstrumentedMoveNext(class System.Threading.Thread)", true, false, false, "11.0.0")]
        [InlineData("AsyncStateMachineBox", "InstrumentedMoveNext(class System.Threading.Thread)", true, true, true, "12.0.0")]
        [InlineData("AsyncStateMachineBox", "InstrumentedMoveNext(class System.Threading.Thread)", true, true, true, null, true)]
        [InlineData("AsyncStateMachineBox", "InstrumentedMoveNext(class System.Threading.Thread)", true, true, true, "11.0.0", true)]
        public void StateMachineAsync_LeafBoxCarrier_RepresentsTheResumedParentOnce(
            string boxType, string member, bool enableFrameAnnotations, bool enableInfrastructureCleanup, bool enableNormalization, string productVersion,
            bool perfMap = false)
        {
            string separator = perfMap ? "::" : ".";
            Frame child = Frame.App("Scenario+<BurnCpuAsync>d__63" + separator + "MoveNext");
            Frame parent = Frame.App("Scenario+<ParentAsync>d__12" + separator + "MoveNext");
            Frame ancestor = Frame.App("Scenario+<AncestorAsync>d__11" + separator + "MoveNext");
            Frame capturedParent = parent.Logical("Scenario.ParentAsync", enableFrameAnnotations ? 0 : (int?)null);
            Frame capturedAncestor = ancestor.Logical("Scenario.AncestorAsync", enableFrameAnnotations ? 1 : (int?)null);
            const string boxPrefix = "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+";
            string boxArguments = "`1[System.Int64,Scenario+<ParentAsync>d__12]" + separator;
            Frame leaf = Frame.CoreLib(boxPrefix + boxType + boxArguments + member, "ParentBoxLeaf");
            Frame profiler = Frame.CoreLib(
                boxPrefix + "AsyncProfilerAsyncStateMachineBox" + boxArguments + "InstrumentedMoveNext()", "ParentBoxOverride");
            Frame boxMoveNext = Frame.CoreLib(
                boxPrefix + "AsyncStateMachineBox" + boxArguments + "MoveNext()", "ParentBoxExecution");
            if (boxMoveNext.Key == leaf.Key)
            {
                boxMoveNext = leaf;
            }
            Frame runContinuations = Frame.CoreLib("System.Threading.Tasks.Task.RunContinuations()", "Task.RunContinuations");
            Frame trySetResult = Frame.CoreLib("System.Threading.Tasks.Task`1[System.Int64].TrySetResult(!0)", "Task.TrySetResult");
            Frame completion = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].SetExistingTaskResult(class System.Threading.Tasks.Task`1<!0>,!0)",
                "SetExistingTaskResult");
            var scenario = new StitchScenario
            {
                Kind = AsyncCallstackKind.StateMachineAsync,
                Sync = new[]
                {
                    leaf, profiler, boxMoveNext, runContinuations, trySetResult, completion, child,
                    Frame.V1MoveNextAsDispatcher, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart,
                },
                Async = new[] { child.Logical("Scenario.BurnCpuAsync"), capturedParent, capturedAncestor },
                AsyncStates = new[] { 0, 0, 1 },
                EnableFrameAnnotations = enableFrameAnnotations,
                EnableInfrastructureCleanup = enableInfrastructureCleanup,
                EnableNormalization = enableNormalization,
                VerifySampleWeights = true,
                ConfigureTraceLog = log => ConfigureCapturedRuntime(log, productVersion),
                ExpectedStitched = new[] { leaf.DispatcherFrames(), capturedParent, capturedAncestor, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation { SegmentsProcessed = 2, V1InlineFallbackUsed = 2 },
                AssertTransformContext = context =>
                {
                    AsyncProfilerStitchedFrame current = Assert.Single(context.Frames.Where(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget));
                    Assert.True(current.IsStateMachineBoxCarrier);
                    Assert.Equal(1, current.SegmentFrameIndex);
                    Assert.Equal(0, current.Segment.FrameStateAt(1));
                    Assert.Equal(leaf.SymbolName, context.TraceLog.CodeAddresses[current.CodeAddress].FullMethodName);
                    Assert.NotEqual(current.Method, current.BoxedStateMachineMethod);
                    AsyncProfilerStitchedFrame marker = Assert.Single(context.Frames.Where(frame =>
                        frame.Presentation == AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames));
                    Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, marker.Origin);
                    Assert.Equal(current.CodeAddress, marker.CodeAddress);
                    Assert.Same(current.Context, marker.Context);
                    Assert.Equal(marker, context.Frames[0]);
                    Assert.Equal(current, context.Frames[1]);
                    Assert.DoesNotContain(context.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
                },
                AssertStitchedStackSource = stackSource =>
                {
                    int currents = 0;
                    for (int i = 0; i < stackSource.CallFrameIndexLimit; i++)
                    {
                        var index = (StackSourceFrameIndex)i;
                        if (!stackSource.TryGetAsyncProfilerCallStackFrameInfo(index, out AsyncProfilerCallStackSourceFrameInfo info) || !info.IsStateMachineBoxCarrier)
                        {
                            continue;
                        }
                        currents++;
                        Assert.True(info.IsStateMachineBoxCarrier);
                        Assert.True(info.IsDispatchTarget);
                        Assert.False(info.IsCurrent);
                        Assert.Equal(0, info.State);
                        Assert.Equal(leaf.SymbolName, stackSource.TraceLog.CodeAddresses[info.CodeAddress].FullMethodName);
                        Assert.Equal(info.CodeAddress, stackSource.GetFrameCodeAddress(index));
                        string name = stackSource.GetFrameName(index, false);
                        Assert.Contains("testapp!Scenario.ParentAsync", name);
                        Assert.DoesNotContain("AsyncStateMachineBox", name);
                        if (enableFrameAnnotations)
                        {
                            Assert.StartsWith("[Async V1] ", name);
                            Assert.EndsWith("[ordinal 12, state 0]", name);
                        }
                        else
                        {
                            Assert.DoesNotContain("state ", name);
                        }
                    }
                    Assert.Equal(1, currents);
                },
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, false, false, 0)]
        [InlineData(true, false, false, 0)]
        [InlineData(true, true, false, 0)]
        [InlineData(false, false, false, 5)]
        [InlineData(true, true, true, 0)]
        [InlineData(false, true, false, 0)]
        [InlineData(false, true, false, 5)]
        public void StateMachineAsync_BoxCarrierWithEventSourceCallees_MarksMachineryBeforeThePendingTarget(
            bool enableNormalization, bool enableInfrastructureCleanup, bool profilerEntry, int capturedState)
        {
            Frame body = Frame.App("Scenario+<DeepLevel1Async>d__8.MoveNext");
            Frame target = body.Logical("Scenario.DeepLevel1Async", capturedState);
            Frame[] callees =
            {
                Frame.CoreLib("System.Collections.Generic.Dictionary`2[System.Int32,System.Diagnostics.Tracing.EventSource+EventMetadata].FindValue(System.Int32)", "FindValue"),
                Frame.CoreLib("System.Diagnostics.Tracing.EventSource.WriteEventWithRelatedActivityIdCore", "WriteEventWithRelatedActivityIdCore"),
                Frame.CoreLib("System.Diagnostics.Tracing.EventSource.WriteEvent(System.Int32,System.Int32,System.Int32)", "WriteEventPayload"),
            };
            Frame resume = Frame.CoreLib("System.Runtime.CompilerServices.AsyncProfiler+ResumeAsyncContext.Resume", "Resume");
            Frame carrier = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncStateMachineBox`1[System.Int64,Scenario+<DeepLevel1Async>d__8].MoveNext(class System.Threading.Thread,value class Flags)",
                "PendingBox");
            Frame yield = Frame.CoreLib(
                "System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter+<>c.<OutputCorrelationEtwEvent>b__6_0",
                "YieldCallback");
            Frame[] tail = { yield, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart };
            Frame[] expected = callees.Concat(new[] { carrier.DispatcherFrames(), target }).Concat(tail).ToArray();
            var scenario = new StitchScenario
            {
                Kind = AsyncCallstackKind.StateMachineAsync,
                Sync = callees.Concat(profilerEntry ? new[] { resume } : Array.Empty<Frame>())
                    .Concat(new[] { carrier, Frame.V1MoveNextAsDispatcher }).Concat(tail).ToArray(),
                Async = new[] { body },
                AsyncStates = new[] { capturedState },
                EnableFrameAnnotations = true,
                EnableNormalization = enableNormalization,
                EnableInfrastructureCleanup = enableInfrastructureCleanup,
                IncludeEventSourceEvents = false,
                VerifyPublicBuilder = true,
                VerifyThreadTimeComputer = true,
                VerifySampleWeights = true,
                ExpectedStitched = enableInfrastructureCleanup ? expected.Where(frame => frame != yield).ToArray() : expected,
                ExpectedWithoutInfrastructureCleanup = callees
                    .Concat(profilerEntry ? new[] { resume } : Array.Empty<Frame>())
                    .Concat(expected.Skip(callees.Length)).ToArray(),
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V1InlineFallbackUsed = 2,
                    AsyncInfrastructureFramesCollapsed = (profilerEntry ? 4 : 0) + (enableInfrastructureCleanup ? 2 : 0),
                    RuntimeResumptionBridgeFramesCollapsed = enableInfrastructureCleanup ? 2 : 0,
                    ProfilerEnvelopeFramesCollapsed = profilerEntry ? 4 : 0,
                },
                AssertTransformContext = context =>
                {
                    AsyncProfilerStitchedFrame pending = Assert.Single(context.Frames.Where(frame => frame.IsStateMachineBoxCarrier));
                    Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget, pending.Origin);
                    Assert.Equal(capturedState, pending.Segment.FrameStateAt(pending.SegmentFrameIndex));
                    Assert.Equal(callees.Length + 1, context.Frames.IndexOf(pending));
                    AsyncProfilerStitchedFrame marker = Assert.Single(context.Frames.Where(frame =>
                        frame.Presentation == AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames));
                    Assert.Equal(marker, context.Frames[callees.Length]);
                    Assert.Same(pending.Context, marker.Context);
                    Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, marker.Origin);
                    Assert.Equal(profilerEntry ? resume.SymbolName : carrier.SymbolName,
                        context.TraceLog.CodeAddresses[marker.CodeAddress].FullMethodName);
                    Assert.DoesNotContain(context.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, false, 0)]
        [InlineData(false, true, 5)]
        [InlineData(true, false, 0)]
        [InlineData(true, true, 0)]
        [InlineData(true, false, 5)]
        public void StateMachineAsync_BoxCarrierWithInitialization_PreservesCapturedStateAndSynchronousCallees(
            bool truncated, bool enableInfrastructureCleanup, int capturedState)
        {
            Frame child = Frame.App("Scenario+<Level24Async>d__42.MoveNext");
            Frame completing = Frame.App("Scenario+<Level23Async>d__41.MoveNext");
            Frame parent = Frame.App("Scenario+<Level22Async>d__40.MoveNext");
            Frame ancestor = Frame.App("Scenario+<Level21Async>d__39.MoveNext");
            Frame resuming = parent.Logical("Scenario.Level22Async", capturedState);
            Frame suspended = ancestor.Logical("Scenario.Level21Async", 1);
            Frame unknown = Frame.Library("NativeInitializer", "NativeInitializer.Run", "UnknownInitializer");
            Frame[] prefix =
            {
                Frame.CoreLib("System.Runtime.CompilerServices.InitHelpers.CallClassConstructor", "CallClassConstructor"),
                unknown,
                Frame.CoreLib("System.Runtime.CompilerServices.InitHelpers.InitClassSlow", "InitClassSlow"),
                Frame.CoreLib("System.Runtime.CompilerServices.StaticsHelpers.GetGCStaticBaseSlow", "GetGCStaticBaseSlow"),
            };
            const string box = "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+";
            const string arguments = "`1[System.Int64,Scenario+<Level22Async>d__40].";
            Frame carrier = Frame.CoreLib(box + "AsyncStateMachineBox" + arguments +
                "MoveNext(class System.Threading.Thread,value class Flags)", "ResumingParentBox");
            Frame[] transition =
            {
                carrier,
                Frame.CoreLib(box + "AsyncStateMachineBox" + arguments +
                    "InstrumentedMoveNext(class System.Threading.Thread,value class Flags)", "ParentBaseInstrumentation"),
                Frame.CoreLib(box + "AsyncProfilerAsyncStateMachineBox" + arguments +
                    "InstrumentedMoveNext(class System.Threading.Thread,value class Flags)", "ParentProfilerInstrumentation"),
                Frame.CoreLib(box + "AsyncStateMachineBox" + arguments + "MoveNext()", "ParentBoxWrapper"),
                Frame.CoreLib("System.Threading.Tasks.Task.RunContinuations", "Task.RunContinuations"),
                Frame.CoreLib("System.Threading.Tasks.Task`1[System.Int64].TrySetResult(!0)", "Task.TrySetResult"),
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].SetExistingTaskResult", "SetExistingTaskResult"),
                completing,
                child,
            };
            Frame[] root = truncated ? Array.Empty<Frame>() : new[]
            {
                Frame.ThreadPoolDispatch, Frame.WorkerThreadStart,
            };
            Frame[] expected = prefix.Concat(new[] { carrier.DispatcherFrames(), resuming, suspended }).Concat(root).ToArray();
            var scenario = new StitchScenario
            {
                Kind = AsyncCallstackKind.StateMachineAsync,
                Sync = prefix.Concat(transition)
                    .Concat(truncated ? Array.Empty<Frame>() : new[] { Frame.V1MoveNextAsDispatcher })
                    .Concat(root).ToArray(),
                Async = new[] { child, completing, parent, ancestor },
                AsyncStates = new[] { 0, 0, capturedState, 1 },
                OmitSymbolFor = unknown,
                EnableFrameAnnotations = true,
                EnableInfrastructureCleanup = enableInfrastructureCleanup,
                OnlyManagedCodeStacks = true,
                SkipNormalStackAssertion = true,
                VerifySampleWeights = true,
                ExpectedStitched = expected,
                ExpectedWithoutInfrastructureCleanup = expected,
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V1InlineFallbackUsed = 2,
                    BoundariesNotFound = truncated ? 2 : 0,
                    IncompleteAsyncRootsNormalized = truncated ? 2 : 0,
                },
                AssertTransformContext = context =>
                {
                    AsyncProfilerStitchedFrame target = Assert.Single(context.Frames.Where(frame => frame.IsStateMachineBoxCarrier));
                    Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget, target.Origin);
                    Assert.Equal(2, target.SegmentFrameIndex);
                    Assert.Equal(capturedState, target.Segment.FrameStateAt(2));
                    Assert.Equal(carrier.SymbolName, context.TraceLog.CodeAddresses[target.CodeAddress].FullMethodName);
                    Assert.Equal(prefix.Length + 1, context.Frames.IndexOf(target));
                    AsyncProfilerStitchedFrame marker = context.Frames[prefix.Length];
                    Assert.Equal(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames, marker.Presentation);
                    Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, marker.Origin);
                    Assert.Equal(target.CodeAddress, marker.CodeAddress);
                    Assert.Same(target.Context, marker.Context);
                    Assert.DoesNotContain(context.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
                    Assert.Equal(truncated, context.Frames.Any(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot));
                },
                AssertStitchedStackSource = stackSource =>
                {
                    var targets = new List<AsyncProfilerCallStackSourceFrameInfo>();
                    for (int i = 0; i < stackSource.CallFrameIndexLimit; i++)
                    {
                        var index = (StackSourceFrameIndex)i;
                        if (!stackSource.TryGetAsyncProfilerCallStackFrameInfo(index, out AsyncProfilerCallStackSourceFrameInfo info) ||
                            !info.IsStateMachineBoxCarrier)
                        {
                            continue;
                        }
                        targets.Add(info);
                        Assert.True(info.IsDispatchTarget);
                        Assert.False(info.IsCurrent);
                        Assert.Equal(capturedState, info.State);
                        Assert.Equal(carrier.SymbolName, stackSource.TraceLog.CodeAddresses[info.CodeAddress].FullMethodName);
                        Assert.Equal(info.CodeAddress, stackSource.GetFrameCodeAddress(index));
                        Assert.EndsWith("[ordinal 40, state " + capturedState + "]", stackSource.GetFrameName(index, false));
                    }
                    Assert.Single(targets);
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("foreign-module")]
        [InlineData("unknown-member")]
        [InlineData("unknown-type")]
        [InlineData("ambiguous-type")]
        [InlineData("unresolved-carrier")]
        [InlineData("no-segment")]
        public void StateMachineAsync_LeafBoxCarrier_LeavesUnverifiedCarriersPhysical(string guard)
        {
            Frame child = Frame.App("Scenario+<BurnCpuAsync>d__63.MoveNext");
            Frame parent = Frame.App("Scenario+<ParentAsync>d__12.MoveNext");
            Frame capturedParent = parent.Logical("Scenario.ParentAsync", 0);
            Frame runningChild = child.Logical("Scenario.BurnCpuAsync", -1);
            string boxedType = guard == "unknown-type" ? "OtherScenario+<ParentAsync>d__12" : "Scenario+<ParentAsync>d__12";
            string member = guard == "unknown-member" ? "UnknownMoveNext" : "InstrumentedMoveNext";
            Frame leaf = Frame.Library(
                guard == "foreign-module" ? "OtherLib" : AsyncProfilerRuntimeSupport.HostModuleName,
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncStateMachineBox`1[System.Int64," + boxedType + "]." + member + "()",
                "ParentBoxLeaf");
            Frame[] prefix = { leaf };
            Frame[] sync = prefix.Concat(
                new[] { child, Frame.V1MoveNextAsDispatcher, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart }).ToArray();
            var scenario = new StitchScenario
            {
                Kind = AsyncCallstackKind.StateMachineAsync,
                Sync = sync,
                Async = new[] { child, capturedParent },
                AsyncStates = new[] { 0, 0 },
                EnableFrameAnnotations = true,
                SkipNormalStackAssertion = guard != "no-segment",
                VerifySampleWeights = true,
                OmitSymbolFor = guard == "unresolved-carrier" ? leaf : null,
                ContextParent = guard == "ambiguous-type"
                    ? AsyncSegment.StateMachine(Frame.Library("OtherApp", parent.SymbolName, "Ambiguous.Parent"))
                    : null,
                AsyncStartQpc = guard == "no-segment" ? 100_030 : 100_010,
                ExpectedStitched = guard == "no-segment"
                    ? sync
                    : prefix.Concat(new[] { runningChild, capturedParent, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart }).ToArray(),
                ExpectedDiagnostics = guard == "no-segment"
                    ? new CallStackDiagnosticsExpectation()
                    : new CallStackDiagnosticsExpectation { SegmentsProcessed = 2, V1InlineFallbackUsed = 2 },
                AssertStitchedStackSource = stackSource =>
                {
                    for (int i = 0; i < stackSource.CallFrameIndexLimit; i++)
                    {
                        if (stackSource.TryGetAsyncProfilerCallStackFrameInfo((StackSourceFrameIndex)i, out AsyncProfilerCallStackSourceFrameInfo info))
                        {
                            Assert.False(info.IsStateMachineBoxCarrier);
                        }
                    }
                },
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, false, false, false)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, false, false, true)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, false, true, false)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, false, true, true)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, true, false, false)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, true, false, true)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, true, true, false)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, true, true, true)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, false, false, false)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, false, false, true)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, false, true, false)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, false, true, true)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, true, false, false)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, true, false, true)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, true, true, false)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, true, true, true)]
        public void StateMachineAsync_ReentrantDispatchTarget_RequiresVerifiedProgressOrBoxCarrier(
            int outerKindValue, bool carrier, bool completionObserved, bool enableNormalization)
        {
            var outerKind = (AsyncCallstackKind)outerKindValue;
            Frame completed = Frame.App("Scenario+<CompletedAsync>d__0.MoveNext");
            Frame target = Frame.App("Scenario+<InnerAsync>d__1.MoveNext");
            Frame ancestor = Frame.App("Scenario+<InnerParentAsync>d__2.MoveNext");
            Frame box = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncStateMachineBox`1[System.Int64,Scenario+<InnerAsync>d__1].MoveNext()",
                "InnerBox.MoveNext");
            Frame outerIdentity = Frame.App(outerKind == AsyncCallstackKind.RuntimeAsync
                ? "Scenario.OuterAsync" : "Scenario+<OuterAsync>d__3.MoveNext");
            Frame outerAncestor = Frame.App(outerKind == AsyncCallstackKind.RuntimeAsync
                ? "Scenario.OuterParentAsync" : "Scenario+<OuterParentAsync>d__4.MoveNext");
            Frame[] entry = carrier
                ? new[] { box, Frame.V1MoveNextAsDispatcher }
                : new[] { Frame.V1MoveNextAsDispatcher };
            Frame[] bridge =
            {
                Frame.CoreLib("System.Threading.Tasks.Task.RunContinuations", "Task.RunContinuations"),
                Frame.CoreLib("System.Threading.Tasks.Task.TrySetResult", "Task.TrySetResult"),
                Frame.CoreLib("System.Threading.Tasks.TaskCompletionSource`1.SetResult", "TaskCompletionSource.SetResult"),
            };
            Frame[] outerDispatch = outerKind == AsyncCallstackKind.RuntimeAsync
                ? new[] { Frame.V2Wrapper(0), Frame.V2InstrumentedDispatch, Frame.V2Dispatch }
                : new[] { Frame.V1MoveNextAsDispatcher };
            Frame[] root = { Frame.ThreadPoolDispatch, Frame.WorkerThreadStart };
            Frame[] innerFrames = completionObserved
                ? new[] { completed, target, ancestor } : new[] { target, ancestor };
            int[] innerStates = completionObserved ? new[] { 0, 5, 7 } : new[] { 5, 7 };
            bool fold = enableNormalization && (carrier || completionObserved);
            var expected = new List<Frame>();
            if (!carrier)
            {
                expected.Add(Frame.V1MoveNextAsDispatcher);
            }
            expected.Add(target.Logical("Scenario.InnerAsync", 5));
            expected.Add(ancestor.Logical("Scenario.InnerParentAsync", 7));
            if (!fold)
            {
                expected.AddRange(bridge);
            }
            expected.AddRange(outerKind == AsyncCallstackKind.RuntimeAsync
                ? new[] { outerIdentity, outerAncestor }
                : new[]
                {
                    outerIdentity.Logical("Scenario.OuterAsync", -1),
                    outerAncestor.Logical("Scenario.OuterParentAsync", 9),
                });
            expected.AddRange(root);
            var scenario = new StitchScenario
            {
                Sync = entry.Concat(bridge).Concat(new[] { outerIdentity }).Concat(outerDispatch).Concat(root).ToArray(),
                AsyncSegments = new[]
                {
                    new AsyncSegment(outerKind, new[] { outerIdentity, outerAncestor }, new[] { 0, 9 }),
                    new AsyncSegment(AsyncCallstackKind.StateMachineAsync, innerFrames, innerStates),
                },
                MethodCompletions = completionObserved ? 1 : 0,
                EnableFrameAnnotations = true,
                EnableNormalization = enableNormalization,
                SkipNormalStackAssertion = true,
                VerifySampleWeights = true,
                ExpectedStitched = expected.ToArray(),
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 4,
                    V1InlineFallbackUsed = completionObserved ? 0 :
                        (outerKind == AsyncCallstackKind.StateMachineAsync ? 4 : 2),
                    V2InfrastructureFramesCollapsed = outerKind == AsyncCallstackKind.RuntimeAsync ? 4 : 0,
                    AsyncReentrantHandoffFramesCollapsed = fold ? 6 : 0,
                },
                AssertTransformContext = context =>
                {
                    AsyncProfilerStitchedFrame dispatchTarget = Assert.Single(context.Frames.Where(frame =>
                        frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget));
                    Assert.Same(context.Segments[0], dispatchTarget.Context);
                    Assert.Equal(carrier, dispatchTarget.IsStateMachineBoxCarrier);
                    Assert.Equal(completionObserved ? 1 : 0, dispatchTarget.SegmentFrameIndex);
                    Assert.Equal(5, dispatchTarget.Segment.FrameStateAt(dispatchTarget.SegmentFrameIndex));
                    Assert.DoesNotContain(context.Frames, frame =>
                        frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent &&
                        ReferenceEquals(frame.Context, dispatchTarget.Context));
                    Assert.Equal(fold ? 3 : 0, context.Diagnostics.AsyncReentrantHandoffFramesCollapsed);
                },
                AssertStitchedStackSource = stackSource =>
                {
                    int targets = 0;
                    for (int i = 0; i < stackSource.CallFrameIndexLimit; i++)
                    {
                        if (stackSource.TryGetAsyncProfilerCallStackFrameInfo((StackSourceFrameIndex)i, out AsyncProfilerCallStackSourceFrameInfo info) &&
                            info.IsDispatchTarget && info.Kind == AsyncCallstackKind.StateMachineAsync)
                        {
                            targets++;
                            Assert.False(info.IsCurrent);
                            Assert.Equal(5, info.State);
                        }
                    }
                    Assert.Equal(1, targets);
                },
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync)]
        public void StateMachineAsync_CompletingInnerInvocation_PreservesReentrantBridge(int outerKindValue)
        {
            var outerKind = (AsyncCallstackKind)outerKindValue;
            Frame completed = Frame.App("Scenario+<CompletedAsync>d__0.MoveNext");
            Frame target = Frame.App("Scenario+<InnerAsync>d__1.MoveNext");
            Frame ancestor = Frame.App("Scenario+<InnerParentAsync>d__2.MoveNext");
            Frame outerIdentity = Frame.App(outerKind == AsyncCallstackKind.RuntimeAsync
                ? "Scenario.OuterAsync" : "Scenario+<OuterAsync>d__3.MoveNext");
            Frame[] bridge =
            {
                Frame.CoreLib("System.Threading.Tasks.Task.RunContinuations", "Task.RunContinuations"),
                Frame.CoreLib("System.Threading.Tasks.Task.TrySetResult", "Task.TrySetResult"),
                Frame.CoreLib("System.Threading.Tasks.TaskCompletionSource`1.SetResult", "TaskCompletionSource.SetResult"),
            };
            Frame[] outerDispatch = outerKind == AsyncCallstackKind.RuntimeAsync
                ? new[] { Frame.V2Wrapper(0), Frame.V2InstrumentedDispatch, Frame.V2Dispatch }
                : new[] { Frame.V1MoveNextAsDispatcher };
            Frame[] root = { Frame.ThreadPoolDispatch, Frame.WorkerThreadStart };
            var scenario = new StitchScenario
            {
                Sync = new[] { completed, Frame.V1MoveNextAsDispatcher }.Concat(bridge)
                    .Concat(new[] { outerIdentity }).Concat(outerDispatch).Concat(root).ToArray(),
                AsyncSegments = new[]
                {
                    new AsyncSegment(outerKind, new[] { outerIdentity }, new[] { 0 }),
                    new AsyncSegment(AsyncCallstackKind.StateMachineAsync,
                        new[] { completed, target, ancestor }, new[] { 0, 5, 7 }),
                },
                MethodCompletions = 1,
                EnableFrameAnnotations = true,
                SkipNormalStackAssertion = true,
                VerifySampleWeights = true,
                ExpectedStitched = new[]
                {
                    completed.Logical("Scenario.CompletedAsync", -2),
                    target.Logical("Scenario.InnerAsync", 5),
                    ancestor.Logical("Scenario.InnerParentAsync", 7),
                }.Concat(bridge).Concat(new[]
                {
                    outerKind == AsyncCallstackKind.RuntimeAsync
                        ? outerIdentity : outerIdentity.Logical("Scenario.OuterAsync", -1),
                }).Concat(root).ToArray(),
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 4,
                    V1CompletionTailUsed = 2,
                    V2InfrastructureFramesCollapsed = outerKind == AsyncCallstackKind.RuntimeAsync ? 4 : 0,
                },
                AssertTransformContext = context =>
                {
                    Assert.Single(context.Frames.Where(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody));
                    Assert.Single(context.Frames.Where(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget));
                    Assert.Equal(0, context.Diagnostics.AsyncReentrantHandoffFramesCollapsed);
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
        public void V1DispatchTarget_IsDistinctAndPreservesThePhysicalDispatcherByDefault(
            bool enableInfrastructureCleanup, bool enableFrameAnnotations, bool enableNormalization)
        {
            Frame current = Frame.App("Scenario+<BurnCpuAsync>d__63.MoveNext");
            Frame logicalCurrent = current.Logical("Scenario.BurnCpuAsync", enableFrameAnnotations ? 1 : (int?)null);
            Frame parent = Frame.App("Scenario+<ParentAsync>d__4.MoveNext")
                .Logical("Scenario.ParentAsync", enableFrameAnnotations ? 1 : (int?)null);
            var scenario = new StitchScenario
            {
                Kind = AsyncCallstackKind.StateMachineAsync,
                Sync = new[] { Frame.V1MoveNextAsDispatcher, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart },
                Async = new[] { logicalCurrent, parent },
                AsyncStates = new[] { 1, 1 },
                EnableInfrastructureCleanup = enableInfrastructureCleanup,
                EnableFrameAnnotations = enableFrameAnnotations,
                EnableNormalization = enableNormalization,
                VerifySampleWeights = true,
                ExpectedStitched = (enableInfrastructureCleanup ? Array.Empty<Frame>() : new[] { Frame.V1MoveNextAsDispatcher })
                    .Concat(new[] { logicalCurrent, parent, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart }).ToArray(),
                ExpectedWithoutInfrastructureCleanup =
                    new[] { Frame.V1MoveNextAsDispatcher, logicalCurrent, parent, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V1InlineFallbackUsed = 2,
                    AsyncInfrastructureFramesCollapsed = enableInfrastructureCleanup ? 2 : 0,
                },
                AssertStitchedStackSource = stackSource =>
                {
                    int targets = 0;
                    for (int i = 0; i < (int)stackSource.CallFrameIndexLimit; i++)
                    {
                        var frameIndex = (StackSourceFrameIndex)i;
                        if (!stackSource.TryGetAsyncProfilerCallStackFrameInfo(frameIndex, out AsyncProfilerCallStackSourceFrameInfo info) ||
                            !info.IsDispatchTarget)
                        {
                            continue;
                        }
                        targets++;
                        Assert.False(info.IsParentContext);
                        Assert.Equal(AsyncCallstackKind.StateMachineAsync, info.Kind);
                        Assert.Equal(1, info.State);
                        Assert.NotEqual(CodeAddressIndex.Invalid, info.CodeAddress);
                        Assert.Contains("BurnCpuAsync", stackSource.TraceLog.CodeAddresses[info.CodeAddress].FullMethodName);
                        string name = stackSource.GetFrameName(frameIndex, false);
                        Assert.StartsWith(enableFrameAnnotations ? "[Async V1] " : logicalCurrent.ModuleName.ToLowerInvariant() + "!", name);
                        Assert.DoesNotContain("dispatching", name);
                    }
                    Assert.Equal(1, targets);
                },
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void V1DispatchTarget_KeepsDistinctMetadataButSharesItsExportedName(bool enableFrameAnnotations)
        {
            Frame current = Frame.App("Scenario+<RecursiveAsync>d__1.MoveNext")
                .Logical("Scenario.RecursiveAsync", enableFrameAnnotations ? 1 : (int?)null);
            var scenario = new StitchScenario
            {
                Kind = AsyncCallstackKind.StateMachineAsync,
                Sync = new[] { Frame.V1MoveNextAsDispatcher, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart },
                Async = new[] { current, current },
                AsyncStates = new[] { 1, 1 },
                EnableFrameAnnotations = enableFrameAnnotations,
                ExpectedStitched =
                    new[] { Frame.V1MoveNextAsDispatcher, current, current, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation { SegmentsProcessed = 2, V1InlineFallbackUsed = 2 },
                AssertStitchedStackSource = stackSource =>
                {
                    var matches = new List<(StackSourceFrameIndex Index, string Name, AsyncProfilerCallStackSourceFrameInfo Info)>();
                    for (int i = 0; i < (int)stackSource.CallFrameIndexLimit; i++)
                    {
                        var frameIndex = (StackSourceFrameIndex)i;
                        if (stackSource.TryGetAsyncProfilerCallStackFrameInfo(frameIndex, out AsyncProfilerCallStackSourceFrameInfo info))
                        {
                            matches.Add((frameIndex, stackSource.GetFrameName(frameIndex, false), info));
                        }
                    }
                    Assert.Equal(2, matches.Count);
                    Assert.Single(matches.Where(frame => frame.Info.IsDispatchTarget));
                    Assert.Single(matches.Where(frame => !frame.Info.IsDispatchTarget));
                    Assert.NotEqual(matches[0].Index, matches[1].Index);
                    Assert.Equal(matches[0].Info.CodeAddress, matches[1].Info.CodeAddress);
                    Assert.Equal(matches[0].Info.MethodId, matches[1].Info.MethodId);
                    Assert.Equal(matches[0].Info.State, matches[1].Info.State);
                    Assert.Equal(matches[0].Name, matches[1].Name);
                    Assert.DoesNotContain("dispatching", matches[0].Name);

                    var exportedNames = new Dictionary<string, int>();
                    var exportedFrames = new Dictionary<int, StackSourceWriterHelper.FrameInfo>();
                    List<StackSourceWriterHelper.Sample> samples =
                        Assert.Single(StackSourceWriterHelper.GetSortedSamplesPerThread(stackSource)).Value;
                    IReadOnlyList<StackSourceWriterHelper.ProfileEvent> events =
                        StackSourceWriterHelper.GetProfileEvents(stackSource, samples, exportedNames, exportedFrames);
                    int exportedId = exportedNames[matches[0].Name];
                    Assert.Equal(2, events.Count(profileEvent =>
                        profileEvent.FrameId == exportedId &&
                        profileEvent.Type == StackSourceWriterHelper.ProfileEventType.Open));
                },
            };
            scenario.AssertProductionStitch();
        }

        [Fact]
        public void V1DispatchTarget_PhysicallyMatchedCurrent_IsNotADispatchTarget()
        {
            StitchScenario scenario = CreateRuntimeResumptionBridgeScenario(AsyncCallstackKind.StateMachineAsync);
            scenario.AssertStitchedStackSource = stackSource =>
            {
                for (int i = 0; i < stackSource.CallFrameIndexLimit; i++)
                {
                    if (stackSource.TryGetAsyncProfilerCallStackFrameInfo((StackSourceFrameIndex)i, out AsyncProfilerCallStackSourceFrameInfo info))
                    {
                        Assert.False(info.IsDispatchTarget);
                    }
                }
            };

            scenario.AssertProductionStitch();
        }

        #region private

        private static StitchScenario CreateReentrantDispatchEntryScenario()
        {
            Frame inner = Frame.App("Scenario+<NestedDispatcherInnerAsync>d__15.MoveNext");
            Frame logicalInner = inner.Logical("Scenario.NestedDispatcherInnerAsync");
            Frame outer = Frame.App("Scenario+<NestedDispatcherOuterAsync>d__14.MoveNext");
            Frame logicalOuter = outer.Logical("Scenario.NestedDispatcherOuterAsync");
            Frame kickoff = Frame.App("Scenario.NestedDispatcherInnerAsync");
            Frame resume = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncProfiler+ResumeAsyncContext.Resume",
                "Profiler.Resume");
            Frame infrastructure = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncProfilerAsyncStateMachineBox.InstrumentedMoveNext",
                "InstrumentedMoveNext");
            Frame[] registration =
            {
                Frame.CoreLib(
                    "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].AwaitUnsafeOnCompleted(!!0&,class System.Runtime.CompilerServices.IAsyncStateMachineBox)",
                    "AwaitUnsafeOnCompleted(box)"),
                Frame.CoreLib(
                    "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].AwaitUnsafeOnCompleted(!!0&,!!1&,class System.Threading.Tasks.Task`1<!0>&)",
                    "AwaitUnsafeOnCompleted(task)"),
                Frame.CoreLib(
                    "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].AwaitUnsafeOnCompleted(!!0&,!!1&)",
                    "AwaitUnsafeOnCompleted(generic)"),
            };
            Frame[] startup =
            {
                inner,
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start(!!0&)",
                    "AsyncMethodBuilderCore.Start"),
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].Start(!!0&)",
                    "AsyncTaskMethodBuilder.Start"),
                kickoff,
            };
            Frame[] root = { Frame.ThreadPoolDispatch, Frame.WorkerThreadStart };
            return new StitchScenario
            {
                Sync = new[] { resume, Frame.V1MoveNextAsDispatcher, infrastructure }
                    .Concat(registration).Concat(startup)
                    .Concat(new[] { outer, Frame.V1MoveNextAsDispatcher, infrastructure }).Concat(root).ToArray(),
                AsyncSegments = new[]
                {
                    AsyncSegment.StateMachine(logicalOuter),
                    AsyncSegment.StateMachine(logicalInner),
                },
                ExpectedStitched = new[] { resume, Frame.V1MoveNextAsDispatcher, logicalInner, logicalOuter }
                    .Concat(root).ToArray(),
                ExpectedWithoutNormalization = new[] { resume, Frame.V1MoveNextAsDispatcher, logicalInner }
                    .Concat(registration).Concat(startup).Concat(new[] { logicalOuter }).Concat(root).ToArray(),
                VerifySampleWeights = true,
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 4,
                    V1InlineFallbackUsed = 4,
                    V1InfrastructureFramesCollapsed = 4,
                    V1ReentrantFramesCollapsed = 14,
                },
            };
        }

        #endregion
    }
}
