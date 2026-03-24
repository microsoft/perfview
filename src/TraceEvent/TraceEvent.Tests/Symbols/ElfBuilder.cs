using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace TraceEventTests
{
    /// <summary>
    /// Builds minimal synthetic ELF binaries for testing ElfSymbolModule.
    /// Produces a valid ELF file with .strtab/.symtab and optionally .dynstr/.dynsym sections.
    /// </summary>
    internal class ElfBuilder
    {
        private bool m_is64Bit = true;
        private bool m_bigEndian = false;
        private ulong m_pVaddr = 0x400000;
        private ulong m_pOffset = 0;
        private byte[] m_buildId = null;
        private readonly List<SymbolDef> m_symtabSymbols = new List<SymbolDef>();
        private readonly List<SymbolDef> m_dynsymSymbols = new List<SymbolDef>();

        private struct SymbolDef
        {
            public string Name;
            public ulong Value;
            public ulong Size;
            public byte Info; // (bind << 4) | type
        }

        // ELF section types.
        private const uint SHT_NULL = 0;
        private const uint SHT_STRTAB = 3;
        private const uint SHT_SYMTAB = 2;
        private const uint SHT_DYNSYM = 11;

        // ELF program header types.
        private const uint PT_NOTE = 4;

        // Symbol type helpers.
        private const byte STT_FUNC = 2;
        private const byte STB_GLOBAL = 1;

        // GNU build-id note type.
        private const uint NT_GNU_BUILD_ID = 3;

        public ElfBuilder Set64Bit(bool is64Bit)
        {
            m_is64Bit = is64Bit;
            return this;
        }

        public ElfBuilder SetBigEndian(bool bigEndian)
        {
            m_bigEndian = bigEndian;
            return this;
        }

        /// <summary>
        /// Sets the PT_LOAD segment parameters that the caller passes to ElfSymbolModule's constructor.
        /// </summary>
        public ElfBuilder SetPTLoad(ulong pVaddr, ulong pOffset)
        {
            m_pVaddr = pVaddr;
            m_pOffset = pOffset;
            return this;
        }

        /// <summary>
        /// Sets the GNU build-id that will be embedded as a PT_NOTE program header.
        /// </summary>
        public ElfBuilder SetBuildId(byte[] buildId)
        {
            m_buildId = buildId;
            return this;
        }

        /// <summary>
        /// Adds a STT_FUNC symbol to the .symtab section.
        /// </summary>
        public ElfBuilder AddFunction(string name, ulong virtualAddress, ulong size)
        {
            m_symtabSymbols.Add(new SymbolDef
            {
                Name = name,
                Value = virtualAddress,
                Size = size,
                Info = (STB_GLOBAL << 4) | STT_FUNC,
            });
            return this;
        }

        /// <summary>
        /// Adds a symbol with a custom type to the .symtab section (for testing filtering).
        /// </summary>
        public ElfBuilder AddSymbol(string name, ulong virtualAddress, ulong size, byte symbolType)
        {
            m_symtabSymbols.Add(new SymbolDef
            {
                Name = name,
                Value = virtualAddress,
                Size = size,
                Info = (byte)((STB_GLOBAL << 4) | (symbolType & 0xf)),
            });
            return this;
        }

        /// <summary>
        /// Adds a STT_FUNC symbol to the .dynsym section.
        /// </summary>
        public ElfBuilder AddDynFunction(string name, ulong virtualAddress, ulong size)
        {
            m_dynsymSymbols.Add(new SymbolDef
            {
                Name = name,
                Value = virtualAddress,
                Size = size,
                Info = (STB_GLOBAL << 4) | STT_FUNC,
            });
            return this;
        }

        /// <summary>
        /// Builds a complete ELF binary and returns it as a byte array.
        /// Layout: [ELF Header] [Section Data...] [Note Data] [Program Headers] [Section Headers]
        /// </summary>
        public byte[] Build()
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // Determine sections we need:
                // [0] SHT_NULL (required)
                // [1] .strtab (string table for .symtab)
                // [2] .symtab (symbol table)
                // [3] .dynstr (string table for .dynsym) — only if dynsym symbols exist
                // [4] .dynsym — only if dynsym symbols exist
                bool hasDynsym = m_dynsymSymbols.Count > 0;
                bool hasBuildId = m_buildId != null;
                int sectionCount = hasDynsym ? 5 : 3;

                int ehSize = m_is64Bit ? 64 : 52;
                int shEntSize = m_is64Bit ? 64 : 40;
                int phEntSize = m_is64Bit ? 56 : 32;

                // Build string table for .symtab.
                byte[] strtab = BuildStringTable(m_symtabSymbols, out int[] strtabOffsets);

                // Build symbol table for .symtab.
                byte[] symtab = BuildSymbolTable(m_symtabSymbols, strtabOffsets);

                // Build dynstr/dynsym if needed.
                byte[] dynstr = null;
                byte[] dynsym = null;
                int[] dynstrOffsets = null;
                if (hasDynsym)
                {
                    dynstr = BuildStringTable(m_dynsymSymbols, out dynstrOffsets);
                    dynsym = BuildSymbolTable(m_dynsymSymbols, dynstrOffsets);
                }

                // Build note data for GNU build-id if requested.
                byte[] noteData = null;
                if (hasBuildId)
                {
                    noteData = BuildBuildIdNote(m_buildId);
                }

                // Section data starts right after the ELF header.
                long dataStart = ehSize;

                // Lay out section data sequentially.
                long strtabOffset = dataStart;
                long symtabOffset = strtabOffset + strtab.Length;
                long dynstrOffset = symtabOffset + symtab.Length;
                long dynsymOffset = hasDynsym ? dynstrOffset + dynstr.Length : dynstrOffset;
                long afterSections = hasDynsym ? dynsymOffset + dynsym.Length : dynstrOffset;

                // Write note data after sections.
                long noteOffset = afterSections;
                long afterNote = hasBuildId ? noteOffset + noteData.Length : afterSections;

                // Write program headers after note data (align to 8 bytes).
                long phOffset = 0;
                ushort phNum = 0;
                if (hasBuildId)
                {
                    phOffset = afterNote;
                    if (phOffset % 8 != 0)
                    {
                        phOffset += 8 - (phOffset % 8);
                    }
                    phNum = 1;
                }

                long afterPh = hasBuildId ? phOffset + phEntSize : afterNote;

                // Section headers follow everything else (align to 8 bytes).
                long sectionHeadersOffset = afterPh;
                if (sectionHeadersOffset % 8 != 0)
                {
                    sectionHeadersOffset += 8 - (sectionHeadersOffset % 8);
                }

                // Write ELF header.
                ushort headerPhEntSize = hasBuildId ? (ushort)phEntSize : (ushort)0;
                WriteElfHeader(writer, (ulong)sectionHeadersOffset, (ushort)sectionCount, (ushort)shEntSize,
                    (ulong)phOffset, headerPhEntSize, phNum);

                // Write section data.
                writer.BaseStream.Seek(strtabOffset, SeekOrigin.Begin);
                writer.Write(strtab);
                writer.Write(symtab);
                if (hasDynsym)
                {
                    writer.Write(dynstr);
                    writer.Write(dynsym);
                }

                // Write note data.
                if (hasBuildId)
                {
                    writer.BaseStream.Seek(noteOffset, SeekOrigin.Begin);
                    writer.Write(noteData);
                }

                // Write program headers.
                if (hasBuildId)
                {
                    writer.BaseStream.Seek(phOffset, SeekOrigin.Begin);
                    WriteProgramHeader(writer, PT_NOTE, (ulong)noteOffset, (ulong)noteData.Length);
                }

                // Pad to section header offset.
                while (writer.BaseStream.Position < sectionHeadersOffset)
                {
                    writer.Write((byte)0);
                }

                int symEntSize = m_is64Bit ? 24 : 16;

                // Section [0]: SHT_NULL
                WriteSectionHeader(writer, 0, SHT_NULL, 0, 0, 0, 0);

                // Section [1]: .strtab (SHT_STRTAB)
                WriteSectionHeader(writer, 0, SHT_STRTAB, (ulong)strtabOffset, (ulong)strtab.Length, 0, 0);

                // Section [2]: .symtab (SHT_SYMTAB), sh_link = 1 (index of .strtab)
                WriteSectionHeader(writer, 0, SHT_SYMTAB, (ulong)symtabOffset, (ulong)symtab.Length, 1, (ulong)symEntSize);

                if (hasDynsym)
                {
                    // Section [3]: .dynstr (SHT_STRTAB)
                    WriteSectionHeader(writer, 0, SHT_STRTAB, (ulong)dynstrOffset, (ulong)dynstr.Length, 0, 0);

                    // Section [4]: .dynsym (SHT_DYNSYM), sh_link = 3 (index of .dynstr)
                    WriteSectionHeader(writer, 0, SHT_DYNSYM, (ulong)dynsymOffset, (ulong)dynsym.Length, 3, (ulong)symEntSize);
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Returns the pVaddr and pOffset values that should be passed to the ElfSymbolModule constructor.
        /// </summary>
        public void GetPTLoadParams(out ulong pVaddr, out ulong pOffset)
        {
            pVaddr = m_pVaddr;
            pOffset = m_pOffset;
        }

        #region Private helpers

        private void WriteElfHeader(BinaryWriter writer, ulong eShoff, ushort eShnum, ushort eShentsize,
            ulong ePhoff, ushort ePhentsize, ushort ePhnum)
        {
            // e_ident: magic + class + data + version + padding (16 bytes total).
            writer.Write((byte)0x7f);
            writer.Write((byte)'E');
            writer.Write((byte)'L');
            writer.Write((byte)'F');
            writer.Write((byte)(m_is64Bit ? 2 : 1));   // ei_class
            writer.Write((byte)(m_bigEndian ? 2 : 1));  // ei_data
            writer.Write((byte)1);                      // ei_version
            writer.Write((byte)0);                      // ei_osabi
            writer.Write(new byte[8]);                  // ei_abiversion + padding

            WriteUInt16(writer, 2);                     // e_type: ET_EXEC
            WriteUInt16(writer, (ushort)(m_is64Bit ? 0x3E : 0x03)); // e_machine: EM_X86_64 or EM_386
            WriteUInt32(writer, 1);                     // e_version

            if (m_is64Bit)
            {
                WriteUInt64(writer, 0);                 // e_entry
                WriteUInt64(writer, ePhoff);            // e_phoff
                WriteUInt64(writer, eShoff);            // e_shoff
            }
            else
            {
                WriteUInt32(writer, 0);                 // e_entry
                WriteUInt32(writer, (uint)ePhoff);      // e_phoff
                WriteUInt32(writer, (uint)eShoff);      // e_shoff
            }

            WriteUInt32(writer, 0);                     // e_flags
            WriteUInt16(writer, (ushort)(m_is64Bit ? 64 : 52)); // e_ehsize
            WriteUInt16(writer, ePhentsize);            // e_phentsize
            WriteUInt16(writer, ePhnum);                // e_phnum
            WriteUInt16(writer, eShentsize);            // e_shentsize
            WriteUInt16(writer, eShnum);                // e_shnum
            WriteUInt16(writer, 0);                     // e_shstrndx
        }

        private void WriteSectionHeader(BinaryWriter writer, uint shName, uint shType,
            ulong shOffset, ulong shSize, uint shLink, ulong shEntsize)
        {
            WriteUInt32(writer, shName);                // sh_name
            WriteUInt32(writer, shType);                // sh_type

            if (m_is64Bit)
            {
                WriteUInt64(writer, 0);                 // sh_flags
                WriteUInt64(writer, 0);                 // sh_addr
                WriteUInt64(writer, shOffset);          // sh_offset
                WriteUInt64(writer, shSize);            // sh_size
                WriteUInt32(writer, shLink);            // sh_link
                WriteUInt32(writer, 0);                 // sh_info
                WriteUInt64(writer, 1);                 // sh_addralign
                WriteUInt64(writer, shEntsize);         // sh_entsize
            }
            else
            {
                WriteUInt32(writer, 0);                 // sh_flags
                WriteUInt32(writer, 0);                 // sh_addr
                WriteUInt32(writer, (uint)shOffset);    // sh_offset
                WriteUInt32(writer, (uint)shSize);      // sh_size
                WriteUInt32(writer, shLink);            // sh_link
                WriteUInt32(writer, 0);                 // sh_info
                WriteUInt32(writer, 1);                 // sh_addralign
                WriteUInt32(writer, (uint)shEntsize);   // sh_entsize
            }
        }

        /// <summary>
        /// Builds a null-terminated string table. Returns the raw bytes and the offset of each name.
        /// Index 0 is always a null byte (ELF convention).
        /// </summary>
        private byte[] BuildStringTable(List<SymbolDef> symbols, out int[] offsets)
        {
            offsets = new int[symbols.Count];
            using (var ms = new MemoryStream())
            {
                ms.WriteByte(0); // Index 0 is always null.

                for (int i = 0; i < symbols.Count; i++)
                {
                    offsets[i] = (int)ms.Position;
                    byte[] nameBytes = Encoding.UTF8.GetBytes(symbols[i].Name);
                    ms.Write(nameBytes, 0, nameBytes.Length);
                    ms.WriteByte(0); // null terminator
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Builds a symbol table section. Includes the mandatory null symbol at index 0.
        /// </summary>
        private byte[] BuildSymbolTable(List<SymbolDef> symbols, int[] strtabOffsets)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                // Symbol [0]: STN_UNDEF (null symbol, required by ELF spec).
                WriteSymbolEntry(writer, 0, 0, 0, 0);

                for (int i = 0; i < symbols.Count; i++)
                {
                    WriteSymbolEntry(writer, (uint)strtabOffsets[i], symbols[i].Value,
                        symbols[i].Size, symbols[i].Info);
                }

                return ms.ToArray();
            }
        }

        private void WriteSymbolEntry(BinaryWriter writer, uint stName, ulong stValue, ulong stSize, byte stInfo)
        {
            if (m_is64Bit)
            {
                // Elf64_Sym: st_name(4), st_info(1), st_other(1), st_shndx(2), st_value(8), st_size(8)
                WriteUInt32(writer, stName);
                writer.Write(stInfo);
                writer.Write((byte)0);              // st_other
                WriteUInt16(writer, 1);             // st_shndx (non-zero = defined)
                WriteUInt64(writer, stValue);
                WriteUInt64(writer, stSize);
            }
            else
            {
                // Elf32_Sym: st_name(4), st_value(4), st_size(4), st_info(1), st_other(1), st_shndx(2)
                WriteUInt32(writer, stName);
                WriteUInt32(writer, (uint)stValue);
                WriteUInt32(writer, (uint)stSize);
                writer.Write(stInfo);
                writer.Write((byte)0);              // st_other
                WriteUInt16(writer, 1);             // st_shndx
            }
        }

        /// <summary>
        /// Builds a .note.gnu.build-id note: namesz(4) + descsz(4) + type(4) + "GNU\0" + buildId.
        /// </summary>
        private byte[] BuildBuildIdNote(byte[] buildId)
        {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                WriteUInt32(writer, 4);                     // namesz: length of "GNU\0"
                WriteUInt32(writer, (uint)buildId.Length);   // descsz: length of build-id
                WriteUInt32(writer, NT_GNU_BUILD_ID);       // type: NT_GNU_BUILD_ID (3)
                writer.Write((byte)'G');                    // name: "GNU\0" (already 4-byte aligned)
                writer.Write((byte)'N');
                writer.Write((byte)'U');
                writer.Write((byte)0);
                writer.Write(buildId);                      // desc: build-id bytes

                // Pad descriptor to 4-byte alignment.
                int descPadding = ((buildId.Length + 3) & ~3) - buildId.Length;
                for (int i = 0; i < descPadding; i++)
                {
                    writer.Write((byte)0);
                }

                return ms.ToArray();
            }
        }

        /// <summary>
        /// Writes a single ELF program header entry (PT_NOTE).
        /// </summary>
        private void WriteProgramHeader(BinaryWriter writer, uint pType, ulong pOffset, ulong pFilesz)
        {
            if (m_is64Bit)
            {
                // Elf64_Phdr: p_type(4), p_flags(4), p_offset(8), p_vaddr(8), p_paddr(8), p_filesz(8), p_memsz(8), p_align(8)
                WriteUInt32(writer, pType);             // p_type
                WriteUInt32(writer, 0);                 // p_flags
                WriteUInt64(writer, pOffset);           // p_offset
                WriteUInt64(writer, 0);                 // p_vaddr
                WriteUInt64(writer, 0);                 // p_paddr
                WriteUInt64(writer, pFilesz);           // p_filesz
                WriteUInt64(writer, pFilesz);           // p_memsz
                WriteUInt64(writer, 4);                 // p_align
            }
            else
            {
                // Elf32_Phdr: p_type(4), p_offset(4), p_vaddr(4), p_paddr(4), p_filesz(4), p_memsz(4), p_flags(4), p_align(4)
                WriteUInt32(writer, pType);             // p_type
                WriteUInt32(writer, (uint)pOffset);     // p_offset
                WriteUInt32(writer, 0);                 // p_vaddr
                WriteUInt32(writer, 0);                 // p_paddr
                WriteUInt32(writer, (uint)pFilesz);     // p_filesz
                WriteUInt32(writer, (uint)pFilesz);     // p_memsz
                WriteUInt32(writer, 0);                 // p_flags
                WriteUInt32(writer, 4);                 // p_align
            }
        }

        #region Endianness helpers

        private void WriteUInt16(BinaryWriter writer, ushort val)
        {
            writer.Write(m_bigEndian ? SwapBytes(val) : val);
        }

        private void WriteUInt32(BinaryWriter writer, uint val)
        {
            writer.Write(m_bigEndian ? SwapBytes(val) : val);
        }

        private void WriteUInt64(BinaryWriter writer, ulong val)
        {
            writer.Write(m_bigEndian ? SwapBytes(val) : val);
        }

        private static ushort SwapBytes(ushort val)
        {
            return (ushort)((val >> 8) | (val << 8));
        }

        private static uint SwapBytes(uint val)
        {
            return ((val >> 24) & 0xFF)
                 | ((val >> 8) & 0xFF00)
                 | ((val << 8) & 0xFF0000)
                 | ((val << 24) & 0xFF000000);
        }

        private static ulong SwapBytes(ulong val)
        {
            return ((ulong)SwapBytes((uint)val) << 32) | SwapBytes((uint)(val >> 32));
        }

        #endregion

        #endregion
    }
}
