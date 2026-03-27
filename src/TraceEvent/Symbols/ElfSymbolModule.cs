using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

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

                // Thread-safe lazy name decode on first access.
                if (Volatile.Read(ref m_symbolNames[hi]) == null)
                {
                    string name = ReadNullTerminatedString(m_strtab, m_symbols[hi].StrtabOffset);
                    name = TryDemangle(name);
                    Interlocked.CompareExchange(ref m_symbolNames[hi], name, null);
                }

                return m_symbolNames[hi];
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
                    // Read the ELF header.
                    byte[] header = new byte[Unsafe.SizeOf<Elf64_Ehdr>()];
                    int headerRead = ReadFully(stream, header, 0, header.Length);
                    if (headerRead < EI_NIDENT)
                    {
                        Debug.WriteLine("ReadBuildId: File too small.");
                        return null;
                    }

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

                    int ehSize = is64Bit ? Unsafe.SizeOf<Elf64_Ehdr>() : Unsafe.SizeOf<Elf32_Ehdr>();
                    if (headerRead < ehSize)
                    {
                        Debug.WriteLine("ReadBuildId: Header too small.");
                        return null;
                    }

                    // Extract program header fields from the typed struct.
                    ulong ePhoff;
                    ushort ePhentsize, ePhnum;
                    if (is64Bit)
                    {
                        var ehdr = ReadStruct<Elf64_Ehdr>(header, 0);
                        if (bigEndian) ehdr.SwapEndian();
                        ePhoff = ehdr.e_phoff;
                        ePhentsize = ehdr.e_phentsize;
                        ePhnum = ehdr.e_phnum;
                    }
                    else
                    {
                        var ehdr = ReadStruct<Elf32_Ehdr>(header, 0);
                        if (bigEndian) ehdr.SwapEndian();
                        ePhoff = ehdr.e_phoff;
                        ePhentsize = ehdr.e_phentsize;
                        ePhnum = ehdr.e_phnum;
                    }

                    if (ePhoff == 0 || ePhentsize == 0 || ePhnum == 0)
                    {
                        Debug.WriteLine("ReadBuildId: No program headers found.");
                        return null;
                    }

                    int minPhentsize = is64Bit ? Unsafe.SizeOf<Elf64_Phdr>() : Unsafe.SizeOf<Elf32_Phdr>();
                    if (ePhentsize < minPhentsize)
                    {
                        Debug.WriteLine("ReadBuildId: ePhentsize too small: " + ePhentsize);
                        return null;
                    }

                    if (ePhnum > MaxProgramHeaderCount)
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

                        uint pType;
                        ulong pOffset, pFilesz;
                        if (is64Bit)
                        {
                            var phdr = ReadStruct<Elf64_Phdr>(phTable, phPos);
                            if (bigEndian) phdr.SwapEndian();
                            pType = phdr.p_type;
                            pOffset = phdr.p_offset;
                            pFilesz = phdr.p_filesz;
                        }
                        else
                        {
                            var phdr = ReadStruct<Elf32_Phdr>(phTable, phPos);
                            if (bigEndian) phdr.SwapEndian();
                            pType = phdr.p_type;
                            pOffset = phdr.p_offset;
                            pFilesz = phdr.p_filesz;
                        }

                        if (pType != PT_NOTE)
                        {
                            continue;
                        }

                        if (pFilesz == 0 || pFilesz > MaxNoteSizeBytes)
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
                    // Read the ELF header.
                    byte[] header = new byte[Unsafe.SizeOf<Elf64_Ehdr>()];
                    int headerRead = ReadFully(stream, header, 0, header.Length);
                    if (headerRead < EI_NIDENT)
                    {
                        Debug.WriteLine("ReadDebugLink: File too small.");
                        return null;
                    }

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

                    int ehSize = is64Bit ? Unsafe.SizeOf<Elf64_Ehdr>() : Unsafe.SizeOf<Elf32_Ehdr>();
                    if (headerRead < ehSize)
                    {
                        Debug.WriteLine("ReadDebugLink: Header too small.");
                        return null;
                    }

                    // Extract section header fields from the typed struct.
                    ulong eShoff;
                    ushort eShentsize, eShnum, eShstrndx;
                    if (is64Bit)
                    {
                        var ehdr = ReadStruct<Elf64_Ehdr>(header, 0);
                        if (bigEndian) ehdr.SwapEndian();
                        eShoff = ehdr.e_shoff;
                        eShentsize = ehdr.e_shentsize;
                        eShnum = ehdr.e_shnum;
                        eShstrndx = ehdr.e_shstrndx;
                    }
                    else
                    {
                        var ehdr = ReadStruct<Elf32_Ehdr>(header, 0);
                        if (bigEndian) ehdr.SwapEndian();
                        eShoff = ehdr.e_shoff;
                        eShentsize = ehdr.e_shentsize;
                        eShnum = ehdr.e_shnum;
                        eShstrndx = ehdr.e_shstrndx;
                    }

                    if (eShoff == 0 || eShentsize == 0 || eShnum == 0)
                    {
                        Debug.WriteLine("ReadDebugLink: No section headers found.");
                        return null;
                    }

                    int minShentsize = is64Bit ? Unsafe.SizeOf<Elf64_Shdr>() : Unsafe.SizeOf<Elf32_Shdr>();
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
                    ReadSectionHeader(shTable, shstrPos, is64Bit, bigEndian, out _, out ulong shstrOffset, out ulong shstrSize, out _, out _);

                    if (shstrSize == 0 || shstrSize > MaxShstrtabSize)
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
                        uint shName;
                        ulong secOffset, secSize;
                        if (is64Bit)
                        {
                            var shdr = ReadStruct<Elf64_Shdr>(shTable, shPos);
                            if (bigEndian) shdr.SwapEndian();
                            shName = shdr.sh_name;
                            secOffset = shdr.sh_offset;
                            secSize = shdr.sh_size;
                        }
                        else
                        {
                            var shdr = ReadStruct<Elf32_Shdr>(shTable, shPos);
                            if (bigEndian) shdr.SwapEndian();
                            shName = shdr.sh_name;
                            secOffset = shdr.sh_offset;
                            secSize = shdr.sh_size;
                        }

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

                        // The section must contain at least a filename byte + null + 4-byte CRC.
                        if (secSize < MinDebugLinkSectionSize || secSize > MaxDebugLinkSectionSize)
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
        /// Reads section header fields from a byte array at the given position.
        /// </summary>
        private static void ReadSectionHeader(byte[] shTable, int shPos, bool is64Bit, bool bigEndian,
            out uint shType, out ulong offset, out ulong size, out uint link, out ulong entsize)
        {
            if (is64Bit)
            {
                var shdr = ReadStruct<Elf64_Shdr>(shTable, shPos);
                if (bigEndian) shdr.SwapEndian();
                shType = shdr.sh_type;
                offset = shdr.sh_offset;
                size = shdr.sh_size;
                link = shdr.sh_link;
                entsize = shdr.sh_entsize;
            }
            else
            {
                var shdr = ReadStruct<Elf32_Shdr>(shTable, shPos);
                if (bigEndian) shdr.SwapEndian();
                shType = shdr.sh_type;
                offset = shdr.sh_offset;
                size = shdr.sh_size;
                link = shdr.sh_link;
                entsize = shdr.sh_entsize;
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

        // Maximum program header count to accept from ELF headers.
        private const int MaxProgramHeaderCount = 4096;

        // Maximum section header entry size and section count.
        private const int MaxShentsize = 256;
        private const uint MaxSectionCount = 65535;

        // Section header types.
        private const uint SHT_SYMTAB = 2;
        private const uint SHT_DYNSYM = 11;

        // Program header types.
        private const uint PT_NOTE = 4;          // Note segment.

        // Note types.
        private const uint NT_GNU_BUILD_ID = 3;  // GNU build-id note type.

        // Expected namesz for GNU notes ("GNU\0").
        private const uint GnuNoteNameSize = 4;

        // PT_NOTE segment size limit for ReadBuildId. Real build-id notes are < 100 bytes;
        // 64 KB is generous. Prevents OOM from crafted ELF with large p_filesz.
        private const int MaxNoteSizeBytes = 64 * 1024;

        // ReadDebugLink section size limits.
        private const int MaxShstrtabSize = 1024 * 1024;    // 1 MB
        private const int MinDebugLinkSectionSize = 6;       // 1-char filename + null + 4-byte CRC
        private const int MaxDebugLinkSectionSize = 4096;

        // Symbol table constants.
        private const byte STT_FUNC = 2;        // Symbol type: function.
        private const byte STT_MASK = 0xf;      // Mask to extract symbol type from st_info.

        private const int StrtabSegmentSize = 65536; // 64KB segments to avoid LOH.

        #region ELF binary format structs

        // These structs match the ELF specification layouts exactly. Fields use ELF naming conventions
        // (e_phoff, sh_type, st_value, etc.) for easy cross-reference with the spec.
        // MemoryMarshal.Read<T> is used to read them from byte arrays — the same pattern as PEFile.cs.
        // For big-endian ELF files, SwapEndian() reverses each multi-byte field after reading.

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Elf64_Ehdr
        {
            // e_ident[16]
            public byte ei_mag0, ei_mag1, ei_mag2, ei_mag3;
            public byte ei_class, ei_data, ei_version, ei_osabi;
            public byte ei_abiversion, ei_pad1, ei_pad2, ei_pad3, ei_pad4, ei_pad5, ei_pad6, ei_pad7;
            // Header fields
            public ushort e_type;
            public ushort e_machine;
            public uint e_version;
            public ulong e_entry;
            public ulong e_phoff;
            public ulong e_shoff;
            public uint e_flags;
            public ushort e_ehsize;
            public ushort e_phentsize;
            public ushort e_phnum;
            public ushort e_shentsize;
            public ushort e_shnum;
            public ushort e_shstrndx;

            public void SwapEndian()
            {
                e_type = BinaryPrimitives.ReverseEndianness(e_type);
                e_machine = BinaryPrimitives.ReverseEndianness(e_machine);
                e_version = BinaryPrimitives.ReverseEndianness(e_version);
                e_entry = BinaryPrimitives.ReverseEndianness(e_entry);
                e_phoff = BinaryPrimitives.ReverseEndianness(e_phoff);
                e_shoff = BinaryPrimitives.ReverseEndianness(e_shoff);
                e_flags = BinaryPrimitives.ReverseEndianness(e_flags);
                e_ehsize = BinaryPrimitives.ReverseEndianness(e_ehsize);
                e_phentsize = BinaryPrimitives.ReverseEndianness(e_phentsize);
                e_phnum = BinaryPrimitives.ReverseEndianness(e_phnum);
                e_shentsize = BinaryPrimitives.ReverseEndianness(e_shentsize);
                e_shnum = BinaryPrimitives.ReverseEndianness(e_shnum);
                e_shstrndx = BinaryPrimitives.ReverseEndianness(e_shstrndx);
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Elf32_Ehdr
        {
            // e_ident[16]
            public byte ei_mag0, ei_mag1, ei_mag2, ei_mag3;
            public byte ei_class, ei_data, ei_version, ei_osabi;
            public byte ei_abiversion, ei_pad1, ei_pad2, ei_pad3, ei_pad4, ei_pad5, ei_pad6, ei_pad7;
            // Header fields
            public ushort e_type;
            public ushort e_machine;
            public uint e_version;
            public uint e_entry;
            public uint e_phoff;
            public uint e_shoff;
            public uint e_flags;
            public ushort e_ehsize;
            public ushort e_phentsize;
            public ushort e_phnum;
            public ushort e_shentsize;
            public ushort e_shnum;
            public ushort e_shstrndx;

            public void SwapEndian()
            {
                e_type = BinaryPrimitives.ReverseEndianness(e_type);
                e_machine = BinaryPrimitives.ReverseEndianness(e_machine);
                e_version = BinaryPrimitives.ReverseEndianness(e_version);
                e_entry = BinaryPrimitives.ReverseEndianness(e_entry);
                e_phoff = BinaryPrimitives.ReverseEndianness(e_phoff);
                e_shoff = BinaryPrimitives.ReverseEndianness(e_shoff);
                e_flags = BinaryPrimitives.ReverseEndianness(e_flags);
                e_ehsize = BinaryPrimitives.ReverseEndianness(e_ehsize);
                e_phentsize = BinaryPrimitives.ReverseEndianness(e_phentsize);
                e_phnum = BinaryPrimitives.ReverseEndianness(e_phnum);
                e_shentsize = BinaryPrimitives.ReverseEndianness(e_shentsize);
                e_shnum = BinaryPrimitives.ReverseEndianness(e_shnum);
                e_shstrndx = BinaryPrimitives.ReverseEndianness(e_shstrndx);
            }
        }

        // 64-bit: p_type(4), p_flags(4), p_offset(8), p_vaddr(8), p_paddr(8), p_filesz(8), p_memsz(8), p_align(8)
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Elf64_Phdr
        {
            public uint p_type;
            public uint p_flags;
            public ulong p_offset;
            public ulong p_vaddr;
            public ulong p_paddr;
            public ulong p_filesz;
            public ulong p_memsz;
            public ulong p_align;

            public void SwapEndian()
            {
                p_type = BinaryPrimitives.ReverseEndianness(p_type);
                p_flags = BinaryPrimitives.ReverseEndianness(p_flags);
                p_offset = BinaryPrimitives.ReverseEndianness(p_offset);
                p_vaddr = BinaryPrimitives.ReverseEndianness(p_vaddr);
                p_paddr = BinaryPrimitives.ReverseEndianness(p_paddr);
                p_filesz = BinaryPrimitives.ReverseEndianness(p_filesz);
                p_memsz = BinaryPrimitives.ReverseEndianness(p_memsz);
                p_align = BinaryPrimitives.ReverseEndianness(p_align);
            }
        }

        // 32-bit: p_type(4), p_offset(4), p_vaddr(4), p_paddr(4), p_filesz(4), p_memsz(4), p_flags(4), p_align(4)
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Elf32_Phdr
        {
            public uint p_type;
            public uint p_offset;
            public uint p_vaddr;
            public uint p_paddr;
            public uint p_filesz;
            public uint p_memsz;
            public uint p_flags;
            public uint p_align;

            public void SwapEndian()
            {
                p_type = BinaryPrimitives.ReverseEndianness(p_type);
                p_offset = BinaryPrimitives.ReverseEndianness(p_offset);
                p_vaddr = BinaryPrimitives.ReverseEndianness(p_vaddr);
                p_paddr = BinaryPrimitives.ReverseEndianness(p_paddr);
                p_filesz = BinaryPrimitives.ReverseEndianness(p_filesz);
                p_memsz = BinaryPrimitives.ReverseEndianness(p_memsz);
                p_flags = BinaryPrimitives.ReverseEndianness(p_flags);
                p_align = BinaryPrimitives.ReverseEndianness(p_align);
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Elf64_Shdr
        {
            public uint sh_name;
            public uint sh_type;
            public ulong sh_flags;
            public ulong sh_addr;
            public ulong sh_offset;
            public ulong sh_size;
            public uint sh_link;
            public uint sh_info;
            public ulong sh_addralign;
            public ulong sh_entsize;

            public void SwapEndian()
            {
                sh_name = BinaryPrimitives.ReverseEndianness(sh_name);
                sh_type = BinaryPrimitives.ReverseEndianness(sh_type);
                sh_flags = BinaryPrimitives.ReverseEndianness(sh_flags);
                sh_addr = BinaryPrimitives.ReverseEndianness(sh_addr);
                sh_offset = BinaryPrimitives.ReverseEndianness(sh_offset);
                sh_size = BinaryPrimitives.ReverseEndianness(sh_size);
                sh_link = BinaryPrimitives.ReverseEndianness(sh_link);
                sh_info = BinaryPrimitives.ReverseEndianness(sh_info);
                sh_addralign = BinaryPrimitives.ReverseEndianness(sh_addralign);
                sh_entsize = BinaryPrimitives.ReverseEndianness(sh_entsize);
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Elf32_Shdr
        {
            public uint sh_name;
            public uint sh_type;
            public uint sh_flags;
            public uint sh_addr;
            public uint sh_offset;
            public uint sh_size;
            public uint sh_link;
            public uint sh_info;
            public uint sh_addralign;
            public uint sh_entsize;

            public void SwapEndian()
            {
                sh_name = BinaryPrimitives.ReverseEndianness(sh_name);
                sh_type = BinaryPrimitives.ReverseEndianness(sh_type);
                sh_flags = BinaryPrimitives.ReverseEndianness(sh_flags);
                sh_addr = BinaryPrimitives.ReverseEndianness(sh_addr);
                sh_offset = BinaryPrimitives.ReverseEndianness(sh_offset);
                sh_size = BinaryPrimitives.ReverseEndianness(sh_size);
                sh_link = BinaryPrimitives.ReverseEndianness(sh_link);
                sh_info = BinaryPrimitives.ReverseEndianness(sh_info);
                sh_addralign = BinaryPrimitives.ReverseEndianness(sh_addralign);
                sh_entsize = BinaryPrimitives.ReverseEndianness(sh_entsize);
            }
        }

        // 64-bit: st_name(4), st_info(1), st_other(1), st_shndx(2), st_value(8), st_size(8) = 24 bytes
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Elf64_Sym
        {
            public uint st_name;
            public byte st_info;
            public byte st_other;
            public ushort st_shndx;
            public ulong st_value;
            public ulong st_size;

            public void SwapEndian()
            {
                st_name = BinaryPrimitives.ReverseEndianness(st_name);
                st_shndx = BinaryPrimitives.ReverseEndianness(st_shndx);
                st_value = BinaryPrimitives.ReverseEndianness(st_value);
                st_size = BinaryPrimitives.ReverseEndianness(st_size);
            }
        }

        // 32-bit: st_name(4), st_value(4), st_size(4), st_info(1), st_other(1), st_shndx(2) = 16 bytes
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Elf32_Sym
        {
            public uint st_name;
            public uint st_value;
            public uint st_size;
            public byte st_info;
            public byte st_other;
            public ushort st_shndx;

            public void SwapEndian()
            {
                st_name = BinaryPrimitives.ReverseEndianness(st_name);
                st_value = BinaryPrimitives.ReverseEndianness(st_value);
                st_size = BinaryPrimitives.ReverseEndianness(st_size);
                st_shndx = BinaryPrimitives.ReverseEndianness(st_shndx);
            }
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct Elf_Nhdr
        {
            public uint n_namesz;
            public uint n_descsz;
            public uint n_type;

            public void SwapEndian()
            {
                n_namesz = BinaryPrimitives.ReverseEndianness(n_namesz);
                n_descsz = BinaryPrimitives.ReverseEndianness(n_descsz);
                n_type = BinaryPrimitives.ReverseEndianness(n_type);
            }
        }

        /// <summary>
        /// Reads an ELF struct from a byte array at the given offset.
        /// For little-endian ELF files the struct is ready to use. For big-endian, the caller
        /// must call SwapEndian() on the returned value.
        /// </summary>
        private static T ReadStruct<T>(byte[] data, int offset) where T : struct
        {
            return MemoryMarshal.Read<T>(data.AsSpan(offset));
        }

        #endregion

        private readonly List<ElfSymbolEntry> m_symbols;
        private readonly ulong m_pVaddr;
        private readonly ulong m_pOffset;
        private readonly bool m_demangle;

        // Demanglers use mutable parser state and are not thread-safe. ThreadLocal ensures
        // each thread gets its own instance for safe concurrent FindNameForRva calls.
        private readonly ThreadLocal<ItaniumDemangler> m_itaniumDemangler = new ThreadLocal<ItaniumDemangler>(() => new ItaniumDemangler());
        private readonly ThreadLocal<RustDemangler> m_rustDemangler = new ThreadLocal<RustDemangler>(() => new RustDemangler());
        private SegmentedList<byte> m_strtab; // Retained for lazy name resolution.
        private string[] m_symbolNames;        // Thread-safe lazy name cache (parallel to m_symbols).
        private bool m_is64Bit;
        private bool m_bigEndian;

        /// <summary>
        /// Represents a resolved ELF symbol with its address range.
        /// Name is decoded lazily on first FindNameForRva hit via m_symbolNames.
        /// </summary>
        private struct ElfSymbolEntry : IComparable<ElfSymbolEntry>
        {
            public uint Start;          // RVA: (st_value - pVaddr) + pOffset.
            public uint End;            // Start + size - 1 (inclusive).
            public uint StrtabOffset;   // Offset into m_strtab for lazy name decode.

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
            // Read the ELF header.
            byte[] header = new byte[Unsafe.SizeOf<Elf64_Ehdr>()];
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

            int ehSize = m_is64Bit ? Unsafe.SizeOf<Elf64_Ehdr>() : Unsafe.SizeOf<Elf32_Ehdr>();
            if (headerRead < ehSize)
            {
                return;
            }

            // Extract section header fields from the typed struct.
            ulong eShoff;
            ushort eShentsize, eShnum;
            if (m_is64Bit)
            {
                var ehdr = ReadStruct<Elf64_Ehdr>(header, 0);
                if (m_bigEndian) ehdr.SwapEndian();
                eShoff = ehdr.e_shoff;
                eShentsize = ehdr.e_shentsize;
                eShnum = ehdr.e_shnum;
            }
            else
            {
                var ehdr = ReadStruct<Elf32_Ehdr>(header, 0);
                if (m_bigEndian) ehdr.SwapEndian();
                eShoff = ehdr.e_shoff;
                eShentsize = ehdr.e_shentsize;
                eShnum = ehdr.e_shnum;
            }

            if (eShoff == 0 || eShentsize == 0)
            {
                Debug.WriteLine("ElfSymbolModule: No section headers found.");
                return;
            }

            // Valid ELF section header sizes are 40 (32-bit) or 64 (64-bit).
            // Reject values below the minimum struct size (would cause out-of-bounds reads)
            // and cap at 256 to guard against overflow in sectionCount * eShentsize.
            int minShentsize = m_is64Bit ? Unsafe.SizeOf<Elf64_Shdr>() : Unsafe.SizeOf<Elf32_Shdr>();
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
                    var firstShdr = ReadStruct<Elf64_Shdr>(firstSh, 0);
                    if (m_bigEndian) firstShdr.SwapEndian();
                    sectionCount = (uint)firstShdr.sh_size;
                }
                else
                {
                    var firstShdr = ReadStruct<Elf32_Shdr>(firstSh, 0);
                    if (m_bigEndian) firstShdr.SwapEndian();
                    sectionCount = firstShdr.sh_size;
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
                ReadSectionHeader(shTable, shPos, m_is64Bit, m_bigEndian,
                    out uint shType, out _, out ulong shSize, out uint shLink, out ulong shEntsize);

                if (shType != SHT_SYMTAB && shType != SHT_DYNSYM)
                {
                    continue;
                }

                if (shEntsize > 0)
                {
                    totalSymbolCount += (long)(shSize / shEntsize);
                }

                // Get the linked string table size.
                if (shLink == 0 || shLink >= sectionCount)
                {
                    continue;
                }

                int strtabShPos = (int)shLink * eShentsize;
                ReadSectionHeader(shTable, strtabShPos, m_is64Bit, m_bigEndian,
                    out _, out _, out ulong strtabSize, out _, out _);
                totalStrtabSize += (long)strtabSize;
            }

            // Pre-allocate with known sizes.
            m_strtab = new SegmentedList<byte>(StrtabSegmentSize, totalStrtabSize);
            m_symbols.Capacity = (int)totalSymbolCount;

            // Pass 2: Load strtabs and symbol entries.
            long strtabBaseOffset = 0;
            for (uint i = 0; i < sectionCount; i++)
            {
                int shPos = (int)i * eShentsize;
                ReadSectionHeader(shTable, shPos, m_is64Bit, m_bigEndian,
                    out uint shType, out ulong shOffset, out ulong shSize, out uint shLink, out ulong shEntsize);

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
                ReadSectionHeader(shTable, strtabShPos, m_is64Bit, m_bigEndian,
                    out _, out ulong strtabOffset, out ulong strtabSize, out _, out _);

                if (strtabSize == 0)
                {
                    continue;
                }

                // Read strtab in chunks and append to SegmentedList.
                stream.Seek((long)strtabOffset, SeekOrigin.Begin);
                long remaining = (long)strtabSize;
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
                byte[] symData = new byte[(long)shSize];
                stream.Seek((long)shOffset, SeekOrigin.Begin);
                if (ReadFully(stream, symData, 0, (int)shSize) < (long)shSize)
                {
                    strtabBaseOffset += (long)strtabSize;
                    continue;
                }

                ReadSymbolTable(symData, (long)shSize, (long)shEntsize, strtabBaseOffset);
                strtabBaseOffset += (long)strtabSize;
            }

            // Sort symbols by start address for binary search.
            m_symbols.Sort();
            m_symbolNames = new string[m_symbols.Count];
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

                if (m_is64Bit)
                {
                    var sym = ReadStruct<Elf64_Sym>(symData, pos);
                    if (m_bigEndian) sym.SwapEndian();
                    stName = sym.st_name;
                    stInfo = sym.st_info;
                    stValue = sym.st_value;
                    stSize = sym.st_size;
                }
                else
                {
                    var sym = ReadStruct<Elf32_Sym>(symData, pos);
                    if (m_bigEndian) sym.SwapEndian();
                    stName = sym.st_name;
                    stInfo = sym.st_info;
                    stValue = sym.st_value;
                    stSize = sym.st_size;
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
            int nhdrSize = Unsafe.SizeOf<Elf_Nhdr>();

            while (pos + nhdrSize <= length)
            {
                var nhdr = ReadStruct<Elf_Nhdr>(noteData, pos);
                if (bigEndian) nhdr.SwapEndian();
                uint namesz = nhdr.n_namesz;
                uint descsz = nhdr.n_descsz;
                uint type = nhdr.n_type;
                pos += nhdrSize;

                // Guard against uint overflow in alignment arithmetic: (x + 3) wraps
                // when x >= 0xFFFFFFFD, producing a small aligned value and an infinite loop.
                uint remaining = (uint)(length - pos);
                if (namesz > remaining || descsz > remaining)
                {
                    break;
                }

                // Align name and desc sizes to 4-byte boundaries.
                uint nameAligned = (namesz + 3) & ~3u;
                uint descAligned = (descsz + 3) & ~3u;
                uint noteSize = nameAligned + descAligned;

                // Validate that the note fits within the segment data.
                if (noteSize > remaining)
                {
                    break;
                }

                // Check for GNU build-id: name == "GNU\0" (namesz == 4) and type == NT_GNU_BUILD_ID (3).
                if (type == NT_GNU_BUILD_ID && namesz == GnuNoteNameSize &&
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

                pos += (int)noteSize;
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
                string demangled = m_itaniumDemangler.Value.Demangle(name);
                if (demangled != null)
                {
                    return demangled;
                }
            }

            if (name.StartsWith("_R"))
            {
                string demangled = m_rustDemangler.Value.Demangle(name);
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

        #endregion
    }
}
