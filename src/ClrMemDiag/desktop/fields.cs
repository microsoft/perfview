using System;
using System.Diagnostics;
using Address = System.UInt64;
using System.Reflection;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    class DesktopStaticField : ClrStaticField
    {
        public DesktopStaticField(DesktopGCHeap heap, IFieldData field, BaseDesktopHeapType containingType, string name, FieldAttributes attributes, object defaultValue, IntPtr sig, int sigLen)
        {
            m_field = field;
            m_name = name;
            m_attributes = attributes;
            m_type = (BaseDesktopHeapType)heap.GetGCHeapType(field.TypeMethodTable, 0);
            m_defaultValue = defaultValue;
            m_heap = heap;

            if (m_type != null && ElementType != ClrElementType.Class)
                m_type.SetElementType(ElementType);

            m_containingType = containingType;


            if (m_type == null)
            {
                if (sig != IntPtr.Zero && sigLen > 0)
                {
                    SigParser sigParser = new SigParser(sig, sigLen);

                    bool res;
                    int sigType, etype = 0;

                    if (res = sigParser.GetCallingConvInfo(out sigType))
                        Debug.Assert(sigType == SigParser.IMAGE_CEE_CS_CALLCONV_FIELD);

                    res = res && sigParser.SkipCustomModifiers();
                    res = res && sigParser.GetElemType(out etype);

                    if (res)
                    {
                        ClrElementType type = (ClrElementType)etype;

                        if (type == ClrElementType.Array)
                        {
                            res = sigParser.PeekElemType(out etype);
                            res = res && sigParser.SkipExactlyOne();

                            int ranks = 0;
                            res = res && sigParser.GetData(out ranks);

                            if (res)
                                m_type = heap.GetArrayType((ClrElementType)etype, ranks, null);
                        }
                        else if (type == ClrElementType.SZArray)
                        {
                            res = sigParser.PeekElemType(out etype);
                            type = (ClrElementType)etype;

                            if (DesktopRuntimeBase.IsObjectReference(type))
                                m_type = (BaseDesktopHeapType)heap.GetBasicType(ClrElementType.SZArray);
                            else
                                m_type = (BaseDesktopHeapType)heap.GetArrayType(type, -1, null);
                        }
                    }
                }

                if (m_type == null)
                    m_type = (BaseDesktopHeapType)TryBuildType(m_heap);

                if (m_type == null)
                    m_type = (BaseDesktopHeapType)heap.GetBasicType(ElementType);
            }
        }

        override public bool HasDefaultValue { get { return m_defaultValue != null; } }
        override public object GetDefaultValue() { return m_defaultValue; }

        override public bool IsPublic
        {
            get
            {
                return (m_attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Public;
            }
        }

        override public bool IsPrivate
        {
            get
            {
                return (m_attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Private;
            }
        }

        override public bool IsInternal
        {
            get
            {
                return (m_attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Assembly;
            }
        }

        override public bool IsProtected
        {
            get
            {
                return (m_attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Family;
            }
        }

        public override ClrElementType ElementType
        {
            get { return (ClrElementType)m_field.CorElementType; }
        }

        public override string Name { get { return m_name; } }

        public override ClrType Type
        {
            get
            {
                if (m_type == null)
                    m_type = (BaseDesktopHeapType)TryBuildType(m_heap);
                return m_type;
            }
        }

        private ClrType TryBuildType(ClrHeap heap)
        {
            var runtime = heap.GetRuntime();
            var domains = runtime.AppDomains;
            ClrType[] types = new ClrType[domains.Count];

            ClrElementType elType = ElementType;
            if (ClrRuntime.IsPrimitive(elType) || elType == ClrElementType.String)
                return ((DesktopGCHeap)heap).GetBasicType(elType);

            int count = 0;
            foreach (var domain in domains)
            {
                object value = GetFieldValue(domain);
                if (value != null && value is ulong && ((ulong)value != 0))
                {
                    types[count++] = heap.GetObjectType((ulong)value);
                }
            }

            int depth = int.MaxValue;
            ClrType result = null;
            for (int i = 0; i < count; ++i)
            {
                ClrType curr = types[i];
                if (curr == result || curr == null)
                    continue;

                int nextDepth = GetDepth(curr);
                if (nextDepth < depth)
                {
                    result = curr;
                    depth = nextDepth;
                }
            }

            return result;
        }

        private int GetDepth(ClrType curr)
        {
            int depth = 0;
            while (curr != null)
            {
                curr = curr.BaseType;
                depth++;
            }

            return depth;
        }

        // these are optional.  
        /// <summary>
        /// If the field has a well defined offset from the base of the object, return it (otherwise -1). 
        /// </summary>
        public override int Offset { get { return (int)m_field.Offset; } }

        /// <summary>
        /// Given an object reference, fetch the address of the field. 
        /// </summary>

        public override bool HasSimpleValue
        {
            get { return m_containingType != null; }
        }
        public override int Size
        {
            get
            {
                if (m_type == null)
                    m_type = (BaseDesktopHeapType)TryBuildType(m_heap);
                return DesktopInstanceField.GetSize(m_type, ElementType);
            }
        }

        public override object GetFieldValue(ClrAppDomain appDomain)
        {
            if (!HasSimpleValue)
                return null;

            Address addr = GetFieldAddress(appDomain);

            if (ElementType == ClrElementType.String)
            {
                object val = m_containingType.m_heap.GetValueAtAddress(ClrElementType.Object, addr);

                Debug.Assert(val == null || val is ulong);
                if (val == null || !(val is ulong))
                    return null;

                addr = (ulong)val;
            }

            // Structs are stored as objects.
            var elementType = ElementType;
            if (elementType == ClrElementType.Struct)
                elementType = ClrElementType.Object;

            if (elementType == ClrElementType.Object && addr == 0)
                return (ulong)0;

            return m_containingType.m_heap.GetValueAtAddress(elementType, addr);
        }

        public override Address GetFieldAddress(ClrAppDomain appDomain)
        {
            if (m_containingType == null)
                return 0;

            bool shared = m_containingType.Shared;

            IDomainLocalModuleData data = null;
            if (shared)
            {
                Address id = m_containingType.m_module.ModuleId;
                data = m_containingType.m_heap.m_runtime.GetDomainLocalModule(appDomain.Address, id);
                if (!IsInitialized(data))
                    return 0;
            }
            else
            {
                Address modAddr = m_containingType.GetModuleAddress(appDomain);
                if (modAddr != 0)
                    data = m_containingType.m_heap.m_runtime.GetDomainLocalModule(modAddr);
            }

            if (data == null)
                return 0;

            Address addr;
            if (DesktopRuntimeBase.IsPrimitive(ElementType))
                addr = data.NonGCStaticDataStart + m_field.Offset;
            else
                addr = data.GCStaticDataStart + m_field.Offset;

            return addr;
        }

        public override bool IsInitialized(ClrAppDomain appDomain)
        {
            if (m_containingType == null)
                return false;

            if (!m_containingType.Shared)
                return true;
            
            Address id = m_containingType.m_module.ModuleId;
            IDomainLocalModuleData data = m_containingType.m_heap.m_runtime.GetDomainLocalModule(appDomain.Address, id);
            if (data == null)
                return false;

            return IsInitialized(data);
        }

        private bool IsInitialized(IDomainLocalModuleData data)
        {
            if (data == null || m_containingType == null)
                return false;

            byte flags = 0;
            ulong flagsAddr = data.ClassData + (m_containingType.MetadataToken & ~0x02000000u) - 1;
            if (!m_heap.m_runtime.ReadByte(flagsAddr, out flags))
                return false;

            return (flags & 1) != 0;
        }

        private IFieldData m_field;
        private string m_name;
        private BaseDesktopHeapType m_type, m_containingType;
        private FieldAttributes m_attributes;
        private object m_defaultValue;
        private DesktopGCHeap m_heap;
    }

    class DesktopThreadStaticField : ClrThreadStaticField
    {
        public DesktopThreadStaticField(DesktopGCHeap heap, IFieldData field, string name)
        {
            m_field = field;
            m_name = name;
            m_type = (BaseDesktopHeapType)heap.GetGCHeapType(field.TypeMethodTable, 0);
        }

        public override object GetFieldValue(ClrAppDomain appDomain, ClrThread thread)
        {
            if (!HasSimpleValue)
                return null;

            Address addr = GetFieldAddress(appDomain, thread);
            if (addr == 0)
                return null;

            if (ElementType == ClrElementType.String)
            {
                object val = m_type.m_heap.GetValueAtAddress(ClrElementType.Object, addr);

                Debug.Assert(val == null || val is ulong);
                if (val == null || !(val is ulong))
                    return null;

                addr = (ulong)val;
            }

            return m_type.m_heap.GetValueAtAddress(ElementType, addr);
        }

        public override Address GetFieldAddress(ClrAppDomain appDomain, ClrThread thread)
        {
            if (m_type == null)
                return 0;

            DesktopRuntimeBase runtime = m_type.m_heap.m_runtime;
            IModuleData moduleData = runtime.GetModuleData(m_field.Module);

            return runtime.GetThreadStaticPointer(thread.Address, (ClrElementType)m_field.CorElementType, (uint)Offset, (uint)moduleData.ModuleId, m_type.Shared);
        }


        public override ClrElementType ElementType
        {
            get { return (ClrElementType)m_field.CorElementType; }
        }

        public override string Name { get { return m_name; } }

        public override ClrType Type { get { return m_type; } }

        // these are optional.  
        /// <summary>
        /// If the field has a well defined offset from the base of the object, return it (otherwise -1). 
        /// </summary>
        public override int Offset { get { return (int)m_field.Offset; } }

        /// <summary>
        /// Given an object reference, fetch the address of the field. 
        /// </summary>

        public override bool HasSimpleValue
        {
            get { return m_type != null && !DesktopRuntimeBase.IsValueClass(ElementType); }
        }
        public override int Size
        {
            get
            {
                return DesktopInstanceField.GetSize(m_type, ElementType);
            }
        }

        public override bool IsPublic
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsPrivate
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsInternal
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsProtected
        {
            get { throw new NotImplementedException(); }
        }

        private IFieldData m_field;
        private string m_name;
        private BaseDesktopHeapType m_type;
    }

    class DesktopInstanceField : ClrInstanceField
    {
        override public bool IsPublic
        {
            get
            {
                return (m_attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Public;
            }
        }

        override public bool IsPrivate
        {
            get
            {
                return (m_attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Private;
            }
        }

        override public bool IsInternal
        {
            get
            {
                return (m_attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Assembly;
            }
        }

        override public bool IsProtected
        {
            get
            {
                return (m_attributes & FieldAttributes.FieldAccessMask) == FieldAttributes.Family;
            }
        }


        public DesktopInstanceField(DesktopGCHeap heap, IFieldData data, string name, FieldAttributes attributes, IntPtr sig, int sigLen)
        {
            m_name = name;
            m_field = data;
            m_attributes = attributes;

            ulong mt = data.TypeMethodTable;
            if (mt != 0)
                m_type = (BaseDesktopHeapType)heap.GetGCHeapType(mt, 0);

            if (m_type == null)
            {
                if (sig != IntPtr.Zero && sigLen > 0)
                {
                    SigParser sigParser = new SigParser(sig, sigLen);

                    bool res;
                    int sigType, etype = 0;

                    if (res = sigParser.GetCallingConvInfo(out sigType))
                        Debug.Assert(sigType == SigParser.IMAGE_CEE_CS_CALLCONV_FIELD);

                    res = res && sigParser.SkipCustomModifiers();
                    res = res && sigParser.GetElemType(out etype);

                    if (res)
                    {
                        ClrElementType type = (ClrElementType)etype;

                        if (type == ClrElementType.Array)
                        {
                            res = sigParser.PeekElemType(out etype);
                            res = res && sigParser.SkipExactlyOne();

                            int ranks = 0;
                            res = res && sigParser.GetData(out ranks);

                            if (res)
                                m_type = heap.GetArrayType((ClrElementType)etype, ranks, null);
                        }
                        else if (type == ClrElementType.SZArray)
                        {
                            res = sigParser.PeekElemType(out etype);
                            type = (ClrElementType)etype;

                            if (DesktopRuntimeBase.IsObjectReference(type))
                                m_type = (BaseDesktopHeapType)heap.GetBasicType(ClrElementType.SZArray);
                            else
                                m_type = (BaseDesktopHeapType)heap.GetArrayType(type, -1, null);
                        }
                    }
                }
                
                if (m_type == null)
                    m_type = (BaseDesktopHeapType)heap.GetBasicType(ElementType);
            }
            else if (ElementType != ClrElementType.Class)
            {
                m_type.SetElementType(ElementType);
            }
        }


        public override ClrElementType ElementType
        {
            get
            {
                if (m_elementType != ClrElementType.Unknown)
                    return m_elementType;

                if (m_type == null)
                    m_elementType = (ClrElementType)m_field.CorElementType;

                else if (m_type.IsEnum)
                    m_elementType = m_type.GetEnumElementType();
                
                else
                    m_elementType = m_type.ElementType;

                return m_elementType;
            }
        }

        public override string Name { get { return m_name; } }

        public override ClrType Type { get { return m_type; } }

        // these are optional.  
        /// <summary>
        /// If the field has a well defined offset from the base of the object, return it (otherwise -1). 
        /// </summary>
        public override int Offset { get { return (int)m_field.Offset; } }

        /// <summary>
        /// Given an object reference, fetch the address of the field. 
        /// </summary>

        public override bool HasSimpleValue
        {
            get { return m_type != null && !DesktopRuntimeBase.IsValueClass(ElementType); }
        }
        public override int Size
        {
            get
            {
                return GetSize(m_type, ElementType);
            }
        }


        #region Fields
        string m_name;
        BaseDesktopHeapType m_type;
        private IFieldData m_field;
        private FieldAttributes m_attributes;
        private ClrElementType m_elementType = ClrElementType.Unknown;
        #endregion

        public override object GetFieldValue(Address objRef, bool interior = false)
        {
            if (!HasSimpleValue)
                return null;

            Address addr = GetFieldAddress(objRef, interior);

            if (ElementType == ClrElementType.String)
            {
                object val = m_type.m_heap.GetValueAtAddress(ClrElementType.Object, addr);

                Debug.Assert(val == null || val is ulong);
                if (val == null || !(val is ulong))
                    return null;

                addr = (ulong)val;
            }

            return m_type.m_heap.GetValueAtAddress(ElementType, addr);
        }

        public override Address GetFieldAddress(Address objRef, bool interior = false)
        {
            if (interior)
                return objRef + (Address)Offset;

            //todo: this is a hack:
            if (m_type == null)
                return objRef + (Address)(Offset + IntPtr.Size);

            return objRef + (Address)(Offset + m_type.m_heap.PointerSize);
        }


        internal static int GetSize(BaseDesktopHeapType type, ClrElementType cet)
        {
            // todo:  What if we have a struct which is not fully constructed (null MT,
            //        null type) and need to get the size of the field?
            switch (cet)
            {
                case ClrElementType.Struct:
                    if (type == null)
                        return 1;
                    return type.BaseSize;

                case ClrElementType.Int8:
                case ClrElementType.UInt8:
                case ClrElementType.Boolean:
                    return 1;

                case ClrElementType.Float:
                case ClrElementType.Int32:
                case ClrElementType.UInt32:
                    return 4;

                case ClrElementType.Double: // double
                case ClrElementType.Int64:
                case ClrElementType.UInt64:
                    return 8;

                case ClrElementType.String:
                case ClrElementType.Class:
                case ClrElementType.Array:
                case ClrElementType.SZArray:
                case ClrElementType.Object:
                case ClrElementType.NativeInt:  // native int
                case ClrElementType.NativeUInt:  // native unsigned int
                case ClrElementType.Pointer:
                case ClrElementType.FunctionPointer:
                    if (type == null)
                        return IntPtr.Size;  // todo: fixme
                    return (int)type.m_heap.PointerSize;


                case ClrElementType.UInt16:
                case ClrElementType.Int16:
                case ClrElementType.Char:  // u2
                    return 2;
            }

            throw new Exception("Unexpected element type.");
        }
    }

}
