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
            SymbolPath = nt_symbol_path;
            if (SymbolPath == null)
                SymbolPath = Microsoft.Diagnostics.Symbols.SymbolPath.SymbolPathFromEnvironment;
            log.WriteLine("Created SymbolReader with SymbolPath {0}", nt_symbol_path);

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
            // log.WriteLine("Morphed Symbol Path: {0}", newSymPathStr);

            this.m_log = log;
        }

        // These routines find a PDB based on something (either an DLL or a PDB 'signature')
        /// <summary>
        /// Finds the symbol file for 'exeFilePath' that exists on the current machine (we open
        /// it to find the needed info).   Uses the SymbolReader.SymbolPath (including Symbol servers) to 
        /// look up the PDB, and will download the PDB to the local cache if necessary.   It will also
        /// generate NGEN pdbs into the local symbol cache unless SymbolReaderFlags.NoNGenPDB is set.   
        /// 
        /// Returns null if the pdb can't be found.  
        /// </summary>
        public string FindSymbolFilePathForModule(string dllFilePath)
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
                        if (peFile.GetPdbSignature(out pdbName, out pdbGuid, out pdbAge, true))
                        {
                            string fileVersionString = null;
                            var fileVersion = peFile.GetFileVersionInfo();
                            if (fileVersion != null)
                                fileVersionString = fileVersion.FileVersion;

                            var ret = FindSymbolFilePath(pdbName, pdbGuid, pdbAge, dllFilePath, fileVersionString);
                            if (ret == null && 0 <= dllFilePath.IndexOf(".ni.", StringComparison.OrdinalIgnoreCase))
                            {
                                if ((Options & SymbolReaderOptions.NoNGenSymbolCreation) != 0)
                                    m_log.WriteLine("FindSymbolFilePathForModule: Could not find NGEN image, NoNGenPdb set, giving up.");
                                else
                                {
                                    m_log.WriteLine("FindSymbolFilePathForModule: Could not find PDB for NGEN image, Trying to generate it.");
                                    ret = GenerateNGenSymbolsForModule(Path.GetFullPath(dllFilePath));
                                }
                            }
                            m_log.WriteLine("FindSymbolFilePathForModule returns {0}", ret ?? "NULL");
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
            m_log.WriteLine("FindSymbolFilePath: *{{ Locating PDB {0} GUID {1} Age {2} Version {3}", pdbFileName, pdbIndexGuid, pdbIndexAge, fileVersion);
            if (dllFilePath != null)
                m_log.WriteLine("FindSymbolFilePath: Pdb is for DLL {0}", dllFilePath);

            string pdbPath = null;
            string pdbIndexPath = null;
            string pdbSimpleName = Path.GetFileName(pdbFileName);        // Make sure the simple name is really a simple name

            SymbolPath path = new SymbolPath(this.SymbolPath);
            foreach (SymbolPathElement element in path.Elements)
            {
                // TODO can do all of these concurrently now.   
                if (element.IsSymServer)
                {
                    if (pdbIndexPath == null)
                        pdbIndexPath = pdbSimpleName + @"\" + pdbIndexGuid.ToString("N") + pdbIndexAge.ToString() + @"\" + pdbSimpleName;
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
                        if (PdbMatches(filePath, pdbIndexGuid, pdbIndexAge))
                            pdbPath = filePath;
                    }
                    else
                        m_log.WriteLine("FindSymbolFilePath: location {0} is remote and cacheOnly set, giving up.", filePath);
                }
                if (pdbPath != null)
                    break;
            }

            // We check these last because they may be hostile PDBs and we have to ask the user about them.
            // If we have a dllPath, look right beside it, or in a directory symbols.pri\retail\dll
            if (pdbPath == null && dllFilePath != null)        // Check next to the file. 
            {
                m_log.WriteLine("FindSymbolFilePath: Checking relative to DLL path {0}", dllFilePath);
                string pdbPathCandidate = Path.ChangeExtension(dllFilePath, ".pdb");
                if (PdbMatches(pdbPathCandidate, pdbIndexGuid, pdbIndexAge) && CheckSecurity(pdbPathCandidate))
                    pdbPath = pdbPathCandidate;

                // Also try the symbols.pri\retail\dll convention that windows and devdiv use
                if (pdbPath == null)
                {
                    pdbPathCandidate = Path.Combine(
                        Path.GetDirectoryName(dllFilePath), @"symbols.pri\retail\dll\" +
                        Path.GetFileNameWithoutExtension(dllFilePath) + ".pdb");
                    if (PdbMatches(pdbPathCandidate, pdbIndexGuid, pdbIndexAge) && CheckSecurity(pdbPathCandidate))
                        pdbPath = pdbPathCandidate;
                }

                if (pdbPath == null)
                {
                    pdbPathCandidate = Path.Combine(
                        Path.GetDirectoryName(dllFilePath), @"symbols\retail\dll\" +
                        Path.GetFileNameWithoutExtension(dllFilePath) + ".pdb");
                    if (PdbMatches(pdbPathCandidate, pdbIndexGuid, pdbIndexAge) && CheckSecurity(pdbPathCandidate))
                        pdbPath = pdbPathCandidate;
                }
            }

            // If the pdbPath is a full path, see if it exists 
            if (pdbPath == null && 0 < pdbFileName.IndexOf('\\'))
            {
                if (PdbMatches(pdbFileName, pdbIndexGuid, pdbIndexAge) && CheckSecurity(pdbFileName))
                    pdbPath = pdbFileName;
            }

            if (pdbPath != null)
            {
                this.m_log.WriteLine("FindSymbolFilePath: *}} Successfully found PDB {0} GUID {1} Age {2} Version {3}", pdbPath, pdbIndexGuid, pdbIndexAge, fileVersion);
                pdbPath = this.CacheFileLocally(pdbPath, pdbIndexGuid, pdbIndexAge);
            }
            else
            {
                string where = "";
                if ((Options & SymbolReaderOptions.CacheOnly) != 0)
                    where = " in local cache";
                m_log.WriteLine("FindSymbolFilePath: *}} Failed to find PDB {0}{1} GUID {2} Age {3} Version {4}", pdbSimpleName, where, pdbIndexGuid, pdbIndexAge, fileVersion);
            }
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
        /// <param name="pdblFilePath">The name of the PDB file to open.</param>
        /// <returns>The SymbolReaderModule that represents the information in the symbol file (PDB)</returns>
        public SymbolModule OpenSymbolFile(string pdblFilePath)
        {
            var ret = new SymbolModule(this, pdblFilePath);
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
        public string SymbolPath { get; set; }
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
        /// Cache even the unsafe pdbs to the SymbolCacheDirectory. 
        /// </summary>
        public bool CacheUnsafeSymbols { get; set; }
        /// <summary>
        /// We call back on this when we find a PDB by probing in 'unsafe' locations (like next to the EXE or in the Built location)
        /// If this function returns true, we assume that it is OK to use the PDB.  
        /// </summary>
        public Func<string, bool> SecurityCheck { get; set; }
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

            SymbolReader symReader = this;

            var log = symReader.m_log;
            if (!File.Exists(ngenImageFullPath))
            {
                log.WriteLine("Warning, NGEN image does not exist: {0}", ngenImageFullPath);
                return null;
            }

            // When V4.5 shipped, NGEN CreatePdb did not support looking up the IL pdb using symbol servers.  
            // We work around by explicitly fetching the IL PDB and pointing NGEN CreatePdb at that.  
            string ilPdbName = null;
            Guid ilPdbGuid = Guid.Empty;
            int ilPdbAge = 0;

            string pdbName;
            Guid pdbGuid;
            int pdbAge;
            using (var peFile = new PEFile.PEFile(ngenImageFullPath))
            {
                if (!peFile.GetPdbSignature(out pdbName, out pdbGuid, out pdbAge, true))
                {
                    log.WriteLine("Could not get PDB signature for {0}", ngenImageFullPath);
                    return null;
                }

                // Also get the IL pdb information
                peFile.GetPdbSignature(out ilPdbName, out ilPdbGuid, out ilPdbAge, false);
            }

            // Fast path, the file already exists.
            pdbName = Path.GetFileName(pdbName);
            var relPath = pdbName + "\\" + pdbGuid.ToString("N") + pdbAge.ToString() + "\\" + pdbName;
            var pdbPath = Path.Combine(outputDirectory, relPath);
            if (File.Exists(pdbPath))
                return pdbPath;

            string privateRuntimeVerString;
            var clrDir = GetClrDirectoryForNGenImage(ngenImageFullPath, log, out privateRuntimeVerString);
            if (clrDir == null)
                return null;

            // See if this is a V4.5 CLR, if so we can do line numbers too.l  
            var lineNumberArg = "";
            var ngenexe = Path.Combine(clrDir, "ngen.exe");
            log.WriteLine("Checking for V4.5 for NGEN image {0}", ngenexe);
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
                    log.WriteLine("Got NGEN image file version number: {0}", clrFileVersion);

                    m = Regex.Match(clrFileVersion, @"^[\d.]+\.(\d+) ");       // Fetch the build number (last number)
                    if (m.Success)
                    {
                        // Is this a V4.5 runtime?
                        var buildNumber = int.Parse(m.Groups[1].Value);
                        log.WriteLine("Got NGEN.exe Build number: {0}", buildNumber);
                        if (buildNumber > 16000 || !string.IsNullOrEmpty(privateRuntimeVerString))
                        {
                            if (ilPdbName != null)
                            {
                                var ilPdbPath = symReader.FindSymbolFilePath(ilPdbName, ilPdbGuid, ilPdbAge);
                                if (ilPdbPath != null)
                                    lineNumberArg = "/lines " + Command.Quote(Path.GetDirectoryName(ilPdbPath));
                                else
                                    log.WriteLine("Could not find IL PDB {0} Guid {1} Age {2}.", ilPdbName, ilPdbGuid, ilPdbAge);
                            }
                            else
                                log.WriteLine("NGEN image did not have IL PDB information, giving up on line number info.");
                            isV4_5Runtime = true;
                        }
                    }
                }
            }

            var options = new CommandOptions();
            options.AddEnvironmentVariable("COMPLUS_NGenEnableCreatePdb", "1");

            // NGenLocalWorker is needed for V4.0 runtimes but interferes on V4.5 runtimes.  
            if (!isV4_5Runtime)
                options.AddEnvironmentVariable("COMPLUS_NGenLocalWorker", "1");
            options.AddEnvironmentVariable("_NT_SYMBOL_PATH", symReader.SymbolPath);
            var newPath = "%PATH%;" + clrDir;
            options.AddEnvironmentVariable("PATH", newPath);
            options.AddOutputStream(log);
            options.AddNoThrow();

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
                outputPdbPath = Path.Combine(tempDir, relPath);
                log.WriteLine("Updating NGEN createPdb output file to {0}", outputPdbPath); // TODO FIX NOW REMOVE (for debugging)
            }
            try
            {
                for (; ; ) // Loop for retrying without /lines 
                {
                    if (!string.IsNullOrEmpty(privateRuntimeVerString))
                    {
                        log.WriteLine("Ngen will run for private runtime ", privateRuntimeVerString);
                        log.WriteLine("set COMPLUS_Version=" + privateRuntimeVerString);
                        options.AddEnvironmentVariable("COMPLUS_Version", privateRuntimeVerString);
                    }
                    // TODO FIX NOW: there is a and ugly problem with persistence of suboptimal PDB files
                    // This is made pretty bad because the not finding the IL PDBs is enough to make it fail.  

                    // TODO we need to figure out a convention show we know that we have fallen back to no-lines
                    // and we should regenerate it if we ultimately get the PDB information 
                    var cmdLine = string.Format(@"{0}\ngen.exe createpdb {1} {2} {3}",
                        clrDir, Command.Quote(ngenImageFullPath), Command.Quote(ngenOutputDirectory), lineNumberArg);
                    // TODO FIX NOW REMOVE after V4.5 is out a while
                    log.WriteLine("set COMPLUS_NGenEnableCreatePdb=1");
                    if (!isV4_5Runtime)
                        log.WriteLine("set COMPLUS_NGenLocalWorker=1");
                    log.WriteLine("set PATH=" + newPath);
                    log.WriteLine("set _NT_SYMBOL_PATH={0}", symReader.SymbolPath);
                    log.WriteLine("*** NGEN  CREATEPDB cmdline: {0}\r\n", cmdLine);
                    options.AddOutputStream(log);
                    var cmd = Command.Run(cmdLine, options);
                    log.WriteLine("*** NGEN CREATEPDB returns: {0}", cmd.ExitCode);

                    if (cmd.ExitCode != 0)
                    {
                        // ngen might make a bad PDB, so if it returns failure delete it.  
                        if (File.Exists(outputPdbPath))
                            File.Delete(outputPdbPath);

                        // We may have failed because we could not get the PDB.  
                        if (lineNumberArg.Length != 0)
                        {
                            log.WriteLine("Ngen failed to generate pdb for {0}, trying again without /lines", ngenImageFullPath);
                            lineNumberArg = "";
                            continue;
                        }
                    }

                    if (cmd.ExitCode != 0 || !File.Exists(outputPdbPath))
                    {
                        log.WriteLine("ngen failed to generate pdb for {0} at expected location {1}", ngenImageFullPath, outputPdbPath);
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
        ///  Called when you are done with the symbol reader.  Currently does nothing.  
        /// </summary>
        public void Dispose() { }

        #region private
        /// <summary>
        /// Returns true if 'filePath' exists and is a PDB that has pdbGuid and pdbAge.  
        /// if pdbGuid == Guid.Empty, then the pdbGuid and pdbAge checks are skipped. 
        /// </summary>
        private bool PdbMatches(string filePath, Guid pdbGuid, int pdbAge)
        {
            if (File.Exists(filePath))
            {
                if (pdbGuid == Guid.Empty)
                {
                    m_log.WriteLine("FindSymbolFilePath: No PDB Guid = Guid.Empty provided, assuming an unsafe PDB match for {0}", filePath);
                    return true;
                }
                SymbolModule module = this.OpenSymbolFile(filePath);
                if ((module.PdbGuid == pdbGuid) && (module.PdbAge == pdbAge))
                    return true;
                else
                    m_log.WriteLine("FindSymbolFilePath: PDB File {0} has Guid {1} age {2} != Desired Guid {3} age {4}",
                        filePath, module.PdbGuid, module.PdbAge, pdbGuid, pdbAge);
            }
            else
                m_log.WriteLine("FindSymbolFilePath: Probed file location {0} does not exist", filePath);
            return false;
        }

        /// <summary>
        /// Fetches a file from the server 'serverPath' with pdb signature path 'pdbSigPath' (concatinate them with a / or \ separator
        /// to form a complete URL or path name).   It will place the file in 'fullDestPath'   It will return true if successful
        /// You should probably be using GetFileFromServer
        /// </summary>
        /// <param name="serverPath">path to server (e.g. \\symbols\symbols or http://symweb) </param>
        /// <param name="pdbIndexPath">pdb path with signature (e.g clr.pdb/1E18F3E494DC464B943EA90F23E256432/clr.pdb)</param>
        /// <param name="fullDestPath">the full path of where to put the file locally </param>
        internal bool GetPhysicalFileFromServer(string serverPath, string pdbIndexPath, string fullDestPath)
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
                    if (serverPath.StartsWith("http:"))
                    {
                        var fullUri = serverPath + "/" + pdbIndexPath.Replace('\\', '/');
                        try
                        {
                            var req = (System.Net.HttpWebRequest)System.Net.HttpWebRequest.Create(fullUri);
                            req.UserAgent = "Microsoft-Symbol-Server/6.13.0009.1140";
                            var response = req.GetResponse();
                            alive = true;
                            if (!canceled)
                            {
                                using (var fromStream = response.GetResponseStream())
                                    CopyStreamToFile(fromStream, fullUri, fullDestPath, ref canceled);
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
                                    CopyStreamToFile(fromStream, fullSrcPath, fullDestPath, ref canceled);
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
        /// This just copies a stream to a file path with logging.  
        /// </summary>
        private void CopyStreamToFile(Stream fromStream, string fromUri, string fullDestPath, ref bool canceled)
        {
            bool completed = false;
            var copyToFileName = fullDestPath + ".new";
            try
            {
                var dirName = Path.GetDirectoryName(fullDestPath);
                Directory.CreateDirectory(dirName);
                m_log.WriteLine("CopyStreamToFile: Copying {0} to {1}", fromUri, copyToFileName);
                var sw = Stopwatch.StartNew();
                int lastMeg = 0;
                int last10K = 0;
                int byteCount = 0;
                using (Stream toStream = File.Create(copyToFileName))
                {
                    byte[] buffer = new byte[8192];
                    for (; ; )
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

            // Just try to fetch the file directly
            m_log.WriteLine("FindSymbolFilePath: Searching Symbol Server {0}.", urlForServer);
            if (GetPhysicalFileFromServer(urlForServer, fileIndexPath, targetPath))
                return targetPath;

            // The rest of this compressed file/file pointers stuff is only for remote servers.  
            if (!urlForServer.StartsWith(@"\\") && !urlForServer.StartsWith("http:"))
                return null;

            // See if it is a compressed file by replacing the last character of the name with an _
            var compressedSigPath = fileIndexPath.Substring(0, fileIndexPath.Length - 1) + "_";
            var compressedFilePath = targetPath.Substring(0, targetPath.Length - 1) + "_";
            if (GetPhysicalFileFromServer(urlForServer, compressedSigPath, compressedFilePath))
            {
                // Decompress it
                m_log.WriteLine("FindSymbolFilePath: Expanding {0} to {1}", compressedFilePath, targetPath);
                Command.Run("Expand " + Command.Quote(compressedFilePath) + " " + Command.Quote(targetPath));
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
                m = Regex.Match(ngenImagePath, @"\\Microsoft\\CLR_v(\d+)\.\d+(_(\d\d))?\\NativeImages", RegexOptions.IgnoreCase);
                if (m.Success)
                {
                    majorVersion = m.Groups[1].Value;
                    bitness = m.Groups[3].Value;
                }
                else
                {
                    m = Regex.Match(ngenImagePath, @"\\Microsoft.NET\\Framework((\d\d)?)\\v(\d+).*\\NativeImages", RegexOptions.IgnoreCase);
                    if (m.Success)
                    {
                        majorVersion = m.Groups[3].Value;
                        bitness = m.Groups[1].Value;
                    }
                    else
                    {
                        log.WriteLine("Warning: Could not deduce CLR version from path of NGEN image, skipping {0}", ngenImagePath);
                        return null;
                    }
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
        /// This is an optional routine.  It is already the case that if you find a PDB on a symbol server
        /// that it will be cached locally, however if you find it on a network path by NOT using a symbol
        /// server, it will be used in place.  This is annoying, and this routine makes up for this by
        /// mimicking this behavior.  Basically if pdbPath is not a local file name, it will copy it to
        /// the local symbol cache and return the local path. 
        /// </summary>
        private string CacheFileLocally(string pdbPath, Guid pdbGuid, int pdbAge)
        {
            try
            {
                var fileName = Path.GetFileName(pdbPath);

                // Use SymSrv conventions in the cache if the Guid is non-zero, otherwise we simply place it in the cache.  
                var localPdbDir = SymbolCacheDirectory;
                if (pdbGuid != Guid.Empty)
                {
                    var pdbPathPrefix = Path.Combine(SymbolCacheDirectory, fileName);
                    // There is a non-trivial possibility that someone puts a FILE that is named what we want the dir to be.  
                    if (File.Exists(pdbPathPrefix))
                    {
                        // If the pdb path happens to be the SymbolCacheDir (a definite possibility) then we would
                        // clobber the source file in our attempt to set up the target.  In this case just give up
                        // and leave the file as it was.  
                        if (string.Compare(pdbPath, pdbPathPrefix, StringComparison.OrdinalIgnoreCase) == 0)
                            return pdbPath;
                        m_log.WriteLine("Removing file {0} from symbol cache to make way for symsrv files.", pdbPathPrefix);
                        File.Delete(pdbPathPrefix);
                    }

                    localPdbDir = Path.Combine(pdbPathPrefix, pdbGuid.ToString("N") + pdbAge.ToString());
                }
                else if (!CacheUnsafeSymbols)
                    return pdbPath;

                if (!Directory.Exists(localPdbDir))
                    Directory.CreateDirectory(localPdbDir);

                var localPdbPath = Path.Combine(localPdbDir, fileName);
                var fileExists = File.Exists(localPdbPath);
                if (!fileExists || File.GetLastWriteTimeUtc(localPdbPath) != File.GetLastWriteTimeUtc(pdbPath))
                {
                    if (fileExists)
                        m_log.WriteLine("WARNING: overwriting existing file {0}.", localPdbPath);

                    m_log.WriteLine("Copying {0} to local cache {1}", pdbPath, localPdbPath);
                    File.Copy(pdbPath, localPdbPath, true);
                }
                return localPdbPath;
            }
            catch (Exception e)
            {
                m_log.WriteLine("Error trying to update local PDB cache {0}", e.Message);
            }
            return pdbPath;
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

        internal TextWriter m_log;
        private List<string> m_deadServers;     // What servers can't be reached right now
        private DateTime m_lastDeadTimeUtc;        // The last time something went dead.  

        private string m_SymbolCacheDirectory;
        private string m_SourceCacheDirectory;
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
            if (ret.Contains("@"))
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

            // See if we have a Project N map that maps $_NN to a pre-merged assembly name 
            var mergedAssembliesMap = GetMergedAssembliesMap();
            if (mergedAssembliesMap != null)
            {
                bool prefixMatchFound = false;
                Regex prefixMatch = new Regex(@"\$(\d+)_");
                ret = prefixMatch.Replace(ret, delegate(Match m)
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
            m_reader.m_log.WriteLine("SourceLocationForRva: looking up RVA {0:x} ", rva);

            uint fetchCount;
            IDiaEnumLineNumbers sourceLocs;
            m_session.findLinesByRVA(rva, 0, out sourceLocs);
            IDiaLineNumber sourceLoc;
            sourceLocs.Next(1, out sourceLoc, out fetchCount);
            if (fetchCount == 0)
            {
                m_reader.m_log.WriteLine("SourceLocationForRva: No lines for RVA {0:x} ", rva);
                return null;
            }
            var buildTimeSourcePath = sourceLoc.sourceFile.fileName;
            var lineNum = (int)sourceLoc.lineNumber;

            var sourceFile = new SourceFile(this, sourceLoc.sourceFile);
            var sourceLocation = new SourceLocation(sourceFile, (int)sourceLoc.lineNumber);
            return sourceLocation;
        }
        /// <summary>
        /// Managed code is shipped as IL, so RVA to NATIVE mapping can't be placed in the PDB. Instead
        /// what is placed in the PDB is a mapping from a method's meta-data token and IL offset to source
        /// line number.  Thus if you have a metadata token and IL offset, you can again get a source location
        /// </summary>
        public SourceLocation SourceLocationForManagedCode(uint methodMetadataToken, int ilOffset)
        {
            m_reader.m_log.WriteLine("SourceLocationForManaged Looking up method token {0:x} ilOffset {1:x}", methodMetadataToken, ilOffset);

            IDiaSymbol methodSym;
            m_session.findSymbolByToken(methodMetadataToken, SymTagEnum.SymTagFunction, out methodSym);
            if (methodSym == null)
            {
                m_reader.m_log.WriteLine("SourceLocationForManaged No symbol for token {0:x} ilOffset {1:x}", methodMetadataToken, ilOffset);
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
                m_reader.m_log.WriteLine("SourceLocationForManaged No lines for token {0:x} ilOffset {1:x}", methodMetadataToken, ilOffset);
                return null;
            }

            var sourceFile = new SourceFile(this, sourceLoc.sourceFile);
            int lineNum;
            // FEEFEE is some sort of illegal line number that is returned some time,  It is better to ignore it.  
            // and take the next valid line
            for (; ; )
            {
                lineNum = (int)sourceLoc.lineNumber;
                if (lineNum != 0xFEEFEE)
                    break;
                sourceLocs.Next(1, out sourceLoc, out fetchCount);
                if (fetchCount == 0)
                    break;
            }

            var sourceLocation = new SourceLocation(sourceFile, lineNum);
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
                        m_managedPdb = new SymbolModule(m_reader, managedPdbPath);
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

        /// For ProjectN modules, gets the merged IL image embedded in the .PDB (only valid for single-file compilation)
        /// </summary>
        /// <returns></returns>
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
        /// <returns></returns>
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
        /// The Tag is the kind of symbol it is (See SymTagEnum for more)
        /// </summary>
        public SymTagEnum Tag { get { return (SymTagEnum)m_diaSymbol.symTag; } }
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
            for (; ;)
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
            return string.Format("Symbol({0}, Tag={1}, RVA=0x{2:x}", Name, Tag, RVA);
        }

        internal Symbol(SymbolModule module, IDiaSymbol diaSymbol) { 
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
        public string BuildTimeFilePath { get; private set; }
        /// <summary>
        /// true if the PDB has a checksum for the data in the source file. 
        /// </summary>
        public bool HasChecksum { get { return m_hash != null; } }
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
            for (; ; )
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
        /// Try to fetch the source file associated with 'buildTimeFilePath' from the symbol server 
        /// information from the PDB from 'pdbPath'.   Will return a path to the returned file (uses 
        /// SourceCacheDirectory associated symbol reader for context where to put the file), 
        /// or null if unsuccessful.  
        /// </summary>
        string GetSourceFromSrcServer()
        {
            var log = m_symbolModule.m_reader.m_log;
            log.WriteLine("*** Looking up {0} using source server", BuildTimeFilePath);

            var srcServerPdb = m_symbolModule.PdbForSourceServer;
            if (srcServerPdb == null)
            {
                log.WriteLine("*** Could not find PDB to look up source server information");
                return null;
            }

            string srcsvcStream = srcServerPdb.GetSrcSrvStream();
            if (srcsvcStream == null)
            {
                log.WriteLine("*** Could not find srcsrv stream in PDB file");
                return null;
            }

            log.WriteLine("*** Found srcsrv stream in PDB file. of size {0}", srcsvcStream.Length);
            StringReader reader = new StringReader(srcsvcStream);

            bool inSrc = false;
            bool inVars = false;
            var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var cacheDir = m_symbolModule.m_reader.SourceCacheDirectory;
            vars.Add("targ", cacheDir);
            for (; ; )
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

                            var target = SourceServerFetchVar("SRCSRVTRG", vars);
                            if (!target.StartsWith(cacheDir, StringComparison.OrdinalIgnoreCase))
                            {
                                if (target.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                                {
                                    log.WriteLine("Fetching file {0} from web.", target);
                                    var url = target;
                                    target = null;
                                    if (vars.ContainsKey("HTTP_ALIAS"))
                                    {
                                        string prefix = vars["HTTP_ALIAS"];
                                        log.WriteLine("HTTP_ALIAS = {0}.", prefix);
                                        if (url.StartsWith(prefix))
                                        {
                                            var relPath = url.Substring(prefix.Length);
                                            var newTarget = cacheDir + @"\" + relPath.Replace('/', '\\');
                                            if (m_symbolModule.m_reader.GetPhysicalFileFromServer(prefix, relPath, newTarget))
                                                target = newTarget;
                                        }
                                        else
                                            log.WriteLine("target does not have HTTP_ALIAS as a prefix");
                                    }
                                    else
                                        log.WriteLine("Could not find HTTP_ALIAS");

                                    if (target == null)
                                    {
                                        log.WriteLine("Could not fetch {0} from web", url);
                                        return null;
                                    }
                                }
                                else
                                {
                                    log.WriteLine("Source Server string {0} is targeting an unsafe location.  Giving up.", target);
                                    return null;
                                }
                            }
                            if (!File.Exists(target))
                            {
                                log.WriteLine("Trying to generate the file {0}.", target);
                                var fetchCmdStr = SourceServerFetchVar("SRCSRVCMD", vars);
                                var toolsDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().ManifestModule.FullyQualifiedName);
                                var archToolsDir = Path.Combine(toolsDir, Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE"));

                                // Find the EXE to do the source server fetch.  We only support SD.exe and TF.exe.   
                                string addToPath = null;
#if !PUBLIC_ONLY                // SD.exe is a Microsoft-internal source code control system.
                                if (fetchCmdStr.StartsWith("sd.exe ", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (!File.Exists(Path.Combine(archToolsDir, "sd.exe")))
                                        log.WriteLine("WARNING: Could not find sd.exe that should have been deployed at {0}", archToolsDir);
                                    addToPath = archToolsDir;
                                }
                                else
#endif
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
#if !PUBLIC_ONLY
            // If we can get \\clrmain we keep a copy there.  
            string standAloneTF = @"\\clrmain\tools\StandAloneTF";
            if (SymbolPath.ComputerNameExists("clrmain", 1000) && Directory.Exists(standAloneTF))
                return Path.Combine(standAloneTF, "tf.exe");
#endif
            return null;
        }

        private string SourceServerFetchVar(string variable, Dictionary<string, string> vars)
        {
            string result = "";
            if (vars.TryGetValue(variable, out result))
                result = SourceServerEvaluate(result, vars);
            return result;
        }

        private string SourceServerEvaluate(string result, Dictionary<string, string> vars)
        {
            if (0 <= result.IndexOf('%'))
            {
                // see http://msdn.microsoft.com/en-us/library/windows/desktop/ms680641(v=vs.85).aspx for details on the %fn* variables 
                result = Regex.Replace(result, @"%fnvar%\((.*?)\)", delegate(Match m)
                {
                    return SourceServerFetchVar(SourceServerEvaluate(m.Groups[1].Value, vars), vars);
                });
                result = Regex.Replace(result, @"%fnbksl%\((.*?)\)", delegate(Match m)
                {
                    return SourceServerEvaluate(m.Groups[1].Value, vars).Replace('/', '\\');
                });
                result = Regex.Replace(result, @"%fnfile%\((.*?)\)", delegate(Match m)
                {
                    return Path.GetFileName(SourceServerEvaluate(m.Groups[1].Value, vars));
                });
                // Normal variable substitution
                result = Regex.Replace(result, @"%(\w+)%", delegate(Match m)
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
            m_hashType = sourceFile.checksumType;
            if (m_hashType != 1 && m_hashType != 0)
            {
                // TODO does anyone use SHA1?   
                Debug.Assert(false, "Unknown hash type");
                m_hashType = 0;
            }
            if (m_hashType != 0)
            {
                // MD5 is 16 bytes
                // SHA1 is 20 bytes  
                m_hash = new byte[16];

                uint bytesFetched;
                fixed (byte* bufferPtr = m_hash)
                    sourceFile.get_checksum((uint)m_hash.Length, out bytesFetched, out *bufferPtr);
                Debug.Assert(bytesFetched == 16);
            }
        }

        private static byte[] ComputeHash(string filePath)
        {
            System.Security.Cryptography.MD5CryptoServiceProvider crypto = new System.Security.Cryptography.MD5CryptoServiceProvider();
            using (var fileStream = File.OpenRead(filePath))
                return crypto.ComputeHash(fileStream);
        }

        SymbolModule m_symbolModule;
        uint m_hashType;
        byte[] m_hash;
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
    /// The DiaLoader class knows how to load the msdia120.dll (the Debug Access Interface) (see docs at
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
                NativeDlls.LoadNative("msdia120.dll");
                s_loadedNativeDll = true;
            }

            var diaSourceClassGuid = new Guid("{3BFCEA48-620F-4B6B-81F7-B9AF75454C7D}");
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
        [DllImport("msdia120.dll", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = false)]
        private static extern object DllGetClassObject(
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid rclsid,
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid);

        static bool s_loadedNativeDll;
        #endregion
    }
}
#endregion

