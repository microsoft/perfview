using Microsoft.Diagnostics.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.PortableExecutable;
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
            DataTargetOptions options = ClrMdDataTargetOptions.Create(symbolPath, null, TextWriter.Null, true);

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
                credential, TextWriter.Null, true);

            Assert.Equal(new[] { "https://symweb.azurefd.net/", "http://symbols.internal/" }, options.SymbolPaths);
            Assert.Equal(cache, options.SymbolCachePath);
            Assert.Same(credential, options.SymbolTokenCredential);
            Assert.True(options.AllowPrivateSymbolServers);
            Assert.True(options.VerifyDacOnWindows);
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData(null, false)]
        [InlineData("https://symbols.example.com/", true)]
        [InlineData("https://symbols.example.com/", false)]
        [InlineData("SRV*C:\\PerfViewSymbols", true)]
        [InlineData("SRV*C:\\PerfViewSymbols", false)]
        public void DacVerificationUsesConfiguredValue(string symbolPath, bool verifyDacOnWindows)
        {
            DataTargetOptions options = ClrMdDataTargetOptions.Create(
                symbolPath, null, TextWriter.Null, verifyDacOnWindows);

            Assert.Equal(verifyDacOnWindows, options.VerifyDacOnWindows);
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
                    symbolStore ? "SRV*" + directory : directory, null, TextWriter.Null, true);

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
                DataTargetOptions options = ClrMdDataTargetOptions.Create("SRV*" + directory, null, TextWriter.Null, true);
                Assert.Equal(candidate, options.FileLocator.FindPEImage(fileName, properties,
                    ImmutableArray.Create<byte>(1, 2, 3), OSPlatform.Create(platform), true));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [WindowsFact]
        public void FindsFlatDacWithoutCheckingRuntimeIndexingProperties()
        {
            using (var fixture = new LocalRuntimeFixture(ClrFlavor.Core))
            {
                DataTargetOptions options = ClrMdDataTargetOptions.Create(
                    fixture.DirectoryPath, null, TextWriter.Null, true);
                string name = Path.GetFileName(fixture.DacPath);
                int timestamp = unchecked(fixture.Timestamp + 1);
                int size = fixture.ImageSize + 1;

                Assert.Equal(fixture.DacPath, options.FileLocator.FindPEImage(name, timestamp, size, false));
                Assert.Null(options.FileLocator.FindPEImage(name, timestamp, size, true));
            }
        }

        [Theory]
        [InlineData("LINUX", SymbolProperties.Self)]
        [InlineData("LINUX", SymbolProperties.Coreclr)]
        [InlineData("OSX", SymbolProperties.Self)]
        [InlineData("OSX", SymbolProperties.Coreclr)]
        public void FindsFlatCrossPlatformDacWithoutCheckingProperties(string platform, SymbolProperties properties)
        {
            using (var fixture = new LocalRuntimeFixture(ClrFlavor.Core))
            {
                DataTargetOptions options = ClrMdDataTargetOptions.Create(
                    fixture.DirectoryPath, null, TextWriter.Null, true);
                string name = Path.GetFileName(fixture.DacPath);
                var buildId = ImmutableArray.Create<byte>(1, 2, 3);
                var originalPlatform = OSPlatform.Create(platform);

                Assert.Equal(fixture.DacPath, options.FileLocator.FindPEImage(
                    name, properties, buildId, originalPlatform, false));
                Assert.Null(options.FileLocator.FindPEImage(
                    name, properties, buildId, originalPlatform, true));
            }
        }

        [WindowsFact]
        public void RejectedIndexedImageContinuesLocalSearch()
        {
            foreach (bool secondIsSymbolStore in new[] { false, true })
            using (var fixture = new LocalRuntimeFixture())
            {
                string name = Path.GetFileName(fixture.RuntimePath);
                string key = fixture.Timestamp.ToString("x8") + fixture.ImageSize.ToString("x");
                string firstDirectory = Path.Combine(fixture.SymbolStore, name, key);
                Directory.CreateDirectory(firstDirectory);
                string rejected = Path.Combine(firstDirectory, name);
                File.Copy(fixture.RuntimePath, rejected);
                WriteImageTimestamp(rejected, unchecked(fixture.Timestamp + 1));

                string secondStore = Path.Combine(fixture.DirectoryPath, "second");
                string secondDirectory = secondIsSymbolStore ? Path.Combine(secondStore, name, key) : secondStore;
                Directory.CreateDirectory(secondDirectory);
                string expected = Path.Combine(secondDirectory, name);
                File.Copy(fixture.RuntimePath, expected);
                string path = "SRV*" + fixture.SymbolStore + ";" + (secondIsSymbolStore ? "SRV*" : "") + secondStore;
                var log = new StringWriter();
                DataTargetOptions options = ClrMdDataTargetOptions.Create(path, null, log, true);

                Assert.Equal(expected, options.FileLocator.FindPEImage(name, fixture.Timestamp, fixture.ImageSize, true));
                Assert.Contains("Rejecting local image", log.ToString());
            }
        }

        [WindowsFact]
        public void UnreadableLocalImageContinuesLocalSearch()
        {
            foreach (bool firstIsSymbolStore in new[] { false, true })
            using (var fixture = new LocalRuntimeFixture())
            {
                string name = Path.GetFileName(fixture.RuntimePath);
                string firstDirectory = firstIsSymbolStore
                    ? Path.Combine(fixture.SymbolStore, name, fixture.Timestamp.ToString("x8") + fixture.ImageSize.ToString("x"))
                    : fixture.SymbolStore;
                Directory.CreateDirectory(firstDirectory);
                string locked = Path.Combine(firstDirectory, name);
                File.Copy(fixture.RuntimePath, locked);
                string path = (firstIsSymbolStore ? "SRV*" : "") + fixture.SymbolStore + ";" + fixture.DirectoryPath;
                var log = new StringWriter();
                DataTargetOptions options = ClrMdDataTargetOptions.Create(path, null, log, true);

                using (File.Open(locked, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    Assert.Equal(fixture.RuntimePath,
                        options.FileLocator.FindPEImage(name, fixture.Timestamp, fixture.ImageSize, true));
                }
                Assert.Contains("Could not", log.ToString());
            }
        }

        [WindowsFact]
        public void FindsAbsoluteImageOutsideConfiguredSymbolStore()
        {
            using (var fixture = new LocalRuntimeFixture())
            {
                DataTargetOptions options = fixture.CreateOptions();
                Assert.Equal(fixture.RuntimePath, options.FileLocator.FindPEImage(
                    fixture.RuntimePath, fixture.Timestamp, fixture.ImageSize, true));
                Assert.Equal(fixture.RuntimePath, options.FileLocator.FindPEImage(
                    fixture.RuntimePath.Replace('\\', '/'), fixture.Timestamp, fixture.ImageSize, true));
                Assert.Null(options.FileLocator.FindPEImage(
                    fixture.RuntimePath, unchecked(fixture.Timestamp + 1), fixture.ImageSize, true));
                Assert.Null(options.FileLocator.FindPEImage(
                    fixture.RuntimePath, fixture.Timestamp, fixture.ImageSize + 1, true));
                Assert.Equal(fixture.RuntimePath, options.FileLocator.FindPEImage(
                    fixture.RuntimePath, 0, 0, false));
                Assert.Null(options.FileLocator.FindPEImage(
                    Path.Combine(fixture.DirectoryPath, "missing.dll"), fixture.Timestamp, fixture.ImageSize, true));

                File.WriteAllText(fixture.RuntimePath, "Not a PE image");
                Assert.Null(options.FileLocator.FindPEImage(
                    fixture.RuntimePath, fixture.Timestamp, fixture.ImageSize, true));
            }
        }

        [WindowsFact]
        public void DoesNotProbeUnsafeImagePaths()
        {
            using (var fixture = new LocalRuntimeFixture())
            {
                DataTargetOptions options = fixture.CreateOptions();
                foreach (string path in new[]
                {
                    "clr.dll", "C:clr.dll", @"\clr.dll", @"\\server\share\clr.dll",
                    @"\\?\" + fixture.RuntimePath, @"\\.\" + fixture.RuntimePath,
                    "https://example.com/clr.dll",
                    Path.Combine(fixture.DirectoryPath, "unused", "..", "clr.dll"),
                    Path.Combine(fixture.DirectoryPath, "CON", "clr.dll"),
                    fixture.RuntimePath + ".", fixture.RuntimePath + " "
                })
                {
                    Assert.Null(options.FileLocator.FindPEImage(path, fixture.Timestamp, fixture.ImageSize, true));
                    Assert.Null(options.FileLocator.FindPEImage(path, fixture.Timestamp, fixture.ImageSize, false));
                }
            }
        }

        [WindowsFact]
        public void NormalizesRuntimePathBeforeAddingAdjacentDac()
        {
            using (var fixture = new LocalRuntimeFixture())
            using (var target = new DataTarget(new EmptyDataReader(), fixture.CreateOptions()))
            {
                ClrInfo clr = fixture.CreateClrInfo(target, ClrFlavor.Desktop, fixture.RuntimePath.Replace('\\', '/'));
                ClrMdDataTargetOptions.AddLocalDacCandidate(clr, TextWriter.Null);
                Assert.Equal(fixture.DacPath, clr.DebuggingLibraries[0].FileName);
            }
        }

        [WindowsFact]
        public void RejectedAbsoluteImageFallsBackToConfiguredStore()
        {
            using (var fixture = new LocalRuntimeFixture())
            {
                string name = Path.GetFileName(fixture.RuntimePath);
                string directory = Path.Combine(fixture.SymbolStore, name,
                    fixture.Timestamp.ToString("x8") + fixture.ImageSize.ToString("x"));
                Directory.CreateDirectory(directory);
                string cached = Path.Combine(directory, name);
                File.Copy(fixture.RuntimePath, cached);
                File.WriteAllText(fixture.RuntimePath, "Not a PE image");

                Assert.Equal(cached, fixture.CreateOptions().FileLocator.FindPEImage(
                    fixture.RuntimePath, fixture.Timestamp, fixture.ImageSize, true));
            }
        }

        [WindowsFact]
        public void AbsoluteImageReadsApplyTargetRelocations()
        {
            using (var fixture = new LocalRuntimeFixture())
            using (var stream = File.OpenRead(fixture.RuntimePath))
            using (var pe = new PEReader(stream))
            {
                var relocation = FindRelocation(pe);
                byte[] originalBytes = pe.GetSectionData(relocation.Rva).GetContent(0, relocation.Size).ToArray();
                ulong original = relocation.Size == 4
                    ? BitConverter.ToUInt32(originalBytes, 0) : BitConverter.ToUInt64(originalBytes, 0);
                ulong loadedBase = pe.PEHeaders.PEHeader.ImageBase + 0x100000;
                ulong expected = relocation.Size == 4
                    ? unchecked((uint)(original + 0x100000)) : original + 0x100000;

                DataTargetOptions options = fixture.CreateOptions();
                Assert.Equal(expected, ReadImagePointer(fixture, options, loadedBase, relocation.Rva, relocation.Size));

                // A locator miss reproduces ClrMD's zero-base fallback, without changing product code.
                options.FileLocator = new MissingFileLocator();
                Assert.Equal(original, ReadImagePointer(fixture, options, loadedBase, relocation.Rva, relocation.Size));
                Assert.NotEqual(original, expected);
            }
        }

        [WindowsFact]
        public void AddsMatchingAdjacentDacBeforeExistingCandidates()
        {
            foreach (ClrFlavor flavor in new[] { ClrFlavor.Desktop, ClrFlavor.Core })
            foreach (bool verifyDac in new[] { true, false })
            {
                using (var fixture = new LocalRuntimeFixture(flavor))
                using (var target = new DataTarget(new EmptyDataReader(), fixture.CreateOptions(verifyDac)))
                {
                    ClrInfo clr = fixture.CreateClrInfo(target, flavor);
                    ImmutableArray<DebugLibraryInfo> existing = clr.DebuggingLibraries;
                    ClrMdDataTargetOptions.AddLocalDacCandidate(clr, TextWriter.Null);

                    DebugLibraryInfo candidate = clr.DebuggingLibraries[0];
                    Assert.Equal(fixture.DacPath, candidate.FileName);
                    Assert.Equal(DebugLibraryKind.Dac, candidate.Kind);
                    Assert.Equal(OSPlatform.Windows, candidate.Platform);
                    Assert.Equal(RuntimeInformation.ProcessArchitecture, candidate.TargetArchitecture);
                    Assert.Equal(SymbolProperties.Coreclr, candidate.ArchivedUnder);
                    Assert.Equal(fixture.Timestamp, candidate.IndexTimeStamp);
                    Assert.Equal(fixture.ImageSize, candidate.IndexFileSize);
                    Assert.Equal(existing.Length + 1, clr.DebuggingLibraries.Length);
                    for (int i = 0; i < existing.Length; i++)
                        Assert.Same(existing[i], clr.DebuggingLibraries[i + 1]);
                    Assert.Equal(verifyDac, target.Options.VerifyDacOnWindows);
                    Assert.Empty(Directory.GetFileSystemEntries(fixture.SymbolStore));

                    ImmutableArray<DebugLibraryInfo> prepared = clr.DebuggingLibraries;
                    ClrMdDataTargetOptions.AddLocalDacCandidate(clr, TextWriter.Null);
                    Assert.Equal(prepared, clr.DebuggingLibraries);
                }
            }
        }

        [WindowsFact]
        public void DoesNotAddUnqualifiedAdjacentDac()
        {
            foreach (string scenario in new[]
            {
                "timestamp", "size", "zero timestamp", "zero size", "single file",
                "missing runtime", "missing dac", "invalid runtime", "relative path", "device path",
                "traversal", "reserved segment", "platform", "architecture"
            })
            {
                using (var fixture = new LocalRuntimeFixture())
                {
                    var reader = new EmptyDataReader();
                    string runtimePath = fixture.RuntimePath;
                    switch (scenario)
                    {
                        case "missing runtime": File.Delete(runtimePath); break;
                        case "missing dac": File.Delete(fixture.DacPath); break;
                        case "invalid runtime": File.WriteAllText(runtimePath, "Not a PE image"); break;
                        case "relative path": runtimePath = Path.GetFileName(runtimePath); break;
                        case "device path": runtimePath = @"\\?\" + runtimePath; break;
                        case "traversal": runtimePath = Path.Combine(fixture.DirectoryPath, "unused", "..", "clr.dll"); break;
                        case "reserved segment": runtimePath = Path.Combine(fixture.DirectoryPath, "CON", "clr.dll"); break;
                        case "platform": reader.TargetPlatform = OSPlatform.Linux; break;
                        case "architecture":
                            reader.Architecture = RuntimeInformation.ProcessArchitecture == Architecture.X64
                                ? Architecture.X86 : Architecture.X64;
                            break;
                    }
                    using (var target = new DataTarget(reader, fixture.CreateOptions()))
                    {
                        ClrInfo clr = fixture.CreateClrInfo(target, ClrFlavor.Desktop, runtimePath);
                        switch (scenario)
                        {
                            case "timestamp": clr.IndexTimeStamp = unchecked(clr.IndexTimeStamp + 1); break;
                            case "size": clr.IndexFileSize++; break;
                            case "zero timestamp": clr.IndexTimeStamp = 0; break;
                            case "zero size": clr.IndexFileSize = 0; break;
                            case "single file": clr.IsSingleFile = true; break;
                        }
                        ImmutableArray<DebugLibraryInfo> existing = clr.DebuggingLibraries;
                        ClrMdDataTargetOptions.AddLocalDacCandidate(clr, TextWriter.Null);
                        Assert.True(existing.Equals(clr.DebuggingLibraries), scenario);
                    }
                }
            }
        }

        #region private
        private static void WriteImageTimestamp(string path, int timestamp)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite))
            using (var reader = new BinaryReader(stream))
            using (var writer = new BinaryWriter(stream))
            {
                stream.Position = 0x3c;
                int peOffset = reader.ReadInt32();
                stream.Position = peOffset + 8;
                writer.Write(timestamp);
            }
        }

        private static (int Rva, int Size) FindRelocation(PEReader pe)
        {
            var directory = pe.PEHeaders.PEHeader.BaseRelocationTableDirectory;
            byte[] data = pe.GetSectionData(directory.RelativeVirtualAddress).GetContent(0, directory.Size).ToArray();
            for (int block = 0; block < data.Length;)
            {
                int page = BitConverter.ToInt32(data, block);
                int blockSize = BitConverter.ToInt32(data, block + 4);
                Assert.InRange(blockSize, 8, data.Length - block);
                for (int offset = block + 8; offset < block + blockSize; offset += 2)
                {
                    ushort entry = BitConverter.ToUInt16(data, offset);
                    int kind = entry >> 12;
                    if (kind == 3 || kind == 10)
                        return (page + (entry & 0xfff), kind == 3 ? 4 : 8);
                }
                block += blockSize;
            }
            throw new InvalidOperationException("The test PE image must have a pointer relocation.");
        }

        private static ulong ReadImagePointer(LocalRuntimeFixture fixture, DataTargetOptions options,
            ulong loadedBase, int rva, int size)
        {
            using (var target = new DataTarget(new EmptyDataReader(), options))
            {
                // LoadPEImage and its returned PEImage are internal to ClrMD.
                MethodInfo load = typeof(DataTarget).GetMethod("LoadPEImage", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(load);
                object image = load.Invoke(target, new object[]
                {
                    fixture.RuntimePath, fixture.Timestamp, fixture.ImageSize, true, loadedBase
                });
                Assert.NotNull(image);
                MethodInfo read = image.GetType().GetMethod("Read", new[] { typeof(int), typeof(Span<byte>) });
                Assert.NotNull(read);
                var readImage = (ReadPEImage)Delegate.CreateDelegate(typeof(ReadPEImage), image, read);
                byte[] bytes = new byte[size];
                Assert.Equal(size, readImage(rva, bytes));
                return size == 4 ? BitConverter.ToUInt32(bytes, 0) : BitConverter.ToUInt64(bytes, 0);
            }
        }

        private delegate int ReadPEImage(int rva, Span<byte> buffer);

        private sealed class LocalRuntimeFixture : IDisposable
        {
            public LocalRuntimeFixture(ClrFlavor flavor = ClrFlavor.Desktop)
            {
                DirectoryPath = Path.Combine(Path.GetTempPath(), "PerfViewClrMdTest-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(DirectoryPath);
                SymbolStore = Path.Combine(DirectoryPath, "symbols");
                Directory.CreateDirectory(SymbolStore);
                RuntimePath = Path.Combine(DirectoryPath, flavor == ClrFlavor.Desktop ? "clr.dll" : "coreclr.dll");
                DacPath = Path.Combine(DirectoryPath, flavor == ClrFlavor.Desktop ? "mscordacwks.dll" : "mscordaccore.dll");
                File.Copy(typeof(DataTarget).Assembly.Location, RuntimePath);
                File.Copy(typeof(DataTarget).Assembly.Location, DacPath);
                using (var stream = File.OpenRead(RuntimePath))
                using (var pe = new PEReader(stream))
                {
                    Timestamp = pe.PEHeaders.CoffHeader.TimeDateStamp;
                    ImageSize = pe.PEHeaders.PEHeader.SizeOfImage;
                }
            }

            public string DirectoryPath { get; }
            public string SymbolStore { get; }
            public string RuntimePath { get; }
            public string DacPath { get; }
            public int Timestamp { get; }
            public int ImageSize { get; }

            public DataTargetOptions CreateOptions(bool verifyDac = true)
            {
                DataTargetOptions options = ClrMdDataTargetOptions.Create("SRV*" + SymbolStore, null, TextWriter.Null, verifyDac);
                options.SkipRuntimeEnumeration = true;
                return options;
            }

            public ClrInfo CreateClrInfo(DataTarget target, ClrFlavor flavor, string runtimePath = null)
            {
                var module = new TestModule(runtimePath ?? RuntimePath);
                return new ClrInfo(target, module, new Version(4, 0))
                {
                    Flavor = flavor,
                    IndexTimeStamp = Timestamp,
                    IndexFileSize = ImageSize,
                    DebuggingLibraries = ImmutableArray.Create(
                        new DebugLibraryInfo(DebugLibraryKind.Dac, "server-dac.dll",
                            target.DataReader.Architecture, SymbolProperties.Coreclr, ImageSize, Timestamp),
                        new DebugLibraryInfo(DebugLibraryKind.Dbi, "server-dbi.dll",
                            target.DataReader.Architecture, SymbolProperties.Coreclr, ImageSize, Timestamp))
                };
            }

            public void Dispose()
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }

        private sealed class TestModule : ModuleInfo
        {
            public TestModule(string fileName) : base(0x10000000, fileName) { }
            public override ModuleKind Kind => ModuleKind.PortableExecutable;
        }

        private sealed class EmptyDataReader : IDataReader
        {
            public string DisplayName => "ClrMD lookup test";
            public bool IsThreadSafe => true;
            public OSPlatform TargetPlatform { get; set; } = OSPlatform.Windows;
            public Architecture Architecture { get; set; } = RuntimeInformation.ProcessArchitecture;
            public int ProcessId => 0;
            public int PointerSize => Architecture == Architecture.X86 ? 4 : 8;
            public IEnumerable<ModuleInfo> EnumerateModules() => Array.Empty<ModuleInfo>();
            public bool GetThreadContext(uint threadID, uint contextFlags, Span<byte> context) => false;
            public void FlushCachedData() { }
            public int Read(ulong address, Span<byte> buffer) => 0;
            public bool Read<T>(ulong address, out T value) where T : unmanaged
            {
                value = default;
                return false;
            }
            public T Read<T>(ulong address) where T : unmanaged => default;
            public bool ReadPointer(ulong address, out ulong value)
            {
                value = 0;
                return false;
            }
            public ulong ReadPointer(ulong address) => 0;
        }

        private sealed class MissingFileLocator : IFileLocator
        {
            public string FindPEImage(string fileName, int buildTimeStamp, int imageSize, bool checkProperties) => null;
            public string FindPEImage(string fileName, SymbolProperties archivedUnder, ImmutableArray<byte> buildIdOrUUID,
                OSPlatform originalPlatform, bool checkProperties) => null;
        }
        #endregion
    }
}
