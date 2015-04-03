//     Copyright (c) Microsoft Corporation.  All rights reserved.
// This file is best viewed using outline mode (Ctrl-M Ctrl-O)
// 
// This program uses code hyperlinks available as part of the HyperAddin Visual Studio plug-in.
// It is available from http://www.codeplex.com/hyperAddin 
// #define DEBUG_SERIALIZE
using System;
using System.Collections.Generic;
using System.ComponentModel; // For Win32Excption;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using Address = System.UInt64;

namespace Microsoft.Diagnostics.Runtime.Utilities
{
    /// <summary>
    /// SymPath is a class that knows how to parse _NT_SYMBOL_PATH syntax.  
    /// </summary>
    public class SymPath
    {
        /// <summary>
        /// This is the _NT_SYMBOL_PATH exposed as a SymPath type setting this sets the environment variable.
        /// If you only set _NT_SYMBOL_PATH through the SymPath class, everything stays in sync. 
        /// </summary>
        public static SymPath SymbolPath
        {
            get
            {
                if (m_SymbolPath == null)
                    m_SymbolPath = new SymPath(_NT_SYMBOL_PATH);
                return m_SymbolPath;
            }
            set
            {
                _NT_SYMBOL_PATH = value.ToString();
                m_SymbolPath = value;
            }
        }
        /// <summary>
        /// This allows you to set the _NT_SYMBOL_PATH as a string.  
        /// </summary>
        public static string _NT_SYMBOL_PATH
        {
            get
            {
                var ret = Environment.GetEnvironmentVariable("_NT_SYMBOL_PATH");
                if (ret == null)
                    ret = "";
                return ret;
            }
            set
            {
                m_SymbolPath = null;
                Environment.SetEnvironmentVariable("_NT_SYMBOL_PATH", value);
            }
        }
        /// <summary>
        /// This 'cleans up' a symbol path.  In particular
        /// Empty ones are replaced with good defaults (symweb or msdl)
        /// All symbol server specs have local caches (%Temp%\symbols if nothing else is specified).  
        /// 
        /// Note that this routine does NOT update _NT_SYMBOL_PATH.  
        /// </summary>
        public static SymPath CleanSymbolPath()
        {
            string symPathStr = _NT_SYMBOL_PATH;
            if (symPathStr.Length == 0)
                symPathStr = MicrosoftSymbolServerPath;
            var symPath = new SymPath(symPathStr);
            return symPath.InsureHasCache(symPath.DefaultSymbolCache).CacheFirst();
        }

        /// <summary>
        /// Return the string representing a symbol path for the 'standard' microsoft symbol servers.   
        /// This returns the public msdl.microsoft.com server if outside Microsoft.  
        /// </summary>
        public static string MicrosoftSymbolServerPath
        {
            get
            {
                if (s_MicrosoftSymbolServerPath == null)
                {
#if INTERNAL_ONLY
                    if (ComputerNameExists("symweb.corp.microsoft.com"))
                        s_MicrosoftSymbolServerPath = "SRV*http://symweb.corp.microsoft.com";   // Internal Microsoft location.  
                    else
                        s_MicrosoftSymbolServerPath = "SRV*http://referencesource.microsoft.com/symbols;SRV*http://msdl.microsoft.com/download/symbols";

                    // TODO Is this a hack? 
                    if (SymPath.ComputerNameExists("ddrps.corp.microsoft.com"))
                        s_MicrosoftSymbolServerPath = s_MicrosoftSymbolServerPath + ";" + @"SRV*\\ddrps.corp.microsoft.com\symbols";
                    if (SymPath.ComputerNameExists("clrmain"))
                        s_MicrosoftSymbolServerPath = s_MicrosoftSymbolServerPath + ";" + @"SRV*\\clrmain\symbols";
#else
                    s_MicrosoftSymbolServerPath = "SRV*http://referencesource.microsoft.com/symbols;SRV*http://msdl.microsoft.com/download/symbols";
#endif
                }
                return s_MicrosoftSymbolServerPath;
            }
        }

        /// <summary>
        /// Create an empty symbol path
        /// </summary>
        public SymPath()
        {
            m_elements = new List<SymPathElement>();
        }
        /// <summary>
        /// Create a symbol that represents 'path' (the standard semicolon separated list of locations)
        /// </summary>
        public SymPath(string path)
            : this()
        {
            Add(path);
        }
        /// <summary>
        /// Returns the List of elements in the symbol path. 
        /// </summary>
        public ICollection<SymPathElement> Elements
        {
            get { return m_elements; }
        }

        public void Set(string path)
        {
            Clear();
            Add(path);
        }

        public void Clear()
        {
            m_elements.Clear();
        }

        /// <summary>
        /// Append all the elements in the semicolon separated list, 'path', to the symbol path represented by 'this'. 
        /// </summary>
        public void Add(string path)
        {
            if (path == null)
                return;
            path = path.Trim();
            if (path.Length == 0)
                return;
            var strElems = path.Split(';');
            foreach (var strElem in strElems)
                Add(new SymPathElement(strElem));
        }
        /// <summary>
        /// append a new symbol path element to the begining of the symbol path represented by 'this'.
        /// </summary>
        public void Add(SymPathElement elem)
        {
            if (elem != null && !m_elements.Contains(elem))
                m_elements.Add(elem);
        }

        /// <summary>
        /// insert all the elements in the semicolon separated list, 'path' to the begining of the symbol path represented by 'this'.
        /// </summary>
        public void Insert(string path)
        {
            var strElems = path.Split(';');
            foreach (var strElem in strElems)
                Insert(new SymPathElement(strElem));
        }
        /// <summary>
        /// insert a new symbol path element to the begining of the symbol path represented by 'this'.
        /// </summary>
        public void Insert(SymPathElement elem)
        {
            if (elem != null)
            {
                var existing = m_elements.IndexOf(elem);
                if (existing >= 0)
                    m_elements.RemoveAt(existing);
                m_elements.Insert(0, elem);
            }
        }

        /// <summary>
        /// If you need to cache files locally, put them here.  It is defined
        /// to be the first local path of a SRV* qualification or %TEMP%\symbols
        /// if not is present.
        /// </summary>
        public string DefaultSymbolCache
        {
            get
            {
                foreach (var elem in Elements)
                {
                    if (elem.IsSymServer)
                    {
                        if (elem.Cache != null)
                            return elem.Cache;
                        else if (!elem.IsRemote)
                            return elem.Target;
                    }
                }
                string temp = Environment.GetEnvironmentVariable("TEMP");
                if (temp == null)
                    temp = ".";
                return Path.Combine(temp, "symbols");
            }
        }
        /// <summary>
        /// People can use symbol servers without a local cache.  This is bad, add one if necessary. 
        /// </summary>
        public SymPath InsureHasCache(string defaultCachePath)
        {
            var ret = new SymPath();
            foreach (var elem in Elements)
                ret.Add(elem.InsureHasCache(defaultCachePath));
            return ret;
        }
        /// <summary>
        /// Removes all references to remote paths.  This insures that network issues don't cause grief.  
        /// </summary>
        public SymPath LocalOnly()
        {
            var ret = new SymPath();
            foreach (var elem in Elements)
                ret.Add(elem.LocalOnly());
            return ret;
        }
        /// <summary>
        /// Create a new symbol path which first search all machine local locations (either explicit location or symbol server cache locations)
        /// folloed by all non-local symbol server.   This produces better behavior (If you can find it locally it will be fast)
        /// </summary>
        public SymPath CacheFirst()
        {
            var ret = new SymPath();
            foreach (var elem in Elements)
            {
                if (elem.IsSymServer && elem.IsRemote)
                    continue;
                ret.Add(elem);
            }
            foreach (var elem in Elements)
            {
                if (elem.IsSymServer && elem.IsRemote)
                    ret.Add(elem);
            }
            return ret;
        }

        /// <summary>
        /// returns the string representation (semicolon separated) for the symbol path.  
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            bool first = true;
            foreach (var elem in Elements)
            {
                if (!first)
                    sb.Append(";");
                first = false;
                sb.Append(elem.ToString());
            }
            return sb.ToString();
        }

        /// <summary>
        /// Checks to see 'computerName' exists (there is a Domain Names Service (DNS) reply to it)
        /// This routine times out relative quickly (after 700 msec) if there is a problem reaching 
        /// the computer, and returns false.  
        /// </summary>
        public static bool ComputerNameExists(string computerName, int timeoutMSec = 700)
        {
            if (computerName == s_lastComputerNameLookupFailure)
                return false;
            try
            {
                System.Net.IPHostEntry ipEntry = null;
                var result = System.Net.Dns.BeginGetHostEntry(computerName, null, null);
                if (result.AsyncWaitHandle.WaitOne(timeoutMSec))
                    ipEntry = System.Net.Dns.EndGetHostEntry(result);
                if (ipEntry != null)
                    return true;
            }
            catch (Exception) { }
            s_lastComputerNameLookupFailure = computerName;
            return false;
        }
        #region private
        private List<SymPathElement> m_elements;
        private static string s_lastComputerNameLookupFailure = "";
        private static string s_MicrosoftSymbolServerPath;

        private static SymPath m_SymbolPath;
        #endregion
    }

    /// <summary>
    /// SymPathElement represents the text between the semicolons in a symbol path.  It can be a symbol server specification or a simple directory path. 
    /// 
    /// SymPathElement follows functional conventions.  After construction everything is read-only. 
    /// </summary>
    public class SymPathElement 
    {
        /// <summary>
        /// returns true if this element of the symbol server path a symbol server specification
        /// </summary>
        public bool IsSymServer { get; private set; }
        /// <summary>
        /// returns the local cache for a symbol server specifcation.  returns null if not specified
        /// </summary>
        public string Cache { get; private set; }
        /// <summary>
        /// returns location to look for symbols.  This is either a directory specification or an URL (for symbol servers)
        /// </summary>
        public string Target { get; private set; }

        /// <summary>
        /// IsRemote returns true if it looks like the target is not on the local machine.
        /// </summary>
        public bool IsRemote
        {
            get
            {
                if (Target != null)
                {
                    if (Target.StartsWith(@"\\"))
                        return true;
                    // We assume drive letters from the back of the alphabet are remote.  
                    if (2 <= Target.Length && Target[1] == ':')
                    {
                        char driveLetter = Char.ToUpperInvariant(Target[0]);
                        if ('T' <= driveLetter && driveLetter <= 'Z')
                            return true;
                    }
                }

                if (!IsSymServer)
                    return false;
                if (Cache != null)
                    return true;
                if (Target == null)
                    return false;

                if (Target.StartsWith("http:/", StringComparison.OrdinalIgnoreCase))
                    return true;
                return false;
            }
        }
        /// <summary>
        /// returns the string repsentation for the symbol server path element (e.g. SRV*c:\temp*\\symbols\symbols)
        /// </summary>
        public override string ToString()
        {
            if (IsSymServer)
            {
                var ret = "SRV";
                if (Cache != null)
                    ret += "*" + Cache;
                if (Target != null)
                    ret += "*" + Target;
                return ret;
            }
            else
                return Target;
        }
        #region overrides

        /// <summary>
        /// Implements object interface
        /// </summary>
        public override bool Equals(object obj)
        {
            var asSymPathElem = obj as SymPathElement;
            if (asSymPathElem == null)
                return false;
            return
                Target == asSymPathElem.Target &&
                Cache == asSymPathElem.Cache &&
                IsSymServer == asSymPathElem.IsSymServer;
        }
        /// <summary>
        /// Implements object interface
        /// </summary>
        public override int GetHashCode()
        {
            return Target.GetHashCode();
        }
        #endregion
        #region private
        internal SymPathElement InsureHasCache(string defaultCachePath)
        {
            if (!IsSymServer)
                return this;
            if (Cache != null)
                return this;
            if (Target == defaultCachePath)
                return this;
            return new SymPathElement(true, defaultCachePath, Target);
        }
        internal SymPathElement LocalOnly()
        {
            if (!IsRemote)
                return this;
            if (Cache != null)
                return new SymPathElement(true, null, Cache);
            return null;
        }

        internal SymPathElement(bool isSymServer, string cache, string target)
        {
            IsSymServer = isSymServer;
            Cache = cache;
            Target = target;
        }
        internal SymPathElement(string strElem)
        {
            var m = Regex.Match(strElem, @"^\s*(SRV\*|http:)((\s*.*?\s*)\*)?\s*(.*?)\s*$", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                IsSymServer = true;
                Cache = m.Groups[3].Value;
                if (m.Groups[1].Value.Equals("http:", StringComparison.CurrentCultureIgnoreCase))
                    Target = "http:" + m.Groups[4].Value;
                else
                    Target = m.Groups[4].Value;
                if (Cache.Length == 0)
                    Cache = null;
                if (Target.Length == 0)
                    Target = null;
                return;
            }
            m = Regex.Match(strElem, @"^\s*CACHE\*(.*?)\s*$", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                IsSymServer = true;
                Cache = m.Groups[1].Value;
            }
            else
                Target = strElem.Trim();
        }
        #endregion
    }

    #region private classes
    internal unsafe class SymbolReaderNativeMethods
    {
        /**
        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern int GetCurrentProcessId();

        [DllImport("kernel32.dll",  SetLastError = true)]
        public static extern IntPtr OpenProcess(int access, bool inherit, int processID);
        **/

        internal const int SSRVOPT_DWORD = 0x0002;
        internal const int SSRVOPT_DWORDPTR = 0x004;
        internal const int SSRVOPT_GUIDPTR = 0x0008;

        [DllImport("dbghelp.dll", CharSet = CharSet.Unicode, SetLastError = true), SuppressUnmanagedCodeSecurityAttribute]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SymFindFileInPathW(
            IntPtr hProcess,
            string searchPath,
            [MarshalAs(UnmanagedType.LPWStr), In] string fileName,
            ref Guid id,
            int two,
            int three,
            int flags,
            [Out]System.Text.StringBuilder filepath,
            SymFindFileInPathProc findCallback,
            IntPtr context // void*
            );

        // Useful for the findCallback parameter of SymFindFileInPathW
        [DllImport("dbghelp.dll", CharSet = CharSet.Unicode, SetLastError = true), SuppressUnmanagedCodeSecurityAttribute]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SymSrvGetFileIndexesW(
            string filePath,
            ref Guid id,
            ref int val1,
            ref int val2,
            int flags);

        [DllImport("dbghelp.dll", CharSet = CharSet.Unicode, SetLastError = true), SuppressUnmanagedCodeSecurityAttribute]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SymInitializeW(
            IntPtr hProcess,
            string UserSearchPath,
            [MarshalAs(UnmanagedType.Bool)] bool fInvadeProcess);

        [DllImport("dbghelp.dll", SetLastError = true), SuppressUnmanagedCodeSecurityAttribute]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SymCleanup(
            IntPtr hProcess);

        [DllImport("dbghelp.dll", CharSet = CharSet.Unicode, SetLastError = true), SuppressUnmanagedCodeSecurityAttribute]
        internal static extern ulong SymLoadModuleExW(
            IntPtr hProcess,
            IntPtr hFile,
            string ImageName,
            string ModuleName,
            ulong BaseOfDll,
            uint DllSize,
            void* Data,
            uint Flags
         );

        [DllImport("dbghelp.dll", SetLastError = true), SuppressUnmanagedCodeSecurityAttribute]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SymUnloadModule64(
            IntPtr hProcess,
            ulong BaseOfDll);

        [DllImport("dbghelp.dll", CharSet = CharSet.Unicode, SetLastError = true), SuppressUnmanagedCodeSecurityAttribute]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SymGetLineFromAddrW64(
            IntPtr hProcess,
            ulong Address,
            ref Int32 Displacement,
            ref IMAGEHLP_LINE64 Line
        );

        [DllImport("dbghelp.dll", CharSet = CharSet.Unicode, SetLastError = true), SuppressUnmanagedCodeSecurityAttribute]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SymFromAddrW(
            IntPtr hProcess,
            ulong Address,
            ref ulong Displacement,
            SYMBOL_INFO* Symbol
        );

        // Some structures used by the callback 
        internal struct IMAGEHLP_CBA_EVENT
        {
            public int Severity;
            public char* pStrDesc;
            public void* pData;
        }
        internal struct IMAGEHLP_DEFERRED_SYMBOL_LOAD64
        {
            public int SizeOfStruct;
            public Int64 BaseOfImage;
            public int CheckSum;
            public int TimeDateStamp;
            public fixed sbyte FileName[MAX_PATH];
            public bool Reparse;
            public void* hFile;
            public int Flags;
        }


        internal struct SYMBOL_INFO
        {
            public UInt32 SizeOfStruct;
            public UInt32 TypeIndex;
            public UInt64 Reserved1;
            public UInt64 Reserved2;
            public UInt32 Index;
            public UInt32 Size;
            public UInt64 ModBase;
            public UInt32 Flags;
            public UInt64 Value;
            public UInt64 Address;
            public UInt32 Register;
            public UInt32 Scope;
            public UInt32 Tag;
            public UInt32 NameLen;
            public UInt32 MaxNameLen;
            public byte Name;           // Actually of variable size Unicode string
        };

        internal struct IMAGEHLP_LINE64
        {
            public UInt32 SizeOfStruct;
            public void* Key;
            public UInt32 LineNumber;
            public byte* FileName;             // pointer to character string. 
            public UInt64 Address;
        };

        [DllImport("dbghelp.dll", CharSet = CharSet.Unicode, SetLastError = true), SuppressUnmanagedCodeSecurityAttribute]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SymRegisterCallbackW64(
            IntPtr hProcess,
            SymRegisterCallbackProc callBack,
            ulong UserContext);

        internal delegate bool SymRegisterCallbackProc(
            IntPtr hProcess,
            SymCallbackActions ActionCode,
            ulong UserData,
            ulong UserContext);


        [Flags]
        public enum SymCallbackActions
        {
            CBA_DEBUG_INFO = 0x10000000,
            CBA_DEFERRED_SYMBOL_LOAD_CANCEL = 0x00000007,
            CBA_DEFERRED_SYMBOL_LOAD_COMPLETE = 0x00000002,
            CBA_DEFERRED_SYMBOL_LOAD_FAILURE = 0x00000003,
            CBA_DEFERRED_SYMBOL_LOAD_PARTIAL = 0x00000020,
            CBA_DEFERRED_SYMBOL_LOAD_START = 0x00000001,
            CBA_DUPLICATE_SYMBOL = 0x00000005,
            CBA_EVENT = 0x00000010,
            CBA_READ_MEMORY = 0x00000006,
            CBA_SET_OPTIONS = 0x00000008,
            CBA_SRCSRV_EVENT = 0x40000000,
            CBA_SRCSRV_INFO = 0x20000000,
            CBA_SYMBOLS_UNLOADED = 0x00000004,
        }

        [Flags]
        public enum SymOptions : uint
        {
            SYMOPT_ALLOW_ABSOLUTE_SYMBOLS = 0x00000800,
            SYMOPT_ALLOW_ZERO_ADDRESS = 0x01000000,
            SYMOPT_AUTO_PUBLICS = 0x00010000,
            SYMOPT_CASE_INSENSITIVE = 0x00000001,
            SYMOPT_DEBUG = 0x80000000,
            SYMOPT_DEFERRED_LOADS = 0x00000004,
            SYMOPT_DISABLE_SYMSRV_AUTODETECT = 0x02000000,
            SYMOPT_EXACT_SYMBOLS = 0x00000400,
            SYMOPT_FAIL_CRITICAL_ERRORS = 0x00000200,
            SYMOPT_FAVOR_COMPRESSED = 0x00800000,
            SYMOPT_FLAT_DIRECTORY = 0x00400000,
            SYMOPT_IGNORE_CVREC = 0x00000080,
            SYMOPT_IGNORE_IMAGEDIR = 0x00200000,
            SYMOPT_IGNORE_NT_SYMPATH = 0x00001000,
            SYMOPT_INCLUDE_32BIT_MODULES = 0x00002000,
            SYMOPT_LOAD_ANYTHING = 0x00000040,
            SYMOPT_LOAD_LINES = 0x00000010,
            SYMOPT_NO_CPP = 0x00000008,
            SYMOPT_NO_IMAGE_SEARCH = 0x00020000,
            SYMOPT_NO_PROMPTS = 0x00080000,
            SYMOPT_NO_PUBLICS = 0x00008000,
            SYMOPT_NO_UNQUALIFIED_LOADS = 0x00000100,
            SYMOPT_OVERWRITE = 0x00100000,
            SYMOPT_PUBLICS_ONLY = 0x00004000,
            SYMOPT_SECURE = 0x00040000,
            SYMOPT_UNDNAME = 0x00000002,
        };

        [DllImport("dbghelp.dll", SetLastError = true), SuppressUnmanagedCodeSecurityAttribute]
        internal static extern SymOptions SymSetOptions(
            SymOptions SymOptions
            );

        [DllImport("dbghelp.dll", SetLastError = true), SuppressUnmanagedCodeSecurityAttribute]
        internal static extern SymOptions SymGetOptions();

        internal delegate bool SymFindFileInPathProc([MarshalAs(UnmanagedType.LPWStr), In] string fileName, IntPtr context);

        internal const int MAX_PATH = 260;

        // Src Server API
        [DllImport("dbghelp.dll", CharSet = CharSet.Unicode, SetLastError = true), SuppressUnmanagedCodeSecurityAttribute]
        internal static extern bool SymGetSourceFileW(
            IntPtr hProcess,
            ulong ImageBase,
            IntPtr Params,
            string fileSpec,
            StringBuilder filePathRet,
            int filePathRetSize);

        [DllImport("dbghelp.dll", CharSet = CharSet.Unicode, SetLastError = true), SuppressUnmanagedCodeSecurityAttribute]
        internal static extern IntPtr SymSetHomeDirectoryW(
             IntPtr hProcess,
             string dir);
    }
    #endregion
}
