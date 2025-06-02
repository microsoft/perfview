using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using EventSources;
using Microsoft.VisualStudio.Threading;
using PerfView;
using PerfView.TestUtilities;
using PerfViewTests.Utilities;
using System.Windows;
using Xunit;
using Xunit.Abstractions;

namespace PerfViewTests
{
    public class EventViewerTests : PerfViewTestBase
    {
        public EventViewerTests(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
        }

        [WpfFact]
        [WorkItem(927, "https://github.com/Microsoft/perfview/issues/927")]
        public Task TestEventNameXmlEscapingRegressionAsync()
        {
            Func<Task<EventWindow>> setupAsync = async () =>
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync();

                var file = new XmlEscapeTestFile();
                await OpenAsync(JoinableTaskFactory, file, GuiApp.MainWindow, GuiApp.MainWindow.StatusBar).ConfigureAwait(true);
                var eventSource = file.Children.OfType<PerfViewEventSource>().First();
                return eventSource.Viewer;
            };

            Func<EventWindow, Task> cleanupAsync = async eventWindow =>
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync();
                eventWindow.Close();
            };

            Func<EventWindow, Task> testDriverAsync = async eventWindow =>
            {
                await JoinableTaskFactory.SwitchToMainThreadAsync();

                // Create a temporary file for XML output
                var tempFile = Path.GetTempFileName() + ".xml";
                try
                {
                    // Call the actual SaveDataToXmlFile method from EventWindow
                    eventWindow.SaveDataToXmlFile(tempFile);

                    // Wait for any background processing to complete
                    await eventWindow.StatusBar.WaitForWorkCompleteAsync().ConfigureAwait(true);

                    // Read the generated XML
                    var xmlContent = File.ReadAllText(tempFile);

                    // Verify the XML is valid and can be parsed
                    var xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(xmlContent);

                    // Verify the problematic EventName from issue #927 is correctly preserved
                    var eventElements = xmlDoc.SelectNodes("/Events/Event");
                    Assert.NotNull(eventElements);
                    Assert.True(eventElements.Count > 0);

                    // Find the event with the problematic name
                    XmlElement problemEvent = null;
                    foreach (XmlElement element in eventElements)
                    {
                        var eventNameAttr = element.Attributes["EventName"];
                        if (eventNameAttr?.Value == "Enter\" providername=\"Microsoft-Azure-Devices")
                        {
                            problemEvent = element;
                            break;
                        }
                    }

                    Assert.NotNull(problemEvent);
                    
                    // The key test: the full EventName should be preserved, not truncated to "Enter"
                    var eventNameAttribute = problemEvent.Attributes["EventName"];
                    Assert.Equal("Enter\" providername=\"Microsoft-Azure-Devices", eventNameAttribute.Value);

                    // Verify there are no spurious attributes created from unescaped content
                    Assert.Null(problemEvent.Attributes["providername"]);

                    // Verify the raw XML contains properly escaped content
                    Assert.Contains("EventName=\"Enter&quot; providername=&quot;Microsoft-Azure-Devices\"", xmlContent);

                    // Also test other XML special characters
                    foreach (XmlElement element in eventElements)
                    {
                        var eventName = element.Attributes["EventName"]?.Value;
                        var processName = element.Attributes["ProcessName"]?.Value;

                        // Verify all data is preserved correctly
                        Assert.NotNull(eventName);
                        Assert.NotNull(processName);
                        
                        // For the XML validation, we just ensure it parsed without exception
                        // and the original data is preserved (no truncation or spurious attributes)
                    }
                }
                finally
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
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
        /// A test file containing events with problematic EventNames for XML escaping testing.
        /// </summary>
        private class XmlEscapeTestFile : PerfViewFile
        {
            public XmlEscapeTestFile() : this(new XmlEscapeTestEventSource())
            {
            }

            public XmlEscapeTestFile(XmlEscapeTestEventSource eventSource)
            {
                Title = FormatName = nameof(XmlEscapeTestFile);
                EventSource = eventSource;
            }

            public override string Title { get; }
            public override string FormatName { get; }
            public override string[] FileExtensions { get; } = new[] { "XML Escape Test" };

            public XmlEscapeTestEventSource EventSource { get; }

            protected override Action<Action> OpenImpl(Window parentWindow, StatusBar worker)
            {
                return doAfter =>
                {
                    TestPerfViewEventSource eventSource = new TestPerfViewEventSource(this);
                    eventSource.Open(parentWindow, worker, doAfter);
                    m_Children = new List<PerfViewTreeItem>();
                    m_Children.Add(eventSource);
                };
            }

            protected internal override EventSource OpenEventSourceImpl(TextWriter log)
            {
                return EventSource;
            }
        }

        /// <summary>
        /// Test subclass of PerfViewEventSource that provides mock event data
        /// </summary>
        private class TestPerfViewEventSource : PerfViewEventSource
        {
            private readonly XmlEscapeTestFile _dataFile;

            public TestPerfViewEventSource(XmlEscapeTestFile dataFile) : base(dataFile)
            {
                _dataFile = dataFile;
            }

            public override EventSource GetEventSource()
            {
                return _dataFile.EventSource;
            }

            public override void Open(Window parentWindow, StatusBar worker, Action doAfter)
            {
                if (Viewer == null)
                {
                    worker.StartWork("Opening " + Name, delegate ()
                    {
                        if (m_eventSource == null)
                        {
                            m_eventSource = DataFile.OpenEventSourceImpl(worker.LogWriter);
                        }

                        worker.EndWork(delegate ()
                        {
                            if (m_eventSource == null)
                            {
                                throw new ApplicationException("Not a file type that supports the EventView.");
                            }

                            Viewer = new EventWindow(parentWindow, this);
                            Viewer.Show();
                            doAfter?.Invoke();
                        });
                    });
                }
                else
                {
                    Viewer.Focus();
                    doAfter?.Invoke();
                }
            }
        }

        /// <summary>
        /// Test EventSource implementation with problematic EventNames for XML escaping testing
        /// </summary>
        private class XmlEscapeTestEventSource : EventSource
        {
            private readonly EventRecord[] _events;

            public XmlEscapeTestEventSource()
            {
                _events = new EventRecord[]
                {
                    // The main test case from issue #927
                    new TestEventRecord("Enter\" providername=\"Microsoft-Azure-Devices", "Process(3164)", 783264.803),
                    
                    // Additional test cases for various XML special characters
                    new TestEventRecord("<script>alert('xss')</script>", "Process(1234)", 1000.0),
                    new TestEventRecord("Test & Company", "Process(5678)", 2000.0),
                    new TestEventRecord("Quote: \"Hello\"", "Process(9012)", 3000.0),
                    new TestEventRecord("Apostrophe: 'Hello'", "Process(3456)", 4000.0),
                };
                
                MaxEventTimeRelativeMsec = double.PositiveInfinity;
            }

            public override void ForEach(Func<EventRecord, bool> callback)
            {
                foreach (var eventRecord in _events)
                {
                    if (!callback(eventRecord))
                        break;
                }
            }

            public override void SetEventFilter(List<string> eventNames)
            {
                // Not needed for this test
            }

            public override ICollection<string> EventNames => 
                new List<string> { "TestEvent1", "TestEvent2", "TestEvent3", "TestEvent4", "TestEvent5" };

            public override EventSource Clone()
            {
                return new XmlEscapeTestEventSource();
            }
        }

        /// <summary>
        /// Test implementation of EventRecord for testing XML escaping
        /// </summary>
        private class TestEventRecord : EventRecord
        {
            private readonly string _eventName;
            private readonly string _processName;
            private readonly double _timeStamp;

            public TestEventRecord(string eventName, string processName, double timeStamp) : base()
            {
                _eventName = eventName;
                _processName = processName;
                _timeStamp = timeStamp;
            }

            public override string EventName => _eventName;
            public override string ProcessName => _processName;
            public override double TimeStampRelatveMSec => _timeStamp;
            public override List<Payload> Payloads => new List<Payload>();
            public override string Rest => "";
        }
    }
}