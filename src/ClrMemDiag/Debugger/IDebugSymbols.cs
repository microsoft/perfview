using System;
using System.Text;
using System.Runtime.InteropServices;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("8c31e98c-983a-48a5-9016-6fe5d667a950")]
    public interface IDebugSymbols
    {
        /* IDebugSymbols */

        [PreserveSig]
        int GetSymbolOptions(
            [Out] out SYMOPT Options);

        [PreserveSig]
        int AddSymbolOptions(
            [In] SYMOPT Options);

        [PreserveSig]
        int RemoveSymbolOptions(
            [In] SYMOPT Options);

        [PreserveSig]
        int SetSymbolOptions(
            [In] SYMOPT Options);

        [PreserveSig]
        int GetNameByOffset(
            [In] UInt64 Offset,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder NameBuffer,
            [In] Int32 NameBufferSize,
            [Out] out UInt32 NameSize,
            [Out] out UInt64 Displacement);

        [PreserveSig]
        int GetOffsetByName(
            [In, MarshalAs(UnmanagedType.LPStr)] string Symbol,
            [Out] out UInt64 Offset);

        [PreserveSig]
        int GetNearNameByOffset(
            [In] UInt64 Offset,
            [In] int Delta,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder NameBuffer,
            [In] Int32 NameBufferSize,
            [Out] out UInt32 NameSize,
            [Out] out UInt64 Displacement);

        [PreserveSig]
        int GetLineByOffset(
            [In] UInt64 Offset,
            [Out] out UInt32 Line,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder FileBuffer,
            [In] Int32 FileBufferSize,
            [Out] out UInt32 FileSize,
            [Out] out UInt64 Displacement);

        [PreserveSig]
        int GetOffsetByLine(
            [In] UInt32 Line,
            [In, MarshalAs(UnmanagedType.LPStr)] string File,
            [Out] out UInt64 Offset);

        [PreserveSig]
        int GetNumberModules(
            [Out] out UInt32 Loaded,
            [Out] out UInt32 Unloaded);

        [PreserveSig]
        int GetModuleByIndex(
            [In] UInt32 Index,
            [Out] out UInt64 Base);

        [PreserveSig]
        int GetModuleByModuleName(
            [In, MarshalAs(UnmanagedType.LPStr)] string Name,
            [In] UInt32 StartIndex,
            [Out] out UInt32 Index,
            [Out] out UInt64 Base);

        [PreserveSig]
        int GetModuleByOffset(
            [In] UInt64 Offset,
            [In] UInt32 StartIndex,
            [Out] out UInt32 Index,
            [Out] out UInt64 Base);

        [PreserveSig]
        int GetModuleNames(
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
        int GetModuleParameters(
            [In] UInt32 Count,
            [In, MarshalAs(UnmanagedType.LPArray)] UInt64[] Bases,
            [In] UInt32 Start,
            [Out, MarshalAs(UnmanagedType.LPArray)] DEBUG_MODULE_PARAMETERS[] Params);

        [PreserveSig]
        int GetSymbolModule(
            [In, MarshalAs(UnmanagedType.LPStr)] string Symbol,
            [Out] out UInt64 Base);

        [PreserveSig]
        int GetTypeName(
            [In] UInt64 Module,
            [In] UInt32 TypeId,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder NameBuffer,
            [In] Int32 NameBufferSize,
            [Out] out UInt32 NameSize);

        [PreserveSig]
        int GetTypeId(
            [In] UInt64 Module,
            [In, MarshalAs(UnmanagedType.LPStr)] string Name,
            [Out] out UInt32 TypeId);

        [PreserveSig]
        int GetTypeSize(
            [In] UInt64 Module,
            [In] UInt32 TypeId,
            [Out] out UInt32 Size);

        [PreserveSig]
        int GetFieldOffset(
            [In] UInt64 Module,
            [In] UInt32 TypeId,
            [In, MarshalAs(UnmanagedType.LPStr)] string Field,
            [Out] out UInt32 Offset);

        [PreserveSig]
        int GetSymbolTypeId(
            [In, MarshalAs(UnmanagedType.LPStr)] string Symbol,
            [Out] out UInt32 TypeId,
            [Out] out UInt64 Module);

        [PreserveSig]
        int GetOffsetTypeId(
            [In] UInt64 Offset,
            [Out] out UInt32 TypeId,
            [Out] out UInt64 Module);

        [PreserveSig]
        int ReadTypedDataVirtual(
            [In] UInt64 Offset,
            [In] UInt64 Module,
            [In] UInt32 TypeId,
            [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 4)] byte[] Buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 BytesRead);

        [PreserveSig]
        int WriteTypedDataVirtual(
            [In] UInt64 Offset,
            [In] UInt64 Module,
            [In] UInt32 TypeId,
            [In] IntPtr Buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 BytesWritten);

        [PreserveSig]
        int OutputTypedDataVirtual(
            [In] DEBUG_OUTCTL OutputControl,
            [In] UInt64 Offset,
            [In] UInt64 Module,
            [In] UInt32 TypeId,
            [In] DEBUG_TYPEOPTS Flags);

        [PreserveSig]
        int ReadTypedDataPhysical(
            [In] UInt64 Offset,
            [In] UInt64 Module,
            [In] UInt32 TypeId,
            [In] IntPtr Buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 BytesRead);

        [PreserveSig]
        int WriteTypedDataPhysical(
            [In] UInt64 Offset,
            [In] UInt64 Module,
            [In] UInt32 TypeId,
            [In] IntPtr Buffer,
            [In] UInt32 BufferSize,
            [Out] out UInt32 BytesWritten);

        [PreserveSig]
        int OutputTypedDataPhysical(
            [In] DEBUG_OUTCTL OutputControl,
            [In] UInt64 Offset,
            [In] UInt64 Module,
            [In] UInt32 TypeId,
            [In] DEBUG_TYPEOPTS Flags);

        [PreserveSig]
        int GetScope(
            [Out] out UInt64 InstructionOffset,
            [Out] out DEBUG_STACK_FRAME ScopeFrame,
            [In] IntPtr ScopeContext,
            [In] UInt32 ScopeContextSize);

        [PreserveSig]
        int SetScope(
            [In] UInt64 InstructionOffset,
            [In] DEBUG_STACK_FRAME ScopeFrame,
            [In] IntPtr ScopeContext,
            [In] UInt32 ScopeContextSize);

        [PreserveSig]
        int ResetScope();

        [PreserveSig]
        int GetScopeSymbolGroup(
            [In] DEBUG_SCOPE_GROUP Flags,
            [In, MarshalAs(UnmanagedType.Interface)] IDebugSymbolGroup Update,
            [Out, MarshalAs(UnmanagedType.Interface)] out IDebugSymbolGroup Symbols);

        [PreserveSig]
        int CreateSymbolGroup(
            [Out, MarshalAs(UnmanagedType.Interface)] out IDebugSymbolGroup Group);

        [PreserveSig]
        int StartSymbolMatch(
            [In, MarshalAs(UnmanagedType.LPStr)] string Pattern,
            [Out] out UInt64 Handle);

        [PreserveSig]
        int GetNextSymbolMatch(
            [In] UInt64 Handle,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 MatchSize,
            [Out] out UInt64 Offset);

        [PreserveSig]
        int EndSymbolMatch(
            [In] UInt64 Handle);

        [PreserveSig]
        int Reload(
            [In, MarshalAs(UnmanagedType.LPStr)] string Module);

        [PreserveSig]
        int GetSymbolPath(
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 PathSize);

        [PreserveSig]
        int SetSymbolPath(
            [In, MarshalAs(UnmanagedType.LPStr)] string Path);

        [PreserveSig]
        int AppendSymbolPath(
            [In, MarshalAs(UnmanagedType.LPStr)] string Addition);

        [PreserveSig]
        int GetImagePath(
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 PathSize);

        [PreserveSig]
        int SetImagePath(
            [In, MarshalAs(UnmanagedType.LPStr)] string Path);

        [PreserveSig]
        int AppendImagePath(
            [In, MarshalAs(UnmanagedType.LPStr)] string Addition);

        [PreserveSig]
        int GetSourcePath(
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 PathSize);

        [PreserveSig]
        int GetSourcePathElement(
            [In] UInt32 Index,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 ElementSize);

        [PreserveSig]
        int SetSourcePath(
            [In, MarshalAs(UnmanagedType.LPStr)] string Path);

        [PreserveSig]
        int AppendSourcePath(
            [In, MarshalAs(UnmanagedType.LPStr)] string Addition);

        [PreserveSig]
        int FindSourceFile(
            [In] UInt32 StartElement,
            [In, MarshalAs(UnmanagedType.LPStr)] string File,
            [In] DEBUG_FIND_SOURCE Flags,
            [Out] out UInt32 FoundElement,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 FoundSize);

        [PreserveSig]
        int GetSourceFileLineOffsets(
            [In, MarshalAs(UnmanagedType.LPStr)] string File,
            [Out, MarshalAs(UnmanagedType.LPArray)] UInt64[] Buffer,
            [In] Int32 BufferLines,
            [Out] out UInt32 FileLines);
    }
}