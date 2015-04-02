using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    class LockInspection
    {
        DesktopGCHeap m_heap;
        DesktopRuntimeBase m_runtime;
        ClrType m_rwType, m_rwsType;
        Dictionary<ulong, DesktopBlockingObject> m_monitors = new Dictionary<ulong, DesktopBlockingObject>();
        Dictionary<ulong, DesktopBlockingObject> m_locks = new Dictionary<ulong, DesktopBlockingObject>();
        Dictionary<ClrThread, DesktopBlockingObject> m_joinLocks = new Dictionary<ClrThread, DesktopBlockingObject>();
        Dictionary<ulong, DesktopBlockingObject> m_waitLocks = new Dictionary<ulong, DesktopBlockingObject>();
        Dictionary<ulong, ulong> m_syncblks = new Dictionary<ulong, ulong>();
        DesktopBlockingObject[] m_result = null;

        internal LockInspection(DesktopGCHeap heap, DesktopRuntimeBase runtime)
        {
            m_heap = heap;
            m_runtime = runtime;
        }

        internal DesktopBlockingObject[] InitLockInspection()
        {
            if (m_result != null)
                return m_result;

            // First, enumerate all thinlocks on the heap.
            foreach (var seg in m_heap.Segments)
            {
                for (ulong obj = seg.FirstObject; obj != 0; obj = seg.NextObject(obj))
                {
                    ClrType type = m_heap.GetObjectType(obj);
                    if (IsReaderWriterLock(obj, type))
                        m_locks[obj] = CreateRWLObject(obj, type);
                    else if (IsReaderWriterSlim(obj, type))
                        m_locks[obj] = CreateRWSObject(obj, type);

                    // Does this object have a syncblk with monitor associated with it?
                    uint header;
                    if (!m_heap.GetObjectHeader(obj, out header))
                        continue;

                    if ((header & (BIT_SBLK_IS_HASH_OR_SYNCBLKINDEX | BIT_SBLK_SPIN_LOCK)) != 0)
                        continue;

                    uint threadId = header & SBLK_MASK_LOCK_THREADID;
                    if (threadId == 0)
                        continue;

                    ClrThread thread = m_runtime.GetThreadFromThinlockID(threadId);
                    if (thread != null)
                    {
                        int recursion = ((int)header & SBLK_MASK_LOCK_RECLEVEL) >> SBLK_RECLEVEL_SHIFT;
                        m_monitors[obj] = new DesktopBlockingObject(obj, true, recursion + 1, thread, BlockingReason.Monitor);
                    }
                }
            }

            // Enumerate syncblocks to find locks
            int syncblkCnt = m_runtime.GetSyncblkCount();
            for (int i = 0; i < syncblkCnt; ++i)
            {
                ISyncBlkData data = m_runtime.GetSyncblkData(i);
                if (data == null || data.Free)
                    continue;

                m_syncblks[data.Address] = data.Object;
                m_syncblks[data.Object] = data.Object;
                ClrThread thread = null;
                if (data.MonitorHeld)
                {
                    ulong threadAddr = data.OwningThread;
                    foreach (var clrThread in m_runtime.Threads)
                    {
                        if (clrThread.Address == threadAddr)
                        {
                            thread = clrThread;
                            break;
                        }
                    }
                }

                m_monitors[data.Object] = new DesktopBlockingObject(data.Object, data.MonitorHeld, (int)data.Recursion, thread, BlockingReason.Monitor);
            }

            SetThreadWaiters();

            int total = m_monitors.Count + m_locks.Count + m_joinLocks.Count + m_waitLocks.Count;
            m_result = new DesktopBlockingObject[total];

            int j = 0;
            foreach (DesktopBlockingObject blocker in m_monitors.Values)
                m_result[j++] = blocker;

            foreach (DesktopBlockingObject blocker in m_locks.Values)
                m_result[j++] = blocker;

            foreach (DesktopBlockingObject blocker in m_joinLocks.Values)
                m_result[j++] = blocker;

            foreach (DesktopBlockingObject blocker in m_waitLocks.Values)
                m_result[j++] = blocker;

            Debug.Assert(j == m_result.Length);

            // Free up some memory.
            m_monitors = null;
            m_locks = null;
            m_joinLocks = null;
            m_waitLocks = null;
            m_syncblks = null;

            return m_result;
        }

        private bool IsReaderWriterLock(ulong obj, ClrType type)
        {
            if (type == null)
                return false;

            if (m_rwType == null)
            {
                if (type.Name != "System.Threading.ReaderWriterLock")
                    return false;
                
                m_rwType = type;
                return true;
            }

            return m_rwType == type;
        }
        private bool IsReaderWriterSlim(ulong obj, ClrType type)
        {
            if (type == null)
                return false;

            if (m_rwsType == null)
            {
                if (type.Name != "System.Threading.ReaderWriterLockSlim")
                    return false;

                m_rwsType = type;
                return true;
            }

            return m_rwsType == type;
        }

        private void SetThreadWaiters()
        {
            HashSet<string> eventTypes = null;
            List<BlockingObject> blobjs = new List<BlockingObject>();

            foreach (DesktopThread thread in m_runtime.Threads)
            {
                int max = thread.StackTrace.Count;
                if (max > 10)
                    max = 10;

                blobjs.Clear();
                for (int i = 0; i < max; ++i)
                {
                    DesktopBlockingObject blockingObj = null;
                    ClrMethod method = thread.StackTrace[i].Method;
                    if (method == null)
                        continue;

                    ClrType type = method.Type;
                    if (type == null)
                        continue;

                    switch (method.Name)
                    {
                        case "AcquireWriterLockInternal":
                        case "FCallUpgradeToWriterLock":
                        case "UpgradeToWriterLock":
                        case "AcquireReaderLockInternal":
                        case "AcquireReaderLock":
                            if (type.Name == "System.Threading.ReaderWriterLock")
                            {
                                blockingObj = FindLocks(thread.StackLimit, thread.StackTrace[i].StackPointer, IsReaderWriterLock);
                                if (blockingObj == null)
                                    blockingObj = FindLocks(thread.StackTrace[i].StackPointer, thread.StackBase, IsReaderWriterLock);

                                if (blockingObj != null && (blockingObj.Reason == BlockingReason.Unknown || blockingObj.Reason == BlockingReason.None))
                                {
                                    // This should have already been set correctly when the BlockingObject was created.  This is just a best-guess.
                                    if (method.Name == "AcquireReaderLockInternal" || method.Name == "AcquireReaderLock")
                                        blockingObj.Reason = BlockingReason.WriterAcquired;
                                    else
                                        blockingObj.Reason = BlockingReason.ReaderAcquired;
                                }
                            }
                            break;

                        case "TryEnterReadLockCore":
                        case "TryEnterReadLock":
                        case "TryEnterUpgradeableReadLock":
                        case "TryEnterUpgradeableReadLockCore":
                        case "TryEnterWriteLock":
                        case "TryEnterWriteLockCore":
                            if (type.Name == "System.Threading.ReaderWriterLockSlim")
                            {
                                blockingObj = FindLocks(thread.StackLimit, thread.StackTrace[i].StackPointer, IsReaderWriterSlim);
                                if (blockingObj == null)
                                    blockingObj = FindLocks(thread.StackTrace[i].StackPointer, thread.StackBase, IsReaderWriterSlim);

                                
                                if (blockingObj != null && (blockingObj.Reason == BlockingReason.Unknown || blockingObj.Reason == BlockingReason.None))
                                {
                                    // This should have already been set correctly when the BlockingObject was created.  This is just a best-guess.
                                    if (method.Name == "TryEnterWriteLock" || method.Name == "TryEnterWriteLockCore")
                                        blockingObj.Reason = BlockingReason.ReaderAcquired;
                                    else
                                        blockingObj.Reason = BlockingReason.WriterAcquired;
                                }
                            }

                            break;

                        case "JoinInternal":
                        case "Join":
                            if (type.Name == "System.Threading.Thread")
                            {
                                ulong threadAddr;
                                ClrThread target;

                                if (FindThread(thread.StackLimit, thread.StackTrace[i].StackPointer, out threadAddr, out target) ||
                                    FindThread(thread.StackTrace[i].StackPointer, thread.StackBase, out threadAddr, out target))
                                {
                                    if (!m_joinLocks.TryGetValue(target, out blockingObj))
                                        m_joinLocks[target] = blockingObj = new DesktopBlockingObject(threadAddr, true, 0, target, BlockingReason.ThreadJoin);
                                }
                            }
                            break;

                        case "Wait":
                        case "ObjWait":
                            if (type.Name == "System.Threading.Monitor")
                            {
                                blockingObj = FindMonitor(thread.StackLimit, thread.StackTrace[i].StackPointer);
                                if (blockingObj == null)
                                    blockingObj = FindMonitor(thread.StackTrace[i].StackPointer, thread.StackBase);

                                blockingObj.Reason = BlockingReason.MonitorWait;
                            }
                            break;

                        case "WaitAny":
                        case "WaitAll":
                            if (type.Name == "System.Threading.WaitHandle")
                            {
                                ulong obj = FindWaitObjects(thread.StackLimit, thread.StackTrace[i].StackPointer, "System.Threading.WaitHandle[]");
                                if (obj == 0)
                                    obj = FindWaitObjects(thread.StackTrace[i].StackPointer, thread.StackBase, "System.Threading.WaitHandle[]");

                                if (obj != 0)
                                {
                                    BlockingReason reason = method.Name == "WaitAny" ? BlockingReason.WaitAny : BlockingReason.WaitAll;
                                    if (!m_waitLocks.TryGetValue(obj, out blockingObj))
                                        m_waitLocks[obj] = blockingObj = new DesktopBlockingObject(obj, true, 0, null, reason);
                                }
                            }
                            break;

                        case "WaitOne":
                        case "InternalWaitOne":
                        case "WaitOneNative":
                            if (type.Name == "System.Threading.WaitHandle")
                            {
                                if (eventTypes == null)
                                {
                                    eventTypes = new HashSet<string>();
                                    eventTypes.Add("System.Threading.Mutex");
                                    eventTypes.Add("System.Threading.Semaphore");
                                    eventTypes.Add("System.Threading.ManualResetEvent");
                                    eventTypes.Add("System.Threading.AutoResetEvent");
                                    eventTypes.Add("System.Threading.WaitHandle");
                                    eventTypes.Add("Microsoft.Win32.SafeHandles.SafeWaitHandle");
                                }

                                ulong obj = FindWaitHandle(thread.StackLimit, thread.StackTrace[i].StackPointer, eventTypes);
                                if (obj == 0)
                                    obj = FindWaitHandle(thread.StackTrace[i].StackPointer, thread.StackBase, eventTypes);

                                if (obj != 0)
                                {
                                    if (m_waitLocks == null)
                                        m_waitLocks = new Dictionary<ulong, DesktopBlockingObject>();

                                    if (!m_waitLocks.TryGetValue(obj, out blockingObj))
                                        m_waitLocks[obj] = blockingObj = new DesktopBlockingObject(obj, true, 0, null, BlockingReason.WaitOne);
                                }
                            }
                            break;


                        case "TryEnter":
                        case "ReliableEnterTimeout":
                        case "TryEnterTimeout":
                        case "Enter":
                            if (type.Name == "System.Threading.Monitor")
                            {
                                blockingObj = FindMonitor(thread.StackLimit, thread.StackTrace[i].StackPointer);
                                if (blockingObj != null)
                                    blockingObj.Reason = BlockingReason.Monitor;
                            }
                            break;
                    }


                    if (blockingObj != null)
                    {
                        bool alreadyEncountered = false;
                        foreach (var blobj in blobjs)
                        {
                            if (blobj.Object == blockingObj.Object)
                            {
                                alreadyEncountered = true;
                                break;
                            }
                        }

                        if (!alreadyEncountered)
                            blobjs.Add(blockingObj);
                    }
                }

                foreach (DesktopBlockingObject blobj in blobjs)
                    blobj.AddWaiter(thread);
                thread.SetBlockingObjects(blobjs.ToArray());
            }
        }


        private DesktopBlockingObject CreateRWLObject(ulong obj, ClrType type)
        {
            if (type == null)
                return new DesktopBlockingObject(obj, false, 0, null, BlockingReason.None);

            ClrInstanceField writerID = type.GetFieldByName("_dwWriterID");
            if (writerID != null && writerID.ElementType == ClrElementType.Int32)
            {
                int id = (int)writerID.GetFieldValue(obj);
                if (id > 0)
                {
                    ClrThread thread = GetThreadById(id);
                    if (thread != null)
                        return new DesktopBlockingObject(obj, true, 0, thread, BlockingReason.ReaderAcquired);
                }
            }

            ClrInstanceField uLock = type.GetFieldByName("_dwULockID");
            ClrInstanceField lLock = type.GetFieldByName("_dwLLockID");

            if (uLock != null && uLock.ElementType == ClrElementType.Int32 && lLock != null && lLock.ElementType == ClrElementType.Int32)
            {
                int uId = (int)uLock.GetFieldValue(obj);
                int lId = (int)lLock.GetFieldValue(obj);


                List<ClrThread> threads = null;
                foreach (ClrThread thread in m_runtime.Threads)
                {
                    foreach (IRWLockData l in m_runtime.EnumerateLockData(thread.Address))
                    {
                        if (l.LLockID == lId && l.ULockID == uId && l.Level > 0)
                        {
                            if (threads == null)
                                threads = new List<ClrThread>();

                            threads.Add(thread);
                            break;
                        }
                    }
                }

                if (threads != null)
                    return new DesktopBlockingObject(obj, true, 0, BlockingReason.ReaderAcquired, threads.ToArray());
            }

            return new DesktopBlockingObject(obj, false, 0, null, BlockingReason.None);
        }

        private DesktopBlockingObject CreateRWSObject(ulong obj, ClrType type)
        {
            if (type == null)
                return new DesktopBlockingObject(obj, false, 0, null, BlockingReason.None);

            ClrInstanceField field = type.GetFieldByName("writeLockOwnerId");
            if (field != null && field.ElementType == ClrElementType.Int32)
            {
                int id = (int)field.GetFieldValue(obj);
                ClrThread thread = GetThreadById(id);
                if (thread != null)
                    return new DesktopBlockingObject(obj, true, 0, thread, BlockingReason.WriterAcquired);
            }

            field = type.GetFieldByName("upgradeLockOwnerId");
            if (field != null && field.ElementType == ClrElementType.Int32)
            {
                int id = (int)field.GetFieldValue(obj);
                ClrThread thread = GetThreadById(id);
                if (thread != null)
                    return new DesktopBlockingObject(obj, true, 0, thread, BlockingReason.WriterAcquired);
            }

            field = type.GetFieldByName("rwc");
            if (field != null)
            {
                List<ClrThread> threads = null;
                ulong rwc = (ulong)field.GetFieldValue(obj);
                ClrType rwcArrayType = m_heap.GetObjectType(rwc);
                if (rwcArrayType != null && rwcArrayType.IsArray && rwcArrayType.ArrayComponentType != null)
                {
                    ClrType rwcType = rwcArrayType.ArrayComponentType;
                    ClrInstanceField threadId = rwcType.GetFieldByName("threadid");
                    ClrInstanceField next = rwcType.GetFieldByName("next");
                    if (threadId != null && next != null)
                    {
                        int count = rwcArrayType.GetArrayLength(rwc);
                        for (int i = 0; i < count; ++i)
                        {
                            ulong entry = (ulong)rwcArrayType.GetArrayElementValue(rwc, i);
                            GetThreadEntry(ref threads, threadId, next, entry, false);
                        }
                    }
                }

                if (threads != null)
                    return new DesktopBlockingObject(obj, true, 0, BlockingReason.ReaderAcquired, threads.ToArray());
            }

            return new DesktopBlockingObject(obj, false, 0, null, BlockingReason.None);
        }

        private void GetThreadEntry(ref List<ClrThread> threads, ClrInstanceField threadId, ClrInstanceField next, ulong curr, bool interior)
        {
            if (curr == 0)
                return;

            int id = (int)threadId.GetFieldValue(curr, interior);
            ClrThread thread = GetThreadById(id);
            if (thread != null)
            {
                if (threads == null)
                    threads = new List<ClrThread>();
                threads.Add(thread);
            }

            curr = (ulong)next.GetFieldValue(curr, interior);
            if (curr != 0)
                GetThreadEntry(ref threads, threadId, next, curr, false);
        }

        private ulong FindWaitHandle(ulong start, ulong stop, HashSet<string> eventTypes)
        {
            ClrHeap heap = m_runtime.GetHeap();
            foreach (ulong obj in EnumerateObjectsOfTypes(start, stop, eventTypes))
                return obj;

            return 0;
        }

        private ulong FindWaitObjects(ulong start, ulong stop, string typeName)
        {
            ClrHeap heap = m_runtime.GetHeap();
            foreach (ulong obj in EnumerateObjectsOfType(start, stop, typeName))
                return obj;

            return 0;
        }

        IEnumerable<ulong> EnumerateObjectsOfTypes(ulong start, ulong stop, HashSet<string> types)
        {
            ClrHeap heap = m_runtime.GetHeap();
            foreach (ulong ptr in EnumeratePointersInRange(start, stop))
            {
                ulong obj;
                if (m_runtime.ReadPointer(ptr, out obj))
                {
                    if (heap.IsInHeap(obj))
                    {
                        ClrType type = heap.GetObjectType(obj);

                        int sanity = 0;
                        while (type != null)
                        {
                            if (types.Contains(type.Name))
                            {
                                yield return obj;
                                break;
                            }

                            type = type.BaseType;

                            if (sanity++ == 16)
                                break;
                        }
                    }
                }
            }
        }


        IEnumerable<ulong> EnumerateObjectsOfType(ulong start, ulong stop, string typeName)
        {
            ClrHeap heap = m_runtime.GetHeap();
            foreach (ulong ptr in EnumeratePointersInRange(start, stop))
            {
                ulong obj;
                if (m_runtime.ReadPointer(ptr, out obj))
                {
                    if (heap.IsInHeap(obj))
                    {
                        ClrType type = heap.GetObjectType(obj);


                        int sanity = 0;
                        while (type != null)
                        {
                            if (type.Name == typeName)
                            {
                                yield return obj;
                                break;
                            }

                            type = type.BaseType;

                            if (sanity++ == 16)
                                break;
                        }
                    }
                }
            }
        }

        private bool FindThread(ulong start, ulong stop, out ulong threadAddr, out ClrThread target)
        {
            ClrHeap heap = m_runtime.GetHeap();
            foreach (ulong obj in EnumerateObjectsOfType(start, stop, "System.Threading.Thread"))
            {
                ClrType type = heap.GetObjectType(obj);
                ClrInstanceField threadIdField = type.GetFieldByName("m_ManagedThreadId");
                if (threadIdField != null && threadIdField.ElementType == ClrElementType.Int32)
                {
                    int id = (int)threadIdField.GetFieldValue(obj);
                    ClrThread thread = GetThreadById(id);
                    if (thread != null)
                    {
                        threadAddr = obj;
                        target = thread;
                        return true;
                    }
                }
            }

            threadAddr = 0;
            target = null;
            return false;
        }

        IEnumerable<ulong> EnumeratePointersInRange(ulong start, ulong stop)
        {
            uint diff = (uint)m_runtime.PointerSize;

            if (start > stop)
                for (ulong ptr = stop; ptr <= start; ptr += diff)
                    yield return ptr;
            else
                for (ulong ptr = stop; ptr >= start; ptr -= diff)
                    yield return ptr;
        }



        private DesktopBlockingObject FindLocks(ulong start, ulong stop, Func<ulong, ClrType, bool> isCorrectType)
        {
            foreach (ulong ptr in EnumeratePointersInRange(start, stop))
            {
                ulong val = 0;
                if (m_runtime.ReadPointer(ptr, out val))
                {
                    DesktopBlockingObject result = null;
                    if (m_locks.TryGetValue(val, out result) && isCorrectType(val, m_heap.GetObjectType(val)))
                        return result;
                }
            }

            return null;
        }

        private DesktopBlockingObject FindMonitor(ulong start, ulong stop)
        {
            ulong obj = 0;
            foreach (ulong ptr in EnumeratePointersInRange(start, stop))
            {
                ulong tmp = 0;
                if (m_runtime.ReadPointer(ptr, out tmp))
                {
                    if (m_syncblks.TryGetValue(tmp, out tmp))
                    {
                        obj = tmp;
                        break;
                    }
                }
            }

            DesktopBlockingObject result = null;
            if (obj != 0 && m_monitors.TryGetValue(obj, out result))
                return result;

            return null;
        }

        ClrThread GetThreadById(int id)
        {
            if (id < 0)
                return null;

            foreach (ClrThread thread in m_runtime.Threads)
                if (thread.ManagedThreadId == id)
                    return thread;

            return null;
        }

        const int HASHCODE_BITS = 25;
        const int SYNCBLOCKINDEX_BITS = 26;
        const uint BIT_SBLK_IS_HASH_OR_SYNCBLKINDEX = 0x08000000;
        const uint BIT_SBLK_FINALIZER_RUN = 0x40000000;
        const uint BIT_SBLK_SPIN_LOCK = 0x10000000;
        const uint SBLK_MASK_LOCK_THREADID = 0x000003FF;   // special value of 0 + 1023 thread ids
        const int SBLK_MASK_LOCK_RECLEVEL = 0x0000FC00;   // 64 recursion levels
        const uint SBLK_APPDOMAIN_SHIFT = 16;           // shift right this much to get appdomain index
        const uint SBLK_MASK_APPDOMAININDEX = 0x000007FF;   // 2048 appdomain indices
        const int SBLK_RECLEVEL_SHIFT = 10;           // shift right this much to get recursion level
        const uint BIT_SBLK_IS_HASHCODE = 0x04000000;
        const uint MASK_HASHCODE = ((1 << HASHCODE_BITS) - 1);
        const uint MASK_SYNCBLOCKINDEX = ((1 << SYNCBLOCKINDEX_BITS) - 1);
    }
}
