using PerfView;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Xunit;

namespace PerfViewTests
{
    public class HeapDumperTests
    {
        [Fact]
        public void DetectsX64Target()
        {
            using (var process = Process.GetCurrentProcess())
            {
                Assert.Equal(ProcessorArchitecture.Amd64, HeapDumper.GetArchForProcess(process.Id));
            }
        }

        [Fact]
        public void DetectsX86TargetFromX64Host()
        {
            var startInfo = new ProcessStartInfo(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), "cmd.exe"),
                "/d /c set /p PERFVIEW_TEST=")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true
            };
            using (var process = Process.Start(startInfo))
            {
                try
                {
                    Assert.Equal(ProcessorArchitecture.X86, HeapDumper.GetArchForProcess(process.Id));
                }
                finally
                {
                    process.StandardInput.Close();
                    if (!process.WaitForExit(5000))
                    {
                        process.Kill();
                        process.WaitForExit();
                    }
                }
            }
        }
    }
}
