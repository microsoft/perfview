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

using Xunit;

using static Microsoft.Diagnostics.Tracing.Computers.AsyncProfilerRuntimeSupport;

namespace TraceEventTests
{
    public partial class AsyncProfilerStitchIntegrationTests
    {
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RuntimeResumptionBridge_CompletedV1Unwind_GroupsOnlyTheCompletionPrefix(bool cleanup)
        {
            StitchScenario callback = CreateCallbackCompletionBridgeScenario(
                AsyncCallstackKind.StateMachineAsync, includeAwaiterHelpers: true);
            Frame[] helpers = callback.Sync.Skip(callback.Sync.Length - 9).Take(7).ToArray();
            StitchScenario scenario = CreateV1CompletedUnwindYieldBridgeScenario(helpers);
            scenario.EnableInfrastructureCleanup = cleanup;
            scenario.ExpectedStitched = cleanup
                ? scenario.ExpectedStitched.Take(3).Concat(new[] { helpers[0].DispatcherFrames() })
                    .Concat(helpers.Skip(5)).Concat(scenario.ExpectedStitched.Skip(3)).ToArray()
                : scenario.ExpectedWithoutInfrastructureCleanup;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = cleanup ? 10 : 0;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = cleanup ? 10 : 0;
            scenario.AssertTransformContext = context =>
            {
                AsyncProfilerStitchedFrame unwind = Assert.Single(context.Frames.Where(frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletionReturn));
                CodeAddressIndex bridge = context.Frames.Single(frame =>
                    frame.SyncClassification.RuntimeKind == RuntimeFrameKind.SchedulerEntry).CodeAddress;
                Assert.False(context.HasVerifiedDispatchEntry(unwind.Context, bridge));
                Assert.False(context.HasVerifiedV1CompletionAnchor(unwind.Context, bridge));
                Assert.True(context.HasVerifiedV1CompletionAnchor(unwind.Context, bridge, allowCompletionUnwind: true));
                Assert.False(AsyncProfilerCallStackTransformHelpers.HasVerifiedAsyncAnchor(
                    context, context.Frames.IndexOf(unwind), unwind.Context));
                Assert.DoesNotContain(context.Frames, frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget);
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, false, false)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, false, true)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, true, false)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, true, true)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, false, false)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, false, true)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, true, false)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, true, true)]
        public void RuntimeResumptionBridge_FoldsOnlyThePhysicalRootBridge(
            int kindValue, bool enableNormalization, bool enableParentContexts)
        {
            var kind = (AsyncCallstackKind)kindValue;
            StitchScenario scenario = CreateRuntimeResumptionBridgeScenario(kind);
            scenario.EnableNormalization = enableNormalization;
            scenario.EnableParentContexts = enableParentContexts;
            if (enableParentContexts)
            {
                Frame owner = Frame.App("Scenario.OwnerAsync");
                scenario.ContextParent = AsyncSegment.Runtime(owner);
                scenario.ExpectedStitched = scenario.ExpectedStitched.Take(3)
                    .Concat(new[] { owner }).Concat(scenario.ExpectedStitched.Skip(3)).ToArray();
                scenario.ExpectedWithoutInfrastructureCleanup =
                    scenario.ExpectedWithoutInfrastructureCleanup.Take(3)
                    .Concat(new[] { owner })
                    .Concat(scenario.ExpectedWithoutInfrastructureCleanup.Skip(3)).ToArray();
            }

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 0)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 1)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 2)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 3)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 4)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 5)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 0)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 1)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 2)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 3)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 4)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 5)]
        public void RuntimeResumptionBridge_UnresolvedFrameOrAnchor_PreservesTheCompleteBridge(
            int kindValue, int unresolved)
        {
            var kind = (AsyncCallstackKind)kindValue;
            StitchScenario scenario = CreateRuntimeResumptionBridgeScenario(kind);
            scenario.OmitSymbolFor = scenario.Sync[scenario.Sync.Length - 7 + unresolved];
            scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 0)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 1)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 2)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 3)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 4)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 5)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 0)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 1)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 2)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 3)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 4)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 5)]
        public void RuntimeResumptionBridge_ForeignFrameOrAnchor_PreservesTheCompleteBridge(
            int kindValue, int foreignIndex)
        {
            var kind = (AsyncCallstackKind)kindValue;
            StitchScenario scenario = CreateRuntimeResumptionBridgeScenario(kind);
            int physicalIndex = scenario.Sync.Length - 7 + foreignIndex;
            Frame original = scenario.Sync[physicalIndex];
            Frame foreign = Frame.Library("OtherLibrary", original.SymbolName, original.Label + " (foreign)");
            scenario.Sync[physicalIndex] = foreign;
            scenario.ExpectedWithoutInfrastructureCleanup[3 + foreignIndex] = foreign;
            scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, "B")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, "BR")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, "BT")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, "BTR")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, "BRXT")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, "BRTX")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, "BRTN")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, "BRTW")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, "B")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, "BR")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, "BT")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, "BTR")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, "BRXT")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, "BRTX")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, "BRTN")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, "BRTW")]
        public void RuntimeResumptionBridge_IncompleteOrInterrupted_PreservesTheCompleteBridge(
            int kindValue, string pattern)
        {
            var kind = (AsyncCallstackKind)kindValue;
            StitchScenario scenario = CreateRuntimeResumptionBridgeScenario(kind);
            int tailStart = scenario.Sync.Length - 7;
            Frame[] bridge = scenario.Sync.Skip(tailStart).Take(3).ToArray();
            var frames = new Dictionary<char, Frame>
            {
                ['B'] = bridge[0],
                ['R'] = bridge[1],
                ['T'] = bridge[2],
                ['X'] = Frame.App("Scenario.SynchronousCallback"),
                ['N'] = Frame.Library("NativeBridge", "Native.Callback", "Native.Callback"),
                ['W'] = Frame.V2Wrapper(1),
            };
            Frame[] tail = pattern.Select(token => frames[token])
                .Concat(scenario.Sync.Skip(tailStart + 3)).ToArray();
            scenario.Sync = scenario.Sync.Take(tailStart).Concat(tail).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup =
                scenario.ExpectedWithoutInfrastructureCleanup.Take(3).Concat(tail).ToArray();
            scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync)]
        public void RuntimeResumptionBridge_NoActiveSegment_IsVerbatim(int kindValue)
        {
            var kind = (AsyncCallstackKind)kindValue;
            StitchScenario scenario = CreateRuntimeResumptionBridgeScenario(kind);
            scenario.AsyncStartQpc = 100_030;
            scenario.ExpectedStitched = scenario.Sync;
            scenario.ExpectedWithoutInfrastructureCleanup = scenario.Sync;
            scenario.ExpectedDiagnostics = new CallStackDiagnosticsExpectation();

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void RuntimeResumptionBridge_V1ResumePrelude_PreservesProfilerWorkAndLogicalCaller()
        {
            StitchScenario scenario = CreateRuntimeResumptionBridgeScenario(AsyncCallstackKind.StateMachineAsync);
            Frame profiler = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncProfiler+AsyncCallstack.EmitEvent", "Profiler.EmitEvent");
            scenario.Sync = new[] { profiler }.Concat(scenario.Sync.Skip(2)).ToArray();
            Frame[] prelude = { profiler, Frame.V1MoveNextAsDispatcher, scenario.Async[0] };
            scenario.ExpectedStitched = prelude.Concat(scenario.ExpectedStitched.Skip(2)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = prelude
                .Concat(scenario.ExpectedWithoutInfrastructureCleanup.Skip(2)).ToArray();

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, false, false)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, false, true)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, true, false)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, true, true)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, false, false)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, false, true)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, true, false)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, true, true)]
        public void RuntimeResumptionBridge_ApplicationCallback_GroupsOnlyTheCompletionPrefix(
            int kindValue, bool enableNormalization, bool enableParentContexts)
        {
            var kind = (AsyncCallstackKind)kindValue;
            StitchScenario scenario = CreateCallbackCompletionBridgeScenario(kind);
            scenario.ExpectedStitched = scenario.ExpectedStitched.Take(3)
                .Concat(scenario.ExpectedStitched.Skip(6)).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 6;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 6;
            Frame prefix = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start", "AsyncMethodBuilderCore.Start");
            scenario.Sync = new[] { prefix }.Concat(scenario.Sync).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup =
                new[] { prefix }.Concat(scenario.ExpectedWithoutInfrastructureCleanup).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed += 2;
            scenario.EnableNormalization = enableNormalization;
            scenario.EnableParentContexts = enableParentContexts;
            if (enableParentContexts)
            {
                Frame owner = Frame.App("Scenario.OwnerAsync");
                scenario.ContextParent = AsyncSegment.Runtime(owner);
                scenario.ExpectedStitched = scenario.ExpectedStitched.Take(3).Concat(new[] { owner })
                    .Concat(scenario.ExpectedStitched.Skip(3)).ToArray();
                scenario.ExpectedWithoutInfrastructureCleanup =
                    scenario.ExpectedWithoutInfrastructureCleanup.Take(4).Concat(new[] { owner })
                        .Concat(scenario.ExpectedWithoutInfrastructureCleanup.Skip(4)).ToArray();
            }
            scenario.AssertTransformContext = context =>
            {
                AsyncProfilerStitchedFrame current = Assert.Single(context.Frames.Where(frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent));
                Assert.Same(context.Segments[0], current.Context);
                Assert.Equal(0, current.SegmentFrameIndex);
                Assert.Equal(0, current.Segment.FrameStateAt(0));
                Assert.Contains(context.Frames, frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
                    context.TraceLog.CodeAddresses[frame.CodeAddress].FullMethodName == "Scenario.SynchronousCallback");
                Assert.Contains(context.Frames, frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
                    context.TraceLog.CodeAddresses[frame.CodeAddress].FullMethodName == "System.Threading.TimerQueueTimer.Fire");
                AsyncProfilerStitchedFrame marker = Assert.Single(context.Frames.Where(frame =>
                    frame.Context == null &&
                    frame.Presentation == AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames));
                Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, marker.Origin);
                Assert.Equal(RuntimeFrameKind.TaskContinuations, marker.SyncClassification);
                Assert.Equal("System.Threading.Tasks.Task.RunContinuations",
                    context.TraceLog.CodeAddresses[marker.CodeAddress].FullMethodName);
                Assert.True(context.Diagnostics.RuntimeResumptionBridgeFramesCollapsed > 0);
                Assert.Equal(0, context.Diagnostics.AsyncReentrantHandoffFramesCollapsed);
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, false)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, true)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, false)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, true)]
        public void RuntimeResumptionBridge_ApplicationCallback_AwaiterPrefixIsMachineryOnly(
            int kindValue, bool cleanup)
        {
            StitchScenario scenario = CreateCallbackCompletionBridgeScenario((AsyncCallstackKind)kindValue, includeAwaiterHelpers: true);
            scenario.EnableInfrastructureCleanup = cleanup;
            scenario.ExpectedWithoutNormalization = scenario.ExpectedWithoutInfrastructureCleanup;
            if (cleanup)
            {
                scenario.ExpectedStitched = scenario.ExpectedStitched.Take(3)
                    .Concat(scenario.ExpectedStitched.Skip(8)).ToArray();
                scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 10;
                scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 10;
            }
            scenario.AssertTransformContext = context =>
            {
                AsyncProfilerStitchedFrame[] markers = context.Frames.Where(frame =>
                    frame.Context == null &&
                    frame.Presentation == AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames).ToArray();
                Assert.Equal(cleanup ? 1 : 0, markers.Length);
                if (cleanup)
                {
                    Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, markers[0].Origin);
                    Assert.Equal(AsyncProfilerStitchSyncFrameKind.AwaiterHelper, markers[0].SyncFrameKind);
                    Assert.Contains("TaskAwaiter+<>c", context.TraceLog.CodeAddresses[markers[0].CodeAddress].FullMethodName);
                    int markerIndex = context.Frames.IndexOf(markers[0]);
                    Assert.Equal("Scenario.SynchronousCallback",
                        context.TraceLog.CodeAddresses[context.Frames[markerIndex + 1].CodeAddress].FullMethodName);
                }
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, "unresolved-awaiter")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, "foreign-awaiter")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, "other-runtime")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, "unknown-helper")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, "interrupted")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, "unresolved-anchor")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, "foreign-anchor")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, "profile-veto")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, "unresolved-awaiter")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, "foreign-awaiter")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, "other-runtime")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, "unknown-helper")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, "interrupted")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, "unresolved-anchor")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, "foreign-anchor")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, "profile-veto")]
        public void RuntimeResumptionBridge_ApplicationCallback_UnverifiedAwaiterPrefixRemainsPhysical(
            int kindValue, string obstruction)
        {
            StitchScenario scenario = CreateCallbackCompletionBridgeScenario((AsyncCallstackKind)kindValue, includeAwaiterHelpers: true);
            int prefixStart = scenario.Sync.Length - 9;
            if (obstruction == "unresolved-awaiter" || obstruction == "unresolved-anchor")
            {
                scenario.OmitSymbolFor = obstruction == "unresolved-awaiter"
                    ? scenario.Sync[prefixStart] : Frame.ThreadPoolDispatch;
            }
            else if (obstruction == "profile-veto")
            {
                var support = new AsyncProfilerRejectRuntimeBridgeSupport();
                scenario.RuntimeSupportForModule = module => AsyncProfilerRuntimeResolver.IsHostModule(module)
                    ? support : AsyncProfilerRuntimeSupport.Common;
            }
            else
            {
                int index = obstruction == "foreign-anchor" ? scenario.Sync.Length - 2 : prefixStart;
                Frame original = scenario.Sync[index];
                Frame replacement = obstruction == "unknown-helper"
                    ? Frame.CoreLib("System.Runtime.CompilerServices.UnknownAwaiter.Invoke", "UnknownAwaiter")
                    : obstruction == "interrupted"
                        ? Frame.App("Scenario.InterruptedCompletion")
                        : Frame.Library(obstruction == "other-runtime" ? @"C:\other\System.Private.CoreLib.dll" : "OtherLibrary",
                            original.SymbolName, original.Label + " (foreign)");
                scenario.Sync[index] = replacement;
                scenario.ExpectedWithoutInfrastructureCleanup = scenario.ExpectedWithoutInfrastructureCleanup
                    .Select(frame => frame.Key == original.Key ? replacement : frame).ToArray();
            }
            scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup;
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync)]
        public void RuntimeResumptionBridge_ApplicationCallback_UnresolvedHostDoesNotBlockVerifiedPrefix(int kindValue)
        {
            StitchScenario scenario = CreateCallbackCompletionBridgeScenario((AsyncCallstackKind)kindValue);
            scenario.OmitSymbolFor = scenario.Sync[scenario.Sync.Length - 3];
            scenario.ExpectedStitched = scenario.ExpectedStitched.Take(3)
                .Concat(scenario.ExpectedStitched.Skip(6)).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 6;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 6;
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RuntimeResumptionBridge_ApplicationCallback_PreservesPendingV1BoxTarget(bool cleanup)
        {
            StitchScenario scenario = CreateCallbackCompletionBridgeScenario(
                AsyncCallstackKind.StateMachineAsync, includeAwaiterHelpers: true);
            scenario.Sync[1] = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncStateMachineBox`1[System.Int64,Scenario+<Level2Async>d__2].MoveNext(class System.Threading.Thread,value class Flags)",
                "PendingBox");
            scenario.EnableInfrastructureCleanup = cleanup;
            scenario.AssertStitchedStackSource = null;
            if (cleanup)
            {
                scenario.ExpectedStitched = scenario.ExpectedStitched.Take(3)
                    .Concat(scenario.ExpectedStitched.Skip(8)).ToArray();
                scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 10;
                scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 10;
            }
            scenario.AssertTransformContext = context =>
            {
                AsyncProfilerStitchedFrame target = Assert.Single(context.Frames.Where(frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget));
                Assert.True(target.IsStateMachineBoxCarrier);
                Assert.Equal(0, target.SegmentFrameIndex);
                Assert.Equal(0, target.Segment.FrameStateAt(0));
                Assert.DoesNotContain(context.Frames, frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, "unresolved-0")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, "unresolved-1")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, "unresolved-2")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, "foreign-0")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, "foreign-1")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, "foreign-2")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, "interrupted")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, "not-root-adjacent")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, "incomplete")]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, "missing-dispatch")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, "unresolved-0")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, "unresolved-1")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, "unresolved-2")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, "foreign-0")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, "foreign-1")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, "foreign-2")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, "interrupted")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, "not-root-adjacent")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, "incomplete")]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, "missing-dispatch")]
        public void RuntimeResumptionBridge_ApplicationCallback_PreservesUnverifiedCompletionChain(
            int kindValue, string obstruction)
        {
            var kind = (AsyncCallstackKind)kindValue;
            StitchScenario scenario = CreateCallbackCompletionBridgeScenario(kind);
            int tailStart = scenario.Sync.Length - 7;
            if (obstruction.StartsWith("unresolved-", StringComparison.Ordinal))
            {
                scenario.OmitSymbolFor = scenario.Sync[tailStart + int.Parse(obstruction.Substring(11))];
            }
            else if (obstruction.StartsWith("foreign-", StringComparison.Ordinal))
            {
                int index = int.Parse(obstruction.Substring(8));
                Frame original = scenario.Sync[tailStart + index];
                Frame foreign = Frame.Library("OtherLibrary", original.SymbolName, original.Label + " (foreign)");
                scenario.Sync[tailStart + index] = foreign;
                scenario.ExpectedWithoutInfrastructureCleanup[3 + index] = foreign;
            }
            else if (obstruction == "interrupted" || obstruction == "not-root-adjacent")
            {
                Frame callback = Frame.App("Scenario.InterruptedCompletion");
                int offset = obstruction == "interrupted" ? 1 : 0;
                scenario.Sync = scenario.Sync.Take(tailStart + offset).Concat(new[] { callback })
                    .Concat(scenario.Sync.Skip(tailStart + offset)).ToArray();
                scenario.ExpectedWithoutInfrastructureCleanup =
                    scenario.ExpectedWithoutInfrastructureCleanup.Take(3 + offset).Concat(new[] { callback })
                        .Concat(scenario.ExpectedWithoutInfrastructureCleanup.Skip(3 + offset)).ToArray();
            }
            else if (obstruction == "incomplete")
            {
                Frame omitted = scenario.Sync[tailStart + 2];
                scenario.Sync = scenario.Sync.Where(frame => frame != omitted).ToArray();
                scenario.ExpectedWithoutInfrastructureCleanup =
                    scenario.ExpectedWithoutInfrastructureCleanup.Where(frame => frame != omitted).ToArray();
            }
            else
            {
                Assert.Equal("missing-dispatch", obstruction);
                scenario.Sync = scenario.Sync.Where(frame => frame != Frame.ThreadPoolDispatch).ToArray();
                scenario.ExpectedWithoutInfrastructureCleanup = scenario.ExpectedWithoutInfrastructureCleanup
                    .Where(frame => frame != Frame.ThreadPoolDispatch).ToArray();
            }
            scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync)]
        public void RuntimeResumptionBridge_ApplicationCallback_PreservesTheChainAndUnresolvedOuterWork(
            int kindValue)
        {
            var kind = (AsyncCallstackKind)kindValue;
            StitchScenario scenario = CreateCallbackCompletionBridgeScenario(kind);
            scenario.OmitSymbolFor = scenario.Sync[scenario.Sync.Length - 4];
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("completing")]
        [InlineData("ambiguous")]
        public void RuntimeResumptionBridge_ApplicationCallback_PreservesUnverifiedV1Anchor(string obstruction)
        {
            StitchScenario scenario = CreateCallbackCompletionBridgeScenario(AsyncCallstackKind.StateMachineAsync);
            scenario.EnableInfrastructureCleanup = false;
            scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
            scenario.AssertTransformContext = context =>
            {
                var frames = new List<AsyncProfilerStitchedFrame>(context.Frames);
                var diagnostics = new AsyncProfilerCallStackDiagnostics();
                AsyncProfilerCallStackStitchingState stitchingState = context.StitchingState.Copy();
                if (obstruction == "completing")
                {
                    int index = frames.FindIndex(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
                    AsyncProfilerStitchedFrame current = frames[index];
                    frames[index] = AsyncProfilerStitchedFrame.CreateV1CompletedBody(
                        current.CodeAddress, current.Method, current.Context, current.SegmentFrameIndex);
                }
                else
                {
                    stitchingState.UnresolvedV1CompletionCount = 1;
                }
                AsyncProfilerStitchedFrame[] expected = frames.ToArray();
                var pipeline = new AsyncProfilerCallStackTransformPipeline
                {
                    EnableNormalization = false,
                    EnableInfrastructureCleanup = true,
                };
                AsyncProfilerStitchResult result = pipeline.Transform(new AsyncProfilerStitchResult(frames, diagnostics, stitchingState), context.Segments,
                    context.TimestampQpc, context.ClassifyBoundary, context.TraceLog);
                Assert.Equal(expected, result.Frames);
                Assert.Equal(0, diagnostics.AsyncInfrastructureFramesCollapsed);
                Assert.Equal(0, diagnostics.RuntimeResumptionBridgeFramesCollapsed);
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync)]
        public void RuntimeResumptionBridge_RejectedTail_DoesNotBlockLeafwardHelperCleanup(int kindValue)
        {
            var kind = (AsyncCallstackKind)kindValue;
            StitchScenario scenario = CreateRuntimeResumptionBridgeScenario(kind);
            Frame[] prefix =
            {
                Frame.CoreLib("System.Threading.Tasks.Task.RunContinuations", "Task.RunContinuations"),
                Frame.CoreLib("System.Threading.Tasks.Task.TrySetResult", "Task.TrySetResult"),
                Frame.CoreLib("System.Threading.Tasks.TaskCompletionSource.SetResult", "TaskCompletionSource.SetResult"),
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start", "AsyncMethodBuilderCore.Start"),
            };
            int timerIndex = scenario.Sync.Length - 3;
            Frame callback = Frame.App("Scenario.SynchronousCallback");
            scenario.Sync[timerIndex] = callback;
            scenario.Sync = prefix.Concat(scenario.Sync).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup[7] = callback;
            scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup;
            scenario.ExpectedWithoutInfrastructureCleanup =
                prefix.Concat(scenario.ExpectedWithoutInfrastructureCleanup).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 8;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void RuntimeAsync_TaskContinuationResume_DispatchOnlyPreservesCalleesAndRejectedBridge(
            bool helperIsLeaf, bool enableInfrastructureCleanup)
        {
            Frame resume = Frame.CoreLib(
                "System.Runtime.CompilerServices.RuntimeAsyncTaskContinuation+TaskContinuationResume." +
                "ResumeTaskContinuation(class System.Runtime.CompilerServices.Continuation,unsigned int8&)",
                "ResumeTaskContinuation");
            Frame current = Frame.App("Scenario.ChildAsync");
            Frame parent = Frame.App("Scenario.ParentAsync");
            Frame owner = Frame.App("Scenario+<OwnerAsync>d__1.MoveNext").Logical("Scenario.OwnerAsync", 0);
            Frame[] callees = helperIsLeaf ? Array.Empty<Frame>() : new[] { Frame.App("Scenario.HelperCallee") };
            Frame[] tail =
            {
                Frame.CoreLib("System.Threading.Tasks.Task.RunContinuations", "Task.RunContinuations"),
                Frame.CoreLib("System.Threading.Tasks.Task`1[System.Boolean].TrySetResult(!0)", "Task.TrySetResult"),
                Frame.CoreLib("System.Threading.Tasks.TaskCompletionSource`1[System.Boolean].SetResult(!0)", "TaskCompletionSource.SetResult"),
                Frame.App("Scenario.CompleteOnNewThread"),
                Frame.CoreLib("System.Threading.Thread.StartCallback", "Thread.StartCallback"),
            };
            var scenario = new StitchScenario
            {
                Sync = callees.Concat(new[]
                {
                    resume, Frame.V2Wrapper(0), Frame.V2InstrumentedDispatch, Frame.V2Dispatch,
                }).Concat(tail).ToArray(),
                Async = new[] { Frame.Zero, current, parent },
                ContextParent = AsyncSegment.StateMachine(owner),
                EnableParentContexts = true,
                EnableInfrastructureCleanup = enableInfrastructureCleanup,
                VerifySampleWeights = true,
                ExpectedStitched = callees.Concat(new[] { resume.DispatcherFrames() }).Concat(tail).ToArray(),
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V2InfrastructureFramesCollapsed = 4,
                },
                AssertTransformContext = context =>
                {
                    Assert.All(context.Frames, frame =>
                        Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, frame.Origin));
                    Assert.Single(context.Frames.Where(frame =>
                        frame.Presentation == AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames));
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void RuntimeResumptionBridge_V2WithoutPhysicalBody_FoldsOnlyVerifiedDispatchTarget(
            bool leafWrapper, bool targetKnown)
        {
            StitchScenario scenario = CreateRuntimeResumptionBridgeScenario(AsyncCallstackKind.RuntimeAsync);
            if (!targetKnown)
            {
                scenario.Async[0] = Frame.Zero;
            }
            scenario.Sync = leafWrapper
                ? scenario.Sync.Skip(2).ToArray()
                : scenario.Sync.Where(frame => frame.Kind != FrameKind.V2Wrapper).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup =
                leafWrapper ? (targetKnown ? scenario.Async.Concat(scenario.Sync.Skip(3))
                    : scenario.Sync.Skip(1)).ToArray() : scenario.Sync;
            scenario.ExpectedStitched = targetKnown
                ? scenario.Async.Concat(scenario.Sync.Skip(scenario.Sync.Length - 2)).ToArray()
                : (leafWrapper ? Enumerable.Empty<Frame>() : scenario.Sync.Take(2))
                    .Concat(new[] { Frame.V2InstrumentedDispatch.DispatcherFrames() })
                    .Concat(scenario.Sync.Skip(scenario.Sync.Length - 7)).ToArray();
            scenario.ExpectedDiagnostics = new CallStackDiagnosticsExpectation
            {
                SegmentsProcessed = targetKnown ? 2 : 0,
                V2InfrastructureFramesCollapsed = targetKnown ? 4 : 0,
                V2SyncLayoutUsed = targetKnown ? 0 : 2,
                V2LeafWrapperDropped = leafWrapper ? 2 : 0,
                AsyncInfrastructureFramesCollapsed = targetKnown ? 10 : 4,
                RuntimeResumptionBridgeFramesCollapsed = targetKnown ? 10 : 0,
            };
            scenario.AssertTransformContext = context =>
            {
                Assert.Equal(targetKnown ? 1 : 0,
                    context.Frames.Count(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget));
                Assert.DoesNotContain(context.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, false, true, true)]
        [InlineData(false, true, true, true)]
        [InlineData(true, false, true, true)]
        [InlineData(true, true, true, true)]
        [InlineData(false, false, false, false)]
        [InlineData(true, false, false, false)]
        [InlineData(false, true, true, false)]
        [InlineData(true, true, true, false)]
        [InlineData(false, false, false, true)]
        [InlineData(true, false, false, true)]
        public void RuntimeResumptionBridge_V2NativePrefix_PreservesWorkAndProjectsOnlyADispatchTarget(
            bool resumeStub, bool methodEvents, bool enableNormalization, bool enableInfrastructureCleanup)
        {
            StitchScenario scenario = CreateNativeRuntimeDispatchScenario(resumeStub, methodEvents);
            scenario.EnableNormalization = enableNormalization;
            scenario.EnableInfrastructureCleanup = enableInfrastructureCleanup;
            if (!enableInfrastructureCleanup)
            {
                scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup;
                scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
                scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;
            }
            scenario.OmitSymbolFor = scenario.Sync[0];
            scenario.AssertTransformContext = context =>
            {
                AsyncProfilerStitchedFrame target = Assert.Single(context.Frames.Where(frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget));
                Assert.Equal(1, target.SegmentFrameIndex);
                Assert.Same(context.Segments[0], target.Context);
                Assert.Equal(context.Segments[0].Frames.CodeAddressAt(1), target.CodeAddress);
                Assert.DoesNotContain(context.Frames, frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
                Assert.All(context.Frames.Take(2), frame =>
                    Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, frame.Origin));
                Assert.Equal(MethodIndex.Invalid, context.Frames[0].Method);
                Assert.NotEqual(MethodIndex.Invalid, context.Frames[1].Method);
                AsyncProfilerStitchedFrame marker = context.Frames[2];
                Assert.Equal(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames, marker.Presentation);
                Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, marker.Origin);
                Assert.Same(target.Context, marker.Context);
                Assert.Equal(resumeStub ? "dynamicClass.IL_STUB_AsyncResume" : Frame.V2Wrapper(1).SymbolName,
                    context.TraceLog.CodeAddresses[marker.CodeAddress].FullMethodName);
                Assert.Equal(3, context.Frames.IndexOf(target));
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void RuntimeResumptionBridge_V2NativeEntry_PreservesArbitraryCallees(
            bool resumeStub, bool unresolvedEntry)
        {
            StitchScenario scenario = CreateNativeRuntimeDispatchScenario(resumeStub, false);
            Frame entry = scenario.Sync[1];
            Frame[] callees =
            {
                Frame.Library("ntdll", "RtlAllocateHeap", "OS work"),
                Frame.App("Scenario.LoaderCallback"),
                Frame.Library("thirdparty", "ThirdParty.NativeHelper", "Native callback work"),
            };
            scenario.Sync = callees.Concat(scenario.Sync).ToArray();
            scenario.ExpectedStitched = callees.Concat(scenario.ExpectedStitched).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup =
                callees.Concat(scenario.ExpectedWithoutInfrastructureCleanup).ToArray();
            scenario.OmitSymbolFor = unresolvedEntry ? entry : callees[0];
            scenario.AssertTransformContext = context =>
            {
                AsyncProfilerStitchedFrame target = Assert.Single(context.Frames.Where(frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget));
                Assert.Equal(1, target.SegmentFrameIndex);
                Assert.DoesNotContain(context.Frames, frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
                Assert.All(context.Frames.Take(5), frame =>
                    Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, frame.Origin));
                Assert.Equal(MethodIndex.Invalid, context.Frames[unresolvedEntry ? 4 : 0].Method);
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("physical-target")]
        [InlineData("v1-dispatcher")]
        [InlineData("v2-dispatch")]
        [InlineData("extra-stub")]
        public void RuntimeResumptionBridge_V2NativeEntry_RejectsConflictingCalleeEvidence(string obstruction)
        {
            StitchScenario scenario = CreateNativeRuntimeDispatchScenario(false, false);
            Frame conflict = obstruction == "physical-target" ? scenario.Async[1] :
                obstruction == "v1-dispatcher" ? Frame.V1MoveNextAsDispatcher :
                obstruction == "v2-dispatch" ? Frame.V2InstrumentedDispatch :
                Frame.App("dynamicClass.IL_STUB_AsyncResume");
            scenario.Sync = new[] { conflict }.Concat(scenario.Sync).ToArray();
            scenario.EnableNormalization = false;
            scenario.EnableInfrastructureCleanup = false;
            scenario.ExpectedStitched = scenario.Sync.Take(3).Concat(scenario.Async.Skip(2))
                .Concat(scenario.Sync.Skip(6)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = scenario.ExpectedStitched;
            scenario.ExpectedDiagnostics.AdjacencyMismatches = 2;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;
            scenario.AssertTransformContext = context =>
            {
                Assert.DoesNotContain(context.Frames, frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RuntimeResumptionBridge_V2NativeEntry_UsesCanonicalTargetIdentity(bool resumeStub)
        {
            StitchScenario scenario = CreateNativeRuntimeDispatchScenario(resumeStub, false);
            scenario.AssertTransformContext = context =>
            {
                TraceLog log = context.TraceLog;
                AsyncProfilerCallStack callStack = context.Segments[0];
                CodeAddressIndex target = callStack.Frames.CodeAddressAt(1);
                MethodIndex original = context.GetMethod(target);
                MethodIndex canonical = context.GetMethod(callStack.Frames.CodeAddressAt(2));
                Assert.NotEqual(original, canonical);
                // Model a target whose canonical identity differs from its captured JIT version.
                Func<CodeAddressIndex, MethodIndex> methodOf = address =>
                {
                    MethodIndex method = context.GetMethod(address);
                    return method == original ? canonical : method;
                };

                TraceEvent sample = log.Events.First(data =>
                    data.ProviderGuid == SampleProfilerTraceEventParser.ProviderGuid &&
                    data.TryGetCallStackIndex(out _, out long timestampQpc) && timestampQpc == context.TimestampQpc);
                Assert.True(sample.TryGetCallStackIndex(out CallStackIndex physicalStack, out _));
                var runtimeResolver = new AsyncProfilerRuntimeResolver(log.ModuleFiles);
                var boundaries = new AsyncProfilerCallStackStitchBoundaryCache(log.CodeAddresses, runtimeResolver);
                var sync = new List<AsyncProfilerStitchSyncFrame>
                {
                    new AsyncProfilerStitchSyncFrame(target, canonical),
                };
                for (CallStackIndex stack = physicalStack; stack != CallStackIndex.Invalid; stack = log.CallStacks.Caller(stack))
                {
                    CodeAddressIndex address = log.CallStacks.CodeAddressIndex(stack);
                    FrameClassification kind = boundaries.GetRuntimeSupport(address).ClassifySynchronousFrame(
                        AsyncProfilerMethodNames.NormalizeMethodName(log.CodeAddresses[address].FullMethodName),
                        runtimeResolver.IsHostModule(log.CodeAddresses.ModuleFileIndex(address)));
                    sync.Add(new AsyncProfilerStitchSyncFrame(address, methodOf(address), kind));
                }

                var frames = new List<AsyncProfilerStitchedFrame>();
                var diagnostics = new AsyncProfilerCallStackDiagnostics();
                AsyncProfilerCallStackStitcher.StitchInto(
                    sync, context.Segments, context.TimestampQpc, boundaries.Classify,
                    (process, kind, timestampQpc) => false, callStack.ProcessIndex, methodOf, false, frames, diagnostics,
                    stitchingState: new AsyncProfilerCallStackStitchingState(),
                    placements: null, runtimeSupportResolver: boundaries.GetRuntimeSupport,
                    runtimeTraceLog: log, runtimeResolver: runtimeResolver);
                Assert.DoesNotContain(frames, frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
                Assert.Equal(target, frames[0].CodeAddress);
                Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, frames[0].Origin);
            };
            scenario.AssertProductionStitch();
        }

        [Fact]
        public void RuntimeResumptionBridge_V2NativePrefix_ProjectsInnerTargetWithoutReplacingTheOuterBody()
        {
            StitchScenario scenario = CreateNativeRuntimeDispatchScenario(false, false);
            Frame outer = Frame.App("Scenario.OuterAsync");
            Frame outerParent = Frame.App("Scenario.OuterParentAsync");
            scenario.AsyncSegments = new[]
            {
                AsyncSegment.Runtime(outer, outerParent),
                AsyncSegment.Runtime(scenario.Async),
            };
            scenario.Sync = scenario.Sync.Take(5).Concat(new[]
            {
                outer, Frame.V2Wrapper(0), Frame.V2InstrumentedDispatch, Frame.V2Dispatch,
            }).Concat(scenario.Sync.Skip(5)).ToArray();
            Frame[] logical = scenario.Sync.Take(2).Concat(new[] { Frame.V2Wrapper(1).DispatcherFrames() }).Concat(scenario.Async.Skip(1))
                .Concat(new[] { outer, outerParent }).ToArray();
            scenario.ExpectedStitched = logical.Concat(scenario.Sync.Skip(scenario.Sync.Length - 2)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = logical.Concat(scenario.Sync.Skip(9)).ToArray();
            scenario.ExpectedDiagnostics.SegmentsProcessed = 4;
            scenario.ExpectedDiagnostics.V2InfrastructureFramesCollapsed = 8;
            scenario.AssertTransformContext = context =>
            {
                Assert.Same(context.Segments[0], Assert.Single(context.Frames.Where(frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget)).Context);
                Assert.Same(context.Segments[1], Assert.Single(context.Frames.Where(frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent)).Context);
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("foreign-runtime")]
        [InlineData("managed-image")]
        [InlineData("missing-image")]
        [InlineData("application-callback")]
        [InlineData("unresolved-target")]
        [InlineData("completed")]
        [InlineData("before-base")]
        public void RuntimeResumptionBridge_V2NativePrefix_UnverifiedEvidenceDoesNotProjectATarget(string obstruction)
        {
            StitchScenario scenario = CreateNativeRuntimeDispatchScenario(false, false);
            Action<TraceLog> configure = scenario.ConfigureTraceLog;
            scenario.ConfigureTraceLog = log =>
            {
                configure(log);
                TraceModuleFile native = log.ModuleFiles.First(module => module.Name == "coreclr");
                if (obstruction == "foreign-runtime")
                {
                    native.fileName = @"C:\other-runtime\coreclr.dll";
                }
                else if (obstruction == "managed-image")
                {
                    native.managedModule = log.ModuleFiles.First(module => string.Equals(
                        module.Name, AsyncProfilerRuntimeSupport.HostModuleName, StringComparison.OrdinalIgnoreCase));
                }
                else if (obstruction == "missing-image")
                {
                    native.imageBase = 0;
                }
            };
            if (obstruction == "application-callback")
            {
                scenario.Sync = scenario.Sync.Take(2).Concat(new[] { Frame.App("Scenario.UnrelatedCallback") })
                    .Concat(scenario.Sync.Skip(2)).ToArray();
            }
            else if (obstruction == "unresolved-target")
            {
                scenario.OmitSymbolFor = scenario.Async[1];
            }
            else if (obstruction == "completed")
            {
                scenario.MethodCompletions = scenario.Async.Length;
            }
            else if (obstruction == "before-base")
            {
                scenario.ContinuationIndexBase = 2;
            }
            int wrapper = Array.FindIndex(scenario.Sync, frame => frame.Kind == FrameKind.V2Wrapper);
            int completed = obstruction == "completed" ? scenario.Async.Length - 1 :
                obstruction == "before-base" ? 0 : 1;
            scenario.ExpectedStitched = scenario.Sync.Take(wrapper).Concat(scenario.Async.Skip(completed + 1))
                .Concat(scenario.Sync.Skip(wrapper + 3)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = scenario.ExpectedStitched;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.AdjacencyMismatches =
                obstruction == "unresolved-target" || obstruction == "before-base" ? 0 : 2;
            scenario.AssertTransformContext = context =>
            {
                Assert.DoesNotContain(context.Frames, frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, true, false)]
        [InlineData(true, false, false)]
        [InlineData(true, true, false)]
        [InlineData(false, false, true)]
        [InlineData(false, true, true)]
        [InlineData(true, false, true)]
        [InlineData(true, true, true)]
        public void RuntimeResumptionBridge_YieldFamily_FoldsOnlyTheBoundedMachinerySpan(
            bool runtimeAsync, bool multiple, bool enableNormalization)
        {
            Frame[] yieldFrames = multiple ? new[]
            {
                Frame.CoreLib("System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter+<>c::<DifferentCallback>b__91_2(System.Action)",
                    "Yield.GeneratedCallback"),
                Frame.CoreLib("System.Void System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter+Helper`1[System.Int64]::Invoke()",
                    "Yield.NestedHelper"),
                Frame.CoreLib("System.Runtime.CompilerServices.YieldAwaitable.GetAwaiter()", "Yield.GetAwaiter"),
            } : new[]
            {
                Frame.CoreLib("System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter+<>c::<OutputCorrelationEtwEvent>b__6_0(System.Action, System.Threading.Tasks.Task)",
                    "Yield.Correlation"),
            };
            StitchScenario scenario = CreateYieldRuntimeBridgeScenario(runtimeAsync
                ? AsyncCallstackKind.RuntimeAsync : AsyncCallstackKind.StateMachineAsync, yieldFrames);
            scenario.EnableNormalization = enableNormalization;
            Frame leafSideYield = Frame.CoreLib(
                "System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter.GetResult()", "Yield.ApplicationWork");
            scenario.Sync = new[] { leafSideYield }.Concat(scenario.Sync).ToArray();
            scenario.ExpectedStitched = new[] { leafSideYield }.Concat(scenario.ExpectedStitched).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup =
                new[] { leafSideYield }.Concat(scenario.ExpectedWithoutInfrastructureCleanup).ToArray();
            scenario.ExpectedWithoutNormalization = scenario.ExpectedWithoutInfrastructureCleanup;

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("missing-instrumented", true)]
        [InlineData("reversed-pair", true)]
        [InlineData("duplicate-pair", true)]
        [InlineData("foreign-module", true)]
        [InlineData("other-corelib", true)]
        [InlineData("whole-bridge-other-runtime", true)]
        [InlineData("lookalike", true)]
        [InlineData("namespace-lookalike", true)]
        [InlineData("interrupted", true)]
        [InlineData("unresolved-yield", true)]
        [InlineData("unresolved-anchor", true)]
        [InlineData("unverified-current", true)]
        [InlineData("foreign-module", false)]
        [InlineData("other-corelib", false)]
        [InlineData("whole-bridge-other-runtime", false)]
        [InlineData("lookalike", false)]
        [InlineData("namespace-lookalike", false)]
        [InlineData("interrupted", false)]
        [InlineData("unresolved-yield", false)]
        [InlineData("unresolved-anchor", false)]
        public void RuntimeResumptionBridge_YieldFamily_UnverifiedSpansRemainIntact(string obstruction, bool runtimeAsync)
        {
            Frame yield = Frame.CoreLib(
                "System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter+<>c.Callback()", "Yield.Callback");
            Frame[] span = obstruction == "interrupted" ? new[]
            {
                yield, Frame.App("Scenario.UnrelatedCallback"),
                Frame.CoreLib(
                    "System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter.System.Runtime.CompilerServices.IStateMachineBoxAwareAwaiter.AwaitUnsafeOnCompleted(class System.Runtime.CompilerServices.IAsyncStateMachineBox)",
                    "Yield.Registration"),
            } : new[]
            {
                obstruction == "foreign-module" ? Frame.Library("Other.CoreLib", yield.SymbolName, "Foreign.Yield") :
                obstruction == "other-corelib" ? Frame.Library(
                    @"C:\other\System.Private.CoreLib.dll", yield.SymbolName, "OtherCoreLib.Yield") :
                obstruction == "lookalike" ? Frame.CoreLib(
                    "System.Runtime.CompilerServices.YieldAwaitableLookalike+YieldAwaiter.Callback()", "Lookalike.Yield") :
                obstruction == "namespace-lookalike" ? Frame.CoreLib(
                    "System.Runtime.CompilerServices.YieldAwaitable.OtherType.Callback()", "NamespaceLookalike.Yield") : yield,
            };
            StitchScenario scenario = CreateYieldRuntimeBridgeScenario(runtimeAsync
                ? AsyncCallstackKind.RuntimeAsync : AsyncCallstackKind.StateMachineAsync, span);
            if (obstruction == "missing-instrumented")
            {
                scenario.Sync = scenario.Sync.Where(frame => frame != Frame.V2InstrumentedDispatch).ToArray();
                scenario.ExpectedDiagnostics.V2InfrastructureFramesCollapsed = 2;
            }
            else if (obstruction == "reversed-pair")
            {
                scenario.Sync[3] = Frame.V2Dispatch;
                scenario.Sync[4] = Frame.V2InstrumentedDispatch;
            }
            else if (obstruction == "duplicate-pair")
            {
                scenario.Sync[4] = Frame.V2InstrumentedDispatch;
            }
            else if (obstruction == "unresolved-yield")
            {
                scenario.OmitSymbolFor = yield;
            }
            else if (obstruction == "unresolved-anchor")
            {
                scenario.OmitSymbolFor = scenario.Sync[scenario.Sync.Length - 2];
            }
            else if (obstruction == "unverified-current")
            {
                Frame other = Frame.App("Scenario.OtherAsync");
                scenario.Sync[1] = other;
                scenario.ExpectedWithoutInfrastructureCleanup[1] = other;
                scenario.ExpectedDiagnostics.AdjacencyMismatches = 2;
            }
            else if (obstruction == "whole-bridge-other-runtime")
            {
                Frame Replace(Frame frame) => frame == yield || frame == Frame.ThreadPoolDispatch
                    ? Frame.Library(@"C:\other\System.Private.CoreLib.dll", frame.SymbolName, "OtherRuntime." + frame.Label)
                    : frame;
                scenario.Sync = scenario.Sync.Select(Replace).ToArray();
                scenario.ExpectedWithoutInfrastructureCleanup = scenario.ExpectedWithoutInfrastructureCleanup.Select(Replace).ToArray();
            }
            scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup;
            scenario.ExpectedWithoutNormalization = scenario.ExpectedStitched;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void RuntimeResumptionBridge_YieldFamily_WithoutStructuralPairEvidenceRemainsIntact(
            bool traceContext, bool runtimeAsync)
        {
            StitchScenario scenario = CreateYieldRuntimeBridgeScenario(runtimeAsync
                ? AsyncCallstackKind.RuntimeAsync : AsyncCallstackKind.StateMachineAsync, Frame.CoreLib(
                "System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter.Callback()", "Yield.Callback"));
            scenario.EnableInfrastructureCleanup = false;
            scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;
            scenario.AssertTransformContext = context =>
            {
                var diagnostics = new AsyncProfilerCallStackDiagnostics { SegmentsProcessed = context.Segments.Count };
                var pipeline = new AsyncProfilerCallStackTransformPipeline
                {
                    EnableNormalization = false,
                    EnableInfrastructureCleanup = true,
                };
                AsyncProfilerStitchResult result = pipeline.Transform(
                    new AsyncProfilerStitchResult(context.Frames.ToArray(), diagnostics, context.StitchingState.Copy()),
                    context.Segments, context.TimestampQpc, context.ClassifyBoundary,
                    traceContext ? context.TraceLog : null);
                Assert.Equal(context.Frames, result.Frames);
                Assert.Equal(0, diagnostics.RuntimeResumptionBridgeFramesCollapsed);
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RuntimeResumptionBridge_V1YieldFamily_UnverifiedTargetRetainsTheBridge(bool enableNormalization)
        {
            StitchScenario scenario = CreateYieldRuntimeBridgeScenario(AsyncCallstackKind.StateMachineAsync, Frame.CoreLib(
                "System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter+<>c.Callback()", "Yield.Callback"));
            Frame other = Frame.App("Scenario.UnrelatedSynchronousBody");
            scenario.Sync[1] = other;
            Frame[] prefix = { scenario.Sync[0], other };
            Frame[] tail = new[] { scenario.Async[0] }.Concat(scenario.ExpectedWithoutInfrastructureCleanup.Skip(2)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = prefix.Concat(new[] { Frame.V1MoveNextAsDispatcher }).Concat(tail).ToArray();
            scenario.ExpectedStitched = prefix.Concat(new[] { Frame.V1MoveNextAsDispatcher.DispatcherFrames() }).Concat(tail).ToArray();
            scenario.EnableNormalization = enableNormalization;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 2;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;
            scenario.AssertTransformContext = context =>
            {
                Assert.DoesNotContain(context.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
                Assert.False(Assert.Single(context.Frames.Where(frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget)).IsStateMachineBoxCarrier);
            };

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void RuntimeResumptionBridge_YieldFamily_RequiresTheRootDispatchPairNotAnInnerPair()
        {
            Frame yield = Frame.CoreLib(
                "System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter.Callback()", "Yield.Callback");
            StitchScenario scenario = CreateYieldRuntimeBridgeScenario(yield);
            Frame inner = Frame.App("Scenario.InnerAsync");
            Frame innerParent = Frame.App("Scenario.InnerParentAsync");
            scenario.AsyncSegments = new[]
            {
                AsyncSegment.Runtime(scenario.Async),
                AsyncSegment.Runtime(inner, innerParent),
            };
            scenario.Sync = new[]
            {
                inner, Frame.V2Wrapper(0), Frame.V2InstrumentedDispatch, Frame.V2Dispatch,
            }.Concat(scenario.Sync.Where(frame => frame != Frame.V2InstrumentedDispatch)).ToArray();
            scenario.ExpectedStitched = new[] { inner, innerParent }
                .Concat(scenario.ExpectedWithoutInfrastructureCleanup).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = scenario.ExpectedStitched;
            scenario.ExpectedWithoutNormalization = scenario.ExpectedStitched;
            scenario.ExpectedDiagnostics.SegmentsProcessed = 4;
            scenario.ExpectedDiagnostics.V2InfrastructureFramesCollapsed = 6;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void RuntimeResumptionBridge_MissingBoundary_DoesNotFoldTimerFrames()
        {
            StitchScenario scenario = CreateRuntimeResumptionBridgeScenario(AsyncCallstackKind.RuntimeAsync);
            scenario.Sync = scenario.Sync.Take(2).Concat(scenario.Sync.Skip(5)).ToArray();
            Frame parent = scenario.Async[1];
            scenario.ExpectedWithoutInfrastructureCleanup = scenario.Sync.Concat(new[] { parent }).ToArray();
            scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup;
            scenario.ExpectedDiagnostics = new CallStackDiagnosticsExpectation
            {
                SegmentsProcessed = 2,
                BoundariesNotFound = 2,
            };

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void RuntimeResumptionBridge_MismatchedV2CurrentFrame_DoesNotFoldTimerFrames()
        {
            StitchScenario scenario = CreateRuntimeResumptionBridgeScenario(AsyncCallstackKind.RuntimeAsync);
            Frame callback = Frame.App("Scenario.UnrelatedCallback");
            scenario.Sync = scenario.Sync.Take(2).Concat(new[] { callback }).Concat(scenario.Sync.Skip(2)).ToArray();
            Frame[] logical = { scenario.Sync[0], scenario.Async[0], callback, scenario.Async[1] };
            scenario.ExpectedWithoutInfrastructureCleanup = logical.Concat(scenario.Sync.Skip(6)).ToArray();
            scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup;
            scenario.ExpectedDiagnostics.AdjacencyMismatches = 2;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void RuntimeResumptionBridge_UnresolvedV2CurrentFrame_DoesNotFoldTimerFrames()
        {
            StitchScenario scenario = CreateRuntimeResumptionBridgeScenario(AsyncCallstackKind.RuntimeAsync);
            scenario.OmitSymbolFor = scenario.Sync[1];
            scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync)]
        public void RuntimeResumptionBridge_WithoutTraceLog_PreservesTheCompleteBridge(int kindValue)
        {
            var kind = (AsyncCallstackKind)kindValue;
            StitchScenario scenario = CreateRuntimeResumptionBridgeScenario(kind);
            scenario.EnableInfrastructureCleanup = false;
            scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;
            scenario.AssertTransformContext = context =>
            {
                var diagnostics = new AsyncProfilerCallStackDiagnostics { SegmentsProcessed = context.Segments.Count };
                var pipeline = new AsyncProfilerCallStackTransformPipeline
                {
                    EnableNormalization = false,
                    EnableInfrastructureCleanup = true,
                };
                AsyncProfilerStitchResult result = pipeline.Transform(
                    new AsyncProfilerStitchResult(context.Frames.ToArray(), diagnostics, context.StitchingState.Copy()),
                    context.Segments, context.TimestampQpc, context.ClassifyBoundary);
                Assert.Equal(context.Frames, result.Frames);
                Assert.Equal(0, diagnostics.AsyncInfrastructureFramesCollapsed);
                Assert.Equal(0, diagnostics.RuntimeResumptionBridgeFramesCollapsed);
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync)]
        public void RuntimeResumptionBridge_DirectTaskContinuation_UsesTheResolvedCompletionPair(int kindValue)
        {
            var kind = (AsyncCallstackKind)kindValue;
            StitchScenario scenario = CreateRuntimeResumptionBridgeScenario(kind);
            Frame continuation = scenario.Sync[scenario.Sync.Length - 7];
            scenario.Sync = scenario.Sync.Where(frame => frame != continuation).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup =
                scenario.ExpectedWithoutInfrastructureCleanup.Where(frame => frame != continuation).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 8;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 8;

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void RuntimeResumptionBridge_NestedActiveContexts_FoldsVerifiedInterContextSpan()
        {
            StitchScenario scenario = CreateRuntimeResumptionBridgeScenario(AsyncCallstackKind.StateMachineAsync);
            Frame outerCurrent = Frame.App("Scenario.OuterAsync");
            Frame outerParent = Frame.App("Scenario.OuterParentAsync");
            Frame[] innerLogical = scenario.ExpectedStitched.Take(3).ToArray();
            Frame[] tail = scenario.Sync.Skip(scenario.Sync.Length - 7).ToArray();
            Frame[] outer =
            {
                outerCurrent, Frame.V2Wrapper(0), Frame.V2InstrumentedDispatch, Frame.V2Dispatch,
                Frame.ThreadPoolDispatch, Frame.WorkerThreadStart,
            };
            scenario.AsyncSegments = new[]
            {
                AsyncSegment.Runtime(outerCurrent, outerParent),
                AsyncSegment.StateMachine(scenario.Async),
            };
            scenario.Sync = scenario.Sync.Take(scenario.Sync.Length - 2).Concat(outer).ToArray();
            scenario.ExpectedWithoutNormalization = innerLogical.Concat(tail.Take(5))
                .Concat(new[] { outerCurrent, outerParent, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart }).ToArray();
            scenario.ExpectedStitched = innerLogical
                .Concat(new[] { outerCurrent, outerParent, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart }).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = scenario.ExpectedStitched;
            scenario.ExpectedDiagnostics.SegmentsProcessed = 4;
            scenario.ExpectedDiagnostics.V2InfrastructureFramesCollapsed = 4;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.AsyncReentrantHandoffFramesCollapsed = 10;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void V1AfterV2Completion_CollapsesOnlyTheVerifiedHandoff(
            bool enableParentContexts, bool applicationCaller)
        {
            StitchScenario scenario = CreateV1AfterV2CompletionScenario();
            scenario.EnableParentContexts = enableParentContexts;
            if (applicationCaller)
            {
                Frame caller = Frame.App("Scenario.SynchronousCaller");
                scenario.Sync[9] = caller;
                scenario.ExpectedStitched[3] = caller;
                scenario.ExpectedWithoutNormalization[8] = caller;
            }

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        [InlineData(5)]
        public void V1AfterV2Completion_UnresolvedHandoffOrCaller_PreservesTheCompleteBridge(int unresolvedFrame)
        {
            StitchScenario scenario = CreateV1AfterV2CompletionScenario();
            scenario.OmitSymbolFor = scenario.Sync[4 + unresolvedFrame];
            scenario.ExpectedStitched = scenario.ExpectedWithoutNormalization;
            scenario.ExpectedDiagnostics.V2CompletionHandoffFramesCollapsed = 0;

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void V1AfterV2Completion_SynchronousStartupNormalization_DoesNotSkipTheHandoff()
        {
            StitchScenario scenario = CreateV1AfterV2CompletionScenario();
            Frame generated = Frame.App("Scenario+<SynchronousChildAsync>d__62.MoveNext");
            Frame builder = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder.Start(Scenario+<SynchronousChildAsync>d__62&)",
                "AsyncTaskMethodBuilder.Start");
            Frame kickoff = Frame.App("Scenario.SynchronousChildAsync()");
            Frame[] prefix = { generated, builder, kickoff };
            scenario.Sync = prefix.Concat(scenario.Sync.Skip(1)).ToArray();
            scenario.ExpectedStitched = new[] { kickoff }.Concat(scenario.ExpectedStitched.Skip(1)).ToArray();
            scenario.ExpectedWithoutNormalization =
                prefix.Concat(scenario.ExpectedWithoutNormalization.Skip(1)).ToArray();
            scenario.ExpectedDiagnostics.V1SynchronousStartupFramesCollapsed = 4;
            scenario.VerifySampleWeights = true;

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void V1AfterV2Completion_NonCoreLibHandoff_PreservesTheCompleteBridge(int foreignFrame)
        {
            StitchScenario scenario = CreateV1AfterV2CompletionScenario();
            Frame original = scenario.Sync[4 + foreignFrame];
            Frame foreign = Frame.Library("OtherLibrary", original.SymbolName, original.Label + " (foreign)");
            scenario.Sync[4 + foreignFrame] = foreign;
            scenario.ExpectedWithoutNormalization[3 + foreignFrame] = foreign;
            scenario.ExpectedStitched = scenario.ExpectedWithoutNormalization;
            scenario.ExpectedDiagnostics.V2CompletionHandoffFramesCollapsed = 0;

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("")]
        [InlineData("B")]
        [InlineData("BR")]
        [InlineData("BRT")]
        [InlineData("RTID")]
        [InlineData("BTID")]
        [InlineData("BRID")]
        [InlineData("BRTI")]
        [InlineData("BRTD")]
        [InlineData("RBTID")]
        [InlineData("BTRID")]
        [InlineData("BERTID")]
        [InlineData("BARTID")]
        [InlineData("BNRTID")]
        [InlineData("BRTWID")]
        [InlineData("BRTII")]
        [InlineData("BRTIID")]
        [InlineData("BRTIDD")]
        public void V1AfterV2Completion_IncompleteOrInterruptedHandoff_PreservesTheCompleteBridge(string pattern)
        {
            StitchScenario scenario = CreateV1AfterV2CompletionScenario();
            Frame[] bridge = scenario.Sync.Skip(4).Take(5).ToArray();
            var frames = new Dictionary<char, Frame>
            {
                ['B'] = bridge[0],
                ['R'] = bridge[1],
                ['T'] = bridge[2],
                ['I'] = bridge[3],
                ['D'] = bridge[4],
                ['E'] = Frame.CoreLib("TestOnly.UnclassifiedFrame", "UnclassifiedFrame"),
                ['A'] = Frame.App("Scenario.ApplicationBridge"),
                ['N'] = Frame.Library("NativeBridge", "NativeBridge.Frame", "NativeBridge"),
                ['W'] = Frame.V2Wrapper(0),
            };
            Frame[] tail = pattern.Select(token => frames[token])
                .Concat(scenario.Sync.Skip(9)).ToArray();
            scenario.Sync = scenario.Sync.Take(4).Concat(tail).ToArray();
            scenario.ExpectedWithoutNormalization =
                scenario.ExpectedWithoutNormalization.Take(3).Concat(tail).ToArray();
            scenario.ExpectedStitched = scenario.ExpectedWithoutNormalization;
            scenario.ExpectedDiagnostics.V2CompletionHandoffFramesCollapsed = 0;

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void V1AfterV2Completion_UnmatchedV1Current_PreservesTheCompleteBridge()
        {
            StitchScenario scenario = CreateV1AfterV2CompletionScenario();
            Frame otherCurrent = Frame.App("Scenario+<OtherAsync>d__99.MoveNext")
                .Logical("Scenario.OtherAsync");
            scenario.Async[0] = otherCurrent;
            scenario.ExpectedStitched = scenario.ExpectedWithoutNormalization.Take(2)
                .Concat(new[] { Frame.V1MoveNextAsDispatcher, otherCurrent })
                .Concat(scenario.ExpectedWithoutNormalization.Skip(2)).ToArray();
            scenario.ExpectedWithoutNormalization = null;
            scenario.ExpectedDiagnostics.V2CompletionHandoffFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.V1SynchronousMoveNextFramesNormalized = 2;

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void V1AfterV2Completion_BridgeAssignedToLiveV2Segment_FoldsVerifiedReentrantHandoff()
        {
            StitchScenario scenario = CreateV1AfterV2CompletionScenario();
            Frame outerCurrent = Frame.App("Scenario.ActiveOuterAsync");
            scenario.AsyncSegments = new[]
            {
                AsyncSegment.Runtime(outerCurrent),
                AsyncSegment.StateMachine(scenario.Async),
            };
            scenario.Sync = scenario.Sync.Take(9).Concat(new[]
            {
                outerCurrent,
                Frame.V2Wrapper(0),
                Frame.V2InstrumentedDispatch,
                Frame.V2Dispatch,
            }).Concat(scenario.Sync.Skip(9)).ToArray();
            Frame[] previous = scenario.ExpectedWithoutNormalization;
            scenario.ExpectedWithoutNormalization = previous.Take(8)
                .Concat(new[] { outerCurrent })
                .Concat(previous.Skip(8)).ToArray();
            scenario.ExpectedStitched = previous.Take(3).Concat(new[] { outerCurrent })
                .Concat(previous.Skip(8)).ToArray();
            scenario.ExpectedDiagnostics.SegmentsProcessed = 4;
            scenario.ExpectedDiagnostics.V2InfrastructureFramesCollapsed = 4;
            scenario.ExpectedDiagnostics.V2CompletionHandoffFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.AsyncReentrantHandoffFramesCollapsed = 10;

            scenario.AssertProductionStitch();
        }

        #region private

        private static StitchScenario CreateNativeRuntimeDispatchScenario(bool resumeStub, bool methodEvents)
        {
            StitchScenario scenario = CreateRuntimeResumptionBridgeScenario(AsyncCallstackKind.RuntimeAsync);
            Frame[] native =
            {
                Frame.Library("clrjit", "JitCompiler.compileMethod", "JIT work"),
                Frame.Library("coreclr", "MethodDesc.PrepareCode", "Runtime work"),
            };
            Frame[] tail = scenario.Sync.Skip(5).ToArray();
            Frame[] entry = resumeStub
                ? new[] { Frame.App("dynamicClass.IL_STUB_AsyncResume") } : Array.Empty<Frame>();
            scenario.Sync = native.Concat(entry).Concat(new[]
            {
                Frame.V2Wrapper(1), Frame.V2InstrumentedDispatch, Frame.V2Dispatch,
            }).Concat(tail).ToArray();
            Frame[] logical = scenario.Async;
            scenario.Async = new[] { Frame.Zero }.Concat(logical).ToArray();
            scenario.MethodCompletions = methodEvents ? 1 : 0;
            Frame marker = (resumeStub ? entry[0] : Frame.V2Wrapper(1)).DispatcherFrames();
            scenario.ExpectedStitched = native.Concat(new[] { marker }).Concat(logical).Concat(tail.Skip(5)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = native.Concat(new[] { marker }).Concat(logical).Concat(tail).ToArray();
            scenario.ExpectedDiagnostics.V2InfrastructureFramesCollapsed = resumeStub ? 6 : 4;
            scenario.ConfigureTraceLog = log =>
            {
                ConfigureCapturedRuntime(log, "12.0.0");
                foreach (TraceModuleFile module in log.ModuleFiles)
                {
                    if (module.Name == "coreclr" || module.Name == "clrjit" ||
                        string.Equals(module.Name, AsyncProfilerRuntimeSupport.HostModuleName, StringComparison.OrdinalIgnoreCase))
                    {
                        module.fileName = @"C:\runtime\" + module.Name + ".dll";
                    }
                }
            };
            return scenario;
        }

        #endregion
    }
}
