using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Microsoft.Samples.Debugging.CorDebug.NativeApi;
using Microsoft.Samples.Debugging.CorMetadata.NativeApi;
using Address = System.UInt64;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.Runtime;

namespace ClrMemory
{
    /// <summary>
    /// CLRMemory represents a dump of the CLR's GC Memory.   
    /// </summary>
    public unsafe class ICorDebugGCHeap : ClrHeap
    {
        public ICorDebugGCHeap(ICorDebugProcess process)
        {
            int isRunning;
            process.IsRunning(out isRunning);
            if (isRunning != 0)
                throw new InvalidOperationException("The process must be stopped to dump the GC ");

            m_typeTable = new Dictionary<COR_TYPEID, ICorDebugGCHeapType>();
            m_types = new List<ICorDebugGCHeapType>();

            // Type index 0 is reserverd for the 'Bad Type' 
            var badType = new ICorDebugGCHeapType(this, "!BAD_TYPE!", "");
            badType.m_size = 4;     // We use the bad type as a way of filling holes in the heap, 

            // Setting these fields marks this as the 'live heap' case.  
            // GCHeapSegment is 'smart' and only fetches the information it 
            // needs from the big blob of data in the segement.   
            m_process = process;
            m_process5 = process as ICorDebugProcess5;
            if (m_process5 == null)
                throw new Exception("The process is not running V4.5 of the .NET Framework (or V5.0 of silverlight), can't dump the GC Heap.");  

            COR_HEAPINFO heapInfo;
            m_process5.GetGCHeapInformation(out heapInfo);

            if (heapInfo.areGCStructuresValid == 0)
                throw new Exception("The process is at a point where the GC structures are being updated.  A heap dump is not possible at this time.");

            m_pointerSize = (int)heapInfo.pointerSize;
            Debug.Assert(PointerSize == 4 || PointerSize == 8);

            // Create the segments (but this leaves the data in the segments 
            // alone)
            var segmentList = new List<ICorDebugGCHeapSegment>();
            ICorDebugHeapSegmentEnum regionEnum;
            m_process5.EnumerateHeapRegions(out regionEnum);
            uint fetched;
            COR_SEGMENT[] corSegment = new COR_SEGMENT[1];
            for (; ; )
            {
                regionEnum.Next(1, corSegment, out fetched);
                if (fetched == 0)
                    break;
                segmentList.Add(new ICorDebugGCHeapSegment(this, ref corSegment[0]));
            }
            m_icorDebugSegments = segmentList.ToArray();

            // Create the segments.  
            UpdateSegments(m_icorDebugSegments);

            // This is used in FetchIntPtrAt
            m_data = new byte[1024];
            m_pinningHandle = GCHandle.Alloc(m_data, GCHandleType.Pinned);
            fixed (byte* ptr = m_data)
                m_dataPtr = ptr;
        }
        public override ClrType GetObjectType(Address objRef)
        {
            if (m_process5 != null)
            {
                COR_TYPEID typeID;
                m_process5.GetTypeID(objRef, out typeID);
                return GetObjectTypeFromID(typeID);
            }
            else
            {
                Debug.Assert(IsInHeap(objRef));
                var typeIndex = (int)FetchIntPtrAt(objRef, 0) - ICorDebugGCHeapType.TypeIndexStart;
                if ((uint)typeIndex < (uint)m_types.Count)
                    return m_types[typeIndex];

                // Return a bad type
                Debug.WriteLine(string.Format("Error: object ref 0x{0:x} does not point at the begining of an object.", objRef));
                Debug.Assert(m_types[0].Name == "!BAD_TYPE!");
                return m_types[0];
            }
        }
        
        public override IEnumerable<ClrRoot> EnumerateRoots() { return EnumerateRoots(false); }
        public override IEnumerable<ClrRoot> EnumerateRoots(bool enumStatics) { if (m_roots == null) InitRoots(); return m_roots; }

        #region private
        private static ICorDebugGCHeapType GetTypeFromNames(Dictionary<string, ICorDebugGCHeapType> types, string className, string moduleFilePath, ICorDebugGCHeap heap)
        {
            ICorDebugGCHeapType ret;
            if (types.TryGetValue(className, out ret))
                return ret;
            ret = new ICorDebugGCHeapType(heap, className, moduleFilePath);
            types.Add(className, ret);
            return ret;
        }

        /// <summary>
        /// Can only be used when we have a live heap.  returns the type type given a ICorDebug COR_TYPEID token
        /// </summary>
        internal ICorDebugGCHeapType GetObjectTypeFromID(COR_TYPEID typeID)
        {
            Debug.Assert(m_process5 != null);           // only used when we have a live heap

            ICorDebugGCHeapType ret;
            if (m_typeTable.TryGetValue(typeID, out ret))
                return ret;

            ret = new ICorDebugGCHeapType(this, typeID);
            return ret;
        }

        internal ulong FetchIntPtrAt(Address address, int offset)
        {
            Debug.Assert(offset >= 0);
            address += (uint) offset;

            TRY_AGAIN:
            // The fast path.  
            long delta = (long) (address - m_dataStart);
            if (0 <= delta && delta < m_dataLength)
            {
                if (PointerSize == 4)
                    return *((uint*) (m_dataPtr + (int) delta));
                else 
                    return *((ulong*) (m_dataPtr + (int) delta));
            }

            IntPtr readSizeIntPtr = IntPtr.Zero;
            m_dataStart = address;
            m_process.ReadMemory(m_dataStart, (uint) m_data.Length, m_data, out readSizeIntPtr);
            m_dataLength = (int) readSizeIntPtr - 8;        // Allows an intPtr size read (under all circumstances.  
            Debug.Assert(m_dataLength >= 0);
            if (m_dataLength >= 0)
                goto TRY_AGAIN;

            throw new InvalidOperationException("Illegal fetch at " + address.ToString("x"));
        }
        
        byte[] m_data;
        Address m_dataStart;
        int m_dataLength;           // This allows an IntPtr size read 
        byte* m_dataPtr;
        GCHandle m_pinningHandle;
        protected ClrSegment[] m_segments;
        ulong[] m_sizeByGen = new Address[4];
        ulong m_totalHeapSize;
        protected int m_lastSegmentIdx;       // The last segment we looked at. 
        ulong m_minAddr, m_maxAddr;
        int m_pointerSize;

        /// <summary>
        /// Sadly this is a bit subtle.  We only want to scan the GC heap once, the read of
        /// the heap only happens when we write during serialization of the heap data.  However
        /// until we do this we don't have the types, and we need the types to initialize the
        /// roots.   Thus we have to pospone the creation of the roots as long as possible 
        /// (after serialization of the GC data), so it all works out.  
        /// </summary>
        private void InitRoots()
        {
            // We should only be calling this when the data is coming from a live process. 
            Debug.Assert(m_process5 != null);
            // TODO FIX NOW REMOVE Console.WriteLines Console.WriteLine("Initializing roots");

            // We need to look up the types by name create a temporary dication to do this.
            // TODO FIX NOW we can have collisions (it happens with generics right now). 
            var types = new Dictionary<string, ICorDebugGCHeapType>();
            foreach (var type in m_types)
                types[type.Name] = type;

            var roots = new List<ICorDebugGCHeapRoot>();
            ICorDebugGCReferenceEnum refEnum;
            m_process5.EnumerateGCReferences(0, out refEnum);
            uint fetched;
            COR_GC_REFERENCE[] corRoots = new COR_GC_REFERENCE[256];
            StringBuilder buffer = new StringBuilder(1024);
            for (; ; )
            {
                refEnum.Next(256, corRoots, out fetched);
                if (fetched == 0)
                    break;
                for (int i = 0; i < fetched; i++)
                    roots.Add(new ICorDebugGCHeapRoot(ref corRoots[i], this, buffer));
            }
            m_roots = roots.ToArray();
            //Console.WriteLine("Root count = {0}", m_roots.Length);
        }

        // Only used when we have a live heap
        internal ICorDebugProcess5 m_process5;
        internal ICorDebugProcess m_process;
        internal Dictionary<COR_TYPEID, ICorDebugGCHeapType> m_typeTable;
        ICorDebugGCHeapRoot[] m_roots;

        internal List<ICorDebugGCHeapType> m_types;
        internal ICorDebugGCHeapSegment[] m_icorDebugSegments;  // This alwasy points at m_segments, but has the stronger type for the array. 

        // Heap enumeration fields
        #region HeapEnumeration
        ICorDebugHeapEnum m_heapEnum;
        COR_HEAPOBJECT[] m_heapObjs;
        uint m_heapObjsLimit;
        uint m_heapObjsCur;

        internal Address GetCurObject(out ICorDebugGCHeapType objType)
        {
            if (m_heapEnum == null)
            {
                m_heapObjs = new COR_HEAPOBJECT[8192];             // TODO decide on a good number
                m_process5.EnumerateHeap(out m_heapEnum);
                Debug.Assert(m_heapObjsCur == m_heapObjsLimit);    // Both should be zero.  
            }

            if (m_heapObjsCur >= m_heapObjsLimit)
            {
                m_heapObjsCur = 0;
                m_heapEnum.Next((uint)m_heapObjs.Length, m_heapObjs, out m_heapObjsLimit);
                if (m_heapObjsLimit == 0)
                {
                    objType = null;
                    return Address.MaxValue;
                }
            }
            objType = GetObjectTypeFromID(m_heapObjs[m_heapObjsCur].type);
            return m_heapObjs[m_heapObjsCur].address;
        }
        internal void GetNextObject()
        {
            m_heapObjsCur++;
        }
        #endregion
        #endregion

        public override bool CanWalkHeap
        {
            get
            {
                COR_HEAPINFO info;
                m_process5.GetGCHeapInformation(out info);
                return info.areGCStructuresValid != 0;
            }
        }

        void UpdateSegments(ClrSegment[] segments)
        {
            // sort the segments.  
            Array.Sort(segments, delegate(ClrSegment x, ClrSegment y) { return x.Start.CompareTo(y.Start); });
            m_segments = segments;

            m_minAddr = Address.MaxValue;
            m_maxAddr = Address.MinValue;
            m_totalHeapSize = 0;
            m_sizeByGen = new ulong[4];
            foreach (var gcSegment in m_segments)
            {
                if (gcSegment.Start < m_minAddr)
                    m_minAddr = gcSegment.Start;
                if (m_maxAddr < gcSegment.End)
                    m_maxAddr = gcSegment.End;

                m_totalHeapSize += gcSegment.Length;
                if (gcSegment.IsLarge)
                    m_sizeByGen[3] += gcSegment.Length;
                else
                {
                    m_sizeByGen[2] += gcSegment.Gen2Length;
                    m_sizeByGen[1] += gcSegment.Gen1Length;
                    m_sizeByGen[0] += gcSegment.Gen0Length;
                }
            }
        }

        public override ClrSegment GetSegmentByAddress(Address objRef)
        {
            if (m_minAddr <= objRef && objRef < m_maxAddr)
            {
                // Start the segment search where you where last
                int curIdx = m_lastSegmentIdx;
                for (; ; )
                {
                    // TODO FIX NOW review this 
                    var segment = m_segments[curIdx];
                    var offsetInSegment = (long)(objRef - segment.Start);
                    if (0 <= offsetInSegment)
                    {
                        var intOffsetInSegment = (long)offsetInSegment;
                        if (intOffsetInSegment < (long)segment.Length)
                        {
                            m_lastSegmentIdx = curIdx;
                            return segment;
                        }
                    }

                    // Get the next segment loop until you come back to where you started.  
                    curIdx++;
                    if (curIdx >= Segments.Count)
                        curIdx = 0;
                    if (curIdx == m_lastSegmentIdx)
                        break;
                }
            }
            return null;
        }

        public override Address TotalHeapSize
        {
            get { return m_totalHeapSize; }
        }

        public override Address GetSizeByGen(int gen)
        {
            Debug.Assert(gen >= 0 && gen < 4);
            return m_sizeByGen[gen];
        }

        public override int PointerSize
        {
            get { return m_pointerSize; }
        }

        public override IList<ClrSegment> Segments
        {
            get { return m_segments; }
        }

        public override ClrRuntime Runtime
        {
            get
            {
                throw new NotImplementedException();
            }
        }

#if !ENUMERATE_SERIALIZED_EXCEPTIONS_ENABLED     // TODO remove when CLRMD has been updated. 
        [Obsolete]
        public override int TypeIndexLimit
        {
            get
            {
                throw new NotImplementedException();
            }
        }
#endif
        public override IEnumerable<Address> EnumerateObjectAddresses()
        {
            throw new NotImplementedException();
        }

        public override ClrType GetTypeByName(string name)
        {
            throw new NotImplementedException();
        }

        public override bool ReadPointer(Address addr, out Address value)
        {
            throw new NotImplementedException();
        }

        public override bool TryGetMethodTable(ulong obj, out ulong typeHandle, out ulong componentTypeHandle)
        {
            throw new NotImplementedException();
        }

        public override ulong GetMethodTable(ulong obj)
        {
            throw new NotImplementedException();
        }

        public override ClrType GetTypeByMethodTable(ulong typeHandle, ulong componentTypeHandle)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// The heap is broken up into a set of contiguous blocks called GCHeapSegments
    /// </summary>
    public class ICorDebugGCHeapSegment : ClrSegment
    {
        public override Address Start { get { return m_start; } }
        public override Address End { get { return m_end; } }
        public override ClrHeap Heap { get { return m_heap; } }
        public override int ProcessorAffinity { get { return m_heapNum; } }

        public override IEnumerable<Address> EnumerateObjectAddresses()
        {
            throw new NotImplementedException();
        }
        #region private
        internal ICorDebugGCHeapSegment(ICorDebugGCHeap heap, ref COR_SEGMENT corSegment)
        {
            Debug.Assert(heap.m_process != null);       // Only call this on live heaps.  

            m_heap = heap;
            m_start = corSegment.start;
            m_end = corSegment.end;
            m_heapNum = (int)corSegment.heap;
        }

        Address m_start;
        Address m_end;
        ICorDebugGCHeap m_heap;
        int m_heapNum;
        #endregion
    }

    public class ICorDebugAD : ClrAppDomain
    {
        string m_name;

        public ICorDebugAD(string name)
        {
            m_name = name;
        }

        public override Address Address
        {
            get { throw new NotImplementedException(); }
        }

        public override int Id
        {
            get { throw new NotImplementedException(); }
        }

        public override string Name
        {
            get { return m_name; }
        }
        

        public override string ConfigurationFile
        {
            get { throw new NotImplementedException(); }
        }

        public override IList<ClrModule> Modules
        {
            get { throw new NotImplementedException(); }
        }

        public override string ApplicationBase
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override ClrRuntime Runtime
        {
            get
            {
                throw new NotImplementedException();
            }
        }
    }

    public class ICorDebugGCHeapRoot : ClrRoot
    {
        public override string Name { get { return m_name; } }
        public override ClrType Type { get { return m_type; } }
        public override ClrAppDomain AppDomain { get { return m_appDomain; } }
        public override GCRootKind Kind { get { return m_kind; } }
        public override Address Object { get { return m_heapReference; } }
        public override Address Address { get { return m_addressOfRoot; } }

        #region private
        internal ICorDebugGCHeapRoot() { } // For deserialization
        internal ICorDebugGCHeapRoot(string name, GCRootKind kind, Address heapReference, Address addressOfRoot, ICorDebugGCHeapType type, string appDomainName)
        {
            m_kind = kind;
            m_name = name;
            m_type = type;
            m_heapReference = heapReference;
            m_addressOfRoot = addressOfRoot;
            m_appDomain = new ICorDebugAD(appDomainName);
        }
        internal ICorDebugGCHeapRoot(ref COR_GC_REFERENCE root, ICorDebugGCHeap heap, StringBuilder buffer)
        {
            Address address;
            Address objRef = 0;
            root.Location.GetAddress(out address);
            m_addressOfRoot = address;
            
            string adName = "";

            if (root.Domain != null)
            {
                uint nameSize;
                root.Domain.GetName((uint)buffer.Capacity, out nameSize, buffer);
                adName = buffer.ToString();
            }

            m_appDomain = new ICorDebugAD(adName);

            var asRef = root.Location as ICorDebugReferenceValue;
            if (asRef != null)
            {
                asRef.GetValue(out objRef);
                m_heapReference = objRef;
            }
            else if (root.Location != null)
            {
                root.Location.GetAddress(out objRef);
                m_heapReference = objRef;
            }
            else
                Console.WriteLine("ERROR! could not fetch value from root 0x{0:x}", address);

            Debug.Assert(Object == 0 || heap.IsInHeap(Object));

            m_type = null;
            if (root.Type == CorGCReferenceType.CorHandleStrong)
            {
                m_kind = GCRootKind.Strong;
                m_name = "Strong Handle";
            }
            else if (root.Type == CorGCReferenceType.CorHandleStrongPinning)
            {
                m_kind = GCRootKind.Pinning;
                m_name = "Pinning Handle";
            }
            else if (root.Type == CorGCReferenceType.CorHandleWeakShort)
            {
                m_kind = GCRootKind.Weak;
                m_name = "Weak Handle";
            }
            else if (root.Type == CorGCReferenceType.CorReferenceStack)
            {
                m_kind = GCRootKind.LocalVar;
                m_name = "Local Variable";
            }
            else if (root.Type == CorGCReferenceType.CorReferenceFinalizer)
            {
                m_kind = GCRootKind.Finalizer;
                m_name = "Finalization";
            }
            else if (root.Type == CorGCReferenceType.CorHandleStrongAsyncPinned)
            {
                m_kind = GCRootKind.AsyncPinning;
                m_name = "Async Pinning";
            }
            else
            {
                m_kind = GCRootKind.Strong;      // TODO FIX NOW complete the enumeration. 
                m_name = "Other Handle";
            }
        }

        private GCRootKind m_kind;
        private string m_name;
        private ICorDebugGCHeapType m_type;
        private Address m_heapReference;
        private Address m_addressOfRoot;
        private ClrAppDomain m_appDomain;
        #endregion
    }

    public class ICorDebugClrModule : ClrModule
    {
        private string m_filename;
        public ICorDebugClrModule(string fn)
        {
            m_filename = fn;
        }

        public override ClrRuntime Runtime
        {
            get { throw new NotImplementedException(); }
        }

        public override PdbInfo Pdb
        {
            get { throw new NotImplementedException(); }
        }

        public override string AssemblyName
        {
            get { throw new NotImplementedException(); }
        }

        public override IEnumerable<ClrType> EnumerateTypes()
        {
            throw new NotImplementedException();
        }

        public override string FileName
        {
            get { return m_filename; }
        }

        public override Address ImageBase
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsFile
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsDynamic
        {
            get { throw new NotImplementedException(); }
        }

        public override string Name
        {
            get { throw new NotImplementedException(); }
        }

        public override Address Size
        {
            get { throw new NotImplementedException(); }
        }

        public override Address MetadataAddress
        {
            get { throw new NotImplementedException(); }
        }

        public override object MetadataImport
        {
            get { throw new NotImplementedException(); }
        }

        public override Address MetadataLength
        {
            get { throw new NotImplementedException(); }
        }

        public override Address AssemblyId
        {
            get { throw new NotImplementedException(); }
        }

        public override DebuggableAttribute.DebuggingModes DebuggingMode
        {
            get { throw new NotImplementedException(); }
        }

        public override IList<ClrAppDomain> AppDomains
        {
            get
            {
                throw new NotImplementedException();
            }
        }

        public override ClrType GetTypeByName(string name)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Represents a type of an object on the GC heap.   
    /// </summary>
    public class ICorDebugGCHeapType : ClrType
    {
        public override string Name { get { return m_name; } }
        public override ulong GetSize(Address objRef)
        {
            Debug.Assert(m_heap.GetObjectType(objRef) == this);         // You should be calling only on appropriate objRefs
            int size = m_size;
            if (IsArray)
            {
                var arrayLength = (int)m_heap.FetchIntPtrAt(objRef, m_array.countOffset);
                size += arrayLength * m_array.elementSize;
                int roundMask = m_heap.PointerSize - 1;
                size = (size + roundMask) & ~roundMask;     // Round up to a pointer size 
            }

            Debug.Assert(size >= 12 && size % m_heap.PointerSize == 0);
            return (ulong)size;
        }
        public override void EnumerateRefsOfObject(Address objRef, Action<Address, int> action)
        {
            if (IsArray)
            {
                bool isRefType = GCRootNames.IsReferenceType(m_array.componentType);
                if (isRefType || m_array.componentType == CorElementType.ELEMENT_TYPE_VALUETYPE)
                {
                    var arrayLength = (int)m_heap.FetchIntPtrAt(objRef, m_array.countOffset);
                    int offset = m_array.firstElementOffset;
                    if (isRefType)
                    {
                        for (int i = 0; i < arrayLength; i++)
                        {
                            var val = m_heap.FetchIntPtrAt(objRef, offset);
                            if (val != 0)
                                action(val, offset);
                            offset += m_array.elementSize;
                        }
                    }
                    else
                    {
                        Debug.Assert(m_array.componentType == CorElementType.ELEMENT_TYPE_VALUETYPE);
                        for (int i = 0; i < arrayLength; i++)
                        {
                            m_elementType.EnumerateRefsOfUnboxedClass(objRef, offset, action);
                            offset += m_array.elementSize;
                        }
                    }
                }
            }
            else
            {
                // Do the base type's references
                if (BaseType != null)
                    BaseType.EnumerateRefsOfObjectCarefully(objRef, action);
                // And then my fields.  
                this.EnumerateRefsOfUnboxedClass(objRef, m_boxOffset, action);
            }
        }


        public override void EnumerateRefsOfObjectCarefully(Address objRef, Action<Address, int> action)
        {
            EnumerateRefsOfObject(objRef, action);
        }

        public override ClrHeap Heap { get { return m_heap; } }
        public override ClrModule Module { get { return new ICorDebugClrModule(m_moduleFilePath); } }

        // Fetching values.  
        public override bool HasSimpleValue { get { return m_typeKind < CorElementType.ELEMENT_TYPE_PTR; } }
        public override object GetValue(Address address)
        {
            Debug.Assert(HasSimpleValue);
            var val = m_heap.FetchIntPtrAt(address, 0);

            return InnerGetValue(val, m_typeKind);
        }

        private object InnerGetValue(Address val, CorElementType typeKind)
        {
            switch (typeKind)
            {
                case CorElementType.ELEMENT_TYPE_BOOLEAN:
                    return (val & 0xFF) != 0;
                case CorElementType.ELEMENT_TYPE_CHAR:
                    return (char)val;
                case CorElementType.ELEMENT_TYPE_I1:
                    return (sbyte)val;
                case CorElementType.ELEMENT_TYPE_U1:
                    return (byte)val;
                case CorElementType.ELEMENT_TYPE_I2:
                    return (short)val;
                case CorElementType.ELEMENT_TYPE_U2:
                    return (ushort)val;
                case CorElementType.ELEMENT_TYPE_I4:
                    return (int)val;
                case CorElementType.ELEMENT_TYPE_U4:
                    return (uint)val;
                case CorElementType.ELEMENT_TYPE_I:
                    return (IntPtr)val;
                case CorElementType.ELEMENT_TYPE_U:
                    return (UIntPtr)val;
                case CorElementType.ELEMENT_TYPE_STRING:
                    {
                        // TODO FIX NOW
                        throw new NotImplementedException();
                    }
                case CorElementType.ELEMENT_TYPE_CLASS:
                    // TODO Hack
                    if (Name == "System.String")
                        goto case CorElementType.ELEMENT_TYPE_STRING;
                    break;

                case CorElementType.ELEMENT_TYPE_I8:
                case CorElementType.ELEMENT_TYPE_U8:
                case CorElementType.ELEMENT_TYPE_R4:
                case CorElementType.ELEMENT_TYPE_R8:

                // TODO FIX NOW NOT DONE 
                default:
                    break;
            }
            return null;
        }

        // These are only valid for Array Types
        public override bool IsArray { get { return m_isArray; } }
        public override ClrType ComponentType { get { return m_elementType; } }
        public override int GetArrayLength(Address objRef)
        {
            Debug.Assert(m_heap.GetObjectType(objRef) == this);
            Debug.Assert(IsArray);

            return (int)m_heap.FetchIntPtrAt(objRef, m_array.countOffset);
        }
        public override Address GetArrayElementAddress(Address objRef, int index)
        {
            Debug.Assert(m_heap.GetObjectType(objRef) == this);
            Debug.Assert(IsArray);
            Debug.Assert(0 <= index && index < GetArrayLength(objRef));

            return objRef + (uint)((index * m_array.elementSize) + m_array.firstElementOffset);
        }

        public override object GetArrayElementValue(Address objRef, int index)
        {
            Debug.Assert(m_heap.GetObjectType(objRef) == this);
            Debug.Assert(IsArray);
            Debug.Assert(0 <= index && index < GetArrayLength(objRef));
            
            //var addr = 
            ulong address = GetArrayElementAddress(objRef, index);
            var val = m_heap.FetchIntPtrAt(address, 0);

            CorElementType elemType = (CorElementType)ClrElementType.Unknown;
            if (ComponentType != null)
                elemType = (CorElementType)ComponentType.ElementType;

            return InnerGetValue(val, elemType);
        }


        // These are for types with fields. 
        public override bool GetFieldForOffset(int fieldOffset, bool inner, out ClrInstanceField childField, out int childFieldOffset)
        {
            if (m_fields != null)
            {
                for (int i = 1; ; i++)
                {
                    // You have to go one past the field you want.  
                    if (i >= m_fields.Length || fieldOffset < m_fields[i].Offset)
                    {
                        childField = m_fields[i - 1];
                        childFieldOffset = fieldOffset - childField.Offset;
                        return true;
                    }
                }
            }
            childField = null;
            childFieldOffset = 0;
            return false;
        }
        public override IList<ClrInstanceField> Fields { get { if (m_fields == null) m_fields = new ICorDebugGCHeapField[0]; return m_fields; } }
        public override ClrType BaseType { get { return m_baseType; } }

        public override ClrInstanceField GetFieldByName(string name)
        {
            foreach (var field in Fields)
                if (field.Name == name)
                    return field;

            return null;
        }

        #region private
        internal const int TypeIndexStart = unchecked((int)0xFF000000);         // We displace the type index by this quantity to make them more recognisable in dumps (better asserts)

        internal ICorDebugGCHeapType() { } // Used for deserialization
        internal ICorDebugGCHeapType(ICorDebugGCHeap heap, COR_TYPEID typeID)
        {
            // Console.WriteLine("Creating type for typeId {0:x} {1:x}", typeID.token1, typeID.token2);
            m_heap = heap;
            m_index = heap.m_types.Count;
            m_name = "";
            m_moduleFilePath = "";

            heap.m_typeTable[typeID] = this;
            heap.m_types.Add(this);

            COR_TYPE_LAYOUT header = new COR_TYPE_LAYOUT();
            // Console.WriteLine("Calling GetTypeLayout for typeId {0:x} {1:x}", typeID.token1, typeID.token2);
            heap.m_process5.GetTypeLayout(typeID, out header);
            m_typeKind = header.type;
            m_boxOffset = header.boxOffset;
            m_size = header.objectSize;

            // Strings are considered arrays.  
            m_isArray = (header.type == CorElementType.ELEMENT_TYPE_ARRAY || header.type == CorElementType.ELEMENT_TYPE_SZARRAY || header.type == CorElementType.ELEMENT_TYPE_STRING);
            if (m_isArray)
            {
                // Console.WriteLine("Calling GetArrayLayout for typeId {0:x} {1:x}", typeID.token1, typeID.token2);
                heap.m_process5.GetArrayLayout(typeID, out m_array);
                m_elementType = heap.GetObjectTypeFromID(m_array.componentID);

                m_moduleFilePath = ComponentType.Module.FileName;
                if (m_typeKind == CorElementType.ELEMENT_TYPE_SZARRAY)
                    m_name = ComponentType.Name + "[]";
                else if (m_typeKind == CorElementType.ELEMENT_TYPE_ARRAY)
                {
                    if (m_array.numRanks == 1)
                        m_name = ComponentType.Name + "[*]";
                    else
                        m_name = ComponentType.Name + "[" + new string(',', m_array.numRanks - 1) + "]";

                    Debug.Assert(m_array.firstElementOffset > m_array.rankOffset);
                }
                else if (m_typeKind == CorElementType.ELEMENT_TYPE_STRING)
                    m_name = "System.String";
                Debug.Assert(m_array.firstElementOffset > 0);
            }
            else
            {
                if (header.parentID.token1 != 0 || header.parentID.token2 != 0) // If we have a parent get it.  
                    m_baseType = heap.GetObjectTypeFromID(header.parentID);

                SetNameModuleAndFields(m_typeKind, typeID, header.numFields);
#if DEBUG
                if (m_fields != null)
                {
                    foreach (var field in m_fields)
                        Debug.Assert(field != null);
                }
#endif
            }
        }
        internal ICorDebugGCHeapType(ICorDebugGCHeap heap, string typeName, string moduleFilePath)
        {
            m_heap = heap;
            m_index = heap.m_types.Count;
            heap.m_types.Add(this);
            m_name = typeName;
            m_moduleFilePath = moduleFilePath;
            m_typeKind = CorElementType.ELEMENT_TYPE_CLASS;
            m_isArray = false;
        }

        /// <summary>
        /// Returns an enumeration that indicates the kind of primitive type you are.  
        /// </summary>
        public CorElementType TypeKind { get; private set; }

        private void SetNameModuleAndFields(CorElementType typeKind, COR_TYPEID typeID, int numFields)
        {
            // THere is recursion in the definition of primitive types (they have a value field of the primtitive type.
            // Cut this off here.  
            if (GCRootNames.IsPrimitiveType(typeKind))
                numFields = 0;

            var buffer = new StringBuilder(1024);
            IMetadataImport metaData = null;
            int bufferSizeRet;

            // This is getting names.   If we fail, we can still plow on ....
            try
            {
                ICorDebugType corType = null;
                // Console.WriteLine("Calling GetTypeForTypeID {0:x} {1:x}", typeID.token1, typeID.token2);
                m_heap.m_process5.GetTypeForTypeID(typeID, out corType);

                string moduleFilePath;
                m_name = GCRootNames.GetTypeName(corType, out moduleFilePath, out metaData, buffer);
                m_moduleFilePath = moduleFilePath;
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: Caught exception for type ID {0:x} {1:x}: {2}", typeID.token1, typeID.token2, e.Message);
                m_name = string.Format("!ERROR TYPE ID {0:x} {1:x}", typeID.token1, typeID.token2);
                m_moduleFilePath = Name;
            }

            if (numFields > 0)
            {
                m_fields = new ICorDebugGCHeapField[numFields];
                var corFields = new COR_FIELD[numFields];

                int fieldsFetched;
                m_heap.m_process5.GetTypeFields(typeID, corFields.Length, corFields, out fieldsFetched);
                Debug.Assert(fieldsFetched == m_fields.Length);

                for (int i = 0; i < corFields.Length; i++)
                {
                    int fieldTypeToken, fieldAttr, sigBlobSize, cplusTypeFlab, fieldValSize;
                    IntPtr sigBlob, fieldVal;
                    buffer.Length = 0;
                    if (metaData != null)
                        metaData.GetFieldProps(corFields[i].token, out fieldTypeToken, buffer, buffer.Capacity, out bufferSizeRet,
                            out fieldAttr, out sigBlob, out sigBlobSize, out cplusTypeFlab, out fieldVal, out fieldValSize);

                    var fieldName = buffer.ToString();
                    ICorDebugGCHeapType fieldType = null;
                    // If the type has never been loaded, then you can get a null field type.
                    // TODO FIX NOW, think about this. 
                    if (corFields[i].id.token1 != 0 || corFields[i].id.token2 != 0)
                    {
                        // Console.WriteLine("Looking up field {0}.{1} typeId {2:x} {3:x}", Name, fieldName, corFields[i].id.token1, corFields[i].id.token2);
                        Debug.Assert(corFields[i].fieldType != CorElementType.ELEMENT_TYPE_END);

                        // TODO FIX NOW remove the condition
                        if (!GCRootNames.IsReferenceType(corFields[i].fieldType))
                            fieldType = m_heap.GetObjectTypeFromID(corFields[i].id);
                    }
                    else
                    {
                        // Console.WriteLine("Warning, NULL type token for {0}.{1} assuming it is an objectRef", Name, fieldName);
                        // Zero means the type is not loaded.   This can only happen if it is a reference type
                        corFields[i].fieldType = CorElementType.ELEMENT_TYPE_CLASS;
                    }
                    // The element types match. (string matches class)
#if DEBUG
                    if (fieldType != null)
                    {
                        var fieldTypeKind = fieldType.TypeKind;
                        if (fieldTypeKind == CorElementType.ELEMENT_TYPE_STRING)
                            fieldTypeKind = CorElementType.ELEMENT_TYPE_CLASS;
                        if (fieldTypeKind == CorElementType.ELEMENT_TYPE_OBJECT)
                            fieldTypeKind = CorElementType.ELEMENT_TYPE_CLASS;
                        Debug.Assert(fieldTypeKind == corFields[i].fieldType);
                    }
#endif
                    m_fields[i] = new ICorDebugGCHeapField(fieldName, corFields[i].offset, fieldType, corFields[i].fieldType);
                }
            }
        }

        private void EnumerateRefsOfUnboxedClass(Address objref, int offsetInObject, Action<ulong, int> action)
        {
            if (m_fields == null)
                return;

            for (int i = 0; i < m_fields.Length; i++)
            {
                var field = m_fields[i];
                var offset = offsetInObject + field.Offset;

                if (GCRootNames.IsReferenceType(field.m_ComponentType))
                {
                    var val = m_heap.FetchIntPtrAt(objref, offset);
                    if (val != 0)
                        action(val, offset);
                }
                else if (field.m_ComponentType == CorElementType.ELEMENT_TYPE_VALUETYPE)
                {
                    // We should not have recursive VALUE TYPEs
                    Debug.Assert(field.m_type != this);

                    field.m_type.EnumerateRefsOfUnboxedClass(objref, offset, action);
                }
            }
        }

        internal int m_size;
        internal int m_boxOffset;
        internal COR_ARRAY_LAYOUT m_array;                    // Only valid if it is an array. 
        internal ICorDebugGCHeapField[] m_fields;             // Only valid if it is a class
        internal ICorDebugGCHeapType m_baseType;              // Only valid if it is a class
        private ICorDebugGCHeap m_heap;
        string m_name;
        string m_moduleFilePath;
        bool m_isArray;
        ICorDebugGCHeapType m_elementType;
        private CorElementType m_typeKind;
        int m_index;
        #endregion

        #region Unimplemented
        public override int ElementSize
        {
            get { throw new NotImplementedException(); }
        }

        public override int BaseSize
        {
            get { throw new NotImplementedException(); }
        }

        public override IList<ClrInterface> Interfaces
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsAbstract
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsFinalizable
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsInterface
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsInternal
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsPrivate
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsProtected
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsPublic
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsSealed
        {
            get { throw new NotImplementedException(); }
        }

        public override ClrStaticField GetStaticFieldByName(string name)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable<ulong> EnumerateMethodTables()
        {
            throw new NotImplementedException();
        }

        public override ulong MethodTable { get { throw new NotImplementedException(); } }

        public override uint MetadataToken
        {
            get { throw new NotImplementedException(); }
        }
        #endregion
    }

    public class ICorDebugGCHeapField : ClrInstanceField
    {
        public override string Name { get { return m_name; } }
        public override int Offset { get { return m_offset; } } 
        public override ClrType Type { get { return m_type; } }
        public override Address GetAddress(Address objRef, bool interior)
        {
            /* TODO FIX NOW - A field could be embedded in another field.  Imagine:
             * 
             * class Outer
             * {
             *    Inner _inner;
             * }
             * 
             * struct Inner
             * {
             *   object foo;
             * }
             * 
             * If GCHeapField represents "Inner.foo", then you need to access this differently
             * depending on whether Inner is a boxed struct.  Here you need to add the size
             * of the method table:
             *   object obj = new Inner();
             * 
             * Here you don't add the size of the method table:
             *   Outer o = new Outer();
             *   o._inner.foo;
             */
            return objRef + (uint)Offset;
        }

        public override object GetValue(Address objRef, bool interior)
        {
            return Type.GetValue(GetAddress(objRef, interior));
        }
        
        public override object GetValue(Address objRef, bool interior, bool convertStrings)
        {
            return Type.GetValue(GetAddress(objRef, interior));
        }

        #region private
        internal ICorDebugGCHeapField(string name, int offset, ICorDebugGCHeapType fieldType, CorElementType componentType)
        {
            m_offset = offset;
            m_name = name;
            m_type = fieldType;
            m_ComponentType = componentType;
        }

        internal CorElementType m_ComponentType;
        string m_name;
        int m_offset;
        internal ICorDebugGCHeapType m_type;

        #endregion

        #region Unimplemented
        public override int Size
        {
            get { throw new NotImplementedException(); }
        }

        public override bool HasSimpleValue
        {
            get { throw new NotImplementedException(); }
        }

        public override ClrElementType ElementType
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsInternal
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsPrivate
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsProtected
        {
            get { throw new NotImplementedException(); }
        }

        public override bool IsPublic
        {
            get { throw new NotImplementedException(); }
        }
        #endregion
    }
}

