using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Parsers.ClrPrivate;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Parsers.LinuxKernel;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Tests
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
            // test trace files with kernel events only, with clr events only or with both types of events presents
            string[] files = new string[] { "kernel-only.trace.zip", "clr-only.trace.zip", "kernel-clr.trace.zip" };
            foreach(string file in files)
            {
                string path = Path.Combine(TestDataDirectory, file);
                using (CtfTraceEventSource ctfSource = new CtfTraceEventSource(path))
                {
                    var kernelParser = new LinuxKernelEventParser(ctfSource);

                    var procStartList = new List<TraceEvent>();
                    var gcList = new List<TraceEvent>();
                    kernelParser.ProcessStart += delegate (ProcessStartTraceData data)
                    {
                        procStartList.Add(data.Clone());
                    };

                    ctfSource.Clr.GCStart += delegate (GCStartTraceData data)
                    {
                        gcList.Add(data.Clone());
                    };
                    ctfSource.Process();
                }
            }
        }
    }
}
