#if _REDHAWK
using Microsoft.Diagnostics.Runtime.Desktop;
using System;
using System.Collections.Generic;
using System.IO;
using Address = System.UInt64;

namespace Microsoft.Diagnostics.Runtime.Redhawk
{
    class RhHeap : HeapBase
    {
        internal RhRuntime m_runtime;
        internal TextWriter Log { get; set; }
        ulong m_lastObj;
        ClrType m_lastType;
        Dictionary<ulong, int> m_indices = new Dictionary<ulong,int>();
        List<RhType> m_types = new List<RhType>(1024);
        RhModule[] m_modules;
        RhModule m_mrtModule;
        RhType m_free;

        internal RhHeap(RhRuntime runtime, RhModule[] modules, TextWriter log)
            : base(runtime)
        {
            Log = log;
            m_runtime = runtime;
            m_modules = modules;
            m_mrtModule = FindMrtModule();

            CreateFreeType();
            InitSegments(runtime);
        }

        public override ClrRuntime GetRuntime() { return m_runtime; }

        public override int TypeIndexLimit
        {
            get { return m_types.Count; }
        }

        public override ClrType GetTypeByIndex(int index)
        {
            return m_types[index];
        }

        private RhModule FindMrtModule()
        {
            foreach (RhModule module in m_modules)
                if (string.Compare(module.Name, "mrt100", StringComparison.CurrentCultureIgnoreCase) == 0 ||
                    string.Compare(module.Name, "mrt100_app", StringComparison.CurrentCultureIgnoreCase) == 0)
                    return module;

            return null;
        }

        private void CreateFreeType()
        {
            ulong free = m_runtime.GetFreeType();
            IMethodTableData mtData = m_runtime.GetMethodTableData(free);
            m_free = new RhType(this, m_types.Count, m_mrtModule, "Free", free, mtData);
            m_indices[free] = m_types.Count;
            m_types.Add(m_free);
        }

        public override ClrType GetObjectType(ulong objRef)
        {
            ulong eeType;

            if (m_lastObj == objRef)
                return m_lastType;

            var cache = MemoryReader;
            if (!cache.Contains(objRef))
                cache = m_runtime.MemoryReader;

            if (!cache.ReadPtr(objRef, out eeType))
                return null;

            if ((((int)eeType) & 3) != 0)
                eeType &= ~3UL;

            ClrType last = null;
            int index;
            if (m_indices.TryGetValue(eeType, out index))
                last = m_types[index];
            else
                last = ConstructObjectType(eeType);

            m_lastObj = objRef;
            m_lastType = last;
            return last;
        }

        private ClrType ConstructObjectType(ulong eeType)
        {
            IMethodTableData mtData = m_runtime.GetMethodTableData(eeType);
            if (mtData == null)
                return null;

            ulong componentType = mtData.ElementTypeHandle;
            bool isArray = componentType != 0;

            // EEClass is the canonical method table.  I stuffed the pointer there instead of creating a new property.
            ulong canonType = isArray ? componentType : mtData.EEClass;
            if (!isArray && canonType != 0)
            {
                int index;
                if (!isArray && m_indices.TryGetValue(canonType, out index))
                {
                    m_indices[eeType] = index;  // Link the original eeType to its canonical GCHeapType.
                    return m_types[index];
                }

                ulong tmp = eeType;
                eeType = canonType;
                canonType = tmp;
            }

            string name = m_runtime.ResolveSymbol(eeType);
            if (string.IsNullOrEmpty(name))
            {
                name = m_runtime.ResolveSymbol(canonType);
                if (name == null)
                    name = string.Format("unknown type {0:x}", eeType);
            }


            int len = name.Length;
            if (name.EndsWith("::`vftable'"))
                len -= 11;

            int i = name.IndexOf('!') + 1;
            name = name.Substring(i, len - i);
            
            if (isArray)
                name += "[]";

            RhModule module = FindContainingModule(eeType);
            if (module == null && canonType != 0)
                module = FindContainingModule(canonType);

            if (module == null)
                module = m_mrtModule;

            RhType type = new RhType(this, m_types.Count, module, name, eeType, mtData);
            m_indices[eeType] = m_types.Count;
            if (!isArray)
                m_indices[canonType] = m_types.Count;
            m_types.Add(type);

            return type;
        }

        private RhModule FindContainingModule(Address eeType)
        {
            int min = 0, max = m_modules.Length;

            while (min <= max)
            {
                int mid = (min + max) / 2;

                int compare = m_modules[mid].ComparePointer(eeType);
                if (compare < 0)
                    max = mid - 1;
                else if (compare > 0)
                    min = mid + 1;
                else
                    return m_modules[mid];
            }

            return null;
        }

        public override IEnumerable<ClrRoot> EnumerateRoots()
        {
            return EnumerateRoots(true);
        }

        
        public override IEnumerable<ClrRoot> EnumerateRoots(bool enumerateStatics)
        {
            // Stack objects.
            foreach (var thread in m_runtime.Threads)
                foreach (var stackRef in m_runtime.EnumerateStackRoots(thread))
                    yield return stackRef;

            // Static Variables.
            foreach (var root in m_runtime.EnumerateStaticRoots(enumerateStatics))
                yield return root;

            // Handle Table.
            foreach (ClrRoot root in m_runtime.EnumerateHandleRoots())
                yield return root;

            // Finalizer Queue.
            ClrAppDomain domain = m_runtime.AppDomains[0];
            foreach (ulong obj in m_runtime.EnumerateFinalizerQueue())
            {
                ClrType type = GetObjectType(obj);
                if (type == null)
                    continue;

                yield return new RhFinalizerRoot(obj, type, domain, "finalizer root");
            }
        }

        public override int ReadMemory(Address address, byte[] buffer, int offset, int count)
        {
            if (offset != 0)
                throw new NotImplementedException("Non-zero offsets not supported (yet)");

            int bytesRead = 0;
            if (!m_runtime.ReadMemory(address, buffer, count, out bytesRead))
                return 0;
            return bytesRead;
        }

        public override IEnumerable<ClrType> EnumerateTypes() { return null; }
        public override IEnumerable<Address> EnumerateFinalizableObjects() { throw new NotImplementedException(); }
        public override IEnumerable<BlockingObject> EnumerateBlockingObjects() { throw new NotImplementedException(); }
        public override ClrException GetExceptionObject(Address objRef) { throw new NotImplementedException(); }

        protected override int GetRuntimeRevision()
        {
            return 0;
        }
    }


    class RhFinalizerRoot : ClrRoot
    {
        private string m_name;
        private ClrType m_type;
        private ClrAppDomain m_appDomain;

        public override GCRootKind Kind
        {
            get { return GCRootKind.Finalizer; }
        }

        public override ClrType Type
        {
            get { return m_type; }
        }

        public override string Name
        {
            get
            {
                return m_name;
            }
        }

        public override ClrAppDomain AppDomain
        {
            get
            {
                return m_appDomain;
            }
        }

        public RhFinalizerRoot(Address obj, ClrType type, ClrAppDomain domain, string name)
        {
            Object = obj;
            m_name = name;
            m_type = type;
            m_appDomain = domain;
        }
    }
}
#endif