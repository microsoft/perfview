using System;
using System.Text;
using System.Runtime.InteropServices;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("3a707211-afdd-4495-ad4f-56fecdf8163f")]
    public interface IDebugSymbols2 : IDebugSymbols
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
        int GetModuleVersionInformation(
            [In] UInt32 Index,
            [In] UInt64 Base,
            [In, MarshalAs(UnmanagedType.LPStr)] string Item,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] byte[] buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 VerInfoSize);


        [PreserveSig]
        int GetModuleNameString(
            [In] DEBUG_MODNAME Which,
            [In] UInt32 Index,
            [In] UInt64 Base,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 NameSize);

        [PreserveSig]
        int GetConstantName(
            [In] UInt64 Module,
            [In] UInt32 TypeId,
            [In] UInt64 Value,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 NameSize);

        [PreserveSig]
        int GetFieldName(
            [In] UInt64 Module,
            [In] UInt32 TypeId,
            [In] UInt32 FieldIndex,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 NameSize);

        [PreserveSig]
        int GetTypeOptions(
            [Out] out DEBUG_TYPEOPTS Options);

        [PreserveSig]
        int AddTypeOptions(
            [In] DEBUG_TYPEOPTS Options);

        [PreserveSig]
        int RemoveTypeOptions(
            [In] DEBUG_TYPEOPTS Options);

        [PreserveSig]
        int SetTypeOptions(
            [In] DEBUG_TYPEOPTS Options);
    }
}