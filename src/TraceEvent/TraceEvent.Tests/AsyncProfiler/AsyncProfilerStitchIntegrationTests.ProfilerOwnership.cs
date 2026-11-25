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

namespace TraceEventTests
{
    public partial class AsyncProfilerStitchIntegrationTests
    {
        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void RuntimeAsync_ProfilerDispatchWithoutWrapper_GroupsOnlyMachinery(
            bool normalization, bool completionEvents)
        {
            Frame native = Frame.Library("coreclr", "SampledNativeCallee", "NativeCallee");
            Frame[] helpers =
            {
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncProfiler+AsyncThreadContext.Acquire", "Acquire"),
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncProfilerEventSource.WriteAsyncEventBuffer", "WriteAsyncEventBuffer"),
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncProfiler+AsyncCallstack.EmitEvent", "EmitEvent"),
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncProfiler+CompleteAsyncMethod.Complete", "CompleteAsyncMethod.Complete"),
            };
            Frame completed = Frame.App("Scenario.BurnCpuAsync");
            Frame pending = Frame.App("Scenario.ParallelBranchAsync");
            Frame[] dispatch = { Frame.V2InstrumentedDispatch, Frame.V2Dispatch };
            Frame[] root = { Frame.ThreadPoolDispatch, Frame.WorkerThreadStart };
            Frame[] logical = completionEvents ? new[] { pending } : Array.Empty<Frame>();
            Frame[] structural = new[] { native }.Concat(helpers)
                .Concat(completionEvents ? new[] { Frame.V2InstrumentedDispatch.DispatcherFrames() } : dispatch)
                .Concat(logical).Concat(root).ToArray();
            var scenario = new StitchScenario
            {
                Sync = new[] { native }.Concat(helpers).Concat(dispatch).Concat(root).ToArray(),
                Async = new[] { completed, pending },
                OmitSymbolFor = native,
                MethodCompletions = completionEvents ? 1 : 0,
                ExpectedStitched = new[] { native, helpers[0].DispatcherFrames() }.Concat(logical).Concat(root).ToArray(),
                ExpectedWithoutNormalization = structural,
                ExpectedWithoutInfrastructureCleanup = structural,
                EnableNormalization = normalization,
                EnableInfrastructureCleanup = true,
                VerifySampleWeights = true,
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = completionEvents ? 2 : 0,
                    V2SyncLayoutUsed = completionEvents ? 0 : 2,
                    V2InfrastructureFramesCollapsed = completionEvents ? 4 : 0,
                    AsyncInfrastructureFramesCollapsed = (helpers.Length + (completionEvents ? 0 : 2)) * 2,
                },
                AssertTransformContext = context =>
                {
                    Assert.Equal(completionEvents ? 1 : 0, context.Segments[0].GetCompletedFrameCount(context.TimestampQpc));
                    AsyncProfilerStitchedFrame marker = Assert.Single(context.Frames.Where(frame =>
                        frame.Presentation == AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames));
                    Assert.Equal(context.Frames[1], marker);
                    Assert.Contains("AsyncThreadContext.Acquire", context.TraceLog.CodeAddresses[marker.CodeAddress].FullMethodName);
                    Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, marker.Origin);
                    Assert.DoesNotContain(context.Frames, frame =>
                        frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent ||
                        frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody);
                    if (completionEvents)
                    {
                        AsyncProfilerStitchedFrame target = Assert.Single(context.Frames.Where(frame =>
                            frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget));
                        Assert.Equal(1, target.SegmentFrameIndex);
                        Assert.Same(target.Context, marker.Context);
                    }
                    else
                    {
                        Assert.All(context.Frames, frame => Assert.Null(frame.Context));
                    }
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("foreign-helper")]
        [InlineData("unresolved-helper")]
        [InlineData("unknown-helper")]
        [InlineData("lookalike-profiler")]
        [InlineData("reversed-pair")]
        [InlineData("different-dispatch-type")]
        [InlineData("foreign-dispatch")]
        public void RuntimeAsync_ProfilerDispatchWithoutWrapper_RejectsUnverifiedHelpersOrPairs(string obstruction)
        {
            const string completeName = "System.Runtime.CompilerServices.AsyncProfiler+CompleteAsyncMethod.Complete";
            Frame helper = obstruction == "foreign-helper"
                ? Frame.Library("OtherRuntime", completeName, "ForeignComplete")
                : Frame.CoreLib(obstruction == "unknown-helper"
                    ? "System.Runtime.CompilerServices.UnknownHelper.Complete"
                    : obstruction == "lookalike-profiler"
                        ? "System.Runtime.CompilerServices.AsyncProfilerExtra+CompleteAsyncMethod.Complete"
                        : completeName, "Complete");
            Frame[] dispatch = { Frame.V2InstrumentedDispatch, Frame.V2Dispatch };
            if (obstruction == "reversed-pair")
            {
                Array.Reverse(dispatch);
            }
            else if (obstruction == "different-dispatch-type")
            {
                dispatch[1] = Frame.CoreLib("System.Threading.Tasks.OtherRuntimeAsyncTask.DispatchContinuations", "OtherDispatch");
            }
            else if (obstruction == "foreign-dispatch")
            {
                dispatch[1] = Frame.Library("OtherRuntime", Frame.V2Dispatch.SymbolName, "ForeignDispatch");
            }
            Frame[] root = { Frame.ThreadPoolDispatch, Frame.WorkerThreadStart };
            Frame[] sync = new[] { helper }.Concat(dispatch).Concat(root).ToArray();
            var scenario = new StitchScenario
            {
                Sync = sync,
                Async = new[] { Frame.App("Scenario.PendingAsync") },
                OmitSymbolFor = obstruction == "unresolved-helper" ? helper : null,
                ExpectedStitched = new[] { helper, dispatch[0].DispatcherFrames() }
                    .Concat(obstruction == "foreign-dispatch" ? new[] { dispatch[1] } : Array.Empty<Frame>())
                    .Concat(root).ToArray(),
                ExpectedWithoutNormalization = sync,
                ExpectedWithoutInfrastructureCleanup = sync,
                EnableInfrastructureCleanup = true,
                IncludeEventSourceEvents = false,
                VerifySampleWeights = true,
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    V2SyncLayoutUsed = 2,
                    AsyncInfrastructureFramesCollapsed = obstruction == "foreign-dispatch" ? 2 : 4,
                },
            };

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void RuntimeAsync_ProfilerDispatchWithoutWrapper_DoesNotCrossApplicationCallbacks()
        {
            Frame emission = Frame.CoreLib("System.Runtime.CompilerServices.AsyncProfiler+AsyncCallstack.EmitEvent", "EmitEvent");
            Frame callback = Frame.App("Scenario.SynchronousCallback");
            Frame complete = Frame.CoreLib("System.Runtime.CompilerServices.AsyncProfiler+CompleteAsyncMethod.Complete", "Complete");
            Frame[] root = { Frame.ThreadPoolDispatch, Frame.WorkerThreadStart };
            Frame[] sync = new[] { emission, callback, complete, Frame.V2InstrumentedDispatch, Frame.V2Dispatch }
                .Concat(root).ToArray();
            new StitchScenario
            {
                Sync = sync,
                Async = new[] { Frame.App("Scenario.PendingAsync") },
                ExpectedStitched = new[] { emission, callback, complete.DispatcherFrames() }.Concat(root).ToArray(),
                ExpectedWithoutNormalization = sync,
                ExpectedWithoutInfrastructureCleanup = sync,
                EnableInfrastructureCleanup = true,
                IncludeEventSourceEvents = false,
                VerifySampleWeights = true,
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    V2SyncLayoutUsed = 2,
                    AsyncInfrastructureFramesCollapsed = 6,
                },
            }.AssertProductionStitch();
        }

        [Theory]
        [InlineData(1, false, false)]
        [InlineData(1, false, true)]
        [InlineData(2, false, false)]
        [InlineData(2, false, true)]
        [InlineData(2, true, false)]
        [InlineData(2, true, true)]
        public void ProfilerOwnership_FoldsIntoTheDispatchTarget(
            int resumeCount, bool includeEmission, bool enableNormalization)
        {
            StitchScenario scenario = CreateProfilerHelperTailScenario(resumeCount, includeEmission);
            scenario.EnableNormalization = enableNormalization;
            scenario.AssertStitchedStackSource = stackSource =>
            {
                var targets = new List<AsyncProfilerCallStackSourceFrameInfo>();
                for (int i = 0; i < stackSource.CallFrameIndexLimit; i++)
                {
                    var frameIndex = (StackSourceFrameIndex)i;
                    if (stackSource.TryGetAsyncProfilerCallStackFrameInfo(frameIndex, out AsyncProfilerCallStackSourceFrameInfo info) &&
                        info.IsDispatchTarget)
                    {
                        targets.Add(info);
                        string name = stackSource.GetFrameName(frameIndex, false);
                        Assert.StartsWith(scenario.Async[0].ModuleName.ToLowerInvariant() + "!", name);
                        Assert.DoesNotContain("dispatching", name);
                    }
                }
                AsyncProfilerCallStackSourceFrameInfo target = Assert.Single(targets);
                Assert.False(target.IsParentContext);
                Assert.Equal(AsyncCallstackKind.StateMachineAsync, target.Kind);
                Assert.Equal(0, target.State);
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ProfilerOwnership_CaptureEntryWithoutResume_KeepsNativeWorkAndPendingTarget(bool enableNormalization)
        {
            StitchScenario scenario = CreateProfilerCaptureEntryScenario();
            Frame native = Frame.Library("coreclr", "MethodDesc.PrepareCode", "NativeWork");
            scenario.Sync = new[] { native }.Concat(scenario.Sync).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = new[] { native }
                .Concat(scenario.ExpectedWithoutInfrastructureCleanup).ToArray();
            scenario.ExpectedStitched = new[] { native }.Concat(scenario.ExpectedStitched).ToArray();
            scenario.OmitSymbolFor = native;
            scenario.EnableNormalization = enableNormalization;
            scenario.AssertTransformContext = context =>
            {
                Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, context.Frames[0].Origin);
                Assert.Equal(AsyncProfilerStitchedFramePresentation.Native, context.Frames[0].Presentation);
                Assert.Equal(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames, context.Frames[1].Presentation);
                AsyncProfilerStitchedFrame target = Assert.Single(context.Frames.Where(frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget));
                Assert.Same(context.Segments[0], target.Context);
                Assert.Equal(0, target.Segment.FrameStateAt(target.SegmentFrameIndex));
                Assert.DoesNotContain(context.Frames, frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent ||
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody);
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("unresolved")]
        [InlineData("foreign")]
        [InlineData("interrupted")]
        [InlineData("profile-veto")]
        public void ProfilerOwnership_CaptureEntryWithoutResume_RequiresContiguousResolvedOwnership(string obstruction)
        {
            StitchScenario scenario = CreateProfilerCaptureEntryScenario();
            Frame capture = scenario.Sync[0];
            bool protectedPrefix = obstruction == "interrupted" || obstruction == "profile-veto";
            if (obstruction == "unresolved")
            {
                scenario.OmitSymbolFor = capture;
            }
            if (obstruction == "foreign")
            {
                capture = Frame.Library("OtherLibrary", capture.SymbolName, capture.Label);
                scenario.Sync[0] = capture;
                scenario.ExpectedWithoutInfrastructureCleanup[0] = capture;
            }
            Frame[] prefix = obstruction == "interrupted"
                ? new[] { capture, Frame.App("Scenario.Callback") } : new[] { capture };
            scenario.Sync = prefix.Concat(scenario.Sync.Skip(1)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = prefix
                .Concat(scenario.ExpectedWithoutInfrastructureCleanup.Skip(1)).ToArray();
            scenario.ExpectedStitched = prefix
                .Concat(protectedPrefix ? new[] { Frame.V1MoveNextAsDispatcher } : Array.Empty<Frame>())
                .Concat(scenario.ExpectedStitched).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = protectedPrefix ? 10 : 12;
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = 0;
            if (obstruction == "profile-veto")
            {
                var support = new RejectingProfilerEntrySupport();
                scenario.RuntimeSupportForModule = module => AsyncProfilerRuntimeResolver.IsHostModule(module)
                    ? support : AsyncProfilerRuntimeSupport.Common;
            }

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void ProfilerOwnership_CaptureEntryWithoutResume_DoesNotOwnAPhysicalApplicationBody()
        {
            StitchScenario scenario = CreateProfilerCaptureEntryScenario();
            Frame capture = scenario.Sync[0];
            Frame current = Frame.App("Scenario+<DeepLevel3Async>d__3.MoveNext");
            scenario.Sync = new[] { capture, current }.Concat(scenario.Sync.Skip(1)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = scenario.ExpectedWithoutInfrastructureCleanup
                .Where(frame => frame != Frame.V1MoveNextAsDispatcher).ToArray();
            scenario.ExpectedStitched = new[] { capture }.Concat(scenario.ExpectedStitched).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 10;
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = 0;
            scenario.AssertTransformContext = context =>
            {
                Assert.Single(context.Frames.Where(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent));
                Assert.DoesNotContain(context.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget);
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("application")]
        [InlineData("foreign")]
        [InlineData("unknown-corelib")]
        [InlineData("diagnostic")]
        [InlineData("builder")]
        [InlineData("unresolved")]
        [InlineData("native")]
        public void ProfilerOwnership_PreservesUnrecognizedCallees(string callee)
        {
            StitchScenario scenario = CreateProfilerHelperTailScenario(2);
            Frame frame;
            switch (callee)
            {
                case "application":
                    frame = Frame.App("Scenario.Callback");
                    break;
                case "foreign":
                    frame = Frame.Library("OtherLibrary", "OtherLibrary.Callback", "OtherLibrary.Callback");
                    break;
                case "native":
                    frame = Frame.Library("coreclr", "SampledNativeCallee", "NativeCallee");
                    scenario.OmitSymbolFor = frame;
                    break;
                case "unknown-corelib":
                    frame = Frame.CoreLib("System.Runtime.CompilerServices.OtherHelpers.Initialize", "OtherHelpers.Initialize");
                    break;
                case "diagnostic":
                    frame = Frame.CoreLib("System.Runtime.CompilerServices.InitHelpers.InitClassSlow", "InitHelpers.InitClassSlow");
                    break;
                case "builder":
                    frame = Frame.CoreLib("System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start", "AsyncMethodBuilderCore.Start");
                    break;
                default:
                    frame = Frame.CoreLib("System.Runtime.CompilerServices.UnknownCallback.Run", "UnknownCallback.Run");
                    scenario.OmitSymbolFor = frame;
                    break;
            }
            scenario.Sync = new[] { frame }.Concat(scenario.Sync).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup =
                new[] { frame }.Concat(scenario.ExpectedWithoutInfrastructureCleanup).ToArray();
            scenario.ExpectedStitched = new[] { frame }.Concat(scenario.ExpectedStitched).ToArray();
            scenario.AssertTransformContext = context =>
            {
                Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, context.Frames[0].Origin);
                Assert.Equal(AsyncProfilerStitchedFramePresentation.Native, context.Frames[0].Presentation);
                AsyncProfilerStitchedFrame marker = Assert.Single(context.Frames.Where(f =>
                    f.Presentation == AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames));
                Assert.Equal(context.Frames[1], marker);
                Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, marker.Origin);
                Assert.Same(context.Frames[0].Context, marker.Context);
                Assert.Contains(context.Frames, f => f.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget);
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ProfilerOwnership_UnresolvedOrForeignResume_IsPreserved(bool foreign)
        {
            StitchScenario scenario = CreateProfilerHelperTailScenario();
            Frame resume = scenario.Sync[0];
            if (foreign)
            {
                resume = Frame.Library("OtherLibrary", resume.SymbolName, resume.Label);
                scenario.Sync[0] = resume;
                scenario.ExpectedWithoutInfrastructureCleanup[0] = resume;
            }
            else
            {
                scenario.OmitSymbolFor = resume;
            }
            // Without a recognized profiler candidate, existing individual cleanup still removes the dispatcher.
            scenario.ExpectedStitched = new[] { resume }.Concat(scenario.ExpectedStitched).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed -= 2;
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = 0;

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("resume-context")]
        [InlineData("dispatcher-context")]
        [InlineData("different-call-stack")]
        [InlineData("unresolved-resume")]
        [InlineData("unresolved-dispatcher")]
        [InlineData("missing-boundary")]
        [InlineData("adjacency-mismatch")]
        [InlineData("incomplete-structural-processing")]
        [InlineData("missing-trace")]
        public void ProfilerOwnership_RequiresResolvedMetadataAndValidatedContext(string missingEvidence)
        {
            StitchScenario scenario = CreateProfilerHelperTailScenario();
            scenario.EnableInfrastructureCleanup = false;
            scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = 0;
            scenario.AssertTransformContext = context =>
            {
                List<AsyncProfilerStitchedFrame> frames = context.Frames.ToList();
                var diagnostics = new AsyncProfilerCallStackDiagnostics();
                AsyncProfilerCallStackStitchingState stitchingState = context.StitchingState.Copy();
                if (missingEvidence == "resume-context" || missingEvidence == "dispatcher-context" ||
                    missingEvidence == "different-call-stack" ||
                    missingEvidence == "unresolved-resume" || missingEvidence == "unresolved-dispatcher")
                {
                    int index = missingEvidence == "dispatcher-context" ||
                                missingEvidence == "unresolved-dispatcher" ? 1 : 0;
                    AsyncProfilerStitchedFrame frame = frames[index];
                    bool unresolved = missingEvidence.StartsWith("unresolved-", StringComparison.Ordinal);
                    AsyncProfilerCallStack owner = unresolved ? frame.Context : null;
                    if (missingEvidence == "different-call-stack")
                    {
                        owner = new AsyncProfilerCallStackComputer(context.TraceLog).GetCallStacks(1234, 5000, context.TimestampQpc)[0];
                        Assert.NotSame(frame.Context, owner);
                    }
                    frames[index] = AsyncProfilerStitchedFrame.CreateSync(
                        new AsyncProfilerStitchSyncFrame(frame.CodeAddress,
                            unresolved ? MethodIndex.Invalid : frame.Method, frame.SyncClassification),
                        owner);
                }
                else if (missingEvidence == "missing-boundary")
                {
                    stitchingState.HasConsistentPhysicalAlignment = false;
                }
                else if (missingEvidence == "adjacency-mismatch")
                {
                    stitchingState.HasConsistentPhysicalAlignment = false;
                }
                else if (missingEvidence == "incomplete-structural-processing")
                {
                    stitchingState.ProcessedCallStackCount = 0;
                }
                AsyncProfilerStitchedFrame resume = frames[0];
                AsyncProfilerStitchedFrame dispatcher = frames[1];
                var pipeline = new AsyncProfilerCallStackTransformPipeline
                {
                    EnableNormalization = false,
                    EnableInfrastructureCleanup = true,
                };
                pipeline.ApplyInPlace(new AsyncProfilerCallStackTransformState(
                    missingEvidence == "missing-trace" ? null : context.TraceLog,
                    context.Segments, context.TimestampQpc, context.ClassifyBoundary, stitchingState, diagnostics, frames));

                Assert.Equal(resume, frames[0]);
                if (missingEvidence != "missing-trace")
                {
                    Assert.Equal(dispatcher, frames[1]);
                }
                Assert.Equal(0, diagnostics.ProfilerEnvelopeFramesCollapsed);
                Assert.Single(frames.Where(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget));
            };

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void ProfilerOwnership_InterruptedBeforeTheDispatcher_IsProtected()
        {
            StitchScenario scenario = CreateProfilerHelperTailScenario();
            Frame callback = Frame.App("Scenario.Callback");
            scenario.Sync = new[] { scenario.Sync[0], callback }.Concat(scenario.Sync.Skip(1)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup =
                new[] { scenario.Sync[0], callback }
                    .Concat(scenario.ExpectedWithoutInfrastructureCleanup.Skip(1)).ToArray();
            scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup.Take(3)
                .Concat(scenario.ExpectedStitched).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 10;
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = 0;

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void ProfilerOwnership_CallbackAboveTheEntry_InterruptsHelperGrouping()
        {
            StitchScenario scenario = CreateProfilerHelperTailScenario(2);
            Frame callback = Frame.App("Scenario.Callback");
            scenario.Sync = new[] { scenario.Sync[0], callback }.Concat(scenario.Sync.Skip(1)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = new[] { scenario.Sync[0], callback }
                .Concat(scenario.ExpectedWithoutInfrastructureCleanup.Skip(1)).ToArray();
            scenario.ExpectedStitched = new[] { scenario.Sync[0], callback }.Concat(scenario.ExpectedStitched).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed -= 2;
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed -= 2;

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void ProfilerOwnership_RejectedRootBridge_RemainsProtectedAfterPrefixRemoval()
        {
            StitchScenario scenario = CreateProfilerHelperTailScenario();
            Frame callback = Frame.App("Scenario.Callback");
            scenario.Sync[scenario.Sync.Length - 3] = callback;
            scenario.ExpectedWithoutInfrastructureCleanup[scenario.ExpectedWithoutInfrastructureCleanup.Length - 3] = callback;
            scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup.Skip(2).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 4;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void ProfilerOwnership_PhysicallyMatchedCurrent_PreservesTheApplicationAnchor()
        {
            StitchScenario scenario = CreateProfilerHelperTailScenario();
            Frame current = Frame.App("Scenario+<DeepLevel3Async>d__3.MoveNext");
            scenario.Sync = scenario.Sync.Take(1).Concat(new[] { current }).Concat(scenario.Sync.Skip(1)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup =
                scenario.ExpectedWithoutInfrastructureCleanup.Where(frame => frame != Frame.V1MoveNextAsDispatcher).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 12;
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = 2;

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ProfilerOwnership_CaptureWithoutEmission_PreservesDiagnosticsAndUnresolvedInitialization(
            bool initializeDiagnostics)
        {
            StitchScenario scenario = CreateProfilerEnvelopeScenario();
            Frame[] prefix = initializeDiagnostics
                ? scenario.Sync.Take(8).Concat(new[] { scenario.Sync[10] }).ToArray()
                : new[] { scenario.Sync[6], scenario.Sync[7], scenario.Sync[10] };
            if (initializeDiagnostics)
            {
                scenario.OmitSymbolFor = prefix[2];
            }
            scenario.Sync = prefix.Concat(scenario.Sync.Skip(14)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = prefix
                .Concat(scenario.ExpectedWithoutInfrastructureCleanup.Skip(14)).ToArray();
            int retained = initializeDiagnostics ? 5 : 0;
            scenario.ExpectedStitched = prefix.Take(retained).Concat(scenario.ExpectedStitched.Skip(5)).ToArray();
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = initializeDiagnostics ? 10 : 8;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed =
                10 + scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed;
            scenario.AssertTransformContext = context =>
            {
                AsyncProfilerStitchedFrame target = Assert.Single(context.Frames.Where(frame =>
                    frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget));
                Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget, target.Origin);
                Assert.Equal(0, target.Segment.FrameStateAt(target.SegmentFrameIndex));
                Assert.Equal(retained + 4, context.Frames.Count(frame =>
                    frame.ContextKind == AsyncProfilerStitchedFrameContextKind.Active));
            };

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void ProfilerOwnership_DispatcherWithoutProfilerEntry_PreservesApplicationWork()
        {
            StitchScenario scenario = CreateProfilerHelperTailScenario();
            Frame work = Frame.App("Scenario.Work");
            scenario.Sync[0] = work;
            scenario.ExpectedWithoutInfrastructureCleanup[0] = work;
            scenario.ExpectedStitched = new[] { work }.Concat(scenario.ExpectedStitched).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 12;
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = 0;

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("callee-context")]
        [InlineData("different-call-stack")]
        [InlineData("nested-boundary")]
        [InlineData("physical-anchor")]
        [InlineData("logical-call-stack")]
        public void ProfilerOwnership_DoesNotCrossContextBoundariesOrApplicationAnchors(string obstruction)
        {
            StitchScenario scenario = CreateProfilerHelperTailScenario();
            Frame callee = Frame.App("Scenario.Callback");
            scenario.Sync = new[] { callee }.Concat(scenario.Sync).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = new[] { callee }
                .Concat(scenario.ExpectedWithoutInfrastructureCleanup).ToArray();
            scenario.EnableInfrastructureCleanup = false;
            scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;
            scenario.AssertTransformContext = context =>
            {
                List<AsyncProfilerStitchedFrame> frames = context.Frames.ToList();
                AsyncProfilerStitchedFrame original = frames[0];
                AsyncProfilerStitchedFrame entry = frames[1];
                AsyncProfilerCallStack owner = original.Context;
                if (obstruction == "different-call-stack")
                {
                    owner = new AsyncProfilerCallStackComputer(context.TraceLog).GetCallStacks(1234, 5000, context.TimestampQpc)[0];
                    Assert.NotSame(original.Context, owner);
                }
                frames[0] = obstruction == "physical-anchor"
                    ? AsyncProfilerStitchedFrame.CreateAsyncCurrent(original.CodeAddress, original.Method, owner, 0)
                    : obstruction == "logical-call-stack"
                        ? AsyncProfilerStitchedFrame.CreateAsync(owner, 0)
                        : obstruction == "nested-boundary"
                            ? frames[2]
                            : AsyncProfilerStitchedFrame.CreateSync(
                                new AsyncProfilerStitchSyncFrame(original.CodeAddress, original.Method, original.SyncClassification),
                                obstruction == "callee-context" ? null : owner);
                AsyncProfilerStitchedFrame protectedFrame = frames[0];
                var diagnostics = new AsyncProfilerCallStackDiagnostics { SegmentsProcessed = context.Segments.Count };
                var pipeline = new AsyncProfilerCallStackTransformPipeline
                {
                    EnableNormalization = false,
                    EnableInfrastructureCleanup = true,
                };
                pipeline.ApplyInPlace(new AsyncProfilerCallStackTransformState(
                    context.TraceLog, context.Segments, context.TimestampQpc,
                    context.ClassifyBoundary, context.StitchingState.Copy(), diagnostics, frames));

                Assert.Equal(protectedFrame, frames[0]);
                Assert.Equal(entry, frames[1]);
                Assert.Equal(0, diagnostics.ProfilerEnvelopeFramesCollapsed);
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, (int)AsyncCallstackKind.StateMachineAsync)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, (int)AsyncCallstackKind.RuntimeAsync)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, (int)AsyncCallstackKind.StateMachineAsync)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, (int)AsyncCallstackKind.RuntimeAsync)]
        public void ProfilerOwnership_NestedProtocols_PreserveOuterCallStackAndInterContextWork(
            int innerKindValue, int outerKindValue)
        {
            var innerKind = (AsyncCallstackKind)innerKindValue;
            var outerKind = (AsyncCallstackKind)outerKindValue;
            StitchScenario source = CreateProfilerEnvelopeScenario();
            Frame[] prefix = { source.Sync[2], source.Sync[6], source.Sync[7], source.Sync[10] };
            Frame innerCurrent = innerKind == AsyncCallstackKind.StateMachineAsync
                ? source.Async[0] : Frame.App("Scenario.InnerV2Async");
            Frame innerParent = innerKind == AsyncCallstackKind.StateMachineAsync
                ? source.Async[1] : Frame.App("Scenario.InnerV2ParentAsync");
            Frame outerIdentity = outerKind == AsyncCallstackKind.StateMachineAsync
                ? Frame.App("Scenario+<OuterAsync>d__2.MoveNext") : Frame.App("Scenario.OuterV2Async");
            Frame outerCurrent = outerKind == AsyncCallstackKind.StateMachineAsync
                ? outerIdentity.Logical("Scenario.OuterAsync") : outerIdentity;
            Frame outerParent = outerKind == AsyncCallstackKind.StateMachineAsync
                ? Frame.App("Scenario+<OuterParentAsync>d__1.MoveNext").Logical("Scenario.OuterParentAsync")
                : Frame.App("Scenario.OuterV2ParentAsync");
            Frame bridge = Frame.App("Scenario.InterContextCallback");
            Frame infrastructure = source.Sync[15];
            var sync = prefix.ToList();
            if (innerKind == AsyncCallstackKind.StateMachineAsync)
            {
                sync.Add(Frame.V1MoveNextAsDispatcher);
                sync.Add(infrastructure);
            }
            else
            {
                sync.Add(innerCurrent);
                AddSyntheticDispatcher(sync, innerKind, null, null);
            }
            sync.Add(bridge);
            sync.Add(outerIdentity);
            if (outerKind == AsyncCallstackKind.StateMachineAsync)
            {
                sync.Add(Frame.V1MoveNextAsDispatcher);
                sync.Add(infrastructure);
            }
            else
            {
                AddSyntheticDispatcher(sync, outerKind, null, null);
            }
            sync.Add(Frame.ThreadPoolDispatch);
            sync.Add(Frame.WorkerThreadStart);
            Frame[] logical =
            {
                innerCurrent, innerParent, bridge, outerCurrent, outerParent,
                Frame.ThreadPoolDispatch, Frame.WorkerThreadStart,
            };
            int v1Segments = (innerKind == AsyncCallstackKind.StateMachineAsync ? 1 : 0) +
                (outerKind == AsyncCallstackKind.StateMachineAsync ? 1 : 0);
            int ownedCount = 3 + (innerKind == AsyncCallstackKind.StateMachineAsync ? 1 : 0);
            var scenario = new StitchScenario
            {
                Sync = sync.ToArray(),
                AsyncSegments = new[]
                {
                    outerKind == AsyncCallstackKind.StateMachineAsync
                        ? AsyncSegment.StateMachine(outerCurrent, outerParent)
                        : AsyncSegment.Runtime(outerCurrent, outerParent),
                    innerKind == AsyncCallstackKind.StateMachineAsync
                        ? AsyncSegment.StateMachine(innerCurrent, innerParent)
                        : AsyncSegment.Runtime(innerCurrent, innerParent),
                },
                OmitSymbolFor = prefix[0],
                EnableInfrastructureCleanup = true,
                VerifySampleWeights = true,
                ExpectedStitched = prefix.Take(1).Concat(logical).ToArray(),
                ExpectedWithoutInfrastructureCleanup = prefix
                    .Concat(innerKind == AsyncCallstackKind.StateMachineAsync
                        ? new[] { Frame.V1MoveNextAsDispatcher } : Array.Empty<Frame>())
                    .Concat(logical).ToArray(),
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 4,
                    V1InlineFallbackUsed = v1Segments * 2,
                    V1InfrastructureFramesCollapsed = v1Segments * 2,
                    V2InfrastructureFramesCollapsed = (2 - v1Segments) * 4,
                    AsyncInfrastructureFramesCollapsed = ownedCount * 2,
                    ProfilerEnvelopeFramesCollapsed = ownedCount * 2,
                },
                AssertTransformContext = context =>
                {
                    Assert.Same(context.Segments[0], context.Frames[0].Context);
                    AsyncProfilerStitchedFrame inner = context.Frames.First(frame =>
                        frame.Origin != AsyncProfilerStitchedFrameOrigin.Sync);
                    Assert.Equal(innerKind == AsyncCallstackKind.StateMachineAsync
                        ? AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget : AsyncProfilerStitchedFrameOrigin.AsyncCurrent,
                        inner.Origin);
                    Assert.Contains(context.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent &&
                        ReferenceEquals(frame.Context, context.Segments[1]));
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void ProfilerEnvelope_PreservesCalleesAndGroupsOnlyVerifiedHelpers(bool physicalCurrent, bool enableNormalization)
        {
            StitchScenario scenario = CreateProfilerEnvelopeScenario(physicalCurrent);
            scenario.EnableNormalization = enableNormalization;

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineDiagnostics`1[Scenario+<DeepLevel3Async>d__3].ResolveMethodId()")]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineDiagnostics`1[Scenario+<DeepLevel3Async>d__3].GetState(!!0&)")]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineDiagnostics`1[Scenario+<DeepLevel3Async>d__3]..cctor()")]
        [InlineData("System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncStateMachineBox`1[System.Int64,Scenario+<DeepLevel3Async>d__3]..ctor()")]
        [InlineData("System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncProfilerAsyncStateMachineBox`1[System.Int64,Scenario+<DeepLevel3Async>d__3].ClearStateUponCompletion()")]
        public void ProfilerHelpers_ExtendTheVerifiedSpanButDoNotRestartAboveAnInitializationBreak(string method)
        {
            StitchScenario scenario = CreateProfilerHelperTailScenario();
            Frame helper = Frame.CoreLib(method, "ContiguousHelper");
            Frame initializer = Frame.CoreLib(
                "System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBaseSlow", "StaticInitialization");
            Frame callee = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncStateMachineDiagnostics`1[Scenario+<DeepLevel3Async>d__3].ResolveStateFieldOffset()",
                "DiagnosticsAboveBreak");
            Frame[] prefix = { callee, initializer, helper };
            scenario.Sync = prefix.Concat(scenario.Sync).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup =
                prefix.Concat(scenario.ExpectedWithoutInfrastructureCleanup).ToArray();
            scenario.ExpectedStitched = prefix.Take(2).Concat(scenario.ExpectedStitched).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed += 2;
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed += 2;

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void ProfilerHelpers_ASeparateDispatcherStartsANewSpanAfterAnApplicationBreak()
        {
            Frame inner = Frame.App("Scenario+<InnerAsync>d__3.MoveNext").Logical("Scenario.InnerAsync");
            Frame outer = Frame.App("Scenario+<OuterAsync>d__2.MoveNext").Logical("Scenario.OuterAsync");
            Frame innerHelper = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncStateMachineDiagnostics`1[Scenario+<InnerAsync>d__3].ResolveMethodId()",
                "InnerDiagnostics");
            Frame outerHelper = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncStateMachineDiagnostics`1[Scenario+<OuterAsync>d__2].ResolveMethodId()",
                "OuterDiagnostics");
            Frame callback = Frame.App("Scenario.InterContextCallback");
            var scenario = new StitchScenario
            {
                Sync = new[]
                {
                    innerHelper, Frame.V1MoveNextAsDispatcher, callback,
                    outerHelper, Frame.V1MoveNextAsDispatcher,
                    Frame.ThreadPoolDispatch, Frame.WorkerThreadStart,
                },
                AsyncSegments = new[]
                {
                    AsyncSegment.StateMachine(outer),
                    AsyncSegment.StateMachine(inner),
                },
                EnableInfrastructureCleanup = true,
                VerifySampleWeights = true,
                IncludeEventSourceEvents = false,
                ExpectedStitched = new[] { inner, callback, outer, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart },
                ExpectedWithoutInfrastructureCleanup = new[]
                {
                    innerHelper, Frame.V1MoveNextAsDispatcher, inner, callback,
                    outerHelper, Frame.V1MoveNextAsDispatcher, outer,
                    Frame.ThreadPoolDispatch, Frame.WorkerThreadStart,
                },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 4,
                    V1InlineFallbackUsed = 4,
                    AsyncInfrastructureFramesCollapsed = 8,
                    ProfilerEnvelopeFramesCollapsed = 4,
                },
                AssertTransformContext = context =>
                {
                    AsyncProfilerStitchedFrame[] markers = context.Frames.Where(frame =>
                        frame.Presentation == AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames).ToArray();
                    Assert.Equal(2, markers.Length);
                    Assert.NotSame(markers[0].Context, markers[1].Context);
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(9)]
        [InlineData(10)]
        [InlineData(11)]
        public void ProfilerEnvelope_UnresolvedCallee_StopsHelperGrouping(int index)
        {
            StitchScenario scenario = CreateProfilerEnvelopeScenario();
            Frame[] logicalAndRoot = scenario.ExpectedStitched.Skip(5).ToArray();
            // All Resume frames share one symbol, so index 10 also unresolves indexes 12 and 13.
            scenario.OmitSymbolFor = scenario.Sync[index];
            if (index == 10)
            {
                scenario.ExpectedStitched = scenario.Sync.Take(15).Concat(logicalAndRoot).ToArray();
                scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 10;
                scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = 0;
            }
            else
            {
                int retained = Math.Max(5, index + 1);
                scenario.ExpectedStitched = scenario.Sync.Take(retained).Concat(logicalAndRoot).ToArray();
                scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = (15 - retained) * 2;
                scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed =
                    10 + scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed;
            }

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(4)]
        [InlineData(5)]
        [InlineData(6)]
        [InlineData(7)]
        [InlineData(8)]
        [InlineData(9)]
        [InlineData(10)]
        [InlineData(11)]
        [InlineData(12)]
        [InlineData(13)]
        public void ProfilerEnvelope_ForeignCallee_StopsHelperGrouping(int index)
        {
            StitchScenario scenario = CreateProfilerEnvelopeScenario();
            Frame[] logicalAndRoot = scenario.ExpectedStitched.Skip(5).ToArray();
            Frame original = scenario.Sync[index];
            Frame foreign = Frame.Library("OtherLibrary", original.SymbolName, original.Label);
            scenario.Sync[index] = foreign;
            scenario.ExpectedWithoutInfrastructureCleanup[index] = foreign;
            if (index == 13)
            {
                scenario.ExpectedStitched = scenario.Sync.Take(15).Concat(logicalAndRoot).ToArray();
                scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 10;
                scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = 0;
            }
            else
            {
                int retained = Math.Max(5, index + 1);
                scenario.ExpectedStitched = scenario.Sync.Take(retained).Concat(logicalAndRoot).ToArray();
                scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = (15 - retained) * 2;
                scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed =
                    10 + scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed;
            }

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("application")]
        [InlineData("native")]
        [InlineData("unknown-corelib")]
        [InlineData("missing-capture")]
        [InlineData("missing-emission")]
        [InlineData("missing-resume")]
        [InlineData("reordered")]
        public void ProfilerEnvelope_CalleeLayout_PreservesFramesOutsideTheVerifiedHelperSpan(string interruption)
        {
            StitchScenario scenario = CreateProfilerEnvelopeScenario();
            Frame[] logicalAndRoot = scenario.ExpectedStitched.Skip(5).ToArray();
            var prefix = scenario.Sync.Take(14).ToList();
            int retained = 5;
            switch (interruption)
            {
                case "application":
                    prefix.Insert(1, Frame.App("Scenario.UserInitializer"));
                    retained++;
                    break;
                case "native":
                    prefix.Insert(1, Frame.Library("NativeBridge", "Native.Initializer", "Native.Initializer"));
                    retained++;
                    break;
                case "unknown-corelib":
                    prefix.Insert(1, Frame.CoreLib("System.Runtime.CompilerServices.OtherHelpers.Initialize", "OtherHelpers.Initialize"));
                    retained++;
                    break;
                case "missing-capture":
                    prefix.RemoveAt(7);
                    break;
                case "missing-emission":
                    prefix.RemoveRange(8, 2);
                    break;
                case "missing-resume":
                    prefix.RemoveAll(frame => frame.Label == "Profiler.Resume");
                    break;
                case "reordered":
                    Frame capture = prefix[7];
                    prefix[7] = prefix[10];
                    prefix[10] = capture;
                    break;
            }
            scenario.Sync = prefix.Concat(scenario.Sync.Skip(14)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = prefix
                .Concat(scenario.ExpectedWithoutInfrastructureCleanup.Skip(14)).ToArray();
            if (interruption == "missing-resume")
            {
                scenario.ExpectedStitched = prefix.Concat(new[] { Frame.V1MoveNextAsDispatcher })
                    .Concat(logicalAndRoot).ToArray();
                scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 10;
                scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = 0;
            }
            else
            {
                scenario.ExpectedStitched = prefix.Take(retained).Concat(logicalAndRoot).ToArray();
                scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = (prefix.Count - retained + 1) * 2;
                scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed =
                    10 + scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed;
            }

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void ProfilerEnvelope_RejectedRootBridge_StillFoldsTheLeafEnvelope()
        {
            StitchScenario scenario = CreateProfilerEnvelopeScenario();
            Frame callback = Frame.App("Scenario.SynchronousCallback");
            scenario.Sync[scenario.Sync.Length - 3] = callback;
            scenario.ExpectedWithoutInfrastructureCleanup[scenario.ExpectedWithoutInfrastructureCleanup.Length - 3] = callback;
            scenario.ExpectedStitched = scenario.Sync.Take(5)
                .Concat(scenario.ExpectedWithoutInfrastructureCleanup.Skip(15)).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 20;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void ProfilerEnvelope_MissingBoundary_RetainsTheUnverifiedEnvelope()
        {
            StitchScenario scenario = CreateProfilerEnvelopeScenario();
            scenario.Sync = scenario.Sync.Take(14).Concat(scenario.Sync.Skip(16)).ToArray();
            scenario.ExpectedStitched = scenario.Sync.Concat(scenario.Async.Skip(1)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = scenario.ExpectedStitched;
            scenario.ExpectedDiagnostics = new CallStackDiagnosticsExpectation
            {
                SegmentsProcessed = 2,
                BoundariesNotFound = 2,
                V1InlineFallbackUsed = 2,
            };

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void ProfilerEnvelope_PreservesParentContextContexts()
        {
            StitchScenario scenario = CreateProfilerEnvelopeScenario();
            Frame owner = Frame.App("Scenario.OwnerAsync");
            scenario.ContextParent = AsyncSegment.Runtime(owner);
            scenario.EnableParentContexts = true;
            scenario.ExpectedStitched = scenario.ExpectedStitched.Take(8)
                .Concat(new[] { owner }).Concat(scenario.ExpectedStitched.Skip(8)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup =
                scenario.ExpectedWithoutInfrastructureCleanup.Take(18)
                .Concat(new[] { owner }).Concat(scenario.ExpectedWithoutInfrastructureCleanup.Skip(18)).ToArray();

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ProfilerEnvelope_CalledFromRuntimeAsync_PreservesThePhysicalCurrentMethod(bool unresolvedCallee)
        {
            StitchScenario scenario = CreateProfilerEnvelopeScenario();
            Frame current = Frame.App("Scenario.RuntimeAsync");
            Frame parent = Frame.App("Scenario.RuntimeParentAsync");
            Frame[] prefix = scenario.Sync.Take(14).ToArray();
            Frame[] tail = scenario.Sync.Skip(scenario.Sync.Length - 7).ToArray();
            scenario.Kind = AsyncCallstackKind.RuntimeAsync;
            scenario.Async = new[] { current, parent };
            scenario.AsyncStates = null;
            scenario.Sync = prefix.Concat(new[]
            {
                current, Frame.V2Wrapper(0), Frame.V2InstrumentedDispatch, Frame.V2Dispatch,
            }).Concat(tail).ToArray();
            if (unresolvedCallee)
            {
                scenario.OmitSymbolFor = prefix[2];
            }
            scenario.ExpectedStitched = prefix.Take(5)
                .Concat(new[] { current, parent }).Concat(tail.Skip(5)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = prefix.Concat(new[] { current, parent }).Concat(tail).ToArray();
            scenario.ExpectedDiagnostics.V1InlineFallbackUsed = 0;
            scenario.ExpectedDiagnostics.V1InfrastructureFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.V2InfrastructureFramesCollapsed = 4;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 28;
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = 18;

            scenario.AssertProductionStitch();
        }

        #region private

        private static StitchScenario CreateProfilerHelperTailScenario(
            int resumeCount = 1, bool includeEmission = false)
        {
            StitchScenario scenario = CreateProfilerEnvelopeScenario();
            var prefix = new List<Frame>();
            if (includeEmission)
            {
                prefix.Add(scenario.Sync[8]);
                prefix.Add(scenario.Sync[9]);
            }
            prefix.AddRange(Enumerable.Repeat(scenario.Sync[10], resumeCount));
            scenario.Sync = prefix.Concat(scenario.Sync.Skip(14)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = prefix
                .Concat(scenario.ExpectedWithoutInfrastructureCleanup.Skip(14)).ToArray();
            scenario.ExpectedStitched = scenario.ExpectedStitched.Skip(5).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 10 + (prefix.Count + 1) * 2;
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = (prefix.Count + 1) * 2;
            return scenario;
        }

        private static StitchScenario CreateProfilerCaptureEntryScenario()
        {
            StitchScenario scenario = CreateProfilerHelperTailScenario();
            Frame capture = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncProfiler+AsyncCallstack.CaptureStateMachineAsyncCallstack(unsigned int8[],int32&,value class CaptureStateMachineAsyncCallstackState&)",
                "Profiler.Capture");
            scenario.Sync[0] = capture;
            scenario.ExpectedWithoutInfrastructureCleanup[0] = capture;
            return scenario;
        }

        #endregion
    }
}
