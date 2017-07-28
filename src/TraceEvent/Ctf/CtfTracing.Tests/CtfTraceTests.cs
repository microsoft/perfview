using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using System.IO;
using Xunit;

namespace Tests
{
    public class CtfTraceTests
    {
        private const string TestDataDirectory = @"inputs";

        [Fact]
        public void LTTng_GCAllocationTick()
        {
            int allocTicks = 0, allocTicksFromAll = 0;

            var path = Path.Combine(TestDataDirectory, "auto-20170728-130015.trace.zip");
            using (var ctfSource = new CtfTraceEventSource(path))
            {
                ctfSource.AllEvents += delegate (TraceEvent obj)
                {
                    if (obj is GCAllocationTickTraceData)
                        allocTicksFromAll++;
                };

                ctfSource.Clr.GCAllocationTick += delegate (GCAllocationTickTraceData o) { allocTicks++; };

                ctfSource.Process();
            }
            Assert.True(allocTicks > 0);
            Assert.Equal(allocTicks, allocTicksFromAll);
        }

        [Fact]
        public void LTTng_GCStartStopEvents()
        {
            var path = Path.Combine(TestDataDirectory, "auto-20170728-131434.trace.zip");
            int startEvents = 0, startEventsFromAll = 0;
            int stopEvents = 0, stopEventsFromAll = 0;

            using (var ctfSource = new CtfTraceEventSource(path))
            {
                ctfSource.AllEvents += delegate (TraceEvent obj)
                {
                    if (obj is GCStartTraceData)
                        startEventsFromAll++;
                    if (obj is GCEndTraceData)
                        stopEventsFromAll++;
                };

                ctfSource.Clr.GCStart += delegate (GCStartTraceData obj)
                {
                    startEvents++;
                };

                ctfSource.Clr.GCStop += delegate (GCEndTraceData obj)
                {
                    stopEvents++;
                };

                ctfSource.Process();
            }

            Assert.True(startEvents > 0);
            Assert.Equal(startEvents, startEventsFromAll);

            Assert.True(stopEvents > 0);
            Assert.Equal(stopEvents, stopEventsFromAll);
        }
    }
}
