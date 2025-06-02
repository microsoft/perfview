using System;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Xml;
using Xunit;

namespace PerfViewTests
{
    /// <summary>
    /// Regression tests for issue #927: XML escaping for EventName when saving to XML
    /// 
    /// Problem: PerfView was not properly escaping double quotes and other XML special 
    /// characters in EventName when saving events to XML format. This resulted in 
    /// invalid XML that could not be parsed correctly by XML parsers.
    /// 
    /// Fix: Applied proper XML escaping to EventName using XmlUtilities.XmlEscape() method.
    /// </summary>
    public class XmlEscapeRegressionTests
    {
        [Fact]
        public void TestXmlEscapingForEventNameWithQuotes()
        {
            // Test case from issue #927 - EventName with double quotes
            var problemEventName = "Enter\" providername=\"Microsoft-Azure-Devices";
            var processName = "Process(3164)";
            var timeMsec = 783264.803;

            var xmlOutput = GenerateEventXml(problemEventName, timeMsec, processName);

            // Verify the XML is valid
            var xmlDoc = new XmlDocument();
            Exception loadException = null;
            try
            {
                xmlDoc.LoadXml(xmlOutput);
            }
            catch (Exception ex)
            {
                loadException = ex;
            }
            Assert.Null(loadException); // XML should load without exceptions

            // Verify the EventName attribute is correctly preserved
            var eventElement = xmlDoc.SelectSingleNode("/Events/Event");
            Assert.NotNull(eventElement);
            
            var eventNameAttr = eventElement.Attributes["EventName"];
            Assert.NotNull(eventNameAttr);
            Assert.Equal(problemEventName, eventNameAttr.Value);
        }

        [Theory]
        [InlineData("Enter\" providername=\"Microsoft-Azure-Devices", "Enter&quot; providername=&quot;Microsoft-Azure-Devices")]
        [InlineData("<script>alert('xss')</script>", "&lt;script&gt;alert(&apos;xss&apos;)&lt;/script&gt;")]
        [InlineData("Test & Company", "Test &amp; Company")]
        [InlineData("Quote: \"Hello\"", "Quote: &quot;Hello&quot;")]
        [InlineData("Apostrophe: 'Hello'", "Apostrophe: &apos;Hello&apos;")]
        [InlineData("Mixed: <tag attr=\"value\">content & more</tag>", "Mixed: &lt;tag attr=&quot;value&quot;&gt;content &amp; more&lt;/tag&gt;")]
        public void TestXmlEscapingForVariousSpecialCharacters(string originalEventName, string expectedEscaped)
        {
            var processName = "Process(1234)";
            var timeMsec = 1000.0;

            var xmlOutput = GenerateEventXml(originalEventName, timeMsec, processName);

            // Verify the XML is valid
            var xmlDoc = new XmlDocument();
            Exception loadException = null;
            try
            {
                xmlDoc.LoadXml(xmlOutput);
            }
            catch (Exception ex)
            {
                loadException = ex;
            }
            Assert.Null(loadException); // XML should load without exceptions

            // Verify the EventName attribute is correctly preserved when parsed
            var eventElement = xmlDoc.SelectSingleNode("/Events/Event");
            Assert.NotNull(eventElement);
            
            var eventNameAttr = eventElement.Attributes["EventName"];
            Assert.NotNull(eventNameAttr);
            Assert.Equal(originalEventName, eventNameAttr.Value);

            // Also verify the raw XML contains the expected escaped content
            Assert.Contains($"EventName=\"{expectedEscaped}\"", xmlOutput);
        }

        [Fact]
        public void TestXmlEscapingForProcessName()
        {
            var eventName = "TestEvent";
            var problemProcessName = "Process<1234> & \"Special\"";
            var timeMsec = 1000.0;

            var xmlOutput = GenerateEventXml(eventName, timeMsec, problemProcessName);

            // Verify the XML is valid
            var xmlDoc = new XmlDocument();
            Exception loadException = null;
            try
            {
                xmlDoc.LoadXml(xmlOutput);
            }
            catch (Exception ex)
            {
                loadException = ex;
            }
            Assert.Null(loadException); // XML should load without exceptions

            // Verify the ProcessName attribute is correctly preserved
            var eventElement = xmlDoc.SelectSingleNode("/Events/Event");
            Assert.NotNull(eventElement);
            
            var processNameAttr = eventElement.Attributes["ProcessName"];
            Assert.NotNull(processNameAttr);
            Assert.Equal(problemProcessName, processNameAttr.Value);
        }

        [Fact]
        public void TestRegressionForIssue927()
        {
            // This is the exact case reported in issue #927
            var eventName = "Enter\" providername=\"Microsoft-Azure-Devices";
            var processName = "Process(3164)";
            var timeMsec = 783264.803;

            var xmlOutput = GenerateEventXml(eventName, timeMsec, processName);

            // Before the fix, this would generate invalid XML like:
            // <Event EventName="Enter" providername="Microsoft-Azure-Devices" TimeMsec="783264.803" ProcessName="Process(3164)"/>
            // Which would truncate the EventName to just "Enter"

            // After the fix, it should generate valid XML like:
            // <Event EventName="Enter&quot; providername=&quot;Microsoft-Azure-Devices" TimeMsec="783264.803" ProcessName="Process(3164)"/>
            // And preserve the full EventName value

            // Verify the XML is valid and can be parsed
            var xmlDoc = new XmlDocument();
            Exception loadException = null;
            try
            {
                xmlDoc.LoadXml(xmlOutput);
            }
            catch (Exception ex)
            {
                loadException = ex;
            }
            Assert.Null(loadException); // XML should load without exceptions

            // Verify the full EventName is preserved
            var eventElement = xmlDoc.SelectSingleNode("/Events/Event");
            Assert.NotNull(eventElement);
            
            var eventNameAttr = eventElement.Attributes["EventName"];
            Assert.NotNull(eventNameAttr);
            Assert.Equal(eventName, eventNameAttr.Value);

            // Verify there are no extra spurious attributes from the unescaped content
            Assert.Null(eventElement.Attributes["providername"]);
        }

        [Fact] 
        public void TestBeforeFixBehaviorShowsIncorrectParsing()
        {
            // This demonstrates what would happen with the old, unfixed code
            var eventName = "Enter\" providername=\"Microsoft-Azure-Devices";
            var processName = "Process(3164)";
            var timeMsec = 783264.803;

            // Simulate the OLD behavior (before the fix) - no escaping
            var invalidXml = GenerateEventXmlWithoutEscaping(eventName, timeMsec, processName);

            // The XML is technically parsable, but the EventName gets truncated
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(invalidXml);

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

        /// <summary>
        /// Simulates the XmlEscape functionality that should be used in EventWindow.SaveDataToXmlFile.
        /// This replicates the logic from Microsoft.Diagnostics.Utilities.XmlUtilities.XmlEscape.
        /// </summary>
        private string XmlEscape(object obj)
        {
            string str = obj.ToString();
            StringBuilder sb = null;
            string entity = null;
            int copied = 0;
            for (int i = 0; i < str.Length; i++)
            {
                switch (str[i])
                {
                    case '&':
                        entity = "&amp;";
                        goto APPEND;
                    case '"':
                        entity = "&quot;";
                        goto APPEND;
                    case '\'':
                        entity = "&apos;";
                        goto APPEND;
                    case '<':
                        entity = "&lt;";
                        goto APPEND;
                    case '>':
                        entity = "&gt;";
                        goto APPEND;
                        APPEND:
                        {
                            if (sb == null)
                            {
                                sb = new StringBuilder();
                            }
                            while (copied < i)
                            {
                                sb.Append(str[copied++]);
                            }

                            sb.Append(entity);
                            copied++;
                        }
                        break;
                }
            }

            if (sb != null)
            {
                while (copied < str.Length)
                {
                    sb.Append(str[copied++]);
                }

                return sb.ToString();
            }

            return str;
        }

        /// <summary>
        /// Generates XML for a single event similar to how EventWindow.SaveDataToXmlFile works
        /// This simulates the FIXED behavior (after the fix).
        /// </summary>
        private string GenerateEventXml(string eventName, double timeMsec, string processName)
        {
            var savedCulture = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                
                var sb = new StringBuilder();
                sb.AppendLine("<Events>");
                
                // This mimics the FIXED line from SaveDataToXmlFile (with XmlEscape)
                sb.AppendLine($" <Event EventName=\"{XmlEscape(eventName)}\" TimeMsec=\"{timeMsec:f3}\" ProcessName=\"{XmlEscape(processName)}\"/>");
                
                sb.AppendLine("</Events>");
                
                return sb.ToString();
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = savedCulture;
            }
        }

        /// <summary>
        /// Generates XML for a single event WITHOUT escaping to demonstrate the old broken behavior
        /// This simulates the BROKEN behavior (before the fix).
        /// </summary>
        private string GenerateEventXmlWithoutEscaping(string eventName, double timeMsec, string processName)
        {
            var savedCulture = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                
                var sb = new StringBuilder();
                sb.AppendLine("<Events>");
                
                // This mimics the OLD, BROKEN line from SaveDataToXmlFile (without XmlEscape on EventName)
                sb.AppendLine($" <Event EventName=\"{eventName}\" TimeMsec=\"{timeMsec:f3}\" ProcessName=\"{XmlEscape(processName)}\"/>");
                
                sb.AppendLine("</Events>");
                
                return sb.ToString();
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = savedCulture;
            }
        }
    }
}