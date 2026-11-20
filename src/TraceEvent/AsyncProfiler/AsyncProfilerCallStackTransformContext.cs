// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tracing.Etlx;

namespace Microsoft.Diagnostics.Tracing.Computers
{
    /// <summary>
    /// Lets a custom transform inspect and edit a reconstructed call stack.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use this context, its collections, and the call stacks they reference only during the callback.
    /// The context and frame-list objects are reused each time the callback runs.
    /// </para>
    /// <para>
    /// Copy scalar values if you need to keep information after the callback returns.
    /// Copying frames or collections does not extend the lifetime of the referenced call stacks.
    /// </para>
    /// <para>
    /// Editing <see cref="Frames"/> changes presentation only, not the recorded async data.
    /// </para>
    /// </remarks>
    public sealed class AsyncProfilerCallStackTransformContext
    {
        /// <summary>The trace containing the frame identities.</summary>
        public TraceLog TraceLog => m_state.TraceLog;

        /// <summary>The reconstruction timestamp in the trace's QPC domain.</summary>
        public long TimestampQpc => m_state.TimestampQpc;

        /// <summary>
        /// The active async call stacks on the sampled thread, ordered leaf-to-root.
        /// </summary>
        /// <remarks>
        /// Historical parent expansion adds frames to <see cref="Frames"/>, not entries to this list.
        /// Each frame's <see cref="AsyncProfilerCallStackFrame.CallStack"/> identifies its associated call stack.
        /// </remarks>
        public IReadOnlyList<AsyncProfilerCallStack> Segments => m_state.Segments;

        /// <summary>
        /// The mutable reconstructed frame list, ordered leaf-to-root.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Any enabled parent-context expansion and built-in transforms run before the callback.
        /// Frame-name annotations, generated context-transition markers, and grouping roots are applied later.
        /// </para>
        /// <para>
        /// Frames can be removed, reordered, or replaced. Add display labels with
        /// <c>new AsyncProfilerCallStackFrame("label")</c>.
        /// Reused frame identities must belong to <see cref="TraceLog"/>.
        /// Inserting or replacing a default frame throws <see cref="ArgumentException"/>.
        /// </para>
        /// </remarks>
        public IList<AsyncProfilerCallStackFrame> Frames { get; }

        #region private

        internal AsyncProfilerCallStackTransformContext(Action<AsyncProfilerCallStackTransformContext> transform)
        {
            m_transform = transform;
            Frames = new FrameList(this);
        }

        internal void Invoke(AsyncProfilerCallStackTransformState state)
        {
            m_state = state;
            try
            {
                m_transform(this);
            }
            finally
            {
                m_state = default;
            }
        }

        private sealed class FrameList : IList<AsyncProfilerCallStackFrame>
        {
            internal FrameList(AsyncProfilerCallStackTransformContext context)
            {
                m_context = context;
            }

            public int Count => CurrentFrames.Count;

            public bool IsReadOnly => false;

            public AsyncProfilerCallStackFrame this[int index]
            {
                get => new AsyncProfilerCallStackFrame(CurrentFrames[index]);
                set => CurrentFrames[index] = value.GetFrame();
            }

            public void Add(AsyncProfilerCallStackFrame item) => CurrentFrames.Add(item.GetFrame());

            public void Clear() => CurrentFrames.Clear();

            public void Insert(int index, AsyncProfilerCallStackFrame item) =>
                CurrentFrames.Insert(index, item.GetFrame());

            public void RemoveAt(int index) => CurrentFrames.RemoveAt(index);

            public bool Contains(AsyncProfilerCallStackFrame item) => IndexOf(item) >= 0;

            public int IndexOf(AsyncProfilerCallStackFrame item) => CurrentFrames.IndexOf(item.GetFrame());

            public bool Remove(AsyncProfilerCallStackFrame item) => CurrentFrames.Remove(item.GetFrame());

            public void CopyTo(AsyncProfilerCallStackFrame[] array, int arrayIndex)
            {
                if (array == null)
                {
                    throw new ArgumentNullException(nameof(array));
                }

                if (arrayIndex < 0 || arrayIndex > array.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(arrayIndex));
                }

                if (array.Length - arrayIndex < Count)
                {
                    throw new ArgumentException("The destination array is too small.", nameof(array));
                }

                for (int i = 0; i < Count; i++)
                {
                    array[arrayIndex + i] = this[i];
                }
            }

            public IEnumerator<AsyncProfilerCallStackFrame> GetEnumerator()
            {
                foreach (AsyncProfilerStitchedFrame frame in CurrentFrames)
                {
                    yield return new AsyncProfilerCallStackFrame(frame);
                }
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            private List<AsyncProfilerStitchedFrame> CurrentFrames => m_context.m_state.Frames ??
                throw new InvalidOperationException("The frame collection is only available during its callback.");

            private readonly AsyncProfilerCallStackTransformContext m_context;
        }

        private readonly Action<AsyncProfilerCallStackTransformContext> m_transform;
        private AsyncProfilerCallStackTransformState m_state;

        #endregion
    }
}
