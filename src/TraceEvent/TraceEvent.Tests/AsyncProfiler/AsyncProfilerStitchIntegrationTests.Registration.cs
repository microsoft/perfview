// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;

using Microsoft.Diagnostics.Tracing.Computers;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;

using Xunit;

namespace TraceEventTests
{
    public partial class AsyncProfilerStitchIntegrationTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void StateMachineAsync_ReentrantAwaitRegistration_DeduplicatesStartupCallStack(bool changedLayout)
        {
            Frame work = Frame.App("Scenario.ReentrantWork");
            Frame innerStateMachine = Frame.App("Scenario+<NestedDispatcherInnerAsync>d__15.MoveNext");
            Frame logicalInner = innerStateMachine.Logical("Scenario.NestedDispatcherInnerAsync");
            Frame innerKickoff = Frame.App("Scenario.NestedDispatcherInnerAsync");
            Frame outerStateMachine = Frame.App("Scenario+<NestedDispatcherOuterAsync>d__14.MoveNext");
            Frame logicalOuter = outerStateMachine.Logical("Scenario.NestedDispatcherOuterAsync");
            Frame innerInfrastructure = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncProfilerAsyncStateMachineBox.InstrumentedMoveNext",
                "Inner.InstrumentedMoveNext");
            Frame awaitGeneric = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].AwaitUnsafeOnCompleted(!!0&,!!1&)",
                "AwaitUnsafeOnCompleted(generic)");
            Frame awaitTask = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].AwaitUnsafeOnCompleted(!!0&,!!1&,class System.Threading.Tasks.Task`1<!0>&)",
                "AwaitUnsafeOnCompleted(task)");
            Frame awaitBox = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].AwaitUnsafeOnCompleted(!!0&,class System.Runtime.CompilerServices.IAsyncStateMachineBox)",
                "AwaitUnsafeOnCompleted(box)");
            Frame builderStart = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].Start(!!0&)",
                "AsyncTaskMethodBuilder.Start");
            Frame builderCoreStart = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start(!!0&)",
                "AsyncMethodBuilderCore.Start");

            var scenario = new StitchScenario
            {
                Kind = AsyncCallstackKind.StateMachineAsync,
                Sync = new[]
                {
                    work,
                    innerStateMachine,
                    Frame.V1MoveNextAsDispatcher,
                    innerInfrastructure,
                    awaitGeneric,
                    awaitTask,
                    awaitBox,
                    innerStateMachine,
                    builderStart,
                    builderCoreStart,
                    innerKickoff,
                    outerStateMachine,
                    Frame.V1MoveNextAsDispatcher,
                    innerInfrastructure,
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
                    logicalOuter,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                ExpectedWithoutNormalization = new[]
                {
                    work,
                    logicalInner,
                    awaitGeneric,
                    awaitTask,
                    awaitBox,
                    innerStateMachine,
                    builderStart,
                    builderCoreStart,
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
                    V1ReentrantFramesCollapsed = 14,
                },
            };

            if (changedLayout)
            {
                UseExtendedRuntimeLayouts(scenario);
                Frame helper = Frame.CoreLib(AsyncProfilerExtendedRuntimeLayoutSupport.HelperName, "LayoutHelper");
                Func<Frame[], Frame[]> extend = frames => frames.SelectMany(frame =>
                    frame.Key == awaitBox.Key ? new[] { frame, helper } :
                    frame.Key == builderStart.Key ? new[] { helper, frame } : new[] { frame }).ToArray();
                // Registration helpers precede the duplicate body; startup helpers follow it.
                scenario.Sync = extend(scenario.Sync);
                scenario.ExpectedWithoutNormalization = extend(scenario.ExpectedWithoutNormalization);
                scenario.ExpectedDiagnostics.V1ReentrantFramesCollapsed += 4;
            }
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DispatcherCreation_CleanupPreservesApplicationKickoffAndSampleWeights(bool enableNormalization)
        {
            StitchScenario scenario = CreateDispatcherCreationScenario();
            scenario.EnableNormalization = enableNormalization;

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, 1, 1, false)]
        [InlineData(true, 1, 1, false)]
        [InlineData(false, 4, 3, true)]
        [InlineData(true, 4, 3, true)]
        [InlineData(false, 9, 0, true)]
        [InlineData(true, 9, 3, true)]
        public void DispatcherCreation_BoundedRegistrationPreservesSynchronousChildrenAndCallees(
            bool enableNormalization, int helperCount, int calleeCount, bool includeStartup)
        {
            StitchScenario scenario = CreateBoundedDispatcherCreationScenario(
                enableNormalization, helperCount, calleeCount, includeStartup);
            scenario.AssertTransformContext = context =>
            {
                AsyncProfilerStitchedFrame marker = context.Frames[calleeCount];
                Assert.Equal(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames, marker.Presentation);
                Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, marker.Origin);
                Assert.Contains("CreateDispatcher", context.TraceLog.CodeAddresses[marker.CodeAddress].FullMethodName);
                AsyncProfilerStitchedFrame current = Assert.Single(context.Frames.Where(frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent));
                Assert.Same(current.Context, marker.Context);
                Assert.Equal(scenario.Sync.Take(calleeCount).Select(frame => frame.SymbolName),
                    context.Frames.Take(calleeCount).Select(frame =>
                        context.TraceLog.CodeAddresses[frame.CodeAddress].FullMethodName));
                Assert.All(context.Frames.Skip(calleeCount + 1).TakeWhile(frame => frame.Origin ==
                    AsyncProfilerStitchedFrameOrigin.Sync), frame => Assert.Same(current.Context, frame.Context));
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("unresolved-entry")]
        [InlineData("foreign-entry")]
        [InlineData("unresolved-registration")]
        [InlineData("foreign-registration")]
        [InlineData("other-runtime-registration")]
        [InlineData("unknown-registration")]
        [InlineData("application-registration")]
        [InlineData("unresolved-caller")]
        public void DispatcherCreation_BoundedRegistrationRequiresResolvedUninterruptedRuntimeEvidence(string obstruction)
        {
            StitchScenario scenario = CreateBoundedDispatcherCreationScenario(true, 4, 1, false);
            int index = obstruction.EndsWith("entry", StringComparison.Ordinal) ? 1 :
                obstruction.EndsWith("caller", StringComparison.Ordinal) ? 6 : 2;
            Frame original = scenario.Sync[index];
            if (obstruction.StartsWith("unresolved", StringComparison.Ordinal))
            {
                scenario.OmitSymbolFor = original;
            }
            else
            {
                Frame replacement = obstruction == "unknown-registration"
                    ? Frame.CoreLib("System.Runtime.CompilerServices.UnknownAwaiter.Register", "UnknownRegistration")
                    : obstruction == "application-registration"
                        ? Frame.App("Scenario.Callback")
                        : Frame.Library(obstruction == "other-runtime-registration"
                            ? @"C:\other-runtime\System.Private.CoreLib.dll" : "OtherLibrary",
                            original.SymbolName, original.Label + " (foreign)");
                scenario.Sync[index] = replacement;
                scenario.ExpectedWithoutInfrastructureCleanup[index] = replacement;
                scenario.ExpectedWithoutNormalization[index] = replacement;
            }
            if (index == 1)
            {
                scenario.ExpectedStitched = scenario.Sync.Take(2)
                    .Concat(new[] { scenario.Sync[2].DispatcherFrames(), scenario.Sync[6] })
                    .Concat(scenario.ExpectedStitched.Skip(3)).ToArray();
                scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 18;
            }
            else
            {
                scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup.Take(7)
                    .Concat(scenario.ExpectedStitched.Skip(3)).ToArray();
                scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 10;
            }

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("entry")]
        [InlineData("registration")]
        [InlineData("cleanup-policy")]
        public void DispatcherCreation_BoundedRegistrationRespectsSupportVetoes(string rejection)
        {
            StitchScenario scenario = CreateBoundedDispatcherCreationScenario(true, 4, 1, false);
            AsyncProfilerRuntimeSupport support = rejection == "entry"
                ? (AsyncProfilerRuntimeSupport)new RejectingProfilerEntrySupport()
                : rejection == "registration"
                    ? (AsyncProfilerRuntimeSupport)new RejectingAwaitRegistrationSupport()
                    : new DisabledDispatcherCreationCleanupSupport();
            scenario.RuntimeSupportForModule = module => AsyncProfilerRuntimeResolver.IsHostModule(module)
                ? support : AsyncProfilerRuntimeSupport.Common;
            scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup.Take(7)
                .Concat(scenario.ExpectedStitched.Skip(3)).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 10;

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DispatcherCreation_BoundedRegistrationDoesNotRequireOwnershipOfItsCallees(bool unresolved)
        {
            StitchScenario scenario = CreateBoundedDispatcherCreationScenario(true, 3, 1, true);
            if (unresolved)
            {
                scenario.OmitSymbolFor = scenario.Sync[0];
            }
            else
            {
                Frame callback = Frame.App("Observer.ArbitraryCallee");
                scenario.Sync[0] = callback;
                scenario.ExpectedStitched[0] = callback;
                scenario.ExpectedWithoutInfrastructureCleanup[0] = callback;
                scenario.ExpectedWithoutNormalization[0] = callback;
            }
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DispatcherCreation_BoundedRegistrationIncludesProfilerTypesButPreservesJit(bool enableNormalization)
        {
            StitchScenario scenario = CreateDispatcherCreationProfilerTypesScenario(enableNormalization);
            scenario.VerifySampleWeights = true;
            scenario.AssertTransformContext = context =>
            {
                AsyncProfilerStitchedFrame marker = context.Frames[2];
                Assert.Equal(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames, marker.Presentation);
                Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, marker.Origin);
                Assert.Contains("get_DispatcherId", context.TraceLog.CodeAddresses[marker.CodeAddress].FullMethodName);
                Assert.Same(context.Frames.First(frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent).Context, marker.Context);
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("application")]
        [InlineData("foreign")]
        [InlineData("unresolved")]
        public void DispatcherCreation_ProfilerTypeExtensionStopsAtUnverifiedFrames(string obstruction)
        {
            StitchScenario scenario = CreateDispatcherCreationProfilerTypesScenario(true);
            Frame helper = scenario.Sync[3];
            if (obstruction == "unresolved")
            {
                scenario.OmitSymbolFor = helper;
            }
            else
            {
                helper = obstruction == "application" ? Frame.App("Observer.Callback")
                    : Frame.Library("OtherRuntime", helper.SymbolName, "Foreign.Create");
                scenario.Sync[3] = helper;
                scenario.ExpectedWithoutInfrastructureCleanup[3] = helper;
                scenario.ExpectedWithoutNormalization[3] = helper;
            }
            scenario.ExpectedStitched = scenario.Sync.Take(4)
                .Concat(new[] { scenario.Sync[4].DispatcherFrames() })
                .Concat(scenario.ExpectedStitched.Skip(3)).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed -= 4;
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed -= 4;

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void DispatcherCreation_BoundedRegistrationPreservesRuntimeAsyncParentProvenance()
        {
            CreateBoundedDispatcherCreationScenario(
                true, 3, 1, true, AsyncCallstackKind.RuntimeAsync).AssertProductionStitch();
        }

        [Theory]
        [InlineData(1)]
        [InlineData(3)]
        [InlineData(6)]
        public void DispatcherCreation_BoundedRegistrationDoesNotCrossCallStackOwnership(int index)
        {
            StitchScenario scenario = CreateBoundedDispatcherCreationScenario(true, 4, 1, false);
            scenario.EnableInfrastructureCleanup = false;
            scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;
            scenario.AssertTransformContext = context =>
            {
                var frames = context.Frames.ToList();
                AsyncProfilerStitchedFrame frame = frames[index];
                frames[index] = AsyncProfilerStitchedFrame.CreateSync(
                    new AsyncProfilerStitchSyncFrame(frame.CodeAddress, frame.Method, frame.SyncClassification), context: null);
                AsyncProfilerStitchedFrame[] prefix = frames.Take(7).ToArray();
                var diagnostics = new AsyncProfilerCallStackDiagnostics { SegmentsProcessed = context.Segments.Count };
                new AsyncProfilerCallStackTransformPipeline
                {
                    EnableNormalization = false,
                    EnableInfrastructureCleanup = true,
                }.ApplyInPlace(new AsyncProfilerCallStackTransformState(
                    context.TraceLog, context.Segments, context.TimestampQpc,
                    context.ClassifyBoundary, context.StitchingState.Copy(), diagnostics, frames));
                Assert.Equal(prefix, frames.Take(7));
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, 1)]
        [InlineData(true, 1)]
        [InlineData(true, 100)]
        public void DispatcherCreationOwnership_FoldsTheEntireAwaitRegistrationPrefix(bool enableNormalization, int calleeCount)
        {
            StitchScenario scenario = CreateDispatcherOwnedPrefixScenario();
            scenario.EnableNormalization = enableNormalization;
            if (calleeCount > 1)
            {
                Frame[] callees = Enumerable.Range(0, calleeCount).Select(i => Frame.CoreLib(
                    "System.Runtime.CompilerServices.AsyncProfiler+OtherHelpers.Append" + i,
                    "Profiler.Append" + i)).ToArray();
                scenario.Sync = callees.Concat(scenario.Sync.Skip(1)).ToArray();
                scenario.ExpectedWithoutInfrastructureCleanup =
                    callees.Concat(scenario.ExpectedWithoutInfrastructureCleanup.Skip(1)).ToArray();
                scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed += (calleeCount - 1) * 2;
                scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed += (calleeCount - 1) * 2;
            }

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void V1AwaitRegistrationOwnership_FoldsContiguousHelpersWithoutASpecificProfilerEntry(
            bool enableNormalization, bool genericHelpers)
        {
            StitchScenario scenario = CreateV1AwaitRegistrationScenario();
            scenario.EnableNormalization = enableNormalization;
            if (genericHelpers)
            {
                Frame[] helpers =
                {
                    Frame.CoreLib(
                        "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncStateMachineBox`1[System.Int64,Scenario+<Level2Async>d__2].ClearStateUponCompletion()",
                        "Box.ClearState"),
                    Frame.CoreLib("System.Runtime.CompilerServices.AsyncStateMachineDiagnostics`1[Scenario+<Level2Async>d__2].ResolveStateFieldOffset()",
                        "Diagnostics.ResolveState"),
                    Frame.CoreLib("System.Runtime.CompilerServices.AsyncProfiler+OtherHelpers.Append()",
                        "Profiler.OtherHelper"),
                };
                scenario.Sync = scenario.Sync.Take(1).Concat(helpers).Concat(scenario.Sync.Skip(4)).ToArray();
                scenario.ExpectedWithoutInfrastructureCleanup = scenario.Sync.Take(6)
                    .Concat(scenario.ExpectedWithoutInfrastructureCleanup.Skip(6)).ToArray();
            }
            scenario.AssertTransformContext = context =>
            {
                AsyncProfilerStitchedFrame marker = Assert.Single(context.Frames.Where(frame =>
                    frame.Presentation == AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames));
                AsyncProfilerStitchedFrame current = Assert.Single(context.Frames.Where(frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent));
                Assert.Same(current.Context, marker.Context);
                Assert.Equal(0, current.Segment.FrameStateAt(current.SegmentFrameIndex));
                Assert.Equal(context.TraceLog.CodeAddresses.MethodIndex(current.CodeAddress), current.Method);
                Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, context.Frames[0].Origin);
                Assert.Equal(AsyncProfilerStitchedFramePresentation.Native, context.Frames[0].Presentation);
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, false, "none", "ValueTaskAwaiter")]
        [InlineData(false, true, "none", "ValueTaskAwaiter")]
        [InlineData(true, false, "none", "ValueTaskAwaiter")]
        [InlineData(true, true, "none", "ValueTaskAwaiter")]
        [InlineData(true, true, "foreign", "ValueTaskAwaiter")]
        [InlineData(true, true, "unresolved", "ValueTaskAwaiter")]
        [InlineData(true, true, "none", "TaskAwaiter")]
        [InlineData(true, true, "none", "TaskAwaiter`1[System.Int64]")]
        [InlineData(true, true, "none", "ConfiguredTaskAwaitable+ConfiguredTaskAwaiter")]
        [InlineData(true, true, "none", "ConfiguredTaskAwaitable`1+ConfiguredTaskAwaiter[System.Int64]")]
        [InlineData(true, true, "none", "ConfiguredTaskAwaitable`1[System.Int64]+ConfiguredTaskAwaiter")]
        [InlineData(true, true, "none", "ConfiguredValueTaskAwaitable+ConfiguredValueTaskAwaiter")]
        [InlineData(true, true, "none", "ConfiguredValueTaskAwaitable`1+ConfiguredValueTaskAwaiter[System.Int64]")]
        [InlineData(true, true, "none", "ConfiguredValueTaskAwaitable`1[System.Int64]+ConfiguredValueTaskAwaiter")]
        public void AwaiterReadinessCheck_GroupsOnlyTheAccessorAndPreservesSynchronousKickoff(
            bool enableNormalization, bool cleanup, string obstruction, string awaiterType)
        {
            Frame native = Frame.Library("coreclr", "MethodDesc.PrepareCode", "NativeWork");
            Frame awaiter = Frame.CoreLib("System.Runtime.CompilerServices." + awaiterType + ".get_IsCompleted()", "Awaiter.IsCompleted");
            if (obstruction == "foreign")
            {
                awaiter = Frame.Library("OtherLibrary", awaiter.SymbolName, "Foreign.IsCompleted");
            }
            Frame kickoff = Frame.App("Scenario.ValueTaskLevel2Async()");
            Frame current = Frame.App("Scenario+<ValueTaskLevel1Async>d__14.MoveNext");
            Frame logical = current.Logical("Scenario.ValueTaskLevel1Async", -1);
            bool grouped = cleanup && obstruction == "none";
            var scenario = new StitchScenario
            {
                Kind = AsyncCallstackKind.StateMachineAsync,
                Sync = new[] { native, awaiter, kickoff, current, Frame.V1MoveNextAsDispatcher, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart },
                Async = new[] { current },
                AsyncStates = new[] { 1 },
                OmitSymbolFor = obstruction == "unresolved" ? awaiter : null,
                EnableFrameAnnotations = true,
                EnableNormalization = enableNormalization,
                EnableInfrastructureCleanup = cleanup,
                SkipNormalStackAssertion = true,
                VerifySampleWeights = true,
                ExpectedStitched = new[] { native, grouped ? awaiter.DispatcherFrames() : awaiter, kickoff, logical, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart },
                ExpectedWithoutInfrastructureCleanup = new[] { native, awaiter, kickoff, logical, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V1InlineFallbackUsed = 2,
                    AsyncInfrastructureFramesCollapsed = grouped ? 2 : 0,
                },
                AssertTransformContext = context =>
                {
                    AsyncProfilerStitchedFrame running = Assert.Single(context.Frames.Where(frame =>
                        frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent));
                    Assert.Equal(1, running.Segment.FrameStateAt(running.SegmentFrameIndex));
                    Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, context.Frames[2].Origin);
                    Assert.Equal(AsyncProfilerStitchedFramePresentation.Native, context.Frames[2].Presentation);
                    Assert.False(context.ClassifyBoundary(context.Frames[1].CodeAddress).IsDispatchMarker);
                    Assert.Equal(grouped ? AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames :
                        AsyncProfilerStitchedFramePresentation.Native, context.Frames[1].Presentation);
                    Assert.DoesNotContain(context.Frames, frame =>
                        frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget);
                },
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("ValueTaskAwaiter")]
        [InlineData("ValueTaskAwaiter`1[System.Int64]")]
        [InlineData("ConfiguredValueTaskAwaitable+ConfiguredValueTaskAwaiter")]
        [InlineData("ConfiguredValueTaskAwaitable`1[System.Int64]+ConfiguredValueTaskAwaiter")]
        public void V1AwaitRegistrationOwnership_GroupsKnownExplicitInterfaceAwaiters(string awaiterType)
        {
            StitchScenario scenario = CreateV1AwaitRegistrationScenario();
            Frame helper = Frame.CoreLib(
                "System.Runtime.CompilerServices." + awaiterType +
                ".System.Runtime.CompilerServices.IStateMachineBoxAwareAwaiter.AwaitUnsafeOnCompleted(class System.Runtime.CompilerServices.IAsyncStateMachineBox)",
                "Explicit await registration");
            scenario.Sync[4] = helper;
            scenario.ExpectedWithoutInfrastructureCleanup[4] = helper;
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("QueueContinuation", false, "none")]
        [InlineData("QueueContinuation", true, "none")]
        [InlineData("QueueContinuation", true, "foreign")]
        [InlineData("QueueContinuation", true, "unresolved")]
        [InlineData("OutputCorrelationEtwEvent", false, "none")]
        [InlineData("OutputCorrelationEtwEvent", true, "none")]
        [InlineData("OutputCorrelationEtwEvent", true, "foreign")]
        [InlineData("OutputCorrelationEtwEvent", true, "unresolved")]
        public void YieldContinuationRegistrationHelpers_GroupMachineryButPreserveRuntimeCallees(
            string method, bool enableNormalization, string obstruction)
        {
            StitchScenario scenario = CreateV1AwaitRegistrationScenario();
            Frame helper = Frame.CoreLib(
                "System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter." + method +
                    (method == "QueueContinuation" ? "(System.Action,bool)" : "(System.Action)"),
                "Yield." + method);
            if (obstruction == "foreign")
            {
                helper = Frame.Library("OtherLibrary", helper.SymbolName, "Foreign." + method);
            }
            else if (obstruction == "unresolved")
            {
                scenario.OmitSymbolFor = helper;
            }
            Frame[] callees = method == "QueueContinuation" ? new[]
            {
                Frame.Library("ntoskrnl", "Kernel.Wake1", "Kernel.Wake1"),
                Frame.Library("ntoskrnl", "Kernel.Wake2", "Kernel.Wake2"),
                Frame.Library("ntdll", "Native.Wake1", "Native.Wake1"),
                Frame.Library("ntdll", "Native.Wake2", "Native.Wake2"),
                Frame.CoreLib("System.Threading.LowLevelLifoSemaphore.WakeOneCore()", "Semaphore.WakeOneCore"),
                Frame.CoreLib("System.Threading.LowLevelLifoSemaphore.MaybeWakeWaiterSlow()", "Semaphore.MaybeWakeWaiterSlow"),
                Frame.CoreLib("System.Threading.PortableThreadPool+WorkerThread.MaybeAddWorkingWorker()", "Pool.MaybeAddWorkingWorker"),
                Frame.CoreLib("System.Threading.PortableThreadPool.EnsureWorkerRequested()", "Pool.EnsureWorkerRequested"),
                Frame.CoreLib("System.Threading.ThreadPool.UnsafeQueueUserWorkItem()", "Pool.UnsafeQueueUserWorkItem"),
            } : new[]
            {
                Frame.Library("coreclr", "Native.Work1", "Native.Work1"),
                Frame.Library("coreclr", "Native.Work2", "Native.Work2"),
                Frame.CoreLib("System.Runtime.CompilerServices.StaticsHelpers.GetGCThreadStaticsByIndexSlow(int32)",
                    "Statics.GetGCThreadStaticsByIndexSlow"),
            };
            Frame[] builders =
            {
                scenario.Sync[5],
                Frame.CoreLib(
                    "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].AwaitUnsafeOnCompleted(!!0&,!!1&,class System.Threading.Tasks.Task`1<!0>&)",
                    "Builder.AwaitUnsafeOnCompleted(task)"),
                Frame.CoreLib(
                    "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].AwaitUnsafeOnCompleted(!!0&,!!1&)",
                    "Builder.AwaitUnsafeOnCompleted(generic)"),
            };
            Frame[] prefix = callees.Concat(new[] { helper }).Concat(builders).ToArray();
            scenario.Sync = prefix.Concat(scenario.Sync.Skip(6)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = prefix
                .Concat(scenario.ExpectedWithoutInfrastructureCleanup.Skip(6)).ToArray();
            scenario.ExpectedStitched = callees.Concat(obstruction == "none"
                ? new[] { helper.DispatcherFrames() } : new[] { helper, builders[0].DispatcherFrames() })
                .Concat(scenario.ExpectedStitched.Skip(1)).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = obstruction == "none" ? 18 : 16;
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = obstruction == "none" ? 8 : 6;
            scenario.EnableNormalization = enableNormalization;
            scenario.AssertTransformContext = context =>
            {
                Assert.Equal(callees.Select(frame => frame.SymbolName),
                    context.Frames.Take(callees.Length).Select(frame =>
                        context.TraceLog.CodeAddresses[frame.CodeAddress].FullMethodName));
                AsyncProfilerStitchedFrame marker = context.Frames[callees.Length + (obstruction == "none" ? 0 : 1)];
                Assert.Equal(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames, marker.Presentation);
                Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, marker.Origin);
                Assert.Contains(obstruction == "none" ? method : "AwaitUnsafeOnCompleted",
                    context.TraceLog.CodeAddresses[marker.CodeAddress].FullMethodName);
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("application")]
        [InlineData("foreign")]
        [InlineData("unresolved")]
        public void V1AwaitRegistrationOwnership_StopsAtABreakAndDoesNotRestartAtEligibleHelpers(string obstruction)
        {
            StitchScenario scenario = CreateV1AwaitRegistrationScenario();
            Frame helper = scenario.Sync[2];
            if (obstruction == "unresolved")
            {
                scenario.OmitSymbolFor = helper;
            }
            else
            {
                Frame replacement = obstruction == "application"
                    ? Frame.App("Scenario.Callback")
                    : Frame.Library("OtherLibrary", helper.SymbolName, "ForeignCapture");
                scenario.Sync[2] = replacement;
                scenario.ExpectedWithoutInfrastructureCleanup[2] = replacement;
            }
            scenario.ExpectedStitched = scenario.Sync.Take(3).Concat(scenario.ExpectedStitched.Skip(1)).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 16;
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = 6;

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("dispatch-target")]
        [InlineData("parent-current")]
        [InlineData("completing")]
        [InlineData("unresolved-body")]
        [InlineData("registration-context")]
        [InlineData("missing-boundary")]
        public void V1AwaitRegistrationOwnership_RequiresVerifiedPhysicalCurrentAndConsistentEvidence(string obstruction)
        {
            StitchScenario scenario = CreateV1AwaitRegistrationScenario();
            scenario.EnableInfrastructureCleanup = false;
            scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = 0;
            scenario.AssertTransformContext = context =>
            {
                var frames = context.Frames.ToList();
                int currentIndex = frames.FindIndex(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
                AsyncProfilerStitchedFrame current = frames[currentIndex];
                if (obstruction == "dispatch-target")
                {
                    frames[currentIndex] = AsyncProfilerStitchedFrame.CreateAsyncDispatchTarget(
                        current.Context, current.SegmentFrameIndex);
                }
                else if (obstruction == "parent-current")
                {
                    frames[currentIndex] = AsyncProfilerStitchedFrame.CreateAsyncContextParent(
                        current.Context, current.SegmentFrameIndex, isCurrent: true);
                }
                else if (obstruction == "completing")
                {
                    frames[currentIndex] = AsyncProfilerStitchedFrame.CreateV1CompletedBody(
                        current.CodeAddress, current.Method, current.Context, current.SegmentFrameIndex);
                }
                else if (obstruction == "unresolved-body")
                {
                    frames[currentIndex] = AsyncProfilerStitchedFrame.CreateAsyncCurrent(
                        current.CodeAddress, MethodIndex.Invalid, current.Context, current.SegmentFrameIndex);
                }
                else if (obstruction == "registration-context")
                {
                    AsyncProfilerStitchedFrame registration = frames[currentIndex - 1];
                    frames[currentIndex - 1] = AsyncProfilerStitchedFrame.CreateSync(
                        new AsyncProfilerStitchSyncFrame(registration.CodeAddress, registration.Method, registration.SyncClassification),
                        context: null);
                }
                AsyncProfilerStitchedFrame[] prefix = frames.Take(currentIndex).ToArray();
                var diagnostics = new AsyncProfilerCallStackDiagnostics();
                AsyncProfilerCallStackStitchingState stitchingState = context.StitchingState.Copy();
                stitchingState.HasConsistentPhysicalAlignment = obstruction != "missing-boundary";
                new AsyncProfilerCallStackTransformPipeline
                {
                    EnableNormalization = false,
                    EnableInfrastructureCleanup = true,
                }.ApplyInPlace(new AsyncProfilerCallStackTransformState(
                    context.TraceLog, context.Segments, context.TimestampQpc,
                    context.ClassifyBoundary, stitchingState, diagnostics, frames));

                Assert.Equal(prefix, frames.Take(currentIndex));
                Assert.Equal(0, diagnostics.ProfilerEnvelopeFramesCollapsed);
            };

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void V1AwaitRegistrationOwnership_RespectsSupportRejection()
        {
            StitchScenario scenario = CreateV1AwaitRegistrationScenario();
            var support = new RejectingAwaitRegistrationSupport();
            scenario.RuntimeSupportForModule = module => AsyncProfilerRuntimeResolver.IsHostModule(module)
                ? support : AsyncProfilerRuntimeSupport.Common;
            scenario.ExpectedStitched = scenario.Sync.Take(6).Concat(scenario.ExpectedStitched.Skip(1)).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 10;
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = 0;

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DispatcherCreationOwnership_PreservesCalleesAboveTheVerifiedEntry(bool unresolved)
        {
            StitchScenario scenario = CreateDispatcherOwnedPrefixScenario();
            Frame callback = unresolved
                ? Frame.CoreLib("System.Runtime.CompilerServices.UnknownCallback.Run", "UnknownCallback.Run")
                : Frame.App("Scenario.Callback");
            if (unresolved)
            {
                scenario.OmitSymbolFor = callback;
            }
            scenario.Sync = new[] { callback }.Concat(scenario.Sync).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup =
                new[] { callback }.Concat(scenario.ExpectedWithoutInfrastructureCleanup).ToArray();
            scenario.ExpectedStitched = new[] { callback }.Concat(scenario.ExpectedStitched).ToArray();

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("application")]
        [InlineData("foreign")]
        [InlineData("unresolved")]
        public void DispatcherCreationOwnership_UnverifiedRegistration_IsProtected(string obstruction)
        {
            StitchScenario scenario = CreateDispatcherOwnedPrefixScenario();
            Frame awaiter = scenario.Sync[2];
            if (obstruction == "unresolved")
            {
                scenario.OmitSymbolFor = awaiter;
            }
            else
            {
                Frame replacement = obstruction == "application"
                    ? Frame.App("Scenario.Callback")
                    : Frame.Library("OtherLibrary", awaiter.SymbolName, awaiter.Label);
                scenario.Sync[2] = replacement;
                scenario.ExpectedWithoutInfrastructureCleanup[2] = replacement;
            }
            scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup.Take(6)
                .Concat(scenario.ExpectedStitched).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 10;
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = 0;

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void DispatcherCreation_UnresolvedOrForeignHelper_IsPreserved(bool foreign)
        {
            StitchScenario scenario = CreateDispatcherCreationScenario();
            Frame helper = scenario.Sync[0];
            if (foreign)
            {
                helper = Frame.Library("OtherLibrary", helper.SymbolName, helper.Label + " (foreign)");
                scenario.Sync[0] = helper;
                scenario.ExpectedWithoutInfrastructureCleanup[0] = helper;
            }
            else
            {
                scenario.OmitSymbolFor = helper;
            }
            scenario.ExpectedStitched = new[] { helper }.Concat(scenario.ExpectedStitched).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed -= 2;

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void DispatcherCreation_InsideProfilerOwnedPrefix_PreservesItsCallback()
        {
            StitchScenario scenario = CreateProfilerEnvelopeScenario();
            Frame helper = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncStateMachineDispatcherInfo.CreateDispatcher(class System.Runtime.CompilerServices.IAsyncStateMachineBox,value class Flags)",
                "CreateDispatcher");
            Frame callback = Frame.App("Scenario.Callback");
            Frame[] prefix = { helper, callback };
            scenario.Sync = prefix.Concat(scenario.Sync).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup =
                prefix.Concat(scenario.ExpectedWithoutInfrastructureCleanup).ToArray();
            scenario.ExpectedStitched = prefix.Concat(scenario.ExpectedStitched).ToArray();

            scenario.AssertProductionStitch();
        }

        #region private

        private static StitchScenario CreateDispatcherCreationScenario(
            AsyncCallstackKind kind = AsyncCallstackKind.StateMachineAsync)
        {
            StitchScenario scenario = CreateRuntimeResumptionBridgeScenario(kind);
            Frame kickoff = Frame.App("Scenario.BurnCpuAsync(int32,int64)");
            Frame[] prefix =
            {
                Frame.CoreLib(
                    "System.Runtime.CompilerServices.AsyncStateMachineDispatcherInfo.CreateDispatcher(class System.Runtime.CompilerServices.IAsyncStateMachineBox,value class Flags)",
                    "CreateDispatcher"),
                Frame.CoreLib(
                    "System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter.System.Runtime.CompilerServices.IStateMachineBoxAwareAwaiter.AwaitUnsafeOnCompleted(class System.Runtime.CompilerServices.IAsyncStateMachineBox)",
                    "YieldAwaiter.AwaitUnsafeOnCompleted"),
                Frame.CoreLib(
                    "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].AwaitUnsafeOnCompleted(!!0&,class System.Runtime.CompilerServices.IAsyncStateMachineBox)",
                    "AwaitUnsafeOnCompleted(box)"),
                Frame.CoreLib(
                    "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].AwaitUnsafeOnCompleted(!!0&,!!1&,class System.Threading.Tasks.Task`1<!0>&)",
                    "AwaitUnsafeOnCompleted(task)"),
                Frame.CoreLib(
                    "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].AwaitUnsafeOnCompleted(!!0&,!!1&)",
                    "AwaitUnsafeOnCompleted(generic)"),
                kickoff,
            };
            scenario.Sync = prefix.Concat(scenario.Sync.Skip(1)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup =
                prefix.Concat(scenario.ExpectedWithoutInfrastructureCleanup.Skip(1)).ToArray();
            scenario.ExpectedStitched = new[] { kickoff }.Concat(scenario.ExpectedStitched.Skip(1)).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed += 10;
            return scenario;
        }

        private static StitchScenario CreateDispatcherOwnedPrefixScenario()
        {
            StitchScenario scenario = CreateDispatcherCreationScenario();
            Frame append = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncProfiler+CreateAsyncContext.Append(value class System.Runtime.CompilerServices.AsyncStateMachineDispatcherInfo&)",
                "Profiler.Append");
            Frame[] prefix = new[] { append }.Concat(scenario.Sync.Take(5)).ToArray();
            // Registration belongs directly to the physical current method, not a synchronous child kickoff.
            scenario.Sync = prefix.Concat(scenario.Sync.Skip(6)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup =
                prefix.Concat(scenario.ExpectedWithoutInfrastructureCleanup.Skip(6)).ToArray();
            scenario.ExpectedStitched = scenario.ExpectedStitched.Skip(1).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 22;
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = 12;
            return scenario;
        }

        private static StitchScenario CreateBoundedDispatcherCreationScenario(
            bool enableNormalization, int helperCount, int calleeCount, bool includeStartup,
            AsyncCallstackKind kind = AsyncCallstackKind.StateMachineAsync)
        {
            StitchScenario scenario = CreateDispatcherCreationScenario(kind);
            Frame entry = scenario.Sync[0];
            Frame kickoff = scenario.Sync[5];
            Frame[] callees = Enumerable.Range(0, calleeCount).Select(i =>
                Frame.Library("coreclr", "Native.Work" + i, "NativeWork" + i)).ToArray();
            Frame[] helperKinds =
            {
                Frame.CoreLib(
                    "System.Runtime.CompilerServices.TaskAwaiter.UnsafeOnCompletedInternal(class System.Threading.Tasks.Task,class System.Runtime.CompilerServices.IAsyncStateMachineBox,bool)",
                    "TaskAwaiter.UnsafeOnCompletedInternal"),
                scenario.Sync[3], scenario.Sync[4], scenario.Sync[1], scenario.Sync[2],
            };
            Frame[] helpers = Enumerable.Range(0, helperCount).Select(i => helperKinds[i % helperKinds.Length]).ToArray();
            Frame[] startup =
            {
                Frame.App("Scenario+<BurnCpuAsync>d__3.MoveNext"),
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start(!!0&)", "BuilderCore.Start"),
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].Start(!!0&)", "Builder.Start"),
            };
            Frame[] child = (includeStartup ? startup : Array.Empty<Frame>()).Concat(new[] { kickoff }).ToArray();
            Frame[] prefix = callees.Concat(new[] { entry }).Concat(helpers).ToArray();
            Frame[] logicalAndHost = scenario.ExpectedStitched.Skip(1).ToArray();
            scenario.Sync = prefix.Concat(child).Concat(scenario.Sync.Skip(6)).ToArray();
            scenario.ExpectedWithoutNormalization = prefix.Concat(child)
                .Concat(scenario.ExpectedWithoutInfrastructureCleanup.Skip(6)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = prefix
                .Concat(enableNormalization ? new[] { kickoff } : child)
                .Concat(scenario.ExpectedWithoutInfrastructureCleanup.Skip(6)).ToArray();
            Frame[] displayedChild = includeStartup && !enableNormalization
                ? new[] { startup[0], startup[1].DispatcherFrames(), kickoff } : new[] { kickoff };
            scenario.ExpectedStitched = callees.Concat(new[] { entry.DispatcherFrames() })
                .Concat(displayedChild).Concat(logicalAndHost).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed =
                10 + 2 * (1 + helperCount) + (includeStartup && !enableNormalization ? 4 : 0);
            scenario.ExpectedDiagnostics.V1SynchronousStartupFramesCollapsed = includeStartup && enableNormalization ? 6 : 0;
            scenario.EnableNormalization = enableNormalization;
            return scenario;
        }

        private static StitchScenario CreateDispatcherCreationProfilerTypesScenario(bool enableNormalization)
        {
            StitchScenario scenario = CreateBoundedDispatcherCreationScenario(enableNormalization, 4, 2, true);
            Frame jit = Frame.Library("clrjit", "JitCompiler.compileMethod", "JIT work");
            scenario.Sync[0] = jit;
            scenario.ExpectedWithoutInfrastructureCleanup[0] = jit;
            scenario.ExpectedWithoutNormalization[0] = jit;
            scenario.ExpectedStitched[0] = jit;
            Frame[] helpers =
            {
                Frame.CoreLib(
                    "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncProfilerAsyncStateMachineBox`1[System.Int64,Scenario+<BurnCpuAsync>d__3].System.Runtime.CompilerServices.IAsyncStateMachineDispatcher.get_DispatcherId()",
                    "Box.DispatcherId"),
                Frame.CoreLib(
                    "System.Runtime.CompilerServices.AsyncProfiler+CreateAsyncContext.Create(System.Runtime.CompilerServices.AsyncStateMachineDispatcherInfo&,System.Runtime.CompilerServices.IAsyncStateMachineDispatcher)",
                    "Profiler.Create"),
            };
            scenario.Sync = scenario.Sync.Take(2).Concat(helpers).Concat(scenario.Sync.Skip(2)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = scenario.ExpectedWithoutInfrastructureCleanup
                .Take(2).Concat(helpers).Concat(scenario.ExpectedWithoutInfrastructureCleanup.Skip(2)).ToArray();
            scenario.ExpectedWithoutNormalization = scenario.ExpectedWithoutNormalization
                .Take(2).Concat(helpers).Concat(scenario.ExpectedWithoutNormalization.Skip(2)).ToArray();
            scenario.ExpectedStitched = scenario.ExpectedStitched.Take(2)
                .Concat(new[] { helpers[0].DispatcherFrames() }).Concat(scenario.ExpectedStitched.Skip(3)).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed += 4;
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed += 4;
            return scenario;
        }

        private static StitchScenario CreateV1AwaitRegistrationScenario()
        {
            StitchScenario scenario = CreateRuntimeResumptionBridgeScenario(AsyncCallstackKind.StateMachineAsync);
            Frame[] prefix =
            {
                Frame.Library("coreclr", "MethodDesc.PrepareCode", "NativeWork"),
                Frame.CoreLib(
                    "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncStateMachineBox`1[System.Int64,Scenario+<Level1Async>d__1].System.Runtime.CompilerServices.IAsyncStateMachineBox.GetDiagnosticData(unsigned int64&,int32&,class System.Object&)",
                    "Box.GetDiagnosticData"),
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncProfiler+AsyncCallstack.CaptureStateMachineAsyncCallstack",
                    "Profiler.Capture"),
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncProfiler+AsyncCallstack.EmitAsyncCallstack",
                    "Profiler.Emit"),
                Frame.CoreLib(
                    "System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter.System.Runtime.CompilerServices.IStateMachineBoxAwareAwaiter.AwaitUnsafeOnCompleted(class System.Runtime.CompilerServices.IAsyncStateMachineBox)",
                    "YieldAwaiter.AwaitUnsafeOnCompleted"),
                Frame.CoreLib(
                    "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].AwaitUnsafeOnCompleted(!!0&,class System.Runtime.CompilerServices.IAsyncStateMachineBox)",
                    "AwaitUnsafeOnCompleted(box)"),
            };
            scenario.Sync = prefix.Concat(scenario.Sync.Skip(1)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup =
                prefix.Concat(scenario.ExpectedWithoutInfrastructureCleanup.Skip(1)).ToArray();
            scenario.ExpectedStitched = prefix.Take(1).Concat(scenario.ExpectedStitched.Skip(1)).ToArray();
            scenario.AsyncStates = new[] { 0, 1 };
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 20;
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = 10;
            return scenario;
        }

        private sealed class RejectingAwaitRegistrationSupport : AsyncProfilerRuntimeSupport
        {
            public RejectingAwaitRegistrationSupport() : base("Rejected await registration") { }

            protected override AsyncProfilerRuntimeSequenceMatch MatchV1AwaitRegistration(
                AsyncProfilerCallStackTransformState context, int start, int end) =>
                start < end ? AsyncProfilerRuntimeSequenceMatch.Reject(end - start) : default;
        }

        private sealed class DisabledDispatcherCreationCleanupSupport : AsyncProfilerRuntimeSupport
        {
            public DisabledDispatcherCreationCleanupSupport() : base("Disabled dispatcher creation cleanup") { }

            public override bool IsOptionalInfrastructureFrame(
                AsyncProfilerStitchedFrame frame, AsyncProfilerCallStackStitchBoundaryKind boundary) =>
                frame.SyncFrameKind != AsyncProfilerStitchSyncFrameKind.V1DispatchSetup &&
                base.IsOptionalInfrastructureFrame(frame, boundary);
        }

        #endregion
    }
}
