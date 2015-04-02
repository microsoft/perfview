using Microsoft.Diagnostics.Runtime.Desktop;
using Microsoft.Diagnostics.Runtime.Interop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime
{
    unsafe class LiveDataReader : IDataReader
    {
        #region Variables
        IntPtr m_process;
        private int m_pid;
        #endregion

        const int PROCESS_VM_READ = 0x10;
        const int PROCESS_QUERY_INFORMATION = 0x0400;
        public LiveDataReader(int pid)
        {
            m_pid = pid;
            m_process = OpenProcess(PROCESS_VM_READ | PROCESS_QUERY_INFORMATION, false, pid);

            if (m_process == IntPtr.Zero)
                throw new ClrDiagnosticsException(String.Format("Could not attach to process. Error {0}.", Marshal.GetLastWin32Error()));

            bool wow64, targetWow64;
            using (Process p = Process.GetCurrentProcess())
                if (NativeMethods.TryGetWow64(p.Handle, out wow64) &&
                    NativeMethods.TryGetWow64(m_process, out targetWow64) &&
                    wow64 != targetWow64)
                {
                    throw new ClrDiagnosticsException("Dac architecture mismatch!");
                }
        }

        public bool IsMinidump
        {
            get
            {
                return false;
            }
        }

        public void Close()
        {
        }

        public void Flush()
        {
        }

        public Architecture GetArchitecture()
        {
            if (IntPtr.Size == 4)
                return Architecture.X86;

            return Architecture.Amd64;
        }

        public uint GetPointerSize()
        {
            return (uint)IntPtr.Size;
        }

        public IList<ModuleInfo> EnumerateModules()
        {
            List<ModuleInfo> result = new List<ModuleInfo>();

            uint needed;
            EnumProcessModules(m_process, null, 0, out needed);

            IntPtr[] modules = new IntPtr[needed / 4];
            uint size = (uint)modules.Length * sizeof(uint);

            if (!EnumProcessModules(m_process, modules, size, out needed))
                throw new ClrDiagnosticsException("Unable to get process modules.", ClrDiagnosticsException.HR.DataRequestError);

            for (int i = 0; i < modules.Length; i++)
            {
                IntPtr ptr = modules[i];

                if (ptr == IntPtr.Zero)
                {
                    break;
                }

                StringBuilder sb = new StringBuilder(1024);
                GetModuleFileNameExA(m_process, ptr, sb, sb.Capacity);

                string filename = sb.ToString();
                ModuleInfo module = new ModuleInfo(this);

                module.ImageBase = (ulong)ptr.ToInt64();
                module.FileName = filename;

                uint filesize, timestamp;
                GetFileProperties(module.ImageBase, out filesize, out timestamp);

                module.FileSize = filesize;
                module.TimeStamp = timestamp;

                result.Add(module);
            }

            return result;
        }

        public void GetVersionInfo(ulong addr, out VersionInfo version)
        {
            StringBuilder filename = new StringBuilder(1024);
            GetModuleFileNameExA(m_process, new IntPtr((long)addr), filename, filename.Capacity);

            int major, minor, revision, patch;
            if (NativeMethods.GetFileVersion(filename.ToString(), out major, out minor, out revision, out patch))
                version = new VersionInfo(major, minor, revision, patch);
            else
                version = new VersionInfo();
        }

        public bool ReadMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead)
        {
            try
            {
                int res = ReadProcessMemory(m_process, new IntPtr((long)address), buffer, bytesRequested, out bytesRead);
                return res != 0;
            }
            catch
            {
                bytesRead = 0;
                return false;
            }
        }


        byte[] m_ptrBuffer = new byte[IntPtr.Size];
        public ulong ReadPointerUnsafe(ulong addr)
        {
            int read;
            if (!ReadMemory(addr, m_ptrBuffer, IntPtr.Size, out read))
                return 0;

            fixed (byte* r = m_ptrBuffer)
            {
                if (IntPtr.Size == 4)
                    return *(((uint*)r));

                return *(((ulong*)r));
            }
        }


        public uint ReadDwordUnsafe(ulong addr)
        {
            int read;
            if (!ReadMemory(addr, m_ptrBuffer, 4, out read))
                return 0;

            fixed (byte* r = m_ptrBuffer)
                return *(((uint*)r));
        }


        public ulong GetThreadTeb(uint thread)
        {
            // todo
            throw new NotImplementedException();
        }

        public IEnumerable<uint> EnumerateAllThreads()
        {
            Process p = Process.GetProcessById(m_pid);
            foreach (ProcessThread thread in p.Threads)
                yield return (uint)thread.Id;
        }

        public bool VirtualQuery(ulong addr, out VirtualQueryData vq)
        {
            vq = new VirtualQueryData();

            MEMORY_BASIC_INFORMATION mem = new MEMORY_BASIC_INFORMATION();
            IntPtr ptr = new IntPtr((long)addr);

            int res = VirtualQueryEx(m_process, ptr, ref mem, new IntPtr(Marshal.SizeOf(mem)));
            if (res == 0)
                return false;

            vq.BaseAddress = mem.BaseAddress;
            vq.Size = mem.Size;
            return true;
        }

        public bool GetThreadContext(uint threadID, uint contextFlags, uint contextSize, IntPtr context)
        {
            using (SafeWin32Handle thread = OpenThread(ThreadAccess.THREAD_ALL_ACCESS, true, threadID))
            {
                if (thread.IsInvalid)
                    return false;

                bool res = GetThreadContext(thread.DangerousGetHandle(), context);
                return res;
            }
        }
        
        public unsafe bool GetThreadContext(uint threadID, uint contextFlags, uint contextSize, byte[] context)
        {
            using (SafeWin32Handle thread = OpenThread(ThreadAccess.THREAD_ALL_ACCESS, true, threadID))
            {
                if (thread.IsInvalid)
                    return false;

                fixed (byte* b = context)
                {
                    bool res = GetThreadContext(thread.DangerousGetHandle(), new IntPtr(b));
                    return res;
                }
            }
        }


        private void GetFileProperties(ulong moduleBase, out uint filesize, out uint timestamp)
        {
            filesize = 0;
            timestamp = 0;
            byte[] buffer = new byte[4];

            int read;
            if (ReadMemory(moduleBase + 0x3c, buffer, buffer.Length, out read) && read == buffer.Length)
            {
                uint sigOffset = (uint)BitConverter.ToInt32(buffer, 0);
                int sigLength = 4;

                if (ReadMemory(moduleBase + (ulong)sigOffset, buffer, buffer.Length, out read) && read == buffer.Length)
                {
                    uint header = (uint)BitConverter.ToInt32(buffer, 0);

                    // Ensure the module contains the magic "PE" value at the offset it says it does.  This check should
                    // never fail unless we have the wrong base address for CLR.
                    Debug.Assert(header == 0x4550);
                    if (header == 0x4550)
                    {
                        const int timeDataOffset = 4;
                        const int imageSizeOffset = 0x4c;
                        if (ReadMemory(moduleBase + (ulong)sigOffset + (ulong)sigLength + (ulong)timeDataOffset, buffer, buffer.Length, out read) && read == buffer.Length)
                            timestamp = (uint)BitConverter.ToInt32(buffer, 0);

                        if (ReadMemory(moduleBase + (ulong)sigOffset + (ulong)sigLength + (ulong)imageSizeOffset, buffer, buffer.Length, out read) && read == buffer.Length)
                            filesize = (uint)BitConverter.ToInt32(buffer, 0);
                    }
                }
            }
        }

        #region PInvoke Structs
        [StructLayout(LayoutKind.Sequential)]
        internal struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr Address;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public IntPtr RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;

            public ulong BaseAddress
            {
                get { return (ulong)Address; }
            }

            public ulong Size
            {
                get { return (ulong)RegionSize; }
            }
        }
        #endregion

        #region PInvokes
        [DllImportAttribute("kernel32.dll", EntryPoint = "OpenProcess")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("psapi.dll", SetLastError = true)]
        public static extern bool EnumProcessModules(IntPtr hProcess, [Out] IntPtr[] lphModule, uint cb, [MarshalAs(UnmanagedType.U4)] out uint lpcbNeeded);

        [DllImport("psapi.dll", SetLastError = true)]
        [PreserveSig]
        public static extern uint GetModuleFileNameExA([In]IntPtr hProcess, [In]IntPtr hModule, [Out]StringBuilder lpFilename, [In][MarshalAs(UnmanagedType.U4)]int nSize);

        [DllImport("kernel32.dll")]
        static extern int ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, ref MEMORY_BASIC_INFORMATION lpBuffer, IntPtr dwLength);

        [DllImport("kernel32.dll")]
        static extern bool GetThreadContext(IntPtr hThread, IntPtr lpContext);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern SafeWin32Handle OpenThread(ThreadAccess dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, uint dwThreadId);
        #endregion

        public bool CanReadAsync
        {
            //todo
            get { return false; }
        }

        public AsyncMemoryReadResult ReadMemoryAsync(ulong address, int bytesRequested)
        {
            throw new NotImplementedException();
        }


        public bool ReadMemory(ulong address, IntPtr buffer, int bytesRequested, out int bytesRead)
        {
            throw new NotImplementedException();
        }


        enum ThreadAccess : int
        {
            THREAD_ALL_ACCESS = (0x1F03FF),
        }
    }
}
