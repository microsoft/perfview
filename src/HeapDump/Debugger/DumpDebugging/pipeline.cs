//---------------------------------------------------------------------
//  This file is part of the CLR Managed Debugger (mdbg) Sample.
// 
//  Copyright (C) Microsoft Corporation.  All rights reserved.
//
// Part of managed wrappers for native debugging APIs.
//---------------------------------------------------------------------




namespace Microsoft.Samples.Debugging.Native
{


    /// <summary>
    /// Describes the ProcessorArchitecture in a SYSTEM_INFO field.
    /// This can also be reported by a dump file.
    /// </summary>
    public enum ProcessorArchitecture : ushort
    {
        PROCESSOR_ARCHITECTURE_INTEL = 0,
        PROCESSOR_ARCHITECTURE_MIPS = 1,
        PROCESSOR_ARCHITECTURE_ALPHA = 2,
        PROCESSOR_ARCHITECTURE_PPC = 3,
        PROCESSOR_ARCHITECTURE_SHX = 4,
        PROCESSOR_ARCHITECTURE_ARM = 5,
        PROCESSOR_ARCHITECTURE_IA64 = 6,
        PROCESSOR_ARCHITECTURE_ALPHA64 = 7,
        PROCESSOR_ARCHITECTURE_MSIL = 8,
        PROCESSOR_ARCHITECTURE_AMD64 = 9,
        PROCESSOR_ARCHITECTURE_IA32_ON_WIN64 = 10,
    }

#if false

    /// <summary>
    /// Abstracts creation on the pipeline.
    /// </summary>
    /// <param name="pipeline"></param>
    /// <returns>a native process object on this pipeline</returns>
    public delegate NativeDbgProcess StartDebugProcessMethod(NativePipeline pipeline);

    /// <summary>
    /// This encapsulates a set of processes being debugged with the native debugging pipeline,
    /// and the wait primitives to get native debug events from these processes.
    /// </summary>
    /// <remarks>
    /// This is single-threaded. The underlying OS APIs must all be called on the same thread.
    /// Multiple instances can exist on different threads.
    /// Only one pipeline object should exist on a given thread.
    /// </remarks>
    public sealed class NativePipeline : IDisposable
    {
        /// <summary>
        /// Processor architecture (x86, amd64, ia64, etc) that the pipeline is running on.
        /// </summary>
        public static ProcessorArchitecture Architecture
        {
            get
            {
                NativeMethods.SYSTEM_INFO info;
                NativeMethods.GetSystemInfo(out info);

                return (info.wProcessorArchitecture);
            }
        }

    #region KillOnExit
        /// <summary>
        /// Do outstanding debuggees get automatically deleted when the debugger exits?
        /// </summary>
        /// <remarks>
        /// Default is 'True'. Only available in WinXp/Win2k03 and beyond.
        /// This corresponds to kernel32!DebugSetProcessKillOnExit()
        /// If somebody calls DebugSetProcessKillOnExit directly on this thread, then the values
        /// will be incorrect.
        /// </remarks>
        public bool KillOnExit
        {
            get
            {
                return m_KillOnExit;
            }
            set
            {
                m_KillOnExit = value;
                NativeMethods.DebugSetProcessKillOnExit(value);
            }
        }
        // Remember value of DebugSetProcessKillOnExit.
        // This defaults to true.
        bool m_KillOnExit = true;
    #endregion KillOnExit


    #region Thread Safety
        // Thread
        int m_Win32EventThreadId = NativeMethods.GetCurrentThreadId();

        /// <summary>
        /// Win32 Debugging APIs on single-threaded. Throw if this is not on the win32 event thread.
        /// </summary>
        void EnsureIsOnWin32EventThread()
        {
            int currentThread = NativeMethods.GetCurrentThreadId();
            if (m_Win32EventThreadId != currentThread)
            {
                throw new InvalidOperationException("The debug event thread is " + m_Win32EventThreadId + ". Can't call debug event APIs for this pipeline on other threads.");
            }
        }

    #endregion

    #region track list of processes
        // Mapping of pids to NativeDbgProcess objects.
        // This lets us hand back rich process objects instead of pids.
        Dictionary<int, NativeDbgProcess> m_processes = new Dictionary<int, NativeDbgProcess>();

        NativeDbgProcess CreateNew(int processId)
        {
            NativeDbgProcess process = new NativeDbgProcess(processId);
            m_processes[processId] = process;
            return process;
        }

        // Useful for picking up processes from debug events that this pipeline
        // didn't explicitly create (such as child process debugging)
        internal NativeDbgProcess GetOrCreateProcess(int processId)
        {
            NativeDbgProcess proc;
            if (m_processes.TryGetValue(processId, out proc))
            {
                return proc;
            }
            else
            {
                return CreateNew(processId);
            }
        }

        /// <summary>
        /// Get the process object for the given pid.
        /// </summary>
        /// <param name="processId">OS process id of process</param>
        /// <returns></returns>
        /// <exception>Throws if process is no longer deing debugger. This is the 
        /// case if you detached the process or after the ExitProcess debug event has been continued</exception>
        public NativeDbgProcess GetProcess(int processId)
        {
            NativeDbgProcess proc;
            if (m_processes.TryGetValue(processId, out proc))
            {
                return proc;
            }
            else
            {
                throw new InvalidOperationException("Process " + processId + " is not being debugged by this pipeline. The process may have exited or been detached from.");
            }
        }

        // Remove a process from the collection.
        internal void RemoveProcess(int pid)
        {
            GetProcess(pid).Dispose();
            m_processes.Remove(pid);
        }

    #endregion // track list of processes


    #region Connect

        /// <summary>
        /// Attach to the given process. Throws on error. 
        /// </summary>
        /// <param name="processId">process ID of target process to attach to</param>
        /// <returns>process object representing process being debugged</returns>
        public NativeDbgProcess Attach(int processId)
        {
            EnsureIsOnWin32EventThread();
            bool fAttached = NativeMethods.DebugActiveProcess((uint)processId);
            if (!fAttached)
            {
                int err = Marshal.GetLastWin32Error();
                throw new InvalidOperationException("Failed to attach to process id " + processId + "error=" + err);
            }

            return CreateNew(processId);
        }

        /// <summary>
        /// Create a process under the debugger, and include debugging any
        /// child processes
        /// </summary>
        /// <param name="application"></param>
        /// <param name="commandArgs"></param>
        /// <returns></returns>
        public NativeDbgProcess CreateProcessChildDebug(string application, string commandArgs)
        {
            return CreateProcessDebugWorker(application, commandArgs,
                NativeMethods.CreateProcessFlags.DEBUG_PROCESS);
        }


        
        /// <summary>
        /// Creates a new process under this debugging pipeline.
        /// </summary>
        /// <param name="application">application to launch</param>
        /// <param name="commandArgs">arguments (not including the application name) to pass to the debuggee.</param>
        /// <returns>NativeDbgProcess instance for newly created process</returns>
        /// <seealso cref="Attach"/>
        /// <remarks>Pump the process for debug events by calling WaitForDebugEvent.
        /// Create a process under the debugger
        /// commandArgs are the arguments to application. Does not need to include arg[0] (the application name).</remarks>
        public NativeDbgProcess CreateProcessDebug(string application, string commandArgs)
        {
            return CreateProcessDebugWorker(application, commandArgs,
                NativeMethods.CreateProcessFlags.DEBUG_PROCESS |
                NativeMethods.CreateProcessFlags.DEBUG_ONLY_THIS_PROCESS);
        }


        /// <summary>
        /// Creates a new process under this debugging pipeline.
        /// </summary>
        /// <param name="application">raw application to launch. Passed directly to kernel32!CreateProcess.</param>
        /// <param name="commandArgs">raw arguments to pass to the debuggee. Passed directly to kernel32!CreateProcess.</param>
        /// <param name="newConsole">true if the debuggee should get a new console, else false</param>
        /// <param name="debugChild">true if this should debug child processes, else false to debug just the
        /// launched processes.</param>
        /// <returns>NativeDbgProcess instance for newly created process</returns>
        /// <seealso cref="Attach"/>
        /// <remarks>Pump the process for debug events by calling WaitForDebugEvent.
        /// Create a process under the debugger.
        /// This passes application and commandArgs directly to Passed directly to kernel32!CreateProcess and
        /// does not do any filtering on them.</remarks>
        public NativeDbgProcess CreateProcessDebugRaw(string application, string commandArgs, bool newConsole, bool debugChild)
        {
            // This is a pretty rich overload. We should considering just using a rich structure (like
            // ProcessStartInfo) instead of ever growign signatures. ProcessStartInfo isn't perfect:
            // - it's missing some flags like child-process debugging.
            // - it has extra flags like UseShellExecute.
            
            NativeMethods.CreateProcessFlags flags = NativeMethods.CreateProcessFlags.DEBUG_PROCESS;
            if (!debugChild)
            {
                flags |= NativeMethods.CreateProcessFlags.DEBUG_ONLY_THIS_PROCESS;
            }
            if (newConsole)
            {
                flags |= NativeMethods.CreateProcessFlags.CREATE_NEW_CONSOLE;
            }

            return CreateProcessDebugRawWorker(application, commandArgs, flags);
        }

        // Adjusts names.
        NativeDbgProcess CreateProcessDebugWorker(string application, string commandArgs, Microsoft.Samples.Debugging.Native.NativeMethods.CreateProcessFlags flags)
        {
            if (application == null)
            {
                throw new ArgumentException("can't be null", "application");
            }
            // Compensate for Win32's behavior, where arg[0] is the application name.
            if (commandArgs != null)
            {
                commandArgs = application + " " + commandArgs;
            }

            flags |= NativeMethods.CreateProcessFlags.CREATE_NEW_CONSOLE;
            return CreateProcessDebugRawWorker(application, commandArgs, flags);
        }

        // No further mangling.
        NativeDbgProcess CreateProcessDebugRawWorker(string application, string commandArgs, Microsoft.Samples.Debugging.Native.NativeMethods.CreateProcessFlags flags)
        {
            if (application == null)
            {
                throw new ArgumentNullException("application");
            }

            EnsureIsOnWin32EventThread();

            // This is using definition imports from Mdbg core, where these are classes.
            PROCESS_INFORMATION pi = new PROCESS_INFORMATION(); // class

            STARTUPINFO si = new STARTUPINFO(); // struct


            bool createOk = NativeMethods.CreateProcess(
                application,
                commandArgs,
                IntPtr.Zero, // process attributes
                IntPtr.Zero, // thread attributes
                false, // inherit handles,
                flags,
                IntPtr.Zero, // env block
                null, // current dir
                si,
                pi);

            if (!createOk)
            {
                int err = Marshal.GetLastWin32Error();
                if (err == 2) // file not found
                {
                    if (!File.Exists(application))
                        throw new FileNotFoundException(application + " could not be found");
                }
                throw new InvalidOperationException("Failed to create process '" + application + "'. error=" + err);
            }

            // We'll close these handle now. We'll get them again from the CreateProcess debug event.
            NativeMethods.CloseHandle(pi.hProcess);
            NativeMethods.CloseHandle(pi.hThread);

            return CreateNew(pi.dwProcessId);
        }

        /// <summary>
        /// Stop debugging the specified process (detach)
        /// </summary>
        /// <param name="process">process to detach from</param>
        /// <remarks>After detaching, the process is removed from the caches and can not be accessed. If detaching at a debug
        /// event, do not call Continue on the event. </remarks>
        public void Detach(NativeDbgProcess process)
        {
            if (process == null)
            {
                throw new ArgumentNullException("process");
            }
            EnsureIsOnWin32EventThread();

            int pid = process.Id;
            bool fDetachOk = NativeMethods.DebugActiveProcessStop((uint)pid);
            if (!fDetachOk)
            {
                int err = Marshal.GetLastWin32Error();
                throw new InvalidOperationException("Failed to detach to process " + pid + "error=" + err);
            }
            RemoveProcess(pid);
        }


    #endregion // Connect

    #region Stop/Go
        /// <summary>
        /// Waits for a debug event from any of the processes in the wait set.
        /// </summary>
        /// <param name="timeout">timeout in milliseconds to wait. If 0, checks for a debug event and returns immediately</param>
        /// <returns>Null if no event is available</returns>
        /// <remarks>Debug events should be continued by calling ContinueEvent. The debuggee is completely stopped when a
        /// debug event is dispatched and until it is continued.</remarks>
        public NativeEvent WaitForDebugEvent(int timeout)
        {
            EnsureIsOnWin32EventThread();
            bool fHasEvent;
            if (IntPtr.Size == sizeof(Int32))
            {
                DebugEvent32 event32 = new DebugEvent32();
                fHasEvent = NativeMethods.WaitForDebugEvent32(ref event32, timeout);
                if (fHasEvent)
                {
                    return NativeEvent.Build(this, ref event32.header, ref event32.union);
                }
            }
            else
            {
                DebugEvent64 event64 = new DebugEvent64();
                fHasEvent = NativeMethods.WaitForDebugEvent64(ref event64, timeout);
                if (fHasEvent)
                {
                    return NativeEvent.Build(this, ref event64.header, ref event64.union);
                }
            }

            // Not having an event could be a timeout, or it could be a real failure.
            // Empirically, timeout produces GetLastError()=121 (ERROR_SEM_TIMEOUT), but MSDN doesn't spec that, so 
            // we don't want to rely on it. So if we don't have an event, just return NULL and
            // don't try to probe any further.
            return null;
        }

        /// <summary>
        /// Wait forever for a debug event from a process. 
        /// </summary>
        /// <returns>event</returns>
        /// <exception cref="System.InvalidOperationException">throws on failure. Since this waits forever, not having a debug event means we must have hit some error </exception>
        /// <seealso cref="WaitForDebugEvent"/>
        /// <remarks>
        /// All pipeline functions must be called on the same thread.
        /// </remarks>
        public NativeEvent WaitForDebugEventInfinite()
        {
            EnsureIsOnWin32EventThread();
            // Ensure that we're debugging at least 1 process before we wait forever.
            if (m_processes.Count == 0)
            {
                throw new InvalidOperationException("Pipeline is not debugging any processes. Waiting for a debug event will hang.");
            }

            // Pass -1 to timeout to wait forever
            NativeEvent nativeEvent = WaitForDebugEvent(-1);
            if (nativeEvent == null)
            {
                throw new InvalidOperationException("WaitForDebugEvent failed for non-timeout reason");
            }
            return nativeEvent;
        }

        /// <summary>
        /// Continue a debug event previously gotten by WaitForDebugEvent
        /// </summary>
        /// <param name="nativeEvent"></param>
        /// <remarks>Can't continue a debug event if we just detached from the process</remarks>
        public void ContinueEvent(NativeEvent nativeEvent)
        {
            if (nativeEvent == null)
            {
                throw new ArgumentNullException("nativeEvent");
            }
            if (nativeEvent.ContinueStatus == NativeMethods.ContinueStatus.CONTINUED)
            {
                throw new ArgumentException("event was already continued", "nativeEvent");
            }
            if (nativeEvent.Pipeline != this)
            {
                throw new ArgumentException("event does not belong to this pipeline");
            }
            EnsureIsOnWin32EventThread();

            // Verify that the process for this event is still connected to our pipeline.
            // The lookup will throw if the process detached or was terminated.
            NativeDbgProcess proc = nativeEvent.Process;
            Debug.Assert(proc.Id == nativeEvent.ProcessId);


            nativeEvent.DoCleanupForContinue();

            bool fContinueOk = NativeMethods.ContinueDebugEvent((uint)nativeEvent.ProcessId, (uint)nativeEvent.ThreadId, nativeEvent.ContinueStatus);
            if (!fContinueOk)
            {
                int err = Marshal.GetLastWin32Error();
                throw new InvalidOperationException("Continue failed on process " + nativeEvent.ProcessId + " error=" + err);
            }

            // Mark as continued so that we don't accidentally continue again.
            nativeEvent.ContinueStatus = NativeMethods.ContinueStatus.CONTINUED;
        }
    #endregion // Stop/Go

    #region Dispose

        /// <summary>
        /// Dispose unmanaged resources, which would include process handles. 
        /// </summary>
        public void Dispose()
        {
            // dispose managed resources            
            foreach (NativeDbgProcess proc in m_processes.Values)
            {
                proc.Dispose();
            }
            // No native resources to free, so we don't need a finalizer.

            GC.SuppressFinalize(true);
        }


    #endregion
    };

#endif

} // namespace Microsoft.Samples.Debugging.Native

