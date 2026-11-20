// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
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
        public static IEnumerable<object[]> HostParityScenarios() =>
            new[]
            {
                "runtime-bridge-v1", "runtime-bridge-v2", "profiler-envelope", "profiler-helper-tail",
                "completed-dispatcher-tail", "fully-completed-recursive", "partially-completed-recursive",
                "reentrant-dispatch-entry", "dispatcher-creation", "await-registration", "yield-runtime-bridge",
                "nested-completed-outer-yield", "native-runtime-dispatch", "v1-after-v2-completion",
                "completion-machinery", "exception-machinery",
            }.Select(name => new object[] { name });

        // Every host wraps the same core builder; scenario tests run only the sample-profiler host and these
        // representative scenarios check that the public builder and thread-time computer agree with it.
        [Theory]
        [MemberData(nameof(HostParityScenarios))]
        public void HostParity_PublicBuilderAndThreadTimeComputerMatchTheSampleProfilerHost(string name)
        {
            StitchScenario scenario = name switch
            {
                "runtime-bridge-v1" => CreateRuntimeResumptionBridgeScenario(AsyncCallstackKind.StateMachineAsync),
                "runtime-bridge-v2" => CreateRuntimeResumptionBridgeScenario(AsyncCallstackKind.RuntimeAsync),
                "profiler-envelope" => CreateProfilerEnvelopeScenario(),
                "profiler-helper-tail" => CreateProfilerHelperTailScenario(),
                "completed-dispatcher-tail" => CreateV1CompletedDispatcherTailScenario(),
                "fully-completed-recursive" => CreateFullyCompletedRecursiveV1UnwindScenario(2, false),
                "partially-completed-recursive" => CreatePartiallyCompletedRecursiveV1UnwindScenario(3, false, false),
                "reentrant-dispatch-entry" => CreateReentrantDispatchEntryScenario(),
                "dispatcher-creation" => CreateDispatcherCreationScenario(),
                "await-registration" => CreateV1AwaitRegistrationScenario(),
                "yield-runtime-bridge" => CreateYieldRuntimeBridgeScenario(AsyncCallstackKind.StateMachineAsync),
                "nested-completed-outer-yield" => CreateNestedCompletedV1OuterYieldScenario(),
                "native-runtime-dispatch" => CreateNativeRuntimeDispatchScenario(false, false),
                "v1-after-v2-completion" => CreateV1AfterV2CompletionScenario(),
                "completion-machinery" => CreateV1CompletionMachineryScenario(),
                "exception-machinery" => CreateV1ExceptionMachineryScenario(false),
                _ => throw new ArgumentOutOfRangeException(nameof(name)),
            };
            scenario.VerifyPublicBuilder = true;
            scenario.VerifyThreadTimeComputer = true;
            scenario.IncludeEventSourceEvents = false;
            scenario.AllowSupplementalSamples = true;
            scenario.AssertProductionStitch();
        }

        [Fact]
        public void PublicBuilder_TransformsRootsAndIncrementalLookup()
        {
            Frame work = Frame.App("Scenario.Work");
            Frame current = Frame.App("Scenario.CurrentAsync");
            Frame parent = Frame.App("Scenario.ParentAsync");
            var scenario = new StitchScenario
            {
                Sync = new[] { work, current, Frame.V2Wrapper(0), Frame.V2InstrumentedDispatch,
                    Frame.V2Dispatch, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart },
                Async = new[] { current, parent },
                ExpectedStitched = new[] { work, current, parent, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V2InfrastructureFramesCollapsed = 4,
                },
                AssertPublicApi = (log, path) =>
                {
                    var query = new AsyncProfilerCallStackComputer(log);
                    var output = new MutableTraceEventStackSource(log);
                    var builder = new AsyncProfilerCallStackSourceBuilder(output);
                    TraceEvent data = log.Events.First(e => e.CallStackIndex() != CallStackIndex.Invalid).Clone();
#pragma warning disable CS0618
                    long timestampQpc = data.TimeStampQPC;
#pragma warning restore CS0618
                    Assert.Throws<ArgumentNullException>(() => new AsyncProfilerCallStackComputer(null));
                    Assert.Throws<ArgumentNullException>(() => new AsyncProfilerCallStackSourceBuilder(null));
                    Assert.Throws<ArgumentNullException>(() => query.GetCallStacks(null));
                    Assert.Throws<ArgumentNullException>(() => builder.GetCallStack((TraceEvent)null));
                    using (var otherLog = new TraceLog(path))
                    {
                        Assert.Throws<ArgumentException>(() => new AsyncProfilerCallStackComputer(otherLog).GetCallStacks(data));
                        Assert.Throws<ArgumentException>(() =>
                            new AsyncProfilerCallStackSourceBuilder(new MutableTraceEventStackSource(otherLog)).GetCallStack(data));
                    }

                    IReadOnlyList<AsyncProfilerCallStack> segments = query.GetCallStacks(data);
                    AsyncProfilerCallStack retained = Assert.Single(segments);
                    AsyncProfilerCallStackFrame captured = retained.CapturedFrames[0];
                    Assert.Equal(retained.Frames.InstructionPointerAt(0), captured.CapturedInstructionPointer);
                    Assert.Equal(retained.Frames.CodeAddressAt(0), captured.CodeAddress);
                    Assert.Null(captured.CapturedState);
                    Assert.False(captured.IsPhysical);
                    Assert.False(captured.IsParentContext);
                    Assert.Empty(query.GetCallStacks(data.ProcessID, (ulong)data.ThreadID, retained.EndQpc));
                    Assert.Equal(captured.CapturedInstructionPointer, retained.CapturedFrames[0].CapturedInstructionPointer);

                    var order = new List<int>();
                    builder.Transforms.AddCustomTransform(context =>
                    {
                        order.Add(1);
                        Assert.Equal(timestampQpc, context.TimestampQpc);
                        Assert.Same(log, context.TraceLog);
                        Assert.Single(context.Segments);
                        Assert.DoesNotContain(context.Frames, frame =>
                            frame.CodeAddress != CodeAddressIndex.Invalid &&
                            log.CodeAddresses[frame.CodeAddress].FullMethodName.Contains("Continuation_Wrapper_"));
                        Assert.Throws<InvalidOperationException>(() => builder.GetCallStack(data));
                        context.Frames.Insert(0, new AsyncProfilerCallStackFrame("Custom label"));
                    });
                    builder.Transforms.AddCustomTransform(context =>
                    {
                        order.Add(2);
                        Assert.Equal("Custom label", context.Frames[0].DisplayName);
                        Assert.False(context.Frames[0].IsPhysical);
                        Assert.Equal(CodeAddressIndex.Invalid, context.Frames[0].CodeAddress);
                    });
                    StackSourceCallStackIndex first = builder.GetCallStack(data);
                    Assert.Equal(new[] { 1, 2 }, order);
                    Assert.Equal("Custom label", output.GetFrameName(output.GetFrameIndex(first), false));
                    Assert.Equal(0, output.SampleIndexLimit);
                    builder.Transforms.ClearCustomTransforms();
                    var root = output.Interner.CallStackIntern(output.Interner.FrameIntern("Custom root"),
                        StackSourceCallStackIndex.Invalid);
                    var next = builder.GetCallStack(data.CallStackIndex(), timestampQpc, root);
                    Assert.Equal(new[] { 1, 2 }, order);
                    Assert.NotEqual("Custom label", output.GetFrameName(output.GetFrameIndex(next), false));
                    while (output.GetCallerIndex(next) != StackSourceCallStackIndex.Invalid)
                    {
                        next = output.GetCallerIndex(next);
                    }
                    Assert.Equal("Custom root", output.GetFrameName(output.GetFrameIndex(next), false));
                    Assert.Equal("Custom label", output.GetFrameName(output.GetFrameIndex(first), false));
                    var fallback = builder.GetCallStack(data.CallStackIndex(), retained.EndQpc);
                    Assert.Equal(output.GetCallStackThread(data.CallStackIndex(), data.Thread()), fallback);
                    Assert.Equal(StackSourceCallStackIndex.Invalid,
                        builder.GetCallStack(CallStackIndex.Invalid, timestampQpc));
                    Assert.Throws<ArgumentOutOfRangeException>(() => builder.GetCallStack((CallStackIndex)int.MaxValue, timestampQpc));
                    Assert.Throws<ArgumentOutOfRangeException>(() =>
                        builder.GetCallStack(data.CallStackIndex(), timestampQpc, (StackSourceCallStackIndex)int.MaxValue));
                    output.AddSample(new StackSourceSample(output) { StackIndex = first, Metric = 3, TimeRelativeMSec = 2 });
                    output.AddSample(new StackSourceSample(output) { StackIndex = fallback, Metric = 7, TimeRelativeMSec = 1 });
                    output.DoneAddingSamples();
                    Assert.Equal("Custom label", output.GetFrameName(output.GetFrameIndex(first), false));
                    Assert.Throws<InvalidOperationException>(() => builder.GetCallStack(data));
                    Assert.Single(query.GetCallStacks(data));
                },
            };
            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void CoreBuilder_ClearResources_RejectsFurtherCallStacks(bool useEvent, bool hasPhysicalStack)
        {
            Frame current = Frame.App("Scenario.CurrentAsync");
            Frame parent = Frame.App("Scenario.ParentAsync");
            var scenario = new StitchScenario
            {
                Sync = hasPhysicalStack
                    ? new[] { current, Frame.V2Wrapper(0), Frame.V2InstrumentedDispatch,
                        Frame.V2Dispatch, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart }
                    : Array.Empty<Frame>(),
                Async = new[] { current, parent },
                SampleHasStack = hasPhysicalStack,
                ExpectedStitched = hasPhysicalStack
                    ? new[] { current, parent, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart }
                    : Array.Empty<Frame>(),
                ExpectedDiagnostics = hasPhysicalStack
                    ? new CallStackDiagnosticsExpectation
                    {
                        SegmentsProcessed = 2,
                        V2InfrastructureFramesCollapsed = 4,
                    }
                    : new CallStackDiagnosticsExpectation(),
                AssertPublicApi = (log, path) =>
                {
                    TraceEvent data = log.Events.First(e =>
                        (e.CallStackIndex() != CallStackIndex.Invalid) == hasPhysicalStack).Clone();
                    Assert.Equal(hasPhysicalStack,
                        data.TryGetCallStackIndex(out CallStackIndex physicalStack, out long timestampQpc));

                    var output = new MutableTraceEventStackSource(log);
                    var core = AsyncProfilerCallStackSourceBuilderCore.TryCreate(
                        log, output, new AsyncProfilerCallStackTransformPipeline(),
                        (eventData, thread) => output.GetCallStackForThread(thread));
                    Assert.NotNull(core);
                    core.ClearResources();

                    if (useEvent)
                    {
                        Assert.Throws<InvalidOperationException>(() =>
                            core.TryGetCallStack(data, data.Thread(), out _));
                    }
                    else
                    {
                        Assert.Throws<InvalidOperationException>(() =>
                            core.TryGetCallStack(physicalStack, timestampQpc, data.Thread(),
                                StackSourceCallStackIndex.Invalid, out _));
                    }
                },
            };
            scenario.AssertProductionStitch();
        }

        [Fact]
        public void PublicBuilder_CompletionObservationsSurviveReusedCallbackSegments()
        {
            Frame current = Frame.App("Scenario.CurrentAsync");
            Frame parent = Frame.App("Scenario.ParentAsync");
            var scenario = new StitchScenario
            {
                Sync = new[] { current, Frame.V2Wrapper(0), Frame.V2InstrumentedDispatch,
                    Frame.V2Dispatch, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart },
                Async = new[] { current, parent },
                MethodCompletions = 1,
                MethodCompletionQpc = 100_035,
                ExpectedStitched = new[] { current, parent, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V2InfrastructureFramesCollapsed = 4,
                },
                AssertPublicApi = (log, path) =>
                {
                    var query = new AsyncProfilerCallStackComputer(log);
                    var builder = new AsyncProfilerCallStackSourceBuilder(new MutableTraceEventStackSource(log));
                    int callbacks = 0;
                    builder.Transforms.AddCustomTransform(context =>
                    {
                        AsyncProfilerCallStack callStack = Assert.Single(context.Segments);
                        Assert.True(callStack.MethodCompletionObserved);
                        Assert.False(callStack.ExceptionCompletionObserved);
                        Assert.Equal(0, callStack.GetMethodCompletedFrameCount(context.TimestampQpc));
                        Assert.Contains(context.Frames, frame => ReferenceEquals(frame.CallStack, callStack));
                        callbacks++;
                    });
                    foreach (TraceEvent sample in log.Events.Where(e => e.CallStackIndex() != CallStackIndex.Invalid))
                    {
                        AsyncProfilerCallStack callStack = Assert.Single(query.GetCallStacks(sample));
                        Assert.True(callStack.MethodCompletionObserved);
                        Assert.False(callStack.ExceptionCompletionObserved);
                        Assert.NotEqual(StackSourceCallStackIndex.Invalid, builder.GetCallStack(sample));
                    }
                    Assert.Equal(2, callbacks);
                },
            };
            scenario.AssertProductionStitch();
        }

        [Fact]
        public void PublicCallbacks_PrepareCompletionObservationsOncePerSample()
        {
            Frame current = Frame.App("Scenario.CurrentAsync");
            Frame parent = Frame.App("Scenario.ParentAsync");
            var scenario = new StitchScenario
            {
                Sync = new[] { current, Frame.V2Wrapper(0), Frame.V2InstrumentedDispatch,
                    Frame.V2Dispatch, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart },
                Async = new[] { current, parent },
                ExpectedStitched = new[] { current, parent, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V2InfrastructureFramesCollapsed = 4,
                },
                AssertPublicApi = (log, path) =>
                {
                    TraceEvent sample = log.Events.First(e => e.CallStackIndex() != CallStackIndex.Invalid);
                    var segments = new CountingCallStacks(new AsyncProfilerCallStackComputer(log).GetCallStacks(sample));
                    var pipeline = new AsyncProfilerCallStackTransformPipeline { EnableNormalization = false };
                    var transforms = new AsyncCallStackTransforms(pipeline);
                    var state = new AsyncProfilerCallStackTransformState(
                        log, segments, 0, _ => AsyncProfilerCallStackStitchBoundaryInfo.None,
                        new AsyncProfilerCallStackStitchingState(), new AsyncProfilerCallStackDiagnostics(), new List<AsyncProfilerStitchedFrame>(),
                        runtimeSupport: AsyncProfilerRuntimeSupport.Common);
                    segments.ReadCount = 0;
                    pipeline.ApplyInPlace(state);
                    Assert.Equal(0, segments.ReadCount);

                    int callbacks = 0;
                    transforms.AddCustomTransform(_ => callbacks++);
                    transforms.AddCustomTransform(_ => callbacks++);
                    pipeline.ApplyInPlace(state);
                    Assert.Equal(segments.Count, segments.ReadCount);
                    Assert.Equal(2, callbacks);

                    segments.ReadCount = 0;
                    transforms.AddCustomTransform(_ => callbacks++);
                    pipeline.ApplyInPlace(state);
                    Assert.Equal(segments.Count, segments.ReadCount);
                    Assert.Equal(5, callbacks);

                    segments.ReadCount = 0;
                    transforms.ClearCustomTransforms();
                    pipeline.ApplyInPlace(state);
                    Assert.Equal(0, segments.ReadCount);
                    Assert.Equal(5, callbacks);

                    transforms.AddCustomTransform(_ => callbacks++);
                    pipeline.ApplyInPlace(state);
                    Assert.Equal(segments.Count, segments.ReadCount);
                    Assert.Equal(6, callbacks);
                },
            };
            scenario.AssertProductionStitch();
        }

        [Fact]
        public void PublicBuilder_NoAsyncIndex_PreservesPhysicalInput()
        {
            var scenario = new StitchScenario
            {
                Sync = new[] { Frame.App("Scenario.Work"), Frame.ThreadPoolDispatch, Frame.WorkerThreadStart },
                Async = Array.Empty<Frame>(),
                ExpectedStitched = Array.Empty<Frame>(),
            };
            scenario.AssertPhysicalFallback(includeAsyncData: false);
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 0)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 1)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, 2)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 0)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 1)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, 2)]
        public void PublicBuilder_EmptyAsyncSegment_PreservesPhysicalInput(int kindValue, int emptyPosition)
        {
            var kind = (AsyncCallstackKind)kindValue;
            Frame child = Frame.App("Scenario+<ChildAsync>d__7.MoveNext()");
            Frame current = Frame.App("Scenario.CurrentAsync");
            var empty = new AsyncSegment(kind, Array.Empty<Frame>());
            var populated = new AsyncSegment(kind, new[] { current });
            var scenario = new StitchScenario
            {
                Sync = new[]
                {
                    child, Frame.CoreLib("System.Runtime.CompilerServices.AsyncTaskMethodBuilder.Start", "Builder.Start"),
                    current, Frame.V2Wrapper(0), Frame.V2InstrumentedDispatch, Frame.V2Dispatch,
                    Frame.ThreadPoolDispatch, Frame.WorkerThreadStart
                },
                AsyncSegments = emptyPosition == 0 ? new[] { empty } :
                    emptyPosition == 1 ? new[] { empty, populated } : new[] { populated, empty },
                ExpectedStitched = Array.Empty<Frame>(),
            };
            scenario.AssertPhysicalFallback(includeAsyncData: true);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PublicBuilder_NormalizedSynchronousFrames_PreserveCodeAddressIdentity(bool annotate)
        {
            Frame child = Frame.App("Scenario+<ChildAsync>d__7.MoveNext()");
            Frame anotherChild = Frame.PerfMap(Frame.ApplicationModule,
                "Scenario+<ChildAsync>d__7::MoveNext()", "Another physical ChildAsync");
            Frame start = Frame.CoreLib("System.Runtime.CompilerServices.AsyncTaskMethodBuilder.Start", "Builder.Start");
            Frame current = Frame.App("Scenario.CurrentAsync");
            Frame parent = Frame.App("Scenario.ParentAsync");
            void AssertAliases(MutableTraceEventStackSource output, StackSourceCallStackIndex stack)
            {
                TraceEvent sample = output.TraceLog.Events.First(data => data.CallStackIndex() != CallStackIndex.Invalid);
                CallStackIndex physical = sample.CallStackIndex();
                var expected = new[]
                {
                    output.TraceLog.CallStacks.CodeAddressIndex(physical),
                    output.TraceLog.CallStacks.CodeAddressIndex(
                        output.TraceLog.CallStacks.Caller(output.TraceLog.CallStacks.Caller(physical)))
                };
                var aliases = new List<StackSourceFrameIndex>();
                while (stack != StackSourceCallStackIndex.Invalid)
                {
                    StackSourceFrameIndex frame = output.GetFrameIndex(stack);
                    if (output.GetFrameName(frame, false).Contains("Scenario.ChildAsync [ordinal 7]"))
                    {
                        aliases.Add(frame);
                    }
                    stack = output.GetCallerIndex(stack);
                }
                Assert.Equal(2, aliases.Count);
                Assert.NotEqual(aliases[0], aliases[1]);
                Assert.Equal(expected, aliases.Select(output.GetFrameCodeAddress));
                Assert.All(aliases, frame => Assert.False(output.TryGetAsyncProfilerCallStackFrameInfo(frame, out _)));
            }
            var scenario = new StitchScenario
            {
                Sync = new[] { child, start, anotherChild, start, current, Frame.V2Wrapper(0),
                    Frame.V2InstrumentedDispatch, Frame.V2Dispatch, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart },
                Async = new[] { current, parent },
                ExpectedStitched = new[] { child.Logical("Scenario.ChildAsync"), anotherChild.Logical("Scenario.ChildAsync"),
                    current, parent, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart },
                EnableFrameAnnotations = annotate,
                IncludeEventSourceEvents = false,
                VerifyPublicBuilder = true,
                VerifyThreadTimeComputer = true,
                AllowSupplementalSamples = true,
                VerifySampleWeights = true,
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V2InfrastructureFramesCollapsed = 4,
                    V1SynchronousMoveNextFramesNormalized = 4,
                    V1SynchronousStartupFramesCollapsed = 4,
                },
                AssertStitchedStackSource = output => output.ForEach(sample => AssertAliases(output, sample.StackIndex)),
                AssertPublicApi = (log, path) =>
                {
                    var output = new MutableTraceEventStackSource(log);
                    var builder = new AsyncProfilerCallStackSourceBuilder(output);
                    builder.Transforms.EnableFrameAnnotations = annotate;
                    TraceEvent sample = log.Events.First(data => data.CallStackIndex() != CallStackIndex.Invalid);
                    StackSourceCallStackIndex stack = builder.GetCallStack(sample);
                    AssertAliases(output, stack);
                    Assert.Equal(stack, builder.GetCallStack(sample));
                    output.DoneAddingSamples();
                    AssertAliases(output, stack);
                },
            };
            scenario.AssertProductionStitch();
        }

        #region private

        private sealed class CountingCallStacks : IReadOnlyList<AsyncProfilerCallStack>
        {
            public CountingCallStacks(IReadOnlyList<AsyncProfilerCallStack> callStacks)
            {
                m_callStacks = callStacks;
            }
            public int Count => m_callStacks.Count;
            public int ReadCount { get; set; }
            public AsyncProfilerCallStack this[int index]
            {
                get
                {
                    ReadCount++;
                    return m_callStacks[index];
                }
            }
            public IEnumerator<AsyncProfilerCallStack> GetEnumerator()
            {
                for (int i = 0; i < Count; i++)
                {
                    yield return this[i];
                }
            }
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            #region private

            private readonly IReadOnlyList<AsyncProfilerCallStack> m_callStacks;

            #endregion
        }

        #endregion
    }
}
