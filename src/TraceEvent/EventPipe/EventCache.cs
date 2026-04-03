using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Tracing.EventPipe
{
    unsafe delegate void ParseBufferItemFunction(ref EventPipeEventHeader header);

    internal class EventCache
    {
        public EventCache(EventPipeEventSource source, ThreadCache threads)
        {
            _source = source;
            _threads = threads;
        }

        public event ParseBufferItemFunction OnEvent;
        public event Action<int> OnEventsDropped;

        public void ProcessEventBlock(Block block)
        {
            SpanReader reader = block.Reader;

            // parse the header
            ushort headerSize = reader.ReadUInt16();
            if(headerSize < 20)
            {
                throw new FormatException("Invalid EventBlock header size");
            }
            ushort flags = reader.ReadUInt16();
            bool useHeaderCompression = (flags & (ushort)EventBlockFlags.HeaderCompression) != 0;

            // skip the rest of the header
            reader.ReadBytes(headerSize - 4);

            // parse the events
            EventPipeEventHeader eventHeader = default;
            long timestamp = 0;
            long maxTimestamp = 0;
            long lastFlushTimestamp = 0;
            SpanReader tempReader = reader;
            _source.ReadEventHeader(ref tempReader, useHeaderCompression, ref eventHeader);
            EventPipeThread thread = _threads.GetOrAddThread(eventHeader.CaptureThreadIndexOrId, eventHeader.SequenceNumber - 1);
            EventMarker eventMarker = new EventMarker();
            while (reader.RemainingBytes.Length > 0)
            {
                _source.ReadEventHeader(ref reader, useHeaderCompression, ref eventMarker.Header);
                bool isSortedEvent = eventMarker.Header.IsSorted;
                thread.LastCachedEventTimestamp = timestamp = eventMarker.Header.TimeStamp;
                maxTimestamp = Math.Max(maxTimestamp, timestamp);
                int sequenceNumber = eventMarker.Header.SequenceNumber;
                if (isSortedEvent)
                {
                    // sorted events are the only time the captureThreadId should change
                    long captureThreadId = eventMarker.Header.CaptureThreadIndexOrId;
                    thread = _threads.GetOrAddThread(captureThreadId, sequenceNumber - 1);
                }
                NotifyDroppedEventsIfNeeded(thread.SequenceNumber, sequenceNumber - 1);
                thread.SequenceNumber = sequenceNumber;

                if(isSortedEvent)
                {
                    lastFlushTimestamp = timestamp;
                    SortAndDispatch(timestamp);
                    OnEvent?.Invoke(ref eventMarker.Header);
                }
                else
                {
                    if (thread.Events.Count == 0)
                    {
                        _activeThreadQueues.Add(thread.Events);
                    }
                    thread.Events.Enqueue(eventMarker);
                }

                reader.ReadBytes(eventMarker.Header.PayloadSize);
                EventMarker lastEvent = eventMarker;
                eventMarker = new EventMarker();
                eventMarker.Header = lastEvent.Header;
            }

            // We need to keep the buffer around until all events are processed
            EventBlockBuffer buffer = new EventBlockBuffer(block.TakeOwnership(), maxTimestamp);
            _buffers.Enqueue(buffer);

            // get rid of old buffers if all their events are older than an event we already flushed
            FreeOldEventBuffers(lastFlushTimestamp);
        }

        public void ProcessSequencePointBlockV5OrLess(Block block)
        {
            SpanReader reader = block.Reader;
            long timestamp = (long)reader.ReadUInt64();
            int threadCount = (int)reader.ReadUInt32();
            Flush();
            TrimEventsAfterSequencePoint();
            for (int i = 0; i < threadCount; i++)
            {
                long captureThreadId = (long)reader.ReadUInt64();
                int sequenceNumber = (int)reader.ReadUInt32();
                CheckpointThread(captureThreadId, sequenceNumber);
            }
        }

        enum SequencePointFlags : uint
        {
            FlushThreads = 1,
            FlushMetadata = 2
        }

        public void ProcessSequencePointBlockV6OrGreater(Block block)
        {
            SpanReader reader = block.Reader;
            long timestamp = (long)reader.ReadUInt64();
            uint flags = reader.ReadUInt32();
            int threadCount = (int)reader.ReadUInt32();
            Flush();
            TrimEventsAfterSequencePoint();
            for (int i = 0; i < threadCount; i++)
            {
                long captureThreadIndex = (long)reader.ReadVarUInt64();
                int sequenceNumber = (int)reader.ReadVarUInt32();
                CheckpointThread(captureThreadIndex, sequenceNumber);
            }

            if((flags & (uint)SequencePointFlags.FlushThreads) != 0)
            {
                _threads.Flush();
            }

            if((flags & (uint)SequencePointFlags.FlushMetadata) != 0)
            {
                _source.FlushMetadataCache();
            }
        }

        /// <summary>
        /// Flush all remaining events, free all buffers, and remove any threads that are pending removal.
        /// </summary>
        public void Flush()
        {
            SortAndDispatch(long.MaxValue);
            FreeOldEventBuffers(long.MaxValue);
            CheckForPendingThreadRemoval();
        }

        private void TrimEventsAfterSequencePoint()
        {
            foreach (EventPipeThread thread in _threads.Values)
            {
                Debug.Assert(thread.Events.Count == 0, "There shouldn't be any pending events after a sequence point");
                thread.Events.Clear();
                thread.Events.TrimExcess();
            }
        }

        private void CheckForPendingThreadRemoval()
        {
            foreach (var thread in _threads.Values)
            {
                if (thread.RemovalPending && thread.Events.Count == 0)
                {
                    _threads.RemoveThread(thread.ThreadId);
                }
            }
        }

        private unsafe void SortAndDispatch(long stopTimestamp)
        {
            // Build a min-heap from active thread queues (those with pending events) whose
            // front event is before stopTimestamp. Using _activeThreadQueues avoids iterating
            // all threads in the dictionary — only threads that have had events enqueued are checked.
            // This gives O(N * log(T)) merge performance where N is the number of events and
            // T is the number of active threads.
            _heap.Clear();
            foreach (Queue<EventMarker> q in _activeThreadQueues)
            {
                if (q.Count > 0)
                {
                    long ts = q.Peek().Header.TimeStamp;
                    if (ts < stopTimestamp)
                    {
                        _heap.Add(ts, q);
                    }
                }
            }

            if (_heap.Count == 0)
            {
                return;
            }

            _heap.Build();

            // Merge events in timestamp order using the min-heap.
            while (_heap.Count > 0)
            {
                Queue<EventMarker> minQueue = _heap.PeekValue;
                EventMarker eventMarker = minQueue.Dequeue();
                OnEvent?.Invoke(ref eventMarker.Header);

                if (minQueue.Count > 0)
                {
                    long nextTs = minQueue.Peek().Header.TimeStamp;
                    if (nextTs < stopTimestamp)
                    {
                        // Update the root with the next timestamp and restore the heap property.
                        _heap.ReplaceRoot(nextTs, minQueue);
                    }
                    else
                    {
                        _heap.RemoveRoot();
                    }
                }
                else
                {
                    _heap.RemoveRoot();
                    // Remove from active set and free internal storage to prevent unbounded
                    // memory growth when the application creates and destroys threads.
                    _activeThreadQueues.Remove(minQueue);
                    minQueue.TrimExcess();
                }
            }
        }

        #region Min-heap Implementation

        /// <summary>
        /// A min-heap that pairs a long key with a value of type <typeparamref name="TValue"/>.
        /// Entries are ordered by key so the minimum key is always at the root.
        /// </summary>
        internal class MinHeap<TValue>
        {
            private struct Entry
            {
                public long Key;
                public TValue Value;

                public Entry(long key, TValue value)
                {
                    Key = key;
                    Value = value;
                }
            }

            private readonly List<Entry> _entries = new List<Entry>();

            public int Count => _entries.Count;

            public TValue PeekValue => _entries[0].Value;

            public void Clear() => _entries.Clear();

            public void Add(long key, TValue value)
            {
                _entries.Add(new Entry(key, value));
            }

            /// <summary>
            /// Establishes the heap property over all entries. Call once after adding all
            /// entries via Add, before extracting from the heap.
            /// </summary>
            /// <remarks>
            /// Starts from the last non-leaf node (_entries.Count / 2 - 1) and sifts each
            /// node down to its correct position. Leaves (the second half of the array) are
            /// already trivially valid heaps of size 1, so they are skipped.
            /// </remarks>
            public void Build()
            {
                // Start from the last non-leaf node and work backwards to the root.
                // Nodes at indices [Count/2 .. Count-1] are leaves that need no adjustment.
                for (int i = _entries.Count / 2 - 1; i >= 0; i--)
                {
                    SiftDown(i);
                }
            }

            /// <summary>
            /// Replaces the root entry with a new key/value pair and restores the heap property.
            /// Use when the root element has been consumed but its source still has more items.
            /// </summary>
            public void ReplaceRoot(long newKey, TValue value)
            {
                _entries[0] = new Entry(newKey, value);
                SiftDown(0);
            }

            /// <summary>
            /// Removes the root (minimum) entry from the heap and restores the heap property.
            /// </summary>
            public void RemoveRoot()
            {
                int lastIndex = _entries.Count - 1;
                if (lastIndex == 0)
                {
                    _entries.Clear();
                }
                else
                {
                    _entries[0] = _entries[lastIndex];
                    _entries.RemoveAt(lastIndex);
                    SiftDown(0);
                }
            }

            /// <summary>
            /// Restores the min-heap property by moving the element at index i down the tree
            /// until it is smaller than both children or reaches a leaf position.
            /// </summary>
            private void SiftDown(int i)
            {
                int count = _entries.Count;
                while (true)
                {
                    int smallest = i;

                    // In a binary heap stored as an array, the children of node i are at
                    // indices 2i+1 (left) and 2i+2 (right).
                    int left = 2 * i + 1;
                    int right = 2 * i + 2;

                    if (left < count && _entries[left].Key < _entries[smallest].Key)
                    {
                        smallest = left;
                    }
                    if (right < count && _entries[right].Key < _entries[smallest].Key)
                    {
                        smallest = right;
                    }
                    if (smallest == i)
                    {
                        break;
                    }
                    (_entries[i], _entries[smallest]) = (_entries[smallest], _entries[i]);
                    i = smallest;
                }
            }
        }

        #endregion

        private void FreeOldEventBuffers(long stopTimestamp)
        {
            while (_buffers.Count > 0)
            {
                EventBlockBuffer blockBuffer = _buffers.Peek();
                if (blockBuffer.MaxEventTimestamp < stopTimestamp)
                {
                    _buffers.Dequeue();
                    ((IDisposable)blockBuffer.Buffer).Dispose();
                }
                else
                {
                    break;
                }
            }
        }

        public void CheckpointThreadAndPendRemoval(long threadIndex, int sequenceNumber)
        {
            EventPipeThread thread = _threads.GetThread(threadIndex);
            CheckpointThread(thread, sequenceNumber);
            thread.RemovalPending = true;
        }

        public void CheckpointThread(long threadIndex, int sequenceNumber)
        {
            EventPipeThread thread = _threads.GetOrAddThread(threadIndex, sequenceNumber);
            CheckpointThread(thread, sequenceNumber);
        }

        private void CheckpointThread(EventPipeThread thread, int sequenceNumber)
        {
            NotifyDroppedEventsIfNeeded(thread.SequenceNumber, sequenceNumber);
            thread.SequenceNumber = sequenceNumber;
        }

        private void NotifyDroppedEventsIfNeeded(int sequenceNumber, int expectedSequenceNumber)
        {
            // Either events were dropped or the sequence number was reset because the thread ID was recycled.
            // V6 format never recycles thread indexes but prior formats do. We assume heuristically that if an event or sequence
            // point implies the last sequenceNumber was zero then the thread was recycled.
            if (_source.FileFormatVersionNumber >= 6 || sequenceNumber != 0)
            {
                int droppedEvents = unchecked(expectedSequenceNumber - sequenceNumber);
                if (droppedEvents < 0)
                {
                    droppedEvents = int.MaxValue;
                }
                if (droppedEvents > 0)
                {
                    OnEventsDropped?.Invoke(droppedEvents);
                }
            }
        }

        struct EventBlockBuffer
        {
            public EventBlockBuffer(FixedBuffer buffer, long maxTimestamp)
            {
                Buffer = buffer;
                MaxEventTimestamp = maxTimestamp;
            }
            public FixedBuffer Buffer;
            public long MaxEventTimestamp;
        }

        EventPipeEventSource _source;
        ThreadCache _threads;
        Queue<EventBlockBuffer> _buffers = new Queue<EventBlockBuffer>();
        MinHeap<Queue<EventMarker>> _heap = new MinHeap<Queue<EventMarker>>();
        HashSet<Queue<EventMarker>> _activeThreadQueues = new HashSet<Queue<EventMarker>>();
    }

    internal class EventMarker
    {
        public EventPipeEventHeader Header;
    }

    internal class PinnedBuffer
    {
        public PinnedBuffer(byte[] data)
        {
            Data = data;
            PinningHandle = GCHandle.Alloc(data, GCHandleType.Pinned);
        }

        ~PinnedBuffer()
        {
            PinningHandle.Free();
        }

        public byte[] Data { get; private set; }
        public GCHandle PinningHandle { get; private set; }
    }
}
