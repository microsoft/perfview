using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;


// The MemoryStats namespace is about taking a complete memory snapshot of the process (basically using Virtual Query 
// and presenting it in a nice way.   
namespace MemoryStats
{
    /// <summary>
    /// A MemoryNode represents a set of memory regions in a process.   MemoryNodes can have children and thus
    /// form trees.   Node also have names, 
    /// </summary>
    internal class MemoryNode
    {
        /// <summary>
        /// This is the main entry point into the MemoryNode class.  Basically giving a process ID return
        /// a MemoryNode that represents the roll-up of all memory in the process.  
        /// </summary>
        public static unsafe MemoryNode MemorySnapShotForProcess(int processID)
        {
            var root = Root();
            var process = Process.GetProcessById(processID);

            bool is32BitProcess = false;
            NativeMethods.IsWow64Process(process.Handle, out is32BitProcess);
            var kernelToUser = new KernelToUserDriveMapping();

            var name = new StringBuilder(260);
            long MaxAddress = 0x7fffffff;
            long address = 0;
            do
            {
                var child = new MemoryNode();

                int result = NativeMethods.VirtualQueryEx(process.Handle,
                    (IntPtr)address,
                    out child.info, (uint)Marshal.SizeOf(child.info));
                address = (long)child.info.BaseAddress + (long)child.info.RegionSize;

                // TODO FIX NOW worry about error codes. 
                if (result == 0)
                {
                    break;
                }

                if (child.info.Type == NativeMethods.MemoryType.MEM_IMAGE || child.info.Type == NativeMethods.MemoryType.MEM_MAPPED)
                {
                    name.Clear();
                    var ret = NativeMethods.GetMappedFileName(process.Handle, (IntPtr)address, name, name.Capacity);
                    if (ret != 0)
                    {
                        var kernelName = name.ToString();
                        child.Name = kernelToUser[kernelName];
                    }
                    else
                    {
                        Debug.WriteLine("Error, GetMappedFileName failed.");
                    }
                }
                root.Insert(child);
            } while (address <= MaxAddress);

            NativeMethods.PSAPI_WORKING_SET_INFORMATION* WSInfo = stackalloc NativeMethods.PSAPI_WORKING_SET_INFORMATION[1];
            NativeMethods.QueryWorkingSet(process.Handle, WSInfo, sizeof(NativeMethods.PSAPI_WORKING_SET_INFORMATION));
            int buffSize = (int)(WSInfo->NumberOfEntries) * 8 + 8 + 1024; // The 1024 is to allow for working set growth

            WSInfo = (NativeMethods.PSAPI_WORKING_SET_INFORMATION*)Marshal.AllocHGlobal(buffSize);
            if (!NativeMethods.QueryWorkingSet(process.Handle, WSInfo, buffSize))
            {
                Marshal.FreeHGlobal((IntPtr)WSInfo);
                Marshal.ThrowExceptionForHR(Marshal.GetLastWin32Error());
            }

            // Copy the working set info to an array and sort the page addresses.  
            int numBlocks = (int)WSInfo->NumberOfEntries;
            ulong[] blocks = new ulong[numBlocks];
            for (var curWSIdx = 0; curWSIdx < numBlocks; curWSIdx++)
            {
                blocks[curWSIdx] = WSInfo->WorkingSetInfo(curWSIdx).Address;
            }

            Array.Sort(blocks);
            Marshal.FreeHGlobal((IntPtr)WSInfo);

            // Attribute the working set to the regions of memory
            int curPageIdx = 0;
            foreach (var region in root.Children)
            {
                var end = region.End;
                while (curPageIdx < blocks.Length && blocks[curPageIdx] < end)
                {
                    curPageIdx++;
                    region.PrivateWorkingSet += 4; // TODO FIX NOW
                }
            }

            GC.KeepAlive(process);
            return root;
        }

        public ulong End { get { return Address + Size; } }
        public ulong Address { get { return (ulong)info.BaseAddress; } }
        public ulong Size { get { return (ulong)info.RegionSize; } }

        /** TODO REMOVE 
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public PageProtection AllocationProtect;
        public IntPtr RegionSize;
        public MemoryState State;
        public PageProtection Protect;
        public MemoryType Type;
        public ulong Blocks;
        public ulong Id;
        public ulong Storage;
        public string Type;
         ****/
        public string Name;
        public UseType UseType;

        public ulong Commit;        // Committed memory
        public ulong PrivateCommit;

        public ulong WorkingSet;
        public ulong PrivateWorkingSet;
        public ulong SharableWorkingSet { get { return WorkingSet - PrivateWorkingSet; } }
        public ulong SharedWorkingSet;

        public PageProtection Protection;
        public List<MemoryNode> Children;
        public MemoryNode Parent;

        public override string ToString()
        {
            var sw = new StringWriter();
            ToXml(sw);
            return sw.ToString();
        }

        public void ToXml(TextWriter writer, string indent = "")
        {
            writer.Write("{0}<MemoryNode", indent);
            writer.Write(" Address=\"{0:x}\"", Address);
            writer.Write(" Size=\"{0:x}\"", Size);
            writer.Write(" Type=\"{0}\"", info.Type);
            writer.Write(" PrivateWS=\"{0}\"", PrivateWorkingSet);
            writer.Write(" Details=\"{0}\"", XmlUtilities.XmlEscape(Name == null ? "" : Name));
            if (Children != null)
            {
                writer.WriteLine(">");
                var childIndent = indent + " ";
                foreach (var child in Children)
                {
                    child.ToXml(writer, childIndent);
                }

                writer.WriteLine("{0}</MemoryNode>", indent);
            }
            else
            {
                writer.WriteLine("/>");
            }
        }

        #region private
        private NativeMethods.MEMORY_BASIC_INFORMATION info;

        private MemoryNode() { }
        private void Insert(MemoryNode newNode)
        {
            Debug.Assert(Address <= newNode.Address && newNode.End <= End);
            if (Children == null)
            {
                Children = new List<MemoryNode>();
            }

            // Search backwards for efficiency.  
            for (int i = Children.Count; 0 < i;)
            {
                var child = Children[--i];
                if (child.Address <= newNode.Address && newNode.End <= child.End)
                {
                    child.Insert(newNode);
                    return;
                }
            }
            Children.Add(newNode);
            newNode.Parent = this;
        }
        private static MemoryNode Root()
        {
            var ret = new MemoryNode();
            ret.info.RegionSize = unchecked(new IntPtr((long)ulong.MaxValue));
            ret.Name = "[ROOT]";
            return ret;
        }

        #endregion
    }

    [Flags]
    internal enum PageProtection
    {
        PAGE_EXECUTE = 0x10,
        PAGE_EXECUTE_READ = 0x20,
        PAGE_EXECUTE_READWRITE = 0x40,
        PAGE_EXECUTE_WRITECOPY = 0x80,
        PAGE_NOACCESS = 0x01,
        PAGE_READONLY = 0x02,
        PAGE_READWRITE = 0x04,
        PAGE_WRITECOPY = 0x08,
    }

    internal enum UseType
    {
        Heap = 0,
        Stack = 1,
        Image = 2,
        MappedFile = 3,
        PrivateData = 4,
        Shareable = 5,
        Free = 6,
        // Unknown1 = 7,
        ManagedHeaps = 8,
        // Unknown2 = 9,
        Unusable = 10,
    }

    #region private classes 
    internal class NativeMethods
    {
        [Flags]
        internal enum MemoryState
        {
            /// <summary>
            /// Indicates committed pages for which physical storage has been allocated, either in memory or in the paging file on disk.
            /// </summary>
            MEM_COMMIT = 0x1000,
            /// <summary>
            /// Indicates free pages not accessible to the calling process and available to be allocated. 
            /// For free pages, the information in the AllocationBase, AllocationProtect, Protect, and Type members is undefined.
            /// </summary>
            MEM_FREE = 0x10000,
            /// <summary>
            /// Indicates reserved pages where a range of the process's virtual address space is reserved without
            /// any physical storage being allocated. 
            /// For reserved pages, the information in the Protect member is undefined.
            /// </summary>
            MEM_RESERVE = 0x2000,
        };

        internal enum MemoryType
        {
            /// <summary>
            /// Indicates that the memory pages within the region are mapped into the view of an image section.
            /// </summary>
            MEM_IMAGE = 0x1000000,
            /// <summary>
            /// Indicates that the memory pages within the region are mapped into the view of a section.
            /// </summary>
            MEM_MAPPED = 0x40000,
            /// <summary>
            /// Indicates that the memory pages within the region are private (that is, not shared by other processes).
            /// </summary>
            MEM_PRIVATE = 0x20000,
        };

        [StructLayout(LayoutKind.Sequential)]
        internal struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public PageProtection AllocationProtect;
            public IntPtr RegionSize;
            public MemoryState State;
            public PageProtection Protect;
            public MemoryType Type;
        }

        [DllImport("kernel32.dll", SetLastError = true), SuppressUnmanagedCodeSecurityAttribute]
        internal static extern int VirtualQueryEx(
           IntPtr hProcess,
           IntPtr lpAddress,
           out MEMORY_BASIC_INFORMATION lpBuffer,
           uint dwLength);

        [StructLayout(LayoutKind.Sequential)]
        internal unsafe struct PSAPI_WORKING_SET_INFORMATION
        {
            public IntPtr NumberOfEntries;
            // The rest of the struct is an array of PSAPI_WORKING_SET_BLOCK.  This function fetches them.  
            public PSAPI_WORKING_SET_BLOCK WorkingSetInfo(int idx)
            {
                fixed (PSAPI_WORKING_SET_INFORMATION* ptr = &this)
                {
                    var blockPtr = (PSAPI_WORKING_SET_BLOCK*)(ptr + 1);
                    return blockPtr[idx];
                }
            }
        }
        internal struct PSAPI_WORKING_SET_BLOCK
        {
            public bool Shared { get { return (((PSAPI_WORKING_SET_BITS)Flags) & PSAPI_WORKING_SET_BITS.Shared) != 0; } }
            public int ShareCount
            {
                get
                {
                    return (((int)Flags) >> (int)PSAPI_WORKING_SET_BITS.ShareCountShift) & (int)PSAPI_WORKING_SET_BITS.ShareCountMask;
                }
            }
            public int Win32Protection
            {
                get
                {
                    return (((int)Flags) >> (int)PSAPI_WORKING_SET_BITS.Win32ProtectionShift) & (int)PSAPI_WORKING_SET_BITS.Win32ProtectionMask;
                }
            }

            public ulong Address { get { return ((ulong)Flags) & ~0xFFFUL; } }

            #region private
            public IntPtr Flags;

            [Flags]
            private enum PSAPI_WORKING_SET_BITS
            {
                ShareCountShift = 1,
                ShareCountMask = 0x7,

                Win32ProtectionShift = 0,
                Win32ProtectionMask = 0x7FF,

                Shared = 0x100,
            };
            #endregion
        }

        // TODO FIX NOW this can be in kernel32 too
        [DllImport("psapi.dll", SetLastError = true), SuppressUnmanagedCodeSecurityAttribute]
        internal static extern unsafe bool QueryWorkingSet(
           IntPtr hProcess,
           PSAPI_WORKING_SET_INFORMATION* workingSetInfo,
           int workingSetInfoSize);

        [DllImport("psapi.dll", SetLastError = true), SuppressUnmanagedCodeSecurityAttribute]
        internal static extern int GetMappedFileName(
           IntPtr hProcess,
           IntPtr address,
           StringBuilder lpFileName,
           int nSize);

        // TODO FIX NOW use or remove 
        [DllImport("kernel32.dll", SetLastError = true), SuppressUnmanagedCodeSecurityAttribute]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWow64Process(
             [In] IntPtr processHandle,
             [Out, MarshalAs(UnmanagedType.Bool)] out bool wow64Process);
    }
    #endregion
}
