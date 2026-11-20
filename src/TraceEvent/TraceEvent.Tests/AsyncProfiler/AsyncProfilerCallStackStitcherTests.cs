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

namespace TraceEventTests
{
    /// <summary>
    /// Tests structural reconstruction with synthetic leaf-to-root frames and recorded call stacks.
    /// Independent code-address and method identities allow unresolved symbols to be tested without a trace.
    /// </summary>
    public partial class AsyncProfilerCallStackStitcherTests
    {
        [Theory]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, false, false)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, false, false)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, true, false)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, true, false)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, false, true)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, false, true)]
        public void SupportedStructuralInfrastructure_RecognizesInsertedHelpersButPreservesForeignSpansAndBoundaries(
            int kindValue, bool foreignSupport, bool crossesBoundary)
        {
            var kind = (AsyncCallstackKind)kindValue;
            var s = new Scenario();
            var support = new StructuralRulesSupport(2);
            AsyncProfilerCallStack segment = s.Segment(kind, new[] { 800, 801 }, new[] { 10, 11 });
            var sync = new[]
            {
                s.Sync(50, 10),
                s.Boundary(51, kind == AsyncCallstackKind.StateMachineAsync ? s_v1Dispatcher : Wrapper(0)),
                s.Sync(52, 90, AsyncProfilerStitchSyncFrameKind.V1MethodHelper),
                s.Boundary(53, crossesBoundary ? s_v1Dispatcher :
                    kind == AsyncCallstackKind.StateMachineAsync ? s_v1Infrastructure : s_dispatchContinuation),
                s.Sync(54, 99),
            };
            AsyncProfilerStitchResult common = s.Run(sync, new[] { segment });
            Assert.Contains(common.Frames, frame => frame.CodeAddress == Scenario.CA(52));
            AsyncProfilerStitchResult changed = s.Run(sync, new[] { segment }, support,
                ca => foreignSupport && ca == Scenario.CA(52) ? AsyncProfilerRuntimeSupport.Common : support);
            Assert.Equal(foreignSupport || crossesBoundary,
                changed.Frames.Any(frame => frame.CodeAddress == Scenario.CA(52)));
            Assert.Contains(changed.Frames, frame => frame.CodeAddress == Scenario.CA(54));
            Assert.Contains(changed.Frames, frame => frame.CodeAddress == Scenario.CA(801));
        }

        [Fact]
        public void SupportedStructuralInfrastructure_InvalidBoundsThrow()
        {
            var s = new Scenario();
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.StateMachineAsync, new[] { 800 }, new[] { 10 });
            var sync = new[]
            {
                s.Sync(50, 10), s.Boundary(51, s_v1Dispatcher),
                s.Sync(52, 90, AsyncProfilerStitchSyncFrameKind.V1MethodHelper),
            };
            Assert.Throws<InvalidOperationException>(() =>
                s.Run(sync, new[] { segment }, new StructuralRulesSupport(10)));
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void V1BoxCarrier_RejectsCallbacksAndPreservesPhysicalBodyPrecedence(bool isCallback, bool physicalBody)
        {
            var s = new Scenario();
            AsyncProfilerCallStack segment = s.Segment(AsyncCallstackKind.StateMachineAsync,
                new[] { 800, 801 }, new[] { 10, 11 });
            var sync = new[]
            {
                s.Sync(50, physicalBody ? 10 : 90),
                s.BoxCarrier(51, 10,
                    isCallback ? AsyncProfilerStitchSyncFrameKind.V1BoxCallback : AsyncProfilerStitchSyncFrameKind.None),
                s.Boundary(52, s_v1Dispatcher), s.Sync(53, 99),
            };
            AsyncProfilerStitchResult result = s.Run(sync, new[] { segment });
            Assert.Equal(!isCallback && !physicalBody, result.Frames.Any(frame => frame.IsStateMachineBoxCarrier));
            Assert.Contains(result.Frames, frame => frame.CodeAddress == Scenario.CA(50));
        }

        [Fact]
        public void StitchInto_ReusesAndClearsCallerOwnedBuffers()
        {
            var s = new Scenario();
            AsyncProfilerCallStack segment = s.Segment(
                AsyncCallstackKind.RuntimeAsync,
                frameCodeAddrs: new[] { 200, 201 },
                frameMethods: new[] { 50, 51 });
            var stitchedSync = new[]
            {
                s.Sync(10, 50),
                s.Boundary(11, Wrapper(0)),
                s.Sync(12, 99),
            };
            var output = new List<AsyncProfilerStitchedFrame>();
            var diagnostics = new AsyncProfilerCallStackDiagnostics();
            var stitchingState = new AsyncProfilerCallStackStitchingState();
            long completionPolicyQpc = long.MinValue;

            AsyncProfilerCallStackStitcher.StitchInto(
                stitchedSync,
                new[] { segment },
                Qpc,
                s.Classify,
                (processIndex, kind, callStackStartQpc) =>
                {
                    completionPolicyQpc = callStackStartQpc;
                    return s.MethodCompletionObserved(kind);
                },
                (ProcessIndex)1,
                s.MethodOf,
                trace: false,
                output,
                diagnostics,
                stitchingState);

            Assert.Equal(
                new[] { Scenario.CA(10), Scenario.CA(201), Scenario.CA(12) },
                output.Select(frame => frame.CodeAddress));
            Assert.Equal(1, diagnostics.SegmentsProcessed);
            Assert.Equal(segment.StartQpc, completionPolicyQpc);

            var passthroughSync = new[] { s.Sync(20, 60), s.Sync(21, 61) };
            AsyncProfilerCallStackStitcher.StitchInto(
                passthroughSync,
                Array.Empty<AsyncProfilerCallStack>(),
                Qpc,
                s.Classify,
                (processIndex, kind, callStackStartQpc) => s.MethodCompletionObserved(kind),
                (ProcessIndex)1,
                s.MethodOf,
                trace: false,
                output,
                diagnostics,
                stitchingState);

            Assert.Equal(
                new[] { Scenario.CA(20), Scenario.CA(21) },
                output.Select(frame => frame.CodeAddress));
            Assert.Equal(0, diagnostics.SegmentsProcessed);
            Assert.Equal(0, diagnostics.V2WrapperFallbackUsed);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, false)]
        [InlineData(false, true)]
        public void NoUsableSegments_IsVerbatimPassthrough(bool wrapperIsLeaf, bool emptySegment)
        {
            // Without async coverage, retain wrappers and infrastructure so the gap remains visible.
            var s = new Scenario();
            var sync = wrapperIsLeaf
                ? new[] { s.Boundary(80, Wrapper(0)), s.Boundary(81, s_dispatchContinuation), s.Sync(82, 99), s.Sync(83, 98) }
                : new[] { s.Sync(70, 50), s.Boundary(71, Wrapper(0)), s.Boundary(72, s_dispatchContinuation), s.Sync(73, 98) };
            AsyncProfilerCallStack[] segments = emptySegment
                ? new[] { s.Segment(AsyncCallstackKind.RuntimeAsync, Array.Empty<int>(), Array.Empty<int>()) }
                : Array.Empty<AsyncProfilerCallStack>();

            AsyncProfilerStitchResult result = s.Run(sync, segments);

            Assert.Equal(sync.Select(frame => AsyncProfilerStitchedFrame.CreateSync(frame, null)), result.Frames);
            Assert.Equal(0, result.Diagnostics.SegmentsProcessed);
            Assert.Equal(0, result.Diagnostics.BoundariesNotFound);
            Assert.Equal(0, result.Diagnostics.V2LeafWrapperDropped);
        }

        [Fact]
        public void MissingBoundary_DegradesToAppendOnly_WithDiagnostic()
        {
            var s = new Scenario();
            s.MarkMethodCompletionObserved(AsyncCallstackKind.RuntimeAsync);
            AsyncProfilerCallStack seg = s.Segment(AsyncCallstackKind.RuntimeAsync,
                frameCodeAddrs: new[] { 600, 601 },
                frameMethods:   new[] {  80,  81 },
                completions: Completed(0)); // current=frames[0]; callers=frames[1]

            var sync = new[] { s.Sync(10, 1), s.Sync(11, 2) }; // no boundary frame present

            AsyncProfilerStitchResult result = s.Run(sync, new[] { seg }, trace: true);

            // All sync frames verbatim, then the remaining async callers appended.
            Assert.Equal(3, result.Frames.Count);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, result.Frames[0].Origin);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.Sync, result.Frames[1].Origin);
            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncCaller, result.Frames[2].Origin);
            Assert.Equal(Scenario.CA(601), result.Frames[2].CodeAddress);
            Assert.Equal(1, result.Diagnostics.BoundariesNotFound);
            Assert.Contains(result.Diagnostics.Messages, m => m.Contains("segment[0]") && m.Contains("boundary=no-boundary"));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Trace_EmitsPerSegmentHappyPathNoteOnlyWhenEnabled(bool trace)
        {
            var s = new Scenario();
            s.MarkMethodCompletionObserved(AsyncCallstackKind.RuntimeAsync);
            AsyncProfilerCallStack seg = s.Segment(AsyncCallstackKind.RuntimeAsync,
                frameCodeAddrs: new[] { 200, 201, 202, 203, 204 },
                frameMethods:   new[] {  50,  51,  52,  53,  54 },
                completions: Completed(2));
            var sync = new[] { s.Sync(90, 52), s.Boundary(91, Wrapper(3)), s.Sync(92, 99) };

            AsyncProfilerStitchResult result = s.Run(sync, new[] { seg }, trace);

            if (!trace)
            {
                Assert.Empty(result.Diagnostics.Messages);
                return;
            }

            string note = Assert.Single(result.Diagnostics.Messages);
            Assert.Contains("segment[0]", note);
            Assert.Contains($"kind={AsyncCallstackKind.RuntimeAsync}", note);
            Assert.Contains($"{AsyncProfilerCallStackStitchBoundaryKind.V2Dispatcher}[3]", note); // boundary + wrapper index
            Assert.Contains("completed=2", note);
            Assert.Contains("spliced=[3..5)", note);                                       // async callers A1,A2
        }

        #region private

        private sealed class Scenario
        {
            public Func<CodeAddressIndex, MethodIndex> MethodOf =>
                ca => _methodOf.TryGetValue(ca, out MethodIndex method) ? method : MethodIndex.Invalid;

            public Func<CodeAddressIndex, AsyncProfilerCallStackStitchBoundaryInfo> Classify =>
                ca => _classify.TryGetValue(ca, out AsyncProfilerCallStackStitchBoundaryInfo boundary)
                    ? boundary : AsyncProfilerCallStackStitchBoundaryInfo.None;

            public Func<AsyncCallstackKind, bool> MethodCompletionObserved => k => _methodCompletionObserved.Contains(k);

            public static CodeAddressIndex CA(int i) => (CodeAddressIndex)i;
            public static MethodIndex MI(int i) => (MethodIndex)i;

            public void MarkMethodCompletionObserved(AsyncCallstackKind kind) => _methodCompletionObserved.Add(kind);

            public void RegisterMethod(int codeAddr, int method) =>
                _methodOf[CA(codeAddr)] = MI(method);

            public AsyncProfilerStitchSyncFrame Sync(int codeAddr, int method) => new AsyncProfilerStitchSyncFrame(CA(codeAddr), MI(method));

            public AsyncProfilerStitchSyncFrame Sync(int codeAddr, int method, FrameClassification kind) =>
                new AsyncProfilerStitchSyncFrame(CA(codeAddr), MI(method), kind);

            public AsyncProfilerStitchSyncFrame BoxCarrier(int codeAddr, int boxedMethod,
                AsyncProfilerStitchSyncFrameKind kind = AsyncProfilerStitchSyncFrameKind.None) =>
                new AsyncProfilerStitchSyncFrame(CA(codeAddr), MI(codeAddr), kind, MI(boxedMethod));

            public AsyncProfilerStitchSyncFrame Boundary(int codeAddr, AsyncProfilerCallStackStitchBoundaryInfo info)
            {
                _classify[CA(codeAddr)] = info;
                return new AsyncProfilerStitchSyncFrame(CA(codeAddr), MI(codeAddr));
            }

            /// <summary>
            /// Builds a call stack with leaf-to-root frames. Negative code addresses are unresolved; raw method IDs
            /// need only be supplied when their values matter to the test.
            /// </summary>
            public AsyncProfilerCallStack Segment(AsyncCallstackKind kind, int[] frameCodeAddrs, int[] frameMethods,
                AsyncProfilerCallStack.CompletionDelta[] completions = null, AsyncProfilerCallStack.CompletionDelta[] exceptionCompletions = null,
                byte continuationIndexBase = 0, byte wrapperCount = 32, ulong[] frameMethodIds = null,
                long[] wrapperResets = null)
            {
                int n = frameCodeAddrs.Length;
                var methodIds = frameMethodIds ?? new ulong[n];
                var frames = new AsyncProfilerCallStackFrames(kind, methodIds, null);
                for (int i = 0; i < n; i++)
                {
                    if (frameCodeAddrs[i] < 0)
                    {
                        frames.SetCodeAddressAt(i, CodeAddressIndex.Invalid);
                    }
                    else
                    {
                        CodeAddressIndex ca = CA(frameCodeAddrs[i]);
                        frames.SetCodeAddressAt(i, ca);
                        _methodOf[ca] = MI(frameMethods[i]);
                    }
                }

                return AsyncProfilerStackTestData.CallStack(frames, continuationIndexBase: continuationIndexBase,
                    wrapperCount: wrapperCount, methodCompletions: completions, exceptionCompletions: exceptionCompletions,
                    wrapperResets: wrapperResets);
            }

            public AsyncProfilerStitchResult Run(IReadOnlyList<AsyncProfilerStitchSyncFrame> sync,
                IReadOnlyList<AsyncProfilerCallStack> segmentsLeafToRoot) =>
                AsyncProfilerCallStackStitcher.Stitch(sync, segmentsLeafToRoot, Qpc, Classify, MethodCompletionObserved, MethodOf);

            public AsyncProfilerStitchResult Run(IReadOnlyList<AsyncProfilerStitchSyncFrame> sync,
                IReadOnlyList<AsyncProfilerCallStack> segmentsLeafToRoot, bool trace) =>
                AsyncProfilerCallStackStitcher.Stitch(sync, segmentsLeafToRoot, Qpc, Classify, MethodCompletionObserved, MethodOf, trace);

            public AsyncProfilerStitchResult RunWithHistory(
                IReadOnlyList<AsyncProfilerStitchSyncFrame> sync,
                IReadOnlyList<AsyncProfilerCallStack> segments, AsyncProfilerCallStacksIndex index)
            {
                var output = new List<AsyncProfilerStitchedFrame>();
                var diagnostics = new AsyncProfilerCallStackDiagnostics();
                var placements = new List<AsyncProfilerCallStackPlacement>();
                var stitchingState = new AsyncProfilerCallStackStitchingState();
                AsyncProfilerCallStackStitcher.StitchInto(sync, segments, Qpc, Classify,
                    (process, kind, start) => MethodCompletionObserved(kind), ProcessIndex.Invalid, MethodOf,
                    false, output, diagnostics, stitchingState, placements, index: index);
                return new AsyncProfilerStitchResult(output, diagnostics, stitchingState, placements);
            }

            public AsyncProfilerStitchResult Run(IReadOnlyList<AsyncProfilerStitchSyncFrame> sync, IReadOnlyList<AsyncProfilerCallStack> segments,
                AsyncProfilerRuntimeSupport support, Func<CodeAddressIndex, AsyncProfilerRuntimeSupport> runtimeSupportResolver = null)
            {
                var output = new List<AsyncProfilerStitchedFrame>();
                var diagnostics = new AsyncProfilerCallStackDiagnostics();
                var stitchingState = new AsyncProfilerCallStackStitchingState();
                AsyncProfilerCallStackStitcher.StitchInto(sync, segments, Qpc, Classify,
                    (process, kind, qpc) => MethodCompletionObserved(kind), ProcessIndex.Invalid, MethodOf,
                    false, output, diagnostics, stitchingState, null, runtimeSupportResolver ?? (_ => support), support);
                return new AsyncProfilerStitchResult(output, diagnostics, stitchingState);
            }

            #region private

            private readonly Dictionary<CodeAddressIndex, MethodIndex> _methodOf = new Dictionary<CodeAddressIndex, MethodIndex>();
            private readonly Dictionary<CodeAddressIndex, AsyncProfilerCallStackStitchBoundaryInfo> _classify =
                new Dictionary<CodeAddressIndex, AsyncProfilerCallStackStitchBoundaryInfo>();
            private readonly HashSet<AsyncCallstackKind> _methodCompletionObserved = new HashSet<AsyncCallstackKind>();

            #endregion
        }

        private sealed class StructuralRulesSupport : AsyncProfilerRuntimeSupport
        {
            public StructuralRulesSupport(int infrastructureLength)
                : base("Test-only structural rules")
            {
                m_infrastructureLength = infrastructureLength;
            }

            public override AsyncProfilerRuntimeSequenceMatch MatchDispatchInfrastructure(
                AsyncCallstackKind kind, IReadOnlyList<AsyncProfilerStitchSyncFrame> frames, int start, int end,
                Func<CodeAddressIndex, AsyncProfilerCallStackStitchBoundaryInfo> classify) =>
                m_infrastructureLength != 0 && start < end &&
                frames[start].Kind == AsyncProfilerStitchSyncFrameKind.V1MethodHelper
                    ? AsyncProfilerRuntimeSequenceMatch.Match(m_infrastructureLength)
                    : base.MatchDispatchInfrastructure(kind, frames, start, end, classify);

            #region private

            private readonly int m_infrastructureLength;

            #endregion
        }

        private static AsyncProfilerCallStackStitchBoundaryInfo Wrapper(int index) =>
            new AsyncProfilerCallStackStitchBoundaryInfo(AsyncProfilerCallStackStitchBoundaryKind.V2Dispatcher, index);

        private static AsyncProfilerCallStack.CompletionDelta[] Completed(int count) =>
            new[] { new AsyncProfilerCallStack.CompletionDelta(Qpc, count) };

        private static AsyncProfilerCallStack.CompletionDelta[] Unwound(int frames) =>
            new[] { new AsyncProfilerCallStack.CompletionDelta(Qpc, frames) };

        private static AsyncProfilerCallStack AddIndexedSegment(
            Scenario scenario,
            AsyncProfilerCallStacksIndex index,
            AsyncProfilerThreadKey thread,
            ulong dispatcherId,
            int depth,
            long startQpc,
            long endQpc,
            int firstCodeAddress,
            int frameCount = 2,
            AsyncProfilerCallStack.CompletionDelta[] methodCompletions = null,
            AsyncProfilerCallStack.CompletionDelta[] exceptionCompletions = null,
            AsyncCallstackKind kind = AsyncCallstackKind.RuntimeAsync,
            int[] frameStates = null)
        {
            var methodIds = new ulong[frameCount];
            for (int i = 0; i < methodIds.Length; i++)
            {
                methodIds[i] = (ulong)(firstCodeAddress + i);
            }
            index.Add(
                thread,
                kind,
                methodIds,
                frameStates: kind == AsyncCallstackKind.StateMachineAsync
                    ? (frameStates ?? new int[frameCount])
                    : null,
                dispatcherId,
                depth,
                continuationIndexBase: 0,
                wrapperCount: 32,
                startQpc,
                endQpc,
                methodCompletions: methodCompletions ?? Array.Empty<AsyncProfilerCallStack.CompletionDelta>(),
                exceptionCompletions: exceptionCompletions ?? Array.Empty<AsyncProfilerCallStack.CompletionDelta>(),
                wrapperResets: Array.Empty<long>());

            AsyncProfilerCallStack segment =
                index.GetCallStack(thread, dispatcherId, startQpc);
            for (int i = 0; i < frameCount; i++)
            {
                segment.Frames.SetCodeAddressAt(i, Scenario.CA(firstCodeAddress + i));
                scenario.RegisterMethod(firstCodeAddress + i, firstCodeAddress + i);
            }
            return segment;
        }

        private static AsyncProfilerCallStacksIndex CreateHistory(AsyncProfilerCallStack segment)
        {
            var index = new AsyncProfilerCallStacksIndex();
            index.Add(segment.Thread, segment.Frames.Kind,
                Enumerable.Range(0, segment.Frames.FrameCount).Select(segment.Frames.InstructionPointerAt).ToArray(), null,
                segment.DispatcherId, segment.Depth, segment.ContinuationIndexBase, segment.WrapperCount,
                segment.StartQpc, segment.EndQpc, Array.Empty<AsyncProfilerCallStack.CompletionDelta>(),
                Array.Empty<AsyncProfilerCallStack.CompletionDelta>(), Array.Empty<long>());
            return index;
        }

        private const long Qpc = 1000;
        private static readonly AsyncProfilerCallStackStitchBoundaryInfo s_dispatchContinuation =
            new AsyncProfilerCallStackStitchBoundaryInfo(AsyncProfilerCallStackStitchBoundaryKind.V2DispatcherInfrastructure, -1);
        private static readonly AsyncProfilerCallStackStitchBoundaryInfo s_v1Dispatcher =
            new AsyncProfilerCallStackStitchBoundaryInfo(AsyncProfilerCallStackStitchBoundaryKind.V1Dispatcher, -1);
        private static readonly AsyncProfilerCallStackStitchBoundaryInfo s_v1Infrastructure =
            new AsyncProfilerCallStackStitchBoundaryInfo(AsyncProfilerCallStackStitchBoundaryKind.V1DispatcherInfrastructure, -1);

        #endregion
    }
}
