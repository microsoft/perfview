using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Parsers.LinuxKernel;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace CtfTracingTests
{
    public class CtfTraceTests
    {
        private static string TestDataDirectory = @"..\..\..\inputs";

        [Fact]
        public void LTTng_GCAllocationTick()
        {
            int allocTicks = 0, allocTicksFromAll = 0;

            //string[] files = new string[] { /*"auto-20160204-132425.trace.zip", "auto-20151103-132930.trace.zip",*/ "auto-20160204-162218.trace.zip" };
            string[] files = new string[] { "netcoreapp31.trace.zip" };
            foreach (string file in files)
            {
                string path = Path.Combine(TestDataDirectory, file);
                using (CtfTraceEventSource ctfSource = new CtfTraceEventSource(path))
                {
                    ctfSource.AllEvents += delegate (TraceEvent obj)
                    {
                        string s = obj.ToString();
                        var d = obj.TimeStamp;
                        if (obj is GCAllocationTickTraceData)
                        {
                            allocTicksFromAll++;
                        }
                    };

                    ctfSource.Clr.GCCreateSegment += delegate (GCCreateSegmentTraceData d)
                    {
                    };

                    ctfSource.Clr.RuntimeStart += delegate (RuntimeInformationTraceData d)
                    {

                    };


                    ctfSource.Clr.LoaderModuleLoad += delegate (ModuleLoadUnloadTraceData d)
                    {

                    };

                    ctfSource.Clr.MethodInliningFailed += delegate (MethodJitInliningFailedTraceData d)
                    {

                    };



                    ctfSource.Clr.MethodTailCallSucceeded += delegate (MethodJitTailCallSucceededTraceData d)
                    {

                    };


                    ctfSource.Clr.GCHeapStats += delegate (GCHeapStatsTraceData d)
                    {
                    };

                    ctfSource.Clr.GCStart += delegate (GCStartTraceData d)
                    {
                    };


                    ctfSource.Clr.GCStop += delegate (GCEndTraceData d)
                    {
                    };

                    ctfSource.Clr.GCPerHeapHistory += delegate (GCPerHeapHistoryTraceData d)
                    {
                    };

                    ctfSource.Clr.GCStart += delegate (GCStartTraceData d)
                    {

                    };

                    ctfSource.Clr.MethodILToNativeMap += delegate (MethodILToNativeMapTraceData d)
                    {

                    };

                    ctfSource.Clr.GCAllocationTick += delegate (GCAllocationTickTraceData o) { allocTicks++; };

                    ctfSource.Process();
                }
            }

            Assert.True(allocTicks > 0);
            Assert.Equal(allocTicks, allocTicksFromAll);
        }

        [Fact]
        public void LTTng_GCStartStopEvents()
        {
            string[] files = new string[] { "netcoreapp22.trace.zip", "netcoreapp31.trace.zip" };
            foreach (string file in files)
            {
                string path = Path.Combine(TestDataDirectory, file);

                using (CtfTraceEventSource ctfSource = new CtfTraceEventSource(path))
                {
                    ctfSource.AllEvents += delegate (TraceEvent obj)
                    {
                    };

                    ctfSource.Clr.GCRestartEEStart += delegate (GCNoUserDataTraceData obj)
                    {
                    };

                    ctfSource.Clr.GCRestartEEStop += delegate (GCNoUserDataTraceData obj)
                    {
                    };


                    ctfSource.Clr.GCSuspendEEStart += delegate (GCSuspendEETraceData obj)
                    {
                    };


                    ctfSource.Clr.GCSuspendEEStop += delegate (GCNoUserDataTraceData obj)
                    {
                    };


                    ctfSource.Process();
                }
            }
        }

        [Fact]
        public void LTTng_KernelEvents()
        {
            string path = Path.Combine(TestDataDirectory, "kernel-only.trace.zip");
            using (CtfTraceEventSource ctfSource = new CtfTraceEventSource(path))
            {
                var kernelParser = new LinuxKernelEventParser(ctfSource);

                int processStartCount = 0;
                int gcStartCount = 0;
                kernelParser.ProcessStart += delegate (ProcessStartTraceData data)
                {
                    processStartCount++;
                    // Check payload fields
                    Assert.True(!string.IsNullOrEmpty(data.FileName));
                    Assert.True(data.PayloadThreadID != 0);
                    Assert.True(data.OldThreadID != 0);
                };

                kernelParser.ProcessStop += delegate (ProcessStopTraceData data)
                {
                    // Check payload fields
                    Assert.True(!string.IsNullOrEmpty(data.Command));
                    Assert.True(data.PayloadThreadID != 0);
                    Assert.True(data.ThreadPriority != 0); // There's no event with priority 0 in this source
                };

                ctfSource.Clr.GCStart += delegate (GCStartTraceData data)
                {
                    gcStartCount++;
                };

                Assert.True(ctfSource.Process());

                Assert.Equal(20, processStartCount);
                Assert.Equal(0, gcStartCount);
            }
        }

        [Theory]
        [InlineData("clr-only.trace.zip")]
        [InlineData("kernel-clr.trace.zip")]
        public void LTTng_OversizedEventSourcePayloadsAreRejected(string file)
        {
            string path = Path.Combine(TestDataDirectory, file);
            using (var source = new CtfTraceEventSource(path))
            {
                int eventSourceEvents = 0;
                source.Clr.EventSourceEvent += data =>
                {
                    eventSourceEvents++;
                    Assert.False(string.IsNullOrEmpty(data.Name));
                    Assert.False(string.IsNullOrEmpty(data.EventSourceName));
                    Assert.NotNull(data.Payload);
                    Assert.Equal(data.EventDataLength, data.Clone().EventData().Length);
                };

                // These captures contain valid CTF strings whose UTF-16 payload exceeds the native 16-bit length.
                var error = Assert.Throws<InvalidDataException>(() => source.Process());
                Assert.Equal("Invalid CTF native payload length.", error.Message);
                Assert.True(eventSourceEvents > 0);
            }
        }

        [Theory]
        [InlineData("validation_001.zip")]
        [InlineData("validation_002.zip")]
        [InlineData("validation_003.zip")]
        [InlineData("validation_004.zip")]
        [InlineData("validation_005.zip")]
        [InlineData("validation_006.zip")]
        public void BasicValidationProcessesFileWithoutException(string file)
        {
            var path = Path.Combine(TestDataDirectory, file);
            using (var source = new CtfTraceEventSource(path))
            {
                var dummy = new DummyParser(source);
                source.Clr.All += evt => { };

                var count = 0;
                source.AllEvents += evt =>
                {
                    count++;
                };
                var exception = Record.Exception(() => source.Process());
                Assert.Null(exception);
                Assert.NotEqual(0, count);
            }
        }

        [Fact]
        public void ProblemAlignment()
        {
            var path = Path.Combine(TestDataDirectory, "problem_alignment.zip");
            using (var source = new CtfTraceEventSource(path))
            {
                var index = 0;
                source.Clr.EventSourceEvent += evt =>
                {
                    switch (index++)
                    {
                        case 0:
                            Assert.Equal(6668, evt.EventID);
#pragma warning disable CS0618
                            Assert.Equal(482332487730801, evt.TimeStampQPC);
#pragma warning restore CS0618
                            break;
                        case 1:
                            Assert.Equal(6668, evt.EventID);
#pragma warning disable CS0618
                            Assert.Equal(482332675464150, evt.TimeStampQPC);
#pragma warning restore CS0618
                            break;
                        default:
                            throw new InvalidOperationException();
                    }
                };
                source.Process();
                Assert.Equal(2, index);
            }
        }

        sealed class DummyParser : TraceEventParser
        {
            public DummyParser(TraceEventSource source)
                : base(source)
            { }

            protected override IEnumerable<CtfEventMapping> EnumerateCtfEventMappings()
            {
                yield return new CtfEventMapping("PerfLabGenericEventSourceLTTngProvider:Startup", Guid.Empty, 0, 0, 0);
                yield return new CtfEventMapping("PerfLabGenericEventSourceLTTngProvider:OnMain", Guid.Empty, 0, 0, 0);
            }

            protected override void EnumerateTemplates(Func<string, string, EventFilterResponse> eventsToObserve, Action<TraceEvent> callback)
            { }

            protected override string GetProviderName() => nameof(DummyParser);
        }
    }
}
