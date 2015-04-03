#if _REDHAWK
using Microsoft.Diagnostics.Runtime.Desktop;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Address = System.UInt64;

namespace Microsoft.Diagnostics.Runtime.Redhawk
{
    class RhRuntime : RuntimeBase
    {
        ISOSRedhawk m_sos;
        RhHeap m_heap;
        ClrThread[] m_threads;
        RhModule[] m_modules;
        RhAppDomain m_domain;
        int m_dacRawVersion;

        public RhRuntime(DataTargetImpl dt, DacLibrary lib)
            : base(dt, lib)
        {
            byte[] tmp = new byte[sizeof(int)];

            if (!Request(DacRequests.VERSION, null, tmp))
                throw new ClrDiagnosticsException("Failed to request dac version.", ClrDiagnosticsException.HR.DacError);

            m_dacRawVersion = BitConverter.ToInt32(tmp, 0);
            if (m_dacRawVersion != 10 && m_dacRawVersion != 11)
                throw new ClrDiagnosticsException("Unsupported dac version.", ClrDiagnosticsException.HR.DacError);
        }

        protected override void InitApi()
        {
            if (m_sos == null)
            {
                var dac = m_library.DacInterface;
                if (!(dac is ISOSRedhawk))
                    throw new ClrDiagnosticsException("This version of mrt100 is too old.", ClrDiagnosticsException.HR.DataRequestError);

                m_sos = (ISOSRedhawk)dac;
            }
        }

        public override ClrHeap GetHeap()
        {
            if (m_heap == null)
                m_heap = new RhHeap(this, RhModules, null);

            return m_heap;
        }

        public override ClrHeap GetHeap(System.IO.TextWriter log)
        {
            if (m_heap == null)
                m_heap = new RhHeap(this, RhModules, log);
            else
                m_heap.Log = log;

            return m_heap;
        }

        public override int PointerSize
        {
            get { return IntPtr.Size; }
        }

        public override IList<ClrAppDomain> AppDomains
        {
            get
            {
                return new ClrAppDomain[] { GetRhAppDomain() };
            }
        }

        public override IList<ClrThread> Threads
        {
            get 
            {
                if (m_threads == null)
                    InitThreads();

                return m_threads;
            }
        }

        public override IEnumerable<ClrHandle> EnumerateHandles()
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<ClrMemoryRegion> EnumerateMemoryRegions()
        {
            throw new NotImplementedException();
        }

        public override ClrMethod GetMethodByAddress(ulong ip)
        {
            throw new NotImplementedException();
        }

        public override void Flush()
        {
            OnRuntimeFlushed();
            throw new NotImplementedException();
        }

        internal override IList<ClrStackFrame> GetStackTrace(uint osThreadId)
        {
            throw new NotImplementedException();
        }
        override public ClrThreadPool GetThreadPool() { throw new NotImplementedException(); }

        internal ClrAppDomain GetRhAppDomain()
        {
            if (m_domain == null)
                m_domain = new RhAppDomain(RhModules);

            return m_domain;
        }

        internal RhModule[] RhModules
        {
            get
            {
                if (m_modules != null)
                    return m_modules;

                List<ModuleInfo> modules = new List<ModuleInfo>(DataTarget.EnumerateModules());
                modules.Sort((x, y) => x.ImageBase.CompareTo(y.ImageBase));

                int count;
                if (m_sos.GetModuleList(0, null, out count) < 0)
                {
                    m_modules = ConvertModuleList(modules);
                    return m_modules;
                }

                Address[] ptrs = new Address[count];
                if (m_sos.GetModuleList(count, ptrs, out count) < 0)
                {
                    m_modules = ConvertModuleList(modules);
                    return m_modules;
                }

                Array.Sort(ptrs);

                int i = 0, j = 0;
                while (i < modules.Count && j < ptrs.Length)
                {
                    ModuleInfo info = modules[i];
                    ulong addr = ptrs[j];
                    if (info.ImageBase <= addr && addr < info.ImageBase + info.FileSize)
                    {
                        i++;
                        j++;
                    }
                    else if (addr < info.ImageBase)
                    {
                        j++;
                    }
                    else if (addr >= info.ImageBase + info.FileSize)
                    {
                        modules.RemoveAt(i);
                    }
                }

                modules.RemoveRange(i, modules.Count - i);
                m_modules = ConvertModuleList(modules);
                return m_modules;
            }
        }

        private RhModule[] ConvertModuleList(List<ModuleInfo> modules)
        {
            RhModule[] result = new RhModule[modules.Count];

            int i = 0;
            foreach (var module in modules)
                result[i++] = new RhModule(this, module);

            return result;
        }

        internal unsafe IList<ClrRoot> EnumerateStackRoots(ClrThread thread)
        {
            int contextSize;

            var plat = m_dataReader.GetArchitecture();
            if (plat == Architecture.Amd64)
                contextSize = 0x4d0;
            else if (plat == Architecture.X86)
                contextSize = 0x2d0;
            else if (plat == Architecture.Arm)
                contextSize = 0x1a0;
            else
                throw new InvalidOperationException("Unexpected architecture.");
            
            byte[] context = new byte[contextSize];
            m_dataReader.GetThreadContext(thread.OSThreadId, 0, (uint)contextSize, context);

            var walker = new RhStackRootWalker(GetHeap(), GetRhAppDomain(), thread);
            THREADROOTCALLBACK del = new THREADROOTCALLBACK(walker.Callback);
            IntPtr callback = Marshal.GetFunctionPointerForDelegate(del);

            fixed (byte* b = &context[0])
            {
                IntPtr ctx = new IntPtr(b);
                m_sos.TraverseStackRoots(thread.Address, ctx, contextSize, callback, IntPtr.Zero);
            }
            GC.KeepAlive(del);

            return walker.Roots;
        }

        internal IList<ClrRoot> EnumerateStaticRoots(bool resolveStatics)
        {
            var walker = new RhStaticRootWalker(this, resolveStatics);
            STATICROOTCALLBACK del = new STATICROOTCALLBACK(walker.Callback);
            IntPtr ptr = Marshal.GetFunctionPointerForDelegate(del);
            m_sos.TraverseStaticRoots(ptr);
            GC.KeepAlive(del);

            return walker.Roots;
        }

        internal IEnumerable<ClrRoot> EnumerateHandleRoots()
        {
            var walker = new RhHandleRootWalker(this, m_dacRawVersion != 10);
            HANDLECALLBACK callback = new HANDLECALLBACK(walker.RootCallback);
            IntPtr ptr = Marshal.GetFunctionPointerForDelegate(callback);
            m_sos.TraverseHandleTable(ptr, IntPtr.Zero);
            GC.KeepAlive(callback);

            return walker.Roots;
        }

        private void InitThreads()
        {
            IThreadStoreData tsData = GetThreadStoreData();
            List<ClrThread> threads = new List<ClrThread>(tsData.Count);

            ulong addr = tsData.FirstThread;
            IThreadData thread = GetThread(tsData.FirstThread);
            for (int i = 0; thread != null; i++)
            {
                threads.Add(new DesktopThread(this, thread, addr, tsData.Finalizer == addr));

                addr = thread.Next;
                thread = GetThread(addr);
            }

            m_threads = threads.ToArray();
        }



        internal string ResolveSymbol(ulong eetype)
        {
            return DataTarget.ResolveSymbol(eetype);
        }

        #region Rh Implementation
        internal override ulong GetFirstThread()
        {
            IThreadStoreData tsData = GetThreadStoreData();
            if (tsData == null)
                return 0;
            return tsData.FirstThread;
        }

        internal override IThreadData GetThread(ulong addr)
        {
            if (addr == 0)
                return null;

            RhThreadData data;
            if (m_sos.GetThreadData(addr, out data) < 0)
                return null;

            return data;
        }

        internal override IHeapDetails GetSvrHeapDetails(ulong addr)
        {
            RhHeapDetails data;
            if (m_sos.GetGCHeapDetails(addr, out data) < 0)
                return null;

            return data;
        }

        internal override IHeapDetails GetWksHeapDetails()
        {
            RhHeapDetails data;
            if (m_sos.GetGCHeapStaticData(out data) < 0)
                return null;

            return data;
        }

        internal override ulong[] GetServerHeapList()
        {
            int count = 0;
            if (m_sos.GetGCHeapList(0, null, out count) < 0)
                return null;

            ulong[] items = new ulong[count];
            if (m_sos.GetGCHeapList(items.Length, items, out count) < 0)
                return null;

            return items;
        }

        internal override IThreadStoreData GetThreadStoreData()
        {
            RhThreadStoreData data;
            if (m_sos.GetThreadStoreData(out data) < 0)
                return null;

            return data;
        }

        internal override ISegmentData GetSegmentData(ulong addr)
        {
            if (addr == 0)
                return null;

            RhSegementData data;
            if (m_sos.GetGCHeapSegment(addr, out data) < 0)
                return null;

            return data;
        }

        internal override IMethodTableData GetMethodTableData(ulong eetype)
        {
            RhMethodTableData data;
            if (m_sos.GetEETypeData(eetype, out data) < 0)
                return null;

            return data;
        }

        internal ulong GetFreeType()
        {
            // Can't return 0 on error here, as that would make values of 0 look like a
            // valid method table.  Instead, return something that won't likely be the value
            // in the methodtable.
            ulong free;
            if (m_sos.GetFreeEEType(out free) < 0)
                return ulong.MaxValue - 42;

            return free;
        }

        internal override IGCInfo GetGCInfo()
        {
            LegacyGCInfo info;
            if (m_sos.GetGCHeapData(out info) < 0)
                return null;

            return info;
        }
        #endregion

        internal override uint GetTlsSlot()
        {
            throw new NotImplementedException();
        }

        internal override uint GetThreadTypeIndex()
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<int> EnumerateGCThreads()
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<ClrModule> EnumerateModules()
        {
            throw new NotImplementedException();
        }
    }
}
#endif