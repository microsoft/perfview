using System;
using System.Text;
using System.Runtime.InteropServices;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("6a7ccc5f-fb5e-4dcc-b41c-6c20307bccc7")]
    public interface IDebugSymbolGroup2 : IDebugSymbolGroup
    {
        /* IDebugSymbolGroup */

        [PreserveSig]
        new int GetNumberSymbols(
            [Out] out UInt32 Number);

        [PreserveSig]
        new int AddSymbol(
            [In, MarshalAs(UnmanagedType.LPStr)] string Name,
            [In, Out] ref UInt32 Index);

        [PreserveSig]
        new int RemoveSymbolByName(
            [In, MarshalAs(UnmanagedType.LPStr)] string Name);

        [PreserveSig]
        new int RemoveSymbolsByIndex(
            [In] UInt32 Index);

        [PreserveSig]
        new int GetSymbolName(
            [In] UInt32 Index,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 NameSize);

        [PreserveSig]
        new int GetSymbolParameters(
            [In] UInt32 Start,
            [In] UInt32 Count,
            [Out, MarshalAs(UnmanagedType.LPArray)] DEBUG_SYMBOL_PARAMETERS[] Params);

        [PreserveSig]
        new int ExpandSymbol(
            [In] UInt32 Index,
            [In, MarshalAs(UnmanagedType.Bool)] bool Expand);

        [PreserveSig]
        new int OutputSymbols(
            [In] DEBUG_OUTCTL OutputControl,
            [In] DEBUG_OUTPUT_SYMBOLS Flags,
            [In] UInt32 Start,
            [In] UInt32 Count);

        [PreserveSig]
        new int WriteSymbol(
            [In] UInt32 Index,
            [In, MarshalAs(UnmanagedType.LPStr)] string Value);

        [PreserveSig]
        new int OutputAsType(
            [In] UInt32 Index,
            [In, MarshalAs(UnmanagedType.LPStr)] string Type);

        /* IDebugSymbolGroup2 */

        [PreserveSig]
        int AddSymbolWide(
            [In, MarshalAs(UnmanagedType.LPWStr)] string Name,
            [In, Out] ref UInt32 Index);

        [PreserveSig]
        int RemoveSymbolByNameWide(
            [In, MarshalAs(UnmanagedType.LPWStr)] string Name);

        [PreserveSig]
        int GetSymbolNameWide(
            [In] UInt32 Index,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 NameSize);

        [PreserveSig]
        int WriteSymbolWide(
            [In] UInt32 Index,
            [In, MarshalAs(UnmanagedType.LPWStr)] string Value);

        [PreserveSig]
        int OutputAsTypeWide(
            [In] UInt32 Index,
            [In, MarshalAs(UnmanagedType.LPWStr)] string Type);

        [PreserveSig]
        int GetSymbolTypeName(
            [In] UInt32 Index,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 NameSize);

        [PreserveSig]
        int GetSymbolTypeNameWide(
            [In] UInt32 Index,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 NameSize);

        [PreserveSig]
        int GetSymbolSize(
            [In] UInt32 Index,
            [Out] out UInt32 Size);

        [PreserveSig]
        int GetSymbolOffset(
            [In] UInt32 Index,
            [Out] out UInt64 Offset);

        [PreserveSig]
        int GetSymbolRegister(
            [In] UInt32 Index,
            [Out] out UInt32 Register);

        [PreserveSig]
        int GetSymbolValueText(
            [In] UInt32 Index,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 NameSize);

        [PreserveSig]
        int GetSymbolValueTextWide(
            [In] UInt32 Index,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 NameSize);

        [PreserveSig]
        int GetSymbolEntryInformation(
            [In] UInt32 Index,
            [Out] out DEBUG_SYMBOL_ENTRY Info);
    }
}