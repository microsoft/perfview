using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Address = System.UInt64;

namespace Microsoft.Diagnostics.Runtime.Desktop
{
    class DesktopGCHeap : HeapBase
    {
        public DesktopGCHeap(DesktopRuntimeBase runtime, TextWriter log)
            : base(runtime)
        {
            m_runtime = runtime;
            m_log = log;
            m_lastObjType = new LastObjectType();
            m_types = new List<ClrType>(1000);
            Revision = runtime.Revision;

            // Prepopulate a few important method tables.
            m_freeType = GetGCHeapType(m_runtime.FreeMethodTable, 0, 0);
            m_arrayType = GetGCHeapType(m_runtime.ArrayMethodTable, m_runtime.ObjectMethodTable, 0);
            m_objectType = GetGCHeapType(m_runtime.ObjectMethodTable, 0, 0);
            m_arrayType.ArrayComponentType = m_objectType;
            ((BaseDesktopHeapType)m_freeType).SetModule(m_objectType.Module);
            m_stringType = GetGCHeapType(m_runtime.StringMethodTable, 0, 0);
            m_exceptionType = GetGCHeapType(m_runtime.ExceptionMethodTable, 0, 0);

            InitSegments(runtime);
        }

        protected override int GetRuntimeRevision()
        {
            return m_runtime.Revision;
        }

        public override ClrRuntime GetRuntime()
        {
            return m_runtime;
        }

        public override ClrException GetExceptionObject(Address objRef)
        {
            ClrType type = GetObjectType(objRef);
            if (type == null)
                return null;

            // It's possible for the exception handle to go stale on a dead thread.  In this
            // case we will simply return null if we have a valid object at the address, but
            // that object isn't actually an exception.
            if (!type.IsException)
                return null;

            return new DesktopException(objRef, (BaseDesktopHeapType)type);
        }

        public override ClrType GetObjectType(Address objRef)
        {
            ulong mt, cmt = 0;

            if (m_lastObjType.Address == objRef)
                return m_lastObjType.Type;

            var cache = MemoryReader;
            if (cache.Contains(objRef))
            {
                if (!cache.ReadPtr(objRef, out mt))
                    return null;
            }
            else if (m_runtime.MemoryReader.Contains(objRef))
            {
                cache = m_runtime.MemoryReader;
                if (!cache.ReadPtr(objRef, out mt))
                    return null;
            }
            else
            {
                cache = null;
                mt = m_runtime.DataReader.ReadPointerUnsafe(objRef);
            }

            if ((((int)mt) & 3) != 0)
                mt &= ~3UL;

            if (mt == m_runtime.ArrayMethodTable)
            {
                uint elemenTypeOffset = (uint)PointerSize * 2;
                if (cache == null)
                    cmt = m_runtime.DataReader.ReadPointerUnsafe(objRef + elemenTypeOffset);
                else if (!cache.ReadPtr(objRef + elemenTypeOffset, out cmt))
                    return null;
            }
            else
            {
                cmt = 0;
            }

            ClrType type = GetGCHeapType(mt, cmt, objRef);
            m_lastObjType.Address = objRef;
            m_lastObjType.Type = type;

            return type;
        }


        internal ClrType GetGCHeapType(ulong mt, ulong cmt)
        {
            return GetGCHeapType(mt, cmt, 0);
        }

        internal ClrType GetGCHeapType(ulong mt, ulong cmt, ulong obj)
        {
            if (mt == 0)
                return null;

            TypeHandle hnd = new TypeHandle(mt, cmt);
            ClrType ret = null;

            // See if we already have the type.
            int index;
            if (m_indices.TryGetValue(hnd, out index))
            {
                ret = m_types[index];
            }
            else if (mt == m_runtime.ArrayMethodTable && cmt == 0)
            {
                // Handle the case where the methodtable is an array, but the component method table
                // was not specified.  (This happens with fields.)  In this case, return System.Object[],
                // with an ArrayComponentType set to System.Object.
                uint token = m_runtime.GetMetadataToken(mt);
                if (token == 0xffffffff)
                    return null;

                ModuleEntry modEnt = new ModuleEntry(m_arrayType.Module, token);

                ret = m_arrayType;
                index = m_types.Count;

                m_indices[hnd] = index;
                m_typeEntry[modEnt] = index;
                m_types.Add(ret);

                Debug.Assert(m_types[(int)index] == ret);
            }
            else
            {
                // No, so we'll have to construct it.
                var moduleAddr = m_runtime.GetModuleForMT(hnd.MethodTable);
                DesktopModule module = m_runtime.GetModule(moduleAddr);
                uint token = m_runtime.GetMetadataToken(mt);

                bool isFree = mt == m_runtime.FreeMethodTable;
                if (token == 0xffffffff && !isFree)
                    return null;

                // Dynamic functions/modules
                uint tokenEnt = token;
                if (!isFree && (module == null || module.IsDynamic))
                    tokenEnt = (uint)mt;

                ModuleEntry modEnt = new ModuleEntry(module, tokenEnt);

                // We key the dictionary on a Module/Token pair.  If names do not match, then
                // do not treat these as the same type (happens with generics).
                string typeName = m_runtime.GetTypeName(hnd);
                if (typeName == null || typeName == "<Unloaded Type>")
                {
                    var builder = GetTypeNameFromToken(module, token);
                    typeName = (builder != null) ? builder.ToString() : "<UNKNOWN>";
                }
                else
                {
                    typeName = DesktopHeapType.FixGenerics(typeName);
                }

                if (m_typeEntry.TryGetValue(modEnt, out index))
                {
                    BaseDesktopHeapType match = (BaseDesktopHeapType)m_types[(int)index];
                    if (match.Name == typeName)
                    {
                        m_indices[hnd] = index;
                        ret = match;
                    }
                }

                if (ret == null)
                {
                    IMethodTableData mtData = m_runtime.GetMethodTableData(mt);
                    if (mtData == null)
                        return null;

                    index = m_types.Count;
                    ret = new DesktopHeapType(typeName, module, token, mt, mtData, this, index);

                    m_indices[hnd] = index;
                    m_typeEntry[modEnt] = index;
                    m_types.Add(ret);

                    Debug.Assert(m_types[(int)index] == ret);
                }
            }

            if (obj != 0 && ret.ArrayComponentType == null && ret.IsArray)
            {
                IObjectData data = GetObjectData(obj);
                if (data != null)
                {
                    if (data.ElementTypeHandle != 0)
                        ret.ArrayComponentType = GetGCHeapType(data.ElementTypeHandle, 0, 0);

                    if (ret.ArrayComponentType == null && data.ElementType != ClrElementType.Unknown)
                        ret.ArrayComponentType = GetBasicType(data.ElementType);
                }
            }

            return ret;
        }

        private static StringBuilder GetTypeNameFromToken(DesktopModule module, uint token)
        {
            if (module == null)
                return null;

            IMetadata meta = module.GetMetadataImport();
            if (meta == null)
                return null;

            // Get type name.
            int ptkExtends;
            int typeDefLen;
            System.Reflection.TypeAttributes typeAttrs;
            StringBuilder typeBuilder = new StringBuilder(256);
            int res = meta.GetTypeDefProps((int)token, typeBuilder, typeBuilder.Capacity, out typeDefLen, out typeAttrs, out ptkExtends);
            if (res < 0)
                return null;

            int enclosing = 0;
            res = meta.GetNestedClassProps((int)token, out enclosing);
            if (res == 0 && token != enclosing)
            {
                StringBuilder inner = GetTypeNameFromToken(module, (uint)enclosing);
                if (inner == null)
                {
                    inner = new StringBuilder(typeBuilder.Capacity + 16);
                    inner.Append("<UNKNOWN>");
                }

                inner.Append('+');
                inner.Append(typeBuilder);
                return inner;
            }

            return typeBuilder;
        }



        public override IEnumerable<ulong> EnumerateFinalizableObjects()
        {
            SubHeap[] heaps;
            if (m_runtime.GetHeaps(out heaps))
            {
                foreach (SubHeap heap in heaps)
                {
                    foreach (Address obj in m_runtime.GetPointersInRange(heap.FQLiveStart, heap.FQLiveStop))
                    {
                        if (obj == 0)
                            continue;

                        var type = GetObjectType(obj);
                        if (type != null && !type.IsFinalizeSuppressed(obj))
                            yield return obj;
                    }
                }
            }
        }

        BlockingObject[] m_managedLocks;

        public override IEnumerable<BlockingObject> EnumerateBlockingObjects()
        {
            InitLockInspection();
            return m_managedLocks;
        }

        internal void InitLockInspection()
        {
            if (m_managedLocks != null)
                return;

            LockInspection li = new LockInspection(this, m_runtime);
            m_managedLocks = li.InitLockInspection();
        }

        public override IEnumerable<ClrRoot> EnumerateRoots()
        {
            return EnumerateRoots(true);
        }

        public override IEnumerable<ClrRoot> EnumerateRoots(bool enumerateStatics)
        {
            if (enumerateStatics)
            {
                // Statics
                foreach (var type in EnumerateTypes())
                {
                    // Statics
                    foreach (var staticField in type.StaticFields)
                    {
                        if (!ClrRuntime.IsPrimitive(staticField.ElementType))
                        {
                            foreach (var ad in m_runtime.AppDomains)
                            {
                                ulong addr = 0;
                                ulong value = 0;
                                // We must manually get the value, as strings will not be returned as an object address.
                                try // If this fails for whatever reasion, don't fail completely.  
                                {
                                    addr = staticField.GetFieldAddress(ad);
                                }
                                catch (Exception e)
                                {
                                    Trace.WriteLine(string.Format("Error getting stack field {0}.{1}: {2}", type.Name, staticField.Name, e.Message));
                                    goto NextStatic;
                                }

                                if (m_runtime.ReadPointer(addr, out value) && value != 0)
                                {
                                    ClrType objType = GetObjectType(value);
                                    if (objType != null)
                                        yield return new StaticVarRoot(addr, value, objType, type.Name, staticField.Name, ad);
                                }
                            }
                        }
                    NextStatic: ;
                    }

                    // Thread statics
                    foreach (var tsf in type.ThreadStaticFields)
                    {
                        if (ClrRuntime.IsObjectReference(tsf.ElementType))
                        {
                            foreach (var ad in m_runtime.AppDomains)
                            {
                                foreach (var thread in m_runtime.Threads)
                                {
                                    // We must manually get the value, as strings will not be returned as an object address.
                                    ulong addr = tsf.GetFieldAddress(ad, thread);
                                    ulong value = 0;

                                    if (m_runtime.ReadPointer(addr, out value) && value != 0)
                                    {
                                        ClrType objType = GetObjectType(value);
                                        if (objType != null)
                                            yield return new ThreadStaticVarRoot(addr, value, objType, type.Name, tsf.Name, ad);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Handles
            var handles = m_runtime.EnumerateHandles();
            if (handles != null)
            {
                foreach (ClrHandle handle in handles)
                {
                    Address objAddr = handle.Object;
                    GCRootKind kind = GCRootKind.Strong;
                    if (objAddr != 0)
                    {
                        ClrType type = GetObjectType(objAddr);
                        if (type != null)
                        {
                            switch (handle.HandleType)
                            {
                                case HandleType.WeakShort:
                                case HandleType.WeakLong:
                                    break;
                                case HandleType.RefCount:
                                    if (handle.RefCount <= 0)
                                        break;
                                    goto case HandleType.Strong;
                                case HandleType.Dependent:
                                    if (objAddr == 0)
                                        continue;
                                    objAddr = handle.DependentTarget;
                                    goto case HandleType.Strong;
                                case HandleType.Pinned:
                                    kind = GCRootKind.Pinning;
                                    goto case HandleType.Strong;
                                case HandleType.AsyncPinned:
                                    kind = GCRootKind.AsyncPinning;
                                    goto case HandleType.Strong;
                                case HandleType.Strong:
                                case HandleType.SizedRef:
                                    yield return new HandleRoot(handle.Address, objAddr, type, handle.HandleType, kind);

                                    // Async pinned handles keep 1 or more "sub objects" alive.  I will report them here as their own pinned handle.
                                    if (handle.HandleType == HandleType.AsyncPinned)
                                    {
                                        ClrInstanceField userObjectField = type.GetFieldByName("m_userObject");
                                        if (userObjectField != null)
                                        {
                                            ulong _userObjAddr = userObjectField.GetFieldAddress(objAddr);
                                            ulong _userObj = (ulong)userObjectField.GetFieldValue(objAddr);
                                            var _userObjType = GetObjectType(_userObj);
                                            if (_userObjType != null)
                                            {
                                                if (_userObjType.IsArray)
                                                {
                                                    if (_userObjType.ArrayComponentType != null)
                                                    {
                                                        if (_userObjType.ArrayComponentType.ElementType == ClrElementType.Object)
                                                        {
                                                            // report elements
                                                            int len = _userObjType.GetArrayLength(_userObj);
                                                            for (int i = 0; i < len; ++i)
                                                            {
                                                                ulong indexAddr = _userObjType.GetArrayElementAddress(_userObj, i);
                                                                ulong indexObj = (ulong)_userObjType.GetArrayElementValue(_userObj, i);
                                                                ClrType indexObjType = GetObjectType(indexObj);

                                                                if (indexObj != 0 && indexObjType != null)
                                                                    yield return new HandleRoot(indexAddr, indexObj, indexObjType, HandleType.AsyncPinned, GCRootKind.AsyncPinning);
                                                            }
                                                        }
                                                        else
                                                        {
                                                            yield return new HandleRoot(_userObjAddr, _userObj, _userObjType, HandleType.AsyncPinned, GCRootKind.AsyncPinning);
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    yield return new HandleRoot(_userObjAddr, _userObj, _userObjType, HandleType.AsyncPinned, GCRootKind.AsyncPinning);
                                                }
                                            }
                                        }


                                    }

                                    break;
                                default:
                                    Debug.WriteLine("Warning, unknown handle type {0} ignored", Enum.GetName(typeof(HandleType), handle.HandleType));
                                    break;
                            }
                        }
                    }
                }
            }
            else
            {
                Trace.WriteLine("Warning, GetHandles() return null!");
            }

            // Finalization Queue
            foreach (Address objAddr in m_runtime.EnumerateFinalizerQueue())
                if (objAddr != 0)
                {
                    ClrType type = GetObjectType(objAddr);
                    if (type != null)
                        yield return new FinalizerRoot(objAddr, type);
                }

            // Threads
            foreach (ClrThread thread in m_runtime.Threads)
                if (thread.IsAlive)
                    foreach (var root in thread.EnumerateStackObjects(false))
                        yield return root;
        }


        internal string GetStringContents(Address strAddr)
        {
            if (strAddr == 0)
                return null;

            if (m_firstChar == null || m_stringLength == null)
            {
                m_firstChar = m_stringType.GetFieldByName("m_firstChar");
                m_stringLength = m_stringType.GetFieldByName("m_stringLength");

                //Debug.Assert(m_firstChar != null && m_stringLength != null);
                //Debug.Assert(m_firstChar == null || m_firstChar.Offset + IntPtr.Size == m_runtime.GetStringFirstCharOffset());
                //Debug.Assert(m_stringLength == null || m_stringLength.Offset + IntPtr.Size == m_runtime.GetStringLengthOffset());
            }

            int length = 0;
            if (m_stringLength != null)
                length = (int)m_stringLength.GetFieldValue(strAddr);
            else if (!m_runtime.ReadDword(strAddr + m_runtime.GetStringLengthOffset(), out length))
                return null;

            
            if (length == 0)
                return "";

            Address data = 0;
            if (m_firstChar != null)
                data = m_firstChar.GetFieldAddress(strAddr);
            else
                data = strAddr + m_runtime.GetStringFirstCharOffset();

            byte[] buffer = new byte[length * 2];
            int read;
            if (!m_runtime.ReadMemory(data, buffer, buffer.Length, out read))
                return null;

            return UnicodeEncoding.Unicode.GetString(buffer);
        }

        public override int ReadMemory(Address address, byte[] buffer, int offset, int count)
        {
            if (offset != 0)
                throw new NotImplementedException("Non-zero offsets not supported (yet)");

            int bytesRead = 0;
            if (!m_runtime.ReadMemory(address, buffer, count, out bytesRead))
                return 0;
            return (int)bytesRead;
        }

        public override IEnumerable<ClrType> EnumerateTypes()
        {
            LoadAllTypes();

            for (int i = 0; i < m_types.Count; ++i)
                yield return m_types[i];
        }


        internal bool TypesLoaded { get { return m_loadedTypes; } }

        internal void LoadAllTypes()
        {
            if (m_loadedTypes)
                return;

            m_loadedTypes = true;

            // Walking a module is sloooow.  Ensure we only walk each module once.
            HashSet<Address> modules = new HashSet<Address>();

            foreach (Address module in m_runtime.EnumerateModules(m_runtime.GetAppDomainData(m_runtime.SystemDomainAddress)))
                modules.Add(module);

            foreach (Address module in m_runtime.EnumerateModules(m_runtime.GetAppDomainData(m_runtime.SharedDomainAddress)))
                modules.Add(module);

            IAppDomainStoreData ads = m_runtime.GetAppDomainStoreData();
            IList<Address> appDomains = m_runtime.GetAppDomainList(ads.Count);


            foreach (Address ad in appDomains)
            {
                var adData = m_runtime.GetAppDomainData(ad);
                if (adData != null)
                {
                    foreach (Address module in m_runtime.EnumerateModules(adData))
                        modules.Add(module);
                }
                else if (m_log != null)
                {
                    m_log.WriteLine("Error: Could not get appdomain information from Appdomain {0:x}.  Skipping.", ad);
                }
            }

            ulong arrayMt = m_runtime.ArrayMethodTable;
            foreach (var module in modules)
            {
                var mtList = m_runtime.GetMethodTableList(module);
                if (mtList != null)
                {
                    foreach (ulong mt in mtList)
                    {
                        if (mt != arrayMt)
                        {
                            // prefetch element type, as this also can load types
                            var type = GetGCHeapType(mt, 0, 0);
                            if (type != null)
                            {
                                ClrElementType cet = type.ElementType;
                            }
                        }
                    }
                }
                else if (m_log != null)
                {
                    m_log.WriteLine("Error: Could not get method table list for module {0:x}.  Skipping.", module);
                }
            }
        }

        internal bool GetObjectHeader(ulong obj, out uint value)
        {
            return MemoryReader.TryReadDword(obj - 4, out value);
        }

        internal IObjectData GetObjectData(Address address)
        {
            LastObjectData last = m_lastObjData;

            if (m_lastObjData != null && m_lastObjData.Address == address)
                return m_lastObjData.Data;

            last = new LastObjectData(address, m_runtime.GetObjectData(address));
            m_lastObjData = last;

            return last.Data;
        }

        internal object GetValueAtAddress(ClrElementType cet, Address addr)
        {
            switch (cet)
            {
                case ClrElementType.String:
                    return GetStringContents(addr);

                case ClrElementType.Class:
                case ClrElementType.Array:
                case ClrElementType.SZArray:
                case ClrElementType.Object:
                    {
                        Address val;
                        if (!MemoryReader.TryReadPtr(addr, out val))
                            return null;

                        return val;
                    }

                case ClrElementType.Boolean:
                    {
                        byte val;
                        if (!m_runtime.ReadByte(addr, out val))
                            return null;
                        return val != 0;
                    }

                case ClrElementType.Int32:
                    {
                        int val;
                        if (!m_runtime.ReadDword(addr, out val))
                            return null;

                        return val;
                    }

                case ClrElementType.UInt32:
                    {
                        uint val;
                        if (!m_runtime.ReadDword(addr, out val))
                            return null;

                        return val;
                    }

                case ClrElementType.Int64:
                    {
                        long val;
                        if (!m_runtime.ReadQword(addr, out val))
                            return long.MaxValue;

                        return val;
                    }

                case ClrElementType.UInt64:
                    {
                        ulong val;
                        if (!m_runtime.ReadQword(addr, out val))
                            return long.MaxValue;

                        return val;
                    }

                case ClrElementType.NativeUInt:  // native unsigned int
                case ClrElementType.Pointer:
                case ClrElementType.FunctionPointer:
                    {
                        ulong val;
                        if (!MemoryReader.TryReadPtr(addr, out val))
                            return null;

                        return val;
                    }

                case ClrElementType.NativeInt:  // native int
                    {
                        ulong val;
                        if (!MemoryReader.TryReadPtr(addr, out val))
                            return null;

                        return (long)val;
                    }

                case ClrElementType.Int8:
                    {
                        sbyte val;
                        if (!m_runtime.ReadByte(addr, out val))
                            return null;
                        return val;
                    }

                case ClrElementType.UInt8:
                    {
                        byte val;
                        if (!m_runtime.ReadByte(addr, out val))
                            return null;
                        return val;
                    }

                case ClrElementType.Float:
                    {
                        float val;
                        if (!m_runtime.ReadFloat(addr, out val))
                            return null;
                        return val;
                    }

                case ClrElementType.Double: // double
                    {
                        double val;
                        if (!m_runtime.ReadFloat(addr, out val))
                            return null;
                        return val;
                    }

                case ClrElementType.Int16:
                    {
                        short val;
                        if (!m_runtime.ReadShort(addr, out val))
                            return null;
                        return val;
                    }

                case ClrElementType.Char:  // u2
                    {
                        ushort val;
                        if (!m_runtime.ReadShort(addr, out val))
                            return null;
                        return (char)val;
                    }

                case ClrElementType.UInt16:
                    {
                        ushort val;
                        if (!m_runtime.ReadShort(addr, out val))
                            return null;
                        return val;
                    }
            }

            throw new Exception("Unexpected element type.");
        }

        internal ClrElementType GetElementType(BaseDesktopHeapType type, int depth)
        {
            // Max recursion.
            if (depth >= 32)
                return ClrElementType.Object;

            if (type == m_objectType)
                return ClrElementType.Object;
            else if (type == m_stringType)
                return ClrElementType.String;
            else if (type.ElementSize > 0)
                return ClrElementType.SZArray;

            BaseDesktopHeapType baseType = (BaseDesktopHeapType)type.BaseType;
            if (baseType == null || baseType == m_objectType)
                return ClrElementType.Object;

            bool vc = false;
            if (m_valueType == null)
            {
                if (baseType.Name == "System.ValueType")
                {
                    m_valueType = baseType;
                    vc = true;
                }
            }
            else if (baseType == m_valueType)
            {
                vc = true;
            }

            if (!vc)
            {
                ClrElementType et = baseType.m_elementType;
                if (et == ClrElementType.Unknown)
                {
                    et = GetElementType(baseType, depth + 1);
                    baseType.m_elementType = et;
                }

                return et;
            }

            switch (type.Name)
            {
                case "System.Int32":
                    return ClrElementType.Int32;
                case "System.Int16":
                    return ClrElementType.Int16;
                case "System.Int64":
                    return ClrElementType.Int64;
                case "System.IntPtr":
                    return ClrElementType.NativeInt;
                case "System.UInt16":
                    return ClrElementType.UInt16;
                case "System.UInt32":
                    return ClrElementType.UInt32;
                case "System.UInt64":
                    return ClrElementType.UInt64;
                case "System.UIntPtr":
                    return ClrElementType.NativeUInt;
                case "System.Boolean":
                    return ClrElementType.Boolean;
                case "System.Single":
                    return ClrElementType.Float;
                case "System.Double":
                    return ClrElementType.Double;
                case "System.Byte":
                    return ClrElementType.UInt8;
                case "System.Char":
                    return ClrElementType.Char;
                case "System.SByte":
                    return ClrElementType.Int8;
                case "System.Enum":
                    return ClrElementType.Int32;

            }

            return ClrElementType.Struct;
        }


        #region private
        private TextWriter m_log;
        internal DesktopRuntimeBase m_runtime;
        internal List<ClrType> m_types;
        Dictionary<TypeHandle, int> m_indices = new Dictionary<TypeHandle, int>(TypeHandle.EqualityComparer);
        Dictionary<ArrayRankHandle, BaseDesktopHeapType> m_arrayTypes;
        ClrModule m_mscorlib;

        Dictionary<ModuleEntry, int> m_typeEntry = new Dictionary<ModuleEntry, int>(new ModuleEntryCompare());
        ClrInstanceField m_firstChar, m_stringLength;
        LastObjectData m_lastObjData;
        internal LastObjectType m_lastObjType;
        internal ClrType m_objectType, m_stringType, m_valueType, m_freeType, m_exceptionType, m_enumType, m_arrayType;
        ClrType[] m_basicTypes;
        bool m_loadedTypes = false;
        internal ClrInterface[] m_emptyInterfaceList = new ClrInterface[0];
        public Dictionary<string, ClrInterface> m_interfaces = new Dictionary<string, ClrInterface>();
        #endregion

        class LastObjectData
        {
            public IObjectData Data;
            public Address Address;
            public LastObjectData(Address addr, IObjectData data)
            {
                Address = addr;
                Data = data;
            }
        }

        internal struct LastObjectType
        {
            public Address Address;
            public ClrType Type;
        }

        class ModuleEntry
        {
            public ClrModule Module;
            public uint Token;
            public ModuleEntry(ClrModule module, uint token)
            {
                Module = module;
                Token = token;
            }
        }

        class ModuleEntryCompare : IEqualityComparer<ModuleEntry>
        {
            public bool Equals(ModuleEntry mx, ModuleEntry my)
            {
                return mx.Token == my.Token && mx.Module == my.Module;
            }

            public int GetHashCode(ModuleEntry obj)
            {
                return (int)obj.Token;
            }
        }

        public override ClrType GetTypeByIndex(int index)
        {
            return m_types[index];
        }

        public override int TypeIndexLimit
        {
            get { return m_types.Count; }
        }

        internal ClrType GetBasicType(ClrElementType elType)
        {
            // Early out without having to construct the array.
            if (m_basicTypes == null)
            {
                switch (elType)
                {
                    case ClrElementType.String:
                        return m_stringType;

                    case ClrElementType.Array:
                    case ClrElementType.SZArray:
                        return m_arrayType;

                    case ClrElementType.Object:
                    case ClrElementType.Class:
                        return m_objectType;

                    case ClrElementType.Struct:
                        if (m_valueType != null)
                            return m_valueType;
                        break;
                }
            }

            if (m_basicTypes == null)
                InitBasicTypes();


            return m_basicTypes[(int)elType];
        }

        private void InitBasicTypes()
        {
            const int max = (int)ClrElementType.SZArray + 1;

            m_basicTypes = new ClrType[max];
            m_basicTypes[(int)ClrElementType.Unknown] = null;  // ???
            m_basicTypes[(int)ClrElementType.String] = m_stringType;
            m_basicTypes[(int)ClrElementType.Array] = m_arrayType;
            m_basicTypes[(int)ClrElementType.SZArray] = m_arrayType;
            m_basicTypes[(int)ClrElementType.Object] = m_objectType;
            m_basicTypes[(int)ClrElementType.Class] = m_objectType;

            ClrModule mscorlib = Mscorlib;
            if (mscorlib == null)
                return;

            int count = 0;
            foreach (ClrType type in mscorlib.EnumerateTypes())
            {
                if (count == 14)
                    break;

                switch (type.Name)
                {
                    case "System.Boolean":
                        Debug.Assert(m_basicTypes[(int)ClrElementType.Boolean] == null);
                        m_basicTypes[(int)ClrElementType.Boolean] = type;
                        count++;
                        break;

                    case "System.Char":
                        Debug.Assert(m_basicTypes[(int)ClrElementType.Char] == null);
                        m_basicTypes[(int)ClrElementType.Char] = type;
                        count++;
                        break;

                    case "System.SByte":
                        Debug.Assert(m_basicTypes[(int)ClrElementType.Int8] == null);
                        m_basicTypes[(int)ClrElementType.Int8] = type;
                        count++;
                        break;

                    case "System.Byte":
                        Debug.Assert(m_basicTypes[(int)ClrElementType.UInt8] == null);
                        m_basicTypes[(int)ClrElementType.UInt8] = type;
                        count++;
                        break;

                    case "System.Int16":
                        Debug.Assert(m_basicTypes[(int)ClrElementType.Int16] == null);
                        m_basicTypes[(int)ClrElementType.Int16] = type;
                        count++;
                        break;

                    case "System.UInt16":
                        Debug.Assert(m_basicTypes[(int)ClrElementType.UInt16] == null);
                        m_basicTypes[(int)ClrElementType.UInt16] = type;
                        count++;
                        break;

                    case "System.Int32":
                        Debug.Assert(m_basicTypes[(int)ClrElementType.Int32] == null);
                        m_basicTypes[(int)ClrElementType.Int32] = type;
                        count++;
                        break;

                    case "System.UInt32":
                        Debug.Assert(m_basicTypes[(int)ClrElementType.UInt32] == null);
                        m_basicTypes[(int)ClrElementType.UInt32] = type;
                        count++;
                        break;

                    case "System.Int64":
                        Debug.Assert(m_basicTypes[(int)ClrElementType.Int64] == null);
                        m_basicTypes[(int)ClrElementType.Int64] = type;
                        count++;
                        break;

                    case "System.UInt64":
                        Debug.Assert(m_basicTypes[(int)ClrElementType.UInt64] == null);
                        m_basicTypes[(int)ClrElementType.UInt64] = type;
                        count++;
                        break;

                    case "System.Single":
                        Debug.Assert(m_basicTypes[(int)ClrElementType.Float] == null);
                        m_basicTypes[(int)ClrElementType.Float] = type;
                        count++;
                        break;

                    case "System.Double":
                        Debug.Assert(m_basicTypes[(int)ClrElementType.Double] == null);
                        m_basicTypes[(int)ClrElementType.Double] = type;
                        count++;
                        break;

                    case "System.IntPtr":
                        Debug.Assert(m_basicTypes[(int)ClrElementType.NativeInt] == null);
                        m_basicTypes[(int)ClrElementType.NativeInt] = type;
                        count++;
                        break;

                    case "System.UIntPtr":
                        Debug.Assert(m_basicTypes[(int)ClrElementType.NativeUInt] == null);
                        m_basicTypes[(int)ClrElementType.NativeUInt] = type;
                        count++;
                        break;
                }
            }

            Debug.Assert(count == 14);
        }

        internal BaseDesktopHeapType GetArrayType(ClrElementType clrElementType, int ranks, string nameHint)
        {
            if (m_arrayTypes == null)
                m_arrayTypes = new Dictionary<ArrayRankHandle, BaseDesktopHeapType>();

            var handle = new ArrayRankHandle(clrElementType, ranks);
            BaseDesktopHeapType result;
            if (!m_arrayTypes.TryGetValue(handle, out result))
                m_arrayTypes[handle] = result = new DesktopArrayType(this, (DesktopBaseModule)Mscorlib, clrElementType, ranks, m_arrayType.MetadataToken, nameHint);

            return result;
        }

        internal ClrModule Mscorlib
        {
            get
            {
                if (m_mscorlib == null)
                {
                    foreach (ClrModule module in m_runtime.EnumerateModules())
                    {
                        if (module.Name.Contains("mscorlib"))
                        {
                            m_mscorlib = module;
                            break;
                        }
                    }
                }

                return m_mscorlib;
            }
        }
    }


    class FinalizerRoot : ClrRoot
    {
        private ClrType m_type;
        public FinalizerRoot(ulong obj, ClrType type)
        {
            Object = obj;
            m_type = type;
        }

        public override GCRootKind Kind
        {
            get { return GCRootKind.Finalizer; }
        }

        public override string Name
        {
            get
            {
                return "finalization handle";
            }
        }

        public override ClrType Type
        {
            get { return m_type; }
        }
    }

    class HandleRoot : ClrRoot
    {
        GCRootKind m_kind;
        string m_name;
        private ClrType m_type;

        public HandleRoot(ulong addr, ulong obj, ClrType type, HandleType hndType, GCRootKind kind)
        {
            m_name = Enum.GetName(typeof(HandleType), hndType) + " handle";
            Address = addr;
            Object = obj;
            m_kind = kind;
            m_type = type;
        }

        public override bool IsPinned
        {
            get
            {
                return Kind == GCRootKind.Pinning || Kind == GCRootKind.AsyncPinning;
            }
        }

        public override GCRootKind Kind
        {
            get { return m_kind; }
        }

        public override string Name
        {
            get
            {
                return m_name;
            }
        }

        public override ClrType Type
        {
            get { return m_type; }
        }
    }

    class StaticVarRoot : ClrRoot
    {
        string m_name;
        ClrAppDomain m_domain;
        private ClrType m_type;

        public StaticVarRoot(ulong addr, ulong obj, ClrType type, string typeName, string variableName, ClrAppDomain appDomain)
        {
            Address = addr;
            Object = obj;
            m_name = string.Format("static var {0}.{1}", typeName, variableName);
            m_domain = appDomain;
            m_type = type;
        }

        public override ClrAppDomain AppDomain
        {
            get
            {
                return m_domain;
            }
        }

        public override GCRootKind Kind
        {
            get { return GCRootKind.StaticVar; }
        }

        public override string Name
        {
            get
            {
                return m_name;
            }
        }

        public override ClrType Type
        {
            get { return m_type; }
        }
    }

    class ThreadStaticVarRoot : ClrRoot
    {
        string m_name;
        ClrAppDomain m_domain;
        ClrType m_type;

        public ThreadStaticVarRoot(ulong addr, ulong obj, ClrType type, string typeName, string variableName, ClrAppDomain appDomain)
        {
            Address = addr;
            Object = obj;
            m_name = string.Format("thread static var {0}.{1}", typeName, variableName);
            m_domain = appDomain;
            m_type = type;
        }

        public override ClrAppDomain AppDomain
        {
            get
            {
                return m_domain;
            }
        }

        public override GCRootKind Kind
        {
            get { return GCRootKind.ThreadStaticVar; }
        }

        public override string Name
        {
            get
            {
                return m_name;
            }
        }

        public override ClrType Type
        {
            get { return m_type; }
        }
    }

    class DesktopBlockingObject : BlockingObject
    {
        Address m_obj;
        bool m_locked;
        int m_recursion;
        IList<ClrThread> m_waiters;
        BlockingReason m_reason;
        ClrThread[] m_owners;

        static readonly ClrThread[] EmptyWaiters = new ClrThread[0];

        internal void SetOwners(ClrThread[] owners)
        {
            m_owners = owners;
        }

        internal void SetOwner(ClrThread owner)
        {
            m_owners = new ClrThread[0];
            m_owners[0] = owner;
        }

        public DesktopBlockingObject(Address obj, bool locked, int recursion, ClrThread owner, BlockingReason reason)
        {
            m_obj = obj;
            m_locked = locked;
            m_recursion = recursion;
            m_reason = reason;
            m_owners = new ClrThread[1];
            m_owners[0] = owner;
        }

        public DesktopBlockingObject(Address obj, bool locked, int recursion, BlockingReason reason, ClrThread[] owners)
        {
            m_obj = obj;
            m_locked = locked;
            m_recursion = recursion;
            m_reason = reason;
            m_owners = owners;
        }

        public DesktopBlockingObject(Address obj, bool locked, int recursion, BlockingReason reason)
        {
            m_obj = obj;
            m_locked = locked;
            m_recursion = recursion;
            m_reason = reason;
        }

        public override Address Object
        {
            get { return m_obj; }
        }

        public override bool Taken
        {
            get { return m_locked; }
        }

        public void SetTaken(bool status)
        {
            m_locked = status;
        }

        public override int RecursionCount
        {
            get { return m_recursion; }
        }


        public override IList<ClrThread> Waiters
        {
            get
            {
                if (m_waiters == null)
                    return EmptyWaiters;

                return m_waiters;
            }
        }

        internal void AddWaiter(ClrThread thread)
        {
            if (thread == null)
                return;

            if (m_waiters == null)
                m_waiters = new List<ClrThread>();

            m_waiters.Add(thread);
            m_locked = true;
        }

        public override BlockingReason Reason
        {
            get { return m_reason; }
            internal set { m_reason = value; }
        }

        public override ClrThread Owner
        {
            get
            {
                if (!HasSingleOwner)
                    throw new InvalidOperationException("BlockingObject has more than one owner.");

                return m_owners[0];
            }
        }

        public override bool HasSingleOwner
        {
            get { return m_owners.Length == 1; }
        }

        public override IList<ClrThread> Owners
        {
            get
            {
                return m_owners ?? new ClrThread[0];
            }
        }
    }

    class DesktopException : ClrException
    {
        public DesktopException(Address objRef, BaseDesktopHeapType type)
        {
            m_object = objRef;
            m_type = type;
        }

        public override ClrType Type
        {
            get { return m_type; }
        }

        public override string Message
        {
            get
            {
                var field = m_type.GetFieldByName("_message");
                if (field != null)
                    return (string)field.GetFieldValue(m_object);

                var runtime = m_type.m_heap.m_runtime;
                uint offset = runtime.GetExceptionMessageOffset();
                Debug.Assert(offset > 0);

                ulong message = m_object + offset;
                if (!runtime.ReadPointer(message, out message))
                    return null;

                return m_type.m_heap.GetStringContents(message);
            }
        }

        public override Address Address
        {
            get { return m_object; }
        }

        public override ClrException Inner
        {
            get
            {
                // TODO:  This needs to get the field offset by runtime instead.
                var field = m_type.GetFieldByName("_innerException");
                if (field == null)
                    return null;
                object inner = field.GetFieldValue(m_object);
                if (inner == null || !(inner is ulong) || ((ulong)inner == 0))
                    return null;

                ulong ex = (ulong)inner;
                BaseDesktopHeapType type = (BaseDesktopHeapType)m_type.m_heap.GetObjectType(ex);

                return new DesktopException(ex, type);
            }
        }

        public override IList<ClrStackFrame> StackTrace
        {
            get
            {
                if (m_stackTrace == null)
                    m_stackTrace = m_type.m_heap.m_runtime.GetExceptionStackTrace(m_object, m_type);

                return m_stackTrace;
            }
        }

        public override int HResult
        {
            get
            {
                var field = m_type.GetFieldByName("_HResult");
                if (field != null)
                    return (int)field.GetFieldValue(m_object);

                int hr = 0;
                var runtime = m_type.m_heap.m_runtime;
                uint offset = runtime.GetExceptionHROffset();
                runtime.ReadDword(m_object + offset, out hr);

                return hr;
            }
        }

        #region Private
        private Address m_object;
        private BaseDesktopHeapType m_type;
        private IList<ClrStackFrame> m_stackTrace;
        #endregion
    }

    struct ArrayRankHandle : IEquatable<ArrayRankHandle>
    {
        private ClrElementType m_type;
        private int m_ranks;

        public ArrayRankHandle(ClrElementType eltype, int ranks)
        {
            m_type = eltype;
            m_ranks = ranks;
        }

        public bool Equals(ArrayRankHandle other)
        {
            return m_type == other.m_type && m_ranks == other.m_ranks;
        }
    }

    struct TypeHandle : IEquatable<TypeHandle>
    {
        public Address MethodTable;
        public Address ComponentMethodTable;

        #region Constructors
        public TypeHandle(ulong mt)
        {
            MethodTable = mt;
            ComponentMethodTable = 0;
        }

        public TypeHandle(ulong mt, ulong cmt)
        {
            MethodTable = mt;
            ComponentMethodTable = cmt;
        }
        #endregion

        public override int GetHashCode()
        {
            return ((int)MethodTable + (int)ComponentMethodTable) >> 3;
        }

        bool IEquatable<TypeHandle>.Equals(TypeHandle other)
        {
            return (MethodTable == other.MethodTable) && (ComponentMethodTable == other.ComponentMethodTable);
        }

        #region Compare Helpers
        // TODO should not be needed.   IEquatable should cover it.  
        public static IEqualityComparer<TypeHandle> EqualityComparer = new HeapTypeEqualityComparer();
        class HeapTypeEqualityComparer : IEqualityComparer<TypeHandle>
        {
            public bool Equals(TypeHandle x, TypeHandle y)
            {
                return (x.MethodTable == y.MethodTable) && (x.ComponentMethodTable == y.ComponentMethodTable);
            }
            public int GetHashCode(TypeHandle obj)
            {
                return ((int)obj.MethodTable + (int)obj.ComponentMethodTable) >> 3;
            }
        }
        #endregion
    }
}