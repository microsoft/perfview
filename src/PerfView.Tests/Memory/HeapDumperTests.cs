using System;
using System.Diagnostics;
using System.IO;
using PerfView;
using PerfViewTests.Utilities;
using Utilities;
using Xunit;
using Xunit.Abstractions;

namespace PerfViewTests.Memory
{
    public class HeapDumperTests : PerfViewTestBase
    {
        public HeapDumperTests(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
        }

        private string PowerShellPath
        {
            get
            {
                return $@"{Environment.GetEnvironmentVariable("SYSTEMROOT")}\System32\WindowsPowerShell\v1.0\powershell.exe";
            }
        }

        [Fact]
        public void TestDumpGCHeap()
        {
            SupportFiles.UnpackResourcesIfNeeded();
            var process = Process.Start(PowerShellPath, "-Command sleep 10");
            var gcheapFileName = Path.ChangeExtension(Path.GetRandomFileName(), ".gcheap");
            try
            {
                var log = new StringWriter();
                var qualifiers = "";
                HeapDumper.DumpGCHeap(process.Id, gcheapFileName, log, qualifiers);
                TestOutputHelper.WriteLine(log.ToString());
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }

                File.Delete(gcheapFileName);
            }
        }
    }
}
