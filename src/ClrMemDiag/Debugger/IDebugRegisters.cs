using System;
using System.Text;
using System.Runtime.InteropServices;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("ce289126-9e84-45a7-937e-67bb18691493")]
    public interface IDebugRegisters
    {
        [PreserveSig]
        int GetNumberRegisters(
            [Out] out UInt32 Number);

        [PreserveSig]
        int GetDescription(
            [In] UInt32 Register,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder NameBuffer,
            [In] Int32 NameBufferSize,
            [Out] out UInt32 NameSize,
            [Out] out DEBUG_REGISTER_DESCRIPTION Desc);

        [PreserveSig]
        int GetIndexByName(
            [In, MarshalAs(UnmanagedType.LPStr)] string Name,
            [Out] out UInt32 Index);

        [PreserveSig]
        int GetValue(
            [In] UInt32 Register,
            [Out] out DEBUG_VALUE Value);

        [PreserveSig]
        int SetValue(
            [In] UInt32 Register,
            [In] DEBUG_VALUE Value);

        [PreserveSig]
        int GetValues( //FIX ME!!! This needs to be tested
            [In] UInt32 Count,
            [In, MarshalAs(UnmanagedType.LPArray)] UInt32[] Indices,
            [In] UInt32 Start,
            [Out, MarshalAs(UnmanagedType.LPArray)] DEBUG_VALUE[] Values);

        [PreserveSig]
        int SetValues(
            [In] UInt32 Count,
            [In, MarshalAs(UnmanagedType.LPArray)] UInt32[] Indices,
            [In] UInt32 Start,
            [In, MarshalAs(UnmanagedType.LPArray)] DEBUG_VALUE[] Values);

        [PreserveSig]
        int OutputRegisters(
            [In] DEBUG_OUTCTL OutputControl,
            [In] DEBUG_REGISTERS Flags);

        [PreserveSig]
        int GetInstructionOffset(
            [Out] out UInt64 Offset);

        [PreserveSig]
        int GetStackOffset(
            [Out] out UInt64 Offset);

        [PreserveSig]
        int GetFrameOffset(
            [Out] out UInt64 Offset);
    }
}