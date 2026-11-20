// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Stacks;

namespace Microsoft.Diagnostics.Tracing.Computers
{
    /// <summary>
    /// Combines physical and recorded async call stacks in a <see cref="MutableTraceEventStackSource"/>.
    /// </summary>
    /// <remarks>
    /// Reuse this builder with inputs from the output's TraceLog. Keep that log open while building stacks or resolving frames.
    /// Returned indices belong to the output and stay valid as more stacks are added.
    /// No samples are added or timestamps or weights assigned. No finalization is needed to inspect stacks.
    /// Stop building before finalizing the output. Access must not be concurrent or reentrant, including in callbacks.
    /// The builder does not own or dispose the TraceLog.
    /// </remarks>
    public sealed class AsyncProfilerCallStackSourceBuilder
    {
        /// <summary>Creates a builder that writes call stacks to <paramref name="output"/>.</summary>
        /// <remarks>Finish building the TraceLog's physical-stack and code-address tables before creating the output.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="output"/> is null.</exception>
        public AsyncProfilerCallStackSourceBuilder(MutableTraceEventStackSource output)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            m_output = output;
            m_log = output.TraceLog;

            var pipeline = new AsyncProfilerCallStackTransformPipeline();
            Transforms = new AsyncCallStackTransforms(pipeline);
            m_core = AsyncProfilerCallStackSourceBuilderCore.TryCreate(m_log, output, pipeline, null);
        }

        /// <summary>Presentation settings and custom transforms. Configure before producing stacks.</summary>
        public AsyncCallStackTransforms Transforms { get; }

        /// <summary>
        /// Combines <paramref name="physicalStack"/> with the async call stacks active at <paramref name="timestampQpc"/>,
        /// applies configured transforms, and returns the final stack's index in the output.
        /// Returns Invalid for an Invalid physical stack; other arguments and builder state are still checked.
        /// </summary>
        /// <param name="physicalStack">A physical stack from the output's TraceLog; its root identifies the process and thread.</param>
        /// <param name="timestampQpc">The user-frame time in trace QPC ticks, used unchanged.
        /// For event stacks, use <see cref="TraceLogExtensions.TryGetCallStackIndex"/> to obtain it when user frames may have been borrowed.</param>
        /// <param name="root">A grouping stack in the output, or Invalid for normal thread/process grouping.</param>
        /// <remarks>
        /// Missing or empty async call stacks use normal physical-stack conversion without custom transforms.
        /// Indices do not identify their owner; range checks cannot detect an index from another TraceLog or output.
        /// </remarks>
        /// <exception cref="ArgumentOutOfRangeException">A non-Invalid physical stack or root is outside its table's range.</exception>
        /// <exception cref="InvalidOperationException">The output was finalized, or the builder is called reentrantly.</exception>
        public StackSourceCallStackIndex GetCallStack(CallStackIndex physicalStack, long timestampQpc, StackSourceCallStackIndex root = StackSourceCallStackIndex.Invalid)
        {
            if (m_output.IsFinalized)
            {
                throw new InvalidOperationException("Cannot produce call stacks after the output has been finalized.");
            }

            bool physicalStackInRange = (int)physicalStack >= 0 && (int)physicalStack < m_log.CallStacks.Count;
            if (physicalStack != CallStackIndex.Invalid && !physicalStackInRange)
            {
                throw new ArgumentOutOfRangeException(nameof(physicalStack));
            }

            bool rootInRange = (int)root >= 0 && (int)root < m_output.CallStackIndexLimit;
            if (root != StackSourceCallStackIndex.Invalid && !rootInRange)
            {
                throw new ArgumentOutOfRangeException(nameof(root));
            }

            if (m_busy)
            {
                throw new InvalidOperationException("Async call-stack production is not reentrant.");
            }

            if (physicalStack == CallStackIndex.Invalid)
            {
                return StackSourceCallStackIndex.Invalid;
            }

            m_busy = true;
            try
            {
                TraceThread thread = m_log.CallStacks.Thread(physicalStack);
                bool defaultRoot = root == StackSourceCallStackIndex.Invalid;

                if (defaultRoot)
                {
                    root = m_output.GetCallStackForThread(thread);
                }

                if (m_core != null && m_core.TryGetCallStack(physicalStack, timestampQpc, thread, root, out var stitched))
                {
                    return stitched;
                }

                if (defaultRoot)
                {
                    return m_output.GetCallStackThread(physicalStack, thread);
                }

                return m_output.GetCallStack(physicalStack, root);
            }
            finally
            {
                m_busy = false;
            }
        }

        /// <summary>
        /// Combines <paramref name="data"/>'s physical stack with the async call stacks active at its user-frame timestamp,
        /// applies configured transforms, and returns the final stack's index in the output.
        /// Returns Invalid if no physical stack is available; arguments and builder state are still checked.
        /// The event is not retained.
        /// </summary>
        /// <param name="data">An event from the output's TraceLog instance.</param>
        /// <param name="root">A grouping stack in the output, or Invalid for normal thread/process grouping.</param>
        /// <remarks>Root and fallback behavior match the physical-stack overload.</remarks>
        /// <exception cref="ArgumentNullException"><paramref name="data"/> is null.</exception>
        /// <exception cref="ArgumentException">The event does not belong to the output's TraceLog instance.</exception>
        /// <exception cref="ArgumentOutOfRangeException">A non-Invalid root is outside the output's stack range.</exception>
        /// <exception cref="InvalidOperationException">The output was finalized, or the builder is called reentrantly.</exception>
        public StackSourceCallStackIndex GetCallStack(TraceEvent data, StackSourceCallStackIndex root = StackSourceCallStackIndex.Invalid)
        {
            AsyncProfilerCallStackComputer.ValidateEvent(data, m_log, nameof(data));
            data.TryGetCallStackIndex(out CallStackIndex stack, out long timestampQpc);
            return GetCallStack(stack, timestampQpc, root);
        }

        #region private

        private readonly MutableTraceEventStackSource m_output;
        private readonly TraceLog m_log;
        private readonly AsyncProfilerCallStackSourceBuilderCore m_core;
        private bool m_busy;

        #endregion
    }
}
