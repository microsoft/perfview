using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

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

        public void ProcessEventBlock(byte[] eventBlockData, long streamOffset)
        {
            PinnedBuffer buffer = new PinnedBuffer(eventBlockData);
            SpanReader reader = new SpanReader(eventBlockData, streamOffset);

            // parse the header
            if (eventBlockData.Length < 20)
            {
                throw new FormatException("Expected EventBlock of at least 20 bytes");
            }
            ushort headerSize = reader.ReadUInt16();
            if(headerSize < 20 || headerSize > eventBlockData.Length)
            {
                throw new FormatException("Invalid EventBlock header size");
            }
            ushort flags = reader.ReadUInt16();
            bool useHeaderCompression = (flags & (ushort)EventBlockFlags.HeaderCompression) != 0;

            // skip the rest of the header
            reader.ReadBytes(headerSize - 4);

            // parse the events
            EventMarker eventMarker = new EventMarker(buffer);
            long timestamp = 0;
            SpanReader tempReader = reader;
            _source.ReadEventHeader(ref tempReader, useHeaderCompression, ref eventMarker.Header);
            EventPipeThread thread = _threads.GetOrAddThread(eventMarker.Header.CaptureThreadIndexOrId, eventMarker.Header.SequenceNumber - 1);
            eventMarker = new EventMarker(buffer);
            while (reader.RemainingBytes.Length > 0)
            {
                _source.ReadEventHeader(ref reader, useHeaderCompression, ref eventMarker.Header);
                bool isSortedEvent = eventMarker.Header.IsSorted;
                timestamp = eventMarker.Header.TimeStamp;
                int sequenceNumber = eventMarker.Header.SequenceNumber;
                if (isSortedEvent)
                {
                    thread.LastCachedEventTimestamp = timestamp;

                    // sorted events are the only time the captureThreadId should change
                    long captureThreadId = eventMarker.Header.CaptureThreadIndexOrId;
                    thread = _threads.GetOrAddThread(captureThreadId, sequenceNumber - 1);
                }
                NotifyDroppedEventsIfNeeded(thread.SequenceNumber, sequenceNumber - 1);
                thread.SequenceNumber = sequenceNumber;

                if(isSortedEvent)
                {
                    SortAndDispatch(timestamp);
                    OnEvent?.Invoke(ref eventMarker.Header);
                }
                else
                {
                    thread.Events.Enqueue(eventMarker);

                }

                reader.ReadBytes(eventMarker.Header.PayloadSize);
                EventMarker lastEvent = eventMarker;
                eventMarker = new EventMarker(buffer);
                eventMarker.Header = lastEvent.Header;
            }
            thread.LastCachedEventTimestamp = timestamp;
        }

        public void ProcessSequencePointBlockV5OrLess(byte[] sequencePointBytes, long streamOffset)
        {
            SpanReader reader = new SpanReader(sequencePointBytes, streamOffset);
            long timestamp = (long)reader.ReadUInt64();
            int threadCount = (int)reader.ReadUInt32();
            Flush(timestamp);
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
            FlushThreads = 1
        }

        public void ProcessSequencePointBlockV6OrGreater(byte[] sequencePointBytes, long streamOffset)
        {
            SpanReader reader = new SpanReader(sequencePointBytes, streamOffset);
            long timestamp = (long)reader.ReadUInt64();
            uint flags = reader.ReadUInt32();
            int threadCount = (int)reader.ReadUInt32();
            Flush(timestamp);
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
        }

        /// <summary>
        /// After all events have been parsed we could have some straglers that weren't
        /// earlier than any sorted event. Sort and dispatch those now.
        /// </summary>
        public void Flush() => Flush(long.MaxValue);

        private void Flush(long timestamp)
        {
            SortAndDispatch(timestamp);
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
            // This sort could be made faster by using a min-heap but this is a simple place to start
            List<Queue<EventMarker>> threadQueues = new List<Queue<EventMarker>>(_threads.Values.Select(t => t.Events));
            while(true)
            {
                long lowestTimestamp = stopTimestamp;
                Queue<EventMarker> oldestEventQueue = null;
                foreach(Queue<EventMarker> threadQueue in threadQueues)
                {
                    if(threadQueue.Count == 0)
                    {
                        continue;
                    }
                    long eventTimestamp = threadQueue.Peek().Header.TimeStamp;
                    if (eventTimestamp < lowestTimestamp)
                    {
                        oldestEventQueue = threadQueue;
                        lowestTimestamp = eventTimestamp;
                    }
                }
                if(oldestEventQueue == null)
                {
                    break;
                }
                else
                {
                    EventMarker eventMarker = oldestEventQueue.Dequeue();
                    OnEvent?.Invoke(ref eventMarker.Header);
                    GC.KeepAlive(eventMarker);
                }
            }

            // If the app creates and destroys threads over time we need to flush old threads
            // from the cache or memory usage will grow unbounded. AddThread handles the
            // the thread objects but the storage for the queue elements also does not shrink
            // below the high water mark unless we free it explicitly.
            foreach (Queue<EventMarker> q in threadQueues)
            {
                if(q.Count == 0)
                {
                    q.TrimExcess();
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

        EventPipeEventSource _source;
        ThreadCache _threads;
    }

    internal class EventMarker
    {
        public EventMarker(PinnedBuffer buffer)
        {
            Buffer = buffer;
        }
        public EventPipeEventHeader Header;
        public PinnedBuffer Buffer;
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
