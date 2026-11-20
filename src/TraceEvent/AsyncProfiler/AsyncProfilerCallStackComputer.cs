// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tracing.Etlx;

namespace Microsoft.Diagnostics.Tracing.Computers
{
    /// <summary>Queries recorded async call stacks and their captured frames.</summary>
    /// <remarks>
    /// Uses data indexed during trace conversion; it does not enable or collect profiler events.
    /// Captured frames do not identify the currently executing method. Use
    /// <see cref="AsyncProfilerCallStackSourceBuilder"/> to combine physical and async stacks.
    /// The caller owns the TraceLog and must keep it open while querying or resolving code addresses.
    /// </remarks>
    public sealed class AsyncProfilerCallStackComputer
    {
        /// <summary>Creates a computer for <paramref name="traceLog"/>.</summary>
        /// <exception cref="ArgumentNullException"><paramref name="traceLog"/> is null.</exception>
        public AsyncProfilerCallStackComputer(TraceLog traceLog)
        {
            m_log = traceLog ?? throw new ArgumentNullException(nameof(traceLog));
        }

        /// <summary>
        /// Returns async call stacks covering <paramref name="timestampQpc"/>, ordered leaf-to-root.
        /// Captured frames are leaf-to-root; completion progress and presentation transforms are not applied.
        /// Results remain valid across later queries and index release.
        /// </summary>
        /// <param name="processId">The OS process ID, not a TraceLog ProcessIndex.
        /// ID reuse is resolved at <paramref name="timestampQpc"/>.</param>
        /// <param name="osThreadId">The OS thread ID, not a TraceLog ThreadIndex.
        /// No TraceThread record is required.</param>
        /// <param name="timestampQpc">A timestamp in the originating trace's QPC domain.
        /// StartQpc is inclusive; EndQpc is exclusive.</param>
        /// <returns>A non-null list of matching call stacks, or an empty list if no data is available.</returns>
        public IReadOnlyList<AsyncProfilerCallStack> GetCallStacks(int processId, ulong osThreadId, long timestampQpc)
        {
            AsyncProfilerCallStacksIndex index = m_log.AsyncProfilerCallStacks;
            TraceProcess process = index == null ? null : m_log.Processes.GetProcess(processId, timestampQpc);
            if (process == null)
            {
                return Array.Empty<AsyncProfilerCallStack>();
            }

            return index.GetCallStacks(new AsyncProfilerThreadKey(process.ProcessIndex, osThreadId), timestampQpc);
        }

        /// <summary>Returns recorded async call stacks for an event's process, thread, and QPC timestamp.</summary>
        /// <remarks>
        /// The event must belong to this computer's TraceLog instance. It is not retained, and no physical stack is required.
        /// Ordering, frame semantics, and result lifetime match the process/thread/QPC overload.
        /// </remarks>
        /// <exception cref="ArgumentNullException"><paramref name="anchorEvent"/> is null.</exception>
        /// <exception cref="ArgumentException">The event does not belong to this computer's TraceLog instance.</exception>
        public IReadOnlyList<AsyncProfilerCallStack> GetCallStacks(TraceEvent anchorEvent)
        {
            ValidateEvent(anchorEvent, m_log, nameof(anchorEvent));
#pragma warning disable CS0618 // Preserve exact event alignment inside TraceEvent.
            return GetCallStacks(anchorEvent.ProcessID, (ulong)anchorEvent.ThreadID, anchorEvent.TimeStampQPC);
#pragma warning restore CS0618
        }

        #region private

        internal static void ValidateEvent(TraceEvent data, TraceLog log, string parameterName)
        {
            if (data == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (!ReferenceEquals(data.Log(), log))
            {
                throw new ArgumentException("The event must belong to the owning TraceLog.", parameterName);
            }
        }

        private readonly TraceLog m_log;

        #endregion
    }
}
