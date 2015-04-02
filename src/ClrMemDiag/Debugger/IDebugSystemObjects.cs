using System;
using System.Text;
using System.Runtime.InteropServices;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("6b86fe2c-2c4f-4f0c-9da2-174311acc327")]
    public interface IDebugSystemObjects
    {
        [PreserveSig]
        int GetEventThread(
            [Out] out UInt32 Id);

        [PreserveSig]
        int GetEventProcess(
            [Out] out UInt32 Id);

        [PreserveSig]
        int GetCurrentThreadId(
            [Out] out UInt32 Id);

        [PreserveSig]
        int SetCurrentThreadId(
            [In] UInt32 Id);

        [PreserveSig]
        int GetCurrentProcessId(
            [Out] out UInt32 Id);

        [PreserveSig]
        int SetCurrentProcessId(
            [In] UInt32 Id);

        [PreserveSig]
        int GetNumberThreads(
            [Out] out UInt32 Number);

        [PreserveSig]
        int GetTotalNumberThreads(
            [Out] out UInt32 Total,
            [Out] out UInt32 LargestProcess);

        [PreserveSig]
        int GetThreadIdsByIndex(
            [In] UInt32 Start,
            [In] UInt32 Count,
            [Out, MarshalAs(UnmanagedType.LPArray)] UInt32[] Ids,
            [Out, MarshalAs(UnmanagedType.LPArray)] UInt32[] SysIds);

        [PreserveSig]
        int GetThreadIdByProcessor(
            [In] UInt32 Processor,
            [Out] out UInt32 Id);

        [PreserveSig]
        int GetCurrentThreadDataOffset(
            [Out] out UInt64 Offset);

        [PreserveSig]
        int GetThreadIdByDataOffset(
            [In] UInt64 Offset,
            [Out] out UInt32 Id);

        [PreserveSig]
        int GetCurrentThreadTeb(
            [Out] out UInt64 Offset);

        [PreserveSig]
        int GetThreadIdByTeb(
            [In] UInt64 Offset,
            [Out] out UInt32 Id);

        [PreserveSig]
        int GetCurrentThreadSystemId(
            [Out] out UInt32 SysId);

        [PreserveSig]
        int GetThreadIdBySystemId(
            [In] UInt32 SysId,
            [Out] out UInt32 Id);

        [PreserveSig]
        int GetCurrentThreadHandle(
            [Out] out UInt64 Handle);

        [PreserveSig]
        int GetThreadIdByHandle(
            [In] UInt64 Handle,
            [Out] out UInt32 Id);

        [PreserveSig]
        int GetNumberProcesses(
            [Out] out UInt32 Number);

        [PreserveSig]
        int GetProcessIdsByIndex(
            [In] UInt32 Start,
            [In] UInt32 Count,
            [Out, MarshalAs(UnmanagedType.LPArray)] UInt32[] Ids,
            [Out, MarshalAs(UnmanagedType.LPArray)] UInt32[] SysIds);

        [PreserveSig]
        int GetCurrentProcessDataOffset(
            [Out] out UInt64 Offset);

        [PreserveSig]
        int GetProcessIdByDataOffset(
            [In] UInt64 Offset,
            [Out] out UInt32 Id);

        [PreserveSig]
        int GetCurrentProcessPeb(
            [Out] out UInt64 Offset);

        [PreserveSig]
        int GetProcessIdByPeb(
            [In] UInt64 Offset,
            [Out] out UInt32 Id);

        [PreserveSig]
        int GetCurrentProcessSystemId(
            [Out] out UInt32 SysId);

        [PreserveSig]
        int GetProcessIdBySystemId(
            [In] UInt32 SysId,
            [Out] out UInt32 Id);

        [PreserveSig]
        int GetCurrentProcessHandle(
            [Out] out UInt64 Handle);

        [PreserveSig]
        int GetProcessIdByHandle(
            [In] UInt64 Handle,
            [Out] out UInt32 Id);

        [PreserveSig]
        int GetCurrentProcessExecutableName(
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 ExeSize);
    }
}