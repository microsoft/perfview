#if _REDHAWK
using System;
using System.Collections.Generic;
using System.IO;
using Address = System.UInt64;

namespace Microsoft.Diagnostics.Runtime.Redhawk
{
    class RhAppDomain : ClrAppDomain
    {
        private IList<ClrModule> m_modules;

        public RhAppDomain(IList<ClrModule> modules)
        {
            m_modules = modules;
        }

        public override Address Address
        {
            get { return 0; }
        }

        public override int Id
        {
            get { return 0; }
        }

        public override string Name
        {
            get { return "default domain"; }
        }

        public override IList<ClrModule> Modules
        {
            get { return m_modules; }
        }

        public override string ConfigurationFile
        {
            get { return null; }
        }

        public override string AppBase
        {
            get { return null; }
        }
    }


    class RhModule : ClrModule
    {
        RhRuntime m_runtime;
        string m_name;
        string m_filename;
        private Address m_imageBase;
        private Address m_size;

        public RhModule(RhRuntime runtime, ModuleInfo module)
        {
            m_runtime = runtime;
            m_name = string.IsNullOrEmpty(module.FileName) ? "" : Path.GetFileNameWithoutExtension(module.FileName);
            m_filename = module.FileName;
            m_imageBase = module.ImageBase;
            m_size = module.FileSize;
        }

        public override string AssemblyName
        {
            get { return m_name; }
        }

        public override string Name
        {
            get { return m_name; }
        }

        public override bool IsDynamic
        {
            get { return false; }
        }

        public override bool IsFile
        {
            get { return true; }
        }

        public override string FileName
        {
            get { return m_filename; }
        }

        public override Address ImageBase
        {
            get { return m_imageBase; }
        }

        public override Address Size
        {
            get { return m_size; }
        }

        public override IEnumerable<ClrType> EnumerateTypes()
        {
            foreach (var type in m_runtime.GetHeap().EnumerateTypes())
                if (type.Module == this)
                    yield return type;
        }

        internal int ComparePointer(Address eetype)
        {
            if (eetype < ImageBase)
                return -1;

            if (eetype >= ImageBase + Size)
                return 1;

            return 0;
        }

        public override Address MetadataAddress
        {
            get { throw new NotImplementedException(); }
        }

        public override Address MetadataLength
        {
            get { throw new NotImplementedException(); }
        }

        public override object MetadataImport
        {
            get { throw new NotImplementedException(); }
        }

        public override System.Diagnostics.DebuggableAttribute.DebuggingModes DebuggingMode
        {
            get { throw new NotImplementedException(); }
        }

        public override ClrType GetTypeByName(string name)
        {
            throw new NotImplementedException();
        }

        public override Address AssemblyId
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsPdbLoaded
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsMatchingPdb(string pdbPath)
        {
            throw new NotImplementedException();
        }

        public override void LoadPdb(string path)
        {
            throw new NotImplementedException();
        }

        public override string TryDownloadPdb(ISymbolNotification notification)
        {
            throw new NotImplementedException();
        }

        public override SourceLocation GetSourceInformation(uint token, int ilOffset)
        {
            throw new NotImplementedException();
        }

        public override SourceLocation GetSourceInformation(ClrMethod method, int ilOffset)
        {
            throw new NotImplementedException();
        }
    }
}
#endif