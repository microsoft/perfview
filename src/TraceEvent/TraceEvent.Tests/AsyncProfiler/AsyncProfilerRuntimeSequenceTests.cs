// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Diagnostics.Tracing.Computers;
using Microsoft.Diagnostics.Tracing.Etlx;

using Xunit;

using static Microsoft.Diagnostics.Tracing.Computers.AsyncProfilerRuntimeSupport;

namespace TraceEventTests
{
    /// <summary>Checks runtime-specific sequence matching and protection of rejected spans.</summary>
    public class AsyncProfilerRuntimeSequenceTests
    {
        [Theory]
        [InlineData("RT", (int)AsyncProfilerRuntimeSequenceKind.TaskCompletion, 2)]
        [InlineData("RTS", (int)AsyncProfilerRuntimeSequenceKind.TaskCompletionChain, 3)]
        [InlineData("BRTID", (int)AsyncProfilerRuntimeSequenceKind.V2CompletionHandoff, 5)]
        [InlineData("R", (int)AsyncProfilerRuntimeSequenceKind.TaskCompletion, 0)]
        [InlineData("RATS", (int)AsyncProfilerRuntimeSequenceKind.TaskCompletionChain, 0)]
        [InlineData("BRTII", (int)AsyncProfilerRuntimeSequenceKind.V2CompletionHandoff, 0)]
        public void CommonAndCoreClr11_PreserveExistingLayouts(string pattern, int kind, int length)
        {
            var fixture = new SequenceFixture(pattern);
            foreach (AsyncProfilerRuntimeSupport support in new[] { AsyncProfilerRuntimeSupport.Common, AsyncProfilerRuntimeSupport.CoreClr11 })
            {
                AsyncProfilerRuntimeSequenceMatch match = fixture.Context(_ => support).MatchRuntimeSequence(
                    (AsyncProfilerRuntimeSequenceKind)kind, 0, fixture.Frames.Count);
                Assert.Equal(length, match.Length);
                Assert.Equal(length != 0, match.IsMatch);
                Assert.False(match.IsRejected);
            }
        }

        [Theory]
        [InlineData("RHT", (int)AsyncProfilerRuntimeSequenceKind.TaskCompletion, 3)]
        [InlineData("RHTS", (int)AsyncProfilerRuntimeSequenceKind.TaskCompletionChain, 4)]
        [InlineData("BRHTID", (int)AsyncProfilerRuntimeSequenceKind.V2CompletionHandoff, 6)]
        public void ExtendedTaskCompletion_IsReusedByChainsAndCompletedDispatchTails(string pattern, int kind, int length)
        {
            var fixture = new SequenceFixture(pattern);
            AsyncProfilerRuntimeSequenceMatch match = fixture.Context().MatchRuntimeSequence(
                (AsyncProfilerRuntimeSequenceKind)kind, 0, fixture.Frames.Count);
            Assert.True(match.IsMatch);
            Assert.Equal(length, match.Length);
        }

        [Theory]
        [InlineData("XRATSXRHTSXRHTS", "RATS")]
        [InlineData("RATSXRHTSXRATSXRHTS", "RATSRATS")]
        [InlineData("R", "R")]
        [InlineData("RH", "RH")]
        [InlineData("RHT", "RHT")]
        [InlineData("RHTA", "RHTA")]
        [InlineData("RUTS", "RUTS")]
        [InlineData("BRATID", "BRATID")]
        [InlineData("BRHTII", "BRHTII")]
        [InlineData("BRHTI", "BRHTI")]
        public void RejectedCandidates_StayProtectedWhileOtherSpansAreGrouped(string pattern, string expected)
        {
            foreach (bool enableNormalization in new[] { false, true })
            {
                var fixture = new SequenceFixture(pattern);
                new AsyncProfilerCallStackTransformPipeline
                {
                    EnableNormalization = enableNormalization,
                    EnableInfrastructureCleanup = true,
                }.ApplyInPlace(fixture.Context());
                Assert.Equal(expected, fixture.RemainingPhysicalPattern);
                Assert.Equal(pattern.Length - expected.Length, fixture.Diagnostics.AsyncInfrastructureFramesCollapsed);
            }
        }

        [Fact]
        public void OnePipeline_UsesEachCandidatesSupportAndRejectsCrossSupportSpans()
        {
            var fixture = new SequenceFixture("RHTSRTS");
            var pipeline = new AsyncProfilerCallStackTransformPipeline { EnableInfrastructureCleanup = true };
            pipeline.ApplyInPlace(fixture.Context(ca =>
                (int)ca < 4 ? fixture.Support : AsyncProfilerRuntimeSupport.Common));
            Assert.Equal(new[] { 0, 4 }, fixture.Frames.Select(frame => (int)frame.CodeAddress));
            Assert.All(fixture.Frames, frame =>
                Assert.Equal(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames, frame.Presentation));
            Assert.Equal(7, fixture.Diagnostics.AsyncInfrastructureFramesCollapsed);

            fixture = new SequenceFixture("RHTS");
            pipeline.ApplyInPlace(fixture.Context(ca =>
                (int)ca == 1 ? AsyncProfilerRuntimeSupport.Common : fixture.Support));
            Assert.Equal("RHTS", fixture.RemainingPhysicalPattern);
            Assert.Equal(0, fixture.Diagnostics.AsyncInfrastructureFramesCollapsed);
        }

        [Fact]
        public void SequenceBounds_DoNotCrossProtectedEndsAndInvalidMatchersThrow()
        {
            var fixture = new SequenceFixture("RHTS");
            AsyncProfilerCallStackTransformState context = fixture.Context();
            Assert.False(context.MatchRuntimeSequence(AsyncProfilerRuntimeSequenceKind.TaskCompletionChain, 0, 3).IsMatch);
            Assert.Equal(0, context.MatchRuntimeSequence(AsyncProfilerRuntimeSequenceKind.TaskCompletionChain, 4, 4).Length);
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                context.MatchRuntimeSequence(AsyncProfilerRuntimeSequenceKind.TaskCompletionChain, 0, 5));
            Assert.Throws<InvalidOperationException>(() =>
                fixture.Context(_ => new OutOfBoundsSupport()).MatchRuntimeSequence(
                    AsyncProfilerRuntimeSequenceKind.TaskCompletionChain, 0, 4));
        }

        [Theory]
        [InlineData("System.Runtime.CompilerServices.AsyncTaskMethodBuilder.AwaitUnsafeOnCompleted()", true)]
        [InlineData("System.Runtime.CompilerServices.TaskAwaiter.OnCompleted()", true)]
        [InlineData("System.Runtime.CompilerServices.TaskAwaiter.get_IsCompleted()", false)]
        [InlineData("System.Runtime.CompilerServices.AsyncMethodBuilderCore.Start()", false)]
        public void SupportOnlyDetails_PreserveRegistrationMatchingAfterPresentationChanges(string name, bool registration)
        {
            AsyncProfilerRuntimeSupport support = AsyncProfilerRuntimeSupport.Common;
            FrameClassification classification = support.ClassifySynchronousFrame(name, true);
            Assert.Equal(AsyncProfilerStitchSyncFrameKind.None, classification.Kind);
            var frames = new List<AsyncProfilerStitchedFrame>
            {
                AsyncProfilerStitchedFrame.CreateSync(
                    new AsyncProfilerStitchSyncFrame((CodeAddressIndex)1, MethodIndex.Invalid, classification), null)
                    .WithPresentation(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames),
            };
            var context = new AsyncProfilerCallStackTransformState(
                null, new List<AsyncProfilerCallStack>(), 1, _ => AsyncProfilerCallStackStitchBoundaryInfo.None,
                new AsyncProfilerCallStackStitchingState(), new AsyncProfilerCallStackDiagnostics(), frames);

            AsyncProfilerRuntimeSequenceMatch match = context.MatchRuntimeSequence(
                AsyncProfilerRuntimeSequenceKind.V1AwaitRegistration, 0, 1);
            Assert.Equal(registration, match.IsMatch);
            Assert.Equal(registration ? 1 : 0, match.Length);
        }

        [Theory]
        [InlineData("AsyncCallstack.CaptureStateMachineAsyncCallstack()", false)]
        [InlineData("AsyncCallstack.EmitAsyncCallstack()", false)]
        [InlineData("ResumeAsyncContext.Resume()", true)]
        public void SharedProfilerEntryRole_DoesNotMakeEntryPhasesInterchangeable(string method, bool entry)
        {
            AsyncProfilerRuntimeSupport support = AsyncProfilerRuntimeSupport.Common;
            FrameClassification classification = support.ClassifySynchronousFrame(
                "System.Runtime.CompilerServices.AsyncProfiler+" + method, true);
            Assert.Equal(AsyncProfilerStitchSyncFrameKind.ProfilerEntry, classification.Kind);
            var frames = new List<AsyncProfilerStitchedFrame>
            {
                AsyncProfilerStitchedFrame.CreateSync(
                    new AsyncProfilerStitchSyncFrame((CodeAddressIndex)1, MethodIndex.Invalid, classification), null),
                AsyncProfilerStitchedFrame.CreateSync(
                    new AsyncProfilerStitchSyncFrame((CodeAddressIndex)2, MethodIndex.Invalid), null),
            };
            var context = new AsyncProfilerCallStackTransformState(
                null, new List<AsyncProfilerCallStack>(), 1, _ => AsyncProfilerCallStackStitchBoundaryInfo.None,
                new AsyncProfilerCallStackStitchingState(), new AsyncProfilerCallStackDiagnostics(), frames);

            Assert.True(AsyncProfilerCallStackTransformHelpers.IsSyncFrameKind(
                frames[0], AsyncProfilerStitchSyncFrameKind.ProfilerEntry));
            AsyncProfilerRuntimeSequenceMatch match = context.MatchRuntimeSequence(
                AsyncProfilerRuntimeSequenceKind.ProfilerEntry, 0, 1);
            Assert.Equal(entry, match.IsMatch);
            Assert.Equal(entry ? 1 : 0, match.Length);
        }

        #region private

        private sealed class SequenceFixture
        {
            public SequenceFixture(string pattern)
            {
                m_pattern = pattern;
                Support = new AsyncProfilerExtendedCompletionRuntimeSupport((context, frame) =>
                    pattern[(int)frame.CodeAddress] == 'H'
                        ? AsyncProfilerExtendedCompletionRuntimeSupport.InjectedHelperName : "Unknown.Helper");
                for (int i = 0; i < pattern.Length; i++)
                {
                    FrameClassification kind;
                    switch (pattern[i])
                    {
                        case 'R':
                            kind = RuntimeFrameKind.TaskContinuations;
                            break;
                        case 'T':
                            kind = RuntimeFrameKind.TaskResult;
                            break;
                        case 'S':
                            kind = RuntimeFrameKind.TaskCompletionSourceResult;
                            break;
                        case 'B':
                            kind = AsyncProfilerStitchSyncFrameKind.ContinuationBridge;
                            break;
                        case 'X':
                            kind = RuntimeFrameKind.V1AwaiterRegistration;
                            break;
                        default:
                            kind = AsyncProfilerStitchSyncFrameKind.None;
                            break;
                    }
                    // I/D are distinct dispatch methods; repeated I models the same dispatch method twice.
                    Frames.Add(AsyncProfilerStitchedFrame.CreateSync(
                        new AsyncProfilerStitchSyncFrame((CodeAddressIndex)i,
                            (MethodIndex)(pattern[i] == 'I' ? 100 : pattern[i] == 'D' ? 101 : i), kind), null));
                }
            }

            public AsyncProfilerExtendedCompletionRuntimeSupport Support { get; }
            public List<AsyncProfilerStitchedFrame> Frames { get; } = new List<AsyncProfilerStitchedFrame>();
            public AsyncProfilerCallStackDiagnostics Diagnostics { get; } = new AsyncProfilerCallStackDiagnostics();
            public string RemainingPhysicalPattern => new string(Frames
                .Where(frame => frame.Presentation != AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames)
                .Select(frame => m_pattern[(int)frame.CodeAddress]).ToArray());

            public AsyncProfilerCallStackTransformState Context(Func<CodeAddressIndex, AsyncProfilerRuntimeSupport> runtimeSupportResolver = null) =>
                new AsyncProfilerCallStackTransformState(null, new List<AsyncProfilerCallStack>(), 1,
                    ca => m_pattern[(int)ca] == 'I' || m_pattern[(int)ca] == 'D'
                        ? new AsyncProfilerCallStackStitchBoundaryInfo(AsyncProfilerCallStackStitchBoundaryKind.V2DispatcherInfrastructure, -1)
                        : AsyncProfilerCallStackStitchBoundaryInfo.None,
                    new AsyncProfilerCallStackStitchingState(), Diagnostics, Frames, runtimeSupportResolver ?? (_ => Support));

            #region private

            private readonly string m_pattern;

            #endregion
        }

        private sealed class OutOfBoundsSupport : AsyncProfilerRuntimeSupport
        {
            public OutOfBoundsSupport() : base("Invalid test profile")
            {
            }

            protected override AsyncProfilerRuntimeSequenceMatch MatchTaskCompletionChain(
                AsyncProfilerCallStackTransformState context, int start, int end) =>
                AsyncProfilerRuntimeSequenceMatch.Match(end - start + 1);
        }

        #endregion
    }
}
