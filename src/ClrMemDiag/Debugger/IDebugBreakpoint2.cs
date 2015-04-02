using System;
using System.Text;
using System.Runtime.InteropServices;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("1b278d20-79f2-426e-a3f9-c1ddf375d48e")]
    public interface IDebugBreakpoint2 : IDebugBreakpoint
    {
        /* IDebugBreakpoint */

        [PreserveSig]
        new int GetId(
            [Out] out UInt32 Id);

        [PreserveSig]
        new int GetType(
            [Out] out DEBUG_BREAKPOINT_TYPE BreakType,
            [Out] out UInt32 ProcType);

        //FIX ME!!! Should try and get an enum for this
        [PreserveSig]
        new int GetAdder(
            [Out, MarshalAs(UnmanagedType.Interface)] out IDebugClient Adder);

        [PreserveSig]
        new int GetFlags(
            [Out] out DEBUG_BREAKPOINT_FLAG Flags);

        [PreserveSig]
        new int AddFlags(
            [In] DEBUG_BREAKPOINT_FLAG Flags);

        [PreserveSig]
        new int RemoveFlags(
            [In] DEBUG_BREAKPOINT_FLAG Flags);

        [PreserveSig]
        new int SetFlags(
            [In] DEBUG_BREAKPOINT_FLAG Flags);

        [PreserveSig]
        new int GetOffset(
            [Out] out UInt64 Offset);

        [PreserveSig]
        new int SetOffset(
            [In] UInt64 Offset);

        [PreserveSig]
        new int GetDataParameters(
            [Out] out UInt32 Size,
            [Out] out DEBUG_BREAKPOINT_ACCESS_TYPE AccessType);

        [PreserveSig]
        new int SetDataParameters(
            [In] UInt32 Size,
            [In] DEBUG_BREAKPOINT_ACCESS_TYPE AccessType);

        [PreserveSig]
        new int GetPassCount(
            [Out] out UInt32 Count);

        [PreserveSig]
        new int SetPassCount(
            [In] UInt32 Count);

        [PreserveSig]
        new int GetCurrentPassCount(
            [Out] out UInt32 Count);

        [PreserveSig]
        new int GetMatchThreadId(
            [Out] out UInt32 Id);

        [PreserveSig]
        new int SetMatchThreadId(
            [In] UInt32 Thread);

        [PreserveSig]
        new int GetCommand(
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 CommandSize);

        [PreserveSig]
        new int SetCommand(
            [In, MarshalAs(UnmanagedType.LPStr)] string Command);

        [PreserveSig]
        new int GetOffsetExpression(
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 ExpressionSize);

        [PreserveSig]
        new int SetOffsetExpression(
            [In, MarshalAs(UnmanagedType.LPStr)] string Expression);

        [PreserveSig]
        new int GetParameters(
            [Out] out DEBUG_BREAKPOINT_PARAMETERS Params);

        /* IDebugBreakpoint2 */

        [PreserveSig]
        int GetCommandWide(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 CommandSize);

        [PreserveSig]
        int SetCommandWide(
            [In, MarshalAs(UnmanagedType.LPWStr)] string Command);

        [PreserveSig]
        int GetOffsetExpressionWide(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 ExpressionSize);

        [PreserveSig]
        int SetOffsetExpressionWide(
            [In, MarshalAs(UnmanagedType.LPWStr)] string Command);
    }
}