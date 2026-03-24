using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Microsoft.Diagnostics.Symbols
{
    /// <summary>
    /// Reads ELF (Executable and Linkable Format) files and resolves RVAs to symbol names.
    /// Supports both 32-bit and 64-bit ELF, and parses both .symtab and .dynsym sections.
    /// </summary>
    internal class ElfSymbolModule : ISymbolLookup
    {
        /// <summary>
        /// Opens an ELF file and loads its symbol tables.
        /// </summary>
        /// <param name="filePath">Path to the ELF file (binary or .debug file).</param>
        /// <param name="pVaddr">Virtual address of first executable PT_LOAD segment (from trace metadata).</param>
        /// <param name="pOffset">File offset of first executable PT_LOAD segment (from trace metadata).</param>
        public ElfSymbolModule(string filePath, ulong pVaddr, ulong pOffset)
            : this(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read), pVaddr, pOffset)
        {
        }

        /// <summary>
        /// Opens an ELF stream and loads its symbol tables.
        /// </summary>
        /// <param name="stream">Stream containing ELF data.</param>
        /// <param name="pVaddr">Virtual address of first executable PT_LOAD segment (from trace metadata).</param>
        /// <param name="pOffset">File offset of first executable PT_LOAD segment (from trace metadata).</param>
        /// <param name="leaveOpen">If false (default), the stream is disposed after parsing.</param>
        /// <param name="demangle">If true (default), symbol names are demangled; if false, raw mangled names are kept.</param>
        internal ElfSymbolModule(Stream stream, ulong pVaddr, ulong pOffset, bool leaveOpen = false, bool demangle = true)
        {
            m_symbols = new List<ElfSymbolEntry>();
            m_pVaddr = pVaddr;
            m_pOffset = pOffset;
            m_demangle = demangle;

            try
            {
                ParseElf(stream);
            }
            finally
            {
                if (!leaveOpen)
                {
                    stream.Dispose();
                }
            }
        }

        /// <summary>
        /// Finds the symbol name for a given RVA.
        /// RVA = (sampleAddress - moduleImageBase).
        /// </summary>
        /// <param name="rva">The relative virtual address to look up.</param>
        /// <param name="symbolStart">Set to the start RVA of the matching symbol if found.</param>
        /// <returns>The symbol name, or null if no matching symbol is found.</returns>
        public string FindNameForRva(uint rva, ref uint symbolStart)
        {
            if (m_symbols.Count == 0)
            {
                return string.Empty;
            }

            // Binary search for the last symbol with Start <= rva.
            int lo = 0, hi = m_symbols.Count - 1;
            while (lo <= hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (m_symbols[mid].Start <= rva)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            // hi now points to the last symbol with Start <= rva.
            if (hi >= 0 && rva <= m_symbols[hi].End)
            {
                symbolStart = m_symbols[hi].Start;

                // Lazy name decode on first access.
                if (m_symbols[hi].Name == null)
                {
                    string name = ReadNullTerminatedString(m_strtab, m_symbols[hi].StrtabOffset);
                    name = TryDemangle(name);
                    var entry = m_symbols[hi];
                    entry.Name = name;
                    m_symbols[hi] = entry;
                }

                return m_symbols[hi].Name;
            }

            return string.Empty;
        }

        /// <summary>
        /// Reads the GNU build-id from an ELF file by scanning PT_NOTE program headers.
        /// Uses program headers (not section headers) because they are always present
        /// even when section headers have been stripped.
        /// </summary>
        /// <param name="filePath">Path to the ELF file.</param>
        /// <returns>Lowercase hex string of the build-id, or null if not found or on any error.</returns>
        internal static string ReadBuildId(string filePath)
        {
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    // Read the ELF header (max 64 bytes for 64-bit).
                    byte[] header = new byte[Elf64EhdrSize];
                    int headerRead = ReadFully(stream, header, 0, header.Length);
                    if (headerRead < EI_NIDENT)
                    {
                        Debug.WriteLine("ReadBuildId: File too small.");
                        return null;
                    }

                    // Verify ELF magic bytes: 0x7f 'E' 'L' 'F'.
                    if (header[0] != ElfMagic0 || header[1] != ElfMagic1 || header[2] != ElfMagic2 || header[3] != ElfMagic3)
                    {
                        Debug.WriteLine("ReadBuildId: Invalid ELF magic.");
                        return null;
                    }

                    byte eiClass = header[EI_CLASS];
                    byte eiData = header[EI_DATA];

                    bool is64Bit = (eiClass == ElfClass64);
                    bool bigEndian = (eiData == ElfDataMsb);

                    if (eiClass != ElfClass32 && eiClass != ElfClass64)
                    {
                        Debug.WriteLine("ReadBuildId: Unknown ELF class " + eiClass + ".");
                        return null;
                    }

                    int ehSize = is64Bit ? Elf64EhdrSize : Elf32EhdrSize;
                    if (headerRead < ehSize)
                    {
                        Debug.WriteLine("ReadBuildId: Header too small.");
                        return null;
                    }

                    // Parse program header table location from ELF header.
                    // Layout after e_ident(16): e_type(2), e_machine(2), e_version(4), e_entry(4/8), e_phoff(4/8).
                    int pos = EI_NIDENT + 2 + 2 + 4; // skip e_ident, e_type, e_machine, e_version
                    ulong ePhoff;
                    if (is64Bit)
                    {
                        pos += 8; // skip e_entry
                        ePhoff = ReadU64Static(header, pos, bigEndian); pos += 8;
                    }
                    else
                    {
                        pos += 4; // skip e_entry
                        ePhoff = ReadU32Static(header, pos, bigEndian); pos += 4;
                    }

                    // Skip to e_phentsize and e_phnum.
                    // After e_phoff: e_shoff(4/8), e_flags(4), e_ehsize(2).
                    if (is64Bit)
                    {
                        pos += 8 + 4 + 2; // e_shoff(8), e_flags(4), e_ehsize(2)
                    }
                    else
                    {
                        pos += 4 + 4 + 2; // e_shoff(4), e_flags(4), e_ehsize(2)
                    }

                    ushort ePhentsize = ReadU16Static(header, pos, bigEndian); pos += 2;
                    ushort ePhnum = ReadU16Static(header, pos, bigEndian);

                    if (ePhoff == 0 || ePhentsize == 0 || ePhnum == 0)
                    {
                        Debug.WriteLine("ReadBuildId: No program headers found.");
                        return null;
                    }

                    // Validate minimum program header entry size to avoid out-of-bounds reads.
                    int minPhentsize = is64Bit ? 56 : 32; // Elf64_Phdr = 56 bytes, Elf32_Phdr = 32 bytes
                    if (ePhentsize < minPhentsize)
                    {
                        Debug.WriteLine("ReadBuildId: ePhentsize too small: " + ePhentsize);
                        return null;
                    }

                    // Guard against corrupt ELF headers with unreasonably large program header counts.
                    if (ePhnum > 4096)
                    {
                        Debug.WriteLine("ReadBuildId: Program header count too large: " + ePhnum);
                        return null;
                    }

                    // Read all program headers in one bulk read.
                    int phTableSize = ePhnum * ePhentsize;
                    byte[] phTable = new byte[phTableSize];
                    stream.Seek((long)ePhoff, SeekOrigin.Begin);
                    if (ReadFully(stream, phTable, 0, phTableSize) < phTableSize)
                    {
                        Debug.WriteLine("ReadBuildId: Could not read program headers.");
                        return null;
                    }

                    // Iterate program headers looking for PT_NOTE segments.
                    for (int i = 0; i < ePhnum; i++)
                    {
                        int phPos = i * ePhentsize;
                        uint pType = ReadU32Static(phTable, phPos, bigEndian);

                        if (pType != PT_NOTE)
                        {
                            continue;
                        }

                        // Parse p_offset and p_filesz from the program header.
                        ulong pOffset, pFilesz;
                        if (is64Bit)
                        {
                            // 64-bit: p_type(4) + p_flags(4) + p_offset(8) + p_vaddr(8) + p_paddr(8) + p_filesz(8).
                            pOffset = ReadU64Static(phTable, phPos + 8, bigEndian);
                            pFilesz = ReadU64Static(phTable, phPos + 8 + 8 + 8 + 8, bigEndian);
                        }
                        else
                        {
                            // 32-bit: p_type(4) + p_offset(4) + p_vaddr(4) + p_paddr(4) + p_filesz(4).
                            pOffset = ReadU32Static(phTable, phPos + 4, bigEndian);
                            pFilesz = ReadU32Static(phTable, phPos + 4 + 4 + 4 + 4, bigEndian);
                        }

                        if (pFilesz == 0 || pFilesz > int.MaxValue)
                        {
                            continue;
                        }

                        // Read the PT_NOTE segment data.
                        byte[] noteData = new byte[(int)pFilesz];
                        stream.Seek((long)pOffset, SeekOrigin.Begin);
                        if (ReadFully(stream, noteData, 0, noteData.Length) < noteData.Length)
                        {
                            continue;
                        }

                        // Iterate notes within the segment looking for GNU build-id.
                        string buildId = ExtractBuildId(noteData, bigEndian);
                        if (buildId != null)
                        {
                            return buildId;
                        }
                    }

                    Debug.WriteLine("ReadBuildId: No GNU build-id note found.");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ReadBuildId: Error reading file: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Reads the .gnu_debuglink section from an ELF file and returns the debug file name.
        /// The .gnu_debuglink section contains a null-terminated filename followed by padding
        /// and a CRC32 checksum. Only the filename is returned (CRC is not validated, matching
        /// one-collect behavior).
        /// </summary>
        /// <param name="filePath">Path to the ELF file.</param>
        /// <returns>The debug link filename (e.g. "libcoreclr.so.dbg"), or null if not found or on any error.</returns>
        internal static string ReadDebugLink(string filePath)
        {
            try
            {
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    // Read the ELF header (max 64 bytes for 64-bit).
                    byte[] header = new byte[Elf64EhdrSize];
                    int headerRead = ReadFully(stream, header, 0, header.Length);
                    if (headerRead < EI_NIDENT)
                    {
                        Debug.WriteLine("ReadDebugLink: File too small.");
                        return null;
                    }

                    // Verify ELF magic bytes: 0x7f 'E' 'L' 'F'.
                    if (header[0] != ElfMagic0 || header[1] != ElfMagic1 || header[2] != ElfMagic2 || header[3] != ElfMagic3)
                    {
                        Debug.WriteLine("ReadDebugLink: Invalid ELF magic.");
                        return null;
                    }

                    byte eiClass = header[EI_CLASS];
                    byte eiData = header[EI_DATA];

                    bool is64Bit = (eiClass == ElfClass64);
                    bool bigEndian = (eiData == ElfDataMsb);

                    if (eiClass != ElfClass32 && eiClass != ElfClass64)
                    {
                        Debug.WriteLine("ReadDebugLink: Unknown ELF class " + eiClass + ".");
                        return null;
                    }

                    int ehSize = is64Bit ? Elf64EhdrSize : Elf32EhdrSize;
                    if (headerRead < ehSize)
                    {
                        Debug.WriteLine("ReadDebugLink: Header too small.");
                        return null;
                    }

                    // Parse section header table location from ELF header.
                    // Layout after e_ident(16): e_type(2), e_machine(2), e_version(4),
                    //   e_entry(4/8), e_phoff(4/8), e_shoff(4/8), e_flags(4), e_ehsize(2),
                    //   e_phentsize(2), e_phnum(2), e_shentsize(2), e_shnum(2), e_shstrndx(2).
                    int pos = EI_NIDENT + 2 + 2 + 4; // skip e_ident, e_type, e_machine, e_version
                    ulong eShoff;
                    if (is64Bit)
                    {
                        pos += 8 + 8; // skip e_entry(8), e_phoff(8)
                        eShoff = ReadU64Static(header, pos, bigEndian); pos += 8;
                    }
                    else
                    {
                        pos += 4 + 4; // skip e_entry(4), e_phoff(4)
                        eShoff = ReadU32Static(header, pos, bigEndian); pos += 4;
                    }

                    pos += 4 + 2 + 2 + 2; // e_flags(4), e_ehsize(2), e_phentsize(2), e_phnum(2)
                    ushort eShentsize = ReadU16Static(header, pos, bigEndian); pos += 2;
                    ushort eShnum = ReadU16Static(header, pos, bigEndian); pos += 2;
                    ushort eShstrndx = ReadU16Static(header, pos, bigEndian);

                    if (eShoff == 0 || eShentsize == 0 || eShnum == 0)
                    {
                        Debug.WriteLine("ReadDebugLink: No section headers found.");
                        return null;
                    }

                    int minShentsize = is64Bit ? Elf64ShdrSize : Elf32ShdrSize;
                    if (eShentsize < minShentsize || eShentsize > MaxShentsize)
                    {
                        Debug.WriteLine("ReadDebugLink: Invalid section header entry size: " + eShentsize);
                        return null;
                    }

                    if (eShnum > MaxSectionCount)
                    {
                        Debug.WriteLine("ReadDebugLink: Section count too large: " + eShnum);
                        return null;
                    }

                    if (eShstrndx >= eShnum)
                    {
                        Debug.WriteLine("ReadDebugLink: Invalid shstrndx: " + eShstrndx);
                        return null;
                    }

                    // Read all section headers in one bulk read.
                    int shTableSize = eShnum * eShentsize;
                    byte[] shTable = new byte[shTableSize];
                    stream.Seek((long)eShoff, SeekOrigin.Begin);
                    if (ReadFully(stream, shTable, 0, shTableSize) < shTableSize)
                    {
                        Debug.WriteLine("ReadDebugLink: Could not read section headers.");
                        return null;
                    }

                    // Read the section name string table (shstrtab).
                    int shstrPos = eShstrndx * eShentsize;
                    ulong shstrOffset, shstrSize;
                    ReadSectionOffsetAndSize(shTable, shstrPos, is64Bit, bigEndian, out shstrOffset, out shstrSize);

                    if (shstrSize == 0 || shstrSize > 1024 * 1024)
                    {
                        Debug.WriteLine("ReadDebugLink: Invalid shstrtab size.");
                        return null;
                    }

                    byte[] shstrtab = new byte[(int)shstrSize];
                    stream.Seek((long)shstrOffset, SeekOrigin.Begin);
                    if (ReadFully(stream, shstrtab, 0, shstrtab.Length) < shstrtab.Length)
                    {
                        Debug.WriteLine("ReadDebugLink: Could not read shstrtab.");
                        return null;
                    }

                    // Iterate sections looking for .gnu_debuglink by name.
                    for (int i = 0; i < eShnum; i++)
                    {
                        int shPos = i * eShentsize;
                        uint shName = ReadU32Static(shTable, shPos, bigEndian);

                        if (shName >= shstrtab.Length)
                        {
                            continue;
                        }

                        // Compare section name against ".gnu_debuglink".
                        if (!SectionNameEquals(shstrtab, (int)shName, GnuDebugLinkName))
                        {
                            continue;
                        }

                        // Found .gnu_debuglink — read its contents.
                        ulong secOffset, secSize;
                        ReadSectionOffsetAndSize(shTable, shPos, is64Bit, bigEndian, out secOffset, out secSize);

                        // The section must contain at least a filename byte + null + 4-byte CRC.
                        if (secSize < 6 || secSize > 4096)
                        {
                            Debug.WriteLine("ReadDebugLink: Invalid .gnu_debuglink section size: " + secSize);
                            return null;
                        }

                        byte[] sectionData = new byte[(int)secSize];
                        stream.Seek((long)secOffset, SeekOrigin.Begin);
                        if (ReadFully(stream, sectionData, 0, sectionData.Length) < sectionData.Length)
                        {
                            Debug.WriteLine("ReadDebugLink: Could not read .gnu_debuglink section data.");
                            return null;
                        }

                        // Extract the null-terminated filename.
                        int nullPos = Array.IndexOf(sectionData, (byte)0);
                        if (nullPos <= 0)
                        {
                            Debug.WriteLine("ReadDebugLink: Empty or missing filename in .gnu_debuglink.");
                            return null;
                        }

                        return Encoding.UTF8.GetString(sectionData, 0, nullPos);
                    }

                    Debug.WriteLine("ReadDebugLink: No .gnu_debuglink section found.");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ReadDebugLink: Error reading file: " + ex.Message);
                return null;
            }
        }

        #region private

        // Name of the .gnu_debuglink section (UTF-8 bytes for fast comparison).
        private static readonly byte[] GnuDebugLinkName = Encoding.UTF8.GetBytes(".gnu_debuglink");

        /// <summary>
        /// Reads sh_offset and sh_size from a section header at the given position.
        /// Layout: sh_name(4), sh_type(4), sh_flags(4/8), sh_addr(4/8), sh_offset(4/8), sh_size(4/8).
        /// </summary>
        private static void ReadSectionOffsetAndSize(byte[] shTable, int shPos, bool is64Bit, bool bigEndian,
            out ulong offset, out ulong size)
        {
            if (is64Bit)
            {
                // 64-bit: sh_name(4) + sh_type(4) + sh_flags(8) + sh_addr(8) = 24 bytes before sh_offset(8), sh_size(8).
                int ofsPos = shPos + 4 + 4 + 8 + 8;
                offset = ReadU64Static(shTable, ofsPos, bigEndian);
                size = ReadU64Static(shTable, ofsPos + 8, bigEndian);
            }
            else
            {
                // 32-bit: sh_name(4) + sh_type(4) + sh_flags(4) + sh_addr(4) = 16 bytes before sh_offset(4), sh_size(4).
                int ofsPos = shPos + 4 + 4 + 4 + 4;
                offset = ReadU32Static(shTable, ofsPos, bigEndian);
                size = ReadU32Static(shTable, ofsPos + 4, bigEndian);
            }
        }

        /// <summary>
        /// Compares a null-terminated string in a byte array against an expected byte sequence.
        /// </summary>
        private static bool SectionNameEquals(byte[] strtab, int offset, byte[] expected)
        {
            if (offset + expected.Length > strtab.Length)
            {
                return false;
            }

            for (int i = 0; i < expected.Length; i++)
            {
                if (strtab[offset + i] != expected[i])
                {
                    return false;
                }
            }

            // Ensure the string in strtab is null-terminated right after the match.
            int endPos = offset + expected.Length;
            return endPos >= strtab.Length || strtab[endPos] == 0;
        }

        // ELF identification (e_ident) constants.
        private const byte ElfMagic0 = 0x7f;
        private const byte ElfMagic1 = (byte)'E';
        private const byte ElfMagic2 = (byte)'L';
        private const byte ElfMagic3 = (byte)'F';
        private const int EI_CLASS = 4;         // e_ident index for file class.
        private const int EI_DATA = 5;          // e_ident index for data encoding.
        private const int EI_NIDENT = 16;       // Size of e_ident array.

        // ELF class values.
        private const byte ElfClass32 = 1;
        private const byte ElfClass64 = 2;

        // ELF data encoding values.
        private const byte ElfDataMsb = 2;      // Big-endian.

        // ELF header sizes.
        private const int Elf32EhdrSize = 52;
        private const int Elf64EhdrSize = 64;

        // Section header entry sizes.
        private const int Elf32ShdrSize = 40;
        private const int Elf64ShdrSize = 64;
        private const int MaxShentsize = 256;

        // Maximum section count to accept from ELF headers.
        private const uint MaxSectionCount = 65535;

        // Section header types.
        private const uint SHT_SYMTAB = 2;
        private const uint SHT_DYNSYM = 11;

        // Program header types.
        private const uint PT_NOTE = 4;          // Note segment.

        // Note types.
        private const uint NT_GNU_BUILD_ID = 3;  // GNU build-id note type.

        // Symbol table constants.
        private const byte STT_FUNC = 2;        // Symbol type: function.
        private const byte STT_MASK = 0xf;      // Mask to extract symbol type from st_info.

        // Elf64_Sym field offsets: st_name(4), st_info(1), st_other(1), st_shndx(2), st_value(8), st_size(8).
        private const int Sym64_Name = 0;
        private const int Sym64_Info = 4;
        private const int Sym64_Value = 8;
        private const int Sym64_Size = 16;

        // Elf32_Sym field offsets: st_name(4), st_value(4), st_size(4), st_info(1), st_other(1), st_shndx(2).
        private const int Sym32_Name = 0;
        private const int Sym32_Value = 4;
        private const int Sym32_Size = 8;
        private const int Sym32_Info = 12;

        private const int StrtabSegmentSize = 65536; // 64KB segments to avoid LOH.

        private readonly List<ElfSymbolEntry> m_symbols;
        private readonly ulong m_pVaddr;
        private readonly ulong m_pOffset;
        private readonly bool m_demangle;
        private readonly ItaniumDemangler m_itaniumDemangler = new ItaniumDemangler();
        private readonly RustDemangler m_rustDemangler = new RustDemangler();
        private SegmentedList<byte> m_strtab; // Retained for lazy name resolution.
        private bool m_is64Bit;
        private bool m_bigEndian;

        /// <summary>
        /// Represents a resolved ELF symbol with its address range.
        /// Name is decoded lazily on first FindNameForRva hit.
        /// </summary>
        private struct ElfSymbolEntry : IComparable<ElfSymbolEntry>
        {
            public uint Start;          // RVA: (st_value - pVaddr) + pOffset.
            public uint End;            // Start + size - 1 (inclusive).
            public uint StrtabOffset;   // Offset into m_strtab for lazy name decode.
            public string Name;         // Null until first lookup, then cached.

            public int CompareTo(ElfSymbolEntry other) => Start.CompareTo(other.Start);
        }

        /// <summary>
        /// Parses the ELF stream with two-pass section scan for optimal pre-allocation.
        /// Pass 1: Measure total strtab and symbol sizes.
        /// Pass 2: Pre-allocate and parse with zero resizes.
        /// Symbol names are NOT decoded during parsing — they are resolved lazily on first lookup.
        /// </summary>
        private void ParseElf(Stream stream)
        {
            // Read the ELF header (max 64 bytes for 64-bit).
            byte[] header = new byte[Elf64EhdrSize];
            int headerRead = ReadFully(stream, header, 0, header.Length);
            if (headerRead < EI_NIDENT)
            {
                Debug.WriteLine("ElfSymbolModule: File too small.");
                return;
            }

            // Verify ELF magic bytes: 0x7f 'E' 'L' 'F'.
            if (header[0] != ElfMagic0 || header[1] != ElfMagic1 || header[2] != ElfMagic2 || header[3] != ElfMagic3)
            {
                Debug.WriteLine("ElfSymbolModule: Invalid ELF magic.");
                return;
            }

            byte eiClass = header[EI_CLASS];
            byte eiData = header[EI_DATA];

            m_is64Bit = (eiClass == ElfClass64);
            m_bigEndian = (eiData == ElfDataMsb);

            if (eiClass != ElfClass32 && eiClass != ElfClass64)
            {
                Debug.WriteLine("ElfSymbolModule: Unknown ELF class " + eiClass + ".");
                return;
            }

            int ehSize = m_is64Bit ? Elf64EhdrSize : Elf32EhdrSize;
            if (headerRead < ehSize)
            {
                return;
            }

            // Parse ELF header fields.
            int pos = 16 + 2 + 2 + 4; // skip e_ident(16), e_type(2), e_machine(2), e_version(4)

            ulong eShoff;
            if (m_is64Bit)
            {
                pos += 8 + 8; // e_entry, e_phoff
                eShoff = ReadU64(header, pos); pos += 8;
            }
            else
            {
                pos += 4 + 4;
                eShoff = ReadU32(header, pos); pos += 4;
            }

            pos += 4 + 2 + 2 + 2; // e_flags, e_ehsize, e_phentsize, e_phnum
            ushort eShentsize = ReadU16(header, pos); pos += 2;
            ushort eShnum = ReadU16(header, pos);

            if (eShoff == 0 || eShentsize == 0)
            {
                Debug.WriteLine("ElfSymbolModule: No section headers found.");
                return;
            }

            // Valid ELF section header sizes are 40 (32-bit) or 64 (64-bit).
            // Reject values below the minimum struct size (would cause out-of-bounds reads)
            // and cap at 256 to guard against overflow in sectionCount * eShentsize.
            int minShentsize = m_is64Bit ? Elf64ShdrSize : Elf32ShdrSize;
            if (eShentsize < minShentsize || eShentsize > MaxShentsize)
            {
                Debug.WriteLine("ElfSymbolModule: Invalid section header entry size: " + eShentsize);
                return;
            }

            // Handle extended section count.
            uint sectionCount = eShnum;
            if (eShnum == 0)
            {
                byte[] firstSh = new byte[eShentsize];
                stream.Seek((long)eShoff, SeekOrigin.Begin);
                if (ReadFully(stream, firstSh, 0, firstSh.Length) < firstSh.Length)
                {
                    return;
                }
                if (m_is64Bit)
                {
                    sectionCount = (uint)ReadU64(firstSh, 8 + 8 + 8 + 8);
                }
                else
                {
                    sectionCount = ReadU32(firstSh, 8 + 4 + 4 + 4);
                }
            }

            if (sectionCount == 0)
            {
                return;
            }

            // Guard against corrupt ELF headers with unreasonably large section counts.
            if (sectionCount > MaxSectionCount)
            {
                Debug.WriteLine("ElfSymbolModule: Section count too large: " + sectionCount);
                return;
            }

            // Read all section headers in one bulk read.
            int shTableSize = (int)sectionCount * eShentsize;
            byte[] shTable = new byte[shTableSize];
            stream.Seek((long)eShoff, SeekOrigin.Begin);
            if (ReadFully(stream, shTable, 0, shTableSize) < shTableSize)
            {
                return;
            }

            // Pass 1: Measure total strtab and symbol count for pre-allocation.
            long totalStrtabSize = 0;
            long totalSymbolCount = 0;
            for (uint i = 0; i < sectionCount; i++)
            {
                int shPos = (int)i * eShentsize;
                uint shType;
                long shOffset, shSize, shLink, shEntsize;
                ReadSectionHeader(shTable, shPos, out shType, out shOffset, out shSize, out shLink, out shEntsize);

                if (shType != SHT_SYMTAB && shType != SHT_DYNSYM)
                {
                    continue;
                }

                if (shEntsize > 0)
                {
                    totalSymbolCount += shSize / shEntsize;
                }

                // Get the linked string table size.
                if (shLink == 0 || shLink >= sectionCount)
                {
                    continue;
                }

                int strtabShPos = (int)shLink * eShentsize;
                uint strtabType;
                long strtabOffset, strtabSize, strtabLink, strtabEntsize;
                ReadSectionHeader(shTable, strtabShPos, out strtabType, out strtabOffset, out strtabSize, out strtabLink, out strtabEntsize);
                totalStrtabSize += strtabSize;
            }

            // Pre-allocate with known sizes.
            m_strtab = new SegmentedList<byte>(StrtabSegmentSize, totalStrtabSize);
            m_symbols.Capacity = (int)totalSymbolCount;

            // Pass 2: Load strtabs and symbol entries.
            long strtabBaseOffset = 0;
            for (uint i = 0; i < sectionCount; i++)
            {
                int shPos = (int)i * eShentsize;
                uint shType;
                long shOffset, shSize, shLink, shEntsize;
                ReadSectionHeader(shTable, shPos, out shType, out shOffset, out shSize, out shLink, out shEntsize);

                if (shType != SHT_SYMTAB && shType != SHT_DYNSYM)
                {
                    continue;
                }

                // Load the linked string table into the SegmentedList.
                if (shLink == 0 || shLink >= sectionCount)
                {
                    continue;
                }

                int strtabShPos = (int)shLink * eShentsize;
                uint strtabType;
                long strtabOffset, strtabSize, strtabLink, strtabEntsize;
                ReadSectionHeader(shTable, strtabShPos, out strtabType, out strtabOffset, out strtabSize, out strtabLink, out strtabEntsize);

                if (strtabSize <= 0)
                {
                    continue;
                }

                // Read strtab in chunks and append to SegmentedList.
                stream.Seek(strtabOffset, SeekOrigin.Begin);
                long remaining = strtabSize;
                byte[] readBuf = new byte[Math.Min(remaining, StrtabSegmentSize)];
                while (remaining > 0)
                {
                    int toRead = (int)Math.Min(remaining, readBuf.Length);
                    int read = ReadFully(stream, readBuf, 0, toRead);
                    if (read == 0)
                    {
                        break;
                    }
                    m_strtab.AppendFrom(readBuf, 0, read);
                    remaining -= read;
                }

                // Read the symbol table section.
                byte[] symData = new byte[shSize];
                stream.Seek(shOffset, SeekOrigin.Begin);
                if (ReadFully(stream, symData, 0, (int)shSize) < shSize)
                {
                    strtabBaseOffset += strtabSize;
                    continue;
                }

                ReadSymbolTable(symData, shSize, shEntsize, strtabBaseOffset);
                strtabBaseOffset += strtabSize;
            }

            // Sort symbols by start address for binary search.
            m_symbols.Sort();
        }

        /// <summary>
        /// Reads exactly count bytes from the stream. Returns the number actually read.
        /// </summary>
        private static int ReadFully(Stream stream, byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = stream.Read(buffer, offset + totalRead, count - totalRead);
                if (read == 0)
                {
                    break;
                }
                totalRead += read;
            }
            return totalRead;
        }

        /// <summary>
        /// Reads section header fields from a byte array at the given position.
        /// </summary>
        private void ReadSectionHeader(byte[] data, int pos, out uint shType, out long shOffset,
            out long shSize, out long shLink, out long shEntsize)
        {
            pos += 4; // skip sh_name
            shType = ReadU32(data, pos); pos += 4;

            if (m_is64Bit)
            {
                pos += 8 + 8; // sh_flags, sh_addr
                shOffset = (long)ReadU64(data, pos); pos += 8;
                shSize = (long)ReadU64(data, pos); pos += 8;
                shLink = ReadU32(data, pos); pos += 4;
                pos += 4 + 8; // sh_info, sh_addralign
                shEntsize = (long)ReadU64(data, pos);
            }
            else
            {
                pos += 4 + 4; // sh_flags, sh_addr
                shOffset = ReadU32(data, pos); pos += 4;
                shSize = ReadU32(data, pos); pos += 4;
                shLink = ReadU32(data, pos); pos += 4;
                pos += 4 + 4; // sh_info, sh_addralign
                shEntsize = ReadU32(data, pos);
            }
        }

        /// <summary>
        /// Reads all symbol entries from a pre-loaded symbol table byte array.
        /// Stores strtab offsets for lazy name resolution instead of decoding strings.
        /// </summary>
        private void ReadSymbolTable(byte[] symData, long size, long entsize, long strtabBaseOffset)
        {
            if (entsize == 0)
            {
                return;
            }

            long count = size / entsize;

            for (long i = 0; i < count; i++)
            {
                int pos = (int)(i * entsize);

                uint stName;
                byte stInfo;
                ulong stValue;
                ulong stSize;

                if (m_is64Bit && !m_bigEndian)
                {
                    // Fast path for 64-bit little-endian (the common case).
                    stName = BitConverter.ToUInt32(symData, pos + Sym64_Name);
                    stInfo = symData[pos + Sym64_Info];
                    stValue = BitConverter.ToUInt64(symData, pos + Sym64_Value);
                    stSize = BitConverter.ToUInt64(symData, pos + Sym64_Size);
                }
                else if (m_is64Bit)
                {
                    stName = ReadU32(symData, pos + Sym64_Name);
                    stInfo = symData[pos + Sym64_Info];
                    stValue = ReadU64(symData, pos + Sym64_Value);
                    stSize = ReadU64(symData, pos + Sym64_Size);
                }
                else if (!m_bigEndian)
                {
                    stName = BitConverter.ToUInt32(symData, pos + Sym32_Name);
                    stValue = BitConverter.ToUInt32(symData, pos + Sym32_Value);
                    stSize = BitConverter.ToUInt32(symData, pos + Sym32_Size);
                    stInfo = symData[pos + Sym32_Info];
                }
                else
                {
                    stName = ReadU32(symData, pos + Sym32_Name);
                    stValue = ReadU32(symData, pos + Sym32_Value);
                    stSize = ReadU32(symData, pos + Sym32_Size);
                    stInfo = symData[pos + Sym32_Info];
                }

                // Filter to STT_FUNC symbols with non-zero value and size.
                if ((stInfo & STT_MASK) != STT_FUNC || stValue == 0 || stSize == 0)
                {
                    continue;
                }

                // Skip symbols whose virtual address is below the PT_LOAD base.
                if (stValue < m_pVaddr)
                {
                    continue;
                }

                // Quick validation that the name offset is within bounds.
                uint absoluteStrtabOffset = (uint)(strtabBaseOffset + stName);
                if (absoluteStrtabOffset >= m_strtab.Count || m_strtab[absoluteStrtabOffset] == 0)
                {
                    continue;
                }

                // Convert virtual address to RVA: (st_value - p_vaddr) + p_offset.
                uint adjustedRva = (uint)((stValue - m_pVaddr) + m_pOffset);
                uint adjustedEnd = (uint)(adjustedRva + (uint)stSize - 1);

                m_symbols.Add(new ElfSymbolEntry
                {
                    Start = adjustedRva,
                    End = adjustedEnd,
                    StrtabOffset = absoluteStrtabOffset,
                    // Name is null — decoded lazily on first FindNameForRva hit.
                });
            }
        }

        /// <summary>
        /// Searches a PT_NOTE segment's raw bytes for a GNU build-id note.
        /// Note format: namesz(4) + descsz(4) + type(4) + name(aligned to 4) + desc(aligned to 4).
        /// </summary>
        /// <param name="noteData">Raw bytes of the PT_NOTE segment.</param>
        /// <param name="bigEndian">True if the ELF file uses big-endian encoding.</param>
        /// <returns>Lowercase hex string of the build-id, or null if not found.</returns>
        private static string ExtractBuildId(byte[] noteData, bool bigEndian)
        {
            int pos = 0;
            int length = noteData.Length;

            while (pos + 12 <= length) // Minimum note header: namesz(4) + descsz(4) + type(4).
            {
                uint namesz = ReadU32Static(noteData, pos, bigEndian);
                uint descsz = ReadU32Static(noteData, pos + 4, bigEndian);
                uint type = ReadU32Static(noteData, pos + 8, bigEndian);
                pos += 12;

                // Align name and desc sizes to 4-byte boundaries.
                uint nameAligned = (namesz + 3) & ~3u;
                uint descAligned = (descsz + 3) & ~3u;

                // Validate that the note fits within the segment data.
                if (pos + nameAligned + descAligned > length)
                {
                    break;
                }

                // Check for GNU build-id: name == "GNU\0" (namesz == 4) and type == NT_GNU_BUILD_ID (3).
                if (type == NT_GNU_BUILD_ID && namesz == 4 &&
                    noteData[pos] == (byte)'G' && noteData[pos + 1] == (byte)'N' &&
                    noteData[pos + 2] == (byte)'U' && noteData[pos + 3] == 0)
                {
                    if (descsz == 0)
                    {
                        return null;
                    }

                    // Extract the build-id descriptor bytes as lowercase hex.
                    int descStart = pos + (int)nameAligned;
                    var sb = new StringBuilder((int)descsz * 2);
                    for (int j = 0; j < (int)descsz; j++)
                    {
                        sb.Append(noteData[descStart + j].ToString("x2"));
                    }
                    return sb.ToString();
                }

                pos += (int)nameAligned + (int)descAligned;
            }

            return null;
        }

        /// <summary>
        /// Attempts to demangle a symbol name using available demanglers.
        /// Supports Itanium C++ ABI (_Z prefix) and Rust v0 (_R prefix) mangling.
        /// </summary>
        private string TryDemangle(string name)
        {
            if (!m_demangle)
            {
                return name;
            }

            if (name.StartsWith("_Z"))
            {
                string demangled = m_itaniumDemangler.Demangle(name);
                if (demangled != null)
                {
                    return demangled;
                }
            }

            if (name.StartsWith("_R"))
            {
                string demangled = m_rustDemangler.Demangle(name);
                if (demangled != null)
                {
                    return demangled;
                }
            }

            return name;
        }

        /// <summary>
        /// Reads a null-terminated UTF-8 string from a SegmentedList at the given offset.
        /// Uses GetSlot() for efficient segment-aware access.
        /// </summary>
        private static string ReadNullTerminatedString(SegmentedList<byte> data, uint offset)
        {
            if (offset >= data.Count)
            {
                return string.Empty;
            }

            // Use GetSlot to get the underlying segment for fast sequential access.
            byte[] segment = data.GetSlot((int)offset, out int slotOffset);
            int segmentRemaining = segment.Length - slotOffset;

            // Fast path: find null terminator within the current segment.
            int len = 0;
            while (len < segmentRemaining && segment[slotOffset + len] != 0)
            {
                len++;
            }

            if (len < segmentRemaining)
            {
                // Null terminator found within segment — fast path.
                return Encoding.UTF8.GetString(segment, slotOffset, len);
            }

            // Slow path: string spans segment boundary. Copy bytes into a temp buffer.
            var bytes = new List<byte>(len + 64);
            for (int i = 0; i < len; i++)
            {
                bytes.Add(segment[slotOffset + i]);
            }

            long pos = offset + len;
            while (pos < data.Count)
            {
                byte b = data[pos];
                if (b == 0)
                {
                    break;
                }
                bytes.Add(b);
                pos++;
            }

            return Encoding.UTF8.GetString(bytes.ToArray(), 0, bytes.Count);
        }

        #region Endianness helpers

        /// <summary>Little-endian uint16 read from byte array.</summary>
        private static ushort ReadU16LE(byte[] data, int offset)
        {
            return (ushort)(data[offset] | data[offset + 1] << 8);
        }

        /// <summary>Big-endian uint16 read from byte array.</summary>
        private static ushort ReadU16BE(byte[] data, int offset)
        {
            return (ushort)(data[offset] << 8 | data[offset + 1]);
        }

        private ushort ReadU16(byte[] data, int offset)
        {
            return m_bigEndian ? ReadU16BE(data, offset) : ReadU16LE(data, offset);
        }

        private uint ReadU32(byte[] data, int offset)
        {
            if (m_bigEndian)
            {
                return (uint)(data[offset] << 24 | data[offset + 1] << 16 | data[offset + 2] << 8 | data[offset + 3]);
            }
            return BitConverter.ToUInt32(data, offset);
        }

        private ulong ReadU64(byte[] data, int offset)
        {
            if (m_bigEndian)
            {
                return ((ulong)ReadU32(data, offset) << 32) | ReadU32(data, offset + 4);
            }
            return BitConverter.ToUInt64(data, offset);
        }

        // Static overloads for use in ReadBuildId (which has no instance state).

        /// <summary>Static uint16 read with explicit endianness.</summary>
        private static ushort ReadU16Static(byte[] data, int offset, bool bigEndian)
        {
            return bigEndian ? ReadU16BE(data, offset) : ReadU16LE(data, offset);
        }

        /// <summary>Static uint32 read with explicit endianness.</summary>
        private static uint ReadU32Static(byte[] data, int offset, bool bigEndian)
        {
            if (bigEndian)
            {
                return (uint)(data[offset] << 24 | data[offset + 1] << 16 | data[offset + 2] << 8 | data[offset + 3]);
            }
            return BitConverter.ToUInt32(data, offset);
        }

        /// <summary>Static uint64 read with explicit endianness.</summary>
        private static ulong ReadU64Static(byte[] data, int offset, bool bigEndian)
        {
            if (bigEndian)
            {
                return ((ulong)ReadU32Static(data, offset, bigEndian) << 32) | ReadU32Static(data, offset + 4, bigEndian);
            }
            return BitConverter.ToUInt64(data, offset);
        }

        #endregion

        #endregion
    }
}
