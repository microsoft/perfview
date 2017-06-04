using System;
using System.IO;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
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

        [Theory]
        [MemberData(nameof(TestEtlFiles))]
        public void RunTests(string etlFileName)
        {
            Console.WriteLine($"In {nameof(ObservableTests)}.{nameof(RunTests)}(\"{etlFileName}\")");
            PrepareTestData();

            string etlFilePath = Path.Combine(UnZippedDataDir, etlFileName);

            Action<GCAllocationTickTraceData> handleGCAllocationTick = data =>
            {
                Assert.True(data.AllocationAmount64 >= 0);
            };
            Action handleGCAllocationTickComplete = () =>
            {
                Console.WriteLine("Ticks Completed.");
            };

            Action<TraceEvent> handlePerfViewTick = data =>
            {
                Console.WriteLine("Got PerfView Tick {0:f4}", data.TimeStampRelativeMSec);
            };
            Action handlePerfViewTickComplete = () =>
            {
                Console.WriteLine("Manifests Completed");
            };

            Action<TraceEvent> handleAllTasks = data =>
            {
                if (data.EventName != "ManifestData")
                    Console.WriteLine("Got AllTasks: Data = {0}", data);
            };
            Action handleAllTasksComplete = () =>
            {
                Console.WriteLine("allTasks Completed");
            };

            Action<TraceEvent> handleLogMessage = data =>
            {
                Console.WriteLine("Got PerfView Log Message {0}", data.PayloadByName("message"));
            };
            Action handleLogMessageComplete = () =>
            {
                Console.WriteLine("Log Messages Completed");
            };

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
                using (var gcSub = Subscribe(gcTicks, handleGCAllocationTick, handleGCAllocationTickComplete))
                using (var manifestSub = Subscribe(perfViewTicks, handlePerfViewTick, handlePerfViewTickComplete))
                using (var allTasksSub = Subscribe(allTasks, handleAllTasks, handleAllTasksComplete))
                using (var logSub = Subscribe(logMessages, handleLogMessage, handleLogMessageComplete))
                {
                    IDisposable allPerfSub = null;
                    allPerfSub = Subscribe(allPerfView,
                         delegate(TraceEvent allPerfViewData)
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
                        allPerfSub.Dispose();
                }
                var endCallbackCount = source.CallbackCount();
                Console.WriteLine("endCallbackCount = " + endCallbackCount);
            }
            Console.WriteLine("Done ObservableTests");
        }

        class MyObserver<T> : IObserver<T>
        {
            public MyObserver(Action<T> action, Action completed = null) { m_action = action; m_completed = completed; }
            public void OnNext(T value) { m_action(value); }
            public void OnCompleted() { if (m_completed != null) m_completed(); }
            public void OnError(Exception error) { }
            Action<T> m_action;
            Action m_completed;
        }

        static IDisposable Subscribe<T>(IObservable<T> observable, Action<T> action, Action completed = null)
        {
            return observable.Subscribe(new MyObserver<T>(action, completed));
        }
    }
}
