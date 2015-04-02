using System;
using System.Runtime.InteropServices;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("4bf58045-d654-4c40-b0af-683090f356dc")]
    public interface IDebugOutputCallbacks
    {
        [PreserveSig]
        int Output(
            [In] DEBUG_OUTPUT Mask,
            [In, MarshalAs(UnmanagedType.LPStr)] string Text);
    }
}