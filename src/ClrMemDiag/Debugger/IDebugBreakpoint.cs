using System;
using System.Text;
using System.Runtime.InteropServices;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("5bd9d474-5975-423a-b88b-65a8e7110e65")]
    public interface IDebugBreakpoint
    {
        /* IDebugBreakpoint */

        [PreserveSig]
        int GetId(
            [Out] out UInt32 Id);

        [PreserveSig]
        int GetType(
            [Out] out DEBUG_BREAKPOINT_TYPE BreakType,
            [Out] out UInt32 ProcType);

        //FIX ME!!! Should try and get an enum for this
        [PreserveSig]
        int GetAdder(
            [Out, MarshalAs(UnmanagedType.Interface)] out IDebugClient Adder);

        [PreserveSig]
        int GetFlags(
            [Out] out DEBUG_BREAKPOINT_FLAG Flags);

        [PreserveSig]
        int AddFlags(
            [In] DEBUG_BREAKPOINT_FLAG Flags);

        [PreserveSig]
        int RemoveFlags(
            [In] DEBUG_BREAKPOINT_FLAG Flags);

        [PreserveSig]
        int SetFlags(
            [In] DEBUG_BREAKPOINT_FLAG Flags);

        [PreserveSig]
        int GetOffset(
            [Out] out UInt64 Offset);

        [PreserveSig]
        int SetOffset(
            [In] UInt64 Offset);

        [PreserveSig]
        int GetDataParameters(
            [Out] out UInt32 Size,
            [Out] out DEBUG_BREAKPOINT_ACCESS_TYPE AccessType);

        [PreserveSig]
        int SetDataParameters(
            [In] UInt32 Size,
            [In] DEBUG_BREAKPOINT_ACCESS_TYPE AccessType);

        [PreserveSig]
        int GetPassCount(
            [Out] out UInt32 Count);

        [PreserveSig]
        int SetPassCount(
            [In] UInt32 Count);

        [PreserveSig]
        int GetCurrentPassCount(
            [Out] out UInt32 Count);

        [PreserveSig]
        int GetMatchThreadId(
            [Out] out UInt32 Id);

        [PreserveSig]
        int SetMatchThreadId(
            [In] UInt32 Thread);

        [PreserveSig]
        int GetCommand(
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 CommandSize);

        [PreserveSig]
        int SetCommand(
            [In, MarshalAs(UnmanagedType.LPStr)] string Command);

        [PreserveSig]
        int GetOffsetExpression(
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 ExpressionSize);

        [PreserveSig]
        int SetOffsetExpression(
            [In, MarshalAs(UnmanagedType.LPStr)] string Expression);

        [PreserveSig]
        int GetParameters(
            [Out] out DEBUG_BREAKPOINT_PARAMETERS Params);
    }
}