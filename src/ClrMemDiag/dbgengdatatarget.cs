using Microsoft.Diagnostics.Runtime.Desktop;
using Microsoft.Diagnostics.Runtime.Interop;
using Microsoft.Diagnostics.Runtime.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Microsoft.Diagnostics.Runtime
{
    unsafe class DbgEngDataReader : IDisposable, IDataReader
    {
        static int s_totalInstanceCount = 0;
        static bool s_needRelease = true;

        IDebugClient m_client;
        IDebugDataSpaces m_spaces;
        IDebugDataSpaces2 m_spaces2;
        IDebugSymbols m_symbols;
        IDebugSymbols3 m_symbols3;
        IDebugControl2 m_control;
        IDebugAdvanced m_advanced;
        IDebugSystemObjects m_systemObjects;
        IDebugSystemObjects3 m_systemObjects3;

        uint m_instance = 0;
        private bool m_disposed;

        byte[] m_ptrBuffer = new byte[IntPtr.Size];
        private List<ModuleInfo> m_modules;

        ~DbgEngDataReader()
        {
            Dispose(false);
        }

        private void SetClientInstance()
        {
            Debug.Assert(s_totalInstanceCount > 0);

            if (m_systemObjects3 != null && s_totalInstanceCount > 1)
                m_systemObjects3.SetCurrentSystemId(m_instance);
        }


        public DbgEngDataReader(string dumpFile)
        {
            if (!File.Exists(dumpFile))
                throw new FileNotFoundException(dumpFile);

            IDebugClient client = CreateIDebugClient();
            int hr = client.OpenDumpFile(dumpFile);

            if (hr != 0)
                throw new ClrDiagnosticsException(String.Format("Could not load crash dump '{0}', HRESULT: 0x{1:x8}", dumpFile, hr), ClrDiagnosticsException.HR.DebuggerError);

            CreateClient(client);

            // This actually "attaches" to the crash dump.
            m_control.WaitForEvent(0, 0xffffffff);
        }

        internal DbgEngDataReader(IDebugClient client)
        {
            //* We need to be very careful to not cleanup the IDebugClient interfaces
            // * (that is, detach from the target process) if we created this wrapper
            // * from a pre-existing IDebugClient interface.  Setting s_needRelease to
            // * false will keep us from *ever* explicitly detaching from any IDebug
            // * interface (even ones legitimately attached with other constructors),
            // * but this is the best we can do with DbgEng's design.  Better to leak
            // * a small amount of memory (and file locks) than detatch someone else's
            // * IDebug interface unexpectedly.
            // 
            CreateClient(client);
            s_needRelease = false;
        }

        internal DbgEngDataReader(int pid, AttachFlag flags, uint msecTimeout)
        {
            IDebugClient client = CreateIDebugClient();
            CreateClient(client);

            DEBUG_ATTACH attach = (flags == AttachFlag.Invasive) ? DEBUG_ATTACH.DEFAULT : DEBUG_ATTACH.NONINVASIVE;
            int hr = m_control.AddEngineOptions(DEBUG_ENGOPT.INITIAL_BREAK);

            if (hr == 0)
                hr = client.AttachProcess(0, (uint)pid, attach);

            if (hr == 0)
                hr = m_control.WaitForEvent(0, msecTimeout);

            if (hr == 1)
                throw new TimeoutException("Break in did not occur within the allotted timeout.");
            else if (hr != 0)
                throw new ClrDiagnosticsException(String.Format("Could not attach to pid {0:X}, HRESULT: 0x{1:x8}", pid, hr), ClrDiagnosticsException.HR.DebuggerError);
        }



        public bool IsMinidump
        {
            get
            {
                SetClientInstance();

                DEBUG_CLASS cls;
                DEBUG_CLASS_QUALIFIER qual;
                m_control.GetDebuggeeType(out cls, out qual);

                if (qual == DEBUG_CLASS_QUALIFIER.USER_WINDOWS_SMALL_DUMP)
                {
                    DEBUG_FORMAT flags;
                    m_control.GetDumpFormatFlags(out flags);
                    return (flags & DEBUG_FORMAT.USER_SMALL_FULL_MEMORY) == 0;
                }

                return false;
            }
        }

        public Architecture GetArchitecture()
        {
            SetClientInstance();

            IMAGE_FILE_MACHINE machineType;
            int hr = m_control.GetExecutingProcessorType(out machineType);
            if (0 != hr)
                throw new ClrDiagnosticsException(String.Format("Failed to get proessor type, HRESULT: {0:x8}", hr), ClrDiagnosticsException.HR.DebuggerError);

            switch (machineType)
            {
                case IMAGE_FILE_MACHINE.I386:
                    return Architecture.X86;

                case IMAGE_FILE_MACHINE.AMD64:
                    return Architecture.Amd64;

                case IMAGE_FILE_MACHINE.ARM:
                case IMAGE_FILE_MACHINE.THUMB:
                case IMAGE_FILE_MACHINE.THUMB2:
                    return Architecture.Arm;

                default:
                    return Architecture.Unknown;
            }
        }

        private static IDebugClient CreateIDebugClient()
        {
            Guid guid = new Guid("27fe5639-8407-4f47-8364-ee118fb08ac8");
            object obj;
            NativeMethods.DebugCreate(ref guid, out obj);

            IDebugClient client = (IDebugClient)obj;
            return client;
        }

        public void Close()
        {
            Dispose();
        }


        internal IDebugClient DebuggerInterface
        {
            get { return m_client; }
        }

        public uint GetPointerSize()
        {
            SetClientInstance();
            int hr = m_control.IsPointer64Bit();
            if (hr == 0)
                return 8;
            else if (hr == 1)
                return 4;

            throw new ClrDiagnosticsException(String.Format("IsPointer64Bit failed: {0:x8}", hr), ClrDiagnosticsException.HR.DebuggerError);
        }

        public void Flush()
        {
            m_modules = null;
        }

        public bool GetThreadContext(uint threadID, uint contextFlags, uint contextSize, IntPtr context)
        {
            uint id = 0;
            GetThreadIdBySystemId(threadID, out id);

            SetCurrentThreadId(id);
            GetThreadContext(context, contextSize);

            return true;
        }

        void GetThreadContext(IntPtr context, uint contextSize)
        {
            SetClientInstance();
            m_advanced.GetThreadContext(context, contextSize);
        }

        internal int ReadVirtual(ulong address, byte[] buffer, int bytesRequested, out int bytesRead)
        {
            SetClientInstance();
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            if (buffer.Length < bytesRequested)
                bytesRequested = buffer.Length;

            uint read = 0;
            int res = m_spaces.ReadVirtual(address, buffer, (uint)bytesRequested, out read);
            bytesRead = (int)read;
            return res;
        }


        private ulong[] GetImageBases()
        {
            List<ulong> bases = null;
            uint count, unloadedCount;
            if (GetNumberModules(out count, out unloadedCount) < 0)
                return null;

            bases = new List<ulong>((int)count);
            for (uint i = 0; i < count + unloadedCount; ++i)
            {
                ulong image;
                if (GetModuleByIndex(i, out image) < 0)
                    continue;

                bases.Add(image);
            }

            return bases.ToArray();
        }

        public IList<ModuleInfo> EnumerateModules()
        {
            if (m_modules != null)
                return m_modules;

            ulong[] bases = GetImageBases();
            DEBUG_MODULE_PARAMETERS[] mods = new DEBUG_MODULE_PARAMETERS[bases.Length];
            List<ModuleInfo> modules = new List<ModuleInfo>();

            if (bases != null && CanEnumerateModules)
            {
                int hr = GetModuleParameters(bases.Length, bases, 0, mods);
                if (hr >= 0)
                {
                    for (int i = 0; i < bases.Length; ++i)
                    {
                        ModuleInfo info = new ModuleInfo(this);
                        info.TimeStamp = mods[i].TimeDateStamp;
                        info.FileSize = mods[i].Size;
                        info.ImageBase = bases[i];

                        uint needed;
                        StringBuilder sbpath = new StringBuilder();
                        if (GetModuleNameString(DEBUG_MODNAME.IMAGE, i, bases[i], null, 0, out needed) >= 0 && needed > 1)
                        {
                            sbpath.EnsureCapacity((int)needed);
                            if (GetModuleNameString(DEBUG_MODNAME.IMAGE, i, bases[i], sbpath, needed, out needed) >= 0)
                                info.FileName = sbpath.ToString();
                        }

                        modules.Add(info);
                    }
                }
            }

            m_modules = modules;
            return modules;
        }


        public bool CanEnumerateModules { get { return m_symbols3 != null; } }

        internal int GetModuleParameters(int count, ulong[] bases, int start, DEBUG_MODULE_PARAMETERS[] mods)
        {
            SetClientInstance();
            return m_symbols.GetModuleParameters((uint)count, bases, (uint)start, mods);
        }

        private void CreateClient(IDebugClient client)
        {
            m_client = client;

            m_spaces = (IDebugDataSpaces)m_client;
            m_symbols = (IDebugSymbols)m_client;
            m_control = (IDebugControl2)m_client;

            // These interfaces may not be present in older DbgEng dlls.
            m_spaces2 = m_client as IDebugDataSpaces2;
            m_symbols3 = m_client as IDebugSymbols3;
            m_advanced = m_client as IDebugAdvanced;
            m_systemObjects = m_client as IDebugSystemObjects;
            m_systemObjects3 = m_client as IDebugSystemObjects3;

            Interlocked.Increment(ref s_totalInstanceCount);

            if (m_systemObjects3 == null && s_totalInstanceCount > 1)
                throw new ClrDiagnosticsException("This version of DbgEng is too old to create multiple instances of DataTarget.", ClrDiagnosticsException.HR.DebuggerError);

            if (m_systemObjects3 != null)
                m_systemObjects3.GetCurrentSystemId(out m_instance);
        }



        internal int GetModuleNameString(DEBUG_MODNAME Which, int Index, UInt64 Base, StringBuilder Buffer, UInt32 BufferSize, out UInt32 NameSize)
        {
            if (m_symbols3 == null)
            {
                NameSize = 0;
                return -1;
            }

            SetClientInstance();
            return m_symbols3.GetModuleNameString(Which, (uint)Index, Base, Buffer, BufferSize, out NameSize);
        }

        internal int GetNumberModules(out uint count, out uint unloadedCount)
        {
            if (m_symbols3 == null)
            {
                count = 0;
                unloadedCount = 0;
                return -1;
            }

            SetClientInstance();
            return m_symbols3.GetNumberModules(out count, out unloadedCount);
        }

        internal int GetModuleByIndex(uint i, out ulong image)
        {
            if (m_symbols3 == null)
            {
                image = 0;
                return -1;
            }

            SetClientInstance();
            return m_symbols3.GetModuleByIndex(i, out image);
        }

        internal int GetNameByOffsetWide(ulong offset, StringBuilder sb, int p, out uint size, out ulong disp)
        {
            SetClientInstance();
            return m_symbols3.GetNameByOffsetWide(offset, sb, p, out size, out disp);
        }

        public bool VirtualQuery(ulong addr, out VirtualQueryData vq)
        {
            vq = new VirtualQueryData();
            if (m_spaces2 == null)
                return false;

            MEMORY_BASIC_INFORMATION64 mem;
            SetClientInstance();
            int hr = m_spaces2.QueryVirtual(addr, out mem);
            vq.BaseAddress = mem.BaseAddress;
            vq.Size = mem.RegionSize;

            return hr == 0;
        }


        public bool ReadMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead)
        {
            return ReadVirtual(address, buffer, bytesRequested, out bytesRead) >= 0;
        }


        public ulong ReadPointerUnsafe(ulong addr)
        {
            int read;
            if (ReadVirtual(addr, m_ptrBuffer, IntPtr.Size, out read) != 0)
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
            if (ReadVirtual(addr, m_ptrBuffer, 4, out read) != 0)
                return 0;

            fixed (byte* r = m_ptrBuffer)
                return *(((uint*)r));
        }

        internal void SetSymbolPath(string path)
        {
            SetClientInstance();
            m_symbols.SetSymbolPath(path);
            m_control.Execute(DEBUG_OUTCTL.NOT_LOGGED, ".reload", DEBUG_EXECUTE.NOT_LOGGED);
        }

        internal int QueryVirtual(ulong addr, out MEMORY_BASIC_INFORMATION64 mem)
        {
            if (m_spaces2 == null)
            {
                mem = new MEMORY_BASIC_INFORMATION64();
                return -1;
            }

            SetClientInstance();
            return m_spaces2.QueryVirtual(addr, out mem);
        }

        internal int GetModuleByModuleName(string image, int start, out uint index, out ulong baseAddress)
        {
            SetClientInstance();
            return m_symbols.GetModuleByModuleName(image, (uint)start, out index, out baseAddress);
        }

        public void GetVersionInfo(ulong addr, out VersionInfo version)
        {
            version = new VersionInfo();

            uint index;
            ulong baseAddr;
            int hr = m_symbols.GetModuleByOffset(addr, 0, out index, out baseAddr);
            if (hr != 0)
                return;

            uint needed = 0;
            hr = GetModuleVersionInformation(index, baseAddr, "\\", null, 0, out needed);
            if (hr != 0)
                return;

            byte[] buffer = new byte[needed];
            hr = GetModuleVersionInformation(index, baseAddr, "\\", buffer, needed, out needed);
            if (hr != 0)
                return;

            version.Minor = (ushort)Marshal.ReadInt16(buffer, 8);
            version.Major = (ushort)Marshal.ReadInt16(buffer, 10);
            version.Patch = (ushort)Marshal.ReadInt16(buffer, 12);
            version.Revision = (ushort)Marshal.ReadInt16(buffer, 14);

            return;
        }

        internal int GetModuleVersionInformation(uint index, ulong baseAddress, string p, byte[] buffer, uint needed1, out uint needed2)
        {
            if (m_symbols3 == null)
            {
                needed2 = 0;
                return -1;
            }

            SetClientInstance();
            return m_symbols3.GetModuleVersionInformation(index, baseAddress, "\\", buffer, needed1, out needed2);
        }

        internal int GetModuleNameString(DEBUG_MODNAME requestType, uint index, ulong baseAddress, StringBuilder sbpath, uint needed1, out uint needed2)
        {
            if (m_symbols3 == null)
            {
                needed2 = 0;
                return -1;
            }

            SetClientInstance();
            return m_symbols3.GetModuleNameString(requestType, index, baseAddress, sbpath, needed1, out needed2);
        }

        internal int GetModuleParameters(UInt32 Count, UInt64[] Bases, UInt32 Start, DEBUG_MODULE_PARAMETERS[] Params)
        {
            SetClientInstance();
            return m_symbols.GetModuleParameters(Count, Bases, Start, Params);
        }

        internal void GetThreadIdBySystemId(uint threadID, out uint id)
        {
            SetClientInstance();
            m_systemObjects.GetThreadIdBySystemId(threadID, out id);
        }

        internal void SetCurrentThreadId(uint id)
        {
            SetClientInstance();
            m_systemObjects.SetCurrentThreadId(id);
        }

        internal void GetExecutingProcessorType(out IMAGE_FILE_MACHINE machineType)
        {
            SetClientInstance();
            m_control.GetEffectiveProcessorType(out machineType);
        }

        public int ReadVirtual(ulong address, byte[] buffer, uint bytesRequested, out uint bytesRead)
        {
            SetClientInstance();
            return m_spaces.ReadVirtual(address, buffer, bytesRequested, out bytesRead);
        }

        public IEnumerable<uint> EnumerateAllThreads()
        {
            SetClientInstance();

            uint count = 0;
            int hr = m_systemObjects.GetNumberThreads(out count);
            if (hr == 0)
            {
                uint[] sysIds = new uint[count];

                hr = m_systemObjects.GetThreadIdsByIndex(0, count, null, sysIds);
                if (hr == 0)
                    return sysIds;
            }

            return new uint[0];
        }

        public ulong GetThreadTeb(uint thread)
        {
            SetClientInstance();

            ulong teb = 0;
            uint id = 0;
            int hr = m_systemObjects.GetCurrentThreadId(out id);
            bool haveId = hr == 0;

            if (m_systemObjects.GetThreadIdBySystemId(thread, out id) == 0 && m_systemObjects.SetCurrentThreadId(id) == 0)
                m_systemObjects.GetCurrentThreadTeb(out teb);

            if (haveId)
                m_systemObjects.SetCurrentThreadId(id);

            return teb;
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        protected virtual void Dispose(bool disposing)
        {
            if (m_disposed)
                return;

            m_disposed = true;

            int count = Interlocked.Decrement(ref s_totalInstanceCount);
            if (count == 0 && s_needRelease && disposing)
            {
                if (m_systemObjects3 != null)
                    m_systemObjects3.SetCurrentSystemId(m_instance);

                m_client.EndSession(DEBUG_END.ACTIVE_DETACH);
                m_client.DetachProcesses();
            }

            // If there are no more debug instances, we can safely reset this variable
            // and start releasing newly created IDebug objects.
            if (count == 0)
                s_needRelease = true;
        }

        public bool CanReadAsync
        {
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

        public unsafe bool GetThreadContext(uint threadID, uint contextFlags, uint contextSize, byte[] context)
        {
            uint id = 0;
            GetThreadIdBySystemId(threadID, out id);

            SetCurrentThreadId(id);

            fixed (byte* pContext = &context[0])
                GetThreadContext(new IntPtr(pContext), contextSize);

            return true;
        }
    }
}
