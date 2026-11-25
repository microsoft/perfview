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
        public void RuntimeAsync_ResumeStub_IsConsumedStructurallyWithHonestBodyProvenance(
            bool sampledStub, bool enableNormalization)
        {
            Frame work = Frame.App("Scenario.Work");
            Frame current = Frame.App("Scenario.CurrentAsync");
            Frame parent = Frame.App("Scenario.ParentAsync");
            Frame stub = Frame.App("dynamicClass.IL_STUB_AsyncResume(class System.Object,unsigned int8&)");
            Frame[] entry = { stub, Frame.V2Wrapper(0), Frame.V2InstrumentedDispatch,
                Frame.V2Dispatch, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart };
            var scenario = new StitchScenario
            {
                Sync = sampledStub ? entry : new[] { work, current }.Concat(entry).ToArray(),
                Async = new[] { current, parent },
                ExpectedStitched = (sampledStub ? Array.Empty<Frame>() : new[] { work })
                    .Concat(new[] { current, parent, Frame.ThreadPoolDispatch, Frame.WorkerThreadStart }).ToArray(),
                EnableNormalization = enableNormalization,
                EnableFrameAnnotations = true,
                VerifySampleWeights = true,
                ConfigureTraceLog = log => ConfigureCapturedRuntime(log, "12.0.0"),
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V2InfrastructureFramesCollapsed = 6,
                },
                AssertTransformContext = context =>
                {
                    AsyncProfilerStitchedFrame anchor = Assert.Single(context.Frames.Where(frame =>
                        frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent ||
                        frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget));
                    Assert.Equal(sampledStub ? AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget :
                        AsyncProfilerStitchedFrameOrigin.AsyncCurrent, anchor.Origin);
                    Assert.Equal(0, anchor.SegmentFrameIndex);
                    Assert.DoesNotContain(context.Frames, frame => frame.SyncFrameKind == AsyncProfilerStitchSyncFrameKind.V2ResumeStub);
                },
            };
            scenario.AssertProductionStitch();
        }

        [Fact]
        public void RuntimeAsync_ThreadPoolDispatch_StitchesSuspendedCallers()
        {
            var scenario = new StitchScenario
            {
                Sync = new[]
                {
                    Frame.App("Scenario.DoWork"),
                    Frame.App("Scenario.Level3Async"),
                    Frame.V2Wrapper(0),
                    Frame.V2InstrumentedDispatch,
                    Frame.V2Dispatch,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                Async = new[]
                {
                    Frame.App("Scenario.Level3Async"),
                    Frame.App("Scenario.Level2Async"),
                    Frame.App("Scenario.Level1Async"),
                    Frame.App("Program.Main"),
                },
                ExpectedStitched = new[]
                {
                    Frame.App("Scenario.DoWork"),
                    Frame.App("Scenario.Level3Async"),
                    Frame.App("Scenario.Level2Async"),
                    Frame.App("Scenario.Level1Async"),
                    Frame.App("Program.Main"),
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    // Both incoming CPU samples are stitched; the second sample causes the first sample's
                    // thread-time interval to be emitted.
                    SegmentsProcessed = 2,
                    V2InfrastructureFramesCollapsed = 4,
                },
                VerifyComputerAndIndexLifecycle = true,
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void RuntimeAsync_ParentContexts_SkipZeroIpSlotsAndNameUnresolvedFrames(
            bool enableFrameAnnotations, bool unresolvedParent)
        {
            Frame current = Frame.App("Scenario.ChildAsync");
            Frame ownerCurrent = Frame.App("Scenario.OwnerCurrentAsync");
            Frame ownerRoot = Frame.App("Scenario.OwnerRootAsync");
            var scenario = new StitchScenario
            {
                Sync = new[]
                {
                    Frame.App("Scenario.DoWork"),
                    current,
                    Frame.V2Wrapper(0),
                    Frame.V2InstrumentedDispatch,
                    Frame.V2Dispatch,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                Async = new[] { current },
                ContextParent = AsyncSegment.Runtime(Frame.Zero, ownerCurrent, Frame.Zero, ownerRoot),
                OmitSymbolFor = unresolvedParent ? ownerRoot : null,
                EnableParentContexts = true,
                EnableFrameAnnotations = enableFrameAnnotations,
                ExpectedStitched = new[]
                {
                    Frame.App("Scenario.DoWork"),
                    current,
                    ownerCurrent,
                    ownerRoot,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V2InfrastructureFramesCollapsed = 4,
                },
                AssertStitchedStackSource = stackSource =>
                {
                    for (int i = 0; i < stackSource.CallFrameIndexLimit; i++)
                    {
                        string name = stackSource.GetFrameName((StackSourceFrameIndex)i, false);
                        Assert.DoesNotContain("AsyncFrame(0x0)", name);
                        Assert.False(name.EndsWith("!", StringComparison.Ordinal), name);
                    }
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RuntimeAsync_ParentContexts_RendersParentContextMetadata(bool enableFrameAnnotations)
        {
            Frame current = Frame.App("Scenario.ChildAsync");
            Frame childParent = Frame.App("Scenario.ChildParentAsync");
            Frame ownerCurrent = Frame.App("Scenario.OwnerCurrentAsync");
            Frame ownerRoot = Frame.App("Scenario.OwnerRootAsync");
            var scenario = new StitchScenario
            {
                Sync = new[]
                {
                    Frame.App("Scenario.DoWork"),
                    current,
                    Frame.V2Wrapper(0),
                    Frame.V2InstrumentedDispatch,
                    Frame.V2Dispatch,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                Async = new[]
                {
                    current,
                    childParent,
                },
                ContextParent = AsyncSegment.Runtime(ownerCurrent, ownerRoot),
                EnableParentContexts = true,
                EnableFrameAnnotations = enableFrameAnnotations,
                VerifyRemovingParentContextRemovesPresentation = true,
                ExpectedStitched = new[]
                {
                    Frame.App("Scenario.DoWork"),
                    current,
                    childParent,
                    ownerCurrent,
                    ownerRoot,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V2InfrastructureFramesCollapsed = 4,
                },
                AssertOutputs = (normal, stitched) =>
                {
                    Assert.Equal(
                        new[]
                        {
                            "[Async resumed parent context]",
                            "[Async resumed parent context]",
                        },
                        stitched.GroupingFrames.Where(
                            frame => string.Equals(
                                frame,
                                "[Async resumed parent context]",
                                StringComparison.Ordinal)));
                },
                AssertStitchedStackSource = stackSource =>
                {
                    bool foundParentContext = false;
                    bool foundActiveAsync = false;
                    stackSource.ForEach(sample =>
                    {
                        for (StackSourceCallStackIndex stack = sample.StackIndex;
                             stack != StackSourceCallStackIndex.Invalid;
                             stack = stackSource.GetCallerIndex(stack))
                        {
                            StackSourceFrameIndex frameIndex = stackSource.GetFrameIndex(stack);
                            string name = stackSource.GetFrameName(frameIndex, false);
                            if (!stackSource.TryGetAsyncProfilerCallStackFrameInfo(
                                frameIndex, out AsyncProfilerCallStackSourceFrameInfo info))
                            {
                                continue;
                            }

                            if (info.IsParentContext || enableFrameAnnotations)
                            {
                                Assert.StartsWith("[Async V2] ", name);
                                Assert.DoesNotContain("![Async V2] ", name);
                            }
                            else
                            {
                                Assert.DoesNotContain("[Async V2] ", name);
                            }
                            foundParentContext |= info.IsParentContext;
                            foundActiveAsync |= !info.IsParentContext;
                        }
                    });
                    Assert.True(foundParentContext);
                    Assert.True(foundActiveAsync);
                },
            };

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void RuntimeAsync_ActiveAnnotations_DoNotAddParentTransitions()
        {
            Frame current = Frame.App("Scenario.CurrentAsync");
            Frame parent = Frame.App("Scenario.ParentAsync");
            var scenario = new StitchScenario
            {
                Sync = new[]
                {
                    Frame.App("Scenario.DoWork"),
                    current,
                    Frame.V2Wrapper(0),
                    Frame.V2InstrumentedDispatch,
                    Frame.V2Dispatch,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                Async = new[] { current, parent },
                EnableFrameAnnotations = true,
                ExpectedStitched = new[]
                {
                    Frame.App("Scenario.DoWork"),
                    current,
                    parent,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V2InfrastructureFramesCollapsed = 4,
                },
                AssertOutputs = (normal, stitched) =>
                    Assert.DoesNotContain(
                        "[Async resumed parent context]",
                        stitched.GroupingFrames),
                AssertStitchedStackSource = stackSource =>
                {
                    bool foundAnnotatedCurrent = false;
                    bool foundUnannotatedSyncCallee = false;
                    stackSource.ForEach(sample =>
                    {
                        for (StackSourceCallStackIndex stack = sample.StackIndex;
                             stack != StackSourceCallStackIndex.Invalid;
                             stack = stackSource.GetCallerIndex(stack))
                        {
                            StackSourceFrameIndex frameIndex = stackSource.GetFrameIndex(stack);
                            string name = stackSource.GetFrameName(frameIndex, false);
                            if (name.IndexOf("Scenario.DoWork", StringComparison.Ordinal) >= 0)
                            {
                                Assert.DoesNotContain("[Async V2] ", name);
                                foundUnannotatedSyncCallee = true;
                            }

                            if (name.IndexOf("[Async V2] ", StringComparison.Ordinal) < 0)
                            {
                                continue;
                            }

                            Assert.StartsWith("[Async V2] ", name);
                            Assert.DoesNotContain("![Async V2] ", name);
                            if (name.IndexOf("Scenario.CurrentAsync", StringComparison.Ordinal) >= 0)
                            {
                                foundAnnotatedCurrent = true;
                            }
                            Assert.True(stackSource.TryGetAsyncProfilerCallStackFrameInfo(
                                frameIndex, out AsyncProfilerCallStackSourceFrameInfo info));
                            Assert.False(info.IsParentContext);
                        }
                    });
                    Assert.True(foundAnnotatedCurrent);
                    Assert.True(foundUnannotatedSyncCallee);
                },
            };

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void RuntimeAsync_IdenticalRecursiveContextParent_IsRendered()
        {
            Frame current = Frame.App("Scenario.CurrentAsync");
            Frame parent = Frame.App("Scenario.ParentAsync");
            var scenario = new StitchScenario
            {
                Sync = new[]
                {
                    Frame.App("Scenario.DoWork"),
                    current,
                    Frame.V2Wrapper(0),
                    Frame.V2InstrumentedDispatch,
                    Frame.V2Dispatch,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                Async = new[] { current, parent },
                ContextParent = AsyncSegment.Runtime(current, parent),
                EnableParentContexts = true,
                EnableFrameAnnotations = true,
                ExpectedStitched = new[]
                {
                    Frame.App("Scenario.DoWork"),
                    current,
                    parent,
                    current,
                    parent,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V2InfrastructureFramesCollapsed = 4,
                },
                AssertOutputs = (normal, stitched) =>
                {
                    Assert.Contains(
                        "[Async resumed parent context]",
                        stitched.GroupingFrames);
                },
                AssertStitchedStackSource = stackSource =>
                {
                    stackSource.ForEach(sample =>
                    {
                        StackSourceFrameIndex activeParent = StackSourceFrameIndex.Invalid;
                        StackSourceFrameIndex parentContext = StackSourceFrameIndex.Invalid;
                        for (StackSourceCallStackIndex stack = sample.StackIndex;
                             stack != StackSourceCallStackIndex.Invalid;
                             stack = stackSource.GetCallerIndex(stack))
                        {
                            StackSourceFrameIndex frameIndex = stackSource.GetFrameIndex(stack);
                            if (stackSource.GetFrameName(frameIndex, false).IndexOf(
                                "Scenario.ParentAsync", StringComparison.Ordinal) < 0 ||
                                !stackSource.TryGetAsyncProfilerCallStackFrameInfo(
                                    frameIndex, out AsyncProfilerCallStackSourceFrameInfo info))
                            {
                                continue;
                            }
                            if (info.IsParentContext)
                            {
                                parentContext = frameIndex;
                            }
                            else
                            {
                                activeParent = frameIndex;
                            }
                        }
                        Assert.NotEqual(StackSourceFrameIndex.Invalid, activeParent);
                        Assert.NotEqual(StackSourceFrameIndex.Invalid, parentContext);
                        Assert.NotEqual(activeParent, parentContext);
                        Assert.Equal(
                            stackSource.GetFrameName(activeParent, false),
                            stackSource.GetFrameName(parentContext, false));
                    });
                },
            };

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void RuntimeAsync_ReplacesTplReconstructionAndPreservesStartStopGrouping()
        {
            Frame taskExecute = Frame.CoreLib("System.Threading.Tasks.Task.Execute", "Task.Execute");
            var scenario = new StitchScenario
            {
                Sync = new[]
                {
                    Frame.App("Scenario.DoWork"),
                    Frame.App("Scenario.CurrentAsync"),
                    Frame.V2Wrapper(0),
                    Frame.V2InstrumentedDispatch,
                    Frame.V2Dispatch,
                    taskExecute,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                Async = new[]
                {
                    Frame.App("Scenario.CurrentAsync"),
                    Frame.App("Scenario.ParentAsync"),
                    Frame.App("Program.Main"),
                },
                ExpectedStitched = new[]
                {
                    Frame.App("Scenario.DoWork"),
                    Frame.App("Scenario.CurrentAsync"),
                    Frame.App("Scenario.ParentAsync"),
                    Frame.App("Program.Main"),
                    taskExecute,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V2InfrastructureFramesCollapsed = 4,
                },
                IncludeTplActivity = true,
                IncludeStartStopActivity = true,
                IncludeEventSourceEvents = false,
                AllowSupplementalSamples = true,
                SkipNormalStackAssertion = true,
                VerifyPublicBuilder = true,
                VerifyThreadTimeComputer = true,
                AssertOutputs = (normal, stitched) =>
                {
                    Assert.Contains(normal.GroupingFrames, name =>
                        name.IndexOf("STARTING TASK", StringComparison.Ordinal) >= 0);
                    Assert.DoesNotContain(stitched.GroupingFrames, name =>
                        name.IndexOf("STARTING TASK", StringComparison.Ordinal) >= 0);
                    Assert.Contains(normal.RootFrames, name =>
                        name.IndexOf("Activity Request", StringComparison.Ordinal) >= 0);
                    Assert.Contains(stitched.RootFrames, name =>
                        name.IndexOf("Activity Request", StringComparison.Ordinal) >= 0);
                },
            };

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void RuntimeAsync_SameIpAsSyncFrame_RetainsDistinctFrameIdentity()
        {
            Frame repeated = Frame.App("Scenario.RepeatedWork");
            Frame current = Frame.App("Scenario.CurrentAsync");
            var scenario = new StitchScenario
            {
                Sync = new[]
                {
                    repeated,
                    current,
                    Frame.V2Wrapper(0),
                    Frame.V2InstrumentedDispatch,
                    Frame.V2Dispatch,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                Async = new[]
                {
                    current,
                    repeated,
                    Frame.App("Program.Main"),
                },
                ExpectedStitched = new[]
                {
                    repeated,
                    current,
                    repeated,
                    Frame.App("Program.Main"),
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V2InfrastructureFramesCollapsed = 4,
                },
                AssertStitchedStackSource = stackSource =>
                {
                    var repeatedFrames =
                        new List<(StackSourceFrameIndex Index, CodeAddressIndex CodeAddress, bool IsAsync)>();
                    stackSource.ForEach(sample =>
                    {
                        StackSourceCallStackIndex stackIndex = sample.StackIndex;
                        while (stackIndex != StackSourceCallStackIndex.Invalid)
                        {
                            StackSourceFrameIndex frameIndex = stackSource.GetFrameIndex(stackIndex);
                            string name = stackSource.GetFrameName(frameIndex, false);
                            if (name.Contains(repeated.SymbolName))
                            {
                                bool isAsync = stackSource.TryGetAsyncProfilerCallStackFrameInfo(
                                    frameIndex, out AsyncProfilerCallStackSourceFrameInfo info);
                                repeatedFrames.Add(
                                    (frameIndex, stackSource.GetFrameCodeAddress(frameIndex), isAsync));
                                if (isAsync)
                                {
                                    Assert.Equal(AsyncCallstackKind.RuntimeAsync, info.Kind);
                                    Assert.Equal(0, info.State);
                                }
                            }
                            stackIndex = stackSource.GetCallerIndex(stackIndex);
                        }
                    });

                    Assert.Equal(2, repeatedFrames.Count);
                    Assert.NotEqual(repeatedFrames[0].Index, repeatedFrames[1].Index);
                    Assert.Equal(repeatedFrames[0].CodeAddress, repeatedFrames[1].CodeAddress);
                    Assert.Single(repeatedFrames.Where(frame => frame.IsAsync));
                    Assert.Single(repeatedFrames.Where(frame => !frame.IsAsync));
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(3)]
        public void RuntimeAsync_DeepResume_UsesWrapperSlotToExcludeCompletedFrames(int completedFrames)
        {
            Frame[] leaf =
            {
                Frame.Library("System.Console", "Interop+Kernel32.ReadConsoleInput", "Interop.Kernel32.ReadConsoleInput"),
                Frame.Library("System.Console", "System.ConsolePal.ReadKey", "ConsolePal.ReadKey"),
                Frame.Library("System.Console", "System.Console.ReadKey", "Console.ReadKey"),
            };
            Frame[] async =
            {
                Frame.App("TestCase8.Level4Async"),
                Frame.App("TestCase8.Level3Async"),
                Frame.App("TestCase8.Level2Async"),
                Frame.App("TestCase8.Level1Async"),
                Frame.App("TestCase8.Run"),
                Frame.App("Program.Main"),
            };
            Frame[] root =
            {
                Frame.ThreadPoolDispatch,
                Frame.CoreLib("System.Threading.PortableThreadPool+WorkerThread.WorkerDoWork", "WorkerThread.WorkerDoWork"),
                Frame.WorkerThreadStart,
                Frame.CoreLib("System.Threading.Thread.StartCallback", "Thread.StartCallback"),
            };

            var scenario = new StitchScenario
            {
                Sync = leaf.Concat(new[]
                {
                    async[completedFrames],
                    Frame.V2Wrapper(completedFrames),
                    Frame.V2InstrumentedDispatch,
                    Frame.V2Dispatch,
                }).Concat(root).ToArray(),
                Async = async,
                ContinuationIndexBase = 0,
                ExpectedStitched = leaf.Concat(async.Skip(completedFrames)).Concat(root).ToArray(),
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V2InfrastructureFramesCollapsed = 4,
                },
            };

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void RuntimeAsync_MissingNativeStack_FallsBackWithoutStitching()
        {
            var scenario = new StitchScenario
            {
                Sync = Array.Empty<Frame>(),
                Async = new[]
                {
                    Frame.App("Scenario.Level3Async"),
                    Frame.App("Scenario.Level2Async"),
                    Frame.App("Scenario.Level1Async"),
                    Frame.App("Program.Main"),
                },
                ExpectedStitched = Array.Empty<Frame>(),
                SampleHasStack = false,
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, false, true)]
        [InlineData(false, true, false)]
        [InlineData(false, true, true)]
        [InlineData(true, false, false)]
        [InlineData(true, false, true)]
        [InlineData(true, true, false)]
        [InlineData(true, true, true)]
        public void RuntimeAsync_WrapperIsSampledLeaf_ProjectsDispatchTarget(
            bool enableFrameAnnotations, bool enableNormalization, bool enableInfrastructureCleanup)
        {
            Frame[] logical =
            {
                Frame.App("Scenario.Level3Async"),
                Frame.App("Scenario.Level2Async"),
                Frame.App("Program.Main"),
            };
            Frame[] root = { Frame.ThreadPoolDispatch, Frame.WorkerThreadStart };
            var scenario = new StitchScenario
            {
                Sync = new[]
                {
                    Frame.V2Wrapper(0),
                    Frame.V2InstrumentedDispatch,
                    Frame.V2Dispatch,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                Async = logical,
                ExpectedStitched = logical.Concat(root).ToArray(),
                EnableFrameAnnotations = enableFrameAnnotations,
                EnableNormalization = enableNormalization,
                EnableInfrastructureCleanup = enableInfrastructureCleanup,
                EnableParentContexts = true,
                VerifySampleWeights = true,
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V2InfrastructureFramesCollapsed = 4,
                    V2LeafWrapperDropped = 2,
                },
                AssertTransformContext = context =>
                {
                    AsyncProfilerStitchedFrame target = Assert.Single(context.Frames.Where(frame =>
                        frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget));
                    Assert.Equal(0, target.SegmentFrameIndex);
                    Assert.Equal(AsyncProfilerStitchedFramePresentation.Native, target.Presentation);
                    Assert.False(target.IsStateMachineBoxCarrier);
                    Assert.Same(context.Segments[0], target.Context);
                    Assert.DoesNotContain(context.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
                    Assert.Equal(0,
                        context.Frames.Count(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.Sync &&
                            context.ClassifyBoundary(frame.CodeAddress).Kind ==
                                AsyncProfilerCallStackStitchBoundaryKind.V2Dispatcher));
                    Assert.Equal(0,
                        context.Frames.Count(frame =>
                            frame.Presentation == AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames));
                },
                AssertStitchedStackSource = stackSource =>
                {
                    int targets = 0;
                    for (int i = 0; i < stackSource.CallFrameIndexLimit; i++)
                    {
                        var frameIndex = (StackSourceFrameIndex)i;
                        if (!stackSource.TryGetAsyncProfilerCallStackFrameInfo(frameIndex, out AsyncProfilerCallStackSourceFrameInfo info) ||
                            !info.IsDispatchTarget)
                        {
                            continue;
                        }
                        targets++;
                        Assert.Equal(AsyncCallstackKind.RuntimeAsync, info.Kind);
                        Assert.False(info.IsCurrent);
                        Assert.False(info.IsCompleting);
                        Assert.False(info.IsStateMachineBoxCarrier);
                        Assert.False(info.IsParentContext);
                        Assert.NotEqual(0UL, info.MethodId);
                        Assert.Equal(info.CodeAddress, stackSource.GetFrameCodeAddress(frameIndex));
                        string name = stackSource.GetFrameName(frameIndex, false);
                        Assert.Contains("testapp!Scenario.Level3Async", name);
                        Assert.Equal(enableFrameAnnotations, name.StartsWith("[Async V2] ", StringComparison.Ordinal));
                        Assert.DoesNotContain("state ", name);
                    }
                    Assert.Equal(1, targets);
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void RuntimeAsync_ZeroIpInitialEntry_IsStructuralAndPreservesNativeCallees(
            bool enableNormalization, bool enableInfrastructureCleanup)
        {
            Frame native = Frame.Library("coreclr", "Runtime.NativeWork", "NativeWork");
            Frame entry = Frame.CoreLib(
                "System.Runtime.CompilerServices.RuntimeAsyncTaskContinuation+TaskContinuationResume.ResumeTaskContinuation",
                "ResumeTaskContinuation");
            Frame[] tail =
            {
                Frame.CoreLib("System.Threading.Tasks.Task.RunContinuations", "Task.RunContinuations"),
                Frame.CoreLib("System.Threading.Tasks.Task`1.TrySetResult", "Task.TrySetResult"),
                Frame.V2InstrumentedDispatch, Frame.V2Dispatch,
                Frame.ThreadPoolDispatch, Frame.WorkerThreadStart,
            };
            var scenario = new StitchScenario
            {
                Sync = new[] { native, entry, Frame.V2Wrapper(0), Frame.V2InstrumentedDispatch, Frame.V2Dispatch }
                    .Concat(tail).ToArray(),
                Async = new[] { Frame.Zero, Frame.App("Scenario.ParallelWhenAllAsync") },
                ContextParent = AsyncSegment.Runtime(Frame.App("Scenario.OwnerAsync")),
                ExpectedStitched = new[] { native, entry.DispatcherFrames() }.Concat(tail).ToArray(),
                EnableNormalization = enableNormalization,
                EnableInfrastructureCleanup = enableInfrastructureCleanup,
                EnableFrameAnnotations = true,
                EnableParentContexts = true,
                VerifySampleWeights = true,
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V2InfrastructureFramesCollapsed = 4,
                },
                AssertTransformContext = context =>
                {
                    Assert.All(context.Frames, frame =>
                        Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, frame.Origin));
                    AsyncProfilerStitchedFrame marker = Assert.Single(context.Frames.Where(frame =>
                        frame.Presentation == AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames));
                    Assert.NotEqual(CodeAddressIndex.Invalid, marker.CodeAddress);
                    Assert.NotEqual(MethodIndex.Invalid, marker.Method);
                    Assert.Same(context.Segments[0], marker.Context);
                    Assert.Equal(0, marker.Context.GetCompletedFrameCount(context.TimestampQpc));
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(2, 3, 0, 0, 0, 1)]
        [InlineData(0, 0, 1, 0, 0, 1)]
        [InlineData(0, 2, 0, 2, 0, 2)]
        [InlineData(0, 0, 1, 1, 0, 2)]
        [InlineData(0, 1, 0, 0, 1, 33)]
        public void RuntimeAsync_WrapperIsSampledLeaf_UsesCallStackProgress(
            byte continuationBase, int slot, int methodCompletions, int exceptionCompletions,
            int wrapperResets, int expectedIndex)
        {
            Frame[] logical = Enumerable.Range(0, 36)
                .Select(i => Frame.App("Scenario.Level" + i + "Async")).ToArray();
            var scenario = new StitchScenario
            {
                Sync = new[]
                {
                    Frame.V2Wrapper(slot), Frame.V2InstrumentedDispatch, Frame.V2Dispatch,
                    Frame.ThreadPoolDispatch, Frame.WorkerThreadStart,
                },
                Async = logical,
                ContinuationIndexBase = continuationBase,
                MethodCompletions = methodCompletions,
                ExceptionCompletions = exceptionCompletions,
                WrapperResets = wrapperResets,
                ExpectedStitched = logical.Skip(expectedIndex)
                    .Concat(new[] { Frame.ThreadPoolDispatch, Frame.WorkerThreadStart }).ToArray(),
                EnableFrameAnnotations = true,
                VerifySampleWeights = true,
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V2InfrastructureFramesCollapsed = 4,
                    V2LeafWrapperDropped = 2,
                },
                AssertTransformContext = context =>
                {
                    AsyncProfilerStitchedFrame target = Assert.Single(context.Frames.Where(frame =>
                        frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget));
                    Assert.Equal(expectedIndex, target.SegmentFrameIndex);
                    Assert.DoesNotContain(context.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
                    Assert.Equal(logical.Length - expectedIndex - 1,
                        context.Frames.Count(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCaller));
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RuntimeAsync_WrapperIsSampledLeaf_ParentContextContextsRequireDispatchTarget(bool targetKnown)
        {
            Frame target = Frame.App("Scenario.InnerAsync");
            Frame parent = Frame.App("Scenario.ParentAsync");
            Frame owner = Frame.App("Scenario.OwnerAsync");
            Frame[] root = { Frame.ThreadPoolDispatch, Frame.WorkerThreadStart };
            var scenario = new StitchScenario
            {
                Sync = new[]
                {
                    Frame.V2Wrapper(0), Frame.V2InstrumentedDispatch, Frame.V2Dispatch,
                    Frame.ThreadPoolDispatch, Frame.WorkerThreadStart,
                },
                Async = new[] { targetKnown ? target : Frame.Zero, parent },
                ContextParent = AsyncSegment.Runtime(owner),
                EnableParentContexts = true,
                EnableFrameAnnotations = true,
                VerifySampleWeights = true,
                ExpectedStitched = (targetKnown ? new[] { target, parent, owner }
                    : new[] { Frame.V2InstrumentedDispatch, Frame.V2Dispatch }).Concat(root).ToArray(),
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = targetKnown ? 2 : 0,
                    V2InfrastructureFramesCollapsed = targetKnown ? 4 : 0,
                    V2SyncLayoutUsed = targetKnown ? 0 : 2,
                    V2LeafWrapperDropped = 2,
                },
                AssertTransformContext = context =>
                {
                    Assert.Equal(targetKnown ? 1 : 0,
                        context.Frames.Count(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget));
                    Assert.Equal(targetKnown ? 1 : 0,
                        context.Frames.Count(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncContextParent));
                    Assert.DoesNotContain(context.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, false, false, false)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, false, false, true)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, false, true, false)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, false, true, true)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, true, false, false)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, true, false, true)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, true, true, false)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, true, true, true)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, false, false, false)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, false, false, true)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, false, true, false)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, false, true, true)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, true, false, false)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, true, false, true)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, true, true, false)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, true, true, true)]
        public void RuntimeAsync_InnerWrapperIsSampledLeaf_FoldsOnlyVerifiedRuntimeBridge(
            int outerKindValue, bool enableNormalization, bool callback, bool targetKnown)
        {
            var outerKind = (AsyncCallstackKind)outerKindValue;
            Frame innerTarget = Frame.App("Scenario.InnerAsync");
            Frame innerParent = Frame.App("Scenario.InnerParentAsync");
            Frame outerIdentity = outerKind == AsyncCallstackKind.RuntimeAsync
                ? Frame.App("Scenario.NestedDispatcherOuterAsync")
                : Frame.App("Scenario+<NestedDispatcherOuterAsync>d__1.MoveNext");
            Frame outerParent = outerKind == AsyncCallstackKind.RuntimeAsync
                ? Frame.App("Scenario.OuterParentAsync")
                : Frame.App("Scenario+<OuterParentAsync>d__2.MoveNext");
            Frame[] entry = { Frame.V2Wrapper(1), Frame.V2InstrumentedDispatch, Frame.V2Dispatch };
            var bridge = new List<Frame>
            {
                Frame.CoreLib("System.Threading.Tasks.Task.RunContinuations(class System.Object)", "Task.RunContinuations"),
                Frame.CoreLib("System.Threading.Tasks.Task`1[System.Boolean].TrySetResult(!0)", "Task.TrySetResult"),
                Frame.CoreLib("System.Threading.Tasks.TaskCompletionSource`1[System.Boolean].SetResult(!0)", "TaskCompletionSource.SetResult"),
            };
            if (callback)
            {
                bridge.Add(Frame.App("Scenario.SynchronousCallback"));
            }
            Frame[] root = { Frame.ThreadPoolDispatch, Frame.WorkerThreadStart };
            Frame[] outerDispatch = outerKind == AsyncCallstackKind.RuntimeAsync
                ? new[] { Frame.V2Wrapper(0), Frame.V2InstrumentedDispatch, Frame.V2Dispatch }
                : new[] { Frame.V1MoveNextAsDispatcher };
            Frame[] logical = outerKind == AsyncCallstackKind.RuntimeAsync
                ? new[] { outerIdentity, outerParent }
                : new[]
                {
                    outerIdentity.Logical("Scenario.NestedDispatcherOuterAsync", -1),
                    outerParent.Logical("Scenario.OuterParentAsync", 5),
                };
            IEnumerable<Frame> inner = targetKnown
                ? new[] { innerTarget, innerParent }
                : (enableNormalization ? entry.Skip(1) : entry);
            bool fold = enableNormalization && targetKnown && !callback;
            Frame[] expected = inner
                .Concat(fold ? Enumerable.Empty<Frame>() : bridge).Concat(logical).Concat(root).ToArray();
            var scenario = new StitchScenario
            {
                Sync = entry.Concat(bridge).Concat(new[] { outerIdentity }).Concat(outerDispatch).Concat(root).ToArray(),
                AsyncSegments = new[]
                {
                    new AsyncSegment(outerKind, new[] { outerIdentity, outerParent }, new[] { 0, 5 }),
                    AsyncSegment.Runtime(Frame.Zero, targetKnown ? innerTarget : Frame.Zero, innerParent),
                },
                ExpectedStitched = expected,
                EnableFrameAnnotations = true,
                EnableNormalization = enableNormalization,
                EnableParentContexts = true,
                OnlyManagedCodeStacks = true,
                SkipNormalStackAssertion = true,
                VerifySampleWeights = true,
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 4,
                    V2SyncLayoutUsed = targetKnown ? 0 : 2,
                    V2LeafWrapperDropped = targetKnown || enableNormalization ? 2 : 0,
                    V2InfrastructureFramesCollapsed = (outerKind == AsyncCallstackKind.RuntimeAsync ? 4 : 0) +
                        (targetKnown ? 4 : 0),
                    V1InlineFallbackUsed = outerKind == AsyncCallstackKind.StateMachineAsync ? 2 : 0,
                    AsyncReentrantHandoffFramesCollapsed = fold ? 6 : 0,
                    V2ReentrantHandoffFramesCollapsed = fold && outerKind == AsyncCallstackKind.RuntimeAsync ? 6 : 0,
                },
                AssertTransformContext = context =>
                {
                    AsyncProfilerStitchedFrame outerCurrent = Assert.Single(context.Frames.Where(frame =>
                        frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent));
                    Assert.Same(context.Segments[1], outerCurrent.Context);
                    if (targetKnown)
                    {
                        AsyncProfilerStitchedFrame target = Assert.Single(context.Frames.Where(frame =>
                            frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget));
                        Assert.Same(context.Segments[0], target.Context);
                        Assert.Equal(1, target.SegmentFrameIndex);
                    }
                    else
                    {
                        Assert.DoesNotContain(context.Frames, frame => ReferenceEquals(frame.Context, context.Segments[0]));
                        Assert.Equal(enableNormalization ? 2 : 3, context.Frames.TakeWhile(frame => frame.Context == null).Count());
                    }
                    Assert.Equal(fold ? 3 : 0, context.Diagnostics.AsyncReentrantHandoffFramesCollapsed);
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(0, false, true)]
        [InlineData(0, true, true)]
        [InlineData(1, false, true)]
        [InlineData(1, true, true)]
        [InlineData(2, false, true)]
        [InlineData(2, true, true)]
        [InlineData(3, false, true)]
        [InlineData(3, true, true)]
        [InlineData(4, false, true)]
        [InlineData(4, true, true)]
        [InlineData(5, false, true)]
        [InlineData(5, true, true)]
        [InlineData(6, false, true)]
        [InlineData(6, true, true)]
        [InlineData(7, false, true)]
        [InlineData(7, true, true)]
        [InlineData(0, false, false)]
        [InlineData(0, true, false)]
        [InlineData(1, false, false)]
        [InlineData(1, true, false)]
        [InlineData(2, false, false)]
        [InlineData(2, true, false)]
        [InlineData(3, false, false)]
        [InlineData(3, true, false)]
        [InlineData(4, false, false)]
        [InlineData(4, true, false)]
        [InlineData(5, false, false)]
        [InlineData(5, true, false)]
        [InlineData(6, false, false)]
        [InlineData(6, true, false)]
        [InlineData(7, false, false)]
        [InlineData(7, true, false)]
        public void RuntimeAsync_InnerWrapperIsSampledLeaf_StitchesMultipleMixedOuterCallStacks(
            int protocols, bool enableNormalization, bool callbacks)
        {
            Frame[] entry = { Frame.V2Wrapper(1), Frame.V2InstrumentedDispatch, Frame.V2Dispatch };
            Frame innerTarget = Frame.App("Scenario.InnerAsync");
            Frame innerParent = Frame.App("Scenario.InnerParentAsync");
            Frame[] root = { Frame.ThreadPoolDispatch, Frame.WorkerThreadStart };
            var sync = new List<Frame>(entry);
            var expected = new List<Frame>();
            expected.Add(innerTarget);
            expected.Add(innerParent);
            var segments = new AsyncSegment[4];
            int v1Count = 0;
            int handoffFrames = 0;
            int v2HandoffFrames = 0;
            for (int i = 2; i >= 0; i--)
            {
                bool v1 = (protocols & (1 << i)) != 0;
                AsyncCallstackKind kind = v1
                    ? AsyncCallstackKind.StateMachineAsync : AsyncCallstackKind.RuntimeAsync;
                if (v1)
                {
                    v1Count++;
                }
                Frame current = Frame.App(v1
                    ? "Scenario+<Outer" + i + "Async>d__" + i + ".MoveNext"
                    : "Scenario.Outer" + i + "Async");
                Frame parent = Frame.App(v1
                    ? "Scenario+<Parent" + i + "Async>d__" + (10 + i) + ".MoveNext"
                    : "Scenario.Parent" + i + "Async");
                Frame[] bridge = i == 2 || !callbacks
                    ? new[]
                    {
                        Frame.CoreLib("System.Threading.Tasks.Task.RunContinuations(class System.Object)", "Task.RunContinuations"),
                        Frame.CoreLib("System.Threading.Tasks.Task`1[System.Boolean].TrySetResult(!0)", "Task.TrySetResult"),
                        Frame.CoreLib("System.Threading.Tasks.TaskCompletionSource`1[System.Boolean].SetResult(!0)", "TaskCompletionSource.SetResult"),
                    }
                    : new[] { Frame.App("Scenario.InterContextCallback" + i) };
                segments[i] = new AsyncSegment(kind, new[] { current, parent }, new[] { 0, 5 });
                sync.AddRange(bridge);
                sync.Add(current);
                sync.AddRange(v1
                    ? new[] { Frame.V1MoveNextAsDispatcher }
                    : new[] { Frame.V2Wrapper(0), Frame.V2InstrumentedDispatch, Frame.V2Dispatch });
                if (!enableNormalization || (callbacks && i != 2))
                {
                    expected.AddRange(bridge);
                }
                else
                {
                    handoffFrames += bridge.Length;
                    if (!v1 && (i == 2 || segments[i + 1].Kind == AsyncCallstackKind.RuntimeAsync))
                    {
                        v2HandoffFrames += bridge.Length;
                    }
                }
                expected.AddRange(v1
                    ? new[]
                    {
                        current.Logical("Scenario.Outer" + i + "Async", -1),
                        parent.Logical("Scenario.Parent" + i + "Async", 5),
                    }
                    : new[] { current, parent });
            }
            segments[3] = AsyncSegment.Runtime(
                Frame.Zero, innerTarget, innerParent);
            sync.AddRange(root);
            expected.AddRange(root);
            var scenario = new StitchScenario
            {
                Sync = sync.ToArray(),
                AsyncSegments = segments,
                ExpectedStitched = expected.ToArray(),
                EnableFrameAnnotations = true,
                EnableNormalization = enableNormalization,
                EnableParentContexts = true,
                OnlyManagedCodeStacks = true,
                SkipNormalStackAssertion = true,
                VerifySampleWeights = true,
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 8,
                    V2LeafWrapperDropped = 2,
                    V2InfrastructureFramesCollapsed = (4 - v1Count) * 4,
                    V1InlineFallbackUsed = v1Count * 2,
                    AsyncReentrantHandoffFramesCollapsed = handoffFrames * 2,
                    V2ReentrantHandoffFramesCollapsed = v2HandoffFrames * 2,
                },
                AssertTransformContext = context =>
                {
                    AsyncProfilerStitchedFrame target = Assert.Single(context.Frames.Where(frame =>
                        frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget));
                    Assert.Same(context.Segments[0], target.Context);
                    Assert.Equal(1, target.SegmentFrameIndex);
                    Assert.Equal(3, context.Frames.Count(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent));
                    for (int i = 0; i < 3; i++)
                    {
                        AsyncProfilerStitchedFrame anchor = Assert.Single(context.Frames.Where(frame =>
                            frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent &&
                            ReferenceEquals(frame.Context, context.Segments[3 - i])));
                        Assert.Equal(0, anchor.SegmentFrameIndex);
                    }
                    Assert.Equal(handoffFrames, context.Diagnostics.AsyncReentrantHandoffFramesCollapsed);
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void RuntimeAsync_DispatchInfrastructureWithoutWrapper_PreservesCallerContext(bool enableInfrastructureCleanup, bool flush)
        {
            Frame[] callees = flush
                ? new[]
                {
                    Frame.CoreLib("System.Diagnostics.Tracing.EventPipeInternal.WriteEventData", "EventPipeInternal.WriteEventData"),
                    Frame.CoreLib("System.Diagnostics.Tracing.EventSource.WriteEventWithRelatedActivityIdCore", "EventSource.WriteEventWithRelatedActivityIdCore"),
                    Frame.CoreLib("System.Runtime.CompilerServices.AsyncProfiler+AsyncThreadContext.Flush", "AsyncThreadContext.Flush"),
                }
                : new[]
                {
                    Frame.CoreLib("System.Threading.Thread.PollGCWorker", "Thread.PollGCWorker"),
                    Frame.CoreLib("Interop+Kernel32.QueryPerformanceCounter", "QueryPerformanceCounter"),
                };
            Frame[] dispatch = { Frame.V2InstrumentedDispatch, Frame.V2Dispatch };
            Frame[] root = { Frame.ThreadPoolDispatch, Frame.WorkerThreadStart };
            var scenario = new StitchScenario
            {
                Sync = callees.Concat(dispatch).Concat(root).ToArray(),
                Async = new[]
                {
                    Frame.App("Scenario.Level3Async"),
                    Frame.App("Scenario.Level2Async"),
                    Frame.App("Program.Main"),
                },
                ExpectedStitched = (enableInfrastructureCleanup && flush ? callees.Take(2) : callees)
                    .Concat(enableInfrastructureCleanup
                        ? new[] { (flush ? callees[2] : Frame.V2InstrumentedDispatch).DispatcherFrames() }
                        : dispatch).Concat(root).ToArray(),
                EnableInfrastructureCleanup = enableInfrastructureCleanup,
                VerifySampleWeights = true,
                AssertTransformContext = context =>
                {
                    Assert.All(context.Frames, frame => Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, frame.Origin));
                    Assert.All(context.Frames, frame => Assert.Null(frame.Context));
                    Assert.Equal(enableInfrastructureCleanup ? 1 : 0, context.Frames.Count(frame =>
                        frame.Presentation == AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames));
                },
                AssertStitchedStackSource = stackSource =>
                {
                    int markers = 0;
                    for (int i = 0; i < stackSource.CallFrameIndexLimit; i++)
                    {
                        var index = (StackSourceFrameIndex)i;
                        if (stackSource.GetFrameName(index, false) == "[Async dispatcher frames]")
                        {
                            markers++;
                            Assert.False(stackSource.TryGetAsyncProfilerCallStackFrameInfo(index, out _));
                            Assert.Equal(CodeAddressIndex.Invalid, stackSource.GetFrameCodeAddress(index));
                        }
                    }
                    Assert.Equal(enableInfrastructureCleanup ? 1 : 0, markers);
                },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    V2SyncLayoutUsed = 2,
                    AsyncInfrastructureFramesCollapsed = enableInfrastructureCleanup ? (flush ? 6 : 4) : 0,
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, false, false)]
        [InlineData(false, true, false)]
        [InlineData(true, false, false)]
        [InlineData(true, true, false)]
        [InlineData(true, true, true)]
        public void RuntimeAsync_DispatchInfrastructureWithCompletions_ProjectsTargetBeforePresentation(
            bool normalization, bool cleanup, bool callback)
        {
            Frame poll = Frame.CoreLib("System.Threading.Thread.PollGCWorker", "PollGCWorker");
            Frame target = Frame.App("Scenario.ParallelWhenAllAsync");
            Frame parent = Frame.App("Scenario.CallerAsync");
            Frame instrumented = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncHelpers+RuntimeAsyncTask`1[System.Int64].InstrumentedDispatchContinuations(value class Flags)",
                "InstrumentedDispatchContinuations");
            Frame dispatch = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncHelpers+RuntimeAsyncTask`1[System.Int64].DispatchContinuations()",
                "DispatchContinuations");
            Frame[] prefix = { poll, instrumented, dispatch };
            Frame[] completion =
            {
                Frame.CoreLib("System.Threading.Tasks.Task.RunContinuations", "Task.RunContinuations"),
                Frame.CoreLib("System.Threading.Tasks.Task`1[System.Int64].TrySetResult(!0)", "Task.TrySetResult"),
            };
            Frame[] bridge = completion.Concat(new[]
            {
                Frame.CoreLib("System.Threading.Tasks.Task+WhenAllPromise`1[System.Int64].Invoke", "WhenAllPromise.Invoke"),
            }).Concat(completion).Concat(new[] { instrumented, dispatch }).ToArray();
            if (callback)
            {
                bridge = new[] { Frame.App("Scenario.SynchronousCallback") }.Concat(bridge).ToArray();
            }
            Frame[] root = { Frame.ThreadPoolDispatch, Frame.WorkerThreadStart };
            Frame[] structural = new[] { poll, instrumented.DispatcherFrames(), target, parent }
                .Concat(bridge).Concat(root).ToArray();
            var scenario = new StitchScenario
            {
                Sync = prefix.Concat(bridge).Concat(root).ToArray(),
                Async = new[] { Frame.Zero, target, parent },
                ContextParent = AsyncSegment.Runtime(Frame.App("Scenario.CompletedBranchAsync")),
                MethodCompletions = 1,
                ExpectedStitched = cleanup
                    ? new[] { poll, instrumented.DispatcherFrames(), target, parent }
                        .Concat(callback ? bridge : Array.Empty<Frame>()).Concat(root).ToArray()
                    : structural,
                ExpectedWithoutNormalization = structural,
                ExpectedWithoutInfrastructureCleanup = cleanup ? structural : null,
                EnableNormalization = normalization,
                EnableInfrastructureCleanup = cleanup,
                EnableFrameAnnotations = true,
                VerifySampleWeights = true,
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V2InfrastructureFramesCollapsed = 4,
                    AsyncInfrastructureFramesCollapsed = cleanup && !callback ? 14 : 0,
                    RuntimeResumptionBridgeFramesCollapsed = cleanup && !callback ? 14 : 0,
                },
                AssertTransformContext = context =>
                {
                    AsyncProfilerStitchedFrame projected = Assert.Single(context.Frames.Where(frame =>
                        frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget));
                    Assert.Equal(1, projected.SegmentFrameIndex);
                    Assert.Same(context.Segments[0], projected.Context);
                    Assert.Equal(projected.Segment.CodeAddressAt(1), projected.CodeAddress);
                    Assert.Single(context.Frames.Where(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCaller));
                    Assert.DoesNotContain(context.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
                    Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, context.Frames[0].Origin);
                    Assert.Contains("PollGCWorker", context.TraceLog.CodeAddresses[context.Frames[0].CodeAddress].FullMethodName);
                    AsyncProfilerStitchedFrame marker = Assert.Single(context.Frames.Where(frame =>
                        frame.Presentation == AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames));
                    Assert.Equal(marker, context.Frames[1]);
                    Assert.Same(projected.Context, marker.Context);
                },
                AssertStitchedStackSource = stackSource =>
                {
                    int targets = 0;
                    for (int i = 0; i < stackSource.CallFrameIndexLimit; i++)
                    {
                        var frameIndex = (StackSourceFrameIndex)i;
                        if (stackSource.TryGetAsyncProfilerCallStackFrameInfo(frameIndex, out AsyncProfilerCallStackSourceFrameInfo info) &&
                            info.IsDispatchTarget)
                        {
                            targets++;
                            Assert.False(info.IsCurrent);
                            Assert.False(info.IsCompleting);
                            Assert.Equal(AsyncCallstackKind.RuntimeAsync, info.Kind);
                            Assert.NotEqual(0UL, info.MethodId);
                            Assert.Equal(info.CodeAddress, stackSource.GetFrameCodeAddress(frameIndex));
                            Assert.StartsWith("[Async V2] ", stackSource.GetFrameName(frameIndex, false));
                        }
                    }
                    Assert.Equal(1, targets);
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false, false, true, true)]
        [InlineData(false, true, true, true)]
        [InlineData(true, false, true, true)]
        [InlineData(true, true, true, true)]
        [InlineData(false, false, false, true)]
        [InlineData(false, true, false, true)]
        [InlineData(true, false, false, true)]
        [InlineData(true, true, false, true)]
        [InlineData(false, false, false, false)]
        [InlineData(true, true, true, false)]
        public void RuntimeAsync_DispatchInfrastructureWithZeroCompletedFrames_ProjectsFirstTarget(
            bool normalization, bool cleanup, bool sampledCallees, bool annotations)
        {
            Frame poll = Frame.CoreLib("System.Threading.Thread.PollGCWorker", "PollGCWorker");
            Frame counter = Frame.CoreLib("Interop+Kernel32.QueryPerformanceCounter(int64*)", "QueryPerformanceCounter");
            Frame target = Frame.App("V2TraceScenarios.RunBoundaryFallbackSamplingAsync");
            Frame[] callees = sampledCallees ? new[] { poll, counter } : Array.Empty<Frame>();
            Frame[] prefix = callees.Concat(new[] { Frame.V2InstrumentedDispatch, Frame.V2Dispatch }).ToArray();
            Frame[] root = { Frame.ThreadPoolDispatch, Frame.WorkerThreadStart };
            Frame[] structural = callees.Concat(new[] { Frame.V2InstrumentedDispatch.DispatcherFrames(), target })
                .Concat(root).ToArray();
            var scenario = new StitchScenario
            {
                Sync = prefix.Concat(root).ToArray(),
                Async = new[] { target },
                MethodCompletions = 1,
                // Establish event availability, but do not count this completion at either sample.
                MethodCompletionQpc = 100_028,
                ExpectedStitched = structural,
                ExpectedWithoutNormalization = structural,
                ExpectedWithoutInfrastructureCleanup = cleanup ? structural : null,
                EnableNormalization = normalization,
                EnableInfrastructureCleanup = cleanup,
                EnableFrameAnnotations = annotations,
                VerifySampleWeights = true,
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V2InfrastructureFramesCollapsed = 4,
                },
                AssertTransformContext = context =>
                {
                    Assert.Equal(0, context.Segments[0].GetCompletedFrameCount(context.TimestampQpc));
                    AsyncProfilerStitchedFrame projected = Assert.Single(context.Frames.Where(frame =>
                        frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget));
                    Assert.Equal(0, projected.SegmentFrameIndex);
                    Assert.Same(context.Segments[0], projected.Context);
                    Assert.Equal(projected.Segment.CodeAddressAt(0), projected.CodeAddress);
                    Assert.DoesNotContain(context.Frames, frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
                    Assert.All(context.Frames.Take(callees.Length), frame =>
                        Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, frame.Origin));
                    AsyncProfilerStitchedFrame marker = Assert.Single(context.Frames.Where(frame =>
                        frame.Presentation == AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames));
                    Assert.Equal(marker, context.Frames[callees.Length]);
                    Assert.Same(projected.Context, marker.Context);
                },
                AssertStitchedStackSource = stackSource =>
                {
                    int targets = 0;
                    int markers = 0;
                    for (int i = 0; i < stackSource.CallFrameIndexLimit; i++)
                    {
                        var frameIndex = (StackSourceFrameIndex)i;
                        if (stackSource.GetFrameName(frameIndex, false) == "[Async dispatcher frames]")
                        {
                            markers++;
                            Assert.False(stackSource.TryGetAsyncProfilerCallStackFrameInfo(frameIndex, out _));
                            Assert.Equal(CodeAddressIndex.Invalid, stackSource.GetFrameCodeAddress(frameIndex));
                        }
                        if (stackSource.TryGetAsyncProfilerCallStackFrameInfo(frameIndex, out AsyncProfilerCallStackSourceFrameInfo info) &&
                            info.IsDispatchTarget)
                        {
                            targets++;
                            Assert.False(info.IsCurrent);
                            Assert.False(info.IsCompleting);
                            Assert.NotEqual(0UL, info.MethodId);
                            Assert.Equal(info.CodeAddress, stackSource.GetFrameCodeAddress(frameIndex));
                        }
                    }
                    Assert.Equal(1, targets);
                    Assert.Equal(1, markers);
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("no-completion-events")]
        [InlineData("completed-single-frame")]
        [InlineData("reversed-pair")]
        [InlineData("different-type")]
        [InlineData("foreign-module")]
        [InlineData("unknown-target")]
        [InlineData("future-completion")]
        [InlineData("popped-inner")]
        public void RuntimeAsync_DispatchInfrastructureWithUncertainTargetOrOwner_PreservesPhysicalStack(string obstruction)
        {
            Frame target = Frame.App("Scenario.ParallelWhenAllAsync");
            Frame[] dispatch = { Frame.V2InstrumentedDispatch, Frame.V2Dispatch };
            if (obstruction == "reversed-pair")
            {
                Array.Reverse(dispatch);
            }
            else if (obstruction == "different-type")
            {
                dispatch[1] = Frame.CoreLib(
                    "System.Threading.Tasks.OtherRuntimeAsyncTask.DispatchContinuations", "Other.DispatchContinuations");
            }
            else if (obstruction == "foreign-module")
            {
                dispatch[1] = Frame.Library("OtherRuntime", Frame.V2Dispatch.SymbolName, "Foreign.DispatchContinuations");
            }
            Frame[] sync = new[] { Frame.CoreLib("System.Threading.Thread.PollGCWorker", "PollGCWorker") }
                .Concat(dispatch).Concat(new[] { Frame.ThreadPoolDispatch, Frame.WorkerThreadStart }).ToArray();
            var scenario = new StitchScenario
            {
                Sync = sync,
                Async = obstruction == "no-completion-events" || obstruction == "completed-single-frame"
                    ? new[] { target }
                    : new[] { Frame.Zero, target, Frame.App("Scenario.CallerAsync") },
                MethodCompletions = obstruction == "no-completion-events" ? 0 : 1,
                MethodCompletionQpc = obstruction == "future-completion" ? 100_028 : 100_020,
                IncludeCompletedInnerCallStack = obstruction == "popped-inner",
                OmitSymbolFor = obstruction == "unknown-target" ? target : null,
                ExpectedStitched = sync,
                IncludeEventSourceEvents = false,
                VerifySampleWeights = true,
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation { V2SyncLayoutUsed = 2 },
                AssertTransformContext = context =>
                {
                    Assert.All(context.Frames, frame =>
                    {
                        Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, frame.Origin);
                        Assert.Null(frame.Context);
                    });
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RuntimeAsync_OtherProcessHasActiveSegment_DoesNotStitchSample(bool enableInfrastructureCleanup)
        {
            var scenario = new StitchScenario
            {
                Sync = new[]
                {
                    Frame.App("Scenario.DoWork"),
                    Frame.App("Scenario.Level3Async"),
                    Frame.V2Wrapper(0),
                    Frame.V2InstrumentedDispatch,
                    Frame.V2Dispatch,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                Async = new[]
                {
                    Frame.App("Scenario.Level3Async"),
                    Frame.App("Scenario.Level2Async"),
                    Frame.App("Program.Main"),
                },
                ExpectedStitched = new[]
                {
                    Frame.App("Scenario.DoWork"),
                    Frame.App("Scenario.Level3Async"),
                    Frame.V2Wrapper(0),
                    Frame.V2InstrumentedDispatch,
                    Frame.V2Dispatch,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                AsyncStartQpc = 100_030,
                AsyncEndQpc = 100_040,
                AddForeignProcessActiveSegment = true,
                EnableInfrastructureCleanup = enableInfrastructureCleanup,
            };

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void RuntimeAsync_Scenario3Resume_RemovesSchedulingFrames()
        {
            Frame readConsoleInput = Frame.Library("System.Console", "Interop+Kernel32.ReadConsoleInput", "Interop.Kernel32.ReadConsoleInput");
            Frame consolePalReadKey = Frame.Library("System.Console", "System.ConsolePal.ReadKey", "ConsolePal.ReadKey");
            Frame consoleReadKey = Frame.Library("System.Console", "System.Console.ReadKey", "Console.ReadKey");
            Frame asyncFoo = Frame.App("TestCase3.AsyncFoo");
            Frame runContinuations = Frame.CoreLib("System.Threading.Tasks.Task.RunContinuations", "Task.RunContinuations");
            Frame trySetResult = Frame.CoreLib("System.Threading.Tasks.Task.TrySetResult", "Task.TrySetResult");
            Frame completeTimedOut = Frame.CoreLib(
                "System.Threading.Tasks.Task+DelayPromise.CompleteTimedOut", "DelayPromise.CompleteTimedOut");
            Frame timerFire = Frame.CoreLib("System.Threading.TimerQueueTimer.Fire", "TimerQueueTimer.Fire");
            Frame workerDoWork = Frame.CoreLib(
                "System.Threading.PortableThreadPool+WorkerThread.WorkerDoWork", "WorkerThread.WorkerDoWork");
            Frame threadStartCallback = Frame.CoreLib("System.Threading.Thread.StartCallback", "Thread.StartCallback");

            var scenario = new StitchScenario
            {
                Sync = new[]
                {
                    readConsoleInput,
                    consolePalReadKey,
                    consoleReadKey,
                    asyncFoo,
                    Frame.V2Wrapper(1),
                    Frame.V2InstrumentedDispatch,
                    Frame.V2Dispatch,
                    runContinuations,
                    trySetResult,
                    completeTimedOut,
                    timerFire,
                    Frame.ThreadPoolDispatch,
                    workerDoWork,
                    Frame.WorkerThreadStart,
                    threadStartCallback,
                },
                Async = new[]
                {
                    Frame.Zero,
                    asyncFoo,
                },
                ExpectedStitched = new[]
                {
                    readConsoleInput,
                    consolePalReadKey,
                    consoleReadKey,
                    asyncFoo,
                    runContinuations,
                    trySetResult,
                    completeTimedOut,
                    timerFire,
                    Frame.ThreadPoolDispatch,
                    workerDoWork,
                    Frame.WorkerThreadStart,
                    threadStartCallback,
                },
                FilteredOut = new[] { runContinuations, trySetResult, completeTimedOut, timerFire },
                ContinuationIndexBase = 0,
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V2InfrastructureFramesCollapsed = 4,
                },
            };

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void RuntimeAsync_SynchronousV1Startup_IsNormalizedByNormalizationPipeline()
        {
            Frame work = Frame.App("Scenario.SynchronousV1Work");
            Frame generatedV1 = Frame.App("Scenario+<SynchronousV1Async>d__4.MoveNext");
            Frame builderStart = Frame.CoreLib(
                "System.Runtime.CompilerServices.AsyncTaskMethodBuilder.Start(Scenario+<SynchronousV1Async>d__4&)",
                "AsyncTaskMethodBuilder.Start");
            Frame kickoffV1 = Frame.App("Scenario.SynchronousV1Async(int32)");
            Frame outerCurrent = Frame.App("Scenario.OuterV2Async");
            Frame outerParent = Frame.App("Scenario.OuterV2ParentAsync");

            var scenario = new StitchScenario
            {
                Sync = new[]
                {
                    work,
                    generatedV1,
                    builderStart,
                    kickoffV1,
                    outerCurrent,
                    Frame.V2Wrapper(0),
                    Frame.V2InstrumentedDispatch,
                    Frame.V2Dispatch,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                Async = new[]
                {
                    outerCurrent,
                    outerParent,
                },
                ExpectedStitched = new[]
                {
                    work,
                    kickoffV1,
                    outerCurrent,
                    outerParent,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                ExpectedWithoutNormalization = new[]
                {
                    work,
                    generatedV1,
                    builderStart,
                    kickoffV1,
                    outerCurrent,
                    outerParent,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V1SynchronousStartupFramesCollapsed = 4,
                    V2InfrastructureFramesCollapsed = 4,
                },
            };

            scenario.AssertProductionStitch();
        }

        [Fact]
        public void RuntimeAsync_ActivityGroupingDisabled_UsesConsistentThreadRoot()
        {
            var scenario = new StitchScenario
            {
                Sync = new[]
                {
                    Frame.App("Scenario.DoWork"),
                    Frame.App("Scenario.Level3Async"),
                    Frame.V2Wrapper(0),
                    Frame.V2InstrumentedDispatch,
                    Frame.V2Dispatch,
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                Async = new[]
                {
                    Frame.App("Scenario.Level3Async"),
                    Frame.App("Scenario.Level2Async"),
                    Frame.App("Program.Main"),
                },
                ExpectedStitched = new[]
                {
                    Frame.App("Scenario.DoWork"),
                    Frame.App("Scenario.Level3Async"),
                    Frame.App("Scenario.Level2Async"),
                    Frame.App("Program.Main"),
                    Frame.ThreadPoolDispatch,
                    Frame.WorkerThreadStart,
                },
                GroupByStartStopActivity = false,
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 2,
                    V2InfrastructureFramesCollapsed = 4,
                },
            };

            scenario.AssertProductionStitch();
        }

        [Theory]
        [InlineData("missing-boundary")]
        [InlineData("adjacency-mismatch")]
        [InlineData("ambiguous-v1")]
        [InlineData("outer-target")]
        [InlineData("nonadjacent")]
        [InlineData("no-trace")]
        [InlineData("unresolved-target")]
        [InlineData("unresolved-bridge")]
        [InlineData("foreign-bridge")]
        public void RuntimeAsync_ReentrantDispatchTarget_PreservesUnverifiedHandoff(string obstruction)
        {
            Frame target = Frame.App("Scenario.InnerAsync");
            Frame outer = Frame.App("Scenario.OuterAsync");
            Frame[] bridge =
            {
                Frame.CoreLib("System.Threading.Tasks.Task.RunContinuations", "Task.RunContinuations"),
                Frame.CoreLib("System.Threading.Tasks.Task.TrySetResult", "Task.TrySetResult"),
                obstruction == "foreign-bridge"
                    ? Frame.Library("ForeignLibrary", "System.Threading.Tasks.TaskCompletionSource`1.SetResult", "Foreign.SetResult")
                    : Frame.CoreLib("System.Threading.Tasks.TaskCompletionSource`1.SetResult", "TaskCompletionSource.SetResult"),
            };
            Frame[] root = { Frame.ThreadPoolDispatch, Frame.WorkerThreadStart };
            var scenario = new StitchScenario
            {
                Sync = new[] { Frame.V2Wrapper(0), Frame.V2InstrumentedDispatch, Frame.V2Dispatch }.Concat(bridge)
                    .Concat(new[] { outer, Frame.V2Wrapper(0), Frame.V2InstrumentedDispatch, Frame.V2Dispatch })
                    .Concat(root).ToArray(),
                AsyncSegments = new[] { AsyncSegment.Runtime(outer), AsyncSegment.Runtime(target) },
                OmitSymbolFor = obstruction == "unresolved-target" ? target :
                    obstruction == "unresolved-bridge" ? bridge[1] : null,
                EnableNormalization = false,
                VerifySampleWeights = true,
                ExpectedStitched = new[] { target }.Concat(bridge)
                    .Concat(new[] { outer }).Concat(root).ToArray(),
                ExpectedDiagnostics = new CallStackDiagnosticsExpectation
                {
                    SegmentsProcessed = 4,
                    V2InfrastructureFramesCollapsed = 8,
                    V2LeafWrapperDropped = 2,
                },
                AssertTransformContext = context =>
                {
                    var frames = new List<AsyncProfilerStitchedFrame>(context.Frames);
                    var diagnostics = new AsyncProfilerCallStackDiagnostics();
                    var stitchingState = new AsyncProfilerCallStackStitchingState
                    {
                        HasConsistentPhysicalAlignment = obstruction != "missing-boundary" && obstruction != "adjacency-mismatch",
                        UnresolvedV1CompletionCount = obstruction == "ambiguous-v1" ? 1 : 0,
                    };
                    int outerIndex = frames.FindIndex(frame => frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent);
                    if (obstruction == "outer-target")
                    {
                        frames[outerIndex] = AsyncProfilerStitchedFrame.CreateAsyncDispatchTarget(context.Segments[1], 0);
                    }
                    else if (obstruction == "nonadjacent")
                    {
                        AsyncProfilerStitchedFrame current = frames[outerIndex];
                        frames[outerIndex] = AsyncProfilerStitchedFrame.CreateAsyncCurrent(
                            current.CodeAddress, current.Method, context.Segments[0], 0);
                        for (int i = 1; i < outerIndex; i++)
                        {
                            AsyncProfilerStitchedFrame frame = frames[i];
                            frames[i] = AsyncProfilerStitchedFrame.CreateSync(
                                new AsyncProfilerStitchSyncFrame(frame.CodeAddress, frame.Method, frame.SyncClassification), context.Segments[0]);
                        }
                    }
                    AsyncProfilerStitchedFrame[] expected = frames.ToArray();
                    var pipeline = new AsyncProfilerCallStackTransformPipeline();
                    AsyncProfilerStitchResult result = pipeline.Transform(new AsyncProfilerStitchResult(frames, diagnostics, stitchingState), context.Segments,
                        context.TimestampQpc, context.ClassifyBoundary, obstruction == "no-trace" ? null : context.TraceLog);
                    Assert.Equal(expected, result.Frames);
                    Assert.Equal(0, diagnostics.AsyncReentrantHandoffFramesCollapsed);
                    Assert.Equal(0, diagnostics.V2ReentrantHandoffFramesCollapsed);
                },
            };
            scenario.AssertProductionStitch();
        }
    }
}
