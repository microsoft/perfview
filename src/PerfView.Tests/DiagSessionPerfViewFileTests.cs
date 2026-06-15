using PerfView;
using System.IO;
using System.Linq;
using Xunit;

namespace PerfViewTests
{
    public class DiagSessionPerfViewFileTests
    {
        [Theory]
        [InlineData(@"..\..\victim", "victim")]
        [InlineData(@"\..\..\Startup", "Startup")]
        [InlineData(@"C:\temp\symbols", "symbols")]
        [InlineData("symbols/cache", "cache")]
        [InlineData("symbols\\cache", "cache")]
        [InlineData("foo.bar", "foo")]
        [InlineData("plain", "plain")]
        [InlineData("C:relative", "relative")]
        [InlineData("foo:bar", "bar")]
        public void GetSafeDiagSessionResourceDirectoryName_StripsPathComponents(string resourceName, string expected)
        {
            string safeName = DiagSessionPerfViewFile.GetSafeDiagSessionResourceDirectoryName(resourceName);

            Assert.Equal(expected, safeName);
            Assert.DoesNotContain(Path.DirectorySeparatorChar, safeName);
            Assert.DoesNotContain(Path.AltDirectorySeparatorChar, safeName);
            Assert.DoesNotContain(safeName, c => Path.GetInvalidFileNameChars().Contains(c));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(".")]
        [InlineData("..")]
        [InlineData(@"\")]
        [InlineData("/")]
        [InlineData(@"foo\..")]
        [InlineData(@"foo\.")]
        public void GetSafeDiagSessionResourceDirectoryName_RejectsUnsafeNames(string resourceName)
        {
            string safeName = DiagSessionPerfViewFile.GetSafeDiagSessionResourceDirectoryName(resourceName);

            Assert.Null(safeName);
        }
    }
}
