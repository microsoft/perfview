using Microsoft.Diagnostics.Utilities;
using Xunit;

namespace PerfViewTests.Memory
{
    public class PathUtilitiesTests
    {
        [Theory]
        [InlineData(@"\\server\share\module.dll")]
        [InlineData(@"\\?\UNC\server\share\module.dll")]
        [InlineData(@"\\?\unc\server\share\module.dll")]
        [InlineData(@"\\.\UNC\server\share\module.dll")]
        [InlineData(@"\\?\GLOBALROOT\Device\Mup\server\share\module.dll")]
        [InlineData(@"\\?\GLOBALROOT\Device\LanmanRedirector\server\share\module.dll")]
        [InlineData(@"\??\UNC\server\share\module.dll")]
        [InlineData(@"\??\unc\server\share\module.dll")]
        [InlineData("https://server/share/module.dll")]
        [InlineData("http://server/share/module.dll")]
        [InlineData("ftp://server/module.dll")]
        public void IsRemotePathDetectsRemotePaths(string modulePath)
        {
            Assert.True(PathUtilities.IsRemotePath(modulePath));
        }

        [Theory]
        [InlineData(@"C:\Symbols\foo.pdb\01234567890123456789012345678901FFFFFFFF\foo.pdb")]
        [InlineData(@"C:\Users\dev\src\bin\foo.dll")]
        [InlineData(@"module.dll")]
        [InlineData(@"subdir\module.dll")]
        [InlineData(@"..\module.dll")]
        [InlineData(@"D:\drive\path.dll")]
        [InlineData(@"\\?\C:\Windows\notepad.exe")]
        [InlineData(@"\\.\C:\Windows\notepad.exe")]
        [InlineData(@"\\?\Volume{12345678-1234-1234-1234-1234567890ab}\foo.dll")]
        public void IsRemotePathAcceptsLocalPaths(string modulePath)
        {
            Assert.False(PathUtilities.IsRemotePath(modulePath));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void IsRemotePathHandlesEmptyInputWithoutThrowing(string modulePath)
        {
            // Empty/null inputs are not remote (they will be caught elsewhere).
            Assert.False(PathUtilities.IsRemotePath(modulePath));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(".")]
        [InlineData("..")]
        public void SanitizeFileName_ReturnsNullForRejectedInput(string input)
        {
            // null / empty / "." / ".." all return null so callers can choose how to
            // handle the missing name (skip the resource, substitute a placeholder,
            // etc.) instead of being forced to accept an arbitrary string.
            Assert.Null(PathUtilities.SanitizeFileName(input));
        }

        [Theory]
        [InlineData(@"..\outside", ".._outside")]
        [InlineData(@"..\..\..\Startup\x", ".._.._.._Startup_x")]
        [InlineData("../forward/slash", ".._forward_slash")]
        [InlineData(@"C:\Windows\System32\evil", "C__Windows_System32_evil")]
        [InlineData(@"\\server\share\evil", "__server_share_evil")]
        [InlineData(@"with:colons", "with_colons")]
        [InlineData("with|pipes?and*wildcards", "with_pipes_and_wildcards")]
        public void SanitizeFileName_ReplacesInvalidCharactersAndSeparators(string input, string expected)
        {
            // Every path separator, volume separator, and Path.GetInvalidFileNameChars
            // character is replaced with '_'.  Control characters are also replaced.
            Assert.Equal(expected, PathUtilities.SanitizeFileName(input));
        }

        [Theory]
        [InlineData("CON", "_CON")]
        [InlineData("nul", "_nul")]
        [InlineData("PRN", "_PRN")]
        [InlineData("AUX", "_AUX")]
        [InlineData("COM1", "_COM1")]
        [InlineData("LPT9", "_LPT9")]
        [InlineData("CLOCK$", "_CLOCK$")]
        [InlineData("CONIN$", "_CONIN$")]
        [InlineData("conout$", "_conout$")]
        [InlineData("CONIN$.log", "_CONIN$.log")]
        [InlineData("NUL.", "_NUL")]
        [InlineData("NUL ", "_NUL")]
        [InlineData("NUL. . ", "_NUL")]
        [InlineData("NUL.evil", "_NUL.evil")]
        [InlineData("CON.foo.bar", "_CON.foo.bar")]
        [InlineData("com1.data", "_com1.data")]
        [InlineData("LPT5.tar.gz", "_LPT5.tar.gz")]
        public void SanitizeFileName_RewritesReservedDosDeviceNames(string input, string expected)
        {
            // Win32 matches a reserved device name on the stem before the first '.'
            // in the basename, so "NUL.evil" or "COM1.data" still open the device.
            // The sanitizer prefixes such names with '_' to make them safe.
            Assert.Equal(expected, PathUtilities.SanitizeFileName(input));
        }

        [Theory]
        [InlineData("Trailing.", "Trailing")]
        [InlineData("Trailing ", "Trailing")]
        [InlineData("Trailing.. .", "Trailing")]
        public void SanitizeFileName_StripsTrailingDotsAndSpaces(string input, string expected)
        {
            // Windows silently trims trailing '.' and ' ' from file names; stripping
            // them ourselves prevents two distinct names colliding on disk and
            // closes the "NUL." device-name evasion.
            Assert.Equal(expected, PathUtilities.SanitizeFileName(input));
        }

        [Theory]
        [InlineData("...")]
        [InlineData("   ")]
        public void SanitizeFileName_ReturnsNullWhenAllCharactersAreStripped(string input)
        {
            // Inputs that consist entirely of trailing-trim characters (or sanitize
            // to nothing) return null rather than the empty string.
            Assert.Null(PathUtilities.SanitizeFileName(input));
        }

        [Theory]
        [InlineData("Contoso.Provider-Valid_1", "Contoso.Provider-Valid_1")]
        [InlineData("My Provider", "My Provider")]
        [InlineData("provider.with.dots", "provider.with.dots")]
        public void SanitizeFileName_PreservesValidNames(string input, string expected)
        {
            Assert.Equal(expected, PathUtilities.SanitizeFileName(input));
        }
    }
}

