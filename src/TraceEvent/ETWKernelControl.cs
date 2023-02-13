using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Win32;

using KernelKeywords = Microsoft.Diagnostics.Tracing.Parsers.KernelTraceEventParser.Keywords;

namespace Microsoft.Diagnostics.Tracing
{
    public unsafe static class ETWKernelControl
    {
        /// <summary>
        /// Start an ETW kernel session.
        /// </summary>
        public static int StartKernelSession(
            out ulong TraceHandle, // EVENT_TRACE_PROPERTIES
            void* propertyBuff, 
            int propertyBuffLength,
            STACK_TRACING_EVENT_ID* stackTracingEventIds,
            int cStackTracingEventIds)
        {
            var properties = (EVENT_TRACE_PROPERTIES*)propertyBuff;
            bool needExtensions = false;
            if ((((KernelKeywords)properties->EnableFlags) &
                (KernelKeywords.PMCProfile | KernelKeywords.ReferenceSet
                | KernelKeywords.ThreadPriority | KernelKeywords.IOQueue | KernelKeywords.Handle)) != 0)
                needExtensions = true;

            if (needExtensions)
            {
                List<ExtensionItem> extensions = new List<ExtensionItem>();
                PutEnableFlagsIntoExtensions(extensions, (KernelKeywords)properties->EnableFlags);
                PutStacksIntoExtensions(extensions, stackTracingEventIds, cStackTracingEventIds);
                int len = SaveExtensions(extensions, null, 0);
                int extensionsOffset = ExtensionsOffset(properties, propertyBuffLength);
                int maxExtensionSize = propertyBuffLength - extensionsOffset;

                if (len > maxExtensionSize)
                    throw new ArgumentOutOfRangeException("Too much ETW extension information specified.");
                SaveExtensions(extensions, properties, extensionsOffset);
                return StartTraceW(out TraceHandle, KernelSessionName, properties);
            }

            LoadKernelTraceControl();

            properties->EnableFlags = properties->EnableFlags & (uint)~KernelTraceEventParser.NonOSKeywords;
            return StartKernelTrace(out TraceHandle, properties, stackTracingEventIds, cStackTracingEventIds);
        }

        /// <summary>
        /// Turn on windows heap logging (stack for allocation) for a particular existing process.
        /// </summary>
        public static int StartWindowsHeapSession(
            out ulong traceHandle,
            void* propertyBuff,
            int propertyBuffLength,
            int pid)
        {
            lock ((object)s_KernelTraceControlLoaded)
            {
                var properties = (EVENT_TRACE_PROPERTIES*)propertyBuff;

                // This is HeapTraceProviderTraceEventParser.ProviderGuid;
                properties->Wnode.Guid = new Guid(unchecked((int)0x222962ab), unchecked((short)0x6180), unchecked((short)0x4b88), 0xa8, 0x25, 0x34, 0x6b, 0x75, 0xf2, 0xa2, 0x4a);

                List<ExtensionItem> extensions = new List<ExtensionItem>();

                /* Prep Extensions */
                // Turn on the Pids feature, selects which process to turn on. 
                var pids = new ExtensionItem(ExtensionItemTypes.ETW_EXT_PIDS);
                pids.Data.Add(pid);
                extensions.Add(pids);

                // Initialize the stack collecting information
                var stackSpec = new ExtensionItem(ExtensionItemTypes.ETW_EXT_STACKWALK_FILTER);
                stackSpec.Data.Add(0x1021);       // 10 = HeapProvider 21 = Stack on Alloc
                stackSpec.Data.Add(0x1022);       // 10 = HeapProvider 22 = Stack on Realloc
                extensions.Add(stackSpec);

                int len = SaveExtensions(extensions, null, 0);
                int extensionsOffset = ExtensionsOffset(properties, propertyBuffLength);
                int maxExtensionSize = propertyBuffLength - extensionsOffset;
                if (len > maxExtensionSize)
                    throw new ArgumentOutOfRangeException("Too much ETW extension information specified.");

                // Save Extensions.
                SaveExtensions(extensions, properties, extensionsOffset);

                // Get the session name from the properties.  
                var sessionName = new String((char*)(((byte*)properties) + properties->LoggerNameOffset));

                // Actually start the session.
                return StartTraceW(out traceHandle, sessionName, properties);
            }
        }

        /// <summary>
        /// Turn on windows heap logging for a particular EXE file name (just the file name, no directory).
        /// This API is OK to call from one thread while Process() is being run on another.
        /// </summary>
        public static int StartWindowsHeapSession(
            out ulong traceHandle,
            void* propertyBuff,
            int propertyBuffLength,
            string exeFileName)
        {
            lock ((object)s_KernelTraceControlLoaded)
            {
                var properties = (EVENT_TRACE_PROPERTIES*)propertyBuff;

                // Get the session name from the properties.  
                var sessionName = new String((char*)(((byte*)properties) + properties->LoggerNameOffset));

                SetImageTracingFlags(sessionName, exeFileName, true);
                return StartWindowsHeapSession(out traceHandle, properties, propertyBuffLength, 0);
            }
        }

        /// <summary>
        /// Resets any windows heap tracing flags that might be set.
        /// Called during Stop.
        /// </summary>
        public static void ResetWindowsHeapTracingFlags(string sessionName, bool noThrow = false)
        {
            lock ((object)s_KernelTraceControlLoaded)
            {
                try
                {
                    // Mark the fact that we have turned off heap tracing.
                    RegistryKey perfViewHeapTraceKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\TraceEvent\HeapTracing", true);
                    if (perfViewHeapTraceKey != null)
                    {
                        var exeFileName = perfViewHeapTraceKey.GetValue(sessionName) as string;
                        if (exeFileName != null)
                        {
                            SetImageTracingFlags(sessionName, exeFileName, false);
                            perfViewHeapTraceKey.DeleteValue(sessionName);
                        }
                        perfViewHeapTraceKey.Dispose();
                    }
                }
                catch (Exception)
                {
                    if (!noThrow)
                        throw;
                }
            }
        }

        /// <summary>
        /// It is sometimes useful to merge the contents of several ETL files into a single 
        /// output ETL file.   This routine does that.  It also will attach additional 
        /// information that will allow correct file name and symbolic lookup if the 
        /// ETL file is used on a machine other than the one that the data was collected on.
        /// If you wish to transport the file to another machine you need to merge it, even 
        /// if you have only one file so that this extra information get incorporated.  
        /// </summary>
        /// <param name="inputETLFileNames">The input ETL files to merge</param>
        /// <param name="outputETLFileName">The output ETL file to produce.</param>
        /// <param name="flags">Optional Additional options for the Merge (see TraceEventMergeOptions).</param>
        public static void Merge(string[] inputETLFileNames, string outputETLFileName, EVENT_TRACE_MERGE_EXTENDED_DATA flags)
        {
            LoadKernelTraceControl();

            IntPtr state = IntPtr.Zero;

            // If we happen to be in the WOW, disable file system redirection as you don't get the System32 dlls otherwise. 
            bool disableRedirection = Wow64DisableWow64FsRedirection(ref state) != 0;
            try
            {
                Debug.Assert(disableRedirection || sizeof(IntPtr) == 8);

                int retValue = CreateMergedTraceFile(outputETLFileName, inputETLFileNames, inputETLFileNames.Length, flags);
                if (retValue != 0 && retValue != 0x7A)      // 0x7A means ERROR_INSUFFICIENT_BUFFER and means events were lost.  This is OK as the file indicates this as well.
                    throw new Exception("Merge operation failed return code 0x" + retValue.ToString("x"));
            }
            finally
            {
                if (disableRedirection)
                    Wow64RevertWow64FsRedirection(state);
            }
        }

        #region private 
        #region ETW Tracing from KernelTraceControl.h

        [DllImport("KernelTraceControl.dll", CharSet = CharSet.Unicode)]
        private extern static int StartKernelTrace(
            out UInt64 TraceHandle,
            void* Properties,                                   // EVENT_TRACE_PROPERTIES
            STACK_TRACING_EVENT_ID* StackTracingEventIds,       // Array of STACK_TRACING_EVENT_ID
            int cStackTracingEventIds);

        [DllImport("KernelTraceControl.dll", CharSet = CharSet.Unicode)]
        private extern static int CreateMergedTraceFile(
            string wszMergedFileName,
            string[] wszTraceFileNames,
            int cTraceFileNames,
            EVENT_TRACE_MERGE_EXTENDED_DATA dwExtendedDataFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int Wow64DisableWow64FsRedirection(ref IntPtr ptr);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int Wow64RevertWow64FsRedirection(IntPtr ptr);

        [System.Runtime.InteropServices.DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
        private extern static int StartTraceW(
            [Out] out UInt64 sessionHandle,
            [In] string sessionName,
            EVENT_TRACE_PROPERTIES* properties);

        /// <summary>
        /// EVENT_TRACE_PROPERTIES is a structure used by StartTrace and ControlTrace,
        /// however it can not be used directly in the definition of these functions
        /// because extra information has to be hung off the end of the structure
        /// before being passed.  (LofFileNameOffset, LoggerNameOffset)
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct EVENT_TRACE_PROPERTIES
        {
            public WNODE_HEADER Wnode;      // Timer Resolution determined by the Wnode.ClientContext.
            public UInt32 BufferSize;
            public UInt32 MinimumBuffers;
            public UInt32 MaximumBuffers;
            public UInt32 MaximumFileSize;
            public UInt32 LogFileMode;
            public UInt32 FlushTimer;
            public UInt32 EnableFlags;
            public Int32 AgeLimit;
            public UInt32 NumberOfBuffers;
            public UInt32 FreeBuffers;
            public UInt32 EventsLost;
            public UInt32 BuffersWritten;
            public UInt32 LogBuffersLost;
            public UInt32 RealTimeBuffersLost;
            public IntPtr LoggerThreadId;
            public UInt32 LogFileNameOffset;
            public UInt32 LoggerNameOffset;
        }

        /// <summary>
        /// EventTraceHeader structure used by EVENT_TRACE_PROPERTIES
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct WNODE_HEADER
        {
            public UInt32 BufferSize;
            public UInt32 ProviderId;
            public UInt64 HistoricalContext;
            public UInt64 TimeStamp;
            public Guid Guid;
            public UInt32 ClientContext;  // Determines the time stamp resolution
            public UInt32 Flags;
        }

        #endregion

        private static void LoadKernelTraceControl()
        {
            if (!s_KernelTraceControlLoaded)
            {
                try
                {
                    string myAssemblyPath = typeof(ETWKernelControl).GetTypeInfo().Assembly.ManifestModule.FullyQualifiedName;
                    string myAssemblyDir = Path.GetDirectoryName(myAssemblyPath);
                    string arch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
                    var kernelTraceControlPath = Path.Combine(Path.Combine(myAssemblyDir, arch), "KernelTraceControl.dll");
                    IntPtr result = LoadLibrary(kernelTraceControlPath);
                    if (result == IntPtr.Zero)
                        throw new Win32Exception();
                }
                catch (BadImageFormatException)
                {
                    throw new BadImageFormatException("Could not load KernelTraceControl.dll (likely 32-64 bit process mismatch)");
                }
                catch (DllNotFoundException)
                {
                    throw new DllNotFoundException("KernelTraceControl.dll missing from distribution.");
                }
            }
            s_KernelTraceControlLoaded = true;
        }

        /// <summary>
        /// Computes the location (offset) where extensions can be put in 'Properties'.  
        /// </summary>
        private static unsafe int ExtensionsOffset(EVENT_TRACE_PROPERTIES* properties, int propertyBuffLength)
        {
            byte* propertyBuff = (byte*)properties;

            // Skip past the LogFileName/LoggerName  Look for the null terminator and add 2.
            int offsetFileName = (int)properties->LogFileNameOffset;
            int offsetLoggerName = (int)properties->LoggerNameOffset;
            int offset = Math.Max(offsetFileName, offsetLoggerName);

            while (offset <= propertyBuffLength - 2)
            {
                if (*((char*)(propertyBuff + offset)) == 0)
                    return offset + 2;
                offset += 2;
            }
            return propertyBuffLength;
        }

        private unsafe static void CopyStringToPtr(char* toPtr, string str)
        {
            fixed (char* fromPtr = str)
            {
                int i = 0;
                while (i < str.Length)
                {
                    toPtr[i] = fromPtr[i];
                    i++;
                }
                toPtr[i] = '\0';   // Null terminate.
            }
        }

        private enum ExtensionItemTypes
        {
            ETW_EXT_ENABLE_FLAGS = 1,
            ETW_EXT_PIDS = 2,
            ETW_EXT_STACKWALK_FILTER = 3,
            ETW_EXT_POOLTAG_FILTER = 4,
            ETW_EXT_STACK_CACHING = 5
        }

        private struct ExtensionItem
        {
            public ExtensionItem(ExtensionItemTypes type) { Type = type; Data = new List<int>(); }
            public ExtensionItemTypes Type;
            public List<int> Data;
        }

        /// <summary>
        /// Saves the given extensions to the Properties structure 'properties' and returns the length that it emitted.
        /// You can pass null for properties and writeLocation, in which case it computes the length needed.
        /// </summary>
        unsafe private static int SaveExtensions(List<ExtensionItem> extensions,
            EVENT_TRACE_PROPERTIES* properties, int writeOffset)
        {
            // Compute the total length
            int lenInBytes = 4;                                             // For the header (count and length).
            foreach (var extension in extensions)
                lenInBytes += 4 + extension.Data.Count * 4;                 // 4 bytes for the header and the data itself.
            Debug.Assert(lenInBytes < 0x10000 * 4);

            if (properties != null && writeOffset != 0)
            {
                int byteOffsetToExtensions = writeOffset;
                Debug.Assert(byteOffsetToExtensions < 0x10000);

                // Indicate that we have extensions
                properties->EnableFlags = (uint)0x80FF0000 | (uint)byteOffsetToExtensions;

                // Write the extension header
                int* ptr = (int*)(((byte*)properties) + writeOffset);
                *ptr++ = (lenInBytes / 4) + (extensions.Count << 16);       // First WORD is total len in DWORDS, next is Extension Count.

                foreach (var extension in extensions)
                {
                    // Write the item header
                    // First WORD is len (including header) in DWORDS, next is Type
                    *ptr++ = (extension.Data.Count + 1) + (((int)extension.Type) << 16);

                    // Write the data 
                    for (int i = 0; i < extension.Data.Count; i++)
                        *ptr++ = extension.Data[i];
                }
                Debug.Assert(((byte*)ptr) - ((byte*)properties) == writeOffset + lenInBytes);
            }
            return lenInBytes;
        }

        /// <summary>
        /// The internal API uses small integer values instead of Guids to represent the stack hooks.
        /// Perform the conversion here.
        /// </summary>
        private static unsafe void PutStacksIntoExtensions(List<ExtensionItem> extensions, STACK_TRACING_EVENT_ID* StackTracingEventIds, int cStackTracingEventIds)
        {
            var converter = new Dictionary<Guid, int>(16);

            // See ntwmi.h
            converter[DiskIOTaskGuid] = 0x1;         // EVENT_TRACE_GROUP_IO
            converter[VirtualAllocTaskGuid] = 0x2;   // EVENT_TRACE_GROUP_MEMORY
            converter[MemoryTaskGuid] = 0x2;
            converter[ProcessTaskGuid] = 0x3;
            converter[FileIOTaskGuid] = 0x4;         // EVENT_TRACE_GROUP_FILE 
            converter[ThreadTaskGuid] = 0x5;
            converter[RegistryTaskGuid] = 0x9;       // EVENT_TRACE_GROUP_REGISTRY 
            converter[PerfInfoTaskGuid] = 0xF;       // EVENT_TRACE_GROUP_PERFINFO     
            converter[ObjectTaskGuid] = 0x11;        // EVENT_TRACE_GROUP_OBJECT

            var stackSpec = new ExtensionItem(ExtensionItemTypes.ETW_EXT_STACKWALK_FILTER);
            while (cStackTracingEventIds > 0)
            {
                int val = (converter[StackTracingEventIds->EventGuid] << 8) + StackTracingEventIds->Type;
                stackSpec.Data.Add(val);
                StackTracingEventIds++;
                --cStackTracingEventIds;
            }

            extensions.Add(stackSpec);
        }

        private static Guid ProcessTaskGuid = new Guid(unchecked((int)0x3d6fa8d0), unchecked((short)0xfe05), unchecked((short)0x11d0), 0x9d, 0xda, 0x00, 0xc0, 0x4f, 0xd7, 0xba, 0x7c);
        private static Guid ThreadTaskGuid = new Guid(unchecked((int)0x3d6fa8d1), unchecked((short)0xfe05), unchecked((short)0x11d0), 0x9d, 0xda, 0x00, 0xc0, 0x4f, 0xd7, 0xba, 0x7c);
        private static Guid DiskIOTaskGuid = new Guid(unchecked((int)0x3d6fa8d4), unchecked((short)0xfe05), unchecked((short)0x11d0), 0x9d, 0xda, 0x00, 0xc0, 0x4f, 0xd7, 0xba, 0x7c);
        private static Guid RegistryTaskGuid = new Guid(unchecked((int)0xae53722e), unchecked((short)0xc863), unchecked((short)0x11d2), 0x86, 0x59, 0x00, 0xc0, 0x4f, 0xa3, 0x21, 0xa1);
        private static Guid FileIOTaskGuid = new Guid(unchecked((int)0x90cbdc39), unchecked((short)0x4a3e), unchecked((short)0x11d1), 0x84, 0xf4, 0x00, 0x00, 0xf8, 0x04, 0x64, 0xe3);
        private static Guid MemoryTaskGuid = new Guid(unchecked((int)0x3d6fa8d3), unchecked((short)0xfe05), unchecked((short)0x11d0), 0x9d, 0xda, 0x00, 0xc0, 0x4f, 0xd7, 0xba, 0x7c);
        private static Guid PerfInfoTaskGuid = new Guid(unchecked((int)0xce1dbfb4), unchecked((short)0x137e), unchecked((short)0x4da6), 0x87, 0xb0, 0x3f, 0x59, 0xaa, 0x10, 0x2c, 0xbc);
        private static Guid VirtualAllocTaskGuid = new Guid(unchecked((int)0x3d6fa8d3), unchecked((short)0xfe05), unchecked((short)0x11d0), 0x9d, 0xda, 0x00, 0xc0, 0x4f, 0xd7, 0xba, 0x7c);
        private static Guid ObjectTaskGuid = new Guid(unchecked((int)0x89497f50), unchecked((short)0xeffe), 0x4440, 0x8c, 0xf2, 0xce, 0x6b, 0x1c, 0xdc, 0xac, 0xa7);

        private static void PutEnableFlagsIntoExtensions(List<ExtensionItem> extensions, KernelKeywords keywords)
        {
            var extendedEnableFlags = new ExtensionItem(ExtensionItemTypes.ETW_EXT_ENABLE_FLAGS);

            if ((keywords & KernelKeywords.ReferenceSet) != 0)     // If ReferenceSet specified, include VAMap and VirtualAlloc.
                keywords |= KernelKeywords.VAMap | KernelKeywords.VirtualAlloc;
            int group0 = ((int)keywords & (int)~KernelTraceEventParser.NonOSKeywords);
            extendedEnableFlags.Data.Add(group0);

            int group1 = 0;
            if ((keywords & KernelKeywords.PMCProfile) != 0)
                group1 |= 0x400;
            if ((keywords & KernelKeywords.Profile) != 0)
                group1 |= 0x002;
            if ((keywords & KernelKeywords.ReferenceSet) != 0)
            {
                //#define PERF_MEMORY          0x20000001   // High level WS manager activities, PFN changes.
                //#define PERF_FOOTPRINT       0x20000008   // Flush WS on every mark_with_flush.
                //#define PERF_MEMINFO         0x20080000
                //#define PERF_MEMINFO_WS      0x20800000   // Logs WorkingSet/Commit information on MemInfo DPC.
                //#define PERF_SESSION         0x20400000
                //#define PERF_REFSET          0x20000020   // PERF_FOOTPRINT + log AutoMark on trace start/stop.

                group1 |= 0xC80029;
            }
            if ((keywords & KernelKeywords.IOQueue) != 0)
            {
                // #define PERF_KERNEL_QUEUE    0x21000000
                group1 |= 0x1000000;
            }

            if ((keywords & KernelKeywords.ThreadPriority) != 0)
            {
                // #define PERF_PRIORITY        0x20002000   // Logs changing of thread priority.
                group1 |= 0x2000;
            }

            extendedEnableFlags.Data.Add(group1);

            int group4 = 0;
            if ((keywords & KernelKeywords.Handle) != 0)
            {
                // #define PERF_OB_HANDLE       0x80000040
                group4 |= 0x40;
            }

            extendedEnableFlags.Data.Add(0); // group 2
            extendedEnableFlags.Data.Add(0);
            extendedEnableFlags.Data.Add(group4); // group 4
            extendedEnableFlags.Data.Add(0);
            extendedEnableFlags.Data.Add(0); // group 6
            extendedEnableFlags.Data.Add(0);
            extensions.Add(extendedEnableFlags);
        }

        /// <summary>
        /// Helper function used to implement EnableWindowsHeapProvider.
        /// </summary>
        private static void SetImageTracingFlags(string sessionName, string exeFileName, bool set)
        {
            // We always want the native registry for the OS (64 bit on a 64 bit machine).

            var registryView = RegistryView.Registry64;
            if (sizeof(IntPtr) == 4 && Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") == null)
                registryView = RegistryView.Default;
            RegistryKey hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView);
            if (hklm == null)
                throw new Exception("Could not open HKLM registry hive on local machine.");

            RegistryKey software = hklm.OpenSubKey(@"SOFTWARE", true);
            if (software == null)
                throw new Exception(@"Could not open HKLM\Software registry hive on local machine for writing.");

            // Mark the fact that we have turned on heap tracing.
            if (set)
            {
                using (RegistryKey perfViewKey = Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\TraceEvent\HeapTracing"))
                {
                    var prevValue = perfViewKey.GetValue(sessionName, null);
                    // Remove any old values.  
                    if (prevValue != null)
                        ResetWindowsHeapTracingFlags(sessionName);
                    perfViewKey.SetValue(sessionName, exeFileName, RegistryValueKind.String);
                }
            }

            // Windows itself will clone these to the Wow3264Node registry keys so we only have to do it once.
            var imageOptionsKeyName = @"Microsoft\Windows NT\CurrentVersion\Image File Execution Options\" + exeFileName;
            using (RegistryKey imageOptions = software.CreateSubKey(imageOptionsKeyName))
            {
                if (set)
                    imageOptions.SetValue("TracingFlags", 1, RegistryValueKind.DWord);
                else
                    imageOptions.DeleteValue("TracingFlags", false);
            }

            software.Dispose();
            hklm.Dispose();
        }

        /// <summary>
        /// The special name for the Kernel session.
        /// </summary>
        private static string KernelSessionName { get { return "NT Kernel Logger"; } }
        private static Guid ProviderGuid = new Guid(unchecked((int)0x9e814aad), unchecked((short)0x3204), unchecked((short)0x11d2), 0x9a, 0x82, 0x00, 0x60, 0x08, 0xa8, 0x69, 0x39);

        //// Code borrowed from CoreFX System.PlatformDetection.Windows to allow targeting netstandard1.6
        //[StructLayout(LayoutKind.Sequential)]
        //private struct RTL_OSVERSIONINFOEX
        //{
        //    internal uint dwOSVersionInfoSize;
        //    internal uint dwMajorVersion;
        //    internal uint dwMinorVersion;
        //    internal uint dwBuildNumber;
        //    internal uint dwPlatformId;
        //    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        //    internal string szCSDVersion;
        //}

        //// Code borrowed from CoreFX System.PlatformDetection.Windows to allow targeting netstandard1.6
        //[DllImport("ntdll.dll")]
        //private static extern int RtlGetVersion(out RTL_OSVERSIONINFOEX lpVersionInformation);

        //// Code borrowed from CoreFX System.PlatformDetection.Windows to allow targeting netstandard1.6
        //private static bool IsWin8orNewer()
        //{
        //    RTL_OSVERSIONINFOEX osvi = new RTL_OSVERSIONINFOEX();
        //    osvi.dwOSVersionInfoSize = (uint)Marshal.SizeOf(osvi);
        //    return osvi.dwMajorVersion * 10 + osvi.dwMinorVersion >= 62;
        //}

        ///// <summary>
        ///// These keywords are can't be passed to the OS.
        ///// </summary>
        //private static KernelKeywords NonOSKeywords
        //{
        //    get
        //    {

        //        var ret = (KernelKeywords)unchecked((int)0xf84c8000);
        //        if (IsWin8orNewer())
        //            ret &= ~KernelKeywords.VAMap;
        //        return ret;
        //    }
        //}

        private static bool s_KernelTraceControlLoaded;
#endregion
    }

    /// <summary>
    /// Used in StartKernelTrace to indicate the kernel events that should have stack traces collected for them.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct STACK_TRACING_EVENT_ID
    {
        public Guid EventGuid;
        public byte Type;
        byte Reserved1;
        byte Reserved2;
        byte Reserved3;
        byte Reserved4;
        byte Reserved5;
        byte Reserved6;
        byte Reserved7;
    }

    /// <summary>
    /// Flags to influence what happens when ETL files are Merged.  
    /// </summary>
    [Flags]
    public enum EVENT_TRACE_MERGE_EXTENDED_DATA
    {
        NONE = 0x00,
        IMAGEID = 0x01,
        BUILDINFO = 0x02,
        VOLUME_MAPPING = 0x04,
        WINSAT = 0x08,
        EVENT_METADATA = 0x10,
        PERFTRACK_METADATA = 0x20,
        NETWORK_INTERFACE = 0x40,
        NGEN_PDB = 0x80,
        COMPRESS_TRACE = 0x10000000,
    }
}