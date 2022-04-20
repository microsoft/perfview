using System;
using System.IO;
using Microsoft.Diagnostics.Tracing;
using Xunit;

namespace TraceEventTests
{
    public class Issue1618
    {
        [Fact]
        public void DynamicParserStructSerialization()
        {
            string expectedValue = "{ \"b\":\"Hello\", \"c\":\"World!\" }";
            string inputTraceFile = Path.Combine("inputs", "Regression", "SelfDescribingSingleEvent.etl");

            using(ETWTraceEventSource source = new ETWTraceEventSource(inputTraceFile))
            {
                source.Dynamic.AddCallbackForProviderEvent("MySource", "TestEvent", (data) =>
                {
                    string jsonStructValue = data.PayloadValue(0).ToString();
                    Assert.Equal(expectedValue, jsonStructValue);
                });

                source.Process();
            }
        }
    }
}
