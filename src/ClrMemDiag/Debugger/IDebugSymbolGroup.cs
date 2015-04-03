using System;
using System.Text;
using System.Runtime.InteropServices;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("f2528316-0f1a-4431-aeed-11d096e1e2ab")]
    public interface IDebugSymbolGroup
    {
        /* IDebugSymbolGroup */

        [PreserveSig]
        int GetNumberSymbols(
            [Out] out UInt32 Number);

        [PreserveSig]
        int AddSymbol(
            [In, MarshalAs(UnmanagedType.LPStr)] string Name,
            [In, Out] ref UInt32 Index);

        [PreserveSig]
        int RemoveSymbolByName(
            [In, MarshalAs(UnmanagedType.LPStr)] string Name);

        [PreserveSig]
        int RemoveSymbolsByIndex(
            [In] UInt32 Index);

        [PreserveSig]
        int GetSymbolName(
            [In] UInt32 Index,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 NameSize);

        [PreserveSig]
        int GetSymbolParameters(
            [In] UInt32 Start,
            [In] UInt32 Count,
            [Out, MarshalAs(UnmanagedType.LPArray)] DEBUG_SYMBOL_PARAMETERS[] Params);

        [PreserveSig]
        int ExpandSymbol(
            [In] UInt32 Index,
            [In, MarshalAs(UnmanagedType.Bool)] bool Expand);

        [PreserveSig]
        int OutputSymbols(
            [In] DEBUG_OUTCTL OutputControl,
            [In] DEBUG_OUTPUT_SYMBOLS Flags,
            [In] UInt32 Start,
            [In] UInt32 Count);

        [PreserveSig]
        int WriteSymbol(
            [In] UInt32 Index,
            [In, MarshalAs(UnmanagedType.LPStr)] string Value);

        [PreserveSig]
        int OutputAsType(
            [In] UInt32 Index,
            [In, MarshalAs(UnmanagedType.LPStr)] string Type);
    }
}