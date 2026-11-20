// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Diagnostics.Tracing.Computers;

using Xunit;

namespace TraceEventTests
{
    /// <summary>
    /// Checks profiler dispatcher contracts and wrapper slots without a trace or symbol table.
    /// </summary>
    public class AsyncProfilerCallStackStitchBoundaryTests
    {
        [Theory]
        [InlineData("Continuation_Wrapper_0", true, 0)]
        [InlineData("Continuation_Wrapper_31", true, 31)]                                                     // last valid slot
        [InlineData("Continuation_Wrapper_32", false, -1)]                                                    // one past the pool
        [InlineData("Continuation_Wrapper_", false, -1)]                                                      // empty suffix
        [InlineData("Continuation_Wrapper_x", false, -1)]                                                     // non-numeric
        [InlineData("Continuation_Wrapper_-1", false, -1)]                                                    // sign rejected (NumberStyles.None)
        [InlineData("Continuation_Wrapper_007", true, 7)]                                                     // leading zeros ok
        [InlineData("NotAWrapper", false, -1)]
        [InlineData(null, false, -1)]
        public void TryGetWrapperIndex_ParsesPoolSlot(string method, bool expectedResult, int expectedIndex)
        {
            bool result = AsyncProfilerCallStackStitchBoundary.TryGetWrapperIndex(method, out int wrapperIndex);
            Assert.Equal(expectedResult, result);
            Assert.Equal(expectedIndex, wrapperIndex);
        }

        [Theory]
        // V2 dispatcher (continuation wrapper carrying a slot)
        [InlineData("System.Private.CoreLib!AsyncProfiler+ContinuationWrapper.Continuation_Wrapper_0", (int)AsyncProfilerCallStackStitchBoundaryKind.V2Dispatcher, 0)]
        [InlineData("System.Private.CoreLib!AsyncProfiler+ContinuationWrapper.Continuation_Wrapper_31", (int)AsyncProfilerCallStackStitchBoundaryKind.V2Dispatcher, 31)]
        [InlineData("System.Private.CoreLib!Continuation_Wrapper_3", (int)AsyncProfilerCallStackStitchBoundaryKind.V2Dispatcher, 3)]
        // V2 dispatcher infrastructure (dispatch-continuation helpers, no wrapper slot)
        [InlineData("System.Private.CoreLib!System.Runtime.CompilerServices.RuntimeAsyncTask`1.DispatchContinuations()", (int)AsyncProfilerCallStackStitchBoundaryKind.V2DispatcherInfrastructure, -1)]
        [InlineData("System.Private.CoreLib!System.Runtime.CompilerServices.RuntimeAsyncTask`1.InstrumentedDispatchContinuations()", (int)AsyncProfilerCallStackStitchBoundaryKind.V2DispatcherInfrastructure, -1)]
        // V1 dispatcher (merged box)
        [InlineData("System.Private.CoreLib!...AsyncProfilerAsyncStateMachineBox`1.MoveNextAsDispatcher()", (int)AsyncProfilerCallStackStitchBoundaryKind.V1Dispatcher, -1)]
        // V1 dispatcher (non-merged/standalone wrapper) -- recognized type-qualified
        [InlineData("System.Private.CoreLib!System.Runtime.CompilerServices.AsyncStateMachineDispatcher.MoveNext()", (int)AsyncProfilerCallStackStitchBoundaryKind.V1Dispatcher, -1)]
        [InlineData("System.Runtime.CompilerServices.AsyncStateMachineDispatcher.MoveNext", (int)AsyncProfilerCallStackStitchBoundaryKind.V1Dispatcher, -1)] // no module
        // Not a boundary
        [InlineData("System.Private.CoreLib!System.Threading.Tasks.Task.RunContinuations(System.Object)", (int)AsyncProfilerCallStackStitchBoundaryKind.None, -1)]
        [InlineData("MyApp!MyApp.Program.MoveNext()", (int)AsyncProfilerCallStackStitchBoundaryKind.None, -1)]
        [InlineData("MyApp!MyApp.AsyncStateMachineBox`1.MoveNext()", (int)AsyncProfilerCallStackStitchBoundaryKind.None, -1)]
        // A bare "MoveNext" (or on any other type) must NOT be mistaken for the non-merged dispatcher.
        [InlineData("System.Private.CoreLib!Some.Other.StateMachineBox`1.MoveNext()", (int)AsyncProfilerCallStackStitchBoundaryKind.None, -1)]
        [InlineData("MoveNext", (int)AsyncProfilerCallStackStitchBoundaryKind.None, -1)]
        [InlineData(null, (int)AsyncProfilerCallStackStitchBoundaryKind.None, -1)]
        [InlineData("", (int)AsyncProfilerCallStackStitchBoundaryKind.None, -1)]
        public void ClassifyContractBoundary_RecognizesDispatcherContracts(string frameName, int expectedKindValue, int expectedWrapperIndex)
        {
            var expectedKind = (AsyncProfilerCallStackStitchBoundaryKind)expectedKindValue;
            AsyncProfilerCallStackStitchBoundaryKind kind = AsyncProfilerCallStackStitchBoundary.ClassifyContractBoundary(frameName, out int wrapperIndex);
            Assert.Equal(expectedKind, kind);
            Assert.Equal(expectedWrapperIndex, wrapperIndex);
        }
    }
}
