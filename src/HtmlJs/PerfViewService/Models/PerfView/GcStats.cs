// Copyright (c) Microsoft Corporation.  All rights reserved
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Parsers.ClrPrivate;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Parsers.Symbol;
using Microsoft.Diagnostics.Tracing.Stacks;
using Microsoft.Diagnostics.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Utilities;
using Address = System.UInt64;

namespace Stats
{
    /// <summary>
    /// GCProcess holds information about GCs in a particular process. 
    /// </summary>
    /// 
    // Notes on parsing GC events:
    // GC events need to be interpreted in sequence and if we attach we 
    // may not get the whole sequence of a GC. We discard the incomplete
    // GC from the beginning and ending. 
    //
    // We can make the assumption if we have the events from the private 
    // provider it means we have events from public provider, but not the
    // other way around.
    //
    // All GC events are at informational level except the following:
    // AllocTick from the public provider
    // GCJoin from the private provider
    // We may only have events at the informational level, not verbose level.
    //
    // TODO: right now we make the assumption that for Server GC, heap is the 
    // same as the processor number - this is true except for processes that 
    // hard affinitize to only some of the processors on the machine.
    class GCProcess : ProcessLookupContract, IComparable<GCProcess>
    {
        public static ProcessLookup<GCProcess> Collect(TraceEventDispatcher source, float sampleIntervalMSec, ProcessLookup<GCProcess> perProc = null, MutableTraceEventStackSource stackSource = null, bool _doServerGCReport = false, TraceLog traceLog = null)
        {
            doServerGCReport = _doServerGCReport;

            if (perProc == null)
            {
                perProc = new ProcessLookup<GCProcess>();
            }

            source.Kernel.AddCallbackForEvents<ProcessCtrTraceData>(delegate (ProcessCtrTraceData data)
            {
                var stats = perProc[data];
                stats.PeakVirtualMB = ((double)data.PeakVirtualSize) / 1000000.0;
                stats.PeakWorkingSetMB = ((double)data.PeakWorkingSetSize) / 1000000.0;
            });

            Action<RuntimeInformationTraceData> doAtRuntimeStart = delegate (RuntimeInformationTraceData data)
            {
                var stats = perProc[data];
                stats.RuntimeVersion = "V " + data.VMMajorVersion.ToString() + "." + data.VMMinorVersion + "." + data.VMBuildNumber
                    + "." + data.VMQfeNumber;
                stats.StartupFlags = data.StartupFlags;
                stats.Bitness = (data.RuntimeDllPath.ToLower().Contains("framework64") ? 64 : 32);
                if (stats.CommandLine == null)
                    stats.CommandLine = data.CommandLine;
            };

            // log at both startup and rundown
            var clrRundown = new ClrRundownTraceEventParser(source);
            clrRundown.RuntimeStart += doAtRuntimeStart;
            source.Clr.RuntimeStart += doAtRuntimeStart;

            source.Kernel.ProcessStartGroup += delegate (ProcessTraceData data)
            {
                var stats = perProc[data];

                if (!string.IsNullOrEmpty(data.KernelImageFileName))
                {
                    // When we just have an EventSource (eg, the source was created by 
                    // ETWTraceEventSource), we don't necessarily have the process names
                    // decoded - it all depends on whether we have seen a ProcessStartGroup 
                    // event or not. When the trace was taken after the process started we 
                    // know we didn't see such an event.
                    string name = GetImageName(data.KernelImageFileName);

                    // Strictly speaking, this is not really fixing it 'cause 
                    // it doesn't handle when a process with the same name starts
                    // with the same pid. The chance of that happening is really small.
                    if (stats.isDead == true)
                    {
                        stats = perProc.Replace(data);
                    }
                }

                var commandLine = data.CommandLine;
                if (!string.IsNullOrEmpty(commandLine))
                    stats.CommandLine = commandLine;
            };

            source.Kernel.ProcessEndGroup += delegate (ProcessTraceData data)
            {
                var stats = perProc[data];

                if (string.IsNullOrEmpty(stats.ProcessName))
                {
                    stats.ProcessName = GetImageName(data.KernelImageFileName);
                }

                if (data.OpcodeName == "Stop")
                {
                    stats.isDead = true;
                }
            };

#if (!CAP)
            CircularBuffer<ThreadWorkSpan> RecentThreadSwitches = new CircularBuffer<ThreadWorkSpan>(1000);
            source.Kernel.ThreadCSwitch += delegate (CSwitchTraceData data)
            {
                RecentThreadSwitches.Add(new ThreadWorkSpan(data));
                var stats = perProc.TryGet(data);
                if (stats != null)
                {
                    stats.ThreadId2Priority[data.NewThreadID] = data.NewThreadPriority;
                    int heapIndex = stats.IsServerGCThread(data.ThreadID);
                    if ((heapIndex > -1) && !(stats.ServerGcHeap2ThreadId.ContainsKey(heapIndex)))
                    {
                        stats.ServerGcHeap2ThreadId[heapIndex] = data.ThreadID;
                    }
                }

                foreach (var gcProcess in perProc)
                {
                    GCEvent _event = gcProcess.GetCurrentGC();
                    // If we are in the middle of a GC.
                    if (_event != null)
                    {
                        if ((_event.Type != GCType.BackgroundGC) && (gcProcess.isServerGCUsed == 1))
                        {
                            _event.AddServerGcThreadSwitch(new ThreadWorkSpan(data));
                        }
                    }
                }
            };

            CircularBuffer<ThreadWorkSpan> RecentCpuSamples = new CircularBuffer<ThreadWorkSpan>(1000);
            StackSourceSample sample = new StackSourceSample(stackSource);
            source.Kernel.PerfInfoSample += delegate (SampledProfileTraceData data)
            {
                RecentCpuSamples.Add(new ThreadWorkSpan(data));
                GCProcess processWithGc = null;
                foreach (var gcProcess in perProc)
                {
                    GCEvent e = gcProcess.GetCurrentGC();
                    // If we are in the middle of a GC.
                    if (e != null)
                    {
                        if ((e.Type != GCType.BackgroundGC) && (gcProcess.isServerGCUsed == 1))
                        {
                            e.AddServerGcSample(new ThreadWorkSpan(data));
                            processWithGc = gcProcess;
                        }
                    }
                }

                if (stackSource != null && processWithGc != null)
                {
                    GCEvent e = processWithGc.GetCurrentGC();
                    sample.Metric = 1;
                    sample.TimeRelativeMSec = data.TimeStampRelativeMSec;
                    var nodeName = string.Format("Server GCs #{0} in {1} (PID:{2})", e.GCNumber, processWithGc.ProcessName, processWithGc.ProcessID);
                    var nodeIndex = stackSource.Interner.FrameIntern(nodeName);
                    sample.StackIndex = stackSource.Interner.CallStackIntern(nodeIndex, stackSource.GetCallStack(data.CallStackIndex(), data));
                    stackSource.AddSample(sample);
                }

                var stats = perProc.TryGet(data);
                if (stats != null)
                {
                    int heapIndex = stats.IsServerGCThread(data.ThreadID);

                    if ((heapIndex > -1) && !(stats.ServerGcHeap2ThreadId.ContainsKey(heapIndex)))
                    {
                        stats.ServerGcHeap2ThreadId[heapIndex] = data.ThreadID;
                    }

                    var cpuIncrement = sampleIntervalMSec;
                    stats.ProcessCpuMSec += cpuIncrement;

                    GCEvent _event = stats.GetCurrentGC();
                    // If we are in the middle of a GC.
                    if (_event != null)
                    {
                        bool isThreadDoingGC = false;
                        if ((_event.Type != GCType.BackgroundGC) && (stats.isServerGCUsed == 1))
                        {
                            if (heapIndex != -1)
                            {
                                _event.AddServerGCThreadTime(heapIndex, cpuIncrement);
                                isThreadDoingGC = true;
                            }
                        }
                        else if (data.ThreadID == stats.suspendThreadIDGC)
                        {
                            _event.GCCpuMSec += cpuIncrement;
                            isThreadDoingGC = true;
                        }
                        else if (stats.IsBGCThread(data.ThreadID))
                        {
                            Debug.Assert(stats.currentBGC != null);
                            if (stats.currentBGC != null)
                                stats.currentBGC.GCCpuMSec += cpuIncrement;
                            isThreadDoingGC = true;
                        }

                        if (isThreadDoingGC)
                        {
                            stats.GCCpuMSec += cpuIncrement;
                        }
                    }
                }
            };
#endif
            source.Clr.GCSuspendEEStart += delegate (GCSuspendEETraceData data)
            {
                var stats = perProc[data];
                switch (data.Reason)
                {
                    case GCSuspendEEReason.SuspendForGC:
                        stats.suspendThreadIDGC = data.ThreadID;
                        break;
                    case GCSuspendEEReason.SuspendForGCPrep:
                        stats.suspendThreadIDBGC = data.ThreadID;
                        break;
                    default:
                        stats.suspendThreadIDOther = data.ThreadID;
                        break;
                }

                stats.suspendTimeRelativeMSec = data.TimeStampRelativeMSec;

                if ((traceLog != null) && !stats.gotThreadInfo)
                {
                    stats.gotThreadInfo = true;
                    TraceProcess traceProc = traceLog.Processes.GetProcess(stats.ProcessID, data.TimeStampRelativeMSec);
                    if (traceProc != null)
                    {
                        foreach (var procThread in traceProc.Threads)
                        {
                            if ((procThread.ThreadInfo != null) && (procThread.ThreadInfo.Contains(".NET Server GC Thread")))
                            {
                                stats.isServerGCUsed = 1;
                                break;
                            }
                        }

                        if (stats.isServerGCUsed == 1)
                        {
                            stats.heapCount = 0;
                            stats.serverGCThreads = new Dictionary<int, int>(2);

                            foreach (var procThread in traceProc.Threads)
                            {
                                if ((procThread.ThreadInfo != null) && (procThread.ThreadInfo.StartsWith(".NET Server GC Thread")))
                                {
                                    stats.heapCount++;

                                    int startIndex = procThread.ThreadInfo.IndexOf('(');
                                    int endIndex = procThread.ThreadInfo.IndexOf(')');
                                    string heapNumString = procThread.ThreadInfo.Substring(startIndex + 1, (endIndex - startIndex - 1));
                                    int heapNum = int.Parse(heapNumString);
                                    stats.serverGCThreads[procThread.ThreadID] = heapNum;
                                    stats.ServerGcHeap2ThreadId[heapNum] = procThread.ThreadID;
                                }
                            }
                        }
                    }
                }
            };

            // In 2.0 we didn't have this event.
            source.Clr.GCSuspendEEStop += delegate (GCNoUserDataTraceData data)
            {
                GCProcess stats = perProc[data];

                if ((stats.suspendThreadIDBGC > 0) && (stats.currentBGC != null))
                {
                    stats.currentBGC._SuspendDurationMSec += data.TimeStampRelativeMSec - stats.suspendTimeRelativeMSec;
                }

                stats.suspendEndTimeRelativeMSec = data.TimeStampRelativeMSec;
            };

            source.Clr.GCRestartEEStop += delegate (GCNoUserDataTraceData data)
            {
                GCProcess stats = perProc[data];
                GCEvent _event = stats.GetCurrentGC();
                if (_event != null)
                {
                    if (_event.Type == GCType.BackgroundGC)
                    {
                        stats.AddConcurrentPauseTime(_event, data.TimeStampRelativeMSec);
                    }
                    else
                    {
                        if (!_event.isConcurrentGC)
                        {
                            Debug.Assert(_event.PauseDurationMSec == 0);
                        }
                        Debug.Assert(_event.PauseStartRelativeMSec != 0);
                        // In 2.0 Concurrent GC, since we don't know the GC's type we can't tell if it's concurrent 
                        // or not. But we know we don't have nested GCs there so simply check if we have received the
                        // GCStop event; if we have it means it's a blocking GC; otherwise it's a concurrent GC so 
                        // simply add the pause time to the GC without making the GC complete.
                        if (_event.GCDurationMSec == 0)
                        {
                            Debug.Assert(_event.is20Event);
                            _event.isConcurrentGC = true;
                            stats.AddConcurrentPauseTime(_event, data.TimeStampRelativeMSec);
                        }
                        else
                        {
                            _event.PauseDurationMSec = data.TimeStampRelativeMSec - _event.PauseStartRelativeMSec;
                            if (_event.HeapStats != null)
                            {
                                _event.isComplete = true;
                                stats.lastCompletedGC = _event;
                            }
                        }
                    }
                }

                // We don't change between a GC end and the pause resume.   
                //Debug.Assert(stats.allocTickAtLastGC == stats.allocTickCurrentMB);
                // Mark that we are not in suspension anymore.  
                stats.suspendTimeRelativeMSec = -1;
                stats.suspendThreadIDOther = -1;
                stats.suspendThreadIDBGC = -1;
                stats.suspendThreadIDGC = -1;
            };

            source.Clr.GCAllocationTick += delegate (GCAllocationTickTraceData data)
            {
                GCProcess stats = perProc[data];

                if (stats.HasAllocTickEvents == false)
                {
                    stats.HasAllocTickEvents = true;
                }

                double valueMB = data.GetAllocAmount(ref stats.SeenBadAllocTick) / 1000000.0;

                if (data.AllocationKind == GCAllocationKind.Small)
                {
                    // Would this do the right thing or is it always 0 for SOH since AllocationAmount 
                    // is an int??? 
                    stats.allocTickCurrentMB[0] += valueMB;
                }
                else
                {
                    stats.allocTickCurrentMB[1] += valueMB;
                }
            };

            source.Clr.GCStart += delegate (GCStartTraceData data)
            {
                GCProcess stats = perProc[data];

                // We need to filter the scenario where we get 2 GCStart events for each GC.
                if ((stats.suspendThreadIDGC > 0 || stats.suspendThreadIDOther > 0) &&
                    !((stats.events.Count > 0) && stats.events[stats.events.Count - 1].GCNumber == data.Count))
                {
                    GCEvent _event = new GCEvent(stats);
                    Debug.Assert(0 <= data.Depth && data.Depth <= 2);
                    // _event.GCGeneration = data.Depth;   Old style events only have this in the GCStop event.  
                    _event.Reason = data.Reason;
                    _event.GCNumber = data.Count;
                    _event.Type = data.Type;
                    _event.Index = stats.events.Count;
                    _event.is20Event = data.IsClassicProvider;
                    bool isEphemeralGCAtBGCStart = false;
                    // Detecting the ephemeral GC that happens at the beginning of a BGC.
                    if (stats.events.Count > 0)
                    {
                        GCEvent lastGCEvent = stats.events[stats.events.Count - 1];
                        if ((lastGCEvent.Type == GCType.BackgroundGC) &&
                            (!lastGCEvent.isComplete) &&
                            (data.Type == GCType.NonConcurrentGC))
                        {
                            isEphemeralGCAtBGCStart = true;
                        }
                    }

                    Debug.Assert(stats.suspendTimeRelativeMSec != -1);
                    if (isEphemeralGCAtBGCStart)
                    {
                        _event.PauseStartRelativeMSec = data.TimeStampRelativeMSec;
                    }
                    else
                    {
                        _event.PauseStartRelativeMSec = stats.suspendTimeRelativeMSec;
                        if (stats.suspendEndTimeRelativeMSec == -1)
                        {
                            stats.suspendEndTimeRelativeMSec = data.TimeStampRelativeMSec;
                        }

                        _event._SuspendDurationMSec = stats.suspendEndTimeRelativeMSec - stats.suspendTimeRelativeMSec;
                    }

                    _event.GCStartRelativeMSec = data.TimeStampRelativeMSec;
                    stats.events.Add(_event);

                    if (_event.Type == GCType.BackgroundGC)
                    {
                        stats.currentBGC = _event;
                        _event.ProcessCpuAtLastGC = stats.ProcessCpuAtLastGC;
                    }

#if (!CAP)
                    if ((_event.Type != GCType.BackgroundGC) && (stats.isServerGCUsed == 1))
                    {
                        _event.SetUpServerGcHistory();
                        foreach (var s in RecentCpuSamples)
                            _event.AddServerGcSample(s);
                        foreach (var s in RecentThreadSwitches)
                            _event.AddServerGcThreadSwitch(s);
                    }
#endif 
                }
            };

            source.Clr.GCPinObjectAtGCTime += delegate (PinObjectAtGCTimeTraceData data)
            {
                GCProcess stats = perProc[data];
                GCEvent _event = stats.GetCurrentGC();
                if (_event != null)
                {
                    if (!_event.PinnedObjects.ContainsKey(data.ObjectID))
                    {
                        _event.PinnedObjects.Add(data.ObjectID, data.ObjectSize);
                    }
                    else
                    {
                        _event.duplicatedPinningReports++;
                    }
                }
            };

            // Some builds have this as a public event, and some have it as a private event.
            // All will move to the private event, so we'll remove this code afterwards.
            source.Clr.GCPinPlugAtGCTime += delegate (PinPlugAtGCTimeTraceData data)
            {
                GCProcess stats = perProc[data];
                GCEvent _event = stats.GetCurrentGC();
                if (_event != null)
                {
                    // ObjectID is supposed to be an IntPtr. But "Address" is defined as UInt64 in 
                    // TraceEvent.
                    _event.PinnedPlugs.Add(new GCEvent.PinnedPlug(data.PlugStart, data.PlugEnd));
                }
            };

            source.Clr.GCMarkWithType += delegate (GCMarkWithTypeTraceData data)
            {
                GCProcess stats = perProc[data];
                stats.AddServerGCThreadFromMark(data.ThreadID, data.HeapNum);

                GCEvent _event = stats.GetCurrentGC();
                if (_event != null)
                {
                    if (_event.PerHeapMarkTimes == null)
                    {
                        _event.PerHeapMarkTimes = new Dictionary<int, GCEvent.MarkInfo>();
                    }

                    if (!_event.PerHeapMarkTimes.ContainsKey(data.HeapNum))
                    {
                        _event.PerHeapMarkTimes.Add(data.HeapNum, new GCEvent.MarkInfo());
                    }

                    _event.PerHeapMarkTimes[data.HeapNum].MarkTimes[(int)data.Type] = data.TimeStampRelativeMSec;
                    _event.PerHeapMarkTimes[data.HeapNum].MarkPromoted[(int)data.Type] = data.Promoted;
                }
            };

            source.Clr.GCGlobalHeapHistory += delegate (GCGlobalHeapHistoryTraceData data)
            {
                GCProcess stats = perProc[data];
                stats.ProcessGlobalHistory(data);
            };

            source.Clr.GCPerHeapHistory += delegate (GCPerHeapHistoryTraceData data)
            {
                GCProcess stats = perProc[data];
                stats.ProcessPerHeapHistory(data);
            };

            source.Clr.GCJoin += delegate (GCJoinTraceData data)
            {
                GCProcess gcProcess = perProc[data];
                GCEvent _event = gcProcess.GetCurrentGC();
                if (_event != null)
                {
                    _event.AddGcJoin(data);
                }
            };

            // See if the source knows about the CLR Private provider, if it does, then 
            var gcPrivate = new ClrPrivateTraceEventParser(source);

            gcPrivate.GCPinPlugAtGCTime += delegate (PinPlugAtGCTimeTraceData data)
            {
                GCProcess stats = perProc[data];
                GCEvent _event = stats.GetCurrentGC();
                if (_event != null)
                {
                    // ObjectID is supposed to be an IntPtr. But "Address" is defined as UInt64 in 
                    // TraceEvent.
                    _event.PinnedPlugs.Add(new GCEvent.PinnedPlug(data.PlugStart, data.PlugEnd));
                }
            };

            // Sometimes at the end of a trace I see only some mark events are included in the trace and they
            // are not in order, so need to anticipate that scenario.
            gcPrivate.GCMarkStackRoots += delegate (GCMarkTraceData data)
            {
                GCProcess stats = perProc[data];
                stats.AddServerGCThreadFromMark(data.ThreadID, data.HeapNum);

                GCEvent _event = stats.GetCurrentGC();
                if (_event != null)
                {
                    if (_event.PerHeapMarkTimes == null)
                    {
                        _event.PerHeapMarkTimes = new Dictionary<int, GCEvent.MarkInfo>();
                    }

                    if (!_event.PerHeapMarkTimes.ContainsKey(data.HeapNum))
                    {
                        _event.PerHeapMarkTimes.Add(data.HeapNum, new GCEvent.MarkInfo(false));
                    }

                    _event.PerHeapMarkTimes[data.HeapNum].MarkTimes[(int)MarkRootType.MarkStack] = data.TimeStampRelativeMSec;
                }
            };

            gcPrivate.GCMarkFinalizeQueueRoots += delegate (GCMarkTraceData data)
            {
                GCProcess stats = perProc[data];
                GCEvent _event = stats.GetCurrentGC();
                if (_event != null)
                {
                    if ((_event.PerHeapMarkTimes != null) && _event.PerHeapMarkTimes.ContainsKey(data.HeapNum))
                    {
                        _event.PerHeapMarkTimes[data.HeapNum].MarkTimes[(int)MarkRootType.MarkFQ] =
                            data.TimeStampRelativeMSec;
                    }
                }
            };

            gcPrivate.GCMarkHandles += delegate (GCMarkTraceData data)
            {
                GCProcess stats = perProc[data];
                GCEvent _event = stats.GetCurrentGC();
                if (_event != null)
                {
                    if ((_event.PerHeapMarkTimes != null) && _event.PerHeapMarkTimes.ContainsKey(data.HeapNum))
                    {
                        _event.PerHeapMarkTimes[data.HeapNum].MarkTimes[(int)MarkRootType.MarkHandles] =
                           data.TimeStampRelativeMSec;
                    }
                }
            };

            gcPrivate.GCMarkCards += delegate (GCMarkTraceData data)
            {
                GCProcess stats = perProc[data];
                GCEvent _event = stats.GetCurrentGC();
                if (_event != null)
                {
                    if ((_event.PerHeapMarkTimes != null) && _event.PerHeapMarkTimes.ContainsKey(data.HeapNum))
                    {
                        _event.PerHeapMarkTimes[data.HeapNum].MarkTimes[(int)MarkRootType.MarkOlder] =
                            data.TimeStampRelativeMSec;
                    }
                }
            };

            gcPrivate.GCGlobalHeapHistory += delegate (GCGlobalHeapHistoryTraceData data)
            {
                GCProcess stats = perProc[data];
                stats.ProcessGlobalHistory(data);
            };

            gcPrivate.GCPerHeapHistory += delegate (GCPerHeapHistoryTraceData data)
            {
                GCProcess stats = perProc[data];
                stats.ProcessPerHeapHistory(data);
            };

            gcPrivate.GCBGCStart += delegate (GCNoUserDataTraceData data)
            {
                GCProcess stats = perProc[data];
                if (stats.currentBGC != null)
                {
                    if (stats.backgroundGCThreads == null)
                    {
                        stats.backgroundGCThreads = new Dictionary<int, object>(16);
                    }
                    stats.backgroundGCThreads[data.ThreadID] = null;
                }
            };

            source.Clr.GCStop += delegate (GCEndTraceData data)
            {
                GCProcess stats = perProc[data];
                GCEvent _event = stats.GetCurrentGC();
                if (_event != null)
                {
                    _event.GCDurationMSec = data.TimeStampRelativeMSec - _event.GCStartRelativeMSec;
                    _event.GCGeneration = data.Depth;
                    _event.GCEnd();
                    Debug.Assert(_event.GCNumber == data.Count);
                }
            };

            source.Clr.GCHeapStats += delegate (GCHeapStatsTraceData data)
            {
                GCProcess stats = perProc[data];
                GCEvent _event = stats.GetCurrentGC();

                var sizeAfterMB = (data.GenerationSize1 + data.GenerationSize2 + data.GenerationSize3) / 1000000.0;
                if (_event != null)
                {
                    _event.HeapStats = (GCHeapStatsTraceData)data.Clone();

                    if (_event.Type == GCType.BackgroundGC)
                    {
                        _event.ProcessCpuMSec = stats.ProcessCpuMSec - _event.ProcessCpuAtLastGC;
                        _event.DurationSinceLastRestartMSec = data.TimeStampRelativeMSec - stats.lastRestartEndTimeRelativeMSec;
                    }
                    else
                    {
                        _event.ProcessCpuMSec = stats.ProcessCpuMSec - stats.ProcessCpuAtLastGC;
                        _event.DurationSinceLastRestartMSec = _event.PauseStartRelativeMSec - stats.lastRestartEndTimeRelativeMSec;
                    }

                    if (stats.HasAllocTickEvents)
                    {
                        _event.HasAllocTickEvents = true;
                        _event.AllocedSinceLastGCBasedOnAllocTickMB[0] = stats.allocTickCurrentMB[0] - stats.allocTickAtLastGC[0];
                        _event.AllocedSinceLastGCBasedOnAllocTickMB[1] = stats.allocTickCurrentMB[1] - stats.allocTickAtLastGC[1];
                    }

                    // This is where a background GC ends.
                    if ((_event.Type == GCType.BackgroundGC) && (stats.currentBGC != null))
                    {
                        stats.currentBGC.isComplete = true;
                        stats.lastCompletedGC = stats.currentBGC;
                        stats.currentBGC = null;
                    }

                    if (_event.isConcurrentGC)
                    {
                        Debug.Assert(_event.is20Event);
                        _event.isComplete = true;
                        stats.lastCompletedGC = _event;
                    }
                }

                stats.ProcessCpuAtLastGC = stats.ProcessCpuMSec;
                stats.allocTickAtLastGC[0] = stats.allocTickCurrentMB[0];
                stats.allocTickAtLastGC[1] = stats.allocTickCurrentMB[1];
                stats.lastRestartEndTimeRelativeMSec = data.TimeStampRelativeMSec;
            };

            source.Clr.GCTerminateConcurrentThread += delegate (GCTerminateConcurrentThreadTraceData data)
            {
                GCProcess stats = perProc[data];
                if (stats.backgroundGCThreads != null)
                {
                    stats.backgroundGCThreads = null;
                }
            };

            gcPrivate.GCBGCAllocWaitStart += delegate (BGCAllocWaitTraceData data)
            {
                GCProcess stats = perProc[data];
                Debug.Assert(stats.currentBGC != null);

                if (stats.currentBGC != null)
                {
                    stats.currentBGC.AddLOHWaitThreadInfo(data.ThreadID, data.TimeStampRelativeMSec, data.Reason, true);
                }
            };

            gcPrivate.GCBGCAllocWaitStop += delegate (BGCAllocWaitTraceData data)
            {
                GCProcess stats = perProc[data];

                GCEvent _event = stats.GetLastBGC();

                if (_event != null)
                {
                    _event.AddLOHWaitThreadInfo(data.ThreadID, data.TimeStampRelativeMSec, data.Reason, false);
                }
            };

            gcPrivate.GCJoin += delegate (GCJoinTraceData data)
            {
                GCProcess gcProcess = perProc[data];
                GCEvent _event = gcProcess.GetCurrentGC();
                if (_event != null)
                {
                    _event.AddGcJoin(data);
                }
            };

            gcPrivate.GCFinalizeObject += data =>
            {
                GCProcess gcProcess = perProc[data];
                long finalizationCount;
                gcProcess.FinalizedObjects[data.TypeName] =
                    gcProcess.FinalizedObjects.TryGetValue(data.TypeName, out finalizationCount) ?
                        finalizationCount + 1 :
                        1;
            };

            source.Process();

#if DEBUG
            foreach (GCProcess gcProcess in perProc)
                foreach (GCEvent _event in gcProcess.events)
                {
                    if (_event.isComplete)
                    {
                        Debug.Assert(_event.HeapStats != null, "Missing GC Heap Stats event");
                        //Debug.Assert(_event.PerHeapHistories != null || gcProcess.RuntimeVersion == null);
                        Debug.Assert(_event.PauseStartRelativeMSec != 0, "Missing GC RestartEE event");
                    }
                }
#endif
            // Compute rollup information.  
            foreach (GCProcess stats in perProc)
            {
                for (int i = 0; i < stats.events.Count; i++)
                {
                    GCEvent _event = stats.events[i];
                    if (!_event.isComplete)
                    {
                        continue;
                    }

                    _event.Index = i;
                    if (_event.DetailedGenDataAvailable())  //per heap histories is not null
                        stats.m_detailedGCInfo = true;

                    // Update the per-generation information 
                    stats.Generations[_event.GCGeneration].GCCount++;
                    bool isInduced = ((_event.Reason == GCReason.Induced) || (_event.Reason == GCReason.InducedNotForced));
                    if (isInduced)
                        (stats.Generations[_event.GCGeneration].NumInduced)++;

                    long PinnedObjectSizes = _event.GetPinnedObjectSizes();
                    if (PinnedObjectSizes != 0)
                    {
                        stats.Generations[_event.GCGeneration].PinnedObjectSizes += PinnedObjectSizes;
                        stats.Generations[_event.GCGeneration].NumGCWithPinEvents++;
                    }

                    int PinnedObjectPercentage = _event.GetPinnedObjectPercentage();
                    if (PinnedObjectPercentage != -1)
                    {
                        stats.Generations[_event.GCGeneration].PinnedObjectPercentage += _event.GetPinnedObjectPercentage();
                        stats.Generations[_event.GCGeneration].NumGCWithPinPlugEvents++;
                    }

                    stats.Generations[_event.GCGeneration].TotalGCCpuMSec += _event.GetTotalGCTime();
                    stats.Generations[_event.GCGeneration].TotalSizeAfterMB += _event.HeapSizeAfterMB;

                    stats.Generations[_event.GCGeneration].TotalSizePeakMB += _event.HeapSizePeakMB;
                    stats.Generations[_event.GCGeneration].TotalPromotedMB += _event.PromotedMB;
                    stats.Generations[_event.GCGeneration].TotalPauseTimeMSec += _event.PauseDurationMSec;
                    stats.Generations[_event.GCGeneration].TotalAllocatedMB += _event.AllocedSinceLastGCMB;
                    stats.Generations[_event.GCGeneration].MaxPauseDurationMSec = Math.Max(stats.Generations[_event.GCGeneration].MaxPauseDurationMSec, _event.PauseDurationMSec);
                    stats.Generations[_event.GCGeneration].MaxSizePeakMB = Math.Max(stats.Generations[_event.GCGeneration].MaxSizePeakMB, _event.HeapSizePeakMB);
                    stats.Generations[_event.GCGeneration].MaxAllocRateMBSec = Math.Max(stats.Generations[_event.GCGeneration].MaxAllocRateMBSec, _event.AllocRateMBSec);
                    stats.Generations[_event.GCGeneration].MaxPauseDurationMSec = Math.Max(stats.Generations[_event.GCGeneration].MaxPauseDurationMSec, _event.PauseDurationMSec);
                    stats.Generations[_event.GCGeneration].MaxSuspendDurationMSec = Math.Max(stats.Generations[_event.GCGeneration].MaxSuspendDurationMSec, _event._SuspendDurationMSec);

                    // And the totals 
                    stats.Total.GCCount++;
                    if (isInduced)
                        stats.Total.NumInduced++;
                    if (PinnedObjectSizes != 0)
                    {
                        stats.Total.PinnedObjectSizes += PinnedObjectSizes;
                        stats.Total.NumGCWithPinEvents++;
                    }
                    if (PinnedObjectPercentage != -1)
                    {
                        stats.Total.PinnedObjectPercentage += _event.GetPinnedObjectPercentage();
                        stats.Total.NumGCWithPinPlugEvents++;
                    }
                    stats.Total.TotalGCCpuMSec += _event.GetTotalGCTime();
                    stats.Total.TotalSizeAfterMB += _event.HeapSizeAfterMB;
                    stats.Total.TotalPromotedMB += _event.PromotedMB;
                    stats.Total.TotalSizePeakMB += _event.HeapSizePeakMB;
                    stats.Total.TotalPauseTimeMSec += _event.PauseDurationMSec;
                    stats.Total.TotalAllocatedMB += _event.AllocedSinceLastGCMB;
                    stats.Total.MaxPauseDurationMSec = Math.Max(stats.Total.MaxPauseDurationMSec, _event.PauseDurationMSec);
                    stats.Total.MaxSizePeakMB = Math.Max(stats.Total.MaxSizePeakMB, _event.HeapSizePeakMB);
                    stats.Total.MaxAllocRateMBSec = Math.Max(stats.Total.MaxAllocRateMBSec, _event.AllocRateMBSec);
                    stats.Total.MaxSuspendDurationMSec = Math.Max(stats.Total.MaxSuspendDurationMSec, _event._SuspendDurationMSec);
                }
            }
#if DEBUG
            foreach (GCProcess stats in perProc)
                for (int i = 0; i < stats.events.Count; i++)
                {
                    if (stats.events[i].isComplete)
                    {
                        Debug.Assert(stats.events[i].HeapStats != null);
                    }
                }
#endif
            return perProc;
        }

        public int ProcessID { get; set; }
        public string ProcessName { get; set; }
        bool isDead;
        public bool Interesting { get { return Total.GCCount > 0 || RuntimeVersion != null; } }
        public bool InterestingForAnalysis
        {
            get
            {
                return ((Total.GCCount > 3) &&
                        ((GetGCPauseTimePercentage() > 1.0) || (Total.MaxPauseDurationMSec > 200.0)));
            }
        }

        // A process can have one or more SBAs associated with it.
        public GCInfo[] Generations = new GCInfo[3];
        public GCInfo Total;
        public float ProcessCpuMSec;     // Total CPU time used in process (approximate)
        public float GCCpuMSec;          // CPU time used in the GC (approximate)
        public int NumberOfHeaps = 1;

        // Of all the CPU, how much as a percentage is spent in the GC. 
        public float PercentTimeInGC { get { return GCCpuMSec * 100 / ProcessCpuMSec; } }

        public static string GetImageName(string path)
        {
            int startIdx = path.LastIndexOf('\\');
            if (0 <= startIdx)
                startIdx++;
            else
                startIdx = 0;
            int endIdx = path.LastIndexOf('.');
            if (endIdx <= startIdx)
                endIdx = path.Length;
            string name = path.Substring(startIdx, endIdx - startIdx);
            return name;
        }

        // This is calculated based on the GC events which is fine for the GC analysis purpose.
        public double ProcessDuration
        {
            get
            {
                double startRelativeMSec = 0.0;

                for (int i = 0; i < events.Count; i++)
                {
                    if (events[i].isComplete)
                    {
                        startRelativeMSec = events[i].PauseStartRelativeMSec;
                        break;
                    }
                }

                if (startRelativeMSec == 0.0)
                    return 0;

                // Get the end time of the last GC.
                double endRelativeMSec = lastRestartEndTimeRelativeMSec;
                return (endRelativeMSec - startRelativeMSec);
            }
        }

        public StartupFlags StartupFlags;
        public string RuntimeVersion;
        public int Bitness = -1;
        public Dictionary<int, int> ThreadId2Priority = new Dictionary<int, int>();
        public Dictionary<int, int> ServerGcHeap2ThreadId = new Dictionary<int, int>();
        public static bool doServerGCReport = false;
        public Dictionary<string, long> FinalizedObjects = new Dictionary<string, long>();

        public string CommandLine { get; set; }

        public double PeakWorkingSetMB { get; set; }
        public double PeakVirtualMB { get; set; }

        /// <summary>
        /// Means it detected that the ETW information is in a format it does not understand.
        /// </summary>
        public bool GCVersionInfoMismatch { get; private set; }

        public void ToHtml(TextWriter writer, string fileName)
        {
            writer.WriteLine("<H3><A Name=\"Stats_{0}\"><font color=\"blue\">GC Stats for for Process {1,5}: {2}</font><A></H3>", ProcessID, ProcessID, ProcessName);
            writer.WriteLine("<UL>");
            if (GCVersionInfoMismatch)
                writer.WriteLine("<LI><Font size=3 color=\"red\">Warning: Did not recognize the V4.0 GC Information events.  Falling back to V2.0 behavior.</font></LI>");
            if (!string.IsNullOrEmpty(CommandLine))
                writer.WriteLine("<LI>CommandLine: {0}</LI>", CommandLine);
            writer.WriteLine("<LI>Runtime Version: {0}</LI>", RuntimeVersion != null ? RuntimeVersion : "<Unknown Runtime Version>");
            writer.WriteLine("<LI>CLR Startup Flags: {0}</LI>", RuntimeVersion != null ? StartupFlags.ToString() : "<Unknown Startup Flags>");
            writer.WriteLine("<LI>Total CPU Time: {0:n0} msec</LI>", ProcessCpuMSec);
            writer.WriteLine("<LI>Total GC CPU Time: {0:n0} msec</LI>", Total.TotalGCCpuMSec);
            writer.WriteLine("<LI>Total Allocs  : {0:n3} MB</LI>", Total.TotalAllocatedMB);
            writer.WriteLine("<LI>GC CPU MSec/MB Alloc : {0:n3} MSec/MB</LI>", Total.TotalGCCpuMSec / Total.TotalAllocatedMB);
            writer.WriteLine("<LI>Total GC Pause: {0:n1} msec</LI>", Total.TotalPauseTimeMSec);
            writer.WriteLine("<LI>% Time paused for Garbage Collection: {0:f1}%</LI>", GetGCPauseTimePercentage());

            writer.WriteLine("<LI>% CPU Time spent Garbage Collecting: {0:f1}%</LI>", Total.TotalGCCpuMSec * 100.0 / ProcessCpuMSec);

            writer.WriteLine("<LI>Max GC Heap Size: {0:n3} MB</LI>", Total.MaxSizePeakMB);
            if (PeakWorkingSetMB != 0)
                writer.WriteLine("<LI>Peak Process Working Set: {0:n3} MB</LI>", PeakWorkingSetMB);
            if (PeakWorkingSetMB != 0)
                writer.WriteLine("<LI>Peak Virtual Memory Usage: {0:n3} MB</LI>", PeakVirtualMB);

            var usersGuideFile = ClrStatsUsersGuide.WriteUsersGuide(fileName);
            writer.WriteLine("<LI> <A HREF=\"{0}#UnderstandingGCPerf\">GC Perf Users Guide</A></LI>", usersGuideFile);

            writer.WriteLine("<LI><A HREF=\"#Events_Pause_{0}\">GCs that &gt; 200 msec Events</A></LI>", ProcessID);
            writer.WriteLine("<LI><A HREF=\"#LOH_allocation_Pause_{0}\">LOH allocation pause (due to background GC) &gt; 200 msec Events</A></LI>", ProcessID);
            writer.WriteLine("<LI><A HREF=\"#Events_Gen2_{0}\">GCs that were Gen2</A></LI>", ProcessID);

            writer.WriteLine("<LI><A HREF=\"#Events_{0}\">Individual GC Events</A> </LI>", ProcessID);
            writer.WriteLine("<UL><LI> <A HREF=\"command:excel/{0}\">View in Excel</A></LI></UL>", ProcessID);
            writer.WriteLine("<LI> <A HREF=\"command:excel/perGeneration/{0}\">Per Generation GC Events in Excel</A></LI>", ProcessID);
            if (m_detailedGCInfo)
                writer.WriteLine("<LI> <A HREF=\"command:xml/{0}\">Raw Data XML file (for debugging)</A></LI>", ProcessID);
            if (FinalizedObjects.Count > 0)
            {
                writer.WriteLine("<LI><A HREF=\"#Finalization_{0}\">Finalized Objects</A> </LI>", ProcessID);
                writer.WriteLine("<UL><LI> <A HREF=\"command:excelFinalization/{0}\">View in Excel</A></LI></UL>", ProcessID);
                writer.WriteLine("<UL><LI> <A HREF=\"{0}#UnderstandingFinalization\">Finalization Perf Users Guide</A></LI></UL>", usersGuideFile);
            }
            else
            {
                writer.WriteLine("<LI><I>No finalized object counts available. No objects were finalized and/or the Finalizers option was not selected.</I></LI>");
            }
            writer.WriteLine("</UL>");
            writer.WriteLine("<Center>");
            writer.WriteLine("<Table Border=\"1\">");
            writer.WriteLine("<TR><TH colspan=\"12\" Align=\"Center\">GC Rollup By Generation</TH></TR>");
            writer.WriteLine("<TR><TH colspan=\"12\" Align=\"Center\">All times are in msec.</TH></TR>");
            writer.WriteLine("<TR>" +
                             "<TH>Gen</TH>" +
                             "<TH>Count</TH>" +
                             "<TH>Max<BR/>Pause</TH>" +
                             "<TH>Max<BR/>Peak MB</TH>" +
                             "<TH>Max Alloc<BR/>MB/sec</TH>" +
                             "<TH>Total<BR/>Pause</TH>" +
                             "<TH>Total<BR/>Alloc MB</TH>" +
                             "<TH>Alloc MB/<BR/>MSec GC</TH>" +
                             "<TH>Survived MB/<BR/>MSec GC</TH>" +
                             "<TH>Mean<BR/>Pause</TH>" +
                             "<TH>Induced</TH>" +

                             (ShowPinnedInformation ?
                             "<TH>Avg Pinned Obj %</TH>"
                             : string.Empty) +

                             "</TR>");
            writer.WriteLine("<TR>" +
                             "<TD Align=\"Center\">{0}</TD>" +
                             "<TD Align=\"Center\">{1}</TD>" +
                             "<TD Align=\"Center\">{2:n1}</TD>" +
                             "<TD Align=\"Center\">{3:n1}</TD>" +
                             "<TD Align=\"Center\">{4:n3}</TD>" +
                             "<TD Align=\"Center\">{5:n1}</TD>" +
                             "<TD Align=\"Center\">{6:n1}</TD>" +
                             "<TD Align=\"Center\">{7:n1}</TD>" +
                             "<TD Align=\"Center\">{8:n3}</TD>" +
                             "<TD Align=\"Center\">{9:n1}</TD>" +
                             "<TD Align=\"Center\">{10}</TD>" +

                             (ShowPinnedInformation ?
                             "<TD Align=\"Center\">{11}</TD>"
                             : string.Empty) +

                             "</TR>",
                            "ALL",
                            Total.GCCount,
                            Total.MaxPauseDurationMSec,
                            Total.MaxSizePeakMB,
                            Total.MaxAllocRateMBSec,
                            Total.TotalPauseTimeMSec,
                            Total.TotalAllocatedMB,
                            Total.TotalAllocatedMB / Total.TotalPauseTimeMSec,
                            Total.TotalPromotedMB / Total.TotalGCCpuMSec,
                            Total.MeanPauseDurationMSec,
                            Total.NumInduced,
                            (((Total.NumGCWithPinEvents != 0) && (Total.NumGCWithPinEvents == Total.NumGCWithPinPlugEvents)) ? (Total.PinnedObjectPercentage / Total.NumGCWithPinEvents) : double.NaN));

            for (int genNum = 0; genNum < Generations.Length; genNum++)
            {
                GCInfo gen = Generations[genNum];
                writer.WriteLine("<TR>" +
                                 "<TD Align=\"Center\">{0}</TD>" +
                                 "<TD Align=\"Center\">{1}</TD>" +
                                 "<TD Align=\"Center\">{2:n1}</TD>" +
                                 "<TD Align=\"Center\">{3:n1}</TD>" +
                                 "<TD Align=\"Center\">{4:n3}</TD>" +
                                 "<TD Align=\"Center\">{5:n1}</TD>" +
                                 "<TD Align=\"Center\">{6:n1}</TD>" +
                                 "<TD Align=\"Center\">{7:n1}</TD>" +
                                 "<TD Align=\"Center\">{8:n3}</TD>" +
                                 "<TD Align=\"Center\">{9:n1}</TD>" +
                                 "<TD Align=\"Center\">{10}</TD>" +

                                 (ShowPinnedInformation ?
                                 "<TD Align=\"Center\">{11}</TD>"
                                 : string.Empty) +

                                 "</TR>",
                                genNum,
                                gen.GCCount,
                                gen.MaxPauseDurationMSec,
                                gen.MaxSizePeakMB,
                                gen.MaxAllocRateMBSec,
                                gen.TotalPauseTimeMSec,
                                gen.TotalAllocatedMB,
                                gen.TotalPauseTimeMSec / Total.TotalAllocatedMB,
                                gen.TotalPromotedMB / gen.TotalGCCpuMSec,
                                gen.MeanPauseDurationMSec,
                                gen.NumInduced,
                                (((gen.NumGCWithPinEvents != 0) && (gen.NumGCWithPinEvents == gen.NumGCWithPinPlugEvents)) ? (gen.PinnedObjectPercentage / gen.NumGCWithPinEvents) : double.NaN));
            }
            writer.WriteLine("</Table>");
            writer.WriteLine("</Center>");

            writer.WriteLine("<HR/>");
            writer.WriteLine("<H4><A Name=\"Events_Pause_{0}\">Pause &gt; 200 Msec GC Events for Process {1,5}: {2}<A></H4>", ProcessID, ProcessID, ProcessName);
            PrintEventTable(writer, 0, delegate (GCEvent _event) { return _event.PauseDurationMSec > 200; });

            writer.WriteLine("<HR/>");
            writer.WriteLine("<H4><A Name=\"LOH_allocation_Pause_{0}\">LOH Allocation Pause (due to background GC) &gt; 200 Msec for Process {1,5}: {2}<A></H4>", ProcessID, ProcessID, ProcessName);
            PrintLOHAllocLargePauseTable(writer, 200);

            writer.WriteLine("<HR/>");
            writer.WriteLine("<H4><A Name=\"Events_Gen2_{0}\">Gen 2 for Process {1,5}: {2}<A></H4>", ProcessID, ProcessID, ProcessName);
            PrintEventTable(writer, 0, delegate (GCEvent _event) { return _event.GCGeneration > 1; });

            writer.WriteLine("<HR/>");
            writer.WriteLine("<H4><A Name=\"Events_{0}\">All GC Events for Process {1,5}: {2}<A></H4>", ProcessID, ProcessID, ProcessName);
            PrintEventTable(writer, Math.Max(0, events.Count - 1000));
            PrintEventCondemnedReasonsTable(writer, Math.Max(0, events.Count - 1000));

#if (!CAP)
            if (PerfView.AppLog.InternalUser)
            {
                RenderServerGcConcurrencyGraphs(writer);
            }
#endif
            if (FinalizedObjects.Count > 0)
            {
                const int MaxResultsToShow = 20;
                int resultsToShow = Math.Min(FinalizedObjects.Count, MaxResultsToShow);
                writer.WriteLine("<HR/>");
                writer.WriteLine("<H4><A Name=\"Finalization_{0}\">Finalized Object Counts for Process {1,5}: {2}<A></H4>", ProcessID, ProcessID, ProcessName);
                writer.WriteLine("<Center><Table Border=\"1\">");
                writer.WriteLine("<TR><TH>Type</TH><TH>Count</TH></TR>");
                foreach (var finalized in FinalizedObjects.OrderByDescending(f => f.Value).Take(resultsToShow))
                {
                    writer.WriteLine("<TR><TD Align=\"Center\">{0}</TD><TD Align=\"Center\">{1}</TD><TR>", finalized.Key, finalized.Value);
                }
                writer.WriteLine("</Table></Center>");
                if (resultsToShow < FinalizedObjects.Count)
                {
                    writer.WriteLine("<P><I>Only showing {0} of {1} rows.</I></P>", resultsToShow, FinalizedObjects.Count);
                }
                writer.WriteLine("<P><A HREF=\"command:excelFinalization/{0}\">View the full list</A> in Excel.<P>", ProcessID);
            }

            writer.WriteLine("<HR/><HR/><BR/><BR/>");
        }

        private bool RenderServerGcConcurrencyGraphs(TextWriter writer)
        {
            if (heapCount <= 1 || isServerGCUsed != 1)
                return false;

            int gcGraphsToRender = 10;

            var serverGCs = events
                            .Where(gc => gc.Type != GCType.BackgroundGC && gc.HasServerGcThreadingInfo)
                            .OrderByDescending(gc => gc.GCDurationMSec + gc._SuspendDurationMSec)
                            .ToArray();

            if (serverGCs.Length == 0)
                return false;

            if (doServerGCReport)
            {
                string name = "SGCStats-" + ProcessName + "-" + ProcessID + ".txt";
                serverGCActivityStatsFile = new StreamWriter(name, false);
            }

            writer.WriteLine("<h3>Longest Server GCs. (CPU time by heap)</h3>");
            RenderServerGcLegend(writer);
            foreach (var gc in serverGCs)
            {
                if (gcGraphsToRender == 0)
                {
                    break;
                }
                if (gc.ServerGcConcurrencyGraphs(writer))
                {
                    gcGraphsToRender--;
                }
            }

            if (serverGCActivityStatsFile != null)
                serverGCActivityStatsFile.Close();
            return true;
        }

        private void RenderServerGcLegend(TextWriter writer)
        {
            writer.WriteLine("<svg width='500' height='200' >");
            writer.WriteLine("<rect x='10' y='10' width='5' height='30' style='fill:rgb(0,200,0);' />");
            writer.WriteLine("<text x='20' y='40'> GC thread working.</text>");
            writer.WriteLine("</rect>");

            writer.WriteLine("<rect x='10' y='50' width='5' height='30' style='fill:rgb(250,20,20);' />");
            writer.WriteLine("<text x='20' y='80'>Another thread working, potentially taking CPU time from GC thread.</text>");
            writer.WriteLine("</rect>");

            writer.WriteLine("<rect x='10' y='90' width='5' height='30' style='fill:rgb(0,0,220);' />");
            writer.WriteLine("<text x='20' y='120'>Idle.</text>");
            writer.WriteLine("</rect>");

            writer.WriteLine("<rect x='10' y='130' width='5' height='30' style='fill:rgb(0,100,220);' />");
            writer.WriteLine("<text x='20' y='160'>Low priority thread is working. (Most likely not taking CPU time from GC thread)</text>");
            writer.WriteLine("</rect>");

            writer.WriteLine("<polygon points='10,170 10,190 13,180'  style='fill:rgb(255,215,0);' />");
            writer.WriteLine("<text x='20' y='185'>GC Join reset event.</text>");

            writer.WriteLine("<rect x='10' y='200' width='15' height='4' style='fill:rgb(255,215,0);' />");
            writer.WriteLine("<text x='30' y='205'>GC Join - start to end.</text>");

            writer.WriteLine("</svg>");
        }


        private void PrintEventTable(TextWriter writer, int start = 0, Predicate<GCEvent> filter = null)
        {
            writer.WriteLine("<Center>");
            writer.WriteLine("<Table Border=\"1\">");
            writer.WriteLine("<TR><TH colspan=\"" + (28 + (ShowPinnedInformation ? 4 : 0)) + "\" Align=\"Center\">GC Events by Time</TH></TR>");
            writer.WriteLine("<TR><TH colspan=\"" + (28 + (ShowPinnedInformation ? 4 : 0)) + "\" Align=\"Center\">All times are in msec.  Hover over columns for help.</TH></TR>");
            writer.WriteLine("<TR>" +
                 "<TH>GC<BR/>Index</TH>" +
                 "<TH>Pause Start</TH>" +
                 "<TH Title=\"How this GC was triggered\">Trigger<BR/>Reason</TH>" +
                 "<TH Title=\"N=NonConcurrent, B=Background, F=Foreground (while background is running) I=Induced i=InducedNotForced\">Gen</TH>" +
                 "<TH Title=\"The time in milliseconds that it took to suspend all threads to start this GC.  For background GCs, we pause multiple times, so this value may be higher than for foreground GCs.\">Suspend<BR/>Msec</TH>" +
                 "<TH Title=\"The amount of time that execution in managed code is blocked because the GC needs exclusive use to the heap.  For background GCs this is small.\">Pause<BR/>MSec</TH>" +
                 "<TH Title=\"Since the last GC, GC pause time expressed as a percentage of total process time.  For background GC, this includes the pause time for foreground GCs that occur during the background GC.\">%<BR/>Pause<BR/>Time</TH>" +
                 "<TH Title=\"Since the last GC, the GC CPU time divided by the total Process CPU time expressed as a percentage.\">% GC</TH>" +
                 "<TH Title=\"Amount allocated since the last GC occured\">Gen0<BR/>Alloc<BR/>MB</TH>" +
                 "<TH Title=\"The average allocation rate since the last GC.\">Gen0<BR/>Alloc<BR/>Rate<BR/>MB/sec</TH>" +
                 "<TH Title=\"The peak size of the GC during GC. (includes fragmentation)\">Peak<BR/>MB</TH>" +
                 "<TH Title=\"The size after GC (includes fragmentation)\">After<BR/>MB</TH>" +
                 "<TH>Ratio<BR/>Peak/After</TH>" +
                 "<TH Title=\"Memory this GC promoted\">Promoted<BR/>MB</TH>" +
                 "<TH Title=\"Size of gen0 at the end of this GC.\">Gen0<BR/>MB</TH>" +
                 "<TH Title=\"The % of objects in Gen0 that survived this GC.\">Gen0<BR/>Survival<BR/>Rate %</TH>" +
                 "<TH Title=\"The % of free space on gen0.\">Gen0<BR/>Frag<BR/>%</TH>" +
                 "<TH Title=\"Size of gen1 at the end of this GC.\">Gen1<BR/>MB</TH>" +
                 "<TH Title=\"The % of objects in Gen1 that survived this GC. Only available if we are doing a gen1 GC.\">Gen1<BR/>Survival<BR/>Rate %</TH>" +
                 "<TH Title=\"The % of free space on Gen1 that is betweeen live objects\">Gen1<BR/>Frag<BR/>%</TH>" +
                 "<TH Title=\"Size of Gen2 in MB at the end of this GC.\">Gen2<BR/>MB</TH>" +
                 "<TH Title=\"The % of objects in Gen2 that survived this GC. Only available if we are doing a gen2 GC.\">Gen2<BR/>Survival<BR/>Rate %</TH>" +
                 "<TH Title=\"The % of free space on gen2.\">Gen2<BR/>Frag<BR/>%</TH>" +
                 "<TH Title=\"Size of Large object heap (LOH) in MB at the end of this GC.\">LOH<BR/>MB</TH>" +
                 "<TH Title=\"The % of objects in the large object heap (LOH) that survived the GC. Only available if we are doing a gen2 GC.\">LOH<BR/>Survival<BR/>Rate %</TH>" +
                 "<TH Title=\"The % of free space that is between live objects on the large object heap (LOH).\">LOH<BR/>Frag<BR/>%</TH>" +
                 "<TH Title=\"The number of MB of objects that have finalizers (destructors) that survived this GC. \">Finalizable<BR/>Surv MB</TH>" +
                 "<TH Title=\"Number of pinned objects this GC promoted.\">Pinned<BR/>Obj</TH>" +

                 (ShowPinnedInformation ?
                 "<TH Title=\"Size of pinned objects this GC promoted.\">Pinned<BR/>Obj<BR/>Size</TH>" +
                 "<TH Title=\"Percentage of pinned plugs occupied by pinned objects.\">Pinned<BR/>Obj<BR/>%</TH>" +
                 "<TH Title=\"Size of pinned plugs\">Pinned<BR/>Size</TH>" +
                 "<TH Title=\"Size of pinned plugs by GC\">GC<BR/>Pinned<BR/>Size</TH>"
                 : string.Empty) +

                 "</TR>");

            if (start != 0)
                writer.WriteLine("<TR><TD colspan=\"26\" Align=\"Center\"> {0} Beginning entries truncated, use <A HREF=\"command:excel/{1}\">View in Excel</A> to view all...</TD></TR>", start, ProcessID);
            for (int i = start; i < events.Count; i++)
            {
                var _event = events[i];
                if (filter == null || filter(_event))
                {
                    if (!_event.isComplete)
                    {
                        continue;
                    }

                    var allocGen0MB = _event.GetUserAllocated(Gens.Gen0);

                    writer.WriteLine("<TR>" +
                                    "<TD Align=\"right\">{0}</TD>" +      // GC index
                                    "<TD Align=\"right\">{1:n3}</TD>" +   // Pause start
                                    "<TD Align=\"right\">{2}</TD>" +      // Reason
                                    "<TD Align=\"right\">{3}</TD>" +      // Gen
                                    "<TD Align=\"right\">{4:n3}</TD>" +   // Suspension time
                                    "<TD Align=\"right\">{5:n3}</TD>" +   // Pause duration 
                                    "<TD Align=\"right\">{6:n1}</TD>" +   // % pause time since last GC
                                    "<TD Align=\"right\">{7:n1}</TD>" +   // % time in GC
                                    "<TD Align=\"right\">{8:n3}</TD>" +   // Amount Allocated in gen0 
                                    "<TD Align=\"right\">{9:n2}</TD>" +   // Gen0 AllocRate
                                    "<TD Align=\"right\">{10:n3}</TD>" +   // Size at the beginning of this GC
                                    "<TD Align=\"right\">{11:n3}</TD>" +   // Size at the end of this GC
                                    "<TD Align=\"right\">{12:n2}</TD>" +  // Ratio of end/beginning
                                    "<TD Align=\"right\">{13:n3}</TD>" +  // Memory this GC promoted
                                    "<TD Align=\"right\">{14:n3}</TD>" +  // Gen0 size at the end of this GC
                                    "<TD Align=\"right\">{15:n0}</TD>" +  // Gen0 survival rate
                                    "<TD Align=\"right\">{16:n2}</TD>" +  // Gen0 frag ratio
                                    "<TD Align=\"right\">{17:n3}</TD>" +  // Gen1 size at the end of this GC
                                    "<TD Align=\"right\">{18:n0}</TD>" +  // Gen1 survival rate
                                    "<TD Align=\"right\">{19:n2}</TD>" +  // Gen1 frag ratio
                                    "<TD Align=\"right\">{20:n3}</TD>" +  // Gen2 size at the end of this GC
                                    "<TD Align=\"right\">{21:n0}</TD>" +  // Gen2 survivl rate
                                    "<TD Align=\"right\">{22:n2}</TD>" +  // Gen2 frag ratio
                                    "<TD Align=\"right\">{23:n3}</TD>" +  // LOH size at the end of this GC
                                    "<TD Align=\"right\">{24:n0}</TD>" +  // LOH survival rate
                                    "<TD Align=\"right\">{25:n2}</TD>" +  // LOH frag ratio
                                    "<TD Align=\"right\">{26:n2}</TD>" +  // Finalize promoted for this GC
                                    "<TD Align=\"right\">{27:n0}</TD>" +  // # of pinned object this GC saw

                                    (ShowPinnedInformation ?
                                    "<TD Align=\"right\">{28:n0}</TD>" +  // size of pinned object this GC saw
                                    "<TD Align=\"right\">{29:n0}</TD>" + // percent of pinned object this GC saw
                                    "<TD Align=\"right\">{30:n0}</TD>" + // size of pinned plugs 
                                    "<TD Align=\"right\">{31:n0}</TD>"  // size of pinned plugs by GC
                                    : string.Empty) +

                                    "</TR>",
                       _event.GCNumber,
                       _event.PauseStartRelativeMSec,
                       _event.Reason,
                       _event.GCGenerationName,
                       _event._SuspendDurationMSec,
                       _event.PauseDurationMSec,
                       _event.GetPauseTimePercentageSinceLastGC(),
                       _event.PercentTimeInGC,
                       allocGen0MB,
                       (allocGen0MB * 1000.0) / _event.DurationSinceLastRestartMSec,
                       _event.HeapSizePeakMB,
                       _event.HeapSizeAfterMB,
                       _event.HeapSizePeakMB / _event.HeapSizeAfterMB,
                       _event.PromotedMB,
                       _event.GenSizeAfterMB(Gens.Gen0),
                       _event.SurvivalPercent(Gens.Gen0),
                       _event.GenFragmentationPercent(Gens.Gen0),
                       _event.GenSizeAfterMB(Gens.Gen1),
                       _event.SurvivalPercent(Gens.Gen1),
                       _event.GenFragmentationPercent(Gens.Gen1),
                       _event.GenSizeAfterMB(Gens.Gen2),
                       _event.SurvivalPercent(Gens.Gen2),
                       _event.GenFragmentationPercent(Gens.Gen2),
                       _event.GenSizeAfterMB(Gens.GenLargeObj),
                       _event.SurvivalPercent(Gens.GenLargeObj),
                       _event.GenFragmentationPercent(Gens.GenLargeObj),
                       _event.HeapStats.FinalizationPromotedSize / 1000000.0,
                       _event.HeapStats.PinnedObjectCount,
                       ((_event.GetPinnedObjectSizes() != 0) ? _event.GetPinnedObjectSizes() : double.NaN),
                       ((_event.GetPinnedObjectPercentage() != -1) ? _event.GetPinnedObjectPercentage() : double.NaN),
                       ((_event.GetPinnedObjectPercentage() != -1) ? _event.totalPinnedPlugSize : double.NaN),
                       ((_event.GetPinnedObjectPercentage() != -1) ? (_event.totalPinnedPlugSize - _event.totalUserPinnedPlugSize) : double.NaN)
                       );
                }
            }

            writer.WriteLine("</Table>");
            writer.WriteLine("</Center>");
        }

        private void PrintEventCondemnedReasonsTable(TextWriter writer, int start = 0, Predicate<GCEvent> filter = null)
        {
            // Validate that we actually have condemned reasons information.
            int missingPerHeapHistories = 0;
            bool perHeapHistoryPresent = false;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].isComplete)
                {
                    if (events[i].PerHeapHistories == null)
                    {
                        missingPerHeapHistories++;

                        // Allow up to 5 complete events without per-heap histories
                        // before we assume that we don't have any per-heap history information.
                        if (missingPerHeapHistories >= 5)
                        {
                            return;
                        }

                        continue;
                    }

                    perHeapHistoryPresent = true;
                    break;
                }
            }

            // Ensure that we have per-heap history data before continuing.
            if (!perHeapHistoryPresent)
            {
                return;
            }

            writer.WriteLine("<HR/>");
            writer.WriteLine("<H4>Condemned reasons for GCs</H4>");
            writer.WriteLine("<P>This table gives a more detailed account of exactly why a GC decided to collect that generation.  ");
            writer.WriteLine("Hover over the column headings for more info.</P>");
            if (start != 0)
                writer.WriteLine("<TR><TD colspan=\"26\" Align=\"Center\"> {0} Beginning entries truncated</TD></TR>", start);

            writer.WriteLine("<Center>");
            writer.WriteLine("<Table Border=\"1\">");
            writer.WriteLine("<TR><TH>GC Index</TH>");
            if (isServerGCUsed == 1)
            {
                writer.WriteLine("<TH>Heap<BR/>Index</TH>");
            }

            for (int i = 0; i < GCEvent.CondemnedReasonsHtmlHeader.Length; i++)
            {
                writer.WriteLine("<TH Title=\"{0}\">{1}</TH>",
                                 GCEvent.CondemnedReasonsHtmlHeader[i][1],
                                 GCEvent.CondemnedReasonsHtmlHeader[i][0]);
            }
            writer.WriteLine("</TR>");

            for (int i = start; i < events.Count; i++)
            {
                var _event = events[i];
                if (filter == null || filter(_event))
                {
                    if (!_event.isComplete)
                    {
                        continue;
                    }

                    writer.WriteLine("<TR><TD Align=\"center\">{0}</TD>{1}",
                                     _event.GCNumber,
                                     _event.PrintCondemnedReasonsToHtml());
                }
            }

            writer.WriteLine("</Table>");
            writer.WriteLine("</Center>");
        }

        private void PrintLOHAllocLargePauseTable(TextWriter writer, int minPauseMSec)
        {
            // Find the first event that has the LOH alloc pause info.
            int index;
            for (index = 0; index < events.Count; index++)
            {
                if ((events[index].LOHWaitThreads != null) && (events[index].LOHWaitThreads.Count != 0))
                {
                    break;
                }
            }

            if (index == events.Count)
            {
                return;
            }

            writer.WriteLine("<Center>");
            writer.WriteLine("<Table Border=\"1\">");
            writer.WriteLine("<TR>" +
                 "<TH>BGC<BR/>Index</TH>" +
                 "<TH>Thread<BR/>ID</TH>" +
                 "<TH>Pause<BR/>Start</TH>" +
                 "<TH>Pause MSec</TH>" +
                 "<TH Title=\"LOH allocation has to wait when BGC is threading its free list; or if the app has already allocated enough on LOH\">Pause<BR/>Reason</TH>" +
                 "</TR>");

            while (index < events.Count)
            {
                GCEvent _event = events[index];
                if (_event.LOHWaitThreads != null)
                {
                    int longPauseCount = 0;

                    Dictionary<int, BGCAllocWaitInfo>.ValueCollection infoCollection = _event.LOHWaitThreads.Values;

                    foreach (BGCAllocWaitInfo info in infoCollection)
                    {
                        // First pass to know how many rows we'll need to print.
                        if (info.IsLOHWaitLong(minPauseMSec))
                        {
                            longPauseCount++;
                        }
                    }

                    if (longPauseCount > 0)
                    {
                        writer.WriteLine("<TR><TD Align=\"right\" rowspan=\"{0}\">{1}</TD>", longPauseCount, _event.GCNumber);

                        bool isFirstRow = true;

                        foreach (KeyValuePair<int, BGCAllocWaitInfo> kvp in _event.LOHWaitThreads)
                        {
                            BGCAllocWaitInfo info = kvp.Value;
                            // Second pass to actually print.
                            if (info.IsLOHWaitLong(minPauseMSec))
                            {
                                if (isFirstRow)
                                {
                                    isFirstRow = false;
                                }
                                else
                                {
                                    writer.WriteLine("<TR>");
                                }

                                writer.WriteLine("<TD>{0}</TD><TD>{1:n3}</TD><TD>{2:n3}</TD><TD>{3}</TD>",
                                                    kvp.Key,
                                                    kvp.Value.WaitStartRelativeMSec,
                                                    (kvp.Value.WaitStopRelativeMSec - kvp.Value.WaitStartRelativeMSec),
                                                    kvp.Value.ToString());
                            }
                        }
                    }
                }

                index++;
            }

            writer.WriteLine("</Table>");
            writer.WriteLine("</Center>");
        }

        public virtual void ToXml(TextWriter writer, string indent)
        {
            writer.Write("{0}<GCProcess", indent);
            writer.Write(" Process="); GCEvent.QuotePadLeft(writer, ProcessName, 10);
            writer.Write(" ProcessID="); GCEvent.QuotePadLeft(writer, ProcessID.ToString(), 5);
            if (ProcessCpuMSec != 0)
            {
                writer.Write(" ProcessCpuTimeMsec="); GCEvent.QuotePadLeft(writer, ProcessCpuMSec.ToString("f0"), 5);
            }
            Total.ToXmlAttribs(writer);
            if (RuntimeVersion != null)
            {
                writer.Write(" RuntimeVersion="); GCEvent.QuotePadLeft(writer, RuntimeVersion, 8);
                writer.Write(" StartupFlags="); GCEvent.QuotePadLeft(writer, StartupFlags.ToString(), 10);
                writer.Write(" CommandLine="); writer.Write(XmlUtilities.XmlQuote(CommandLine));
            }
            if (PeakVirtualMB != 0)
            {
                writer.Write(" PeakVirtualMB="); GCEvent.QuotePadLeft(writer, PeakVirtualMB.ToString(), 8);
            }
            if (PeakWorkingSetMB != 0)
            {
                writer.Write(" PeakWorkingSetMB="); GCEvent.QuotePadLeft(writer, PeakWorkingSetMB.ToString(), 8);
            }
            writer.WriteLine(">");
            writer.WriteLine("{0}  <Generations Count=\"{1}\" TotalGCCount=\"{2}\" TotalAllocatedMB=\"{3:n3}\" TotalGCCpuMSec=\"{4:n3}\" MSecPerMBAllocated=\"{5:f3}\">",
                indent, Generations.Length, Total.GCCount, Total.TotalAllocatedMB, Total.TotalGCCpuMSec, Total.TotalGCCpuMSec / Total.TotalAllocatedMB);
            for (int gen = 0; gen < Generations.Length; gen++)
            {
                writer.Write("{0}   <Generation Gen=\"{1}\"", indent, gen);
                Generations[gen].ToXmlAttribs(writer);
                writer.WriteLine("/>");
            }
            writer.WriteLine("{0}  </Generations>", indent);

            writer.WriteLine("{0}  <GCEvents Count=\"{1}\">", indent, events.Count);
            foreach (GCEvent _event in events)
                _event.ToXml(writer);

            writer.WriteLine("{0}  </GCEvents>", indent);
            writer.WriteLine("{0} </GCProcess>", indent);
        }

        public void ToCsv(string filePath)
        {
            string listSeparator = Thread.CurrentThread.CurrentCulture.TextInfo.ListSeparator;
            using (var writer = File.CreateText(filePath))
            {
                //                  0   1           2       3    4             5        6         7            8       9        10                11          12          13        14            15          16       17         18           19        20            21      22                23        24             25                                      27             28
                writer.WriteLine("Num{0}PauseStart{0}Reason{0}Gen{0}PauseMSec{0}Time In GC{0}AllocMB{0}Alloc MB/sec{0}PeakMB{0}AfterMB{0}Peak/After Ratio{0}PromotedMB{0}Gen0 Size{0}Gen0 Surv%{0}Gen0 Frag%{0}Gen1 Size{0}Gen1 Surv%{0}Gen1 Frag%{0}Gen2 Size{0}Gen2 Surv%{0}Gen2 Frag%{0}LOH Size{0}LOH Surv%{0}LOH Frag%{0}FinalizeSurviveMB{0}Pinned Object{0}% Pause Time{0}Suspend Msec", listSeparator);
                for (int i = 0; i < events.Count; i++)
                {
                    var _event = events[i];
                    if (!(_event.isComplete))
                        continue;

                    var allocGen0MB = _event.GenSizeBeforeMB(Gens.Gen0);
                    writer.WriteLine("{0}{26}{1:f3}{26}{2}{26}{3}{26}{4:f3}{26}{5:f1}{26}{6:f3}{26}{7:f2}{26}{8:f3}{26}{9:f3}{26}{10:2}{26}{11:f3}{26}{12:f3}{26}{13:f0}{26}{14:f2}{26}{15:f3}{26}{16:f0}{26}{17:f2}{26}{18:f3}{26}{19:f0}{26}{20:f2}{26}{21:f3}{26}{22:f0}{26}{23:f2}{26}{24:f2}{26}{25:f0}{26}{27:f1}{26}{28:f3}",
                                   _event.GCNumber,
                                   _event.PauseStartRelativeMSec,
                                   _event.Reason,
                                   _event.GCGenerationName,
                                   _event.PauseDurationMSec,
                                   _event.PercentTimeInGC,
                                   allocGen0MB,
                                   (allocGen0MB * 1000.0) / _event.DurationSinceLastRestartMSec,
                                   _event.HeapSizePeakMB,
                                   _event.HeapSizeAfterMB,
                                   _event.HeapSizePeakMB / _event.HeapSizeAfterMB,
                                   _event.PromotedMB,
                                   _event.GenSizeAfterMB(Gens.Gen0),
                                   _event.SurvivalPercent(Gens.Gen0),
                                   _event.GenFragmentationPercent(Gens.Gen0),
                                   _event.GenSizeAfterMB(Gens.Gen1),
                                   _event.SurvivalPercent(Gens.Gen1),
                                   _event.GenFragmentationPercent(Gens.Gen1),
                                   _event.GenSizeAfterMB(Gens.Gen2),
                                   _event.SurvivalPercent(Gens.Gen2),
                                   _event.GenFragmentationPercent(Gens.Gen2),
                                   _event.GenSizeAfterMB(Gens.GenLargeObj),
                                   _event.SurvivalPercent(Gens.GenLargeObj),
                                   _event.GenFragmentationPercent(Gens.GenLargeObj),
                                   _event.HeapStats.FinalizationPromotedSize / 1000000.0,
                                   _event.HeapStats.PinnedObjectCount,
                                   listSeparator,
                                   _event.GetPauseTimePercentageSinceLastGC(),
                                   _event._SuspendDurationMSec);
                }
            }
        }

        public void ToCsvFinalization(string filePath)
        {
            string listSeparator = Thread.CurrentThread.CurrentCulture.TextInfo.ListSeparator;
            using (var writer = File.CreateText(filePath))
            {
                writer.WriteLine("Type{0}Count", listSeparator);
                foreach (var finalized in FinalizedObjects.OrderByDescending(f => f.Value))
                {
                    writer.WriteLine("{0}{1}{2}", finalized.Key.Replace(listSeparator, ""), listSeparator, finalized.Value);
                }
            }
        }

        public void PerGenerationCsv(string filePath)
        {
            // Sadly, streamWriter does not have a way of setting the IFormatProvider property
            // So we have to do it in this ugly, global variable way.  
            string listSeparator = Thread.CurrentThread.CurrentCulture.TextInfo.ListSeparator;
            using (var writer = File.CreateText(filePath))
            {
                writer.WriteLine("PauseStart{0}Num{0}Gen{0}GCStop{0}TickMB{0}" +
                    "Before0{0}Before1{0}Before2{0}Before3{0}" +
                    "After0{0}After1{0}After2{0}After3{0}" +

                    "Surv0{0}Surv1{0}Surv2{0}Surv3{0}" +
                    "In0{0}In1{0}In2{0}In3{0}" +
                    "Out0{0}Out1{0}Out2{0}Out3{0}" +
                    "Frag0{0}Frag1{0}Frag2{0}Frag3" +
                    (m_detailedGCInfo ? "{0}Budget0{0}Budget1{0}Budget2{0}Budget3" : ""), listSeparator);

                foreach (GCEvent _event in events)
                {
                    if (!_event.isComplete)
                        continue;

                    writer.WriteLine("{0:f3}{33}{1}{33}{2}{33}{3:f3}{33}{4:f3}{33}" +
                        "{5:f3}{33}{6:f3}{33}{7:f3}{33}{8:f3}{33}" +
                        "{9:f3}{33}{10:f3}{33}{11:f3}{33}{12:f3}{33}" +
                        "{13:f3}{33}{14:f3}{33}{15:f3}{33}{16:f3}{33}" +
                        "{17:f3}{33}{18:f3}{33}{19:f3}{33}{20:f3}{33}" +

                        "{21:f3}{33}{22:f3}{33}{23:f3}{33}{24:f3}{33}" +
                        "{25:f3}{33}{26:f3}{33}{27:f3}{33}{28:f3}" +
                        (m_detailedGCInfo ? "{33}{29:f3}{33}{30:f3}{33}{31:f3}{33}{32:f3}" : ""),
                        _event.PauseStartRelativeMSec,
                        _event.GCNumber,
                        _event.GCGenerationName,
                        _event.GCDurationMSec + _event.GCStartRelativeMSec,
                        (_event.AllocedSinceLastGCBasedOnAllocTickMB[0] + _event.AllocedSinceLastGCBasedOnAllocTickMB[1]),

                        _event.GenSizeBeforeMB(Gens.Gen0), _event.GenSizeBeforeMB(Gens.Gen1),
                        _event.GenSizeBeforeMB(Gens.Gen2), _event.GenSizeBeforeMB(Gens.GenLargeObj),

                        _event.GenSizeAfterMB(Gens.Gen0), _event.GenSizeAfterMB(Gens.Gen1),
                        _event.GenSizeAfterMB(Gens.Gen2), _event.GenSizeAfterMB(Gens.GenLargeObj),

                        _event.GenPromotedMB(Gens.Gen0), _event.GenPromotedMB(Gens.Gen1),
                        _event.GenPromotedMB(Gens.Gen2), _event.GenPromotedMB(Gens.GenLargeObj),

                        _event.GenInMB(Gens.Gen0), _event.GenInMB(Gens.Gen1),
                        _event.GenInMB(Gens.Gen2), _event.GenInMB(Gens.GenLargeObj),

                        _event.GenOutMB(Gens.Gen0), _event.GenOutMB(Gens.Gen1),
                        _event.GenOutMB(Gens.Gen2), _event.GenOutMB(Gens.GenLargeObj),

                        _event.GenFragmentationMB(Gens.Gen0), _event.GenFragmentationMB(Gens.Gen1),
                        _event.GenFragmentationMB(Gens.Gen2), _event.GenFragmentationMB(Gens.GenLargeObj),

                        _event.GenBudgetMB(Gens.Gen0), _event.GenBudgetMB(Gens.Gen1),
                        _event.GenBudgetMB(Gens.Gen2), _event.GenBudgetMB(Gens.GenLargeObj),
                        listSeparator
                        );
                }
            }
        }

        public double GetGCPauseTimePercentage()
        {
            return ((ProcessDuration == 0) ? 0.0 : ((Total.TotalPauseTimeMSec * 100) / ProcessDuration));
        }

        #region private
        public virtual void Init(TraceEvent data)
        {
            ProcessID = data.ProcessID;
            ProcessName = data.ProcessName;
            isDead = false;
        }
        private GCEvent GetCurrentGC()
        {
            if (events.Count > 0)
            {
                if (!events[events.Count - 1].isComplete)
                {
                    return events[events.Count - 1];
                }
                else if (currentBGC != null)
                {
                    return currentBGC;
                }
            }

            return null;
        }

        // This is the last GC in progress. We need this for server Background GC.
        // See comments for lastCompletedGC.
        private GCEvent GetLastGC()
        {
            GCEvent _event = GetCurrentGC();
            if ((isServerGCUsed == 1) &&
                (_event == null))
            {
                if (lastCompletedGC != null)
                {
                    Debug.Assert(lastCompletedGC.Type == GCType.BackgroundGC);
                    _event = lastCompletedGC;
                }
            }

            return _event;
        }

        private GCEvent GetLastBGC()
        {
            if (currentBGC != null)
            {
                return currentBGC;
            }

            if ((lastCompletedGC != null) && (lastCompletedGC.Type == GCType.BackgroundGC))
            {
                return lastCompletedGC;
            }

            // Otherwise we search till we find the last BGC if we have seen one.
            for (int i = (events.Count - 1); i >= 0; i--)
            {
                if (events[i].Type == GCType.BackgroundGC)
                {
                    return events[i];
                }
            }

            return null;
        }

        private void AddConcurrentPauseTime(GCEvent _event, double RestartEEMSec)
        {
            if (suspendThreadIDBGC > 0)
            {
                _event.PauseDurationMSec += RestartEEMSec - suspendTimeRelativeMSec;
            }
            else
            {
                Debug.Assert(_event.PauseDurationMSec == 0);
                _event.PauseDurationMSec = RestartEEMSec - _event.PauseStartRelativeMSec;
            }
        }

        private bool ShowPinnedInformation
        {
            get
            {
                if ((PerfView.AppLog.InternalUser) && (this.Total.NumGCWithPinEvents > 0))
                {
                    return true;
                }

                return false;
            }
        }

        private void AddServerGCThreadFromMark(int ThreadID, int HeapNum)
        {
            if (isServerGCUsed == 1)
            {
                Debug.Assert(heapCount > 1);

                if (serverGCThreads.Count < heapCount)
                {
                    // I am seeing that sometimes we are not getting these events from all heaps
                    // for a complete GC so I have to check for that.
                    if (!serverGCThreads.ContainsKey(ThreadID))
                    {
                        serverGCThreads.Add(ThreadID, HeapNum);
                    }
                }
            }
        }

        private void ProcessGlobalHistory(GCGlobalHeapHistoryTraceData data)
        {
            if (isServerGCUsed == -1)
            {
                // We detected whether we are using Server GC now.
                isServerGCUsed = ((data.NumHeaps > 1) ? 1 : 0);
                if (heapCount == -1)
                {
                    heapCount = data.NumHeaps;
                }

                if (isServerGCUsed == 1)
                {
                    serverGCThreads = new Dictionary<int, int>(data.NumHeaps);
                }
            }

            GCEvent _event = GetLastGC();
            if (_event != null)
            {
                _event.GlobalHeapHistory = (GCGlobalHeapHistoryTraceData)data.Clone();
                _event.SetHeapCount(heapCount);
            }
        }

        private void ProcessPerHeapHistory(GCPerHeapHistoryTraceData data)
        {
            if (!data.VersionRecognized)
            {
                GCVersionInfoMismatch = true;
                return;
            }

            GCEvent _event = GetLastGC();
            if (_event != null)
            {
                if (_event.PerHeapHistories == null)
                    _event.PerHeapHistories = new List<GCPerHeapHistoryTraceData>();
                _event.PerHeapHistories.Add((GCPerHeapHistoryTraceData)data.Clone());
            }
        }

        public override string ToString()
        {
            StringWriter sw = new StringWriter();
            ToXml(sw, "");
            return sw.ToString();
        }

        internal List<GCEvent> Events
        {
            get
            {
                return events;
            }
        }

        public List<GCEvent> events = new List<GCEvent>();

        // The amount of memory allocated by the user threads. So they are divided up into gen0 and LOH allocations.
        double[] allocTickCurrentMB = { 0.0, 0.0 };
        double[] allocTickAtLastGC = { 0.0, 0.0 };
        bool HasAllocTickEvents = false;
        bool SeenBadAllocTick = false;

        double lastRestartEndTimeRelativeMSec;

        // EE can be suspended via different reasons. The only ones we care about are
        // SuspendForGC(1) - suspending for GC start
        // SuspendForGCPrep(6) - BGC uses it in the middle of a BGC.
        // We need to filter out the rest of the suspend/resume events.
        // Keep track of the last time we started suspending the EE.  Will use in 'Start' to set PauseStartRelativeMSec
        int suspendThreadIDOther = -1;
        int suspendThreadIDBGC = -1;
        // This is either the user thread (in workstation case) or a server GC thread that called SuspendEE to do a GC
        int suspendThreadIDGC = -1;
        double suspendTimeRelativeMSec = -1;
        double suspendEndTimeRelativeMSec = -1;

        // This records the amount of CPU time spent at the end of last GC.
        float ProcessCpuAtLastGC = 0;

        // This is the BGC that's in progress as we are parsing. We need to remember this 
        // so we can correctly attribute the suspension time.
        GCEvent currentBGC = null;
        Dictionary<int, object> backgroundGCThreads = null;
        bool IsBGCThread(int threadID)
        {
            if (backgroundGCThreads != null)
                return backgroundGCThreads.ContainsKey(threadID);
            return false;
        }

        // I keep this for the purpose of server Background GC. Unfortunately for server background 
        // GC we are firing the GCEnd/GCHeaps events and Global/Perheap events in the reversed order.
        // This is so that the Global/Perheap events can still be attributed to the right BGC.
        GCEvent lastCompletedGC = null;

        // We don't necessarily have the GCSettings event (only fired at the beginning if we attach)
        // So we have to detect whether we are running server GC or not.
        // Till we get our first GlobalHeapHistory event which indicates whether we use server GC 
        // or not this remains -1.
        public int isServerGCUsed = -1;
        public bool gotThreadInfo = false;
        public int heapCount = -1;
        // This is the server GC threads. It's built up in the 2nd server GC we see. 
        Dictionary<int, int> serverGCThreads = null;
        public StreamWriter serverGCActivityStatsFile = null;

        internal void LogServerGCAnalysis(string format, params Object[] args)
        {
            if (serverGCActivityStatsFile != null)
            {
                serverGCActivityStatsFile.WriteLine(format, args);
            }
        }

        internal void LogServerGCAnalysis(string msg)
        {
            if (serverGCActivityStatsFile != null)
            {
                serverGCActivityStatsFile.WriteLine(msg);
            }
        }

        internal bool m_detailedGCInfo;
        int IsServerGCThread(int threadID)
        {
            int heapIndex;
            if (serverGCThreads != null)
            {
                if (serverGCThreads.TryGetValue(threadID, out heapIndex))
                {
                    return heapIndex;
                }
            }
            return -1;
        }
        #endregion

        #region IComparable<GCProcess> Members

        public int CompareTo(GCProcess other)
        {
            // Sort highest to lowest peak GC heap size.  
            return -Total.MaxSizePeakMB.CompareTo(other.Total.MaxSizePeakMB);
        }

        #endregion
    }

    // Condemned reasons are organized into the following groups.
    // Each group corresponds to one or more reasons. 
    // Groups are organized in the way that they mean something to users. 
    enum CondemnedReasonGroup
    {
        // The first 4 will have values of a number which is the generation.
        // Note that right now these 4 have the exact same value as what's in
        // Condemned_Reason_Generation.
        CRG_Initial_Generation = 0,
        CRG_Final_Generation = 1,
        CRG_Alloc_Exceeded = 2,
        CRG_Time_Tuning = 3,

        // The following are either true(1) or false(0). They are not 
        // a 1:1 mapping from 
        CRG_Induced = 4,
        CRG_Low_Ephemeral = 5,
        CRG_Expand_Heap = 6,
        CRG_Fragmented_Ephemeral = 7,
        CRG_Fragmented_Gen1_To_Gen2 = 8,
        CRG_Fragmented_Gen2 = 9,
        CRG_Fragmented_Gen2_High_Mem = 10,
        CRG_GC_Before_OOM = 11,
        CRG_Too_Small_For_BGC = 12,
        CRG_Ephemeral_Before_BGC = 13,
        CRG_Internal_Tuning = 14,
        CRG_Max = 15,
    }

    /// <summary>
    /// GCInfo are accumulated statistics per generation.  
    /// </summary>    
    struct GCInfo
    {
        public int GCCount;
        public int NumInduced;
        public long PinnedObjectSizes;
        public int PinnedObjectPercentage;
        public long NumGCWithPinEvents;
        public long NumGCWithPinPlugEvents;
        public double MaxPauseDurationMSec;
        public double MeanPauseDurationMSec { get { return TotalPauseTimeMSec / GCCount; } }
        public double MeanSizeAfterMB { get { return TotalSizeAfterMB / GCCount; } }
        public double MeanSizePeakMB { get { return TotalSizePeakMB / GCCount; } }
        public double MeanAllocatedMB { get { return TotalAllocatedMB / GCCount; } }
        public double RatioMeanPeakAfter { get { return MeanSizePeakMB / MeanSizeAfterMB; } }
        public double MeanGCCpuMSec { get { return TotalGCCpuMSec / GCCount; } }

        public double TotalPauseTimeMSec;
        public double MaxSuspendDurationMSec;
        public double MaxSizePeakMB;
        public double MaxAllocRateMBSec;

        public double TotalAllocatedMB;
        public double TotalGCCpuMSec;
        public double TotalPromotedMB;

        // these do not have a useful meaning so we hide them. 
        internal double TotalSizeAfterMB;
        internal double TotalSizePeakMB;

        public void ToXmlAttribs(TextWriter writer)
        {
            writer.Write(" GCCount="); GCEvent.QuotePadLeft(writer, GCCount.ToString(), 6);
            writer.Write(" MaxPauseDurationMSec="); GCEvent.QuotePadLeft(writer, MaxPauseDurationMSec.ToString("n3"), 10);
            writer.Write(" MeanPauseDurationMSec="); GCEvent.QuotePadLeft(writer, MeanPauseDurationMSec.ToString("n3"), 10);
            writer.Write(" MeanSizePeakMB="); GCEvent.QuotePadLeft(writer, MeanSizePeakMB.ToString("f1"), 10);
            writer.Write(" MeanSizeAfterMB="); GCEvent.QuotePadLeft(writer, MeanSizeAfterMB.ToString("f1"), 10);
            writer.Write(" TotalAllocatedMB="); GCEvent.QuotePadLeft(writer, TotalAllocatedMB.ToString("f1"), 10);
            writer.Write(" TotalGCDurationMSec="); GCEvent.QuotePadLeft(writer, TotalGCCpuMSec.ToString("n3"), 10);
            writer.Write(" MSecPerMBAllocated="); GCEvent.QuotePadLeft(writer, (TotalGCCpuMSec / TotalAllocatedMB).ToString("n3"), 10);
            writer.Write(" TotalPauseTimeMSec="); GCEvent.QuotePadLeft(writer, TotalPauseTimeMSec.ToString("n3"), 10);
            writer.Write(" MaxAllocRateMBSec="); GCEvent.QuotePadLeft(writer, MaxAllocRateMBSec.ToString("n3"), 10);
            writer.Write(" MeanGCCpuMSec="); GCEvent.QuotePadLeft(writer, MeanGCCpuMSec.ToString("n3"), 10);
            writer.Write(" MaxSuspendDurationMSec="); GCEvent.QuotePadLeft(writer, MaxSuspendDurationMSec.ToString("n3"), 10);
            writer.Write(" MaxSizePeakMB="); GCEvent.QuotePadLeft(writer, MaxSizePeakMB.ToString("n3"), 10);
        }
    }

    class BGCAllocWaitInfo
    {
        public double WaitStartRelativeMSec;
        public double WaitStopRelativeMSec;
        public BGCAllocWaitReason Reason;

        public bool GetWaitTime(ref double pauseMSec)
        {
            if ((WaitStartRelativeMSec != 0) &&
                (WaitStopRelativeMSec != 0))
            {
                pauseMSec = WaitStopRelativeMSec - WaitStartRelativeMSec;
                return true;
            }
            return false;
        }

        public bool IsLOHWaitLong(double pauseMSecMin)
        {
            double pauseMSec = 0;
            if (GetWaitTime(ref pauseMSec))
            {
                return (pauseMSec > pauseMSecMin);
            }
            return false;
        }

        public override string ToString()
        {
            if ((Reason == BGCAllocWaitReason.GetLOHSeg) ||
                (Reason == BGCAllocWaitReason.AllocDuringSweep))
            {
                return "Waiting for BGC to thread free lists";
            }
            else
            {
                Debug.Assert(Reason == BGCAllocWaitReason.AllocDuringBGC);
                return "Allocated too much during BGC, waiting for BGC to finish";
            }
        }
    }

    /// <summary>
    /// Span of thread work recorded by CSwitch or CPU Sample Profile events
    /// </summary>
    internal class ThreadWorkSpan
    {
        public int ThreadId;
        public int ProcessId;
        public string ProcessName;
        public int ProcessorNumber;
        public double AbsoluteTimestampMsc;
        public double DurationMsc;
        public int Priority = -1;
        public int WaitReason = -1;

        public ThreadWorkSpan(CSwitchTraceData switchData)
        {
            ProcessName = switchData.NewProcessName;
            ThreadId = switchData.NewThreadID;
            ProcessId = switchData.NewProcessID;
            ProcessorNumber = switchData.ProcessorNumber;
            AbsoluteTimestampMsc = switchData.TimeStampRelativeMSec;
            Priority = switchData.NewThreadPriority;
            WaitReason = (int)switchData.OldThreadWaitReason;
        }

        public ThreadWorkSpan(ThreadWorkSpan span)
        {
            ProcessName = span.ProcessName;
            ThreadId = span.ThreadId;
            ProcessId = span.ProcessId;
            ProcessorNumber = span.ProcessorNumber;
            AbsoluteTimestampMsc = span.AbsoluteTimestampMsc;
            DurationMsc = span.DurationMsc;
            Priority = span.Priority;
            WaitReason = span.WaitReason;
        }

        public ThreadWorkSpan(SampledProfileTraceData sample)
        {
            ProcessName = sample.ProcessName;
            ProcessId = sample.ProcessID;
            ThreadId = sample.ThreadID;
            ProcessorNumber = sample.ProcessorNumber;
            AbsoluteTimestampMsc = sample.TimeStampRelativeMSec;
            DurationMsc = 1;
            Priority = 0;
        }
    }

    /// <summary>
    /// GCEvent holds information on a particluar GC
    /// </summary>
    class GCEvent
    {
        public GCProcess Parent;                //process that did that GC
        public int Index;                       // Index into the list of GC events
        // The list that contains this event
        public List<GCEvent> Events
        {
            get
            {
                return Parent.Events;
            }
        }

        // Primary fields (set in the callbacks)
        public int GCNumber;                    //  Set in GCStart (starts at 1, unique for process)

        public int GCGeneration;                //  Set in GCStop(Generation 0, 1 or 2)
        public GCType Type;                     //  Set in GCStart
        public GCReason Reason;                 //  Set in GCStart

        public double DurationSinceLastRestartMSec;  //  Set in GCStart

        public double PauseStartRelativeMSec;        //  Set in GCStart

        public double PauseDurationMSec;        //  Total time EE is suspended (can be less than GC time for background)

        //  Time it takes to do the suspension. Before 4.0 we didn't have a SuspendEnd event so we calculate it by just 
        // substracting GC duration from pause duration. For concurrent GC this would be inaccurate so we just return 0.
        public double _SuspendDurationMSec;

        public double GCStartRelativeMSec;           //  Set in Start, does not include suspension.  
        public double GCDurationMSec;           //  Set in Stop This is JUST the GC time (not including suspension) That is Stop-Start.  

        // Did we get the complete event sequence for this GC?
        // For BGC this is the HeapStats event; for other GCs this means you have both the HeapStats and RestartEEEnd event.
        public bool isComplete;

        // In 2.0 we didn't have all the events. I try to keep the code not version specific and this is really
        // for debugging/verification purpose.
        public bool is20Event;
        // This only applies to 2.0. I could just set the type to Background GC but I'd rather not disturb
        // the code that handles background GC.
        public bool isConcurrentGC;

        // The amount of CPU time the process consumed since the last GC.
        public float ProcessCpuMSec;
        // The amount of CPU time this GC consumed.
        public float GCCpuMSec;
        // For background GC we need to remember when the GC before it ended because
        // when we get the GCStop event some foreground GCs may have happened.
        public float ProcessCpuAtLastGC;

        private double _TotalGCTimeMSec = -1;

        // When we are using Server GC we store the CPU spent on each thread
        // so we can see if there's an imbalance. We concurrently don't do this
        // for server background GC as the imbalance there is much less important.
        int heapCount = -1;
        public float[] GCCpuServerGCThreads = null;

        //list of workload histories per server GC heap
        private GrowableArray<ServerGcHistory> ServerGcHeapHistories;


        // Of all the CPU, how much as a percentage is spent in the GC since end of last GC.
        public float PercentTimeInGC { get { return (float)(GetTotalGCTime() * 100 / ProcessCpuMSec); } }

        public bool HasAllocTickEvents = false;
        public double[] AllocedSinceLastGCBasedOnAllocTickMB = { 0.0, 0.0 };// Set in HeapStats

        public GCEvent(GCProcess owningProcess)
        {
            Parent = owningProcess;
            heapCount = owningProcess.heapCount;

            if (heapCount > 1)
            {
                GCCpuServerGCThreads = new float[heapCount];
            }

            pinnedObjectSizes = -1;
            totalPinnedPlugSize = -1;
            totalUserPinnedPlugSize = -1;
            duplicatedPinningReports = 0;
        }

        public void SetHeapCount(int count)
        {
            if (heapCount == -1)
            {
                heapCount = count;
            }
        }

        // Unfortunately sometimes we just don't get mark events from all heaps, even for GCs that we have seen GCStart for.
        // So accommodating this scenario.
        public bool AllHeapsSeenMark()
        {
            if (PerHeapMarkTimes != null)
                return (heapCount == PerHeapMarkTimes.Count);
            else
                return false;
        }

        // Server history per heap. This is for CSwitch/CPU sample/Join events.
        // Each server GC thread goes through this flow during each GC
        // 1) runs server GC code
        // 2) joins with other GC threads
        // 3) restarts
        // 4) goes back to 1).
        // We call 1 through 3 an activity. There are as many activities as there are joins.
        public class ServerGcHistory
        {
            public int HeapId;
            public int ProcessId;
            public int GcWorkingThreadId;
            public int GcWorkingThreadPriority;
            public GrowableArray<GcWorkSpan> SwitchSpans;
            public GrowableArray<GcWorkSpan> SampleSpans;
            ServerGCThreadStateInfo[] activityStats;
            double lastGCSpanEndTime;
            double gcReadyTime; // When GC thread is ready to run.

            //list of times in msc starting from GC start when GCJoin events were fired for this heap
            private GrowableArray<GcJoin> GcJoins;
            public GCEvent Parent;
            public double TimeBaseMsc { get { return Parent.PauseStartRelativeMSec; } }

            internal enum WorkSpanType
            {
                GcThread,
                RivalThread,
                LowPriThread,
                Idle
            }

            internal class GcWorkSpan : ThreadWorkSpan
            {
                public WorkSpanType Type;
                public double RelativeTimestampMsc;

                public GcWorkSpan(ThreadWorkSpan span)
                    : base(span)
                {
                }
            }

            internal class GcJoin
            {
                public int Heap;
                public double RelativeTimestampMsc;
                public double AbsoluteTimestampMsc;
                public GcJoinType Type;
                public GcJoinTime Time;
                public int JoinID;
            }

            internal enum ServerGCThreadState
            {
                // This is when GC thread needs to run to do GC work. We care the most about
                // other threads running during this state.
                SGCState_Ready = 0,
                // GC thread doesn't need the CPU so other threads can run and don't count as
                // interference to the GC thread.
                SGCState_WaitInJoin = 1,
                // This is when GC needs to do work on a single thread. Other threads running
                // in this state is also important.
                SGCState_SingleThreaded = 2,
                // For the last joined thread, this is how long it took between restart start and end.
                // For other threads, this is when restart start is fired and when this join actually
                // ended. This usually should be really short and interference is also important.
                SGCState_WaitingInRestart = 3,
                SGCState_Max = 4,
            }

            internal class OtherThreadInfo
            {
                public string processName;
                public double runningTime;

                public OtherThreadInfo(string name, double time)
                {
                    processName = name;
                    runningTime = time;
                }
            }

            internal class ServerGCThreadStateInfo
            {
                public double gcThreadRunningTime;
                // Process ID and running time in that process.
                // The process ID could be the current process, but not the GC thread.
                public Dictionary<int, OtherThreadInfo> otherThreadsRunningTime;
            }

            public void AddSampleEvent(ThreadWorkSpan sample)
            {
                GcWorkSpan lastSpan = SampleSpans.Count > 0 ? SampleSpans[SampleSpans.Count - 1] : null;
                if (lastSpan != null && lastSpan.ThreadId == sample.ThreadId && lastSpan.ProcessId == sample.ProcessId &&
                    ((ulong)sample.AbsoluteTimestampMsc == (ulong)(lastSpan.AbsoluteTimestampMsc + lastSpan.DurationMsc)))
                {
                    lastSpan.DurationMsc++;
                }
                else
                {
                    SampleSpans.Add(new GcWorkSpan(sample)
                    {
                        Type = GetSpanType(sample),
                        RelativeTimestampMsc = sample.AbsoluteTimestampMsc - TimeBaseMsc,
                        DurationMsc = 1
                    });
                }
            }

            public void AddSwitchEvent(ThreadWorkSpan switchData)
            {
                GcWorkSpan lastSpan = SwitchSpans.Count > 0 ? SwitchSpans[SwitchSpans.Count - 1] : null;
                if (switchData.ThreadId == GcWorkingThreadId && switchData.ProcessId == ProcessId)
                {
                    //update gc thread priority since we have new data
                    GcWorkingThreadPriority = switchData.Priority;
                }

                if (lastSpan != null)
                {
                    //updating duration of the last one, based on a timestamp from the new one
                    lastSpan.DurationMsc = switchData.AbsoluteTimestampMsc - lastSpan.AbsoluteTimestampMsc;

                    //updating wait readon of the last one
                    lastSpan.WaitReason = switchData.WaitReason;
                }

                SwitchSpans.Add(new GcWorkSpan(switchData)
                {
                    Type = GetSpanType(switchData),
                    RelativeTimestampMsc = switchData.AbsoluteTimestampMsc - TimeBaseMsc,
                    Priority = switchData.Priority
                });
            }

            internal void GCEnd()
            {
                GcWorkSpan lastSpan = SwitchSpans.Count > 0 ? SwitchSpans[SwitchSpans.Count - 1] : null;
                if (lastSpan != null)
                {
                    lastSpan.DurationMsc = Parent.PauseDurationMSec - lastSpan.RelativeTimestampMsc;
                }
            }

            // A note about the join events - the restart events have no heap number associated so 
            // we add them to every heap with the ProcessorNumber so we know which heap/processor it was 
            // fired on.
            // Also for these restart events, the id field is always -1.
            internal void AddJoin(GCJoinTraceData data)
            {
                GcJoins.Add(new GcJoin
                {
                    Heap = data.ProcessorNumber,
                    AbsoluteTimestampMsc = data.TimeStampRelativeMSec,
                    RelativeTimestampMsc = data.TimeStampRelativeMSec - Parent.PauseStartRelativeMSec,
                    Type = data.JoinType,
                    Time = data.JoinTime,
                    JoinID = data.GCID,
                });
            }

            private WorkSpanType GetSpanType(ThreadWorkSpan span)
            {
                if (span.ThreadId == GcWorkingThreadId && span.ProcessId == ProcessId)
                    return WorkSpanType.GcThread;
                if (span.ProcessId == 0)
                    return WorkSpanType.Idle;

                if (span.Priority >= GcWorkingThreadPriority || span.Priority == -1)
                    return WorkSpanType.RivalThread;
                else
                    return WorkSpanType.LowPriThread;
            }

            private static Dictionary<WorkSpanType, string> Type2Color = new Dictionary<WorkSpanType, string>()
                {
                    {WorkSpanType.GcThread, "rgb(0,200,0)"},
                    {WorkSpanType.RivalThread, "rgb(250,20,20)"},
                    {WorkSpanType.Idle, "rgb(0,0,220)"},
                    {WorkSpanType.LowPriThread, "rgb(0,100,220)"},
                };

            private static string[] SGCThreadStateDesc = new string[(int)ServerGCThreadState.SGCState_Max]
            {
                "GC thread needs to run - non GC threads running on this CPU means GC runs slower",
                "GC thread is waiting to synchronize with other threads - non GC threads running does not affect GC",
                "GC thread needs to run single threaded work - non GC threads running on this CPU means GC runs slower",
                "GC thread is waiting to restart - non GC threads running on this CPU means GC runs slower",
            };

            internal void RenderGraph(TextWriter writer, int scale)
            {
                double puaseTime = Parent.PauseDurationMSec;
                writer.WriteLine(string.Format("<svg width='{0}' height='37' >", scale * puaseTime));
                //draw ruler
                writer.WriteLine(string.Format("<rect x='0' y='35' width='{0}' height='1' style='fill:black;' />", scale * puaseTime));
                for (int i = 0; i < puaseTime; i += 10)
                {
                    writer.WriteLine(string.Format("<rect x='{0}' y='32' width='1' height='4' style='fill:black;' />", scale * i));
                }

                // Server GC report isn't implemented for CSwitch yet.
                if ((SwitchSpans.Count > 0) && !GCProcess.doServerGCReport)
                {
                    RenderSwitches(writer, scale);
                }
                else
                {
                    RenderSamples(writer, scale);
                }

                //draw GC start time marker
                {
                    writer.WriteLine(string.Format("<rect x='{0}' y='0' width='2' height='37' style='fill:black;' />", scale * Parent._SuspendDurationMSec));
                }

                //draw GC joins, if any
                GcJoin lastStartJoin = null;
                foreach (var join in GcJoins)
                {

                    if (join.Type == GcJoinType.Restart)
                    {
                        if (join.Time == GcJoinTime.End)
                        {
                            int x = scale * (int)join.RelativeTimestampMsc;
                            string color = "rgb(255,215,0)";
                            writer.WriteLine(string.Format("<polygon points='{0},5 {0},25 {1},15'  style='fill:{2};' >", x, x + 3, color));
                            writer.WriteLine(string.Format("<title>GC Join Restart. Timestamp:{0:0.00} Type: {1} Called from heap #:{2} (Waking up other threads)</title>", join.AbsoluteTimestampMsc, join.Type, join.Heap));
                            writer.WriteLine("</polygon>");
                        }
                    }
                    else
                    {
                        if (join.Time == GcJoinTime.Start)
                        {
                            lastStartJoin = join;
                        }
                        else
                        {
                            if (lastStartJoin != null)
                            {
                                if (lastStartJoin.Type == join.Type)
                                {
                                    int x = scale * (int)lastStartJoin.RelativeTimestampMsc;
                                    int width = scale * (int)(join.RelativeTimestampMsc - lastStartJoin.RelativeTimestampMsc);

                                    string color = "rgb(255,215,0)";
                                    writer.WriteLine(string.Format("<rect x='{0}' y='13' width='{1}' height='4' style='fill:{2};'  >", x, Math.Max(width, 2), color));
                                    writer.WriteLine(string.Format("<title>GC Join. Timestamp:{0:0.00}ms Duration: {1:0.00}ms Type: {2} (Waiting for other threads)</title>",
                                        lastStartJoin.AbsoluteTimestampMsc, join.AbsoluteTimestampMsc - lastStartJoin.AbsoluteTimestampMsc, join.Type));
                                    writer.WriteLine("</rect>");
                                }
                                lastStartJoin = null;
                            }

                        }
                    }
                }
                writer.WriteLine("</svg>");
            }

            private void UpdateActivityThreadTime(GcWorkSpan span, ServerGCThreadStateInfo info, double threadTime, ServerGCThreadState currentThreadState)
            {
                Parent.Parent.LogServerGCAnalysis("TIME: {0, 20} - {1}: {2:n3}ms (span: {3:n3} ms -> {4:n3} ms({5:n3}))",
                    currentThreadState, span.ProcessName,
                    threadTime,
                    span.AbsoluteTimestampMsc, (span.AbsoluteTimestampMsc + span.DurationMsc), span.DurationMsc);

                if (span.Type == WorkSpanType.GcThread)
                    info.gcThreadRunningTime += threadTime;
                else
                {
                    if (info.otherThreadsRunningTime.ContainsKey(span.ProcessId))
                    {
                        OtherThreadInfo other = info.otherThreadsRunningTime[span.ProcessId];
                        if (!other.processName.Contains(span.ProcessName))
                            other.processName += ";" + span.ProcessName;
                        other.runningTime += threadTime;
                    }
                    else
                    {
                        info.otherThreadsRunningTime.Add(span.ProcessId, new OtherThreadInfo(span.ProcessName, threadTime));
                    }
                }

                if ((currentThreadState != ServerGCThreadState.SGCState_WaitInJoin) &&
                    (span.ThreadId != GcWorkingThreadId) &&
                    (threadTime > 5))
                {
                    Parent.Parent.LogServerGCAnalysis("Long interference of {0:n3} ms detected on thread {1}({2}:{3}) ({4:n3} ms -> {5:n3} ms)",
                        threadTime, span.ThreadId, span.ProcessName, span.ProcessId, span.AbsoluteTimestampMsc, (span.AbsoluteTimestampMsc + span.DurationMsc));
                }
                if ((Parent.Parent.ProcessID == 9140) &&
                    (currentThreadState != ServerGCThreadState.SGCState_WaitInJoin) &&
                    (span.ThreadId == GcWorkingThreadId) &&
                    // If the reason is not one of UserRequest, QuantumEnd or YieldExecution, we need to pay attention.
                    ((span.WaitReason != 6) || (span.WaitReason != 30) || (span.WaitReason != 33)))
                {
                    Parent.Parent.LogServerGCAnalysis("S: {0, 30} - {1:n3} ms -> {2:n3} ms({3:n3}) (WR: {4}), pri: {5}",
                        currentThreadState, span.AbsoluteTimestampMsc, (span.AbsoluteTimestampMsc + span.DurationMsc), threadTime,
                        span.WaitReason, span.Priority);
                    Parent.Parent.LogServerGCAnalysis("S: {8} - {0:n3} ms from thread {1}({2}:{3})(WR: {4}), pri: {5} ({6:n3} ms -> {7:n3} ms)",
                        threadTime, span.ThreadId, span.ProcessName, span.ProcessId, span.WaitReason, span.Priority, span.AbsoluteTimestampMsc, (span.AbsoluteTimestampMsc + span.DurationMsc),
                        currentThreadState);
                }
            }

            private ServerGCThreadState UpdateCurrentThreadState(GcJoin join, ServerGCThreadState oldState)
            {
                ServerGCThreadState newThreadState = oldState;
                switch (join.Time)
                {
                    case GcJoinTime.Start:
                        if ((join.Type == GcJoinType.LastJoin) || (join.Type == GcJoinType.FirstJoin))
                            newThreadState = ServerGCThreadState.SGCState_SingleThreaded;
                        else if (join.Type == GcJoinType.Restart)
                            newThreadState = ServerGCThreadState.SGCState_WaitingInRestart;
                        else
                            newThreadState = ServerGCThreadState.SGCState_WaitInJoin;
                        break;
                    case GcJoinTime.End:
                        if (join.Heap == HeapId)
                            newThreadState = ServerGCThreadState.SGCState_Ready;
                        break;
                    default:
                        break;
                }

                Parent.Parent.LogServerGCAnalysis("S: {0}->{1} {2:n3} - heap: {3}, time: {4}, type: {5}, id: {6}",
                    oldState, newThreadState,
                    join.AbsoluteTimestampMsc,
                    join.Heap, join.Time, join.Type, join.JoinID);

                return newThreadState;
            }

            // This is for verbose logging within a span (CSwitch or CPU sample).
            private void LogJoinInSpan(int currentJoinEventIndex, ServerGCThreadState state)
            {
                if ((GcJoins.Count > 0) && (currentJoinEventIndex < GcJoins.Count))
                {
                    Parent.Parent.LogServerGCAnalysis("{0:n3}: Heap{1}: Join {2}: type: {3}, time: {4} [S={5}]",
                        GcJoins[currentJoinEventIndex].AbsoluteTimestampMsc,
                        GcJoins[currentJoinEventIndex].Heap,
                        currentJoinEventIndex,
                        GcJoins[currentJoinEventIndex].Type, GcJoins[currentJoinEventIndex].Time, state);
                }
            }

            private void RenderSwitches(TextWriter writer, int scale)
            {
                double lastTimestamp = 0;
                foreach (var span in SwitchSpans)
                {
                    //filtering out workspans that ended before GC actually started
                    if (span.AbsoluteTimestampMsc + span.DurationMsc >= Parent.PauseStartRelativeMSec)
                    {
                        if (span.DurationMsc >= 1.0 || span.Type == WorkSpanType.GcThread || (span.RelativeTimestampMsc - lastTimestamp) >= 1.0)
                        {
                            string color = Type2Color[span.Type];
                            lastTimestamp = (int)(span.RelativeTimestampMsc);
                            int width = scale * (int)(span.DurationMsc + 1);
                            int x = scale * (int)(span.RelativeTimestampMsc);
                            if (x < 0)
                            {
                                width += x;
                                x = 0;
                            }

                            writer.WriteLine(string.Format("<rect x='{0}' y='2' width='{1}' height='30' style='fill:{2};' >", x, width, color));
                            writer.WriteLine(string.Format("<title>{0} (PID: {1} TIP: {2} Priority: {3} Timestamp:{4:0.00} Duration: {5}ms WR:{6})</title>",
                                span.ProcessName, span.ProcessId, span.ThreadId, span.Priority, span.AbsoluteTimestampMsc, (int)span.DurationMsc, span.WaitReason));
                            writer.WriteLine("</rect>");
                            //border
                            if (span.DurationMsc > 3)
                            {
                                writer.WriteLine(string.Format("<rect x='{0}' y='2' width='1' height='30' style='fill:rgb(0,0,0);' />", x + width - 1));
                            }
                        }
                    }
                }
            }

            private void RenderSamples(TextWriter writer, int scale)
            {
                if (GcJoins.Count > 0)
                {
                    activityStats = new ServerGCThreadStateInfo[(int)ServerGCThreadState.SGCState_Max];
                    for (int i = 0; i < activityStats.Length; i++)
                    {
                        activityStats[i] = new ServerGCThreadStateInfo();
                        activityStats[i].otherThreadsRunningTime = new Dictionary<int, OtherThreadInfo>();
                    }
                }

                int currentJoinEventIndex = 0;
                ServerGCThreadState currentThreadState = ServerGCThreadState.SGCState_Ready;
                ServerGCThreadState lastThreadState = currentThreadState;
                gcReadyTime = Parent.PauseStartRelativeMSec;
                lastGCSpanEndTime = gcReadyTime;

                Parent.Parent.LogServerGCAnalysis("GC#{0}, gen{1}, {2:n3} ms -> {3:n3} ms",
                    Parent.GCNumber, Parent.GCGeneration, Parent.PauseStartRelativeMSec, (Parent.PauseStartRelativeMSec + Parent.PauseDurationMSec));
                Parent.Parent.LogServerGCAnalysis("GC thread ready to run at {0:n3}ms", gcReadyTime);

                foreach (var span in SampleSpans)
                {
                    //filtering out workspans that ended before GC actually started
                    if (span.AbsoluteTimestampMsc + span.DurationMsc >= Parent.PauseStartRelativeMSec)
                    {
                        //Parent.Parent.LogServerGCAnalysis("CPU: {0:n1}->{1:n1}({2:n1}ms) from Process: {3}, thread {4}",
                        //    span.AbsoluteTimestampMsc, (span.AbsoluteTimestampMsc + span.DurationMsc),
                        //    span.DurationMsc,
                        //    span.ProcessName, span.ThreadId);

                        if ((GcJoins.Count > 0) && (currentJoinEventIndex < GcJoins.Count))
                        {
                            if (span.AbsoluteTimestampMsc > GcJoins[currentJoinEventIndex].AbsoluteTimestampMsc)
                            {
                                while ((currentJoinEventIndex < GcJoins.Count) &&
                                       (GcJoins[currentJoinEventIndex].AbsoluteTimestampMsc < span.AbsoluteTimestampMsc))
                                {
                                    currentThreadState = UpdateCurrentThreadState(GcJoins[currentJoinEventIndex], currentThreadState);
                                    //LogJoinInSpan(currentJoinEventIndex, currentThreadState);
                                    currentJoinEventIndex++;
                                }
                            }

                            double spanEndTime = span.AbsoluteTimestampMsc + span.DurationMsc;

                            // We straddle a join event, update state and attribute the thread time. Note there can be multiple joins
                            // in this sample.
                            if ((currentJoinEventIndex < GcJoins.Count) && (spanEndTime > GcJoins[currentJoinEventIndex].AbsoluteTimestampMsc))
                            {
                                double lastStateEndTime = ((span.AbsoluteTimestampMsc < Parent.PauseStartRelativeMSec) ?
                                                           Parent.PauseStartRelativeMSec : span.AbsoluteTimestampMsc);

                                while ((currentJoinEventIndex < GcJoins.Count) &&
                                       (GcJoins[currentJoinEventIndex].AbsoluteTimestampMsc < spanEndTime))
                                {
                                    double currentStateDuration = GcJoins[currentJoinEventIndex].AbsoluteTimestampMsc - lastStateEndTime;
                                    UpdateActivityThreadTime(span, activityStats[(int)currentThreadState], currentStateDuration, currentThreadState);

                                    currentThreadState = UpdateCurrentThreadState(GcJoins[currentJoinEventIndex], currentThreadState);
                                    //LogJoinInSpan(currentJoinEventIndex, currentThreadState);
                                    lastStateEndTime = GcJoins[currentJoinEventIndex].AbsoluteTimestampMsc;
                                    currentJoinEventIndex++;
                                }

                                // Attribute the last part of the sample.
                                UpdateActivityThreadTime(span, activityStats[(int)currentThreadState], (spanEndTime - lastStateEndTime), currentThreadState);
                            }
                            else
                            {
                                double duration = ((span.AbsoluteTimestampMsc < Parent.PauseStartRelativeMSec) ?
                                                   (span.AbsoluteTimestampMsc + span.DurationMsc - Parent.PauseStartRelativeMSec) :
                                                   span.DurationMsc);

                                UpdateActivityThreadTime(span, activityStats[(int)currentThreadState], duration, currentThreadState);
                            }
                        }

                        if (currentThreadState != lastThreadState)
                        {
                            if (lastThreadState == ServerGCThreadState.SGCState_WaitInJoin)
                            {
                                //Parent.Parent.LogServerGCAnalysis("last S: {0}, this S: {1}, GC thread ready to run at {2:n3}ms",
                                //    lastThreadState, currentThreadState, GcJoins[currentJoinEventIndex - 1].AbsoluteTimestampMsc);
                                gcReadyTime = GcJoins[currentJoinEventIndex - 1].AbsoluteTimestampMsc;
                            }
                            lastThreadState = currentThreadState;
                        }

                        if (span.ThreadId == GcWorkingThreadId)
                        {
                            lastGCSpanEndTime = span.AbsoluteTimestampMsc + span.DurationMsc;
                            //Parent.Parent.LogServerGCAnalysis("Updating last GC span end time to {0:n3}ms", lastGCSpanEndTime);
                        }

                        string color = Type2Color[span.Type];
                        int width = scale * (int)span.DurationMsc;
                        int x = scale * (int)(span.RelativeTimestampMsc);
                        if (x < 0)
                        {
                            width += x;
                            x = 0;
                        }

                        writer.WriteLine(string.Format("<rect x='{0}' y='2' width='{1}' height='30' style='fill:{2};' >", x, width, color));
                        writer.WriteLine(string.Format("<title>{0} (PID: {1} TIP: {2} Priority: {3} Timestamp:{4:0.00} Duration: {4}ms WR:{5})</title>",
                            span.ProcessName, span.ProcessId, span.ThreadId, span.Priority, span.AbsoluteTimestampMsc, (int)span.DurationMsc, span.WaitReason));
                        writer.WriteLine("</rect>");
                    }
                }

                if (GcJoins.Count > 0)
                {
                    for (int i = 0; i < (int)ServerGCThreadState.SGCState_Max; i++)
                    {
                        ServerGCThreadStateInfo info = activityStats[i];
                        Parent.Parent.LogServerGCAnalysis("---------[State - {0}]", SGCThreadStateDesc[i]);
                        Parent.Parent.LogServerGCAnalysis("[S{0}] GC: {1:n3} ms", i, info.gcThreadRunningTime);
                        var otherThreads = from pair in info.otherThreadsRunningTime
                                           orderby pair.Value.runningTime descending
                                           select pair;

                        // This is the time from non GC threads.
                        double interferenceTime = 0;

                        foreach (KeyValuePair<int, OtherThreadInfo> item in otherThreads)
                        {
                            // If it's less than 1ms we don't bother to print it.
                            //if (item.Value.runningTime > 1)
                            Parent.Parent.LogServerGCAnalysis("Process {0,8}({1,10}): {2:n3} ms", item.Key, item.Value.processName, item.Value.runningTime);
                            interferenceTime += item.Value.runningTime;
                        }

                        if ((i != (int)ServerGCThreadState.SGCState_WaitInJoin) && ((interferenceTime + info.gcThreadRunningTime) > 0.0))
                        {
                            Parent.Parent.LogServerGCAnalysis("[S{0}] Other threads took away {1:n2}% CPU from GC running time {2:n3}",
                                (ServerGCThreadState)i, (int)((interferenceTime * 100.0) / (interferenceTime + info.gcThreadRunningTime)), info.gcThreadRunningTime);
                        }
                    }
                }
            }
        }

        public void SetUpServerGcHistory()
        {
            for (int i = 0; i < heapCount; i++)
            {
                int gcThreadId = 0;
                int gcThreadPriority = 0;
                Parent.ServerGcHeap2ThreadId.TryGetValue(i, out gcThreadId);
                Parent.ThreadId2Priority.TryGetValue(gcThreadId, out gcThreadPriority);
                ServerGcHeapHistories.Add(new ServerGcHistory
                {
                    Parent = this,
                    ProcessId = Parent.ProcessID,
                    HeapId = i,
                    GcWorkingThreadId = gcThreadId,
                    GcWorkingThreadPriority = gcThreadPriority
                });
            }
        }

        public bool HasServerGcThreadingInfo
        {
            get
            {
                foreach (var heap in ServerGcHeapHistories)
                {
                    if (heap.SampleSpans.Count > 0 || heap.SwitchSpans.Count > 0)
                        return true;
                }
                return false;
            }
        }

        public long GetPinnedObjectSizes()
        {
            if (pinnedObjectSizes == -1)
            {
                pinnedObjectSizes = 0;
                foreach (KeyValuePair<ulong, long> item in PinnedObjects)
                {
                    pinnedObjectSizes += item.Value;
                }
            }
            return pinnedObjectSizes;
        }

        public int GetPinnedObjectPercentage()
        {
            if (totalPinnedPlugSize == -1)
            {
                totalPinnedPlugSize = 0;
                totalUserPinnedPlugSize = 0;

                foreach (KeyValuePair<ulong, long> item in PinnedObjects)
                {
                    ulong Address = item.Key;

                    for (int i = 0; i < PinnedPlugs.Count; i++)
                    {
                        if ((Address >= PinnedPlugs[i].Start) && (Address < PinnedPlugs[i].End))
                        {
                            PinnedPlugs[i].PinnedByUser = true;
                            break;
                        }
                    }
                }

                for (int i = 0; i < PinnedPlugs.Count; i++)
                {
                    long Size = (long)(PinnedPlugs[i].End - PinnedPlugs[i].Start);
                    totalPinnedPlugSize += Size;
                    if (PinnedPlugs[i].PinnedByUser)
                    {
                        totalUserPinnedPlugSize += Size;
                    }
                }
            }

            return ((totalPinnedPlugSize == 0) ? -1 : (int)((double)pinnedObjectSizes * 100 / (double)totalPinnedPlugSize));
        }

        public void AddServerGCThreadTime(int heapIndex, float cpuMSec)
        {
            if (GCCpuServerGCThreads != null)
            {
                if (heapIndex >= GCCpuServerGCThreads.Length)
                {
                    var old = GCCpuServerGCThreads;
                    GCCpuServerGCThreads = new float[heapIndex + 1];
                    Array.Copy(old, GCCpuServerGCThreads, old.Length);
                }
                GCCpuServerGCThreads[heapIndex] += cpuMSec;
            }
        }

        public void AddServerGcThreadSwitch(ThreadWorkSpan cswitch)
        {
            if (cswitch.ProcessorNumber >= 0 && cswitch.ProcessorNumber < ServerGcHeapHistories.Count)
                ServerGcHeapHistories[cswitch.ProcessorNumber].AddSwitchEvent(cswitch);
        }

        public void AddServerGcSample(ThreadWorkSpan sample)
        {
            if (sample.ProcessorNumber >= 0 && sample.ProcessorNumber < ServerGcHeapHistories.Count)
                ServerGcHeapHistories[sample.ProcessorNumber].AddSampleEvent(sample);
        }

        internal void AddGcJoin(GCJoinTraceData data)
        {
            if (data.Heap >= 0 && data.Heap < ServerGcHeapHistories.Count)
                ServerGcHeapHistories[data.Heap].AddJoin(data);
            else
            {
                foreach (var heap in ServerGcHeapHistories)
                    heap.AddJoin(data);
            }
        }

        public double GetTotalGCTime()
        {
            if (_TotalGCTimeMSec < 0)
            {
                _TotalGCTimeMSec = 0;
                if (GCCpuServerGCThreads != null)
                {
                    for (int i = 0; i < GCCpuServerGCThreads.Length; i++)
                    {
                        _TotalGCTimeMSec += GCCpuServerGCThreads[i];
                    }
                }
                _TotalGCTimeMSec += GCCpuMSec;
            }

            Debug.Assert(_TotalGCTimeMSec >= 0);
            return _TotalGCTimeMSec;
        }

        // This represents the percentage time spent paused for this GC since the last GC completed.
        public double GetPauseTimePercentageSinceLastGC()
        {
            double pauseTimePercentage;

            if (Type == GCType.BackgroundGC)
            {
                // Find all GCs that occurred during the current background GC.
                double startTimeRelativeMSec = this.GCStartRelativeMSec;
                double endTimeRelativeMSec = this.GCStartRelativeMSec + this.GCDurationMSec;

                // Calculate the pause time for this BGC.
                // Pause time is defined as pause time for the BGC + pause time for all FGCs that ran during the BGC.
                double totalPauseTime = this.PauseDurationMSec;

                if (Index + 1 < Events.Count)
                {
                    GCEvent gcEvent;
                    for (int i = Index + 1; i < Events.Count; ++i)
                    {
                        gcEvent = Events[i];
                        if ((gcEvent.GCStartRelativeMSec >= startTimeRelativeMSec) && (gcEvent.GCStartRelativeMSec < endTimeRelativeMSec))
                        {
                            totalPauseTime += gcEvent.PauseDurationMSec;
                        }
                        else
                        {
                            // We've finished processing all FGCs that occurred during this BGC.
                            break;
                        }
                    }
                }

                // Get the elapsed time since the previous GC finished.
                int previousGCIndex = Index - 1;
                double previousGCStopTimeRelativeMSec;
                if (previousGCIndex >= 0)
                {
                    GCEvent previousGCEvent = Events[previousGCIndex];
                    previousGCStopTimeRelativeMSec = previousGCEvent.GCStartRelativeMSec + previousGCEvent.GCDurationMSec;
                }
                else
                {
                    // Backstop in case this is the first GC.
                    previousGCStopTimeRelativeMSec = Events[0].GCStartRelativeMSec;
                }

                double totalTime = (GCStartRelativeMSec + GCDurationMSec) - previousGCStopTimeRelativeMSec;
                pauseTimePercentage = (totalPauseTime * 100) / (totalTime);
            }
            else
            {
                double totalTime = PauseDurationMSec + DurationSinceLastRestartMSec;
                pauseTimePercentage = (PauseDurationMSec * 100) / (totalTime);
            }

            Debug.Assert(pauseTimePercentage <= 100);
            return pauseTimePercentage;
        }

        public void GCEnd()
        {
            ConvertMarkTimes();
            foreach (var serverHeap in ServerGcHeapHistories)
            {
                serverHeap.GCEnd();
            }
        }

        // We recorded these as the timestamps when we saw the mark events, now convert them 
        // to the actual time that it took for each mark.
        private void ConvertMarkTimes()
        {
            if (PerHeapMarkTimes != null)
            {
                foreach (KeyValuePair<int, MarkInfo> item in PerHeapMarkTimes)
                {
                    if (item.Value.MarkTimes[(int)MarkRootType.MarkSizedRef] == 0.0)
                        item.Value.MarkTimes[(int)MarkRootType.MarkSizedRef] = GCStartRelativeMSec;

                    if (GCGeneration == 2)
                        item.Value.MarkTimes[(int)MarkRootType.MarkOlder] = 0;
                    else
                        item.Value.MarkTimes[(int)MarkRootType.MarkOlder] -= item.Value.MarkTimes[(int)MarkRootType.MarkHandles];

                    item.Value.MarkTimes[(int)MarkRootType.MarkHandles] -= item.Value.MarkTimes[(int)MarkRootType.MarkFQ];
                    item.Value.MarkTimes[(int)MarkRootType.MarkFQ] -= item.Value.MarkTimes[(int)MarkRootType.MarkStack];
                    item.Value.MarkTimes[(int)MarkRootType.MarkStack] -= item.Value.MarkTimes[(int)MarkRootType.MarkSizedRef];
                    item.Value.MarkTimes[(int)MarkRootType.MarkSizedRef] -= GCStartRelativeMSec;
                }
            }
        }

        // Derived information. 
        public object GCGenerationName
        {
            get
            {
                string typeSuffix = "";
                if (Type == GCType.NonConcurrentGC)
                    typeSuffix = "N";
                else if (Type == GCType.BackgroundGC)
                    typeSuffix = "B";
                else if (Type == GCType.ForegroundGC)
                    typeSuffix = "F";
                string inducedSuffix = "";
                if (Reason == GCReason.Induced)
                    inducedSuffix = "I";
                if (Reason == GCReason.InducedNotForced)
                    inducedSuffix = "i";
                return GCGeneration.ToString() + typeSuffix + inducedSuffix;
            }
        }
        public double HeapSizeBeforeMB
        {
            get
            {
                double ret = 0;
                for (Gens gen = Gens.Gen0; gen <= Gens.GenLargeObj; gen++)
                    ret += GenSizeBeforeMB(gen);
                return ret;
            }
        }
        /// <summary>
        /// This include fragmentation
        /// </summary>
        public double HeapSizeAfterMB
        {
            get
            {
                if (null != HeapStats)
                {
                    return (HeapStats.GenerationSize0 + HeapStats.GenerationSize1 + HeapStats.GenerationSize2 + HeapStats.GenerationSize3) / 1000000.0;
                }
                else
                {
                    return -1.0;
                }
            }
        }

        public double RatioPeakAfter { get { if (HeapSizeAfterMB == 0) return 0; return HeapSizePeakMB / HeapSizeAfterMB; } }
        public double AllocRateMBSec { get { return AllocedSinceLastGCMB * 1000.0 / DurationSinceLastRestartMSec; } }

        public double HeapSizePeakMB
        {
            get
            {
                var ret = HeapSizeBeforeMB;
                if (Type == GCType.BackgroundGC)
                {
                    var BgGcEndedRelativeMSec = PauseStartRelativeMSec + GCDurationMSec;
                    for (int i = Index + 1; i < Events.Count; i++)
                    {
                        var _event = Events[i];
                        if (BgGcEndedRelativeMSec < _event.PauseStartRelativeMSec)
                            break;
                        ret = Math.Max(ret, _event.HeapSizeBeforeMB);
                    }
                }
                return ret;
            }
        }
        public double CondemnedMB
        {
            get
            {
                double ret = GenSizeBeforeMB(0);
                if (1 <= GCGeneration)
                    ret += GenSizeBeforeMB(Gens.Gen1);
                if (2 <= GCGeneration)
                    ret += GenSizeBeforeMB(Gens.Gen2) + GenSizeBeforeMB(Gens.GenLargeObj);
                return ret;
            }
        }
        public double PromotedMB
        {
            get
            {
                return (HeapStats.TotalPromotedSize0 + HeapStats.TotalPromotedSize1 +
                       HeapStats.TotalPromotedSize2 + HeapStats.TotalPromotedSize3) / 1000000.0;
            }
        }

        public bool DetailedGenDataAvailable()
        {
            return (PerHeapHistories != null);
        }

        public double ObjSizeAfter(Gens gen)
        {
            double TotalObjSizeAfter = 0;

            if (PerHeapHistories != null)
            {
                for (int i = 0; i < PerHeapHistories.Count; i++)
                {
                    TotalObjSizeAfter += PerHeapGenData[i][(int)gen].ObjSizeAfter;
                }
            }

            return TotalObjSizeAfter;
        }

        public double SurvivalPercent(Gens gen)
        {
            double retSurvRate = double.NaN;

            long SurvRate = 0;

            if (gen == Gens.GenLargeObj)
            {
                if (GCGeneration < 2)
                {
                    return retSurvRate;
                }
            }
            else if ((int)gen > GCGeneration)
            {
                return retSurvRate;
            }

            if (PerHeapHistories != null)
            {
                for (int i = 0; i < PerHeapHistories.Count; i++)
                {
                    SurvRate += PerHeapGenData[i][(int)gen].SurvRate;
                }

                SurvRate /= PerHeapHistories.Count;
            }

            retSurvRate = SurvRate;

            return retSurvRate;
        }

        // When survival rate is 0, for certain releases (see comments for GetUserAllocatedPerHeap)
        // we need to estimate.
        private double EstimateAllocSurv0(int HeapIndex, Gens gen)
        {
            if (HasAllocTickEvents)
            {
                return AllocedSinceLastGCBasedOnAllocTickMB[(gen == Gens.Gen0) ? 0 : 1];
            }
            else
            {
                if (Index > 0)
                {
                    // If the prevous GC has that heap get its size.  
                    var perHeapGenData = Events[Index - 1].PerHeapGenData;
                    if (HeapIndex < perHeapGenData.Length)
                        return perHeapGenData[HeapIndex][(int)gen].Budget;
                }
                return 0;
            }
        }

        /// <summary>
        /// For a given heap, get what's allocated into gen0 or gen3.
        /// We calculate this differently on 4.0, 4.5 Beta and 4.5 RC+.
        /// The caveat with 4.0 and 4.5 Beta is that when survival rate is 0,
        /// We don't know how to calculate the allocated - so we just use the
        /// last GC's budget (We should indicate this in the tool)
        /// </summary>
        private double GetUserAllocatedPerHeap(int HeapIndex, Gens gen)
        {
            long prevObjSize = 0;
            if (Index > 0)
            {
                // If the prevous GC has that heap get its size.  
                var perHeapGenData = Events[Index - 1].PerHeapGenData;
                if (HeapIndex < perHeapGenData.Length)
                    prevObjSize = perHeapGenData[HeapIndex][(int)gen].ObjSizeAfter;
            }
            GCPerHeapHistoryGenData currentGenData = PerHeapGenData[HeapIndex][(int)gen];
            long survRate = currentGenData.SurvRate;
            long currentObjSize = currentGenData.ObjSizeAfter;
            double Allocated;

            if (currentGenData.HasObjSpaceBefore)
            {
                Allocated = currentGenData.ObjSpaceBefore - prevObjSize;
            }
            else
            {
                if (survRate == 0)
                    Allocated = EstimateAllocSurv0(HeapIndex, gen);
                else
                    Allocated = (currentGenData.Out + currentObjSize) * 100 / survRate - prevObjSize;
            }


            return Allocated;
        }
        /// <summary>
        /// Get what's allocated into gen0 or gen3. For server GC this gets the total for 
        /// all heaps.
        /// </summary>
        public double GetUserAllocated(Gens gen)
        {
            Debug.Assert((gen == Gens.Gen0) || (gen == Gens.GenLargeObj));

            if ((Type == GCType.BackgroundGC) && (gen == Gens.Gen0))
            {
                return AllocedSinceLastGCBasedOnAllocTickMB[(int)gen];
            }

            if (PerHeapHistories != null && Index > 0 && Events[Index - 1].PerHeapHistories != null)
            {
                double TotalAllocated = 0;
                if (Index > 0)
                {
                    for (int i = 0; i < PerHeapHistories.Count; i++)
                    {
                        double Allocated = GetUserAllocatedPerHeap(i, gen);

                        TotalAllocated += Allocated / 1000000.0;
                    }

                    return TotalAllocated;
                }
                else
                {
                    return GenSizeBeforeMB(gen);
                }
            }

            return AllocedSinceLastGCBasedOnAllocTickMB[(gen == Gens.Gen0) ? 0 : 1];
        }

        public double AllocedSinceLastGCMB
        {
            get
            {
                return GetUserAllocated(Gens.Gen0) + GetUserAllocated(Gens.GenLargeObj);
            }
        }
        public double FragmentationMB
        {
            get
            {
                double ret = 0;
                for (Gens gen = Gens.Gen0; gen <= Gens.GenLargeObj; gen++)
                    ret += GenFragmentationMB(gen);
                return ret;
            }
        }

        // Per generation stats.  
        public double GenSizeBeforeMB(Gens gen)
        {
            if (PerHeapHistories != null)
            {
                double ret = 0.0;
                for (int HeapIndex = 0; HeapIndex < PerHeapHistories.Count; HeapIndex++)
                    ret += PerHeapGenData[HeapIndex][(int)gen].SizeBefore / 1000000.0;
                return ret;
            }

            // When we don't have perheap history we can only estimate for gen0 and gen3.
            double Gen0SizeBeforeMB = 0;
            if (gen == Gens.Gen0)
                Gen0SizeBeforeMB = AllocedSinceLastGCBasedOnAllocTickMB[0];
            if (Index == 0)
            {
                return ((gen == Gens.Gen0) ? Gen0SizeBeforeMB : 0);
            }

            // Find a previous HeapStats.  
            GCHeapStatsTraceData heapStats = null;
            for (int j = Index - 1; ; --j)
            {
                if (j == 0)
                    return 0;
                heapStats = Events[j].HeapStats;
                if (heapStats != null)
                    break;
            }
            if (gen == Gens.Gen0)
                return Math.Max((heapStats.GenerationSize0 / 1000000.0), Gen0SizeBeforeMB);
            if (gen == Gens.Gen1)
                return heapStats.GenerationSize1 / 1000000.0;
            if (gen == Gens.Gen2)
                return heapStats.GenerationSize2 / 1000000.0;

            Debug.Assert(gen == Gens.GenLargeObj);

            if (HeapStats != null)
                return Math.Max(heapStats.GenerationSize3, HeapStats.GenerationSize3) / 1000000.0;
            else
                return heapStats.GenerationSize3 / 1000000.0;
        }
        public double GenSizeAfterMB(Gens gen)
        {
            if (gen == Gens.GenLargeObj)
                return HeapStats.GenerationSize3 / 1000000.0;
            if (gen == Gens.Gen2)
                return HeapStats.GenerationSize2 / 1000000.0;
            if (gen == Gens.Gen1)
                return HeapStats.GenerationSize1 / 1000000.0;
            if (gen == Gens.Gen0)
                return HeapStats.GenerationSize0 / 1000000.0;
            Debug.Assert(false);
            return double.NaN;
        }
        public void GetGenDataSizeAfterMB(ref double[] GenData)
        {
            for (int GenIndex = 0; GenIndex <= (int)Gens.GenLargeObj; GenIndex++)
                GenData[GenIndex] = GenSizeAfterMB((Gens)GenIndex);
        }
        public void GetGenDataObjSizeAfterMB(ref double[] GenData)
        {
            for (int HeapIndex = 0; HeapIndex < PerHeapHistories.Count; HeapIndex++)
            {
                for (int GenIndex = 0; GenIndex <= (int)Gens.GenLargeObj; GenIndex++)
                    GenData[GenIndex] += PerHeapGenData[HeapIndex][GenIndex].ObjSizeAfter / 1000000.0;
            }
        }
        public double GetMaxGen0ObjSizeMB()
        {
            double MaxGen0ObjSize = 0;
            for (int HeapIndex = 0; HeapIndex < PerHeapHistories.Count; HeapIndex++)
            {
                MaxGen0ObjSize = Math.Max(MaxGen0ObjSize, PerHeapGenData[HeapIndex][(int)Gens.Gen0].ObjSizeAfter / 1000000.0);
            }
            return MaxGen0ObjSize;
        }
        public double GenFragmentationMB(Gens gen)
        {
            if (PerHeapHistories == null)
                return double.NaN;

            double ret = 0.0;
            for (int HeapIndex = 0; HeapIndex < PerHeapHistories.Count; HeapIndex++)
                ret += PerHeapGenData[HeapIndex][(int)gen].Fragmentation / 1000000.0;
            return ret;
        }
        public double GenFreeListBefore(Gens gen)
        {
            if (PerHeapHistories == null)
                return double.NaN;
            double ret = 0.0;
            for (int HeapIndex = 0; HeapIndex < PerHeapHistories.Count; HeapIndex++)
                ret += PerHeapGenData[HeapIndex][(int)gen].FreeListSpaceBefore;
            return ret;
        }

        public double GenFreeListAfter(Gens gen)
        {
            if (PerHeapHistories == null)
                return double.NaN;
            double ret = 0.0;
            for (int HeapIndex = 0; HeapIndex < PerHeapHistories.Count; HeapIndex++)
                ret += PerHeapGenData[HeapIndex][(int)gen].FreeListSpaceAfter;
            return ret;
        }

        public double GenFragmentationPercent(Gens gen)
        {
            return (GenFragmentationMB(gen) * 100.0 / GenSizeAfterMB(gen));
        }

        //
        // Approximations we do in this function for V4_5 and prior:
        // On 4.0 we didn't seperate free list from free obj, so we just use fragmentation (which is the sum)
        // as an approximation. This makes the efficiency value a bit larger than it actually is.
        // We don't actually update in for the older gen - this means we only know the out for the younger 
        // gen which isn't necessarily all allocated into the older gen. So we could see cases where the 
        // out is > 0, yet the older gen's free list doesn't change. Using the younger gen's out as an 
        // approximation makes the efficiency value larger than it actually is.
        //
        // For V4_6 this requires no approximation.
        //
        public bool GetFreeListEfficiency(Gens gen, ref double Allocated, ref double FreeListConsumed)
        {
            // I am not worried about gen0 or LOH's free list efficiency right now - it's 
            // calculated differently.
            if ((PerHeapHistories == null) ||
                (gen == Gens.Gen0) ||
                (gen == Gens.GenLargeObj) ||
                (Index <= 0) ||
                !(PerHeapHistories[0].VersionRecognized))
            {
                return false;
            }

            int YoungerGen = (int)gen - 1;

            if (GCGeneration != YoungerGen)
                return false;

            if (PerHeapHistories[0].HasFreeListAllocated)
            {
                Allocated = 0;
                FreeListConsumed = 0;
                for (int HeapIndex = 0; HeapIndex < PerHeapHistories.Count; HeapIndex++)
                {
                    GCPerHeapHistoryTraceData hist = (GCPerHeapHistoryTraceData)PerHeapHistories[HeapIndex];
                    Allocated += hist.FreeListAllocated;
                    FreeListConsumed += hist.FreeListAllocated + hist.FreeListRejected;
                }
                return true;
            }

            // I am not using MB here because what's promoted from gen1 can easily be less than a MB.
            double YoungerGenOut = 0;
            double FreeListBefore = 0;
            double FreeListAfter = 0;
            // Includes fragmentation. This lets us know if we had to expand the size.
            double GenSizeBefore = 0;
            double GenSizeAfter = 0;
            for (int HeapIndex = 0; HeapIndex < PerHeapHistories.Count; HeapIndex++)
            {
                YoungerGenOut += PerHeapGenData[HeapIndex][YoungerGen].Out;
                GenSizeBefore += PerHeapGenData[HeapIndex][(int)gen].SizeBefore;
                GenSizeAfter += PerHeapGenData[HeapIndex][(int)gen].SizeAfter;
                // Occasionally I've seen a GC in the middle that simply missed some events,
                // some of which are PerHeap hist events so we don't have data.
                if (Events[Index - 1].PerHeapGenData == null)
                    return false;
                if (PerHeapGenData[HeapIndex][(int)gen].HasFreeListSpaceAfter && PerHeapGenData[HeapIndex][(int)gen].HasFreeListSpaceBefore)
                {
                    FreeListBefore += PerHeapGenData[HeapIndex][(int)gen].FreeListSpaceBefore;
                    FreeListAfter += PerHeapGenData[HeapIndex][(int)gen].FreeListSpaceAfter;
                }
                else
                {
                    FreeListBefore += Events[Index - 1].PerHeapGenData[HeapIndex][(int)gen].Fragmentation;
                    FreeListAfter += PerHeapGenData[HeapIndex][(int)gen].Fragmentation;
                }
            }

            double GenSizeGrown = GenSizeAfter - GenSizeBefore;

            // This is the most accurate situation we can calculuate (if it's not accurate it means
            // we are over estimating which is ok.
            if ((GenSizeGrown == 0) && ((FreeListBefore > 0) && (FreeListAfter >= 0)))
            {
                Allocated = YoungerGenOut;
                FreeListConsumed = FreeListBefore - FreeListAfter;
                // We don't know how much of the survived is pinned so we are overestimating here.
                if (Allocated < FreeListConsumed)
                    return true;
            }

            return false;
        }

        public double GenInMB(Gens gen)
        {
            if (PerHeapHistories == null)
                return double.NaN;
            double ret = 0.0;
            for (int HeapIndex = 0; HeapIndex < PerHeapHistories.Count; HeapIndex++)
                ret += PerHeapGenData[HeapIndex][(int)gen].In / 1000000.0;
            return ret;
        }
        public double GenOutMB(Gens gen)
        {
            if (PerHeapHistories == null)
                return double.NaN;
            double ret = 0.0;
            for (int HeapIndex = 0; HeapIndex < PerHeapHistories.Count; HeapIndex++)
                ret += PerHeapGenData[HeapIndex][(int)gen].Out / 1000000.0;
            return ret;
        }
        public double GenOut(Gens gen)
        {
            if (PerHeapHistories == null)
                return double.NaN;
            double ret = 0.0;
            for (int HeapIndex = 0; HeapIndex < PerHeapHistories.Count; HeapIndex++)
                ret += PerHeapGenData[HeapIndex][(int)gen].Out;
            return ret;
        }
        public double GenPinnedSurv(Gens gen)
        {
            if ((PerHeapHistories == null) || !(PerHeapGenData[0][0].HasPinnedSurv))
                return double.NaN;
            double ret = 0.0;
            for (int HeapIndex = 0; HeapIndex < PerHeapHistories.Count; HeapIndex++)
                ret += PerHeapGenData[HeapIndex][(int)gen].PinnedSurv;
            return ret;
        }
        public double GenNonePinnedSurv(Gens gen)
        {
            if ((PerHeapHistories == null) || !(PerHeapGenData[0][0].HasNonePinnedSurv))
                return double.NaN;
            double ret = 0.0;
            for (int HeapIndex = 0; HeapIndex < PerHeapHistories.Count; HeapIndex++)
                ret += PerHeapGenData[HeapIndex][(int)gen].NonePinnedSurv;
            return ret;
        }
        // Note that in 4.0 TotalPromotedSize is not entirely accurate (since it doesn't
        // count the pins that got demoted. We could consider using the PerHeap event data
        // to compute the accurate promoted size. 
        // In 4.5 this is accurate.
        public double GenPromotedMB(Gens gen)
        {
            if (gen == Gens.GenLargeObj)
                return HeapStats.TotalPromotedSize3 / 1000000.0;
            if (gen == Gens.Gen2)
                return HeapStats.TotalPromotedSize2 / 1000000.0;
            if (gen == Gens.Gen1)
                return HeapStats.TotalPromotedSize1 / 1000000.0;
            if (gen == Gens.Gen0)
                return HeapStats.TotalPromotedSize0 / 1000000.0;
            Debug.Assert(false);
            return double.NaN;
        }
        public double GenBudgetMB(Gens gen)
        {
            if (PerHeapHistories == null)
                return double.NaN;
            double budget = 0.0;
            for (int HeapIndex = 0; HeapIndex < PerHeapHistories.Count; HeapIndex++)
                budget += PerHeapGenData[HeapIndex][(int)gen].Budget / 1000000.0;
            return budget;
        }

        // There's a list of things we need to get from the events we collected. 
        // To increase the efficiency so we don't need to go back to TraceEvent
        // too often we construct the generation data all at once here.
        private GCPerHeapHistoryGenData[][] PerHeapGenData
        {
            get
            {
                if ((PerHeapHistories != null) && (_PerHeapGenData == null))
                {
                    int NumHeaps = PerHeapHistories.Count;
                    _PerHeapGenData = new GCPerHeapHistoryGenData[NumHeaps][];
                    for (int HeapIndex = 0; HeapIndex < NumHeaps; HeapIndex++)
                    {
                        _PerHeapGenData[HeapIndex] = new GCPerHeapHistoryGenData[(int)Gens.GenLargeObj + 1];
                        for (Gens GenIndex = Gens.Gen0; GenIndex <= Gens.GenLargeObj; GenIndex++)
                        {
                            _PerHeapGenData[HeapIndex][(int)GenIndex] = PerHeapHistories[HeapIndex].GenData(GenIndex);
                        }
                    }
                }

                return _PerHeapGenData;
            }
        }

        private GCCondemnedReasons[] PerHeapCondemnedReasons
        {
            get
            {
                if ((PerHeapHistories != null) && (_PerHeapCondemnedReasons == null))
                {
                    int NumHeaps = PerHeapHistories.Count;
                    _PerHeapCondemnedReasons = new GCCondemnedReasons[NumHeaps];

                    for (int HeapIndex = 0; HeapIndex < NumHeaps; HeapIndex++)
                    {
                        _PerHeapCondemnedReasons[HeapIndex] = new GCCondemnedReasons();
                        _PerHeapCondemnedReasons[HeapIndex].EncodedReasons.Reasons = PerHeapHistories[HeapIndex].CondemnReasons0;
                        if (PerHeapHistories[HeapIndex].HasCondemnReasons1)
                        {
                            _PerHeapCondemnedReasons[HeapIndex].EncodedReasons.ReasonsEx = PerHeapHistories[HeapIndex].CondemnReasons1;
                        }
                        _PerHeapCondemnedReasons[HeapIndex].CondemnedReasonGroups = new byte[(int)CondemnedReasonGroup.CRG_Max];
                        _PerHeapCondemnedReasons[HeapIndex].Decode(PerHeapHistories[HeapIndex].Version);
                    }
                }

                return _PerHeapCondemnedReasons;
            }
        }

        private enum InducedType
        {
            Blocking = 1,
            NotForced = 2,
        }

        // This is what we use for the html header and the help text.
        public static string[][] CondemnedReasonsHtmlHeader = new string[(int)CondemnedReasonGroup.CRG_Max][]
        {
            new string[] {"Initial<BR/>Requested<BR/>Generation", "This is the generation when this GC was triggered"},
            new string[] {"Final<BR/>Generation", "The final generation to be collected"},
            new string[] {"Generation<BR/>Budget<BR/>Exceeded", "This is the highest generation whose budget is exceeded"},
            new string[] {"Time<BR/>Tuning", "Time exceeded between GCs so we need to collect this generation"},
            new string[] {"Induced", "Blocking means this was induced as a blocking GC; NotForced means it's up to GC to decide whether it should be a blocking GC or a background GC"},
            new string[] {"Ephemeral<BR/>Low", "We are running low on the ephemeral segment, GC needs to do at least a gen1 GC"},
            new string[] {"Expand<BR/>Heap", "We are running low in an ephemeral GC, GC needs to do a full GC"},
            new string[] {"Fragmented<BR/>Ephemeral", "Ephemeral generations are fragmented"},
            new string[] {"Very<BR/>Fragmented<BR/>Ephemeral", "Ephemeral generations are VERY fragmented, doing a full GC"},
            new string[] {"Fragmented<BR/>Gen2", "Gen2 is too fragmented, doing a full blocking GC"},
            new string[] {"High<BR/>Memory", "We are in high memory load situation and doing a full blocking GC"},
            new string[] {"Compacting<BR/>Full<BR/>GC", "Last GC we trigger before we throw OOM"},
            new string[] {"Small<BR/>Heap", "Heap is too small for doing a background GC and we do a blocking one instead"},
            new string[] {"Ephemeral<BR/>Before<BR/>BGC", "Ephemeral GC before a background GC starts"},
            new string[] {"Internal<BR/>Tuning", "Internal tuning"}
        };

        private struct EncodedCondemnedReasons
        {
            public int Reasons;
            public int ReasonsEx;
        }

        private class GCCondemnedReasons
        {
            // These values right now are the same as the first 4 in CondemnedReasonGroup.
            enum Condemned_Reason_Generation
            {
                CRG_initial = 0,
                CRG_final_per_heap = 1,
                CRG_alloc_budget = 2,
                CRG_time_tuning = 3,
                CRG_max = 4,
            };

            enum Condemned_Reason_Condition
            {
                CRC_induced_fullgc_p = 0,
                CRC_expand_fullgc_p = 1,
                CRC_high_mem_p = 2,
                CRC_very_high_mem_p = 3,
                CRC_low_ephemeral_p = 4,
                CRC_low_card_p = 5,
                CRC_eph_high_frag_p = 6,
                CRC_max_high_frag_p = 7,
                CRC_max_high_frag_e_p = 8,
                CRC_max_high_frag_m_p = 9,
                CRC_max_high_frag_vm_p = 10,
                CRC_max_gen1 = 11,
                CRC_before_oom = 12,
                CRC_gen2_too_small = 13,
                CRC_induced_noforce_p = 14,
                CRC_before_bgc = 15,
                CRC_max = 16,
            };

            private int GetReasonWithGenNumber(Condemned_Reason_Generation Reason_GenNumber)
            {
                int GenNumber = ((EncodedReasons.Reasons >> ((int)Reason_GenNumber * 2)) & 0x3);
                return GenNumber;
            }

            private bool GetReasonWithCondition(Condemned_Reason_Condition Reason_Condition, int Version)
            {
                bool ConditionIsSet = false;
                if (Version == 0)
                {
                    Debug.Assert((int)Reason_Condition < 16);
                    ConditionIsSet = ((EncodedReasons.Reasons & (1 << (int)(Reason_Condition + 16))) != 0);
                }
                else if (Version >= 2)
                {
                    ConditionIsSet = ((EncodedReasons.ReasonsEx & (1 << (int)Reason_Condition)) != 0);
                }
                else Debug.Assert(false, "GetReasonWithCondition invalid version : " + Version);

                return ConditionIsSet;
            }

            public EncodedCondemnedReasons EncodedReasons;
            /// <summary>
            /// This records which reasons are used and the value. Since the biggest value
            /// we need to record is the generation number a byte is sufficient.
            /// </summary>
            public byte[] CondemnedReasonGroups;

            public void Decode(int Version)
            {
                // First decode the reasons that return us a generation number. 
                // It's the same in 4.0 and 4.5.
                for (Condemned_Reason_Generation i = 0; i < Condemned_Reason_Generation.CRG_max; i++)
                {
                    CondemnedReasonGroups[(int)i] = (byte)GetReasonWithGenNumber(i);
                }

                // Then decode the reasons that just indicate true or false.
                for (Condemned_Reason_Condition i = 0; i < Condemned_Reason_Condition.CRC_max; i++)
                {
                    if (GetReasonWithCondition(i, Version))
                    {
                        switch (i)
                        {
                            case Condemned_Reason_Condition.CRC_induced_fullgc_p:
                                CondemnedReasonGroups[(int)CondemnedReasonGroup.CRG_Induced] = (byte)InducedType.Blocking;
                                break;
                            case Condemned_Reason_Condition.CRC_induced_noforce_p:
                                CondemnedReasonGroups[(int)CondemnedReasonGroup.CRG_Induced] = (byte)InducedType.NotForced;
                                break;
                            case Condemned_Reason_Condition.CRC_low_ephemeral_p:
                                CondemnedReasonGroups[(int)CondemnedReasonGroup.CRG_Low_Ephemeral] = 1;
                                break;
                            case Condemned_Reason_Condition.CRC_low_card_p:
                                CondemnedReasonGroups[(int)CondemnedReasonGroup.CRG_Internal_Tuning] = 1;
                                break;
                            case Condemned_Reason_Condition.CRC_eph_high_frag_p:
                                CondemnedReasonGroups[(int)CondemnedReasonGroup.CRG_Fragmented_Ephemeral] = 1;
                                break;
                            case Condemned_Reason_Condition.CRC_max_high_frag_p:
                                CondemnedReasonGroups[(int)CondemnedReasonGroup.CRG_Fragmented_Gen2] = 1;
                                break;
                            case Condemned_Reason_Condition.CRC_max_high_frag_e_p:
                                CondemnedReasonGroups[(int)CondemnedReasonGroup.CRG_Fragmented_Gen1_To_Gen2] = 1;
                                break;
                            case Condemned_Reason_Condition.CRC_max_high_frag_m_p:
                            case Condemned_Reason_Condition.CRC_max_high_frag_vm_p:
                                CondemnedReasonGroups[(int)CondemnedReasonGroup.CRG_Fragmented_Gen2_High_Mem] = 1;
                                break;
                            case Condemned_Reason_Condition.CRC_max_gen1:
                                CondemnedReasonGroups[(int)CondemnedReasonGroup.CRG_Alloc_Exceeded] = 2;
                                break;
                            case Condemned_Reason_Condition.CRC_expand_fullgc_p:
                                CondemnedReasonGroups[(int)CondemnedReasonGroup.CRG_Expand_Heap] = 1;
                                break;
                            case Condemned_Reason_Condition.CRC_before_oom:
                                CondemnedReasonGroups[(int)CondemnedReasonGroup.CRG_GC_Before_OOM] = 1;
                                break;
                            case Condemned_Reason_Condition.CRC_gen2_too_small:
                                CondemnedReasonGroups[(int)CondemnedReasonGroup.CRG_Too_Small_For_BGC] = 1;
                                break;
                            case Condemned_Reason_Condition.CRC_before_bgc:
                                CondemnedReasonGroups[(int)CondemnedReasonGroup.CRG_Ephemeral_Before_BGC] = 1;
                                break;
                            default:
                                Debug.Assert(false, "Unexpected reason");
                                break;
                        }
                    }
                }
            }
        }

        private int FindFirstHighestCondemnedHeap()
        {
            int GenNumberHighest = (int)GCGeneration;
            for (int HeapIndex = 0; HeapIndex < PerHeapCondemnedReasons.Length; HeapIndex++)
            {
                int gen = PerHeapCondemnedReasons[HeapIndex].CondemnedReasonGroups[(int)CondemnedReasonGroup.CRG_Final_Generation];
                if (gen == GenNumberHighest)
                {
                    return HeapIndex;
                }
            }

            return 0;
        }

        // For true/false groups, return whether that group is set.
        private bool CondemnedReasonGroupSet(CondemnedReasonGroup Group)
        {
            if (PerHeapCondemnedReasons == null)
            {
                return false;
            }

            int HeapIndexHighestGen = 0;
            if (PerHeapCondemnedReasons.Length != 1)
            {
                HeapIndexHighestGen = FindFirstHighestCondemnedHeap();
            }

            return (PerHeapCondemnedReasons[HeapIndexHighestGen].CondemnedReasonGroups[(int)Group] != 0);
        }

        public bool IsLowEphemeral()
        {
            return CondemnedReasonGroupSet(CondemnedReasonGroup.CRG_Low_Ephemeral);
        }

        public bool IsNotCompacting()
        {
            return ((GlobalHeapHistory.GlobalMechanisms & (GCGlobalMechanisms.Compaction)) != 0);
        }

        public string PrintCondemnedReasonsToHtml()
        {
            if (PerHeapCondemnedReasons == null)
            {
                return null;
            }

            StringBuilder sb = new StringBuilder(100);
            int HeapIndexHighestGen = 0;

            if (PerHeapCondemnedReasons.Length != 1)
            {
                // Only need to print out the heap index for server GC - when we are displaying this
                // in the GCStats Html page we only display the first heap we find that caused us to 
                // collect the generation we collect.
                HeapIndexHighestGen = FindFirstHighestCondemnedHeap();

                // We also need to consider the factors that cause blocking GCs.
                if (((int)GCGeneration == 2) && (Type != GCType.BackgroundGC))
                {
                    int GenToCheckBlockingIndex = HeapIndexHighestGen;
                    int BlockingFactorsHighest = 0;

                    for (int HeapIndex = GenToCheckBlockingIndex; HeapIndex < PerHeapCondemnedReasons.Length; HeapIndex++)
                    {
                        byte[] ReasonGroups = PerHeapCondemnedReasons[HeapIndex].CondemnedReasonGroups;
                        int BlockingFactors = ReasonGroups[(int)CondemnedReasonGroup.CRG_Expand_Heap] +
                                              ReasonGroups[(int)CondemnedReasonGroup.CRG_GC_Before_OOM] +
                                              ReasonGroups[(int)CondemnedReasonGroup.CRG_Fragmented_Gen2] +
                                              ReasonGroups[(int)CondemnedReasonGroup.CRG_Fragmented_Gen2_High_Mem];

                        if (BlockingFactors > BlockingFactorsHighest)
                        {
                            HeapIndexHighestGen = HeapIndex;
                        }
                    }
                }

                sb.Append("<TD Align=\"center\">");
                sb.Append(HeapIndexHighestGen);
                sb.Append("</TD>");
            }

            for (CondemnedReasonGroup i = 0; i < CondemnedReasonGroup.CRG_Max; i++)
            {
                sb.Append("<TD Align=\"center\">");
                if (i == CondemnedReasonGroup.CRG_Induced)
                {
                    sb.Append((InducedType)PerHeapCondemnedReasons[HeapIndexHighestGen].CondemnedReasonGroups[(int)i]);
                }
                else
                    sb.Append(PerHeapCondemnedReasons[HeapIndexHighestGen].CondemnedReasonGroups[(int)i]);
                sb.Append("</TD>");
            }

            sb.Append(Environment.NewLine);

            return sb.ToString();
        }

        private void AddCondemnedReason(Dictionary<CondemnedReasonGroup, int> ReasonsInfo, CondemnedReasonGroup Reason)
        {
            if (!ReasonsInfo.ContainsKey(Reason))
                ReasonsInfo.Add(Reason, 1);
            else
                (ReasonsInfo[Reason])++;
        }

        public void GetCondemnedReasons(Dictionary<CondemnedReasonGroup, int> ReasonsInfo)
        {
            // Older versions of the runtime does not have this event. So even for a complete GC, we may not have this
            // info.
            if (PerHeapCondemnedReasons == null)
                return;

            int HeapIndexHighestGen = 0;
            if (PerHeapCondemnedReasons.Length != 1)
            {
                HeapIndexHighestGen = FindFirstHighestCondemnedHeap();
            }

            byte[] ReasonGroups = PerHeapCondemnedReasons[HeapIndexHighestGen].CondemnedReasonGroups;

            // These 2 reasons indicate a gen number. If the number is the same as the condemned gen, we 
            // include this reason.
            for (int i = (int)CondemnedReasonGroup.CRG_Alloc_Exceeded; i <= (int)CondemnedReasonGroup.CRG_Time_Tuning; i++)
            {
                if (ReasonGroups[i] == GCGeneration)
                    AddCondemnedReason(ReasonsInfo, (CondemnedReasonGroup)i);
            }

            if (ReasonGroups[(int)CondemnedReasonGroup.CRG_Induced] != 0)
            {
                if (ReasonGroups[(int)CondemnedReasonGroup.CRG_Initial_Generation] == GCGeneration)
                {
                    AddCondemnedReason(ReasonsInfo, CondemnedReasonGroup.CRG_Induced);
                }
            }

            // The rest of the reasons are conditions so include the ones that are set.
            for (int i = (int)CondemnedReasonGroup.CRG_Low_Ephemeral; i < (int)CondemnedReasonGroup.CRG_Max; i++)
            {
                if (ReasonGroups[i] != 0)
                    AddCondemnedReason(ReasonsInfo, (CondemnedReasonGroup)i);
            }
        }

        public void AddLOHWaitThreadInfo(int TID, double time, int reason, bool IsStart)
        {
            BGCAllocWaitReason ReasonLOHAlloc = (BGCAllocWaitReason)reason;

            if ((ReasonLOHAlloc == BGCAllocWaitReason.GetLOHSeg) ||
                (ReasonLOHAlloc == BGCAllocWaitReason.AllocDuringSweep))
            {
                if (LOHWaitThreads == null)
                {
                    LOHWaitThreads = new Dictionary<int, BGCAllocWaitInfo>();
                }

                BGCAllocWaitInfo info;

                if (LOHWaitThreads.TryGetValue(TID, out info))
                {
                    if (IsStart)
                    {
                        // If we are finding the value it means we are hitting the small
                        // window where BGC sweep finished and BGC itself finished, discard
                        // this.
                    }
                    else
                    {
                        Debug.Assert(info.Reason == ReasonLOHAlloc);
                        info.WaitStopRelativeMSec = time;
                    }
                }
                else
                {
                    info = new BGCAllocWaitInfo();
                    if (IsStart)
                    {
                        info.Reason = ReasonLOHAlloc;
                        info.WaitStartRelativeMSec = time;
                    }
                    else
                    {
                        // We are currently not displaying this because it's incomplete but I am still adding 
                        // it so we could display if we want to.
                        info.WaitStopRelativeMSec = time;
                    }

                    LOHWaitThreads.Add(TID, info);
                }
            }
        }

        // TODO: get rid of the remaining version checking here - convert the leftover checks with using the Has* methods 
        // to determine whether that particular data is available.
        private enum PerHeapEventVersion
        {
            V0, // Not set
            V4_0,
            V4_5,
            V4_6,
        }

        public class MarkInfo
        {
            // Note that in 4.5 and prior (ie, from GCMark events, not GCMarkWithType), the first stage of the time 
            // includes scanning sizedref handles(which can be very significant). We could distinguish that by interpreting 
            // the Join events which I haven't done yet.
            public double[] MarkTimes;
            public long[] MarkPromoted;

            public MarkInfo(bool initPromoted = true)
            {
                MarkTimes = new double[(int)MarkRootType.MarkMax];
                if (initPromoted)
                    MarkPromoted = new long[(int)MarkRootType.MarkMax];
            }
        };

        public class PinnedPlug
        {
            public ulong Start;
            public ulong End;
            public bool PinnedByUser;

            public PinnedPlug(ulong s, ulong e)
            {
                Start = s;
                End = e;
                PinnedByUser = false;
            }
        };

        public Dictionary<ulong, long> PinnedObjects = new Dictionary<ulong, long>();
        public List<PinnedPlug> PinnedPlugs = new List<PinnedPlug>();
        private long pinnedObjectSizes;
        public long duplicatedPinningReports;
        public long totalPinnedPlugSize;
        public long totalUserPinnedPlugSize;
        public List<GCPerHeapHistoryTraceData> PerHeapHistories;
        // The dictionary of heap number and info on time it takes to mark various roots.
        public Dictionary<int, MarkInfo> PerHeapMarkTimes;
        private GCPerHeapHistoryGenData[][] _PerHeapGenData;
        private GCCondemnedReasons[] _PerHeapCondemnedReasons;
        public GCGlobalHeapHistoryTraceData GlobalHeapHistory;
        public GCHeapStatsTraceData HeapStats;

        public Dictionary<int, BGCAllocWaitInfo> LOHWaitThreads;

        //returns true if server GC graph has data
        internal bool ServerGcConcurrencyGraphs(TextWriter writer)
        {
            bool hasData = false;
            writer.WriteLine("<div>");
            writer.WriteLine("<h4>" + GCNumber + "</h4>");

            int scale;
            if (PauseDurationMSec < 100)
                scale = 3;
            else if (PauseDurationMSec < 600)
                scale = 2;
            else
                scale = 1;

            writer.WriteLine("Gen" + GCGeneration + " Pause:" + (int)PauseDurationMSec + "ms");
            writer.WriteLine("1ms = " + scale + "px");
            foreach (var heap in this.ServerGcHeapHistories)
            {
                if (heap.SwitchSpans.Count > 0 || heap.SampleSpans.Count > 0)
                {
                    writer.WriteLine("<table><tr>");
                    writer.WriteLine("<td style='min-width:200px'>Heap #" + heap.HeapId + " Gc Thread Id: " + heap.GcWorkingThreadId + "</td>");
                    writer.WriteLine("<td>");
                    Parent.LogServerGCAnalysis("--------------[HEAP {0}]--------------", heap.HeapId);
                    heap.RenderGraph(writer, scale);
                    writer.WriteLine("</td></tr></table>");
                    hasData = true;
                }
            }
            writer.WriteLine("</div>");
            return hasData;
        }

        public void ToXml(TextWriter writer)
        {
            writer.Write("   <GCEvent");
            writer.Write(" GCNumber="); QuotePadLeft(writer, GCNumber.ToString(), 10);
            writer.Write(" GCGeneration="); QuotePadLeft(writer, GCGeneration.ToString(), 3);
            writer.Write(" GCCpuMSec="); QuotePadLeft(writer, GCCpuMSec.ToString("n0").ToString(), 10);
            writer.Write(" ProcessCpuMSec="); QuotePadLeft(writer, ProcessCpuMSec.ToString("n0").ToString(), 10);
            writer.Write(" PercentTimeInGC="); QuotePadLeft(writer, PercentTimeInGC.ToString("n2").ToString(), 10);
            writer.Write(" PauseStartRelativeMSec="); QuotePadLeft(writer, PauseStartRelativeMSec.ToString("n3").ToString(), 10);
            writer.Write(" PauseDurationMSec="); QuotePadLeft(writer, PauseDurationMSec.ToString("n3").ToString(), 10);
            writer.Write(" PercentPauseTime="); QuotePadLeft(writer, GetPauseTimePercentageSinceLastGC().ToString("n2").ToString(), 10);
            writer.Write(" SizePeakMB="); QuotePadLeft(writer, HeapSizePeakMB.ToString("n3"), 10);
            writer.Write(" SizeAfterMB="); QuotePadLeft(writer, HeapSizeAfterMB.ToString("n3"), 10);
            writer.Write(" RatioPeakAfter="); QuotePadLeft(writer, RatioPeakAfter.ToString("n3"), 5);
            writer.Write(" AllocRateMBSec="); QuotePadLeft(writer, AllocRateMBSec.ToString("n3"), 5);
            writer.Write(" GCDurationMSec="); QuotePadLeft(writer, GCDurationMSec.ToString("n3").ToString(), 10);
            writer.Write(" SuspendDurationMSec="); QuotePadLeft(writer, _SuspendDurationMSec.ToString("n3").ToString(), 10);
            writer.Write(" GCStartRelativeMSec="); QuotePadLeft(writer, GCStartRelativeMSec.ToString("n3"), 10);
            writer.Write(" DurationSinceLastRestartMSec="); QuotePadLeft(writer, DurationSinceLastRestartMSec.ToString("n3"), 5);
            writer.Write(" AllocedSinceLastGC="); QuotePadLeft(writer, AllocedSinceLastGCMB.ToString("n3"), 5);
            writer.Write(" Type="); QuotePadLeft(writer, Type.ToString(), 18);
            writer.Write(" Reason="); QuotePadLeft(writer, Reason.ToString(), 27);
            writer.WriteLine(">");
            if (HeapStats != null)
            {
                writer.Write("      <HeapStats");
                writer.Write(" GenerationSize0=\"{0:n0}\"", HeapStats.GenerationSize0);
                writer.Write(" TotalPromotedSize0=\"{0:n0}\"", HeapStats.TotalPromotedSize0);
                writer.Write(" GenerationSize1=\"{0:n0}\"", HeapStats.GenerationSize1);
                writer.Write(" TotalPromotedSize1=\"{0:n0}\"", HeapStats.TotalPromotedSize1);
                writer.Write(" GenerationSize2=\"{0:n0}\"", HeapStats.GenerationSize2);
                writer.Write(" TotalPromotedSize2=\"{0:n0}\"", HeapStats.TotalPromotedSize2);
                writer.Write(" GenerationSize3=\"{0:n0}\"", HeapStats.GenerationSize3);
                writer.Write(" TotalPromotedSize3=\"{0:n0}\"", HeapStats.TotalPromotedSize3);
                writer.Write(" FinalizationPromotedSize=\"{0:n0}\"", HeapStats.FinalizationPromotedSize);
                writer.Write(" FinalizationPromotedCount=\"{0:n0}\"", HeapStats.FinalizationPromotedCount);
                writer.Write(" PinnedObjectCount=\"{0:n0}\"", HeapStats.PinnedObjectCount);
                writer.Write(" SinkBlockCount=\"{0:n0}\"", HeapStats.SinkBlockCount);
                writer.Write(" GCHandleCount=\"{0:n0}\"", HeapStats.GCHandleCount);
                writer.WriteLine("/>");
            }
            if (GlobalHeapHistory != null)
            {
                writer.Write("      <GlobalHeapHistory");
                writer.Write(" FinalYoungestDesired=\"{0:n0}\"", GlobalHeapHistory.FinalYoungestDesired);
                writer.Write(" NumHeaps=\"{0}\"", GlobalHeapHistory.NumHeaps);
                writer.Write(" CondemnedGeneration=\"{0}\"", GlobalHeapHistory.CondemnedGeneration);
                writer.Write(" Gen0ReductionCount=\"{0:n0}\"", GlobalHeapHistory.Gen0ReductionCount);
                writer.Write(" Reason=\"{0}\"", GlobalHeapHistory.Reason);
                writer.Write(" GlobalMechanisms=\"{0}\"", GlobalHeapHistory.GlobalMechanisms);
                writer.WriteLine("/>");
            }

            if (PerHeapHistories != null)
            {
                writer.WriteLine("      <PerHeapHistories Count=\"{0}\" MemoryLoad=\"{1}\">",
                                 PerHeapHistories.Count,
                                 (GlobalHeapHistory.HasMemoryPressure ? GlobalHeapHistory.MemoryPressure : PerHeapHistories[0].MemoryPressure));
                int HeapNum = 0;
                foreach (var perHeapHistory in PerHeapHistories)
                {
                    writer.Write("      <PerHeapHistory");
#if false // TODO FIX NOW 
                    writer.Write(" MemoryPressure=\"{0:n0}\"", perHeapHistory.MemoryPressure);
                    writer.Write(" MechanismHeapExpand=\"{0}\"", perHeapHistory.MechanismHeapExpand);
                    writer.Write(" MechanismHeapCompact=\"{0}\"", perHeapHistory.MechanismHeapCompact);
                    writer.Write(" InitialGenCondemned=\"{0}\"", perHeapHistory.InitialGenCondemned);
                    writer.Write(" FinalGenCondemned=\"{0}\"", perHeapHistory.FinalGenCondemned);
                    writer.Write(" GenWithExceededBudget=\"{0}\"", perHeapHistory.GenWithExceededBudget);
                    writer.Write(" GenWithTimeTuning=\"{0}\"", perHeapHistory.GenWithTimeTuning);
                    writer.Write(" GenCondemnedReasons=\"{0}\"", perHeapHistory.GenCondemnedReasons);
#endif
                    if ((PerHeapMarkTimes != null) && (PerHeapMarkTimes.ContainsKey(HeapNum)))
                    {
                        MarkInfo mt = PerHeapMarkTimes[HeapNum];

                        if (mt != null)
                        {
                            writer.Write(" MarkStack =\"{0:n3}", mt.MarkTimes[(int)MarkRootType.MarkStack]);
                            if (mt.MarkPromoted != null) writer.Write("({0})", mt.MarkPromoted[(int)MarkRootType.MarkStack]);
                            writer.Write("\" MarkFQ =\"{0:n3}", mt.MarkTimes[(int)MarkRootType.MarkFQ]);
                            if (mt.MarkPromoted != null) writer.Write("({0})", mt.MarkPromoted[(int)MarkRootType.MarkFQ]);
                            writer.Write("\" MarkHandles =\"{0:n3}", mt.MarkTimes[(int)MarkRootType.MarkHandles]);
                            if (mt.MarkPromoted != null) writer.Write("({0})", mt.MarkPromoted[(int)MarkRootType.MarkHandles]);
                            writer.Write("\"");
                            if (GCGeneration != 2)
                            {
                                writer.Write(" MarkOldGen =\"{0:n3}", mt.MarkTimes[(int)MarkRootType.MarkOlder]);
                                if (mt.MarkPromoted != null) writer.Write("({0})", mt.MarkPromoted[(int)MarkRootType.MarkOlder]);
                                writer.Write("\"");
                            }
                        }
                    }
                    else
                    {
                        writer.Write(" DataUnavailable=\"true\"");
                    }
                    writer.WriteLine(">");

                    var sb = new System.Text.StringBuilder();
                    for (var gens = Gens.Gen0; gens <= Gens.GenLargeObj; gens++)
                    {
                        sb.Clear();
                        sb.Append("        ");
                        writer.Write(perHeapHistory.GenData(gens).ToXml(gens, sb).AppendLine().ToString());
                    }

                    writer.Write("      </PerHeapHistory>");
                    HeapNum++;
                }
                writer.WriteLine("      </PerHeapHistories>");
            }
            writer.WriteLine("   </GCEvent>");
        }
        #region private

        internal static void QuotePadLeft(TextWriter writer, string str, int totalSize)
        {
            int spaces = totalSize - 2 - str.Length;
            while (spaces > 0)
            {
                --spaces;
                writer.Write(' ');
            }
            writer.Write('"');
            writer.Write(str);
            writer.Write('"');
        }
        #endregion

    }

    public class CircularBuffer<T> : IEnumerable<T>
        where T : class
    {
        private int StartIndex, AfterEndIndex, Size;
        private T[] Items;
        public CircularBuffer(int size)
        {
            if (size < 1)
                throw new ArgumentException("size");

            StartIndex = 0;
            AfterEndIndex = 0;
            Size = size + 1;
            Items = new T[Size];
        }

        public void Add(T item)
        {
            if (Next(AfterEndIndex) == StartIndex)
            {
                Items[StartIndex] = null;
                StartIndex = Next(StartIndex);
            }
            Items[AfterEndIndex] = item;
            AfterEndIndex = Next(AfterEndIndex);
        }

        private int Next(int i)
        {
            return (i == Size - 1) ? 0 : i + 1;
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = StartIndex; i != AfterEndIndex; i = Next(i))
            {
                yield return Items[i];
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }



}
