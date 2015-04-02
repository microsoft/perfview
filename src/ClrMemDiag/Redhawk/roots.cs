#if _REDHAWK
using System;
using System.Collections.Generic;
using Address = System.UInt64;

namespace Microsoft.Diagnostics.Runtime.Redhawk
{
    class RhStackRootWalker
    {
        private ClrHeap m_heap;
        private ClrAppDomain m_domain;
        private ClrThread m_thread;
        public List<ClrRoot> Roots { get; set; }

        public RhStackRootWalker(ClrHeap heap, ClrAppDomain domain, ClrThread thread)
        {
            m_heap = heap;
            m_domain = domain;
            m_thread = thread;
            Roots = new List<ClrRoot>();
        }

        public void Callback(IntPtr token, ulong symbol, ulong addr, ulong obj, int pinned, int interior)
        {
            string name = "local variable";
            RhStackRoot root = new RhStackRoot(m_thread, addr, obj, name, m_heap.GetObjectType(obj), m_domain, pinned != 0, interior != 0);
            Roots.Add(root);
        }
    }

    class RhHandleRootWalker
    {
        public List<ClrRoot> Roots { get; set; }
        ClrHeap m_heap;
        ClrAppDomain m_domain;
        bool m_dependentSupport;

        public RhHandleRootWalker(RhRuntime runtime, bool dependentHandleSupport)
        {
            m_heap = runtime.GetHeap();
            m_domain = runtime.GetRhAppDomain();
            m_dependentSupport = dependentHandleSupport;
        }

        public void RootCallback(IntPtr ptr, ulong addr, ulong obj, int hndType, uint refCount, int strong)
        {
            bool isDependent = hndType == (int)HandleType.Dependent;
            if ((isDependent && m_dependentSupport) || strong != 0)
            {
                if (Roots == null)
                    Roots = new List<ClrRoot>(128);

                string name = Enum.GetName(typeof(HandleType), hndType) + " handle";
                if (isDependent)
                {
                    ulong dependentTarget = obj;
                    if (!m_heap.ReadPointer(addr, out obj))
                        obj = 0;

                    ClrType type = m_heap.GetObjectType(obj);
                    Roots.Add(new RhHandleRoot(addr, obj, dependentTarget, type, hndType, m_domain, name));
                }
                else
                {
                    ClrType type = m_heap.GetObjectType(obj);
                    Roots.Add(new RhHandleRoot(addr, obj, type, hndType, m_domain, name));
                }
            }
        }
    }

    class RhStaticRootWalker
    {
        public List<ClrRoot> Roots { get; set; }
        RhRuntime m_runtime;
        ClrHeap m_heap;

        public RhStaticRootWalker(RhRuntime runtime, bool resolveStatics)
        {
            Roots = new List<ClrRoot>(128);
            m_runtime = resolveStatics ? runtime : null;
            m_heap = m_runtime.GetHeap();
        }

        public void Callback(IntPtr token, ulong addr, ulong obj, int pinned, int interior)
        {
            string name = name = m_runtime.ResolveSymbol(addr);
            if (name == null)
            {
                name = addr.ToString("X");
            }
            else
            {
                int index = name.IndexOf('!');
                if (index >= 0)
                    name = name.Substring(index + 1);

                name = string.Format("{0:X}: {1}", addr, name);
            }
            
            name = "static var " + name;
            var type = m_heap.GetObjectType(obj);
            Roots.Add(new RhStaticVar(m_runtime, addr, obj, type, name, interior != 0, pinned != 0));
        }
    }

    class RhStackRoot : ClrRoot
    {
        string m_name;
        ClrType m_type;
        ClrAppDomain m_appDomain;
        ClrThread m_thread;
        private bool m_pinned;
        private bool m_interior;
        
        public override GCRootKind Kind
        {
            get { return GCRootKind.LocalVar; }
        }

        public override ClrType Type
        {
            get { return m_type; }
        }

        public override bool IsPinned
        {
            get
            {
                return m_pinned;
            }
        }

        public override bool IsInterior
        {
            get
            {
                return m_interior;
            }
        }

        public override ClrAppDomain AppDomain
        {
            get
            {
                return m_appDomain;
            }
        }

        public override string Name
        {
            get
            {
                return m_name;
            }
        }

        public override ClrThread Thread
        {
            get
            {
                return m_thread;
            }
        }

        public RhStackRoot(ClrThread thread, ulong addr, ulong obj, string name, ClrType type, ClrAppDomain domain, bool pinned, bool interior)
        {
            Address = addr;
            Object = obj;
            m_name = name;
            m_type = type;
            m_appDomain = domain;
            m_pinned = pinned;
            m_interior = interior;
            m_thread = thread;
        }
    }

    class RhHandleRoot : ClrRoot
    {
        private string m_name;
        private ClrType m_type;
        private ClrAppDomain m_appDomain;
        GCRootKind m_kind;

        public override GCRootKind Kind
        {
            get { return m_kind; }
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


        public RhHandleRoot(Address addr, Address obj, Address dependentTarget, ClrType type, int hndType, ClrAppDomain domain, string name)
        {

            Init(addr, obj, dependentTarget, type, hndType, domain, name);
        }

        public RhHandleRoot(Address addr, Address obj, ClrType type, int hndType, ClrAppDomain domain, string name)
        {

            Init(addr, obj, 0, type, hndType, domain, name);
        }

        private void Init(Address addr, Address obj, Address dependentTarget, ClrType type, int hndType, ClrAppDomain domain, string name)
        {
            HandleType htype = (HandleType)hndType;
            switch (htype)
            {
                case HandleType.AsyncPinned:
                    m_kind = GCRootKind.AsyncPinning;
                    break;

                case HandleType.Pinned:
                    m_kind = GCRootKind.Pinning;
                    break;

                case HandleType.WeakShort:
                case HandleType.WeakLong:
                    m_kind = GCRootKind.Weak;
                    break;

                default:
                    m_kind = GCRootKind.Strong;
                    break;
            }

            Address = addr;
            m_name = name;
            m_type = type;
            m_appDomain = domain;

            if (htype == HandleType.Dependent && dependentTarget != 0)
                Object = dependentTarget;
            else
                Object = obj;
        }
    }


    class RhStaticVar : ClrRoot
    {
        private string m_name;
        private bool m_pinned;
        private bool m_interior;
        private ClrType m_type;
        private ClrAppDomain m_appDomain;

        public override GCRootKind Kind
        {
            get { return GCRootKind.StaticVar; }
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

        public override bool IsPinned
        {
            get
            {
                return m_pinned;
            }
        }

        public override bool IsInterior
        {
            get
            {
                return m_interior;
            }
        }

        public override ClrAppDomain AppDomain
        {
            get
            {
                return m_appDomain;
            }
        }

        public RhStaticVar(RhRuntime runtime, Address addr, Address obj, ClrType type, string name, bool pinned, bool interior)
        {
            Address = addr;
            Object = obj;
            m_type = type;
            m_name = name;
            m_pinned = pinned;
            m_interior = interior;
            m_type = runtime.GetHeap().GetObjectType(obj);
            m_appDomain = runtime.GetRhAppDomain();
        }
    }
}
#endif