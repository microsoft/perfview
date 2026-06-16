using Dia2Lib;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Diagnostics.Utilities;

namespace Microsoft.Diagnostics.Symbols
{
    /// <summary>
    /// A NativeSymbolModule represents symbol information for a native code module.   
    /// NativeSymbolModules can potentially represent Managed modules (which is why it is a subclass of that interface).  
    /// 
    /// NativeSymbolModule should just be the CONTRACT for Native Symbols (some subclass implements
    /// it for a particular format like Windows PDBs), however today because we have only one file format we
    /// simply implement Windows PDBS here.   This can be factored out of this class when we 
    /// support other formats (e.g. Dwarf).
    /// 
    /// To implement support for Windows PDBs we use the Debug Interface Access (DIA).  See 
    /// http://msdn.microsoft.com/library/x93ctkx8.aspx for more.   I have only exposed what
    /// I need, and the interface is quite large (and not super pretty).  
    /// </summary>
    public unsafe class NativeSymbolModule : ManagedSymbolModule, IDisposable, ISymbolLookup
    {
        /// <summary>
        /// Returns the name of the type allocated for a given relative virtual address.
        /// Returns null if the given rva does not match a known heap allocation site.
        /// </summary>
        public string GetTypeForHeapAllocationSite(uint rva)
        {
            ThrowIfDisposed();

            return m_heapAllocationSites.Value.TryGetValue(rva, out var name) ? name : null;
        }

        /// <summary>
        /// Finds a (method) symbolic name for a given relative virtual address of some code.  
        /// Returns an empty string if a name could not be found. 
        /// </summary>
        public string FindNameForRva(uint rva)
        {
            ThrowIfDisposed();

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
            ThrowIfDisposed();

            System.Threading.Thread.Sleep(0);           // Allow cancellation.  
            if (m_symbolsByAddr == null)
            {
                return "";
            }

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
                {
                    ret = unmangled;
                }

                if (ret.StartsWith("@"))
                {
                    ret = ret.Substring(1);
                }

                if (ret.StartsWith("_"))
                {
                    ret = ret.Substring(1);
                }

                var atIdx = ret.IndexOf('@');
                if (0 < atIdx)
                {
                    ret = ret.Substring(0, atIdx);
                }
            }

            // See if this is a NGEN mangled name, which is $#Assembly#Token suffix.  If so strip it off. 
            var dollarIdx = ret.LastIndexOf('$');
            if (0 <= dollarIdx && dollarIdx + 2 < ret.Length && ret[dollarIdx + 1] == '#' && 0 <= ret.IndexOf('#', dollarIdx + 2))
            {
                ret = ret.Substring(0, dollarIdx);
            }

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
                    return GetAssemblyNameFromModuleIndex(mergedAssembliesMap, moduleIndex, original);
                });

                // By default - .NET native compilers do not generate a $#_ prefix for the methods coming from 
                // the assembly containing System.Object - the implicit module number is int.MaxValue

                if (!prefixMatchFound)
                {
                    ret = GetAssemblyNameFromModuleIndex(mergedAssembliesMap, int.MaxValue, String.Empty) + ret;
                }
            }

            return ret;
        }

        private static string GetAssemblyNameFromModuleIndex(Dictionary<int, string> mergedAssembliesMap, int moduleIndex, string defaultValue)
        {
            string fullAssemblyName;
            if (mergedAssembliesMap.TryGetValue(moduleIndex, out fullAssemblyName))
            {
                try
                {
                    var assemblyName = new AssemblyName(fullAssemblyName);
                    return assemblyName.Name + "!";
                }
                catch (Exception) { } // Catch all AssemblyName fails with ' in the name.   
            }

            return defaultValue;
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
        /// can convert the RVA back to IL artifacts (ilAssemblyName, methodMetadataToken, iloffset).  These can then
        /// be used to look up the source line using the IL PDB.  
        /// 
        /// Thus if the return value from this is null, check to see if the ilAssemblyName is non-null, and if not 
        /// you can look up the source location using that information.  
        /// </summary>
        public SourceLocation SourceLocationForRva(uint rva, out string ilAssemblyName, out uint methodMetadataToken, out int ilOffset)
        {
            ThrowIfDisposed();

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
                                ilAssemblyName = Path.GetFileNameWithoutExtension(SymbolFilePath);
                                // strip off the .ni if present
                                if (ilAssemblyName.EndsWith(".ni", StringComparison.OrdinalIgnoreCase))
                                {
                                    ilAssemblyName = ilAssemblyName.Substring(0, ilAssemblyName.Length - 3);
                                }
                            }
                            else
                            {
                                ilAssemblyName = name.Substring(suffixIdx + 2, tokenIdx - (suffixIdx + 2));
                            }

                            methodMetadataToken = (uint)token;
                            ilOffset = 0;           // If we don't find an IL offset, we 'guess' an ILOffset of 0

                            m_reader.m_log.WriteLine("SourceLocationForRva: Looking up IL Offset by RVA 0x{0:x}", rva);
                            m_session.findILOffsetsByRVA(rva, 0, out sourceLocs);
                            // FEEFEE is some sort of illegal line number that is returned some time,  It is better to ignore it.  
                            // and take the next valid line
                            for (; ; )
                            {
                                sourceLocs.Next(1, out sourceLoc, out fetchCount);
                                if (fetchCount == 0)
                                {
                                    m_reader.m_log.WriteLine("SourceLocationForRva: Ran out of IL mappings, guessing 0x{0:x}", ilOffset);
                                    break;
                                }
                                ilOffset = (int)sourceLoc.lineNumber;
                                if (ilOffset != 0xFEEFEE)
                                {
                                    break;
                                }

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

            var sourceFile = new MicrosoftPdbSourceFile(this, diaSrcFile);
            if (lineNum == 0xFEEFEE)
            {
                lineNum = 0;
            }

            var sourceLocation = new SourceLocation(sourceFile, (int)sourceLoc.lineNumber, (int)sourceLoc.lineNumberEnd, (int)sourceLoc.columnNumber, (int)sourceLoc.columnNumberEnd);
            m_reader.m_log.WriteLine("SourceLocationForRva: RVA {0:x} maps to line ({1},{2}):({3},{4}) file {5} ", rva, sourceLocation.LineNumber, sourceLocation.ColumnNumber, sourceLocation.LineNumberEnd, sourceLocation.ColumnNumberEnd, sourceFile.BuildTimeFilePath);
            return sourceLocation;
        }

        /// <summary>
        /// Managed code is shipped as IL, so RVA to NATIVE mapping can't be placed in the PDB. Instead
        /// what is placed in the PDB is a mapping from a method's meta-data token and IL offset to source
        /// line number.  Thus if you have a metadata token and IL offset, you can again get a source location
        /// </summary>
        public override SourceLocation SourceLocationForManagedCode(uint methodMetadataToken, int ilOffset)
        {
            ThrowIfDisposed();

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

            var sourceFile = new MicrosoftPdbSourceFile(this, sourceLoc.sourceFile);
            IDiaLineNumber lineNum = null;

            // FEEFEE is some sort of illegal line number that is returned some time,  It is better to ignore it.  
            // and take the next valid line
            for (; ; )
            {
                lineNum = sourceLoc;
                if (sourceLoc.lineNumber != 0xFEEFEE)
                {
                    break;
                }

                lineNum = null;
                sourceLocs.Next(1, out sourceLoc, out fetchCount);
                if (fetchCount == 0)
                {
                    break;
                }
            }

            int lineBegin = lineNum != null ? (int)lineNum.lineNumber : 0;
            int lineEnd = lineNum != null ? (int)lineNum.lineNumberEnd : 0;
            int columnBegin = lineNum != null ? (int)lineNum.columnNumber : 0;
            int columnEnd = lineNum != null ? (int)lineNum.columnNumberEnd : 0;

            var sourceLocation = new SourceLocation(sourceFile, lineBegin, lineEnd, columnBegin, columnEnd);
            m_reader.m_log.WriteLine("SourceLocationForManaged: found source linenum ({0},{1}):({2},{3}) file {4}", sourceLocation.LineNumber, sourceLocation.ColumnNumber, sourceLocation.LineNumberEnd, sourceLocation.ColumnNumberEnd, sourceFile.BuildTimeFilePath);
            return sourceLocation;
        }

        /// <summary>
        /// The symbol representing the module as a whole.  All global symbols are children of this symbol 
        /// </summary>
        public Symbol GlobalSymbol 
        { 
            get 
            {
                ThrowIfDisposed();
                return new Symbol(this, m_session.globalScope); 
            } 
        }

#if TEST_FIRST
        /// <summary>
        /// Returns a list of all source files referenced in the PDB
        /// </summary>
        public IEnumerable<SourceFile> AllSourceFiles()
        {
            ThrowIfDisposed();

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
        public override Guid PdbGuid 
        { 
            get 
            {
                ThrowIfDisposed();
                return m_session.globalScope.guid; 
            } 
        }

        /// <summary>
        /// Along with the PdbGuid, there is a small integer 
        /// call the age is also used to find the PDB (it represents the different 
        /// post link transformations the DLL has undergone).  
        /// </summary>
        public override int PdbAge 
        { 
            get 
            {
                ThrowIfDisposed();
                return (int)m_session.globalScope.age; 
            } 
        }

        #region private
        /// <summary>
        /// A source file represents a source file from a PDB.  This is not just a string
        /// because the file has a build time path, a checksum, and it needs to be 'smart'
        /// to copy down the file if requested.  
        /// 
        /// TODO We don't need this subclass.   We can have SourceFile simply a container
        /// that holds the BuildTimePath, hashType and hashValue.    The lookup of the
        /// source can then be put on NativeSymbolModule and called from SourceFile generically.  
        /// This makes the different symbol files more similar and is a nice simplification.  
        /// </summary>
        public class MicrosoftPdbSourceFile : SourceFile
        {
            private const string OldSourceServerUrl = "http://vstfdevdiv.redmond.corp.microsoft.com:8080";
            private const string NewSourceServerUrl = "https://vstfdevdiv";

            /// <inheritdoc/>
            public override bool GetSourceLinkInfo(out string url, out string relativePath)
            {
                // See if it is in sourceLink information.
                if (base.GetSourceLinkInfo(out url, out relativePath))
                {
                    return true;
                }
                else
                {
                    // Try to convert srcsrv information 
                    GetSourceServerTargetAndCommand(out string target, out _);

                    if (!string.IsNullOrEmpty(target) && Uri.IsWellFormedUriString(target, UriKind.Absolute))
                    {
                        url = target;
                        relativePath = Path.GetFileName(this.BuildTimeFilePath);
                        return true;
                    }

                    return false;
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
            protected override string GetSourceFromSrcServer()
            {
                // Try getting the source from the source server using SourceLink information.
                var ret = base.GetSourceFromSrcServer();
                if (ret != null)
                {
                    return ret;
                }

                var cacheDir = _symbolModule.SymbolReader.SourceCacheDirectory;

                GetSourceServerTargetAndCommand(out string target, out string fetchCmdStr, cacheDir);

                if (target == null)
                {
                    _log.WriteLine("Did not find source file in the set of source files in the PDB.");
                    return null;
                }

                // Synthesize a path under cacheDir that is structurally impossible to escape, no matter what
                // the PDB-supplied 'target' looks like.  The shape is <cacheDir>\<hash(target)>\<file name>.
                if (!TryGetSafeSourceCachePath(cacheDir, target, out string safeCachePath))
                {
                    _log.WriteLine("Source Server target {0} cannot be mapped to a safe cache path. Giving up.", target);
                    return null;
                }

                // If the file already exists in the safe cache path, return it without re-fetching.
                // This is also what suppresses repeat authorization prompts when the user opens the same source
                // file more than once: the second open finds the cached file and never reaches the prompt below.
                if (File.Exists(safeCachePath))
                {
                    if (new FileInfo(safeCachePath).Length > 0)
                    {
                        _log.WriteLine("Found an existing source server file {0}.", safeCachePath);
                        return safeCachePath;
                    }

                    if (!FileUtilities.TryDelete(safeCachePath))
                    {
                        _log.WriteLine("Found an existing empty source server file that could not be deleted: {0}", safeCachePath);
                        return null;
                    }

                    _log.WriteLine("Found an existing empty source server file {0}. Re-fetching.", safeCachePath);
                }

                // HTTP(S) fetch path.  Non-HTTP(S) targets fall through to the source-server command branch.
                if (Uri.TryCreate(target, UriKind.Absolute, out Uri uri)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
                {
                    if (!TryCreateSafeSourceCacheDirectory(safeCachePath))
                    {
                        return null;
                    }

                    if (_symbolModule.SymbolReader.GetPhysicalFileFromServer(
                            uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped),
                            uri.AbsolutePath,
                            safeCachePath))
                    {
                        _log.WriteLine("Source Server command succeeded creating {0}", safeCachePath);
                        return safeCachePath;
                    }

                    _log.WriteLine("Could not fetch {0} from web", uri.AbsoluteUri);
                    return null;
                }

                // Source-server command-line fetch path (tf.exe view / tf.exe git view).
                if (string.IsNullOrEmpty(fetchCmdStr))
                {
                    _log.WriteLine("Source Server target {0} is not an HTTP(S) URL and no fetch command was provided.  Giving up.", target);
                    return null;
                }

                _log.WriteLine("Trying to generate the file {0}.", safeCachePath);

                // Find tf.exe.  We only support tf.exe-based fetch commands (the per-tool allow-list in
                // TryCreateSafeSourceServerCommand will reject any other executable).
                string addToPath = null;
                var tfExe = Command.FindOnPath("tf.exe");
                if (tfExe == null)
                {
                    tfExe = FindTfExe();
                    if (tfExe == null)
                    {
                        _log.WriteLine("Could not find TF.exe, place it on the PATH environment variable to fix this.");
                        return null;
                    }
                    addToPath = Path.GetDirectoryName(tfExe);
                }

                // Legacy URL fixup: the original Microsoft TFS server's URL changed.  Because we no longer go
                // through cmd /c, this is just a string rewrite on the PDB-supplied command before tokenization.
                fetchCmdStr = fetchCmdStr.Replace(OldSourceServerUrl, NewSourceServerUrl);

                // Rebuild the fetch command from validated, allow-listed tokens, with safeCachePath forced as
                // the output destination.
                if (!TryCreateSafeSourceServerCommand(fetchCmdStr, tfExe, safeCachePath, out string safeFetchCmdStr, out string rejectReason))
                {
                    _log.WriteLine("Source Server command (PDB-supplied, not executed): {0}", fetchCmdStr);
                    _log.WriteLine("Source Server command is not recognized as safe ({0}). Failing.", rejectReason);
                    return null;
                }

                // Ask the consumer for permission to run the safe command.  Defaults to deny when no authorizer
                // is installed: non-PerfView consumers must opt in to executing source-server fetch commands.
                var authorize = _symbolModule.SymbolReader.AuthorizeSourceServerCommand;
                if (authorize == null)
                {
                    _log.WriteLine("Source Server command execution denied by default because no source-server command authorizer is installed.  Install SymbolReader.AuthorizeSourceServerCommand, or in PerfView run with /TrustPdbs, to allow this.  Safe rebuilt command was not executed: {0}", safeFetchCmdStr);
                    return null;
                }

                bool authorized;
                try
                {
                    authorized = authorize(new SourceServerAuthorizationRequest { Command = safeFetchCmdStr });
                }
                catch (Exception ex)
                {
                    _log.WriteLine("Source Server command authorizer threw exception.  Treating as denied.  Safe rebuilt command was not executed: {0}\r\nException: {1}", safeFetchCmdStr, ex.ToString());
                    return null;
                }

                if (!authorized)
                {
                    _log.WriteLine("Source Server command execution denied by authorizer.  Safe rebuilt command was not executed: {0}", safeFetchCmdStr);
                    return null;
                }

                _log.WriteLine("Source Server command authorized; running: {0}", safeFetchCmdStr);

                if (!TryCreateSafeSourceCacheDirectory(safeCachePath))
                {
                    return null;
                }

                var options = new CommandOptions().AddOutputStream(_log).AddNoThrow();
                if (addToPath != null)
                {
                    options = options.AddEnvironmentVariable("PATH", addToPath + ";%PATH%");
                }

                var fetchCmd = Command.Run(safeFetchCmdStr, options);
                if (fetchCmd.ExitCode != 0)
                {
                    _log.WriteLine("Source Server command failed with exit code {0}", fetchCmd.ExitCode);
                }

                if (File.Exists(safeCachePath))
                {
                    // If the fetch command fails it might still create an empty output file.  Treat that as a failure.
                    if (new FileInfo(safeCachePath).Length == 0)
                    {
                        if (!FileUtilities.TryDelete(safeCachePath))
                        {
                            _log.WriteLine("Source Server command produced an empty output file that could not be deleted: {0}", safeCachePath);
                        }

                        _log.WriteLine("Source Server command failed to produce the output file.");
                        return null;
                    }

                    _log.WriteLine("Source Server command succeeded creating {0}", safeCachePath);
                    return safeCachePath;
                }

                _log.WriteLine("Source Server command failed to produce the output file.");
                return null;
            }

            /// <summary>
            /// Creates the cache directory for a synthesized source-server cache path.
            /// Source-server fetches are best-effort: in locked-down environments the parent symbol cache
            /// directory may be read-only, ACL-restricted, unavailable, or otherwise unsuitable for creating
            /// per-source subdirectories.  Treat those filesystem failures as a source lookup miss and log the
            /// full exception instead of allowing a malformed or untrusted PDB lookup to fail the caller.
            /// </summary>
            private bool TryCreateSafeSourceCacheDirectory(string safeCachePath)
            {
                string directory = Path.GetDirectoryName(safeCachePath);
                try
                {
                    Directory.CreateDirectory(directory);
                    return true;
                }
                catch (Exception ex) when (
                    ex is IOException ||
                    ex is UnauthorizedAccessException ||
                    ex is ArgumentException ||
                    ex is NotSupportedException)
                {
                    _log.WriteLine("Could not create Source Server cache directory {0}. {1}", directory, ex);
                    return false;
                }
            }

            /// <summary>
            /// Synthesizes a structurally-safe path under <paramref name="cacheDir"/> for the PDB-supplied
            /// source-server <paramref name="target"/>.  The returned path has the shape
            /// <c>&lt;cacheDir&gt;\&lt;hash(target)&gt;\&lt;Path.GetFileName(target)&gt;</c>, which makes path
            /// traversal structurally impossible: the hash subdirectory is a 32-character hexadecimal string
            /// and the file name is a single path segment produced by <see cref="Path.GetFileName(string)"/>.
            ///
            /// Returns false if either input is null/empty, if the file-name component is missing, or if it
            /// contains characters not allowed in a file name.  On failure <paramref name="safeCachePath"/>
            /// is set to null.
            /// </summary>
            internal static bool TryGetSafeSourceCachePath(string cacheDir, string target, out string safeCachePath)
            {
                safeCachePath = null;

                if (string.IsNullOrEmpty(cacheDir) || string.IsNullOrEmpty(target))
                {
                    return false;
                }

                // Path.GetFileName treats both '\' and '/' as separators on Windows, so it correctly extracts
                // the trailing path segment from local paths and HTTP(S) URLs alike.
                string fileName;
                try
                {
                    fileName = Path.GetFileName(target);
                }
                catch (ArgumentException)
                {
                    return false;
                }

                if (string.IsNullOrEmpty(fileName))
                {
                    return false;
                }

                // Reject "." and ".." (and the trailing-space / trailing-dot variants Windows normalizes
                // to them).  Neither character sequence contains any character from
                // Path.GetInvalidFileNameChars() (so the IndexOfAny check below would pass them through),
                // but on Windows the kernel path normalizer strips trailing spaces and trailing dots from
                // every path component before resolving '.' / '..' semantics.  Consequently ".. ", "..  ",
                // "...", and similar all canonicalize at the OS level to ".." and would break the
                // single-segment containment invariant -- safeCachePath = <cacheDir>\<hash>\.. resolves to
                // <cacheDir>.  TrimEnd(' ', '.') reduces every such name to "" (and reduces "." / ".." to ""
                // as well), giving us a single check that subsumes the literal-dot-segment cases.
                if (fileName.TrimEnd(' ', '.').Length == 0)
                {
                    return false;
                }

                if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                {
                    return false;
                }

                string canonicalCacheDir;
                try
                {
                    canonicalCacheDir = Path.GetFullPath(cacheDir);
                }
                catch (ArgumentException)
                {
                    return false;
                }
                catch (NotSupportedException)
                {
                    return false;
                }
                catch (PathTooLongException)
                {
                    return false;
                }
                catch (System.Security.SecurityException)
                {
                    // Path.GetFullPath checks FileIOPermission on the resolved path on .NET Framework, and
                    // can throw SecurityException on sandboxed hosts.  Treat as failure.
                    return false;
                }

                string subdir = ComputeCacheSubdir(target);
                safeCachePath = Path.Combine(canonicalCacheDir, subdir, fileName);
                return true;
            }

            /// <summary>
            /// Returns a 32-character hexadecimal subdirectory name derived from the XxHash128 of the UTF-8
            /// bytes of <paramref name="value"/>.  XxHash128 is a fast non-cryptographic hash; it is used here
            /// solely as a stable mapping from <paramref name="value"/> to a directory name so that the cache
            /// layout is identical across processes and runtimes (unlike <see cref="string.GetHashCode"/>,
            /// which is randomized in .NET Core).
            /// </summary>
            private static string ComputeCacheSubdir(string value)
            {
                byte[] hash = System.IO.Hashing.XxHash128.Hash(Encoding.UTF8.GetBytes(value));
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
                }
                return sb.ToString();
            }

            /// <summary>
            /// Allow-list of switches accepted by <c>tf.exe view</c> (TFVC).  All other switches cause the
            /// command to be rejected.  The PDB-supplied output destination is always stripped and replaced
            /// with our own <c>/output:&lt;safeCachePath&gt;</c>; <c>/login</c> is always stripped.
            /// </summary>
            private static readonly HashSet<string> s_tfViewAllowedSwitches = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "version",   // /version:<changeset>
                "noprompt",  // flag
                "server",    // /server:<TFS server URL>
                "console",   // flag - now a no-op since we always write to /output:
            };

            /// <summary>
            /// Allow-list of switches accepted by <c>tf.exe git view</c> (Azure DevOps Git via tf.exe).  All
            /// other switches cause the command to be rejected.  The PDB-supplied output destination is always
            /// stripped and replaced with our own <c>/output:&lt;safeCachePath&gt;</c>; <c>/login</c> is always
            /// stripped.
            /// </summary>
            private static readonly HashSet<string> s_tfGitViewAllowedSwitches = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "collection",   // /collection:<collection URL>
                "teamproject",  // /teamproject:<project>
                "repository",   // /repository:<repo>
                "commitid",     // /commitid:<sha>
                "path",         // /path:<repo-relative path>
                "applyfilters", // flag
            };

            /// <summary>
            /// Validates and rebuilds <paramref name="sourceServerCommand"/> (the PDB-supplied SRCSRVCMD /
            /// TFS_EXTRACT_CMD expansion) into a safe command line that can be executed directly via
            /// <c>CreateProcess</c> (not through a shell).
            ///
            /// Three command shapes are accepted (case-insensitive): <c>tf.exe view ...</c> (TFVC),
            /// <c>tf.exe vc view ...</c> (TFVC via the explicit vc namespace), and
            /// <c>tf.exe git view ...</c> (Azure DevOps Git via tf.exe).  Other executables -- including
            /// <c>cmd.exe</c>, <c>fastVstsBlob.exe</c>, <c>sd.exe</c> (Perforce), and other tf.exe sub-commands
            /// like <c>workfold</c> -- are rejected.  Within an accepted command, only switches on the
            /// corresponding per-tool allow-list (<see cref="s_tfViewAllowedSwitches"/> /
            /// <see cref="s_tfGitViewAllowedSwitches"/>) are kept; any unrecognized switch causes the whole
            /// command to be rejected and the offending token is named in <paramref name="rejectionReason"/>.
            ///
            /// Regardless of accept/reject, the rewriter always strips any PDB-supplied <c>/output</c> or
            /// <c>/login</c> token (PDBs have no business carrying credentials, and we always force the output
            /// destination to <paramref name="outputPath"/>) and stops processing at any <c>&gt;file</c>
            /// shell-redirection token (legacy templates that ran through <c>cmd /c</c>).  The rebuilt command
            /// always ends with a single <c>/output:&lt;outputPath&gt;</c>.
            /// </summary>
            internal static bool TryCreateSafeSourceServerCommand(
                string sourceServerCommand,
                string tfExe,
                string outputPath,
                out string safeCommand,
                out string rejectionReason)
            {
                safeCommand = null;
                rejectionReason = null;

                if (string.IsNullOrWhiteSpace(sourceServerCommand))
                {
                    rejectionReason = "empty command";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(tfExe))
                {
                    rejectionReason = "missing tf.exe path";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    rejectionReason = "missing output path";
                    return false;
                }

                if (!TrySplitCommandLine(sourceServerCommand, out List<string> tokens, out rejectionReason))
                {
                    return false;
                }

                // Identify the tool: "tf.exe view ...", "tf.exe vc view ...", or "tf.exe git view ...".
                // The starting position of the argument walk depends on which shape we saw.
                HashSet<string> allowedSwitches;
                List<string> safeArguments;
                int argStart;

                if (tokens.Count >= 2
                    && string.Equals(tokens[0], "tf.exe", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(tokens[1], "view", StringComparison.OrdinalIgnoreCase))
                {
                    allowedSwitches = s_tfViewAllowedSwitches;
                    safeArguments = new List<string> { "view" };
                    argStart = 2;
                }
                else if (tokens.Count >= 3
                    && string.Equals(tokens[0], "tf.exe", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(tokens[1], "vc", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(tokens[2], "view", StringComparison.OrdinalIgnoreCase))
                {
                    allowedSwitches = s_tfViewAllowedSwitches;
                    safeArguments = new List<string> { "vc", "view" };
                    argStart = 3;
                }
                else if (tokens.Count >= 3
                    && string.Equals(tokens[0], "tf.exe", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(tokens[1], "git", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(tokens[2], "view", StringComparison.OrdinalIgnoreCase))
                {
                    allowedSwitches = s_tfGitViewAllowedSwitches;
                    safeArguments = new List<string> { "git", "view" };
                    argStart = 3;
                }
                else
                {
                    rejectionReason = "only 'tf.exe view', 'tf.exe vc view', and 'tf.exe git view' source-server commands are supported";
                    return false;
                }

                // Whether we've already taken the positional depot-path argument for the TFVC view shapes.
                // Only one such positional is allowed; further positionals are rejected as unknown.
                bool tookTfViewPositional = false;

                for (int i = argStart; i < tokens.Count; i++)
                {
                    string token = tokens[i];

                    // Stop at legacy '>file' shell redirection.  These appear in templates that originally
                    // ran through cmd /c; we always write via /output: instead.
                    if (token.Length > 0 && token[0] == '>')
                    {
                        break;
                    }

                    // Strip any PDB-supplied output destination.  We append our own /output: at the end.
                    if (IsTfSwitch(token, "output", out bool outputHasAttachedValue))
                    {
                        if (!outputHasAttachedValue && i + 1 < tokens.Count && !LooksLikeSwitchOrRedirection(tokens[i + 1]))
                        {
                            i++;
                        }
                        continue;
                    }

                    // Strip any PDB-supplied /login.  Source-server templates have no business carrying credentials.
                    if (IsTfSwitch(token, "login", out bool loginHasAttachedValue))
                    {
                        if (!loginHasAttachedValue && i + 1 < tokens.Count && !LooksLikeSwitchOrRedirection(tokens[i + 1]))
                        {
                            i++;
                        }
                        continue;
                    }

                    // Switch-shaped token: must be on the per-tool allow-list.
                    if (token.Length > 0 && (token[0] == '/' || token[0] == '-'))
                    {
                        string switchName = ExtractSwitchName(token);
                        if (switchName != null && allowedSwitches.Contains(switchName))
                        {
                            safeArguments.Add(token);
                            continue;
                        }

                        rejectionReason = "unrecognized switch '" + token + "'";
                        return false;
                    }

                    // Positional argument.  For the TFVC view shapes the legitimate templates carry exactly
                    // one depot path (e.g. "$/team/path/file.cs").  Require the minimum plausible file shape
                    // "$/x"; "$" and "$/" are not source-file paths.
                    if (allowedSwitches == s_tfViewAllowedSwitches
                        && !tookTfViewPositional
                        && token.Length >= 3
                        && token[0] == '$'
                        && token[1] == '/')
                    {
                        safeArguments.Add(token);
                        tookTfViewPositional = true;
                        continue;
                    }

                    rejectionReason = "unrecognized argument '" + token + "'";
                    return false;
                }

                safeArguments.Add("/output:" + outputPath);

                var builder = new StringBuilder();
                builder.Append(QuoteCommandLineArgument(tfExe));
                foreach (string argument in safeArguments)
                {
                    builder.Append(' ');
                    builder.Append(QuoteCommandLineArgument(argument));
                }

                safeCommand = builder.ToString();
                return true;
            }

            /// <summary>
            /// Returns the name portion of a tf.exe-style switch token (e.g. <c>"/server:foo"</c> -&gt;
            /// <c>"server"</c>, <c>"/noprompt"</c> -&gt; <c>"noprompt"</c>, <c>"-applyfilters"</c> -&gt;
            /// <c>"applyfilters"</c>).  Returns null if <paramref name="token"/> is not a switch-shaped token.
            /// </summary>
            private static string ExtractSwitchName(string token)
            {
                if (string.IsNullOrEmpty(token) || (token[0] != '/' && token[0] != '-'))
                {
                    return null;
                }

                int end = 1;
                while (end < token.Length && token[end] != ':' && token[end] != '=')
                {
                    end++;
                }

                if (end == 1)
                {
                    return null;
                }

                return token.Substring(1, end - 1);
            }

            /// <summary>
            /// Returns true if <paramref name="token"/> looks like a switch (leading '/' or '-') or a shell
            /// redirection ('&gt;' / '&lt;').  Used when stripping a PDB-supplied <c>/output</c> or
            /// <c>/login</c> with no attached value to decide whether the following token is the missing
            /// value (consume it) or another switch / structural marker (leave it for the main walk to
            /// process normally).  This keeps us from silently swallowing a legitimate allow-listed switch
            /// when a malformed PDB emits a bare <c>/output</c> with no following value.
            /// </summary>
            private static bool LooksLikeSwitchOrRedirection(string token)
            {
                if (string.IsNullOrEmpty(token))
                {
                    return false;
                }

                char first = token[0];
                return first == '/' || first == '-' || first == '>' || first == '<';
            }

            /// <summary>
            /// Returns true if <paramref name="token"/> is a tf.exe-style switch named <paramref name="switchName"/>.
            /// Accepts both '/' and '-' prefixes and recognizes the value-attached forms 'switch:value' and
            /// 'switch=value'.  <paramref name="hasAttachedValue"/> is set when the token also carries the
            /// switch's value; when false, callers that consume a value should skip the next token as well.
            /// </summary>
            private static bool IsTfSwitch(string token, string switchName, out bool hasAttachedValue)
            {
                hasAttachedValue = false;
                if (string.IsNullOrEmpty(token))
                {
                    return false;
                }

                if (token[0] != '/' && token[0] != '-')
                {
                    return false;
                }

                if (token.Length == 1 + switchName.Length)
                {
                    return string.Equals(token.Substring(1), switchName, StringComparison.OrdinalIgnoreCase);
                }

                if (token.Length > 1 + switchName.Length
                    && (token[1 + switchName.Length] == ':' || token[1 + switchName.Length] == '=')
                    && string.Equals(token.Substring(1, switchName.Length), switchName, StringComparison.OrdinalIgnoreCase))
                {
                    hasAttachedValue = true;
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Splits <paramref name="commandLine"/> into tokens using a simple Windows-style quote-aware
            /// tokenizer.  Double quotes can group whitespace into a single token and are stripped from the
            /// resulting token value.
            /// </summary>
            private static bool TrySplitCommandLine(string commandLine, out List<string> tokens, out string rejectionReason)
            {
                tokens = new List<string>();
                rejectionReason = null;

                int i = 0;
                while (i < commandLine.Length)
                {
                    while (i < commandLine.Length && char.IsWhiteSpace(commandLine[i]))
                    {
                        i++;
                    }

                    if (i >= commandLine.Length)
                    {
                        break;
                    }

                    var token = new StringBuilder();
                    bool inQuotes = false;
                    while (i < commandLine.Length && (inQuotes || !char.IsWhiteSpace(commandLine[i])))
                    {
                        char c = commandLine[i++];
                        if (c == '"')
                        {
                            inQuotes = !inQuotes;
                            continue;
                        }

                        token.Append(c);
                    }

                    if (inQuotes)
                    {
                        rejectionReason = "unterminated quoted argument";
                        return false;
                    }

                    tokens.Add(token.ToString());
                }

                return true;
            }

            /// <summary>
            /// Quotes <paramref name="argument"/> for inclusion in a Windows command line consumable by
            /// CommandLineToArgvW.  Backslashes immediately preceding a quote (or a closing quote) are doubled
            /// per CRT rules; other characters are emitted literally.  Returns <paramref name="argument"/>
            /// unchanged when no quoting is needed.
            /// </summary>
            private static string QuoteCommandLineArgument(string argument)
            {
                if (argument.Length == 0)
                {
                    return "\"\"";
                }

                bool quote = false;
                for (int i = 0; i < argument.Length; i++)
                {
                    if (char.IsWhiteSpace(argument[i]) || argument[i] == '"')
                    {
                        quote = true;
                        break;
                    }
                }

                if (!quote)
                {
                    return argument;
                }

                var quotedArgument = new StringBuilder();
                quotedArgument.Append('"');
                int backslashCount = 0;
                foreach (char c in argument)
                {
                    if (c == '\\')
                    {
                        backslashCount++;
                    }
                    else if (c == '"')
                    {
                        quotedArgument.Append('\\', backslashCount * 2 + 1);
                        quotedArgument.Append('"');
                        backslashCount = 0;
                    }
                    else
                    {
                        quotedArgument.Append('\\', backslashCount);
                        quotedArgument.Append(c);
                        backslashCount = 0;
                    }
                }

                quotedArgument.Append('\\', backslashCount * 2);
                quotedArgument.Append('"');
                return quotedArgument.ToString();
            }


            #region private
            internal unsafe MicrosoftPdbSourceFile(NativeSymbolModule module, IDiaSourceFile sourceFile) : base(module)
            {
                BuildTimeFilePath = sourceFile.fileName;

                if (sourceFile.checksumType == 0)
                {
                    // If the checksum type is zero, this means either this is a non-C++ PDB, or there is no checksum info
                    TryInitializeManagedChecksum(module);
                }
                else
                {
                    // Otherwise this is a C++ style PDB
                    TryInitializeCppChecksum(sourceFile);
                }
            }

            private void TryInitializeCppChecksum(IDiaSourceFile sourceFile)
            {
                // 1 CALG_MD5 checksum generated with the MD5 hashing algorithm.
                // 2 CALG_SHA1 checksum generated with the SHA1 hashing algorithm.
                // 3 checksum generated with the SHA256 hashing algorithm.
                if (sourceFile.checksumType == 1)
                {
                    // CodeQL [SM02196] The checksum algorithm is specified by the built artifact.  This is not controlled by TraceEvent.
                    // CodeQL [SM03938] The checksum algorithm is specified by the built artifact.  This is not controlled by TraceEvent.
                    // CodeQL [SM03939] The checksum algorithm is specified by the built artifact.  This is not controlled by TraceEvent.
                    _hashAlgorithm = System.Security.Cryptography.MD5.Create();
                }
                else if (sourceFile.checksumType == 2)
                {
                    // CodeQL [SM02196] The checksum algorithm is specified by the built artifact.  This is not controlled by TraceEvent.
                    // CodeQL [SM03938] The checksum algorithm is specified by the built artifact.  This is not controlled by TraceEvent.
                    // CodeQL [SM03939] The checksum algorithm is specified by the built artifact.  This is not controlled by TraceEvent.
                    _hashAlgorithm = System.Security.Cryptography.SHA1.Create();
                }
                else if (sourceFile.checksumType == 3)
                {
                    _hashAlgorithm = System.Security.Cryptography.SHA256.Create();
                }

                if (_hashAlgorithm != null)
                {
                    uint hashSizeInBytes;
                    byte* dummy = null;
                    sourceFile.get_checksum(0, out hashSizeInBytes, out *dummy);

                    // MD5 is 16 bytes
                    // SHA1 is 20 bytes  
                    // SHA-256 is 32 bytes
                    _hash = new byte[hashSizeInBytes];

                    uint bytesFetched;
                    fixed (byte* bufferPtr = _hash)
                    {
                        sourceFile.get_checksum((uint)_hash.Length, out bytesFetched, out *bufferPtr);
                    }

                    Debug.Assert(bytesFetched == _hash.Length);
                }
            }

            private void TryInitializeManagedChecksum(NativeSymbolModule module)
            {
                try
                {
                    module.m_session.findInjectedSource(this.BuildTimeFilePath, out IDiaEnumInjectedSources injectedSources);
                    if (injectedSources == null)
                    {
                        return;
                    }

                    injectedSources.Next(1, out IDiaInjectedSource injectedSource, out uint count);
                    if (count != 1)
                    {
                        return;
                    }

                    SrcFormat srcFormat = new SrcFormat();
                    int srcFormatSize = Marshal.SizeOf(typeof(SrcFormat));
                    int srcFormatHeaderSize = Marshal.SizeOf(typeof(SrcFormatHeader));
                    byte* pSrcFormat = (byte*)&srcFormat;
                    injectedSource.get_source((uint)srcFormatSize, out uint sizeAvailable, out *pSrcFormat);

                    if (sizeAvailable < srcFormatHeaderSize || sizeAvailable < srcFormat.Header.checkSumSize + srcFormatHeaderSize || srcFormatSize < srcFormat.Header.checkSumSize + srcFormatHeaderSize)
                    {
                        return;
                    }

                    if (srcFormat.Header.algorithmId == guidMD5)
                    {
                        // CodeQL [SM02196] The checksum algorithm is specified by the built artifact.  This is not controlled by TraceEvent.
                        // CodeQL [SM03938] The checksum algorithm is specified by the built artifact.  This is not controlled by TraceEvent.
                        // CodeQL [SM03939] The checksum algorithm is specified by the built artifact.  This is not controlled by TraceEvent.
                        _hashAlgorithm = System.Security.Cryptography.MD5.Create();
                    }
                    else if (srcFormat.Header.algorithmId == guidSHA1)
                    {
                        // CodeQL [SM02196] The checksum algorithm is specified by the built artifact.  This is not controlled by TraceEvent.
                        // CodeQL [SM03938] The checksum algorithm is specified by the built artifact.  This is not controlled by TraceEvent.
                        // CodeQL [SM03939] The checksum algorithm is specified by the built artifact.  This is not controlled by TraceEvent.
                        _hashAlgorithm = System.Security.Cryptography.SHA1.Create();
                    }
                    else if (srcFormat.Header.algorithmId == guidSHA256)
                    {
                        _hashAlgorithm = System.Security.Cryptography.SHA256.Create();
                    }
                    else if (srcFormat.Header.algorithmId == guidSHA384)
                    {
                        _hashAlgorithm = System.Security.Cryptography.SHA384.Create();
                    }
                    else if (srcFormat.Header.algorithmId == guidSHA512)
                    {
                        _hashAlgorithm = System.Security.Cryptography.SHA512.Create();
                    }

                    if (_hashAlgorithm != null)
                    {
                        _hash = new byte[srcFormat.Header.checkSumSize];
                        Marshal.Copy((IntPtr)srcFormat.checksumBytes, _hash, startIndex: 0, length: _hash.Length);
                    }
                }
                catch (COMException)
                {
                    // DIA API failed. Ignore.
                }
            }

            /// <summary>
            /// Parse the 'srcsrv' stream in a PDB file and return the target for SourceFile
            /// represented by the 'this' pointer.   This target is either a ULR or a local file
            /// path.  
            /// 
            /// You can dump the srcsrv stream using a tool called pdbstr 
            ///     pdbstr -r -s:srcsrv -p:PDBPATH
            /// 
            /// The target in this stream is called SRCSRVTRG and there is another variable SRCSRVCMD
            /// which represents the command to run to fetch the source into SRCSRVTRG
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
            ///  SRCSRV: source files ---------------------------------            ------
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
            ///  c:\Users\dev\Documents\Visual Studio 2012\Projects\DavidSymbolSourceTest\DavidSymbolSourceTest\Demo.cs*SQPvxWBMtvANyCp8Pd3OjoZEUgpKvjDVIY1WbaiFPMw=
            ///  SRCSRV: end ------------------------------------------------
            ///  
            /// </summary>
            /// <param name="target">returns the target source file path</param>
            /// <param name="command">returns the command to fetch the target source file</param>
            /// <param name="localDirectoryToPlaceSourceFiles">Specify the value for %targ% variable. This is the
            /// directory where source files can be fetched to.  Typically the returned file is under this directory
            /// If the value is null, %targ% variable be empty.  This assumes that the resulting file is something
            /// that does not need to be copied to the machine (either a URL or a file that already exists)</param>
            private void GetSourceServerTargetAndCommand(out string target, out string command, string localDirectoryToPlaceSourceFiles = null)
            {
                target = null;
                command = null;

                _log.WriteLine("*** Looking up {0} using source server", BuildTimeFilePath);

                NativeSymbolModule srcServerPdb = (_symbolModule as NativeSymbolModule).PdbForSourceServer as NativeSymbolModule;
                if (srcServerPdb == null)
                {
                    _log.WriteLine("*** Could not find PDB to look up source server information");
                    return;
                }

                string srcsvcStream = srcServerPdb.GetSrcSrvStream();
                if (srcsvcStream == null)
                {
                    _log.WriteLine("*** Could not find srcsrv stream in PDB file");
                    return;
                }

                _log.WriteLine("*** Found srcsrv stream in PDB file. of size {0}", srcsvcStream.Length);
                StringReader reader = new StringReader(srcsvcStream);

                bool inSrc = false;
                bool inVars = false;
                var vars = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                if (localDirectoryToPlaceSourceFiles != null)
                {
                    vars.Add("targ", localDirectoryToPlaceSourceFiles);
                }

                for (; ; )
                {
                    var line = reader.ReadLine();
                    if (line == null)
                    {
                        break;
                    }

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
                                {
                                    vars.Add("var" + (i + 1).ToString(), pieces[i]);
                                }

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
                        {
                            vars[m.Groups[1].Value] = m.Groups[2].Value;
                        }
                    }
                }
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
                {
                    progFiles = Environment.GetEnvironmentVariable("ProgramFiles");
                }

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
                        {
                            return tfexe;
                        }
                    }
                }
                return null;
            }

            private string SourceServerFetchVar(string variable, Dictionary<string, string> vars)
            {
                string result = "";
                if (vars.TryGetValue(variable, out result))
                {
                    if (0 <= result.IndexOf('%'))
                    {
                        _log.WriteLine("SourceServerFetchVar: Before Evaluation {0} = '{1}'", variable, result);
                    }

                    result = SourceServerEvaluate(result, vars);
                }
                _log.WriteLine("SourceServerFetchVar: {0} = '{1}'", variable, result);
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
#endif
#if false
        // Here is ana example of the stream in use for the jithlp.asm file.  

f:\dd\ndp\clr\src\vm\i386\jithelp.asm*DEVDIV_TFS2*/DevDiv/D11RelS/FX45RTMGDR/ndp/clr/src/VM/i386/jithelp.asm*592925

        // Here is the command that it issues.  
tf.exe view /version:592925 /noprompt "$/DevDiv/D11RelS/FX45RTMGDR/ndp/clr/src/VM/i386/jithelp.asm" /server:http://vstfdevdiv.redmond.corp.microsoft.com:8080/devdiv2 /console >"C:\Users\vancem\AppData\Local\Temp\PerfView\src\DEVDIV_TFS2\DevDiv\D11RelS\FX45RTMGDR\ndp\clr\src\VM\i386\jithelp.asm\592925\jithelp.asm"

#endif
            #endregion
        }

        private NativeSymbolModule(SymbolReader reader, string pdbFilePath, Action<IDiaDataSource3> loadData) : base(reader, pdbFilePath)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // DIA is not supported on non-Windows systems.
                throw new PlatformNotSupportedException("Cannot load a Windows PDB on a non-Windows system.");
            }

            m_reader = reader;

            m_source = DiaLoader.GetDiaSourceObject();
            loadData(m_source);
            m_source.openSession(out m_session);
            m_session.getSymbolsByAddr(out m_symbolsByAddr);

            m_heapAllocationSites = new Lazy<IReadOnlyDictionary<uint, string>>(() =>
            {
                // Retrieves the S_HEAPALLOCSITE information from the pdb as described here:
                // https://docs.microsoft.com/visualstudio/profiling/custom-native-etw-heap-events
                Dictionary<uint, string> result = null;
                m_session.getHeapAllocationSites(out var diaEnumSymbols);
                for (; ; )
                {
                    diaEnumSymbols.Next(1, out var sym, out var fetchCount);
                    if (fetchCount == 0)
                    {
                        return (IReadOnlyDictionary<uint, string>)result ?? System.Collections.Immutable.ImmutableDictionary<uint, string>.Empty;
                    }

                    result = result ?? new Dictionary<uint, string>();
                    m_session.symbolById(sym.typeId, out var typeSym);
                    result[sym.relativeVirtualAddress + (uint)sym.length] = HeapAllocationTypeInfo.GetTypeName(typeSym);
                }
            });

            m_reader.m_log.WriteLine("Opening PDB {0} with signature GUID {1} Age {2}", pdbFilePath, PdbGuid, PdbAge);
        }

        internal NativeSymbolModule(SymbolReader reader, string pdbFilePath)
            : this(reader, pdbFilePath, s => s.loadDataFromPdb(pdbFilePath))
        {
        }

        internal NativeSymbolModule(SymbolReader reader, string pdbFilePath, Stream pdbStream)
            : this(reader, pdbFilePath, s => s.loadDataFromIStream(new ComStreamWrapper(pdbStream)))
        {
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
            ThrowIfDisposed();

            // In order to get the IDiaDataSource3 which includes 'getStreamSize' API, you need to use the 
            // dia2_internal.idl file from devdiv to produce the Interop.Dia2Lib.dll 
            // see class DiaLoader for more
            var log = m_reader.m_log;
            log.WriteLine("Getting source server stream for PDB {0}", SymbolFilePath);
            uint len = 0;
            m_source.getStreamSize("srcsrv", out len);
            if (len == 0)
            {
                if (0 <= SymbolFilePath.IndexOf(".ni.", StringComparison.OrdinalIgnoreCase))
                {
                    log.WriteLine("Error, trying to look up source information on an NGEN file, giving up");
                }
                else
                {
                    log.WriteLine("Pdb {0} does not have source server information (srcsrv stream) in it", SymbolFilePath);
                }

                return null;
            }

            return GetUTF8PDBStream("srcsrv", len);
        }

        private string GetUTF8PDBStream(string name, uint len)
        {
            byte[] buffer = new byte[len];
            fixed (byte* bufferPtr = buffer)
            {
                m_source.getStreamRawData(name, len, out *bufferPtr);
                var ret = new UTF8Encoding().GetString(buffer);
                return ret;
            }
        }

        protected override IEnumerable<string> GetSourceLinkJson()
        {
            ThrowIfDisposed();

            // Source Link is stored in windows pdb in *EITHER* the 'sourcelink' stream *OR* 1 or more 'sourcelink$n' streams where n starts at 1.
            // For multi stream format, we read the streams starting at 1 until we receive a stream size of 0.

            const string singleStreamName = "sourcelink";
            const string multiStreamNameFormat = "sourcelink${0}";

            // first check the single stream
            m_source.getStreamSize(singleStreamName, out uint streamSize);
            if (streamSize > 0)
            {
                string content = GetUTF8PDBStream(singleStreamName, streamSize);
                return new string[] { content };
            }
            else
            {
                List<string> result = new List<string>();

                // if there was no single stream, check the multi stream
                for (int cStream = 1; cStream < int.MaxValue; cStream++)
                {
                    string streamName = string.Format(CultureInfo.InvariantCulture, multiStreamNameFormat, cStream);
                    m_source.getStreamSize(streamName, out streamSize);
                    if (streamSize == 0)
                    {
                        break;
                    }

                    string content = GetUTF8PDBStream(streamName, streamSize);
                    result.Add(content);
                }

                return result;
            }
        }

        // returns the path of the PDB that has source server information in it (which for NGEN images is the PDB for the managed image)
        internal ManagedSymbolModule PdbForSourceServer
        {
            get
            {
                if (m_managedPdbName == null)
                {
                    return this;
                }

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
                    {
                        m_reader.m_log.WriteLine("Could not find managed PDB {0}", m_managedPdbName);
                    }
                }
                return m_managedPdb;
            }
        }

        /// <summary>
        /// For Project N modules it returns the list of pre merged IL assemblies and the corresponding mapping.
        /// </summary>
        public Dictionary<int, string> GetMergedAssembliesMap()
        {
            ThrowIfDisposed();

            if (m_mergedAssemblies == null && !m_checkedForMergedAssemblies)
            {
                IDiaEnumInputAssemblyFiles diaMergedAssemblyRecords;
                m_session.findInputAssemblyFiles(out diaMergedAssemblyRecords);
                for (; ; )
                {
                    IDiaInputAssemblyFile inputAssembly;
                    uint fetchCount;
                    diaMergedAssemblyRecords.Next(1, out inputAssembly, out fetchCount);
                    if (fetchCount != 1)
                    {
                        break;
                    }

                    int index = (int)inputAssembly.index;
                    string assemblyName = inputAssembly.fileName;

                    if (m_mergedAssemblies == null)
                    {
                        m_mergedAssemblies = new Dictionary<int, string>();
                    }

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
            ThrowIfDisposed();

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
            ThrowIfDisposed();

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
            ThrowIfDisposed();

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
            ThrowIfDisposed();

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

        ~NativeSymbolModule()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!m_isDisposed)
            {
                m_isDisposed = true;

                if (m_session is IDiaSession3 diaSession3)
                {
                    int hr = diaSession3.dispose();
                    Debug.Assert(hr == 0, "IDiaSession3.dispose failed");
                }
            }
        }

        /// <summary>
        /// This function checks if the SymbolModule is disposed before proceeding with the call.
        /// This is important because DIA doesn't provide any guarantees as to what will happen if 
        /// one attempts to call after the session is disposed, so this at least ensure that we
        /// fail cleanly in non-concurrent cases.
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (m_isDisposed)
            {
                throw new ObjectDisposedException(nameof(NativeSymbolModule));
            }
        }

        /// <summary>
        /// This static class contains the GetTypeName method for retrieving the type name of 
        /// a heap allocation site. 
        /// 
        /// See https://github.com/KirillOsenkov/Dia2Dump/blob/master/PrintSymbol.cpp for more details
        /// </summary>
        private static class HeapAllocationTypeInfo
        {
            internal static string GetTypeName(IDiaSymbol symbol)
            {
                var name = symbol.name ?? "<unknown>";

                switch ((SymTagEnum)symbol.symTag)
                {
                    case SymTagEnum.UDT:
                    case SymTagEnum.Enum:
                    case SymTagEnum.Typedef:
                        return name;
                    case SymTagEnum.FunctionType:
                        return "function";
                    case SymTagEnum.PointerType:
                        return $"{GetTypeName(symbol.type)} {(symbol.reference != 0 ? "&" : "*") }";
                    case SymTagEnum.ArrayType:
                        return "array";
                    case SymTagEnum.BaseType:
                        var sb = new StringBuilder();
                        switch ((BasicType)symbol.baseType)
                        {
                            case BasicType.btUInt:
                                sb.Append("unsigned ");
                                goto case BasicType.btInt;
                            case BasicType.btInt:
                                switch (symbol.length)
                                {
                                    case 1:
                                        sb.Append("char");
                                        break;
                                    case 2:
                                        sb.Append("short");
                                        break;
                                    case 4:
                                        sb.Append("int");
                                        break;
                                    case 8:
                                        sb.Append("long");
                                        break;
                                }
                                return sb.ToString();
                            case BasicType.btFloat:
                                return symbol.length == 4 ? "float" : "double";
                            default:
                                return BaseTypes.Length > symbol.baseType ? BaseTypes[symbol.baseType] : $"base type {symbol.baseType}";
                        }
                }

                return $"unhandled symbol tag {symbol.symTag}";
            }

            private enum SymTagEnum
            {
                Null,
                Exe,
                Compiland,
                CompilandDetails,
                CompilandEnv,
                Function,
                Block,
                Data,
                Annotation,
                Label,
                PublicSymbol,
                UDT,
                Enum,
                FunctionType,
                PointerType,
                ArrayType,
                BaseType,
                Typedef,
                BaseClass,
                Friend,
                FunctionArgType,
                FuncDebugStart,
                FuncDebugEnd,
                UsingNamespace,
                VTableShape,
                VTable,
                Custom,
                Thunk,
                CustomType,
                ManagedType,
                Dimension,
                CallSite,
                InlineSite,
                BaseInterface,
                VectorType,
                MatrixType,
                HLSLType
            };

            // See https://learn.microsoft.com/visualstudio/debugger/debug-interface-access/basictype
            private enum BasicType
            {
                btNoType = 0,
                btVoid = 1,
                btChar = 2,
                btWChar = 3,
                btInt = 6,
                btUInt = 7,
                btFloat = 8,
                btBCD = 9,
                btBool = 10,
                btLong = 13,
                btULong = 14,
                btCurrency = 25,
                btDate = 26,
                btVariant = 27,
                btComplex = 28,
                btBit = 29,
                btBSTR = 30,
                btHresult = 31,
                btChar16 = 32,  // char16_t
                btChar32 = 33,  // char32_t
                btChar8 = 34,   // char8_t
            };

            private static readonly string[] BaseTypes = new[]
            {
                "<NoType>",             // btNoType = 0,
                "void",                 // btVoid = 1,
                "char",                 // btChar = 2,
                "wchar_t",              // btWChar = 3,
                "signed char",
                "unsigned char",
                "int",                  // btInt = 6,
                "unsigned int",         // btUInt = 7,
                "float",                // btFloat = 8,
                "<BCD>",                // btBCD = 9,
                "bool",                 // btBool = 10,
                "short",
                "unsigned short",
                "long",                 // btLong = 13,
                "unsigned long",        // btULong = 14,
                "__int8",
                "__int16",
                "__int32",
                "__int64",
                "__int128",
                "unsigned __int8",
                "unsigned __int16",
                "unsigned __int32",
                "unsigned __int64",
                "unsigned __int128",
                "<currency>",           // btCurrency = 25,
                "<date>",               // btDate = 26,
                "VARIANT",              // btVariant = 27,
                "<complex>",            // btComplex = 28,
                "<bit>",                // btBit = 29,
                "BSTR",                 // btBSTR = 30,
                "HRESULT",              // btHresult = 31,
                "char16_t",             // btChar16 = 32,
                "char32_t",             // btChar32 = 33,
                "char8_t",              // btChar8 = 34
            };
        }

        private bool m_isDisposed;
        private bool m_checkedForMergedAssemblies;
        private Dictionary<int, string> m_mergedAssemblies;

        private string m_managedPdbName;
        private Guid m_managedPdbGuid;
        private int m_managedPdbAge;
        private ManagedSymbolModule m_managedPdb;
        private bool m_managedPdbAttempted;

        internal readonly IDiaSession m_session;
        private readonly SymbolReader m_reader;
        private readonly IDiaDataSource3 m_source;
        private readonly IDiaEnumSymbolsByAddr m_symbolsByAddr;
        private readonly Lazy<IReadOnlyDictionary<uint, string>> m_heapAllocationSites; // rva => typename

        [StructLayout(LayoutKind.Sequential)]
        struct SrcFormatHeader
        {
            public Guid language;
            public Guid languageVendor;
            public Guid documentType;
            public Guid algorithmId;
            public UInt32 checkSumSize;
            public UInt32 sourceSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct SrcFormat
        {
            public SrcFormatHeader Header;
            public fixed byte checksumBytes[512/8]; // this size of this may be smaller, it is controlled by the size of the `checksumSize` field
        }

        private static readonly Guid guidMD5 = new Guid("406ea660-64cf-4c82-b6f0-42d48172a799");
        private static readonly Guid guidSHA1 = new Guid("ff1816ec-aa5e-4d10-87f7-6f4963833460");
        private static readonly Guid guidSHA256 = new Guid("8829d00f-11b8-4213-878b-770e8597ac16");
        private static readonly Guid guidSHA384 = new Guid("d99cfeb1-8c43-444a-8a6c-b61269d2a0bf");
        private static readonly Guid guidSHA512 = new Guid("ef2d1afc-6550-46d6-b14b-d70afe9a5566");

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
            {
                return null;
            }

            uint fetchCount;
            var ret = new List<Symbol>();
            for (; ; )
            {
                IDiaSymbol sym;
                symEnum.Next(1, out sym, out fetchCount);
                if (fetchCount == 0)
                {
                    break;
                }

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

        /// <summary>
        /// override
        /// </summary>
        public override string ToString()
        {
            return string.Format("Symbol({0}, RVA=0x{1:x}", Name, RVA);
        }

        internal Symbol(NativeSymbolModule module, IDiaSymbol diaSymbol)
        {
            m_module = module;
            m_diaSymbol = diaSymbol;
            m_name = m_diaSymbol.name;
        }
        private string m_name;
        private IDiaSymbol m_diaSymbol;
        private NativeSymbolModule m_module;
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
            {
                p[i] = buf[i];
            }
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
    /// In order to get IDiaDataSource3 which includes 'getStreamSize' API, you need to use the 
    /// vctools\langapi\idl\dia2_internal.idl file from devdiv to produce Dia2Lib.dll
    /// 
    /// roughly what you need to do is 
    ///     copy vctools\langapi\idl\dia2_internal.idl .
    ///     copy vctools\langapi\idl\dia2.idl .
    ///     copy vctools\langapi\include\cvconst.h .
    ///     Change dia2.idl to include interface IDiaDataSource3 inside library Dia2Lib->importlib->coclass DiaSource
    ///     midl dia2_internal.idl /D CC_DP_CXX
    ///     tlbimp dia2_internal.tlb
    ///     REM result is Dia2Lib.dll 
    /// </summary>
    internal static class DiaLoader
    {
        /// <summary>
        /// Load the msdia140 dll and get a IDiaDataSource from it.  This is your gateway to PDB reading.
        /// </summary>
        public static IDiaDataSource3 GetDiaSourceObject()
        {
            Guid iDataDataSourceGuid = typeof(IDiaDataSource3).GetTypeInfo().GUID;
            s_diaClassFactory.CreateInstance(null, iDataDataSourceGuid, out object comObject);
            return comObject as IDiaDataSource3;
        }

        private static IClassFactory CreateDiaClassFactory()
        {
            // Ensure that the native DLL we need exists.
            NativeDlls.LoadNative("msdia140.dll");

            // This is the value it was for msdia120 and before 
            // var diaSourceClassGuid = new Guid("{3BFCEA48-620F-4B6B-81F7-B9AF75454C7D}");

            // This is the value for msdia140.  
            var diaSourceClassGuid = new Guid("{e6756135-1e65-4d17-8576-610761398c3c}");
            return (IClassFactory)DllGetClassObject(diaSourceClassGuid, typeof(IClassFactory).GetTypeInfo().GUID);
        }

        #region private
        [ComImport, ComVisible(false), Guid("00000001-0000-0000-C000-000000000046"),
         InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IClassFactory
        {
            void CreateInstance([MarshalAs(UnmanagedType.Interface)] object aggregator,
                                [In] in Guid refiid,
                                [MarshalAs(UnmanagedType.Interface)] out object createdObject);
            void LockServer(bool incrementRefCount);
        }

        // Methods
        [return: MarshalAs(UnmanagedType.Interface)]
        [DllImport("msdia140.dll", CharSet = CharSet.Unicode, ExactSpelling = true, PreserveSig = false)]
        private static extern object DllGetClassObject(
            [In] in Guid rclsid,
            [In] in Guid riid);

        /// <summary>
        /// The COM class factory for DIA.
        /// Used to ensure the native library is loaded prior to trying to use it.
        /// Note that we never release this class factory, but this is not a problem since we
        /// aren't trying to unload the library after use.
        /// </summary>
        private static readonly IClassFactory s_diaClassFactory = CreateDiaClassFactory();
        #endregion
    }

    [ComVisible(true)]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [GuidAttribute("52585014-e2b6-49fe-aa72-3a1e178682ee")]
    interface IDiaSession3
    {
        #region Uncalled methods to declare the VTable correctly
        void Reserved01(); // get_loadAddress
        void Reserved02(); // put_loadAddress
        void Reserved03(); // get_globalScope
        void Reserved04(); // getEnumTables
        void Reserved05(); // getSymbolsByAddr
        void Reserved06(); // findChildren
        void Reserved07(); // findChildrenEx
        void Reserved08(); // findChildrenExByAddr
        void Reserved09(); // findChildrenExByVA
        void Reserved10(); // findChildrenExByRVA
        void Reserved11(); // findSymbolByAddr
        void Reserved12(); // findSymbolByRVA
        void Reserved13(); // findSymbolByVA
        void Reserved14(); // findSymbolByToken
        void Reserved15(); // symsAreEquiv
        void Reserved16(); // symbolById
        void Reserved17(); // findSymbolByRVAEx
        void Reserved18(); // findSymbolByVAEx
        void Reserved19(); // findFile
        void Reserved20(); // findFileById
        void Reserved21(); // findLines
        void Reserved22(); // findLinesByAddr
        void Reserved23(); // findLinesByRVA
        void Reserved24(); // findLinesByVA
        void Reserved25(); // findLinesByLinenum
        void Reserved26(); // findInjectedSource
        void Reserved27(); // getEnumDebugStreams
        void Reserved28(); // findInlineFramesByAddr
        void Reserved29(); // findInlineFramesByRVA
        void Reserved30(); // findInlineFramesByVA
        void Reserved31(); // findInlineeLines
        void Reserved32(); // findInlineeLinesByAddr
        void Reserved33(); // findInlineeLinesByRVA
        void Reserved34(); // findInlineeLinesByVA
        void Reserved35(); // findInlineeLinesByLinenum
        void Reserved36(); // findInlineesByName
        void Reserved37(); // findAcceleratorInlineeLinesByLinenum
        void Reserved38(); // findSymbolsForAcceleratorPointerTag
        void Reserved39(); // findSymbolsByRVAForAcceleratorPointerTag
        void Reserved40(); // findAcceleratorInlineesByName
        void Reserved41(); // addressForVA
        void Reserved42(); // addressForRVA
        void Reserved43(); // findILOffsetsByAddr
        void Reserved44(); // findILOffsetsByRVA
        void Reserved45(); // findILOffsetsByVA
        void Reserved46(); // findInputAssemblyFiles
        void Reserved47(); // findInputAssembly
        void Reserved48(); // findInputAssemblyById
        void Reserved49(); // getFuncMDTokenMapSize
        void Reserved50(); // getFuncMDTokenMap
        void Reserved51(); // getTypeMDTokenMapSize
        void Reserved52(); // getTypeMDTokenMap
        void Reserved53(); // getNumberOfFunctionFragments_VA
        void Reserved54(); // getNumberOfFunctionFragments_RVA
        void Reserved55(); // getFunctionFragments_VA
        void Reserved56(); // getFunctionFragments_RVA
        void Reserved57(); // getExports
        void Reserved58(); // getHeapAllocationSites
        void Reserved59(); // findInputAssemblyFile
        void Reserved60(); // addPublicSymbol
        void Reserved61(); // addStaticSymbol
        void Reserved62(); // findSectionAddressByCrc
        void Reserved63(); // findThunkSymbol
        void Reserved64(); // makeThunkSymbol
        void Reserved65(); // mergeObjPDB
        void Reserved66(); // commitObjPDBMerge
        void Reserved67(); // cancelObjPDBMerge
        void Reserved68(); // getLinkInfo
        void Reserved69(); // isMiniPDB
        void Reserved70(); // prepareEnCRebuild
        #endregion

        [PreserveSig] 
        int dispose();
    };
}
#endregion
