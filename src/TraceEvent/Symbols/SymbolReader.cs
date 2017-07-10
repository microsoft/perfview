using System;
using System.Collections.Generic;
using System.ComponentModel; // For Win32Excption;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Diagnostics.Symbols;
using Dia2Lib;
using Address = System.UInt64;
using Utilities;
using System.Reflection;
using Microsoft.Diagnostics.Utilities;
using System.Threading.Tasks;
using System.Threading;
using System.Net;

namespace Microsoft.Diagnostics.Symbols
{
    /// <summary>
    /// A symbol reader represents something that can FIND pdbs (either on a symbol server or via a symbol path)
    /// Its job is to find a full path a PDB.  Then you can use OpenSymbolFile to get a SymbolReaderModule and do more. 
    /// </summary>
    public sealed unsafe class SymbolReader : IDisposable
    {
        /// <summary>
        /// Opens a new SymbolReader.   All diagnostics messages about symbol lookup go to 'log'.  
        /// </summary>
        public SymbolReader(TextWriter log, string nt_symbol_path = null)
        {
            this.m_log = log;
            this.m_symbolModuleCache = new Cache<string, SymbolModule>(10);
            this.m_pdbPathCache = new Cache<PdbSignature, string>(10);

            m_symbolPath = nt_symbol_path;
            if (m_symbolPath == null)
                m_symbolPath = Microsoft.Diagnostics.Symbols.SymbolPath.SymbolPathFromEnvironment;
            log.WriteLine("Created SymbolReader with SymbolPath {0}", m_symbolPath);

            // TODO FIX NOW.  the code below does not support probing a file extension directory.  
            // we work around this by adding more things to the symbol path
            var symPath = new SymbolPath(SymbolPath);
            var newSymPath = new SymbolPath();
            foreach (var symElem in symPath.Elements)
            {
                newSymPath.Add(symElem);
                if (!symElem.IsSymServer)
                {
                    var probe = Path.Combine(symElem.Target, "dll");
                    if (Directory.Exists(probe))
                        newSymPath.Add(probe);
                    probe = Path.Combine(symElem.Target, "exe");
                    if (Directory.Exists(probe))
                        newSymPath.Add(probe);
                }
            }
            var newSymPathStr = newSymPath.ToString();
            m_symbolPath = newSymPathStr;
        }

        // These routines find a PDB based on something (either an DLL or a PDB 'signature')
        /// <summary>
        /// Finds the symbol file for 'exeFilePath' that exists on the current machine (we open
        /// it to find the needed info).   Uses the SymbolReader.SymbolPath (including Symbol servers) to 
        /// look up the PDB, and will download the PDB to the local cache if necessary.   It will also
        /// generate NGEN pdbs into the local symbol cache unless SymbolReaderFlags.NoNGenPDB is set.   
        /// 
        /// By default for NGEN images it returns the NGEN pdb.  However if 'ilPDB' is true it returns
        /// the IL PDB.  
        /// 
        /// Returns null if the pdb can't be found.  
        /// </summary>
        public string FindSymbolFilePathForModule(string dllFilePath, bool ilPDB = false)
        {
            m_log.WriteLine("FindSymbolFilePathForModule: searching for PDB for DLL {0}.", dllFilePath);
            try
            {
                dllFilePath = BypassSystem32FileRedirection(dllFilePath);
                if (File.Exists(dllFilePath))
                {
                    using (var peFile = new PEFile.PEFile(dllFilePath))
                    {
                        string pdbName;
                        Guid pdbGuid;
                        int pdbAge;
                        if (peFile.GetPdbSignature(out pdbName, out pdbGuid, out pdbAge, !ilPDB))
                        {
                            string fileVersionString = null;
                            var fileVersion = peFile.GetFileVersionInfo();
                            if (fileVersion != null)
                                fileVersionString = fileVersion.FileVersion;

                            var ret = FindSymbolFilePath(pdbName, pdbGuid, pdbAge, dllFilePath, fileVersionString);
                            if (ret == null && (0 <= dllFilePath.IndexOf(".ni.", StringComparison.OrdinalIgnoreCase) || peFile.IsManagedReadyToRun))
                            {
                                if ((Options & SymbolReaderOptions.NoNGenSymbolCreation) != 0)
                                    m_log.WriteLine("FindSymbolFilePathForModule: Could not find NGEN image, NoNGenPdb set, giving up.");
                                else
                                {
                                    m_log.WriteLine("FindSymbolFilePathForModule: Could not find PDB for NGEN image, Trying to generate it.");
                                    ret = GenerateNGenSymbolsForModule(Path.GetFullPath(dllFilePath));
                                }
                            }
                            m_log.WriteLine("FindSymbolFilePathForModule returns {0} for {1} {2} {3} {4}", ret ?? "NULL", pdbName, pdbGuid, pdbAge, fileVersionString ?? "NULL");
                            return ret;
                        }
                        else
                            m_log.WriteLine("FindSymbolFilePathForModule: {0} does not have a codeview debug signature.", dllFilePath);
                    }
                }
                else
                    m_log.WriteLine("FindSymbolFilePathForModule: {0} does not exist.", dllFilePath);
            }
            catch (Exception e)
            {
                m_log.WriteLine("FindSymbolFilePathForModule: Failure opening PE file: {0}", e.Message);
            }

            m_log.WriteLine("[Failed to find PDB file for DLL {0}]", dllFilePath);
            return null;
        }
        /// <summary>
        /// Find the complete PDB path, given just the simple name (filename + pdb extension) as well as its 'signature', 
        /// which uniquely identifies it (on symbol servers).   Uses the SymbolReader.SymbolPath (including Symbol servers) to 
        /// look up the PDB, and will download the PDB to the local cache if necessary.  
        /// 
        /// A Guid of Empty, means 'unknown' and will match the first PDB that matches simple name.  Thus it is unsafe. 
        /// 
        /// Returns null if the PDB could  not be found
        /// </summary>
        /// <param name="pdbFileName">The name of the PDB file (we only use the file name part)</param>
        /// <param name="pdbIndexGuid">The GUID that is embedded in the DLL in the debug information that allows matching the DLL and the PDB</param>
        /// <param name="pdbIndexAge">Tools like BBT transform a DLL into another DLL (with the same GUID) the 'pdbAge' is a small integers
        /// that indicates how many transformations were done</param>
        /// <param name="dllFilePath">If you know the path to the DLL for this pdb add it here.  That way we can probe next to the DLL
        /// for the PDB file.</param>
        /// <param name="fileVersion">This is an optional string that identifies the file version (the 'Version' resource information.  
        /// It is used only to provided better error messages for the log.</param>
        public string FindSymbolFilePath(string pdbFileName, Guid pdbIndexGuid, int pdbIndexAge, string dllFilePath = null, string fileVersion = "")
        {
            PdbSignature pdbSig = new PdbSignature() { Name = pdbFileName, ID = pdbIndexGuid, Age = pdbIndexAge };
            string pdbPath = null;
            if (m_pdbPathCache.TryGet(pdbSig, out pdbPath))
                return pdbPath;

            m_log.WriteLine("FindSymbolFilePath: *{{ Locating PDB {0} GUID {1} Age {2} Version {3}", pdbFileName, pdbIndexGuid, pdbIndexAge, fileVersion);
            if (dllFilePath != null)
                m_log.WriteLine("FindSymbolFilePath: Pdb is for DLL {0}", dllFilePath);

            string pdbIndexPath = null;
            string pdbSimpleName = Path.GetFileName(pdbFileName);        // Make sure the simple name is really a simple name

            // If we have a dllPath, look right beside it, or in a directory symbols.pri\retail\dll
            if (pdbPath == null && dllFilePath != null)        // Check next to the file. 
            {
                m_log.WriteLine("FindSymbolFilePath: Checking relative to DLL path {0}", dllFilePath);
                string pdbPathCandidate = Path.Combine(Path.GetDirectoryName(dllFilePath), Path.GetFileName(pdbFileName)); 
                if (PdbMatches(pdbPathCandidate, pdbIndexGuid, pdbIndexAge))
                    pdbPath = pdbPathCandidate;

                // Also try the symbols.pri\retail\dll convention that windows and devdiv use
                if (pdbPath == null)
                {
                    pdbPathCandidate = Path.Combine(
                        Path.GetDirectoryName(dllFilePath), @"symbols.pri\retail\dll\" +
                        Path.GetFileName(pdbFileName));
                    if (PdbMatches(pdbPathCandidate, pdbIndexGuid, pdbIndexAge))
                        pdbPath = pdbPathCandidate;
                }

                if (pdbPath == null)
                {
                    pdbPathCandidate = Path.Combine(
                        Path.GetDirectoryName(dllFilePath), @"symbols\retail\dll\" +
                        Path.GetFileName(pdbFileName));
                    if (PdbMatches(pdbPathCandidate, pdbIndexGuid, pdbIndexAge))
                        pdbPath = pdbPathCandidate;
                }
            }

            // If the pdbPath is a full path, see if it exists 
            if (pdbPath == null && 0 < pdbFileName.IndexOf('\\'))
            {
                if (PdbMatches(pdbFileName, pdbIndexGuid, pdbIndexAge))
                    pdbPath = pdbFileName;
            }

            // Did not find it locally, 
            if (pdbPath == null)
            {
                SymbolPath path = new SymbolPath(this.SymbolPath);
                foreach (SymbolPathElement element in path.Elements)
                {
                    // TODO can do all of these concurrently now.   
                    if (element.IsSymServer)
                    {
                        if (pdbIndexPath == null)
                            // symbolsource.org and nuget.smbsrc.net only support upper case of pdbIndexGuid
                            pdbIndexPath = pdbSimpleName + @"\" + pdbIndexGuid.ToString("N").ToUpper() + pdbIndexAge.ToString() + @"\" + pdbSimpleName;
                        string cache = element.Cache;
                        if (cache == null)
                            cache = path.DefaultSymbolCache();

                        pdbPath = GetFileFromServer(element.Target, pdbIndexPath, Path.Combine(cache, pdbIndexPath));
                    }
                    else
                    {
                        string filePath = Path.Combine(element.Target, pdbSimpleName);
                        if ((Options & SymbolReaderOptions.CacheOnly) == 0 || !element.IsRemote)
                        {
                            // TODO can stall if the path is a remote path.   
                            if (PdbMatches(filePath, pdbIndexGuid, pdbIndexAge, false))
                                pdbPath = filePath;
                        }
                        else
                            m_log.WriteLine("FindSymbolFilePath: location {0} is remote and cacheOnly set, giving up.", filePath);
                    }
                    if (pdbPath != null)
                        break;
                }
            }

            if (pdbPath != null)
            {
                if (OnSymbolFileFound != null)
                    OnSymbolFileFound(pdbPath, pdbIndexGuid, pdbIndexAge);
                this.m_log.WriteLine("FindSymbolFilePath: *}} Successfully found PDB {0} GUID {1} Age {2} Version {3}", pdbPath, pdbIndexGuid, pdbIndexAge, fileVersion);
            }
            else
            {
                string where = "";
                if ((Options & SymbolReaderOptions.CacheOnly) != 0)
                    where = " in local cache";
                m_log.WriteLine("FindSymbolFilePath: *}} Failed to find PDB {0}{1} GUID {2} Age {3} Version {4}", pdbSimpleName, where, pdbIndexGuid, pdbIndexAge, fileVersion);
            }

            m_pdbPathCache.Add(pdbSig, pdbPath);
            return pdbPath;
        }

        // Find an executable file path (not a PDB) based on information about the file image.  
        /// <summary>
        /// This API looks up an executable file, by its build-timestamp and size (on a symbol server),  'fileName' should be 
        /// a simple name (no directory), and you need the buildTimeStamp and sizeOfImage that are found in the PE header.
        /// 
        /// Returns null if it cannot find anything.  
        /// </summary>
        public string FindExecutableFilePath(string fileName, int buildTimestamp, int sizeOfImage, bool sybmolServerOnly = false)
        {
            string exeIndexPath = null;
            SymbolPath path = new SymbolPath(this.SymbolPath);
            foreach (SymbolPathElement element in path.Elements)
            {
                if (element.IsSymServer)
                {
                    if (exeIndexPath == null)
                        exeIndexPath = fileName + @"\" + buildTimestamp.ToString("x") + sizeOfImage.ToString("x") + @"\" + fileName;

                    string cache = element.Cache;
                    if (cache == null)
                        cache = path.DefaultSymbolCache();

                    string targetPath = GetFileFromServer(element.Target, exeIndexPath, Path.Combine(cache, exeIndexPath));
                    if (targetPath != null)
                        return targetPath;
                }
                else if (!sybmolServerOnly)
                {
                    string filePath = Path.Combine(element.Target, fileName);
                    m_log.WriteLine("Probing file {0}", filePath);
                    if (File.Exists(filePath))
                    {
                        using (PEFile.PEFile file = new PEFile.PEFile(filePath))
                        {
                            if ((file.Header.TimeDateStampSec == buildTimestamp) && (file.Header.SizeOfImage == sizeOfImage))
                                return filePath;
                            m_log.WriteLine("Found file {0} but file timestamp:size {1}:{2} != desired {3}:{4}, rejecting.",
                                filePath, file.Header.TimeDateStampSec, file.Header.SizeOfImage, buildTimestamp, sizeOfImage);
                        }
                    }
                }
            }
            return null;
        }

        // Once you have a file path to a PDB file, you can open it with this method
        /// <summary>
        /// Given the path name to a particular PDB file, load it so that you can resolve symbols in it.  
        /// </summary>
        /// <param name="pdbFilePath">The name of the PDB file to open.</param>
        /// <returns>The SymbolReaderModule that represents the information in the symbol file (PDB)</returns>
        public SymbolModule OpenSymbolFile(string pdbFilePath)
        {
            SymbolModule ret;
            if (!m_symbolModuleCache.TryGet(pdbFilePath, out ret))
            {
                ret = new SymbolModule(this, pdbFilePath);
                m_symbolModuleCache.Add(pdbFilePath, ret);
            }
            return ret;
        }

        /// <summary>
        /// Loads symbols from a Stream.
        /// </summary>
        public SymbolModule OpenSymbolFile(string pdbFilePath, Stream pdbStream)
        {
            return new SymbolModule(this, pdbFilePath, pdbStream);
        }

        // Various state that controls symbol and source file lookup.  
        /// <summary>
        /// The symbol path used to look up PDB symbol files.   Set when the reader is initialized.  
        /// </summary>
        public string SymbolPath
        {
            get { return m_symbolPath; }
            set
            {
                m_symbolPath = value;
                m_symbolModuleCache.Clear();
                m_pdbPathCache.Clear();
                m_log.WriteLine("Symbol Path Updated to {0}", m_symbolPath);
            }
        }
        /// <summary>
        /// The paths used to look up source files.  defaults to _NT_SOURCE_PATH.  
        /// </summary>
        public string SourcePath
        {
            get
            {
                if (m_SourcePath == null)
                {
                    m_SourcePath = Environment.GetEnvironmentVariable("_NT_SOURCE_PATH");
                    if (m_SourcePath == null)
                        m_SourcePath = "";
                }
                return m_SourcePath;
            }
            set
            {
                m_SourcePath = value;
                m_parsedSourcePath = null;
            }
        }
        /// <summary>
        /// Where symbols are downloaded if needed.   Derived from symbol path.  It is the first
        /// directory on the local machine in a SRV*DIR*LOC spec, and %TEMP%\Symbols otherwise.  
        /// </summary>
        public string SymbolCacheDirectory
        {
            get
            {
                if (m_SymbolCacheDirectory == null)
                    m_SymbolCacheDirectory = new SymbolPath(SymbolPath).DefaultSymbolCache();
                return m_SymbolCacheDirectory;
            }
        }
        /// <summary>
        /// The place where source is downloaded from a source server.  
        /// </summary>
        public string SourceCacheDirectory
        {
            get
            {
                if (m_SourceCacheDirectory == null)
                    m_SourceCacheDirectory = Path.Combine(Environment.GetEnvironmentVariable("TEMP"), "SrcCache");
                return m_SourceCacheDirectory;
            }
            set
            {
                m_SourceCacheDirectory = value;
            }
        }
        /// <summary>
        /// Is this symbol reader limited to just the local machine cache or not?
        /// </summary>
        public SymbolReaderOptions Options { get; set; }
        /// <summary>
        /// We call back on this when we find a PDB by probing in 'unsafe' locations (like next to the EXE or in the Built location)
        /// If this function returns true, we assume that it is OK to use the PDB.  
        /// </summary>
        public Func<string, bool> SecurityCheck { get; set; }

        /// <summary>
        /// If set OnSymbolFileFound will be called when a PDB file is found.  
        /// It is passed the complete local file path, the PDB Guid (may be Guid.Empty) and PDB age.
        /// </summary>
        public event Action<string, Guid, int> OnSymbolFileFound;

        /// <summary>
        /// A place to log additional messages 
        /// </summary>
        public TextWriter Log { get { return m_log; } }

        /// <summary>
        /// Given a full filename path to an NGEN image, insure that there is an NGEN image for it
        /// in the symbol cache.  If one already exists, this method simply returns that.   If not
        /// it is generated and placed in the symbol cache.  When generating the PDB this routine
        /// attempt to resolve line numbers, which DOES require looking up the PDB for the IL image. 
        /// Thus routine may do network accesses (to download IL PDBs).  
        /// 
        /// Note that FindSymbolFilePathForModule calls this, so normally you don't need to call 
        /// this method directly.  
        /// 
        /// By default it places the PDB in the SymbolCacheDirectory using normal symbol server 
        /// cache conventions (PDBNAME\Guid-AGE\Name).   You can override this by specifying
        /// the outputDirectory parameter.  
        /// 
        /// <returns>The full path name of the PDB generated for the NGEN image.</returns>
        /// </summary>
        public string GenerateNGenSymbolsForModule(string ngenImageFullPath, string outputDirectory = null)
        {
            if (outputDirectory == null)
                outputDirectory = SymbolCacheDirectory;

            if (!File.Exists(ngenImageFullPath))
            {
                m_log.WriteLine("Warning, NGEN image does not exist: {0}", ngenImageFullPath);
                return null;
            }

            // When V4.5 shipped, NGEN CreatePdb did not support looking up the IL pdb using symbol servers.  
            // We work around by explicitly fetching the IL PDB and pointing NGEN CreatePdb at that.  
            string ilPdbName = null;
            Guid ilPdbGuid = Guid.Empty;
            int ilPdbAge = 0;

            string pdbFileName;
            Guid pdbGuid;
            int pdbAge;
            using (var peFile = new PEFile.PEFile(ngenImageFullPath))
            {
                if (!peFile.GetPdbSignature(out pdbFileName, out pdbGuid, out pdbAge, true))
                {
                    m_log.WriteLine("Could not get PDB signature for {0}", ngenImageFullPath);
                    return null;
                }

                // Also get the IL pdb information (can rip out when we don't care about source code for pre V4.6 runtimes)
                peFile.GetPdbSignature(out ilPdbName, out ilPdbGuid, out ilPdbAge, false);
            }

            // Fast path, the file already exists.
            pdbFileName = Path.GetFileName(pdbFileName);
            string relDirPath = pdbFileName + "\\" + pdbGuid.ToString("N") + pdbAge.ToString();
            string pdbDir = Path.Combine(outputDirectory, relDirPath);
            var pdbPath = Path.Combine(pdbDir, pdbFileName);
            if (File.Exists(pdbPath))
                return pdbPath;

            // We only handle cases where we generate NGEN pdbs.  
            if (!pdbPath.EndsWith(".ni.pdb", StringComparison.OrdinalIgnoreCase))
                return null;

            string privateRuntimeVerString;
            var clrDir = GetClrDirectoryForNGenImage(ngenImageFullPath, m_log, out privateRuntimeVerString);
            if (clrDir == null)
                return HandleNetCorePdbs(ngenImageFullPath, pdbPath);

            // See if this is a V4.5 CLR, if so we can do line numbers too.l  
            var lineNumberArg = "";
            var ngenexe = Path.Combine(clrDir, "ngen.exe");
            m_log.WriteLine("Checking for V4.5 for NGEN image {0}", ngenexe);
            if (!File.Exists(ngenexe))
                return null;
            var isV4_5Runtime = false;

            Match m;
            using (var peFile = new PEFile.PEFile(ngenexe))
            {
                var fileVersionInfo = peFile.GetFileVersionInfo();
                if (fileVersionInfo != null)
                {
                    var clrFileVersion = fileVersionInfo.FileVersion;
                    m_log.WriteLine("Got NGEN image file version number: {0}", clrFileVersion);

                    m = Regex.Match(clrFileVersion, @"(\d+).(\d+)((\d|\.)*)");
                    if (m.Success)
                    {
                        var majorVersion = int.Parse(m.Groups[1].Value);
                        var minorVersion = int.Parse(m.Groups[2].Value);
                        var majorMinor = majorVersion * 10 + minorVersion;
                        if (majorMinor >= 46)
                        {
                            m_log.WriteLine("Is a V4.6 or beyond");
                            isV4_5Runtime = true;
                        }
                        else if (majorMinor == 40)
                        {
                            // 4.0.30319.16000 == V4.5 We need a build number >= 16000) to be a V4.5 runtime.  
                            m = Regex.Match(m.Groups[3].Value, @"(\d+)$");
                            if (m.Success && int.Parse(m.Groups[1].Value) >= 16000)
                                isV4_5Runtime = true;
                        }
                    }

#if Skip_Symbol_Lookup_At_Collection_Time
                    // Symbol lookup is not required at collection time in .Net Framework 4.6.1 and beyond
#else
                    // TODO FIX NOW:  In V4.6.1 of the runtime we no longer need /lines to get line number 
                    // information (the native to IL mapping is always put in the NGEN image and that
                    // is sufficient to look up line numbers later (not at NGEN pdb creation time).  
                    // Thus this code could be removed once we really don't care about the case where
                    // it is a V4.5.* runtime but not a V4.6.1+ runtime AND we care about line numbers.  
                    // After 12/2016 we can probably pull this code.  
                    if (isV4_5Runtime)
                    {
                        m_log.WriteLine("Is a V4.5 Runtime or beyond");
                        if (ilPdbName != null)
                        {
                            var ilPdbPath = this.FindSymbolFilePath(ilPdbName, ilPdbGuid, ilPdbAge);
                            if (ilPdbPath != null)
                                lineNumberArg = "/lines " + Command.Quote(Path.GetDirectoryName(ilPdbPath));
                            else
                                m_log.WriteLine("Could not find IL PDB {0} Guid {1} Age {2}.", ilPdbName, ilPdbGuid, ilPdbAge);
                        }
                        else
                            m_log.WriteLine("NGEN image did not have IL PDB information, giving up on line number info.");
                    }
#endif
                }
            }

            var options = new CommandOptions();
            options.AddEnvironmentVariable("_NT_SYMBOL_PATH", this.SymbolPath);
            options.AddOutputStream(m_log);
            options.AddNoThrow();

            options.AddEnvironmentVariable("COMPLUS_NGenEnableCreatePdb", "1");

            // NGenLocalWorker is needed for V4.0 runtimes but interferes on V4.5 runtimes.  
            if (!isV4_5Runtime)
                options.AddEnvironmentVariable("COMPLUS_NGenLocalWorker", "1");
            var newPath = "%PATH%;" + clrDir;
            options.AddEnvironmentVariable("PATH", newPath);

            // For Win8 Store Auto-NGEN images we need to use a location where the app can write the PDB file
            var outputPdbPath = pdbPath;
            var ngenOutputDirectory = outputDirectory;

            // Find the tempDir where we can write.  
            string tempDir = null;
            m = Regex.Match(ngenImageFullPath, @"(.*)\\Microsoft\\CLR_v(\d+)\.\d+(_(\d\d))?\\NativeImages", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                tempDir = Path.Combine(m.Groups[1].Value, @"Temp\NGenPdb");
                DirectoryUtilities.Clean(tempDir);
                Directory.CreateDirectory(tempDir);
                ngenOutputDirectory = tempDir;
                outputPdbPath = Path.Combine(tempDir, relDirPath, pdbFileName);
                m_log.WriteLine("Updating NGEN createPdb output file to {0}", outputPdbPath); // TODO FIX NOW REMOVE (for debugging)
            }

            // TODO: Hack.   V4.6.1 has both these characteristics, which leads to the issue
            //      1) NGEN CreatePDB requires the path to be in the NIC or it fails. 
            //      2) It uses links to some NGEN images, which means that the OS may give you path that is not in the NIC.  
            // Should be fixed by 12/2015
            if (isV4_5Runtime)
                InsurePathIsInNIC(m_log, ref ngenImageFullPath);

            try
            {
                for (;;) // Loop for retrying without /lines 
                {
                    if (!string.IsNullOrEmpty(privateRuntimeVerString))
                    {
                        m_log.WriteLine("Ngen will run for private runtime ", privateRuntimeVerString);
                        m_log.WriteLine("set COMPLUS_Version=" + privateRuntimeVerString);
                        options.AddEnvironmentVariable("COMPLUS_Version", privateRuntimeVerString);
                    }
                    // TODO FIX NOW: there is a and ugly problem with persistence of suboptimal PDB files
                    // This is made pretty bad because the not finding the IL PDBs is enough to make it fail.  

                    // TODO we need to figure out a convention show we know that we have fallen back to no-lines
                    // and we should regenerate it if we ultimately get the PDB information 
                    var cmdLine = string.Format(@"{0}\ngen.exe createpdb {1} {2} {3}",
                        clrDir, Command.Quote(ngenImageFullPath), Command.Quote(ngenOutputDirectory), lineNumberArg);
                    // TODO FIX NOW REMOVE after V4.5 is out a while
                    m_log.WriteLine("set COMPLUS_NGenEnableCreatePdb=1");
                    if (!isV4_5Runtime)
                        m_log.WriteLine("set COMPLUS_NGenLocalWorker=1");
                    m_log.WriteLine("set PATH=" + newPath);
                    m_log.WriteLine("set _NT_SYMBOL_PATH={0}", this.SymbolPath);
                    m_log.WriteLine("*** NGEN  CREATEPDB cmdline: {0}\r\n", cmdLine);
                    var cmd = Command.Run(cmdLine, options);
                    m_log.WriteLine("*** NGEN CREATEPDB returns: {0}", cmd.ExitCode);

                    if (cmd.ExitCode != 0)
                    {
                        // ngen might make a bad PDB, so if it returns failure delete it.  
                        if (File.Exists(outputPdbPath))
                            File.Delete(outputPdbPath);

                        // We may have failed because we could not get the PDB.  
                        if (lineNumberArg.Length != 0)
                        {
                            m_log.WriteLine("Ngen failed to generate pdb for {0}, trying again without /lines", ngenImageFullPath);
                            lineNumberArg = "";
                            continue;
                        }
                    }

                    if (cmd.ExitCode != 0 || !File.Exists(outputPdbPath))
                    {
                        m_log.WriteLine("ngen failed to generate pdb for {0} at expected location {1}", ngenImageFullPath, outputPdbPath);
                        return null;
                    }

                    // Copy the file to where we want the PDB to live (if we could not create there in the first place).    
                    if (outputPdbPath != pdbPath)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(pdbPath));        // Make sure the destination directory exists.
                        File.Copy(outputPdbPath, pdbPath);
                    }
                    return pdbPath;
                }
            }
            finally
            {
                // Insure we have cleaned up any temporary files.  
                if (tempDir != null)
                    DirectoryUtilities.Clean(tempDir);
            }
        }

        /// <summary>
        /// Given a NGEN (or ReadyToRun) imge 'ngenImageFullPath' and the PDB path
        /// that we WANT it to generate generate the PDB.  Returns either pdbPath 
        /// on success or null on failure.  
        /// 
        /// TODO can be removed when we properly publish the NGEN pdbs as part of build.  
        /// </summary>
        string HandleNetCorePdbs(string ngenImageFullPath, string pdbPath)
        {
            // We only handle NGEN PDB. 
            if (!pdbPath.EndsWith(".ni.pdb", StringComparison.OrdinalIgnoreCase))
            {
                m_log.WriteLine("Not a crossGen PDB {0}", pdbPath);
                return null;
            }

            var ngenImageDir = Path.GetDirectoryName(ngenImageFullPath);
            var pdbDir = Path.GetDirectoryName(pdbPath);

            // We need Crossgen, and there are several options, see what we can do. 
            string crossGen = GetCrossGenExePath(ngenImageFullPath);
            if (crossGen == null)
            {
                m_log.WriteLine("Could not find Crossgen.exe to generate PDBs, giving up.");
                return null;
            }

            var winDir = Environment.GetEnvironmentVariable("winDir");
            if (winDir == null)
                return null;

            // Make sure the output dir exists.  
            Directory.CreateDirectory(pdbDir);

            // Are these readyToRun images
            string crossGenInputName = ngenImageFullPath;
            if (!crossGenInputName.EndsWith(".ni.dll", StringComparison.OrdinalIgnoreCase))
            {
                // Note that the PDB does not pick the correct PDB signature unless the name
                // of the PDB matches the name of the DLL (with suffixes removed).  

                crossGenInputName = pdbPath.Substring(0, pdbPath.Length - 3) + "dll";
                File.Copy(ngenImageFullPath, crossGenInputName);
            }

            var cmdLine = Command.Quote(crossGen) +
                " /CreatePdb " + Command.Quote(pdbDir) +
                " /Platform_Assemblies_Paths " + Command.Quote(ngenImageDir) +
                " " + Command.Quote(crossGenInputName);

            var options = new CommandOptions();
            options.AddOutputStream(m_log);
            options.AddNoThrow();

            // Needs diasymreader.dll to be on the path.  
            var newPath = winDir + @"\Microsoft.NET\Framework\v4.0.30319" + ";" +
                winDir + @"\Microsoft.NET\Framework64\v4.0.30319" + ";%PATH%";
            options.AddEnvironmentVariable("PATH", newPath);
            options.AddCurrentDirectory(ngenImageDir);
            m_log.WriteLine("**** Running CrossGen");
            m_log.WriteLine("set PATH=" + newPath);
            m_log.WriteLine("{0}\r\n", cmdLine);
            var cmd = Command.Run(cmdLine, options);

            // Delete the temporary file if necessary
            if (crossGenInputName != ngenImageFullPath)
                FileUtilities.ForceDelete(crossGenInputName);

            if (cmd.ExitCode != 0 || !File.Exists(pdbPath))
            {
                m_log.WriteLine("CrossGen failed to generate {0} exit code {0}", pdbPath, cmd.ExitCode);
                return null;
            }

            return pdbPath;
        }

        private static string getNugetPackageDir()
        {
            string homeDrive = Environment.GetEnvironmentVariable("HOMEDRIVE");
            if (homeDrive == null)
                return null;
            string homePath = Environment.GetEnvironmentVariable("HOMEPATH");
            if (homePath == null)
                return null;

            var nugetPackageDir = homeDrive + homePath + @"\.nuget\packages";
            if (!Directory.Exists(nugetPackageDir))
                return null;
            return nugetPackageDir;
        }

        private string GetCrossGenExePath(string ngenImageFullPath)
        {
            var imageDir = Path.GetDirectoryName(ngenImageFullPath);
            string crossGen = Path.Combine(imageDir, "crossGen.exe");

            m_log.WriteLine("Checking for CoreCLR case, looking for CrossGen at {0}", crossGen);
            if (File.Exists(crossGen))
                return crossGen;

            string coreclr = Path.Combine(imageDir, "coreclr.dll");
            if (File.Exists(coreclr))
            {
                DateTime coreClrTimeStamp = File.GetLastWriteTimeUtc(coreclr);
                m_log.WriteLine("Found coreclr: at  {0}, timestamp {1}", coreclr, coreClrTimeStamp);
                string nugetDir = getNugetPackageDir();
                if (nugetDir != null)
                {
                    m_log.WriteLine("Found nuget package dir: at  {0}", nugetDir);
                    foreach (var runtimeDir in Directory.GetDirectories(nugetDir, "runtime.win*.microsoft.netcore.runtime.coreclr"))
                    {
                        foreach (var runtimeVersionDir in Directory.GetDirectories(runtimeDir))
                        {
                            foreach (var osarchDir in Directory.GetDirectories(Path.Combine(runtimeVersionDir, "runtimes"), "win*"))
                            {
                                string packageCoreCLR = Path.Combine(osarchDir, @"native\coreclr.dll");
                                DateTime packageCoreClrTimeStamp = File.GetLastWriteTimeUtc(packageCoreCLR);
                                m_log.WriteLine("Checking timestamp of file {0} = {1}", packageCoreCLR, packageCoreClrTimeStamp);
                                if (File.Exists(packageCoreCLR) && packageCoreClrTimeStamp == coreClrTimeStamp)
                                {
                                    crossGen = Path.Combine(runtimeVersionDir, @"tools\crossgen.exe");
                                    m_log.WriteLine("Found matching CoreCLR, probing for crossgen at {0}", crossGen);
                                    if (File.Exists(crossGen))
                                        return crossGen;
                                }
                            }
                        }
                    }
                }
            }

            // Check if you are running the runtime out of the nuget directory itself 
            var m = Regex.Match(imageDir, @"^(.*)\\runtimes\\win.*\\native$", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                crossGen = Path.Combine(m.Groups[1].Value, "tools", "crossGen.exe");
                if (File.Exists(crossGen))
                    return crossGen;
            }

            m_log.WriteLine("Could not find crossgen, giving up");
            return null;
        }

        // TODO remove after 12/2015
        private void InsurePathIsInNIC(TextWriter log, ref string ngenImageFullPath)
        {
            // We only get called if we are 4.5. or beyond, so we should have AUX files if we are in the nic.  
            string auxFilePath = ngenImageFullPath + ".aux";
            if (File.Exists(auxFilePath))
            {
                log.WriteLine("Path has a AUX file in NIC: {0}", ngenImageFullPath);
                return;
            }
            string ngenFileName = Path.GetFileName(ngenImageFullPath);
            long ngenFileSize = (new FileInfo(ngenImageFullPath)).Length;
            log.WriteLine("Path is not in NIC, trying to put it in the NIC: Size {0} {1}", ngenFileSize, ngenImageFullPath);

            string windir = Environment.GetEnvironmentVariable("WinDir");
            if (windir == null)
                return;

            string candidate = null;
            string assemblyDir = Path.Combine(windir, "Assembly");
            foreach (string nicBase in Directory.GetDirectories(assemblyDir, "NativeImages_v4*"))
            {
                foreach (string file in Directory.EnumerateFiles(nicBase, ngenFileName, SearchOption.AllDirectories))
                {
                    long fileLen = (new FileInfo(file)).Length;
                    if (fileLen == ngenFileSize)
                    {
                        if (candidate != null)      // Ambiguity, give up.  
                        {
                            log.WriteLine("There is more than one file in the NIC with matching name and size! giving up.");
                            return;
                        }
                        candidate = file;
                    }
                }
            }

            if (candidate != null)
            {
                ngenImageFullPath = candidate;
                log.WriteLine("Updating path to be in NIC: {0}", ngenImageFullPath);
            }
        }

        /// <summary>
        ///  Called when you are done with the symbol reader.  Currently does nothing.  
        /// </summary>
        public void Dispose() { }

#region private
        /// <summary>
        /// Returns true if 'filePath' exists and is a PDB that has pdbGuid and pdbAge.  
        /// if pdbGuid == Guid.Empty, then the pdbGuid and pdbAge checks are skipped. 
        /// </summary>
        private bool PdbMatches(string filePath, Guid pdbGuid, int pdbAge, bool checkSecurity = true)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    if (checkSecurity && !CheckSecurity(filePath))
                    {
                        m_log.WriteLine("FindSymbolFilePath: Aborting, security check failed on {0}", filePath);
                        return false;
                    }

                    if (pdbGuid == Guid.Empty)
                    {
                        m_log.WriteLine("FindSymbolFilePath: No PDB Guid = Guid.Empty provided, assuming an unsafe PDB match for {0}", filePath);
                        return true;
                    }
                    SymbolModule module = this.OpenSymbolFile(filePath);
                    if ((module.PdbGuid == pdbGuid) && (module.PdbAge == pdbAge))
                        return true;
                    else
                        m_log.WriteLine("FindSymbolFilePath: ************ FOUND PDB File {0} has Guid {1} age {2} != Desired Guid {3} age {4}",
                            filePath, module.PdbGuid, module.PdbAge, pdbGuid, pdbAge);
                }
                else
                    m_log.WriteLine("FindSymbolFilePath: Probed file location {0} does not exist", filePath);
            }
            catch(Exception e) {
                m_log.WriteLine("FindSymbolFilePath: Aborting pdbMatch of {0} Exception thrown: {1}", filePath, e.Message);
            }
            return false;
        }

        /// <summary>
        /// Fetches a file from the server 'serverPath' with pdb signature path 'pdbSigPath' (concatinate them with a / or \ separator
        /// to form a complete URL or path name).   It will place the file in 'fullDestPath'   It will return true if successful
        /// If 'contentTypeFilter is present, this predicate is called with the URL content type (e.g. application/octet-stream)
        /// and if it returns false, it fails.   This insures that things that are the wrong content type (e.g. redirects to 
        /// some sort of login) fail cleanly.  
        /// 
        /// You should probably be using GetFileFromServer
        /// </summary>
        /// <param name="serverPath">path to server (e.g. \\symbols\symbols or http://symweb) </param>
        /// <param name="pdbIndexPath">pdb path with signature (e.g clr.pdb/1E18F3E494DC464B943EA90F23E256432/clr.pdb)</param>
        /// <param name="fullDestPath">the full path of where to put the file locally </param>
        /// <param name="contentTypeFilter">if present this allows you to filter out urls that dont match this ContentType.</param>
        internal bool GetPhysicalFileFromServer(string serverPath, string pdbIndexPath, string fullDestPath, Predicate<string> contentTypeFilter = null)
        {
            if (File.Exists(fullDestPath))
                return true;

            var sw = Stopwatch.StartNew();

            if (m_deadServers != null)
            {
                // Try again after 5 minutes.  
                if ((DateTime.UtcNow - m_lastDeadTimeUtc).TotalSeconds > 300)
                    m_deadServers = null;
            }

            if (m_deadServers != null && m_deadServers.Contains(serverPath))
            {
                m_log.WriteLine("FindSymbolFilePath: Skipping server {0} because it was unreachable in the past, will try again in 5 min.", serverPath);
                return false;
            }

            bool canceled = false;        // Are we trying to cancel the task
            bool alive = false;           // Has the task ever been shown to be alive (worth giving them time)
            bool successful = false;      // The task was successful
            try
            {
                // This implements a timeout 
                Task task = Task.Factory.StartNew(delegate
                {
                    if (Uri.IsWellFormedUriString(serverPath, UriKind.Absolute))
                    {
                        var fullUri = BuildFullUri(serverPath, pdbIndexPath);
                        try
                        {
                            m_log.WriteLine("FindSymbolFilePath: In task, sending HTTP request {0}", fullUri);

                            var req = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(fullUri);
                            req.UserAgent = "Microsoft-Symbol-Server/6.13.0009.1140";
                            var response = req.GetResponse();
                            alive = true;
                            if (!canceled)
                            {
                                if (contentTypeFilter != null && !contentTypeFilter(response.ContentType))
                                    throw new InvalidOperationException("Bad File Content type " + response.ContentType + " for " + fullDestPath);

                                using (var fromStream = response.GetResponseStream())
                                    if (CopyStreamToFile(fromStream, fullUri, fullDestPath, ref canceled) == 0)
                                    {
                                        File.Delete(fullDestPath);
                                        throw new InvalidOperationException("Illegal Zero sized file " + fullDestPath);
                                    }
                                successful = true;
                            }
                        }
                        catch (Exception e)
                        {
                            if (!canceled)
                            {
                                var asWeb = e as WebException;
                                var sentMessage = false;
                                if (asWeb != null)
                                {
                                    var asHttpResonse = asWeb.Response as HttpWebResponse;
                                    if (asHttpResonse != null && asHttpResonse.StatusCode == HttpStatusCode.NotFound)
                                    {
                                        sentMessage = true;
                                        m_log.WriteLine("FindSymbolFilePath: Probe of {0} was not found.", fullUri);
                                    }
                                }
                                if (!sentMessage)
                                    m_log.WriteLine("FindSymbolFilePath: Probe of {0} failed: {1}", fullUri, e.Message);
                            }
                        }
                    }
                    else
                    {
                        // Use CopyStreamToFile instead of File.Copy because it is interruptible and displays status.  
                        var fullSrcPath = Path.Combine(serverPath, pdbIndexPath);
                        if (File.Exists(fullSrcPath))
                        {
                            alive = true;
                            if (!canceled)
                            {
                                using (var fromStream = File.OpenRead(fullSrcPath))
                                    if (CopyStreamToFile(fromStream, fullSrcPath, fullDestPath, ref canceled) == 0)
                                    {
                                        File.Delete(fullDestPath);
                                        throw new InvalidOperationException("Illegal Zero sized file " + fullDestPath);
                                    }
                                successful = true;
                            }
                        }
                        else
                        {
                            alive = true;
                            if (!canceled)
                                m_log.WriteLine("FindSymbolFilePath: Probe of {0}, file not present", fullSrcPath);
                        }
                    }
                });

                // Wait 10 seconds allowing for interruptions.  
                var limit = 100;
                if (serverPath.StartsWith(@"\\symbols", StringComparison.OrdinalIgnoreCase))     // This server is pretty slow.  
                    limit = 250;

                for (int i = 0; i < limit; i++)
                {
                    if (i == 10)
                        m_log.WriteLine("\r\nFindSymbolFilePath: Waiting for initial connection to {0}/{1}.", serverPath, pdbIndexPath);

                    if (task.Wait(100))
                        break;
                    Thread.Sleep(0);
                }

                if (alive)
                {
                    if (!task.Wait(100))
                        m_log.WriteLine("\r\nFindSymbolFilePath: Copy in progress on {0}/{1}, waiting for completion.", serverPath, pdbIndexPath);

                    // Let it complete, however we do sleep so we can be interrupted.  
                    while (!task.Wait(100))
                        Thread.Sleep(0);        // TO allow interruption
                }
                // If we did not complete, set the dead server information.  
                else if (!task.IsCompleted)
                {
                    canceled = true;
                    m_log.WriteLine("FindSymbolFilePath: Time {0} sec.  Timeout of {1} seconds exceeded for {2}.  Setting as dead server",
                            sw.Elapsed.TotalSeconds, limit / 10, serverPath);
                    if (m_deadServers == null)
                        m_deadServers = new List<string>();
                    m_deadServers.Add(serverPath);
                    m_lastDeadTimeUtc = DateTime.UtcNow;
                }
            }
            finally
            {
                canceled = true;
            }

            return successful && File.Exists(fullDestPath);
        }

        /// <summary>
        /// Build the full uri from server path and pdb index path
        /// </summary>
        private string BuildFullUri(string serverPath, string pdbIndexPath)
        {
            var tail = pdbIndexPath.Replace('\\', '/');
            if (!tail.StartsWith("/", StringComparison.Ordinal))
                tail = "/" + tail;

            // The server path can contain query parameters (eg, Azure storage SAS token).
            // Append the pdb index to the path part.
            var query = serverPath.IndexOf('?');
            if (query > 0)
            {
                return serverPath.Insert(query, tail);
            }
            else
            {
                return serverPath + tail;
            }
        }

        /// <summary>
        /// This just copies a stream to a file path with logging.  
        /// </summary>
        private int CopyStreamToFile(Stream fromStream, string fromUri, string fullDestPath, ref bool canceled)
        {
            bool completed = false;
            int byteCount = 0;
            var copyToFileName = fullDestPath + ".new";
            try
            {
                var dirName = Path.GetDirectoryName(fullDestPath);
                Directory.CreateDirectory(dirName);
                m_log.WriteLine("CopyStreamToFile: Copying {0} to {1}", fromUri, copyToFileName);
                var sw = Stopwatch.StartNew();
                int lastMeg = 0;
                int last10K = 0;
                using (Stream toStream = File.Create(copyToFileName))
                {
                    byte[] buffer = new byte[8192];
                    for (;;)
                    {
                        int count = fromStream.Read(buffer, 0, buffer.Length);
                        if (count == 0)
                            break;

                        toStream.Write(buffer, 0, count);
                        byteCount += count;

                        if (byteCount - last10K >= 10000)
                        {
                            m_log.Write(".");
                            last10K += 10000;
                        }
                        if (byteCount - lastMeg >= 1000000)
                        {
                            m_log.WriteLine(" {0:f1} Meg", byteCount / 1000000.0);
                            m_log.Flush();
                            lastMeg += 1000000;
                        }
                        if (sw.Elapsed.TotalMilliseconds > 100)
                        {
                            m_log.Flush();
                            System.Threading.Thread.Sleep(0);       // allow interruption.
                            sw.Restart();
                        }

                        if (canceled)
                            break;
                    }
                }
                if (!canceled)
                    completed = true;
            }
            finally
            {
                m_log.WriteLine();
                if (completed)
                {
                    FileUtilities.ForceMove(copyToFileName, fullDestPath);
                    m_log.WriteLine("CopyStreamToFile: Copy Done, moving to {0}", fullDestPath);
                }
                else
                {
                    m_log.WriteLine("CopyStreamToFile: Copy not complete, deleting temp copy to file");
                    FileUtilities.ForceDelete(copyToFileName);
                }
            }
            return byteCount;
        }

        /// <summary>
        /// Looks up 'fileIndexPath' on the server 'urlForServer' (concatenate to form complete URL) copying the file to 
        /// 'targetPath' and returning targetPath name there (thus it is always a local file).  Unlike  GetPhysicalFileFromServer, 
        /// GetFileFromServer understands how to deal with compressed files and file.ptr (redirection).  
        /// </summary>
        /// <returns>targetPath or null if the file cannot be found.</returns>
        private string GetFileFromServer(string urlForServer, string fileIndexPath, string targetPath)
        {
            if (File.Exists(targetPath))
            {
                m_log.WriteLine("FindSymbolFilePath: Found in cache {0}", targetPath);
                return targetPath;
            }

            // Fail quickly if instructed to  
            if ((Options & SymbolReaderOptions.CacheOnly) != 0)
            {
                m_log.WriteLine("FindSymbolFilePath: no file at cache location {0} and cacheOnly set, giving up.", targetPath);
                return null;
            }

            // We just had a symbol cache with no target.   
            if (urlForServer == null)
                return null;

            // Allows us to reject files that are not binary (sometimes you get redirected to a 
            // login script and we don't want to blindly accept that).  
            Predicate<string> onlyBinaryContent = delegate (string contentType)
            {
                bool ret = contentType.EndsWith("octet-stream");
                if (!ret)
                    m_log.WriteLine("FindSymbolFilePath: expecting 'octet-stream' (Binary) data, got {0} (are you redirected to a login page?)", contentType);
                return ret;
            };

            // Just try to fetch the file directly
            m_log.WriteLine("FindSymbolFilePath: Searching Symbol Server {0}.", urlForServer);
            if (GetPhysicalFileFromServer(urlForServer, fileIndexPath, targetPath, onlyBinaryContent))
                return targetPath;

            // The rest of this compressed file/file pointers stuff is only for remote servers.  
            if (!urlForServer.StartsWith(@"\\") && !Uri.IsWellFormedUriString(urlForServer, UriKind.Absolute))
                return null;

            // See if it is a compressed file by replacing the last character of the name with an _
            var compressedSigPath = fileIndexPath.Substring(0, fileIndexPath.Length - 1) + "_";
            var compressedFilePath = targetPath.Substring(0, targetPath.Length - 1) + "_";
            if (GetPhysicalFileFromServer(urlForServer, compressedSigPath, compressedFilePath, onlyBinaryContent))
            {
                // Decompress it
                m_log.WriteLine("FindSymbolFilePath: Expanding {0} to {1}", compressedFilePath, targetPath);
                var commandline = "Expand " + Command.Quote(compressedFilePath) + " " + Command.Quote(targetPath);
                var options = new CommandOptions().AddNoThrow();
                var command = Command.Run(commandline, options);
                if (command.ExitCode != 0)
                {
                    m_log.WriteLine("FindSymbolFilePath: Failure executing: {0}", commandline);
                    return null;
                }
                File.Delete(compressedFilePath);
                return targetPath;
            }

            // See if we have a file that tells us to redirect elsewhere. 
            var filePtrSigPath = Path.Combine(Path.GetDirectoryName(fileIndexPath), "file.ptr");
            var filePtrFilePath = Path.Combine(Path.GetDirectoryName(targetPath), "file.ptr");
            FileUtilities.ForceDelete(filePtrFilePath);
            var goodSeparator = urlForServer.StartsWith(@"\\") ? @"\\" : "/";
            if (GetPhysicalFileFromServer(urlForServer, filePtrSigPath, filePtrFilePath))
            {
                var filePtrData = File.ReadAllText(filePtrFilePath).Trim();
                FileUtilities.ForceDelete(filePtrFilePath);
                if (filePtrData.StartsWith("MSG"))
                {
                    m_log.WriteLine("FindSymbolFilePath: Probe of {0}{1}{2} fails with message '{3}'", urlForServer, goodSeparator, filePtrSigPath, filePtrData);
                    m_log.WriteLine("FindSymbolFilePath: target File.ptr file {0} ", filePtrFilePath);
                    return null;
                }
                if (filePtrData.StartsWith("PATH:"))
                    filePtrData = filePtrData.Substring(5);
                else
                    m_log.WriteLine("FindSymbolFilePath: file.ptr data: {0}", filePtrData);

                if (filePtrData.EndsWith(fileIndexPath, StringComparison.OrdinalIgnoreCase))
                {
                    var redirectedServer = filePtrData.Substring(0, filePtrData.Length - fileIndexPath.Length - 1);
                    var goodSeparatorForRedirectedServer = redirectedServer.StartsWith(@"\\") ? @"\\" : "/";

                    m_log.WriteLine("FindSymbolFilePath: Probe of {0}{1}{2} redirecting to {3}{4}{5}",
                        urlForServer, goodSeparator, filePtrSigPath,
                        redirectedServer, goodSeparatorForRedirectedServer, fileIndexPath);
                    return GetFileFromServer(redirectedServer, fileIndexPath, targetPath);
                }
                else
                {
                    if (filePtrData.StartsWith(@"\\"))
                    {
                        var bangIdx = filePtrData.IndexOf('\\', 2);
                        if (0 <= bangIdx)
                        {
                            m_log.WriteLine("FindSymbolFilePath: Looking up UNC path {0}", filePtrData);
                            var server = filePtrData.Substring(0, bangIdx);
                            var path = filePtrData.Substring(bangIdx + 1);
                            return GetFileFromServer(server, path, targetPath);
                        }
                    }
                    else if (filePtrData.StartsWith(@"..\") || filePtrData.StartsWith(@".\"))
                    {
                        m_log.WriteLine("FindSymbolFilePath: Probe of {0}{1}{2} redirecting to {3}\\{4}",
                            urlForServer, goodSeparator, filePtrSigPath,
                            urlForServer, filePtrData);
                        return GetFileFromServer(urlForServer, filePtrData, targetPath);
                    }
                    m_log.WriteLine("FindSymbolFilePath: Error, Don't know how to look up redirected location {0}", filePtrData);
                }
            }

            // See if we already have a prefix XX/ prefix.  
            if (2 < fileIndexPath.Length && fileIndexPath[2] != '/')
            {
                m_log.WriteLine("Trying the XX/XXYYY.PDB convention");
                // See if it is following the XX/XXYYYY.PDB convention (used to make directories smaller).  
                var prefixedPath = fileIndexPath.Substring(0, 2) + "/" + fileIndexPath;
                return GetFileFromServer(urlForServer, prefixedPath, targetPath);
            }

            return null;
        }

        /// <summary>
        /// Deduce the path to where CLR.dll (and in particular NGEN.exe live for the NGEN image 'ngenImagepath')
        /// Returns null if it can't be found.  If the NGEN image is associated with a private runtime return 
        /// that value in 'privateVerStr'
        /// </summary>
        private static string GetClrDirectoryForNGenImage(string ngenImagePath, TextWriter log, out string privateRuntimeVerStr)
        {
            string majorVersion;            // a small integer (e.g. 4)
            privateRuntimeVerStr = null;
            // Set the default bitness
            string bitness;            // Either "64" or ""
            var m = Regex.Match(ngenImagePath, @"^(.*)\\assembly\\NativeImages_(v(\d+)[\dA-Za-z.]*)_(\d\d)\\", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                var basePath = m.Groups[1].Value;
                var version = m.Groups[2].Value;
                majorVersion = m.Groups[3].Value;
                bitness = m.Groups[4].Value;

                // See if this NGEN image was in a NIC associated with a private runtime.  
                if (basePath.EndsWith(version))
                {
                    if (Directory.Exists(basePath))
                    {
                        privateRuntimeVerStr = version;
                        return basePath;
                    }
                }
            }
            else
            {
                // Per-user NGEN Image Caches.   
                m = Regex.Match(ngenImagePath, @"\\Microsoft\\CLR_v(\d+)\.\d+(_(\d\d))?\\NativeImages", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    majorVersion = m.Groups[1].Value;
                    bitness = m.Groups[3].Value;
                }
                else
                {
                    // Pre-generated native images.  
                    m = Regex.Match(ngenImagePath, @"\\Microsoft.NET\\Framework((\d\d)?)\\v(\d+).*\\NativeImages", RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        majorVersion = m.Groups[3].Value;
                        bitness = m.Groups[1].Value;
                    }
                    else
                        return null;
                }
            }

            // Only version 4.0 of the runtime has NGEN PDB support 
            if (int.Parse(majorVersion) < 4)
            {
                log.WriteLine("Pre V4.0 native image, skipping: {0}", ngenImagePath);
                return null;
            }

            var winDir = Environment.GetEnvironmentVariable("winDir");

            if (bitness != "64")
                bitness = "";
            Debug.Assert(bitness == "64" || bitness == "");

            var frameworkDir = Path.Combine(winDir, @"Microsoft.NET\Framework" + bitness);
            var candidates = Directory.GetDirectories(frameworkDir, "v" + majorVersion + ".*");
            if (candidates.Length != 1)
            {
                log.WriteLine("Warning: Could not find Version {0} of the .NET Framework in {1}", majorVersion, frameworkDir);
                return null;
            }
            return candidates[0];
        }

        private string m_SourcePath;
        internal List<string> ParsedSourcePath
        {
            get
            {
                if (m_parsedSourcePath == null)
                {
                    m_parsedSourcePath = new List<string>();
                    foreach (var path in SourcePath.Split(';'))
                    {
                        var normalizedPath = path.Trim();
                        if (normalizedPath.EndsWith(@"\"))
                            normalizedPath = normalizedPath.Substring(0, normalizedPath.Length - 1);
                        if (Directory.Exists(normalizedPath))
                            m_parsedSourcePath.Add(normalizedPath);
                        else
                            m_log.WriteLine("Path {0} in source path does not exist, skipping.", normalizedPath);
                    }
                }
                return m_parsedSourcePath;
            }
        }
        internal List<string> m_parsedSourcePath;

        private bool CheckSecurity(string pdbName)
        {
            if (SecurityCheck == null)
            {
                m_log.WriteLine("Found PDB {0}, however this is in an unsafe location.", pdbName);
                m_log.WriteLine("If you trust this location, place this directory the symbol path to correct this (or use the SecurityCheck method to override)");
                return false;
            }
            if (!SecurityCheck(pdbName))
            {
                m_log.WriteLine("Found PDB {0}, but failed security check.", pdbName);
                return false;
            }
            return true;
        }

        /// <summary>
        /// We may be a 32 bit app which has File system redirection turned on
        /// Morph System32 to SysNative in that case to bypass file system redirection         
        /// </summary>
        private static string BypassSystem32FileRedirection(string path)
        {
            var winDir = Environment.GetEnvironmentVariable("WinDir");
            if (winDir != null)
            {
                var system32 = Path.Combine(winDir, "System32");
                if (path.StartsWith(system32, StringComparison.OrdinalIgnoreCase))
                {
                    if (Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432") != null)
                    {
                        var sysNative = Path.Combine(winDir, "Sysnative");
                        var newPath = Path.Combine(sysNative, path.Substring(system32.Length + 1));
                        if (File.Exists(newPath))
                            path = newPath;
                    }
                }
            }
            return path;
        }

        // Used as the key to the m_pdbPathCache.  
        struct PdbSignature : IEquatable<PdbSignature>
        {
            public override int GetHashCode() { return Name.GetHashCode() + ID.GetHashCode(); }
            public bool Equals(PdbSignature other) { return ID == other.ID && Name == other.Name && Age == other.Age; }
            public string Name;
            public Guid ID;
            public int Age;
        }

        internal TextWriter m_log;
        private List<string> m_deadServers;     // What servers can't be reached right now
        private DateTime m_lastDeadTimeUtc;     // The last time something went dead.  
        private string m_SymbolCacheDirectory;
        private string m_SourceCacheDirectory;
        private Cache<string, SymbolModule> m_symbolModuleCache;
        private Cache<PdbSignature, string> m_pdbPathCache;
        private string m_symbolPath;

#endregion
    }

    /// <summary>
    /// A symbolReaderModule represents a single PDB.   You get one from SymbolReader.OpenSymbolFile
    /// It is effecively a managed interface to the Debug Interface Access (DIA) see 
    /// http://msdn.microsoft.com/en-us/library/x93ctkx8.aspx for more.   I have only exposed what
    /// I need, and the interface is quite large (and not super pretty).  
    /// </summary>
    public unsafe class SymbolModule
    {
        /// <summary>
        /// The path name to the PDB itself
        /// </summary>
        public string SymbolFilePath { get { return m_pdbPath; } }
        /// <summary>
        /// This is the EXE associated with the Pdb.  It may be null or an invalid path.  It is used
        /// to help look up source code (it is implicitly part of the Source Path search) 
        /// </summary>
        public string ExePath { get; set; }
        /// <summary>
        /// Finds a (method) symbolic name for a given relative virtual address of some code.  
        /// Returns an empty string if a name could not be found. 
        /// </summary>
        public string FindNameForRva(uint rva)
        {
            uint dummy = 0;
            return FindNameForRva(rva, ref dummy);
        }
        /// <summary>
        /// Finds a (method) symbolic name for a given relative virtual address of some code.  
        /// Returns an empty string if a name could not be found.  
        /// symbolStartRva is set to the start of the symbol start 
        /// </summary>
        public string FindNameForRva(uint rva, ref uint symbolStartRva)
        {
            System.Threading.Thread.Sleep(0);           // Allow cancellation.  
            if (m_symbolsByAddr == null)
                return "";
            IDiaSymbol symbol = m_symbolsByAddr.symbolByRVA(rva);
            if (symbol == null)
            {
                Debug.WriteLine(string.Format("Warning: address 0x{0:x} not found.", rva));
                return "";
            }

            var ret = symbol.name;
            if (ret == null)
            {
                Debug.WriteLine(string.Format("Warning: address 0x{0:x} had a null symbol name.", rva));
                return "";
            }
            var symbolLen = symbol.length;
            if (symbolLen == 0)
            {
                Debug.WriteLine(string.Format("Warning: address 0x{0:x} symbol {1} has length 0", rva, ret));
            }
            symbolStartRva = symbol.relativeVirtualAddress;

            // TODO determine why this happens!
            var symbolRva = symbol.relativeVirtualAddress;
            if (!(symbolRva <= rva && rva < symbolRva + symbolLen) && symbolLen != 0)
            {
                m_reader.Log.WriteLine("Warning: NOT IN RANGE: address 0x{0:x} start {2:x} end {3:x} Offset {4:x} Len {5:x}, symbol {1}, prefixing with ??.",
                    rva, ret, symbolRva, symbolRva + symbolLen, rva - symbolRva, symbolLen);
                ret = "??" + ret;   // Prefix with ?? to indicate it is questionable.  
            }

            // TODO FIX NOW, should not need to do this hand-unmangling.
            if (0 <= ret.IndexOf('@'))
            {
                // TODO relatively inefficient.  
                string unmangled = null;
                symbol.get_undecoratedNameEx(0x1000, out unmangled);
                if (unmangled != null)
                    ret = unmangled;

                if (ret.StartsWith("@"))
                    ret = ret.Substring(1);
                if (ret.StartsWith("_"))
                    ret = ret.Substring(1);

#if false // TODO FIX NOW remove  
                var m = Regex.Match(ret, @"(.*)@\d+$");
                if (m.Success)
                    ret = m.Groups[1].Value;
                else
                    Debug.WriteLine(string.Format("Warning: address 0x{0:x} symbol {1} has a mangled name.", rva, ret));
#else
                var atIdx = ret.IndexOf('@');
                if (0 < atIdx)
                    ret = ret.Substring(0, atIdx);
#endif
            }

            // See if this is a NGEN mangled name, which is $#Assembly#Token suffix.  If so strip it off. 
            var dollarIdx = ret.LastIndexOf('$');
            if (0 <= dollarIdx && dollarIdx + 2 < ret.Length && ret[dollarIdx + 1] == '#' && 0 <= ret.IndexOf('#', dollarIdx + 2))
                ret = ret.Substring(0, dollarIdx);

            // See if we have a Project N map that maps $_NN to a pre-merged assembly name 
            var mergedAssembliesMap = GetMergedAssembliesMap();
            if (mergedAssembliesMap != null)
            {
                bool prefixMatchFound = false;
                Regex prefixMatch = new Regex(@"\$(\d+)_");
                ret = prefixMatch.Replace(ret, delegate (Match m)
                {
                    prefixMatchFound = true;
                    var original = m.Groups[1].Value;
                    var moduleIndex = int.Parse(original);
                    string fullAssemblyName;
                    if (mergedAssembliesMap.TryGetValue(moduleIndex, out fullAssemblyName))
                    {
                        try
                        {
                            var assemblyName = new AssemblyName(fullAssemblyName);
                            return assemblyName.Name + "!";
                        }
                        catch (Exception) { } // Catch all AssemlyName fails with ' in the name.   
                    }
                    return original;
                });

                // corefx.dll does not have a tag.  TODO this feels like a hack!
                if (!prefixMatchFound)
                    ret = "mscorlib!" + ret;
            }
            return ret;
        }

        /// <summary>
        /// Fetches the source location (line number and file), given the relative virtual address (RVA)
        /// of the location in the executable.  
        /// </summary>
        public SourceLocation SourceLocationForRva(uint rva)
        {
            string dummyString;
            uint dummyToken;
            int dummyILOffset;
            return SourceLocationForRva(rva, out dummyString, out dummyToken, out dummyILOffset);
        }

        /// <summary>
        /// This overload of SourceLocationForRva like the one that takes only an RVA will return a source location
        /// if it can.   However this version has additional support for NGEN images.   In the case of NGEN images 
        /// for .NET V4.6.1 or later), the NGEN images can't convert all the way back to a source location, but they 
        /// can convert the RVA back to IL artifacts (ilAssemblyName, methodMetadataToken, iloffset).  THese can then
        /// be used to look up the source line using the IL PDB.  
        /// 
        /// Thus if the return value from this is null, check to see if the ilAssemblyName is non-null, and if not 
        /// you can look up the source location using that information.  
        /// </summary>
        public SourceLocation SourceLocationForRva(uint rva, out string ilAssemblyName, out uint methodMetadataToken, out int ilOffset)
        {
            ilAssemblyName = null;
            methodMetadataToken = 0;
            ilOffset = -1;
            m_reader.m_log.WriteLine("SourceLocationForRva: looking up RVA {0:x} ", rva);

            // First fetch the line number information 'normally'.  (for the non-NGEN case, and old style NGEN (with /lines)). 
            uint fetchCount;
            IDiaEnumLineNumbers sourceLocs;
            m_session.findLinesByRVA(rva, 0, out sourceLocs);
            IDiaLineNumber sourceLoc;
            sourceLocs.Next(1, out sourceLoc, out fetchCount);
            if (fetchCount == 0)
            {
                // We have no native line number information.   See if we are an NGEN image and we can convert the RVA to an IL Offset.   
                m_reader.m_log.WriteLine("SourceLocationForRva: did not find line info Looking for mangled symbol name (for NGEN pdbs)");
                IDiaSymbol method = m_symbolsByAddr.symbolByRVA(rva);
                if (method != null)
                {
                    // Check to see if the method name follows the .NET V4.6.1 conventions
                    // of $#ASSEMBLY#TOKEN.   If so the line number we got back is not a line number at all but
                    // an ILOffset. 
                    string name = method.name;
                    if (name != null)
                    {
                        m_reader.m_log.WriteLine("SourceLocationForRva: RVA lives in method with 4.6.1 mangled name {0}", name);
                        int suffixIdx = name.LastIndexOf("$#");
                        if (0 <= suffixIdx && suffixIdx + 2 < name.Length)
                        {
                            int tokenIdx = name.IndexOf('#', suffixIdx + 2);
                            if (tokenIdx < 0)
                            {
                                m_reader.m_log.WriteLine("SourceLocationForRva: Error parsing method name mangling.  No # separating token");
                                return null;
                            }
                            string tokenStr = name.Substring(tokenIdx + 1);
                            int token;
                            if (!int.TryParse(tokenStr, System.Globalization.NumberStyles.AllowHexSpecifier, null, out token))
                            {
                                m_reader.m_log.WriteLine("SourceLocationForRva: Could not parse token as a Hex number {0}", tokenStr);
                                return null;
                            }

                            // We need the metadata token and assembly.   We get this from the name mangling of the method symbol, 
                            // so look that up.  
                            if (tokenIdx == suffixIdx + 2)      // The assembly name is null
                            {
                                ilAssemblyName = Path.GetFileNameWithoutExtension(m_pdbPath);
                                // strip off the .ni if present
                                if (ilAssemblyName.EndsWith(".ni", StringComparison.OrdinalIgnoreCase))
                                    ilAssemblyName = ilAssemblyName.Substring(0, ilAssemblyName.Length - 3);
                            }
                            else
                                ilAssemblyName = name.Substring(suffixIdx + 2, tokenIdx - (suffixIdx + 2));
                            methodMetadataToken = (uint)token;
                            ilOffset = 0;           // If we don't find an IL offset, we 'guess' an ILOffset of 0

                            m_reader.m_log.WriteLine("SourceLocationForRva: Looking up IL Offset by RVA 0x{0:x}", rva);
                            m_session.findILOffsetsByRVA(rva, 0, out sourceLocs);
                            // FEEFEE is some sort of illegal line number that is returned some time,  It is better to ignore it.  
                            // and take the next valid line
                            for (;;)
                            {
                                sourceLocs.Next(1, out sourceLoc, out fetchCount);
                                if (fetchCount == 0)
                                {
                                    m_reader.m_log.WriteLine("SourceLocationForRva: Ran out of IL mappings, guessing 0x{0:x}", ilOffset);
                                    break;
                                }
                                ilOffset = (int)sourceLoc.lineNumber;
                                if (ilOffset != 0xFEEFEE)
                                    break;
                                m_reader.m_log.WriteLine("SourceLocationForRva: got illegal offset FEEFEE picking next offset.");
                                ilOffset = 0;
                            }
                            m_reader.m_log.WriteLine("SourceLocationForRva: Found native to IL mappings, IL offset 0x{0:x}", ilOffset);
                            return null;                           // we don't have source information but we did return the IL information. 
                        }
                    }
                }
                m_reader.m_log.WriteLine("SourceLocationForRva: No lines for RVA {0:x} ", rva);
                return null;
            }

            // If we reach here we are in the non-NGEN case, we are not mapping to IL information and 
            IDiaSourceFile diaSrcFile = sourceLoc.sourceFile;
            var lineNum = (int)sourceLoc.lineNumber;

            var sourceFile = new SourceFile(this, diaSrcFile);
            if (lineNum == 0xFEEFEE)
                lineNum = 0;
            var sourceLocation = new SourceLocation(sourceFile, lineNum);
            m_reader.m_log.WriteLine("SourceLocationForRva: RVA {0:x} maps to line {1} file {2} ", rva, lineNum, sourceFile.BuildTimeFilePath);
            return sourceLocation;
        }

        /// <summary>
        /// Managed code is shipped as IL, so RVA to NATIVE mapping can't be placed in the PDB. Instead
        /// what is placed in the PDB is a mapping from a method's meta-data token and IL offset to source
        /// line number.  Thus if you have a metadata token and IL offset, you can again get a source location
        /// </summary>
        public SourceLocation SourceLocationForManagedCode(uint methodMetadataToken, int ilOffset)
        {
            m_reader.m_log.WriteLine("SourceLocationForManaged: Looking up method token {0:x} ilOffset {1:x}", methodMetadataToken, ilOffset);

            IDiaSymbol methodSym;
            m_session.findSymbolByToken(methodMetadataToken, SymTagEnum.SymTagFunction, out methodSym);
            if (methodSym == null)
            {
                m_reader.m_log.WriteLine("SourceLocationForManaged: No symbol for token {0:x} ilOffset {1:x}", methodMetadataToken, ilOffset);
                return null;
            }

            uint fetchCount;
            IDiaEnumLineNumbers sourceLocs;
            IDiaLineNumber sourceLoc;

            // TODO FIX NOW, this code here is for debugging only turn if off when we are happy.  
            //m_session.findLinesByRVA(methodSym.relativeVirtualAddress, (uint)(ilOffset + 256), out sourceLocs);
            //for (int i = 0; ; i++)
            //{
            //    sourceLocs.Next(1, out sourceLoc, out fetchCount);
            //    if (fetchCount == 0)
            //        break;
            //    if (i == 0)
            //        m_reader.m_log.WriteLine("SourceLocationForManaged source file: {0}", sourceLoc.sourceFile.fileName);
            //    m_reader.m_log.WriteLine("SourceLocationForManaged ILOffset {0:x} -> line {1}",
            //        sourceLoc.relativeVirtualAddress - methodSym.relativeVirtualAddress, sourceLoc.lineNumber);
            //} // End TODO FIX NOW debugging code

            // For managed code, the 'RVA' is a 'cumulative IL offset' (amount of IL bytes before this in the module)
            // Thus you find the line number of a particular IL offset by adding the offset within the method to
            // the cumulative IL offset of the start of the method.  
            m_session.findLinesByRVA(methodSym.relativeVirtualAddress + (uint)ilOffset, 256, out sourceLocs);
            sourceLocs.Next(1, out sourceLoc, out fetchCount);
            if (fetchCount == 0)
            {
                m_reader.m_log.WriteLine("SourceLocationForManaged: No lines for token {0:x} ilOffset {1:x}", methodMetadataToken, ilOffset);
                return null;
            }

            var sourceFile = new SourceFile(this, sourceLoc.sourceFile);
            int lineNum;
            // FEEFEE is some sort of illegal line number that is returned some time,  It is better to ignore it.  
            // and take the next valid line
            for (;;)
            {
                lineNum = (int)sourceLoc.lineNumber;
                if (lineNum != 0xFEEFEE)
                    break;
                lineNum = 0;
                sourceLocs.Next(1, out sourceLoc, out fetchCount);
                if (fetchCount == 0)
                    break;
            }

            var sourceLocation = new SourceLocation(sourceFile, lineNum);
            m_reader.m_log.WriteLine("SourceLocationForManaged: found source linenum {0} file {1}", lineNum, sourceFile.BuildTimeFilePath);
            return sourceLocation;
        }

        /// <summary>
        /// The symbol representing the module as a whole.  All global symbols are children of this symbol 
        /// </summary>
        public Symbol GlobalSymbol { get { return new Symbol(this, m_session.globalScope); } }

#if TEST_FIRST
        /// <summary>
        /// Returns a list of all source files referenced in the PDB
        /// </summary>
        public IEnumerable<SourceFile> AllSourceFiles()
        {

            IDiaEnumTables tables;
            m_session.getEnumTables(out tables);

            IDiaEnumSourceFiles sourceFiles;
            IDiaTable table = null;
            uint fetchCount = 0;
            for (; ; )
            {
                tables.Next(1, ref table, ref fetchCount);
                if (fetchCount == 0)
                    return null;
                sourceFiles = table as IDiaEnumSourceFiles;
                if (sourceFiles != null)
                    break;
            }

            var ret = new List<SourceFile>();
            IDiaSourceFile sourceFile = null;
            for (; ; )
            {
                sourceFiles.Next(1, out sourceFile, out fetchCount);
                if (fetchCount == 0)
                    break;
                ret.Add(new SourceFile(this, sourceFile));
            }
            return ret;
        }
#endif

        /// <summary>
        /// The a unique identifier that is used to relate the DLL and its PDB.   
        /// </summary>
        public Guid PdbGuid { get { return m_session.globalScope.guid; } }
        /// <summary>
        /// Along with the PdbGuid, there is a small integer 
        /// call the age is also used to find the PDB (it represents the different 
        /// post link transformations the DLL has undergone).  
        /// </summary>
        public int PdbAge { get { return (int)m_session.globalScope.age; } }

        /// <summary>
        /// The symbol reader this SymbolModule was created from.  
        /// </summary>
        public SymbolReader SymbolReader { get { return m_reader; } }

#region private

        private void Initialize(SymbolReader reader, string pdbFilePath, Action loadData)
        {
            m_pdbPath = pdbFilePath;
            this.m_reader = reader;

            m_source = DiaLoader.GetDiaSourceObject();
            loadData();
            m_source.openSession(out m_session);
            m_session.getSymbolsByAddr(out m_symbolsByAddr);

            m_reader.m_log.WriteLine("Opening PDB {0} with signature GUID {1} Age {2}", pdbFilePath, PdbGuid, PdbAge);
        }

        internal SymbolModule(SymbolReader reader, string pdbFilePath)
        {
            Initialize(reader, pdbFilePath, () => m_source.loadDataFromPdb(pdbFilePath));
        }

        internal SymbolModule(SymbolReader reader, string pdbFilePath, Stream pdbStream)
        {
            IStream comStream = new ComStreamWrapper(pdbStream);
            Initialize(reader, pdbFilePath, () => m_source.loadDataFromIStream(comStream));
        }

        internal void LogManagedInfo(string pdbName, Guid pdbGuid, int pdbAge)
        {
            // Simply remember this if we decide we need it for source server support
            m_managedPdbName = pdbName;
            m_managedPdbGuid = pdbGuid;
            m_managedPdbAge = pdbAge;
        }

        /// <summary>
        /// Gets the 'srcsvc' data stream from the PDB and return it in as a string.   Returns null if it is not present. 
        /// 
        /// There is a tool called pdbstr associated with srcsrv that basically does this.  
        ///     pdbstr -r -s:srcsrv -p:PDBPATH
        /// will dump it. 
        /// </summary>
        internal string GetSrcSrvStream()
        {
            // In order to get the IDiaDataSource3 which includes'getStreamSize' API, you need to use the 
            // dia2_internal.idl file from devdiv to produce the Interop.Dia2Lib.dll 
            // see class DiaLoader for more
            var log = m_reader.m_log;
            log.WriteLine("Getting source server stream for PDB {0}", SymbolFilePath);
            uint len = 0;
            m_source.getStreamSize("srcsrv", out len);
            if (len == 0)
            {
                if (0 <= SymbolFilePath.IndexOf(".ni.", StringComparison.OrdinalIgnoreCase))
                    log.WriteLine("Error, trying to look up source information on an NGEN file, giving up");
                else
                    log.WriteLine("Pdb {0} does not have source server information (srcsrv stream) in it", SymbolFilePath);
                return null;
            }

            byte[] buffer = new byte[len];
            fixed (byte* bufferPtr = buffer)
            {
                m_source.getStreamRawData("srcsrv", len, out *bufferPtr);
                var ret = UTF8Encoding.Default.GetString(buffer);
                return ret;
            }
        }

        // returns the path of the PDB that has source server information in it (which for NGEN images is the PDB for the managed image)
        internal SymbolModule PdbForSourceServer
        {
            get
            {
                if (m_managedPdbName == null)
                    return this;

                if (!m_managedPdbAttempted)
                {
                    m_reader.m_log.WriteLine("We have a NGEN image with an IL PDB {0}, looking it up", m_managedPdbName);
                    m_managedPdbAttempted = true;
                    var managedPdbPath = m_reader.FindSymbolFilePath(m_managedPdbName, m_managedPdbGuid, m_managedPdbAge);
                    if (managedPdbPath != null)
                    {
                        m_reader.m_log.WriteLine("Found managed PDB path {0}", managedPdbPath);
                        m_managedPdb = m_reader.OpenSymbolFile(managedPdbPath);
                    }
                    else
                        m_reader.m_log.WriteLine("Could not find managed PDB {0}", m_managedPdbName);
                }
                return m_managedPdb;
            }
        }

        /// <summary>
        /// For Project N modules it returns the list of pre merged IL assemblies and the corresponding mapping.
        /// </summary>
        [Obsolete("This is experimental, you should not use it yet for non-experimental purposes.")]
        public Dictionary<int, string> GetMergedAssembliesMap()
        {
            if (m_mergedAssemblies == null && !m_checkedForMergedAssemblies)
            {
                IDiaEnumInputAssemblyFiles diaMergedAssemblyRecords;
                m_session.findInputAssemblyFiles(out diaMergedAssemblyRecords);
                foreach (IDiaInputAssemblyFile inputAssembly in diaMergedAssemblyRecords)
                {
                    int index = (int)inputAssembly.index;
                    string assemblyName = inputAssembly.fileName;

                    if (m_mergedAssemblies == null)
                        m_mergedAssemblies = new Dictionary<int, string>();
                    m_mergedAssemblies.Add(index, assemblyName);
                }
                m_checkedForMergedAssemblies = true;
            }
            return m_mergedAssemblies;
        }

        /// <summary>
        /// For ProjectN modules, gets the merged IL image embedded in the .PDB (only valid for single-file compilation)
        /// </summary>
        public MemoryStream GetEmbeddedILImage()
        {
            try
            {
                uint ilimageSize;
                m_source.getStreamSize("ilimage", out ilimageSize);
                if (ilimageSize > 0)
                {
                    byte[] ilImage = new byte[ilimageSize];
                    m_source.getStreamRawData("ilimage", ilimageSize, out ilImage[0]);
                    return new MemoryStream(ilImage);
                }
            }
            catch (COMException)
            {
            }

            return null;
        }

        /// <summary>
        /// For ProjectN modules, gets the pseudo-assembly embedded in the .PDB, if there is one.
        /// </summary>
        /// <returns></returns>
        public MemoryStream GetPseudoAssembly()
        {
            try
            {
                uint ilimageSize;
                m_source.getStreamSize("pseudoil", out ilimageSize);
                if (ilimageSize > 0)
                {
                    byte[] ilImage = new byte[ilimageSize];
                    m_source.getStreamRawData("pseudoil", ilimageSize, out ilImage[0]);
                    return new MemoryStream(ilImage, writable: false);
                }
            }
            catch (COMException)
            {
            }

            return null;
        }

        /// <summary>
        /// For ProjectN modules, gets the binary blob that describes the mapping from RVAs to methods.
        /// </summary>
        public byte[] GetFuncMDTokenMap()
        {
            uint mapSize;
            m_session.getFuncMDTokenMapSize(out mapSize);

            byte[] buf = new byte[mapSize];
            fixed (byte* pBuf = buf)
            {
                m_session.getFuncMDTokenMap((uint)buf.Length, out mapSize, out buf[0]);
                Debug.Assert(mapSize == buf.Length);
            }

            return buf;
        }

        /// <summary>
        /// For ProjectN modules, gets the binary blob that describes the mapping from RVAs to types.
        /// </summary>
        /// <returns></returns>
        public byte[] GetTypeMDTokenMap()
        {
            uint mapSize;
            m_session.getTypeMDTokenMapSize(out mapSize);

            byte[] buf = new byte[mapSize];
            fixed (byte* pBuf = buf)
            {
                m_session.getTypeMDTokenMap((uint)buf.Length, out mapSize, out buf[0]);
                Debug.Assert(mapSize == buf.Length);
            }

            return buf;
        }

        bool m_checkedForMergedAssemblies;
        Dictionary<int, string> m_mergedAssemblies;

        private string m_managedPdbName;
        private Guid m_managedPdbGuid;
        private int m_managedPdbAge;
        private SymbolModule m_managedPdb;
        private bool m_managedPdbAttempted;

        internal SymbolReader m_reader;
        internal IDiaSession m_session;
        IDiaDataSource3 m_source;
        IDiaEnumSymbolsByAddr m_symbolsByAddr;
        string m_pdbPath;

#endregion
    }

    /// <summary>
    /// Represents a single symbol in a PDB file.  
    /// </summary>
    public class Symbol : IComparable<Symbol>
    {
        /// <summary>
        /// The name for the symbol 
        /// </summary>
        public string Name { get { return m_name; } }
        /// <summary>
        /// The relative virtual address (offset from the image base when loaded in memory) of the symbol
        /// </summary>
        public uint RVA { get { return m_diaSymbol.relativeVirtualAddress; } }
        /// <summary>
        /// The length of the memory that the symbol represents.  
        /// </summary>
        public ulong Length { get { return m_diaSymbol.length; } }
        /// <summary>
        /// A small integer identifier tat is unique for that symbol in the DLL. 
        /// </summary>
        public uint Id { get { return m_diaSymbol.symIndexId; } }

        /// <summary>
        /// Decorated names are names that most closely resemble the source code (have overloading).  
        /// However when the linker does not directly support all the expressiveness of the
        /// source language names are encoded to represent this.   This return this encoded name. 
        /// </summary>
        public string UndecoratedName
        {
            get
            {
                const uint UNDNAME_NO_PTR64 = 0x20000;
                string undecoratedName;
                m_diaSymbol.get_undecoratedNameEx(UNDNAME_NO_PTR64, out undecoratedName);

                return undecoratedName ?? m_diaSymbol.name;
            }
        }

        /// <summary>
        /// Returns true if the two symbols live in the same linker section (e.g. text,  data ...)
        /// </summary>
        public static bool InSameSection(Symbol a, Symbol b)
        {
            return a.m_diaSymbol.addressSection == b.m_diaSymbol.addressSection;
        }

        /// <summary>
        /// Returns the children of the symbol.  Will return null if there are no children.  
        /// </summary>
        public IEnumerable<Symbol> GetChildren()
        {
            return GetChildren(SymTagEnum.SymTagNull);
        }

        /// <summary>
        /// Returns the children of the symbol, with the given tag.  Will return null if there are no children.  
        /// </summary>
        public IEnumerable<Symbol> GetChildren(SymTagEnum tag)
        {
            IDiaEnumSymbols symEnum = null;
            m_module.m_session.findChildren(m_diaSymbol, tag, null, 0, out symEnum);
            if (symEnum == null)
                return null;

            uint fetchCount;
            var ret = new List<Symbol>();
            for (;;)
            {
                IDiaSymbol sym;
                symEnum.Next(1, out sym, out fetchCount);
                if (fetchCount == 0)
                    break;
                SymTagEnum symTag = (SymTagEnum)sym.symTag;
                ret.Add(new Symbol(m_module, sym));
            }

            return ret;
        }

        /// <summary>
        /// Compares the symbol by their relative virtual address (RVA)
        /// </summary>
        public int CompareTo(Symbol other)
        {
            return ((int)RVA - (int)other.RVA);
        }
#region private
#if false
        // TODO FIX NOW use or remove
        internal enum NameSearchOptions
        {
            nsNone,
            nsfCaseSensitive = 0x1,
            nsfCaseInsensitive = 0x2,
            nsfFNameExt = 0x4,                  // treat as a file path
            nsfRegularExpression = 0x8,         // * and ? wildcards
            nsfUndecoratedName = 0x10,          // A undecorated name is the name you see in the source code.  
        };
#endif

        /// <summary>
        /// override
        /// </summary>
        public override string ToString()
        {
            return string.Format("Symbol({0}, RVA=0x{1:x}", Name, RVA);
        }

        internal Symbol(SymbolModule module, IDiaSymbol diaSymbol)
        {
            m_module = module;
            m_diaSymbol = diaSymbol;
            m_name = m_diaSymbol.name;
        }
        private string m_name;
        private IDiaSymbol m_diaSymbol;
        private SymbolModule m_module;
#endregion
    }


    /// <summary>
    /// SymbolReaderFlags indicates preferences on how aggressively symbols should be looked up.  
    /// </summary>
    [Flags]
    public enum SymbolReaderOptions
    {
        /// <summary>
        /// No options this is the common case, where you want to look up everything you can. 
        /// </summary>
        None = 0,
        /// <summary>
        /// Only fetch the PDB if it lives in the symbolCacheDirectory (is local an is generated).  
        /// This will generate NGEN pdbs unless the NoNGenPDBs flag is set. 
        /// </summary>
        CacheOnly = 1,
        /// <summary>
        /// No NGEN PDB generation.  
        /// </summary>
        NoNGenSymbolCreation = 2,
    }

    /// <summary>
    /// A source file represents a source file from a PDB.  This is not just a string
    /// because the file has a build time path, a checksum, and it needs to be 'smart'
    /// to copy down the file if requested.  
    /// </summary>
    public class SourceFile
    {
        /// <summary>
        /// The path of the file at the time the source file was built. 
        /// </summary>
        public string BuildTimeFilePath { get; internal set; }

        /// <summary>
        /// If the source file is directly available on the web (that is there is a Url that 
        /// can be used to fetch it with HTTP Get), then return that Url.   If no such publishing 
        /// point exists this property will return null.   
        /// </summary>
        public string Url
        {
            get
            {
                string target, command;
                GetSourceServerTargetAndCommand(out target, out command);

                if (!string.IsNullOrEmpty(target) && Uri.IsWellFormedUriString(target, UriKind.Absolute))
                    return target;
                else
                    return null;
            }
        }

        /// <summary>
        /// true if the PDB has a checksum for the data in the source file. 
        /// </summary>
        public bool HasChecksum { get { return m_hashAlgorithm != null; } }

        /// <summary>
        /// This may fetch things from the source server, and thus can be very slow, which is why it is not a property. 
        /// </summary>
        /// <returns></returns>
        public string GetSourceFile(bool requireChecksumMatch = false)
        {
            m_checksumMatches = false;
            m_getSourceCalled = true;
            var log = m_symbolModule.m_reader.m_log;
            string bestGuess = null;

            // Did we build on this machine?  
            if (File.Exists(BuildTimeFilePath))
            {
                bestGuess = BuildTimeFilePath;
                m_checksumMatches = DoesChecksumMatch(BuildTimeFilePath);
                if (m_checksumMatches)
                {
                    log.WriteLine("Found in build location.");
                    return BuildTimeFilePath;
                }
            }
            log.WriteLine("Not present at build location {0}, trying source server.", BuildTimeFilePath);

            // Try the source server 
            var ret = GetSourceFromSrcServer();
            if (ret != null)
            {
                log.WriteLine("Got source from source server.");
                m_checksumMatches = true;       // TODO we assume source server is right, is that OK? 
                return ret;
            }
            log.WriteLine("Not present on source server, looking on NT_SOURCE_PATH.");

            // Try _NT_SOURCE_PATH
            var locations = m_symbolModule.m_reader.ParsedSourcePath;
            log.WriteLine("_NT_SOURCE_PATH={0}", m_symbolModule.m_reader.SourcePath);

            // If we know the exe path, add that to the search path.   
            if (m_symbolModule.ExePath != null)
            {
                var exeDir = Path.GetDirectoryName(m_symbolModule.ExePath);
                if (Directory.Exists(exeDir))
                {
                    // Add directories up the path, we stop somewhat arbitrarily at 3 
                    for (int i = 0; i < 3; i++)
                    {
                        locations.Insert(0, exeDir);
                        log.WriteLine("Adding Exe path {0}", exeDir);

                        exeDir = Path.GetDirectoryName(exeDir);
                        if (exeDir == null)
                            break;
                    }
                }
            }

            var curIdx = 0;
            for (;;)
            {
                var sepIdx = BuildTimeFilePath.IndexOf('\\', curIdx);
                if (sepIdx < 0)
                    break;
                curIdx = sepIdx + 1;
                var tail = BuildTimeFilePath.Substring(sepIdx);

                foreach (string location in locations)
                {
                    var probe = location + tail;
                    log.WriteLine("Probing {0}", probe);
                    if (File.Exists(probe))
                    {
                        if (bestGuess == null)
                            bestGuess = probe;
                        m_checksumMatches = DoesChecksumMatch(probe);
                        if (m_checksumMatches)
                        {
                            log.WriteLine("Success {0}", probe);
                            return probe;
                        }
                        else
                            log.WriteLine("Found file {0} but checksum mismatches", probe);
                    }
                }
            }

            if (!requireChecksumMatch && bestGuess != null)
            {
                log.WriteLine("[Warning: Checksum mismatch for {0}]", bestGuess);
                return bestGuess;
            }

            log.WriteLine("[Could not find source for {0}]", BuildTimeFilePath);
            return null;
        }

        /// <summary>
        /// If GetSourceFile is called and 'requireChecksumMatch' == false then you can call this property to 
        /// determine if the checksum actually matched or not.   This will return true if the original
        /// PDB does not have a checksum (HasChecksum == false)
        /// </summary>
        public bool ChecksumMatches
        {
            get
            {
                Debug.Assert(m_getSourceCalled);
                return m_checksumMatches;
            }
        }

#region private
        /// <summary>
        /// Parse the 'srcsrv' stream in a PDB file and return the target for SourceFile
        /// represented by the 'this' pointer.   This target is iether a ULR or a local file
        /// path.  
        /// 
        /// You can dump the srcsrv stream using a tool called pdbstr 
        ///     pdbstr -r -s:srcsrv -p:PDBPATH
        /// 
        /// The target in this stream is called SRCSRVTRG and there is another variable SRCSRVCMD
        /// which represents the command to run to fetch the soruce into SRCSRVTRG
        /// 
        /// To form the target, the stream expect you to private a %targ% variable which is a directory
        /// prefix to tell where to put the source file being fetched.   If the source file is
        /// available via a URL this variable is not needed.  
        /// 
        ///  ********* This is a typical example of what is in a PDB with source server information. 
        ///  SRCSRV: ini ------------------------------------------------
        ///  VERSION=3
        ///  INDEXVERSION=2
        ///  VERCTRL=Team Foundation Server
        ///  DATETIME=Thu Mar 10 16:15:55 2016
        ///  SRCSRV: variables ------------------------------------------
        ///  TFS_EXTRACT_CMD=tf.exe view /version:%var4% /noprompt "$%var3%" /server:%fnvar%(%var2%) /output:%srcsrvtrg%
        ///  TFS_EXTRACT_TARGET=%targ%\%var2%%fnbksl%(%var3%)\%var4%\%fnfile%(%var1%)
        ///  VSTFDEVDIV_DEVDIV2=http://vstfdevdiv.redmond.corp.microsoft.com:8080/DevDiv2
        ///  SRCSRVVERCTRL=tfs
        ///  SRCSRVERRDESC=access
        ///  SRCSRVERRVAR=var2
        ///  SRCSRVTRG=%TFS_extract_target%
        ///  SRCSRVCMD=%TFS_extract_cmd%
        ///  SRCSRV: source files ---------------------------------------
        ///  f:\dd\externalapis\legacy\vctools\vc12\inc\cvconst.h*VSTFDEVDIV_DEVDIV2*/DevDiv/Fx/Rel/NetFxRel3Stage/externalapis/legacy/vctools/vc12/inc/cvconst.h*1363200
        ///  f:\dd\externalapis\legacy\vctools\vc12\inc\cvinfo.h*VSTFDEVDIV_DEVDIV2*/DevDiv/Fx/Rel/NetFxRel3Stage/externalapis/legacy/vctools/vc12/inc/cvinfo.h*1363200
        ///  f:\dd\externalapis\legacy\vctools\vc12\inc\vc\ammintrin.h*VSTFDEVDIV_DEVDIV2*/DevDiv/Fx/Rel/NetFxRel3Stage/externalapis/legacy/vctools/vc12/inc/vc/ammintrin.h*1363200
        ///  SRCSRV: end ------------------------------------------------
        ///  
        ///  ********* And here is a more modern one where the source code is available via a URL.  
        ///  SRCSRV: ini ------------------------------------------------
        ///  VERSION=2
        ///  INDEXVERSION=2
        ///  VERCTRL=http
        ///  SRCSRV: variables ------------------------------------------
        ///  SRCSRVTRG=https://nuget.smbsrc.net/src/%fnfile%(%var1%)/%var2%/%fnfile%(%var1%)
        ///  SRCSRVCMD=
        ///  SRCSRVVERCTRL=http
        ///  SRCSRV: source files ---------------------------------------
        ///  c:\Users\rafalkrynski\Documents\Visual Studio 2012\Projects\DavidSymbolSourceTest\DavidSymbolSourceTest\Demo.cs*SQPvxWBMtvANyCp8Pd3OjoZEUgpKvjDVIY1WbaiFPMw=
        ///  SRCSRV: end ------------------------------------------------
        ///  
        /// </summary>
        /// <param name="target">returns the target source file path</param>
        /// <param name="command">returns the command to fetch the target source file</param>
        /// <param name="localDirectoryToPlaceSourceFiles">Specify the value for %targ% variable. This is the
        /// directory where source files can be fetched to.  Typically the returned file is under this directory
        /// If the value is null, %targ% variable be emtpy.  This assumes that the resulting file is something
        /// that does not need to be copied to the machine (either a URL or a file that already exists)</param>
        private void GetSourceServerTargetAndCommand(out string target, out string command, string localDirectoryToPlaceSourceFiles = null)
        {
            target = null;
            command = null;

            var log = m_symbolModule.m_reader.m_log;
            log.WriteLine("*** Looking up {0} using source server", BuildTimeFilePath);

            var srcServerPdb = m_symbolModule.PdbForSourceServer;
            if (srcServerPdb == null)
            {
                log.WriteLine("*** Could not find PDB to look up source server information");
                return;
            }

            string srcsvcStream = srcServerPdb.GetSrcSrvStream();
            if (srcsvcStream == null)
            {
                log.WriteLine("*** Could not find srcsrv stream in PDB file");
                return;
            }

            log.WriteLine("*** Found srcsrv stream in PDB file. of size {0}", srcsvcStream.Length);
            StringReader reader = new StringReader(srcsvcStream);

            bool inSrc = false;
            bool inVars = false;
            var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (localDirectoryToPlaceSourceFiles != null)
                vars.Add("targ", localDirectoryToPlaceSourceFiles);

            for (;;)
            {
                var line = reader.ReadLine();
                if (line == null)
                    break;

                // log.WriteLine("Got srcsrv line {0}", line);
                if (line.StartsWith("SRCSRV: "))
                {
                    inSrc = line.StartsWith("SRCSRV: source files");
                    inVars = line.StartsWith("SRCSRV: variables");
                    continue;
                }
                if (inSrc)
                {
                    var pieces = line.Split('*');
                    if (pieces.Length >= 2)
                    {
                        var buildTimePath = pieces[0];
                        // log.WriteLine("Found source {0} in the PDB", buildTimePath);
                        if (string.Compare(BuildTimeFilePath, buildTimePath, StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            // Create variables for each of the pieces.  
                            for (int i = 0; i < pieces.Length; i++)
                                vars.Add("var" + (i + 1).ToString(), pieces[i]);

                            target = SourceServerFetchVar("SRCSRVTRG", vars);
                            command = SourceServerFetchVar("SRCSRVCMD", vars);

                            return;
                        }
                    }
                }
                else if (inVars)
                {
                    // Gather up the KEY=VALUE pairs into a dictionary.  
                    var m = Regex.Match(line, @"^(\w+)=(.*?)\s*$");
                    if (m.Success)
                        vars[m.Groups[1].Value] = m.Groups[2].Value;
                }
            }
        }

        /// <summary>
        /// Try to fetch the source file associated with 'buildTimeFilePath' from the symbol server 
        /// information from the PDB from 'pdbPath'.   Will return a path to the returned file (uses 
        /// SourceCacheDirectory associated symbol reader for context where to put the file), 
        /// or null if unsuccessful.  
        /// 
        /// There is a tool called pdbstr associated with srcsrv that basically does this.  
        ///     pdbstr -r -s:srcsrv -p:PDBPATH
        /// will dump it. 
        ///
        /// The basic flow is 
        /// 
        /// There is a variables section and a files section
        /// 
        /// The file section is a list of items separated by *.   The first is the path, the rest are up to you
        /// 
        /// You form a command by using the SRCSRVTRG variable and substituting variables %var1 where var1 is the first item in the * separated list
        /// There are special operators %fnfile%(XXX), etc that manipulate the string XXX (get file name, translate \ to / ...
        /// 
        /// If what is at the end is a valid URL it is looked up.   
        /// </summary>
        string GetSourceFromSrcServer()
        {
            var log = m_symbolModule.m_reader.m_log;
            var cacheDir = m_symbolModule.m_reader.SourceCacheDirectory;

            string target, fetchCmdStr;
            GetSourceServerTargetAndCommand(out target, out fetchCmdStr, cacheDir);

            if (target != null)
            {
                if (!target.StartsWith(cacheDir, StringComparison.OrdinalIgnoreCase))
                {
                    // if target is not in cache dir, it means it's from a remote server.
                    Uri uri = null;
                    if (Uri.TryCreate(target, UriKind.Absolute, out uri))
                    {
                        target = null;
                        var newTarget = Path.Combine(cacheDir, uri.AbsolutePath.TrimStart('/').Replace('/', '\\'));
                        if (m_symbolModule.m_reader.GetPhysicalFileFromServer(uri.GetLeftPart(UriPartial.Authority), uri.AbsolutePath, newTarget))
                            target = newTarget;

                        if (target == null)
                        {
                            log.WriteLine("Could not fetch {0} from web", uri.AbsoluteUri);
                            return null;
                        }
                    }
                    else
                    {
                        log.WriteLine("Source Server string {0} is targeting an unsafe location.  Giving up.", target);
                        return null;
                    }
                }

                if (!File.Exists(target) && fetchCmdStr != null)
                {
                    log.WriteLine("Trying to generate the file {0}.", target);
                    var toolsDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().ManifestModule.FullyQualifiedName);
                    var archToolsDir = Path.Combine(toolsDir, NativeDlls.ProcessArchitectureDirectory);

                    // Find the EXE to do the source server fetch.  We only support SD.exe and TF.exe.   
                    string addToPath = null;
                    if (fetchCmdStr.StartsWith("sd.exe ", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!File.Exists(Path.Combine(archToolsDir, "sd.exe")))
                            log.WriteLine("WARNING: Could not find sd.exe that should have been deployed at {0}", archToolsDir);
                        addToPath = archToolsDir;
                    }
                    else
                    if (fetchCmdStr.StartsWith("tf.exe ", StringComparison.OrdinalIgnoreCase))
                    {
                        var tfExe = Command.FindOnPath("tf.exe");
                        if (tfExe == null)
                        {
                            tfExe = FindTfExe();
                            if (tfExe == null)
                            {
                                log.WriteLine("Could not find TF.exe, place it on the PATH environment variable to fix this.");
                                return null;
                            }
                            addToPath = Path.GetDirectoryName(tfExe);
                        }
                    }
                    else
                    {
                        log.WriteLine("Source Server command is not recognized as safe (sd.exe or tf.exe), failing.");
                        return null;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    fetchCmdStr = "cmd /c " + fetchCmdStr;
                    var options = new CommandOptions().AddOutputStream(log).AddNoThrow();
                    if (addToPath != null)
                        options = options.AddEnvironmentVariable("PATH", addToPath + ";%PATH%");

                    log.WriteLine("Source Server command {0}.", fetchCmdStr);
                    var fetchCmd = Command.Run(fetchCmdStr, options);
                    if (fetchCmd.ExitCode != 0)
                        log.WriteLine("Source Server command failed with exit code {0}", fetchCmd.ExitCode);
                    if (File.Exists(target))
                    {
                        // If TF.exe command files it might still create an empty output file.   Fix that 
                        if (new FileInfo(target).Length == 0)
                        {
                            File.Delete(target);
                            target = null;
                        }
                    }
                    else
                        target = null;

                    if (target == null)
                        log.WriteLine("Source Server command failed to produce the output file.");
                    else
                        log.WriteLine("Source Server command succeeded creating {0}", target);
                }
                else
                    log.WriteLine("Found an existing source server file {0}.", target);
                return target;
            }

            log.WriteLine("Did not find source file in the set of source files in the PDB.");
            return null;
        }

        /// <summary>
        /// Returns the location of the tf.exe executable or 
        /// </summary>
        /// <returns></returns>
        private static string FindTfExe()
        {
            // If you have VS installed used that TF.exe associated with that.  
            var progFiles = Environment.GetEnvironmentVariable("ProgramFiles (x86)");
            if (progFiles == null)
                progFiles = Environment.GetEnvironmentVariable("ProgramFiles");
            if (progFiles != null)
            {
                // Find the oldest Visual Studio directory;
                var dirs = Directory.GetDirectories(progFiles, "Microsoft Visual Studio*");
                Array.Sort(dirs);
                if (dirs.Length > 0)
                {
                    var VSDir = Path.Combine(dirs[dirs.Length - 1], @"Common7\IDE");
                    var tfexe = Path.Combine(VSDir, "tf.exe");
                    if (File.Exists(tfexe))
                        return tfexe;
                }
            }
            return null;
        }

        private string SourceServerFetchVar(string variable, Dictionary<string, string> vars)
        {
            var log = m_symbolModule.m_reader.m_log;
            string result = "";
            if (vars.TryGetValue(variable, out result))
            {
                if (0 <= result.IndexOf('%'))
                    log.WriteLine("SourceServerFetchVar: Before Evaluation {0} = '{1}'", variable, result);
                result = SourceServerEvaluate(result, vars);
            }
            log.WriteLine("SourceServerFetchVar: {0} = '{1}'", variable, result);
            return result;
        }

        private string SourceServerEvaluate(string result, Dictionary<string, string> vars)
        {
            if (0 <= result.IndexOf('%'))
            {
                // see http://msdn.microsoft.com/en-us/library/windows/desktop/ms680641(v=vs.85).aspx for details on the %fn* variables 
                result = Regex.Replace(result, @"%fnvar%\((.*?)\)", delegate (Match m)
                {
                    return SourceServerFetchVar(SourceServerEvaluate(m.Groups[1].Value, vars), vars);
                });
                result = Regex.Replace(result, @"%fnbksl%\((.*?)\)", delegate (Match m)
                {
                    return SourceServerEvaluate(m.Groups[1].Value, vars).Replace('/', '\\');
                });
                result = Regex.Replace(result, @"%fnfile%\((.*?)\)", delegate (Match m)
                {
                    return Path.GetFileName(SourceServerEvaluate(m.Groups[1].Value, vars));
                });
                // Normal variable substitution
                result = Regex.Replace(result, @"%(\w+)%", delegate (Match m)
                {
                    return SourceServerFetchVar(m.Groups[1].Value, vars);
                });
            }
            return result;
        }


        // Here is an example of the srcsrv stream.  
#if false
SRCSRV: ini ------------------------------------------------
VERSION=3
INDEXVERSION=2
VERCTRL=Team Foundation Server
DATETIME=Wed Nov 28 03:47:14 2012
SRCSRV: variables ------------------------------------------
TFS_EXTRACT_CMD=tf.exe view /version:%var4% /noprompt "$%var3%" /server:%fnvar%(%var2%) /console >%srcsrvtrg%
TFS_EXTRACT_TARGET=%targ%\%var2%%fnbksl%(%var3%)\%var4%\%fnfile%(%var1%)
SRCSRVVERCTRL=tfs
SRCSRVERRDESC=access
SRCSRVERRVAR=var2
DEVDIV_TFS2=http://vstfdevdiv.redmond.corp.microsoft.com:8080/devdiv2
SRCSRVTRG=%TFS_extract_target%
SRCSRVCMD=%TFS_extract_cmd%
SRCSRV: source files ---------------------------------------
f:\dd\ndp\clr\src\vm\i386\gmsasm.asm*DEVDIV_TFS2*/DevDiv/D11RelS/FX45RTMGDR/ndp/clr/src/VM/i386/gmsasm.asm*592925
f:\dd\ndp\clr\src\vm\i386\jithelp.asm*DEVDIV_TFS2*/DevDiv/D11RelS/FX45RTMGDR/ndp/clr/src/VM/i386/jithelp.asm*592925
f:\dd\ndp\clr\src\vm\i386\RedirectedHandledJITCase.asm*DEVDIV_TFS2*/DevDiv/D11RelS/FX45RTMGDR/ndp/clr/src/VM/i386/RedirectedHandledJITCase.asm*592925
f:\dd\public\devdiv\inc\ddbanned.h*DEVDIV_TFS2*/DevDiv/D11RelS/FX45RTMGDR/public/devdiv/inc/ddbanned.h*592925
f:\dd\ndp\clr\src\debug\ee\i386\dbghelpers.asm*DEVDIV_TFS2*/DevDiv/D11RelS/FX45RTMGDR/ndp/clr/src/Debug/EE/i386/dbghelpers.asm*592925
SRCSRV: end ------------------------------------------------
      
        // Here is one for SD. 

SRCSRV: ini ------------------------------------------------
VERSION=1
VERCTRL=Source Depot
SRCSRV: variables ------------------------------------------
SRCSRVTRG=%targ%\%var2%\%fnbksl%(%var3%)\%var4%\%fnfile%(%var1%)
SRCSRVCMD=sd.exe -p %fnvar%(%var2%) print -o %srcsrvtrg% -q %depot%/%var3%#%var4%
DEPOT=//depot
SRCSRVVERCTRL=sd
SRCSRVERRDESC=Connect to server failed
SRCSRVERRVAR=var2
WIN_MINKERNEL=minkerneldepot.sys-ntgroup.ntdev.microsoft.com:2020
WIN_PUBLIC=publicdepot.sys-ntgroup.ntdev.microsoft.com:2017
WIN_PUBLICINT=publicintdepot.sys-ntgroup.ntdev.microsoft.com:2018
SRCSRV: source files ---------------------------------------
d:\win7sp1_gdr.public.amd64fre\sdk\inc\pshpack4.h*WIN_PUBLIC*win7sp1_gdr/public/sdk/inc/pshpack4.h*1
d:\win7sp1_gdr.public.amd64fre\internal\minwin\priv_sdk\inc\ntos\pnp.h*WIN_PUBLICINT*win7sp1_gdr/publicint/minwin/priv_sdk/inc/ntos/pnp.h*1
d:\win7sp1_gdr.public.amd64fre\internal\minwin\priv_sdk\inc\ntos\cm.h*WIN_PUBLICINT*win7sp1_gdr/publicint/minwin/priv_sdk/inc/ntos/cm.h*1
d:\win7sp1_gdr.public.amd64fre\internal\minwin\priv_sdk\inc\ntos\pnp_x.h*WIN_PUBLICINT*win7sp1_gdr/publicint/minwin/priv_sdk/inc/ntos/pnp_x.h*2
SRCSRV: end ------------------------------------------------


#endif
#if false
        // Here is ana example of the stream in use for the jithlp.asm file.  

f:\dd\ndp\clr\src\vm\i386\jithelp.asm*DEVDIV_TFS2*/DevDiv/D11RelS/FX45RTMGDR/ndp/clr/src/VM/i386/jithelp.asm*592925

        // Here is the command that it issues.  
tf.exe view /version:592925 /noprompt "$/DevDiv/D11RelS/FX45RTMGDR/ndp/clr/src/VM/i386/jithelp.asm" /server:http://vstfdevdiv.redmond.corp.microsoft.com:8080/devdiv2 /console >"C:\Users\vancem\AppData\Local\Temp\PerfView\src\DEVDIV_TFS2\DevDiv\D11RelS\FX45RTMGDR\ndp\clr\src\VM\i386\jithelp.asm\592925\jithelp.asm"

sd.exe -p minkerneldepot.sys-ntgroup.ntdev.microsoft.com:2020 print -o "C:\Users\vancem\AppData\Local\Temp\PerfView\src\WIN_MINKERNEL\win8_gdr\minkernel\ntdll\rtlstrt.c\1\rtlstrt.c" -q //depot/win8_gdr/minkernel/ntdll/rtlstrt.c#1

#endif

        private bool DoesChecksumMatch(string filePath)
        {
            if (!HasChecksum)
                return true;

            byte[] checksum = ComputeHash(filePath);
            if (checksum.Length != m_hash.Length)
                return false;
            for (int i = 0; i < checksum.Length; i++)
                if (checksum[i] != m_hash[i])
                    return false;
            return true;
        }

        unsafe internal SourceFile(SymbolModule module, IDiaSourceFile sourceFile)
        {
            m_symbolModule = module;
            BuildTimeFilePath = sourceFile.fileName;

            // 0 No checksum present.
            // 1 CALG_MD5 checksum generated with the MD5 hashing algorithm.
            // 2 CALG_SHA1 checksum generated with the SHA1 hashing algorithm.
            // 3 checksum generated with the SHA256 hashing algorithm.
            m_hashType = sourceFile.checksumType;
            SetCryptoProvider();

            if (HasChecksum)
            {
                uint hashSizeInBytes;
                fixed (byte* bufferPtr = m_hash)
                    sourceFile.get_checksum(0, out hashSizeInBytes, out *bufferPtr);

                // MD5 is 16 bytes
                // SHA1 is 20 bytes  
                // SHA-256 is 32 bytes
                m_hash = new byte[hashSizeInBytes];

                uint bytesFetched;
                fixed (byte* bufferPtr = m_hash)
                    sourceFile.get_checksum((uint)m_hash.Length, out bytesFetched, out *bufferPtr);
                Debug.Assert(bytesFetched == m_hash.Length);
            }
        }

        private void SetCryptoProvider()
        {
            switch (m_hashType)
            {
                case 1:
                    m_hashAlgorithm = new System.Security.Cryptography.MD5CryptoServiceProvider();
                    break;

                case 2:
                    m_hashAlgorithm = new System.Security.Cryptography.SHA1CryptoServiceProvider();
                    break;

                case 3: 
                    m_hashAlgorithm = new System.Security.Cryptography.SHA256CryptoServiceProvider();
                    break;

                default:
                    m_hashAlgorithm = null; // unknown hash type
                    break;
            }
        }

        private System.Security.Cryptography.HashAlgorithm GetCryptoProvider()
        {
            return m_hashAlgorithm;
        }

        private byte[] ComputeHash(string filePath)
        {
            Debug.Assert(m_hashAlgorithm != null);

            using (var fileStream = File.OpenRead(filePath))
                return m_hashAlgorithm.ComputeHash(fileStream);
        }

        SymbolModule m_symbolModule;
        uint m_hashType;
        byte[] m_hash;
        System.Security.Cryptography.HashAlgorithm m_hashAlgorithm;
        bool m_getSourceCalled;
        bool m_checksumMatches;
#endregion
    }

    /// <summary>
    /// A SourceLocation represents a point in the source code.  That is the file and the line number.  
    /// </summary>
    public class SourceLocation
    {
        /// <summary>
        /// The source file for the code
        /// </summary>
        public SourceFile SourceFile { get; private set; }
        /// <summary>
        /// The line number for the code.
        /// </summary>
        public int LineNumber { get; private set; }
#region private
        internal SourceLocation(SourceFile sourceFile, int lineNumber)
        {
            // The library seems to see FEEFEE for the 'unknown' line number.  0 seems more intuitive
            if (0xFEEFEE <= lineNumber)
                lineNumber = 0;

            SourceFile = sourceFile;
            LineNumber = lineNumber;
        }
#endregion
    }
}

#region private classes

internal sealed class ComStreamWrapper : IStream
{
    private readonly Stream stream;

    public ComStreamWrapper(Stream stream)
    {
        this.stream = stream;
    }

    public void Commit(uint grfCommitFlags)
    {
        throw new NotSupportedException();
    }

    public unsafe void RemoteRead(out byte pv, uint cb, out uint pcbRead)
    {
        byte[] buf = new byte[cb];

        int bytesRead = stream.Read(buf, 0, (int)cb);
        pcbRead = (uint)bytesRead;

        fixed (byte* p = &pv)
        {
            for (int i = 0; i < bytesRead; i++)
                p[i] = buf[i];
        }
    }

    public unsafe void RemoteSeek(_LARGE_INTEGER dlibMove, uint origin, out _ULARGE_INTEGER plibNewPosition)
    {
        long newPosition = stream.Seek(dlibMove.QuadPart, (SeekOrigin)origin);
        plibNewPosition.QuadPart = (ulong)newPosition;
    }

    public void SetSize(_ULARGE_INTEGER libNewSize)
    {
        throw new NotSupportedException();
    }

    public void Stat(out tagSTATSTG pstatstg, uint grfStatFlag)
    {
        pstatstg = new tagSTATSTG()
        {
            cbSize = new _ULARGE_INTEGER() { QuadPart = (ulong)stream.Length }
        };
    }

    public unsafe void RemoteWrite(ref byte pv, uint cb, out uint pcbWritten)
    {
        throw new NotSupportedException();
    }

    public void Clone(out IStream ppstm)
    {
        throw new NotSupportedException();
    }

    public void RemoteCopyTo(IStream pstm, _ULARGE_INTEGER cb, out _ULARGE_INTEGER pcbRead, out _ULARGE_INTEGER pcbWritten)
    {
        throw new NotSupportedException();
    }

    public void LockRegion(_ULARGE_INTEGER libOffset, _ULARGE_INTEGER cb, uint lockType)
    {
        throw new NotSupportedException();
    }

    public void Revert()
    {
        throw new NotSupportedException();
    }

    public void UnlockRegion(_ULARGE_INTEGER libOffset, _ULARGE_INTEGER cb, uint lockType)
    {
        throw new NotSupportedException();
    }
}

namespace Dia2Lib
{
    /// <summary>
    /// The DiaLoader class knows how to load the msdia140.dll (the Debug Access Interface) (see docs at
    /// http://msdn.microsoft.com/en-us/library/x93ctkx8.aspx), without it being registered as a COM object.
    /// Basically it just called the DllGetClassObject interface directly.
    /// 
    /// It has one public method 'GetDiaSourceObject' which knows how to create a IDiaDataSource object. 
    /// From there you can do anything you need.  
    /// 
    /// In order to get IDiaDataSource3 which includes'getStreamSize' API, you need to use the 
    /// vctools\langapi\idl\dia2_internal.idl file from devdiv to produce Interop.Dia2Lib.dll
    /// 
    /// roughly what you need to do is 
    ///     copy vctools\langapi\idl\dia2_internal.idl .
    ///     copy vctools\langapi\idl\dia2.idl .
    ///     copy vctools\langapi\include\cvconst.h .
    ///     Change dia2.idl to include interface IDiaDataSource3 inside library Dia2Lib->importlib->coclass DiaSource
    ///     midl dia2_internal.idl /D CC_DP_CXX
    ///     tlbimp dia2_internal.tlb
    ///     xcopy Dia2Lib.dll Interop.Dia2Lib.dll
    /// </summary>
    internal static class DiaLoader
    {
        /// <summary>
        /// Load the msdia100 dll and get a IDiaDataSource from it.  This is your gateway to PDB reading.   
        /// </summary>
        public static IDiaDataSource3 GetDiaSourceObject()
        {
            if (!s_loadedNativeDll)
            {
                // Insure that the native DLL we need exist.  
                NativeDlls.LoadNative("msdia140.dll");
                s_loadedNativeDll = true;
            }

            // This is the value it was for msdia120 and before 
            // var diaSourceClassGuid = new Guid("{3BFCEA48-620F-4B6B-81F7-B9AF75454C7D}");

            // This is the value for msdia140.  
            var diaSourceClassGuid = new Guid("{e6756135-1e65-4d17-8576-610761398c3c}");
            var comClassFactory = (IClassFactory)DllGetClassObject(diaSourceClassGuid, typeof(IClassFactory).GUID);

            object comObject = null;
            Guid iDataDataSourceGuid = typeof(IDiaDataSource3).GUID;
            comClassFactory.CreateInstance(null, ref iDataDataSourceGuid, out comObject);
            return (comObject as IDiaDataSource3);
        }
#region private
        [ComImport, ComVisible(false), Guid("00000001-0000-0000-C000-000000000046"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IClassFactory
        {
            void CreateInstance([MarshalAs(UnmanagedType.Interface)] object aggregator,
                                ref Guid refiid,
                                [MarshalAs(UnmanagedType.Interface)] out object createdObject);
            void LockServer(bool incrementRefCount);
        }

        // Methods
        [return: MarshalAs(UnmanagedType.Interface)]
        [DllImport("msdia140.dll", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = false)]
        private static extern object DllGetClassObject(
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid rclsid,
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid);

        /// <summary>
        /// Used to ensure the native library is loaded at least once prior to trying to use it. No protection is
        /// included to avoid multiple loads, but this is not a problem since we aren't trying to unload the library
        /// after use.
        /// </summary>
        static bool s_loadedNativeDll;
#endregion
    }
}
#endregion

