// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Microsoft.Diagnostics.Tracing.Computers
{
    /// <summary>
    /// Adds recorded parent call stacks to a stitched stack using the child's dispatcher-creation time.
    /// Parents need not be active at sample time. Captured frame states are unchanged.
    /// </summary>
    internal sealed class AsyncProfilerParentContextBuilder
    {
        internal const int DefaultMaximumDepth = 1;

        /// <summary>
        /// Returns true if an active call stack has a recorded parent outside the active call-stack list.
        /// </summary>
        internal bool HasParentContextsToAdd(
            AsyncProfilerCallStacksIndex index,
            IReadOnlyList<AsyncProfilerCallStack> activeCallStacks)
        {
            for (int i = 0; i < activeCallStacks.Count; i++)
            {
                AsyncProfilerCallStack child = activeCallStacks[i];
                if (!TryGetParent(index, child, out AsyncProfilerCallStack parent, out _))
                {
                    continue;
                }

                var parentIdentity = new CallStackIdentity(parent);
                if (Contains(activeCallStacks, parentIdentity))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Adds missing parent contexts at each placed call stack's root in <paramref name="frames"/>.
        /// Follows at most <paramref name="maximumDepth"/> parent links for each placed call stack.
        /// </summary>
        internal void AddParentContexts(
            AsyncProfilerCallStacksIndex index,
            IReadOnlyList<AsyncProfilerCallStack> activeCallStacks,
            IReadOnlyList<AsyncProfilerCallStackPlacement> placements,
            List<AsyncProfilerStitchedFrame> frames,
            int maximumDepth)
        {
            if (index == null) throw new ArgumentNullException(nameof(index));
            if (activeCallStacks == null) throw new ArgumentNullException(nameof(activeCallStacks));
            if (placements == null) throw new ArgumentNullException(nameof(placements));
            if (frames == null) throw new ArgumentNullException(nameof(frames));
            if (maximumDepth <= 0) throw new ArgumentOutOfRangeException(nameof(maximumDepth));

            m_active.Clear();
            for (int i = 0; i < activeCallStacks.Count; i++)
            {
                m_active.Add(new CallStackIdentity(activeCallStacks[i]));
            }

            // Placements are recorded leaf-to-root. Insert root-to-leaf so the remaining frame indices stay valid.
            for (int placementIndex = placements.Count - 1; placementIndex >= 0; placementIndex--)
            {
                AsyncProfilerCallStackPlacement placement = placements[placementIndex];
                CollectMissingParents(index, placement.CallStack, maximumDepth);
                if (m_chain.Count == 0)
                {
                    continue;
                }

                m_insertion.Clear();
                for (int i = 0; i < m_chain.Count; i++)
                {
                    CollectParentFrames(index, m_chain[i]);
                    m_insertion.AddRange(m_parentFrames);
                }

                if (m_insertion.Count != 0)
                {
                    frames.InsertRange(placement.RootIndexExclusive, m_insertion);
                }
            }
        }

        /// <summary>Clears reusable buffers and cached parent lookups.</summary>
        internal void Clear()
        {
            m_active.Clear();
            m_visited.Clear();
            m_chain.Clear();
            m_parentCandidates.Clear();
            m_insertion.Clear();
            m_parentFrames.Clear();
            m_parentCache = null;
        }

        #region private

        /// <summary>Collects parents until the depth limit, an active call stack, or a repeated call stack.</summary>
        private void CollectMissingParents(
            AsyncProfilerCallStacksIndex index,
            AsyncProfilerCallStack callStack,
            int maximumDepth)
        {
            m_chain.Clear();
            m_visited.Clear();
            m_visited.Add(new CallStackIdentity(callStack));

            AsyncProfilerCallStack child = callStack;
            for (int depth = 0; depth < maximumDepth; depth++)
            {
                if (!TryGetParent(index, child, out AsyncProfilerCallStack parent, out long creationQpc))
                {
                    break;
                }

                var identity = new CallStackIdentity(parent);
                if (Contains(m_active, identity) || Contains(m_visited, identity))
                {
                    break;
                }

                m_visited.Add(identity);
                m_chain.Add(new ParentAtCreation(parent, creationQpc));
                child = parent;
            }
        }

        /// <summary>Finds the parent at child-dispatcher creation, caching successful lookups.</summary>
        private bool TryGetParent(
            AsyncProfilerCallStacksIndex index,
            AsyncProfilerCallStack child,
            out AsyncProfilerCallStack parent,
            out long creationQpc)
        {
            var identity = new CallStackIdentity(child);
            int cacheIndex = GetParentCacheIndex(index, identity);
            if (m_parentCache != null)
            {
                ParentCacheEntry cached = m_parentCache[cacheIndex];
                if (cached.Matches(index, identity))
                {
                    parent = cached.Parent;
                    creationQpc = cached.CreationQpc;
                    return true;
                }
            }

            bool found = index.TryGetParentCallStack(child, m_parentCandidates, out parent, out creationQpc);
            if (found)
            {
                if (m_parentCache == null)
                {
                    m_parentCache = new ParentCacheEntry[ParentCacheSize];
                }
                m_parentCache[cacheIndex] = new ParentCacheEntry(index, identity, parent, creationQpc);
            }
            return found;
        }

        private static int GetParentCacheIndex(
            AsyncProfilerCallStacksIndex index,
            CallStackIdentity identity) =>
            (identity.GetHashCode() ^ RuntimeHelpers.GetHashCode(index)) &
            (ParentCacheSize - 1);

        /// <summary>
        /// Copies the parent's frames, omitting completed frames at child creation only when both
        /// method-completion and exception-unwind observations are available. Zero-IP placeholders
        /// are skipped, and a placeholder in the current slot leaves no frame marked current.
        /// </summary>
        private void CollectParentFrames(
            AsyncProfilerCallStacksIndex index,
            ParentAtCreation parentAtCreation)
        {
            m_parentFrames.Clear();
            AsyncProfilerCallStack parent = parentAtCreation.Parent;
            bool canTrimCompletedFrames = index.MethodCompletionObserved(parent) && index.ExceptionCompletionObserved(parent);
            int frameCount = parent.Frames.FrameCount;
            int completedFrameCount = canTrimCompletedFrames
                ? Math.Min(frameCount, Math.Max(0, parent.GetCompletedFrameCount(parentAtCreation.CreationQpc)))
                : 0;

            for (int frameIndex = completedFrameCount; frameIndex < frameCount; frameIndex++)
            {
                if (!parent.Frames.IsCapturedFrameAt(frameIndex))
                {
                    continue;
                }

                // "Current" refers to child creation, not the sample time.
                bool isCurrent = canTrimCompletedFrames && frameIndex == completedFrameCount;
                m_parentFrames.Add(AsyncProfilerStitchedFrame.CreateAsyncContextParent(parent, frameIndex, isCurrent));
            }
        }

        private static bool Contains(
            List<CallStackIdentity> identities,
            CallStackIdentity candidate)
        {
            for (int i = 0; i < identities.Count; i++)
            {
                if (identities[i].Equals(candidate))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool Contains(
            IReadOnlyList<AsyncProfilerCallStack> callStacks,
            CallStackIdentity candidate)
        {
            for (int i = 0; i < callStacks.Count; i++)
            {
                if (new CallStackIdentity(callStacks[i]).Equals(candidate))
                {
                    return true;
                }
            }

            return false;
        }

        private readonly struct ParentAtCreation
        {
            internal ParentAtCreation(AsyncProfilerCallStack parent, long creationQpc)
            {
                Parent = parent;
                CreationQpc = creationQpc;
            }

            internal AsyncProfilerCallStack Parent { get; }
            internal long CreationQpc { get; }
        }

        private readonly struct CallStackIdentity : IEquatable<CallStackIdentity>
        {
            internal CallStackIdentity(AsyncProfilerCallStack callStack)
            {
                Thread = callStack.Thread;
                DispatcherId = callStack.DispatcherId;
                StartQpc = callStack.StartQpc;
            }

            public bool Equals(CallStackIdentity other) =>
                Thread.Equals(other.Thread) &&
                DispatcherId == other.DispatcherId &&
                StartQpc == other.StartQpc;

            public override bool Equals(object obj) =>
                obj is CallStackIdentity other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = Thread.GetHashCode();
                    hash = (hash * 397) ^ DispatcherId.GetHashCode();
                    return (hash * 397) ^ StartQpc.GetHashCode();
                }
            }

            private readonly AsyncProfilerThreadKey Thread;
            private readonly ulong DispatcherId;
            private readonly long StartQpc;
        }

        private readonly struct ParentCacheEntry
        {
            internal ParentCacheEntry(
                AsyncProfilerCallStacksIndex index,
                CallStackIdentity identity,
                AsyncProfilerCallStack parent,
                long creationQpc)
            {
                Index = index;
                Identity = identity;
                Parent = parent;
                CreationQpc = creationQpc;
            }

            internal bool Matches(
                AsyncProfilerCallStacksIndex index,
                CallStackIdentity identity) =>
                ReferenceEquals(Index, index) && Identity.Equals(identity);

            internal AsyncProfilerCallStack Parent { get; }
            internal long CreationQpc { get; }

            private AsyncProfilerCallStacksIndex Index { get; }
            private CallStackIdentity Identity { get; }
        }

        // Must be a power of two for masked cache indexing.
        private const int ParentCacheSize = 256;

        private readonly List<CallStackIdentity> m_active = new List<CallStackIdentity>();
        private readonly List<CallStackIdentity> m_visited = new List<CallStackIdentity>();
        private readonly List<ParentAtCreation> m_chain = new List<ParentAtCreation>();
        private readonly List<AsyncProfilerCallStack> m_parentCandidates = new List<AsyncProfilerCallStack>();
        private readonly List<AsyncProfilerStitchedFrame> m_insertion = new List<AsyncProfilerStitchedFrame>();
        private readonly List<AsyncProfilerStitchedFrame> m_parentFrames = new List<AsyncProfilerStitchedFrame>();
        private ParentCacheEntry[] m_parentCache;

        #endregion
    }
}
