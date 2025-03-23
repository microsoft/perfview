using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Microsoft.Diagnostics.Tracing.EventPipe
{

    internal class EventPipeThread
    {
        internal EventPipeThread(long threadId, long processId)
        {
            ThreadId = threadId;
            ProcessId = processId;
        }

        public long ThreadId { get; set; }
        public long ProcessId { get; set; }
        public string Name { get; set; }

        public Dictionary<string,string> Attributes { get; } = new Dictionary<string, string>();

        internal Queue<EventMarker> Events = new Queue<EventMarker>();
        internal int SequenceNumber;
        internal long LastCachedEventTimestamp;
        internal bool RemovalPending;
    }

    class ThreadCache
    {
        public ThreadCache(int fileFormatVersion, int processId)
        {
            _isFormatV6OrGreater = fileFormatVersion >= 6;
            _processId = processId;
        }

        // Prior to V6 threadIndex is the same as the threadID
        // In V6 onwards threadIndex is a unique index that is used to refer to a thread
        public EventPipeThread GetOrAddThread(long threadIndex, int sequenceNumber)
        {
            if (!_threads.TryGetValue(threadIndex, out EventPipeThread thread) || thread.RemovalPending)
            {
                // Prior to V6 new thread IDs could appear in any event or sequence point block
                // and we need to create threads on demand. After V6 all threads are explicitly
                // introduced in a AddThread block so we never expect to see an unknown index.
                if (_isFormatV6OrGreater)
                {
                    throw new FormatException($"Reference to unknown thread index {threadIndex}");
                }
                // Only V6 onwards has RemoveThread blocks that would send us into the alternate thread.RemvalPending path
                Debug.Assert(thread == null);

                // prior to V6, threadIndex == threadId
                long threadId = threadIndex;
                thread = new EventPipeThread(threadId, _processId);
                thread.SequenceNumber = sequenceNumber;
                AddThread(threadIndex, thread);
            }
            return thread;
        }

        public EventPipeThread GetThread(long threadIndex)
        {
            Debug.Assert(_isFormatV6OrGreater);
            if (!_threads.TryGetValue(threadIndex, out EventPipeThread thread) || thread.RemovalPending)
            {
                // Prior to V6 new thread IDs could appear in any event or sequence point block
                // and we need to create threads on demand. After V6 all threads are explicitly
                // introduced in a AddThread block so we never expect to see an unknown index.
                throw new FormatException($"Reference to unknown thread index {threadIndex}");
            }
            return thread;
        }

        public bool TryGetValue(long threadIndex, out EventPipeThread thread)
        {
            _threads.TryGetValue(threadIndex, out thread);
            if(thread != null && thread.RemovalPending)
            {
                thread = null;
            }
            return thread != null;
        }
        public IEnumerable<EventPipeThread> Values => _threads.Values;
        public bool ContainsKey(long threadIndex) => _threads.ContainsKey(threadIndex);

        public void AddThread(long threadIndex, EventPipeThread thread)
        {
            // Prior to V6 of the NetTrace format there was no explicit thread removal mechanism
            // To ensure we don't have unbounded growth we evict old threads to make room
            // for new ones. Evicted threads can always be re-added later if they log again
            // but there are two consequences:
            // a) We won't detect lost events on that thread after eviction
            // b) If the thread still had events pending dispatch they will be lost
            // We pick the thread that has gone the longest since it last logged an event
            // under the presumption that it is probably dead, has no events, and won't
            // log again.
            if (!_isFormatV6OrGreater && _threads.Count >= 5000)
            {
                long oldestThreadCaptureId = -1;
                long smallestTimestamp = long.MaxValue;
                foreach (var kv in _threads)
                {
                    if (kv.Value.LastCachedEventTimestamp < smallestTimestamp)
                    {
                        smallestTimestamp = kv.Value.LastCachedEventTimestamp;
                        oldestThreadCaptureId = kv.Key;
                    }
                }
                Debug.Assert(oldestThreadCaptureId != -1);
                _threads.Remove(oldestThreadCaptureId);
            }
            _threads[threadIndex] = thread;
        }

        public void RemoveThread(long threadIndex) => _threads.Remove(threadIndex);

        public void Flush()
        {

#if DEBUG
            // we should only be flushing after all events have been processed
            foreach (var thread in _threads.Values)
            {
                Debug.Assert(thread.Events.Count == 0);
            }
#endif
            _threads.Clear();
        }

        bool _isFormatV6OrGreater;
        int _processId;
        Dictionary<long, EventPipeThread> _threads = new Dictionary<long, EventPipeThread>();
    }
}
