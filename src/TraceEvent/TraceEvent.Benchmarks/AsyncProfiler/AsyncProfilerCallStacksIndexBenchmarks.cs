using System;
using System.Collections.Generic;

using BenchmarkDotNet.Attributes;

using Microsoft.Diagnostics.Tracing.Computers;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;

namespace TraceEventBenchmarks
{
    /// <summary>Compares allocating and reusable queries as the number of indexed call stacks grows.</summary>
    [MemoryDiagnoser]
    public class AsyncProfilerCallStacksIndexBenchmarks
    {
        /// <summary>Number of sequential outer call stacks, in addition to the nested call stacks at the query time.</summary>
        [Params(1_000, 100_000)]
        public int IntervalCount { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            _index = new AsyncProfilerCallStacksIndex();
            _result = new List<AsyncProfilerCallStack>(4);

            ulong[] methodIds = { 0x1000, 0x2000, 0x3000 };
            for (int i = 0; i < IntervalCount; i++)
            {
                long startQpc = i * 100L;
                _index.Add(
                    s_thread, AsyncCallstackKind.RuntimeAsync, methodIds, frameStates: null,
                    dispatcherId: 0, depth: 0, continuationIndexBase: 0, wrapperCount: 32,
                    startQpc, endQpc: startQpc + 90,
                    methodCompletions: Array.Empty<AsyncProfilerCallStack.CompletionDelta>(),
                    exceptionCompletions: Array.Empty<AsyncProfilerCallStack.CompletionDelta>(),
                    wrapperResets: Array.Empty<long>());
            }

            _queryQpc = (IntervalCount / 2) * 100L + 50;
            for (int depth = 1; depth < 8; depth++)
            {
                _index.Add(
                    s_thread, AsyncCallstackKind.RuntimeAsync, methodIds, frameStates: null,
                    dispatcherId: 0, depth, continuationIndexBase: 0, wrapperCount: 32,
                    startQpc: _queryQpc - (8 - depth),
                    endQpc: _queryQpc + (8 - depth),
                    methodCompletions: Array.Empty<AsyncProfilerCallStack.CompletionDelta>(),
                    exceptionCompletions: Array.Empty<AsyncProfilerCallStack.CompletionDelta>(),
                    wrapperResets: Array.Empty<long>());
            }
            _index.GetCallStacks(s_thread, _queryQpc, _result);
            if (_result.Count != 8 || AllocateResultList() != _result.Count)
            {
                throw new InvalidOperationException("The index benchmark did not find all eight nested call stacks.");
            }
            for (int i = 0; i < _result.Count; i++)
            {
                if (_result[i].Depth != _result.Count - 1 - i)
                {
                    throw new InvalidOperationException("The index benchmark returned call stacks out of innermost-first order.");
                }
            }
        }

        /// <summary>Queries the warmed index into a newly allocated list of stable call-stack objects.</summary>
        [Benchmark(Baseline = true)]
        public int AllocateResultList() => _index.GetCallStacks(s_thread, _queryQpc).Count;

        /// <summary>Queries the same index using a reusable result list and borrowed call-stack objects.</summary>
        [Benchmark]
        public int ReuseResultList()
        {
            _index.GetCallStacks(s_thread, _queryQpc, _result);
            return _result.Count;
        }

        #region private

        private static readonly ProcessIndex s_processIndex = (ProcessIndex)1;
        private static readonly AsyncProfilerThreadKey s_thread = new AsyncProfilerThreadKey(s_processIndex, 42);

        private AsyncProfilerCallStacksIndex _index;
        private List<AsyncProfilerCallStack> _result;
        private long _queryQpc;

        #endregion
    }
}
