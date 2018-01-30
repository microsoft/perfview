using System;
using System.Diagnostics.Tracing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TraceEventSamples
{
    // In these demos we generate events with System.Diagnostics.Tracing.EventSource and read them with ETWTraceEventSource
    // 
    // Normally the EventSource and the ETWTraceEventSource would be in different processes, however we 
    // don't do this here to make the scenario really easy to run.   The code works in the multi-process case however.  
    namespace Producer
    {
        [EventSource(Name = "Microsoft-Demos-SimpleMonitor")]     // This is the name of my eventSource outside my program.  
        class MyEventSource : EventSource
        {
            // Notice that the bodies of the events follow a pattern:  WriteEvent(ID, <args>) where 
            //     ID is a unique ID starting at 1 and incrementing for each new event method. and
            //     <args> is every argument for the method.  
            // WriteEvent then takes care of all the details of actually writing out the values complete
            // with the name of the event (method name) as well as the names and types of all the parameters. 
            public void MyFirstEvent(string MyName, int MyId) { WriteEvent(1, MyName, MyId); }
            public void MySecondEvent(int MyId) { WriteEvent(2, MyId); }
            public void MyStopEvent() { WriteEvent(3); }

            // Typically you only create one EventSource and use it throughout your program.  Thus a static field makes sense.  
            public static MyEventSource Log = new MyEventSource();

            // You don't need to define this override, but it does show you when your eventSource gets commands, which
            // is helpful for debugging (you know that your EventSource got the command.  
            protected override void OnEventCommand(EventCommandEventArgs command)
            {
                EventGenerator.Out.WriteLine("EventSource Gets command {0}", command.Command);
            }

            // We could add Keyword definitions so that you could turn on some events but not others
            // but we don't do this here to keep it simple.  Thus you either turn on all events or none.  
        }

        // This code belongs in the process generating the events.   It is in this process for simplicity.  
        class EventGenerator
        {
            static internal TextWriter Out = AllSamples.Out;

            public static void CreateEvents()
            {
                // This just spawns a thread that generates events every second for 10 seconds than issues a Stop event.  
                Task.Factory.StartNew(delegate
                {
                    Out.WriteLine("***** Starting to generate events to Microsoft-Demos-SimpleMonitor for 10 seconds.");
                    for (int i = 0; i < 10; i++)
                    {
                        Out.WriteLine("** Generating a MyFirst and MySecond from Microsoft-Demos-SimpleMonitor.");
                        MyEventSource.Log.MyFirstEvent("Some string " + i.ToString(), i);
                        Thread.Sleep(10);
                        MyEventSource.Log.MySecondEvent(i);
                        Out.WriteLine("Waiting a second");
                        Thread.Sleep(1000);
                    }
                    Out.WriteLine("** Generating the Microsoft-Demos-SimpleMonitor Stop Event.");
                    MyEventSource.Log.MyStopEvent();
                });
            }

            #region Exception Generator
            /// <summary>
            /// Generate two exceptions (but catch them).   This is just to generate interesting CLR events.  
            /// </summary>
            public static void GenerateExceptions()
            {
                Out.WriteLine("Generating a file not found and a Overflow exception.");
                try { MethodA(); }
                catch (Exception) { }
                try { MethodB(-3); }
                catch (Exception) { }
            }

            /// <summary>
            /// This method is here to get a pretty stack trace, force it not to be inlined by the JIT compiler
            /// </summary>
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            private static void MethodA()
            {
                // Try to open a non-existent file, which will cause an exception 
                File.OpenText("NonExistantFile.txt");
            }

            /// <summary>
            /// This method is here to get a pretty stack trace, force it not to be inlined by the JIT compiler
            /// </summary>
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            private static void MethodB(int arraySize)
            {
                // Try to allocate an array with a negative size, which will cause an exception
                var myArray = new byte[arraySize];
            }
            #endregion
        }
    }
}
