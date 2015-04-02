using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Address = System.UInt64;
using System.Text;
using System.Collections;
using System.IO;
using System.Reflection;
using Microsoft.Diagnostics.Runtime;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime.Utilities;
using Dia2Lib;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    abstract class DesktopBaseModule : ClrModule
    {
        internal abstract Address GetDomainModule(ClrAppDomain appDomain);

        internal Address ModuleId { get; set; }

        internal virtual IMetadata GetMetadataImport()
        {
            return null;
        }

        public int Revision { get; set; }
    }

    class DesktopModule : DesktopBaseModule
    {
        bool m_reflection, m_isPE;
        string m_name, m_assemblyName;
        DesktopRuntimeBase m_runtime;
        IMetadata m_metadata;
        Dictionary<ClrAppDomain, ulong> m_mapping = new Dictionary<ClrAppDomain, ulong>();
        Address m_imageBase, m_size;
        private Address m_metadataStart;
        private Address m_metadataLength;
        DebuggableAttribute.DebuggingModes? m_debugMode;
        private Address m_address;
        private Address m_assemblyAddress;
        private bool m_typesLoaded;
        SymbolModule m_symbols;
        PEFile m_peFile;


        public override SourceLocation GetSourceInformation(ClrMethod method, int ilOffset)
        {
            if (method == null)
                throw new ArgumentNullException("method");

            if (method.Type != null && method.Type.Module != this)
                throw new InvalidOperationException("Method not in this module.");

            return GetSourceInformation(method.MetadataToken, ilOffset);
        }

        public override SourceLocation GetSourceInformation(uint token, int ilOffset)
        {
            if (m_symbols == null)
                return null;

            return m_symbols.SourceLocationForManagedCode(token, ilOffset);
        }

        public override bool IsPdbLoaded { get { return m_symbols != null; } }

        public override bool IsMatchingPdb(string pdbPath)
        {
            if (m_peFile == null)
                m_peFile = new PEFile(new ReadVirtualStream(m_runtime.DataReader, (long)m_imageBase, (long)m_size), true);

            string pdbName;
            Guid pdbGuid;
            int rev;
            if (!m_peFile.GetPdbSignature(out pdbName, out pdbGuid, out rev))
                throw new ClrDiagnosticsException("Failed to get PDB signature from module.", ClrDiagnosticsException.HR.DataRequestError);
                
            IDiaDataSource source = DiaLoader.GetDiaSourceObject();
            IDiaSession session;
            source.loadDataFromPdb(pdbPath);
            source.openSession(out session);
            return pdbGuid == session.globalScope.guid;
        }

        public override void LoadPdb(string path)
        {
            m_symbols = new SymbolModule(m_runtime.DataTarget.SymbolReader, path);
        }

        public override string TryDownloadPdb(ISymbolNotification notification)
        {
            var dataTarget = m_runtime.DataTarget;
            if (notification == null)
                notification = dataTarget.DefaultSymbolNotification ?? new NullSymbolNotification();

            if (m_peFile == null)
                m_peFile = new PEFile(new ReadVirtualStream(m_runtime.DataReader, (long)m_imageBase, (long)m_size), true);
            
            string pdbName;
            Guid pdbGuid;
            int rev;
            if (!m_peFile.GetPdbSignature(out pdbName, out pdbGuid, out rev))
                throw new ClrDiagnosticsException("Failed to get PDB signature from module.", ClrDiagnosticsException.HR.DataRequestError);

            if (File.Exists(pdbName))
                return pdbName;

            var reader = dataTarget.SymbolReader;
            return reader.FindSymbolFilePath(pdbName, pdbGuid, rev, notification);
        }


        public DesktopModule(DesktopRuntimeBase runtime, ulong address, IModuleData data, string name, string assemblyName, ulong size)
        {
            Revision = runtime.Revision;
            m_imageBase = data.ImageBase;
            m_runtime = runtime;
            m_assemblyName = assemblyName;
            m_isPE = data.IsPEFile;
            m_reflection = data.IsReflection || string.IsNullOrEmpty(name) || !name.Contains("\\");
            m_name = name;
            ModuleId = data.ModuleId;
            ModuleIndex = data.ModuleIndex;
            m_metadataStart = data.MetdataStart;
            m_metadataLength = data.MetadataLength;
            m_assemblyAddress = data.Assembly;
            m_address = address;
            m_size = size;

            // This is very expensive in the minidump case, as we may be heading out to the symbol server or
            // reading multiple files from disk. Only optimistically fetch this data if we have full memory.
            if (!runtime.DataReader.IsMinidump)
                m_metadata = data.LegacyMetaDataImport as IMetadata;
        }

        public override IEnumerable<ClrType> EnumerateTypes()
        {
            var heap = (DesktopGCHeap)m_runtime.GetHeap();
            var mtList = m_runtime.GetMethodTableList(m_address);
            if (m_typesLoaded)
            {
                foreach (var type in heap.EnumerateTypes())
                    if (type.Module == this)
                        yield return type;
            }
            else
            {
                if (mtList != null)
                {
                    foreach (ulong mt in mtList)
                    {
                        if (mt != m_runtime.ArrayMethodTable)
                        {
                            // prefetch element type, as this also can load types
                            var type = heap.GetGCHeapType(mt, 0, 0);
                            if (type != null)
                                yield return type;
                        }
                    }
                }

                m_typesLoaded = true;
            }

            
        }

        public override string AssemblyName
        {
            get { return m_assemblyName; }
        }

        public override string Name
        {
            get { return m_name; }
        }

        public override bool IsDynamic
        {
            get { return m_reflection; }
        }

        public override bool IsFile
        {
            get { return m_isPE; }
        }

        public override string FileName
        {
            get { return m_isPE ? m_name : null; }
        }

        internal ulong ModuleIndex { get; private set; }

        internal void AddMapping(ClrAppDomain domain, ulong domainModule)
        {
            DesktopAppDomain appDomain = (DesktopAppDomain)domain;
            m_mapping[domain] = domainModule;
        }

        internal override ulong GetDomainModule(ClrAppDomain domain)
        {
            m_runtime.InitDomains();
            if (domain == null)
            {
                foreach (ulong addr in m_mapping.Values)
                    return addr;

                return 0;
            }

            ulong value;
            if (m_mapping.TryGetValue(domain, out value))
                return value;

            return 0;
        }

        internal override IMetadata GetMetadataImport()
        {
            if (Revision != m_runtime.Revision)
                ClrDiagnosticsException.ThrowRevisionError(Revision, m_runtime.Revision);

            if (m_metadata != null)
                return m_metadata;

            ulong module = GetDomainModule(null);
            if (module == 0)
                return null;

            m_metadata = m_runtime.GetMetadataImport(module);
            return m_metadata;
        }

        public override Address ImageBase
        {
            get { return m_imageBase; }
        }


        public override Address Size
        {
            get
            {
                return m_size;
            }
        }

        internal void SetImageSize(Address size)
        {
            m_size = size;
        }


        public override Address MetadataAddress
        {
            get { return m_metadataStart; }
        }

        public override Address MetadataLength
        {
            get { return m_metadataLength; }
        }

        public override object MetadataImport
        {
            get { return GetMetadataImport(); }
        }

        public override DebuggableAttribute.DebuggingModes DebuggingMode
        {
            get
            {
                if (m_debugMode == null)
                    InitDebugAttributes();

                Debug.Assert(m_debugMode != null);
                return m_debugMode.Value;
            }
        }

        void InitDebugAttributes()
        {
            IMetadata metadata = GetMetadataImport();
            if (metadata == null)
            {
                m_debugMode = DebuggableAttribute.DebuggingModes.None;
                return;
            }

            try
            {
                IntPtr data;
                uint cbData;
                int hr = metadata.GetCustomAttributeByName(0x20000001, "System.Diagnostics.DebuggableAttribute", out data, out cbData);
                if (hr != 0 || cbData <= 4)
                {
                    m_debugMode = DebuggableAttribute.DebuggingModes.None;
                    return;
                }

                unsafe
                {
                    byte* b = (byte*)data.ToPointer();
                    UInt16 opt = b[2];
                    UInt16 dbg = b[3];

                    m_debugMode = (System.Diagnostics.DebuggableAttribute.DebuggingModes)((dbg << 8) | opt);
                }
            }
            catch (SEHException)
            {
                m_debugMode = DebuggableAttribute.DebuggingModes.None;
            }
        }

        public override ClrType GetTypeByName(string name)
        {
            foreach (ClrType type in EnumerateTypes())
                if (type.Name == name)
                    return type;

            return null;
        }

        public override Address AssemblyId
        {
            get { return m_assemblyAddress; }
        }
    }

    class ErrorModule : DesktopBaseModule
    {
        static uint s_id = 0;
        uint m_id = s_id++;

        public override string AssemblyName
        {
            get { return "<error>"; }
        }

        public override string Name
        {
            get { return "<error>"; }
        }

        public override bool IsDynamic
        {
            get { return false; }
        }

        public override bool IsFile
        {
            get { return false; }
        }

        public override string FileName
        {
            get { return "<error>"; }
        }

        public override Address ImageBase
        {
            get { return 0; }
        }

        public override Address Size
        {
            get { return 0; }
        }

        public override IEnumerable<ClrType> EnumerateTypes()
        {
            return new ClrType[0];
        }

        public override Address MetadataAddress
        {
            get { return 0; }
        }

        public override Address MetadataLength
        {
            get { return 0; }
        }

        public override object MetadataImport
        {
            get { return null; }
        }

        internal override Address GetDomainModule(ClrAppDomain appDomain)
        {
            return 0;
        }

        public override DebuggableAttribute.DebuggingModes DebuggingMode
        {
            get { return DebuggableAttribute.DebuggingModes.None; }
        }

        public override ClrType GetTypeByName(string name)
        {
            return null;
        }

        public override Address AssemblyId
        {
            get { return m_id; }
        }

        public override bool IsPdbLoaded
        {
            get { return false; }
        }

        public override bool IsMatchingPdb(string pdbPath)
        {
            return false;
        }

        public override void LoadPdb(string path)
        {
        }

        public override string TryDownloadPdb(ISymbolNotification notification)
        {
            return null;
        }

        public override SourceLocation GetSourceInformation(uint token, int ilOffset)
        {
            return null;
        }

        public override SourceLocation GetSourceInformation(ClrMethod method, int ilOffset)
        {
            return null;
        }
    }
}
