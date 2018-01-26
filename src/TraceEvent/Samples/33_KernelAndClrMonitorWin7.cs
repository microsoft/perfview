using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

/* README FIRST */
// Until Windows 8, kernel events can only be turned on in a session called 'NT Kernel Logger' and no
// other providers except the kernel provider can be in such a session.   Thus if you wish a trace 
// that has both kernel and non-kernel events you must have two session.   In the code below we show
// how to do this in the real time case.    Basically you simply have two session, one for the
// kernel events and one for everything else.  
//
// This requires the program to be multi-threaded, since there needs to be a thread to listen for
// each session.   This is the main complication of doing this.
//
// Note also that since each session's 'flushing schedule' will be different the events from the
// two session will be NOT necessarily serviced in chronological order (all users events and all
// kernel events will come in order, but user and kernel events may not be in order).  
//
// As mentioned on Windows 8 you can use a single session and thus avoid these complexities.  In addition
// event before Windows 8 there are a number of kernel events that are available as non-kernel 
// providers.   Do a 
//             logman query providers
// To see all providers that the OS provides, and look for ones with the Microsoft-Windows-Kernel prefix.
// While these providers have 'Kernel' in their name they are not subject to the restrictions mentioned
// above, and thus can be used with other user-mode providers.   In particular the Microsoft-Windows-Kernel-Process
// provider can give you the process, thread, and image load events which are some of the most likely
// kernel events you may want.  
// 
namespace TraceEventSamples
{
    public class KernelAndClrMonitorWin7
    {
        /// <summary>
        /// Where all the output goes.  
        /// </summary>
        static TextWriter Out = AllSamples.Out;

        public static void Run()
        {
            var monitoringTimeSec = 10;

            Out.WriteLine("******************** KernelAndClrMonitor DEMO (Win7) ********************");
            Out.WriteLine("Printing both Kernel and CLR (user mode) events simultaneously");
            Out.WriteLine("The monitor will run for a maximum of {0} seconds", monitoringTimeSec);
            Out.WriteLine("Press Ctrl-C to stop monitoring early.");
            Out.WriteLine();
            Out.WriteLine("Start a .NET program to see some events!");
            Out.WriteLine();
            if (TraceEventSession.IsElevated() != true)
            {
                Out.WriteLine("Must be elevated (Admin) to run this program.");
                Debugger.Break();
                return;
            }

            // Set up Ctrl-C to stop both user mode and kernel mode sessions
            Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs cancelArgs) =>
            {
                StopSessions();
                cancelArgs.Cancel = true;
            };

            // Note that because there are different sessions, the events may not be printed
            // in time order since the different sessions may flush their buffers at different 
            // times.   If you care, you must buffer the events and order them by event 
            // timestamp.   Note that the timestamps however ARE accurate.  
            Out.WriteLine("Setup up threads to process the events");

            // start processing kernel events on a thread pool thread
            var task1 = Task.Run(() =>
            {
                // Note that TraceEventSession and EtwTraceEventParser are IN GENERAL NOT THREADSAFE, 
                // Normally this is not a hardship you just set up the session TraceDispacher on one
                // thread.  It is OK to call a session's Dispose() and 'Enable and Disable provider APIS
                // from another thread, but things associated with ETWTraceEventSource and TraceEventParsers
                // should all be on the same thread.  
                Out.WriteLine("Kernel event Thread Starting");
                Out.WriteLine("Enabling Image load, thread and process kernel events.");
                using (s_kernelSession = new TraceEventSession(KernelTraceEventParser.KernelSessionName))
                {
                    // Enable the events we care about for the kernel in the kernel session
                    // For this instant the session will buffer any incoming events.  
                    // If you only have to run on Win8 systems you can use one session for both.  
                    // Here we turn in process, thread and Image load events.  
                    s_kernelSession.EnableKernelProvider(
                        KernelTraceEventParser.Keywords.ImageLoad |
                        KernelTraceEventParser.Keywords.Process |
                        KernelTraceEventParser.Keywords.Thread);

                    // You should do all processing from a single source on a single thread.
                    // Thus call calls to .Source, or Process() should be on the same thread.  
                    s_kernelSession.Source.Kernel.All += Print;
#if DEBUG
                    // in debug builds it is useful to see any unhandled events because they could be bugs. 
                    s_kernelSession.Source.UnhandledEvents += Print;
#endif
                    // process events until Ctrl-C is pressed

                    Out.WriteLine("Waiting on kernel events.");
                    s_kernelSession.Source.Process();
                }
                Out.WriteLine("Thread 1 dieing");
            });

            // start processing CLR events on a thread pool thread
            var task2 = Task.Run(() =>
            {
                // Note that TraceEventSession and EtwTraceEventParser are IN GENERAL NOT THREADSAFE, 
                // Normally this is not a hardship you just set up the session TraceDispacher on one
                // thread.  It is OK to call a session's Dispose() and 'Enable and Disable provider APIS
                // from another thread, but things associated with ETWTraceEventSource and TraceEventParsers
                // should all be on the same thread.  
                using (s_userSession = new TraceEventSession("MonitorKernelAndClrEventsSession"))
                {
                    Out.WriteLine("Enabling CLR GC and Exception events.");
                    // Enable the events we care about for the CLR (in the user session).
                    // unlike the kernel session, you can call EnableProvider on other things too.  
                    // For this instant the ;session will buffer any incoming events.  
                    s_userSession.EnableProvider(
                        ClrTraceEventParser.ProviderGuid,
                        TraceEventLevel.Informational,
                        (ulong)(ClrTraceEventParser.Keywords.GC | ClrTraceEventParser.Keywords.Exception));

                    // s_userSession.Source.Clr.GCHeapStats += (GCHeapStatsTraceData data) => Out.WriteLine(" ", data.GenerationSize0);

                    Out.WriteLine("User event Thread  Starting");
                    s_userSession.Source.Clr.All += Print;
#if DEBUG
                    // in debug builds it is useful to see any unhandled events because they could be bugs. 
                    s_userSession.Source.UnhandledEvents += Print;
#endif
                    // process events until Ctrl-C is pressed or timeout expires
                    Out.WriteLine("Waiting on user events.");
                    s_userSession.Source.Process();
                }
                Out.WriteLine("Thread 2 dieing");
            });

            // Set up a timer to stop processing after monitoringTimeSec 
            var timer = new Timer(delegate(object state)
            {
                Out.WriteLine("Stopped Monitoring after {0} sec", monitoringTimeSec);
                StopSessions();
            }, null, monitoringTimeSec * 1000, Timeout.Infinite);

            // Wait until tasks are complete 
            Out.WriteLine("Waiting for processing tasks to complete");
            Task.WaitAll(task1, task2);
            Out.WriteLine("Monitoring stopped");
            timer.Dispose();
        }

        static bool s_stopping = false;
        static void StopSessions()
        {
            s_stopping = true;
            Out.WriteLine("Insuring all ETW sessions are stopped.");
            if (s_kernelSession != null)
                s_kernelSession.Dispose();
            if (s_userSession != null)
                s_userSession.Dispose();
        }

        /// <summary>
        /// Print data.  Note that this method is called FROM DIFFERNET THREADS which means you need to properly
        /// lock any read-write data you access.   It turns out Out.Writeline is already thread safe so
        /// there is nothing I have to do in this case. 
        /// </summary>
        static void Print(TraceEvent data)
        {
            if (s_stopping)        // Ctrl-C will stop the sessions, but buffered events may still come in, ignore these.  
                return;

            // There are a lot of data collection start on entry that I don't want to see (but often they are quite handy
            if (data.Opcode == TraceEventOpcode.DataCollectionStart)
                return;

            Out.WriteLine(data.ToString());
            if (data is UnhandledTraceEvent)
                Out.WriteLine(data.Dump());
        }

        static TraceEventSession s_userSession;
        static TraceEventSession s_kernelSession;
    }
}

