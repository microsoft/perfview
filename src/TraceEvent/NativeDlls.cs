using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

/// <summary>
/// Finds native DLLS next to the managed DLL that uses them.   
/// </summary>
class NativeDlls
{
    /// <summary>
    /// Loads a native DLL with a filename-extention of 'simpleName' by adding the path of the currently executing assembly
    /// 
    /// </summary>
    /// <param name="simpleName"></param>
    public static void LoadNative(string simpleName)
    {
        var thisDllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().ManifestModule.FullyQualifiedName);
        var dllName = Path.Combine(Path.Combine(thisDllDir, ProcessArch), simpleName);
        var ret = LoadLibrary(dllName);
        if (ret == IntPtr.Zero)
        {
            var errorCode = Marshal.GetLastWin32Error();
            throw new ApplicationException("Could not load native DLL " + dllName + " HRESULT=0x" + errorCode.ToString("x"));
        }
    }

    public static string ProcessArch
    {
        get
        {
            if (s_ProcessArch == null)
            {
                s_ProcessArch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
                // THis should not be necessary, but the VS hosting process says its AMD64 but is in fact a 32 bit process. 
                if (s_ProcessArch == "AMD64" && Marshal.SizeOf(typeof(IntPtr)) == 4)
                    s_ProcessArch = "x86";
            }
            return s_ProcessArch;
        }
    }
    #region private
    private static string s_ProcessArch;


    [System.Runtime.InteropServices.DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);
    #endregion
}
