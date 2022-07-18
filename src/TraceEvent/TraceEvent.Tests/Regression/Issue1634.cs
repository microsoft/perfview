using System;
using System.IO;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Xunit;
using Xunit.Abstractions;

namespace TraceEventTests
{
    public class Issue1634 : TestBase
    {
        public Issue1634(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public void TraceEventSourceBasicProperties()
        {
            Directory.CreateDirectory(OutputDir);

            string inputTraceFile = Path.Combine("inputs", "Regression", "SelfDescribingSingleEvent.etl");
            string etlxFilePath = Path.Combine(OutputDir, Path.GetFileNameWithoutExtension(inputTraceFile) + ".etlx");

            using (ETWTraceEventSource source = new ETWTraceEventSource(inputTraceFile))
            {
                etlxFilePath = TraceLog.CreateFromEventTraceLogFile(source, etlxFilePath);
                using (TraceLog traceLog = new TraceLog(etlxFilePath))
                {
                    // Compare the raw source and the TraceLog.
                    Assert.Equal(source.pointerSize, traceLog.pointerSize);
                    Assert.Equal(source.numberOfProcessors, traceLog.numberOfProcessors);
                    Assert.Equal(source.cpuSpeedMHz, traceLog.cpuSpeedMHz);
                    //Assert.Equal(source.utcOffsetMinutes, traceLog.utcOffsetMinutes);
                    Assert.Equal(source.osVersion, traceLog.osVersion);
                    Assert.Equal(source._QPCFreq, traceLog._QPCFreq);
                    Assert.Equal(source._syncTimeQPC, traceLog._syncTimeQPC);
                    Assert.Equal(source._syncTimeUTC, traceLog._syncTimeUTC);
                    Assert.Equal(source.sessionStartTimeQPC, traceLog.sessionStartTimeQPC);

                    // These are not guaranteed to be the same due to bookkeeping events being removed.
                    // Assert.Equal(source.sessionEndTimeQPC, traceLog.sessionEndTimeQPC);
                    
                    Assert.Equal(source.useClassicETW, traceLog.useClassicETW);

                    // Compare the TraceLog and the TraceLogEventSource.
                    TraceLogEventSource traceLogEventSource = traceLog.Events.GetSource();

                    Assert.Equal(traceLog.pointerSize, traceLogEventSource.pointerSize);
                    Assert.Equal(traceLog.numberOfProcessors, traceLogEventSource.numberOfProcessors);
                    Assert.Equal(traceLog.cpuSpeedMHz, traceLogEventSource.cpuSpeedMHz);
                    Assert.Equal(traceLog.utcOffsetMinutes, traceLogEventSource.utcOffsetMinutes);
                    Assert.Equal(traceLog.osVersion, traceLogEventSource.osVersion);
                    Assert.Equal(traceLog._QPCFreq, traceLogEventSource._QPCFreq);
                    Assert.Equal(traceLog._syncTimeQPC, traceLogEventSource._syncTimeQPC);
                    Assert.Equal(traceLog._syncTimeUTC, traceLogEventSource._syncTimeUTC);
                    Assert.Equal(traceLog.sessionStartTimeQPC, traceLogEventSource.sessionStartTimeQPC);
                    Assert.Equal(traceLog.sessionEndTimeQPC, traceLogEventSource.sessionEndTimeQPC);
                    Assert.Equal(traceLog.useClassicETW, traceLogEventSource.useClassicETW);
                }
            }
        }
    }
}
