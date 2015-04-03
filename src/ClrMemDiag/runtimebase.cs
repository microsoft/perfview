using Address = System.UInt64;
using Microsoft.Diagnostics.Runtime.Desktop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.Diagnostics.Runtime
{

    abstract class RuntimeBase : ClrRuntime
    {
        static ulong[] EmptyPointerArray = new ulong[0];
        protected DacLibrary m_library;
        protected IXCLRDataProcess m_dacInterface;
        MemoryReader m_cache;
        protected IDataReader m_dataReader;
        protected DataTargetImpl m_dataTarget;
        public RuntimeBase(DataTargetImpl dataTarget, DacLibrary lib)
        {
            Debug.Assert(lib != null);
            Debug.Assert(lib.DacInterface != null);

            m_dataTarget = dataTarget;
            m_library = lib;
            m_dacInterface = m_library.DacInterface;
            InitApi();

            m_dacInterface.Flush();

            IGCInfo data = GetGCInfo();
            if (data == null)
                throw new ClrDiagnosticsException("This runtime is not initialized and contains no data.", ClrDiagnosticsException.HR.RuntimeUninitialized);

            ServerGC = data.ServerMode;
            HeapCount = data.HeapCount;
            CanWalkHeap = data.GCStructuresValid && !dataTarget.DataReader.IsMinidump;
            m_dataReader = dataTarget.DataReader;
        }

        public override DataTarget DataTarget
        {
            get { return m_dataTarget; }
        }

        public IDataReader DataReader
        {
            get { return m_dataReader; }
        }

        protected abstract void InitApi();

        public override int PointerSize
        {
            get { return IntPtr.Size; }
        }

        internal bool CanWalkHeap { get; private set; }
        
        internal MemoryReader MemoryReader
        {
            get
            {
                if (this.m_cache == null)
                    this.m_cache = new MemoryReader(DataReader, 0x200);
                return this.m_cache;
            }
            set
            {
                m_cache = value;
            }
        }

        internal bool GetHeaps(out SubHeap[] heaps)
        {
            heaps = new SubHeap[HeapCount];
            Dictionary<ulong, ulong> allocContexts = GetAllocContexts();
            if (ServerGC)
            {
                ulong[] heapList = GetServerHeapList();
                if (heapList == null)
                    return false;

                bool succeeded = false;
                for (int i = 0; i < heapList.Length; ++i)
                {
                    IHeapDetails heap = GetSvrHeapDetails(heapList[i]);
                    if (heap == null)
                        continue;

                    heaps[i] = new SubHeap(heap, i);
                    heaps[i].AllocPointers = new Dictionary<ulong, ulong>(allocContexts);
                    heaps[i].AllocPointers[heap.EphemeralAllocContextPtr] = heap.EphemeralAllocContextLimit;

                    succeeded = true;
                }

                return succeeded;
            }
            else
            {
                Debug.Assert(HeapCount == 1);

                IHeapDetails heap = GetWksHeapDetails();
                if (heap == null)
                    return false;

                heaps[0] = new SubHeap(heap, 0);
                heaps[0].AllocPointers = allocContexts;
                heaps[0].AllocPointers[heap.EphemeralAllocContextPtr] = heap.EphemeralAllocContextLimit;

                return true;
            }
        }

        internal Dictionary<ulong, ulong> GetAllocContexts()
        {
            Dictionary<ulong, ulong> ret = new Dictionary<ulong, ulong>();

            // Give a max number of threads to walk to ensure no infinite loops due to data
            // inconsistency.
            int max = 1024;

            IThreadData thread = GetThread(GetFirstThread());

            while (max-- > 0 && thread != null)
            {
                if (thread.AllocPtr != 0)
                    ret[thread.AllocPtr] = thread.AllocLimit;

                if (thread.Next == 0)
                    break;
                thread = GetThread(thread.Next);
            }

            return ret;
        }

        struct StackRef
        {
            public ulong Address;
            public ulong Object;

            public StackRef(ulong stackPtr, ulong objRef)
            {
                Address = stackPtr;
                Object = objRef;
            }
        }

        public override IEnumerable<Address> EnumerateFinalizerQueue()
        {
            SubHeap[] heaps;
            if (GetHeaps(out heaps))
            {
                foreach (SubHeap heap in heaps)
                {
                    foreach (Address objAddr in GetPointersInRange(heap.FQStart, heap.FQStop))
                    {
                        if (objAddr != 0)
                            yield return objAddr;
                    }
                }
            }
        }

        internal virtual IEnumerable<ClrRoot> EnumerateStackReferences(ClrThread thread, bool includeDead)
        {
            Address stackBase = thread.StackBase;
            Address stackLimit = thread.StackLimit;
            if (stackLimit <= stackBase)
            {
                Address tmp = stackLimit;
                stackLimit = stackBase;
                stackBase = tmp;
            }

            ClrHeap heap = GetHeap();
            var mask = ((ulong)(PointerSize - 1));
            var cache = MemoryReader;
            cache.EnsureRangeInCache(stackBase);
            for (Address stackPtr = stackBase; stackPtr < stackLimit; stackPtr += (uint)PointerSize)
            {
                Address objRef;
                if (cache.ReadPtr(stackPtr, out objRef))
                {
                    // If the value isn't pointer aligned, it cannot be a managed pointer.
                    if (heap.IsInHeap(objRef))
                    {
                        ulong mt;
                        if (heap.ReadPointer(objRef, out mt))
                        {
                            ClrType type = null;

                            if (mt > 1024)
                                type = heap.GetObjectType(objRef);

                            if (type != null && !type.IsFree)
                                yield return new LocalVarRoot(stackPtr, objRef, type, thread, false, true, false);
                        }
                    }
                }
            }
        }

        private bool IsInSegment(ClrSegment seg, Address p)
        {
            return seg.Start <= p && p <= seg.End;
        }

        #region Abstract
        internal abstract IList<ClrStackFrame> GetStackTrace(uint osThreadId);
        internal abstract ulong GetFirstThread();
        internal abstract IThreadData GetThread(ulong addr);
        internal abstract IHeapDetails GetSvrHeapDetails(ulong addr);
        internal abstract IHeapDetails GetWksHeapDetails();
        internal abstract ulong[] GetServerHeapList();
        internal abstract IThreadStoreData GetThreadStoreData();
        internal abstract ISegmentData GetSegmentData(ulong addr);
        internal abstract IGCInfo GetGCInfo();
        internal abstract IMethodTableData GetMethodTableData(ulong addr);
        internal abstract uint GetTlsSlot();
        internal abstract uint GetThreadTypeIndex();
        #endregion

        #region Helpers
        #region Request Helpers
        protected bool Request(uint id, ulong param, byte[] output)
        {
            byte[] input = BitConverter.GetBytes(param);

            return Request(id, input, output);
        }

        protected bool Request(uint id, uint param, byte[] output)
        {
            byte[] input = BitConverter.GetBytes(param);

            return Request(id, input, output);
        }

        protected bool Request(uint id, byte[] input, byte[] output)
        {
            uint inSize = 0;
            if (input != null)
                inSize = (uint)input.Length;

            uint outSize = 0;
            if (output != null)
                outSize = (uint)output.Length;

            int result = m_dacInterface.Request(id, inSize, input, outSize, output);

            return result >= 0;
        }

        protected I Request<I, T>(uint id, byte[] input)
            where T : struct, I
            where I : class
        {
            byte[] output = GetByteArrayForStruct<T>();

            if (!Request(id, input, output))
                return null;

            return ConvertStruct<I, T>(output);
        }

        protected I Request<I, T>(uint id, ulong param)
            where T : struct, I
            where I : class
        {
            byte[] output = GetByteArrayForStruct<T>();

            if (!Request(id, param, output))
                return null;

            return ConvertStruct<I, T>(output);
        }

        protected I Request<I, T>(uint id, uint param)
            where T : struct, I
            where I : class
        {
            byte[] output = GetByteArrayForStruct<T>();

            if (!Request(id, param, output))
                return null;

            return ConvertStruct<I, T>(output);
        }

        protected I Request<I, T>(uint id)
            where T : struct, I
            where I : class
        {
            byte[] output = GetByteArrayForStruct<T>();

            if (!Request(id, null, output))
                return null;

            return ConvertStruct<I, T>(output);
        }

        protected bool RequestStruct<T>(uint id, ref T t)
            where T : struct
        {
            byte[] output = GetByteArrayForStruct<T>();

            if (!Request(id, null, output))
                return false;

            GCHandle handle = GCHandle.Alloc(output, GCHandleType.Pinned);
            t = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            return true;
        }

        protected bool RequestStruct<T>(uint id, ulong addr, ref T t)
            where T : struct
        {
            byte[] input = new byte[sizeof(ulong)];
            byte[] output = GetByteArrayForStruct<T>();

            WriteValueToBuffer(addr, input, 0);

            if (!Request(id, input, output))
                return false;

            GCHandle handle = GCHandle.Alloc(output, GCHandleType.Pinned);
            t = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            return true;
        }

        protected ulong[] RequestAddrList(uint id, int length)
        {
            byte[] bytes = new byte[length * sizeof(ulong)];
            if (!Request(id, null, bytes))
                return null;

            ulong[] result = new ulong[length];
            for (uint i = 0; i < length; ++i)
                result[i] = BitConverter.ToUInt64(bytes, (int)(i * sizeof(ulong)));

            return result;
        }


        protected ulong[] RequestAddrList(uint id, ulong param, int length)
        {
            byte[] bytes = new byte[length * sizeof(ulong)];
            if (!Request(id, param, bytes))
                return null;

            ulong[] result = new ulong[length];
            for (uint i = 0; i < length; ++i)
                result[i] = BitConverter.ToUInt64(bytes, (int)(i * sizeof(ulong)));

            return result;
        }
        #endregion

        #region Marshalling Helpers
        protected static string BytesToString(byte[] output)
        {
            int len = 0;
            while (len < output.Length && (output[len] != 0 || output[len + 1] != 0))
                len += 2;

            if (len > output.Length)
                len = output.Length;

            return Encoding.Unicode.GetString(output, 0, len);
        }

        protected byte[] GetByteArrayForStruct<T>() where T : struct
        {
            return new byte[Marshal.SizeOf(typeof(T))];
        }

        protected I ConvertStruct<I, T>(byte[] bytes)
            where I : class
            where T : I
        {
            GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
            I result = (I)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            handle.Free();
            return result;
        }

        protected int WriteValueToBuffer(IntPtr ptr, byte[] buffer, int offset)
        {
            ulong value = (ulong)ptr.ToInt64();
            for (int i = offset; i < offset + IntPtr.Size; ++i)
            {
                buffer[i] = (byte)value;
                value >>= 8;
            }

            return offset + IntPtr.Size;
        }

        protected int WriteValueToBuffer(int value, byte[] buffer, int offset)
        {
            for (int i = offset; i < offset + sizeof(int); ++i)
            {
                buffer[i] = (byte)value;
                value >>= 8;
            }

            return offset + sizeof(int);
        }

        protected int WriteValueToBuffer(uint value, byte[] buffer, int offset)
        {
            for (int i = offset; i < offset + sizeof(int); ++i)
            {
                buffer[i] = (byte)value;
                value >>= 8;
            }

            return offset + sizeof(int);
        }

        protected int WriteValueToBuffer(ulong value, byte[] buffer, int offset)
        {
            for (int i = offset; i < offset + sizeof(ulong); ++i)
            {
                buffer[i] = (byte)value;
                value >>= 8;
            }

            return offset + sizeof(ulong);
        }
        #endregion

        #region Data Read


        public override bool ReadMemory(Address address, byte[] buffer, int bytesRequested, out int bytesRead)
        {
            return m_dataReader.ReadMemory(address, buffer, bytesRequested, out bytesRead);
        }

        [Obsolete]
        public override bool ReadVirtual(ulong address, byte[] buffer, int bytesRequested, out int bytesRead)
        {
            return m_dataReader.ReadMemory(address, buffer, bytesRequested, out bytesRead);
        }


        byte[] m_dataBuffer = new byte[8];
        public bool ReadByte(Address addr, out byte value)
        {
            // todo: There's probably a more efficient way to implement this if ReadVirtual accepted an "out byte"
            //       "out dword", "out long", etc.
            value = 0;
            int read = 0;
            if (!ReadMemory(addr, m_dataBuffer, 1, out read))
                return false;

            Debug.Assert(read == 1);

            value = m_dataBuffer[0];
            return true;
        }

        public bool ReadByte(Address addr, out sbyte value)
        {
            value = 0;
            int read = 0;
            if (!ReadMemory(addr, m_dataBuffer, 1, out read))
                return false;

            Debug.Assert(read == 1);

            value = (sbyte)m_dataBuffer[0];
            return true;
        }

        public bool ReadDword(ulong addr, out int value)
        {
            value = 0;
            int read = 0;
            if (!ReadMemory(addr, m_dataBuffer, sizeof(int), out read))
                return false;

            Debug.Assert(read == 4);

            value = BitConverter.ToInt32(m_dataBuffer, 0);
            return true;
        }

        public bool ReadDword(ulong addr, out uint value)
        {
            value = 0;
            int read = 0;
            if (!ReadMemory(addr, m_dataBuffer, sizeof(uint), out read))
                return false;

            Debug.Assert(read == 4);

            value = BitConverter.ToUInt32(m_dataBuffer, 0);
            return true;
        }

        public bool ReadFloat(ulong addr, out float value)
        {
            value = 0;
            int read = 0;
            if (!ReadMemory(addr, m_dataBuffer, sizeof(float), out read))
                return false;

            Debug.Assert(read == sizeof(float));

            value = BitConverter.ToSingle(m_dataBuffer, 0);
            return true;
        }

        public bool ReadFloat(ulong addr, out double value)
        {
            value = 0;
            int read = 0;
            if (!ReadMemory(addr, m_dataBuffer, sizeof(double), out read))
                return false;

            Debug.Assert(read == sizeof(double));

            value = BitConverter.ToDouble(m_dataBuffer, 0);
            return true;
        }


        public bool ReadShort(ulong addr, out short value)
        {
            value = 0;
            int read = 0;
            if (!ReadMemory(addr, m_dataBuffer, sizeof(short), out read))
                return false;

            Debug.Assert(read == sizeof(short));

            value = BitConverter.ToInt16(m_dataBuffer, 0);
            return true;
        }

        public bool ReadShort(ulong addr, out ushort value)
        {
            value = 0;
            int read = 0;
            if (!ReadMemory(addr, m_dataBuffer, sizeof(ushort), out read))
                return false;

            Debug.Assert(read == sizeof(ushort));

            value = BitConverter.ToUInt16(m_dataBuffer, 0);
            return true;
        }

        public bool ReadQword(ulong addr, out ulong value)
        {
            value = 0;
            int read = 0;
            if (!ReadMemory(addr, m_dataBuffer, sizeof(ulong), out read))
                return false;

            Debug.Assert(read == sizeof(ulong));

            value = BitConverter.ToUInt64(m_dataBuffer, 0);
            return true;
        }

        public bool ReadQword(ulong addr, out long value)
        {
            value = 0;
            int read = 0;
            if (!ReadMemory(addr, m_dataBuffer, sizeof(long), out read))
                return false;

            Debug.Assert(read == sizeof(long));

            value = BitConverter.ToInt64(m_dataBuffer, 0);
            return true;
        }

        public override bool ReadPointer(ulong addr, out ulong value)
        {
            int ptrSize = (int)PointerSize;
            int read = 0;
            if (!ReadMemory(addr, m_dataBuffer, ptrSize, out read))
            {
                value = 0xcccccccc;
                return false;
            }

            Debug.Assert(read == ptrSize);

            if (ptrSize == 4)
                value = (ulong)BitConverter.ToUInt32(m_dataBuffer, 0);
            else
                value = (ulong)BitConverter.ToUInt64(m_dataBuffer, 0);

            return true;
        }

        public bool ReadPtr(ulong addr, out long value)
        {
            int ptrSize = (int)PointerSize;
            int read = 0;
            if (!ReadMemory(addr, m_dataBuffer, ptrSize, out read))
            {
                value = 0xcccccccc;
                return false;
            }

            Debug.Assert(read == ptrSize);

            if (ptrSize == 4)
                value = (long)BitConverter.ToInt32(m_dataBuffer, 0);
            else
                value = (long)BitConverter.ToInt64(m_dataBuffer, 0);

            return true;
        }

        internal IEnumerable<ulong> GetPointersInRange(ulong start, ulong stop)
        {
            // Possible we have empty list, or inconsistent data.
            if (start >= stop)
                return EmptyPointerArray;

            // Enumerate individually if we have too many.
            ulong count = (stop - start) / (ulong)IntPtr.Size;
            if (count > 4096)
                return EnumeratePointersInRange(start, stop);

            ulong[] array = new ulong[count];
            byte[] tmp = new byte[(int)count * IntPtr.Size];
            int read;
            if (!ReadMemory(start, tmp, tmp.Length, out read))
                return EmptyPointerArray;

            if (IntPtr.Size == 4)
                for (uint i = 0; i < array.Length; ++i)
                    array[i] = BitConverter.ToUInt32(tmp, (int)(i * IntPtr.Size));
            else
                for (uint i = 0; i < array.Length; ++i)
                    array[i] = BitConverter.ToUInt64(tmp, (int)(i * IntPtr.Size));

            return array;
        }

        private IEnumerable<Address> EnumeratePointersInRange(ulong start, ulong stop)
        {
            ulong obj;
            for (ulong ptr = start; ptr < stop; ptr += (uint)IntPtr.Size)
            {
                if (!ReadPointer(ptr, out obj))
                    break;

                yield return obj;
            }
        }
        #endregion
        #endregion
    }
}
