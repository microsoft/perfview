using System;
using System.Text;
using System.Runtime.InteropServices;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("0ae9f5ff-1852-4679-b055-494bee6407ee")]
    public interface IDebugSystemObjects2 : IDebugSystemObjects
    {
        /* IDebugSystemObjects */

        [PreserveSig]
        new int GetEventThread(
            [Out] out UInt32 Id);

        [PreserveSig]
        new int GetEventProcess(
            [Out] out UInt32 Id);

        [PreserveSig]
        new int GetCurrentThreadId(
            [Out] out UInt32 Id);

        [PreserveSig]
        new int SetCurrentThreadId(
            [In] UInt32 Id);

        [PreserveSig]
        new int GetCurrentProcessId(
            [Out] out UInt32 Id);

        [PreserveSig]
        new int SetCurrentProcessId(
            [In] UInt32 Id);

        [PreserveSig]
        new int GetNumberThreads(
            [Out] out UInt32 Number);

        [PreserveSig]
        new int GetTotalNumberThreads(
            [Out] out UInt32 Total,
            [Out] out UInt32 LargestProcess);

        [PreserveSig]
        new int GetThreadIdsByIndex(
            [In] UInt32 Start,
            [In] UInt32 Count,
            [Out, MarshalAs(UnmanagedType.LPArray)] UInt32[] Ids,
            [Out, MarshalAs(UnmanagedType.LPArray)] UInt32[] SysIds);

        [PreserveSig]
        new int GetThreadIdByProcessor(
            [In] UInt32 Processor,
            [Out] out UInt32 Id);

        [PreserveSig]
        new int GetCurrentThreadDataOffset(
            [Out] out UInt64 Offset);

        [PreserveSig]
        new int GetThreadIdByDataOffset(
            [In] UInt64 Offset,
            [Out] out UInt32 Id);

        [PreserveSig]
        new int GetCurrentThreadTeb(
            [Out] out UInt64 Offset);

        [PreserveSig]
        new int GetThreadIdByTeb(
            [In] UInt64 Offset,
            [Out] out UInt32 Id);

        [PreserveSig]
        new int GetCurrentThreadSystemId(
            [Out] out UInt32 SysId);

        [PreserveSig]
        new int GetThreadIdBySystemId(
            [In] UInt32 SysId,
            [Out] out UInt32 Id);

        [PreserveSig]
        new int GetCurrentThreadHandle(
            [Out] out UInt64 Handle);

        [PreserveSig]
        new int GetThreadIdByHandle(
            [In] UInt64 Handle,
            [Out] out UInt32 Id);

        [PreserveSig]
        new int GetNumberProcesses(
            [Out] out UInt32 Number);

        [PreserveSig]
        new int GetProcessIdsByIndex(
            [In] UInt32 Start,
            [In] UInt32 Count,
            [Out, MarshalAs(UnmanagedType.LPArray)] UInt32[] Ids,
            [Out, MarshalAs(UnmanagedType.LPArray)] UInt32[] SysIds);

        [PreserveSig]
        new int GetCurrentProcessDataOffset(
            [Out] out UInt64 Offset);

        [PreserveSig]
        new int GetProcessIdByDataOffset(
            [In] UInt64 Offset,
            [Out] out UInt32 Id);

        [PreserveSig]
        new int GetCurrentProcessPeb(
            [Out] out UInt64 Offset);

        [PreserveSig]
        new int GetProcessIdByPeb(
            [In] UInt64 Offset,
            [Out] out UInt32 Id);

        [PreserveSig]
        new int GetCurrentProcessSystemId(
            [Out] out UInt32 SysId);

        [PreserveSig]
        new int GetProcessIdBySystemId(
            [In] UInt32 SysId,
            [Out] out UInt32 Id);

        [PreserveSig]
        new int GetCurrentProcessHandle(
            [Out] out UInt64 Handle);

        [PreserveSig]
        new int GetProcessIdByHandle(
            [In] UInt64 Handle,
            [Out] out UInt32 Id);

        [PreserveSig]
        new int GetCurrentProcessExecutableName(
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 ExeSize);

        /* IDebugSystemObjects2 */

        [PreserveSig]
        int GetCurrentProcessUpTime(
            [Out] out uint UpTime);

        [PreserveSig]
        int GetImplicitThreadDataOffset(
            [Out] out UInt64 Offset);

        [PreserveSig]
        int SetImplicitThreadDataOffset(
            [In] UInt64 Offset);

        [PreserveSig]
        int GetImplicitProcessDataOffset(
            [Out] out UInt64 Offset);

        [PreserveSig]
        int SetImplicitProcessDataOffset(
            [In] UInt64 Offset);
    }
}