using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;

using System;
using System.IO;
using System.Security;
using System.Text;

using Xunit;

using FormatHint = Microsoft.Diagnostics.Tracing.Parsers.TdhFormatter.FormatHint;
using TdhInputType = Microsoft.Diagnostics.Tracing.Parsers.RegisteredTraceEventParser.TdhInputType;
using TdhOutputType = Microsoft.Diagnostics.Tracing.Parsers.RegisteredTraceEventParser.TdhOutputType;

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

        [Theory]
        [InlineData(FormatHint.None, TdhInputType.Int32, TdhOutputType.Null)]
        [InlineData(FormatHint.None, TdhInputType.Int32, TdhOutputType.Int)]
        [InlineData(FormatHint.None, TdhInputType.Pointer, TdhOutputType.HexBinary)]
        [InlineData(FormatHint.Hex, TdhInputType.UInt8, TdhOutputType.HexInt8)]
        [InlineData(FormatHint.Hex, TdhInputType.Int16, TdhOutputType.HexInt16)]
        [InlineData(FormatHint.Hex, TdhInputType.Int32, TdhOutputType.HexInt32)]
        [InlineData(FormatHint.Hex, TdhInputType.UInt64, TdhOutputType.HexInt64)]
        // Input type of pointer overrides hex formatting on output
        [InlineData(FormatHint.Pointer, TdhInputType.Pointer, TdhOutputType.HexInt8)]
        [InlineData(FormatHint.Pointer, TdhInputType.Pointer, TdhOutputType.HexInt16)]
        [InlineData(FormatHint.Pointer, TdhInputType.Pointer, TdhOutputType.HexInt32)]
        [InlineData(FormatHint.Pointer, TdhInputType.Pointer, TdhOutputType.HexInt64)]
        [InlineData(FormatHint.Pid, TdhInputType.UInt32, TdhOutputType.Pid)]
        [InlineData(FormatHint.Tid, TdhInputType.UInt32, TdhOutputType.Tid)]
        [InlineData(FormatHint.Port, TdhInputType.UInt16, TdhOutputType.Port)]
        [InlineData(FormatHint.IPv4, TdhInputType.UInt32, TdhOutputType.Ipv4)]
        [InlineData(FormatHint.IPv6, TdhInputType.Binary, TdhOutputType.Ipv6)]
        [InlineData(FormatHint.SocketAddress, TdhInputType.Binary, TdhOutputType.SocketAddress)]
        [InlineData(FormatHint.GenericError, TdhInputType.Int32, TdhOutputType.ErrorCode)]
        [InlineData(FormatHint.GenericError, TdhInputType.UInt32, TdhOutputType.ErrorCode)]
        [InlineData(FormatHint.Win32Error, TdhInputType.Int32, TdhOutputType.Win32Error)]
        [InlineData(FormatHint.Win32Error, TdhInputType.UInt32, TdhOutputType.Win32Error)]
        [InlineData(FormatHint.NtStatus, TdhInputType.Int32, TdhOutputType.NtStatus)]
        [InlineData(FormatHint.NtStatus, TdhInputType.UInt32, TdhOutputType.NtStatus)]
        [InlineData(FormatHint.HResult, TdhInputType.Int32, TdhOutputType.HResult)]
        [InlineData(FormatHint.HResult, TdhInputType.UInt32, TdhOutputType.HResult)]
        [InlineData(FormatHint.Pointer, TdhInputType.UInt32, TdhOutputType.CodePointer)]
        [InlineData(FormatHint.Pointer, TdhInputType.UInt64, TdhOutputType.CodePointer)]
        // HexIntXX input types are hex formatted even if there is no output hint
        [InlineData(FormatHint.Hex, TdhInputType.HexInt32, TdhOutputType.Null)]
        [InlineData(FormatHint.Hex, TdhInputType.HexInt64, TdhOutputType.Null)]
        public void ComputeFormatHintFromInOutTypes_MapsToExpectedFormatHint(FormatHint expected, object inType, object outType)
        {
            // inType/outType are passed as object to avoid CS0051: "Inconsistent accessibility:
            // parameter type 'X' is less accessible than method 'Y'"
            Assert.Equal(expected, DynamicTraceEventData.PayloadFetch.ComputeFormatHintFromInOutTypes((TdhInputType)inType, (TdhOutputType)outType));
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
