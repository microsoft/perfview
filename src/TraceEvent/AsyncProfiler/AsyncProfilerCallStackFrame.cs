// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;

namespace Microsoft.Diagnostics.Tracing.Computers
{
    /// <summary>A captured or reconstructed stack frame, or a synthetic display label.</summary>
    /// <remarks>
    /// Code addresses belong to the originating TraceLog. Frames from query results can be retained.
    /// Frames and call stack references supplied to transform callbacks are valid only during the callback;
    /// copying a frame does not extend that lifetime.
    /// A default value has no identity or label and cannot be inserted into a transform.
    /// </remarks>
    public readonly struct AsyncProfilerCallStackFrame
    {
        /// <summary>Creates a synthetic display label with no physical or captured async identity.</summary>
        /// <exception cref="ArgumentException"><paramref name="displayName"/> is null or empty.</exception>
        public AsyncProfilerCallStackFrame(string displayName)
        {
            if (string.IsNullOrEmpty(displayName))
            {
                throw new ArgumentException("A synthetic frame requires a nonempty display name.", nameof(displayName));
            }
            m_frame = AsyncProfilerStitchedFrame.CreateSynthetic(displayName);
            m_initialized = true;
        }

        /// <summary>The code address in the originating TraceLog, or Invalid when unavailable.
        /// Physically matched frames retain the sampled address.
        /// A valid address may still have unresolved symbols.</summary>
        public CodeAddressIndex CodeAddress => m_initialized ? m_frame.CodeAddress : CodeAddressIndex.Invalid;

        /// <summary>The captured async instruction pointer, or zero for non-async frames and captured zero-IP entries.
        /// This may differ from the physical instruction pointer represented by CodeAddress.</summary>
        public ulong CapturedInstructionPointer => m_frame.Segment == null ? 0 : m_frame.Segment.InstructionPointerAt(m_frame.SegmentFrameIndex);

        /// <summary>The captured V1 compiler state, or null for V2 and non-async frames.
        /// Inferred running/completed states do not change this value.</summary>
        public int? CapturedState
        {
            get
            {
                if (m_frame.Segment?.Kind != AsyncCallstackKind.StateMachineAsync)
                {
                    return null;
                }

                return m_frame.Segment.FrameStateAt(m_frame.SegmentFrameIndex);
            }
        }

        /// <summary>The async call stack associated with this frame, or null.
        /// For historical parent frames, this is the parent's call stack, which may not appear in the callback's segments list.</summary>
        public AsyncProfilerCallStack CallStack => m_frame.Context;

        /// <summary>True when the frame represents physically sampled execution, rather than a logical or synthetic frame.</summary>
        public bool IsPhysical => m_initialized &&
            (m_frame.Origin == AsyncProfilerStitchedFrameOrigin.Sync ||
             (m_frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent && !m_frame.IsStateMachineBoxCarrier) ||
             m_frame.Origin == AsyncProfilerStitchedFrameOrigin.V1CompletedBody);

        /// <summary>True for the physically matched current async method. Dispatch targets are excluded.</summary>
        public bool IsCurrent => m_frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncCurrent;

        /// <summary>True for a historical parent frame linked through the child call stack's creation.
        /// This is not a stack captured at child creation or evidence of execution at sample time.</summary>
        public bool IsParentContext => m_frame.ContextKind == AsyncProfilerStitchedFrameContextKind.ParentContext;

        /// <summary>True for the logical parent frame identified as current at child creation.
        /// This does not prove physical execution or change the captured state or instruction pointer.</summary>
        public bool IsParentContextCurrent => m_frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncContextParentCurrent;

        /// <summary>True when this frame identifies an async resumption target without a physically matched method body.
        /// For example, ReadAsync can be identified as the target while a sample catches runtime dispatch code.</summary>
        public bool IsDispatchTarget => m_frame.Origin == AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget;

        /// <summary>The synthetic frame's label, or null for captured or reconstructed frames.
        /// This is not a resolved method name or the final formatted stack-source name.</summary>
        public string DisplayName => m_frame.DisplayName;

        #region private

        internal AsyncProfilerCallStackFrame(AsyncProfilerStitchedFrame frame)
        {
            m_frame = frame;
            m_initialized = true;
        }

        internal AsyncProfilerStitchedFrame GetFrame()
        {
            if (!m_initialized)
            {
                throw new ArgumentException("A default frame has no identity or display label.");
            }

            return m_frame;
        }

        private readonly AsyncProfilerStitchedFrame m_frame;
        private readonly bool m_initialized;

        #endregion
    }
}
