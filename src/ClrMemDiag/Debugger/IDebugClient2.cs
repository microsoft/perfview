using System;
using System.Text;
using System.Runtime.InteropServices;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("edbed635-372e-4dab-bbfe-ed0d2f63be81")]
    public interface IDebugClient2 : IDebugClient
    {
        /* IDebugClient */

        [PreserveSig]
        new int AttachKernel(
            [In] DEBUG_ATTACH Flags,
            [In, MarshalAs(UnmanagedType.LPStr)] string ConnectOptions);

        [PreserveSig]
        new int GetKernelConnectionOptions(
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 OptionsSize);

        [PreserveSig]
        new int SetKernelConnectionOptions(
            [In, MarshalAs(UnmanagedType.LPStr)] string Options);

        [PreserveSig]
        new int StartProcessServer(
            [In] DEBUG_CLASS Flags,
            [In, MarshalAs(UnmanagedType.LPStr)] string Options,
            [In] IntPtr Reserved);

        [PreserveSig]
        new int ConnectProcessServer(
            [In, MarshalAs(UnmanagedType.LPStr)] string RemoteOptions,
            [Out] out UInt64 Server);

        [PreserveSig]
        new int DisconnectProcessServer(
            [In] UInt64 Server);

        [PreserveSig]
        new int GetRunningProcessSystemIds(
            [In] UInt64 Server,
            [Out, MarshalAs(UnmanagedType.LPArray)] UInt32[] Ids,
            [In] UInt32 Count,
            [Out] out UInt32 ActualCount);

        [PreserveSig]
        new int GetRunningProcessSystemIdByExecutableName(
            [In] UInt64 Server,
            [In, MarshalAs(UnmanagedType.LPStr)] string ExeName,
            [In] DEBUG_GET_PROC Flags,
            [Out] out UInt32 Id);

        [PreserveSig]
        new int GetRunningProcessDescription(
            [In] UInt64 Server,
            [In] UInt32 SystemId,
            [In] DEBUG_PROC_DESC Flags,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder ExeName,
            [In] Int32 ExeNameSize,
            [Out] out UInt32 ActualExeNameSize,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Description,
            [In] Int32 DescriptionSize,
            [Out] out UInt32 ActualDescriptionSize);

        [PreserveSig]
        new int AttachProcess(
            [In] UInt64 Server,
            [In] UInt32 ProcessID,
            [In] DEBUG_ATTACH AttachFlags);

        [PreserveSig]
        new int CreateProcess(
            [In] UInt64 Server,
            [In, MarshalAs(UnmanagedType.LPStr)] string CommandLine,
            [In] DEBUG_CREATE_PROCESS Flags);

        [PreserveSig]
        new int CreateProcessAndAttach(
            [In] UInt64 Server,
            [In, MarshalAs(UnmanagedType.LPStr)] string CommandLine,
            [In] DEBUG_CREATE_PROCESS Flags,
            [In] UInt32 ProcessId,
            [In] DEBUG_ATTACH AttachFlags);

        [PreserveSig]
        new int GetProcessOptions(
            [Out] out DEBUG_PROCESS Options);

        [PreserveSig]
        new int AddProcessOptions(
            [In] DEBUG_PROCESS Options);

        [PreserveSig]
        new int RemoveProcessOptions(
            [In] DEBUG_PROCESS Options);

        [PreserveSig]
        new int SetProcessOptions(
            [In] DEBUG_PROCESS Options);

        [PreserveSig]
        new int OpenDumpFile(
            [In, MarshalAs(UnmanagedType.LPStr)] string DumpFile);

        [PreserveSig]
        new int WriteDumpFile(
            [In, MarshalAs(UnmanagedType.LPStr)] string DumpFile,
            [In] DEBUG_DUMP Qualifier);

        [PreserveSig]
        new int ConnectSession(
            [In] DEBUG_CONNECT_SESSION Flags,
            [In] UInt32 HistoryLimit);

        [PreserveSig]
        new int StartServer(
            [In, MarshalAs(UnmanagedType.LPStr)] string Options);

        [PreserveSig]
        new int OutputServer(
            [In] DEBUG_OUTCTL OutputControl,
            [In, MarshalAs(UnmanagedType.LPStr)] string Machine,
            [In] DEBUG_SERVERS Flags);

        [PreserveSig]
        new int TerminateProcesses();

        [PreserveSig]
        new int DetachProcesses();

        [PreserveSig]
        new int EndSession(
            [In] DEBUG_END Flags);

        [PreserveSig]
        new int GetExitCode(
            [Out] out UInt32 Code);

        [PreserveSig]
        new int DispatchCallbacks(
            [In] UInt32 Timeout);

        [PreserveSig]
        new int ExitDispatch(
            [In, MarshalAs(UnmanagedType.Interface)] IDebugClient Client);

        [PreserveSig]
        new int CreateClient(
            [Out, MarshalAs(UnmanagedType.Interface)] out IDebugClient Client);

        [PreserveSig]
        new int GetInputCallbacks(
            [Out, MarshalAs(UnmanagedType.Interface)] out IDebugInputCallbacks Callbacks);

        [PreserveSig]
        new int SetInputCallbacks(
            [In, MarshalAs(UnmanagedType.Interface)] IDebugInputCallbacks Callbacks);

        /* GetOutputCallbacks could a conversion thunk from the debugger engine so we can't specify a specific interface */

        [PreserveSig]
        new int GetOutputCallbacks(
            [Out] out IntPtr Callbacks);

        /* We may have to pass a debugger engine conversion thunk back in so we can't specify a specific interface */

        [PreserveSig]
        new int SetOutputCallbacks(
            [In] IntPtr Callbacks);

        [PreserveSig]
        new int GetOutputMask(
            [Out] out DEBUG_OUTPUT Mask);

        [PreserveSig]
        new int SetOutputMask(
            [In] DEBUG_OUTPUT Mask);

        [PreserveSig]
        new int GetOtherOutputMask(
            [In, MarshalAs(UnmanagedType.Interface)] IDebugClient Client,
            [Out] out DEBUG_OUTPUT Mask);

        [PreserveSig]
        new int SetOtherOutputMask(
            [In, MarshalAs(UnmanagedType.Interface)] IDebugClient Client,
            [In] DEBUG_OUTPUT Mask);

        [PreserveSig]
        new int GetOutputWidth(
            [Out] out UInt32 Columns);

        [PreserveSig]
        new int SetOutputWidth(
            [In] UInt32 Columns);

        [PreserveSig]
        new int GetOutputLinePrefix(
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 PrefixSize);

        [PreserveSig]
        new int SetOutputLinePrefix(
            [In, MarshalAs(UnmanagedType.LPStr)] string Prefix);

        [PreserveSig]
        new int GetIdentity(
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 IdentitySize);

        [PreserveSig]
        new int OutputIdentity(
            [In] DEBUG_OUTCTL OutputControl,
            [In] UInt32 Flags,
            [In, MarshalAs(UnmanagedType.LPStr)] string Format);

        /* GetEventCallbacks could a conversion thunk from the debugger engine so we can't specify a specific interface */

        [PreserveSig]
        new int GetEventCallbacks(
            [Out] out IntPtr Callbacks);

        /* We may have to pass a debugger engine conversion thunk back in so we can't specify a specific interface */

        [PreserveSig]
        new int SetEventCallbacks(
            [In] IntPtr Callbacks);

        [PreserveSig]
        new int FlushCallbacks();

        /* IDebugClient2 */

        [PreserveSig]
        int WriteDumpFile2(
            [In, MarshalAs(UnmanagedType.LPStr)] string DumpFile,
            [In] DEBUG_DUMP Qualifier,
            [In] DEBUG_FORMAT FormatFlags,
            [In, MarshalAs(UnmanagedType.LPStr)] string Comment);

        [PreserveSig]
        int AddDumpInformationFile(
            [In, MarshalAs(UnmanagedType.LPStr)] string InfoFile,
            [In] DEBUG_DUMP_FILE Type);

        [PreserveSig]
        int EndProcessServer(
            [In] UInt64 Server);

        [PreserveSig]
        int WaitForProcessServerEnd(
            [In] UInt32 Timeout);

        [PreserveSig]
        int IsKernelDebuggerEnabled();

        [PreserveSig]
        int TerminateCurrentProcess();

        [PreserveSig]
        int DetachCurrentProcess();

        [PreserveSig]
        int AbandonCurrentProcess();
    }
}