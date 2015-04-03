using System;
using System.Runtime.InteropServices;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("67721fe9-56d2-4a44-a325-2b65513ce6eb")]
    public interface IDebugOutputCallbacks2 : IDebugOutputCallbacks
    {
        /* IDebugOutputCallbacks */

        /// <summary>
        ///    This method is not used.
        /// </summary>
        [PreserveSig]
        new int Output(
            [In] DEBUG_OUTPUT Mask,
            [In, MarshalAs(UnmanagedType.LPStr)] string Text);

        /* IDebugOutputCallbacks2 */

        [PreserveSig]
        int GetInterestMask(
            [Out] out DEBUG_OUTCBI Mask);

        [PreserveSig]
        int Output2(
            [In] DEBUG_OUTCB Which,
            [In] DEBUG_OUTCBF Flags,
            [In] UInt64 Arg,
            [In, MarshalAs(UnmanagedType.LPWStr)] string Text);
    }
}
