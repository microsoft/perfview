using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing.Etlx;
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

        /// <summary>
        /// Regression test for the libcoreclr.so bug where p_vaddr != p_offset.
        /// In the real trace: p_vaddr=0x1c9060, p_offset=0x1c8060, pageSize=4096.
        /// The Linux loader maps at PAGE_DOWN(p_vaddr) = 0x1c9000.
        /// The caller (OpenElfSymbolsForModuleFile) page-aligns pVaddr and passes
        /// the actual pOffset. LookupSymbolsForModule adds pOffset to
        /// (address - ImageBase) so that the lookup RVA matches the ElfSymbolModule
        /// formula (st_value - alignedPVaddr) + pOffset.
        /// </summary>
        [Fact]
        public void NonPageAlignedPVaddr_CallerPageAligns()
        {
            // These values are from a real libcoreclr.so trace.
            ulong rawPVaddr = 0x1c9060;
            ulong rawPOffset = 0x1c8060;
            // Note: rawPOffset (0x1c8060) differs from rawPVaddr — this is the root cause of the bug.
            ulong pageSize = 4096;

            // The caller (OpenElfSymbolsForModuleFile) page-aligns before passing to ElfSymbolModule.
            ulong alignedPVaddr = rawPVaddr & ~(pageSize - 1); // 0x1c9000
            Assert.Equal((ulong)0x1c9000, alignedPVaddr);

            // Symbol at virtual address 0x1D0000 (inside the executable segment).
            ulong symbolAddr = 0x1D0000;
            ulong symbolSize = 0x100;

            var builder = new ElfBuilder()
                .Set64Bit(true)
                .SetPTLoad(alignedPVaddr, rawPOffset)
                .AddFunction("coreclr_execute_assembly", symbolAddr, symbolSize);

            byte[] data = builder.Build();

            // The caller passes (alignedPVaddr, rawPOffset) — the actual p_offset.
            var module = CreateModule(data, alignedPVaddr, rawPOffset);

            uint symbolStart = 0;
            // The ElfSymbolModule RVA formula: (st_value - pVaddr) + pOffset
            // = (0x1D0000 - 0x1c9000) + 0x1c8060 = 0x7000 + 0x1c8060 = 0x1CF060
            // The caller (LookupSymbolsForModule) computes: (address - ImageBase) + pOffset
            // = (st_value - alignedPVaddr) + pOffset — same value.
            uint lookupRva = (uint)(symbolAddr - alignedPVaddr + rawPOffset);
            Assert.Equal("coreclr_execute_assembly", module.FindNameForRva(lookupRva, ref symbolStart));
            Assert.Equal(lookupRva, symbolStart);
        }

        /// <summary>
        /// Verifies that ElfSymbolInfo.PageAlignedVirtualAddress correctly page-aligns p_vaddr.
        /// </summary>
        [Fact]
        public void ElfSymbolInfo_PageAlignedVirtualAddress()
        {
            var info = new Microsoft.Diagnostics.Tracing.Etlx.ElfSymbolInfo();

            // With page size set, non-aligned p_vaddr gets aligned.
            info.VirtualAddress = 0x1c9060;
            info.PageSize = 4096;
            Assert.Equal((ulong)0x1c9000, info.PageAlignedVirtualAddress);

            // Already-aligned p_vaddr stays the same.
            info.VirtualAddress = 0x400000;
            info.PageSize = 4096;
            Assert.Equal((ulong)0x400000, info.PageAlignedVirtualAddress);

            // PageSize=0 (unknown) returns raw VirtualAddress.
            info.VirtualAddress = 0x1c9060;
            info.PageSize = 0;
            Assert.Equal((ulong)0x1c9060, info.PageAlignedVirtualAddress);

            // 64K pages (ARM64).
            info.VirtualAddress = 0x1c9060;
            info.PageSize = 65536;
            Assert.Equal((ulong)0x1c0000, info.PageAlignedVirtualAddress);
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

        #region ReadBuildId

        [Fact]
        public void ReadBuildId_ValidElf64_ReturnsBuildId()
        {
            byte[] buildId = new byte[] { 0xab, 0xcd, 0xef, 0x01, 0x23, 0x45, 0x67, 0x89,
                                           0xab, 0xcd, 0xef, 0x01, 0x23, 0x45, 0x67, 0x89,
                                           0xab, 0xcd, 0xef, 0x01 };
            var builder = new ElfBuilder()
                .Set64Bit(true)
                .SetPTLoad(0x400000, 0)
                .SetBuildId(buildId);

            byte[] data = builder.Build();
            RunWithTempFile(data, (path) =>
            {
                string result = ElfSymbolModule.ReadBuildId(path);
                Assert.Equal("abcdef0123456789abcdef0123456789abcdef01", result);
            });
        }

        [Fact]
        public void ReadBuildId_ValidElf32_ReturnsBuildId()
        {
            byte[] buildId = new byte[] { 0xde, 0xad, 0xbe, 0xef };
            var builder = new ElfBuilder()
                .Set64Bit(false)
                .SetPTLoad(0x400000, 0)
                .SetBuildId(buildId);

            byte[] data = builder.Build();
            RunWithTempFile(data, (path) =>
            {
                string result = ElfSymbolModule.ReadBuildId(path);
                Assert.Equal("deadbeef", result);
            });
        }

        [Fact]
        public void ReadBuildId_NoBuildId_ReturnsNull()
        {
            // ELF with no build-id note (no program headers).
            var builder = new ElfBuilder()
                .Set64Bit(true)
                .SetPTLoad(0x400000, 0)
                .AddFunction("test", 0x401000, 0x100);

            byte[] data = builder.Build();
            RunWithTempFile(data, (path) =>
            {
                string result = ElfSymbolModule.ReadBuildId(path);
                Assert.Null(result);
            });
        }

        [Fact]
        public void ReadBuildId_NotElfFile_ReturnsNull()
        {
            byte[] data = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07 };
            RunWithTempFile(data, (path) =>
            {
                string result = ElfSymbolModule.ReadBuildId(path);
                Assert.Null(result);
            });
        }

        [Fact]
        public void ReadBuildId_EmptyFile_ReturnsNull()
        {
            RunWithTempFile(Array.Empty<byte>(), (path) =>
            {
                string result = ElfSymbolModule.ReadBuildId(path);
                Assert.Null(result);
            });
        }

        [Fact]
        public void ReadBuildId_NonExistentFile_ReturnsNull()
        {
            string result = ElfSymbolModule.ReadBuildId(@"C:\nonexistent\path\fake.so");
            Assert.Null(result);
        }

        [Fact]
        public void ReadBuildId_BigEndianElf64_ReturnsBuildId()
        {
            byte[] buildId = new byte[] { 0x11, 0x22, 0x33, 0x44 };
            var builder = new ElfBuilder()
                .Set64Bit(true)
                .SetBigEndian(true)
                .SetPTLoad(0x400000, 0)
                .SetBuildId(buildId);

            byte[] data = builder.Build();
            RunWithTempFile(data, (path) =>
            {
                string result = ElfSymbolModule.ReadBuildId(path);
                Assert.Equal("11223344", result);
            });
        }

        #endregion

        #region MatchOrInit tests

        [Fact]
        public void MatchOrInitPE_WhenNull_CreatesPESymbolInfo()
        {
            var moduleFile = new TraceModuleFile(null, 0, (ModuleFileIndex)0);
            var pe = moduleFile.MatchOrInitPE();
            Assert.NotNull(pe);
            Assert.IsType<PESymbolInfo>(pe);
            Assert.Equal(ModuleBinaryFormat.PE, moduleFile.BinaryFormat);
        }

        [Fact]
        public void MatchOrInitPE_WhenAlreadyPE_ReturnsSame()
        {
            var moduleFile = new TraceModuleFile(null, 0, (ModuleFileIndex)0);
            var pe1 = moduleFile.MatchOrInitPE();
            var pe2 = moduleFile.MatchOrInitPE();
            Assert.Same(pe1, pe2);
        }

        [Fact]
        public void MatchOrInitPE_WhenElf_ReturnsNull()
        {
            var moduleFile = new TraceModuleFile(null, 0, (ModuleFileIndex)0);
            moduleFile.MatchOrInitElf(); // Set as ELF first

            // Suppress Debug.Assert so we can verify the return value.
            var listeners = new TraceListener[Trace.Listeners.Count];
            Trace.Listeners.CopyTo(listeners, 0);
            Trace.Listeners.Clear();
            try
            {
                var pe = moduleFile.MatchOrInitPE();
                Assert.Null(pe);
            }
            finally
            {
                Trace.Listeners.AddRange(listeners);
            }
        }

        [Fact]
        public void MatchOrInitElf_WhenNull_CreatesElfSymbolInfo()
        {
            var moduleFile = new TraceModuleFile(null, 0, (ModuleFileIndex)0);
            var elf = moduleFile.MatchOrInitElf();
            Assert.NotNull(elf);
            Assert.IsType<ElfSymbolInfo>(elf);
            Assert.Equal(ModuleBinaryFormat.ELF, moduleFile.BinaryFormat);
        }

        [Fact]
        public void MatchOrInitElf_WhenAlreadyElf_ReturnsSame()
        {
            var moduleFile = new TraceModuleFile(null, 0, (ModuleFileIndex)0);
            var elf1 = moduleFile.MatchOrInitElf();
            var elf2 = moduleFile.MatchOrInitElf();
            Assert.Same(elf1, elf2);
        }

        [Fact]
        public void MatchOrInitElf_WhenPE_ReturnsNull()
        {
            var moduleFile = new TraceModuleFile(null, 0, (ModuleFileIndex)0);
            moduleFile.MatchOrInitPE(); // Set as PE first

            // Suppress Debug.Assert so we can verify the return value.
            var listeners = new TraceListener[Trace.Listeners.Count];
            Trace.Listeners.CopyTo(listeners, 0);
            Trace.Listeners.Clear();
            try
            {
                var elf = moduleFile.MatchOrInitElf();
                Assert.Null(elf);
            }
            finally
            {
                Trace.Listeners.AddRange(listeners);
            }
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
