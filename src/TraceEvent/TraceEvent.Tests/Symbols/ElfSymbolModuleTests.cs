using System;
using System.IO;
using Microsoft.Diagnostics.Symbols;
using Xunit;
using Xunit.Abstractions;

namespace TraceEventTests
{
    public class ElfSymbolModuleTests : TestBase
    {
        public ElfSymbolModuleTests(ITestOutputHelper output)
            : base(output)
        {
        }

        #region Error handling

        [Fact]
        public void InvalidMagicBytes_ProducesNoSymbols()
        {
            byte[] data = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };
            var module = CreateModule(data, 0, 0);
            uint symbolStart = 0;
            Assert.Equal(string.Empty, module.FindNameForRva(0x1000, ref symbolStart));
        }

        [Fact]
        public void TruncatedFile_ProducesNoSymbols()
        {
            // Just the magic bytes, nothing else — too small to parse.
            byte[] data = new byte[] { 0x7f, (byte)'E', (byte)'L', (byte)'F' };
            var module = CreateModule(data, 0, 0);
            uint symbolStart = 0;
            Assert.Equal(string.Empty, module.FindNameForRva(0x1000, ref symbolStart));
        }

        [Fact]
        public void EmptyFile_ProducesNoSymbols()
        {
            var module = CreateModule(Array.Empty<byte>(), 0, 0);
            uint symbolStart = 0;
            Assert.Equal(string.Empty, module.FindNameForRva(0x1000, ref symbolStart));
        }

        [Fact]
        public void InvalidElfClass_ProducesNoSymbols()
        {
            // Build a valid header but patch ei_class to an invalid value (0xFF).
            byte[] data = new ElfBuilder()
                .Set64Bit(true)
                .AddFunction("test", 0x401000, 0x10)
                .Build();

            data[4] = 0xFF; // Corrupt ei_class.

            var module = CreateModule(data, 0x400000, 0);
            uint symbolStart = 0;
            Assert.Equal(string.Empty, module.FindNameForRva(0x1000, ref symbolStart));
        }

        [Fact]
        public void NoSectionHeaders_ProducesNoSymbols()
        {
            // Minimal valid ELF header with e_shoff = 0 (no section headers).
            byte[] data = new ElfBuilder()
                .Set64Bit(true)
                .Build(); // No symbols added, but will have section headers.

            // Patch e_shoff to 0 (bytes 40-47 in 64-bit ELF).
            for (int i = 40; i < 48; i++)
            {
                data[i] = 0;
            }

            var module = CreateModule(data, 0x400000, 0);
            uint symbolStart = 0;
            Assert.Equal(string.Empty, module.FindNameForRva(0x1000, ref symbolStart));
        }

        #endregion

        #region 64-bit Little-Endian

        [Fact]
        public void SingleSymbol_64BitLE_FindsSymbolInRange()
        {
            ulong pVaddr = 0x400000;
            var builder = new ElfBuilder()
                .Set64Bit(true)
                .SetBigEndian(false)
                .SetPTLoad(pVaddr, 0)
                .AddFunction("my_function", 0x401000, 0x100);

            byte[] data = builder.Build();
            var module = CreateModule(data, pVaddr, 0);

            uint symbolStart = 0;
            uint expectedRva = 0x401000 - (uint)pVaddr; // 0x1000

            // Hit at start of symbol.
            Assert.Equal("my_function", module.FindNameForRva(expectedRva, ref symbolStart));
            Assert.Equal(expectedRva, symbolStart);

            // Hit in middle of symbol.
            Assert.Equal("my_function", module.FindNameForRva(expectedRva + 0x80, ref symbolStart));
            Assert.Equal(expectedRva, symbolStart);

            // Hit at end of symbol (inclusive: start + size - 1).
            Assert.Equal("my_function", module.FindNameForRva(expectedRva + 0xFF, ref symbolStart));
            Assert.Equal(expectedRva, symbolStart);

            // Miss before symbol.
            Assert.Equal(string.Empty, module.FindNameForRva(expectedRva - 1, ref symbolStart));

            // Miss after symbol.
            Assert.Equal(string.Empty, module.FindNameForRva(expectedRva + 0x100, ref symbolStart));
        }

        [Fact]
        public void MultipleSymbols_64BitLE_FindsCorrectSymbols()
        {
            ulong pVaddr = 0x400000;
            var builder = new ElfBuilder()
                .Set64Bit(true)
                .SetBigEndian(false)
                .SetPTLoad(pVaddr, 0)
                .AddFunction("func_a", 0x401000, 0x50)
                .AddFunction("func_b", 0x401100, 0x80)
                .AddFunction("func_c", 0x402000, 0x200);

            byte[] data = builder.Build();
            var module = CreateModule(data, pVaddr, 0);

            uint symbolStart = 0;

            // func_a
            Assert.Equal("func_a", module.FindNameForRva(0x1000, ref symbolStart));
            Assert.Equal((uint)0x1000, symbolStart);

            // func_b
            Assert.Equal("func_b", module.FindNameForRva(0x1100, ref symbolStart));
            Assert.Equal((uint)0x1100, symbolStart);

            // func_c
            Assert.Equal("func_c", module.FindNameForRva(0x2000, ref symbolStart));
            Assert.Equal((uint)0x2000, symbolStart);

            // Gap between func_a and func_b.
            Assert.Equal(string.Empty, module.FindNameForRva(0x1050, ref symbolStart));

            // Gap between func_b and func_c.
            Assert.Equal(string.Empty, module.FindNameForRva(0x1180, ref symbolStart));

            // Before any symbol.
            Assert.Equal(string.Empty, module.FindNameForRva(0x0500, ref symbolStart));

            // After all symbols.
            Assert.Equal(string.Empty, module.FindNameForRva(0x2200, ref symbolStart));
        }

        [Fact]
        public void SymbolBoundaryExact_64BitLE()
        {
            ulong pVaddr = 0x400000;
            var builder = new ElfBuilder()
                .Set64Bit(true)
                .SetBigEndian(false)
                .SetPTLoad(pVaddr, 0)
                .AddFunction("boundary_func", 0x401000, 0x10);

            byte[] data = builder.Build();
            var module = CreateModule(data, pVaddr, 0);

            uint symbolStart = 0;
            uint rvaStart = 0x1000;
            uint rvaEnd = 0x100F; // start + size - 1

            // Exact start.
            Assert.Equal("boundary_func", module.FindNameForRva(rvaStart, ref symbolStart));
            Assert.Equal(rvaStart, symbolStart);

            // Exact end (inclusive).
            Assert.Equal("boundary_func", module.FindNameForRva(rvaEnd, ref symbolStart));
            Assert.Equal(rvaStart, symbolStart);

            // One past end.
            Assert.Equal(string.Empty, module.FindNameForRva(rvaEnd + 1, ref symbolStart));

            // One before start.
            Assert.Equal(string.Empty, module.FindNameForRva(rvaStart - 1, ref symbolStart));
        }

        #endregion

        #region 32-bit Little-Endian

        [Fact]
        public void SingleSymbol_32BitLE_FindsSymbol()
        {
            ulong pVaddr = 0x08048000;
            var builder = new ElfBuilder()
                .Set64Bit(false)
                .SetBigEndian(false)
                .SetPTLoad(pVaddr, 0)
                .AddFunction("func_32", 0x08049000, 0x40);

            byte[] data = builder.Build();
            var module = CreateModule(data, pVaddr, 0);

            uint symbolStart = 0;
            uint expectedRva = 0x08049000 - (uint)pVaddr; // 0x1000

            Assert.Equal("func_32", module.FindNameForRva(expectedRva, ref symbolStart));
            Assert.Equal(expectedRva, symbolStart);

            Assert.Equal("func_32", module.FindNameForRva(expectedRva + 0x3F, ref symbolStart));
            Assert.Equal(expectedRva, symbolStart);

            Assert.Equal(string.Empty, module.FindNameForRva(expectedRva - 1, ref symbolStart));
            Assert.Equal(string.Empty, module.FindNameForRva(expectedRva + 0x40, ref symbolStart));
        }

        [Fact]
        public void MultipleSymbols_32BitLE_FindsCorrectSymbols()
        {
            ulong pVaddr = 0x08048000;
            var builder = new ElfBuilder()
                .Set64Bit(false)
                .SetBigEndian(false)
                .SetPTLoad(pVaddr, 0)
                .AddFunction("alpha", 0x08049000, 0x20)
                .AddFunction("beta", 0x0804A000, 0x30);

            byte[] data = builder.Build();
            var module = CreateModule(data, pVaddr, 0);

            uint symbolStart = 0;

            Assert.Equal("alpha", module.FindNameForRva(0x1000, ref symbolStart));
            Assert.Equal((uint)0x1000, symbolStart);

            Assert.Equal("beta", module.FindNameForRva(0x2000, ref symbolStart));
            Assert.Equal((uint)0x2000, symbolStart);

            // Gap between.
            Assert.Equal(string.Empty, module.FindNameForRva(0x1020, ref symbolStart));
        }

        #endregion

        #region Big-Endian

        [Fact]
        public void SingleSymbol_64BitBE_FindsSymbol()
        {
            ulong pVaddr = 0x400000;
            var builder = new ElfBuilder()
                .Set64Bit(true)
                .SetBigEndian(true)
                .SetPTLoad(pVaddr, 0)
                .AddFunction("be_func_64", 0x401000, 0x80);

            byte[] data = builder.Build();
            var module = CreateModule(data, pVaddr, 0);

            uint symbolStart = 0;
            Assert.Equal("be_func_64", module.FindNameForRva(0x1000, ref symbolStart));
            Assert.Equal((uint)0x1000, symbolStart);

            Assert.Equal("be_func_64", module.FindNameForRva(0x107F, ref symbolStart));
            Assert.Equal(string.Empty, module.FindNameForRva(0x1080, ref symbolStart));
        }

        [Fact]
        public void SingleSymbol_32BitBE_FindsSymbol()
        {
            ulong pVaddr = 0x08048000;
            var builder = new ElfBuilder()
                .Set64Bit(false)
                .SetBigEndian(true)
                .SetPTLoad(pVaddr, 0)
                .AddFunction("be_func_32", 0x08049000, 0x60);

            byte[] data = builder.Build();
            var module = CreateModule(data, pVaddr, 0);

            uint symbolStart = 0;
            Assert.Equal("be_func_32", module.FindNameForRva(0x1000, ref symbolStart));
            Assert.Equal((uint)0x1000, symbolStart);

            Assert.Equal("be_func_32", module.FindNameForRva(0x105F, ref symbolStart));
            Assert.Equal(string.Empty, module.FindNameForRva(0x1060, ref symbolStart));
        }

        #endregion

        #region Symbol Filtering

        [Fact]
        public void NonFunctionSymbolsFiltered()
        {
            ulong pVaddr = 0x400000;
            byte STT_OBJECT = 1;
            byte STT_NOTYPE = 0;
            var builder = new ElfBuilder()
                .Set64Bit(true)
                .SetPTLoad(pVaddr, 0)
                .AddSymbol("object_sym", 0x401000, 0x100, STT_OBJECT)
                .AddSymbol("notype_sym", 0x402000, 0x100, STT_NOTYPE)
                .AddFunction("real_func", 0x403000, 0x100);

            byte[] data = builder.Build();
            var module = CreateModule(data, pVaddr, 0);

            uint symbolStart = 0;

            // Non-function symbols should not be found.
            Assert.Equal(string.Empty, module.FindNameForRva(0x1000, ref symbolStart));
            Assert.Equal(string.Empty, module.FindNameForRva(0x2000, ref symbolStart));

            // The function symbol should be found.
            Assert.Equal("real_func", module.FindNameForRva(0x3000, ref symbolStart));
            Assert.Equal((uint)0x3000, symbolStart);
        }

        [Fact]
        public void ZeroValueSymbolFiltered()
        {
            ulong pVaddr = 0x400000;
            var builder = new ElfBuilder()
                .Set64Bit(true)
                .SetPTLoad(pVaddr, 0)
                .AddFunction("zero_val", 0, 0x100) // st_value == 0
                .AddFunction("good_func", 0x401000, 0x100);

            byte[] data = builder.Build();
            var module = CreateModule(data, pVaddr, 0);

            uint symbolStart = 0;

            // Zero-value symbol is filtered.
            Assert.Equal(string.Empty, module.FindNameForRva(0, ref symbolStart));

            // Valid symbol works.
            Assert.Equal("good_func", module.FindNameForRva(0x1000, ref symbolStart));
        }

        [Fact]
        public void ZeroSizeSymbolFiltered()
        {
            ulong pVaddr = 0x400000;
            var builder = new ElfBuilder()
                .Set64Bit(true)
                .SetPTLoad(pVaddr, 0)
                .AddFunction("zero_size", 0x401000, 0) // st_size == 0
                .AddFunction("good_func", 0x402000, 0x50);

            byte[] data = builder.Build();
            var module = CreateModule(data, pVaddr, 0);

            uint symbolStart = 0;

            // Zero-size symbol is filtered.
            Assert.Equal(string.Empty, module.FindNameForRva(0x1000, ref symbolStart));

            // Valid symbol works.
            Assert.Equal("good_func", module.FindNameForRva(0x2000, ref symbolStart));
        }

        [Fact]
        public void SymbolBelowPTLoadBaseFiltered()
        {
            ulong pVaddr = 0x400000;
            var builder = new ElfBuilder()
                .Set64Bit(true)
                .SetPTLoad(pVaddr, 0)
                .AddFunction("below_base", 0x100000, 0x100) // Below pVaddr
                .AddFunction("good_func", 0x401000, 0x50);

            byte[] data = builder.Build();
            var module = CreateModule(data, pVaddr, 0);

            uint symbolStart = 0;

            // Symbol below PT_LOAD base should not appear.
            Assert.Equal(string.Empty, module.FindNameForRva(0x100000, ref symbolStart));

            // Valid symbol works.
            Assert.Equal("good_func", module.FindNameForRva(0x1000, ref symbolStart));
        }

        #endregion

        #region RVA Adjustment

        [Fact]
        public void RvaAdjustedByPTLoad()
        {
            // Use a non-trivial pVaddr to verify the RVA = (st_value - pVaddr) + pOffset adjustment.
            ulong pVaddr = 0x200000;
            ulong pOffset = 0;
            var builder = new ElfBuilder()
                .Set64Bit(true)
                .SetPTLoad(pVaddr, pOffset)
                .AddFunction("adjusted", 0x205000, 0x100);

            byte[] data = builder.Build();
            var module = CreateModule(data, pVaddr, pOffset);

            uint symbolStart = 0;
            uint expectedRva = (uint)(0x205000 - pVaddr + pOffset); // 0x5000

            Assert.Equal("adjusted", module.FindNameForRva(expectedRva, ref symbolStart));
            Assert.Equal(expectedRva, symbolStart);

            // Using the raw virtual address as RVA should NOT match.
            Assert.Equal(string.Empty, module.FindNameForRva(0x205000, ref symbolStart));
        }

        [Fact]
        public void NonZeroPOffset_AdjustsRvaCorrectly()
        {
            // When pOffset is non-zero, the RVA formula is (st_value - pVaddr) + pOffset.
            ulong pVaddr = 0x400000;
            ulong pOffset = 0x1000;
            var builder = new ElfBuilder()
                .Set64Bit(true)
                .SetPTLoad(pVaddr, pOffset)
                .AddFunction("offset_func", 0x401000, 0x50);

            byte[] data = builder.Build();
            var module = CreateModule(data, pVaddr, pOffset);

            uint symbolStart = 0;
            // RVA = (0x401000 - 0x400000) + 0x1000 = 0x2000
            uint expectedRva = (uint)(0x401000 - pVaddr + pOffset);
            Assert.Equal((uint)0x2000, expectedRva);

            Assert.Equal("offset_func", module.FindNameForRva(expectedRva, ref symbolStart));
            Assert.Equal(expectedRva, symbolStart);

            // Without pOffset adjustment (0x1000) should NOT match.
            Assert.Equal(string.Empty, module.FindNameForRva(0x1000, ref symbolStart));
        }

        #endregion

        #region Demangling Integration

        [Fact]
        public void ItaniumCppSymbolDemangled()
        {
            ulong pVaddr = 0x400000;
            // _Z3foov demangles to "foo()"
            var builder = new ElfBuilder()
                .Set64Bit(true)
                .SetPTLoad(pVaddr, 0)
                .AddFunction("_Z3foov", 0x401000, 0x20);

            byte[] data = builder.Build();
            var module = CreateModule(data, pVaddr, 0);

            uint symbolStart = 0;
            string name = module.FindNameForRva(0x1000, ref symbolStart);
            Assert.Equal("foo()", name);
        }

        [Fact]
        public void RustV0SymbolDemangled()
        {
            ulong pVaddr = 0x400000;
            // _RNvC5hello4main demangles to "hello::main"
            var builder = new ElfBuilder()
                .Set64Bit(true)
                .SetPTLoad(pVaddr, 0)
                .AddFunction("_RNvC5hello4main", 0x401000, 0x20);

            byte[] data = builder.Build();
            var module = CreateModule(data, pVaddr, 0);

            uint symbolStart = 0;
            string name = module.FindNameForRva(0x1000, ref symbolStart);
            Assert.Equal("hello::main", name);
        }

        [Fact]
        public void PlainSymbolPassedThrough()
        {
            ulong pVaddr = 0x400000;
            var builder = new ElfBuilder()
                .Set64Bit(true)
                .SetPTLoad(pVaddr, 0)
                .AddFunction("plain_c_function", 0x401000, 0x20);

            byte[] data = builder.Build();
            var module = CreateModule(data, pVaddr, 0);

            uint symbolStart = 0;
            string name = module.FindNameForRva(0x1000, ref symbolStart);
            Assert.Equal("plain_c_function", name);
        }

        #endregion

        #region .dynsym Section

        [Fact]
        public void DynsymSymbolsParsed()
        {
            ulong pVaddr = 0x400000;
            var builder = new ElfBuilder()
                .Set64Bit(true)
                .SetPTLoad(pVaddr, 0)
                .AddFunction("static_func", 0x401000, 0x50)
                .AddDynFunction("dynamic_func", 0x402000, 0x80);

            byte[] data = builder.Build();
            var module = CreateModule(data, pVaddr, 0);

            uint symbolStart = 0;

            // Symbol from .symtab.
            Assert.Equal("static_func", module.FindNameForRva(0x1000, ref symbolStart));
            Assert.Equal((uint)0x1000, symbolStart);

            // Symbol from .dynsym.
            Assert.Equal("dynamic_func", module.FindNameForRva(0x2000, ref symbolStart));
            Assert.Equal((uint)0x2000, symbolStart);
        }

        #endregion

        #region Edge Cases

        [Fact]
        public void EmptySymbolTable_FindReturnsEmpty()
        {
            ulong pVaddr = 0x400000;
            // Build with no symbols at all — the .symtab section will exist but contain only the null entry.
            var builder = new ElfBuilder()
                .Set64Bit(true)
                .SetPTLoad(pVaddr, 0);

            byte[] data = builder.Build();
            var module = CreateModule(data, pVaddr, 0);

            uint symbolStart = 0;
            Assert.Equal(string.Empty, module.FindNameForRva(0x1000, ref symbolStart));
        }

        [Fact]
        public void FindNameForRva_AtRvaZero_ReturnsEmpty()
        {
            ulong pVaddr = 0x400000;
            var builder = new ElfBuilder()
                .Set64Bit(true)
                .SetPTLoad(pVaddr, 0)
                .AddFunction("some_func", 0x401000, 0x50);

            byte[] data = builder.Build();
            var module = CreateModule(data, pVaddr, 0);

            uint symbolStart = 0;
            Assert.Equal(string.Empty, module.FindNameForRva(0, ref symbolStart));
        }

        [Fact]
        public void ManySymbols_BinarySearchWorksCorrectly()
        {
            // Stress the binary search with many symbols.
            ulong pVaddr = 0x400000;
            var builder = new ElfBuilder()
                .Set64Bit(true)
                .SetPTLoad(pVaddr, 0);

            int count = 100;
            for (int i = 0; i < count; i++)
            {
                // Each function is 0x10 bytes, spaced 0x100 apart.
                ulong addr = 0x401000 + (ulong)(i * 0x100);
                builder.AddFunction("func_" + i, addr, 0x10);
            }

            byte[] data = builder.Build();
            var module = CreateModule(data, pVaddr, 0);

            uint symbolStart = 0;

            // Verify each symbol can be found.
            for (int i = 0; i < count; i++)
            {
                uint rva = (uint)(0x1000 + i * 0x100);
                string name = module.FindNameForRva(rva, ref symbolStart);
                Assert.Equal("func_" + i, name);
                Assert.Equal(rva, symbolStart);
            }

            // Verify gaps between symbols.
            for (int i = 0; i < count; i++)
            {
                uint gapRva = (uint)(0x1000 + i * 0x100 + 0x10); // Just past each symbol.
                Assert.Equal(string.Empty, module.FindNameForRva(gapRva, ref symbolStart));
            }
        }

        #endregion

        #region File Path Constructor

        [Fact]
        public void FilePathConstructor_LoadsSymbols()
        {
            ulong pVaddr = 0x400000;
            var builder = new ElfBuilder()
                .Set64Bit(true)
                .SetPTLoad(pVaddr, 0)
                .AddFunction("file_func_a", 0x401000, 0x50)
                .AddFunction("file_func_b", 0x402000, 0x80);

            byte[] data = builder.Build();

            RunWithTempFile(data, (path) =>
            {
                var module = new ElfSymbolModule(path, pVaddr, 0);

                uint symbolStart = 0;
                Assert.Equal("file_func_a", module.FindNameForRva(0x1000, ref symbolStart));
                Assert.Equal((uint)0x1000, symbolStart);

                Assert.Equal("file_func_b", module.FindNameForRva(0x2000, ref symbolStart));
                Assert.Equal((uint)0x2000, symbolStart);

                Assert.Equal(string.Empty, module.FindNameForRva(0x3000, ref symbolStart));
            });
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Creates an ElfSymbolModule from a byte array. The stream constructor disposes the stream.
        /// </summary>
        private static ElfSymbolModule CreateModule(byte[] data, ulong pVaddr, ulong pOffset)
        {
            return new ElfSymbolModule(new MemoryStream(data), pVaddr, pOffset);
        }

        /// <summary>
        /// Writes data to a temp file, invokes the action, then cleans up.
        /// </summary>
        private static void RunWithTempFile(byte[] data, Action<string> action)
        {
            string tempPath = Path.GetTempFileName();
            try
            {
                File.WriteAllBytes(tempPath, data);
                action(tempPath);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        #endregion
    }
}
