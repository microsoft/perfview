using System;
using System.Diagnostics;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Microsoft.Diagnostics.Utilities
{
    internal static class Win32Native
    {
        // Copied from /src/ndp/clr/src/BCL/microsoft/win32/win32native.cs

        internal const string KERNEL32 = "kernel32.dll";

        [DllImport(KERNEL32)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWow64Process([In] IntPtr hProcess, [Out] out bool isWow64);

        internal static bool DoesWin32MethodExist(String moduleName, String methodName)
        {
            // GetModuleHandle does not increment the module's ref count, so we don't need to call FreeLibrary.
            IntPtr hModule = Win32Native.GetModuleHandle(moduleName);
            if (hModule == IntPtr.Zero)
            {
                Debug.Assert(hModule != IntPtr.Zero, "GetModuleHandle failed.  Dll isn't loaded?");
                return false;
            }
            IntPtr functionPointer = Win32Native.GetProcAddress(hModule, methodName);
            return (functionPointer != IntPtr.Zero);
        }

        [DllImport(KERNEL32, CharSet = CharSet.Auto, SetLastError = true)]
        [ResourceExposure(ResourceScope.Process)]
        internal static extern IntPtr GetCurrentProcess();

        #region Private methods

        // Note - do NOT use this to call methods.  Use P/Invoke, which will
        // do much better things w.r.t. marshaling, pinning memory, security 
        // stuff, better interactions with thread aborts, etc.  This is used
        // solely by DoesWin32MethodExist for avoiding try/catch EntryPointNotFoundException
        // in scenarios where an OS Version check is insufficient
        [DllImport(KERNEL32, CharSet = CharSet.Ansi, BestFitMapping = false, SetLastError = true, ExactSpelling = true)]
        [ResourceExposure(ResourceScope.None)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, String methodName);

        [DllImport(KERNEL32, CharSet = CharSet.Auto, BestFitMapping = false, SetLastError = true)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.MayFail)]
        [ResourceExposure(ResourceScope.Process)]  // Is your module side-by-side?
        private static extern IntPtr GetModuleHandle(String moduleName);

        #endregion
    }
}
