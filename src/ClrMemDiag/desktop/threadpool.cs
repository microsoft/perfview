using System;
using System.Collections.Generic;
using Address = System.UInt64;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    class DesktopThreadPool : ClrThreadPool
    {
        private DesktopRuntimeBase m_runtime;
        ClrHeap m_heap;
        private int m_totalThreads;
        private int m_runningThreads;
        private int m_idleThreads;
        private int m_minThreads;
        private int m_maxThreads;
        private int m_minCP;
        private int m_maxCP;
        private int m_cpu;
        int m_freeCP;
        int m_maxFreeCP;

        public DesktopThreadPool(DesktopRuntimeBase runtime, IThreadPoolData data)
        {
            m_runtime = runtime;
            m_totalThreads = data.TotalThreads;
            m_runningThreads = data.RunningThreads;
            m_idleThreads = data.IdleThreads;
            m_minThreads = data.MinThreads;
            m_maxThreads = data.MaxThreads;
            m_minCP = data.MinCP;
            m_maxCP = data.MaxCP;
            m_cpu = data.CPU;
            m_freeCP = data.NumFreeCP;
            m_maxFreeCP = data.MaxFreeCP;
        }

        public override int TotalThreads
        {
            get { return m_totalThreads; }
        }

        public override int RunningThreads
        {
            get { return m_runningThreads; }
        }

        public override int IdleThreads
        {
            get { return m_idleThreads; }
        }

        public override int MinThreads
        {
            get { return m_minThreads; }
        }

        public override int MaxThreads
        {
            get { return m_maxThreads; }
        }

        public override IEnumerable<NativeWorkItem> EnumerateNativeWorkItems()
        {
            return m_runtime.EnumerateWorkItems();
        }

        public override IEnumerable<ManagedWorkItem> EnumerateManagedWorkItems()
        {
            foreach (ulong obj in EnumerateManagedThreadpoolObjects())
            {
                if (obj != 0)
                {
                    ClrType type = m_heap.GetObjectType(obj);
                    if (type != null)
                        yield return new DesktopManagedWorkItem(type, obj);
                }
            }
        }

        private IEnumerable<ulong> EnumerateManagedThreadpoolObjects()
        {
            m_heap = m_runtime.GetHeap();

            ClrModule mscorlib = GetMscorlib();
            if (mscorlib != null)
            {
                ClrType queueType = mscorlib.GetTypeByName("System.Threading.ThreadPoolGlobals");
                if (queueType != null)
                {
                    ClrStaticField workQueueField = queueType.GetStaticFieldByName("workQueue");
                    if (workQueueField != null)
                    {
                        foreach (var appDomain in m_runtime.AppDomains)
                        {
                            ulong workQueue = (ulong)workQueueField.GetFieldValue(appDomain);
                            ClrType workQueueType = m_heap.GetObjectType(workQueue);

                            if (workQueue == 0 || workQueueType == null)
                                continue;

                            ulong queueHead;
                            ClrType queueHeadType;
                            do
                            {
                                if (!GetFieldObject(workQueueType, workQueue, "queueHead", out queueHeadType, out queueHead))
                                    break;

                                ulong nodes;
                                ClrType nodesType;
                                if (GetFieldObject(queueHeadType, queueHead, "nodes", out nodesType, out nodes) && nodesType.IsArray)
                                {
                                    int len = nodesType.GetArrayLength(nodes);
                                    for (int i = 0; i < len; ++i)
                                    {
                                        ulong addr = (ulong)nodesType.GetArrayElementValue(nodes, i);
                                        if (addr != 0)
                                            yield return addr;
                                    }
                                }

                                if (!GetFieldObject(queueHeadType, queueHead, "Next", out queueHeadType, out queueHead))
                                    break;
                            } while (queueHead != 0);
                        }
                    }
                }


                queueType = mscorlib.GetTypeByName("System.Threading.ThreadPoolWorkQueue");
                if (queueType != null)
                {
                    ClrStaticField threadQueuesField = queueType.GetStaticFieldByName("allThreadQueues");
                    if (threadQueuesField != null)
                    {
                        foreach (ClrAppDomain domain in m_runtime.AppDomains)
                        {
                            ulong threadQueue = (ulong)threadQueuesField.GetFieldValue(domain);
                            if (threadQueue == 0)
                                continue;

                            ClrType threadQueueType = m_heap.GetObjectType(threadQueue);
                            if (threadQueueType == null)
                                continue;

                            ulong outerArray = 0;
                            ClrType outerArrayType = null;
                            if (!GetFieldObject(threadQueueType, threadQueue, "m_array", out outerArrayType, out outerArray) || !outerArrayType.IsArray)
                                continue;

                            int outerLen = outerArrayType.GetArrayLength(outerArray);
                            for (int i = 0; i < outerLen; ++i)
                            {
                                ulong entry = (ulong)outerArrayType.GetArrayElementValue(outerArray, i);
                                if (entry == 0)
                                    continue;

                                ClrType entryType = m_heap.GetObjectType(entry);
                                if (entryType == null)
                                    continue;

                                ulong array;
                                ClrType arrayType;
                                if (!GetFieldObject(entryType, entry, "m_array", out arrayType, out array) || !arrayType.IsArray)
                                    continue;

                                int len = arrayType.GetArrayLength(array);
                                for (int j = 0; j < len; ++j)
                                {
                                    ulong addr = (ulong)arrayType.GetArrayElementValue(array, i);
                                    if (addr != 0)
                                        yield return addr;
                                }
                            }
                        }
                    }
                }
            }
        }

        private ClrModule GetMscorlib()
        {
            foreach (ClrModule module in m_runtime.EnumerateModules())
                if (module.AssemblyName.Contains("mscorlib.dll"))
                    return module;

            // Uh oh, this shouldn't have happened.  Let's look more carefully (slowly).
            foreach (ClrModule module in m_runtime.EnumerateModules())
                if (module.AssemblyName.ToLower().Contains("mscorlib"))
                    return module;

            // Ok...not sure why we couldn't find it.
            return null;
        }

        bool GetFieldObject(ClrType type, ulong obj, string fieldName, out ClrType valueType, out ulong value)
        {
            value = 0;
            valueType = null;

            ClrInstanceField field = type.GetFieldByName(fieldName);
            if (field == null)
                return false;

            value = (ulong)field.GetFieldValue(obj);
            if (value == 0)
                return false;

            valueType = m_heap.GetObjectType(value);
            return valueType != null;
        }

        public override int MinCompletionPorts
        {
            get { return m_minCP; }
        }

        public override int MaxCompletionPorts
        {
            get { return m_maxCP; }
        }

        public override int CpuUtilization
        {
            get { return m_cpu; }
        }

        public override int FreeCompletionPortCount
        {
            get { return m_freeCP; }
        }

        public override int MaxFreeCompletionPorts
        {
            get { return m_maxFreeCP; }
        }
    }

    class DesktopManagedWorkItem : ManagedWorkItem
    {
        private ClrType m_type;
        private Address m_addr;

        public DesktopManagedWorkItem(ClrType type, ulong addr)
        {
            m_type = type;
            m_addr = addr;
        }

        public override Address Object
        {
            get { return m_addr; }
        }

        public override ClrType Type
        {
            get { return m_type; }
        }
    }

    class DesktopNativeWorkItem : NativeWorkItem
    {
        WorkItemKind m_kind;
        ulong m_callback, m_data;

        public DesktopNativeWorkItem(DacpWorkRequestData result)
        {
            m_callback = result.Function;
            m_data = result.Context;

            switch (result.FunctionType)
            {
                default:
                case WorkRequestFunctionTypes.UNKNOWNWORKITEM:
                    m_kind = WorkItemKind.Unknown;
                    break;

                case WorkRequestFunctionTypes.TIMERDELETEWORKITEM:
                    m_kind = WorkItemKind.TimerDelete;
                    break;

                case WorkRequestFunctionTypes.QUEUEUSERWORKITEM:
                    m_kind = WorkItemKind.QueueUserWorkItem;
                    break;

                case WorkRequestFunctionTypes.ASYNCTIMERCALLBACKCOMPLETION:
                    m_kind = WorkItemKind.AsyncTimer;
                    break;

                case WorkRequestFunctionTypes.ASYNCCALLBACKCOMPLETION:
                    m_kind = WorkItemKind.AsyncCallback;
                    break;
            }
        }


        public DesktopNativeWorkItem(V45WorkRequestData result)
        {
            m_callback = result.Function;
            m_data = result.Context;
            m_kind = WorkItemKind.Unknown;
        }

        public override WorkItemKind Kind
        {
            get { return m_kind; }
        }

        public override Address Callback
        {
            get { return m_callback; }
        }

        public override Address Data
        {
            get { return m_data; }
        }
    }
}
