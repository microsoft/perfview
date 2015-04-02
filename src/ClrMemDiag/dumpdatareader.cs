using Microsoft.Diagnostics.Runtime.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Microsoft.Diagnostics.Runtime
{
    unsafe class DumpDataReader : IDataReader, IDisposable
    {
        private string m_fileName;
        private DumpReader m_dumpReader;
        private List<ModuleInfo> m_modules;
        private string m_generatedPath;

        public DumpDataReader(string file)
        {
            if (!File.Exists(file))
                throw new FileNotFoundException(file);

            if (Path.GetExtension(file).ToLower() == ".cab")
                file = ExtractCab(file);

            m_fileName = file;
            m_dumpReader = new DumpReader(file);
        }

        ~DumpDataReader()
        {
            Dispose();
        }

        private string ExtractCab(string file)
        {
            m_generatedPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            while (Directory.Exists(m_generatedPath))
                m_generatedPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

            Directory.CreateDirectory(m_generatedPath);

            CommandOptions options = new CommandOptions();
            options.NoThrow = true;
            options.NoWindow = true;
            Command cmd = Command.Run(string.Format("expand -F:*dmp {0} {1}", file, m_generatedPath), options);

            bool error = false;
            if (cmd.ExitCode != 0)
            {
                error = true;
            }
            else
            {
                file = null;
                foreach (var item in Directory.GetFiles(m_generatedPath))
                {
                    string ext = Path.GetExtension(item).ToLower();
                    if (ext == ".dll" || ext == ".pdb" || ext == ".exe")
                        continue;

                    file = item;
                    break;
                }


                error |= file == null;
            }

            if (error)
            {
                Dispose();
                throw new IOException("Failed to extract a crash dump from " + file);
            }

            return file;
        }


        public bool IsMinidump
        {
            get
            {
                return m_dumpReader.IsMinidump;
            }
        }

        public override string ToString()
        {
            return m_fileName;
        }

        public void Close()
        {
            m_dumpReader.Dispose();
            Dispose();
        }

        public void Dispose()
        {
            if (m_generatedPath != null)
            {
                try
                {
                    foreach (var file in Directory.GetFiles(m_generatedPath))
                        File.Delete(file);

                    Directory.Delete(m_generatedPath, false);
                }
                catch
                {
                }

                m_generatedPath = null;
            }
        }

        public void Flush()
        {
            m_modules = null;
        }

        public Architecture GetArchitecture()
        {
            switch (m_dumpReader.ProcessorArchitecture)
            {
                case ProcessorArchitecture.PROCESSOR_ARCHITECTURE_ARM:
                    return Architecture.Arm;

                case ProcessorArchitecture.PROCESSOR_ARCHITECTURE_AMD64:
                    return Architecture.Amd64;

                case ProcessorArchitecture.PROCESSOR_ARCHITECTURE_INTEL:
                    return Architecture.X86;
            }

            return Architecture.Unknown;
        }

        public uint GetPointerSize()
        {
            switch (GetArchitecture())
            {
                case Architecture.Amd64:
                    return 8;

                default:
                    return 4;
            }
        }

        public IList<ModuleInfo> EnumerateModules()
        {
            if (m_modules != null)
                return m_modules;

            List<ModuleInfo> modules = new List<ModuleInfo>();

            foreach (var mod in m_dumpReader.EnumerateModules())
            {
                var raw = mod.Raw;

                ModuleInfo module = new ModuleInfo(this);
                module.FileName = mod.FullName;
                module.ImageBase = raw.BaseOfImage;
                module.FileSize = raw.SizeOfImage;
                module.TimeStamp = raw.TimeDateStamp;

                module.Version = GetVersionInfo(mod);
                modules.Add(module);
            }

            m_modules = modules;
            return modules;
        }

        public void GetVersionInfo(ulong baseAddress, out VersionInfo version)
        {
            DumpModule module = m_dumpReader.TryLookupModuleByAddress(baseAddress);
            version = (module != null) ? GetVersionInfo(module) : new VersionInfo();
        }

        private static VersionInfo GetVersionInfo(DumpModule module)
        {
            var raw = module.Raw;
            var version = raw.VersionInfo;
            int minor = (ushort)version.dwFileVersionMS;
            int major = (ushort)(version.dwFileVersionMS >> 16);
            int patch = (ushort)version.dwFileVersionLS;
            int rev = (ushort)(version.dwFileVersionLS >> 16);

            var versionInfo = new VersionInfo(major, minor, rev, patch);
            return versionInfo;
        }


        byte[] m_ptrBuffer = new byte[IntPtr.Size];
        public ulong ReadPointerUnsafe(ulong addr)
        {
            return m_dumpReader.ReadPointerUnsafe(addr);
        }

        public uint ReadDwordUnsafe(ulong addr)
        {
            return m_dumpReader.ReadDwordUnsafe(addr);
        }


        public bool ReadMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead)
        {
            bytesRead = m_dumpReader.ReadPartialMemory(address, buffer, bytesRequested);
            
            return bytesRead != 0;
        }

        public ulong GetThreadTeb(uint id)
        {
            var thread = m_dumpReader.GetThread((int)id);
            if (thread == null)
                return 0;

            return thread.Teb;
        }

        public IEnumerable<uint> EnumerateAllThreads()
        {
            foreach (var dumpThread in m_dumpReader.EnumerateThreads())
                yield return (uint)dumpThread.ThreadId;
        }

        public bool VirtualQuery(ulong addr, out VirtualQueryData vq)
        {
            return m_dumpReader.VirtualQuery(addr, out vq);
        }

        public bool GetThreadContext(uint id, uint contextFlags, uint contextSize, IntPtr context)
        {
            var thread = m_dumpReader.GetThread((int)id);
            if (thread == null)
                return false;

            thread.GetThreadContext(context, (int)contextSize);
            return true;
        }

        public bool CanReadAsync
        {
            get { return true; }
        }

        public AsyncMemoryReadResult ReadMemoryAsync(ulong address, int bytesRequested)
        {
            AsyncMemoryReadResult result = new AsyncMemoryReadResult(address, bytesRequested);
            ThreadPool.QueueUserWorkItem(QueueMemoryRead, result);
            return result;
        }

        private void QueueMemoryRead(object state)
        {
            m_dumpReader.ReadMemory((AsyncMemoryReadResult)state);
        }


        public bool ReadMemory(ulong address, IntPtr buffer, int bytesRequested, out int bytesRead)
        {
            throw new NotImplementedException();
        }

        public bool GetThreadContext(uint threadID, uint contextFlags, uint contextSize, byte[] context)
        {
            throw new NotImplementedException();
        }
    }
}
