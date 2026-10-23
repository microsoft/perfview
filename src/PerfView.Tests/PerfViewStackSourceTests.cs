using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Stacks;
using PerfView;
using PerfViewTests.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Xunit;
using Xunit.Abstractions;

namespace PerfViewTests
{
    public class PerfViewStackSourceTests : PerfViewTestBase
    {
        public PerfViewStackSourceTests(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
        }

        [WpfTheory]
        [InlineData(true)]
        [InlineData(false)]
        public Task EtlxProcessSelectionPrecedesStacksAndCancellationDoesNotBuildAsync(bool etl)
        {
            return RunStackTestAsync(
                () => etl ? (PerfViewFile)new TestEtlFile() : new TestEventPipeFile(),
                async source =>
                {
                    source.FailConstruction = true;
                    bool completed = false;
                    source.Open(GuiApp.MainWindow, GuiApp.MainWindow.StatusBar, () => completed = true);
                    await WaitForWorkerAsync();

                    Assert.NotNull(source.m_SelectProcess);
                    Assert.True(source.m_SelectProcess.IsVisible);
                    Assert.Equal(0, source.ConstructionCount);
                    Assert.Null(source.Viewer);
                    source.m_SelectProcess.Close();
                    Assert.False(completed);

                    source.Open(GuiApp.MainWindow, GuiApp.MainWindow.StatusBar, () => completed = true);
                    await WaitForWorkerAsync();
                    source.m_SelectProcess.AllProcsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    await WaitForWorkerAsync();

                    Assert.Equal(1, source.ConstructionCount);
                    Assert.True(GuiApp.MainWindow.StatusBar.LoggedError);
                    Assert.Contains("Expected stack construction failure", GuiApp.MainWindow.StatusBar.Status);
                    Assert.Null(source.Viewer);
                    Assert.False(completed);
                });
        }

        [WpfTheory]
        [InlineData("selected")]
        [InlineData("all")]
        [InlineData("initial")]
        [InlineData("skip")]
        [InlineData("single-process")]
        [InlineData("no-processes")]
        [InlineData("fallback")]
        public Task EventPipeLaunchPreservesSelectionAndCompletionAsync(string selection)
        {
            return RunStackTestAsync(() => new TestEventPipeFile(), async source =>
            {
                var file = (TestEventPipeFile)source.DataFile;
                file.SupportsSelection = selection != "single-process";
                file.ReturnProcesses = selection != "no-processes";
                source.SkipSelectProcess = selection == "skip";
                source.UseFallback = selection == "fallback";
                if (selection == "initial" || selection == "fallback")
                {
                    file.InitiallyIncludedProcesses = new[] { "tArGeT" };
                }

                Task opened = OpenAsync(source);
                await WaitForWorkerAsync();
                if (selection == "selected" || selection == "all")
                {
                    Assert.Equal(0, source.ConstructionCount);
                    Assert.Null(source.Viewer);
                    Assert.True(source.m_SelectProcess.IsVisible);
                    if (selection == "selected")
                    {
                        source.m_SelectProcess.Grid.SelectedItems.Clear();
                        source.m_SelectProcess.Grid.SelectedItems.Add(file.Processes[0]);
                        source.m_SelectProcess.OKClicked(null, null);
                    }
                    else
                    {
                        source.m_SelectProcess.AllProcsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }
                }
                else
                {
                    Assert.Null(source.m_SelectProcess);
                }

                await opened;
                Assert.Equal(1, source.ConstructionCount);
                Assert.Equal(source.UseFallback ? 1 : 0, source.FallbackCount);
                Assert.Same(source.m_StackSource, source.Viewer.StackSource);
                bool filtered = selection == "selected" || selection == "initial" || selection == "fallback";
                if (filtered)
                {
                    Assert.Equal(new[] { 42 }, file.LaunchedProcessIDs);
                    Assert.Equal(file.GetProcessIncPat(file.Processes[0]), source.Viewer.IncludeRegExTextBox.Text);
                }
                else
                {
                    Assert.Null(file.LaunchedProcessIDs);
                    Assert.Equal("", source.Viewer.IncludeRegExTextBox.Text);
                }

                Assert.Equal(new[] { "configure", "launch", "first", "complete" }, source.LaunchEvents);
                Assert.False(GuiApp.MainWindow.StatusBar.LoggedError);
                Assert.False(source.Viewer.StatusBar.LoggedError);
            });
        }

        [WpfFact]
        public Task EventPipeReusesValidCacheButRebuildsStaleStacksAsync()
        {
            return RunStackTestAsync(() => new TestEventPipeFile(), async source =>
            {
                source.SkipSelectProcess = true;
                await OpenAsync(source);
                StackWindow viewer = source.Viewer;
                await OpenAsync(source);
                Assert.Same(viewer, source.Viewer);
                Assert.Equal(1, source.ConstructionCount);

                viewer.Close();
                await OpenAsync(source);
                Assert.Equal(1, source.ConstructionCount);

                source.Viewer.Close();
                source.DataFile.m_utcLastWriteAtOpen = DateTime.MinValue;
                await OpenAsync(source);
                Assert.Equal(2, source.ConstructionCount);
            });
        }

        [WpfFact]
        public Task EventPipeKeepsCachedStacksIfTraceChangesDuringProcessSelectionAsync()
        {
            return RunStackTestAsync(() => new TestEventPipeFile(), async source =>
            {
                source.SkipSelectProcess = true;
                await OpenAsync(source);
                var cachedStacks = source.m_StackSource;
                source.Viewer.Close();

                source.SkipSelectProcess = false;
                Task opened = OpenAsync(source);
                await WaitForWorkerAsync();
                Assert.True(source.DataFile.IsUpToDate);
                Assert.True(source.m_SelectProcess.IsVisible);

                source.DataFile.m_utcLastWriteAtOpen = DateTime.MinValue;
                Assert.False(source.DataFile.IsUpToDate);
                source.m_SelectProcess.OKClicked(null, null);
                await opened;

                Assert.Equal(1, source.ConstructionCount);
                Assert.Same(cachedStacks, source.Viewer.StackSource);

                source.Viewer.Close();
                source.SkipSelectProcess = true;
                await OpenAsync(source);
                Assert.Equal(2, source.ConstructionCount);
                Assert.NotSame(cachedStacks, source.Viewer.StackSource);
            });
        }

        [WpfFact]
        public Task NetOsHeapAllocSkipsProcessDiscoveryAndRebuildsAsync()
        {
            return RunStackTestAsync(() => new TestEventPipeFile(), async source =>
            {
                var file = (TestEventPipeFile)source.DataFile;
                await OpenAsync(source);
                Assert.Null(source.m_SelectProcess);
                Assert.Equal(0, file.ProcessDiscoveryCount);
                Assert.Equal(1, source.ConstructionCount);

                source.Viewer.Close();
                await OpenAsync(source);
                Assert.Equal(0, file.ProcessDiscoveryCount);
                Assert.Equal(2, source.ConstructionCount);
            }, "Net OS Heap Alloc");
        }

        [WpfFact]
        public Task OtherFormatsConstructStacksBeforeDiscoveringProcessesAsync()
        {
            return RunStackTestAsync(() => new TestStackDependentFile(), async source =>
            {
                var file = (TestStackDependentFile)source.DataFile;
                file.Stacks = source;
                Task opened = OpenAsync(source);
                await WaitForWorkerAsync();
                Assert.Equal(1, source.ConstructionCount);
                Assert.True(source.m_SelectProcess.IsVisible);
                source.m_SelectProcess.OKClicked(null, null);
                await opened;
            });
        }

        #region private
        private Task RunStackTestAsync(Func<PerfViewFile> createFile, Func<TestStackSource, Task> test,
            string sourceName = "CPU")
        {
            return RunUITestAsync(
                () =>
                {
                    var file = createFile();
                    MarkUpToDate(file);
                    return Task.FromResult(new TestStackSource(file, sourceName));
                },
                async source =>
                {
                    await JoinableTaskFactory.SwitchToMainThreadAsync();
                    await test(source);
                },
                async source =>
                {
                    await JoinableTaskFactory.SwitchToMainThreadAsync();
                    source.m_SelectProcess?.Close();
                    await WaitForWorkerAsync();
                    if (source.Viewer != null)
                    {
                        await WithTimeoutAsync(source.Viewer.StatusBar.WaitForWorkCompleteAsync());
                        source.Viewer.Close();
                    }
                });
        }

        private static Task OpenAsync(TestStackSource source)
        {
            var completed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            source.Open(GuiApp.MainWindow, GuiApp.MainWindow.StatusBar, () =>
            {
                source.LaunchEvents.Add("complete");
                completed.TrySetResult(true);
            });
            return WithTimeoutAsync(completed.Task);
        }

        private static Task WaitForWorkerAsync() =>
            WithTimeoutAsync(GuiApp.MainWindow.StatusBar.WaitForWorkCompleteAsync());

        private static async Task WithTimeoutAsync(Task task)
        {
#pragma warning disable VSTHRD003 // Deliberately bound externally-created UI operations in test code.
            Assert.Same(task, await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(30))));
            await task;
#pragma warning restore VSTHRD003
        }

        private static void MarkUpToDate(PerfViewFile file) =>
            file.m_utcLastWriteAtOpen = File.GetLastWriteTimeUtc(file.FilePath);

        private static List<IProcess> CreateProcesses() => new List<IProcess>
        {
            new IProcessForStackSource("Target") { ProcessID = 42, StartTime = DateTime.Today, EndTime = DateTime.Today },
            new IProcessForStackSource("Other") { ProcessID = 43, StartTime = DateTime.Today, EndTime = DateTime.Today }
        };

        private sealed class TestStackSource : PerfViewStackSource
        {
            public TestStackSource(PerfViewFile file, string sourceName) : base(file, sourceName) { }

            protected override StackSource OpenStackSource(string streamName, TextWriter log,
                double startRelativeMSec = 0, double endRelativeMSec = double.PositiveInfinity,
                Predicate<TraceEvent> predicate = null)
            {
                Assert.NotSame(GuiApp.MainWindow.Dispatcher.Thread, Thread.CurrentThread);
                ConstructionCount++;
                if (FailConstruction)
                {
                    throw new ApplicationException("Expected stack construction failure.");
                }
                return UseFallback ? null : new CopyStackSource();
            }

            protected internal override StackSource OpenStackSourceImpl(TextWriter log)
            {
                Assert.NotSame(GuiApp.MainWindow.Dispatcher.Thread, Thread.CurrentThread);
                FallbackCount++;
                return new CopyStackSource();
            }

            protected internal override void ConfigureStackWindow(StackWindow window) => LaunchEvents.Add("configure");
            protected internal override void FirstAction(StackWindow window) => LaunchEvents.Add("first");

            public int ConstructionCount;
            public int FallbackCount;
            public bool UseFallback;
            public bool FailConstruction;
            public readonly List<string> LaunchEvents = new List<string>();
        }

        private sealed class TestEtlFile : ETLPerfViewData
        {
            public TestEtlFile() { m_filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".etl"); }

            public override List<IProcess> GetProcesses(TextWriter log)
            {
                Assert.NotSame(GuiApp.MainWindow.Dispatcher.Thread, Thread.CurrentThread);
                MarkUpToDate(this);
                return CreateProcesses();
            }
        }

        private sealed class TestEventPipeFile : EventPipePerfViewData
        {
            public TestEventPipeFile() { m_filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".nettrace"); }

            public override bool SupportsProcesses => SupportsSelection;

            public override List<IProcess> GetProcesses(TextWriter log)
            {
                Assert.NotSame(GuiApp.MainWindow.Dispatcher.Thread, Thread.CurrentThread);
                ProcessDiscoveryCount++;
                MarkUpToDate(this);
                return ReturnProcesses ? Processes : null;
            }

            protected internal override void OnStackWindowLaunch(Window parentWindow, List<int> processIDs, string sourceName)
            {
                LaunchedProcessIDs = processIDs;
                var source = (TestStackSource)Assert.Single(StackWindow.StackWindows).DataSource;
                source.LaunchEvents.Add("launch");
            }

            public bool SupportsSelection = true;
            public bool ReturnProcesses = true;
            public int ProcessDiscoveryCount;
            public List<int> LaunchedProcessIDs;
            public readonly List<IProcess> Processes = CreateProcesses();
        }

        private sealed class TestStackDependentFile : PerfViewFile
        {
            public TestStackDependentFile() { m_filePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".test"); }

            public override string FormatName => "Test";
            public override string[] FileExtensions => new[] { ".test" };
            public override bool SupportsProcesses => true;

            public override List<IProcess> GetProcesses(TextWriter log)
            {
                Assert.NotSame(GuiApp.MainWindow.Dispatcher.Thread, Thread.CurrentThread);
                Assert.Equal(1, Stacks.ConstructionCount);
                return CreateProcesses();
            }

            public TestStackSource Stacks;
        }
        #endregion
    }
}
