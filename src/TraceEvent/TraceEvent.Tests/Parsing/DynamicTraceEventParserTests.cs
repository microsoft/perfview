using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using System;
using System.IO;
using System.Security;
using System.Text;
using Xunit;

namespace TraceEventTests
{
    public class DynamicTraceEventParserTests
    {
        [Theory]
        [InlineData(@"..\outside", ".._outside.manifest.xml")]
        [InlineData(@"..\..\..\Startup\x", ".._.._.._Startup_x.manifest.xml")]
        [InlineData(@"C:\Windows\System32\evil", "C__Windows_System32_evil.manifest.xml")]
        public void WriteAllManifests_SanitizesPathTraversalProviderNames(string providerName, string expectedFileName)
        {
            // Integration smoke test: an attacker-controlled provider name containing
            // path-traversal characters must produce a manifest file whose name has
            // been sanitized AND whose final path remains inside the requested
            // directory.  The exhaustive sanitizer-behavior tests live in
            // PathUtilitiesTests.SanitizeFileName_*.
            string parentDirectory = CreateTempDirectory();
            string outputDirectory = Path.Combine(parentDirectory, "manifests");
            try
            {
                using (var source = new EventPipeEventSource(new MemoryStream()))
                {
                    var parser = new DynamicTraceEventParser(source);
                    parser.AddDynamicProvider(CreateProviderManifest(providerName, Guid.NewGuid()));

                    parser.WriteAllManifests(outputDirectory);
                }

                string[] manifestFiles = Directory.GetFiles(outputDirectory, "*.manifest.xml");
                string manifestPath = Assert.Single(manifestFiles);
                Assert.Equal(expectedFileName, Path.GetFileName(manifestPath));
                AssertPathInDirectory(manifestPath, outputDirectory);

                // No files leaked outside the output directory.
                Assert.Empty(Directory.GetFiles(parentDirectory, "*.manifest.xml", SearchOption.TopDirectoryOnly));
            }
            finally
            {
                Directory.Delete(parentDirectory, recursive: true);
            }
        }

        [Fact]
        public void WriteAllManifests_FallsBackToProviderGuidForEmptyName()
        {
            // PathUtilities.SanitizeFileName returns null for empty input;
            // WriteAllManifests then falls back to the provider's GUID so distinct
            // providers still produce distinct manifest files (rather than
            // colliding on a single "_.manifest.xml").
            Guid providerGuid = new Guid("12345678-1234-1234-1234-1234567890ab");
            string outputDirectory = CreateTempDirectory();
            try
            {
                using (var source = new EventPipeEventSource(new MemoryStream()))
                {
                    var parser = new DynamicTraceEventParser(source);
                    parser.AddDynamicProvider(CreateProviderManifest(string.Empty, providerGuid));

                    parser.WriteAllManifests(outputDirectory);
                }

                string[] manifestFiles = Directory.GetFiles(outputDirectory, "*.manifest.xml");
                string manifestPath = Assert.Single(manifestFiles);
                Assert.Equal(providerGuid.ToString("N") + ".manifest.xml", Path.GetFileName(manifestPath));
            }
            finally
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }

        [Fact]
        public void WriteAllManifests_PreservesValidProviderName()
        {
            string outputDirectory = CreateTempDirectory();
            try
            {
                const string providerName = "Contoso.Provider-Valid_1";
                using (var source = new EventPipeEventSource(new MemoryStream()))
                {
                    var parser = new DynamicTraceEventParser(source);
                    parser.AddDynamicProvider(CreateProviderManifest(providerName, Guid.NewGuid()));

                    parser.WriteAllManifests(outputDirectory);
                }

                string manifestPath = Path.Combine(outputDirectory, providerName + ".manifest.xml");
                Assert.True(File.Exists(manifestPath));
                Assert.Contains("name=\"" + providerName + "\"", File.ReadAllText(manifestPath));
            }
            finally
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }

        [Fact]
        public void WriteAllManifests_CreatesOutputDirectory()
        {
            string parentDirectory = CreateTempDirectory();
            string outputDirectory = Path.Combine(parentDirectory, "nested", "manifests");
            try
            {
                using (var source = new EventPipeEventSource(new MemoryStream()))
                {
                    var parser = new DynamicTraceEventParser(source);
                    parser.AddDynamicProvider(CreateProviderManifest("Contoso.Provider", Guid.NewGuid()));

                    parser.WriteAllManifests(outputDirectory);
                }

                Assert.True(Directory.Exists(outputDirectory));
                Assert.Single(Directory.GetFiles(outputDirectory, "*.manifest.xml"));
            }
            finally
            {
                Directory.Delete(parentDirectory, recursive: true);
            }
        }

        [Fact]
        public void WriteAllManifests_MultipleProvidersCollidingAfterSanitizationLastWriterWins()
        {
            // Two distinct attacker-controlled provider names that sanitize to the
            // same file name.  The current contract is that the second WriteToFile
            // overwrites the first -- this test pins that behavior so it does not
            // silently regress to e.g. throwing or corrupting the output directory.
            string outputDirectory = CreateTempDirectory();
            try
            {
                using (var source = new EventPipeEventSource(new MemoryStream()))
                {
                    var parser = new DynamicTraceEventParser(source);
                    parser.AddDynamicProvider(CreateProviderManifest(@"a\b", Guid.NewGuid()));
                    parser.AddDynamicProvider(CreateProviderManifest("a/b", Guid.NewGuid()));

                    parser.WriteAllManifests(outputDirectory);
                }

                string[] manifestFiles = Directory.GetFiles(outputDirectory, "*.manifest.xml");
                string manifestPath = Assert.Single(manifestFiles);
                Assert.Equal("a_b.manifest.xml", Path.GetFileName(manifestPath));
                AssertPathInDirectory(manifestPath, outputDirectory);
            }
            finally
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }

        private static ProviderManifest CreateProviderManifest(string providerName, Guid providerGuid)
        {
            string manifest =
                "<instrumentationManifest xmlns=\"http://schemas.microsoft.com/win/2004/08/events\">" +
                "  <instrumentation>" +
                "    <events>" +
                "      <provider name=\"" + SecurityElement.Escape(providerName) + "\" guid=\"" + providerGuid + "\" />" +
                "    </events>" +
                "  </instrumentation>" +
                "</instrumentationManifest>";

            return new ProviderManifest(new MemoryStream(Encoding.UTF8.GetBytes(manifest)));
        }

        private static string CreateTempDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), "TraceEventTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        private static void AssertPathInDirectory(string path, string directoryPath)
        {
            string fullDirectoryPath = Path.GetFullPath(directoryPath);
            if (!fullDirectoryPath.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) &&
                !fullDirectoryPath.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                fullDirectoryPath += Path.DirectorySeparatorChar;
            }

            Assert.StartsWith(fullDirectoryPath, Path.GetFullPath(path), StringComparison.OrdinalIgnoreCase);
        }
    }
}
