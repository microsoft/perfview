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
            // Build a min-heap from non-empty thread queues whose front event is before stopTimestamp.
            // This gives O(N * log(T)) merge performance instead of O(N * T) where N is the number
            // of events and T is the number of threads.
            _heap.Clear();
            foreach (EventPipeThread thread in _threads.Values)
            {
                Queue<EventMarker> q = thread.Events;
                if (q.Count > 0)
                {
                    long ts = q.Peek().Header.TimeStamp;
                    if (ts < stopTimestamp)
                    {
                        _heap.Add(new HeapEntry(ts, q));
                    }
                }
            }

            if (_heap.Count == 0)
            {
                return;
            }

            HeapBuild();

            // Merge events in timestamp order using the min-heap.
            while (_heap.Count > 0)
            {
                HeapEntry min = _heap[0];
                EventMarker eventMarker = min.Queue.Dequeue();
                OnEvent?.Invoke(ref eventMarker.Header);

                if (min.Queue.Count > 0)
                {
                    long nextTs = min.Queue.Peek().Header.TimeStamp;
                    if (nextTs < stopTimestamp)
                    {
                        // Update the root with the next timestamp and restore the heap property.
                        _heap[0] = new HeapEntry(nextTs, min.Queue);
                        HeapSiftDown(0);
                    }
                    else
                    {
                        HeapRemoveRoot();
                    }
                }
                else
                {
                    HeapRemoveRoot();
                    // Free internal storage for empty queues to prevent unbounded memory growth
                    // when the application creates and destroys threads over time.
                    min.Queue.TrimExcess();
                }
            }
        }

        #region Min-heap helpers for SortAndDispatch

        private struct HeapEntry
        {
            public long Timestamp;
            public Queue<EventMarker> Queue;

            public HeapEntry(long timestamp, Queue<EventMarker> queue)
            {
                Timestamp = timestamp;
                Queue = queue;
            }
        }

        private void HeapBuild()
        {
            for (int i = _heap.Count / 2 - 1; i >= 0; i--)
            {
                HeapSiftDown(i);
            }
        }

        private void HeapSiftDown(int i)
        {
            int count = _heap.Count;
            while (true)
            {
                int smallest = i;
                int left = 2 * i + 1;
                int right = 2 * i + 2;
                if (left < count && _heap[left].Timestamp < _heap[smallest].Timestamp)
                {
                    smallest = left;
                }
                if (right < count && _heap[right].Timestamp < _heap[smallest].Timestamp)
                {
                    smallest = right;
                }
                if (smallest == i)
                {
                    break;
                }
                HeapEntry temp = _heap[i];
                _heap[i] = _heap[smallest];
                _heap[smallest] = temp;
                i = smallest;
            }
        }

        private void HeapRemoveRoot()
        {
            int lastIndex = _heap.Count - 1;
            if (lastIndex == 0)
            {
                _heap.Clear();
            }
            else
            {
                _heap[0] = _heap[lastIndex];
                _heap.RemoveAt(lastIndex);
                HeapSiftDown(0);
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
        List<HeapEntry> _heap = new List<HeapEntry>();
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
