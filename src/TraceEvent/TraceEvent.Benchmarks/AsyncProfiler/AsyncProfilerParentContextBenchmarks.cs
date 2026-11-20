using System;
using System.Collections.Generic;

using BenchmarkDotNet.Attributes;

using Microsoft.Diagnostics.Tracing.Computers;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;

namespace TraceEventBenchmarks
{
    /// <summary>Measures the cost of enabling historical parent contexts and following increasingly deep parent chains.</summary>
    [MemoryDiagnoser]
    public class AsyncProfilerParentContextBenchmarks
    {
        /// <summary>Length of the historical parent chain, also used as the maximum depth followed.</summary>
        [Params(1, 2, 4, 8)]
        public int ParentDepth { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _input = new AsyncProfilerSyntheticStitchInput();
            _output = new List<AsyncProfilerStitchedFrame>(64);
            _placements = new List<AsyncProfilerCallStackPlacement>(1);
            _diagnostics = new AsyncProfilerCallStackDiagnostics();
            _stitchingState = new AsyncProfilerCallStackStitchingState();
            _parentContextBuilder = new AsyncProfilerParentContextBuilder();

            _sync = _input.RuntimeSync(firstCodeAddress: 100, firstAsyncMethod: 200, wrapperIndex: 3);

            _segmentsWithoutParents = CreateIndex(parentCount: 0).GetCallStacks(s_childThread, Qpc);

            _indexWithUnrelatedDispatcherInfo = CreateIndex(parentCount: 0);
            _indexWithUnrelatedDispatcherInfo.AddDispatcherInfo(
                s_childThread, dispatcherId: 999, parentDispatcherId: 1_000, creationQpc: 800);
            _segmentsWithUnrelatedDispatcherInfo =
                _indexWithUnrelatedDispatcherInfo.GetCallStacks(s_childThread, Qpc);

            _indexWithParents = CreateIndex(parentCount: ParentDepth);
            _segmentsWithParents = _indexWithParents.GetCallStacks(s_childThread, Qpc);

            int baselineFrameCount = Disabled();
            if (EnabledWithUnrelatedDispatcherInfo() != baselineFrameCount)
            {
                throw new InvalidOperationException("Unrelated dispatcher metadata changed the stitched stack.");
            }
            if (EnabledWithParents() != baselineFrameCount + (ParentDepth * FramesPerCallStack))
            {
                throw new InvalidOperationException("The parent-context benchmark did not insert the expected historical frames.");
            }
        }

        /// <summary>Stitches the child stack without collecting parent placements or querying parent metadata.</summary>
        [Benchmark(Baseline = true)]
        public int Disabled()
        {
            Stitch(_segmentsWithoutParents, placements: null);
            return _output.Count;
        }

        /// <summary>Measures an unsuccessful parent lookup when metadata exists but does not describe the active child.</summary>
        /// <remarks>Independent of <see cref="ParentDepth"/>; repeated for each value as a reference point.</remarks>
        [Benchmark]
        public int EnabledWithUnrelatedDispatcherInfo() =>
            StitchWithOptionalParentContexts(
                _indexWithUnrelatedDispatcherInfo, _segmentsWithUnrelatedDispatcherInfo, ParentDepth);

        /// <summary>Stitches the child and follows a historical parent chain of <see cref="ParentDepth"/> levels.</summary>
        [Benchmark]
        public int EnabledWithParents() =>
            StitchWithOptionalParentContexts(_indexWithParents, _segmentsWithParents, ParentDepth);

        #region private

        // Mirrors the parent-context gating in AsyncProfilerCallStackSourceBuilderCore, which cannot be called directly
        // without a TraceLog-backed stack source.
        private int StitchWithOptionalParentContexts(
            AsyncProfilerCallStacksIndex index, IReadOnlyList<AsyncProfilerCallStack> segments, int maximumDepth)
        {
            bool parentContextsAvailable = index.HasDispatcherRecords && _parentContextBuilder.HasParentContextsToAdd(index, segments);
            Stitch(segments, parentContextsAvailable ? _placements : null);
            if (!parentContextsAvailable)
            {
                return _output.Count;
            }

            _parentContextBuilder.AddParentContexts(index, segments, _placements, _output, maximumDepth);
            return _output.Count;
        }

        private void Stitch(IReadOnlyList<AsyncProfilerCallStack> segments, List<AsyncProfilerCallStackPlacement> placements)
        {
            AsyncProfilerCallStackStitcher.StitchInto(
                _sync, segments, Qpc, _input.Classify, _input.MethodCompletionObserved,
                s_processIndex, _input.MethodOf, trace: false, _output, _diagnostics, _stitchingState, placements);
        }

        private AsyncProfilerCallStacksIndex CreateIndex(int parentCount)
        {
            var index = new AsyncProfilerCallStacksIndex();
            AddCallStack(
                index, s_childThread, dispatcherId: 1, depth: 0,
                startQpc: 900, endQpc: 1_100, firstMethodId: 200);
            _input.SetCodeAddresses(index.GetCallStacks(s_childThread, Qpc)[0].Frames, firstCodeAddress: 200);

            ulong dispatcherId = 1;
            long childStartQpc = 900;
            for (int parentIndex = 0; parentIndex < parentCount; parentIndex++)
            {
                ulong parentDispatcherId = (ulong)(10 + parentIndex);
                long creationQpc = childStartQpc - 100;
                var parentThread = new AsyncProfilerThreadKey(s_processIndex, (ulong)(100 + parentIndex));

                AddCallStack(
                    index, parentThread, parentDispatcherId, depth: 0,
                    startQpc: creationQpc - 50, endQpc: creationQpc + 50,
                    firstMethodId: 300 + (parentIndex * 10));
                _input.SetCodeAddresses(
                    index.GetCallStacks(parentThread, creationQpc)[0].Frames,
                    firstCodeAddress: 300 + (parentIndex * 10));
                index.AddDispatcherInfo(parentThread, dispatcherId, parentDispatcherId, creationQpc);

                dispatcherId = parentDispatcherId;
                childStartQpc = creationQpc - 50;
            }

            return index;
        }

        private static void AddCallStack(
            AsyncProfilerCallStacksIndex index, AsyncProfilerThreadKey thread, ulong dispatcherId,
            int depth, long startQpc, long endQpc, int firstMethodId)
        {
            var methodIds = new ulong[FramesPerCallStack];
            for (int i = 0; i < methodIds.Length; i++)
            {
                methodIds[i] = (ulong)(firstMethodId + i);
            }

            index.Add(
                thread, AsyncCallstackKind.RuntimeAsync, methodIds, frameStates: null,
                dispatcherId, depth, continuationIndexBase: 0, wrapperCount: 32, startQpc, endQpc,
                methodCompletions: Array.Empty<AsyncProfilerCallStack.CompletionDelta>(),
                exceptionCompletions: Array.Empty<AsyncProfilerCallStack.CompletionDelta>(),
                wrapperResets: Array.Empty<long>());
        }

        private const long Qpc = 1_000;
        private const int FramesPerCallStack = 6;

        private static readonly ProcessIndex s_processIndex = (ProcessIndex)1;
        private static readonly AsyncProfilerThreadKey s_childThread = new AsyncProfilerThreadKey(s_processIndex, 42);

        private AsyncProfilerSyntheticStitchInput _input;
        private IReadOnlyList<AsyncProfilerStitchSyncFrame> _sync;
        private IReadOnlyList<AsyncProfilerCallStack> _segmentsWithoutParents;
        private IReadOnlyList<AsyncProfilerCallStack> _segmentsWithUnrelatedDispatcherInfo;
        private IReadOnlyList<AsyncProfilerCallStack> _segmentsWithParents;
        private AsyncProfilerCallStacksIndex _indexWithUnrelatedDispatcherInfo;
        private AsyncProfilerCallStacksIndex _indexWithParents;
        private List<AsyncProfilerStitchedFrame> _output;
        private List<AsyncProfilerCallStackPlacement> _placements;
        private AsyncProfilerCallStackDiagnostics _diagnostics;
        private AsyncProfilerCallStackStitchingState _stitchingState;
        private AsyncProfilerParentContextBuilder _parentContextBuilder;

        #endregion
    }
}
