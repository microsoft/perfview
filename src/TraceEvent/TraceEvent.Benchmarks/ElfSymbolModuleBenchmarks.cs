using System.IO;
using BenchmarkDotNet.Attributes;
using Microsoft.Diagnostics.Symbols;
using TraceEventTests;

namespace TraceEventBenchmarks
{
    /// <summary>
    /// Benchmarks for ELF symbol parsing (construction) and lookup (FindNameForRva).
    /// Uses ElfBuilder to generate synthetic ELF binaries in-memory with demangling
    /// disabled to isolate parser performance.
    /// </summary>
    [MemoryDiagnoser]
    public class ElfSymbolModuleBenchmarks
    {
        private const ulong PVaddr = 0x400000;
        private const ulong POffset = 0;

        private byte[] m_smallBytes;    // ~5 symbols
        private byte[] m_mediumBytes;   // ~256 symbols
        private byte[] m_largeBytes;    // ~10000 symbols

        // Pre-parsed modules for lookup benchmarks.
        private ElfSymbolModule m_smallModule;
        private ElfSymbolModule m_mediumModule;
        private ElfSymbolModule m_largeModule;

        // RVAs known to hit symbols.
        private uint m_smallLookupRva;
        private uint m_mediumLookupRva;
        private uint m_largeLookupRva;

        [GlobalSetup]
        public void Setup()
        {
            m_smallBytes = BuildElf(5);
            m_mediumBytes = BuildElf(256);
            m_largeBytes = BuildElf(10000);

            m_smallModule = CreateModule(m_smallBytes);
            m_mediumModule = CreateModule(m_mediumBytes);
            m_largeModule = CreateModule(m_largeBytes);

            // Middle of the symbol range — guaranteed to hit.
            m_smallLookupRva = 2 * 0x100;       // 3rd symbol
            m_mediumLookupRva = 128 * 0x100;     // 129th symbol
            m_largeLookupRva = 5000 * 0x100;     // 5001st symbol
        }

        #region Parse benchmarks

        [Benchmark]
        public void ParseSmall()
        {
            CreateModule(m_smallBytes);
        }

        [Benchmark]
        public void ParseMedium()
        {
            CreateModule(m_mediumBytes);
        }

        [Benchmark]
        public void ParseLarge()
        {
            CreateModule(m_largeBytes);
        }

        #endregion

        #region Lookup benchmarks

        [Benchmark]
        public string LookupSmall()
        {
            uint symbolStart = 0;
            return m_smallModule.FindNameForRva(m_smallLookupRva, ref symbolStart);
        }

        [Benchmark]
        public string LookupMedium()
        {
            uint symbolStart = 0;
            return m_mediumModule.FindNameForRva(m_mediumLookupRva, ref symbolStart);
        }

        [Benchmark]
        public string LookupLarge()
        {
            uint symbolStart = 0;
            return m_largeModule.FindNameForRva(m_largeLookupRva, ref symbolStart);
        }

        #endregion

        /// <summary>
        /// Builds a synthetic 64-bit LE ELF with the given number of STT_FUNC symbols.
        /// Each function is 0x80 bytes, spaced 0x100 apart starting at PVaddr + 0.
        /// </summary>
        private static byte[] BuildElf(int symbolCount)
        {
            var builder = new ElfBuilder()
                .Set64Bit(true)
                .SetBigEndian(false)
                .SetPTLoad(PVaddr, POffset);

            for (int i = 0; i < symbolCount; i++)
            {
                ulong addr = PVaddr + (ulong)(i * 0x100);
                builder.AddFunction("func_" + i, addr, 0x80);
            }

            return builder.Build();
        }

        private static ElfSymbolModule CreateModule(byte[] data)
        {
            return new ElfSymbolModule(new MemoryStream(data), PVaddr, POffset, leaveOpen: false, demangle: false);
        }
    }
}
