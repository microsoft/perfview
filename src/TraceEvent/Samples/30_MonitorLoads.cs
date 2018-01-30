using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using System.IO;
using System.Threading;

/* README FIRST */
// <summary>
// MonitorModuleLoads is a simple application that uses the TraceEvent library to print ever time any
// process starts or stops as well as any DLL that is loaded in any process.   This illustrates very
// basic event processing of OS Kernel events using the TraceEvent library.  
// 
// The basic flow of the program is
//     * Create a 'real time' TraceEventSession.  This lets you control what events you wish to collect
//     * Enable the events of interest (in this case process and image load events).  Because these are 
//          kernel events you must use EnableKernelProvider rather then EnableProvider.  
//     * Connect a ETWTraceEventSource for the session, An TraceEventSource represents the stream of events
//     * Connect a TraceEventParser to the source.    Parsers know how to interpret events from particular 
//          ETW providers.  In this case we care about Kernel events so we get the KernelTraceEventParser
//     * Attach callbacks to the Parser for the events you are interested in.  THese callback get events
//          that are nicely parsed into intellisense-friendly properties. 
//     * Call the ETWTraceEventSource.Process() method, which waits for events and sends them to the callbacks. 
//     
// After 'Process()' is called, it will return if
//     * ETWTraceEventSource.StopProcessing() is called.
//     * The TraceEventSession.Dispose() is called.  
// </summary
namespace TraceEventSamples
{
    class ModuleLoadMonitor
    {
        /// <summary>
        /// Where all the output goes.  
        /// </summary>
        static TextWriter Out = AllSamples.Out;

        public static void Run()
        {
            var monitoringTimeSec = 10;

            Out.WriteLine("******************** ModuleLoadMonitor DEMO ********************");
            Out.WriteLine("Monitoring DLL Loads and Process Starts/Stops system wide");
            Out.WriteLine("The monitor will run for a maximum of {0} seconds", monitoringTimeSec);
            Out.WriteLine("Press Ctrl-C to stop monitoring early.");
            Out.WriteLine();
            Out.WriteLine("Start a program to see some events!");
            Out.WriteLine();
            if (TraceEventSession.IsElevated() != true)
            {
                Out.WriteLine("Must be elevated (Admin) to run this program.");
                Debugger.Break();
                return;
            }

            // Start the session as a real time monitoring session,  
            // Before windows 8, there is a restriction that if you wanted kernel events you must name your session 
            // 'NT Kernel Logger' (the value of KernelSessionName) and there can only be one such session and no
            // other ETW providers can be enabled for that session (thus you need two sessions if you want both
            // kernel and non-kernel events (fixed in Win 8).  We want this to work on Win 7 so we live with those
            // restrictions.   
            using (TraceEventSession session = new TraceEventSession(KernelTraceEventParser.KernelSessionName))
            {
                /* BY DEFAULT ETW SESSIONS SURVIVE THE DEATH OF THE PROESS THAT CREATES THEM! */
                // Unlike most other resources on the system, ETW session live beyond the lifetime of the 
                // process that created them.   This is very useful in some scenarios, but also creates the 
                // very real possibility of leaving 'orphan' sessions running.  
                //
                // To help avoid this by default TraceEventSession sets 'StopOnDispose' so that it will stop
                // the ETW session if the TraceEventSession dies.   Thus executions that 'clean up' the TraceEventSession
                // will clean up the ETW session.   This covers many cases (including throwing exceptions)
                //  
                // However if the process is killed manually (including control C) this cleanup will not happen.  
                // Thus best practices include
                //
                //     * Add a Control C handler that calls session.Dispose() so it gets cleaned up in this common case
                //     * use the same session name run-to-run so you don't create many orphans. 
                //
                // By default TraceEventSessions are in 'create' mode where it assumes you want to create a new session.
                // In this mode if a session already exists, it is stopped and the new one is created.   
                // 
                // Here we install the Control C handler.   
                Console.CancelKeyPress += new ConsoleCancelEventHandler((object sender, ConsoleCancelEventArgs cancelArgs) =>
                {
                    Out.WriteLine("Control C pressed");     // Note that if you hit Ctrl-C twice rapidly you may be called concurrently.  
                    session.Dispose();                          // Note that this causes Process() to return.  
                    cancelArgs.Cancel = true;                   // This says don't abort, since Process() will return we can terminate nicely.   
                });

                // Enable the Kernel events that we want.   At this point data is being collected (but being buffered since we are not reading it)
                // See KernelTraceEventParser.Keywords for what else can be turned on and KernelTraceEventParser for a description
                // of the events that you get when you turn on the various kernel keywords.   Many kernel events will also log a stack
                // when they fire see EnableKernelProvider for more on that.  
                session.EnableKernelProvider(KernelTraceEventParser.Keywords.ImageLoad | KernelTraceEventParser.Keywords.Process);

                // .Source will auto-create a TraceEventSource reading the data from the session
                // .Kernel will auto-create a KernelTraceEventParser getting its events from the source
                // .ImageLoad is an event that you can subscribe to that will be called back when Image load events happen (complete with parsed event)
                session.Source.Kernel.ImageLoad += delegate(ImageLoadTraceData data)
                {
                    Out.WriteLine("Process {0,16} At 0x{1,8:x} Loaded {2}", data.ProcessName, data.ImageBase, data.FileName);
                };
                //  Subscribe to more events (process start) 
                session.Source.Kernel.ProcessStart += delegate(ProcessTraceData data)
                {
                    Out.WriteLine("Process Started {0,6} Parent {1,6} Name {2,8} Cmd: {3}",
                        data.ProcessID, data.ParentID, data.ProcessName, data.CommandLine);
                };
                //  Subscribe to more events (process end)
                session.Source.Kernel.ProcessStop += delegate(ProcessTraceData data)
                {
                    Out.WriteLine("Process Ending {0,6} ", data.ProcessID);
                };

                // Set up a timer to stop processing after monitoringTimeSec
                var timer = new Timer(delegate(object state)
                {
                    Out.WriteLine("Stopped after {0} sec", monitoringTimeSec);
                    session.Source.StopProcessing();
                }, null, monitoringTimeSec * 1000, Timeout.Infinite);

                // Start listening for events, will end if session.Source.StopProcessing() is called or session.Dispose() is called.  
                // Here we never do either of these and thus will only stop when Ctrl-C is hit (but it will clean up because of 
                // our control C handler). 
                session.Source.Process();
                timer.Dispose();    // Done with the timer.  
            }
            Out.WriteLine("Stopping monitor");
        }
    }
}