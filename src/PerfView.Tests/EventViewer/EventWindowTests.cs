using EventSources;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Stacks;
using PerfView;
using PerfView.TestUtilities;
using PerfViewTests.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Xunit;
using Xunit.Abstractions;

namespace PerfViewTests.EventViewer
{
    public class EventWindowTests : PerfViewTestBase
    {
        private static readonly TimeSpan TestTimeout = TimeSpan.FromMinutes(2);

        public EventWindowTests(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
        }

        [WpfFact]
        [UseCulture("en-US")]
        public Task TestOpenStacksForOneSelectedCellAsync()
        {
            return TestOpenStacksAsync(SelectedCellScenario.OneCell);
        }

        [WpfFact]
        [UseCulture("en-US")]
        public Task TestOpenStacksForTwoSelectedCellsAsync()
        {
            return TestOpenStacksAsync(SelectedCellScenario.TwoCells);
        }

        [WpfFact]
        [UseCulture("en-US")]
        public Task TestOpenStacksForThreeSelectedCellsAsync()
        {
            return TestOpenStacksAsync(SelectedCellScenario.ThreeCells);
        }

        [WpfFact]
        [UseCulture("en-US")]
        public Task TestOpenStacksForSelectedTimeRangeAsync()
        {
            return TestOpenStacksAsync(SelectedCellScenario.TimeRange);
        }

        private Task TestOpenStacksAsync(SelectedCellScenario scenario)
        {
            Func<Task<EventWindow>> setupAsync = async () =>
            {
                var tracePath = await WithTimeoutAsync(
                    Task.Run(() => GetExtractedTracePath()),
                    "extracting the ETL fixture").ConfigureAwait(false);

                await JoinableTaskFactory.SwitchToMainThreadAsync();

                var etlFile = Assert.IsType<ETLPerfViewData>(PerfViewFile.Get(tracePath));
                try
                {
                    var perfViewFile = new SampledProfileFile(etlFile);
                    var eventData = new PerfViewEventSource(perfViewFile);
                    var opened = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                    eventData.Open(GuiApp.MainWindow, GuiApp.MainWindow.StatusBar, () => opened.TrySetResult(true));
                    await WithTimeoutAsync(opened.Task, "opening the Event Window").ConfigureAwait(true);

                    var eventWindow = eventData.Viewer;
                    eventWindow.EventTypes.SelectAll();
                    eventWindow.Update();
                    await WithTimeoutAsync(
                        eventWindow.StatusBar.WaitForWorkCompleteAsync(),
                        "loading events from the ETL fixture").ConfigureAwait(true);
                    return eventWindow;
                }
                catch
                {
                    etlFile.Close();
                    throw;
                }
            };

            Func<EventWindow, Task> cleanupAsync = async eventWindow =>
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync();

                try
                {
                    foreach (var stackWindow in StackWindow.StackWindows.ToArray())
                    {
                        stackWindow.Close();
                    }

                    eventWindow.Close();
                }
                finally
                {
                    eventWindow.DataSource.DataFile.Close();
                }
            };

            Func<EventWindow, Task> testDriverAsync = async eventWindow =>
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync();

                await VerifyScenarioAsync(eventWindow, scenario).ConfigureAwait(true);
            };

            return RunUITestAsync(setupAsync, testDriverAsync, cleanupAsync);
        }

        private async Task VerifyScenarioAsync(EventWindow eventWindow, SelectedCellScenario scenario)
        {
            Assert.Same(eventWindow.Dispatcher.Thread, Thread.CurrentThread);

            var selectedCells = eventWindow.Grid.SelectedCells;
            selectedCells.Clear();
            var sampledProfileRows = GetSampledProfileRows(
                eventWindow,
                scenario == SelectedCellScenario.OneCell ? 1 : 2);
            if (scenario == SelectedCellScenario.TimeRange)
            {
                var timeColumn = eventWindow.Grid.Columns.Single(column => Equals(column.Header, "Time MSec"));
                selectedCells.Add(new DataGridCellInfo(sampledProfileRows[0], timeColumn));
                selectedCells.Add(new DataGridCellInfo(sampledProfileRows[1], timeColumn));
            }
            else
            {
                selectedCells.Add(new DataGridCellInfo(sampledProfileRows[0], eventWindow.Grid.Columns[0]));
                if (scenario == SelectedCellScenario.TwoCells)
                {
                    selectedCells.Add(new DataGridCellInfo(sampledProfileRows[1], eventWindow.Grid.Columns[0]));
                }
                else if (scenario == SelectedCellScenario.ThreeCells)
                {
                    // Select two cells from the first event to verify that rows are de-duplicated.
                    selectedCells.Add(new DataGridCellInfo(sampledProfileRows[0], eventWindow.Grid.Columns[1]));
                    selectedCells.Add(new DataGridCellInfo(sampledProfileRows[1], eventWindow.Grid.Columns[0]));
                }
            }

            EventWindow.OpenCpuStacksCommand.Execute(null, eventWindow.Grid);
            await WithTimeoutAsync(
                eventWindow.StatusBar.WaitForWorkCompleteAsync(),
                $"opening CPU stacks for {scenario}").ConfigureAwait(true);
            await WithTimeoutAsync(
                WaitForUIAsync(eventWindow.Dispatcher, CancellationToken.None),
                $"dispatching the stack window for {scenario}").ConfigureAwait(true);

            var stackWindow = Assert.Single(StackWindow.StackWindows);
            try
            {
                await WithTimeoutAsync(
                    stackWindow.StatusBar.WaitForWorkCompleteAsync(),
                    $"computing the stack view for {scenario}").ConfigureAwait(true);
                var selectedTimes = scenario == SelectedCellScenario.OneCell
                    ? new[] { sampledProfileRows[0].TimeStampRelatveMSec }
                    : new[] { sampledProfileRows[0].TimeStampRelatveMSec, sampledProfileRows[1].TimeStampRelatveMSec };
                var expectedStart = selectedTimes.Min();
                var expectedEnd = selectedTimes.Max();

                Assert.Equal(expectedStart.ToString("n3"), stackWindow.StartTextBox.Text);
                Assert.Equal(expectedEnd.ToString("n3"), stackWindow.EndTextBox.Text);
                if (scenario == SelectedCellScenario.TimeRange)
                {
                    Assert.NotEmpty(GetSampleTimes(stackWindow.StackSource));
                }
                else
                {
                    Assert.Equal(
                        selectedTimes.OrderBy(time => time),
                        GetSampleTimes(stackWindow.StackSource).OrderBy(time => time));
                }
            }
            finally
            {
                stackWindow.Close();
            }
        }

        private static async Task<T> WithTimeoutAsync<T>(Task<T> task, string operation)
        {
            var timeoutTask = Task.Delay(TestTimeout);
#pragma warning disable VSTHRD003 // Deliberately bound an externally-created operation in test code.
            if (await Task.WhenAny(task, timeoutTask).ConfigureAwait(true) != task)
            {
                throw new TimeoutException($"Timed out after {TestTimeout} while {operation}.");
            }

            return await task.ConfigureAwait(true);
#pragma warning restore VSTHRD003
        }

        private static async Task WithTimeoutAsync(Task task, string operation)
        {
            var timeoutTask = Task.Delay(TestTimeout);
#pragma warning disable VSTHRD003 // Deliberately bound an externally-created operation in test code.
            if (await Task.WhenAny(task, timeoutTask).ConfigureAwait(true) != task)
            {
                throw new TimeoutException($"Timed out after {TestTimeout} while {operation}.");
            }

            await task.ConfigureAwait(true);
#pragma warning restore VSTHRD003
        }

        private enum SelectedCellScenario
        {
            OneCell,
            TwoCells,
            ThreeCells,
            TimeRange,
        }

        private static double[] GetSampleTimes(StackSource stackSource)
        {
            var times = new List<double>();
            stackSource.ForEach(sample => times.Add(sample.TimeRelativeMSec));
            return times.ToArray();
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private static string GetExtractedTracePath()
        {
            var inputDirectory = Path.Combine(AppContext.BaseDirectory, "EventViewer", "inputs");
            var zipPath = Path.Combine(inputDirectory, "net.4.5.2.x86.etl.zip");
            var extractedDirectory = Path.Combine(AppContext.BaseDirectory, "EventViewer", "unzipped");
            var etlPath = Path.Combine(extractedDirectory, Path.GetFileNameWithoutExtension(zipPath));

            Directory.CreateDirectory(extractedDirectory);
            if (!File.Exists(etlPath) || File.GetLastWriteTimeUtc(etlPath) < File.GetLastWriteTimeUtc(zipPath))
            {
                var zipReader = new ZippedETLReader(zipPath)
                {
                    EtlFileName = etlPath,
                    SymbolDirectory = Path.Combine(extractedDirectory, "Symbols"),
                };
                zipReader.UnpackArchive();
            }

            Assert.True(File.Exists(etlPath));
            return etlPath;
        }

        private static ETWEventSource.ETWEventRecord[] GetSampledProfileRows(EventWindow eventWindow, int count)
        {
            var file = Assert.IsType<SampledProfileFile>(eventWindow.DataSource.DataFile);
            var rows = eventWindow.Grid.Items
                .OfType<ETWEventSource.ETWEventRecord>()
                .Where(row => file.IsSampledProfileWithStack(row, eventWindow.StatusBar.LogWriter))
                .Take(count)
                .ToArray();

            Assert.Equal(count, rows.Length);
            return rows;
        }

        /// <summary>
        /// Exposes the real ETL event and CPU-stack sources without running the Event Window's
        /// unrelated cached-symbol lookup, which depends on a PerfView executable host.
        /// </summary>
        private sealed class SampledProfileFile : PerfViewFile
        {
            private readonly ETLPerfViewData m_file;
            private readonly PerfViewStackSource m_cpuStacks;

            public SampledProfileFile(ETLPerfViewData file)
            {
                m_file = file;
                m_cpuStacks = new PerfViewStackSource(this, "CPU");
                Title = file.Title;
            }

            public override string Title { get; }
            public override string FormatName => m_file.FormatName;
            public override string[] FileExtensions => m_file.FileExtensions;
            public override string FilePath => m_file.FilePath;
            public override PerfViewStackSource GetStackSource(string sourceName = null) => m_cpuStacks;

            protected internal override EventSource OpenEventSourceImpl(TextWriter log) => m_file.OpenEventSourceImpl(log);

            public bool IsSampledProfileWithStack(ETWEventSource.ETWEventRecord row, TextWriter log)
            {
                var traceEvent = m_file.GetTraceLog(log).GetEvent(row.Index);
                return traceEvent is SampledProfileTraceData &&
                    traceEvent.ProcessID != 0 &&
                    traceEvent.CallStackIndex() != CallStackIndex.Invalid;
            }

            protected internal override StackSource OpenStackSourceImpl(
                string streamName,
                TextWriter log,
                double startRelativeMSec = 0,
                double endRelativeMSec = double.PositiveInfinity,
                Predicate<TraceEvent> predicate = null)
            {
                return m_file.OpenStackSourceImpl(streamName, log, startRelativeMSec, endRelativeMSec, predicate);
            }

            public override void Close() => m_file.Close();
        }
    }
}
