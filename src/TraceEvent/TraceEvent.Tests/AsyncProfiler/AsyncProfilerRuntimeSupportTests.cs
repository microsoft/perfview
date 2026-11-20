// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Diagnostics.Tracing.Computers;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

using Xunit;

using static Microsoft.Diagnostics.Tracing.Computers.AsyncProfilerRuntimeSupport;

namespace TraceEventTests
{
    /// <summary>Checks runtime-support selection and ownership-sensitive frame classification.</summary>
    public class AsyncProfilerRuntimeSupportTests
    {
        [Theory]
        [InlineData("11.0.26.12345", "11.0.0", RuntimeSku.CoreClr, true)]
        [InlineData(null, "11.0.0-preview.1.12345+abcdef", RuntimeSku.CoreClr, true)]
        [InlineData("", "11.0.0-dev Commit Hash: abcdef", RuntimeSku.CoreClr, true)]
        [InlineData("11.0.0.0", null, RuntimeSku.CoreClr, true)]
        [InlineData("11.0", "", RuntimeSku.CoreClr, true)]
        [InlineData(" 11.0.0 ", " 11.0.0+abcdef ", RuntimeSku.CoreClr, true)]
        [InlineData("invalid", "11.0.0", RuntimeSku.CoreClr, true)]
        [InlineData("11.0.0", "invalid", RuntimeSku.CoreClr, true)]
        [InlineData("12.0.26.12345", "12.0.0-preview.1", RuntimeSku.CoreClr, true)]
        [InlineData("13.2.0.0", null, RuntimeSku.CoreClr, true)]
        [InlineData(null, "111.0.0", RuntimeSku.CoreClr, true)]
        [InlineData(null, "12.0.0-preview.1", RuntimeSku.CoreClr, true)]
        [InlineData(null, "11.1.0", RuntimeSku.CoreClr, true)]
        [InlineData(null, null, RuntimeSku.CoreClr, false)]
        [InlineData("", "", RuntimeSku.CoreClr, false)]
        [InlineData("invalid", "not-a-version", RuntimeSku.CoreClr, false)]
        [InlineData("11", null, RuntimeSku.CoreClr, false)]
        [InlineData(null, "11.0garbage", RuntimeSku.CoreClr, false)]
        [InlineData(null, "10.0.0", RuntimeSku.CoreClr, false)]
        [InlineData(null, "8.0.0", RuntimeSku.CoreClr, false)]
        [InlineData("12.0.0.0", "11.0.0", RuntimeSku.CoreClr, false)]
        [InlineData("11.0.0.0", "12.0.0", RuntimeSku.CoreClr, false)]
        [InlineData("11.1.0.0", "11.0.0", RuntimeSku.CoreClr, false)]
        [InlineData("12.0.0.0", "13.0.0", RuntimeSku.CoreClr, false)]
        [InlineData("11.0.0", null, RuntimeSku.None, false)]
        [InlineData("11.0.0", null, RuntimeSku.Mono, false)]
        [InlineData(null, "12.0.0", RuntimeSku.Mono, false)]
        [InlineData("11.0.0", null, RuntimeSku.DesktopClr, false)]
        [InlineData("11.0.0", null, RuntimeSku.CoreClr | RuntimeSku.Mono, false)]
        public void Select_UsesNewestApplicableSupportForCapturedRuntime(
            string fileVersion, string productVersion, RuntimeSku flavor, bool supported)
        {
            AsyncProfilerRuntimeSupport support = AsyncProfilerRuntimeSupport.Select(fileVersion, productVersion, flavor);
            Assert.Same(supported ? AsyncProfilerRuntimeSupport.CoreClr11 : AsyncProfilerRuntimeSupport.Common, support);
            Assert.Equal(supported ? "CoreCLR 11.0" : "Common", support.Name);
        }

        [Theory]
        [InlineData("App!Scenario+<Run>d__1.MoveNext()", false, AsyncProfilerStitchSyncFrameKind.V1StateMachineBody)]
        [InlineData("System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start(!!0&)", true, RuntimeFrameKind.V1Startup)]
        [InlineData("System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start(!!0&)", false, AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData("Other.CoreLib!System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start()", true, AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData("System.Threading.Tasks.Task.RunContinuations()", true, RuntimeFrameKind.TaskContinuations)]
        [InlineData("System.Threading.Tasks.Task.RunContinuations()", false, AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData("Other.Namespace.Task.RunContinuations()", true, AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData("System.Runtime.CompilerServices.AsyncProfiler+ResumeAsyncContext.Resume()", true, RuntimeFrameKind.ProfilerResume)]
        [InlineData("System.Runtime.CompilerServices.AsyncProfiler+ResumeAsyncContext.Resume()", false, AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData("System.Runtime.CompilerServices.ValueTaskAwaiter.System.Runtime.CompilerServices.IStateMachineBoxAwareAwaiter.AwaitUnsafeOnCompleted(class System.Runtime.CompilerServices.IAsyncStateMachineBox)", true, RuntimeFrameKind.V1AwaiterRegistration)]
        [InlineData("System.Runtime.CompilerServices.ValueTaskAwaiter`1[System.Int64].System.Runtime.CompilerServices.IStateMachineBoxAwareAwaiter.AwaitUnsafeOnCompleted(class System.Runtime.CompilerServices.IAsyncStateMachineBox)", true, RuntimeFrameKind.V1AwaiterRegistration)]
        [InlineData("System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable+ConfiguredValueTaskAwaiter.System.Runtime.CompilerServices.IStateMachineBoxAwareAwaiter.AwaitUnsafeOnCompleted(class System.Runtime.CompilerServices.IAsyncStateMachineBox)", true, RuntimeFrameKind.V1AwaiterRegistration)]
        [InlineData("System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable`1[System.Int64]+ConfiguredValueTaskAwaiter.System.Runtime.CompilerServices.IStateMachineBoxAwareAwaiter.AwaitUnsafeOnCompleted(class System.Runtime.CompilerServices.IAsyncStateMachineBox)", true, RuntimeFrameKind.V1AwaiterRegistration)]
        [InlineData("System.Runtime.CompilerServices.ValueTaskAwaiter.System.Runtime.CompilerServices.IStateMachineBoxAwareAwaiter.AwaitUnsafeOnCompleted()", false, AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData("Other.Namespace.ValueTaskAwaiter.System.Runtime.CompilerServices.IStateMachineBoxAwareAwaiter.AwaitUnsafeOnCompleted()", true, AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData("System.Runtime.CompilerServices.OtherAwaiter.System.Runtime.CompilerServices.IStateMachineBoxAwareAwaiter.AwaitUnsafeOnCompleted()", true, AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData("System.Runtime.CompilerServices.ValueTaskAwaiter.Other.Namespace.IStateMachineBoxAwareAwaiter.AwaitUnsafeOnCompleted()", true, AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData("System.Runtime.CompilerServices.AsyncProfiler+Other.Resume()", true, AsyncProfilerStitchSyncFrameKind.ProfilerHelper)]
        [InlineData(null, true, AsyncProfilerStitchSyncFrameKind.None)]
        public void InitialSupports_ShareExistingEvidenceAndOwnershipChecks(
            string method, bool isHostModule, object expectedValue)
        {
            FrameClassification expected = Classification(expectedValue);
            foreach (AsyncProfilerRuntimeSupport support in InitialSupport())
            {
                Assert.Equal(expected, support.ClassifySynchronousFrame(method, isHostModule));
            }
        }

        [Theory]
        [InlineData("System.Boolean System.Threading.Tasks.Task::TrySetResult()",
            RuntimeFrameKind.TaskResult)]
        [InlineData("System.Boolean System.Threading.Tasks.Task`1[System.Int64]::TrySetResult(!0)",
            RuntimeFrameKind.TaskResult)]
        [InlineData("System.Void System.Threading.Tasks.Task::RunContinuations(System.Object)",
            RuntimeFrameKind.TaskContinuations)]
        [InlineData("System.Threading.ThreadPoolWorkQueue+DispatchResult System.Threading.ThreadPoolWorkQueue::Dispatch()",
            RuntimeFrameKind.SchedulerEntry)]
        [InlineData("System.Private.CoreLib!System.Boolean System.Threading.Tasks.Task::TrySetResult()",
            RuntimeFrameKind.TaskResult)]
        [InlineData("System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter::QueueContinuation(System.Action,System.Boolean)",
            RuntimeFrameKind.V1AwaiterRegistration)]
        [InlineData("System.Private.CoreLib!System.Void System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter::QueueContinuation(System.Action,System.Boolean)",
            RuntimeFrameKind.V1AwaiterRegistration)]
        [InlineData("Other.Namespace.YieldAwaitable+YieldAwaiter::QueueContinuation()", AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData("System.Runtime.CompilerServices.OtherAwaitable+YieldAwaiter::QueueContinuation()", AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData("System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter::QueueContinuationExtra()", AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData("Other.CoreLib!System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter::QueueContinuation()", AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData("System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter::OutputCorrelationEtwEvent(System.Action)",
            RuntimeFrameKind.V1AwaiterRegistration)]
        [InlineData("System.Private.CoreLib!System.Action System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter::OutputCorrelationEtwEvent(System.Action)",
            RuntimeFrameKind.V1AwaiterRegistration)]
        [InlineData("Other.Namespace.YieldAwaitable+YieldAwaiter::OutputCorrelationEtwEvent()", AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData("System.Runtime.CompilerServices.OtherAwaitable+YieldAwaiter::OutputCorrelationEtwEvent()", AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData("System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter::OutputCorrelationEtwEventExtra()", AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData("System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter+<>c::<OutputCorrelationEtwEvent>b__6_0(System.Action,System.Threading.Tasks.Task)",
            AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData("Other.CoreLib!System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter::OutputCorrelationEtwEvent()", AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData("System.Boolean System.Threading.ThreadPool::UnsafeQueueUserWorkItem(System.Threading.WaitCallback,System.Object)",
            AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData("System.Threading.LowLevelLifoSemaphore::WakeOneCore()", AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData("System.Boolean Other.Namespace.Task::TrySetResult()", AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData("System.Threading.Tasks.Task Other.Namespace.Task::TrySetResult()", AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData("System.Boolean System.Threading.Tasks.Task+Other::TrySetResult()", AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData("System.Boolean System.Threading.Tasks.Task`2[System.Int32,System.Int64]::TrySetResult()", AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData("System.Boolean System.Threading.Tasks.Task`1[System.Int64::TrySetResult()", AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData("Other.CoreLib!System.Boolean System.Threading.Tasks.Task::TrySetResult()", AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData("System.Boolean System.Threading.Tasks.Task::TrySetResultExtra()", AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData("System.Threading.ThreadPoolWorkQueue+DispatchResult Other.Namespace.ThreadPoolWorkQueue::Dispatch()", AsyncProfilerStitchSyncFrameKind.None)]
        public void PdbNames_PreserveExactDeclaringTypeAndModuleOwnershipChecks(string name, object expected)
        {
            string normalized = AsyncProfilerMethodNames.NormalizeMethodName(name);
            foreach (AsyncProfilerRuntimeSupport support in InitialSupport())
            {
                Assert.Equal(Classification(expected), support.ClassifySynchronousFrame(normalized, true));
                Assert.Equal(AsyncProfilerStitchSyncFrameKind.None, support.ClassifySynchronousFrame(normalized, false));
            }
        }

        [Theory]
        [InlineData("dynamicClass.IL_STUB_AsyncResume(class System.Object,unsigned int8&)", true)]
        [InlineData("dynamicClass.IL_STUB_AsyncResume", true)]
        [InlineData("Other.dynamicClass.IL_STUB_AsyncResume()", false)]
        [InlineData("Other+dynamicClass.IL_STUB_AsyncResume()", false)]
        [InlineData("dynamicClass.IL_STUB_AsyncResumeOther()", false)]
        [InlineData("Other.IL_STUB_AsyncResume()", false)]
        [InlineData("IL_STUB_AsyncResume()", false)]
        public void ResumeStub_IsSupportSpecificApplicationInfrastructureNotABoundary(string name, bool recognized)
        {
            foreach (AsyncProfilerRuntimeSupport support in InitialSupport())
            {
                foreach (bool hostModule in new[] { false, true })
                {
                    Assert.Equal(recognized ? AsyncProfilerStitchSyncFrameKind.V2ResumeStub : AsyncProfilerStitchSyncFrameKind.None,
                        support.ClassifySynchronousFrame(name, hostModule));
                }
                Assert.Equal(AsyncProfilerCallStackStitchBoundaryKind.None, support.ClassifyBoundary(name).Kind);
            }
        }

        [Theory]
        [InlineData(@"C:\runtime\coreclr.dll", @"C:\runtime\System.Private.CoreLib.dll", true)]
        [InlineData(@"C:\RUNTIME\CLRJIT.DLL", @"c:\runtime\System.Private.CoreLib.dll", true)]
        [InlineData("/runtime/libcoreclr.so", "/runtime/System.Private.CoreLib.dll", true)]
        [InlineData("/runtime/libclrjit.so", "/runtime/System.Private.CoreLib.dll", true)]
        [InlineData("/runtime/libcoreclr.dylib", "/runtime/System.Private.CoreLib.dll", true)]
        [InlineData("/runtime/libclrjit.dylib", "/runtime/System.Private.CoreLib.dll", true)]
        [InlineData(@"C:\other\coreclr.dll", @"C:\runtime\System.Private.CoreLib.dll", false)]
        [InlineData(@"C:\runtime\coreclr.extra.dll", @"C:\runtime\System.Private.CoreLib.dll", false)]
        [InlineData("clrjit.dll", "System.Private.CoreLib.dll", false)]
        public void NativeRuntimeModules_RequireCapturedRuntimeImagesInTheHostDirectory(
            string path, string hostPath, bool recognized)
        {
            var module = new TraceModuleFile(path, 0x1000, (ModuleFileIndex)0);
            var host = new TraceModuleFile(hostPath, 0x2000, (ModuleFileIndex)1);
            foreach (AsyncProfilerRuntimeSupport support in InitialSupport())
            {
                Assert.Equal(recognized, support.IsNativeRuntimeModule(module, host));
            }
        }

        [Theory]
        [InlineData("Continuation_Wrapper_31", (int)AsyncProfilerCallStackStitchBoundaryKind.V2Dispatcher, 31)]
        [InlineData("Continuation_Wrapper_32", (int)AsyncProfilerCallStackStitchBoundaryKind.None, -1)]
        [InlineData("RuntimeAsyncTask.DispatchContinuations()", (int)AsyncProfilerCallStackStitchBoundaryKind.V2DispatcherInfrastructure, -1)]
        [InlineData("RuntimeAsyncTask.InstrumentedDispatchContinuations()", (int)AsyncProfilerCallStackStitchBoundaryKind.V2DispatcherInfrastructure, -1)]
        [InlineData("Box.MoveNextAsDispatcher()", (int)AsyncProfilerCallStackStitchBoundaryKind.V1Dispatcher, -1)]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineDispatcher.MoveNext()", (int)AsyncProfilerCallStackStitchBoundaryKind.V1Dispatcher, -1)]
        [InlineData(null, (int)AsyncProfilerCallStackStitchBoundaryKind.None, -1)]
        public void BoundaryContracts_AreSharedEvenWhenASupportDisablesDispatcherInfrastructure(
            string method, int expectedValue, int expectedWrapperIndex)
        {
            var expected = (AsyncProfilerCallStackStitchBoundaryKind)expectedValue;
            var supports = new[]
            {
                AsyncProfilerRuntimeSupport.Common,
                AsyncProfilerRuntimeSupport.CoreClr11,
                new NoDispatcherInfrastructureSupport(),
            };
            foreach (AsyncProfilerRuntimeSupport support in supports)
            {
                AsyncProfilerCallStackStitchBoundaryInfo info = support.ClassifyBoundary(method);
                Assert.Equal(expected, info.Kind);
                Assert.Equal(expectedWrapperIndex, info.WrapperIndex);
            }
        }

        [Theory]
        [InlineData("System.Private.CoreLib!System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncStateMachineBox`1.MoveNext()")]
        [InlineData("System.Private.CoreLib!System.Runtime.CompilerServices.AsyncTaskMethodBuilder+AsyncProfilerAsyncStateMachineBox`1.InstrumentedMoveNext()")]
        [InlineData("System.Private.CoreLib!System.Runtime.CompilerServices.AsyncTaskMethodBuilder+ProfilerAsyncStateMachineBox`1.ExecuteDirectly()")]
        [InlineData("System.Private.CoreLib!System.Runtime.CompilerServices.AsyncTaskMethodBuilder+DebugFinalizableAsyncStateMachineBox`1.MoveNext()")]
        [InlineData("System.Private.CoreLib!System.Runtime.CompilerServices.AsyncStateMachineDispatcher.ExecuteDirectly()")]
        [InlineData("System.Private.CoreLib!System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncStateMachineBox`1[System.Int64,V1TraceScenarios+<BurnCpuAsync>d__15].ExecuteDirectly(class System.Threading.Thread)")]
        [InlineData("System.Private.CoreLib!System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncStateMachineBox`1[System.Int64,V1TraceScenarios+<BurnCpuAsync>d__15].MoveNext(class System.Threading.Thread)")]
        [InlineData("System.Private.CoreLib!System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncProfilerAsyncStateMachineBox`1[System.Int64,V1TraceScenarios+<BurnCpuAsync>d__15].InstrumentedMoveNext(class System.Threading.Thread,value class Flags)")]
        public void ClassifyBoundary_RecognizesV1DispatcherInfrastructure(string frameName)
        {
            AsyncProfilerCallStackStitchBoundaryInfo info = AsyncProfilerRuntimeSupport.Common.ClassifyBoundary(frameName);
            Assert.Equal(AsyncProfilerCallStackStitchBoundaryKind.V1DispatcherInfrastructure, info.Kind);
            Assert.Equal(-1, info.WrapperIndex);
        }

        [Fact]
        public void BoxRecognition_IsImplementationDependentRatherThanABoundaryContract()
        {
            const string box =
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+" +
                "AsyncProfilerAsyncStateMachineBox`1[System.Int64,Scenario+<Run>d__1].MoveNext()";
            Assert.Equal(AsyncProfilerCallStackStitchBoundaryKind.None, AsyncProfilerCallStackStitchBoundary.ClassifyContractBoundary(box, out _));
            Assert.Equal(
                AsyncProfilerCallStackStitchBoundaryKind.None,
                new NoDispatcherInfrastructureSupport().ClassifyBoundary(box).Kind);

            foreach (AsyncProfilerRuntimeSupport support in InitialSupport())
            {
                Assert.Equal(AsyncProfilerCallStackStitchBoundaryKind.V1DispatcherInfrastructure, support.ClassifyBoundary(box).Kind);
                Assert.True(support.TryGetBoxedStateMachineTypeName(box, true, out string stateMachineType));
                Assert.Equal("Scenario+<Run>d__1", stateMachineType);
                Assert.False(support.TryGetBoxedStateMachineTypeName(box, false, out _));
            }
        }

        [Theory]
        [InlineData("AsyncStateMachineBox`1[App+<Run>d__1].MoveNext()", "App+<Run>d__1")]
        [InlineData("AsyncProfilerAsyncStateMachineBox`1[System.Int64,App+<Run>d__1].InstrumentedMoveNext(class System.Threading.Thread)", "App+<Run>d__1")]
        [InlineData("ProfilerAsyncStateMachineBox`1[System.Collections.Generic.List`1[System.Int64],App`1+<Run>d__1`1[System.Int32,System.String]].MoveNext()", "App`1+<Run>d__1`1[System.Int32,System.String]")]
        [InlineData("DebugFinalizableAsyncStateMachineBox`1[System.Int64, App+<Run>d__1].MoveNext()", "App+<Run>d__1")]
        public void BoxCarrier_ExtractsTheFinalConstructedTypeArgument(string boxMethod, string expected)
        {
            string name = "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+" + boxMethod;
            foreach (AsyncProfilerRuntimeSupport support in InitialSupport())
            {
                Assert.True(support.TryGetBoxedStateMachineTypeName(name, true, out string type));
                Assert.Equal(expected, type);
            }
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData("", true)]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineBox`1[App].MoveNext()", false)]
        [InlineData("Other.Namespace.AsyncStateMachineBox`1[App].MoveNext()", true)]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineBox`1[App].MoveNextAsDispatcher()", true)]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineBox`1[App].UnknownMoveNext()", true)]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineDispatcher.MoveNext()", true)]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineBox`1.MoveNext()", true)]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineBox`10[App].MoveNext()", true)]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineBox`1[].MoveNext()", true)]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineBox`1[App,].MoveNext()", true)]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineBox`1[,App].MoveNext()", true)]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineBox`1[App`1[System.Int32].MoveNext()", true)]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineBox`1[App]].MoveNext()", true)]
        public void BoxCarrier_RejectsUnknownUnconstructedAndMalformedNames(string name, bool isHostModule)
        {
            foreach (AsyncProfilerRuntimeSupport support in InitialSupport())
            {
                Assert.False(support.TryGetBoxedStateMachineTypeName(name, isHostModule, out _));
            }
        }

        [Fact]
        public void BoundaryInfrastructureOverride_DoesNotChangeInheritedCarrierAndHelperRules()
        {
            const string boxType =
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+" +
                "AsyncProfilerAsyncStateMachineBox`1[System.Int64,Scenario+<Run>d__1]";
            var support = new NoDispatcherInfrastructureSupport();

            Assert.Equal(AsyncProfilerCallStackStitchBoundaryKind.None, support.ClassifyBoundary(boxType + ".MoveNext()").Kind);
            Assert.True(support.TryGetBoxedStateMachineTypeName(boxType + ".MoveNext()", true, out string type));
            Assert.Equal("Scenario+<Run>d__1", type);
            Assert.Equal(
                AsyncProfilerStitchSyncFrameKind.ProfilerHelper,
                support.ClassifySynchronousFrame(boxType + ".GetDiagnosticData()", true));
            Assert.Equal(
                AsyncProfilerStitchSyncFrameKind.None,
                support.ClassifySynchronousFrame(boxType + ".GetDiagnosticData()", false));
        }

        [Theory]
        [InlineData("System.Runtime.CompilerServices.AsyncProfiler..cctor()", true)]
        [InlineData("System.Runtime.CompilerServices.AsyncProfiler+OtherHelpers.Initialize()", true)]
        [InlineData("System.Runtime.CompilerServices.AsyncProfilerEventSource..ctor()", true)]
        [InlineData("System.Runtime.CompilerServices.TaskAwaiter.GetResult()", true)]
        [InlineData("System.Runtime.CompilerServices.TaskAwaiter`1[System.Int64].GetResult()", true)]
        [InlineData("System.Runtime.CompilerServices.TaskAwaiter+<>c.<OutputWaitEtwEvents>b__12_0(System.Action,System.Threading.Tasks.Task)", true)]
        [InlineData("System.Runtime.CompilerServices.TaskAwaiter+OtherHelper.Invoke()", true)]
        [InlineData("System.Runtime.CompilerServices.TaskAwaiterLookalike.GetResult()", false)]
        [InlineData("System.Runtime.CompilerServices.TaskAwaiter.OtherType.Invoke()", false)]
        [InlineData("System.Runtime.CompilerServices.TaskAwaiter`10[System.Int64].GetResult()", false)]
        [InlineData("Other.Namespace.TaskAwaiter.GetResult()", false)]
        [InlineData("Other.CoreLib!System.Runtime.CompilerServices.TaskAwaiter.GetResult()", false)]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineDiagnostics`1[App+<Run>d__1].ResolveMethodId()", true)]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineDiagnostics`1[App+<Run>d__1].GetState(!!0&)", true)]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineDiagnostics`1[App+<Run>d__1]..cctor()", true)]
        [InlineData("System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncStateMachineBox`1[System.Int64,App+<Run>d__1]..ctor()", true)]
        [InlineData("System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncProfilerAsyncStateMachineBox`1[System.Int64,App+<Run>d__1].ClearStateUponCompletion()", true)]
        [InlineData("System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+DebugFinalizableAsyncStateMachineBox`1[System.Int64,App+<Run>d__1].System.Runtime.CompilerServices.IAsyncStateMachineBox.GetDiagnosticData()", true)]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineDiagnostics`10[App].ResolveMethodId()", false)]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineDiagnostics`1[App]+Other.Initialize()", false)]
        [InlineData("System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBaseSlow()", false)]
        [InlineData("System.Runtime.CompilerServices.InitHelpers.CallClassConstructor()", false)]
        [InlineData("Other.Namespace.AsyncStateMachineDiagnostics`1[App].ResolveMethodId()", false)]
        [InlineData("Other.Namespace.AsyncStateMachineBox`1[App].GetDiagnosticData()", false)]
        [InlineData("App+<Run>d__1.MoveNext()", false)]
        public void DispatchSpanHelpers_RecognizeTypeFamiliesButNotGenericInitialization(string name, bool expected)
        {
            foreach (AsyncProfilerRuntimeSupport support in InitialSupport())
            {
                FrameClassification kind = support.ClassifySynchronousFrame(name, true);
                Assert.Equal(expected, support.IsProfilerDispatchInfrastructure(kind));
                Assert.False(support.IsProfilerDispatchInfrastructure(support.ClassifySynchronousFrame(name, false)));
            }
        }

        [Theory]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineDispatcher.System.Runtime.CompilerServices.IAsyncStateMachineDispatcher.get_DispatcherId()", true)]
        [InlineData("System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncProfilerAsyncStateMachineBox`1[System.Int64,App+<Run>d__1].System.Runtime.CompilerServices.IAsyncStateMachineDispatcher.get_DispatcherId()", true)]
        [InlineData("System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncProfilerAsyncStateMachineBox`1[System.Int64,App+<Run>d__1].System.Runtime.CompilerServices.IAsyncStateMachineDispatcher.get_IsLeaf()", true)]
        [InlineData("System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncProfilerAsyncStateMachineBox`1[System.Int64,App+<Run>d__1].Other.Interface.FutureMember()", true)]
        [InlineData("System.Private.CoreLib!System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncProfilerAsyncStateMachineBox`1[System.Int64,App+<Run>d__1].System.IDisposable.Dispose()", true)]
        [InlineData("Other.Namespace.AsyncStateMachineDispatcher.System.Runtime.CompilerServices.IAsyncStateMachineDispatcher.get_DispatcherId()", false)]
        [InlineData("System.Runtime.CompilerServices.OrdinaryType.System.Runtime.CompilerServices.IAsyncStateMachineDispatcher.get_DispatcherId()", false)]
        [InlineData("System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncProfilerAsyncStateMachineBoxLookalike`1[System.Int64,App+<Run>d__1].System.Runtime.CompilerServices.IAsyncStateMachineDispatcher.get_DispatcherId()", false)]
        [InlineData("System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncProfilerAsyncStateMachineBox`1[System.Int64,App+<Run>d__1.System.Runtime.CompilerServices.IAsyncStateMachineDispatcher.get_DispatcherId()", false)]
        public void ProfilerImplementingTypes_RecognizeInterfaceMembersWithoutMethodLists(string name, bool expected)
        {
            foreach (AsyncProfilerRuntimeSupport support in InitialSupport())
            {
                Assert.Equal(expected, support.IsProfilerDispatchInfrastructure(
                    support.ClassifySynchronousFrame(name, true)));
                Assert.Equal(AsyncProfilerStitchSyncFrameKind.None, support.ClassifySynchronousFrame(name, false));
                Assert.Equal(AsyncProfilerCallStackStitchBoundaryKind.None, support.ClassifyBoundary(name).Kind);
            }
        }

        [Theory]
        [InlineData("System.Threading.Tasks.Task.TrySetException(System.Object)", true)]
        [InlineData("System.Boolean System.Threading.Tasks.Task::TrySetException(System.Object)", true)]
        [InlineData("System.Threading.Tasks.Task`1[System.Int64].TrySetException(System.Object)", true)]
        [InlineData("System.Threading.Tasks.Task.AddException(System.Object,System.Boolean)", true)]
        [InlineData("System.Threading.Tasks.TaskExceptionHolder.AddFaultException(System.Object)", true)]
        [InlineData("System.Threading.Tasks.Task.TrySetExceptionExtra(System.Object)", false)]
        [InlineData("System.Threading.Tasks.Task+Other.TrySetException(System.Object)", false)]
        [InlineData("System.Threading.Tasks.Task`10[System.Int64].TrySetException(System.Object)", false)]
        [InlineData("System.Threading.Tasks.TaskExceptionHolderLookalike.AddFaultException(System.Object)", false)]
        [InlineData("Other.Namespace.Task.AddException(System.Object)", false)]
        [InlineData("Other.CoreLib!System.Threading.Tasks.Task.TrySetException(System.Object)", false)]
        public void TaskExceptionInfrastructure_IsClassifiedButNotIndividuallyCleaned(string name, bool expected)
        {
            foreach (AsyncProfilerRuntimeSupport support in InitialSupport())
            {
                string normalized = AsyncProfilerMethodNames.NormalizeMethodName(name);
                FrameClassification kind = support.ClassifySynchronousFrame(normalized, true);
                Assert.Equal(expected, kind == AsyncProfilerStitchSyncFrameKind.FaultHandling);
                Assert.Equal(expected, support.IsProfilerDispatchInfrastructure(kind));
                Assert.Equal(AsyncProfilerStitchSyncFrameKind.None, support.ClassifySynchronousFrame(normalized, false));
                var frame = AsyncProfilerStitchedFrame.CreateSync(
                    new AsyncProfilerStitchSyncFrame(CodeAddressIndex.Invalid, MethodIndex.Invalid, kind), null);
                Assert.False(support.IsOptionalInfrastructureFrame(frame, AsyncProfilerCallStackStitchBoundaryKind.None));
                Assert.False(support.IsV1CompletionUnwindInfrastructure(frame, AsyncProfilerCallStackStitchBoundaryKind.None));
            }
        }

        [Theory]
        [InlineData("System.Runtime.CompilerServices.TaskAwaiter.GetResult()", true)]
        [InlineData("System.Runtime.CompilerServices.TaskAwaiter`1[System.Int64].GetResult()", true)]
        [InlineData("System.Runtime.CompilerServices.TaskAwaiter+<>c.<OutputWaitEtwEvents>b__12_0(System.Action,System.Threading.Tasks.Task)", true)]
        [InlineData("System.Runtime.CompilerServices.TaskAwaiterLookalike.GetResult()", false)]
        [InlineData("System.Runtime.CompilerServices.TaskAwaiter.OtherType.Invoke()", false)]
        [InlineData("System.Runtime.CompilerServices.TaskAwaiter`10[System.Int64].GetResult()", false)]
        [InlineData("Other.Namespace.TaskAwaiter.GetResult()", false)]
        [InlineData("Other.CoreLib!System.Runtime.CompilerServices.TaskAwaiter.GetResult()", false)]
        public void AwaiterInfrastructure_IsClassifiedWithoutAuthorizingIndividualCleanup(string name, bool expected)
        {
            foreach (AsyncProfilerRuntimeSupport support in InitialSupport())
            {
                FrameClassification kind = support.ClassifySynchronousFrame(name, true);
                Assert.Equal(expected, kind == AsyncProfilerStitchSyncFrameKind.AwaiterHelper);
                Assert.Equal(AsyncProfilerStitchSyncFrameKind.None, support.ClassifySynchronousFrame(name, false));
                var frame = AsyncProfilerStitchedFrame.CreateSync(
                    new AsyncProfilerStitchSyncFrame(CodeAddressIndex.Invalid, MethodIndex.Invalid, kind), null);
                Assert.False(support.IsOptionalInfrastructureFrame(frame, AsyncProfilerCallStackStitchBoundaryKind.None));
                Assert.Equal(expected, support.IsV1CompletionUnwindInfrastructure(frame, AsyncProfilerCallStackStitchBoundaryKind.None));
            }
        }

        [Theory]
        [InlineData("System.Runtime.CompilerServices.TaskAwaiter.get_IsCompleted()")]
        [InlineData("System.Runtime.CompilerServices.TaskAwaiter`1[System.Int64].get_IsCompleted()")]
        [InlineData("System.Runtime.CompilerServices.ValueTaskAwaiter.get_IsCompleted()")]
        [InlineData("System.Runtime.CompilerServices.ValueTaskAwaiter`1[System.Int64].get_IsCompleted()")]
        [InlineData("System.Runtime.CompilerServices.ConfiguredTaskAwaitable+ConfiguredTaskAwaiter.get_IsCompleted()")]
        [InlineData("System.Runtime.CompilerServices.ConfiguredTaskAwaitable`1+ConfiguredTaskAwaiter[System.Int64].get_IsCompleted()")]
        [InlineData("System.Runtime.CompilerServices.ConfiguredTaskAwaitable`1[System.Int64]+ConfiguredTaskAwaiter.get_IsCompleted()")]
        [InlineData("System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable+ConfiguredValueTaskAwaiter.get_IsCompleted()")]
        [InlineData("System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable`1+ConfiguredValueTaskAwaiter[System.Int64].get_IsCompleted()")]
        [InlineData("System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable`1[System.Int64]+ConfiguredValueTaskAwaiter.get_IsCompleted()")]
        public void AwaiterReadinessCheck_IsOptionalMachineryNotDispatchOrCompletionEvidence(string name)
        {
            foreach (AsyncProfilerRuntimeSupport support in InitialSupport())
            {
                FrameClassification kind = support.ClassifySynchronousFrame(name, true);
                Assert.Equal(RuntimeFrameKind.V1AwaiterReadinessCheck, kind);
                Assert.Equal(AsyncProfilerStitchSyncFrameKind.None, support.ClassifySynchronousFrame(name, false));
                Assert.False(support.ClassifyBoundary(name).IsDispatchMarker);
                var frame = AsyncProfilerStitchedFrame.CreateSync(
                    new AsyncProfilerStitchSyncFrame(CodeAddressIndex.Invalid, MethodIndex.Invalid, kind), null);
                Assert.True(support.IsOptionalInfrastructureFrame(frame, AsyncProfilerCallStackStitchBoundaryKind.None));
                Assert.False(support.IsProfilerDispatchInfrastructure(kind));
                Assert.False(support.IsV1CompletionUnwindInfrastructure(frame, AsyncProfilerCallStackStitchBoundaryKind.None));
            }
        }

        [Theory]
        [InlineData("Other.Namespace.ValueTaskAwaiter.get_IsCompleted()")]
        [InlineData("Other.CoreLib!System.Runtime.CompilerServices.ValueTaskAwaiter.get_IsCompleted()")]
        [InlineData("System.Runtime.CompilerServices.ValueTaskAwaiter+Other.get_IsCompleted()")]
        [InlineData("System.Runtime.CompilerServices.ValueTaskAwaiter`10[System.Int64].get_IsCompleted()")]
        [InlineData("System.Runtime.CompilerServices.ValueTaskAwaiter.get_IsCompletedExtra()")]
        [InlineData("System.Runtime.CompilerServices.ValueTaskAwaiter.GetResult()")]
        [InlineData("Other.Namespace.TaskAwaiter.get_IsCompleted()")]
        [InlineData("System.Runtime.CompilerServices.TaskAwaiter+Other.get_IsCompleted()")]
        [InlineData("System.Runtime.CompilerServices.OtherAwaitable+ConfiguredTaskAwaiter.get_IsCompleted()")]
        [InlineData("System.Runtime.CompilerServices.ConfiguredTaskAwaitable+Other+ConfiguredTaskAwaiter.get_IsCompleted()")]
        [InlineData("System.Runtime.CompilerServices.ConfiguredTaskAwaitable`10[System.Int64]+ConfiguredTaskAwaiter.get_IsCompleted()")]
        [InlineData("System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable`1[System.Int64+ConfiguredValueTaskAwaiter.get_IsCompleted()")]
        [InlineData("System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable+ConfiguredValueTaskAwaiter.GetResult()")]
        [InlineData("System.Threading.Tasks.Sources.ManualResetValueTaskSourceCore`1[System.Int64].GetStatus(int16)")]
        [InlineData("Scenario.PooledSource.GetStatus(int16)")]
        public void AwaiterReadinessCheck_DoesNotBroadenOtherAwaiterCleanup(string name)
        {
            foreach (AsyncProfilerRuntimeSupport support in InitialSupport())
            {
                FrameClassification kind = support.ClassifySynchronousFrame(name, true);
                Assert.NotEqual(RuntimeFrameKind.V1AwaiterReadinessCheck, kind);
                var frame = AsyncProfilerStitchedFrame.CreateSync(
                    new AsyncProfilerStitchSyncFrame(CodeAddressIndex.Invalid, MethodIndex.Invalid, kind), null);
                Assert.False(support.IsOptionalInfrastructureFrame(frame, AsyncProfilerCallStackStitchBoundaryKind.None));
            }
        }

        [Theory]
        [InlineData("System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder.SetResult()", true)]
        [InlineData("System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder`1[System.Int64].SetResult(!0)", true)]
        [InlineData("System.Private.CoreLib!System.Void System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder`1[System.Int64]::SetResult(System.Int64)", true)]
        [InlineData("System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder.SetException(System.Exception)", false)]
        [InlineData("System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder.SetResultExtra()", false)]
        [InlineData("System.Runtime.CompilerServices.AsyncValueTaskMethodBuilderLookalike.SetResult()", false)]
        [InlineData("System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder`10[System.Int64].SetResult(!0)", false)]
        [InlineData("System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder`1[System.Int64.SetResult(!0)", false)]
        [InlineData("System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder+Other.SetResult()", false)]
        [InlineData("Other.Namespace.AsyncValueTaskMethodBuilder.SetResult()", false)]
        [InlineData("Other.CoreLib!System.Runtime.CompilerServices.AsyncValueTaskMethodBuilder.SetResult()", false)]
        public void ValueTaskCompletionWrapper_RequiresExactBuilderAndNormalCompletion(string name, bool expected)
        {
            string normalized = AsyncProfilerMethodNames.NormalizeMethodName(name);
            foreach (AsyncProfilerRuntimeSupport support in InitialSupport())
            {
                Assert.Equal(expected, support.ClassifySynchronousFrame(normalized, true) ==
                    AsyncProfilerStitchSyncFrameKind.V1MethodCompletion);
                Assert.Equal(AsyncProfilerStitchSyncFrameKind.None, support.ClassifySynchronousFrame(normalized, false));
            }
        }

        [Theory]
        [InlineData(
            "AsyncProfilerScenarios!V1TraceScenarios+<BurnCpuAsync>d__15.MoveNext()",
            AsyncProfilerStitchSyncFrameKind.V1StateMachineBody)]
        [InlineData(
            "Namespace.Type+<>c+<<Run>b__0_0>d.MoveNext()",
            AsyncProfilerStitchSyncFrameKind.V1StateMachineBody)]
        [InlineData(
            "Namespace.Type+<<Run>g__Inner|1_0>d.MoveNext()",
            AsyncProfilerStitchSyncFrameKind.V1StateMachineBody)]
        [InlineData(
            "Namespace.Type`1+<<Run>b__0_0>d[MyApp.MoveNextPayload].MoveNext()",
            AsyncProfilerStitchSyncFrameKind.V1StateMachineBody)]
        [InlineData(
            "Namespace.Type+<Run>d_4.MoveNext()",
            AsyncProfilerStitchSyncFrameKind.V1StateMachineBody)]
        [InlineData(
            "Namespace.Type+<Run>display.MoveNext()",
            AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData(
            "System.Private.CoreLib!System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].Start(!!0&)",
            RuntimeFrameKind.V1Startup)]
        [InlineData(
            "System.Private.CoreLib!System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].AwaitUnsafeOnCompleted(!!0&,!!1&)",
            RuntimeFrameKind.V1BuilderAwaitRegistration)]
        [InlineData(
            "System.Private.CoreLib!System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].AwaitUnsafeOnCompleted(!!0&,!!1&,class System.Threading.Tasks.Task`1<!0>&)",
            RuntimeFrameKind.V1BuilderAwaitRegistration)]
        [InlineData(
            "System.Private.CoreLib!System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].AwaitUnsafeOnCompleted(!!0&,class System.Runtime.CompilerServices.IAsyncStateMachineBox)",
            RuntimeFrameKind.V1BuilderAwaitRegistration)]
        [InlineData(
            "System.Private.CoreLib!System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start(!!0&)",
            RuntimeFrameKind.V1Startup)]
        [InlineData(
            "System.Private.CoreLib!System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].SetExistingTaskResult(class System.Threading.Tasks.Task`1<!0>,!0)",
            AsyncProfilerStitchSyncFrameKind.V1MethodCompletion)]
        [InlineData(
            "System.Private.CoreLib!System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].GetStateMachineBox(!!0&,class System.Threading.Tasks.Task`1<!0>&)",
            AsyncProfilerStitchSyncFrameKind.V1MethodHelper)]
        [InlineData(
            "System.Private.CoreLib!System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1[System.Int64].SetException(class System.Exception)",
            AsyncProfilerStitchSyncFrameKind.V1MethodHelper)]
        [InlineData(
            "System.Private.CoreLib!System.Runtime.CompilerServices.AsyncIteratorMethodBuilder.Complete()",
            AsyncProfilerStitchSyncFrameKind.V1MethodHelper)]
        [InlineData(
            "System.Private.CoreLib!System.Runtime.CompilerServices.YieldAwaitable+YieldAwaiter.System.Runtime.CompilerServices.IStateMachineBoxAwareAwaiter.AwaitUnsafeOnCompleted(class System.Runtime.CompilerServices.IAsyncStateMachineBox)",
            RuntimeFrameKind.V1AwaiterRegistration)]
        [InlineData(
            "System.Private.CoreLib!System.Runtime.CompilerServices.TaskAwaiter.UnsafeOnCompletedInternal(class System.Threading.Tasks.Task,class System.Action,bool)",
            RuntimeFrameKind.V1AwaiterRegistration)]
        [InlineData(
            "System.Private.CoreLib!System.Runtime.CompilerServices.ConfiguredValueTaskAwaitable+ConfiguredValueTaskAwaiter.UnsafeOnCompleted(class System.Action)",
            RuntimeFrameKind.V1AwaiterRegistration)]
        [InlineData(
            "System.Private.CoreLib!System.Runtime.CompilerServices.TaskAwaiter.GetResult()",
            AsyncProfilerStitchSyncFrameKind.AwaiterHelper)]
        [InlineData(
            "System.Private.CoreLib!System.Threading.Tasks.Task.RunContinuations(class System.Object)",
            RuntimeFrameKind.TaskContinuations)]
        [InlineData(
            "System.Private.CoreLib!System.Threading.ThreadPoolWorkQueue.Dispatch()",
            RuntimeFrameKind.SchedulerEntry)]
        [InlineData(
            "System.Private.CoreLib!System.Threading.Tasks.Task.TrySetResult()",
            RuntimeFrameKind.TaskResult)]
        [InlineData(
            "System.Private.CoreLib!System.Threading.Tasks.Task`1[System.Boolean].TrySetResult(!0)",
            RuntimeFrameKind.TaskResult)]
        [InlineData(
            "System.Private.CoreLib!System.Threading.Tasks.TaskCompletionSource`1[System.Boolean].SetResult(!0)",
            RuntimeFrameKind.TaskCompletionSourceResult)]
        [InlineData(
            "System.Private.CoreLib!System.Threading.Tasks.Task`1[[System.Collections.Generic.List`1[System.Int32]]].TrySetResult(!0)",
            RuntimeFrameKind.TaskResult)]
        [InlineData(
            "System.Private.CoreLib!System.Threading.Tasks.Task`1.TrySetResult(!0)",
            RuntimeFrameKind.TaskResult)]
        [InlineData(
            "System.Private.CoreLib!Other.Namespace.Task.RunContinuations()",
            AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData(
            "System.Private.CoreLib!Other.Namespace.TaskCompletionSource`1[System.Boolean].SetResult(!0)",
            AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData(
            "System.Private.CoreLib!Other.Namespace.ExecutionContext.RunInternal()",
            AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData(
            "System.Private.CoreLib!System.Threading.Tasks.TaskExtra.RunContinuations()",
            AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData(
            "System.Private.CoreLib!System.Threading.Tasks.Task`1[System.Boolean]+Other.TrySetResult(!0)",
            AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData(
            "System.Private.CoreLib!System.Threading.Tasks.Task`2[System.Boolean,System.Int32].TrySetResult(!0)",
            AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData(
            "System.Private.CoreLib!System.Threading.Tasks.TaskCompletionSource`10.SetResult(!0)",
            AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData(
            "System.Private.CoreLib!System.Threading.Tasks.AwaitTaskContinuation`1[System.Boolean].RunOrScheduleAction()",
            AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData(
            "System.Private.CoreLib!System.Threading.ExecutionContext`1.RunInternal()",
            AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData(
            "System.Private.CoreLib!System.Threading.Tasks.Task`1[System.Boolean.TrySetResult(!0)",
            AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData(
            "System.Private.CoreLib!System.Threading.Tasks.AwaitTaskContinuation.RunOrScheduleAction(class System.Action,bool)",
            AsyncProfilerStitchSyncFrameKind.ContinuationBridge)]
        [InlineData(
            "System.Private.CoreLib!System.Runtime.CompilerServices.RuntimeAsyncTaskContinuation+TaskContinuationResume.ResumeTaskContinuation(class System.Runtime.CompilerServices.Continuation,unsigned int8&)",
            AsyncProfilerStitchSyncFrameKind.ContinuationBridge)]
        [InlineData(
            "System.Private.CoreLib!System.Runtime.CompilerServices.RuntimeAsyncTaskContinuation+Other.ResumeTaskContinuation()",
            AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData(
            "System.Private.CoreLib!System.Runtime.CompilerServices.RuntimeAsyncTaskContinuation+TaskContinuationResume.ResumeTaskContinuationExtra()",
            AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData(
            "System.Private.CoreLib!System.Threading.ExecutionContext.RunInternal(class System.Threading.ExecutionContext,class System.Threading.ContextCallback,class System.Object)",
            AsyncProfilerStitchSyncFrameKind.ContinuationBridge)]
        [InlineData(
            "System.Private.CoreLib!System.Threading.ExecutionContext.RunFromThreadPoolDispatchLoop(class System.Threading.Thread,class System.Threading.ExecutionContext,class System.Threading.ContextCallback,class System.Object)",
            AsyncProfilerStitchSyncFrameKind.ContinuationBridge)]
        [InlineData(
            "Other.CoreLib!System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start(!!0&)",
            AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData(
            "System.Private.CoreLib!Other.Namespace.AsyncMethodBuilderCore.Start(!!0&)",
            AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData(
            "MyBuilders!CustomAsyncMethodBuilder.Start(!!0&)",
            AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData(
            "MyBuilders!CustomAsyncMethodBuilder.AwaitUnsafeOnCompleted(!!0&,!!1&)",
            AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData(
            "MyBuilders!CustomAsyncMethodBuilder.SetException(class System.Exception)",
            AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData(
            "MyCoreLib!System.Threading.Tasks.Task.RunContinuations(class System.Object)",
            AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData(
            "MyApp!InlineContinuationAwaiter.UnsafeOnCompleted(class System.Action)",
            AsyncProfilerStitchSyncFrameKind.None)]
        [InlineData(
            "MyApp!MyType.MoveNext()",
            AsyncProfilerStitchSyncFrameKind.None)]
        public void ClassifySynchronousFrame_RecognizesKnownAsyncRelatedFrames(string frameName, object expectedValue)
        {
            Assert.Equal("System.Private.CoreLib", AsyncProfilerRuntimeSupport.HostModuleName);
            FrameClassification expected = Classification(expectedValue);
            bool isHostModule = AsyncProfilerRuntimeSupport.IsHostModuleQualifiedFrame(frameName);
            Assert.Equal(expected, AsyncProfilerRuntimeSupport.Common.ClassifySynchronousFrame(frameName, isHostModule));
        }

        [Theory]
        [InlineData(
            "System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start(!!0&)",
            RuntimeFrameKind.V1Startup)]
        [InlineData(
            "System.Threading.ThreadPoolWorkQueue.Dispatch()",
            RuntimeFrameKind.SchedulerEntry)]
        [InlineData(
            "System.Threading.Tasks.Task`1[System.Int64].TrySetResult(!0)",
            RuntimeFrameKind.TaskResult)]
        [InlineData(
            "System.Threading.Tasks.Task`1[!0].TrySetResult(!0)",
            RuntimeFrameKind.TaskResult)]
        [InlineData(
            "System.Threading.Tasks.Task`1[[System.Collections.Generic.List`1[System.Int32]]].TrySetResult(!0)",
            RuntimeFrameKind.TaskResult)]
        [InlineData(
            "System.Threading.Tasks.TaskCompletionSource`1[System.Boolean].SetResult(!0)",
            RuntimeFrameKind.TaskCompletionSourceResult)]
        [InlineData(
            "System.Runtime.CompilerServices.RuntimeAsyncTaskContinuation+TaskContinuationResume.ResumeTaskContinuation(class System.Runtime.CompilerServices.Continuation,unsigned int8&)",
            AsyncProfilerStitchSyncFrameKind.ContinuationBridge)]
        public void ClassifySynchronousFrame_SplitTraceEventModuleAndMethod_RequiresCoreLib(
            string method, object expectedValue)
        {
            FrameClassification expected = Classification(expectedValue);
            Assert.Equal(
                expected,
                AsyncProfilerRuntimeSupport.Common.ClassifySynchronousFrame(method, isHostModule: true));
            Assert.Equal(
                AsyncProfilerStitchSyncFrameKind.None,
                AsyncProfilerRuntimeSupport.Common.ClassifySynchronousFrame(method, isHostModule: false));
        }

        [Theory]
        [InlineData("Other.Namespace.Task`1[System.Int64].TrySetResult(!0)")]
        [InlineData("System.Threading.Tasks.Task`2[System.Int64,System.Int32].TrySetResult(!0)")]
        [InlineData("System.Threading.Tasks.Task`1[System.Int64]+Other.TrySetResult(!0)")]
        [InlineData("System.Threading.Tasks.Task`1[System.Int64.TrySetResult(!0)")]
        [InlineData("System.Threading.Tasks.TaskCompletionSource`10.SetResult(!0)")]
        [InlineData("Other.CoreLib!System.Threading.Tasks.Task`1[System.Int64].TrySetResult(!0)")]
        [InlineData("Other.Namespace.ThreadPoolWorkQueue.Dispatch()")]
        [InlineData("System.Threading.ThreadPoolWorkQueue`1[System.Int64].Dispatch()")]
        [InlineData("System.Threading.ThreadPoolWorkQueue.DispatchExtra()")]
        [InlineData("Other.CoreLib!System.Threading.ThreadPoolWorkQueue.Dispatch()")]
        [InlineData("Other.Namespace.AsyncProfiler+AsyncCallstack.CaptureStateMachineAsyncCallstack()")]
        [InlineData("System.Runtime.CompilerServices.OtherAsyncProfiler+AsyncCallstack.EmitEvent()")]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineDiagnostics`2[!0,!1].get_MethodId()")]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineDiagnostics`10.get_MethodId()")]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineDiagnostics`1[!0].cctor()")]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineDiagnostics`1[!0]+Other..cctor()")]
        [InlineData("Other.Namespace.AsyncStateMachineDiagnostics`1[!0]..cctor()")]
        [InlineData("Other.Namespace.InitHelpers.InitClassSlow()")]
        [InlineData("System.Runtime.CompilerServices.InitHelpers.Other()")]
        [InlineData("System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBaseSlowExtra()")]
        [InlineData("Other.CoreLib!System.Runtime.CompilerServices.AsyncProfiler+AsyncCallstack.EmitEvent()")]
        public void ClassifySynchronousFrame_SplitModuleAndMethod_RejectsLookalikes(string method)
        {
            Assert.Equal(
                AsyncProfilerStitchSyncFrameKind.None,
                AsyncProfilerRuntimeSupport.Common.ClassifySynchronousFrame(method, isHostModule: true));
        }

        [Theory]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineDispatcherInfo.CreateDispatcher")]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineDispatcherInfo.CreateDispatcher(class System.Runtime.CompilerServices.IAsyncStateMachineBox,value class Flags)")]
        public void ClassifySynchronousFrame_DispatcherCreation_RequiresCoreLibAndIsNotABoundary(string method)
        {
            Assert.Equal(
                AsyncProfilerStitchSyncFrameKind.V1DispatchSetup,
                AsyncProfilerRuntimeSupport.Common.ClassifySynchronousFrame("System.Private.CoreLib!" + method, isHostModule: true));
            Assert.Equal(
                AsyncProfilerStitchSyncFrameKind.V1DispatchSetup,
                AsyncProfilerRuntimeSupport.Common.ClassifySynchronousFrame(method, isHostModule: true));
            Assert.Equal(
                AsyncProfilerStitchSyncFrameKind.None,
                AsyncProfilerRuntimeSupport.Common.ClassifySynchronousFrame(method, isHostModule: false));
            AsyncProfilerCallStackStitchBoundaryInfo info = AsyncProfilerRuntimeSupport.Common.ClassifyBoundary("System.Private.CoreLib!" + method);
            Assert.Equal(AsyncProfilerCallStackStitchBoundaryKind.None, info.Kind);
            Assert.Equal(-1, info.WrapperIndex);
        }

        [Theory]
        [InlineData("Other.CoreLib!System.Runtime.CompilerServices.AsyncStateMachineDispatcherInfo.CreateDispatcher()")]
        [InlineData("Other.Namespace.AsyncStateMachineDispatcherInfo.CreateDispatcher()")]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineDispatcherInfoExtra.CreateDispatcher()")]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineDispatcherInfo`1[!0].CreateDispatcher()")]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineDispatcherInfo+Other.CreateDispatcher()")]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineDispatcherInfo.CreateDispatcherExtra()")]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineDispatcherInfo.Other(System.Runtime.CompilerServices.AsyncStateMachineDispatcherInfo.CreateDispatcher)")]
        public void ClassifySynchronousFrame_DispatcherCreation_RejectsLookalikes(string method)
        {
            Assert.Equal(
                AsyncProfilerStitchSyncFrameKind.None,
                AsyncProfilerRuntimeSupport.Common.ClassifySynchronousFrame(method, isHostModule: true));
            Assert.Equal(
                AsyncProfilerStitchSyncFrameKind.None,
                AsyncProfilerRuntimeSupport.Common.ClassifySynchronousFrame("System.Private.CoreLib!" + method, isHostModule: true));
        }

        [Theory]
        [InlineData("System.Runtime.CompilerServices.AsyncProfiler+AsyncCallstack.CaptureStateMachineAsyncCallstack(unsigned int8[],int32&,value class State&)", RuntimeFrameKind.ProfilerCapture)]
        [InlineData("System.Runtime.CompilerServices.AsyncProfiler+AsyncCallstack.EmitAsyncCallstack(unsigned int8[],int32&,!!0&)", RuntimeFrameKind.ProfilerEmission)]
        [InlineData("System.Runtime.CompilerServices.AsyncProfiler+AsyncCallstack.EmitEvent()", RuntimeFrameKind.ProfilerEmission)]
        [InlineData("System.Runtime.CompilerServices.AsyncProfiler+ResumeAsyncContext.Resume()", RuntimeFrameKind.ProfilerResume)]
        [InlineData("System.Runtime.CompilerServices.AsyncProfiler+SyncPoint.ResetContext()", AsyncProfilerStitchSyncFrameKind.ProfilerHelper)]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineDiagnostics`1[Scenario+<ChildAsync>d__3]..cctor()", AsyncProfilerStitchSyncFrameKind.ProfilerHelper)]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineDiagnostics`1[Scenario+<ChildAsync>d__3].get_MethodId()", AsyncProfilerStitchSyncFrameKind.ProfilerHelper)]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineDiagnostics`1[!0].get_StateOffset()", AsyncProfilerStitchSyncFrameKind.ProfilerHelper)]
        [InlineData("System.Runtime.CompilerServices.InitHelpers.CallClassConstructor(void*,void*,class System.Exception*)", RuntimeFrameKind.RuntimeInitialization)]
        [InlineData("System.Runtime.CompilerServices.InitHelpers.InitClassHelper(value class MethodTable*)", RuntimeFrameKind.RuntimeInitialization)]
        [InlineData("System.Runtime.CompilerServices.InitHelpers.InitClassSlow(value class MethodTable*)", RuntimeFrameKind.RuntimeInitialization)]
        [InlineData("System.Runtime.CompilerServices.StaticsHelpers.GetNonGCStaticBaseSlow(value class MethodTable*)", RuntimeFrameKind.RuntimeInitialization)]
        [InlineData("System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncStateMachineBox`1[System.Int64,Scenario+<ChildAsync>d__3].System.Runtime.CompilerServices.IAsyncStateMachineBox.GetDiagnosticData(unsigned int64&,int32&,class System.Object&)", AsyncProfilerStitchSyncFrameKind.ProfilerHelper)]
        [InlineData("System.Runtime.CompilerServices.AsyncProfiler+Other.EmitEvent()", AsyncProfilerStitchSyncFrameKind.ProfilerHelper)]
        [InlineData("System.Runtime.CompilerServices.AsyncProfiler+AsyncCallstack.EmitEventExtra()", AsyncProfilerStitchSyncFrameKind.ProfilerHelper)]
        [InlineData("System.Runtime.CompilerServices.AsyncProfiler+ResumeAsyncContext.Other()", AsyncProfilerStitchSyncFrameKind.ProfilerHelper)]
        [InlineData("System.Runtime.CompilerServices.AsyncProfiler+Other.Resume()", AsyncProfilerStitchSyncFrameKind.ProfilerHelper)]
        public void ClassifySynchronousFrame_ProfilerEnvelope_PreservesSpecializedKindsAndOwnership(
            string method, object expectedValue)
        {
            FrameClassification expected = Classification(expectedValue);
            Assert.Equal(expected, AsyncProfilerRuntimeSupport.Common.ClassifySynchronousFrame("System.Private.CoreLib!" + method, isHostModule: true));
            Assert.Equal(expected, AsyncProfilerRuntimeSupport.Common.ClassifySynchronousFrame(method, isHostModule: true));
            Assert.Equal(AsyncProfilerStitchSyncFrameKind.None, AsyncProfilerRuntimeSupport.Common.ClassifySynchronousFrame(method, isHostModule: false));
        }

        #region private

        private static FrameClassification Classification(object expected) =>
            expected is RuntimeFrameKind helper
                ? (FrameClassification)helper
                : new FrameClassification((AsyncProfilerStitchSyncFrameKind)expected);

        private static AsyncProfilerRuntimeSupport[] InitialSupport() =>
            new[] { AsyncProfilerRuntimeSupport.Common, AsyncProfilerRuntimeSupport.CoreClr11 };

        private sealed class NoDispatcherInfrastructureSupport : AsyncProfilerRuntimeSupport
        {
            public NoDispatcherInfrastructureSupport() : base("Test profile")
            {
            }

            protected override bool IsV1DispatcherInfrastructure(string frameName) => false;
        }

        #endregion
    }
}
