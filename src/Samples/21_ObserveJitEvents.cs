using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Session;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;

namespace TraceEventSamples
{
    class ObserveJitEvents
    {
        /// <summary>
        /// Where all the output goes.  
        /// </summary>
        static TextWriter Out = AllSamples.Out;

        /// <summary>
        /// Sample function demonstrating how to match pairs of events in a live ETW stream,
        /// and compute a duration based on the start and end events. It uses CLR's JIT events
        ///  MethodJittingStarted, ModuleLoadUnload, and ModuleLoadUnloadVerbose.
        ///  for this but the principle applies to most events that mark a duration. 
        /// </summary>
        public static void Run()
        {
            Out.WriteLine("******************** ObserveJitEvents DEMO ********************");
            Out.WriteLine("This program Demos using the reactive framework (IObservable) to monitor");
            Out.WriteLine(".NET Runtime JIT compiler events.");
            Out.WriteLine();
            Out.WriteLine("This program shows how you can use the reactive framework to find pairs");
            Out.WriteLine("of related events (in this the JIT start and stop events) and use them");
            Out.WriteLine("to calculate values (in this case the time spent JIT compiling. ");
            Out.WriteLine();
            Out.WriteLine("The program also shows how to create on the fly aggregate statistics using");
            Out.WriteLine("the reactive framework.   ");
            Out.WriteLine();
            Out.WriteLine("The program will print a line every time a .NET method is JIT compiled");
            Out.WriteLine("in any process on the machine and will print stats every 8 methods.");
            Out.WriteLine();
            Out.WriteLine("Start a .NET Program while the monitoring is active to see the JIT events.");
            Out.WriteLine();

            if (TraceEventSession.IsElevated() != true)
            {
                Out.WriteLine("Must be elevated (Admin) to run this method.");
                Debugger.Break();
                return;
            }
            var monitoringTimeSec = 10;
            Out.WriteLine("The monitor will run for a maximum of {0} seconds", monitoringTimeSec);
            Out.WriteLine("Press Ctrl-C to stop monitoring early.");

            // create a real time user mode session
            using (var userSession = new TraceEventSession("ObserveJitEvents1"))
            {
                // Set up Ctrl-C to stop both user mode and kernel mode sessions
                SetupCtrlCHandler(() => { if (userSession != null) userSession.Stop(); });

                // enable the CLR JIT compiler events. 
                userSession.EnableProvider(ClrTraceEventParser.ProviderGuid, TraceEventLevel.Verbose, (ulong)(ClrTraceEventParser.Keywords.Default));

                // Get the stream of starts.
                IObservable<MethodJittingStartedTraceData> jitStartStream = userSession.Source.Clr.Observe<MethodJittingStartedTraceData>("Method/JittingStarted");

                // And the stream of ends.
                IObservable<MethodLoadUnloadVerboseTraceData> jitEndStream = userSession.Source.Clr.Observe<MethodLoadUnloadVerboseTraceData>("Method/LoadVerbose");

                // Compute the stream of matched-up pairs, and for each create a tuple of the start event and the time between the pair of events.  
                // Note that the 'Take(1)' is pretty important because a nested 'from' statement logically creates the 'cross product' of a two streams
                // In this case the stream of starts and the stream of ends).   Because we filter this stream only to matching entities and then only
                // take the first entry, we stop waiting.   Thus we only 'remember' those 'starts' that are not yet matched, which is very important
                // for efficiency.   Note that any 'lost' end events will never be matched and will accumulate over time, slowing things down.
                // We should put a time window on it as well to 'forget' old start events.  
                var jitTimes =
                    from start in jitStartStream
                    from end in jitEndStream.Where(e => start.MethodID == e.MethodID && start.ProcessID == e.ProcessID).Take(1)
                    select new
                    {
                        Name = GetName(start),
                        ProcessID = start.ProcessID,
                        JitTIme = end.TimeStampRelativeMSec - start.TimeStampRelativeMSec
                    };

                // Create a stream of just the JIT times and compute statistics every 8 methods that are JIT compiled.
                IObservable<Statistics> jitStats = ComputeRunningStats(jitTimes, jitData => jitData.JitTIme, windowSize: 8);

                // Print every time you compile a method 
                jitTimes.Subscribe(onNext: jitData => Out.WriteLine("JIT_TIME: {0,7:f2} PROC: {1,10} METHOD: {2}", jitData.JitTIme, GetProcessName(jitData.ProcessID), jitData.Name));

                // Also output the statistics.  
                jitStats.Subscribe(onNext: Out.WriteLine);      // print some aggregation stats

                // for debugging purposes to see any events that entered by were not handled by any parser.   These can be bugs.  
                // IObservable<TraceEvent> unhandledEventStream = userSession.Source.ObserveUnhandled();
                // unhandledEventStream.Subscribe(onNext: ev => Out.WriteLine("UNHANDLED :  PID: {0,5} {1}/{2} ", ev.ProcessID, ev.ProviderName, ev.EventName));

                // Set up a timer to stop processing after monitoringTimeSec
                IObservable<long> timer = Observable.Timer(new TimeSpan(0, 0, monitoringTimeSec));
                timer.Subscribe(delegate
                {
                    Out.WriteLine("Stopped after {0} sec", monitoringTimeSec);
                    userSession.Stop();
                });

                // OK we are all set up, time to listen for events and pass them to the observers.  
                userSession.Source.Process();
            }
        }

        /// <summary>
        /// The JIT start event breaks a name into its pieces.  Reform the name from the pieces.  
        /// </summary>
        private static string GetName(MethodJittingStartedTraceData data)
        {
            // Prepare sig (strip return value)
            var sig = "";
            var sigWithRet = data.MethodSignature;
            var parenIdx = sigWithRet.IndexOf('(');
            if (0 <= parenIdx)
                sig = sigWithRet.Substring(parenIdx);

            // prepare class name (strip namespace)
            var className = data.MethodNamespace;
            var lastDot = className.LastIndexOf('.');
            if (0 <= lastDot)
                className = className.Substring(lastDot + 1);
            var sep = ".";
            if (className.Length == 0)
                sep = "";

            return className + sep + data.MethodName + sig;
        }

        /// <summary>
        /// Generate an IObservable of statistics from a field in a source IObservable, for "windows" a sizes specified by
        /// "windowSize"
        /// </summary>
        /// <typeparam name="T">Observed type</typeparam>
        /// <param name="source">The initial sequence</param>
        /// <param name="selector">A selector from the observed type to a double field</param>
        /// <param name="windowSize">Size of the window used for computing the statistical values</param>
        /// <returns>IObservable{Statistics}</returns>
        private static IObservable<Statistics> ComputeRunningStats<T>(IObservable<T> source, Func<T, double> selector, int windowSize)
        {
            // Create a stream of floating point valeus from the stream of Ts
            IObservable<double> values = from item in source select selector(item);

            // for each new data point, compute the new running sum to for groups of the last 'windowSize' data points.  
            var accums = from window in values.Window(windowSize)
                         from accum in window.Aggregate(
                             new { curCount = 0, curSum = 0.0, curSumSquares = 0.0, curMin = double.PositiveInfinity, curMax = double.NegativeInfinity },
                             (acc, value) => new
                             {
                                 curCount = acc.curCount + 1,
                                 curSum = acc.curSum + value,
                                 curSumSquares = acc.curSumSquares + value * value,
                                 curMin = (acc.curMin > value) ? value : acc.curMin,
                                 curMax = (acc.curMax < value) ? value : acc.curMax,
                             })
                         select accum;

            // For each accumlation in the stream, compute map it to the statistics for that accumulation.  
            var stats = from accum in accums
                        select new Statistics
                        {
                            Count = accum.curCount,
                            Average = accum.curSum / accum.curCount,
                            Deviation = Math.Sqrt((accum.curCount * accum.curSumSquares - accum.curSum * accum.curSum) / (accum.curCount * accum.curCount - 1)),
                            Min = accum.curMin,
                            Max = accum.curMax,
                        };

            return stats;
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
                        s_bCtrlCExecuted = true;    // ensure non-re-entrancy

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

    /// <summary>
    /// Class containing a set of statistical values
    /// </summary>
    public class Statistics
    {
        public int Count;
        public double Average;
        public double Deviation;
        public double Min;
        public double Max;

        public override string ToString()
        { return string.Format("STATS: count {0} avg {1:F1}. stddev {2:F1}. min {3:F1}. max {4:F1}.", Count, Average, Deviation, Min, Max); }
    }
}