using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using System.IO;

namespace Tests
{
    [TestClass]
    public class CtfTraceTests
    {
        [TestMethod]
        public void GCAllocationTick()
        {
            string path = new DirectoryInfo(Environment.CurrentDirectory).Parent.Parent.FullName;
            path = Path.Combine(path, "auto-20151103-132930.lttng.zip");

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
    }
}
