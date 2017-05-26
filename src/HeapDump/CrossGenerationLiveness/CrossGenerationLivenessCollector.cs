#if CROSS_GENERATION_LIVENESS
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Diagnostics.HeapDump;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Runtime.Interop;

namespace Microsoft.Diagnostics.CrossGenerationLiveness
{
    internal delegate CollectionMetadata CaptureDumpDelegate(int processId);

    /// <summary>
    /// Top level object that knows how to collect cross generation liveness data.
    /// </summary>
    internal sealed class CrossGenerationLivenessCollector
    {
        private const int AttachTimeoutInMsec = 60000;
        private const int WaitForEventTimeoutInMsec = 5000;

        private static string s_ProcessArchDirectory;

        private int _PID;
        private int _GenerationToTrigger;
        private ulong _PromotedBytesThreshold;
        private Exception _EncounteredException;
        private bool _CapturedDump;
        private string _DumpFilePath;
        private GCHeapDumper _HeapDumper;
        private CaptureDumpDelegate _CaptureDump;
        private TextWriter _Log;
        private CollectionMetadata _CollectionMetadata;

        public CrossGenerationLivenessCollector(
            int pid,
            int generationToTrigger,
            ulong promotedBytesThreshold,
            string dumpFilePath,
            GCHeapDumper heapDumper,
            CaptureDumpDelegate captureDump,
            TextWriter log)
        {
            if (heapDumper == null)
            {
                throw new ArgumentNullException("heapDumper");
            }
            if (captureDump == null)
            {
                throw new ArgumentNullException("captureDump");
            }
            if (log == null)
            {
                throw new ArgumentNullException("log");
            }

            _PID = pid;
            _GenerationToTrigger = generationToTrigger;
            _PromotedBytesThreshold = promotedBytesThreshold;
            _DumpFilePath = dumpFilePath;
            _CaptureDump = captureDump;
            _Log = log;
        }

        public CollectionMetadata CollectionMetadata
        {
            get { return _CollectionMetadata; }
        }

        public void AttachAndExecute()
        {
            // Explicitly load app-local dependencies.
            LoadNative("dbghelp.dll");
            LoadNative("symsrv.dll");
            LoadNative("dbgeng.dll");

            // Attach to the process.
            using (DataTarget targetProcess = DataTarget.AttachToProcess(_PID, AttachTimeoutInMsec))
            {
                // Create a new debugger wrapper object.
                Debugger debugger = new Debugger(targetProcess, _Log);

                // Subscribe to exception handling.
                debugger.Callbacks.HandleExceptionEvent += HandleException;

                // Dump symbol information and set high verbosity on symbol output.
                debugger.Execute(".sympath");
                debugger.Execute("!sym noisy");

                // Force load the PDB for clr.dll
                debugger.Execute(".reload /f clr.dll");

                // Ensure that the PDB for clr.dll was successfully loaded.  We do this by attempting to load one of the required symbols.
                ulong baseAddress;
                if (debugger.DebugSymbols.GetSymbolModule("clr!SVR::gc_heap::n_heaps", out baseAddress) != 0)
                {
                    throw new Exception("CLR symbols are not properly loaded.  Please set the symbol path and try again.");
                }

                // Load SOS.
                debugger.Execute(".loadby sos clr");

                // Execute find roots, which will throw an exception when we complete the mark phase.
                debugger.Execute("!FindRoots -gen " + _GenerationToTrigger);

                // GO!
                debugger.DebugControl.SetExecutionStatus(DEBUG_STATUS.GO);

                // Loop until we have captured the right number of dumps, or we are no longer attached
                // to the debuggee.
                DEBUG_STATUS debuggerStatus = DEBUG_STATUS.NO_CHANGE;
                int hr;
                do
                {
                    hr = debugger.DebugControl.WaitForEvent(0, WaitForEventTimeoutInMsec);
                    if (hr < 0)
                    {
                        _Log.WriteLine("Failure encountered in WaitForEvent.  HR: {0:x}", hr);
                        _EncounteredException = new Exception("Failure encountered in WaitForEvent.");
                        break;
                    }
                    hr = debugger.DebugControl.GetExecutionStatus(out debuggerStatus);
                    if (hr < 0)
                    {
                        _Log.WriteLine("Failure encountered in GetExecutionStatus.  HR: {0:x}", hr);
                        _EncounteredException = new Exception("Failure encountered in GetExecutionStatus.");
                        break;
                    }
                }
                while ((debuggerStatus != DEBUG_STATUS.NO_DEBUGGEE) && (_EncounteredException == null) && (!_CapturedDump));

                // Once we're done, detach the debuggee so that it doesn't get killed.
                if (debuggerStatus != DEBUG_STATUS.NO_CHANGE)
                {
                    debugger.DebugClient.DetachProcesses();
                }

                // If we encountered an exception, throw it here.
                if (_EncounteredException != null)
                {
                    throw _EncounteredException;
                }
            }
        }

        // Callback that will handle all exceptions.
        private void HandleException(object sender, HandleExceptionEventArgs eventArgs)
        {
            // Determine if this is the exception thrown due to !FindRoots
            if ((eventArgs.FirstChance == 1) && (eventArgs.Exception.ExceptionCode == 0xE0444143))
            {
                // Calculate the promoted bytes and determine if it is over the input threshold.

                // Get the number of server GC heaps.
                int nHeaps;
                if (!eventArgs.Debugger.Evaluate("clr!SVR::gc_heap::n_heaps", out nHeaps))
                {
                    _EncounteredException = new Exception("Unable to get the number of heaps.");
                    return;
                }

                uint promotedBytes = 0;
                if (nHeaps > 0)
                {
                    // Server GC
                    for (uint i = 0; i < nHeaps; ++i)
                    {
                        // Calculate the array offset.
                        uint offset = i * 16; // hardcoded in gc as well.
                        uint result;
                        if (!eventArgs.Debugger.Evaluate("clr!SVR::gc_heap::g_promoted", true, offset, out result))
                        {
                            _EncounteredException = new Exception("Unable to calculate promoted bytes for server GC.");
                            return;
                        }
                        promotedBytes += result;
                    }

                }
                else
                {
                    // Workstation GC
                    if (!eventArgs.Debugger.Evaluate("clr!WKS::gc_heap::g_promoted", false, out promotedBytes))
                    {
                        _EncounteredException = new Exception("Unable to get promoted bytes for workstation GC.");
                        return;
                    }
                }

                _Log.WriteLine("Promoted Bytes: {0}; In MB: {1}", promotedBytes, promotedBytes / 1024 / 1024);

                // If we've exceeded the promoted bytes threshold, take a process dump.
                if (promotedBytes > _PromotedBytesThreshold)
                {
                    // Take a heap snapshot.
                    _CollectionMetadata = _CaptureDump(_PID);
                    _CapturedDump = true;
                }
                else
                {
                    _Log.WriteLine("Skipping GC.");

                    // Execute !FindRoots again.
                    eventArgs.Debugger.Execute("!FindRoots -gen " + _GenerationToTrigger);
                }
            }
        }

        /// <summary>
        /// Load the specified DLL from the proper architecture directory based on the 
        /// architecture of the current process.
        /// </summary>
        public static void LoadNative(string relativePath)
        {
            var fullPath = Path.Combine(RootDir, ProcessArchitectureDirectory, relativePath);
            var ret = LoadLibrary(fullPath);
            if (ret == IntPtr.Zero)
            {
                throw new ApplicationException("Unable to load " + relativePath + ".");
            }
        }

        /// <summary>
        /// Get the name of the architecture of the current process.
        /// </summary>
        private static ProcessorArchitecture ProcessArch
        {
            get
            {
                return Environment.Is64BitProcess ? ProcessorArchitecture.Amd64 : ProcessorArchitecture.X86;
            }
        }

        /// <summary>
        /// Gets the name of the directory containing compiled binaries (DLLs) which have the same architecture as the
        /// currently executing process.
        /// </summary>
        public static string ProcessArchitectureDirectory
        {
            get
            {
                if (s_ProcessArchDirectory == null)
                {
                    s_ProcessArchDirectory = ProcessArch.ToString().ToLowerInvariant();
                }

                return s_ProcessArchDirectory;
            }
        }

        private static string RootDir
        {
            get
            {
                // Get the full path of the directory containing this executable.
                string path = System.IO.Path.GetDirectoryName(
                    System.Reflection.Assembly.GetExecutingAssembly().Location);

                // Strip off the trailing '\' if necessary.
                if (path[path.Length - 1] == Path.DirectorySeparatorChar)
                {
                    path = path.Substring(0, path.Length - 1);
                }

                // Strip off the last directory in the path.
                path = path.Substring(
                    0,
                    path.LastIndexOf(Path.DirectorySeparatorChar));

                return path;
            }
            
        }

        [System.Runtime.InteropServices.DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);
    }
}
#endif