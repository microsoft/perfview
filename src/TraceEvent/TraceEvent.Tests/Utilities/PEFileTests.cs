using System;
using System.IO;
using System.Reflection;
using PEFile;
using Xunit;
using Xunit.Abstractions;

namespace TraceEventTests
{
    public class PEFileTests
    {
        private readonly ITestOutputHelper _output;

        public PEFileTests(ITestOutputHelper output)
        {
            _output = output;
        }

        /// <summary>
        /// Test that we can successfully read a PE file and access basic properties
        /// </summary>
        [Fact]
        public void PEFile_CanReadManagedAssembly()
        {
            // Use the currently executing assembly as a test PE file
            string assemblyPath = typeof(PEFileTests).Assembly.Location;
            _output.WriteLine($"Testing with assembly: {assemblyPath}");

            using (var peFile = new PEFile.PEFile(assemblyPath))
            {
                Assert.NotNull(peFile.Header);
                
                // Verify basic PE header properties
                Assert.True(peFile.Header.PEHeaderSize > 0);
                _output.WriteLine($"PE Header Size: {peFile.Header.PEHeaderSize}");
                
                Assert.True(peFile.Header.NumberOfSections > 0);
                _output.WriteLine($"Number of Sections: {peFile.Header.NumberOfSections}");
                
                // Check that it's a managed assembly
                Assert.True(peFile.Header.IsManaged);
                _output.WriteLine($"Is Managed: {peFile.Header.IsManaged}");
            }
        }

        /// <summary>
        /// Test that machine type is correctly identified
        /// </summary>
        [Fact]
        public void PEFile_ReadsCorrectMachineType()
        {
            string assemblyPath = typeof(PEFileTests).Assembly.Location;

            using (var peFile = new PEFile.PEFile(assemblyPath))
            {
                var machineType = peFile.Header.Machine;
                _output.WriteLine($"Machine Type: {machineType}");
                
                // Should be one of the known machine types
                Assert.True(
                    machineType == MachineType.X86 ||
                    machineType == MachineType.Amd64 ||
                    machineType == MachineType.ARM ||
                    machineType == MachineType.ia64,
                    $"Unexpected machine type: {machineType}");
            }
        }

        /// <summary>
        /// Test that PE64 detection works correctly
        /// </summary>
        [Fact]
        public void PEFile_DetectsPE64Correctly()
        {
            string assemblyPath = typeof(PEFileTests).Assembly.Location;

            using (var peFile = new PEFile.PEFile(assemblyPath))
            {
                bool isPE64 = peFile.Header.IsPE64;
                _output.WriteLine($"Is PE64: {isPE64}");
                
                // The IsPE64 flag should match the machine type
                if (peFile.Header.Machine == MachineType.Amd64 || peFile.Header.Machine == MachineType.ia64)
                {
                    Assert.True(isPE64, "64-bit machine type should report IsPE64 = true");
                }
                else if (peFile.Header.Machine == MachineType.X86)
                {
                    Assert.False(isPE64, "32-bit machine type should report IsPE64 = false");
                }
            }
        }

        /// <summary>
        /// Test that timestamp is valid
        /// </summary>
        [Fact]
        public void PEFile_HasValidTimestamp()
        {
            string assemblyPath = typeof(PEFileTests).Assembly.Location;

            using (var peFile = new PEFile.PEFile(assemblyPath))
            {
                int timestampSec = peFile.Header.TimeDateStampSec;
                DateTime timestamp = peFile.Header.TimeDateStamp;
                
                _output.WriteLine($"Timestamp (seconds): {timestampSec}");
                _output.WriteLine($"Timestamp (DateTime): {timestamp}");
                
                // Timestamp should be reasonable (after 1990, before far future)
                // Note: Some builds use deterministic timestamps which may be in future
                Assert.True(timestamp.Year >= 1990, "Timestamp year should be >= 1990");
                Assert.True(timestamp.Year <= 2100, "Timestamp year should be <= 2100");
            }
        }

        /// <summary>
        /// Test that various PE header properties are accessible without throwing
        /// </summary>
        [Fact]
        public void PEFile_AllPropertiesAccessible()
        {
            string assemblyPath = typeof(PEFileTests).Assembly.Location;

            using (var peFile = new PEFile.PEFile(assemblyPath))
            {
                var header = peFile.Header;
                
                // Access all major properties to ensure they don't throw
                var signature = header.Signature;
                var machine = header.Machine;
                var numberOfSections = header.NumberOfSections;
                var sizeOfOptionalHeader = header.SizeOfOptionalHeader;
                var characteristics = header.Characteristics;
                var magic = header.Magic;
                var majorLinkerVersion = header.MajorLinkerVersion;
                var minorLinkerVersion = header.MinorLinkerVersion;
                var sizeOfCode = header.SizeOfCode;
                var sizeOfInitializedData = header.SizeOfInitializedData;
                var sizeOfUninitializedData = header.SizeOfUninitializedData;
                var addressOfEntryPoint = header.AddressOfEntryPoint;
                var baseOfCode = header.BaseOfCode;
                var imageBase = header.ImageBase;
                var sectionAlignment = header.SectionAlignment;
                var fileAlignment = header.FileAlignment;
                var sizeOfImage = header.SizeOfImage;
                var sizeOfHeaders = header.SizeOfHeaders;
                var checkSum = header.CheckSum;
                var subsystem = header.Subsystem;
                var dllCharacteristics = header.DllCharacteristics;
                
                _output.WriteLine($"Signature: 0x{signature:X}");
                _output.WriteLine($"Machine: {machine}");
                _output.WriteLine($"Sections: {numberOfSections}");
                _output.WriteLine($"Magic: 0x{magic:X}");
                _output.WriteLine($"Entry Point: 0x{addressOfEntryPoint:X}");
                _output.WriteLine($"Image Base: 0x{imageBase:X}");
                _output.WriteLine($"Size of Image: 0x{sizeOfImage:X}");
                
                // Verify PE signature is correct
                Assert.Equal(0x4550u, signature); // "PE\0\0"
            }
        }

        /// <summary>
        /// Test that data directories are accessible
        /// </summary>
        [Fact]
        public void PEFile_DataDirectoriesAccessible()
        {
            string assemblyPath = typeof(PEFileTests).Assembly.Location;

            using (var peFile = new PEFile.PEFile(assemblyPath))
            {
                var header = peFile.Header;
                
                // Access various data directories
                var exportDir = header.ExportDirectory;
                var importDir = header.ImportDirectory;
                var resourceDir = header.ResourceDirectory;
                var exceptionDir = header.ExceptionDirectory;
                var securityDir = header.CertificatesDirectory;
                var relocDir = header.BaseRelocationDirectory;
                var debugDir = header.DebugDirectory;
                var comDescriptorDir = header.ComDescriptorDirectory;
                
                _output.WriteLine($"Export Directory RVA: 0x{exportDir.VirtualAddress:X}");
                _output.WriteLine($"Import Directory RVA: 0x{importDir.VirtualAddress:X}");
                _output.WriteLine($"Resource Directory RVA: 0x{resourceDir.VirtualAddress:X}");
                _output.WriteLine($"COM Descriptor Directory RVA: 0x{comDescriptorDir.VirtualAddress:X}");
                
                // Managed assemblies should have a COM descriptor
                if (header.IsManaged)
                {
                    Assert.True(comDescriptorDir.VirtualAddress > 0, "Managed assembly should have COM descriptor");
                }
            }
        }

        /// <summary>
        /// Test that RvaToFileOffset works correctly
        /// </summary>
        [Fact]
        public void PEFile_RvaToFileOffsetWorks()
        {
            string assemblyPath = typeof(PEFileTests).Assembly.Location;

            using (var peFile = new PEFile.PEFile(assemblyPath))
            {
                var header = peFile.Header;
                
                // Get a valid RVA from the entry point
                uint entryPointRva = header.AddressOfEntryPoint;
                
                if (entryPointRva > 0)
                {
                    // Convert RVA to file offset
                    int fileOffset = header.RvaToFileOffset((int)entryPointRva);
                    
                    _output.WriteLine($"Entry Point RVA: 0x{entryPointRva:X}");
                    _output.WriteLine($"Entry Point File Offset: 0x{fileOffset:X}");
                    
                    // File offset should be positive and reasonable
                    Assert.True(fileOffset > 0, "File offset should be positive");
                    Assert.True(fileOffset < new FileInfo(assemblyPath).Length, "File offset should be within file size");
                }
            }
        }

        /// <summary>
        /// Test that invalid PE files throw appropriate exceptions
        /// </summary>
        [Fact]
        public void PEFile_InvalidFileThrowsException()
        {
            // Create a temporary file with invalid content
            string tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, "This is not a PE file");
                
                Assert.Throws<InvalidOperationException>(() =>
                {
                    using (var peFile = new PEFile.PEFile(tempFile))
                    {
                        // Should throw before getting here
                    }
                });
            }
            finally
            {
                // Wait a bit and retry deletion in case file is still locked
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        if (File.Exists(tempFile))
                        {
                            File.Delete(tempFile);
                        }
                        break;
                    }
                    catch (IOException)
                    {
                        if (i == 9) throw; // Rethrow on last attempt
                        System.Threading.Thread.Sleep(50); // Wait 50ms before retry
                    }
                }
            }
        }

        /// <summary>
        /// Test that bounds checking works - accessing beyond buffer should be caught
        /// </summary>
        [Fact]
        public void PEFile_BoundsCheckingWorks()
        {
            string assemblyPath = typeof(PEFileTests).Assembly.Location;

            using (var peFile = new PEFile.PEFile(assemblyPath))
            {
                var header = peFile.Header;
                
                // Try to access a directory index that's out of range
                // This should return an empty directory structure rather than throwing
                var invalidDir = header.Directory(99);
                
                Assert.Equal(0u, (uint)invalidDir.VirtualAddress);
                Assert.Equal(0u, (uint)invalidDir.Size);
            }
        }

        /// <summary>
        /// Test multiple sequential reads from the same file
        /// </summary>
        [Fact]
        public void PEFile_MultipleReadsWork()
        {
            string assemblyPath = typeof(PEFileTests).Assembly.Location;

            using (var peFile = new PEFile.PEFile(assemblyPath))
            {
                var header = peFile.Header;
                
                // Read the same property multiple times
                var machine1 = header.Machine;
                var machine2 = header.Machine;
                var machine3 = header.Machine;
                
                Assert.Equal(machine1, machine2);
                Assert.Equal(machine2, machine3);
                
                // Read different properties
                var numberOfSections1 = header.NumberOfSections;
                var isPE64 = header.IsPE64;
                var numberOfSections2 = header.NumberOfSections;
                
                Assert.Equal(numberOfSections1, numberOfSections2);
            }
        }
    }
}
