using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Xunit;

namespace TraceEventTests
{
    public class ZippedETLTests
    {
        [Fact]
        public void UnpackArchiveExtractsValidPdbIntoSymbolDirectory()
        {
            WithTempDirectory(tempDir =>
            {
                string zipPath = Path.Combine(tempDir, "trace.etl.zip");
                string symbolDirectory = Path.Combine(tempDir, "symbols");
                const string pdbRelativePath = @"valid.pdb\ABCDEF1234567890\valid.pdb";
                const string pdbContents = "valid pdb contents";

                CreateZippedEtl(
                    zipPath,
                    new[] { new KeyValuePair<string, string>(@"symbols\" + pdbRelativePath, pdbContents) });

                var reader = new ZippedETLReader(zipPath, new StringWriter())
                {
                    EtlFileName = Path.Combine(tempDir, "trace.etl"),
                    SymbolDirectory = symbolDirectory
                };

                reader.UnpackArchive();

                Assert.Equal(pdbContents, File.ReadAllText(Path.Combine(symbolDirectory, pdbRelativePath)));
            });
        }

        [Fact]
        public void UnpackArchiveRejectsPdbTraversalAfterPrefixStripping()
        {
            WithTempDirectory(tempDir =>
            {
                string zipPath = Path.Combine(tempDir, "trace.etl.zip");
                string symbolDirectory = Path.Combine(tempDir, "symbols");
                var log = new StringWriter();

                CreateZippedEtl(
                    zipPath,
                    new[]
                    {
                        new KeyValuePair<string, string>("symbols/../outside.pdb", "traversal"),
                        new KeyValuePair<string, string>("payload.ngenpdbs/../prefixCollision.pdb", "prefix collision"),
                    });

                var reader = new ZippedETLReader(zipPath, log)
                {
                    EtlFileName = Path.Combine(tempDir, "trace.etl"),
                    SymbolDirectory = symbolDirectory
                };

                reader.UnpackArchive();

                Assert.False(File.Exists(Path.Combine(tempDir, "outside.pdb")));
                Assert.False(File.Exists(Path.Combine(tempDir, "prefixCollision.pdb")));
                Assert.Contains("invalid path", log.ToString());
            });
        }

        [Fact]
        public void UnpackArchiveDoesNotStripDiagsessionPrefixFromMiddleOfPath()
        {
            WithTempDirectory(tempDir =>
            {
                string zipPath = Path.Combine(tempDir, "trace.etl.zip");
                string symbolDirectory = Path.Combine(tempDir, "symbols");
                const string pdbName = "prefixCollision.pdb";
                var log = new StringWriter();

                CreateZippedEtl(
                    zipPath,
                    new[]
                    {
                        new KeyValuePair<string, string>(
                            @"prefix\194BAE98-C4ED-470E-9204-1F9389FC9DC1\symcache\" + pdbName,
                            "prefix collision"),
                    });

                var reader = new ZippedETLReader(zipPath, log)
                {
                    EtlFileName = Path.Combine(tempDir, "trace.etl"),
                    SymbolDirectory = symbolDirectory
                };

                reader.UnpackArchive();

                Assert.False(File.Exists(Path.Combine(symbolDirectory, pdbName)));
                Assert.Contains("not in a symbol server style directory", log.ToString());
            });
        }

        [Fact]
        public void UnpackArchiveExtractsDiagsessionPdbFromRootPrefix()
        {
            WithTempDirectory(tempDir =>
            {
                string zipPath = Path.Combine(tempDir, "trace.etl.zip");
                string symbolDirectory = Path.Combine(tempDir, "symbols");
                const string pdbName = "valid.pdb";
                const string pdbContents = "valid pdb contents";

                CreateZippedEtl(
                    zipPath,
                    new[]
                    {
                        new KeyValuePair<string, string>(
                            @"194BAE98-C4ED-470E-9204-1F9389FC9DC1\symcache\" + pdbName,
                            pdbContents),
                    });

                var reader = new ZippedETLReader(zipPath, new StringWriter())
                {
                    EtlFileName = Path.Combine(tempDir, "trace.etl"),
                    SymbolDirectory = symbolDirectory
                };

                reader.UnpackArchive();

                Assert.Equal(pdbContents, File.ReadAllText(Path.Combine(symbolDirectory, pdbName)));
            });
        }

        private static void CreateZippedEtl(string zipPath, IEnumerable<KeyValuePair<string, string>> pdbEntries)
        {
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                ZipArchiveEntry etlEntry = archive.CreateEntry("trace.etl");
                using (var writer = new StreamWriter(etlEntry.Open()))
                {
                    writer.Write("etl");
                }

                foreach (KeyValuePair<string, string> pdbEntry in pdbEntries)
                {
                    ZipArchiveEntry entry = archive.CreateEntry(pdbEntry.Key);
                    using (var writer = new StreamWriter(entry.Open()))
                    {
                        writer.Write(pdbEntry.Value);
                    }
                }
            }
        }

        private static void WithTempDirectory(Action<string> action)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "ZippedETLTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                action(tempDir);
            }
            finally
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    public class SymbolCachePathUtilitiesTests
    {
        // Paths that must be accepted.
        [Theory]
        [InlineData(@"module.pdb\ABCDEF1234567890ABCDEF1234567890\module.pdb")]
        [InlineData("module.pdb/ABCDEF1234567890ABCDEF1234567890/module.pdb")]
        [InlineData(@"sub\module.pdb\ABCDEF1234567890ABCDEF1234567890\module.pdb")]
        public void AcceptsValidSymbolServerStylePaths(string pdbRelativePath)
        {
            string symbolCache = @"C:\SymbolCache";

            bool ok = SymbolCachePathUtilities.TryGetPdbTargetPath(symbolCache, pdbRelativePath, out string pdbTargetPath);

            Assert.True(ok, $"Expected path '{pdbRelativePath}' to be accepted.");
            Assert.NotNull(pdbTargetPath);
            string fullCache = Path.GetFullPath(symbolCache);
            Assert.StartsWith(
                fullCache + Path.DirectorySeparatorChar,
                pdbTargetPath,
                StringComparison.OrdinalIgnoreCase);
        }

        // Paths that must be rejected: traversal, whitespace-padded traversal, rooted paths,
        // and paths that escape the cache via prefix-collision or sibling-cache tricks.
        [Theory]
        [InlineData(@"..\outside.pdb")]
        [InlineData(@"module.pdb\..\outside.pdb")]
        [InlineData("module.pdb/../outside.pdb")]
        [InlineData(@"module.pdb\.. \outside.pdb")]
        [InlineData(@"module.pdb\ ..\outside.pdb")]
        [InlineData(@"module.pdb\ .. \outside.pdb")]
        [InlineData(@"\rooted\module.pdb")]
        [InlineData(@"C:\rooted\module.pdb")]
        [InlineData(@"C:rooted\module.pdb")]
        [InlineData(@"module.pdb:stream\ABCDEF1234567890ABCDEF1234567890\module.pdb")]
        [InlineData(@"module.pdb\ABCDEF1234567890ABCDEF1234567890\module.pdb:stream")]
        [InlineData(@"..\SymbolCache2\module.pdb\ABCDEF1234567890ABCDEF1234567890\module.pdb")]
        public void RejectsUnsafePaths(string pdbRelativePath)
        {
            string symbolCache = @"C:\SymbolCache";

            bool ok = SymbolCachePathUtilities.TryGetPdbTargetPath(symbolCache, pdbRelativePath, out string pdbTargetPath);

            Assert.False(ok, $"Expected path '{pdbRelativePath}' to be rejected.");
            Assert.Null(pdbTargetPath);
        }

        [Theory]
        [InlineData(null, @"module.pdb\HEX\module.pdb")]
        [InlineData("", @"module.pdb\HEX\module.pdb")]
        [InlineData(@"C:\SymbolCache", null)]
        [InlineData(@"C:\SymbolCache", "")]
        public void RejectsNullOrEmptyInputs(string symbolCache, string pdbRelativePath)
        {
            bool ok = SymbolCachePathUtilities.TryGetPdbTargetPath(symbolCache, pdbRelativePath, out string pdbTargetPath);

            Assert.False(ok);
            Assert.Null(pdbTargetPath);
        }
    }
}
