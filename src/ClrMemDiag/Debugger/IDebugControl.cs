using System;
using System.Text;
using System.Runtime.InteropServices;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("5182e668-105e-416e-ad92-24ef800424ba")]
    public interface IDebugControl
    {
        /* IDebugControl */

        [PreserveSig]
        int GetInterrupt();

        [PreserveSig]
        int SetInterrupt(
            [In] DEBUG_INTERRUPT Flags);

        [PreserveSig]
        int GetInterruptTimeout(
            [Out] out UInt32 Seconds);

        [PreserveSig]
        int SetInterruptTimeout(
            [In] UInt32 Seconds);

        [PreserveSig]
        int GetLogFile(
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 FileSize,
            [Out, MarshalAs(UnmanagedType.Bool)] out bool Append);

        [PreserveSig]
        int OpenLogFile(
            [In, MarshalAs(UnmanagedType.LPStr)] string File,
            [In, MarshalAs(UnmanagedType.Bool)] bool Append);

        [PreserveSig]
        int CloseLogFile();

        [PreserveSig]
        int GetLogMask(
            [Out] out DEBUG_OUTPUT Mask);

        [PreserveSig]
        int SetLogMask(
            [In] DEBUG_OUTPUT Mask);

        [PreserveSig]
        int Input(
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 InputSize);

        [PreserveSig]
        int ReturnInput(
            [In, MarshalAs(UnmanagedType.LPStr)] string Buffer);

        [PreserveSig]
        int Output(
            [In] DEBUG_OUTPUT Mask,
            [In, MarshalAs(UnmanagedType.LPStr)] string Format);

        [PreserveSig]
        int OutputVaList( /* THIS SHOULD NEVER BE CALLED FROM C# */
            [In] DEBUG_OUTPUT Mask,
            [In, MarshalAs(UnmanagedType.LPStr)] string Format,
            [In] IntPtr va_list_Args);

        [PreserveSig]
        int ControlledOutput(
            [In] DEBUG_OUTCTL OutputControl,
            [In] DEBUG_OUTPUT Mask,
            [In, MarshalAs(UnmanagedType.LPStr)] string Format);

        [PreserveSig]
        int ControlledOutputVaList( /* THIS SHOULD NEVER BE CALLED FROM C# */
            [In] DEBUG_OUTCTL OutputControl,
            [In] DEBUG_OUTPUT Mask,
            [In, MarshalAs(UnmanagedType.LPStr)] string Format,
            [In] IntPtr va_list_Args);

        [PreserveSig]
        int OutputPrompt(
            [In] DEBUG_OUTCTL OutputControl,
            [In, MarshalAs(UnmanagedType.LPStr)] string Format);

        [PreserveSig]
        int OutputPromptVaList( /* THIS SHOULD NEVER BE CALLED FROM C# */
            [In] DEBUG_OUTCTL OutputControl,
            [In, MarshalAs(UnmanagedType.LPStr)] string Format,
            [In] IntPtr va_list_Args);

        [PreserveSig]
        int GetPromptText(
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 TextSize);

        [PreserveSig]
        int OutputCurrentState(
            [In] DEBUG_OUTCTL OutputControl,
            [In] DEBUG_CURRENT Flags);

        [PreserveSig]
        int OutputVersionInformation(
            [In] DEBUG_OUTCTL OutputControl);

        [PreserveSig]
        int GetNotifyEventHandle(
            [Out] out UInt64 Handle);

        [PreserveSig]
        int SetNotifyEventHandle(
            [In] UInt64 Handle);

        [PreserveSig]
        int Assemble(
            [In] UInt64 Offset,
            [In, MarshalAs(UnmanagedType.LPStr)] string Instr,
            [Out] out UInt64 EndOffset);

        [PreserveSig]
        int Disassemble(
            [In] UInt64 Offset,
            [In] DEBUG_DISASM Flags,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 DisassemblySize,
            [Out] out UInt64 EndOffset);

        [PreserveSig]
        int GetDisassembleEffectiveOffset(
            [Out] out UInt64 Offset);

        [PreserveSig]
        int OutputDisassembly(
            [In] DEBUG_OUTCTL OutputControl,
            [In] UInt64 Offset,
            [In] DEBUG_DISASM Flags,
            [Out] out UInt64 EndOffset);

        [PreserveSig]
        int OutputDisassemblyLines(
            [In] DEBUG_OUTCTL OutputControl,
            [In] UInt32 PreviousLines,
            [In] UInt32 TotalLines,
            [In] UInt64 Offset,
            [In] DEBUG_DISASM Flags,
            [Out] out UInt32 OffsetLine,
            [Out] out UInt64 StartOffset,
            [Out] out UInt64 EndOffset,
            [Out, MarshalAs(UnmanagedType.LPArray)] UInt64[] LineOffsets);

        [PreserveSig]
        int GetNearInstruction(
            [In] UInt64 Offset,
            [In] int Delta,
            [Out] out UInt64 NearOffset);

        [PreserveSig]
        int GetStackTrace(
            [In] UInt64 FrameOffset,
            [In] UInt64 StackOffset,
            [In] UInt64 InstructionOffset,
            [Out, MarshalAs(UnmanagedType.LPArray)] DEBUG_STACK_FRAME[] Frames,
            [In] Int32 FrameSize,
            [Out] out UInt32 FramesFilled);

        [PreserveSig]
        int GetReturnOffset(
            [Out] out UInt64 Offset);

        [PreserveSig]
        int OutputStackTrace(
            [In] DEBUG_OUTCTL OutputControl,
            [In, MarshalAs(UnmanagedType.LPArray)] DEBUG_STACK_FRAME[] Frames,
            [In] Int32 FramesSize,
            [In] DEBUG_STACK Flags);

        [PreserveSig]
        int GetDebuggeeType(
            [Out] out DEBUG_CLASS Class,
            [Out] out DEBUG_CLASS_QUALIFIER Qualifier);

        [PreserveSig]
        int GetActualProcessorType(
            [Out] out IMAGE_FILE_MACHINE Type);

        [PreserveSig]
        int GetExecutingProcessorType(
            [Out] out IMAGE_FILE_MACHINE Type);

        [PreserveSig]
        int GetNumberPossibleExecutingProcessorTypes(
            [Out] out UInt32 Number);

        [PreserveSig]
        int GetPossibleExecutingProcessorTypes(
            [In] UInt32 Start,
            [In] UInt32 Count,
            [Out, MarshalAs(UnmanagedType.LPArray)] IMAGE_FILE_MACHINE[] Types);

        [PreserveSig]
        int GetNumberProcessors(
            [Out] out UInt32 Number);

        [PreserveSig]
        int GetSystemVersion(
            [Out] out UInt32 PlatformId,
            [Out] out UInt32 Major,
            [Out] out UInt32 Minor,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder ServicePackString,
            [In] Int32 ServicePackStringSize,
            [Out] out UInt32 ServicePackStringUsed,
            [Out] out UInt32 ServicePackNumber,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder BuildString,
            [In] Int32 BuildStringSize,
            [Out] out UInt32 BuildStringUsed);

        [PreserveSig]
        int GetPageSize(
            [Out] out UInt32 Size);

        [PreserveSig]
        int IsPointer64Bit();

        [PreserveSig]
        int ReadBugCheckData(
            [Out] out UInt32 Code,
            [Out] out UInt64 Arg1,
            [Out] out UInt64 Arg2,
            [Out] out UInt64 Arg3,
            [Out] out UInt64 Arg4);

        [PreserveSig]
        int GetNumberSupportedProcessorTypes(
            [Out] out UInt32 Number);

        [PreserveSig]
        int GetSupportedProcessorTypes(
            [In] UInt32 Start,
            [In] UInt32 Count,
            [Out, MarshalAs(UnmanagedType.LPArray)] IMAGE_FILE_MACHINE[] Types);

        [PreserveSig]
        int GetProcessorTypeNames(
            [In] IMAGE_FILE_MACHINE Type,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder FullNameBuffer,
            [In] Int32 FullNameBufferSize,
            [Out] out UInt32 FullNameSize,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder AbbrevNameBuffer,
            [In] Int32 AbbrevNameBufferSize,
            [Out] out UInt32 AbbrevNameSize);

        [PreserveSig]
        int GetEffectiveProcessorType(
            [Out] out IMAGE_FILE_MACHINE Type);

        [PreserveSig]
        int SetEffectiveProcessorType(
            [In] IMAGE_FILE_MACHINE Type);

        [PreserveSig]
        int GetExecutionStatus(
            [Out] out DEBUG_STATUS Status);

        [PreserveSig]
        int SetExecutionStatus(
            [In] DEBUG_STATUS Status);

        [PreserveSig]
        int GetCodeLevel(
            [Out] out DEBUG_LEVEL Level);

        [PreserveSig]
        int SetCodeLevel(
            [In] DEBUG_LEVEL Level);

        [PreserveSig]
        int GetEngineOptions(
            [Out] out DEBUG_ENGOPT Options);

        [PreserveSig]
        int AddEngineOptions(
            [In] DEBUG_ENGOPT Options);

        [PreserveSig]
        int RemoveEngineOptions(
            [In] DEBUG_ENGOPT Options);

        [PreserveSig]
        int SetEngineOptions(
            [In] DEBUG_ENGOPT Options);

        [PreserveSig]
        int GetSystemErrorControl(
            [Out] out ERROR_LEVEL OutputLevel,
            [Out] out ERROR_LEVEL BreakLevel);

        [PreserveSig]
        int SetSystemErrorControl(
            [In] ERROR_LEVEL OutputLevel,
            [In] ERROR_LEVEL BreakLevel);

        [PreserveSig]
        int GetTextMacro(
            [In] UInt32 Slot,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 MacroSize);

        [PreserveSig]
        int SetTextMacro(
            [In] UInt32 Slot,
            [In, MarshalAs(UnmanagedType.LPStr)] string Macro);

        [PreserveSig]
        int GetRadix(
            [Out] out UInt32 Radix);

        [PreserveSig]
        int SetRadix(
            [In] UInt32 Radix);

        [PreserveSig]
        int Evaluate(
            [In, MarshalAs(UnmanagedType.LPStr)] string Expression,
            [In] DEBUG_VALUE_TYPE DesiredType,
            [Out] out DEBUG_VALUE Value,
            [Out] out UInt32 RemainderIndex);

        [PreserveSig]
        int CoerceValue(
            [In] DEBUG_VALUE In,
            [In] DEBUG_VALUE_TYPE OutType,
            [Out] out DEBUG_VALUE Out);

        [PreserveSig]
        int CoerceValues(
            [In] UInt32 Count,
            [In, MarshalAs(UnmanagedType.LPArray)] DEBUG_VALUE[] In,
            [In, MarshalAs(UnmanagedType.LPArray)] DEBUG_VALUE_TYPE[] OutType,
            [Out, MarshalAs(UnmanagedType.LPArray)] DEBUG_VALUE[] Out);

        [PreserveSig]
        int Execute(
            [In] DEBUG_OUTCTL OutputControl,
            [In, MarshalAs(UnmanagedType.LPStr)] string Command,
            [In] DEBUG_EXECUTE Flags);

        [PreserveSig]
        int ExecuteCommandFile(
            [In] DEBUG_OUTCTL OutputControl,
            [In, MarshalAs(UnmanagedType.LPStr)] string CommandFile,
            [In] DEBUG_EXECUTE Flags);

        [PreserveSig]
        int GetNumberBreakpoints(
            [Out] out UInt32 Number);

        [PreserveSig]
        int GetBreakpointByIndex(
            [In] UInt32 Index,
            [Out, MarshalAs(UnmanagedType.Interface)] out IDebugBreakpoint bp);

        [PreserveSig]
        int GetBreakpointById(
            [In] UInt32 Id,
            [Out, MarshalAs(UnmanagedType.Interface)] out IDebugBreakpoint bp);

        [PreserveSig]
        int GetBreakpointParameters(
            [In] UInt32 Count,
            [In, MarshalAs(UnmanagedType.LPArray)] UInt32[] Ids,
            [In] UInt32 Start,
            [Out, MarshalAs(UnmanagedType.LPArray)] DEBUG_BREAKPOINT_PARAMETERS[] Params);

        [PreserveSig]
        int AddBreakpoint(
            [In] DEBUG_BREAKPOINT_TYPE Type,
            [In] UInt32 DesiredId,
            [Out, MarshalAs(UnmanagedType.Interface)] out IDebugBreakpoint Bp);

        [PreserveSig]
        int RemoveBreakpoint(
            [In, MarshalAs(UnmanagedType.Interface)] IDebugBreakpoint Bp);

        [PreserveSig]
        int AddExtension(
            [In, MarshalAs(UnmanagedType.LPStr)] string Path,
            [In] UInt32 Flags,
            [Out] out UInt64 Handle);

        [PreserveSig]
        int RemoveExtension(
            [In] UInt64 Handle);

        [PreserveSig]
        int GetExtensionByPath(
            [In, MarshalAs(UnmanagedType.LPStr)] string Path,
            [Out] out UInt64 Handle);

        [PreserveSig]
        int CallExtension(
            [In] UInt64 Handle,
            [In, MarshalAs(UnmanagedType.LPStr)] string Function,
            [In, MarshalAs(UnmanagedType.LPStr)] string Arguments);

        [PreserveSig]
        int GetExtensionFunction(
            [In] UInt64 Handle,
            [In, MarshalAs(UnmanagedType.LPStr)] string FuncName,
            [Out] out IntPtr Function);

        [PreserveSig]
        int GetWindbgExtensionApis32(
            [In, Out] ref WINDBG_EXTENSION_APIS32 Api);

        /* Must be In and Out as the nSize member has to be initialized */

        [PreserveSig]
        int GetWindbgExtensionApis64(
            [In, Out] ref WINDBG_EXTENSION_APIS64 Api);

        /* Must be In and Out as the nSize member has to be initialized */

        [PreserveSig]
        int GetNumberEventFilters(
            [Out] out UInt32 SpecificEvents,
            [Out] out UInt32 SpecificExceptions,
            [Out] out UInt32 ArbitraryExceptions);

        [PreserveSig]
        int GetEventFilterText(
            [In] UInt32 Index,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 TextSize);

        [PreserveSig]
        int GetEventFilterCommand(
            [In] UInt32 Index,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 CommandSize);

        [PreserveSig]
        int SetEventFilterCommand(
            [In] UInt32 Index,
            [In, MarshalAs(UnmanagedType.LPStr)] string Command);

        [PreserveSig]
        int GetSpecificFilterParameters(
            [In] UInt32 Start,
            [In] UInt32 Count,
            [Out, MarshalAs(UnmanagedType.LPArray)] DEBUG_SPECIFIC_FILTER_PARAMETERS[] Params);

        [PreserveSig]
        int SetSpecificFilterParameters(
            [In] UInt32 Start,
            [In] UInt32 Count,
            [In, MarshalAs(UnmanagedType.LPArray)] DEBUG_SPECIFIC_FILTER_PARAMETERS[] Params);

        [PreserveSig]
        int GetSpecificEventFilterArgument(
            [In] UInt32 Index,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 ArgumentSize);

        [PreserveSig]
        int SetSpecificEventFilterArgument(
            [In] UInt32 Index,
            [In, MarshalAs(UnmanagedType.LPStr)] string Argument);

        [PreserveSig]
        int GetExceptionFilterParameters(
            [In] UInt32 Count,
            [In, MarshalAs(UnmanagedType.LPArray)] UInt32[] Codes,
            [In] UInt32 Start,
            [Out, MarshalAs(UnmanagedType.LPArray)] DEBUG_EXCEPTION_FILTER_PARAMETERS[] Params);

        [PreserveSig]
        int SetExceptionFilterParameters(
            [In] UInt32 Count,
            [In, MarshalAs(UnmanagedType.LPArray)] DEBUG_EXCEPTION_FILTER_PARAMETERS[] Params);

        [PreserveSig]
        int GetExceptionFilterSecondCommand(
            [In] UInt32 Index,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 CommandSize);

        [PreserveSig]
        int SetExceptionFilterSecondCommand(
            [In] UInt32 Index,
            [In, MarshalAs(UnmanagedType.LPStr)] string Command);

        [PreserveSig]
        int WaitForEvent(
            [In] DEBUG_WAIT Flags,
            [In] UInt32 Timeout);

        [PreserveSig]
        int GetLastEventInformation(
            [Out] out DEBUG_EVENT Type,
            [Out] out UInt32 ProcessId,
            [Out] out UInt32 ThreadId,
            [In] IntPtr ExtraInformation,
            [In] UInt32 ExtraInformationSize,
            [Out] out UInt32 ExtraInformationUsed,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Description,
            [In] Int32 DescriptionSize,
            [Out] out UInt32 DescriptionUsed);
    }
}