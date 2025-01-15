
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
                return Environment.Is64BitOperatingSystem;
            }
        }
    }
}
