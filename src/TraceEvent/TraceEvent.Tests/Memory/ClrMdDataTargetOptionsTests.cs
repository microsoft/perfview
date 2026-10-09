using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Immutable;
using System.IO;
using System.Runtime.InteropServices;
using Xunit;

namespace TraceEventTests
{
    public class ClrMdDataTargetOptionsTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void DefaultOptionsKeepSecureDefaults(string symbolPath)
        {
            DataTargetOptions options = ClrMdDataTargetOptions.Create(symbolPath, null, TextWriter.Null);

            Assert.Equal(new[] { "https://msdl.microsoft.com/download/symbols" }, options.SymbolPaths);
            Assert.True(options.VerifyDacOnWindows);
            Assert.False(options.AllowPrivateSymbolServers);
            Assert.Equal(Environment.Is64BitProcess, options.UseLockFreeMemoryMapReader);
            Assert.False(options.ForceCompleteRuntimeEnumeration);
            Assert.False(options.SkipRuntimeEnumeration);
        }

        [Fact]
        public void ConfiguredServersUsePerfViewCache()
        {
            string cache = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
            var credential = new Azure.Identity.InteractiveBrowserCredential();
            DataTargetOptions options = ClrMdDataTargetOptions.Create(
                "SRV*" + cache + "*https://symweb.azurefd.net/;SRV*http://symbols.internal/",
                credential, TextWriter.Null);

            Assert.Equal(new[] { "https://symweb.azurefd.net/", "http://symbols.internal/" }, options.SymbolPaths);
            Assert.Equal(cache, options.SymbolCachePath);
            Assert.Same(credential, options.SymbolTokenCredential);
            Assert.True(options.AllowPrivateSymbolServers);
            Assert.True(options.VerifyDacOnWindows);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void FindsLocalDacWithoutTreatingDirectoryAsHttpServer(bool symbolStore)
        {
            string directory = Path.Combine(Path.GetTempPath(), "PerfViewClrMdTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string source = typeof(DataTarget).Assembly.Location;
                string fileName = Path.GetFileName(source);
                int timestamp;
                int size;
                using (var pe = new PEFile.PEFile(source))
                {
                    timestamp = unchecked((int)pe.Header.TimeDateStampSec);
                    size = unchecked((int)pe.Header.SizeOfImage);
                }

                string destinationDirectory = symbolStore
                    ? Path.Combine(directory, fileName, timestamp.ToString("x8") + size.ToString("x"))
                    : directory;
                Directory.CreateDirectory(destinationDirectory);
                string destination = Path.Combine(destinationDirectory, fileName);
                File.Copy(source, destination);

                DataTargetOptions options = ClrMdDataTargetOptions.Create(
                    symbolStore ? "SRV*" + directory : directory, null, TextWriter.Null);

                Assert.Empty(options.SymbolPaths);
                Assert.Equal(destination, options.FileLocator.FindPEImage(fileName, timestamp, size, true));
                Assert.Null(options.FileLocator.FindPEImage("nonexistent-perfview-test.dll", timestamp, size, true));
                if (symbolStore)
                {
                    int wrongTimestamp = unchecked(timestamp + 1);
                    string wrongDirectory = Path.Combine(directory, fileName, wrongTimestamp.ToString("x8") + size.ToString("x"));
                    Directory.CreateDirectory(wrongDirectory);
                    File.Copy(source, Path.Combine(wrongDirectory, fileName));
                    Assert.Null(options.FileLocator.FindPEImage(fileName, wrongTimestamp, size, true));
                }
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Theory]
        [InlineData("LINUX", SymbolProperties.Self, "elf-buildid-010203")]
        [InlineData("LINUX", SymbolProperties.Coreclr, "elf-buildid-coreclr-010203")]
        [InlineData("OSX", SymbolProperties.Self, "mach-uuid-010203")]
        [InlineData("OSX", SymbolProperties.Coreclr, "mach-uuid-coreclr-010203")]
        public void FindsCachedCrossPlatformDac(string platform, SymbolProperties properties, string key)
        {
            string directory = Path.Combine(Path.GetTempPath(), "PerfViewClrMdTest-" + Guid.NewGuid().ToString("N"));
            string fileName = "mscordaccore.dll";
            string candidate = Path.Combine(directory, fileName, key, fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(candidate));
            try
            {
                File.Copy(typeof(DataTarget).Assembly.Location, candidate);
                DataTargetOptions options = ClrMdDataTargetOptions.Create("SRV*" + directory, null, TextWriter.Null);
                Assert.Equal(candidate, options.FileLocator.FindPEImage(fileName, properties,
                    ImmutableArray.Create<byte>(1, 2, 3), OSPlatform.Create(platform), true));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
