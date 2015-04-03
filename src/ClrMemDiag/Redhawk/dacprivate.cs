#if _REDHAWK
using Microsoft.Diagnostics.Runtime.Desktop;
using System;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Redhawk
{

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate void STATICROOTCALLBACK(IntPtr token, ulong addr, ulong obj, int pinned, int interior);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate void HANDLECALLBACK(IntPtr ptr, ulong HandleAddr, ulong DependentTarget, int HandleType, uint ulRefCount, int strong);

    
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate void THREADROOTCALLBACK(IntPtr token, ulong symbol, ulong address, ulong obj, int pinned, int interior);
    
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("90456375-3774-4c70-999a-a6fa78aab107")]
    interface ISOSRedhawk
    {
        // ThreadStore
        [PreserveSig] int Flush();
        [PreserveSig] int GetThreadStoreData(out RhThreadStoreData pData);
        [PreserveSig] int GetThreadAddress(ulong teb, out ulong pThread);
        [PreserveSig] int GetThreadData(ulong addr, out RhThreadData pThread);
        [PreserveSig] int GetCurrentExceptionObject(ulong thread, out ulong pExceptionRefAddress);

        [PreserveSig] int GetObjectData(ulong addr, out RhObjectData pData);
        [PreserveSig] int GetEETypeData(ulong addr, out RhMethodTableData pData);
    
        [PreserveSig] int GetGcHeapAnalyzeData_do_not_use();//(CLRDATA_ADDRESS addr, struct DacpGcHeapAnalyzeData *pData);
        [PreserveSig] int GetGCHeapData(out LegacyGCInfo pData);
        [PreserveSig] int GetGCHeapList(int count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] ulong[] heaps, out int pNeeded);
        [PreserveSig] int GetGCHeapDetails(ulong heap, out RhHeapDetails details);
        [PreserveSig] int GetGCHeapStaticData(out RhHeapDetails data);
        [PreserveSig] int GetGCHeapSegment(ulong segment, out RhSegementData pSegmentData);
        [PreserveSig] int GetFreeEEType(out ulong freeType);

        [PreserveSig] int DumpGCInfo_do_not_use(ulong codeAddr, IntPtr callback);
        [PreserveSig] int DumpEHInfo_do_not_use(ulong ehInfo, IntPtr symbolResolver, IntPtr callback);

        [PreserveSig] int DumpStackObjects(ulong threadAddr, IntPtr pCallback, IntPtr token);
        [PreserveSig] int TraverseStackRoots(ulong threadAddr, IntPtr pInitialContext, int initialContextSize, IntPtr pCallback, IntPtr token);
        [PreserveSig] int TraverseStaticRoots(IntPtr pCallback);
        [PreserveSig] int TraverseHandleTable(IntPtr pCallback, IntPtr token);
        [PreserveSig] int TraverseHandleTableFiltered(IntPtr pCallback, IntPtr token, int type, int gen);

        [PreserveSig] int GetCodeHeaderData(ulong ip, out RhCodeHeader pData);

        [PreserveSig] int GetModuleList(int count, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] ulong[] modules, out int pNeeded);

        
        [PreserveSig] int GetStressLogAddress_do_not_use();//(CLRDATA_ADDRESS *stressLog);
        [PreserveSig] int GetStressLogData_do_not_use();//(CLRDATA_ADDRESS addr, struct DacpStressLogData *pData);
        [PreserveSig] int EnumStressLogMessages_do_not_use();//(CLRDATA_ADDRESS addr, STRESSMSGCALLBACK smcb, ENDTHREADLOGCALLBACK etcb, void *token);
        [PreserveSig] int EnumStressLogMemRanges_do_not_use();//(CLRDATA_ADDRESS addr, STRESSLOGMEMRANGECALLBACK slmrcb, void* token);

        [PreserveSig] int UpdateDebugEventFilter_do_not_use();//(uint eventFilter);
        //HRESULT UpdateCurrentExceptionNotificationFrame(CLRDATA_ADDRESS pThread, CLRDATA_ADDRESS sp);
        //HRESULT EnumGcStressStatsInfo(GCSTRESSINFOCALLBACK cb, void* token);
    }

    struct RhCodeHeader
    {
        public ulong GCInfo;
        public ulong EHInfo;
        public ulong MethodStart;
        public uint MethodSize;
    }

    enum DacpObjectType
    {
        OBJ_FREE = 0,
        OBJ_OBJECT = 1,
        OBJ_VALUETYPE = 2,
        OBJ_ARRAY = 3,
        OBJ_OTHER = 4
    }

    struct RhObjectData
    {
        public ulong MethodTable;
        public DacpObjectType ObjectType;
        public uint Size;
        public ulong ElementTypeHandle;
        public uint ElementType;
        public uint dwRank;
        public uint dwNumComponents;
        public uint dwComponentSize;
        public ulong ArrayDataPtr;
        public ulong ArrayBoundsPtr;
        public ulong ArrayLowerBoundsPtr;
    }
    struct RhThreadStoreData : IThreadStoreData
    {
        public int threadCount;
        public ulong firstThread;
        public ulong finalizerThread;
        public ulong gcThread;

        public ulong Finalizer
        {
            get { return finalizerThread; }
        }

        public ulong FirstThread
        {
            get { return firstThread; }
        }

        public int Count
        {
            get { return threadCount; }
        }
    }

    struct RhThreadData : IThreadData
    {
        public uint osThreadId;
        public int state;
        public uint preemptiveGCDisabled;
        public ulong allocContextPtr;
        public ulong allocContextLimit;
        public ulong context;
        public ulong teb;
        public ulong nextThread;

        public ulong Next
        {
            get { return nextThread; }
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
            get { return teb; }
        }


        public ulong AppDomain
        {
            get { return 0; }
        }

        public uint LockCount
        {
            get { return 0; }
        }

        public int State
        {
            get { return state; }
        }

        public ulong ExceptionPtr
        {
            get { return 0; }
        }


        public uint ManagedThreadID
        {
            get { return osThreadId; }
        }


        public bool Preemptive
        {
            get { return preemptiveGCDisabled == 0; }
        }
    }

    struct RhMethodTableData : IMethodTableData
    {
        public uint objectType; // everything else is NULL if this is true.
        public ulong canonicalMethodTable;
        public ulong parentMethodTable;
        public ushort wNumInterfaces;
        public ushort wNumVtableSlots;
        public uint baseSize;
        public uint componentSize;
        public uint sizeofMethodTable;
        public uint containsPointers;
        public ulong elementTypeHandle;

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
            get { return canonicalMethodTable; }
        }

        public bool Free
        {
            get { return objectType == 0; }
        }

        public ulong Parent
        {
            get { return parentMethodTable; }
        }

        public bool Shared
        {
            get { return false; }
        }


        public uint NumMethods
        {
            get { return wNumVtableSlots; }
        }


        public ulong ElementTypeHandle
        {
            get { return elementTypeHandle; }
        }
    }

    struct RhSegementData : ISegmentData
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
        public uint isReadOnly;

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

    struct RhHeapDetails : IHeapDetails
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
            get { return finalization_fill_pointers5; }
        }

        public ulong FQStart
        {
            get { return finalization_fill_pointers4; }
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
}
#endif