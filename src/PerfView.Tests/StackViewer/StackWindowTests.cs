using Microsoft.Diagnostics.Tracing.Stacks;
using Microsoft.VisualStudio.Threading;
using PerfView;
using PerfView.TestUtilities;
using PerfView.Utilities;
using PerfViewTests.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Xunit;
using Xunit.Abstractions;
using DataGridCellInfo = System.Windows.Controls.DataGridCellInfo;

namespace PerfViewTests.StackViewer
{
    public class StackWindowTests : PerfViewTestBase
    {
        public StackWindowTests(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
        }

        [WpfFact]
        [WorkItem(316, "https://github.com/Microsoft/perfview/issues/316")]
        public Task TestIncludeItemOnByNameTabAsync()
        {
            return TestIncludeItemAsync(KnownDataGrid.ByName);
        }

        [WpfFact]
        [WorkItem(316, "https://github.com/Microsoft/perfview/issues/316")]
        public Task TestIncludeItemOnCallerCalleeTabCallerAsync()
        {
            return TestIncludeItemAsync(KnownDataGrid.CallerCalleeCallers);
        }

        [WpfFact]
        [WorkItem(316, "https://github.com/Microsoft/perfview/issues/316")]
        public Task TestIncludeItemOnCallerCalleeTabFocusAsync()
        {
            return TestIncludeItemAsync(KnownDataGrid.CallerCalleeFocus);
        }

        [WpfFact]
        [WorkItem(316, "https://github.com/Microsoft/perfview/issues/316")]
        public Task TestIncludeItemOnCallerCalleeTabCalleesAsync()
        {
            return TestIncludeItemAsync(KnownDataGrid.CallerCalleeCallees);
        }

        [WpfFact]
        [WorkItem(316, "https://github.com/Microsoft/perfview/issues/316")]
        public Task TestIncludeItemOnCallTreeTabAsync()
        {
            return TestIncludeItemAsync(KnownDataGrid.CallTree);
        }

        [WpfFact]
        [WorkItem(316, "https://github.com/Microsoft/perfview/issues/316")]
        public Task TestIncludeItemOnCallersTabAsync()
        {
            return TestIncludeItemAsync(KnownDataGrid.Callers);
        }

        [WpfFact]
        [WorkItem(316, "https://github.com/Microsoft/perfview/issues/316")]
        public Task TestIncludeItemOnCalleesTabAsync()
        {
            return TestIncludeItemAsync(KnownDataGrid.Callees);
        }

        private Task TestIncludeItemAsync(KnownDataGrid grid)
        {
            Func<Task<StackWindow>> setupAsync = async () =>
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync();

                var file = new TimeRangeFile();
                await OpenAsync(JoinableTaskFactory, file, GuiApp.MainWindow, GuiApp.MainWindow.StatusBar).ConfigureAwait(true);
                var stackSource = file.GetStackSource();
                return stackSource.Viewer;
            };

            Func<StackWindow, Task> cleanupAsync = async stackWindow =>
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync();

                stackWindow.Close();
            };

            Func<StackWindow, Task> testDriverAsync = async stackWindow =>
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync();

                PerfDataGrid dataGrid = await SelectTabAsync(stackWindow, grid, CancellationToken.None).ConfigureAwait(true);

                object selectedItem = dataGrid.Grid.Items[0];
                var callTreeNodeBase = selectedItem as CallTreeNodeBase;
                if (callTreeNodeBase == null)
                {
                    callTreeNodeBase = (selectedItem as CallTreeViewNode)?.Data;
                }

                Assert.NotNull(callTreeNodeBase);

                // Keep a copy of the DisplayName since setting focus can clear this information from nodes
                string selectedItemName = callTreeNodeBase.DisplayName;

                var dataGridCell = (DataGridCell)dataGrid.DisplayNameColumn.GetCellContent(selectedItem).Parent;
                dataGridCell.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
                dataGridCell.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });

                await WaitForUIAsync(stackWindow.Dispatcher, CancellationToken.None);

                StackWindow.IncludeItemCommand.Execute(null, dataGridCell);

                // Wait for any background processing to complete
                await stackWindow.StatusBar.WaitForWorkCompleteAsync().ConfigureAwait(true);

                Assert.Equal("^" + Regex.Escape(selectedItemName), stackWindow.IncludeRegExTextBox.Text);
            };

            return RunUITestAsync(setupAsync, testDriverAsync, cleanupAsync);
        }

        private static async Task<PerfDataGrid> SelectTabAsync(StackWindow stackWindow, KnownDataGrid grid, CancellationToken cancellationToken)
        {
            Assert.Same(stackWindow.Dispatcher.Thread, Thread.CurrentThread);

            var tabControl = (TabControl)stackWindow.ByNameTab.Parent;
            PerfDataGrid dataGrid;
            switch (grid)
            {
                case KnownDataGrid.ByName:
                    tabControl.SelectedItem = stackWindow.ByNameTab;
                    dataGrid = stackWindow.ByNameDataGrid;
                    break;

                case KnownDataGrid.CallerCalleeCallers:
                case KnownDataGrid.CallerCalleeFocus:
                case KnownDataGrid.CallerCalleeCallees:
                    tabControl.SelectedItem = stackWindow.CallerCalleeTab;
                    if (grid == KnownDataGrid.CallerCalleeCallers)
                    {
                        dataGrid = stackWindow.CallerCalleeView.CallersGrid;
                    }
                    else if (grid == KnownDataGrid.CallerCalleeFocus)
                    {
                        dataGrid = stackWindow.CallerCalleeView.FocusGrid;
                    }
                    else
                    {
                        dataGrid = stackWindow.CallerCalleeView.CalleesGrid;
                    }

                    break;

                case KnownDataGrid.CallTree:
                    tabControl.SelectedItem = stackWindow.CallTreeTab;
                    dataGrid = stackWindow.CallTreeDataGrid;
                    break;

                case KnownDataGrid.Callers:
                    tabControl.SelectedItem = stackWindow.CallersTab;
                    dataGrid = stackWindow.CallersDataGrid;
                    break;

                case KnownDataGrid.Callees:
                    tabControl.SelectedItem = stackWindow.CalleesTab;
                    dataGrid = stackWindow.CalleesDataGrid;
                    break;

                default:
                    throw new ArgumentException("Unsupported data grid.", nameof(grid));
            }

            await WaitForUIAsync(stackWindow.Dispatcher, cancellationToken);

            if (!dataGrid.Grid.HasItems)
            {
                PerfDataGrid gridToDoubleClick = null;
                if (grid == KnownDataGrid.CallerCalleeCallers)
                {
                    gridToDoubleClick = stackWindow.CallerCalleeView.CalleesGrid;
                }
                else if (grid == KnownDataGrid.CallerCalleeCallees)
                {
                    gridToDoubleClick = stackWindow.CallerCalleeView.CallersGrid;
                }

                if (gridToDoubleClick?.Grid.HasItems ?? false)
                {
                    var itemToDoubleClick = gridToDoubleClick.Grid.Items[0];
                    var cellToDoubleClick = (DataGridCell)gridToDoubleClick.DisplayNameColumn.GetCellContent(itemToDoubleClick).Parent;

                    var border = (Border)VisualTreeHelper.GetChild(cellToDoubleClick, 0);
                    var textBlock = (TextBlock)VisualTreeHelper.GetChild(VisualTreeHelper.GetChild(VisualTreeHelper.GetChild(border, 0), 0), 0);
                    Point controlCenter = new Point(textBlock.ActualWidth / 2, textBlock.ActualHeight / 2);
                    Point controlCenterOnView = textBlock.TranslatePoint(controlCenter, (UIElement)Helpers.RootVisual(textBlock));
                    Point controlCenterOnDevice = PresentationSource.FromDependencyObject(Helpers.RootVisual(textBlock)).CompositionTarget.TransformToDevice.Transform(controlCenterOnView);
                    RaiseMouseInputReportEvent(textBlock, Environment.TickCount, (int)controlCenterOnDevice.X, (int)controlCenterOnDevice.Y, 0);

                    gridToDoubleClick.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = Control.MouseDoubleClickEvent, Source = textBlock });

                    await WaitForUIAsync(stackWindow.Dispatcher, cancellationToken);
                }
            }

            return dataGrid;
        }

        // Since the implementation relies on hit testing and not just events coming from the proper sources, we must
        // move the mouse prior to clicking.
        private static void RaiseMouseInputReportEvent(Visual eventSource, int timestamp, int pointX, int pointY, int wheel)
        {
            Assembly targetAssembly = Assembly.GetAssembly(typeof(InputEventArgs));
            Type mouseInputReportType = targetAssembly.GetType("System.Windows.Input.RawMouseInputReport");

            const int AbsoluteMove = 0x10;
            const int Activate = 2;
            Type rawMouseActionsType = targetAssembly.GetType("System.Windows.Input.RawMouseActions");

            object mouseInputReport = mouseInputReportType.GetConstructors()[0].Invoke(
                new[]
                {
                    InputMode.Foreground,
                    timestamp,
                    PresentationSource.FromVisual(eventSource),
                    Enum.ToObject(rawMouseActionsType, AbsoluteMove | Activate),
                    pointX,
                    pointY,
                    wheel,
                    IntPtr.Zero
                });

            mouseInputReportType
                .GetField("_isSynchronize", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(mouseInputReport, true);

            InputEventArgs inputReportEventArgs = (InputEventArgs)targetAssembly
                .GetType("System.Windows.Input.InputReportEventArgs")
                .GetConstructors()[0]
                .Invoke(new[] { Mouse.PrimaryDevice, mouseInputReport });

            inputReportEventArgs.RoutedEvent = (RoutedEvent)typeof(InputManager)
                .GetField("PreviewInputReportEvent", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                .GetValue(null);

            InputManager.Current.ProcessInput(inputReportEventArgs);
        }

        [WpfFact]
        [WorkItem(235, "https://github.com/Microsoft/perfview/issues/235")]
        [UseCulture("en-US")]
        public Task TestSetTimeRangeAsync()
        {
            return TestSetTimeRangeWithSpaceImplAsync(CultureInfo.CurrentCulture);
        }

        [WpfFact]
        [WorkItem(235, "https://github.com/Microsoft/perfview/issues/235")]
        [UseCulture("ru-RU")]
        public Task TestSetTimeRangeWithSpaceAsync()
        {
            return TestSetTimeRangeWithSpaceImplAsync(CultureInfo.CurrentCulture);
        }

        private Task TestSetTimeRangeWithSpaceImplAsync(CultureInfo culture)
        {
            Func<Task<StackWindow>> setupAsync = async () =>
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync();

                var file = new TimeRangeFile();
                await OpenAsync(JoinableTaskFactory, file, GuiApp.MainWindow, GuiApp.MainWindow.StatusBar).ConfigureAwait(true);
                var stackSource = file.GetStackSource();
                return stackSource.Viewer;
            };

            Func<StackWindow, Task> cleanupAsync = async stackWindow =>
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync();

                stackWindow.Close();
            };

            Func<StackWindow, Task> testDriverAsync = async stackWindow =>
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync();

                var byNameView = stackWindow.m_byNameView;
                var row = byNameView.FindIndex(node => node.FirstTimeRelativeMSec > 0 && node.FirstTimeRelativeMSec < node.LastTimeRelativeMSec);
                CallTreeNodeBase selected = byNameView[row];

                var selectedCells = stackWindow.ByNameDataGrid.Grid.SelectedCells;
                selectedCells.Clear();
                selectedCells.Add(new DataGridCellInfo(byNameView[row], stackWindow.ByNameDataGrid.FirstTimeColumn));
                selectedCells.Add(new DataGridCellInfo(byNameView[row], stackWindow.ByNameDataGrid.LastTimeColumn));

                StackWindow.SetTimeRangeCommand.Execute(null, stackWindow.ByNameDataGrid);

                // Wait for any background processing to complete
                await stackWindow.StatusBar.WaitForWorkCompleteAsync().ConfigureAwait(true);

                Assert.Equal(selected.FirstTimeRelativeMSec.ToString("n3", culture), stackWindow.StartTextBox.Text);
                Assert.Equal(selected.LastTimeRelativeMSec.ToString("n3", culture), stackWindow.EndTextBox.Text);
            };

            return RunUITestAsync(setupAsync, testDriverAsync, cleanupAsync);
        }

        private static Task OpenAsync(JoinableTaskFactory factory, PerfViewTreeItem item, Window parentWindow, StatusBar worker)
        {
            return factory.RunAsync(async () =>
            {
                await factory.SwitchToMainThreadAsync();

                var result = new TaskCompletionSource<VoidResult>();
                item.Open(parentWindow, worker, () => result.SetResult(default(VoidResult)));
                await result.Task.ConfigureAwait(false);
            }).Task;
        }

        /// <summary>
        /// A simple file containing four events at different points of time in order to test filtering.
        /// </summary>
        private class TimeRangeFile : PerfViewFile
        {
            public override string Title => nameof(TimeRangeFile);

            public override string FormatName => Title;

            public override string[] FileExtensions => new[] { "Time Range Test" };

            protected override Action<Action> OpenImpl(Window parentWindow, StatusBar worker)
            {
                return doAfter =>
                {
                    m_singletonStackSource = new PerfViewStackSource(this, string.Empty);
                    m_singletonStackSource.Open(parentWindow, worker, doAfter);
                };
            }

            protected internal override StackSource OpenStackSourceImpl(TextWriter log)
            {
                return new TimeRangeStackSource();
            }
        }

        private class TimeRangeStackSource : StackSource
        {
            private readonly List<StackSourceSample> _samples;

            private const StackSourceCallStackIndex StackRoot = StackSourceCallStackIndex.Invalid;
            private const StackSourceCallStackIndex StackEntry = StackSourceCallStackIndex.Start + 0;
            private const StackSourceCallStackIndex StackMiddle = StackSourceCallStackIndex.Start + 1;
            private const StackSourceCallStackIndex StackTail = StackSourceCallStackIndex.Start + 2;

            private const StackSourceFrameIndex FrameEntry = StackSourceFrameIndex.Start + 0;
            private const StackSourceFrameIndex FrameMiddle = StackSourceFrameIndex.Start + 1;
            private const StackSourceFrameIndex FrameTail = StackSourceFrameIndex.Start + 2;

            public TimeRangeStackSource()
            {
                _samples = new List<StackSourceSample>
                {
                    new StackSourceSample(this)
                    {
                        SampleIndex = 0,
                        StackIndex = StackEntry,
                        Metric = 1,
                        TimeRelativeMSec = 0
                    },
                    new StackSourceSample(this)
                    {
                        SampleIndex = (StackSourceSampleIndex)1,
                        StackIndex = StackMiddle,
                        Metric = 1,
                        TimeRelativeMSec = 1000.25
                    },
                    new StackSourceSample(this)
                    {
                        SampleIndex = (StackSourceSampleIndex)2,
                        StackIndex = StackMiddle,
                        Metric = 1,
                        TimeRelativeMSec = 2000.5
                    },
                    new StackSourceSample(this)
                    {
                        SampleIndex = (StackSourceSampleIndex)3,
                        StackIndex = StackTail,
                        Metric = 1,
                        TimeRelativeMSec = 3000.75
                    },
                };
            }

            public override int CallStackIndexLimit
                => (int)StackTail + 1;

            public override int CallFrameIndexLimit
                => (int)FrameTail + 1;

            public override int SampleIndexLimit
                => _samples.Count;

            public override double SampleTimeRelativeMSecLimit
                => _samples.Select(sample => sample.TimeRelativeMSec).Max();

            public override void ForEach(Action<StackSourceSample> callback)
            {
                _samples.ForEach(callback);
            }

            public override StackSourceSample GetSampleByIndex(StackSourceSampleIndex sampleIndex)
                => _samples[(int)sampleIndex];

            public override StackSourceCallStackIndex GetCallerIndex(StackSourceCallStackIndex callStackIndex)
            {
                return StackSourceCallStackIndex.Invalid;
            }

            public override StackSourceFrameIndex GetFrameIndex(StackSourceCallStackIndex callStackIndex)
            {
                switch (callStackIndex)
                {
                    case StackEntry:
                        return FrameEntry;

                    case StackMiddle:
                        return FrameMiddle;

                    case StackTail:
                        return FrameTail;

                    default:
                        return StackSourceFrameIndex.Root;
                }
            }

            public override string GetFrameName(StackSourceFrameIndex frameIndex, bool verboseName)
            {
                if (frameIndex >= StackSourceFrameIndex.Start)
                {
                    return (frameIndex - StackSourceFrameIndex.Start).ToString();
                }

                return frameIndex.ToString();
            }
        }
    }
}
