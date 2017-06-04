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
        var dllName = Path.Combine(Path.Combine(thisDllDir, ProcessArchitectureDirectory), simpleName);
        var ret = LoadLibrary(dllName);
        if (ret == IntPtr.Zero)
        {
            var errorCode = Marshal.GetLastWin32Error();
            throw new ApplicationException("Could not load native DLL " + dllName + " HRESULT=0x" + errorCode.ToString("x"));
        }
    }

    public static ProcessorArchitecture ProcessArch
    {
        get
        {
            return IntPtr.Size == 8 ? ProcessorArchitecture.Amd64 : ProcessorArchitecture.X86;
        }
    }

    /// <summary>
    /// Gets the name of the directory containing compiled binaries (DLLs) which have the same architecture as the
    /// currently executing process.
    /// </summary>
    public static string ProcessArchitectureDirectory
    {
        get
        {
            if (s_ProcessArchDirectory == null)
            {
                s_ProcessArchDirectory = ProcessArch.ToString().ToLowerInvariant();
            }

            return s_ProcessArchDirectory;
        }
    }

    /// <summary>
    /// This is the backing field for the lazily-computed <see cref="ProcessArchitectureDirectory"/> property.
    /// </summary>
    private static string s_ProcessArchDirectory;


    [System.Runtime.InteropServices.DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);
}
