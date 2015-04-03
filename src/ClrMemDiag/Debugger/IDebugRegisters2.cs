using System;
using System.Text;
using System.Runtime.InteropServices;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("1656afa9-19c6-4e3a-97e7-5dc9160cf9c4")]
    public interface IDebugRegisters2 : IDebugRegisters
    {
        [PreserveSig]
        new int GetNumberRegisters(
            [Out] out UInt32 Number);

        [PreserveSig]
        new int GetDescription(
            [In] UInt32 Register,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder NameBuffer,
            [In] Int32 NameBufferSize,
            [Out] out UInt32 NameSize,
            [Out] out DEBUG_REGISTER_DESCRIPTION Desc);

        [PreserveSig]
        new int GetIndexByName(
            [In, MarshalAs(UnmanagedType.LPStr)] string Name,
            [Out] out UInt32 Index);

        [PreserveSig]
        new int GetValue(
            [In] UInt32 Register,
            [Out] out DEBUG_VALUE Value);

        [PreserveSig]
        new int SetValue(
            [In] UInt32 Register,
            [In] DEBUG_VALUE Value);

        [PreserveSig]
        new int GetValues( //FIX ME!!! This needs to be tested
            [In] UInt32 Count,
            [In, MarshalAs(UnmanagedType.LPArray)] UInt32[] Indices,
            [In] UInt32 Start,
            [Out, MarshalAs(UnmanagedType.LPArray)] DEBUG_VALUE[] Values);

        [PreserveSig]
        new int SetValues(
            [In] UInt32 Count,
            [In, MarshalAs(UnmanagedType.LPArray)] UInt32[] Indices,
            [In] UInt32 Start,
            [In, MarshalAs(UnmanagedType.LPArray)] DEBUG_VALUE[] Values);

        [PreserveSig]
        new int OutputRegisters(
            [In] DEBUG_OUTCTL OutputControl,
            [In] DEBUG_REGISTERS Flags);

        [PreserveSig]
        new int GetInstructionOffset(
            [Out] out UInt64 Offset);

        [PreserveSig]
        new int GetStackOffset(
            [Out] out UInt64 Offset);

        [PreserveSig]
        new int GetFrameOffset(
            [Out] out UInt64 Offset);

        /* IDebugRegisters2 */

        [PreserveSig]
        int GetDescriptionWide(
            [In] UInt32 Register,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder NameBuffer,
            [In] Int32 NameBufferSize,
            [Out] out UInt32 NameSize,
            [Out] out DEBUG_REGISTER_DESCRIPTION Desc);

        [PreserveSig]
        int GetIndexByNameWide(
            [In, MarshalAs(UnmanagedType.LPWStr)] string Name,
            [Out] out UInt32 Index);

        [PreserveSig]
        int GetNumberPseudoRegisters(
            [Out] out UInt32 Number
            );

        [PreserveSig]
        int GetPseudoDescription(
            [In] UInt32 Register,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder NameBuffer,
            [In] Int32 NameBufferSize,
            [Out] out UInt32 NameSize,
            [Out] out UInt64 TypeModule,
            [Out] out UInt32 TypeId
            );

        [PreserveSig]
        int GetPseudoDescriptionWide(
            [In] UInt32 Register,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder NameBuffer,
            [In] Int32 NameBufferSize,
            [Out] out UInt32 NameSize,
            [Out] out UInt64 TypeModule,
            [Out] out UInt32 TypeId
            );

        [PreserveSig]
        int GetPseudoIndexByName(
            [In, MarshalAs(UnmanagedType.LPStr)] string Name,
            [Out] out UInt32 Index
            );

        [PreserveSig]
        int GetPseudoIndexByNameWide(
            [In, MarshalAs(UnmanagedType.LPWStr)] string Name,
            [Out] out UInt32 Index
            );

        [PreserveSig]
        int GetPseudoValues(
            [In] UInt32 Source,
            [In] UInt32 Count,
            [In, MarshalAs(UnmanagedType.LPArray)] UInt32[] Indices,
            [In] UInt32 Start,
            [Out, MarshalAs(UnmanagedType.LPArray)] DEBUG_VALUE[] Values
            );

        [PreserveSig]
        int SetPseudoValues(
            [In] UInt32 Source,
            [In] UInt32 Count,
            [In, MarshalAs(UnmanagedType.LPArray)] UInt32[] Indices,
            [In] UInt32 Start,
            [In, MarshalAs(UnmanagedType.LPArray)] DEBUG_VALUE[] Values
            );

        [PreserveSig]
        int GetValues2(
            [In] DEBUG_REGSRC Source,
            [In] UInt32 Count,
            [In, MarshalAs(UnmanagedType.LPArray)] UInt32[] Indices,
            [In] UInt32 Start,
            [Out, MarshalAs(UnmanagedType.LPArray)] DEBUG_VALUE[] Values
            );

        [PreserveSig]
        int SetValues2(
            [In] UInt32 Source,
            [In] UInt32 Count,
            [In, MarshalAs(UnmanagedType.LPArray)] UInt32[] Indices,
            [In] UInt32 Start,
            [In, MarshalAs(UnmanagedType.LPArray)] DEBUG_VALUE[] Values
            );

        [PreserveSig]
        int OutputRegisters2(
            [In] UInt32 OutputControl,
            [In] UInt32 Source,
            [In] UInt32 Flags
            );

        [PreserveSig]
        int GetInstructionOffset2(
            [In] UInt32 Source,
            [Out] out UInt64 Offset
            );

        [PreserveSig]
        int GetStackOffset2(
            [In] UInt32 Source,
            [Out] out UInt64 Offset
            );

        [PreserveSig]
        int GetFrameOffset2(
            [In] UInt32 Source,
            [Out] out UInt64 Offset
            );
    }
}
