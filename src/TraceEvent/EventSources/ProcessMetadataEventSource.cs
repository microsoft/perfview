using System;
using System.Diagnostics.Tracing;

namespace Microsoft.Diagnostics.Tracing
{
    [EventSource(Name = "ProcessMetadataEventSource")]
    public sealed class ProcessMetadataEventSource : EventSource
    {
        public class Tasks
        {
            public const EventTask Process = (EventTask)1;
            public const EventTask Thread = (EventTask)2;
            public const EventTask Module = (EventTask)3;
        }

        [Event(1, Opcode = EventOpcode.Start, Task = Tasks.Process)]
        public void ProcessStart(long ProcessId, long ParentProcessId, string Executable, string CommandLine)
        {
            this.WriteEvent(1, ProcessId, ParentProcessId, Executable, CommandLine);
        }

        [Event(2, Opcode = EventOpcode.Stop, Task = Tasks.Process)]
        public void ProcessExit(long ProcessId, long ParentProcessId, int ExitCode, string Executable, string CommandLine)
        {
            this.WriteEvent(2, ProcessId, ParentProcessId, ExitCode, Executable, CommandLine);
        }

        [Event(3, Opcode = EventOpcode.Start, Task = Tasks.Thread)]
        public void ThreadCreate(long ProcessId, long ThreadId, ulong StackBaseAddress, string ThreadName)
        {
            this.WriteEvent(3, ProcessId, ThreadId, StackBaseAddress, ThreadName);
        }

        [Event(4, Opcode = EventOpcode.Stop, Task = Tasks.Thread)]
        public void ThreadDestroy(long ProcessId, long ThreadId, ulong StackBaseAddress, string ThreadName)
        {
            this.WriteEvent(4, ProcessId, ThreadId, StackBaseAddress, ThreadName);
        }

        [Event(5, Opcode = EventOpcode.Start, Task = Tasks.Module)]
        public void ModuleLoad(long ProcessId, ulong LoadAddress, long ModuleSize, Guid DebugGuid, int DebugAge, string ModuleFilePath, string DebugModuleFileName)
        {
            this.WriteEvent(5, ProcessId, LoadAddress, ModuleSize, DebugGuid, DebugAge, ModuleFilePath, DebugModuleFileName);
        }

        [Event(6, Opcode = EventOpcode.Stop, Task = Tasks.Module)]
        public void ModuleUnload(long ProcessId, long LoadAddress, long ModuleSize, string ModuleFilePath)
        {
            this.WriteEvent(6, ProcessId, LoadAddress, ModuleSize, ModuleFilePath);
        }
    }
}