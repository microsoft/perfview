using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using FastSerialization;
using Microsoft.Samples.Debugging.CorDebug.NativeApi;
using Microsoft.Samples.Debugging.CorMetadata.NativeApi;
using Address = System.UInt64;

namespace ClrMemory
{
    /// <summary>
    /// CLRMemory represents a dump of the CLR's GC Memory.   
    /// </summary>
    public class GCHeap : IFastSerializable, IFastSerializableVersion
    {
        /// <summary>
        /// Dump the GC heap associated with 'process' to 'fileName'
        /// </summary>
        public static void DumpHeap(ICorDebugProcess process, string fileName)
        {
            int isRunning;
            process.IsRunning(out isRunning);
            if (isRunning != 0)
                throw new InvalidOperationException("The process must be stopped to dump the GC heap.");

            var heap = new GCHeap();
            heap.m_typeTable = new Dictionary<COR_TYPEID, GCHeapType>();
            heap.m_types = new List<GCHeapType>();
            heap.TimeCollected = DateTime.Now;
            heap.MachineName = Environment.MachineName;


            // Setting these fields marks this as the 'live heap' case.  
            // GCHeapSegment is 'smart' and only fetches the information it 
            // needs from the big blob of data in the segement.   
            heap.m_process = process;
            heap.m_process5 = process as ICorDebugProcess5;
            if (heap.m_process5 == null)
                throw new Exception("The process is not running V4.5 of the .NET Framework, can't dump the heap.");  // TODO better message

            COR_HEAPINFO heapInfo;
            heap.m_process5.GetGCHeapInformation(out heapInfo);

            if (heapInfo.areGCStructuresValid == 0)
                throw new Exception("The process is at a point where the GC structures are being updated.  A heap dump is not possible at this time.");

            heap.PointerSize = (int)heapInfo.pointerSize;
            Debug.Assert(heap.PointerSize == 4 || heap.PointerSize == 8);

#if false
            // TODO: Experimental fetch all the static roots in the process. 
            Console.WriteLine("Experimental: Fetching all static variables that point into GC heap.");
            heap.EnumerateStaticRoots(process, delegate(string moduleName, string typeName, string fieldName, Address fieldValue)
            {
                var newRoot = new GCRoot(typeName + "." + fieldName, moduleName, fieldValue, 0); 
                Console.WriteLine("Field: {1}.{2} = 0x{3:x}", moduleName, typeName, fieldName, fieldValue);
            });
#endif
            // Create the segments (but this leaves the data in the segements 
            // alone
            var segmentList = new List<GCHeapSegment>();
            ICorDebugHeapSegmentEnum regionEnum;
            heap.m_process5.EnumerateHeapRegions(out regionEnum);

            uint fetched;
            COR_SEGMENT[] corSegment = new COR_SEGMENT[1];
            for (; ; )
            {
                regionEnum.Next(1, corSegment, out fetched);
                if (fetched == 0)
                    break;
                segmentList.Add(new GCHeapSegment(heap, ref corSegment[0]));
            }
            heap.m_segments = segmentList.ToArray();

            ICorDebugGCReferenceEnum refEnum;
            heap.m_process5.EnumerateGCReferences(0, out refEnum);
            COR_GC_REFERENCE[] corRoots = new COR_GC_REFERENCE[256];
            var roots = new List<GCRoot>();
            for (; ; )
            {
                refEnum.Next(256, corRoots, out fetched);
                if (fetched == 0)
                    break;
                for (int i = 0; i < fetched; i++)
                    roots.Add(new GCRoot(ref corRoots[i], heap));
            }
            heap.m_roots = roots.ToArray();
            Console.WriteLine("Root count = {0}", heap.m_roots.Length);

            // Actually write out the file.  This actualy forces
            // the fetching of the segment data in the GCHeapSegment.ToStream
            var serializer = new Serializer(fileName, heap);
            serializer.Close();

#if DEBUG
            // If this fires, it means that we have not properly modified every GC reference in the heap.  
            GCHeapType objType;
            Debug.Assert(heap.GetCurObject(out objType) == Address.MaxValue);
#endif
        }
        /// <summary>
        /// Make a GCHeap from a serialized heap (written with DumpHeap) in 'filePath'
        /// </summary>
        public GCHeap(string filePath)
        {
            var deserializer = new Deserializer(filePath);

            // Register that I should use 'this' as the storage
            deserializer.RegisterFactory(typeof(GCHeap), delegate { return this; });

            deserializer.RegisterFactory(typeof(GCHeapSegment), delegate() { return new GCHeapSegment(); });
            deserializer.RegisterFactory(typeof(GCRoot), delegate() { return new GCRoot(); });
            deserializer.RegisterFactory(typeof(GCHeapType), delegate() { return new GCHeapType(); });
            deserializer.RegisterFactory(typeof(GCHeapField), delegate() { return new GCHeapField(); });

            var entryObj = (GCHeap)deserializer.GetEntryObject();
            Debug.Assert(entryObj == this);
        }
        /// <summary>
        /// enumerates all the object references in the object 'objRef'.   It calls
        /// Calls 'action(childRef, childOffset)' on each object reference in 'objRef'.  'childOffset' is
        /// the offset in 'objRef' of the reference, and 'childRef' is the value of the GC reference
        /// in the object.   
        /// 
        /// The 'childOffset' can be converted to a name by using GCHeapType.GetFieldForOffset.  
        /// </summary>
        public void EnumerateRefsOfObject(Address objRef, GCHeapType objType, Action<Address, int> action)
        {
            if (objType.IsArray)
            {
                if (objType.m_array.componentType <= CorComponentType.CorComponentValueClass)
                {
                    var arrayLength = (int)FetchIntPtrAt(objRef, objType.m_array.countOffset);
                    int offset = objType.m_array.firstElementOffset;
                    if (objType.m_array.componentType == CorComponentType.CorComponentGCRef)
                    {
                        for (int i = 0; i < arrayLength; i++)
                        {
                            var val = FetchIntPtrAt(objRef, offset);
                            if (val != 0)
                                action(val, offset);
                            offset += objType.m_array.elementSize;
                        }
                    }
                    else
                    {
                        Debug.Assert(objType.m_array.componentType == CorComponentType.CorComponentValueClass);
                        for (int i = 0; i < arrayLength; i++)
                        {
                            EnumerateRefsOfUnboxedClass(objRef, offset, objType.ElementType, action);
                            offset += objType.m_array.elementSize;
                        }
                    }
                }
                else
                    Debug.Assert(objType.m_array.componentType == CorComponentType.CorComponentPrimitive);
            }
            else
                EnumerateRefsOfUnboxedClass(objRef, objType.m_boxOffset, objType, action);
        }
        /// <summary>
        /// Most operations on an object (including fetching its size), you do through the GCHeapType.   
        /// Thus getting an objects types is a key operation.  
        /// </summary>
        /// <param name="obj"></param>
        public GCHeapType GetObjectType(Address objRef)
        {
            if (m_process5 != null)
            {
                COR_TYPEID typeID;
                m_process5.GetTypeID(objRef, out typeID);
                return GetObjectTypeFromID(typeID);
            }
            else
            {
                var typeIndex = (int)FetchIntPtrAt(objRef, 0) - GCHeapType.TypeIndexStart;
                Debug.Assert(0 <= typeIndex && typeIndex < m_types.Count);
                return GetTypeByIndex((GCHeapTypeIndex)typeIndex);
            }
        }

        /// <summary>
        /// Returns true if 'objRef' points to a valid address in the GC heap 
        /// (may not be at the start of an object however)
        /// </summary>
        public bool IsInHeap(Address objRef)
        {
            return Seek(objRef) != null;
        }

        public DateTime TimeCollected { get; private set; }
        public string MachineName { get; private set; }
        /// <summary>
        /// Pointer size of on the machine (4 or 8 bytes).  
        /// </summary>
        public int PointerSize { get; private set; }
        /// <summary>
        /// This size includes potentially freed object not the 'empty' areas that may not
        /// hold valid object references.  
        /// </summary>
        public long TotalGCSize
        {
            get
            {
                long ret = 0;
                foreach (var segment in Segments)
                    ret += segment.Length;
                return ret;
            }
        }
        /// <summary>
        /// Returns the number of objects in the heap.  Note that some of the objects may not be live.  
        /// </summary>
        public int NumberOfObjects { get; internal set; }

        public IEnumerable<GCRoot> Roots { get { return m_roots; } }
        public IEnumerable<GCHeapType> Types { get { return m_types; } }
        public IEnumerable<GCHeapSegment> Segments { get { return m_segments; } }

        public GCHeapType GetTypeByIndex(GCHeapTypeIndex typeIndex) { return m_types[(int)typeIndex]; }
        /// <summary>
        /// every GCHeapType is given index so you can look up infomation about the type
        /// efficiently in an array.  TypeIndexLimit is the bound of that array (all indexes
        /// are strictly below this limit.  
        /// </summary>
        public GCHeapTypeIndex TypeIndexLimit { get { return (GCHeapTypeIndex)m_types.Count; } }

        public void ToXml(TextWriter writer)
        {
            writer.WriteLine("<GCHeap TotalGCSize=\"{0}\" TotalNumberOfObjects=\"{1}\"" +
                "MachineName=\"{2}\" TimeCollected=\"{3}\" PointerSize=\"{4}\" >",
                TotalGCSize, NumberOfObjects, MachineName, TimeCollected, PointerSize);

            if (m_segments != null)
            {
                writer.WriteLine(" <Segments Count=\"{0}\">", m_segments.Length);
                foreach (var segment in m_segments)
                    segment.ToXml(writer);
                writer.WriteLine(" </Segments>");
            }

            if (m_roots != null)
            {
                writer.WriteLine(" <Roots Count=\"{0}\">", m_roots.Length);
                foreach (var root in m_roots)
                    root.ToXml(writer);
                writer.WriteLine(" </Roots>");
            }

            writer.WriteLine(" <Types Count=\"{0}\">", m_types.Count);
            foreach (var type in m_types)
                type.ToXml(writer);
            writer.WriteLine(" </Types>");

            writer.Write("</GCHeap>");
        }
        public override string ToString()
        {
            TextWriter sw = new StringWriter();
            ToXml(sw);
            return sw.ToString();
        }

        #region private
#if DEBUG
        // FOR DEBUGGING 
        public string DumpAt(Address address, int len = 256)
        {
            StringWriter sw = new StringWriter();
            var reader = Seek(address - 16);
            byte[] bytesBefore = new byte[16];
            byte[] bytes = new byte[len];
            for (int i = 0; i < bytesBefore.Length; i++)
                bytesBefore[i] = reader.ReadByte();

            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = reader.ReadByte();
            DumpBytes(bytesBefore, sw, "");
            sw.WriteLine("********************************");
            DumpBytes(bytes, sw, "");
            return sw.ToString();
        }
        internal static void DumpBytes(byte[] bytes, TextWriter output, string indent)
        {
            int row = 0;
            while (row < bytes.Length)
            {
                output.Write(indent);
                output.Write("{0,4:x}:  ", row);
                for (int i = 0; i < 16; i++)
                {
                    if (i == 8)
                        output.Write("| ");
                    if (i + row < bytes.Length)
                        output.Write("{0,2:x} ", bytes[i + row]);
                    else
                        output.Write("   ");
                }
                output.Write("  ");
                for (int i = 0; i < 16; i++)
                {
                    if (i == 8)
                        output.Write(" ");
                    if (i + row >= bytes.Length)
                        break;
                    byte val = bytes[i + row];
                    if (32 <= val && val < 128)
                        output.Write((Char)val);
                    else
                        output.Write(".");
                }
                output.WriteLine();
                row += 16;
            }
        }
#endif // DEBUG

        private GCHeap() { }

        void EnumerateRefsOfUnboxedClass(Address objref, int offsetInObject, GCHeapType structType, Action<ulong, int> action)
        {
            if (structType.Fields == null)
                return;

            for (int i = 0; i < structType.m_fields.Length; i++)
            {
                var field = structType.m_fields[i];
                var offset = offsetInObject + field.Offset;

                if (field.m_ComponentType == CorComponentType.CorComponentGCRef)
                {
                    var val = FetchIntPtrAt(objref, offset);
                    if (val != 0)
                        action(val, offset);
                }
                else if (field.m_ComponentType == CorComponentType.CorComponentValueClass)
                {
                    // We should not have recursive VALUE TYPEs
                    Debug.Assert(field.Type != structType);

                    EnumerateRefsOfUnboxedClass(objref, offset, field.Type, action);
                }
            }
        }

        static bool IsReferenceType(CorElementType elementType)
        {
            switch (elementType)
            {
                case CorElementType.ELEMENT_TYPE_STRING:
                case CorElementType.ELEMENT_TYPE_OBJECT:
                case CorElementType.ELEMENT_TYPE_ARRAY:
                case CorElementType.ELEMENT_TYPE_SZARRAY:
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Find all static variable that point in the GC heap that have non-null values, and 
        /// call 'action' on them (given the full name of the variable and the address in 
        /// the heap it points at. 
        /// </summary>
        internal void EnumerateStaticRoots(ICorDebugProcess proc, Action<string, string, string, Address> action)
        {
            var staticAddresses = new SortedDictionary<long, string>();

            uint fetched;
            StringBuilder buffer = new StringBuilder(1024);
            char[] moduleNameBuffer = new Char[260];

            int bufferSizeRet;

            ICorDebugAppDomainEnum appDomainEnum;
            proc.EnumerateAppDomains(out appDomainEnum);
            var appDomains = new ICorDebugAppDomain[1];
            for (; ; )
            {
                appDomainEnum.Next(1, appDomains, out fetched);
                if (fetched == 0)
                    break;

                // TODO Remove 
                // appDomains[0].GetName((uint) buffer.Capacity, out fetched, buffer);
                // Console.WriteLine("Got Appdomain {0}", buffer.ToString());

                ICorDebugAssemblyEnum assemblyEnum;
                appDomains[0].EnumerateAssemblies(out assemblyEnum);
                var assemblies = new ICorDebugAssembly[1];
                for (; ; )
                {
                    assemblyEnum.Next(1, assemblies, out fetched);
                    if (fetched == 0)
                        break;

                    ICorDebugModuleEnum moduleEnum;
                    assemblies[0].EnumerateModules(out moduleEnum);
                    var modules = new ICorDebugModule[1];
                    for (; ; )
                    {
                        moduleEnum.Next(1, modules, out fetched);
                        if (fetched == 0)
                            break;

                        IMetadataImport metaData;
                        var guid = new Guid("FCE5EFA0-8BBA-4f8e-A036-8F2022B08466");
                        modules[0].GetMetaDataInterface(guid, out metaData);
                        string moduleName = null;

                        IntPtr typeEnum = IntPtr.Zero;
                        int typeToken;
                        for (; ; )
                        {
                            metaData.EnumTypeDefs(ref typeEnum, out typeToken, 1, out fetched);
                            if (fetched == 0)
                                break;

                            ICorDebugClass class_ = null;
                            string className = null;

                            IntPtr fieldEnum = IntPtr.Zero;
                            int fieldToken;
                            for (; ; )
                            {
                                metaData.EnumFields(ref fieldEnum, typeToken, out fieldToken, 1, out fetched);
                                if (fetched == 0)
                                    break;

                                int fieldTypeToken, fieldAttr, sigBlobSize, cplusTypeFlab, fieldLiteralValSize;
                                IntPtr sigBlob, fieldLiteralVal;
                                buffer.Length = 0;
                                metaData.GetFieldProps(fieldToken, out fieldTypeToken, null, 0, out bufferSizeRet,
                                    out fieldAttr, out sigBlob, out sigBlobSize, out cplusTypeFlab, out fieldLiteralVal, out fieldLiteralValSize);

                                if ((FieldAttributes.Static & (FieldAttributes)fieldAttr) == 0)
                                    continue;
                                if ((FieldAttributes.Literal & (FieldAttributes)fieldAttr) != 0)
                                    continue;

                                // TODO FIX NOW: figure out if you are a reference type, without fetching value 
                                if (class_ == null)
                                    modules[0].GetClassFromToken((uint)typeToken, out class_);

                                // TODO FIX NOW try-catch is ugly 
                                ICorDebugValue fieldValue;
                                try
                                {
                                    class_.GetStaticFieldValue((uint)fieldToken, null, out fieldValue);
                                }
                                catch (Exception e)
                                {
                                    TypeAttributes typeAttr;
                                    int extendsToken;
                                    int typeNameLen;
                                    metaData.GetTypeDefProps(typeToken, buffer, buffer.Capacity, out typeNameLen, out typeAttr, out extendsToken);
                                    className = buffer.ToString();

                                    metaData.GetFieldProps(fieldToken, out fieldTypeToken, buffer, buffer.Capacity, out bufferSizeRet,
                                        out fieldAttr, out sigBlob, out sigBlobSize, out cplusTypeFlab, out fieldLiteralVal, out fieldLiteralValSize);
                                    var fieldName1 = buffer.ToString();

                                    Debug.WriteLine("For field {0}.{1} Caught exception {1}", className, fieldName1, e.Message);
                                    continue;
                                }

                                CorElementType fieldElemType;
                                fieldValue.GetType(out fieldElemType);

                                if (!IsReferenceType(fieldElemType))
                                    continue;

                                Address fieldAddress;
                                fieldValue.GetAddress(out fieldAddress);

                                Address objRef = FetchLiveIntPtrAt(fieldAddress, 0);
                                if (objRef == 0)
                                    continue;

                                // Fetch the name 
                                metaData.GetFieldProps(fieldToken, out fieldTypeToken, buffer, buffer.Capacity, out bufferSizeRet,
                                    out fieldAttr, out sigBlob, out sigBlobSize, out cplusTypeFlab, out fieldLiteralVal, out fieldLiteralValSize);
                                var fieldName = buffer.ToString();

                                if (className == null)
                                {
                                    TypeAttributes typeAttr;
                                    int extendsToken;
                                    int typeNameLen;
                                    metaData.GetTypeDefProps(typeToken, buffer, buffer.Capacity, out typeNameLen, out typeAttr, out extendsToken);
                                    className = buffer.ToString();
                                }
                                if (moduleName == null)
                                {
                                    modules[0].GetName((uint)moduleNameBuffer.Length, out fetched, moduleNameBuffer);
                                    moduleName = new String(moduleNameBuffer, 0, (int)(fetched - 1)); // Remove trailing null
                                }

                                action(moduleName, className, fieldName, objRef);
                            }
                        }
                        metaData.CloseEnum(typeEnum);
                    }
                }
            }
        }

        /// <summary>
        /// Can only be used when we have a live heap.  returns the type type given a ICorDebug COR_TYPEID token
        /// </summary>
        internal GCHeapType GetObjectTypeFromID(COR_TYPEID typeID)
        {
            Debug.Assert(m_process5 != null);           // only used when we have a live heap

            GCHeapType ret;
            if (m_typeTable.TryGetValue(typeID, out ret))
                return ret;

            ret = new GCHeapType(this, typeID, (GCHeapTypeIndex)m_types.Count);
            return ret;
        }

        internal MemoryStreamReader Seek(Address address)
        {
            // Start the segment search where you where last
            int curIdx = m_lastSegmentIdx;
            for (; ; )
            {
                var segment = m_segments[curIdx];
                var offsetInSegment = (long)(address - segment.Start);
                if (0 <= offsetInSegment)
                {
                    var intOffsetInSegment = (uint)offsetInSegment;
                    if (intOffsetInSegment < (uint)segment.Length)
                    {
                        m_lastSegmentIdx = curIdx;
                        // TODO make a access that avoids the casts. 
                        segment.m_DataReader.Goto((StreamLabel)((int)segment.m_DataStart + intOffsetInSegment));
                        return segment.m_DataReader;
                    }
                }

                // Get the next segment loop until you come back to where you started.  
                curIdx++;
                if (curIdx >= m_segments.Length)
                    curIdx = 0;
                if (curIdx == m_lastSegmentIdx)
                    break;
            }
            return null;
        }

        internal ulong FetchIntPtrAt(Address address, int offset)
        {
            Debug.Assert(offset >= 0);
            Debug.Assert(m_process == null);       // We are not live

            var reader = Seek(address + (uint)offset);
            if (reader == null)
            {
                Console.WriteLine("Warning address {0:x} not in GC heap.", address + (uint)offset);
                return 0;
            }
            if (PointerSize == 4)
                return (uint)reader.ReadInt32();
            else
                return (ulong)reader.ReadInt64();
        }

        internal unsafe ulong FetchLiveIntPtrAt(Address address, int offset)
        {
            Debug.Assert(m_process != null);
            // TODO Inefficient however right now this is only used rarely so it is OK.   
            // may change if we read heaps live all the time.  
            byte[] buffer = new byte[8];
            fixed (byte* bufferPtr = buffer)
            {
                IntPtr readBytes;
                m_process.ReadMemory(address + (uint)offset, (uint)PointerSize, buffer, out readBytes);
                Debug.Assert((int)readBytes == PointerSize);

                // Console.WriteLine("Fetching {0:x} = {1:x}", address + (uint)offset, (uint)*((int*)bufferPtr));
                if (PointerSize == 4)
                    return (uint)*((int*)bufferPtr);
                else
                    return *((ulong*)bufferPtr);
            }
        }

        void IFastSerializable.ToStream(Serializer serializer)
        {
            serializer.Write(PointerSize);
            serializer.Write(TimeCollected.Ticks);
            serializer.Write(MachineName);

            // As part of serialiation, we collect the number of objects.  
            NumberOfObjects = 0;

            // Write out segments
            serializer.Write(m_segments.Length);
            for (int i = 0; i < m_segments.Length; i++)
                serializer.WritePrivate(m_segments[i]);

            // Write out roots
            serializer.Write(m_roots.Length);
            for (int i = 0; i < m_roots.Length; i++)
                serializer.WritePrivate(m_roots[i]);

            // Write out types
            serializer.Write(m_types.Count);
            foreach (var type in m_types)
                serializer.WritePrivate(type);

            serializer.Write(NumberOfObjects);
        }
        void IFastSerializable.FromStream(Deserializer deserializer)
        {
            PointerSize = deserializer.ReadInt();
            TimeCollected = new DateTime(deserializer.ReadInt64());
            MachineName = deserializer.ReadString();

            // Read in segments
            var len = deserializer.ReadInt();
            m_segments = new GCHeapSegment[len];
            for (int i = 0; i < len; i++)
                deserializer.Read(out m_segments[i]);

            // Read in roots
            len = deserializer.ReadInt();
            m_roots = new GCRoot[len];
            for (int i = 0; i < m_roots.Length; i++)
                deserializer.Read(out m_roots[i]);

            // Read in types
            len = deserializer.ReadInt();
            m_types = new List<GCHeapType>(len);
            for (int i = 0; i < len; i++)
            {
                GCHeapType type;
                deserializer.Read(out type);
                m_types.Add(type);
            }
            NumberOfObjects = deserializer.ReadInt();
        }
        // The version number for thse data, you should increment this any time you add data 
        // to GCHeap if you wish to not crash on old formats.   You also have to use 
        // Deserializer.VersionBeingRead to avoid reading new fields from old formats.
        public int Version { get { return 1; } }
        // This smallest version that this code will read.  Thus if you set it to Version
        // (as I have done here) you will not read any old formats.   This is easy but
        // a poor user experience 
        public int MinimumVersion { get { return Version; } }

        // Only used when we have a live heap
        internal ICorDebugProcess5 m_process5;
        internal ICorDebugProcess m_process;
        internal Dictionary<COR_TYPEID, GCHeapType> m_typeTable;

        // Not serialized, used in FetchIntPtrAt
        private int m_lastSegmentIdx;

        GCHeapSegment[] m_segments;
        internal List<GCHeapType> m_types;
        GCRoot[] m_roots;

        // Heap enumeration fields
        #region HeapEnumeration
        ICorDebugHeapEnum m_heapEnum;
        COR_HEAPOBJECT[] m_heapObjs;
        uint m_heapObjsLimit;
        uint m_heapObjsCur;

        internal Address GetCurObject(out GCHeapType objType)
        {
            if (m_heapEnum == null)
            {
                m_heapObjs = new COR_HEAPOBJECT[8192];             // TODO decide on a good number
                m_process5.EnumerateHeap(out m_heapEnum);
                Debug.Assert(m_heapObjsCur == m_heapObjsLimit);    // BOth should be zero.  
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
    }

    /// <summary>
    /// The heap is broken up into a set of contiguous blocks called GCHeapSegments
    /// </summary>
    public class GCHeapSegment : IFastSerializable
    {
        public Address Start { get; private set; }
        public Address End { get { return Start + (uint)Length; } }
        public int Length { get; private set; }
        /// <summary>
        /// Returns the number of objects in the segment Note that some of the objects may not be live.  
        /// </summary>
        public int NumberOfObjects { get; private set; }

        public int Generation { get; private set; }
        public GCHeap Heap { get; private set; }

        public void ToXml(TextWriter writer)
        {
            writer.WriteLine("  <GCSegment Start=\"0x{0:x}\" End=\"0x{1:x}\" Length=\"0x{2:x}\" Generation=\"{3}\" NumberOfObjects=\"{4}\"/>",
                Start, End, Length, Generation, NumberOfObjects);
        }
        public override string ToString()
        {
            TextWriter sw = new StringWriter();
            ToXml(sw);
            return sw.ToString();
        }
        #region private
        internal GCHeapSegment() { } // Used for deserialization
        internal GCHeapSegment(GCHeap heap, ref COR_SEGMENT corSegment)
        {
            Debug.Assert(heap.m_process != null);       // Only call this on live heaps.  

            // TODO: Complete member initialization
            this.Heap = heap;
            this.Start = corSegment.start;
            ulong len = corSegment.end - corSegment.start;
            if (len > int.MaxValue)
                throw new NotImplementedException();        // TODO can split it up into multiple segments, but we can give up for now. 
            this.Length = (int)len;
            this.Generation = (int)corSegment.type;
        }

        private unsafe void SerializeHeapDataToStream(Stream stream)
        {
            var bytesLeftInSegment = Length;

            GCHeapType objType;
            var heap = Heap;
            Address bufferBasePos = Start;

            byte[] buffer = new byte[65536];    // Must be a multiple of pointer size.  
            var chunkSize = buffer.Length;
            int numObjects = 0;

            while (bytesLeftInSegment > 0)
            {
                if (chunkSize > bytesLeftInSegment)
                    chunkSize = bytesLeftInSegment;

                // Read a chunk of heap
                IntPtr readSizeIntPtr;
                Heap.m_process.ReadMemory(bufferBasePos, (uint)chunkSize, buffer, out readSizeIntPtr);
                int readSize = (int)readSizeIntPtr;
                Console.WriteLine("Read Chunk at 0x{0:x} size 0x{1:x}", bufferBasePos, readSize);

                fixed (byte* bufferBase = buffer)
                {
                    // We wack the MethodTable pointer of each object to be a GCHeapTypeIndex before writing it out.  
                    // This also populates all the type information we will dump later.   
                    for (; ; )
                    {
                        Address objRef = heap.GetCurObject(out objType);
                        var delta = objRef - bufferBasePos;
                        Debug.Assert(0 <= delta || numObjects == 0);        // We should increase within a segment. 
                        if (delta < 0 || delta >= (ulong)readSize)
                            break;

                        int idx = (int)(objRef - bufferBasePos);
                        numObjects++;
                        Console.WriteLine("Obj at 0x{0:x}  indexInChunk 0x{1:x}", objRef, idx);
                        Debug.Assert(idx % heap.PointerSize == 0);              // GC heap is kept aligned to this degree.  
                        Debug.Assert(*((int*)(bufferBase + idx)) > 0x10000);    // Is the method table pointer half way sane? 

                        // Mutate the method table pointer to be the type index instead. 
                        *((int*)(bufferBase + idx)) = (int)objType.Index + GCHeapType.TypeIndexStart;
                        heap.GetNextObject();
                    }
                    stream.Write(buffer, 0, readSize);

                    bufferBasePos += (ulong)readSize;
                    bytesLeftInSegment -= readSize;
                }
            }
            Debug.Assert(numObjects > 0 || Length < 16);
            NumberOfObjects = numObjects;
            Heap.NumberOfObjects += numObjects;
        }

        void IFastSerializable.ToStream(Serializer serializer)
        {
            serializer.Write((long)Start);
            serializer.Write(Length);
            serializer.Write((int)Generation);
            serializer.Write(Heap);

            // TODO remove tags, and align
            serializer.Write("Start of Heap Data");

            // We cheat here and access the raw stream for efficiently (to avoid a copy)
            var ioStreamWriter = serializer.Writer as IOStreamStreamWriter;
            Debug.Assert(ioStreamWriter != null);
            ioStreamWriter.Flush();
            Console.WriteLine("Position = 0x{0:x}", ioStreamWriter.GetLabel());
            Console.WriteLine("Start of rawStream = 0x{0:x}", ioStreamWriter.RawStream.Position);

            // TODO we can fix this restriction. 
            Debug.Assert(Heap.m_process != null, "Currently we only support serializing live heaps");
            SerializeHeapDataToStream(ioStreamWriter.RawStream);
            Console.WriteLine("End of rawStream = 0x{0:x}", ioStreamWriter.RawStream.Position);

            // Write a tag so that we know we did not screw up
            serializer.Write("End of Heap Data");
            serializer.Write(NumberOfObjects);
        }
        void IFastSerializable.FromStream(Deserializer deserializer)
        {
            Start = (Address)deserializer.ReadInt64();
            int length;
            deserializer.Read(out length);
            Length = length;
            Generation = deserializer.ReadInt();
            Heap = (GCHeap)deserializer.ReadObject();

            string tag = deserializer.ReadString();
            Debug.Assert(tag == "Start of Heap Data");

            // Be lazy about the actual data.  
            m_DataReader = (MemoryStreamReader)deserializer.Reader;
            m_DataStart = deserializer.Current;

            // Skip the data, we will read it on demand later.  
            deserializer.Goto(m_DataReader.AddOffset(m_DataStart, Length));

            tag = deserializer.ReadString();
            Debug.Assert(tag == "End of Heap Data");
            NumberOfObjects = deserializer.ReadInt();
        }

        internal StreamLabel m_DataStart;
        internal MemoryStreamReader m_DataReader;
        #endregion
    }
    public enum GCRootKind { Strong, Weak, Pinning, Finalizer, Stack, Max = Stack }
    public class GCRoot : IFastSerializable
    {
        /// <summary>
        /// Can be empty string if the root has no name.  
        /// </summary>
        public string Name { get; private set; }
        /// <summary>
        /// The module associated with the root.  May be the empty string if there is no information
        /// </summary>
        public string Module { get; private set; }
        public GCRootKind Kind { get; private set; }
        public Address HeapReference { get; private set; }
        public Address AddressOfRoot { get; private set; }

        public void ToXml(TextWriter writer)
        {
            writer.WriteLine("  <GCRoot Name=\"{0}\" Kind=\"{1}\" AddressOfRoot=\"0x{2:x}\" HeapReference=\"0x{3:x}\" Module=\"{4}\"/>",
                Name, Kind, AddressOfRoot, HeapReference, Module);
        }
        public override string ToString()
        {
            TextWriter sw = new StringWriter();
            ToXml(sw);
            return sw.ToString();
        }
        #region private
        internal GCRoot() { } // For deserialization
        internal GCRoot(string name, string module, Address heapReference, Address addressOfRoot)
        {
            Kind = GCRootKind.Strong;
            Name = name;
            Module = module;
            HeapReference = heapReference;
            AddressOfRoot = addressOfRoot;
        }
        internal GCRoot(ref COR_GC_REFERENCE root, GCHeap heap)
        {
            Address address;
            root.Location.GetAddress(out address);
            AddressOfRoot = address;
            HeapReference = heap.FetchLiveIntPtrAt(address, 0);
            Module = "";
            if (root.Type == CorGCReferenceType.CorHandleStrong)
            {
                Kind = GCRootKind.Strong;
                Name = "Strong Handle";
            }
            else if (root.Type == CorGCReferenceType.CorHandleStrongPinning)
            {
                Kind = GCRootKind.Pinning;
                Name = "Pinning Handle";
            }
            else if (root.Type == CorGCReferenceType.CorHandleWeakShort)
            {
                Kind = GCRootKind.Weak;
                Name = "Weak Handle";
            }
            else if (root.Type == CorGCReferenceType.CorReferenceStack)
            {
                Kind = GCRootKind.Stack;
                Name = "Local Variable";
            }
            else if (root.Type == CorGCReferenceType.CorReferenceFinalizer)
            {
                Kind = GCRootKind.Finalizer;
                Name = "Finalization";
            }
            else
            {
                Kind = GCRootKind.Strong;      // TODO FIX NOW complete the enumeration. 
                Name = "Other Handle";
            }
        }

        void IFastSerializable.ToStream(Serializer serializer)
        {
            serializer.Write(Name);
            serializer.Write(Module);
            serializer.Write((byte)Kind);
            serializer.Write((long)AddressOfRoot);
            serializer.Write((long)HeapReference);
        }
        void IFastSerializable.FromStream(Deserializer deserializer)
        {
            Name = deserializer.ReadString();
            Module = deserializer.ReadString();
            Kind = (GCRootKind)deserializer.ReadByte();
            AddressOfRoot = (Address)deserializer.ReadInt64();
            HeapReference = (Address)deserializer.ReadInt64();
        }
        #endregion
    }

    /// <summary>
    /// GCHeapTypeIndex is a integer (can be used in arrays).  We use an enum
    /// so that we can't mix it up with other indexes etc.  
    /// </summary>
    public enum GCHeapTypeIndex { Invalid = -1 }

    /// <summary>
    /// Represents a type of an object on the GC heap.   
    /// </summary>
    public class GCHeapType : IFastSerializable
    {
        public string Name { get; private set; }
        /// <summary>
        /// A Index is an integer from [0 GCHeap.
        /// </summary>
        public GCHeapTypeIndex Index { get; private set; }
        /// <summary>
        /// BaseSize is the fixed size of the object (the whole size for classes, the size of a 0 
        /// length array for arrays.   This is what is known of the size without seeing a particual
        /// instance.   Call 'GetSize' to get the size of a particular instance. 
        /// </summary>
        public int BaseSize { get { return m_size; } }
        /// <summary>
        /// Compute the size of an object at it exists on the GC heap (boxed)
        /// </summary>
        public int GetSize(Address objRef)
        {
            Debug.Assert(m_heap.GetObjectType(objRef) == this);         // You should be calling only on appropriate objRefs
            int size = m_size;
            if (IsArray)
            {
                var arrayLength = (int)m_heap.FetchIntPtrAt(objRef, m_array.countOffset);
                size += arrayLength * m_array.elementSize;
                int roundMask = Heap.PointerSize - 1;
                size = (size + roundMask) & ~roundMask;     // Round up to a pointer size 
            }

            Debug.Assert(size >= 12 && size % Heap.PointerSize == 0);
            return size;
        }
        public IList<GCHeapField> Fields { get { return m_fields; } }
        /// <summary>
        /// The heap this type belongs to
        /// </summary>
        public GCHeap Heap { get { return m_heap; } }
        /// <summary>
        /// The file name of the module that defined this type (or its generic template
        /// </summary>
        public string ModuleFileName { get; private set; }
        /// <summary>
        /// Returns true if this type is a primitive type GetValue() can be called.  
        /// Strings are primitive, so are all integer types, floats char and bool.  
        /// </summary>
        public bool IsPrimitive { get { return TypeKind <= CorElementType.ELEMENT_TYPE_PTR; } }
        /// <summary>
        /// Returns an enumeration that indictes the kind of primitive type you are.  
        /// </summary>
        public CorElementType TypeKind { get; private set; }
        /// <summary>
        /// Returns true if the object is an array.   Strings are considered arrays.    
        /// </summary>
        public bool IsArray { get; private set; }
        /// <summary>
        /// This fuction only works if 'this' is a primitive type.  For structures or arrays, you need to fetch
        /// the address and type, and recurse until you get to primitive types.  
        /// </summary>
        public object GetValue(Address address)
        {
            // TODO FIX NOW put back in Debug.Assert(IsPrimitive);

            var val = Heap.FetchIntPtrAt(address, 0);
            switch (TypeKind)
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
                        var reader = Heap.Seek(address);
                        reader.Skip(m_array.countOffset);
                        // TODO do something more efficient
                        int count = reader.ReadInt32();
                        var chars = new char[count];
                        for (int i = 0; i < chars.Length; i++)
                            chars[i] = (Char)reader.ReadInt16();
                        var ret = new string(chars);
                        return ret;
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
        /// <summary>
        /// When you enumerate a object, the offset within the object is returned.  This offset might represent
        /// nested fields (obj.Field1.Field2).    GetFieldOffset returns the first of these field (Field1), 
        /// and 'remaining' offset with the type of Field1 (which must be a struct type).   Calling 
        /// GetFieldForOffset repeatedly until the childFieldOffset is 0 will retrieve the whole chain.  
        /// </summary>
        /// <returns>true if successful.  Will fail if it 'this' is an array type</returns>
        public bool GetFieldForOffset(int fieldOffset, out GCHeapField childField, out int childFieldOffset)
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

        // These are only valid for Array Types
        public GCHeapType ElementType { get; private set; }
        public int GetArrayLength(Address objRef)
        {
            Debug.Assert(m_heap.GetObjectType(objRef) == this);
            Debug.Assert(IsArray);

            return (int)m_heap.FetchIntPtrAt(objRef, m_array.countOffset);
        }
        public Address GetArrayElementAddress(Address objRef, int index)
        {
            Debug.Assert(m_heap.GetObjectType(objRef) == this);
            Debug.Assert(IsArray);
            Debug.Assert(0 <= index && index < GetArrayLength(objRef));

            return objRef + (uint)((index * m_array.elementSize) + m_array.firstElementOffset);
        }

        public void ToXml(TextWriter writer)
        {
            writer.WriteLine("   <GCHeapType Name=\"{0}\" Index=\"0x{1:x}\" IsArray=\"{2}\" BaseSize=\"{3}\" TypeKind=\"{4}\"",
                Name, Index, IsArray, m_size, TypeKind);
            writer.Write("    ModuleFileName=\"{0}\"", ModuleFileName);
            if (IsArray)
            {
                writer.Write(" ElementSize=\"{0}\"", m_array.elementSize);
                if (ElementType != null)
                    writer.Write(" ElementTypeIndex=\"{0}\"", ElementType.Index);
            }
            if (m_fields != null && m_fields.Length != 0)
            {
                writer.WriteLine(">");
                writer.WriteLine("    <Fields Count=\"{0}\">", m_fields.Length);
                foreach (var field in m_fields)
                    if (field != null)
                        field.ToXml(writer);
                writer.WriteLine("    </Fields>");
                writer.WriteLine("   </GCHeapType>");
            }
            else
                writer.WriteLine("/>");
        }
        public override string ToString()
        {
            TextWriter sw = new StringWriter();
            ToXml(sw);
            return sw.ToString();
        }
        #region private
        internal const int TypeIndexStart = unchecked((int) 0xFF000000);         // We displace the type index by this quantity to make them more recognisable in dumps (better asserts)

        internal GCHeapType() { } // Used for deserialization
        internal GCHeapType(GCHeap heap, COR_TYPEID typeID, GCHeapTypeIndex typeIndex)
        {
            heap.m_typeTable[typeID] = this;
            heap.m_types.Add(this);

            Index = typeIndex;
            m_heap = heap;
            Name = "";
            ModuleFileName = "";

            IMetadataImport metaData = null;
            var buffer = new StringBuilder(1024);
            int bufferSizeRet;

            // This is getting names.   If we fail, we can still plow on ....
            try // FIX NOW REMOVE try-catch...
            {
                ICorDebugType corType = null;
                Console.WriteLine("Calling GetTypeForTypeID {0:x} {1:x}", typeID.token1, typeID.token2);
                heap.m_process5.GetTypeForTypeID(typeID, out corType);

                CorElementType corElementType;
                corType.GetType(out corElementType);
                TypeKind = corElementType;

                if (corElementType == CorElementType.ELEMENT_TYPE_CLASS || corElementType == CorElementType.ELEMENT_TYPE_VALUETYPE)
                {
                    ICorDebugClass corClass;
                    corType.GetClass(out corClass);

                    uint classToken;
                    corClass.GetToken(out classToken);

                    ICorDebugModule corModule;
                    corClass.GetModule(out corModule);

                    // Get the module name
                    char[] moduleNameChars = new char[1024];
                    uint moduleNameLen;
                    corModule.GetName((uint)moduleNameChars.Length, out moduleNameLen, moduleNameChars);
                    ModuleFileName = new string(moduleNameChars, 0, (int)moduleNameLen - 1);  // -1 since the len includes the terminator;

                    // TODO cache modules and meta-data interfaces.  
                    var guid = new Guid("FCE5EFA0-8BBA-4f8e-A036-8F2022B08466");
                    corModule.GetMetaDataInterface(ref guid, out metaData);

                    System.Reflection.TypeAttributes flags;
                    int baseClassTok;
                    metaData.GetTypeDefProps((int)classToken, buffer, buffer.Capacity, out bufferSizeRet, out flags, out baseClassTok);
                    Name = buffer.ToString();
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine("Caught exception for type ID {0:x} {1:x}: {2}", typeID.token1, typeID.token2, e.Message);
                Name = string.Format("!ERROR TYPE ID {0:x} {1:x}", typeID.token1, typeID.token2);
                ModuleFileName = Name;
            }


            COR_TYPE_LAYOUT header = new COR_TYPE_LAYOUT();
            heap.m_process5.GetTypeLayout(typeID, out header);
            m_boxOffset = header.boxOffset;
            m_size = header.objectSize;
            IsArray = (header.isStringOrArray != 0);
            if (IsArray)
            {
                heap.m_process5.GetArrayLayout(typeID, out m_array);
                if (Name != "System.String")
                {
                    // TODO FIX NOW should not have to special case, string should 'just work' 
                    ElementType = heap.GetObjectTypeFromID(m_array.componentID);

                    // TODO FIX NOW HACK for arrays of value types
                    if (m_array.componentType == CorComponentType.CorComponentValueClass)
                        m_array.firstElementOffset = Heap.PointerSize * 2;

                    if (TypeKind == CorElementType.ELEMENT_TYPE_SZARRAY)
                        Name = ElementType.Name + "[]";
                    else if (TypeKind == CorElementType.ELEMENT_TYPE_ARRAY)
                    {
                        if (m_array.numRanks == 1)
                            Name = ElementType.Name + "[*]";
                        else
                            Name = ElementType.Name + "[" + new string(',', m_array.numRanks - 1) + "]";
                    }
                }
                Debug.Assert(m_array.firstElementOffset > 0);
            }
            else
            {
                if (header.numFields > 0)
                {
                    m_fields = new GCHeapField[header.numFields];
                    var corFields = new COR_FIELD[header.numFields];

                    int fieldsFetched;
                    heap.m_process5.GetTypeFields(typeID, corFields.Length, corFields, out fieldsFetched);
                    Debug.Assert(fieldsFetched == m_fields.Length);

                    for (int i = 0; i < corFields.Length; i++)
                    {
                        int fieldTypeToken, fieldAttr, sigBlobSize, cplusTypeFlab, fieldValSize;
                        IntPtr sigBlob, fieldVal;
                        buffer.Length = 0;
                        if (metaData != null)
                            metaData.GetFieldProps(corFields[i].token, out fieldTypeToken, buffer, buffer.Capacity, out bufferSizeRet,
                                out fieldAttr, out sigBlob, out sigBlobSize, out cplusTypeFlab, out fieldVal, out fieldValSize);

                        var name = buffer.ToString();
                        GCHeapType fieldType = null;
                        // TODO FIX NOW ignoring object ref fields.  This is working around a bug.   
                        if (corFields[i].fieldType != CorComponentType.CorComponentGCRef)
                            fieldType = heap.GetObjectTypeFromID(corFields[i].id);

                        // TODO FIX NOW better way?
                        // Detect a primitive type (which has recursion, and stop the recursion 
                        if (corFields[i].id.token1 == typeID.token1 && corFields[i].id.token2 == typeID.token2)
                        {
                            m_fields = new GCHeapField[0];
                            break;
                        }

                        // TODO FIX NOW HACK for field offsets
                        if (TypeKind == CorElementType.ELEMENT_TYPE_CLASS)
                            corFields[i].offset -= Heap.PointerSize;

                        m_fields[i] = new GCHeapField(name, corFields[i].offset, fieldType, corFields[i].fieldType);
                    }
                }
                else
                    m_fields = new GCHeapField[0];

#if DEBUG
                foreach (var field in m_fields)
                    Debug.Assert(field != null);
#endif
            }
        }

        void IFastSerializable.ToStream(Serializer serializer)
        {
            serializer.Write((int)Index);
            serializer.Write(Name);
            serializer.Write(ModuleFileName);
            serializer.Write((int)TypeKind);

            serializer.Write(m_heap);
            serializer.Write(m_boxOffset);
            serializer.Write(m_size);

            // array specific 
            serializer.Write(IsArray);
            serializer.Write(ElementType);

            // Dump m_array;
            serializer.Write((int)m_array.componentType);
            serializer.Write(m_array.firstElementOffset);
            serializer.Write(m_array.elementSize);
            serializer.Write(m_array.countOffset);
            serializer.Write(m_array.rankSize);
            serializer.Write(m_array.numRanks);
            serializer.Write(m_array.rankOffset);

            if (m_fields != null)
            {
                serializer.Write(m_fields.Length);
                for (int i = 0; i < m_fields.Length; i++)
                    serializer.WritePrivate(m_fields[i]);
            }
            else
                serializer.Write(0);

            // TODO FIX NOW remove when we know it works
            serializer.Write("GCHeapTypeCheck");
        }
        void IFastSerializable.FromStream(Deserializer deserializer)
        {
            Index = (GCHeapTypeIndex)deserializer.ReadInt();
            Name = deserializer.ReadString();
            ModuleFileName = deserializer.ReadString();
            TypeKind = (CorElementType)deserializer.ReadInt();

            deserializer.Read(out m_heap);
            deserializer.Read(out m_boxOffset);
            deserializer.Read(out m_size);

            // array specific 
            IsArray = deserializer.ReadBool();
            ElementType = (GCHeapType)deserializer.ReadObject();

            // read m_array
            m_array.componentType = (CorComponentType)deserializer.ReadInt();
            deserializer.Read(out m_array.firstElementOffset);
            deserializer.Read(out m_array.elementSize);
            deserializer.Read(out m_array.countOffset);
            deserializer.Read(out m_array.rankSize);
            deserializer.Read(out m_array.numRanks);
            deserializer.Read(out m_array.rankOffset);

            // Read in fields
            var len = deserializer.ReadInt();
            m_fields = null;
            if (len > 0)
            {
                m_fields = new GCHeapField[len];
                for (int i = 0; i < m_fields.Length; i++)
                    deserializer.Read(out m_fields[i]);
            }

            // TODO FIX NOW remove when we know it works
            var check = deserializer.ReadString();
            Debug.Assert(check == "GCHeapTypeCheck");
        }

        internal int m_size;
        internal int m_boxOffset;
        internal COR_ARRAY_LAYOUT m_array;           // Only valid if it is an array. 
        internal GCHeapField[] m_fields;             // Only valid if it is a class
        private GCHeap m_heap;
        #endregion
    }

    public class GCHeapField : IFastSerializable
    {
        /// <summary>
        /// The name of the field
        /// </summary>
        public string Name { get; private set; }
        /// <summary>
        /// The offset from the begining of the object does NOT include the 'boxing' overhead (the Method Table Pointer)
        /// </summary>
        public int Offset { get; private set; }
        /// <summary>
        /// The type of the field 
        /// </summary>
        public GCHeapType Type { get; private set; }
        /// <summary>
        /// Fetch the address of the field. 
        /// </summary>
        public Address GetFieldAddress(Address objRef)
        {
            return objRef + (uint)Offset;
        }
        /// <summary>
        /// Only works for primtitive types.    
        /// 
        /// This is a convenience function.  It fetches the field address and the calls GetValue()
        /// </summary>
        public object GetFieldValue(Address objRef)
        {
            return Type.GetValue(GetFieldAddress(objRef));
        }

        public void ToXml(TextWriter writer)
        {
            if (Type != null)
            {
                writer.WriteLine("     <GCHeapField Name=\"{0}\" Type=\"{1}\" TypeIndex=\"{2}\" Offset=\"{3}\"/>", Name, Type.Name, Type.Index, Offset);
                return;
            }

            var typeName = "!UNKNOWN";
            if (m_ComponentType == CorComponentType.CorComponentGCRef)
                typeName += "_REF";
            writer.WriteLine("     <GCHeapField Name=\"{0}\" Type=\"{1}\" Offset=\"{2}\"/>", Name, typeName, Offset);
        }
        public override string ToString()
        {
            TextWriter sw = new StringWriter();
            ToXml(sw);
            return sw.ToString();
        }
        #region private
        internal GCHeapField() { } // For deserialization 
        internal GCHeapField(string name, int offset, GCHeapType fieldType, CorComponentType componentType)
        {
            Offset = offset;
            Name = name;
            Type = fieldType;
            m_ComponentType = componentType;
        }
        void IFastSerializable.ToStream(Serializer serializer)
        {
            serializer.Write(Name);
            serializer.Write(Offset);
            serializer.Write((int)m_ComponentType);
            serializer.Write(Type);
        }
        void IFastSerializable.FromStream(Deserializer deserializer)
        {
            Name = deserializer.ReadString();
            Offset = deserializer.ReadInt();
            m_ComponentType = (CorComponentType)deserializer.ReadInt();
            Type = (GCHeapType)deserializer.ReadObject();
        }

        internal CorComponentType m_ComponentType;
        #endregion
    }
}