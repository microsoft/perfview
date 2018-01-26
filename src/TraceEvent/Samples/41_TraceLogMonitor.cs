using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TraceEventSamples
{
    /// <summary>
    /// This is an example of using the Real-Time (non-file) based support of TraceLog to get stack traces for events.   
    /// </summary>
    class TraceLogMonitor
    {
        /// <summary>
        /// Where all the output goes.  
        /// </summary>
        static TextWriter Out = AllSamples.Out;

        public static void Run()
        {
            var monitoringTimeSec = 10;

            Out.WriteLine("******************** RealTimeTraceLog DEMO ********************");
            Out.WriteLine("This program Shows how to use the real-time support in TraceLog");
            Out.WriteLine("We do this by showing how to monitor exceptions in real time ");
            Out.WriteLine();
            Out.WriteLine("This code depends on a Feature of Windows 8.1 (combined user and kernel sessions)");
            Out.WriteLine("It will work on Win7 machines, however win7 can have only one kernel session");
            Out.WriteLine("so it will disrupt any use of the kernel session on that OS. ");
            Out.WriteLine();
            Out.WriteLine("Note that this support is currently experimental and subject to change");
            Out.WriteLine();
            Out.WriteLine("Monitoring .NET Module load and Exception events (with stacks).");
            Out.WriteLine("Run some managed code (ideally that has exceptions) while the monitor is running.");
            Out.WriteLine();

            if (Environment.OSVersion.Version.Major * 10 + Environment.OSVersion.Version.Minor < 62)
                Out.WriteLine("This demo will preempt any use of the kernel provider. ");

           TraceEventSession session = null;

            // Set up Ctrl-C to stop both user mode and kernel mode sessions
            Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs cancelArgs) =>
            {
                if (session != null)
                    session.Dispose();
                cancelArgs.Cancel = true;
            };

            // Cause an exception to be thrown a few seconds in (so we have something interesting to look at)
            var exceptionGeneationTask = Task.Factory.StartNew(delegate
            {
                Thread.Sleep(3000);
                ThrowException();
            });

            Timer timer = null;

            // Create the new session to receive the events.  
            // Because we are on Win 8 this single session can handle both kernel and non-kernel providers.  
            using (session = new TraceEventSession("TraceLogSession"))
            {
                // Enable the events we care about for the kernel
                // For this instant the session will buffer any incoming events.  
                // Enabling kernel events must be done before anything else.   
                // Note that on Win7 it will turn on the one and only NT Kernel Session, and thus interrupt any kernel session in progress.
                // On WIn8 you get a new session (like you would expect).  
                //
                // Note that if you turn on the KernelTraceEventParser.Keywords.Profile, you can also get stacks for CPU sampling 
                // (every millisecond).  (You can use the traceLogSource.Kernel.PerfInfoSample callback).  
                Out.WriteLine("Enabling Image load, Process and Thread events.  These are needed to look up native method names.");
                session.EnableKernelProvider(
                    // KernelTraceEventParser.Keywords.Profile |            // If you want CPU sampling events 
                    // KernelTraceEventParser.Keywords.ContextSwitch |      // If you want context switch events
                    // KernelTraceEventParser.Keywords.Thread |             // If you want context switch events you also need thread start events.  
                    KernelTraceEventParser.Keywords.ImageLoad |
                    KernelTraceEventParser.Keywords.Process,   /****** The second parameter indicates which kernel events should have stacks *****/
                    // KernelTraceEventParser.Keywords.ImageLoad |          // If you want Stacks image load (load library) events
                    // KernelTraceEventParser.Keywords.Profile |            // If you want Stacks for CPU sampling events 
                    // KernelTraceEventParser.Keywords.ContextSwitch |      // If you want Stacks for context switch events
                    KernelTraceEventParser.Keywords.None
                    );

                Out.WriteLine("Enabling CLR Exception and Load events (and stack for those events)");
                // We are monitoring exception events (with stacks) and module load events (with stacks)
                session.EnableProvider(
                    ClrTraceEventParser.ProviderGuid,
                    TraceEventLevel.Informational,
                    (ulong)(ClrTraceEventParser.Keywords.Jit |              // Turning on JIT events is necessary to resolve JIT compiled code 
                    ClrTraceEventParser.Keywords.JittedMethodILToNativeMap | // This is needed if you want line number information in the stacks
                    ClrTraceEventParser.Keywords.Loader |                   // You must include loader events as well to resolve JIT compiled code. 
                    ClrTraceEventParser.Keywords.Exception |                // We want to see the exception events.   
                    ClrTraceEventParser.Keywords.Stack));                   // And stacks on all CLR events where it makes sense.  

                // The CLR events turned on above will let you resolve JIT compiled code as long as the JIT compilation
                // happens AFTER the session has started.   To handle the case for JIT compiled code that was already
                // compiled we need to tell the CLR to dump 'Rundown' events for all existing JIT compiled code.  We
                // do that here.  
                Out.WriteLine("Enabling CLR Events to 'catch up' on JIT compiled code in running processes.");
                session.EnableProvider(ClrRundownTraceEventParser.ProviderGuid, TraceEventLevel.Informational,
                    (ulong)(ClrTraceEventParser.Keywords.Jit |          // We need JIT events to be rundown to resolve method names
                    ClrTraceEventParser.Keywords.JittedMethodILToNativeMap | // This is needed if you want line number information in the stacks
                    ClrTraceEventParser.Keywords.Loader |               // As well as the module load events.  
                    ClrTraceEventParser.Keywords.StartEnumeration));    // This indicates to do the rundown now (at enable time)

                // Because we care about symbols in native code or NGEN images, we need a SymbolReader to decode them.  

                // There is a lot of messages associated with looking up symbols, but we don't want to clutter up 
                // The output by default, so we save it to an internal buffer you can ToString in debug code.  
                // A real app should make this available somehow to the user, because sooner or later you DO need it.  
                TextWriter SymbolLookupMessages = new StringWriter();
                // TextWriter SymbolLookupMessages = Out;           // If you want the symbol debug spew to go to the output, use this. 

                // By default a symbol Reader uses whatever is in the _NT_SYMBOL_PATH variable.  However you can override
                // if you wish by passing it to the SymbolReader constructor.  Since we want this to work even if you 
                // have not set an _NT_SYMBOL_PATH, so we add the Microsoft default symbol server path to be sure/
                var symbolPath = new SymbolPath(SymbolPath.SymbolPathFromEnvironment).Add(SymbolPath.MicrosoftSymbolServerPath);
                SymbolReader symbolReader = new SymbolReader(SymbolLookupMessages, symbolPath.ToString());

                // By default the symbol reader will NOT read PDBs from 'unsafe' locations (like next to the EXE)  
                // because hackers might make malicious PDBs.   If you wish ignore this threat, you can override this
                // check to always return 'true' for checking that a PDB is 'safe'.  
                symbolReader.SecurityCheck = (path => true);

                Out.WriteLine("Open a real time TraceLog session (which understands how to decode stacks).");
                using (TraceLogEventSource traceLogSource = TraceLog.CreateFromTraceEventSession(session)) 
                {
                    // We use this action in the particular callbacks below.  Basically we pass in a symbol reader so we can decode the stack.  
                    // Often the symbol reader is a global variable instead.  
                    Action<TraceEvent> PrintEvent = ((TraceEvent data) => Print(data, symbolReader));

                    // We will print Exceptions and ModuleLoad events. (with stacks).  
                    traceLogSource.Clr.ExceptionStart += PrintEvent;
                    traceLogSource.Clr.LoaderModuleLoad += PrintEvent;
                    // traceLogSource.Clr.All += PrintEvent;

                    // If you want to see stacks for various other kernel events, uncomment these (you also need to turn on the events above)
                    traceLogSource.Kernel.PerfInfoSample += ((SampledProfileTraceData data) => Print(data, symbolReader));
                    // traceLogSource.Kernel.ImageLoad += ((ImageLoadTraceData data) => Print(data, symbolReader));

                    // process events until Ctrl-C is pressed or timeout expires
                    Out.WriteLine("Waiting {0} sec for Events.  Run managed code to see data. ", monitoringTimeSec);
                    Out.WriteLine("Keep in mind there is a several second buffering delay");

                    // Set up a timer to stop processing after monitoringTimeSec 
                    timer = new Timer(delegate(object state)
                    {
                        Out.WriteLine("Stopped Monitoring after {0} sec", monitoringTimeSec);
                        if (session != null)    
                            session.Dispose();
                        session = null;
                    }, null, monitoringTimeSec * 1000, Timeout.Infinite);

                    traceLogSource.Process();
                }
            }
            Out.WriteLine("Finished");
            if (timer != null)
                timer.Dispose();    // Turn off the timer.  
        }

        /// <summary>
        /// Print data.  Note that this method is called FROM DIFFERNET THREADS which means you need to properly
        /// lock any read-write data you access.   It turns out Out.Writeline is already thread safe so
        /// there is nothing I have to do in this case. 
        /// </summary>
        static void Print(TraceEvent data, SymbolReader symbolReader)
        {
            // There are a lot of data collection start on entry that I don't want to see (but often they are quite handy
            if (data.Opcode == TraceEventOpcode.DataCollectionStart)
                return;
            // V3.5 runtimes don't log the stack and in fact don't event log the exception name (it shows up as an empty string)
            // Just ignore these as they are not that interesting. 
            if (data is ExceptionTraceData && ((ExceptionTraceData) data).ExceptionType.Length == 0)
                return;

            if (!data.ProcessName.Contains("Samples"))
                return;

            Out.WriteLine("EVENT: {0}", data.ToString());
            var callStack = data.CallStack();
            if (callStack != null)
            {
                // Because symbol lookup is complex, error prone, and expensive TraceLog requires you to be explicit.  
                // Here we look up names in this call stack using the symbol reader.  
                ResolveNativeCode(callStack, symbolReader);
                Out.WriteLine("CALLSTACK: {0}", callStack.ToString());
            }
        }

        /// <summary>
        /// Because it is expensive and often unnecessary, lookup of native symbols needs to be explicitly requested.  
        /// Here we do this for every frame in the stack.     Note that this is not needed for JIT compiled managed code. 
        /// </summary>
        static private void ResolveNativeCode(TraceCallStack callStack, SymbolReader symbolReader)
        {
            while (callStack != null)
            {
                var codeAddress = callStack.CodeAddress;
                if (codeAddress.Method == null)
                {
                    var moduleFile = codeAddress.ModuleFile;
                    if (moduleFile == null)
                        Trace.WriteLine(string.Format("Could not find module for Address 0x{0:x}", codeAddress.Address));
                    else
                        codeAddress.CodeAddresses.LookupSymbolsForModule(symbolReader, moduleFile);
                }
                callStack = callStack.Caller;
            }
        }

        // Force it not to be inlined so we see the stack. 
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void ThrowException()
        {
            ThrowException1();
        }

        // Force it not to be inlined so we see the stack. 
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void ThrowException1()
        {
            Out.WriteLine("Causing an exception to happen so a CLR Exception Start event will be generated.");
            try
            {
                throw new Exception("This is a test exception thrown to generate a CLR event");
            }
            catch (Exception) { }
        }
    }
}
