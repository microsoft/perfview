using Microsoft.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    class AppDomainHeapWalker
    {
        #region Variables
        enum InternalHeapTypes
        {
            IndcellHeap,
            LookupHeap,
            ResolveHeap,
            DispatchHeap,
            CacheEntryHeap
        }

        List<MemoryRegion> mRegions = new List<MemoryRegion>();
        DesktopRuntimeBase.LoaderHeapTraverse mDelegate;
        ClrMemoryRegionType mType;
        ulong mAppDomain;
        DesktopRuntimeBase mRuntime;
        #endregion

        public AppDomainHeapWalker(DesktopRuntimeBase runtime)
        {
            mRuntime = runtime;
            mDelegate = new DesktopRuntimeBase.LoaderHeapTraverse(VisitOneHeap);
        }

        public IEnumerable<MemoryRegion> EnumerateHeaps(IAppDomainData appDomain)
        {
            Debug.Assert(appDomain != null);
            mAppDomain = appDomain.Address;
            mRegions.Clear();

            // Standard heaps.
            mType = ClrMemoryRegionType.LowFrequencyLoaderHeap;
            mRuntime.TraverseHeap(appDomain.LowFrequencyHeap, mDelegate);

            mType = ClrMemoryRegionType.HighFrequencyLoaderHeap;
            mRuntime.TraverseHeap(appDomain.HighFrequencyHeap, mDelegate);

            mType = ClrMemoryRegionType.StubHeap;
            mRuntime.TraverseHeap(appDomain.StubHeap, mDelegate);

            // Stub heaps.
            mType = ClrMemoryRegionType.IndcellHeap;
            mRuntime.TraverseStubHeap(mAppDomain, (int)InternalHeapTypes.IndcellHeap, mDelegate);

            mType = ClrMemoryRegionType.LookupHeap;
            mRuntime.TraverseStubHeap(mAppDomain, (int)InternalHeapTypes.LookupHeap, mDelegate);

            mType = ClrMemoryRegionType.ResolveHeap;
            mRuntime.TraverseStubHeap(mAppDomain, (int)InternalHeapTypes.ResolveHeap, mDelegate);

            mType = ClrMemoryRegionType.DispatchHeap;
            mRuntime.TraverseStubHeap(mAppDomain, (int)InternalHeapTypes.DispatchHeap, mDelegate);

            mType = ClrMemoryRegionType.CacheEntryHeap;
            mRuntime.TraverseStubHeap(mAppDomain, (int)InternalHeapTypes.CacheEntryHeap, mDelegate);

            return mRegions;
        }

        public IEnumerable<MemoryRegion> EnumerateModuleHeaps(IAppDomainData appDomain, ulong addr)
        {
            Debug.Assert(appDomain != null);
            mAppDomain = appDomain.Address;
            mRegions.Clear();

            if (addr == 0)
                return mRegions;

            IModuleData module = mRuntime.GetModuleData(addr);
            if (module != null)
            {
                mType = ClrMemoryRegionType.ModuleThunkHeap;
                mRuntime.TraverseHeap(module.ThunkHeap, mDelegate);

                mType = ClrMemoryRegionType.ModuleLookupTableHeap;
                mRuntime.TraverseHeap(module.LookupTableHeap, mDelegate);
            }

            return mRegions;
        }

        public IEnumerable<MemoryRegion> EnumerateJitHeap(ulong heap)
        {
            mAppDomain = 0;
            mRegions.Clear();

            mType = ClrMemoryRegionType.JitLoaderCodeHeap;
            mRuntime.TraverseHeap(heap, mDelegate);

            return mRegions;
        }

        #region Helper Functions
        void VisitOneHeap(ulong address, IntPtr size, int isCurrent)
        {
            if (mAppDomain == 0)
                mRegions.Add(new MemoryRegion(mRuntime, address, (ulong)size.ToInt64(), mType));
            else
                mRegions.Add(new MemoryRegion(mRuntime, address, (ulong)size.ToInt64(), mType, mAppDomain));
        }
        #endregion

    }

    class HandleTableWalker
    {
        #region Variables
        DesktopRuntimeBase m_runtime;
        ClrHeap m_heap;
        int m_max = 10000;
        VISITHANDLEV2 mV2Delegate;
        VISITHANDLEV4 mV4Delegate;
        #endregion

        #region Properties
        public List<ClrHandle> Handles { get; private set; }
        public byte[] V4Request
        {
            get
            {
                // MULTITHREAD ISSUE
                if (mV4Delegate == null)
                    mV4Delegate = new VISITHANDLEV4(VisitHandleV4);

                IntPtr functionPtr = Marshal.GetFunctionPointerForDelegate(mV4Delegate);
                byte[] request = new byte[IntPtr.Size * 2];
                FunctionPointerToByteArray(functionPtr, request, 0);

                return request;
            }
        }


        public byte[] V2Request
        {
            get
            {
                // MULTITHREAD ISSUE
                if (mV2Delegate == null)
                    mV2Delegate = new VISITHANDLEV2(VisitHandleV2);

                IntPtr functionPtr = Marshal.GetFunctionPointerForDelegate(mV2Delegate);
                byte[] request = new byte[IntPtr.Size * 2];

                FunctionPointerToByteArray(functionPtr, request, 0);

                return request;
            }
        }
        #endregion

        #region Functions
        public HandleTableWalker(DesktopRuntimeBase dac)
        {
            m_runtime = dac;
            m_heap = dac.GetHeap();
            Handles = new List<ClrHandle>();
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int VISITHANDLEV4(ulong HandleAddr, ulong HandleValue, int HandleType, uint ulRefCount, ulong appDomainPtr, IntPtr token);

        int VisitHandleV4(ulong addr, ulong obj, int hndType, uint refCnt, ulong appDomain, IntPtr unused)
        {
            Debug.Assert(unused == IntPtr.Zero);

            return AddHandle(addr, obj, hndType, refCnt, 0, appDomain);
        }


        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int VISITHANDLEV2(ulong HandleAddr, ulong HandleValue, int HandleType, ulong appDomainPtr, IntPtr token);

        int VisitHandleV2(ulong addr, ulong obj, int hndType, ulong appDomain, IntPtr unused)
        {
            Debug.Assert(unused == IntPtr.Zero);

            // V2 cannot actually get the ref count from a handle.  We'll always report the RefCount as
            // 1 in this case so the user will treat this as a strong handle (which the majority of COM
            // handles are).
            uint refCnt = 0;
            if (hndType == (uint)HandleType.RefCount)
                refCnt = 1;

            return AddHandle(addr, obj, hndType, refCnt, 0, appDomain);
        }

        public int AddHandle(ulong addr, ulong obj, int hndType, uint refCnt, uint dependentTarget, ulong appDomain)
        {
            ulong mt;
            ulong cmt;

            // If we fail to get the MT of this object, just skip it and keep going
            if (!GetMethodTables(obj, out mt, out cmt))
                return m_max-- > 0 ? 1 : 0;

            ClrHandle handle = new ClrHandle();
            handle.Address = addr;
            handle.Object = obj;
            handle.Type = m_heap.GetObjectType(obj);
            handle.HandleType = (HandleType)hndType;
            handle.RefCount = refCnt;
            handle.AppDomain = m_runtime.GetAppDomainByAddress(appDomain);
            handle.DependentTarget = dependentTarget;

            if (dependentTarget != 0)
                handle.DependentType = m_heap.GetObjectType(dependentTarget);

            Handles.Add(handle);

            // Stop if we have too many handles (likely infinite loop in dac due to
            // inconsistent data).
            return m_max-- > 0 ? 1 : 0;
        }

        private bool GetMethodTables(ulong obj, out ulong mt, out ulong cmt)
        {
            mt = 0;
            cmt = 0;

            byte[] data = new byte[IntPtr.Size * 3];        // TODO assumes bitness same as dump
            int read = 0;
            if (!m_runtime.ReadMemory(obj, data, data.Length, out read) || read != data.Length)
                return false;

            if (IntPtr.Size == 4)
                mt = BitConverter.ToUInt32(data, 0);
            else
                mt = BitConverter.ToUInt64(data, 0);

            if (mt == m_runtime.ArrayMethodTable)
            {
                if (IntPtr.Size == 4)
                    cmt = BitConverter.ToUInt32(data, 2 * IntPtr.Size);
                else
                    cmt = BitConverter.ToUInt64(data, 2 * IntPtr.Size);
            }

            return true;
        }

        private static void FunctionPointerToByteArray(IntPtr functionPtr, byte[] request, int start)
        {
            long ptr = functionPtr.ToInt64();

            for (int i = start; i < start + sizeof(ulong); ++i)
            {
                request[i] = (byte)ptr;
                ptr >>= 8;
            }
        }
        #endregion
    }

    class NativeMethods
    {
        public static bool LoadNative(string dllName)
        {
            return LoadLibrary(dllName) != IntPtr.Zero;
        }

        const string Kernel32LibraryName = "kernel32.dll";

        public const uint FILE_MAP_READ = 4;

        // Call CloseHandle to clean up.
        [DllImport(Kernel32LibraryName, SetLastError = true)]
        public static extern SafeWin32Handle CreateFileMapping(
           SafeFileHandle hFile,
           IntPtr lpFileMappingAttributes, PageProtection flProtect, uint dwMaximumSizeHigh,
           uint dwMaximumSizeLow, string lpName);

        [DllImport(Kernel32LibraryName, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnmapViewOfFile(IntPtr baseAddress);


        [DllImport(Kernel32LibraryName, SetLastError = true)]
        public static extern SafeMapViewHandle MapViewOfFile(SafeWin32Handle hFileMappingObject, uint
           dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow,
           IntPtr dwNumberOfBytesToMap);

        [DllImportAttribute(Kernel32LibraryName)]
        public static extern void RtlMoveMemory(IntPtr destination, IntPtr source, IntPtr numberBytes);

        [DllImport(Kernel32LibraryName, SetLastError = true, PreserveSig = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseHandle(IntPtr handle);

        [DllImportAttribute(Kernel32LibraryName)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FreeLibrary(IntPtr hModule);

        public static IntPtr LoadLibrary(string lpFileName)
        {
            return LoadLibraryEx(lpFileName, 0, LoadLibraryFlags.NoFlags);
        }

        [DllImportAttribute(Kernel32LibraryName, SetLastError = true)]
        public static extern IntPtr LoadLibraryEx(String fileName, int hFile, LoadLibraryFlags dwFlags);

        [Flags]
        public enum LoadLibraryFlags : uint
        {
            NoFlags = 0x00000000,
            DontResolveDllReferences = 0x00000001,
            LoadIgnoreCodeAuthzLevel = 0x00000010,
            LoadLibraryAsDatafile = 0x00000002,
            LoadLibraryAsDatafileExclusive = 0x00000040,
            LoadLibraryAsImageResource = 0x00000020,
            LoadWithAlteredSearchPath = 0x00000008
        }


        [Flags]
        public enum PageProtection : uint
        {
            NoAccess = 0x01,
            Readonly = 0x02,
            ReadWrite = 0x04,
            WriteCopy = 0x08,
            Execute = 0x10,
            ExecuteRead = 0x20,
            ExecuteReadWrite = 0x40,
            ExecuteWriteCopy = 0x80,
            Guard = 0x100,
            NoCache = 0x200,
            WriteCombine = 0x400,
        }

        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWow64Process([In] IntPtr hProcess, [Out] out bool isWow64);

        [DllImport("version.dll")]
        internal static extern bool GetFileVersionInfo(string sFileName, int handle, int size, byte[] infoBuffer);

        [DllImport("version.dll")]
        internal static extern int GetFileVersionInfoSize(string sFileName, out int handle);

        [DllImport("version.dll")]
        internal static extern bool VerQueryValue(byte[] pBlock, string pSubBlock, out IntPtr val, out int len);

        const int VS_FIXEDFILEINFO_size = 0x34;
        public static short IMAGE_DIRECTORY_ENTRY_COM_DESCRIPTOR = 14;

        [DllImport("dbgeng.dll")]
        internal static extern uint DebugCreate(ref Guid InterfaceId, [MarshalAs(UnmanagedType.IUnknown)] out object Interface);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        internal delegate int CreateDacInstance([In, ComAliasName("REFIID")] ref Guid riid,
                                       [In, MarshalAs(UnmanagedType.Interface)] IDacDataTarget data,
                                       [Out, MarshalAs(UnmanagedType.IUnknown)] out object ppObj);


        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);




        [DllImport("dbghelp.dll")]
        internal static extern IntPtr ImageDirectoryEntryToData(IntPtr mapping, bool mappedAsImage, short directoryEntry, out uint size);

        [DllImport("dbghelp.dll")]
        public static extern IntPtr ImageRvaToVa(IntPtr mapping, IntPtr baseAddr, uint rva, IntPtr lastRvaSection);

        [DllImport("dbghelp.dll")]
        public static extern IntPtr ImageNtHeader(IntPtr imageBase);

        internal static bool IsEqualFileVersion(string file, VersionInfo version)
        {
            int major, minor, revision, patch;
            if (!GetFileVersion(file, out major, out minor, out revision, out patch))
                return false;

            return major == version.Major && minor == version.Minor && revision == version.Revision && patch == version.Patch;
        }


        internal static bool GetFileVersion(string dll, out int major, out int minor, out int revision, out int patch)
        {
            major = minor = revision = patch = 0;

            int handle;
            int len = GetFileVersionInfoSize(dll, out handle);

            if (len <= 0)
                return false;

            byte[] data = new byte[len];
            if (!GetFileVersionInfo(dll, handle, len, data))
                return false;

            IntPtr ptr;
            if (!VerQueryValue(data, "\\", out ptr, out len))
            {
                return false;
            }


            byte[] vsFixedInfo = new byte[len];
            Marshal.Copy(ptr, vsFixedInfo, 0, len);

            minor = (ushort)Marshal.ReadInt16(vsFixedInfo, 8);
            major = (ushort)Marshal.ReadInt16(vsFixedInfo, 10);
            patch = (ushort)Marshal.ReadInt16(vsFixedInfo, 12);
            revision = (ushort)Marshal.ReadInt16(vsFixedInfo, 14);

            return true;
        }

        internal static bool TryGetWow64(IntPtr proc, out bool result)
        {
            if (Environment.OSVersion.Version.Major > 5 ||
                (Environment.OSVersion.Version.Major == 5 && Environment.OSVersion.Version.Minor >= 1))
            {
                return IsWow64Process(proc, out result);
            }
            else
            {
                result = false;
                return false;
            }
        }
    }


    sealed class SafeWin32Handle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public SafeWin32Handle() : base(true) { }

        public SafeWin32Handle(IntPtr handle)
            : this(handle, true)
        {
        }

        public SafeWin32Handle(IntPtr handle, bool ownsHandle)
            : base(ownsHandle)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
        {
            return NativeMethods.CloseHandle(handle);
        }
    }

    sealed class SafeMapViewHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeMapViewHandle() : base(true) { }

        protected override bool ReleaseHandle()
        {
            return NativeMethods.UnmapViewOfFile(handle);
        }

        // This is technically equivalent to DangerousGetHandle, but it's safer for file
        // mappings. In file mappings, the "handle" is actually a base address that needs
        // to be used in computations and RVAs.
        // So provide a safer accessor method.
        public IntPtr BaseAddress
        {
            get
            {
                return handle;
            }
        }
    }

    sealed class SafeLoadLibraryHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeLoadLibraryHandle() : base(true) { }
        public SafeLoadLibraryHandle(IntPtr handle)
            : base(true)
        {
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
        {
            return NativeMethods.FreeLibrary(handle);
        }

        // This is technically equivalent to DangerousGetHandle, but it's safer for loaded
        // libraries where the HMODULE is also the base address the module is loaded at.
        public IntPtr BaseAddress
        {
            get
            {
                return handle;
            }
        }
    }
}
