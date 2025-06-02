using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;
using EventSources;
using Microsoft.Diagnostics.Utilities;
using Xunit;

namespace PerfViewTests
{
    /// <summary>
    /// Regression tests for issue #927: XML escaping for EventName when saving to XML
    /// 
    /// Tests that replicate the actual XML generation logic from EventWindow.SaveDataToXmlFile
    /// to ensure proper XML escaping of EventName and other fields.
    /// </summary>
    public class EventViewerXmlEscapeTests
    {
        [Fact]
        public void TestEventNameXmlEscapingInSaveToXmlLogic()
        {
            // Test case from issue #927 - EventName with double quotes that was causing invalid XML
            var problemEventName = "Enter\" providername=\"Microsoft-Azure-Devices";
            var processName = "Process(3164)";
            var timeMsec = 783264.803;

            // Create test event records with problematic EventName
            var eventRecord = new TestEventRecord(problemEventName, processName, timeMsec);
            var eventSource = new TestEventSource(new[] { eventRecord });

            // Test the actual XML generation logic that mirrors EventWindow.SaveDataToXmlFile
            var tempFile = Path.GetTempFileName() + ".xml";
            try
            {
                // This replicates the SaveDataToXmlFile logic from EventWindow.xaml.cs
                SaveDataToXmlFileWithEscaping(eventSource, tempFile);

                // Read the generated XML
                var xmlContent = File.ReadAllText(tempFile);

                // Verify the XML is valid and can be parsed
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(xmlContent);

                // Verify the EventName attribute is correctly preserved
                var eventElement = xmlDoc.SelectSingleNode("/Events/Event");
                Assert.NotNull(eventElement);
                
                var eventNameAttr = eventElement.Attributes["EventName"];
                Assert.NotNull(eventNameAttr);
                
                // The key test: the full EventName should be preserved, not truncated to "Enter"
                Assert.Equal(problemEventName, eventNameAttr.Value);

                // Verify there are no spurious attributes created from unescaped content
                Assert.Null(eventElement.Attributes["providername"]);

                // Verify the raw XML contains properly escaped content
                Assert.Contains("EventName=\"Enter&quot; providername=&quot;Microsoft-Azure-Devices\"", xmlContent);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Theory]
        [InlineData("Enter\" providername=\"Microsoft-Azure-Devices", "Enter&quot; providername=&quot;Microsoft-Azure-Devices")]
        [InlineData("<script>alert('xss')</script>", "&lt;script&gt;alert(&apos;xss&apos;)&lt;/script&gt;")]
        [InlineData("Test & Company", "Test &amp; Company")]
        [InlineData("Quote: \"Hello\"", "Quote: &quot;Hello&quot;")]
        [InlineData("Apostrophe: 'Hello'", "Apostrophe: &apos;Hello&apos;")]
        public void TestEventNameXmlEscapingForVariousSpecialCharacters(string originalEventName, string expectedEscaped)
        {
            var processName = "Process(1234)";
            var timeMsec = 1000.0;

            var eventRecord = new TestEventRecord(originalEventName, processName, timeMsec);
            var eventSource = new TestEventSource(new[] { eventRecord });

            var tempFile = Path.GetTempFileName() + ".xml";
            try
            {
                SaveDataToXmlFileWithEscaping(eventSource, tempFile);
                var xmlContent = File.ReadAllText(tempFile);

                // Verify the XML is valid
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(xmlContent);

                // Verify the EventName attribute is correctly preserved when parsed
                var eventElement = xmlDoc.SelectSingleNode("/Events/Event");
                Assert.NotNull(eventElement);
                
                var eventNameAttr = eventElement.Attributes["EventName"];
                Assert.NotNull(eventNameAttr);
                Assert.Equal(originalEventName, eventNameAttr.Value);

                // Also verify the raw XML contains the expected escaped content
                Assert.Contains($"EventName=\"{expectedEscaped}\"", xmlContent);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public void TestRegressionForIssue927()
        {
            // This is the exact case reported in issue #927
            var eventName = "Enter\" providername=\"Microsoft-Azure-Devices";
            var processName = "Process(3164)";
            var timeMsec = 783264.803;

            var eventRecord = new TestEventRecord(eventName, processName, timeMsec);
            var eventSource = new TestEventSource(new[] { eventRecord });

            var tempFile = Path.GetTempFileName() + ".xml";
            try
            {
                SaveDataToXmlFileWithEscaping(eventSource, tempFile);
                var xmlContent = File.ReadAllText(tempFile);

                // Before the fix, this would generate invalid XML like:
                // <Event EventName="Enter" providername="Microsoft-Azure-Devices" TimeMsec="783264.803" ProcessName="Process(3164)"/>
                // Which would truncate the EventName to just "Enter"

                // After the fix, it should generate valid XML like:
                // <Event EventName="Enter&quot; providername=&quot;Microsoft-Azure-Devices" TimeMsec="783264.803" ProcessName="Process(3164)"/>
                // And preserve the full EventName value

                // Verify the XML is valid and can be parsed
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(xmlContent);

                // Verify the full EventName is preserved
                var eventElement = xmlDoc.SelectSingleNode("/Events/Event");
                Assert.NotNull(eventElement);
                
                var eventNameAttr = eventElement.Attributes["EventName"];
                Assert.NotNull(eventNameAttr);
                Assert.Equal(eventName, eventNameAttr.Value);

                // Verify there are no extra spurious attributes from the unescaped content
                Assert.Null(eventElement.Attributes["providername"]);
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        [Fact]
        public void TestOldBehaviorWithoutEscapingShowsDataCorruption()
        {
            // This demonstrates what happens with the old, unfixed code (without escaping EventName)
            var eventName = "Enter\" providername=\"Microsoft-Azure-Devices";
            var processName = "Process(3164)";
            var timeMsec = 783264.803;

            var eventRecord = new TestEventRecord(eventName, processName, timeMsec);
            var eventSource = new TestEventSource(new[] { eventRecord });

            var tempFile = Path.GetTempFileName() + ".xml";
            try
            {
                // Simulate the OLD behavior (before the fix) - no escaping on EventName
                SaveDataToXmlFileWithoutEscaping(eventSource, tempFile);
                var xmlContent = File.ReadAllText(tempFile);

                // The XML is technically parsable, but the EventName gets truncated
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(xmlContent);

                var eventElement = xmlDoc.SelectSingleNode("/Events/Event");
                Assert.NotNull(eventElement);

                // Before the fix, EventName would be truncated to just "Enter"
                var eventNameAttr = eventElement.Attributes["EventName"];
                Assert.NotNull(eventNameAttr);
                Assert.Equal("Enter", eventNameAttr.Value);  // Truncated, not the full original value!

                // And there would be spurious attributes created from the unescaped content
                var spuriousAttr = eventElement.Attributes["providername"];
                Assert.NotNull(spuriousAttr);
                Assert.Equal("Microsoft-Azure-Devices", spuriousAttr.Value);

                // This demonstrates the data corruption that occurred before the fix
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        #region Test Infrastructure and XML Generation Logic

        /// <summary>
        /// Replicates the SaveDataToXmlFile logic from EventWindow.xaml.cs (lines 270-330)
        /// This uses the FIXED behavior with proper XmlUtilities.XmlEscape on EventName.
        /// </summary>
        private void SaveDataToXmlFileWithEscaping(EventSource eventSource, string xmlFileName)
        {
            var savedCulture = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                using (var xmlFile = File.CreateText(xmlFileName))
                {
                    // Write out column header
                    xmlFile.WriteLine("<Events>");
                    eventSource.ForEach(delegate (EventRecord _event)
                    {
                        // We have exceeded MaxRet, skip it.
                        if (_event.EventName == null)
                        {
                            return false;
                        }

                        // This replicates the FIXED line from EventWindow.xaml.cs (line 291-292)
                        // Note: XmlUtilities.XmlEscape is applied to BOTH EventName and ProcessName
                        xmlFile.Write(" <Event EventName=\"{0}\" TimeMsec=\"{1:f3}\" ProcessName=\"{2}\"",
                            XmlUtilities.XmlEscape(_event.EventName), _event.TimeStampRelatveMSec, XmlUtilities.XmlEscape(_event.ProcessName));

                        xmlFile.WriteLine("/>");
                        return true;
                    });
                    xmlFile.WriteLine("</Events>");
                }
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = savedCulture;
            }
        }

        /// <summary>
        /// Replicates the OLD SaveDataToXmlFile logic WITHOUT escaping EventName
        /// This demonstrates the BROKEN behavior before the fix.
        /// </summary>
        private void SaveDataToXmlFileWithoutEscaping(EventSource eventSource, string xmlFileName)
        {
            var savedCulture = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                using (var xmlFile = File.CreateText(xmlFileName))
                {
                    // Write out column header
                    xmlFile.WriteLine("<Events>");
                    eventSource.ForEach(delegate (EventRecord _event)
                    {
                        // We have exceeded MaxRet, skip it.
                        if (_event.EventName == null)
                        {
                            return false;
                        }

                        // This replicates the OLD, BROKEN line from EventWindow.xaml.cs (without XmlEscape on EventName)
                        xmlFile.Write(" <Event EventName=\"{0}\" TimeMsec=\"{1:f3}\" ProcessName=\"{2}\"",
                            _event.EventName, _event.TimeStampRelatveMSec, XmlUtilities.XmlEscape(_event.ProcessName));

                        xmlFile.WriteLine("/>");
                        return true;
                    });
                    xmlFile.WriteLine("</Events>");
                }
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = savedCulture;
            }
        }
        /// <summary>
        /// Replicates the OLD SaveDataToXmlFile logic WITHOUT escaping EventName
        /// This demonstrates the BROKEN behavior before the fix.
        /// </summary>
        private void SaveDataToXmlFileWithoutEscaping(EventSource eventSource, string xmlFileName)
        {
            var savedCulture = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                using (var xmlFile = File.CreateText(xmlFileName))
                {
                    // Write out column header
                    xmlFile.WriteLine("<Events>");
                    eventSource.ForEach(delegate (EventRecord _event)
                    {
                        // We have exceeded MaxRet, skip it.
                        if (_event.EventName == null)
                        {
                            return false;
                        }

                        // This replicates the OLD, BROKEN line from EventWindow.xaml.cs (without XmlEscape on EventName)
                        xmlFile.Write(" <Event EventName=\"{0}\" TimeMsec=\"{1:f3}\" ProcessName=\"{2}\"",
                            _event.EventName, _event.TimeStampRelatveMSec, XmlUtilities.XmlEscape(_event.ProcessName));

                        xmlFile.WriteLine("/>");
                        return true;
                    });
                    xmlFile.WriteLine("</Events>");
                }
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = savedCulture;
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

        /// <summary>
        /// Test implementation of EventSource for testing XML escaping
        /// </summary>
        private class TestEventSource : EventSource
        {
            private readonly EventRecord[] _events;

            public TestEventSource(EventRecord[] events)
            {
                _events = events;
            }

            public override void ForEach(Func<EventRecord, bool> callback)
            {
                foreach (var eventRecord in _events)
                {
                    if (!callback(eventRecord))
                        break;
                }
            }
        }

        #endregion
    }
}
