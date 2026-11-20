// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.Diagnostics.Tracing.Computers
{
    /// <summary>Frame sequences recognized by runtime support.</summary>
    internal enum AsyncProfilerRuntimeSequenceKind
    {
        TaskCompletion,
        TaskCompletionChain,
        V2CompletionHandoff,
        V1Startup,
        V1AwaitRegistration,
        ProfilerEntry,
        RuntimeResumptionBridge,
        TaskExceptionCompletion,
    }

    /// <summary>Built-in presentation transforms supplied by runtime support.</summary>
    internal enum AsyncProfilerRuntimeTransformKind
    {
        SampledV2Wrapper,
        ReentrantHandoff,
        V1Presentation,
        InfrastructureCleanup,
    }

    /// <summary>
    /// Describes a matched or rejected frame sequence. The default value means no candidate was found.
    /// Cleanup preserves rejected sequences instead of removing their frames individually.
    /// </summary>
    /// <remarks>A match identifies a frame layout; callers still check whether it is safe to change those frames.</remarks>
    internal readonly struct AsyncProfilerRuntimeSequenceMatch
    {
        /// <summary>Creates a successful match covering <paramref name="length"/> frames.</summary>
        public static AsyncProfilerRuntimeSequenceMatch Match(int length) => new AsyncProfilerRuntimeSequenceMatch(length, true);

        /// <summary>Creates a rejected candidate covering <paramref name="length"/> frames.</summary>
        public static AsyncProfilerRuntimeSequenceMatch Reject(int length) => new AsyncProfilerRuntimeSequenceMatch(length, false);

        /// <summary>Number of frames covered by the match or rejection; zero when no candidate was found.</summary>
        public int Length { get; }

        /// <summary>True when the frame sequence was recognized.</summary>
        public bool IsMatch { get; }

        /// <summary>True when a candidate was rejected; false for the default value.</summary>
        public bool IsRejected => Length != 0 && !IsMatch;

        #region private

        private AsyncProfilerRuntimeSequenceMatch(int length, bool isMatch)
        {
            if (length <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            Length = length;
            IsMatch = isMatch;
        }

        #endregion
    }
}
