// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Diagnostics.Tracing.Computers;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;

using Xunit;

using static Microsoft.Diagnostics.Tracing.Computers.AsyncProfilerRuntimeSupport;
using static TraceEventTests.AsyncProfilerStackTestData;

namespace TraceEventTests
{
    /// <summary>Exercises presentation transforms, pipeline composition, and the lifetime of borrowed public callback views.</summary>
    public sealed class AsyncProfilerCallStackTransformPipelineTests
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ConservativeTransforms_AreEnabledByDefaultAndCanBeDisabled(bool useDefault)
        {
            var diagnostics = new AsyncProfilerCallStackDiagnostics();
            List<AsyncProfilerStitchedFrame> frames = StartupFrames();
            var pipeline = new AsyncProfilerCallStackTransformPipeline();
            if (!useDefault)
            {
                pipeline.EnableNormalization = false;
            }

            pipeline.ApplyInPlace(Context(frames, diagnostics));

            Assert.Equal(useDefault ? new[] { CA(10), CA(11), CA(14) } : new[] { CA(10), CA(11), CA(12), CA(13), CA(14) },
                frames.Select(frame => frame.CodeAddress));
            Assert.Equal(
                useDefault ? AsyncProfilerStitchedFramePresentation.LogicalStateMachineMethod : AsyncProfilerStitchedFramePresentation.Native,
                frames[1].Presentation);
            Assert.Equal(useDefault ? 1 : 0, diagnostics.V1SynchronousMoveNextFramesNormalized);
            Assert.Equal(useDefault ? 2 : 0, diagnostics.V1SynchronousStartupFramesCollapsed);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void MaximumParentContextDepth_MustBePositive(int depth)
        {
            var pipeline = new AsyncProfilerCallStackTransformPipeline();
            var transforms = new AsyncCallStackTransforms(pipeline)
            {
                MaximumParentContextDepth = 3,
            };

            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => transforms.MaximumParentContextDepth = depth);
            Assert.Equal(nameof(AsyncCallStackTransforms.MaximumParentContextDepth), exception.ParamName);
            Assert.Equal(3, transforms.MaximumParentContextDepth);
        }

        [Fact]
        public void ConservativeTransforms_NormalizeStandaloneV1MoveNext()
        {
            var diagnostics = new AsyncProfilerCallStackDiagnostics();
            var frames = new List<AsyncProfilerStitchedFrame>
            {
                Sync(15),
                Sync(16, AsyncProfilerStitchSyncFrameKind.V1StateMachineBody),
                Sync(17),
            };
            var pipeline = new AsyncProfilerCallStackTransformPipeline();

            pipeline.ApplyInPlace(Context(frames, diagnostics));

            Assert.Equal(new[] { CA(15), CA(16), CA(17) }, frames.Select(frame => frame.CodeAddress));
            Assert.Equal(
                AsyncProfilerStitchedFramePresentation.LogicalStateMachineMethod,
                frames[1].Presentation);
            Assert.Equal(1, diagnostics.V1SynchronousMoveNextFramesNormalized);
            Assert.Equal(0, diagnostics.V1SynchronousStartupFramesCollapsed);
        }

        [Theory]
        [InlineData(false, 0)]
        [InlineData(false, 1)]
        [InlineData(true, 0)]
        [InlineData(true, 1)]
        public void SampledV2Wrapper_UsesReconstructionStateRatherThanDiagnostics(
            bool usesSynchronousLayout, int reportedSynchronousLayouts)
        {
            var diagnostics = new AsyncProfilerCallStackDiagnostics
            {
                V2SyncLayoutUsed = reportedSynchronousLayouts,
            };
            var frames = new List<AsyncProfilerStitchedFrame> { Sync(20), Sync(21) };
            var pipeline = new AsyncProfilerCallStackTransformPipeline();

            pipeline.ApplyInPlace(Context(frames, diagnostics,
                wrapperCodeAddress: CA(20), usesV2SynchronousLayout: usesSynchronousLayout));

            Assert.Equal(usesSynchronousLayout ? new[] { Sync(21) } : new[] { Sync(20), Sync(21) }, frames);
            Assert.Equal(usesSynchronousLayout ? 1 : 0, diagnostics.V2LeafWrapperDropped);
        }

        [Fact]
        public void AsyncInfrastructureCleanup_IsOptIn()
        {
            var diagnostics = new AsyncProfilerCallStackDiagnostics();
            var frames = new List<AsyncProfilerStitchedFrame>
            {
                Sync(30),
                Sync(31),
                Sync(32),
            };
            var pipeline = new AsyncProfilerCallStackTransformPipeline
            {
                EnableNormalization = false,
            };
            AsyncProfilerCallStackTransformState context = Context(
                frames,
                diagnostics,
                infrastructureCodeAddress: CA(31));

            Assert.False(pipeline.EnableInfrastructureCleanup);
            pipeline.ApplyInPlace(context);
            Assert.Equal(new[] { CA(30), CA(31), CA(32) }, frames.Select(frame => frame.CodeAddress));

            pipeline.EnableInfrastructureCleanup = true;
            pipeline.ApplyInPlace(context);
            Assert.Equal(new[] { CA(30), CA(31), CA(32) }, frames.Select(frame => frame.CodeAddress));
            Assert.Equal(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames, frames[1].Presentation);
            Assert.Equal(1, diagnostics.AsyncInfrastructureFramesCollapsed);
        }

        [Theory]
        [InlineData((int)AsyncProfilerCallStackStitchBoundaryKind.V1Dispatcher, (int)AsyncProfilerCallStackStitchBoundaryKind.V1DispatcherInfrastructure, false)]
        [InlineData((int)AsyncProfilerCallStackStitchBoundaryKind.V1Dispatcher, (int)AsyncProfilerCallStackStitchBoundaryKind.V1DispatcherInfrastructure, true)]
        [InlineData((int)AsyncProfilerCallStackStitchBoundaryKind.V2DispatcherInfrastructure, (int)AsyncProfilerCallStackStitchBoundaryKind.V2DispatcherInfrastructure, false)]
        [InlineData((int)AsyncProfilerCallStackStitchBoundaryKind.V2DispatcherInfrastructure, (int)AsyncProfilerCallStackStitchBoundaryKind.V2DispatcherInfrastructure, true)]
        [InlineData((int)AsyncProfilerCallStackStitchBoundaryKind.V2Dispatcher, (int)AsyncProfilerCallStackStitchBoundaryKind.V2DispatcherInfrastructure, false)]
        [InlineData((int)AsyncProfilerCallStackStitchBoundaryKind.V2Dispatcher, (int)AsyncProfilerCallStackStitchBoundaryKind.V2DispatcherInfrastructure, true)]
        public void AsyncInfrastructureCleanup_RepresentsResidualDispatcherSpans(
            int leafKindValue, int rootKindValue, bool enableNormalization)
        {
            var leafKind = (AsyncProfilerCallStackStitchBoundaryKind)leafKindValue;
            var rootKind = (AsyncProfilerCallStackStitchBoundaryKind)rootKindValue;
            var diagnostics = new AsyncProfilerCallStackDiagnostics();
            var frames = new List<AsyncProfilerStitchedFrame> { Sync(30), Sync(31), Sync(32), Sync(33) };
            var pipeline = new AsyncProfilerCallStackTransformPipeline
            {
                EnableNormalization = enableNormalization,
            };
            var context = Context(frames, diagnostics, classifyBoundary: ca =>
                new AsyncProfilerCallStackStitchBoundaryInfo(ca == CA(31) ? leafKind :
                    ca == CA(32) ? rootKind : AsyncProfilerCallStackStitchBoundaryKind.None, 0));

            pipeline.ApplyInPlace(context);
            Assert.Equal(new[] { CA(30), CA(31), CA(32), CA(33) }, frames.Select(frame => frame.CodeAddress));
            Assert.All(frames, frame => Assert.Equal(AsyncProfilerStitchedFramePresentation.Native, frame.Presentation));

            pipeline.EnableInfrastructureCleanup = true;
            pipeline.ApplyInPlace(context);

            Assert.Equal(new[] { CA(30), CA(31), CA(33) }, frames.Select(frame => frame.CodeAddress));
            Assert.Equal(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames, frames[1].Presentation);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, frames[1].Origin);
            Assert.Null(frames[1].Context);
            Assert.Equal(2, diagnostics.AsyncInfrastructureFramesCollapsed);

            pipeline.ApplyInPlace(context);
            Assert.Equal(new[] { CA(30), CA(31), CA(33) }, frames.Select(frame => frame.CodeAddress));
            Assert.Equal(2, diagnostics.AsyncInfrastructureFramesCollapsed);
        }

        [Fact]
        public void AsyncInfrastructureCleanup_DoesNotMergeDispatcherSpansAcrossCallees()
        {
            var frames = new List<AsyncProfilerStitchedFrame> { Sync(30), Sync(31), Sync(32), Sync(33) };
            var diagnostics = new AsyncProfilerCallStackDiagnostics();
            var pipeline = new AsyncProfilerCallStackTransformPipeline { EnableInfrastructureCleanup = true };

            pipeline.ApplyInPlace(Context(frames, diagnostics, classifyBoundary: ca =>
                new AsyncProfilerCallStackStitchBoundaryInfo(ca == CA(30) || ca == CA(32)
                    ? AsyncProfilerCallStackStitchBoundaryKind.V2DispatcherInfrastructure : AsyncProfilerCallStackStitchBoundaryKind.None, -1)));

            Assert.Equal(new[] { CA(30), CA(31), CA(32), CA(33) }, frames.Select(frame => frame.CodeAddress));
            Assert.Equal(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames, frames[0].Presentation);
            Assert.Equal(AsyncProfilerStitchedFramePresentation.Native, frames[1].Presentation);
            Assert.Equal(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames, frames[2].Presentation);
            Assert.Equal(AsyncProfilerStitchedFramePresentation.Native, frames[3].Presentation);
            Assert.Equal(2, diagnostics.AsyncInfrastructureFramesCollapsed);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void AsyncInfrastructureCleanup_PreservesDispatcherContextBoundaries(bool sameContext, bool builderHelper)
        {
            AsyncProfilerCallStack leafContext = CallStack();
            AsyncProfilerCallStack rootContext = sameContext ? leafContext : CallStack();
            FrameClassification kind = builderHelper
                ? (FrameClassification)RuntimeFrameKind.V1BuilderAwaitRegistration
                : AsyncProfilerStitchSyncFrameKind.None;
            var frames = new List<AsyncProfilerStitchedFrame>
            {
                Sync(30, kind, leafContext),
                Sync(31, kind, rootContext),
                Sync(32),
            };
            var diagnostics = new AsyncProfilerCallStackDiagnostics();
            var pipeline = new AsyncProfilerCallStackTransformPipeline { EnableInfrastructureCleanup = true };

            pipeline.ApplyInPlace(Context(frames, diagnostics, classifyBoundary: ca =>
                new AsyncProfilerCallStackStitchBoundaryInfo(builderHelper || ca == CA(32) ? AsyncProfilerCallStackStitchBoundaryKind.None :
                    AsyncProfilerCallStackStitchBoundaryKind.V1DispatcherInfrastructure, -1)));

            Assert.Equal(sameContext ? 2 : 3, frames.Count);
            Assert.Equal(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames, frames[0].Presentation);
            Assert.Same(leafContext, frames[0].Context);
            if (!sameContext)
            {
                Assert.Equal(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames, frames[1].Presentation);
                Assert.Same(rootContext, frames[1].Context);
            }
            Assert.Equal(CA(32), frames[frames.Count - 1].CodeAddress);
            Assert.Equal(2, diagnostics.AsyncInfrastructureFramesCollapsed);
        }

        [Fact]
        public void AsyncInfrastructureCleanup_DispatcherCreation_IsOptInAndPreservesTheApplicationFrame()
        {
            var diagnostics = new AsyncProfilerCallStackDiagnostics();
            var frames = new List<AsyncProfilerStitchedFrame>
            {
                Sync(34, AsyncProfilerStitchSyncFrameKind.V1DispatchSetup),
                Sync(35),
                Sync(36, RuntimeFrameKind.SchedulerEntry),
            };
            var pipeline = new AsyncProfilerCallStackTransformPipeline();
            AsyncProfilerCallStackTransformState context = Context(frames, diagnostics);

            pipeline.ApplyInPlace(context);
            Assert.Equal(new[] { CA(34), CA(35), CA(36) }, frames.Select(frame => frame.CodeAddress));
            Assert.Equal(0, diagnostics.AsyncInfrastructureFramesCollapsed);

            pipeline.EnableInfrastructureCleanup = true;
            pipeline.ApplyInPlace(context);
            Assert.Equal(new[] { CA(34), CA(35), CA(36) }, frames.Select(frame => frame.CodeAddress));
            Assert.Equal(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames, frames[0].Presentation);
            Assert.Equal(1, diagnostics.AsyncInfrastructureFramesCollapsed);
        }

        [Fact]
        public void AsyncInfrastructureCleanup_GroupsBuilderAwaitUnsafeOnCompletedOverloads()
        {
            var diagnostics = new AsyncProfilerCallStackDiagnostics();
            var frames = new List<AsyncProfilerStitchedFrame>
            {
                Sync(40),
                Sync(41, RuntimeFrameKind.V1BuilderAwaitRegistration),
                Sync(42, RuntimeFrameKind.V1BuilderAwaitRegistration),
                Sync(43, RuntimeFrameKind.V1BuilderAwaitRegistration),
                Sync(44, AsyncProfilerStitchSyncFrameKind.V1MethodHelper),
                Sync(45, AsyncProfilerStitchSyncFrameKind.V1MethodCompletion),
                Sync(46, RuntimeFrameKind.V1AwaiterRegistration),
                Sync(47, AsyncProfilerStitchSyncFrameKind.ContinuationBridge),
                Sync(48, AsyncProfilerStitchSyncFrameKind.ContinuationBridge),
                Sync(49), // User/custom awaiter, builder, or bridge method remains ordinary.
                Sync(50, RuntimeFrameKind.SchedulerEntry),
            };
            var pipeline = new AsyncProfilerCallStackTransformPipeline
            {
                EnableNormalization = false,
                EnableInfrastructureCleanup = true,
            };

            AsyncProfilerCallStackTransformState context = Context(frames, diagnostics);
            pipeline.ApplyInPlace(context);

            Assert.Equal(new[] { CA(40), CA(41), CA(49), CA(50) }, frames.Select(frame => frame.CodeAddress));
            Assert.Equal(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames, frames[1].Presentation);
            Assert.Equal(8, diagnostics.AsyncInfrastructureFramesCollapsed);

            pipeline.ApplyInPlace(context);
            Assert.Equal(new[] { CA(40), CA(41), CA(49), CA(50) }, frames.Select(frame => frame.CodeAddress));
            Assert.Equal(8, diagnostics.AsyncInfrastructureFramesCollapsed);
        }

        [Fact]
        public void AsyncInfrastructureCleanup_GroupsCompleteTaskCompletionChain()
        {
            var diagnostics = new AsyncProfilerCallStackDiagnostics();
            var frames = new List<AsyncProfilerStitchedFrame>
            {
                Sync(60),
                Sync(61, RuntimeFrameKind.TaskContinuations),
                Sync(62, RuntimeFrameKind.TaskResult),
                Sync(63, RuntimeFrameKind.TaskCompletionSourceResult),
                Sync(64),
            };
            var pipeline = new AsyncProfilerCallStackTransformPipeline
            {
                EnableNormalization = false,
                EnableInfrastructureCleanup = true,
            };

            pipeline.ApplyInPlace(Context(frames, diagnostics));

            Assert.Equal(new[] { CA(60), CA(61), CA(64) }, frames.Select(frame => frame.CodeAddress));
            Assert.Equal(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames, frames[1].Presentation);
            Assert.Equal(3, diagnostics.AsyncInfrastructureFramesCollapsed);
        }

        [Fact]
        public void AsyncInfrastructureCleanup_PreservesIsolatedTaskCompletionSourceSetResult()
        {
            var diagnostics = new AsyncProfilerCallStackDiagnostics();
            var frames = new List<AsyncProfilerStitchedFrame>
            {
                Sync(70),
                Sync(71, RuntimeFrameKind.TaskCompletionSourceResult),
                Sync(72),
            };
            var pipeline = new AsyncProfilerCallStackTransformPipeline
            {
                EnableNormalization = false,
                EnableInfrastructureCleanup = true,
            };

            pipeline.ApplyInPlace(Context(frames, diagnostics));

            Assert.Equal(new[] { CA(70), CA(71), CA(72) }, frames.Select(frame => frame.CodeAddress));
            Assert.Equal(0, diagnostics.AsyncInfrastructureFramesCollapsed);
        }

        [Fact]
        public void AsyncInfrastructureCleanup_PreservesCompletionChainsAcrossContextBoundaries()
        {
            AsyncProfilerCallStack leafContext = CallStack();
            AsyncProfilerCallStack rootContext = CallStack();
            var frames = new List<AsyncProfilerStitchedFrame>
            {
                Sync(60, RuntimeFrameKind.TaskContinuations, leafContext),
                Sync(61, RuntimeFrameKind.TaskResult, rootContext),
                Sync(62, RuntimeFrameKind.TaskCompletionSourceResult, rootContext),
            };
            AsyncProfilerStitchedFrame[] original = frames.ToArray();
            var diagnostics = new AsyncProfilerCallStackDiagnostics();
            new AsyncProfilerCallStackTransformPipeline
            {
                EnableNormalization = false,
                EnableInfrastructureCleanup = true,
            }.ApplyInPlace(Context(frames, diagnostics));

            Assert.Equal(original, frames);
            Assert.Equal(0, diagnostics.AsyncInfrastructureFramesCollapsed);
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public void AsyncInfrastructureCleanup_TruncatedPrefixBeforeCompleteChains_IsBoundsSafe(int chainCount)
        {
            var diagnostics = new AsyncProfilerCallStackDiagnostics();
            var frames = new List<AsyncProfilerStitchedFrame>
            {
                Sync(80, RuntimeFrameKind.TaskContinuations),
                Sync(81, RuntimeFrameKind.TaskResult),
            };
            for (int i = 0; i < chainCount; i++)
            {
                frames.Add(Sync(82, RuntimeFrameKind.TaskContinuations));
                frames.Add(Sync(83, RuntimeFrameKind.TaskResult));
                frames.Add(Sync(84, RuntimeFrameKind.TaskCompletionSourceResult));
            }
            var pipeline = new AsyncProfilerCallStackTransformPipeline
            {
                EnableNormalization = false,
                EnableInfrastructureCleanup = true,
            };

            pipeline.ApplyInPlace(Context(frames, diagnostics));

            Assert.Equal(CA(80), Assert.Single(frames).CodeAddress);
            Assert.Equal(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames, frames[0].Presentation);
            Assert.Equal(2 + 3 * chainCount, diagnostics.AsyncInfrastructureFramesCollapsed);
        }

        [Theory]
        [InlineData("", 0, "")]
        [InlineData("R", 1, "M")]
        [InlineData("RT", 2, "M")]
        [InlineData("RS", 1, "MS")]
        [InlineData("TRS", 2, "MS")]
        [InlineData("RATS", 2, "MAMS")]
        [InlineData("RSTR", 3, "MSM")]
        [InlineData("RTRTSS", 5, "MS")]
        public void AsyncInfrastructureCleanup_IncompleteChains_PreserveSetResult(string pattern, int collapsed, string expected)
        {
            var diagnostics = new AsyncProfilerCallStackDiagnostics();
            var frames = new List<AsyncProfilerStitchedFrame>();
            foreach (char frame in pattern)
            {
                FrameClassification kind = frame == 'R' ? (FrameClassification)RuntimeFrameKind.TaskContinuations
                    : frame == 'T' ? (FrameClassification)RuntimeFrameKind.TaskResult
                    : frame == 'S' ? (FrameClassification)RuntimeFrameKind.TaskCompletionSourceResult
                    : AsyncProfilerStitchSyncFrameKind.None;
                frames.Add(Sync(frame, kind));
            }
            var pipeline = new AsyncProfilerCallStackTransformPipeline
            {
                EnableNormalization = false,
                EnableInfrastructureCleanup = true,
            };

            pipeline.ApplyInPlace(Context(frames, diagnostics));

            Assert.Equal(expected, new string(frames.ConvertAll(frame =>
                frame.Presentation == AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames
                    ? 'M' : (char)frame.CodeAddress).ToArray()));
            Assert.Equal(collapsed, diagnostics.AsyncInfrastructureFramesCollapsed);
        }

        [Fact]
        public void CustomTransforms_RunAfterBuiltInsInRegistrationOrder()
        {
            var diagnostics = new AsyncProfilerCallStackDiagnostics();
            List<AsyncProfilerStitchedFrame> frames = StartupFrames();
            var observed = new List<int>();
            var pipeline = new AsyncProfilerCallStackTransformPipeline();
            pipeline.AddCustomTransform(context =>
            {
                observed.Add(context.Frames.Count);
                context.Frames.RemoveAt(0);
            });
            pipeline.AddCustomTransform(context => observed.Add(context.Frames.Count));

            pipeline.ApplyInPlace(Context(frames, diagnostics));

            Assert.Equal(new[] { 3, 2 }, observed);
            Assert.Equal(new[] { CA(11), CA(14) }, frames.Select(frame => frame.CodeAddress));
        }

#if NETCOREAPP
        [Fact]
        public void PublicCallbacks_FrameSearchAndRemovalDoNotAllocateAfterWarmup()
        {
            var pipeline = new AsyncProfilerCallStackTransformPipeline { EnableNormalization = false };
            var transforms = new AsyncCallStackTransforms(pipeline);
            var frames = new List<AsyncProfilerStitchedFrame>();
            for (int i = 0; i < 8; i++)
            {
                frames.Add(AsyncProfilerStitchedFrame.CreateSynthetic("Frame " + i));
            }
            var target = new AsyncProfilerCallStackFrame(new string("Frame 7".ToCharArray()));
            int indexSum = 0;
            int found = 0;
            int removed = 0;
            transforms.AddCustomTransform(context =>
            {
                indexSum += context.Frames.IndexOf(target);
                found += context.Frames.Contains(target) ? 1 : 0;
                removed += context.Frames.Remove(target) ? 1 : 0;
                context.Frames.Add(target);
            });
            AsyncProfilerCallStackTransformState state = Context(frames, new AsyncProfilerCallStackDiagnostics());
            const int Warmup = 100;
            const int Iterations = 1_000;
            for (int i = 0; i < Warmup; i++)
            {
                pipeline.ApplyInPlace(state);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < Iterations; i++)
            {
                pipeline.ApplyInPlace(state);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.Equal(0L, allocated);
            Assert.Equal(7 * (Warmup + Iterations), indexSum);
            Assert.Equal(Warmup + Iterations, found);
            Assert.Equal(Warmup + Iterations, removed);
            Assert.Equal(8, frames.Count);
        }

        [Fact]
        public void PublicCallbacks_DoNotAllocateAfterWarmup()
        {
            var pipeline = new AsyncProfilerCallStackTransformPipeline { EnableNormalization = false };
            var transforms = new AsyncCallStackTransforms(pipeline);
            int calls = 0;
            CodeAddressIndex observed = CodeAddressIndex.Invalid;
            transforms.AddCustomTransform(context =>
            {
                observed = context.Frames[0].CodeAddress;
                calls++;
            });
            AsyncProfilerCallStackTransformState state = Context(StartupFrames(), new AsyncProfilerCallStackDiagnostics());
            const int Warmup = 100;
            const int Iterations = 1_000;
            for (int i = 0; i < Warmup; i++)
            {
                pipeline.ApplyInPlace(state);
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < Iterations; i++)
            {
                pipeline.ApplyInPlace(state);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.Equal(0L, allocated);
            Assert.Equal(Warmup + Iterations, calls);
            Assert.Equal(CA(10), observed);
        }
#endif

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void PublicCallbacks_ReuseViewsAndReleaseBorrowedState(bool failFirst)
        {
            var pipeline = new AsyncProfilerCallStackTransformPipeline { EnableNormalization = false };
            var transforms = new AsyncCallStackTransforms(pipeline);
            var failure = new InvalidOperationException("Public callback failed.");
            AsyncProfilerCallStackTransformContext reusedContext = null;
            IList<AsyncProfilerCallStackFrame> reusedFrames = null;
            AsyncProfilerCallStackTransformState expected = default;
            int calls = 0;
            transforms.AddCustomTransform(context =>
            {
                if (reusedContext == null)
                {
                    reusedContext = context;
                    reusedFrames = context.Frames;
                }
                Assert.Same(reusedContext, context);
                Assert.Same(reusedFrames, context.Frames);
                Assert.Same(expected.Segments, context.Segments);
                Assert.Equal(expected.TimestampQpc, context.TimestampQpc);
                Assert.Equal(expected.Frames[0].CodeAddress, context.Frames[0].CodeAddress);
                context.Frames.Add(new AsyncProfilerCallStackFrame("Injected"));
                context.Frames.RemoveAt(0);
                calls++;
                if (failFirst && calls == 1)
                {
                    throw failure;
                }
            });

            for (int i = 0; i < 2; i++)
            {
                List<AsyncProfilerStitchedFrame> frames = StartupFrames();
                frames[0] = Sync(20 + i);
                expected = Context(frames, new AsyncProfilerCallStackDiagnostics(), timestampQpc: i + 1);
                if (failFirst && i == 0)
                {
                    Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => pipeline.ApplyInPlace(expected)));
                }
                else
                {
                    pipeline.ApplyInPlace(expected);
                }

                Assert.Null(reusedContext.Segments);
                Assert.Throws<InvalidOperationException>(() => reusedFrames.Count);
                Assert.Equal("Injected", frames[frames.Count - 1].DisplayName);
            }
            Assert.Equal(2, calls);
        }

        [Fact]
        public void Pipeline_RecomposesWhenFlagsChange()
        {
            var pipeline = new AsyncProfilerCallStackTransformPipeline();
            var observed = new List<int>();
            pipeline.AddCustomTransform(context => observed.Add(context.Frames.Count));
            bool[,] configurations =
            {
                { true, false },
                { false, false },
                { false, true },
                { true, true },
                { true, false },
                { false, false },
                { true, false },
            };

            for (int configuration = 0; configuration < configurations.GetLength(0); configuration++)
            {
                bool enableNormalization = configurations[configuration, 0];
                bool enableInfrastructureCleanup = configurations[configuration, 1];
                pipeline.EnableNormalization = enableNormalization;
                pipeline.EnableInfrastructureCleanup = enableInfrastructureCleanup;
                int[] expected = !enableNormalization && !enableInfrastructureCleanup
                    ? new[] { 20, 10, 11, 12, 13, 14, 15, 16, 17 }
                    : enableInfrastructureCleanup
                        ? enableNormalization ? new[] { 10, 11, 14, 15, 16, 17 } : new[] { 20, 10, 11, 12, 14, 15, 16, 17 }
                        : new[] { 10, 11, 14, 15, 16, 17 };

                for (int sample = 0; sample < 2; sample++)
                {
                    observed.Clear();
                    var diagnostics = new AsyncProfilerCallStackDiagnostics();
                    List<AsyncProfilerStitchedFrame> frames = CompositionFrames();

                    pipeline.ApplyInPlace(Context(frames, diagnostics, wrapperCodeAddress: CA(20), usesV2SynchronousLayout: true));

                    Assert.Equal(expected.Select(CA), frames.Select(frame => frame.CodeAddress));
                    Assert.Equal(new[] { expected.Length }, observed);
                    Assert.Equal(enableNormalization ? 1 : 0, diagnostics.V2LeafWrapperDropped);
                    Assert.Equal(enableNormalization ? 1 : 0, diagnostics.V1SynchronousMoveNextFramesNormalized);
                    Assert.Equal(enableNormalization ? 2 : 0, diagnostics.V1SynchronousStartupFramesCollapsed);
                    Assert.Equal(enableInfrastructureCleanup ? (enableNormalization ? 1 : 4) : 0,
                        diagnostics.AsyncInfrastructureFramesCollapsed);
                    Assert.Equal(
                        enableNormalization ? AsyncProfilerStitchedFramePresentation.LogicalStateMachineMethod
                            : AsyncProfilerStitchedFramePresentation.Native,
                        frames.Single(frame => frame.CodeAddress == CA(11)).Presentation);
                }
            }
        }

        [Fact]
        public void CustomTransforms_AddedAfterFirstSample_RunInRegistrationOrder()
        {
            var pipeline = new AsyncProfilerCallStackTransformPipeline { EnableNormalization = false };
            var observed = new List<int>();
            pipeline.ApplyInPlace(Context(StartupFrames(), new AsyncProfilerCallStackDiagnostics()));

            pipeline.AddCustomTransform(context =>
            {
                observed.Add(context.Frames.Count);
                context.Frames.RemoveAt(0);
            });
            pipeline.ApplyInPlace(Context(StartupFrames(), new AsyncProfilerCallStackDiagnostics()));
            Assert.Equal(new[] { 5 }, observed);

            pipeline.AddCustomTransform(context => observed.Add(context.Frames.Count));
            observed.Clear();
            pipeline.ApplyInPlace(Context(StartupFrames(), new AsyncProfilerCallStackDiagnostics()));
            Assert.Equal(new[] { 5, 4 }, observed);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ClearCustomTransforms_PreservesEnabledBuiltIns(bool enableInfrastructureCleanup)
        {
            var pipeline = new AsyncProfilerCallStackTransformPipeline { EnableInfrastructureCleanup = enableInfrastructureCleanup };
            pipeline.AddCustomTransform(context => context.Frames.Clear());
            List<AsyncProfilerStitchedFrame> frames = CompositionFrames();
            pipeline.ApplyInPlace(Context(frames, new AsyncProfilerCallStackDiagnostics(), wrapperCodeAddress: CA(20)));
            Assert.Empty(frames);

            pipeline.ClearCustomTransforms();
            var diagnostics = new AsyncProfilerCallStackDiagnostics();
            frames = CompositionFrames();
            pipeline.ApplyInPlace(Context(frames, diagnostics, wrapperCodeAddress: CA(20), usesV2SynchronousLayout: true));

            int[] expected = { 10, 11, 14, 15, 16, 17 };
            Assert.Equal(expected.Select(CA), frames.Select(frame => frame.CodeAddress));
            Assert.True(pipeline.EnableNormalization);
            Assert.Equal(enableInfrastructureCleanup, pipeline.EnableInfrastructureCleanup);
            Assert.Equal(1, diagnostics.V2LeafWrapperDropped);
            Assert.Equal(1, diagnostics.V1SynchronousMoveNextFramesNormalized);
            Assert.Equal(2, diagnostics.V1SynchronousStartupFramesCollapsed);
            Assert.Equal(enableInfrastructureCleanup ? 1 : 0, diagnostics.AsyncInfrastructureFramesCollapsed);
        }

        [Fact]
        public void BuiltInComponents_CanBeRegisteredThroughExistingDelegateAPI()
        {
            var pipeline = new AsyncProfilerCallStackTransformPipeline { EnableNormalization = false };
            pipeline.AddCustomTransform(new AsyncProfilerSampledV2WrapperTransformPass().Apply);
            pipeline.AddCustomTransform(new AsyncProfilerReentrantHandoffTransformPass().Apply);
            pipeline.AddCustomTransform(new AsyncProfilerV1PresentationTransformPass().Apply);
            pipeline.AddCustomTransform(new AsyncProfilerInfrastructureCleanupTransformPass().Apply);

            for (int sample = 0; sample < 2; sample++)
            {
                var diagnostics = new AsyncProfilerCallStackDiagnostics();
                List<AsyncProfilerStitchedFrame> frames = CompositionFrames();

                pipeline.ApplyInPlace(Context(frames, diagnostics, wrapperCodeAddress: CA(20), usesV2SynchronousLayout: true));

                Assert.Equal(new[] { CA(10), CA(11), CA(14), CA(15), CA(16), CA(17) },
                    frames.Select(frame => frame.CodeAddress));
                Assert.Equal(1, diagnostics.V2LeafWrapperDropped);
                Assert.Equal(1, diagnostics.V1SynchronousMoveNextFramesNormalized);
                Assert.Equal(2, diagnostics.V1SynchronousStartupFramesCollapsed);
                Assert.Equal(1, diagnostics.AsyncInfrastructureFramesCollapsed);
            }
        }

        [Fact]
        public void AddCustomTransform_RejectsNullTransform()
        {
            var pipeline = new AsyncProfilerCallStackTransformPipeline();

            Assert.Throws<ArgumentNullException>(() => pipeline.AddCustomTransform(null));
        }

        [Fact]
        public void CustomTransformFailure_PropagatesAndStopsThePipeline()
        {
            var pipeline = new AsyncProfilerCallStackTransformPipeline();
            var failure = new InvalidOperationException("Custom transform failed.");
            bool laterTransformRan = false;
            pipeline.AddCustomTransform(_ => throw failure);
            pipeline.AddCustomTransform(_ => laterTransformRan = true);

            InvalidOperationException actual = Assert.Throws<InvalidOperationException>(
                () => pipeline.ApplyInPlace(Context(StartupFrames(), new AsyncProfilerCallStackDiagnostics())));

            Assert.Same(failure, actual);
            Assert.False(laterTransformRan);
        }

        [Fact]
        public void Transform_AppliesPipelineWithoutComputerAndPreservesStructuralFrames()
        {
            var diagnostics = new AsyncProfilerCallStackDiagnostics();
            List<AsyncProfilerStitchedFrame> structuralFrames = StartupFrames();
            var structuralResult = new AsyncProfilerStitchResult(
                structuralFrames, diagnostics, new AsyncProfilerCallStackStitchingState());
            var pipeline = new AsyncProfilerCallStackTransformPipeline();

            AsyncProfilerStitchResult transformed = pipeline.Transform(
                structuralResult,
                segments: new List<AsyncProfilerCallStack>(),
                timestampQpc: 1,
                classifyBoundary: _ => AsyncProfilerCallStackStitchBoundaryInfo.None);

            Assert.Equal(
                new[] { CA(10), CA(11), CA(12), CA(13), CA(14) },
                structuralResult.Frames.Select(frame => frame.CodeAddress));
            Assert.Equal(
                new[] { CA(10), CA(11), CA(14) },
                transformed.Frames.Select(frame => frame.CodeAddress));
            Assert.Equal(
                AsyncProfilerStitchedFramePresentation.LogicalStateMachineMethod,
                transformed.Frames[1].Presentation);
            Assert.Same(structuralResult.Diagnostics, transformed.Diagnostics);
        }

        #region private

        private static List<AsyncProfilerStitchedFrame> CompositionFrames()
        {
            List<AsyncProfilerStitchedFrame> frames = StartupFrames();
            frames.Insert(0, Sync(20));
            frames.Add(Sync(15, RuntimeFrameKind.V1AwaiterRegistration));
            frames.Add(Sync(16, RuntimeFrameKind.TaskCompletionSourceResult));
            frames.Add(Sync(17, RuntimeFrameKind.SchedulerEntry));
            return frames;
        }

        private static List<AsyncProfilerStitchedFrame> StartupFrames() =>
            new List<AsyncProfilerStitchedFrame>
            {
                Sync(10),
                Sync(11, AsyncProfilerStitchSyncFrameKind.V1StateMachineBody),
                Sync(12, RuntimeFrameKind.V1Startup),
                Sync(13, RuntimeFrameKind.V1Startup),
                Sync(14),
            };

        private static AsyncProfilerCallStackTransformState Context(
            List<AsyncProfilerStitchedFrame> frames, AsyncProfilerCallStackDiagnostics diagnostics,
            CodeAddressIndex wrapperCodeAddress = CodeAddressIndex.Invalid,
            CodeAddressIndex infrastructureCodeAddress = CodeAddressIndex.Invalid,
            Func<CodeAddressIndex, AsyncProfilerCallStackStitchBoundaryInfo> classifyBoundary = null,
            long timestampQpc = 1, bool usesV2SynchronousLayout = false) =>
            new AsyncProfilerCallStackTransformState(
                traceLog: null, segments: new List<AsyncProfilerCallStack>(), timestampQpc,
                classifyBoundary: classifyBoundary ?? (codeAddress =>
                {
                    if (codeAddress == wrapperCodeAddress)
                    {
                        return new AsyncProfilerCallStackStitchBoundaryInfo(
                            AsyncProfilerCallStackStitchBoundaryKind.V2Dispatcher, wrapperIndex: 0);
                    }
                    if (codeAddress == infrastructureCodeAddress)
                    {
                        return new AsyncProfilerCallStackStitchBoundaryInfo(
                            AsyncProfilerCallStackStitchBoundaryKind.V1DispatcherInfrastructure, wrapperIndex: -1);
                    }
                    return AsyncProfilerCallStackStitchBoundaryInfo.None;
                }),
                new AsyncProfilerCallStackStitchingState { UsesV2SynchronousLayout = usesV2SynchronousLayout },
                diagnostics, frames);

        private static CodeAddressIndex CA(int value) => (CodeAddressIndex)value;

        #endregion
    }
}
