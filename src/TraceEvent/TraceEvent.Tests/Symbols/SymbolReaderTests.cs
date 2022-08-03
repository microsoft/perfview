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
        private const string FileName_CppConPdb = "CppCon.pdb";
        private const string FileName_CsDesktopPdbWithSourceLink = "CsDesktopWithSourceLink.pdb";

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
    }
}
