//---------------------------------------------------------------------
//  This file is part of the CLR Managed Debugger (mdbg) Sample.
// 
//  Copyright (C) Microsoft Corporation.  All rights reserved.
//
// Imports ICorDebug interface from CorDebug.idl into managed code.
//---------------------------------------------------------------------

using Microsoft.Samples.Debugging.CorMetadata.NativeApi;
using Microsoft.Samples.Debugging.NativeApi;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using IStream = System.Runtime.InteropServices.ComTypes.IStream;

namespace Microsoft.Samples.Debugging.CorDebug.NativeApi
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct _COR_VERSION
    {
        public uint dwMajor;
        public uint dwMinor;
        public uint dwBuild;
        public uint dwSubBuild;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct _LARGE_INTEGER
    {
        public long QuadPart;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct _SECURITY_ATTRIBUTES
    {
        public uint nLength;
        public IntPtr lpSecurityDescriptor;
        public int bInheritHandle;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct _ULARGE_INTEGER
    {
        public ulong QuadPart;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct COR_IL_MAP
    {
        public uint oldOffset;
        public uint newOffset;
        public int fAccurate;
    }

    public enum CorDebugCreateProcessFlags
    {
        DEBUG_NO_SPECIAL_OPTIONS
    }

    public enum CorDebugExceptionCallbackType
    {
        // Fields
        DEBUG_EXCEPTION_CATCH_HANDLER_FOUND = 3,
        DEBUG_EXCEPTION_FIRST_CHANCE = 1,
        DEBUG_EXCEPTION_UNHANDLED = 4,
        DEBUG_EXCEPTION_USER_FIRST_CHANCE = 2
    }

    public enum CorDebugExceptionUnwindCallbackType
    {
        // Fields
        DEBUG_EXCEPTION_INTERCEPTED = 2,
        DEBUG_EXCEPTION_UNWIND_BEGIN = 1
    }


    public enum CorDebugRegister
    {
        // Fields
        REGISTER_AMD64_R10 = 11,
        REGISTER_AMD64_R11 = 12,
        REGISTER_AMD64_R12 = 13,
        REGISTER_AMD64_R13 = 14,
        REGISTER_AMD64_R14 = 15,
        REGISTER_AMD64_R15 = 0x10,
        REGISTER_AMD64_R8 = 9,
        REGISTER_AMD64_R9 = 10,
        REGISTER_AMD64_RAX = 3,
        REGISTER_AMD64_RBP = 2,
        REGISTER_AMD64_RBX = 6,
        REGISTER_AMD64_RCX = 4,
        REGISTER_AMD64_RDI = 8,
        REGISTER_AMD64_RDX = 5,
        REGISTER_AMD64_RIP = 0,
        REGISTER_AMD64_RSI = 7,
        REGISTER_AMD64_RSP = 1,
        REGISTER_AMD64_XMM0 = 0x11,
        REGISTER_AMD64_XMM1 = 0x12,
        REGISTER_AMD64_XMM10 = 0x1b,
        REGISTER_AMD64_XMM11 = 0x1c,
        REGISTER_AMD64_XMM12 = 0x1d,
        REGISTER_AMD64_XMM13 = 30,
        REGISTER_AMD64_XMM14 = 0x1f,
        REGISTER_AMD64_XMM15 = 0x20,
        REGISTER_AMD64_XMM2 = 0x13,
        REGISTER_AMD64_XMM3 = 20,
        REGISTER_AMD64_XMM4 = 0x15,
        REGISTER_AMD64_XMM5 = 0x16,
        REGISTER_AMD64_XMM6 = 0x17,
        REGISTER_AMD64_XMM7 = 0x18,
        REGISTER_AMD64_XMM8 = 0x19,
        REGISTER_AMD64_XMM9 = 0x1a,
        REGISTER_FRAME_POINTER = 2,
        REGISTER_IA64_BSP = 2,
        REGISTER_IA64_F0 = 0x83,
        REGISTER_IA64_R0 = 3,
        REGISTER_INSTRUCTION_POINTER = 0,
        REGISTER_STACK_POINTER = 1,
        REGISTER_X86_EAX = 3,
        REGISTER_X86_EBP = 2,
        REGISTER_X86_EBX = 6,
        REGISTER_X86_ECX = 4,
        REGISTER_X86_EDI = 8,
        REGISTER_X86_EDX = 5,
        REGISTER_X86_EIP = 0,
        REGISTER_X86_ESI = 7,
        REGISTER_X86_ESP = 1,
        REGISTER_X86_FPSTACK_0 = 9,
        REGISTER_X86_FPSTACK_1 = 10,
        REGISTER_X86_FPSTACK_2 = 11,
        REGISTER_X86_FPSTACK_3 = 12,
        REGISTER_X86_FPSTACK_4 = 13,
        REGISTER_X86_FPSTACK_5 = 14,
        REGISTER_X86_FPSTACK_6 = 15,
        REGISTER_X86_FPSTACK_7 = 0x10,
        // add ARM stuff here
        REGISTER_ARM_LR = 15,
        REGISTER_ARM_PC = 0,
        REGISTER_ARM_R0 = 2,
        REGISTER_ARM_R1 = 3,
        REGISTER_ARM_R2 = 4,
        REGISTER_ARM_R3 = 5,
        REGISTER_ARM_R4 = 6,
        REGISTER_ARM_R5 = 7,
        REGISTER_ARM_R6 = 8,
        REGISTER_ARM_R7 = 9,
        REGISTER_ARM_R8 = 10,
        REGISTER_ARM_R9 = 11,
        REGISTER_ARM_R10 = 12,
        REGISTER_ARM_R11 = 13,
        REGISTER_ARM_R12 = 14,
        REGISTER_ARM_SP = 1,
    }



    [Flags]
    public enum CorDebugUserState
    {
        // Fields
        USER_NONE = 0x00,
        USER_STOP_REQUESTED = 0x01,
        USER_SUSPEND_REQUESTED = 0x02,
        USER_BACKGROUND = 0x04,
        USER_UNSTARTED = 0x08,
        USER_STOPPED = 0x10,
        USER_WAIT_SLEEP_JOIN = 0x20,
        USER_SUSPENDED = 0x40,
        USER_UNSAFE_POINT = 0x80
    }

    // Technically 0x40 is the only flag that could be set in combination with others, but we might
    // want to test for the presence of this value so we'll mark the enum as 'Flags'.
    // But in almost all cases we just use one of the individual values.
    // Reflection (Enum.ToString) appears to do a good job of picking the simplest combination to
    // represent a value as a set of flags, but the Visual Studio debugger does not - it just does
    // a linear search from the start looking for matches.  To make debugging these values easier,
    // their order is reversed so that VS always produces the simplest representation.
    [Flags]
    public enum CorElementType
    {
        ELEMENT_TYPE_PINNED = 0x45,
        ELEMENT_TYPE_SENTINEL = 0x41,
        ELEMENT_TYPE_MODIFIER = 0x40,

        ELEMENT_TYPE_MAX = 0x22,

        ELEMENT_TYPE_INTERNAL = 0x21,

        ELEMENT_TYPE_CMOD_OPT = 0x20,
        ELEMENT_TYPE_CMOD_REQD = 0x1f,

        ELEMENT_TYPE_MVAR = 0x1e,
        ELEMENT_TYPE_SZARRAY = 0x1d,
        ELEMENT_TYPE_OBJECT = 0x1c,
        ELEMENT_TYPE_FNPTR = 0x1b,
        ELEMENT_TYPE_U = 0x19,
        ELEMENT_TYPE_I = 0x18,

        ELEMENT_TYPE_TYPEDBYREF = 0x16,
        ELEMENT_TYPE_GENERICINST = 0x15,
        ELEMENT_TYPE_ARRAY = 0x14,
        ELEMENT_TYPE_VAR = 0x13,
        ELEMENT_TYPE_CLASS = 0x12,
        ELEMENT_TYPE_VALUETYPE = 0x11,

        ELEMENT_TYPE_BYREF = 0x10,
        ELEMENT_TYPE_PTR = 0xf,

        ELEMENT_TYPE_STRING = 0xe,
        ELEMENT_TYPE_R8 = 0xd,
        ELEMENT_TYPE_R4 = 0xc,
        ELEMENT_TYPE_U8 = 0xb,
        ELEMENT_TYPE_I8 = 0xa,
        ELEMENT_TYPE_U4 = 0x9,
        ELEMENT_TYPE_I4 = 0x8,
        ELEMENT_TYPE_U2 = 0x7,
        ELEMENT_TYPE_I2 = 0x6,
        ELEMENT_TYPE_U1 = 0x5,
        ELEMENT_TYPE_I1 = 0x4,
        ELEMENT_TYPE_CHAR = 0x3,
        ELEMENT_TYPE_BOOLEAN = 0x2,
        ELEMENT_TYPE_VOID = 0x1,
        ELEMENT_TYPE_END = 0x0
    }

    #region Top-level interfaces
    [ComImport, Guid("3D6F5F61-7538-11D3-8D5B-00104B35E7EF"), InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ICorDebug
    {
        //
        void Initialize();
        //
        void Terminate();
        //
        void SetManagedHandler([In, MarshalAs(UnmanagedType.Interface)] ICorDebugManagedCallback pCallback);
        //
        void SetUnmanagedHandler([In, MarshalAs(UnmanagedType.Interface)] ICorDebugUnmanagedCallback pCallback);
        //
        void CreateProcess([In, MarshalAs(UnmanagedType.LPWStr)] string lpApplicationName, [In, MarshalAs(UnmanagedType.LPWStr)] string lpCommandLine, [In] SECURITY_ATTRIBUTES lpProcessAttributes, [In] SECURITY_ATTRIBUTES lpThreadAttributes, [In] int bInheritHandles, [In] uint dwCreationFlags, [In] IntPtr lpEnvironment, [In, MarshalAs(UnmanagedType.LPWStr)] string lpCurrentDirectory, [In] STARTUPINFO lpStartupInfo, [In] PROCESS_INFORMATION lpProcessInformation, [In] CorDebugCreateProcessFlags debuggingFlags, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugProcess ppProcess);
        //
        void DebugActiveProcess([In] uint id, [In] int win32Attach, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugProcess ppProcess);
        //
        void EnumerateProcesses([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugProcessEnum ppProcess);
        //
        void GetProcess([In] uint dwProcessId, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugProcess ppProcess);
        //
        void CanLaunchOrAttach([In] uint dwProcessId, [In] int win32DebuggingEnabled);
    }

    [ComImport, Guid("C3ED8383-5A49-4cf5-B4B7-01864D9E582D"), InterfaceType(1)]
    public interface ICorDebugRemoteTarget
    {
        //
        void GetHostName([In] uint cchHostName, [Out] out uint pcchHostName, [In, Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] char[] szHostName);
    }

    [ComImport, Guid("D5EBB8E2-7BBE-4c1d-98A6-A3C04CBDEF64"), InterfaceType(1)]
    public interface ICorDebugRemote
    {
        //
        void CreateProcessEx([In, MarshalAs(UnmanagedType.Interface)] ICorDebugRemoteTarget pRemoteTarget, [In, MarshalAs(UnmanagedType.LPWStr)] string lpApplicationName, [In, MarshalAs(UnmanagedType.LPWStr)] string lpCommandLine, [In] SECURITY_ATTRIBUTES lpProcessAttributes, [In] SECURITY_ATTRIBUTES lpThreadAttributes, [In] int bInheritHandles, [In] uint dwCreationFlags, [In] IntPtr lpEnvironment, [In, MarshalAs(UnmanagedType.LPWStr)] string lpCurrentDirectory, [In] STARTUPINFO lpStartupInfo, [In] PROCESS_INFORMATION lpProcessInformation, [In] CorDebugCreateProcessFlags debuggingFlags, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugProcess ppProcess);
        //
        void DebugActiveProcessEx([In, MarshalAs(UnmanagedType.Interface)] ICorDebugRemoteTarget pRemoteTarget, [In] uint id, [In] int win32Attach, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugProcess ppProcess);
    }

    [ComImport, CoClass(typeof(CorDebugClass)), Guid("3D6F5F61-7538-11D3-8D5B-00104B35E7EF")]
    public interface CorDebug : ICorDebug
    {
    }



    [ComImport, ClassInterface((short)0), Guid("6fef44d0-39e7-4c77-be8e-c9f8cf988630"), TypeLibType((short)2)]
    public class CorDebugClass : ICorDebug, CorDebug
    {
        // Methods

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern virtual void CanLaunchOrAttach([In] uint dwProcessId, [In] int win32DebuggingEnabled);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern virtual void CreateProcess([In, MarshalAs(UnmanagedType.LPWStr)] string lpApplicationName, [In, MarshalAs(UnmanagedType.LPWStr)] string lpCommandLine, [In] SECURITY_ATTRIBUTES lpProcessAttributes, [In] SECURITY_ATTRIBUTES lpThreadAttributes, [In] int bInheritHandles, [In] uint dwCreationFlags, [In] IntPtr lpEnvironment, [In, MarshalAs(UnmanagedType.LPWStr)] string lpCurrentDirectory, [In] STARTUPINFO lpStartupInfo, [In] PROCESS_INFORMATION lpProcessInformation, [In] CorDebugCreateProcessFlags debuggingFlags, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugProcess ppProcess);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern virtual void DebugActiveProcess([In] uint id, [In] int win32Attach, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugProcess ppProcess);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern virtual void EnumerateProcesses([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugProcessEnum ppProcess);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern virtual void GetProcess([In] uint dwProcessId, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugProcess ppProcess);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern virtual void Initialize();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern virtual void SetManagedHandler([In, MarshalAs(UnmanagedType.Interface)] ICorDebugManagedCallback pCallback);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern virtual void SetUnmanagedHandler([In, MarshalAs(UnmanagedType.Interface)] ICorDebugUnmanagedCallback pCallback);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern virtual void Terminate();
    }

    [ComImport, Guid("3D6F5F61-7538-11D3-8D5B-00104B35E7EF"), CoClass(typeof(CorDebugManagerClass))]
    public interface CorDebugManager : ICorDebug
    {
    }

    [ComImport, ClassInterface((short)0), TypeLibType((short)2), Guid("B76B17EF-16FA-43A3-BABF-DB6E59439EB0")]
    public class CorDebugManagerClass : ICorDebug, CorDebugManager
    {
        // Methods

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern virtual void CanLaunchOrAttach([In] uint dwProcessId, [In] int win32DebuggingEnabled);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern virtual void CreateProcess([In, MarshalAs(UnmanagedType.LPWStr)] string lpApplicationName, [In, MarshalAs(UnmanagedType.LPWStr)] string lpCommandLine, [In] SECURITY_ATTRIBUTES lpProcessAttributes, [In] SECURITY_ATTRIBUTES lpThreadAttributes, [In] int bInheritHandles, [In] uint dwCreationFlags, [In] IntPtr lpEnvironment, [In, MarshalAs(UnmanagedType.LPWStr)] string lpCurrentDirectory, [In, ComAliasName("Microsoft.Debugging.CorDebug.NativeApi.ULONG_PTR")] STARTUPINFO lpStartupInfo, [In, ComAliasName("Microsoft.Debugging.CorDebug.NativeApi.ULONG_PTR")] PROCESS_INFORMATION lpProcessInformation, [In] CorDebugCreateProcessFlags debuggingFlags, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugProcess ppProcess);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern virtual void DebugActiveProcess([In] uint id, [In] int win32Attach, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugProcess ppProcess);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern virtual void EnumerateProcesses([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugProcessEnum ppProcess);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern virtual void GetProcess([In] uint dwProcessId, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugProcess ppProcess);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern virtual void Initialize();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern virtual void SetManagedHandler([In, MarshalAs(UnmanagedType.Interface)] ICorDebugManagedCallback pCallback);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern virtual void SetUnmanagedHandler([In, MarshalAs(UnmanagedType.Interface)] ICorDebugUnmanagedCallback pCallback);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern virtual void Terminate();
    }

    [ComImport, Guid("3D6F5F61-7538-11D3-8D5B-00104B35E7EF"), CoClass(typeof(EmbeddedCLRCorDebugClass))]
    public interface EmbeddedCLRCorDebug : ICorDebug
    {
    }

    [ComImport, TypeLibType(2), Guid("211F1254-BC7E-4AF5-B9AA-067308D83DD1"), ClassInterface((short)0)]
    public class EmbeddedCLRCorDebugClass : ICorDebug, EmbeddedCLRCorDebug
    {
        // Methods

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern virtual void CanLaunchOrAttach([In] uint dwProcessId, [In] int win32DebuggingEnabled);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern virtual void CreateProcess([In, MarshalAs(UnmanagedType.LPWStr)] string lpApplicationName, [In, MarshalAs(UnmanagedType.LPWStr)] string lpCommandLine, [In] SECURITY_ATTRIBUTES lpProcessAttributes, [In] SECURITY_ATTRIBUTES lpThreadAttributes, [In] int bInheritHandles, [In] uint dwCreationFlags, [In] IntPtr lpEnvironment, [In, MarshalAs(UnmanagedType.LPWStr)] string lpCurrentDirectory, [In, ComAliasName("Microsoft.Debugging.CorDebug.NativeApi.ULONG_PTR")] STARTUPINFO lpStartupInfo, [In, ComAliasName("Microsoft.Debugging.CorDebug.NativeApi.ULONG_PTR")] PROCESS_INFORMATION lpProcessInformation, [In] CorDebugCreateProcessFlags debuggingFlags, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugProcess ppProcess);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern virtual void DebugActiveProcess([In] uint id, [In] int win32Attach, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugProcess ppProcess);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern virtual void EnumerateProcesses([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugProcessEnum ppProcess);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern virtual void GetProcess([In] uint dwProcessId, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugProcess ppProcess);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern virtual void Initialize();

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern virtual void SetManagedHandler([In, MarshalAs(UnmanagedType.Interface)] ICorDebugManagedCallback pCallback);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern virtual void SetUnmanagedHandler([In, MarshalAs(UnmanagedType.Interface)] ICorDebugUnmanagedCallback pCallback);

        [MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        public extern virtual void Terminate();
    }
    #endregion // Top-level interfaces

    #region AppDomain, Process
    public enum CorDebugThreadState
    {
        THREAD_RUN,
        THREAD_SUSPEND
    }

    [ComImport, Guid("3D6F5F62-7538-11D3-8D5B-00104B35E7EF"), InterfaceType(1)]
    public interface ICorDebugController
    {

        void Stop([In] uint dwTimeout);

        void Continue([In] int fIsOutOfBand);

        void IsRunning([Out] out int pbRunning);

        void HasQueuedCallbacks([In, MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread, [Out] out int pbQueued);

        void EnumerateThreads([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugThreadEnum ppThreads);

        void SetAllThreadsDebugState([In] CorDebugThreadState state, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugThread pExceptThisThread);

        void Detach();

        void Terminate([In] uint exitCode);

        void CanCommitChanges([In] uint cSnapshots, [In, MarshalAs(UnmanagedType.Interface)] ref ICorDebugEditAndContinueSnapshot pSnapshots, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugErrorInfoEnum pError);

        void CommitChanges([In] uint cSnapshots, [In, MarshalAs(UnmanagedType.Interface)] ref ICorDebugEditAndContinueSnapshot pSnapshots, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugErrorInfoEnum pError);
    }

    [ComImport, ComConversionLoss, InterfaceType(1), Guid("3D6F5F64-7538-11D3-8D5B-00104B35E7EF")]
    public interface ICorDebugProcess : ICorDebugController
    {
        new void Stop([In] uint dwTimeout);
        /// <summary>
        /// fIsOutOfBand == 0 is the normalcase.   If fIsOutOfBand == 1 when continuing
        /// after an event that did not bring the runtime to a 'safe' spot.  
        /// </summary>
        /// <param name="fIsOutOfBand"></param>
        new void Continue([In] int fIsOutOfBand);

        new void IsRunning([Out] out int pbRunning);

        new void HasQueuedCallbacks([In, MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread, [Out] out int pbQueued);

        new void EnumerateThreads([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugThreadEnum ppThreads);

        new void SetAllThreadsDebugState([In] CorDebugThreadState state, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugThread pExceptThisThread);

        new void Detach();

        new void Terminate([In] uint exitCode);

        new void CanCommitChanges([In] uint cSnapshots, [In, MarshalAs(UnmanagedType.Interface)] ref ICorDebugEditAndContinueSnapshot pSnapshots, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugErrorInfoEnum pError);

        new void CommitChanges([In] uint cSnapshots, [In, MarshalAs(UnmanagedType.Interface)] ref ICorDebugEditAndContinueSnapshot pSnapshots, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugErrorInfoEnum pError);

        void GetID([Out] out uint pdwProcessId);

        void GetHandle([Out, ComAliasName("HPROCESS*")] out IntPtr phProcessHandle);

        void GetThread([In] uint dwThreadId, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugThread ppThread);

        void EnumerateObjects([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugObjectEnum ppObjects);

        void IsTransitionStub([In] ulong address, [Out] out int pbTransitionStub);

        void IsOSSuspended([In] uint threadID, [Out] out int pbSuspended);

        void GetThreadContext([In] uint threadID, [In] uint contextSize, [In, ComAliasName("BYTE*")] IntPtr context);

        void SetThreadContext([In] uint threadID, [In] uint contextSize, [In, ComAliasName("BYTE*")] IntPtr context);

        void ReadMemory([In] ulong address, [In] uint size, [Out, MarshalAs(UnmanagedType.LPArray)] byte[] buffer, [Out, ComAliasName("SIZE_T*")] out IntPtr read);

        void WriteMemory([In] ulong address, [In] uint size, [In, MarshalAs(UnmanagedType.LPArray)] byte[] buffer, [Out, ComAliasName("SIZE_T*")] out IntPtr written);

        void ClearCurrentException([In] uint threadID);

        void EnableLogMessages([In] int fOnOff);

        void ModifyLogSwitch([In, MarshalAs(UnmanagedType.LPWStr)] string pLogSwitchName, [In] int lLevel);

        void EnumerateAppDomains([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugAppDomainEnum ppAppDomains);

        void GetObject([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppObject);

        void ThreadForFiberCookie([In] uint fiberCookie, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugThread ppThread);

        void GetHelperThreadID([Out] out uint pThreadID);
    }

    [ComImport, Guid("AD1B3588-0EF0-4744-A496-AA09A9F80371"), InterfaceType(1), ComConversionLoss]
    public interface ICorDebugProcess2
    {

        void GetThreadForTaskID([In] ulong taskid, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugThread2 ppThread);

        void GetVersion([Out] out _COR_VERSION version);

        void SetUnmanagedBreakpoint([In] ulong address, [In] uint bufsize, [Out, MarshalAs(UnmanagedType.LPArray)] byte[] buffer, [Out] out uint bufLen);

        void ClearUnmanagedBreakpoint([In] ulong address);

        void SetDesiredNGENCompilerFlags([In] uint pdwFlags);

        void GetDesiredNGENCompilerFlags([Out] out uint pdwFlags);

        void GetReferenceValueFromGCHandle([In] IntPtr handle, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugReferenceValue pOutValue);
    }

    [ComImport, Guid("2EE06488-C0D4-42B1-B26D-F3795EF606FB"), InterfaceType(1), ComConversionLoss]
    public interface ICorDebugProcess3
    {
        void SetEnableCustomNotification([In, MarshalAs(UnmanagedType.Interface)] ICorDebugClass pClass, [In] int fOnOff);

    }

    [Flags]
    public enum CorDebugFilterFlagsWindows
    {
        None = 0,
        IS_FIRST_CHANCE = 0x1,
    };

    public enum CorDebugRecordFormat
    {
        None = 0,
        FORMAT_WINDOWS_EXCEPTIONRECORD32 = 1,
        FORMAT_WINDOWS_EXCEPTIONRECORD64 = 2,
    }

    public enum CorDebugStateChange
    {
        None = 0,
        PROCESS_RUNNING = 0x0000001,
        FLUSH_ALL = 0x0000002,
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct COR_HEAPOBJECT
    {
        public ulong address;       // The address (in heap) of the object.
        public ulong size;          // The total size of the object.
        public COR_TYPEID type;     // The fully instantiated type of the object.
    }

    public enum CorDebugGenerationTypes
    {
        CorDebug_Gen0 = 0,
        CorDebug_Gen1 = 1,
        CorDebug_Gen2 = 2,
        CorDebug_LOH = 3,
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct COR_SEGMENT
    {
        public ulong start;            // The start address of the segment.
        public ulong end;              // The end address of the segment.
        public CorDebugGenerationTypes type; // The generation of the segment.
        public uint heap;                   // The heap the segment resides in.
    }

    public enum CorDebugGCType
    {
        CorDebugWorkstationGC = 0,
        CorDebugServerGC = 1,
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct COR_HEAPINFO
    {
        public uint areGCStructuresValid;  // TRUE if it's ok to walk the heap, FALSE otherwise.
        public uint pointerSize;           // The size of pointers on the target architecture in bytes.
        public uint numHeaps;              // The number of logical GC heaps in the process.
        public uint concurrent;            // Is the GC concurrent?
        public CorDebugGCType gcType;      // Workstation or Server?
    }

    [ComImport, InterfaceType(1), Guid("A2FA0F8E-D045-11DF-AC8E-CE2ADFD72085"), ComConversionLoss]
    public interface ICorDebugHeapSegmentEnum : ICorDebugEnum
    {
        new void Skip([In] uint celt);

        new void Reset();

        new void Clone([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugEnum ppEnum);

        new void GetCount([Out] out uint pcelt);

        [PreserveSig, MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        int Next([In] uint celt, [Out, MarshalAs(UnmanagedType.LPArray)] COR_SEGMENT[] segs, [Out] out uint pceltFetched);
    }

    public enum CorGCReferenceType
    {
        CorHandleStrong = 1 << 0,
        CorHandleStrongPinning = 1 << 1,
        CorHandleWeakShort = 1 << 2,
        CorHandleWeakLong = 1 << 3,
        CorHandleWeakRefCount = 1 << 4,
        CorHandleStrongRefCount = 1 << 5,
        CorHandleStrongDependent = 1 << 6,
        CorHandleStrongAsyncPinned = 1 << 7,
        CorHandleStrongSizedByref = 1 << 8,

        CorReferenceStack = -2147483647,
        CorReferenceFinalizer = -2147483648,

        // Used for EnumHandles
        CorHandleStrongOnly = 0x1E3,
        CorHandleWeakOnly = 0x1C,
        CorHandleAll = 0x7FFFFFFF
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct COR_GC_REFERENCE
    {
        public ICorDebugAppDomain Domain;
        public ICorDebugValue Location;
        public CorGCReferenceType Type;
        public ulong ExtraData;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct COR_TYPEID : IEquatable<COR_TYPEID>
    {
        public ulong token1;
        public ulong token2;

        public override int GetHashCode()
        {
            return (int)token1 + (int)token2;
        }
        public override bool Equals(object obj)
        {
            if (!(obj is COR_TYPEID))
            {
                return false;
            }

            return Equals((COR_TYPEID)obj);
        }
        public bool Equals(COR_TYPEID other)
        {
            return token1 == other.token1 && token2 == other.token2;
        }
    };

    public enum CorComponentType
    {
        CorComponentGCRef,
        CorComponentValueClass,
        CorComponentPrimitive
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct COR_ARRAY_LAYOUT
    {
        public COR_TYPEID componentID;             // The type of objects the array contains
        public CorElementType componentType;     // Whether the component itself is a GC reference, value class, or primitive
        public int firstElementOffset;             // The offset to the first element
        public int elementSize;                    // The size of each element
        public int countOffset;                    // The offset to the number of elements in the array.

        // For multidimensional arrays (works with normal arrays too).
        public int rankSize;       // The size of the rank
        public int numRanks;       // The number of ranks in the array (1 for array, N for multidimensional array)
        public int rankOffset;     // The offset at which the ranks start
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct COR_TYPE_LAYOUT
    {
        public COR_TYPEID parentID;
        public int objectSize;
        public int numFields;
        public int boxOffset;
        public CorElementType type;
    };

    [StructLayout(LayoutKind.Sequential)]
    public struct COR_FIELD
    {
        public int token;          // FieldDef token to get the field info
        public int offset;         // Offset in object of data.
        public COR_TYPEID id;      // TYPEID of the field
        public CorElementType fieldType;
    };

    [ComImport, InterfaceType(1), Guid("7F3C24D3-7E1D-4245-AC3A-F72F8859C80C"), ComConversionLoss]
    public interface ICorDebugGCReferenceEnum : ICorDebugEnum
    {
        new void Skip([In] uint celt);

        new void Reset();

        new void Clone([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugEnum ppEnum);

        new void GetCount([Out] out uint pcelt);

        [PreserveSig, MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        int Next([In] uint celt, [Out, MarshalAs(UnmanagedType.LPArray)] COR_GC_REFERENCE[] segs, [Out] out uint pceltFetched);
    }

    [ComImport, InterfaceType(1), Guid("76D7DAB8-D044-11DF-9A15-7E29DFD72085"), ComConversionLoss]
    public interface ICorDebugHeapEnum : ICorDebugEnum
    {
        new void Skip([In] uint celt);

        new void Reset();

        new void Clone([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugEnum ppEnum);

        new void GetCount([Out] out uint pcelt);

        [PreserveSig, MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        int Next([In] uint celt, [Out, MarshalAs(UnmanagedType.LPArray)] COR_HEAPOBJECT[] objs, [Out] out uint pceltFetched);
    }

    [ComImport, ComConversionLoss, InterfaceType(1), Guid("E930C679-78AF-4953-8AB7-B0AABF0F9F80")]
    public interface ICorDebugProcess4
    {
        void Filter(
             [In] IntPtr pRecord,
             [In] uint countBytes,
             [In] CorDebugRecordFormat format,
             [In] CorDebugFilterFlagsWindows dwFlags,
             [In] uint dwThreadId,
             [In] ICorDebugManagedCallback pCallback,
             [In][Out] ref uint dwContinueStatus);

        void ProcessStateChanged([In] CorDebugStateChange eChange);
    }

    public enum CorDebugNGENPolicyFlags
    {
        DISABLE_LOCAL_NIC = 1
    }

    [ComImport, ComConversionLoss, InterfaceType(1), Guid("21e9d9c0-fcb8-11df-8cff-0800200c9a66")]
    public interface ICorDebugProcess5
    {
        void GetGCHeapInformation([Out] out COR_HEAPINFO pHeapInfo);
        void EnumerateHeap([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugHeapEnum ppObjects);
        void EnumerateHeapRegions([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugHeapSegmentEnum ppRegions);
        void GetObject([In] ulong addr, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugObjectValue ppObject);
        void EnumerateGCReferences([In] int bEnumerateWeakReferences, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugGCReferenceEnum ppEnum);
        void EnumerateHandles([In] uint types, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugGCReferenceEnum ppEnum);

        // This is used for fast Heap dumping.  You have to keep track of field layout but you can bulk copy everything. 
        void GetTypeID([In] ulong objAddr, [Out] out COR_TYPEID pId);
        void GetTypeForTypeID([In] COR_TYPEID id, [Out] out ICorDebugType type);
        void GetArrayLayout([In] COR_TYPEID id, [Out] out COR_ARRAY_LAYOUT layout);
        void GetTypeLayout([In] COR_TYPEID id, [Out] out COR_TYPE_LAYOUT layout);
        void GetTypeFields([In] COR_TYPEID id, int celt, [Out, MarshalAs(UnmanagedType.LPArray)] COR_FIELD[] fields, [Out] out int pceltNeeded);

        void EnableNGENPolicy(CorDebugNGENPolicyFlags ePolicy);
    }

    [ComImport, ComConversionLoss, InterfaceType(1), Guid("CC7BCB05-8A68-11D2-983C-0000F808342D")]
    public interface ICorDebugProcessEnum : ICorDebugEnum
    {

        new void Skip([In] uint celt);

        new void Reset();

        new void Clone([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugEnum ppEnum);

        new void GetCount([Out] out uint pcelt);
        [PreserveSig, MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        int Next([In] uint celt, [Out, MarshalAs(UnmanagedType.LPArray)] ICorDebugProcess[] processes, [Out] out uint pceltFetched);
    }

    [ComImport, ComConversionLoss, InterfaceType(1), Guid("3D6F5F63-7538-11D3-8D5B-00104B35E7EF")]
    public interface ICorDebugAppDomain : ICorDebugController
    {
        new void Stop([In] uint dwTimeout);

        new void Continue([In] int fIsOutOfBand);

        new void IsRunning([Out] out int pbRunning);

        new void HasQueuedCallbacks([In, MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread, [Out] out int pbQueued);

        new void EnumerateThreads([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugThreadEnum ppThreads);

        new void SetAllThreadsDebugState([In] CorDebugThreadState state, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugThread pExceptThisThread);

        new void Detach();

        new void Terminate([In] uint exitCode);

        new void CanCommitChanges([In] uint cSnapshots, [In, MarshalAs(UnmanagedType.Interface)] ref ICorDebugEditAndContinueSnapshot pSnapshots, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugErrorInfoEnum pError);

        new void CommitChanges([In] uint cSnapshots, [In, MarshalAs(UnmanagedType.Interface)] ref ICorDebugEditAndContinueSnapshot pSnapshots, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugErrorInfoEnum pError);

        void GetProcess([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugProcess ppProcess);

        void EnumerateAssemblies([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugAssemblyEnum ppAssemblies);

        void GetModuleFromMetaDataInterface([In, MarshalAs(UnmanagedType.IUnknown)] object pIMetaData, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugModule ppModule);

        void EnumerateBreakpoints([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugBreakpointEnum ppBreakpoints);

        void EnumerateSteppers([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugStepperEnum ppSteppers);

        void IsAttached([Out] out int pbAttached);

        void GetName([In] uint cchName, [Out] out uint pcchName, [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder szName);

        void GetObject([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppObject);

        void Attach();

        void GetID([Out] out uint pId);
    }

    [ComImport, InterfaceType(1), Guid("096E81D5-ECDA-4202-83F5-C65980A9EF75")]
    public interface ICorDebugAppDomain2
    {

        void GetArrayOrPointerType([In] CorElementType elementType, [In] uint nRank, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugType pTypeArg, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugType ppType);

        void GetFunctionPointerType([In] uint nTypeArgs, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] ICorDebugType[] ppTypeArgs, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugType ppType);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CorDebugGuidToTypeMapping
    {
        public System.Guid iid;
        public ICorDebugType icdType;
    }

    [ComImport, InterfaceType(1), Guid("6164D242-1015-4BD6-8CBE-D0DBD4B8275A")]
    public interface ICorDebugGuidToTypeEnum : ICorDebugEnum
    {

        new void Skip([In] uint celt);

        new void Reset();

        new void Clone([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugEnum ppEnum);

        new void GetCount([Out] out uint pcelt);
        [PreserveSig, MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        int Next([In] uint celt, [Out, MarshalAs(UnmanagedType.LPArray)] CorDebugGuidToTypeMapping[] values, [Out] out uint pceltFetched);
    }

    [ComImport, InterfaceType(1), Guid("8CB96A16-B588-42E2-B71C-DD849FC2ECCC")]
    public interface ICorDebugAppDomain3
    {
        void GetCachedWinRTTypesForIIDs([In] uint cReqTypes, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] System.Guid[] iidsToResolve, out ICorDebugTypeEnum ppTypesEnum);

        void GetCachedWinRTTypes(out ICorDebugGuidToTypeEnum ppGuidToTypeEnum);
    }

    [ComImport, InterfaceType(1), Guid("63CA1B24-4359-4883-BD57-13F815F58744"), ComConversionLoss]
    public interface ICorDebugAppDomainEnum : ICorDebugEnum
    {

        new void Skip([In] uint celt);

        new void Reset();

        new void Clone([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugEnum ppEnum);

        new void GetCount([Out] out uint pcelt);
        [PreserveSig, MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        int Next([In] uint celt, [Out, MarshalAs(UnmanagedType.LPArray)] ICorDebugAppDomain[] values, [Out] out uint pceltFetched);
    }

    #endregion // AppDomain, Process

    #region Assembly

    [ComImport, ComConversionLoss, Guid("DF59507C-D47A-459E-BCE2-6427EAC8FD06"), InterfaceType(1)]
    public interface ICorDebugAssembly
    {

        void GetProcess([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugProcess ppProcess);

        void GetAppDomain([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugAppDomain ppAppDomain);

        void EnumerateModules([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugModuleEnum ppModules);

        void GetCodeBase([In] uint cchName, [Out] out uint pcchName, [MarshalAs(UnmanagedType.LPArray)] char[] szName);

        void GetName([In] uint cchName, [Out] out uint pcchName, [MarshalAs(UnmanagedType.LPArray)] char[] szName);
    }

    [ComImport, InterfaceType(1), Guid("426D1F9E-6DD4-44C8-AEC7-26CDBAF4E398")]
    public interface ICorDebugAssembly2
    {

        void IsFullyTrusted([Out] out int pbFullyTrusted);
    }

    [ComImport, Guid("4A2A1EC9-85EC-4BFB-9F15-A89FDFE0FE83"), ComConversionLoss, InterfaceType(1)]
    public interface ICorDebugAssemblyEnum : ICorDebugEnum
    {

        new void Skip([In] uint celt);

        new void Reset();

        new void Clone([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugEnum ppEnum);

        new void GetCount([Out] out uint pcelt);
        [PreserveSig, MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        int Next([In] uint celt, [Out, MarshalAs(UnmanagedType.LPArray)] ICorDebugAssembly[] values, [Out] out uint pceltFetched);
    }
    #endregion Assembly

    #region Execution Control

    #region Breakpoints
    [ComImport, InterfaceType(1), Guid("CC7BCAE8-8A68-11D2-983C-0000F808342D")]
    public interface ICorDebugBreakpoint
    {

        void Activate([In] int bActive);

        void IsActive([Out] out int pbActive);
    }

    [ComImport, Guid("CC7BCB03-8A68-11D2-983C-0000F808342D"), ComConversionLoss, InterfaceType(1)]
    public interface ICorDebugBreakpointEnum : ICorDebugEnum
    {

        new void Skip([In] uint celt);

        new void Reset();

        new void Clone([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugEnum ppEnum);

        new void GetCount([Out] out uint pcelt);
        [PreserveSig, MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        int Next([In] uint celt, [Out, MarshalAs(UnmanagedType.LPArray)] ICorDebugBreakpoint[] breakpoints, [Out] out uint pceltFetched);
    }

    [ComImport, Guid("CC7BCAE9-8A68-11D2-983C-0000F808342D"), InterfaceType(1)]
    public interface ICorDebugFunctionBreakpoint : ICorDebugBreakpoint
    {

        new void Activate([In] int bActive);

        new void IsActive([Out] out int pbActive);


        void GetFunction([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugFunction ppFunction);

        void GetOffset([Out] out uint pnOffset);
    }

    #endregion Breakpoints

    #region Stepping
    [Flags]
    public enum CorDebugUnmappedStop
    {
        // Fields
        STOP_ALL = 0xffff,
        STOP_NONE = 0,
        STOP_PROLOG = 1,
        STOP_EPILOG = 2,
        STOP_NO_MAPPING_INFO = 4,
        STOP_OTHER_UNMAPPED = 8,
        STOP_UNMANAGED = 0x10
    }

    public enum CorDebugStepReason
    {
        STEP_NORMAL,
        STEP_RETURN,
        STEP_CALL,
        STEP_EXCEPTION_FILTER,
        STEP_EXCEPTION_HANDLER,
        STEP_INTERCEPT,
        STEP_EXIT
    }
    [Flags]
    public enum CorDebugIntercept
    {
        // Fields
        INTERCEPT_NONE = 0,
        INTERCEPT_ALL = 0xffff,
        INTERCEPT_CLASS_INIT = 1,
        INTERCEPT_EXCEPTION_FILTER = 2,
        INTERCEPT_SECURITY = 4,
        INTERCEPT_CONTEXT_POLICY = 8,
        INTERCEPT_INTERCEPTION = 0x10,
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct COR_DEBUG_STEP_RANGE
    {
        public uint startOffset;
        public uint endOffset;
    }

    [ComImport, Guid("CC7BCAEC-8A68-11D2-983C-0000F808342D"), InterfaceType(1)]
    public interface ICorDebugStepper
    {

        void IsActive([Out] out int pbActive);

        void Deactivate();

        void SetInterceptMask([In] CorDebugIntercept mask);

        void SetUnmappedStopMask([In] CorDebugUnmappedStop mask);

        void Step([In] int bStepIn);

        void StepRange([In] int bStepIn, [In, MarshalAs(UnmanagedType.LPArray)] COR_DEBUG_STEP_RANGE[] ranges, [In] uint cRangeCount);

        void StepOut();

        void SetRangeIL([In] int bIL);
    }

    [ComImport, Guid("C5B6E9C3-E7D1-4A8E-873B-7F047F0706F7"), InterfaceType(1)]
    public interface ICorDebugStepper2
    {

        void SetJMC([In] int fIsJMCStepper);
    }

    [ComImport, ComConversionLoss, Guid("CC7BCB04-8A68-11D2-983C-0000F808342D"), InterfaceType(1)]
    public interface ICorDebugStepperEnum : ICorDebugEnum
    {

        new void Skip([In] uint celt);

        new void Reset();

        new void Clone([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugEnum ppEnum);

        new void GetCount([Out] out uint pcelt);
        [PreserveSig, MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        int Next([In] uint celt, [Out, MarshalAs(UnmanagedType.LPArray)] ICorDebugStepper[] steppers, [Out] out uint pceltFetched);
    }

    #endregion

    #endregion Execution Control

    #region Class, Type

    [ComImport, Guid("CC7BCAF5-8A68-11D2-983C-0000F808342D"), InterfaceType(1)]
    public interface ICorDebugClass
    {

        void GetModule([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugModule pModule);

        void GetToken([Out] out uint pTypeDef);

        void GetStaticFieldValue([In] uint fieldDef, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugFrame pFrame, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppValue);
    }

    [ComImport, Guid("B008EA8D-7AB1-43F7-BB20-FBB5A04038AE"), InterfaceType(1)]
    public interface ICorDebugClass2
    {

        void GetParameterizedType([In] CorElementType elementType, [In] uint nTypeArgs, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] ICorDebugType[] ppTypeArgs, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugType ppType);

        void SetJMCStatus([In] int bIsJustMyCode);
    }

    [ComImport, Guid("D613F0BB-ACE1-4C19-BD72-E4C08D5DA7F5"), InterfaceType(1)]
    public interface ICorDebugType
    {
        void GetType([Out] out CorElementType ty);

        void GetClass([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugClass ppClass);

        void EnumerateTypeParameters([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugTypeEnum ppTyParEnum);

        void GetFirstTypeParameter([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugType value);

        void GetBase([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugType pBase);

        void GetStaticFieldValue([In] uint fieldDef, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugFrame pFrame, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppValue);

        void GetRank([Out] out uint pnRank);
    }

    [ComImport, Guid("10F27499-9DF2-43CE-8333-A321D7C99CB4"), InterfaceType(1), ComConversionLoss]
    public interface ICorDebugTypeEnum : ICorDebugEnum
    {

        new void Skip([In] uint celt);

        new void Reset();

        new void Clone([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugEnum ppEnum);

        new void GetCount([Out] out uint pcelt);
        [PreserveSig, MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        int Next([In] uint celt, [Out, MarshalAs(UnmanagedType.LPArray)] ICorDebugType[] values, [Out] out uint pceltFetched);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct CorDebugExceptionObjectStackFrame
    {
        public ICorDebugModule pModule;
        public UInt64 ip;
        public int methodDef;
        public bool isLastForeignException;
    }

    [ComImport, ComConversionLoss, InterfaceType(1), Guid("ED775530-4DC4-41F7-86D0-9E2DEF7DFC66")]
    public interface ICorDebugExceptionObjectCallStackEnum : ICorDebugEnum
    {
        new void Skip([In] uint celt);

        new void Reset();

        new void Clone([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugEnum ppEnum);

        new void GetCount([Out] out uint pcelt);

        [PreserveSig, MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        int Next([In] uint celt, [Out, MarshalAs(UnmanagedType.LPArray)] CorDebugExceptionObjectStackFrame[] values, [Out] out uint pceltFetched);
    }

    [ComImport, Guid("AE4CA65D-59DD-42A2-83A5-57E8A08D8719"), InterfaceType(1)]
    public interface ICorDebugExceptionObjectValue
    {
        void EnumerateExceptionCallStack([Out] out ICorDebugExceptionObjectCallStackEnum ppCallStackEnum);
    }

    [ComImport, Guid("5F69C5E5-3E12-42DF-B371-F9D761D6EE24"), InterfaceType(1)]
    public interface ICorDebugComObjectValue
    {
        void GetCachedInterfaceTypes([In] bool bIInspectableOnly, [Out] out ICorDebugTypeEnum ppInterfacesEnum);
    }

    #endregion // Class, Type

    #region Code and Function
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct COR_DEBUG_IL_TO_NATIVE_MAP
    {
        public uint ilOffset;
        public uint nativeStartOffset;
        public uint nativeEndOffset;
    }

    [ComImport, InterfaceType(1), Guid("CC7BCAF4-8A68-11D2-983C-0000F808342D"), ComConversionLoss]
    public interface ICorDebugCode
    {

        void IsIL([Out] out int pbIL);

        void GetFunction([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugFunction ppFunction);

        void GetAddress([Out] out ulong pStart);

        void GetSize([Out] out uint pcBytes);

        void CreateBreakpoint([In] uint offset, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugFunctionBreakpoint ppBreakpoint);


        void GetCode([In] uint startOffset, [In] uint endOffset, [In] uint cBufferAlloc, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] byte[] buffer, [Out] out uint pcBufferSize);

        void GetVersionNumber([Out] out uint nVersion);

        void GetILToNativeMapping([In] uint cMap, [Out] out uint pcMap, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] COR_DEBUG_IL_TO_NATIVE_MAP[] map);

        void GetEnCRemapSequencePoints([In] uint cMap, [Out] out uint pcMap, [Out, MarshalAs(UnmanagedType.LPArray)] uint[] offsets);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct _CodeChunkInfo
    {
        public ulong startAddr;
        public uint length;
    }

    [ComImport, ComConversionLoss, Guid("5F696509-452F-4436-A3FE-4D11FE7E2347"), InterfaceType(1)]
    public interface ICorDebugCode2
    {

        void GetCodeChunks([In] uint cbufSize, [Out] out uint pcnumChunks, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] _CodeChunkInfo[] chunks);

        void GetCompilerFlags([Out] out uint pdwFlags);
    }

    [ComImport, Guid("55E96461-9645-45E4-A2FF-0367877ABCDE"), InterfaceType(1), ComConversionLoss]
    public interface ICorDebugCodeEnum : ICorDebugEnum
    {

        new void Skip([In] uint celt);

        new void Reset();

        new void Clone([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugEnum ppEnum);

        new void GetCount([Out] out uint pcelt);
        [PreserveSig, MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        int Next([In] uint celt, [Out, MarshalAs(UnmanagedType.LPArray)] ICorDebugCode[] values, [Out] out uint pceltFetched);
    }

    [ComImport, Guid("CC7BCAF3-8A68-11D2-983C-0000F808342D"), InterfaceType(1)]
    public interface ICorDebugFunction
    {

        void GetModule([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugModule ppModule);

        void GetClass([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugClass ppClass);

        void GetToken([Out] out uint pMethodDef);

        void GetILCode([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugCode ppCode);

        void GetNativeCode([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugCode ppCode);

        void CreateBreakpoint([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugFunctionBreakpoint ppBreakpoint);

        void GetLocalVarSigToken([Out] out uint pmdSig);

        void GetCurrentVersionNumber([Out] out uint pnCurrentVersion);
    }

    [ComImport, InterfaceType(1), Guid("EF0C490B-94C3-4E4D-B629-DDC134C532D8")]
    public interface ICorDebugFunction2
    {

        void SetJMCStatus([In] int bIsJustMyCode);

        void GetJMCStatus([Out] out int pbIsJustMyCode);

        void EnumerateNativeCode([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugCodeEnum ppCodeEnum);

        void GetVersionNumber([Out] out uint pnVersion);
    }


    #endregion Code and Function

    #region Deprecated
    //
    // These interfaces are not used
    //

    [ComImport, InterfaceType(1), Guid("CC7BCB00-8A68-11D2-983C-0000F808342D")]
    public interface ICorDebugContext : ICorDebugObjectValue
    {

        new void GetType([Out] out CorElementType pType);

        new void GetSize([Out] out uint pSize);

        new void GetAddress([Out] out ulong pAddress);

        new void CreateBreakpoint([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValueBreakpoint ppBreakpoint);


        new void GetClass([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugClass ppClass);

        new void GetFieldValue([In, MarshalAs(UnmanagedType.Interface)] ICorDebugClass pClass, [In] uint fieldDef, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppValue);

        new void GetVirtualMethod([In] uint memberRef, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugFunction ppFunction);

        new void GetContext([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugContext ppContext);

        new void IsValueClass([Out] out int pbIsValueClass);

        new void GetManagedCopy([Out, MarshalAs(UnmanagedType.IUnknown)] out object ppObject);

        new void SetFromManagedCopy([In, MarshalAs(UnmanagedType.IUnknown)] object pObject);
    }

    [ComImport, InterfaceType(1), Guid("6DC3FA01-D7CB-11D2-8A95-0080C792E5D8")]
    public interface ICorDebugEditAndContinueSnapshot
    {

        void CopyMetaData([In, MarshalAs(UnmanagedType.Interface)] IStream pIStream, [Out] out Guid pMvid);

        void GetMvid([Out] out Guid pMvid);

        void GetRoDataRVA([Out] out uint pRoDataRVA);

        void GetRwDataRVA([Out] out uint pRwDataRVA);

        void SetPEBytes([In, MarshalAs(UnmanagedType.Interface)] IStream pIStream);

        void SetILMap([In] uint mdFunction, [In] uint cMapSize, [In] ref COR_IL_MAP map);

        void SetPESymbolBytes([In, MarshalAs(UnmanagedType.Interface)] IStream pIStream);
    }

    [ComImport, ComConversionLoss, InterfaceType(1), Guid("F0E18809-72B5-11D2-976F-00A0C9B4D50C")]
    public interface ICorDebugErrorInfoEnum : ICorDebugEnum
    {

        new void Skip([In] uint celt);

        new void Reset();

        new void Clone([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugEnum ppEnum);

        new void GetCount([Out] out uint pcelt);
        [PreserveSig, MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        int Next([In] uint celt, [Out, ComAliasName("ICorDebugEditAndContinueErrorInfo**")] IntPtr errors, [Out] out uint pceltFetched);
    }
    #endregion Deprecated

    [ComImport, InterfaceType(1), Guid("CC7BCB01-8A68-11D2-983C-0000F808342D")]
    public interface ICorDebugEnum
    {

        void Skip([In] uint celt);

        void Reset();

        void Clone([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugEnum ppEnum);

        void GetCount([Out] out uint pcelt);
    }

    #region Function Evaluation
    [ComImport, InterfaceType(1), Guid("CC7BCAF6-8A68-11D2-983C-0000F808342D")]
    public interface ICorDebugEval
    {

        void CallFunction([In, MarshalAs(UnmanagedType.Interface)] ICorDebugFunction pFunction, [In] uint nArgs, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] ICorDebugValue[] ppArgs);

        void NewObject([In, MarshalAs(UnmanagedType.Interface)] ICorDebugFunction pConstructor, [In] uint nArgs, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] ICorDebugValue[] ppArgs);

        void NewObjectNoConstructor([In, MarshalAs(UnmanagedType.Interface)] ICorDebugClass pClass);

        void NewString([In, MarshalAs(UnmanagedType.LPWStr)] string @string);

        void NewArray([In] CorElementType elementType, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugClass pElementClass, [In] uint rank, [In] ref uint dims, [In] ref uint lowBounds);

        void IsActive([Out] out int pbActive);

        void Abort();

        void GetResult([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppResult);

        void GetThread([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugThread ppThread);

        void CreateValue([In] CorElementType elementType, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugClass pElementClass, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppValue);
    }

    [ComImport, Guid("FB0D9CE7-BE66-4683-9D32-A42A04E2FD91"), InterfaceType(1)]
    public interface ICorDebugEval2
    {

        void CallParameterizedFunction([In, MarshalAs(UnmanagedType.Interface)] ICorDebugFunction pFunction, [In] uint nTypeArgs, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] ICorDebugType[] ppTypeArgs, [In] uint nArgs, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] ICorDebugValue[] ppArgs);

        void CreateValueForType([In, MarshalAs(UnmanagedType.Interface)] ICorDebugType pType, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppValue);

        void NewParameterizedObject([In, MarshalAs(UnmanagedType.Interface)] ICorDebugFunction pConstructor, [In] uint nTypeArgs, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] ICorDebugType[] ppTypeArgs, [In] uint nArgs, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 3)] ICorDebugValue[] ppArgs);

        void NewParameterizedObjectNoConstructor([In, MarshalAs(UnmanagedType.Interface)] ICorDebugClass pClass, [In] uint nTypeArgs, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] ICorDebugType[] ppTypeArgs);

        void NewParameterizedArray([In, MarshalAs(UnmanagedType.Interface)] ICorDebugType pElementType, [In] uint rank, [In] ref uint dims, [In] ref uint lowBounds);

        void NewStringWithLength([In, MarshalAs(UnmanagedType.LPWStr)] string @string, [In] uint uiLength);

        void RudeAbort();
    }
    #endregion Function Evaluation

    #region ICorDebugValue

    [ComImport, Guid("CC7BCAF7-8A68-11D2-983C-0000F808342D"), InterfaceType(1)]
    public interface ICorDebugValue
    {

        void GetType([Out] out CorElementType pType);

        void GetSize([Out] out uint pSize);

        void GetAddress([Out] out ulong pAddress);

        void CreateBreakpoint([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValueBreakpoint ppBreakpoint);
    }

    [ComImport, Guid("5E0B54E7-D88A-4626-9420-A691E0A78B49"), InterfaceType(1)]
    public interface ICorDebugValue2
    {

        void GetExactType([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugType ppType);
    }

    [ComImport, Guid("565005FC-0F8A-4F3E-9EDB-83102B156595"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ICorDebugValue3
    {
        void GetSize64(out ulong pSize);
    }

    [ComImport, InterfaceType(1), Guid("CC7BCAF8-8A68-11D2-983C-0000F808342D")]
    public interface ICorDebugGenericValue : ICorDebugValue
    {

        new void GetType([Out] out CorElementType pType);

        new void GetSize([Out] out uint pSize);

        new void GetAddress([Out] out ulong pAddress);

        new void CreateBreakpoint([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValueBreakpoint ppBreakpoint);


        void GetValue([Out] IntPtr pTo);

        void SetValue([In] IntPtr pFrom);
    }

    [ComImport, InterfaceType(1), Guid("CC7BCAF9-8A68-11D2-983C-0000F808342D")]
    public interface ICorDebugReferenceValue : ICorDebugValue
    {

        new void GetType([Out] out CorElementType pType);

        new void GetSize([Out] out uint pSize);

        new void GetAddress([Out] out ulong pAddress);

        new void CreateBreakpoint([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValueBreakpoint ppBreakpoint);


        void IsNull([Out] out int pbNull);

        void GetValue([Out] out ulong pValue);

        void SetValue([In] ulong value);

        void Dereference([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppValue);

        void DereferenceStrong([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppValue);
    }


    public enum CorDebugHandleType
    {
        // Fields
        HANDLE_STRONG = 1,
        HANDLE_WEAK_TRACK_RESURRECTION = 2
    }

    [ComImport, Guid("029596E8-276B-46A1-9821-732E96BBB00B"), InterfaceType(1)]
    public interface ICorDebugHandleValue : ICorDebugReferenceValue
    {

        new void GetType([Out] out CorElementType pType);

        new void GetSize([Out] out uint pSize);

        new void GetAddress([Out] out ulong pAddress);

        new void CreateBreakpoint([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValueBreakpoint ppBreakpoint);


        new void IsNull([Out] out int pbNull);

        new void GetValue([Out] out ulong pValue);

        new void SetValue([In] ulong value);

        new void Dereference([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppValue);

        new void DereferenceStrong([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppValue);


        void GetHandleType([Out] out CorDebugHandleType pType);

        void Dispose();
    }

    [ComImport, Guid("CC7BCAFA-8A68-11D2-983C-0000F808342D"), InterfaceType(1)]
    public interface ICorDebugHeapValue : ICorDebugValue
    {

        new void GetType([Out] out CorElementType pType);

        new void GetSize([Out] out uint pSize);

        new void GetAddress([Out] out ulong pAddress);

        new void CreateBreakpoint([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValueBreakpoint ppBreakpoint);



        void IsValid([Out] out int pbValid);

        void CreateRelocBreakpoint([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValueBreakpoint ppBreakpoint);

    }

    [ComImport, InterfaceType(1), Guid("E3AC4D6C-9CB7-43E6-96CC-B21540E5083C")]
    public interface ICorDebugHeapValue2
    {

        void CreateHandle([In] CorDebugHandleType type, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugHandleValue ppHandle);
    }

    [ComImport,
     InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
     Guid("A69ACAD8-2374-46e9-9FF8-B1F14120D296")]
    public interface ICorDebugHeapValue3
    {
        void GetThreadOwningMonitorLock([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugThread thread,
                                       [Out] out int acquisitionCount);
        void GetMonitorEventWaitList([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugThreadEnum threadEnum);
    }

    [ComImport, InterfaceType(1), Guid("CC7BCAFC-8A68-11D2-983C-0000F808342D")]
    public interface ICorDebugBoxValue : ICorDebugHeapValue
    {

        new void GetType([Out] out CorElementType pType);

        new void GetSize([Out] out uint pSize);

        new void GetAddress([Out] out ulong pAddress);

        new void CreateBreakpoint([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValueBreakpoint ppBreakpoint);


        new void IsValid([Out] out int pbValid);


        new void CreateRelocBreakpoint([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValueBreakpoint ppBreakpoint);


        void GetObject([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugObjectValue ppObject);
    }

    [ComImport, ComConversionLoss, Guid("0405B0DF-A660-11D2-BD02-0000F80849BD"), InterfaceType(1)]
    public interface ICorDebugArrayValue : ICorDebugHeapValue
    {

        new void GetType([Out] out CorElementType pType);

        new void GetSize([Out] out uint pSize);

        new void GetAddress([Out] out ulong pAddress);

        new void CreateBreakpoint([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValueBreakpoint ppBreakpoint);


        new void IsValid([Out] out int pbValid);


        new void CreateRelocBreakpoint([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValueBreakpoint ppBreakpoint);


        void GetElementType([Out] out CorElementType pType);

        void GetRank([Out] out uint pnRank);

        void GetCount([Out] out uint pnCount);

        void GetDimensions([In] uint cdim, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] uint[] dims);

        void HasBaseIndicies([Out] out int pbHasBaseIndicies);

        void GetBaseIndicies([In] uint cdim, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] uint[] indicies);

        void GetElement([In] uint cdim, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] int[] indices, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppValue);

        void GetElementAtPosition([In] uint nPosition, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppValue);
    }




    [ComImport, InterfaceType(1), Guid("CC7BCAEB-8A68-11D2-983C-0000F808342D")]
    public interface ICorDebugValueBreakpoint : ICorDebugBreakpoint
    {

        new void Activate([In] int bActive);

        new void IsActive([Out] out int pbActive);


        void GetValue([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppValue);
    }

    [ComImport, ComConversionLoss, Guid("CC7BCB0A-8A68-11D2-983C-0000F808342D"), InterfaceType(1)]
    public interface ICorDebugValueEnum : ICorDebugEnum
    {

        new void Skip([In] uint celt);

        new void Reset();

        new void Clone([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugEnum ppEnum);

        new void GetCount([Out] out uint pcelt);
        [PreserveSig, MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        int Next([In] uint celt, [Out, MarshalAs(UnmanagedType.LPArray)] ICorDebugValue[] values, [Out] out uint pceltFetched);
    }


    [ComImport, Guid("CC7BCAFD-8A68-11D2-983C-0000F808342D"), ComConversionLoss, InterfaceType(1)]
    public interface ICorDebugStringValue : ICorDebugHeapValue
    {

        new void GetType([Out] out CorElementType pType);

        new void GetSize([Out] out uint pSize);

        new void GetAddress([Out] out ulong pAddress);

        new void CreateBreakpoint([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValueBreakpoint ppBreakpoint);


        new void IsValid([Out] out int pbValid);

        new void CreateRelocBreakpoint([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValueBreakpoint ppBreakpoint);


        void GetLength([Out] out uint pcchString);

        void GetString([In] uint cchString, [Out] out uint pcchString, [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder szString);
    }



    [ComImport, InterfaceType(1), Guid("18AD3D6E-B7D2-11D2-BD04-0000F80849BD")]
    public interface ICorDebugObjectValue : ICorDebugValue
    {

        new void GetType([Out] out CorElementType pType);

        new void GetSize([Out] out uint pSize);

        new void GetAddress([Out] out ulong pAddress);

        new void CreateBreakpoint([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValueBreakpoint ppBreakpoint);


        void GetClass([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugClass ppClass);

        void GetFieldValue([In, MarshalAs(UnmanagedType.Interface)] ICorDebugClass pClass, [In] uint fieldDef, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppValue);

        void GetVirtualMethod([In] uint memberRef, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugFunction ppFunction);

        void GetContext([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugContext ppContext);

        void IsValueClass([Out] out int pbIsValueClass);

        void GetManagedCopy([Out, MarshalAs(UnmanagedType.IUnknown)] out object ppObject);

        void SetFromManagedCopy([In, MarshalAs(UnmanagedType.IUnknown)] object pObject);
    }

    [ComImport, Guid("49E4A320-4A9B-4ECA-B105-229FB7D5009F"), InterfaceType(1)]
    public interface ICorDebugObjectValue2
    {

        void GetVirtualMethodAndType([In] uint memberRef, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugFunction ppFunction, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugType ppType);
    }

    [ComImport, ComConversionLoss, Guid("CC7BCB02-8A68-11D2-983C-0000F808342D"), InterfaceType(1)]
    public interface ICorDebugObjectEnum : ICorDebugEnum
    {

        new void Skip([In] uint celt);

        new void Reset();

        new void Clone([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugEnum ppEnum);

        new void GetCount([Out] out uint pcelt);
        [PreserveSig, MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        int Next([In] uint celt, [Out, MarshalAs(UnmanagedType.LPArray)] ulong[] objects, [Out] out uint pceltFetched);
    }

    #endregion // ICorDebugValue

    #region Frames and Chains

    #region Chains
    public enum CorDebugChainReason
    {
        // Fields
        CHAIN_CLASS_INIT = 1,
        CHAIN_CONTEXT_POLICY = 8,
        CHAIN_CONTEXT_SWITCH = 0x400,
        CHAIN_DEBUGGER_EVAL = 0x200,
        CHAIN_ENTER_MANAGED = 0x80,
        CHAIN_ENTER_UNMANAGED = 0x100,
        CHAIN_EXCEPTION_FILTER = 2,
        CHAIN_FUNC_EVAL = 0x800,
        CHAIN_INTERCEPTION = 0x10,
        CHAIN_NONE = 0,
        CHAIN_PROCESS_START = 0x20,
        CHAIN_SECURITY = 4,
        CHAIN_THREAD_START = 0x40
    }

    [ComImport, Guid("CC7BCAEE-8A68-11D2-983C-0000F808342D"), InterfaceType(1)]
    public interface ICorDebugChain
    {

        void GetThread([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugThread ppThread);

        void GetStackRange([Out] out ulong pStart, [Out] out ulong pEnd);

        void GetContext([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugContext ppContext);

        void GetCaller([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugChain ppChain);

        void GetCallee([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugChain ppChain);

        void GetPrevious([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugChain ppChain);

        void GetNext([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugChain ppChain);

        void IsManaged([Out] out int pManaged);

        void EnumerateFrames([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugFrameEnum ppFrames);

        void GetActiveFrame([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugFrame ppFrame);

        void GetRegisterSet([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugRegisterSet ppRegisters);

        void GetReason([Out] out CorDebugChainReason pReason);
    }

    [ComImport, InterfaceType(1), ComConversionLoss, Guid("CC7BCB08-8A68-11D2-983C-0000F808342D")]
    public interface ICorDebugChainEnum : ICorDebugEnum
    {

        new void Skip([In] uint celt);

        new void Reset();

        new void Clone([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugEnum ppEnum);

        new void GetCount([Out] out uint pcelt);
        [PreserveSig, MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        int Next([In] uint celt, [Out, MarshalAs(UnmanagedType.LPArray)] ICorDebugChain[] chains, [Out] out uint pceltFetched);
    }
    #endregion // end Chains

    [ComImport, Guid("CC7BCAEF-8A68-11D2-983C-0000F808342D"), InterfaceType(1)]
    public interface ICorDebugFrame
    {

        void GetChain([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugChain ppChain);

        void GetCode([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugCode ppCode);

        void GetFunction([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugFunction ppFunction);

        void GetFunctionToken([Out] out uint pToken);

        void GetStackRange([Out] out ulong pStart, [Out] out ulong pEnd);

        void GetCaller([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugFrame ppFrame);

        void GetCallee([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugFrame ppFrame);

        void CreateStepper([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugStepper ppStepper);
    }

    [ComImport, ComConversionLoss, Guid("CC7BCB07-8A68-11D2-983C-0000F808342D"), InterfaceType(1)]
    public interface ICorDebugFrameEnum : ICorDebugEnum
    {

        new void Skip([In] uint celt);

        new void Reset();

        new void Clone([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugEnum ppEnum);

        new void GetCount([Out] out uint pcelt);
        [PreserveSig, MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        int Next([In] uint celt, [Out, MarshalAs(UnmanagedType.LPArray)] ICorDebugFrame[] frames, [Out] out uint pceltFetched);
    }


    [ComImport, InterfaceType(1), Guid("03E26314-4F76-11D3-88C6-006097945418")]
    public interface ICorDebugNativeFrame : ICorDebugFrame
    {

        new void GetChain([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugChain ppChain);

        new void GetCode([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugCode ppCode);

        new void GetFunction([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugFunction ppFunction);

        new void GetFunctionToken([Out] out uint pToken);

        new void GetStackRange([Out] out ulong pStart, [Out] out ulong pEnd);

        new void GetCaller([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugFrame ppFrame);

        new void GetCallee([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugFrame ppFrame);

        new void CreateStepper([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugStepper ppStepper);



        void GetIP([Out] out uint pnOffset);

        void SetIP([In] uint nOffset);

        void GetRegisterSet([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugRegisterSet ppRegisters);

        void GetLocalRegisterValue([In] CorDebugRegister reg, [In] uint cbSigBlob, [In, ComAliasName("Microsoft.Debugging.CorDebug.NativeApi.ULONG_PTR")] uint pvSigBlob, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppValue);

        void GetLocalDoubleRegisterValue([In] CorDebugRegister highWordReg, [In] CorDebugRegister lowWordReg, [In] uint cbSigBlob, [In, ComAliasName("Microsoft.Debugging.CorDebug.NativeApi.ULONG_PTR")] uint pvSigBlob, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppValue);

        void GetLocalMemoryValue([In] ulong address, [In] uint cbSigBlob, [In, ComAliasName("Microsoft.Debugging.CorDebug.NativeApi.ULONG_PTR")] uint pvSigBlob, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppValue);

        void GetLocalRegisterMemoryValue([In] CorDebugRegister highWordReg, [In] ulong lowWordAddress, [In] uint cbSigBlob, [In, ComAliasName("Microsoft.Debugging.CorDebug.NativeApi.ULONG_PTR")] uint pvSigBlob, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppValue);

        void GetLocalMemoryRegisterValue([In] ulong highWordAddress, [In] CorDebugRegister lowWordRegister, [In] uint cbSigBlob, [In, ComAliasName("Microsoft.Debugging.CorDebug.NativeApi.ULONG_PTR")] uint pvSigBlob, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppValue);
        [PreserveSig, MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        int CanSetIP([In] uint nOffset);
    }

    [ComImport, InterfaceType(1), Guid("35389FF1-3684-4c55-A2EE-210F26C60E5E")]
    public interface ICorDebugNativeFrame2
    {
        void IsChild([Out] out int pChild);

        void IsMatchingParentFrame([In, MarshalAs(UnmanagedType.Interface)] ICorDebugNativeFrame2 pFrame, [Out] out int pParent);

        void GetCalleeStackParameterSize([Out] out uint pSize);
    }

    public enum CorDebugMappingResult
    {
        // Fields
        MAPPING_APPROXIMATE = 0x20,
        MAPPING_EPILOG = 2,
        MAPPING_EXACT = 0x10,
        MAPPING_NO_INFO = 4,
        MAPPING_PROLOG = 1,
        MAPPING_UNMAPPED_ADDRESS = 8
    }


    [ComImport, InterfaceType(1), Guid("03E26311-4F76-11D3-88C6-006097945418")]
    public interface ICorDebugILFrame : ICorDebugFrame
    {

        new void GetChain([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugChain ppChain);

        new void GetCode([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugCode ppCode);

        new void GetFunction([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugFunction ppFunction);

        new void GetFunctionToken([Out] out uint pToken);

        new void GetStackRange([Out] out ulong pStart, [Out] out ulong pEnd);

        new void GetCaller([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugFrame ppFrame);

        new void GetCallee([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugFrame ppFrame);


        new void CreateStepper([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugStepper ppStepper);


        void GetIP([Out] out uint pnOffset, [Out] out CorDebugMappingResult pMappingResult);

        void SetIP([In] uint nOffset);

        void EnumerateLocalVariables([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValueEnum ppValueEnum);

        void GetLocalVariable([In] uint dwIndex, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppValue);

        void EnumerateArguments([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValueEnum ppValueEnum);

        void GetArgument([In] uint dwIndex, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppValue);

        void GetStackDepth([Out] out uint pDepth);

        void GetStackValue([In] uint dwIndex, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppValue);
        [PreserveSig, MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        int CanSetIP([In] uint nOffset);
    }

    [ComImport, InterfaceType(1), Guid("5D88A994-6C30-479B-890F-BCEF88B129A5")]
    public interface ICorDebugILFrame2
    {

        void RemapFunction([In] uint newILOffset);

        void EnumerateTypeParameters([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugTypeEnum ppTyParEnum);
    }

    public enum CorDebugInternalFrameType
    {
        STUBFRAME_NONE,
        STUBFRAME_M2U,
        STUBFRAME_U2M,
        STUBFRAME_APPDOMAIN_TRANSITION,
        STUBFRAME_LIGHTWEIGHT_FUNCTION,
        STUBFRAME_FUNC_EVAL,
        STUBFRAME_INTERNALCALL,
        STUBFRAME_CLASS_INIT,
        STUBFRAME_EXCEPTION,
        STUBFRAME_SECURITY,
        STUBFRAME_JIT_COMPILATION,
    }

    [ComImport, InterfaceType(1), Guid("B92CC7F7-9D2D-45C4-BC2B-621FCC9DFBF4")]
    public interface ICorDebugInternalFrame : ICorDebugFrame
    {

        new void GetChain([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugChain ppChain);

        new void GetCode([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugCode ppCode);

        new void GetFunction([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugFunction ppFunction);

        new void GetFunctionToken([Out] out uint pToken);

        new void GetStackRange([Out] out ulong pStart, [Out] out ulong pEnd);

        new void GetCaller([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugFrame ppFrame);

        new void GetCallee([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugFrame ppFrame);

        new void CreateStepper([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugStepper ppStepper);

        void GetFrameType([Out] out CorDebugInternalFrameType pType);
    }

    [ComImport, InterfaceType(1), Guid("C0815BDC-CFAB-447e-A779-C116B454EB5B")]
    public interface ICorDebugInternalFrame2
    {
        void GetAddress([Out] out ulong pAddress);

        void IsCloserToLeaf([In, MarshalAs(UnmanagedType.Interface)] ICorDebugFrame pFrameToCompare,
                            [Out] out int pIsCloser);
    }

    [ComImport, InterfaceType(1), Guid("879CAC0A-4A53-4668-B8E3-CB8473CB187F")]
    public interface ICorDebugRuntimeUnwindableFrame
    {
    }
    #endregion // Frames

    #region Callbacks

    // Unmanaged callback is only used for Interop-debugging to dispatch native debug events.
    [ComImport, Guid("5263E909-8CB5-11D3-BD2F-0000F80849BD"), InterfaceType(1)]
    public interface ICorDebugUnmanagedCallback
    {
        void DebugEvent([In] IntPtr pDebugEvent, [In] int fOutOfBand);
    }

    [ComImport, Guid("3D6F5F60-7538-11D3-8D5B-00104B35E7EF"), InterfaceType(1)]
    public interface ICorDebugManagedCallback
    {

        void Breakpoint([In, MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugBreakpoint pBreakpoint);


        void StepComplete([In, MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugStepper pStepper, [In] CorDebugStepReason reason);


        void Break([In, MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugThread thread);


        void Exception([In, MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread, [In] int unhandled);


        void EvalComplete([In, MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugEval pEval);


        void EvalException([In, MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugEval pEval);


        void CreateProcess([In, MarshalAs(UnmanagedType.Interface)] ICorDebugProcess pProcess);


        void ExitProcess([In, MarshalAs(UnmanagedType.Interface)] ICorDebugProcess pProcess);


        void CreateThread([In, MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugThread thread);


        void ExitThread([In, MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugThread thread);


        void LoadModule([In, MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugModule pModule);


        void UnloadModule([In, MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugModule pModule);


        void LoadClass([In, MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugClass c);


        void UnloadClass([In, MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugClass c);


        void DebuggerError([In, MarshalAs(UnmanagedType.Interface)] ICorDebugProcess pProcess, [In, MarshalAs(UnmanagedType.Error)] int errorHR, [In] uint errorCode);


        void LogMessage([In, MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread, [In] int lLevel, [In, MarshalAs(UnmanagedType.LPWStr)] string pLogSwitchName, [In, MarshalAs(UnmanagedType.LPWStr)] string pMessage);


        void LogSwitch([In, MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread, [In] int lLevel, [In] uint ulReason, [In, MarshalAs(UnmanagedType.LPWStr)] string pLogSwitchName, [In, MarshalAs(UnmanagedType.LPWStr)] string pParentName);


        void CreateAppDomain([In, MarshalAs(UnmanagedType.Interface)] ICorDebugProcess pProcess, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain);


        void ExitAppDomain([In, MarshalAs(UnmanagedType.Interface)] ICorDebugProcess pProcess, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain);


        void LoadAssembly([In, MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugAssembly pAssembly);


        void UnloadAssembly([In, MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugAssembly pAssembly);


        void ControlCTrap([In, MarshalAs(UnmanagedType.Interface)] ICorDebugProcess pProcess);


        void NameChange([In, MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread);


        void UpdateModuleSymbols([In, MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugModule pModule, [In, MarshalAs(UnmanagedType.Interface)] IStream pSymbolStream);


        void EditAndContinueRemap([In, MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugFunction pFunction, [In] int fAccurate);


        void BreakpointSetError([In, MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugBreakpoint pBreakpoint, [In] uint dwError);
    }

    [ComImport, Guid("250E5EEA-DB5C-4C76-B6F3-8C46F12E3203"), InterfaceType(1)]
    public interface ICorDebugManagedCallback2
    {

        void FunctionRemapOpportunity([In, MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugFunction pOldFunction, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugFunction pNewFunction, [In] uint oldILOffset);


        void CreateConnection([In, MarshalAs(UnmanagedType.Interface)] ICorDebugProcess pProcess, [In] uint dwConnectionId, [In] ref ushort pConnName);


        void ChangeConnection([In, MarshalAs(UnmanagedType.Interface)] ICorDebugProcess pProcess, [In] uint dwConnectionId);


        void DestroyConnection([In, MarshalAs(UnmanagedType.Interface)] ICorDebugProcess pProcess, [In] uint dwConnectionId);


        void Exception([In, MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugFrame pFrame, [In] uint nOffset, [In] CorDebugExceptionCallbackType dwEventType, [In] uint dwFlags);


        void ExceptionUnwind([In, MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread, [In] CorDebugExceptionUnwindCallbackType dwEventType, [In] uint dwFlags);


        void FunctionRemapComplete([In, MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugFunction pFunction);


        void MDANotification([In, MarshalAs(UnmanagedType.Interface)] ICorDebugController pController, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread, [In, MarshalAs(UnmanagedType.Interface)] ICorDebugMDA pMDA);
    }

    [ComImport, Guid("264EA0FC-2591-49AA-868E-835E6515323F"), InterfaceType(1)]
    public interface ICorDebugManagedCallback3
    {
        void CustomNotification([In, MarshalAs(UnmanagedType.Interface)] ICorDebugThread pThread,
                                [In, MarshalAs(UnmanagedType.Interface)] ICorDebugAppDomain pAppDomain);
    }


    #endregion // Callbacks

    #region Module
    [ComImport, ComConversionLoss, InterfaceType(1), Guid("DBA2D8C1-E5C5-4069-8C13-10A7C6ABF43D")]
    public interface ICorDebugModule
    {

        void GetProcess([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugProcess ppProcess);

        void GetBaseAddress([Out] out ulong pAddress);

        void GetAssembly([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugAssembly ppAssembly);

        void GetName([In] uint cchName, [Out] out uint pcchName, [MarshalAs(UnmanagedType.LPArray)] char[] szName);

        void EnableJITDebugging([In] int bTrackJITInfo, [In] int bAllowJitOpts);

        void EnableClassLoadCallbacks([In] int bClassLoadCallbacks);

        void GetFunctionFromToken([In] uint methodDef, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugFunction ppFunction);

        void GetFunctionFromRVA([In] ulong rva, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugFunction ppFunction);

        void GetClassFromToken([In] uint typeDef, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugClass ppClass);

        void CreateBreakpoint([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugModuleBreakpoint ppBreakpoint);

        void GetEditAndContinueSnapshot([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugEditAndContinueSnapshot ppEditAndContinueSnapshot);


        // <strip>TODO: We may want to just return an IUnknown here, but then the fake-com wrappers will need some way of knowing how to wrap it</strip>

        void GetMetaDataInterface([In, ComAliasName("REFIID")] ref Guid riid, [Out, MarshalAs(UnmanagedType.Interface)] out IMetadataImport ppObj);


        void GetToken([Out] out uint pToken);

        void IsDynamic([Out] out int pDynamic);

        void GetGlobalVariableValue([In] uint fieldDef, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppValue);

        void GetSize([Out] out uint pcBytes);

        void IsInMemory([Out] out int pInMemory);
    }

    [ComImport, InterfaceType(1), Guid("7FCC5FB5-49C0-41DE-9938-3B88B5B9ADD7")]
    public interface ICorDebugModule2
    {

        void SetJMCStatus([In] int bIsJustMyCode, [In] uint cTokens, [In] ref uint pTokens);

        void ApplyChanges([In] uint cbMetadata, [In, MarshalAs(UnmanagedType.LPArray)] byte[] pbMetadata, [In] uint cbIL, [In, MarshalAs(UnmanagedType.LPArray)] byte[] pbIL);
        [PreserveSig, MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        int SetJITCompilerFlags([In] uint dwFlags);

        void GetJITCompilerFlags([Out] out uint pdwFlags);

        void ResolveAssembly([In] uint tkAssemblyRef, [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugAssembly ppAssembly);
    }

    [ComImport, InterfaceType(1), Guid("86F012BF-FF15-4372-BD30-B6F11CAAE1DD")]
    public interface ICorDebugModule3
    {
        void CreateReaderForInMemorySymbols([In, ComAliasName("REFIID")] ref Guid riid, [Out, MarshalAs(UnmanagedType.Interface)] out Object ppObj);
    }

    [ComImport, Guid("CC7BCAEA-8A68-11D2-983C-0000F808342D"), InterfaceType(1)]
    public interface ICorDebugModuleBreakpoint : ICorDebugBreakpoint
    {

        new void Activate([In] int bActive);

        new void IsActive([Out] out int pbActive);


        void GetModule([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugModule ppModule);
    }

    [ComImport, InterfaceType(1), ComConversionLoss, Guid("CC7BCB09-8A68-11D2-983C-0000F808342D")]
    public interface ICorDebugModuleEnum : ICorDebugEnum
    {

        new void Skip([In] uint celt);

        new void Reset();

        new void Clone([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugEnum ppEnum);

        new void GetCount([Out] out uint pcelt);
        [PreserveSig, MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        int Next([In] uint celt, [Out, MarshalAs(UnmanagedType.LPArray)] ICorDebugModule[] modules, [Out] out uint pceltFetched);
    }


    #endregion Module

    #region MDA
    [Flags]
    public enum CorDebugMDAFlags
    {
        // Fields
        None = 0,
        MDA_FLAG_SLIP = 2
    }

    [ComImport, Guid("CC726F2F-1DB7-459B-B0EC-05F01D841B42"), InterfaceType(1)]
    public interface ICorDebugMDA
    {

        void GetName([In] uint cchName, [Out] out uint pcchName, [MarshalAs(UnmanagedType.LPArray)] char[] szName);


        void GetDescription([In] uint cchName, [Out] out uint pcchName, [MarshalAs(UnmanagedType.LPArray)] char[] szName);


        void GetXML([In] uint cchName, [Out] out uint pcchName, [MarshalAs(UnmanagedType.LPArray)] char[] szName);


        void GetFlags([Out] out CorDebugMDAFlags pFlags);


        void GetOSThreadId([Out] out uint pOsTid);
    }
    #endregion // MDA

    [ComImport, Guid("CC7BCB0B-8A68-11D2-983C-0000F808342D"), ComConversionLoss, InterfaceType(1)]
    public interface ICorDebugRegisterSet
    {
        void GetRegistersAvailable([Out] out ulong pAvailable);


        void GetRegisters([In] ulong mask, [In] uint regCount, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] ulong[] regBuffer);


        void SetRegisters([In] ulong mask, [In] uint regCount, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] ulong[] regBuffer);


        void GetThreadContext([In] uint contextSize, [In, ComAliasName("BYTE*")] IntPtr context);


        void SetThreadContext([In] uint contextSize, [In, ComAliasName("BYTE*")] IntPtr context);
    }

    #region Threads
    [ComImport, Guid("938C6D66-7FB6-4F69-B389-425B8987329B"), InterfaceType(1)]
    public interface ICorDebugThread
    {

        void GetProcess([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugProcess ppProcess);

        void GetID([Out] out uint pdwThreadId);

        void GetHandle([Out] out IntPtr phThreadHandle);

        void GetAppDomain([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugAppDomain ppAppDomain);

        void SetDebugState([In] CorDebugThreadState state);

        void GetDebugState([Out] out CorDebugThreadState pState);

        void GetUserState([Out] out CorDebugUserState pState);

        void GetCurrentException([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppExceptionObject);

        void ClearCurrentException();

        void CreateStepper([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugStepper ppStepper);

        void EnumerateChains([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugChainEnum ppChains);

        void GetActiveChain([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugChain ppChain);

        void GetActiveFrame([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugFrame ppFrame);

        void GetRegisterSet([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugRegisterSet ppRegisters);

        void CreateEval([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugEval ppEval);

        void GetObject([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppObject);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct COR_ACTIVE_FUNCTION
    {
        public ICorDebugAppDomain pAppDomain;
        public ICorDebugModule pModule;
        public ICorDebugFunction2 pFunction;
        public uint ilOffset;
        public uint Flags;
    }


    [ComImport, Guid("2BD956D9-7B07-4BEF-8A98-12AA862417C5"), ComConversionLoss, InterfaceType(1)]
    public interface ICorDebugThread2
    {

        void GetActiveFunctions([In] uint cFunctions, [Out] out uint pcFunctions, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] COR_ACTIVE_FUNCTION[] pFunctions);

        void GetConnectionID([Out] out uint pdwConnectionId);

        void GetTaskID([Out] out ulong pTaskId);

        void GetVolatileOSThreadID([Out] out uint pdwTid);

        void InterceptCurrentException([In, MarshalAs(UnmanagedType.Interface)] ICorDebugFrame pFrame);
    }

    [ComImport, Guid("1A1F204B-1C66-4637-823F-3EE6C744A69C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ICorDebugThread4
    {
        [PreserveSig]
        int HasUnhandledException();

        void GetBlockingObjects(
            [Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugEnumBlockingObject blockingObjectEnumerator);

        void GetCurrentCustomDebuggerNotification([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugValue ppNotificationObject);
    }

    [ComImport, Guid("F8544EC3-5E4E-46c7-8D3E-A52B8405B1F5"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ICorDebugThread3
    {
        void CreateStackWalk([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugStackWalk ppStackWalk);

        void GetActiveInternalFrames([In] uint cInternalFrames,
            [Out] out uint pcInternalFrames,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] ICorDebugInternalFrame2[] ppFunctions);
    }

    public enum CorDebugSetContextFlag
    {
        // Fields
        SET_CONTEXT_FLAG_ACTIVE_FRAME = 0x1,
        SET_CONTEXT_FLAG_UNWIND_FRAME = 0x2
    }

    [ComImport, Guid("A0647DE9-55DE-4816-929C-385271C64CF7"), InterfaceType(1)]
    public interface ICorDebugStackWalk
    {
        void GetContext([In] uint contextFlags,
            [In] uint contextBufferSize,
            [Out] out uint contextSize,
            [In] IntPtr contextBuffer);

        void SetContext([In] CorDebugSetContextFlag flag,
            [In] uint contextSize,
            [In] IntPtr contextBuffer);

        [PreserveSig]
        int Next();

        void GetFrame([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugFrame ppFrame);
    }

    public enum CorDebugBlockingReason
    {
        None = 0,
        MonitorCriticalSection = 1,
        MonitorEvent = 2
    }

    public struct CorDebugBlockingObject
    {
        public ICorDebugValue BlockingObject;
        public uint Timeout;
        public CorDebugBlockingReason BlockingReason;
    }

    [ComImport, Guid("976A6278-134A-4a81-81A3-8F277943F4C3"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface ICorDebugEnumBlockingObject : ICorDebugEnum
    {
        new void Skip([In] uint countElements);
        new void Reset();
        new void Clone([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugEnum enumerator);
        new void GetCount([Out] out uint countElements);
        [PreserveSig]
        int Next([In] uint countElements,
                 [Out, MarshalAs(UnmanagedType.LPArray)] CorDebugBlockingObject[] blockingObjects,
                 [Out] out uint countElementsFetched);
    }

    [ComImport, InterfaceType(1), ComConversionLoss, Guid("CC7BCB06-8A68-11D2-983C-0000F808342D")]
    public interface ICorDebugThreadEnum : ICorDebugEnum
    {

        new void Skip([In] uint celt);

        new void Reset();

        new void Clone([Out, MarshalAs(UnmanagedType.Interface)] out ICorDebugEnum ppEnum);

        new void GetCount([Out] out uint pcelt);
        [PreserveSig, MethodImpl(MethodImplOptions.InternalCall, MethodCodeType = MethodCodeType.Runtime)]
        int Next([In] uint celt, [Out, MarshalAs(UnmanagedType.LPArray)] ICorDebugThread[] threads, [Out] out uint pceltFetched);
    }

    #endregion Threads

    /// <summary>
    /// Constants to return from GetPlatform 
    /// </summary>
    public enum CorDebugPlatform : int
    {
        CORDB_PLATFORM_WINDOWS_X86 = 0,       // Windows on Intel x86
        CORDB_PLATFORM_WINDOWS_AMD64 = 1,     // Windows x64 (Amd64, Intel EM64T)
        CORDB_PLATFORM_WINDOWS_IA64 = 2,      // Windows on Intel IA-64
        CORDB_PLATFORM_MAC_PPC = 3,           // MacOS on PowerPC
        CORDB_PLATFORM_MAC_X86 = 4,           // MacOS on Intel x86
        CORDB_PLATFORM_WINDOWS_ARM = 5        // Windows on ARM
    }

    [ComImport, InterfaceType(1), Guid("FE06DC28-49FB-4636-A4A3-E80DB4AE116C")]
    public interface ICorDebugDataTarget
    {
        CorDebugPlatform GetPlatform();

        uint ReadVirtual(System.UInt64 address,
                         IntPtr buffer,
                         uint bytesRequested);

        void GetThreadContext(uint threadId,
                                 uint contextFlags,
                                 uint contextSize,
                                 IntPtr context);
    }
}


