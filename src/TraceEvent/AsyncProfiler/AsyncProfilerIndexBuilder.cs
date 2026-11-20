// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;

namespace Microsoft.Diagnostics.Tracing.Computers
{
    /// <summary>
    /// Builds an index of the async call stacks active on each thread from V1 and V2 profiler events.
    /// Tracks nested call stacks and assembles V1 frames across resume and append events.
    /// Records each call stack's active interval and finalized frames in <see cref="Index"/>.
    /// Call <see cref="Finish"/> after processing to include call stacks still open at trace end.
    /// </summary>
    internal sealed class AsyncProfilerIndexBuilder : ISubEventSink, ICallstackPayloadSink
    {
        /// <summary>
        /// Creates an index builder driven directly through <see cref="Process(byte[])"/>.
        /// </summary>
        public AsyncProfilerIndexBuilder()
        {
        }

        /// <summary>
        /// Subscribes to the parser, using <paramref name="processIndexOf"/> to identify each event's process.
        /// </summary>
        internal AsyncProfilerIndexBuilder(
            AsyncProfilerTraceEventParser parser, Func<AsyncProfilerTraceEvent, ProcessIndex> processIndexOf)
        {
            _parser = parser ?? throw new ArgumentNullException(nameof(parser));
            _processIndexOf = processIndexOf ?? throw new ArgumentNullException(nameof(processIndexOf));
            _parser.AsyncEvents += OnRawTraceEvent;
        }

        /// <summary>
        /// Reports nonzero native code IPs from accepted resume and append events.
        /// This lets the caller register addresses before method rundown, even when frames are recorded at trace end.
        /// Arguments: process instance, instruction pointer, instrumentation kind.
        /// </summary>
        public Action<ProcessIndex, ulong, AsyncCallstackKind> OnFrameObserved;

        /// <summary>
        /// Reports a decode error after closing or discarding the affected open call stacks.
        /// Arguments: process instance, error details.
        /// </summary>
        public event Action<ProcessIndex, ParseError> ParseError;

        /// <summary>
        /// The recorded call-stack intervals and shared frame lists.
        /// Call <see cref="Finish"/> after processing to include call stacks still open at trace end.
        /// </summary>
        public AsyncProfilerCallStacksIndex Index => _index;

        /// <summary>True when the default process instance (index 0) has a nonzero QPC frequency.</summary>
        public bool ClockKnown => ClockKnownForProcess(0);

        /// <summary>Wrapper pool size for the default process instance (index 0), or 0 if unknown.</summary>
        public byte WrapperCount => GetWrapperCount(0);

        /// <summary>
        /// Number of unique call-stack frame lists stored in the index, not individual frames.
        /// Several call-stack intervals can share one frame list and contribute only one to this count.
        /// </summary>
        public int DistinctFramesCount => _index.DistinctFramesCount;

        /// <summary>The number of decode failures reported by this index builder.</summary>
        public long ParseErrorCount { get; private set; }

        /// <summary>
        /// Decodes an async-profiler buffer for the default process instance (index 0). For multiple processes,
        /// use <see cref="Process(byte[], ProcessIndex)"/> to keep their manifests and open call stacks separate.
        /// </summary>
        public void Process(byte[] buffer)
        {
            Process(buffer, 0);
        }

        /// <summary>Decodes an async-profiler buffer for one process instance into the index.</summary>
        /// <param name="buffer">The raw buffer from an <c>AsyncEvents</c> event.</param>
        /// <param name="processIndex">The unique process instance in the trace, not its reusable OS process id.</param>
        /// <remarks>
        /// Processing is synchronous. Use the same builder and process index for every buffer from that process.
        /// On a decode error, open call stacks are closed at the last known QPC, or discarded if no usable end
        /// timestamp is available. The affected thread ignores events until a valid reset.
        /// Unreadable headers and malformed metadata affect every thread in the process and require fresh metadata
        /// followed by per-thread resets. Previously recorded intervals and other processes are preserved.
        /// Errors are reported through <see cref="ParseError"/> and <see cref="ParseErrorCount"/>.
        /// </remarks>
        public void Process(byte[] buffer, ProcessIndex processIndex)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            Process(buffer, buffer.Length, processIndex);
        }

        private void Process(byte[] buffer, int length, ProcessIndex processIndex)
        {
            ProcessBuildState processState = GetOrCreateProcessState(processIndex);
            _currentProcessIndex = processIndex;
            AsyncProfilerTraceEventParser.ParseBuffer(buffer, length, processState.Manifest, this);
        }

        /// <summary>
        /// Records open call stacks with <c>EndQpc = long.MaxValue</c> so they remain queryable through trace end.
        /// Call once after processing all events.
        /// </summary>
        public void Finish()
        {
            foreach (KeyValuePair<AsyncProfilerThreadKey, CallStacks> thread in _threads)
            {
                CommitOpenCallStacks(thread.Key, thread.Value, long.MaxValue);
            }
        }

        /// <summary>Gets a shared frame list from the index, or null when the handle is out of range.</summary>
        public AsyncProfilerCallStackFrames GetFrames(AsyncProfilerCallStackFramesIndex index) =>
            _index.GetFrames(index);

        /// <summary>
        /// Returns the nested async call stacks active on <paramref name="thread"/> at <paramref name="timestampQpc"/>,
        /// ordered leaf-to-root. Empty if the thread was unarmed or idle at that instant.
        /// </summary>
        /// <remarks>
        /// Returns recorded intervals only. Later queries do not overwrite the returned call-stack objects.
        /// </remarks>
        public IReadOnlyList<AsyncProfilerCallStack> GetCallStacks(AsyncProfilerThreadKey thread, long timestampQpc) =>
            _index.GetCallStacks(thread, timestampQpc);

        /// <summary>True when <paramref name="processIndex"/> has a nonzero QPC frequency.</summary>
        public bool ClockKnownForProcess(ProcessIndex processIndex) =>
            _processStates.TryGetValue(processIndex, out ProcessBuildState processState) && processState.QpcFrequency != 0;

        /// <summary>The latest wrapper pool size for <paramref name="processIndex"/>, or 0 if unavailable.</summary>
        public byte GetWrapperCount(ProcessIndex processIndex) =>
            _processStates.TryGetValue(processIndex, out ProcessBuildState processState) ? processState.WrapperCount : (byte)0;

        /// <summary>Converts QPC to UTC for process instance 0, or returns null if the clock is unknown.</summary>
        public DateTime? QpcToDateTime(long timestampQpc)
        {
            return QpcToDateTime(0, timestampQpc);
        }

        /// <summary>
        /// Converts an absolute QPC timestamp to UTC using the current clock synchronization for
        /// <paramref name="processIndex"/>; returns null when that process has no usable metadata.
        /// </summary>
        public DateTime? QpcToDateTime(ProcessIndex processIndex, long timestampQpc)
        {
            if (!_processStates.TryGetValue(processIndex, out ProcessBuildState processState) || processState.QpcFrequency == 0)
            {
                return null;
            }

            long frequency = checked((long)processState.QpcFrequency);
            long qpcDelta = timestampQpc - checked((long)processState.QpcSync);
            // Split the delta before converting to UTC ticks so a large QPC value is not multiplied directly.
            long wholeSeconds = qpcDelta / frequency;
            long remainingQpc = qpcDelta % frequency;
            long utcTicks =
                checked(checked((long)processState.UtcSync) +
                checked(wholeSeconds * TimeSpan.TicksPerSecond) +
                checked(remainingQpc * TimeSpan.TicksPerSecond) / frequency);

            return DateTime.FromFileTimeUtc(utcTicks);
        }

        #region private

        #region sink

        /// <summary>Records a dispatcher creation after the thread has been armed by a reset.</summary>
        void ISubEventSink.OnContextCreate(in AsyncContextEvent e)
        {
            AsyncProfilerThreadKey creationThread = ThreadKeyOf(e.OsThreadId);
            CallStacks state = GetOrCreate(creationThread);
            if (state.Armed)
            {
                state.RecordDispatcherCreation(
                    _index, creationThread, e.DispatcherId, e.ParentDispatcherId, e.TimestampQpc);
            }
        }

        void ISubEventSink.OnContextResume(in AsyncContextEvent e)
        {
            // The resume-callstack event supplies the frames and opens the call stack.
        }

        void ISubEventSink.OnContextSuspend(in AsyncContextEvent e) =>
            CloseTop(ThreadKeyOf(e.OsThreadId), e.TimestampQpc);

        void ISubEventSink.OnContextComplete(in AsyncContextEvent e) =>
            CloseTop(ThreadKeyOf(e.OsThreadId), e.TimestampQpc);

        void ISubEventSink.OnCallstack(in AsyncCallstackEvent e) => ProcessCallstack(e, reusablePayload: false);

        /// <summary>Decodes a call stack into reusable buffers, then processes the decoded event.</summary>
        bool ICallstackPayloadSink.TryOnCallstack(AsyncEventID eventId, long timestampQpc,
            in BufferHeader header, byte[] buffer, ref int index, int payloadEnd)
        {
            if (!AsyncCallstackEvent.TryReadInto(eventId, timestampQpc, header, buffer, ref index, payloadEnd,
                _callstackMethodIds, _callstackFrameStates, out AsyncCallstackEvent callstack))
            {
                return false;
            }

            ProcessCallstack(callstack, reusablePayload: true);
            return true;
        }

        /// <summary>Records creation information, opens a call stack, or appends to the current call stack.</summary>
        private void ProcessCallstack(in AsyncCallstackEvent e, bool reusablePayload)
        {
            AsyncProfilerThreadKey thread = ThreadKeyOf(e.OsThreadId);
            CallStacks state = GetOrCreate(thread);
            if (!state.Armed)
            {
                return;
            }

            if (e.EventId == AsyncEventID.CreateRuntimeAsyncCallstack)
            {
                state.RecordDispatcherCreation(_index, thread, e.DispatcherId, e.ParentDispatcherId, e.TimestampQpc);
                return;
            }

            if (IsResumeCallstack(e.EventId))
            {
                state.Push(RentBuilder(e, CurrentProcessState.WrapperCount, reusablePayload));
            }
            else if (IsAppendCallstack(e.EventId))
            {
                CallStackBuilder currentCallStack = state.Top;
                bool hasMatchingAppendTarget =
                    currentCallStack != null &&
                    currentCallStack.Kind == e.Kind &&
                    currentCallStack.DispatcherId == e.DispatcherId;
                if (!hasMatchingAppendTarget)
                {
                    return;
                }

                currentCallStack.AddFrames(e);
            }
            else
            {
                return;
            }

            // Report addresses now; waiting for a later commit could miss method rundown.
            ReportObservedFrames(e);
        }

        private void ReportObservedFrames(in AsyncCallstackEvent e)
        {
            if (OnFrameObserved == null || e.MethodIds == null)
            {
                return;
            }

            for (int i = 0; i < e.FrameCount; i++)
            {
                ulong instructionPointer = e.MethodIds[i];
                if (instructionPointer != 0)
                {
                    OnFrameObserved(_currentProcessIndex, instructionPointer, e.Kind);
                }
            }
        }

        void ISubEventSink.OnMethodResume(in AsyncMethodEvent e)
        {
            // Method resumption does not change the captured frame list.
        }

        /// <summary>Records method-completion availability and advances the current call stack, if any.</summary>
        void ISubEventSink.OnMethodComplete(in AsyncMethodEvent e)
        {
            AsyncProfilerThreadKey key = ThreadKeyOf(e.OsThreadId);
            CallStacks state = GetOrCreate(key);
            if (!state.Armed)
            {
                return;
            }

            AsyncCallstackKind kind = e.IsStateMachine ? AsyncCallstackKind.StateMachineAsync : AsyncCallstackKind.RuntimeAsync;
            _index.MarkMethodCompletionObserved(_currentProcessIndex, kind, e.TimestampQpc);
            state.Top?.MethodCompletions.Add(new AsyncProfilerCallStack.CompletionDelta(e.TimestampQpc, 1));
        }

        /// <summary>Records unwind availability and completion counts for the current call stack.</summary>
        void ISubEventSink.OnException(in AsyncUnwindEvent e)
        {
            AsyncProfilerThreadKey key = ThreadKeyOf(e.OsThreadId);
            CallStacks state = GetOrCreate(key);
            if (!state.Armed)
            {
                return;
            }

            AsyncCallstackKind kind = e.IsStateMachine ? AsyncCallstackKind.StateMachineAsync : AsyncCallstackKind.RuntimeAsync;
            _index.MarkExceptionCompletionObserved(_currentProcessIndex, kind, e.TimestampQpc);
            if (e.UnwoundFrameCount > 0)
            {
                var completion = new AsyncProfilerCallStack.CompletionDelta(e.TimestampQpc, (int)e.UnwoundFrameCount);
                state.Top?.ExceptionCompletions.Add(completion);
            }
        }

        /// <summary>Closes open call stacks at a valid reset and arms the thread for subsequent events.</summary>
        void ISubEventSink.OnResetThreadContext(in AsyncNeutralEvent e)
        {
            // Delivery order can differ from timestamp order. Ignore resets from before the first metadata QPC.
            // A reset at the same QPC is valid on a coarse-resolution clock.
            ProcessBuildState processState = CurrentProcessState;
            if (e.TimestampQpc < processState.FirstMetadataQpc)
            {
                return;
            }

            // Preserve the valid [resume, reset) prefix before clearing the thread's nesting state.
            AsyncProfilerThreadKey key = ThreadKeyOf(e.OsThreadId);
            CallStacks state = GetOrCreate(key);
            if (state.Armed)
            {
                CommitOpenCallStacks(key, state, e.TimestampQpc);
            }

            state.Armed = true;
            state.ClearNesting();
        }

        /// <summary>Records a wrapper-index reset on the current call stack, if any.</summary>
        void ISubEventSink.OnResetContinuationWrapperIndex(in AsyncNeutralEvent e)
        {
            CallStacks state = GetOrCreate(ThreadKeyOf(e.OsThreadId));
            if (state.Armed)
            {
                state.Top?.WrapperResets.Add(e.TimestampQpc);
            }
        }

        /// <summary>Starts a completion-observation epoch and saves clock and wrapper settings.</summary>
        void ISubEventSink.OnMetadata(in AsyncMetadataEvent e)
        {
            ProcessBuildState processState = CurrentProcessState;
            _index.StartCompletionAvailabilityEpoch(_currentProcessIndex, e.TimestampQpc);
            // Buffers can arrive out of timestamp order, so keep the earliest metadata QPC.
            if (e.TimestampQpc < processState.FirstMetadataQpc)
            {
                processState.FirstMetadataQpc = e.TimestampQpc;
            }

            processState.QpcFrequency = e.QpcFrequency;
            processState.QpcSync = e.QpcSync;
            processState.UtcSync = e.UtcSync;
            processState.WrapperCount = e.WrapperCount;
        }

        /// <summary>Updates the process's QPC/UTC synchronization point without changing its frequency.</summary>
        void ISubEventSink.OnSyncClock(in AsyncSyncClockEvent e)
        {
            ProcessBuildState processState = CurrentProcessState;
            processState.QpcSync = e.QpcSync;
            processState.UtcSync = e.UtcSync;
        }

        void ISubEventSink.OnUnknown(in AsyncUnknownEvent e) { }

        /// <summary>Invalidates affected open call stacks before notifying subscribers of a decode error.</summary>
        void ISubEventSink.OnParseError(in ParseError e)
        {
            ParseErrorCount++;

            // A bad header cannot identify a thread; bad metadata affects every thread's decoding.
            bool affectsEntireProcess = !e.BufferHeader.HasValue || e.EventId == AsyncEventID.AsyncProfilerMetadata;
            if (affectsEntireProcess)
            {
                foreach (KeyValuePair<AsyncProfilerThreadKey, CallStacks> thread in _threads)
                {
                    if (thread.Key.ProcessIndex == _currentProcessIndex)
                    {
                        InvalidateThread(thread.Key, thread.Value, e.TimestampQpc);
                    }
                }

                ProcessBuildState processState = CurrentProcessState;
                processState.FirstMetadataQpc = long.MaxValue;
                processState.QpcFrequency = 0;
                processState.QpcSync = 0;
                processState.UtcSync = 0;
                processState.WrapperCount = 0;
            }
            else
            {
                AsyncProfilerThreadKey key = ThreadKeyOf(e.BufferHeader.Value.OsThreadId);
                if (_threads.TryGetValue(key, out CallStacks state))
                {
                    InvalidateThread(key, state, e.TimestampQpc);
                }
            }

            ParseError?.Invoke(_currentProcessIndex, e);
        }

        #endregion

        /// <summary>Identifies the V1 and V2 call-stack events that open a nested call stack.</summary>
        private static bool IsResumeCallstack(AsyncEventID id) =>
            id == AsyncEventID.ResumeRuntimeAsyncCallstack || id == AsyncEventID.ResumeStateMachineAsyncCallstack;

        /// <summary>Identifies the V1 event that extends the current call stack's frames.</summary>
        private static bool IsAppendCallstack(AsyncEventID id) =>
            id == AsyncEventID.AppendStateMachineAsyncCallstack;

        /// <summary>Identifies the event's process and decodes its buffer, or reports an invalid envelope.</summary>
        private void OnRawTraceEvent(AsyncProfilerTraceEvent data)
        {
            _currentProcessIndex = _processIndexOf(data);
            if (data.TryGetBuffer(ref _eventBuffer, out int length))
            {
                Process(_eventBuffer, length, _currentProcessIndex);
            }
            else
            {
                ((ISubEventSink)this).OnParseError(new ParseError("Invalid or truncated AsyncEvents envelope.", 0));
            }
        }

        /// <summary>Combines the current process instance with an OS thread id.</summary>
        private AsyncProfilerThreadKey ThreadKeyOf(ulong osThreadId) =>
            new AsyncProfilerThreadKey(_currentProcessIndex, osThreadId);

        /// <summary>The manifest and clock state for the buffer currently being decoded.</summary>
        private ProcessBuildState CurrentProcessState => GetOrCreateProcessState(_currentProcessIndex);

        /// <summary>Gets or creates the process's manifest and clock state.</summary>
        private ProcessBuildState GetOrCreateProcessState(ProcessIndex processIndex)
        {
            if (!_processStates.TryGetValue(processIndex, out ProcessBuildState processState))
            {
                processState = new ProcessBuildState();
                _processStates[processIndex] = processState;
            }

            return processState;
        }

        /// <summary>Gets or creates the thread's nesting state.</summary>
        private CallStacks GetOrCreate(AsyncProfilerThreadKey key)
        {
            if (!_threads.TryGetValue(key, out CallStacks state))
            {
                state = new CallStacks();
                _threads[key] = state;
            }

            return state;
        }

        /// <summary>Disarms a thread, recording only open call-stack prefixes with a usable end timestamp.</summary>
        private void InvalidateThread(AsyncProfilerThreadKey key, CallStacks state, long? endQpc)
        {
            state.Armed = false;
            while (state.Top != null)
            {
                CallStackBuilder builder = state.Pop();
                bool hasValidPrefix = endQpc.HasValue && endQpc.Value > builder.StartQpc;
                if (hasValidPrefix)
                {
                    Commit(key, builder, endQpc.Value);
                }
                else
                {
                    builder.Clear();
                    _builderPool.Push(builder);
                }
            }
        }

        /// <summary>Closes and records the current call stack when an armed thread suspends or completes it.</summary>
        private void CloseTop(AsyncProfilerThreadKey key, long timestampQpc)
        {
            CallStacks state = GetOrCreate(key);
            if (!state.Armed || state.Top == null)
            {
                return;
            }

            CallStackBuilder builder = state.Pop();
            Commit(key, builder, timestampQpc);
        }

        /// <summary>
        /// Closes and records every open call stack at the supplied timestamp.
        /// Used for thread resets and the end-of-stream flush.
        /// </summary>
        private void CommitOpenCallStacks(AsyncProfilerThreadKey key, CallStacks state, long endQpc)
        {
            while (state.Top != null)
            {
                CallStackBuilder builder = state.Pop();
                Commit(key, builder, endQpc);
            }
        }

        /// <summary>Records the call-stack interval and returns its builder to the pool.</summary>
        private void Commit(AsyncProfilerThreadKey key, CallStackBuilder builder, long endQpc)
        {
            AsyncProfilerCallStack.CompletionDelta[] methodCompletions = builder.GetMethodCompletions();
            AsyncProfilerCallStack.CompletionDelta[] exceptionCompletions = builder.GetExceptionCompletions();
            long[] wrapperResets = builder.GetWrapperResets();

            try
            {
                if (builder.HasInternedFrames)
                {
                    _index.Add(key, builder.FramesIndex,
                        builder.DispatcherId, builder.Depth, builder.ContinuationIndexBase,
                        builder.WrapperCount, builder.StartQpc, endQpc,
                        methodCompletions, exceptionCompletions, wrapperResets);
                }
                else
                {
                    _index.Add(key, builder.Kind, builder.GetMethodIds(), builder.GetFrameStates(),
                        builder.DispatcherId, builder.Depth, builder.ContinuationIndexBase,
                        builder.WrapperCount, builder.StartQpc, endQpc,
                        methodCompletions, exceptionCompletions, wrapperResets);
                }
            }
            finally
            {
                builder.Clear();
                _builderPool.Push(builder);
            }
        }

        /// <summary>Initializes a pooled or new builder, sharing indexed frames when possible.</summary>
        private CallStackBuilder RentBuilder(in AsyncCallstackEvent e, byte wrapperCount, bool reusablePayload)
        {
            CallStackBuilder builder = _builderPool.Count != 0
                ? _builderPool.Pop()
                : new CallStackBuilder();

            if (reusablePayload &&
                _index.TryGetInternedFrames(
                    _currentProcessIndex, e.Kind, e.MethodIds, e.FrameStates, e.FrameCount,
                    out AsyncProfilerCallStackFramesIndex framesIndex, out AsyncProfilerCallStackFrames frames))
            {
                builder.Initialize(e, wrapperCount, framesIndex, frames);
            }
            else
            {
                builder.Initialize(e, wrapperCount, copyFrames: reusablePayload);
            }

            return builder;
        }

        /// <summary>Tracks the open call stacks on one thread.</summary>
        private sealed class CallStacks
        {
            /// <summary>True after a valid reset, until a decode error invalidates the thread.</summary>
            public bool Armed;

            /// <summary>The current call stack, or null when the thread has none.</summary>
            public CallStackBuilder Top => _nesting.Count > 0 ? _nesting[_nesting.Count - 1] : null;

            /// <summary>Makes a call stack current and sets its nesting depth.</summary>
            public void Push(CallStackBuilder builder)
            {
                builder.Depth = _nesting.Count;
                _nesting.Add(builder);
            }

            /// <summary>Removes and returns the current call stack.</summary>
            public CallStackBuilder Pop()
            {
                CallStackBuilder top = _nesting[_nesting.Count - 1];
                _nesting.RemoveAt(_nesting.Count - 1);
                return top;
            }

            /// <summary>Records a dispatcher creation unless it repeats the previous creation record.</summary>
            public void RecordDispatcherCreation(
                AsyncProfilerCallStacksIndex index,
                AsyncProfilerThreadKey thread,
                ulong dispatcherId,
                ulong parentDispatcherId,
                long creationQpc)
            {
                // V2 can report one creation in both a context event and a call-stack event.
                bool isRepeatedCreation =
                    _lastCreatedDispatcherId == dispatcherId &&
                    _lastCreatedParentDispatcherId == parentDispatcherId &&
                    _lastCreationQpc == creationQpc;
                if (isRepeatedCreation)
                {
                    return;
                }

                index.AddDispatcherInfo(thread, dispatcherId, parentDispatcherId, creationQpc);
                _lastCreatedDispatcherId = dispatcherId;
                _lastCreatedParentDispatcherId = parentDispatcherId;
                _lastCreationQpc = creationQpc;
            }

            /// <summary>Clears the nesting list after its open call stacks have been handled.</summary>
            public void ClearNesting() => _nesting.Clear();

            #region private

            // Open call stacks in root-to-leaf order; the last entry is current.
            private readonly List<CallStackBuilder> _nesting = new List<CallStackBuilder>();
            private ulong _lastCreatedDispatcherId;
            private ulong _lastCreatedParentDispatcherId;
            private long _lastCreationQpc;

            #endregion
        }

        /// <summary>Holds the manifest, clock, and wrapper settings for one process instance.</summary>
        private sealed class ProcessBuildState
        {
            public readonly Manifest Manifest = new Manifest();
            // Ignore earlier resets. MaxValue means fresh metadata is still required.
            public long FirstMetadataQpc = long.MaxValue;
            public ulong QpcFrequency;
            public ulong QpcSync;
            public ulong UtcSync;
            public byte WrapperCount;
        }

        /// <summary>Assembles a call stack's frames and completion history until its interval is recorded.</summary>
        private sealed class CallStackBuilder
        {
            public ulong DispatcherId;
            public long StartQpc;
            public byte ContinuationIndexBase;
            public byte WrapperCount;
            public AsyncCallstackKind Kind;
            public int Depth;
            public AsyncProfilerCallStackFramesIndex FramesIndex;
            public AsyncProfilerCallStackFrames Frames;

            /// <summary>True while the builder shares an existing frame list from the index.</summary>
            public bool HasInternedFrames => Frames != null;

            public List<AsyncProfilerCallStack.CompletionDelta> MethodCompletions =>
                _methodCompletions ?? (_methodCompletions = new List<AsyncProfilerCallStack.CompletionDelta>());

            public List<AsyncProfilerCallStack.CompletionDelta> ExceptionCompletions =>
                _exceptionCompletions ?? (_exceptionCompletions = new List<AsyncProfilerCallStack.CompletionDelta>());

            public List<long> WrapperResets =>
                _wrapperResets ?? (_wrapperResets = new List<long>());

            /// <summary>Starts a call stack, copying borrowed decoder buffers when needed.</summary>
            public void Initialize(in AsyncCallstackEvent e, byte wrapperCount, bool copyFrames)
            {
                InitializeHeader(e, wrapperCount);
                if (!copyFrames)
                {
                    _methodIds = e.MethodIds ?? Array.Empty<ulong>();
                    _frameStates = e.FrameStates;
                    return;
                }

                _methodIds = new ulong[e.FrameCount];
                Array.Copy(e.MethodIds, _methodIds, e.FrameCount);
                if (e.FrameStates != null)
                {
                    _frameStates = new int[e.FrameCount];
                    Array.Copy(e.FrameStates, _frameStates, e.FrameCount);
                }
            }

            /// <summary>Starts a call stack with an existing shared frame list.</summary>
            public void Initialize(in AsyncCallstackEvent e, byte wrapperCount,
                AsyncProfilerCallStackFramesIndex framesIndex, AsyncProfilerCallStackFrames frames)
            {
                InitializeHeader(e, wrapperCount);
                FramesIndex = framesIndex;
                Frames = frames;
            }

            /// <summary>Appends frames without changing any frame list already stored in the index.</summary>
            public void AddFrames(in AsyncCallstackEvent e)
            {
                if (e.MethodIds == null || e.FrameCount == 0)
                {
                    return;
                }

                bool hasFrameStates = e.FrameStates != null;
                if (_methodIdsBuilder == null)
                {
                    // Copy the initial frames into append lists; indexed frame lists must stay unchanged.
                    int existingCount = HasInternedFrames ? Frames.FrameCount : _methodIds.Length;
                    _methodIdsBuilder = new List<ulong>(existingCount + e.FrameCount);
                    for (int i = 0; i < existingCount; i++)
                    {
                        _methodIdsBuilder.Add(HasInternedFrames ? Frames.InstructionPointerAt(i) : _methodIds[i]);
                    }

                    if (Kind == AsyncCallstackKind.StateMachineAsync)
                    {
                        // Keep the existing V1 states aligned with their instruction pointers.
                        _frameStatesBuilder = new List<int>(existingCount + e.FrameCount);
                        for (int i = 0; i < existingCount; i++)
                        {
                            _frameStatesBuilder.Add(HasInternedFrames ? Frames.FrameStateAt(i) : _frameStates[i]);
                        }
                    }

                    FramesIndex = AsyncProfilerCallStackFramesIndex.Invalid;
                    Frames = null;
                }
                else if (hasFrameStates && _frameStatesBuilder == null)
                {
                    // Fill state slots for earlier frames before appending frames that have states.
                    _frameStatesBuilder = new List<int>(_methodIdsBuilder.Count + e.MethodIds.Length);
                    for (int i = 0; i < _methodIdsBuilder.Count; i++)
                    {
                        _frameStatesBuilder.Add(0);
                    }
                }

                for (int i = 0; i < e.FrameCount; i++)
                {
                    _methodIdsBuilder.Add(e.MethodIds[i]);
                    if (_frameStatesBuilder != null)
                    {
                        _frameStatesBuilder.Add(hasFrameStates ? e.FrameStates[i] : 0);
                    }
                }
            }

            /// <summary>Returns the complete instruction-pointer array, including appended frames.</summary>
            public ulong[] GetMethodIds() => _methodIdsBuilder?.ToArray() ?? _methodIds;

            /// <summary>Returns the V1 states aligned with the instruction pointers, or null for V2.</summary>
            public int[] GetFrameStates() => _frameStatesBuilder?.ToArray() ?? _frameStates;

            /// <summary>Returns the recorded method completions, or an empty array.</summary>
            public AsyncProfilerCallStack.CompletionDelta[] GetMethodCompletions() =>
                _methodCompletions?.ToArray() ?? Array.Empty<AsyncProfilerCallStack.CompletionDelta>();

            /// <summary>Returns the recorded exception unwinds, or an empty array.</summary>
            public AsyncProfilerCallStack.CompletionDelta[] GetExceptionCompletions() =>
                _exceptionCompletions?.ToArray() ?? Array.Empty<AsyncProfilerCallStack.CompletionDelta>();

            /// <summary>Returns the wrapper-reset timestamps, or an empty array.</summary>
            public long[] GetWrapperResets() => _wrapperResets?.ToArray() ?? Array.Empty<long>();

            /// <summary>Drops frame references and empties the history lists before the builder is reused.</summary>
            public void Clear()
            {
                DispatcherId = 0;
                StartQpc = 0;
                ContinuationIndexBase = 0;
                WrapperCount = 0;
                Kind = default;
                Depth = 0;
                FramesIndex = AsyncProfilerCallStackFramesIndex.Invalid;
                Frames = null;
                _methodIds = null;
                _frameStates = null;
                _methodIdsBuilder = null;
                _frameStatesBuilder = null;
                _methodCompletions?.Clear();
                _exceptionCompletions?.Clear();
                _wrapperResets?.Clear();
            }

            #region private

            /// <summary>Copies call-stack identity and timing; the caller supplies the frame storage.</summary>
            private void InitializeHeader(in AsyncCallstackEvent e, byte wrapperCount)
            {
                DispatcherId = e.DispatcherId;
                StartQpc = e.TimestampQpc;
                ContinuationIndexBase = e.ContinuationIndex;
                WrapperCount = wrapperCount;
                Kind = e.Kind;
                FramesIndex = AsyncProfilerCallStackFramesIndex.Invalid;
                Frames = null;
            }

            private ulong[] _methodIds;
            private int[] _frameStates;
            private List<ulong> _methodIdsBuilder;
            private List<int> _frameStatesBuilder;
            private List<AsyncProfilerCallStack.CompletionDelta> _methodCompletions;
            private List<AsyncProfilerCallStack.CompletionDelta> _exceptionCompletions;
            private List<long> _wrapperResets;

            #endregion
        }

        private readonly AsyncProfilerTraceEventParser _parser;
        private readonly Func<AsyncProfilerTraceEvent, ProcessIndex> _processIndexOf;
        private readonly AsyncProfilerCallStacksIndex _index = new AsyncProfilerCallStacksIndex();
        private readonly Dictionary<AsyncProfilerThreadKey, CallStacks> _threads =
            new Dictionary<AsyncProfilerThreadKey, CallStacks>();
        private readonly Dictionary<ProcessIndex, ProcessBuildState> _processStates =
            new Dictionary<ProcessIndex, ProcessBuildState>();
        private readonly Stack<CallStackBuilder> _builderPool = new Stack<CallStackBuilder>();
        private readonly ulong[] _callstackMethodIds = new ulong[byte.MaxValue];
        private readonly int[] _callstackFrameStates = new int[byte.MaxValue];
        // Reused for each AsyncEvents payload; decoders copy what they keep.
        private byte[] _eventBuffer;
        private ProcessIndex _currentProcessIndex;

        #endregion
    }
}
