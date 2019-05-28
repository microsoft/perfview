using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Tracing.EventPipe
{
    unsafe delegate void ParseBufferItemFunction(byte* bufferPtr);

    internal class EventCache
    {
        public event ParseBufferItemFunction OnEvent;
        public event Action<int> OnEventsDropped;

        public unsafe void ProcessEventBlock(byte[] eventBlockData)
        {
            PinnedBuffer buffer = new PinnedBuffer(eventBlockData);
            byte* cursor = (byte*) buffer.PinningHandle.AddrOfPinnedObject();
            byte* end = cursor + eventBlockData.Length;
            long captureThreadId = EventPipeEventHeader.GetCaptureThreadId(cursor);
            long sequenceNumber = EventPipeEventHeader.GetSequenceNumber(cursor);
            long timestamp = 0;
            if (!_threads.TryGetValue(captureThreadId, out EventCacheThread thread))
            {
                thread = new EventCacheThread();
                thread.SequenceNumber = sequenceNumber - 1;
                AddThread(captureThreadId, thread);
            }
            while (cursor < end)
            {
                int totalSize = EventPipeEventHeader.GetTotalEventSize(cursor, 4);
                bool isSortedEvent = EventPipeEventHeader.GetIsSortedEvent(cursor);
                timestamp = EventPipeEventHeader.GetTimestamp(cursor, 4);
                sequenceNumber = EventPipeEventHeader.GetSequenceNumber(cursor);
                if (isSortedEvent)
                {
                    thread.LastCachedEventTimestamp = timestamp;

                    // sorted events are the only time the captureThreadId should change
                    captureThreadId = EventPipeEventHeader.GetCaptureThreadId(cursor);
                    if (!_threads.TryGetValue(captureThreadId, out thread))
                    {
                        thread = new EventCacheThread();
                        thread.SequenceNumber = sequenceNumber - 1;
                        AddThread(captureThreadId, thread);
                    }
                }

                int droppedEvents = (int)Math.Min(int.MaxValue, sequenceNumber - thread.SequenceNumber - 1);
                if(droppedEvents > 0)
                {
                    OnEventsDropped?.Invoke(droppedEvents);
                }
                else
                {
                    // When a thread id is recycled the sequenceNumber can abruptly reset to 1 which
                    // makes droppedEvents go negative
                    Debug.Assert(droppedEvents == 0 || sequenceNumber == 1);
                }
                thread.SequenceNumber = sequenceNumber;

                if(isSortedEvent)
                {
                    SortAndDispatch(timestamp);
                    OnEvent?.Invoke(cursor);
                }
                else
                {
                    thread.Events.Enqueue(new EventMarker(cursor, buffer, timestamp));
                }

                cursor += totalSize;
            }
            thread.LastCachedEventTimestamp = timestamp;
        }

        /// <summary>
        /// After all events have been parsed we could have some straglers that weren't
        /// earlier than any sorted event. Sort and dispatch those now.
        /// </summary>
        public void Flush()
        {
            SortAndDispatch(long.MaxValue);
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
                    long eventTimestamp = threadQueue.Peek().Timestamp;
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
                    OnEvent?.Invoke(eventMarker.BufferPosition);
                }
            }

            // If the app creates and destroys threads over time we need to flush old threads
            // from the cache or memory usage will grow unbounded. AddThread handles the
            // the thread objects but the storage for the queue elements also does not shrink
            // below the high water mark unless we free it explicitly. Although not unbounded
            // growth our current runtime policy reads ahead up to 10,000 events ahead per-thread
            // * 5000 threads * 24 bytes = ~1GB. It would be nice not to leave that much memory
            // laying around probably mostly unused.
            foreach(Queue<EventMarker> q in threadQueues)
            {
                if(q.Count == 0)
                {
                    q.TrimExcess();
                }
            }
        }

        private void AddThread(long captureThreadId, EventCacheThread thread)
        {
            // To ensure we don't have unbounded growth we evict old threads to make room
            // for new ones. Evicted threads can always be re-added later if they log again
            // but there are two consequences:
            // a) We won't detect lost events on that thread after eviction
            // b) If the thread still had events pending dispatch they will be lost
            // We pick the thread that has gone the longest since it last logged an event
            // under the presumption that it is probably dead, has no events, and won't
            // log again.
            //
            // In the future if we had explicit thread death notification events we could keep
            // this cache leaner.
            if(_threads.Count >= 5000)
            {
                long oldestThreadCaptureId = -1;
                long smallestTimestamp = long.MaxValue;
                foreach(var kv in _threads)
                {
                    if(kv.Value.LastCachedEventTimestamp < smallestTimestamp)
                    {
                        smallestTimestamp = kv.Value.LastCachedEventTimestamp;
                        oldestThreadCaptureId = kv.Key;
                    }
                }
                Debug.Assert(oldestThreadCaptureId != -1);
                _threads.Remove(oldestThreadCaptureId);
            }
            _threads[captureThreadId] = thread;
        }

        Dictionary<long, EventCacheThread> _threads = new Dictionary<long, EventCacheThread>();
    }

    internal class EventCacheThread
    {
        public Queue<EventMarker> Events = new Queue<EventMarker>();
        public long SequenceNumber;
        public long LastCachedEventTimestamp;
    }

    internal unsafe struct EventMarker
    {
        public EventMarker(byte* bufferPosition, PinnedBuffer buffer, long timestamp)
        {
            BufferPosition = bufferPosition;
            Timestamp = timestamp;
            Buffer = buffer;
        }
        public byte* BufferPosition;
        public PinnedBuffer Buffer;
        public long Timestamp;
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
