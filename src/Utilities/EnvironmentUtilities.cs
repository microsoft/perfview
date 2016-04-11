
using System;
namespace Microsoft.Diagnostics.Utilities
{
#if UTILITIES_PUBLIC 
    public 
#endif
    static class EnvironmentUtilities
    {
        public static bool Is64BitProcess
        {
            get
            {
                return (IntPtr.Size == 8);
            }
        }

        public static bool Is64BitOperatingSystem
        {
            get
            {
#if STANDALONE_EXE
                return Environment.Is64BitOperatingSystem;
#else
                // 64-bit programs run only on 64-bit
                if (EnvironmentUtilities.Is64BitProcess)
                    return true;

                bool isWow64; // WinXP SP2+ and Win2k3 SP1+
                return Win32Native.DoesWin32MethodExist(Win32Native.KERNEL32, "IsWow64Process")
                    && Win32Native.IsWow64Process(Win32Native.GetCurrentProcess(), out isWow64)
                    && isWow64;
#endif
            }
        }
    }
}
