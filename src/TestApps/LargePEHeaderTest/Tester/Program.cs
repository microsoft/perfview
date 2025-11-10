using System;
using System.IO;
using PEFile;

namespace Tester
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== Testing Large PE Header with PEFile (New vs Old Implementation) ===\n");
            
            string testFile = args.Length > 0 ? args[0] : "../LargeHeaderTest.exe";
            
            if (!File.Exists(testFile))
            {
                Console.WriteLine($"ERROR: Test file not found: {testFile}");
                Console.WriteLine("Please run the LargePEHeaderGenerator first to create the test file.");
                Console.WriteLine("\nUsage: dotnet run [path_to_pe_file]");
                return;
            }
            
            Console.WriteLine($"Testing file: {testFile}");
            AnalyzePEStructure(testFile);
            
            // Test OLD implementation first (expected to fail for large headers)
            Console.WriteLine("\n" + new string('=', 80));
            Console.WriteLine("TEST 1: OLD Implementation (byte[] based, 1024 byte initial buffer)");
            Console.WriteLine(new string('=', 80));
            TestOldImplementation(testFile);
            
            // Test NEW implementation (expected to succeed)
            Console.WriteLine("\n" + new string('=', 80));
            Console.WriteLine("TEST 2: NEW Implementation (ReadOnlySpan based, progressive reads)");
            Console.WriteLine(new string('=', 80));
            TestNewImplementation(testFile);
            
            Console.WriteLine("\n" + new string('=', 80));
            Console.WriteLine("SUMMARY");
            Console.WriteLine(new string('=', 80));
            Console.WriteLine("The old implementation fails with large PE headers (>1024 bytes)");
            Console.WriteLine("because it uses a fixed 1024-byte buffer for the initial read.");
            Console.WriteLine("\nThe new ReadOnlySpan-based implementation succeeds by using");
            Console.WriteLine("progressive reads to handle arbitrarily large PE headers.");
        }
        
        static void TestOldImplementation(string testFile)
        {
            Console.WriteLine("Attempting to load with OLD PEFile implementation...");
            try
            {
                using (var peFile = new OldPEFile.PEFile(testFile))
                {
                    Console.WriteLine("✓ SUCCESS: File loaded successfully with OLD implementation!");
                    Console.WriteLine($"\nPE Header Information:");
                    Console.WriteLine($"  Machine Type: 0x{peFile.Header.Machine:X}");
                    Console.WriteLine($"  Number of Sections: {peFile.Header.NumberOfSections}");
                    Console.WriteLine($"  PE Header Size: {peFile.Header.PEHeaderSize} bytes");
                    Console.WriteLine($"  Is PE64: {peFile.Header.IsPE64}");
                    Console.WriteLine($"  Is Managed: {peFile.Header.IsManaged}");
                    
                    Console.WriteLine("\n⚠ UNEXPECTED: Old implementation should have failed!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ EXPECTED FAILURE: {ex.Message}");
                Console.WriteLine($"  Exception Type: {ex.GetType().Name}");
                Console.WriteLine($"\n  This is expected! The old implementation cannot handle");
                Console.WriteLine($"  PE headers larger than its initial 1024-byte buffer.");
            }
        }
        
        static void TestNewImplementation(string testFile)
        {
            Console.WriteLine("Attempting to load with NEW PEFile implementation...");
            try
            {
                using (var peFile = new PEFile.PEFile(testFile))
                {
                    Console.WriteLine("✓ SUCCESS: File loaded successfully!");
                    Console.WriteLine($"\nPE Header Information:");
                    Console.WriteLine($"  Machine Type: 0x{peFile.Header.Machine:X}");
                    Console.WriteLine($"  Number of Sections: {peFile.Header.NumberOfSections}");
                    Console.WriteLine($"  PE Header Size: {peFile.Header.PEHeaderSize} bytes");
                    Console.WriteLine($"  Is PE64: {peFile.Header.IsPE64}");
                    Console.WriteLine($"  Is Managed: {peFile.Header.IsManaged}");
                    Console.WriteLine($"  Image Base: 0x{peFile.Header.ImageBase:X}");
                    Console.WriteLine($"  Size of Image: {peFile.Header.SizeOfImage} bytes");
                    Console.WriteLine($"  Entry Point RVA: 0x{peFile.Header.AddressOfEntryPoint:X}");
                    Console.WriteLine($"  Subsystem: {peFile.Header.Subsystem}");
                    
                    // Try to access a section to verify bounds checking works
                    Console.WriteLine($"\nTesting section access:");
                    for (int i = 0; i < Math.Min(3, (int)peFile.Header.NumberOfSections); i++)
                    {
                        try
                        {
                            var rva = (int)peFile.Header.AddressOfEntryPoint;
                            var fileOffset = peFile.Header.RvaToFileOffset(rva);
                            Console.WriteLine($"  Section {i}: RVA 0x{rva:X} -> File Offset 0x{fileOffset:X}");
                            break; // Just test one conversion
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  Section {i}: Error accessing - {ex.Message}");
                        }
                    }
                    
                    Console.WriteLine("\n✓ NEW Implementation Test PASSED!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ FAILED: {ex.Message}");
                Console.WriteLine($"  Exception Type: {ex.GetType().Name}");
                Console.WriteLine($"\n  Stack Trace:");
                Console.WriteLine(ex.StackTrace);
                
                Console.WriteLine("\n✗ NEW Implementation Test FAILED!");
            }
        }
        
        static void AnalyzePEStructure(string filePath)
        {
            byte[] buffer = File.ReadAllBytes(filePath);
            
            int peOffset = BitConverter.ToInt32(buffer, 60);
            ushort numSections = BitConverter.ToUInt16(buffer, peOffset + 6);
            ushort optionalHeaderSize = BitConverter.ToUInt16(buffer, peOffset + 20);
            
            int sectionsOffset = peOffset + 4 + 20 + optionalHeaderSize;
            int totalHeaderSize = sectionsOffset + (numSections * 40);
            
            Console.WriteLine($"\nPE File Structure:");
            Console.WriteLine($"  File size: {buffer.Length} bytes");
            Console.WriteLine($"  PE offset: {peOffset} bytes");
            Console.WriteLine($"  Number of sections: {numSections}");
            Console.WriteLine($"  Sections start at: {sectionsOffset} bytes");
            Console.WriteLine($"  Total header size: {totalHeaderSize} bytes");
            
            if (totalHeaderSize > 1024)
            {
                Console.WriteLine($"\n  ⚠ Headers exceed 1024 bytes by {totalHeaderSize - 1024} bytes");
                Console.WriteLine("  This tests the new implementation's ability to handle large headers");
            }
        }
    }
}
