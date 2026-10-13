using Azure.Core;
using Microsoft.Diagnostics.Runtime;
using Microsoft.Diagnostics.Symbols;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;

/// <summary>
/// Adapts PerfView's symbol path and local runtime discovery to ClrMD's data-target options.
/// </summary>
internal static class ClrMdDataTargetOptions
{
    /// <summary>
    /// Creates options with PerfView's cache and authentication settings, adding local lookup
    /// before ClrMD's HTTP(S) lookup when the symbol path contains filesystem locations.
    /// </summary>
    internal static DataTargetOptions Create(string symbolPath, TokenCredential credential, TextWriter log, bool verifyDacOnWindows)
    {
        bool hasConfiguredPath = !string.IsNullOrWhiteSpace(symbolPath);
        var path = new SymbolPath(hasConfiguredPath ? symbolPath : SymbolPath.MicrosoftSymbolServerPath);
        var servers = new List<string>();
        var localPath = new SymbolPath();
        foreach (SymbolPathElement element in path.Elements)
        {
            // Probe existing indexed cache entries locally, even when their server is unavailable.
            if (!string.IsNullOrEmpty(element.Cache))
                localPath.Add("SRV*" + element.Cache);

            if (string.IsNullOrEmpty(element.Target))
                continue;

            if (Uri.TryCreate(element.Target, UriKind.Absolute, out Uri uri) &&
                (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            {
                servers.Add(element.Target);
            }
            else
            {
                // Retain the distinction between a flat directory and an indexed symbol store.
                localPath.Add(element.IsSymServer ? "SRV*" + element.Target : element.Target);
            }
        }

        var options = new DataTargetOptions
        {
            SymbolPaths = servers.ToArray(),
            SymbolCachePath = path.DefaultSymbolCache(),
            SymbolTokenCredential = credential,
            // PerfView's explicitly configured symbol path may contain trusted internal or HTTP servers.
            AllowPrivateSymbolServers = hasConfiguredPath,
            VerifyDacOnWindows = verifyDacOnWindows,
            // The x86 HeapDump worker must not map large dumps into its limited address space.
            UseLockFreeMemoryMapReader = Environment.Is64BitProcess
        };

        // ClrMD's built-in downloader only supports HTTP(S), not local directories or UNC symbol stores.
        if (localPath.Elements.Count != 0)
        {
            // Keep the fallback separate from options.FileLocator so it cannot call this wrapper recursively.
            var remoteOptions = new DataTargetOptions
            {
                SymbolPaths = options.SymbolPaths,
                SymbolCachePath = options.SymbolCachePath,
                SymbolTokenCredential = options.SymbolTokenCredential,
                AllowPrivateSymbolServers = options.AllowPrivateSymbolServers
            };
            options.FileLocator = new LocalFileLocator(localPath.ToString(), log, remoteOptions);
        }

        return options;
    }

    /// <summary>
    /// Prioritizes a DAC beside a matching installed Windows runtime, without removing
    /// ClrMD's existing candidates or changing its DAC verification policy.
    /// </summary>
    internal static void AddLocalDacCandidate(ClrInfo clr, TextWriter log)
    {
        // An adjacent DAC must be loadable by this worker. Single-file images need different discovery.
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            clr.DataTarget.DataReader.TargetPlatform != OSPlatform.Windows ||
            clr.DataTarget.DataReader.Architecture != RuntimeInformation.ProcessArchitecture ||
            clr.IsSingleFile || clr.IndexTimeStamp == 0 || clr.IndexFileSize == 0 ||
            !IsAbsoluteLocalPath(clr.ModuleInfo.FileName))
            return;

        string dacName;
        switch (clr.Flavor)
        {
            case ClrFlavor.Desktop:
                dacName = "mscordacwks.dll";
                break;
            case ClrFlavor.Core:
                dacName = "mscordaccore.dll";
                break;
            default:
                return;
        }

        // A dump's runtime path is useful only if the installed image still matches the target.
        if (!MatchesPEImage(clr.ModuleInfo.FileName, clr.IndexTimeStamp, clr.IndexFileSize, log))
            return;

        string dacPath = Path.Combine(Path.GetDirectoryName(clr.ModuleInfo.FileName), dacName);
        if (!File.Exists(dacPath))
        {
            log.WriteLine("No runtime-adjacent DAC at {0}.", dacPath);
            return;
        }

        var libraries = clr.DebuggingLibraries.IsDefault
            ? ImmutableArray<DebugLibraryInfo>.Empty : clr.DebuggingLibraries;
        // Preparing the same runtime more than once must not duplicate the local candidate.
        if (libraries.Any(library => library.Kind == DebugLibraryKind.Dac &&
            string.Equals(library.FileName, dacPath, StringComparison.OrdinalIgnoreCase)))
            return;

        // Coreclr here means indexed under the runtime's identity, including for Desktop CLR DACs.
        var candidate = new DebugLibraryInfo(DebugLibraryKind.Dac, dacPath,
            clr.DataTarget.DataReader.Architecture, SymbolProperties.Coreclr, clr.IndexFileSize, clr.IndexTimeStamp);
        // The normal ClrMD loader still applies signature policy and retries the remaining candidates.
        clr.DebuggingLibraries = libraries.Insert(0, candidate);
        log.WriteLine("Using runtime-adjacent DAC candidate {0}.", dacPath);
    }

    #region private
    /// <summary>
    /// Recognizes fully qualified Windows drive paths without accessing the filesystem.
    /// Relative, UNC, and device paths are not eligible for implicit local probing.
    /// </summary>
    private static bool IsAbsoluteLocalPath(string path)
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !string.IsNullOrEmpty(path) &&
            path.Length >= 3 && ((path[0] >= 'A' && path[0] <= 'Z') || (path[0] >= 'a' && path[0] <= 'z')) &&
            path[1] == ':' && (path[2] == '\\' || path[2] == '/');
    }

    /// <summary>
    /// Matches an installed PE image against the target's symbol-server identity.
    /// Logs mismatches and inspection failures so callers can continue with other candidates.
    /// </summary>
    private static bool MatchesPEImage(string path, int timestamp, int imageSize, TextWriter log)
    {
        if (!File.Exists(path))
            return false;

        try
        {
            using (var stream = File.OpenRead(path))
            using (var image = new PEReader(stream))
            {
                var headers = image.PEHeaders;
                // Timestamp plus SizeOfImage identifies the binary; this is not a signing-signature check.
                if (headers.PEHeader != null && headers.CoffHeader.TimeDateStamp == timestamp &&
                    headers.PEHeader.SizeOfImage == imageSize)
                    return true;
            }
            log.WriteLine("Rejecting local image {0}: PE properties do not match timestamp {1:x8}, size {2:x}.",
                path, timestamp, imageSize);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is BadImageFormatException)
        {
            log.WriteLine("Could not inspect local image {0}: {1}", path, ex.Message);
        }
        return false;
    }

    /// <summary>
    /// Searches local images and symbol stores before lazily delegating remote lookup to ClrMD.
    /// </summary>
    private sealed class LocalFileLocator : IFileLocator
    {
        /// <summary>
        /// Stores the local search path and fallback options without initializing the remote transport.
        /// </summary>
        public LocalFileLocator(string symbolPath, TextWriter log, DataTargetOptions remoteOptions)
        {
            m_symbolPath = symbolPath;
            m_log = log;
            m_remoteOptions = remoteOptions;
        }

        /// <summary>
        /// Finds a Windows image by its PE identity, preferring an existing absolute local
        /// path, then the configured local locations, and finally ClrMD's fallback.
        /// </summary>
        public string FindPEImage(string fileName, int buildTimeStamp, int imageSize, bool checkProperties)
        {
            // Returning the path here lets ClrMD retain the target load address and apply PE relocations.
            // DAC queries can use runtime indexing properties, so honor checkProperties=false here.
            if (IsAbsoluteLocalPath(fileName) && File.Exists(fileName) &&
                (!checkProperties || MatchesPEImage(fileName, buildTimeStamp, imageSize, m_log)))
                return fileName;

            using (var reader = new SymbolReader(m_log, m_symbolPath))
            {
                string localFile = reader.FindExecutableFilePath(Path.GetFileName(fileName), buildTimeStamp, imageSize);
                if (localFile != null)
                {
                    if (!checkProperties)
                        return localFile;

                    using (var image = new PEFile.PEFile(localFile))
                    {
                        if (image.Header.TimeDateStampSec == buildTimeStamp &&
                            image.Header.SizeOfImage == unchecked((uint)imageSize))
                            return localFile;
                    }
                }

                return m_remoteOptions.FileLocator.FindPEImage(fileName, buildTimeStamp, imageSize, checkProperties);
            }
        }

        /// <summary>
        /// Finds a debugging image indexed by an ELF build ID or Mach-O UUID, including
        /// Windows DACs for those runtimes, before delegating to ClrMD.
        /// </summary>
        public string FindPEImage(string fileName, SymbolProperties archivedUnder, ImmutableArray<byte> buildIdOrUUID,
            OSPlatform originalPlatform, bool checkProperties)
        {
            if (!buildIdOrUUID.IsDefaultOrEmpty && (originalPlatform == OSPlatform.Linux || originalPlatform == OSPlatform.OSX))
            {
                string name = Path.GetFileName(fileName);
                string platformKey = originalPlatform == OSPlatform.Linux ? "elf-buildid-" : "mach-uuid-";
                // Runtime-indexed DACs use a coreclr qualifier; self-indexed images do not.
                if (archivedUnder == SymbolProperties.Coreclr)
                    platformKey += "coreclr-";
                string key = platformKey + string.Concat(buildIdOrUUID.Select(b => b.ToString("x2")));
                foreach (SymbolPathElement element in new SymbolPath(m_symbolPath).Elements)
                {
                    string directory = element.Target;
                    if (string.IsNullOrEmpty(directory))
                        continue;

                    string candidate = Path.Combine(directory, name.ToLowerInvariant(), key, name.ToLowerInvariant());
                    if (File.Exists(candidate))
                        return candidate;
                }
            }

            return m_remoteOptions.FileLocator.FindPEImage(fileName, archivedUnder, buildIdOrUUID, originalPlatform, checkProperties);
        }

        private readonly string m_symbolPath;
        private readonly TextWriter m_log;
        // Preserve ClrMD's lazy transport initialization until local lookup actually misses.
        private readonly DataTargetOptions m_remoteOptions;
    }
    #endregion
}
