using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Utilities
{
    internal static class OperatingSystemVersion
    {
        #region TraceEvent Required Windows Version
        public const string Requirement = "Windows 10 or Windows Server 2016 or later is required.";
        public const int Win10 = 100;
        public const int Win8 = 62;
        public const int Win7 = 61;
        public const int Vista = 60;

        public static bool IsSupported
        {
            get { return IsSupportedPlatform(Environment.OSVersion.Platform, () => GetWindowsVersion().dwMajorVersion); }
        }

        internal static bool IsSupportedPlatform(PlatformID platform, Func<uint> getWindowsMajorVersion)
        {
            return platform != PlatformID.Win32NT || getWindowsMajorVersion() >= 10;
        }

        public static void EnsureSupported()
        {
            if (!IsSupported)
            {
                throw new PlatformNotSupportedException(Requirement);
            }
        }
        #endregion

        /// <summary>
        /// requiredOSVersion is a number that is the major version * 10 + minor.  Thus
        ///     Win 10 == 100
        ///     Win 8 == 62
        ///     Win 7 == 61
        ///     Vista == 60
        /// This returns true if true OS version is >= 'requiredOSVersion
        /// </summary>

        public static bool AtLeast(int requiredOSVersion)
        {
            var osvi = GetWindowsVersion();
            uint osVersion = osvi.dwMajorVersion * 10 + osvi.dwMinorVersion;
            return osVersion >= requiredOSVersion;
        }

        #region private
        private static RTL_OSVERSIONINFO GetWindowsVersion()
        {
            var version = new RTL_OSVERSIONINFO();
            version.dwOSVersionInfoSize = (uint)Marshal.SizeOf(version);
            CheckVersionQueryStatus(RtlGetVersion(ref version));
            return version;
        }

        internal static void CheckVersionQueryStatus(int status)
        {
            if (status < 0)
            {
                throw new Win32Exception(status, "Could not determine the Windows version (NTSTATUS 0x" + status.ToString("X8") + ").");
            }
        }

        [DllImport("ntdll.dll")]
        private static extern int RtlGetVersion(ref RTL_OSVERSIONINFO lpVersionInformation);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct RTL_OSVERSIONINFO
        {
            internal uint dwOSVersionInfoSize;
            internal uint dwMajorVersion;
            internal uint dwMinorVersion;
            internal uint dwBuildNumber;
            internal uint dwPlatformId;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            internal string szCSDVersion;
        }
        #endregion
    }
}