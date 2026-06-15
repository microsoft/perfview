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
    }
}
