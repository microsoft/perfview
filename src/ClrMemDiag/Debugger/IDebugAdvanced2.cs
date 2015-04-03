using System;
using System.Text;
using System.Runtime.InteropServices;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("716d14c9-119b-4ba5-af1f-0890e672416a")]
    public interface IDebugAdvanced2 : IDebugAdvanced
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
        int Request(
            [In] DEBUG_REQUEST Request,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] inBuffer,
            [In] Int32 InBufferSize,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] byte[] outBuffer,
            [In] Int32 OutBufferSize,
            [Out] out Int32 OutSize);

        [PreserveSig]
        int GetSourceFileInformation(
            [In] DEBUG_SRCFILE Which,
            [In, MarshalAs(UnmanagedType.LPStr)] string SourceFile,
            [In] UInt64 Arg64,
            [In] UInt32 Arg32,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 5)] byte[] buffer,
            [In] Int32 BufferSize,
            [Out] out Int32 InfoSize);

        [PreserveSig]
        int FindSourceFileAndToken(
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
        int GetSymbolInformation(
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
        int GetSystemObjectInformation(
            [In] DEBUG_SYSOBJINFO Which,
            [In] UInt64 Arg64,
            [In] UInt32 Arg32,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] byte[] buffer,
            [In] Int32 BufferSize,
            [Out] out Int32 InfoSize);
    }
}