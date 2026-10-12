using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

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

        /// <summary>
        /// Returns true if <paramref name="name"/> can be used unchanged as one file-name
        /// component on Windows and POSIX systems.
        /// </summary>
        public static bool IsSafeFileName(string name)
        {
            return name != null &&
                string.Equals(name, SanitizeFileName(name), StringComparison.Ordinal);
        }

        /// <summary>
        /// Converts an absolute Windows drive path or POSIX path into safe directory
        /// components, excluding the final file name. UNC, device, relative, traversal,
        /// and otherwise unsafe paths are rejected.
        /// </summary>
        private static bool TryGetAbsoluteFileDirectorySegments(string filePath, out string[] directorySegments)
        {
            directorySegments = null;
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            var segments = new List<string>();
            int segmentStart;
            bool isWindowsPath = IsWindowsDriveAbsolutePath(filePath);
            if (isWindowsPath)
            {
                segments.Add(char.ToUpperInvariant(filePath[0]).ToString());
                segmentStart = 3;
            }
            else if (filePath[0] == '/' &&
                     (filePath.Length == 1 || filePath[1] != '/') &&
                     filePath.IndexOf('\\') < 0)
            {
                segmentStart = 1;
            }
            else
            {
                return false;
            }

            int currentStart = segmentStart;
            for (int i = segmentStart; i <= filePath.Length; i++)
            {
                bool atEnd = i == filePath.Length;
                bool atSeparator = !atEnd &&
                    (filePath[i] == '/' || (isWindowsPath && filePath[i] == '\\'));
                if (!atEnd && !atSeparator)
                {
                    continue;
                }

                if (i == currentStart)
                {
                    return false;
                }

                string segment = filePath.Substring(currentStart, i - currentStart);
                if (!IsSafeFileName(segment))
                {
                    return false;
                }

                segments.Add(segment);
                currentStart = i + 1;
            }

            if (segments.Count == 0)
            {
                return false;
            }

            segments.RemoveAt(segments.Count - 1);
            directorySegments = segments.ToArray();
            return true;
        }

        /// <summary>
        /// Canonicalizes an absolute file path that uses the current platform's path
        /// syntax and is safe for local filesystem access.
        /// </summary>
        public static bool TryGetSafeLocalFilePath(string filePath, out string safeFilePath)
        {
            safeFilePath = null;
            if (!TryGetAbsoluteFileDirectorySegments(filePath, out _))
            {
                return false;
            }

            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            bool isWindowsDrivePath = IsWindowsDriveAbsolutePath(filePath);
            if ((isWindows && !isWindowsDrivePath) ||
                (!isWindows && isWindowsDrivePath))
            {
                return false;
            }

            try
            {
                safeFilePath = Path.GetFullPath(filePath);
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
            catch (System.Security.SecurityException)
            {
                return false;
            }
        }

        /// <summary>
        /// Reduces <paramref name="name"/> to a value safe to embed in a single file-name
        /// component on disk.  Every character reported by
        /// <see cref="Path.GetInvalidFileNameChars"/>, every Windows file-name
        /// metacharacter, every path / volume separator, and every control character is
        /// replaced with '_'.  Trailing '.' and ' ' characters are removed because
        /// Windows silently trims them, which would otherwise let two distinct names
        /// collide on disk and let inputs like "NUL." slip past the reserved-name guard.
        /// Reserved DOS device names (CON, PRN, AUX, NUL, CLOCK$, CONIN$, CONOUT$,
        /// COM0-9, LPT0-9) are detected on the stem before the first '.' (Win32 opens
        /// the device for paths like "NUL.txt") and prefixed with '_' on match.
        ///
        /// Returns <c>null</c> if the input is null, empty, '.', '..', or sanitizes
        /// to an empty string so callers can choose to skip the resource rather than
        /// substitute an arbitrary placeholder.
        /// </summary>
        public static string SanitizeFileName(string name)
        {
            if (string.IsNullOrEmpty(name) || name == "." || name == "..")
            {
                return null;
            }

            HashSet<char> invalid = s_invalidFileNameChars;
            StringBuilder sanitized = new StringBuilder(name.Length);
            foreach (char c in name)
            {
                sanitized.Append(invalid.Contains(c) || char.IsControl(c) ? '_' : c);
            }

            while (sanitized.Length > 0)
            {
                char last = sanitized[sanitized.Length - 1];
                if (last != '.' && last != ' ')
                {
                    break;
                }
                sanitized.Length--;
            }

            if (sanitized.Length == 0)
            {
                return null;
            }

            string candidate = sanitized.ToString();
            int firstDot = candidate.IndexOf('.');
            string stem = firstDot < 0 ? candidate : candidate.Substring(0, firstDot);
            if (s_reservedDosDeviceNames.Contains(stem))
            {
                return "_" + candidate;
            }

            return candidate;
        }

        private static readonly HashSet<char> s_invalidFileNameChars = BuildInvalidFileNameChars();
        private static readonly HashSet<string> s_reservedDosDeviceNames = BuildReservedDosDeviceNames();

        private static HashSet<char> BuildInvalidFileNameChars()
        {
            HashSet<char> chars = new HashSet<char>(Path.GetInvalidFileNameChars());
            chars.Add('\0');
            chars.Add(Path.DirectorySeparatorChar);
            chars.Add(Path.AltDirectorySeparatorChar);
            chars.Add(Path.VolumeSeparatorChar);
            chars.Add('\\');
            chars.Add('/');
            chars.Add(':');
            chars.Add('<');
            chars.Add('>');
            chars.Add('"');
            chars.Add('|');
            chars.Add('?');
            chars.Add('*');
            return chars;
        }

        private static HashSet<string> BuildReservedDosDeviceNames()
        {
            HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "CON", "PRN", "AUX", "NUL", "CLOCK$", "CONIN$", "CONOUT$",
            };
            for (int i = 0; i <= 9; i++)
            {
                names.Add("COM" + i);
                names.Add("LPT" + i);
            }
            return names;
        }

        private static bool IsWindowsDriveAbsolutePath(string path)
        {
            return path.Length >= 3 &&
                   ((path[0] >= 'A' && path[0] <= 'Z') || (path[0] >= 'a' && path[0] <= 'z')) &&
                   path[1] == ':' &&
                   (path[2] == '\\' || path[2] == '/');
        }
    }
}
