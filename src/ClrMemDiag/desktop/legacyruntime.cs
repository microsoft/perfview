using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Diagnostics.Runtime;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    class LegacyRuntime : DesktopRuntimeBase
    {
        #region Fields
        // Buffer used for all name requests, this needs to be QUITE large because with anonymous types we can have
        // type names that are 8k+ long...
        byte[] s_buffer = new byte[1024 * 32];
        DesktopVersion m_version;
        int m_minor;
        #endregion

        #region Constructor
        public LegacyRuntime(DataTargetImpl dt, DacLibrary lib, DesktopVersion version, int minor)
            : base(dt, lib)
        {
            m_version = version;
            m_minor = minor;

            if (!GetCommonMethodTables(ref m_commonMTs))
                throw new ClrDiagnosticsException("Could not request common MethodTable list.", ClrDiagnosticsException.HR.DacError);

            // Ensure the version of the dac API matches the one we expect.  (Same for both
            // v2 and v4 rtm.)
            byte[] tmp = new byte[sizeof(int)];

            if (!Request(DacRequests.VERSION, null, tmp))
                throw new ClrDiagnosticsException("Failed to request dac version.", ClrDiagnosticsException.HR.DacError);

            int v = BitConverter.ToInt32(tmp, 0);
            if (v != 8)
                throw new ClrDiagnosticsException("Unsupported dac version.", ClrDiagnosticsException.HR.DacError);
        }

        protected override void InitApi()
        {
        }
        #endregion

        internal override DesktopVersion CLRVersion
        {
            get
            {
                return m_version;
            }
        }

        internal override ulong[] GetAssemblyList(ulong appDomain, int count)
        {
            return RequestAddrList(DacRequests.ASSEMBLY_LIST, appDomain, count);
        }

        internal override ulong[] GetModuleList(ulong assembly, int count)
        {
            return RequestAddrList(DacRequests.ASSEMBLYMODULE_LIST, assembly, count);
        }

        internal override IAssemblyData GetAssemblyData(ulong appDomain, ulong assembly)
        {
            if (assembly == 0)
                return null;

            return Request<IAssemblyData, LegacyAssemblyData>(DacRequests.ASSEMBLY_DATA, assembly);
        }

        public override IEnumerable<ClrHandle> EnumerateHandles()
        {
            HandleTableWalker handleTable = new HandleTableWalker(this);

            byte[] input = null;
            if (CLRVersion == DesktopVersion.v2)
                input = handleTable.V2Request;
            else
                input = handleTable.V4Request;

            // TODO:  Better to return partial data or null?  Maybe bool function return?
            //        I don't even think the dac api will fail unless there's a data read error.
            var ret = Request(DacRequests.HANDLETABLE_TRAVERSE, input, null);
            if (!ret)
                Trace.WriteLine("Warning, GetHandles() method failed, returning partial results.");

            return handleTable.Handles;
        }

        internal override bool TraverseHeap(ulong heap, DesktopRuntimeBase.LoaderHeapTraverse callback)
        {
            byte[] input = new byte[sizeof(ulong) * 2];
            WriteValueToBuffer(heap, input, 0);
            WriteValueToBuffer(Marshal.GetFunctionPointerForDelegate(callback), input, sizeof(ulong));

            return Request(DacRequests.LOADERHEAP_TRAVERSE, input, null);
        }

        internal override bool TraverseStubHeap(ulong appDomain, int type, LoaderHeapTraverse callback)
        {
            byte[] input;
            if (IntPtr.Size == 4)
                input = new byte[sizeof(ulong) * 2];
            else
                input = new byte[sizeof(ulong) * 3];

            WriteValueToBuffer(appDomain, input, 0);
            WriteValueToBuffer(type, input, sizeof(ulong));
            WriteValueToBuffer(Marshal.GetFunctionPointerForDelegate(callback), input, sizeof(ulong)+sizeof(int));

            return Request(DacRequests.VIRTCALLSTUBHEAP_TRAVERSE, input, null);
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

            byte[] input = new byte[2*sizeof(ulong)];
            Buffer.BlockCopy(BitConverter.GetBytes(addr), 0, input, 0, sizeof(ulong));

            if (CLRVersion == DesktopVersion.v2)
                return Request<IThreadData, V2ThreadData>(DacRequests.THREAD_DATA, input);
            
            return Request<IThreadData, V4ThreadData>(DacRequests.THREAD_DATA, input);
        }

        internal override IHeapDetails GetSvrHeapDetails(ulong addr)
        {
            if (CLRVersion == DesktopVersion.v2)
                return Request<IHeapDetails, V2HeapDetails>(DacRequests.GCHEAPDETAILS_DATA, addr);
            
            return Request<IHeapDetails, V4HeapDetails>(DacRequests.GCHEAPDETAILS_DATA, addr);
        }

        internal override IHeapDetails GetWksHeapDetails()
        {
            if (CLRVersion == DesktopVersion.v2)
                return Request<IHeapDetails, V2HeapDetails>(DacRequests.GCHEAPDETAILS_STATIC_DATA);

            return Request<IHeapDetails, V4HeapDetails>(DacRequests.GCHEAPDETAILS_STATIC_DATA);
        }

        internal override ulong[] GetServerHeapList()
        {
            return RequestAddrList(DacRequests.GCHEAP_LIST, HeapCount);
        }

        internal override IList<ulong> GetAppDomainList(int count)
        {
            return RequestAddrList(DacRequests.APPDOMAIN_LIST, count);
        }

        internal override IMethodTableData GetMethodTableData(ulong addr)
        {
            return Request<IMethodTableData, LegacyMethodTableData>(DacRequests.METHODTABLE_DATA, addr);
        }

        internal override IGCInfo GetGCInfo()
        {
            return Request<IGCInfo, LegacyGCInfo>(DacRequests.GCHEAP_DATA);
        }

        internal override ISegmentData GetSegmentData(ulong segmentAddr)
        {
            if (CLRVersion == DesktopVersion.v2)
                return Request<ISegmentData, V2SegmentData>(DacRequests.HEAPSEGMENT_DATA, segmentAddr);

            return Request<ISegmentData, V4SegmentData>(DacRequests.HEAPSEGMENT_DATA, segmentAddr);
        }

        internal override string GetAppDomaminName(ulong addr)
        {
            if (addr == 0)
                return null;

            ClearBuffer();
            if (!Request(DacRequests.APPDOMAIN_NAME, addr, s_buffer))
                return null;

            return BytesToString(s_buffer);
        }

        private void ClearBuffer()
        {
            s_buffer[0] = 0;
            s_buffer[1] = 0;
        }

        internal override string GetAssemblyName(ulong addr)
        {
            if (addr == 0)
                return null;

            // todo: should this be ASSEMBLY_DISPLAY_NAME?
            ClearBuffer();
            if (!Request(DacRequests.ASSEMBLY_NAME, addr, s_buffer))
                return null;

            return BytesToString(s_buffer);
        }

        internal override IAppDomainStoreData GetAppDomainStoreData()
        {
            return Request<IAppDomainStoreData, LegacyAppDomainStoreData>(DacRequests.APPDOMAIN_STORE_DATA);
        }

        internal override IAppDomainData GetAppDomainData(ulong addr)
        {
            return Request<IAppDomainData, LegacyAppDomainData>(DacRequests.APPDOMAIN_DATA, addr);
        }

        internal override bool GetCommonMethodTables(ref CommonMethodTables mCommonMTs)
        {
            return RequestStruct<CommonMethodTables>(DacRequests.USEFULGLOBALS, ref mCommonMTs);
        }

        internal override string GetNameForMT(ulong mt)
        {
            ClearBuffer();
            if (!Request(DacRequests.METHODTABLE_NAME, mt, s_buffer))
                return null;

            return BytesToString(s_buffer);
        }

        internal override string GetPEFileName(ulong addr)
        {
            if (addr == 0)
                return null;

            ClearBuffer();
            if (!Request(DacRequests.PEFILE_NAME, addr, s_buffer))
                return null;

            return BytesToString(s_buffer);
        }

        internal override IModuleData GetModuleData(ulong addr)
        {
            if (addr == 0)
                return null;

            if (CLRVersion == DesktopVersion.v2)
                return Request<IModuleData, V2ModuleData>(DacRequests.MODULE_DATA, addr);

            return Request<IModuleData, V4ModuleData>(DacRequests.MODULE_DATA, addr);
        }

        internal override ulong GetModuleForMT(ulong mt)
        {
            if (mt == 0)
                return 0;

            IMethodTableData mtData = GetMethodTableData(mt);
            if (mtData == null)
                return 0;

            IEEClassData classData;
            if (CLRVersion == DesktopVersion.v2)
                classData = Request<IEEClassData, V2EEClassData>(DacRequests.EECLASS_DATA, mtData.EEClass);
            else
                classData = Request<IEEClassData, V4EEClassData>(DacRequests.EECLASS_DATA, mtData.EEClass);

            if (classData == null)
                return 0;

            return classData.Module;
        }

        internal override IEnumerable<ICodeHeap> EnumerateJitHeaps()
        {
            byte[] output = new byte[sizeof(int)];
            if (Request(DacRequests.JITLIST, null, output))
            {
                int JitManagerSize = Marshal.SizeOf(typeof(LegacyJitManagerInfo));
                int count = BitConverter.ToInt32(output, 0);
                int size = JitManagerSize * count;

                if (size > 0)
                {
                    output = new byte[size];
                    if (Request(DacRequests.MANAGER_LIST, null, output))
                    {
                        LegacyJitCodeHeapInfo heapInfo = new LegacyJitCodeHeapInfo();
                        int CodeHeapTypeOffset = Marshal.OffsetOf(typeof(LegacyJitCodeHeapInfo), "codeHeapType").ToInt32();
                        int AddressOffset = Marshal.OffsetOf(typeof(LegacyJitCodeHeapInfo), "address").ToInt32();
                        int CurrAddrOffset = Marshal.OffsetOf(typeof(LegacyJitCodeHeapInfo), "currentAddr").ToInt32();
                        int JitCodeHeapInfoSize = Marshal.SizeOf(typeof(LegacyJitCodeHeapInfo));

                        for (int i = 0; i < count; ++i)
                        {
                            int type = BitConverter.ToInt32(output, i * JitManagerSize + sizeof(ulong));

                            // Is this code heap IL?
                            if ((type & 3) != 0)
                                continue;

                            ulong address = BitConverter.ToUInt64(output, i * JitManagerSize);
                            byte[] jitManagerBuffer = new byte[sizeof(ulong) * 2];
                            WriteValueToBuffer(address, jitManagerBuffer, 0);

                            if (Request(DacRequests.JITHEAPLIST, jitManagerBuffer, jitManagerBuffer))
                            {
                                int heapCount = BitConverter.ToInt32(jitManagerBuffer, sizeof(ulong));

                                byte[] codeHeapBuffer = new byte[heapCount * JitCodeHeapInfoSize];
                                if (Request(DacRequests.CODEHEAP_LIST, jitManagerBuffer, codeHeapBuffer))
                                {
                                    for (int j = 0; j < heapCount; ++j)
                                    {
                                        heapInfo.address = BitConverter.ToUInt64(codeHeapBuffer, j * JitCodeHeapInfoSize + AddressOffset);
                                        heapInfo.codeHeapType = BitConverter.ToUInt32(codeHeapBuffer, j * JitCodeHeapInfoSize + CodeHeapTypeOffset);
                                        heapInfo.currentAddr = BitConverter.ToUInt64(codeHeapBuffer, j * JitCodeHeapInfoSize + CurrAddrOffset);

                                        yield return (ICodeHeap)heapInfo;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        internal override IFieldInfo GetFieldInfo(ulong mt)
        {
            IMethodTableData mtData = GetMethodTableData(mt);

            IFieldInfo fieldData;

            if (CLRVersion == DesktopVersion.v2)
                fieldData = Request<IFieldInfo, V2EEClassData>(DacRequests.EECLASS_DATA, mtData.EEClass);
            else
                fieldData = Request<IFieldInfo, V4EEClassData>(DacRequests.EECLASS_DATA, mtData.EEClass);

            return fieldData;
        }

        internal override IFieldData GetFieldData(ulong fieldDesc)
        {
            return Request<IFieldData, LegacyFieldData>(DacRequests.FIELDDESC_DATA, fieldDesc);
        }

        internal override IObjectData GetObjectData(ulong objRef)
        {
            return Request<IObjectData, LegacyObjectData>(DacRequests.OBJECT_DATA, objRef);
        }

        internal override IMetadata GetMetadataImport(ulong module)
        {
            IModuleData data = GetModuleData(module);

            if (data != null && data.LegacyMetaDataImport != null)
                return data.LegacyMetaDataImport as IMetadata;

            return null;
        }

        internal override ICCWData GetCCWData(ulong ccw)
        {
            // Not supported pre-v4.5.
            return null;
        }

        internal override IRCWData GetRCWData(ulong rcw)
        {
            // Not supported pre-v4.5.
            return null;
        }

        internal override COMInterfacePointerData[] GetCCWInterfaces(ulong ccw, int count)
        {
            return null;
        }

        internal override COMInterfacePointerData[] GetRCWInterfaces(ulong rcw, int count)
        {
            return null;
        }

        internal override IDomainLocalModuleData GetDomainLocalModule(ulong appDomain, ulong id)
        {
            byte[] inout = GetByteArrayForStruct<LegacyDomainLocalModuleData>();

            int i = WriteValueToBuffer(appDomain, inout, 0);
            i = WriteValueToBuffer(new IntPtr((long)id), inout, i);

            if (Request(DacRequests.DOMAINLOCALMODULEFROMAPPDOMAIN_DATA, null, inout))
                return ConvertStruct<IDomainLocalModuleData, LegacyDomainLocalModuleData>(inout);

            return null;
        }

        internal override IList<ulong> GetMethodTableList(ulong module)
        {
            List<ulong> mts = new List<ulong>();

            ModuleMapTraverse traverse = delegate(uint index, ulong mt, IntPtr token) { mts.Add(mt); };
            LegacyModuleMapTraverseArgs args = new LegacyModuleMapTraverseArgs();
            args.pCallback = Marshal.GetFunctionPointerForDelegate(traverse);
            args.module = module;

            // TODO:  Blah, theres got to be a better way to do this.
            byte[] input = GetByteArrayForStruct<LegacyModuleMapTraverseArgs>();
            IntPtr mem = Marshal.AllocHGlobal(input.Length);
            Marshal.StructureToPtr(args, mem, true);
            Marshal.Copy(mem, input, 0, input.Length);
            Marshal.FreeHGlobal(mem);

            bool r = Request(DacRequests.MODULEMAP_TRAVERSE, input, null);

            GC.KeepAlive(traverse);
            return mts;
        }


        internal override IDomainLocalModuleData GetDomainLocalModule(ulong module)
        {
            return Request<IDomainLocalModuleData, LegacyDomainLocalModuleData>(DacRequests.DOMAINLOCALMODULE_DATA_FROM_MODULE, module);
        }

        private ulong GetMethodDescFromIp(ulong ip)
        {
            if (ip == 0)
                return 0;

            IMethodDescData data = Request<IMethodDescData, V35MethodDescData>(DacRequests.METHODDESC_IP_DATA, ip);
            if (data == null)
                data = Request<IMethodDescData, V2MethodDescData>(DacRequests.METHODDESC_IP_DATA, ip);
            
            return data != null ? data.MethodDesc : 0;
        }


        internal override string GetNameForMD(ulong md)
        {
            ClearBuffer();
            if (!Request(DacRequests.METHODDESC_NAME, md, s_buffer))
                return "<Error>";

            return BytesToString(s_buffer);
        }

        internal override uint GetMetadataToken(ulong mt)
        {
            uint token = uint.MaxValue;

            IMethodTableData mtData = GetMethodTableData(mt);
            if (mtData != null)
            {
                byte[] buffer = null;
                if (CLRVersion == DesktopVersion.v2)
                    buffer = GetByteArrayForStruct<V2EEClassData>();
                else
                    buffer = GetByteArrayForStruct<V4EEClassData>();

                if (Request(DacRequests.EECLASS_DATA, mtData.EEClass, buffer))
                {
                    if (CLRVersion == DesktopVersion.v2)
                    {
                        GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                        V2EEClassData result = (V2EEClassData)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(V2EEClassData));
                        handle.Free();

                        token = result.token;
                    }
                    else
                    {
                        GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                        V4EEClassData result = (V4EEClassData)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(V4EEClassData));
                        handle.Free();

                        token = result.token;
                    }
                }
            }

            return token;
        }

        protected override DesktopStackFrame GetStackFrame(int res, ulong ip, ulong sp, ulong frameVtbl)
        {
            DesktopStackFrame frame;
            ClearBuffer();

            if (res >= 0 && frameVtbl != 0)
            {
                ClrMethod method = null;
                string frameName = "Unknown Frame";
                if (Request(DacRequests.FRAME_NAME, frameVtbl, s_buffer))
                    frameName = BytesToString(s_buffer);

                var mdData = GetMethodDescData(DacRequests.METHODDESC_FRAME_DATA, sp);
                if (mdData != null)
                    method = DesktopMethod.Create(this, mdData);

                frame = new DesktopStackFrame(this, sp, frameName, method);
            }
            else
            {
                ulong md = GetMethodDescFromIp(ip);
                string frameName = GetNameForMD(md);

                frame = new DesktopStackFrame(this, ip, sp, frameName);
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
            List<ClrStackFrame> result = new List<ClrStackFrame>();
            
            ulong _stackTrace;
            if (!GetStackTraceFromField(type, obj, out _stackTrace))
            {
                if (!ReadPointer(obj + GetStackTraceOffset(), out _stackTrace))
                    return result;
            }

            if (_stackTrace == 0)
                return result;

            var heap = TryGetHeap();
            ClrType stackTraceType = heap.GetObjectType(_stackTrace);

            if (stackTraceType == null)
                stackTraceType = heap.m_arrayType;

            if (!stackTraceType.IsArray)
                return result;
            
            int len = stackTraceType.GetArrayLength(_stackTrace);
            if (len == 0)
                return result;

            int elementSize = (CLRVersion == DesktopVersion.v2) ? IntPtr.Size * 4 : IntPtr.Size * 3;
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

        internal override IMethodDescData GetMethodDescData(ulong md)
        {
            return GetMethodDescData(DacRequests.METHODDESC_DATA, md);
        }

        internal override IList<ulong> GetMethodDescList(ulong methodTable)
        {
            IMethodTableData mtData = Request<IMethodTableData, LegacyMethodTableData>(DacRequests.METHODTABLE_DATA, methodTable);
            ulong[] values = new ulong[mtData.NumMethods];

            if (mtData.NumMethods == 0)
                return values;

            CodeHeaderData codeHeader = new CodeHeaderData();
            byte[] slotArgs = new byte[0x10];
            byte[] result = new byte[sizeof(ulong)];

            WriteValueToBuffer(methodTable, slotArgs, 0);
            for (int i = 0; i < mtData.NumMethods; ++i)
            {
                WriteValueToBuffer(i, slotArgs, sizeof(ulong));
                if (!Request(DacRequests.METHODTABLE_SLOT, slotArgs, result))
                    continue;

                ulong ip = BitConverter.ToUInt64(result, 0);

                if (!RequestStruct<CodeHeaderData>(DacRequests.CODEHEADER_DATA, ip, ref codeHeader))
                    continue;
                values[i] = codeHeader.MethodDescPtr;
            }

            return values;
        }

        internal override ulong GetThreadStaticPointer(ulong thread, ClrElementType type, uint offset, uint moduleId, bool shared)
        {
            // TODO
            return 0;
        }

        internal override IThreadStoreData GetThreadStoreData()
        {
            LegacyThreadStoreData threadStore = new LegacyThreadStoreData();
            if (!RequestStruct<LegacyThreadStoreData>(DacRequests.THREAD_STORE_DATA, ref threadStore))
                return null;

            return threadStore;
        }


        internal override string GetAppBase(ulong appDomain)
        {
            ClearBuffer();
            if (!Request(DacRequests.APPDOMAIN_APP_BASE, appDomain, s_buffer))
                return null;

            return BytesToString(s_buffer);
        }

        internal override string GetConfigFile(ulong appDomain)
        {
            ClearBuffer();
            if (!Request(DacRequests.APPDOMAIN_CONFIG_FILE, appDomain, s_buffer))
                return null;

            return BytesToString(s_buffer);
        }

        internal override IMethodDescData GetMDForIP(ulong ip)
        {
            return GetMethodDescData(DacRequests.METHODDESC_IP_DATA, ip);
        }


        internal override IEnumerable<NativeWorkItem> EnumerateWorkItems()
        {
            IThreadPoolData data = GetThreadPoolData();

            if (m_version == DesktopVersion.v2)
            {
                ulong curr = data.FirstWorkRequest;
                byte[] bytes = GetByteArrayForStruct<DacpWorkRequestData>();

                while (Request(DacRequests.WORKREQUEST_DATA, curr, bytes))
                {
                    GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                    DacpWorkRequestData result = (DacpWorkRequestData)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(DacpWorkRequestData));
                    handle.Free();

                    yield return new DesktopNativeWorkItem(result);

                    curr = result.NextWorkRequest;
                    if (curr == 0)
                        break;
                }

            }
        }

        private IMethodDescData GetMethodDescData(uint request_id, ulong addr)
        {
            if (addr == 0)
                return null;

            if (m_version == DesktopVersion.v4 || m_minor > 4016)
            {
                return Request<IMethodDescData, V35MethodDescData>(request_id, addr);
            }
            else if (m_minor < 3053)
            {
                return Request<IMethodDescData, V2MethodDescData>(request_id, addr);
            }
            else
            {
                // We aren't sure which version it is between 3053 and 4016, so we'll just do both.  Slow, but we
                // might not even encounter those versions in the wild.
                IMethodDescData data = Request<IMethodDescData, V35MethodDescData>(request_id, addr);
                if (data == null)
                    data = Request<IMethodDescData, V2MethodDescData>(request_id, addr);

                return data;
            }
        }

        protected override ulong GetThreadFromThinlock(uint threadId)
        {
            byte[] input = new byte[sizeof(uint)];
            WriteValueToBuffer(threadId, input, 0);

            byte[] output = new byte[sizeof(ulong)];
            if (!Request(DacRequests.THREAD_THINLOCK_DATA, input, output))
                return 0;

            return BitConverter.ToUInt64(output, 0);
        }

        internal override int GetSyncblkCount()
        {
            ISyncBlkData data = Request<ISyncBlkData, LegacySyncBlkData>(DacRequests.SYNCBLOCK_DATA, 1);
            if (data == null)
                return 0;

            return (int)data.TotalCount;
        }

        internal override ISyncBlkData GetSyncblkData(int index)
        {
            if (index < 0)
                return null;

            return Request<ISyncBlkData, LegacySyncBlkData>(DacRequests.SYNCBLOCK_DATA, (uint)index + 1);
        }

        internal override IThreadPoolData GetThreadPoolData()
        {
            if (m_version == DesktopVersion.v2)
                return Request<IThreadPoolData, V2ThreadPoolData>(DacRequests.THREADPOOL_DATA);
            else
                return Request<IThreadPoolData, V4ThreadPoolData>(DacRequests.THREADPOOL_DATA_2);
        }

        internal override uint GetTlsSlot()
        {
            byte []value = new byte[sizeof(uint)];
            if (!Request(DacRequests.CLRTLSDATA_INDEX, null, value))
                return uint.MaxValue;
            
            return BitConverter.ToUInt32(value, 0);
        }

        internal override uint GetThreadTypeIndex()
        {
            if (m_version == DesktopVersion.v2)
                return (PointerSize == 4) ? 12u : 13u;

            return 11;
        }

        protected override uint GetRWLockDataOffset()
        {
            if (PointerSize == 8)
                return 0x38;
            else
                return 0x24;
        }

        internal override uint GetStringFirstCharOffset()
        {
            if (PointerSize == 0x8)
                return 0x10;

            return 0xc;
        }

        internal override uint GetStringLengthOffset()
        {
            if (PointerSize == 8)
                return 0xc;

            return 8;
        }

        internal override uint GetExceptionHROffset()
        {
            return PointerSize == 8 ? 0x74u : 0x38u;
        }
    }


    #region Dac Requests
    class DacRequests
    {
        internal const uint VERSION = 0xe0000000U;
        internal const uint THREAD_STORE_DATA = 0xf0000000U;
        internal const uint APPDOMAIN_STORE_DATA = 0xf0000001U;
        internal const uint APPDOMAIN_LIST = 0xf0000002U;
        internal const uint APPDOMAIN_DATA = 0xf0000003U;
        internal const uint APPDOMAIN_NAME = 0xf0000004U;
        internal const uint APPDOMAIN_APP_BASE = 0xf0000005U;
        internal const uint APPDOMAIN_PRIVATE_BIN_PATHS = 0xf0000006U;
        internal const uint APPDOMAIN_CONFIG_FILE = 0xf0000007U;
        internal const uint ASSEMBLY_LIST = 0xf0000008U;
        internal const uint FAILED_ASSEMBLY_LIST = 0xf0000009U;
        internal const uint ASSEMBLY_DATA = 0xf000000aU;
        internal const uint ASSEMBLY_NAME = 0xf000000bU;
        internal const uint ASSEMBLY_DISPLAY_NAME = 0xf000000cU;
        internal const uint ASSEMBLY_LOCATION = 0xf000000dU;
        internal const uint FAILED_ASSEMBLY_DATA = 0xf000000eU;
        internal const uint FAILED_ASSEMBLY_DISPLAY_NAME = 0xf000000fU;
        internal const uint FAILED_ASSEMBLY_LOCATION = 0xf0000010U;
        internal const uint THREAD_DATA = 0xf0000011U;
        internal const uint THREAD_THINLOCK_DATA = 0xf0000012U;
        internal const uint CONTEXT_DATA = 0xf0000013U;
        internal const uint METHODDESC_DATA = 0xf0000014U;
        internal const uint METHODDESC_IP_DATA = 0xf0000015U;
        internal const uint METHODDESC_NAME = 0xf0000016U;
        internal const uint METHODDESC_FRAME_DATA = 0xf0000017U;
        internal const uint CODEHEADER_DATA = 0xf0000018U;
        internal const uint THREADPOOL_DATA = 0xf0000019U;
        internal const uint WORKREQUEST_DATA = 0xf000001aU;
        internal const uint OBJECT_DATA = 0xf000001bU;
        internal const uint FRAME_NAME = 0xf000001cU;
        internal const uint OBJECT_STRING_DATA = 0xf000001dU;
        internal const uint OBJECT_CLASS_NAME = 0xf000001eU;
        internal const uint METHODTABLE_NAME = 0xf000001fU;
        internal const uint METHODTABLE_DATA = 0xf0000020U;
        internal const uint EECLASS_DATA = 0xf0000021U;
        internal const uint FIELDDESC_DATA = 0xf0000022U;
        internal const uint MANAGEDSTATICADDR = 0xf0000023U;
        internal const uint MODULE_DATA = 0xf0000024U;
        internal const uint MODULEMAP_TRAVERSE = 0xf0000025U;
        internal const uint MODULETOKEN_DATA = 0xf0000026U;
        internal const uint PEFILE_DATA = 0xf0000027U;
        internal const uint PEFILE_NAME = 0xf0000028U;
        internal const uint ASSEMBLYMODULE_LIST = 0xf0000029U;
        internal const uint GCHEAP_DATA = 0xf000002aU;
        internal const uint GCHEAP_LIST = 0xf000002bU;
        internal const uint GCHEAPDETAILS_DATA = 0xf000002cU;
        internal const uint GCHEAPDETAILS_STATIC_DATA = 0xf000002dU;
        internal const uint HEAPSEGMENT_DATA = 0xf000002eU;
        internal const uint UNITTEST_DATA = 0xf000002fU;
        internal const uint ISSTUB = 0xf0000030U;
        internal const uint DOMAINLOCALMODULE_DATA = 0xf0000031U;
        internal const uint DOMAINLOCALMODULEFROMAPPDOMAIN_DATA = 0xf0000032U;
        internal const uint DOMAINLOCALMODULE_DATA_FROM_MODULE = 0xf0000033U;
        internal const uint SYNCBLOCK_DATA = 0xf0000034U;
        internal const uint SYNCBLOCK_CLEANUP_DATA = 0xf0000035U;
        internal const uint HANDLETABLE_TRAVERSE = 0xf0000036U;
        internal const uint RCWCLEANUP_TRAVERSE = 0xf0000037U;
        internal const uint EHINFO_TRAVERSE = 0xf0000038U;
        internal const uint STRESSLOG_DATA = 0xf0000039U;
        internal const uint JITLIST = 0xf000003aU;
        internal const uint JIT_HELPER_FUNCTION_NAME = 0xf000003bU;
        internal const uint JUMP_THUNK_TARGET = 0xf000003cU;
        internal const uint LOADERHEAP_TRAVERSE = 0xf000003dU;
        internal const uint MANAGER_LIST = 0xf000003eU;
        internal const uint JITHEAPLIST = 0xf000003fU;
        internal const uint CODEHEAP_LIST = 0xf0000040U;
        internal const uint METHODTABLE_SLOT = 0xf0000041U;
        internal const uint VIRTCALLSTUBHEAP_TRAVERSE = 0xf0000042U;
        internal const uint NESTEDEXCEPTION_DATA = 0xf0000043U;
        internal const uint USEFULGLOBALS = 0xf0000044U;
        internal const uint CLRTLSDATA_INDEX = 0xf0000045U;
        internal const uint MODULE_FINDIL = 0xf0000046U;
        internal const uint CLR_WATSON_BUCKETS = 0xf0000047U;
        internal const uint OOM_DATA = 0xf0000048U;
        internal const uint OOM_STATIC_DATA = 0xf0000049U;
        internal const uint GCHEAP_HEAPANALYZE_DATA = 0xf000004aU;
        internal const uint GCHEAP_HEAPANALYZE_STATIC_DATA = 0xf000004bU;
        internal const uint HANDLETABLE_FILTERED_TRAVERSE = 0xf000004cU;
        internal const uint METHODDESC_TRANSPARENCY_DATA = 0xf000004dU;
        internal const uint EECLASS_TRANSPARENCY_DATA = 0xf000004eU;
        internal const uint THREAD_STACK_BOUNDS = 0xf000004fU;
        internal const uint HILL_CLIMBING_LOG_ENTRY = 0xf0000050U;
        internal const uint THREADPOOL_DATA_2 = 0xf0000051U;
        internal const uint THREADLOCALMODULE_DAT = 0xf0000052U;
    }
    #endregion

#pragma warning disable 0649
#pragma warning disable 0169

    #region Common Dac Structs
    struct LegacySyncBlkData : ISyncBlkData
    {
        ulong pObject;
        uint bFree;
        ulong SyncBlockPointer;
        uint COMFlags;
        uint bMonitorHeld;
        uint nRecursion;
        ulong HoldingThread;
        uint AdditionalThreadCount;
        ulong appDomainPtr;
        uint SyncBlockCount;

        public bool Free
        {
            get { return bFree != 0; }
        }

        public ulong Object
        {
            get { return pObject; }
        }

        public bool MonitorHeld
        {
            get { return bMonitorHeld != 0; }
        }

        public uint Recursion
        {
            get { return nRecursion; }
        }

        public uint TotalCount
        {
            get { return SyncBlockCount; }
        }

        public ulong OwningThread
        {
            get { return HoldingThread; }
        }


        public ulong Address
        {
            get { return SyncBlockPointer; }
        }
    }

    // Same for v2 and v4
    [StructLayout(LayoutKind.Sequential)]
    struct LegacyModuleMapTraverseArgs
    {
        uint setToZero;
        public ulong module;
        public IntPtr pCallback;
        public IntPtr token;
    };


    struct V2MethodDescData : IMethodDescData
    {
        int bHasNativeCode;
        int bIsDynamic;
        short wSlotNumber;
        ulong NativeCodeAddr;
        // Useful for breaking when a method is jitted.
        ulong AddressOfNativeCodeSlot;

        ulong MethodDescPtr;
        ulong MethodTablePtr;
        ulong EEClassPtr;
        ulong ModulePtr;

        ulong PreStubAddr;
        uint mdToken;
        ulong GCInfo;
        short JITType;
        ulong GCStressCodeCopy;

        // This is only valid if bIsDynamic is true
        ulong managedDynamicMethodObject;

        public ulong MethodDesc
        {
            get { return MethodDescPtr; }
        }

        public ulong Module
        {
            get { return ModulePtr; }
        }

        public uint MDToken
        {
            get { return mdToken; }
        }


        ulong IMethodDescData.NativeCodeAddr
        {
            get { return NativeCodeAddr; }
        }

        MethodCompilationType IMethodDescData.JITType
        {
            get
            {
                if (JITType == 1)
                    return MethodCompilationType.Jit;
                else if (JITType == 2)
                    return MethodCompilationType.Ngen;
                return MethodCompilationType.None;
            }
        }


        public ulong MethodTable
        {
            get { return MethodTablePtr; }
        }
    }

    struct V35MethodDescData : IMethodDescData
    {
        int bHasNativeCode;
        int bIsDynamic;
        short wSlotNumber;
        ulong NativeCodeAddr;
        // Useful for breaking when a method is jitted.
        ulong AddressOfNativeCodeSlot;
    
        ulong MethodDescPtr;
        ulong MethodTablePtr;
        ulong EEClassPtr;
        ulong ModulePtr;
    
        uint mdToken;
        ulong GCInfo;
        short JITType;
        ulong GCStressCodeCopy;

        // This is only valid if bIsDynamic is true
        ulong managedDynamicMethodObject;

        public ulong MethodTable
        {
            get { return MethodTablePtr; }
        }

        public ulong MethodDesc
        {
            get { return MethodDescPtr; }
        }

        public ulong Module
        {
            get { return ModulePtr; }
        }

        public uint MDToken
        {
            get { return mdToken; }
        }


        ulong IMethodDescData.NativeCodeAddr
        {
            get { return NativeCodeAddr; }
        }

        MethodCompilationType IMethodDescData.JITType
        {
            get
            {
                if (JITType == 1)
                    return MethodCompilationType.Jit;
                else if (JITType == 2)
                    return MethodCompilationType.Ngen;
                return MethodCompilationType.None;
            }
        }
    }

    struct LegacyDomainLocalModuleData : IDomainLocalModuleData
    {
        ulong appDomainAddr;
        IntPtr moduleID;

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
            get { return (ulong)moduleID.ToInt64(); }
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


    struct LegacyObjectData : IObjectData
    {
        ulong eeClass;
        ulong methodTable;
        uint ObjectType;
        uint Size;
        ulong elementTypeHandle;
        uint elementType;
        uint dwRank;
        uint dwNumComponents;
        uint dwComponentSize;
        ulong ArrayDataPtr;
        ulong ArrayBoundsPtr;
        ulong ArrayLowerBoundsPtr;

        public ClrElementType ElementType { get { return (ClrElementType)elementType; } }
        public ulong ElementTypeHandle { get { return elementTypeHandle; } }
        public ulong RCW { get { return 0; } }
        public ulong CCW { get { return 0; } }

        public ulong DataPointer
        {
            get { return ArrayDataPtr; }
        }
    }

    struct LegacyMethodTableData : IMethodTableData
    {
        public uint bIsFree; // everything else is NULL if this is true.
        public ulong eeClass;
        public ulong parentMethodTable;
        public ushort wNumInterfaces;
        public ushort wNumVtableSlots;
        public uint baseSize;
        public uint componentSize;
        public uint isShared; // flags & enum_flag_DomainNeutral
        public uint sizeofMethodTable;
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
            get { return wNumVtableSlots; }
        }


        public ulong ElementTypeHandle
        {
            get { throw new NotImplementedException(); }
        }
    }

    // Same for v2 and v4
    struct LegacyGCInfo : IGCInfo
    {
        public int serverMode;
        public int gcStructuresValid;
        public uint heapCount;
        public uint maxGeneration;

        bool IGCInfo.ServerMode
        {
            get { return serverMode != 0; }
        }

        int IGCInfo.HeapCount
        {
            get { return (int)heapCount; }
        }

        int IGCInfo.MaxGeneration
        {
            get { return (int)maxGeneration; }
        }


        bool IGCInfo.GCStructuresValid
        {
            get { return gcStructuresValid != 0; }
        }
    }

    struct V4GenerationData
    {
        public ulong StartSegment;
        public ulong AllocationStart;

        // These are examined only for generation 0, otherwise NULL
        public ulong AllocContextPtr;
        public ulong AllocContextLimit;
    }

    struct LegacyJitCodeHeapInfo : ICodeHeap
    {
        public uint codeHeapType;
        public ulong address;
        public ulong currentAddr;

        public CodeHeapType Type
        {
            get { return (CodeHeapType)codeHeapType; }
        }

        public ulong Address
        {
            get { return address; }
        }
    }

    struct LegacyJitManagerInfo
    {
        public ulong addr;
        public CodeHeapType type;
        public ulong ptrHeapList;
    }

    // Same for both v2 and v4.
    struct LegacyAppDomainData : IAppDomainData
    {
        ulong address;
        ulong appSecDesc;
        ulong pLowFrequencyHeap;
        ulong pHighFrequencyHeap;
        ulong pStubHeap;
        ulong pDomainLocalBlock;
        ulong pDomainLocalModules;
        int dwId;
        int assemblyCount;
        int FailedAssemblyCount;
        int appDomainStage;

        public int Id
        {
            get { return dwId; }
        }

        public ulong Address
        {
            get { return address; }
        }

        public ulong LowFrequencyHeap { get { return pLowFrequencyHeap; } }
        public ulong HighFrequencyHeap { get { return pHighFrequencyHeap; } }
        public ulong StubHeap { get { return pStubHeap; } }
        public int AssemblyCount
        {
            get { return assemblyCount; }
        }
    }

    // Same for both v2 and v4.
    struct LegacyAppDomainStoreData : IAppDomainStoreData
    {
        ulong shared;
        ulong system;
        int domainCount;

        public ulong SharedDomain
        {
            get { return shared; }
        }

        public ulong SystemDomain
        {
            get { return system; }
        }

        public int Count
        {
            get { return domainCount; }
        }
    }

    struct LegacyAssemblyData : IAssemblyData
    {
        ulong AssemblyPtr;
        ulong ClassLoader;
        ulong parentDomain;
        ulong AppDomainPtr;
        ulong AssemblySecDesc;
        int isDynamic;
        int moduleCount;
        uint LoadContext;
        int isDomainNeutral;
        uint dwLocationFlags;

        public ulong Address
        {
            get { return AssemblyPtr; }
        }

        public ulong ParentDomain
        {
            get { return parentDomain; }
        }

        public ulong AppDomain
        {
            get { return AppDomainPtr; }
        }

        public bool IsDynamic
        {
            get { return isDynamic != 0; }
        }

        public bool IsDomainNeutral
        {
            get { return isDomainNeutral != 0; }
        }

        public int ModuleCount
        {
            get { return moduleCount; }
        }
    }

    struct LegacyThreadStoreData : IThreadStoreData
    {
        public int threadCount;
        public int unstartedThreadCount;
        public int backgroundThreadCount;
        public int pendingThreadCount;
        public int deadThreadCount;
        public ulong firstThread;
        public ulong finalizerThread;
        public ulong gcThread;
        public uint fHostConfig;          // Uses hosting flags defined above

        public ulong Finalizer
        {
            get { return finalizerThread; }
        }

        public int Count
        {
            get
            {
                return threadCount;
            }
        }

        public ulong FirstThread
        {
            get { return firstThread; }
        }
    }

    struct LegacyFieldData : IFieldData
    {
        uint type;      // CorElementType
        uint sigType;   // CorElementType
        ulong mtOfType; // NULL if Type is not loaded

        ulong moduleOfType;
        uint mdType;

        uint mdField;
        ulong MTOfEnclosingClass;
        uint dwOffset;
        uint bIsThreadLocal;
        uint bIsContextLocal;
        uint bIsStatic;
        ulong nextField;

        public uint CorElementType
        {
            get { return type; }
        }

        public uint SigType
        {
            get { return sigType; }
        }

        public ulong TypeMethodTable
        {
            get { return mtOfType; }
        }

        public ulong Module
        {
            get { return moduleOfType; }
        }

        public uint TypeToken
        {
            get { return mdType; }
        }

        public uint FieldToken
        {
            get { return mdField; }
        }

        public ulong EnclosingMethodTable
        {
            get { return MTOfEnclosingClass; }
        }

        public uint Offset
        {
            get { return dwOffset; }
        }

        public bool IsThreadLocal
        {
            get { return bIsThreadLocal != 0; }
        }

        bool IFieldData.bIsContextLocal
        {
            get { return bIsContextLocal != 0; }
        }

        bool IFieldData.bIsStatic
        {
            get { return bIsStatic != 0; }
        }

        ulong IFieldData.nextField
        {
            get { return nextField; }
        }
    }
    #endregion

    #region V2 Dac Data Structs


    enum WorkRequestFunctionTypes
    {
        QUEUEUSERWORKITEM,
        TIMERDELETEWORKITEM,
        ASYNCCALLBACKCOMPLETION,
        ASYNCTIMERCALLBACKCOMPLETION,
        UNKNOWNWORKITEM
    }
    struct DacpWorkRequestData
    {
        public WorkRequestFunctionTypes FunctionType;
        public ulong Function;
        public ulong Context;
        public ulong NextWorkRequest;
    }

    struct V2ThreadPoolData : IThreadPoolData
    {
        int cpuUtilization;
        int NumWorkerThreads;
        int MinLimitTotalWorkerThreads;
        int MaxLimitTotalWorkerThreads;
        int NumRunningWorkerThreads;
        int NumIdleWorkerThreads;
        int NumQueuedWorkRequests;

        ulong FirstWorkRequest;

        uint NumTimers;

        int NumCPThreads;
        int NumFreeCPThreads;
        int MaxFreeCPThreads;
        int NumRetiredCPThreads;
        int MaxLimitTotalCPThreads;
        int CurrentLimitTotalCPThreads;
        int MinLimitTotalCPThreads;

        ulong _QueueUserWorkItemCallbackFPtr;
        ulong _AsyncCallbackCompletionFPtr;
        ulong _AsyncTimerCallbackCompletionFPtr;



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
            get { return NumWorkerThreads; }
        }

        public int RunningThreads
        {
            get { return NumRunningWorkerThreads; }
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


        ulong IThreadPoolData.FirstWorkRequest
        {
            get { return FirstWorkRequest; }
        }


        public ulong QueueUserWorkItemCallbackFPtr
        {
            get { return _QueueUserWorkItemCallbackFPtr; }
        }

        public ulong AsyncCallbackCompletionFPtr
        {
            get { return _AsyncCallbackCompletionFPtr; }
        }

        public ulong AsyncTimerCallbackCompletionFPtr
        {
            get { return _AsyncTimerCallbackCompletionFPtr; }
        }
    }

    struct V2ModuleData : IModuleData
    {
        public ulong peFile;
        public ulong ilBase;
        public ulong metadataStart;
        public IntPtr metadataSize;
        public ulong assembly;
        public uint bIsReflection;
        public uint bIsPEFile;
        public IntPtr dwBaseClassIndex;
        [MarshalAs(UnmanagedType.IUnknown)]
        public object ModuleDefinition;
        public IntPtr dwDomainNeutralIndex;

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

        public ulong Assembly
        {
            get
            {
                return assembly;
            }
        }

        public ulong ImageBase
        {
            get
            {
                return ilBase;
            }
        }

        public ulong PEFile
        {
            get { return peFile; }
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
            get { return ModuleDefinition; }
        }


        public ulong ModuleId
        {
            get { return (ulong)dwDomainNeutralIndex.ToInt64(); }
        }


        public ulong ModuleIndex
        {
            get { return 0; }
        }

        public bool IsReflection
        {
            get { return bIsReflection != 0; }
        }

        public bool IsPEFile
        {
            get { return bIsPEFile != 0; }
        }


        public ulong MetdataStart
        {
            get { return metadataStart; }
        }

        public ulong MetadataLength
        {
            get { return (ulong)metadataSize.ToInt64(); }
        }
    }

    struct V2EEClassData : IEEClassData, IFieldInfo
    {
        public ulong methodTable;
        public ulong module;
        public short wNumVtableSlots;
        public short wNumMethodSlots;
        public short wNumInstanceFields;
        public short wNumStaticFields;
        public uint dwClassDomainNeutralIndex;
        public uint dwAttrClass; // cached metadata
        public uint token; // Metadata token    

        public ulong addrFirstField; // If non-null, you can retrieve more

        public short wThreadStaticOffset;
        public short wThreadStaticsSize;
        public short wContextStaticOffset;
        public short wContextStaticsSize;

        public ulong Module
        {
            get { return module; }
        }

        public ulong MethodTable
        {
            get { return methodTable; }
        }

        public uint InstanceFields
        {
            get { return (uint)wNumInstanceFields; }
        }

        public uint StaticFields
        {
            get { return (uint)wNumStaticFields; }
        }

        public uint ThreadStaticFields
        {
            get { return (uint)0; }
        }

        public ulong FirstField
        {
            get { return (ulong)addrFirstField; }
        }
    }

    struct V2ThreadData : IThreadData
    {
        public uint corThreadId;
        public uint osThreadId;
        public int state;
        public uint preemptiveGCDisabled;
        public ulong allocContextPtr;
        public ulong allocContextLimit;
        public ulong context;
        public ulong domain;
        public ulong sharedStaticData;
        public ulong unsharedStaticData;
        public ulong pFrame;
        public uint lockCount;
        public ulong firstNestedException;
        public ulong teb;
        public ulong fiberData;
        public ulong lastThrownObjectHandle;
        public ulong nextThread;

        public ulong Next
        {
            get { return IntPtr.Size == 8 ? nextThread : (ulong)(uint)nextThread; }
        }

        public ulong AllocPtr
        {
            get { return allocContextPtr; }
        }

        public ulong AllocLimit
        {
            get { return allocContextLimit; }
        }


        public uint OSThreadID
        {
            get { return osThreadId; }
        }

        public ulong Teb
        {
            get { return IntPtr.Size == 8 ? teb : (ulong)(uint)teb; }
        }


        public ulong AppDomain
        {
            get { return domain; }
        }

        public uint LockCount
        {
            get { return lockCount; }
        }

        public int State
        {
            get { return state; }
        }


        public ulong ExceptionPtr
        {
            get { return lastThrownObjectHandle; }
        }

        public uint ManagedThreadID
        {
            get { return corThreadId; }
        }


        public bool Preemptive
        {
            get { return preemptiveGCDisabled == 0; }
        }
    }


    struct V2SegmentData : ISegmentData
    {
        public ulong segmentAddr;
        public ulong allocated;
        public ulong committed;
        public ulong reserved;
        public ulong used;
        public ulong mem;
        public ulong next;
        public ulong gc_heap;
        public ulong highAllocMark;

        public ulong Address
        {
            get { return segmentAddr; }
        }

        public ulong Next
        {
            get { return next; }
        }

        public ulong Start
        {
            get { return mem; }
        }

        public ulong End
        {
            get { return allocated; }
        }

        public ulong Reserved
        {
            get { return reserved; }
        }

        public ulong Committed
        {
            get { return committed; }
        }
    }


    struct V2HeapDetails : IHeapDetails
    {
        public ulong heapAddr;
        public ulong alloc_allocated;

        public V4GenerationData generation_table0;
        public V4GenerationData generation_table1;
        public V4GenerationData generation_table2;
        public V4GenerationData generation_table3;
        public ulong ephemeral_heap_segment;
        public ulong finalization_fill_pointers0;
        public ulong finalization_fill_pointers1;
        public ulong finalization_fill_pointers2;
        public ulong finalization_fill_pointers3;
        public ulong finalization_fill_pointers4;
        public ulong finalization_fill_pointers5;
        public ulong lowest_address;
        public ulong highest_address;
        public ulong card_table;

        public ulong FirstHeapSegment
        {
            get { return generation_table2.StartSegment; }
        }

        public ulong FirstLargeHeapSegment
        {
            get { return generation_table3.StartSegment; }
        }

        public ulong EphemeralSegment
        {
            get { return ephemeral_heap_segment; }
        }

        public ulong EphemeralEnd { get { return alloc_allocated; } }


        public ulong EphemeralAllocContextPtr
        {
            get { return generation_table0.AllocContextPtr; }
        }

        public ulong EphemeralAllocContextLimit
        {
            get { return generation_table0.AllocContextLimit; }
        }


        public ulong FQStop
        {
            get { return finalization_fill_pointers5; }
        }

        public ulong FQStart
        {
            get { return finalization_fill_pointers2; }
        }

        public ulong FQLiveStart
        {
            get { return finalization_fill_pointers0; }
        }

        public ulong FQLiveEnd
        {
            get { return finalization_fill_pointers2; }
        }

        public ulong Gen0Start
        {
            get { return generation_table0.AllocationStart; }
        }

        public ulong Gen0Stop
        {
            get { return alloc_allocated; }
        }

        public ulong Gen1Start
        {
            get { return generation_table1.AllocationStart; }
        }

        public ulong Gen1Stop
        {
            get { return generation_table0.AllocationStart; }
        }

        public ulong Gen2Start
        {
            get { return generation_table2.AllocationStart; }
        }

        public ulong Gen2Stop
        {
            get { return generation_table1.AllocationStart; }
        }
    }

    #endregion

    #region V4 Dac Data Structs
    struct V4ThreadPoolData : IThreadPoolData
    {
        uint UseNewWorkerPool;

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

        uint NumTimers;

        int NumCPThreads;
        int NumFreeCPThreads;
        int MaxFreeCPThreads;
        int NumRetiredCPThreads;
        int MaxLimitTotalCPThreads;
        int CurrentLimitTotalCPThreads;
        int MinLimitTotalCPThreads;

        ulong QueueUserWorkItemCallbackFPtr;
        ulong AsyncCallbackCompletionFPtr;
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
            get { return NumWorkingWorkerThreads; }
        }

        public int RunningThreads
        {
            get { return NumWorkingWorkerThreads + NumIdleWorkerThreads + NumRetiredWorkerThreads; }
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


        ulong IThreadPoolData.QueueUserWorkItemCallbackFPtr
        {
            get { return ulong.MaxValue; }
        }

        ulong IThreadPoolData.AsyncCallbackCompletionFPtr
        {
            get { return ulong.MaxValue; }
        }

        ulong IThreadPoolData.AsyncTimerCallbackCompletionFPtr
        {
            get { return ulong.MaxValue; }
        }
    }

    struct V4EEClassData : IEEClassData, IFieldInfo
    {
        public ulong methodTable;
        public ulong module;
        public short wNumVtableSlots;
        public short wNumMethodSlots;
        public short wNumInstanceFields;
        public short wNumStaticFields;
        public short wNumThreadStaticFields;
        public uint dwClassDomainNeutralIndex;
        public uint dwAttrClass; // cached metadata
        public uint token; // Metadata token    

        public ulong addrFirstField; // If non-null, you can retrieve more

        public short wContextStaticOffset;
        public short wContextStaticsSize;

        public ulong Module
        {
            get { return module; }
        }

        ulong IEEClassData.MethodTable
        {
            get { return methodTable; }
        }

        public uint InstanceFields
        {
            get { return (uint)wNumInstanceFields; }
        }

        public uint StaticFields
        {
            get { return (uint)wNumStaticFields; }
        }

        public uint ThreadStaticFields
        {
            get { return (uint)0; }
        }

        public ulong FirstField
        {
            get { return addrFirstField; }
        }
    }

    struct V4ModuleData : IModuleData
    {
        public ulong peFile;
        public ulong ilBase;
        public ulong metadataStart;
        public IntPtr metadataSize;
        public ulong assembly;
        public uint bIsReflection;
        public uint bIsPEFile;
        public IntPtr dwBaseClassIndex;
        [MarshalAs(UnmanagedType.IUnknown)]
        public object ModuleDefinition;
        public IntPtr dwModuleID;

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

        public IntPtr dwModuleIndex;

        public ulong PEFile
        {
            get { return peFile; }
        }

        public ulong Assembly
        {
            get
            {
                return assembly;
            }
        }

        public ulong ImageBase
        {
            get { return ilBase; }
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
            get { return ModuleDefinition; }
        }


        public ulong ModuleId
        {
            get { return (ulong)dwModuleID.ToInt64(); }
        }

        public ulong ModuleIndex
        {
            get { return (ulong)dwModuleIndex.ToInt64(); }
        }

        public bool IsReflection
        {
            get { return bIsReflection != 0; }
        }

        public bool IsPEFile
        {
            get { return bIsPEFile != 0; }
        }


        public ulong MetdataStart
        {
            get { return metadataStart; }
        }

        public ulong MetadataLength
        {
            get { return (ulong)metadataSize.ToInt64(); }
        }
    }


    struct V4ThreadData : IThreadData
    {
        public uint corThreadId;
        public uint osThreadId;
        public int state;
        public uint preemptiveGCDisabled;
        public ulong allocContextPtr;
        public ulong allocContextLimit;
        public ulong context;
        public ulong domain;
        public ulong pFrame;
        public uint lockCount;
        public ulong firstNestedException;
        public ulong teb;
        public ulong fiberData;
        public ulong lastThrownObjectHandle;
        public ulong nextThread;

        public ulong Next
        {
            get { return IntPtr.Size == 8 ? nextThread : (ulong)(uint)nextThread; }
        }

        public ulong AllocPtr
        {
            get { return allocContextPtr; }
        }

        public ulong AllocLimit
        {
            get { return allocContextLimit; }
        }



        public uint OSThreadID
        {
            get { return osThreadId; }
        }

        public ulong Teb
        {
            get { return IntPtr.Size == 8 ? teb : (ulong)(uint)teb; }
        }


        public ulong AppDomain
        {
            get { return domain; }
        }

        public uint LockCount
        {
            get { return lockCount; }
        }

        public int State
        {
            get { return state; }
        }

        public ulong ExceptionPtr
        {
            get { return lastThrownObjectHandle; }
        }


        public uint ManagedThreadID
        {
            get { return corThreadId; }
        }


        public bool Preemptive
        {
            get { return preemptiveGCDisabled == 0; }
        }
    }

    struct V45AllocData
    {
        public ulong allocBytes;
        public ulong allocBytesLoh;
    }

    struct V45GenerationAllocData
    {
        public ulong allocBytesGen0;
        public ulong allocBytesLohGen0;
        public ulong allocBytesGen1;
        public ulong allocBytesLohGen1;
        public ulong allocBytesGen2;
        public ulong allocBytesLohGen2;
        public ulong allocBytesGen3;
        public ulong allocBytesLohGen3;
    }

    struct V4FieldInfo : IFieldInfo
    {
        short wNumInstanceFields;
        short wNumStaticFields;
        short wNumThreadStaticFields;

        ulong addrFirstField; // If non-null, you can retrieve more

        short wContextStaticOffset;
        short wContextStaticsSize;

        public uint InstanceFields
        {
            get { return (uint)wNumInstanceFields; }
        }

        public uint StaticFields
        {
            get { return (uint)wNumStaticFields; }
        }

        public uint ThreadStaticFields
        {
            get { return (uint)wNumThreadStaticFields; }
        }

        public ulong FirstField
        {
            get { return addrFirstField; }
        }
    }

    struct V4SegmentData : ISegmentData
    {
        public ulong segmentAddr;
        public ulong allocated;
        public ulong committed;
        public ulong reserved;
        public ulong used;
        public ulong mem;
        public ulong next;
        public ulong gc_heap;
        public ulong highAllocMark;
        public IntPtr flags;
        public ulong background_allocated;

        public ulong Address
        {
            get { return segmentAddr; }
        }

        public ulong Next
        {
            get { return next; }
        }

        public ulong Start
        {
            get { return mem; }
        }

        public ulong End
        {
            get { return allocated; }
        }

        public ulong Reserved
        {
            get { return reserved; }
        }

        public ulong Committed
        {
            get { return committed; }
        }
    }

    struct V4HeapDetails : IHeapDetails
    {
        public ulong heapAddr; // Only filled in in server mode, otherwise NULL

        public ulong alloc_allocated;
        public ulong mark_array;
        public ulong c_allocate_lh;
        public ulong next_sweep_obj;
        public ulong saved_sweep_ephemeral_seg;
        public ulong saved_sweep_ephemeral_start;
        public ulong background_saved_lowest_address;
        public ulong background_saved_highest_address;

        public V4GenerationData generation_table0;
        public V4GenerationData generation_table1;
        public V4GenerationData generation_table2;
        public V4GenerationData generation_table3;
        public ulong ephemeral_heap_segment;
        public ulong finalization_fill_pointers0;
        public ulong finalization_fill_pointers1;
        public ulong finalization_fill_pointers2;
        public ulong finalization_fill_pointers3;
        public ulong finalization_fill_pointers4;
        public ulong finalization_fill_pointers5;
        public ulong finalization_fill_pointers6;
        public ulong lowest_address;
        public ulong highest_address;
        public ulong card_table;

        public ulong FirstHeapSegment
        {
            get { return generation_table2.StartSegment; }
        }

        public ulong FirstLargeHeapSegment
        {
            get { return generation_table3.StartSegment; }
        }

        public ulong EphemeralSegment
        {
            get { return ephemeral_heap_segment; }
        }

        public ulong EphemeralEnd { get { return alloc_allocated; } }


        public ulong EphemeralAllocContextPtr
        {
            get { return generation_table0.AllocContextPtr; }
        }

        public ulong EphemeralAllocContextLimit
        {
            get { return generation_table0.AllocContextLimit; }
        }

        public ulong FQStop
        {
            get { return finalization_fill_pointers6; }
        }

        public ulong FQStart
        {
            get { return finalization_fill_pointers3; }
        }

        public ulong FQLiveStart
        {
            get { return finalization_fill_pointers0; }
        }

        public ulong FQLiveEnd
        {
            get { return finalization_fill_pointers3; }
        }

        public ulong Gen0Start
        {
            get { return generation_table0.AllocationStart; }
        }

        public ulong Gen0Stop
        {
            get { return alloc_allocated; }
        }

        public ulong Gen1Start
        {
            get { return generation_table1.AllocationStart; }
        }

        public ulong Gen1Stop
        {
            get { return generation_table0.AllocationStart; }
        }

        public ulong Gen2Start
        {
            get { return generation_table2.AllocationStart; }
        }

        public ulong Gen2Stop
        {
            get { return generation_table1.AllocationStart; }
        }
    }
    #endregion

#pragma warning restore 0169
#pragma warning restore 0649
}
