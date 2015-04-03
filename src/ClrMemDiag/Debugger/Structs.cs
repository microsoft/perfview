using System;
using System.Runtime.InteropServices;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct IMAGEHLP_MODULE64
    {
        private const int MAX_PATH = 260;

        public UInt32 SizeOfStruct;
        public UInt64 BaseOfImage;
        public UInt32 ImageSize;
        public UInt32 TimeDateStamp;
        public UInt32 CheckSum;
        public UInt32 NumSyms;
        public DEBUG_SYMTYPE SymType;
        private fixed char _ModuleName [32];
        private fixed char _ImageName [256];
        private fixed char _LoadedImageName [256];
        private fixed char _LoadedPdbName [256];
        public UInt32 CVSig;
        public fixed char CVData [MAX_PATH*3];
        public UInt32 PdbSig;
        public Guid PdbSig70;
        public UInt32 PdbAge;
        private UInt32 bPdbUnmatched; /* BOOL */
        private UInt32 bDbgUnmatched; /* BOOL */
        private UInt32 bLineNumbers; /* BOOL */
        private UInt32 bGlobalSymbols; /* BOOL */
        private UInt32 bTypeInfo; /* BOOL */
        private UInt32 bSourceIndexed; /* BOOL */
        private UInt32 bPublics; /* BOOL */

        public bool PdbUnmatched
        {
            get { return bPdbUnmatched != 0; }
            set { bPdbUnmatched = value ? 1U : 0U; }
        }

        public bool DbgUnmatched
        {
            get { return bDbgUnmatched != 0; }
            set { bDbgUnmatched = value ? 1U : 0U; }
        }

        public bool LineNumbers
        {
            get { return bLineNumbers != 0; }
            set { bLineNumbers = value ? 1U : 0U; }
        }

        public bool GlobalSymbols
        {
            get { return bGlobalSymbols != 0; }
            set { bGlobalSymbols = value ? 1U : 0U; }
        }

        public bool TypeInfo
        {
            get { return bTypeInfo != 0; }
            set { bTypeInfo = value ? 1U : 0U; }
        }

        public bool SourceIndexed
        {
            get { return bSourceIndexed != 0; }
            set { bSourceIndexed = value ? 1U : 0U; }
        }

        public bool Publics
        {
            get { return bPublics != 0; }
            set { bPublics = value ? 1U : 0U; }
        }

        public string ModuleName
        {
            get
            {
                fixed (char* moduleNamePtr = this._ModuleName)
                {
                    return Marshal.PtrToStringUni((IntPtr)moduleNamePtr, 32);
                }
            }
        }

        public string ImageName
        {
            get
            {
                fixed (char* imageNamePtr = this._ImageName)
                {
                    return Marshal.PtrToStringUni((IntPtr)imageNamePtr, 256);
                }
            }
        }

        public string LoadedImageName
        {
            get
            {
                fixed (char* loadedImageNamePtr = this._LoadedImageName)
                {
                    return Marshal.PtrToStringUni((IntPtr)loadedImageNamePtr, 256);
                }
            }
        }

        public string LoadedPdbName
        {
            get
            {
                fixed (char* loadedPdbNamePtr = this._LoadedPdbName)
                {
                    return Marshal.PtrToStringUni((IntPtr)loadedPdbNamePtr, 256);
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_THREAD_BASIC_INFORMATION
    {
        public DEBUG_TBINFO Valid;
        public UInt32 ExitStatus;
        public UInt32 PriorityClass;
        public UInt32 Priority;
        public UInt64 CreateTime;
        public UInt64 ExitTime;
        public UInt64 KernelTime;
        public UInt64 UserTime;
        public UInt64 StartOffset;
        public UInt64 Affinity;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_READ_USER_MINIDUMP_STREAM
    {
        public UInt32 StreamType;
        public UInt32 Flags;
        public UInt64 Offset;
        public IntPtr Buffer;
        public UInt32 BufferSize;
        public UInt32 BufferUsed;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_GET_TEXT_COMPLETIONS_IN
    {
        public DEBUG_GET_TEXT_COMPLETIONS Flags;
        public UInt32 MatchCountLimit;
        public UInt64 Reserved0;
        public UInt64 Reserved1;
        public UInt64 Reserved2;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_GET_TEXT_COMPLETIONS_OUT
    {
        public DEBUG_GET_TEXT_COMPLETIONS Flags;
        public UInt32 ReplaceIndex;
        public UInt32 MatchCount;
        public UInt32 Reserved1;
        public UInt64 Reserved2;
        public UInt64 Reserved3;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_CACHED_SYMBOL_INFO
    {
        public UInt64 ModBase;
        public UInt64 Arg1;
        public UInt64 Arg2;
        public UInt32 Id;
        public UInt32 Arg3;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_CREATE_PROCESS_OPTIONS
    {
        public DEBUG_CREATE_PROCESS CreateFlags;
        public DEBUG_ECREATE_PROCESS EngCreateFlags;
        public uint VerifierFlags;
        public uint Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct EXCEPTION_RECORD64
    {
        public UInt32 ExceptionCode;
        public UInt32 ExceptionFlags;
        public UInt64 ExceptionRecord;
        public UInt64 ExceptionAddress;
        public UInt32 NumberParameters;
        public UInt32 __unusedAlignment;
        public fixed UInt64 ExceptionInformation [15]; //EXCEPTION_MAXIMUM_PARAMETERS
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_BREAKPOINT_PARAMETERS
    {
        public UInt64 Offset;
        public UInt32 Id;
        public DEBUG_BREAKPOINT_TYPE BreakType;
        public UInt32 ProcType;
        public DEBUG_BREAKPOINT_FLAG Flags;
        public UInt32 DataSize;
        public DEBUG_BREAKPOINT_ACCESS_TYPE DataAccessType;
        public UInt32 PassCount;
        public UInt32 CurrentPassCount;
        public UInt32 MatchThread;
        public UInt32 CommandSize;
        public UInt32 OffsetExpressionSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_REGISTER_DESCRIPTION
    {
        public DEBUG_VALUE_TYPE Type;
        public DEBUG_REGISTER Flags;
        public UInt64 SubregMaster;
        public UInt64 SubregLength;
        public UInt64 SubregMask;
        public UInt64 SubregShift;
        public UInt64 Reserved0;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct I64PARTS32
    {
        [FieldOffset(0)] public UInt32 LowPart;
        [FieldOffset(4)] public UInt32 HighPart;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct F128PARTS64
    {
        [FieldOffset(0)] public UInt64 LowPart;
        [FieldOffset(8)] public UInt64 HighPart;
    }

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct DEBUG_VALUE
    {
        [FieldOffset(0)] public byte I8;
        [FieldOffset(0)] public ushort I16;
        [FieldOffset(0)] public uint I32;
        [FieldOffset(0)] public ulong I64;
        [FieldOffset(8)] public uint Nat;
        [FieldOffset(0)] public float F32;
        [FieldOffset(0)] public double F64;
        [FieldOffset(0)] public fixed byte F80Bytes [10];
        [FieldOffset(0)] public fixed byte F82Bytes [11];
        [FieldOffset(0)] public fixed byte F128Bytes [16];
        [FieldOffset(0)] public fixed byte VI8 [16];
        [FieldOffset(0)] public fixed ushort VI16 [8];
        [FieldOffset(0)] public fixed uint VI32 [4];
        [FieldOffset(0)] public fixed ulong VI64 [2];
        [FieldOffset(0)] public fixed float VF32 [4];
        [FieldOffset(0)] public fixed double VF64 [2];
        [FieldOffset(0)] public I64PARTS32 I64Parts32;
        [FieldOffset(0)] public F128PARTS64 F128Parts64;
        [FieldOffset(0)] public fixed byte RawBytes [24];
        [FieldOffset(24)] public UInt32 TailOfRawBytes;
        [FieldOffset(28)] public DEBUG_VALUE_TYPE Type;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct DEBUG_MODULE_PARAMETERS
    {
        public UInt64 Base;
        public UInt32 Size;
        public UInt32 TimeDateStamp;
        public UInt32 Checksum;
        public DEBUG_MODULE Flags;
        public DEBUG_SYMTYPE SymbolType;
        public UInt32 ImageNameSize;
        public UInt32 ModuleNameSize;
        public UInt32 LoadedImageNameSize;
        public UInt32 SymbolFileNameSize;
        public UInt32 MappedImageNameSize;
        public fixed UInt64 Reserved [2];
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct DEBUG_STACK_FRAME
    {
        public UInt64 InstructionOffset;
        public UInt64 ReturnOffset;
        public UInt64 FrameOffset;
        public UInt64 StackOffset;
        public UInt64 FuncTableEntry;
        public fixed UInt64 Params [4];
        public fixed UInt64 Reserved [6];
        public UInt32 Virtual;
        public UInt32 FrameNumber;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct DEBUG_STACK_FRAME_EX
    {
        /* DEBUG_STACK_FRAME */
        public UInt64 InstructionOffset;
        public UInt64 ReturnOffset;
        public UInt64 FrameOffset;
        public UInt64 StackOffset;
        public UInt64 FuncTableEntry;
        public fixed UInt64 Params [4];
        public fixed UInt64 Reserved [6];
        public UInt32 Virtual;
        public UInt32 FrameNumber;

        /* DEBUG_STACK_FRAME_EX */
        public UInt32 InlineFrameContext;
        public UInt32 Reserved1;

        public DEBUG_STACK_FRAME_EX(DEBUG_STACK_FRAME dsf)
        {
            InstructionOffset = dsf.InstructionOffset;
            ReturnOffset = dsf.ReturnOffset;
            FrameOffset = dsf.FrameOffset;
            StackOffset = dsf.StackOffset;
            FuncTableEntry = dsf.FuncTableEntry;
            fixed (UInt64* pParams = Params)
            {
                for (int i = 0; i < 4; ++i)
                    pParams[i] = dsf.Params[i];
            }
            fixed (UInt64* pReserved = Reserved)
            {
                for (int i = 0; i < 6; ++i)
                    pReserved[i] = dsf.Reserved[i];
            }
            Virtual = dsf.Virtual;
            FrameNumber = dsf.FrameNumber;
            InlineFrameContext = 0xFFFFFFFF;
            Reserved1 = 0;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_SYMBOL_PARAMETERS
    {
        public UInt64 Module;
        public UInt32 TypeId;
        public UInt32 ParentSymbol;
        public UInt32 SubElements;
        public DEBUG_SYMBOL Flags;
        public UInt64 Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDBG_EXTENSION_APIS32
    {
        public UInt32 nSize;
        public IntPtr lpOutputRoutine;
        public IntPtr lpGetExpressionRoutine;
        public IntPtr lpGetSymbolRoutine;
        public IntPtr lpDisasmRoutine;
        public IntPtr lpCheckControlCRoutine;
        public IntPtr lpReadProcessMemoryRoutine;
        public IntPtr lpWriteProcessMemoryRoutine;
        public IntPtr lpGetThreadContextRoutine;
        public IntPtr lpSetThreadContextRoutine;
        public IntPtr lpIoctlRoutine;
        public IntPtr lpStackTraceRoutine;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDBG_EXTENSION_APIS64
    {
        public UInt32 nSize;
        public IntPtr lpOutputRoutine;
        public IntPtr lpGetExpressionRoutine;
        public IntPtr lpGetSymbolRoutine;
        public IntPtr lpDisasmRoutine;
        public IntPtr lpCheckControlCRoutine;
        public IntPtr lpReadProcessMemoryRoutine;
        public IntPtr lpWriteProcessMemoryRoutine;
        public IntPtr lpGetThreadContextRoutine;
        public IntPtr lpSetThreadContextRoutine;
        public IntPtr lpIoctlRoutine;
        public IntPtr lpStackTraceRoutine;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_SPECIFIC_FILTER_PARAMETERS
    {
        public DEBUG_FILTER_EXEC_OPTION ExecutionOption;
        public DEBUG_FILTER_CONTINUE_OPTION ContinueOption;
        public UInt32 TextSize;
        public UInt32 CommandSize;
        public UInt32 ArgumentSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_EXCEPTION_FILTER_PARAMETERS
    {
        public DEBUG_FILTER_EXEC_OPTION ExecutionOption;
        public DEBUG_FILTER_CONTINUE_OPTION ContinueOption;
        public UInt32 TextSize;
        public UInt32 CommandSize;
        public UInt32 SecondCommandSize;
        public UInt32 ExceptionCode;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_HANDLE_DATA_BASIC
    {
        public UInt32 TypeNameSize;
        public UInt32 ObjectNameSize;
        public UInt32 Attributes;
        public UInt32 GrantedAccess;
        public UInt32 HandleCount;
        public UInt32 PointerCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORY_BASIC_INFORMATION64
    {
        public UInt64 BaseAddress;
        public UInt64 AllocationBase;
        public PAGE AllocationProtect;
        public UInt32 __alignment1;
        public UInt64 RegionSize;
        public MEM State;
        public PAGE Protect;
        public MEM Type;
        public UInt32 __alignment2;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct IMAGE_NT_HEADERS32
    {
        [FieldOffset(0)] public uint Signature;
        [FieldOffset(4)] public IMAGE_FILE_HEADER FileHeader;
        [FieldOffset(24)] public IMAGE_OPTIONAL_HEADER32 OptionalHeader;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct IMAGE_NT_HEADERS64
    {
        [FieldOffset(0)] public uint Signature;
        [FieldOffset(4)] public IMAGE_FILE_HEADER FileHeader;
        [FieldOffset(24)] public IMAGE_OPTIONAL_HEADER64 OptionalHeader;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct IMAGE_FILE_HEADER
    {
        [FieldOffset(0)] public UInt16 Machine;
        [FieldOffset(2)] public UInt16 NumberOfSections;
        [FieldOffset(4)] public UInt32 TimeDateStamp;
        [FieldOffset(8)] public UInt32 PointerToSymbolTable;
        [FieldOffset(12)] public UInt32 NumberOfSymbols;
        [FieldOffset(16)] public UInt16 SizeOfOptionalHeader;
        [FieldOffset(18)] public UInt16 Characteristics;
    }

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct IMAGE_DOS_HEADER
    {
        [FieldOffset(0)] public UInt16 e_magic; // Magic number
        [FieldOffset(2)] public UInt16 e_cblp; // Bytes on last page of file
        [FieldOffset(4)] public UInt16 e_cp; // Pages in file
        [FieldOffset(6)] public UInt16 e_crlc; // Relocations
        [FieldOffset(8)] public UInt16 e_cparhdr; // Size of header in paragraphs
        [FieldOffset(10)] public UInt16 e_minalloc; // Minimum extra paragraphs needed
        [FieldOffset(12)] public UInt16 e_maxalloc; // Maximum extra paragraphs needed
        [FieldOffset(14)] public UInt16 e_ss; // Initial (relative) SS value
        [FieldOffset(16)] public UInt16 e_sp; // Initial SP value
        [FieldOffset(18)] public UInt16 e_csum; // Checksum
        [FieldOffset(20)] public UInt16 e_ip; // Initial IP value
        [FieldOffset(22)] public UInt16 e_cs; // Initial (relative) CS value
        [FieldOffset(24)] public UInt16 e_lfarlc; // File address of relocation table
        [FieldOffset(26)] public UInt16 e_ovno; // Overlay number
        [FieldOffset(28)] public fixed UInt16 e_res [4]; // Reserved words
        [FieldOffset(36)] public UInt16 e_oemid; // OEM identifier (for e_oeminfo)
        [FieldOffset(38)] public UInt16 e_oeminfo; // OEM information; e_oemid specific
        [FieldOffset(40)] public fixed UInt16 e_res2 [10]; // Reserved words
        [FieldOffset(60)] public UInt32 e_lfanew; // File address of new exe header
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct IMAGE_OPTIONAL_HEADER32
    {
        [FieldOffset(0)] public ushort Magic;
        [FieldOffset(2)] public byte MajorLinkerVersion;
        [FieldOffset(3)] public byte MinorLinkerVersion;
        [FieldOffset(4)] public UInt32 SizeOfCode;
        [FieldOffset(8)] public UInt32 SizeOfInitializedData;
        [FieldOffset(12)] public UInt32 SizeOfUninitializedData;
        [FieldOffset(16)] public UInt32 AddressOfEntryPoint;
        [FieldOffset(20)] public UInt32 BaseOfCode;
        [FieldOffset(24)] public UInt32 BaseOfData;
        [FieldOffset(28)] public UInt32 ImageBase;
        [FieldOffset(32)] public UInt32 SectionAlignment;
        [FieldOffset(36)] public UInt32 FileAlignment;
        [FieldOffset(40)] public ushort MajorOperatingSystemVersion;
        [FieldOffset(42)] public ushort MinorOperatingSystemVersion;
        [FieldOffset(44)] public ushort MajorImageVersion;
        [FieldOffset(46)] public ushort MinorImageVersion;
        [FieldOffset(48)] public ushort MajorSubsystemVersion;
        [FieldOffset(50)] public ushort MinorSubsystemVersion;
        [FieldOffset(52)] public UInt32 Win32VersionValue;
        [FieldOffset(56)] public UInt32 SizeOfImage;
        [FieldOffset(60)] public UInt32 SizeOfHeaders;
        [FieldOffset(64)] public UInt32 CheckSum;
        [FieldOffset(68)] public ushort Subsystem;
        [FieldOffset(70)] public ushort DllCharacteristics;
        [FieldOffset(72)] public UInt32 SizeOfStackReserve;
        [FieldOffset(76)] public UInt32 SizeOfStackCommit;
        [FieldOffset(80)] public UInt32 SizeOfHeapReserve;
        [FieldOffset(84)] public UInt32 SizeOfHeapCommit;
        [FieldOffset(88)] public UInt32 LoaderFlags;
        [FieldOffset(92)] public UInt32 NumberOfRvaAndSizes;
        [FieldOffset(96)] public IMAGE_DATA_DIRECTORY DataDirectory0;
        [FieldOffset(104)] public IMAGE_DATA_DIRECTORY DataDirectory1;
        [FieldOffset(112)] public IMAGE_DATA_DIRECTORY DataDirectory2;
        [FieldOffset(120)] public IMAGE_DATA_DIRECTORY DataDirectory3;
        [FieldOffset(128)] public IMAGE_DATA_DIRECTORY DataDirectory4;
        [FieldOffset(136)] public IMAGE_DATA_DIRECTORY DataDirectory5;
        [FieldOffset(144)] public IMAGE_DATA_DIRECTORY DataDirectory6;
        [FieldOffset(152)] public IMAGE_DATA_DIRECTORY DataDirectory7;
        [FieldOffset(160)] public IMAGE_DATA_DIRECTORY DataDirectory8;
        [FieldOffset(168)] public IMAGE_DATA_DIRECTORY DataDirectory9;
        [FieldOffset(176)] public IMAGE_DATA_DIRECTORY DataDirectory10;
        [FieldOffset(284)] public IMAGE_DATA_DIRECTORY DataDirectory11;
        [FieldOffset(292)] public IMAGE_DATA_DIRECTORY DataDirectory12;
        [FieldOffset(300)] public IMAGE_DATA_DIRECTORY DataDirectory13;
        [FieldOffset(308)] public IMAGE_DATA_DIRECTORY DataDirectory14;
        [FieldOffset(316)] public IMAGE_DATA_DIRECTORY DataDirectory15;

        public static unsafe IMAGE_DATA_DIRECTORY* GetDataDirectory(IMAGE_OPTIONAL_HEADER32* header, int zeroBasedIndex)
        {
            return (&header->DataDirectory0) + zeroBasedIndex;
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct IMAGE_OPTIONAL_HEADER64
    {
        [FieldOffset(0)] public ushort Magic;
        [FieldOffset(2)] public byte MajorLinkerVersion;
        [FieldOffset(3)] public byte MinorLinkerVersion;
        [FieldOffset(4)] public UInt32 SizeOfCode;
        [FieldOffset(8)] public UInt32 SizeOfInitializedData;
        [FieldOffset(12)] public UInt32 SizeOfUninitializedData;
        [FieldOffset(16)] public UInt32 AddressOfEntryPoint;
        [FieldOffset(20)] public UInt32 BaseOfCode;
        [FieldOffset(24)] public UInt64 ImageBase;
        [FieldOffset(32)] public UInt32 SectionAlignment;
        [FieldOffset(36)] public UInt32 FileAlignment;
        [FieldOffset(40)] public ushort MajorOperatingSystemVersion;
        [FieldOffset(42)] public ushort MinorOperatingSystemVersion;
        [FieldOffset(44)] public ushort MajorImageVersion;
        [FieldOffset(46)] public ushort MinorImageVersion;
        [FieldOffset(48)] public ushort MajorSubsystemVersion;
        [FieldOffset(50)] public ushort MinorSubsystemVersion;
        [FieldOffset(52)] public UInt32 Win32VersionValue;
        [FieldOffset(56)] public UInt32 SizeOfImage;
        [FieldOffset(60)] public UInt32 SizeOfHeaders;
        [FieldOffset(64)] public UInt32 CheckSum;
        [FieldOffset(68)] public ushort Subsystem;
        [FieldOffset(70)] public ushort DllCharacteristics;
        [FieldOffset(72)] public UInt64 SizeOfStackReserve;
        [FieldOffset(80)] public UInt64 SizeOfStackCommit;
        [FieldOffset(88)] public UInt64 SizeOfHeapReserve;
        [FieldOffset(96)] public UInt64 SizeOfHeapCommit;
        [FieldOffset(104)] public UInt32 LoaderFlags;
        [FieldOffset(108)] public UInt32 NumberOfRvaAndSizes;
        [FieldOffset(112)] public IMAGE_DATA_DIRECTORY DataDirectory0;
        [FieldOffset(120)] public IMAGE_DATA_DIRECTORY DataDirectory1;
        [FieldOffset(128)] public IMAGE_DATA_DIRECTORY DataDirectory2;
        [FieldOffset(136)] public IMAGE_DATA_DIRECTORY DataDirectory3;
        [FieldOffset(144)] public IMAGE_DATA_DIRECTORY DataDirectory4;
        [FieldOffset(152)] public IMAGE_DATA_DIRECTORY DataDirectory5;
        [FieldOffset(160)] public IMAGE_DATA_DIRECTORY DataDirectory6;
        [FieldOffset(168)] public IMAGE_DATA_DIRECTORY DataDirectory7;
        [FieldOffset(176)] public IMAGE_DATA_DIRECTORY DataDirectory8;
        [FieldOffset(184)] public IMAGE_DATA_DIRECTORY DataDirectory9;
        [FieldOffset(192)] public IMAGE_DATA_DIRECTORY DataDirectory10;
        [FieldOffset(200)] public IMAGE_DATA_DIRECTORY DataDirectory11;
        [FieldOffset(208)] public IMAGE_DATA_DIRECTORY DataDirectory12;
        [FieldOffset(216)] public IMAGE_DATA_DIRECTORY DataDirectory13;
        [FieldOffset(224)] public IMAGE_DATA_DIRECTORY DataDirectory14;
        [FieldOffset(232)] public IMAGE_DATA_DIRECTORY DataDirectory15;

        public static unsafe IMAGE_DATA_DIRECTORY* GetDataDirectory(IMAGE_OPTIONAL_HEADER64* header, int zeroBasedIndex)
        {
            return (&header->DataDirectory0) + zeroBasedIndex;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IMAGE_DATA_DIRECTORY
    {
        public UInt32 VirtualAddress;
        public UInt32 Size;
    }

    /// <summary>
    ///    Describes a symbol within a module.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_MODULE_AND_ID
    {
        /// <summary>
        ///    The location in the target's virtual address space of the module's base address.
        /// </summary>
        public UInt64 ModuleBase;

        /// <summary>
        ///    The symbol ID of the symbol within the module.
        /// </summary>
        public UInt64 Id;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_SYMBOL_ENTRY
    {
        public UInt64 ModuleBase;
        public UInt64 Offset;
        public UInt64 Id;
        public UInt64 Arg64;
        public UInt32 Size;
        public UInt32 Flags;
        public UInt32 TypeId;
        public UInt32 NameSize;
        public UInt32 Token;
        public SymTag Tag;
        public UInt32 Arg32;
        public UInt32 Reserved;
    }


    [StructLayout(LayoutKind.Explicit)]
    public struct IMAGE_IMPORT_DESCRIPTOR
    {
        [FieldOffset(0)] public UInt32 Characteristics; // 0 for terminating null import descriptor
        [FieldOffset(0)] public UInt32 OriginalFirstThunk; // RVA to original unbound IAT (PIMAGE_THUNK_DATA)
        [FieldOffset(4)] public UInt32 TimeDateStamp; // 0 if not bound,
        // -1 if bound, and real date\time stamp
        //     in IMAGE_DIRECTORY_ENTRY_BOUND_IMPORT (new BIND)
        // O.W. date/time stamp of DLL bound to (Old BIND)

        [FieldOffset(8)] public UInt32 ForwarderChain; // -1 if no forwarders
        [FieldOffset(12)] public UInt32 Name;
        [FieldOffset(16)] public UInt32 FirstThunk; // RVA to IAT (if bound this IAT has actual addresses)
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct IMAGE_THUNK_DATA32
    {
        [FieldOffset(0)] public UInt32 ForwarderString; // PBYTE
        [FieldOffset(0)] public UInt32 Function; // PDWORD
        [FieldOffset(0)] public UInt32 Ordinal;
        [FieldOffset(0)] public UInt32 AddressOfData; // PIMAGE_IMPORT_BY_NAME
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct IMAGE_THUNK_DATA64
    {
        [FieldOffset(0)] public UInt64 ForwarderString; // PBYTE
        [FieldOffset(0)] public UInt64 Function; // PDWORD
        [FieldOffset(0)] public UInt64 Ordinal;
        [FieldOffset(0)] public UInt64 AddressOfData; // PIMAGE_IMPORT_BY_NAME
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct LANGANDCODEPAGE
    {
        [FieldOffset(0)] public UInt16 wLanguage;
        [FieldOffset(2)] public UInt16 wCodePage;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct VS_FIXEDFILEINFO
    {
        public UInt32 dwSignature;
        public UInt32 dwStrucVersion;
        public UInt32 dwFileVersionMS;
        public UInt32 dwFileVersionLS;
        public UInt32 dwProductVersionMS;
        public UInt32 dwProductVersionLS;
        public UInt32 dwFileFlagsMask;
        public VS_FF dwFileFlags;
        public UInt32 dwFileOS;
        public UInt32 dwFileType;
        public UInt32 dwFileSubtype;
        public UInt32 dwFileDateMS;
        public UInt32 dwFileDateLS;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct IMAGE_COR20_HEADER_ENTRYPOINT
    {
        [FieldOffset(0)] private UInt32 Token;
        [FieldOffset(0)] private UInt32 RVA;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct IMAGE_COR20_HEADER
    {
        // Header versioning
        public UInt32 cb;
        public UInt16 MajorRuntimeVersion;
        public UInt16 MinorRuntimeVersion;

        // Symbol table and startup information
        public IMAGE_DATA_DIRECTORY MetaData;
        public UInt32 Flags;

        // The main program if it is an EXE (not used if a DLL?)
        // If COMIMAGE_FLAGS_NATIVE_ENTRYPOINT is not set, EntryPointToken represents a managed entrypoint.
        // If COMIMAGE_FLAGS_NATIVE_ENTRYPOINT is set, EntryPointRVA represents an RVA to a native entrypoint
        // (depricated for DLLs, use modules constructors intead).
        public IMAGE_COR20_HEADER_ENTRYPOINT EntryPoint;

        // This is the blob of managed resources. Fetched using code:AssemblyNative.GetResource and
        // code:PEFile.GetResource and accessible from managed code from
        // System.Assembly.GetManifestResourceStream.  The meta data has a table that maps names to offsets into
        // this blob, so logically the blob is a set of resources.
        public IMAGE_DATA_DIRECTORY Resources;
        // IL assemblies can be signed with a public-private key to validate who created it.  The signature goes
        // here if this feature is used.
        public IMAGE_DATA_DIRECTORY StrongNameSignature;

        public IMAGE_DATA_DIRECTORY CodeManagerTable; // Depricated, not used
        // Used for manged codee that has unmaanaged code inside it (or exports methods as unmanaged entry points)
        public IMAGE_DATA_DIRECTORY VTableFixups;
        public IMAGE_DATA_DIRECTORY ExportAddressTableJumps;

        // null for ordinary IL images.  NGEN images it points at a code:CORCOMPILE_HEADER structure
        public IMAGE_DATA_DIRECTORY ManagedNativeHeader;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WDBGEXTS_THREAD_OS_INFO
    {
        public UInt32 ThreadId;
        public UInt32 ExitStatus;
        public UInt32 PriorityClass;
        public UInt32 Priority;
        public UInt64 CreateTime;
        public UInt64 ExitTime;
        public UInt64 KernelTime;
        public UInt64 UserTime;
        public UInt64 StartOffset;
        public UInt64 Affinity;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct WDBGEXTS_CLR_DATA_INTERFACE
    {
        public Guid* Iid;
        private void* Iface;

        public WDBGEXTS_CLR_DATA_INTERFACE(Guid* iid)
        {
            Iid = iid;
            Iface = null;
        }

        public object Interface
        {
            get { return (Iface != null) ? Marshal.GetObjectForIUnknown((IntPtr)Iface) : null; }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct DEBUG_SYMBOL_SOURCE_ENTRY
    {
        private UInt64 ModuleBase;
        private UInt64 Offset;
        private UInt64 FileNameId;
        private UInt64 EngineInternal;
        private UInt32 Size;
        private UInt32 Flags;
        private UInt32 FileNameSize;
        // Line numbers are one-based.
        // May be DEBUG_ANY_ID if unknown.
        private UInt32 StartLine;
        private UInt32 EndLine;
        // Column numbers are one-based byte indices.
        // May be DEBUG_ANY_ID if unknown.
        private UInt32 StartColumn;
        private UInt32 EndColumn;
        private UInt32 Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct DEBUG_OFFSET_REGION
    {
        private UInt64 Base;
        private UInt64 Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct _DEBUG_TYPED_DATA
    {
        public UInt64 ModBase;
        public UInt64 Offset;
        public UInt64 EngineHandle;
        public UInt64 Data;
        public UInt32 Size;
        public UInt32 Flags;
        public UInt32 TypeId;
        public UInt32 BaseTypeId;
        public UInt32 Tag;
        public UInt32 Register;
        public fixed UInt64 Internal [9];
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct _EXT_TYPED_DATA
    {
        public _EXT_TDOP Operation;
        public UInt32 Flags;
        public _DEBUG_TYPED_DATA InData;
        public _DEBUG_TYPED_DATA OutData;
        public UInt32 InStrIndex;
        public UInt32 In32;
        public UInt32 Out32;
        public UInt64 In64;
        public UInt64 Out64;
        public UInt32 StrBufferIndex;
        public UInt32 StrBufferChars;
        public UInt32 StrCharsNeeded;
        public UInt32 DataBufferIndex;
        public UInt32 DataBufferBytes;
        public UInt32 DataBytesNeeded;
        public UInt32 Status;
        public fixed UInt64 Reserved [8];
    }


    [StructLayout(LayoutKind.Sequential)]
    public class EXT_TYPED_DATA
    {
        public _EXT_TDOP Operation;
        public UInt32 Flags;
        public _DEBUG_TYPED_DATA InData;
        public _DEBUG_TYPED_DATA OutData;
        public UInt32 InStrIndex;
        public UInt32 In32;
        public UInt32 Out32;
        public UInt64 In64;
        public UInt64 Out64;
        public UInt32 StrBufferIndex;
        public UInt32 StrBufferChars;
        public UInt32 StrCharsNeeded;
        public UInt32 DataBufferIndex;
        public UInt32 DataBufferBytes;
        public UInt32 DataBytesNeeded;
        public UInt32 Status;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public Int32 left;
        public Int32 top;
        public Int32 right;
        public Int32 bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_LAST_EVENT_INFO_BREAKPOINT
    {
        public uint Id;
    }


    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_LAST_EVENT_INFO_EXCEPTION
    {
        public EXCEPTION_RECORD64 ExceptionRecord;
        public uint FirstChance;
    }
    
    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_LAST_EVENT_INFO_EXIT_THREAD
    {
        public uint ExitCode;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_LAST_EVENT_INFO_LOAD_MODULE
    {
        public ulong Base;
    }


    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_LAST_EVENT_INFO_UNLOAD_MODULE
    {
        public ulong Base;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DEBUG_LAST_EVENT_INFO_SYSTEM_ERROR
    {
        public uint Error;
        public uint Level;
    }
}
