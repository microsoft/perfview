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

internal static class ClrMdDataTargetOptions
{
    internal static DataTargetOptions Create(string symbolPath, TokenCredential credential, TextWriter log, bool verifyDacOnWindows)
    {
        bool hasConfiguredPath = !string.IsNullOrWhiteSpace(symbolPath);
        var path = new SymbolPath(hasConfiguredPath ? symbolPath : SymbolPath.MicrosoftSymbolServerPath);
        var servers = new List<string>();
        var localPath = new SymbolPath();
        foreach (SymbolPathElement element in path.Elements)
        {
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

    internal static void AddLocalDacCandidate(ClrInfo clr, TextWriter log)
    {
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
        if (libraries.Any(library => library.Kind == DebugLibraryKind.Dac &&
            string.Equals(library.FileName, dacPath, StringComparison.OrdinalIgnoreCase)))
            return;

        var candidate = new DebugLibraryInfo(DebugLibraryKind.Dac, dacPath,
            clr.DataTarget.DataReader.Architecture, SymbolProperties.Coreclr, clr.IndexFileSize, clr.IndexTimeStamp);
        clr.DebuggingLibraries = libraries.Insert(0, candidate);
        log.WriteLine("Using runtime-adjacent DAC candidate {0}.", dacPath);
    }

    #region private
    private static bool IsAbsoluteLocalPath(string path)
    {
        return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !string.IsNullOrEmpty(path) &&
            path.Length >= 3 && ((path[0] >= 'A' && path[0] <= 'Z') || (path[0] >= 'a' && path[0] <= 'z')) &&
            path[1] == ':' && (path[2] == '\\' || path[2] == '/');
    }

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

    private sealed class LocalFileLocator : IFileLocator
    {
        public LocalFileLocator(string symbolPath, TextWriter log, DataTargetOptions remoteOptions)
        {
            m_symbolPath = symbolPath;
            m_log = log;
            m_remoteOptions = remoteOptions;
        }

        public string FindPEImage(string fileName, int buildTimeStamp, int imageSize, bool checkProperties)
        {
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

        public string FindPEImage(string fileName, SymbolProperties archivedUnder, ImmutableArray<byte> buildIdOrUUID,
            OSPlatform originalPlatform, bool checkProperties)
        {
            if (!buildIdOrUUID.IsDefaultOrEmpty && (originalPlatform == OSPlatform.Linux || originalPlatform == OSPlatform.OSX))
            {
                string name = Path.GetFileName(fileName);
                string platformKey = originalPlatform == OSPlatform.Linux ? "elf-buildid-" : "mach-uuid-";
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
