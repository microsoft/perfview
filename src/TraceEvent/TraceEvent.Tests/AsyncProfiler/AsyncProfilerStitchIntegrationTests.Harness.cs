// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;

using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Computers;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.EventPipe;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;
using Microsoft.Diagnostics.Tracing.Stacks;

using Xunit;

using static Microsoft.Diagnostics.Tracing.Computers.AsyncProfilerRuntimeSupport;

namespace TraceEventTests
{
    public partial class AsyncProfilerStitchIntegrationTests
    {
        #region private

        private static void AssertScenarioBrokenRoots(MutableTraceEventStackSource source, int expectedCount)
        {
            bool found = false;
            source.ForEach(sample =>
            {
                int broken = 0;
                bool scenario = false;
                for (var stack = sample.StackIndex; stack != StackSourceCallStackIndex.Invalid; stack = source.GetCallerIndex(stack))
                {
                    StackSourceFrameIndex frame = source.GetFrameIndex(stack);
                    string name = source.GetFrameName(frame, false);
                    scenario |= name.Contains("Scenario");
                    if (name != "BROKEN")
                    {
                        continue;
                    }
                    broken++;
                    Assert.NotEqual(StackSourceFrameIndex.Broken, frame);
                    Assert.Equal(CodeAddressIndex.Invalid, source.GetFrameCodeAddress(frame));
                    Assert.False(source.TryGetAsyncProfilerCallStackFrameInfo(frame, out _));
                }
                if (scenario)
                {
                    found = true;
                    Assert.Equal(expectedCount, broken);
                }
            });
            Assert.True(found);
        }

        private static StitchScenario CreateV1CompletedUnwindScenario(bool truncated, Frame awaiterCallback = null)
        {
            Frame deep = Frame.App("Scenario+<DeepAsync>d__1.MoveNext");
            Frame child = Frame.App("Scenario+<ChildAsync>d__2.MoveNext");
            Frame current = Frame.App("Scenario+<CurrentAsync>d__3.MoveNext");
            Frame root = Frame.App("Scenario+<RootAsync>d__4.MoveNext");
            const string box = "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncStateMachineBox`1";
            Frame currentBox = Frame.CoreLib(
                box + "[System.Int64,Scenario+<CurrentAsync>d__3].MoveNext(class System.Threading.Thread,value class Flags)",
                "CurrentBox.MoveNext");
            Frame childBox = Frame.CoreLib(
                box + "[System.Int64,Scenario+<ChildAsync>d__2].MoveNext(class System.Threading.Thread)",
                "ChildBox.MoveNext");
            Frame callback = Frame.CoreLib(
                box + "[System.Int64,Scenario+<ChildAsync>d__2].ExecutionContextCallback(class System.Object)",
                "ChildBox.ExecutionContextCallback");
            Frame deepBox = Frame.CoreLib(
                box + "[System.Int64,Scenario+<DeepAsync>d__1].MoveNext()",
                "DeepBox.MoveNext");
            Frame continuation = Frame.CoreLib(
                "System.Threading.Tasks.AwaitTaskContinuation.RunOrScheduleAction(class System.Runtime.CompilerServices.IAsyncStateMachineBox,bool)",
                "AwaitTaskContinuation.RunOrScheduleAction");
            Frame runContinuations = Frame.CoreLib("System.Threading.Tasks.Task.RunContinuations", "Task.RunContinuations");
            Frame result = Frame.CoreLib("System.Threading.Tasks.Task`1[System.Int64].TrySetResult", "Task.TrySetResult");
            Frame completion = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].SetExistingTaskResult",
                "Builder.SetExistingTaskResult");
            Frame[] unwind =
            {
                currentBox, continuation, runContinuations, result, completion, child, callback, childBox,
                continuation, runContinuations, result, completion, deep, deepBox,
            };
            return new StitchScenario
            {
                Kind = AsyncCallstackKind.StateMachineAsync,
                Sync = unwind.SelectMany(frame => awaiterCallback != null && frame == continuation
                    ? new[] { awaiterCallback, frame } : new[] { frame }).Concat(truncated ? Array.Empty<Frame>() :
                    new[] { Frame.V1MoveNextAsDispatcher, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart }).ToArray(),
                Async = new[] { deep, child, current, root },
                AsyncStates = new[] { 0, 0, 0, 7 },
                MethodCompletions = 4,
                EnableFrameAnnotations = true,
                SkipNormalStackAssertion = true,
                VerifySampleWeights = true,
            };
        }

        private static StitchScenario CreateV1CompletedUnwindYieldBridgeScenario(params Frame[] helpers)
        {
            StitchScenario scenario = CreateV1CompletedUnwindScenario(false);
            int unwindLength = scenario.Sync.Length - 3;
            Frame native = Frame.Library("coreclr", "MethodDesc.PrepareCode", "NativeWork");
            Frame marker = scenario.Sync[0].DispatcherFrames();
            Frame root = scenario.Async.Last().Logical("Scenario.RootAsync", -2);
            Frame[] tail = { Frame.ThreadPoolDispatch, Frame.WorkerThreadStart };
            scenario.Sync = new[] { native }.Concat(scenario.Sync.Take(scenario.Sync.Length - 2))
                .Concat(helpers).Concat(tail).ToArray();
            scenario.EnableInfrastructureCleanup = true;
            scenario.ExpectedStitched = new[] { native, marker, root }.Concat(tail).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = new[] { native, marker, root }.Concat(helpers).Concat(tail).ToArray();
            scenario.ExpectedWithoutNormalization = scenario.Sync.Where(frame => frame != Frame.V1MoveNextAsDispatcher).ToArray();
            scenario.ExpectedDiagnostics = new CallStackDiagnosticsExpectation
            {
                SegmentsProcessed = 2,
                V1CompletionUnwindFramesCollapsed = unwindLength * 2,
                AsyncInfrastructureFramesCollapsed = helpers.Length * 2,
                RuntimeResumptionBridgeFramesCollapsed = helpers.Length * 2,
            };
            return scenario;
        }

        private static StitchScenario CreateProfilerEnvelopeScenario(bool physicalCurrent = false)
        {
            StitchScenario scenario = CreateRuntimeResumptionBridgeScenario(AsyncCallstackKind.StateMachineAsync);
            Frame current = Frame.App("Scenario+<DeepLevel3Async>d__3.MoveNext");
            Frame logicalCurrent = current.Logical("Scenario.DeepLevel3Async");
            Frame parent = Frame.App("Scenario+<DeepLevel2Async>d__2.MoveNext").Logical("Scenario.DeepLevel2Async");
            Frame root = Frame.App("Scenario+<DeepLevel1Async>d__1.MoveNext").Logical("Scenario.DeepLevel1Async");
            Frame[] prefix =
            {
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncStateMachineDiagnostics`1[Scenario+<DeepLevel3Async>d__3]..cctor()", "Diagnostics..cctor"),
                Frame.CoreLib("System.Runtime.CompilerServices.InitHelpers.CallClassConstructor", "InitHelpers.CallClassConstructor"),
                Frame.CoreLib("System.Runtime.CompilerServices.InitHelpers.InitClassHelper", "InitHelpers.InitClassHelper"),
                Frame.CoreLib("System.Runtime.CompilerServices.InitHelpers.InitClassSlow", "InitHelpers.InitClassSlow"),
                Frame.CoreLib("System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBaseSlow", "StaticsHelpers.GetNonGCStaticBaseSlow"),
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncStateMachineDiagnostics`1[Scenario+<DeepLevel3Async>d__3].get_MethodId()", "Diagnostics.get_MethodId"),
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncStateMachineBox`1[System.Int64,Scenario+<DeepLevel3Async>d__3].System.Runtime.CompilerServices.IAsyncStateMachineBox.GetDiagnosticData(unsigned int64&,int32&,class System.Object&)", "Box.GetDiagnosticData"),
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncProfiler+AsyncCallstack.CaptureStateMachineAsyncCallstack", "Profiler.Capture"),
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncProfiler+AsyncCallstack.EmitAsyncCallstack(unsigned int8[],int32&,!!0&)", "Profiler.EmitAsyncCallstack"),
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncProfiler+AsyncCallstack.EmitEvent", "Profiler.EmitEvent"),
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncProfiler+ResumeAsyncContext.Resume", "Profiler.Resume"),
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncProfiler+SyncPoint.ResetContext", "Profiler.ResetContext"),
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncProfiler+ResumeAsyncContext.Resume", "Profiler.Resume"),
                Frame.CoreLib("System.Runtime.CompilerServices.AsyncProfiler+ResumeAsyncContext.Resume", "Profiler.Resume"),
            };
            Frame[] logical = { logicalCurrent, parent, root };
            Frame[] tail = scenario.Sync.Skip(scenario.Sync.Length - 7).ToArray();
            scenario.Sync = prefix.Concat(physicalCurrent ? new[] { current } : Array.Empty<Frame>())
                .Concat(scenario.Sync.Skip(2)).ToArray();
            scenario.Async = new[] { logicalCurrent, parent, root };
            scenario.AsyncStates = new[] { 0, 1, 1 };
            scenario.ExpectedStitched = prefix.Take(5).Concat(logical).Concat(tail.Skip(5)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = prefix
                .Concat(physicalCurrent ? Array.Empty<Frame>() : new[] { Frame.V1MoveNextAsDispatcher })
                .Concat(logical).Concat(tail).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = physicalCurrent ? 28 : 30;
            scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = physicalCurrent ? 18 : 20;
            return scenario;
        }

        private static StitchScenario CreateCallbackCompletionBridgeScenario(
            AsyncCallstackKind kind, bool includeAwaiterHelpers = false)
        {
            StitchScenario scenario = CreateRuntimeResumptionBridgeScenario(kind);
            Frame[] logical = scenario.ExpectedStitched.Take(3).ToArray();
            Frame[] chain =
            {
                Frame.CoreLib("System.Threading.Tasks.Task.RunContinuations", "Task.RunContinuations"),
                Frame.CoreLib("System.Threading.Tasks.Task`1[System.Boolean].TrySetResult(!0)", "Task.TrySetResult"),
                Frame.CoreLib("System.Threading.Tasks.TaskCompletionSource`1[System.Boolean].SetResult(!0)",
                    "TaskCompletionSource.SetResult"),
            };
            if (includeAwaiterHelpers)
            {
                chain = new[]
                {
                    Frame.CoreLib(
                        "System.Runtime.CompilerServices.TaskAwaiter+<>c::<OutputWaitEtwEvents>b__12_0(System.Action, System.Threading.Tasks.Task)",
                        "TaskAwaiter.Callback"),
                    Frame.CoreLib("System.Threading.Tasks.AwaitTaskContinuation.RunOrScheduleAction",
                        "AwaitTaskContinuation.RunOrScheduleAction"),
                }.Concat(chain).ToArray();
            }
            Frame[] tail =
            {
                Frame.App("Scenario.SynchronousCallback"),
                Frame.CoreLib("System.Threading.TimerQueueTimer.Fire", "TimerQueueTimer.Fire"),
                Frame.ThreadPoolDispatch,
                Frame.WorkerThreadStart,
            };
            scenario.Sync = scenario.Sync.Take(scenario.Sync.Length - 7).Concat(chain).Concat(tail).ToArray();
            scenario.ExpectedStitched = logical.Concat(chain).Concat(tail).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = logical.Concat(chain).Concat(tail).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;
            scenario.AssertStitchedStackSource = stackSource =>
            {
                for (int i = 0; i < stackSource.CallFrameIndexLimit; i++)
                {
                    if (stackSource.TryGetAsyncProfilerCallStackFrameInfo((StackSourceFrameIndex)i, out AsyncProfilerCallStackSourceFrameInfo info))
                    {
                        Assert.False(info.IsDispatchTarget);
                        Assert.False(info.IsCompleting);
                    }
                }
            };
            return scenario;
        }

        private static StitchScenario CreateRuntimeResumptionBridgeScenario(AsyncCallstackKind kind)
        {
            Frame work = Frame.App("Scenario.Work");
            Frame current = kind == AsyncCallstackKind.StateMachineAsync
                ? Frame.App("Scenario+<Level2Async>d__2.MoveNext")
                : Frame.App("Scenario.Level2Async");
            Frame logicalCurrent = kind == AsyncCallstackKind.StateMachineAsync
                ? current.Logical("Scenario.Level2Async") : current;
            Frame parent = kind == AsyncCallstackKind.StateMachineAsync
                ? Frame.App("Scenario+<Level1Async>d__1.MoveNext").Logical("Scenario.Level1Async")
                : Frame.App("Scenario.Level1Async");
            var sync = new List<Frame> { work, current };
            if (kind == AsyncCallstackKind.StateMachineAsync)
            {
                sync.Add(Frame.V1MoveNextAsDispatcher);
                sync.Add(Frame.CoreLib(
                    "System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncProfilerAsyncStateMachineBox.InstrumentedMoveNext",
                    "V1.InstrumentedMoveNext"));
            }
            else
            {
                sync.Add(Frame.V2Wrapper(0));
                sync.Add(Frame.V2InstrumentedDispatch);
                sync.Add(Frame.V2Dispatch);
            }
            Frame[] tail =
            {
                Frame.CoreLib("System.Threading.Tasks.AwaitTaskContinuation.RunOrScheduleAction",
                    "AwaitTaskContinuation.RunOrScheduleAction"),
                Frame.CoreLib("System.Threading.Tasks.Task.RunContinuations", "Task.RunContinuations"),
                Frame.CoreLib("System.Threading.Tasks.Task`1[System.Int64].TrySetResult(!0)", "Task.TrySetResult"),
                Frame.CoreLib("System.Threading.Tasks.Task+DelayPromise.CompleteTimedOut", "DelayPromise.CompleteTimedOut"),
                Frame.CoreLib("System.Threading.TimerQueueTimer.Fire", "TimerQueueTimer.Fire"),
                Frame.ThreadPoolDispatch,
                Frame.WorkerThreadStart,
            };
            sync.AddRange(tail);
            Frame[] logical = { work, logicalCurrent, parent };
            return new StitchScenario
            {
                Kind = kind,
                Sync = sync.ToArray(),
                Async = new[] { logicalCurrent, parent },
                EnableInfrastructureCleanup = true,
                VerifySampleWeights = true,
                ExpectedStitched = logical.Concat(tail.Skip(5)).ToArray(),
                ExpectedWithoutInfrastructureCleanup = logical.Concat(tail).ToArray(),
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V1InlineFallbackUsed = kind == AsyncCallstackKind.StateMachineAsync ? 2 : 0,
                    V1InfrastructureFramesCollapsed = kind == AsyncCallstackKind.StateMachineAsync ? 2 : 0,
                    V2InfrastructureFramesCollapsed = kind == AsyncCallstackKind.RuntimeAsync ? 4 : 0,
                    AsyncInfrastructureFramesCollapsed = 10,
                    RuntimeResumptionBridgeFramesCollapsed = 10,
                },
            };
        }

        private static StitchScenario CreateYieldRuntimeBridgeScenario(params Frame[] yieldFrames) =>
            CreateYieldRuntimeBridgeScenario(AsyncCallstackKind.RuntimeAsync, yieldFrames);

        private static StitchScenario CreateYieldRuntimeBridgeScenario(AsyncCallstackKind kind, params Frame[] yieldFrames)
        {
            StitchScenario scenario = CreateRuntimeResumptionBridgeScenario(kind);
            Frame[] tail = scenario.Sync.Skip(scenario.Sync.Length - 2).ToArray();
            scenario.Sync = scenario.Sync.Take(kind == AsyncCallstackKind.RuntimeAsync ? 5 : 4).Concat(yieldFrames).Concat(tail).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup =
                scenario.ExpectedStitched.Take(3).Concat(yieldFrames).Concat(tail).ToArray();
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = yieldFrames.Length * 2;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = yieldFrames.Length * 2;
            return scenario;
        }

        private static StitchScenario CreateV1AfterV2CompletionScenario()
        {
            Frame work = Frame.App("Scenario.Work");
            Frame current = Frame.App("Scenario+<ParentAsync>d__60.MoveNext");
            Frame logicalCurrent = current.Logical("Scenario.ParentAsync");
            Frame logicalParent = Frame.App("Scenario+<GrandparentAsync>d__61.MoveNext")
                .Logical("Scenario.GrandparentAsync");
            Frame infrastructure = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncProfilerAsyncStateMachineBox.InstrumentedMoveNext",
                "V1.InstrumentedMoveNext");
            Frame bridge = Frame.CoreLib(
                "System.Threading.Tasks.AwaitTaskContinuation.RunOrScheduleAction",
                "AwaitTaskContinuation.RunOrScheduleAction");
            Frame runContinuations = Frame.CoreLib(
                "System.Threading.Tasks.Task.RunContinuations", "Task.RunContinuations");
            Frame trySetResult = Frame.CoreLib(
                "System.Threading.Tasks.Task`1[System.Int64].TrySetResult(!0)", "Task.TrySetResult");
            Frame[] tail =
            {
                bridge, runContinuations, trySetResult,
                Frame.V2InstrumentedDispatch, Frame.V2Dispatch,
                Frame.ThreadPoolDispatch, Frame.WorkerThreadStart,
            };
            Frame[] logical = { work, logicalCurrent, logicalParent };
            return new StitchScenario
            {
                Kind = AsyncCallstackKind.StateMachineAsync,
                Sync = new[] { work, current, Frame.V1MoveNextAsDispatcher, infrastructure }
                    .Concat(tail).ToArray(),
                Async = new[] { logicalCurrent, logicalParent },
                ExpectedStitched = logical.Concat(tail.Skip(5)).ToArray(),
                ExpectedWithoutNormalization = logical.Concat(tail).ToArray(),
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V1InlineFallbackUsed = 2,
                    V1InfrastructureFramesCollapsed = 2,
                    V2CompletionHandoffFramesCollapsed = 10,
                },
            };
        }

        private static void AddSyntheticDispatcher(
            List<Frame> sync, AsyncCallstackKind kind, Frame v1LeafTransition, Frame v1RootInfrastructure)
        {
            if (kind == AsyncCallstackKind.StateMachineAsync)
            {
                sync.Add(v1LeafTransition);
                sync.Add(Frame.V1MoveNextAsDispatcher);
                sync.Add(v1RootInfrastructure);
            }
            else
            {
                sync.Add(Frame.V2Wrapper(0));
                sync.Add(Frame.V2InstrumentedDispatch);
                sync.Add(Frame.V2Dispatch);
            }
        }

        private static void UseExtendedRuntimeLayouts(StitchScenario scenario)
        {
            var support = new AsyncProfilerExtendedRuntimeLayoutSupport();
            scenario.RuntimeSupportForModule = module => AsyncProfilerRuntimeResolver.IsHostModule(module)
                ? support : AsyncProfilerRuntimeSupport.Common;
            Frame anchor = Frame.CoreLib(AsyncProfilerExtendedRuntimeLayoutSupport.AnchorName, "ChangedDispatch");
            Func<Frame[], Frame[]> replace = frames => frames?.Select(frame =>
                frame.Key == Frame.ThreadPoolDispatch.Key ? anchor : frame).ToArray();
            scenario.Sync = replace(scenario.Sync);
            scenario.ExpectedStitched = replace(scenario.ExpectedStitched);
            scenario.ExpectedWithoutNormalization = replace(scenario.ExpectedWithoutNormalization);
            scenario.ExpectedWithoutInfrastructureCleanup = replace(scenario.ExpectedWithoutInfrastructureCleanup);
            scenario.VerifySampleWeights = true;
        }

        private static TraceModuleFile ConfigureCapturedRuntime(
            TraceLog log, string productVersion, string engineModule = "coreclr.dll")
        {
            TraceProcess process = log.Processes.First(p => p.ProcessID == 1234);
            TraceModuleFile host = log.ModuleFiles.FirstOrDefault(module =>
                string.Equals(module.Name, AsyncProfilerRuntimeSupport.HostModuleName, StringComparison.OrdinalIgnoreCase));
            if (host == null)
            {
                return null;
            }
            host.productVersion = productVersion ?? "";
            if (engineModule != null)
            {
                process.LoadedModules.UniversalMapping(
                    @"C:\runtime\" + engineModule, 0x100000, 0x101000, 100_001, null);
            }
            return host;
        }

        private sealed class RejectingCompletionChainSupport : AsyncProfilerRuntimeSupport
        {
            public RejectingCompletionChainSupport() : base("Rejected completion chain") { }

            protected override AsyncProfilerRuntimeSequenceMatch MatchTaskCompletionChain(
                AsyncProfilerCallStackTransformState context, int start, int end)
            {
                AsyncProfilerRuntimeSequenceMatch pair = MatchTaskCompletion(context, start, end);
                return pair.IsMatch && end - start > pair.Length
                    ? AsyncProfilerRuntimeSequenceMatch.Reject(pair.Length + 1) : default;
            }
        }

        private sealed class RejectingProfilerEntrySupport : AsyncProfilerRuntimeSupport
        {
            public RejectingProfilerEntrySupport() : base("Rejected profiler entry") { }

            protected override AsyncProfilerRuntimeSequenceMatch MatchProfilerEntry(
                AsyncProfilerCallStackTransformState context, int start, int end) =>
                AsyncProfilerRuntimeSequenceMatch.Reject(end - start);
        }

        private sealed class StitchScenario
        {
            public Frame[] Sync { get; set; }
            public Frame[] Async { get; set; }
            // Fixture segments describe resume-event order; queries and callbacks return them leaf-to-root.
            public AsyncSegment[] AsyncSegments { get; set; }
            public Frame[] ExpectedStitched { get; set; }
            public Frame[] ExpectedWithoutNormalization { get; set; }
            public Frame[] ExpectedWithoutInfrastructureCleanup { get; set; }
            public Frame[] FilteredOut { get; set; }
            public Frame OmitSymbolFor { get; set; }
            public AsyncSegment ContextParent { get; set; }
            public int ParentMethodCompletions { get; set; }
            public int ParentExceptionCompletions { get; set; }
            public CallStackDiagnosticsExpectation ExpectedDiagnostics { get; set; }
            public AsyncCallstackKind Kind { get; set; } = AsyncCallstackKind.RuntimeAsync;
            public long AsyncStartQpc { get; set; } = StartQpc + 10;
            public long AsyncEndQpc { get; set; } = StartQpc + 40;
            public bool SampleHasStack { get; set; } = true;
            public bool OnlyManagedCodeStacks { get; set; }
            public bool IncludeEventSourceEvents { get; set; } = true;
            public bool GroupByStartStopActivity { get; set; } = true;
            public bool AddForeignProcessActiveSegment { get; set; }
            public bool IncludeCoreLibUnloadRundown { get; set; }
            public Frame CoreLibMethodBeforeUnload { get; set; }
            public byte ContinuationIndexBase { get; set; }
            public int[] AsyncStates { get; set; }
            public int MethodCompletions { get; set; }
            public long MethodCompletionQpc { get; set; } = StartQpc + 20;
            public int ExceptionCompletions { get; set; }
            public bool IncludeCompletedInnerCallStack { get; set; }
            public int WrapperResets { get; set; }
            public bool VerifyComputerAndIndexLifecycle { get; set; }
            public bool VerifyThreadTimeComputer { get; set; }
            public bool IncludeTplActivity { get; set; }
            public bool IncludeStartStopActivity { get; set; }
            public bool AllowSupplementalSamples { get; set; }
            public bool SkipNormalStackAssertion { get; set; }
            public bool EnableParentContexts { get; set; }
            public bool EnableFrameAnnotations { get; set; }
            public bool EnableNormalization { get; set; } = true;
            public bool EnableInfrastructureCleanup { get; set; }
            public bool VerifySampleWeights { get; set; }
            public bool VerifyRemovingParentContextRemovesPresentation { get; set; }
            public Action<MutableTraceEventStackSource> AssertStitchedStackSource { get; set; }
            public Action<EmittedStack, EmittedStack> AssertOutputs { get; set; }
            public Action<AsyncProfilerCallStackTransformState> AssertTransformContext { get; set; }
            public Action<TraceLog, string> AssertPublicApi { get; set; }
            public bool VerifyPublicBuilder { get; set; }
            public Action<TraceLog> ConfigureTraceLog { get; set; } =
                log => ConfigureCapturedRuntime(log, "11.0.0");
            public Func<TraceModuleFile, AsyncProfilerRuntimeSupport> RuntimeSupportForModule { get; set; }
            public AsyncProfilerRuntimeSupport ExpectedRuntimeSupport { get; set; }

            public void AssertProductionStitch()
            {
                Validate();

                string nettracePath = Path.Combine(Path.GetTempPath(), $"asyncprofiler_stitch_{Guid.NewGuid():N}.nettrace");
                string etlxPath = null;
                try
                {
                    File.WriteAllBytes(nettracePath, BuildNettrace());
                    etlxPath = TraceLog.CreateFromEventPipeDataFile(nettracePath);

                    using (var symbolReader = new SymbolReader(TextWriter.Null))
                    using (var traceLog = new TraceLog(etlxPath))
                    {
                        ConfigureTraceLog?.Invoke(traceLog);
                        AsyncSegment[] expectedSegments = GetAsyncSegments().Reverse().ToArray();
                        long asyncProbeQpc = AsyncStartQpc + expectedSegments.Length;
                        IReadOnlyList<AsyncProfilerCallStack> segments = new AsyncProfilerCallStackComputer(traceLog).GetCallStacks(ProcessId, OsThreadId, asyncProbeQpc);
                        Assert.Equal(expectedSegments.Length, segments.Count);
                        for (int i = 0; i < expectedSegments.Length; i++)
                        {
                            Assert.Equal(expectedSegments.Length - 1 - i, segments[i].Depth);
                            Assert.Equal(Labels(expectedSegments[i].Frames),
                                AsyncFrameLabels(traceLog, segments[i].Frames, expectedSegments[i]));
                            Assert.Equal(segments[i].Frames.FrameCount, segments[i].CapturedFrames.Count);
                            for (int j = 0; j < segments[i].CapturedFrames.Count; j++)
                            {
                                AsyncProfilerCallStackFrame frame = segments[i].CapturedFrames[j];
                                Assert.Equal(segments[i].Frames.InstructionPointerAt(j), frame.CapturedInstructionPointer);
                                Assert.Equal(segments[i].Frames.CodeAddressAt(j), frame.CodeAddress);
                                Assert.Equal(segments[i].Frames.Kind == AsyncCallstackKind.StateMachineAsync
                                    ? segments[i].Frames.FrameStateAt(j) : (int?)null, frame.CapturedState);
                            }
                        }
                        if (AddForeignProcessActiveSegment)
                        {
                            Assert.Empty(new AsyncProfilerCallStackComputer(traceLog).GetCallStacks(ProcessId, OsThreadId, SampleQpc));
                            Assert.Single(new AsyncProfilerCallStackComputer(traceLog).GetCallStacks(ForeignProcessId, OsThreadId, SampleQpc));
                        }

                        var syncStackSource = new MutableTraceEventStackSource(traceLog)
                        {
                            OnlyManagedCodeStacks = OnlyManagedCodeStacks,
                        };
                        var syncComputer = new SampleProfilerThreadTimeComputer(traceLog, symbolReader)
                        {
                            IncludeEventSourceEvents = IncludeEventSourceEvents,
                            GroupByStartStopActivity = GroupByStartStopActivity,
                        };
                        syncComputer.GenerateThreadTimeStacks(syncStackSource);
                        EmittedStack syncOutput = ReadSingleStack(syncStackSource);
                        if (!SkipNormalStackAssertion)
                        {
                            Assert.Equal(Labels(Sync), syncOutput.ScenarioFrames);
                        }

                        var stitchedStackSource = new MutableTraceEventStackSource(traceLog)
                        {
                            OnlyManagedCodeStacks = OnlyManagedCodeStacks,
                        };
                        var stitchedComputer = new SampleProfilerThreadTimeComputer(traceLog, symbolReader)
                        {
                            EnableAsyncCallStacks = true,
                            IncludeEventSourceEvents = IncludeEventSourceEvents,
                            GroupByStartStopActivity = GroupByStartStopActivity,
                        };
                        stitchedComputer.AsyncCallStackTransforms.EnableParentContexts =
                            EnableParentContexts;
                        stitchedComputer.AsyncCallStackTransforms.EnableFrameAnnotations =
                            EnableFrameAnnotations;
                        ConfigurePresentation(stitchedComputer.AsyncCallStackTransforms, stitchedComputer.AsyncProfilerCallStackTransformPipeline,
                            enableNormalization: EnableNormalization, enableInfrastructureCleanup: EnableInfrastructureCleanup);
                        if (AssertTransformContext != null)
                        {
                            stitchedComputer.AsyncProfilerCallStackTransformPipeline.AddCustomTransform(
                                context => AssertTransformContext(context));
                        }
                        stitchedComputer.GenerateThreadTimeStacks(stitchedStackSource);
                        if (VerifySampleWeights)
                        {
                            var syncWeights = new List<KeyValuePair<double, float>>();
                            var stitchedWeights = new List<KeyValuePair<double, float>>();
                            syncStackSource.ForEach(sample =>
                                syncWeights.Add(new KeyValuePair<double, float>(sample.TimeRelativeMSec, sample.Metric)));
                            stitchedStackSource.ForEach(sample =>
                                stitchedWeights.Add(new KeyValuePair<double, float>(sample.TimeRelativeMSec, sample.Metric)));
                            Assert.Equal(syncWeights, stitchedWeights);
                        }
                        if (VerifyComputerAndIndexLifecycle)
                        {
                            Assert.Throws<InvalidOperationException>(() =>
                                stitchedComputer.GenerateThreadTimeStacks(new MutableTraceEventStackSource(traceLog)));
                            Assert.True(traceLog.IsAsyncProfilerCallStacksLoaded);
                            Assert.True(traceLog.ReleaseAsyncProfilerCallStacks());
                            Assert.False(traceLog.IsAsyncProfilerCallStacksLoaded);
                        }
                        EmittedStack stitchedOutput = ReadSingleStack(stitchedStackSource);
                        Assert.Equal(Labels(ExpectedStitched), stitchedOutput.ScenarioFrames);
                        AssertStitchedStackSource?.Invoke(stitchedStackSource);
                        AssertOutputs?.Invoke(syncOutput, stitchedOutput);
                        AssertPublicApi?.Invoke(traceLog, etlxPath);
                        if (RuntimeSupportForModule == null && VerifyPublicBuilder)
                        {
                            AssertPublicBuilderEquivalence(traceLog);
                        }

                        Assert.Equal(syncOutput.RootFrames, stitchedOutput.RootFrames);
                        Assert.True(stitchedComputer.AsyncCallStacksActive);
                        (ExpectedDiagnostics ?? new CallStackDiagnosticsExpectation()).Assert(stitchedComputer.AsyncProfilerCallStackDiagnostics);

                        if (VerifyRemovingParentContextRemovesPresentation)
                        {
                            var filteredContextStackSource =
                                new MutableTraceEventStackSource(traceLog);
                            var filteredContextComputer =
                                new SampleProfilerThreadTimeComputer(
                                    traceLog, symbolReader)
                                {
                                    EnableAsyncCallStacks = true,
                                    IncludeEventSourceEvents = IncludeEventSourceEvents,
                                    GroupByStartStopActivity = GroupByStartStopActivity,
                                };
                            filteredContextComputer.AsyncCallStackTransforms.EnableParentContexts = true;
                            filteredContextComputer.AsyncCallStackTransforms.AddCustomTransform(context =>
                            {
                                for (int i = context.Frames.Count - 1; i >= 0; i--)
                                {
                                    if (context.Frames[i].IsParentContext)
                                    {
                                        context.Frames.RemoveAt(i);
                                    }
                                }
                            });
                            filteredContextComputer.GenerateThreadTimeStacks(
                                filteredContextStackSource);

                            EmittedStack filteredContextOutput =
                                ReadSingleStack(filteredContextStackSource);
                            var parentKeys = new HashSet<string>(
                                ContextParent.Frames.Select(frame => frame.Key));
                            Assert.Equal(
                                Labels(ExpectedStitched.Where(
                                    frame => !parentKeys.Contains(frame.Key))),
                                filteredContextOutput.ScenarioFrames);
                            Assert.DoesNotContain(
                                "[Async resumed parent context]",
                                filteredContextOutput.GroupingFrames);
                        }

                        if (VerifyThreadTimeComputer)
                        {
                            var threadTimeStackSource = new MutableTraceEventStackSource(traceLog)
                            {
                                OnlyManagedCodeStacks = OnlyManagedCodeStacks,
                            };
#pragma warning disable CS0618 // ThreadTimeStackComputer is intentionally experimental.
                            var threadTimeComputer = new ThreadTimeStackComputer(
                                traceLog, symbolReader)
#pragma warning restore CS0618
                            {
                                EnableAsyncCallStacks = true,
                                GroupByStartStopActivity = GroupByStartStopActivity,
                                IncludeEventSourceEvents = false,
                            };
                            threadTimeComputer.AsyncCallStackTransforms.EnableParentContexts = EnableParentContexts;
                            threadTimeComputer.AsyncCallStackTransforms.EnableFrameAnnotations = EnableFrameAnnotations;
                            ConfigurePresentation(threadTimeComputer.AsyncCallStackTransforms, threadTimeComputer.AsyncProfilerCallStackTransformPipeline,
                                enableNormalization: EnableNormalization, enableInfrastructureCleanup: EnableInfrastructureCleanup);
                            threadTimeComputer.GenerateThreadTimeStacks(threadTimeStackSource);
                            AssertThreadTimeStack(
                                threadTimeStackSource, "CPU_TIME", Labels(ExpectedStitched));
                            AssertThreadTimeStack(
                                threadTimeStackSource, "BLOCKED_TIME", Labels(ExpectedStitched));
                            Assert.True(threadTimeComputer.AsyncCallStacksActive);
                        }

                        // A variant whose configuration matches the main run would only repeat it, so check
                        // its expectation against the main output instead of stitching again.
                        bool mainRunIsCleanupVariant = !EnableInfrastructureCleanup && !OnlyManagedCodeStacks;
                        bool mainRunIsStructuralVariant = mainRunIsCleanupVariant && !EnableNormalization &&
                            !EnableParentContexts && !EnableFrameAnnotations;
                        AsyncProfilerCallStackDiagnostics mainDiagnostics = stitchedComputer.AsyncProfilerCallStackDiagnostics;
                        if (ExpectedWithoutInfrastructureCleanup != null && mainRunIsCleanupVariant)
                        {
                            Assert.Equal(Labels(ExpectedWithoutInfrastructureCleanup), stitchedOutput.ScenarioFrames);
                            Assert.Equal(0, mainDiagnostics.AsyncInfrastructureFramesCollapsed);
                            Assert.Equal(0, mainDiagnostics.RuntimeResumptionBridgeFramesCollapsed);
                        }
                        else if (ExpectedWithoutInfrastructureCleanup != null)
                        {
                            var defaultStackSource = new MutableTraceEventStackSource(traceLog);
                            var defaultComputer = new SampleProfilerThreadTimeComputer(
                                traceLog, symbolReader)
                            {
                                EnableAsyncCallStacks = true,
                                IncludeEventSourceEvents = IncludeEventSourceEvents,
                                GroupByStartStopActivity = GroupByStartStopActivity,
                            };
                            defaultComputer.AsyncCallStackTransforms.EnableParentContexts = EnableParentContexts;
                            defaultComputer.AsyncCallStackTransforms.EnableFrameAnnotations = EnableFrameAnnotations;
                            ConfigurePresentation(defaultComputer.AsyncCallStackTransforms, defaultComputer.AsyncProfilerCallStackTransformPipeline,
                                enableNormalization: EnableNormalization, enableInfrastructureCleanup: false);
                            defaultComputer.GenerateThreadTimeStacks(defaultStackSource);
                            Assert.Equal(Labels(ExpectedWithoutInfrastructureCleanup),
                                ReadSingleStack(defaultStackSource).ScenarioFrames);
                            Assert.Equal(0, defaultComputer.AsyncProfilerCallStackDiagnostics.AsyncInfrastructureFramesCollapsed);
                            Assert.Equal(0, defaultComputer.AsyncProfilerCallStackDiagnostics.RuntimeResumptionBridgeFramesCollapsed);
                        }

                        if (ExpectedWithoutNormalization != null && mainRunIsStructuralVariant)
                        {
                            Assert.Equal(Labels(ExpectedWithoutNormalization), stitchedOutput.ScenarioFrames);
                            Assert.Equal(0, mainDiagnostics.V1SynchronousMoveNextFramesNormalized);
                            Assert.Equal(0, mainDiagnostics.V1SynchronousStartupFramesCollapsed);
                            Assert.Equal(0, mainDiagnostics.V2CompletionHandoffFramesCollapsed);
                            Assert.Equal(0, mainDiagnostics.AsyncReentrantHandoffFramesCollapsed);
                        }
                        else if (ExpectedWithoutNormalization != null)
                        {
                            var structuralStackSource = new MutableTraceEventStackSource(traceLog);
                            var structuralComputer = new SampleProfilerThreadTimeComputer(
                                traceLog, symbolReader)
                            {
                                EnableAsyncCallStacks = true,
                                IncludeEventSourceEvents = IncludeEventSourceEvents,
                                GroupByStartStopActivity = GroupByStartStopActivity,
                            };
                            ConfigurePresentation(structuralComputer.AsyncCallStackTransforms, structuralComputer.AsyncProfilerCallStackTransformPipeline,
                                enableNormalization: false, enableInfrastructureCleanup: false);
                            structuralComputer.GenerateThreadTimeStacks(structuralStackSource);
                            EmittedStack structuralOutput = ReadSingleStack(structuralStackSource);
                            Assert.Equal(
                                Labels(ExpectedWithoutNormalization),
                                structuralOutput.ScenarioFrames);
                            Assert.Equal(syncOutput.RootFrames, structuralOutput.RootFrames);
                            Assert.Equal(
                                0,
                                structuralComputer.AsyncProfilerCallStackDiagnostics.V1SynchronousMoveNextFramesNormalized);
                            Assert.Equal(
                                0,
                                structuralComputer.AsyncProfilerCallStackDiagnostics.V1SynchronousStartupFramesCollapsed);
                            Assert.Equal(
                                0,
                                structuralComputer.AsyncProfilerCallStackDiagnostics.V2CompletionHandoffFramesCollapsed);
                            Assert.Equal(
                                0,
                                structuralComputer.AsyncProfilerCallStackDiagnostics.AsyncReentrantHandoffFramesCollapsed);
                        }

                        if (VerifyComputerAndIndexLifecycle)
                        {
                            Assert.Equal(
                                expectedSegments.Length,
                                new AsyncProfilerCallStackComputer(traceLog).GetCallStacks(ProcessId, OsThreadId, asyncProbeQpc).Count);
                            Assert.True(traceLog.IsAsyncProfilerCallStacksLoaded);
                        }

                        if (FilteredOut != null && FilteredOut.Length != 0)
                        {
                            var filteredStackSource = new MutableTraceEventStackSource(traceLog);
                            var filteredComputer = new SampleProfilerThreadTimeComputer(
                                traceLog, symbolReader)
                            {
                                EnableAsyncCallStacks = true,
                                IncludeEventSourceEvents = IncludeEventSourceEvents,
                                GroupByStartStopActivity = GroupByStartStopActivity,
                            };
                            filteredComputer.AsyncCallStackTransforms.AddCustomTransform(context =>
                            {
                                for (int i = context.Frames.Count - 1; i >= 0; i--)
                                {
                                    if (!IncludeFilteredFrame(traceLog, context.Frames[i]))
                                    {
                                        context.Frames.RemoveAt(i);
                                    }
                                }
                            });
                            filteredComputer.GenerateThreadTimeStacks(filteredStackSource);
                            EmittedStack filteredOutput = ReadSingleStack(filteredStackSource);
                            Assert.Equal(Labels(ExpectedFilteredFrames()), filteredOutput.ScenarioFrames);
                            Assert.Equal(syncOutput.RootFrames, filteredOutput.RootFrames);
                        }

                    }
                }
                finally
                {
                    if (File.Exists(nettracePath))
                    {
                        File.Delete(nettracePath);
                    }
                    if (etlxPath != null && File.Exists(etlxPath))
                    {
                        File.Delete(etlxPath);
                    }
                }
            }

            public void AssertPhysicalFallback(bool includeAsyncData)
            {
                string path = Path.Combine(Path.GetTempPath(), $"asyncprofiler_public_{Guid.NewGuid():N}.nettrace");
                string etlxPath = null;
                try
                {
                    File.WriteAllBytes(path, BuildNettrace(includeAsyncData));
                    etlxPath = TraceLog.CreateFromEventPipeDataFile(path);
                    using (var log = new TraceLog(etlxPath))
                    {
                        ConfigureTraceLog?.Invoke(log);
                        if (!includeAsyncData)
                        {
                            Assert.Null(log.AsyncProfilerCallStacks);
                        }
                        var query = new AsyncProfilerCallStackComputer(log);
                        var output = new MutableTraceEventStackSource(log);
                        var builder = new AsyncProfilerCallStackSourceBuilder(output);
                        builder.Transforms.EnableInfrastructureCleanup = true;
                        builder.Transforms.EnableFrameAnnotations = true;
                        builder.Transforms.EnableParentContexts = true;
                        builder.Transforms.AddCustomTransform(context => throw new InvalidOperationException("Fallback must not run transforms."));
                        foreach (TraceEvent data in log.Events.Where(e => e.CallStackIndex() != CallStackIndex.Invalid))
                        {
                            if (includeAsyncData)
                            {
                                Assert.Contains(query.GetCallStacks(data), callStack => callStack.CapturedFrames.Count == 0);
                            }
                            else
                            {
                                Assert.Empty(query.GetCallStacks(data));
                            }
                            Assert.Equal(output.GetCallStackThread(data.CallStackIndex(), data.Thread()), builder.GetCallStack(data));
                            Assert.True(data.TryGetCallStackIndex(out var physical, out long qpc));
                            var root = output.Interner.CallStackIntern(output.Interner.FrameIntern("Custom root"),
                                StackSourceCallStackIndex.Invalid);
                            Assert.Equal(output.GetCallStack(physical, root), builder.GetCallStack(physical, qpc, root));
                        }
                        using (var symbols = new SymbolReader(TextWriter.Null))
                        {
                            var baseline = new MutableTraceEventStackSource(log);
                            new SampleProfilerThreadTimeComputer(log, symbols).GenerateThreadTimeStacks(baseline);
                            var reconstructed = new MutableTraceEventStackSource(log);
                            var computer = new SampleProfilerThreadTimeComputer(log, symbols) { EnableAsyncCallStacks = true };
                            computer.AsyncCallStackTransforms.EnableInfrastructureCleanup = true;
                            computer.AsyncCallStackTransforms.AddCustomTransform(context =>
                                throw new InvalidOperationException("Fallback must not run transforms."));
                            computer.GenerateThreadTimeStacks(reconstructed);
                            Assert.Equal(ReadSingleStack(baseline).ScenarioFrames, ReadSingleStack(reconstructed).ScenarioFrames);
                            var expectedWeights = new List<(double, float)>();
                            var actualWeights = new List<(double, float)>();
                            baseline.ForEach(sample => expectedWeights.Add((sample.TimeRelativeMSec, sample.Metric)));
                            reconstructed.ForEach(sample => actualWeights.Add((sample.TimeRelativeMSec, sample.Metric)));
                            Assert.Equal(expectedWeights, actualWeights);
                        }
                    }
                }
                finally
                {
                    File.Delete(path);
                    if (etlxPath != null)
                    {
                        File.Delete(etlxPath);
                    }
                }
            }

            #region private

            private void AssertPublicBuilderEquivalence(TraceLog log)
            {
                var output = new MutableTraceEventStackSource(log) { OnlyManagedCodeStacks = OnlyManagedCodeStacks };
                var builder = new AsyncProfilerCallStackSourceBuilder(output);
                builder.Transforms.EnableNormalization = EnableNormalization;
                builder.Transforms.EnableInfrastructureCleanup = EnableInfrastructureCleanup;
                builder.Transforms.EnableFrameAnnotations = EnableFrameAnnotations;
                builder.Transforms.EnableParentContexts = EnableParentContexts;
                int count = 0;
                foreach (TraceEvent data in log.Events)
                {
#pragma warning disable CS0618
                    long timestampQpc = data.TimeStampQPC;
#pragma warning restore CS0618
                    if (timestampQpc != SampleQpc && timestampQpc != SampleQpc + 1)
                    {
                        continue;
                    }
                    var stack = builder.GetCallStack(data);
                    if (!SampleHasStack)
                    {
                        Assert.Equal(StackSourceCallStackIndex.Invalid, stack);
                    }
                    else
                    {
                        Assert.Equal(Labels(ExpectedStitched), ReadStack(output, stack).ScenarioFrames);
                        Assert.True(data.TryGetCallStackIndex(out var physicalStack, out long userModeTimeStampQPC));
                        Assert.Equal(stack, builder.GetCallStack(physicalStack, userModeTimeStampQPC));
                    }
                    count++;
                }
                Assert.Equal(2, count);
                Assert.Equal(0, output.SampleIndexLimit);
            }

            private byte[] BuildNettrace(bool includeAsyncData = true)
            {
                Dictionary<string, ModuleMapping> mappings = CreateModuleMappings();
                Dictionary<string, ulong> addresses = _addresses = AssignAddresses(mappings);
                var asyncBufferBuilder = new AsyncProfilerBufferBuilder(OsThreadId, 0x0BADF00D, StartQpc)
                    .Armed(StartQpc);
                AsyncSegment[] segments = GetAsyncSegments();
                if (ContextParent != null)
                {
                    ulong[] ownerFrameAddresses =
                        ContextParent.Frames.Select(frame => addresses[frame.Key]).ToArray();
                    if (ContextParent.Kind == AsyncCallstackKind.RuntimeAsync)
                    {
                        asyncBufferBuilder
                            .ResumeRuntimeStack(StartQpc + 1, dispatcher: 99, ownerFrameAddresses);
                    }
                    else
                    {
                        asyncBufferBuilder
                            .ResumeStack(
                                StartQpc + 1,
                                dispatcher: 99,
                                ownerFrameAddresses,
                                ContextParent.States ?? new int[ownerFrameAddresses.Length]);
                    }
                    for (int i = 0; i < ParentMethodCompletions; i++)
                    {
                        asyncBufferBuilder.Method(
                            ContextParent.Kind == AsyncCallstackKind.RuntimeAsync
                                ? AsyncEventID.CompleteRuntimeAsyncMethod : AsyncEventID.CompleteStateMachineAsyncMethod,
                            StartQpc + 2);
                    }
                    if (ParentExceptionCompletions > 0)
                    {
                        asyncBufferBuilder.Unwind(
                            ContextParent.Kind == AsyncCallstackKind.RuntimeAsync
                                ? AsyncEventID.UnwindRuntimeAsyncException : AsyncEventID.UnwindStateMachineAsyncException,
                            StartQpc + 3, (uint)ParentExceptionCompletions);
                    }
                    asyncBufferBuilder.CreateContext(
                        segments[0].Kind == AsyncCallstackKind.RuntimeAsync
                            ? AsyncEventID.CreateRuntimeAsyncContext : AsyncEventID.CreateStateMachineAsyncContext,
                        StartQpc + 5, parent: 99, dispatcher: 1);
                    if (ContextParent.Kind == AsyncCallstackKind.RuntimeAsync)
                    {
                        asyncBufferBuilder.SuspendRuntime(StartQpc + 8);
                    }
                    else
                    {
                        asyncBufferBuilder.Suspend(StartQpc + 8);
                    }
                }
                for (int i = 0; i < segments.Length; i++)
                {
                    AsyncSegment segment = segments[i];
                    ulong[] frameAddresses = segment.Frames.Select(frame => addresses[frame.Key]).ToArray();
                    if (segment.Kind == AsyncCallstackKind.RuntimeAsync)
                    {
                        asyncBufferBuilder.ResumeRuntimeStack(
                            AsyncStartQpc + i, dispatcher: (ulong)i + 1, frameAddresses, segment.ContinuationIndexBase);
                    }
                    else
                    {
                        asyncBufferBuilder.ResumeStack(
                            AsyncStartQpc + i, dispatcher: (ulong)i + 1, frameAddresses,
                            segment.States ?? new int[frameAddresses.Length]);
                    }
                    for (int completed = 0; completed < segment.MethodCompletions; completed++)
                    {
                        asyncBufferBuilder.Method(segment.Kind == AsyncCallstackKind.RuntimeAsync
                            ? AsyncEventID.CompleteRuntimeAsyncMethod : AsyncEventID.CompleteStateMachineAsyncMethod,
                            AsyncStartQpc + i);
                    }
                    if (segment.ExceptionCompletions > 0)
                    {
                        asyncBufferBuilder.Unwind(segment.Kind == AsyncCallstackKind.RuntimeAsync
                            ? AsyncEventID.UnwindRuntimeAsyncException : AsyncEventID.UnwindStateMachineAsyncException,
                            AsyncStartQpc + i, (uint)segment.ExceptionCompletions);
                    }
                }

                AsyncCallstackKind leafKind = segments[segments.Length - 1].Kind;
                for (int i = 0; i < WrapperResets; i++)
                {
                    asyncBufferBuilder.ContextNoPayload(AsyncEventID.ResetAsyncContinuationWrapperIndex, StartQpc + 20);
                }
                for (int i = 0; i < MethodCompletions; i++)
                {
                    asyncBufferBuilder.Method(leafKind == AsyncCallstackKind.RuntimeAsync
                        ? AsyncEventID.CompleteRuntimeAsyncMethod : AsyncEventID.CompleteStateMachineAsyncMethod, MethodCompletionQpc);
                }
                if (ExceptionCompletions > 0)
                {
                    asyncBufferBuilder.Unwind(
                        leafKind == AsyncCallstackKind.RuntimeAsync
                            ? AsyncEventID.UnwindRuntimeAsyncException : AsyncEventID.UnwindStateMachineAsyncException,
                        StartQpc + 20, (uint)ExceptionCompletions);
                }
                if (IncludeCompletedInnerCallStack)
                {
                    asyncBufferBuilder.ResumeRuntimeStack(StartQpc + 21, dispatcher: 100, new ulong[] { 0 })
                        .SuspendRuntime(StartQpc + 22);
                }

                for (int i = segments.Length - 1; i >= 0; i--)
                {
                    long closeQpc = AsyncEndQpc + (segments.Length - 1 - i);
                    if (segments[i].Kind == AsyncCallstackKind.RuntimeAsync)
                    {
                        asyncBufferBuilder.SuspendRuntime(closeQpc);
                    }
                    else
                    {
                        asyncBufferBuilder.Suspend(closeQpc);
                    }
                }
                byte[] asyncBuffer = asyncBufferBuilder.Build();
                byte[] foreignAsyncBuffer = AddForeignProcessActiveSegment
                    ? new AsyncProfilerBufferBuilder(OsThreadId, 0x0BADBEEF, StartQpc)
                        .Armed(StartQpc)
                        .ResumeRuntimeStack(StartQpc + 10, dispatcher: 2,
                            segments[0].Frames.Select(frame => addresses[frame.Key]).ToArray(),
                            continuationIndex: segments[0].ContinuationIndexBase)
                        .SuspendRuntime(StartQpc + 40)
                        .Build()
                    : null;

                var asyncMetadata = new EventMetadata(
                    1, AsyncProfilerTraceEventParser.ProviderName, "AsyncEvents", AsyncProfilerTraceEventParser.AsyncEventsEventId)
                {
                    ProviderId = AsyncProfilerTraceEventParser.ProviderGuid,
                };
                var sampleMetadata = new EventMetadata(
                    2, SampleProfilerTraceEventParser.ProviderName, "Sample", 0,
                    new MetadataParameter("Type", MetadataTypeCode.Int32))
                {
                    ProviderId = SampleProfilerTraceEventParser.ProviderGuid,
                };
                var mappingMetadata = new EventMetadata(3, "Universal.System", "ProcessMapping", 3)
                {
                    ProviderId = UniversalSystemTraceEventParser.ProviderGuid,
                };
                var symbolMetadata = new EventMetadata(4, "Universal.System", "ProcessSymbol", 4)
                {
                    ProviderId = UniversalSystemTraceEventParser.ProviderGuid,
                };
                var taskScheduledMetadata = new EventMetadata(
                    5, TplEtwProviderTraceEventParser.ProviderName, "TaskScheduled", 7)
                {
                    ProviderId = TplEtwProviderTraceEventParser.ProviderGuid,
                };
                var taskStartedMetadata = new EventMetadata(
                    6, TplEtwProviderTraceEventParser.ProviderName, "TaskExecuteStart", 8)
                {
                    ProviderId = TplEtwProviderTraceEventParser.ProviderGuid,
                };
                var activityStartMetadata = new EventMetadata(
                    7, "Microsoft-Diagnostics-ActivityTracking", "RequestStart", 1)
                {
                    ProviderId = new Guid("3b268b3d-903f-5835-c77e-790d518a26c4"),
                    OpCode = (byte)EventOpcode.Start,
                };
                var universalCpuMetadata = new EventMetadata(
                    8, UniversalEventsTraceEventParser.ProviderName, "cpu", 1,
                    new MetadataParameter("Value", MetadataTypeCode.VarUInt))
                {
                    ProviderId = UniversalEventsTraceEventParser.ProviderGuid,
                };
                var universalCSwitchMetadata = new EventMetadata(
                    9, UniversalEventsTraceEventParser.ProviderName, "cswitch", 2,
                    new MetadataParameter("Value", MetadataTypeCode.VarUInt))
                {
                    ProviderId = UniversalEventsTraceEventParser.ProviderGuid,
                };

                var writer = new EventPipeWriterV6();
                writer.WriteHeaders(keyValues: null, syncTimeQpc: 1);
                writer.WriteMetadataBlock(
                    asyncMetadata,
                    sampleMetadata,
                    mappingMetadata,
                    symbolMetadata,
                    taskScheduledMetadata,
                    taskStartedMetadata,
                    activityStartMetadata,
                    universalCpuMetadata,
                    universalCSwitchMetadata);
                if (IncludeCoreLibUnloadRundown)
                {
                    writer.WriteMetadataBlock(
                        new EventMetadata(10, ClrTraceEventParser.ProviderName, "MethodUnloadVerbose", 144)
                        {
                            ProviderId = ClrTraceEventParser.ProviderGuid, Version = 2,
                        },
                        new EventMetadata(11, ClrTraceEventParser.ProviderName, "ModuleUnload", 153)
                        {
                            ProviderId = ClrTraceEventParser.ProviderGuid, Version = 2,
                        },
                        new EventMetadata(12, Microsoft.Diagnostics.Tracing.Parsers.Clr.ClrRundownTraceEventParser.ProviderName,
                            "MethodDCStopVerbose", 144)
                        {
                            ProviderId = Microsoft.Diagnostics.Tracing.Parsers.Clr.ClrRundownTraceEventParser.ProviderGuid,
                            Version = 2,
                        },
                        new EventMetadata(13, Microsoft.Diagnostics.Tracing.Parsers.Clr.ClrRundownTraceEventParser.ProviderName,
                            "ModuleDCStop", 154)
                        {
                            ProviderId = Microsoft.Diagnostics.Tracing.Parsers.Clr.ClrRundownTraceEventParser.ProviderGuid,
                            Version = 2,
                        });
                }
                writer.WriteThreadBlock(w =>
                {
                    w.WriteThreadEntry(ThreadStreamIndex, OsThreadId, ProcessId);
                    if (AddForeignProcessActiveSegment)
                    {
                        w.WriteThreadEntry(ForeignThreadStreamIndex, OsThreadId + 1, ForeignProcessId);
                    }
                });
                if (SampleHasStack)
                {
                    writer.WriteStackBlock(1, 1, w => w.WriteStackPayload(Sync.Select(frame => addresses[frame.Key]).ToArray()));
                }
                writer.WriteEventBlock(w =>
                {
                    int sequence = 1;
                    foreach (ModuleMapping mapping in mappings.Values)
                    {
                        if (IncludeCoreLibUnloadRundown &&
                            mapping.Name == AsyncProfilerRuntimeSupport.HostModuleName)
                        {
                            continue;
                        }
                        w.WriteEventBlob(EventOptions(3, sequence++, StartQpc + 1), p =>
                            p.WriteProcessMappingPayload(mapping.Id, mapping.StartAddress, mapping.EndAddress, mapping.Name));
                    }

                    ulong symbolId = 1;
                    foreach (Frame frame in InputFrames())
                    {
                        if (frame == Frame.Zero || frame == OmitSymbolFor)
                        {
                            continue;
                        }
                        if (IncludeCoreLibUnloadRundown &&
                            frame.ModuleName == AsyncProfilerRuntimeSupport.HostModuleName)
                        {
                            continue;
                        }

                        ulong mappingId = mappings[frame.ModuleName].Id;
                        ulong address = addresses[frame.Key];
                        w.WriteEventBlob(EventOptions(4, sequence++, StartQpc + 2), p =>
                            p.WriteProcessSymbolPayload(symbolId++, mappingId, address, frame.RawSymbolName));
                    }

                    Guid activityId = new Guid("9f7e55d2-694f-4cc2-a68d-df35efca1c81");
                    if (IncludeStartStopActivity)
                    {
                        w.WriteEventBlob(
                            EventOptions(7, sequence++, StartQpc + 3, activityId: activityId),
                            p => { });
                    }
                    if (IncludeTplActivity)
                    {
                        w.WriteEventBlob(
                            EventOptions(5, sequence++, StartQpc + 4, stackId: 1, activityId: activityId),
                            p =>
                            {
                                p.Write(1);
                                p.Write(0);
                                p.Write(42);
                                p.Write(0);
                                p.Write(0);
                            });
                        w.WriteEventBlob(
                            EventOptions(6, sequence++, StartQpc + 5, stackId: 1, activityId: activityId),
                            p =>
                            {
                                p.Write(1);
                                p.Write(0);
                                p.Write(42);
                            });
                    }

                    // Thread-time computation emits a CPU sample when the following sample arrives.
                    int stackId = SampleHasStack ? 1 : 0;
                    if (VerifyThreadTimeComputer)
                    {
                        w.WriteEventBlob(EventOptions(9, sequence++, SampleQpc - 3, stackId),
                            p => p.WriteVarUInt(1));
                        w.WriteEventBlob(EventOptions(8, sequence++, SampleQpc - 2, stackId),
                            p => p.WriteVarUInt(1));
                        w.WriteEventBlob(EventOptions(8, sequence++, SampleQpc - 1, stackId),
                            p => p.WriteVarUInt(1));
                    }
                    w.WriteEventBlob(EventOptions(2, sequence++, SampleQpc, stackId),
                        p => p.Write((int)ClrThreadSampleType.Managed));
                    w.WriteEventBlob(EventOptions(2, sequence++, SampleQpc + 1, stackId),
                        p => p.Write((int)ClrThreadSampleType.Managed));
                    if (includeAsyncData)
                    {
                        w.WriteEventBlob(EventOptions(1, sequence, StartQpc + 50),
                            p => p.Write(AsyncProfilerTestData.AsyncEventsPayload(asyncBuffer)));
                    }
                    if (foreignAsyncBuffer != null)
                    {
                        w.WriteEventBlob(EventOptions(1, ++sequence, StartQpc + 51, threadIndex: ForeignThreadStreamIndex),
                            p => p.Write(AsyncProfilerTestData.AsyncEventsPayload(foreignAsyncBuffer)));
                    }
                    if (IncludeCoreLibUnloadRundown)
                    {
                        Frame[] coreLib = InputFrames().Where(frame =>
                            frame.ModuleName == AsyncProfilerRuntimeSupport.HostModuleName).ToArray();
                        foreach (Frame frame in coreLib.Where(ResolvedBeforeCoreLibUnload))
                        {
                            w.WriteEventBlob(EventOptions(10, ++sequence, StartQpc + 60),
                                p => WriteCoreLibMethod(p, frame, addresses[frame.Key]));
                        }
                        w.WriteEventBlob(EventOptions(11, ++sequence, StartQpc + 61), WriteCoreLibModule);
                        foreach (Frame frame in coreLib.Where(frame => !ResolvedBeforeCoreLibUnload(frame)))
                        {
                            w.WriteEventBlob(EventOptions(12, ++sequence, StartQpc + 62),
                                p => WriteCoreLibMethod(p, frame, addresses[frame.Key]));
                        }
                        w.WriteEventBlob(EventOptions(13, ++sequence, StartQpc + 63), WriteCoreLibModule);
                    }
                });
                writer.WriteEndBlock();
                return writer.ToArray();
            }

            private bool ResolvedBeforeCoreLibUnload(Frame frame) =>
                frame == CoreLibMethodBeforeUnload || frame.Kind == FrameKind.V2Wrapper || frame == Frame.ThreadPoolDispatch ||
                frame.SymbolName == "System.Threading.Tasks.Task.RunContinuations";

            private static void WriteCoreLibMethod(BinaryWriter writer, Frame frame, ulong address)
            {
                int signature = frame.SymbolName.IndexOf('(');
                int methodEnd = signature < 0 ? frame.SymbolName.Length : signature;
                int typeEnd = frame.SymbolName.LastIndexOf('.', methodEnd - 1);
                writer.Write(address);
                writer.Write(0xcafeUL); // ModuleID is shared by unload and rundown.
                writer.Write(address);
                writer.Write(0x20);
                writer.Write(0x06000000 | (int)(address >> 5));
                writer.Write(8); // MethodFlags.Jitted
                writer.WriteNullTerminatedUTF16String(frame.SymbolName.Substring(0, typeEnd));
                writer.WriteNullTerminatedUTF16String(frame.SymbolName.Substring(typeEnd + 1, methodEnd - typeEnd - 1));
                writer.WriteNullTerminatedUTF16String(signature < 0 ? "" : frame.SymbolName.Substring(signature));
                writer.Write((ushort)1);
                writer.Write(0L);
            }

            private static void WriteCoreLibModule(BinaryWriter writer)
            {
                writer.Write(0xcafeUL);
                writer.Write(0xbabeUL);
                writer.Write(0);
                writer.Write(0);
                writer.WriteNullTerminatedUTF16String(@"C:\runtime\System.Private.CoreLib.dll");
                writer.WriteNullTerminatedUTF16String("");
                writer.Write((ushort)1);
                writer.Write(new Guid("45b6c23e-36c5-4926-b688-817c8a9b12b1").ToByteArray());
                writer.Write(1);
                writer.WriteNullTerminatedUTF16String("System.Private.CoreLib.pdb");
                writer.Write(Guid.Empty.ToByteArray());
                writer.Write(0);
                writer.WriteNullTerminatedUTF16String("");
            }

            private Dictionary<string, ModuleMapping> CreateModuleMappings()
            {
                var result = new Dictionary<string, ModuleMapping>();
                ulong mappingId = 1;
                ulong startAddress = 0x1000;
                foreach (Frame frame in InputFrames())
                {
                    if (frame == Frame.Zero)
                    {
                        continue;
                    }

                    if (!result.ContainsKey(frame.ModuleName))
                    {
                        result.Add(frame.ModuleName,
                            new ModuleMapping(mappingId++, frame.ModuleName, startAddress, startAddress + 0x1000));
                        startAddress += 0x1000;
                    }
                }
                return result;
            }

            private Dictionary<string, ulong> AssignAddresses(Dictionary<string, ModuleMapping> mappings)
            {
                var nextAddress = mappings.ToDictionary(pair => pair.Key, pair => pair.Value.StartAddress + 0x100);
                var result = new Dictionary<string, ulong>();
                foreach (Frame frame in InputFrames())
                {
                    if (frame == Frame.Zero)
                    {
                        if (!result.ContainsKey(frame.Key))
                        {
                            result.Add(frame.Key, 0);
                        }
                        continue;
                    }

                    if (!result.ContainsKey(frame.Key))
                    {
                        result.Add(frame.Key, nextAddress[frame.ModuleName]);
                        nextAddress[frame.ModuleName] += 0x20;
                    }
                }
                return result;
            }

            private IEnumerable<Frame> InputFrames()
            {
                var seen = new HashSet<string>();
                IEnumerable<Frame> frames =
                    Sync.Concat(GetAsyncSegments().SelectMany(segment => segment.Frames));
                if (ContextParent != null)
                {
                    frames = frames.Concat(ContextParent.Frames);
                }
                foreach (Frame frame in frames)
                {
                    if (seen.Add(frame.Key))
                    {
                        yield return frame;
                    }
                }
            }

            private EmittedStack ReadSingleStack(MutableTraceEventStackSource stackSource)
            {
                EmittedStack result = null;
                double primaryTimeRelativeMSec = stackSource.TraceLog.QPCTimeToRelMSec(SampleQpc);
                int sampleCount = 0;
                int matchingSampleCount = 0;
                stackSource.ForEach(sample =>
                {
                    sampleCount++;
                    EmittedStack candidate = ReadStack(stackSource, sample.StackIndex);
                    if (sample.TimeRelativeMSec == primaryTimeRelativeMSec &&
                        candidate.GroupingFrames.Contains("CPU_TIME"))
                    {
                        matchingSampleCount++;
                        result = candidate;
                    }
                });
                Assert.Equal(1, matchingSampleCount);
                if (!AllowSupplementalSamples)
                {
                    Assert.Equal(1, sampleCount);
                }
                return result;
            }

            private void AssertThreadTimeStack(
                MutableTraceEventStackSource stackSource,
                string metricFrame,
                string[] expectedScenarioFrames)
            {
                bool found = false;
                stackSource.ForEach(sample =>
                {
                    EmittedStack stack = ReadStack(stackSource, sample.StackIndex);
                    if (stack.GroupingFrames.Contains(metricFrame) &&
                        stack.ScenarioFrames.SequenceEqual(expectedScenarioFrames))
                    {
                        if (IncludeStartStopActivity)
                        {
                            Assert.Contains(stack.RootFrames, name =>
                                name.IndexOf("Activity Request", StringComparison.Ordinal) >= 0);
                        }
                        found = true;
                    }
                });
                Assert.True(found, $"No {metricFrame} sample was emitted.");
            }

            private EmittedStack ReadStack(MutableTraceEventStackSource stackSource, StackSourceCallStackIndex stackIndex)
            {
                var scenarioFrames = new List<string>();
                var groupingFrames = new List<string>();
                var rootFrames = new List<string>();
                bool inRoot = false;

                while (stackIndex != StackSourceCallStackIndex.Invalid)
                {
                    string name = stackSource.GetFrameName(stackSource.GetFrameIndex(stackIndex), false);
                    if (name.StartsWith("Thread (", StringComparison.Ordinal))
                    {
                        inRoot = true;
                    }

                    if (inRoot)
                    {
                        rootFrames.Add(name);
                    }
                    else
                    {
                        Frame frame = DeclaredFrames()
                            .Where(candidate =>
                                candidate != Frame.Zero &&
                                (name.Contains(candidate.SymbolName) || name.Contains(candidate.Label)))
                            .OrderByDescending(candidate =>
                                Math.Max(
                                    name.Contains(candidate.SymbolName) ? candidate.SymbolName.Length : 0,
                                    name.Contains(candidate.Label) ? candidate.Label.Length : 0))
                            .FirstOrDefault();
                        if (frame == null && OmitSymbolFor != null)
                        {
                            CodeAddressIndex address = stackSource.GetFrameCodeAddress(
                                stackSource.GetFrameIndex(stackIndex));
                            if (address != CodeAddressIndex.Invalid &&
                                stackSource.TraceLog.CodeAddresses[address].Address == _addresses[OmitSymbolFor.Key])
                            {
                                Assert.Equal(MethodIndex.Invalid, stackSource.TraceLog.CodeAddresses.MethodIndex(address));
                                Assert.Equal(OmitSymbolFor.ModuleName, stackSource.TraceLog.CodeAddresses[address].ModuleName, ignoreCase: true);
                                frame = OmitSymbolFor;
                            }
                        }
                        if (frame != null)
                        {
                            scenarioFrames.Add(frame.Label);
                        }
                        else if (!string.Equals(name, "BROKEN", StringComparison.Ordinal))
                        {
                            groupingFrames.Add(name);
                        }
                    }
                    stackIndex = stackSource.GetCallerIndex(stackIndex);
                }

                return new EmittedStack(
                    scenarioFrames.ToArray(),
                    groupingFrames.ToArray(),
                    rootFrames.ToArray());
            }

            private string[] AsyncFrameLabels(
                TraceLog traceLog, AsyncProfilerCallStackFrames frames, AsyncSegment expectedSegment)
            {
                var result = new string[frames.FrameCount];
                for (int i = 0; i < result.Length; i++)
                {
                    Frame expected = expectedSegment.Frames[i];
                    CodeAddressIndex codeAddress = frames.CodeAddressAt(i);
                    string name = codeAddress == CodeAddressIndex.Invalid
                        ? "0x" + frames.InstructionPointerAt(i).ToString("x")
                        : traceLog.CodeAddresses[codeAddress].FullMethodName;
                    if (expected == Frame.Zero)
                    {
                        Assert.Equal(0UL, frames.InstructionPointerAt(i));
                    }
                    else if (expected.Key == OmitSymbolFor?.Key)
                    {
                        Assert.NotEqual(CodeAddressIndex.Invalid, codeAddress);
                        Assert.Equal(MethodIndex.Invalid, traceLog.CodeAddresses.MethodIndex(codeAddress));
                        Assert.Equal(_addresses[expected.Key], traceLog.CodeAddresses[codeAddress].Address);
                        Assert.Equal(expected.ModuleName, traceLog.CodeAddresses[codeAddress].ModuleName, ignoreCase: true);
                    }
                    else
                    {
                        Assert.Contains(expected.SymbolName, name);
                    }
                    result[i] = expected.Label;
                }
                return result;
            }

            private IEnumerable<Frame> DeclaredFrames() =>
                Sync.Concat(GetAsyncSegments().SelectMany(segment => segment.Frames))
                    .Concat(ContextParent?.Frames ?? Array.Empty<Frame>())
                    .Concat(ExpectedStitched);

            private IEnumerable<Frame> ExpectedFilteredFrames()
            {
                var filteredKeys = new HashSet<string>(FilteredOut.Select(frame => frame.Key));
                return ExpectedStitched.Where(frame => !filteredKeys.Contains(frame.Key));
            }

            private bool IncludeFilteredFrame(TraceLog traceLog, AsyncProfilerCallStackFrame stitchedFrame)
            {
                if (stitchedFrame.CodeAddress == CodeAddressIndex.Invalid)
                {
                    return true;
                }

                string methodName = traceLog.CodeAddresses[stitchedFrame.CodeAddress].FullMethodName;
                return !(FilteredOut ?? Array.Empty<Frame>()).Any(frame => methodName.Contains(frame.SymbolName));
            }

            private void ConfigurePresentation(
                AsyncCallStackTransforms transforms, AsyncProfilerCallStackTransformPipeline pipeline,
                bool enableNormalization, bool enableInfrastructureCleanup)
            {
                if (ExpectedRuntimeSupport != null)
                {
                    pipeline.AddCustomTransform(context =>
                    {
                        Assert.Same(ExpectedRuntimeSupport, context.RuntimeSupport);
                        var standalone = new AsyncProfilerCallStackTransformState(context.TraceLog, context.Segments,
                            context.TimestampQpc, context.ClassifyBoundary, context.StitchingState.Copy(), new AsyncProfilerCallStackDiagnostics(),
                            new List<AsyncProfilerStitchedFrame>(context.Frames));
                        Assert.Same(ExpectedRuntimeSupport, standalone.RuntimeSupport);
                    });
                }
                transforms.EnableNormalization = RuntimeSupportForModule == null && enableNormalization;
                transforms.EnableInfrastructureCleanup = RuntimeSupportForModule == null && enableInfrastructureCleanup;
                if (RuntimeSupportForModule == null)
                {
                    return;
                }

                // Install a hypothetical test support after stitching, without adding fake production versions.
                var presentation = new AsyncProfilerCallStackTransformPipeline
                {
                    EnableNormalization = enableNormalization,
                    EnableInfrastructureCleanup = enableInfrastructureCleanup,
                };
                pipeline.AddCustomTransform(context =>
                {
                    Func<CodeAddressIndex, AsyncProfilerRuntimeSupport> runtimeSupportResolver = ca =>
                        RuntimeSupportForModule(ca == CodeAddressIndex.Invalid
                            ? null : context.TraceLog.CodeAddresses.ModuleFile(ca));
                    for (int i = 0; i < context.Frames.Count; i++)
                    {
                        AsyncProfilerStitchedFrame frame = context.Frames[i];
                        if (frame.Origin != AsyncProfilerStitchedFrameOrigin.Sync)
                        {
                            continue;
                        }
                        string name = frame.CodeAddress == CodeAddressIndex.Invalid
                            ? null : context.TraceLog.CodeAddresses[frame.CodeAddress].FullMethodName;
                        FrameClassification kind = runtimeSupportResolver(frame.CodeAddress).ClassifySynchronousFrame(
                            name, context.GetHostModuleOwner(frame.CodeAddress) != ModuleFileIndex.Invalid);
                        context.Frames[i] = AsyncProfilerStitchedFrame.CreateSync(
                            new AsyncProfilerStitchSyncFrame(frame.CodeAddress, frame.Method, kind, frame.BoxedStateMachineMethod),
                            frame.Context);
                    }
                    presentation.ApplyInPlace(new AsyncProfilerCallStackTransformState(context.TraceLog, context.Segments,
                        context.TimestampQpc, context.ClassifyBoundary, context.StitchingState, context.Diagnostics, context.Frames, runtimeSupportResolver));
                });
            }

            private void Validate()
            {
                Assert.NotNull(Sync);
                if (SampleHasStack)
                {
                    Assert.NotEmpty(Sync);
                }
                AsyncSegment[] segments = GetAsyncSegments();
                Assert.NotEmpty(segments);
                foreach (AsyncSegment segment in segments)
                {
                    Assert.NotNull(segment.Frames);
                    Assert.NotEmpty(segment.Frames);
                    if (segment.States != null)
                    {
                        Assert.Equal(segment.Frames.Length, segment.States.Length);
                    }
                }
                if (ContextParent != null)
                {
                    Assert.NotNull(ContextParent.Frames);
                    Assert.NotEmpty(ContextParent.Frames);
                }
                Assert.NotNull(ExpectedStitched);
                Assert.True(AsyncStartQpc < AsyncEndQpc);

                var inputKeys = new HashSet<string>(
                    Sync.Concat(segments.SelectMany(segment => segment.Frames))
                        .Concat(ContextParent?.Frames ?? Array.Empty<Frame>())
                        .Select(frame => frame.Key));
                foreach (Frame expected in ExpectedStitched)
                {
                    Assert.True(inputKeys.Contains(expected.Key),
                        $"Expected stitched frame '{expected.Label}' is not present in the sync or async input.");
                }
                foreach (Frame filtered in FilteredOut ?? Array.Empty<Frame>())
                {
                    Assert.Contains(ExpectedStitched, expected => expected.Key == filtered.Key);
                }
            }

            private AsyncSegment[] GetAsyncSegments()
            {
                if (AsyncSegments != null)
                {
                    return AsyncSegments;
                }

                return new[]
                {
                    new AsyncSegment(Kind, Async, AsyncStates, ContinuationIndexBase),
                };
            }

            private static string[] Labels(IEnumerable<Frame> frames) => frames.Select(frame => frame.Label).ToArray();

            private static WriteEventOptions EventOptions(
                int metadataId,
                int sequence,
                long timestamp,
                int stackId = 0,
                long threadIndex = ThreadStreamIndex,
                Guid activityId = default)
            {
                return new WriteEventOptions
                {
                    MetadataId = metadataId,
                    ThreadIndexOrId = threadIndex,
                    CaptureThreadIndexOrId = threadIndex,
                    SequenceNumber = sequence,
                    StackId = stackId,
                    Timestamp = timestamp,
                    ActivityId = activityId,
                    IsSorted = true,
                };
            }

            private const int ProcessId = 1234;
            private const int ForeignProcessId = 5678;
            private const int OsThreadId = 5000;
            private const long ThreadStreamIndex = 7;
            private const long ForeignThreadStreamIndex = 8;
            private const long StartQpc = 100_000;
            private const long SampleQpc = StartQpc + 25;
            private Dictionary<string, ulong> _addresses;

            #endregion
        }

        private sealed class Frame
        {
            public const string ApplicationModule = "TestApp";

            public string ModuleName { get; }
            public string SymbolName { get; }
            public string RawSymbolName { get; }
            public string Label { get; }
            public FrameKind Kind { get; }
            public byte WrapperIndex { get; }
            public string Key => ModuleName + "!" + SymbolName;

            public static Frame App(string name) => new Frame(ApplicationModule, name, name);

            public static Frame Library(string moduleName, string symbolName, string label) =>
                new Frame(moduleName, symbolName, label);

            public static Frame CoreLib(string symbolName, string label) =>
                new Frame(AsyncProfilerRuntimeSupport.HostModuleName, symbolName, label);

            public static Frame PerfMap(string moduleName, string methodSignature, string label) =>
                new Frame(moduleName, methodSignature, label,
                    rawSymbolName: "instance void [" + moduleName + "] " + methodSignature + "[Optimized]");

            public Frame DispatcherFrames() =>
                new Frame(ModuleName, SymbolName, "[Async dispatcher frames]", Kind, WrapperIndex);

            public Frame Logical(string label, int? state = null)
            {
                Assert.True(
                    AsyncProfilerMethodNames.TryGetLogicalStateMachineMethodName(
                        AsyncProfilerMethodNames.NormalizeMethodName(SymbolName), out _, out string stateMachineSuffix));
                string ordinal = stateMachineSuffix.StartsWith("d__", StringComparison.Ordinal)
                    ? stateMachineSuffix.Substring(3)
                    : (stateMachineSuffix.StartsWith("d_", StringComparison.Ordinal)
                        ? stateMachineSuffix.Substring(2)
                        : stateMachineSuffix);
                return new Frame(
                    ModuleName,
                    SymbolName,
                    state.HasValue
                        ? label + " [ordinal " + ordinal + ", state " + state.Value + "]"
                        : label + " [ordinal " + ordinal + "]",
                    Kind,
                    WrapperIndex,
                    RawSymbolName);
            }

            public static Frame V1InlineInfrastructure(string level, string name) =>
                new Frame(AsyncProfilerRuntimeSupport.HostModuleName, level + "." + name, level + "." + name);

            public static Frame V2Wrapper(int index) =>
                new Frame(AsyncProfilerRuntimeSupport.HostModuleName,
                    "System.Runtime.CompilerServices.AsyncProfiler.ContinuationWrapper." +
                    AsyncProfilerCallStackStitchBoundary.ContinuationWrapperPrefix + index,
                    AsyncProfilerCallStackStitchBoundary.ContinuationWrapperPrefix + index,
                    FrameKind.V2Wrapper,
                    checked((byte)index));

            public static readonly Frame Zero = new Frame(null, null, "?? (0x0)");

            public static readonly Frame V2InstrumentedDispatch =
                new Frame(AsyncProfilerRuntimeSupport.HostModuleName,
                    "System.Threading.Tasks.RuntimeAsyncTask." + AsyncProfilerCallStackStitchBoundary.InstrumentedDispatchContinuationsName,
                    AsyncProfilerCallStackStitchBoundary.InstrumentedDispatchContinuationsName);

            public static readonly Frame V2Dispatch =
                new Frame(AsyncProfilerRuntimeSupport.HostModuleName,
                    "System.Threading.Tasks.RuntimeAsyncTask." + AsyncProfilerCallStackStitchBoundary.DispatchContinuationsName,
                    AsyncProfilerCallStackStitchBoundary.DispatchContinuationsName);

            public static readonly Frame V1MoveNextAsDispatcher =
                new Frame(AsyncProfilerRuntimeSupport.HostModuleName,
                    "System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncProfilerAsyncStateMachineBox." +
                    AsyncProfilerCallStackStitchBoundary.MoveNextAsDispatcherName + "(Thread,Flags)",
                    AsyncProfilerCallStackStitchBoundary.MoveNextAsDispatcherName);

            public static readonly Frame ThreadPoolDispatch =
                new Frame(AsyncProfilerRuntimeSupport.HostModuleName,
                    "System.Threading.ThreadPoolWorkQueue.Dispatch",
                    "ThreadPoolWorkQueue.Dispatch");

            public static readonly Frame WorkerThreadStart =
                new Frame(AsyncProfilerRuntimeSupport.HostModuleName,
                    "System.Threading.PortableThreadPool.WorkerThread.WorkerThreadStart",
                    "WorkerThreadStart");

            #region private

            private Frame(string moduleName, string symbolName, string label, FrameKind kind = FrameKind.Ordinary,
                byte wrapperIndex = 0, string rawSymbolName = null)
            {
                ModuleName = moduleName;
                SymbolName = symbolName;
                RawSymbolName = rawSymbolName ?? symbolName;
                Label = label;
                Kind = kind;
                WrapperIndex = wrapperIndex;
            }

            #endregion
        }

        private enum FrameKind
        {
            Ordinary,
            V2Wrapper,
        }

        private sealed class AsyncSegment
        {
            public AsyncSegment(
                AsyncCallstackKind kind, Frame[] frames, int[] states = null, byte continuationIndexBase = 0)
            {
                Kind = kind;
                Frames = frames;
                States = states;
                ContinuationIndexBase = continuationIndexBase;
            }

            public AsyncCallstackKind Kind { get; }
            public Frame[] Frames { get; }
            public int[] States { get; }
            public byte ContinuationIndexBase { get; }
            public int MethodCompletions { get; set; }
            public int ExceptionCompletions { get; set; }

            public static AsyncSegment Runtime(params Frame[] frames) =>
                new AsyncSegment(AsyncCallstackKind.RuntimeAsync, frames);

            public static AsyncSegment StateMachine(params Frame[] frames) =>
                new AsyncSegment(AsyncCallstackKind.StateMachineAsync, frames, new int[frames.Length]);
        }

        private sealed class ModuleMapping
        {
            public ModuleMapping(ulong id, string name, ulong startAddress, ulong endAddress)
            {
                Id = id;
                Name = name;
                StartAddress = startAddress;
                EndAddress = endAddress;
            }

            public ulong Id { get; }
            public string Name { get; }
            public ulong StartAddress { get; }
            public ulong EndAddress { get; }
        }

        private sealed class EmittedStack
        {
            public EmittedStack(string[] scenarioFrames, string[] groupingFrames, string[] rootFrames)
            {
                ScenarioFrames = scenarioFrames;
                GroupingFrames = groupingFrames;
                RootFrames = rootFrames;
            }

            public string[] ScenarioFrames { get; }
            public string[] GroupingFrames { get; }
            public string[] RootFrames { get; }
        }

        private sealed class CallStackDiagnosticsExpectation
        {
            public int SegmentsProcessed { get; set; }
            public int BoundariesNotFound { get; set; }
            public int IncompleteAsyncRootsNormalized { get; set; }
            public int V2TruncatedSuffixFallbackUsed { get; set; }
            public int AdjacencyMismatches { get; set; }
            public int V2InfrastructureFramesCollapsed { get; set; }
            public int V1InfrastructureFramesCollapsed { get; set; }
            public int V1SynchronousMoveNextFramesNormalized { get; set; }
            public int V1ReentrantFramesCollapsed { get; set; }
            public int V1SynchronousStartupFramesCollapsed { get; set; }
            public int AsyncInfrastructureFramesCollapsed { get; set; }
            public int RuntimeResumptionBridgeFramesCollapsed { get; set; }
            public int ProfilerEnvelopeFramesCollapsed { get; set; }
            public int AsyncReentrantHandoffFramesCollapsed { get; set; }
            public int V2ReentrantHandoffFramesCollapsed { get; set; }
            public int V2CompletionHandoffFramesCollapsed { get; set; }
            public int V1InlineFallbackUsed { get; set; }
            public int V1CompletionTailUsed { get; set; }
            public int V1CompletionUnwindFramesCollapsed { get; set; }
            public int V2SyncLayoutUsed { get; set; }
            public int V2LeafWrapperDropped { get; set; }

            public void Assert(AsyncProfilerCallStackDiagnostics actual)
            {
                Xunit.Assert.Equal(SegmentsProcessed, actual.SegmentsProcessed);
                Xunit.Assert.Equal(BoundariesNotFound, actual.BoundariesNotFound);
                Xunit.Assert.Equal(IncompleteAsyncRootsNormalized, actual.IncompleteAsyncRootsNormalized);
                Xunit.Assert.Equal(V2TruncatedSuffixFallbackUsed, actual.V2TruncatedSuffixFallbackUsed);
                Xunit.Assert.Equal(AdjacencyMismatches, actual.AdjacencyMismatches);
                Xunit.Assert.Equal(V2InfrastructureFramesCollapsed, actual.V2InfrastructureFramesCollapsed);
                Xunit.Assert.Equal(V1InfrastructureFramesCollapsed, actual.V1InfrastructureFramesCollapsed);
                Xunit.Assert.Equal(V1SynchronousMoveNextFramesNormalized, actual.V1SynchronousMoveNextFramesNormalized);
                Xunit.Assert.Equal(V1ReentrantFramesCollapsed, actual.V1ReentrantFramesCollapsed);
                Xunit.Assert.Equal(V1SynchronousStartupFramesCollapsed, actual.V1SynchronousStartupFramesCollapsed);
                Xunit.Assert.Equal(AsyncInfrastructureFramesCollapsed, actual.AsyncInfrastructureFramesCollapsed);
                Xunit.Assert.Equal(RuntimeResumptionBridgeFramesCollapsed, actual.RuntimeResumptionBridgeFramesCollapsed);
                Xunit.Assert.Equal(ProfilerEnvelopeFramesCollapsed, actual.ProfilerEnvelopeFramesCollapsed);
                Xunit.Assert.Equal(AsyncReentrantHandoffFramesCollapsed, actual.AsyncReentrantHandoffFramesCollapsed);
                Xunit.Assert.Equal(V2ReentrantHandoffFramesCollapsed, actual.V2ReentrantHandoffFramesCollapsed);
                Xunit.Assert.Equal(V2CompletionHandoffFramesCollapsed, actual.V2CompletionHandoffFramesCollapsed);
                Xunit.Assert.Equal(V1InlineFallbackUsed, actual.V1InlineFallbackUsed);
                Xunit.Assert.Equal(V1CompletionTailUsed, actual.V1CompletionTailUsed);
                Xunit.Assert.Equal(V1CompletionUnwindFramesCollapsed, actual.V1CompletionUnwindFramesCollapsed);
                Xunit.Assert.Equal(V2SyncLayoutUsed, actual.V2SyncLayoutUsed);
                Xunit.Assert.Equal(V2LeafWrapperDropped, actual.V2LeafWrapperDropped);
            }
        }

        #endregion
    }
}
