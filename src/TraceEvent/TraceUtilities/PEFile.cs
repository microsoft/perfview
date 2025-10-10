//  Copyright (c) Microsoft Corporation.  All rights reserved.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PEFile
{
    /// <summary>
    /// PEFile is a reader for the information in a Portable Exectable (PE) FILE.   This is what EXEs and DLLs are.  
    /// 
    /// It can read both 32 and 64 bit PE files.  
    /// </summary>
#if PEFILE_PUBLIC
    public
#endif
    sealed unsafe class PEFile : IDisposable
    {
        /// <summary>
        /// Create a new PEFile header reader that inspects the 
        /// </summary>
        public PEFile(string filePath)
        {
            m_stream = File.OpenRead(filePath);
            m_headerBuff = new PEBufferedReader(m_stream);

            PEBufferedSlice slice = m_headerBuff.EnsureRead(0, 1024);
            if (m_headerBuff.Length < 512)
            {
                goto ThrowBadHeader;
            }

            Header = new PEHeader(slice);

            if (Header.PEHeaderSize > 1024 * 64)      // prevent insane numbers;
            {
                goto ThrowBadHeader;
            }

            // We did not read in the complete header, Try again using the right sized buffer.  
            if (Header.PEHeaderSize > m_headerBuff.Length)
            {
                slice = m_headerBuff.EnsureRead(0, Header.PEHeaderSize);
                if (m_headerBuff.Length < Header.PEHeaderSize)
                {
                    goto ThrowBadHeader;
                }

                Header = new PEHeader(slice);
            }
            return;
            ThrowBadHeader:
            throw new InvalidOperationException("Bad PE Header in " + filePath);
        }
        /// <summary>
        /// The Header for the PE file.  This contains the infor in a link /dump /headers 
        /// </summary>
        public PEHeader Header { get; private set; }

        /// <summary>
        /// Looks up the debug signature information in the EXE.   Returns true and sets the parameters if it is found. 
        /// 
        /// If 'first' is true then the first entry is returned, otherwise (by default) the last entry is used 
        /// (this is what debuggers do today).   Thus NGEN images put the IL PDB last (which means debuggers 
        /// pick up that one), but we can set it to 'first' if we want the NGEN PDB.
        /// </summary>
        public bool GetPdbSignature(out string pdbName, out Guid pdbGuid, out int pdbAge, bool first = false)
        {
            pdbName = null;
            pdbGuid = Guid.Empty;
            pdbAge = 0;
            bool ret = false;

            if (Header.DebugDirectory.VirtualAddress != 0)
            {
                var buff = AllocBuff();
                var debugEntries = (IMAGE_DEBUG_DIRECTORY*)FetchRVA(Header.DebugDirectory.VirtualAddress, Header.DebugDirectory.Size, buff);
                Debug.Assert(Header.DebugDirectory.Size % sizeof(IMAGE_DEBUG_DIRECTORY) == 0);
                int debugCount = Header.DebugDirectory.Size / sizeof(IMAGE_DEBUG_DIRECTORY);
                for (int i = 0; i < debugCount; i++)
                {
                    if (debugEntries[i].Type == IMAGE_DEBUG_TYPE.CODEVIEW)
                    {
                        var stringBuff = AllocBuff();
                        var info = (CV_INFO_PDB70*)stringBuff.Fetch((int)debugEntries[i].PointerToRawData, debugEntries[i].SizeOfData);
                        if (info->CvSignature == CV_INFO_PDB70.PDB70CvSignature)
                        {
                            // If there are several this picks the last one.  
                            pdbGuid = info->Signature;
                            pdbAge = info->Age;
                            pdbName = info->PdbFileName;
                            ret = true;
                            if (first)
                            {
                                break;
                            }
                        }
                        FreeBuff(stringBuff);
                    }
                }
                FreeBuff(buff);
            }
            return ret;
        }
        /// <summary>
        /// Gets the File Version Information that is stored as a resource in the PE file.  (This is what the
        /// version tab a file's property page is populated with).  
        /// </summary>
        public FileVersionInfo GetFileVersionInfo()
        {
            var resources = GetResources();
            var versionNode = ResourceNode.GetChild(ResourceNode.GetChild(resources, "Version"), "1");
            if (versionNode == null)
            {
                return null;
            }

            if (!versionNode.IsLeaf && versionNode.Children.Count == 1)
            {
                versionNode = versionNode.Children[0];
            }

            var buff = AllocBuff();
            byte* bytes = versionNode.FetchData(0, versionNode.DataLength, buff);
            var ret = new FileVersionInfo(bytes, versionNode.DataLength);

            FreeBuff(buff);
            return ret;
        }
        /// <summary>
        /// For side by side dlls, the manifest that describes the binding information is stored as the RT_MANIFEST resource, and it
        /// is an XML string.   This routine returns this.  
        /// </summary>
        /// <returns></returns>
        public string GetSxSManfest()
        {
            var resources = GetResources();
            var manifest = ResourceNode.GetChild(ResourceNode.GetChild(resources, "RT_MANIFEST"), "1");
            if (manifest == null)
            {
                return null;
            }

            if (!manifest.IsLeaf && manifest.Children.Count == 1)
            {
                manifest = manifest.Children[0];
            }

            var buff = AllocBuff();
            byte* bytes = manifest.FetchData(0, manifest.DataLength, buff);
            string ret = null;
            using (var textReader = new StreamReader(new UnmanagedMemoryStream(bytes, manifest.DataLength)))
            {
                ret = textReader.ReadToEnd();
            }

            FreeBuff(buff);
            return ret;
        }

        /// <summary>
        /// Returns true if this is and NGEN or Ready-to-Run image (it has precompiled native code)
        /// </summary>
        public bool HasPrecompiledManagedCode
        {
            get
            {
                if (!getNativeInfoCalled)
                {
                    GetNativeInfo();
                }

                return hasPrecomiledManagedCode;
            }
        }

        /// <summary>
        /// Returns true if file has a managed ready-to-run image.  
        /// </summary>
        public bool IsManagedReadyToRun
        {
            get
            {
                if (!getNativeInfoCalled)
                {
                    GetNativeInfo();
                }

                return isManagedReadyToRun;
            }
        }

        /// <summary>
        /// Gets the major and minor ready-to-run version.   returns true if ready-to-run. 
        /// </summary>
        public bool ReadyToRunVersion(out short major, out short minor)
        {
            if (!getNativeInfoCalled)
            {
                GetNativeInfo();
            }

            major = readyToRunMajor;
            minor = readyToRunMinor;
            return isManagedReadyToRun;
        }

        /// <summary>
        /// Closes any file handles and cleans up resources.  
        /// </summary>
        public void Dispose()
        {
            // This method can only be called once on a given object.  
            m_stream.Dispose();
            m_headerBuff.Dispose();
            if (m_freeBuff != null)
            {
                m_freeBuff.Dispose();
            }
        }

        // TODO make public?
        internal ResourceNode GetResources()
        {
            if (Header.ResourceDirectory.VirtualAddress == 0 || Header.ResourceDirectory.Size < sizeof(IMAGE_RESOURCE_DIRECTORY))
            {
                return null;
            }

            var ret = new ResourceNode("", Header.FileOffsetOfResources, this, false, true);
            return ret;
        }

        #region private
        private bool getNativeInfoCalled;
        private bool hasPrecomiledManagedCode;
        private bool isManagedReadyToRun;
        private short readyToRunMajor;
        private short readyToRunMinor;

        private struct IMAGE_COR20_HEADER
        {
            // Header versioning
            public int cb;
            public short MajorRuntimeVersion;
            public short MinorRuntimeVersion;

            // Symbol table and startup information
            public IMAGE_DATA_DIRECTORY MetaData;
            public int Flags;

            public int EntryPointToken;
            public IMAGE_DATA_DIRECTORY Resources;
            public IMAGE_DATA_DIRECTORY StrongNameSignature;

            public IMAGE_DATA_DIRECTORY CodeManagerTable;
            public IMAGE_DATA_DIRECTORY VTableFixups;
            public IMAGE_DATA_DIRECTORY ExportAddressTableJumps;

            // Precompiled image info (internal use only - set to zero)
            public IMAGE_DATA_DIRECTORY ManagedNativeHeader;
        }

        private const int READYTORUN_SIGNATURE = 0x00525452; // 'RTR'

        private struct READYTORUN_HEADER
        {
            public int Signature;      // READYTORUN_SIGNATURE
            public short MajorVersion;   // READYTORUN_VERSION_XXX
            public short MinorVersion;

            public int Flags;          // READYTORUN_FLAG_XXX

            public int NumberOfSections;

            // Array of sections follows. The array entries are sorted by Type
            // READYTORUN_SECTION   Sections[];
        };

        public void GetNativeInfo()
        {
            if (getNativeInfoCalled)
            {
                return;
            }

            if (Header.ComDescriptorDirectory.VirtualAddress != 0 && sizeof(IMAGE_COR20_HEADER) <= Header.ComDescriptorDirectory.Size)
            {
                var buff = AllocBuff();
                var managedHeader = (IMAGE_COR20_HEADER*)FetchRVA(Header.ComDescriptorDirectory.VirtualAddress, sizeof(IMAGE_COR20_HEADER), buff);
                if (managedHeader->ManagedNativeHeader.VirtualAddress != 0)
                {
                    hasPrecomiledManagedCode = true;
                    if (sizeof(READYTORUN_HEADER) <= managedHeader->ManagedNativeHeader.Size)
                    {
                        var r2rHeader = (READYTORUN_HEADER*)FetchRVA(managedHeader->ManagedNativeHeader.VirtualAddress, sizeof(READYTORUN_HEADER), buff);
                        if (r2rHeader->Signature == READYTORUN_SIGNATURE)
                        {
                            isManagedReadyToRun = true;
                            readyToRunMajor = r2rHeader->MajorVersion;
                            readyToRunMinor = r2rHeader->MinorVersion;
                        }
                    }
                }
                FreeBuff(buff);
            }
        }

        private PEBufferedReader m_headerBuff;
        private PEBufferedReader m_freeBuff;
        private FileStream m_stream;

        internal byte* FetchRVA(int rva, int size, PEBufferedReader buffer)
        {
            return buffer.Fetch(Header.RvaToFileOffset(rva), size);
        }
        internal PEBufferedReader AllocBuff()
        {
            var ret = m_freeBuff;
            if (ret == null)
            {
                return new PEBufferedReader(m_stream);
            }

            m_freeBuff = null;
            return ret;
        }
        internal void FreeBuff(PEBufferedReader buffer)
        {
            if (m_freeBuff != null)
            {
                buffer.Dispose();           // Get rid of it, since we already have cached one
            }
            else
            {
                m_freeBuff = buffer;
            }
        }
        #endregion
    };

    /// <summary>
    /// A PEHeader is a reader of the data at the beginning of a PEFile.    If the header bytes of a 
    /// PEFile are read or mapped into memory, this class can parse it when given a buffer slice to it. 
    /// It can read both 32 and 64 bit PE files.  
    /// </summary>
#if PEFILE_PUBLIC
    public
#endif
    sealed unsafe class PEHeader
    {
        /// <summary>
        /// Returns a PEHeader that references an existing buffer without copying. Validates buffer bounds.
        /// </summary>
        internal PEHeader(PEBufferedSlice slice)
        {
            m_slice = slice;
            
            var span = slice.AsSpan();
            if (span.Length < sizeof(IMAGE_DOS_HEADER))
            {
                goto ThrowBadHeader;
            }

            IMAGE_DOS_HEADER dosHdr = MemoryMarshal.Read<IMAGE_DOS_HEADER>(span);
            
            if (dosHdr.e_magic != IMAGE_DOS_HEADER.IMAGE_DOS_SIGNATURE)
            {
                goto ThrowBadHeader;
            }

            var imageHeaderOffset = dosHdr.e_lfanew;
            if (!(sizeof(IMAGE_DOS_HEADER) <= imageHeaderOffset && imageHeaderOffset <= 512))
            {
                goto ThrowBadHeader;
            }

            if (span.Length < imageHeaderOffset + sizeof(IMAGE_NT_HEADERS))
            {
                goto ThrowBadHeader;
            }

            m_ntHeaderOffset = imageHeaderOffset;
            IMAGE_NT_HEADERS ntHdr = MemoryMarshal.Read<IMAGE_NT_HEADERS>(span.Slice(m_ntHeaderOffset));

            var optionalHeaderSize = ntHdr.FileHeader.SizeOfOptionalHeader;
            if (!(sizeof(IMAGE_NT_HEADERS) + sizeof(IMAGE_OPTIONAL_HEADER32) <= optionalHeaderSize))
            {
                goto ThrowBadHeader;
            }

            m_sectionsOffset = m_ntHeaderOffset + sizeof(IMAGE_NT_HEADERS) + ntHdr.FileHeader.SizeOfOptionalHeader;
            if (m_sectionsOffset >= 1024)
            {
                goto ThrowBadHeader;
            }

            if (span.Length < m_sectionsOffset + sizeof(IMAGE_SECTION_HEADER) * ntHdr.FileHeader.NumberOfSections)
            {
                goto ThrowBadHeader;
            }

            return;
            ThrowBadHeader:
            throw new InvalidOperationException("Bad PE Header.");
        }

        /// <summary>
        /// The total s,ize of the header,  including section array of the the PE header.  
        /// </summary>
        public int PEHeaderSize
        {
            get
            {
                return m_sectionsOffset + sizeof(IMAGE_SECTION_HEADER) * NtHeader.FileHeader.NumberOfSections;
            }
        }

        /// <summary>
        /// Given a relative virtual address (displacement from start of the image) return a offset in the file data for that data.  
        /// </summary>
        public int RvaToFileOffset(int rva)
        {
            ushort numSections = NtHeader.FileHeader.NumberOfSections;
            for (int i = 0; i < numSections; i++)
            {
                ref readonly IMAGE_SECTION_HEADER section = ref GetSectionHeader(i);
                if (section.VirtualAddress <= rva && rva < section.VirtualAddress + section.VirtualSize)
                {
                    return (int)section.PointerToRawData + (rva - (int)section.VirtualAddress);
                }
            }
            throw new InvalidOperationException("Illegal RVA 0x" + rva.ToString("x"));
        }

        /// <summary>
        /// Returns true if this is PE file for a 64 bit architecture.  
        /// </summary>
        public bool IsPE64 { get { return OptionalHeader32Span.Magic == 0x20b; } }
        /// <summary>
        /// Returns true if this file contains managed code (might also contain native code). 
        /// </summary>
        public bool IsManaged { get { return ComDescriptorDirectory.VirtualAddress != 0; } }

        // fields of code:IMAGE_NT_HEADERS
        /// <summary>   
        /// Returns the 'Signature' of the PE HEader PE\0\0 = 0x4550, used for sanity checking.  
        /// </summary>
        public uint Signature { get { return NtHeader.Signature; } }

        // fields of code:IMAGE_FILE_HEADER
        /// <summary>
        /// The machine this PE file is intended to run on 
        /// </summary>
        public MachineType Machine { get { return (MachineType)NtHeader.FileHeader.Machine; } }
        /// <summary>
        /// PE files have a number of sections that represent regions of memory with the access permisions.  This is the nubmer of such sections.  
        /// </summary>
        public ushort NumberOfSections { get { return NtHeader.FileHeader.NumberOfSections; } }
        /// <summary>
        /// The the PE file was created represented as the number of seconds since Jan 1 1970 
        /// </summary>
        public int TimeDateStampSec { get { return (int)NtHeader.FileHeader.TimeDateStamp; } }
        /// <summary>
        /// The the PE file was created represented as a DateTime object
        /// </summary>
        public DateTime TimeDateStamp
        {
            get
            {
                return TimeDateStampToDate(TimeDateStampSec);
            }
        }

        /// <summary>
        /// PointerToSymbolTable (see IMAGE_FILE_HEADER in PE File spec)
        /// </summary>
        public ulong PointerToSymbolTable { get { return NtHeader.FileHeader.PointerToSymbolTable; } }
        /// <summary>
        /// NumberOfSymbols (see IMAGE_FILE_HEADER PE File spec)
        /// </summary>
        public ulong NumberOfSymbols { get { return NtHeader.FileHeader.NumberOfSymbols; } }
        /// <summary>
        /// SizeOfOptionalHeader (see IMAGE_FILE_HEADER PE File spec)
        /// </summary>
        public ushort SizeOfOptionalHeader { get { return NtHeader.FileHeader.SizeOfOptionalHeader; } }
        /// <summary>
        /// Characteristics (see IMAGE_FILE_HEADER PE File spec)
        /// </summary>
        public ushort Characteristics { get { return NtHeader.FileHeader.Characteristics; } }

        // fields of code:IMAGE_OPTIONAL_HEADER32 (or code:IMAGE_OPTIONAL_HEADER64)
        /// <summary>
        /// Magic (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public ushort Magic { get { return OptionalHeader32Span.Magic; } }
        /// <summary>
        /// MajorLinkerVersion (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public byte MajorLinkerVersion { get { return OptionalHeader32Span.MajorLinkerVersion; } }
        /// <summary>
        /// MinorLinkerVersion (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public byte MinorLinkerVersion { get { return OptionalHeader32Span.MinorLinkerVersion; } }
        /// <summary>
        /// SizeOfCode (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public uint SizeOfCode { get { return OptionalHeader32Span.SizeOfCode; } }
        /// <summary>
        /// SizeOfInitializedData (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public uint SizeOfInitializedData { get { return OptionalHeader32Span.SizeOfInitializedData; } }
        /// <summary>
        /// SizeOfUninitializedData (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public uint SizeOfUninitializedData { get { return OptionalHeader32Span.SizeOfUninitializedData; } }
        /// <summary>
        /// AddressOfEntryPoint (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public uint AddressOfEntryPoint { get { return OptionalHeader32Span.AddressOfEntryPoint; } }
        /// <summary>
        /// BaseOfCode (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public uint BaseOfCode { get { return OptionalHeader32Span.BaseOfCode; } }

        // These depend on the whether you are PE32 or PE64
        /// <summary>
        /// ImageBase (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public ulong ImageBase
        {
            get
            {
                if (IsPE64)
                {
                    return OptionalHeader64Span.ImageBase;
                }
                else
                {
                    return OptionalHeader32Span.ImageBase;
                }
            }
        }
        /// <summary>
        /// SectionAlignment (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public uint SectionAlignment
        {
            get
            {
                if (IsPE64)
                {
                    return OptionalHeader64Span.SectionAlignment;
                }
                else
                {
                    return OptionalHeader32Span.SectionAlignment;
                }
            }
        }
        /// <summary>
        /// FileAlignment (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public uint FileAlignment
        {
            get
            {
                if (IsPE64)
                {
                    return OptionalHeader64Span.FileAlignment;
                }
                else
                {
                    return OptionalHeader32Span.FileAlignment;
                }
            }
        }
        /// <summary>
        /// MajorOperatingSystemVersion (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public ushort MajorOperatingSystemVersion
        {
            get
            {
                if (IsPE64)
                {
                    return OptionalHeader64Span.MajorOperatingSystemVersion;
                }
                else
                {
                    return OptionalHeader32Span.MajorOperatingSystemVersion;
                }
            }
        }
        /// <summary>
        /// MinorOperatingSystemVersion (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public ushort MinorOperatingSystemVersion
        {
            get
            {
                if (IsPE64)
                {
                    return OptionalHeader64Span.MinorOperatingSystemVersion;
                }
                else
                {
                    return OptionalHeader32Span.MinorOperatingSystemVersion;
                }
            }
        }
        /// <summary>
        /// MajorImageVersion (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public ushort MajorImageVersion
        {
            get
            {
                if (IsPE64)
                {
                    return OptionalHeader64Span.MajorImageVersion;
                }
                else
                {
                    return OptionalHeader32Span.MajorImageVersion;
                }
            }
        }
        /// <summary>
        /// MinorImageVersion (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public ushort MinorImageVersion
        {
            get
            {
                if (IsPE64)
                {
                    return OptionalHeader64Span.MinorImageVersion;
                }
                else
                {
                    return OptionalHeader32Span.MinorImageVersion;
                }
            }
        }
        /// <summary>
        /// MajorSubsystemVersion (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public ushort MajorSubsystemVersion
        {
            get
            {
                if (IsPE64)
                {
                    return OptionalHeader64Span.MajorSubsystemVersion;
                }
                else
                {
                    return OptionalHeader32Span.MajorSubsystemVersion;
                }
            }
        }
        /// <summary>
        /// MinorSubsystemVersion (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public ushort MinorSubsystemVersion
        {
            get
            {
                if (IsPE64)
                {
                    return OptionalHeader64Span.MinorSubsystemVersion;
                }
                else
                {
                    return OptionalHeader32Span.MinorSubsystemVersion;
                }
            }
        }
        /// <summary>
        /// Win32VersionValue (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public uint Win32VersionValue
        {
            get
            {
                if (IsPE64)
                {
                    return OptionalHeader64Span.Win32VersionValue;
                }
                else
                {
                    return OptionalHeader32Span.Win32VersionValue;
                }
            }
        }
        /// <summary>
        /// SizeOfImage (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public uint SizeOfImage
        {
            get
            {
                if (IsPE64)
                {
                    return OptionalHeader64Span.SizeOfImage;
                }
                else
                {
                    return OptionalHeader32Span.SizeOfImage;
                }
            }
        }
        /// <summary>
        /// SizeOfHeaders (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public uint SizeOfHeaders
        {
            get
            {
                if (IsPE64)
                {
                    return OptionalHeader64Span.SizeOfHeaders;
                }
                else
                {
                    return OptionalHeader32Span.SizeOfHeaders;
                }
            }
        }
        /// <summary>
        /// CheckSum (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public uint CheckSum
        {
            get
            {
                if (IsPE64)
                {
                    return OptionalHeader64Span.CheckSum;
                }
                else
                {
                    return OptionalHeader32Span.CheckSum;
                }
            }
        }
        /// <summary>
        /// Subsystem (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public ushort Subsystem
        {
            get
            {
                if (IsPE64)
                {
                    return OptionalHeader64Span.Subsystem;
                }
                else
                {
                    return OptionalHeader32Span.Subsystem;
                }
            }
        }
        /// <summary>
        /// DllCharacteristics (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public ushort DllCharacteristics
        {
            get
            {
                if (IsPE64)
                {
                    return OptionalHeader64Span.DllCharacteristics;
                }
                else
                {
                    return OptionalHeader32Span.DllCharacteristics;
                }
            }
        }
        /// <summary>
        /// SizeOfStackReserve (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public ulong SizeOfStackReserve
        {
            get
            {
                if (IsPE64)
                {
                    return OptionalHeader64Span.SizeOfStackReserve;
                }
                else
                {
                    return OptionalHeader32Span.SizeOfStackReserve;
                }
            }
        }
        /// <summary>
        /// SizeOfStackCommit (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public ulong SizeOfStackCommit
        {
            get
            {
                if (IsPE64)
                {
                    return OptionalHeader64Span.SizeOfStackCommit;
                }
                else
                {
                    return OptionalHeader32Span.SizeOfStackCommit;
                }
            }
        }
        /// <summary>
        /// SizeOfHeapReserve (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public ulong SizeOfHeapReserve
        {
            get
            {
                if (IsPE64)
                {
                    return OptionalHeader64Span.SizeOfHeapReserve;
                }
                else
                {
                    return OptionalHeader32Span.SizeOfHeapReserve;
                }
            }
        }
        /// <summary>
        /// SizeOfHeapCommit (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public ulong SizeOfHeapCommit
        {
            get
            {
                if (IsPE64)
                {
                    return OptionalHeader64Span.SizeOfHeapCommit;
                }
                else
                {
                    return OptionalHeader32Span.SizeOfHeapCommit;
                }
            }
        }
        /// <summary>
        /// LoaderFlags (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public uint LoaderFlags
        {
            get
            {
                if (IsPE64)
                {
                    return OptionalHeader64Span.LoaderFlags;
                }
                else
                {
                    return OptionalHeader32Span.LoaderFlags;
                }
            }
        }
        /// <summary>
        /// NumberOfRvaAndSizes (see IMAGE_OPTIONAL_HEADER32 or IMAGE_OPTIONAL_HEADER64 in PE File spec)
        /// </summary>
        public uint NumberOfRvaAndSizes
        {
            get
            {
                if (IsPE64)
                {
                    return OptionalHeader64Span.NumberOfRvaAndSizes;
                }
                else
                {
                    return OptionalHeader32Span.NumberOfRvaAndSizes;
                }
            }
        }

        // Well known data blobs (directories)  
        /// <summary>
        /// Returns the data directory (virtual address an blob, of a data directory with index 'idx'.   14 are currently defined.
        /// </summary>
        public IMAGE_DATA_DIRECTORY Directory(int idx)
        {
            if (idx >= NumberOfRvaAndSizes)
            {
                return new IMAGE_DATA_DIRECTORY();
            }

            return GetDirectory(idx);
        }
        /// <summary>
        /// Returns the data directory for DLL Exports see PE file spec for more
        /// </summary>
        public IMAGE_DATA_DIRECTORY ExportDirectory { get { return Directory(0); } }
        /// <summary>
        /// Returns the data directory for DLL Imports see PE file spec for more
        /// </summary>
        public IMAGE_DATA_DIRECTORY ImportDirectory { get { return Directory(1); } }
        /// <summary>
        /// Returns the data directory for DLL Resources see PE file spec for more
        /// </summary>
        public IMAGE_DATA_DIRECTORY ResourceDirectory { get { return Directory(2); } }
        /// <summary>
        /// Returns the data directory for DLL Exceptions see PE file spec for more
        /// </summary>
        public IMAGE_DATA_DIRECTORY ExceptionDirectory { get { return Directory(3); } }
        /// <summary>
        /// Returns the data directory for DLL securiy certificates (Authenticode) see PE file spec for more
        /// </summary>
        public IMAGE_DATA_DIRECTORY CertificatesDirectory { get { return Directory(4); } }
        /// <summary>
        /// Returns the data directory Image Base Relocations (RELOCS) see PE file spec for more
        /// </summary>
        public IMAGE_DATA_DIRECTORY BaseRelocationDirectory { get { return Directory(5); } }
        /// <summary>
        /// Returns the data directory for Debug information see PE file spec for more
        /// </summary>
        public IMAGE_DATA_DIRECTORY DebugDirectory { get { return Directory(6); } }
        /// <summary>
        /// Returns the data directory for DLL Exports see PE file spec for more
        /// </summary>
        public IMAGE_DATA_DIRECTORY ArchitectureDirectory { get { return Directory(7); } }
        /// <summary>
        /// Returns the data directory for GlobalPointer (IA64) see PE file spec for more
        /// </summary>
        public IMAGE_DATA_DIRECTORY GlobalPointerDirectory { get { return Directory(8); } }
        /// <summary>
        /// Returns the data directory for THread local storage see PE file spec for more
        /// </summary>
        public IMAGE_DATA_DIRECTORY ThreadStorageDirectory { get { return Directory(9); } }
        /// <summary>
        /// Returns the data directory for Load Configuration see PE file spec for more
        /// </summary>
        public IMAGE_DATA_DIRECTORY LoadConfigurationDirectory { get { return Directory(10); } }
        /// <summary>
        /// Returns the data directory for Bound Imports see PE file spec for more
        /// </summary>
        public IMAGE_DATA_DIRECTORY BoundImportDirectory { get { return Directory(11); } }
        /// <summary>
        /// Returns the data directory for the DLL Import Address Table (IAT) see PE file spec for more
        /// </summary>
        public IMAGE_DATA_DIRECTORY ImportAddressTableDirectory { get { return Directory(12); } }
        /// <summary>
        /// Returns the data directory for Delayed Imports see PE file spec for more
        /// </summary>
        public IMAGE_DATA_DIRECTORY DelayImportDirectory { get { return Directory(13); } }
        /// <summary>
        ///  see PE file spec for more .NET Runtime infomration.  
        /// </summary>
        public IMAGE_DATA_DIRECTORY ComDescriptorDirectory { get { return Directory(14); } }

        #region private
        internal static DateTime TimeDateStampToDate(int timeDateStampSec)
        {
            // Convert seconds from Jan 1 1970 to DateTime ticks.  
            // The 621356004000000000L represents Jan 1 1970 as DateTime 100ns ticks.  
            DateTime ret = new DateTime(((long)(uint)timeDateStampSec) * 10000000 + 621356004000000000L, DateTimeKind.Utc).ToLocalTime();

            // Calculation above seems to be off by an hour  Don't know why 
            ret = ret.AddHours(-1.0);
            return ret;
        }

        internal int FileOffsetOfResources
        {
            get
            {
                if (ResourceDirectory.VirtualAddress == 0)
                {
                    return 0;
                }

                return RvaToFileOffset(ResourceDirectory.VirtualAddress);
            }
        }

        // Helper method to get a span from the buffer with bounds checking
        private ReadOnlySpan<byte> GetBufferSpan(int offset, int length)
        {
            var span = m_slice.AsSpan();
            if (offset < 0 || offset + length > span.Length)
            {
                throw new ArgumentOutOfRangeException($"Attempted to read {length} bytes at offset {offset}, but buffer is only {span.Length} bytes.");
            }
            return span.Slice(offset, length);
        }

        // Helper properties to access structures from span with bounds checking
        private ref readonly IMAGE_DOS_HEADER DosHeader
        {
            get
            {
                var span = GetBufferSpan(0, sizeof(IMAGE_DOS_HEADER));
                return ref MemoryMarshal.Cast<byte, IMAGE_DOS_HEADER>(span)[0];
            }
        }

        private ref readonly IMAGE_NT_HEADERS NtHeader
        {
            get
            {
                var span = GetBufferSpan(m_ntHeaderOffset, sizeof(IMAGE_NT_HEADERS));
                return ref MemoryMarshal.Cast<byte, IMAGE_NT_HEADERS>(span)[0];
            }
        }

        private ref readonly IMAGE_SECTION_HEADER GetSectionHeader(int index)
        {
            int offset = m_sectionsOffset + index * sizeof(IMAGE_SECTION_HEADER);
            var span = GetBufferSpan(offset, sizeof(IMAGE_SECTION_HEADER));
            return ref MemoryMarshal.Cast<byte, IMAGE_SECTION_HEADER>(span)[0];
        }

        private ref readonly IMAGE_OPTIONAL_HEADER32 OptionalHeader32Span
        {
            get
            {
                int offset = m_ntHeaderOffset + sizeof(IMAGE_NT_HEADERS);
                var span = GetBufferSpan(offset, sizeof(IMAGE_OPTIONAL_HEADER32));
                return ref MemoryMarshal.Cast<byte, IMAGE_OPTIONAL_HEADER32>(span)[0];
            }
        }

        private ref readonly IMAGE_OPTIONAL_HEADER64 OptionalHeader64Span
        {
            get
            {
                int offset = m_ntHeaderOffset + sizeof(IMAGE_NT_HEADERS);
                var span = GetBufferSpan(offset, sizeof(IMAGE_OPTIONAL_HEADER64));
                return ref MemoryMarshal.Cast<byte, IMAGE_OPTIONAL_HEADER64>(span)[0];
            }
        }

        private ref readonly IMAGE_DATA_DIRECTORY GetDirectory(int index)
        {
            int dirOffset;
            if (IsPE64)
            {
                dirOffset = m_ntHeaderOffset + sizeof(IMAGE_NT_HEADERS) + sizeof(IMAGE_OPTIONAL_HEADER64);
            }
            else
            {
                dirOffset = m_ntHeaderOffset + sizeof(IMAGE_NT_HEADERS) + sizeof(IMAGE_OPTIONAL_HEADER32);
            }
            dirOffset += index * sizeof(IMAGE_DATA_DIRECTORY);
            var span = GetBufferSpan(dirOffset, sizeof(IMAGE_DATA_DIRECTORY));
            return ref MemoryMarshal.Cast<byte, IMAGE_DATA_DIRECTORY>(span)[0];
        }

        // Span-based fields
        private PEBufferedSlice m_slice;
        private int m_ntHeaderOffset;
        private int m_sectionsOffset;
        #endregion
    }

    /// <summary>
    /// The Machine types supported by the portable executable (PE) File format
    /// </summary>
#if PEFILE_PUBLIC
    public
#endif
    enum MachineType : ushort
    {
        /// <summary>
        /// Unknown machine type
        /// </summary>
        Native = 0,
        /// <summary>
        /// Intel X86 CPU 
        /// </summary>
        X86 = 0x014c,
        /// <summary>
        /// Intel IA64 
        /// </summary>
        ia64 = 0x0200,
        /// <summary>
        /// ARM 32 bit 
        /// </summary>
        ARM = 0x01c0,
        /// <summary>
        /// Arm 64 bit 
        /// </summary>
        Amd64 = 0x8664,
    };

    /// <summary>
    /// Represents a Portable Executable (PE) Data directory.  This is just a well known optional 'Blob' of memory (has a starting point and size)
    /// </summary>
#if PEFILE_PUBLIC
    public
#endif
    struct IMAGE_DATA_DIRECTORY
    {
        /// <summary>
        /// The start of the data blob when the file is mapped into memory
        /// </summary>
        public int VirtualAddress;
        /// <summary>
        /// The length of the data blob.  
        /// </summary>
        public int Size;
    }

    /// <summary>
    /// FileVersionInfo represents the extended version formation that is optionally placed in the PE file resource area. 
    /// </summary>
#if PEFILE_PUBLIC
    public
#endif
    sealed unsafe class FileVersionInfo
    {
        // TODO incomplete, but this is all I need.  
        /// <summary>
        /// The version string 
        /// </summary>
        public string FileVersion { get; private set; }
        #region private 
        internal FileVersionInfo(byte* data, int dataLen)
        {
            FileVersion = "";
            if (dataLen <= 0x5c)
            {
                return;
            }

            // See http://msdn.microsoft.com/en-us/library/ms647001(v=VS.85).aspx
            byte* stringInfoPtr = data + 0x5c;   // Gets to first StringInfo

            // TODO hack, search for FileVersion string ... 
            string dataAsString = new string((char*)stringInfoPtr, 0, (dataLen - 0x5c) / 2);

            string fileVersionKey = "FileVersion";
            int fileVersionIdx = dataAsString.IndexOf(fileVersionKey);
            if (fileVersionIdx >= 0)
            {
                int valIdx = fileVersionIdx + fileVersionKey.Length;
                for (; ; )
                {
                    valIdx++;
                    if (valIdx >= dataAsString.Length)
                    {
                        return;
                    }

                    if (dataAsString[valIdx] != (char)0)
                    {
                        break;
                    }
                }
                int varEndIdx = dataAsString.IndexOf((char)0, valIdx);
                if (varEndIdx < 0)
                {
                    return;
                }

                FileVersion = dataAsString.Substring(valIdx, varEndIdx - valIdx);
            }
        }

        #endregion
    }

    #region private classes we may want to expose 

    /// <summary>
    /// Represents a slice of a buffered PE file with buffer, offset, and length information.
    /// </summary>
    internal struct PEBufferedSlice
    {
        public byte[] Buffer { get; }
        public int Offset { get; }
        public int Length { get; }

        public PEBufferedSlice(byte[] buffer, int offset, int length)
        {
            Buffer = buffer;
            Offset = offset;
            Length = length;
        }

        public ReadOnlySpan<byte> AsSpan()
        {
            return new ReadOnlySpan<byte>(Buffer, Offset, Length);
        }
    }

    /// <summary>
    /// A PEBufferedReader represents a buffer (efficient) scanner of the 
    /// </summary>
    internal sealed unsafe class PEBufferedReader : IDisposable
    {
        public PEBufferedReader(Stream stream, int buffSize = 512)
        {
            m_stream = stream;
            GetBuffer(buffSize);
        }
        public byte* Fetch(int filePos, int size)
        {
            if (size > m_buff.Length)
            {
                GetBuffer(size);
            }
            if (!(m_buffPos <= filePos && filePos + size <= m_buffPos + m_buffLen))
            {
                // Read in the block of 'size' bytes at filePos
                m_buffPos = filePos;
                m_stream.Seek(m_buffPos, SeekOrigin.Begin);
                m_buffLen = 0;
                while (m_buffLen < m_buff.Length)
                {
                    var count = m_stream.Read(m_buff, m_buffLen, size - m_buffLen);
                    if (count == 0)
                    {
                        break;
                    }

                    m_buffLen += count;
                }
            }
            return &m_buffPtr[filePos - m_buffPos];
        }
        public ReadOnlySpan<byte> FetchSpan(int filePos, int size)
        {
            if (size > m_buff.Length)
            {
                GetBuffer(size);
            }
            if (!(m_buffPos <= filePos && filePos + size <= m_buffPos + m_buffLen))
            {
                // Read in the block of 'size' bytes at filePos
                m_buffPos = filePos;
                m_stream.Seek(m_buffPos, SeekOrigin.Begin);
                m_buffLen = 0;
                while (m_buffLen < m_buff.Length)
                {
                    var count = m_stream.Read(m_buff, m_buffLen, size - m_buffLen);
                    if (count == 0)
                    {
                        break;
                    }

                    m_buffLen += count;
                }
            }
            int offset = filePos - m_buffPos;
            int actualSize = Math.Min(size, m_buffLen - offset);
            return new ReadOnlySpan<byte>(m_buff, offset, actualSize);
        }
        public int Length { get { return m_buffLen; } }

        // Internal method to ensure data is read and return buffer slice for zero-copy PEHeader construction
        internal PEBufferedSlice EnsureRead(int filePos, int size)
        {
            // Ensure the data is fetched
            FetchSpan(filePos, size);
            
            int offset = filePos - m_buffPos;
            int length = Math.Min(size, m_buffLen - offset);
            return new PEBufferedSlice(m_buff, offset, length);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            m_pinningHandle.Free();
        }
        #region private
        ~PEBufferedReader()
        {
            FreeBuffer();
        }

        private void FreeBuffer()
        {
            try
            {
                if (m_pinningHandle.IsAllocated)
                {
                    m_pinningHandle.Free();
                }
            }
            catch (Exception) { }
        }

        private void GetBuffer(int buffSize)
        {
            FreeBuffer();

            m_buff = new byte[buffSize];
            fixed (byte* ptr = m_buff)
            {
                m_buffPtr = ptr;
            }

            m_buffLen = 0;
            m_pinningHandle = GCHandle.Alloc(m_buff, GCHandleType.Pinned);
        }

        private int m_buffPos;
        private int m_buffLen;      // Number of valid bytes in m_buff
        private byte[] m_buff;
        private byte* m_buffPtr;
        private GCHandle m_pinningHandle;
        private Stream m_stream;
        #endregion
    }

    internal sealed unsafe class ResourceNode
    {
        public string Name { get; private set; }
        public bool IsLeaf { get; private set; }

        // If IsLeaf is true
        public int DataLength { get { return m_dataLen; } }
        public byte* FetchData(int offsetInResourceData, int size, PEBufferedReader buff)
        {
            return buff.Fetch(m_dataFileOffset + offsetInResourceData, size);
        }
        public FileVersionInfo GetFileVersionInfo()
        {
            var buff = m_file.AllocBuff();
            byte* bytes = FetchData(0, DataLength, buff);
            var ret = new FileVersionInfo(bytes, DataLength);
            m_file.FreeBuff(buff);
            return ret;
        }

        public override string ToString()
        {
            StringWriter sw = new StringWriter();
            ToString(sw, "");
            return sw.ToString();
        }

        public static ResourceNode GetChild(ResourceNode node, string name)
        {
            if (node == null)
            {
                return null;
            }

            foreach (var child in node.Children)
            {
                if (child.Name == name)
                {
                    return child;
                }
            }

            return null;
        }

        // If IsLeaf is false
        public List<ResourceNode> Children
        {
            get
            {
                if (m_Children == null && !IsLeaf)
                {
                    var buff = m_file.AllocBuff();
                    var resourceStartFileOffset = m_file.Header.FileOffsetOfResources;

                    IMAGE_RESOURCE_DIRECTORY* resourceHeader = (IMAGE_RESOURCE_DIRECTORY*)buff.Fetch(
                        m_nodeFileOffset, sizeof(IMAGE_RESOURCE_DIRECTORY));

                    int totalCount = resourceHeader->NumberOfNamedEntries + resourceHeader->NumberOfIdEntries;
                    int totalSize = totalCount * sizeof(IMAGE_RESOURCE_DIRECTORY_ENTRY);

                    IMAGE_RESOURCE_DIRECTORY_ENTRY* entries = (IMAGE_RESOURCE_DIRECTORY_ENTRY*)buff.Fetch(
                        m_nodeFileOffset + sizeof(IMAGE_RESOURCE_DIRECTORY), totalSize);

                    var nameBuff = m_file.AllocBuff();
                    m_Children = new List<ResourceNode>();
                    for (int i = 0; i < totalCount; i++)
                    {
                        var entry = &entries[i];
                        string entryName = null;
                        if (m_isTop)
                        {
                            entryName = IMAGE_RESOURCE_DIRECTORY_ENTRY.GetTypeNameForTypeId(entry->Id);
                        }
                        else
                        {
                            entryName = entry->GetName(nameBuff, resourceStartFileOffset);
                        }

                        Children.Add(new ResourceNode(entryName, resourceStartFileOffset + entry->DataOffset, m_file, entry->IsLeaf));
                    }
                    m_file.FreeBuff(nameBuff);
                    m_file.FreeBuff(buff);
                }
                return m_Children;
            }
        }

        #region private
        private void ToString(StringWriter sw, string indent)
        {
            sw.Write("{0}<ResourceNode", indent);
            sw.Write(" Name=\"{0}\"", Name);
            sw.Write(" IsLeaf=\"{0}\"", IsLeaf);

            if (IsLeaf)
            {
                sw.Write("DataLength=\"{0}\"", DataLength);
                sw.WriteLine("/>");
            }
            else
            {
                sw.Write("ChildCount=\"{0}\"", Children.Count);
                sw.WriteLine(">");
                foreach (var child in Children)
                {
                    child.ToString(sw, indent + "  ");
                }

                sw.WriteLine("{0}</ResourceNode>", indent);
            }
        }

        internal ResourceNode(string name, int nodeFileOffset, PEFile file, bool isLeaf, bool isTop = false)
        {
            m_file = file;
            m_nodeFileOffset = nodeFileOffset;
            m_isTop = isTop;
            IsLeaf = isLeaf;
            Name = name;

            if (isLeaf)
            {
                var buff = m_file.AllocBuff();
                IMAGE_RESOURCE_DATA_ENTRY* dataDescr = (IMAGE_RESOURCE_DATA_ENTRY*)buff.Fetch(nodeFileOffset, sizeof(IMAGE_RESOURCE_DATA_ENTRY));

                m_dataLen = dataDescr->Size;
                m_dataFileOffset = file.Header.RvaToFileOffset(dataDescr->RvaToData);
                var data = FetchData(0, m_dataLen, buff);
                m_file.FreeBuff(buff);
            }
        }

        private PEFile m_file;
        private int m_nodeFileOffset;
        private List<ResourceNode> m_Children;
        private bool m_isTop;
        private int m_dataLen;
        private int m_dataFileOffset;
        #endregion
    }
    #endregion

    #region private classes
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    internal struct IMAGE_DOS_HEADER
    {
        public const short IMAGE_DOS_SIGNATURE = 0x5A4D;       // MZ.  
        [FieldOffset(0)]
        public short e_magic;
        [FieldOffset(60)]
        public int e_lfanew;            // Offset to the IMAGE_FILE_HEADER
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct IMAGE_NT_HEADERS
    {
        public uint Signature;
        public IMAGE_FILE_HEADER FileHeader;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct IMAGE_FILE_HEADER
    {
        public ushort Machine;
        public ushort NumberOfSections;
        public uint TimeDateStamp;
        public uint PointerToSymbolTable;
        public uint NumberOfSymbols;
        public ushort SizeOfOptionalHeader;
        public ushort Characteristics;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct IMAGE_OPTIONAL_HEADER32
    {
        public ushort Magic;
        public byte MajorLinkerVersion;
        public byte MinorLinkerVersion;
        public uint SizeOfCode;
        public uint SizeOfInitializedData;
        public uint SizeOfUninitializedData;
        public uint AddressOfEntryPoint;
        public uint BaseOfCode;
        public uint BaseOfData;
        public uint ImageBase;
        public uint SectionAlignment;
        public uint FileAlignment;
        public ushort MajorOperatingSystemVersion;
        public ushort MinorOperatingSystemVersion;
        public ushort MajorImageVersion;
        public ushort MinorImageVersion;
        public ushort MajorSubsystemVersion;
        public ushort MinorSubsystemVersion;
        public uint Win32VersionValue;
        public uint SizeOfImage;
        public uint SizeOfHeaders;
        public uint CheckSum;
        public ushort Subsystem;
        public ushort DllCharacteristics;
        public uint SizeOfStackReserve;
        public uint SizeOfStackCommit;
        public uint SizeOfHeapReserve;
        public uint SizeOfHeapCommit;
        public uint LoaderFlags;
        public uint NumberOfRvaAndSizes;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    internal struct IMAGE_OPTIONAL_HEADER64
    {
        public ushort Magic;
        public byte MajorLinkerVersion;
        public byte MinorLinkerVersion;
        public uint SizeOfCode;
        public uint SizeOfInitializedData;
        public uint SizeOfUninitializedData;
        public uint AddressOfEntryPoint;
        public uint BaseOfCode;
        public ulong ImageBase;
        public uint SectionAlignment;
        public uint FileAlignment;
        public ushort MajorOperatingSystemVersion;
        public ushort MinorOperatingSystemVersion;
        public ushort MajorImageVersion;
        public ushort MinorImageVersion;
        public ushort MajorSubsystemVersion;
        public ushort MinorSubsystemVersion;
        public uint Win32VersionValue;
        public uint SizeOfImage;
        public uint SizeOfHeaders;
        public uint CheckSum;
        public ushort Subsystem;
        public ushort DllCharacteristics;
        public ulong SizeOfStackReserve;
        public ulong SizeOfStackCommit;
        public ulong SizeOfHeapReserve;
        public ulong SizeOfHeapCommit;
        public uint LoaderFlags;
        public uint NumberOfRvaAndSizes;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal unsafe struct IMAGE_SECTION_HEADER
    {
        public string Name
        {
            get
            {
                fixed (byte* ptr = NameBytes)
                {
                    if (ptr[7] == 0)
                    {
                        return System.Runtime.InteropServices.Marshal.PtrToStringAnsi((IntPtr)ptr);
                    }
                    else
                    {
                        return System.Runtime.InteropServices.Marshal.PtrToStringAnsi((IntPtr)ptr, 8);
                    }
                }
            }
        }
        public fixed byte NameBytes[8];
        public uint VirtualSize;
        public uint VirtualAddress;
        public uint SizeOfRawData;
        public uint PointerToRawData;
        public uint PointerToRelocations;
        public uint PointerToLinenumbers;
        public ushort NumberOfRelocations;
        public ushort NumberOfLinenumbers;
        public uint Characteristics;
    };

    internal struct IMAGE_DEBUG_DIRECTORY
    {
        public int Characteristics;
        public int TimeDateStamp;
        public short MajorVersion;
        public short MinorVersion;
        public IMAGE_DEBUG_TYPE Type;
        public int SizeOfData;
        public int AddressOfRawData;
        public int PointerToRawData;
    };

    internal enum IMAGE_DEBUG_TYPE
    {
        UNKNOWN = 0,
        COFF = 1,
        CODEVIEW = 2,
        FPO = 3,
        MISC = 4,
        BBT = 10,
    };

    internal unsafe struct CV_INFO_PDB70
    {
        public const int PDB70CvSignature = 0x53445352; // RSDS in ascii

        public int CvSignature;
        public Guid Signature;
        public int Age;
        public fixed byte bytePdbFileName[1];   // Actually variable sized. 
        public string PdbFileName
        {
            get
            {
                fixed (byte* ptr = bytePdbFileName)
                {
                    return System.Runtime.InteropServices.Marshal.PtrToStringAnsi((IntPtr)ptr);
                }
            }
        }
    };


    /* Resource information */
    // Resource directory consists of two counts, following by a variable length
    // array of directory entries.  The first count is the number of entries at
    // beginning of the array that have actual names associated with each entry.
    // The entries are in ascending order, case insensitive strings.  The second
    // count is the number of entries that immediately follow the named entries.
    // This second count identifies the number of entries that have 16-bit integer
    // Ids as their name.  These entries are also sorted in ascending order.
    //
    // This structure allows fast lookup by either name or number, but for any
    // given resource entry only one form of lookup is supported, not both.
    internal unsafe struct IMAGE_RESOURCE_DIRECTORY
    {
        public int Characteristics;
        public int TimeDateStamp;
        public short MajorVersion;
        public short MinorVersion;
        public ushort NumberOfNamedEntries;
        public ushort NumberOfIdEntries;
        //  IMAGE_RESOURCE_DIRECTORY_ENTRY DirectoryEntries[];
    };

    //
    // Each directory contains the 32-bit Name of the entry and an offset,
    // relative to the beginning of the resource directory of the data associated
    // with this directory entry.  If the name of the entry is an actual text
    // string instead of an integer Id, then the high order bit of the name field
    // is set to one and the low order 31-bits are an offset, relative to the
    // beginning of the resource directory of the string, which is of type
    // IMAGE_RESOURCE_DIRECTORY_STRING.  Otherwise the high bit is clear and the
    // low-order 16-bits are the integer Id that identify this resource directory
    // entry. If the directory entry is yet another resource directory (i.e. a
    // subdirectory), then the high order bit of the offset field will be
    // set to indicate this.  Otherwise the high bit is clear and the offset
    // field points to a resource data entry.
    internal unsafe struct IMAGE_RESOURCE_DIRECTORY_ENTRY
    {
        public bool IsStringName { get { return NameOffsetAndFlag < 0; } }
        public int NameOffset { get { return NameOffsetAndFlag & 0x7FFFFFFF; } }

        public bool IsLeaf { get { return (0x80000000 & DataOffsetAndFlag) == 0; } }
        public int DataOffset { get { return DataOffsetAndFlag & 0x7FFFFFFF; } }
        public int Id { get { return 0xFFFF & NameOffsetAndFlag; } }

        private int NameOffsetAndFlag;
        private int DataOffsetAndFlag;

        internal unsafe string GetName(PEBufferedReader buff, int resourceStartFileOffset)
        {
            if (IsStringName)
            {
                int nameLen = *((ushort*)buff.Fetch(NameOffset + resourceStartFileOffset, 2));
                char* namePtr = (char*)buff.Fetch(NameOffset + resourceStartFileOffset + 2, nameLen);
                return new string(namePtr);
            }
            else
            {
                return Id.ToString();
            }
        }

        internal static string GetTypeNameForTypeId(int typeId)
        {
            switch (typeId)
            {
                case 1:
                    return "Cursor";
                case 2:
                    return "BitMap";
                case 3:
                    return "Icon";
                case 4:
                    return "Menu";
                case 5:
                    return "Dialog";
                case 6:
                    return "String";
                case 7:
                    return "FontDir";
                case 8:
                    return "Font";
                case 9:
                    return "Accelerator";
                case 10:
                    return "RCData";
                case 11:
                    return "MessageTable";
                case 12:
                    return "GroupCursor";
                case 14:
                    return "GroupIcon";
                case 16:
                    return "Version";
                case 19:
                    return "PlugPlay";
                case 20:
                    return "Vxd";
                case 21:
                    return "Aniicursor";
                case 22:
                    return "Aniicon";
                case 23:
                    return "Html";
                case 24:
                    return "RT_MANIFEST";
            }
            return typeId.ToString();
        }
    }

    // Each resource data entry describes a leaf node in the resource directory
    // tree.  It contains an offset, relative to the beginning of the resource
    // directory of the data for the resource, a size field that gives the number
    // of bytes of data at that offset, a CodePage that should be used when
    // decoding code point values within the resource data.  Typically for new
    // applications the code page would be the unicode code page.
    internal unsafe struct IMAGE_RESOURCE_DATA_ENTRY
    {
        public int RvaToData;
        public int Size;
        public int CodePage;
        public int Reserved;
    };

    #endregion
}
