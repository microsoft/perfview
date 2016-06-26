using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using System.IO;
using System.Reflection;

namespace Tests
{
    [TestClass]
    public class CtfTraceTests
    {
        static string TestDataDirectory = @"..\..\inputs";

        [TestMethod]
        public void GCAllocationTick()
        {
            Assert.Inconclusive();
            return;

            int allocTicks = 0, allocTicksFromAll = 0;

            string[] files = new string[] { "auto-20160204-132425.lttng.zip", "auto-20151103-132930.lttng.zip" , "auto-20160204-162218.lttng.zip" };
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
                            allocTicksFromAll++;
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

            Assert.IsTrue(allocTicks > 0);
            Assert.AreEqual(allocTicks, allocTicksFromAll);
        }

        [TestMethod]
        public void GCStartStopEvents()
        {
            Assert.Inconclusive("Issue https://github.com/Microsoft/perfview/issues/102 track this.");

            string path = Path.Combine(TestDataDirectory, "auto-20151103-132930.lttng.zip");

            using (CtfTraceEventSource ctfSource = new CtfTraceEventSource(path))
            {
                ctfSource.AllEvents += delegate(TraceEvent obj)
                {
                };

                ctfSource.Clr.GCRestartEEStart += delegate(GCNoUserDataTraceData obj)
                {
                };

                ctfSource.Clr.GCRestartEEStop += delegate(GCNoUserDataTraceData obj)
                {
                };


                ctfSource.Clr.GCSuspendEEStart += delegate(GCSuspendEETraceData obj)
                {
                };


                ctfSource.Clr.GCSuspendEEStop += delegate(GCNoUserDataTraceData obj)
                {
                };
                

                ctfSource.Process();
            }
        }
    }
}
