using Graphs;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.JSDumpHeap;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Diagnostics.Utilities;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

public class JavaScriptHeapDumper
{
    /// <summary>
    /// Dump the JSHeap for process 'processID' to the etl file name 'etlFileName'.  Send diagnostics to 'log'.
    /// If 'memoryGraph is non-null also update it to contain the JSDump.  If null you get just the ETL file. 
    /// returns true if successful. 
    /// </summary>
    public static bool DumpAsEtlFile(int processID, string etlFileName, TextWriter log, MemoryGraph memoryGraph = null)
    {
        var ver = Environment.OSVersion.Version;
        var intVer = ver.Major * 10 + ver.Minor;
        if (intVer < 62)
        {
            log.WriteLine("JavaScript Heap Dumping only supported on Win8 or above.");
            return false;
        }

        bool success = false;
        var kernelFileName = Path.ChangeExtension(etlFileName, ".kernel.etl");
        try
        {
            // WPA for some reason won't recognize the ETL file as having a JSDump in it unless it has the process and thread events in it.  
            log.WriteLine("Starting Kernel Logging on {0}", kernelFileName);
            FileUtilities.ForceDelete(kernelFileName);
            using (TraceEventSession kernelModeSession = new TraceEventSession("PerfViewGCHeapKernelETLSession", kernelFileName))
            {
                kernelModeSession.EnableKernelProvider(KernelTraceEventParser.Keywords.Process | KernelTraceEventParser.Keywords.Thread);
                log.WriteLine("Starting ETW logging on File {0}", etlFileName);
                using (var session = new TraceEventSession("PerfViewGCHeapETLSession", etlFileName))
                {
                    session.EnableProvider(JSDumpHeapTraceEventParser.ProviderGuid, TraceEventLevel.Informational,
                        (ulong)JSDumpHeapTraceEventParser.Keywords.jsdumpheap);
                    success = Dump(processID, memoryGraph, log);
                    log.WriteLine("Stopping ETW logging on {0}", etlFileName);
                }
            }

            if (success)
            {
                log.WriteLine("Merging JS and Kernel data ");
                TraceEventSession.MergeInPlace(etlFileName, log);
            }
        }
        finally
        {
            FileUtilities.ForceDelete(kernelFileName);
        }

        log.WriteLine("DumpAsETLFile returns.  Success={0}", success);
        return success;
    }

    /// <summary>
    /// Add the nodes of the JS heap for the process 'processID' to 'memoryGraph' sending diagnostic
    /// messages to 'log'.  If 'memoryGraph' is null, the ETW providers are triggered by we obviously
    /// don't update the memoryGraph.  Thus it is only done for the side effect of triggering a JS heap 
    /// dump. returns true if successful. 
    /// </summary>
    public static bool Dump(int processID, MemoryGraph memoryGraph, TextWriter log)
    {
        var ver = Environment.OSVersion.Version;
        var intVer = ver.Major * 10 + ver.Minor;
        if (intVer < 62)
        {
            log.WriteLine("JavaScript Heap Dumping only supported on Win8 or above.");
            return false;
        }

        var sw = Stopwatch.StartNew();
        var dumper = new JavaScriptDumpGraphReader(log);
        bool dumpComplete = false;
        bool listening = false;
        int edgeRecords = 0;
        TraceEventSession session = null;
        Task readerTask = null;
        try
        {
            bool jsDataPresent = false;
            TimeSpan lastJSUpdate = sw.Elapsed;
            // Set up a separate thread that will listen for ETW events coming back telling us we succeeded. 
            readerTask = Task.Factory.StartNew(delegate
            {
                string sessionName = "PerfViewJSHeapSession";
                session = new TraceEventSession(sessionName, null);
                // Set up the JScript heap listener
                var etwJSParser = new JSDumpHeapTraceEventParser(session.Source);
                etwJSParser.JSDumpHeapEnvelopeStop += delegate (SummaryTraceData data)
                {
                    if (data.ProcessID == processID)
                    {
                        log.WriteLine("{0,5:n1}s: JavaScript GC Complete.", sw.Elapsed.TotalSeconds);
                        dumpComplete = true;
                        memoryGraph.Is64Bit = (data.PointerSize == 8);
                    }
                };

                etwJSParser.JSDumpHeapEnvelopeStart += delegate (SettingsTraceData data)
                {
                    log.WriteLine("{0,5:n1}s: JS Heap Dump Started...", sw.Elapsed.TotalSeconds);
                    jsDataPresent = true;
                };

                etwJSParser.JSDumpHeapBulkEdge += delegate (BulkEdgeTraceData data)
                {
                    if (data.ProcessID == processID)
                    {
                        edgeRecords++;
                        if ((sw.Elapsed - lastJSUpdate).TotalMilliseconds > 500)
                        {
                            log.WriteLine("{0,5:n1}s: Making JS GC Heap Progress...", sw.Elapsed.TotalSeconds);
                        }

                        lastJSUpdate = sw.Elapsed;
                    }
                };

                log.WriteLine("{0,5:n1}s: Enabling JScript Heap Provider", sw.Elapsed.TotalSeconds);
                session.EnableProvider(JSDumpHeapTraceEventParser.ProviderGuid, TraceEventLevel.Informational,
                    (ulong)JSDumpHeapTraceEventParser.Keywords.jsdumpheap);

                listening = true;
                if (memoryGraph != null)
                {
                    dumper.SetupCallbacks(memoryGraph, session.Source, processID);
                }

                session.Source.Process();
                log.WriteLine("{0,5:n1}s: ETW Listener dieing", sw.Elapsed.TotalSeconds);
            });

            // Wait for thread above to start listening (should be very fast)
            while (!listening)
            {
                readerTask.Wait(1);
            }

            Debug.Assert(session != null);

            // Start the providers and trigger the GCs.  
            log.WriteLine("{0,5:n1}s: Requesting a JScript Heap Dump", sw.Elapsed.TotalSeconds);

            // WinBlue (V6.3) does not support the passing of the process ID we used on Win8.
            // Thus we have to drop that so it works everywhere.   
            // !TODO captures state for all processes!   This is not so bad because the others are probably
            // suspended and thus do not get dumped.   You could fix this on  WinBlue by using the new ETW 
            // process filtering.  
            session.CaptureState(JSDumpHeapTraceEventParser.ProviderGuid, (ulong)JSDumpHeapTraceEventParser.Keywords.jsdumpheap);

            for (; ; )
            {
                if (readerTask.Wait(100))
                {
                    break;
                }

                if (!jsDataPresent && sw.Elapsed.TotalSeconds > 5)
                {
                    log.WriteLine("{0,5:n1}s: Assume no JSHeap", sw.Elapsed.TotalSeconds);
                    break;
                }

                if (sw.Elapsed.TotalSeconds > 60)
                {
                    log.WriteLine("{0,5:n1}s: Timed out after 60 seconds", sw.Elapsed.TotalSeconds);
                    break;
                }
                // TODO FIX NOW, time out faster if we seek to be stuck
                if (dumpComplete)
                {
                    break;
                }
            }
            if (jsDataPresent)
            {
                dumper.ConvertHeapDataToGraph();        // Finish the conversion.  
            }
        }
        finally
        {
            // Stop the ETW providers
            log.WriteLine("{0,5:n1}s: Shutting down ETW session", sw.Elapsed.TotalSeconds);
            if (session != null)
            {
                session.Dispose();
            }
        }
        if (readerTask != null)
        {
            log.WriteLine("{0,5:n1}s: Waiting for shutdown to complete.", sw.Elapsed.TotalSeconds);
            if (!readerTask.Wait(2000))
            {
                log.WriteLine("{0,5:n1}s: Shutdown wait timed out after 2 seconds.", sw.Elapsed.TotalSeconds);
            }
        }
        log.WriteLine("Collected {0} JSHeap Bulk Edge Events.", edgeRecords);
        log.WriteLine("[{0,5:n1}s: Done Dumping JScript heap success={1}]", sw.Elapsed.TotalSeconds, dumpComplete);

        return dumpComplete;
    }
}
