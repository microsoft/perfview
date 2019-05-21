// Copyright (c) Microsoft Corporation.  All rights reserved
// This file is best viewed using outline mode (Ctrl-M Ctrl-O)
//
// This program uses code hyperlinks available as part of the HyperAddin Visual Studio plug-in.
// It is available from http://www.codeplex.com/hyperAddin 
// using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Analysis.GC;
using Microsoft.Diagnostics.Tracing.Analysis.JIT;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Parsers.ClrPrivate;
using Microsoft.Diagnostics.Tracing.Parsers.FrameworkEventSource;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Parsers.Symbol;
using Microsoft.Diagnostics.Tracing.Parsers.Tpl;
using Microsoft.Diagnostics.Tracing.Stacks;
using Microsoft.Diagnostics.Utilities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Address = System.UInt64;
using HeapID = System.Int32;
using ThreadID = System.Int32;
using ProcessID = System.Int32;
using ProcessorNumber = System.Int32;
using System.Reflection.Metadata.Ecma335;
using System.Xml.Schema;

namespace Microsoft.Diagnostics.Tracing.Analysis
{
    /// <summary>
    /// Extension methods to enable TraceManagedProcess
    /// </summary>
    public static class TraceLoadedDotNetRuntimeExtensions
    {
        public static void NeedLoadedDotNetRuntimes(this TraceEventDispatcher source)
        {
            // ensure there are base processes
            source.NeedProcesses();

            if (m_currentSource != source)
            {
                TraceLoadedDotNetRuntime.SetupCallbacks(source);
            }

            source.UserData["Computers/LoadedDotNetRuntimes"] = new Dictionary<ProcessIndex, DotNetRuntime>();

            m_currentSource = source;
        }

        public static TraceLoadedDotNetRuntime LoadedDotNetRuntime(this TraceProcess process)
        {
            Debug.Assert(process.Source != null);
            Debug.Assert(m_currentSource == process.Source);
            Dictionary<ProcessIndex, DotNetRuntime> map = process.Source.UserData["Computers/LoadedDotNetRuntimes"] as Dictionary<ProcessIndex, DotNetRuntime>;
            if (map.ContainsKey(process.ProcessIndex))
            {
                return map[process.ProcessIndex].Runtime;
            }
            else
            {
                return null;
            }
        }

        public static void AddCallbackOnDotNetRuntimeLoad(this TraceProcess process, Action<TraceLoadedDotNetRuntime> OnDotNetRuntimeLoaded)
        {
            Debug.Assert(process.Source != null);
            Debug.Assert(m_currentSource == process.Source);
            Dictionary<ProcessIndex, DotNetRuntime> map = (Dictionary<ProcessIndex, DotNetRuntime>)process.Source.UserData["Computers/LoadedDotNetRuntimes"];
            if (!map.ContainsKey(process.ProcessIndex))
            {
                map.Add(process.ProcessIndex, new DotNetRuntime());
            }

            map[process.ProcessIndex].OnLoaded += OnDotNetRuntimeLoaded;
        }

        public static void SetMutableTraceEventStackSource(this TraceProcess process, MutableTraceEventStackSource stackSource)
        {
            Debug.Assert(process.Source != null);
            Debug.Assert(m_currentSource == process.Source);
            Dictionary<ProcessIndex, DotNetRuntime> map = (Dictionary<ProcessIndex, DotNetRuntime>)process.Source.UserData["Computers/LoadedDotNetRuntimes"];
            if (!map.ContainsKey(process.ProcessIndex))
            {
                map.Add(process.ProcessIndex, new DotNetRuntime());
            }

            map[process.ProcessIndex].StackSource = stackSource;
        }

        public static MutableTraceEventStackSource MutableTraceEventStackSource(this TraceProcess process)
        {
            Debug.Assert(process.Source != null);
            Debug.Assert(m_currentSource == process.Source);
            Dictionary<ProcessIndex, DotNetRuntime> map = (Dictionary<ProcessIndex, DotNetRuntime>)process.Source.UserData["Computers/LoadedDotNetRuntimes"];
            if (map.ContainsKey(process.ProcessIndex))
            {
                return map[process.ProcessIndex].StackSource;
            }
            else
            {
                return null;
            }
        }

        public static bool HasMutableTraceEventStackSource(this TraceEventDispatcher source)
        {
            Dictionary<ProcessIndex, DotNetRuntime> map = (Dictionary<ProcessIndex, DotNetRuntime>)source.UserData["Computers/LoadedDotNetRuntimes"];
            return map.Any(kv => kv.Value.StackSource != null);
        }

        #region private
        public class DotNetRuntime
        {
            public Action<TraceLoadedDotNetRuntime> OnLoaded;
            public TraceLoadedDotNetRuntime Runtime;
            public MutableTraceEventStackSource StackSource;
        }

        internal static void OnDotNetRuntimeLoaded(this TraceProcess process, TraceLoadedDotNetRuntime runtime)
        {
            Debug.Assert(process.Source != null);
            Dictionary<ProcessIndex, DotNetRuntime> map = (Dictionary<ProcessIndex, DotNetRuntime>)process.Source.UserData["Computers/LoadedDotNetRuntimes"];
            if (!map.ContainsKey(process.ProcessIndex))
            {
                map.Add(process.ProcessIndex, new DotNetRuntime());
            }

            map[process.ProcessIndex].Runtime = runtime;
            if (map[process.ProcessIndex].OnLoaded != null)
            {
                map[process.ProcessIndex].OnLoaded(runtime);
            }
        }

        internal static void OnDotNetRuntimeUnloaded(this TraceProcess process)
        {
            Debug.Assert(process.Source != null);
            Dictionary<ProcessIndex, DotNetRuntime> map = (Dictionary<ProcessIndex, DotNetRuntime>)process.Source.UserData["Computers/LoadedDotNetRuntimes"];
            if (map.ContainsKey(process.ProcessIndex))
            {
                // remove this runtime, since the process has terminated
                map.Remove(process.ProcessIndex);
            }
        }

        private static TraceEventDispatcher m_currentSource; // used to ensure non-concurrent usage
        #endregion
    }

    /// <summary>
    /// Extension properties for TraceProcess that include necessary .NET values
    /// 
    /// TODO This implementation is poor at idenitfying the ParentPID, 64bitness, and Start/End times
    /// </summary>
    public class TraceLoadedDotNetRuntime
    {
        /// <summary>
        /// Returns the textual version of the .NET Framework
        /// </summary>
        public string RuntimeVersion { get { return "V " + runtimeVersion.Major + "." + runtimeVersion.Minor + "." + runtimeVersion.Build + "." + runtimeVersion.Revision; } }
        /// <summary>
        /// Returns the .NET startup flags
        /// </summary>
        public StartupFlags StartupFlags { get; internal set; }
        /// <summary>
        /// Date and time of when the runtime was built
        /// This is useful when a more detailed version is not present
        /// </summary>
        public DateTime RuntimeBuiltTime { get; internal set; }

        /// <summary>
        /// Garbage Collector (GC) specific details about this process
        /// </summary>
        public TraceGarbageCollector GC { get; } = new TraceGarbageCollector();
        /// <summary>
        /// Fired on the start of a GC
        /// </summary>
        public event Action<TraceProcess, TraceGC> GCStart = null;
        /// <summary>
        /// Fired at the end of tha GC.  Given the nature of the GC, it is possible that multiple GCs will be inflight at the same time.
        /// </summary>
        public event Action<TraceProcess, TraceGC> GCEnd = null;

        /// <summary>
        /// Just-in-time compilation (JIT) specific details about this process
        /// </summary>
        public TraceJitCompiler JIT { get; } = new TraceJitCompiler();

        /// <summary>
        /// Fired when a managed method is starting to compile (jit)
        /// </summary>
        public event Action<TraceProcess, TraceJittedMethod> JITMethodStart = null;
        /// <summary>
        /// Fired when a managed method is done compiling (jitting).  Given the nature of the JIT, it is possible that multiple methods will be compiled at the same time.
        /// </summary>
        public event Action<TraceProcess, TraceJittedMethod> JITMethodEnd = null;

        /// <summary>
        /// An XML representation of the TraceEventProcess (for debugging)
        /// </summary>
        public override string ToString()
        {
            string xml = base.ToString();
            StringBuilder sb = new StringBuilder();

            sb.Append("ClrRuntimeVersion=").Append(XmlUtilities.XmlQuote(RuntimeVersion)).Append(" ");
            sb.Append("ClrStartupFlags=").Append(XmlUtilities.XmlQuote(StartupFlags)).Append(" ");
            sb.Append("/>");

            return xml.Replace("/>", sb.ToString());
        }

        #region private

        internal TraceLoadedDotNetRuntime(TraceProcess proc)
        {
            runtimeVersion = new Version(0, 0, 0, 0);
            StartupFlags = StartupFlags.None;
            RuntimeBuiltTime = default(DateTime);
        }

        /// <summary>
        /// Gathers relevant details about the processes in the event source
        /// </summary>
        /// <param name="source"></param>
        internal static void SetupCallbacks(TraceEventDispatcher source)
        {
            bool processGCEvents = true;
            bool processJITEvents = true;

            //
            // Set additional TraceManageProcess properties
            //

            // These parsers create state and we want to collect that so we put it on our 'parsers' list that we serialize.  
            var clrRundownParser = new ClrRundownTraceEventParser(source);
            // See if the source knows about the CLR Private provider, if it does, then 
            var clrPrivate = new ClrPrivateTraceEventParser(source);

            Dictionary<TraceProcess, TraceLoadedDotNetRuntime> processRuntimes = new Dictionary<TraceProcess, TraceLoadedDotNetRuntime>();

            // if any clr event is fired, this process is managed
            Action<TraceEvent> createManagedProc = delegate (TraceEvent data)
            {
                var proc = data.Process(); // this will create an instance, if one does not exist
                TraceLoadedDotNetRuntime mang;
                if (!processRuntimes.TryGetValue(proc, out mang))
                {
                    // duplicate the TraceProcess and create an instance of TraceManagedProcess
                    mang = new TraceLoadedDotNetRuntime(proc);

                    // The TraceProcess infrastructure relies on Kernel Events to properly set key process information (like process name)
                    // Set process name directly if not set
                    // This is needed for linux traces or traces on Windows which do not have backProcessing enabled (very rare)
                    if (string.IsNullOrWhiteSpace(proc.Name))
                    {
                        proc.name = data.ProcessName;
                    }

                    // fire callback and associate this DotNetRuntime with this process
                    proc.OnDotNetRuntimeLoaded(mang);
                    processRuntimes.Add(proc, mang);
                }
            };

            // if applying lifetime, trim loaded managed runtimes when processes are terminated and are out of lifetime
            source.AddCallbackOnProcessStop((p) =>
            {
                // check if we are applying a lifetime model
                if (source.DataLifetimeEnabled())
                {
                    // iterate through all processes and unload the managed runtime from processes
                    //  that have exited and are out of lifetime
                    // immediately removing the runtime for stopped processes is possible, but that
                    //  breaks the contract of how long data is kept with the lifetime
                    foreach (var process in source.Processes())
                    {
                        // continue if the process has not exited yet
                        if (!process.ExitStatus.HasValue)
                        {
                            continue;
                        }

                        if (process.EndTimeRelativeMsec < (p.EndTimeRelativeMsec - source.DataLifetimeMsec))
                        {
                            // remove this managed runtime instance
                            process.OnDotNetRuntimeUnloaded();
                            // remove from the local cache
                            if (processRuntimes.ContainsKey(process))
                            {
                                processRuntimes.Remove(process);
                            }
                        }
                    }
                }
            });

            source.Clr.All += createManagedProc;
            clrPrivate.All += createManagedProc;

            Func<TraceEvent, TraceLoadedDotNetRuntime> currentManagedProcess = delegate (TraceEvent data)
            {
                TraceLoadedDotNetRuntime mang;
                if (!processRuntimes.TryGetValue(data.Process(), out mang))
                {
                    createManagedProc(data);
                    mang = processRuntimes[data.Process()];

                    Debug.Assert(mang != null);
                }

                return mang;
            };

            Action<RuntimeInformationTraceData> doAtRuntimeStart = delegate (RuntimeInformationTraceData data)
           {
               TraceProcess process = data.Process();
               TraceLoadedDotNetRuntime mang = currentManagedProcess(data);

               // replace the current runtimeversion if it is currently not set, or this version has information including revision (eg. qfe number)
               if (mang.runtimeVersion.Major == 0 || data.VMQfeNumber > 0)
               {
                   mang.runtimeVersion = new Version(data.VMMajorVersion, data.VMMinorVersion, data.VMBuildNumber, data.VMQfeNumber);
               }

               mang.StartupFlags = data.StartupFlags;
               // proxy for bitness, given we don't have a traceevent to pass through
               process.Is64Bit = (data.RuntimeDllPath.ToLower().Contains("framework64"));

               if (process.CommandLine.Length == 0)
               {
                   process.CommandLine = data.CommandLine;
               }
           };
            clrRundownParser.RuntimeStart += doAtRuntimeStart;
            source.Clr.RuntimeStart += doAtRuntimeStart;

            var symbolParser = new SymbolTraceEventParser(source);
            symbolParser.ImageIDFileVersion += delegate (FileVersionTraceData data)
            {
                TraceProcess process = data.Process();

                if (string.Equals(data.OrigFileName, "clr.dll", StringComparison.OrdinalIgnoreCase) || string.Equals(data.OrigFileName, "mscorwks.dll", StringComparison.OrdinalIgnoreCase) || string.Equals(data.OrigFileName, "coreclr.dll", StringComparison.OrdinalIgnoreCase))
                {
                    // this will create a mang instance for this process
                    TraceLoadedDotNetRuntime mang = currentManagedProcess(data);
                    Version version;
                    // replace the current runtimeVersion if there is not good revision information
                    if ((mang.runtimeVersion.Major == 0 || mang.runtimeVersion.Revision == 0) && Version.TryParse(data.ProductVersion, out version))
                    {
                        mang.runtimeVersion = new Version(version.Major, version.Minor, version.Build, version.Revision);
                    }

                    if (mang.RuntimeBuiltTime == default(DateTime))
                    {
                        mang.RuntimeBuiltTime = data.BuildTime;
                    }
                }
            };
            symbolParser.ImageID += delegate (ImageIDTraceData data)
            {
                TraceProcess process = data.Process();

                if (string.Equals(data.OriginalFileName, "clr.dll", StringComparison.OrdinalIgnoreCase) || string.Equals(data.OriginalFileName, "mscorwks.dll", StringComparison.OrdinalIgnoreCase) || string.Equals(data.OriginalFileName, "coreclr.dll", StringComparison.OrdinalIgnoreCase))
                {
                    // this will create a mang instance for this process
                    TraceLoadedDotNetRuntime mang = currentManagedProcess(data);
                    // capture the CLR build stamp to provide deeper version information (when version information is not present)
                    if (mang.RuntimeBuiltTime == default(DateTime))
                    {
                        mang.RuntimeBuiltTime = data.BuildTime;
                    }
                }
            };

            //
            // GC
            //
            // Blocking GCs are marked as complete (IsComplete set to true) during RestartEEStop, except for the gen1 GCs that
            // happen right before the NGC2 (full blocking GC) in provisional mode. For the exceptional case we set the gen1 as
            // complete during the NGC2's GCStart at which point we know that's an NGC2 triggered due to provisional mode.
            // Background GCs are marked as complete during GCHeapStats as it does not call RestartEE at the end of a GC.
            //
            if (processGCEvents)
            {
                // log at both startup and rundown
                var clrRundown = new ClrRundownTraceEventParser(source);
                clrRundown.RuntimeStart += doAtRuntimeStart;
                source.Clr.RuntimeStart += doAtRuntimeStart;

                CircularBuffer<ThreadWorkSpan> RecentThreadSwitches = new CircularBuffer<ThreadWorkSpan>(1000);
                source.Kernel.ThreadCSwitch += delegate (CSwitchTraceData data)
                {
                    RecentThreadSwitches.Add(new ThreadWorkSpan(data));
                    TraceProcess tmpProc = data.Process();
                    TraceLoadedDotNetRuntime mang;
                    if (processRuntimes.TryGetValue(tmpProc, out mang))
                    {
                        mang.GC.m_stats.ThreadId2Priority[data.NewThreadID] = data.NewThreadPriority;
                        // Not necessary now that servergcthreads is a bimap
                        //HeapID? heapIndex = mang.GC.m_stats.IsServerGCThread(data.ThreadID);
                        //if ((heapIndex != null))
                        //{
                        //    HandleHeapForegroundThreadID(mang.GC.m_stats, heapIndex.Value, data.ThreadID);
                        //}
                    }

                    foreach (var pair in processRuntimes)
                    {
                        var proc = pair.Key;
                        mang = pair.Value;

                        foreach (GCHeapAndThreadKindAndIsNewThread heapAndThreadKind in mang.GC.m_stats.GetHeapAndThreadKinds(data.OldThreadID, data.NewThreadID))
                        {
                            TraceGC _gc = TraceGarbageCollector.GetCurrentGC(mang, data.TimeStampRelativeMSec, threadKind: heapAndThreadKind.ThreadKind);
                            // If we are in the middle of a GC.
                            if (_gc != null)
                            {
                                // TODO: Why does bgc not get cswitch?
                                if (GCShouldHaveServerGCHeapHistories(mang, _gc))
                                {
                                    _gc.AddServerGcThreadSwitch(new ThreadWorkSpan(data), heapAndThreadKindAndIsNewThread: heapAndThreadKind);
                                }
                            }
                        }
                    }
                };

                CircularBuffer<ThreadWorkSpan> RecentCpuSamples = new CircularBuffer<ThreadWorkSpan>(1000);
                source.Kernel.PerfInfoSample += delegate (SampledProfileTraceData data)
                {
                    RecentCpuSamples.Add(new ThreadWorkSpan(data));
                    bool hadHeap = false;
                    //TODO: This was preventing us from getting stolen times from CPU samples. We don't need stacks for that.
                    //if (source.HasMutableTraceEventStackSource())
                    {
                        TraceLoadedDotNetRuntime loadedRuntime = null;
                        TraceProcess gcProcess = null;
                        // Loop over all runtimes. If there's a GC going on, this may be stolen time from that GC.
                        foreach (var pair in processRuntimes)
                        {
                            var proc = pair.Key;
                            var tmpMang = pair.Value;

                            TraceGC e = TraceGarbageCollector.GetCurrentGC(tmpMang, data.TimeStampRelativeMSec);
                            // If we are in the middle of a GC.
                            if (e != null && GCShouldHaveServerGCHeapHistories(tmpMang, e))
                            {
                                GCStats stats = tmpMang.GC.m_stats;

                                foreach (GCHeapAndThreadKind htk in stats.GetHeapAndThreadKinds(data.ThreadID))
                                {
                                    hadHeap = true;
                                    e.AddServerGcSample(new ThreadWorkSpan(data), heapAndThreadKind: new GCHeapAndThreadKindAndIsNewThread(htk, newThreadIsGC: true));
                                }

                                loadedRuntime = tmpMang;
                                gcProcess = proc;

                                if (!hadHeap)
                                {
                                    // This is from a different process. So consider it stolen time from this process.
                                    HeapID? heapID = stats.GetHeapIDFromProcessorNumber(data.ProcessorNumber);

                                    if (heapID != null)
                                    {
                                        // TODO: From just the processor number, we can't know what the threadkind should be ... so guessing foreground
                                        e.AddServerGcSample(
                                            sample: new ThreadWorkSpan(data),
                                            heapAndThreadKind: new GCHeapAndThreadKindAndIsNewThread(
                                                heapAndThreadKind: new GCHeapAndThreadKind(heapID: heapID.Value, threadKind: GCThreadKind.Foreground),
                                                newThreadIsGC: false));
                                    }
                                }
                            }
                        }

                        if (loadedRuntime != null && gcProcess != null && gcProcess.MutableTraceEventStackSource() != null)
                        {
                            var stackSource = gcProcess.MutableTraceEventStackSource();
                            if (stackSource != null)
                            {
                                TraceGC e = TraceGarbageCollector.GetCurrentGC(loadedRuntime, data.TimeStampRelativeMSec);
                                StackSourceSample sample = new StackSourceSample(stackSource);
                                sample.Metric = 1;
                                sample.TimeRelativeMSec = data.TimeStampRelativeMSec;
                                var nodeName = string.Format("Server GCs #{0} in {1} (PID:{2})", e.Number, gcProcess.Name, gcProcess.ProcessID);
                                var nodeIndex = stackSource.Interner.FrameIntern(nodeName);
                                sample.StackIndex = stackSource.Interner.CallStackIntern(nodeIndex, stackSource.GetCallStack(data.CallStackIndex(), data));
                                stackSource.AddSample(sample);
                            }
                        }
                    }

                    TraceProcess tmpProc = data.Process();
                    TraceLoadedDotNetRuntime mang;
                    if (processRuntimes.TryGetValue(tmpProc, out mang))
                    {
                        HeapID? heapIndex = mang.GC.m_stats.GetServerGCHeapFromThread(data.ThreadID);

                        //Not necessary now that servergcthreads is a bimap
                        //if (heapIndex != null)
                        //{
                        //    HandleHeapForegroundThreadID(mang.GC.m_stats, heapID: heapIndex.Value, threadID: data.ThreadID);
                        //}

                        var cpuIncrement = tmpProc.SampleIntervalMSec();

                        TraceGC _gc = TraceGarbageCollector.GetCurrentGC(mang, data.TimeStampRelativeMSec);
                        // If we are in the middle of a GC.
                        if (_gc != null)
                        {
                            bool isThreadDoingGC = false;
                            if ((_gc.Type != GCType.BackgroundGC) && (mang.GC.m_stats.IsServerGCUsed == 1))
                            {
                                if (heapIndex != null)
                                {
                                    _gc.AddServerGCThreadTime(heapIndex.Value, cpuIncrement);
                                    isThreadDoingGC = true;
                                }
                            }
                            else if (data.ThreadID == mang.GC.m_stats.suspendThreadIDGC)
                            {
                                _gc.GCCpuMSec += cpuIncrement;
                                isThreadDoingGC = true;
                            }
                            else if (mang.GC.m_stats.IsBGCThread(data.ThreadID))
                            {
                                Debug.Assert(mang.GC.m_stats.currentBGC != null);
                                if (mang.GC.m_stats.currentBGC != null)
                                {
                                    mang.GC.m_stats.currentBGC.GCCpuMSec += cpuIncrement;
                                }

                                isThreadDoingGC = true;
                            }

                            if (isThreadDoingGC)
                            {
                                mang.GC.m_stats.TotalCpuMSec += cpuIncrement;
                            }
                        }
                    }
                };

                HashSet<TraceLoadedDotNetRuntime> isEESuspended = new HashSet<TraceLoadedDotNetRuntime>();

                source.Clr.GCSuspendEEStart += delegate (GCSuspendEETraceData data)
                {
                    MaybePrintEvent(data);

                    var process = data.Process();
                    var mang = currentManagedProcess(data);
                    isEESuspended.Add(mang);

                    switch (data.Reason)
                    {
                        case GCSuspendEEReason.SuspendForGC:
                            mang.GC.m_stats.suspendThreadIDGC = data.ThreadID;
                            break;
                        case GCSuspendEEReason.SuspendForGCPrep:
                            mang.GC.m_stats.suspendThreadIDBGC = data.ThreadID;
                            break;
                        default:
                            mang.GC.m_stats.suspendThreadIDOther = data.ThreadID;
                            // There are several other reasons for a suspend but we
                            // don't care about them
                            return;
                    }
                    mang.GC.m_stats.lastSuspendReason = data.Reason;

                    mang.GC.m_stats.suspendTimeRelativeMSec = data.TimeStampRelativeMSec;

                    if ((process.Log != null) && !mang.GC.m_stats.gotThreadInfo)
                    {
                        mang.GC.m_stats.gotThreadInfo = true;
                        Microsoft.Diagnostics.Tracing.Etlx.TraceProcess traceProc = process.Log.Processes.GetProcess(process.ProcessID, data.TimeStampRelativeMSec);
                        if (traceProc != null)
                        {
                            foreach (var procThread in traceProc.Threads)
                            {
                                if ((procThread.ThreadInfo != null) && (procThread.ThreadInfo.Contains(".NET Server GC Thread")))
                                {
                                    mang.GC.m_stats.IsServerGCUsed = 1;
                                    break;
                                }
                            }

                            if (mang.GC.m_stats.IsServerGCUsed == 1)
                            {
                                mang.GC.m_stats.HeapCount = 0;
                                //not needed, it's always initialized now
                                //mang.GC.m_stats.serverGCThreadToHeap = new Dictionary<int, int>(2);

                                foreach (var procThread in traceProc.Threads)
                                {
                                    if ((procThread.ThreadInfo != null) && (procThread.ThreadInfo.StartsWith(".NET Server GC Thread")))
                                    {
                                        mang.GC.m_stats.HeapCount++;

                                        int startIndex = procThread.ThreadInfo.IndexOf('(');
                                        int endIndex = procThread.ThreadInfo.IndexOf(')');
                                        string heapNumString = procThread.ThreadInfo.Substring(startIndex + 1, (endIndex - startIndex - 1));
                                        int heapNum = int.Parse(heapNumString);
                                        mang.GC.m_stats.AssociateServerGCThreadAndHeap(threadID: procThread.ThreadID, heapID: heapNum);
                                    }
                                }
                            }
                        }
                    }

                    if (data.Reason == GCSuspendEEReason.SuspendForGC)
                    {
                        AddNewGC(process, mang, isKnownToBeBackground: false, number: data.Count);
                    }
                };

                bool GCMayNeedServerGCHeapHistories(TraceLoadedDotNetRuntime mang, bool isKnownToBeBackground)
                {
                    //TODO: don't know why background gcs were excluded from this...
                    return /*!isKnownToBeBackground &&*/ mang.GC.m_stats.IsServerGCUsed != 0;
                }

                bool GCShouldHaveServerGCHeapHistories(TraceLoadedDotNetRuntime mang, TraceGC gc)
                {
                    bool res = (gc.Type != GCType.BackgroundGC) && (mang.GC.m_stats.IsServerGCUsed == 1);
                    // Possible that we should have ServerGcHeapHistories, but never set it up.
                    return res && gc.ServerGcHeapHistories.Count > 0;
                }

                TraceGC AddNewGC(TraceProcess process, TraceLoadedDotNetRuntime mang, bool isKnownToBeBackground, int? number)
                {
                    TraceGC gc = new TraceGC(mang.GC.m_stats.HeapCount) { Index = mang.GC.GCs.Count };
                    mang.GC.GCs.Add(gc);

                    // Ideally we would do this only for non-background GCs, but we might not have that information at this point
                    // and we want SetUpServerGcHistory called immediately
                    if (GCMayNeedServerGCHeapHistories(mang, isKnownToBeBackground))
                    {
                        mang.GC.m_stats.SetUpServerGcHistory(process.ProcessID, gc);

                        IEnumerable<GCHeapAndThreadKindAndIsNewThread> threadKinds(ThreadWorkSpan s) =>
                            mang.GC.m_stats.GetHeapAndThreadKinds(s.OldThreadId, s.ThreadId);

                        foreach (var s in RecentCpuSamples)
                        {
                            foreach (GCHeapAndThreadKindAndIsNewThread htk in threadKinds(s))
                            {
                                gc.AddServerGcSample(s, heapAndThreadKind: htk);
                            }
                        }

                        foreach (var s in RecentThreadSwitches)
                        {
                            foreach (GCHeapAndThreadKindAndIsNewThread htk in threadKinds(s))
                            {
                                gc.AddServerGcThreadSwitch(s, heapAndThreadKindAndIsNewThread: htk);
                            }
                        }
                    }

                    return gc;
                }

                // In 2.0 we didn't have this event.
                source.Clr.GCSuspendEEStop += delegate (GCNoUserDataTraceData data)
                {
                    MaybePrintEvent(data);
                    var mang = currentManagedProcess(data);

                    if(!(data.ThreadID == mang.GC.m_stats.suspendThreadIDBGC || data.ThreadID == mang.GC.m_stats.suspendThreadIDGC))
                    {
                        // We only care about SuspendStop events that correspond to GC or PrepForGC reasons
                        // If we had initiated one of those then we set the corresponding threadid field in
                        // SuspendStart and we are guaranteed that the matching stop will occur on the same
                        // thread. Any other SuspendStop must be part of a suspension we aren't tracking.
                        return;
                    }

                    if ((mang.GC.m_stats.suspendThreadIDBGC > 0) && (mang.GC.m_stats.currentBGC != null))
                    {
                        mang.GC.m_stats.currentBGC.SuspendDurationMSec += data.TimeStampRelativeMSec - mang.GC.m_stats.suspendTimeRelativeMSec;
                    }

                    mang.GC.m_stats.suspendEndTimeRelativeMSec = data.TimeStampRelativeMSec;
                };

                source.Clr.GCRestartEEStop += delegate (GCNoUserDataTraceData data)
                {
                    MaybePrintEvent(data);
                    var process = data.Process();
                    var stats = currentManagedProcess(data);
                    isEESuspended.Remove(stats);

                    if(data.ThreadID == stats.GC.m_stats.suspendThreadIDOther)
                    {
                        stats.GC.m_stats.suspendThreadIDOther = -1;
                    }

                    if (!(data.ThreadID == stats.GC.m_stats.suspendThreadIDBGC || data.ThreadID == stats.GC.m_stats.suspendThreadIDGC))
                    {
                        // We only care about RestartEE events that correspond to GC or PrepForGC suspensions
                        // If we had initiated one of those then we set the corresponding threadid field in
                        // SuspendStart and we are guaranteed that the matching RestartEE will occur on the 
                        // same thread. Any other RestartEE must be part of a suspension we aren't tracking.
                        return;
                    }

                    TraceGC _gc = TraceGarbageCollector.GetCurrentGC(stats, data.TimeStampRelativeMSec, mustBeStarted: true);
                    if (_gc != null)
                    {
                        if (_gc.Type == GCType.BackgroundGC)
                        {
                            stats.GC.m_stats.AddConcurrentPauseTime(_gc, data.TimeStampRelativeMSec);
                        }
                        else
                        {
                            if (!_gc.IsConcurrentGC)
                            {
                                Debug.Assert(_gc.PauseDurationMSec == 0, "gc should not have PauseDurationMSec set yet");
                            }
                            Debug.Assert(_gc.PauseStartRelativeMSec != 0, "gc should have PauseStartRelativeMSec set");
                            // In 2.0 Concurrent GC, since we don't know the GC's type we can't tell if it's concurrent 
                            // or not. But we know we don't have nested GCs there so simply check if we have received the
                            // GCStop event; if we have it means it's a blocking GC; otherwise it's a concurrent GC so 
                            // simply add the pause time to the GC without making the GC complete.
                            if (_gc.DurationMSec == 0)
                            {
                                Debug.Assert(_gc.is20Event, "gc should have DurationMSec set unless 2.0");
                                _gc.IsConcurrentGC = true;
                                stats.GC.m_stats.AddConcurrentPauseTime(_gc, data.TimeStampRelativeMSec);
                            }
                            else
                            {
                                _gc.PauseDurationMSec = data.TimeStampRelativeMSec - _gc.PauseStartRelativeMSec;
                                if (_gc.HeapStats != null)
                                {
                                    _gc.OnEnd(stats.GC); // set IsComplete = true;
                                    stats.GC.m_stats.lastCompletedGC = _gc;

                                    // fire event
                                    if (stats.GCEnd != null)
                                    {
                                        stats.GCEnd(process, _gc);
                                    }
                                }
                            }
                        }

                        // There may be a few of these before the end of the GC;
                        // the final GCRestartEEStop after the end of the GC will be the final value of PauseEndRelativeMSec.
                        _gc.PauseEndRelativeMSec = data.TimeStampRelativeMSec;

                        FinishUpGC(_gc, stats.GC.m_stats);
                    }

                    // We don't change between a GC end and the pause resume.   
                    //Debug.Assert(stats.allocTickAtLastGC == stats.allocTickCurrentMB);
                    // Mark that we are not in suspension anymore.  
                    stats.GC.m_stats.suspendTimeRelativeMSec = -1;
                    stats.GC.m_stats.suspendThreadIDBGC = -1;
                    stats.GC.m_stats.suspendThreadIDGC = -1;
                };

                void FinishUpGC(TraceGC gc, GCStats m_stats)
                {
                    foreach (ServerGcHistory hp in gc.ServerGcHeapHistories)
                    {
                        ThreadID? workingThreadId = m_stats.GetServerGCThreadFromHeap(hp.HeapId);
                        if (workingThreadId != null)
                        {
                            if (hp.GcWorkingThreadId == null)
                            {
                                hp.GcWorkingThreadId = workingThreadId;
                            }
                            else
                            {
                                Debug.Assert(hp.GcWorkingThreadId == workingThreadId);
                            }
                        }
                    }
                }

                source.Clr.GCAllocationTick += delegate (GCAllocationTickTraceData data)
                {
                    var stats = currentManagedProcess(data);

                    if (stats.GC.m_stats.HasAllocTickEvents == false)
                    {
                        stats.GC.m_stats.HasAllocTickEvents = true;
                    }

                    double valueMB = data.GetAllocAmount(ref stats.GC.m_stats.SeenBadAllocTick) / 1000000.0;

                    if (data.AllocationKind == GCAllocationKind.Small)
                    {
                        // Would this do the right thing or is it always 0 for SOH since AllocationAmount 
                        // is an int??? 
                        stats.GC.m_stats.allocTickCurrentMB[0] += valueMB;
                    }
                    else
                    {
                        stats.GC.m_stats.allocTickCurrentMB[1] += valueMB;
                    }
                };

                source.Clr.GCStart += delegate (GCStartTraceData data)
                {
                    MaybePrintEvent(data);
                    var process = data.Process();
                    var stats = currentManagedProcess(data);

                    // We need to filter the scenario where we get 2 GCStart events for each GC.
                    if ((stats.GC.m_stats.suspendThreadIDGC > 0 || stats.GC.m_stats.suspendThreadIDOther > 0) &&
                            !((stats.GC.GCs.Count > 0) && stats.GC.GCs[stats.GC.GCs.Count - 1].Number == data.Count))
                    {
                        Debug.Assert(0 <= data.Depth && data.Depth <= 2, "GC generation should be 0-2");
                        TraceGC _gc = TraceGarbageCollector.GetCurrentGC(stats, data.TimeStampRelativeMSec);
                        if (_gc.SeenStartEvent)
                        {
                            // After starting a BGC, we may proceed with an ephemeral GC.
                            // This ephemeral GC won't have an associated SuspendEEStart, so we have to create it here instead
                            Debug.Assert(stats.GC.m_stats.currentBGC == _gc, "Expect to see a BGC here");
                            _gc = AddNewGC(process, stats, isKnownToBeBackground: data.Type == GCType.BackgroundGC, number: data.Count);
                        }
                        _gc.Generation = data.Depth;
                        _gc.Reason = data.Reason;
                        _gc.Number = data.Count;
                        _gc.Type = data.Type;
                        _gc.is20Event = data.IsClassicProvider;
                        Debug.Assert(_gc.SeenStartEvent, "We should wrote GC number so SeenStartEvent should be true");
                        bool isEphemeralGCAtBGCStart = false;
                        // Detecting the ephemeral GC that happens at the beginning of a BGC.
                        if (stats.GC.GCs.Count > 0)
                        {
                            TraceGC lastGCEvent = stats.GC.GCs[stats.GC.GCs.Count - 1];
                            if ((lastGCEvent.Type == GCType.BackgroundGC) &&
                                (!lastGCEvent.IsComplete) &&
                                (data.Type == GCType.NonConcurrentGC))
                            {
                                isEphemeralGCAtBGCStart = true;
                            }
                        }

                        Debug.Assert(stats.GC.m_stats.suspendTimeRelativeMSec != -1, "suspendTimeRelativeMSec should be set");
                        if (isEphemeralGCAtBGCStart || _gc.Reason == GCReason.PMFullGC)
                        {
                            _gc.PauseStartRelativeMSec = data.TimeStampRelativeMSec;

                            if (_gc.Reason == GCReason.PMFullGC)
                            {
                                TraceGC lastGC = TraceGarbageCollector.GetCurrentGC(stats, data.TimeStampRelativeMSec);
                                if (lastGC != null)
                                {
                                    lastGC.OnEnd(stats.GC);
                                }
                            }
                        }
                        else
                        {
                            _gc.PauseStartRelativeMSec = stats.GC.m_stats.suspendTimeRelativeMSec;
                            if (stats.GC.m_stats.suspendEndTimeRelativeMSec == -1)
                            {
                                stats.GC.m_stats.suspendEndTimeRelativeMSec = data.TimeStampRelativeMSec;
                            }

                            _gc.SuspendDurationMSec = stats.GC.m_stats.suspendEndTimeRelativeMSec - stats.GC.m_stats.suspendTimeRelativeMSec;
                        }

                        _gc.StartRelativeMSec = data.TimeStampRelativeMSec;
                        if (_gc.Type == GCType.BackgroundGC)
                        {
                            stats.GC.m_stats.currentBGC = _gc;
                            stats.GC.m_stats.currentOrFinishedBGC = _gc;
                            // For BGC, we need to add the suspension time so far to its pause so we don't miss including it.
                            // If there's an ephemeral GC happening before the BGC starts, AddConcurrentPauseTime will not
                            // add this suspension time to GC pause as that GC would be seen the ephemeral GC, not the BGC.
                            _gc.PauseDurationMSec = _gc.SuspendDurationMSec;
                            _gc.ProcessCpuAtLastGC = stats.GC.m_stats.ProcessCpuAtLastGC;
                        }

                        // fire event
                        if (stats.GCStart != null)
                        {
                            stats.GCStart(process, _gc);
                        }

                        // check if we should apply a lifetime limit to the GC cache
                        if (source.DataLifetimeEnabled() && data.TimeStampRelativeMSec >= stats.GC.NextRelativeTimeStampMsec)
                        {
                            // note the next time that lifetime should be applied, to avoid cleaningup too frequently
                            stats.GC.NextRelativeTimeStampMsec = data.TimeStampRelativeMSec + (source.DataLifetimeMsec / 2.0);
                            // trim the GCs to only include those either incomplete or completed after lifetime
                            stats.GC.m_gcs = stats.GC.m_gcs.Where(gc => !gc.IsComplete || gc.StartRelativeMSec >= (data.TimeStampRelativeMSec - source.DataLifetimeMsec)).ToList();
                            // rewrite the index for fast lookup
                            for (int i = 0; i < stats.GC.m_gcs.Count; i++)
                            {
                                stats.GC.m_gcs[i].Index = i;
                            }
                        }
                    }
                };

                source.Clr.GCPinObjectAtGCTime += delegate (PinObjectAtGCTimeTraceData data)
                {
                    var stats = currentManagedProcess(data);

                    TraceGC _gc = TraceGarbageCollector.GetCurrentGC(stats, data.TimeStampRelativeMSec);
                    if (_gc != null)
                    {
                        if (_gc.PinnedObjects == null)
                        {
                            _gc.PinnedObjects = new Dictionary<Address, long>();
                        }

                        if (!_gc.PinnedObjects.ContainsKey(data.ObjectID))
                        {
                            _gc.PinnedObjects.Add(data.ObjectID, data.ObjectSize);
                        }
                        else
                        {
                            _gc.duplicatedPinningReports++;
                        }
                    }
                };

                // Some builds have this as a public event, and some have it as a private event.
                // All will move to the private event, so we'll remove this code afterwards.
                source.Clr.GCPinPlugAtGCTime += delegate (PinPlugAtGCTimeTraceData data)
                {
                    var stats = currentManagedProcess(data);

                    TraceGC _event = TraceGarbageCollector.GetCurrentGC(stats, data.TimeStampRelativeMSec);
                    if (_event != null)
                    {
                        // ObjectID is supposed to be an IntPtr. But "Address" is defined as UInt64 in 
                        // TraceEvent.
                        if (_event.PinnedPlugs == null)
                        {
                            _event.PinnedPlugs = new List<TraceGC.PinnedPlug>();
                        }

                        _event.PinnedPlugs.Add(new TraceGC.PinnedPlug(data.PlugStart, data.PlugEnd));
                    }
                };

                source.Clr.GCMarkWithType += delegate (GCMarkWithTypeTraceData data)
                {
                    var stats = currentManagedProcess(data);

                    stats.GC.m_stats.AddServerGCThreadFromMark(data.ThreadID, data.HeapNum);

                    TraceGC _gc = TraceGarbageCollector.GetCurrentGC(stats, data.TimeStampRelativeMSec);
                    if (_gc != null)
                    {
                        if (_gc.PerHeapMarkTimes == null)
                        {
                            _gc.PerHeapMarkTimes = new Dictionary<int, MarkInfo>();
                        }

                        if (!_gc.PerHeapMarkTimes.ContainsKey(data.HeapNum))
                        {
                            _gc.PerHeapMarkTimes.Add(data.HeapNum, new MarkInfo());
                        }

                        _gc.PerHeapMarkTimes[data.HeapNum].MarkTimes[(int)data.Type] = data.TimeStampRelativeMSec;
                        _gc.PerHeapMarkTimes[data.HeapNum].MarkPromoted[(int)data.Type] = data.Promoted;
                    }
                };

                source.Clr.GCGlobalHeapHistory += delegate (GCGlobalHeapHistoryTraceData data)
                {
                    var stats = currentManagedProcess(data);
                    GCStats.ProcessGlobalHistory(stats, data);
                };

                source.Clr.GCPerHeapHistory += delegate (GCPerHeapHistoryTraceData data)
                {
                    var stats = currentManagedProcess(data);
                    GCStats.ProcessPerHeapHistory(stats, data);
                };

                Dictionary<TraceLoadedDotNetRuntime, TraceGarbageCollector.ManagedProcessJoinState> processJoinStates = new Dictionary<TraceLoadedDotNetRuntime, TraceGarbageCollector.ManagedProcessJoinState>();
                // TODO: clear entries in this dictionary when we're sure a GC is done (to save memory)
                Dictionary<TraceGC, TraceGarbageCollector.GCJoinStateFgOrBg> gcJoinStatesFg = new Dictionary<TraceGC, TraceGarbageCollector.GCJoinStateFgOrBg>();
                Dictionary<TraceGC, TraceGarbageCollector.GCJoinStateFgOrBg> gcJoinStatesBg = new Dictionary<TraceGC, TraceGarbageCollector.GCJoinStateFgOrBg>();

                Action<GCJoinTraceData> handleJoin = delegate (GCJoinTraceData data)
                {
                    if (DEBUG_IGNORE_JOINS)
                        return;

                    MaybePrintEvent(data);
                    TraceLoadedDotNetRuntime stats = currentManagedProcess(data);

                    if (data.JoinType != GcJoinType.Restart)
                    {
                        stats.GC.m_stats.AssociateProcessorNumberAndServerGCHeapID(processorNumber: data.ProcessorNumber, heapID: data.Heap);
                    }

                    GCJoinStage joinStage = (GCJoinStage)data.GCID;

                    TraceGarbageCollector.ManagedProcessJoinState procState = GetOrInit(processJoinStates, stats, () => new TraceGarbageCollector.ManagedProcessJoinState());
                    GCThreadKind? threadKind = procState.GetThreadKindAndPossiblyAddThread(threadID: data.ThreadID, joinStage: joinStage, joinTime: data.JoinTime, joinType: data.JoinType);

                    if (threadKind == null)
                    {
                        if (DEBUG_PRINT_GC)
                        {
                            Console.WriteLine("IGNORING EVENT -- threadKind == null");
                        }
                    }
                    else
                    {
                        Dictionary<TraceGC, TraceGarbageCollector.GCJoinStateFgOrBg> gcJoinStates = threadKind == GCThreadKind.Foreground ? gcJoinStatesFg : gcJoinStatesBg;

                        if (TraceGarbageCollector.JoinIndicatesNewGc(proc: stats, joinStates: gcJoinStates, joinStage: joinStage, joinTime: data.JoinTime))
                        {
                            if (DEBUG_PRINT_GC) Console.WriteLine("JOIN INDICATES NEW GC");
                            // A generation_determined event only comes on a foreground GC
                            AddNewGC(data.Process(), stats, isKnownToBeBackground: false, number: null);
                        }

                        // Ignore join events until we're on the third GC.
                        // Earlier join events may be incomplete -- it seems like some threads may go missing.
                        // (An alternative would be to keep the events but disable asserts.)
                        // Also ignore background events if we don't have a BGC yet.

                        bool haveEnoughGCs = stats.GC.GCs.Count >= 3;

                        if (haveEnoughGCs && !(threadKind == GCThreadKind.Background && stats.GC.m_stats.currentOrFinishedBGC == null))
                        {
                            int heapCount = stats.GC.m_stats.HeapCount;
                            Debug.Assert(heapCount > 0); // Should have been set by the 3rd GC.

                            TraceGC _gc = TraceGarbageCollector.GetCurrentGCForJoin(
                                proc: stats,
                                timeStampRelativeMSec: data.TimeStampRelativeMSec,
                                heapCount: (uint) heapCount,
                                joinStates: gcJoinStates,
                                joinStage: joinStage,
                                time: data.JoinTime,
                                type: data.JoinType,
                                threadId: data.ThreadID,
                                threadKind: threadKind.Value);

                            if (_gc != null)
                            {
                                _gc.AddGcJoin(data, isEESuspended: isEESuspended.Contains(stats), threadKind: threadKind.Value);
                            }
                            else
                            {
                                // It's possible that we have a time slice that includes join events but not the GCStart. Just ignore them in that case.
                                if (DEBUG_PRINT_GC)
                                {
                                    Console.WriteLine("IGNORING EVENT -- gc is null");
                                }
                            }
                        }
                        else if (DEBUG_PRINT_GC)
                        {
                            Console.WriteLine("Skipping event, haven't seen enough GCs");
                        }
                    }
                };

                source.Clr.GCJoin += handleJoin;

                clrPrivate.GCPinPlugAtGCTime += delegate (PinPlugAtGCTimeTraceData data)
                {
                    var stats = currentManagedProcess(data);
                    TraceGC _gc = TraceGarbageCollector.GetCurrentGC(stats, data.TimeStampRelativeMSec);
                    if (_gc != null)
                    {
                        // ObjectID is supposed to be an IntPtr. But "Address" is defined as UInt64 in 
                        // TraceEvent.
                        if (_gc.PinnedPlugs == null)
                        {
                            _gc.PinnedPlugs = new List<TraceGC.PinnedPlug>();
                        }

                        _gc.PinnedPlugs.Add(new TraceGC.PinnedPlug(data.PlugStart, data.PlugEnd));
                    }
                };

                // Sometimes at the end of a trace I see only some mark events are included in the trace and they
                // are not in order, so need to anticipate that scenario.
                clrPrivate.GCMarkStackRoots += delegate (GCMarkTraceData data)
                {
                    var stats = currentManagedProcess(data);
                    stats.GC.m_stats.AddServerGCThreadFromMark(data.ThreadID, data.HeapNum);

                    TraceGC _gc = TraceGarbageCollector.GetCurrentGC(stats, data.TimeStampRelativeMSec);
                    if (_gc != null)
                    {
                        if (_gc.PerHeapMarkTimes == null)
                        {
                            _gc.PerHeapMarkTimes = new Dictionary<int, MarkInfo>();
                        }

                        if (!_gc.PerHeapMarkTimes.ContainsKey(data.HeapNum))
                        {
                            _gc.PerHeapMarkTimes.Add(data.HeapNum, new MarkInfo(false));
                        }

                        _gc.PerHeapMarkTimes[data.HeapNum].MarkTimes[(int)MarkRootType.MarkStack] = data.TimeStampRelativeMSec;
                    }
                };

                clrPrivate.GCMarkFinalizeQueueRoots += delegate (GCMarkTraceData data)
                {
                    var stats = currentManagedProcess(data);
                    TraceGC _gc = TraceGarbageCollector.GetCurrentGC(stats, data.TimeStampRelativeMSec);
                    if (_gc != null)
                    {
                        if ((_gc.PerHeapMarkTimes != null) && _gc.PerHeapMarkTimes.ContainsKey(data.HeapNum))
                        {
                            _gc.PerHeapMarkTimes[data.HeapNum].MarkTimes[(int)MarkRootType.MarkFQ] =
                                data.TimeStampRelativeMSec;
                        }
                    }
                };

                clrPrivate.GCMarkHandles += delegate (GCMarkTraceData data)
                {
                    var stats = currentManagedProcess(data);
                    TraceGC _gc = TraceGarbageCollector.GetCurrentGC(stats, data.TimeStampRelativeMSec);
                    if (_gc != null)
                    {
                        if ((_gc.PerHeapMarkTimes != null) && _gc.PerHeapMarkTimes.ContainsKey(data.HeapNum))
                        {
                            _gc.PerHeapMarkTimes[data.HeapNum].MarkTimes[(int)MarkRootType.MarkHandles] =
                               data.TimeStampRelativeMSec;
                        }
                    }
                };

                clrPrivate.GCMarkCards += delegate (GCMarkTraceData data)
                {
                    var stats = currentManagedProcess(data);
                    TraceGC _gc = TraceGarbageCollector.GetCurrentGC(stats, data.TimeStampRelativeMSec);
                    if (_gc != null)
                    {
                        if ((_gc.PerHeapMarkTimes != null) && _gc.PerHeapMarkTimes.ContainsKey(data.HeapNum))
                        {
                            _gc.PerHeapMarkTimes[data.HeapNum].MarkTimes[(int)MarkRootType.MarkOlder] =
                                data.TimeStampRelativeMSec;
                        }
                    }
                };

                clrPrivate.GCGlobalHeapHistory += delegate (GCGlobalHeapHistoryTraceData data)
                {
                    var stats = currentManagedProcess(data);
                    GCStats.ProcessGlobalHistory(stats, data);
                };

                clrPrivate.GCPerHeapHistory += delegate (GCPerHeapHistoryTraceData data)
                {
                    var stats = currentManagedProcess(data);
                    GCStats.ProcessPerHeapHistory(stats, data);
                };

                clrPrivate.GCBGCStart += delegate (GCNoUserDataTraceData data)
                {
                    var stats = currentManagedProcess(data);
                    TraceGC _gc = stats.GC.m_stats.currentBGC;
                    if (_gc != null)
                    {
                        if (stats.GC.m_stats.backgroundGCThreads == null)
                        {
                            stats.GC.m_stats.backgroundGCThreads = new Dictionary<int, object>(16);
                        }
                        stats.GC.m_stats.backgroundGCThreads[data.ThreadID] = null;
                        _gc.BGCCurrentPhase = BGCPhase.BGC1stNonConcurrent;
                    }
                };

                clrPrivate.GCBGC1stNonCondStop += delegate (GCNoUserDataTraceData data)
                {
                    var stats = currentManagedProcess(data);
                    TraceGC _gc = stats.GC.m_stats.currentBGC;
                    if (_gc != null)
                    {
                        _gc.BGCCurrentPhase = BGCPhase.BGC1stConcurrent;
                    }
                };

                clrPrivate.GCBGC2ndNonConStart += delegate (GCNoUserDataTraceData data)
                {
                    var stats = currentManagedProcess(data);
                    TraceGC _gc = stats.GC.m_stats.currentBGC;
                    if (_gc != null)
                    {
                        _gc.BGCCurrentPhase = BGCPhase.BGC2ndNonConcurrent;
                    }
                };

                clrPrivate.GCBGC2ndConStart += delegate (GCNoUserDataTraceData data)
                {
                    var stats = currentManagedProcess(data);
                    TraceGC _gc = stats.GC.m_stats.currentBGC;
                    if (_gc != null)
                    {
                        _gc.BGCCurrentPhase = BGCPhase.BGC2ndConcurrent;
                    }
                };

                clrPrivate.GCBGCRevisit += delegate (BGCRevisitTraceData data)
                {
                    var stats = currentManagedProcess(data);
                    TraceGC _gc = stats.GC.m_stats.currentBGC;
                    if (_gc != null)
                    {
                        Debug.Assert(_gc.Type == GCType.BackgroundGC);
                        int iStateIndex = ((_gc.BGCCurrentPhase == BGCPhase.BGC1stConcurrent) ?
                                           (int)TraceGC.BGCRevisitState.Concurrent :
                                           (int)TraceGC.BGCRevisitState.NonConcurrent);
                        int iHeapTypeIndex = ((data.IsLarge == 1) ? (int)TraceGC.HeapType.LOH : (int)TraceGC.HeapType.SOH);
                        _gc.EnsureBGCRevisitInfoAlloc();
                        (_gc.BGCRevisitInfoArr[iStateIndex][iHeapTypeIndex]).PagesRevisited += data.Pages;
                        (_gc.BGCRevisitInfoArr[iStateIndex][iHeapTypeIndex]).ObjectsRevisited += data.Objects;
                    }
                };

                source.Clr.GCStop += delegate (GCEndTraceData data)
                {
                    MaybePrintEvent(data);
                    var stats = currentManagedProcess(data);
                    TraceGC _gc = TraceGarbageCollector.GetCurrentGC(stats, data.TimeStampRelativeMSec, mustBeStarted: true, expectedGCNumber: data.Count);
                    if (_gc != null)
                    {
                        _gc.DurationMSec = data.TimeStampRelativeMSec - _gc.StartRelativeMSec;
                        _gc.PauseEndRelativeMSec = data.TimeStampRelativeMSec; // Will likely be overwritten by a RestartEEStop
                    }
                };

                source.Clr.GCHeapStats += delegate (GCHeapStatsTraceData data)
                {
                    var process = data.Process();
                    var stats = currentManagedProcess(data);
                    TraceGC _gc = TraceGarbageCollector.GetCurrentGC(stats, data.TimeStampRelativeMSec);

                    var sizeAfterMB = (data.GenerationSize1 + data.GenerationSize2 + data.GenerationSize3) / 1000000.0;
                    if (_gc != null)
                    {
                        _gc.HeapStats = new GCHeapStats()
                        {
                            Depth = data.Depth
                            ,
                            FinalizationPromotedCount = data.FinalizationPromotedCount
                            ,
                            FinalizationPromotedSize = data.FinalizationPromotedSize
                            ,
                            GCHandleCount = data.GCHandleCount
                            ,
                            GenerationSize0 = data.GenerationSize0
                            ,
                            GenerationSize1 = data.GenerationSize1
                            ,
                            GenerationSize2 = data.GenerationSize2
                            ,
                            GenerationSize3 = data.GenerationSize3
                            ,
                            PinnedObjectCount = data.PinnedObjectCount
                            ,
                            SinkBlockCount = data.SinkBlockCount
                            ,
                            TotalHeapSize = data.TotalHeapSize
                            ,
                            TotalPromoted = data.TotalPromoted
                            ,
                            TotalPromotedSize0 = data.TotalPromotedSize0
                            ,
                            TotalPromotedSize1 = data.TotalPromotedSize1
                            ,
                            TotalPromotedSize2 = data.TotalPromotedSize2
                            ,
                            TotalPromotedSize3 = data.TotalPromotedSize3
                        };

                        if (_gc.Type == GCType.BackgroundGC)
                        {
                            _gc.ProcessCpuMSec = process.CPUMSec - _gc.ProcessCpuAtLastGC;
                            _gc.DurationSinceLastRestartMSec = data.TimeStampRelativeMSec - stats.GC.m_stats.lastRestartEndTimeRelativeMSec;
                        }
                        else
                        {
                            _gc.ProcessCpuMSec = process.CPUMSec - stats.GC.m_stats.ProcessCpuAtLastGC;
                            _gc.DurationSinceLastRestartMSec = _gc.PauseStartRelativeMSec - stats.GC.m_stats.lastRestartEndTimeRelativeMSec;
                        }

                        if (stats.GC.m_stats.HasAllocTickEvents)
                        {
                            _gc.HasAllocTickEvents = true;
                            _gc.AllocedSinceLastGCBasedOnAllocTickMB[0] = stats.GC.m_stats.allocTickCurrentMB[0] - stats.GC.m_stats.allocTickAtLastGC[0];
                            _gc.AllocedSinceLastGCBasedOnAllocTickMB[1] = stats.GC.m_stats.allocTickCurrentMB[1] - stats.GC.m_stats.allocTickAtLastGC[1];
                        }

                        // This is where a background GC ends.
                        if ((_gc.Type == GCType.BackgroundGC) && (stats.GC.m_stats.currentBGC != null))
                        {
                            stats.GC.m_stats.currentBGC.OnEnd(stats.GC); // set IsComplete = true;
                            stats.GC.m_stats.lastCompletedGC = stats.GC.m_stats.currentBGC;
                            stats.GC.m_stats.currentBGC = null;

                            // fire event
                            if (stats.GCEnd != null)
                            {
                                stats.GCEnd(process, stats.GC.m_stats.lastCompletedGC);
                            }
                        }

                        if (_gc.IsConcurrentGC)
                        {
                            Debug.Assert(_gc.is20Event);
                            _gc.OnEnd(stats.GC); // set IsComplete = true
                            stats.GC.m_stats.lastCompletedGC = _gc;

                            // fire event
                            if (stats.GCEnd != null)
                            {
                                stats.GCEnd(process, _gc);
                            }
                        }
                    }

                    stats.GC.m_stats.ProcessCpuAtLastGC = process.CPUMSec;
                    stats.GC.m_stats.allocTickAtLastGC[0] = stats.GC.m_stats.allocTickCurrentMB[0];
                    stats.GC.m_stats.allocTickAtLastGC[1] = stats.GC.m_stats.allocTickCurrentMB[1];
                    stats.GC.m_stats.lastRestartEndTimeRelativeMSec = data.TimeStampRelativeMSec;
                };

                source.Clr.GCTerminateConcurrentThread += delegate (GCTerminateConcurrentThreadTraceData data)
                {
                    var stats = currentManagedProcess(data);
                    if (stats.GC.m_stats.backgroundGCThreads != null)
                    {
                        stats.GC.m_stats.backgroundGCThreads = null;
                    }
                };

                clrPrivate.GCBGCAllocWaitStart += delegate (BGCAllocWaitTraceData data)
                {
                    var stats = currentManagedProcess(data);
                    Debug.Assert(stats.GC.m_stats.currentBGC != null);

                    if (stats.GC.m_stats.currentBGC != null)
                    {
                        stats.GC.m_stats.currentBGC.AddLOHWaitThreadInfo(data.ThreadID, data.TimeStampRelativeMSec, data.Reason, true);
                    }
                };

                clrPrivate.GCBGCAllocWaitStop += delegate (BGCAllocWaitTraceData data)
                {
                    var stats = currentManagedProcess(data);

                    TraceGC _gc = GCStats.GetLastBGC(stats);

                    if (_gc != null)
                    {
                        _gc.AddLOHWaitThreadInfo(data.ThreadID, data.TimeStampRelativeMSec, data.Reason, false);
                    }
                };

                clrPrivate.GCJoin += handleJoin;

                source.Clr.GCFinalizeObject += data =>
                {
                    var stats = currentManagedProcess(data);
                    long finalizationCount;
                    stats.GC.m_stats.FinalizedObjects[data.TypeName] =
                        stats.GC.m_stats.FinalizedObjects.TryGetValue(data.TypeName, out finalizationCount) ?
                            finalizationCount + 1 :
                            1;
                };

            }

            //
            // Jit
            //
            bool backgroundJITEventsOn = false;
            if (processJITEvents)
            {
                source.Clr.MethodJittingStarted += delegate (MethodJittingStartedTraceData data)
                {
                    var process = data.Process();
                    var stats = currentManagedProcess(data);
                    var _method = stats.JIT.m_stats.LogJitStart(stats, data, JITStats.GetMethodName(data), data.MethodILSize, data.ModuleID, data.MethodID);

                    // fire event
                    if (stats.JITMethodStart != null)
                    {
                        stats.JITMethodStart(process, _method);
                    }

                    // check if we should apply a lifetime limit to the method cache
                    if (source.DataLifetimeEnabled() && data.TimeStampRelativeMSec >= stats.JIT.NextRelativeTimeStampMsec)
                    {
                        // note the next time that lifetime should be applied, to avoid cleaningup too frequently
                        stats.JIT.NextRelativeTimeStampMsec = data.TimeStampRelativeMSec + (source.DataLifetimeMsec / 2.0);
                        // trim the methods to only include those that were JITT'd after the lifetime timestamp
                        stats.JIT.m_methods = stats.JIT.m_methods.Where(meth => meth.StartTimeMSec >= (data.TimeStampRelativeMSec - source.DataLifetimeMsec)).ToList();
                    }
                };
                ClrRundownTraceEventParser parser = new ClrRundownTraceEventParser(source);
                Action<ModuleLoadUnloadTraceData> moduleLoadAction = delegate (ModuleLoadUnloadTraceData data)
                {
                    var stats = currentManagedProcess(data);
                    stats.JIT.m_stats.moduleNamesFromID[data.ModuleID] = data.ModuleILPath;

                    // fix-up methods that have been previously marked with incomplete module information
                    foreach (var _method in stats.JIT.Methods.Where(m => m.ModuleID == data.ModuleID))
                    {
                        if (string.IsNullOrWhiteSpace(_method.ModuleILPath))
                        {
                            _method.ModuleILPath = data.ModuleILPath;
                        }
                    }
                };
                source.Clr.LoaderModuleLoad += moduleLoadAction;
                source.Clr.LoaderModuleUnload += moduleLoadAction;
                parser.LoaderModuleDCStop += moduleLoadAction;

                source.Clr.MethodLoadVerbose += delegate (MethodLoadUnloadVerboseTraceData data)
                {
                    if (data.IsJitted)
                    {
                        var process = data.Process();
                        var stats = currentManagedProcess(data);

                        bool createdNewMethod;
                        var _method = JITStats.MethodComplete(stats, data, data.MethodSize, data.ModuleID, JITStats.GetMethodName(data), data.MethodID, (int)data.ReJITID, out createdNewMethod);

                        // fire event - but only once
                        if (createdNewMethod && stats.JITMethodStart != null)
                        {
                            stats.JITMethodStart(process, _method);
                        }

                        if (stats.JITMethodEnd != null && _method.Completed == 1)
                        {
                            stats.JITMethodEnd(process, _method);
                        }
                    }
                };

                source.Clr.MethodLoad += delegate (MethodLoadUnloadTraceData data)
                {
                    if (data.IsJitted)
                    {
                        var process = data.Process();
                        var stats = currentManagedProcess(data);

                        bool createdNewMethod;
                        var _method = JITStats.MethodComplete(stats, data, data.MethodSize, data.ModuleID, "", data.MethodID, 0, out createdNewMethod);

                        // fire event - but only once
                        if (createdNewMethod && stats.JITMethodStart != null)
                        {
                            stats.JITMethodStart(process, _method);
                        }

                        if (stats.JITMethodEnd != null && _method.Completed == 1)
                        {
                            stats.JITMethodEnd(process, _method);
                        }
                    }
                };
                source.Clr.RuntimeStart += delegate (RuntimeInformationTraceData data)
                {
                    var process = data.Process();
                    var stats = currentManagedProcess(data);
                    stats.JIT.m_stats.IsClr4 = true;
                    if (process.CommandLine == null)
                    {
                        process.CommandLine = data.CommandLine;
                    }
                };

                clrPrivate.ClrMulticoreJitCommon += delegate (MulticoreJitPrivateTraceData data)
                {
                    var process = data.Process();
                    var stats = currentManagedProcess(data);
                    if (!backgroundJITEventsOn)
                    {
                        stats.JIT.m_stats.LastBlockedReason = null;
                    }
                    backgroundJITEventsOn = true;

                    if (process.Name == null)
                    {
                        process.name = data.ProcessName;
                    }

                    if (stats.JIT.m_stats.BackgroundJitThread == 0 && (data.String1 == "GROUPWAIT" || data.String1 == "JITTHREAD"))
                    {
                        stats.JIT.m_stats.BackgroundJitThread = data.ThreadID;
                    }

                    if (data.String1 == "ADDMODULEDEPENDENCY")
                    {
                        // Add the blocked module to the list of recorded modules.
                        if (!stats.JIT.m_stats.RecordedModules.Contains(data.String2))
                        {
                            stats.JIT.m_stats.RecordedModules.Add(data.String2);
                        }
                    }

                    if (data.String1 == "BLOCKINGMODULE")
                    {
                        // Set the blocking module.
                        stats.JIT.m_stats.LastBlockedReason = data.String2;

                        // Add the blocked module to the list of recorded modules.
                        if (!stats.JIT.m_stats.RecordedModules.Contains(data.String2))
                        {
                            stats.JIT.m_stats.RecordedModules.Add(data.String2);
                        }
                    }

                    if (data.String1 == "GROUPWAIT" && data.String2 == "Leave")
                    {
                        if (data.Int2 == 0)
                        {
                            // Clear the last blocked reason, since we're no longer blocked on modules.
                            stats.JIT.m_stats.LastBlockedReason = null;
                        }
                        else
                        {
                            // If GroupWait returns and Int2 != 0, this means that not all of the module loads were satisifed
                            // and we have aborted playback.
                            stats.JIT.m_stats.LastBlockedReason = "Playback Aborted";
                            stats.JIT.m_stats.playbackAborted = true;
                        }
                    }

                    if (data.String1 == "ABORTPROFILE")
                    {
                        stats.JIT.m_stats.BackgroundJitAbortedAtMSec = data.TimeStampRelativeMSec;
                    }
                };
                clrPrivate.ClrMulticoreJitMethodCodeReturned += delegate (MulticoreJitMethodCodeReturnedPrivateTraceData data)
                {
                    backgroundJITEventsOn = true;

                    var stats = currentManagedProcess(data);

                    // Get the associated JIT information
                    TraceJittedMethod backgroundJitInfo = null;

                    JITStats.MethodKey methodKey = new JITStats.MethodKey(data.ModuleID, data.MethodID);
                    if (stats.JIT.m_stats.backgroundJitEvents.TryGetValue(methodKey, out backgroundJitInfo))
                    {
                        if (backgroundJitInfo.ThreadID == stats.JIT.m_stats.BackgroundJitThread)
                        {
                            backgroundJitInfo.ForegroundMethodRequestTimeMSec = data.TimeStampRelativeMSec;
                            stats.JIT.m_stats.backgroundJitEvents.Remove(methodKey);
                        }
                    }
                };

                clrPrivate.BindingLoaderPhaseStart += delegate (BindingTraceData data)
                {
                    // Keep track if the last assembly loaded before Background JIT aborts.  
                    var stats = currentManagedProcess(data);
                    if (stats.JIT.m_stats.BackgroundJitAbortedAtMSec == 0)
                    {
                        stats.JIT.m_stats.LastAssemblyLoadNameBeforeAbort = data.AssemblyName;
                        stats.JIT.m_stats.LastAssemblyLoadBeforeAbortMSec = data.TimeStampRelativeMSec;
                    }
                };

                clrPrivate.BindingLoaderDeliverEventsPhaseStop += delegate (BindingTraceData data)
                {
                    // If we hit this events, we assume assembly load is successful. 

                    var stats = currentManagedProcess(data);
                    if (stats.JIT.m_stats.BackgroundJitAbortedAtMSec != 0)
                    {
                        if (stats.JIT.m_stats.LastAssemblyLoadNameBeforeAbort == data.AssemblyName)
                        {
                            stats.JIT.m_stats.LastAssemblyLoadBeforeAbortSuccessful = true;
                        }
                    }
                };

                clrPrivate.StartupPrestubWorkerStart += delegate (StartupTraceData data)
                {
                    // TODO, we want to know if we have background JIT events.   Today we don't have an event
                    // that says 'events are enabled, its just no one used the events'  We want this.  
                    // Today we turn on all CLRPrivate events to turn on listening to Backgroung JITTing and
                    // we use the fact that the PrestubWorker evnets are on as a proxy.  
                    backgroundJITEventsOn = true;
                };
                source.Clr.AppDomainResourceManagementThreadTerminated += delegate (ThreadTerminatedOrTransitionTraceData data)
                {

                    var stats = currentManagedProcess(data);
                    if (!stats.JIT.m_stats.playbackAborted)
                    {
                        stats.JIT.m_stats.LastBlockedReason = "Playback Completed";
                    }
                };

                source.Clr.MethodInliningSucceeded += delegate (MethodJitInliningSucceededTraceData data)
                {
                    var stats = currentManagedProcess(data);
                    stats.JIT.m_stats.InliningSuccesses.Add(new InliningSuccessResult
                    {
                        MethodBeingCompiled = data.MethodBeingCompiledNamespace + "." + data.MethodBeingCompiledName,
                        Inliner = data.InlinerNamespace + "." + data.InlinerName,
                        Inlinee = data.InlineeNamespace + "." + data.InlineeName
                    });
                };
                source.Clr.MethodInliningFailedAnsi += delegate (MethodJitInliningFailedAnsiTraceData data)
                {
                    var stats = currentManagedProcess(data);
                    stats.JIT.m_stats.InliningFailures.Add(new InliningFailureResult
                    {
                        MethodBeingCompiled = data.MethodBeingCompiledNamespace + "." + data.MethodBeingCompiledName,
                        Inliner = data.InlinerNamespace + "." + data.InlinerName,
                        Inlinee = data.InlineeNamespace + "." + data.InlineeName,
                        Reason = data.FailReason
                    });
                };
                source.Clr.MethodInliningFailed += delegate (MethodJitInliningFailedTraceData data)
                {
                    var stats = currentManagedProcess(data);
                    stats.JIT.m_stats.InliningFailures.Add(new InliningFailureResult
                    {
                        MethodBeingCompiled = data.MethodBeingCompiledNamespace + "." + data.MethodBeingCompiledName,
                        Inliner = data.InlinerNamespace + "." + data.InlinerName,
                        Inlinee = data.InlineeNamespace + "." + data.InlineeName,
                        Reason = data.FailReason
                    });
                };
            }
        }

        private static V GetOrInit<K, V>(Dictionary<K, V> dict, K key, Func<V> getValue)
        {
            if (dict.TryGetValue(key, out V value))
            {
                return value;
            }
            else
            {
                V newValue = getValue();
                dict[key] = newValue;
                return newValue;
            }
        }

        internal const bool DEBUG_PRINT_GC = false;
        internal const bool DEBUG_IGNORE_JOINS = false;
        internal const bool DONTUSE_IGNORE_MISSING_JOIN_EVENTS = false;

        private static void MaybePrintEvent(TraceEvent te)
        {
            if (DEBUG_PRINT_GC)
            {
                PrintEvent(te);
            }
        }

        private static void PrintEvent(TraceEvent te)
        {
            StringBuilder sb = new StringBuilder();
            te.ToXml(sb);
            Console.WriteLine(sb.ToString());
        }

        private Version runtimeVersion;
    #endregion
    }

    // This could be merged into GcJoinID, but this is experimental and that isn't.
    [Obsolete] // Experimental
    public enum GCJoinStage : sbyte
    {
        restart = -1,
        init_cpu_mapping = 0,
        done = 1,
        generation_determined = 2,
        begin_mark_phase = 3,
        scan_dependent_handles = 4,
        rescan_dependent_handles = 5,
        scan_sizedref_done = 6,
        null_dead_short_weak = 7,
        scan_finalization = 8,
        null_dead_long_weak = 9,
        null_dead_syncblk = 10,
        decide_on_compaction = 11,
        rearrange_segs_compaction = 12,
        adjust_handle_age_compact = 13,
        adjust_handle_age_sweep = 14,
        begin_relocate_phase = 15,
        relocate_phase_done = 16,
        verify_objects_done = 17,
        start_bgc = 18,
        restart_ee = 19,
        concurrent_overflow = 20,
        suspend_ee = 21,
        bgc_after_ephemeral = 22,
        allow_fgc = 23,
        bgc_sweep = 24,
        suspend_ee_verify = 25,
        restart_ee_verify = 26,
        set_state_free = 27,
        r_join_update_card_bundle = 28,
        after_absorb = 29,
        verify_copy_table = 30,
        after_reset = 31,
        after_ephemeral_sweep = 32,
        after_profiler_heap_walk = 33,
        minimal_gc = 34,
        after_commit_soh_no_gc = 35,
        expand_loh_no_gc = 36,
        final_no_gc = 37,
        disable_software_write_watch = 38,
        count = 39,
    }

    [Obsolete] // Experimental
    public static class GCJoinStageUtil
    {
        public static bool IsRJoinStage(GCJoinStage stage) =>
            stage == GCJoinStage.r_join_update_card_bundle;

        public static bool IsPossibleFinalStage(GCJoinStage stage)
        {
            switch (stage)
            {
                case GCJoinStage.done:
                case GCJoinStage.start_bgc:
                case GCJoinStage.bgc_after_ephemeral:
                    return true;
                default:
                    return false;
            }
        }

        // Some stages only happen in background  GCs.
        internal static GcBackgroundKind? TryGetBackgroundKindFromJoinStage(GCJoinStage stage)
        {
            if (TryGetThreadKindFromJoinStage(stage) == GCThreadKind.Background)
            {
                // Only background GCs can have background threads.
                return GcBackgroundKind.Background;
            }
            else
            {
                switch (stage)
                {
                    case GCJoinStage.start_bgc:
                    case GCJoinStage.restart_ee:
                        return GcBackgroundKind.Background;
                    default:
                        return null;
                }
            }
        }

        [Obsolete] // Experimental
        public static GCThreadKind? TryGetThreadKindFromJoinStage(GCJoinStage stage)
        {
            switch (stage)
            {
                case GCJoinStage.init_cpu_mapping:
                case GCJoinStage.allow_fgc:
                case GCJoinStage.bgc_sweep:
                    throw new Exception("These joins appear to never be used, don't know if BGC");

                case GCJoinStage.restart:
                case GCJoinStage.done:
                case GCJoinStage.scan_dependent_handles:
                case GCJoinStage.rescan_dependent_handles:
                case GCJoinStage.scan_sizedref_done:
                case GCJoinStage.null_dead_short_weak:
                case GCJoinStage.scan_finalization:
                case GCJoinStage.null_dead_long_weak:
                case GCJoinStage.null_dead_syncblk:
                case GCJoinStage.verify_objects_done:
                case GCJoinStage.verify_copy_table:
                    // Used in both ways
                    return null;

                case GCJoinStage.generation_determined:
                case GCJoinStage.begin_mark_phase:
                case GCJoinStage.decide_on_compaction:
                case GCJoinStage.rearrange_segs_compaction:
                case GCJoinStage.adjust_handle_age_compact:
                case GCJoinStage.adjust_handle_age_sweep:
                case GCJoinStage.begin_relocate_phase:
                case GCJoinStage.relocate_phase_done:
                case GCJoinStage.start_bgc:
                case GCJoinStage.bgc_after_ephemeral:
                case GCJoinStage.r_join_update_card_bundle:
                case GCJoinStage.minimal_gc:
                case GCJoinStage.after_commit_soh_no_gc:
                case GCJoinStage.expand_loh_no_gc:
                case GCJoinStage.final_no_gc:
                    return GCThreadKind.Foreground;

                case GCJoinStage.restart_ee:
                case GCJoinStage.concurrent_overflow:
                case GCJoinStage.suspend_ee:
                case GCJoinStage.suspend_ee_verify:
                case GCJoinStage.restart_ee_verify:
                case GCJoinStage.set_state_free:
                case GCJoinStage.after_absorb:
                case GCJoinStage.after_reset:
                case GCJoinStage.after_ephemeral_sweep:
                case GCJoinStage.after_profiler_heap_walk:
                case GCJoinStage.disable_software_write_watch:
                    return GCThreadKind.Background;

                default:
                    throw new Exception($"Unexpected join stage: {stage}");
            }
        }
    }

    internal readonly struct GCAndThreadKind
    {
        public readonly TraceGC GC;
        public readonly GCThreadKind ThreadKind;

        public GCAndThreadKind(TraceGC gc, GCThreadKind threadKind)
        {
            GC = gc;
            ThreadKind = threadKind;
        }
    }

    internal readonly struct GCAndJoinStage
    {
        public readonly TraceGC GC;
        public readonly GCJoinStage JoinStage;

        public GCAndJoinStage(TraceGC gc, GCJoinStage joinStage)
        {
            GC = gc;
            JoinStage = joinStage;
        }
    }

    /// <summary>
    /// Garbage Collector (GC) specific details about this process
    /// </summary>
    public class TraceGarbageCollector
    {
        /// <summary>
        /// Process view of GC statistics
        /// </summary>
        public GCStats Stats() { Calculate(); return m_stats; }
        /// <summary>
        /// Process view of GC generational statistics
        /// </summary>
        public GCStats[] Generations() { Calculate(); return m_generations; }

        /// <summary>
        /// Process view of all GCs
        /// </summary>
        public List<TraceGC> GCs { get { return m_gcs; } }

        #region private

        [Obsolete]
        internal readonly struct GCJoinState
        {
            public readonly GCJoinStateFgOrBg Fg;
            public readonly GCJoinStateFgOrBg Bg;

            public GCJoinState(
                GCJoinStateFgOrBg fg,
                GCJoinStateFgOrBg bg)
            {
                Fg = fg;
                Bg = bg;
            }
        }

        public class ManagedProcessJoinState
        {
            public uint? HeapCount;
            // These are added to but never removed from.
            public readonly HashSet<ThreadID> Fg;
            public readonly HashSet<ThreadID> Bg;

            public ManagedProcessJoinState()
            {
                HeapCount = null;
                Fg = new HashSet<ThreadID>();
                Bg = new HashSet<ThreadID>();
            }

            public bool HasThreadID(ThreadID id) =>
                Fg.Contains(id) || Bg.Contains(id);

            public GCThreadKind? GetThreadKindAndPossiblyAddThread(ThreadID threadID, GCJoinStage joinStage, GcJoinTime joinTime, GcJoinType joinType)
            {
                if (TraceLoadedDotNetRuntime.DEBUG_PRINT_GC)
                {
                    Console.WriteLine($"  {this}");
                }

                if (Fg.Contains(threadID))
                {
                    return GCThreadKind.Foreground;
                }
                else if (Bg.Contains(threadID))
                {
                    return GCThreadKind.Background;
                }
                else if (joinTime == GcJoinTime.Start && joinType != GcJoinType.Restart)
                {
                    // If this is a join start for certain stages, it may start a new GC.
                    switch (joinStage)
                    {
                        case GCJoinStage.generation_determined:
                        case GCJoinStage.begin_mark_phase:
                            Debug.Assert(GCJoinStageUtil.TryGetThreadKindFromJoinStage(joinStage) == GCThreadKind.Foreground);
                            Fg.Add(threadID);
                            if (HeapCount != null && Fg.Count > HeapCount)
                            {
                                throw new Exception($"Seen {Fg.Count} foreground threads but should be only {HeapCount} heaps");
                            }
                            return GCThreadKind.Foreground;

                        case GCJoinStage.restart_ee:
                            Debug.Assert(GCJoinStageUtil.TryGetThreadKindFromJoinStage(joinStage) == GCThreadKind.Background);
                            Bg.Add(threadID);
                            if (HeapCount != null && Bg.Count > HeapCount)
                            {
                                throw new Exception($"Seen {Bg.Count} foreground threads but should be only {HeapCount} heaps");
                            }
                            return GCThreadKind.Background;

                        default:
                            return null;
                    }
                }
                else
                {
                    return null;
                }
            }

            public GCThreadKind? GetThreadKind(ThreadID threadID)
            {
                if (Fg.Contains(threadID))
                {
                    return GCThreadKind.Foreground;
                }
                else if (Bg.Contains(threadID))
                {
                    return GCThreadKind.Background;
                }
                else
                {
                    return null;
                }
            }

            public override string ToString() =>
                $"fg: {ThreadIDsToString(Fg)}, bg: {ThreadIDsToString(Bg)}";

            private static string ThreadIDsToString(HashSet<ThreadID> threadIDs)
            {
                if (threadIDs.Any())
                {
                    return string.Join("|", threadIDs);
                }
                else
                {
                    return "empty";
                }
            }
        }

        [Obsolete]
        internal readonly struct GCJoinStateFgOrBg
        {
            private readonly struct SingleJoinState
            {
                public readonly GCJoinStage Stage;
                public readonly ushort SeenStarts;
                public readonly ushort SeenEnds;
                // Restart end is always the very last event for a join, so that tells us we shouldn't see any more events for it.
                public readonly bool SeenRestartEnd;
                public readonly ThreadID? ExpectingRestartThreadID;

                public SingleJoinState(
                    GCJoinStage stage,
                    ushort seenStarts,
                    ushort seenEnds,
                    bool seenRestartEnd,
                    ThreadID? expectingRestartThreadID)
                {
                    Debug.Assert(stage != GCJoinStage.restart, "join stage should never be 'restart'");
                    if (!TraceLoadedDotNetRuntime.DONTUSE_IGNORE_MISSING_JOIN_EVENTS)
                        Debug.Assert(seenEnds <= seenStarts);
                    Stage = stage;
                    SeenStarts = seenStarts;
                    SeenEnds = seenEnds;
                    SeenRestartEnd = seenRestartEnd;
                    ExpectingRestartThreadID = expectingRestartThreadID;
                    Debug.Assert(!SeenRestartEnd || ExpectingRestartThreadID == null, "Once SeenRestartEnd, we should clear ExpectingRestartThreadID.");
                }

                public bool AwaitingEnd =>
                    SeenStarts < SeenEnds;

                public static SingleJoinState Empty(GCJoinStage stage) =>
                    new SingleJoinState(stage, seenStarts: 0, seenEnds: 0, seenRestartEnd: false, expectingRestartThreadID: null);

                public SingleJoinState WithExpectingRestart(ThreadID threadID)
                {
                    Debug.Assert(ExpectingRestartThreadID == null);
                    Debug.Assert(!SeenRestartEnd);
                    return new SingleJoinState(
                        stage: Stage,
                        // This FirstJoin / LastJoin also counts as a join start.
                        seenStarts: Incr(SeenStarts),
                        seenEnds: SeenEnds,
                        seenRestartEnd: SeenRestartEnd,
                        expectingRestartThreadID: threadID);
                }

                private static ushort Add(ushort a, ushort b)
                {
                    int res = a + b;
                    Debug.Assert(res <= ushort.MaxValue);
                    return (ushort) res;
                }

                public static ushort Incr(ushort a) =>
                    Add(a, 1);

                public SingleJoinState WithSeenRestartEnd()
                {
                    Debug.Assert(!SeenRestartEnd);
                    return new SingleJoinState(
                        stage: Stage,
                        seenStarts: SeenStarts,
                        // Restart end is an end -- no JoinEnd event for those that started with FirstJoin / LastJoin
                        seenEnds: Incr(SeenEnds),
                        expectingRestartThreadID: null,
                        seenRestartEnd: true);
                }

                public SingleJoinState WithJoinStart() =>
                    new SingleJoinState(
                        stage: Stage,
                        seenStarts: Incr(SeenStarts),
                        seenEnds: SeenEnds,
                        expectingRestartThreadID: ExpectingRestartThreadID,
                        seenRestartEnd: SeenRestartEnd);

                public SingleJoinState WithJoinEnd()
                {
                    ushort newSeenEnds = Incr(SeenEnds);
                    if (newSeenEnds > SeenStarts)
                    {
                        if (!TraceLoadedDotNetRuntime.DONTUSE_IGNORE_MISSING_JOIN_EVENTS)
                            throw new Exception("Should not get more ends than starts");
                    }

                    return new SingleJoinState(
                        stage: Stage,
                        seenStarts: SeenStarts,
                        seenEnds: newSeenEnds,
                        expectingRestartThreadID: ExpectingRestartThreadID,
                        seenRestartEnd: SeenRestartEnd);
                }

                public bool SeenBegunEnding() =>
                    SeenEnds > 0 || ExpectingOrSeenRestart();

                public bool ExpectingOrSeenRestart() =>
                    ExpectingRestartThreadID != null || SeenRestartEnd;

                public override string ToString() =>
                    $"SingleJoinState({Stage}, {SeenStarts}, {SeenEnds}, {SeenRestartEnd}, {NullableToString(ExpectingRestartThreadID)})";
            }

            // Prev2Join is the join before PrevJoin.
            // It's rare but it is possible to have 3 joins simultaneously.
            // This happens only when there are many heaps and restarting takes a while.
            // We may have:
            // Thread A is the LastJoin for begin_mark_phase and begins restarting.
            // Thread B is restarted and advances to r_join_update_card_bundle.
            // Thread B does a restart start and end -- there are no threads that it needs to restart though.
            // Thread B then proceeds to join start for scan_dependent_handles.
            // Thread A finally finishes restarting all the other threads.
            private readonly SingleJoinState? Prev2Join;
            private readonly SingleJoinState? PrevJoin;
            // Might not have been a prevjoin, but if the GCJoinState exists there's always a CurJOin.
            private readonly SingleJoinState CurJoin;

            private GCJoinStateFgOrBg(
                SingleJoinState? prev2Join,
                SingleJoinState? prevJoin,
                SingleJoinState curJoin)
            {
                Prev2Join = prev2Join;
                PrevJoin = prevJoin;
                CurJoin = curJoin;

                if (Prev2Join != null)
                {
                    Debug.Assert(PrevJoin != null);
                }

                // TODO: delete (valid assertion, but too specific)
                if (CurJoin.Stage == GCJoinStage.done)
                {
                    if (PrevJoin == null)
                    {
                        throw new Exception("'done' should not be the first stage!");
                    }

                    // TODO: delete (valid assertion, but too specific)
                    if (prevJoin?.Stage == GCJoinStage.start_bgc)
                    {
                        throw new Exception("'done' should not immediately follow 'start_bgc'");
                    }
                }

                // Stages may repeat ater 2 intervening stages, but no repeats allowed in the most recent 3 stages.
                if (!StagesUniqueOrScanDependentHandles(Prev2Join?.Stage, PrevJoin?.Stage, CurJoin.Stage))
                {
                    throw new Exception($"Unexpected repeated join stage: {Prev2Join?.Stage}, {PrevJoin?.Stage}, {CurJoin.Stage}");
                }
                if (!UniqueIfNonNull<ThreadID>(Prev2Join?.ExpectingRestartThreadID, PrevJoin?.ExpectingRestartThreadID, CurJoin.ExpectingRestartThreadID, EqualityComparer<ThreadID>.Default))
                {
                    throw new Exception(
                        $"Unexpected repeated ExpectingRestartThreadID: {Prev2Join?.ExpectingRestartThreadID} {PrevJoin?.ExpectingRestartThreadID}, {CurJoin.ExpectingRestartThreadID}");
                }
            }
            
            private GCJoinStateFgOrBg WithCurJoin(SingleJoinState newCurJoin) =>
                new GCJoinStateFgOrBg(prev2Join: Prev2Join, prevJoin: PrevJoin, curJoin: newCurJoin);

            private GCJoinStateFgOrBg WithPrevJoin(SingleJoinState newPrevJoin) =>
                new GCJoinStateFgOrBg(prev2Join: Prev2Join, prevJoin: newPrevJoin, curJoin: CurJoin);

            private GCJoinStateFgOrBg WithPrev2Join(SingleJoinState newPrev2Join) =>
                new GCJoinStateFgOrBg(prev2Join: newPrev2Join, prevJoin: PrevJoin, curJoin: CurJoin);

            private static bool StagesUniqueOrScanDependentHandles(GCJoinStage? a, GCJoinStage? b, GCJoinStage c)
            {
                GCJoinStage? repeated = FindRepeatedValue(a, b, c, EqualityComparer<GCJoinStage>.Default);
                // Allow ScanDependentHandles to appear twice, but must be separated by at least 1
                return repeated == null || (a == c && IsRepeatableJoinStage(c) && b != c);
            }

            private static bool IsRepeatableJoinStage(GCJoinStage s)
            {
                switch (s)
                {
                    case GCJoinStage.scan_dependent_handles:
                    case GCJoinStage.rescan_dependent_handles:
                        return true;
                    default:
                        return false;
                }
            }

            private static bool UniqueIfNonNull<T>(T? a, T? b, T? c, IEqualityComparer<T> comparer) where T : struct =>
                FindRepeatedValue<T>(a, b, c, comparer) == null;

            private static T? FindRepeatedValue<T>(T? a, T? b, T? c, IEqualityComparer<T> comparer) where T : struct =>
                a != null && (b != null && comparer.Equals(a.Value, b.Value) || c != null && comparer.Equals(a.Value, c.Value))
                    ? a
                    : b != null && c != null && comparer.Equals(b.Value, c.Value) ? b : null;

            public bool IsExpectingRestartFromThread(ThreadID threadID) =>
                Prev2Join != null && Prev2Join.Value.ExpectingRestartThreadID == threadID
                || PrevJoin != null && PrevJoin.Value.ExpectingRestartThreadID == threadID
                || CurJoin.ExpectingRestartThreadID == threadID;

            public static GCJoinStateFgOrBg FreshWithJoinStart(GCJoinStage joinStage, bool expectRestart, ThreadID threadID)
            {
                Debug.Assert(joinStage != GCJoinStage.restart);
                return new GCJoinStateFgOrBg(
                    prev2Join: null,
                    prevJoin: null,
                    curJoin: SingleJoinState.Empty(joinStage)
                )
                    // Dont' worry about heapCount here, just used for asserting that there aren't too many starts
                    .WithJoinStart(joinStage, threadID, expectRestart, heapCount: null);
            }

            public GCJoinStateFgOrBg WithNewCur(GCJoinStage newCurJoinStage, bool expectRestart, ThreadID threadID)
            {
                if (!TraceLoadedDotNetRuntime.DONTUSE_IGNORE_MISSING_JOIN_EVENTS)
                {
                    if (Prev2Join?.SeenRestartEnd == false)
                    {
                        throw new Exception($"Never saw a restart end for {Prev2Join.Value.Stage}");
                    }

                    if (Prev2Join?.AwaitingEnd == true)
                    {
                        throw new Exception($"{Prev2Join?.Stage} has {Prev2Join?.SeenStarts} starts but only {Prev2Join?.SeenEnds} ends");
                    }
                }

                return new GCJoinStateFgOrBg(
                    prev2Join: PrevJoin,
                    prevJoin: CurJoin,
                    curJoin: SingleJoinState.Empty(newCurJoinStage)
                ).WithJoinStart(newCurJoinStage, threadID, expectRestart, heapCount: null);
            }

            public bool HasJoinStagePrev2OrPrevOrCur(GCJoinStage joinStage) =>
                joinStage == Prev2Join?.Stage || joinStage == PrevJoin?.Stage || HasJoinStageCur(joinStage);

            // Note: the order we check these matters since there may be scan_dependent_handles as both Prev2Join and CurJoin.
            // In that case, prefer CurJoin.
            private SingleJoinState? StateForStage(GCJoinStage stage) =>
                StateForStageNoPrev2(stage) ?? (Prev2Join?.Stage == stage ? Prev2Join : null);

            private SingleJoinState? StateForStageNoPrev2(GCJoinStage stage) =>
                CurJoin.Stage == stage? CurJoin
                : PrevJoin?.Stage == stage ? PrevJoin
                : null;

            public bool HasJoinWithRemainingStarts(GCJoinStage stage, uint heapCount)
            {
                SingleJoinState? state = StateForStage(stage);
                return state != null && state.Value.SeenStarts < heapCount;
            }

            public bool HasJoinWithNoRemainingStarts(GCJoinStage stage, uint heapCount)
            {
                SingleJoinState? state = StateForStage(stage);
                Debug.Assert(!(state?.SeenStarts > heapCount));
                return state?.SeenStarts == heapCount;
            }

            public bool HasJoinWithRemainingStartsOrCanStartNew(GCJoinStage stage, uint heapCount)
            {
                SingleJoinState? state = StateForStageNoPrev2(stage);
                // Apparently scan_dependent_handles can occur multiple times in quick succession, so allow that to start new at any time.
                return state == null || state.Value.SeenStarts < heapCount || IsRepeatableJoinStage(stage);
            }

            public bool CanEndJoin(GCJoinStage stage) =>
                StateForStage(stage)?.AwaitingEnd ?? false;

            public bool HasJoinStageCur(GCJoinStage joinStage) =>
                joinStage == CurJoin.Stage;

            // Default for null is an empty string which isn't helpful
            private static string NullableToString<T>(T? value) where T : struct =>
                value == null ? "null" : value.Value.ToString();

            public GCJoinStateFgOrBg WithSeenRestartStart(ThreadID threadID)
            {
                Debug.Assert(IsExpectingRestartFromThread(threadID));
                return this;
            }

            public GCJoinStage JoinStageForRestartingThread(ThreadID threadID)
            {
                Debug.Assert(IsExpectingRestartFromThread(threadID));
                if (threadID == Prev2Join?.ExpectingRestartThreadID)
                {
                    return Prev2Join.Value.Stage;
                }
                else if (threadID == PrevJoin?.ExpectingRestartThreadID)
                {
                    return PrevJoin.Value.Stage;
                }
                else if (threadID == CurJoin.ExpectingRestartThreadID)
                {
                    return CurJoin.Stage;
                }
                else
                {
                    throw new Exception();
                }
            }

            public GCJoinStateFgOrBg WithSeenRestartEnd(ThreadID threadID)
            {
                Debug.Assert(IsExpectingRestartFromThread(threadID));

                // TODO: assert that the same threadID doesn't appear for multiple prev/cur join

                if (threadID == Prev2Join?.ExpectingRestartThreadID)
                {
                    return WithPrev2Join(Prev2Join.Value.WithSeenRestartEnd());
                }
                else if (threadID == PrevJoin?.ExpectingRestartThreadID)
                {
                    return WithPrevJoin(PrevJoin.Value.WithSeenRestartEnd());
                }
                else if (threadID == CurJoin.ExpectingRestartThreadID)
                {
                    return WithCurJoin(CurJoin.WithSeenRestartEnd());
                }
                else
                {
                    throw new Exception();
                }
            }

            public GCJoinStateFgOrBg WithJoinStart(GCJoinStage stage, ThreadID threadID, bool expectRestart, uint? heapCount)
            {
                if (heapCount != null && !HasJoinWithRemainingStarts(stage, heapCount.Value))
                {
                    throw new Exception($"Getting a JoinStart for {stage} when we shouldn't");
                }

                // Apparently we can actually see a join start event come out after the restart end, so commenting out the following check.
                //if (CurJoin.SeenRestartEnd)
                //{
                //    throw new Exception("Already saw restart end, should not get any more JoinStarts");
                //}

                if (expectRestart)
                {
                    if (Prev2Join != null && !Prev2Join.Value.SeenBegunEnding())
                    {
                        throw new Exception($"Prev2Join ({Prev2Join.Value.Stage}) has not begun ending but the next join is starting?");
                    }

                    // If there's a PrevJoin, it should already have seen a FirstJoin/LastJoin.
                    if (PrevJoin != null && !PrevJoin.Value.SeenBegunEnding())
                    {
                        throw new Exception($"PrevJoin ({PrevJoin.Value.Stage}) has not begun ending but the next join is restarting?");
                    }

                    // Note: order we check these matters since both Prev2Join and CurJoin may be scan_dependent_handles.
                    if (stage == CurJoin.Stage)
                    {
                        return WithCurJoin(CurJoin.WithExpectingRestart(threadID));
                    }
                    else if (stage == PrevJoin?.Stage)
                    {
                        // TODO: actually, even with missing events this shouldn't happen.
                        if (!TraceLoadedDotNetRuntime.DONTUSE_IGNORE_MISSING_JOIN_EVENTS)
                            throw new Exception("Adding a new LastJoin for PrevJoin, but we're already on the next.");
                        return this;
                    }
                    else if (stage == Prev2Join?.Stage)
                    {
                        // TODO: actually, even with missing events this shouldn't happen.
                        if (!TraceLoadedDotNetRuntime.DONTUSE_IGNORE_MISSING_JOIN_EVENTS)
                            throw new Exception("Adding a new LastJoin for Prev2Join, but we're already on the next.");
                        return this;
                    }
                    else
                    {
                        // should be unreachable
                        throw new Exception("stage not found");
                    }
                }
                else
                {
                    return WithModifyJoin(stage, j => j.WithJoinStart());
                }
            }

            public GCJoinStateFgOrBg WithJoinEnd(GCJoinStage stage) =>
                WithModifyJoin(stage, j => j.WithJoinEnd());

            private GCJoinStateFgOrBg WithModifyJoin(GCJoinStage stage, Func<SingleJoinState, SingleJoinState> modify) =>
                // Note: order we check these matters since both Prev2Join and CurJoin may be scan_dependent_handles.
                stage == CurJoin.Stage? WithCurJoin(modify(CurJoin))
                : stage == PrevJoin?.Stage ? WithPrevJoin(modify(PrevJoin.Value))
                : stage == Prev2Join?.Stage ? WithPrev2Join(modify(Prev2Join.Value))
                : throw new Exception($"No join matches stage {stage}");

            public override string ToString() =>
                $"{nameof(GCJoinStateFgOrBg)}(prev2: {NullableToString(Prev2Join)}, prev: {NullableToString(PrevJoin)}, cur: {CurJoin})";
        }

        [Obsolete]
        internal static TraceGC GetCurrentGC(
            TraceLoadedDotNetRuntime proc,
            double timeStampRelativeMSec,
            bool mustBeStarted = false,
            int? expectedGCNumber = null,
            GCThreadKind? threadKind = null)
        {
            IReadOnlyList<TraceGC> gcs = proc.GC.GCs;
            if (gcs.Count > 0)
            {
                TraceGC last = gcs.Last();
                // Give a 1ms buffer, occasionally a join end event will come out a fraction of a millisecond after the GC is over
                //double lastPauseEndRelativeMSecSafe = last.PauseEndRelativeMSec + 1;

                bool bgcIsFinished = proc.GC.m_stats.currentBGC == null;
                //TODO: don't need currentOrFinishedBGC any more here
                TraceGC bgc = proc.GC.m_stats.currentBGC ?? proc.GC.m_stats.currentOrFinishedBGC;

                if (expectedGCNumber != null)
                {
                    if (bgc != null && expectedGCNumber == bgc.Number)
                    {
                        return bgc;
                    }
                    else if (last != null && expectedGCNumber == last.Number)
                    {
                        return last;
                    }
                }
                else
                {
                    if (threadKind == GCThreadKind.Background)
                    {
                        return NonNull(bgc);
                    }

                    if (!mustBeStarted || last.SeenStartEvent)
                    {
                        if (!last.IsComplete)
                        {
                            return last;
                        }
                    }

                    if (bgc != null && (!mustBeStarted || bgc.SeenStartEvent))
                    {
                        if (!bgcIsFinished)// || timeStampRelativeMSec < bgc.PauseEndRelativeMSec + 1))
                        {
                            Debug.Assert(bgc.SeenStartEvent);
                            Debug.Assert(bgc.Type == GCType.BackgroundGC);
                            return bgc;
                        }
                    }
                }
            }
            return null;

        }

        static T NonNull<T>(T value) where T : class
        {
            if (value == null)
            {
                throw new Exception("Value was null");
            }
            return value;
        }

        private static V? TryGet<K, V>(IReadOnlyDictionary<K, V> d, K k) where V : struct
        {
            return d.TryGetValue(k, out V v) ? v : (V?) null;
        }

        private static string ShowGc(TraceGC gc) =>
            gc == null
                ? "null"
                : $"n: {gc.Number}; hash: {gc.GetHashCode()}; started: {gc.SeenStartEvent}; complete: {gc.IsComplete}; type: {gc.Type}";

        private static void PrintGCs(TraceLoadedDotNetRuntime proc, IReadOnlyDictionary<TraceGC, GCJoinStateFgOrBg> joinStates, string name, bool showRes, TraceGC res)
        {
            if (TraceLoadedDotNetRuntime.DEBUG_PRINT_GC)
            {
                IReadOnlyList<TraceGC> gcs = proc.GC.GCs;
                TraceGC last = gcs.Count == 0 ? null : gcs.Last();
                TraceGC bgc = proc.GC.m_stats.currentBGC ?? proc.GC.m_stats.currentOrFinishedBGC;
                if (res == null)
                {
                    Console.WriteLine($"  last: {ShowGc(last)}");
                    Console.WriteLine($"  bgc: {(bgc == last ? "same" : ShowGc(bgc))}");
                }
                Console.WriteLine($"  {name}LastState: {(last == null ? null : TryGet(joinStates, last))}");
                if (bgc != last)
                {
                    string bgcState = bgc == null ? "null" : TryGet(joinStates, bgc).ToString();
                    Console.WriteLine($"  {name}BgcState: {bgcState}");
                }
                if (showRes)
                {
                    if (res == null)
                    {
                        Console.WriteLine("  returning null");
                    }
                    else
                    {
                        Debug.Assert(res == last || res == bgc);
                        string gcName = res == last ? "last" : "bgc";
                        Console.WriteLine($"  returning {gcName} (number {res.Number}, hash {res.GetHashCode()})");
                    }
                }
            }
        }

        // Some GCs don't have a SuspendEEStart event.
        // Since the first JoinStart comes *before* the GCSTart event, we'll need to add it at the first join start.
        internal static bool JoinIndicatesNewGc(
            TraceLoadedDotNetRuntime proc,
            IReadOnlyDictionary<TraceGC, GCJoinStateFgOrBg> joinStates,
            GCJoinStage joinStage,
            GcJoinTime joinTime)
        {
            if (joinStage == GCJoinStage.generation_determined && joinTime == GcJoinTime.Start)
            {
                if (proc.GC.GCs.Any())
                {
                    TraceGC last = proc.GC.GCs.Last();
                    if (joinStates.TryGetValue(last, out GCJoinStateFgOrBg lastState))
                    {
                        // Since generation_determined must be the first stage, if a state exists but is beyond generation_determined, this new generation_determined must be for a new GC.
                        return !lastState.HasJoinStagePrev2OrPrevOrCur(joinStage);
                    }
                    else
                    {
                        return false;
                    }
                }
                else
                {
                    // No GCs yet, so of course we need to add one.
                    return true;
                }
            }
            else
            {
                // only generation_determined should start a new GC.
                return false;
            }
        }

        internal static TraceGC GetCurrentGCForJoin(
            TraceLoadedDotNetRuntime proc,
            uint heapCount,
            double timeStampRelativeMSec,
            Dictionary<TraceGC, GCJoinStateFgOrBg> joinStates,
            GCJoinStage joinStage,
            GcJoinTime time,
            GcJoinType type,
            ThreadID threadId,
            GCThreadKind threadKind)
        {
            PrintGCs(proc, joinStates, "old", showRes: false, res: null);

            GCAndJoinStage? resWithStage = GetCurrentGCForJoinWorker(
                proc, timeStampRelativeMSec, joinStates, joinStage, time, type, threadId, threadKind, heapCount);
            TraceGC res;
            if (resWithStage == null)
            {
                res = null;
            }
            else
            {
                res = resWithStage.Value.GC;

                if (res != null && res.IsComplete)
                {
                    // Shouldn't be join events more than 1ms after the end.
                    Debug.Assert(res.PauseEndRelativeMSec < timeStampRelativeMSec + 1.0);
                }
            }

            PrintGCs(proc, joinStates, "new", showRes: true, res: res);
            return res;
        }

        private static GCAndJoinStage? GetCurrentGCForJoinWorker(
            TraceLoadedDotNetRuntime proc,
            double timeStampRelativeMSec,
            Dictionary<TraceGC, GCJoinStateFgOrBg> joinStates,
            GCJoinStage joinStage,
            GcJoinTime time,
            GcJoinType type,
            ThreadID threadId,
            GCThreadKind threadKind,
            uint heapCount)
        {
            bool bgcIsFinished = proc.GC.m_stats.currentBGC == null;
            //TODO: don't need currentOrFinishedBGC any more here?
            TraceGC bgc = proc.GC.m_stats.currentBGC ?? proc.GC.m_stats.currentOrFinishedBGC;

            IReadOnlyList<TraceGC> gcs = proc.GC.GCs;
            if (gcs.Count == 0)
            {
                return null;
            }

            TraceGC last = gcs.Last();

            GCJoinStateFgOrBg? oldLastState = TryGet(joinStates, last);
            GCJoinStateFgOrBg? oldBgcState = bgc == null ? null : TryGet(joinStates, bgc);

            switch (time)
            {
                case GcJoinTime.Start:
                    switch (type)
                    {
                        case GcJoinType.FirstJoin:
                        case GcJoinType.LastJoin:
                        case GcJoinType.Join:
                        {
                            TraceGC gc = HandleJoinStart(
                               joinStates: joinStates,
                               oldLastState: oldLastState,
                               oldBgcState: oldBgcState,
                               last: last,
                               bgc: bgc,
                               threadID: threadId,
                               threadKind: threadKind,
                               joinStage: joinStage,
                               expectRestart: type != GcJoinType.Join,
                               heapCount: heapCount);
                            return new GCAndJoinStage(gc, joinStage);
                        }
                        case GcJoinType.Restart:
                            return HandleRestartStart(
                                joinStates: joinStates, oldLastState: oldLastState, oldBgcState: oldBgcState, last: last, bgc: bgc, threadID: threadId);
                        default:
                            throw new Exception(type.ToString());
                    }
                case GcJoinTime.End:
                    switch (type)
                    {
                        case GcJoinType.FirstJoin:
                        case GcJoinType.LastJoin:
                        case GcJoinType.Join:
                        {
                            TraceGC gc = HandleJoinEnd(
                                joinStates: joinStates,
                                oldLastState: oldLastState,
                                oldBgcState: oldBgcState,
                                last: last,
                                bgc: bgc,
                                joinStage: joinStage,
                                threadKind: threadKind);
                            return new GCAndJoinStage(gc, joinStage);
                        }
                        case GcJoinType.Restart:
                            return HandleRestartEnd(
                                joinStates: joinStates, oldLastState: oldLastState, oldBgcState: oldBgcState, last: last, bgc: bgc, threadID: threadId);
                        default:
                            throw new Exception(type.ToString());
                    }
                default:
                    throw new Exception(time.ToString());
            }
        }

        private static TraceGC HandleJoinStart(
            Dictionary<TraceGC, GCJoinStateFgOrBg> joinStates,
            GCJoinStateFgOrBg? oldLastState,
            GCJoinStateFgOrBg? oldBgcState,
            TraceGC last,
            TraceGC/*?*/ bgc,
            ThreadID threadID,
            GCThreadKind threadKind,
            GCJoinStage joinStage,
            bool expectRestart,
            uint heapCount)
        {
            if (last.Type == GCType.BackgroundGC && last != bgc)
            {
                throw new Exception("Last is background?");
            }
            if (bgc != null && bgc.Type != GCType.BackgroundGC)
            {
                throw new Exception("BGC not a bgc?");
            }

            TraceGC/*?*/ gc = ChooseGcForJoinStart(oldLastState, oldBgcState, last, bgc, joinStage, threadKind, heapCount);
            if (gc == null)
            {
                return gc;
            }
            Debug.Assert(gc != null, "Chose a null GC?");

            Debug.Assert(gc == last || gc == bgc);
            Debug.Assert((gc?.Type == GCType.BackgroundGC) == (gc == bgc));
            GCJoinStateFgOrBg? oldState = gc == last ? oldLastState : oldBgcState;

            if (oldState == null)
            {
                joinStates[gc] = GCJoinStateFgOrBg.FreshWithJoinStart(joinStage, expectRestart, threadID);
            }
            // Only check PrevOrCur here and not prev2 -- we may see the same stage multiple times,
            // so consider a new JoinStart at the same stage as Prev2 to be a new join.
            else if (oldState.Value.HasJoinWithRemainingStarts(joinStage, heapCount))
            {
                joinStates[gc] = oldState.Value.WithJoinStart(joinStage, threadID, expectRestart: expectRestart, heapCount: heapCount);
            }
            else
            {
                joinStates[gc] = oldState.Value.WithNewCur(joinStage, expectRestart, threadID);
            }

            return gc;
        }

        private static TraceGC ChooseGcForJoinStart(
            GCJoinStateFgOrBg? oldLastState,
            GCJoinStateFgOrBg? oldBgcState,
            TraceGC last,
            TraceGC/*?*/ bgc,
            GCJoinStage joinStage,
            GCThreadKind threadKind,
            uint heapCount)
        {
            bool canLast = oldLastState == null
                || oldLastState.Value.HasJoinWithRemainingStartsOrCanStartNew(joinStage, heapCount);
            bool canBgc = bgc != null && (
                oldBgcState == null
                    || oldBgcState.Value.HasJoinWithRemainingStartsOrCanStartNew(joinStage, heapCount));
            switch (GCJoinStageUtil.TryGetBackgroundKindFromJoinStage(joinStage))
            {
                case GcBackgroundKind.Foreground:
                    Debug.Assert(threadKind == GCThreadKind.Foreground, "A foreground GC should only use foreground threads.");
                    Debug.Assert(canLast, "foreground stage, but already completed it");
                    return last;
                case GcBackgroundKind.Background:
                    Debug.Assert(bgc == null ? canLast : canBgc, "background stage, but already completed it");
                    // Possible that bgc is not set yet if this is the beginning of the trace and we missed the GC/Start event.
                    return bgc ?? last;
                case null:
                    switch (threadKind)
                    {
                        case GCThreadKind.Background:
                            Debug.Assert(canBgc, "choosing BGC, but it's done");
                            return bgc;
                        case GCThreadKind.Foreground:
                            // background GCs can have foreground threads. So this one is tricky.
                            if (canLast && !canBgc)
                            {
                                return last;
                            }
                            else if (!canLast && canBgc)
                            {
                                return bgc;
                            }
                            else if (!canLast && !canBgc)
                            {
                                throw new Exception("TODO: GC can't be last, can't be bgc...");
                            }
                            else
                            {
                                Debug.Assert(canLast && canBgc);

                                // Could be on 'last', could be on 'bgc'.
                                if (!last.IsComplete)
                                {
                                    return last;
                                }
                                else
                                {
                                    Debug.Assert(oldLastState.HasValue, "If the gc is complete we should have state by now");
                                    if (oldLastState.Value.HasJoinWithRemainingStarts(joinStage, heapCount))
                                    {
                                        return last;
                                    }
                                    else
                                    {
                                        return bgc ?? last;
                                    }
                                }
                            }
                        default:
                            throw new Exception(threadKind.ToString());
                    }

                default:
                    throw new Exception();
            }
        }

        private static TraceGC HandleJoinEnd(
            Dictionary<TraceGC, GCJoinStateFgOrBg> joinStates,
            GCJoinStateFgOrBg? oldLastState,
            GCJoinStateFgOrBg? oldBgcState,
            TraceGC last,
            TraceGC bgc,
            GCJoinStage joinStage,
            GCThreadKind threadKind)
        {
            TraceGC retLast()
            {
                joinStates[last] = oldLastState.Value.WithJoinEnd(joinStage);
                return last;
            };
            TraceGC retBgc()
            {
                joinStates[bgc] = oldBgcState.Value.WithJoinEnd(joinStage);
                return bgc;
            };

            bool lastHas = oldLastState != null && oldLastState.Value.HasJoinStagePrev2OrPrevOrCur(joinStage);
            bool bgcHas = oldBgcState != null && oldBgcState.Value.HasJoinStagePrev2OrPrevOrCur(joinStage);

            if (lastHas && bgcHas)
            {
                if (last == bgc)
                {
                    return retLast();
                }
                else
                {
                    // There can't be two background gcs. So only way there are two different gcs with the same state is if this is in the foreground.
                    if (threadKind != GCThreadKind.Foreground) throw new Exception("Must be foreground");

                    // Go with the *current* state.
                    bool lastHasCurrent = oldLastState.Value.HasJoinStageCur(joinStage);
                    bool bgcHasCurrent = oldBgcState.Value.HasJoinStageCur(joinStage);
                    if (lastHasCurrent == bgcHasCurrent)
                    {
                        // Prefer a non-complete GC.
                        if (last.IsComplete && bgc.IsComplete)
                        {
                            switch (threadKind)
                            {
                                case GCThreadKind.Foreground:
                                    // If this is a foreground thread, presume it's for a foreground gc.
                                    // (Background GCs do have foreground threads though)
                                    return retLast();
                                case GCThreadKind.Background:
                                    return retBgc();
                                default:
                                    throw new Exception();
                            }
                        }
                        else if (last.IsComplete)
                        {
                            return retBgc();
                        }
                        else if (bgc.IsComplete)
                        {
                            return retLast();
                        }
                        else
                        {
                            // Neither is complete and they have the same prev state.
                            // Since we asserted that this is GCThreadKind.Foreground,
                            // Assume that the bgc is paused while 'last' does work.
                            return retLast();
                        }
                    }
                    else if (lastHasCurrent)
                    {
                        return retLast();
                    }
                    else if (bgcHasCurrent)
                    {
                        return retBgc();
                    }
                    else
                    {
                        throw new Exception(); // we handled all cases
                    }
                }
            }
            else if (lastHas)
            {
                return retLast();
            }
            else if (bgcHas)
            {
                return retBgc();
            }
            else
            {
                if (TraceLoadedDotNetRuntime.DEBUG_PRINT_GC)
                {
                    Console.WriteLine("Ignoring join end");
                }
                return null;
            }
        }


        private static GCAndJoinStage? HandleRestartStart(
            Dictionary<TraceGC, GCJoinStateFgOrBg> joinStates,
            GCJoinStateFgOrBg? oldLastState,
            GCJoinStateFgOrBg? oldBgcState,
            TraceGC last,
            TraceGC bgc,
            ThreadID threadID)
        {
            if (oldLastState != null && oldLastState.Value.IsExpectingRestartFromThread(threadID))
            {
                joinStates[last] = oldLastState.Value.WithSeenRestartStart(threadID);
                return new GCAndJoinStage(last, oldLastState.Value.JoinStageForRestartingThread(threadID));
            }
            else if (oldBgcState != null && oldBgcState.Value.IsExpectingRestartFromThread(threadID))
            {
                joinStates[bgc] = oldBgcState.Value.WithSeenRestartStart(threadID);
                return new GCAndJoinStage(bgc, oldBgcState.Value.JoinStageForRestartingThread(threadID));
            }
            else
            {
                if (TraceLoadedDotNetRuntime.DEBUG_PRINT_GC)
                {
                    Console.WriteLine("IGNORING RESTART START");
                }
                return null;
            }
        }

        private static GCAndJoinStage? HandleRestartEnd(
            Dictionary<TraceGC, GCJoinStateFgOrBg> joinStates,
            GCJoinStateFgOrBg? oldLastState,
            GCJoinStateFgOrBg? oldBgcState,
            TraceGC last,
            TraceGC bgc,
            ThreadID threadID)
        {
            if (oldLastState != null && oldLastState.Value.IsExpectingRestartFromThread(threadID))
            {
                joinStates[last] = oldLastState.Value.WithSeenRestartEnd(threadID);
                return new GCAndJoinStage(last, oldLastState.Value.JoinStageForRestartingThread(threadID));
            }
            else if (oldBgcState != null && oldBgcState.Value.IsExpectingRestartFromThread(threadID))
            {
                joinStates[bgc] = oldBgcState.Value.WithSeenRestartEnd(threadID);
                return new GCAndJoinStage(bgc, oldBgcState.Value.JoinStageForRestartingThread(threadID));
            }
            else
            {
                if (TraceLoadedDotNetRuntime.DEBUG_PRINT_GC)
                {
                    Console.WriteLine("IGNORING RESTART END");
                }
                return null;
            }
        }

        internal List<TraceGC> m_gcs = new List<TraceGC>();
        private GCStats[] m_generations = new GCStats[3];
        internal GCStats m_stats = new GCStats();
        private int m_prvcount = 0;
        private int m_prvCompleted = 0;
        internal double NextRelativeTimeStampMsec;

        private void Calculate()
        {
            bool recalc = false;

            // determine if the stats need to be recalculated
            if (m_gcs.Count != m_prvcount)
            {
                recalc = true;
            }
            else
            {
                int complete = m_gcs.Sum(gc => (gc.IsComplete) ? 1 : 0);
                if (m_prvCompleted < complete)
                {
                    recalc = true;
                }

                int gencount = m_generations.Sum(gen => (gen != null) ? gen.Count : 0);
                if (gencount < m_prvCompleted)
                {
                    recalc = true;
                }

                m_prvCompleted = complete;
            }

            // calculate and cache the results - only when there are new GCs to process
            if (recalc)
            {
                // clear
                m_stats.Count = 0;
                m_stats.NumInduced = 0;
                m_stats.PinnedObjectSizes = 0;
                m_stats.NumWithPinEvents = 0;
                m_stats.PinnedObjectPercentage = 0;
                m_stats.NumWithPinPlugEvents = 0;
                m_stats.TotalSizeAfterMB = 0;
                m_stats.TotalPromotedMB = 0;
                m_stats.TotalSizePeakMB = 0;
                m_stats.TotalPauseTimeMSec = 0;
                m_stats.TotalAllocatedMB = 0;
                m_stats.MaxPauseDurationMSec = 0;
                m_stats.MaxSizePeakMB = 0;
                m_stats.MaxAllocRateMBSec = 0;
                m_stats.MaxSuspendDurationMSec = 0;

                // clear out the generation information
                for (int gen = 0; gen <= (int)Gens.Gen2; gen++)
                {
                    m_generations[gen] = new GCStats();
                }

                // calculate the stats
                for (int i = 0; i < m_gcs.Count; i++)
                {
                    TraceGC _gc = m_gcs[i];
                    if (!_gc.IsComplete)
                    {
                        continue;
                    }

                    // GC event details trickle in, and as they do the stats need to be regenerated
                    _gc.OnEnd(this);

                    _gc.Index = i;
                    if (_gc.PerHeapHistories != null && _gc.PerHeapHistories.Count > 0)  //per heap histories is not null
                    {
                        m_stats.HasDetailedGCInfo = true;
                    }

                    // Update the per-generation information 
                    m_generations[_gc.Generation].Count++;
                    bool isInduced = ((_gc.Reason == GCReason.Induced) || (_gc.Reason == GCReason.InducedNotForced));
                    if (isInduced)
                    {
                        (m_generations[_gc.Generation].NumInduced)++;
                    }

                    long PinnedObjectSizes = _gc.GetPinnedObjectSizes();
                    if (PinnedObjectSizes != 0)
                    {
                        m_generations[_gc.Generation].PinnedObjectSizes += PinnedObjectSizes;
                        m_generations[_gc.Generation].NumWithPinEvents++;
                    }

                    int PinnedObjectPercentage = _gc.GetPinnedObjectPercentage();
                    if (PinnedObjectPercentage != -1)
                    {
                        m_generations[_gc.Generation].PinnedObjectPercentage += _gc.GetPinnedObjectPercentage();
                        m_generations[_gc.Generation].NumWithPinPlugEvents++;
                    }

                    m_generations[_gc.Generation].TotalCpuMSec += _gc.GetTotalGCTime();
                    m_generations[_gc.Generation].TotalSizeAfterMB += _gc.HeapSizeAfterMB;

                    m_generations[_gc.Generation].TotalSizePeakMB += _gc.HeapSizePeakMB;
                    m_generations[_gc.Generation].TotalPromotedMB += _gc.PromotedMB;
                    m_generations[_gc.Generation].TotalPauseTimeMSec += _gc.PauseDurationMSec;
                    m_generations[_gc.Generation].TotalAllocatedMB += _gc.AllocedSinceLastGCMB;
                    m_generations[_gc.Generation].MaxPauseDurationMSec = Math.Max(m_generations[_gc.Generation].MaxPauseDurationMSec, _gc.PauseDurationMSec);
                    m_generations[_gc.Generation].MaxSizePeakMB = Math.Max(m_generations[_gc.Generation].MaxSizePeakMB, _gc.HeapSizePeakMB);
                    m_generations[_gc.Generation].MaxAllocRateMBSec = Math.Max(m_generations[_gc.Generation].MaxAllocRateMBSec, _gc.AllocRateMBSec);
                    m_generations[_gc.Generation].MaxPauseDurationMSec = Math.Max(m_generations[_gc.Generation].MaxPauseDurationMSec, _gc.PauseDurationMSec);
                    m_generations[_gc.Generation].MaxSuspendDurationMSec = Math.Max(m_generations[_gc.Generation].MaxSuspendDurationMSec, _gc.SuspendDurationMSec);

                    // And the totals 
                    m_stats.Count++;
                    if (isInduced)
                    {
                        m_stats.NumInduced++;
                    }

                    if (PinnedObjectSizes != 0)
                    {
                        m_stats.PinnedObjectSizes += PinnedObjectSizes;
                        m_stats.NumWithPinEvents++;
                    }
                    if (PinnedObjectPercentage != -1)
                    {
                        m_stats.PinnedObjectPercentage += _gc.GetPinnedObjectPercentage();
                        m_stats.NumWithPinPlugEvents++;
                    }
                    m_stats.TotalSizeAfterMB += _gc.HeapSizeAfterMB;
                    m_stats.TotalPromotedMB += _gc.PromotedMB;
                    m_stats.TotalSizePeakMB += _gc.HeapSizePeakMB;
                    m_stats.TotalPauseTimeMSec += _gc.PauseDurationMSec;
                    m_stats.TotalAllocatedMB += _gc.AllocedSinceLastGCMB;
                    m_stats.MaxPauseDurationMSec = Math.Max(m_stats.MaxPauseDurationMSec, _gc.PauseDurationMSec);
                    m_stats.MaxSizePeakMB = Math.Max(m_stats.MaxSizePeakMB, _gc.HeapSizePeakMB);
                    m_stats.MaxAllocRateMBSec = Math.Max(m_stats.MaxAllocRateMBSec, _gc.AllocRateMBSec);
                    m_stats.MaxSuspendDurationMSec = Math.Max(m_stats.MaxSuspendDurationMSec, _gc.SuspendDurationMSec);
                }

                m_prvcount = m_gcs.Count;
            }
        }
        #endregion
    }

    /// <summary>
    /// Just-in-time compilation (JIT) specific details about this process
    /// </summary>
    public class TraceJitCompiler
    {
        /// <summary>
        /// Process view of JIT statistics
        /// </summary>
        public JITStats Stats() { return m_stats; }
        /// <summary>
        /// Process view of all methods jitted
        /// </summary>
        public List<TraceJittedMethod> Methods { get { return m_methods; } }

        #region private
        internal JITStats m_stats = new JITStats();
        internal List<TraceJittedMethod> m_methods = new List<TraceJittedMethod>();
        internal double NextRelativeTimeStampMsec;
        #endregion
    }

    #region internal classes 
    internal class CircularBuffer<T> : IEnumerable<T>
        where T : class
    {
        private int StartIndex, AfterEndIndex, Size;
        private T[] Items;
        public CircularBuffer(int size)
        {
            if (size < 1)
            {
                throw new ArgumentException("size");
            }

            StartIndex = 0;
            AfterEndIndex = 0;
            Size = size + 1;
            Items = new T[Size];
        }

        public void Add(T item)
        {
            if (Next(AfterEndIndex) == StartIndex)
            {
                Items[StartIndex] = null;
                StartIndex = Next(StartIndex);
            }
            Items[AfterEndIndex] = item;
            AfterEndIndex = Next(AfterEndIndex);
        }

        private int Next(int i)
        {
            return (i == Size - 1) ? 0 : i + 1;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = StartIndex; i != AfterEndIndex; i = Next(i))
            {
                yield return Items[i];
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
    #endregion // internal classes
}

namespace Microsoft.Diagnostics.Tracing.Analysis.GC
{
    /// <summary>
    /// 
    /// </summary>
    public class TraceGC
    {
        public enum BGCRevisitState
        {
            Concurrent = 0,
            NonConcurrent = 1,
            MaxState = 2,
        }

        public enum HeapType
        {
            SOH = 0,
            LOH = 1,
            MaxType = 2,
        }

        public struct BGCRevisitInfo
        {
            public long PagesRevisited;
            public long ObjectsRevisited;
        }

        public TraceGC(int heapCount)
        {
            HeapCount = heapCount;

            if (HeapCount > 1)
            {
                GCCpuServerGCThreads = new float[HeapCount];
            }

            pinnedObjectSizes = -1;
            TotalPinnedPlugSize = -1;
            TotalUserPinnedPlugSize = -1;
            duplicatedPinningReports = 0;
        }

        /// <summary>
        /// Primary GC information
        /// </summary>
        public int Number;                      // Set in GCStart (starts at 1, unique for process)

        internal bool SeenStartEvent => Number != 0;

        /// <summary>
        /// Type of the GC, eg. NonConcurrent, Background or Foreground
        /// </summary>
        public GCType Type;                     // Set in GCStart
        /// <summary>
        /// Reason for the GC, eg. exhausted small heap, etc.
        /// </summary>
        public GCReason Reason;                 // Set in GCStart
        /// <summary>
        /// Generation of the heap collected.  If you compare Generation at the start and stop GC events they may differ.
        /// </summary>
        public int Generation;                  // Set in GCStop(Generation 0, 1 or 2)
        /// <summary>
        /// Time relative to the start of the trace.  Useful for ordering
        /// </summary>
        public double StartRelativeMSec;           //  Set in Start, does not include suspension.  
        /// <summary>
        /// Duration of the GC, excluding the suspension time
        /// </summary>
        public double DurationMSec;             // Set in Stop This is JUST the GC time (not including suspension) That is Stop-Start.  
        /// <summary>
        /// Duration the EE suspended the process
        /// </summary>
        public double PauseDurationMSec;       // Total time EE is suspended (can be less than GC time for background)
        /// <summary>
        /// Time the EE took to suspend all the threads
        /// </summary>
        public double SuspendDurationMSec;      // Time it takes to do the suspension
        /// <summary>
        /// Percentage time the GC took compared to the process lifetime
        /// </summary>
        public double PercentTimeInGC { get { return (float)(GetTotalGCTime() * 100 / ProcessCpuMSec); } }          // Of all the CPU, how much as a percentage is spent in the GC since end of last GC.
        /// <summary>
        /// The number of CPU samples gathered for the lifetime of this process
        /// </summary>
        public double ProcessCpuMSec;               // The amount of CPU time the process consumed since the last GC.
        /// <summary>
        /// The number of CPU samples gathered during a GC
        /// </summary>
        public double GCCpuMSec;                 // The amount of CPU time this GC consumed.
        /// <summary>
        /// Mark time information per heap.  Key is the heap number
        /// </summary>
        public Dictionary<int /*heap number*/, MarkInfo > PerHeapMarkTimes;      // The dictionary of heap number and info on time it takes to mark various roots.
        internal bool fMarkTimesConverted;
        /// <summary>
        /// Time since the last EE restart
        /// </summary>
        public double DurationSinceLastRestartMSec;  //  Set in GCStart
        /// <summary>
        /// Relative time to the trace of when the GC pause began
        /// </summary>
        public double PauseStartRelativeMSec;        //  Set in GCStart (but usually determined by GCSuspendEEStart)
        /// <summary>
        /// Relative time to the trace of the last time when the GC pause ended.
        /// (If there are multiple pauses this will be the last one.)
        /// </summary>
        public double PauseEndRelativeMSec; // Set in GCRestartEEStop
        /// <summary>
        /// Marks if the GC is in a completed state
        /// </summary>
        public bool IsComplete;
        // 
        // The 2 fields below would only make sense if the type is BackgroundGC.
        // 
        public BGCPhase BGCCurrentPhase;
        public BGCRevisitInfo[][] BGCRevisitInfoArr;
        public double BGCFinalPauseMSec;

        public void EnsureBGCRevisitInfoAlloc()
        {
            if (BGCRevisitInfoArr == null)
            {
                BGCRevisitInfoArr = new TraceGC.BGCRevisitInfo[(int)TraceGC.BGCRevisitState.MaxState][];
                for (int i = 0; i < (int)TraceGC.BGCRevisitState.MaxState; i++)
                {
                    BGCRevisitInfoArr[i] = new TraceGC.BGCRevisitInfo[(int)TraceGC.HeapType.MaxType];
                }
            }
        }

        /// <summary>
        /// Server GC histories
        /// </summary>
        //list of workload histories per server GC heap
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public List<ServerGcHistory> ServerGcHeapHistories = new List<ServerGcHistory>();
        /// <summary>
        /// Amount of memory allocated since last GC.  Requires GCAllocationTicks enabled.  The 
        /// data is split into small and large heaps
        /// </summary>
        public double[] AllocedSinceLastGCBasedOnAllocTickMB = { 0.0, 0.0 };// Set in HeapStats
        /// <summary>
        /// Number of heaps.  -1 is the default
        /// </summary>
        public int HeapCount { get; private set; } = -1;
        /// <summary>
        /// Calculate the size of all pinned objects
        /// </summary>
        /// <returns></returns>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public long GetPinnedObjectSizes()
        {
            if (pinnedObjectSizes == -1)
            {
                pinnedObjectSizes = 0;
                if (PinnedObjects != null)
                {
                    foreach (KeyValuePair<ulong, long> item in PinnedObjects)
                    {
                        pinnedObjectSizes += item.Value;
                    }
                }
            }
            return pinnedObjectSizes;
        }
        /// <summary>
        /// Percentage of the pinned objects created by the user
        /// </summary>
        /// <returns></returns>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public int GetPinnedObjectPercentage()
        {
            if (TotalPinnedPlugSize == -1)
            {
                TotalPinnedPlugSize = 0;
                TotalUserPinnedPlugSize = 0;

                if (PinnedObjects != null && PinnedPlugs != null)
                {
                    foreach (KeyValuePair<ulong, long> item in PinnedObjects)
                    {
                        ulong Address = item.Key;

                        for (int i = 0; i < PinnedPlugs.Count; i++)
                        {
                            if ((Address >= PinnedPlugs[i].Start) && (Address < PinnedPlugs[i].End))
                            {
                                PinnedPlugs[i].PinnedByUser = true;
                                break;
                            }
                        }
                    }
                }

                if (PinnedPlugs != null)
                {
                    for (int i = 0; i < PinnedPlugs.Count; i++)
                    {
                        long Size = (long)(PinnedPlugs[i].End - PinnedPlugs[i].Start);
                        TotalPinnedPlugSize += Size;
                        if (PinnedPlugs[i].PinnedByUser)
                        {
                            TotalUserPinnedPlugSize += Size;
                        }
                    }
                }
            }

            return ((TotalPinnedPlugSize == 0) ? -1 : (int)((double)pinnedObjectSizes * 100 / (double)TotalPinnedPlugSize));
        }
        /// <summary>
        /// Total time taken by the GC
        /// </summary>
        /// <returns></returns>
        public double GetTotalGCTime()
        {
            if (_TotalGCTimeMSec < 0)
            {
                _TotalGCTimeMSec = 0;
                if (GCCpuServerGCThreads != null)
                {
                    for (int i = 0; i < GCCpuServerGCThreads.Length; i++)
                    {
                        _TotalGCTimeMSec += GCCpuServerGCThreads[i];
                    }
                }
                _TotalGCTimeMSec += GCCpuMSec;
            }

            Debug.Assert(_TotalGCTimeMSec >= 0);
            return _TotalGCTimeMSec;
        }
        /// <summary>
        /// Friendly GC name including type, reason and generation
        /// </summary>
        public object GCGenerationName
        {
            get
            {
                string typeSuffix = "";
                if (Type == GCType.NonConcurrentGC)
                {
                    typeSuffix = "N";
                }
                else if (Type == GCType.BackgroundGC)
                {
                    typeSuffix = "B";
                }
                else if (Type == GCType.ForegroundGC)
                {
                    typeSuffix = "F";
                }

                string inducedSuffix = "";
                if (Reason == GCReason.Induced)
                {
                    inducedSuffix = "I";
                }

                if (Reason == GCReason.InducedNotForced)
                {
                    inducedSuffix = "i";
                }

                return Generation.ToString() + typeSuffix + inducedSuffix;
            }
        }
        /// <summary>
        /// Heap size after GC (mb)
        /// </summary>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public double HeapSizeAfterMB
        {
            get
            {
                if (null != HeapStats)
                {
                    return (HeapStats.GenerationSize0 + HeapStats.GenerationSize1 + HeapStats.GenerationSize2 + HeapStats.GenerationSize3) / 1000000.0;
                }
                else
                {
                    return -1.0;
                }
            }
        }
        /// <summary>
        /// Amount of memory promoted with GC (mb)
        /// </summary>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public double PromotedMB
        {
            get
            {
                if (null != HeapStats)
                {
                    return (HeapStats.TotalPromotedSize0 + HeapStats.TotalPromotedSize1 +
                       HeapStats.TotalPromotedSize2 + HeapStats.TotalPromotedSize3) / 1000000.0;
                }
                else
                {
                    return -1.0;
                }
            }
        }
        /// <summary>
        /// Memory survival percentage by generation
        /// </summary>
        /// <param name="gen"></param>
        /// <returns></returns>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public double SurvivalPercent(Gens gen)
        {
            double retSurvRate = double.NaN;

            long SurvRate = 0;

            if (gen == Gens.GenLargeObj)
            {
                if (Generation < 2)
                {
                    return retSurvRate;
                }
            }
            else if ((int)gen > Generation)
            {
                return retSurvRate;
            }

            if (PerHeapHistories != null && PerHeapHistories.Count > 0)
            {
                for (int i = 0; i < PerHeapHistories.Count; i++)
                {
                    SurvRate += PerHeapHistories[i].GenData[(int)gen].SurvRate;
                }

                SurvRate /= PerHeapHistories.Count;
            }

            retSurvRate = SurvRate;

            return retSurvRate;
        }
        /// <summary>
        /// Heap size by generation after GC (mb)
        /// </summary>
        /// <param name="gen"></param>
        /// <returns></returns>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public double GenSizeAfterMB(Gens gen)
        {
            if (gen == Gens.GenLargeObj)
            {
                return HeapStats.GenerationSize3 / 1000000.0;
            }

            if (gen == Gens.Gen2)
            {
                return HeapStats.GenerationSize2 / 1000000.0;
            }

            if (gen == Gens.Gen1)
            {
                return HeapStats.GenerationSize1 / 1000000.0;
            }

            if (gen == Gens.Gen0)
            {
                return HeapStats.GenerationSize0 / 1000000.0;
            }

            Debug.Assert(false);
            return double.NaN;
        }
        /// <summary>
        /// Heap fragmentation by generation (mb)
        /// </summary>
        /// <param name="gen"></param>
        /// <returns></returns>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public double GenFragmentationMB(Gens gen)
        {
            if (PerHeapHistories == null)
            {
                return double.NaN;
            }

            double ret = 0.0;
            for (int HeapIndex = 0; HeapIndex < PerHeapHistories.Count; HeapIndex++)
            {
                ret += PerHeapHistories[HeapIndex].GenData[(int)gen].Fragmentation / 1000000.0;
            }

            return ret;
        }
        /// <summary>
        /// Percentage of heap fragmented by generation 
        /// </summary>
        /// <param name="gen"></param>
        /// <returns></returns>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public double GenFragmentationPercent(Gens gen)
        {
            return (GenFragmentationMB(gen) * 100.0 / GenSizeAfterMB(gen));
        }
        /// <summary>
        /// Amount of memory at the start of the GC by generation (mb)
        /// </summary>
        /// <param name="gen"></param>
        /// <returns></returns>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public double GenInMB(Gens gen)
        {
            if (PerHeapHistories == null)
            {
                return double.NaN;
            }

            double ret = 0.0;
            for (int HeapIndex = 0; HeapIndex < PerHeapHistories.Count; HeapIndex++)
            {
                ret += PerHeapHistories[HeapIndex].GenData[(int)gen].In / 1000000.0;
            }

            return ret;
        }
        /// <summary>
        /// Amount of memory after the gc by generation (mb)
        /// </summary>
        /// <param name="gen"></param>
        /// <returns></returns>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public double GenOutMB(Gens gen)
        {
            if (PerHeapHistories == null)
            {
                return double.NaN;
            }

            double ret = 0.0;
            for (int HeapIndex = 0; HeapIndex < PerHeapHistories.Count; HeapIndex++)
            {
                ret += PerHeapHistories[HeapIndex].GenData[(int)gen].Out / 1000000.0;
            }

            return ret;
        }
        /// <summary>
        /// Memory promoted by generation (mb)
        /// Note that in 4.0 TotalPromotedSize is not entirely accurate (since it doesn't
        /// count the pins that got demoted. We could consider using the PerHeap event data
        /// to compute the accurate promoted size. 
        /// In 4.5 this is accurate.
        /// </summary>
        /// <param name="gen"></param>
        /// <returns></returns>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public double GenPromotedMB(Gens gen)
        {
            if (gen == Gens.GenLargeObj)
            {
                return HeapStats.TotalPromotedSize3 / 1000000.0;
            }

            if (gen == Gens.Gen2)
            {
                return HeapStats.TotalPromotedSize2 / 1000000.0;
            }

            if (gen == Gens.Gen1)
            {
                return HeapStats.TotalPromotedSize1 / 1000000.0;
            }

            if (gen == Gens.Gen0)
            {
                return HeapStats.TotalPromotedSize0 / 1000000.0;
            }

            Debug.Assert(false);
            return double.NaN;
        }
        /// <summary>
        /// Heap budget by generation (mb)
        /// </summary>
        /// <param name="gen"></param>
        /// <returns></returns>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public double GenBudgetMB(Gens gen)
        {
            if (PerHeapHistories == null)
            {
                return double.NaN;
            }

            double budget = 0.0;
            for (int HeapIndex = 0; HeapIndex < PerHeapHistories.Count; HeapIndex++)
            {
                budget += PerHeapHistories[HeapIndex].GenData[(int)gen].Budget / 1000000.0;
            }

            return budget;
        }
        /// <summary>
        /// Object size by generation after GC (mb)
        /// </summary>
        /// <param name="gen"></param>
        /// <returns></returns>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public double GenObjSizeAfterMB(Gens gen)
        {
            if (PerHeapHistories == null)
            {
                return double.NaN;
            }

            double objSizeAfter = 0.0;
            for (int HeapIndex = 0; HeapIndex < PerHeapHistories.Count; HeapIndex++)
            {
                objSizeAfter += PerHeapHistories[HeapIndex].GenData[(int)gen].ObjSizeAfter / 1000000.0;
            }

            return objSizeAfter;
        }
        /// <summary>
        /// Heap condemned reasons by GC
        /// </summary>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public GCCondemnedReasons[] PerHeapCondemnedReasons
        {
            get
            {
                if ((PerHeapHistories != null) && (_PerHeapCondemnedReasons == null))
                {
                    int NumHeaps = PerHeapHistories.Count;
                    _PerHeapCondemnedReasons = new GCCondemnedReasons[NumHeaps];

                    for (int HeapIndex = 0; HeapIndex < NumHeaps; HeapIndex++)
                    {
                        _PerHeapCondemnedReasons[HeapIndex] = new GCCondemnedReasons();
                        _PerHeapCondemnedReasons[HeapIndex].EncodedReasons.Reasons = PerHeapHistories[HeapIndex].CondemnReasons0;
                        if (PerHeapHistories[HeapIndex].HasCondemnReasons1)
                        {
                            _PerHeapCondemnedReasons[HeapIndex].EncodedReasons.ReasonsEx = PerHeapHistories[HeapIndex].CondemnReasons1;
                        }
                        _PerHeapCondemnedReasons[HeapIndex].CondemnedReasonGroups = new byte[(int)CondemnedReasonGroup.Max];
                        _PerHeapCondemnedReasons[HeapIndex].Decode(PerHeapHistories[HeapIndex].Version);
                    }
                }

                return _PerHeapCondemnedReasons;
            }
        }
        /// <summary>
        /// Identify the first and greatest condemned heap
        /// </summary>
        /// <returns></returns>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public int FindFirstHighestCondemnedHeap()
        {
            int GenNumberHighest = (int)Generation;
            for (int HeapIndex = 0; HeapIndex < PerHeapCondemnedReasons.Length; HeapIndex++)
            {
                int gen = PerHeapCondemnedReasons[HeapIndex].CondemnedReasonGroups[(int)CondemnedReasonGroup.Final_Generation];
                if (gen == GenNumberHighest)
                {
                    return HeapIndex;
                }
            }

            return 0;
        }
        /// <summary>
        /// Indicates that the GC has low ephemeral space
        /// </summary>
        /// <returns></returns>
        public bool IsLowEphemeral()
        {
            return CondemnedReasonGroupSet(CondemnedReasonGroup.Low_Ephemeral);
        }
        /// <summary>
        /// Indicates that the GC was not compacting
        /// </summary>
        /// <returns></returns>
        public bool IsNotCompacting()
        {
            if (GlobalHeapHistory == null)
            {
                return true;
            }

            return ((GlobalHeapHistory.GlobalMechanisms & (GCGlobalMechanisms.Compaction)) != 0);
        }
        /// <summary>
        /// Returns the condemned reason for this heap
        /// </summary>
        /// <param name="ReasonsInfo"></param>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public void GetCondemnedReasons(Dictionary<CondemnedReasonGroup, int> ReasonsInfo)
        {
            // Older versions of the runtime does not have this event. So even for a complete GC, we may not have this
            // info.
            if (PerHeapCondemnedReasons == null || PerHeapCondemnedReasons.Length == 0)
            {
                return;
            }

            int HeapIndexHighestGen = 0;
            if (PerHeapCondemnedReasons.Length > 1)
            {
                HeapIndexHighestGen = FindFirstHighestCondemnedHeap();
            }

            byte[] ReasonGroups = PerHeapCondemnedReasons[HeapIndexHighestGen].CondemnedReasonGroups;

            // These 2 reasons indicate a gen number. If the number is the same as the condemned gen, we 
            // include this reason.
            for (int i = (int)CondemnedReasonGroup.Alloc_Exceeded; i <= (int)CondemnedReasonGroup.Time_Tuning; i++)
            {
                if (ReasonGroups[i] == Generation)
                {
                    AddCondemnedReason(ReasonsInfo, (CondemnedReasonGroup)i);
                }
            }

            if (ReasonGroups[(int)CondemnedReasonGroup.Induced] != 0)
            {
                if (ReasonGroups[(int)CondemnedReasonGroup.Initial_Generation] == Generation)
                {
                    AddCondemnedReason(ReasonsInfo, CondemnedReasonGroup.Induced);
                }
            }

            // The rest of the reasons are conditions so include the ones that are set.
            for (int i = (int)CondemnedReasonGroup.Low_Ephemeral; i < (int)CondemnedReasonGroup.Max; i++)
            {
                if (ReasonGroups[i] != 0)
                {
                    AddCondemnedReason(ReasonsInfo, (CondemnedReasonGroup)i);
                }
            }
        }
        /// <summary>
        /// Per heap statistics
        /// </summary>
        public List<GCPerHeapHistory> PerHeapHistories = new List<GCPerHeapHistory>();
        /// <summary>
        /// Sum of the pinned plug sizes
        /// </summary>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public long TotalPinnedPlugSize;
        /// <summary>
        /// Sum of the user created pinned plug sizes
        /// </summary>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public long TotalUserPinnedPlugSize;
        /// <summary>
        /// Per heap statstics
        /// </summary>
        public GCHeapStats HeapStats;
        /// <summary>
        /// Large object heap wait threads
        /// </summary>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public Dictionary<int, BGCAllocWaitInfo> LOHWaitThreads;
        /// <summary>
        /// Process heap statistics
        /// </summary>
        public GCGlobalHeapHistory GlobalHeapHistory;
        /// <summary>
        /// Free list efficiency statistics
        /// </summary>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public FreeListEfficiency FreeList;
        /// <summary>
        /// Memory allocated since last GC (mb)
        /// </summary>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public double AllocedSinceLastGCMB;
        /// <summary>
        /// Ratio of heap size before and after
        /// </summary>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public double RatioPeakAfter;
        /// <summary>
        /// Ratio of allocations since last GC over time executed
        /// </summary>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public double AllocRateMBSec;
        /// <summary>
        /// Peak heap size before GCs (mb)
        /// </summary>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public double HeapSizePeakMB;
        /// <summary>
        /// Per generation view of user allocated data
        /// </summary>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public double[] UserAllocated = new double[(int)Gens.Gen0After];
        /// <summary>
        /// Heap size before gc (mb)
        /// </summary>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public double HeapSizeBeforeMB;
        /// <summary>
        /// Per generation view of heap sizes before GC (mb)
        /// </summary>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public double[] GenSizeBeforeMB = new double[(int)Gens.Gen0After];
        /// <summary>
        /// This represents the percentage time spent paused for this GC since the last GC completed. 
        /// </summary>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public double PauseTimePercentageSinceLastGC;

        #region private
        internal void OnEnd(TraceGarbageCollector details)
        {
            IsComplete = true;

            // calculate core gc values
            pinnedObjectSizes = -1;
            TotalPinnedPlugSize = -1;
            GetPinnedObjectSizes();
            GetPinnedObjectPercentage();
            FreeList = GetFreeListEfficiency(details.GCs, this);
            AllocedSinceLastGCMB = GetAllocedSinceLastGCMB(details.GCs, this);
            RatioPeakAfter = GetRatioPeakAfter(details.GCs, this);
            AllocRateMBSec = GetAllocRateMBSec(details.GCs, this);
            HeapSizePeakMB = GetHeapSizePeakMB(details.GCs, this);
            UserAllocated[(int)Gens.Gen0] = GetUserAllocated(details.GCs, this, Gens.Gen0);
            UserAllocated[(int)Gens.GenLargeObj] = GetUserAllocated(details.GCs, this, Gens.GenLargeObj);
            HeapSizeBeforeMB = GetHeapSizeBeforeMB(details.GCs, this);
            for (int gen = (int)Gens.Gen0; gen <= (int)Gens.GenLargeObj; gen++)
            {
                GenSizeBeforeMB[gen] = GetGenSizeBeforeMB(details.GCs, this, (Gens)gen);
            }

            PauseTimePercentageSinceLastGC = GetPauseTimePercentageSinceLastGC(details.GCs, this);

            // calculate core gc process values
            details.m_stats.ProcessDuration = GetProcessDuration(details.m_stats, details.GCs, this);

            BlockingGCEnd();

            // clear out large internal data, after the data was used to calculate satistics
            if (PinnedObjects != null) { PinnedObjects.Clear(); PinnedObjects = null; }
            if (PinnedPlugs != null) { PinnedPlugs.Clear(); PinnedPlugs = null; }
        }

        internal static double GetProcessDuration(GCStats stats, List<TraceGC> GCs, TraceGC gc)
        {
            double startRelativeMSec = 0.0;

            for (int i = 0; i < GCs.Count; i++)
            {
                if (GCs[i].IsComplete)
                {
                    startRelativeMSec = GCs[i].PauseStartRelativeMSec;
                    break;
                }
            }

            if (startRelativeMSec == 0.0)
            {
                return 0;
            }

            // Get the end time of the last GC.
            double endRelativeMSec = stats.lastRestartEndTimeRelativeMSec;
            return (endRelativeMSec - startRelativeMSec);
        }

        internal static double GetPauseTimePercentageSinceLastGC(List<TraceGC> GCs, TraceGC gc)
        {
            double pauseTimePercentage;

            if (gc.Type == GCType.BackgroundGC)
            {
                // Find all GCs that occurred during the current background GC.
                double startTimeRelativeMSec = gc.StartRelativeMSec;
                double endTimeRelativeMSec = gc.StartRelativeMSec + gc.DurationMSec;

                // Calculate the pause time for this BGC.
                // Pause time is defined as pause time for the BGC + pause time for all FGCs that ran during the BGC.
                double totalPauseTime = gc.PauseDurationMSec;

                if (gc.Index + 1 < GCs.Count)
                {
                    TraceGC gcEvent;
                    for (int i = gc.Index + 1; i < GCs.Count; ++i)
                    {
                        gcEvent = GCs[i];
                        if ((gcEvent.StartRelativeMSec >= startTimeRelativeMSec) && (gcEvent.StartRelativeMSec < endTimeRelativeMSec))
                        {
                            totalPauseTime += gcEvent.PauseDurationMSec;
                        }
                        else
                        {
                            // We've finished processing all FGCs that occurred during this BGC.
                            break;
                        }
                    }
                }

                // Get the elapsed time since the previous GC finished.
                if (GCs[gc.Index] != gc)
                {
                    int actualIndex = GCs.IndexOf(gc);
                    Console.WriteLine($"gc.Index is {gc.Index}, but actual index is {actualIndex}");
                    string actualNumbers = string.Join(", ", GCs.Select(g => g.Number.ToString()));
                    Console.WriteLine($"gc number is {gc.Number}, gc numbers are {actualNumbers}");
                }
                Debug.Assert(GCs[gc.Index] == gc, "GC INDEX IS WRONG!");

                int previousGCIndex = gc.Index - 1;
                if (previousGCIndex >= 0 && !GCs[previousGCIndex].SeenStartEvent)
                {
                    previousGCIndex--;
                }
                double previousGCStopTimeRelativeMSec;
                if (previousGCIndex >= 0)
                {
                    TraceGC previousGCEvent = GCs[previousGCIndex];
                    previousGCStopTimeRelativeMSec = previousGCEvent.StartRelativeMSec + previousGCEvent.DurationMSec;
                }
                else
                {
                    // Backstop in case this is the first GC.
                    previousGCStopTimeRelativeMSec = GCs[0].StartRelativeMSec;
                }

                double totalTime = (gc.StartRelativeMSec + gc.DurationMSec) - previousGCStopTimeRelativeMSec;
                pauseTimePercentage = (totalPauseTime * 100) / (totalTime);
            }
            else
            {
                double totalTime = gc.PauseDurationMSec + gc.DurationSinceLastRestartMSec;
                pauseTimePercentage = (gc.PauseDurationMSec * 100) / (totalTime);
            }

            Debug.Assert(pauseTimePercentage <= 100);
            return pauseTimePercentage;
        }

        internal static FreeListEfficiency GetFreeListEfficiency(List<TraceGC> GCs, TraceGC gc)
        {
            Gens gen = Gens.Gen2;
            FreeListEfficiency freeList = new FreeListEfficiency();

            // I am not worried about gen0 or LOH's free list efficiency right now - it's 
            // calculated differently.
            if ((gc.PerHeapHistories == null) ||
                (gc.PerHeapHistories.Count == 0) ||
                (gen == Gens.Gen0) ||
                (gen == Gens.GenLargeObj) ||
                (gc.Index <= 0) ||
                !(gc.PerHeapHistories[0].VersionRecognized))
            {
                return freeList;
            }

            int YoungerGen = (int)gen - 1;

            if (gc.Generation != YoungerGen)
            {
                return freeList;
            }

            if (gc.PerHeapHistories[0].HasFreeListAllocated && gc.PerHeapHistories[0].HasFreeListRejected)
            {
                freeList.Allocated = 0;
                freeList.FreeListConsumed = 0;
                for (int HeapIndex = 0; HeapIndex < gc.PerHeapHistories.Count; HeapIndex++)
                {
                    GCPerHeapHistory hist = (GCPerHeapHistory)gc.PerHeapHistories[HeapIndex];
                    freeList.Allocated += hist.FreeListAllocated;
                    freeList.FreeListConsumed += hist.FreeListAllocated + hist.FreeListRejected;
                }
                freeList.Valid = true;
                return freeList;
            }

            // I am not using MB here because what's promoted from gen1 can easily be less than a MB.
            double YoungerGenOut = 0;
            double FreeListBefore = 0;
            double FreeListAfter = 0;
            // Includes fragmentation. This lets us know if we had to expand the size.
            double GenSizeBefore = 0;
            double GenSizeAfter = 0;
            for (int HeapIndex = 0; HeapIndex < gc.PerHeapHistories.Count; HeapIndex++)
            {
                YoungerGenOut += gc.PerHeapHistories[HeapIndex].GenData[YoungerGen].Out;
                GenSizeBefore += gc.PerHeapHistories[HeapIndex].GenData[(int)gen].SizeBefore;
                GenSizeAfter += gc.PerHeapHistories[HeapIndex].GenData[(int)gen].SizeAfter;
                // Occasionally I've seen a GC in the middle that simply missed some events,
                // some of which are PerHeap hist events so we don't have data.
                if (GCs[gc.Index - 1].PerHeapHistories == null || GCs[gc.Index - 1].PerHeapHistories.Count == 0)
                {
                    return freeList;
                }

                if (gc.PerHeapHistories[HeapIndex].GenData[(int)gen].HasFreeListSpaceAfter && gc.PerHeapHistories[HeapIndex].GenData[(int)gen].HasFreeListSpaceBefore)
                {
                    FreeListBefore += gc.PerHeapHistories[HeapIndex].GenData[(int)gen].FreeListSpaceBefore;
                    FreeListAfter += gc.PerHeapHistories[HeapIndex].GenData[(int)gen].FreeListSpaceAfter;
                }
                else
                {
                    FreeListBefore += GCs[gc.Index - 1].PerHeapHistories[HeapIndex].GenData[(int)gen].Fragmentation;
                    FreeListAfter += gc.PerHeapHistories[HeapIndex].GenData[(int)gen].Fragmentation;
                }
            }

            double GenSizeGrown = GenSizeAfter - GenSizeBefore;

            // This is the most accurate situation we can calculuate (if it's not accurate it means
            // we are over estimating which is ok.
            if ((GenSizeGrown == 0) && ((FreeListBefore > 0) && (FreeListAfter >= 0)))
            {
                freeList.Allocated = YoungerGenOut;
                freeList.FreeListConsumed = FreeListBefore - FreeListAfter;
                // We don't know how much of the survived is pinned so we are overestimating here.
                if (freeList.Allocated < freeList.FreeListConsumed)
                {
                    freeList.Valid = true;
                    return freeList;
                }
            }

            return freeList;
        }

        internal static double GetAllocedSinceLastGCMB(List<TraceGC> GCs, TraceGC gc)
        {
            return GetUserAllocated(GCs, gc, Gens.Gen0) + GetUserAllocated(GCs, gc, Gens.GenLargeObj);
        }

        internal static double GetRatioPeakAfter(List<TraceGC> GCs, TraceGC gc) { if (gc.HeapSizeAfterMB == 0) { return 0; } return GetHeapSizePeakMB(GCs, gc) / gc.HeapSizeAfterMB; }
        internal static double GetAllocRateMBSec(List<TraceGC> GCs, TraceGC gc) { return GetAllocedSinceLastGCMB(GCs, gc) * 1000.0 / gc.DurationSinceLastRestartMSec; }

        internal static double GetHeapSizePeakMB(List<TraceGC> GCs, TraceGC gc)
        {
            var ret = GetHeapSizeBeforeMB(GCs, gc);
            if (gc.Type == GCType.BackgroundGC)
            {
                var BgGcEndedRelativeMSec = gc.PauseStartRelativeMSec + gc.DurationMSec;
                for (int i = gc.Index + 1; i < GCs.Count; i++)
                {
                    var _event = GCs[i];
                    if (BgGcEndedRelativeMSec < _event.PauseStartRelativeMSec)
                    {
                        break;
                    }

                    ret = Math.Max(ret, GetHeapSizeBeforeMB(GCs, _event));
                }
            }
            return ret;
        }

        /// <summary>
        /// Get what's allocated into gen0 or gen3. For server GC this gets the total for 
        /// all heaps.
        /// </summary>
        internal static double GetUserAllocated(List<TraceGC> GCs, TraceGC gc, Gens gen)
        {
            Debug.Assert((gen == Gens.Gen0) || (gen == Gens.GenLargeObj));

            if ((gc.Type == GCType.BackgroundGC) && (gen == Gens.Gen0))
            {
                return gc.AllocedSinceLastGCBasedOnAllocTickMB[(int)gen];
            }

            if (gc.PerHeapHistories != null && gc.Index > 0 && GCs[gc.Index - 1].PerHeapHistories != null)
            {
                double TotalAllocated = 0;
                if (gc.Index > 0)
                {
                    for (int i = 0; i < gc.PerHeapHistories.Count; i++)
                    {
                        double Allocated = GetUserAllocatedPerHeap(GCs, gc, i, gen);

                        TotalAllocated += Allocated / 1000000.0;
                    }

                    return TotalAllocated;
                }
                else
                {
                    return GetGenSizeBeforeMB(GCs, gc, gen);
                }
            }

            return gc.AllocedSinceLastGCBasedOnAllocTickMB[(gen == Gens.Gen0) ? 0 : 1];
        }

        internal static double GetHeapSizeBeforeMB(List<TraceGC> GCs, TraceGC gc)
        {
            double ret = 0;
            for (Gens gen = Gens.Gen0; gen <= Gens.GenLargeObj; gen++)
            {
                ret += GetGenSizeBeforeMB(GCs, gc, gen);
            }

            return ret;
        }

        // Per generation stats.  
        internal static double GetGenSizeBeforeMB(List<TraceGC> GCs, TraceGC gc, Gens gen)
        {
            if (gc.PerHeapHistories != null && gc.PerHeapHistories.Count > 0)
            {
                double ret = 0.0;
                for (int HeapIndex = 0; HeapIndex < gc.PerHeapHistories.Count; HeapIndex++)
                {
                    ret += gc.PerHeapHistories[HeapIndex].GenData[(int)gen].SizeBefore / 1000000.0;
                }

                return ret;
            }

            // When we don't have perheap history we can only estimate for gen0 and gen3.
            double Gen0SizeBeforeMB = 0;
            if (gen == Gens.Gen0)
            {
                Gen0SizeBeforeMB = gc.AllocedSinceLastGCBasedOnAllocTickMB[0];
            }

            if (gc.Index == 0)
            {
                return ((gen == Gens.Gen0) ? Gen0SizeBeforeMB : 0);
            }

            // Find a previous HeapStats.  
            GCHeapStats heapStats = null;
            for (int j = gc.Index - 1; ; --j)
            {
                if (j == 0)
                {
                    return 0;
                }

                heapStats = GCs[j].HeapStats;
                if (heapStats != null)
                {
                    break;
                }
            }
            if (gen == Gens.Gen0)
            {
                return Math.Max((heapStats.GenerationSize0 / 1000000.0), Gen0SizeBeforeMB);
            }

            if (gen == Gens.Gen1)
            {
                return heapStats.GenerationSize1 / 1000000.0;
            }

            if (gen == Gens.Gen2)
            {
                return heapStats.GenerationSize2 / 1000000.0;
            }

            Debug.Assert(gen == Gens.GenLargeObj);

            if (gc.HeapStats != null)
            {
                return Math.Max(heapStats.GenerationSize3, gc.HeapStats.GenerationSize3) / 1000000.0;
            }
            else
            {
                return heapStats.GenerationSize3 / 1000000.0;
            }
        }

        /// <summary>
        /// For a given heap, get what's allocated into gen0 or gen3.
        /// We calculate this differently on 4.0, 4.5 Beta and 4.5 RC+.
        /// The caveat with 4.0 and 4.5 Beta is that when survival rate is 0,
        /// We don't know how to calculate the allocated - so we just use the
        /// last GC's budget (We should indicate this in the tool)
        /// </summary>
        private static double GetUserAllocatedPerHeap(List<TraceGC> GCs, TraceGC gc, int HeapIndex, Gens gen)
        {
            long prevObjSize = 0;
            if (gc.Index > 0)
            {
                // If the prevous GC has that heap get its size.  
                var perHeapGenData = GCs[gc.Index - 1].PerHeapHistories;
                if (HeapIndex < perHeapGenData.Count)
                {
                    prevObjSize = perHeapGenData[HeapIndex].GenData[(int)gen].ObjSizeAfter;
                    // Note that for gen3 we need to do something extra as its after data may not be updated if the last
                    // GC was a gen0 GC (A GC will update its size after data up to (Generation + 1) because that's all 
                    // it would change).
                    if ((gen == Gens.GenLargeObj) && (prevObjSize == 0) && (GCs[gc.Index - 1].Generation < (int)Gens.Gen1))
                    {
                        prevObjSize = perHeapGenData[HeapIndex].GenData[(int)gen].ObjSpaceBefore;
                    }
                }
            }
            GCPerHeapHistoryGenData currentGenData = gc.PerHeapHistories[HeapIndex].GenData[(int)gen];
            double Allocated;

            if (currentGenData.HasObjSpaceBefore)
            {
                Allocated = currentGenData.ObjSpaceBefore - prevObjSize;
            }
            else
            {
                long survRate = currentGenData.SurvRate;

                if (survRate == 0)
                {
                    Allocated = EstimateAllocSurv0(GCs, gc, HeapIndex, gen);
                }
                else
                {
                    long currentObjSize = currentGenData.ObjSizeAfter;
                    Allocated = (currentGenData.Out + currentObjSize) * 100 / survRate - prevObjSize;
                }
            }

            return Allocated;
        }

        // When survival rate is 0, for certain releases (see comments for GetUserAllocatedPerHeap)
        // we need to estimate.
        private static double EstimateAllocSurv0(List<TraceGC> GCs, TraceGC gc, int HeapIndex, Gens gen)
        {
            if (gc.HasAllocTickEvents)
            {
                return gc.AllocedSinceLastGCBasedOnAllocTickMB[(gen == Gens.Gen0) ? 0 : 1];
            }
            else
            {
                if (gc.Index > 0)
                {
                    // If the prevous GC has that heap get its size.  
                    var perHeapGenData = GCs[gc.Index - 1].PerHeapHistories;
                    if (HeapIndex < perHeapGenData.Count)
                    {
                        return perHeapGenData[HeapIndex].GenData[(int)gen].Budget;
                    }
                }
                return 0;
            }
        }

        internal bool IsConcurrentGC;
        internal Dictionary<ulong /*objectid*/, long> PinnedObjects;   // list of Pinned objects
        internal int Index;                       // Index into the list of GC events

        /// <summary>
        /// Legacy properties that need to be refactored and removed
        /// </summary>

        internal bool is20Event;


        // For background GC we need to remember when the GC before it ended because
        // when we get the GCStop event some foreground GCs may have happened.
        internal double ProcessCpuAtLastGC;

        internal bool HasAllocTickEvents = false;

        internal void SetHeapCount(int count)
        {
            if (HeapCount == -1)
            {
                HeapCount = count;
            }
        }


        internal void AddServerGCThreadTime(int heapIndex, float cpuMSec)
        {
            if (GCCpuServerGCThreads != null)
            {
                if (heapIndex >= GCCpuServerGCThreads.Length)
                {
                    var old = GCCpuServerGCThreads;
                    GCCpuServerGCThreads = new float[heapIndex + 1];
                    Array.Copy(old, GCCpuServerGCThreads, old.Length);
                }
                GCCpuServerGCThreads[heapIndex] += cpuMSec;
            }
        }

        private void CheckThreadKind(GCThreadKind threadKind)
        {
            if (SeenStartEvent && Type != GCType.BackgroundGC && threadKind == GCThreadKind.Background)
            {
                throw new Exception($"Thread kind doesn't make sense. Got {threadKind} but this is a {Type}");
            }
        }

        internal void AddServerGcThreadSwitch(ThreadWorkSpan cswitch, GCHeapAndThreadKindAndIsNewThread heapAndThreadKindAndIsNewThread)
        {
            CheckThreadKind(heapAndThreadKindAndIsNewThread.ThreadKind);
            ServerGcHeapHistories[(int) heapAndThreadKindAndIsNewThread.HeapID].AddSwitchEvent(cswitch, PauseStartRelativeMSec, threadKind: heapAndThreadKindAndIsNewThread.ThreadKind, newThreadIsGC: heapAndThreadKindAndIsNewThread.NewThreadIsGC);
        }

        internal void AddServerGcSample(ThreadWorkSpan sample, GCHeapAndThreadKindAndIsNewThread heapAndThreadKind)
        {
            CheckThreadKind(heapAndThreadKind.ThreadKind);
            ServerGcHeapHistories[(int) heapAndThreadKind.HeapID].AddSampleEvent(sample, PauseStartRelativeMSec, threadKind: heapAndThreadKind.ThreadKind, newThreadIsGC: heapAndThreadKind.NewThreadIsGC);
        }

        internal void AddGcJoin(GCJoinTraceData data, bool isEESuspended, GCThreadKind threadKind)
        {
            GCHeapAndThreadKind heapAndThreadKind = new GCHeapAndThreadKind(data.Heap, threadKind);

            bool isRestart = data.JoinType == GcJoinType.Restart;
            // Not true, there may be that many heaps!
            // Debug.Assert(isRestart == (data.Heap == 100 || data.Heap == 200));
            if (data.Heap >= 0 && data.Heap < ServerGcHeapHistories.Count)
            {
                ServerGcHeapHistories[data.Heap].AddJoin(data, PauseStartRelativeMSec, isEESuspended: isEESuspended, heapAndThreadKind: heapAndThreadKind);
            }
            else if (isRestart)
            {
                // restart
                foreach (var heap in ServerGcHeapHistories)
                {
                    heap.AddJoin(data, PauseStartRelativeMSec, isEESuspended: isEESuspended, heapAndThreadKind: heapAndThreadKind);
                }
            }
            else
            {
                // TODO: Count really should never be 0 here
                if (ServerGcHeapHistories.Count != 0)
                {
                    throw new Exception($"Got a join for heap {data.Heap}, but there are only {ServerGcHeapHistories.Count} heap histories");
                }
            }
        }

        internal void BlockingGCEnd()
        {
            ConvertMarkTimes();
            foreach (var serverHeap in ServerGcHeapHistories)
            {
                serverHeap.GCEnd(PauseDurationMSec);
            }
        }

        // We recorded these as the timestamps when we saw the mark events, now convert them 
        // to the actual time that it took for each mark.
        private void ConvertMarkTimes()
        {
            if (fMarkTimesConverted)
            {
                return;
            }

            if (PerHeapMarkTimes != null)
            {
                foreach (KeyValuePair<int, MarkInfo> item in PerHeapMarkTimes)
                {
                    if (item.Value.MarkTimes[(int)MarkRootType.MarkSizedRef] == 0.0)
                    {
                        item.Value.MarkTimes[(int)MarkRootType.MarkSizedRef] = StartRelativeMSec;
                    }

                    if (item.Value.MarkTimes[(int)MarkRootType.MarkOverflow] > StartRelativeMSec)
                    {
                        if (item.Value.MarkTimes[(int)MarkRootType.MarkOlder] == 0.0)
                        {
                            item.Value.MarkTimes[(int)MarkRootType.MarkOverflow] -= item.Value.MarkTimes[(int)MarkRootType.MarkOlder];
                        }
                        else
                        {
                            item.Value.MarkTimes[(int)MarkRootType.MarkOverflow] -= item.Value.MarkTimes[(int)MarkRootType.MarkHandles];
                        }
                    }

                    if (Generation == 2)
                    {
                        item.Value.MarkTimes[(int)MarkRootType.MarkOlder] = 0;
                    }
                    else
                    {
                        item.Value.MarkTimes[(int)MarkRootType.MarkOlder] -= item.Value.MarkTimes[(int)MarkRootType.MarkHandles];
                    }

                    item.Value.MarkTimes[(int)MarkRootType.MarkHandles] -= item.Value.MarkTimes[(int)MarkRootType.MarkFQ];
                    item.Value.MarkTimes[(int)MarkRootType.MarkFQ] -= item.Value.MarkTimes[(int)MarkRootType.MarkStack];
                    item.Value.MarkTimes[(int)MarkRootType.MarkStack] -= item.Value.MarkTimes[(int)MarkRootType.MarkSizedRef];
                    item.Value.MarkTimes[(int)MarkRootType.MarkSizedRef] -= StartRelativeMSec;
                }
            }
            fMarkTimesConverted = true;
        }

        // For true/false groups, return whether that group is set.
        private bool CondemnedReasonGroupSet(CondemnedReasonGroup Group)
        {
            if (PerHeapCondemnedReasons == null)
            {
                return false;
            }

            int HeapIndexHighestGen = 0;
            if (PerHeapCondemnedReasons.Length != 1)
            {
                HeapIndexHighestGen = FindFirstHighestCondemnedHeap();
            }

            return (PerHeapCondemnedReasons[HeapIndexHighestGen].CondemnedReasonGroups[(int)Group] != 0);
        }

        private void AddCondemnedReason(Dictionary<CondemnedReasonGroup, int> ReasonsInfo, CondemnedReasonGroup Reason)
        {
            if (!ReasonsInfo.ContainsKey(Reason))
            {
                ReasonsInfo.Add(Reason, 1);
            }
            else
            {
                (ReasonsInfo[Reason])++;
            }
        }


        internal void AddLOHWaitThreadInfo(int TID, double time, int reason, bool IsStart)
        {
            BGCAllocWaitReason ReasonLOHAlloc = (BGCAllocWaitReason)reason;

            if ((ReasonLOHAlloc == BGCAllocWaitReason.GetLOHSeg) ||
                (ReasonLOHAlloc == BGCAllocWaitReason.AllocDuringSweep) ||
                (ReasonLOHAlloc == BGCAllocWaitReason.AllocDuringBGC))
            {
                if (LOHWaitThreads == null)
                {
                    LOHWaitThreads = new Dictionary<int, BGCAllocWaitInfo>();
                }

                BGCAllocWaitInfo info;

                if (LOHWaitThreads.TryGetValue(TID, out info))
                {
                    if (IsStart)
                    {
                        // If we are finding the value it means we are hitting the small
                        // window where BGC sweep finished and BGC itself finished, discard
                        // this.
                    }
                    else
                    {
                        Debug.Assert(info.Reason == ReasonLOHAlloc);
                        info.WaitStopRelativeMSec = time;
                    }
                }
                else
                {
                    info = new BGCAllocWaitInfo();
                    if (IsStart)
                    {
                        info.Reason = ReasonLOHAlloc;
                        info.WaitStartRelativeMSec = time;
                    }
                    else
                    {
                        // We are currently not displaying this because it's incomplete but I am still adding 
                        // it so we could display if we want to.
                        info.WaitStopRelativeMSec = time;
                    }

                    LOHWaitThreads.Add(TID, info);
                }
            }
        }

        internal class PinnedPlug
        {
            public ulong Start;
            public ulong End;
            public bool PinnedByUser;

            public PinnedPlug(ulong s, ulong e)
            {
                Start = s;
                End = e;
                PinnedByUser = false;
            }
        };

        internal List<PinnedPlug> PinnedPlugs;
        private long pinnedObjectSizes;
        internal long duplicatedPinningReports;

        // The dictionary of heap number and info on time it takes to mark various roots.
        private GCCondemnedReasons[] _PerHeapCondemnedReasons;

        private double _TotalGCTimeMSec = -1;
        // When we are using Server GC we store the CPU spent on each thread
        // so we can see if there's an imbalance. We concurrently don't do this
        // for server background GC as the imbalance there is much less important.

        private float[] GCCpuServerGCThreads = null;

        #endregion
    }

    /// <summary>
    /// Condemned reasons are organized into the following groups.
    /// Each group corresponds to one or more reasons. 
    /// Groups are organized in the way that they mean something to users. 
    /// </summary>
    [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
    public enum CondemnedReasonGroup
    {
        // The first 4 will have values of a number which is the generation.
        // Note that right now these 4 have the exact same value as what's in
        // Condemned_Reason_Generation.
        Initial_Generation = 0,
        Final_Generation = 1,
        Alloc_Exceeded = 2,
        Time_Tuning = 3,

        // The following are either true(1) or false(0). They are not 
        // a 1:1 mapping from 
        Induced = 4,
        Low_Ephemeral = 5,
        Expand_Heap = 6,
        Fragmented_Ephemeral = 7,
        Fragmented_Gen1_To_Gen2 = 8,
        Fragmented_Gen2 = 9,
        Fragmented_Gen2_High_Mem = 10,
        GC_Before_OOM = 11,
        Too_Small_For_BGC = 12,
        Ephemeral_Before_BGC = 13,
        Internal_Tuning = 14,
        Max = 15,
    }

    /// <summary>
    /// Background GC allocation information
    /// </summary>
    [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
    public class BGCAllocWaitInfo
    {
        public double WaitStartRelativeMSec;
        public double WaitStopRelativeMSec;
        public BGCAllocWaitReason Reason;

        public bool GetWaitTime(ref double pauseMSec)
        {
            if ((WaitStartRelativeMSec != 0) &&
                (WaitStopRelativeMSec != 0))
            {
                pauseMSec = WaitStopRelativeMSec - WaitStartRelativeMSec;
                return true;
            }
            return false;
        }

        public bool IsLOHWaitLong(double pauseMSecMin)
        {
            double pauseMSec = 0;
            if (GetWaitTime(ref pauseMSec))
            {
                return (pauseMSec > pauseMSecMin);
            }
            return false;
        }

        public override string ToString()
        {
            if ((Reason == BGCAllocWaitReason.GetLOHSeg) ||
                (Reason == BGCAllocWaitReason.AllocDuringSweep))
            {
                return "Waiting for BGC to thread free lists";
            }
            else
            {
                Debug.Assert(Reason == BGCAllocWaitReason.AllocDuringBGC);
                return "Allocated too much during BGC, waiting for BGC to finish";
            }
        }
    }

    /// <summary>
    /// Span of thread work recorded by CSwitch or CPU Sample Profile events
    /// </summary>
    [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
    public class ThreadWorkSpan
    {
        public readonly ThreadID ThreadId;
        /// <summary>
        /// WARN: Not as reliable as ThreadId
        /// </summary>
        public readonly ProcessID ProcessId;
        /// <summary>
        /// WARN: Not as reliable as ThreadId
        /// </summary>
        public readonly string ProcessName;
        public readonly int ProcessorNumber;
        public readonly double AbsoluteTimestampMsc;
        public double DurationMsc;
        public int Priority = -1;
        public int WaitReason = -1;
        public readonly ThreadID? OldThreadId;

        public ThreadWorkSpan(CSwitchTraceData switchData)
        {
            ProcessName = switchData.NewProcessName;
            ThreadId = switchData.NewThreadID;
            ProcessId = switchData.NewProcessID;
            ProcessorNumber = switchData.ProcessorNumber;
            AbsoluteTimestampMsc = switchData.TimeStampRelativeMSec;
            Priority = switchData.NewThreadPriority;
            WaitReason = (int)switchData.OldThreadWaitReason;
            OldThreadId = switchData.OldThreadID;
        }

        public ThreadWorkSpan(ThreadWorkSpan span)
        {
            ProcessName = span.ProcessName;
            ThreadId = span.ThreadId;
            ProcessId = span.ProcessId;
            ProcessorNumber = span.ProcessorNumber;
            AbsoluteTimestampMsc = span.AbsoluteTimestampMsc;
            DurationMsc = span.DurationMsc;
            Priority = span.Priority;
            WaitReason = span.WaitReason;
            OldThreadId = span.OldThreadId;
        }

        public ThreadWorkSpan(SampledProfileTraceData sample)
        {
            ProcessName = sample.ProcessName;
            ProcessId = sample.ProcessID;
            ThreadId = sample.ThreadID;
            ProcessorNumber = sample.ProcessorNumber;
            AbsoluteTimestampMsc = sample.TimeStampRelativeMSec;
            DurationMsc = 1;
            Priority = 0;
            OldThreadId = null;
        }
    }
    /// <summary>
    /// Reason for an induced GC
    /// </summary>
    public enum InducedType
    {
        Blocking = 1,
        NotForced = 2,
    }
    /// <summary>
    /// CondemnedReason
    /// </summary>
    [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
    public struct EncodedCondemnedReasons
    {
        public int Reasons;
        public int ReasonsEx;
    }

    /// <summary>
    /// Heap condemned reason
    /// </summary>
    [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
    public class GCCondemnedReasons
    {
        public EncodedCondemnedReasons EncodedReasons;
        /// <summary>
        /// This records which reasons are used and the value. Since the biggest value
        /// we need to record is the generation number a byte is sufficient.
        /// </summary>
        public byte[] CondemnedReasonGroups;
        public void Decode(int Version)
        {
            // First decode the reasons that return us a generation number. 
            // It's the same in 4.0 and 4.5.
            for (Condemned_Reason_Generation i = 0; i < Condemned_Reason_Generation.Max; i++)
            {
                CondemnedReasonGroups[(int)i] = (byte)GetReasonWithGenNumber(i);
            }

            // Then decode the reasons that just indicate true or false.
            for (Condemned_Reason_Condition i = 0; i < Condemned_Reason_Condition.Max; i++)
            {
                if (GetReasonWithCondition(i, Version))
                {
                    switch (i)
                    {
                        case Condemned_Reason_Condition.Induced_fullgc_p:
                            CondemnedReasonGroups[(int)CondemnedReasonGroup.Induced] = (byte)InducedType.Blocking;
                            break;
                        case Condemned_Reason_Condition.Induced_noforce_p:
                            CondemnedReasonGroups[(int)CondemnedReasonGroup.Induced] = (byte)InducedType.NotForced;
                            break;
                        case Condemned_Reason_Condition.Low_ephemeral_p:
                            CondemnedReasonGroups[(int)CondemnedReasonGroup.Low_Ephemeral] = 1;
                            break;
                        case Condemned_Reason_Condition.Low_card_p:
                            CondemnedReasonGroups[(int)CondemnedReasonGroup.Internal_Tuning] = 1;
                            break;
                        case Condemned_Reason_Condition.Eph_high_frag_p:
                            CondemnedReasonGroups[(int)CondemnedReasonGroup.Fragmented_Ephemeral] = 1;
                            break;
                        case Condemned_Reason_Condition.Max_high_frag_p:
                            CondemnedReasonGroups[(int)CondemnedReasonGroup.Fragmented_Gen2] = 1;
                            break;
                        case Condemned_Reason_Condition.Max_high_frag_e_p:
                            CondemnedReasonGroups[(int)CondemnedReasonGroup.Fragmented_Gen1_To_Gen2] = 1;
                            break;
                        case Condemned_Reason_Condition.Max_high_frag_m_p:
                        case Condemned_Reason_Condition.Max_high_frag_vm_p:
                            CondemnedReasonGroups[(int)CondemnedReasonGroup.Fragmented_Gen2_High_Mem] = 1;
                            break;
                        case Condemned_Reason_Condition.Max_gen1:
                            CondemnedReasonGroups[(int)CondemnedReasonGroup.Alloc_Exceeded] = 2;
                            break;
                        case Condemned_Reason_Condition.Expand_fullgc_p:
                            CondemnedReasonGroups[(int)CondemnedReasonGroup.Expand_Heap] = 1;
                            break;
                        case Condemned_Reason_Condition.Before_oom:
                            CondemnedReasonGroups[(int)CondemnedReasonGroup.GC_Before_OOM] = 1;
                            break;
                        case Condemned_Reason_Condition.Gen2_too_small:
                            CondemnedReasonGroups[(int)CondemnedReasonGroup.Too_Small_For_BGC] = 1;
                            break;
                        case Condemned_Reason_Condition.Before_bgc:
                            CondemnedReasonGroups[(int)CondemnedReasonGroup.Ephemeral_Before_BGC] = 1;
                            break;
                        default:
                            Debug.Assert(false, "Unexpected reason");
                            break;
                    }
                }
            }
        }

        #region private
        // These values right now are the same as the first 4 in CondemnedReasonGroup.
        private enum Condemned_Reason_Generation
        {
            Initial = 0,
            Final_per_heap = 1,
            Alloc_budget = 2,
            Time_tuning = 3,
            Max = 4,
        };

        private enum Condemned_Reason_Condition
        {
            Induced_fullgc_p = 0,
            Expand_fullgc_p = 1,
            High_mem_p = 2,
            Very_high_mem_p = 3,
            Low_ephemeral_p = 4,
            Low_card_p = 5,
            Eph_high_frag_p = 6,
            Max_high_frag_p = 7,
            Max_high_frag_e_p = 8,
            Max_high_frag_m_p = 9,
            Max_high_frag_vm_p = 10,
            Max_gen1 = 11,
            Before_oom = 12,
            Gen2_too_small = 13,
            Induced_noforce_p = 14,
            Before_bgc = 15,
            Max = 16,
        };

        private int GetReasonWithGenNumber(Condemned_Reason_Generation Reason_GenNumber)
        {
            int GenNumber = ((EncodedReasons.Reasons >> ((int)Reason_GenNumber * 2)) & 0x3);
            return GenNumber;
        }

        private bool GetReasonWithCondition(Condemned_Reason_Condition Reason_Condition, int Version)
        {
            bool ConditionIsSet = false;
            if (Version == 0)
            {
                Debug.Assert((int)Reason_Condition < 16);
                ConditionIsSet = ((EncodedReasons.Reasons & (1 << (int)(Reason_Condition + 16))) != 0);
            }
            else if (Version >= 2)
            {
                ConditionIsSet = ((EncodedReasons.ReasonsEx & (1 << (int)Reason_Condition)) != 0);
            }
            else
            {
                Debug.Assert(false, "GetReasonWithCondition invalid version : " + Version);
            }

            return ConditionIsSet;
        }

        #endregion 
    }

    /// <summary>
    /// Container for mark times 
    /// </summary>
    public class MarkInfo
    {
        // Note that in 4.5 and prior (ie, from GCMark events, not GCMarkWithType), the first stage of the time 
        // includes scanning sizedref handles(which can be very significant). We could distinguish that by interpreting 
        // the Join events which I haven't done yet.
        public double[] MarkTimes;
        public long[] MarkPromoted;

        public MarkInfo(bool initPromoted = true)
        {
            MarkTimes = new double[(int)MarkRootType.MarkMax];
            if (initPromoted)
            {
                MarkPromoted = new long[(int)MarkRootType.MarkMax];
            }
        }
    };

    /// <summary>
    /// Per heap statistics
    /// </summary>
    public class GCPerHeapHistory
    {
        public int MemoryPressure;
        public bool HasMemoryPressure;
        public bool VersionRecognized;
        public long FreeListAllocated;
        public bool HasFreeListAllocated;
        public long FreeListRejected;
        public bool HasFreeListRejected;
        public int CondemnReasons0;
        public int CondemnReasons1;
        public bool HasCondemnReasons1;
        public int CompactMechanisms;
        public int ExpandMechanisms;
        public int Version;
        public GCPerHeapHistoryGenData[] GenData;
    }

    /// <summary>
    /// Process heap statistics
    /// </summary>
    public class GCGlobalHeapHistory
    {
        public long FinalYoungestDesired;
        public int NumHeaps;
        public int CondemnedGeneration;
        public int Gen0ReductionCount;
        public GCReason Reason;
        public GCGlobalMechanisms GlobalMechanisms;
        public GCPauseMode PauseMode;
        public int MemoryPressure;
        public bool HasMemoryPressure;
    }

    /// <summary>
    /// Per heap stastics
    /// </summary>
    public class GCHeapStats
    {
        public long TotalHeapSize;
        public long TotalPromoted;
        public int Depth;
        public long GenerationSize0;
        public long TotalPromotedSize0;
        public long GenerationSize1;
        public long TotalPromotedSize1;
        public long GenerationSize2;
        public long TotalPromotedSize2;
        public long GenerationSize3;
        public long TotalPromotedSize3;
        public long FinalizationPromotedSize;
        public long FinalizationPromotedCount;
        public int PinnedObjectCount;
        public int SinkBlockCount;
        public int GCHandleCount;
    }

    /// <summary>
    /// Approximations we do in this function for V4_5 and prior:
    /// On 4.0 we didn't seperate free list from free obj, so we just use fragmentation (which is the sum)
    /// as an approximation. This makes the efficiency value a bit larger than it actually is.
    /// We don't actually update in for the older gen - this means we only know the out for the younger 
    /// gen which isn't necessarily all allocated into the older gen. So we could see cases where the 
    /// out is > 0, yet the older gen's free list doesn't change. Using the younger gen's out as an 
    /// approximation makes the efficiency value larger than it actually is.
    ///
    /// For V4_6 this requires no approximation.
    ///
    /// 
    /// </summary>
    [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
    public class FreeListEfficiency
    {
        public bool Valid = false;
        public double Allocated;
        public double FreeListConsumed;
    }

    // WARN: A background GC may still have foreground threads.
    [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
    public enum GCThreadKind
    {
        Foreground,
        Background,
    }

    [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
    public readonly struct GCHeapAndThreadKind
    {
        public readonly HeapID HeapID;
        public readonly GCThreadKind ThreadKind;

        public GCHeapAndThreadKind(
            HeapID heapID,
            GCThreadKind threadKind)
        {
            HeapID = heapID;
            ThreadKind = threadKind;
        }
    }

    internal readonly struct GCHeapAndThreadKindAndIsNewThread
    {
        public readonly GCHeapAndThreadKind HeapAndThreadKind;
        public readonly bool NewThreadIsGC;

        public GCHeapAndThreadKindAndIsNewThread(
            GCHeapAndThreadKind heapAndThreadKind,
            bool newThreadIsGC)
        {
            HeapAndThreadKind = heapAndThreadKind;
            NewThreadIsGC = newThreadIsGC;
        }

        public HeapID HeapID =>
            HeapAndThreadKind.HeapID;

        public GCThreadKind ThreadKind =>
            HeapAndThreadKind.ThreadKind;
    }

    internal enum GcBackgroundKind
    {
        Foreground,
        Background
    }

    [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
    public class GcJoin
    {
        public int ThreadId;
        public bool IsEESuspended;
        public GCHeapAndThreadKind HeapAndThreadKind;
        public int ProcessorNumber;
        public double RelativeTimestampMsc;
        public double AbsoluteTimestampMsc;
        public GcJoinType Type;
        public GcJoinTime Time;
        public int JoinID;

        public HeapID Heap => HeapAndThreadKind.HeapID;
        public GCThreadKind ThreadKind => HeapAndThreadKind.ThreadKind;

        [Obsolete]
        public GCJoinStage JoinStage =>
            (GCJoinStage) JoinID;
    }

    // Server history per heap. This is for CSwitch/CPU sample/Join events.
    // Each server GC thread goes through this flow during each GC
    // 1) runs server GC code
    // 2) joins with other GC threads
    // 3) restarts
    // 4) goes back to 1).
    // We call 1 through 3 an activity. There are as many activities as there are joins.
    [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
    public class ServerGcHistory
    {
        public HeapID HeapId;
        public int ProcessId;
        public ThreadID? GcWorkingThreadId;
        public ThreadID? GcBackgroundThreadId;
        public int? GcWorkingThreadPriority;
        public List<GcWorkSpan> SwitchSpans = new List<GcWorkSpan>();
        public List<GcWorkSpan> SampleSpans = new List<GcWorkSpan>();
        public List<GcJoin> GcJoins = new List<GcJoin>();

        #region private 
        //list of times in msc starting from GC start when GCJoin events were fired for this heap

        internal void AddSampleEvent(ThreadWorkSpan sample, double pauseStartRelativeMSec, GCThreadKind? threadKind, bool newThreadIsGC)
        {
            GcWorkSpan lastSpan = SampleSpans.Count > 0 ? SampleSpans[SampleSpans.Count - 1] : null;
            if (lastSpan != null && lastSpan.ThreadId == sample.ThreadId && lastSpan.ProcessId == sample.ProcessId &&
                ((ulong)sample.AbsoluteTimestampMsc == (ulong)(lastSpan.AbsoluteTimestampMsc + lastSpan.DurationMsc)))
            {
                lastSpan.DurationMsc++;
            }
            else
            {
                SampleSpans.Add(new GcWorkSpan(sample, threadKind)
                {
                    Type = GetSpanType(sample, newThreadIsGC: newThreadIsGC),
                    RelativeTimestampMsc = sample.AbsoluteTimestampMsc - pauseStartRelativeMSec,
                    DurationMsc = 1
                });
            }
        }

        internal void AddSwitchEvent(ThreadWorkSpan switchData, double pauseStartRelativeMSec, GCThreadKind? threadKind, bool newThreadIsGC)
        {
            GcWorkSpan lastSpan = SwitchSpans.Count > 0 ? SwitchSpans[SwitchSpans.Count - 1] : null;
            if (switchData.ThreadId == GcWorkingThreadId && switchData.ProcessId == ProcessId)
            {
                //update gc thread priority since we have new data
                GcWorkingThreadPriority = switchData.Priority;
            }

            if (lastSpan != null)
            {
                //updating duration of the last one, based on a timestamp from the new one
                lastSpan.DurationMsc = switchData.AbsoluteTimestampMsc - lastSpan.AbsoluteTimestampMsc;

                //updating wait readon of the last one
                lastSpan.WaitReason = switchData.WaitReason;
            }

            SwitchSpans.Add(new GcWorkSpan(switchData, threadKind: threadKind)
            {
                Type = GetSpanType(switchData, newThreadIsGC: newThreadIsGC),
                RelativeTimestampMsc = switchData.AbsoluteTimestampMsc - pauseStartRelativeMSec,
                Priority = switchData.Priority
            });
        }

        internal void GCEnd(double pauseDurationMSec)
        {
            GcWorkSpan lastSpan = SwitchSpans.Count > 0 ? SwitchSpans[SwitchSpans.Count - 1] : null;
            if (lastSpan != null)
            {
                lastSpan.DurationMsc = pauseDurationMSec - lastSpan.RelativeTimestampMsc;
            }
        }

        private WorkSpanType GetSpanType(ThreadWorkSpan span, bool newThreadIsGC)
        {
            if (span.ProcessId == 0)
            {
                Debug.Assert(span.ThreadId == 0, "Idle process should have idle thread");
            }

            if (newThreadIsGC)
            {
                Debug.Assert(span.ThreadId == GcWorkingThreadId || GcWorkingThreadId == null, "Should agree on which thread is the GC thread");
                return WorkSpanType.GcThread;
            }
            else if (span.ThreadId == 0)
            {
                return WorkSpanType.Idle;
            }
            else if (span.Priority >= GcWorkingThreadPriority || span.Priority == -1)
            {
                return WorkSpanType.RivalThread;
            }
            else
            {
                return WorkSpanType.LowPriThread;
            }
        }

        // A note about the join events - the restart events have no heap number associated so 
        // we add them to every heap with the ProcessorNumber so we know which heap/processor it was 
        // fired on.
        // Also for these restart events, the id field is always -1.
        internal void AddJoin(GCJoinTraceData data, double pauseStartRelativeMSec, bool isEESuspended, GCHeapAndThreadKind heapAndThreadKind)
        {
            GcJoins.Add(new GcJoin
            {
                ThreadId = data.ThreadID,
                IsEESuspended = isEESuspended,
                HeapAndThreadKind = heapAndThreadKind,
                ProcessorNumber = data.ProcessorNumber,
                AbsoluteTimestampMsc = data.TimeStampRelativeMSec,
                RelativeTimestampMsc = data.TimeStampRelativeMSec - pauseStartRelativeMSec,
                Type = data.JoinType,
                Time = data.JoinTime,
                JoinID = data.GCID,
            });
        }
        #endregion
    }

    [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
    public enum WorkSpanType
    {
        GcThread,
        RivalThread,
        LowPriThread,
        Idle
    }

    [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
    public class GcWorkSpan : ThreadWorkSpan
    {
        public WorkSpanType Type;
        public double RelativeTimestampMsc;
        public GCThreadKind? ThreadKind;

        public GcWorkSpan(ThreadWorkSpan span, GCThreadKind? threadKind)
            : base(span)
        {
            ThreadKind = threadKind;
        }
    }
}

// Aggregate analysis.  

namespace Microsoft.Diagnostics.Tracing.Analysis.JIT
{

    /// <summary>
    /// Process statistics about JIT'd code
    /// </summary>
    public class JITStats
    {
        /// <summary>
        /// Number of JITT'd methods
        /// </summary>
        public long Count;
        /// <summary>
        /// Total cpu samples for this process
        /// </summary>
        public double TotalCpuTimeMSec;
        /// <summary>
        /// Number of methods JITT'd by foreground threads just prior to execution
        /// </summary>
        public long CountForeground;
        /// <summary>
        /// Total time spent compiling methods on foreground threads
        /// </summary>
        public double TotalForegroundCpuTimeMSec;
        /// <summary>
        /// Number of methods JITT'd by the multicore JIT background threads
        /// </summary>
        public long CountBackgroundMultiCoreJit;
        /// <summary>
        /// Total time spent compiling methods on background threads for multicore JIT
        /// </summary>
        public double TotalBackgroundMultiCoreJitCpuTimeMSec;
        /// <summary>
        /// Number of methods JITT'd by the tiered compilation background threads
        /// </summary>
        public long CountBackgroundTieredCompilation;
        /// <summary>
        /// Total time spent compiling methods on background threads for tiered compilation
        /// </summary>
        public double TotalBackgroundTieredCompilationCpuTimeMSec;
        /// <summary>
        /// Total IL size for all JITT'd methods
        /// </summary>
        public long TotalILSize;
        /// <summary>
        /// Total native code size for all JITT'd methods
        /// </summary>
        public long TotalNativeSize;
        /// <summary>
        /// Indication if this is running on .NET 4.x+
        /// </summary>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public bool IsClr4;
        /// <summary>
        /// Indicates if this process has sufficient JIT activity to be interesting
        /// </summary>
        public bool Interesting { get { return Count > 0 || IsClr4; } }

        /// <summary>
        /// Background JIT: Time Jit was aborted (ms)
        /// </summary>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public double BackgroundJitAbortedAtMSec;
        /// <summary>
        /// Background JIT: Assembly name of last assembly loaded before JIT aborted
        /// </summary>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public string LastAssemblyLoadNameBeforeAbort;
        /// <summary>
        /// Background JIT: Relative start time of last assembly loaded before JIT aborted
        /// </summary>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public double LastAssemblyLoadBeforeAbortMSec;
        /// <summary>
        /// Background JIT: Indication if the last assembly load was successful before JIT aborted
        /// </summary>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public bool LastAssemblyLoadBeforeAbortSuccessful;
        /// <summary>
        /// Background JIT: Thread id of the background JIT
        /// </summary>
        public int BackgroundJitThread;
        /// <summary>
        /// Background JIT: Indication that background JIT events are enabled
        /// </summary>
        public bool BackgroundJITEventsOn;

        /// <summary>
        /// List of successfully inlinded methods
        /// </summary>
        public List<InliningSuccessResult> InliningSuccesses = new List<InliningSuccessResult>();

        /// <summary>
        /// List of failed inlined methods
        /// </summary>
        public List<InliningFailureResult> InliningFailures = new List<InliningFailureResult>();

        /// <summary>
        /// Modules encountered while processing managed samples
        /// </summary>
        public HashSet<string> RecordedModules = new HashSet<string>();
        /// <summary>
        /// List of modules whose symbols were not successfully loaded
        /// </summary>
        public HashSet<string> SymbolsMissing = new HashSet<string>();

        /// <summary>
        /// Aggregate a method to be included in the statistics
        /// </summary>
        /// <param name="method"></param>
        public void AddMethodToStatistics(TraceJittedMethod method)
        {
            Count++;
            TotalCpuTimeMSec += method.CompileCpuTimeMSec;
            TotalILSize += method.ILSize;
            TotalNativeSize += method.NativeSize;
            if (method.CompilationThreadKind == CompilationThreadKind.MulticoreJitBackground)
            {
                CountBackgroundMultiCoreJit++;
                TotalBackgroundMultiCoreJitCpuTimeMSec += method.CompileCpuTimeMSec;
            }
            else if (method.CompilationThreadKind == CompilationThreadKind.TieredCompilationBackground)
            {
                CountBackgroundTieredCompilation++;
                TotalBackgroundTieredCompilationCpuTimeMSec += method.CompileCpuTimeMSec;
            }
            else if (method.CompilationThreadKind == CompilationThreadKind.Foreground)
            {
                CountForeground++;
                TotalForegroundCpuTimeMSec += method.CompileCpuTimeMSec;
            }
        }

        #region private
        /// <summary>
        /// Legacgy
        /// </summary>
        internal static TraceJittedMethod MethodComplete(TraceLoadedDotNetRuntime stats, TraceEvent data, int methodNativeSize, long moduleID, string methodName, long methodID, int rejitID, out bool createdNewMethod)
        {
            TraceJittedMethod _method = stats.JIT.m_stats.FindIncompleteJitEventOnThread(stats, data.ThreadID);
            createdNewMethod = false;
            if (_method == null)
            {
                createdNewMethod = true;

                // We don't have JIT start, do the best we can.  
                _method = stats.JIT.m_stats.LogJitStart(stats, data, methodName, 0, moduleID, methodID);
                if (stats.JIT.m_stats.IsClr4)
                {
                    // Debug.WriteLine("Warning: MethodComplete at {0:n3} process {1} thread {2} without JIT Start, assuming 0 JIT time",
                    //    data.TimeStampRelativeMSec, data.ProcessName, data.ThreadID);
                }
                else if (!stats.JIT.m_stats.warnedUser)
                {
                    // Console.WriteLine("Warning: Process {0} ({1}) is running a V2.0 CLR, no JIT Start events available, so JIT times will all be 0.", stats.ProcessName, stats.ProcessID);
                    stats.JIT.m_stats.warnedUser = true;
                }
            }
            _method.NativeSize = methodNativeSize;
            _method.CompileCpuTimeMSec = data.TimeStampRelativeMSec - _method.StartTimeMSec;
            _method.VersionID = rejitID;

            if (stats.JIT.Stats().BackgroundJitThread != 0 && _method.ThreadID == stats.JIT.Stats().BackgroundJitThread)
            {
                _method.CompilationThreadKind = CompilationThreadKind.MulticoreJitBackground;
            }
            else
            {
                // This isn't always true, but we don't yet have enough data to distinguish tiered compilation from other causes of versioned compilation (ie profiler ReJIT)
                _method.CompilationThreadKind = _method.IsDefaultVersion ? CompilationThreadKind.Foreground : CompilationThreadKind.TieredCompilationBackground;

            }

            _method.Completed++;
            stats.JIT.m_stats.AddMethodToStatistics(_method);

            return _method;
        }

        /// <summary>
        /// Uniquely represents a method within a process.
        /// Used as a lookup key for data structures.
        /// </summary>
        internal struct MethodKey
        {
            private long _ModuleId;
            private long _MethodId;

            public MethodKey(
                long moduleId,
                long methodId)
            {
                _ModuleId = moduleId;
                _MethodId = methodId;
            }

            public override bool Equals(object obj)
            {
                if (obj is MethodKey)
                {
                    MethodKey otherKey = (MethodKey)obj;
                    return ((_ModuleId == otherKey._ModuleId) && (_MethodId == otherKey._MethodId));
                }

                return false;
            }

            public override int GetHashCode()
            {
                return (int)(_ModuleId ^ _MethodId);
            }
        }

        internal TraceJittedMethod LogJitStart(TraceLoadedDotNetRuntime proc, TraceEvent data, string methodName, int ILSize, long moduleID, long methodID)
        {
            TraceJittedMethod _method = new TraceJittedMethod();
            _method.StartTimeMSec = data.TimeStampRelativeMSec;
            _method.ILSize = ILSize;
            _method.MethodName = methodName;
            _method.ThreadID = data.ThreadID;
            string modname = "";
            _method.ModuleID = moduleID;
            moduleNamesFromID.TryGetValue(moduleID, out modname);
            if (!string.IsNullOrWhiteSpace(modname))
            {
                _method.ModuleILPath = modname;
            }
            else
            {
                _method.ModuleILPath = "";
            }

            proc.JIT.Methods.Add(_method);

            if (BackgroundJitThread == _method.ThreadID)
            {
                MethodKey key = new MethodKey(moduleID, methodID);
                backgroundJitEvents[key] = _method;
            }
            else if (BackgroundJitThread != 0)
            {
                // Get the module name.
                if (moduleNamesFromID.ContainsKey(moduleID))
                {
                    string moduleName = moduleNamesFromID[moduleID];
                    if (!string.IsNullOrEmpty(moduleName))
                    {
                        moduleName = System.IO.Path.GetFileNameWithoutExtension(moduleName);
                    }

                    // Check to see if this module is in the profile.
                    if (!RecordedModules.Contains(moduleName))
                    {
                        // Mark the blocking reason that the module is not in the profile, so we'd never background JIT it.
                        _method.BlockedReason = "Module not recorded";
                    }
                    else
                    {
                        _method.BlockedReason = LastBlockedReason;
                    }
                }
                else
                {
                    _method.BlockedReason = LastBlockedReason;
                }
            }
            else
            {
                _method.BlockedReason = LastBlockedReason;
            }

            return _method;
        }

        internal static string GetMethodName(MethodJittingStartedTraceData data)
        {
            int parenIdx = data.MethodSignature.IndexOf('(');
            if (parenIdx < 0)
            {
                parenIdx = data.MethodSignature.Length;
            }

            return data.MethodNamespace + "." + data.MethodName + data.MethodSignature.Substring(parenIdx);
        }
        internal static string GetMethodName(MethodLoadUnloadVerboseTraceData data)
        {
            int parenIdx = data.MethodSignature.IndexOf('(');
            if (parenIdx < 0)
            {
                parenIdx = data.MethodSignature.Length;
            }

            return data.MethodNamespace + "." + data.MethodName + data.MethodSignature.Substring(parenIdx);
        }

        private TraceJittedMethod FindIncompleteJitEventOnThread(TraceLoadedDotNetRuntime proc, int threadID)
        {
            for (int i = proc.JIT.Methods.Count - 1; 0 <= i; --i)
            {
                TraceJittedMethod ret = proc.JIT.Methods[i];
                if (ret.ThreadID == threadID)
                {
                    // This is a completed JIT event, not what we are looking for. 
                    if (ret.NativeSize > 0 || ret.CompileCpuTimeMSec > 0)
                    {
                        continue;
                    }

                    return ret;
                }
            }
            return null;
        }

        internal string LastBlockedReason = "BG JIT not enabled";

        internal bool warnedUser;

        internal bool playbackAborted = false;
        internal Dictionary<MethodKey, TraceJittedMethod> backgroundJitEvents = new Dictionary<MethodKey, TraceJittedMethod>();
        internal Dictionary<long, string> moduleNamesFromID = new Dictionary<long, string>();

        #endregion
    }

    /// <summary>
    /// JIT inlining successes
    /// </summary>
    public struct InliningSuccessResult
    {
        public string MethodBeingCompiled;
        public string Inliner;
        public string Inlinee;
    }

    /// <summary>
    /// JIT inlining failures
    /// </summary>
    public struct InliningFailureResult
    {
        public string MethodBeingCompiled;
        public string Inliner;
        public string Inlinee;
        public string Reason;
    }

    public enum CompilationThreadKind
    {
        Foreground,
        MulticoreJitBackground,
        TieredCompilationBackground
    }

    /// <summary>
    /// Per method information
    /// </summary>
    public class TraceJittedMethod
    {
        /// <summary>
        /// Time taken to compile the method
        /// </summary>
        public double CompileCpuTimeMSec;
        /// <summary>
        /// IL size of method
        /// </summary>
        public int ILSize;
        /// <summary>
        /// Native code size of method
        /// </summary>
        public int NativeSize;
        /// <summary>
        /// Relative start time of JIT'd method
        /// </summary>
        public double StartTimeMSec;
        /// <summary>
        /// Method name
        /// </summary>
        public string MethodName;
        /// <summary>
        /// Module name
        /// </summary>
        public string ModuleILPath;
        /// <summary>
        /// Thread id where JIT'd
        /// </summary>
        public int ThreadID;
        /// <summary>
        /// Indication of if it was JIT'd in the background
        /// </summary>
        [Obsolete("Use CompilationThreadKind")]
        public bool IsBackground
        {
            get
            {
                return CompilationThreadKind != CompilationThreadKind.Foreground;
            }
        }
        /// <summary>
        /// Indication of if it was JIT'd in the background and why
        /// </summary>
        public CompilationThreadKind CompilationThreadKind;
        /// <summary>
        /// Amount of time the method was forcasted to JIT
        /// </summary>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public double DistanceAhead
        {
            get
            {
                double distanceAhead = 0;
                if (default(double) != ForegroundMethodRequestTimeMSec)
                {
                    distanceAhead = ForegroundMethodRequestTimeMSec - StartTimeMSec;
                }

                return distanceAhead;
            }
        }
        /// <summary>
        /// Indication of if the background JIT request was blocked and why
        /// </summary>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public string BlockedReason
        {
            get
            {
                if (null == _blockedReason)
                {
                    _blockedReason = "None";
                }

                return _blockedReason;
            }
            set
            {
                _blockedReason = value;
            }
        }
        /// <summary>
        /// Number of cpu samples for this method
        /// </summary>
        public double RunCpuTimeMSec;

        /// <summary>
        /// The version id that is created by the runtime code versioning feature. This is an incrementing counter that starts at 0 for each method.
        /// The ETW events historically name this as the ReJITID event parameter in the payload, but we have now co-opted its usage.
        /// </summary>
        public int VersionID;

        public bool IsDefaultVersion { get { return VersionID == 0; } }

        #region private
        /// <summary>
        /// Legacy
        /// </summary>
        internal double ForegroundMethodRequestTimeMSec;
        internal string _blockedReason;
        internal int Completed = 0;
        internal long ModuleID = 0;
        #endregion
    }
}

namespace Microsoft.Diagnostics.Tracing.Analysis.GC
{
    /// <summary>
    /// Statistical garbage collector (GC) information about a managed process
    /// </summary>
    public class GCStats
    {
        /// <summary>
        /// Number of GC's for this process
        /// </summary>
        public int Count;
        /// <summary>
        /// Number of GC's which were induced, eg. GC.Collect, etc.
        /// </summary>
        public int NumInduced;
        /// <summary>
        /// Total size of the pinned objects seen at collection time
        /// </summary>
        public long PinnedObjectSizes;
        /// <summary>
        /// Of all the memory that is current pinned, how much of it is from pinned objects
        /// </summary>
        public int PinnedObjectPercentage;
        /// <summary>
        /// Number of GC's that contained pinned objects
        /// </summary>
        public long NumWithPinEvents;
        /// <summary>
        /// Number of GC's that contained pin plugs
        /// </summary>
        public long NumWithPinPlugEvents;
        /// <summary>
        /// The longest pause duration (ms)
        /// </summary>
        public double MaxPauseDurationMSec;
        /// <summary>
        /// Avarege pause duration (ms)
        /// </summary>
        public double MeanPauseDurationMSec { get { return TotalPauseTimeMSec / Count; } }
        /// <summary>
        /// Average heap size after a GC (mb)
        /// </summary>
        public double MeanSizeAfterMB { get { return TotalSizeAfterMB / Count; } }
        /// <summary>
        /// Average peak heap size (mb)
        /// </summary>
        public double MeanSizePeakMB { get { return TotalSizePeakMB / Count; } }
        /// <summary>
        /// Average exclusive cpu samples (ms) during GC's
        /// </summary>
        public double MeanCpuMSec { get { return TotalCpuMSec / Count; } }
        /// <summary>
        /// Total GC pause time (ms)
        /// </summary>
        public double TotalPauseTimeMSec;
        /// <summary>
        /// Max suspend duration (ms), should be very small
        /// </summary>
        public double MaxSuspendDurationMSec;
        /// <summary>
        /// Max peak heap size (mb)
        /// </summary>
        public double MaxSizePeakMB;
        /// <summary>
        /// Max allocation per second (mb/sec)
        /// </summary>
        public double MaxAllocRateMBSec;
        /// <summary>
        /// Total allocations in the process lifetime (mb)
        /// </summary>
        public double TotalAllocatedMB;
        /// <summary>
        /// Total exclusive cpu samples (ms)
        /// </summary>
        public double TotalCpuMSec;
        /// <summary>
        /// Total memory promoted between generations (mb)
        /// </summary>
        public double TotalPromotedMB;
        /// <summary>
        /// (obsolete) Total size of heaps after GC'ss (mb)
        /// </summary>
        public double TotalSizeAfterMB;
        /// <summary>
        /// (obsolete) Total peak heap sizes (mb)
        /// </summary>
        public double TotalSizePeakMB;
        /// <summary>
        /// Indication if this process is interesting from a GC pov
        /// </summary>
        public bool Interesting { get { return Count > 0; } }
        /// <summary>
        /// List of finalizer objects
        /// </summary>
        public Dictionary<string, long> FinalizedObjects = new Dictionary<string, long>();
        /// <summary>
        /// Percentage of time spent paused as compared to the process lifetime
        /// </summary>
        /// <returns></returns>
        public double GetGCPauseTimePercentage()
        {
            return ((ProcessDuration == 0) ? 0.0 : ((TotalPauseTimeMSec * 100) / ProcessDuration));
        }
        /// <summary>
        /// Running time of the process.  Measured as time spent between first and last GC event observed
        /// </summary>
        public double ProcessDuration;
        /// <summary>
        /// Means it detected that the ETW information is in a format it does not understand.
        /// </summary>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public bool GCVersionInfoMismatch { get; private set; }
        /// <summary>
        /// Indicator of if ServerGC is enabled (1).  -1 indicates that not enough events have been processed to know for sure.
        /// We don't necessarily have the GCSettings event (only fired at the beginning if we attach)
        /// So we have to detect whether we are running server GC or not.
        /// Till we get our first GlobalHeapHistory event which indicates whether we use server GC 
        /// or not this remains -1.
        /// </summary>
        public int IsServerGCUsed = -1;
        /// <summary>
        /// Number of heaps.  -1 indicates that not enough events have been processed to know for sure.
        /// </summary>
        public int HeapCount = -1;
        /// <summary>
        /// Indicator if PerHeapHistories is present
        /// </summary>
        public bool HasDetailedGCInfo;

        #region private

        // This is the last GC in progress. We need this for server Background GC.
        // See comments for lastCompletedGC.
        private static TraceGC GetLastGC(TraceLoadedDotNetRuntime proc, double timeStampRelativeMSec)
        {
            TraceGC _event = TraceGarbageCollector.GetCurrentGC(proc, timeStampRelativeMSec: timeStampRelativeMSec);
            if ((proc.GC.m_stats.IsServerGCUsed == 1) &&
                (_event == null))
            {
                if (proc.GC.m_stats.lastCompletedGC != null)
                {
                    // TODO: I've seen this fail. No idea why it's supposed to succeed.
                    // Debug.Assert(proc.GC.m_stats.lastCompletedGC.Type == GCType.BackgroundGC);
                    _event = proc.GC.m_stats.lastCompletedGC;
                }
            }

            return _event;
        }

        //
        // candiate to be made private/ex
        //
        // The amount of memory allocated by the user threads. So they are divided up into gen0 and LOH allocations.
        internal double[] allocTickCurrentMB = { 0.0, 0.0 };
        internal double[] allocTickAtLastGC = { 0.0, 0.0 };
        internal bool HasAllocTickEvents = false;
        internal bool SeenBadAllocTick = false;

        internal double lastRestartEndTimeRelativeMSec;

        // This is the BGC that's in progress as we are parsing. We need to remember this 
        // so we can correctly attribute the suspension time.
        internal TraceGC currentBGC = null;
        internal TraceGC currentOrFinishedBGC = null;

        internal static TraceGC GetLastBGC(TraceLoadedDotNetRuntime proc)
        {
            if (proc.GC.m_stats.currentBGC != null)
            {
                return proc.GC.m_stats.currentBGC;
            }

            if ((proc.GC.m_stats.lastCompletedGC != null) && (proc.GC.m_stats.lastCompletedGC.Type == GCType.BackgroundGC))
            {
                return proc.GC.m_stats.lastCompletedGC;
            }

            // Otherwise we search till we find the last BGC if we have seen one.
            for (int i = (proc.GC.GCs.Count - 1); i >= 0; i--)
            {
                if (proc.GC.GCs[i].Type == GCType.BackgroundGC)
                {
                    return proc.GC.GCs[i];
                }
            }

            return null;
        }

        internal void AddConcurrentPauseTime(TraceGC _event, double RestartEEMSec)
        {
            if (suspendThreadIDBGC > 0)
            {
                double pause = RestartEEMSec - suspendTimeRelativeMSec;
                _event.PauseDurationMSec += pause;
                if (lastSuspendReason == GCSuspendEEReason.SuspendForGCPrep)
                {
                    _event.BGCFinalPauseMSec = pause;
                }
            }
            else
            {
                // Debug.Assert(_event.PauseDurationMSec == 0);
                _event.PauseDurationMSec = RestartEEMSec - _event.PauseStartRelativeMSec;
            }
        }

        internal void AddServerGCThreadFromMark(ThreadID ThreadID, HeapID HeapNum)
        {
            if (IsServerGCUsed == 1)
            {
                Debug.Assert(HeapCount > 1);
                AssociateServerGCThreadAndHeap(ThreadID, HeapNum);
            }
        }

        internal static void ProcessGlobalHistory(TraceLoadedDotNetRuntime proc, GCGlobalHeapHistoryTraceData data)
        {
            if (proc.GC.m_stats.IsServerGCUsed == -1)
            {
                // We detected whether we are using Server GC now.
                proc.GC.m_stats.IsServerGCUsed = ((data.NumHeaps > 1) ? 1 : 0);
                if (proc.GC.m_stats.HeapCount == -1)
                {
                    proc.GC.m_stats.HeapCount = data.NumHeaps;
                }

                // Below is not necessary, the dictionary always exists
                //if (proc.GC.m_stats.IsServerGCUsed == 1)
                //{
                //    proc.GC.m_stats.serverGCThreads = new Dictionary<ThreadID, HeapID>(data.NumHeaps);
                //}
            }

            TraceGC _event = GetLastGC(proc, data.TimeStampRelativeMSec);
            if (_event != null)
            {
                _event.GlobalHeapHistory = new GCGlobalHeapHistory()
                {
                    FinalYoungestDesired = data.FinalYoungestDesired,
                    CondemnedGeneration = data.CondemnedGeneration,
                    Gen0ReductionCount = data.Gen0ReductionCount,
                    GlobalMechanisms = data.GlobalMechanisms,
                    PauseMode = data.PauseMode,
                    HasMemoryPressure = data.HasMemoryPressure,
                    MemoryPressure = (data.HasMemoryPressure) ? data.MemoryPressure : -1,
                    NumHeaps = data.NumHeaps,
                    Reason = data.Reason
                };
                _event.SetHeapCount(proc.GC.m_stats.HeapCount);
            }
        }

        internal static void ProcessPerHeapHistory(TraceLoadedDotNetRuntime proc, GCPerHeapHistoryTraceData data)
        {
            if (!data.VersionRecognized)
            {
                proc.GC.m_stats.GCVersionInfoMismatch = true;
                return;
            }

            TraceGC _event = GetLastGC(proc, data.TimeStampRelativeMSec);
            if (_event != null)
            {
                var hist = new GCPerHeapHistory()
                {
                    FreeListAllocated = (data.HasFreeListAllocated) ? data.FreeListAllocated : -1,
                    HasFreeListAllocated = data.HasFreeListAllocated,
                    FreeListRejected = (data.HasFreeListRejected) ? data.FreeListRejected : -1,
                    HasFreeListRejected = data.HasFreeListRejected,
                    MemoryPressure = (data.HasMemoryPressure) ? data.MemoryPressure : -1,
                    HasMemoryPressure = data.HasMemoryPressure,
                    VersionRecognized = data.VersionRecognized,
                    GenData = new GCPerHeapHistoryGenData[(int)Gens.GenLargeObj + 1],
                    CondemnReasons0 = data.CondemnReasons0,
                    CondemnReasons1 = (data.HasCondemnReasons1) ? data.CondemnReasons1 : -1,
                    HasCondemnReasons1 = data.HasCondemnReasons1,
                    CompactMechanisms = (int)data.CompactMechanisms,
                    ExpandMechanisms = (int)data.ExpandMechanisms,
                    Version = data.Version,
                };

                for (Gens GenIndex = Gens.Gen0; GenIndex <= Gens.GenLargeObj; GenIndex++)
                {
                    hist.GenData[(int)GenIndex] = data.GenData(GenIndex);
                }

                _event.PerHeapHistories.Add(hist);
            }
        }


        internal Dictionary<ThreadID, int> ThreadId2Priority = new Dictionary<int, int>();


        // EE can be suspended via different reasons. The only ones we care about are
        // SuspendForGC(1) - suspending for GC start
        // SuspendForGCPrep(6) - BGC uses it in the middle of a BGC.
        // We need to filter out the rest of the suspend/resume events.
        // Keep track of the last time we started suspending the EE.  Will use in 'Start' to set PauseStartRelativeMSec
        internal int suspendThreadIDOther = -1;
        internal int suspendThreadIDBGC = -1;
        // This is either the user thread (in workstation case) or a server GC thread that called SuspendEE to do a GC
        internal int suspendThreadIDGC = -1;
        internal double suspendTimeRelativeMSec = -1;
        internal double suspendEndTimeRelativeMSec = -1;
        internal GCSuspendEEReason lastSuspendReason;

        // This records the amount of CPU time spent at the end of last GC.
        internal double ProcessCpuAtLastGC = 0;

        internal Dictionary<int, object> backgroundGCThreads = new Dictionary<int, object>();
        internal bool IsBGCThread(int threadID)
        {
            bool res = backgroundGCThreads != null && backgroundGCThreads.ContainsKey(threadID);
            // Debug.Assert(res || serverGCThreads.ContainsKey(threadID)); // Fails because serverGCThreads is set in the second GC?
            return res;
        }

        internal GCThreadKind? GetThreadKind(int threadID)
        {
            if (serverGCThreadToHeap.BFromA(threadID) != null)
            {
                return GCThreadKind.Foreground;
            }
            else if (backgroundGCThreads != null && backgroundGCThreads.ContainsKey(threadID))
            {
                return GCThreadKind.Background;
            }
            else
            {
                return null;
            }
        }

        private GCHeapAndThreadKind? TryGetHeapAndThreadKind(ThreadID threadID)
        {
            HeapID? heapIDFG = serverGCThreadToHeap.BFromA(threadID);
            if (heapIDFG != null)
            {
                return new GCHeapAndThreadKind(heapIDFG.Value, GCThreadKind.Foreground);
            }
            // Can't do anything for background threads, we don't have heap numbers for them
            //else if (backgroundGCThreads != null && backgroundGCThreads.TryGetValue(threadID, out HeapID heapIDBG))
            //{
            //    return new GCHeapAndThreadKind(heapIDBG, GCThreadKind.Background);
            //}
            else
            {
                return null;
            }
        }


        internal IEnumerable<GCHeapAndThreadKind> GetHeapAndThreadKinds(ThreadID threadID)
        {
            GCHeapAndThreadKind? heapAndThreadKind = TryGetHeapAndThreadKind(threadID);
            if (heapAndThreadKind != null)
            {
                yield return heapAndThreadKind.Value;
            }
        }

        internal IEnumerable<GCHeapAndThreadKindAndIsNewThread> GetHeapAndThreadKinds(ThreadID? oldThreadID, ThreadID newThreadID) =>
            Enumerable.Concat<GCHeapAndThreadKindAndIsNewThread>(
                oldThreadID == null
                    ? Enumerable.Empty<GCHeapAndThreadKindAndIsNewThread>()
                    : (from x in GetHeapAndThreadKinds(oldThreadID.Value) select new GCHeapAndThreadKindAndIsNewThread(x, newThreadIsGC: false)),
                (from x in GetHeapAndThreadKinds(newThreadID) select new GCHeapAndThreadKindAndIsNewThread(x, newThreadIsGC: true)));

        // I keep this for the purpose of server Background GC. Unfortunately for server background 
        // GC we are firing the GCEnd/GCHeaps events and Global/Perheap events in the reversed order.
        // This is so that the Global/Perheap events can still be attributed to the right BGC.
        internal TraceGC lastCompletedGC = null;

        internal bool gotThreadInfo = false;
        // This is the server GC threads. It's built up in the 2nd server GC we see. 
        private BidirectionalDictionary<ThreadID, HeapID> serverGCThreadToHeap = new BidirectionalDictionary<ThreadID, HeapID>();
        // Since it's possible threads aren't affinitized, we'll count how much there is.
        private CountingDictionary<ProcessorNumber, ThreadID> processorNumberToHeapID = new CountingDictionary<ProcessorNumber, ThreadID>();

        internal void AssociateServerGCThreadAndHeap(ThreadID threadID, HeapID heapID)
        {
            HeapID? curHeapID = GetServerGCHeapFromThread(threadID);
            if (curHeapID == null)
            {
                serverGCThreadToHeap.Associate(threadID, heapID);
            }
            else
            {
                Debug.Assert(curHeapID == heapID);
            }
        }

        internal void AssociateProcessorNumberAndServerGCHeapID(ProcessorNumber processorNumber, HeapID heapID)
        {
            processorNumberToHeapID.Add(processorNumber, heapID);
        }

        internal HeapID? GetHeapIDFromProcessorNumber(ProcessorNumber processorNumber) =>
            processorNumberToHeapID.GetIfOverFraction(processorNumber, 0.9);

        internal string ShowHeapIDsForProcessorNumber(ProcessorNumber processorNumber) =>
            processorNumberToHeapID.ShowEntries(processorNumber);

        internal ThreadID? GetServerGCThreadFromHeap(HeapID heapID) =>
            serverGCThreadToHeap?.AFromB(heapID);

        internal HeapID? GetServerGCHeapFromThread(ThreadID threadID) =>
            serverGCThreadToHeap?.BFromA(threadID);

        internal void SetUpServerGcHistory(int processID, TraceGC gc)
        {
            for (HeapID i = 0; i < HeapCount; i++)
            {
                ThreadID? gcThreadId = serverGCThreadToHeap.AFromB(i);
                int? gcThreadPriority = gcThreadId != null && ThreadId2Priority.TryGetValue(gcThreadId.Value, out int pri) ? pri : (int?) null;
                gc.ServerGcHeapHistories.Add(new ServerGcHistory
                {
                    ProcessId = processID,
                    HeapId = i,
                    // NOTE: Since we might not have all the information yet, we'll set this again at the end.
                    GcWorkingThreadId = gcThreadId,
                    GcBackgroundThreadId = null, // TODO -- similar to above
                    GcWorkingThreadPriority = gcThreadPriority
                });
            }
        }

        #endregion
    }

    class BidirectionalDictionary<A, B> where A : struct where B : struct
    {
        private readonly Dictionary<A, B> a2b = new Dictionary<A, B>();
        private readonly Dictionary<B, A> b2a = new Dictionary<B, A>();

        public void Associate(A a, B b)
        {
            try
            {
                a2b.Add(a, b);
                b2a.Add(b, a);
            }
            catch (ArgumentException)
            {
                if (false)
                {
                    Console.WriteLine("a->b:");
                    foreach (var pair in a2b)
                    {
                        Console.WriteLine($"{pair.Key} -> {pair.Value}");
                    }
                    Console.WriteLine("b->a:");
                    foreach (var pair in b2a)
                    {
                        Console.WriteLine($"{pair.Key} -> {pair.Value}");
                    }
                    Console.WriteLine($"Adding {a} -> {b} failed");
                }
                throw;
            }
        }

        public B? BFromA(A a) =>
            a2b.TryGetValue(a, out B b) ? b : (B?) null;

        public A? AFromB(B b) =>
            b2a.TryGetValue(b, out A a) ? a : (A?) null;
    }

    // Dictionary type where a K may map to multiple of V, with different numbers each time.
    class CountingDictionary<K, V> where K : struct where V : struct, IEquatable<V>
    {
        private readonly struct Entry
        {
            public readonly uint Count;
            public readonly V Value;

            public Entry(uint count, V value)
            {
                Count = count;
                Value = value;
            }

            public Entry Incr() =>
                new Entry(Count + 1, Value);
        }

        // Entries should be sorted from highest to lowest count
        private readonly Dictionary<K, List<Entry>> inner = new Dictionary<K, List<Entry>>();

        public void Add(K key, V value)
        {
            List<Entry> entries = Util.GetOrAdd(inner, key, () => new List<Entry>());
            for (uint i = 0; i < entries.Count; i++)
            {
                Entry entry = entries[(int) i];
                if (entry.Value.Equals(value))
                {
                    entries[(int) i] = entry.Incr();
                    SwapHigherCountsEarlier(entries, i);
                    Validate(entries);
                    return;
                }
            }
            entries.Add(new Entry(1, value));
        }

        private static void SwapHigherCountsEarlier(List<Entry> entries, uint i)
        {
            while (i != 0 && entries[(int)i - 1].Count < entries[(int)i].Count)
            {
                Util.Swap(entries, i - 1, i);
                i--;
            }
        }

        public readonly struct MostCommonEntry
        {
            public readonly double Fraction;
            public readonly V Value;

            public MostCommonEntry(double fraction, V value)
            {
                Fraction = fraction;
                Value = value;
            }
        }

        public MostCommonEntry? GetMostCommonEntry(K key)
        {
            List<Entry>/*?*/ entries = Util.Get(inner, key);
            if (entries == null)
            {
                return null;
            }
            else
            {
                uint total = Util.Sum(from e in entries select e.Count);
                Validate(entries);
                Entry biggestEntry = entries[0];
                double fraction = ((double)biggestEntry.Count) / ((double)total);
                return new MostCommonEntry(fraction: fraction, value: biggestEntry.Value);
            }
        }

        private static void Validate(List<Entry> entries)
        {
            for (uint i = 1; i < entries.Count; i++)
            {
                Debug.Assert(entries[(int)i - 1].Count >= entries[(int)i].Count);
            }
        }

        public V? GetIfOverFraction(K key, double fraction)
        {
            MostCommonEntry? m = GetMostCommonEntry(key);
            return m != null && m.Value.Fraction > fraction
                ? m.Value.Value
                : (V?) null;
        }

        public string ShowEntries(K key)
        {
            List<Entry>/*?*/ entries = Util.Get(inner, key);
            return entries == null
                ? "no entries"
                : string.Join(", ", from entry in entries select $"{entry.Count} of {entry.Value}");
        }
    }

    internal static class Util
    {
        internal static uint Sum(IEnumerable<uint> e) =>
            e.Aggregate<uint, uint>(0, (uint a, uint b) => a + b);

        internal static void Swap<T>(List<T> l, uint a, uint b)
        {
            T temp = l[(int) a];
            l[(int) a] = l[(int) b];
            l[(int) b] = temp;
        }

        internal static V/*?*/ Get<K, V>(Dictionary<K, V> d, K key) where V : class =>
            d.TryGetValue(key, out V value) ? value : null;

        internal static V GetOrAdd<K, V>(Dictionary<K, V> d, K key, Func<V> getValue)
        {
            if (d.TryGetValue(key, out V value))
            {
                return value;
            }
            else
            {
                V val = getValue();
                d[key] = val;
                return val;
            }
        }
    }
}
