using EventSources;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Stacks;
using Microsoft.VisualStudio.Threading;
using PerfView;
using PerfView.TestUtilities;
using PerfViewTests.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Xunit;
using Xunit.Abstractions;

namespace PerfViewTests.EventViewer
{
    public class EventWindowTests : PerfViewTestBase
    {
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
                await JoinableTaskFactory.SwitchToMainThreadAsync();

                var eventData = new PerfViewEventSource(new SelectedStacksFile());
                var opened = new TaskCompletionSource<bool>();
                eventData.Open(GuiApp.MainWindow, GuiApp.MainWindow.StatusBar, () => opened.SetResult(true));
                await opened.Task.ConfigureAwait(true);

                var eventWindow = eventData.Viewer;
                eventWindow.EventTypes.SelectAll();
                eventWindow.Update();
                await eventWindow.StatusBar.WaitForWorkCompleteAsync().ConfigureAwait(true);
                return eventWindow;
            };

            Func<EventWindow, Task> cleanupAsync = async eventWindow =>
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync();

                foreach (var stackWindow in StackWindow.StackWindows.ToArray())
                {
                    stackWindow.Close();
                }

                eventWindow.Close();
            };

            Func<EventWindow, Task> testDriverAsync = async eventWindow =>
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync();

                var selectedCells = eventWindow.Grid.SelectedCells;
                if (scenario == SelectedCellScenario.TimeRange)
                {
                    var timeColumn = eventWindow.Grid.Columns.Single(column => Equals(column.Header, "Time MSec"));
                    selectedCells.Add(new DataGridCellInfo(eventWindow.Grid.Items[1], timeColumn));
                    selectedCells.Add(new DataGridCellInfo(eventWindow.Grid.Items[2], timeColumn));
                }
                else
                {
                    selectedCells.Add(new DataGridCellInfo(eventWindow.Grid.Items[1], eventWindow.Grid.Columns[0]));
                    if (scenario == SelectedCellScenario.TwoCells)
                    {
                        selectedCells.Add(new DataGridCellInfo(eventWindow.Grid.Items[2], eventWindow.Grid.Columns[0]));
                    }
                    else if (scenario == SelectedCellScenario.ThreeCells)
                    {
                        // Select two cells from the first event to verify that rows are de-duplicated.
                        selectedCells.Add(new DataGridCellInfo(eventWindow.Grid.Items[1], eventWindow.Grid.Columns[1]));
                        selectedCells.Add(new DataGridCellInfo(eventWindow.Grid.Items[2], eventWindow.Grid.Columns[0]));
                    }
                }

                EventWindow.OpenCpuStacksCommand.Execute(null, eventWindow.Grid);
                await eventWindow.StatusBar.WaitForWorkCompleteAsync().ConfigureAwait(true);
                await WaitForUIAsync(eventWindow.Dispatcher, CancellationToken.None);

                var stackWindow = Assert.Single(StackWindow.StackWindows);
                await stackWindow.StatusBar.WaitForWorkCompleteAsync().ConfigureAwait(true);
                Assert.Equal("20.000", stackWindow.StartTextBox.Text);
                if (scenario == SelectedCellScenario.OneCell)
                {
                    Assert.Equal("20.000", stackWindow.EndTextBox.Text);
                    Assert.Equal(new[] { 20.0 }, GetSampleTimes(stackWindow.StackSource));
                }
                else if (scenario == SelectedCellScenario.TimeRange)
                {
                    Assert.Equal("30.000", stackWindow.EndTextBox.Text);
                    Assert.Equal(new[] { 20.0, 25.0, 30.0, 30.0 }, GetSampleTimes(stackWindow.StackSource));
                }
                else
                {
                    Assert.Equal("30.000", stackWindow.EndTextBox.Text);
                    Assert.Equal(new[] { 20.0, 30.0 }, GetSampleTimes(stackWindow.StackSource));
                }
            };

            return RunUITestAsync(setupAsync, testDriverAsync, cleanupAsync);
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

        private sealed class SelectedStacksFile : PerfViewFile
        {
            private readonly PerfViewStackSource m_stackSource;

            public SelectedStacksFile()
            {
                m_stackSource = new PerfViewStackSource(this, "CPU");
                Title = FormatName = nameof(SelectedStacksFile);
            }

            public override string Title { get; }
            public override string FormatName { get; }
            public override string[] FileExtensions { get; } = new[] { "Selected Stacks Test" };
            public override PerfViewStackSource GetStackSource(string sourceName = null) => m_stackSource;

            protected internal override EventSource OpenEventSourceImpl(TextWriter log) => new SelectedStacksEventSource();

            protected internal override StackSource OpenStackSourceImpl(
                string streamName,
                TextWriter log,
                double startRelativeMSec = 0,
                double endRelativeMSec = double.PositiveInfinity,
                Predicate<TraceEvent> predicate = null)
            {
                return new SelectedStacksStackSource(startRelativeMSec, endRelativeMSec, predicate);
            }
        }

        private sealed class SelectedStacksEventSource : EventSource
        {
            private readonly List<TestEtwEventRecord> m_events;

            public override ICollection<string> EventNames { get; } = new[] { "Test/Event" };

            public SelectedStacksEventSource()
            {
                MaxEventTimeRelativeMsec = 30;
                var recordSource = CreateRecordSource();
                m_events = new List<TestEtwEventRecord>
                {
                    new TestEtwEventRecord(recordSource, 10, (EventIndex)1),
                    new TestEtwEventRecord(recordSource, 20, (EventIndex)2),
                    new TestEtwEventRecord(recordSource, 30, (EventIndex)4),
                };
            }

            public override EventSource Clone() => new SelectedStacksEventSource();
            public override void SetEventFilter(List<string> eventNames) { }

            public override void ForEach(Func<EventRecord, bool> callback)
            {
                foreach (var eventRecord in m_events.Where(e => e.TimeStampRelatveMSec >= StartTimeRelativeMSec && e.TimeStampRelatveMSec <= EndTimeRelativeMSec))
                {
                    if (!callback(eventRecord))
                    {
                        break;
                    }
                }
            }

            private static ETWEventSource CreateRecordSource()
            {
#pragma warning disable SYSLIB0050 // Formatter-based initialization is used only for this lightweight test double.
                var source = (ETWEventSource)FormatterServices.GetUninitializedObject(typeof(ETWEventSource));
#pragma warning restore SYSLIB0050
                typeof(ETWEventSource)
                    .GetField("<SessionStartTime>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(source, new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                typeof(ETWEventSource)
                    .GetField("<OriginTimeZone>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(source, TimeZoneInfo.Utc);
                return source;
            }
        }

        private sealed class TestEtwEventRecord : ETWEventSource.ETWEventRecord
        {
            private readonly double m_time;

            public TestEtwEventRecord(ETWEventSource source, double time, EventIndex index)
                : base(source)
            {
                m_time = time;
                m_displayFields = new string[12];
                typeof(ETWEventSource.ETWEventRecord)
                    .GetField("m_idx", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(this, index);
            }

            public override string EventName => "Test/Event";
            public override string ProcessName => "test";
            public override double TimeStampRelatveMSec => m_time;
            public override string Rest { get => string.Empty; set { } }
            public override List<Payload> Payloads { get; } = new List<Payload>();
        }

        private sealed class SelectedStacksStackSource : StackSource
        {
            private readonly List<StackSourceSample> m_samples = new List<StackSourceSample>();

            public SelectedStacksStackSource(double startTime, double endTime, Predicate<TraceEvent> predicate)
            {
                var candidates = new[]
                {
                    new { Time = 10.0, Index = (EventIndex)1 },
                    new { Time = 20.0, Index = (EventIndex)2 },
                    new { Time = 25.0, Index = (EventIndex)3 },
                    new { Time = 30.0, Index = (EventIndex)4 },
                    new { Time = 30.0, Index = (EventIndex)5 },
                };

                foreach (var candidate in candidates)
                {
                    if (candidate.Time >= startTime &&
                        candidate.Time <= endTime &&
                        (predicate == null || predicate(new TestTraceEvent(candidate.Index))))
                    {
                        m_samples.Add(new StackSourceSample(this)
                        {
                            SampleIndex = (StackSourceSampleIndex)m_samples.Count,
                            StackIndex = StackSourceCallStackIndex.Start,
                            Metric = 1,
                            TimeRelativeMSec = candidate.Time,
                        });
                    }
                }
            }

            public override int CallStackIndexLimit => (int)StackSourceCallStackIndex.Start + 1;
            public override int CallFrameIndexLimit => (int)StackSourceFrameIndex.Start + 1;
            public override int SampleIndexLimit => m_samples.Count;
            public override double SampleTimeRelativeMSecLimit => m_samples.LastOrDefault()?.TimeRelativeMSec ?? 0;
            public override void ForEach(Action<StackSourceSample> callback) => m_samples.ForEach(callback);
            public override StackSourceSample GetSampleByIndex(StackSourceSampleIndex sampleIndex) => m_samples[(int)sampleIndex];
            public override StackSourceCallStackIndex GetCallerIndex(StackSourceCallStackIndex callStackIndex) => StackSourceCallStackIndex.Invalid;
            public override StackSourceFrameIndex GetFrameIndex(StackSourceCallStackIndex callStackIndex) => StackSourceFrameIndex.Start;
            public override string GetFrameName(StackSourceFrameIndex frameIndex, bool verboseName) => "Selected event stack";
        }

        private sealed class TestTraceEvent : TraceEvent
        {
            private static readonly FieldInfo EventIndexField = typeof(TraceEvent)
                .GetField("eventIndex", BindingFlags.Instance | BindingFlags.NonPublic);

            public TestTraceEvent(EventIndex index)
                : base(0, 0, "Test", Guid.Empty, 0, "Info", Guid.Empty, "Test")
            {
                EventIndexField.SetValue(this, index);
            }

            public override string[] PayloadNames { get; } = Array.Empty<string>();
            public override object PayloadValue(int index) => throw new ArgumentOutOfRangeException(nameof(index));
            protected override Delegate Target { get; set; }
        }
    }
}
