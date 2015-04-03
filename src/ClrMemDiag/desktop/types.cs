using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Address = System.UInt64;
using System.Text;
using System.Collections;
using System.Reflection;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    class DesktopHeapInterface : ClrInterface
    {
        private string m_name;
        private ClrInterface m_base;
        public DesktopHeapInterface(string name, ClrInterface baseInterface)
        {
            m_name = name;
            m_base = baseInterface;
        }

        public override string Name
        {
            get { return m_name; }
        }

        public override ClrInterface BaseInterface
        {
            get { return m_base; }
        }
    }

    abstract class BaseDesktopHeapType : ClrType
    {
        internal DesktopGCHeap m_heap;
        internal DesktopBaseModule m_module;
        internal ClrElementType m_elementType;
        protected uint m_token;
        private IList<ClrInterface> m_interfaces;
        public bool Shared { get; protected set; }
        internal abstract void SetModule(ClrModule module);
        internal abstract void SetElementType(ClrElementType ElementType);
        internal abstract ulong GetModuleAddress(ClrAppDomain domain);


        public BaseDesktopHeapType(DesktopGCHeap heap, DesktopBaseModule module, uint token)
        {
            m_heap = heap;
            m_module = module;
            m_token = token;
        }

        public override uint MetadataToken
        {
            get { return m_token; }
        }

        public override IList<ClrInterface> Interfaces
        {
            get
            {
                if (m_interfaces == null)
                    InitInterfaces();

                Debug.Assert(m_interfaces != null);
                return m_interfaces;
            }
        }

        public List<ClrInterface> InitInterfaces()
        {
            if (m_module == null)
            {
                m_interfaces = m_heap.m_emptyInterfaceList;
                return null;
            }

            BaseDesktopHeapType baseType = BaseType as BaseDesktopHeapType;
            List<ClrInterface> interfaces = baseType != null ? new List<ClrInterface>(baseType.Interfaces) : null;
            IMetadata import = m_module.GetMetadataImport();
            if (import == null)
            {
                m_interfaces = m_heap.m_emptyInterfaceList;
                return null;
            }

            IntPtr hnd = IntPtr.Zero;
            int[] mdTokens = new int[32];
            int count;

            do
            {
                int res = import.EnumInterfaceImpls(ref hnd, (int)m_token, mdTokens, mdTokens.Length, out count);
                if (res < 0)
                    break;

                for (int i = 0; i < count; ++i)
                {

                    int mdClass, mdIFace;
                    res = import.GetInterfaceImplProps(mdTokens[i], out mdClass, out mdIFace);

                    if (interfaces == null)
                        interfaces = new List<ClrInterface>(count == mdTokens.Length ? 64 : count);

                    var result = GetInterface(import, mdIFace);
                    if (result != null && !interfaces.Contains(result))
                    {
                            interfaces.Add(result);
                    }
                }

            } while (count == mdTokens.Length);

            import.CloseEnum(hnd);

            if (interfaces == null)
                m_interfaces = m_heap.m_emptyInterfaceList;
            else
                m_interfaces = interfaces.ToArray();

            return interfaces;
        }



        ClrInterface GetInterface(IMetadata import, int mdIFace)
        {
            StringBuilder builder = new StringBuilder(1024);
            int extends, cnt;
            System.Reflection.TypeAttributes attr;
            int res = import.GetTypeDefProps(mdIFace, builder, builder.Capacity, out cnt, out attr, out extends);

            ClrInterface result = null;
            if (res == 0)
            {
                string name = builder.ToString();

                if (!m_heap.m_interfaces.TryGetValue(name, out result))
                {
                    ClrInterface type = null;
                    if (extends != 0 && extends != 0x01000000)
                        type = GetInterface(import, extends);

                    result = new DesktopHeapInterface(name, type);
                    m_heap.m_interfaces[name] = result;
                }
            }

            return result;
        }
    }

    class DesktopArrayType : BaseDesktopHeapType
    {
        private ClrElementType m_arrayElement;
        ClrType m_arrayElementType;
        private int m_ranks;
        string m_name;
        
        public DesktopArrayType(DesktopGCHeap heap, DesktopBaseModule module, ClrElementType eltype, int ranks, uint token, string nameHint)
            : base(heap, module, token)
        {
            m_elementType = ClrElementType.Array;
            m_arrayElement = eltype;
            m_ranks = ranks;
            if (nameHint != null)
                BuildName(nameHint);
        }

        public override ClrModule Module { get { return m_module; } }

        public override ClrElementType ElementType { get { return m_elementType; } }

        internal override void SetModule(ClrModule module)
        {
        }

        internal override void SetElementType(ClrElementType ElementType)
        {
        }

        internal override Address GetModuleAddress(ClrAppDomain domain)
        {
            return 0;
        }

        public override int Index
        {
            get { return -1; }
        }

        public override string Name
        {
            get
            {
                if (m_name == null)
                    BuildName(null);

                return m_name;
            }
        }

        private void BuildName(string hint)
        {
            StringBuilder builder = new StringBuilder();
            ClrType inner = ArrayComponentType;

            builder.Append(inner != null ? inner.Name : GetElementTypeName(hint));
            builder.Append("[");

            for (int i = 0; i < m_ranks - 1; ++i)
                builder.Append(",");

            builder.Append("]");
            m_name = builder.ToString();
        }

        private string GetElementTypeName(string hint)
        {
            switch (m_arrayElement)
            {
                case ClrElementType.Boolean:
                    return "System.Boolean";

                case ClrElementType.Char:
                    return "System.Char";

                case ClrElementType.Int8:
                    return "System.SByte";
                    
                case ClrElementType.UInt8:
                    return "System.Byte";

                case ClrElementType.Int16:
                    return "System.Int16";

                case ClrElementType.UInt16:
                    return "ClrElementType.UInt16";

                case ClrElementType.Int32:
                    return "System.Int32";

                case ClrElementType.UInt32:
                    return "System.UInt32";

                case ClrElementType.Int64:
                    return "System.Int64";

                case ClrElementType.UInt64:
                    return "System.UInt64";

                case ClrElementType.Float:
                    return "System.Single";

                case ClrElementType.Double:
                    return "System.Double";

                case ClrElementType.NativeInt:
                    return "System.IntPtr";

                case ClrElementType.NativeUInt:
                    return "System.UIntPtr";

                case ClrElementType.Struct:
                    return "Sytem.ValueType";
            }

            if (hint != null)
                return hint;

            return "ARRAY";
        }

        public override bool IsFinalizeSuppressed(Address obj)
        {
            return false;
        }

        public override ClrType ArrayComponentType
        {
            get
            {
                if (m_arrayElementType == null)
                    m_arrayElementType = m_heap.GetBasicType(m_arrayElement);

                return m_arrayElementType;
            }
            internal set
            {
                if (value != null)
                    m_arrayElementType = value;
            }
        }

        override public bool IsArray { get { return true; } }

        override public IList<ClrInstanceField> Fields { get { return new ClrInstanceField[0]; } }

        override public IList<ClrStaticField> StaticFields { get { return new ClrStaticField[0]; } }

        override public IList<ClrThreadStaticField> ThreadStaticFields { get { return new ClrThreadStaticField[0]; } }

        override public IList<ClrMethod> Methods { get { return new ClrMethod[0]; } }

        public override Address GetSize(Address objRef)
        {
            ClrType realType = m_heap.GetObjectType(objRef);
            return realType.GetSize(objRef);
        }

        public override void EnumerateRefsOfObject(Address objRef, Action<Address, int> action)
        {
            ClrType realType = m_heap.GetObjectType(objRef);
            realType.EnumerateRefsOfObject(objRef, action);
        }

        public override ClrHeap Heap
        {
            get { return m_heap; }
        }

        public override IList<ClrInterface> Interfaces
        { // todo
            get { return new ClrInterface[0]; }
        }

        public override bool IsFinalizable
        {
            get { return false; }
        }

        public override bool IsPublic
        {
            get { return true; }
        }

        public override bool IsPrivate
        {
            get { return false; }
        }

        public override bool IsInternal
        {
            get { return false; }
        }

        public override bool IsProtected
        {
            get { return false; }
        }

        public override bool IsAbstract
        {
            get { return false; }
        }

        public override bool IsSealed
        {
            get { return false; }
        }

        public override bool IsInterface
        {
            get { return false; }
        }

        public override bool GetFieldForOffset(int fieldOffset, bool inner, out ClrInstanceField childField, out int childFieldOffset)
        {
            childField = null;
            childFieldOffset = 0;
            return false;
        }

        public override ClrInstanceField GetFieldByName(string name)
        {
            return null;
        }

        public override ClrStaticField GetStaticFieldByName(string name)
        {
            return null;
        }

        public override ClrType BaseType
        {
            get { return m_heap.m_arrayType; }
        }

        public override int GetArrayLength(Address objRef)
        {
            //todo
            throw new NotImplementedException();
        }

        public override Address GetArrayElementAddress(Address objRef, int index)
        {
            throw new NotImplementedException();
        }

        public override object GetArrayElementValue(Address objRef, int index)
        {
            throw new NotImplementedException();
        }

        public override int ElementSize
        {
            get { return DesktopInstanceField.GetSize(null, m_arrayElement); }
        }

        public override int BaseSize
        {
            get { return IntPtr.Size * 8; }
        }

        public override void EnumerateRefsOfObjectCarefully(Address objRef, Action<Address, int> action)
        {
            ClrType realType = m_heap.GetObjectType(objRef);
            realType.EnumerateRefsOfObjectCarefully(objRef, action);
        }
    }


    class DesktopHeapType : BaseDesktopHeapType
    {
        public override ClrElementType ElementType
        {
            get
            {
                if (m_elementType == ClrElementType.Unknown)
                    m_elementType = m_heap.GetElementType(this, 0);

                return m_elementType;
            }
        }

        public override int Index
        {
            get { return m_index; }
        }

        public override string Name { get { return m_name; } }
        public override ClrModule Module
        {
            get
            {
                if (m_module == null)
                    return new ErrorModule();
                return m_module;
            }
        }
        public override ulong GetSize(Address objRef)
        {
            ulong size;
            uint pointerSize = (uint)m_heap.PointerSize;
            if (m_componentSize == 0)
            {
                size = m_baseSize;
            }
            else
            {
                uint count = 0;
                uint countOffset = pointerSize;
                ulong loc = objRef + countOffset;

                var cache = m_heap.MemoryReader;
                if (!cache.Contains(loc))
                {
                    var runtimeCache = m_heap.m_runtime.MemoryReader;
                    if (runtimeCache.Contains(loc))
                        cache = m_heap.m_runtime.MemoryReader;
                }

                if (!cache.ReadDword(loc, out count))
                    throw new Exception("Could not read from heap at " + objRef.ToString("x"));

                // Strings in v4+ contain a trailing null terminator not accounted for.
                if (m_heap.m_stringType == this && m_heap.m_runtime.CLRVersion != DesktopVersion.v2)
                    count++;

                size = count * (ulong)m_componentSize + m_baseSize;
            }

            uint minSize = pointerSize * 3;
            if (size < minSize)
                size = minSize;
            return size;
        }

        public override void EnumerateRefsOfObjectCarefully(Address objRef, Action<Address, int> action)
        {
            if (!m_containsPointers)
                return;

            if (m_gcDesc == null)
                if (!FillGCDesc() || m_gcDesc == null)
                    return;

            ulong size = GetSize(objRef);

            ClrSegment seg = m_heap.GetSegmentByAddress(objRef);
            if (seg == null || objRef + size > seg.End)
                return;

            var cache = m_heap.MemoryReader;
            if (!cache.Contains(objRef))
                cache = m_heap.m_runtime.MemoryReader;

            m_gcDesc.WalkObject(objRef, (ulong)size, cache, action);
        }

        public override void EnumerateRefsOfObject(Address objRef, Action<Address, int> action)
        {
            if (!m_containsPointers)
                return;

            if (m_gcDesc == null)
                if (!FillGCDesc() || m_gcDesc == null)
                    return;

            var size = GetSize(objRef);
            var cache = m_heap.MemoryReader;
            if (!cache.Contains(objRef))
                cache = m_heap.m_runtime.MemoryReader;

            m_gcDesc.WalkObject(objRef, (ulong)size, cache, action);
        }


        bool FillGCDesc()
        {
            DesktopRuntimeBase runtime = m_heap.m_runtime;

            int entries;
            if (!runtime.ReadDword(m_handle - (ulong)IntPtr.Size, out entries))
                return false;

            // Get entries in map
            if (entries < 0)
                entries = -entries;

            int read;
            int slots = 1 + entries * 2;
            byte[] buffer = new byte[slots * IntPtr.Size];
            if (!runtime.ReadMemory(m_handle - (ulong)(slots * IntPtr.Size), buffer, buffer.Length, out read) || read != buffer.Length)
                return false;

            // Construct the gc desc
            m_gcDesc = new GCDesc(buffer);
            return true;
        }

        public override ClrHeap Heap { get { return m_heap; } }
        public override string ToString()
        {
            return "<GCHeapType Name=\"" + Name + "\">";
        }

        public override bool HasSimpleValue { get { return ElementType != ClrElementType.Struct; } }
        public override object GetValue(Address address)
        {
            if (IsPrimitive)
                address += (ulong)m_heap.PointerSize;

            return m_heap.GetValueAtAddress(ElementType, address);
        }



        public override bool IsException
        {
            get
            {
                ClrType type = this;
                while (type != null)
                    if (type == m_heap.m_exceptionType)
                        return true;
                    else
                        type = type.BaseType;
   
                return false;
            }
        }


        public override bool IsCCW(Address obj)
        {
            if (m_checkedIfIsCCW)
                return !m_notCCW;

            // The dac cannot report this information prior to v4.5.
            if (m_heap.m_runtime.CLRVersion != DesktopVersion.v45)
                return false;

            IObjectData data = m_heap.GetObjectData(obj);
            m_notCCW = !(data != null && data.CCW != 0);
            m_checkedIfIsCCW = true;

            return !m_notCCW;
        }

        public override CcwData GetCCWData(Address obj)
        {
            if (m_notCCW)
                return null;

            // The dac cannot report this information prior to v4.5.
            if (m_heap.m_runtime.CLRVersion != DesktopVersion.v45)
                return null;

            DesktopCCWData result = null;
            IObjectData data = m_heap.GetObjectData(obj);

            if (data != null && data.CCW != 0)
            {
                ICCWData ccw = m_heap.m_runtime.GetCCWData(data.CCW);
                if (ccw != null)
                    result = new DesktopCCWData(m_heap, data.CCW, ccw);
            }
            else if (!m_checkedIfIsCCW)
            {
                m_notCCW = true;
            }

            m_checkedIfIsCCW = true;
            return result;
        }

        public override bool IsRCW(Address obj)
        {
            if (m_checkedIfIsRCW)
                return !m_notRCW;

            // The dac cannot report this information prior to v4.5.
            if (m_heap.m_runtime.CLRVersion != DesktopVersion.v45)
                return false;

            IObjectData data = m_heap.GetObjectData(obj);
            m_notRCW = !(data != null && data.RCW != 0);
            m_checkedIfIsRCW = true;

            return !m_notRCW;
        }

        public override RcwData GetRCWData(Address obj)
        {
            // Most types can't possibly be RCWs.  
            if (m_notRCW)
                return null;

            // The dac cannot report this information prior to v4.5.
            if (m_heap.m_runtime.CLRVersion != DesktopVersion.v45)
            {
                m_notRCW = true;
                return null;
            }

            DesktopRCWData result = null;
            IObjectData data = m_heap.GetObjectData(obj);

            if (data != null && data.RCW != 0)
            {
                IRCWData rcw = m_heap.m_runtime.GetRCWData(data.RCW);
                if (rcw != null)
                    result = new DesktopRCWData(m_heap, data.RCW, rcw);
            }
            else if (!m_checkedIfIsRCW)     // If the first time fails, we assume that all instances of this type can't be RCWs.
            {
                m_notRCW = true;            // TODO FIX NOW review.  We really want to simply ask the runtime... 
            }

            m_checkedIfIsRCW = true;
            return result;
        }

        class EnumData
        {
            internal ClrElementType ElementType;
            internal Dictionary<string, object> NameToValue = new Dictionary<string, object>();
            internal Dictionary<object, string> ValueToName = new Dictionary<object,string>();
        }

        public override ClrElementType GetEnumElementType()
        {
            if (m_enumData == null)
                InitEnumData();

            return m_enumData.ElementType;
        }

        public override bool TryGetEnumValue(string name, out int value)
        {
            object val = null;
            if (TryGetEnumValue(name, out val))
            {
                value = (int)val;
                return true;
            }

            value = int.MinValue;
            return false;
        }


        public override bool TryGetEnumValue(string name, out object value)
        {
            if (m_enumData == null)
                InitEnumData();

            return m_enumData.NameToValue.TryGetValue(name, out value);
        }

        override public string GetEnumName(object value)
        {
            if (m_enumData == null)
                InitEnumData();

            string result = null;
            m_enumData.ValueToName.TryGetValue(value, out result);
            return result;
        }

        override public string GetEnumName(int value)
        {
            return GetEnumName((object)value);
        }



        public override IEnumerable<string> GetEnumNames()
        {
            if (m_enumData == null)
                InitEnumData();

            return m_enumData.NameToValue.Keys;
        }

        private void InitEnumData()
        {
            if (!IsEnum)
                throw new InvalidOperationException("Type is not an Enum.");

            m_enumData = new EnumData();
            IMetadata import = null;
            if (m_module != null)
                import = m_module.GetMetadataImport();

            if (import == null)
            {
                m_enumData = new EnumData();
                return;
            }

            IntPtr hnd = IntPtr.Zero;
            int tokens;

            List<string> names = new List<string>();
            int[] fields = new int[64];
            do
            {
                int res = import.EnumFields(ref hnd, (int)m_token, fields, fields.Length, out tokens);
                for (int i = 0; i < tokens; ++i)
                {
                    FieldAttributes attr;
                    int mdTypeDef, pchField, pcbSigBlob, pdwCPlusTypeFlag, pcchValue;
                    IntPtr ppvSigBlob, ppValue = IntPtr.Zero;
                    StringBuilder builder = new StringBuilder(256);

                    res = import.GetFieldProps(fields[i], out mdTypeDef, builder, builder.Capacity, out pchField, out attr, out ppvSigBlob, out pcbSigBlob, out pdwCPlusTypeFlag, out ppValue, out pcchValue);

                    if ((int)attr == 0x606 && builder.ToString() == "value__")
                    {
                        SigParser parser = new SigParser(ppvSigBlob, pcbSigBlob);
                        int sigType, elemType;

                        if (parser.GetCallingConvInfo(out sigType) && parser.GetElemType(out elemType))
                            m_enumData.ElementType = (ClrElementType)elemType;
                    }

                    // public, static, literal, has default
                    int intAttr = (int)attr;
                    if ((int)attr == 0x8056)
                    {
                        string name = builder.ToString();
                        names.Add(name);

                        int ccinfo;
                        SigParser parser = new SigParser(ppvSigBlob, pcbSigBlob);
                        parser.GetCallingConvInfo(out ccinfo);
                        int elemType;
                        parser.GetElemType(out elemType);

                        Type type = ClrRuntime.GetTypeForElementType((ClrElementType)pdwCPlusTypeFlag);
                        if (type != null)
                        {
                            object o = System.Runtime.InteropServices.Marshal.PtrToStructure(ppValue, type);
                            m_enumData.NameToValue[name] = o;
                            m_enumData.ValueToName[o] = name;
                        }
                    }
                }
            } while (fields.Length == tokens);

            import.CloseEnum(hnd);
        }
        
        public override bool IsEnum
        {
            get
            {
                ClrType type = this;

                ClrType enumType = m_heap.m_enumType;
                while (type != null)
                {
                    if (enumType == null && type.Name == "System.Enum")
                    {
                        m_heap.m_enumType = type;
                        return true;
                    }
                    else if (type == enumType)
                    {
                        return true;
                    }
                    else
                    {
                        type = type.BaseType;
                    }
                }

                return false;
            }
        }


        public override bool IsFree
        {
            get
            {
                return this == m_heap.m_freeType;
            }
        }

        const uint FinalizationSuppressedFlag = 0x40000000;
        public override bool IsFinalizeSuppressed(Address obj)
        {
            uint value;
            bool result = m_heap.GetObjectHeader(obj, out value);

            return result && (value & FinalizationSuppressedFlag) == FinalizationSuppressedFlag;
        }

        public override bool IsFinalizable
        {
            get
            {
                if (m_finalizable == 0)
                {
                    foreach (var method in Methods)
                    {
                        if (method.IsVirtual && method.Name == "Finalize")
                        {
                            m_finalizable = 1;
                            break;
                        }
                    }

                    if (m_finalizable == 0)
                        m_finalizable = 2;
                }

                return m_finalizable == 1;
            }
        }

        public override bool IsArray { get { return m_componentSize != 0 && this != m_heap.m_stringType && this != m_heap.m_freeType; } }
        public override bool ContainsPointers { get { return m_containsPointers; } }
        public override bool IsString { get { return this == m_heap.m_stringType; } }
        
        public override bool GetFieldForOffset(int fieldOffset, bool inner, out ClrInstanceField childField, out int childFieldOffset)
        {
            int ps = (int)m_heap.PointerSize;
            int offset = fieldOffset;

            if (!IsArray)
            {
                if (!inner)
                    offset -= ps;

                foreach (ClrInstanceField field in Fields)
                {
                    if (field.Offset <= offset)
                    {
                        int size = field.Size;

                        if (offset < field.Offset + size)
                        {
                            childField = field;
                            childFieldOffset = offset - field.Offset;
                            return true;
                        }
                    }
                }
            }

            if (BaseType != null)
                return BaseType.GetFieldForOffset(fieldOffset, inner, out childField, out childFieldOffset);

            childField = null;
            childFieldOffset = 0;
            return false;
        }

        public override int ElementSize { get { return (int)m_componentSize; } }
        public override IList<ClrInstanceField> Fields
        {
            get
            {
                if (m_fields == null)
                    InitFields();

                return m_fields;
            }
        }


        public override IList<ClrStaticField> StaticFields
        {
            get
            {
                if (m_fields == null)
                    InitFields();

                if (m_statics == null)
                    return EmptyStatics;

                return m_statics;
            }
        }

        public override IList<ClrThreadStaticField> ThreadStaticFields
        {
            get
            {
                if (m_fields == null)
                    InitFields();

                if (m_threadStatics == null)
                    return EmptyThreadStatics;

                return m_threadStatics;
            }
        }

        private void InitFields()
        {
            if (m_fields != null)
                return;

            DesktopRuntimeBase runtime = m_heap.m_runtime;
            IFieldInfo fieldInfo = runtime.GetFieldInfo(m_handle);

            if (fieldInfo == null)
            {
                // Fill fields so we don't repeatedly try to init these fields on error.
                m_fields = new List<ClrInstanceField>();
                return;
            }

            m_fields = new List<ClrInstanceField>((int)fieldInfo.InstanceFields);

            // Add base type's fields.
            if (BaseType != null)
            {
                foreach (var field in BaseType.Fields)
                    m_fields.Add(field);
            }

            int count = (int)(fieldInfo.InstanceFields + fieldInfo.StaticFields) - m_fields.Count;
            ulong nextField = fieldInfo.FirstField;
            int i = 0;

            IMetadata import = null;
            if (nextField != 0 && m_module != null)
                import = m_module.GetMetadataImport();

            while (i < count && nextField != 0)
            {
                IFieldData field = runtime.GetFieldData(nextField);
                if (field == null)
                    break;

                // We don't handle context statics.
                if (field.bIsContextLocal)
                {
                    nextField = field.nextField;
                    continue;
                }

                // Get the name of the field.
                string name = null;
                FieldAttributes attr = FieldAttributes.PrivateScope;
                int pcchValue = 0, sigLen = 0;
                IntPtr ppValue = IntPtr.Zero;
                IntPtr fieldSig = IntPtr.Zero;

                if (import != null)
                {
                    int mdTypeDef, pchField, pdwCPlusTypeFlab;
                    StringBuilder builder = new StringBuilder(256);

                    int res = import.GetFieldProps((int)field.FieldToken, out mdTypeDef, builder, builder.Capacity, out pchField, out attr, out fieldSig, out sigLen, out pdwCPlusTypeFlab, out ppValue, out pcchValue);
                    if (res >= 0)
                        name = builder.ToString();
                    else
                        fieldSig = IntPtr.Zero;
                }

                // If we couldn't figure out the name, at least give the token.
                if (import == null || name == null)
                {
                    name = string.Format("<FIELD_TOKEN:{0:X}>", field.FieldToken);
                }

                // construct the appropriate type of field.
                if (field.IsThreadLocal)
                {
                    if (m_threadStatics == null)
                        m_threadStatics = new List<ClrThreadStaticField>((int)fieldInfo.ThreadStaticFields);

                    // TODO:  Renable when thread statics are fixed.
                    //m_threadStatics.Add(new RealTimeMemThreadStaticField(m_heap, field, name));
                }
                else if (field.bIsStatic)
                {
                    if (m_statics == null)
                        m_statics = new List<ClrStaticField>();

                    // TODO:  Enable default values.
                    /*
                    object defaultValue = null;


                    FieldAttributes sdl = FieldAttributes.Static | FieldAttributes.HasDefault | FieldAttributes.Literal;
                    if ((attr & sdl) == sdl)
                        Debugger.Break();
                    */
                    m_statics.Add(new DesktopStaticField(m_heap, field, this, name, attr, null, fieldSig, sigLen));
                }
                else // instance variable
                {
                    m_fields.Add(new DesktopInstanceField(m_heap, field, name, attr, fieldSig, sigLen));
                }

                i++;
                nextField = field.nextField;
            }

            m_fields.Sort((a, b) => a.Offset.CompareTo(b.Offset));
        }

        
        public override bool TryGetFieldValue(ulong obj, ICollection<string> fields, out object value)
        {
            // todo:  There's probably a better way of implementing GetFieldValue/TryGetFieldValue
            //        than simply wrapping this in a try/catch.
            try
            {
                value = GetFieldValue(obj, fields);
                return true;
            }
            catch
            {
                value = null;
                return false;
            }
        }

        public override object GetFieldValue(ulong obj, ICollection<string> fields)
        {
            ClrType type = this;
            ClrInstanceField field = null;

            object value = obj;
            bool inner = false;

            int i = 0;
            int lastElement = fields.Count - 1;
            foreach (string name in fields)
            {
                if (type == null)
                    throw new Exception(string.Format("Could not get type information for field {0}.", name));

                field = type.GetFieldByName(name);
                if (field == null)
                    throw new Exception(string.Format("Type {0} does not contain field {1}.", type.Name, name));

                if (field.HasSimpleValue)
                {
                    value = field.GetFieldValue((ulong)value, inner);
                    if (i != lastElement)
                    {
                        if (DesktopRuntimeBase.IsObjectReference(field.ElementType))
                            obj = (ulong)value;
                        else
                            throw new Exception(string.Format("Field {0}.{1} is not an object reference.", type.Name, name));
                    }

                    inner = false;
                }
                else
                {
                    obj = field.GetFieldAddress(obj, inner);
                    value = obj;
                    inner = true;
                }

                type = field.Type;
                i++;
            }

            return value;
        }


        public override IList<ClrMethod> Methods
        {
            get
            {
                if (m_methods != null)
                    return m_methods;

                IMetadata metadata = null;
                if (m_module != null)
                    metadata = m_module.GetMetadataImport();

                DesktopRuntimeBase runtime = m_heap.m_runtime;
                IList<ulong> mdList = runtime.GetMethodDescList(m_handle);

                if (mdList != null)
                {
                    m_methods = new List<ClrMethod>(mdList.Count);
                    foreach (ulong md in mdList)
                    {
                        if (md == 0)
                            continue;

                        IMethodDescData mdData = runtime.GetMethodDescData(md);
                        DesktopMethod method = DesktopMethod.Create(runtime, metadata, mdData);
                        if (method != null)
                            m_methods.Add(method);
                    }
                }
                else
                {
                    m_methods = new ClrMethod[0];
                }

                return m_methods;
            }
        }



        public override ClrStaticField GetStaticFieldByName(string name)
        {
            foreach (var field in StaticFields)
                if (field.Name == name)
                    return field;

            return null;
        }

        IList<ClrMethod> m_methods;

        public override ClrInstanceField GetFieldByName(string name)
        {
            if (m_fields == null)
                InitFields();

            if (m_fields.Count == 0)
                return null;

            if (m_fieldNameMap == null)
            {
                m_fieldNameMap = new int[m_fields.Count];
                for (int j = 0; j < m_fieldNameMap.Length; ++j)
                    m_fieldNameMap[j] = j;

                Array.Sort(m_fieldNameMap, (x, y) => { return m_fields[x].Name.CompareTo(m_fields[y].Name); });
            }

            int min = 0, max = m_fieldNameMap.Length-1;

            while (max >= min)
            {
                int mid = (max + min) / 2;

                ClrInstanceField field = m_fields[m_fieldNameMap[mid]];
                int comp = field.Name.CompareTo(name);
                if (comp < 0)
                    min = mid + 1;
                else if (comp > 0)
                    max = mid - 1;
                else
                    return m_fields[m_fieldNameMap[mid]];
            }

            return null;
        }

        public override ClrType BaseType
        {
            get
            {
                if (m_parent == 0)
                    return null;

                return m_heap.GetGCHeapType(m_parent, 0, 0);
            }
        }

        public override int GetArrayLength(Address objRef)
        {
            Debug.Assert(IsArray);

            uint res;
            if (!m_heap.m_runtime.ReadDword(objRef + (uint)m_heap.m_runtime.PointerSize, out res))
                res = 0;

            return (int)res;
        }

        public override Address GetArrayElementAddress(Address objRef, int index)
        {
            if (m_baseArrayOffset == 0)
            {
                IObjectData data = m_heap.m_runtime.GetObjectData(objRef);
                if (data == null)
                    return 0;

                m_baseArrayOffset = (int)(data.DataPointer - objRef);
                Debug.Assert(m_baseArrayOffset >= 0);
            }

            return objRef + (Address)(m_baseArrayOffset + index * m_componentSize);
        }

        public override object GetArrayElementValue(Address objRef, int index)
        {
            ulong addr = GetArrayElementAddress(objRef, index);
            if (addr == 0)
                return null;
            
            ClrElementType cet = ClrElementType.Unknown;
            var componentType = this.ArrayComponentType;
            if (componentType != null)
            {
                cet = componentType.ElementType;
            }
            else
            {
                // Slow path, we need to get the element type of the array.
                IObjectData data = m_heap.m_runtime.GetObjectData(objRef);
                if (data == null)
                    return null;

                cet = data.ElementType;
            }

            if (cet == ClrElementType.Unknown)
                return null;

            if (cet == ClrElementType.String && !m_heap.MemoryReader.ReadPtr(addr, out addr))
                    return null;

            return m_heap.GetValueAtAddress(cet, addr);
        }

        public override int BaseSize
        {
            get { return (int)m_baseSize; }
        }
        
        #region private

        /// <summary>
        /// A messy version with better performance that doesn't use regular expression.
        /// </summary>
        internal static int FixGenericsWorker(string name, int start, int end, StringBuilder sb)
        {
            int parenCount = 0;
            while (start < end)
            {
                char c = name[start];
                if (c == '`')
                    break;

                if (c == '[')
                    parenCount++;

                if (c == ']')
                    parenCount--;

                if (parenCount < 0)
                    return start + 1;

                if (c == ',' && parenCount == 0)
                    return start;

                sb.Append(c);
                start++;
            }

            if (start >= end)
                return start;

            start++;

            bool hasSubtypeAirity = false;
            int paramCount = 0;
            do
            {
                int currParamCount = 0;
                hasSubtypeAirity = false;
                // Skip airity.
                while (start < end)
                {
                    char c = name[start];
                    if (c < '0' || c > '9')
                        break;

                    currParamCount = (currParamCount * 10) + c - '0';
                    start++;
                }

                paramCount += currParamCount;
                if (start >= end)
                    return start;

                if (name[start] == '+')
                {
                    while (start < end && name[start] != '[')
                    {
                        if (name[start] == '`')
                        {
                            start++;
                            hasSubtypeAirity = true;
                            break;
                        }

                        sb.Append(name[start]);
                        start++;
                    }

                    if (start >= end)
                        return start;
                }
            } while (hasSubtypeAirity);

            if (name[start] == '[')
            {
                sb.Append('<');
                start++;
                while (paramCount-- > 0)
                {
                    if (start >= end)
                        return start;

                    bool withModule = false;
                    if (name[start] == '[')
                    {
                        withModule = true;
                        start++;
                    }

                    start = FixGenericsWorker(name, start, end, sb);

                    if (start < end && name[start] == '[')
                    {
                        start++;
                        if (start >= end)
                            return start;

                        sb.Append('[');

                        while (start < end && name[start] == ',')
                        {
                            sb.Append(',');
                            start++;
                        }

                        if (start >= end)
                            return start;

                        if (name[start] == ']')
                        {
                            sb.Append(']');
                            start++;
                        }
                    }

                    if (withModule)
                    {
                        while (start < end && name[start] != ']')
                            start++;
                        start++;
                    }

                    if (paramCount > 0)
                    {
                        if (start >= end)
                            return start;

                        //Debug.Assert(name[start] == ',');
                        sb.Append(',');
                        start++;

                        if (start >= end)
                            return start;

                        if (name[start] == ' ')
                            start++;
                    }
                }

                sb.Append('>');
                start++;
            }

            if (start + 1 >= end)
                return start;

            if (name[start] == '[' && name[start + 1] == ']')
                sb.Append("[]");

            return start;
        }

        internal static string FixGenerics(string name)
        {
            StringBuilder builder = new StringBuilder();
            FixGenericsWorker(name, 0, name.Length, builder);
            return builder.ToString();
        }

        internal DesktopHeapType(string typeName, DesktopModule module, uint token, ulong mt, IMethodTableData mtData, DesktopGCHeap heap, int index)
            : base(heap, module, token)
        {
            m_name = typeName;
            m_index = index;

            m_handle = mt;
            Shared = mtData.Shared;
            m_parent = mtData.Parent;
            m_baseSize = mtData.BaseSize;
            m_componentSize = mtData.ComponentSize;
            m_containsPointers = mtData.ContainsPointers;
            m_hasMethods = mtData.NumMethods > 0;
        }

        void InitFlags()
        {
            if ((int)m_attributes != 0 || m_module == null)
                return;

            IMetadata import = m_module.GetMetadataImport();
            if (import == null)
            {
                m_attributes = (TypeAttributes)0x70000000;
                return;
            }

            int tdef;
            int extends;
            int i = import.GetTypeDefProps((int)m_token, null, 0, out tdef, out m_attributes, out extends);
            if (i < 0 || (int)m_attributes == 0)
                m_attributes = (TypeAttributes)0x70000000;
        }


        override public bool IsInternal
        {
            get
            {
                if ((int)m_attributes == 0)
                    InitFlags();

                TypeAttributes visibility = (m_attributes & TypeAttributes.VisibilityMask);
                return visibility == TypeAttributes.NestedAssembly || visibility == TypeAttributes.NotPublic;
            }
        }

        override public bool IsPublic
        {
            get
            {
                if ((int)m_attributes == 0)
                    InitFlags();

                TypeAttributes visibility = (m_attributes & TypeAttributes.VisibilityMask);
                return visibility == TypeAttributes.Public || visibility == TypeAttributes.NestedPublic;
            }
        }

        override public bool IsPrivate
        {
            get
            {
                if ((int)m_attributes == 0)
                    InitFlags();

                TypeAttributes visibility = (m_attributes & TypeAttributes.VisibilityMask);
                return visibility == TypeAttributes.NestedPrivate;
            }
        }

        override public bool IsProtected
        {
            get
            {
                if ((int)m_attributes == 0)
                    InitFlags();

                TypeAttributes visibility = (m_attributes & TypeAttributes.VisibilityMask);
                return visibility == TypeAttributes.NestedFamily;
            }
        }

        override public bool IsAbstract
        {
            get
            {
                if ((int)m_attributes == 0)
                    InitFlags();

                return (m_attributes & TypeAttributes.Abstract) == TypeAttributes.Abstract;
            }
        }

        override public bool IsSealed
        {
            get
            {
                if ((int)m_attributes == 0)
                    InitFlags();

                return (m_attributes & TypeAttributes.Sealed) == TypeAttributes.Sealed;
            }
        }

        override public bool IsInterface
        {
            get
            {
                if ((int)m_attributes == 0)
                    InitFlags();
                return (m_attributes & TypeAttributes.Interface) == TypeAttributes.Interface;
            }
        }


        internal override void SetElementType(ClrElementType value)
        {
            if (m_elementType == ClrElementType.Unknown && value != ClrElementType.Class)
                m_elementType = value;
        }


        internal override Address GetModuleAddress(ClrAppDomain appDomain)
        {
            if (m_module == null)
                return 0;
            return m_module.GetDomainModule(appDomain);
        }

        internal override void SetModule(ClrModule module)
        {
            m_module = (DesktopBaseModule)module;
        }

        public override bool IsRuntimeType
        {
            get
            {
                if (m_runtimeType == null)
                    m_runtimeType = Name == "System.RuntimeType";

                return (bool)m_runtimeType;
            }
        }

        public override ClrType GetRuntimeType(ulong obj)
        {
            if (!IsRuntimeType)
                return null;

            ClrInstanceField field = GetFieldByName("m_handle");
            if (field == null)
                return null;

            ulong methodTable = 0;
            if (field.ElementType == ClrElementType.NativeInt)
            {
                methodTable = (ulong)(long)field.GetFieldValue(obj);
            }
            else if (field.ElementType == ClrElementType.Struct)
            {
                ClrInstanceField ptrField = field.Type.GetFieldByName("m_ptr");
                methodTable = (ulong)(long)ptrField.GetFieldValue(field.GetFieldAddress(obj, false), true);
            }

            return m_heap.GetGCHeapType(methodTable, 0, obj);
        }

        string m_name;
        int m_index;

        TypeAttributes m_attributes;
        GCDesc m_gcDesc;
        ulong m_handle;
        ulong m_parent;
        uint m_baseSize;
        uint m_componentSize;
        bool m_containsPointers;
        byte m_finalizable;

        List<ClrInstanceField> m_fields;
        List<ClrStaticField> m_statics;
        List<ClrThreadStaticField> m_threadStatics;
        int[] m_fieldNameMap;

        int m_baseArrayOffset;
        bool m_hasMethods;
        bool? m_runtimeType;
        private EnumData m_enumData;
        private bool m_notRCW;
        private bool m_checkedIfIsRCW;
        private bool m_checkedIfIsCCW;
        private bool m_notCCW;

        static ClrStaticField[] EmptyStatics = new ClrStaticField[0];
        static ClrThreadStaticField[] EmptyThreadStatics = new ClrThreadStaticField[0];
        #endregion
    }
}
