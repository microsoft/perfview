using System;
using System.Collections.Generic;
using System.IO;
using Address = System.UInt64;

// This code is never meant to be run.  It is a reference assembly for HeapDump.exe.  
// It only exposes those classes that I wish to export (GCHeap), and thus avoid 
// multiple definition errors at compile time. 


namespace ClrMemory
{
    public class GCHeap
    {
        public GCHeap(string filePath) { }
        public GCHeapType GetObjectType(Address objRef) { return null; }
        public bool IsInHeap(Address objRef) { return false; }
        public DateTime TimeCollected { get; private set; }
        public string ProcessName { get; private set; }
        public int ProcessID { get; private set; }
        public string MachineName { get; private set; }
        public int PointerSize { get; private set; }
        public long TotalSize { get { return 0; } }
        public int NumberOfObjects { get; internal set; }
        public IList<GCHeapRoot> Roots { get { return null; } }
        public IList<GCHeapType> Types { get { return null; } }
        public IList<GCHeapSegment> Segments { get { return null; } }
        public GCHeapType GetTypeByIndex(GCHeapTypeIndex typeIndex) { return null; }
        public GCHeapTypeIndex TypeIndexLimit { get { return 0; } }
        public void ToXml(TextWriter writer) { }
        public override string ToString() { return null; }

#if DEBUG
        public string DumpAt(Address address, int len = 256) { return null;  }
#endif
    }

    public class GCHeapSegment
    {
        public Address Start { get; private set; }
        public Address End { get { return 0; } }
        public int Length { get; private set; }
        public int NumberOfObjects { get; private set; }
        public bool EnumerateObjectsInSegment(Func<Address, GCHeapType, bool> action) { return false; }
        public int Generation { get; private set; }
        public GCHeap Heap { get; private set; }
        public void ToXml(TextWriter writer) { }
        public override string ToString() { return null; }
    }
    public enum GCRootKind { StaticVar, LocalVar, Strong, Weak, Pinning, Finalizer, Max = Finalizer }
    public class GCHeapRoot
    {
        public string Name { get; private set; }
        public GCHeapType Type { get; private set; }
        public string ModuleFilePath { get { return null; } }
        public string AppDomainName { get; private set; }
        public GCRootKind Kind { get; private set; }
        public Address HeapReference { get; private set; }
        public Address AddressOfRoot { get; private set; }
        public void ToXml(TextWriter writer) { }
        public override string ToString() { return null; }
    }

    public enum GCHeapTypeIndex { Invalid = -1 }

    public class GCHeapType
    {
        public string Name { get; private set; }
        public string ModuleFilePath { get { return null; } }
        public GCHeapTypeIndex Index { get; private set; }
        public int BaseSize { get { return 0; } }
        public int GetSize(Address objRef) { return 0; }
        public void EnumerateRefsOfObject(Address objRef, Action<Address, int> action) { }
        public IList<GCHeapField> Fields { get { return null; } }
        public GCHeap Heap { get { return null; } }
        public bool HasSimpleValue { get { return false; } }
        public bool IsArray { get; private set; }
        public object GetValue(Address address) { return null; }
        public bool GetFieldForOffset(int fieldOffset, out GCHeapField childField, out int childFieldOffset)
        {
            childField = null;
            childFieldOffset = 0;
            return false;
        }
        public GCHeapType ElementType { get; private set; }
        public int GetArrayLength(Address objRef) { return 0; }
        public Address GetArrayElementAddress(Address objRef, int index) { return 0; }
        public void ToXml(TextWriter writer) { }
        public override string ToString() { return null; }
    }

    public class GCHeapField
    {
        public string Name { get; private set; }
        public int Offset { get; private set; }
        public GCHeapType Type { get; private set; }
        public Address GetFieldAddress(Address objRef) { return 0; }
        public object GetFieldValue(Address objRef) { return null; }
        public void ToXml(TextWriter writer) { }
        public override string ToString() { return null; }
    }
}
