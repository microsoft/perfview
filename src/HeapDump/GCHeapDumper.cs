using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Parsers.JSDumpHeap;
using Microsoft.Diagnostics.Tracing.Session;
using System.Threading.Tasks;
using FastSerialization;
using Graphs;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Samples.Debugging.CorDebug.NativeApi;
using Microsoft.Samples.Debugging.CorMetadata.NativeApi;
using Profiler;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Address = System.UInt64;
using Microsoft.Diagnostics.Utilities;
using Microsoft.Diagnostics.HeapDump;
#if CROSS_GENERATION_LIVENESS
using Microsoft.Diagnostics.CrossGenerationLiveness;
#endif
using System.Linq;

/// <summary>
/// GCHeapDumper contains the transient state that needs to be tracked only WHILE the heap is being dumped.
/// </summary>
public class GCHeapDumper
{
    /// <summary>
    /// Create a dumper.   Add options to it, and then call 'DumpFromLiveHeap' or 'DumpHeapFromProcessDump'
    /// to dump a heap.  
    /// </summary>
    /// <param name="log"></param>
    public GCHeapDumper(TextWriter log)
    {
        m_origLog = log;
        m_copyOfLog = new StringWriter();
        m_log = new TeeTextWriter(m_copyOfLog, m_origLog);

        MaxDumpCountK = 250;
    }

    /// <summary>
    /// Dump from a live process with a given process ID.
    /// </summary>
    public CollectionMetadata DumpLiveHeap(int processID, Stream outputStream)
    {
        m_outputStream = outputStream;
        return DumpLiveHeap(processID);
    }

    /// <summary>
    /// Dump from a live process with a given process ID.
    /// </summary>
    public CollectionMetadata DumpLiveHeap(int processID, string outputFileName)
    {
        m_outputFileName = outputFileName;
        return DumpLiveHeap(processID);
    }

    private CollectionMetadata DumpLiveHeap(int processID)
    {
        m_sw = Stopwatch.StartNew();

        Debug.Assert(m_outputStream != null || m_outputFileName != null);

        // If we are a win8, bring the process out of suspension.   
        ResumeProcessIfNecessary(processID);

        CollectionMetadata collectionMetadata = null;

        if (!CrossGeneration)
        {
            collectionMetadata = CaptureLiveHeapDump(processID);
        }
        else
        {
#if CROSS_GENERATION_LIVENESS
            CrossGenerationLivenessCollector collector = new CrossGenerationLivenessCollector(
                processID,
                GenerationToTrigger,
                PromotedBytesThreshold,
                m_outputFileName,
                this,
                CaptureLiveHeapDump,
                m_log);

            collector.AttachAndExecute();
            collectionMetadata = collector.CollectionMetadata;
#else
            throw new Exception("Cross generation collection is only supported in heap dump EXE.");
#endif

        }

        return collectionMetadata;
    }

    private CollectionMetadata CaptureLiveHeapDump(int processID)
    {
        m_gcHeapDump = new GCHeapDump((MemoryGraph)null);

        // There are assumptions that JavaScript is first (CCW nodes, and aggregate stats)
        bool hasDotNet = false;
        bool hasJScript = false;
        bool hasCoreClr = false;
        bool hasSilverlight = false;
        bool hasClrDll = false;
        bool hasMrt = false;

        using (var process = Process.GetProcessById(processID))
        {
            if (process == null)
            {
                throw new HeapDumpException("Could not find process with ID " + processID, HR.CouldNotFindProcessId);
            }

            foreach (ProcessModule module in process.Modules)
            {
                var fileName = module.FileName;
                if (!fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (fileName.EndsWith("\\clr.dll", StringComparison.OrdinalIgnoreCase))
                {
                    hasDotNet = true;
                    hasClrDll = true;
                }
                else if (fileName.EndsWith("\\coreclr.dll", StringComparison.OrdinalIgnoreCase))
                {
                    if (0 <= fileName.IndexOf("Microsoft Silverlight", StringComparison.OrdinalIgnoreCase))
                    {
                        hasSilverlight = true;
                    }

                    hasCoreClr = true;
                    hasDotNet = true;
                }
                else if (fileName.EndsWith("\\mscorwks.dll", StringComparison.OrdinalIgnoreCase))
                {
                    hasDotNet = true;
                }
                else
                {
                    // Jscript + digit 
                    var index = fileName.IndexOf("\\jscript", 0, StringComparison.OrdinalIgnoreCase);
                    if (0 <= index && index + 12 < fileName.Length && fileName.Length < index + 14 && Char.IsDigit(module.FileName[index + 8]))
                    {
                        hasJScript = true;
                    }

                    // mrt + digit + * + .dll  Project N.  
                    index = fileName.IndexOf("\\mrt", 0, StringComparison.OrdinalIgnoreCase);
                    if (0 <= index && index + 8 < fileName.Length && fileName.Length < index + 17 && Char.IsDigit(module.FileName[index + 4]))
                    {
                        hasMrt = true;
                    }
                }
            }
        }

        m_log.WriteLine("Process Has DotNet: {0} Has JScript: {1} Has ClrDll: {2} HasMrt {3} HasCoreClr {4}", hasDotNet, hasJScript, hasClrDll, hasMrt, hasCoreClr);

        if (hasClrDll && hasJScript)
        {
            m_log.WriteLine("[Detected both a JScript and .NET heap, forcing a GC before doing a heap dump.]");
            try
            {
                if (!ForceGC(processID))
                {
                    m_log.WriteLine("[WARNING failed Continuing anyway.]");
                }
            }
            catch (Exception e)
            {
                m_log.WriteLine("[WARNING: ForceGC failed with exception {0}.  Continuing anyway.]", e.Message);
            }
        }

        if (hasJScript)
        {
            TryGetJavaScriptDump(processID);
        }

        string[] configurationDirectories = null;
        bool is64bitSource = false;

        if (hasMrt || (hasCoreClr && !hasSilverlight) || (hasDotNet && UseETW))
        {
            if (hasMrt)
                m_log.WriteLine("Detected a project N application, using ETW heap dump");

            if (hasCoreClr && !hasSilverlight)
                m_log.WriteLine("Detected a project K application, using ETW heap dump");

            // Project N and K Support    
            if (!TryGetDotNetDumpETW(processID))
                throw new ApplicationException("Could not get .NET Heap Dump.");
        }
        else if (hasDotNet)
        {
            if (!TryGetDotNetDump(processID, out int pointerSize, out configurationDirectories))
                throw new ApplicationException("Could not get .NET Heap Dump.");

            is64bitSource = pointerSize == 8;
        }

        m_log.WriteLine("Creating a GC Dump from a liver process {0}", processID);
        WriteData(logLiveStats: true);

        var collectionMetadata = new CollectionMetadata()
        {
            Source = TargetSource.LiveProcess,
            Is64BitSource = is64bitSource,
            ConfigurationDirectories = configurationDirectories
        };

        return collectionMetadata;
    }

    /// <summary>
    /// Dump from a process memory dump 
    /// </summary>
    public CollectionMetadata DumpHeapFromProcessDump(string processDumpFile, string outputFileName)
    {
        m_outputFileName = outputFileName;
        return DumpHeapFromProcessDump(processDumpFile);
    }

    /// <summary>
    /// Dump from a process memory dump 
    /// </summary>
    public CollectionMetadata DumpHeapFromProcessDump(string processDumpFile, Stream outputStream)
    {
        m_outputStream = outputStream;
        return DumpHeapFromProcessDump(processDumpFile);
    }

    /// <summary>
    /// Dump from a process memory dump 
    /// </summary>
    private CollectionMetadata DumpHeapFromProcessDump(string processDumpFile)
    {
        m_sw = Stopwatch.StartNew();
        m_gcHeapDump = new GCHeapDump((MemoryGraph)null);

        using (DataTarget dataTarget = InitializeClrRuntime(processDumpFile, -1, out ClrRuntime[] runtimes))
        {
            m_log.WriteLine("Creating a GC Dump from the dump file {0}", processDumpFile);
            ICorDebugProcess proc = null;
            try
            {
                m_log.WriteLine("Trying to get a ICorDebugProcess object.");
                proc = Profiler.Debugger.GetDebuggerHandleFromProcessDump(processDumpFile, 0L);
            }
            catch (Exception e)
            {
                m_log.WriteLine("Warning: Failed to get a V4.0 debugger Message: {0}", e.Message);
                m_log.WriteLine("Continuing with less accurate GC root information.");
            }

            var collectionMetadata = new CollectionMetadata()
            {
                Source = TargetSource.MiniDumpFile,
                Is64BitSource = dataTarget.DataReader.PointerSize == 8,
                ConfigurationDirectories = GetConfigurationDirectoryPaths(runtimes).ToArray()
            };

            DumpDotNetHeapData(dataTarget, runtimes);
            WriteData(logLiveStats: false);
            return collectionMetadata;
        }
    }

    private DataTarget InitializeClrRuntime(string processDumpFile, int processID, out ClrRuntime[] result)
    {
        List<ClrRuntime> runtimes = new List<ClrRuntime>();

        DataTarget dataTarget = null;

        if (string.IsNullOrWhiteSpace(processDumpFile))
        {
            try
            {
                dataTarget = DataTarget.CreateSnapshotAndAttach(processID);
            }
            catch
            {
                dataTarget = DataTarget.AttachToProcess(processID, Freeze);
            }
        }
        else
        {
            dataTarget = DataTarget.LoadDump(processDumpFile);
        }

        if (dataTarget.DataReader.PointerSize != IntPtr.Size)
        {
            if (IntPtr.Size == 8)
            {
                throw new HeapDumpException("Opening a 32 bit dump in a 64 bit process.", HR.Opening32BitDumpIn64BitProcess);
            }
            else
            {
                throw new HeapDumpException("Opening a 64 bit dump in a 32 bit process.", HR.Opening64BitDumpIn32BitProcess);
            }
        }

        if (dataTarget.ClrVersions.Length == 0)
        {
            throw new HeapDumpException("Could not find a .NET Runtime in the process dump " + processDumpFile, HR.NoDotNetRuntimeFound);
        }

        m_log.WriteLine("Enumerating over {0} detected runtimes...", dataTarget.ClrVersions.Length);
        var symbolReader = new SymbolReader(m_log, null);
        if (symbolReader.SymbolPath.Length == 0)
            symbolReader.SymbolPath = SymbolPath.MicrosoftSymbolServerPath;

        foreach (ClrInfo clr in dataTarget.ClrVersions)
        {
            m_log.WriteLine("Creating Runtime access object for runtime {0}.", clr.Version);

            try
            {
                runtimes.Add(clr.CreateRuntime());
            }
            catch (InvalidDataException ex)
            {
                m_log.WriteLine(ex.Message);
            }
            catch (NotSupportedException ex)
            {
                m_log.WriteLine(ex.Message);
            }
        }

        if (runtimes.Count == 0)
            throw new HeapDumpException("Could not open DAC", HR.CouldNotAccessDac);

        result = runtimes.ToArray();
        return dataTarget;
    }

    /// <summary>
    /// For JavaScript heap dumps also generate the ETL file that represents the dump.  
    /// </summary>
    public bool SaveETL;

    // If true forces the use of ETW to collect the heap (only matters for .NET Post V4.5).  
    public bool UseETW;

    /// <summary>
    /// Should we freeze the process during the dump
    /// </summary>
    public bool Freeze;
    /// <summary>
    /// Should we also dump the data associated with the objects
    /// TODO current does nothing. 
    /// </summary>
    public bool DumpData;
    /// <summary>
    /// The maximum number of heap objects to dump(in kiloObjects).  Above this number we start sampling to keep
    /// file size and viewer processing time under control.  
    /// </summary>
    public int MaxDumpCountK;

    /// <summary>
    /// This number tells us when to stop even looking at the heap (otherwise we do make a graph and then sample from that)
    /// </summary>
    public int MaxNodeCountK;

    /// <summary>
    /// True iff we are going to perform a cross-generation reference data collection.
    /// </summary>
    public bool CrossGeneration;

    /// <summary>
    /// The generation to trigger a dump on if this is a cross-generation collection.
    /// </summary>
    public int GenerationToTrigger;

    /// <summary>
    /// The threshold at which we want to dump the heap when collecting cross-generation liveness data.
    /// </summary>
    public ulong PromotedBytesThreshold;

    /// <summary>
    /// Force a .NET GC on a particular process. 
    /// </summary>
    public bool ForceGC(int processID)
    {
        var sw = Stopwatch.StartNew();
        DateTime startTime = DateTime.Now;
        bool success = false;

        if (!(TraceEventSession.IsElevated() ?? false))
        {
            throw new ApplicationException("Must be Administrator to use the ForceGC option.");
        }

        // If we are a win8 app make sure we are not suspended.  
        ResumeProcessIfNecessary(processID);

        // Try to attach the .NET Profiler
        bool loadedClrProfiler = LoadETWClrProfiler(processID, sw);

        // Start up ETW providers and trigger GCs.  
        bool dotNetHeapExists = loadedClrProfiler;
        bool jsHeapExists = false;
        int jsGCs = 0;
        int dotNetGCs = 0;
        bool listening = false;
        string sessionName = "PerfViewGCHeapSession";
        TraceEventSession session = null;
        ETWTraceEventSource source = null;
        // Set up a separate thread that will listen for ETW events coming back telling us we succeeded. 
        long lastDotNetSurvived = 0;
        long curDotNetSurvived = 0;
        int dotNetGCCount = 0;
        double firstJSGCCompleteTime = 0;
        var readerTask = Task.Factory.StartNew(delegate
        {
            using (session = new TraceEventSession(sessionName, null))
            {
                using (source = new ETWTraceEventSource(sessionName, TraceEventSourceType.Session))
                {
                    source.Clr.GCHeapStats += delegate (GCHeapStatsTraceData data)
                    {
                        if (data.ProcessID == processID)
                        {
                            dotNetGCCount++;
                            lastDotNetSurvived = curDotNetSurvived;
                            curDotNetSurvived = data.TotalPromotedSize0 + data.TotalPromotedSize1 + data.TotalPromotedSize2 + data.TotalPromotedSize3 + data.TotalPromotedSize4;
                            m_log.WriteLine("{0,5:n1}s: .NET GC stats, at {1:n2}s Survived {2}.", sw.Elapsed.TotalSeconds, (data.TimeStamp - startTime).TotalSeconds, curDotNetSurvived);
                        }
                    };

                    // Set up the JScript heap listener
                    var etwJSParser = new JSDumpHeapTraceEventParser(source);
                    etwJSParser.JSDumpHeapEnvelopeStop += delegate (SummaryTraceData data)
                    {
                        if (data.ProcessID == processID)
                        {
                            m_log.WriteLine("{0,5:n1}s: JavaScript GC Complete at {1:n2}s", sw.Elapsed.TotalSeconds, (data.TimeStamp - startTime).TotalSeconds);
                            if (jsGCs == 0)
                            {
                                firstJSGCCompleteTime = sw.Elapsed.TotalSeconds;
                            }

                            jsGCs++;
                        }
                    };
                    etwJSParser.JSDumpHeapEnvelopeStart += delegate (SettingsTraceData data)
                    {
                        if (data.ProcessID == processID)
                        {
                            m_log.WriteLine("{0,5:n1}s: JavaScript GC Started at {1:n2}s.", sw.Elapsed.TotalSeconds, (data.TimeStamp - startTime).TotalSeconds);
                            jsHeapExists = true;
                        }
                    };
                    TimeSpan lastJSUpdate = sw.Elapsed;
                    etwJSParser.JSDumpHeapBulkEdge += delegate (BulkEdgeTraceData data)
                    {
                        if (data.ProcessID == processID)
                        {
                            if ((sw.Elapsed - lastJSUpdate).TotalMilliseconds > 500)
                            {
                                m_log.WriteLine("{0,5:n1}s: Making JS GC Heap Progress...", sw.Elapsed.TotalSeconds);
                            }

                            lastJSUpdate = sw.Elapsed;
                        }
                    };

                    // Set up the .NET heap listener
                    var etwClrParser = new ETWClrProfilerTraceEventParser(source);
                    etwClrParser.CaptureStateStop += delegate (EmptyTraceData data)
                    {
                        if (data.ProcessID == processID)
                        {
                            m_log.WriteLine("{0,5:n1}s: .NET GC complete at {1:n2}s.", sw.Elapsed.TotalSeconds, (data.TimeStamp - startTime).TotalSeconds);
                            dotNetGCs++;
                        }
                    };

                    etwClrParser.CaptureStateStart += delegate (EmptyTraceData data)
                    {
                        if (data.ProcessID == processID)
                        {
                            m_log.WriteLine("{0,5:n1}s: .NET GC Starting at {1:n2}s.", sw.Elapsed.TotalSeconds, (data.TimeStamp - startTime).TotalSeconds);
                            dotNetHeapExists = true;
                        }
                    };

                    m_log.WriteLine("{0,5:n1}s: Enabling JScript Heap Provider", sw.Elapsed.TotalSeconds);
                    session.EnableProvider(JSDumpHeapTraceEventParser.ProviderGuid, TraceEventLevel.Informational,
                        (ulong)JSDumpHeapTraceEventParser.Keywords.jsdumpheap);

                    m_log.WriteLine("{0,5:n1}s: Enabling EtwClrProfiler", sw.Elapsed.TotalSeconds);
                    session.EnableProvider(ETWClrProfilerTraceEventParser.ProviderGuid, TraceEventLevel.Informational,
                        (long)ETWClrProfilerTraceEventParser.Keywords.GCHeap);

                    m_log.WriteLine("{0,5:n1}s: Enabling CLR GC events", sw.Elapsed.TotalSeconds);
                    session.EnableProvider(ClrTraceEventParser.ProviderGuid, TraceEventLevel.Informational,
                        (long)(ClrTraceEventParser.Keywords.GC | ClrTraceEventParser.Keywords.GCHeapSurvivalAndMovement));

                    listening = true;
                    source.Process();
                    m_log.WriteLine("{0,5:n1}s: ETW Listener dieing", sw.Elapsed.TotalSeconds);
                }
            }
        });

        // Wait for thread above to start listening (should be very fast)
        while (!listening)
        {
            Thread.Sleep(1);
        }

        Debug.Assert(session != null);

        // Start the providers and trigger the GCs.  
        // Note that because the ETW events are all triggered by a single thread, the 
        // GCs are guaranteed to be serialized (first the WHOLE JScript GC then the WHOLE .NET GC).
        int gcsTriggered = 1;
        TriggerAllGCs(session, sw, processID);
        double lastStatusUpdate = 0;
        for (; ; )
        {
            Thread.Sleep(100);
            if (sw.Elapsed.TotalSeconds > 60)
            {
                m_log.WriteLine("{0,5:n1}s: Timed out after 60 seconds, GCs done but dead loops between .NET and JS heap may still exist.", sw.Elapsed.TotalSeconds);
                break;
            }

            if (sw.Elapsed.TotalSeconds - lastStatusUpdate > 10)
            {
                m_log.WriteLine("{0,5:n1}s: Waiting for reply", sw.Elapsed.TotalSeconds);
                lastStatusUpdate = sw.Elapsed.TotalSeconds;
            }

            // If we have not received either reply, then continue waiting. 
            if (!jsHeapExists && !dotNetHeapExists)
            {
                continue;
            }

            if (jsHeapExists)
            {
                // If we see a JScript GC, the .NET GC is stalled waiting for it, so wait for it to complete
                if (jsGCs == 0)
                {
                    continue;
                }

                Debug.Assert(firstJSGCCompleteTime > 0);
                // If we did not start the .NET GC, wait at least 1.1 seconds for it to start before giving up on .NET 
                if (!dotNetHeapExists && sw.Elapsed.TotalSeconds - firstJSGCCompleteTime < 1.1)
                {
                    continue;
                }
            }

            // OK at this point we think that dotNetHeapExists and jsHeapExists are accurate.  

            if (dotNetGCs > 0 && !jsHeapExists)
            {
                m_log.WriteLine("{0,5:n1}s: Triggered .NET GC,  No JScript heap detected", sw.Elapsed.TotalSeconds);
                success = true;
                break;
            }

            if (jsGCs > 0 && !dotNetHeapExists)
            {
                m_log.WriteLine("{0,5:n1}s: Triggered JScript GC,  No .NET heap detected", sw.Elapsed.TotalSeconds);
                success = true;
                break;
            }

            if (jsHeapExists && dotNetHeapExists)
            {
                if (gcsTriggered == 1)
                {
                    m_log.WriteLine("{0,5:n1}s: Detected .NET and JS heap, triggering two more GCs", sw.Elapsed.TotalSeconds);
                    TriggerAllGCs(session, sw, processID);
                    TriggerAllGCs(session, sw, processID);
                    gcsTriggered += 2;
                }

                if (gcsTriggered > 15)
                {
                    m_log.WriteLine("{0,5:n1}s: Triggered 15 GCs, giving up trying to converge.", sw.Elapsed.TotalSeconds);
                    success = true;
                    break;
                }

                if (dotNetGCs == gcsTriggered)
                {
                    if (lastDotNetSurvived == curDotNetSurvived)
                    {
                        m_log.WriteLine("{0,5:n1}s: No promoted object on the {1} .NET GC.  SUCCESS!", sw.Elapsed.TotalSeconds, dotNetGCs);
                        success = true;
                        break;
                    }
                    else
                    {
                        m_log.WriteLine("{0,5:n1}s: .NET promoted {1} != {2} prev Promoted, doing another GC", sw.Elapsed.TotalSeconds, curDotNetSurvived, lastDotNetSurvived);
                        TriggerAllGCs(session, sw, processID);
                        gcsTriggered++;
                    }
                }
            }
        }

        // Unload the ETWClrProfiler 
        m_log.WriteLine("{0,5:n1}s: Requesting ETWClrProfiler unload.", sw.Elapsed.TotalSeconds);
        session.CaptureState(ETWClrProfilerTraceEventParser.ProviderGuid, (long)(ETWClrProfilerTraceEventParser.Keywords.Detach));

        // Stop our listener.  
        if (source != null)
        {
            source.StopProcessing();
        }

        // Stop the ETW providers
        m_log.WriteLine("{0,5:n1}s: Shutting down ETW session", sw.Elapsed.TotalSeconds);
        session.DisableProvider(JSDumpHeapTraceEventParser.ProviderGuid);
        session.DisableProvider(ETWClrProfilerTraceEventParser.ProviderGuid);

        m_log.WriteLine("[{0,5:n1}s: Done forcing GCs success={1}]", sw.Elapsed.TotalSeconds, success);
        return success;
    }

    private void TriggerAllGCs(TraceEventSession session, Stopwatch sw, int processID)
    {
        m_log.WriteLine("{0,5:n1}s: Requesting a JScript GC", sw.Elapsed.TotalSeconds);
        session.CaptureState(JSDumpHeapTraceEventParser.ProviderGuid,
            (ulong)JSDumpHeapTraceEventParser.Keywords.jsdumpheap);

        m_log.WriteLine("{0,5:n1}s: Requesting a DotNet GC", sw.Elapsed.TotalSeconds);
        session.CaptureState(ETWClrProfilerTraceEventParser.ProviderGuid,
            (long)(ETWClrProfilerTraceEventParser.Keywords.GCHeap));

        m_log.WriteLine("{0,5:n1}s: Requesting .NET Native GC", sw.Elapsed.TotalSeconds);
        try
        {
            session.CaptureState(ClrTraceEventParser.NativeProviderGuid,
                (long)(ClrTraceEventParser.Keywords.GCHeapCollect));
        }
        catch
        {
            m_log.WriteLine("{0,5:n1}s: .NET Native Capture state failed. OK if this is not a .NET Native scenario.", sw.Elapsed.TotalSeconds);
        };

    }

    // output properties
    /// <summary>
    /// The number of bad objects encountered during the dump 
    /// </summary>
    public int BadObjectCount { get; private set; }

    #region private

    /// <summary>
    /// Gets the list of directories containing the app domain config files for the runtime
    /// </summary>
    private IEnumerable<string> GetConfigurationDirectoryPaths(ClrRuntime[] runtimes) => runtimes.SelectMany(r => r.AppDomains).Select(r => r.ConfigurationFile).Where(cf => !string.IsNullOrWhiteSpace(cf)).Select(cf => Path.GetDirectoryName(cf));

    /// <summary>
    /// Make sure that the given process is not suspended.  
    /// </summary>
    private void ResumeProcessIfNecessary(int processID)
    {
        using (Process process = Process.GetProcessById(processID))
        {
            if (process == null)
            {
                throw new HeapDumpException("Could not find process with ID " + processID, HR.CouldNotFindProcessId);
            }

            // Determine if we are a Win8 Application.  
            var fullPackageName = PackageUtil.FullPackageNameForProcess(process);
            if (fullPackageName != null)
            {
                m_log.WriteLine("Process {0} is a Windows 8 application, resuming that process.", processID);
                var pkgDebugSettings = (IPackageDebugSettings)new PackageDebugSettingsClass();
                // pkgDebugSettings.EnableDebugging(fullPackageName, null, IntPtr.Zero);
                pkgDebugSettings.Resume(fullPackageName);
            }
        }
    }

    /// <summary>
    /// Loads the ETWClrProfiler into the process 'processID'.   
    /// </summary>
    private bool LoadETWClrProfiler(int processID, Stopwatch sw)
    {
        m_log.WriteLine("Loading the ETWClrProfiler.");
        m_log.WriteLine("Turning on debug privilege.");
        TraceEventSession.SetDebugPrivilege();

        CLRMetaHost mh = new CLRMetaHost();
        CLRRuntimeInfo highestLoadedRuntime = null;
        foreach (CLRRuntimeInfo runtime in mh.EnumerateLoadedRuntimes(processID))
        {
            if (highestLoadedRuntime == null ||
                string.Compare(highestLoadedRuntime.GetVersionString(), runtime.GetVersionString(), StringComparison.OrdinalIgnoreCase) < 0)
            {
                highestLoadedRuntime = runtime;
            }
        }
        if (highestLoadedRuntime == null)
        {
            m_log.WriteLine("Could not enumerate .NET runtimes on the system.");
            return false;
        }

        var version = highestLoadedRuntime.GetVersionString();
        m_log.WriteLine("Highest Runtime in process is version {0}", version);
        if (version.StartsWith("v2"))
        {
            throw new ApplicationException("Object logging only supported on V4.0 .NET runtimes.");
        }

        ICLRProfiling clrProfiler = highestLoadedRuntime.GetProfilingInterface();
        if (clrProfiler == null)
        {
            throw new ApplicationException("Could not get Attach Profiler interface (target runtime must be at least V4.0))");
        }

        string myPath = Assembly.GetExecutingAssembly().ManifestModule.FullyQualifiedName;
        string myDir = Path.GetDirectoryName(myPath);
        string EtwClrProfilerPath = Path.Combine(myDir, "EtwClrProfiler.dll");

#if DEBUG
        if (!File.Exists(EtwClrProfilerPath))
        {
            var buildPath = Path.Combine(myDir, @"..\..\..\..\ETWClrProfiler\Debug\x86\EtwClrProfiler.dll");
            if (File.Exists(buildPath))
                EtwClrProfilerPath = buildPath;
        }
#endif
        if (!File.Exists(EtwClrProfilerPath))
        {
            throw new ApplicationException("Could not find profiler DLL " + EtwClrProfilerPath);
        }

        // Warn the user to unsuspend win8 apps if 3 seconds goes by 
        bool attached = false;
        ThreadPool.QueueUserWorkItem(delegate
        {
            Thread.Sleep(3000);
            if (!attached)
            {
                m_log.WriteLine("[Can't Attach Yet... Bring Win8 Apps to the forground.]");
                m_log.Flush();
            }
        });

        try
        {
            // Wait 30 seconds because you may have to wake the process for win8 
            m_log.WriteLine("{0,5:n1}s: Trying to attach a profiler.", sw.Elapsed.TotalSeconds);
            // We use the provider guid as the GUID of the COM object for the profiler too. 
            int ret = clrProfiler.AttachProfiler(processID, 30000, ETWClrProfilerTraceEventParser.ProviderGuid, EtwClrProfilerPath, IntPtr.Zero, 0);
            attached = true;
            m_log.WriteLine("{0,5:n1}s: Done Attaching ETLClrProfiler ret = {1}", sw.Elapsed.TotalSeconds, ret);
        }
        catch (COMException e)
        {
            if (e.ErrorCode == unchecked((int)0x800705B4))  // Timeout
            {
                throw new ApplicationException("Timeout: For Win8 Apps this may because they were suspended.  Make sure to switch to the app.");
            }
            // TODO Confirm this error code is what I think it is. 
            if (e.ErrorCode == unchecked((int)0x8013136a))
            {
                throw new ApplicationException("A CLR Profiler has already been attached.  You cannot attach another. (a process restart will fix)");
            }

            m_log.WriteLine("Failure attaching profiler, see the Windows Application Event Log for details.");
            throw;
        }

        m_log.WriteLine("Attached ETWClrProfiler.");
        return true;
    }

    /// <summary>
    /// Tries to get a javaScript dump and adds it to m_gcHeapDump.MemoryGraph if present.
    /// returns true if it found data. 
    /// </summary>
    private bool TryGetJavaScriptDump(int processID)
    {
        m_log.WriteLine("*****  Attempting a ETW based JavaScript Heap Dump.");
        m_gcHeapDump.MemoryGraph = new MemoryGraph(10000);     // TODO Can we be more accurate?  
        // m_gotJScriptData = JavaScriptHeapDumper.Dump(processID, m_gcHeapDump.MemoryGraph, m_log);

        if (SaveETL)
        {
            m_log.WriteLine("SaveETL option specified, additionally saving the JS Heap as an ETL file.");
            var etlFileName = Path.ChangeExtension(m_outputFileName, ".jsHeap.etl");
            m_gotJScriptData = JavaScriptHeapDumper.DumpAsEtlFile(processID, etlFileName, m_log, m_gcHeapDump.MemoryGraph);
            m_log.WriteLine("Wrote data to {0}.", etlFileName);
        }
        else
        {
            m_gotJScriptData = JavaScriptHeapDumper.Dump(processID, m_gcHeapDump.MemoryGraph, m_log);
        }

        if (m_gotJScriptData)
        {
            m_log.WriteLine("Finished reading JS Dump {0}, {1} Nodes {2} Types", m_outputFileName,
                    m_gcHeapDump.MemoryGraph.NodeIndexLimit, m_gcHeapDump.MemoryGraph.NodeTypeIndexLimit);

            m_JSRoot = m_gcHeapDump.MemoryGraph.RootIndex;
        }
        else
        {
            m_gcHeapDump.MemoryGraph = null;        // WE null it out so that if we have only a .NET heap we pick a good initial size
        }

        return m_gotJScriptData;
    }

    /// <summary>
    /// Tries to get a DotNet dump and adds it to m_gcHeapDump.MemoryGraph if present.  Uses ETW to do it.  
    /// </summary>
    private bool TryGetDotNetDumpETW(int processID)
    {
        m_log.WriteLine("*****  Attempting a ETW based DotNet Heap Dump.");

        if (m_gcHeapDump.MemoryGraph == null)
        {
            m_gcHeapDump.MemoryGraph = new MemoryGraph(10000);     // TODO Can we be more accurate?  
        }

        m_gcHeapDump.DotNetHeapInfo = new DotNetHeapInfo();

        if (SaveETL)
        {
            m_log.WriteLine("SaveETL option specified, additionally saving the .NET Heap as an ETL file.");
            var etlFileName = Path.ChangeExtension(m_outputFileName, ".gcHeap.etl");
            m_gotDotNetData = DotNetHeapDumper.DumpAsEtlFile(processID, etlFileName, m_log, m_gcHeapDump.MemoryGraph, m_gcHeapDump.DotNetHeapInfo);
            m_log.WriteLine("Wrote data to {0}.", etlFileName);
        }
        else
        {
            m_gotDotNetData = DotNetHeapDumper.Dump(processID, m_gcHeapDump.MemoryGraph, m_log, m_gcHeapDump.DotNetHeapInfo);
        }

        if (m_gotDotNetData)
        {
            m_log.WriteLine("Finished reading .NET Dump {0}, {1} Nodes {2} Types", m_outputFileName,
                    m_gcHeapDump.MemoryGraph.NodeIndexLimit, m_gcHeapDump.MemoryGraph.NodeTypeIndexLimit);

            m_dotNetRoot = m_gcHeapDump.MemoryGraph.RootIndex;
        }
        return m_gotDotNetData;
    }

    private bool TryGetDotNetDump(int processID, out int pointerSize, out string[] configDirectories)
    {
        m_log.WriteLine("*****  Attempting a .NET Heap Dump.");

        m_processID = processID;
        using (DataTarget dataTarget = InitializeClrRuntime(null, processID, out ClrRuntime[] runtimes))
        {
            pointerSize = dataTarget.DataReader.PointerSize;
            configDirectories = GetConfigurationDirectoryPaths(runtimes).ToArray();

            if (dataTarget.ClrVersions.Length == 0)
            {
                // Could not get ClrMD
                m_log.WriteLine("Could not get Desktop .NET Runtime in process with ID {0}", processID);
                return false;
            }

            m_log.WriteLine("Enumerating over {0} detected runtimes...", dataTarget.ClrVersions.Length);

            DumpDotNetHeapData(dataTarget, runtimes);
            m_dotNetRoot = m_gcHeapDump.MemoryGraph.RootIndex;

            return true;
        }
    }

    /// <summary>
    /// Dump a heap associated with 'runtime' to  the m_gcHeapDump.  If 'debugProcess' is non-null use it
    /// to gather symbolic information on the roots.  
    /// 
    /// Dump tries to aggressively Detach from debugProcess (so if we are killed, it does 
    /// not bring down the debuggee).   When we have detached, we also null out debugProcess, 
    /// since it is now pretty useless.   
    /// 
    /// The resulting heap dump is in the m_gcHeapDump.MemoryGraph variable. 
    /// </summary>
    private void DumpDotNetHeapData(DataTarget dataTarget, ClrRuntime[] runtimes)
    {
        // We retry if we run out of memory with smaller MaxNodeCount.  
        for (double retryScale = 1; ; retryScale = retryScale * 1.5)
        {
            try
            {
                var curHeapSize = GC.GetTotalMemory(false);
                m_log.WriteLine("DumpDotNetHeapData: Heap Size {0:n0} MB", curHeapSize / 1000000.0);
                DumpDotNetHeapDataWorker(dataTarget, runtimes, retryScale);
                return;
            }
            catch (OutOfMemoryException e)
            {
                // Give up after trying a few times.  
                if (retryScale > 10)
                {
                    throw;
                }

                foreach (ClrRuntime runtime in runtimes)
                    runtime.FlushCachedData();

                // Keep caching types since it's used in a Dictionary, maybe rethink that in the future
                dataTarget.CacheOptions.CacheTypes = true;
                dataTarget.CacheOptions.CacheMethods = false;
                dataTarget.CacheOptions.CacheFields = false;

                dataTarget.CacheOptions.CacheTypeNames = StringCaching.None;
                dataTarget.CacheOptions.CacheMethodNames = StringCaching.None;
                dataTarget.CacheOptions.CacheFieldNames = StringCaching.None;

                // Thow away the log that we will put into the .gcdump file for this first round. 
                m_copyOfLog = new StringWriter();
                m_log = new TeeTextWriter(m_copyOfLog, m_origLog);

                long beforeGCMemSize = GC.GetTotalMemory(false);
                m_gcHeapDump.MemoryGraph = null;        // Free most of the memory.  
                long afterGCMemSize = GC.GetTotalMemory(true);
                m_log.WriteLine("{0,5:f1}s: WARNING: Hit and Out of Memory Condition, retrying with a smaller MaxObjectCount", m_sw.Elapsed.TotalSeconds);
                m_log.WriteLine("Stack: {0}", e.StackTrace);

                m_log.WriteLine("{0,5:f1}s: Dumper heap usage before {1:n0} MB after {2:n0} MB",
                    m_sw.Elapsed.TotalSeconds, beforeGCMemSize / 1000000.0, afterGCMemSize / 1000000.0);

            }
        }
    }

    private void DumpDotNetHeapDataWorker(DataTarget dataTarget, ClrRuntime[] runtimes, double retryScale)
    {
        IEnumerable<ClrSegment> allSegments = runtimes.SelectMany(r => r.Heap.Segments).OrderBy(r => r.Start);

        m_children = new GrowableArray<NodeIndex>(2000);
        m_graphTypeIdxForArrayType = new Dictionary<string, NodeTypeIndex>(100);
        m_typeIdxToGraphIdx = new GrowableArray<int>();

        m_gotDotNetData = true;
        m_copyOfLog.GetStringBuilder().Length = 0;  // Restart the copy

        m_log.WriteLine("Dumping GC heap, This process is a {0} bit process on a {1} bit OS",
            EnvironmentUtilities.Is64BitProcess ? "64" : "32",
            EnvironmentUtilities.Is64BitOperatingSystem ? "64" : "32");
        m_log.WriteLine("{0,5:f1}s: Starting heap dump {1}", m_sw.Elapsed.TotalSeconds, DateTime.Now);

        ulong totalGCSize = (ulong)allSegments.Sum(s => (long)s.Length);
        if (MaxDumpCountK != 0 && MaxDumpCountK < 10)   // Having fewer than 10K is probably wrong.
            MaxDumpCountK = 10;

        m_log.WriteLine("{0,5:f1}s: Size of heap = {1:f3} GB", m_sw.Elapsed.TotalSeconds, ((double)totalGCSize) / 1000000000.0);

        // We have an overhead of about 52 bytes per object (24 for the hash table, 28 for the rest)
        // we have 1GB in a 32 bit process 
        m_maxNodeCount = 1000000000 / 52;       // 20 Meg objects;
        if (EnvironmentUtilities.Is64BitOperatingSystem)
        {
            m_maxNodeCount *= 3;                // We have 4GB instead of 2GB, so we 3GB instead of 1GB available for us to use in 32 bit processes = 60Meg objects
        }

        // On 64 bit process we are limited by the fact that the graph node is in a MemoryStream and its byte array is limited to 2 gig.  Most objects will
        // be represented by 10 bytes in this array and we round this up to 16 = 128Meg
        if (EnvironmentUtilities.Is64BitProcess)
        {
            m_maxNodeCount = int.MaxValue / 16 - 11;      // Limited to 128Meg objects.  (We are limited by the size of the stream)
            m_log.WriteLine("In a 64 bit process.  Increasing the max node count to {0:f1} Meg", m_maxNodeCount / 1000000.0);
        }
        m_log.WriteLine("Implicitly limit the number of nodes to {0:f1} Meg to avoid arrays that are too large", m_maxNodeCount / 1000000.0);

        // Can force it smaller in case our estimate is not good enough.  
        var explicitMax = MaxNodeCountK * 1000;
        if (0 < explicitMax)
        {
            m_maxNodeCount = Math.Min(m_maxNodeCount, explicitMax);
            m_log.WriteLine("Explicit object count maximum {0:n0}, resulting max {1:n0}", explicitMax, m_maxNodeCount);
        }

        if (retryScale != 1)
        {
            m_maxNodeCount = (int)(m_maxNodeCount / retryScale);
            m_log.WriteLine("We are retrying the dump so we scale the max by {0} to the value {1}", retryScale, m_maxNodeCount);
        }

        // We assume that object on average are 8 object pointers.      
        int estimatedObjectCount = (int)(totalGCSize / ((uint)(8 * IntPtr.Size)));
        m_log.WriteLine("Estimated number of objects = {0:n0}", estimatedObjectCount);

        // We force the node count to be this max node count if we are within a factor of 2.  
        // This ensures that we don't have an issue where growing algorithms overshoot the amount
        // of memory available and fail.   Note we do this on 64 bit too 
        if (estimatedObjectCount >= m_maxNodeCount / 2)
        {
            m_log.WriteLine("Limiting object count to {0:n0}", m_maxNodeCount);
            estimatedObjectCount = m_maxNodeCount + 2;
        }

        // Allocate a memory graph if we have not already.  
        if (m_gcHeapDump.MemoryGraph == null)
        {
            m_gcHeapDump.MemoryGraph = new MemoryGraph(estimatedObjectCount);
        }

        m_gcHeapDump.MemoryGraph.Is64Bit = EnvironmentUtilities.Is64BitProcess;

        ulong total = 0;
        m_log.WriteLine("DumpDotNetHeapDataWorker: Heap Size of dumper {0:n0} MB", GC.GetTotalMemory(false) / 1000000.0);

        int segmentCount = allSegments.Count();
        m_log.WriteLine("A total of {0} segments.", segmentCount);
        // Get the GC Segments to dump
        var gcHeapDumpSegments = new List<GCHeapDumpSegment>(segmentCount);
        foreach (var seg in allSegments)
        {
            var gcHeapDumpSegment = new GCHeapDumpSegment
            {
                Start = seg.Start,
                End = seg.End
            };

            if (seg.IsLargeObjectSegment)
            {
                // Everything is Gen3 (large objects)
                gcHeapDumpSegment.Gen0End = seg.End;
                gcHeapDumpSegment.Gen1End = seg.End;
                gcHeapDumpSegment.Gen2End = seg.End;
                gcHeapDumpSegment.Gen3End = seg.End;
            }
            else
            {
                gcHeapDumpSegment.Gen0End = seg.Generation0.End;
                gcHeapDumpSegment.Gen1End = seg.Generation1.End;
                gcHeapDumpSegment.Gen2End = seg.Generation2.End;
                gcHeapDumpSegment.Gen3End = seg.Start;
            }

            gcHeapDumpSegments.Add(gcHeapDumpSegment);

            total += seg.Length;
            m_log.WriteLine("Segment: Start {0,16:x} Length: {1,16:x} {2,11:n3}M LOH:{3}", seg.Start, seg.Length, seg.Length / 1000000.0, seg.IsLargeObjectSegment);
        }

        m_log.WriteLine("Segment: Total {0,16} Length: {1,16:x} {2,11:n3}M", "", total, total / 1000000.0);

        m_gcHeapDump.InteropInfo = new InteropInfo();
        var dotNetRoot = DumpRoots(dataTarget, runtimes);

        m_log.WriteLine("{0,5:f1}s: Starting GC Graph Traversal.  This can take a while...", m_sw.Elapsed.TotalSeconds);
        double heapTravseralStartSec = m_sw.Elapsed.TotalSeconds;

        // If we are want to dump the whole heap, do it now, this is much more efficient.  
        long startSize = m_gcHeapDump.MemoryGraph.TotalSize;
        DumpAllSegments(dataTarget, runtimes);
        Debug.Assert(m_gcHeapDump.MemoryGraph.TotalSize - startSize < (long)totalGCSize);

        m_log.Write("{0,5:f1}s: Dump RCW/CCW information", m_sw.Elapsed.TotalSeconds);

        try
        {
            DumpCCWRCW(dataTarget);
        }
        catch (Exception e)
        {
            m_log.Write("Error: dumping CCW/RCW information\r\n{0}", e);
            m_gcHeapDump.InteropInfo = new InteropInfo();       // Clear the info
        }

        m_log.WriteLine("{0,5:f1}s: Done collecting data.", m_sw.Elapsed.TotalSeconds);

        var dotNetInfo = m_gcHeapDump.DotNetHeapInfo = new DotNetHeapInfo();

        // Write out the dump (TODO we should do this incrementally).    
        dotNetInfo.SizeOfAllSegments = (long)totalGCSize;
        dotNetInfo.Segments = gcHeapDumpSegments;

        m_gcHeapDump.MemoryGraph.RootIndex = dotNetRoot.Build();

        m_log.WriteLine("Number of bad objects during trace {0:n0}", BadObjectCount);
        m_log.WriteLine("{0,5:f1}s: Finished heap dump {1}", m_sw.Elapsed.TotalSeconds, DateTime.Now);
        return;
    }

    private readonly object _sync = new object();
    private MemoryNodeBuilder DumpRoots(DataTarget dataTarget, ClrRuntime[] runtimes)
    {
        int numRoots = 0;
        var dotNetRoot = new MemoryNodeBuilder(m_gcHeapDump.MemoryGraph, "[.NET Roots]");
        try
        {
            m_log.WriteLine("{0,5:f1}s: Scanning Static Variables", m_sw.Elapsed.TotalSeconds);

            foreach (ClrModule module in runtimes.SelectMany(r => r.EnumerateModules()))
            {
                ClrRuntime runtime = module.AppDomain.Runtime;

                foreach (var item in module.EnumerateTypeDefToMethodTableMap())
                {
                    ClrType type = runtime.GetTypeByMethodTable(item.MethodTable);
                    if (type is null)
                        continue;

                    foreach (ClrStaticField field in type.StaticFields.Where(sf => sf.IsObjectReference))
                    {
                        foreach (ClrAppDomain domain in runtime.AppDomains)
                        {
                            ClrObject obj = field.ReadObject(domain);

                            // Only report objects if they contain pointers (and therefore are interesting roots) or are large in size.
                            if (obj.IsValid && (obj.Type.ContainsPointers || obj.Size > 0x1000))
                            {
                                string name = $"static var {field.ContainingType?.Name}.{field.Name}";
                                ComCallableWrapper ccwInfo = obj.HasComCallableWrapper ? obj.GetComCallableWrapper() : null;

                                // We will use -1 to mean "static variable".
                                WriteRoot(dataTarget.DataReader, dotNetRoot, obj, (ClrRootKind)(-1), false, ccwInfo, name, ref numRoots);
                            }
                        }
                    }
                }
            }



            m_log.WriteLine("{0,5:f1}s: Scanning Actual GC roots", m_sw.Elapsed.TotalSeconds);
            var rootsStartTimeMSec = m_sw.Elapsed.TotalMilliseconds;
            foreach (IClrRoot root in runtimes.SelectMany(r => r.Heap.EnumerateRoots()))
            {
                if (!root.Object.IsValid)
                    continue;

                ClrObject obj = root.Object;
                ClrRootKind kind = root.RootKind;
                bool pinned = root.IsPinned;
                ComCallableWrapper ccwInfo = obj.HasComCallableWrapper ? obj.GetComCallableWrapper() : null;

                string name;
                switch (kind)
                {
                    case ClrRootKind.Stack:
                        name = "local vars";
                        break;

                    case ClrRootKind.RefCountedHandle:
                        name = "COM/WinRT Objects";
                        break;

                    default:
                        name = kind.ToString();
                        break;
                };

                WriteRoot(dataTarget.DataReader, dotNetRoot, obj, kind, pinned, ccwInfo, name, ref numRoots);
            }

            var rootDuration = m_sw.Elapsed.TotalMilliseconds - rootsStartTimeMSec;
            m_log.WriteLine("Scanning UNNAMED GC roots took {0:n1} msec", rootDuration);
        }
        catch (Exception e) when (!(e is OutOfMemoryException))
        {
            m_log.WriteLine("[ERROR while processing roots: {0}", e.Message);
            m_log.WriteLine("Continuing without complete root information");
        }
        m_log.Flush();

        return dotNetRoot;
    }

    private void WriteRoot(IDataReader reader, MemoryNodeBuilder dotNetRoot, ClrObject obj, ClrRootKind kind, bool pinned, ComCallableWrapper ccwInfo, string name, ref int numRoots)
    {
        // If there is a named root already then we assume that that root is the interesting one and we drop this one.  
        if (m_gcHeapDump.MemoryGraph.IsInGraph(obj))
            return;

        numRoots++;
        if (numRoots % 1024 == 0)
            m_log.WriteLine("{0,5:f1}s: Scanned {1} roots.", m_sw.Elapsed.TotalSeconds, numRoots);

        MemoryNodeBuilder nodeToAddRootTo = dotNetRoot;

        if (ccwInfo != null)
        {
            ulong comPtr = ccwInfo.IUnknown != 0 ? ccwInfo.IUnknown : ccwInfo.Interfaces.FirstOrDefault().InterfacePointer;

            // Create a CCW node that represents the COM object that has one child that points at the managed object.  
            var ccwNode = m_gcHeapDump.MemoryGraph.GetNodeIndex(ccwInfo.Handle);

            string typeName = $"[CCW for {obj.Type?.Name ?? "unknown"} RefCnt: {ccwInfo.RefCount:n0}]";
            var ccwTypeIndex = GetTypeIndexForName(typeName, null, 200);

            NodeIndex childNode = m_gcHeapDump.MemoryGraph.GetNodeIndex(obj);

            DumpCCW(reader, childNode, obj, ccwInfo);

            GrowableArray<NodeIndex> ccwChildren = new GrowableArray<NodeIndex>();
            ccwChildren.Add(childNode);

            if (comPtr != 0)
                m_gcHeapDump.MemoryGraph.SetNode(ccwNode, ccwTypeIndex, 200, ccwChildren);

            nodeToAddRootTo = nodeToAddRootTo.FindOrCreateChild("[COM/WinRT Objects]");
            nodeToAddRootTo.AddChild(ccwNode);
        }
        else
        {
            if (kind == (ClrRootKind)(-1))
                nodeToAddRootTo = nodeToAddRootTo.FindOrCreateChild("[static vars]");

            // Add pinned local vars to their own node
            if (pinned && kind == ClrRootKind.Stack)
                nodeToAddRootTo = nodeToAddRootTo.FindOrCreateChild("[Pinned local vars]");
            else
                nodeToAddRootTo = nodeToAddRootTo.FindOrCreateChild("[" + name + "]");

            NodeIndex child = m_gcHeapDump.MemoryGraph.GetNodeIndex(obj);
            nodeToAddRootTo.AddChild(child);
        }
    }

    /// <summary>
    /// Writes the data in the m_gcHeapDump to 'm_outputFileName'
    /// </summary>
    private void WriteData(bool logLiveStats)
    {
        if (!m_gotDotNetData && !m_gotJScriptData)
        {
            throw new HeapDumpException("Could not dump either a .NET or JavaScript Heap.  See log file for details", HR.NoHeapFound);
        }

        if (m_dotNetRoot != NodeIndex.Invalid && m_JSRoot != NodeIndex.Invalid)
        {
            var rootNode = new MemoryNodeBuilder(m_gcHeapDump.MemoryGraph, "[GC Heaps]");
            rootNode.AddChild(m_JSRoot);
            rootNode.AddChild(m_dotNetRoot);
            m_gcHeapDump.MemoryGraph.RootIndex = rootNode.Build();
        }

        // Allow reading.  
        m_gcHeapDump.MemoryGraph.AllowReading();

        var maxDumpCount = MaxDumpCountK * 1000;
        if (maxDumpCount != 0 && maxDumpCount < m_gcHeapDump.MemoryGraph.NodeCount)
        {
            m_log.WriteLine("Object count {0}K > MaxDumpCount = {1}K, sampling", m_gcHeapDump.MemoryGraph.NodeCount / 1000, MaxDumpCountK);

            m_log.WriteLine("{0,5:f1}s:   Started Sampling.", m_sw.Elapsed.TotalSeconds);
            var graphSampler = new GraphSampler(m_gcHeapDump.MemoryGraph, maxDumpCount, m_log);
            var sampledGraph = graphSampler.GetSampledGraph();
            m_log.WriteLine("{0,5:f1}s:   Done Sampling.", m_sw.Elapsed.TotalSeconds);

            m_gcHeapDump.CountMultipliersByType = graphSampler.CountScalingByType;
            m_gcHeapDump.AverageCountMultiplier = (float)((double)m_gcHeapDump.MemoryGraph.NodeCount / sampledGraph.NodeCount);
            m_gcHeapDump.AverageSizeMultiplier = (float)((double)m_gcHeapDump.MemoryGraph.TotalSize / sampledGraph.TotalSize);
            m_log.WriteLine("Average Count Multiplier: {0,6:f2}", m_gcHeapDump.AverageCountMultiplier);
            m_log.WriteLine("Average Size Multiplier:  {0,6:f2}", m_gcHeapDump.AverageSizeMultiplier);

            m_gcHeapDump.MemoryGraph = sampledGraph;
            m_log.WriteLine("After sampling Object Count {0}K    Total GC Heap Size {1:f1} MB ",
                m_gcHeapDump.MemoryGraph.NodeCount, m_gcHeapDump.MemoryGraph.TotalSize / 1000000.0);
        }
        else
        {
            m_log.WriteLine("Object count {0}K less than {1}K, Dumped all objects.", m_gcHeapDump.MemoryGraph.NodeCount / 1000, MaxDumpCountK);
        }

        if (logLiveStats)
        {
            m_log.WriteLine("Dump Created from a live process.");
            m_gcHeapDump.TimeCollected = DateTime.Now;
            m_gcHeapDump.MachineName = Environment.MachineName;
            m_gcHeapDump.ProcessID = m_processID;
            m_log.WriteLine("Dumped process ID {0} on {1} at {2}",
                m_gcHeapDump.ProcessID, m_gcHeapDump.MachineName, m_gcHeapDump.TimeCollected);
            try
            {
                var process = System.Diagnostics.Process.GetProcessById(m_processID);
                m_gcHeapDump.ProcessName = process.ProcessName;
                m_gcHeapDump.TotalProcessCommit = process.VirtualMemorySize64;
                m_gcHeapDump.TotalProcessWorkingSet = process.WorkingSet64;
                m_log.WriteLine("Dumped process {0} ID {1} TotalProcessCommit {2:n0} MB, TotalProcessWorkingSet {3:n0} MB",
                    m_gcHeapDump.ProcessName, m_gcHeapDump.ProcessID,
                    m_gcHeapDump.TotalProcessCommit / 1000000, m_gcHeapDump.TotalProcessWorkingSet / 1000000);

                m_log.WriteLine("Total GC Size = {0:n0} = {1:n2} % of total working set",
                m_gcHeapDump.MemoryGraph.TotalSize / 1000000,
                m_gcHeapDump.MemoryGraph.TotalSize * 100.0 / m_gcHeapDump.TotalProcessWorkingSet);
            }
            catch (Exception) { }
        }
        else
        {
            m_log.WriteLine("Dump Created from a .DMP file, no live statistics");
        }


        // This code always matches the bitness of the process being dumped.  
        Debug.Assert(EnvironmentUtilities.Is64BitProcess == m_gcHeapDump.MemoryGraph.Is64Bit);
        m_log.WriteLine("The process being dumped is {0} Bit", m_gcHeapDump.MemoryGraph.Is64Bit ? 64 : 32);

        m_log.WriteLine("Actual number of objects dumped = {0:n0}", m_gcHeapDump.MemoryGraph.NodeCount);
        m_log.WriteLine("Actual number of types = {0:n0}", (int)m_gcHeapDump.MemoryGraph.NodeTypeIndexLimit);
        m_log.WriteLine("Size of dumped objects {0:n1} MB", m_gcHeapDump.MemoryGraph.TotalSize / 1000000.0);

        // Attach a copy of the log to the dump file.  
        m_log.Flush();
        m_gcHeapDump.CollectionLog = m_copyOfLog.ToString();

        if (m_outputFileName != null)
        {
            m_log.WriteLine("{0,5:f1}s:   Started Writing to file.", m_sw.Elapsed.TotalSeconds);
            var serializer = new Serializer(new IOStreamStreamWriter(m_outputFileName, config: new SerializationConfiguration() { StreamLabelWidth = StreamLabelWidth.FourBytes }), m_gcHeapDump);
            serializer.Close();

            m_log.WriteLine("Actual file size = {0:f3}MB", new FileInfo(m_outputFileName).Length / 1000000.0);
            m_log.WriteLine("[{0,5:f1}s:   Heap Dump complete: {1}]", m_sw.Elapsed.TotalSeconds, m_outputFileName);
        }
        if (m_outputStream != null)
        {
            m_log.WriteLine("{0,5:f1}s:   Started Writing to stream.", m_sw.Elapsed.TotalSeconds);
            var serializer = new Serializer(new IOStreamStreamWriter(m_outputStream, config: new SerializationConfiguration() { StreamLabelWidth = StreamLabelWidth.FourBytes }), m_gcHeapDump);
            serializer.Close();
        }

        m_copyOfLog.GetStringBuilder().Length = 0;
#if false // TODO FIX NOW remove
        using (StreamWriter writer = File.CreateText(Path.ChangeExtension(m_outputFileName, ".heapGraph.xml")))
        {
            m_gcHeapDump.MemoryGraph.DumpNormalized(writer);
        }
        using (StreamWriter writer = File.CreateText(Path.ChangeExtension(m_outputFileName, ".rawGraph.xml")))
        {
            m_gcHeapDump.MemoryGraph.WriteXml(writer);
        }
#endif
    }

    private int DumpRCW(IDataReader reader, NodeIndex node, Address addr, RuntimeCallableWrapper rcw)
    {
        try
        {
            InteropInfo.RCWInfo infoRCW = new InteropInfo.RCWInfo();
            infoRCW.node = node;
            infoRCW.refCount = rcw.RefCount;
            infoRCW.addrIUnknown = rcw.IUnknown;
            infoRCW.addrJupiter = rcw.WinRTObject;
            infoRCW.addrVTable = rcw.VTablePointer;
            infoRCW.firstComInf = m_gcHeapDump.InteropInfo.currentInterfaceCount;
            int countInterfaces = DumpInterfaces(reader, rcw.Interfaces, true);
            infoRCW.countComInf = countInterfaces;
            m_gcHeapDump.InteropInfo.AddRCW(infoRCW);
        }
        catch (System.NullReferenceException)
        {
            return 0;
        }

        return 1;
    }

    private void DumpCCW(IDataReader reader, NodeIndex node, Address addr, ComCallableWrapper ccw)
    {
        InteropInfo.CCWInfo infoCCW = new InteropInfo.CCWInfo();
        infoCCW.node = node;
        infoCCW.refCount = ccw.RefCount;
        infoCCW.addrIUnknown = ccw.IUnknown;
        infoCCW.addrHandle = ccw.Handle;
        infoCCW.firstComInf = m_gcHeapDump.InteropInfo.currentInterfaceCount;
        int countInterfaces = DumpInterfaces(reader, ccw.Interfaces, false);
        infoCCW.countComInf = countInterfaces;
        m_gcHeapDump.InteropInfo.AddCCW(infoCCW);
    }

    private int DumpInterfaces(IDataReader reader, IList<ComInterfaceData> infs, bool fRCW)
    {
        int countInterfaces = 0;

        if (infs != null)
        {
            foreach (ComInterfaceData inf in infs)
            {
                InteropInfo.ComInterfaceInfo infoComInterface = new InteropInfo.ComInterfaceInfo();
                infoComInterface.fRCW = fRCW;

                if (fRCW)
                {
                    infoComInterface.owner = m_gcHeapDump.InteropInfo.currentRCWCount;
                }
                else
                {
                    infoComInterface.owner = m_gcHeapDump.InteropInfo.currentCCWCount;
                }

                ClrType t = inf.Type;

                NodeTypeIndex ti = (NodeTypeIndex)(-1);

                if (t != null)
                {
                    ti = GetTypeIndexForClrType(t, 0);
                }

                infoComInterface.typeID = ti;

                ulong vftable = reader.ReadPointer(inf.InterfacePointer);
                ulong ffirst = reader.ReadPointer(vftable);

                infoComInterface.addrFirstVTable = vftable;
                infoComInterface.addrFirstFunc = ffirst;

                m_gcHeapDump.InteropInfo.AddComInterface(infoComInterface);

                countInterfaces++;
            }
        }

        return countInterfaces;
    }

    /// <summary>
    /// Gather information about CCW/RCW, write to m_gcHeapDump.InteropInfo.
    /// </summary>
    private void DumpCCWRCW(DataTarget dataTarget)
    {
        // We need module information to decode virtual function table pointers, and virtual function pointers.
        if (m_gcHeapDump.InteropInfo.InteropInfoExists())
        {
            foreach (ModuleInfo module in dataTarget.EnumerateModules())
            {
                InteropInfo.InteropModuleInfo infoModule = new InteropInfo.InteropModuleInfo();
                infoModule.baseAddress = module.ImageBase;
                infoModule.fileSize = (uint)module.IndexFileSize;
                infoModule.timeStamp = (uint)module.IndexTimeStamp;
                infoModule.fileName = module.FileName;
                m_gcHeapDump.InteropInfo.AddModule(infoModule);
            }
        }
    }

    /// <summary>
    /// DumpAllSegments dumps all the data in the GC heap in bulk (in order).  This means that both live and dead objects
    /// are collected (since we can't tell the difference at this point.  This is much more efficient if you want 
    /// to dump the whole heap.  
    /// </summary>
    private void DumpAllSegments(DataTarget dataTarget, ClrRuntime[] runtimes)
    {
        var segments = runtimes.SelectMany(r => r.Heap.Segments).OrderBy(seg => seg.Start).ToArray();

        m_log.WriteLine("Dumping {0} GC segments in the heap in bulk.", segments.Length);
        var segmentCount = 0;
        foreach (ClrSegment segment in segments)
        {
            var start = segment.Start;
            var end = segment.End;
            m_log.WriteLine("[{0,5:f1}s: Dumping segment {1} of {2} start: {3:x} len: {4:f2}M]", m_sw.Elapsed.TotalSeconds, segmentCount, segments.Length, start, (end - start) / 1000000.0);

            ulong nextStatusUpdateObj = 0;

            foreach (ClrObject obj in segment.EnumerateObjects())
            {
                if (obj.Type is null)
                    continue;

                m_children.Clear();

                foreach (var childObj in obj.EnumerateReferences(carefully: true, considerDependantHandles: true))
                    m_children.Add(m_gcHeapDump.MemoryGraph.GetNodeIndex(childObj));

                var objNodeIdx = m_gcHeapDump.MemoryGraph.GetNodeIndex(obj);
                ulong objSize = obj.Size;
                int objSizeAsInt = objSize <= int.MaxValue ? (int)objSize : int.MaxValue;


                var memoryGraphTypeIdx = GetTypeIndexForClrType(obj.Type, objSizeAsInt);

                RuntimeCallableWrapper rcwData = obj.HasRuntimeCallableWrapper ? obj.GetRuntimeCallableWrapper() : null;
                if (rcwData != null)
                {
                    // Add the COM object this RCW points at as a child of this node.  
                    m_children.Add(m_gcHeapDump.MemoryGraph.GetNodeIndex(rcwData.IUnknown));

                    var fullTypeName = obj.Type.Name;
                    string moduleName = obj.Type.Module?.Name;
                    if (moduleName != null)
                        fullTypeName = Path.GetFileNameWithoutExtension(moduleName) + "!" + fullTypeName;

                    var typeName = $"[RCW {fullTypeName} RefCnt: {rcwData.RefCount:n0}]";

                    // We add 1000 to account for the overhead of the RCW that is NOT on the GC heap.
                    if (objSizeAsInt < int.MaxValue - 1000)
                        objSizeAsInt += 1000;

                    memoryGraphTypeIdx = GetTypeIndexForName(typeName, null, objSizeAsInt);

                    DumpRCW(dataTarget.DataReader, objNodeIdx, obj, rcwData);
                }

                ComCallableWrapper ccwData = obj.HasComCallableWrapper ? obj.GetComCallableWrapper() : null;
                if (ccwData != null)
                    DumpCCW(dataTarget.DataReader, objNodeIdx, obj, ccwData);

                if (obj > nextStatusUpdateObj)
                {
                    m_log.WriteLine("{0,5:f1}s: Dumped {1:n0} objects, max_dump_limit {2:n0} Dumper heap Size {3:n0}MB",
                        m_sw.Elapsed.TotalSeconds, m_gcHeapDump.MemoryGraph.NodeCount, m_maxNodeCount, GC.GetTotalMemory(false) / 1000000.0);
                    nextStatusUpdateObj = obj + 1000000;        // log a message every 1 Meg 
                }

                if (m_gcHeapDump.MemoryGraph.NodeCount >= m_maxNodeCount ||
                    m_gcHeapDump.MemoryGraph.DistinctRefCount + m_children.Count > m_maxNodeCount)
                {
                    m_log.WriteLine("[WARNING, exceeded the maximum number of node allowed {0}]", m_maxNodeCount);
                    m_log.WriteLine("{0,5:f1}s: Truncating heap dump.", m_sw.Elapsed.TotalSeconds);
                    return;
                }

                m_gcHeapDump.MemoryGraph.SetNode(objNodeIdx, memoryGraphTypeIdx, objSizeAsInt, m_children);
            }
            segmentCount++;
        }

        m_log.WriteLine("{0,5:f1}s: Done Dumping all the segments.", m_sw.Elapsed.TotalSeconds);
    }

    /// <summary>
    /// Given a type, find the graph's type index for it.  If this is the first
    /// time we have seen the type we generate a new type for it and return that
    /// We do intern types (same type will return the same type index). 
    /// </summary>
    private NodeTypeIndex GetTypeIndexForClrType(ClrType type, int objSize)
    {
        int idx;
        if (!m_typeTable.TryGetValue(type, out idx))
        {
            idx = m_typeTable.Count;
            m_typeTable[type] = idx;
        }

        if (m_typeIdxToGraphIdx.Count <= idx)
        {
            m_typeIdxToGraphIdx.Count = idx + (m_typeIdxToGraphIdx.Count / 2 + 32);
        }

        // We add 1 so that 0 is an illegal value (to represent 'not present')
        var val = m_typeIdxToGraphIdx[idx] - 1;
        if (val >= 0)
        {
            return (NodeTypeIndex)val;
        }

        // We give more complex names to arrays and strings that are large
        NodeTypeIndex ret;
        var name = type.Name;
        if (type.IsString || type.IsArray || name == "Free")
        {
            var typeName = type.Name;
            var sizeStr = "";
            if (objSize > 1000)
            {
                string size;
                if (objSize < 10000)
                {
                    size = "1K";
                }
                else if (objSize < 100000)
                {
                    size = "10K";
                }
                else if (objSize < 1000000)
                {
                    size = "100K";
                }
                else if (objSize < 10000000)
                {
                    size = "1M";
                }
                else if (objSize < 100000000)
                {
                    size = "10M";
                }
                else
                {
                    size = "100M";
                }

                sizeStr = "Bytes > " + size;
            }

            if (type.IsArray)
            {
                var ptrs = "NoPtrs";
                if (type.ContainsPointers)
                {
                    ptrs = "Ptrs";
                }

                var sep = "";
                if (sizeStr.Length > 0)
                {
                    sep = ",";
                }

                typeName = typeName + " (" + sizeStr + sep + ptrs + ",ElemSize=" + type.ComponentSize.ToString() + ")";
            }
            else if (sizeStr.Length > 0)
            {
                typeName = typeName + " (" + sizeStr + ")";
            }

            ret = GetTypeIndexForName(typeName, type.Module?.Name, 0);
        }
        else
        {
            ret = GetTypeIndexForName(name ?? "<Unnamed " + type.MetadataToken.ToString("x8") + ">", type.Module?.Name, 0);
            m_typeIdxToGraphIdx[idx] = (int)ret + 1;
        }
        return ret;
    }

    private NodeTypeIndex GetTypeIndexForName(string typeName, string moduleName, int defaultSize)
    {
        NodeTypeIndex ret;
        if (!m_graphTypeIdxForArrayType.TryGetValue(typeName, out ret))
        {
            ret = m_gcHeapDump.MemoryGraph.CreateType(typeName, moduleName, defaultSize);
            m_graphTypeIdxForArrayType[typeName] = ret;
        }
        return ret;
    }

    /// <summary>
    /// The debugger has a variety of callbacks.  This class is my 'hook' into these callbacks.  
    /// </summary>
    private class GCHeapDumpDebuggerCallbacks : Profiler.DebuggerCallBacks
    {
        public GCHeapDumpDebuggerCallbacks()
        {
            m_LastCallBackTimeUtc = DateTime.UtcNow;
        }
        public bool WaitForFullAttach(int timeout)
        {
            for (; ; )
            {
                Thread.Sleep(300);
                var waitingMSec = (DateTime.UtcNow - m_LastCallBackTimeUtc).Milliseconds;
                // Console.WriteLine("msec since last callback = {0}", waitingMSec);
                if (waitingMSec > 300)
                {
                    if (m_AssembliesLoaded > 0 && m_ThreadsLoaded > 0)
                    {
                        return true;
                    }

                    if (waitingMSec > timeout)
                    {
                        return false;
                    }
                }
            }
        }

        #region private
        public override void CreateProcess(ICorDebugProcess pProcess)
        {
            Console.WriteLine("CreateProcess");
            m_LastCallBackTimeUtc = DateTime.UtcNow;
            base.CreateProcess(pProcess);
        }
        public override void CreateThread(ICorDebugAppDomain pAppDomain, ICorDebugThread thread)
        {
            Console.WriteLine("CreateThread");
            m_LastCallBackTimeUtc = DateTime.UtcNow;
            m_ThreadsLoaded++;
            base.CreateThread(pAppDomain, thread);
        }
        public override void LoadModule(ICorDebugAppDomain pAppDomain, ICorDebugModule pModule)
        {
            Console.WriteLine("LoadModule");
            m_LastCallBackTimeUtc = DateTime.UtcNow;
            base.LoadModule(pAppDomain, pModule);
        }
        public override void LoadAssembly(ICorDebugAppDomain pAppDomain, ICorDebugAssembly pAssembly)
        {
            Console.WriteLine("LoadAssembly");
            m_LastCallBackTimeUtc = DateTime.UtcNow;
            m_AssembliesLoaded++;
            base.LoadAssembly(pAppDomain, pAssembly);
        }


        private DateTime m_LastCallBackTimeUtc;
        private int m_ThreadsLoaded;
        private int m_AssembliesLoaded;
        #endregion
    }

    private int m_processID;
    private string m_outputFileName;
    private Stream m_outputStream;
    private TextWriter m_origLog;           // What was passed into the constructor.
    private TextWriter m_log;               // Where we send messages
    private StringWriter m_copyOfLog;       // We keep a copy of all logged messages here to append to output file. 
    private Stopwatch m_sw;                 // We keep track of how long it takes.  

    private GCHeapDump m_gcHeapDump;        // The image of what we are putting in the file
    private NodeIndex m_JSRoot = NodeIndex.Invalid;     // The root of the JS heap
    private NodeIndex m_dotNetRoot = NodeIndex.Invalid; // The root of the .NET heap
    private int m_maxNodeCount;             // The maximum node count (to keep from getting out of memory exceptions)

    private bool m_gotJScriptData;          // Did we find a JScript heap?
    private bool m_gotDotNetData;           // Did we find a .NET heap?

    private GrowableArray<NodeIndex> m_children;
    private Dictionary<ClrType, int> m_typeTable = new Dictionary<ClrType, int>();
    private GrowableArray<int> m_typeIdxToGraphIdx;

    private Dictionary<string, NodeTypeIndex> m_graphTypeIdxForArrayType;

    [Conditional("DEBUG")]
    public static void DebugWriteLine(string format, params object[] args)
    {
        //#if DEBUG
#if false
        if (m_debugLog == null)
            m_debugLog = File.CreateText("HeapDumpDebugLog.txt");

        m_debugLog.WriteLine(format, args);
        m_debugLog.Flush();
#endif
    }

#if false
    private static TextWriter m_debugLog;
#endif
    #endregion
}

#region internal classes
// These are not needed in V4.0 but for V2.0 they are not present. 
public delegate void Action<in T1, in T2, in T3, in T4, in T5, in T6>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6);
public delegate void Action<in T1, in T2, in T3, in T4, in T5, in T6, in T7>(T1 arg1, T2 arg2, T3 arg3, T4 arg4, T5 arg5, T6 arg6, T7 arg7);

/// <summary>
/// This is a helper class that knows how to find local variables and static variable names for GC roots. 
/// </summary>
internal static class GCRootNames
{
    /// <summary>
    /// Enumerates all threads and calls onLocalVar(objRef, localName, methodName, className, moduleName, threadID, appDomainName)
    /// </summary>
    public static ThreadContextSet EnumerateThreadRoots(ICorDebugProcess proc, Action<Address, string, string, string, string, int, string> onLocalVar)
    {
        Debug.Assert(pointerSize == 4 || pointerSize == 8);
        var contextsForThreadLocalVars = new ThreadContextSet();

        int runningOnEntry;
        proc.IsRunning(out runningOnEntry);
        Stopwatch timeStopped = null;
        if (runningOnEntry != 0)
        {
            Console.WriteLine("Stopping in EnumerateThreadRoots");
            Thread.Sleep(100);      // Ensure that the debuggee got some CPU recently
            proc.Stop(5000);        // TODO FIX NOW failure?
            timeStopped = Stopwatch.StartNew();
        }
        try
        {
            uint fetched;
            StringBuilder buffer = new StringBuilder(1024);
            char[] moduleNameBuffer = new Char[260];
            int bufferSizeRet;

            // Get all the threads (We dont' use the enumerator directly because we will let the process run.  
            ICorDebugThreadEnum threadEnum;
            proc.EnumerateThreads(out threadEnum);
            var threadBuff = new ICorDebugThread[1];
            var threads = new List<ICorDebugThread>(16);
            for (; ; )      // For all threads
            {
                threadEnum.Next(1, threadBuff, out fetched);
                if (fetched == 0)
                {
                    break;
                }

                threads.Add(threadBuff[0]);
            }

            // Enumerate the threads 
            foreach (var thread in threads)
            {
                AllowProgramToRun(proc, runningOnEntry, timeStopped);
                try
                {
                    uint threadID;
                    thread.GetID(out threadID);

                    ICorDebugChainEnum chainEnum;
                    ICorDebugChain[] chains = new ICorDebugChain[1];
                    thread.EnumerateChains(out chainEnum);
                    for (; ; ) // For all Chains in the thread
                    {
                        chainEnum.Next(1, chains, out fetched);
                        if (fetched == 0)
                        {
                            break;
                        }

                        ICorDebugFrameEnum frameEnum;
                        ICorDebugFrame[] frames = new ICorDebugFrame[1];
                        chains[0].EnumerateFrames(out frameEnum);
                        for (; ; )  // For all frames in the chain
                        {
                            frameEnum.Next(1, frames, out fetched);
                            if (fetched == 0)
                            {
                                break;
                            }

                            var ilFrame = frames[0] as ICorDebugILFrame;
                            if (ilFrame == null)
                            {
                                continue;
                            }

                            var refLocalVars = new List<Address>();

                            ICorDebugValueEnum valueEnum;
                            ilFrame.EnumerateArguments(out valueEnum);
                            EnumerateLocalVars(valueEnum, refLocalVars, proc, pointerSize);

                            ilFrame.EnumerateLocalVariables(out valueEnum);
                            EnumerateLocalVars(valueEnum, refLocalVars, proc, pointerSize);

                            ICorDebugFunction function;
                            frames[0].GetFunction(out function);

                            ICorDebugModule module;
                            function.GetModule(out module);

                            ICorDebugAssembly assembly;
                            module.GetAssembly(out assembly);

                            ICorDebugAppDomain appDomain;
                            assembly.GetAppDomain(out appDomain);

                            uint nameSize;
                            appDomain.GetName((uint)buffer.Capacity, out nameSize, buffer);
                            var appDomainName = buffer.ToString().Replace(' ', '_');  // We don't want spaces.  
                            contextsForThreadLocalVars.Add(appDomainName, (int)threadID, ilFrame);

                            if (refLocalVars.Count > 0)
                            {
                                IMetadataImport metaData;
                                var guid = new Guid("FCE5EFA0-8BBA-4f8e-A036-8F2022B08466");
                                module.GetMetaDataInterface(guid, out metaData);

                                module.GetName((uint)moduleNameBuffer.Length, out fetched, moduleNameBuffer);
                                string moduleName = new String(moduleNameBuffer, 0, (int)(fetched - 1)); // Remove trailing null

                                uint methodToken;
                                function.GetToken(out methodToken);

                                int methodTypeToken;
                                uint methodAttr, sigBlobSize, codeRVA, implFlags;
                                IntPtr sigBlob;
                                buffer.Length = 0;
                                metaData.GetMethodProps(methodToken, out methodTypeToken, buffer, buffer.Capacity, out bufferSizeRet,
                                    out methodAttr, out sigBlob, out sigBlobSize, out codeRVA, out implFlags);
                                string methodName = buffer.ToString();

                                var className = Regex.Replace(GetMetaDataTypeName(metaData, methodTypeToken, buffer), @"`\d+", "");

                                for (int i = 0; i < refLocalVars.Count; i++)
                                {
                                    onLocalVar(refLocalVars[i], "Local" + i, methodName, className, moduleName, (int)threadID, appDomainName);
                                }
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Error enumerating thread, continuing: {0}", e.Message);
                    Console.WriteLine("Error enumerating thread, continuing: {0}", e.Message);       // TODO FIX NOW
                }
            }
        }
        finally
        {
            if (runningOnEntry != 0)
            {
                Console.WriteLine("Continuing in EnumerateThreadRoots");
                proc.Continue(0);
            }
        }
        return contextsForThreadLocalVars;
    }

    /// <summary>
    /// Allows the program controlled by proc to run some of the time if 'runningOnEntry is non-zero. 
    /// (otherwise it does nothing).  timeStopped is a stopwatch which was started when the program
    /// was last stopped.  (we allow it only 5th of the time).  
    /// </summary>
    private static void AllowProgramToRun(ICorDebugProcess proc, int runningOnEntry, Stopwatch timeStopped)
    {
        if (runningOnEntry == 0)
        {
            return;
        }

        if (timeStopped.ElapsedMilliseconds > 50)
        {
            Console.WriteLine("Used 50 msec.  Letting proc run 200 msec");
            proc.Continue(0);
            Thread.Sleep(200);
            proc.Stop(5000);
            timeStopped.Reset();
            timeStopped.Start();
        }
    }

    /// <summary>
    /// Enumerates all statics in a process and calls 'onStaticVar(objRef, fieldName, className, moduleName, threadID, appDomainName) on each. 
    /// The 'threadID is 0 for normal statics and non-zero for a thread local [ThreadStatic] statics. 
    /// 
    /// TODO contextsForThreadLocalVars is a bit ugly.  
    /// </summary>
    public static void EnumerateStaticRoots(ICorDebugProcess proc, ThreadContextSet contextsForThreadLocalVars, Action<Address, string, string, string, int, string> onStaticVar,
        Action<string, string> onClass = null)
    {

        int runningOnEntry;
        proc.IsRunning(out runningOnEntry);
        Stopwatch timeStopped = null;
        if (runningOnEntry != 0)
        {
            Console.WriteLine("Stopping in EnumerateStaticRoots");
            Thread.Sleep(100);      // Ensure that the debuggee got some CPU recently
            proc.Stop(5000);        // TODO FIX NOW failure?
            timeStopped = Stopwatch.StartNew();
        }
        try
        {
            uint fetched;
            StringBuilder buffer = new StringBuilder(1024);
            char[] moduleNameBuffer = new Char[260];
            int bufferSizeRet;

            ICorDebugAppDomainEnum appDomainEnum;
            proc.EnumerateAppDomains(out appDomainEnum);
            var appDomains = new ICorDebugAppDomain[1];
            for (; ; )
            {
                appDomainEnum.Next(1, appDomains, out fetched);
                if (fetched == 0)
                {
                    break;
                }

                uint nameSize;
                appDomains[0].GetName((uint)buffer.Capacity, out nameSize, buffer);
                var appDomainName = buffer.ToString().Replace(' ', '_');        // We don't want spaces 

                ICorDebugAssemblyEnum assemblyEnum;
                appDomains[0].EnumerateAssemblies(out assemblyEnum);
                var assemblies = new ICorDebugAssembly[1];
                for (; ; )
                {
                    assemblyEnum.Next(1, assemblies, out fetched);
                    if (fetched == 0)
                    {
                        break;
                    }

                    ICorDebugModuleEnum moduleEnum;
                    assemblies[0].EnumerateModules(out moduleEnum);
                    var modules = new ICorDebugModule[1];
                    for (; ; )      // For every module
                    {
                        moduleEnum.Next(1, modules, out fetched);
                        if (fetched == 0)
                        {
                            break;
                        }

                        IMetadataImport metaData;
                        var guid = new Guid("FCE5EFA0-8BBA-4f8e-A036-8F2022B08466");
                        modules[0].GetMetaDataInterface(guid, out metaData);

                        modules[0].GetName((uint)moduleNameBuffer.Length, out fetched, moduleNameBuffer);
                        string moduleName = new String(moduleNameBuffer, 0, (int)(fetched - 1)); // Remove trailing null

                        IntPtr typeEnum = IntPtr.Zero;
                        int typeToken;
                        for (; ; )     // For every type
                        {
                            metaData.EnumTypeDefs(ref typeEnum, out typeToken, 1, out fetched);
                            if (fetched == 0)
                            {
                                break;
                            }

                            AllowProgramToRun(proc, runningOnEntry, timeStopped);
                            try
                            {
                                ICorDebugClass class_ = null;
                                modules[0].GetClassFromToken((uint)typeToken, out class_);

                                var className = Regex.Replace(GetMetaDataTypeName(metaData, typeToken, buffer), @"`\d+", "");

                                IntPtr fieldEnum = IntPtr.Zero;
                                int fieldToken;
                                for (; ; )      // For every field 
                                {
                                    metaData.EnumFields(ref fieldEnum, typeToken, out fieldToken, 1, out fetched);
                                    if (fetched == 0)
                                    {
                                        break;
                                    }

                                    int fieldTypeToken, fieldAttr, sigBlobSize, cplusTypeFlab, fieldLiteralValSize;
                                    IntPtr sigBlob, fieldLiteralVal;
                                    metaData.GetFieldProps(fieldToken, out fieldTypeToken, null, 0, out bufferSizeRet,
                                        out fieldAttr, out sigBlob, out sigBlobSize, out cplusTypeFlab, out fieldLiteralVal, out fieldLiteralValSize);

                                    if ((FieldAttributes.Static & (FieldAttributes)fieldAttr) == 0)
                                    {
                                        continue;
                                    }

                                    if ((FieldAttributes.Literal & (FieldAttributes)fieldAttr) != 0)
                                    {
                                        continue;
                                    }

                                    // TODO check the sig blob and filter non-ref types.  

                                    // Fetch the value, returns 0 if there is none, returns a special error code if it is a threadStatic var.  
                                    var objRef = FetchStaticRefValue(class_, className, fieldToken, metaData, null);
                                    if (!IsError(objRef))
                                    {
                                        var fieldName = GetFieldName(metaData, fieldToken, buffer);
                                        // Console.WriteLine("Found static root 0x{0:x} {1}.{2} thread {3}", objRef, className, fieldName, 0);
                                        onStaticVar(objRef, fieldName, className, moduleName, 0, appDomainName);
                                    }
                                    else if (objRef == PossibleThreadStaticRef)
                                    {
                                        // Thread local case.   
                                        var fieldName = GetFieldName(metaData, fieldToken, buffer);
                                        // Console.WriteLine("Seeing if {0}.{1} is thread local.", className, fieldName);
                                        foreach (var contextForThreadLocalsVars in contextsForThreadLocalVars.Contexts)
                                        {
                                            if (appDomainName != contextForThreadLocalsVars.AppDomainName)
                                            {
                                                continue;
                                            }

                                            // Console.WriteLine("Trying appDomain {0} thread {1}", contextForThreadLocalsVars.AppDomainName, contextForThreadLocalsVars.ThreadId);
                                            objRef = FetchStaticRefValue(class_, className, fieldToken, metaData, contextForThreadLocalsVars.Frame);
                                            if (!IsError(objRef))
                                            {
                                                // Console.WriteLine("Found thread static root 0x{0:x} {1}.{2} appDomain {3} thread {4}", objRef, className, fieldName, contextForThreadLocalsVars.AppDomainName, contextForThreadLocalsVars.ThreadId);
                                                onStaticVar(objRef, fieldName, className, moduleName, contextForThreadLocalsVars.ThreadId, contextForThreadLocalsVars.AppDomainName);
                                            }
                                            else if (objRef != 0)       // Only the 0 code is OK to continue.  
                                            {
                                                // Console.WriteLine("Told not to continue fetching thread statics.");
                                                break;
                                            }
                                        }
                                        // Console.WriteLine("Succeeded with thread local field {0}", fieldName);
                                    }
                                }

                                onClass?.Invoke(className, moduleName);
                            }
                            catch (Exception e)
                            {
                                Debug.WriteLine("Error during static Enumeration ignoring: {0}", e.Message);
                                Console.WriteLine("Error during static Enumeration ignoring: {0}", e.Message);     // TODO FIX NOW 
                            }
                        }
                        metaData.CloseEnum(typeEnum);
                    }
                }
            }
        }
        finally
        {
            if (runningOnEntry != 0)
            {
                Console.WriteLine("Continuing in EnumerateStaticRoots");
                proc.Continue(0);
            }
        }
    }

    // The following functions don't really belong on GCRootNames, as they are general purpose, but given 
    // that the class is internal, it does not matter much...
    /// <summary>
    /// Get the name for a type.  buffer is there efficiency (reusing a buffer)
    /// metaDataOut returns a metaData pointer if it was needed to fetch the name.  (can be ignored if you don't need it). 
    /// </summary>
    internal static string GetTypeName(ICorDebugType corType, out string moduleFilePath, out IMetadataImport metaDataOut, StringBuilder buffer = null)
    {
        metaDataOut = null;
        CorElementType corElemType;
        corType.GetType(out corElemType);
        uint rank;

        moduleFilePath = "mscorlib.dll";    // TODO FIX NOW we need to do this better to get the full path.  
        switch (corElemType)
        {
            case CorElementType.ELEMENT_TYPE_OBJECT:
                return "System.Object";
            case CorElementType.ELEMENT_TYPE_STRING:
                return "System.String";
            case CorElementType.ELEMENT_TYPE_TYPEDBYREF:
                return "System.TypedReference";
            case CorElementType.ELEMENT_TYPE_I:
                return "System.IntPtr";
            case CorElementType.ELEMENT_TYPE_U:
                return "System.UIntPtr";
            case CorElementType.ELEMENT_TYPE_R8:
                return "System.Double";
            case CorElementType.ELEMENT_TYPE_R4:
                return "System.Single";
            case CorElementType.ELEMENT_TYPE_I8:
                return "System.Int64";
            case CorElementType.ELEMENT_TYPE_U8:
                return "System.UInt64";
            case CorElementType.ELEMENT_TYPE_I4:
                return "System.Int32";
            case CorElementType.ELEMENT_TYPE_U4:
                return "System.UInt32";
            case CorElementType.ELEMENT_TYPE_I2:
                return "System.Int16";
            case CorElementType.ELEMENT_TYPE_U2:
                return "System.UInt16";
            case CorElementType.ELEMENT_TYPE_I1:
                return "System.Int8";
            case CorElementType.ELEMENT_TYPE_U1:
                return "System.UInt8";
            case CorElementType.ELEMENT_TYPE_CHAR:
                return "System.Char";
            case CorElementType.ELEMENT_TYPE_BOOLEAN:
                return "System.Boolean";
            case CorElementType.ELEMENT_TYPE_ARRAY:
                corType.GetRank(out rank);
                Debug.Assert(rank >= 1);
                DO_ARRAY:
                ICorDebugType elemType;
                corType.GetFirstTypeParameter(out elemType);
                var elemName = GetTypeName(elemType, out moduleFilePath, out metaDataOut, buffer);
                return elemName + "[" + new string(',', (int)rank - 1) + "]";

            case CorElementType.ELEMENT_TYPE_SZARRAY:
                rank = 1;
                goto DO_ARRAY;

            case CorElementType.ELEMENT_TYPE_CLASS:
            case CorElementType.ELEMENT_TYPE_VALUETYPE:
                break;
            default:
                return "UNKNOWN_TYPE(" + corElemType.ToString() + ")";
        }

        ICorDebugClass corClass = null;
        ICorDebugModule corModule = null;
        try
        {
            corType.GetClass(out corClass);
            corClass.GetModule(out corModule);

            // Get the module name
            char[] moduleNameChars = new char[1024];
            uint moduleNameLen;
            corModule.GetName((uint)moduleNameChars.Length, out moduleNameLen, moduleNameChars);
            moduleFilePath = new string(moduleNameChars, 0, (int)moduleNameLen - 1);  // -1 since the len includes the terminator;
        }
        catch (Exception)
        {
            Console.WriteLine("Error: looking up class for a type with element type {0} !, will have a poor name", corElemType);
            moduleFilePath = "";
            return "UNKNOWN_TYPE(" + corElemType.ToString() + ")";
        }

        if (buffer == null)
        {
            buffer = new StringBuilder(1024);
        }

        var guid = new Guid("FCE5EFA0-8BBA-4f8e-A036-8F2022B08466");
        IMetadataImport metaData;
        corModule.GetMetaDataInterface(ref guid, out metaData);
        metaDataOut = metaData;

        uint classToken;
        corClass.GetToken(out classToken);

        var ret = GCRootNames.GetMetaDataTypeName(metaData, (int)classToken, buffer);

        // Is it a generic type TODO better way of detecting? 
        // TODO FIX NOW issue with nested generic types 
        var match = Regex.Match(ret, @"(.*)`\d+$");
        if (match.Success)
        {
            ret = match.Groups[1].Value + "<";
            ICorDebugTypeEnum typeEnum;
            corType.EnumerateTypeParameters(out typeEnum);
            var typeParams = new ICorDebugType[1];
            uint fetched;
            bool first = true;
            for (; ; )
            {
                typeEnum.Next(1, typeParams, out fetched);
                if (fetched == 0)
                {
                    break;
                }

                if (!first)
                {
                    ret += ",";
                }

                first = false;
                string paramModulePath;
                IMetadataImport paramMetaData;
                ret += GetTypeName(typeParams[0], out paramModulePath, out paramMetaData, buffer);
            }
            ret += ">";
        }
        return ret;
    }

    public static readonly int pointerSize = IntPtr.Size;

    public static bool IsReferenceType(CorElementType elementType)
    {
        switch (elementType)
        {
            case CorElementType.ELEMENT_TYPE_STRING:
            case CorElementType.ELEMENT_TYPE_OBJECT:
            case CorElementType.ELEMENT_TYPE_ARRAY:
            case CorElementType.ELEMENT_TYPE_SZARRAY:
            case CorElementType.ELEMENT_TYPE_CLASS:
                return true;
        }
        return false;
    }
    public static bool IsPrimitiveType(CorElementType corElementType)
    {
        switch (corElementType)
        {
            case CorElementType.ELEMENT_TYPE_I:
            case CorElementType.ELEMENT_TYPE_U:
            case CorElementType.ELEMENT_TYPE_R8:
            case CorElementType.ELEMENT_TYPE_R4:
            case CorElementType.ELEMENT_TYPE_I8:
            case CorElementType.ELEMENT_TYPE_U8:
            case CorElementType.ELEMENT_TYPE_I4:
            case CorElementType.ELEMENT_TYPE_U4:
            case CorElementType.ELEMENT_TYPE_I2:
            case CorElementType.ELEMENT_TYPE_U2:
            case CorElementType.ELEMENT_TYPE_I1:
            case CorElementType.ELEMENT_TYPE_U1:
            case CorElementType.ELEMENT_TYPE_CHAR:
            case CorElementType.ELEMENT_TYPE_BOOLEAN:
                return true;
        }
        return false;
    }

    #region private
    /// <summary>
    /// Stores a context needed to resolve a thread-static variable.  Basically it is a pair appdomain-thread.   
    /// The context is a ICorDebugFrame that has that appdomain-thread combination.  
    /// 
    /// This type is really private to the GCRootNames class.  
    /// </summary>
    public class ThreadContextSet
    {
        public void Add(string appDomainName, int threadId, ICorDebugFrame frame)
        {
            var key = appDomainName + threadId.ToString();
            ThreadContext ret;
            if (m_contexts.TryGetValue(key, out ret))
            {
                return;
            }
            // Console.WriteLine("Interned context Appdomain: {0} ThreadID {1}", appDomainName, threadId);
            m_contexts.Add(key, new ThreadContext { AppDomainName = appDomainName, ThreadId = threadId, Frame = frame });
        }

        public IEnumerable<ThreadContext> Contexts { get { return m_contexts.Values; } }

        public class ThreadContext
        {
            public string AppDomainName;
            public int ThreadId;
            public ICorDebugFrame Frame;
        }

        #region private
        private Dictionary<string, ThreadContext> m_contexts = new Dictionary<string, ThreadContext>();
        #endregion
    }

    private const Address DoNotContinueError = 2;
    private const Address PossibleThreadStaticRef = 1;
    private static bool IsError(Address objRef) { return objRef < 8; }

    private static Address FetchStaticRefValue(ICorDebugClass class_, string className, int fieldToken, IMetadataImport metaData, ICorDebugFrame context)
    {
        ICorDebugValue fieldValue;
        try
        {
            class_.GetStaticFieldValue((uint)fieldToken, context, out fieldValue);
        }
        catch (Exception e)
        {
            var asCom = e as COMException;
            if (asCom != null)
            {
                // 0x8013131A is the 'uniniitalized' error.  
                // 0x80131303 is the 'class not loaded' error 
                // We can safely skip both of these as the variable does not really exist yet, so it can't be root.  
                if ((uint)asCom.ErrorCode == 0x8013131A || (uint)asCom.ErrorCode == 0x80131303)
                {
                    // Console.WriteLine("field not initialized.");
                    return 0;
                }
            }
            if (e is ArgumentException && context == null)
            {
                return PossibleThreadStaticRef;
            }

            Console.WriteLine("Error: looking up thread static {0}.{1} : {2}", className, GetFieldName(metaData, fieldToken, null), e.Message);
            return DoNotContinueError;
        }

        CorElementType fieldElemType;
        fieldValue.GetType(out fieldElemType);
        if (!IsReferenceType(fieldElemType))
        {
            // Console.WriteLine("Field not a reference type.");
            return DoNotContinueError;
        }

        var fieldRefValue = fieldValue as ICorDebugReferenceValue;
        if (fieldRefValue == null)
        {
            Console.WriteLine("Could not dereference field value.");
            return DoNotContinueError;
        }

        Address objRef;
        fieldRefValue.GetValue(out objRef);
        if (objRef == 0)
        {
            // Console.WriteLine("Null value.");
            return 0;
        }
        return objRef;
    }

    private static string GetFieldName(IMetadataImport metaData, int fieldToken, StringBuilder buffer)
    {
        if (buffer == null)
        {
            buffer = new StringBuilder(1024);
        }

        int fieldTypeToken, fieldAttr, sigBlobSize, cplusTypeFlab, fieldLiteralValSize, bufferSizeRet;
        IntPtr sigBlob, fieldLiteralVal;
        metaData.GetFieldProps(fieldToken, out fieldTypeToken, buffer, buffer.Capacity, out bufferSizeRet,
            out fieldAttr, out sigBlob, out sigBlobSize, out cplusTypeFlab, out fieldLiteralVal, out fieldLiteralValSize);
        return buffer.ToString();
    }

    private static void EnumerateLocalVars(ICorDebugValueEnum valueEnum, List<Address> refLocalVars, ICorDebugProcess proc, int pointerSize)
    {

        uint fetched;
        ICorDebugValue[] values = new ICorDebugValue[1];
        for (; ; )
        {
            valueEnum.Next(1, values, out fetched);
            if (fetched == 0)
            {
                break;
            }

            var refVal = values[0] as ICorDebugReferenceValue;
            if (refVal != null)
            {
                Address heapRef;
                refVal.GetValue(out heapRef);
                refLocalVars.Add(heapRef);
            }
        }
    }

    /// <summary>
    /// This version does not give type parmeters for a generic type.  It also has the '`\d* suffix for generic types.  
    /// </summary>
    private static string GetMetaDataTypeName(IMetadataImport metaData, int typeToken, StringBuilder buffer)
    {
        TypeAttributes typeAttr;
        int extendsToken;
        int typeNameLen;
        metaData.GetTypeDefProps(typeToken, buffer, buffer.Capacity, out typeNameLen, out typeAttr, out extendsToken);
        string className = buffer.ToString();

        if ((typeAttr & TypeAttributes.VisibilityMask) >= TypeAttributes.NestedPublic)
        {
            int enclosingClassToken;
            metaData.GetNestedClassProps(typeToken, out enclosingClassToken);
            string enclosingClassName = GetMetaDataTypeName(metaData, enclosingClassToken, buffer);
            className = enclosingClassName + "." + className;
        }
        return className;
    }

    #endregion
}

/// <summary>
/// Gets at Win8 Package information.  
/// </summary>
internal class PackageUtil
{
    public static string FullPackageNameForProcess(Process process)
    {
        var version = Environment.OSVersion.Version.Major * 10 + Environment.OSVersion.Version.Minor;
        if (version < 62)
        {
            return null;        // Packages only exist on Windows 8
        }

        var packageFullNameBuff = new StringBuilder(512);
        int packageFullNameBuffLen = packageFullNameBuff.Capacity;
        var hr = GetPackageFullName(process.Handle, ref packageFullNameBuffLen, packageFullNameBuff);
        GC.KeepAlive(process);
        if (hr != 0)
        {
            return null;
        }

        return packageFullNameBuff.ToString();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetPackageFullName(IntPtr hProcess, ref int packageFullNameLength, StringBuilder packageFullName);
}

[ComImport, Guid("B1AEC16F-2383-4852-B0E9-8F0B1DC66B4D")]
internal class PackageDebugSettingsClass
{
}

[Guid("F27C3930-8029-4AD1-94E3-3DBA417810C1")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPackageDebugSettings
{
    void EnableDebugging(string packageFullName, string debuggerCommandLine, IntPtr environment);
    void DisableDebugging(string packageFullName);
    void Suspend(string packageFullName);
    void Resume(string packageFullName);
    // ...
}

#endregion



