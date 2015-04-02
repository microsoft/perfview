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

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    class DesktopInterfaceData : ComInterfaceData
    {
        public override ClrType Type
        {
            get { return m_type; }
        }

        public override Address InterfacePointer
        {
            get { return m_interface; }
        }

        public DesktopInterfaceData(ClrType type, Address ptr)
        {
            m_type = type;
            m_interface = ptr;
        }

        private Address m_interface;
        private ClrType m_type;
    }


    class DesktopCCWData : CcwData
    {
        public override Address IUnknown { get { return m_ccw.IUnknown; } }
        public override Address Object { get { return m_ccw.Object; } }
        public override Address Handle { get { return m_ccw.Handle; } }
        public override int RefCount { get { return m_ccw.RefCount + m_ccw.JupiterRefCount; } }

        public override IList<ComInterfaceData> Interfaces
        {
            get
            {
                if (m_interfaces != null)
                    return m_interfaces;

                m_heap.LoadAllTypes();

                m_interfaces = new List<ComInterfaceData>();

                COMInterfacePointerData[] interfaces = m_heap.m_runtime.GetCCWInterfaces(m_addr, m_ccw.InterfaceCount);
                for (int i = 0; i < interfaces.Length; ++i)
                {
                    ClrType type = null;
                    if (interfaces[i].MethodTable != 0)
                        type = m_heap.GetGCHeapType(interfaces[i].MethodTable, 0);

                    m_interfaces.Add(new DesktopInterfaceData(type, interfaces[i].InterfacePtr));
                }

                return m_interfaces;
            }
        }

        internal DesktopCCWData(DesktopGCHeap heap, Address ccw, ICCWData data)
        {
            m_addr = ccw;
            m_ccw = data;
            m_heap = heap;
        }

        private Address m_addr;
        private ICCWData m_ccw;
        private DesktopGCHeap m_heap;
        private List<ComInterfaceData> m_interfaces;
    }

    class DesktopRCWData : RcwData
    {
        //public ulong IdentityPointer { get; }
        public override Address IUnknown { get { return m_rcw.UnknownPointer; } }
        public override Address VTablePointer { get { return m_rcw.VTablePtr; } }
        public override int RefCount { get { return m_rcw.RefCount; } }
        public override Address Object { get { return m_rcw.ManagedObject; } }
        public override bool Disconnected { get { return m_rcw.IsDisconnected; } }
        public override Address WinRTObject
        {
            get { return m_rcw.JupiterObject; }
        }
        public override uint CreatorThread
        {
            get
            {
                if (m_osThreadID == uint.MaxValue)
                {
                    IThreadData data = m_heap.m_runtime.GetThread(m_rcw.CreatorThread);
                    if (data == null || data.OSThreadID == uint.MaxValue)
                        m_osThreadID = 0;
                    else
                        m_osThreadID = data.OSThreadID;
                }

                return m_osThreadID;
            }
        }

        public override IList<ComInterfaceData> Interfaces
        {
            get
            {
                if (m_interfaces != null)
                    return m_interfaces;

                m_heap.LoadAllTypes();

                m_interfaces = new List<ComInterfaceData>();

                COMInterfacePointerData[] interfaces = m_heap.m_runtime.GetRCWInterfaces(m_addr, m_rcw.InterfaceCount);
                for (int i = 0; i < interfaces.Length; ++i)
                {
                    ClrType type = null;
                    if (interfaces[i].MethodTable != 0)
                        type = m_heap.GetGCHeapType(interfaces[i].MethodTable, 0);

                    m_interfaces.Add(new DesktopInterfaceData(type, interfaces[i].InterfacePtr));
                }

                return m_interfaces;
            }
        }

        internal DesktopRCWData(DesktopGCHeap heap, Address rcw, IRCWData data)
        {
            m_addr = rcw;
            m_rcw = data;
            m_heap = heap;
            m_osThreadID = uint.MaxValue;
        }

        private IRCWData m_rcw;
        DesktopGCHeap m_heap;
        private uint m_osThreadID;
        private List<ComInterfaceData> m_interfaces;
        private Address m_addr;
    }
}
