// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Tracing.Computers
{
    /// <summary>
    /// Carries the matching results used by the stitcher, pipeline helpers, and presentation passes.
    /// Diagnostics report these results but do not determine them.
    /// </summary>
    internal sealed class AsyncProfilerCallStackStitchingState
    {
        /// <summary>Active call stacks consumed by structural stitching, including retained physical fallbacks.</summary>
        public int ProcessedCallStackCount { get; set; }

        /// <summary>
        /// Whether stitching has found no missing dispatch boundary or conflicting resolved current method.
        /// False until stitching begins. Individual edits still require their own ownership checks.
        /// </summary>
        public bool HasConsistentPhysicalAlignment { get; set; }

        /// <summary>V1 call stacks whose completion position remains ambiguous after any recovery.</summary>
        public int UnresolvedV1CompletionCount { get; set; }

        /// <summary>Whether the inner V2 call stack retained its physical layout instead of being spliced.</summary>
        public bool UsesV2SynchronousLayout { get; set; }

        /// <summary>Discards the previous sample's matching results, leaving alignment unverified.</summary>
        internal void Clear()
        {
            ProcessedCallStackCount = 0;
            HasConsistentPhysicalAlignment = false;
            UnresolvedV1CompletionCount = 0;
            UsesV2SynchronousLayout = false;
        }

        /// <summary>Copies the matching results when a standalone transform copies the associated frame list.</summary>
        internal AsyncProfilerCallStackStitchingState Copy() =>
            new AsyncProfilerCallStackStitchingState
            {
                ProcessedCallStackCount = ProcessedCallStackCount,
                HasConsistentPhysicalAlignment = HasConsistentPhysicalAlignment,
                UnresolvedV1CompletionCount = UnresolvedV1CompletionCount,
                UsesV2SynchronousLayout = UsesV2SynchronousLayout,
            };
    }
}
