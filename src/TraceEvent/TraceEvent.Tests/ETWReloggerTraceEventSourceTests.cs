using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Diagnostics.Eventing;
using System.IO;

namespace TraceEventTests
{
    [TestClass]
    public class ETWReloggerTraceEventSourceTests
    {
        // Creates an ETL from an exsiting one with the full paths replaced with the file name.
        [TestMethod]
        [DeploymentItem("smallEtlFile.etl")]
        public void ETWReloggerTraceEventSource_AbilityToChangeTheContentsOfPayloads()
        {
            // Remove the file paths in the ModuleILPath payload. There is only one event in this ETL trace.
            using (var relogger = new ETWReloggerTraceEventSource("smallEtlFile.etl", "filtered.etl"))
            {
                var rundown = new ClrRundownTraceEventParser(relogger);
                rundown.LoaderModuleDCStop += delegate (ModuleLoadUnloadTraceData e)
                {
                    var descriptor = new EventDescriptor(
                        id: (int)e.ID,
                        version: (byte)e.Version,
                        channel: (byte)e.Channel,
                        keywords: (long)e.Keywords,
                        opcode: (byte)e.Opcode,
                        task: (int)e.Task,
                        level: (byte)e.Level);

                    object[] payloads = new object[]
                    {
                        e.ModuleID,
                        e.AssemblyID,
                        e.ModuleFlags,
                        0, // reserved payload
                        Path.GetFileName(e.ModuleILPath), // Remove the path from ModuleILPath
                        e.ModuleNativePath,
                        e.ManagedPdbSignature,
                        e.ManagedPdbAge,
                        e.ManagedPdbBuildPath,
                        e.NativePdbSignature,
                        e.NativePdbAge,
                        e.NativePdbBuildPath,
                    };

                    relogger.WriteEvent(e.ProviderGuid, ref descriptor, e, payloads);
                };
                
                relogger.Process();
            }

            // Verify the the file paths have been removed.
            string path = null;
            int eventCount = 0;
            using (var eventSource = new ETWTraceEventSource("filtered.etl"))
            {
                var rundown = new ClrRundownTraceEventParser(eventSource);
                rundown.LoaderModuleDCStop += delegate (ModuleLoadUnloadTraceData e)
                {
                    eventCount++;
                    path = e.ModuleILPath;
                };

                eventSource.Process();
            }
            if (eventCount != 1)
            {
                Assert.Fail("rundown.LoaderModuleDCStop was not fired {0} times but should have been fired exactly once.", eventCount);
            }
            Assert.AreEqual("Microsoft.VisualStudio.Services.Client.dll", path);
        }
    }
}
