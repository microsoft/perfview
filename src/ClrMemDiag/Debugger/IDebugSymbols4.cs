using System;
using System.Text;
using System.Runtime.InteropServices;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("e391bbd8-9d8c-4418-840b-c006592a1752")]
    public interface IDebugSymbols4 : IDebugSymbols3
    {
        /* IDebugSymbols */

        [PreserveSig]
        new int GetSymbolOptions(
            [Out] out SYMOPT Options);

        [PreserveSig]
        new int AddSymbolOptions(
            [In] SYMOPT Options);

        [PreserveSig]
        new int RemoveSymbolOptions(
            [In] SYMOPT Options);

        [PreserveSig]
        new int SetSymbolOptions(
            [In] SYMOPT Options);

        [PreserveSig]
        new int GetNameByOffset(
            [In] UInt64 Offset,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder NameBuffer,
            [In] Int32 NameBufferSize,
            [Out] out UInt32 NameSize,
            [Out] out UInt64 Displacement);

        [PreserveSig]
        new int GetOffsetByName(
            [In, MarshalAs(UnmanagedType.LPStr)] string Symbol,
            [Out] out UInt64 Offset);

        [PreserveSig]
        new int GetNearNameByOffset(
            [In] UInt64 Offset,
            [In] int Delta,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder NameBuffer,
            [In] Int32 NameBufferSize,
            [Out] out UInt32 NameSize,
            [Out] out UInt64 Displacement);

        [PreserveSig]
        new int GetLineByOffset(
            [In] UInt64 Offset,
            [Out] out UInt32 Line,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder FileBuffer,
            [In] Int32 FileBufferSize,
            [Out] out UInt32 FileSize,
            [Out] out UInt64 Displacement);

        [PreserveSig]
        new int GetOffsetByLine(
            [In] UInt32 Line,
            [In, MarshalAs(UnmanagedType.LPStr)] string File,
            [Out] out UInt64 Offset);

        [PreserveSig]
        new int GetNumberModules(
            [Out] out UInt32 Loaded,
            [Out] out UInt32 Unloaded);

        [PreserveSig]
        new int GetModuleByIndex(
            [In] UInt32 Index,
            [Out] out UInt64 Base);

        [PreserveSig]
        new int GetModuleByModuleName(
            [In, MarshalAs(UnmanagedType.LPStr)] string Name,
            [In] UInt32 StartIndex,
            [Out] out UInt32 Index,
            [Out] out UInt64 Base);

        [PreserveSig]
        new int GetModuleByOffset(
            [In] UInt64 Offset,
            [In] UInt32 StartIndex,
            [Out] out UInt32 Index,
            [Out] out UInt64 Base);

        [PreserveSig]
        new int GetModuleNames(
            [In] UInt32 Index,
            [In] UInt64 Base,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder ImageNameBuffer,
            [In] Int32 ImageNameBufferSize,
            [Out] out UInt32 ImageNameSize,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder ModuleNameBuffer,
            [In] Int32 ModuleNameBufferSize,
            [Out] out UInt32 ModuleNameSize,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder LoadedImageNameBuffer,
            [In] Int32 LoadedImageNameBufferSize,
            [Out] out UInt32 LoadedImageNameSize);

        [PreserveSig]
        new int GetModuleParameters(
            [In] UInt32 Count,
            [In, MarshalAs(UnmanagedType.LPArray)] UInt64[] Bases,
            [In] UInt32 Start,
            [Out, MarshalAs(UnmanagedType.LPArray)] DEBUG_MODULE_PARAMETERS[] Params);

        [PreserveSig]
        new int GetSymbolModule(
            [In, MarshalAs(UnmanagedType.LPStr)] string Symbol,
            [Out] out UInt64 Base);

        [PreserveSig]
        new int GetTypeName(
            [In] UInt64 Module,
            [In] UInt32 TypeId,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder NameBuffer,
            [In] Int32 NameBufferSize,
            [Out] out UInt32 NameSize);

        [PreserveSig]
        new int GetTypeId(
            [In] UInt64 Module,
            [In, MarshalAs(UnmanagedType.LPStr)] string Name,
            [Out] out UInt32 TypeId);

        [PreserveSig]
        new int GetTypeSize(
            [In] UInt64 Module,
            [In] UInt32 TypeId,
            [Out] out UInt32 Size);

        [PreserveSig]
        new int GetFieldOffset(
            [In] UInt64 Module,
            [In] UInt32 TypeId,
            [In, MarshalAs(UnmanagedType.LPStr)] string Field,
            [Out] out UInt32 Offset);

        [PreserveSig]
        new int GetSymbolTypeId(
            [In, MarshalAs(UnmanagedType.LPStr)] string Symbol,
            [Out] out UInt32 TypeId,
            [Out] out UInt64 Module);

        [PreserveSig]
        new int GetOffsetTypeId(
            [In] UInt64 Offset,
            [Out] out UInt32 TypeId,
            [Out] out UInt64 Module);

        [PreserveSig]
        new int ReadTypedDataVirtual(
            [In] UInt64 Offset,
            [In] UInt64 Module,
            [In] UInt32 TypeId,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] byte[] Buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 BytesRead);

        [PreserveSig]
        new int WriteTypedDataVirtual(
            [In] UInt64 Offset,
            [In] UInt64 Module,
            [In] UInt32 TypeId,
            [In] IntPtr Buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 BytesWritten);

        [PreserveSig]
        new int OutputTypedDataVirtual(
            [In] DEBUG_OUTCTL OutputControl,
            [In] UInt64 Offset,
            [In] UInt64 Module,
            [In] UInt32 TypeId,
            [In] DEBUG_TYPEOPTS Flags);

        [PreserveSig]
        new int ReadTypedDataPhysical(
            [In] UInt64 Offset,
            [In] UInt64 Module,
            [In] UInt32 TypeId,
            [In] IntPtr Buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 BytesRead);

        [PreserveSig]
        new int WriteTypedDataPhysical(
            [In] UInt64 Offset,
            [In] UInt64 Module,
            [In] UInt32 TypeId,
            [In] IntPtr Buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 BytesWritten);

        [PreserveSig]
        new int OutputTypedDataPhysical(
            [In] DEBUG_OUTCTL OutputControl,
            [In] UInt64 Offset,
            [In] UInt64 Module,
            [In] UInt32 TypeId,
            [In] DEBUG_TYPEOPTS Flags);

        [PreserveSig]
        new int GetScope(
            [Out] out UInt64 InstructionOffset,
            [Out] out DEBUG_STACK_FRAME ScopeFrame,
            [In] IntPtr ScopeContext,
            [In] UInt32 ScopeContextSize);

        [PreserveSig]
        new int SetScope(
            [In] UInt64 InstructionOffset,
            [In] DEBUG_STACK_FRAME ScopeFrame,
            [In] IntPtr ScopeContext,
            [In] UInt32 ScopeContextSize);

        [PreserveSig]
        new int ResetScope();

        [PreserveSig]
        new int GetScopeSymbolGroup(
            [In] DEBUG_SCOPE_GROUP Flags,
            [In, MarshalAs(UnmanagedType.Interface)] IDebugSymbolGroup Update,
            [Out, MarshalAs(UnmanagedType.Interface)] out IDebugSymbolGroup Symbols);

        [PreserveSig]
        new int CreateSymbolGroup(
            [Out, MarshalAs(UnmanagedType.Interface)] out IDebugSymbolGroup Group);

        [PreserveSig]
        new int StartSymbolMatch(
            [In, MarshalAs(UnmanagedType.LPStr)] string Pattern,
            [Out] out UInt64 Handle);

        [PreserveSig]
        new int GetNextSymbolMatch(
            [In] UInt64 Handle,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 MatchSize,
            [Out] out UInt64 Offset);

        [PreserveSig]
        new int EndSymbolMatch(
            [In] UInt64 Handle);

        [PreserveSig]
        new int Reload(
            [In, MarshalAs(UnmanagedType.LPStr)] string Module);

        [PreserveSig]
        new int GetSymbolPath(
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 PathSize);

        [PreserveSig]
        new int SetSymbolPath(
            [In, MarshalAs(UnmanagedType.LPStr)] string Path);

        [PreserveSig]
        new int AppendSymbolPath(
            [In, MarshalAs(UnmanagedType.LPStr)] string Addition);

        [PreserveSig]
        new int GetImagePath(
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 PathSize);

        [PreserveSig]
        new int SetImagePath(
            [In, MarshalAs(UnmanagedType.LPStr)] string Path);

        [PreserveSig]
        new int AppendImagePath(
            [In, MarshalAs(UnmanagedType.LPStr)] string Addition);

        [PreserveSig]
        new int GetSourcePath(
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 PathSize);

        [PreserveSig]
        new int GetSourcePathElement(
            [In] UInt32 Index,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 ElementSize);

        [PreserveSig]
        new int SetSourcePath(
            [In, MarshalAs(UnmanagedType.LPStr)] string Path);

        [PreserveSig]
        new int AppendSourcePath(
            [In, MarshalAs(UnmanagedType.LPStr)] string Addition);

        [PreserveSig]
        new int FindSourceFile(
            [In] UInt32 StartElement,
            [In, MarshalAs(UnmanagedType.LPStr)] string File,
            [In] DEBUG_FIND_SOURCE Flags,
            [Out] out UInt32 FoundElement,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 FoundSize);

        [PreserveSig]
        new int GetSourceFileLineOffsets(
            [In, MarshalAs(UnmanagedType.LPStr)] string File,
            [Out, MarshalAs(UnmanagedType.LPArray)] UInt64[] Buffer,
            [In] Int32 BufferLines,
            [Out] out UInt32 FileLines);

        /* IDebugSymbols2 */

        [PreserveSig]
        new int GetModuleVersionInformation(
            [In] UInt32 Index,
            [In] UInt64 Base,
            [In, MarshalAs(UnmanagedType.LPStr)] string Item,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] byte[] buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 VerInfoSize);

        [PreserveSig]
        new int GetModuleNameString(
            [In] DEBUG_MODNAME Which,
            [In] UInt32 Index,
            [In] UInt64 Base,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 NameSize);

        [PreserveSig]
        new int GetConstantName(
            [In] UInt64 Module,
            [In] UInt32 TypeId,
            [In] UInt64 Value,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 NameSize);

        [PreserveSig]
        new int GetFieldName(
            [In] UInt64 Module,
            [In] UInt32 TypeId,
            [In] UInt32 FieldIndex,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 NameSize);

        [PreserveSig]
        new int GetTypeOptions(
            [Out] out DEBUG_TYPEOPTS Options);

        [PreserveSig]
        new int AddTypeOptions(
            [In] DEBUG_TYPEOPTS Options);

        [PreserveSig]
        new int RemoveTypeOptions(
            [In] DEBUG_TYPEOPTS Options);

        [PreserveSig]
        new int SetTypeOptions(
            [In] DEBUG_TYPEOPTS Options);

        /* IDebugSymbols3 */

        [PreserveSig]
        new int GetNameByOffsetWide(
            [In] UInt64 Offset,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder NameBuffer,
            [In] Int32 NameBufferSize,
            [Out] out UInt32 NameSize,
            [Out] out UInt64 Displacement);

        [PreserveSig]
        new int GetOffsetByNameWide(
            [In, MarshalAs(UnmanagedType.LPWStr)] string Symbol,
            [Out] out UInt64 Offset);

        [PreserveSig]
        new int GetNearNameByOffsetWide(
            [In] UInt64 Offset,
            [In] int Delta,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder NameBuffer,
            [In] Int32 NameBufferSize,
            [Out] out UInt32 NameSize,
            [Out] out UInt64 Displacement);

        [PreserveSig]
        new int GetLineByOffsetWide(
            [In] UInt64 Offset,
            [Out] out UInt32 Line,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder FileBuffer,
            [In] Int32 FileBufferSize,
            [Out] out UInt32 FileSize,
            [Out] out UInt64 Displacement);

        [PreserveSig]
        new int GetOffsetByLineWide(
            [In] UInt32 Line,
            [In, MarshalAs(UnmanagedType.LPWStr)] string File,
            [Out] out UInt64 Offset);

        [PreserveSig]
        new int GetModuleByModuleNameWide(
            [In, MarshalAs(UnmanagedType.LPWStr)] string Name,
            [In] UInt32 StartIndex,
            [Out] out UInt32 Index,
            [Out] out UInt64 Base);

        [PreserveSig]
        new int GetSymbolModuleWide(
            [In, MarshalAs(UnmanagedType.LPWStr)] string Symbol,
            [Out] out UInt64 Base);

        [PreserveSig]
        new int GetTypeNameWide(
            [In] UInt64 Module,
            [In] UInt32 TypeId,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder NameBuffer,
            [In] Int32 NameBufferSize,
            [Out] out UInt32 NameSize);

        [PreserveSig]
        new int GetTypeIdWide(
            [In] UInt64 Module,
            [In, MarshalAs(UnmanagedType.LPWStr)] string Name,
            [Out] out UInt32 TypeId);

        [PreserveSig]
        new int GetFieldOffsetWide(
            [In] UInt64 Module,
            [In] UInt32 TypeId,
            [In, MarshalAs(UnmanagedType.LPWStr)] string Field,
            [Out] out UInt32 Offset);

        [PreserveSig]
        new int GetSymbolTypeIdWide(
            [In, MarshalAs(UnmanagedType.LPWStr)] string Symbol,
            [Out] out UInt32 TypeId,
            [Out] out UInt64 Module);

        [PreserveSig]
        new int GetScopeSymbolGroup2(
            [In] DEBUG_SCOPE_GROUP Flags,
            [In, MarshalAs(UnmanagedType.Interface)] IDebugSymbolGroup2 Update,
            [Out, MarshalAs(UnmanagedType.Interface)] out IDebugSymbolGroup2 Symbols);

        [PreserveSig]
        new int CreateSymbolGroup2(
            [Out, MarshalAs(UnmanagedType.Interface)] out IDebugSymbolGroup2 Group);

        [PreserveSig]
        new int StartSymbolMatchWide(
            [In, MarshalAs(UnmanagedType.LPWStr)] string Pattern,
            [Out] out UInt64 Handle);

        [PreserveSig]
        new int GetNextSymbolMatchWide(
            [In] UInt64 Handle,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 MatchSize,
            [Out] out UInt64 Offset);

        [PreserveSig]
        new int ReloadWide(
            [In, MarshalAs(UnmanagedType.LPWStr)] string Module);

        [PreserveSig]
        new int GetSymbolPathWide(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 PathSize);

        [PreserveSig]
        new int SetSymbolPathWide(
            [In, MarshalAs(UnmanagedType.LPWStr)] string Path);

        [PreserveSig]
        new int AppendSymbolPathWide(
            [In, MarshalAs(UnmanagedType.LPWStr)] string Addition);

        [PreserveSig]
        new int GetImagePathWide(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 PathSize);

        [PreserveSig]
        new int SetImagePathWide(
            [In, MarshalAs(UnmanagedType.LPWStr)] string Path);

        [PreserveSig]
        new int AppendImagePathWide(
            [In, MarshalAs(UnmanagedType.LPWStr)] string Addition);

        [PreserveSig]
        new int GetSourcePathWide(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 PathSize);

        [PreserveSig]
        new int GetSourcePathElementWide(
            [In] UInt32 Index,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 ElementSize);

        [PreserveSig]
        new int SetSourcePathWide(
            [In, MarshalAs(UnmanagedType.LPWStr)] string Path);

        [PreserveSig]
        new int AppendSourcePathWide(
            [In, MarshalAs(UnmanagedType.LPWStr)] string Addition);

        [PreserveSig]
        new int FindSourceFileWide(
            [In] UInt32 StartElement,
            [In, MarshalAs(UnmanagedType.LPWStr)] string File,
            [In] DEBUG_FIND_SOURCE Flags,
            [Out] out UInt32 FoundElement,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 FoundSize);

        [PreserveSig]
        new int GetSourceFileLineOffsetsWide(
            [In, MarshalAs(UnmanagedType.LPWStr)] string File,
            [Out, MarshalAs(UnmanagedType.LPArray)] UInt64[] Buffer,
            [In] Int32 BufferLines,
            [Out] out UInt32 FileLines);

        [PreserveSig]
        new int GetModuleVersionInformationWide(
            [In] UInt32 Index,
            [In] UInt64 Base,
            [In, MarshalAs(UnmanagedType.LPWStr)] string Item,
            [In] IntPtr Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 VerInfoSize);

        [PreserveSig]
        new int GetModuleNameStringWide(
            [In] DEBUG_MODNAME Which,
            [In] UInt32 Index,
            [In] UInt64 Base,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 NameSize);

        [PreserveSig]
        new int GetConstantNameWide(
            [In] UInt64 Module,
            [In] UInt32 TypeId,
            [In] UInt64 Value,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 NameSize);

        [PreserveSig]
        new int GetFieldNameWide(
            [In] UInt64 Module,
            [In] UInt32 TypeId,
            [In] UInt32 FieldIndex,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 NameSize);

        [PreserveSig]
        new int IsManagedModule(
            [In] UInt32 Index,
            [In] UInt64 Base
            );

        [PreserveSig]
        new int GetModuleByModuleName2(
            [In, MarshalAs(UnmanagedType.LPStr)] string Name,
            [In] UInt32 StartIndex,
            [In] DEBUG_GETMOD Flags,
            [Out] out UInt32 Index,
            [Out] out UInt64 Base
            );

        [PreserveSig]
        new int GetModuleByModuleName2Wide(
            [In, MarshalAs(UnmanagedType.LPWStr)] string Name,
            [In] UInt32 StartIndex,
            [In] DEBUG_GETMOD Flags,
            [Out] out UInt32 Index,
            [Out] out UInt64 Base
            );

        [PreserveSig]
        new int GetModuleByOffset2(
            [In] UInt64 Offset,
            [In] UInt32 StartIndex,
            [In] DEBUG_GETMOD Flags,
            [Out] out UInt32 Index,
            [Out] out UInt64 Base
            );

        [PreserveSig]
        new int AddSyntheticModule(
            [In] UInt64 Base,
            [In] UInt32 Size,
            [In, MarshalAs(UnmanagedType.LPStr)] string ImagePath,
            [In, MarshalAs(UnmanagedType.LPStr)] string ModuleName,
            [In] DEBUG_ADDSYNTHMOD Flags
            );

        [PreserveSig]
        new int AddSyntheticModuleWide(
            [In] UInt64 Base,
            [In] UInt32 Size,
            [In, MarshalAs(UnmanagedType.LPWStr)] string ImagePath,
            [In, MarshalAs(UnmanagedType.LPWStr)] string ModuleName,
            [In] DEBUG_ADDSYNTHMOD Flags
            );

        [PreserveSig]
        new int RemoveSyntheticModule(
            [In] UInt64 Base
            );

        [PreserveSig]
        new int GetCurrentScopeFrameIndex(
            [Out] out UInt32 Index
            );

        [PreserveSig]
        new int SetScopeFrameByIndex(
            [In] UInt32 Index
            );

        [PreserveSig]
        new int SetScopeFromJitDebugInfo(
            [In] UInt32 OutputControl,
            [In] UInt64 InfoOffset
            );

        [PreserveSig]
        new int SetScopeFromStoredEvent(
            );

        [PreserveSig]
        new int OutputSymbolByOffset(
            [In] UInt32 OutputControl,
            [In] DEBUG_OUTSYM Flags,
            [In] UInt64 Offset
            );

        [PreserveSig]
        new int GetFunctionEntryByOffset(
            [In] UInt64 Offset,
            [In] DEBUG_GETFNENT Flags,
            [In] IntPtr Buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 BufferNeeded
            );

        [PreserveSig]
        new int GetFieldTypeAndOffset(
            [In] UInt64 Module,
            [In] UInt32 ContainerTypeId,
            [In, MarshalAs(UnmanagedType.LPStr)] string Field,
            [Out] out UInt32 FieldTypeId,
            [Out] out UInt32 Offset
            );

        [PreserveSig]
        new int GetFieldTypeAndOffsetWide(
            [In] UInt64 Module,
            [In] UInt32 ContainerTypeId,
            [In, MarshalAs(UnmanagedType.LPWStr)] string Field,
            [Out] out UInt32 FieldTypeId,
            [Out] out UInt32 Offset
            );

        [PreserveSig]
        new int AddSyntheticSymbol(
            [In] UInt64 Offset,
            [In] UInt32 Size,
            [In, MarshalAs(UnmanagedType.LPStr)] string Name,
            [In] DEBUG_ADDSYNTHSYM Flags,
            [Out] out DEBUG_MODULE_AND_ID Id
            );

        [PreserveSig]
        new int AddSyntheticSymbolWide(
            [In] UInt64 Offset,
            [In] UInt32 Size,
            [In, MarshalAs(UnmanagedType.LPWStr)] string Name,
            [In] DEBUG_ADDSYNTHSYM Flags,
            [Out] out DEBUG_MODULE_AND_ID Id
            );

        [PreserveSig]
        new int RemoveSyntheticSymbol([In, MarshalAs(UnmanagedType.LPStruct)] DEBUG_MODULE_AND_ID Id
            );

        [PreserveSig]
        new int GetSymbolEntriesByOffset(
            [In] UInt64 Offset,
            [In] UInt32 Flags,
            [Out, MarshalAs(UnmanagedType.LPArray)] DEBUG_MODULE_AND_ID[] Ids,
            [Out, MarshalAs(UnmanagedType.LPArray)] UInt64[] Displacements,
            [In] UInt32 IdsCount,
            [Out] out UInt32 Entries
            );

        [PreserveSig]
        new int GetSymbolEntriesByName(
            [In, MarshalAs(UnmanagedType.LPStr)] string Symbol,
            [In] UInt32 Flags,
            [Out, MarshalAs(UnmanagedType.LPArray)] DEBUG_MODULE_AND_ID[] Ids,
            [In] UInt32 IdsCount,
            [Out] out UInt32 Entries
            );

        [PreserveSig]
        new int GetSymbolEntriesByNameWide(
            [In, MarshalAs(UnmanagedType.LPWStr)] string Symbol,
            [In] UInt32 Flags,
            [Out, MarshalAs(UnmanagedType.LPArray)] DEBUG_MODULE_AND_ID[] Ids,
            [In] UInt32 IdsCount,
            [Out] out UInt32 Entries
            );

        [PreserveSig]
        new int GetSymbolEntryByToken(
            [In] UInt64 ModuleBase,
            [In] UInt32 Token,
            [Out] out DEBUG_MODULE_AND_ID Id
            );

        [PreserveSig]
        new int GetSymbolEntryInformation(
            [In, MarshalAs(UnmanagedType.LPStruct)] DEBUG_MODULE_AND_ID Id,
            [Out] out DEBUG_SYMBOL_ENTRY Info
            );

        [PreserveSig]
        new int GetSymbolEntryString(
            [In, MarshalAs(UnmanagedType.LPStruct)] DEBUG_MODULE_AND_ID Id,
            [In] UInt32 Which,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 StringSize
            );

        [PreserveSig]
        new int GetSymbolEntryStringWide(
            [In, MarshalAs(UnmanagedType.LPStruct)] DEBUG_MODULE_AND_ID Id,
            [In] UInt32 Which,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 StringSize
            );

        [PreserveSig]
        new int GetSymbolEntryOffsetRegions(
            [In, MarshalAs(UnmanagedType.LPStruct)] DEBUG_MODULE_AND_ID Id,
            [In] UInt32 Flags,
            [Out, MarshalAs(UnmanagedType.LPArray)] DEBUG_OFFSET_REGION[] Regions,
            [In] UInt32 RegionsCount,
            [Out] out UInt32 RegionsAvail
            );

        [PreserveSig]
        new int GetSymbolEntryBySymbolEntry(
            [In, MarshalAs(UnmanagedType.LPStruct)] DEBUG_MODULE_AND_ID FromId,
            [In] UInt32 Flags,
            [Out] out DEBUG_MODULE_AND_ID ToId
            );

        [PreserveSig]
        new int GetSourceEntriesByOffset(
            [In] UInt64 Offset,
            [In] UInt32 Flags,
            [Out, MarshalAs(UnmanagedType.LPArray)] DEBUG_SYMBOL_SOURCE_ENTRY[] Entries,
            [In] UInt32 EntriesCount,
            [Out] out UInt32 EntriesAvail
            );

        [PreserveSig]
        new int GetSourceEntriesByLine(
            [In] UInt32 Line,
            [In, MarshalAs(UnmanagedType.LPStr)] string File,
            [In] UInt32 Flags,
            [Out, MarshalAs(UnmanagedType.LPArray)] DEBUG_SYMBOL_SOURCE_ENTRY[] Entries,
            [In] UInt32 EntriesCount,
            [Out] out UInt32 EntriesAvail
            );

        [PreserveSig]
        new int GetSourceEntriesByLineWide(
            [In] UInt32 Line,
            [In, MarshalAs(UnmanagedType.LPWStr)] string File,
            [In] UInt32 Flags,
            [Out, MarshalAs(UnmanagedType.LPArray)] DEBUG_SYMBOL_SOURCE_ENTRY[] Entries,
            [In] UInt32 EntriesCount,
            [Out] out UInt32 EntriesAvail
            );

        [PreserveSig]
        new int GetSourceEntryString(
            [In, MarshalAs(UnmanagedType.LPStruct)] DEBUG_SYMBOL_SOURCE_ENTRY Entry,
            [In] UInt32 Which,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 StringSize
            );

        [PreserveSig]
        new int GetSourceEntryStringWide(
            [In, MarshalAs(UnmanagedType.LPStruct)] DEBUG_SYMBOL_SOURCE_ENTRY Entry,
            [In] UInt32 Which,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 StringSize
            );

        [PreserveSig]
        new int GetSourceEntryOffsetRegions(
            [In, MarshalAs(UnmanagedType.LPStruct)] DEBUG_SYMBOL_SOURCE_ENTRY Entry,
            [In] UInt32 Flags,
            [Out, MarshalAs(UnmanagedType.LPArray)] DEBUG_OFFSET_REGION[] Regions,
            [In] UInt32 RegionsCount,
            [Out] out UInt32 RegionsAvail
            );

        [PreserveSig]
        new int GetSourceEntryBySourceEntry(
            [In, MarshalAs(UnmanagedType.LPStruct)] DEBUG_SYMBOL_SOURCE_ENTRY FromEntry,
            [In] UInt32 Flags,
            [Out] out DEBUG_SYMBOL_SOURCE_ENTRY ToEntry
            );

        /* IDebugSymbols4 */

        [PreserveSig]
        int GetScopeEx(
            [Out] out UInt64 InstructionOffset,
            [Out] out DEBUG_STACK_FRAME_EX ScopeFrame,
            [In] IntPtr ScopeContext,
            [In] UInt32 ScopeContextSize
            );

        [PreserveSig]
        int SetScopeEx(
            [In] UInt64 InstructionOffset,
            [In, MarshalAs(UnmanagedType.LPStruct)] DEBUG_STACK_FRAME_EX ScopeFrame,
            [In] IntPtr ScopeContext,
            [In] UInt32 ScopeContextSize
            );

        [PreserveSig]
        int GetNameByInlineContext(
            [In] UInt64 Offset,
            [In] UInt32 InlineContext,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder NameBuffer,
            [In] Int32 NameBufferSize,
            [Out] out UInt32 NameSize,
            [Out] out UInt64 Displacement
            );

        [PreserveSig]
        int GetNameByInlineContextWide(
            [In] UInt64 Offset,
            [In] UInt32 InlineContext,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder NameBuffer,
            [In] Int32 NameBufferSize,
            [Out] out UInt32 NameSize,
            [Out] out UInt64 Displacement
            );

        [PreserveSig]
        int GetLineByInlineContext(
            [In] UInt64 Offset,
            [In] UInt32 InlineContext,
            [Out] out UInt32 Line,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder FileBuffer,
            [In] Int32 FileBufferSize,
            [Out] out UInt32 FileSize,
            [Out] out UInt64 Displacement
            );

        [PreserveSig]
        int GetLineByInlineContextWide(
            [In] UInt64 Offset,
            [In] UInt32 InlineContext,
            [Out] out UInt32 Line,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder FileBuffer,
            [In] Int32 FileBufferSize,
            [Out] out UInt32 FileSize,
            [Out] out UInt64 Displacement
            );

        [PreserveSig]
        int OutputSymbolByInlineContext(
            [In] UInt32 OutputControl,
            [In] UInt32 Flags,
            [In] UInt64 Offset,
            [In] UInt32 InlineContext
            );
    }
}