using Microsoft.Diagnostics.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security;
using Utilities;

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
            {
                throw new ApplicationException("Must be Administrator (elevated).");
            }

            var arch = GetArchForProcess(processID);
            if (log != null)
            {
                log.WriteLine("Starting Heap dump on Process {0} running architecture {1}.", processID, arch);
            }

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
            {
                throw new ApplicationException("Must be Administrator (elevated) to use Force GC option.");
            }

            var arch = GetArchForProcess(processID);
            if (log != null)
            {
                log.WriteLine("Starting Heap dump on Process {0} running architecture {1}.", processID, arch);
            }

            var heapDumpExe = Path.Combine(SupportFiles.SupportFileDir, arch + @"\HeapDump.exe");
            var options = new CommandOptions().AddNoThrow().AddTimeout(1 * 3600 * 1000);
            if (log != null)
            {
                options.AddOutputStream(log);
            }

            // Add SymbolsAuth argument if specified
            var symbolsAuthArg = "";
            if (App.CommandLineArgs.SymbolsAuth != SymbolsAuthenticationType.Interactive)
            {
                symbolsAuthArg = $" /SymbolsAuth:{App.CommandLineArgs.SymbolsAuth.ToString().Replace(" ", "")}";
            }

            var commandLine = string.Format("\"{0}\"{1} /ForceGC {2}", heapDumpExe, symbolsAuthArg, processID.ToString());
            log.WriteLine("Exec: {0}", commandLine);
            var cmd = Command.Run(commandLine, options);
            if (cmd.ExitCode != 0)
            {
                throw new ApplicationException("HeapDump failed with exit code " + cmd.ExitCode + ".  See log for details.");
            }
        }

        /// <summary>
        /// Take a heap dump from a process dump
        /// </summary>
        public static void DumpGCHeap(string processDumpFile, string outputFile, TextWriter log, string qualifiers = "")
        {
            // Determine if we are on a 64 bit system.
            if (Environment.Is64BitOperatingSystem)
            {
                // TODO FIX NOW.   Find a way of determing which architecture a dump is
                try
                {
                    log.WriteLine("********** TRYING TO OPEN THE DUMP AS 64 BIT ************");
                    DumpGCHeap("/processDump " + qualifiers, processDumpFile, outputFile, log, ProcessorArchitecture.Amd64);
                    return; // Yeah! success the first time
                }
                catch (ApplicationException)
                {
                    // It might have failed because this was a 32 bit dump, if so try again.  
                    log.WriteLine("********** TRYING TO OPEN THE DUMP AS 32 BIT ************");
                    DumpGCHeap("/processDump" + qualifiers, processDumpFile, outputFile, log, ProcessorArchitecture.X86);
                    return;
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
            {
                processNameOrID = processNameOrID.Substring(0, processNameOrID.Length - 4);
            }

            Process youngestProcess = null;
            foreach (var process in Process.GetProcessesByName(processNameOrID))
            {
                if (youngestProcess == null || process.StartTime > youngestProcess.StartTime)
                {
                    youngestProcess = process;
                }
            }
            if (youngestProcess != null)
            {
                return youngestProcess.Id;
            }

            return -1;
        }

        #region private
        private static void DumpGCHeap(string qualifiers, string inputArg, string outputFile, TextWriter log, ProcessorArchitecture arch)
        {
            var directory = arch == ProcessorArchitecture.X86 ? "x86" : "amd64";
            var heapDumpExe = Path.Combine(SupportFiles.SupportFileDir, Path.Combine(directory, "HeapDump.exe"));

            var options = new CommandOptions().AddNoThrow().AddTimeout(CommandOptions.Infinite);
            if (log != null)
            {
                options.AddOutputStream(log);
            }

            // TODO breaking abstraction to know about StackWindow. 
            options.AddEnvironmentVariable("_NT_SYMBOL_PATH", App.SymbolPath);
            log.WriteLine("set _NT_SYMBOL_PATH={0}", App.SymbolPath);

            // Add SymbolsAuth argument if specified
            var symbolsAuthArg = "";
            if (App.CommandLineArgs.SymbolsAuth != SymbolsAuthenticationType.Interactive)
            {
                symbolsAuthArg = $" /SymbolsAuth:{App.CommandLineArgs.SymbolsAuth.ToString().Replace(" ", "")}";
            }

            var commandLine = string.Format("\"{0}\"{1} {2} \"{3}\" \"{4}\"", heapDumpExe, symbolsAuthArg, qualifiers, inputArg, outputFile);
            log.WriteLine("Exec: {0}", commandLine);
            PerfViewLogger.Log.TriggerHeapSnapshot(outputFile, inputArg, qualifiers);
            var cmd = Command.Run(commandLine, options);
            if (cmd.ExitCode == 3)
            {
                throw new ApplicationException("Unable to open the process dump.  PerfView only supports converting Windows process dumps.  Please confirm that this is a Windows process dump.");
            }
            else if (cmd.ExitCode != 0)
            {
                throw new ApplicationException("HeapDump failed with exit code " + cmd.ExitCode);
            }

            if (log != null)
            {
                log.WriteLine("Completed Heap Dump for {0} to {1}", inputArg, outputFile);
            }
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
                {
                    return ProcessorArchitecture.X86;
                }

                bool is32Bit = false;
                bool ret = IsWow64Process(process.Handle, out is32Bit);
                GC.KeepAlive(process);
                if (ret)
                {
                    return is32Bit ? ProcessorArchitecture.X86 : ProcessorArchitecture.Amd64;
                }
            }
            catch (System.Runtime.InteropServices.ExternalException e)
            {
                if ((uint)e.ErrorCode == 0x80004005)
                {
                    throw new ApplicationException("Access denied to inspect process (Not Elevated?).");
                }
            }
            catch (Exception) { }
            throw new ApplicationException("Could not determine the process architecture for process with ID " + processID);
        }

        [DllImport("kernel32.dll", SetLastError = true), SuppressUnmanagedCodeSecurityAttribute]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWow64Process(
             [In] IntPtr processHandle,
             [Out, MarshalAs(UnmanagedType.Bool)] out bool wow64Process);

        #endregion
    }
}
