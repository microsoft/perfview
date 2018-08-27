using Microsoft.Samples.Debugging.CorDebug;
using Microsoft.Samples.Debugging.CorDebug.NativeApi;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using Profiler;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

public class Silverlight
{
    public static ICorDebug DebugActiveSilverlightProcess(int processId, DebuggerCallBacks callBacks = null)
    {
        // Get all funcs exported by the coreclr's dbg shim.
        Silverlight.InitSLApi();

        // Enumerate all coreclr instances in the process
        string[] fullPaths;
        EventWaitHandle[] continueStartupEvents;
        Silverlight.EnumerateCLRs((uint)processId, out fullPaths, out continueStartupEvents);
        int nSilverlight = fullPaths.Length;
        if (fullPaths == null || nSilverlight == 0)
        {
            return null;
        }

        // for each coreclr instance found.....
        if (nSilverlight > 0)
        {
            // Attach to the first one
            // TODO are we leaking handles for the rest, do we care about which one?  
            string slVersion = Silverlight.CreateVersionStringFromModule((uint)processId, fullPaths[0]);
            var debugAPI = Silverlight.CreateDebuggingInterfaceFromVersionEx(4, slVersion);
            debugAPI.Initialize();

            if (callBacks == null)
            {
                callBacks = new DebuggerCallBacks();
            }

            debugAPI.SetManagedHandler(callBacks);
            return debugAPI;
        }

        return null;
    }

    #region private
    private static void InitSLApi()
    {
        // check if already initialized
        if (true == s_bSLApiInit)
        {
            return;
        }

        // we must check if debug pack is installed

        RegistryKey netFrameworkKey = null;
        IntPtr procAddr = IntPtr.Zero;
        try
        {
            netFrameworkKey = Registry.LocalMachine.OpenSubKey(
                s_strNetFramework,
                false);

            if (null == netFrameworkKey)
            {
                throw new Exception("Unable to open the .NET Registry key: HKLM\\" + s_strNetFramework);
            }

            s_strDbgPackShimPath = (string)netFrameworkKey.GetValue(
                s_strValDbgPackShimPath,
                null,
                RegistryValueOptions.DoNotExpandEnvironmentNames);

            if (null == s_strDbgPackShimPath)
            {
                throw new Exception("Unable to open up the DbgPackShimPath registry key: HKLM\\" + s_strNetFramework + "\\" + s_strDbgPackShimPath + "\r\n" +
                                    "It is likely that the Silverlight Developer Package is not installed.\r\n" +
                                    "Please Download and install from http://www.silverlight.net/downloads");
            }

            // now initializing all the func ptrs
            s_hndModuleMscoree = LoadLib(s_strDbgPackShimPath);

            try
            {
                procAddr = GetProcAddr(s_hndModuleMscoree, "CreateDebuggingInterfaceFromVersionEx");
            }
            catch (Win32Exception)
            {
                throw new Exception("Could not find CreateDebuggingInterfaceFromVersionEx, are you running Silverlight 5.0 or above?");
            }
            s_CreateDebuggingInterfaceFromVersionEx = (delgCreateDebuggingInterfaceFromVersionEx)Marshal.GetDelegateForFunctionPointer(
                    procAddr,
                    typeof(delgCreateDebuggingInterfaceFromVersionEx));

            procAddr = GetProcAddr(s_hndModuleMscoree, "CreateVersionStringFromModule");
            s_CreateVersionStringFromModule = (delgCreateVersionStringFromModule)Marshal.GetDelegateForFunctionPointer(
                    procAddr,
                    typeof(delgCreateVersionStringFromModule));

            procAddr = GetProcAddr(s_hndModuleMscoree, "EnumerateCLRs");
            s_EnumerateCLRs = (delgEnumerateCLRs)Marshal.GetDelegateForFunctionPointer(
                    procAddr,
                    typeof(delgEnumerateCLRs));

            procAddr = GetProcAddr(s_hndModuleMscoree, "CloseCLREnumeration");
            s_CloseCLREnumeration = (delgCloseCLREnumeration)Marshal.GetDelegateForFunctionPointer(
                    procAddr,
                    typeof(delgCloseCLREnumeration));

            procAddr = GetProcAddr(s_hndModuleMscoree, "GetStartupNotificationEvent");
            s_GetStartupNotificationEvent = (delgGetStartupNotificationEvent)Marshal.GetDelegateForFunctionPointer(
                    procAddr,
                    typeof(delgGetStartupNotificationEvent));

            // We are done initializing
            s_bSLApiInit = true;
        }
        finally
        {
            if (null != netFrameworkKey)
            {
                netFrameworkKey.Close();
            }
        }
    }
    private static string CreateVersionStringFromModule(UInt32 debuggeePid, string clrPath)
    {
        UInt32 reqBufferSize = 0;

        // first call is getting the reqBufferSize
        s_CreateVersionStringFromModule(
            0,
            clrPath,
            null,
            0,
            out reqBufferSize);

        StringBuilder sb = new StringBuilder((int)reqBufferSize);

        // this call can fail because the underlying call uses CreateToolhelp32Snapshot
        //
        int ret;
        int numTries = 0;
        do
        {
            Trace.WriteLine("In CreateVersionStringFromModule, numTries=" + numTries.ToString());
            ret = (int)s_CreateVersionStringFromModule(
                                        debuggeePid,
                                        clrPath,
                                        sb,
                                        (UInt32)sb.Capacity,
                                        out reqBufferSize);
            // s_CreateVersionStringFromModule uses the OS API CreateToolhelp32Snapshot which can return 
            // ERROR_BAD_LENGTH or ERROR_PARTIAL_COPY. If we get either of those, we try wait 1/10th of a second
            // try again (that is the recommendation of the OS API owners)
            if (((int)HResult.E_BAD_LENGTH == ret) || ((int)HResult.E_PARTIAL_COPY == ret))
            {
                System.Threading.Thread.Sleep(100);
            }
            else
            {
                break;
            }
            numTries++;
        } while (numTries < 10);

        if ((int)HResult.S_OK != ret)
        {
            throw new COMException("CreateVersionStringFromModule failed returning the following HResult: " + Silverlight.HResultToString(ret), ret);
        }

        return sb.ToString();
    }
    private static ICorDebug CreateDebuggingInterfaceFromVersionEx(int iDebuggerVersion, string strDebuggeeVersion)
    {
        if (null == s_CreateDebuggingInterfaceFromVersionEx)
        {
            return null;
        }
        ICorDebug ICorDbgIntf;
        int ret;
        int numTries = 0;
        do
        {
            Trace.WriteLine("In CreateDebuggingInterfaceFromVersionEx, numTries=" + numTries.ToString());
            ret = (int)s_CreateDebuggingInterfaceFromVersionEx(
                iDebuggerVersion,
                strDebuggeeVersion,
                out ICorDbgIntf
               );
            // CreateDebuggingInterfaceFromVersionEx uses the OS API CreateToolhelp32Snapshot which can return 
            // ERROR_BAD_LENGTH or ERROR_PARTIAL_COPY. If we get either of those, we try wait 1/10th of a second
            // try again (that is the recommendation of the OS API owners)
            if (((int)HResult.E_BAD_LENGTH == ret) || ((int)HResult.E_PARTIAL_COPY == ret))
            {
                System.Threading.Thread.Sleep(100);
            }
            else
            {
                // else we've hit one of the HRESULTS that we shouldn't try again for, if the result isn't OK then the error will ge reported below
                break;
            }
            numTries++;
        }
        while (numTries < 10);

        // if we're not OK then throw an exception with the returned hResult
        if ((int)HResult.S_OK != ret)
        {
            throw new COMException("CreateDebuggingInterfaceFromVersionEx for debuggee version " + strDebuggeeVersion +
                " requesting interface version " + iDebuggerVersion +
                " failed returning " + Silverlight.HResultToString(ret), ret);
        }
        return ICorDbgIntf;
    }
    private static void EnumerateCLRs(UInt32 debuggeePid, out string[] fullPaths, out EventWaitHandle[] continueStartupEvents)
    {
        UInt32 elementCount;
        IntPtr pEventArray;
        IntPtr pStringArray;

        int ret;
        int numTries = 0;
        do
        {
            Trace.WriteLine(numTries > 0, "In EnumerateCLRs, numTries=" + numTries.ToString());
            ret = (int)s_EnumerateCLRs(
                debuggeePid,
                out pEventArray,
                out pStringArray,
                out elementCount
                );
            // EnumerateCLRs uses the OS API CreateToolhelp32Snapshot which can return 
            // ERROR_BAD_LENGTH or ERROR_PARTIAL_COPY. If we get either of those, we try wait 1/10th of a second
            // try again (that is the recommendation of the OS API owners)
            if (((int)HResult.E_BAD_LENGTH == ret) || ((int)HResult.E_PARTIAL_COPY == ret))
            {
                System.Threading.Thread.Sleep(100);
            }
            else
            {
                break;
            }
            numTries++;
        } while (((int)HResult.S_OK != ret) && (numTries < 10));

        if ((int)HResult.S_OK != ret)
        {
            fullPaths = new string[0];
            continueStartupEvents = new EventWaitHandle[0];
            return;
        }

        MarshalCLREnumeration(
            pEventArray,
            pStringArray,
            elementCount,
            out fullPaths,
            out continueStartupEvents);

        ret = (int)s_CloseCLREnumeration(
            pEventArray,
            pStringArray,
            elementCount);

        if ((int)HResult.S_OK != ret)
        {
            throw new COMException("CloseCLREnumeration failed for process PID=" + debuggeePid + ", HResult= " + Silverlight.HResultToString(ret), ret);
        }
    }

    #region delegate definitions
    private delegate UInt32 delgCreateDebuggingInterfaceFromVersionEx(
        [MarshalAs(UnmanagedType.I4)] int iDebuggerVersion,
        [MarshalAs(UnmanagedType.LPWStr)] string strDebuggeeVersion,
        out ICorDebug ICorDebugIntf
        );
    private delegate UInt32 delgCreateVersionStringFromModule(
        UInt32 pidDebuggee,
        [MarshalAs(UnmanagedType.LPWStr)] string strModuleName,
        [MarshalAs(UnmanagedType.LPWStr)] StringBuilder strBuffer,
        UInt32 cchBufferSize,
        out UInt32 cchRequiredBufferSize
        );
    private delegate UInt32 delgEnumerateCLRs(
        UInt32 pidDebuggee,
        out IntPtr pEventArray,
        out IntPtr pStringArray,
        out UInt32 elementCount
        );
    private delegate UInt32 delgCloseCLREnumeration(
        IntPtr pEventArray,
        IntPtr pStringArray,
        UInt32 elementCount
        );
    private delegate UInt32 delgGetStartupNotificationEvent(
        UInt32 debuggeePid,
        out SafeWaitHandle startupNotifyEvent
        );
    #endregion

    private static unsafe void MarshalCLREnumeration(
        IntPtr ptrEventArray, IntPtr ptrStringArray, UInt32 elementCount,
        out string[] fullPaths, out EventWaitHandle[] continueStartupEvents)
    {
        fullPaths = new string[elementCount];
        continueStartupEvents = new EventWaitHandle[elementCount];

        IntPtr* phEvents = (IntPtr*)ptrEventArray.ToPointer();
        char** pstrStrings = (char**)ptrStringArray.ToPointer();

        IntPtr hDuppedHandle;
        IntPtr hCurrentProcess = NativeMethods.GetCurrentProcess();

        for (int i = 0; i < elementCount; i++)
        {
            IntPtr hEvent = *phEvents;

            if ((hEvent == IntPtr.Zero) || (hEvent == new IntPtr(-1)))
            {
                hDuppedHandle = hEvent;
            }

            else
            {
                if (!NativeMethods.DuplicateHandle(
                        hCurrentProcess,
                        hEvent,
                        hCurrentProcess,
                        out hDuppedHandle,
                        0,
                        false,
                        (int)NativeMethods.DuplicateHandleOptions.DUPLICATE_SAME_ACCESS))
                {
                    throw new Exception(
                        "could not duplicate handle in MarshalCLREnumeration.  GLE: " + Marshal.GetLastWin32Error());
                }

            }

            continueStartupEvents[i] = new AutoResetEvent(false);
            continueStartupEvents[i].SafeWaitHandle = new SafeWaitHandle(hDuppedHandle, true);
            fullPaths[i] = new string(*pstrStrings);
            pstrStrings++;
            phEvents++;
        }
    }

    private static IntPtr LoadLib(string strFileName)
    {
        IntPtr hndModule = NativeMethods.LoadLibrary(strFileName);

        if (IntPtr.Zero == hndModule)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return hndModule;
    }
    private static IntPtr GetProcAddr(IntPtr hndModule, string strProcName)
    {
        IntPtr farProc = NativeMethods.GetProcAddress(hndModule, strProcName);

        if (IntPtr.Zero == farProc)
        {
            Console.WriteLine("GetProcAddress failed in Silverlight Extension for " + strProcName + ": " + Marshal.GetLastWin32Error());
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return farProc;
    }

    private static String HResultToString(int hResult)
    {
        String returnValue = "";
        try
        {
            if (Enum.IsDefined(typeof(HResult), hResult))
            {
                returnValue = ((HResult)hResult).ToString();
            }
            else
            {
                returnValue = "0x" + hResult.ToString("X");
            }
        }
        catch (Exception)
        {
            // default just convert the hresult to hex
            returnValue = "0x" + hResult.ToString("X");
        }
        return returnValue;
    }

    private static delgCreateDebuggingInterfaceFromVersionEx s_CreateDebuggingInterfaceFromVersionEx;
    private static delgCreateVersionStringFromModule s_CreateVersionStringFromModule;
    private static delgEnumerateCLRs s_EnumerateCLRs;
    private static delgCloseCLREnumeration s_CloseCLREnumeration;
    private static delgGetStartupNotificationEvent s_GetStartupNotificationEvent;

    private static bool s_bSLApiInit = false;
    private static string s_strNetFramework = @"SOFTWARE\Microsoft\.NETFramework";
    private static string s_strValDbgPackShimPath = "DbgPackShimPath";
    private static string s_strDbgPackShimPath = null;
    private static IntPtr s_hndModuleMscoree = IntPtr.Zero;
    #endregion
}

#region internal classes
internal static class NativeMethods
{
    private const string Kernel32LibraryName = "kernel32.dll";

    [DllImport(Kernel32LibraryName, SetLastError = true)]
    public static extern bool DuplicateHandle(
        IntPtr hSourceProcessHandle,
        IntPtr hSourceHandle,
        IntPtr hTargetProcessHandle,
        out IntPtr targetHandle,
        int dwDesiredAccess,
        bool inheritHandle,
        int dwOptions);

    [DllImport(Kernel32LibraryName, SetLastError = true)]
    public static extern IntPtr GetCurrentProcess();
    [DllImport(Kernel32LibraryName, CharSet = CharSet.Ansi, ExactSpelling = true)]
    public static extern IntPtr GetProcAddress(
        IntPtr hModule,
        string procName);

    [DllImport(Kernel32LibraryName)]
    public static extern IntPtr LoadLibrary(string lpFileName);

    [Flags]
    public enum DuplicateHandleOptions : uint
    {
        DUPLICATE_CLOSE_SOURCE = 0x1,
        DUPLICATE_SAME_ACCESS = 0x2,
    }
}
#endregion
