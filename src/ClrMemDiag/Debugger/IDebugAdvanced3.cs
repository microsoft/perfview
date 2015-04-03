using System;
using System.Text;
using System.Runtime.InteropServices;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("cba4abb4-84c4-444d-87ca-a04e13286739")]
    public interface IDebugAdvanced3 : IDebugAdvanced2
    {
        /* IDebugAdvanced */

        [PreserveSig]
        new int GetThreadContext(
            [In] IntPtr Context,
            [In] UInt32 ContextSize);

        [PreserveSig]
        new int SetThreadContext(
            [In] IntPtr Context,
            [In] UInt32 ContextSize);

        /* IDebugAdvanced2 */

        [PreserveSig]
        new int Request(
            [In] DEBUG_REQUEST Request,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] inBuffer,
            [In] Int32 InBufferSize,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] byte[] outBuffer,
            [In] Int32 OutBufferSize,
            [Out] out Int32 OutSize);

        [PreserveSig]
        new int GetSourceFileInformation(
            [In] DEBUG_SRCFILE Which,
            [In, MarshalAs(UnmanagedType.LPStr)] string SourceFile,
            [In] UInt64 Arg64,
            [In] UInt32 Arg32,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 5)] byte[] buffer,
            [In] Int32 BufferSize,
            [Out] out Int32 InfoSize);

        [PreserveSig]
        new int FindSourceFileAndToken(
            [In] UInt32 StartElement,
            [In] UInt64 ModAddr,
            [In, MarshalAs(UnmanagedType.LPStr)] string File,
            [In] DEBUG_FIND_SOURCE Flags,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 5)] byte[] buffer,
            [In] Int32 FileTokenSize,
            [Out] out Int32 FoundElement,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out Int32 FoundSize);

        [PreserveSig]
        new int GetSymbolInformation(
            [In] DEBUG_SYMINFO Which,
            [In] UInt64 Arg64,
            [In] UInt32 Arg32,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] byte[] buffer,
            [In] Int32 BufferSize,
            [Out] out Int32 InfoSize,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder StringBuffer,
            [In] Int32 StringBufferSize,
            [Out] out Int32 StringSize);

        [PreserveSig]
        new int GetSystemObjectInformation(
            [In] DEBUG_SYSOBJINFO Which,
            [In] UInt64 Arg64,
            [In] UInt32 Arg32,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] byte[] buffer,
            [In] Int32 BufferSize,
            [Out] out Int32 InfoSize);

        /* IDebugAdvanced3 */

        [PreserveSig]
        int GetSourceFileInformationWide(
            [In] DEBUG_SRCFILE Which,
            [In, MarshalAs(UnmanagedType.LPWStr)] string SourceFile,
            [In] UInt64 Arg64,
            [In] UInt32 Arg32,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 5)] byte[] buffer,
            [In] Int32 BufferSize,
            [Out] out Int32 InfoSize);

        [PreserveSig]
        int FindSourceFileAndTokenWide(
            [In] UInt32 StartElement,
            [In] UInt64 ModAddr,
            [In, MarshalAs(UnmanagedType.LPWStr)] string File,
            [In] DEBUG_FIND_SOURCE Flags,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 5)] byte[] buffer,
            [In] Int32 FileTokenSize,
            [Out] out Int32 FoundElement,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out Int32 FoundSize);

        [PreserveSig]
        int GetSymbolInformationWide(
            [In] DEBUG_SYMINFO Which,
            [In] UInt64 Arg64,
            [In] UInt32 Arg32,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] byte[] buffer,
            [In] Int32 BufferSize,
            [Out] out Int32 InfoSize,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder StringBuffer,
            [In] Int32 StringBufferSize,
            [Out] out Int32 StringSize);
    }
}