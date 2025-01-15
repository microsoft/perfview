using Microsoft.Diagnostics.Tracing.Compatibility;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

/// <summary>
/// Finds native DLLS next to the managed DLL that uses them.   
/// </summary>
internal class NativeDlls
{
    /// <summary>
    /// ManifestModule.FullyQualifiedName returns this as file path if the assembly is loaded as byte array
    /// </summary>
    private const string UnknownLocation = "<Unknown>";

    /// <summary>
    /// Loads a native DLL with a filename-extension of 'simpleName' by adding the path of the currently executing assembly
    /// 
    /// </summary>
    /// <param name="simpleName"></param>
    public static void LoadNative(string simpleName)
    {
        // When TraceEvent is loaded as embedded assembly the manifest path is <Unknown>
        // We use as fallback in that case the process executable location to enable scenarios where TraceEvent and related dlls
        // are loaded from byte arrays into the AppDomain to create self contained executables with no other dependent libraries. 
        string assemblyLocation = typeof(NativeDlls).GetTypeInfo().Assembly.ManifestModule.FullyQualifiedName;
        if (assemblyLocation == UnknownLocation)
        {
            assemblyLocation = Process.GetCurrentProcess().MainModule.FileName;
        }

        var thisDllDir = Path.GetDirectoryName(assemblyLocation);

        // Try next to the current DLL
        var dllName = Path.Combine(thisDllDir, simpleName);
        var ret = LoadLibraryEx(dllName, IntPtr.Zero, LoadLibraryFlags.LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR);
        if (ret != IntPtr.Zero)
        {
            return;
        }

        // Try in <arch> directory
        dllName = Path.Combine(thisDllDir, ProcessArchitectureDirectory, simpleName);
        ret = LoadLibraryEx(dllName, IntPtr.Zero, LoadLibraryFlags.LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR);
        if (ret != IntPtr.Zero)
        {
            return;
        }

        // Try in ../native/<arch>.  This is where it will be in a nuget package. 
        dllName = Path.Combine(Path.GetDirectoryName(thisDllDir), "native", ProcessArchitectureDirectory, simpleName);
        ret = LoadLibraryEx(dllName, IntPtr.Zero, LoadLibraryFlags.LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR);
        if (ret != IntPtr.Zero)
        {
            return;
        }

        throw new ApplicationException("Could not load native DLL " + dllName);
    }

    public static Architecture ProcessArch
    {
        get
        {
            return RuntimeInformation.ProcessArchitecture;
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
                // Special case amd64 because the Architecture uses X64, but the previous behavior was to use amd64.
                s_ProcessArchDirectory = ProcessArch == Architecture.X64 ? "amd64" : ProcessArch.ToString().ToLowerInvariant();
            }

            return s_ProcessArchDirectory;
        }
    }

    /// <summary>
    /// This is the backing field for the lazily-computed <see cref="ProcessArchitectureDirectory"/> property.
    /// </summary>
    private static string s_ProcessArchDirectory;


    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hReservedNull, LoadLibraryFlags dwFlags);

    private enum LoadLibraryFlags : uint
    {
        LOAD_LIBRARY_SEARCH_DLL_LOAD_DIR = 0x00000100
    }


}
