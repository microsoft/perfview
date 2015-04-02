using System;
using System.Runtime.InteropServices;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("4c7fd663-c394-4e26-8ef1-34ad5ed3764c")]
    public interface IDebugOutputCallbacksWide
    {
        [PreserveSig]
        int Output(
            [In] DEBUG_OUTPUT Mask,
            [In, MarshalAs(UnmanagedType.LPWStr)] string Text);
    }
}