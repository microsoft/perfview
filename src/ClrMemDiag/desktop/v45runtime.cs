using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    class V45Runtime : DesktopRuntimeBase
    {
        ISOSDac m_sos;

        #region Constructor
        public V45Runtime(DataTargetImpl dt, DacLibrary lib)
            : base(dt, lib)
        {
            if (!GetCommonMethodTables(ref m_commonMTs))
                throw new ClrDiagnosticsException("Could not request common MethodTable list.", ClrDiagnosticsException.HR.DacError);

            // Ensure the version of the dac API matches the one we expect.  (Same for both
            // v2 and v4 rtm.)
            byte[] tmp = new byte[sizeof(int)];

            if (!Request(DacRequests.VERSION, null, tmp))
                throw new ClrDiagnosticsException("Failed to request dac version.", ClrDiagnosticsException.HR.DacError);

            int v = BitConverter.ToInt32(tmp, 0);
            if (v != 9)
                throw new ClrDiagnosticsException("Unsupported dac version.", ClrDiagnosticsException.HR.DacError);
        }
        #endregion

        #region Overrides
        protected override void InitApi()
        {
            if (m_sos == null)
                m_sos = m_library.SOSInterface;

            Debug.Assert(m_sos != null);
        }
        internal override DesktopVersion CLRVersion
        {
            get { return DesktopVersion.v45; }
        }

        public override IEnumerable<ClrHandle> EnumerateHandles()
        {
            object tmp;
            if (m_sos.GetHandleEnum(out tmp) < 0)
                return null;

            ISOSHandleEnum handleEnum = tmp as ISOSHandleEnum;
            if (handleEnum == null)
                return null;

            HandleData[] handles = new HandleData[1];
            List<ClrHandle> res = new List<ClrHandle>();

            uint fetched = 0;
            do
            {
                if (handleEnum.Next(1, handles, out fetched) != 0)
                    break;

                ClrHandle handle = new ClrHandle(this, GetHeap(), handles[0]);
                res.Add(handle);
            } while (fetched == 1);

            return res;
        }

        internal override IEnumerable<ClrRoot> EnumerateStackReferences(ClrThread thread, bool includeDead)
        {
            if (includeDead)
                return base.EnumerateStackReferences(thread, includeDead);

            return EnumerateStackReferencesWorker(thread);
        }

        IEnumerable<ClrRoot> EnumerateStackReferencesWorker(ClrThread thread)
        {
            ISOSStackRefEnum handleEnum = null;
            object tmp;
            if (m_sos.GetStackReferences(thread.OSThreadId, out tmp) >= 0)
                handleEnum = tmp as ISOSStackRefEnum;

            if (handleEnum != null)
            {
                var heap = GetHeap();
                StackRefData[] refs = new StackRefData[1024];

                const int GCInteriorFlag = 1;
                const int GCPinnedFlag = 2;
                uint fetched = 0;
                do
                {
                    if (handleEnum.Next((uint)refs.Length, refs, out fetched) < 0)
                        break;

                    for (uint i = 0; i < fetched && i < refs.Length; ++i)
                    {
                        if (refs[i].Object == 0)
                            continue;

                        bool pinned = (refs[i].Flags & GCPinnedFlag) == GCPinnedFlag;
                        bool interior = (refs[i].Flags & GCInteriorFlag) == GCInteriorFlag;

                        ClrType type = null;

                        if (!interior)
                            type = heap.GetObjectType(refs[i].Object);

                        if (interior || type != null)
                            yield return new LocalVarRoot(refs[i].Address, refs[i].Object, type, thread, pinned, false, interior);
                    }
                } while (fetched == refs.Length);
            }
        }

        internal override ulong GetFirstThread()
        {
            IThreadStoreData threadStore = GetThreadStoreData();
            return threadStore != null ? threadStore.FirstThread : 0;
        }

        internal override IThreadData GetThread(ulong addr)
        {
            if (addr == 0)
                return null;

            V4ThreadData data;
            if (m_sos.GetThreadData(addr, out data) < 0)
                return null;

            return data;
        }

        internal override IHeapDetails GetSvrHeapDetails(ulong addr)
        {
            V4HeapDetails data;
            if (m_sos.GetGCHeapDetails(addr, out data) < 0)
                return null;
            return data;
        }

        internal override IHeapDetails GetWksHeapDetails()
        {
            V4HeapDetails data;
            if (m_sos.GetGCHeapStaticData(out data) < 0)
                return null;
            return data;
        }

        internal override ulong[] GetServerHeapList()
        {
            uint needed;
            ulong[] refs = new ulong[HeapCount];
            if (m_sos.GetGCHeapList((uint)HeapCount, refs, out needed) < 0)
                return null;

            return refs;
        }

        internal override IList<ulong> GetAppDomainList(int count)
        {
            ulong[] data = new ulong[1024];
            uint needed;
            if (m_sos.GetAppDomainList((uint)data.Length, data, out needed) < 0)
                return null;

            List<ulong> list = new List<ulong>((int)needed);

            for (uint i = 0; i < needed; ++i)
                list.Add(data[i]);

            return list;
        }

        internal override ulong[] GetAssemblyList(ulong appDomain, int count)
        {
            // It's not valid to request an assembly list for the system domain in v4.5.
            if (appDomain == SystemDomainAddress)
                return new ulong[0];

            int needed;
            if (m_sos.GetAssemblyList(appDomain, 0, null, out needed) < 0)
                return null;

            ulong[] modules = new ulong[needed];
            if (m_sos.GetAssemblyList(appDomain, needed, modules, out needed) < 0)
                return null;

            return modules;
        }

        internal override ulong[] GetModuleList(ulong assembly, int count)
        {
            uint needed;
            if (m_sos.GetAssemblyModuleList(assembly, 0, null, out needed) < 0)
                return null;

            ulong[] modules = new ulong[needed];
            if (m_sos.GetAssemblyModuleList(assembly, needed, modules, out needed) < 0)
                return null;

            return modules;
        }

        internal override IAssemblyData GetAssemblyData(ulong domain, ulong assembly)
        {
            LegacyAssemblyData data;
            if (m_sos.GetAssemblyData(domain, assembly, out data) < 0)
                return null;

            return data;
        }

        internal override IAppDomainStoreData GetAppDomainStoreData()
        {
            LegacyAppDomainStoreData data;
            if (m_sos.GetAppDomainStoreData(out data) < 0)
                return null;

            return data;
        }

        internal override IMethodTableData GetMethodTableData(ulong addr)
        {
            V45MethodTableData data;
            if (m_sos.GetMethodTableData(addr, out data) < 0)
                return null;

            return data;
        }

        internal override IGCInfo GetGCInfo()
        {
            LegacyGCInfo gcInfo;
            return (m_sos.GetGCHeapData(out gcInfo) >= 0) ? (IGCInfo)gcInfo : null;
        }

        internal override bool GetCommonMethodTables(ref CommonMethodTables mCommonMTs)
        {
            return m_sos.GetUsefulGlobals(out mCommonMTs) >= 0;
        }

        internal override string GetNameForMT(ulong mt)
        {
            uint count;
            if (m_sos.GetMethodTableName(mt, 0, null, out count) < 0)
                return null;

            StringBuilder sb = new StringBuilder();
            sb.Capacity = (int)count;

            if (m_sos.GetMethodTableName(mt, count, sb, out count) < 0)
                return null;

            return sb.ToString();
        }

        internal override string GetPEFileName(ulong addr)
        {
            uint needed;
            if (m_sos.GetPEFileName(addr, 0, null, out needed) < 0)
                return null;

            StringBuilder sb = new StringBuilder((int)needed);
            if (m_sos.GetPEFileName(addr, needed, sb, out needed) < 0)
                return null;

            return sb.ToString();
        }

        internal override IModuleData GetModuleData(ulong addr)
        {
            V45ModuleData data;
            return m_sos.GetModuleData(addr, out data) >= 0 ? (IModuleData)data : null;
        }

        internal override ulong GetModuleForMT(ulong addr)
        {
            V45MethodTableData data;
            if (m_sos.GetMethodTableData(addr, out data) < 0)
                return 0;

            return data.module;
        }

        internal override ISegmentData GetSegmentData(ulong addr)
        {
            V4SegmentData seg;
            if (m_sos.GetHeapSegmentData(addr, out seg) < 0)
                return null;
            return seg;
        }

        internal override IAppDomainData GetAppDomainData(ulong addr)
        {
            LegacyAppDomainData data;
            if (m_sos.GetAppDomainData(addr, out data) < 0)
                return null;
            return data;
        }

        internal override string GetAppDomaminName(ulong addr)
        {
            uint count;
            if (m_sos.GetAppDomainName(addr, 0, null, out count) < 0)
                return null;

            StringBuilder sb = new StringBuilder();
            sb.Capacity = (int)count;

            if (m_sos.GetAppDomainName(addr, count, sb, out count) < 0)
                return null;

            return sb.ToString();
        }

        internal override string GetAssemblyName(ulong addr)
        {
            uint count;
            if (m_sos.GetAssemblyName(addr, 0, null, out count) < 0)
                return null;

            StringBuilder sb = new StringBuilder();
            sb.Capacity = (int)count;

            if (m_sos.GetAssemblyName(addr, count, sb, out count) < 0)
                return null;

            return sb.ToString();
        }

        internal override bool TraverseHeap(ulong heap, DesktopRuntimeBase.LoaderHeapTraverse callback)
        {
            bool res = m_sos.TraverseLoaderHeap(heap, Marshal.GetFunctionPointerForDelegate(callback)) >= 0;
            GC.KeepAlive(callback);
            return res;
        }

        internal override bool TraverseStubHeap(ulong appDomain, int type, DesktopRuntimeBase.LoaderHeapTraverse callback)
        {
            bool res = m_sos.TraverseVirtCallStubHeap(appDomain, (uint)type, Marshal.GetFunctionPointerForDelegate(callback)) >= 0;
            GC.KeepAlive(callback);
            return res;
        }

        internal override IEnumerable<ICodeHeap> EnumerateJitHeaps()
        {
            LegacyJitManagerInfo[] jitManagers = null;

            uint needed = 0;
            int res = m_sos.GetJitManagerList(0, null, out needed);
            if (res >= 0)
            {
                jitManagers = new LegacyJitManagerInfo[needed];
                res = m_sos.GetJitManagerList(needed, jitManagers, out needed);
            }

            if (res >= 0 && jitManagers != null)
            {
                for (int i = 0; i < jitManagers.Length; ++i)
                {
                    if (jitManagers[i].type != CodeHeapType.Unknown)
                        continue;

                    res = m_sos.GetCodeHeapList(jitManagers[i].addr, 0, null, out needed);
                    if (res >= 0 && needed > 0)
                    {
                        LegacyJitCodeHeapInfo[] heapInfo = new LegacyJitCodeHeapInfo[needed];
                        res = m_sos.GetCodeHeapList(jitManagers[i].addr, needed, heapInfo, out needed);

                        if (res >= 0)
                        {
                            for (int j = 0; j < heapInfo.Length; ++j)
                            {
                                yield return (ICodeHeap)heapInfo[i];
                            }
                        }
                    }
                }
            }
        }

        internal override IFieldInfo GetFieldInfo(ulong mt)
        {
            V4FieldInfo fieldInfo;
            if (m_sos.GetMethodTableFieldData(mt, out fieldInfo) < 0)
                return null;

            return fieldInfo;
        }

        internal override IFieldData GetFieldData(ulong fieldDesc)
        {
            LegacyFieldData data;
            if (m_sos.GetFieldDescData(fieldDesc, out data) < 0)
                return null;

            return data;
        }

        internal override IMetadata GetMetadataImport(ulong module)
        {
            object obj = null;
            if (module == 0 || m_sos.GetModule(module, out obj) < 0)
                return null;

            return obj as IMetadata;
        }

        internal override IObjectData GetObjectData(ulong objRef)
        {
            V45ObjectData data;
            if (m_sos.GetObjectData(objRef, out data) < 0)
                return null;
            return data;
        }

        internal override IList<ulong> GetMethodTableList(ulong module)
        {
            List<ulong> mts = new List<ulong>();
            int res = m_sos.TraverseModuleMap(0, module, new ModuleMapTraverse(delegate(uint index, ulong mt, IntPtr token)
                { mts.Add(mt); }),
                IntPtr.Zero);

            return (res < 0) ? null : mts;
        }

        internal override IDomainLocalModuleData GetDomainLocalModule(ulong appDomain, ulong id)
        {
            V45DomainLocalModuleData data;
            int res = m_sos.GetDomainLocalModuleDataFromAppDomain(appDomain, (int)id, out data);
            if (res < 0)
                return null;

            return data;
        }

        internal override COMInterfacePointerData[] GetCCWInterfaces(ulong ccw, int count)
        {
            COMInterfacePointerData[] data = new COMInterfacePointerData[count];
            uint pNeeded;
            if (m_sos.GetCCWInterfaces(ccw, (uint)count, data, out pNeeded) >= 0)
                return data;

            return null;
        }

        internal override COMInterfacePointerData[] GetRCWInterfaces(ulong rcw, int count)
        {
            COMInterfacePointerData[] data = new COMInterfacePointerData[count];
            uint pNeeded;
            if (m_sos.GetRCWInterfaces(rcw, (uint)count, data, out pNeeded) >= 0)
                return data;

            return null;
        }
        internal override ICCWData GetCCWData(ulong ccw)
        {
            V45CCWData data;
            if (ccw != 0 && m_sos.GetCCWData(ccw, out data) >= 0)
                return data;

            return null;
        }

        internal override IRCWData GetRCWData(ulong rcw)
        {
            V45RCWData data;
            if (rcw != 0 && m_sos.GetRCWData(rcw, out data) >= 0)
                return data;

            return null;
        }
        #endregion

        internal override ulong GetThreadStaticPointer(ulong thread, ClrElementType type, uint offset, uint moduleId, bool shared)
        {
            ulong addr = offset;

            V45ThreadLocalModuleData data;
            if (m_sos.GetThreadLocalModuleData(thread, moduleId, out data) < 0)
                return 0;

            if (IsObjectReference(type) || IsValueClass(type))
                addr += data.pGCStaticDataStart;
            else
                addr += data.pNonGCStaticDataStart;

            return addr;
        }

        internal override IDomainLocalModuleData GetDomainLocalModule(ulong module)
        {
            V45DomainLocalModuleData data;
            if (m_sos.GetDomainLocalModuleDataFromModule(module, out data) < 0)
                return null;

            return data;
        }

        internal override IList<ulong> GetMethodDescList(ulong methodTable)
        {
            V45MethodTableData mtData;
            if (m_sos.GetMethodTableData(methodTable, out mtData) < 0)
                return null;

            List<ulong> mds = new List<ulong>((int)mtData.wNumMethods);

            CodeHeaderData header;
            ulong ip = 0;
            for (uint i = 0; i < mtData.wNumMethods; ++i)
                if (m_sos.GetMethodTableSlot(methodTable, i, out ip) >= 0)
                {
                    if (m_sos.GetCodeHeaderData(ip, out header) >= 0)
                        mds.Add(header.MethodDescPtr);
                }

            return mds;
        }

        internal override string GetNameForMD(ulong md)
        {
            StringBuilder sb = new StringBuilder();
            uint needed = 0;
            if (m_sos.GetMethodDescName(md, 0, null, out needed) < 0)
                return "UNKNOWN";

            sb.Capacity = (int)needed;
            if (m_sos.GetMethodDescName(md, (uint)sb.Capacity, sb, out needed) < 0)
                return "UNKNOWN";

            return sb.ToString();
        }

        internal override IMethodDescData GetMethodDescData(ulong md)
        {
            V45MethodDescDataWrapper wrapper = new V45MethodDescDataWrapper();
            if (!wrapper.Init(m_sos, md))
                return null;

            return wrapper;
        }

        internal override uint GetMetadataToken(ulong mt)
        {
            V45MethodTableData data;
            if (m_sos.GetMethodTableData(mt, out data) < 0)
                return uint.MaxValue;

            return data.token;
        }

        protected override DesktopStackFrame GetStackFrame(int res, ulong ip, ulong framePtr, ulong frameVtbl)
        {
            DesktopStackFrame frame;
            StringBuilder sb = new StringBuilder();
            sb.Capacity = 256;
            uint needed;
            if (res >= 0 && frameVtbl != 0)
            {
                ClrMethod innerMethod = null;
                string frameName = "Unknown Frame";
                if (m_sos.GetFrameName(frameVtbl, (uint)sb.Capacity, sb, out needed) >= 0)
                    frameName = sb.ToString();

                ulong md = 0;
                if (m_sos.GetMethodDescPtrFromFrame(framePtr, out md) == 0)
                {
                    V45MethodDescDataWrapper mdData = new V45MethodDescDataWrapper();
                    if (mdData.Init(m_sos, md))
                        innerMethod = DesktopMethod.Create(this, mdData);
                }

                frame = new DesktopStackFrame(this, framePtr, frameName, innerMethod);
            }
            else
            {
                ulong md;
                if (m_sos.GetMethodDescPtrFromIP(ip, out md) >= 0 && m_sos.GetMethodDescName(md, (uint)sb.Capacity, sb, out needed) >= 0)
                    frame = new DesktopStackFrame(this, ip, framePtr, sb.ToString());
                else
                    frame = new DesktopStackFrame(this, ip, framePtr, "Unknown");
            }

            return frame;
        }

        bool GetStackTraceFromField(ClrType type, ulong obj, out ulong stackTrace)
        {
            stackTrace = 0;
            var field = type.GetFieldByName("_stackTrace");
            if (field == null)
                return false;

            object tmp = field.GetFieldValue(obj);
            if (tmp == null || !(tmp is ulong))
                return false;

            stackTrace = (ulong)tmp;
            return true;
        }


        internal override IList<ClrStackFrame> GetExceptionStackTrace(ulong obj, ClrType type)
        {
            // TODO: Review this and if it works on v4.5, merge the two implementations back into RuntimeBase.
            List<ClrStackFrame> result = new List<ClrStackFrame>();
            if (type == null)
                return result;

            ulong _stackTrace;
            if (!GetStackTraceFromField(type, obj, out _stackTrace))
            {
                if (!ReadPointer(obj + GetStackTraceOffset(), out _stackTrace))
                    return result;
            }

            if (_stackTrace == 0)
                return result;

            ClrHeap heap = GetHeap();
            ClrType stackTraceType = heap.GetObjectType(_stackTrace);
            if (stackTraceType == null || !stackTraceType.IsArray)
                return result;

            int len = stackTraceType.GetArrayLength(_stackTrace);
            if (len == 0)
                return result;

            int elementSize = IntPtr.Size * 4;
            ulong dataPtr = _stackTrace + (ulong)(IntPtr.Size * 2);
            ulong count = 0;
            if (!ReadPointer(dataPtr, out count))
                return result;

            // Skip size and header
            dataPtr += (ulong)(IntPtr.Size * 2);

            for (int i = 0; i < (int)count; ++i)
            {
                ulong ip, sp, md;
                if (!ReadPointer(dataPtr, out ip))
                    break;
                if (!ReadPointer(dataPtr + (ulong)IntPtr.Size, out sp))
                    break;
                if (!ReadPointer(dataPtr + (ulong)(2 * IntPtr.Size), out md))
                    break;

                string method = GetNameForMD(md);
                result.Add(new DesktopStackFrame(this, ip, sp, method));

                dataPtr += (ulong)elementSize;
            }

            return result;
        }

        internal override IThreadStoreData GetThreadStoreData()
        {
            LegacyThreadStoreData data;
            if (m_sos.GetThreadStoreData(out data) < 0)
                return null;

            return data;
        }

        internal override string GetAppBase(ulong appDomain)
        {
            uint needed;
            if (m_sos.GetApplicationBase(appDomain, 0, null, out needed) < 0)
                return null;

            StringBuilder builder = new StringBuilder((int)needed);
            if (m_sos.GetApplicationBase(appDomain, (int)needed, builder, out needed) < 0)
                return null;

            return builder.ToString();
        }

        internal override string GetConfigFile(ulong appDomain)
        {
            uint needed;
            if (m_sos.GetAppDomainConfigFile(appDomain, 0, null, out needed) < 0)
                return null;

            StringBuilder builder = new StringBuilder((int)needed);
            if (m_sos.GetAppDomainConfigFile(appDomain, (int)needed, builder, out needed) < 0)
                return null;

            return builder.ToString();
        }

        internal override IMethodDescData GetMDForIP(ulong ip)
        {
            ulong md;
            if (m_sos.GetMethodDescPtrFromIP(ip, out md) < 0 || md == 0)
                return null;

            V45MethodDescDataWrapper mdWrapper = new V45MethodDescDataWrapper();
            if (!mdWrapper.Init(m_sos, md))
                return null;

            return mdWrapper;
        }

        protected override ulong GetThreadFromThinlock(uint threadId)
        {
            ulong thread;
            if (m_sos.GetThreadFromThinlockID(threadId, out thread) < 0)
                return 0;

            return thread;
        }

        internal override int GetSyncblkCount()
        {
            LegacySyncBlkData data;
            if (m_sos.GetSyncBlockData(1, out data) < 0)
                return 0;

            return (int)data.TotalCount;
        }

        internal override ISyncBlkData GetSyncblkData(int index)
        {
            LegacySyncBlkData data;
            if (m_sos.GetSyncBlockData((uint)index + 1, out data) < 0)
                return null;

            return data;
        }

        internal override IThreadPoolData GetThreadPoolData()
        {
            V45ThreadPoolData data;
            if (m_sos.GetThreadpoolData(out data) < 0)
                return null;

            return data;
        }

        internal override uint GetTlsSlot()
        {
            uint result = 0;
            if (m_sos.GetTLSIndex(out result) < 0)
                return uint.MaxValue;

            return result;
        }

        internal override uint GetThreadTypeIndex()
        {
            return 11;
        }

        protected override uint GetRWLockDataOffset()
        {
            if (PointerSize == 8)
                return 0x30;
            else
                return 0x18;
        }

        internal override IEnumerable<NativeWorkItem> EnumerateWorkItems()
        {
            V45ThreadPoolData data;
            if (m_sos.GetThreadpoolData(out data) == 0)
            {
                ulong request = data.FirstWorkRequest;
                while (request != 0)
                {
                    V45WorkRequestData requestData;
                    if (m_sos.GetWorkRequestData(request, out requestData) != 0)
                        break;

                    yield return new DesktopNativeWorkItem(requestData);
                    request = requestData.NextWorkRequest;
                }
            }
        }

        internal override uint GetStringFirstCharOffset()
        {
            if (PointerSize == 8)
                return 0xc;

            return 8;
        }

        internal override uint GetStringLengthOffset()
        {
            if (PointerSize == 8)
                return 0x8;

            return 0x4;
        }

        internal override uint GetExceptionHROffset()
        {
            return PointerSize == 8 ? 0x8cu : 0x40u;
        }
    }



    #region V45 Dac Interface

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("3E269830-4A2B-4301-8EE2-D6805B29B2FA")]
    interface ISOSHandleEnum
    {
        void Skip(uint count);
        void Reset();
        void GetCount(out uint count);
        [PreserveSig]
        int Next(uint count, [Out, MarshalAs(UnmanagedType.LPArray)] HandleData[] handles, out uint pNeeded);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("8FA642BD-9F10-4799-9AA3-512AE78C77EE")]
    interface ISOSStackRefEnum
    {
        void Skip(uint count);
        void Reset();
        void GetCount(out uint count);
        [PreserveSig]
        int Next(uint count, [Out, MarshalAs(UnmanagedType.LPArray)] StackRefData[] handles, out uint pNeeded);
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void ModuleMapTraverse(uint index, ulong methodTable, IntPtr token);

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("436f00f2-b42a-4b9f-870c-e73db66ae930")]
    interface ISOSDac
    {
        // ThreadStore
        [PreserveSig]
        int GetThreadStoreData(out LegacyThreadStoreData data);

        // AppDomains
        [PreserveSig]
        int GetAppDomainStoreData(out LegacyAppDomainStoreData data);
        [PreserveSig]
        int GetAppDomainList(uint count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)]ulong[] values, out uint pNeeded);
        [PreserveSig]
        int GetAppDomainData(ulong addr, out LegacyAppDomainData data);
        [PreserveSig]
        int GetAppDomainName(ulong addr, uint count, [Out]StringBuilder lpFilename, out uint pNeeded);
        [PreserveSig]
        int GetDomainFromContext(ulong context, out ulong domain);

        // Assemblies
        [PreserveSig]
        int GetAssemblyList(ulong appDomain, int count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] ulong[] values, out int pNeeded);
        [PreserveSig]
        int GetAssemblyData(ulong baseDomainPtr, ulong assembly, out LegacyAssemblyData data);
        [PreserveSig]
        int GetAssemblyName(ulong assembly, uint count, [Out] StringBuilder name, out uint pNeeded);

        // Modules
        [PreserveSig]
        int GetModule(ulong addr, [Out, MarshalAs(UnmanagedType.IUnknown)] out object module);
        [PreserveSig]
        int GetModuleData(ulong moduleAddr, out V45ModuleData data);
        [PreserveSig]
        int TraverseModuleMap(int mmt, ulong moduleAddr, [In, MarshalAs(UnmanagedType.FunctionPtr)] ModuleMapTraverse pCallback, IntPtr token);
        [PreserveSig]
        int GetAssemblyModuleList(ulong assembly, uint count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] ulong[] modules, out uint pNeeded);
        [PreserveSig]
        int GetILForModule(ulong moduleAddr, uint rva, out ulong il);

        // Threads
        [PreserveSig]
        int GetThreadData(ulong thread, out V4ThreadData data);
        [PreserveSig]
        int GetThreadFromThinlockID(uint thinLockId, out ulong pThread);
        [PreserveSig]
        int GetStackLimits(ulong threadPtr, out ulong lower, out ulong upper, out ulong fp);

        // MethodDescs
        [PreserveSig]
        int GetMethodDescData(ulong methodDesc, ulong ip, out V45MethodDescData data, uint cRevertedRejitVersions, V45ReJitData[] rgRevertedRejitData, out ulong pcNeededRevertedRejitData);
        [PreserveSig]
        int GetMethodDescPtrFromIP(ulong ip, out ulong ppMD);
        [PreserveSig]
        int GetMethodDescName(ulong methodDesc, uint count, [Out] StringBuilder name, out uint pNeeded);
        [PreserveSig]
        int GetMethodDescPtrFromFrame(ulong frameAddr, out ulong ppMD);
        [PreserveSig]
        int GetMethodDescFromToken(ulong moduleAddr, uint token, out ulong methodDesc);
        [PreserveSig]
        int GetMethodDescTransparencyData_do_not_use();//(ulong methodDesc, out DacpMethodDescTransparencyData data);

        // JIT Data
        [PreserveSig]
        int GetCodeHeaderData(ulong ip, out CodeHeaderData data);
        [PreserveSig]
        int GetJitManagerList(uint count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] LegacyJitManagerInfo[] jitManagers, out uint pNeeded);
        [PreserveSig]
        int GetJitHelperFunctionName(ulong ip, uint count, char name, out uint pNeeded);
        [PreserveSig]
        int GetJumpThunkTarget_do_not_use(uint ctx, out ulong targetIP, out ulong targetMD);

        // ThreadPool
        [PreserveSig]
        int GetThreadpoolData(out V45ThreadPoolData data);
        [PreserveSig]
        int GetWorkRequestData(ulong addrWorkRequest, out V45WorkRequestData data);
        [PreserveSig]
        int GetHillClimbingLogEntry_do_not_use(); //(ulong addr, out DacpHillClimbingLogEntry data);

        // Objects
        [PreserveSig]
        int GetObjectData(ulong objAddr, out V45ObjectData data);
        [PreserveSig]
        int GetObjectStringData(ulong obj, uint count, [Out] StringBuilder stringData, out uint pNeeded);
        [PreserveSig]
        int GetObjectClassName(ulong obj, uint count, [Out] StringBuilder className, out uint pNeeded);

        // MethodTable
        [PreserveSig]
        int GetMethodTableName(ulong mt, uint count, [Out] StringBuilder mtName, out uint pNeeded);
        [PreserveSig]
        int GetMethodTableData(ulong mt, out V45MethodTableData data);
        [PreserveSig]
        int GetMethodTableSlot(ulong mt, uint slot, out ulong value);
        [PreserveSig]
        int GetMethodTableFieldData(ulong mt, out V4FieldInfo data);
        [PreserveSig]
        int GetMethodTableTransparencyData_do_not_use(); //(ulong mt, out DacpMethodTableTransparencyData data);

        // EEClass
        [PreserveSig]
        int GetMethodTableForEEClass(ulong eeClass, out ulong value);

        // FieldDesc
        [PreserveSig]
        int GetFieldDescData(ulong fieldDesc, out LegacyFieldData data);

        // Frames
        [PreserveSig]
        int GetFrameName(ulong vtable, uint count, [Out] StringBuilder frameName, out uint pNeeded);


        // PEFiles
        [PreserveSig]
        int GetPEFileBase(ulong addr, [Out] out ulong baseAddr);

        [PreserveSig]
        int GetPEFileName(ulong addr, uint count, [Out] StringBuilder ptr, [Out] out uint pNeeded);

        // GC
        [PreserveSig]
        int GetGCHeapData(out LegacyGCInfo data);
        [PreserveSig]
        int GetGCHeapList(uint count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] ulong[] heaps, out uint pNeeded); // svr only
        [PreserveSig]
        int GetGCHeapDetails(ulong heap, out V4HeapDetails details); // wks only
        [PreserveSig]
        int GetGCHeapStaticData(out V4HeapDetails data);
        [PreserveSig]
        int GetHeapSegmentData(ulong seg, out V4SegmentData data);
        [PreserveSig]
        int GetOOMData_do_not_use(); //(ulong oomAddr, out DacpOomData data);
        [PreserveSig]
        int GetOOMStaticData_do_not_use(); //(out DacpOomData data);
        [PreserveSig]
        int GetHeapAnalyzeData_do_not_use(); //(ulong addr, out  DacpGcHeapAnalyzeData data);
        [PreserveSig]
        int GetHeapAnalyzeStaticData_do_not_use(); //(out DacpGcHeapAnalyzeData data);

        // DomainLocal
        [PreserveSig]
        int GetDomainLocalModuleData_do_not_use(); //(ulong addr, out DacpDomainLocalModuleData data);
        [PreserveSig]
        int GetDomainLocalModuleDataFromAppDomain(ulong appDomainAddr, int moduleID, out V45DomainLocalModuleData data);
        [PreserveSig]
        int GetDomainLocalModuleDataFromModule(ulong moduleAddr, out V45DomainLocalModuleData data);

        // ThreadLocal
        [PreserveSig]
        int GetThreadLocalModuleData(ulong thread, uint index, out V45ThreadLocalModuleData data);

        // SyncBlock
        [PreserveSig]
        int GetSyncBlockData(uint number, out LegacySyncBlkData data);
        [PreserveSig]
        int GetSyncBlockCleanupData_do_not_use(); //(ulong addr, out DacpSyncBlockCleanupData data);

        // Handles
        [PreserveSig]
        int GetHandleEnum([Out, MarshalAs(UnmanagedType.IUnknown)] out object ppHandleEnum);
        [PreserveSig]
        int GetHandleEnumForTypes([In] uint[] types, uint count, [Out, MarshalAs(UnmanagedType.IUnknown)] out object ppHandleEnum);
        [PreserveSig]
        int GetHandleEnumForGC(uint gen, [Out, MarshalAs(UnmanagedType.IUnknown)] out object ppHandleEnum);

        // EH
        [PreserveSig]
        int TraverseEHInfo_do_not_use(); //(ulong ip, DUMPEHINFO pCallback, IntPtr token);
        [PreserveSig]
        int GetNestedExceptionData(ulong exception, out ulong exceptionObject, out ulong nextNestedException);

        // StressLog
        [PreserveSig]
        int GetStressLogAddress(out ulong stressLog);

        // Heaps
        [PreserveSig]
        int TraverseLoaderHeap(ulong loaderHeapAddr, IntPtr pCallback);
        [PreserveSig]
        int GetCodeHeapList(ulong jitManager, uint count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] LegacyJitCodeHeapInfo[] codeHeaps, out uint pNeeded);
        [PreserveSig]
        int TraverseVirtCallStubHeap(ulong pAppDomain, uint heaptype, IntPtr pCallback);

        // Other
        [PreserveSig]
        int GetUsefulGlobals(out CommonMethodTables data);
        [PreserveSig]
        int GetClrWatsonBuckets(ulong thread, out IntPtr pGenericModeBlock);
        [PreserveSig]
        int GetTLSIndex(out uint pIndex);
        [PreserveSig]
        int GetDacModuleHandle(out IntPtr phModule);

        // COM
        [PreserveSig]
        int GetRCWData(ulong addr, out V45RCWData data);
        [PreserveSig]
        int GetRCWInterfaces(ulong rcw, uint count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] COMInterfacePointerData[] interfaces, out uint pNeeded);
        [PreserveSig]
        int GetCCWData(ulong ccw, out V45CCWData data);
        [PreserveSig]
        int GetCCWInterfaces(ulong ccw, uint count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] COMInterfacePointerData[] interfaces, out uint pNeeded);
        [PreserveSig]
        int TraverseRCWCleanupList_do_not_use(); //(ulong cleanupListPtr, VISITRCWFORCLEANUP pCallback, LPVOID token);

        // GC Reference Functions
        [PreserveSig]
        int GetStackReferences(uint osThreadID, [Out, MarshalAs(UnmanagedType.IUnknown)] out object ppEnum);
        [PreserveSig]
        int GetRegisterName(int regName, uint count, [Out] StringBuilder buffer, out uint pNeeded);


        [PreserveSig]
        int GetThreadAllocData(ulong thread, ref V45AllocData data);
        [PreserveSig]
        int GetHeapAllocData(uint count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] V45GenerationAllocData[] data, out uint pNeeded);

        // For BindingDisplay plugin
        [PreserveSig]
        int GetFailedAssemblyList(ulong appDomain, int count, ulong[] values, out uint pNeeded);
        [PreserveSig]
        int GetPrivateBinPaths(ulong appDomain, int count, [Out] StringBuilder paths, out uint pNeeded);
        [PreserveSig]
        int GetAssemblyLocation(ulong assembly, int count, [Out] StringBuilder location, out uint pNeeded);
        [PreserveSig]
        int GetAppDomainConfigFile(ulong appDomain, int count, [Out] StringBuilder configFile, out uint pNeeded);
        [PreserveSig]
        int GetApplicationBase(ulong appDomain, int count, [Out] StringBuilder appBase, out uint pNeeded);
        [PreserveSig]
        int GetFailedAssemblyData(ulong assembly, out uint pContext, out int pResult);
        [PreserveSig]
        int GetFailedAssemblyLocation(ulong assesmbly, uint count, [Out] StringBuilder location, out uint pNeeded);
        [PreserveSig]
        int GetFailedAssemblyDisplayName(ulong assembly, uint count, [Out] StringBuilder name, out uint pNeeded);
    }
    #endregion

#pragma warning disable 0649
#pragma warning disable 0169


    #region V45 Structs
    struct V45ThreadPoolData : IThreadPoolData
    {
        int cpuUtilization;
        int NumIdleWorkerThreads;
        int NumWorkingWorkerThreads;
        int NumRetiredWorkerThreads;
        int MinLimitTotalWorkerThreads;
        int MaxLimitTotalWorkerThreads;

        ulong FirstUnmanagedWorkRequest;

        ulong HillClimbingLog;
        int HillClimbingLogFirstIndex;
        int HillClimbingLogSize;

        int NumTimers;

        int NumCPThreads;
        int NumFreeCPThreads;
        int MaxFreeCPThreads;
        int NumRetiredCPThreads;
        int MaxLimitTotalCPThreads;
        int CurrentLimitTotalCPThreads;
        int MinLimitTotalCPThreads;

        ulong AsyncTimerCallbackCompletionFPtr;

        public int MinCP
        {
            get { return MinLimitTotalCPThreads; }
        }

        public int MaxCP
        {
            get { return MaxLimitTotalCPThreads; }
        }

        public int CPU
        {
            get { return cpuUtilization; }
        }

        public int NumFreeCP
        {
            get { return NumFreeCPThreads; }
        }

        public int MaxFreeCP
        {
            get { return MaxFreeCPThreads; }
        }

        public int TotalThreads
        {
            get { return NumIdleWorkerThreads + NumWorkingWorkerThreads + NumRetiredWorkerThreads; }
        }

        public int RunningThreads
        {
            get { return NumWorkingWorkerThreads; }
        }

        public int IdleThreads
        {
            get { return NumIdleWorkerThreads; }
        }

        public int MinThreads
        {
            get { return MinLimitTotalWorkerThreads; }
        }

        public int MaxThreads
        {
            get { return MaxLimitTotalWorkerThreads; }
        }


        public ulong FirstWorkRequest
        {
            get { return FirstUnmanagedWorkRequest; }
        }


        public ulong QueueUserWorkItemCallbackFPtr
        {
            get { return ulong.MaxValue; }
        }

        public ulong AsyncCallbackCompletionFPtr
        {
            get { return ulong.MaxValue; }
        }

        ulong IThreadPoolData.AsyncTimerCallbackCompletionFPtr
        {
            get { return ulong.MaxValue; }
        }
    }

    struct V45ThreadLocalModuleData
    {
        ulong threadAddr;
        ulong moduleIndex;

        ulong pClassData;
        ulong pDynamicClassTable;
        public ulong pGCStaticDataStart;
        public ulong pNonGCStaticDataStart;
    }

    struct V45DomainLocalModuleData : IDomainLocalModuleData
    {
        ulong appDomainAddr;
        ulong moduleID;

        ulong pClassData;
        ulong pDynamicClassTable;
        ulong pGCStaticDataStart;
        ulong pNonGCStaticDataStart;

        public ulong AppDomainAddr
        {
            get { return appDomainAddr; }
        }

        public ulong ModuleID
        {
            get { return moduleID; }
        }

        public ulong ClassData
        {
            get { return pClassData; }
        }

        public ulong DynamicClassTable
        {
            get { return pDynamicClassTable; }
        }

        public ulong GCStaticDataStart
        {
            get { return pGCStaticDataStart; }
        }

        public ulong NonGCStaticDataStart
        {
            get { return pNonGCStaticDataStart; }
        }
    }

    struct StackRefData
    {
        public uint HasRegisterInformation;
        public int Register;
        public int Offset;
        public ulong Address;
        public ulong Object;
        public uint Flags;

        public uint SourceType;
        public ulong Source;
        public ulong StackPointer;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct HandleData
    {
        public ulong AppDomain;
        public ulong Handle;
        public ulong Secondary;
        public uint Type;
        public uint StrongReference;

        // For RefCounted Handles
        public uint RefCount;
        public uint JupiterRefCount;
        public uint IsPegged;
    }

    struct V45ReJitData
    {
        ulong rejitID;
        uint flags;
        ulong NativeCodeAddr;
    }

    class V45MethodDescDataWrapper : IMethodDescData
    {
        public bool Init(ISOSDac sos, ulong md)
        {
            ulong count = 0;
            V45MethodDescData data = new V45MethodDescData();
            if (sos.GetMethodDescData(md, 0, out data, 0, null, out count) < 0)
                return false;


            m_md = data.MethodDescPtr;
            m_ip = data.NativeCodeAddr;
            m_module = data.ModulePtr;
            m_token = data.MDToken;
            m_mt = data.MethodTablePtr;

            CodeHeaderData header;
            if (sos.GetCodeHeaderData(data.NativeCodeAddr, out header) >= 0)
            {
                if (header.JITType == 1)
                    m_jitType = MethodCompilationType.Jit;
                else if (header.JITType == 2)
                    m_jitType = MethodCompilationType.Ngen;
                else
                    m_jitType = MethodCompilationType.None;
            }
            else
            {
                m_jitType = MethodCompilationType.None;
            }

            return true;
        }

        MethodCompilationType m_jitType;
        ulong m_md, m_module, m_ip;
        uint m_token;
        private ulong m_mt;

        public ulong MethodDesc
        {
            get { return m_md; }
        }

        public ulong Module
        {
            get { return m_module; }
        }

        public uint MDToken
        {
            get { return m_token; }
        }

        public ulong NativeCodeAddr
        {
            get { return m_ip; }
        }

        public MethodCompilationType JITType
        {
            get { return m_jitType; }
        }


        public ulong MethodTable
        {
            get { return m_mt; }
        }
    }

    struct V45MethodDescData
    {
        uint bHasNativeCode;
        uint bIsDynamic;
        short wSlotNumber;
        internal ulong NativeCodeAddr;
        // Useful for breaking when a method is jitted.
        ulong AddressOfNativeCodeSlot;

        internal ulong MethodDescPtr;
        internal ulong MethodTablePtr;
        internal ulong ModulePtr;

        internal uint MDToken;
        ulong GCInfo;
        ulong GCStressCodeCopy;

        // This is only valid if bIsDynamic is true
        ulong managedDynamicMethodObject;

        ulong requestedIP;

        // Gives info for the single currently active version of a method
        V45ReJitData rejitDataCurrent;

        // Gives info corresponding to requestedIP (for !ip2md)
        V45ReJitData rejitDataRequested;

        // Total number of rejit versions that have been jitted
        uint cJittedRejitVersions;
    }

    struct CodeHeaderData
    {
        public ulong GCInfo;
        public uint JITType;
        public ulong MethodDescPtr;
        public ulong MethodStart;
        public uint MethodSize;
        public ulong ColdRegionStart;
        public uint ColdRegionSize;
        public uint HotRegionSize;
    }

    struct V45ModuleData : IModuleData
    {
        public ulong address;
        public ulong peFile;
        public ulong ilBase;
        public ulong metadataStart;
        public ulong metadataSize;
        public ulong assembly;
        public uint bIsReflection;
        public uint bIsPEFile;
        public ulong dwBaseClassIndex;
        public ulong dwModuleID;
        public uint dwTransientFlags;
        public ulong TypeDefToMethodTableMap;
        public ulong TypeRefToMethodTableMap;
        public ulong MethodDefToDescMap;
        public ulong FieldDefToDescMap;
        public ulong MemberRefToDescMap;
        public ulong FileReferencesMap;
        public ulong ManifestModuleReferencesMap;
        public ulong pLookupTableHeap;
        public ulong pThunkHeap;
        public ulong dwModuleIndex;

        #region IModuleData
        public ulong Assembly
        {
            get
            {
                return assembly;
            }
        }

        public ulong PEFile
        {
            get
            {
                return (bIsPEFile == 0) ? ilBase : peFile;
            }
        }
        public ulong LookupTableHeap
        {
            get { return pLookupTableHeap; }
        }
        public ulong ThunkHeap
        {
            get { return pThunkHeap; }
        }


        public object LegacyMetaDataImport
        {
            get { return null; }
        }


        public ulong ModuleId
        {
            get { return dwModuleID; }
        }

        public ulong ModuleIndex
        {
            get { return dwModuleIndex; }
        }

        public bool IsReflection
        {
            get { return bIsReflection != 0; }
        }

        public bool IsPEFile
        {
            get { return bIsPEFile != 0; }
        }
        public ulong ImageBase
        {
            get { return ilBase; }
        }
        public ulong MetdataStart
        {
            get { return metadataStart; }
        }

        public ulong MetadataLength
        {
            get { return metadataSize; }
        }
        #endregion
    }

    struct V45ObjectData : IObjectData
    {
        ulong methodTable;
        uint ObjectType;
        ulong Size;
        ulong elementTypeHandle;
        uint elementType;
        uint dwRank;
        ulong dwNumComponents;
        ulong dwComponentSize;
        ulong ArrayDataPtr;
        ulong ArrayBoundsPtr;
        ulong ArrayLowerBoundsPtr;

        ulong rcw;
        ulong ccw;

        public ClrElementType ElementType { get { return (ClrElementType)elementType; } }
        public ulong ElementTypeHandle { get { return elementTypeHandle; } }
        public ulong RCW { get { return rcw; } }
        public ulong CCW { get { return ccw; } }

        public ulong DataPointer
        {
            get { return ArrayDataPtr; }
        }
    }

    struct V45MethodTableData : IMethodTableData
    {
        public uint bIsFree; // everything else is NULL if this is true.
        public ulong module;
        public ulong eeClass;
        public ulong parentMethodTable;
        public ushort wNumInterfaces;
        public ushort wNumMethods;
        public ushort wNumVtableSlots;
        public ushort wNumVirtuals;
        public uint baseSize;
        public uint componentSize;
        public uint token;
        public uint dwAttrClass;
        public uint isShared; // flags & enum_flag_DomainNeutral
        public uint isDynamic;
        public uint containsPointers;

        public bool ContainsPointers
        {
            get { return containsPointers != 0; }
        }

        public uint BaseSize
        {
            get { return baseSize; }
        }

        public uint ComponentSize
        {
            get { return componentSize; }
        }

        public ulong EEClass
        {
            get { return eeClass; }
        }

        public bool Free
        {
            get { return bIsFree != 0; }
        }

        public ulong Parent
        {
            get { return parentMethodTable; }
        }

        public bool Shared
        {
            get { return isShared != 0; }
        }


        public uint NumMethods
        {
            get { return wNumMethods; }
        }


        public ulong ElementTypeHandle
        {
            get { throw new NotImplementedException(); }
        }
    }

    struct V45CCWData : ICCWData
    {
        ulong outerIUnknown;
        ulong managedObject;
        ulong handle;
        ulong ccwAddress;

        int refCount;
        int interfaceCount;
        uint isNeutered;

        int jupiterRefCount;
        uint isPegged;
        uint isGlobalPegged;
        uint hasStrongRef;
        uint isExtendsCOMObject;
        uint hasWeakReference;
        uint isAggregated;

        public ulong IUnknown
        {
            get { return outerIUnknown; }
        }

        public ulong Object
        {
            get { return managedObject; }
        }

        public ulong Handle
        {
            get { return handle; }
        }

        public ulong CCWAddress
        {
            get { return ccwAddress; }
        }

        public int RefCount
        {
            get { return refCount; }
        }

        public int JupiterRefCount
        {
            get { return jupiterRefCount; }
        }

        public int InterfaceCount
        {
            get { return interfaceCount; }
        }
    }

    struct COMInterfacePointerData
    {
        public ulong MethodTable;
        public ulong InterfacePtr;
        public ulong ComContext;
    }

    struct V45RCWData : IRCWData
    {
        ulong identityPointer;
        ulong unknownPointer;
        ulong managedObject;
        ulong jupiterObject;
        ulong vtablePtr;
        ulong creatorThread;
        ulong ctxCookie;

        int refCount;
        int interfaceCount;

        uint isJupiterObject;
        uint supportsIInspectable;
        uint isAggregated;
        uint isContained;
        uint isFreeThreaded;
        uint isDisconnected;

        public ulong IdentityPointer
        {
            get { return identityPointer; }
        }

        public ulong UnknownPointer
        {
            get { return unknownPointer; }
        }

        public ulong ManagedObject
        {
            get { return managedObject; }
        }

        public ulong JupiterObject
        {
            get { return jupiterObject; }
        }

        public ulong VTablePtr
        {
            get { return vtablePtr; }
        }

        public ulong CreatorThread
        {
            get { return creatorThread; }
        }

        public int RefCount
        {
            get { return refCount; }
        }

        public int InterfaceCount
        {
            get { return interfaceCount; }
        }

        public bool IsJupiterObject
        {
            get { return isJupiterObject != 0; }
        }

        public bool IsDisconnected
        {
            get { return isDisconnected != 0; }
        }
    }


    struct V45WorkRequestData
    {
        public ulong Function;
        public ulong Context;
        public ulong NextWorkRequest;
    }

    #endregion

#pragma warning restore 0169
#pragma warning restore 0649
}
