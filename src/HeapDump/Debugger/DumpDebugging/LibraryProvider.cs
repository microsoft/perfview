using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Samples.Debugging.CorDebug;
using Microsoft.Samples.Debugging.CorDebug.Utility;
using System.Runtime.InteropServices;
using System.IO;
using Microsoft.Samples.Debugging.Native;

namespace Profiler
{
    /// <summary>
    /// A simple ICLRDebuggingLibraryProvider implementation that scans all
    /// the installed runtime directories looking for a matching file
    /// </summary>
    public class LibraryProvider : ICLRDebuggingLibraryProvider
    {
        public int ProvideLibrary(string fileName, int timestamp, int sizeOfImage, out IntPtr hModule)
        {
            CLRMetaHost mh = new CLRMetaHost();
            foreach (CLRRuntimeInfo rti in mh.EnumerateInstalledRuntimes())
            {
                string versionString = rti.GetVersionString();
                if (versionString.StartsWith("v2."))
                    continue;

                string libPath = Path.Combine(rti.GetRuntimeDirectory(), fileName);
                if (DoesFileMatch(libPath, timestamp, sizeOfImage))
                {
                    hModule = LoadLibrary(libPath);
                    if (hModule != IntPtr.Zero)
                    {
                        UpdateLastLoaded(fileName, libPath);
                        return 0;
                    }
                    else
                    {
                        return -1;
                    }
                }
            }

            // not found
            hModule = IntPtr.Zero;
            return -1;
        }

        const string s_kernel = "kernel32";
        [DllImport(s_kernel, CharSet = CharSet.Auto, BestFitMapping = false, SetLastError = true)]
        public static extern IntPtr LoadLibrary(string fileName);

        /// <summary>
        /// Determines if the file at the given path matches the timestamp and sizeOfImage values
        /// </summary>
        /// <param name="filePath">the file to check</param>
        /// <param name="timestamp">the PE timestamp value to verifiy</param>
        /// <param name="sizeOfImage">the PE sizeOfImage value to verify</param>
        /// <returns>true if the file is a match and false otherwise</returns>
        bool DoesFileMatch(string filePath, int timestamp, int sizeOfImage)
        {
            try
            {
                // quick check to avoid exception handling for a common error
                // this check is not required for correctness
                if (!File.Exists(filePath))
                    return false;

                // TODO we could do a timestamp match too. 
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        public string LastLoadedDbi
        {
            get { return m_lastDbi;}
        }

        public string LastLoadedDac
        {
            get { return m_lastDac; }
        }

        private void UpdateLastLoaded(string fileName, string libPath)
        {
            if (String.Compare(fileName, "mscordacwks.dll", true) == 0)
            {
                m_lastDac = libPath;
            }
            else if (String.Compare(fileName, "mscordbi.dll", true) == 0)
            {
                m_lastDbi = libPath;
            }
        }

        private string m_lastDbi;
        private string m_lastDac;
    }
}