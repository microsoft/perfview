// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Tracing.Computers
{
    /// <summary>
    /// Runtime implementation names used by async frame classification and sequence matching.
    /// Groups keep declaring types, members, and symbol-name variants together.
    /// </summary>
    /// <remarks>
    /// These are implementation dependencies, not promises about every runtime version.
    /// Shared profiler boundary contracts remain in <see cref="AsyncProfilerCallStackStitchBoundary"/>.
    /// Matching still requires the ownership and layout checks in <see cref="AsyncProfilerRuntimeSupport"/>.
    /// </remarks>
    internal static class AsyncProfilerRuntimeNames
    {
        public const string CoreLib = "System.Private.CoreLib";
        public const string CompilerServicesNamespace = "System.Runtime.CompilerServices.";

        // TraceModuleFile.Name omits Windows extensions but retains Unix shared-library extensions.
        internal static class NativeModules
        {
            public const string CoreClr = "coreclr";
            public const string Jit = "clrjit";
            public const string UnixCoreClr = "libcoreclr";
            public const string UnixJit = "libclrjit";
            public const string LinuxExtension = ".so";
            public const string MacExtension = ".dylib";
            public const string LinuxCoreClr = UnixCoreClr + LinuxExtension;
            public const string LinuxJit = UnixJit + LinuxExtension;
            public const string MacCoreClr = UnixCoreClr + MacExtension;
            public const string MacJit = UnixJit + MacExtension;
            public const string MonoSGen = "mono-2.0-sgen";
            public const string UnixMonoSGen = "libmonosgen-2.0";
            public const string UnixMonoBoehm = "libmonobdwgc-2.0";
            public const string DesktopClr = "clr";
            public const string LegacyDesktopClr = "mscorwks";
        }

        // Generated resume stubs can belong to an application module, not CoreLib.
        internal static class DynamicMethods
        {
            public const string Type = "dynamicClass";
            public const string AsyncResume = "IL_STUB_AsyncResume";
        }

        // GetBareMethodName removes the leading dot from CLR constructor names.
        internal static class Constructors
        {
            public const string Instance = "ctor";
            public const string Static = "cctor";
        }

        // The suffix deliberately includes ordinary, profiler-enabled, and debug-finalizable boxes.
        internal static class StateMachineBoxes
        {
            public const string TypeSuffix = "AsyncStateMachineBox";
            public const string GenericTypeSuffix = TypeSuffix + "`1";
            public const string MoveNext = "MoveNext";
            public const string InstrumentedMoveNext = "InstrumentedMoveNext";
            public const string ExecutionContextCallback = "ExecutionContextCallback";
        }

        // Builder names are compared after removing generic arity. Other members remain builder helpers.
        internal static class Builders
        {
            public const string Core = "AsyncMethodBuilderCore";
            public const string Task = "AsyncTaskMethodBuilder";
            public const string ValueTask = "AsyncValueTaskMethodBuilder";
            public const string PoolingValueTask = "PoolingAsyncValueTaskMethodBuilder";
            public const string Void = "AsyncVoidMethodBuilder";
            public const string Iterator = "AsyncIteratorMethodBuilder";
            public const string ValueTaskType = CompilerServicesNamespace + ValueTask;
            public const string GenericValueTaskType = ValueTaskType + "`1";
            public const string GenericPoolingValueTaskType = CompilerServicesNamespace + PoolingValueTask + "`1";
            public const string PooledBoxName = "StateMachineBox";
            public const string PooledBoxType = GenericPoolingValueTaskType + "+" + PooledBoxName;
            public const string Start = "Start";
            public const string AwaitUnsafeOnCompleted = "AwaitUnsafeOnCompleted";
            public const string SetExistingTaskResult = "SetExistingTaskResult";
            public const string SetResult = "SetResult";
            public const string SetException = "SetException";
        }

        // Configured awaiters have symbol variants with generic arguments on the enclosing or nested type.
        internal static class Awaiters
        {
            public const string Task = "TaskAwaiter";
            public const string ConfiguredTask = "ConfiguredTaskAwaiter";
            public const string ValueTask = "ValueTaskAwaiter";
            public const string ConfiguredValueTask = "ConfiguredValueTaskAwaiter";
            public const string Yield = "YieldAwaiter";
            public const string ForceYielding = "ForceYieldingAwaiter";
            public const string TaskType = CompilerServicesNamespace + Task;
            public const string GenericTaskType = TaskType + "`1";
            public const string ValueTaskType = CompilerServicesNamespace + ValueTask;
            public const string GenericValueTaskType = ValueTaskType + "`1";
            public const string ConfiguredTaskType =
                CompilerServicesNamespace + "ConfiguredTaskAwaitable+" + ConfiguredTask;
            public const string GenericTaskAwaitableType = CompilerServicesNamespace + "ConfiguredTaskAwaitable`1";
            public const string GenericConfiguredTaskType = GenericTaskAwaitableType + "+" + ConfiguredTask;
            public const string ConfiguredValueTaskType =
                CompilerServicesNamespace + "ConfiguredValueTaskAwaitable+" + ConfiguredValueTask;
            public const string GenericValueTaskAwaitableType =
                CompilerServicesNamespace + "ConfiguredValueTaskAwaitable`1";
            public const string GenericConfiguredValueTaskType =
                GenericValueTaskAwaitableType + "+" + ConfiguredValueTask;
            public const string YieldAwaitableType = CompilerServicesNamespace + "YieldAwaitable";
            public const string YieldAwaiterType = YieldAwaitableType + "+" + Yield;
            public const string BoxAwareInterface = "." + CompilerServicesNamespace + "IStateMachineBoxAwareAwaiter.";
            public const string OnCompleted = "OnCompleted";
            public const string UnsafeOnCompleted = "UnsafeOnCompleted";
            public const string OnCompletedInternal = "OnCompletedInternal";
            public const string UnsafeOnCompletedInternal = "UnsafeOnCompletedInternal";
            public const string AwaitUnsafeOnCompleted = "AwaitUnsafeOnCompleted";
            public const string QueueContinuation = "QueueContinuation";
            public const string OutputCorrelationEtwEvent = "OutputCorrelationEtwEvent";
            public const string IsCompleted = "get_IsCompleted";
        }

        // General profiler and nested-box helpers are families; capture/emission/resume keep distinct roles.
        internal static class Profiler
        {
            public const string Type = CompilerServicesNamespace + "AsyncProfiler";
            public const string CallstackType = Type + "+AsyncCallstack";
            public const string ResumeContextType = Type + "+ResumeAsyncContext";
            public const string EventSourceType = CompilerServicesNamespace + "AsyncProfilerEventSource";
            public const string DiagnosticsType = CompilerServicesNamespace + "AsyncStateMachineDiagnostics`1";
            public const string DispatcherInfoType = CompilerServicesNamespace + "AsyncStateMachineDispatcherInfo";
            public const string CreateDispatcher = "CreateDispatcher";
            public const string CaptureStateMachineCallstack = "CaptureStateMachineAsyncCallstack";
            public const string EmitCallstack = "EmitAsyncCallstack";
            public const string EmitEvent = "EmitEvent";
            public const string Resume = "Resume";
        }

        // Task and Task<T> are exact declaring types, not arbitrary Task-nested helpers.
        internal static class Task
        {
            public const string Type = "System.Threading.Tasks.Task";
            public const string GenericType = Type + "`1";
            public const string RunContinuations = "RunContinuations";
            public const string TrySetResult = "TrySetResult";
            public const string TrySetException = "TrySetException";
            public const string AddException = "AddException";
            public const string TrySetCanceled = "TrySetCanceled";
            public const string AddTaskContinuation = "AddTaskContinuation";
            public const string SetContinuationForAwait = "SetContinuationForAwait";
            public const string UnsafeSetContinuationForAwait = "UnsafeSetContinuationForAwait";
        }

        internal static class TaskCompletionSource
        {
            public const string Type = "System.Threading.Tasks.TaskCompletionSource";
            public const string GenericType = Type + "`1";
            public const string SetResult = "SetResult";
        }

        internal static class TaskExceptionHolder
        {
            public const string Type = "System.Threading.Tasks.TaskExceptionHolder";
            public const string AddFaultException = "AddFaultException";
            public const string SetCancellationException = "SetCancellationException";
        }

        internal static class AwaitTaskContinuation
        {
            public const string Type = "System.Threading.Tasks.AwaitTaskContinuation";
            public const string RunOrScheduleAction = "RunOrScheduleAction";
        }

        internal static class RuntimeAsyncTaskContinuation
        {
            public const string ResumeType =
                CompilerServicesNamespace + "RuntimeAsyncTaskContinuation+TaskContinuationResume";
            public const string ResumeTaskContinuation = "ResumeTaskContinuation";
        }

        internal static class ExecutionContext
        {
            public const string Type = "System.Threading.ExecutionContext";
            public const string RunInternal = "RunInternal";
            public const string RunFromThreadPoolDispatchLoop = "RunFromThreadPoolDispatchLoop";
        }

        internal static class ThreadPool
        {
            public const string WorkQueueType = "System.Threading.ThreadPoolWorkQueue";
            public const string Dispatch = "Dispatch";
        }

        // Only CoreLib's source core and pooled builder boxes are recognized, not arbitrary IValueTaskSource types.
        internal static class ValueTaskSources
        {
            public const string CoreType = "System.Threading.Tasks.Sources.ManualResetValueTaskSourceCore`1";
            public const string SharedType = "System.Threading.Tasks.Sources.ManualResetValueTaskSourceCoreShared";
            public const string SignalCompletion = "SignalCompletion";
            public const string InvokeContinuationWithContext = "InvokeContinuationWithContext";
            public const string OnCompleted = "OnCompleted";
            public const string SetResult = "SetResult";
            public const string SetException = "SetException";
        }

        // These helpers are classified separately; their names alone do not authorize dispatcher cleanup.
        internal static class Initialization
        {
            public const string InitHelpersType = CompilerServicesNamespace + "InitHelpers";
            public const string StaticsHelpersType = CompilerServicesNamespace + "StaticsHelpers";
            public const string CallClassConstructor = "CallClassConstructor";
            public const string InitClassHelper = "InitClassHelper";
            public const string InitClassSlow = "InitClassSlow";
            public const string GetNonGCStaticBaseSlow = "GetNonGCStaticBaseSlow";
        }
    }
}
