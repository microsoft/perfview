using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Utilities
{
    // This class is a helper that hides the OS Version tests that you might have done with Evironment.OSVersion
    // but can't because this API was removed in .NET COre. 
    internal static class OperatingSystemVersion
    {
        public const int Win10 = 100;
        public const int Win8 = 62;
        public const int Win7 = 61;
        public const int Vista = 60;
        /// <summary>
        /// requiredOSVersion is a number that is the major version * 10 + minor.  Thus
        ///     Win 10 == 100
        ///     Win 8 == 62
        ///     Win 7 == 61
        ///     Vista == 60
        /// This returns true if true OS version is >= 'requiredOSVersion
        /// </summary>

        // Code borrowed from CoreFX System.PlatformDetection.Windows to allow targeting nestandard1.6
        [StructLayout(LayoutKind.Sequential)]
        private struct RTL_OSVERSIONINFOEX
        {
            internal uint dwOSVersionInfoSize;
            internal uint dwMajorVersion;
            internal uint dwMinorVersion;
            internal uint dwBuildNumber;
            internal uint dwPlatformId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            internal string szCSDVersion;
        }

        // Code borrowed from CoreFX System.PlatformDetection.Windows to allow targeting nestandard1.6
        [DllImport("ntdll.dll")]
        private static extern int RtlGetVersion(out RTL_OSVERSIONINFOEX lpVersionInformation);

        // Code borrowed from CoreFX System.PlatformDetection.Windows to allow targeting nestandard1.6
        public static bool AtLeast(int requiredOSVersion)
        {
            RTL_OSVERSIONINFOEX osvi = new RTL_OSVERSIONINFOEX();
            osvi.dwOSVersionInfoSize = (uint)Marshal.SizeOf(osvi);
            RtlGetVersion(out osvi);
            uint osVersion = osvi.dwMajorVersion * 10 + osvi.dwMinorVersion;
            return osVersion >= requiredOSVersion;
        }
    }
}