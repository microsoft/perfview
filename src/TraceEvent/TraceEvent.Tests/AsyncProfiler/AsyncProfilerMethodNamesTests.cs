// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Diagnostics.Tracing.Computers;

using Xunit;

namespace TraceEventTests
{
    /// <summary>
    /// Checks symbol-name parsing and compiler-generated state-machine names without a trace or symbol table.
    /// </summary>
    public class AsyncProfilerMethodNamesTests
    {
        [Theory]
        [InlineData(null, null)]
        [InlineData("", "")]
        [InlineData("(", "(")]
        [InlineData("Namespace.Type.Method()", "Namespace.Type.Method()")]
        [InlineData("Namespace.Type.Method(class Other::Parameter)", "Namespace.Type.Method(class Other::Parameter)")]
        [InlineData("Namespace.Type::Method(class Other::Parameter)", "Namespace.Type.Method(class Other::Parameter)")]
        [InlineData("Namespace.Type::Method", "Namespace.Type.Method")]
        [InlineData("Namespace.Type::.ctor()", "Namespace.Type..ctor()")]
        [InlineData("Namespace.Type::.cctor()", "Namespace.Type..cctor()")]
        [InlineData("Namespace.Type::System.IDisposable.Dispose()", "Namespace.Type.System.IDisposable.Dispose()")]
        [InlineData("System.Runtime.CompilerServices.AsyncHelpers+RuntimeAsyncTask`1[System.Threading.Tasks.VoidTaskResult]::DispatchContinuations()",
                    "System.Runtime.CompilerServices.AsyncHelpers+RuntimeAsyncTask`1[System.Threading.Tasks.VoidTaskResult].DispatchContinuations()")]
        [InlineData("System.Runtime.CompilerServices.AsyncHelpers+RuntimeAsyncTask`1[System.Threading.Tasks.VoidTaskResult]::InstrumentedDispatchContinuations(valuetype System.Runtime.CompilerServices.AsyncInstrumentation/Flags)",
                    "System.Runtime.CompilerServices.AsyncHelpers+RuntimeAsyncTask`1[System.Threading.Tasks.VoidTaskResult].InstrumentedDispatchContinuations(valuetype System.Runtime.CompilerServices.AsyncInstrumentation/Flags)")]
        [InlineData("System.Private.CoreLib!System.Runtime.CompilerServices.AsyncProfiler+ContinuationWrapper::Continuation_Wrapper_1(class System.Runtime.CompilerServices.Continuation,uint8&)",
                    "System.Private.CoreLib!System.Runtime.CompilerServices.AsyncProfiler+ContinuationWrapper.Continuation_Wrapper_1(class System.Runtime.CompilerServices.Continuation,uint8&)")]
        public void NormalizeMethodName_ChangesOnlyTheTypeMethodSeparator(string name, string expected)
        {
            string normalized = AsyncProfilerMethodNames.NormalizeMethodName(name);
            Assert.Equal(expected, normalized);
            Assert.Same(normalized, AsyncProfilerMethodNames.NormalizeMethodName(normalized));
            if (name == expected)
            {
                Assert.Same(name, normalized);
            }
        }

        [Theory]
        [InlineData("System.Boolean System.Threading.Tasks.Task::TrySetResult()",
                    "System.Threading.Tasks.Task.TrySetResult()")]
        [InlineData("System.Threading.ThreadPoolWorkQueue+DispatchResult System.Threading.ThreadPoolWorkQueue::Dispatch()",
                    "System.Threading.ThreadPoolWorkQueue.Dispatch()")]
        [InlineData("System.Boolean System.Threading.Tasks.Task.TrySetResult()",
                    "System.Threading.Tasks.Task.TrySetResult()")]
        [InlineData("System.Private.CoreLib!System.Boolean System.Threading.Tasks.Task::TrySetResult()",
                    "System.Private.CoreLib!System.Threading.Tasks.Task.TrySetResult()")]
        [InlineData("Other.CoreLib!System.Boolean System.Threading.Tasks.Task::TrySetResult()",
                    "Other.CoreLib!System.Threading.Tasks.Task.TrySetResult()")]
        [InlineData("System.Collections.Generic.Dictionary`2[System.Int32, System.String] Namespace.Type::Method(class Other::Parameter)",
                    "Namespace.Type.Method(class Other::Parameter)")]
        [InlineData("System.Boolean System.Threading.Tasks.Task`1[System.Collections.Generic.Dictionary`2[System.Int32, System.String]]::TrySetResult(!0)",
                    "System.Threading.Tasks.Task`1[System.Collections.Generic.Dictionary`2[System.Int32, System.String]].TrySetResult(!0)")]
        [InlineData("System.Threading.Tasks.Task`1[System.Int32, System.String]::TrySetResult(!0)",
                    "System.Threading.Tasks.Task`1[System.Int32, System.String].TrySetResult(!0)")]
        [InlineData("!!0 Namespace.Type`1[!0, !!0]::Method(!!0&)",
                    "Namespace.Type`1[!0, !!0].Method(!!0&)")]
        [InlineData("unsigned int8 Namespace.Type::Method(unsigned int8&,class Other::Parameter)",
                    "Namespace.Type.Method(unsigned int8&,class Other::Parameter)")]
        public void NormalizeMethodName_RemovesPdbReturnTypesWithoutChangingTypeArgumentsOrParameters(string name, string expected)
        {
            string normalized = AsyncProfilerMethodNames.NormalizeMethodName(name);
            Assert.Equal(expected, normalized);
            Assert.Same(normalized, AsyncProfilerMethodNames.NormalizeMethodName(normalized));
        }

        [Theory]
        [InlineData("Namespace.Type+<RunAsync>d__1::MoveNext()", "Namespace.Type+<RunAsync>d__1")]
        [InlineData("Namespace.Type`1+<RunAsync>d__1`1[System.Int64,System.String]::MoveNext()",
                    "Namespace.Type`1+<RunAsync>d__1`1[System.Int64,System.String]")]
        public void NormalizeMethodName_SupportsExistingV1CompilerNameDecoding(string name, string expectedType)
        {
            string normalized = AsyncProfilerMethodNames.NormalizeMethodName(name);
            Assert.Equal(expectedType, AsyncProfilerMethodNames.GetStateMachineTypeName(normalized));
            Assert.True(AsyncProfilerMethodNames.TryGetLogicalStateMachineMethodName(
                normalized, out string logicalName, out string suffix));
            Assert.StartsWith("Namespace.Type", logicalName);
            Assert.EndsWith(".RunAsync", logicalName);
            Assert.StartsWith("d__1", suffix);
        }

        [Fact]
        public void NormalizeMethodName_PreservesV1CarrierTypeIdentity()
        {
            const string stateMachineType = "Namespace.Type`1+<RunAsync>d__1`1[System.Int64,System.String]";
            string box = AsyncProfilerMethodNames.NormalizeMethodName(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncProfilerAsyncStateMachineBox`1[" +
                "System.Int64," + stateMachineType + "]::InstrumentedMoveNext(class System.Threading.Thread)");
            string moveNext = AsyncProfilerMethodNames.NormalizeMethodName(stateMachineType + "::MoveNext()");
            foreach (AsyncProfilerRuntimeSupport support in new[] { AsyncProfilerRuntimeSupport.Common, AsyncProfilerRuntimeSupport.CoreClr11 })
            {
                Assert.True(support.TryGetBoxedStateMachineTypeName(box, true, out string boxedType));
                Assert.Equal(AsyncProfilerMethodNames.GetStateMachineTypeName(moveNext), boxedType);
                Assert.False(support.TryGetBoxedStateMachineTypeName(box, false, out _));
            }
        }

        [Theory]
        [InlineData("Continuation_Wrapper_0", "Continuation_Wrapper_0")]                                     // already bare
        [InlineData("System.Private.CoreLib!AsyncProfiler+ContinuationWrapper.Continuation_Wrapper_7", "Continuation_Wrapper_7")] // module + nested type
        [InlineData("SomeModule!Ns.Type.Method(System.Int32, System.String)", "Method")]                     // signature stripped
        [InlineData("Ns.Type.MoveNextAsDispatcher", "MoveNextAsDispatcher")]                                 // dotted, no module
        [InlineData("ntdll!RtlUserThreadStart", "RtlUserThreadStart")]                                        // module only, no dot
        [InlineData("System.Private.CoreLib!Continuation_Wrapper_3", "Continuation_Wrapper_3")]
        [InlineData("My.App.dll!Method", "Method")]
        [InlineData("Ns.Type.Method[Other.Namespace.Payload]()", "Method[Other.Namespace.Payload]")]
        [InlineData("BareToken", "BareToken")]                                                                // unqualified
        [InlineData(null, null)]
        [InlineData("", "")]
        public void GetBareMethodName_ExtractsMethodComponent(string frameName, string expected)
        {
            Assert.Equal(expected, AsyncProfilerMethodNames.GetBareMethodName(frameName));
        }

        [Theory]
        [InlineData(
            "Namespace.Type+<FooAsync>d__17.MoveNext()",
            "Namespace.Type.FooAsync",
            "d__17")]
        [InlineData(
            "Namespace.Outer+Nested+<Run>d_4.MoveNext()",
            "Namespace.Outer.Nested.Run",
            "d_4")]
        [InlineData(
            "Namespace.Type`1+<RunAsync>d__1`1[MyApp.MoveNextPayload].MoveNext()",
            "Namespace.Type`1.RunAsync",
            "d__1`1[MyApp.MoveNextPayload]")]
        [InlineData(
            "Namespace.Type`1+<RunAsync>d__1`1[Other.Type+<Nested>d__2].MoveNext()",
            "Namespace.Type`1.RunAsync",
            "d__1`1[Other.Type+<Nested>d__2]")]
        [InlineData(
            "Namespace.Type+<>c+<<Run>b__0_0>d.MoveNext()",
            "Namespace.Type.<>c.<Run>b__0_0",
            "d")]
        [InlineData(
            "Namespace.Type+<<Run>g__Inner|1_0>d.MoveNext()",
            "Namespace.Type.<Run>g__Inner|1_0",
            "d")]
        public void TryGetLogicalStateMachineMethodName_ReturnsGeneratedSuffix(
            string methodName, string expectedLogicalName, string expectedStateMachineSuffix)
        {
            Assert.True(AsyncProfilerMethodNames.TryGetLogicalStateMachineMethodName(
                methodName, out string logicalName, out string stateMachineSuffix));
            Assert.Equal(expectedLogicalName, logicalName);
            Assert.Equal(expectedStateMachineSuffix, stateMachineSuffix);
        }

        [Theory]
        [InlineData("Namespace.Type+<Run>d__1.MoveNextHelper()")]
        [InlineData("Namespace.Type+<Run>d__1.MoveNextAsync()")]
        [InlineData("Namespace.Type+<Run>d__1.SetStateMachine(MyApp.MoveNextPayload)")]
        [InlineData("Namespace.Type+<Run>display.MoveNext()")]
        public void TryGetLogicalStateMachineMethodName_RejectsOtherMembersAndTypeSuffixes(string methodName)
        {
            Assert.False(AsyncProfilerMethodNames.TryGetLogicalStateMachineMethodName(
                methodName, out string logicalName, out string stateMachineSuffix));
            Assert.Null(logicalName);
            Assert.Null(stateMachineSuffix);
        }

        [Theory]
        [InlineData("Namespace.Type+<>c+<<Run>b__0_0>d.MoveNext()", null, "Namespace.Type.<>c.<Run>b__0_0")]
        [InlineData("Namespace.Type+<>c+<<Run>b__0_0>d.MoveNext()", -1, "Namespace.Type.<>c.<Run>b__0_0 [state -1]")]
        [InlineData("Namespace.Type+<<Run>g__Inner|1_0>d.MoveNext()", 2, "Namespace.Type.<Run>g__Inner|1_0 [state 2]")]
        [InlineData("Namespace.Type`1+<<Run>b__0_0>d`1[MyApp.Payload].MoveNext()", null, "Namespace.Type`1.<Run>b__0_0`1[MyApp.Payload]")]
        public void LogicalStateMachineMethodName_LambdaAndLocalFunctionLabelsDoNotInventOrdinals(string methodName, int? state, string expected)
        {
            Assert.True(AsyncProfilerMethodNames.TryGetLogicalStateMachineMethodName(
                methodName, out string logicalName, out string stateMachineSuffix));
            Assert.Equal(expected, AsyncProfilerMethodNames.FormatLogicalStateMachineMethodName(logicalName, stateMachineSuffix, state));
        }

        [Theory]
        [InlineData("TestApp!Namespace.App+<Run>d__1.MoveNext()", "Namespace.App+<Run>d__1")]
        [InlineData("Namespace.App`1+<Run>d__1`1[System.Int32,System.String].MoveNext()", "Namespace.App`1+<Run>d__1`1[System.Int32,System.String]")]
        [InlineData("Namespace.App`1+<Run>d__1`1[!0].MoveNext()", "Namespace.App`1+<Run>d__1`1[!0]")]
        [InlineData("My.App.dll!MoveNext()", null)]
        [InlineData("Namespace.App.Run()", null)]
        [InlineData(null, null)]
        public void GetStateMachineTypeName_ExtractsQualifiedStateMachineMethodTypes(string name, string expected)
        {
            Assert.Equal(expected, AsyncProfilerMethodNames.GetStateMachineTypeName(name));
        }

        [Theory]
        [InlineData("System.Private.CoreLib!System.Runtime.CompilerServices.AsyncStateMachineDispatcher.MoveNext()", "AsyncStateMachineDispatcher")]
        [InlineData("Ns.Outer+Inner.Method", "Inner")]                       // nested type ('+') separator
        [InlineData("Module!Type.Method(System.Int32)", "Type")]             // module + signature stripped
        [InlineData("Type.Method", "Type")]                                  // no module/namespace
        [InlineData("Type..ctor()", "Type")]
        [InlineData("Module!Ns.Outer+Inner..cctor()", "Inner")]
        [InlineData("System.Private.CoreLib!System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncProfilerAsyncStateMachineBox`1[System.Int64,Scenario+<ChildAsync>d__9]..ctor()",
            "AsyncProfilerAsyncStateMachineBox`1[System.Int64,Scenario+<ChildAsync>d__9]")]
        [InlineData("System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+DebugFinalizableAsyncStateMachineBox`1[System.Int64,Scenario+<ChildAsync>d__9]..ctor()",
            "DebugFinalizableAsyncStateMachineBox`1[System.Int64,Scenario+<ChildAsync>d__9]")]
        [InlineData("System.Private.CoreLib!System.Runtime.CompilerServices.AsyncTaskMethodBuilder`1+AsyncProfilerAsyncStateMachineBox`1[System.Int64,V1TraceScenarios+<BurnCpuAsync>d__15].InstrumentedMoveNext(class System.Threading.Thread,value class Flags)",
            "AsyncProfilerAsyncStateMachineBox`1[System.Int64,V1TraceScenarios+<BurnCpuAsync>d__15]")]   // separators inside type arguments and signature
        [InlineData(".ctor()", null)]
        [InlineData(".cctor()", null)]
        [InlineData("BareToken", null)]                                       // no type qualifier
        [InlineData("Module!Method", null)]                                   // module but no type
        [InlineData("System.Private.CoreLib!Continuation_Wrapper_3", null)]
        [InlineData("My.App.dll!Method", null)]
        [InlineData("Ns.Type.Method[Other.Namespace.Payload]()", "Type")]
        [InlineData(null, null)]
        [InlineData("", null)]
        public void GetDeclaringTypeName_ExtractsTypeComponent(string frameName, string expected)
        {
            Assert.Equal(expected, AsyncProfilerMethodNames.GetDeclaringTypeName(frameName));
        }

        [Theory]
        [InlineData("Box`1[State`2[System.Int32,System.String]]", "State`2[System.Int32,System.String]")]
        [InlineData("Builder`1+Box`1[System.Int32, State`1[Other+Nested]]", "State`1[Other+Nested]")]
        [InlineData("Box`1[[Namespace.State, Assembly]]", "[Namespace.State, Assembly]")]
        public void TryGetLastTypeArgument_PreservesNestedTypeArguments(string typeName, string expected)
        {
            Assert.True(AsyncProfilerMethodNames.TryGetLastTypeArgument(typeName, out string typeArgument));
            Assert.Equal(expected, typeArgument);
        }

        [Theory]
        [InlineData("Box`1[State")]
        [InlineData("Box`1[State]]")]
        [InlineData("Box`1[State`1[Other]")]
        [InlineData("Box`1[,State]")]
        public void TryGetLastTypeArgument_RejectsMalformedLists(string typeName)
        {
            Assert.False(AsyncProfilerMethodNames.TryGetLastTypeArgument(typeName, out _));
        }

        [Theory]
        [InlineData("Box`1[Other.Type].Namespace.Interface.Run()", "Box`1[Other.Type]")]
        [InlineData("Builder`1[Other.Type]+Box`1[State].Namespace.Interface.Run()", "Box`1[State]")]
        [InlineData("Box`1[Other.Type.Namespace.Interface.Run()", null)]
        public void GetImplementingTypeName_PreservesExplicitInterfaceOwnership(string frameName, string expected)
        {
            Assert.Equal(expected, AsyncProfilerMethodNames.GetImplementingTypeName(frameName, 0));
        }
    }
}
