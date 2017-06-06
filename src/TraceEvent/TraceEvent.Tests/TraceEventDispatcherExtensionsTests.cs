using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Analysis;
using PerfView.TestUtilities;
using Xunit;
using Xunit.Abstractions;

namespace TraceEventTests
{
    public class TraceEventDispatcherExtensionsTests : EtlTestBase
    {
        public TraceEventDispatcherExtensionsTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        [WorkItem(261, "https://github.com/Microsoft/perfview/pull/261")]
        public void TestNeedProcessesSingleSource()
        {
            Console.WriteLine($"In {nameof(ObservableTests)}.{nameof(TestNeedProcessesCalledTwice)}");
            PrepareTestData();

            string etlFileName = TestEtlFileNames.First();
            string etlFilePath = Path.Combine(UnZippedDataDir, etlFileName);

            Dictionary<int, float> processTimes;
            using (var source = new ETWTraceEventSource(etlFilePath))
            {
                source.NeedProcesses();
                source.Process();
                processTimes = source.Processes().ToDictionary(p => p.ProcessID, p => p.CPUMSec);
            }

            using (var source = new ETWTraceEventSource(etlFilePath))
            {
                source.NeedProcesses();
                source.Process();

                foreach (var process in source.Processes())
                {
                    Assert.Equal(processTimes[process.ProcessID], process.CPUMSec);
                }
            }
        }

        [Fact]
        [WorkItem(261, "https://github.com/Microsoft/perfview/pull/261")]
        public void TestNeedProcessesTwoSources()
        {
            Console.WriteLine($"In {nameof(ObservableTests)}.{nameof(TestNeedProcessesCalledTwice)}");
            PrepareTestData();

            string etlFileName1 = TestEtlFileNames.First();
            string etlFileName2 = TestEtlFileNames.ElementAt(1);
            string etlFilePath1 = Path.Combine(UnZippedDataDir, etlFileName1);
            string etlFilePath2 = Path.Combine(UnZippedDataDir, etlFileName2);

            Dictionary<int, float> processTimes1;
            using (var source = new ETWTraceEventSource(etlFilePath1))
            {
                source.NeedProcesses();
                source.Process();
                processTimes1 = source.Processes().ToDictionary(p => p.ProcessID, p => p.CPUMSec);
            }

            Dictionary<int, float> processTimes2;
            using (var source = new ETWTraceEventSource(etlFilePath2))
            {
                source.NeedProcesses();
                source.Process();
                processTimes2 = source.Processes().ToDictionary(p => p.ProcessID, p => p.CPUMSec);
            }

            using (var source = new ETWTraceEventSource(etlFilePath1))
            {
                source.NeedProcesses();
                source.Process();

                foreach (var process in source.Processes())
                {
                    Assert.Equal(processTimes1[process.ProcessID], process.CPUMSec);
                }
            }

            using (var source = new ETWTraceEventSource(etlFilePath2))
            {
                source.NeedProcesses();
                source.Process();

                foreach (var process in source.Processes())
                {
                    Assert.Equal(processTimes2[process.ProcessID], process.CPUMSec);
                }
            }
        }

        [Fact]
        [WorkItem(261, "https://github.com/Microsoft/perfview/pull/261")]
        public void TestNeedProcessesCalledTwice()
        {
            Console.WriteLine($"In {nameof(ObservableTests)}.{nameof(TestNeedProcessesCalledTwice)}");
            PrepareTestData();

            string etlFileName = TestEtlFileNames.First();
            string etlFilePath = Path.Combine(UnZippedDataDir, etlFileName);

            Dictionary<int, float> processTimes;
            using (var source = new ETWTraceEventSource(etlFilePath))
            {
                source.NeedProcesses();
                source.Process();
                processTimes = source.Processes().ToDictionary(p => p.ProcessID, p => p.CPUMSec);
            }

            using (var source = new ETWTraceEventSource(etlFilePath))
            {
                source.NeedProcesses();
                source.NeedProcesses();

                source.Process();

                foreach (var process in source.Processes())
                {
                    Assert.Equal(processTimes[process.ProcessID], process.CPUMSec);
                }
            }
        }

        [Fact(Skip = "https://github.com/Microsoft/perfview/issues/276")]
        [WorkItem(261, "https://github.com/Microsoft/perfview/pull/261")]
        public void TestNeedProcessesResetsState()
        {
            Console.WriteLine($"In {nameof(ObservableTests)}.{nameof(TestNeedProcessesResetsState)}");
            PrepareTestData();

            string etlFileName = TestEtlFileNames.First();
            string etlFilePath = Path.Combine(UnZippedDataDir, etlFileName);

            using (var source = new ETWTraceEventSource(etlFilePath))
            {
                source.NeedProcesses();
                source.Process();
                var processTimes = source.Processes().ToDictionary(p => p.ProcessID, p => p.CPUMSec);

                source.NeedProcesses();
                source.Process();

                foreach (var process in source.Processes())
                {
                    Assert.Equal(processTimes[process.ProcessID], process.CPUMSec);
                }
            }
        }

        [Fact]
        [WorkItem(261, "https://github.com/Microsoft/perfview/pull/261")]
        public void TestNeedLoadedDotNetRuntimesSingleSource()
        {
            Console.WriteLine($"In {nameof(ObservableTests)}.{nameof(TestNeedLoadedDotNetRuntimesCalledTwice)}");
            PrepareTestData();

            string etlFileName = TestEtlFileNames.First();
            string etlFilePath = Path.Combine(UnZippedDataDir, etlFileName);

            Dictionary<int, long?> processDuplicatePinningReports;
            Dictionary<int, double?> processGCTotalCpu;
            Dictionary<int, double?> processGCCpuTime;
            using (var source = new ETWTraceEventSource(etlFilePath))
            {
                source.NeedLoadedDotNetRuntimes();
                source.Process();
                processDuplicatePinningReports = source.Processes().ToDictionary(p => p.ProcessID, p => p.LoadedDotNetRuntime()?.GC.GCs.Sum(gc => gc.duplicatedPinningReports));
                processGCTotalCpu = source.Processes().ToDictionary(p => p.ProcessID, p => p.LoadedDotNetRuntime()?.GC.Stats().TotalCpuMSec);
                processGCCpuTime = source.Processes().ToDictionary(p => p.ProcessID, p => p.LoadedDotNetRuntime()?.GC.GCs.Sum(gc => gc.GCCpuMSec));
            }

            using (var source = new ETWTraceEventSource(etlFilePath))
            {
                source.NeedLoadedDotNetRuntimes();
                source.Process();

                foreach (var process in source.Processes())
                {
                    var loadedDotNetRuntime = process.LoadedDotNetRuntime();
                    if (loadedDotNetRuntime == null)
                    {
                        Assert.Null(processDuplicatePinningReports[process.ProcessID]);
                        continue;
                    }

                    Assert.Equal(processDuplicatePinningReports[process.ProcessID], loadedDotNetRuntime.GC.GCs.Sum(gc => gc.duplicatedPinningReports));
                    Assert.Equal(processGCTotalCpu[process.ProcessID], loadedDotNetRuntime.GC.Stats().TotalCpuMSec);
                    Assert.Equal(processGCCpuTime[process.ProcessID], loadedDotNetRuntime.GC.GCs.Sum(gc => gc.GCCpuMSec));
                }
            }
        }

        [Fact]
        [WorkItem(261, "https://github.com/Microsoft/perfview/pull/261")]
        public void TestNeedLoadedDotNetRuntimesTwoSources()
        {
            Console.WriteLine($"In {nameof(ObservableTests)}.{nameof(TestNeedLoadedDotNetRuntimesCalledTwice)}");
            PrepareTestData();

            string etlFileName1 = TestEtlFileNames.First();
            string etlFileName2 = TestEtlFileNames.ElementAt(1);
            string etlFilePath1 = Path.Combine(UnZippedDataDir, etlFileName1);
            string etlFilePath2 = Path.Combine(UnZippedDataDir, etlFileName2);

            Dictionary<int, long?> processDuplicatePinningReports1;
            Dictionary<int, double?> processGCTotalCpu1;
            Dictionary<int, double?> processGCCpuTime1;
            using (var source = new ETWTraceEventSource(etlFilePath1))
            {
                source.NeedLoadedDotNetRuntimes();
                source.Process();
                processDuplicatePinningReports1 = source.Processes().ToDictionary(p => p.ProcessID, p => p.LoadedDotNetRuntime()?.GC.GCs.Sum(gc => gc.duplicatedPinningReports));
                processGCTotalCpu1 = source.Processes().ToDictionary(p => p.ProcessID, p => p.LoadedDotNetRuntime()?.GC.Stats().TotalCpuMSec);
                processGCCpuTime1 = source.Processes().ToDictionary(p => p.ProcessID, p => p.LoadedDotNetRuntime()?.GC.GCs.Sum(gc => gc.GCCpuMSec));
            }

            Dictionary<int, long?> processDuplicatePinningReports2;
            Dictionary<int, double?> processGCTotalCpu2;
            Dictionary<int, double?> processGCCpuTime2;
            using (var source = new ETWTraceEventSource(etlFilePath2))
            {
                source.NeedLoadedDotNetRuntimes();
                source.Process();
                processDuplicatePinningReports2 = source.Processes().ToDictionary(p => p.ProcessID, p => p.LoadedDotNetRuntime()?.GC.GCs.Sum(gc => gc.duplicatedPinningReports));
                processGCTotalCpu2 = source.Processes().ToDictionary(p => p.ProcessID, p => p.LoadedDotNetRuntime()?.GC.Stats().TotalCpuMSec);
                processGCCpuTime2 = source.Processes().ToDictionary(p => p.ProcessID, p => p.LoadedDotNetRuntime()?.GC.GCs.Sum(gc => gc.GCCpuMSec));
            }

            using (var source = new ETWTraceEventSource(etlFilePath1))
            {
                source.NeedLoadedDotNetRuntimes();
                source.Process();

                foreach (var process in source.Processes())
                {
                    var loadedDotNetRuntime = process.LoadedDotNetRuntime();
                    if (loadedDotNetRuntime == null)
                    {
                        Assert.Null(processDuplicatePinningReports1[process.ProcessID]);
                        continue;
                    }

                    Assert.Equal(processDuplicatePinningReports1[process.ProcessID], loadedDotNetRuntime.GC.GCs.Sum(gc => gc.duplicatedPinningReports));
                    Assert.Equal(processGCTotalCpu1[process.ProcessID], loadedDotNetRuntime.GC.Stats().TotalCpuMSec);
                    Assert.Equal(processGCCpuTime1[process.ProcessID], loadedDotNetRuntime.GC.GCs.Sum(gc => gc.GCCpuMSec));
                }
            }

            using (var source = new ETWTraceEventSource(etlFilePath2))
            {
                source.NeedLoadedDotNetRuntimes();
                source.Process();

                foreach (var process in source.Processes())
                {
                    var loadedDotNetRuntime = process.LoadedDotNetRuntime();
                    if (loadedDotNetRuntime == null)
                    {
                        Assert.Null(processDuplicatePinningReports2[process.ProcessID]);
                        continue;
                    }

                    Assert.Equal(processDuplicatePinningReports2[process.ProcessID], loadedDotNetRuntime.GC.GCs.Sum(gc => gc.duplicatedPinningReports));
                    Assert.Equal(processGCTotalCpu2[process.ProcessID], loadedDotNetRuntime.GC.Stats().TotalCpuMSec);
                    Assert.Equal(processGCCpuTime2[process.ProcessID], loadedDotNetRuntime.GC.GCs.Sum(gc => gc.GCCpuMSec));
                }
            }
        }

        [Fact]
        [WorkItem(261, "https://github.com/Microsoft/perfview/pull/261")]
        public void TestNeedLoadedDotNetRuntimesCalledTwice()
        {
            Console.WriteLine($"In {nameof(ObservableTests)}.{nameof(TestNeedLoadedDotNetRuntimesCalledTwice)}");
            PrepareTestData();

            string etlFileName = TestEtlFileNames.First();
            string etlFilePath = Path.Combine(UnZippedDataDir, etlFileName);

            Dictionary<int, long?> processDuplicatePinningReports;
            Dictionary<int, double?> processGCTotalCpu;
            Dictionary<int, double?> processGCCpuTime;
            using (var source = new ETWTraceEventSource(etlFilePath))
            {
                source.NeedLoadedDotNetRuntimes();
                source.Process();
                processDuplicatePinningReports = source.Processes().ToDictionary(p => p.ProcessID, p => p.LoadedDotNetRuntime()?.GC.GCs.Sum(gc => gc.duplicatedPinningReports));
                processGCTotalCpu = source.Processes().ToDictionary(p => p.ProcessID, p => p.LoadedDotNetRuntime()?.GC.Stats().TotalCpuMSec);
                processGCCpuTime = source.Processes().ToDictionary(p => p.ProcessID, p => p.LoadedDotNetRuntime()?.GC.GCs.Sum(gc => gc.GCCpuMSec));
            }

            using (var source = new ETWTraceEventSource(etlFilePath))
            {
                source.NeedLoadedDotNetRuntimes();
                source.NeedLoadedDotNetRuntimes();

                source.Process();

                foreach (var process in source.Processes())
                {
                    var loadedDotNetRuntime = process.LoadedDotNetRuntime();
                    if (loadedDotNetRuntime == null)
                    {
                        Assert.Null(processDuplicatePinningReports[process.ProcessID]);
                        continue;
                    }

                    Assert.Equal(processDuplicatePinningReports[process.ProcessID], loadedDotNetRuntime.GC.GCs.Sum(gc => gc.duplicatedPinningReports));
                    Assert.Equal(processGCTotalCpu[process.ProcessID], loadedDotNetRuntime.GC.Stats().TotalCpuMSec);
                    Assert.Equal(processGCCpuTime[process.ProcessID], loadedDotNetRuntime.GC.GCs.Sum(gc => gc.GCCpuMSec));
                }
            }
        }

        [Fact]
        [WorkItem(261, "https://github.com/Microsoft/perfview/pull/261")]
        public void TestNeedLoadedDotNetRuntimesResetsState()
        {
            Console.WriteLine($"In {nameof(ObservableTests)}.{nameof(TestNeedLoadedDotNetRuntimesResetsState)}");
            PrepareTestData();

            string etlFileName = TestEtlFileNames.First();
            string etlFilePath = Path.Combine(UnZippedDataDir, etlFileName);

            using (var source = new ETWTraceEventSource(etlFilePath))
            {
                source.NeedLoadedDotNetRuntimes();
                source.Process();
                var processDuplicatePinningReports = source.Processes().ToDictionary(p => p.ProcessID, p => p.LoadedDotNetRuntime()?.GC.GCs.Sum(gc => gc.duplicatedPinningReports));
                var processGCTotalCpu = source.Processes().ToDictionary(p => p.ProcessID, p => p.LoadedDotNetRuntime()?.GC.Stats().TotalCpuMSec);
                var processGCCpuTime = source.Processes().ToDictionary(p => p.ProcessID, p => p.LoadedDotNetRuntime()?.GC.GCs.Sum(gc => gc.GCCpuMSec));

                source.NeedLoadedDotNetRuntimes();
                source.Process();

                foreach (var process in source.Processes())
                {
                    var loadedDotNetRuntime = process.LoadedDotNetRuntime();
                    if (loadedDotNetRuntime == null)
                    {
                        Assert.Null(processDuplicatePinningReports[process.ProcessID]);
                        continue;
                    }

                    Assert.Equal(processDuplicatePinningReports[process.ProcessID], loadedDotNetRuntime.GC.GCs.Sum(gc => gc.duplicatedPinningReports));
                    Assert.Equal(processGCTotalCpu[process.ProcessID], loadedDotNetRuntime.GC.Stats().TotalCpuMSec);
                    Assert.Equal(processGCCpuTime[process.ProcessID], loadedDotNetRuntime.GC.GCs.Sum(gc => gc.GCCpuMSec));
                }
            }
        }

        [Fact]
        [WorkItem(261, "https://github.com/Microsoft/perfview/pull/261")]
        public void TestNeedProcessesTwoPipelines()
        {
            Console.WriteLine($"In {nameof(ObservableTests)}.{nameof(TestNeedProcessesTwoPipelines)}");
            PrepareTestData();

            string etlFileName1 = TestEtlFileNames.First();
            string etlFileName2 = TestEtlFileNames.ElementAt(1);
            string etlFilePath1 = Path.Combine(UnZippedDataDir, etlFileName1);
            string etlFilePath2 = Path.Combine(UnZippedDataDir, etlFileName2);

            Dictionary<int, float> processTimes1;
            using (var source = new ETWTraceEventSource(etlFilePath1))
            {
                source.NeedProcesses();
                source.Process();
                processTimes1 = source.Processes().ToDictionary(p => p.ProcessID, p => p.CPUMSec);
            }

            Dictionary<int, float> processTimes2;
            using (var source = new ETWTraceEventSource(etlFilePath2))
            {
                source.NeedProcesses();
                source.Process();
                processTimes2 = source.Processes().ToDictionary(p => p.ProcessID, p => p.CPUMSec);
            }

            using (var source1 = new ETWTraceEventSource(etlFilePath1))
            using (var source2 = new ETWTraceEventSource(etlFilePath2))
            {
                source1.NeedProcesses();
                source2.NeedProcesses();
                source1.NeedProcesses();
                source2.NeedProcesses();

                source2.Process();
                source1.Process();

                foreach (var process in source1.Processes())
                {
                    Assert.Equal(processTimes1[process.ProcessID], process.CPUMSec);
                }

                foreach (var process in source2.Processes())
                {
                    Assert.Equal(processTimes2[process.ProcessID], process.CPUMSec);
                }
            }
        }

        [Fact]
        [WorkItem(261, "https://github.com/Microsoft/perfview/pull/261")]
        public void TestNeedLoadedDotNetRuntimesTwoPipelines()
        {
            Console.WriteLine($"In {nameof(ObservableTests)}.{nameof(TestNeedLoadedDotNetRuntimesTwoPipelines)}");
            PrepareTestData();

            string etlFileName1 = TestEtlFileNames.First();
            string etlFileName2 = TestEtlFileNames.ElementAt(1);
            string etlFilePath1 = Path.Combine(UnZippedDataDir, etlFileName1);
            string etlFilePath2 = Path.Combine(UnZippedDataDir, etlFileName2);

            Dictionary<int, long?> processDuplicatePinningReports1;
            Dictionary<int, double?> processGCTotalCpu1;
            Dictionary<int, double?> processGCCpuTime1;
            using (var source = new ETWTraceEventSource(etlFilePath1))
            {
                source.NeedLoadedDotNetRuntimes();
                source.Process();
                processDuplicatePinningReports1 = source.Processes().ToDictionary(p => p.ProcessID, p => p.LoadedDotNetRuntime()?.GC.GCs.Sum(gc => gc.duplicatedPinningReports));
                processGCTotalCpu1 = source.Processes().ToDictionary(p => p.ProcessID, p => p.LoadedDotNetRuntime()?.GC.Stats().TotalCpuMSec);
                processGCCpuTime1 = source.Processes().ToDictionary(p => p.ProcessID, p => p.LoadedDotNetRuntime()?.GC.GCs.Sum(gc => gc.GCCpuMSec));
            }

            Dictionary<int, long?> processDuplicatePinningReports2;
            Dictionary<int, double?> processGCTotalCpu2;
            Dictionary<int, double?> processGCCpuTime2;
            using (var source = new ETWTraceEventSource(etlFilePath2))
            {
                source.NeedLoadedDotNetRuntimes();
                source.Process();
                processDuplicatePinningReports2 = source.Processes().ToDictionary(p => p.ProcessID, p => p.LoadedDotNetRuntime()?.GC.GCs.Sum(gc => gc.duplicatedPinningReports));
                processGCTotalCpu2 = source.Processes().ToDictionary(p => p.ProcessID, p => p.LoadedDotNetRuntime()?.GC.Stats().TotalCpuMSec);
                processGCCpuTime2 = source.Processes().ToDictionary(p => p.ProcessID, p => p.LoadedDotNetRuntime()?.GC.GCs.Sum(gc => gc.GCCpuMSec));
            }

            using (var source1 = new ETWTraceEventSource(etlFilePath1))
            using (var source2 = new ETWTraceEventSource(etlFilePath2))
            {
                source1.NeedLoadedDotNetRuntimes();
                source2.NeedLoadedDotNetRuntimes();
                source1.NeedLoadedDotNetRuntimes();
                source2.NeedLoadedDotNetRuntimes();

                source2.Process();
                source1.Process();

                foreach (var process in source1.Processes())
                {
                    var loadedDotNetRuntime = process.LoadedDotNetRuntime();
                    if (loadedDotNetRuntime == null)
                    {
                        Assert.Null(processDuplicatePinningReports1[process.ProcessID]);
                        continue;
                    }

                    Assert.Equal(processDuplicatePinningReports1[process.ProcessID], loadedDotNetRuntime.GC.GCs.Sum(gc => gc.duplicatedPinningReports));
                    Assert.Equal(processGCTotalCpu1[process.ProcessID], loadedDotNetRuntime.GC.Stats().TotalCpuMSec);
                    Assert.Equal(processGCCpuTime1[process.ProcessID], loadedDotNetRuntime.GC.GCs.Sum(gc => gc.GCCpuMSec));
                }

                foreach (var process in source2.Processes())
                {
                    var loadedDotNetRuntime = process.LoadedDotNetRuntime();
                    if (loadedDotNetRuntime == null)
                    {
                        Assert.Null(processDuplicatePinningReports2[process.ProcessID]);
                        continue;
                    }

                    Assert.Equal(processDuplicatePinningReports2[process.ProcessID], loadedDotNetRuntime.GC.GCs.Sum(gc => gc.duplicatedPinningReports));
                    Assert.Equal(processGCTotalCpu2[process.ProcessID], loadedDotNetRuntime.GC.Stats().TotalCpuMSec);
                    Assert.Equal(processGCCpuTime2[process.ProcessID], loadedDotNetRuntime.GC.GCs.Sum(gc => gc.GCCpuMSec));
                }
            }
        }
    }
}
