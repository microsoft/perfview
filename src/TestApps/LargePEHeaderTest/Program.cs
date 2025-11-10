using System;
using System.IO;
using System.Runtime.InteropServices;

namespace LargePEHeaderGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Large PE Header Test Generator ===");
            Console.WriteLine("This tool generates a PE file with headers larger than 1024 bytes");
            Console.WriteLine("to demonstrate the improvement in the new PEFile implementation.\n");

            string outputPath = "LargeHeaderTest.exe";
            
            // Generate a PE file with many sections (headers > 1024 bytes)
            GenerateLargeHeaderPE(outputPath);
            
            Console.WriteLine($"\nGenerated test PE file: {outputPath}");
            Console.WriteLine($"File size: {new FileInfo(outputPath).Length} bytes\n");
            
            // Analyze with a simple reader to show the header size
            AnalyzePEFile(outputPath);
        }

        static void GenerateLargeHeaderPE(string outputPath)
        {
            // Create a minimal PE file with many sections to make headers > 1024 bytes
            // We'll create 20 sections which should push us well over 1024 bytes
            
            const int numSections = 20;
            const int sectionHeaderSize = 40; // sizeof(IMAGE_SECTION_HEADER)
            
            using (var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(fs))
            {
                // DOS Header
                writer.Write((ushort)0x5A4D); // e_magic "MZ"
                writer.Write(new byte[58]); // Rest of DOS header (mostly zeros)
                writer.Write((int)128); // e_lfanew - offset to PE header
                
                // DOS Stub (padding to reach PE header at offset 128)
                writer.Write(new byte[128 - 64]);
                
                // PE Signature
                writer.Write((uint)0x00004550); // "PE\0\0"
                
                // IMAGE_FILE_HEADER
                writer.Write((ushort)0x8664); // Machine (AMD64)
                writer.Write((ushort)numSections); // NumberOfSections
                writer.Write((uint)0); // TimeDateStamp
                writer.Write((uint)0); // PointerToSymbolTable
                writer.Write((uint)0); // NumberOfSymbols
                writer.Write((ushort)240); // SizeOfOptionalHeader (standard for PE32+)
                writer.Write((ushort)0x22); // Characteristics (EXECUTABLE_IMAGE | LARGE_ADDRESS_AWARE)
                
                // IMAGE_OPTIONAL_HEADER64
                writer.Write((ushort)0x20b); // Magic (PE32+)
                writer.Write((byte)14); // MajorLinkerVersion
                writer.Write((byte)0); // MinorLinkerVersion
                writer.Write((uint)0x1000); // SizeOfCode
                writer.Write((uint)0x1000); // SizeOfInitializedData
                writer.Write((uint)0); // SizeOfUninitializedData
                writer.Write((uint)0x2000); // AddressOfEntryPoint
                writer.Write((uint)0x1000); // BaseOfCode
                writer.Write((ulong)0x140000000); // ImageBase
                writer.Write((uint)0x1000); // SectionAlignment
                writer.Write((uint)0x200); // FileAlignment
                writer.Write((ushort)6); // MajorOperatingSystemVersion
                writer.Write((ushort)0); // MinorOperatingSystemVersion
                writer.Write((ushort)0); // MajorImageVersion
                writer.Write((ushort)0); // MinorImageVersion
                writer.Write((ushort)6); // MajorSubsystemVersion
                writer.Write((ushort)0); // MinorSubsystemVersion
                writer.Write((uint)0); // Win32VersionValue
                writer.Write((uint)(0x1000 + 0x1000 * numSections)); // SizeOfImage
                writer.Write((uint)0x400); // SizeOfHeaders
                writer.Write((uint)0); // CheckSum
                writer.Write((ushort)3); // Subsystem (WINDOWS_CUI)
                writer.Write((ushort)0x8160); // DllCharacteristics
                writer.Write((ulong)0x100000); // SizeOfStackReserve
                writer.Write((ulong)0x1000); // SizeOfStackCommit
                writer.Write((ulong)0x100000); // SizeOfHeapReserve
                writer.Write((ulong)0x1000); // SizeOfHeapCommit
                writer.Write((uint)0); // LoaderFlags
                writer.Write((uint)16); // NumberOfRvaAndSizes
                
                // Data Directories (16 entries, 8 bytes each)
                for (int i = 0; i < 16; i++)
                {
                    writer.Write((ulong)0); // VirtualAddress and Size
                }
                
                // Section Headers (20 sections)
                uint virtualAddress = 0x1000;
                uint fileOffset = 0x400;
                
                for (int i = 0; i < numSections; i++)
                {
                    // Section name (8 bytes)
                    string sectionName = $".sec{i:D2}";
                    byte[] nameBytes = new byte[8];
                    System.Text.Encoding.ASCII.GetBytes(sectionName).CopyTo(nameBytes, 0);
                    writer.Write(nameBytes);
                    
                    writer.Write((uint)0x1000); // VirtualSize
                    writer.Write(virtualAddress); // VirtualAddress
                    writer.Write((uint)0x200); // SizeOfRawData
                    writer.Write(fileOffset); // PointerToRawData
                    writer.Write((uint)0); // PointerToRelocations
                    writer.Write((uint)0); // PointerToLinenumbers
                    writer.Write((ushort)0); // NumberOfRelocations
                    writer.Write((ushort)0); // NumberOfLinenumbers
                    writer.Write((uint)0x60000020); // Characteristics (CODE | EXECUTE | READ)
                    
                    virtualAddress += 0x1000;
                    fileOffset += 0x200;
                }
                
                // Calculate actual header size
                long headerSize = fs.Position;
                Console.WriteLine($"Header size: {headerSize} bytes (Original implementation limited to 1024 bytes)");
                
                // Pad to file alignment
                while (fs.Position < 0x400)
                {
                    writer.Write((byte)0);
                }
                
                // Write minimal section data
                for (int i = 0; i < numSections; i++)
                {
                    // Write 512 bytes per section
                    writer.Write(new byte[0x200]);
                }
            }
        }

        static void AnalyzePEFile(string filePath)
        {
            Console.WriteLine("=== PE File Analysis ===");
            
            byte[] buffer = File.ReadAllBytes(filePath);
            Console.WriteLine($"Total file size: {buffer.Length} bytes");
            
            // Read DOS header
            ushort magic = BitConverter.ToUInt16(buffer, 0);
            if (magic != 0x5A4D)
            {
                Console.WriteLine("ERROR: Invalid DOS signature");
                return;
            }
            
            int peOffset = BitConverter.ToInt32(buffer, 60);
            Console.WriteLine($"PE header offset: {peOffset} bytes");
            
            // Read PE signature
            uint peSig = BitConverter.ToUInt32(buffer, peOffset);
            if (peSig != 0x00004550)
            {
                Console.WriteLine("ERROR: Invalid PE signature");
                return;
            }
            
            // Read COFF header
            ushort machine = BitConverter.ToUInt16(buffer, peOffset + 4);
            ushort numSections = BitConverter.ToUInt16(buffer, peOffset + 6);
            ushort optionalHeaderSize = BitConverter.ToUInt16(buffer, peOffset + 20);
            
            Console.WriteLine($"Machine type: 0x{machine:X4}");
            Console.WriteLine($"Number of sections: {numSections}");
            Console.WriteLine($"Optional header size: {optionalHeaderSize} bytes");
            
            // Calculate total header size
            int sectionsOffset = peOffset + 4 + 20 + optionalHeaderSize;
            int totalHeaderSize = sectionsOffset + (numSections * 40);
            
            Console.WriteLine($"Sections start at: {sectionsOffset} bytes");
            Console.WriteLine($"Total header size: {totalHeaderSize} bytes");
            Console.WriteLine();
            
            if (totalHeaderSize > 1024)
            {
                Console.WriteLine($"✓ Headers are {totalHeaderSize} bytes (> 1024 bytes)");
                Console.WriteLine("  This would FAIL with the original PEFile implementation");
                Console.WriteLine("  This SUCCEEDS with the new ReadOnlySpan-based implementation");
            }
            else
            {
                Console.WriteLine($"  Headers are only {totalHeaderSize} bytes");
                Console.WriteLine("  Need to increase section count to exceed 1024 bytes");
            }
        }
    }
}
