using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Utilities
{
    /// <summary>
    /// Path-related helper methods for working with file paths.
    /// </summary>
#if UTILITIES_PUBLIC
    public
#endif
    static class PathUtilities
    {
        /// <summary>
        /// Returns true if <paramref name="filePath"/> is an obviously remote path
        /// (UNC or absolute URI such as http/https/ftp/file).  This is intended as a
        /// cheap, side-effect-free pre-filter so untrusted candidate paths never
        /// reach <see cref="File.Exists(string)"/>, which on Windows triggers an SMB
        /// authentication probe for UNC paths and can leak NTLM credentials even
        /// when the target does not exist.  Returns false for null/empty input.
        /// </summary>
        public static bool IsRemotePath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            if (Uri.TryCreate(filePath, UriKind.Absolute, out Uri uri) && (!uri.IsFile || uri.IsUnc))
            {
                return true;
            }

            // Win32 extended-length / device path prefixes (\\?\ and \\.\).  These map
            // straight into the object manager and bypass normal path normalization, so
            // we have to inspect what follows the prefix:
            //   \\?\C:\foo,   \\?\Volume{...}\foo            -> local
            //   \\.\C:\foo,   \\.\PhysicalDrive0             -> local
            //   \\?\UNC\server\share, \\.\UNC\server\share   -> remote SMB
            //   \\?\GLOBALROOT\Device\Mup\...                -> remote via SMB redirector
            //   \\?\GLOBALROOT\Device\LanmanRedirector\...   -> remote via SMB redirector
            if (filePath.StartsWith(@"\\?\", StringComparison.Ordinal) ||
                filePath.StartsWith(@"\\.\", StringComparison.Ordinal))
            {
                if (filePath.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase) ||
                    filePath.StartsWith(@"\\.\UNC\", StringComparison.OrdinalIgnoreCase) ||
                    filePath.StartsWith(@"\\?\GLOBALROOT\", StringComparison.OrdinalIgnoreCase) ||
                    filePath.StartsWith(@"\\.\GLOBALROOT\", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return false;
            }

            if (filePath.StartsWith(@"\\", StringComparison.Ordinal) ||
                filePath.StartsWith("//", StringComparison.Ordinal))
            {
                return true;
            }

            // The NT object namespace prefix (\??\) bypasses Win32 path parsing and is
            // accepted by File.Exists; \??\UNC\server\share\... and
            // \??\GLOBALROOT\Device\Mup\... both reach the SMB redirector and would
            // leak credentials.  Reject all NT-namespace paths -- legitimate symbol
            // probing never uses them.
            if (filePath.StartsWith(@"\??\", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            try
            {
                string root = Path.GetPathRoot(filePath);
                return root != null &&
                    (root.StartsWith(@"\\", StringComparison.Ordinal) ||
                     root.StartsWith("//", StringComparison.Ordinal));
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        /// <summary>
        /// Returns true if <paramref name="filePath"/> normalizes to a location inside
        /// <paramref name="directoryPath"/> (or one of its subdirectories).  Used to
        /// enforce containment when a caller resolves an untrusted relative or
        /// absolute path against a trusted base directory.
        /// </summary>
        public static bool IsPathWithinDirectory(string filePath, string directoryPath)
        {
            string normalizedDirectory = Path.GetFullPath(directoryPath);
            if (!normalizedDirectory.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) &&
                !normalizedDirectory.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                normalizedDirectory += Path.DirectorySeparatorChar;
            }

            string normalizedFilePath = Path.GetFullPath(filePath);

            // Windows path comparisons are case-insensitive; POSIX file systems are not.
            // Using OrdinalIgnoreCase on a case-sensitive file system would let
            // "/trusted/Foo/bar" be treated as contained in "/trusted/foo/", silently
            // breaking the containment guarantee callers rely on.
            StringComparison comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return normalizedFilePath.StartsWith(normalizedDirectory, comparison);
        }
    }
}
