using System;
using System.Text;
using System.Runtime.InteropServices;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("d98ada1f-29e9-4ef5-a6c0-e53349883212")]
    public interface IDebugDataSpaces4 : IDebugDataSpaces3
    {
        /* IDebugDataSpaces */

        [PreserveSig]
        new int ReadVirtual(
            [In] UInt64 Offset,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 BytesRead);

        [PreserveSig]
        new int WriteVirtual(
            [In] UInt64 Offset,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 BytesWritten);

        [PreserveSig]
        new int SearchVirtual(
            [In] UInt64 Offset,
            [In] UInt64 Length,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] pattern,
            [In] UInt32 PatternSize,
            [In] UInt32 PatternGranularity,
            [Out] out UInt64 MatchOffset);

        [PreserveSig]
        new int ReadVirtualUncached(
            [In] UInt64 Offset,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 BytesRead);

        [PreserveSig]
        new int WriteVirtualUncached(
            [In] UInt64 Offset,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 BytesWritten);

        [PreserveSig]
        new int ReadPointersVirtual(
            [In] UInt32 Count,
            [In] UInt64 Offset,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] UInt64[] Ptrs);

        [PreserveSig]
        new int WritePointersVirtual(
            [In] UInt32 Count,
            [In] UInt64 Offset,
            [In, MarshalAs(UnmanagedType.LPArray)] UInt64[] Ptrs);

        [PreserveSig]
        new int ReadPhysical(
            [In] UInt64 Offset,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 BytesRead);

        [PreserveSig]
        new int WritePhysical(
            [In] UInt64 Offset,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 BytesWritten);

        [PreserveSig]
        new int ReadControl(
            [In] UInt32 Processor,
            [In] UInt64 Offset,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 BytesRead);

        [PreserveSig]
        new int WriteControl(
            [In] UInt32 Processor,
            [In] UInt64 Offset,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 BytesWritten);

        [PreserveSig]
        new int ReadIo(
            [In] INTERFACE_TYPE InterfaceType,
            [In] UInt32 BusNumber,
            [In] UInt32 AddressSpace,
            [In] UInt64 Offset,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 5)] byte[] buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 BytesRead);

        [PreserveSig]
        new int WriteIo(
            [In] INTERFACE_TYPE InterfaceType,
            [In] UInt32 BusNumber,
            [In] UInt32 AddressSpace,
            [In] UInt64 Offset,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 5)] byte[] buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 BytesWritten);

        [PreserveSig]
        new int ReadMsr(
            [In] UInt32 Msr,
            [Out] out UInt64 MsrValue);

        [PreserveSig]
        new int WriteMsr(
            [In] UInt32 Msr,
            [In] UInt64 MsrValue);

        [PreserveSig]
        new int ReadBusData(
            [In] BUS_DATA_TYPE BusDataType,
            [In] UInt32 BusNumber,
            [In] UInt32 SlotNumber,
            [In] UInt32 Offset,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 5)] byte[] buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 BytesRead);

        [PreserveSig]
        new int WriteBusData(
            [In] BUS_DATA_TYPE BusDataType,
            [In] UInt32 BusNumber,
            [In] UInt32 SlotNumber,
            [In] UInt32 Offset,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 5)] byte[] buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 BytesWritten);

        [PreserveSig]
        new int CheckLowMemory();

        [PreserveSig]
        new int ReadDebuggerData(
            [In] UInt32 Index,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 DataSize);

        [PreserveSig]
        new int ReadProcessorSystemData(
            [In] UInt32 Processor,
            [In] DEBUG_DATA Index,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 DataSize);

        /* IDebugDataSpaces2 */

        [PreserveSig]
        new int VirtualToPhysical(
            [In] UInt64 Virtual,
            [Out] out UInt64 Physical);

        [PreserveSig]
        new int GetVirtualTranslationPhysicalOffsets(
            [In] UInt64 Virtual,
            [Out, MarshalAs(UnmanagedType.LPArray)] UInt64[] Offsets,
            [In] UInt32 OffsetsSize,
            [Out] out UInt32 Levels);

        [PreserveSig]
        new int ReadHandleData(
            [In] UInt64 Handle,
            [In] DEBUG_HANDLE_DATA_TYPE DataType,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 DataSize);

        [PreserveSig]
        new int FillVirtual(
            [In] UInt64 Start,
            [In] UInt32 Size,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] buffer,
            [In] UInt32 PatternSize,
            [Out] out UInt32 Filled);

        [PreserveSig]
        new int FillPhysical(
            [In] UInt64 Start,
            [In] UInt32 Size,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] buffer,
            [In] UInt32 PatternSize,
            [Out] out UInt32 Filled);

        [PreserveSig]
        new int QueryVirtual(
            [In] UInt64 Offset,
            [Out] out MEMORY_BASIC_INFORMATION64 Info);

        /* IDebugDataSpaces3 */

        [PreserveSig]
        new int ReadImageNtHeaders(
            [In] UInt64 ImageBase,
            [Out] out IMAGE_NT_HEADERS64 Headers);

        [PreserveSig]
        new int ReadTagged(
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid Tag,
            [In] UInt32 Offset,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 TotalSize);

        [PreserveSig]
        new int StartEnumTagged(
            [Out] out UInt64 Handle);

        [PreserveSig]
        new int GetNextTagged(
            [In] UInt64 Handle,
            [Out] out Guid Tag,
            [Out] out UInt32 Size);

        [PreserveSig]
        new int EndEnumTagged(
            [In] UInt64 Handle);

        /* IDebugDataSpaces4 */

        [PreserveSig]
        int GetOffsetInformation(
            [In] DEBUG_DATA_SPACE Space,
            [In] DEBUG_OFFSINFO Which,
            [In] UInt64 Offset,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] byte[] buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 InfoSize);

        [PreserveSig]
        int GetNextDifferentlyValidOffsetVirtual(
            [In] UInt64 Offset,
            [Out] out UInt64 NextOffset);

        [PreserveSig]
        int GetValidRegionVirtual(
            [In] UInt64 Base,
            [In] UInt32 Size,
            [Out] out UInt64 ValidBase,
            [Out] out UInt32 ValidSize);

        [PreserveSig]
        int SearchVirtual2(
            [In] UInt64 Offset,
            [In] UInt64 Length,
            [In] DEBUG_VSEARCH Flags,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] byte[] buffer,
            [In] UInt32 PatternSize,
            [In] UInt32 PatternGranularity,
            [Out] out UInt64 MatchOffset);

        [PreserveSig]
        int ReadMultiByteStringVirtual(
            [In] UInt64 Offset,
            [In] UInt32 MaxBytes,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 StringBytes);

        [PreserveSig]
        int ReadMultiByteStringVirtualWide(
            [In] UInt64 Offset,
            [In] UInt32 MaxBytes,
            [In] CODE_PAGE CodePage,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 StringBytes);

        [PreserveSig]
        int ReadUnicodeStringVirtual(
            [In] UInt64 Offset,
            [In] UInt32 MaxBytes,
            [In] CODE_PAGE CodePage,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 StringBytes);

        [PreserveSig]
        int ReadUnicodeStringVirtualWide(
            [In] UInt64 Offset,
            [In] UInt32 MaxBytes,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 StringBytes);

        [PreserveSig]
        int ReadPhysical2(
            [In] UInt64 Offset,
            [In] DEBUG_PHYSICAL Flags,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 BytesRead);

        [PreserveSig]
        int WritePhysical2(
            [In] UInt64 Offset,
            [In] DEBUG_PHYSICAL Flags,
            [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] byte[] buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 BytesWritten);
    }
}