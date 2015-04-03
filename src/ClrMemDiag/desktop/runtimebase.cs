using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Address = System.UInt64;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    enum DesktopVersion
    {
        v2,
        v4,
        v45
    }

    abstract class DesktopRuntimeBase : RuntimeBase
    {
        #region Variables
        protected CommonMethodTables m_commonMTs;
        Dictionary<Address, DesktopModule> m_modules = new Dictionary<Address,DesktopModule>();
        Dictionary<ulong, uint> m_moduleSizes = null;
        Dictionary<string, DesktopModule> m_moduleFiles = null;
        DesktopAppDomain m_system, m_shared;
        List<ClrAppDomain> m_domains;
        List<ClrThread> m_threads;
        private DesktopGCHeap m_heap;
        DesktopThreadPool m_threadpool;
        internal int Revision { get; set; }
        #endregion

        public override IEnumerable<int> EnumerateGCThreads()
        {
            foreach (uint thread in m_dataReader.EnumerateAllThreads())
            {
                ulong teb = m_dataReader.GetThreadTeb(thread);
                int threadType = DesktopThread.GetTlsSlotForThread(this, teb);
                if ((threadType & (int)DesktopThread.TlsThreadType.ThreadType_GC) == (int)DesktopThread.TlsThreadType.ThreadType_GC)
                    yield return (int)thread;
            }
        }

        internal DesktopGCHeap TryGetHeap()
        {
            return m_heap;
        }


        /// <summary>
        /// Returns the version of the target process (v2, v4, v45)
        /// </summary>
        internal abstract DesktopVersion CLRVersion { get; }

        /// <summary>
        /// Returns the pointer size of the target process.
        /// </summary>
        public override int PointerSize
        {
            get
            {
                return IntPtr.Size;
            }
        }

        /// <summary>
        /// Returns the MethodTable for an array of objects.
        /// </summary>
        public ulong ArrayMethodTable
        {
            get
            {
                return m_commonMTs.ArrayMethodTable;
            }
        }

        internal DesktopModule GetModule(Address module)
        {
            if (module == 0)
                return null;

            DesktopModule res;
            if (m_modules.TryGetValue(module, out res))
                return res;

            IModuleData moduleData = GetModuleData(module);
            if (moduleData == null)
                return null;

            string peFile = GetPEFileName(moduleData.PEFile);
            string assemblyName = GetAssemblyName(moduleData.Assembly);

            if (m_moduleSizes == null)
            {
                m_moduleSizes = new Dictionary<Address, uint>();
                foreach (var native in m_dataReader.EnumerateModules())
                    m_moduleSizes[native.ImageBase] = native.FileSize;
            }

            if (m_moduleFiles == null)
                m_moduleFiles = new Dictionary<string, DesktopModule>();

            uint size = 0;
            m_moduleSizes.TryGetValue(moduleData.ImageBase, out size);
            if (peFile == null)
            {
                res = new DesktopModule(this, module, moduleData, peFile, assemblyName, size);
            }
            else if (!m_moduleFiles.TryGetValue(peFile, out res))
            {
                res = new DesktopModule(this, module, moduleData, peFile, assemblyName, size);
                m_moduleFiles[peFile] = res;
            }

            m_modules[module] = res;
            return res;
        }

        public override IList<ClrAppDomain> AppDomains
        {
            get
            {
                if (m_domains == null)
                    InitDomains();

                return m_domains;
            }
        }

        /// <summary>
        /// Enumerates all managed threads in the process.  Only threads which have previously run managed
        /// code will be enumerated.
        /// </summary>
        public override IList<ClrThread> Threads
        {
            get
            {
                if (m_threads == null)
                    InitThreads();

                return m_threads;
            }
        }

        private void InitThreads()
        {
            if (m_threads == null)
            {
                IThreadStoreData threadStore = GetThreadStoreData();
                ulong finalizer = ulong.MaxValue - 1;
                if (threadStore != null)
                    finalizer = threadStore.Finalizer;

                List<ClrThread> threads = new List<ClrThread>();

                // Give a max number of threads to walk to ensure no infinite loops due to data
                // inconsistency.
                int max = 4098;
                ulong addr = GetFirstThread();
                IThreadData thread = GetThread(addr);

                while (max-- > 0 && thread != null)
                {
                    threads.Add(new DesktopThread(this, thread, addr, addr == finalizer));
                    addr = thread.Next;
                    thread = GetThread(addr);
                }

                m_threads = threads;
            }
        }

        public Address ExceptionMethodTable { get { return m_commonMTs.ExceptionMethodTable; } }
        public ulong ObjectMethodTable
        {
            get
            {
                return m_commonMTs.ObjectMethodTable;
            }
        }

        /// <summary>
        /// Returns the MethodTable for string objects.
        /// </summary>
        public ulong StringMethodTable
        {
            get
            {
                return m_commonMTs.StringMethodTable;
            }
        }

        /// <summary>
        /// Returns the MethodTable for free space markers.
        /// </summary>
        public ulong FreeMethodTable
        {
            get
            {
                return m_commonMTs.FreeMethodTable;
            }
        }


        /// <summary>
        /// Gets the GC heap of the process.
        /// </summary>
        public override ClrHeap GetHeap(TextWriter diagnosticLog)
        {
            if (m_heap == null)
                m_heap = new DesktopGCHeap(this, diagnosticLog);

            return m_heap;
        }

        public override ClrHeap GetHeap()
        {
            if (m_heap == null)
                m_heap = new DesktopGCHeap(this, null);

            return m_heap;
        }

        public override ClrThreadPool GetThreadPool()
        {
            if (m_threadpool != null)
                return m_threadpool;

            IThreadPoolData data = GetThreadPoolData();
            if (data == null)
                return null;

            m_threadpool = new DesktopThreadPool(this, data);
            return m_threadpool;
        }


        /// <summary>
        /// The address of the system domain in CLR.
        /// </summary>
        public ulong SystemDomainAddress
        {
            get
            {
                if (m_domains == null)
                    InitDomains();

                if (m_system == null)
                    return 0;

                return m_system.Address;
            }
        }

        /// <summary>
        /// The address of the shared domain in CLR.
        /// </summary>
        public ulong SharedDomainAddress
        {
            get
            {
                if (m_domains == null)
                    InitDomains();

                if (m_shared == null)
                    return 0;

                return m_shared.Address;
            }
        }

        /// <summary>
        /// Enumerates regions of memory which CLR has allocated with a description of what data
        /// resides at that location.  Note that this does not return every chunk of address space
        /// that CLR allocates.
        /// </summary>
        /// <returns>An enumeration of memory regions in the process.</returns>
        public override IEnumerable<ClrMemoryRegion> EnumerateMemoryRegions()
        {
            // Enumerate GC Segment regions.
            IHeapDetails[] heaps;
            if (ServerGC)
            {
                heaps = new IHeapDetails[HeapCount];
                int i = 0;
                Address[] heapList = GetServerHeapList();
                if (heapList != null)
                {

                    foreach (ulong addr in heapList)
                    {
                        heaps[i++] = GetSvrHeapDetails(addr);
                        if (i == heaps.Length)
                            break;
                    }
                }
                else
                {
                    heaps = new IHeapDetails[0];
                }
            }
            else
            {
                Debug.Assert(HeapCount == 1);
                heaps = new IHeapDetails[1];
                heaps[0] = GetWksHeapDetails();
            }

            int max = 2048;  // Max number of segments in case of inconsistent data.
            for (int i = 0; i < heaps.Length; ++i)
            {
                // Small heap
                ISegmentData segment = GetSegmentData(heaps[i].FirstHeapSegment);
                while (segment != null && max-- > 0)
                {
                    Debug.Assert(segment.Start < segment.Committed);
                    Debug.Assert(segment.Committed <= segment.Reserved);

                    GCSegmentType type = (segment.Address == heaps[i].EphemeralSegment) ? GCSegmentType.Ephemeral : GCSegmentType.Regular;
                    yield return new MemoryRegion(this, segment.Start, segment.Committed - segment.Start, ClrMemoryRegionType.GCSegment, (uint)i, type);
                    yield return new MemoryRegion(this, segment.Committed, segment.Reserved - segment.Committed, ClrMemoryRegionType.ReservedGCSegment, (uint)i, type);

                    if (segment.Address == segment.Next || segment.Address == 0)
                        segment = null;
                    else
                        segment = GetSegmentData(segment.Next);
                }

                segment = GetSegmentData(heaps[i].FirstLargeHeapSegment);
                while (segment != null && max-- > 0)
                {
                    Debug.Assert(segment.Start < segment.Committed);
                    Debug.Assert(segment.Committed <= segment.Reserved);

                    yield return new MemoryRegion(this, segment.Start, segment.Committed - segment.Start, ClrMemoryRegionType.GCSegment, (uint)i, GCSegmentType.LargeObject);
                    yield return new MemoryRegion(this, segment.Committed, segment.Reserved - segment.Committed, ClrMemoryRegionType.ReservedGCSegment, (uint)i, GCSegmentType.LargeObject);

                    if (segment.Address == segment.Next || segment.Address == 0)
                        segment = null;
                    else
                        segment = GetSegmentData(segment.Next);
                }
            }

            // Enumerate handle table regions.
            HashSet<ulong> regions = new HashSet<ulong>();
            foreach (ClrHandle handle in EnumerateHandles())
            {
                VirtualQueryData vq;
                if (!m_dataReader.VirtualQuery(handle.Address, out vq))
                    continue;

                if (regions.Contains(vq.BaseAddress))
                    continue;

                regions.Add(vq.BaseAddress);
                yield return new MemoryRegion(this, vq.BaseAddress, vq.Size, ClrMemoryRegionType.HandleTableChunk, handle.AppDomain);
            }

            // Enumerate each AppDomain and Module specific heap.
            AppDomainHeapWalker adhw = new AppDomainHeapWalker(this);
            IAppDomainData ad = GetAppDomainData(SystemDomainAddress);
            foreach (MemoryRegion region in adhw.EnumerateHeaps(ad))
                yield return region;

            foreach (ulong module in EnumerateModules(ad))
                foreach (MemoryRegion region in adhw.EnumerateModuleHeaps(ad, module))
                    yield return region;

            ad = GetAppDomainData(SharedDomainAddress);
            foreach (MemoryRegion region in adhw.EnumerateHeaps(ad))
                yield return region;

            foreach (ulong module in EnumerateModules(ad))
                foreach (MemoryRegion region in adhw.EnumerateModuleHeaps(ad, module))
                    yield return region;

            IAppDomainStoreData ads = GetAppDomainStoreData();
            if (ads != null)
            {
                IList<ulong> appDomains = GetAppDomainList(ads.Count);
                if (appDomains != null)
                {
                    foreach (ulong addr in appDomains)
                    {
                        ad = GetAppDomainData(addr);
                        foreach (MemoryRegion region in adhw.EnumerateHeaps(ad))
                            yield return region;

                        foreach (ulong module in EnumerateModules(ad))
                            foreach (MemoryRegion region in adhw.EnumerateModuleHeaps(ad, module))
                                yield return region;
                    }
                }
            }


            // Enumerate each JIT code heap.
            regions.Clear();
            foreach (ICodeHeap jitHeap in EnumerateJitHeaps())
            {
                if (jitHeap.Type == CodeHeapType.Host)
                {
                    VirtualQueryData vq;
                    if (m_dataReader.VirtualQuery(jitHeap.Address, out vq))
                        yield return new MemoryRegion(this, vq.BaseAddress, vq.Size, ClrMemoryRegionType.JitHostCodeHeap);
                    else
                        yield return new MemoryRegion(this, jitHeap.Address, 0, ClrMemoryRegionType.JitHostCodeHeap);
                }
                else if (jitHeap.Type == CodeHeapType.Loader)
                {
                    foreach (MemoryRegion region in adhw.EnumerateJitHeap(jitHeap.Address))
                        yield return region;
                }
            }
        }

        /// <summary>
        /// Converts an address into an AppDomain.
        /// </summary>
        public ClrAppDomain GetAppDomainByAddress(ulong address)
        {
            foreach (var ad in AppDomains)
                if (ad.Address == address)
                    return ad;

            return null;
        }

        /// <summary>
        /// Flushes the dac cache.  This function MUST be called any time you expect to call the same function
        /// but expect different results.  For example, after walking the heap, you need to call Flush before
        /// attempting to walk the heap again.
        /// </summary>
        public override void Flush()
        {
            OnRuntimeFlushed();

            Revision++;
            m_dacInterface.Flush();

            m_modules.Clear();
            m_moduleFiles = null;
            m_moduleSizes = null;
            m_domains = null;
            m_system = null;
            m_shared = null;
            m_threads = null;
            MemoryReader = null;
            m_heap = null;
            m_threadpool = null;
        }

        public override ClrMethod GetMethodByAddress(Address ip)
        {
            IMethodDescData mdData = GetMDForIP(ip);
            if (mdData == null)
                return null;

            return DesktopMethod.Create(this, mdData);
        }

        internal IEnumerable<IRWLockData> EnumerateLockData(ulong thread)
        {
            // add offset of the m_pHead (tagLockEntry) field
            thread += GetRWLockDataOffset();
            ulong firstEntry;
            if (ReadPointer(thread, out firstEntry))
            {
                ulong lockEntry = firstEntry;
                byte[] output = GetByteArrayForStruct<RWLockData>();
                do
                {
                    int read;
                    if (!ReadMemory(lockEntry, output, output.Length, out read) || read != output.Length)
                        break;

                    IRWLockData result = ConvertStruct<IRWLockData, RWLockData>(output);
                    if (result != null)
                        yield return result;

                    if (result.Next == lockEntry)
                        break;

                    lockEntry = result.Next;
                } while (lockEntry != firstEntry);
            }
        }

        #region Internal Functions
        internal uint GetExceptionMessageOffset()
        {
            if (PointerSize == 8)
                return 0x20;

            return 0x10;
        }

        internal uint GetStackTraceOffset()
        {
            if (PointerSize == 8)
                return 0x40;

            return 0x20;
        }

        internal ClrThread GetThreadFromThinlockID(uint threadId)
        {
            Address thread = GetThreadFromThinlock(threadId);
            if (thread == 0)
                return null;

            if (m_threads == null)
                InitThreads();

            foreach (var clrThread in m_threads)
                if (clrThread.Address == thread)
                    return clrThread;

            return null;
        }

        public override IEnumerable<ClrModule> EnumerateModules()
        {
            if (m_domains == null)
                InitDomains();

            foreach (var module in m_modules.Values)
                yield return module;
        }

        internal IEnumerable<ulong> EnumerateModules(IAppDomainData appDomain)
        {
            ulong[] assemblies = GetAssemblyList(appDomain.Address, appDomain.AssemblyCount);
            if (assemblies != null)
                foreach (ulong assembly in assemblies)
                {
                    IAssemblyData data = GetAssemblyData(appDomain.Address, assembly);
                    if (data == null)
                        continue;

                    Address[] moduleList = GetModuleList(assembly, (int)data.ModuleCount);
                    if (moduleList != null)
                        foreach (ulong module in moduleList)
                            yield return module;
                }
        }

        internal DesktopRuntimeBase(DataTargetImpl dt, DacLibrary lib)
            : base(dt, lib)
        {
        }

        internal void InitDomains()
        {
            if (m_domains != null)
                return;

            IAppDomainStoreData ads = GetAppDomainStoreData();

            m_modules.Clear();
            m_domains = new List<ClrAppDomain>();

            IList<ulong> domains = GetAppDomainList(ads.Count);
            foreach (ulong domain in domains)
            {
                DesktopAppDomain appDomain = InitDomain(domain);
                if (appDomain != null)
                    m_domains.Add(appDomain);
            }

            m_system = InitDomain(ads.SystemDomain);
            m_shared = InitDomain(ads.SharedDomain);

            m_moduleFiles = null;
            m_moduleSizes = null;
        }

        private DesktopAppDomain InitDomain(ulong domain)
        {
            ulong[] bases = new ulong[1];
            IAppDomainData domainData = GetAppDomainData(domain);
            if (domainData == null)
                return null;

            DesktopAppDomain appDomain = new DesktopAppDomain(this, domainData, GetAppDomaminName(domain));

            if (domainData.AssemblyCount > 0)
            {
                foreach (ulong assembly in GetAssemblyList(domain, domainData.AssemblyCount))
                {
                    IAssemblyData assemblyData = GetAssemblyData(domain, assembly);
                    if (assemblyData == null)
                        continue;

                    if (assemblyData.ModuleCount > 0)
                    {
                        foreach (ulong module in GetModuleList(assembly, assemblyData.ModuleCount))
                        {
                            DesktopModule clrModule = GetModule(module);
                            if (clrModule != null)
                            {
                                clrModule.AddMapping(appDomain, module);
                                appDomain.AddModule(clrModule);
                            }
                        }
                    }
                }
            }

            return appDomain;
        }

        private IEnumerable<DesktopModule> EnumerateImages()
        {
            InitDomains();
            foreach (var module in m_modules.Values)
                if (module.ImageBase != 0)
                    yield return module;
        }

        private IEnumerable<ulong> EnumerateImageBases(IEnumerable<DesktopModule> modules)
        {
            foreach (var module in modules)
                yield return module.ImageBase;
        }

        /// <summary>
        /// 
        /// Returns the name of the type as specified by the TypeHandle.  Note this returns the name as specified by the
        /// metadata, NOT as you would expect to see it in a C# program.  For example, generics are denoted with a ` and
        /// the number of params.  Thus a Dictionary (with two type params) would look like:
        ///     System.Collections.Generics.Dictionary`2
        /// </summary>
        /// <param name="id">The TypeHandle to get the name of.</param>
        /// <returns>The name of the type, or null on error.</returns>
        internal string GetTypeName(TypeHandle id)
        {
            if (id.MethodTable == FreeMethodTable)
                return "Free";

            if (id.MethodTable == ArrayMethodTable && id.ComponentMethodTable != 0)
            {
                string name = GetNameForMT(id.ComponentMethodTable);
                if (name != null)
                    return name + "[]";
            }

            return GetNameForMT(id.MethodTable);
        }

        protected IXCLRDataProcess GetClrDataProcess()
        {
            return m_dacInterface;
        }


        internal override IList<ClrStackFrame> GetStackTrace(uint osThreadId)
        {
            List<ClrStackFrame> result = new List<ClrStackFrame>();
            IXCLRDataProcess proc = GetClrDataProcess();
            object tmp;

            int res = proc.GetTaskByOSThreadID(osThreadId, out tmp);
            if (res < 0)
                return result;

            IXCLRDataTask task = (IXCLRDataTask)tmp;
            res = task.CreateStackWalk(0xf, out tmp);
            if (res < 0)
                return result;

            IXCLRDataStackWalk stackWalk = (IXCLRDataStackWalk)tmp;

            byte[] ulongBuffer = new byte[8];
            byte[] context = new byte[PointerSize == 4 ? 716 : 1232];
            byte[] name = new byte[256];

            int ip_offset = 184;
            int sp_offset = 196;

            if (PointerSize == 8)
            {
                ip_offset = 248;
                sp_offset = 152;
            }

            int maxRecursion = 3;
            int maxDepth = 0xffff;
            ulong last_ip = 0, last_sp = 0;
            int recursionDepth = 0;
            do
            {
                uint size;
                res = stackWalk.GetContext(0x1003f, (uint)context.Length, out size, context);
                if (res < 0 || res == 1)
                    break;

                ulong ip, sp;

                if (PointerSize == 4)
                {
                    ip = BitConverter.ToUInt32(context, ip_offset);
                    sp = BitConverter.ToUInt32(context, sp_offset);
                }
                else
                {
                    ip = BitConverter.ToUInt64(context, ip_offset);
                    sp = BitConverter.ToUInt64(context, sp_offset);
                }


                res = stackWalk.Request(0xf0000000, 0, null, (uint)ulongBuffer.Length, ulongBuffer);

                ulong frameVtbl = 0;
                if (res >= 0)
                {

                    frameVtbl = BitConverter.ToUInt64(ulongBuffer, 0);
                    if (frameVtbl != 0)
                    {
                        sp = frameVtbl;
                        ReadPointer(sp, out frameVtbl);
                    }
                }

                DesktopStackFrame frame = GetStackFrame(res, ip, sp, frameVtbl);
                result.Add(frame);

                if (last_ip == ip && last_sp == sp)
                {
                    recursionDepth++;
                    if (recursionDepth >= maxRecursion)
                        break;
                }
                else
                {
                    last_ip = ip;
                    last_sp = sp;
                }

                maxDepth--;
            } while (stackWalk.Next() == 0 && maxDepth > 0);

            return result;
        }



        internal ILToNativeMap[] GetILMap(Address ip)
        {
            ILToNativeMap[] result = null;

            ulong handle;
            int res = m_dacInterface.StartEnumMethodInstancesByAddress(ip, null, out handle);
            if (res < 0)
                return null;

            object objMethod;
            res = m_dacInterface.EnumMethodInstanceByAddress(ref handle, out objMethod);

            if (res == 0)
            {
                IXCLRDataMethodInstance method = (IXCLRDataMethodInstance)objMethod;

                uint needed = 0;
                res = method.GetILAddressMap(0, out needed, null);

                if (res == 0)
                {
                    result = new ILToNativeMap[needed];
                    res = method.GetILAddressMap(needed, out needed, result);

                    if (res != 0)
                        result = null;
                }

                m_dacInterface.EndEnumMethodInstancesByAddress(handle);
            }

            return result;
        }

        #endregion

        #region Abstract Functions
        internal abstract uint GetExceptionHROffset();
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate void LoaderHeapTraverse(ulong address, IntPtr size, int isCurrent);
        internal abstract IList<ulong> GetAppDomainList(int count);
        internal abstract ulong[] GetAssemblyList(ulong appDomain, int count);
        internal abstract ulong[] GetModuleList(ulong assembly, int count);
        internal abstract IAssemblyData GetAssemblyData(ulong domain, ulong assembly);
        internal abstract IAppDomainStoreData GetAppDomainStoreData();
        internal abstract bool GetCommonMethodTables(ref CommonMethodTables mCommonMTs);
        internal abstract string GetNameForMT(ulong mt);
        internal abstract string GetPEFileName(ulong addr);
        internal abstract IModuleData GetModuleData(ulong addr);
        internal abstract IAppDomainData GetAppDomainData(ulong addr);
        internal abstract string GetAppDomaminName(ulong addr);
        internal abstract bool TraverseHeap(ulong heap, LoaderHeapTraverse callback);
        internal abstract bool TraverseStubHeap(ulong appDomain, int type, LoaderHeapTraverse callback);
        internal abstract IEnumerable<ICodeHeap> EnumerateJitHeaps();
        internal abstract ulong GetModuleForMT(ulong mt);
        internal abstract IFieldInfo GetFieldInfo(Address mt);
        internal abstract IFieldData GetFieldData(Address fieldDesc);
        internal abstract IMetadata GetMetadataImport(Address module);
        internal abstract IObjectData GetObjectData(Address objRef);
        internal abstract IList<Address> GetMethodTableList(Address module);
        internal abstract IDomainLocalModuleData GetDomainLocalModule(Address appDomain, Address id);
        internal abstract ICCWData GetCCWData(Address ccw);
        internal abstract IRCWData GetRCWData(Address rcw);
        internal abstract COMInterfacePointerData[] GetCCWInterfaces(Address ccw, int count);
        internal abstract COMInterfacePointerData[] GetRCWInterfaces(Address rcw, int count);
        internal abstract ulong GetThreadStaticPointer(ulong thread, ClrElementType type, uint offset, uint moduleId, bool shared);
        internal abstract IDomainLocalModuleData GetDomainLocalModule(Address module);
        internal abstract IList<Address> GetMethodDescList(Address methodTable);
        internal abstract string GetNameForMD(Address md);
        internal abstract IMethodDescData GetMethodDescData(Address md);
        internal abstract uint GetMetadataToken(Address mt);
        protected abstract DesktopStackFrame GetStackFrame(int res, ulong ip, ulong sp, ulong frameVtbl);
        internal abstract IList<ClrStackFrame> GetExceptionStackTrace(Address obj, ClrType type);
        internal abstract string GetAssemblyName(Address assembly);
        internal abstract string GetAppBase(Address appDomain);
        internal abstract string GetConfigFile(Address appDomain);
        internal abstract IMethodDescData GetMDForIP(ulong ip);
        protected abstract Address GetThreadFromThinlock(uint threadId);
        internal abstract int GetSyncblkCount();
        internal abstract ISyncBlkData GetSyncblkData(int index);
        internal abstract IThreadPoolData GetThreadPoolData();
        protected abstract uint GetRWLockDataOffset();
        internal abstract IEnumerable<NativeWorkItem> EnumerateWorkItems();
        internal abstract uint GetStringFirstCharOffset();
        internal abstract uint GetStringLengthOffset();
        #endregion


    }

    class MemoryRegion : ClrMemoryRegion
    {
        #region Private Variables
        DesktopRuntimeBase m_runtime;
        ulong m_domainModuleHeap;
        GCSegmentType m_segmentType;
        #endregion

        bool HasAppDomainData
        {
            get
            {
                return Type <= ClrMemoryRegionType.CacheEntryHeap || Type == ClrMemoryRegionType.HandleTableChunk;
            }
        }

        bool HasModuleData
        {
            get
            {
                return Type == ClrMemoryRegionType.ModuleThunkHeap || Type == ClrMemoryRegionType.ModuleLookupTableHeap;
            }
        }

        bool HasGCHeapData
        {
            get
            {
                return Type == ClrMemoryRegionType.GCSegment || Type == ClrMemoryRegionType.ReservedGCSegment;
            }
        }


        public override ClrAppDomain AppDomain
        {
            get
            {
                if (!HasAppDomainData)
                    return null;
                return m_runtime.GetAppDomainByAddress(m_domainModuleHeap);
            }
        }

        public override string Module
        {
            get
            {
                if (!HasModuleData)
                    return null;

                return m_runtime.GetModule(m_domainModuleHeap).FileName;
            }
        }

        public override int HeapNumber
        {
            get
            {
                if (!HasGCHeapData)
                    return -1;

                Debug.Assert(m_domainModuleHeap < uint.MaxValue);
                return (int)m_domainModuleHeap;
            }
            set
            {
                m_domainModuleHeap = (ulong)value;
            }
        }

        public override GCSegmentType GCSegmentType
        {
            get
            {
                if (!HasGCHeapData)
                    throw new NotSupportedException();

                return m_segmentType;
            }
            set
            {
                m_segmentType = value;
            }
        }

        public override string ToString(bool detailed)
        {
            string value = null;

            switch (Type)
            {
                case ClrMemoryRegionType.LowFrequencyLoaderHeap:
                    value = "Low Frequency Loader Heap";
                    break;

                case ClrMemoryRegionType.HighFrequencyLoaderHeap:
                    value = "High Frequency Loader Heap";
                    break;

                case ClrMemoryRegionType.StubHeap:
                    value = "Stub Heap";
                    break;

                // Virtual Call Stub heaps
                case ClrMemoryRegionType.IndcellHeap:
                    value = "Indirection Cell Heap";
                    break;

                case ClrMemoryRegionType.LookupHeap:
                    value = "Loopup Heap";
                    break;

                case ClrMemoryRegionType.ResolveHeap:
                    value = "Resolver Heap";
                    break;

                case ClrMemoryRegionType.DispatchHeap:
                    value = "Dispatch Heap";
                    break;

                case ClrMemoryRegionType.CacheEntryHeap:
                    value = "Cache Entry Heap";
                    break;

                // Other regions
                case ClrMemoryRegionType.JitHostCodeHeap:
                    value = "JIT Host Code Heap";
                    break;

                case ClrMemoryRegionType.JitLoaderCodeHeap:
                    value = "JIT Loader Code Heap";
                    break;

                case ClrMemoryRegionType.ModuleThunkHeap:
                    value = "Thunk Heap";
                    break;

                case ClrMemoryRegionType.ModuleLookupTableHeap:
                    value = "Lookup Table Heap";
                    break;

                case ClrMemoryRegionType.HandleTableChunk:
                    value = "GC Handle Table Chunk";
                    break;

                case ClrMemoryRegionType.ReservedGCSegment:
                case ClrMemoryRegionType.GCSegment:
                    if (m_segmentType == GCSegmentType.Ephemeral)
                        value = "Ephemeral Segment";
                    else if (m_segmentType == GCSegmentType.LargeObject)
                        value = "Large Object Segment";
                    else
                        value = "GC Segment";

                    if (Type == ClrMemoryRegionType.ReservedGCSegment)
                        value += " (Reserved)";
                    break;

                default:
                    // should never happen.
                    value = "<unknown>";
                    break;
            }

            if (detailed)
            {
                if (HasAppDomainData)
                {
                    if (m_domainModuleHeap == m_runtime.SharedDomainAddress)
                    {
                        value = string.Format("{0} for Shared AppDomain", value);
                    }
                    else if (m_domainModuleHeap == m_runtime.SystemDomainAddress)
                    {
                        value = string.Format("{0} for System AppDomain", value);
                    }
                    else
                    {
                        ClrAppDomain domain = AppDomain;
                        value = string.Format("{0} for AppDomain {1}: {2}", value, domain.Id, domain.Name);
                    }
                }
                else if (HasModuleData)
                {
                    string fn = m_runtime.GetModule(m_domainModuleHeap).FileName;
                    value = string.Format("{0} for Module: {1}", value, Path.GetFileName(fn));
                }
                else if (HasGCHeapData)
                {
                    value = string.Format("{0} for Heap {1}", value, HeapNumber);
                }
            }

            return value;
        }

        /// <summary>
        /// Equivalent to GetDisplayString(false).
        /// </summary>
        public override string ToString()
        {
            return ToString(false);
        }

        #region Constructors
        internal MemoryRegion(DesktopRuntimeBase clr, ulong addr, ulong size, ClrMemoryRegionType type, ulong moduleOrAppDomain)
        {
            Address = addr;
            Size = size;
            m_runtime = clr;
            Type = type;
            m_domainModuleHeap = moduleOrAppDomain;
        }

        internal MemoryRegion(DesktopRuntimeBase clr, ulong addr, ulong size, ClrMemoryRegionType type, ClrAppDomain domain)
        {
            Address = addr;
            Size = size;
            m_runtime = clr;
            Type = type;
            m_domainModuleHeap = domain.Address;
        }

        internal MemoryRegion(DesktopRuntimeBase clr, ulong addr, ulong size, ClrMemoryRegionType type)
        {
            Address = addr;
            Size = size;
            m_runtime = clr;
            Type = type;
        }

        internal MemoryRegion(DesktopRuntimeBase clr, ulong addr, ulong size, ClrMemoryRegionType type, uint heap, GCSegmentType seg)
        {
            Address = addr;
            Size = size;
            m_runtime = clr;
            Type = type;
            m_domainModuleHeap = heap;
            m_segmentType = seg;
        }
        #endregion
    }

    class SubHeap
    {
        internal int HeapNum { get; private set; }
        IHeapDetails ActualHeap { get; set; }

        /// <summary>
        /// The allocation context pointers/limits for this heap.  The keys of this
        /// dictionary are the allocation pointers, the values of this dictionary are
        /// the limits.  If an allocation pointer is ever reached while walking a
        /// segment, you must "skip" past the allocation limit.  That is:
        ///     if (curr_obj is in AllocPointers)
        ///         curr_obj = AllocPointers[curr_obj] + min_object_size;
        /// </summary>
        internal Dictionary<ulong, ulong> AllocPointers { get; set; }

        /// <summary>
        /// Returns the address of the ephemeral segment.  Users of this API should use
        /// HeapSegment.Ephemeral instead of this property.
        /// </summary>
        internal ulong EphemeralSegment { get { return ActualHeap.EphemeralSegment; } }

        /// <summary>
        /// Returns the actual end of the ephemeral segment.
        /// </summary>
        internal ulong EphemeralEnd { get { return ActualHeap.EphemeralEnd; } }

        internal ulong Gen0Start { get { return ActualHeap.Gen0Start; } }
        internal ulong Gen1Start { get { return ActualHeap.Gen1Start; } }
        internal ulong Gen2Start { get { return ActualHeap.Gen2Start; } }
        internal ulong FirstLargeSegment { get { return ActualHeap.FirstLargeHeapSegment; } }
        internal ulong FirstSegment { get { return ActualHeap.FirstHeapSegment; } }
        internal ulong FQStart { get { return ActualHeap.FQStart; } }
        internal ulong FQStop { get { return ActualHeap.FQStop; } }
        internal ulong FQLiveStart { get { return ActualHeap.FQLiveStart; } }
        internal ulong FQLiveStop { get { return ActualHeap.FQLiveEnd; } }

        internal SubHeap(IHeapDetails heap, int heapNum)
        {
            ActualHeap = heap;
            HeapNum = heapNum;
        }
    }


    #region Data Interfaces
    enum CodeHeapType
    {
        Loader,
        Host,
        Unknown
    }

    interface ICodeHeap
    {
        CodeHeapType Type { get; }
        ulong Address { get; }
    }

    interface IThreadPoolData
    {
        int TotalThreads { get; }
        int RunningThreads { get; }
        int IdleThreads { get; }
        int MinThreads { get; }
        int MaxThreads { get; }
        ulong FirstWorkRequest { get; }
        ulong QueueUserWorkItemCallbackFPtr { get; }
        ulong AsyncCallbackCompletionFPtr { get; }
        ulong AsyncTimerCallbackCompletionFPtr { get; }
        int MinCP { get; }
        int MaxCP { get; }
        int CPU { get; }
        int NumFreeCP { get; }
        int MaxFreeCP { get; }
    }

    interface IAssemblyData
    {
        ulong Address { get; }
        ulong ParentDomain { get; }
        ulong AppDomain { get; }
        bool IsDynamic { get; }
        bool IsDomainNeutral { get; }
        int ModuleCount { get; }
    }

    interface IAppDomainData
    {
        int Id { get; }
        ulong Address { get; }
        ulong LowFrequencyHeap { get; }
        ulong HighFrequencyHeap { get; }
        ulong StubHeap { get; }
        int AssemblyCount { get; }
    }

    interface IThreadStoreData
    {
        ulong Finalizer { get; }
        ulong FirstThread { get; }
        int Count { get; }
    }

    interface IThreadData
    {
        ulong Next { get; }
        ulong AllocPtr { get; }
        ulong AllocLimit { get; }
        uint OSThreadID { get; }
        uint ManagedThreadID { get; }
        ulong Teb { get; }
        ulong AppDomain { get; }
        uint LockCount { get; }
        int State { get; }
        ulong ExceptionPtr { get; }
        bool Preemptive { get; }
    }

    interface ISegmentData
    {
        ulong Address { get; }
        ulong Next { get; }
        ulong Start { get; }
        ulong End { get; }
        ulong Committed { get; }
        ulong Reserved { get; }
    }

    interface IHeapDetails
    {
        ulong FirstHeapSegment { get; }
        ulong FirstLargeHeapSegment { get; }
        ulong EphemeralSegment { get; }
        ulong EphemeralEnd { get; }
        ulong EphemeralAllocContextPtr { get; }
        ulong EphemeralAllocContextLimit { get; }
        ulong FQStart { get; }
        ulong FQStop { get; }

        ulong Gen0Start { get; }
        ulong Gen0Stop { get; }
        ulong Gen1Start { get; }
        ulong Gen1Stop { get; }
        ulong Gen2Start { get; }
        ulong Gen2Stop { get; }

        ulong FQLiveStart { get; }
        ulong FQLiveEnd { get; }
    }

    interface IGCInfo
    {
        bool ServerMode { get; }
        int HeapCount { get; }
        int MaxGeneration { get; }
        bool GCStructuresValid { get; }
    }

    interface IMethodTableData
    {
        bool Shared { get; }
        bool Free { get; }
        bool ContainsPointers { get; }
        uint BaseSize { get; }
        uint ComponentSize { get; }
        ulong EEClass { get; }
        ulong Parent { get; }
        uint NumMethods { get; }
        ulong ElementTypeHandle { get; }
    }

    interface IFieldInfo
    {
        uint InstanceFields { get; }
        uint StaticFields { get; }
        uint ThreadStaticFields { get; }
        ulong FirstField { get; }
    }

    interface IFieldData
    {
        uint CorElementType { get; }
        uint SigType { get; }
        ulong TypeMethodTable { get; }

        ulong Module { get; }
        uint TypeToken { get; }

        uint FieldToken { get; }
        ulong EnclosingMethodTable { get; }
        uint Offset { get; }
        bool IsThreadLocal { get; }
        bool bIsContextLocal { get; }
        bool bIsStatic { get; }
        ulong nextField { get; }
    }

    interface IEEClassData
    {
        ulong MethodTable { get; }
        ulong Module { get; }
    }

    interface IDomainLocalModuleData
    {
        ulong AppDomainAddr { get; }
        ulong ModuleID { get; }

        ulong ClassData { get; }
        ulong DynamicClassTable { get; }
        ulong GCStaticDataStart { get; }
        ulong NonGCStaticDataStart { get; }
    }

    interface IModuleData
    {
        ulong ImageBase { get; }
        ulong PEFile { get; }
        ulong LookupTableHeap { get; }
        ulong ThunkHeap { get; }
        object LegacyMetaDataImport { get; }
        ulong ModuleId { get; }
        ulong ModuleIndex { get; }
        ulong Assembly { get; }
        bool IsReflection { get; }
        bool IsPEFile { get; }
        ulong MetdataStart { get; }
        ulong MetadataLength { get; }
    }

    interface IMethodDescData
    {
        ulong MethodDesc { get; }
        ulong Module { get; }
        uint MDToken { get; }
        ulong NativeCodeAddr { get; }
        ulong MethodTable { get; }
        MethodCompilationType JITType { get; }
    }

    interface ICCWData
    {
        ulong IUnknown { get; }
        ulong Object { get; }
        ulong Handle { get; }
        ulong CCWAddress { get; }
        int RefCount { get; }
        int JupiterRefCount { get; }
        int InterfaceCount { get; }
    }

    interface IRCWData
    {
        ulong IdentityPointer { get; }
        ulong UnknownPointer { get; }
        ulong ManagedObject { get; }
        ulong JupiterObject { get; }
        ulong VTablePtr { get; }
        ulong CreatorThread { get; }

        int RefCount { get; }
        int InterfaceCount { get; }

        bool IsJupiterObject { get; }
        bool IsDisconnected { get; }
    }

    interface IAppDomainStoreData
    {
        ulong SharedDomain { get; }
        ulong SystemDomain { get; }
        int Count { get; }
    }

    interface IObjectData
    {
        ulong DataPointer { get; }
        ulong ElementTypeHandle { get; }
        ClrElementType ElementType { get; }
        ulong RCW { get; }
        ulong CCW { get; }
    }

    interface ISyncBlkData
    {
        bool Free { get; }
        ulong Address { get; }
        ulong Object { get; }
        ulong OwningThread { get; }
        bool MonitorHeld { get; }
        uint Recursion { get; }
        uint TotalCount { get; }
    }

    // This is consistent across all dac versions.  No need for interface.
    struct CommonMethodTables
    {
        public ulong ArrayMethodTable;
        public ulong StringMethodTable;
        public ulong ObjectMethodTable;
        public ulong ExceptionMethodTable;
        public ulong FreeMethodTable;
    };

    
    interface IRWLockData
    {
        ulong Next { get; }
        int ULockID { get; }
        int LLockID { get; }
        int Level { get; }
    }

    struct RWLockData : IRWLockData
    {
        public IntPtr pNext;
        public IntPtr pPrev;
        public int _uLockID;
        public int _lLockID;
        public Int16 wReaderLevel;

        public ulong Next
        {
            get { return (ulong)pNext.ToInt64(); }
        }

        public int ULockID
        {
            get { return _uLockID; }
        }

        public int LLockID
        {
            get { return _lLockID; }
        }


        public int Level
        {
            get { return wReaderLevel; }
        }
    }
    #endregion
}
