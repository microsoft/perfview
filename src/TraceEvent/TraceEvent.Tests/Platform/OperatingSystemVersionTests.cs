using Microsoft.Diagnostics.Utilities;
using System;
using System.ComponentModel;
using Xunit;

namespace TraceEventTests
{
    public class OperatingSystemVersionTests
    {
        [Theory]
        [InlineData(5, false)]
        [InlineData(6, false)]
        [InlineData(10, true)]
        [InlineData(11, true)]
        public void WindowsMinimumUsesMajorVersion(uint major, bool supported)
        {
            Assert.Equal(supported, OperatingSystemVersion.IsSupportedPlatform(PlatformID.Win32NT, () => major));
        }

        [Theory]
        [InlineData(PlatformID.Unix)]
        [InlineData(PlatformID.MacOSX)]
        public void OtherPlatformsNeverQueryWindows(PlatformID platform)
        {
            // The callback throws InvalidOperationException if the Windows version query runs for a non-Windows platform.
            Assert.True(OperatingSystemVersion.IsSupportedPlatform(platform,
                () => throw new InvalidOperationException("Windows API must not be called.")));
        }

        [Fact]
        public void NativeVersionQueryFailureIsNotIgnored()
        {
            Assert.Throws<Win32Exception>(() => OperatingSystemVersion.CheckVersionQueryStatus(unchecked((int)0xC0000001)));
            OperatingSystemVersion.CheckVersionQueryStatus(0);
        }

        [Fact]
        public void CurrentHostPassedModuleInitialization()
        {
            Assert.True(OperatingSystemVersion.IsSupported);
        }

    }
}
