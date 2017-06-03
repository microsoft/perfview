using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Microsoft.Diagnostics.Tracing.PEFile;
using Microsoft.Diagnostics.Tracing.Stacks;
using System.Runtime.InteropServices;

namespace Diagnostics.Tracing.StackSources
{
    /// <summary>
    /// A stack source that displays file size by path.     
    /// </summary>
    class FileSizeStackSource : InternStackSource
    {
        /// <summary>
        /// Note that on windows, lastAccessTime does not really work (acts like lastWriteTime).  They turned it off for efficiency reasons. 
        /// </summary>
        public FileSizeStackSource(string directoryPath, TextWriter log, bool useWriteTime=true)
        {
            m_useWriteTime = useWriteTime;
            m_nowUtc = DateTime.UtcNow;
            m_log = log;
            // Make the full path the root node.   
            var stackBase = Interner.CallStackIntern(Interner.FrameIntern("DIR: " + Path.GetFullPath(directoryPath)), StackSourceCallStackIndex.Invalid);
            AddSamplesForDirectory(directoryPath, stackBase);
            Interner.DoneInterning();
        }

        #region private
        private void AddSamplesForDirectory(string directoryPath, StackSourceCallStackIndex directoryStack)
        {
            StackSourceSample sample = null;
            try
            {
                var directory = new FastDirectory(directoryPath);
                foreach (var member in directory.Members)
                {

                    if (member.IsDirectory)
                    {
                        var stack = Interner.CallStackIntern(Interner.FrameIntern("DIR: " + member.Name), directoryStack);
                        AddSamplesForDirectory(Path.Combine(directoryPath, member.Name), stack);
                    }
                    else
                    {
                        var stack = directoryStack;

                        // Allow easy grouping by extension.  
                        var ext = Path.GetExtension(member.Name).ToLower();
                        // And whether the DLL/EXE is managed or not.  
                        var suffix = "";
                        if (string.Compare(ext, ".dll", true) == 0 || string.Compare(ext, ".exe", true) == 0 || string.Compare(ext, ".winmd", true) == 0)
                        {
                            suffix = "";
                            string fileName = Path.Combine(directoryPath, member.Name);
                            try
                            {
                                using (var peFile = new PEFile(fileName))
                                {
                                    suffix = peFile.Header.IsManaged ? " (MANAGED)" : " (UNMANAGED)";
                                    if (peFile.Header.IsPE64)
                                        suffix += " (64Bit)";
                                    if (peFile.HasPrecompiledManagedCode)
                                    {
                                        if (peFile.IsManagedReadyToRun)
                                        {
                                            short major, minor;
                                            peFile.ReadyToRunVersion(out major, out minor);
                                            suffix += " (ReadyToRun(" + major + "." + minor + "))";

                                        }
                                        else
                                            suffix += " (NGEN)";
                                    }
                                }
                            }
                            catch (Exception) {
                                m_log.WriteLine("Error: exception looking at file " + fileName);
                                m_log.Flush();
                            }
                        }
                        stack = Interner.CallStackIntern(Interner.FrameIntern("EXT: " + ext + suffix), stack);

                        // Finally the file name itself.  
                        stack = Interner.CallStackIntern(Interner.FrameIntern("FILE: " + member.Name), stack);
                        if (sample == null)
                            sample = new StackSourceSample(this);

                        sample.Metric = member.Size;
                        sample.StackIndex = stack;
                        if (m_useWriteTime)
                            sample.TimeRelativeMSec = (m_nowUtc - member.LastWriteTimeUtc).TotalDays;
                        else 
                            sample.TimeRelativeMSec = (m_nowUtc - member.LastAccessTimeUtc).TotalDays; 
                        AddSample(sample);

                        m_totalSize += member.Size;
                        int count = SampleIndexLimit;
                        if ((count % 1000) == 0)
                            m_log.WriteLine("[Processed " + count + " files, size " + (m_totalSize/1000000).ToString("n0") + " MB in directory scan at " + Path.Combine(directoryPath, member.Name) + " ]");
                    }
                }
            }
            catch (Exception e)
            {
                m_log.WriteLine("Error processing directory " + directoryPath + ": " + e.Message); 
            }
        }

        TextWriter m_log;
        DateTime m_nowUtc;
        bool m_useWriteTime;
        long m_totalSize;
        #endregion 
    }

    /// <summary>
    /// Turns out that Directory.GetFiles is not very efficient.  This is an alternative.    
    ///
    /// </summary>
    public unsafe class FastDirectory
    {
        public class FastDirectoryMember
        {
            public string Name { get; private set; }
            // This is the timestamp convention used for windows files, which is defined as 100ns units
            // from Jan 1, 1601 at Universal Time Coordinates (Greenwich Mean Time).
            public long LastWriteFileTime { get; private set; }
            // This is the timestamp convention used for windows files, which is defined as 100ns units
            // from Jan 1, 1601 at Universal Time Coordinates (Greenwich Mean Time).
            public long LastAccessFileTime { get; private set; }
            public long Size { get; private set; }
            public FileAttributes Attributes { get; private set; }
            public DateTime LastWriteTimeUtc { get { return DateTime.FromFileTimeUtc(LastWriteFileTime); } }
            public DateTime LastAccessTimeUtc { get { return DateTime.FromFileTimeUtc(LastAccessFileTime); } }
            public bool IsDirectory { get { return (Attributes & FileAttributes.Directory) != 0; } }
            public bool IsReparsePoint { get { return (Attributes & FileAttributes.ReparsePoint) != 0; } }
            #region private
            internal FastDirectoryMember(ref WIN32_FIND_DATA info)
            {
                Attributes = info.dwFileAttributes;
                LastWriteFileTime = info.ftLastWriteTime;
                LastAccessFileTime = info.ftLastAccessTime;
                Size = (((long)info.nFileSizeHigh) << 32) + info.nFileSizeLow;
                fixed (char* ptr = info.cFileName)
                    Name = new string(ptr);
            }
            #endregion
        }
        public FastDirectory(string directoryName)
        {
            System.Threading.Thread.Sleep(0);       // Allow interruption
            members = new Dictionary<string, FastDirectoryMember>(32, StringComparer.OrdinalIgnoreCase);
            WIN32_FIND_DATA info = new WIN32_FIND_DATA();
            if (directoryName.Length == 0 || (directoryName.Length == 2 && directoryName[1] == ':'))
                directoryName += ".";

            IntPtr handle = FindFirstFileW(directoryName + @"\*", ref info);
            if (handle != (IntPtr)(-1))
            {
                for (; ; )
                {
                    if (ShouldAdd(ref info))
                    {
                        var member = new FastDirectoryMember(ref info);
                        members.Add(member.Name, member);
                    }
                    if (FindNextFileW(handle, ref info) == 0)
                    {
                        int hr = Marshal.GetLastWin32Error();
                        if (hr != 18)       // NO MORE FILES 
                            throw new System.ComponentModel.Win32Exception(hr);
                        break;
                    }
                }
                FindClose(handle);
            }
            else
            {
                int hr = Marshal.GetLastWin32Error();
                if (hr != 3)       // FILE NOT FOUND
                    throw new System.ComponentModel.Win32Exception(hr);
            }
        }
        public IEnumerable<FastDirectoryMember> Members { get { return members.Values; } }
        public FastDirectoryMember this[string name]
        {
            get
            {
                FastDirectoryMember ret = null;
                members.TryGetValue(name, out ret);
                return ret;
            }
        }
        public int Count { get { return members.Count; } }
        #region private

        private const int MAX_PATH = 260;
        private const int MAX_ALTERNATE = 14;

        [StructLayout(LayoutKind.Sequential, Pack = 4, CharSet = CharSet.Unicode)]
        internal struct WIN32_FIND_DATA
        {
            public FileAttributes dwFileAttributes;
            public long ftCreationTime;
            public long ftLastAccessTime;
            public long ftLastWriteTime;
            public uint nFileSizeHigh;
            public uint nFileSizeLow;
            public int dwReserved0;
            public int dwReserved1;
            public fixed char cFileName[MAX_PATH];
            public fixed char cAlternate[MAX_ALTERNATE];
        };

        [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true), System.Security.SuppressUnmanagedCodeSecurityAttribute]
        private static extern IntPtr FindFirstFileW(string lpFileName, ref WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32", SetLastError = true), System.Security.SuppressUnmanagedCodeSecurityAttribute]
        private static extern int FindNextFileW(IntPtr hFindFile, ref WIN32_FIND_DATA lpFindFileData);

        [DllImport("kernel32"), System.Security.SuppressUnmanagedCodeSecurityAttribute]
        private static extern int FindClose(IntPtr hFindFile);

        private static bool ShouldAdd(ref WIN32_FIND_DATA info)
        {
            if ((info.dwFileAttributes & FileAttributes.Directory) == 0)
                return true;
            fixed (char* ptr = info.cFileName)
            {
                if (ptr[0] != '.')
                    return true;
                if (ptr[1] == '\0')
                    return false;         // . should not be included
                if (ptr[1] != '.')
                    return true;
                if (ptr[2] == '\0')       // .. should not be included
                    return false;
                return true;
            }
        }

        private Dictionary<string, FastDirectoryMember> members;

        #endregion
    }
}