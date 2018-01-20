using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace TraceEventSamples
{
    /// <summary>
    /// AllSamples contains a harness for running a the TraceEvent samples. 
    /// </summary>
    partial class AllSamples
    {
        /// <summary>
        /// The samples are 'console based' in that the spew text to an output stream.   By default this is
        /// the Console, but you can redirect it elsewhere by overriding this static variable.  
        /// </summary>
        public static TextWriter Out = Console.Out;

        /// <summary>
        /// This is the main entry point for all the samples.   It runs them sequentially, modify to run the ones you are really interested in. 
        /// </summary>
        public static void Run()
        {
            Console.WriteLine("****************************************************************************");
            Console.WriteLine("We are about Running all demos in order.");
            Console.WriteLine("This takes a miniute or two and is often not that interesting.");
            Console.WriteLine("The intent is that you will find the samples that you are most interested in");
            Console.WriteLine("and modify 00_AllSamples.cs to simply select the demos of interest.");
            Console.WriteLine("If run in a debugger, the program will break after each demo.");
            Console.WriteLine("****************************************************************************");
            Console.WriteLine();
            Console.WriteLine();
 
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Pausing 3 seconds for you to notice the statement above.");
            Thread.Sleep(3000);
            Debugger.Break();       // About to run the actual demos. hit F5 to continue  

            // Note that we are set up by default to run all the samples in order.  
            // Obviously, it is more likely that you will care about some scenarios more than others,
            // so simply comment out (or place a early return statement) to select the demos you 
            // actually care about.   
            SimpleEventSourceMonitor.Run(); Debugger.Break();       // Break point between demos, hit F5 to continue. 
            SimpleEventSourceFile.Run(); Debugger.Break();
            SimpleOSEventMonitor.Run(); Debugger.Break();
            ObserveGCEvents.Run(); Debugger.Break();
            ObserveJitEvents.Run(); Debugger.Break();
            ObserveEventSource.Run(); Debugger.Break();
            ModuleLoadMonitor.Run(); Debugger.Break();
            KernelAndClrMonitor.Run(); Debugger.Break();
            KernelAndClrFile.Run(); Debugger.Break();
            KernelAndClrMonitorWin7.Run(); Debugger.Break();
            KernelAndClrFileWin7.Run(); Debugger.Break();
            SimpleTraceLog.Run(); Debugger.Break();
            TraceLogMonitor.Run(); Debugger.Break();
            SimpleFileRelogger.Run(); Debugger.Break();
            SimpleMonitorRelogger.Run(); Debugger.Break();
            Console.WriteLine("Done with samples");
        }
    }
}