using System;
using System.Collections.Generic;

using Microsoft.Diagnostics.Tracing.Computers;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;

namespace TraceEventBenchmarks
{
    /// <summary>Builds synthetic physical stacks and frame identities for the structural-stitching benchmarks.</summary>
    /// <remarks>Code-address and method indexes are synthetic integers; boundaries and methods are resolved from in-memory maps.</remarks>
    internal sealed class AsyncProfilerSyntheticStitchInput
    {
        public AsyncProfilerSyntheticStitchInput()
        {
            Classify = ClassifyCore;
            MethodOf = MethodOfCore;
            MethodCompletionObserved = (processIndex, kind, callStackStartQpc) => false;
        }

        public Func<CodeAddressIndex, AsyncProfilerCallStackStitchBoundaryInfo> Classify { get; }
        public Func<CodeAddressIndex, MethodIndex> MethodOf { get; }
        public Func<ProcessIndex, AsyncCallstackKind, long, bool> MethodCompletionObserved { get; }

        /// <summary>Returns a V2 stack: two async-method frames, the dispatcher wrapper, dispatcher infrastructure, and two callers.</summary>
        public AsyncProfilerStitchSyncFrame[] RuntimeSync(int firstCodeAddress, int firstAsyncMethod, int wrapperIndex)
        {
            return new[]
            {
                Sync(firstCodeAddress, firstAsyncMethod + 10),
                Sync(firstCodeAddress + 1, firstAsyncMethod + wrapperIndex),
                Boundary(firstCodeAddress + 2, AsyncProfilerCallStackStitchBoundaryKind.V2Dispatcher, wrapperIndex),
                Boundary(firstCodeAddress + 3, AsyncProfilerCallStackStitchBoundaryKind.V2DispatcherInfrastructure),
                Boundary(firstCodeAddress + 4, AsyncProfilerCallStackStitchBoundaryKind.V2DispatcherInfrastructure),
                Sync(firstCodeAddress + 5, 95),
                Sync(firstCodeAddress + 6, 96),
            };
        }

        /// <summary>Returns a V1 stack: a leaf, two state-machine frames, the dispatcher, and dispatcher infrastructure.</summary>
        public AsyncProfilerStitchSyncFrame[] StateMachineSync(int firstCodeAddress, int firstAsyncMethod)
        {
            return new[]
            {
                Sync(firstCodeAddress, 90),
                Sync(firstCodeAddress + 1, firstAsyncMethod + 1),
                Sync(firstCodeAddress + 2, firstAsyncMethod),
                Boundary(firstCodeAddress + 3, AsyncProfilerCallStackStitchBoundaryKind.V1Dispatcher),
                Boundary(firstCodeAddress + 4, AsyncProfilerCallStackStitchBoundaryKind.V1DispatcherInfrastructure),
                Boundary(firstCodeAddress + 5, AsyncProfilerCallStackStitchBoundaryKind.V1DispatcherInfrastructure),
            };
        }

        /// <summary>Returns <paramref name="count"/> ordinary frames with no async boundary.</summary>
        public AsyncProfilerStitchSyncFrame[] SyncFrames(int count, int firstCodeAddress)
        {
            var result = new AsyncProfilerStitchSyncFrame[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = Sync(firstCodeAddress + i, firstCodeAddress + i);
            }
            return result;
        }

        public AsyncProfilerStitchSyncFrame Sync(int codeAddress, int method)
        {
            CodeAddressIndex ca = CodeAddress(codeAddress);
            MethodIndex mi = Method(method);
            _methods[ca] = mi;
            return new AsyncProfilerStitchSyncFrame(ca, mi);
        }

        public AsyncProfilerStitchSyncFrame Boundary(
            int codeAddress, AsyncProfilerCallStackStitchBoundaryKind kind, int wrapperIndex = -1)
        {
            CodeAddressIndex ca = CodeAddress(codeAddress);
            _boundaries[ca] = new AsyncProfilerCallStackStitchBoundaryInfo(kind, wrapperIndex);
            return new AsyncProfilerStitchSyncFrame(ca, MethodIndex.Invalid);
        }

        /// <summary>Resolves each frame to consecutive synthetic code addresses and methods starting at <paramref name="firstCodeAddress"/>.</summary>
        public void SetCodeAddresses(AsyncProfilerCallStackFrames frames, int firstCodeAddress)
        {
            for (int i = 0; i < frames.FrameCount; i++)
            {
                CodeAddressIndex codeAddress = CodeAddress(firstCodeAddress + i);
                frames.SetCodeAddressAt(i, codeAddress);
                _methods[codeAddress] = Method(firstCodeAddress + i);
            }
        }

        public static CodeAddressIndex CodeAddress(int value) => (CodeAddressIndex)value;

        public static MethodIndex Method(int value) => (MethodIndex)value;

        #region private

        private AsyncProfilerCallStackStitchBoundaryInfo ClassifyCore(CodeAddressIndex codeAddress) =>
            _boundaries.TryGetValue(codeAddress, out AsyncProfilerCallStackStitchBoundaryInfo boundary)
                ? boundary
                : AsyncProfilerCallStackStitchBoundaryInfo.None;

        private MethodIndex MethodOfCore(CodeAddressIndex codeAddress) =>
            _methods.TryGetValue(codeAddress, out MethodIndex method) ? method : MethodIndex.Invalid;

        private readonly Dictionary<CodeAddressIndex, AsyncProfilerCallStackStitchBoundaryInfo> _boundaries =
            new Dictionary<CodeAddressIndex, AsyncProfilerCallStackStitchBoundaryInfo>();
        private readonly Dictionary<CodeAddressIndex, MethodIndex> _methods = new Dictionary<CodeAddressIndex, MethodIndex>();

        #endregion
    }
}
