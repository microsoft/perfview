using System;
using System.Reflection;
using Address = System.UInt64;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    class DesktopMethod : ClrMethod
    {
        public override string ToString()
        {
            return string.Format("<ClrMethod signature='{0}' />", m_sig);
        }

        internal static DesktopMethod Create(DesktopRuntimeBase runtime, IMetadata metadata, IMethodDescData mdData)
        {
            if (mdData == null)
                return null;

            MethodAttributes attrs = (MethodAttributes)0;
            if (metadata != null)
            {
                int pClass, methodLength;
                uint blobLen, codeRva, implFlags;
                IntPtr blob;
                if (metadata.GetMethodProps(mdData.MDToken, out pClass, null, 0, out methodLength, out attrs, out blob, out blobLen, out codeRva, out implFlags) < 0)
                    attrs = (MethodAttributes)0;
            }

            return new DesktopMethod(runtime, mdData.MethodDesc, mdData, attrs);
        }

        internal static ClrMethod Create(DesktopRuntimeBase runtime, IMethodDescData mdData)
        {
            if (mdData == null)
                return null;

            DesktopModule module = runtime.GetModule(mdData.Module);
            return Create(runtime, module != null ? module.GetMetadataImport() : null, mdData);
        }

        public DesktopMethod(DesktopRuntimeBase runtime, ulong md, IMethodDescData mdData, MethodAttributes attrs)
        {
            m_runtime = runtime;
            m_sig = runtime.GetNameForMD(md);
            m_ip = mdData.NativeCodeAddr;
            m_jit = mdData.JITType;
            m_attrs = attrs;
            m_token = mdData.MDToken;
            var heap = (DesktopGCHeap)runtime.GetHeap();
            m_type = heap.GetGCHeapType(mdData.MethodTable, 0);
        }

        public override string Name
        {
            get
            {
                if (m_sig == null)
                    return null;

                int last = m_sig.LastIndexOf('(');
                if (last > 0)
                {
                    int first = m_sig.LastIndexOf('.', last - 1);

                    if (first != -1 && m_sig[first - 1] == '.')
                        first--;

                    return m_sig.Substring(first + 1, last - first - 1);
                }

                return "{error}";
            }
        }

        public override Address NativeCode
        {
            get { return m_ip; }
        }

        public override MethodCompilationType CompilationType
        {
            get { return m_jit; }
        }

        public override string GetFullSignature()
        {
            return m_sig;
        }

        public override SourceLocation GetSourceLocationForOffset(Address nativeOffset)
        {
            ClrType type = Type;
            if (type == null)
                return null;

            DesktopModule module = (DesktopModule)type.Module;
            if (module == null)
                return null;

            if (!module.IsPdbLoaded)
            {
                string val = module.TryDownloadPdb(null);
                if (val == null)
                    return null;

                module.LoadPdb(val);
                if (!module.IsPdbLoaded)
                    return null;
            }

            ILToNativeMap[] map = ILOffsetMap;
            if (map == null)
                return null;

            int ilOffset = 0;
            if (map.Length > 1)
                ilOffset = map[1].ILOffset;

            for (int i = 0; i < map.Length; ++i)
            {
                if (map[i].StartAddress <= m_ip && m_ip <= map[i].EndAddress)
                {
                    ilOffset = map[i].ILOffset;
                    break;
                }
            }

            return module.GetSourceInformation(MetadataToken, ilOffset);
        }

        public override bool IsStatic
        {
            get { return (m_attrs & MethodAttributes.Static) == MethodAttributes.Static; }
        }

        public override bool IsFinal
        {
            get { return (m_attrs & MethodAttributes.Final) == MethodAttributes.Final; }
        }

        public override bool IsPInvoke
        {
            get { return (m_attrs & MethodAttributes.PinvokeImpl) == MethodAttributes.PinvokeImpl; }
        }

        public override bool IsVirtual
        {
            get { return (m_attrs & MethodAttributes.Virtual) == MethodAttributes.Virtual; }
        }

        public override bool IsAbstract
        {
            get { return (m_attrs & MethodAttributes.Abstract) == MethodAttributes.Abstract; }
        }


        public override bool IsPublic
        {
            get { return (m_attrs & MethodAttributes.MemberAccessMask) == MethodAttributes.Public; }
        }

        public override bool IsPrivate
        {
            get { return (m_attrs & MethodAttributes.MemberAccessMask) == MethodAttributes.Private; }
        }

        public override bool IsInternal
        {
            get
            {
                MethodAttributes access = (m_attrs & MethodAttributes.MemberAccessMask);
                return access == MethodAttributes.Assembly || access == MethodAttributes.FamANDAssem;
            }
        }

        public override bool IsProtected
        {
            get
            {
                MethodAttributes access = (m_attrs & MethodAttributes.MemberAccessMask);
                return access == MethodAttributes.Family || access == MethodAttributes.FamANDAssem || access == MethodAttributes.FamORAssem;
            }
        }

        public override bool IsSpecialName
        {
            get
            { 
                return (m_attrs & MethodAttributes.SpecialName) == MethodAttributes.SpecialName;
            }
        }

        public override bool IsRTSpecialName
        {
            get
            {
                return (m_attrs & MethodAttributes.RTSpecialName) == MethodAttributes.RTSpecialName;
            }
        }


        public override ILToNativeMap[] ILOffsetMap
        {
            get
            {
                if (m_ilMap == null)
                    m_ilMap = m_runtime.GetILMap(m_ip);

                return m_ilMap;
            }
        }

        public override uint MetadataToken
        {
            get { return m_token; }
        }

        public override ClrType Type
        {
            get { return m_type; }
        }

        uint m_token;
        ILToNativeMap[] m_ilMap;
        string m_sig;
        ulong m_ip;
        MethodCompilationType m_jit;
        MethodAttributes m_attrs;
        private DesktopRuntimeBase m_runtime;
        private ClrType m_type;
    }
}
