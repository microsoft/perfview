using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using Utilities;
using Address = System.UInt64;
using PerfView;

namespace PerfView
{
    public class HeapDumper
    {
        /// <summary>
        /// Take a heap dump from a live process. 
        /// </summary>
        public static void DumpGCHeap(int processID, string outputFile, TextWriter log = null, string qualifiers = "")
        {
            if (!App.IsElevated)
                throw new ApplicationException("Must be Administrator (elevated).");

            var arch = GetArchForProcess(processID);
            if (log != null)
                log.WriteLine("Starting Heap dump on Process {0} running architecture {1}.", processID, arch);

            DumpGCHeap(qualifiers, processID.ToString(), outputFile, log, arch);
            log.WriteLine("Finished Heap Dump.");
        }

#if CROSS_GENERATION_LIVENESS
        /// <summary>
        /// Take a heap dump from a live process. 
        /// </summary>
        public static void DumpGCHeapForCrossGenerationLiveness(int processID, int generationToTrigger, ulong promotedBytesThreshold, string outputFile, TextWriter log = null, string qualifiers = "")
        {
            if (!App.IsElevated)
                throw new ApplicationException("Must be Administrator (elevated).");

            var arch = GetArchForProcess(processID);
            if (log != null)
                log.WriteLine("Starting Heap dump for cross generation liveness on Process {0} running architecture {1}.", processID, arch);

            qualifiers += " /PromotedBytesThreshold:" + promotedBytesThreshold;
            qualifiers += " /GenerationToTrigger:" + generationToTrigger;
            DumpGCHeap(qualifiers, processID.ToString(), outputFile, log, arch);
            log.WriteLine("Finished Heap Dump.");
        }
#endif

        /// <summary>
        /// Force a GC on process processID
        /// </summary>
        internal static void ForceGC(int processID, TextWriter log = null)
        {
            // We force a GC by turning on an ETW provider, which needs admin to do.  
            if (!App.IsElevated)
                throw new ApplicationException("Must be Administrator (elevated) to use Force GC option.");

            var arch = GetArchForProcess(processID);
            if (log != null)
                log.WriteLine("Starting Heap dump on Process {0} running architecture {1}.", processID, arch);

            var heapDumpExe = Path.Combine(SupportFiles.SupportFileDir, arch + @"\HeapDump.exe");
            var options = new CommandOptions().AddNoThrow().AddTimeout(1 * 3600 * 1000);
            if (log != null)
                options.AddOutputStream(log);

            var commandLine = string.Format("\"{0}\" /ForceGC {1}", heapDumpExe, processID.ToString());
            log.WriteLine("Exec: {0}", commandLine);
            var cmd = Command.Run(commandLine, options);
            if (cmd.ExitCode != 0)
                throw new ApplicationException("HeapDump failed with exit code " + cmd.ExitCode + ".  See log for details.");
        }

        /// <summary>
        /// Take a heap dump from a process dump
        /// </summary>
        public static void DumpGCHeap(string processDumpFile, string outputFile, TextWriter log, string qualifiers = "")
        {
            // Determine if we are on a 64 bit system.
            if (Environment.Is64BitOperatingSystem)
            {
                bool isDump64 = DumpReader.IsDump64(processDumpFile).GetValueOrDefault();
                if (isDump64)
                {
                    log.WriteLine("********** OPENING THE DUMP AS 64 BIT ************");
                    DumpGCHeap("/processDump " + qualifiers, processDumpFile, outputFile, log, ProcessorArchitecture.Amd64);
                }
                else
                {
                    log.WriteLine("********** OPENING THE DUMP AS 32 BIT ************");
                    DumpGCHeap("/processDump" + qualifiers, processDumpFile, outputFile, log, ProcessorArchitecture.X86);
                }
            }
            else
            {
                DumpGCHeap("/processDump", processDumpFile, outputFile, log, ProcessorArchitecture.X86);
            }
        }
        /// <summary>
        /// Given a name or a process ID, return the process ID for it.  If it is a name
        /// it will return the youngest process ID for all processes with that
        /// name.   Returns a negative ID if the process is not found.  
        /// </summary>
        public static int GetProcessID(string processNameOrID)
        {
            int parsedInt;
            if (int.TryParse(processNameOrID, out parsedInt))
            {
                var process = Process.GetProcessById(parsedInt);
                if (process != null)
                {
                    process.Dispose();
                    return parsedInt;
                }
            }
            // It is a name find the youngest process with that name.  
            // remove .exe if present.
            if (processNameOrID.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                processNameOrID = processNameOrID.Substring(0, processNameOrID.Length - 4);
            Process youngestProcess = null;
            foreach (var process in Process.GetProcessesByName(processNameOrID))
            {
                if (youngestProcess == null || process.StartTime > youngestProcess.StartTime)
                    youngestProcess = process;
            }
            if (youngestProcess != null)
                return youngestProcess.Id;

            return -1;
        }

        #region private
        private static void DumpGCHeap(string qualifiers, string inputArg, string outputFile, TextWriter log, ProcessorArchitecture arch)
        {
            var directory = arch == ProcessorArchitecture.X86 ? "x86" : "amd64";
            var heapDumpExe = Path.Combine(SupportFiles.SupportFileDir, Path.Combine(directory, "HeapDump.exe"));

            var options = new CommandOptions().AddNoThrow().AddTimeout(CommandOptions.Infinite);
            if (log != null)
                options.AddOutputStream(log);

            // TODO breaking abstraction to know about StackWindow. 
            options.AddEnvironmentVariable("_NT_SYMBOL_PATH", App.SymbolPath);
            log.WriteLine("set _NT_SYMBOL_PATH={0}", App.SymbolPath);

            var commandLine = string.Format("\"{0}\" {1} \"{2}\" \"{3}\"", heapDumpExe, qualifiers, inputArg, outputFile);
            log.WriteLine("Exec: {0}", commandLine);
            PerfViewLogger.Log.TriggerHeapSnapshot(outputFile, inputArg, qualifiers);
            var cmd = Command.Run(commandLine, options);
            if (cmd.ExitCode != 0)
                throw new ApplicationException("HeapDump failed with exit code " + cmd.ExitCode);

            if (log != null)
                log.WriteLine("Completed Heap Dump for {0} to {1}", inputArg, outputFile);
        }

        /// <summary>
        /// Returns processor architecture for a process with a specific process ID.
        /// </summary>
        private static ProcessorArchitecture GetArchForProcess(int processID)
        {
            try
            {
                // To make error paths simple always try to access the process here even though we don't need it
                // for a 32 bit machine.
                var process = Process.GetProcessById(processID);

                // Currently only AMD64 has a wow.
                if (!Environment.Is64BitOperatingSystem)
                    return ProcessorArchitecture.X86;

                bool is32Bit = false;
                bool ret = IsWow64Process(process.Handle, out is32Bit);
                GC.KeepAlive(process);
                if (ret)
                    return is32Bit ? ProcessorArchitecture.X86 : ProcessorArchitecture.Amd64;
            }
            catch (System.Runtime.InteropServices.ExternalException e)
            {
                if ((uint)e.ErrorCode == 0x80004005)
                    throw new ApplicationException("Access denied to inspect process (Not Elevated?).");
            }
            catch (Exception) { }
            throw new ApplicationException("Could not determine the process architecture for process with ID " + processID);
        }

        [DllImport("kernel32.dll", SetLastError = true), SuppressUnmanagedCodeSecurityAttribute]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWow64Process(
             [In] IntPtr processHandle,
             [Out, MarshalAs(UnmanagedType.Bool)] out bool wow64Process);

        /// <summary>
        /// Knows how to open a process dump and read the headers to determine its bitness
        /// </summary>
        private static class DumpReader
        {
            /// <summary>
            /// opens the provided dump and determines its bitness
            /// </summary>
            /// <param name="dumpFileName">the file name of the dump</param>
            /// <returns></returns>
            public static bool? IsDump64(string dumpFileName)
            {
                using (BinaryReader binaryReader = new BinaryReader(File.OpenRead(dumpFileName)))
                {
                    binaryReader.BaseStream.Seek(0, SeekOrigin.Begin);
                    var dumpHeader = ReadStruct<MINIDUMP_HEADER>(binaryReader);

                    if (dumpHeader.Signature != MINIDUMP_HEADER.MinidumpSignature)
                        return null;

                    uint directoryOffset = dumpHeader.StreamDirectoryRVA;

                    for (int streamNumber = 0; streamNumber < dumpHeader.NumberOfStreams; streamNumber++)
                    {
                        binaryReader.BaseStream.Seek(directoryOffset, SeekOrigin.Begin);
                        MINIDUMP_DIRECTORY entry = ReadStruct<MINIDUMP_DIRECTORY>(binaryReader);
                        if (entry.StreamType == MINIDUMP_STREAM_TYPE.SystemInfoStream && entry.DataSize > Marshal.SizeOf(typeof(MINIDUMP_SYSTEM_INFO)))
                        {
                            binaryReader.BaseStream.Seek(entry.Rva, SeekOrigin.Begin);
                            MINIDUMP_SYSTEM_INFO dumpSystemInfo = ReadStruct<MINIDUMP_SYSTEM_INFO>(binaryReader);

                            return dumpSystemInfo.ProcessorArchitecture == ProcessorArchitecture.PROCESSOR_ARCHITECTURE_AMD64 ||
                                   dumpSystemInfo.ProcessorArchitecture == ProcessorArchitecture.PROCESSOR_ARCHITECTURE_ARM64;
                        }
                        directoryOffset += (uint)Marshal.SizeOf(typeof(MINIDUMP_DIRECTORY));
                    }
                    return null;
                }
            }

            private static T ReadStruct<T>(BinaryReader binaryReader)
            {
                int sizeOfStruct = Marshal.SizeOf(typeof(T));
                byte[] buffer = binaryReader.ReadBytes(sizeOfStruct);
                GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
                try
                {
                    T ret = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
                    return ret;
                }
                finally
                {
                    handle.Free();
                }
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct MINIDUMP_HEADER
            {
                public UInt32 Signature;
                public UInt32 Version;
                public UInt32 NumberOfStreams;
                public UInt32 StreamDirectoryRVA;
                public UInt32 CheckSum;
                public UInt32 Reserved;
                public UInt64 Flags;

                public const UInt32 MinidumpSignature = 0x504d444d; // 'MDMP'
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct MINIDUMP_DIRECTORY
            {
                public MINIDUMP_STREAM_TYPE StreamType;
                public UInt32 DataSize;
                public UInt32 Rva;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct MINIDUMP_SYSTEM_INFO
            {
                public ProcessorArchitecture ProcessorArchitecture;
                public UInt16 ProcessorLevel;
                public UInt16 ProcessorRevision;
                // There are more fields here, but we don't care about them.
            }

            private enum ProcessorArchitecture : UInt16
            {
                PROCESSOR_ARCHITECTURE_INTEL = 0,
                PROCESSOR_ARCHITECTURE_ARM = 5,
                PROCESSOR_ARCHITECTURE_AMD64 = 9,
                PROCESSOR_ARCHITECTURE_ARM64 = 12,
            }

            private enum MINIDUMP_STREAM_TYPE : uint
            {
                UnusedStream = 0,
                ReservedStream0 = 1,
                ReservedStream1 = 2,
                ThreadListStream = 3,
                ModuleListStream = 4,
                MemoryListStream = 5,
                ExceptionStream = 6,
                SystemInfoStream = 7,
                ThreadExListStream = 8,
                Memory64ListStream = 9,
                CommentStreamA = 10,
                CommentStreamW = 11,
                HandleDataStream = 12,
                FunctionTableStream = 13,
                UnloadedModuleListStream = 14,
                MiscInfoStream = 15,
                MemoryInfoListStream = 16,
                ThreadInfoListStream = 17,
                HandleOperationListStream = 18,
                TokenStream = 19,
                JavaScriptDataStream = 20,
                SystemMemoryInfoStream = 21,
                ProcessVmCountersStream = 22,

                ceStreamNull = 0x8000,
                ceStreamSystemInfo = 0x8001,
                ceStreamException = 0x8002,
                ceStreamModuleList = 0x8003,
                ceStreamProcessList = 0x8004,
                ceStreamThreadList = 0x8005,
                ceStreamThreadContextList = 0x8006,
                ceStreamThreadCallStackList = 0x8007,
                ceStreamMemoryVirtualList = 0x8008,
                ceStreamMemoryPhysicalList = 0x8009,
                ceStreamBucketParameters = 0x800A,

                LastReservedStream = 0xffff
            }
        }

        #endregion
    }
}
