// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Tracing.Computers
{
    /// <summary>
    /// Counters and bounded messages gathered while stitching and applying presentation transforms.
    /// Includes successful processing and expected fallbacks as well as soft anomalies; nonzero counters do not
    /// by themselves indicate failure. These values report results; stitching decisions use
    /// <see cref="AsyncProfilerCallStackStitchingState"/> instead. Diagnostic conditions do not throw.
    /// </summary>
    internal sealed class AsyncProfilerCallStackDiagnostics
    {
        /// <summary>Number of async segments consumed.</summary>
        public int SegmentsProcessed;

        /// <summary>Segments whose expected sync-stack dispatch boundary was not found. Stitching may recover a unique
        /// physical alignment; otherwise stitching degrades conservatively.</summary>
        public int BoundariesNotFound;

        /// <summary>V2 segments whose resolved current method did not match the resolved sync frame immediately
        /// before the boundary, skipping a recognized resume stub when present.</summary>
        public int AdjacencyMismatches;

        /// <summary>V2 segments whose completed-frame count came from the wrapper slot because normal
        /// <c>CompleteMethod</c> events were not observed in their configuration epoch.</summary>
        public int V2WrapperFallbackUsed;

        /// <summary>V1 segments whose normal completed-frame count came from physical frames because
        /// <c>CompleteMethod</c> events were not observed in their configuration epoch.</summary>
        public int V1InlineFallbackUsed;

        /// <summary>V1 segments whose dispatcher boundary was truncated, but whose current position was recovered
        /// from a unique contiguous sequence of state-machine methods retained on the synchronous stack.</summary>
        public int V1TruncatedSuffixFallbackUsed;

        /// <summary>Rootmost V2 call stacks whose missing wrapper position was recovered from a unique ordered
        /// physical-method sequence consistent with recorded completion and wrapper-reset evidence.</summary>
        public int V2TruncatedSuffixFallbackUsed;

        /// <summary>Rootmost async call stacks whose uniquely recovered, resolved physical span ended before the
        /// dispatcher. Their incomplete caller fragment was replaced by an explicit <c>BROKEN</c> root.</summary>
        public int IncompleteAsyncRootsNormalized;

        /// <summary>V1 segments anchored by a physically present, record-verified completing method.</summary>
        public int V1CompletionTailUsed;

        /// <summary>Physical V1 return-unwind frames normalized by default presentation using a record-verified
        /// completed call stack or a truncated completed prefix with a physical finishing body.</summary>
        public int V1CompletionUnwindFramesCollapsed;

        /// <summary>V1 segments whose completion evidence cannot be safely aligned to a physical anchor,
        /// including repeated occurrences or records beyond the matched prefix. Their physical slices are retained.</summary>
        public int V1CompletionAlignmentAmbiguous;

        /// <summary>V2 samples whose inner call stack retained its sync layout: either a sampled wrapper/resume
        /// stub had no identifiable indexed dispatch target, or the sample sat in dispatch-continuation infrastructure
        /// without a wrapper or another verified target. No inner logical frames are spliced.
        /// Independently verified enclosing call stacks may still be stitched.
        /// Enabled presentation transforms may subsequently normalize that layout.</summary>
        public int V2SyncLayoutUsed;

        /// <summary>V2 sampled-leaf continuation wrappers consumed by structural dispatch-target projection
        /// or dropped from a retained synchronous layout by conservative presentation.</summary>
        public int V2LeafWrapperDropped;

        /// <summary>V2 dispatch-continuation callers collapsed after a stitched continuation-wrapper,
        /// and verified resume stubs consumed immediately before it. The reconstructed async stack
        /// represents that machinery.</summary>
        public int V2InfrastructureFramesCollapsed;

        /// <summary>V1 CoreLib async-state-machine-box callers collapsed contiguously after a consumed
        /// dispatcher boundary.</summary>
        public int V1InfrastructureFramesCollapsed;

        /// <summary>Physical V1 state-machine <c>MoveNext</c> frames presented as their logical source methods by
        /// the conservative presentation transform.</summary>
        public int V1SynchronousMoveNextFramesNormalized;

        /// <summary>
        /// Duplicate V1 startup representations removed from an exact synchronously reentrant call-stack pattern
        /// by the conservative presentation transform.
        /// </summary>
        public int V1ReentrantFramesCollapsed;

        /// <summary>Generated V1 startup frames removed by the conservative presentation transform. When an exact
        /// matching kickoff follows <c>stateMachine.MoveNext -&gt; methodBuilder.Start+</c>, the generated
        /// <c>MoveNext</c> and builder frames are folded into that native kickoff so its full signature is retained;
        /// otherwise only the known builder frames are removed.</summary>
        public int V1SynchronousStartupFramesCollapsed;

        /// <summary>Recognized V1/V2 runtime-infrastructure frames hidden or represented by dispatcher markers by
        /// <see cref="AsyncProfilerCallStackTransformPipeline.EnableInfrastructureCleanup"/>.
        /// Does not include frames removed by structural stitching or conservative normalization.</summary>
        public int AsyncInfrastructureFramesCollapsed;

        /// <summary>Runtime-owned frames atomically folded between a matched async call stack and thread-pool
        /// dispatch by the optional cleanup. A subset of <see cref="AsyncInfrastructureFramesCollapsed"/>.</summary>
        public int RuntimeResumptionBridgeFramesCollapsed;

        /// <summary>Resolved profiler helpers and a verified entry spine represented by a dispatcher marker,
        /// preserving native and unrecognized callees.
        /// A subset of <see cref="AsyncInfrastructureFramesCollapsed"/>.</summary>
        public int ProfilerEnvelopeFramesCollapsed;

        /// <summary>Resolved System.Private.CoreLib-only synchronous handoff frames collapsed between verified
        /// adjacent V1/V2 call stacks. Dispatch targets retain their role and captured V1 state.</summary>
        public int AsyncReentrantHandoffFramesCollapsed;

        /// <summary>The RuntimeAsync-to-RuntimeAsync subset of
        /// <see cref="AsyncReentrantHandoffFramesCollapsed"/>.</summary>
        public int V2ReentrantHandoffFramesCollapsed;

        /// <summary>Resolved CoreLib completion-bridge and V2 dispatch frames removed by conservative
        /// presentation when the remaining physical dispatcher resumes an exactly matched V1 call stack.</summary>
        public int V2CompletionHandoffFramesCollapsed;

        /// <summary>Human-readable notes: the anomalies above, plus per-segment happy-path trace steps when
        /// stitching is run with tracing enabled.</summary>
        public IReadOnlyList<string> Messages => m_messages ?? (IReadOnlyList<string>)Array.Empty<string>();

        /// <summary>Number of diagnostic messages omitted after the bounded message collection reached capacity.</summary>
        public int MessagesDropped;

        internal int MessageLimit { get; set; } = MaximumMessageCount;

        internal int RemainingMessageCapacity =>
            Math.Max(0, MessageLimit - (m_messages?.Count ?? 0));

        internal bool CanRetainMessage => RemainingMessageCapacity != 0;

        internal void Note(string message)
        {
            if (m_messages == null)
            {
                m_messages = new List<string>();
            }

            if (m_messages.Count < MessageLimit)
            {
                m_messages.Add(message);
            }
            else
            {
                MessagesDropped++;
            }
        }

        internal void Clear()
        {
            SegmentsProcessed = 0;
            BoundariesNotFound = 0;
            AdjacencyMismatches = 0;
            V2WrapperFallbackUsed = 0;
            V1InlineFallbackUsed = 0;
            V1TruncatedSuffixFallbackUsed = 0;
            V2TruncatedSuffixFallbackUsed = 0;
            IncompleteAsyncRootsNormalized = 0;
            V1CompletionTailUsed = 0;
            V1CompletionUnwindFramesCollapsed = 0;
            V1CompletionAlignmentAmbiguous = 0;
            V2SyncLayoutUsed = 0;
            V2LeafWrapperDropped = 0;
            V2InfrastructureFramesCollapsed = 0;
            V1InfrastructureFramesCollapsed = 0;
            V1SynchronousMoveNextFramesNormalized = 0;
            V1ReentrantFramesCollapsed = 0;
            V1SynchronousStartupFramesCollapsed = 0;
            AsyncInfrastructureFramesCollapsed = 0;
            RuntimeResumptionBridgeFramesCollapsed = 0;
            ProfilerEnvelopeFramesCollapsed = 0;
            AsyncReentrantHandoffFramesCollapsed = 0;
            V2ReentrantHandoffFramesCollapsed = 0;
            V2CompletionHandoffFramesCollapsed = 0;
            MessagesDropped = 0;
            m_messages?.Clear();
        }

        internal void AddTo(AsyncProfilerCallStackDiagnostics target)
        {
            target.SegmentsProcessed += SegmentsProcessed;
            target.BoundariesNotFound += BoundariesNotFound;
            target.AdjacencyMismatches += AdjacencyMismatches;
            target.V2WrapperFallbackUsed += V2WrapperFallbackUsed;
            target.V1InlineFallbackUsed += V1InlineFallbackUsed;
            target.V1TruncatedSuffixFallbackUsed += V1TruncatedSuffixFallbackUsed;
            target.V2TruncatedSuffixFallbackUsed += V2TruncatedSuffixFallbackUsed;
            target.IncompleteAsyncRootsNormalized += IncompleteAsyncRootsNormalized;
            target.V1CompletionTailUsed += V1CompletionTailUsed;
            target.V1CompletionUnwindFramesCollapsed += V1CompletionUnwindFramesCollapsed;
            target.V1CompletionAlignmentAmbiguous += V1CompletionAlignmentAmbiguous;
            target.V2SyncLayoutUsed += V2SyncLayoutUsed;
            target.V2LeafWrapperDropped += V2LeafWrapperDropped;
            target.V2InfrastructureFramesCollapsed += V2InfrastructureFramesCollapsed;
            target.V1InfrastructureFramesCollapsed += V1InfrastructureFramesCollapsed;
            target.V1SynchronousMoveNextFramesNormalized += V1SynchronousMoveNextFramesNormalized;
            target.V1ReentrantFramesCollapsed += V1ReentrantFramesCollapsed;
            target.V1SynchronousStartupFramesCollapsed += V1SynchronousStartupFramesCollapsed;
            target.AsyncInfrastructureFramesCollapsed += AsyncInfrastructureFramesCollapsed;
            target.RuntimeResumptionBridgeFramesCollapsed += RuntimeResumptionBridgeFramesCollapsed;
            target.ProfilerEnvelopeFramesCollapsed += ProfilerEnvelopeFramesCollapsed;
            target.AsyncReentrantHandoffFramesCollapsed += AsyncReentrantHandoffFramesCollapsed;
            target.V2ReentrantHandoffFramesCollapsed += V2ReentrantHandoffFramesCollapsed;
            target.V2CompletionHandoffFramesCollapsed += V2CompletionHandoffFramesCollapsed;
            target.MessagesDropped += MessagesDropped;
            if (m_messages != null)
            {
                for (int i = 0; i < m_messages.Count; i++)
                {
                    target.Note(m_messages[i]);
                }
            }
        }

        #region private

        private const int MaximumMessageCount = 1_024;
        private List<string> m_messages;

        #endregion
    }
}
