    //---------------------------------------------------------------------
//  This file is part of the CLR Managed Debugger (mdbg) Sample.
// 
//  Copyright (C) Microsoft Corporation.  All rights reserved.
//---------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Microsoft.Samples.Debugging.CorDebug.NativeApi;
using Microsoft.Samples.Debugging.Native;
using System.Security.Permissions;

namespace Profiler
{
    public class Debugger
    {
        public static ICorDebug GetDebuggerForProcess(int processID, string minimumVersion, DebuggerCallBacks callBacks = null)
        {
            CLRMetaHost mh = new CLRMetaHost();
            CLRRuntimeInfo highestLoadedRuntime = null;
            foreach (var runtime in mh.EnumerateLoadedRuntimes(processID))
            {
                if (highestLoadedRuntime == null ||
                    string.Compare(highestLoadedRuntime.GetVersionString(), runtime.GetVersionString(), StringComparison.OrdinalIgnoreCase) < 0)
                    highestLoadedRuntime = runtime;
            }
            if (highestLoadedRuntime == null)
                throw new ApplicationException("Could not enumerate .NET runtimes on the system.");

            var runtimeVersion = highestLoadedRuntime.GetVersionString();
            if (string.Compare(runtimeVersion, minimumVersion, StringComparison.OrdinalIgnoreCase) < 0)
                throw new ApplicationException("Runtime in process " + runtimeVersion + " below the minimum of " + minimumVersion);

            ICorDebug rawDebuggingAPI = highestLoadedRuntime.GetLegacyICorDebugInterface();
            if (rawDebuggingAPI == null)
                throw new ArgumentException("Cannot be null.", "rawDebugggingAPI");
            rawDebuggingAPI.Initialize();
            if (callBacks == null)
                callBacks = new DebuggerCallBacks();
            rawDebuggingAPI.SetManagedHandler(callBacks);
            return rawDebuggingAPI;
        }
    }

    public class DebuggerCallBacks : ICorDebugManagedCallback3, ICorDebugManagedCallback2, ICorDebugManagedCallback
    {
        public virtual void CustomNotification(ICorDebugThread pThread, ICorDebugAppDomain pAppDomain) { pAppDomain.Continue(0); }
        public virtual void FunctionRemapOpportunity(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, ICorDebugFunction pOldFunction, ICorDebugFunction pNewFunction, uint oldILOffset) { pAppDomain.Continue(0); }
        public virtual void CreateConnection(ICorDebugProcess pProcess, uint dwConnectionId, ref ushort pConnName) { pProcess.Continue(0); }
        public virtual void ChangeConnection(ICorDebugProcess pProcess, uint dwConnectionId) { pProcess.Continue(0); }
        public virtual void DestroyConnection(ICorDebugProcess pProcess, uint dwConnectionId) { pProcess.Continue(0); }
        public virtual void Exception(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, ICorDebugFrame pFrame, uint nOffset, CorDebugExceptionCallbackType dwEventType, uint dwFlags) { pAppDomain.Continue(0); }
        public virtual void ExceptionUnwind(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, CorDebugExceptionUnwindCallbackType dwEventType, uint dwFlags) { pAppDomain.Continue(0); }
        public virtual void FunctionRemapComplete(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, ICorDebugFunction pFunction) { pAppDomain.Continue(0); }
        public virtual void MDANotification(ICorDebugController pController, ICorDebugThread pThread, ICorDebugMDA pMDA) { pController.Continue(0); }
        public virtual void Breakpoint(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, ICorDebugBreakpoint pBreakpoint) { pAppDomain.Continue(0); }
        public virtual void StepComplete(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, ICorDebugStepper pStepper, CorDebugStepReason reason) { pAppDomain.Continue(0); }
        public virtual void Break(ICorDebugAppDomain pAppDomain, ICorDebugThread thread) { pAppDomain.Continue(0); }
        public virtual void Exception(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, int unhandled) { pAppDomain.Continue(0); }
        public virtual void EvalComplete(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, ICorDebugEval pEval) { pAppDomain.Continue(0); }
        public virtual void EvalException(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, ICorDebugEval pEval) { pAppDomain.Continue(0); }
        public virtual void CreateProcess(ICorDebugProcess pProcess) { pProcess.Continue(0); }
        public virtual void ExitProcess(ICorDebugProcess pProcess) { pProcess.Continue(0); }
        public virtual void CreateThread(ICorDebugAppDomain pAppDomain, ICorDebugThread thread) { pAppDomain.Continue(0); }
        public virtual void ExitThread(ICorDebugAppDomain pAppDomain, ICorDebugThread thread) { pAppDomain.Continue(0); }
        public virtual void LoadModule(ICorDebugAppDomain pAppDomain, ICorDebugModule pModule) { pAppDomain.Continue(0); }
        public virtual void UnloadModule(ICorDebugAppDomain pAppDomain, ICorDebugModule pModule) { pAppDomain.Continue(0); }
        public virtual void LoadClass(ICorDebugAppDomain pAppDomain, ICorDebugClass c) { pAppDomain.Continue(0); }
        public virtual void UnloadClass(ICorDebugAppDomain pAppDomain, ICorDebugClass c) { pAppDomain.Continue(0); }
        public virtual void DebuggerError(ICorDebugProcess pProcess, int errorHR, uint errorCode) { pProcess.Continue(0); }
        public virtual void LogMessage(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, int lLevel, string pLogSwitchName, string pMessage) { pAppDomain.Continue(0); }
        public virtual void LogSwitch(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, int lLevel, uint ulReason, string pLogSwitchName, string pParentName) { pAppDomain.Continue(0); }
        public virtual void CreateAppDomain(ICorDebugProcess pProcess, ICorDebugAppDomain pAppDomain) { pAppDomain.Continue(0); }
        public virtual void ExitAppDomain(ICorDebugProcess pProcess, ICorDebugAppDomain pAppDomain) { pAppDomain.Continue(0); }
        public virtual void LoadAssembly(ICorDebugAppDomain pAppDomain, ICorDebugAssembly pAssembly) { pAppDomain.Continue(0); }
        public virtual void UnloadAssembly(ICorDebugAppDomain pAppDomain, ICorDebugAssembly pAssembly) { pAppDomain.Continue(0); }
        public virtual void ControlCTrap(ICorDebugProcess pProcess) { pProcess.Continue(0); }
        public virtual void NameChange(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread) { pAppDomain.Continue(0); }
        public virtual void UpdateModuleSymbols(ICorDebugAppDomain pAppDomain, ICorDebugModule pModule, IStream pSymbolStream) { pAppDomain.Continue(0); }
        public virtual void EditAndContinueRemap(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, ICorDebugFunction pFunction, int fAccurate) { pAppDomain.Continue(0); }
        public virtual void BreakpointSetError(ICorDebugAppDomain pAppDomain, ICorDebugThread pThread, ICorDebugBreakpoint pBreakpoint, uint dwError) { pAppDomain.Continue(0); }
    }

    #region internal classes 
    public class ProcessSafeHandle : Microsoft.Win32.SafeHandles.SafeHandleZeroOrMinusOneIsInvalid
    {
        private ProcessSafeHandle()
            : base(true)
        {
        }

        private ProcessSafeHandle(IntPtr handle, bool ownsHandle)
            : base(ownsHandle)
        {
            SetHandle(handle);
        }

        [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
        override protected bool ReleaseHandle()
        {
            return NativeMethods.CloseHandle(handle);
        }
    }

    public static class NativeMethods
    {
        private const string Kernel32LibraryName = "kernel32.dll";
        private const string Ole32LibraryName = "ole32.dll";
        private const string ShlwapiLibraryName = "shlwapi.dll";
        private const string ShimLibraryName = "mscoree.dll";

        public const int MAX_PATH = 260;


        [
         System.Runtime.ConstrainedExecution.ReliabilityContract(System.Runtime.ConstrainedExecution.Consistency.WillNotCorruptState, System.Runtime.ConstrainedExecution.Cer.Success),
         DllImport(Kernel32LibraryName)
        ]
        public static extern bool CloseHandle(IntPtr handle);


        [
         DllImport(ShimLibraryName, CharSet = CharSet.Unicode, PreserveSig = false)
        ]
        public static extern ICorDebug CreateDebuggingInterfaceFromVersion(int iDebuggerVersion
                                                                           , string szDebuggeeVersion);

        [
         DllImport(ShimLibraryName, CharSet = CharSet.Unicode, PreserveSig = false)
        ]
        public static extern void GetVersionFromProcess(ProcessSafeHandle hProcess, StringBuilder versionString,
                                                        Int32 bufferSize, out Int32 dwLength);

        [
         DllImport(ShimLibraryName, CharSet = CharSet.Unicode, PreserveSig = false)
        ]
        public static extern void GetRequestedRuntimeVersion(string pExe, StringBuilder pVersion,
                                                             Int32 cchBuffer, out Int32 dwLength);

        [
         DllImport(ShimLibraryName, CharSet = CharSet.Unicode, PreserveSig = false)
        ]
        public static extern void CLRCreateInstance(ref Guid clsid, ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)]out object metahostInterface);

        public enum ProcessAccessOptions : int
        {
            ProcessTerminate = 0x0001,
            ProcessCreateThread = 0x0002,
            ProcessSetSessionID = 0x0004,
            ProcessVMOperation = 0x0008,
            ProcessVMRead = 0x0010,
            ProcessVMWrite = 0x0020,
            ProcessDupHandle = 0x0040,
            ProcessCreateProcess = 0x0080,
            ProcessSetQuota = 0x0100,
            ProcessSetInformation = 0x0200,
            ProcessQueryInformation = 0x0400,
            ProcessSuspendResume = 0x0800,
            Synchronize = 0x100000,
        }

        [
         DllImport(Kernel32LibraryName, PreserveSig = true)
        ]
        public static extern ProcessSafeHandle OpenProcess(Int32 dwDesiredAccess, bool bInheritHandle, Int32 dwProcessId);
#if false 

        [
         DllImport(Kernel32LibraryName, CharSet = CharSet.Unicode, PreserveSig = true)
        ]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool QueryFullProcessImageName(ProcessSafeHandle hProcess,
                                                            int dwFlags,
                                                            StringBuilder lpExeName,
                                                            ref int lpdwSize);

        [
         DllImport(Ole32LibraryName, PreserveSig = false)
        ]
        public static extern void CoCreateInstance(ref Guid rclsid, IntPtr pUnkOuter,
                                                   Int32 dwClsContext,
                                                   ref Guid riid, // must use "typeof(ICorDebug).GUID"
                                                   [MarshalAs(UnmanagedType.Interface)]out ICorDebug debuggingInterface
                                                   );

        public enum Stgm
        {
            StgmRead = 0x00000000,
            StgmWrite = 0x00000001,
            StgmReadWrite = 0x00000002,
            StgmShareDenyNone = 0x00000040,
            StgmShareDenyRead = 0x00000030,
            StgmShareDenyWrite = 0x00000020,
            StgmShareExclusive = 0x00000010,
            StgmPriority = 0x00040000,
            StgmCreate = 0x00001000,
            StgmConvert = 0x00020000,
            StgmFailIfThere = 0x00000000,
            StgmDirect = 0x00000000,
            StgmTransacted = 0x00010000,
            StgmNoScratch = 0x00100000,
            StgmNoSnapshot = 0x00200000,
            StgmSimple = 0x08000000,
            StgmDirectSwmr = 0x00400000,
            StgmDeleteOnRelease = 0x04000000
        }

        // SHCreateStreamOnFile* is used to create IStreams to pass to ICLRMetaHostPolicy::GetRequestedRuntime().
        // Since we can't count on the EX version being available, we have SHCreateStreamOnFile as a fallback.
        [
         DllImport(ShlwapiLibraryName, PreserveSig = false)
        ]
        // Only in version 6 and later
        public static extern void SHCreateStreamOnFileEx([MarshalAs(UnmanagedType.LPWStr)]string file,
                                                        Stgm dwMode,
                                                        Int32 dwAttributes, // Used if a file is created.  Identical to dwFlagsAndAttributes param of CreateFile.
                                                        bool create,
                                                        IntPtr pTemplate,   // Reserved, always pass null.
                                                        [MarshalAs(UnmanagedType.Interface)]out IStream openedStream);

        [
         DllImport(ShlwapiLibraryName, PreserveSig = false)
        ]
        public static extern void SHCreateStreamOnFile(string file,
                                                        Stgm dwMode,
                                                        [MarshalAs(UnmanagedType.Interface)]out IStream openedStream);

#endif
    }

    // Wrapper for ICLRMetaHost.  Used to find information about runtimes.
    public sealed class CLRMetaHost
    {
        private ICLRMetaHost m_metaHost;

        public const int MaxVersionStringLength = 26; // 24 + NULL and an extra
        private static readonly Guid clsidCLRMetaHost = new Guid("9280188D-0E8E-4867-B30C-7FA83884E8DE");

        public CLRMetaHost()
        {
            object o;
            Guid ifaceId = typeof(ICLRMetaHost).GUID;
            Guid clsid = clsidCLRMetaHost;
            NativeMethods.CLRCreateInstance(ref clsid, ref ifaceId, out o);
            m_metaHost = (ICLRMetaHost)o;
        }

        public CLRRuntimeInfo GetInstalledRuntimeByVersion(string version)
        {
            IEnumerable<CLRRuntimeInfo> runtimes = EnumerateInstalledRuntimes();

            foreach (CLRRuntimeInfo rti in runtimes)
            {
                if (rti.GetVersionString().ToString().ToLower() == version.ToLower())
                {
                    return rti;
                }
            }

            return null;
        }

        public CLRRuntimeInfo GetLoadedRuntimeByVersion(Int32 processId, string version)
        {
            IEnumerable<CLRRuntimeInfo> runtimes = EnumerateLoadedRuntimes(processId);

            foreach (CLRRuntimeInfo rti in runtimes)
            {
                if (rti.GetVersionString().Equals(version, StringComparison.OrdinalIgnoreCase))
                {
                    return rti;
                }
            }

            return null;
        }

        // Retrieve information about runtimes installed on the machine (i.e. in %WINDIR%\Microsoft.NET\)
        public IEnumerable<CLRRuntimeInfo> EnumerateInstalledRuntimes()
        {
            List<CLRRuntimeInfo> runtimes = new List<CLRRuntimeInfo>();
            IEnumUnknown enumRuntimes = m_metaHost.EnumerateInstalledRuntimes();

            // Since we're only getting one at a time, we can pass NULL for count.
            // S_OK also means we got the single element we asked for.
            for (object oIUnknown; enumRuntimes.Next(1, out oIUnknown, IntPtr.Zero) == 0; /* empty */)
            {
                runtimes.Add(new CLRRuntimeInfo(oIUnknown));
            }

            return runtimes;
        }

        // Retrieve information about runtimes that are currently loaded into the target process.
        public IEnumerable<CLRRuntimeInfo> EnumerateLoadedRuntimes(Int32 processId)
        {
            List<CLRRuntimeInfo> runtimes = new List<CLRRuntimeInfo>();
            IEnumUnknown enumRuntimes;

            using (ProcessSafeHandle hProcess = NativeMethods.OpenProcess(
                /*
                (int)(NativeMethods.ProcessAccessOptions.ProcessVMRead |
                                                                        NativeMethods.ProcessAccessOptions.ProcessQueryInformation |
                                                                        NativeMethods.ProcessAccessOptions.ProcessDupHandle |
                                                                        NativeMethods.ProcessAccessOptions.Synchronize),
                 */ 
                // TODO FIX NOW for debugging. 
                0x1FFFFF, // PROCESS_ALL_ACCESS
                                                                        false, // inherit handle
                                                                        processId))
            {
                if (hProcess.IsInvalid)
                {
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                }

                enumRuntimes = m_metaHost.EnumerateLoadedRuntimes(hProcess);
            }

            // Since we're only getting one at a time, we can pass NULL for count.
            // S_OK also means we got the single element we asked for.
            for (object oIUnknown; enumRuntimes.Next(1, out oIUnknown, IntPtr.Zero) == 0; /* empty */)
            {
                runtimes.Add(new CLRRuntimeInfo(oIUnknown));
            }

            return runtimes;
        }

        public CLRRuntimeInfo GetRuntime(string version)
        {
            Guid ifaceId = typeof(ICLRRuntimeInfo).GUID;
            return new CLRRuntimeInfo(m_metaHost.GetRuntime(version, ref ifaceId));
        }
    }

    // You're expected to get this interface from mscoree!GetCLRMetaHost.
    // Details for APIs are in metahost.idl.
    [ComImport, InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown), Guid("D332DB9E-B9B3-4125-8207-A14884F53216")]
    internal interface ICLRMetaHost
    {
        [return: MarshalAs(UnmanagedType.Interface)]
        System.Object GetRuntime(
            [In, MarshalAs(UnmanagedType.LPWStr)] string pwzVersion,
            [In] ref Guid riid /*must use typeof(ICLRRuntimeInfo).GUID*/);

        void GetVersionFromFile(
            [In, MarshalAs(UnmanagedType.LPWStr)] string pwzFilePath,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwzBuffer,
            [In, Out] ref uint pcchBuffer);

        [return: MarshalAs(UnmanagedType.Interface)]
        IEnumUnknown EnumerateInstalledRuntimes();

        [return: MarshalAs(UnmanagedType.Interface)]
        IEnumUnknown EnumerateLoadedRuntimes(
            [In] ProcessSafeHandle hndProcess);
    }

    // Wrapper for ICLRRuntimeInfo.  Represents information about a CLR install instance.
    public sealed class CLRRuntimeInfo
    {
        public CLRRuntimeInfo(System.Object clrRuntimeInfo)
        {
            m_runtimeInfo = (ICLRRuntimeInfo)clrRuntimeInfo;
        }

        public string GetVersionString()
        {
            StringBuilder sb = new StringBuilder(CLRMetaHost.MaxVersionStringLength);
            int verStrLength = sb.Capacity;
            m_runtimeInfo.GetVersionString(sb, ref verStrLength);
            return sb.ToString();
        }
        public string GetRuntimeDirectory()
        {
            StringBuilder sb = new StringBuilder();
            int strLength = 0;
            m_runtimeInfo.GetRuntimeDirectory(sb, ref strLength);
            sb.Capacity = strLength;
            int ret = m_runtimeInfo.GetRuntimeDirectory(sb, ref strLength);
            if (ret < 0)
                Marshal.ThrowExceptionForHR(ret);
            return sb.ToString();
        }
        public ICorDebug GetLegacyICorDebugInterface()
        {
            Guid ifaceId = typeof(ICorDebug).GUID;
            Guid clsId = s_ClsIdClrDebuggingLegacy;
            return (ICorDebug)m_runtimeInfo.GetInterface(ref clsId, ref ifaceId);
        }

        public ICLRProfiling GetProfilingInterface()
        {
            Guid ifaceId = typeof(ICLRProfiling).GUID;
            Guid clsId = s_ClsIdClrProfiler;
            return (ICLRProfiling)m_runtimeInfo.GetInterface(ref clsId, ref ifaceId);
        }
        public IMetaDataDispenser GetIMetaDataDispenser()
        {
            Guid ifaceId = typeof(ICLRProfiling).GUID;
            Guid clsId = s_ClsIdClrProfiler;
            return (IMetaDataDispenser)m_runtimeInfo.GetInterface(ref clsId, ref ifaceId);
        }

        #region private 
        private static Guid s_ClsIdClrDebuggingLegacy = new Guid("DF8395B5-A4BA-450b-A77C-A9A47762C520");
        private static Guid s_ClsIdClrProfiler = new Guid("BD097ED8-733E-43FE-8ED7-A95FF9A8448C");
        private static Guid s_CorMetaDataDispenser = new Guid("E5CB7A31-7512-11d2-89CE-0080C792E5D8");

        private ICLRRuntimeInfo m_runtimeInfo;
        #endregion 
    }

     [ComImport, Guid("809C652E-7396-11D2-9771-00A0C9B4D50C"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
     public interface IMetaDataDispenser {
     /// <summary>
     /// Creates a new area in memory in which you can create new metadata.
     /// </summary>
     /// <param name="rclsid">[in] The CLSID of the version of metadata structures to be created. This value must be CLSID_CorMetaDataRuntime.</param>
     /// <param name="dwCreateFlags">[in] Flags that specify options. This value must be zero.</param>
     /// <param name="riid">
     /// [in] The IID of the desired metadata interface to be returned; the caller will use the interface to create the new metadata.
     /// The value of riid must specify one of the "emit" interfaces. Valid values are IID_IMetaDataEmit, IID_IMetaDataAssemblyEmit, or IID_IMetaDataEmit2. 
     /// </param>
     /// <param name="ppIUnk">[out] The pointer to the returned interface.</param>
     /// <remarks>
     /// STDMETHOD(DefineScope)(         // Return code.
     ///     REFCLSID    rclsid,         // [in] What version to create.
     ///     DWORD       dwCreateFlags,      // [in] Flags on the create.
     ///     REFIID      riid,           // [in] The interface desired.
     ///     IUnknown    **ppIUnk) PURE;     // [out] Return interface on success.
     /// </remarks>
     [PreserveSig]
     void DefineScope(
         [In] ref Guid rclsid, 
         [In] uint dwCreateFlags, 
         [In] ref Guid riid, 
         [Out, MarshalAs(UnmanagedType.Interface)] out object ppIUnk);

     /// <summary>
     /// Opens an existing, on-disk file and maps its metadata into memory.
     /// </summary>
     /// <param name="szScope">[in] The name of the file to be opened. The file must contain common language runtime (CLR) metadata.</param>
     /// <param name="dwOpenFlags">[in] A value of the <c>CorOpenFlags</c> enumeration to specify the mode (read, write, and so on) for opening. </param>
     /// <param name="riid">
     /// [in] The IID of the desired metadata interface to be returned; the caller will use the interface to import (read) or emit (write) metadata. 
     /// The value of riid must specify one of the "import" or "emit" interfaces. Valid values are IID_IMetaDataEmit, IID_IMetaDataImport, IID_IMetaDataAssemblyEmit, IID_IMetaDataAssemblyImport, IID_IMetaDataEmit2, or IID_IMetaDataImport2. 
     /// </param>
     /// <param name="ppIUnk">[out] The pointer to the returned interface.</param>
     /// <remarks>
     /// STDMETHOD(OpenScope)(           // Return code.
     ///     LPCWSTR     szScope,        // [in] The scope to open.
     ///     DWORD       dwOpenFlags,        // [in] Open mode flags.
     ///     REFIID      riid,           // [in] The interface desired.
     ///     IUnknown    **ppIUnk) PURE;     // [out] Return interface on success.
     /// </remarks>
     [PreserveSig]
     void OpenScope(
         [In, MarshalAs(UnmanagedType.LPWStr)] string szScope,
         [In] int dwOpenFlags,
         [In] ref Guid riid,
         [Out, MarshalAs(UnmanagedType.Interface)] out object ppIUnk);

     /// <summary>
     /// Opens an area of memory that contains existing metadata. That is, this method opens a specified area of memory in which the existing data is treated as metadata.
     /// </summary>
     /// <param name="pData">[in] A pointer that specifies the starting address of the memory area.</param>
     /// <param name="cbData">[in] The size of the memory area, in bytes.</param>
     /// <param name="dwOpenFlags">[in] A value of the <c>CorOpenFlags</c> enumeration to specify the mode (read, write, and so on) for opening.</param>
     /// <param name="riid">
     /// [in] The IID of the desired metadata interface to be returned; the caller will use the interface to import (read) or emit (write) metadata. 
     /// The value of riid must specify one of the "import" or "emit" interfaces. Valid values are IID_IMetaDataEmit, IID_IMetaDataImport, IID_IMetaDataAssemblyEmit, IID_IMetaDataAssemblyImport, IID_IMetaDataEmit2, or IID_IMetaDataImport2. 
     /// </param>
     /// <param name="ppIUnk">[out] The pointer to the returned interface.</param>
     /// <remarks>
     /// STDMETHOD(OpenScopeOnMemory)(       // Return code.
     ///     LPCVOID     pData,          // [in] Location of scope data.
     ///     ULONG       cbData,         // [in] Size of the data pointed to by pData.
     ///     DWORD       dwOpenFlags,        // [in] Open mode flags.
     ///     REFIID      riid,           // [in] The interface desired.
     ///     IUnknown    **ppIUnk) PURE;     // [out] Return interface on success.
     /// </remarks>
     [PreserveSig]
     void OpenScopeOnMemory(
         [In] IntPtr pData,
         [In] uint cbData,
         [In] int dwOpenFlags,
         [In] ref Guid riid,
         [Out, MarshalAs(UnmanagedType.IUnknown)] out object ppIUnk);
     }


    // Details about this interface are in metahost.idl.
    [ComImport, InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown), Guid("BD39D1D2-BA2F-486A-89B0-B4B0CB466891")]
    internal interface ICLRRuntimeInfo
    {
        // Marshalling pcchBuffer as int even though it's unsigned. Max version string is 24 characters, so we should not need to go over 2 billion soon.
        void GetVersionString([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwzBuffer,
                              [In, Out, MarshalAs(UnmanagedType.U4)] ref int pcchBuffer);

        // Marshalling pcchBuffer as int even though it's unsigned. MAX_PATH is 260, unicode paths are 65535, so we should not need to go over 2 billion soon.
        [PreserveSig]
        int GetRuntimeDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwzBuffer,
                                [In, Out, MarshalAs(UnmanagedType.U4)] ref int pcchBuffer);

        int IsLoaded([In] IntPtr hndProcess);

        // Marshal pcchBuffer as int even though it's unsigned. Error strings approaching 2 billion characters are currently unheard-of.
        [LCIDConversion(3)]
        void LoadErrorString([In, MarshalAs(UnmanagedType.U4)] int iResourceID,
                             [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwzBuffer,
                             [In, Out, MarshalAs(UnmanagedType.U4)] ref int pcchBuffer,
                             [In] int iLocaleID);

        IntPtr LoadLibrary([In, MarshalAs(UnmanagedType.LPWStr)] string pwzDllName);

        IntPtr GetProcAddress([In, MarshalAs(UnmanagedType.LPStr)] string pszProcName);

        [return: MarshalAs(UnmanagedType.IUnknown)]
        System.Object GetInterface([In] ref Guid rclsid, [In] ref Guid riid);

    }

    // Wrapper for standard COM IEnumUnknown, needed for ICLRMetaHost enumeration APIs.
    [ComImport, InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown), Guid("00000100-0000-0000-C000-000000000046")]
    internal interface IEnumUnknown
    {

        [PreserveSig]
        int Next(
            [In, MarshalAs(UnmanagedType.U4)]
             int celt,
            [Out, MarshalAs(UnmanagedType.IUnknown)]
            out System.Object rgelt,
            IntPtr pceltFetched);

        [PreserveSig]
        int Skip(
        [In, MarshalAs(UnmanagedType.U4)]
            int celt);

        void Reset();

        void Clone(
            [Out] 
            out IEnumUnknown ppenum);
    }

    // Dump Debugging support 

    /// <summary>
    /// Wrapper for the ICLRDebugging shim interface. This interface exposes the native pipeline
    /// architecture startup APIs
    /// </summary>
    public sealed class CLRDebugging
    {

        private static readonly Guid clsidCLRDebugging = new Guid("BACC578D-FBDD-48a4-969F-02D932B74634");
        private ICLRDebugging m_CLRDebugging;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <remarks>Creates the underlying interface from mscoree!CLRCreateInstance</remarks>
        public CLRDebugging()
        {
            object o;
            Guid ifaceId = typeof(ICLRDebugging).GUID;
            Guid clsid = clsidCLRDebugging;
            NativeMethods.CLRCreateInstance(ref clsid, ref ifaceId, out o);
            m_CLRDebugging = (ICLRDebugging)o;
        }

        /// <summary>
        /// Detects if a native module represents a CLR and if so provides the debugging interface
        /// and versioning information
        /// </summary>
        /// <param name="moduleBaseAddress">The native base address of a module which might be a CLR</param>
        /// <param name="dataTarget">The process abstraction which can be used for inspection</param>
        /// <param name="libraryProvider">A callback interface for locating version specific debug libraries
        /// such as mscordbi.dll and mscordacwks.dll</param>
        /// <param name="maxDebuggerSupportedVersion">The highest version of the CLR/debugging libraries which
        /// the caller can support</param>
        /// <param name="version">The version of the CLR detected or null if no CLR was detected</param>
        /// <param name="flags">Flags which have additional information about the CLR.
        /// See ClrDebuggingProcessFlags for more details</param>
        /// <returns>The CLR's debugging interface</returns>
        public ICorDebugProcess OpenVirtualProcess(ulong moduleBaseAddress,
            ICorDebugDataTarget dataTarget,
            ICLRDebuggingLibraryProvider libraryProvider,
            Version maxDebuggerSupportedVersion,
            out Version version,
            out ClrDebuggingProcessFlags flags)
        {
            ICorDebugProcess process;
            int hr = TryOpenVirtualProcess(moduleBaseAddress, dataTarget, libraryProvider, maxDebuggerSupportedVersion, out version, out flags, out process);
            if (hr < 0)
                throw new COMException("Failed to OpenVirtualProcess for module at " + moduleBaseAddress + ".", hr);
            return process;
        }

        /// <summary>
        /// Version of the above that doesn't throw exceptions on failure
        /// </summary>        
        public int TryOpenVirtualProcess(ulong moduleBaseAddress,
            ICorDebugDataTarget dataTarget,
            ICLRDebuggingLibraryProvider libraryProvider,
            Version maxDebuggerSupportedVersion,
            out Version version,
            out ClrDebuggingProcessFlags flags,
            out ICorDebugProcess process)
        {
            ClrDebuggingVersion maxSupport = new ClrDebuggingVersion();
            ClrDebuggingVersion clrVersion = new ClrDebuggingVersion();
            maxSupport.StructVersion = 0;
            maxSupport.Major = (short)maxDebuggerSupportedVersion.Major;
            maxSupport.Minor = (short)maxDebuggerSupportedVersion.Minor;
            maxSupport.Build = (short)maxDebuggerSupportedVersion.Build;
            maxSupport.Revision = (short)maxDebuggerSupportedVersion.Revision;
            object processIface = null;
            clrVersion.StructVersion = 0;
            Guid iid = typeof(ICorDebugProcess).GUID;

            int result = m_CLRDebugging.OpenVirtualProcess(moduleBaseAddress, dataTarget, libraryProvider,
                ref maxSupport, ref iid, out processIface, ref clrVersion, out flags);

            // This may be set regardless of success/failure
            version = new Version(clrVersion.Major, clrVersion.Minor, clrVersion.Build, clrVersion.Revision);

            if (result < 0)
            {
                // OpenVirtualProcess failed
                process = null;
                return result;
            }

            // Success
            process = (ICorDebugProcess)processIface;
            return 0;
        }

        /// <summary>
        /// Determines if the module is no longer in use
        /// </summary>
        /// <param name="moduleHandle">A module handle that was provided via the ILibraryProvider</param>
        /// <returns>True if the module can be unloaded, False otherwise</returns>
        public bool CanUnloadNow(IntPtr moduleHandle)
        {
            int ret = m_CLRDebugging.CanUnloadNow(moduleHandle);
            if (ret == 0)   // S_OK
                return true;
            else if (ret == (int)1) // S_FALSE
                return false;
            else
                Marshal.ThrowExceptionForHR(ret);

            //unreachable
            throw new Exception();
        }
    }



    /// <summary>
    /// Represents a version of the CLR runtime
    /// </summary>
    public struct ClrDebuggingVersion
    {
        public short StructVersion;
        public short Major;
        public short Minor;
        public short Build;
        public short Revision;
    }

    /// <summary>
    /// Information flags about the state of a CLR when it is being attached
    /// to in the native pipeline debugging model
    /// </summary>
    public enum ClrDebuggingProcessFlags
    {
        // This CLR has a non-catchup managed debug event to send after jit attach is complete
        ManagedDebugEventPending = 1
    }

    /// <summary>
    /// This interface exposes the native pipeline architecture startup APIs
    /// </summary>
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("D28F3C5A-9634-4206-A509-477552EEFB10")]
    public interface ICLRDebugging
    {
        /// <summary>
        /// Detects if a native module represents a CLR and if so provides the debugging interface
        /// and versioning information
        /// </summary>
        /// <param name="moduleBaseAddress">The native base address of a module which might be a CLR</param>
        /// <param name="dataTarget">The process abstraction which can be used for inspection</param>
        /// <param name="libraryProvider">A callback interface for locating version specific debug libraries
        /// such as mscordbi.dll and mscordacwks.dll</param>
        /// <param name="maxDebuggerSupportedVersion">The highest version of the CLR/debugging libraries which
        /// the caller can support</param>
        /// <param name="process">The CLR's debugging interface or null if no debugger was detected</param>
        /// <param name="version">The version of the CLR detected or null if no CLR was detected</param>
        /// <param name="flags">Flags which have additional information about the CLR.
        /// See ClrDebuggingProcessFlags for more details</param>
        /// <returns>HResults.S_OK if an appropriate version CLR was detected, otherwise an appropriate
        /// error hresult</returns>
        [PreserveSig]
        int OpenVirtualProcess([In] ulong moduleBaseAddress,
                                [In, MarshalAs(UnmanagedType.IUnknown)] object dataTarget,
                                [In, MarshalAs(UnmanagedType.Interface)] ICLRDebuggingLibraryProvider libraryProvider,
                                [In] ref ClrDebuggingVersion maxDebuggerSupportedVersion,
                                [In] ref Guid riidProcess,
                                [Out, MarshalAs(UnmanagedType.IUnknown)] out object process,
                                [In, Out] ref ClrDebuggingVersion version,
                                [Out] out ClrDebuggingProcessFlags flags);

        /// <summary>
        /// Determines if the module is no longer in use
        /// </summary>
        /// <param name="moduleHandle">A module handle that was provided via the ILibraryProvider</param>
        /// <returns>HResults.S_OK if the module can be unloaded, HResults.S_FALSE if it is in use
        /// or an appropriate error hresult otherwise</returns>
        [PreserveSig]
        int CanUnloadNow(IntPtr moduleHandle);
    }

    /// <summary>
    /// Provides version specific debugging libraries such as mscordbi.dll and mscorwks.dll during
    /// startup in the native pipeline debugging architecture
    /// </summary>
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("3151C08D-4D09-4f9b-8838-2880BF18FE51")]
    public interface ICLRDebuggingLibraryProvider
    {
        /// <summary>
        /// Provides a version specific debugging library
        /// </summary>
        /// <param name="fileName">The name of the library being requested</param>
        /// <param name="timestamp">The timestamp of the library being requested as specified
        /// in the PE header</param>
        /// <param name="sizeOfImage">The SizeOfImage of the library being requested as specified
        /// in the PE header</param>
        /// <param name="hModule">An OS handle to the requested library</param>
        /// <returns>HResults.S_OK if the library was located, otherwise any appropriate
        /// error hresult</returns>
        [PreserveSig]
        int ProvideLibrary([In, MarshalAs(UnmanagedType.LPWStr)]string fileName,
            int timestamp,
            int sizeOfImage,
            out IntPtr hModule);
    }

#endregion
} /* namespace */
