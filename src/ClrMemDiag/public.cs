using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Address = System.UInt64;
using Microsoft.Diagnostics.Runtime;
using System.Text;
using Microsoft.Diagnostics.Runtime.Utilities;
using System.Threading;
using Dia2Lib;

namespace Microsoft.Diagnostics.Runtime
{
    /// <summary>
    /// The architecture of a process.
    /// </summary>
    public enum Architecture
    {
        /// <summary>
        /// Unknown.  Should never be exposed except in case of error.
        /// </summary>
        Unknown,

        /// <summary>
        /// x86.
        /// </summary>
        X86,

        /// <summary>
        /// x64
        /// </summary>
        Amd64,

        /// <summary>
        /// ARM
        /// </summary>
        Arm
    }

    /// <summary>
    /// A ClrHeap is a abstraction for the whole GC Heap.   Subclasses allow you to implement this for 
    /// a particular kind of heap (whether live,
    /// </summary>
    public abstract class ClrHeap
    {
        /// <summary>
        /// And the ability to take an address of an object and fetch its type (The type alows further exploration)
        /// </summary>
        abstract public ClrType GetObjectType(Address objRef);

        /// <summary>
        /// Returns a  wrapper around a System.Exception object (or one of its subclasses).
        /// </summary>
        virtual public ClrException GetExceptionObject(Address objRef) { return null; }

        /// <summary>
        /// Returns the runtime associated with this heap.
        /// </summary>
        virtual public ClrRuntime GetRuntime() { return null; }

        /// <summary>
        /// A heap is has a list of contiguous memory regions called segments.  This list is returned in order of
        /// of increasing object addresses.  
        /// </summary>
        abstract public IList<ClrSegment> Segments { get; }

        /// <summary>
        /// Enumerate the roots of the process.  (That is, all objects which keep other objects alive.)
        /// Equivalent to EnumerateRoots(true).
        /// </summary>
        abstract public IEnumerable<ClrRoot> EnumerateRoots();

        /// <summary>
        /// Returns a type by its index.
        /// </summary>
        /// <param name="index">The type to get.</param>
        /// <returns>The ClrType of that index.</returns>
        abstract public ClrType GetTypeByIndex(int index);

        /// <summary>
        /// Looks up a type by name.
        /// </summary>
        /// <param name="name">The name of the type.</param>
        /// <returns>The ClrType matching 'name', null if the type was not found, and undefined if more than one
        /// type shares the same name.</returns>
        abstract public ClrType GetTypeByName(string name);

        /// <summary>
        /// Returns the max index.
        /// </summary>
        abstract public int TypeIndexLimit { get; }

        /// <summary>
        /// Enumerate the roots in the process.
        /// </summary>
        /// <param name="enumerateStatics">True if we should enumerate static variables.  Enumerating with statics 
        /// can take much longer than enumerating without them.  Additionally these will be be "double reported",
        /// since all static variables are pinned by handles on the HandleTable (which is also enumerated with 
        /// EnumerateRoots).  You would want to enumerate statics with roots if you care about what exact statics
        /// root what objects, but not if you care about performance.</param>
        abstract public IEnumerable<ClrRoot> EnumerateRoots(bool enumerateStatics);

        /// <summary>
        /// Enumerates all types in the runtime.
        /// </summary>
        /// <returns>An enumeration of all types in the target process.  May return null if it's unsupported for
        /// that version of CLR.</returns>
        virtual public IEnumerable<ClrType> EnumerateTypes() { return null; }

        /// <summary>
        /// Enumerates all finalizable objects on the heap.
        /// </summary>
        virtual public IEnumerable<Address> EnumerateFinalizableObjects() { throw new NotImplementedException(); }

        /// <summary>
        /// Enumerates all managed locks in the process.  That is anything using System.Monitor either explictly
        /// or implicitly through "lock (obj)".  This is roughly equivalent to combining SOS's !syncblk command
        /// with !dumpheap -thinlock.
        /// </summary>
        virtual public IEnumerable<BlockingObject> EnumerateBlockingObjects() { throw new NotImplementedException(); }

        /// <summary>
        /// Returns true if the GC heap is in a consistent state for heap enumeration.  This will return false
        /// if the process was stopped in the middle of a GC, which can cause the GC heap to be unwalkable.
        /// Note, you may still attempt to walk the heap if this function returns false, but you will likely
        /// only be able to partially walk each segment.
        /// </summary>
        abstract public bool CanWalkHeap { get; }

        /// <summary>
        /// Enumerates all objects on the heap.  This is equivalent to enumerating all segments then walking
        /// each object with ClrSegment.FirstObject, ClrSegment.NextObject, but in a simple enumerator
        /// for easier use in linq queries.
        /// </summary>
        /// <returns>An enumerator for all objects on the heap.</returns>
        abstract public IEnumerable<Address> EnumerateObjects();

        /// <summary>
        /// TotalHeapSize is defined as the sum of the length of all segments.  
        /// </summary>
        abstract public ulong TotalHeapSize { get; }

        /// <summary>
        /// Get the size by generation 0, 1, 2, 3.  The large object heap is Gen 3 here. 
        /// The sum of all of these should add up to the TotalHeapSize.  
        /// </summary>
        abstract public ulong GetSizeByGen(int gen);

        /// <summary>
        /// Returns the generation of an object.
        /// </summary>
        public int GetGeneration(Address obj)
        {
            ClrSegment seg = GetSegmentByAddress(obj);
            if (seg == null)
                return -1;

            return seg.GetGeneration(obj);
        }

        /// <summary>
        /// Returns the GC segment for the given object.
        /// </summary>
        public abstract ClrSegment GetSegmentByAddress(Address objRef);

        /// <summary>
        /// Returns true if the given address resides somewhere on the managed heap.
        /// </summary>
        public bool IsInHeap(Address address) { return GetSegmentByAddress(address) != null; }

        /// <summary>
        /// Pointer size of on the machine (4 or 8 bytes).  
        /// </summary>
        public abstract int PointerSize { get; }

        /// <summary>
        /// Returns a string representation of this heap, including the size and number of segments.
        /// </summary>
        /// <returns>The string representation of this heap.</returns>
        public override string ToString()
        {
            var sizeMB = TotalHeapSize / 1000000.0;
            int segCount = Segments != null ? Segments.Count : 0;
            return string.Format("ClrHeap {0}mb {1} segments", sizeMB, segCount);
        }

        /// <summary>
        /// Read 'count' bytes from the ClrHeap at 'address' placing it in 'buffer' starting at offset 'offset'
        /// </summary>
        virtual public int ReadMemory(Address address, byte[] buffer, int offset, int count) { return 0; }

        /// <summary>
        /// Attempts to efficiently read a pointer from memory.  This acts exactly like ClrRuntime.ReadPointer, but
        /// there is a greater chance you will hit a chache for a more efficient memory read.
        /// </summary>
        /// <param name="addr">The address to read.</param>
        /// <param name="value">The pointer value.</param>
        /// <returns>True if we successfully read the value, false if addr is not mapped into the process space.</returns>
        public abstract bool ReadPointer(Address addr, out Address value);
    }

    /// <summary>
    /// Represents a managed lock within the runtime.
    /// </summary>
    public abstract class BlockingObject
    {
        /// <summary>
        /// The object associated with the lock.
        /// </summary>
        abstract public Address Object { get; }

        /// <summary>
        /// Whether or not the object is currently locked.
        /// </summary>
        abstract public bool Taken { get; }

        /// <summary>
        /// The recursion count of the lock (only valid if Locked is true).
        /// </summary>
        abstract public int RecursionCount { get; }

        /// <summary>
        /// The thread which currently owns the lock.  This is only valid if Taken is true and
        /// only valid if HasSingleOwner is true.
        /// </summary>
        abstract public ClrThread Owner { get; }

        /// <summary>
        /// Returns true if this lock has only one owner.  Returns false if this lock
        /// may have multiple owners (for example, readers on a RW lock).
        /// </summary>
        abstract public bool HasSingleOwner { get; }

        /// <summary>
        /// Returns the list of owners for this object.
        /// </summary>
        abstract public IList<ClrThread> Owners { get; }

        /// <summary>
        /// Returns the list of threads waiting on this object.
        /// </summary>
        abstract public IList<ClrThread> Waiters { get; }

        /// <summary>
        /// The reason why it's blocking.
        /// </summary>
        abstract public BlockingReason Reason { get; internal set; }
    }

    /// <summary>
    /// The type of GCRoot that a ClrRoot represnts.
    /// </summary>
    public enum GCRootKind
    {
        /// <summary>
        /// The root is a static variable.
        /// </summary>
        StaticVar,

        /// <summary>
        /// The root is a thread static.
        /// </summary>
        ThreadStaticVar,

        /// <summary>
        /// The root is a local variable (or compiler generated temporary variable).
        /// </summary>
        LocalVar,
        
        /// <summary>
        /// The root is a strong handle.
        /// </summary>
        Strong,

        /// <summary>
        /// The root is a weak handle.
        /// </summary>
        Weak,

        /// <summary>
        /// The root is a strong pinning handle.
        /// </summary>
        Pinning,

        /// <summary>
        /// The root comes from the finalizer queue.
        /// </summary>
        Finalizer,

        /// <summary>
        /// The root is an async IO (strong) pinning handle.
        /// </summary>
        AsyncPinning, 

        /// <summary>
        /// The max value of this enum.
        /// </summary>
        Max = AsyncPinning
    }

    /// <summary>
    /// Represents a root in the target process.  A root is the base entry to the GC's mark and sweep algorithm.
    /// </summary>
    public abstract class ClrRoot
    {
        /// <summary>
        /// A GC Root also has a Kind, which says if it is a strong or weak root
        /// </summary>
        abstract public GCRootKind Kind { get; }

        /// <summary>
        /// The name of the root. 
        /// </summary>
        virtual public string Name { get { return ""; } }

        /// <summary>
        /// The type of the object this root points to.  That is, ClrHeap.GetObjectType(ClrRoot.Object).
        /// </summary>
        abstract public ClrType Type { get; }

        /// <summary>
        /// The object on the GC heap that this root keeps alive.
        /// </summary>
        virtual public Address Object { get; protected set; }

        /// <summary>
        /// The address of the root in the target process.
        /// </summary>
        virtual public Address Address { get; protected set; }

        /// <summary>
        /// If the root can be identified as belonging to a particular AppDomain this is that AppDomain.
        /// It an be null if there is no AppDomain associated with the root.  
        /// </summary>
        virtual public ClrAppDomain AppDomain { get { return null; } }

        /// <summary>
        /// If the root has a thread associated with it, this will return that thread.
        /// </summary>
        virtual public ClrThread Thread { get { return null; } }

        /// <summary>
        /// Returns true if Object is an "interior" pointer.  This means that the pointer may actually
        /// point inside an object instead of to the start of the object.
        /// </summary>
        virtual public bool IsInterior { get { return false; } }

        /// <summary>
        /// Returns true if the root "pins" the object, preventing the GC from relocating it.
        /// </summary>
        virtual public bool IsPinned { get { return false; } }

        /// <summary>
        /// Unfortunately some versions of the APIs we consume do not give us perfect information.  If
        /// this property is true it means we used a heuristic to find the value, and it might not
        /// actually be considered a root by the GC.
        /// </summary>
        virtual public bool IsPossibleFalsePositive { get { return false; } }

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override string ToString()
        {
            return string.Format("GCRoot {0:X8}->{1:X8} {2}", Address, Object, Name);
        }
    }

    /// <summary>
    /// An interface implementation in the target process.
    /// </summary>
    public abstract class ClrInterface
    {
        /// <summary>
        /// The typename of the interface.
        /// </summary>
        abstract public string Name { get; }

        /// <summary>
        /// The interface that this interface inherits from.
        /// </summary>
        abstract public ClrInterface BaseInterface { get; }

        /// <summary>
        /// Display string for this interface.
        /// </summary>
        /// <returns>Display string for this interface.</returns>
        public override string ToString()
        {
            return Name;
        }

        /// <summary>
        /// Equals override.
        /// </summary>
        /// <param name="obj">Object to compare to.</param>
        /// <returns>True if this interface equals another.</returns>
        public override bool Equals(object obj)
        {
            if (obj == null || !(obj is ClrInterface))
                return false;

            ClrInterface rhs = (ClrInterface)obj;
            if (Name != rhs.Name)
                return false;

            if (BaseInterface == null)
            {
                return rhs.BaseInterface == null;
            }
            else
            {
                return BaseInterface.Equals(rhs.BaseInterface);
            }
        }

        /// <summary>
        /// GetHashCode override.
        /// </summary>
        /// <returns>A hashcode for this object.</returns>
        public override int GetHashCode()
        {
            int hashCode = 0;

            if (Name != null)
                hashCode ^= Name.GetHashCode();

            if (BaseInterface != null)
                hashCode ^= BaseInterface.GetHashCode();

            return hashCode;
        }
    }

    /// <summary>
    /// A representation of a type in the target process.
    /// </summary>
    public abstract class ClrType
    {
        /// <summary>
        /// The index of this type.
        /// </summary>
        abstract public int Index { get; }

        /// <summary>
        /// Returns the metadata token of this type.
        /// </summary>
        abstract public uint MetadataToken { get; }

        /// <summary>
        /// Types have names.
        /// </summary>
        abstract public string Name { get; }
        /// <summary>
        /// GetSize returns the size in bytes for the total overhead of the object 'objRef'.   
        /// </summary>
        abstract public ulong GetSize(Address objRef);
        /// <summary>
        /// EnumeationRefsOfObject will call 'action' once for each object reference inside 'objRef'.  
        /// 'action' is passed the address of the outgoing refernece as well as an integer that
        /// represents the field offset.  While often this is the physical offset of the outgoing
        /// refernece, abstractly is simply something that can be given to GetFieldForOffset to 
        /// return the field information for that object reference  
        /// </summary>
        abstract public void EnumerateRefsOfObject(Address objRef, Action<Address, int> action);
        
        /// <summary>
        /// Does the same as EnumerateRefsOfObject, but does additional bounds checking to ensure
        /// we don't loop forever with inconsistent data.
        /// </summary>
        abstract public void EnumerateRefsOfObjectCarefully(Address objRef, Action<Address, int> action);

        /// <summary>
        /// Returns true if the type CAN contain references to other objects.  This is used in optimizations 
        /// and 'true' can always be returned safely.  
        /// </summary>
        virtual public bool ContainsPointers { get { return true; } }

        /// <summary>
        /// All types know the heap they belong to.  
        /// </summary>
        abstract public ClrHeap Heap { get; }

        /// <summary>
        /// Returns true if this object is a 'RuntimeType' (that is, the concrete System.RuntimeType class
        /// which is what you get when calling "typeof" in C#).
        /// </summary>
        virtual public bool IsRuntimeType { get { return false; } }

        /// <summary>
        /// Returns the concrete type (in the target process) that this RuntimeType represents.
        /// Note you may only call this function if IsRuntimeType returns true.
        /// </summary>
        /// <param name="obj">The RuntimeType object to get the concrete type for.</param>
        /// <returns>The underlying type that this RuntimeType actually represents.  May return null if the
        ///          underlying type has not been fully constructed by the runtime, or if the underlying type
        ///          is actually a typehandle (which unfortunately ClrMD cannot convert into a ClrType due to
        ///          limitations in the underlying APIs.  (So always null-check the return value of this
        ///          function.) </returns>
        virtual public ClrType GetRuntimeType(ulong obj) { throw new NotImplementedException(); }

        /// <summary>
        /// Returns the module this type is defined in.
        /// </summary>
        virtual public ClrModule Module { get { return null; } }

        /// <summary>
        /// Returns the ElementType of this Type.  Can return ELEMENT_TYPE_VOID on error.
        /// </summary>
        virtual public ClrElementType ElementType { get { return ClrElementType.Unknown; } }

        /// <summary>
        /// Returns true if this type is a primitive (int, float, etc), false otherwise.
        /// </summary>
        /// <returns>True if this type is a primitive (int, float, etc), false otherwise.</returns>
        virtual public bool IsPrimitive { get { return ClrRuntime.IsPrimitive(ElementType); } }

        /// <summary>
        /// Returns true if this type is a ValueClass (struct), false otherwise.
        /// </summary>
        /// <returns>True if this type is a ValueClass (struct), false otherwise.</returns>
        virtual public bool IsValueClass { get { return ClrRuntime.IsValueClass(ElementType); } }

        /// <summary>
        /// Returns true if this type is an object reference, false otherwise.
        /// </summary>
        /// <returns>True if this type is an object reference, false otherwise.</returns>
        virtual public bool IsObjectReference { get { return ClrRuntime.IsObjectReference(ElementType); } }

        /// <summary>
        /// Returns the list of interfaces this type implements.
        /// </summary>
        abstract public IList<ClrInterface> Interfaces { get; }

        /// <summary>
        /// Returns true if the finalization is suppressed for an object.  (The user program called
        /// System.GC.SupressFinalize.  The behavior of this function is undefined if the object itself
        /// is not finalizable.
        /// </summary>
        virtual public bool IsFinalizeSuppressed(Address obj) { throw new NotImplementedException(); }

        /// <summary>
        /// Returns whether objects of this type are finalizable.
        /// </summary>
        abstract public bool IsFinalizable { get; }

        // Visibility:
        /// <summary>
        /// Returns true if this type is marked Public.
        /// </summary>
        abstract public bool IsPublic { get; }

        /// <summary>
        /// returns true if this type is marked Private.
        /// </summary>
        abstract public bool IsPrivate { get; }

        /// <summary>
        /// Returns true if this type is accessable only by items in its own assembly.
        /// </summary>
        abstract public bool IsInternal { get; }

        /// <summary>
        /// Returns true if this nested type is accessable only by subtypes of its outer type.
        /// </summary>
        abstract public bool IsProtected { get; }

        // Other attributes:
        /// <summary>
        /// Returns true if this class is abstract.
        /// </summary>
        abstract public bool IsAbstract { get; }

        /// <summary>
        /// Returns true if this class is sealed.
        /// </summary>
        abstract public bool IsSealed { get; }

        /// <summary>
        /// Returns true if this type is an interface.
        /// </summary>
        abstract public bool IsInterface { get; }

        /// <summary>
        /// Returns all possible fields in this type.   It does not return dynamically typed fields.  
        /// Returns an empty list if there are no fields.
        /// </summary>
        virtual public IList<ClrInstanceField> Fields { get { return null; } }

        /// <summary>
        /// Returns a list of static fields on this type.  Returns an empty list if there are no fields.
        /// </summary>
        virtual public IList<ClrStaticField> StaticFields { get { return null; } }
        
        /// <summary>
        /// Returns a list of thread static fields on this type.  Returns an empty list if there are no fields.
        /// </summary>
        virtual public IList<ClrThreadStaticField> ThreadStaticFields { get { return null; } }

        /// <summary>
        /// Gets the list of methods this type implements.
        /// </summary>
        virtual public IList<ClrMethod> Methods { get { return null; } }

        /// <summary>
        /// When you enumerate a object, the offset within the object is returned.  This offset might represent
        /// nested fields (obj.Field1.Field2).    GetFieldOffset returns the first of these field (Field1), 
        /// and 'remaining' offset with the type of Field1 (which must be a struct type).   Calling 
        /// GetFieldForOffset repeatedly until the childFieldOffset is 0 will retrieve the whole chain.  
        /// </summary>
        /// <returns>true if successful.  Will fail if it 'this' is an array type</returns>
        abstract public bool GetFieldForOffset(int fieldOffset, bool inner, out ClrInstanceField childField, out int childFieldOffset);

        /// <summary>
        /// Returns the field given by 'name', case sensitive.  Returns NULL if no such field name exists (or on error).
        /// </summary>
        abstract public ClrInstanceField GetFieldByName(string name);

        /// <summary>
        /// Returns the field given by 'name', case sensitive.  Returns NULL if no such field name exists (or on error).
        /// </summary>
        abstract public ClrStaticField GetStaticFieldByName(string name);

        /// <summary>
        /// Convenience function which dereferences fields.  For example, if you wish to dereference m_foo.m_bar.m_baz, you can pass:
        /// { "m_foo", "m_bar", "m_baz" } into this function's second parameter to dereference those fields to get the value.
        /// Throws Exception if a field you expect does not exist.
        /// </summary>
        virtual public object GetFieldValue(Address obj, ICollection<string> fields) { throw new NotImplementedException(); }

        /// <summary>
        /// Same as GetFieldValue but returns true on success, false on failure, and does not throw.
        /// </summary>
        virtual public bool TryGetFieldValue(Address obj, ICollection<string> fields, out object value) { value = null; return false; }

        /// <summary>
        /// If this type inherits from another type, this is that type.  Can return null if it does not inherit (or is unknown)
        /// </summary>
        abstract public ClrType BaseType { get; }

        /// <summary>
        /// Returns true if the given object is a Com-Callable-Wrapper.  This is only supported in v4.5 and later.
        /// </summary>
        /// <param name="obj">The object to check.</param>
        /// <returns>True if this is a CCW.</returns>
        virtual public bool IsCCW(Address obj) { return false; }

        /// <summary>
        /// Returns the CCWData for the given object.  Note you may only call this function if IsCCW returns true.
        /// </summary>
        /// <returns>The CCWData associated with the object, undefined result of obj is not a CCW.</returns>
        virtual public CcwData GetCCWData(Address obj)
        {
            return null;
        }

        /// <summary>
        /// Returns true if the given object is a Runtime-Callable-Wrapper.  This is only supported in v4.5 and later.
        /// </summary>
        /// <param name="obj">The object to check.</param>
        /// <returns>True if this is an RCW.</returns>
        virtual public bool IsRCW(Address obj) { return false; }

        /// <summary>
        /// Returns the RCWData for the given object.  Note you may only call this function if IsRCW returns true.
        /// </summary>
        /// <returns>The RCWData associated with the object, undefined result of obj is not a RCW.</returns>
        virtual public RcwData GetRCWData(Address obj)
        {
            return null;
        }

        /// <summary>
        /// A type is an array if you can use the array operators below, Abstractly arrays are objects 
        /// that whose children are not statically known by just knowing the type.  
        /// </summary>
        virtual public bool IsArray { get { return false; } }

        // If the type has dynamic elements (it is an array) this describes them.  
        /// <summary>
        /// Gets the type of the elements in the array.  
        /// </summary>
        virtual public ClrType ArrayComponentType { get; internal set; }

        /// <summary>
        /// If the type is an array, then GetArrayLength returns the number of elements in the array.  Undefined
        /// behavior if this type is not an array.
        /// </summary>
        abstract public int GetArrayLength(Address objRef);

        /// <summary>
        /// Returns the absolute address to the given array element.  You may then make a direct memory read out
        /// of the process to get the value if you want.
        /// </summary>
        abstract public Address GetArrayElementAddress(Address objRef, int index);

        /// <summary>
        /// Returns the array element value at the given index.  Returns 'null' if the array element is of type
        /// VALUE_CLASS.
        /// </summary>
        abstract public object GetArrayElementValue(Address objRef, int index);

        /// <summary>
        /// Returns the size of individual elements of an array.
        /// </summary>
        abstract public int ElementSize { get; }

        /// <summary>
        /// Returns the base size of the object.
        /// </summary>
        abstract public int BaseSize { get; }

        /// <summary>
        /// Returns true if this type is System.String.
        /// </summary>
        virtual public bool IsString { get { return false; } }

        /// <summary>
        /// Returns true if this type represents free space on the heap.
        /// </summary>
        virtual public bool IsFree { get { return false; } }

        /// <summary>
        /// Returns true if this type is an exception (that is, it derives from System.Exception).
        /// </summary>
        virtual public bool IsException { get { return false; } }

        /// <summary>
        /// Returns true if this type is an enum.
        /// </summary>
        virtual public bool IsEnum { get { return false; } }

        /// <summary>
        /// Returns the element type of this enum.
        /// </summary>
        virtual public ClrElementType GetEnumElementType() { throw new NotImplementedException(); }

        /// <summary>
        /// Returns a list of names in the enum.
        /// </summary>
        virtual public IEnumerable<string> GetEnumNames() { throw new NotImplementedException(); }

        /// <summary>
        /// Gets the name of the value in the enum, or null if the value doesn't have a name.
        /// This is a convenience function, and has undefined results if the same value appears
        /// twice in the enum.
        /// </summary>
        /// <param name="value">The value to lookup.</param>
        /// <returns>The name of one entry in the enum with this value, or null if none exist.</returns>
        virtual public string GetEnumName(object value) { throw new NotImplementedException(); }

        /// <summary>
        /// Gets the name of the value in the enum, or null if the value doesn't have a name.
        /// This is a convenience function, and has undefined results if the same value appears
        /// twice in the enum.
        /// </summary>
        /// <param name="value">The value to lookup.</param>
        /// <returns>The name of one entry in the enum with this value, or null if none exist.</returns>
        virtual public string GetEnumName(int value) { throw new NotImplementedException(); }

        /// <summary>
        /// Attempts to get the integer value for a given enum entry.  Note you should only call this function if
        /// GetEnumElementType returns ELEMENT_TYPE_I4.
        /// </summary>
        /// <param name="name">The name of the value to get (taken from GetEnumNames).</param>
        /// <param name="value">The value to write out.</param>
        /// <returns>True if we successfully filled value, false if 'name' is not a part of the enumeration.</returns>
        virtual public bool TryGetEnumValue(string name, out int value) { throw new NotImplementedException(); }

        /// <summary>
        /// Attempts to get the value for a given enum entry.  The type of "value" can be determined by the
        /// return value of GetEnumElementType.
        /// </summary>
        /// <param name="name">The name of the value to get (taken from GetEnumNames).</param>
        /// <param name="value">The value to write out.</param>
        /// <returns>True if we successfully filled value, false if 'name' is not a part of the enumeration.</returns>
        virtual public bool TryGetEnumValue(string name, out object value) { throw new NotImplementedException(); }

        /// <summary>
        /// Returns true if instances of this type have a simple value.
        /// </summary>
        virtual public bool HasSimpleValue { get { return false; } }

        /// <summary>
        /// Returns the simple value of an instance of this type.  Undefined behavior if HasSimpleValue returns false.
        /// For example ELEMENT_TYPE_I4 is an "int" and the return value of this function would be an int.
        /// </summary>
        /// <param name="address">The address of an instance of this type.</param>
        virtual public object GetValue(Address address) { return null; }

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override string ToString()
        {
            return string.Format("HeapType {0}", Name);
        }
    }

    /// <summary>
    /// A representation of a field in the target process.
    /// </summary>
    public abstract class ClrField
    {
        /// <summary>
        /// The name of the field.
        /// </summary>
        abstract public string Name { get; }

        /// <summary>
        /// The type of the field.  Note this property may return null on error.  There is a bug in several versions
        /// of our debugging layer which causes this.  You should always null-check the return value of this field.
        /// </summary>
        abstract public ClrType Type { get; }

        /// <summary>
        /// Returns the element type of this field.  Note that even when Type is null, this should still tell you
        /// the element type of the field.
        /// </summary>
        abstract public ClrElementType ElementType { get; }

        /// <summary>
        /// Returns true if this field is a primitive (int, float, etc), false otherwise.
        /// </summary>
        /// <returns>True if this field is a primitive (int, float, etc), false otherwise.</returns>
        virtual public bool IsPrimitive() { return ClrRuntime.IsPrimitive(ElementType); }

        /// <summary>
        /// Returns true if this field is a ValueClass (struct), false otherwise.
        /// </summary>
        /// <returns>True if this field is a ValueClass (struct), false otherwise.</returns>
        virtual public bool IsValueClass() { return ClrRuntime.IsValueClass(ElementType); }

        /// <summary>
        /// Returns true if this field is an object reference, false otherwise.
        /// </summary>
        /// <returns>True if this field is an object reference, false otherwise.</returns>
        virtual public bool IsObjectReference() { return ClrRuntime.IsObjectReference(ElementType); }

        /// <summary>
        /// Gets the size of this field.
        /// </summary>
        abstract public int Size { get; }

        /// <summary>
        /// Returns true if this field is public.
        /// </summary>
        abstract public bool IsPublic { get; }

        /// <summary>
        /// Returns true if this field is private.
        /// </summary>
        abstract public bool IsPrivate { get; }

        /// <summary>
        /// Returns true if this field is internal.
        /// </summary>
        abstract public bool IsInternal { get; }

        /// <summary>
        /// Returns true if this field is protected.
        /// </summary>
        abstract public bool IsProtected { get; }

        /// <summary>
        /// Returns true if this field has a simple value (meaning you may call "GetFieldValue" in one of the subtypes
        /// of this class).
        /// </summary>
        abstract public bool HasSimpleValue { get; }

        /// <summary>
        /// If the field has a well defined offset from the base of the object, return it (otherwise -1). 
        /// </summary>
        virtual public int Offset { get { return -1; } }

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override string ToString()
        {
            var type = Type;
            if (type != null)
                return string.Format("{0} {1}", type.Name, Name);

            return Name;
        }
    }

    /// <summary>
    /// Represents an instance field of a type.   Fundamentally it represents a name and a type 
    /// </summary>
    public abstract class ClrInstanceField : ClrField
    {
        /// <summary>
        /// Returns the value of this field.  Equivalent to GetFieldValue(objRef, false).
        /// </summary>
        /// <param name="objRef">The object to get the field value for.</param>
        /// <returns>The value of the field.</returns>
        virtual public object GetFieldValue(Address objRef)
        {
            return GetFieldValue(objRef, false);
        }

        /// <summary>
        /// Returns the value of this field, optionally specifying if this field is
        /// on a value class which is on the interior of another object.
        /// </summary>
        /// <param name="objRef">The object to get the field value for.</param>
        /// <param name="interior">Whether the enclosing type of this field is a value class,
        /// and that value class is embedded in another object.</param>
        /// <returns>The value of the field.</returns>
        abstract public object GetFieldValue(Address objRef, bool interior);


        /// <summary>
        /// Returns the address of the value of this field.  Equivalent to GetFieldAddress(objRef, false).
        /// </summary>
        /// <param name="objRef">The object to get the field address for.</param>
        /// <returns>The value of the field.</returns>
        virtual public Address GetFieldAddress(Address objRef)
        {
            return GetFieldAddress(objRef, false);
        }


        /// <summary>
        /// Returns the address of the value of this field.  Equivalent to GetFieldAddress(objRef, false).
        /// </summary>
        /// <param name="objRef">The object to get the field address for.</param>
        /// <param name="interior">Whether the enclosing type of this field is a value class,
        /// and that value class is embedded in another object.</param>
        /// <returns>The value of the field.</returns>
        abstract public Address GetFieldAddress(Address objRef, bool interior);
    }

    /// <summary>
    /// Represents a static field in the target process.
    /// </summary>
    public abstract class ClrStaticField : ClrField
    {
        /// <summary>
        /// Returns whether this static field has been initialized in a particular AppDomain
        /// or not.  If a static variable has not been initialized, then its class constructor
        /// may have not been run yet.  Calling GetFieldValue on an uninitialized static
        /// will result in returning either NULL or a value of 0.
        /// </summary>
        /// <param name="appDomain">The AppDomain to see if the variable has been initialized.</param>
        /// <returns>True if the field has been initialized (even if initialized to NULL or a default
        /// value), false if the runtime has not initialized this variable.</returns>
        abstract public bool IsInitialized(ClrAppDomain appDomain);

        /// <summary>
        /// Gets the value of the static field.
        /// </summary>
        /// <param name="appDomain">The AppDomain in which to get the value.</param>
        /// <returns>The value of this static field.</returns>
        abstract public object GetFieldValue(ClrAppDomain appDomain);

        /// <summary>
        /// Returns the address of the static field's value in memory.
        /// </summary>
        /// <param name="appDomain">The AppDomain in which to get the field's address.</param>
        /// <returns>The address of the field's value.</returns>
        abstract public Address GetFieldAddress(ClrAppDomain appDomain);

        /// <summary>
        /// Returns true if the static field has a default value (and if we can obtain it).
        /// </summary>
        virtual public bool HasDefaultValue { get { return false; } }

        /// <summary>
        /// The default value of the field.
        /// </summary>
        /// <returns>The default value of the field.</returns>
        virtual public object GetDefaultValue()  { throw new NotImplementedException(); }
    }

    /// <summary>
    /// Represents a thread static value in the target process.
    /// </summary>
    public abstract class ClrThreadStaticField : ClrField
    {
        /// <summary>
        /// Gets the value of the field.
        /// </summary>
        /// <param name="appDomain">The AppDomain in which to get the field's value.</param>
        /// <param name="thread">The thread on which to get the field's value.</param>
        /// <returns>The value of the field.</returns>
        abstract public object GetFieldValue(ClrAppDomain appDomain, ClrThread thread);

        /// <summary>
        /// Gets the address of the field.
        /// </summary>
        /// <param name="appDomain">The AppDomain in which to get the field's address.</param>
        /// <param name="thread">The thread on which to get the field's address.</param>
        /// <returns>The address of the field.</returns>
        abstract public Address GetFieldAddress(ClrAppDomain appDomain, ClrThread thread);
    }

    /// <summary>
    /// A wrapper class for exception objects which help with common tasks for exception objects.
    /// Create this using GCHeap.GetExceptionObject.  You may call that when GCHeapType.IsException
    /// returns true.
    /// </summary>
    public abstract class ClrException
    {
        /// <summary>
        /// Returns the GCHeapType for this exception object.
        /// </summary>
        abstract public ClrType Type { get; }
        
        /// <summary>
        /// Returns the exception message.
        /// </summary>
        abstract public string Message { get; }

        /// <summary>
        /// Returns the address of the exception object.
        /// </summary>
        abstract public Address Address { get; }

        /// <summary>
        /// Returns the inner exception, if one exists, null otherwise.
        /// </summary>
        abstract public ClrException Inner { get; }

        /// <summary>
        /// Returns the HRESULT associated with this exception (or S_OK if there isn't one).
        /// </summary>
        abstract public int HResult { get; }

        /// <summary>
        /// Returns the StackTrace for this exception.  Note that this may be empty or partial depending
        /// on the state of the exception in the process.  (It may have never been thrown or we may be in
        /// the middle of constructing the stackwalk.)  This returns an empty list if no stack trace is
        /// associated with this exception object.
        /// </summary>
        abstract public IList<ClrStackFrame> StackTrace { get; }
    }


    /// <summary>
    /// A GCHeapSegment represents a contiguous region of memory that is devoted to the GC heap. 
    /// Segments.  It has a start and end and knows what heap it belongs to.   Segments can
    /// optional have regions for Gen 0, 1 and 2, and Large properties.  
    /// </summary>
    public abstract class ClrSegment
    {
        /// <summary>
        /// The start address of the segment.  All objects in this segment fall within Start &lt;= object &lt; End.
        /// </summary>
        abstract public Address Start { get; }

        /// <summary>
        /// The end address of the segment.  All objects in this segment fall within Start &lt;= object &lt; End.
        /// </summary>
        abstract public Address End { get; }

        /// <summary>
        /// The number of bytes in the segment.
        /// </summary>
        public ulong Length { get { return (End - Start); } }

        /// <summary>
        /// The GC heap associated with this segment.  There's only one GCHeap per process, so this is
        /// only a convenience method to keep from having to pass the heap along with a segment.
        /// </summary>
        abstract public ClrHeap Heap { get; }

        /// <summary>
        /// The processor that this heap is affinitized with.  In a workstation GC, there is no processor
        /// affinity (and the return value of this property is undefined).  In a server GC each segment
        /// has a logical processor in the PC associated with it.  This property returns that logical
        /// processor number (starting at 0).
        /// </summary>
        abstract public int ProcessorAffinity { get; }

        /// <summary>
        /// The address of the end of memory reserved for the segment, but not committed.
        /// </summary>
        [Obsolete("Use ReservedEnd instead", false)]
        virtual public Address Reserved { get { return ReservedEnd; } }

        /// <summary>
        /// The address of the end of memory committed for the segment (this may be longer than Length).
        /// </summary>
        [Obsolete("Use CommittedEnd instead", false)]
        virtual public Address Committed { get { return CommittedEnd; } }

        /// <summary>
        /// The address of the end of memory reserved for the segment, but not committed.
        /// </summary>
        virtual public Address ReservedEnd { get { return 0; } }

        /// <summary>
        /// The address of the end of memory committed for the segment (this may be longer than Length).
        /// </summary>
        virtual public Address CommittedEnd { get { return 0; } }

        /// <summary>
        /// If it is possible to move from one object to the 'next' object in the segment. 
        /// Then FirstObject returns the first object in the heap (or null if it is not
        /// possible to walk the heap.
        /// </summary>
        virtual public Address FirstObject { get { return 0; } }

        /// <summary>
        /// Given an object on the segment, return the 'next' object in the segment.  Returns
        /// 0 when there are no more objects.   (Or enumeration is not possible)  
        /// </summary>
        virtual public Address NextObject(Address objRef) { return 0; }

        /// <summary>
        /// Returns true if this is a segment for the Large Object Heap.  False otherwise.
        /// Large objects (greater than 85,000 bytes in size), are stored in their own segments and
        /// only collected on full (gen 2) collections. 
        /// </summary>
        virtual public bool Large { get { return false; } }

        /// <summary>
        /// Returns true if this segment is the ephemeral segment (meaning it contains gen0 and gen1
        /// objects).
        /// </summary>
        virtual public bool Ephemeral { get { return false; } }

        /// <summary>
        /// Ephemeral heap sements have geneation 0 and 1 in them.  Gen 1 is always above Gen 2 and
        /// Gen 0 is above Gen 1.  This property tell where Gen 0 start in memory.   Note that
        /// if this is not an Ephemeral segment, then this will return End (which makes Gen 0 empty
        /// for this segment)
        /// </summary>
        virtual public Address Gen0Start { get { return Start; } }

        /// <summary>
        /// The length of the gen0 portion of this segment.
        /// </summary>
        virtual public ulong Gen0Length { get { return Length; } }

        /// <summary>
        /// The start of the gen1 portion of this segment.
        /// </summary>
        virtual public Address Gen1Start { get { return End; } }

        /// <summary>
        /// The length of the gen1 portion of this segment.
        /// </summary>
        virtual public ulong Gen1Length { get { return 0; } }

        /// <summary>
        /// The start of the gen2 portion of this segment.
        /// </summary>
        virtual public Address Gen2Start { get { return End; } }

        /// <summary>
        /// The length of the gen2 portion of this segment.
        /// </summary>
        virtual public ulong Gen2Length { get { return 0; } }

        /// <summary>
        /// Enumerates all objects on the segment.
        /// </summary>
        abstract public IEnumerable<ulong> EnumerateObjects();

        /// <summary>
        /// Returns the generation of an object in this segment.
        /// </summary>
        /// <param name="obj">An object in this segment.</param>
        /// <returns>The generation of the given object if that object lies in this segment.  The return
        ///          value is undefined if the object does not lie in this segment.
        /// </returns>
        virtual public int GetGeneration(Address obj)
        {
            if (Gen0Start <= obj && obj < (Gen0Start + Gen0Length))
            {
                return 0;
            }

            if (Gen1Start <= obj && obj < (Gen1Start + Gen1Length))
            {
                return 1;
            }

            if (Gen2Start <= obj && obj < (Gen2Start + Gen2Length))
            {
                return 2;
            }

            return -1;
        }

        /// <summary>
        /// Returns a string representation of this object.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override string ToString()
        {
            return string.Format("HeapSegment {0:n2}mb [{1:X8}, {2:X8}]", Length / 1000000.0, Start, End);
        }
    }

    /// <summary>
    /// The type of frame the ClrStackFrame represents.
    /// </summary>
    public enum ClrStackFrameType
    {
        /// <summary>
        /// Indicates this stack frame is a standard managed method.
        /// </summary>
        ManagedMethod,

        /// <summary>
        /// Indicates this stack frame is a special stack marker that the Clr runtime leaves on the stack.
        /// Note that the ClrStackFrame may still have a ClrMethod associated with the marker.
        /// </summary>
        Runtime
    }

    /// <summary>
    /// A frame in a managed stack trace.  Note you can call ToString on an instance of this object to get the
    /// function name (or clr!Frame name) similar to SOS's !clrstack output.
    /// </summary>
    public abstract class ClrStackFrame
    {
        /// <summary>
        /// The instruction pointer of this frame.
        /// </summary>
        public abstract Address InstructionPointer { get; }

        /// <summary>
        /// The stack pointer of this frame.
        /// </summary>
        public abstract Address StackPointer { get; }

        /// <summary>
        /// The type of frame (managed or internal).
        /// </summary>
        public abstract ClrStackFrameType Kind { get; }

        /// <summary>
        /// The string to display in a stack trace.  Similar to !clrstack output.
        /// </summary>
        public abstract string DisplayString { get; }

        /// <summary>
        /// Returns the ClrMethod which corresponds to the current stack frame.  This may be null if the
        /// current frame is actually a CLR "Internal Frame" representing a marker on the stack, and that
        /// stack marker does not have a managed method associated with it.
        /// </summary>
        public abstract ClrMethod Method { get; }

        /// <summary>
        /// Returns the source file and line number of the location represented by this stack frame.
        /// This will return null if the location cannot be determined (or the module containing it does
        /// not have PDBs loaded).
        /// </summary>
        /// <returns>The file and line number for this stack frame, null if not found.</returns>
        public abstract SourceLocation GetFileAndLineNumber();
    }

    /// <summary>
    /// A SourceLocation represents a point in the source code.  That is the file and the line number.  
    /// </summary>
    public class SourceLocation
    {
        /// <summary>
        /// The source file for the code
        /// </summary>
        public string FilePath { get { return m_source.sourceFile.fileName; } }
        
        /// <summary>
        /// The line number for the code.
        /// </summary>
        public int LineNumber { get { return (int)m_source.lineNumber; } }

        /// <summary>
        /// The end line number of the location (if multiline this will be different from LineNumber).
        /// </summary>
        public int LineNumberEnd { get { return (int)m_source.lineNumberEnd; } }

        /// <summary>
        /// The start column of the source line.
        /// </summary>
        public int ColStart { get { return (int)m_source.columnNumber; } }

        /// <summary>
        /// The end column of the source line.
        /// </summary>
        public int ColEnd { get { return (int)m_source.columnNumberEnd; } }

        /// <summary>
        /// Generates a human readable form of the source location.
        /// </summary>
        /// <returns>File:Line-Line</returns>
        public override string ToString()
        {
            int line = LineNumber;
            int lineEnd = LineNumberEnd;

            if (line == lineEnd)
                return string.Format("{0}:{1}", FilePath, line);
            else
                return string.Format("{0}:{1}-{2}", FilePath, line, lineEnd);
        }


        #region private
        internal SourceLocation(IDiaLineNumber source)
        {
            m_source = source;
        }

        IDiaLineNumber m_source;
        #endregion
    }

    /// <summary>
    /// Defines the state of the thread from the runtime's perspective.
    /// </summary>
    public enum GcMode
    {
        /// <summary>
        /// In Cooperative mode the thread must cooperate before a GC may proceed.  This means when a GC
        /// starts, the runtime will attempt to suspend the thread at a safepoint but cannot immediately
        /// stop the thread until it synchronizes.
        /// </summary>
        Cooperative,
        /// <summary>
        /// In Preemptive mode the runtime is free to suspend the thread at any time for a GC to occur.
        /// </summary>
        Preemptive
    }

    /// <summary>
    /// Represents a managed thread in the target process.  Note this does not wrap purely native threads
    /// in the target process (that is, threads which have never run managed code before).
    /// </summary>
    public abstract class ClrThread
    {
        /// <summary>
        /// The suspension state of the thread according to the runtime.
        /// </summary>
        public abstract GcMode GcMode { get; }

        /// <summary>
        /// Returns true if this is the finalizer thread.
        /// </summary>
        public abstract bool IsFinalizer { get; }

        /// <summary>
        /// The address of the underlying datastructure which makes up the Thread object.  This
        /// serves as a unique identifier.
        /// </summary>
        public abstract Address Address { get; }

        /// <summary>
        /// Returns true if the thread is alive in the process, false if this thread was recently terminated.
        /// </summary>
        public abstract bool IsAlive { get; }

        /// <summary>
        /// The OS thread id for the thread.
        /// </summary>
        public abstract uint OSThreadId { get; }

        /// <summary>
        /// The managed thread ID (this is equivalent to System.Threading.Thread.ManagedThreadId
        /// in the target process).
        /// </summary>
        public abstract int ManagedThreadId { get; }

        /// <summary>
        /// The AppDomain the thread is running in.
        /// </summary>
        public abstract Address AppDomain { get; }

        /// <summary>
        /// The number of managed locks (Monitors) the thread has currently entered but not left.
        /// This will be highly inconsistent unless the process is stopped.
        /// </summary>
        public abstract uint LockCount { get; }

        /// <summary>
        /// The TEB (thread execution block) address in the process.
        /// </summary>
        public abstract Address Teb { get; }

        /// <summary>
        /// The base of the stack for this thread, or 0 if the value could not be obtained.
        /// </summary>
        public abstract Address StackBase { get; }

        /// <summary>
        /// The limit of the stack for this thread, or 0 if the value could not be obtained.
        /// </summary>
        public abstract Address StackLimit { get; }

        /// <summary>
        /// Enumerates the GC references (objects) on the stack.  This is equivalent to
        /// EnumerateStackObjects(true).
        /// </summary>
        /// <returns>An enumeration of GC references on the stack as the GC sees them.</returns>
        public abstract IEnumerable<ClrRoot> EnumerateStackObjects();

        /// <summary>
        /// Enumerates the GC references (objects) on the stack.
        /// </summary>
        /// <param name="includePossiblyDead">Include all objects found on the stack.  Passing
        /// false attempts to replicate the behavior of the GC, reporting only live objects.</param>
        /// <returns>An enumeration of GC references on the stack as the GC sees them.</returns>
        public abstract IEnumerable<ClrRoot> EnumerateStackObjects(bool includePossiblyDead);

        /// <summary>
        /// Returns the managed stack trace of the thread.
        /// </summary>
        public abstract IList<ClrStackFrame> StackTrace { get; }

        /// <summary>
        /// Returns the exception currently on the thread.  Note that this field may be null.  Also note
        /// that this is basically the "last thrown exception", and may be stale...meaning the thread could
        /// be done processing the exception but a crash dump was taken before the current exception was
        /// cleared off the field.
        /// </summary>
        public abstract ClrException CurrentException { get; }


        /// <summary>
        /// Returns if this thread is a GC thread.  If the runtime is using a server GC, then there will be
        /// dedicated GC threads, which this will indicate.  For a runtime using the workstation GC, this flag
        /// will only be true for a thread which is currently running a GC (and the background GC thread).
        /// </summary>
        public abstract bool IsGC { get; }
        
        /// <summary>
        /// Returns if this thread is the debugger helper thread.
        /// </summary>
        public abstract bool IsDebuggerHelper { get; }

        /// <summary>
        /// Returns true if this thread is a threadpool timer thread.
        /// </summary>
        public abstract bool IsThreadpoolTimer { get; }

        /// <summary>
        /// Returns true if this thread is a threadpool IO completion port.
        /// </summary>
        public abstract bool IsThreadpoolCompletionPort { get; }
        
        /// <summary>
        /// Returns true if this is a threadpool worker thread.
        /// </summary>
        public abstract bool IsThreadpoolWorker { get; }

        /// <summary>
        /// Returns true if this is a threadpool wait thread.
        /// </summary>
        public abstract bool IsThreadpoolWait { get; }
        
        /// <summary>
        /// Returns true if this is the threadpool gate thread.
        /// </summary>
        public abstract bool IsThreadpoolGate { get; }

        /// <summary>
        /// Returns if this thread currently suspending the runtime.
        /// </summary>
        public abstract bool IsSuspendingEE { get; }

        /// <summary>
        /// Returns true if this thread is currently the thread shutting down the runtime.
        /// </summary>
        public abstract bool IsShutdownHelper { get; }

        /// <summary>
        /// Returns true if an abort was requested for this thread (such as Thread.Abort, or AppDomain unload).
        /// </summary>
        public abstract bool IsAbortRequested { get; }

        /// <summary>
        /// Returns true if this thread was aborted.
        /// </summary>
        public abstract bool IsAborted { get; }

        /// <summary>
        /// Returns true if the GC is attempting to suspend this thread.
        /// </summary>
        public abstract bool IsGCSuspendPending { get; }

        /// <summary>
        /// Returns true if the user has suspended the thread (using Thread.Suspend).
        /// </summary>
        public abstract bool IsUserSuspended { get; }

        /// <summary>
        /// Returns true if the debugger has suspended the thread.
        /// </summary>
        public abstract bool IsDebugSuspended { get; }

        /// <summary>
        /// Returns true if this thread is a background thread.  (That is, if the thread does not keep the
        /// managed execution environment alive and running.)
        /// </summary>
        public abstract bool IsBackground { get; }

        /// <summary>
        /// Returns true if this thread was created, but not started.
        /// </summary>
        public abstract bool IsUnstarted { get; }

        /// <summary>
        /// Returns true if the Clr runtime called CoIntialize for this thread.
        /// </summary>
        public abstract bool IsCoInitialized { get; }

        /// <summary>
        /// Returns true if this thread is in a COM single threaded apartment.
        /// </summary>
        public abstract bool IsSTA { get; }

        /// <summary>
        /// Returns true if the thread is a COM multithreaded apartment.
        /// </summary>
        public abstract bool IsMTA { get; }

        /// <summary>
        /// Returns the object this thread is blocked waiting on, or null if the thread is not blocked.
        /// </summary>
        public abstract IList<BlockingObject> BlockingObjects { get; }
    }

    /// <summary>
    /// Every thread which is blocking on an object specifies why the object is waiting.
    /// </summary>
    public enum BlockingReason
    {
        /// <summary>
        /// Object is not locked.
        /// </summary>
        None,

        /// <summary>
        /// Not able to determine why the object is blocking.
        /// </summary>
        Unknown,

        /// <summary>
        /// The thread is waiting for a Mutex or Semaphore (such as Monitor.Enter, lock(obj), etc).
        /// </summary>
        Monitor,

        /// <summary>
        /// The thread is waiting for a mutex with Monitor.Wait.
        /// </summary>
        MonitorWait,

        /// <summary>
        /// The thread is waiting for an event (ManualResetEvent.WaitOne, AutoResetEvent.WaitOne).
        /// </summary>
        WaitOne,

        /// <summary>
        /// The thread is waiting in WaitHandle.WaitAll.
        /// </summary>
        WaitAll,

        /// <summary>
        /// The thread is waiting in WaitHandle.WaitAny.
        /// </summary>
        WaitAny,

        /// <summary>
        /// The thread is blocked on a call to Thread.Join.
        /// </summary>
        ThreadJoin,

        /// <summary>
        /// ReaderWriterLock, reader lock is taken.
        /// </summary>
        ReaderAcquired,


        /// <summary>
        /// ReaderWriterLock, writer lock is taken.
        /// </summary>
        WriterAcquired
    }

    /// <summary>
    /// Represents a managed module in the target process.
    /// </summary>
    public abstract class ClrModule
    {
        /// <summary>
        /// Returns true if ClrMD has loaded the the PDB for this module into memory.
        /// </summary>
        public abstract bool IsPdbLoaded { get; }

        /// <summary>
        /// Determines whether a PDB on disk matches this module.  (Note that TryDownloadPdb
        /// always provides a matching PDB if it finds one, so you do not need to check pdbs
        /// downloaded with TryDownloadPdb with this function.)
        /// </summary>
        /// <param name="pdbPath">The location of the PDB on disk.</param>
        /// <returns>True if the pdb matches, false otherwise.</returns>
        public abstract bool IsMatchingPdb(string pdbPath);

        /// <summary>
        /// Loads the pdb for this module.
        /// </summary>
        /// <param name="path">The path to the PDB on disk.</param>
        public abstract void LoadPdb(string path);

        /// <summary>
        /// Attempts to download the PDB for this module from the symbol server.
        /// </summary>
        /// <param name="notification">A notification callback (null is ok).</param>
        /// <returns>The path on disk of the downloaded PDB, or null if not found.</returns>
        public abstract string TryDownloadPdb(ISymbolNotification notification);

        /// <summary>
        /// Gets the source location of a given metadata token for a function and offset.
        /// </summary>
        /// <param name="mdMethodToken">A method def token (ClrMethod.MetadataToken).</param>
        /// <param name="ilOffset">The il offset to look up the source information.</param>
        /// <returns>The SourceLocation for the given IL offset, or null if no mapping exists.</returns>
        public abstract SourceLocation GetSourceInformation(uint mdMethodToken, int ilOffset);

        /// <summary>
        /// Gets the source location of a given metadata token for a function and offset.
        /// </summary>
        /// <param name="method">The method to look up the source information.</param>
        /// <param name="ilOffset">The il offset to look up the source information.</param>
        /// <returns>The SourceLocation for the given IL offset, or null if no mapping exists.</returns>
        public abstract SourceLocation GetSourceInformation(ClrMethod method, int ilOffset);

        /// <summary>
        /// Returns the name of the assembly that this module is defined in.
        /// </summary>
        public abstract string AssemblyName { get; }

        /// <summary>
        /// Returns an identifier to uniquely represent this assembly.  This value is not used by any other
        /// function in ClrMD, but can be used to group modules by their assembly.  (Do not use AssemblyName
        /// for this, as reflection and other special assemblies can share the same name, but actually be
        /// different.)
        /// </summary>
        public abstract ulong AssemblyId { get; }

        /// <summary>
        /// Returns the name of the module.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Returns true if this module was created through Reflection.Emit (and thus has no associated
        /// file).
        /// </summary>
        public abstract bool IsDynamic { get; }

        /// <summary>
        /// Returns true if this module is an actual PEFile on disk.
        /// </summary>
        public abstract bool IsFile { get; }

        /// <summary>
        /// Returns the filename of where the module was loaded from on disk.  Undefined results if
        /// IsPEFile returns false.
        /// </summary>
        public abstract string FileName { get; }

        /// <summary>
        /// Returns the base of the image loaded into memory.  This may be 0 if there is not a physical
        /// file backing it.
        /// </summary>
        public abstract Address ImageBase { get; }

        /// <summary>
        /// Returns the size of the image in memory.
        /// </summary>
        public abstract ulong Size { get; }

        /// <summary>
        /// Enumerate all types defined by this module.
        /// </summary>
        public abstract IEnumerable<ClrType> EnumerateTypes();

        /// <summary>
        /// The location of metadata for this module in the process's memory.  This is useful if you
        /// need to manually create IMetaData* objects.
        /// </summary>
        public abstract ulong MetadataAddress { get; }

        /// <summary>
        /// The length of the metadata for this module.
        /// </summary>
        public abstract ulong MetadataLength { get; }

        /// <summary>
        /// The IMetaDataImport interface for this module.  Note that this API does not provide a
        /// wrapper for IMetaDataImport.  You will need to wrap the API yourself if you need to use this.
        /// </summary>
        public abstract object MetadataImport { get; }

        /// <summary>
        /// The debugging attributes for this module.
        /// </summary>
        public abstract DebuggableAttribute.DebuggingModes DebuggingMode { get; }

        /// <summary>
        /// Attempts to obtain a ClrType based on the name of the type.  Note this is a "best effort" due to
        /// the way that the dac handles types.  This function will fail for Generics, and types which have
        /// never been constructed in the target process.  Please be sure to null-check the return value of
        /// this function.
        /// </summary>
        /// <param name="name">The name of the type.  (This would be the EXACT value returned by ClrType.Name.</param>
        /// <returns>The requested ClrType, or null if the type doesn't exist or couldn't be constructed.</returns>
        public abstract ClrType GetTypeByName(string name);

        /// <summary>
        /// Returns a name for the assembly.
        /// </summary>
        /// <returns>A name for the assembly.</returns>
        public override string ToString()
        {
            if (string.IsNullOrEmpty(Name))
            {
                if (!string.IsNullOrEmpty(AssemblyName))
                    return AssemblyName;

                if (IsDynamic)
                    return "dynamic";
            }

            return Name;
        }
    }

    /// <summary>
    /// Represents an AppDomain in the target runtime.
    /// </summary>
    public abstract class ClrAppDomain
    {
        /// <summary>
        /// Address of the AppDomain.
        /// </summary>
        public abstract Address Address { get; }

        /// <summary>
        /// The AppDomain's ID.
        /// </summary>
        public abstract int Id { get; }

        /// <summary>
        /// The name of the AppDomain, as specified when the domain was created.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Returns a list of modules loaded into this AppDomain.
        /// </summary>
        public abstract IList<ClrModule> Modules { get; }

        /// <summary>
        /// Returns the config file used for the AppDomain.  This may be null if there was no config file
        /// loaded, or if the targeted runtime does not support enumerating that data.
        /// </summary>
        public abstract string ConfigurationFile { get; }

        /// <summary>
        /// Returns the base directory for this AppDomain.  This may return null if the targeted runtime does
        /// not support enumerating this information.
        /// </summary>
        public abstract string AppBase { get; }
    }

    /// <summary>
    /// The COM implementation details of a single CCW entry.
    /// </summary>
    public abstract class ComInterfaceData
    {
        /// <summary>
        /// The CLR type this represents.
        /// </summary>
        public abstract ClrType Type { get; }

        /// <summary>
        /// The interface pointer of Type.
        /// </summary>
        public abstract Address InterfacePointer { get; }
    }

    /// <summary>
    /// Helper for Com Callable Wrapper objects.  (CCWs are CLR objects exposed to native code as COM
    /// objects).
    /// </summary>
    public abstract class CcwData
    {
        /// <summary>
        /// Returns the pointer to the IUnknown representing this CCW.
        /// </summary>
        public abstract Address IUnknown { get; }

        /// <summary>
        /// Returns the pointer to the managed object representing this CCW.
        /// </summary>
        public abstract Address Object { get; }

        /// <summary>
        /// Returns the CLR handle associated with this CCW.
        /// </summary>
        public abstract Address Handle { get; }

        /// <summary>
        /// Returns the refcount of this CCW.
        /// </summary>
        public abstract int RefCount { get; }

        /// <summary>
        /// Returns the interfaces that this CCW implements.
        /// </summary>
        public abstract IList<ComInterfaceData> Interfaces { get; }
    }

    /// <summary>
    /// Helper for Runtime Callable Wrapper objects.  (RCWs are COM objects which are exposed to the runtime
    /// as managed objects.)
    /// </summary>
    public abstract class RcwData
    {
        /// <summary>
        /// Returns the pointer to the IUnknown representing this CCW.
        /// </summary>
        public abstract Address IUnknown { get; }

        /// <summary>
        /// Returns the external VTable associated with this RCW.  (It's useful to resolve the VTable as a symbol
        /// which will tell you what the underlying native type is...if you have the symbols for it loaded).
        /// </summary>
        public abstract Address VTablePointer { get; }

        /// <summary>
        /// Returns the RefCount of the RCW.
        /// </summary>
        public abstract int RefCount { get; }

        /// <summary>
        /// Returns the managed object associated with this of RCW.
        /// </summary>
        public abstract Address Object { get; }

        /// <summary>
        /// Returns true if the RCW is disconnected from the underlying COM type.
        /// </summary>
        public abstract bool Disconnected { get; }

        /// <summary>
        /// Returns the thread which created this RCW.
        /// </summary>
        public abstract uint CreatorThread { get; }

        /// <summary>
        /// Returns the internal WinRT object associated with this RCW (if one exists).
        /// </summary>
        public abstract ulong WinRTObject { get; }

        /// <summary>
        /// Returns the list of interfaces this RCW implements.
        /// </summary>
        public abstract IList<ComInterfaceData> Interfaces { get; }
    }

    /// <summary>
    /// Represents the version of a DLL.
    /// </summary>
    public struct VersionInfo
    {
        /// <summary>
        /// In a version 'A.B.C.D', this field represents 'A'.
        /// </summary>
        public int Major;

        /// <summary>
        /// In a version 'A.B.C.D', this field represents 'B'.
        /// </summary>
        public int Minor;

        /// <summary>
        /// In a version 'A.B.C.D', this field represents 'C'.
        /// </summary>
        public int Revision;

        /// <summary>
        /// In a version 'A.B.C.D', this field represents 'D'.
        /// </summary>
        public int Patch;

        internal VersionInfo(int major, int minor, int revision, int patch)
        {
            Major = major;
            Minor = minor;
            Revision = revision;
            Patch = patch;
        }

        /// <summary>
        /// To string.
        /// </summary>
        /// <returns>The A.B.C.D version prepended with 'v'.</returns>
        public override string ToString()
        {
            return string.Format("v{0}.{1}.{2}.{3:D2}", Major, Minor, Revision, Patch);
        }
    }

    /// <summary>
    /// Returns the "flavor" of CLR this module represents.
    /// </summary>
    public enum ClrFlavor
    {
        /// <summary>
        /// This is the full version of CLR included with windows.
        /// </summary>
        Desktop,

        /// <summary>
        /// This is a reduced CLR used in other projects.
        /// </summary>
        CoreCLR,

#if _REDHAWK
        Redhawk
#endif
    }

    /// <summary>
    /// Represents information about a single Clr runtime in a process.
    /// </summary>
    [Serializable]
    public class ClrInfo : IComparable
    {
        /// <summary>
        /// The version number of this runtime.
        /// </summary>
        public VersionInfo Version { get; set; }

        /// <summary>
        /// The type of CLR this module represents.
        /// </summary>
        public ClrFlavor Flavor { get; set; }

        /// <summary>
        /// Returns module information about the Dac needed create a ClrRuntime instance for this runtime.
        /// </summary>
        public ModuleInfo DacInfo { get; set; }

        /// <summary>
        /// The location of the Dac on the local machine, if a matching Dac could be found.
        /// If this returns null it means that no matching Dac could be found, and you will
        /// need to make a symbol server request using DacInfo.
        /// </summary>
        public string TryGetDacLocation()
        {
            return m_dacLocation;
        }

        /// <summary>
        /// Attemps to download the matching dac for this runtime from the symbol server.  Note that this command
        /// does not attempt to inspect or parse _NT_SYMBOL_PATH, so if you want to use that as a "default", you
        /// need to add that path to the sympath parameter manually.  This function will return a local dac location
        /// (and bypass the symbol server) if a matching dac exists locally on your computer.
        /// </summary>
        /// <param name="notification">A notification callback (null ok).</param>
        /// <returns>The local path (in the cache) of the dac if found, null otherwise.</returns>
        public string TryDownloadDac(ISymbolNotification notification)
        {
            if (m_dacLocation != null)
            {
                if (notification != null)
                    notification.FoundSymbolInCache(m_dacLocation);

                return m_dacLocation;
            }

            return m_dataTarget.TryDownloadFile(DacInfo.FileName, (int)DacInfo.TimeStamp, (int)DacInfo.FileSize, notification);
        }

        /// <summary>
        /// Attemps to download the matching dac for this runtime from the symbol server.  Note that this command
        /// does not attempt to inspect or parse _NT_SYMBOL_PATH, so if you want to use that as a "default", you
        /// need to add that path to the sympath parameter manually.  This function will return a local dac location
        /// (and bypass the symbol server) if a matching dac exists locally on your computer.
        /// </summary>
        /// <returns>The local path (in the cache) of the dac if found, null otherwise.</returns>
        public string TryDownloadDac()
        {
            return TryDownloadDac(null);
        }


        /// <summary>
        /// To string.
        /// </summary>
        /// <returns>A version string for this Clr runtime.</returns>
        public override string ToString()
        {
            return Version.ToString();
        }

        internal ClrInfo(DataTarget dt, ClrFlavor flavor, VersionInfo clrVersion, ModuleInfo dacInfo, string dacLocation)
        {
            Debug.Assert(dacInfo != null);

            Flavor = flavor;
            DacInfo = dacInfo;
            Version = clrVersion;
            m_dataTarget = dt;
            m_dacLocation = dacLocation;
        }

        internal ClrInfo()
        {
        }

        string m_dacLocation;
        DataTarget m_dataTarget;

        /// <summary>
        /// IComparable.  Sorts the object by version.
        /// </summary>
        /// <param name="obj">The object to compare to.</param>
        /// <returns>-1 if less, 0 if equal, 1 if greater.</returns>
        public int CompareTo(object obj)
        {
            if (obj == null)
                return 1;

            if (!(obj is ClrInfo))
                throw new InvalidOperationException("Object not ClrInfo.");

            VersionInfo rhs = ((ClrInfo)obj).Version;
            if (Version.Major != rhs.Major)
                return Version.Major.CompareTo(rhs.Major);


            if (Version.Minor != rhs.Minor)
                return Version.Minor.CompareTo(rhs.Minor);


            if (Version.Revision != rhs.Revision)
                return Version.Revision.CompareTo(rhs.Revision);

            return Version.Patch.CompareTo(rhs.Patch);
        }
    }

    /// <summary>
    /// Specifies how to attach to a live process.
    /// </summary>
    public enum AttachFlag
    {
        /// <summary>
        /// Performs an invasive debugger attach.  Allows the consumer of this API to control the target
        /// process through normal IDebug function calls.  The process will be paused.
        /// </summary>
        Invasive,

        /// <summary>
        /// Performs a non-invasive debugger attach.  The process will be paused by this attached (and
        /// for the duration of the attach) but the caller cannot control the target process.  This is
        /// useful when there's already a debugger attached to the process.
        /// </summary>
        NonInvasive,

        /// <summary>
        /// Performs a "passive" attach, meaning no debugger is actually attached to the target process.
        /// The process is not paused, so queries for quickly changing data (such as the contents of the
        /// GC heap or callstacks) will be highly inconsistent unless the user pauses the process through
        /// other means.  Useful when attaching with ICorDebug (managed debugger), as you cannot use a
        /// non-invasive attach with ICorDebug.
        /// </summary>
        Passive
    }

    /// <summary>
    /// Information about a specific PDB instance obtained from a PE image.
    /// </summary>
    public class PdbInfo
    {
        /// <summary>
        /// The Guid of the PDB.
        /// </summary>
        public Guid Guid { get; set; }

        /// <summary>
        /// The pdb revision.
        /// </summary>
        public int Revision { get; set; }

        /// <summary>
        /// The filename of the pdb.
        /// </summary>
        public string FileName { get; set; }
    }

    /// <summary>
    /// Provides information about loaded modules in a DataTarget
    /// </summary>
    public class ModuleInfo
    {
        /// <summary>
        /// The base address of the object.
        /// </summary>
        public virtual ulong ImageBase { get; set; }

        /// <summary>
        /// The filesize of the image.
        /// </summary>
        public virtual uint FileSize { get; set; }

        /// <summary>
        /// The build timestamp of the image.
        /// </summary>
        public virtual uint TimeStamp { get; set; }

        /// <summary>
        /// The filename of the module on disk.
        /// </summary>
        public virtual string FileName { get; set; }

        /// <summary>
        /// The PDB associated with this module.
        /// </summary>
        public virtual PdbInfo Pdb
        {
            get
            {
                if (m_pdb != null || m_dataReader == null)
                    return m_pdb;

                PdbInfo pdb = null;

                try
                {
                    PEFile file = new PEFile(new ReadVirtualStream(m_dataReader, (long)ImageBase, (long)FileSize), true);

                    string pdbName;
                    Guid guid;
                    int age;
                    if (file.GetPdbSignature(out pdbName, out guid, out age))
                    {
                        pdb = new PdbInfo();
                        pdb.FileName = pdbName;
                        pdb.Guid = guid;
                        pdb.Revision = age;
                    }
                }
                catch
                {
                    return null;
                }

                m_pdb = pdb;
                return m_pdb;
            }

            set
            {
                m_pdb = value;
            }
        }

        /// <summary>
        /// The version information for this file.
        /// </summary>
        public virtual VersionInfo Version
        {
            get
            {
                if (m_versionInit || m_dataReader == null)
                    return m_version;

                m_dataReader.GetVersionInfo(ImageBase, out m_version);
                m_versionInit = true;
                return m_version;
            }

            set
            {
                m_version = value;
                m_versionInit = true;
            }
        }

        /// <summary>
        /// To string.
        /// </summary>
        /// <returns>The filename of the module.</returns>
        public override string ToString()
        {
            return FileName;
        }

        /// <summary>
        /// Creates a ModuleInfo object with an IDataReader instance.  This is used when
        /// lazily evaluating VersionInfo. 
        /// </summary>
        /// <param name="reader"></param>
        public ModuleInfo(IDataReader reader)
        {
            m_dataReader = reader;
        }


        /// <summary>
        /// Empty constructor for serialization.
        /// </summary>
        public ModuleInfo()
        {
        }

        IDataReader m_dataReader;
        PdbInfo m_pdb;
        VersionInfo m_version;
        bool m_versionInit;
    }

    /// <summary>
    /// The result of a VirtualQuery.
    /// </summary>
    public struct VirtualQueryData
    {
        /// <summary>
        /// The base address of the allocation.
        /// </summary>
        public ulong BaseAddress;

        /// <summary>
        ///  The size of the allocation.
        /// </summary>
        public ulong Size;


        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="addr">Base address of the memory range.</param>
        /// <param name="size">The size of the memory range.</param>
        public VirtualQueryData(ulong addr, ulong size)
        {
            BaseAddress = addr;
            Size = size;
        }
    }

    /// <summary>
    /// The result of an asynchronous memory read.  This is returned by an IDataReader
    /// when an async memory read is requested.
    /// </summary>
    public class AsyncMemoryReadResult
    {
        /// <summary>
        /// A wait handle which is signaled when the read operation is complete.
        /// Complete must be assigned a valid EventWaitHandle before this object is
        /// returned by ReadMemoryAsync, and Complete must be signaled after the
        /// request is completed.
        /// </summary>
        public virtual EventWaitHandle Complete { get; set; }

        /// <summary>
        /// The address to read from.  Address must be assigned to before this objct is
        /// returned by ReadMemoryAsync.
        /// </summary>
        public virtual ulong Address { get; set; }

        /// <summary>
        /// The number of bytes requested in this async read.  BytesRequested must be
        /// assigned to before this objct is returned by ReadMemoryAsync.
        /// </summary>
        public virtual int BytesRequested { get; set; }

        /// <summary>
        /// The actual number of bytes read out of the data target.  This must be
        /// assigned to before Complete is signaled.
        /// </summary>
        public virtual int BytesRead { get { return m_read; } set { m_read = value; } }

        /// <summary>
        /// The result of the memory read.  This must be assigned to before Complete is
        /// signaled.
        /// </summary>
        public virtual byte[] Result { get { return m_result; } set { m_result = value; } }

        /// <summary>
        /// Empty constructor, no properties/fields assigned.
        /// </summary>
        public AsyncMemoryReadResult()
        {
        }
        
        /// <summary>
        /// Constructor.  Assigns Address, BytesRequested, and Complete.  (Uses a ManualResetEvent
        /// for Complete).
        /// </summary>
        /// <param name="addr">The address of the memory read.</param>
        /// <param name="requested">The number of bytes requested.</param>
        public AsyncMemoryReadResult(ulong addr, int requested)
        {
            Address = addr;
            BytesRequested = requested;
            Complete = new ManualResetEvent(false);
        }

        /// <summary>
        /// To string.
        /// </summary>
        /// <returns>The memory range requested.</returns>
        public override string ToString()
        {
            return string.Format("[{0:x}, {1:x}]", Address, Address + (uint)BytesRequested);
        }

        /// <summary>
        /// The amount read, backing variable for BytesRead.
        /// </summary>
        protected volatile int m_read;

        /// <summary>
        /// The actual data buffer, backing variable for Result.
        /// </summary>
        protected volatile byte[] m_result;
    }

    /// <summary>
    /// An interface for reading data out of the target process.
    /// </summary>
    public interface IDataReader
    {
        /// <summary>
        /// Called when the DataTarget is closing (Disposing).  Used to clean up resources.
        /// </summary>
        void Close();

        /// <summary>
        /// Informs the data reader that the user has requested all data be flushed.
        /// </summary>
        void Flush();

        /// <summary>
        /// Gets the architecture of the target.
        /// </summary>
        /// <returns>The architecture of the target.</returns>
        Architecture GetArchitecture();

        /// <summary>
        /// Gets the size of a pointer in the target process.
        /// </summary>
        /// <returns>The pointer size of the target process.</returns>
        uint GetPointerSize();

        /// <summary>
        /// Enumerates modules in the target process.
        /// </summary>
        /// <returns>A list of the modules in the target process.</returns>
        IList<ModuleInfo> EnumerateModules();

        /// <summary>
        /// Gets the version information for a given module (given by the base address of the module).
        /// </summary>
        /// <param name="baseAddress">The base address of the module to look up.</param>
        /// <param name="version">The version info for the given module.</param>
        void GetVersionInfo(ulong baseAddress, out VersionInfo version);

        /// <summary>
        /// Read memory out of the target process.
        /// </summary>
        /// <param name="address">The address of memory to read.</param>
        /// <param name="buffer">The buffer to write to.</param>
        /// <param name="bytesRequested">The number of bytes to read.</param>
        /// <param name="bytesRead">The number of bytes actually read out of the target process.</param>
        /// <returns>True if any bytes were read at all, false if the read failed (and no bytes were read).</returns>
        bool ReadMemory(ulong address, byte[] buffer, int bytesRequested, out int bytesRead);

        /// <summary>
        /// Read memory out of the target process.
        /// </summary>
        /// <param name="address">The address of memory to read.</param>
        /// <param name="buffer">The buffer to write to.</param>
        /// <param name="bytesRequested">The number of bytes to read.</param>
        /// <param name="bytesRead">The number of bytes actually read out of the target process.</param>
        /// <returns>True if any bytes were read at all, false if the read failed (and no bytes were read).</returns>
        bool ReadMemory(ulong address, IntPtr buffer, int bytesRequested, out int bytesRead);

        /// <summary>
        /// Returns true if this data reader can read data out of the target process asynchronously.
        /// </summary>
        bool CanReadAsync { get; }

        /// <summary>
        /// Reads memory from the target process asynchronously.  Only called if CanReadAsync returns true.
        /// </summary>
        /// <param name="address">The address of memory to read.</param>
        /// <param name="bytesRequested">The number of bytes to read.</param>
        /// <returns>A data structure containing an event to wait for as well as a new byte array to read from.</returns>
        AsyncMemoryReadResult ReadMemoryAsync(ulong address, int bytesRequested);

        /// <summary>
        /// Returns true if the data target is a minidump (or otherwise may not contain full heap data).
        /// </summary>
        /// <returns>True if the data target is a minidump (or otherwise may not contain full heap data).</returns>
        bool IsMinidump { get; }

        /// <summary>
        /// Gets the TEB of the specified thread.
        /// </summary>
        /// <param name="thread">The OS thread ID to get the TEB for.</param>
        /// <returns>The address of the thread's teb.</returns>
        ulong GetThreadTeb(uint thread);

        /// <summary>
        /// Enumerates the OS thread ID of all threads in the process.
        /// </summary>
        /// <returns>An enumeration of all threads in the target process.</returns>
        IEnumerable<uint> EnumerateAllThreads();

        /// <summary>
        /// Gets information about the given memory range.
        /// </summary>
        /// <param name="addr">An arbitrary address in the target process.</param>
        /// <param name="vq">The base address and size of the allocation.</param>
        /// <returns>True if the address was found and vq was filled, false if the address is not valid memory.</returns>
        bool VirtualQuery(ulong addr, out VirtualQueryData vq);

        /// <summary>
        /// Gets the thread context for the given thread.
        /// </summary>
        /// <param name="threadID">The OS thread ID to read the context from.</param>
        /// <param name="contextFlags">The requested context flags, or 0 for default flags.</param>
        /// <param name="contextSize">The size (in bytes) of the context parameter.</param>
        /// <param name="context">A pointer to the buffer to write to.</param>
        bool GetThreadContext(uint threadID, uint contextFlags, uint contextSize, IntPtr context);

        /// <summary>
        /// Gets the thread context for the given thread.
        /// </summary>
        /// <param name="threadID">The OS thread ID to read the context from.</param>
        /// <param name="contextFlags">The requested context flags, or 0 for default flags.</param>
        /// <param name="contextSize">The size (in bytes) of the context parameter.</param>
        /// <param name="context">A pointer to the buffer to write to.</param>
        bool GetThreadContext(uint threadID, uint contextFlags, uint contextSize, byte[] context);

        /// <summary>
        /// Read a pointer out of the target process.
        /// </summary>
        /// <returns>The pointer at the give address, or 0 if that pointer doesn't exist in
        /// the data target.</returns>
        ulong ReadPointerUnsafe(ulong addr);

        /// <summary>
        /// Read an int out of the target process.
        /// </summary>
        /// <returns>The int at the give address, or 0 if that pointer doesn't exist in
        /// the data target.</returns>
        uint ReadDwordUnsafe(ulong addr);
    }

    /// <summary>
    /// The type of crash dump reader to use.
    /// </summary>
    public enum CrashDumpReader
    {
        /// <summary>
        /// Use DbgEng.  This allows the user to obtain an instance of IDebugClient through the
        /// DataTarget.DebuggerInterface property, at the cost of strict threading requirements.
        /// </summary>
        DbgEng,

        /// <summary>
        /// Use a simple dump reader to read data out of the crash dump.  This allows processing
        /// multiple dumps (using separate DataTargets) on multiple threads, but the
        /// DataTarget.DebuggerInterface property will return null.
        /// </summary>
        ClrMD
    }

    /// <summary>
    /// A crash dump or live process to read out of.
    /// </summary>
    public abstract class DataTarget : IDisposable
    {
        /// <summary>
        /// Creates a DataTarget from a crash dump.
        /// </summary>
        /// <param name="fileName">The crash dump's filename.</param>
        /// <returns>A DataTarget instance.</returns>
        public static DataTarget LoadCrashDump(string fileName)
        {
            DbgEngDataReader reader = new DbgEngDataReader(fileName);
            return CreateFromReader(reader, reader.DebuggerInterface);
        }


        /// <summary>
        /// Creates a DataTarget from a crash dump, specifying the dump reader to use.
        /// </summary>
        /// <param name="fileName">The crash dump's filename.</param>
        /// <param name="dumpReader">The type of dump reader to use.</param>
        /// <returns>A DataTarget instance.</returns>
        public static DataTarget LoadCrashDump(string fileName, CrashDumpReader dumpReader)
        {
            if (dumpReader == CrashDumpReader.DbgEng)
            {
                DbgEngDataReader reader = new DbgEngDataReader(fileName);
                return CreateFromReader(reader, reader.DebuggerInterface);
            }
            else
            {
                DumpDataReader reader = new DumpDataReader(fileName);
                return CreateFromReader(reader, null);
            }
        }

        /// <summary>
        /// Create an instance of DataTarget from a user defined DataReader
        /// </summary>
        /// <param name="reader">A user defined DataReader.</param>
        /// <returns>A new DataTarget instance.</returns>
        public static DataTarget CreateFromDataReader(IDataReader reader)
        {
            return CreateFromReader(reader, null);
        }

        static DataTarget CreateFromReader(IDataReader reader, Interop.IDebugClient client)
        {
#if _TRACING
            reader = new TraceDataReader(reader);
#endif
            return new DataTargetImpl(reader, client);
        }

        /// <summary>
        /// Creates a data target from an existing IDebugClient interface.  If you created and attached
        /// a dbgeng based debugger to a process you may pass the IDebugClient RCW object to this function
        /// to create the DataTarget.
        /// </summary>
        /// <param name="client">The dbgeng IDebugClient object.  We will query interface on this for IDebugClient.</param>
        /// <returns>A DataTarget instance.</returns>
        public static DataTarget CreateFromDebuggerInterface(Microsoft.Diagnostics.Runtime.Interop.IDebugClient client)
        {
            DbgEngDataReader reader = new DbgEngDataReader(client);
            DataTargetImpl dataTarget = new DataTargetImpl(reader, reader.DebuggerInterface);

            return dataTarget;
        }

        /// <summary>
        /// Invasively attaches to a live process.
        /// </summary>
        /// <param name="pid">The process ID of the process to attach to.</param>
        /// <param name="msecTimeout">Timeout in milliseconds.</param>
        /// <returns>A DataTarget instance.</returns>
        public static DataTarget AttachToProcess(int pid, uint msecTimeout)
        {
            return AttachToProcess(pid, msecTimeout, AttachFlag.Invasive);
        }

        /// <summary>
        /// Attaches to a live process.
        /// </summary>
        /// <param name="pid">The process ID of the process to attach to.</param>
        /// <param name="msecTimeout">Timeout in milliseconds.</param>
        /// <param name="attachFlag">The type of attach requested for the target process.</param>
        /// <returns>A DataTarget instance.</returns>
        public static DataTarget AttachToProcess(int pid, uint msecTimeout, AttachFlag attachFlag)
        {
            Microsoft.Diagnostics.Runtime.Interop.IDebugClient client = null;
            IDataReader reader;
            if (attachFlag == AttachFlag.Passive)
            {
                reader = new LiveDataReader(pid);
            }
            else
            {
                var dbgeng = new DbgEngDataReader(pid, attachFlag, msecTimeout);
                reader = dbgeng;
                client = dbgeng.DebuggerInterface;
            }

            DataTargetImpl dataTarget = new DataTargetImpl(reader, client);
            return dataTarget;
        }


        internal SymPath m_symPath;
        SymbolReader m_symbolReader;
        internal SymbolReader SymbolReader
        {
            get
            {
                if (m_symbolReader == null)
                    m_symbolReader = new SymbolReader(TextWriter.Null, m_symPath);

                return m_symbolReader;
            }
        }

        /// <summary>
        /// The ISymbolNotification to use if none is specified.
        /// </summary>
        public ISymbolNotification DefaultSymbolNotification { get; set; }

        /// <summary>
        /// Returns true if the target process is a minidump, or otherwise might have limited memory.  If IsMinidump
        /// returns true, a greater range of functions may fail to return data due to the data not being present in
        /// the application/crash dump you are debugging.
        /// </summary>
        public abstract bool IsMinidump { get; }

        /// <summary>
        /// Sets the symbol path for ClrMD.
        /// </summary>
        /// <param name="path">This should be in the format that Windbg/dbgeng expects with the '.sympath' command.</param>
        public abstract void SetSymbolPath(string path);

        /// <summary>
        /// Clears the symbol path.
        /// </summary>
        public abstract void ClearSymbolPath();

        /// <summary>
        /// Appends 'path' to the symbol path.
        /// </summary>
        /// <param name="path">The location to add.</param>
        public abstract void AppendSymbolPath(string path);

        /// <summary>
        /// Returns the current symbol path.
        /// </summary>
        /// <returns>The symbol path.</returns>
        public abstract string GetSymbolPath();

        /// <summary>
        /// Returns the architecture of the target process or crash dump.
        /// </summary>
        public abstract Architecture Architecture { get; }

        /// <summary>
        /// Returns the list of Clr versions loaded into the process.
        /// </summary>
        public abstract IList<ClrInfo> ClrVersions { get; }

        /// <summary>
        /// Returns the pointer size for the target process.
        /// </summary>
        public abstract uint PointerSize { get; }

        /// <summary>
        /// Reads memory from the target.
        /// </summary>
        /// <param name="address">The address to read from.</param>
        /// <param name="buffer">The buffer to store the data in.  Size must be greator or equal to
        /// bytesRequested.</param>
        /// <param name="bytesRequested">The amount of bytes to read from the target process.</param>
        /// <param name="bytesRead">The actual number of bytes read.</param>
        /// <returns>True if any bytes were read out of the process (including a partial read).  False
        /// if no bytes could be read from the address.</returns>
        public abstract bool ReadProcessMemory(Address address, byte[] buffer, int bytesRequested, out int bytesRead);

        /// <summary>
        /// Creates a runtime from the given Dac file on disk.
        /// </summary>
        public abstract ClrRuntime CreateRuntime(string dacFileName);

        /// <summary>
        /// Creates a runtime from a given IXClrDataProcess interface.  Used for debugger plugins.
        /// </summary>
        public abstract ClrRuntime CreateRuntime(object clrDataProcess);

        /// <summary>
        /// Returns the IDebugClient interface associated with this datatarget.  (Will return null if the
        /// user attached passively.)
        /// </summary>
        public abstract Microsoft.Diagnostics.Runtime.Interop.IDebugClient DebuggerInterface { get; }

        /// <summary>
        /// Enumerates information about the loaded modules in the process (both managed and unmanaged).
        /// </summary>
        public abstract IEnumerable<ModuleInfo> EnumerateModules();

        /// <summary>
        /// IDisposable implementation.
        /// </summary>
        public abstract void Dispose();

        abstract internal string ResolveSymbol(Address addr);

        internal string GetDacRequestFileName(string dllName, Runtime.Architecture targetArchitecture, VersionInfo clrVersion)
        {
            dllName = Path.GetFileNameWithoutExtension(dllName);

#if _REDHAWK
            if (string.Compare(dllName, "mrt100", StringComparison.CurrentCultureIgnoreCase) == 0)
                return Architecture == Runtime.Architecture.Amd64 ? "mrt100dac_winamd64.dll" : "mrt100dac_winx86.dll";
#endif

            string currentArchitecture = (IntPtr.Size == 4) ? "x86" : "amd64";
            string dacName = dllName.Equals("coreclr", StringComparison.CurrentCultureIgnoreCase) ? "mscordaccore" :  "mscordacwks";
            return string.Format("{0}_{1}_{2}_{3}.{4}.{5}.{6:D2}.dll", dacName, currentArchitecture, targetArchitecture, clrVersion.Major, clrVersion.Minor, clrVersion.Revision, clrVersion.Patch);
        }


        internal string TryDownloadFile(string fileName, int timeStamp, int fileSize, ISymbolNotification notification)
        {
            if (m_symCache == null)
                m_symCache = new Dictionary<SymbolEntry, string>();

            SymbolEntry entry = new SymbolEntry(fileName, timeStamp, fileSize);
            string result;
            if (m_symCache.TryGetValue(entry, out result))
                return result;

            if (notification == null)
                notification = DefaultSymbolNotification ?? new NullSymbolNotification();

            result = SymbolReader.FindExecutableFilePath(fileName, timeStamp, fileSize, notification);
            m_symCache[entry] = result;
            return result;
        }

        Dictionary<SymbolEntry, string> m_symCache;
    }

    struct SymbolEntry : IEquatable<SymbolEntry>
    {
        public string FileName;
        public int TimeStamp;
        public int FileSize;

        public SymbolEntry(string filename, int timestamp, int filesize)
        {
            FileName = filename;
            TimeStamp = timestamp;
            FileSize = filesize;
        }

        public override int GetHashCode()
        {
            return FileName.GetHashCode() ^ TimeStamp ^ FileSize;
        }

        public override bool Equals(object obj)
        {
            return obj is SymbolEntry && Equals((SymbolEntry)obj);
        }

        public bool Equals(SymbolEntry other)
        {
            return FileName == other.FileName && TimeStamp == other.TimeStamp && FileSize == other.FileSize;
        }
    }


    /// <summary>
    /// Interface for receiving callback notifications when downloading symbol files.
    /// </summary>
    public interface ISymbolNotification
    {
        /// <summary>
        /// Symbol lookup was initiated, but found in a cache without needing to fetch it
        /// from the symbol path.
        /// </summary>
        /// <param name="localPath">The location of the file on disk.</param>
        void FoundSymbolInCache(string localPath);

        /// <summary>
        /// Called when attempting to resolve a location (either local or remote), but we did
        /// not find the file.
        /// </summary>
        /// <param name="url">The path/url attempted.</param>
        void ProbeFailed(string url);

        /// <summary>
        /// We found the symbol on the symbol path.
        /// </summary>
        /// <param name="url">Where we found the symbol from.</param>
        void FoundSymbolOnPath(string url);

        /// <summary>
        /// Called periodically when downloading the symbol from the symbol server.
        /// </summary>
        /// <param name="bytesDownloaded">The total bytes downloaded thus far.</param>
        void DownloadProgress(int bytesDownloaded);

        /// <summary>
        /// Called when the download is complete.
        /// </summary>
        /// <param name="localPath">Where the file was placed.</param>
        /// <param name="requiresDecompression">True if the file requires us to decompress it (done automatically).</param>
        void DownloadComplete(string localPath, bool requiresDecompression);

        /// <summary>
        /// Called when the file is finished decompressing.
        /// </summary>
        /// <param name="localPath">The location of the resulting decompressed file.</param>
        void DecompressionComplete(string localPath);
    }

    /// <summary>
    /// Represents a single runtime in a target process or crash dump.  This serves as the primary
    /// entry point for getting diagnostic information.
    /// </summary>
    public abstract class ClrRuntime
    {
        /// <summary>
        /// Returns the DataTarget associated with this runtime.
        /// </summary>
        public abstract DataTarget DataTarget { get; }

        /// <summary>
        /// Whether or not the process is running in server GC mode or not.
        /// </summary>
        public bool ServerGC { get; protected set; }

        /// <summary>
        /// Enumerates the OS thread ID of GC threads in the runtime.  
        /// </summary>
        public abstract IEnumerable<int> EnumerateGCThreads();

        /// <summary>
        /// The number of logical GC heaps in the process.  This is always 1 for a workstation
        /// GC, and usually it's the number of logical processors in a server GC application.
        /// </summary>
        public int HeapCount { get; protected set; }

        /// <summary>
        /// Returns the pointer size of the target process.
        /// </summary>
        abstract public int PointerSize { get; }

        /// <summary>
        /// Enumerates the list of appdomains in the process.  Note the System appdomain and Shared
        /// AppDomain are omitted.
        /// </summary>
        abstract public IList<ClrAppDomain> AppDomains { get; }

        /// <summary>
        /// Enumerates all managed threads in the process.  Only threads which have previously run managed
        /// code will be enumerated.
        /// </summary>
        abstract public IList<ClrThread> Threads { get; }

        /// <summary>
        /// Enumerates all objects currently on the finalizer queue.  (Not finalizable objects, but objects
        /// which have been collected and will be imminently finalized.)
        /// </summary>
        abstract public IEnumerable<Address> EnumerateFinalizerQueue();

        /// <summary>
        /// Read data out of the target process.
        /// </summary>
        /// <param name="address">The address to start the read from.</param>
        /// <param name="buffer">The buffer to write memory to.</param>
        /// <param name="bytesRequested">How many bytes to read (must be less than/equal to buffer.Length)</param>
        /// <param name="bytesRead">The number of bytes actually read out of the process.  This will be less than
        /// bytes requested if the request falls off the end of an allocation.</param>
        /// <returns>False if the memory is not readable (free or no read permission), true if *some* memory was read.</returns>
        [Obsolete("Use ReadMemory instead.")]
        abstract public bool ReadVirtual(Address address, byte[] buffer, int bytesRequested, out int bytesRead);


        /// <summary>
        /// Read data out of the target process.
        /// </summary>
        /// <param name="address">The address to start the read from.</param>
        /// <param name="buffer">The buffer to write memory to.</param>
        /// <param name="bytesRequested">How many bytes to read (must be less than/equal to buffer.Length)</param>
        /// <param name="bytesRead">The number of bytes actually read out of the process.  This will be less than
        /// bytes requested if the request falls off the end of an allocation.</param>
        /// <returns>False if the memory is not readable (free or no read permission), true if *some* memory was read.</returns>
        abstract public bool ReadMemory(Address address, byte[] buffer, int bytesRequested, out int bytesRead);


        /// <summary>
        /// Reads a pointer value out of the target process.  This function reads only the target's pointer size,
        /// so if this is used on an x86 target, only 4 bytes is read and written to val.
        /// </summary>
        /// <param name="address">The address to read from.</param>
        /// <param name="value">The value at that address.</param>
        /// <returns>True if the read was successful, false otherwise.</returns>
        abstract public bool ReadPointer(Address address, out Address value);

        /// <summary>
        /// Enumerates a list of GC handles currently in the process.  Note that this list may be incomplete
        /// depending on the state of the process when we attempt to walk the handle table.
        /// </summary>
        /// <returns>The list of GC handles in the process, NULL on catastrophic error.</returns>
        public abstract IEnumerable<ClrHandle> EnumerateHandles();

        /// <summary>
        /// Gets the GC heap of the process.
        /// </summary>
        abstract public ClrHeap GetHeap();

        /// <summary>
        /// Gets the GC heap of the process.
        /// </summary>
        abstract public ClrHeap GetHeap(TextWriter diagnosticLog);

        /// <summary>
        /// Returns data on the CLR thread pool for this runtime.
        /// </summary>
        virtual public ClrThreadPool GetThreadPool() { throw new NotImplementedException(); }

        /// <summary>
        /// Enumerates regions of memory which CLR has allocated with a description of what data
        /// resides at that location.  Note that this does not return every chunk of address space
        /// that CLR allocates.
        /// </summary>
        /// <returns>An enumeration of memory regions in the process.</returns>
        abstract public IEnumerable<ClrMemoryRegion> EnumerateMemoryRegions();

        /// <summary>
        /// Attempts to get a ClrMethod for the given instruction pointer.  This will return NULL if the
        /// given instruction pointer is not within any managed method.
        /// </summary>
        abstract public ClrMethod GetMethodByAddress(Address ip);

        /// <summary>
        /// Enumerates all modules in the process.
        /// </summary>
        /// <returns>An enumeration of modules.</returns>
        public abstract IEnumerable<ClrModule> EnumerateModules();

        /// <summary>
        /// Flushes the dac cache.  This function MUST be called any time you expect to call the same function
        /// but expect different results.  For example, after walking the heap, you need to call Flush before
        /// attempting to walk the heap again.  After calling this function, you must discard ALL ClrMD objects
        /// you have cached other than DataTarget and ClrRuntime and re-request the objects and data you need.
        /// (E.G. if you want to use the ClrHeap object after calling flush, you must call ClrRuntime.GetHeap
        /// again after Flush to get a new instance.)
        /// </summary>
        abstract public void Flush();

        /// <summary>
        /// Delegate called when the RuntimeFlushed event is triggered.
        /// </summary>
        /// <param name="runtime">Which runtime was flushed.</param>
        public delegate void RuntimeFlushedCallback(ClrRuntime runtime);

        /// <summary>
        /// Called whenever the runtime is being flushed.  All references to ClrMD objects need to be released
        /// and not used for the given runtime after this call.
        /// </summary>
        public event RuntimeFlushedCallback RuntimeFlushed;

        /// <summary>
        /// Call when flushing the runtime.
        /// </summary>
        protected void OnRuntimeFlushed()
        {
            var evt = RuntimeFlushed;
            if (evt != null)
                evt(this);
        }

        internal static bool IsPrimitive(ClrElementType cet)
        {
            return cet >= ClrElementType.Boolean && cet <= ClrElementType.Double
                || cet == ClrElementType.NativeInt || cet == ClrElementType.NativeUInt
                || cet == ClrElementType.Pointer || cet == ClrElementType.FunctionPointer;
        }

        internal static bool IsValueClass(ClrElementType cet)
        {
            return cet == ClrElementType.Struct;
        }

        internal static bool IsObjectReference(ClrElementType cet)
        {
            return cet == ClrElementType.String || cet == ClrElementType.Class
                || cet == ClrElementType.Array || cet == ClrElementType.SZArray
                || cet == ClrElementType.Object;
        }

        internal static Type GetTypeForElementType(ClrElementType type)
        {
            switch (type)
            {
                case ClrElementType.Boolean:
                    return typeof(bool);

                case ClrElementType.Char:
                    return typeof(char);

                case ClrElementType.Double:
                    return typeof(double);

                case ClrElementType.Float:
                    return typeof(float);
                    
                case ClrElementType.Pointer:
                case ClrElementType.NativeInt:
                case ClrElementType.FunctionPointer:
                    return typeof(IntPtr);

                case ClrElementType.NativeUInt:
                    return typeof(UIntPtr);

                case ClrElementType.Int16:
                    return typeof(short);

                case ClrElementType.Int32:
                    return typeof(int);

                case ClrElementType.Int64:
                    return typeof(long);

                case ClrElementType.Int8:
                    return typeof(sbyte);

                case ClrElementType.UInt16:
                    return typeof(ushort);

                case ClrElementType.UInt32:
                    return typeof(uint);

                case ClrElementType.UInt64:
                    return typeof(ulong);

                case ClrElementType.UInt8:
                    return typeof(byte);

                default:
                    return null;
            }
        }
    }

    /// <summary>
    /// Provides information about CLR's threadpool.
    /// </summary>
    public abstract class ClrThreadPool
    {
        /// <summary>
        /// The total number of threadpool worker threads in the process.
        /// </summary>
        abstract public int TotalThreads { get; }

        /// <summary>
        /// The number of running threadpool threads in the process.
        /// </summary>
        abstract public int RunningThreads { get; }

        /// <summary>
        /// The number of idle threadpool threads in the process.
        /// </summary>
        abstract public int IdleThreads { get; }

        /// <summary>
        /// The minimum number of threadpool threads allowable.
        /// </summary>
        abstract public int MinThreads { get; }

        /// <summary>
        /// The maximum number of threadpool threads allowable.
        /// </summary>
        abstract public int MaxThreads { get; }

        /// <summary>
        /// Returns the minimum number of completion ports (if any).
        /// </summary>
        abstract public int MinCompletionPorts { get; }

        /// <summary>
        /// Returns the maximum number of completion ports.
        /// </summary>
        abstract public int MaxCompletionPorts { get; }

        /// <summary>
        /// Returns the CPU utilization of the threadpool (as a percentage out of 100).
        /// </summary>
        abstract public int CpuUtilization { get; }

        /// <summary>
        /// The number of free completion port threads.
        /// </summary>
        abstract public int FreeCompletionPortCount { get; }

        /// <summary>
        /// The maximum number of free completion port threads.
        /// </summary>
        abstract public int MaxFreeCompletionPorts { get; }

        /// <summary>
        /// Enumerates the work items on the threadpool (native side).
        /// </summary>
        abstract public IEnumerable<NativeWorkItem> EnumerateNativeWorkItems();

        /// <summary>
        /// Enumerates work items on the thread pool (managed side).
        /// </summary>
        /// <returns></returns>
        abstract public IEnumerable<ManagedWorkItem> EnumerateManagedWorkItems();
    }

    /// <summary>
    /// A managed threadpool object.
    /// </summary>
    public abstract class ManagedWorkItem
    {
        /// <summary>
        /// The object address of this entry.
        /// </summary>
        public abstract ulong Object { get; }

        /// <summary>
        /// The type of Object.
        /// </summary>
        public abstract ClrType Type { get; }
    }

    /// <summary>
    /// The type of work item this is.
    /// </summary>
    public enum WorkItemKind
    {
        /// <summary>
        /// Unknown.
        /// </summary>
        Unknown,

        /// <summary>
        /// Callback for an async timer.
        /// </summary>
        AsyncTimer,

        /// <summary>
        /// Async callback.
        /// </summary>
        AsyncCallback,

        /// <summary>
        /// From ThreadPool.QueueUserWorkItem.
        /// </summary>
        QueueUserWorkItem,

        /// <summary>
        /// Timer delete callback.
        /// </summary>
        TimerDelete
    }

    /// <summary>
    /// Represents a work item on CLR's thread pool (native side).
    /// </summary>
    public abstract class NativeWorkItem
    {
        /// <summary>
        /// The type of work item this is.
        /// </summary>
        public abstract WorkItemKind Kind { get; }

        /// <summary>
        /// Returns the callback's address.
        /// </summary>
        public abstract Address Callback { get; }

        /// <summary>
        /// Returns the pointer to the user's data.
        /// </summary>
        public abstract Address Data { get; }
    }

    /// <summary>
    /// Types of Clr handles.
    /// </summary>
    public enum HandleType
    {
        /// <summary>
        /// Weak, short lived handle.
        /// </summary>
        WeakShort = 0,

        /// <summary>
        /// Weak, long lived handle.
        /// </summary>
        WeakLong = 1,
        
        /// <summary>
        /// Strong handle.
        /// </summary>
        Strong = 2,

        /// <summary>
        /// Strong handle, prevents relocation of target object.
        /// </summary>
        Pinned = 3,

        /// <summary>
        /// RefCounted handle (strong when the reference count is greater than 0).
        /// </summary>
        RefCount = 5,

        /// <summary>
        /// A weak handle which may keep its "secondary" object alive if the "target" object is also alive.
        /// </summary>
        Dependent = 6,

        /// <summary>
        /// A strong, pinned handle (keeps the target object from being relocated), used for async IO operations.
        /// </summary>
        AsyncPinned = 7,
        
        /// <summary>
        /// Strong handle used internally for book keeping.
        /// </summary>
        SizedRef = 8
    }

    /// <summary>
    /// Represents a Clr handle in the target process.
    /// </summary>
    public class ClrHandle
    {
        /// <summary>
        /// The address of the handle itself.  That is, *Address == Object.
        /// </summary>
        public Address Address { get; set; }

        /// <summary>
        /// The Object the handle roots.
        /// </summary>
        public Address Object { get; set; }

        /// <summary>
        /// The the type of the Object.
        /// </summary>
        public ClrType Type { get; set; }

        /// <summary>
        /// Whether the handle is strong (roots the object) or not.
        /// </summary>
        public bool Strong
        {
            get
            {
                switch (HandleType)
                {
                    case HandleType.RefCount:
                        return RefCount > 0;

                    case HandleType.WeakLong:
                    case HandleType.WeakShort:
                    case HandleType.Dependent:
                        return false;

                    default:
                        return true;
                }
            }
        }

        /// <summary>
        /// Gets the type of handle.
        /// </summary>
        public HandleType HandleType { get; set; }

        /// <summary>
        /// If this handle is a RefCount handle, this returns the reference count.
        /// RefCount handles with a RefCount > 0 are strong.
        /// NOTE: v2 CLR CANNOT determine the RefCount.  We always set the RefCount
        ///       to 1 in a v2 query since a strong RefCount handle is the common case.
        /// </summary>
        public uint RefCount { get; set; }

        /// <summary>
        /// Set only if the handle type is a DependentHandle.  Dependent handles add
        /// an extra edge to the object graph.  Meaning, this.Object now roots the
        /// dependent target, but only if this.Object is alive itself.
        /// NOTE: CLRs prior to v4.5 cannot obtain the dependent target.  This field will
        ///       be 0 for any CLR prior to v4.5.
        /// </summary>
        public Address DependentTarget { get; set; }

        /// <summary>
        /// The type of the dependent target, if non 0.
        /// </summary>
        public ClrType DependentType { get; set; }

        /// <summary>
        /// The AppDomain the handle resides in.
        /// </summary>
        public ClrAppDomain AppDomain { get; set; }

        #region Internal
        internal ClrHandle()
        {
        }

        internal ClrHandle(Microsoft.Diagnostics.Runtime.Desktop.V45Runtime clr, ClrHeap heap, Microsoft.Diagnostics.Runtime.Desktop.HandleData handleData)
        {
            Address obj;
            Address = handleData.Handle;
            clr.ReadPointer(Address, out obj);

            Object = obj;
            Type = heap.GetObjectType(obj);

            uint refCount = 0;

            if (handleData.Type == (int)HandleType.RefCount)
            {
                if (handleData.IsPegged != 0)
                    refCount = handleData.JupiterRefCount;

                if (refCount < handleData.RefCount)
                    refCount = handleData.RefCount;

                if (Type != null)
                {
                    if (Type.IsCCW(obj))
                    {
                        CcwData data = Type.GetCCWData(obj);
                        if (data != null && refCount < data.RefCount)
                            refCount = (uint)data.RefCount;
                    }
                    else if (Type.IsRCW(obj))
                    {
                        RcwData data = Type.GetRCWData(obj);
                        if (data != null && refCount < data.RefCount)
                            refCount = (uint)data.RefCount;
                    }
                }

                RefCount = refCount;
            }


            HandleType = (HandleType)handleData.Type;
            AppDomain = clr.GetAppDomainByAddress(handleData.AppDomain);

            if (HandleType == HandleType.Dependent)
            {
                DependentTarget = handleData.Secondary;
                DependentType = heap.GetObjectType(handleData.Secondary);
            }
        }
        #endregion
    }

    /// <summary>
    /// Types of memory regions in a Clr process.
    /// </summary>
    public enum ClrMemoryRegionType
    {
        // Loader heaps
        /// <summary>
        /// Data on the loader heap.
        /// </summary>
        LowFrequencyLoaderHeap,

        /// <summary>
        /// Data on the loader heap.
        /// </summary>
        HighFrequencyLoaderHeap,

        /// <summary>
        /// Data on the stub heap.
        /// </summary>
        StubHeap,

        // Virtual Call Stub heaps
        /// <summary>
        /// Clr implementation detail (this is here to allow you to distinguish from other
        /// heap types).
        /// </summary>
        IndcellHeap,
        /// <summary>
        /// Clr implementation detail (this is here to allow you to distinguish from other
        /// heap types).
        /// </summary>
        LookupHeap,
        /// <summary>
        /// Clr implementation detail (this is here to allow you to distinguish from other
        /// heap types).
        /// </summary>
        ResolveHeap,
        /// <summary>
        /// Clr implementation detail (this is here to allow you to distinguish from other
        /// heap types).
        /// </summary>
        DispatchHeap,
        /// <summary>
        /// Clr implementation detail (this is here to allow you to distinguish from other
        /// heap types).
        /// </summary>
        CacheEntryHeap,

        // Other regions
        /// <summary>
        /// Heap for JIT code data.
        /// </summary>
        JitHostCodeHeap,
        /// <summary>
        /// Heap for JIT loader data.
        /// </summary>
        JitLoaderCodeHeap,
        /// <summary>
        /// Heap for module jump thunks.
        /// </summary>
        ModuleThunkHeap,
        /// <summary>
        /// Heap for module lookup tables.
        /// </summary>
        ModuleLookupTableHeap,

        /// <summary>
        /// A segment on the GC heap (committed memory).
        /// </summary>
        GCSegment,

        /// <summary>
        /// A segment on the GC heap (reserved, but not committed, memory).
        /// </summary>
        ReservedGCSegment,

        /// <summary>
        /// A portion of Clr's handle table.
        /// </summary>
        HandleTableChunk
    }

    /// <summary>
    /// Types of GC segments.
    /// </summary>
    public enum GCSegmentType
    {
        /// <summary>
        /// Ephemeral segments are the only segments to contain Gen0 and Gen1 objects.
        /// It may also contain Gen2 objects, but not always.  Objects are only allocated
        /// on the ephemeral segment.  There is one ephemeral segment per logical GC heap.
        /// It is important to not have too many pinned objects in the ephemeral segment,
        /// or you will run into a performance problem where the runtime runs too many GCs.
        /// </summary>
        Ephemeral,

        /// <summary>
        /// Regular GC segments only contain Gen2 objects.
        /// </summary>
        Regular,

        /// <summary>
        /// The large object heap contains objects greater than a certain threshold.  Large
        /// object segments are never compacted.  Large objects are directly allocated
        /// onto LargeObject segments, and all large objects are considered gen2.
        /// </summary>
        LargeObject
    }

    /// <summary>
    /// Represents a region of memory in the process which Clr allocated and controls.
    /// </summary>
    public abstract class ClrMemoryRegion
    {
        /// <summary>
        /// The start address of the memory region.
        /// </summary>
        public Address Address { get; set; }

        /// <summary>
        /// The size of the memory region in bytes.
        /// </summary>
        public ulong Size { get; set; }

        /// <summary>
        /// The type of heap/memory that the region contains.
        /// </summary>
        public ClrMemoryRegionType Type { get; set; }

        /// <summary>
        /// The AppDomain pointer that corresponds to this heap.  You can obtain the
        /// name of the AppDomain index or name by calling the appropriate function
        /// on RuntimeBase.
        /// Note:  HasAppDomainData must be true before getting this property.
        /// </summary>
        abstract public ClrAppDomain AppDomain { get; }

        /// <summary>
        /// The Module pointer that corresponds to this heap.  You can obtain the
        /// filename of the module with this property.
        /// Note:  HasModuleData must be true or this property will be null.
        /// </summary>
        abstract public string Module { get; }

        /// <summary>
        /// Returns the heap number associated with this data.  Returns -1 if no
        /// GC heap is associated with this memory region.
        /// </summary>
        abstract public int HeapNumber { get; set; }

        /// <summary>
        /// Returns the gc segment type associated with this data.  Only callable if
        /// HasGCHeapData is true.
        /// </summary>
        abstract public GCSegmentType GCSegmentType { get; set; }

        /// <summary>
        /// Returns a string describing the region of memory (for example "JIT Code Heap"
        /// or "GC Segment").
        /// </summary>
        /// <param name="detailed">Whether or not to include additional data such as the module,
        /// AppDomain, or GC Heap associated with it.</param>
        abstract public string ToString(bool detailed);

        /// <summary>
        /// Equivalent to GetDisplayString(false).
        /// </summary>
        public override string ToString()
        {
            return ToString(false);
        }
    }

    /// <summary>
    /// The way a method was JIT'ed.
    /// </summary>
    public enum MethodCompilationType
    {
        /// <summary>
        /// Method is not yet JITed and no NGEN image exists.
        /// </summary>
        None,

        /// <summary>
        /// Method was JITed.
        /// </summary>
        Jit,

        /// <summary>
        /// Method was NGEN'ed (pre-JITed).
        /// </summary>
        Ngen
    }

    /// <summary>
    /// Represents a method on a class.
    /// </summary>
    public abstract class ClrMethod
    {
        /// <summary>
        /// The name of the method.  For example, "void System.Foo.Bar(object o, int i)" would return "Bar".
        /// </summary>
        abstract public string Name { get; }

        /// <summary>
        /// Returns the full signature of the function.  For example, "void System.Foo.Bar(object o, int i)"
        /// would return "System.Foo.Bar(System.Object, System.Int32)"
        /// </summary>
        abstract public string GetFullSignature();

        /// <summary>
        /// Returns the instruction pointer in the target process for the start of the method's assembly.
        /// </summary>
        abstract public Address NativeCode { get; }

        /// <summary>
        /// Returns the file and line number for the given offset in the method.
        /// </summary>
        /// <param name="nativeOffset">The offset within the method (not the address in memory) of the instruction pointer.</param>
        /// <returns>The file and line number for the given offset.</returns>
        abstract public SourceLocation GetSourceLocationForOffset(Address nativeOffset);

        /// <summary>
        /// Returns the way this method was compiled.
        /// </summary>
        abstract public MethodCompilationType CompilationType { get; }

        /// <summary>
        /// Returns the IL to native offset mapping.
        /// </summary>
        abstract public ILToNativeMap[] ILOffsetMap { get; }

        /// <summary>
        /// Returns the metadata token of the current method.
        /// </summary>
        abstract public uint MetadataToken { get; }

        /// <summary>
        /// Returns the enclosing type of this method.
        /// </summary>
        abstract public ClrType Type { get; }

        // Visibility:
        /// <summary>
        /// Returns if this method is public.
        /// </summary>
        abstract public bool IsPublic { get; }

        /// <summary>
        /// Returns if this method is private.
        /// </summary>
        abstract public bool IsPrivate { get; }

        /// <summary>
        /// Returns if this method is internal.
        /// </summary>
        abstract public bool IsInternal { get; }

        /// <summary>
        /// Returns if this method is protected.
        /// </summary>
        abstract public bool IsProtected { get; }

        // Attributes:
        /// <summary>
        /// Returns if this method is static.
        /// </summary>
        abstract public bool IsStatic { get; }
        /// <summary>
        /// Returns if this method is final.
        /// </summary>
        abstract public bool IsFinal { get; }
        /// <summary>
        /// Returns if this method is a PInvoke.
        /// </summary>
        abstract public bool IsPInvoke { get; }
        /// <summary>
        /// Returns if this method is a special method.
        /// </summary>
        abstract public bool IsSpecialName { get; }
        /// <summary>
        /// Returns if this method is runtime special method.
        /// </summary>
        abstract public bool IsRTSpecialName { get; }

        /// <summary>
        /// Returns if this method is virtual.
        /// </summary>
        abstract public bool IsVirtual { get; }
        /// <summary>
        /// Returns if this method is abstract.
        /// </summary>
        abstract public bool IsAbstract { get; }
    }
    
    /// <summary>
    /// A method's mapping from IL to native offsets.
    /// </summary>
    public struct ILToNativeMap
    {
        /// <summary>
        /// The IL offset for this entry.
        /// </summary>
        public int ILOffset;

        /// <summary>
        /// The native start offset of this IL entry.
        /// </summary>
        public ulong StartAddress;

        /// <summary>
        /// The native end offset of this IL entry.
        /// </summary>
        public ulong EndAddress;

        /// <summary>
        /// To string.
        /// </summary>
        /// <returns>A visual display of the map entry.</returns>
        public override string ToString()
        {
            return string.Format("{0,2:X} - [{1:X}-{2:X}]", ILOffset, StartAddress, EndAddress);
        }
        
#pragma warning disable 0169
        /// <summary>
        /// Reserved.
        /// </summary>
        private int Reserved;
#pragma warning restore 0169
    }

    /// <summary>
    /// Exception thrown by Microsoft.Diagnostics.Runtime unless there is a more appropriate
    /// exception subclass.
    /// </summary>
    public class ClrDiagnosticsException : Exception
    {
        /// <summary>
        /// Specific HRESULTS for errors.
        /// </summary>
        public enum HR
        {
            /// <summary>
            /// Unknown error occured.
            /// </summary>
            UnknownError = unchecked((int)(((ulong)(0x3) << 31) | ((ulong)(0x125) << 16) | ((ulong)(0x0)))),

            /// <summary>
            /// The dll of the specified runtime (mscorwks.dll or clr.dll) is loaded into the process, but
            /// has not actually been initialized and thus cannot be debugged.
            /// </summary>
            RuntimeUninitialized = UnknownError + 1,

            /// <summary>
            /// Something unexpected went wrong with the debugger we used to attach to the process or load
            /// the crash dump.
            /// </summary>
            DebuggerError,

            /// <summary>
            /// Something unexpected went wrong when requesting data from the target process.
            /// </summary>
            DataRequestError,

            /// <summary>
            /// Hit an unexpected (non-recoverable) dac error.
            /// </summary>
            DacError,

            /// <summary>
            /// The caller attempted to re-use an object after calling ClrRuntime.Flush.  See the
            /// documentation for ClrRuntime.Flush for more details.
            /// </summary>
            RevisionError,

            /// <summary>
            /// An error occurred while processing the given crash dump.
            /// </summary>
            CrashDumpError,

            /// <summary>
            /// There is an issue with the configuration of this application.
            /// </summary>
            ApplicationError,
        }

        /// <summary>
        /// The HRESULT of this exception.
        /// </summary>
        public new int HResult { get { return base.HResult; } }

        #region Functions
        internal ClrDiagnosticsException(string message)
            : base(message)
        {
            base.HResult = (int)HR.UnknownError;
        }

        internal ClrDiagnosticsException(string message, HR hr)
            : base(message)
        {
            base.HResult = (int)hr;
        }
        #endregion

        internal static void ThrowRevisionError(int revision, int runtimeRevision)
        {
            throw new ClrDiagnosticsException(string.Format("You must not reuse any object other than ClrRuntime after calling flush!\nClrModule revision ({0}) != ClrRuntime revision ({1}).", revision, runtimeRevision), ClrDiagnosticsException.HR.RevisionError);
        }
    }
}
