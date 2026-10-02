using Microsoft.Diagnostics.Tracing;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace TraceEventTests
{
    public class ReloggerTests : TestBase
    {
        public ReloggerTests(ITestOutputHelper output) : base(output)
        {
        }

        [WindowsFact]
        public void RelogsCompressedEvents()
        {
            VerifyRelogging(true);
        }

        [WindowsFact]
        public void RelogsUncompressedEvents()
        {
            VerifyRelogging(false);
        }

        private void VerifyRelogging(bool compressed)
        {
            Directory.CreateDirectory(OutputDir);
            string input = Path.Combine("inputs", "Regression", "SelfDescribingSingleEvent.etl");
            string output = Path.Combine(OutputDir, "relogged.etl");
            var expected = new List<string>();
            var providers = new HashSet<Guid>();
            using (var source = new ETWReloggerTraceEventSource(input, TraceEventSourceType.FileOnly, output))
            {
                source.OutputUsesCompressedFormat = compressed;
                source.AllEvents += data =>
                {
                    if (!data.IsClassicProvider)
                    {
                        providers.Add(data.ProviderGuid);
                        expected.Add(EventSignature(data));
                    }
                    source.WriteEvent(data);
                };
                source.Process();
            }

            Assert.NotEmpty(expected);
            var actual = new List<string>();
            using (var source = new ETWTraceEventSource(output))
            {
                source.AllEvents += data =>
                {
                    if (providers.Contains(data.ProviderGuid))
                    {
                        actual.Add(EventSignature(data));
                    }
                };
                source.Process();
            }
            Assert.Equal(expected, actual);
        }

        private static string EventSignature(TraceEvent data)
        {
            return data.ProviderGuid + ":" + data.ID + ":" + data.Opcode + ":" + Convert.ToBase64String(data.EventData());
        }
    }
}
