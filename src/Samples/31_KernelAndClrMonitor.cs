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
// This shows you how to listen to both Kernel and non-Kernel (in this case the CLR) events on Windows 8.
// This is significantly easier than on Win7 (which is shown in 33_KernelAndClrMonitorWin7.cs) because
// a single session can have both kernel and non-kernel providers. 
// 
namespace TraceEventSamples
{
    public class KernelAndClrMonitor
    {
        /// <summary>
        /// Where all the output goes.  
        /// </summary>
        static TextWriter Out = AllSamples.Out;

        public static void Run()
        {
            var monitoringTimeSec = 10;

            if (Environment.OSVersion.Version.Major * 10 + Environment.OSVersion.Version.Minor < 62)
            {
                Out.WriteLine("This demo only works on Win8 / Win 2012 an above)");
                return;
            }

            Out.WriteLine("******************** KernelAndClrMonitor DEMO (Win 8) ********************");
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

            TraceEventSession session = null;

            // Set up Ctrl-C to stop both user mode and kernel mode sessions
            Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs cancelArgs) =>
            {
                if (session != null)
                    session.Dispose();
                cancelArgs.Cancel = true;
            };

            // Set up a timer to stop processing after monitoringTimeSec 
            var timer = new Timer(delegate(object state)
            {
                Out.WriteLine("Stopped Monitoring after {0} sec", monitoringTimeSec);
                if (session != null)
                    session.Dispose();
            }, null, monitoringTimeSec * 1000, Timeout.Infinite);

            // Create the new session to receive the events.  
            // Because we are on Win 8 this single session can handle both kernel and non-kernel providers.  
            using (session = new TraceEventSession("MonitorKernelAndClrEventsSession"))
            {
                // Enable the events we care about for the kernel
                // For this instant the session will buffer any incoming events.  
                // Enabling kernel events must be done before anything else.   
                // This will fail on Win7.  
                Out.WriteLine("Enabling Image load, Process and Thread events.");
                session.EnableKernelProvider(
                    KernelTraceEventParser.Keywords.ImageLoad |
                    KernelTraceEventParser.Keywords.Process |
                    KernelTraceEventParser.Keywords.Thread);

                // Subscribe the events of interest.   In this case we just print all events.  
                session.Source.Kernel.All += Print;

                Out.WriteLine("Enabling CLR GC and Exception events.");
                // Enable the events we care about for the CLR (in the user session).
                // unlike the kernel session, you can call EnableProvider on other things too.  
                // For this instant the ;session will buffer any incoming events.  
                session.EnableProvider(
                    ClrTraceEventParser.ProviderGuid,
                    TraceEventLevel.Informational,
                    (ulong)(ClrTraceEventParser.Keywords.GC | ClrTraceEventParser.Keywords.Exception));

                session.Source.Clr.All += Print;
#if DEBUG
                // in debug builds it is useful to see any unhandled events because they could be bugs. 
                session.Source.UnhandledEvents += Print;
#endif
                // process events until Ctrl-C is pressed or timeout expires
                Out.WriteLine("Waiting for Events.");
                session.Source.Process();
            }

            timer.Dispose();    // Turn off the timer.  
        }

        /// <summary>
        /// Print data.  Note that this method is called FROM DIFFERNET THREADS which means you need to properly
        /// lock any read-write data you access.   It turns out Out.Writeline is already thread safe so
        /// there is nothing I have to do in this case. 
        /// </summary>
        static void Print(TraceEvent data)
        {
            // There are a lot of data collection start on entry that I don't want to see (but often they are quite handy
            if (data.Opcode == TraceEventOpcode.DataCollectionStart)
                return;

            Out.WriteLine(data.ToString());
            if (data is UnhandledTraceEvent)
                Out.WriteLine(data.Dump());
        }
    }
}
