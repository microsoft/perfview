using System;
using System.Text;
using System.Runtime.InteropServices;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("7df74a86-b03f-407f-90ab-a20dadcead08")]
    public interface IDebugControl3 : IDebugControl2
    {
        /* IDebugControl */

        [PreserveSig]
        new int GetInterrupt();

        [PreserveSig]
        new int SetInterrupt(
            [In] DEBUG_INTERRUPT Flags);

        [PreserveSig]
        new int GetInterruptTimeout(
            [Out] out UInt32 Seconds);

        [PreserveSig]
        new int SetInterruptTimeout(
            [In] UInt32 Seconds);

        [PreserveSig]
        new int GetLogFile(
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 FileSize,
            [Out, MarshalAs(UnmanagedType.Bool)] out bool Append);

        [PreserveSig]
        new int OpenLogFile(
            [In, MarshalAs(UnmanagedType.LPStr)] string File,
            [In, MarshalAs(UnmanagedType.Bool)] bool Append);

        [PreserveSig]
        new int CloseLogFile();

        [PreserveSig]
        new int GetLogMask(
            [Out] out DEBUG_OUTPUT Mask);

        [PreserveSig]
        new int SetLogMask(
            [In] DEBUG_OUTPUT Mask);

        [PreserveSig]
        new int Input(
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 InputSize);

        [PreserveSig]
        new int ReturnInput(
            [In, MarshalAs(UnmanagedType.LPStr)] string Buffer);

        [PreserveSig]
        new int Output(
            [In] DEBUG_OUTPUT Mask,
            [In, MarshalAs(UnmanagedType.LPStr)] string Format);

        [PreserveSig]
        new int OutputVaList( /* THIS SHOULD NEVER BE CALLED FROM C# */
            [In] DEBUG_OUTPUT Mask,
            [In, MarshalAs(UnmanagedType.LPStr)] string Format,
            [In] IntPtr va_list_Args);

        [PreserveSig]
        new int ControlledOutput(
            [In] DEBUG_OUTCTL OutputControl,
            [In] DEBUG_OUTPUT Mask,
            [In, MarshalAs(UnmanagedType.LPStr)] string Format);

        [PreserveSig]
        new int ControlledOutputVaList( /* THIS SHOULD NEVER BE CALLED FROM C# */
            [In] DEBUG_OUTCTL OutputControl,
            [In] DEBUG_OUTPUT Mask,
            [In, MarshalAs(UnmanagedType.LPStr)] string Format,
            [In] IntPtr va_list_Args);

        [PreserveSig]
        new int OutputPrompt(
            [In] DEBUG_OUTCTL OutputControl,
            [In, MarshalAs(UnmanagedType.LPStr)] string Format);

        [PreserveSig]
        new int OutputPromptVaList( /* THIS SHOULD NEVER BE CALLED FROM C# */
            [In] DEBUG_OUTCTL OutputControl,
            [In, MarshalAs(UnmanagedType.LPStr)] string Format,
            [In] IntPtr va_list_Args);

        [PreserveSig]
        new int GetPromptText(
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 TextSize);

        [PreserveSig]
        new int OutputCurrentState(
            [In] DEBUG_OUTCTL OutputControl,
            [In] DEBUG_CURRENT Flags);

        [PreserveSig]
        new int OutputVersionInformation(
            [In] DEBUG_OUTCTL OutputControl);

        [PreserveSig]
        new int GetNotifyEventHandle(
            [Out] out UInt64 Handle);

        [PreserveSig]
        new int SetNotifyEventHandle(
            [In] UInt64 Handle);

        [PreserveSig]
        new int Assemble(
            [In] UInt64 Offset,
            [In, MarshalAs(UnmanagedType.LPStr)] string Instr,
            [Out] out UInt64 EndOffset);

        [PreserveSig]
        new int Disassemble(
            [In] UInt64 Offset,
            [In] DEBUG_DISASM Flags,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 DisassemblySize,
            [Out] out UInt64 EndOffset);

        [PreserveSig]
        new int GetDisassembleEffectiveOffset(
            [Out] out UInt64 Offset);

        [PreserveSig]
        new int OutputDisassembly(
            [In] DEBUG_OUTCTL OutputControl,
            [In] UInt64 Offset,
            [In] DEBUG_DISASM Flags,
            [Out] out UInt64 EndOffset);

        [PreserveSig]
        new int OutputDisassemblyLines(
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
        new int GetNearInstruction(
            [In] UInt64 Offset,
            [In] int Delta,
            [Out] out UInt64 NearOffset);

        [PreserveSig]
        new int GetStackTrace(
            [In] UInt64 FrameOffset,
            [In] UInt64 StackOffset,
            [In] UInt64 InstructionOffset,
            [Out, MarshalAs(UnmanagedType.LPArray)] DEBUG_STACK_FRAME[] Frames,
            [In] Int32 FrameSize,
            [Out] out UInt32 FramesFilled);

        [PreserveSig]
        new int GetReturnOffset(
            [Out] out UInt64 Offset);

        [PreserveSig]
        new int OutputStackTrace(
            [In] DEBUG_OUTCTL OutputControl,
            [In, MarshalAs(UnmanagedType.LPArray)] DEBUG_STACK_FRAME[] Frames,
            [In] Int32 FramesSize,
            [In] DEBUG_STACK Flags);

        [PreserveSig]
        new int GetDebuggeeType(
            [Out] out DEBUG_CLASS Class,
            [Out] out DEBUG_CLASS_QUALIFIER Qualifier);

        [PreserveSig]
        new int GetActualProcessorType(
            [Out] out IMAGE_FILE_MACHINE Type);

        [PreserveSig]
        new int GetExecutingProcessorType(
            [Out] out IMAGE_FILE_MACHINE Type);

        [PreserveSig]
        new int GetNumberPossibleExecutingProcessorTypes(
            [Out] out UInt32 Number);

        [PreserveSig]
        new int GetPossibleExecutingProcessorTypes(
            [In] UInt32 Start,
            [In] UInt32 Count,
            [Out, MarshalAs(UnmanagedType.LPArray)] IMAGE_FILE_MACHINE[] Types);

        [PreserveSig]
        new int GetNumberProcessors(
            [Out] out UInt32 Number);

        [PreserveSig]
        new int GetSystemVersion(
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
        new int GetPageSize(
            [Out] out UInt32 Size);

        [PreserveSig]
        new int IsPointer64Bit();

        [PreserveSig]
        new int ReadBugCheckData(
            [Out] out UInt32 Code,
            [Out] out UInt64 Arg1,
            [Out] out UInt64 Arg2,
            [Out] out UInt64 Arg3,
            [Out] out UInt64 Arg4);

        [PreserveSig]
        new int GetNumberSupportedProcessorTypes(
            [Out] out UInt32 Number);

        [PreserveSig]
        new int GetSupportedProcessorTypes(
            [In] UInt32 Start,
            [In] UInt32 Count,
            [Out, MarshalAs(UnmanagedType.LPArray)] IMAGE_FILE_MACHINE[] Types);

        [PreserveSig]
        new int GetProcessorTypeNames(
            [In] IMAGE_FILE_MACHINE Type,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder FullNameBuffer,
            [In] Int32 FullNameBufferSize,
            [Out] out UInt32 FullNameSize,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder AbbrevNameBuffer,
            [In] Int32 AbbrevNameBufferSize,
            [Out] out UInt32 AbbrevNameSize);

        [PreserveSig]
        new int GetEffectiveProcessorType(
            [Out] out IMAGE_FILE_MACHINE Type);

        [PreserveSig]
        new int SetEffectiveProcessorType(
            [In] IMAGE_FILE_MACHINE Type);

        [PreserveSig]
        new int GetExecutionStatus(
            [Out] out DEBUG_STATUS Status);

        [PreserveSig]
        new int SetExecutionStatus(
            [In] DEBUG_STATUS Status);

        [PreserveSig]
        new int GetCodeLevel(
            [Out] out DEBUG_LEVEL Level);

        [PreserveSig]
        new int SetCodeLevel(
            [In] DEBUG_LEVEL Level);

        [PreserveSig]
        new int GetEngineOptions(
            [Out] out DEBUG_ENGOPT Options);

        [PreserveSig]
        new int AddEngineOptions(
            [In] DEBUG_ENGOPT Options);

        [PreserveSig]
        new int RemoveEngineOptions(
            [In] DEBUG_ENGOPT Options);

        [PreserveSig]
        new int SetEngineOptions(
            [In] DEBUG_ENGOPT Options);

        [PreserveSig]
        new int GetSystemErrorControl(
            [Out] out ERROR_LEVEL OutputLevel,
            [Out] out ERROR_LEVEL BreakLevel);

        [PreserveSig]
        new int SetSystemErrorControl(
            [In] ERROR_LEVEL OutputLevel,
            [In] ERROR_LEVEL BreakLevel);

        [PreserveSig]
        new int GetTextMacro(
            [In] UInt32 Slot,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 MacroSize);

        [PreserveSig]
        new int SetTextMacro(
            [In] UInt32 Slot,
            [In, MarshalAs(UnmanagedType.LPStr)] string Macro);

        [PreserveSig]
        new int GetRadix(
            [Out] out UInt32 Radix);

        [PreserveSig]
        new int SetRadix(
            [In] UInt32 Radix);

        [PreserveSig]
        new int Evaluate(
            [In, MarshalAs(UnmanagedType.LPStr)] string Expression,
            [In] DEBUG_VALUE_TYPE DesiredType,
            [Out] out DEBUG_VALUE Value,
            [Out] out UInt32 RemainderIndex);

        [PreserveSig]
        new int CoerceValue(
            [In] DEBUG_VALUE In,
            [In] DEBUG_VALUE_TYPE OutType,
            [Out] out DEBUG_VALUE Out);

        [PreserveSig]
        new int CoerceValues(
            [In] UInt32 Count,
            [In, MarshalAs(UnmanagedType.LPArray)] DEBUG_VALUE[] In,
            [In, MarshalAs(UnmanagedType.LPArray)] DEBUG_VALUE_TYPE[] OutType,
            [Out, MarshalAs(UnmanagedType.LPArray)] DEBUG_VALUE[] Out);

        [PreserveSig]
        new int Execute(
            [In] DEBUG_OUTCTL OutputControl,
            [In, MarshalAs(UnmanagedType.LPStr)] string Command,
            [In] DEBUG_EXECUTE Flags);

        [PreserveSig]
        new int ExecuteCommandFile(
            [In] DEBUG_OUTCTL OutputControl,
            [In, MarshalAs(UnmanagedType.LPStr)] string CommandFile,
            [In] DEBUG_EXECUTE Flags);

        [PreserveSig]
        new int GetNumberBreakpoints(
            [Out] out UInt32 Number);

        [PreserveSig]
        new int GetBreakpointByIndex(
            [In] UInt32 Index,
            [Out, MarshalAs(UnmanagedType.Interface)] out IDebugBreakpoint bp);

        [PreserveSig]
        new int GetBreakpointById(
            [In] UInt32 Id,
            [Out, MarshalAs(UnmanagedType.Interface)] out IDebugBreakpoint bp);

        [PreserveSig]
        new int GetBreakpointParameters(
            [In] UInt32 Count,
            [In, MarshalAs(UnmanagedType.LPArray)] UInt32[] Ids,
            [In] UInt32 Start,
            [Out, MarshalAs(UnmanagedType.LPArray)] DEBUG_BREAKPOINT_PARAMETERS[] Params);

        [PreserveSig]
        new int AddBreakpoint(
            [In] DEBUG_BREAKPOINT_TYPE Type,
            [In] UInt32 DesiredId,
            [Out, MarshalAs(UnmanagedType.Interface)] out IDebugBreakpoint Bp);

        [PreserveSig]
        new int RemoveBreakpoint(
            [In, MarshalAs(UnmanagedType.Interface)] IDebugBreakpoint Bp);

        [PreserveSig]
        new int AddExtension(
            [In, MarshalAs(UnmanagedType.LPStr)] string Path,
            [In] UInt32 Flags,
            [Out] out UInt64 Handle);

        [PreserveSig]
        new int RemoveExtension(
            [In] UInt64 Handle);

        [PreserveSig]
        new int GetExtensionByPath(
            [In, MarshalAs(UnmanagedType.LPStr)] string Path,
            [Out] out UInt64 Handle);

        [PreserveSig]
        new int CallExtension(
            [In] UInt64 Handle,
            [In, MarshalAs(UnmanagedType.LPStr)] string Function,
            [In, MarshalAs(UnmanagedType.LPStr)] string Arguments);

        [PreserveSig]
        new int GetExtensionFunction(
            [In] UInt64 Handle,
            [In, MarshalAs(UnmanagedType.LPStr)] string FuncName,
            [Out] out IntPtr Function);

        [PreserveSig]
        new int GetWindbgExtensionApis32(
            [In, Out] ref WINDBG_EXTENSION_APIS32 Api);

        /* Must be In and Out as the nSize member has to be initialized */

        [PreserveSig]
        new int GetWindbgExtensionApis64(
            [In, Out] ref WINDBG_EXTENSION_APIS64 Api);

        /* Must be In and Out as the nSize member has to be initialized */

        [PreserveSig]
        new int GetNumberEventFilters(
            [Out] out UInt32 SpecificEvents,
            [Out] out UInt32 SpecificExceptions,
            [Out] out UInt32 ArbitraryExceptions);

        [PreserveSig]
        new int GetEventFilterText(
            [In] UInt32 Index,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 TextSize);

        [PreserveSig]
        new int GetEventFilterCommand(
            [In] UInt32 Index,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 CommandSize);

        [PreserveSig]
        new int SetEventFilterCommand(
            [In] UInt32 Index,
            [In, MarshalAs(UnmanagedType.LPStr)] string Command);

        [PreserveSig]
        new int GetSpecificFilterParameters(
            [In] UInt32 Start,
            [In] UInt32 Count,
            [Out, MarshalAs(UnmanagedType.LPArray)] DEBUG_SPECIFIC_FILTER_PARAMETERS[] Params);

        [PreserveSig]
        new int SetSpecificFilterParameters(
            [In] UInt32 Start,
            [In] UInt32 Count,
            [In, MarshalAs(UnmanagedType.LPArray)] DEBUG_SPECIFIC_FILTER_PARAMETERS[] Params);

        [PreserveSig]
        new int GetSpecificEventFilterArgument(
            [In] UInt32 Index,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 ArgumentSize);

        [PreserveSig]
        new int SetSpecificEventFilterArgument(
            [In] UInt32 Index,
            [In, MarshalAs(UnmanagedType.LPStr)] string Argument);

        [PreserveSig]
        new int GetExceptionFilterParameters(
            [In] UInt32 Count,
            [In, MarshalAs(UnmanagedType.LPArray)] UInt32[] Codes,
            [In] UInt32 Start,
            [Out, MarshalAs(UnmanagedType.LPArray)] DEBUG_EXCEPTION_FILTER_PARAMETERS[] Params);

        [PreserveSig]
        new int SetExceptionFilterParameters(
            [In] UInt32 Count,
            [In, MarshalAs(UnmanagedType.LPArray)] DEBUG_EXCEPTION_FILTER_PARAMETERS[] Params);

        [PreserveSig]
        new int GetExceptionFilterSecondCommand(
            [In] UInt32 Index,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 CommandSize);

        [PreserveSig]
        new int SetExceptionFilterSecondCommand(
            [In] UInt32 Index,
            [In, MarshalAs(UnmanagedType.LPStr)] string Command);

        [PreserveSig]
        new int WaitForEvent(
            [In] DEBUG_WAIT Flags,
            [In] UInt32 Timeout);

        [PreserveSig]
        new int GetLastEventInformation(
            [Out] out DEBUG_EVENT Type,
            [Out] out UInt32 ProcessId,
            [Out] out UInt32 ThreadId,
            [In] IntPtr ExtraInformation,
            [In] UInt32 ExtraInformationSize,
            [Out] out UInt32 ExtraInformationUsed,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Description,
            [In] Int32 DescriptionSize,
            [Out] out UInt32 DescriptionUsed);

        /* IDebugControl3 */

        [PreserveSig]
        int GetAssemblyOptions(
            [Out] out DEBUG_ASMOPT Options);

        [PreserveSig]
        int AddAssemblyOptions(
            [In] DEBUG_ASMOPT Options);

        [PreserveSig]
        int RemoveAssemblyOptions(
            [In] DEBUG_ASMOPT Options);

        [PreserveSig]
        int SetAssemblyOptions(
            [In] DEBUG_ASMOPT Options);

        [PreserveSig]
        int GetExpressionSyntax(
            [Out] out DEBUG_EXPR Flags);

        [PreserveSig]
        int SetExpressionSyntax(
            [In] DEBUG_EXPR Flags);

        [PreserveSig]
        int SetExpressionSyntaxByName(
            [In, MarshalAs(UnmanagedType.LPStr)] string AbbrevName);

        [PreserveSig]
        int GetNumberExpressionSyntaxes(
            [Out] out UInt32 Number);

        [PreserveSig]
        int GetExpressionSyntaxNames(
            [In] UInt32 Index,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder FullNameBuffer,
            [In] Int32 FullNameBufferSize,
            [Out] out UInt32 FullNameSize,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder AbbrevNameBuffer,
            [In] Int32 AbbrevNameBufferSize,
            [Out] out UInt32 AbbrevNameSize);

        [PreserveSig]
        int GetNumberEvents(
            [Out] out UInt32 Events);

        [PreserveSig]
        int GetEventIndexDescription(
            [In] UInt32 Index,
            [In] DEBUG_EINDEX Which,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 DescSize);

        [PreserveSig]
        int GetCurrentEventIndex(
            [Out] out UInt32 Index);

        [PreserveSig]
        int SetNextEventIndex(
            [In] DEBUG_EINDEX Relation,
            [In] UInt32 Value,
            [Out] out UInt32 NextIndex);
    }
}