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
        [InlineData(false, false, false)]
        [InlineData(false, true, false)]
        [InlineData(false, false, true)]
        [InlineData(false, true, true)]
        [InlineData(true, false, false)]
        [InlineData(true, true, false)]
        [InlineData(true, false, true)]
        [InlineData(true, true, true)]
        public void AsyncMachineryCallees_KeepTheirBoundaryAboveThePhysicalApplicationBody(
            bool runtimeAsync, bool enableNormalization, bool enableInfrastructureCleanup)
        {
            Frame body = Frame.App(runtimeAsync ? "Scenario.CurrentAsync" : "Scenario+<CurrentAsync>d__8.MoveNext");
            Frame current = runtimeAsync ? body : body.Logical("Scenario.CurrentAsync", -1);
            Frame helper = Frame.CoreLib(runtimeAsync
                ? "System.Threading.Tasks.Task.RunContinuations"
                : "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].AwaitUnsafeOnCompleted(!!0&,!!1&)",
                "RuntimeHelper");
            Frame[] callees = Enumerable.Range(0, 9)
                .Select(i => Frame.Library("coreclr", "Native.Work" + i, "NativeFrame" + i))
                .Concat(new[] { Frame.App("Observer.ArbitraryCallee") }).ToArray();
            Frame[] tail = { Frame.ThreadPoolDispatch, Frame.WorkerThreadStart };
            Frame boundary = runtimeAsync ? Frame.V2Wrapper(0) : Frame.V1MoveNextAsDispatcher;
            Frame[] infrastructure = runtimeAsync
                ? new[] { Frame.V2InstrumentedDispatch, Frame.V2Dispatch } : Array.Empty<Frame>();
            var scenario = new StitchScenario
            {
                Kind = runtimeAsync ? AsyncCallstackKind.RuntimeAsync : AsyncCallstackKind.StateMachineAsync,
                Sync = callees.Concat(new[] { helper, current, boundary }).Concat(infrastructure).Concat(tail).ToArray(),
                Async = new[] { body },
                AsyncStates = runtimeAsync ? null : new[] { 0 },
                OmitSymbolFor = callees[0],
                EnableNormalization = enableNormalization,
                EnableInfrastructureCleanup = enableInfrastructureCleanup,
                EnableFrameAnnotations = true,
                VerifySampleWeights = true,
                ExpectedStitched = callees.Concat(new[]
                    { enableInfrastructureCleanup ? helper.DispatcherFrames() : helper, current }).Concat(tail).ToArray(),
                ExpectedWithoutInfrastructureCleanup = callees.Concat(new[] { helper, current }).Concat(tail).ToArray(),
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V1InlineFallbackUsed = runtimeAsync ? 0 : 2,
                    V2InfrastructureFramesCollapsed = runtimeAsync ? 4 : 0,
                    AsyncInfrastructureFramesCollapsed = enableInfrastructureCleanup ? 2 : 0,
                    ProfilerEnvelopeFramesCollapsed = !runtimeAsync && enableInfrastructureCleanup ? 2 : 0,
                },
                AssertTransformContext = context =>
                {
                    AsyncProfilerStitchedFrame active = Assert.Single(context.Frames.Where(frame =>
                        frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent));
                    Assert.False(active.IsStateMachineBoxCarrier);
                    Assert.DoesNotContain(context.Frames, frame =>
                        frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget);
                    Assert.Equal(MethodIndex.Invalid, context.Frames[0].Method);
                    Assert.Equal(callees.Skip(1).Select(frame => frame.SymbolName),
                        context.Frames.Skip(1).Take(callees.Length - 1).Select(frame =>
                            context.TraceLog.CodeAddresses[frame.CodeAddress].FullMethodName));
                    AsyncProfilerStitchedFrame machinery = context.Frames[callees.Length];
                    Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, machinery.Origin);
                    Assert.Same(active.Context, machinery.Context);
                    Assert.Equal(helper.SymbolName, context.TraceLog.CodeAddresses[machinery.CodeAddress].FullMethodName);
                    Assert.Equal(enableInfrastructureCleanup
                        ? AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames
                        : AsyncProfilerStitchedFramePresentation.Native, machinery.Presentation);
                    Assert.Equal(callees.Length + 1, context.Frames.IndexOf(active));
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void AsyncMachineryMarkers_MergeOnlyForTheSameCapturedRuntimeOwner(bool nativeAlias)
        {
            Frame first = Frame.Library(@"C:\runtime-a\System.Private.CoreLib.dll",
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1.GetStateMachineBox", "GetBox");
            Frame second = Frame.Library(nativeAlias
                ? @"C:\runtime-a\System.Private.CoreLib.ni.dll" : @"C:\runtime-b\System.Private.CoreLib.dll",
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1.CreateDebugFinalizableAsyncStateMachineBox", "CreateBox");
            Frame body = Frame.App("Scenario+<CurrentAsync>d__8.MoveNext");
            Frame current = body.Logical("Scenario.CurrentAsync", -1);
            Frame callee = Frame.App("Observer.ArbitraryCallee");
            Frame[] tail = { current, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart };
            Frame[] markers = nativeAlias
                ? new[] { first.DispatcherFrames() } : new[] { first.DispatcherFrames(), second.DispatcherFrames() };
            var scenario = new StitchScenario
            {
                Kind = AsyncCallstackKind.StateMachineAsync,
                Sync = new[] { callee, first, second, current, Frame.V1MoveNextAsDispatcher }
                    .Concat(tail.Skip(1)).ToArray(),
                Async = new[] { body },
                AsyncStates = new[] { 0 },
                EnableInfrastructureCleanup = true,
                EnableFrameAnnotations = true,
                VerifySampleWeights = true,
                ExpectedStitched = new[] { callee }.Concat(markers).Concat(tail).ToArray(),
                ExpectedWithoutInfrastructureCleanup = new[] { callee, first, second }.Concat(tail).ToArray(),
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V1InlineFallbackUsed = 2,
                    AsyncInfrastructureFramesCollapsed = 4,
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
                AssertTransformContext = context =>
                {
                    AsyncProfilerStitchedFrame[] actual = context.Frames.Where(frame =>
                        frame.Presentation == AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames).ToArray();
                    Assert.Equal(nativeAlias ? 1 : 2, actual.Length);
                    Assert.All(actual, frame => Assert.Same(context.Segments[0], frame.Context));
                    Assert.Equal(first.SymbolName, context.TraceLog.CodeAddresses[actual[0].CodeAddress].FullMethodName);
                    if (!nativeAlias)
                    {
                        Assert.Same(context.GetRuntimeSupport(actual[0].CodeAddress), context.GetRuntimeSupport(actual[1].CodeAddress));
                        Assert.NotEqual(context.GetHostModuleOwner(actual[0].CodeAddress), context.GetHostModuleOwner(actual[1].CodeAddress));
                    }
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void V1ExceptionMachinery_GroupsFaultHelpersAndInlineAwaitersWithoutHidingHandlers(bool inline)
        {
            StitchScenario scenario = CreateV1ExceptionMachineryScenario(inline);
            scenario.AssertTransformContext = context =>
            {
                Assert.Single(context.Frames.TakeWhile(frame =>
                    frame.SyncFrameKind != AsyncProfilerStitchSyncFrameKind.V1StateMachineBody).Where(frame =>
                    frame.Presentation == AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames));
                Assert.Equal(2, context.Frames.Count(frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
                    frame.SyncFrameKind == AsyncProfilerStitchSyncFrameKind.V1StateMachineBody));
                Assert.DoesNotContain(context.Frames, frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
                AsyncProfilerStitchedFrame target = Assert.Single(context.Frames.Where(frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget));
                Assert.Equal(1, target.SegmentFrameIndex);
                Assert.Equal(1, target.Context.GetExceptionCompletedFrameCount(context.TimestampQpc));
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("task-result", false)]
        [InlineData("task-result", true)]
        [InlineData("task-chain", false)]
        [InlineData("fault", false)]
        [InlineData("fault-continuation", false)]
        [InlineData("cancellation", false)]
        [InlineData("cancellation-continuation", false)]
        [InlineData("value-result", false)]
        [InlineData("value-fault", false)]
        [InlineData("value-fault", true)]
        [InlineData("pooled-result", false)]
        [InlineData("pooled-fault", false)]
        [InlineData("task-registration", false)]
        [InlineData("pooled-registration", false)]
        [InlineData("pooled-registration-nested", false)]
        public void OptionalAsyncInfrastructure_GroupsAnchoredRuntimeInteriorsWithoutChangingLogicalFrames(
            string layout, bool runtimeAsync)
        {
            StitchScenario scenario = CreateOptionalAsyncInfrastructureScenario(layout, runtimeAsync);
            scenario.AssertTransformContext = context =>
            {
                AsyncProfilerStitchedFrame marker = Assert.Single(context.Frames.Where(frame =>
                    frame.Presentation == AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames));
                Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, marker.Origin);
                Assert.Equal(scenario.Sync[1].SymbolName, context.TraceLog.CodeAddresses[marker.CodeAddress].FullMethodName);
                AsyncProfilerStitchedFrame current = Assert.Single(context.Frames.Where(frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent));
                Assert.Same(current.Context, marker.Context);
                Assert.Equal(0, current.Context.GetCompletedFrameCount(context.TimestampQpc));
                Assert.Equal(0, current.SegmentFrameIndex);
                Assert.DoesNotContain(context.Frames, frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletionReturn);
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("task-result", false)]
        [InlineData("fault", true)]
        [InlineData("cancellation", true)]
        [InlineData("value-result", false)]
        [InlineData("value-fault", true)]
        [InlineData("pooled-result", false)]
        [InlineData("pooled-fault", true)]
        public void OptionalAsyncInfrastructure_SeparatesNormalAndExceptionalCompletion(string layout, bool exceptional)
        {
            StitchScenario scenario = CreateOptionalAsyncInfrastructureScenario(layout, false, cleanup: false);
            scenario.AssertTransformContext = context =>
            {
                var frames = context.Frames.ToList();
                var state = CreateOptionalInfrastructureTransformState(context, frames);
                int end = frames.FindIndex(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
                if (layout.StartsWith("pooled-", StringComparison.Ordinal))
                {
                    end--;
                }
                AsyncProfilerStitchedFrame root = frames[end - 1];
                Assert.True(state.GetRuntimeSupport(root.CodeAddress).TryGetOptionalSequenceKind(
                    AsyncProfilerMethodNames.NormalizeMethodName(
                        context.TraceLog.CodeAddresses[root.CodeAddress].FullMethodName),
                    root.SyncClassification, out var sequence));
                Assert.Equal(exceptional ? AsyncProfilerRuntimeSequenceKind.TaskExceptionCompletion :
                    AsyncProfilerRuntimeSequenceKind.TaskCompletion, sequence);
                Assert.False(state.MatchRuntimeSequence(sequence, 1, end).IsMatch);
                AsyncProfilerRuntimeSequenceMatch match = state.MatchRuntimeSequence(
                    sequence, 1, end, allowRuntimeInteriors: true);
                Assert.True(match.IsMatch);
                Assert.Equal(end - 1, match.Length);
                Assert.False(state.MatchRuntimeSequence(
                    exceptional ? AsyncProfilerRuntimeSequenceKind.TaskCompletion :
                        AsyncProfilerRuntimeSequenceKind.TaskExceptionCompletion,
                    1, end, allowRuntimeInteriors: true).IsMatch);
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("task-result", "application")]
        [InlineData("task-result", "native")]
        [InlineData("task-result", "foreign")]
        [InlineData("task-result", "unresolved")]
        [InlineData("task-result", "other-runtime")]
        [InlineData("fault", "application")]
        [InlineData("value-result", "application")]
        [InlineData("task-registration", "application")]
        [InlineData("pooled-registration", "application")]
        public void OptionalAsyncInfrastructure_PreservesInterruptedSpans(string layout, string obstruction)
        {
            StitchScenario scenario = CreateOptionalAsyncInfrastructureScenario(layout, false, cleanup: false);
            Frame original = scenario.Sync[2];
            Frame interruption = obstruction == "application" ? Frame.App("Scenario.Callback") :
                obstruction == "native" ? Frame.Library("coreclr", "Native.Callback", "NativeCallback") :
                obstruction == "foreign" ? Frame.Library("OtherRuntime", original.SymbolName, "ForeignHelper") :
                obstruction == "other-runtime"
                    ? Frame.Library(@"C:\OtherRuntime\System.Private.CoreLib", original.SymbolName, "OtherRuntimeHelper")
                    : original;
            scenario.Sync[2] = interruption;
            scenario.ExpectedStitched[2] = interruption;
            scenario.ExpectedWithoutInfrastructureCleanup[2] = interruption;
            if (obstruction == "unresolved")
            {
                scenario.OmitSymbolFor = interruption;
            }
            scenario.AssertTransformContext = context =>
            {
                var frames = context.Frames.ToList();
                AsyncProfilerStitchedFrame leaf = frames[1];
                AsyncProfilerStitchedFrame retained = frames[2];
                var state = CreateOptionalInfrastructureTransformState(context, frames);
                new AsyncProfilerInfrastructureCleanupTransformPass().Apply(state);
                Assert.Contains(retained, frames);
                Assert.Contains(frames, frame => frame.CodeAddress == leaf.CodeAddress);
                Assert.Equal(AsyncProfilerStitchedFramePresentation.Native,
                    frames.Single(frame => frame.CodeAddress == retained.CodeAddress).Presentation);
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("missing-placement")]
        [InlineData("incomplete-segments")]
        [InlineData("missing-boundary")]
        [InlineData("adjacency")]
        [InlineData("ambiguous")]
        [InlineData("wrong-context")]
        [InlineData("logical-frame")]
        [InlineData("profile")]
        [InlineData("missing-anchor")]
        public void OptionalAsyncInfrastructure_RequiresConsistentOwnershipAndBothAnchors(string obstruction)
        {
            StitchScenario scenario = CreateOptionalAsyncInfrastructureScenario("task-result", false, cleanup: false);
            scenario.AssertTransformContext = context =>
            {
                var frames = context.Frames.ToList();
                if (obstruction == "wrong-context")
                {
                    AsyncProfilerStitchedFrame helper = frames[2];
                    frames[2] = AsyncProfilerStitchedFrame.CreateSync(
                        new AsyncProfilerStitchSyncFrame(helper.CodeAddress, helper.Method, helper.SyncClassification),
                        context: null);
                }
                else if (obstruction == "logical-frame")
                {
                    frames[2] = AsyncProfilerStitchedFrame.CreateAsyncDispatchTarget(context.Segments[0], 0);
                }
                else if (obstruction == "missing-anchor")
                {
                    frames.RemoveAt(1);
                }
                AsyncProfilerStitchedFrame interior = frames[obstruction == "missing-anchor" ? 1 : 2];
                var state = CreateOptionalInfrastructureTransformState(context, frames,
                    includePlacement: obstruction != "missing-placement",
                    supportResolver: obstruction == "profile"
                        ? (Func<CodeAddressIndex, AsyncProfilerRuntimeSupport>)(ca =>
                            ca == interior.CodeAddress
                                ? AsyncProfilerRuntimeSupport.CoreClr11 : AsyncProfilerRuntimeSupport.Common) : null);
                if (obstruction == "missing-boundary")
                {
                    state.StitchingState.HasConsistentPhysicalAlignment = false;
                }
                if (obstruction == "incomplete-segments")
                {
                    state.StitchingState.ProcessedCallStackCount--;
                }
                if (obstruction == "adjacency")
                {
                    state.StitchingState.HasConsistentPhysicalAlignment = false;
                }
                if (obstruction == "ambiguous")
                {
                    state.StitchingState.UnresolvedV1CompletionCount++;
                }
                new AsyncProfilerInfrastructureCleanupTransformPass().Apply(state);
                Assert.Contains(interior, frames);
                Assert.Equal(0, state.Diagnostics.ProfilerEnvelopeFramesCollapsed);
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("task-result")]
        [InlineData("task-chain")]
        [InlineData("fault")]
        [InlineData("value-result")]
        [InlineData("task-registration")]
        [InlineData("pooled-registration")]
        public void OptionalAsyncInfrastructure_DoesNotChangeStrictMatchingAndHonorsSupportVetoes(string layout)
        {
            StitchScenario scenario = CreateOptionalAsyncInfrastructureScenario(layout, false, cleanup: false);
            scenario.AssertTransformContext = context =>
            {
                var frames = context.Frames.ToList();
                var state = CreateOptionalInfrastructureTransformState(context, frames);
                int end = frames.FindIndex(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
                AsyncProfilerStitchedFrame root = frames[end - 1];
                string rootName = context.TraceLog.CodeAddresses[root.CodeAddress].FullMethodName;
                if (layout.Contains("registration"))
                {
                    end--;
                    root = frames[end - 1];
                    rootName = context.TraceLog.CodeAddresses[root.CodeAddress].FullMethodName;
                }
                Assert.True(state.GetRuntimeSupport(root.CodeAddress).TryGetOptionalSequenceKind(
                    AsyncProfilerMethodNames.NormalizeMethodName(rootName), root.SyncClassification, out var sequence));
                Assert.False(state.MatchRuntimeSequence(sequence, 1, end).IsMatch);
                AsyncProfilerRuntimeSequenceMatch match = state.MatchRuntimeSequence(
                    sequence, 1, end, allowRuntimeInteriors: true);
                Assert.True(match.IsMatch);
                Assert.Equal(end - 1, match.Length);
                var support = new RejectingOptionalInfrastructureSupport();
                state = CreateOptionalInfrastructureTransformState(context, frames, supportResolver: ca => support);
                Assert.True(state.MatchRuntimeSequence(sequence, 1, end, allowRuntimeInteriors: true).IsRejected);
                new AsyncProfilerInfrastructureCleanupTransformPass().Apply(state);
                Assert.Equal(context.Frames, frames);
                Assert.Equal(0, state.Diagnostics.AsyncInfrastructureFramesCollapsed);
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("fault", "System.Threading.Tasks.Task.TrySetCanceled()")]
        [InlineData("cancellation", "System.Threading.Tasks.Task.TrySetException(System.Object)")]
        [InlineData("task-registration", "System.Threading.Tasks.Task.OtherRegistrationHelper()")]
        [InlineData("pooled-registration", "System.Runtime.CompilerServices.OtherSourceBox.OnCompleted()")]
        public void OptionalAsyncInfrastructure_RequiresTheMatchingTransitionOrRegistrationAnchor(
            string layout, string transition)
        {
            StitchScenario scenario = CreateOptionalAsyncInfrastructureScenario(layout, false, cleanup: false);
            int transitionIndex = layout.Contains("registration") ? 2 : 3;
            Frame replacement = Frame.CoreLib(transition, "MismatchedTransition");
            scenario.Sync[transitionIndex] = replacement;
            scenario.ExpectedStitched[transitionIndex] = replacement;
            scenario.ExpectedWithoutInfrastructureCleanup[transitionIndex] = replacement;
            scenario.AssertTransformContext = context =>
            {
                var frames = context.Frames.ToList();
                var state = CreateOptionalInfrastructureTransformState(context, frames);
                int end = frames.FindIndex(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
                if (layout.Contains("registration"))
                {
                    end--;
                }
                AsyncProfilerStitchedFrame root = frames[end - 1];
                Assert.True(state.GetRuntimeSupport(root.CodeAddress).TryGetOptionalSequenceKind(
                    AsyncProfilerMethodNames.NormalizeMethodName(
                        context.TraceLog.CodeAddresses[root.CodeAddress].FullMethodName),
                    root.SyncClassification, out var sequence));
                Assert.False(state.MatchRuntimeSequence(sequence, 1, end, allowRuntimeInteriors: true).IsMatch);
                new AsyncProfilerInfrastructureCleanupTransformPass().Apply(state);
                Assert.Equal(AsyncProfilerStitchedFramePresentation.Native,
                    frames.Single(frame => frame.CodeAddress == context.Frames[1].CodeAddress).Presentation);
            };
            scenario.AssertProductionStitch();
        }

        [Fact]
        public void OptionalAsyncInfrastructure_DoesNotUseValueTaskSourceCompletionForATaskCompletionChain()
        {
            StitchScenario scenario = CreateOptionalAsyncInfrastructureScenario("value-result", false, cleanup: false);
            Frame trigger = Frame.CoreLib(
                "System.Threading.Tasks.TaskCompletionSource`1[System.Int64].SetResult(!0)", "Source.SetResult");
            scenario.Sync = scenario.Sync.Take(4).Concat(new[] { trigger }).Concat(scenario.Sync.Skip(4)).ToArray();
            scenario.ExpectedStitched = scenario.ExpectedStitched.Take(4).Concat(new[] { trigger })
                .Concat(scenario.ExpectedStitched.Skip(4)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = scenario.ExpectedStitched;
            scenario.AssertTransformContext = context =>
            {
                var frames = context.Frames.ToList();
                var state = CreateOptionalInfrastructureTransformState(context, frames);
                Assert.False(state.MatchRuntimeSequence(AsyncProfilerRuntimeSequenceKind.TaskCompletionChain,
                    1, 5, allowRuntimeInteriors: true).IsMatch);
                new AsyncProfilerInfrastructureCleanupTransformPass().Apply(state);
                Assert.Equal(AsyncProfilerStitchedFramePresentation.Native,
                    frames.Single(frame => context.TraceLog.CodeAddresses[frame.CodeAddress].FullMethodName ==
                        trigger.SymbolName).Presentation);
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("application")]
        [InlineData("native")]
        [InlineData("foreign")]
        [InlineData("unresolved")]
        public void V1ExceptionMachinery_DoesNotCrossCallbacksOrUnprovenFrames(string obstruction)
        {
            StitchScenario scenario = CreateV1ExceptionMachineryScenario(false);
            Frame interruption = obstruction == "application" ? Frame.App("Scenario.ExceptionHandlerCallback") :
                obstruction == "native" ? Frame.Library("coreclr", "OtherNativeCallback", "NativeCallback") :
                obstruction == "foreign" ? Frame.Library("OtherRuntime", "System.Threading.Tasks.Task.AddException", "ForeignHelper") :
                Frame.CoreLib("System.Runtime.CompilerServices.TaskAwaiter.GetResult", "UnresolvedHelper");
            if (obstruction == "unresolved")
            {
                scenario.OmitSymbolFor = interruption;
            }
            scenario.Sync = scenario.Sync.Take(3).Concat(new[] { interruption }).Concat(scenario.Sync.Skip(3)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = scenario.ExpectedWithoutInfrastructureCleanup.Take(3)
                .Concat(new[] { interruption }).Concat(scenario.ExpectedWithoutInfrastructureCleanup.Skip(3)).ToArray();
            scenario.ExpectedWithoutNormalization = scenario.ExpectedWithoutNormalization.Take(3)
                .Concat(new[] { interruption }).Concat(scenario.ExpectedWithoutNormalization.Skip(3)).ToArray();
            scenario.ExpectedStitched = scenario.Sync.Take(4)
                .Concat(new[] { scenario.Sync[4].DispatcherFrames() })
                .Concat(scenario.ExpectedStitched.Skip(2)).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 6;
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = 2;
            scenario.AssertProductionStitch();
        }

        [Fact]
        public void V1ExceptionMachinery_StandaloneFaultRecordingDoesNotStartAGroup()
        {
            StitchScenario scenario = CreateV1ExceptionMachineryScenario(false);
            scenario.Sync = scenario.Sync.Where((frame, i) => i != 4).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = scenario.ExpectedWithoutInfrastructureCleanup
                .Where((frame, i) => i != 4).ToArray();
            scenario.ExpectedWithoutNormalization = scenario.ExpectedWithoutNormalization
                .Where((frame, i) => i != 4).ToArray();
            scenario.ExpectedStitched = scenario.Sync.Take(4)
                .Concat(scenario.ExpectedStitched.Skip(2).Where(frame =>
                    frame.Label != "[Async dispatcher frames]")).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 2;
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = 0;
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void V1CompletionMachinery_FoldsOwnedTaskAwaiterAndYieldWithoutChangingCompletionRoles(
            bool enableNormalization)
        {
            StitchScenario scenario = CreateV1CompletionMachineryScenario();
            scenario.EnableNormalization = enableNormalization;
            scenario.AssertTransformContext = context =>
            {
                AsyncProfilerStitchedFrame completing = Assert.Single(context.Frames.Where(frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody));
                AsyncProfilerStitchedFrame marker = Assert.Single(context.Frames.Where(frame =>
                    frame.Presentation == AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames));
                Assert.Same(completing.Context, marker.Context);
                Assert.Contains("TaskAwaiter+", context.TraceLog.CodeAddresses[marker.CodeAddress].FullMethodName);
                Assert.Equal(0, completing.Segment.FrameStateAt(completing.SegmentFrameIndex));
                Assert.True(context.HasVerifiedV1CompletionAnchor(completing.Context, marker.CodeAddress));
                Assert.False(context.HasVerifiedDispatchEntry(completing.Context, marker.CodeAddress));
                Assert.False(AsyncProfilerCallStackTransformHelpers.HasVerifiedAsyncAnchor(
                    context, context.Frames.IndexOf(completing), completing.Context));
            };
            scenario.AssertStitchedStackSource = source =>
            {
                var completing = new List<AsyncProfilerCallStackSourceFrameInfo>();
                for (int i = 0; i < source.CallFrameIndexLimit; i++)
                {
                    if (source.TryGetAsyncProfilerCallStackFrameInfo((StackSourceFrameIndex)i, out var info) &&
                        info.IsCompleting)
                    {
                        completing.Add(info);
                        Assert.Equal(info.CodeAddress, source.GetFrameCodeAddress((StackSourceFrameIndex)i));
                        Assert.False(info.IsCurrent);
                        Assert.False(info.IsDispatchTarget);
                        Assert.Equal(0, info.State);
                    }
                }
                Assert.Single(completing);
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("application")]
        [InlineData("foreign")]
        [InlineData("unresolved")]
        public void V1CompletionMachinery_TaskAwaiterFamilyDoesNotRestartAboveABreak(string obstruction)
        {
            StitchScenario scenario = CreateV1CompletionMachineryScenario();
            Frame outer = Frame.CoreLib("System.Runtime.CompilerServices.TaskAwaiter.GetResult()", "TaskAwaiter.GetResult");
            Frame interruption = obstruction == "application" ? Frame.App("Scenario.Callback") :
                obstruction == "foreign" ? Frame.Library("OtherRuntime", "Helper.Invoke()", "ForeignHelper") :
                Frame.CoreLib("System.Runtime.CompilerServices.TaskAwaiter+OtherHelper.Invoke()", "UnresolvedHelper");
            if (obstruction == "unresolved")
            {
                scenario.OmitSymbolFor = interruption;
            }
            Frame[] prefix = { scenario.Sync[0], outer, interruption };
            scenario.Sync = prefix.Concat(scenario.Sync.Skip(1)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup =
                prefix.Concat(scenario.ExpectedWithoutInfrastructureCleanup.Skip(1)).ToArray();
            scenario.ExpectedStitched = prefix.Concat(scenario.ExpectedStitched.Skip(1)).ToArray();
            scenario.AssertProductionStitch();
        }

        [Fact]
        public void V1CompletionMachinery_TaskAwaiterApplicationWorkWithoutAnEntryIsNotGrouped()
        {
            StitchScenario scenario = CreateYieldRuntimeBridgeScenario(AsyncCallstackKind.StateMachineAsync,
                Frame.CoreLib("System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter.Callback()", "Yield.Callback"));
            Frame work = Frame.CoreLib("System.Runtime.CompilerServices.TaskAwaiter.GetResult()", "TaskAwaiter.GetResult");
            scenario.Sync[0] = work;
            scenario.ExpectedStitched[0] = work;
            scenario.ExpectedWithoutInfrastructureCleanup[0] = work;
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("foreign")]
        [InlineData("lookalike")]
        [InlineData("unresolved")]
        public void V1CompletionMachinery_RequiresResolvedOwnedTaskAwaiterFamily(string obstruction)
        {
            StitchScenario scenario = CreateV1CompletionMachineryScenario();
            Frame callback = scenario.Sync[1];
            Frame retained = obstruction == "foreign"
                ? Frame.Library("OtherRuntime", callback.SymbolName, "ForeignTaskAwaiter") :
                obstruction == "lookalike"
                    ? Frame.CoreLib(callback.SymbolName.Replace("TaskAwaiter+", "TaskAwaiterLookalike+"), "LookalikeTaskAwaiter")
                    : callback;
            scenario.Sync[1] = retained;
            scenario.ExpectedWithoutInfrastructureCleanup[1] = retained;
            if (obstruction == "unresolved")
            {
                scenario.OmitSymbolFor = callback;
            }
            scenario.ExpectedStitched = new[] { scenario.Sync[0], retained, scenario.Sync[2].DispatcherFrames() }
                .Concat(scenario.ExpectedStitched.Skip(2)).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 10;
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = 8;
            scenario.AssertProductionStitch();
        }

        [Fact]
        public void V1CompletionMachinery_RespectsCompletionChainSupportRejection()
        {
            StitchScenario scenario = CreateV1CompletionMachineryScenario();
            scenario.EnableInfrastructureCleanup = false;
            scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;
            var support = new RejectingCompletionChainSupport();
            scenario.AssertTransformContext = context =>
            {
                var frames = context.Frames.ToList();
                AsyncProfilerCallStack callStack = Assert.Single(context.Segments);
                CodeAddressIndex dispatch = Enumerable.Range(0, context.TraceLog.CodeAddresses.Count)
                    .Select(i => (CodeAddressIndex)i).Single(ca =>
                        context.ClassifyBoundary(ca).Kind == AsyncProfilerCallStackStitchBoundaryKind.V1Dispatcher);
                var placements = new[] { new AsyncProfilerCallStackPlacement(callStack, 0, frames.Count, dispatch: dispatch) };
                var diagnostics = new AsyncProfilerCallStackDiagnostics { SegmentsProcessed = 1 };
                var state = new AsyncProfilerCallStackTransformState(
                    context.TraceLog, context.Segments, context.TimestampQpc, context.ClassifyBoundary, context.StitchingState.Copy(), diagnostics, frames,
                    runtimeSupportResolver: ca => context.GetHostModuleOwner(ca) != ModuleFileIndex.Invalid
                        ? support : AsyncProfilerRuntimeSupport.Common,
                    segmentPlacements: placements);
                CodeAddressIndex helper = frames.Single(frame =>
                    frame.SyncFrameKind == AsyncProfilerStitchSyncFrameKind.V1MethodCompletion).CodeAddress;
                Assert.True(state.HasVerifiedV1CompletionAnchor(callStack, helper));
                new AsyncProfilerInfrastructureCleanupTransformPass().Apply(state);
                Assert.Equal(0, diagnostics.ProfilerEnvelopeFramesCollapsed);
                Assert.Equal(2, diagnostics.AsyncInfrastructureFramesCollapsed);
                Assert.Equal(1, diagnostics.RuntimeResumptionBridgeFramesCollapsed);
                foreach (FrameClassification kind in new FrameClassification[]
                {
                    RuntimeFrameKind.TaskContinuations,
                    RuntimeFrameKind.TaskResult,
                    AsyncProfilerStitchSyncFrameKind.V1MethodCompletion,
                })
                {
                    Assert.Equal(AsyncProfilerStitchedFramePresentation.Native,
                        Assert.Single(frames.Where(frame => frame.SyncClassification == kind)).Presentation);
                }
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("unresolved-physical")]
        [InlineData("wrong-method")]
        [InlineData("wrong-index")]
        [InlineData("dispatch-target")]
        [InlineData("parent-current")]
        [InlineData("unwind")]
        [InlineData("missing-boundary")]
        [InlineData("adjacency")]
        [InlineData("ambiguous")]
        [InlineData("missing-placement")]
        public void V1CompletionMachinery_RequiresPhysicalCompletionAndConsistentStructuralEvidence(string obstruction)
        {
            StitchScenario scenario = CreateV1CompletionMachineryScenario();
            scenario.AssertTransformContext = context =>
            {
                int index = context.Frames.FindIndex(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody);
                AsyncProfilerStitchedFrame original = context.Frames[index];
                CodeAddressIndex bridge = context.Frames[index - 1].CodeAddress;
                Assert.True(context.HasVerifiedV1CompletionAnchor(original.Context, bridge));
                var frames = context.Frames.ToList();
                var state = CreateOptionalInfrastructureTransformState(context, frames,
                    includePlacement: obstruction != "missing-placement");
                if (obstruction == "unresolved-physical" || obstruction == "wrong-method" || obstruction == "wrong-index")
                {
                    frames[index] = AsyncProfilerStitchedFrame.CreateV1CompletedBody(
                        obstruction == "unresolved-physical" ? CodeAddressIndex.Invalid : original.CodeAddress,
                        obstruction == "wrong-method"
                            ? context.TraceLog.CodeAddresses.MethodIndex(original.Segment.CodeAddressAt(1)) : original.Method,
                        original.Context, obstruction == "wrong-index" ? 1 : original.SegmentFrameIndex);
                }
                else if (obstruction == "dispatch-target")
                {
                    frames[index] = AsyncProfilerStitchedFrame.CreateAsyncDispatchTarget(
                        original.Context, original.SegmentFrameIndex);
                }
                else if (obstruction == "parent-current")
                {
                    frames[index] = AsyncProfilerStitchedFrame.CreateAsyncContextParent(
                        original.Context, original.SegmentFrameIndex, isCurrent: true);
                }
                else if (obstruction == "unwind")
                {
                    frames[index] = AsyncProfilerStitchedFrame.CreateV1CompletionReturn(original.Context);
                }
                else if (obstruction == "missing-boundary" || obstruction == "adjacency")
                {
                    state.StitchingState.HasConsistentPhysicalAlignment = false;
                }
                else if (obstruction == "ambiguous")
                {
                    state.StitchingState.UnresolvedV1CompletionCount++;
                }
                Assert.False(state.HasVerifiedV1CompletionAnchor(original.Context, bridge));
                Assert.False(state.HasVerifiedV1CompletionAnchor(
                    original.Context, bridge, allowCompletionUnwind: true));
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void V1CompletionMachinery_UnverifiedStateCannotBeAuthorizedByDiagnostics(bool clearState)
        {
            StitchScenario scenario = CreateV1CompletionMachineryScenario();
            scenario.AssertTransformContext = context =>
            {
                var frames = context.Frames.ToList();
                AsyncProfilerCallStack callStack = Assert.Single(context.Segments);
                CodeAddressIndex dispatch = Enumerable.Range(0, context.TraceLog.CodeAddresses.Count)
                    .Select(i => (CodeAddressIndex)i).Single(ca =>
                        context.ClassifyBoundary(ca).Kind == AsyncProfilerCallStackStitchBoundaryKind.V1Dispatcher);
                var stitchingState = clearState
                    ? context.StitchingState.Copy()
                    : new AsyncProfilerCallStackStitchingState();
                if (clearState)
                {
                    stitchingState.Clear();
                }
                stitchingState.ProcessedCallStackCount = context.Segments.Count;
                var diagnostics = new AsyncProfilerCallStackDiagnostics { SegmentsProcessed = context.Segments.Count };
                var state = new AsyncProfilerCallStackTransformState(
                    context.TraceLog, context.Segments, context.TimestampQpc, context.ClassifyBoundary,
                    stitchingState, diagnostics, frames,
                    segmentPlacements: new[] { new AsyncProfilerCallStackPlacement(callStack, 0, frames.Count, dispatch: dispatch) });
                CodeAddressIndex bridge = frames.First(frame => frame.Context == null).CodeAddress;

                Assert.True(context.HasVerifiedV1CompletionAnchor(callStack, bridge));
                Assert.False(state.HasVerifiedV1CompletionAnchor(callStack, bridge));
            };
            scenario.AssertProductionStitch();
        }

        [Fact]
        public void V1CompletionMachinery_PipelineHelpersAndCleanupIgnoreDiagnosticCounters()
        {
            StitchScenario scenario = CreateV1CompletionMachineryScenario();
            scenario.EnableInfrastructureCleanup = false;
            scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;
            scenario.AssertTransformContext = context =>
            {
                AsyncProfilerCallStack callStack = Assert.Single(context.Segments);
                AsyncProfilerStitchedFrame[] expected = null;
                for (int diagnosticMode = 0; diagnosticMode < 3; diagnosticMode++)
                {
                    var frames = context.Frames.ToList();
                    var state = CreateOptionalInfrastructureTransformState(context, frames);
                    if (diagnosticMode == 0)
                    {
                        state.Diagnostics.SegmentsProcessed = context.Segments.Count;
                    }
                    else if (diagnosticMode == 1)
                    {
                        state.Diagnostics.Clear();
                    }
                    else
                    {
                        state.Diagnostics.SegmentsProcessed = 50;
                        state.Diagnostics.BoundariesNotFound = 50;
                        state.Diagnostics.AdjacencyMismatches = 50;
                        state.Diagnostics.V1CompletionAlignmentAmbiguous = 50;
                        state.Diagnostics.V2SyncLayoutUsed = 50;
                    }
                    CodeAddressIndex bridge = frames.First(frame => frame.Context == null).CodeAddress;
                    Assert.True(state.HasVerifiedV1CompletionAnchor(callStack, bridge));

                    var pipeline = new AsyncProfilerCallStackTransformPipeline
                    {
                        EnableNormalization = false,
                        EnableInfrastructureCleanup = true,
                    };
                    pipeline.ApplyInPlace(state);

                    Assert.True(state.Diagnostics.ProfilerEnvelopeFramesCollapsed > 0);
                    Assert.True(state.Diagnostics.RuntimeResumptionBridgeFramesCollapsed > 0);
                    if (expected == null)
                    {
                        expected = frames.ToArray();
                        Assert.True(expected.Length < context.Frames.Count);
                    }
                    else
                    {
                        Assert.Equal(expected, frames);
                    }
                }
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("owned", false)]
        [InlineData("owned", true)]
        [InlineData("application", true)]
        [InlineData("foreign", true)]
        [InlineData("unresolved", true)]
        public void V1CompletionMachinery_IndependentPairGroupsOwnedCallbacksWithoutCrossingWhenAll(
            string callbackOwner, bool enableNormalization)
        {
            StitchScenario scenario = CreateV1CompletionMachineryScenario();
            scenario.EnableNormalization = enableNormalization;
            Frame box = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncStateMachineBox.MoveNext()",
                "OtherBox.MoveNext");
            Frame whenAll = Frame.CoreLib(
                "System.Threading.Tasks.Task+WhenAllPromise`1[System.Int64].Invoke(class System.Threading.Tasks.Task)",
                "WhenAllPromise.Invoke");
            Frame[] original = scenario.Sync.Take(6).ToArray();
            Frame callback = callbackOwner == "application" ? Frame.App("Scenario.SynchronousCallback") :
                callbackOwner == "foreign" ? Frame.Library("OtherRuntime", original[1].SymbolName, "ForeignCallback") :
                original[1];
            if (callbackOwner == "unresolved")
            {
                scenario.OmitSymbolFor = callback;
            }
            Frame[] prefix =
            {
                original[0], box, callback, original[2], original[3], original[4],
                whenAll, original[3], original[4], original[5],
            };
            scenario.Sync = prefix.Concat(scenario.Sync.Skip(6)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = prefix
                .Concat(scenario.ExpectedWithoutInfrastructureCleanup.Skip(6)).ToArray();
            Frame[] grouped = callbackOwner == "owned" ? new[]
            {
                original[0], box.DispatcherFrames(), whenAll, original[3].DispatcherFrames(),
            } : new[]
            {
                original[0], box.DispatcherFrames(), callback, original[2].DispatcherFrames(),
                whenAll, original[3].DispatcherFrames(),
            };
            scenario.ExpectedStitched = grouped.Concat(scenario.ExpectedStitched.Skip(2)).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = callbackOwner == "owned" ? 18 : 16;
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = callbackOwner == "owned" ? 12 : 8;
            scenario.AssertProductionStitch();
        }

        [Fact]
        public void V1CompletionMachinery_UsesTheStructuralCanonicalMethodResolver()
        {
            StitchScenario scenario = CreateV1CompletionMachineryScenario();
            scenario.AssertTransformContext = context =>
            {
                var frames = context.Frames.ToList();
                int index = frames.FindIndex(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody);
                AsyncProfilerStitchedFrame original = frames[index];
                AsyncProfilerCallStack callStack = original.Context;
                CodeAddressIndex captured = callStack.Frames.CodeAddressAt(original.SegmentFrameIndex);
                MethodIndex canonical = context.TraceLog.CodeAddresses.MethodIndex(callStack.Frames.CodeAddressAt(1));
                Assert.NotEqual(canonical, context.TraceLog.CodeAddresses.MethodIndex(original.CodeAddress));
                frames[index] = AsyncProfilerStitchedFrame.CreateV1CompletedBody(
                    original.CodeAddress, canonical, callStack, original.SegmentFrameIndex);
                CodeAddressIndex dispatch = Enumerable.Range(0, context.TraceLog.CodeAddresses.Count)
                    .Select(i => (CodeAddressIndex)i).Single(ca =>
                        context.ClassifyBoundary(ca).Kind == AsyncProfilerCallStackStitchBoundaryKind.V1Dispatcher);
                var placements = new[] { new AsyncProfilerCallStackPlacement(callStack, 0, frames.Count, dispatch: dispatch) };
                var diagnostics = new AsyncProfilerCallStackDiagnostics { SegmentsProcessed = 1 };
                var raw = new AsyncProfilerCallStackTransformState(
                    context.TraceLog, context.Segments, context.TimestampQpc, context.ClassifyBoundary, context.StitchingState.Copy(), diagnostics, frames,
                    segmentPlacements: placements);
                var normalized = new AsyncProfilerCallStackTransformState(
                    context.TraceLog, context.Segments, context.TimestampQpc, context.ClassifyBoundary, context.StitchingState.Copy(), diagnostics, frames,
                    segmentPlacements: placements,
                    methodOf: ca => ca == original.CodeAddress || ca == captured ? canonical : context.GetMethod(ca));
                Assert.False(raw.HasVerifiedV1CompletionAnchor(callStack, frames[index - 1].CodeAddress));
                Assert.True(normalized.HasVerifiedV1CompletionAnchor(callStack, frames[index - 1].CodeAddress));
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("foreign-yield")]
        [InlineData("unresolved-yield")]
        [InlineData("interrupted")]
        [InlineData("foreign-anchor")]
        public void V1CompletionMachinery_RejectsUnownedOrInterruptedYieldBridges(string obstruction)
        {
            StitchScenario scenario = CreateV1CompletionMachineryScenario();
            int yieldIndex = Array.IndexOf(scenario.Sync, Frame.ThreadPoolDispatch) - 1;
            Frame yield = scenario.Sync[yieldIndex];
            Frame[] tail = scenario.Sync.Skip(yieldIndex).ToArray();
            if (obstruction == "foreign-yield")
            {
                tail[0] = Frame.Library("OtherRuntime", yield.SymbolName, "ForeignYield");
            }
            else if (obstruction == "unresolved-yield")
            {
                scenario.OmitSymbolFor = yield;
            }
            else if (obstruction == "interrupted")
            {
                tail = new[] { yield, Frame.App("Scenario.Callback") }.Concat(tail.Skip(1)).ToArray();
            }
            else
            {
                tail[1] = Frame.Library("OtherRuntime", Frame.ThreadPoolDispatch.SymbolName, "ForeignDispatch");
            }
            scenario.Sync = scenario.Sync.Take(yieldIndex).Concat(tail).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = scenario.ExpectedWithoutInfrastructureCleanup
                .Take(scenario.ExpectedWithoutInfrastructureCleanup.Length - 3).Concat(tail).ToArray();
            scenario.ExpectedStitched = scenario.ExpectedStitched.Take(4).Concat(tail).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 10;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;
            scenario.AssertProductionStitch();
        }

        #region private

        private static StitchScenario CreateOptionalAsyncInfrastructureScenario(
            string layout, bool runtimeAsync, bool cleanup = true)
        {
            Frame run = Frame.CoreLib("System.Threading.Tasks.Task.RunContinuations(System.Object)", "Task.RunContinuations");
            Frame finish = Frame.CoreLib("System.Threading.Tasks.Task.FinishContinuations()", "Task.FinishContinuations");
            Frame result = Frame.CoreLib("System.Threading.Tasks.Task`1[System.Int64].TrySetResult(!0)", "Task.TrySetResult");
            Frame fault = Frame.CoreLib("System.Threading.Tasks.Task.TrySetException(System.Object)", "Task.TrySetException");
            Frame cancel = Frame.CoreLib("System.Threading.Tasks.Task.TrySetCanceled()", "Task.TrySetCanceled");
            Frame builderFault = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].SetException(System.Exception)",
                "Builder.SetException");
            Frame builderAwait = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].AwaitUnsafeOnCompleted(!!0&,!!1&)",
                "Builder.AwaitUnsafeOnCompleted");
            const string source = "System.Threading.Tasks.Sources.ManualResetValueTaskSourceCore`1[System.Int64]";
            Frame[] prefix;
            switch (layout)
            {
                case "task-result":
                    prefix = new[] { run, finish, result };
                    break;
                case "task-chain":
                    prefix = new[] { run, finish, result,
                        Frame.CoreLib("System.Threading.Tasks.TaskCompletionSource`1[System.Int64].SetResult(!0)", "Source.SetResult") };
                    break;
                case "fault":
                case "cancellation":
                    prefix = new[]
                    {
                        Frame.CoreLib("System.Threading.Tasks.TaskExceptionHolder." +
                            (layout == "fault" ? "AddFaultException" : "SetCancellationException") + "(System.Object)", "Holder.Record"),
                        Frame.CoreLib("System.Threading.Tasks.TaskExceptionHolder.Add(System.Object,System.Boolean)", "Holder.Add"),
                        layout == "fault" ? fault : cancel,
                        builderFault,
                    };
                    break;
                case "fault-continuation":
                case "cancellation-continuation":
                    prefix = new[] { run, finish, layout == "fault-continuation" ? fault : cancel, builderFault };
                    break;
                case "value-result":
                case "value-fault":
                case "pooled-result":
                case "pooled-fault":
                    prefix = new[]
                    {
                        Frame.CoreLib(
                            "System.Threading.Tasks.Sources.ManualResetValueTaskSourceCoreShared.InvokeContinuationWithContext()",
                            "Source.InvokeContinuation"),
                        Frame.CoreLib(source + ".SignalCompletion()", "Source.SignalCompletion"),
                        Frame.CoreLib(source + (layout.EndsWith("result", StringComparison.Ordinal)
                            ? ".SetResult(!0)" : ".SetException(System.Exception)"),
                            "Source.Complete"),
                    };
                    if (layout.StartsWith("pooled-", StringComparison.Ordinal))
                    {
                        string completion = layout == "pooled-result" ? "SetResult(!0)" : "SetException(System.Exception)";
                        prefix = prefix.Concat(new[]
                        {
                            Frame.CoreLib("System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1[System.Int64]+" +
                                "StateMachineBox." + completion, "Box.Complete"),
                            Frame.CoreLib("System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1[System.Int64]." +
                                completion, "Builder.Complete"),
                        }).ToArray();
                    }
                    break;
                case "task-registration":
                    prefix = new[]
                    {
                        Frame.CoreLib("System.Threading.Tasks.Task.AddTaskContinuation(System.Object,System.Boolean)", "Task.AddContinuation"),
                        Frame.CoreLib("System.Threading.Tasks.Task.UnsafeSetContinuationForAwait()", "Task.RegisterContinuation"),
                        Frame.CoreLib("System.Void System.Runtime.CompilerServices.TaskAwaiter::UnsafeOnCompletedInternal()", "Awaiter.Register"),
                        builderAwait,
                    };
                    break;
                case "pooled-registration":
                case "pooled-registration-nested":
                    prefix = new[]
                    {
                        Frame.CoreLib(source + ".OnCompleted()", "Source.OnCompleted"),
                        Frame.CoreLib("System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1" +
                            (layout == "pooled-registration" ? "[System.Int64]+StateMachineBox" : "+StateMachineBox[System.Int64]") +
                            ".OnCompleted()", "Box.OnCompleted"),
                        Frame.CoreLib("System.Runtime.CompilerServices.ValueTaskAwaiter`1[System.Int64]." +
                            "System.Runtime.CompilerServices.IStateMachineBoxAwareAwaiter.AwaitUnsafeOnCompleted()", "Awaiter.Register"),
                        builderAwait,
                    };
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(layout));
            }
            StitchScenario scenario = CreateYieldRuntimeBridgeScenario(runtimeAsync
                ? AsyncCallstackKind.RuntimeAsync : AsyncCallstackKind.StateMachineAsync);
            Frame callee = Frame.App("Scenario.ObservedCallback");
            Frame[] logicalTail = scenario.ExpectedStitched.Skip(1).ToArray();
            scenario.Sync = new[] { callee }.Concat(prefix).Concat(scenario.Sync.Skip(1)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = new[] { callee }.Concat(prefix).Concat(logicalTail).ToArray();
            scenario.ExpectedStitched = cleanup
                ? new[] { callee, prefix[0].DispatcherFrames() }.Concat(logicalTail).ToArray()
                : scenario.ExpectedWithoutInfrastructureCleanup.ToArray();
            scenario.EnableInfrastructureCleanup = cleanup;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = cleanup ? prefix.Length * 2 : 0;
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = cleanup
                ? (layout.Contains("registration") ? prefix.Length :
                    layout.StartsWith("pooled-", StringComparison.Ordinal) ? prefix.Length - 2 : prefix.Length - 1) * 2 : 0;
            scenario.AllowSupplementalSamples = true;
            return scenario;
        }

        private static AsyncProfilerCallStackTransformState CreateOptionalInfrastructureTransformState(
            AsyncProfilerCallStackTransformState context, List<AsyncProfilerStitchedFrame> frames,
            bool includePlacement = true, Func<CodeAddressIndex, AsyncProfilerRuntimeSupport> supportResolver = null)
        {
            AsyncProfilerCallStack callStack = Assert.Single(context.Segments);
            CodeAddressIndex dispatch = Enumerable.Range(0, context.TraceLog.CodeAddresses.Count)
                .Select(i => (CodeAddressIndex)i).Single(ca =>
                    context.ClassifyBoundary(ca).Kind == AsyncProfilerCallStackStitchBoundaryKind.V1Dispatcher);
            return new AsyncProfilerCallStackTransformState(
                context.TraceLog, context.Segments, context.TimestampQpc, context.ClassifyBoundary,
                context.StitchingState.Copy(),                 new AsyncProfilerCallStackDiagnostics(), frames,
                runtimeSupportResolver: supportResolver,
                segmentPlacements: includePlacement
                    ? new[] { new AsyncProfilerCallStackPlacement(callStack, 0, frames.Count, dispatch: dispatch) } : null);
        }

        private static StitchScenario CreateV1ExceptionMachineryScenario(bool inline)
        {
            StitchScenario scenario = CreateYieldRuntimeBridgeScenario(AsyncCallstackKind.StateMachineAsync);
            Frame body = scenario.Sync[1];
            Frame native = Frame.Library("coreclr", "MethodDesc.PrepareCode", "NativeWork");
            Frame exceptionDispatch = Frame.Library("coreclr", "CallCatchFunclet", "ExceptionDispatch");
            Frame[] prefix = inline ? new[]
            {
                Frame.CoreLib(
                    "System.Runtime.CompilerServices.TaskAwaiter+<>c.<OutputWaitEtwEvents>b__12_0(System.Action,System.Threading.Tasks.Task)",
                    "TaskAwaiter.Correlation"),
                Frame.CoreLib("System.Threading.Tasks.AwaitTaskContinuation.RunOrScheduleAction", "Continuation.Run"),
                Frame.CoreLib("System.Threading.Tasks.Task.RunContinuations", "Task.RunContinuations"),
                Frame.CoreLib("System.Threading.Tasks.Task.TrySetException(System.Object)", "Task.TrySetException"),
            } : new[]
            {
                Frame.CoreLib("System.Threading.Tasks.TaskExceptionHolder.AddFaultException(System.Object)", "Holder.AddFaultException"),
                Frame.CoreLib("System.Threading.Tasks.Task.AddException(System.Object,System.Boolean)", "Task.AddException"),
                Frame.CoreLib("System.Threading.Tasks.Task.TrySetException(System.Object)", "Task.TrySetException"),
            };
            Frame builder = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].SetException(System.Exception)",
                "Builder.SetException");
            Frame logicalBody = body.Logical("Scenario.Level2Async");
            Frame parent = scenario.Async[1];
            Frame[] physical = { body, exceptionDispatch, body };
            Frame[] logical = { logicalBody, exceptionDispatch, logicalBody, Frame.V1MoveNextAsDispatcher, parent };
            Frame[] tail = { Frame.ThreadPoolDispatch, Frame.WorkerThreadStart };
            scenario.Sync = new[] { native }.Concat(prefix).Concat(new[] { builder }).Concat(physical)
                .Concat(scenario.Sync.Skip(2)).ToArray();
            scenario.ExceptionCompletions = 1;
            scenario.ExpectedStitched = new[] { native, prefix[0].DispatcherFrames() }
                .Concat(new[] { logicalBody, exceptionDispatch, logicalBody, Frame.V1MoveNextAsDispatcher.DispatcherFrames(), parent })
                .Concat(tail).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = new[] { native }.Concat(prefix).Concat(new[] { builder })
                .Concat(logical).Concat(tail).ToArray();
            scenario.ExpectedWithoutNormalization = new[] { native }.Concat(prefix).Concat(new[] { builder })
                .Concat(physical).Concat(new[] { Frame.V1MoveNextAsDispatcher, parent }).Concat(tail).ToArray();
            scenario.ExpectedDiagnostics = new CallStackDiagnosticsExpectation
            {
                SegmentsProcessed = 2,
                V1InlineFallbackUsed = 2,
                V1InfrastructureFramesCollapsed = 2,
                V1SynchronousMoveNextFramesNormalized = 4,
                AsyncInfrastructureFramesCollapsed = (prefix.Length + 2) * 2,
                ProfilerEnvelopeFramesCollapsed = inline ? 4 : 6,
            };
            return scenario;
        }

        private static StitchScenario CreateV1CompletionMachineryScenario()
        {
            Frame yield = Frame.CoreLib(
                "System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter+<>c.<OutputCorrelationEtwEvent>b__6_0(System.Action,System.Threading.Tasks.Task)",
                "Yield.Correlation");
            StitchScenario scenario = CreateYieldRuntimeBridgeScenario(AsyncCallstackKind.StateMachineAsync, yield);
            Frame[] prefix =
            {
                Frame.Library("coreclr", "MethodDesc.PrepareCode", "NativeWork"),
                Frame.CoreLib(
                    "System.Runtime.CompilerServices.TaskAwaiter+<>c.<OutputWaitEtwEvents>b__12_0(System.Action,System.Threading.Tasks.Task)",
                    "TaskAwaiter.Correlation"),
                Frame.CoreLib("System.Threading.Tasks.AwaitTaskContinuation.RunOrScheduleAction",
                    "AwaitTaskContinuation.RunOrScheduleAction"),
                Frame.CoreLib("System.Threading.Tasks.Task.RunContinuations", "Task.RunContinuations"),
                Frame.CoreLib("System.Threading.Tasks.Task`1[System.Int64].TrySetResult(!0)", "Task.TrySetResult"),
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].SetExistingTaskResult(class System.Object)",
                    "Builder.SetExistingTaskResult"),
            };
            Frame[] logical = scenario.ExpectedStitched.Skip(1).Take(2).ToArray();
            scenario.Sync = prefix.Concat(scenario.Sync.Skip(1)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = prefix.Concat(logical)
                .Concat(new[] { yield, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart }).ToArray();
            scenario.ExpectedStitched = new[] { prefix[0], prefix[1].DispatcherFrames() }
                .Concat(logical).Concat(new[] { Frame.ThreadPoolDispatch, Frame.WorkerThreadStart }).ToArray();
            scenario.AsyncStates = new[] { 0, 1 };
            scenario.MethodCompletions = 1;
            scenario.ExpectedDiagnostics.V1InlineFallbackUsed = 0;
            scenario.ExpectedDiagnostics.V1CompletionTailUsed = 2;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 12;
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = 10;
            return scenario;
        }

        private sealed class RejectingOptionalInfrastructureSupport : AsyncProfilerRuntimeSupport
        {
            public RejectingOptionalInfrastructureSupport() : base("Rejected optional infrastructure") { }

            protected override AsyncProfilerRuntimeSequenceMatch MatchOptionalTaskCompletion(
                AsyncProfilerCallStackTransformState context, int start, int end)
            {
                AsyncProfilerRuntimeSequenceMatch match = base.MatchOptionalTaskCompletion(context, start, end);
                return match.IsMatch ? AsyncProfilerRuntimeSequenceMatch.Reject(match.Length) : match;
            }

            protected override AsyncProfilerRuntimeSequenceMatch MatchOptionalTaskExceptionCompletion(
                AsyncProfilerCallStackTransformState context, int start, int end)
            {
                AsyncProfilerRuntimeSequenceMatch match = base.MatchOptionalTaskExceptionCompletion(context, start, end);
                return match.IsMatch ? AsyncProfilerRuntimeSequenceMatch.Reject(match.Length) : match;
            }

            protected override AsyncProfilerRuntimeSequenceMatch MatchOptionalTaskCompletionChain(
                AsyncProfilerCallStackTransformState context, int start, int end)
            {
                AsyncProfilerRuntimeSequenceMatch match = base.MatchOptionalTaskCompletionChain(context, start, end);
                return match.IsMatch ? AsyncProfilerRuntimeSequenceMatch.Reject(match.Length) : match;
            }

            protected override AsyncProfilerRuntimeSequenceMatch MatchOptionalV1AwaitRegistration(
                AsyncProfilerCallStackTransformState context, int start, int end)
            {
                AsyncProfilerRuntimeSequenceMatch match = base.MatchOptionalV1AwaitRegistration(context, start, end);
                return match.IsMatch ? AsyncProfilerRuntimeSequenceMatch.Reject(match.Length) : match;
            }
        }

        #endregion
    }
}
