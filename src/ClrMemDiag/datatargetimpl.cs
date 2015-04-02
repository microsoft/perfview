using Microsoft.Diagnostics.Runtime.Desktop;
using Microsoft.Diagnostics.Runtime.Interop;
using Microsoft.Diagnostics.Runtime.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime
{
    class DataTargetImpl : DataTarget
    {
        private IDataReader m_dataReader;
        IDebugClient m_client;
        private ClrInfo[] m_versions;
        private Architecture m_architecture;
        ModuleInfo[] m_modules;
        Dictionary<ModuleInfo, SymbolModule> m_symbols = new Dictionary<ModuleInfo, SymbolModule>();

        public DataTargetImpl(IDataReader dataReader, IDebugClient client)
        {
            if (dataReader == null)
                throw new ArgumentNullException("dataReader");

            m_dataReader = dataReader;
            m_client = client;
            m_architecture = m_dataReader.GetArchitecture();

            var sympath = SymPath._NT_SYMBOL_PATH;
            if (string.IsNullOrEmpty(sympath))
                sympath = SymPath.MicrosoftSymbolServerPath;

            m_symPath = new SymPath(sympath);
        }

        public IDataReader DataReader
        {
            get
            {
                return m_dataReader;
            }
        }

        public override bool IsMinidump
        {
            get { return m_dataReader.IsMinidump; }
        }

        public override void SetSymbolPath(string path)
        {
            m_symPath.Set(path);
        }

        public override void ClearSymbolPath()
        {
            m_symPath.Clear();
        }

        public override void AppendSymbolPath(string path)
        {
            m_symPath.Add(path);
        }

        public override string GetSymbolPath()
        {
            return m_symPath.ToString();
        }

        public override Architecture Architecture
        {
            get { return m_architecture; }
        }

        public override uint PointerSize
        {
            get { return m_dataReader.GetPointerSize(); }
        }

        public override IList<ClrInfo> ClrVersions
        {
            get
            {
                if (m_versions != null)
                    return m_versions;

                int count = 0;
                ClrInfo[] versions = new ClrInfo[1];
                foreach (ModuleInfo module in EnumerateModules())
                {
                    string clrName = Path.GetFileNameWithoutExtension(module.FileName).ToLower();

                    if (clrName != "clr" && clrName != "mscorwks" && clrName != "coreclr"
#if _REDHAWK
                        && clrName != "mrt100"
#endif
)
                        continue;

                    VersionInfo version = module.Version;
                    string dacFileName = GetDacRequestFileName(clrName, Architecture, version);

                    ModuleInfo dacInfo = new ModuleInfo(m_dataReader);
                    dacInfo.FileSize = module.FileSize;
                    dacInfo.TimeStamp = module.TimeStamp;
                    dacInfo.FileName = dacFileName;

                    if (versions.Length == count)
                    {
                        ClrInfo[] tmp = versions;
                        versions = new ClrInfo[count + 1];
                        Array.Copy(tmp, versions, tmp.Length);
                    }

                    string dacLocation = Path.Combine(Path.GetDirectoryName(module.FileName), "mscordacwks.dll");
                    if (!File.Exists(dacLocation) || !NativeMethods.IsEqualFileVersion(module.FileName, module.Version))
                        dacLocation = null;

                    ClrFlavor flavor;
                    switch (clrName)
                    {
#if _REDHAWK
                        case "mrt100":
                            flavor = ClrFlavor.Redhawk;
                            break;
#endif

                        case "coreclr":
                            flavor = ClrFlavor.CoreCLR;
                            break;

                        default:
                            flavor = ClrFlavor.Desktop;
                            break;
                    }


                    versions[count++] = new ClrInfo(this, flavor, version, dacInfo, dacLocation);
                }

                if (count != 0)
                    m_versions = versions;
                else
                    m_versions = new ClrInfo[0];

                Array.Sort(m_versions);
                return m_versions;
            }
        }

        public override bool ReadProcessMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead)
        {
            return m_dataReader.ReadMemory(address, buffer, bytesRequested, out bytesRead);
        }

        public override ClrRuntime CreateRuntime(string dacFilename)
        {
            if (IntPtr.Size != (int)m_dataReader.GetPointerSize())
                throw new InvalidOperationException("Mismatched architecture between this process and the dac.");

            if (string.IsNullOrEmpty(dacFilename))
                throw new ArgumentNullException("dacFilename");

            if (!File.Exists(dacFilename))
                throw new FileNotFoundException(dacFilename);

            DacLibrary lib = new DacLibrary(this, dacFilename);

            // TODO: There has to be a better way to determine this is coreclr.
            string dacFileNoExt = Path.GetFileNameWithoutExtension(dacFilename).ToLower();
            bool isCoreClr = dacFileNoExt.Contains("mscordaccore");

#if _REDHAWK
            bool isRedhawk = dacFileNoExt.Contains("mrt100");
#endif

            int major, minor, revision, patch;
            bool res = NativeMethods.GetFileVersion(dacFilename, out major, out minor, out revision, out patch);

            string version = string.Format("v{0}.{1}.{2}.{3}", major, minor, revision, patch);
            DesktopVersion ver;
            if (isCoreClr)
            {
                return new V45Runtime(this, lib);
            }
#if _REDHAWK
            else if (isRedhawk)
            {
                return new Redhawk.RhRuntime(this, lib);
            }
#endif
            else if (major == 2)
            {
                ver = DesktopVersion.v2;
            }
            else if (major == 4 && minor == 0 && patch < 10000)
            {
                ver = DesktopVersion.v4;
            }
            else
            {
                // Assume future versions will all work on the newest runtime version.
                return new V45Runtime(this, lib);
            }

            return new LegacyRuntime(this, lib, ver, patch);
        }

        public override ClrRuntime CreateRuntime(object clrDataProcess)
        {
            DacLibrary lib = new DacLibrary(this, (IXCLRDataProcess)clrDataProcess);

            // Figure out what version we are on.
            if (clrDataProcess is ISOSDac)
            {
                return new V45Runtime(this, lib);
            }
            else
            {
                byte[] buffer = new byte[Marshal.SizeOf(typeof(V2HeapDetails))];

                int val = lib.DacInterface.Request(DacRequests.GCHEAPDETAILS_STATIC_DATA, 0, null, (uint)buffer.Length, buffer);
                if ((uint)val == (uint)0x80070057)
                    return new LegacyRuntime(this, lib, DesktopVersion.v4, 10000);
                else
                    return new LegacyRuntime(this, lib, DesktopVersion.v2, 3054);
            }
        }

        public override IDebugClient DebuggerInterface
        {
            get { return m_client; }
        }

        public override IEnumerable<ModuleInfo> EnumerateModules()
        {
            if (m_modules == null)
                InitModules();

            return m_modules;
        }

        internal override string ResolveSymbol(ulong addr)
        {
            ModuleInfo module = FindModule(addr);
            if (module == null)
                return null;

            SymbolModule symbols;
            if (!m_symbols.TryGetValue(module, out symbols))
                symbols = FindPdbForModule(module);

            if (symbols == null)
                return null;

            return symbols.FindNameForRva((uint)(addr - module.ImageBase));
        }

        private ModuleInfo FindModule(ulong addr)
        {
            if (m_modules == null)
                InitModules();

            // TODO: Make binary search.
            foreach (var module in m_modules)
                if (module.ImageBase <= addr && addr < module.ImageBase + module.FileSize)
                    return module;

            return null;
        }

        private void InitModules()
        {
            if (m_modules == null)
            {
                var sortedModules = new List<ModuleInfo>(m_dataReader.EnumerateModules());
                sortedModules.Sort((a, b) => a.ImageBase.CompareTo(b.ImageBase));
                m_modules = sortedModules.ToArray();
            }
        }

        SymbolModule FindPdbForModule(ModuleInfo module)
        {
            if (module == null)
                return null;

            string pdbName;
            Guid pdbGuid;
            int rev;
            using (PEFile pefile = new PEFile(new ReadVirtualStream(m_dataReader, (long)module.ImageBase, (long)module.FileSize), true))
                if (!pefile.GetPdbSignature(out pdbName, out pdbGuid, out rev))
                    return null;

            if (!File.Exists(pdbName))
            {
                ISymbolNotification notification = DefaultSymbolNotification ?? new NullSymbolNotification();
                pdbName = Path.GetFileName(pdbName);
                pdbName = SymbolReader.FindSymbolFilePath(pdbName, pdbGuid, rev, notification);

                if (string.IsNullOrEmpty(pdbName) || !File.Exists(pdbName))
                    return null;
            }

            if (pdbName == null)
            {
                m_symbols[module] = null;
                return null;
            }

            SymbolModule symbols = null;
            try
            {
                symbols = new SymbolModule(SymbolReader, pdbName);
                m_symbols[module] = symbols;
            }
            catch
            {
                m_symbols[module] = null;
                return null;
            }

            return symbols;
        }




        public override void Dispose()
        {
            m_dataReader.Close();
        }
    }


    class DacLibrary
    {
        #region Variables
        IntPtr m_library;
        IDacDataTarget m_dacDataTarget;
        IXCLRDataProcess m_dac;
        ISOSDac m_sos;
        #endregion

        public IXCLRDataProcess DacInterface { get { return m_dac; } }

        public ISOSDac SOSInterface
        {
            get
            {
                if (m_sos == null)
                    m_sos = (ISOSDac)m_dac;

                return m_sos;
            }
        }

        public DacLibrary(DataTargetImpl dataTarget, object ix)
        {
            m_dac = ix as IXCLRDataProcess;
            if (m_dac == null)
                throw new ArgumentException("clrDataProcess not an instance of IXCLRDataProcess");
        }

        public DacLibrary(DataTargetImpl dataTarget, string dacDll)
        {
            if (dataTarget.ClrVersions.Count == 0)
                throw new ClrDiagnosticsException(String.Format("Process is not a CLR process!"));

            m_library = NativeMethods.LoadLibrary(dacDll);
            if (m_library == IntPtr.Zero)
                throw new ClrDiagnosticsException("Failed to load dac: " + dacDll);

            IntPtr addr = NativeMethods.GetProcAddress(m_library, "CLRDataCreateInstance");
            m_dacDataTarget = new DacDataTarget(dataTarget);

            object obj;
            NativeMethods.CreateDacInstance func = (NativeMethods.CreateDacInstance)Marshal.GetDelegateForFunctionPointer(addr, typeof(NativeMethods.CreateDacInstance));
            Guid guid = new Guid("5c552ab6-fc09-4cb3-8e36-22fa03c798b7");
            int res = func(ref guid, m_dacDataTarget, out obj);

            if (res == 0)
                m_dac = obj as IXCLRDataProcess;

            if (m_dac == null)
                throw new ClrDiagnosticsException("Failure loading DAC: CreateDacInstance failed 0x" + res.ToString("x"), ClrDiagnosticsException.HR.DacError);
        }

        ~DacLibrary()
        {
            if (m_library != IntPtr.Zero)
                NativeMethods.FreeLibrary(m_library);
        }
    }

    class DacDataTarget : IDacDataTarget, IMetadataLocator
    {
        DataTargetImpl m_dataTarget;
        IDataReader m_dataReader;

        public DacDataTarget(DataTargetImpl dataTarget)
        {
            m_dataTarget = dataTarget;
            m_dataReader = m_dataTarget.DataReader;
        }


        public void GetMachineType(out IMAGE_FILE_MACHINE machineType)
        {
            var arch = m_dataReader.GetArchitecture();

            switch (arch)
            {
                case Architecture.Amd64:
                    machineType = IMAGE_FILE_MACHINE.AMD64;
                    break;

                case Architecture.X86:
                    machineType = IMAGE_FILE_MACHINE.I386;
                    break;

                case Architecture.Arm:
                    machineType = IMAGE_FILE_MACHINE.THUMB2;
                    break;

                default:
                    machineType = IMAGE_FILE_MACHINE.UNKNOWN;
                    break;
            }
        }

        public void GetPointerSize(out uint pointerSize)
        {
            pointerSize = m_dataReader.GetPointerSize();
        }

        public void GetImageBase(string imagePath, out ulong baseAddress)
        {
            imagePath = Path.GetFileNameWithoutExtension(imagePath);

            foreach (var module in m_dataTarget.EnumerateModules())
            {
                string moduleName = Path.GetFileNameWithoutExtension(module.FileName);
                if (imagePath.Equals(moduleName, StringComparison.CurrentCultureIgnoreCase))
                {
                    baseAddress = module.ImageBase;
                    return;
                }
            }

            throw new Exception();
        }

        public int ReadMemory(ulong address, byte[] buffer, uint bytesRequested, out uint bytesRead)
        {
            int read = 0;
            if (m_dataReader.ReadMemory(address, buffer, (int)bytesRequested, out read))
            {
                bytesRead = (uint)read;
                return 0;
            }

            bytesRead = 0;
            return -1;
        }

        public int ReadVirtual(ulong address, byte[] buffer, uint bytesRequested, out uint bytesRead)
        {
            return ReadMemory(address, buffer, bytesRequested, out bytesRead);
        }

        public void WriteVirtual(ulong address, byte[] buffer, uint bytesRequested, out uint bytesWritten)
        {
            throw new NotImplementedException();
        }

        public void GetTLSValue(uint threadID, uint index, out ulong value)
        {
            // TODO:  Validate this is not used?
            value = 0;
        }

        public void SetTLSValue(uint threadID, uint index, ulong value)
        {
            throw new NotImplementedException();
        }

        public void GetCurrentThreadID(out uint threadID)
        {
            throw new NotImplementedException();
        }

        public void GetThreadContext(uint threadID, uint contextFlags, uint contextSize, IntPtr context)
        {
            m_dataReader.GetThreadContext(threadID, contextFlags, contextSize, context);
        }

        public void SetThreadContext(uint threadID, uint contextSize, IntPtr context)
        {
            throw new NotImplementedException();
        }

        public void Request(uint reqCode, uint inBufferSize, IntPtr inBuffer, IntPtr outBufferSize, out IntPtr outBuffer)
        {
            throw new NotImplementedException();
        }

        public int GetMetadata(string filename, uint imageTimestamp, uint imageSize, IntPtr mvid, uint mdRva, uint flags, uint bufferSize, byte[] buffer, IntPtr dataSize)
        {
            filename = FindImage(filename, imageTimestamp, imageSize);

            if (filename == null)
                return -1;

            if (!File.Exists(filename))
                return -1;

            try
            {
                using (FileStream file = File.OpenRead(filename))
                {
                    using (SafeWin32Handle handle = NativeMethods.CreateFileMapping(file.SafeFileHandle, IntPtr.Zero, NativeMethods.PageProtection.Readonly, 0, 0, null))
                    {
                        if (handle.IsInvalid)
                            return -1;

                        using (SafeMapViewHandle image = NativeMethods.MapViewOfFile(handle, NativeMethods.FILE_MAP_READ, 0, 0, IntPtr.Zero))
                        {
                            if (image.IsInvalid)
                                return -1;

                            if (mdRva == 0)
                            {
                                uint size;
                                IntPtr header = NativeMethods.ImageDirectoryEntryToData(image.BaseAddress, false,
                                    NativeMethods.IMAGE_DIRECTORY_ENTRY_COM_DESCRIPTOR,
                                    out size);

                                if (header == IntPtr.Zero)
                                    return -1;

                                IMAGE_COR20_HEADER corhdr = (IMAGE_COR20_HEADER)Marshal.PtrToStructure(header, typeof(IMAGE_COR20_HEADER));
                                if (bufferSize < corhdr.MetaData.Size)
                                    return -1;

                                mdRva = corhdr.MetaData.VirtualAddress;
                                bufferSize = corhdr.MetaData.Size;
                            }


                            IntPtr ntHeader = NativeMethods.ImageNtHeader(image.BaseAddress);
                            IntPtr addr = NativeMethods.ImageRvaToVa(ntHeader, image.BaseAddress, mdRva, IntPtr.Zero);
                            Marshal.Copy(addr, buffer, 0, (int)bufferSize);

                            return 0;
                        }
                    }
                }
            }
            catch
            {
                Debug.Assert(false);
            }

            return -1;
        }

        private string FindImage(string image, uint imageTimestamp, uint imageSize)
        {
            // Test file on disk.
            if (File.Exists(image))
            {
                try
                {
                    using (PEFile file = new PEFile(image))
                    {
                        var header = file.Header;
                        if (header.TimeDateStampSec == (int)imageTimestamp && header.SizeOfImage == imageSize)
                            return image;
                    }
                }
                catch
                {
                    // Ignore any exceptions when trying to determine image and file timestamp.
                    Debug.Assert(false);
                }
            }

            try
            {
                image = Path.GetFileName(image);
            }
            catch (ArgumentException)
            {
                return null;
            }

            // Try symbol server instead.
            return m_dataTarget.TryDownloadFile(image, (int)imageTimestamp, (int)imageSize, null);
        }
    }
}
