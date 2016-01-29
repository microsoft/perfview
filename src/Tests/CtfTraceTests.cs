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
        static string TestPath = @"C:\Users\leecu_000\Documents\GitHub\perfview\src\Tests";

        [TestMethod]
        public void GCAllocationTick()
        {
            string path = Path.Combine(TestPath, "auto-20151103-132930.lttng.zip");

            using (CtfTraceEventSource ctfSource = new CtfTraceEventSource(path))
            {
                int allocTicks = 0, allocTicksFromAll = 0;

                ctfSource.AllEvents += delegate(TraceEvent obj)
                {
                    string s = obj.ToString();
                    var d = obj.TimeStamp;
                    if (obj is GCAllocationTickTraceData)
                        allocTicksFromAll++;
                };

                ctfSource.Clr.GCAllocationTick += delegate(GCAllocationTickTraceData o) { allocTicks++; };

                ctfSource.Process();

                Assert.IsTrue(allocTicks > 0);
                Assert.AreEqual(allocTicks, allocTicksFromAll);
            }
        }

        [TestMethod]
        public void GCStartStopEvents()
        {
            string path = Path.Combine(TestPath, "auto-20151103-132930.lttng.zip");

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
