using Microsoft.Diagnostics.Tracing.Parsers;
using System;
using System.Diagnostics.Tracing;

[EventSource(Name = "PerfView")]
internal class PerfViewLogger : System.Diagnostics.Tracing.EventSource
{
    [Event(1)]
    public void Mark(string message) { WriteEvent(1, message); }
    [Event(2, Opcode = EventOpcode.Start, Task = Tasks.Tracing)]
    public void StartTracing() { WriteEvent(2); }
    [Event(3, Opcode = EventOpcode.Stop, Task = Tasks.Tracing)]
    public void StopTracing() { WriteEvent(3); }
    [Event(4, Opcode = EventOpcode.Start, Task = Tasks.Rundown)]
    public void StartRundown() { WriteEvent(4); }
    [Event(5, Opcode = EventOpcode.Stop, Task = Tasks.Rundown)]
    public void StopRundown() { WriteEvent(5); }
    [Event(6)]
    public void WaitForIdle() { WriteEvent(6); }

    [Event(10)]
    public void CommandLineParameters(string commandLine, string currentDirectory, string version)
    {
        WriteEvent(10, commandLine, currentDirectory, version);
    }
    [Event(11)]
    public void SessionParameters(string sessionName, string sessionFileName, int bufferSizeMB, int circularBuffSizeMB)
    {
        WriteEvent(11, sessionName, sessionFileName, bufferSizeMB, circularBuffSizeMB);
    }
    [Event(12)]
    public void KernelEnableParameters(KernelTraceEventParser.Keywords keywords, KernelTraceEventParser.Keywords stacks)
    {
        WriteEvent(12, (int)keywords, (int)stacks);
    }
    [Event(13)]
    public void ClrEnableParameters(ulong keywords, Microsoft.Diagnostics.Tracing.TraceEventLevel level)
    {
        WriteEvent(13, (long)keywords, (int)level);
    }
    [Event(14)]
    public void ProviderEnableParameters(string providerName, Guid providerGuid, Microsoft.Diagnostics.Tracing.TraceEventLevel level, ulong keywords, int stacks, string values)
    {
        WriteEvent(14, providerName, providerGuid, (int)level, keywords, stacks, values);
    }
    [Event(15)]
    private void StartAndStopTimes(int startTimeRelativeMSec, int stopTimeRelativeMSec)
    {
        WriteEvent(15, startTimeRelativeMSec, stopTimeRelativeMSec);
    }
    [Event(16)]
    public void DebugMessage(string message)
    {
        WriteEvent(16, message);
    }
    /// <summary>
    /// Logs the time (relative to this event firing) when the trace was started and stopped.
    /// This is useful for circular buffer situations where that may not be known.  
    /// </summary>
    [NonEvent]
    public void StartAndStopTimes()
    {
        var now = DateTime.UtcNow;
        int startTimeRelativeMSec = 0;
        if (StartTime.Ticks != 0)
        {
            startTimeRelativeMSec = (int)(now - StartTime).TotalMilliseconds;
        }

        int stopTimeRelativeMSec = 0;
        if (StopTime.Ticks != 0)
        {
            stopTimeRelativeMSec = (int)(now - StopTime).TotalMilliseconds;
        }

        StartAndStopTimes(startTimeRelativeMSec, stopTimeRelativeMSec);
    }
    [Event(17)]
    public void CpuCounterIntervalSetting(string profileSourceName, int profileSourceCount, int profileSourceID) { WriteEvent(17, profileSourceName, profileSourceCount, profileSourceID); }
    /// <summary>
    /// Logged at consistent intervals so we can see where circular buffering starts.  
    /// </summary>
    [Event(18)]
    public void Tick(string message) { WriteEvent(18, message); }
    [Event(19)]
    public void StopReason(string message) { WriteEvent(19, message); }
    [Event(20)]
    public void Unused1() { WriteEvent(20); }
    [Event(21)]
    public void Unused2() { WriteEvent(21); }
    [Event(22)]
    public void PerfViewLog(string message) { WriteEvent(22, message); }
    [Event(23)]
    public void TriggerHeapSnapshot(string outputFile, string inputArg, string qualifiers) { WriteEvent(23, outputFile, inputArg, qualifiers); }
    [Event(24)]
    public void PerformanceCounterUpdate(string counterSpec, double value) { WriteEvent(24, counterSpec, value); }
    [Event(25)]
    public void EventStopTrigger(DateTime eventTime, int processID, int threadID, string processName, string eventName, double durationMSec)
    { WriteEvent(25, eventTime, processID, threadID, processName, eventName, durationMSec); }
    [Event(26)]
    public void StopTriggerDebugMessage(DateTime eventTime, string message) { WriteEvent(26, eventTime, message); }
    [Event(27)]
    public void RuntimeVersion(string path, string version) { WriteEvent(27, path, version); }
    public class Tasks
    {
        public const EventTask Tracing = (EventTask)1;
        public const EventTask Rundown = (EventTask)2;
    };

    public static PerfViewLogger Log = new PerfViewLogger();

    // Remember the real time where we started and stopped the trace so they are there event 
    // If the Start and Stop events get lost (because of circular buffering)
    public static DateTime StartTime = DateTime.Now;
    public static DateTime StopTime = DateTime.MaxValue;
}
