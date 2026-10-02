using PerfView;
using System;
using System.ComponentModel;
using Xunit;

namespace PerfViewTests
{
    public class StartupTests
    {
        [Fact]
        public void ApplicationAndTestHostAreX64()
        {
            Assert.Equal(8, IntPtr.Size);
            Assert.Equal(System.Reflection.ProcessorArchitecture.Amd64, typeof(Startup).Assembly.GetName().ProcessorArchitecture);
        }

        [Fact]
        public void UnsupportedWindowsReportsOnceWithoutStarting()
        {
            int reports = 0;
            int result = Startup.Run(() => false, message =>
            {
                reports++;
                Assert.Contains("Windows 10", message);
            }, () => throw new InvalidOperationException("Application must not start."));

            Assert.Equal(1, result);
            Assert.Equal(1, reports);
        }

        [Fact]
        public void SupportedWindowsReturnsApplicationStatus()
        {
            Assert.Equal(42, Startup.Run(() => true,
                message => throw new InvalidOperationException(message), () => 42));
        }

        [Fact]
        public void VersionQueryFailureReportsWithoutStarting()
        {
            string reported = null;
            int result = Startup.Run(() => throw new Win32Exception("Version query failed."),
                message => reported = message, () => throw new InvalidOperationException("Application must not start."));
            Assert.Equal(1, result);
            Assert.Equal("Version query failed.", reported);
        }
    }
}
