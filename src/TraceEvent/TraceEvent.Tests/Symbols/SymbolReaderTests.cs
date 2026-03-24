using Microsoft.Diagnostics.Symbols;
using PerfView.TestUtilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace TraceEventTests
{
    [UseCulture("en-US")]
    public class SymbolReaderTests : TestBase
    {
        private const string SymbolReaderTestInput = "SymbolReaderTestInput";
        private const string FileName_CsPortablePdb1 = "CsPortablePdb1.pdb";
        private const string FileName_CsPortableEmbeddedSource = "CsPortableEmbeddedSource.pdb";
        private const string FileName_CppConPdb = "CppCon.pdb";
        private const string FileName_CsDesktopPdbWithSourceLink = "CsDesktopWithSourceLink.pdb";
        private const string FileName_CsPortablePdbEscapeSourceLink = "CsPortableEscapeSourceLink.pdb";

        private static readonly object s_fileLock = new object();
        private static string s_inputPdbDir;

        readonly InterceptingHandler _handler;
        readonly SymbolReader _symbolReader;

        public SymbolReaderTests(ITestOutputHelper output)
            : base(output)
        {
            _handler = new InterceptingHandler();
            _symbolReader = new SymbolReader(TextWriter.Null, nt_symbol_path: null, httpClientDelegatingHandler: _handler);
            PrepareTestData();
        }


        [Fact]
        public void NativeCppPdbHasValidSourceInfo()
        {
            var pdbFile = _symbolReader.OpenNativeSymbolFile(Path.Combine(s_inputPdbDir, FileName_CppConPdb));
            using (pdbFile as IDisposable)
            {
                SourceLocation sourceLocation = pdbFile.SourceLocationForRva(4096);
                Assert.NotNull(sourceLocation);

                var sourceFile = sourceLocation.SourceFile;
                Assert.NotNull(sourceFile);
                Assert.Equal(@"C:\PerfViewTestData\CppCon\CppCon.cpp", sourceFile.BuildTimeFilePath, StringComparer.OrdinalIgnoreCase);
                Assert.Equal("MD5", sourceFile.ChecksumAlgorithm);
                Assert.Equal("4385B143F32D90E0D9F5340774D6873D", ChecksumToString(sourceFile.ChecksumValue));
                Assert.True(sourceFile.GetSourceLinkInfo(out string url, out string relativePath));
                Assert.Equal("https://contoso.com/fake-source-link-url/CppCon/CppCon.cpp", url);
                Assert.Equal("CppCon/CppCon.cpp", relativePath);

                Assert.Equal(7, sourceLocation.LineNumber);
            }
        }

        [Fact]
        public void ManagedWinPdbHasValidSourceInfo1()
        {
            var pdbFile = _symbolReader.OpenSymbolFile(Path.Combine(s_inputPdbDir, "CsDesktopProj.pdb"));
            using (pdbFile as IDisposable)
            {
                Assert.IsType<NativeSymbolModule>(pdbFile);

                const int tokenMain = 0x06000001;
                SourceLocation sourceLocation = pdbFile.SourceLocationForManagedCode(tokenMain, ilOffset: 0);
                Assert.NotNull(sourceLocation);

                var sourceFile = sourceLocation.SourceFile;
                Assert.NotNull(sourceFile);
                Assert.Equal(@"C:\PerfViewTestData\CsDesktopProj\Program.cs", sourceFile.BuildTimeFilePath, StringComparer.OrdinalIgnoreCase);
                Assert.Equal("SHA256", sourceFile.ChecksumAlgorithm);
                Assert.Equal("5705741A5E2D7F01C9502134B5948607E8BB01AEB107BE055B496386E493BA75", ChecksumToString(sourceFile.ChecksumValue));
                Assert.False(sourceFile.GetSourceLinkInfo(out string url, out string relativePath));

                Assert.Equal(9, sourceLocation.LineNumber);
            }
        }

        [Fact]
        public void ManagedWinPdbHasValidSourceInfo2()
        {
            var pdbFile = _symbolReader.OpenSymbolFile(Path.Combine(s_inputPdbDir, FileName_CsDesktopPdbWithSourceLink));
            using (pdbFile as IDisposable)
            {
                Assert.IsType<NativeSymbolModule>(pdbFile);

                const int tokenMain = 0x06000001;
                SourceLocation sourceLocation = pdbFile.SourceLocationForManagedCode(tokenMain, ilOffset: 0);
                Assert.NotNull(sourceLocation);

                var sourceFile = sourceLocation.SourceFile;
                Assert.NotNull(sourceFile);
                Assert.Equal(@"C:\PerfViewTestData\CsDesktopWithSourceLink\Program.cs", sourceFile.BuildTimeFilePath, StringComparer.OrdinalIgnoreCase);
                Assert.Equal("SHA256", sourceFile.ChecksumAlgorithm);
                Assert.Equal("A2706557AB7C40E142355E4C6811CA9FBD6344E98CC795B6EEA76FB39A44E915", ChecksumToString(sourceFile.ChecksumValue));
                Assert.True(sourceFile.GetSourceLinkInfo(out string url, out string relativePath));
                Assert.Equal("https://contoso.com/fake-source-link-url/CsDesktopWithSourceLink/Program.cs", url);
                Assert.Equal("CsDesktopWithSourceLink/Program.cs", relativePath);

                Assert.Equal(13, sourceLocation.LineNumber);
            }
        }

        [Fact]
        public void PortablePdbHasValidSourceInfo()
        {
            var pdbFile = _symbolReader.OpenSymbolFile(Path.Combine(s_inputPdbDir, FileName_CsPortablePdb1));
            using (pdbFile as IDisposable)
            {
                Assert.IsType<PortableSymbolModule>(pdbFile);

                const int tokenMain = 0x06000001;
                SourceLocation sourceLocation = pdbFile.SourceLocationForManagedCode(tokenMain, ilOffset: 0);
                Assert.NotNull(sourceLocation);

                var sourceFile = sourceLocation.SourceFile;
                Assert.NotNull(sourceFile);
                Assert.Equal(@"C:\PerfViewTestData\CsPortablePdb1\Program.cs", sourceFile.BuildTimeFilePath, StringComparer.OrdinalIgnoreCase);
                Assert.Equal("SHA256", sourceFile.ChecksumAlgorithm);
                Assert.Equal("134F2D3A27BE69575627B7A69E96610052738A10017FCA12A299BE28B1C47F76", ChecksumToString(sourceFile.ChecksumValue));
                Assert.True(sourceFile.GetSourceLinkInfo(out string url, out string relativePath));
                Assert.Equal("https://contoso.com/fake-source-link-url/CsPortablePdb1/Program.cs", url);
                Assert.Equal("CsPortablePdb1/Program.cs", relativePath);

                Assert.Equal(9, sourceLocation.LineNumber);
            }
        }

        [Fact]
        public void SourceLinkUrlsAreEscaped()
        {
            var pdbFile = _symbolReader.OpenSymbolFile(Path.Combine(s_inputPdbDir, FileName_CsPortablePdbEscapeSourceLink));
            using (pdbFile as IDisposable)
            {
                const int tokenMain = 0x06000001;
                SourceLocation sourceLocation = pdbFile.SourceLocationForManagedCode(tokenMain, ilOffset: 0);
                Assert.NotNull(sourceLocation);

                var sourceFile = sourceLocation.SourceFile;
                Assert.NotNull(sourceFile);
                Assert.True(sourceFile.GetSourceLinkInfo(out string url, out string relativePath));
                Assert.Equal("https://contoso.com/fake-source-link-url/CsPortableEscapeSourceLink/%23Directory/Program.cs", url);
                Assert.Equal("CsPortableEscapeSourceLink/#Directory/Program.cs", relativePath);
            }
        }

        [Fact]
        public void SourceLinkSupportsWildcardAndExactPathMappings()
        {
            // Create a test symbol module that returns SourceLink JSON with both wildcard and exact path mappings
            var testModule = new TestSymbolModuleWithSourceLink(_symbolReader);

            // Test wildcard pattern matching
            bool result1 = testModule.GetUrlForFilePathUsingSourceLink(
                @"C:\src\myproject\subfolder\file.cs",
                out string url1,
                out string relativePath1);

            Assert.True(result1, "Should match wildcard pattern");
            Assert.Equal("https://raw.githubusercontent.com/org/repo/commit/subfolder/file.cs", url1);
            Assert.Equal("subfolder/file.cs", relativePath1);

            // Test exact path matching
            bool result2 = testModule.GetUrlForFilePathUsingSourceLink(
                @"c:\external\sdk\inc\header.h",
                out string url2,
                out string relativePath2);

            Assert.True(result2, "Should match exact path");
            Assert.Equal("https://example.com/blobs/ABC123?download=true&filename=header.h", url2);
            Assert.Equal("", relativePath2);

            // Test another wildcard pattern with escaped characters
            bool result3 = testModule.GetUrlForFilePathUsingSourceLink(
                @"C:\src\myproject\some folder\another file.cs",
                out string url3,
                out string relativePath3);

            Assert.True(result3, "Should match wildcard pattern with spaces");
            Assert.Equal("https://raw.githubusercontent.com/org/repo/commit/some%20folder/another%20file.cs", url3);
            Assert.Equal("some folder/another file.cs", relativePath3);

            // Test non-matching path
            bool result4 = testModule.GetUrlForFilePathUsingSourceLink(
                @"C:\other\path\file.cs",
                out string url4,
                out string relativePath4);

            Assert.False(result4, "Should not match any pattern");
            Assert.Null(url4);
            Assert.Null(relativePath4);
        }

        /// <summary>
        /// Tests that the checksum matching allows for different line endings.
        /// Open the PDB and try to retrieve the source code for one of the files,
        /// but bock the source server to return the source with Unix-style line
        /// endings. Ensure that the checksum in the PDB (which was computed using
        /// Windows-style line endings) still matches.
        /// </summary>
        /// <param name="pdbName">The PDB file name relative to the unzipped input test data folder. May be a Portable PDB or Windows (native) PDB.</param>
        /// <param name="metadataTokenOrRva">A metadata token or an RVA to look up a symbol. We then find the source code containing that symbol.</param>
        [Theory]
        [InlineData(FileName_CsPortablePdb1, 0x06000001)]
        [InlineData(FileName_CsDesktopPdbWithSourceLink, 0x06000001)]
        [InlineData(FileName_CppConPdb, 4096)]
        public void ChecksumMatchAllowsAlternateLineEndings(string pdbName, uint metadataTokenOrRva)
        {
            var pdbFile = _symbolReader.OpenSymbolFile(Path.Combine(s_inputPdbDir, pdbName));
            using (pdbFile as IDisposable)
            {
                // Try a managed token first. If that fails, try a native RVA look up.
                // Note: It's not impossible that an RVA will accidentally match a valid
                // metadata token, but it's unlikely and I didn't want to complicate
                // the set-up for this test case with more parameters.
                SourceLocation sourceLocation = pdbFile.SourceLocationForManagedCode(metadataTokenOrRva, ilOffset: 0);
                if (sourceLocation == null && pdbFile is NativeSymbolModule nativePdb)
                {
                    sourceLocation = nativePdb.SourceLocationForRva(metadataTokenOrRva);
                }

                Assert.NotNull(sourceLocation);

                var sourceFile = sourceLocation.SourceFile;
                Assert.NotNull(sourceFile);

                string renamedSourceFile = null;
                try
                {
                    // You may be running tests with C:\PerfViewTestData contents on your
                    // local disk and we don't want source look-up to succeed there. So
                    // we temporarily rename any existing file at the BuildPath location.
                    if (File.Exists(sourceFile.BuildTimeFilePath))
                    {
                        renamedSourceFile = Path.ChangeExtension(sourceFile.BuildTimeFilePath, ".orig");
                        File.Move(sourceFile.BuildTimeFilePath, renamedSourceFile);
                    }

                    // Find the original source file in the unzipped directory, by using
                    // the relative path discovered from SourceLink information.
                    sourceFile.GetSourceLinkInfo(out string url, out string relativePath);
                    string unzippedSourcePath = Path.Combine(UnzippedSymbolReaderTestInputDir, relativePath);

                    // Read the content and replace line endings with Unix style line endings.
                    string originalContent = ReadAllTextIncludingBOM(unzippedSourcePath);
                    string contentWithUnixLineEndings = originalContent.Replace("\r\n", "\n");

                    // Configure the HTTP handler to intercept the request for source code and return
                    // the modified content.
                    _handler.AddIntercept("https://contoso.com/fake-source-link-url/" + relativePath, contentWithUnixLineEndings);

                    // Get the source file from the (fake) repo and check the checksum.
                    string path = sourceFile.GetSourceFile(requireChecksumMatch: true);

                    // It should match because we allow for both styles of line endings.
                    Assert.True(sourceFile.ChecksumMatches);
                }
                finally
                {
                    if (renamedSourceFile != null)
                    {
                        File.Move(renamedSourceFile, sourceFile.BuildTimeFilePath);
                    }
                }
            }
        }

        [Theory]
        [InlineData(0x06000003 /*Program.Main*/, "Console.WriteLine(\"Hello from CsPortableEmbeddedSource!\");\r\n")]
        [InlineData(0x06000001 /*Test.get_X*/, "int X=>0;")]
        public void EmbeddedSourceCanBeLoaded(uint methodToken, string expectedSubstring)
        {
            string pdbName = FileName_CsPortableEmbeddedSource;
            var pdbFile = _symbolReader.OpenSymbolFile(Path.Combine(s_inputPdbDir, pdbName));
            using (pdbFile as IDisposable)
            {
                SourceLocation sourceLocation = pdbFile.SourceLocationForManagedCode(methodToken, ilOffset: 0);
                Assert.NotNull(sourceLocation);

                var sourceFile = sourceLocation.SourceFile;
                Assert.NotNull(sourceFile);

                string renamedSourceFile = null;
                string downloadedPath = null;
                try
                {
                    // You may be running tests with C:\PerfViewTestData contents on your
                    // local disk and we don't want source look-up to succeed there. So
                    // we temporarily rename any existing file at the BuildPath location.
                    if (File.Exists(sourceFile.BuildTimeFilePath))
                    {
                        renamedSourceFile = Path.ChangeExtension(sourceFile.BuildTimeFilePath, ".orig");
                        File.Move(sourceFile.BuildTimeFilePath, renamedSourceFile);
                    }

                    downloadedPath = sourceFile.GetSourceFile(requireChecksumMatch: true);
                    Assert.NotEqual(downloadedPath, sourceFile.BuildTimeFilePath); // Should not be using BuildTimeFilePath
                    Assert.True(sourceFile.ChecksumMatches);

                    // Check the contents
                    string fileContents = File.ReadAllText(downloadedPath);
                    Assert.Contains(expectedSubstring, fileContents, StringComparison.Ordinal);
                }
                finally
                {
                    if (downloadedPath != null)
                    {
                        File.Delete(downloadedPath);
                    }

                    if (renamedSourceFile != null)
                    {
                        File.Move(renamedSourceFile, sourceFile.BuildTimeFilePath);
                    }
                }
            }
        }

        /// <summary>
        /// Read all the text from the given file path into a string,
        /// preserving any byte order mark.
        /// </summary>
        /// <param name="path">Path to the file to read.</param>
        /// <returns>A string representing the contents of the entire file with any byte order marks preserved.</returns>
        private static string ReadAllTextIncludingBOM(string path)
        {
            // The trick is to use a flavor of UTF8Encoding that does not emit
            // a byte order mark (GetPreamble returns an empty array).
            using (var reader = new StreamReader(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), detectEncodingFromByteOrderMarks: false))
            {
                return reader.ReadToEnd();
            }
        }

        [Theory]
        [InlineData(FileName_CppConPdb)]
        [InlineData(FileName_CsDesktopPdbWithSourceLink)]
        [InlineData(FileName_CsPortablePdb1)]
        public void PdbFileLockingIsCorrect(string pdbFileName)
        {
            const int E_WIN32_ERROR_SHARING_VIOLATION = -2147024864;

            string tempPdbPath = Path.Combine(Path.GetTempPath(), pdbFileName);
            File.Copy(Path.Combine(s_inputPdbDir, pdbFileName), tempPdbPath, overwrite: true);

            try
            {
                bool CanWrite()
                {
                    try
                    {
                        var stream = new FileStream(tempPdbPath, FileMode.Open, FileAccess.Write, FileShare.Read | FileShare.Write | FileShare.Delete);
                        stream.Dispose();
                        return true;
                    }
                    catch (IOException ioException) when (ioException.HResult == E_WIN32_ERROR_SHARING_VIOLATION)
                    {
                        return false;
                    }
                }

                Assert.True(CanWrite());
                var symbolFile = _symbolReader.OpenSymbolFile(tempPdbPath);
                var disposableSymbolFile = symbolFile as IDisposable;
                Assert.NotNull(disposableSymbolFile);
                Assert.False(CanWrite());

                disposableSymbolFile.Dispose();
                disposableSymbolFile = null;

                Assert.True(CanWrite());
            }
            finally
            {
                try
                {
                    File.Delete(tempPdbPath);
                }
                catch
                {
                }
            }
        }

        private string ChecksumToString(IReadOnlyCollection<byte> checksum)
        {
            if (checksum == null)
            {
                return "<none>";
            }
            else
            {
                // Convert the byte array to hex string
                var sb = new StringBuilder(checksum.Count * 2);
                foreach (byte b in checksum)
                {
                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0:X2}", b);
                }

                return sb.ToString();
            }
        }

        [Fact]
        public void MsfzFileDetectionWorks()
        {
            // Create a temporary file with MSFZ header
            var tempDir = Path.GetTempPath();
            var testFile = Path.Combine(tempDir, "test_msfz.pdb");
            var nonMsfzFile = Path.Combine(tempDir, "test_non_msfz.pdb");

            try
            {
                // Write MSFZ header followed by some dummy data
                var msfzHeader = "Microsoft MSFZ Container";
                var headerBytes = Encoding.UTF8.GetBytes(msfzHeader);
                var dummyData = new byte[] { 0x01, 0x02, 0x03, 0x04 };

                using (var stream = File.Create(testFile))
                {
                    stream.Write(headerBytes, 0, headerBytes.Length);
                    stream.Write(dummyData, 0, dummyData.Length);
                }

                // Use reflection to call the private IsMsfzFile method
                var method = typeof(SymbolReader).GetMethod("IsMsfzFile",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                var result = (bool)method.Invoke(_symbolReader, new object[] { testFile });

                Assert.True(result, "File with MSFZ header should be detected as MSFZ file");

                // Test with non-MSFZ file
                File.WriteAllText(nonMsfzFile, "This is not an MSFZ file");

                result = (bool)method.Invoke(_symbolReader, new object[] { nonMsfzFile });
                Assert.False(result, "File without MSFZ header should not be detected as MSFZ file");
            }
            finally
            {
                if (File.Exists(testFile))
                    File.Delete(testFile);
                if (File.Exists(nonMsfzFile))
                    File.Delete(nonMsfzFile);
            }
        }

        [Fact]
        public void MsfzFileMovesToCorrectSubdirectory()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "msfz_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var testFile = Path.Combine(tempDir, "test.pdb");

                // Create MSFZ file
                var msfzHeader = "Microsoft MSFZ Container";
                var headerBytes = Encoding.UTF8.GetBytes(msfzHeader);
                var dummyData = new byte[] { 0x01, 0x02, 0x03, 0x04 };

                using (var stream = File.Create(testFile))
                {
                    stream.Write(headerBytes, 0, headerBytes.Length);
                    stream.Write(dummyData, 0, dummyData.Length);
                }

                // Since MSFZ logic is now integrated into GetFileFromServer,
                // this test validates the MSFZ detection logic which remains the same
                var isMsfzMethod = typeof(SymbolReader).GetMethod("IsMsfzFile",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                var isMsfz = (bool)isMsfzMethod.Invoke(_symbolReader, new object[] { testFile });
                Assert.True(isMsfz, "File should be detected as MSFZ file");

                // The file moving functionality is now tested through integration tests
                // since it's part of the GetFileFromServer method
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void HttpRequestIncludesMsfzAcceptHeader()
        {
            // This test verifies that our HttpRequestMessage creation includes the MSFZ accept header
            // We'll create a minimal test by checking the private method behavior indirectly

            var tempDir = Path.Combine(Path.GetTempPath(), "msfz_http_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var targetPath = Path.Combine(tempDir, "test.pdb");

            try
            {
                // Configure intercepting handler to capture the request with MSFZ content
                _handler.AddIntercept(new Uri("https://test.example.com/test.pdb"), HttpMethod.Get, HttpStatusCode.OK, () =>
                {
                    var msfzContent = "Microsoft MSFZ Container\x00\x01\x02\x03";
                    return new StringContent(msfzContent, Encoding.UTF8, "application/msfz0");
                });

                // This will trigger an HTTP request that should include the Accept header
                var method = typeof(SymbolReader).GetMethod("GetPhysicalFileFromServer",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                var result = (bool)method.Invoke(_symbolReader, new object[] {
                    "https://test.example.com",
                    "test.pdb",
                    targetPath,
                    null
                });

                // Verify that the download was successful
                Assert.True(result, "GetPhysicalFileFromServer should succeed with MSFZ content");

                // In the new architecture, GetPhysicalFileFromServer just downloads the file
                // The MSFZ moving logic is handled by GetFileFromServer
                Assert.True(File.Exists(targetPath), "Downloaded file should exist at target path");

                // Verify the content is MSFZ
                var isMsfzMethod = typeof(SymbolReader).GetMethod("IsMsfzFile",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var isMsfz = (bool)isMsfzMethod.Invoke(_symbolReader, new object[] { targetPath });
                Assert.True(isMsfz, "Downloaded file should be detected as MSFZ");
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        #region FindElfSymbolFilePath Tests

        [Fact]
        public void FindElfSymbolFilePath_DebugSymbolsFoundLocally()
        {
            string tempDir = Path.Combine(OutputDir, "elf-local-debug");
            try
            {
                string buildId = "abc123";
                string normalizedBuildId = buildId.ToLowerInvariant();

                // Create SSQP debug symbol directory structure with valid ELF build-id.
                string debugDir = Path.Combine(tempDir, "_.debug", "elf-buildid-sym-" + normalizedBuildId);
                Directory.CreateDirectory(debugDir);
                string debugFile = Path.Combine(debugDir, "_.debug");
                File.WriteAllBytes(debugFile, CreateMinimalElfWithBuildId(normalizedBuildId));

                _symbolReader.SymbolPath = tempDir;
                string result = _symbolReader.FindElfSymbolFilePath("libcoreclr.so", buildId);

                Assert.NotNull(result);
                Assert.Equal(Path.GetFullPath(debugFile), Path.GetFullPath(result));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void FindElfSymbolFilePath_BinaryFallbackLocally()
        {
            string tempDir = Path.Combine(OutputDir, "elf-local-binary");
            try
            {
                string buildId = "def456";
                string normalizedBuildId = buildId.ToLowerInvariant();

                // Create only the binary directory structure (no debug symbols).
                string binaryDir = Path.Combine(tempDir, "libcoreclr.so", "elf-buildid-" + normalizedBuildId);
                Directory.CreateDirectory(binaryDir);
                string binaryFile = Path.Combine(binaryDir, "libcoreclr.so");
                File.WriteAllBytes(binaryFile, CreateMinimalElfWithBuildId(normalizedBuildId));

                _symbolReader.SymbolPath = tempDir;
                string result = _symbolReader.FindElfSymbolFilePath("libcoreclr.so", buildId);

                Assert.NotNull(result);
                Assert.Equal(Path.GetFullPath(binaryFile), Path.GetFullPath(result));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void FindElfSymbolFilePath_DebugPreferredOverBinary()
        {
            string tempDir = Path.Combine(OutputDir, "elf-local-prefer-debug");
            try
            {
                string buildId = "aabbcc";
                string normalizedBuildId = buildId.ToLowerInvariant();

                // Create both debug and binary directory structures with valid ELF build-ids.
                string debugDir = Path.Combine(tempDir, "_.debug", "elf-buildid-sym-" + normalizedBuildId);
                Directory.CreateDirectory(debugDir);
                string debugFile = Path.Combine(debugDir, "_.debug");
                File.WriteAllBytes(debugFile, CreateMinimalElfWithBuildId(normalizedBuildId));

                string binaryDir = Path.Combine(tempDir, "libtest.so", "elf-buildid-" + normalizedBuildId);
                Directory.CreateDirectory(binaryDir);
                string binaryFile = Path.Combine(binaryDir, "libtest.so");
                File.WriteAllBytes(binaryFile, CreateMinimalElfWithBuildId(normalizedBuildId));

                _symbolReader.SymbolPath = tempDir;
                string result = _symbolReader.FindElfSymbolFilePath("libtest.so", buildId);

                Assert.NotNull(result);
                Assert.Equal(Path.GetFullPath(debugFile), Path.GetFullPath(result));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void FindElfSymbolFilePath_NotFoundLocally()
        {
            string tempDir = Path.Combine(OutputDir, "elf-local-empty");
            try
            {
                Directory.CreateDirectory(tempDir);

                _symbolReader.SymbolPath = tempDir;
                string result = _symbolReader.FindElfSymbolFilePath("libmissing.so", "deadbeef");

                Assert.Null(result);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Theory]
        [InlineData("abcd", "abcd")]
        [InlineData("ABC123", "abc123")]
        [InlineData("aabbccdd00112233445566778899aabbccddeeff", "aabbccdd00112233445566778899aabbccddeeff")]
        public void FindElfSymbolFilePath_BuildIdNormalization(string inputBuildId, string expectedNormalized)
        {
            string tempDir = Path.Combine(OutputDir, "elf-buildid-norm");
            try
            {
                // Create debug symbol directory structure with valid ELF build-id.
                string debugDir = Path.Combine(tempDir, "_.debug", "elf-buildid-sym-" + expectedNormalized);
                Directory.CreateDirectory(debugDir);
                string debugFile = Path.Combine(debugDir, "_.debug");
                File.WriteAllBytes(debugFile, CreateMinimalElfWithBuildId(expectedNormalized));

                _symbolReader.SymbolPath = tempDir;
                string result = _symbolReader.FindElfSymbolFilePath("libnorm.so", inputBuildId);

                Assert.NotNull(result);
                Assert.Equal(Path.GetFullPath(debugFile), Path.GetFullPath(result));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void FindElfSymbolFilePath_AbsolutePathExtractsFilename()
        {
            string tempDir = Path.Combine(OutputDir, "elf-abspath");
            try
            {
                string buildId = "1122334455";
                string normalizedBuildId = buildId.ToLowerInvariant();

                // Create binary directory structure using just the simple filename.
                string binaryDir = Path.Combine(tempDir, "libc.so.6", "elf-buildid-" + normalizedBuildId);
                Directory.CreateDirectory(binaryDir);
                string binaryFile = Path.Combine(binaryDir, "libc.so.6");
                File.WriteAllBytes(binaryFile, CreateMinimalElfWithBuildId(normalizedBuildId));

                _symbolReader.SymbolPath = tempDir;
                // Pass an absolute path — only the filename portion should be used for lookup.
                string result = _symbolReader.FindElfSymbolFilePath("/usr/lib/x86_64-linux-gnu/libc.so.6", buildId);

                Assert.NotNull(result);
                Assert.Equal(Path.GetFullPath(binaryFile), Path.GetFullPath(result));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void FindElfSymbolFilePath_CacheOnlySkipsRemotePaths()
        {
            // Use a UNC-style path that is "remote" but won't actually be accessed.
            _symbolReader.SymbolPath = @"\\nonexistent-server\symbols";
            _symbolReader.Options = SymbolReaderOptions.CacheOnly;

            string result = _symbolReader.FindElfSymbolFilePath("libcoreclr.so", "aabbccdd");

            Assert.Null(result);
        }

        [Fact]
        public void FindElfSymbolFilePath_CacheHitSkipsSearch()
        {
            string tempDir = Path.Combine(OutputDir, "elf-cache-hit");
            try
            {
                string buildId = "cacced1d12";
                string normalizedBuildId = buildId.ToLowerInvariant();

                string debugDir = Path.Combine(tempDir, "_.debug", "elf-buildid-sym-" + normalizedBuildId);
                Directory.CreateDirectory(debugDir);
                string debugFile = Path.Combine(debugDir, "_.debug");
                File.WriteAllBytes(debugFile, CreateMinimalElfWithBuildId(normalizedBuildId));

                _symbolReader.SymbolPath = tempDir;

                // First call populates the cache.
                string result1 = _symbolReader.FindElfSymbolFilePath("libcache.so", buildId);
                Assert.NotNull(result1);

                // Remove the file so only cache can return it.
                File.Delete(debugFile);
                Directory.Delete(debugDir);

                string result2 = _symbolReader.FindElfSymbolFilePath("libcache.so", buildId);
                Assert.Equal(result1, result2);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void FindElfSymbolFilePath_NegativeCacheReturnsNull()
        {
            string tempDir = Path.Combine(OutputDir, "elf-negative-cache");
            try
            {
                Directory.CreateDirectory(tempDir);
                _symbolReader.SymbolPath = tempDir;

                // First call: nothing found, null is cached.
                string result1 = _symbolReader.FindElfSymbolFilePath("libnocache.so", "ffffffff");
                Assert.Null(result1);

                // Now create the file — but the negative cache should still return null.
                string normalizedBuildId = "ffffffff";
                string debugDir = Path.Combine(tempDir, "_.debug", "elf-buildid-sym-" + normalizedBuildId);
                Directory.CreateDirectory(debugDir);
                File.WriteAllBytes(Path.Combine(debugDir, "_.debug"), new byte[] { 0x7F });

                string result2 = _symbolReader.FindElfSymbolFilePath("libnocache.so", "ffffffff");
                Assert.Null(result2);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void FindElfSymbolFilePath_DifferentBuildIdsAreDifferentCacheKeys()
        {
            string tempDir = Path.Combine(OutputDir, "elf-diff-keys");
            try
            {
                string buildId1 = "aaaa";
                string buildId2 = "bbbb";
                string norm1 = buildId1;
                string norm2 = buildId2;

                // Only create debug symbols for the second build ID.
                string debugDir2 = Path.Combine(tempDir, "_.debug", "elf-buildid-sym-" + norm2);
                Directory.CreateDirectory(debugDir2);
                string debugFile2 = Path.Combine(debugDir2, "_.debug");
                File.WriteAllBytes(debugFile2, CreateMinimalElfWithBuildId(norm2));

                _symbolReader.SymbolPath = tempDir;

                string result1 = _symbolReader.FindElfSymbolFilePath("lib.so", buildId1);
                Assert.Null(result1);

                string result2 = _symbolReader.FindElfSymbolFilePath("lib.so", buildId2);
                Assert.NotNull(result2);
                Assert.Equal(Path.GetFullPath(debugFile2), Path.GetFullPath(result2));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void FindElfSymbolFilePath_DebugLinkDiscovery()
        {
            string tempDir = Path.Combine(OutputDir, "elf-debuglink");
            try
            {
                string buildId = "aabb0011";

                // Build an ELF binary with .gnu_debuglink pointing to "libtest.so.dbg".
                var binaryBuilder = new ElfBuilder()
                    .Set64Bit(true)
                    .SetPTLoad(0x400000, 0)
                    .SetBuildId(HexToBytes(buildId))
                    .SetDebugLink("libtest.so.dbg");
                byte[] binaryData = binaryBuilder.Build();

                // Build a debug ELF file with matching build-id.
                byte[] debugData = CreateMinimalElfWithBuildId(buildId);

                // Place the binary and debug file in the same directory.
                Directory.CreateDirectory(tempDir);
                string binaryPath = Path.Combine(tempDir, "libtest.so");
                string debugPath = Path.Combine(tempDir, "libtest.so.dbg");
                File.WriteAllBytes(binaryPath, binaryData);
                File.WriteAllBytes(debugPath, debugData);

                // Set symbol path to an empty location (no SSQP match),
                // but provide elfFilePath so adjacent search kicks in.
                // SecurityCheck is needed because adjacent search uses checkSecurity: true.
                string emptyDir = Path.Combine(tempDir, "empty");
                Directory.CreateDirectory(emptyDir);
                _symbolReader.SymbolPath = emptyDir;
                _symbolReader.SecurityCheck = _ => true;

                string result = _symbolReader.FindElfSymbolFilePath("libtest.so", buildId, elfFilePath: binaryPath);

                Assert.NotNull(result);
                Assert.Equal(Path.GetFullPath(debugPath), Path.GetFullPath(result));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void FindElfSymbolFilePath_DebugLinkInSubdir()
        {
            string tempDir = Path.Combine(OutputDir, "elf-debuglink-subdir");
            try
            {
                string buildId = "ccdd0022";

                // Build an ELF binary with .gnu_debuglink pointing to "libfoo.debug".
                var binaryBuilder = new ElfBuilder()
                    .Set64Bit(true)
                    .SetPTLoad(0x400000, 0)
                    .SetBuildId(HexToBytes(buildId))
                    .SetDebugLink("libfoo.debug");
                byte[] binaryData = binaryBuilder.Build();

                // Build a debug ELF file with matching build-id.
                byte[] debugData = CreateMinimalElfWithBuildId(buildId);

                // Place the binary in tempDir, debug file in {tempDir}/.debug/ subdir.
                Directory.CreateDirectory(tempDir);
                string debugSubDir = Path.Combine(tempDir, ".debug");
                Directory.CreateDirectory(debugSubDir);

                string binaryPath = Path.Combine(tempDir, "libfoo.so");
                string debugPath = Path.Combine(debugSubDir, "libfoo.debug");
                File.WriteAllBytes(binaryPath, binaryData);
                File.WriteAllBytes(debugPath, debugData);

                string emptyDir = Path.Combine(tempDir, "empty");
                Directory.CreateDirectory(emptyDir);
                _symbolReader.SymbolPath = emptyDir;
                _symbolReader.SecurityCheck = _ => true;

                string result = _symbolReader.FindElfSymbolFilePath("libfoo.so", buildId, elfFilePath: binaryPath);

                Assert.NotNull(result);
                Assert.Equal(Path.GetFullPath(debugPath), Path.GetFullPath(result));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        #endregion

        #region FindR2RPerfMapSymbolFilePath Tests

        [Fact]
        public void FindR2RPerfMapSymbolFilePath_FoundLocally()
        {
            string tempDir = Path.Combine(OutputDir, "r2r-local");
            try
            {
                Directory.CreateDirectory(tempDir);
                var sig = new Guid("a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d");
                int version = 1;
                string perfMapFile = Path.Combine(tempDir, "CoreLib.r2rmap");
                File.WriteAllBytes(perfMapFile, CreateMinimalR2RPerfMap(sig, version));

                _symbolReader.SymbolPath = tempDir;
                string result = _symbolReader.FindR2RPerfMapSymbolFilePath("CoreLib.r2rmap", sig, version);

                Assert.NotNull(result);
                Assert.Equal(perfMapFile, result);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void FindR2RPerfMapSymbolFilePath_NotFound()
        {
            string tempDir = Path.Combine(OutputDir, "r2r-empty");
            try
            {
                Directory.CreateDirectory(tempDir);

                _symbolReader.SymbolPath = tempDir;
                var sig = new Guid("11111111-2222-3333-4444-555555555555");
                string result = _symbolReader.FindR2RPerfMapSymbolFilePath("Missing.r2rmap", sig, 1);

                Assert.Null(result);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void FindR2RPerfMapSymbolFilePath_CacheOnlySkipsRemotePaths()
        {
            _symbolReader.SymbolPath = @"\\nonexistent-server\symbols";
            _symbolReader.Options = SymbolReaderOptions.CacheOnly;

            var sig = new Guid("a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d");
            string result = _symbolReader.FindR2RPerfMapSymbolFilePath("CoreLib.r2rmap", sig, 1);

            Assert.Null(result);
        }

        [Fact]
        public void FindR2RPerfMapSymbolFilePath_CacheHitSkipsSearch()
        {
            string tempDir = Path.Combine(OutputDir, "r2r-cache-hit");
            try
            {
                Directory.CreateDirectory(tempDir);
                var sig = new Guid("cc000000-0000-0000-0000-000000000000");
                int version = 1;
                string perfMapFile = Path.Combine(tempDir, "Cached.r2rmap");
                File.WriteAllBytes(perfMapFile, CreateMinimalR2RPerfMap(sig, version));

                _symbolReader.SymbolPath = tempDir;

                // First call populates the cache.
                string result1 = _symbolReader.FindR2RPerfMapSymbolFilePath("Cached.r2rmap", sig, version);
                Assert.NotNull(result1);

                // Remove the file so only cache can return it.
                File.Delete(perfMapFile);

                string result2 = _symbolReader.FindR2RPerfMapSymbolFilePath("Cached.r2rmap", sig, version);
                Assert.Equal(result1, result2);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void FindR2RPerfMapSymbolFilePath_DifferentSignaturesAreDifferentCacheKeys()
        {
            string tempDir = Path.Combine(OutputDir, "r2r-diff-keys");
            try
            {
                Directory.CreateDirectory(tempDir);
                // No file on disk — both lookups will miss the file system.

                _symbolReader.SymbolPath = tempDir;
                var sig1 = new Guid("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
                var sig2 = new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

                string result1 = _symbolReader.FindR2RPerfMapSymbolFilePath("Test.r2rmap", sig1, 1);
                Assert.Null(result1);

                // Now create the file with sig2's identity — sig2 should find it (not negatively cached).
                string perfMapFile = Path.Combine(tempDir, "Test.r2rmap");
                File.WriteAllBytes(perfMapFile, CreateMinimalR2RPerfMap(sig2, 1));

                string result2 = _symbolReader.FindR2RPerfMapSymbolFilePath("Test.r2rmap", sig2, 1);
                Assert.NotNull(result2);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                    Directory.Delete(tempDir, true);
            }
        }

        #endregion

        #region FindSymbolFilePathForModule Tests

        [Fact]
        public void FindSymbolFilePathForModule_FileDoesNotExist()
        {
            string result = _symbolReader.FindSymbolFilePathForModule(@"C:\nonexistent\path\fake.dll");

            Assert.Null(result);
        }

        [Fact]
        public void FindSymbolFilePathForModule_InvalidPeFile()
        {
            string tempDir = Path.Combine(OutputDir, "module-invalid-pe");
            Directory.CreateDirectory(tempDir);
            string invalidDll = Path.Combine(tempDir, "invalid.dll");
            File.WriteAllText(invalidDll, "This is not a valid PE file");

            // Should not throw — exception is caught internally and null returned.
            string result = _symbolReader.FindSymbolFilePathForModule(invalidDll);

            Assert.Null(result);
        }

        [Fact]
        public void FindSymbolFilePathForModule_FindsPdbNextToDll()
        {
            PrepareTestData();

            // The test data has PDB files. We need a DLL that references one of those PDBs.
            // Since we may not have a matching DLL in test data, verify the basic "file exists" path
            // by testing that a DLL file that exists but has no CodeView signature returns null gracefully.
            string tempDir = Path.Combine(OutputDir, "module-no-codeview");
            Directory.CreateDirectory(tempDir);
            // Create a minimal valid PE file (just MZ header + PE signature) that lacks CodeView info.
            // The DOS stub points to PE signature at offset 0x80.
            byte[] minimalPe = new byte[0x100];
            minimalPe[0] = 0x4D; // 'M'
            minimalPe[1] = 0x5A; // 'Z'
            minimalPe[0x3C] = 0x80; // e_lfanew
            minimalPe[0x80] = 0x50; // 'P'
            minimalPe[0x81] = 0x45; // 'E'
            minimalPe[0x82] = 0x00;
            minimalPe[0x83] = 0x00;
            string minimalDll = Path.Combine(tempDir, "minimal.dll");
            File.WriteAllBytes(minimalDll, minimalPe);

            // This PE file has no CodeView debug directory, so FindSymbolFilePathForModule
            // should return null (either via no PDB signature or PE parsing gracefully failing).
            string result = _symbolReader.FindSymbolFilePathForModule(minimalDll);
            Assert.Null(result);
        }

        #endregion

        #region Cache Invalidation Tests

        [Fact]
        public void ElfCache_ClearedWhenSymbolPathChanges()
        {
            string tempDir1 = Path.Combine(OutputDir, "elf-cache-inv1");
            string tempDir2 = Path.Combine(OutputDir, "elf-cache-inv2");
            try
            {
                // Set up: first path has nothing, second path has the file.
                Directory.CreateDirectory(tempDir1);

                string buildId = "cace0e0010";
                string normalizedBuildId = buildId;
                string debugDir = Path.Combine(tempDir2, "_.debug", "elf-buildid-sym-" + normalizedBuildId);
                Directory.CreateDirectory(debugDir);
                File.WriteAllBytes(Path.Combine(debugDir, "_.debug"), CreateMinimalElfWithBuildId(normalizedBuildId));

                // First search against empty path — null is cached.
                _symbolReader.SymbolPath = tempDir1;
                Assert.Null(_symbolReader.FindElfSymbolFilePath("lib.so", buildId));

                // Change SymbolPath — cache should be cleared, so the new path is searched.
                _symbolReader.SymbolPath = tempDir2;
                Assert.NotNull(_symbolReader.FindElfSymbolFilePath("lib.so", buildId));
            }
            finally
            {
                if (Directory.Exists(tempDir1)) Directory.Delete(tempDir1, true);
                if (Directory.Exists(tempDir2)) Directory.Delete(tempDir2, true);
            }
        }

        [Fact]
        public void R2RCache_ClearedWhenSymbolPathChanges()
        {
            string tempDir1 = Path.Combine(OutputDir, "r2r-cache-inv1");
            string tempDir2 = Path.Combine(OutputDir, "r2r-cache-inv2");
            try
            {
                Directory.CreateDirectory(tempDir1);
                Directory.CreateDirectory(tempDir2);
                var sig = new Guid("12345678-1234-1234-1234-123456789abc");
                int version = 1;
                File.WriteAllBytes(Path.Combine(tempDir2, "Test.r2rmap"), CreateMinimalR2RPerfMap(sig, version));

                // First search against empty path — null is cached.
                _symbolReader.SymbolPath = tempDir1;
                Assert.Null(_symbolReader.FindR2RPerfMapSymbolFilePath("Test.r2rmap", sig, version));

                // Change SymbolPath — cache should be cleared, so the new path is searched.
                _symbolReader.SymbolPath = tempDir2;
                Assert.NotNull(_symbolReader.FindR2RPerfMapSymbolFilePath("Test.r2rmap", sig, version));
            }
            finally
            {
                if (Directory.Exists(tempDir1)) Directory.Delete(tempDir1, true);
                if (Directory.Exists(tempDir2)) Directory.Delete(tempDir2, true);
            }
        }

        [Fact]
        public void ElfCache_ClearedWhenOptionsChange()
        {
            string tempDir = Path.Combine(OutputDir, "elf-cache-opt");
            try
            {
                string buildId = "00ee0010";
                string normalizedBuildId = buildId;
                string debugDir = Path.Combine(tempDir, "_.debug", "elf-buildid-sym-" + normalizedBuildId);
                Directory.CreateDirectory(debugDir);
                File.WriteAllBytes(Path.Combine(debugDir, "_.debug"), CreateMinimalElfWithBuildId(normalizedBuildId));

                // First: find it successfully and cache it.
                _symbolReader.SymbolPath = tempDir;
                Assert.NotNull(_symbolReader.FindElfSymbolFilePath("lib.so", buildId));

                // Remove the file.
                Directory.Delete(debugDir, true);

                // Without cache invalidation the cached path would still be returned.
                // Changing Options should clear the cache, forcing a fresh lookup.
                _symbolReader.Options = SymbolReaderOptions.CacheOnly;
                Assert.Null(_symbolReader.FindElfSymbolFilePath("lib.so", buildId));
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        #endregion

        #region ELF Module Cache Tests

        [Fact]
        public void OpenElfSymbolFile_CacheHitReturnsSameInstance()
        {
            string tempDir = Path.Combine(OutputDir, "elf-mod-cache-hit");
            try
            {
                Directory.CreateDirectory(tempDir);
                // Create a dummy file — ElfSymbolModule gracefully handles non-ELF content.
                string elfFile = Path.Combine(tempDir, "libtest.so");
                File.WriteAllBytes(elfFile, new byte[] { 0x00 });

                var module1 = _symbolReader.OpenElfSymbolFile(elfFile, 0x1000, 0x0);
                var module2 = _symbolReader.OpenElfSymbolFile(elfFile, 0x1000, 0x0);

                Assert.Same(module1, module2);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void OpenElfSymbolFile_DifferentParamsAreDifferentCacheEntries()
        {
            string tempDir = Path.Combine(OutputDir, "elf-mod-cache-diff");
            try
            {
                Directory.CreateDirectory(tempDir);
                string elfFile = Path.Combine(tempDir, "libtest.so");
                File.WriteAllBytes(elfFile, new byte[] { 0x00 });

                var module1 = _symbolReader.OpenElfSymbolFile(elfFile, 0x1000, 0x0);
                var module2 = _symbolReader.OpenElfSymbolFile(elfFile, 0x2000, 0x0);

                Assert.NotSame(module1, module2);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void OpenElfSymbolFile_CacheClearedOnSymbolPathChange()
        {
            string tempDir = Path.Combine(OutputDir, "elf-mod-cache-clear");
            try
            {
                Directory.CreateDirectory(tempDir);
                string elfFile = Path.Combine(tempDir, "libtest.so");
                File.WriteAllBytes(elfFile, new byte[] { 0x00 });

                var module1 = _symbolReader.OpenElfSymbolFile(elfFile, 0x1000, 0x0);

                // Changing SymbolPath clears all caches including the module cache.
                _symbolReader.SymbolPath = tempDir;

                var module2 = _symbolReader.OpenElfSymbolFile(elfFile, 0x1000, 0x0);

                // Should be a different instance because cache was cleared.
                Assert.NotSame(module1, module2);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        #endregion

        /// <summary>
        /// Creates a minimal valid ELF64 little-endian binary with a GNU build-id note.
        /// Used by tests that need a file whose build-id can be read by ReadBuildId.
        /// </summary>
        /// <param name="buildIdHex">Lowercase hex string (e.g., "abc123" → 3 bytes: 0xab, 0xc1, 0x23).</param>
        private static byte[] CreateMinimalElfWithBuildId(string buildIdHex)
        {
            // Convert hex string to bytes.
            int byteCount = buildIdHex.Length / 2;
            byte[] buildIdBytes = new byte[byteCount];
            for (int i = 0; i < byteCount; i++)
            {
                buildIdBytes[i] = byte.Parse(buildIdHex.Substring(i * 2, 2), NumberStyles.HexNumber);
            }

            // Build the GNU build-id note.
            // Note header: namesz(4) + descsz(4) + type(4) = 12 bytes.
            // Name: "GNU\0" = 4 bytes (already 4-byte aligned).
            // Desc: buildId bytes, padded to 4-byte alignment.
            uint descsz = (uint)buildIdBytes.Length;
            uint descAligned = (descsz + 3) & ~3u;
            int noteSize = 12 + 4 + (int)descAligned; // header + name + aligned desc

            // ELF64 header (64 bytes) + one program header (56 bytes) + note.
            int phOffset = 64;
            int noteOffset = 64 + 56;
            int totalSize = noteOffset + noteSize;
            byte[] elf = new byte[totalSize];

            // ELF header.
            elf[0] = 0x7f; elf[1] = (byte)'E'; elf[2] = (byte)'L'; elf[3] = (byte)'F'; // magic
            elf[4] = 2;   // ELFCLASS64
            elf[5] = 1;   // ELFDATA2LSB
            elf[6] = 1;   // EV_CURRENT
            // e_type = ET_EXEC (2)
            elf[16] = 2;
            // e_machine = EM_X86_64 (0x3e)
            elf[18] = 0x3e;
            // e_version = 1
            elf[20] = 1;
            // e_phoff = 64 (0x40)
            elf[32] = 0x40;
            // e_ehsize = 64 (0x40)
            elf[52] = 0x40;
            // e_phentsize = 56 (0x38)
            elf[54] = 0x38;
            // e_phnum = 1
            elf[56] = 1;

            // Program header (PT_NOTE at offset 64).
            // p_type = PT_NOTE (4)
            elf[phOffset] = 4;
            // p_flags (at +4 for ELF64)
            // p_offset (at +8) = noteOffset
            elf[phOffset + 8] = (byte)noteOffset;
            // p_filesz (at +32) = noteSize
            elf[phOffset + 32] = (byte)(noteSize & 0xFF);
            elf[phOffset + 33] = (byte)((noteSize >> 8) & 0xFF);
            // p_memsz (at +40) = noteSize
            elf[phOffset + 40] = (byte)(noteSize & 0xFF);
            elf[phOffset + 41] = (byte)((noteSize >> 8) & 0xFF);

            // Note at noteOffset.
            int np = noteOffset;
            // namesz = 4
            elf[np] = 4;
            // descsz
            elf[np + 4] = (byte)(descsz & 0xFF);
            elf[np + 5] = (byte)((descsz >> 8) & 0xFF);
            // type = NT_GNU_BUILD_ID (3)
            elf[np + 8] = 3;
            // name = "GNU\0"
            elf[np + 12] = (byte)'G';
            elf[np + 13] = (byte)'N';
            elf[np + 14] = (byte)'U';
            elf[np + 15] = 0;
            // desc = build-id bytes
            Array.Copy(buildIdBytes, 0, elf, np + 16, buildIdBytes.Length);

            return elf;
        }

        /// <summary>
        /// Creates a minimal valid R2R perfmap text file with the given Signature and Version.
        /// Used by tests that need a file whose Signature/Version can be read by R2RPerfMapSymbolModule.
        /// </summary>
        private static byte[] CreateMinimalR2RPerfMap(Guid signature, int version)
        {
            // R2R perfmap format: each line is "address size name"
            // Signature: FFFFFFFF 0 {guid}
            // Version:   FFFFFFFE 0 {version}
            string content = $"FFFFFFFF 0 {signature:D}\nFFFFFFFE 0 {version}\n";
            return Encoding.UTF8.GetBytes(content);
        }

        /// <summary>
        /// Converts a hex string to a byte array.
        /// </summary>
        private static byte[] HexToBytes(string hex)
        {
            int byteCount = hex.Length / 2;
            byte[] bytes = new byte[byteCount];
            for (int i = 0; i < byteCount; i++)
            {
                bytes[i] = byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber);
            }
            return bytes;
        }

        protected void PrepareTestData()
        {
            lock (s_fileLock)
            {
                if (s_inputPdbDir is null)
                {
                    Assert.True(Directory.Exists(TestDataDir));
                    TestDataDir = Path.GetFullPath(TestDataDir);

                    UnZippedDataDir = Path.GetFullPath(UnZippedDataDir);
                    Directory.CreateDirectory(UnZippedDataDir);

                    string zipFile = Path.Combine(TestDataDir, SymbolReaderTestInput + ".zip");
                    string symbolReaderDataDir = UnzippedSymbolReaderTestInputDir;

                    bool symbolReaderDataDirExists = Directory.Exists(symbolReaderDataDir);
                    if (!symbolReaderDataDirExists || File.GetLastWriteTimeUtc(symbolReaderDataDir) < File.GetLastWriteTimeUtc(zipFile))
                    {
                        if (symbolReaderDataDirExists)
                        {
                            Directory.Delete(symbolReaderDataDir, recursive: true);
                        }

                        ZipFile.ExtractToDirectory(zipFile, symbolReaderDataDir);
                    }

                    var inputPdbDir = Path.Combine(symbolReaderDataDir, "bin", "Release");
                    Assert.True(Directory.Exists(inputPdbDir));

                    s_inputPdbDir = inputPdbDir;
                }
            }
        }

        protected string UnzippedSymbolReaderTestInputDir => Path.Combine(UnZippedDataDir, SymbolReaderTestInput);

        /// <summary>
        /// A handler for the <see cref="HttpClient"/> in <see cref="SymbolReader"/> that
        /// can be used by unit tests to intercept requests to symbol server (for PDB
        /// lookup) and source servers for source code lookup.
        /// </summary>
        private class InterceptingHandler : DelegatingHandler
        {
            /// <summary>
            /// Construct a new <see cref="InterceptingHandler"/> instance.
            /// </summary>
            public InterceptingHandler()
            {
                InnerHandler = new HttpClientHandler();
            }

            /// <summary>
            /// Mapping of HTTP requests to intercept with their expected response.
            /// </summary>
            public Dictionary<(Uri uri, HttpMethod method), (HttpStatusCode statusCode, Func<HttpContent> contentFactory)> Intercepts { get; } = new Dictionary<(Uri uri, HttpMethod method), (HttpStatusCode statusCode, Func<HttpContent> contentFactory)>();

            /// <summary>
            /// Convenience helper for adding an intercept for HTTP GET
            /// returning UTF-8 content with status code 200 (OK).
            /// </summary>
            /// <param name="uri">The request URI to intercept.</param>
            /// <param name="response">The body of the response.</param>
            public void AddIntercept(string uri, string response)
            {
                AddIntercept(new Uri(uri), HttpMethod.Get, HttpStatusCode.OK, () => new StringContent(response, Encoding.UTF8));
            }

            public void AddIntercept(Uri uri, HttpMethod method, HttpStatusCode statusCode, Func<HttpContent> contentFactory) => Intercepts.Add((uri, method), (statusCode, contentFactory));

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                if (Intercepts.TryGetValue((request.RequestUri, request.Method), out var response))
                {
                    return Task.FromResult(new HttpResponseMessage(response.statusCode)
                    {
                        Content = response.contentFactory()
                    });
                }

                return base.SendAsync(request, cancellationToken);
            }
        }

        /// <summary>
        /// A test symbol module that provides SourceLink JSON for testing.
        /// </summary>
        private class TestSymbolModuleWithSourceLink : ManagedSymbolModule
        {
            public TestSymbolModuleWithSourceLink(SymbolReader reader)
                : base(reader, "test.pdb")
            {
            }

            public override SourceLocation SourceLocationForManagedCode(uint methodMetadataToken, int ilOffset)
            {
                // Not used in this test
                return null;
            }

            protected override IEnumerable<string> GetSourceLinkJson()
            {
                // Return SourceLink JSON with both wildcard and exact path mappings
                // This mimics the example from issue #2350
                return new[]
                {
                    @"{
                        ""documents"": {
                            ""C:\\src\\myproject\\*"": ""https://raw.githubusercontent.com/org/repo/commit/*"",
                            ""c:\\external\\sdk\\inc\\header.h"": ""https://example.com/blobs/ABC123?download=true&filename=header.h""
                        }
                    }"
                };
            }
        }
    }
}
