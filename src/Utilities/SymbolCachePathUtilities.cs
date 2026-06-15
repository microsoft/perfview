using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Utilities
{
    /// <summary>
    /// Utilities for validating paths that will be combined with a symbol cache
    /// directory before a file is extracted into it.  Designed to prevent
    /// path-traversal and prefix-collision attacks where a malicious archive
    /// entry name could cause a file to be written outside the symbol cache or
    /// could overwrite an unrelated cache entry.
    /// </summary>
#if UTILITIES_PUBLIC
    public
#endif
    static class SymbolCachePathUtilities
    {
        /// <summary>
        /// Validates that <paramref name="pdbRelativePath"/> can be safely combined with
        /// <paramref name="symbolDirectory"/> to form a PDB extraction target that lives
        /// inside <paramref name="symbolDirectory"/>.  Rejects null/empty inputs, any path
        /// containing a <c>..</c> segment (including whitespace-padded variants), any
        /// <c>:</c> character, and any
        /// combined path that would land outside <paramref name="symbolDirectory"/> after
        /// canonicalization.
        /// </summary>
        /// <param name="symbolDirectory">The directory under which the file must be written.</param>
        /// <param name="pdbRelativePath">The relative path of the file (e.g. as it appeared in an
        /// archive entry name).</param>
        /// <param name="pdbTargetPath">On success, the canonicalized absolute path where the file
        /// should be written; otherwise <c>null</c>.</param>
        /// <returns><c>true</c> if the combined path is safe to use; otherwise <c>false</c>.</returns>
        public static bool TryGetPdbTargetPath(string symbolDirectory, string pdbRelativePath, out string pdbTargetPath)
        {
            pdbTargetPath = null;

            if (string.IsNullOrEmpty(symbolDirectory) || string.IsNullOrEmpty(pdbRelativePath))
            {
                return false;
            }

            // Reject any traversal segment.  Even when '..' cancels out inside the symbol directory,
            // it can be used to overwrite an unrelated cache entry (e.g. evil.pdb\HASH\..\..\real.pdb).
            // Trim handles whitespace-padded variants like ".. " that some filesystems may otherwise
            // accept or that downstream code might collapse.  Also reject ':' to prevent NTFS alternate
            // data stream names and Windows drive-qualified paths from reaching extraction.
            foreach (string segment in pdbRelativePath.Split('\\', '/'))
            {
                if (segment.Trim() == ".." || segment.Contains(":"))
                {
                    return false;
                }
            }

            try
            {
                string fullSymbolDirectory = Path.GetFullPath(symbolDirectory);
                string fullPdbTargetPath = Path.GetFullPath(Path.Combine(fullSymbolDirectory, pdbRelativePath));

                string symbolDirectoryPrefix = fullSymbolDirectory[fullSymbolDirectory.Length - 1] == Path.DirectorySeparatorChar
                    ? fullSymbolDirectory
                    : fullSymbolDirectory + Path.DirectorySeparatorChar;

                StringComparison directoryComparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;

                if (!fullPdbTargetPath.StartsWith(symbolDirectoryPrefix, directoryComparison))
                {
                    return false;
                }

                pdbTargetPath = fullPdbTargetPath;
                return true;
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
        }
    }
}
