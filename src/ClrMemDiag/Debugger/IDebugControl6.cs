using System;
using System.Text;
using System.Runtime.InteropServices;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Runtime.Interop
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("bc0d583f-126d-43a1-9cc4-a860ab1d537b")]
    public interface IDebugControl6 : IDebugControl5
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

        /* IDebugControl2 */

        [PreserveSig]
        new int GetCurrentTimeDate(
            [Out] out UInt32 TimeDate);

        [PreserveSig]
        new int GetCurrentSystemUpTime(
            [Out] out UInt32 UpTime);

        [PreserveSig]
        new int GetDumpFormatFlags(
            [Out] out DEBUG_FORMAT FormatFlags);

        [PreserveSig]
        new int GetNumberTextReplacements(
            [Out] out UInt32 NumRepl);

        [PreserveSig]
        new int GetTextReplacement(
            [In, MarshalAs(UnmanagedType.LPStr)] string SrcText,
            [In] UInt32 Index,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder SrcBuffer,
            [In] Int32 SrcBufferSize,
            [Out] out UInt32 SrcSize,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder DstBuffer,
            [In] Int32 DstBufferSize,
            [Out] out UInt32 DstSize);

        [PreserveSig]
        new int SetTextReplacement(
            [In, MarshalAs(UnmanagedType.LPStr)] string SrcText,
            [In, MarshalAs(UnmanagedType.LPStr)] string DstText);

        [PreserveSig]
        new int RemoveTextReplacements();

        [PreserveSig]
        new int OutputTextReplacements(
            [In] DEBUG_OUTCTL OutputControl,
            [In] DEBUG_OUT_TEXT_REPL Flags);

        /* IDebugControl3 */

        [PreserveSig]
        new int GetAssemblyOptions(
            [Out] out DEBUG_ASMOPT Options);

        [PreserveSig]
        new int AddAssemblyOptions(
            [In] DEBUG_ASMOPT Options);

        [PreserveSig]
        new int RemoveAssemblyOptions(
            [In] DEBUG_ASMOPT Options);

        [PreserveSig]
        new int SetAssemblyOptions(
            [In] DEBUG_ASMOPT Options);

        [PreserveSig]
        new int GetExpressionSyntax(
            [Out] out DEBUG_EXPR Flags);

        [PreserveSig]
        new int SetExpressionSyntax(
            [In] DEBUG_EXPR Flags);

        [PreserveSig]
        new int SetExpressionSyntaxByName(
            [In, MarshalAs(UnmanagedType.LPStr)] string AbbrevName);

        [PreserveSig]
        new int GetNumberExpressionSyntaxes(
            [Out] out UInt32 Number);

        [PreserveSig]
        new int GetExpressionSyntaxNames(
            [In] UInt32 Index,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder FullNameBuffer,
            [In] Int32 FullNameBufferSize,
            [Out] out UInt32 FullNameSize,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder AbbrevNameBuffer,
            [In] Int32 AbbrevNameBufferSize,
            [Out] out UInt32 AbbrevNameSize);

        [PreserveSig]
        new int GetNumberEvents(
            [Out] out UInt32 Events);

        [PreserveSig]
        new int GetEventIndexDescription(
            [In] UInt32 Index,
            [In] DEBUG_EINDEX Which,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 DescSize);

        [PreserveSig]
        new int GetCurrentEventIndex(
            [Out] out UInt32 Index);

        [PreserveSig]
        new int SetNextEventIndex(
            [In] DEBUG_EINDEX Relation,
            [In] UInt32 Value,
            [Out] out UInt32 NextIndex);

        /* IDebugControl4 */

        [PreserveSig]
        new int GetLogFileWide(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 FileSize,
            [Out, MarshalAs(UnmanagedType.Bool)] out bool Append);

        [PreserveSig]
        new int OpenLogFileWide(
            [In, MarshalAs(UnmanagedType.LPWStr)] string File,
            [In, MarshalAs(UnmanagedType.Bool)] bool Append);

        [PreserveSig]
        new int InputWide(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 InputSize);

        [PreserveSig]
        new int ReturnInputWide(
            [In, MarshalAs(UnmanagedType.LPWStr)] string Buffer);

        [PreserveSig]
        new int OutputWide(
            [In] DEBUG_OUTPUT Mask,
            [In, MarshalAs(UnmanagedType.LPWStr)] string Format);

        [PreserveSig]
        new int OutputVaListWide( /* THIS SHOULD NEVER BE CALLED FROM C# */
            [In] DEBUG_OUTPUT Mask,
            [In, MarshalAs(UnmanagedType.LPWStr)] string Format,
            [In] IntPtr va_list_Args);

        [PreserveSig]
        new int ControlledOutputWide(
            [In] DEBUG_OUTCTL OutputControl,
            [In] DEBUG_OUTPUT Mask,
            [In, MarshalAs(UnmanagedType.LPWStr)] string Format);

        [PreserveSig]
        new int ControlledOutputVaListWide( /* THIS SHOULD NEVER BE CALLED FROM C# */
            [In] DEBUG_OUTCTL OutputControl,
            [In] DEBUG_OUTPUT Mask,
            [In, MarshalAs(UnmanagedType.LPWStr)] string Format,
            [In] IntPtr va_list_Args);

        [PreserveSig]
        new int OutputPromptWide(
            [In] DEBUG_OUTCTL OutputControl,
            [In, MarshalAs(UnmanagedType.LPWStr)] string Format);

        [PreserveSig]
        new int OutputPromptVaListWide( /* THIS SHOULD NEVER BE CALLED FROM C# */
            [In] DEBUG_OUTCTL OutputControl,
            [In, MarshalAs(UnmanagedType.LPWStr)] string Format,
            [In] IntPtr va_list_Args);

        [PreserveSig]
        new int GetPromptTextWide(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 TextSize);

        [PreserveSig]
        new int AssembleWide(
            [In] UInt64 Offset,
            [In, MarshalAs(UnmanagedType.LPWStr)] string Instr,
            [Out] out UInt64 EndOffset);

        [PreserveSig]
        new int DisassembleWide(
            [In] UInt64 Offset,
            [In] DEBUG_DISASM Flags,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 DisassemblySize,
            [Out] out UInt64 EndOffset);

        [PreserveSig]
        new int GetProcessorTypeNamesWide(
            [In] IMAGE_FILE_MACHINE Type,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder FullNameBuffer,
            [In] Int32 FullNameBufferSize,
            [Out] out UInt32 FullNameSize,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder AbbrevNameBuffer,
            [In] Int32 AbbrevNameBufferSize,
            [Out] out UInt32 AbbrevNameSize);

        [PreserveSig]
        new int GetTextMacroWide(
            [In] UInt32 Slot,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 MacroSize);

        [PreserveSig]
        new int SetTextMacroWide(
            [In] UInt32 Slot,
            [In, MarshalAs(UnmanagedType.LPWStr)] string Macro);

        [PreserveSig]
        new int EvaluateWide(
            [In, MarshalAs(UnmanagedType.LPWStr)] string Expression,
            [In] DEBUG_VALUE_TYPE DesiredType,
            [Out] out DEBUG_VALUE Value,
            [Out] out UInt32 RemainderIndex);

        [PreserveSig]
        new int ExecuteWide(
            [In] DEBUG_OUTCTL OutputControl,
            [In, MarshalAs(UnmanagedType.LPWStr)] string Command,
            [In] DEBUG_EXECUTE Flags);

        [PreserveSig]
        new int ExecuteCommandFileWide(
            [In] DEBUG_OUTCTL OutputControl,
            [In, MarshalAs(UnmanagedType.LPWStr)] string CommandFile,
            [In] DEBUG_EXECUTE Flags);

        [PreserveSig]
        new int GetBreakpointByIndex2(
            [In] UInt32 Index,
            [Out, MarshalAs(UnmanagedType.Interface)] out IDebugBreakpoint2 bp);

        [PreserveSig]
        new int GetBreakpointById2(
            [In] UInt32 Id,
            [Out, MarshalAs(UnmanagedType.Interface)] out IDebugBreakpoint2 bp);

        [PreserveSig]
        new int AddBreakpoint2(
            [In] DEBUG_BREAKPOINT_TYPE Type,
            [In] UInt32 DesiredId,
            [Out, MarshalAs(UnmanagedType.Interface)] out IDebugBreakpoint2 Bp);

        [PreserveSig]
        new int RemoveBreakpoint2(
            [In, MarshalAs(UnmanagedType.Interface)] IDebugBreakpoint2 Bp);

        [PreserveSig]
        new int AddExtensionWide(
            [In, MarshalAs(UnmanagedType.LPWStr)] string Path,
            [In] UInt32 Flags,
            [Out] out UInt64 Handle);

        [PreserveSig]
        new int GetExtensionByPathWide(
            [In, MarshalAs(UnmanagedType.LPWStr)] string Path,
            [Out] out UInt64 Handle);

        [PreserveSig]
        new int CallExtensionWide(
            [In] UInt64 Handle,
            [In, MarshalAs(UnmanagedType.LPWStr)] string Function,
            [In, MarshalAs(UnmanagedType.LPWStr)] string Arguments);

        [PreserveSig]
        new int GetExtensionFunctionWide(
            [In] UInt64 Handle,
            [In, MarshalAs(UnmanagedType.LPWStr)] string FuncName,
            [Out] out IntPtr Function);

        [PreserveSig]
        new int GetEventFilterTextWide(
            [In] UInt32 Index,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 TextSize);

        [PreserveSig]
        new int GetEventFilterCommandWide(
            [In] UInt32 Index,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 CommandSize);

        [PreserveSig]
        new int SetEventFilterCommandWide(
            [In] UInt32 Index,
            [In, MarshalAs(UnmanagedType.LPWStr)] string Command);

        [PreserveSig]
        new int GetSpecificEventFilterArgumentWide(
            [In] UInt32 Index,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 ArgumentSize);

        [PreserveSig]
        new int SetSpecificEventFilterArgumentWide(
            [In] UInt32 Index,
            [In, MarshalAs(UnmanagedType.LPWStr)] string Argument);

        [PreserveSig]
        new int GetExceptionFilterSecondCommandWide(
            [In] UInt32 Index,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 CommandSize);

        [PreserveSig]
        new int SetExceptionFilterSecondCommandWide(
            [In] UInt32 Index,
            [In, MarshalAs(UnmanagedType.LPWStr)] string Command);

        [PreserveSig]
        new int GetLastEventInformationWide(
            [Out] out DEBUG_EVENT Type,
            [Out] out UInt32 ProcessId,
            [Out] out UInt32 ThreadId,
            [In] IntPtr ExtraInformation,
            [In] Int32 ExtraInformationSize,
            [Out] out UInt32 ExtraInformationUsed,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Description,
            [In] Int32 DescriptionSize,
            [Out] out UInt32 DescriptionUsed);

        [PreserveSig]
        new int GetTextReplacementWide(
            [In, MarshalAs(UnmanagedType.LPWStr)] string SrcText,
            [In] UInt32 Index,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder SrcBuffer,
            [In] Int32 SrcBufferSize,
            [Out] out UInt32 SrcSize,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder DstBuffer,
            [In] Int32 DstBufferSize,
            [Out] out UInt32 DstSize);

        [PreserveSig]
        new int SetTextReplacementWide(
            [In, MarshalAs(UnmanagedType.LPWStr)] string SrcText,
            [In, MarshalAs(UnmanagedType.LPWStr)] string DstText);

        [PreserveSig]
        new int SetExpressionSyntaxByNameWide(
            [In, MarshalAs(UnmanagedType.LPWStr)] string AbbrevName);

        [PreserveSig]
        new int GetExpressionSyntaxNamesWide(
            [In] UInt32 Index,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder FullNameBuffer,
            [In] Int32 FullNameBufferSize,
            [Out] out UInt32 FullNameSize,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder AbbrevNameBuffer,
            [In] Int32 AbbrevNameBufferSize,
            [Out] out UInt32 AbbrevNameSize);

        [PreserveSig]
        new int GetEventIndexDescriptionWide(
            [In] UInt32 Index,
            [In] DEBUG_EINDEX Which,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 DescSize);

        [PreserveSig]
        new int GetLogFile2(
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 FileSize,
            [Out] out DEBUG_LOG Flags);

        [PreserveSig]
        new int OpenLogFile2(
            [In, MarshalAs(UnmanagedType.LPStr)] string File,
            [Out] out DEBUG_LOG Flags);

        [PreserveSig]
        new int GetLogFile2Wide(
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 FileSize,
            [Out] out DEBUG_LOG Flags);

        [PreserveSig]
        new int OpenLogFile2Wide(
            [In, MarshalAs(UnmanagedType.LPWStr)] string File,
            [Out] out DEBUG_LOG Flags);

        [PreserveSig]
        new int GetSystemVersionValues(
            [Out] out UInt32 PlatformId,
            [Out] out UInt32 Win32Major,
            [Out] out UInt32 Win32Minor,
            [Out] out UInt32 KdMajor,
            [Out] out UInt32 KdMinor);

        [PreserveSig]
        new int GetSystemVersionString(
            [In] DEBUG_SYSVERSTR Which,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 StringSize);

        [PreserveSig]
        new int GetSystemVersionStringWide(
            [In] DEBUG_SYSVERSTR Which,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder Buffer,
            [In] Int32 BufferSize,
            [Out] out UInt32 StringSize);

        [PreserveSig]
        new int GetContextStackTrace(
            [In] IntPtr StartContext,
            [In] UInt32 StartContextSize,
            [Out, MarshalAs(UnmanagedType.LPArray)] DEBUG_STACK_FRAME[] Frames,
            [In] Int32 FrameSize,
            [In] IntPtr FrameContexts,
            [In] UInt32 FrameContextsSize,
            [In] UInt32 FrameContextsEntrySize,
            [Out] out UInt32 FramesFilled);

        [PreserveSig]
        new int OutputContextStackTrace(
            [In] DEBUG_OUTCTL OutputControl,
            [In, MarshalAs(UnmanagedType.LPArray)] DEBUG_STACK_FRAME[] Frames,
            [In] Int32 FramesSize,
            [In] IntPtr FrameContexts,
            [In] UInt32 FrameContextsSize,
            [In] UInt32 FrameContextsEntrySize,
            [In] DEBUG_STACK Flags);

        [PreserveSig]
        new int GetStoredEventInformation(
            [Out] out DEBUG_EVENT Type,
            [Out] out UInt32 ProcessId,
            [Out] out UInt32 ThreadId,
            [In] IntPtr Context,
            [In] UInt32 ContextSize,
            [Out] out UInt32 ContextUsed,
            [In] IntPtr ExtraInformation,
            [In] UInt32 ExtraInformationSize,
            [Out] out UInt32 ExtraInformationUsed);

        [PreserveSig]
        new int GetManagedStatus(
            [Out] out DEBUG_MANAGED Flags,
            [In] DEBUG_MANSTR WhichString,
            [Out, MarshalAs(UnmanagedType.LPStr)] StringBuilder String,
            [In] Int32 StringSize,
            [Out] out UInt32 StringNeeded);

        [PreserveSig]
        new int GetManagedStatusWide(
            [Out] out DEBUG_MANAGED Flags,
            [In] DEBUG_MANSTR WhichString,
            [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder String,
            [In] Int32 StringSize,
            [Out] out UInt32 StringNeeded);

        [PreserveSig]
        new int ResetManagedStatus(
            [In] DEBUG_MANRESET Flags);

        /* IDebugControl5 */

        [PreserveSig]
        new int GetStackTraceEx(
            [In] UInt64 FrameOffset,
            [In] UInt64 StackOffset,
            [In] UInt64 InstructionOffset,
            [Out, MarshalAs(UnmanagedType.LPArray)] DEBUG_STACK_FRAME_EX[] Frames,
            [In] Int32 FramesSize,
            [Out] out UInt32 FramesFilled);

        [PreserveSig]
        new int OutputStackTraceEx(
            [In] UInt32 OutputControl,
            [In, MarshalAs(UnmanagedType.LPArray)] DEBUG_STACK_FRAME_EX[] Frames,
            [In] Int32 FramesSize,
            [In] DEBUG_STACK Flags);

        [PreserveSig]
        new int GetContextStackTraceEx(
            [In] IntPtr StartContext,
            [In] UInt32 StartContextSize,
            [Out, MarshalAs(UnmanagedType.LPArray)] DEBUG_STACK_FRAME_EX[] Frames,
            [In] Int32 FramesSize,
            [In] IntPtr FrameContexts,
            [In] UInt32 FrameContextsSize,
            [In] UInt32 FrameContextsEntrySize,
            [Out] out UInt32 FramesFilled);

        [PreserveSig]
        new int OutputContextStackTraceEx(
            [In] UInt32 OutputControl,
            [In, MarshalAs(UnmanagedType.LPArray)] DEBUG_STACK_FRAME_EX[] Frames,
            [In] Int32 FramesSize,
            [In] IntPtr FrameContexts,
            [In] UInt32 FrameContextsSize,
            [In] UInt32 FrameContextsEntrySize,
            [In] DEBUG_STACK Flags);

        [PreserveSig]
        new int GetBreakpointByGuid(
            [In, MarshalAs(UnmanagedType.LPStruct)] Guid Guid,
            [Out] out IDebugBreakpoint3 Bp);

        /* IDebugControl6 */

        [PreserveSig]
        int GetExecutionStatusEx([Out] out DEBUG_STATUS Status);

        [PreserveSig]
        int GetSynchronizationStatus(
            [Out] out UInt32 SendsAttempted,
            [Out] out UInt32 SecondsSinceLastResponse);
    }
}