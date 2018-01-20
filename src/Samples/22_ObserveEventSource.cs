using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;

namespace TraceEventSamples
{
    class ObserveEventSource
    {
        /// <summary>
        /// Where all the output goes.  
        /// </summary>
        static TextWriter Out = AllSamples.Out;

        public static void Run()
        {
            Out.WriteLine("******************** ObserveEventSource DEMO ********************");
            Out.WriteLine("This program Demos using the reactive framework (IObservable) to monitor");
            Out.WriteLine("EventSource events in real time, parsing their payloads dynamically.");
            Out.WriteLine();
            Out.WriteLine("The program has an EventSource that generates two kinds of events for 10 secs.");
            Out.WriteLine("while another part of the program reads them using IObservables and prints");
            Out.WriteLine("their parsed payload values.  ");
            Out.WriteLine();

            if (TraceEventSession.IsElevated() != true)
            {
                Out.WriteLine("Must be elevated (Admin) to run this method.");
                Debugger.Break();
                return;
            }
            var monitoringTimeSec = 15;
            Out.WriteLine("The monitor will run for a maximum of {0} seconds", monitoringTimeSec);
            Out.WriteLine("Press Ctrl-C to stop monitoring.");

            // create a real time user mode session
            using (var userSession = new TraceEventSession("ObserveEventSource"))
            {
                // Set up Ctrl-C to stop both user mode and kernel mode sessions
                SetupCtrlCHandler(() => { if (userSession != null) userSession.Stop(); });

                // Turn on the Microsoft-Demos-SimpleMonitor provider
                userSession.EnableProvider("Microsoft-Demos-SimpleMonitor");

                // Create a stream of the 'MyFirstEvent' event source events and print their payloads
                IObservable<TraceEvent> firstEventStream = userSession.Source.Dynamic.Observe("Microsoft-Demos-SimpleMonitor", "MyFirstEvent");
                firstEventStream.Subscribe(onNext: ev => Out.WriteLine("FIRST_EVENTS :  MyName: '{0}' MyId: {1}", ev.PayloadByName("MyName"), ev.PayloadByName("MyId")));

                // Create a stream of the 'MySecond'Event' event source events and print their payloads
                IObservable<TraceEvent> secondEventStream = userSession.Source.Dynamic.Observe("Microsoft-Demos-SimpleMonitor", "MySecondEvent");
                secondEventStream.Subscribe(onNext: ev => Out.WriteLine("SECOND_EVENTS :  MyId: {0}", ev.PayloadByName("MyId")));

                // For debugging purposes, print every event from the SimpleMonitor stream
                IObservable<TraceEvent> allEventStream = userSession.Source.Dynamic.Observe(null);
                allEventStream.Subscribe(onNext: ev => Out.WriteLine("FROM_EVENTSOURCE: {0}/{1} ", ev.ProviderName, ev.EventName));

                // It is also useful for debugging purposes to see any events that entered by were not handled by any parser.   These can be bugs.  
                IObservable<TraceEvent> unhandledEventStream = userSession.Source.ObserveUnhandled();
                unhandledEventStream.Subscribe(onNext: ev => Out.WriteLine("UNHANDLED :  {0}/{1} ", ev.ProviderName, ev.EventName));

                // Start Generate some events so we have something to see  
                Producer.EventGenerator.CreateEvents();

                // Set up a timer to stop processing after monitoringTimeSec
                IObservable<long> timer = Observable.Timer(new TimeSpan(0, 0, monitoringTimeSec));
                timer.Subscribe(delegate
                {
                    Out.WriteLine("Stopped after {0} sec", monitoringTimeSec);
                    userSession.Dispose();
                });

                // OK we are all set up, time to listen for events and pass them to the observers.  
                userSession.Source.Process();
            }
            Out.WriteLine("Done with program.");
        }

        /// <summary>
        /// Returns the process name for a given process ID
        /// </summary>
        private static string GetProcessName(int processID)
        {
            // Only keep the cache for 10 seconds to avoid issues with process ID reuse.  
            var now = DateTime.UtcNow;
            if ((now - s_processNameCacheLastUpdate).TotalSeconds > 10)
                s_processNameCache.Clear();
            s_processNameCacheLastUpdate = now;

            string ret = null;
            if (!s_processNameCache.TryGetValue(processID, out ret))
            {
                Process proc = null;
                try { proc = Process.GetProcessById(processID); }
                catch (Exception) { }
                if (proc != null)
                    ret = proc.ProcessName;
                if (string.IsNullOrWhiteSpace(ret))
                    ret = processID.ToString();
                s_processNameCache.Add(processID, ret);
            }
            return ret;
        }
        private static Dictionary<int, string> s_processNameCache = new Dictionary<int, string>();
        private static DateTime s_processNameCacheLastUpdate;

        #region Console CtrlC handling
        private static bool s_bCtrlCExecuted;
        private static ConsoleCancelEventHandler s_CtrlCHandler;
        /// <summary>
        /// This implementation allows one to call this function multiple times during the
        /// execution of a console application. The CtrlC handling is disabled when Ctrl-C 
        /// is typed, one will need to call this method again to re-enable it.
        /// </summary>
        /// <param name="action"></param>
        private static void SetupCtrlCHandler(Action action)
        {
            s_bCtrlCExecuted = false;
            // uninstall previous handler
            if (s_CtrlCHandler != null)
                Console.CancelKeyPress -= s_CtrlCHandler;

            s_CtrlCHandler =
                (object sender, ConsoleCancelEventArgs cancelArgs) =>
                {
                    if (!s_bCtrlCExecuted)
                    {
                        s_bCtrlCExecuted = true;    // ensure not reentrant

                        Out.WriteLine("Stopping monitor");

                        action();                   // execute custom action

                        // terminate normally (i.e. when the monitoring tasks complete b/c we've stopped the sessions)
                        cancelArgs.Cancel = true;
                    }
                };
            Console.CancelKeyPress += s_CtrlCHandler;
        }
        #endregion
    }
}
