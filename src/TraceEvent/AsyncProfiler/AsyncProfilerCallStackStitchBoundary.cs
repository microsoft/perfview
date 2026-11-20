// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;

using Microsoft.Diagnostics.Tracing.Etlx;

namespace Microsoft.Diagnostics.Tracing.Computers
{
    /// <summary>
    /// Classifies async dispatch boundaries and related infrastructure on a physical call stack.
    /// Infrastructure frames are not independent splice boundaries.
    /// </summary>
    internal enum AsyncProfilerCallStackStitchBoundaryKind
    {
        /// <summary>Not an async dispatch boundary; an ordinary frame.</summary>
        None = 0,

        /// <summary>
        /// A V2 dispatcher boundary, identified by a continuation wrapper carrying a slot from 0 to 31.
        /// The resumed method may be reached through a runtime resume stub.
        /// </summary>
        V2Dispatcher,

        /// <summary>
        /// V2 dispatcher helpers, not independent splice boundaries.
        /// A wrapperless target requires separate completion and call-stack history evidence.
        /// </summary>
        V2DispatcherInfrastructure,

        /// <summary>
        /// A V1 merged-box or standalone dispatcher. Completion records or ordered physical-method matches
        /// establish the position within its async segment.
        /// </summary>
        V1Dispatcher,

        /// <summary>
        /// Runtime-recognized V1 box or dispatcher helpers. A verified contiguous span can be grouped with
        /// a consumed dispatcher; these frames are not splice boundaries.
        /// </summary>
        V1DispatcherInfrastructure,
    }

    /// <summary>
    /// A method's dispatch classification and, for V2 dispatchers, its continuation-wrapper slot.
    /// </summary>
    internal readonly struct AsyncProfilerCallStackStitchBoundaryInfo
    {
        public AsyncProfilerCallStackStitchBoundaryInfo(AsyncProfilerCallStackStitchBoundaryKind kind, int wrapperIndex)
        {
            Kind = kind;
            WrapperIndex = wrapperIndex;
        }

        /// <summary>
        /// True for V1/V2 dispatchers and V2 dispatcher infrastructure.
        /// These markers require explicit handling during stack scans; they do not independently authorize a splice.
        /// </summary>
        public bool IsDispatchMarker =>
            Kind == AsyncProfilerCallStackStitchBoundaryKind.V2Dispatcher ||
            Kind == AsyncProfilerCallStackStitchBoundaryKind.V2DispatcherInfrastructure ||
            Kind == AsyncProfilerCallStackStitchBoundaryKind.V1Dispatcher;

        /// <summary>The classification of the method.</summary>
        public readonly AsyncProfilerCallStackStitchBoundaryKind Kind;

        /// <summary>The continuation-wrapper slot from 0 to 31 for V2 dispatchers; otherwise -1.</summary>
        public readonly int WrapperIndex;

        /// <summary>An unrecognized boundary or helper.</summary>
        public static readonly AsyncProfilerCallStackStitchBoundaryInfo None = new AsyncProfilerCallStackStitchBoundaryInfo(AsyncProfilerCallStackStitchBoundaryKind.None, -1);
    }

    /// <summary>
    /// Recognizes profiler dispatcher contracts and continuation-wrapper slots.
    /// Runtime-specific helper recognition belongs to <see cref="AsyncProfilerRuntimeSupport"/>.
    /// The cache below verifies CoreLib ownership before classifying captured methods.
    /// </summary>
    internal static class AsyncProfilerCallStackStitchBoundary
    {
        /// <summary>The V2 wrapper-name prefix; the numeric suffix identifies the wrapper slot.</summary>
        public const string ContinuationWrapperPrefix = "Continuation_Wrapper_";

        /// <summary>The runtime's continuation-wrapper pool size. Valid slots are 0 to 31.</summary>
        public const int WrapperPoolCount = 32;

        /// <summary>The V2 uninstrumented dispatch-continuation method name (<c>RuntimeAsyncTask&lt;T&gt;</c>).</summary>
        public const string DispatchContinuationsName = "DispatchContinuations";

        /// <summary>The V2 instrumented dispatch-continuation method name (<c>RuntimeAsyncTask&lt;T&gt;</c>).</summary>
        public const string InstrumentedDispatchContinuationsName = "InstrumentedDispatchContinuations";

        /// <summary>The V1 dispatcher method on a profiler-enabled state-machine box.</summary>
        public const string MoveNextAsDispatcherName = "MoveNextAsDispatcher";

        /// <summary>The standalone V1 dispatcher type, which wraps a separate state-machine box.</summary>
        public const string AsyncStateMachineDispatcherTypeName = "AsyncStateMachineDispatcher";

        /// <summary>The standalone V1 dispatcher method; recognition also requires its declaring type.</summary>
        public const string AsyncStateMachineDispatcherMethodName = "MoveNext";

        /// <summary>
        /// If <paramref name="method"/> is a continuation-wrapper method name
        /// (<c>Continuation_Wrapper_&lt;n&gt;</c>), returns true and sets <paramref name="wrapperIndex"/> to
        /// <c>n</c> (0..<see cref="WrapperPoolCount"/>-1). <paramref name="method"/> is the bare method-name
        /// component (see <see cref="AsyncProfilerMethodNames.GetBareMethodName"/>).
        /// </summary>
        public static bool TryGetWrapperIndex(string method, out int wrapperIndex)
        {
            wrapperIndex = -1;
            if (method is null || !method.StartsWith(ContinuationWrapperPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            string suffix = method.Substring(ContinuationWrapperPrefix.Length);
            if (suffix.Length == 0 || !int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out int index))
            {
                return false;
            }

            if ((uint)index >= (uint)WrapperPoolCount)
            {
                return false;
            }

            wrapperIndex = index;
            return true;
        }

        #region private

        /// <summary>Recognizes shared dispatcher contracts by name without checking module ownership.</summary>
        internal static AsyncProfilerCallStackStitchBoundaryKind ClassifyContractBoundary(string frameName, out int wrapperIndex)
        {
            wrapperIndex = -1;
            string method = AsyncProfilerMethodNames.GetBareMethodName(frameName);
            if (string.IsNullOrEmpty(method))
            {
                return AsyncProfilerCallStackStitchBoundaryKind.None;
            }

            if (TryGetWrapperIndex(method, out wrapperIndex))
            {
                return AsyncProfilerCallStackStitchBoundaryKind.V2Dispatcher;
            }

            if (method == DispatchContinuationsName || method == InstrumentedDispatchContinuationsName)
            {
                return AsyncProfilerCallStackStitchBoundaryKind.V2DispatcherInfrastructure;
            }

            if (method == MoveNextAsDispatcherName)
            {
                return AsyncProfilerCallStackStitchBoundaryKind.V1Dispatcher;
            }

            // MoveNext alone does not distinguish the standalone dispatcher from application state machines.
            bool isStandaloneDispatcher =
                method == AsyncStateMachineDispatcherMethodName &&
                AsyncProfilerMethodNames.GetDeclaringTypeName(frameName) == AsyncStateMachineDispatcherTypeName;
            if (isStandaloneDispatcher)
            {
                return AsyncProfilerCallStackStitchBoundaryKind.V1Dispatcher;
            }

            return AsyncProfilerCallStackStitchBoundaryKind.None;
        }

        #endregion
    }

    /// <summary>
    /// Caches boundary classifications by MethodIndex, scanning captured CoreLib methods on first use.
    /// Construct after managed symbols are resolved; subsequent frame classification avoids string parsing.
    /// </summary>
    internal sealed class AsyncProfilerCallStackStitchBoundaryCache
    {
        internal AsyncProfilerCallStackStitchBoundaryCache(TraceCodeAddresses codeAddresses, AsyncProfilerRuntimeResolver runtimeResolver)
        {
            m_codeAddresses = codeAddresses ?? throw new ArgumentNullException(nameof(codeAddresses));
            m_runtimeResolver = runtimeResolver ?? throw new ArgumentNullException(nameof(runtimeResolver));
        }

        internal AsyncProfilerRuntimeResolver RuntimeResolver => m_runtimeResolver;

        internal TraceLog TraceLog => m_codeAddresses.ModuleFiles.Log;

        /// <summary>Classifies the method containing <paramref name="codeAddressIndex"/>. Returns
        /// <see cref="AsyncProfilerCallStackStitchBoundaryInfo.None"/> for an invalid or unresolved code address.</summary>
        public AsyncProfilerCallStackStitchBoundaryInfo Classify(CodeAddressIndex codeAddressIndex)
        {
            if (codeAddressIndex == CodeAddressIndex.Invalid)
            {
                return AsyncProfilerCallStackStitchBoundaryInfo.None;
            }

            MethodIndex methodIndex = m_codeAddresses.MethodIndex(codeAddressIndex);
            if (methodIndex == MethodIndex.Invalid)
            {
                return AsyncProfilerCallStackStitchBoundaryInfo.None;
            }

            EnsureBuilt();
            if (!m_boundaries.TryGetValue(methodIndex, out AsyncProfilerCallStackStitchBoundaryInfo info))
            {
                return AsyncProfilerCallStackStitchBoundaryInfo.None;
            }

            return info;
        }

        #region private

        internal AsyncProfilerRuntimeSupport GetRuntimeSupport(CodeAddressIndex codeAddress)
        {
            if (codeAddress == CodeAddressIndex.Invalid)
            {
                return AsyncProfilerRuntimeSupport.Common;
            }

            return m_runtimeResolver.ResolveModuleSupport(m_codeAddresses.ModuleFileIndex(codeAddress));
        }

        /// <summary>
        /// Builds classifications once, checking CoreLib ownership before reading method names.
        /// </summary>
        private void EnsureBuilt()
        {
            if (m_built)
            {
                return;
            }

            Dictionary<ModuleFileIndex, AsyncProfilerRuntimeSupport> hostModules = GetHostModules();
            if (hostModules.Count != 0)
            {
                TraceMethods methods = m_codeAddresses.Methods;
                int count = methods.Count;
                for (int i = 0; i < count; i++)
                {
                    var methodIndex = (MethodIndex)i;
                    ModuleFileIndex moduleIndex = methods.MethodModuleFileIndex(methodIndex);
                    if (!hostModules.TryGetValue(moduleIndex, out AsyncProfilerRuntimeSupport support))
                    {
                        continue;
                    }

                    string name = AsyncProfilerMethodNames.NormalizeMethodName(methods.FullMethodName(methodIndex));
                    AsyncProfilerCallStackStitchBoundaryInfo info = support.ClassifyBoundary(name);
                    if (info.Kind != AsyncProfilerCallStackStitchBoundaryKind.None)
                    {
                        m_boundaries[methodIndex] = info;
                    }
                }
            }

            m_built = true;
        }

        /// <summary>Gets the support for each captured CoreLib module, including linked native images.</summary>
        private Dictionary<ModuleFileIndex, AsyncProfilerRuntimeSupport> GetHostModules()
        {
            var result = new Dictionary<ModuleFileIndex, AsyncProfilerRuntimeSupport>();
            TraceModuleFiles moduleFiles = m_codeAddresses.ModuleFiles;
            foreach (TraceModuleFile moduleFile in moduleFiles)
            {
                if (m_runtimeResolver.IsHostModule(moduleFile.ModuleFileIndex))
                {
                    result.Add(moduleFile.ModuleFileIndex, m_runtimeResolver.ResolveModuleSupport(moduleFile.ModuleFileIndex));
                }
            }

            return result;
        }

        // Only recognized boundaries and helpers are stored; an absent MethodIndex means None.
        private readonly Dictionary<MethodIndex, AsyncProfilerCallStackStitchBoundaryInfo> m_boundaries = new Dictionary<MethodIndex, AsyncProfilerCallStackStitchBoundaryInfo>();
        private readonly TraceCodeAddresses m_codeAddresses;
        private readonly AsyncProfilerRuntimeResolver m_runtimeResolver;
        private bool m_built;

        #endregion
    }
}
