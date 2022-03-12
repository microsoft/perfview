using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using System;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace TraceEventTests
{
    public class ObservableTests : EtlTestBase
    {
        public ObservableTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory(Skip = "https://github.com/Microsoft/perfview/issues/249")]
        [MemberData(nameof(TestEtlFiles))]
        public void RunTests(string etlFileName)
        {
            Console.WriteLine($"In {nameof(ObservableTests)}.{nameof(RunTests)}(\"{etlFileName}\")");
            PrepareTestData();

            string etlFilePath = Path.Combine(UnZippedDataDir, etlFileName);

            Console.WriteLine("Start ObservableTests");
            using (var source = new ETWTraceEventSource(etlFilePath))
            {
                var startCallbackCount = source.CallbackCount();
                Console.WriteLine("StartCallbackCount = " + startCallbackCount);

                var clrParser = new ClrTraceEventParser(source);
                var eventSourceParser = new DynamicTraceEventParser(source);

                IObservable<GCAllocationTickTraceData> gcTicks = clrParser.Observe<GCAllocationTickTraceData>("GC/AllocationTick");
                IObservable<TraceEvent> logMessages = eventSourceParser.Observe("PerfView", "PerfViewLog");
                IObservable<TraceEvent> allPerfView = eventSourceParser.Observe("PerfView", null);
                IObservable<TraceEvent> perfViewTicks = eventSourceParser.Observe("PerfView", "Tick");
                IObservable<TraceEvent> allTasks = eventSourceParser.Observe("System.Threading.Tasks.TplEventSource", null);

                var cnt = 0;
                using (var gcSub = Subscribe(gcTicks, gcTickData => Console.WriteLine("Got Tick {0}", gcTickData.AllocationAmount), () => Console.WriteLine("Ticks Completed.")))
                using (var manifestSub = Subscribe(perfViewTicks, manifestData => Console.WriteLine("Got PerfView Tick {0:f4}", manifestData.TimeStampRelativeMSec), () => Console.WriteLine("Manifests Completed")))
                using (var allTasksSub = Subscribe(allTasks, delegate (TraceEvent allTasksData)
                {
                    if (allTasksData.EventName != "ManifestData")
                    {
                        Console.WriteLine("Got AllTasks: Data = {0}", allTasksData);
                    }
                }, () => Console.WriteLine("allTasks Completed")))
                using (var logSub = Subscribe(logMessages, logMessageData => Console.WriteLine("Got PerfView Log Message {0}", logMessageData.PayloadByName("message")), () => Console.WriteLine("Log Messages Completed")))
                {
                    IDisposable allPerfSub = null;
                    allPerfSub = Subscribe(allPerfView,
                         delegate (TraceEvent allPerfViewData)
                         {
                             cnt++;
                             if (cnt >= 5)
                             {
                                 Console.WriteLine("Canceling allPerfiew");
                                 allPerfSub.Dispose();
                                 allPerfSub = null;
                             }

                             Console.WriteLine("allPerfView {0}", allPerfViewData);
                         },
                         () => Console.WriteLine("allPerfView Completed"));
                    source.Process();

                    if (allPerfSub != null)
                    {
                        allPerfSub.Dispose();
                    }
                }
                var endCallbackCount = source.CallbackCount();
                Console.WriteLine("endCallbackCount = " + endCallbackCount);
            }
            Console.WriteLine("Done ObservableTests");
        }

        private class MyObserver<T> : IObserver<T>
        {
            public MyObserver(Action<T> action, Action completed = null) { m_action = action; m_completed = completed; }
            public void OnNext(T value) { m_action(value); }
            public void OnCompleted()
            {
                if (m_completed != null)
                {
                    m_completed();
                }
            }
            public void OnError(Exception error) { }

            private Action<T> m_action;
            private Action m_completed;
        }

        private static IDisposable Subscribe<T>(IObservable<T> observable, Action<T> action, Action completed = null)
        {
            return observable.Subscribe(new MyObserver<T>(action, completed));
        }
    }
}
