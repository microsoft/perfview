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
    public partial class AsyncProfilerStitchIntegrationTests
    {
        [Theory]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, false)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, true)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, false)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, true)]
        public void PerfMapSymbols_StitchBothAsyncFamiliesWithoutRewritingStoredSymbols(
            int kindValue, bool enableInfrastructureCleanup)
        {
            var kind = (AsyncCallstackKind)kindValue;
            bool stateMachine = kind == AsyncCallstackKind.StateMachineAsync;
            Frame current = Frame.PerfMap(Frame.ApplicationModule,
                stateMachine ? "Scenario+<ChildAsync>d__1::MoveNext()" : "AsyncDemo.ClockScenario::ChildAAsync(int32)",
                "Physical current");
            Frame parent = Frame.PerfMap(Frame.ApplicationModule,
                stateMachine ? "Scenario+<ParentAsync>d__2::MoveNext()" : "AsyncDemo.ClockScenario::ParentAAsync(int32)",
                "Captured parent");
            Frame native = Frame.Library("NativeHelpers", "NativeHelper::Run()", "NativeHelper::Run");
            Frame dispatcher = Frame.PerfMap(AsyncProfilerRuntimeSupport.HostModuleName,
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncProfilerAsyncStateMachineBox`1[" +
                "System.Int64,Scenario+<ChildAsync>d__1]::MoveNextAsDispatcher(class System.Threading.Thread," +
                "valuetype System.Runtime.CompilerServices.AsyncInstrumentation/Flags)", "V1 dispatcher");
            Frame wrapper = Frame.PerfMap(AsyncProfilerRuntimeSupport.HostModuleName,
                "System.Runtime.CompilerServices.AsyncProfiler+ContinuationWrapper::Continuation_Wrapper_1(" +
                "class System.Runtime.CompilerServices.Continuation,uint8&)", "V2 wrapper");
            const string runtimeTask = "System.Runtime.CompilerServices.AsyncHelpers+RuntimeAsyncTask`1[" +
                "System.Threading.Tasks.VoidTaskResult]";
            Frame instrumented = Frame.PerfMap(AsyncProfilerRuntimeSupport.HostModuleName,
                runtimeTask + "::InstrumentedDispatchContinuations(valuetype System.Runtime.CompilerServices.AsyncInstrumentation/Flags)",
                "V2 instrumented dispatch");
            Frame dispatch = Frame.PerfMap(AsyncProfilerRuntimeSupport.HostModuleName,
                runtimeTask + "::DispatchContinuations()", "V2 dispatch");
            Frame threadPool = Frame.PerfMap(AsyncProfilerRuntimeSupport.HostModuleName,
                "System.Threading.ThreadPoolWorkQueue::Dispatch()", "ThreadPoolWorkQueue.Dispatch");
            Frame worker = Frame.PerfMap(AsyncProfilerRuntimeSupport.HostModuleName,
                "System.Threading.PortableThreadPool+WorkerThread::WorkerThreadStart()", "WorkerThreadStart");
            Frame logicalCurrent = stateMachine ? current.Logical("Scenario.ChildAsync") : current;
            Frame logicalParent = stateMachine ? parent.Logical("Scenario.ParentAsync") : parent;
            var scenario = new StitchScenario
            {
                Kind = kind,
                Sync = stateMachine
                    ? new[] { native, current, dispatcher, threadPool, worker }
                    : new[] { native, current, wrapper, instrumented, dispatch, threadPool, worker },
                Async = new[] { current, parent },
                AsyncStates = stateMachine ? new[] { 0, 1 } : null,
                ContinuationIndexBase = 1,
                ExpectedStitched = new[] { native, logicalCurrent, logicalParent, threadPool, worker },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V1InlineFallbackUsed = stateMachine ? 2 : 0,
                    V2InfrastructureFramesCollapsed = stateMachine ? 0 : 4,
                },
                EnableInfrastructureCleanup = enableInfrastructureCleanup,
                VerifySampleWeights = true,
                AssertStitchedStackSource = stackSource =>
                {
                    var names = new List<string>();
                    stackSource.ForEach(sample =>
                    {
                        for (var stack = sample.StackIndex; stack != StackSourceCallStackIndex.Invalid;
                            stack = stackSource.GetCallerIndex(stack))
                        {
                            names.Add(stackSource.GetFrameName(stackSource.GetFrameIndex(stack), false));
                        }
                    });
                    Assert.Contains(names, name => name.Contains(worker.SymbolName));
                    Assert.Contains(names, name => name.Contains(native.SymbolName));
                    Assert.DoesNotContain(names, name => name.Contains(AsyncProfilerMethodNames.NormalizeMethodName(worker.SymbolName)));
                },
            };
            scenario.AssertProductionStitch();
        }

        [Fact]
        public void PerfMapSymbols_V1ReentrantRegistrationMatchesItsKickoff()
        {
            Frame work = Frame.PerfMap(Frame.ApplicationModule, "Scenario::ReentrantWork()", "ReentrantWork");
            Frame inner = Frame.PerfMap(Frame.ApplicationModule, "Scenario+<InnerAsync>d__1::MoveNext()", "Inner.MoveNext");
            Frame outer = Frame.PerfMap(Frame.ApplicationModule, "Scenario+<OuterAsync>d__2::MoveNext()", "Outer.MoveNext");
            Frame logicalInner = inner.Logical("Scenario.InnerAsync");
            Frame logicalOuter = outer.Logical("Scenario.OuterAsync");
            Frame kickoff = Frame.PerfMap(Frame.ApplicationModule, "Scenario::InnerAsync(int32)", "Kickoff");
            Frame dispatcher = Frame.PerfMap(AsyncProfilerRuntimeSupport.HostModuleName,
                "System.Runtime.CompilerServices.AsyncStateMachineDispatcher::MoveNext()", "AsyncStateMachineDispatcher.MoveNext");
            Frame box = Frame.PerfMap(AsyncProfilerRuntimeSupport.HostModuleName,
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncProfilerAsyncStateMachineBox::InstrumentedMoveNext()",
                "Box.InstrumentedMoveNext");
            Frame await = Frame.PerfMap(AsyncProfilerRuntimeSupport.HostModuleName,
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64]::AwaitUnsafeOnCompleted(!!0&,!!1&)",
                "Builder.AwaitUnsafeOnCompleted");
            Frame start = Frame.PerfMap(AsyncProfilerRuntimeSupport.HostModuleName,
                "System.Runtime.CompilerServices.AsyncMethodBuilderCore::Start(!!0&)", "AsyncMethodBuilderCore.Start");
            var scenario = new StitchScenario
            {
                Kind = AsyncCallstackKind.StateMachineAsync,
                Sync = new[]
                {
                    work, inner, dispatcher, box, await, inner, start, kickoff,
                    outer, dispatcher, box, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart,
                },
                AsyncSegments = new[] { AsyncSegment.StateMachine(logicalOuter), AsyncSegment.StateMachine(logicalInner) },
                ExpectedStitched = new[] { work, logicalInner, logicalOuter, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart },
                ExpectedWithoutNormalization = new[]
                {
                    work, logicalInner, await, inner, start, kickoff, logicalOuter,
                    Frame.ThreadPoolDispatch, Frame.WorkerThreadStart,
                },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 4,
                    V1InlineFallbackUsed = 4,
                    V1InfrastructureFramesCollapsed = 4,
                    V1ReentrantFramesCollapsed = 8,
                },
                VerifySampleWeights = true,
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync)]
        public void PerfMapSymbols_ZeroIpWithoutBoundary_PreservesPhysicalStackAndAppendsAsyncCallers(int kindValue)
        {
            var kind = (AsyncCallstackKind)kindValue;
            bool stateMachine = kind == AsyncCallstackKind.StateMachineAsync;
            Frame work = Frame.PerfMap(Frame.ApplicationModule, "Scenario::SampledWork()", "SampledWork");
            Frame current = Frame.PerfMap(Frame.ApplicationModule,
                stateMachine ? "Scenario+<CurrentAsync>d__1::MoveNext()" : "Scenario::CurrentAsync()", "Captured current");
            Frame parent = Frame.PerfMap(Frame.ApplicationModule,
                stateMachine ? "Scenario+<ParentAsync>d__2::MoveNext()" : "Scenario::ParentAsync()", "Captured parent");
            var scenario = new StitchScenario
            {
                Kind = kind,
                Sync = new[] { work, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart },
                Async = new[] { Frame.Zero, current, parent },
                ExpectedStitched = new[]
                {
                    work, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart,
                    stateMachine ? current.Logical("Scenario.CurrentAsync") : current,
                    stateMachine ? parent.Logical("Scenario.ParentAsync") : parent,
                },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    BoundariesNotFound = 2,
                    V1InlineFallbackUsed = stateMachine ? 2 : 0,
                },
                VerifySampleWeights = true,
                AssertTransformContext = context =>
                    Assert.DoesNotContain(context.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.IncompletePhysicalRoot),
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync)]
        public void VersionedWholeBridge_RejectsAnOtherwiseAcceptedLayoutAtomically(int kindValue)
        {
            var kind = (AsyncCallstackKind)kindValue;
            StitchScenario scenario = CreateRuntimeResumptionBridgeScenario(kind);
            var support = new AsyncProfilerRejectRuntimeBridgeSupport();
            scenario.RuntimeSupportForModule = module => AsyncProfilerRuntimeResolver.IsHostModule(module)
                ? support : AsyncProfilerRuntimeSupport.Common;
            scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup;
            scenario.ExpectedWithoutInfrastructureCleanup = null;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void VersionedEntrySpineAndSchedulerAnchor_UseSharedOwnershipGuards(bool physicalCurrent, bool enableInfrastructureCleanup)
        {
            StitchScenario scenario = CreateProfilerEnvelopeScenario(physicalCurrent);
            UseExtendedRuntimeLayouts(scenario);
            Frame helper = Frame.CoreLib(AsyncProfilerExtendedRuntimeLayoutSupport.HelperName, "LayoutHelper");
            scenario.Sync = InsertEntryHelper(scenario.Sync, helper);
            scenario.ExpectedWithoutInfrastructureCleanup =
                InsertEntryHelper(scenario.ExpectedWithoutInfrastructureCleanup, helper);
            scenario.EnableInfrastructureCleanup = enableInfrastructureCleanup;
            if (enableInfrastructureCleanup)
            {
                scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed += 2;
                scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed += 2;
            }
            else
            {
                scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup;
                scenario.ExpectedWithoutInfrastructureCleanup = null;
                scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
                scenario.ExpectedDiagnostics.ProfilerEnvelopeFramesCollapsed = 0;
                scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;
            }
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("11.0.0", false)]
        [InlineData("12.0.0", false)]
        [InlineData("11.0.0", true)]
        public void RuntimeResolver_WholeStageSelectionUsesCapturedProcessEvenWithoutRetainedRuntimeFrames(
            string productVersion, bool conflicting)
        {
            var scenario = new StitchScenario
            {
                Sync = new[] { Frame.App("Scenario.Current"), Frame.V2Wrapper(0), Frame.V2Dispatch, Frame.App("Scenario.Caller") },
                Async = new[] { Frame.App("Scenario.Current"), Frame.App("Scenario.Parent") },
                ExpectedStitched = new[] { Frame.App("Scenario.Current"), Frame.App("Scenario.Parent"), Frame.App("Scenario.Caller") },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation { SegmentsProcessed = 2, V2InfrastructureFramesCollapsed = 2 },
                ExpectedRuntimeSupport = conflicting ? AsyncProfilerRuntimeSupport.Common : AsyncProfilerRuntimeSupport.CoreClr11,
                ConfigureTraceLog = log =>
                {
                    TraceModuleFile host = ConfigureCapturedRuntime(log, productVersion);
                    TraceProcess process = log.Processes.First(p => p.ProcessID == 1234);
                    if (conflicting)
                    {
                        TraceModuleFile other = process.LoadedModules.UniversalMapping(
                            @"C:\other\System.Private.CoreLib.dll", 0x220000, 0x221000, 100_001, null);
                        other.productVersion = "10.0.0";
                    }
                    var runtimeResolver = new AsyncProfilerRuntimeResolver(log.ModuleFiles);
                    Assert.Same(AsyncProfilerRuntimeSupport.CoreClr11, runtimeResolver.ResolveModuleSupport(host.ModuleFileIndex));
                    AsyncProfilerRuntimeSupport expected = conflicting
                        ? AsyncProfilerRuntimeSupport.Common : AsyncProfilerRuntimeSupport.CoreClr11;
                    Assert.Same(expected, runtimeResolver.ResolveProcessSupport(process.ProcessIndex));
                    Assert.Same(expected, runtimeResolver.ResolveProcessSupport(process.ProcessIndex));
                    Assert.Same(AsyncProfilerRuntimeSupport.Common, runtimeResolver.ResolveProcessSupport(ProcessIndex.Invalid));
                },
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
        public void VersionedCompletionPair_ResumptionBridgeUsesTheChangedLayout(
            int kindValue, bool enableNormalization, bool enableInfrastructureCleanup)
        {
            var kind = (AsyncCallstackKind)kindValue;
            StitchScenario scenario = CreateRuntimeResumptionBridgeScenario(kind);
            UseExtendedCompletionLayout(scenario);
            scenario.EnableNormalization = enableNormalization;
            scenario.EnableInfrastructureCleanup = enableInfrastructureCleanup;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = enableInfrastructureCleanup ? 12 : 0;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = enableInfrastructureCleanup ? 12 : 0;
            if (!enableInfrastructureCleanup)
            {
                scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup;
                scenario.ExpectedWithoutInfrastructureCleanup = null;
            }
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void VersionedCompletionPair_CompletedV2TailUsesTheChangedSpanLength(bool enableNormalization, bool enableInfrastructureCleanup)
        {
            StitchScenario scenario = CreateV1AfterV2CompletionScenario();
            UseExtendedCompletionLayout(scenario);
            scenario.EnableNormalization = enableNormalization;
            scenario.EnableInfrastructureCleanup = enableInfrastructureCleanup;
            scenario.ExpectedDiagnostics.V2CompletionHandoffFramesCollapsed = enableNormalization ? 12 : 0;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = !enableNormalization && enableInfrastructureCleanup ? 12 : 0;
            scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = !enableNormalization && enableInfrastructureCleanup ? 12 : 0;
            if (!enableNormalization && !enableInfrastructureCleanup)
            {
                scenario.ExpectedStitched = scenario.ExpectedWithoutNormalization;
            }
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync)]
        public void VersionedCompletionChain_RejectedBridgePreservesTheCompleteChain(int kindValue)
        {
            var kind = (AsyncCallstackKind)kindValue;
            StitchScenario scenario = CreateCallbackCompletionBridgeScenario(kind);
            UseExtendedCompletionLayout(scenario);
            scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup;
            scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, "unknown")]
        [InlineData(true, "unknown")]
        [InlineData(false, "unresolved")]
        [InlineData(true, "unresolved")]
        [InlineData(false, "foreign")]
        [InlineData(true, "foreign")]
        [InlineData(false, "application")]
        [InlineData(true, "application")]
        [InlineData(false, "other-corelib")]
        [InlineData(true, "other-corelib")]
        public void VersionedCompletionPair_RejectedHelperProtectsTheCompleteV2Tail(bool enableNormalization, string obstruction)
        {
            StitchScenario scenario = CreateV1AfterV2CompletionScenario();
            UseExtendedCompletionLayout(scenario);
            Frame helper = scenario.Sync.Single(frame => frame.SymbolName == AsyncProfilerExtendedCompletionRuntimeSupport.InjectedHelperName);
            if (obstruction == "unresolved")
            {
                scenario.OmitSymbolFor = helper;
            }
            else
            {
                Frame replacement = obstruction == "foreign"
                    ? Frame.Library("OtherLibrary", helper.SymbolName, "ForeignCompletionHelper")
                    : obstruction == "other-corelib"
                        ? Frame.Library(@"C:\other\System.Private.CoreLib.dll", helper.SymbolName, "OtherRuntimeCompletionHelper")
                    : obstruction == "application"
                        ? Frame.App("Scenario.ApplicationCallback")
                        : Frame.CoreLib("System.Threading.Tasks.Task.UnknownHelper", "UnknownCompletionHelper");
                scenario.Sync = scenario.Sync.Select(frame => frame == helper ? replacement : frame).ToArray();
                scenario.ExpectedWithoutNormalization = scenario.ExpectedWithoutNormalization
                    .Select(frame => frame.Key == helper.Key ? replacement : frame).ToArray();
            }
            scenario.EnableNormalization = enableNormalization;
            scenario.EnableInfrastructureCleanup = true;
            scenario.ExpectedStitched = scenario.ExpectedWithoutNormalization;
            scenario.ExpectedDiagnostics.V2CompletionHandoffFramesCollapsed = 0;
            scenario.AssertProductionStitch();
        }

        [Fact]
        public void VersionedCompletionPair_NativeAndIlAliasesShareOneRuntimeSequence()
        {
            StitchScenario scenario = CreateV1AfterV2CompletionScenario();
            UseExtendedCompletionLayout(scenario);
            Frame alias = Frame.Library(
                @"C:\runtime\System.Private.CoreLib.ni.dll",
                AsyncProfilerExtendedCompletionRuntimeSupport.InjectedHelperName, "NativeCompletionHelper");
            scenario.Sync = scenario.Sync.Select(frame =>
                frame.SymbolName == alias.SymbolName ? alias : frame).ToArray();
            scenario.ExpectedWithoutNormalization = scenario.ExpectedWithoutNormalization
                .Select(frame => frame.SymbolName == alias.SymbolName ? alias : frame).ToArray();
            scenario.ConfigureTraceLog = log =>
            {
                TraceModuleFile host = ConfigureCapturedRuntime(log, "11.0.0");
                log.ModuleFiles.Single(module => string.Equals(
                    module.Name, "System.Private.CoreLib.ni", StringComparison.OrdinalIgnoreCase)).managedModule = host;
            };
            scenario.ExpectedDiagnostics.V2CompletionHandoffFramesCollapsed = 12;
            scenario.AssertProductionStitch();
        }

        [Fact]
        public void CoreLibUnloadRundownAliases_CollapseStructuralInfrastructureAndResumptionBridge()
        {
            StitchScenario scenario = CreateRuntimeResumptionBridgeScenario(AsyncCallstackKind.RuntimeAsync);
            scenario.IncludeCoreLibUnloadRundown = true;
            scenario.EnableNormalization = false;
            scenario.ExpectedWithoutNormalization = scenario.ExpectedWithoutInfrastructureCleanup;
            scenario.ExpectedDiagnostics = new CallStackDiagnosticsExpectation
            {
                SegmentsProcessed = 2,
                V2InfrastructureFramesCollapsed = 4,
                AsyncInfrastructureFramesCollapsed = 10,
                RuntimeResumptionBridgeFramesCollapsed = 10,
            };
            scenario.ConfigureTraceLog = log =>
            {
                TraceModuleFile[] modules = log.ModuleFiles.Where(module =>
                    module.Name == AsyncProfilerRuntimeSupport.HostModuleName).ToArray();
                Assert.Equal(2, modules.Length);
                Assert.NotEqual(modules[0].ModuleFileIndex, modules[1].ModuleFileIndex);
                Assert.Equal(modules[0].FilePath, modules[1].FilePath);
                Assert.NotEqual(Guid.Empty, modules[0].PdbSignature);
                Assert.Equal(modules[0].PdbSignature, modules[1].PdbSignature);
                Assert.Null(modules[0].ManagedModule);
                Assert.Null(modules[1].ManagedModule);
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("path")]
        [InlineData("signature")]
        [InlineData("age")]
        [InlineData("missing-signature")]
        [InlineData("profile")]
        public void CoreLibUnloadRundownAliases_ConflictingOrMissingIdentityPreservesTheBridge(string obstruction)
        {
            StitchScenario scenario = CreateRuntimeResumptionBridgeScenario(AsyncCallstackKind.RuntimeAsync);
            scenario.IncludeCoreLibUnloadRundown = true;
            scenario.EnableNormalization = false;
            scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup.Take(3)
                .Concat(new[] { Frame.V2InstrumentedDispatch, Frame.V2Dispatch })
                .Concat(scenario.ExpectedWithoutInfrastructureCleanup.Skip(3)).ToArray();
            scenario.ExpectedWithoutInfrastructureCleanup = scenario.ExpectedStitched;
            scenario.ExpectedWithoutNormalization = scenario.ExpectedStitched;
            scenario.ExpectedDiagnostics = new CallStackDiagnosticsExpectation { SegmentsProcessed = 2 };
            scenario.ConfigureTraceLog = log =>
            {
                TraceModuleFile alias = log.ModuleFiles.Last(module =>
                    module.Name == AsyncProfilerRuntimeSupport.HostModuleName);
                if (obstruction == "path")
                {
                    alias.fileName = @"C:\other\System.Private.CoreLib.dll";
                }
                else if (obstruction == "signature")
                {
                    alias.PEInfo.PdbSignature = new Guid("fd85f1ed-6152-4ee3-bf58-917b38d65eaf");
                }
                else if (obstruction == "age")
                {
                    alias.PEInfo.PdbAge++;
                }
                else if (obstruction == "missing-signature")
                {
                    alias.PEInfo.PdbSignature = Guid.Empty;
                }
                else
                {
                    ConfigureCapturedRuntime(log, "11.0.0");
                    alias.productVersion = "10.0.0";
                }
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("11.0.0", false)]
        [InlineData("12.0.0", false)]
        [InlineData("10.0.0", true)]
        public void CoreLibUnloadRundownAliases_CompletionProjectionRequiresCompatibleSupports(
            string aliasProductVersion, bool conflictingSupport)
        {
            Frame target = Frame.App("Scenario.TargetAsync");
            Frame[] sync = { Frame.V2InstrumentedDispatch, Frame.V2Dispatch, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart };
            var scenario = new StitchScenario
            {
                Sync = sync,
                Async = new[] { target },
                IncludeCoreLibUnloadRundown = true,
                CoreLibMethodBeforeUnload = Frame.V2InstrumentedDispatch,
                MethodCompletions = 1,
                MethodCompletionQpc = 100_030,
                EnableNormalization = false,
                IncludeEventSourceEvents = false,
                VerifySampleWeights = true,
                ExpectedStitched = conflictingSupport ? sync : new[]
                {
                    Frame.V2InstrumentedDispatch.DispatcherFrames(), target, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart,
                },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = conflictingSupport ? 0 : 2,
                    V2SyncLayoutUsed = conflictingSupport ? 2 : 0,
                    V2InfrastructureFramesCollapsed = conflictingSupport ? 0 : 4,
                },
                ConfigureTraceLog = log =>
                {
                    ConfigureCapturedRuntime(log, "11.0.0");
                    TraceModuleFile alias = log.ModuleFiles.Last(module =>
                        module.Name == AsyncProfilerRuntimeSupport.HostModuleName);
                    alias.productVersion = aliasProductVersion;
                },
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("11.0.0", "coreclr.dll", true)]
        [InlineData("11.0.0-preview.1+abcdef", "libcoreclr.so", true)]
        [InlineData("11.0.0", "libcoreclr.dylib", true)]
        [InlineData("11.1.0", "coreclr.dll", true)]
        [InlineData("12.0.0", "coreclr.dll", true)]
        [InlineData("10.0.0", "coreclr.dll", false)]
        [InlineData(null, "coreclr.dll", false)]
        [InlineData("11.0.0", "libmonosgen-2.0.so", false)]
        [InlineData("11.0.0", null, false)]
        public void RuntimeResolver_CachesSupportPerCapturedModuleAcrossMixedRuntimes(
            string productVersion, string engineModule, bool supported)
        {
            var scenario = new StitchScenario
            {
                Sync = new[]
                {
                    Frame.App("Scenario.Current"), Frame.V2Wrapper(0), Frame.V2Dispatch,
                    Frame.ThreadPoolDispatch, Frame.WorkerThreadStart,
                },
                Async = new[] { Frame.App("Scenario.Current"), Frame.App("Scenario.Parent") },
                ExpectedStitched = new[]
                {
                    Frame.App("Scenario.Current"), Frame.App("Scenario.Parent"),
                    Frame.ThreadPoolDispatch, Frame.WorkerThreadStart,
                },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V2InfrastructureFramesCollapsed = 2,
                },
                ConfigureTraceLog = log =>
                {
                    TraceModuleFile host = ConfigureCapturedRuntime(log, productVersion, engineModule);
                    TraceProcess otherProcess = log.Processes.GetOrCreateProcess(9876, 100_000);
                    TraceModuleFile other = otherProcess.LoadedModules.UniversalMapping(
                        @"C:\other\System.Private.CoreLib.dll", 0x200000, 0x201000, 100_001, null);
                    other.productVersion = "12.0.0";
                    otherProcess.LoadedModules.UniversalMapping(
                        @"C:\other\coreclr.dll", 0x210000, 0x211000, 100_001, null);

                    var runtimeResolver = new AsyncProfilerRuntimeResolver(log.ModuleFiles);
                    AsyncProfilerRuntimeSupport expected = supported
                        ? AsyncProfilerRuntimeSupport.CoreClr11
                        : AsyncProfilerRuntimeSupport.Common;
                    Assert.Same(expected, runtimeResolver.ResolveModuleSupport(host.ModuleFileIndex));
                    Assert.Same(AsyncProfilerRuntimeSupport.CoreClr11,
                        runtimeResolver.ResolveModuleSupport(other.ModuleFileIndex));
                    Assert.Same(expected, runtimeResolver.ResolveProcessSupport(
                        log.Processes.First(p => p.ProcessID == 1234).ProcessIndex));
                    Assert.Same(AsyncProfilerRuntimeSupport.CoreClr11,
                        runtimeResolver.ResolveProcessSupport(otherProcess.ProcessIndex));
                    Assert.Same(expected, runtimeResolver.ResolveModuleSupport(host.ModuleFileIndex));
                    Assert.Same(AsyncProfilerRuntimeSupport.Common, runtimeResolver.ResolveModuleSupport(ModuleFileIndex.Invalid));

                    TraceModuleFile native = log.ModuleFiles.GetOrCreateModuleFile(
                        @"C:\runtime\System.Private.CoreLib.ni.dll", 0x220000);
                    native.managedModule = host;
                    Assert.True(runtimeResolver.IsHostModule(native.ModuleFileIndex));
                    Assert.Same(expected, runtimeResolver.ResolveModuleSupport(native.ModuleFileIndex));
                    Assert.Same(expected, runtimeResolver.ResolveModuleSupport(native.ModuleFileIndex));
                    var nativeFirstSupports = new AsyncProfilerRuntimeResolver(log.ModuleFiles);
                    Assert.Same(expected, nativeFirstSupports.ResolveModuleSupport(native.ModuleFileIndex));
                    Assert.Same(expected, nativeFirstSupports.ResolveModuleSupport(host.ModuleFileIndex));
                    Assert.Same(expected, nativeFirstSupports.ResolveModuleSupport(native.ModuleFileIndex));

                    ModuleFileIndex applicationModule = log.ModuleFiles.GetOrCreateModuleFile(
                        @"C:\app\NotCoreLib.dll", 0).ModuleFileIndex;
                    Assert.Same(AsyncProfilerRuntimeSupport.Common, runtimeResolver.ResolveModuleSupport(applicationModule));
                    Assert.Same(AsyncProfilerRuntimeSupport.Common, runtimeResolver.ResolveModuleSupport(applicationModule));
                    Assert.Same(AsyncProfilerRuntimeSupport.Common, runtimeResolver.ResolveModuleSupport(ModuleFileIndex.Invalid));
                },
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void RuntimeResolver_ConflictingOrUnknownModuleOwnersUseCommonRules(
            bool sharedWithAnotherProcess, bool unknownFlavor)
        {
            var scenario = new StitchScenario
            {
                Sync = new[] { Frame.App("Scenario.Current"), Frame.V2Wrapper(0), Frame.ThreadPoolDispatch },
                Async = new[] { Frame.App("Scenario.Current"), Frame.App("Scenario.Parent") },
                ExpectedStitched = new[] { Frame.App("Scenario.Current"), Frame.App("Scenario.Parent"), Frame.ThreadPoolDispatch },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation { SegmentsProcessed = 2 },
                ConfigureTraceLog = log =>
                {
                    TraceModuleFile host = ConfigureCapturedRuntime(log, "11.0.0");
                    TraceProcess process = sharedWithAnotherProcess
                        ? log.Processes.GetOrCreateProcess(9876, 100_000)
                        : log.Processes.First(p => p.ProcessID == 1234);
                    if (sharedWithAnotherProcess)
                    {
                        process.LoadedModules.UniversalMapping(
                            host.FilePath, host.ImageBase, host.ImageEnd, 100_001, null);
                    }
                    if (!unknownFlavor || !sharedWithAnotherProcess)
                    {
                        process.LoadedModules.UniversalMapping(
                            unknownFlavor ? @"C:\runtime\clr.dll" : @"C:\runtime\libmonosgen-2.0.so",
                            0x210000, 0x211000, 100_001, null);
                    }
                    Assert.Same(AsyncProfilerRuntimeSupport.Common,
                        new AsyncProfilerRuntimeResolver(log.ModuleFiles).ResolveModuleSupport(host.ModuleFileIndex));
                },
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, true, false)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, false, false)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, true, true)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, true, false)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, false, false)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, true, true)]
        public void PdbNames_EnableOnlyVerifiedResumptionBridgeCleanupWithoutRewritingSymbols(
            int kindValue, bool cleanup, bool lookalike)
        {
            StitchScenario scenario = CreateRuntimeResumptionBridgeScenario((AsyncCallstackKind)kindValue);
            Frame[] pdbFrames =
            {
                Frame.CoreLib("System.Void System.Threading.Tasks.Task::RunContinuations(System.Object)", "Task.RunContinuations"),
                Frame.CoreLib(lookalike
                    ? "System.Boolean Other.Namespace.Task::TrySetResult(!0)"
                    : "System.Boolean System.Threading.Tasks.Task`1[System.Int64]::TrySetResult(!0)", "Task.TrySetResult"),
                Frame.CoreLib("System.Threading.ThreadPoolWorkQueue+DispatchResult System.Threading.ThreadPoolWorkQueue::Dispatch()", "ThreadPoolWorkQueue.Dispatch"),
                Frame.CoreLib("System.Void System.Threading.PortableThreadPool+WorkerThread::WorkerThreadStart()", "WorkerThreadStart"),
            };
            foreach (Frame pdbFrame in pdbFrames)
            {
                Frame original = scenario.Sync.Single(frame => frame.Label == pdbFrame.Label);
                scenario.Sync = scenario.Sync.Select(frame => frame == original ? pdbFrame : frame).ToArray();
                scenario.ExpectedStitched = scenario.ExpectedStitched.Select(frame => frame == original ? pdbFrame : frame).ToArray();
                scenario.ExpectedWithoutInfrastructureCleanup = scenario.ExpectedWithoutInfrastructureCleanup
                    .Select(frame => frame == original ? pdbFrame : frame).ToArray();
            }
            scenario.EnableInfrastructureCleanup = cleanup;
            if (!cleanup || lookalike)
            {
                scenario.ExpectedStitched = scenario.ExpectedWithoutInfrastructureCleanup;
                scenario.ExpectedDiagnostics.AsyncInfrastructureFramesCollapsed = 0;
                scenario.ExpectedDiagnostics.RuntimeResumptionBridgeFramesCollapsed = 0;
            }
            scenario.AssertPublicApi = (log, path) =>
            {
                TraceEvent sample = log.Events.First(data => data.CallStackIndex() != CallStackIndex.Invalid);
                var names = new List<string>();
                for (CallStackIndex stack = sample.CallStackIndex(); stack != CallStackIndex.Invalid; stack = log.CallStacks.Caller(stack))
                {
                    names.Add(log.CodeAddresses[log.CallStacks.CodeAddressIndex(stack)].FullMethodName);
                }
                foreach (Frame frame in pdbFrames)
                {
                    Assert.Contains(frame.SymbolName, names);
                }
            };
            scenario.AssertProductionStitch();
        }

        #region private

        private static void UseExtendedCompletionLayout(StitchScenario scenario)
        {
            var support = new AsyncProfilerExtendedCompletionRuntimeSupport();
            scenario.RuntimeSupportForModule = module => AsyncProfilerRuntimeResolver.IsHostModule(module)
                ? support : AsyncProfilerRuntimeSupport.Common;
            scenario.Sync = ExpandCompletionPair(scenario.Sync);
            if (scenario.ExpectedWithoutInfrastructureCleanup != null)
            {
                scenario.ExpectedWithoutInfrastructureCleanup =
                    ExpandCompletionPair(scenario.ExpectedWithoutInfrastructureCleanup);
            }
            if (scenario.ExpectedWithoutNormalization != null)
            {
                scenario.ExpectedWithoutNormalization =
                    ExpandCompletionPair(scenario.ExpectedWithoutNormalization);
            }
            scenario.VerifySampleWeights = true;
        }

        private static Frame[] InsertEntryHelper(Frame[] frames, Frame helper)
        {
            int resume = Array.FindLastIndex(frames, frame =>
                frame.SymbolName == "System.Runtime.CompilerServices.AsyncProfiler+ResumeAsyncContext.Resume");
            Assert.True(resume >= 0);
            return frames.Take(resume + 1).Concat(new[] { helper }).Concat(frames.Skip(resume + 1)).ToArray();
        }

        private static Frame[] ExpandCompletionPair(Frame[] frames) =>
            frames.SelectMany(frame =>
                frame.Label == "Task.RunContinuations"
                    ? new[]
                    {
                        frame,
                        Frame.CoreLib(AsyncProfilerExtendedCompletionRuntimeSupport.InjectedHelperName, "CompletionHelper"),
                    }
                    : new[]
                    {
                        frame.Label == "Task.TrySetResult"
                            ? Frame.CoreLib(AsyncProfilerExtendedCompletionRuntimeSupport.RenamedCompletionName, "Task.TryComplete")
                            : frame,
                    }).ToArray();

        #endregion
    }
}
