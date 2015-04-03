using System;
using System.Runtime.InteropServices;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("0690e046-9c23-45ac-a04f-987ac29ad0d3")]
    public interface IDebugEventCallbacksWide
    {
        [PreserveSig]
        int GetInterestMask(
            [Out] out DEBUG_EVENT Mask);

        [PreserveSig]
        int Breakpoint(
            [In, MarshalAs(UnmanagedType.Interface)] IDebugBreakpoint2 Bp);

        [PreserveSig]
        int Exception(
            [In] ref EXCEPTION_RECORD64 Exception,
            [In] UInt32 FirstChance);

        [PreserveSig]
        int CreateThread(
            [In] UInt64 Handle,
            [In] UInt64 DataOffset,
            [In] UInt64 StartOffset);

        [PreserveSig]
        int ExitThread(
            [In] UInt32 ExitCode);

        [PreserveSig]
        int CreateProcess(
            [In] UInt64 ImageFileHandle,
            [In] UInt64 Handle,
            [In] UInt64 BaseOffset,
            [In] UInt32 ModuleSize,
            [In, MarshalAs(UnmanagedType.LPWStr)] string ModuleName,
            [In, MarshalAs(UnmanagedType.LPWStr)] string ImageName,
            [In] UInt32 CheckSum,
            [In] UInt32 TimeDateStamp,
            [In] UInt64 InitialThreadHandle,
            [In] UInt64 ThreadDataOffset,
            [In] UInt64 StartOffset);

        [PreserveSig]
        int ExitProcess(
            [In] UInt32 ExitCode);

        [PreserveSig]
        int LoadModule(
            [In] UInt64 ImageFileHandle,
            [In] UInt64 BaseOffset,
            [In] UInt32 ModuleSize,
            [In, MarshalAs(UnmanagedType.LPWStr)] string ModuleName,
            [In, MarshalAs(UnmanagedType.LPWStr)] string ImageName,
            [In] UInt32 CheckSum,
            [In] UInt32 TimeDateStamp);

        [PreserveSig]
        int UnloadModule(
            [In, MarshalAs(UnmanagedType.LPWStr)] string ImageBaseName,
            [In] UInt64 BaseOffset);

        [PreserveSig]
        int SystemError(
            [In] UInt32 Error,
            [In] UInt32 Level);

        [PreserveSig]
        int SessionStatus(
            [In] DEBUG_SESSION Status);

        [PreserveSig]
        int ChangeDebuggeeState(
            [In] DEBUG_CDS Flags,
            [In] UInt64 Argument);

        [PreserveSig]
        int ChangeEngineState(
            [In] DEBUG_CES Flags,
            [In] UInt64 Argument);

        [PreserveSig]
        int ChangeSymbolState(
            [In] DEBUG_CSS Flags,
            [In] UInt64 Argument);
    }
}