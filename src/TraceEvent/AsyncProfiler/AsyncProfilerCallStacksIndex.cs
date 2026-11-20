// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Threading;

using FastSerialization;

using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;

namespace Microsoft.Diagnostics.Tracing.Computers
{
    /// <summary>A dispatcher's recorded parent, creation thread, and creation time.</summary>
    internal sealed class AsyncProfilerDispatcherInfo
    {
        internal AsyncProfilerDispatcherInfo(AsyncProfilerThreadKey creationThread, ulong dispatcherId, ulong parentDispatcherId, long creationQpc)
        {
            CreationThread = creationThread;
            DispatcherId = dispatcherId;
            ParentDispatcherId = parentDispatcherId;
            CreationQpc = creationQpc;
        }

        /// <summary>The thread that created this dispatcher.</summary>
        public AsyncProfilerThreadKey CreationThread { get; }

        /// <summary>The dispatcher identifier.</summary>
        public ulong DispatcherId { get; }

        /// <summary>The parent dispatcher recorded at creation, or 0 if none.</summary>
        public ulong ParentDispatcherId { get; }

        /// <summary>The creation time in trace QPC ticks.</summary>
        public long CreationQpc { get; }
    }

    /// <summary>Stores recorded async call stacks by thread and time.</summary>
    /// <remarks>
    /// The index is saved in ETLX. Times are trace QPC ticks.
    /// A call stack is active from its start time up to, but not including, its end time.
    /// Public query results are not overwritten by later queries. Internal queries can reuse objects.
    /// Frame indices belong to this index; code-address indices belong to its TraceLog.
    /// Retained results can keep index data alive after <see cref="TraceLog.ReleaseAsyncProfilerCallStacks"/>.
    /// Seeing completion events does not mean every completion or unwind was captured.
    /// </remarks>
    internal sealed class AsyncProfilerCallStacksIndex : IFastSerializable
    {
        /// <summary>The number of unique frame lists.</summary>
        public int DistinctFramesCount
        {
            get
            {
                return _internedCallStackFrames.Count;
            }
        }

        /// <summary>True if no async call stacks were recorded.</summary>
        public bool IsEmpty
        {
            get
            {
                return _threads.Count == 0;
            }
        }

        /// <summary>The threads with recorded async call stacks.</summary>
        public IEnumerable<AsyncProfilerThreadKey> Threads
        {
            get
            {
                return _threads.Keys;
            }
        }

        /// <summary>
        /// True if method-completion events for <paramref name="kind"/> were observed in the default process (index 0).
        /// </summary>
        public bool MethodCompletionObserved(AsyncCallstackKind kind)
        {
            return MethodCompletionObserved(0, kind);
        }

        /// <summary>
        /// True if method-completion events for <paramref name="kind"/> were observed in <paramref name="processIndex"/>.
        /// </summary>
        public bool MethodCompletionObserved(ProcessIndex processIndex, AsyncCallstackKind kind)
        {
            if (!_completionAvailability.TryGetValue(processIndex, out List<CompletionAvailabilityEpoch> epochs))
            {
                return false;
            }

            for (int i = 0; i < epochs.Count; i++)
            {
                if (epochs[i].MethodObserved(kind))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// True if exception-unwind events for <paramref name="kind"/> were observed in the default process (index 0).
        /// </summary>
        public bool ExceptionCompletionObserved(AsyncCallstackKind kind)
        {
            return ExceptionCompletionObserved(0, kind);
        }

        /// <summary>
        /// True if exception-unwind events for <paramref name="kind"/> were observed in <paramref name="processIndex"/>.
        /// </summary>
        public bool ExceptionCompletionObserved(ProcessIndex processIndex, AsyncCallstackKind kind)
        {
            if (!_completionAvailability.TryGetValue(processIndex, out List<CompletionAvailabilityEpoch> epochs))
            {
                return false;
            }

            for (int i = 0; i < epochs.Count; i++)
            {
                if (epochs[i].ExceptionObserved(kind))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Returns the indexed frames, or null if <paramref name="index"/> is out of range.</summary>
        public AsyncProfilerCallStackFrames GetFrames(AsyncProfilerCallStackFramesIndex index)
        {
            int i = (int)index;
            if ((uint)i < (uint)_internedCallStackFrames.Count)
            {
                return _internedCallStackFrames[i];
            }

            return null;
        }

        /// <summary>
        /// Returns the async call stacks active on <paramref name="thread"/> at <paramref name="timestampQpc"/>
        /// in leaf-to-root order, or an empty list if none are active.
        /// </summary>
        public IReadOnlyList<AsyncProfilerCallStack> GetCallStacks(AsyncProfilerThreadKey thread, long timestampQpc)
        {
            var result = new List<AsyncProfilerCallStack>();
            if (_threads.TryGetValue(thread, out ThreadCallStacks callStacks))
            {
                callStacks.Query(timestampQpc, result, this, cacheMaterialized: true);
                result.Sort(CompareDepth);
            }

            return result;
        }

        /// <summary>
        /// Returns the active call stack for <paramref name="dispatcherId"/> closest to the leaf,
        /// or null if no matching call stack is active.
        /// </summary>
        public AsyncProfilerCallStack GetCallStack(AsyncProfilerThreadKey thread, ulong dispatcherId, long timestampQpc)
        {
            IReadOnlyList<AsyncProfilerCallStack> callStacks = GetCallStacks(thread, timestampQpc);
            for (int i = 0; i < callStacks.Count; i++)
            {
                if (callStacks[i].DispatcherId == dispatcherId)
                {
                    return callStacks[i];
                }
            }

            return null;
        }

        /// <summary>
        /// True if method-completion events were observed for the profiler configuration used by <paramref name="callStack"/>.
        /// </summary>
        public bool MethodCompletionObserved(AsyncProfilerCallStack callStack)
        {
            if (callStack == null)
            {
                throw new ArgumentNullException(nameof(callStack));
            }

            return MethodCompletionObserved(callStack.Thread.ProcessIndex, callStack.Frames.Kind, callStack.StartQpc);
        }

        /// <summary>
        /// True if exception-unwind events were observed for the profiler configuration used by <paramref name="callStack"/>.
        /// </summary>
        public bool ExceptionCompletionObserved(AsyncProfilerCallStack callStack)
        {
            if (callStack == null)
            {
                throw new ArgumentNullException(nameof(callStack));
            }

            CompletionAvailabilityEpoch epoch = FindCompletionEpoch(callStack.Thread.ProcessIndex, callStack.StartQpc);
            return epoch != null && epoch.ExceptionObserved(callStack.Frames.Kind);
        }

        /// <summary>
        /// Returns the latest creation metadata for <paramref name="dispatcherId"/> at or before
        /// <paramref name="timestampQpc"/> in <paramref name="processIndex"/>, or null if none was recorded.
        /// </summary>
        public AsyncProfilerDispatcherInfo GetDispatcherInfo(ProcessIndex processIndex, ulong dispatcherId, long timestampQpc)
        {
            int dispatcherIndex = GetDispatcherIndex(processIndex, dispatcherId, timestampQpc);
            if (dispatcherIndex < 0)
            {
                return null;
            }

            return MaterializeDispatcherInfo(_dispatchers[dispatcherIndex]);
        }

        /// <summary>Returns metadata for <paramref name="callStack"/>'s dispatcher, or null if none was recorded.</summary>
        public AsyncProfilerDispatcherInfo GetDispatcherInfo(AsyncProfilerCallStack callStack)
        {
            if (callStack == null)
            {
                throw new ArgumentNullException(nameof(callStack));
            }

            return GetDispatcherInfo(callStack.Thread.ProcessIndex, callStack.DispatcherId, callStack.StartQpc);
        }

        /// <summary>Returns the recorded children of <paramref name="parentDispatcherId"/>, ordered by creation time.</summary>
        public IReadOnlyList<AsyncProfilerDispatcherInfo> GetChildDispatcherInfos(ProcessIndex processIndex, ulong parentDispatcherId)
        {
            EnsureDispatcherIndexes();
            var key = new DispatcherKey(processIndex, parentDispatcherId);
            if (!_dispatchersByParentId.TryGetValue(key, out List<int> dispatcherIndices))
            {
                return Array.Empty<AsyncProfilerDispatcherInfo>();
            }

            var result = new AsyncProfilerDispatcherInfo[dispatcherIndices.Count];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = MaterializeDispatcherInfo(_dispatchers[dispatcherIndices[i]]);
            }

            return result;
        }

        /// <summary>
        /// Returns children created while <paramref name="parent"/> was active, ordered by creation time.
        /// </summary>
        public IReadOnlyList<AsyncProfilerDispatcherInfo> GetChildDispatcherInfos(AsyncProfilerCallStack parent)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            IReadOnlyList<AsyncProfilerDispatcherInfo> candidates = GetChildDispatcherInfos(parent.Thread.ProcessIndex, parent.DispatcherId);
            if (candidates.Count == 0)
            {
                return candidates;
            }

            var result = new List<AsyncProfilerDispatcherInfo>();
            for (int i = 0; i < candidates.Count; i++)
            {
                AsyncProfilerDispatcherInfo dispatcher = candidates[i];
                bool createdOnParentThread = dispatcher.CreationThread.Equals(parent.Thread);
                bool createdDuringParent =
                    parent.StartQpc <= dispatcher.CreationQpc &&
                    dispatcher.CreationQpc < parent.EndQpc;
                if (createdOnParentThread && createdDuringParent)
                {
                    result.Add(dispatcher);
                }
            }

            return result;
        }

        /// <summary>
        /// Returns the parent call stack at <paramref name="child"/>'s dispatcher creation time,
        /// or null if it cannot be found.
        /// </summary>
        public AsyncProfilerCallStack GetParentCallStack(AsyncProfilerCallStack child)
        {
            if (TryGetParentCallStack(child, out AsyncProfilerCallStack parent, out _))
            {
                return parent;
            }

            return null;
        }

        /// <summary>
        /// Finds the parent call stack at dispatcher creation and returns the creation time.
        /// The parent may no longer be active when <paramref name="child"/> runs.
        /// </summary>
        public bool TryGetParentCallStack(AsyncProfilerCallStack child, out AsyncProfilerCallStack parent, out long parentQpc)
        {
            if (child == null)
            {
                throw new ArgumentNullException(nameof(child));
            }

            AsyncProfilerDispatcherInfo dispatcher = GetDispatcherInfo(child);
            parentQpc = dispatcher?.CreationQpc ?? 0;
            if (dispatcher == null || dispatcher.ParentDispatcherId == 0)
            {
                parent = null;
            }
            else
            {
                parent = GetCallStack(dispatcher.CreationThread, dispatcher.ParentDispatcherId, dispatcher.CreationQpc);
            }

            return parent != null;
        }

        /// <summary>Returns all finalized call stacks for <paramref name="thread"/> in recording order.</summary>
        /// <remarks>
        /// The list includes later additions during conversion.
        /// Call <see cref="AsyncProfilerIndexBuilder.Finish"/> to include call stacks still open at the end of the stream.
        /// </remarks>
        public IReadOnlyList<AsyncProfilerCallStack> GetCallStacks(AsyncProfilerThreadKey thread)
        {
            if (_threads.TryGetValue(thread, out ThreadCallStacks callStacks))
            {
                return new ThreadCallStackList(this, callStacks);
            }

            return Array.Empty<AsyncProfilerCallStack>();
        }

        #region private

        /// <summary>
        /// Called when a new frame list is stored so <see cref="TraceLog"/> can attach code-address indices.
        /// Used only while building the index.
        /// </summary>
        internal Action<AsyncProfilerCallStackFrames> OnFrameInterned { get; set; }

        internal bool HasDispatcherRecords
        {
            get
            {
                return _dispatchers.Count != 0;
            }
        }

        internal bool MethodCompletionObserved(ProcessIndex processIndex, AsyncCallstackKind kind, long callStackStartQpc)
        {
            CompletionAvailabilityEpoch epoch = FindCompletionEpoch(processIndex, callStackStartQpc);
            return epoch != null && epoch.MethodObserved(kind);
        }

        internal void StartCompletionAvailabilityEpoch(ProcessIndex processIndex, long startQpc)
        {
            List<CompletionAvailabilityEpoch> epochs = GetCompletionEpochs(processIndex);
            int index = FindCompletionEpochIndex(epochs, startQpc);
            if (index >= 0 && epochs[index].StartQpc == startQpc)
            {
                return;
            }

            epochs.Insert(index + 1, new CompletionAvailabilityEpoch(startQpc));
        }

        /// <summary>
        /// Records that a <c>CompleteMethod</c> event was seen at <paramref name="timestampQpc"/>.
        /// </summary>
        internal void MarkMethodCompletionObserved(ProcessIndex processIndex, AsyncCallstackKind kind, long timestampQpc)
        {
            FindCompletionEpoch(processIndex, timestampQpc)?.MarkMethodObserved(kind);
        }

        /// <summary>
        /// Records that an <c>Unwind</c> event was seen at <paramref name="timestampQpc"/>.
        /// </summary>
        internal void MarkExceptionCompletionObserved(ProcessIndex processIndex, AsyncCallstackKind kind, long timestampQpc)
        {
            FindCompletionEpoch(processIndex, timestampQpc)?.MarkExceptionObserved(kind);
        }

        internal bool TryGetParentCallStack(AsyncProfilerCallStack child, List<AsyncProfilerCallStack> candidates, out AsyncProfilerCallStack parent, out long parentQpc)
        {
            if (child == null)
            {
                throw new ArgumentNullException(nameof(child));
            }

            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            int dispatcherIndex = GetDispatcherIndex(child.Thread.ProcessIndex, child.DispatcherId, child.StartQpc);
            if (dispatcherIndex < 0)
            {
                parent = null;
                parentQpc = 0;
                return false;
            }

            DispatcherRecord dispatcher = _dispatchers[dispatcherIndex];
            parentQpc = dispatcher.CreationQpc;
            if (dispatcher.ParentDispatcherId == 0 || !_threads.TryGetValue(dispatcher.CreationThread, out ThreadCallStacks callStacks))
            {
                parent = null;
                return false;
            }

            candidates.Clear();
            callStacks.Query(dispatcher.CreationQpc, candidates, this, cacheMaterialized: true);
            candidates.Sort(CompareDepth);
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].DispatcherId == dispatcher.ParentDispatcherId)
                {
                    parent = candidates[i];
                    return true;
                }
            }

            parent = null;
            return false;
        }

        /// <summary>
        /// Fills <paramref name="result"/> with active call stacks in leaf-to-root order.
        /// Do not retain these call-stack objects; later queries can overwrite them.
        /// </summary>
        /// <remarks>Callback adapters set completion-observation flags; this query does not.</remarks>
        internal void GetCallStacks(AsyncProfilerThreadKey thread, long timestampQpc, List<AsyncProfilerCallStack> result)
        {
            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }

            if (_threads.TryGetValue(thread, out ThreadCallStacks callStacks))
            {
                callStacks.Query(timestampQpc, result, this, cacheMaterialized: false);
                result.Sort(CompareDepth);
            }
            else
            {
                result.Clear();
            }
        }

        /// <summary>
        /// True if this call stack is active at <paramref name="timestampQpc"/> and has the thread's
        /// latest start time by then, with no ties.
        /// </summary>
        internal bool IsLatestCallStack(AsyncProfilerCallStack callStack, long timestampQpc)
        {
            if (!_threads.TryGetValue(callStack.Thread, out ThreadCallStacks callStacks))
            {
                return false;
            }

            return callStacks.QueryIndex().IsLatestCallStack(callStack, timestampQpc);
        }

        /// <summary>
        /// Stores a finalized call stack, reusing any matching frame list.
        /// </summary>
        internal void Add(
            AsyncProfilerThreadKey thread,
            AsyncCallstackKind kind,
            ulong[] instructionPointers,
            int[] frameStates,
            ulong dispatcherId,
            int depth,
            byte continuationIndexBase,
            byte wrapperCount,
            long startQpc,
            long endQpc,
            AsyncProfilerCallStack.CompletionDelta[] methodCompletions,
            AsyncProfilerCallStack.CompletionDelta[] exceptionCompletions,
            long[] wrapperResets)
        {
            AsyncProfilerCallStackFramesIndex framesIndex = Intern(kind, instructionPointers, frameStates, thread.ProcessIndex);
            Add(thread, framesIndex, dispatcherId, depth, continuationIndexBase, wrapperCount, startQpc, endQpc, methodCompletions, exceptionCompletions, wrapperResets);
        }

        internal void Add(
            AsyncProfilerThreadKey thread,
            AsyncProfilerCallStackFramesIndex framesIndex,
            ulong dispatcherId,
            int depth,
            byte continuationIndexBase,
            byte wrapperCount,
            long startQpc,
            long endQpc,
            AsyncProfilerCallStack.CompletionDelta[] methodCompletions,
            AsyncProfilerCallStack.CompletionDelta[] exceptionCompletions,
            long[] wrapperResets)
        {
            GetOrCreate(thread).Add(
                framesIndex,
                dispatcherId,
                depth,
                continuationIndexBase,
                wrapperCount,
                startQpc,
                endQpc,
                methodCompletions,
                exceptionCompletions,
                wrapperResets);
        }

        internal void AddDispatcherInfo(AsyncProfilerThreadKey creationThread, ulong dispatcherId, ulong parentDispatcherId, long creationQpc)
        {
            if (dispatcherId == 0)
            {
                return;
            }

            _dispatchers.Add(new DispatcherRecord(creationThread, dispatcherId, parentDispatcherId, creationQpc));
            InvalidateDispatcherIndexes();
        }

        internal bool TryGetInternedFrames(
            ProcessIndex processIndex,
            AsyncCallstackKind kind,
            ulong[] instructionPointers,
            int[] frameStates,
            int frameCount,
            out AsyncProfilerCallStackFramesIndex framesIndex,
            out AsyncProfilerCallStackFrames frames)
        {
            var key = new FrameKey(processIndex, kind, instructionPointers, frameStates, frameCount);
            if (_frameKeyToIndex.TryGetValue(key, out framesIndex))
            {
                frames = _internedCallStackFrames[(int)framesIndex];
                return true;
            }

            frames = null;
            return false;
        }

        private AsyncProfilerCallStackFramesIndex Intern(AsyncCallstackKind kind, ulong[] instructionPointers, int[] frameStates, ProcessIndex processIndex)
        {
            var key = new FrameKey(processIndex, kind, instructionPointers, frameStates, instructionPointers.Length);
            if (_frameKeyToIndex.TryGetValue(key, out AsyncProfilerCallStackFramesIndex existing))
            {
                return existing;
            }

            var index = (AsyncProfilerCallStackFramesIndex)_internedCallStackFrames.Count;
            var frames = new AsyncProfilerCallStackFrames(kind, instructionPointers, frameStates, processIndex);

            _internedCallStackFrames.Add(frames);
            _frameKeyToIndex[key] = index;
            OnFrameInterned?.Invoke(frames);

            return index;
        }

        private ThreadCallStacks GetOrCreate(AsyncProfilerThreadKey key)
        {
            if (!_threads.TryGetValue(key, out ThreadCallStacks callStacks))
            {
                callStacks = new ThreadCallStacks(key);
                _threads[key] = callStacks;
            }

            return callStacks;
        }

        private void InvalidateDispatcherIndexes()
        {
            lock (_dispatcherIndexLock)
            {
                _dispatchersById = null;
                _dispatchersByParentId = null;
            }
        }

        private void EnsureDispatcherIndexes()
        {
            if (_dispatchersById != null)
            {
                return;
            }

            lock (_dispatcherIndexLock)
            {
                if (_dispatchersById != null)
                {
                    return;
                }

                var byId = new Dictionary<DispatcherKey, List<int>>();
                var byParentId = new Dictionary<DispatcherKey, List<int>>();
                for (int i = 0; i < _dispatchers.Count; i++)
                {
                    DispatcherRecord dispatcher = _dispatchers[i];
                    AddDispatcherIndex(byId, new DispatcherKey(dispatcher.CreationThread.ProcessIndex, dispatcher.DispatcherId), i);
                    if (dispatcher.ParentDispatcherId != 0)
                    {
                        AddDispatcherIndex(byParentId, new DispatcherKey(dispatcher.CreationThread.ProcessIndex, dispatcher.ParentDispatcherId), i);
                    }
                }

                foreach (List<int> dispatcherIndices in byId.Values)
                {
                    dispatcherIndices.Sort((left, right) => _dispatchers[left].CreationQpc.CompareTo(_dispatchers[right].CreationQpc));
                }

                foreach (List<int> dispatcherIndices in byParentId.Values)
                {
                    dispatcherIndices.Sort((left, right) => _dispatchers[left].CreationQpc.CompareTo(_dispatchers[right].CreationQpc));
                }

                _dispatchersByParentId = byParentId;
                _dispatchersById = byId;
            }
        }

        private static void AddDispatcherIndex(Dictionary<DispatcherKey, List<int>> index, DispatcherKey key, int dispatcherIndex)
        {
            if (!index.TryGetValue(key, out List<int> dispatcherIndices))
            {
                dispatcherIndices = new List<int>();
                index.Add(key, dispatcherIndices);
            }

            dispatcherIndices.Add(dispatcherIndex);
        }

        private int GetDispatcherIndex(ProcessIndex processIndex, ulong dispatcherId, long timestampQpc)
        {
            EnsureDispatcherIndexes();
            var key = new DispatcherKey(processIndex, dispatcherId);
            if (!_dispatchersById.TryGetValue(key, out List<int> dispatcherIndices))
            {
                return -1;
            }

            int lo = 0;
            int hi = dispatcherIndices.Count;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (_dispatchers[dispatcherIndices[mid]].CreationQpc <= timestampQpc)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }

            if (lo == 0)
            {
                return -1;
            }

            return dispatcherIndices[lo - 1];
        }

        private static AsyncProfilerDispatcherInfo MaterializeDispatcherInfo(DispatcherRecord dispatcher)
        {
            return new AsyncProfilerDispatcherInfo(dispatcher.CreationThread, dispatcher.DispatcherId, dispatcher.ParentDispatcherId, dispatcher.CreationQpc);
        }

        void IFastSerializable.ToStream(Serializer serializer)
        {
            serializer.Write(SerializationVersion);

            serializer.Write(_internedCallStackFrames.Count);
            for (int i = 0; i < _internedCallStackFrames.Count; i++)
            {
                _internedCallStackFrames[i].Write(serializer);
            }

            serializer.Write(_threads.Count);
            foreach (KeyValuePair<AsyncProfilerThreadKey, ThreadCallStacks> pair in _threads)
            {
                serializer.Write((int)pair.Key.ProcessIndex);
                serializer.Write((long)pair.Key.OsThreadId);

                ThreadCallStacks recorded = pair.Value;
                serializer.Write(recorded.Count);
                for (int i = 0; i < recorded.Count; i++)
                {
                    recorded.Write(serializer, i);
                }
            }

            serializer.Write(_completionAvailability.Count);
            foreach (KeyValuePair<ProcessIndex, List<CompletionAvailabilityEpoch>> pair in _completionAvailability)
            {
                serializer.Write((int)pair.Key);
                serializer.Write(pair.Value.Count);
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    serializer.Write(pair.Value[i].StartQpc);
                    serializer.Write(pair.Value[i].Flags);
                }
            }

            serializer.Write(_dispatchers.Count);
            for (int i = 0; i < _dispatchers.Count; i++)
            {
                DispatcherRecord dispatcher = _dispatchers[i];
                serializer.Write((int)dispatcher.CreationThread.ProcessIndex);
                serializer.Write((long)dispatcher.CreationThread.OsThreadId);
                serializer.Write((long)dispatcher.DispatcherId);
                serializer.Write((long)dispatcher.ParentDispatcherId);
                serializer.Write(dispatcher.CreationQpc);
            }
        }

        void IFastSerializable.FromStream(Deserializer deserializer)
        {
            int version = deserializer.ReadInt();
            if (version != SerializationVersion)
            {
                throw new SerializationException("Unsupported AsyncProfilerCallStacksIndex serialization version " + version);
            }

            _internedCallStackFrames.Clear();
            _frameKeyToIndex.Clear();
            _threads.Clear();
            _completionAvailability.Clear();
            _dispatchers.Clear();
            InvalidateDispatcherIndexes();

            int frameCount = deserializer.ReadInt();
            for (int i = 0; i < frameCount; i++)
            {
                _internedCallStackFrames.Add(AsyncProfilerCallStackFrames.Read(deserializer));
            }

            int threadCount = deserializer.ReadInt();
            for (int t = 0; t < threadCount; t++)
            {
                ProcessIndex processIndex = (ProcessIndex)deserializer.ReadInt();
                ulong osThreadId = (ulong)deserializer.ReadInt64();
                int count = deserializer.ReadInt();
                var thread = new AsyncProfilerThreadKey(processIndex, osThreadId);
                var callStacks = new ThreadCallStacks(thread, count);
                _threads[thread] = callStacks;

                for (int i = 0; i < count; i++)
                {
                    callStacks.ReadAndAdd(deserializer);
                }
            }

            int processCount = deserializer.ReadInt();
            for (int i = 0; i < processCount; i++)
            {
                ProcessIndex processIndex = (ProcessIndex)deserializer.ReadInt();
                int epochCount = deserializer.ReadInt();
                var epochs = new List<CompletionAvailabilityEpoch>(epochCount);

                for (int e = 0; e < epochCount; e++)
                {
                    epochs.Add(new CompletionAvailabilityEpoch(deserializer.ReadInt64(), deserializer.ReadByte()));
                }

                _completionAvailability[processIndex] = epochs;
            }

            int dispatcherCount = deserializer.ReadInt();
            for (int i = 0; i < dispatcherCount; i++)
            {
                var creationThread = new AsyncProfilerThreadKey((ProcessIndex)deserializer.ReadInt(), (ulong)deserializer.ReadInt64());
                _dispatchers.Add(new DispatcherRecord(creationThread, (ulong)deserializer.ReadInt64(), (ulong)deserializer.ReadInt64(), deserializer.ReadInt64()));
            }
        }

        internal void PopulateCompletionObservations(AsyncProfilerCallStack callStack)
        {
            CompletionAvailabilityEpoch epoch = FindCompletionEpoch(callStack.Thread.ProcessIndex, callStack.StartQpc);
            AsyncCallstackKind kind = callStack.Frames.Kind;
            callStack.MethodCompletionObserved = epoch != null && epoch.MethodObserved(kind);
            callStack.ExceptionCompletionObserved = epoch != null && epoch.ExceptionObserved(kind);
        }

        private AsyncProfilerCallStackFrames ResolveFrames(AsyncProfilerCallStackFramesIndex index)
        {
            return _internedCallStackFrames[(int)index];
        }

        private static int CompareDepth(AsyncProfilerCallStack left, AsyncProfilerCallStack right)
        {
            return right.Depth.CompareTo(left.Depth);
        }

        private List<CompletionAvailabilityEpoch> GetCompletionEpochs(ProcessIndex processIndex)
        {
            if (!_completionAvailability.TryGetValue(processIndex, out List<CompletionAvailabilityEpoch> epochs))
            {
                epochs = new List<CompletionAvailabilityEpoch>();
                _completionAvailability[processIndex] = epochs;
            }

            return epochs;
        }

        private CompletionAvailabilityEpoch FindCompletionEpoch(ProcessIndex processIndex, long timestampQpc)
        {
            if (!_completionAvailability.TryGetValue(processIndex, out List<CompletionAvailabilityEpoch> epochs))
            {
                return null;
            }

            int index = FindCompletionEpochIndex(epochs, timestampQpc);
            if (index >= 0)
            {
                return epochs[index];
            }

            return null;
        }

        private static int FindCompletionEpochIndex(List<CompletionAvailabilityEpoch> epochs, long timestampQpc)
        {
            int lo = 0;
            int hi = epochs.Count;
            while (lo < hi)
            {
                int mid = (lo + hi) >> 1;
                if (epochs[mid].StartQpc <= timestampQpc)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid;
                }
            }

            return lo - 1;
        }

        // Each process tracks V1 and V2 completion events separately between metadata updates.
        // A call stack uses the period in which it started.
        private sealed class CompletionAvailabilityEpoch
        {
            public CompletionAvailabilityEpoch(long startQpc)
            {
                StartQpc = startQpc;
            }

            public CompletionAvailabilityEpoch(long startQpc, byte flags)
            {
                StartQpc = startQpc;
                Flags = flags;
            }

            public long StartQpc { get; }

            public byte Flags { get; private set; }

            public bool MethodObserved(AsyncCallstackKind kind)
            {
                if (kind == AsyncCallstackKind.StateMachineAsync)
                {
                    return (Flags & StateMachineMethod) != 0;
                }

                return (Flags & RuntimeMethod) != 0;
            }

            public bool ExceptionObserved(AsyncCallstackKind kind)
            {
                if (kind == AsyncCallstackKind.StateMachineAsync)
                {
                    return (Flags & StateMachineException) != 0;
                }

                return (Flags & RuntimeException) != 0;
            }

            public void MarkMethodObserved(AsyncCallstackKind kind)
            {
                if (kind == AsyncCallstackKind.StateMachineAsync)
                {
                    Flags |= StateMachineMethod;
                }
                else
                {
                    Flags |= RuntimeMethod;
                }
            }

            public void MarkExceptionObserved(AsyncCallstackKind kind)
            {
                if (kind == AsyncCallstackKind.StateMachineAsync)
                {
                    Flags |= StateMachineException;
                }
                else
                {
                    Flags |= RuntimeException;
                }
            }

            #region private

            private const byte RuntimeMethod = 1 << 0;
            private const byte StateMachineMethod = 1 << 1;
            private const byte RuntimeException = 1 << 2;
            private const byte StateMachineException = 1 << 3;

            #endregion
        }

        private readonly struct CallStackRecord
        {
            public CallStackRecord(
                ulong dispatcherId,
                int depth,
                AsyncProfilerCallStackFramesIndex framesIndex,
                int timelinesIndex,
                byte continuationIndexBase,
                byte wrapperCount,
                long startQpc,
                long endQpc)
            {
                DispatcherId = dispatcherId;
                Depth = depth;
                FramesIndex = framesIndex;
                TimelinesIndex = timelinesIndex;
                ContinuationIndexBase = continuationIndexBase;
                WrapperCount = wrapperCount;
                StartQpc = startQpc;
                EndQpc = endQpc;
            }

            public readonly long StartQpc;
            public readonly long EndQpc;
            public readonly ulong DispatcherId;
            public readonly int Depth;
            public readonly AsyncProfilerCallStackFramesIndex FramesIndex;
            public readonly int TimelinesIndex;
            public readonly byte ContinuationIndexBase;
            public readonly byte WrapperCount;
        }

        private readonly struct DispatcherRecord
        {
            public DispatcherRecord(AsyncProfilerThreadKey creationThread, ulong dispatcherId, ulong parentDispatcherId, long creationQpc)
            {
                CreationThread = creationThread;
                DispatcherId = dispatcherId;
                ParentDispatcherId = parentDispatcherId;
                CreationQpc = creationQpc;
            }

            public readonly AsyncProfilerThreadKey CreationThread;
            public readonly ulong DispatcherId;
            public readonly ulong ParentDispatcherId;
            public readonly long CreationQpc;
        }

        private sealed class CallStackTimelines
        {
            public CallStackTimelines(
                AsyncProfilerCallStack.CompletionDelta[] methodCompletions,
                AsyncProfilerCallStack.CompletionDelta[] exceptionCompletions,
                long[] wrapperResets)
            {
                MethodCompletions = methodCompletions;
                ExceptionCompletions = exceptionCompletions;
                WrapperResets = wrapperResets;
            }

            public readonly AsyncProfilerCallStack.CompletionDelta[] MethodCompletions;
            public readonly AsyncProfilerCallStack.CompletionDelta[] ExceptionCompletions;
            public readonly long[] WrapperResets;
        }

        /// <summary>Recorded call stacks for one thread, with an interval index built when needed.</summary>
        private sealed class ThreadCallStacks
        {
            public ThreadCallStacks(AsyncProfilerThreadKey thread)
            {
                _thread = thread;
                _recorded = new CallStackRecordCollection();
            }

            public ThreadCallStacks(AsyncProfilerThreadKey thread, int capacity)
            {
                _thread = thread;
                _recorded = new CallStackRecordCollection(capacity);
            }

            public int Count
            {
                get
                {
                    return _recorded.Count;
                }
            }

            public void Add(
                AsyncProfilerCallStackFramesIndex framesIndex,
                ulong dispatcherId,
                int depth,
                byte continuationIndexBase,
                byte wrapperCount,
                long startQpc,
                long endQpc,
                AsyncProfilerCallStack.CompletionDelta[] methodCompletions,
                AsyncProfilerCallStack.CompletionDelta[] exceptionCompletions,
                long[] wrapperResets)
            {
                int timelinesIndex = AddTimelines(methodCompletions, exceptionCompletions, wrapperResets);
                _recorded.Add(new CallStackRecord(dispatcherId, depth, framesIndex, timelinesIndex, continuationIndexBase, wrapperCount, startQpc, endQpc));
                _index = null;
            }

            public void ReadAndAdd(Deserializer deserializer)
            {
                ulong dispatcherId = (ulong)deserializer.ReadInt64();
                int depth = deserializer.ReadInt();
                var framesIndex = (AsyncProfilerCallStackFramesIndex)deserializer.ReadInt();
                byte continuationIndexBase = deserializer.ReadByte();
                byte wrapperCount = deserializer.ReadByte();
                long startQpc = deserializer.ReadInt64();
                long endQpc = deserializer.ReadInt64();

                AsyncProfilerCallStack.CompletionDelta[] methodCompletions = ReadCompletions(deserializer);
                AsyncProfilerCallStack.CompletionDelta[] exceptionCompletions = ReadCompletions(deserializer);
                long[] wrapperResets = ReadQpcs(deserializer);
                Add(framesIndex, dispatcherId, depth, continuationIndexBase, wrapperCount, startQpc, endQpc, methodCompletions, exceptionCompletions, wrapperResets);
            }

            public void Write(Serializer serializer, int index)
            {
                CallStackRecord record = _recorded[index];
                GetTimelines(
                    record.TimelinesIndex,
                    out AsyncProfilerCallStack.CompletionDelta[] methodCompletions,
                    out AsyncProfilerCallStack.CompletionDelta[] exceptionCompletions,
                    out long[] wrapperResets);
                serializer.Write((long)record.DispatcherId);
                serializer.Write(record.Depth);
                serializer.Write((int)record.FramesIndex);
                serializer.Write(record.ContinuationIndexBase);
                serializer.Write(record.WrapperCount);
                serializer.Write(record.StartQpc);
                serializer.Write(record.EndQpc);
                Write(serializer, methodCompletions);
                Write(serializer, exceptionCompletions);
                Write(serializer, wrapperResets);
            }

            public AsyncProfilerCallStack Materialize(int index, AsyncProfilerCallStackFrames frames, AsyncProfilerCallStack reusable = null)
            {
                CallStackRecord record = _recorded[index];
                GetTimelines(
                    record.TimelinesIndex,
                    out AsyncProfilerCallStack.CompletionDelta[] methodCompletions,
                    out AsyncProfilerCallStack.CompletionDelta[] exceptionCompletions,
                    out long[] wrapperResets);
                if (reusable == null)
                {
                    return new AsyncProfilerCallStack(
                        _thread,
                        record.DispatcherId,
                        record.Depth,
                        record.FramesIndex,
                        frames,
                        record.ContinuationIndexBase,
                        record.WrapperCount,
                        record.StartQpc,
                        record.EndQpc,
                        methodCompletions,
                        exceptionCompletions,
                        wrapperResets);
                }

                reusable.Reset(
                    _thread,
                    record.DispatcherId,
                    record.Depth,
                    record.FramesIndex,
                    frames,
                    record.ContinuationIndexBase,
                    record.WrapperCount,
                    record.StartQpc,
                    record.EndQpc,
                    methodCompletions,
                    exceptionCompletions,
                    wrapperResets);

                return reusable;
            }

            public AsyncProfilerCallStackFramesIndex FramesIndexAt(int index)
            {
                return _recorded[index].FramesIndex;
            }

            public void Query(long timestampQpc, List<AsyncProfilerCallStack> result, AsyncProfilerCallStacksIndex owner, bool cacheMaterialized)
            {
                if (cacheMaterialized)
                {
                    lock (_queryLock)
                    {
                        QueryCore(timestampQpc, result, owner, cacheMaterialized: true);
                    }

                    return;
                }

                QueryCore(timestampQpc, result, owner, cacheMaterialized: false);
            }

            public AsyncProfilerCallStack GetOrCreateMaterialized(int index, AsyncProfilerCallStackFrames frames)
            {
                if (_materialized == null)
                {
                    _materialized = new Dictionary<int, AsyncProfilerCallStack>();
                }

                if (!_materialized.TryGetValue(index, out AsyncProfilerCallStack callStack))
                {
                    callStack = Materialize(index, frames);
                    _materialized[index] = callStack;
                }

                return callStack;
            }

            public AsyncProfilerCallStack GetOrCreateMaterializedThreadSafe(int index, AsyncProfilerCallStackFrames frames)
            {
                lock (_queryLock)
                {
                    return GetOrCreateMaterialized(index, frames);
                }
            }

            public CallStacksIntervalIndex QueryIndex()
            {
                CallStacksIntervalIndex index = Volatile.Read(ref _index);
                if (index != null)
                {
                    return index;
                }

                lock (_queryLock)
                {
                    index = _index;
                    if (index == null)
                    {
                        index = new CallStacksIntervalIndex(_recorded);
                        Volatile.Write(ref _index, index);
                    }

                    return index;
                }
            }

            #region private

            private void QueryCore(long timestampQpc, List<AsyncProfilerCallStack> result, AsyncProfilerCallStacksIndex owner, bool cacheMaterialized)
            {
                int count = 0;
                QueryIndex().Stab(timestampQpc, this, result, owner, cacheMaterialized, ref count);
                if (result.Count > count)
                {
                    result.RemoveRange(count, result.Count - count);
                }
            }

            private int AddTimelines(
                AsyncProfilerCallStack.CompletionDelta[] methodCompletions,
                AsyncProfilerCallStack.CompletionDelta[] exceptionCompletions,
                long[] wrapperResets)
            {
                if (methodCompletions.Length == 0 && exceptionCompletions.Length == 0 && wrapperResets.Length == 0)
                {
                    return -1;
                }

                if (_timelines == null)
                {
                    _timelines = new List<CallStackTimelines>();
                }

                int index = _timelines.Count;
                _timelines.Add(new CallStackTimelines(methodCompletions, exceptionCompletions, wrapperResets));

                return index;
            }

            private void GetTimelines(
                int index,
                out AsyncProfilerCallStack.CompletionDelta[] methodCompletions,
                out AsyncProfilerCallStack.CompletionDelta[] exceptionCompletions,
                out long[] wrapperResets)
            {
                if (index < 0)
                {
                    methodCompletions = Array.Empty<AsyncProfilerCallStack.CompletionDelta>();
                    exceptionCompletions = Array.Empty<AsyncProfilerCallStack.CompletionDelta>();
                    wrapperResets = Array.Empty<long>();
                    return;
                }

                CallStackTimelines timelines = _timelines[index];
                methodCompletions = timelines.MethodCompletions;
                exceptionCompletions = timelines.ExceptionCompletions;
                wrapperResets = timelines.WrapperResets;
            }

            private static AsyncProfilerCallStack.CompletionDelta[] ReadCompletions(Deserializer deserializer)
            {
                int count = deserializer.ReadInt();
                if (count == 0)
                {
                    return Array.Empty<AsyncProfilerCallStack.CompletionDelta>();
                }

                var result = new AsyncProfilerCallStack.CompletionDelta[count];
                for (int i = 0; i < count; i++)
                {
                    result[i] = new AsyncProfilerCallStack.CompletionDelta(deserializer.ReadInt64(), deserializer.ReadInt());
                }

                return result;
            }

            private static long[] ReadQpcs(Deserializer deserializer)
            {
                int count = deserializer.ReadInt();
                if (count == 0)
                {
                    return Array.Empty<long>();
                }

                var result = new long[count];
                for (int i = 0; i < count; i++)
                {
                    result[i] = deserializer.ReadInt64();
                }

                return result;
            }

            private static void Write(Serializer serializer, AsyncProfilerCallStack.CompletionDelta[] values)
            {
                serializer.Write(values.Length);
                for (int i = 0; i < values.Length; i++)
                {
                    serializer.Write(values[i].Qpc);
                    serializer.Write(values[i].Delta);
                }
            }

            private static void Write(Serializer serializer, long[] values)
            {
                serializer.Write(values.Length);
                for (int i = 0; i < values.Length; i++)
                {
                    serializer.Write(values[i]);
                }
            }

            private readonly AsyncProfilerThreadKey _thread;
            private readonly CallStackRecordCollection _recorded;
            private readonly object _queryLock = new object();
            private List<CallStackTimelines> _timelines;
            private Dictionary<int, AsyncProfilerCallStack> _materialized;
            private CallStacksIntervalIndex _index;

            #endregion
        }

        private sealed class CallStackRecordCollection
        {
            public CallStackRecordCollection()
            {
                _chunks = new List<CallStackRecord[]>();
            }

            public CallStackRecordCollection(int capacity)
            {
                _items = new CallStackRecord[capacity];
            }

            public int Count
            {
                get
                {
                    return _count;
                }
            }

            public CallStackRecord this[int index]
            {
                get
                {
                    if ((uint)index >= (uint)_count)
                    {
                        throw new ArgumentOutOfRangeException(nameof(index));
                    }

                    if (_items != null)
                    {
                        return _items[index];
                    }

                    return _chunks[index >> ChunkShift][index & ChunkMask];
                }
            }

            public void Add(CallStackRecord record)
            {
                if (_items != null)
                {
                    _items[_count++] = record;
                    return;
                }

                int chunkIndex = _count >> ChunkShift;
                if (chunkIndex == _chunks.Count)
                {
                    _chunks.Add(new CallStackRecord[ChunkSize]);
                }

                _chunks[chunkIndex][_count & ChunkMask] = record;
                _count++;
            }

            #region private

            private const int ChunkShift = 12;
            private const int ChunkSize = 1 << ChunkShift;
            private const int ChunkMask = ChunkSize - 1;

            private readonly CallStackRecord[] _items;
            private readonly List<CallStackRecord[]> _chunks;
            private int _count;

            #endregion
        }

        /// <summary>
        /// Finds active call stacks in start-time order, skipping subtrees whose call stacks have all ended.
        /// </summary>
        private sealed class CallStacksIntervalIndex
        {
            public CallStacksIntervalIndex(CallStackRecordCollection callStacks)
            {
                _records = callStacks;
                _byStart = new int[callStacks.Count];
                for (int i = 0; i < _byStart.Length; i++)
                {
                    _byStart[i] = i;
                }

                Array.Sort(_byStart, (a, b) => callStacks[a].StartQpc.CompareTo(callStacks[b].StartQpc));
                _maxEnd = new long[_byStart.Length];
                Build(0, _byStart.Length - 1);
            }

            public void Stab(
                long timestampQpc,
                ThreadCallStacks callStacks,
                List<AsyncProfilerCallStack> result,
                AsyncProfilerCallStacksIndex owner,
                bool cacheMaterialized,
                ref int count)
            {
                Stab(0, _byStart.Length - 1, timestampQpc, callStacks, result, owner, cacheMaterialized, ref count);
            }

            public bool IsLatestCallStack(AsyncProfilerCallStack callStack, long timestampQpc)
            {
                int lo = 0;
                int hi = _byStart.Length;
                while (lo < hi)
                {
                    int mid = lo + ((hi - lo) >> 1);
                    if (_records[_byStart[mid]].StartQpc <= timestampQpc)
                    {
                        lo = mid + 1;
                    }
                    else
                    {
                        hi = mid;
                    }
                }

                if (lo == 0)
                {
                    return false;
                }

                // A later call stack can leave its dispatcher on the physical stack even after it ends.
                CallStackRecord latest = _records[_byStart[lo - 1]];
                if (latest.StartQpc != callStack.StartQpc || latest.EndQpc != callStack.EndQpc)
                {
                    return false;
                }

                if (latest.EndQpc <= timestampQpc)
                {
                    return false;
                }

                bool sameCallStack =
                    latest.DispatcherId == callStack.DispatcherId &&
                    latest.Depth == callStack.Depth &&
                    latest.FramesIndex == callStack.FramesIndex;
                if (!sameCallStack)
                {
                    return false;
                }

                if (lo >= 2 && _records[_byStart[lo - 2]].StartQpc == latest.StartQpc)
                {
                    return false;
                }

                return true;
            }

            #region private

            private long Build(int lo, int hi)
            {
                if (lo > hi)
                {
                    return long.MinValue;
                }

                int mid = (lo + hi) >> 1;
                long left = Build(lo, mid - 1);
                long right = Build(mid + 1, hi);
                long max = _records[_byStart[mid]].EndQpc;
                if (left > max)
                {
                    max = left;
                }

                if (right > max)
                {
                    max = right;
                }

                _maxEnd[mid] = max;
                return max;
            }

            private void Stab(
                int lo,
                int hi,
                long timestampQpc,
                ThreadCallStacks callStacks,
                List<AsyncProfilerCallStack> result,
                AsyncProfilerCallStacksIndex owner,
                bool cacheMaterialized,
                ref int count)
            {
                if (lo > hi)
                {
                    return;
                }

                int mid = (lo + hi) >> 1;
                if (_maxEnd[mid] <= timestampQpc)
                {
                    return; // All call stacks in this subtree have ended.
                }

                Stab(lo, mid - 1, timestampQpc, callStacks, result, owner, cacheMaterialized, ref count);

                int recordIndex = _byStart[mid];
                CallStackRecord record = _records[recordIndex];
                if (record.StartQpc <= timestampQpc)
                {
                    if (record.EndQpc > timestampQpc)
                    {
                        AsyncProfilerCallStack materialized;
                        if (cacheMaterialized)
                        {
                            materialized = callStacks.GetOrCreateMaterialized(recordIndex, owner.ResolveFrames(record.FramesIndex));
                            owner.PopulateCompletionObservations(materialized);
                            result.Add(materialized);
                        }
                        else
                        {
                            AsyncProfilerCallStack reusable = null;
                            if (count < result.Count)
                            {
                                reusable = result[count];
                            }

                            materialized = callStacks.Materialize(recordIndex, owner.ResolveFrames(record.FramesIndex), reusable);
                            if (reusable == null)
                            {
                                result.Add(materialized);
                            }
                        }
                        count++;
                    }

                    Stab(mid + 1, hi, timestampQpc, callStacks, result, owner, cacheMaterialized, ref count);
                }
                // Otherwise, all call stacks to the right start after the query time.
            }

            private readonly int[] _byStart;            // Record indices in start-time order.
            private readonly long[] _maxEnd;            // Latest end QPC in the subtree rooted at each position.
            private readonly CallStackRecordCollection _records;

            #endregion
        }

        private sealed class ThreadCallStackList : IReadOnlyList<AsyncProfilerCallStack>
        {
            public ThreadCallStackList(AsyncProfilerCallStacksIndex owner, ThreadCallStacks callStacks)
            {
                _owner = owner;
                _callStacks = callStacks;
            }

            public int Count
            {
                get
                {
                    return _callStacks.Count;
                }
            }

            public AsyncProfilerCallStack this[int index]
            {
                get
                {
                    AsyncProfilerCallStack result = _callStacks.GetOrCreateMaterializedThreadSafe(index, _owner.ResolveFrames(_callStacks.FramesIndexAt(index)));
                    _owner.PopulateCompletionObservations(result);
                    return result;
                }
            }

            public IEnumerator<AsyncProfilerCallStack> GetEnumerator()
            {
                for (int i = 0; i < Count; i++)
                {
                    yield return this[i];
                }
            }

            #region private

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            private readonly AsyncProfilerCallStacksIndex _owner;
            private readonly ThreadCallStacks _callStacks;

            #endregion
        }

        private readonly struct DispatcherKey : IEquatable<DispatcherKey>
        {
            public DispatcherKey(ProcessIndex processIndex, ulong dispatcherId)
            {
                ProcessIndex = processIndex;
                DispatcherId = dispatcherId;
            }

            public ProcessIndex ProcessIndex { get; }
            public ulong DispatcherId { get; }

            public bool Equals(DispatcherKey other)
            {
                return ProcessIndex == other.ProcessIndex && DispatcherId == other.DispatcherId;
            }

            public override bool Equals(object obj)
            {
                return obj is DispatcherKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return ((int)ProcessIndex * 397) ^ DispatcherId.GetHashCode();
                }
            }
        }

        private readonly struct FrameKey : IEquatable<FrameKey>
        {
            public FrameKey(ProcessIndex processIndex, AsyncCallstackKind kind, ulong[] instructionPointers, int[] frameStates, int frameCount)
            {
                _processIndex = processIndex;
                _kind = kind;
                _instructionPointers = instructionPointers;
                _frameStates = frameStates;
                _frameCount = frameCount;

                int hash = ((int)processIndex * 31) + (int)kind;
                unchecked
                {
                    for (int i = 0; i < frameCount; i++)
                    {
                        hash = (hash * 31) + instructionPointers[i].GetHashCode();
                    }

                    if (frameStates != null)
                    {
                        for (int i = 0; i < frameCount; i++)
                        {
                            hash = (hash * 31) + frameStates[i];
                        }
                    }
                }

                _hash = hash;
            }

            public bool Equals(FrameKey other)
            {
                bool sameHeader =
                    _processIndex == other._processIndex &&
                    _kind == other._kind &&
                    _hash == other._hash &&
                    _frameCount == other._frameCount;
                if (!sameHeader)
                {
                    return false;
                }

                for (int i = 0; i < _frameCount; i++)
                {
                    if (_instructionPointers[i] != other._instructionPointers[i])
                    {
                        return false;
                    }
                }

                if ((_frameStates == null) != (other._frameStates == null))
                {
                    return false;
                }

                if (_frameStates != null)
                {
                    for (int i = 0; i < _frameCount; i++)
                    {
                        if (_frameStates[i] != other._frameStates[i])
                        {
                            return false;
                        }
                    }
                }

                return true;
            }

            public override bool Equals(object obj)
            {
                return obj is FrameKey o && Equals(o);
            }

            public override int GetHashCode()
            {
                return _hash;
            }

            #region private

            private readonly ProcessIndex _processIndex;
            private readonly AsyncCallstackKind _kind;
            private readonly ulong[] _instructionPointers;
            private readonly int[] _frameStates;
            private readonly int _frameCount;
            private readonly int _hash;

            #endregion
        }

        private const int SerializationVersion = 1;

        private readonly List<AsyncProfilerCallStackFrames> _internedCallStackFrames = new List<AsyncProfilerCallStackFrames>();

        private readonly Dictionary<FrameKey, AsyncProfilerCallStackFramesIndex> _frameKeyToIndex = new Dictionary<FrameKey, AsyncProfilerCallStackFramesIndex>();

        private readonly Dictionary<AsyncProfilerThreadKey, ThreadCallStacks> _threads = new Dictionary<AsyncProfilerThreadKey, ThreadCallStacks>();

        private readonly List<DispatcherRecord> _dispatchers = new List<DispatcherRecord>();

        private readonly object _dispatcherIndexLock = new object();

        private Dictionary<DispatcherKey, List<int>> _dispatchersById;

        private Dictionary<DispatcherKey, List<int>> _dispatchersByParentId;

        private readonly Dictionary<ProcessIndex, List<CompletionAvailabilityEpoch>> _completionAvailability = new Dictionary<ProcessIndex, List<CompletionAvailabilityEpoch>>();

        #endregion
    }
}
