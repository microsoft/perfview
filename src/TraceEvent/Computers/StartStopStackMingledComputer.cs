// Copyright (c) Microsoft Corporation.  All rights reserved
// This file is best viewed using outline mode (Ctrl-M Ctrl-O)
//
// This program uses code hyperlinks available as part of the HyperAddin Visual Studio plug-in.
// It is available from http://www.codeplex.com/hyperAddin 
// using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.ApplicationServer;
using Microsoft.Diagnostics.Tracing.Parsers.AspNet;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Session;
using Microsoft.Diagnostics.Tracing.Stacks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using StartStopKey = System.Guid;   // The start-stop key is unique in the trace.  We incorperate the process as well as activity ID to achieve this.

namespace Microsoft.Diagnostics.Tracing
{
    /// <summary>
    /// Calculates start-stop activities (computes duration), Its designed to merge nested start and stop data with call stacks
    /// To do so, it requires the both the start and end events to capture stacks, and to be provided to the Computer at construction.
    /// </summary>
    public unsafe class StartStopStackMingledComputer
    {
        public struct EventUID : IComparable<EventUID>, IEquatable<EventUID>
        {
            public EventUID(TraceEvent evt) : this(evt.EventIndex, evt.TimeStampRelativeMSec) { }

            public EventUID(EventIndex eventId, double time)
            {
                EventId = eventId;
                Time = time;
            }

            public readonly EventIndex EventId;
            public readonly double Time;

            public int CompareTo(EventUID other)
            {
                int timeCompare = Time.CompareTo(other.Time);
                if (timeCompare != 0)
                    return timeCompare;

                return EventId.CompareTo(other.EventId);
            }

            public override int GetHashCode()
            {
                return (int)EventId;
            }

            public override bool Equals(object obj)
            {
                if (!(obj is EventUID))
                    return false;
                return this.CompareTo((EventUID)obj) == 0;
            }

            public bool Equals(EventUID other)
            {
                return this.CompareTo(other) == 0;
            }
        }

        public struct StartStopThreadEventData : IComparable<StartStopThreadEventData>
        {
            public StartStopThreadEventData(EventUID start, EventUID end, string name)
            {
                Start = start;
                End = end;
                Name = name;
                OutputStacks = default(StackSwap);
                NameFrame = default(StackSourceFrameIndex);
            }

            public EventUID Start;
            public EventUID End;
            public StackSwap OutputStacks;
            public string Name;
            public StackSourceFrameIndex NameFrame;

            int IComparable<StartStopThreadEventData>.CompareTo(StartStopThreadEventData other)
            {
                return Start.CompareTo(other.Start);
            }
        }

        public struct StackSwap
        {
            public StackSourceCallStackIndex OriginalStack;
            public StackSourceCallStackIndex ReplacementStack;
        }

        Dictionary<int, Stack<StackSwap>> _currentStackSwap = new Dictionary<int, Stack<StackSwap>>();

        class PerThreadStartStopData
        {
            public int Offset;

            public StartStopThreadEventData[] Data;
            public StartStopThreadEventData[] SplitUpData;
            public double[] SplitUpDataStarts;
        }

        Dictionary<int, PerThreadStartStopData> _startStopData = new Dictionary<int, PerThreadStartStopData>();
        Dictionary<StackSourceFrameIndex, TraceThread> _stackFrameToThread = new Dictionary<StackSourceFrameIndex, TraceThread>();

        MutableTraceEventStackSource _outputStackSource;
        MutableTraceEventStackSource _inputStackSource;
        TraceLogEventSource _eventSource;

        public StartStopStackMingledComputer(MutableTraceEventStackSource outputStackSource, MutableTraceEventStackSource inputStackSource, TraceLogEventSource source, Dictionary<int, List<StartStopThreadEventData>> perThreadEventStartAndStop)
        {
            _outputStackSource = outputStackSource;
            _inputStackSource = inputStackSource;
            _eventSource = source;

            HashSet<EventUID> interestingEvents = new HashSet<EventUID>();
            foreach (var entry in perThreadEventStartAndStop)
            {
                StartStopThreadEventData[] data = entry.Value.ToArray();
                Array.Sort(data);
                PerThreadStartStopData perThread = new PerThreadStartStopData();
                perThread.Data = data;
                for (int i = 0; i < data.Length; i++)
                {
                    data[i].NameFrame = outputStackSource.Interner.FrameIntern(data[i].Name);
                    interestingEvents.Add(data[i].Start);
                    interestingEvents.Add(data[i].End);
                }
                _startStopData.Add(entry.Key, perThread);
            }

            Dictionary<EventUID, StackSourceCallStackIndex> _interestingEventToCallStackOutput = new Dictionary<EventUID, StackSourceCallStackIndex>();
            Dictionary<EventUID, StackSourceCallStackIndex> _interestingEventToCallStackInput = new Dictionary<EventUID, StackSourceCallStackIndex>();

            Action<TraceEvent> allEvents = (TraceEvent evt) =>
            {
                EventUID uid = new EventUID(evt);
                if (interestingEvents.Contains(uid))
                {
                    lock (_interestingEventToCallStackOutput)
                    {
                        _interestingEventToCallStackOutput.Add(uid, _outputStackSource.GetCallStack(evt.CallStackIndex(), evt));
                        _interestingEventToCallStackInput.Add(uid, _inputStackSource.GetCallStack(evt.CallStackIndex(), evt));
                    }
                }
            };

            source.AllEvents += allEvents;
            source.Process();
            source.AllEvents -= allEvents;

            foreach (var thread in source.TraceLog.Threads)
            {
                var callStackWithThread = outputStackSource.GetCallStackForThread(thread);
                var threadFrame = inputStackSource.GetFrameIndex(callStackWithThread);
                _stackFrameToThread.Add(threadFrame, thread);
            }

            // Compute all the output stacks to replace
            foreach (var entry in _startStopData)
            {
                for (int i = 0; i < entry.Value.Data.Length; i++)
                {
                    var startStop = entry.Value.Data[i];
                    List<StackSourceCallStackIndex> startStackIndices = StackIndicesOfCallStack(outputStackSource, _interestingEventToCallStackOutput[startStop.Start]);
                    List<StackSourceCallStackIndex> endStackIndices = StackIndicesOfCallStack(outputStackSource, _interestingEventToCallStackOutput[startStop.End]);

                    int matchCount = Math.Min(startStackIndices.Count, endStackIndices.Count);
                    int lastMatchingIndex;
                    for (lastMatchingIndex = matchCount - 1; lastMatchingIndex > 0; lastMatchingIndex--)
                    {
                        if (startStackIndices[lastMatchingIndex] == endStackIndices[lastMatchingIndex])
                            break; // We found matching in the stack walk
                    }

                    startStop.OutputStacks.OriginalStack = startStackIndices[lastMatchingIndex];
                    entry.Value.Data[i] = startStop;
                }
            }

            foreach (var entry in _startStopData)
            {
                List<StartStopThreadEventData> splitUpStartStopData = new List<StartStopThreadEventData>();

                Stack<StartStopThreadEventData> currentPerThreadProcessingState = new Stack<StartStopThreadEventData>();
                for (int i = 0; i < entry.Value.Data.Length; i++)
                {
                    var startStop = entry.Value.Data[i];

                    if (currentPerThreadProcessingState.Count > 0)
                    {
                        while ((currentPerThreadProcessingState.Count > 0) && (currentPerThreadProcessingState.Peek().End.CompareTo(startStop.Start) < 0))
                        {
                            // Current stack top event finished before this event happened.
                            var poppedEvent = currentPerThreadProcessingState.Pop();
                            EventUID lastEventProcessedEnd = poppedEvent.End;
                            if (currentPerThreadProcessingState.Count > 0)
                            {
                                var tempPoppedEvent = currentPerThreadProcessingState.Pop();
                                tempPoppedEvent.Start = lastEventProcessedEnd;
                                splitUpStartStopData.Add(tempPoppedEvent);
                                currentPerThreadProcessingState.Push(tempPoppedEvent);
                            }
                        }
                    }

                    StackSourceCallStackIndex callStackWithReplacedDetailsFromHierarchy;
                    if (currentPerThreadProcessingState.Count == 0)
                    {
                        callStackWithReplacedDetailsFromHierarchy = startStop.OutputStacks.OriginalStack;
                    }
                    else
                    {
                        callStackWithReplacedDetailsFromHierarchy = MergeInCurrentOverrideStack(startStop.OutputStacks.OriginalStack, currentPerThreadProcessingState.Peek().OutputStacks);
                    }
                    startStop.OutputStacks.ReplacementStack = _outputStackSource.Interner.CallStackIntern(startStop.NameFrame, callStackWithReplacedDetailsFromHierarchy);
                    splitUpStartStopData.Add(startStop);
                    currentPerThreadProcessingState.Push(startStop);
                }
                entry.Value.SplitUpData = splitUpStartStopData.ToArray();
                entry.Value.SplitUpDataStarts = new double[entry.Value.SplitUpData.Length];
                for (int i = 0; i < entry.Value.SplitUpDataStarts.Length; i++)
                {
                    entry.Value.SplitUpDataStarts[i] = entry.Value.SplitUpData[i].Start.Time;
                }
            }

            StackSourceSample outputSample = new StackSourceSample(outputStackSource);

            inputStackSource.ForEach((StackSourceSample sample) =>
            {
                var stackInOutputWorld = MapFromInputStackSampleToOutputStackSample(sample.StackIndex, out int threadID);

                outputSample.Count = sample.Count;
                outputSample.Metric = 1;// sample.Metric;
                outputSample.SampleIndex = sample.SampleIndex;
                outputSample.Scenario = sample.Scenario;
                outputSample.TimeRelativeMSec = sample.TimeRelativeMSec;
                outputSample.StackIndex = stackInOutputWorld;

                if (_startStopData.TryGetValue(threadID, out var perThreadStartStop))
                {
                    int interestingIndex = Array.BinarySearch(perThreadStartStop.SplitUpDataStarts, sample.TimeRelativeMSec);
                    if (interestingIndex > 0)
                    {
                        // roll forward until interestingIndex is past exact matches
                        while (interestingIndex < perThreadStartStop.SplitUpDataStarts.Length && perThreadStartStop.SplitUpDataStarts[interestingIndex] == sample.TimeRelativeMSec)
                        {
                            interestingIndex++;
                        }
                    }
                    else
                    {
                        interestingIndex = ~interestingIndex;
                    }

                    if (interestingIndex == 0)
                    {
                        // Nothing interesting found...
                        outputSample.StackIndex = stackInOutputWorld;
                    }
                    else
                    {
                        interestingIndex--;
                        if (perThreadStartStop.SplitUpData[interestingIndex].End.Time > sample.TimeRelativeMSec)
                        {
                            outputSample.StackIndex = MergeInCurrentOverrideStack(stackInOutputWorld, perThreadStartStop.SplitUpData[interestingIndex].OutputStacks);
                        }
                        else
                        {
                            // Nothing interesting found...
                            outputSample.StackIndex = stackInOutputWorld;
                        }
                    }
                }

                outputStackSource.AddSample(outputSample);
            });

            outputStackSource.Interner.DoneInterning();
            outputStackSource.DoneAddingSamples();
        }

        private List<StackSourceCallStackIndex> StackIndicesOfCallStack(TraceEventStackSource stackSource, StackSourceCallStackIndex callStack)
        {
            List<StackSourceCallStackIndex> stackIndices = new List<StackSourceCallStackIndex>();

            while (callStack != StackSourceCallStackIndex.Invalid)
            {
                stackIndices.Add(callStack);
                callStack = stackSource.GetCallerIndex(callStack);
            }

            stackIndices.Add(StackSourceCallStackIndex.Invalid);
            stackIndices.Reverse();
            return stackIndices;
        }


        private List<StackSourceFrameIndex> StackFrameIndicesOfCallStack(TraceEventStackSource stackSource, StackSourceCallStackIndex callStack)
        {
            List<StackSourceFrameIndex> stackIndices = new List<StackSourceFrameIndex>();
            foreach (var index in StackIndicesOfCallStack(stackSource, callStack))
            {
                if (index != StackSourceCallStackIndex.Invalid)
                {
                    stackIndices.Add(stackSource.GetFrameIndex(index));
                }
            }

            return stackIndices;
        }

        private string StringOfStack(TraceEventStackSource stackSource, StackSourceCallStackIndex callStack)
        {
            StringBuilder output = new StringBuilder();
            foreach (StackSourceFrameIndex stackIndex in StackFrameIndicesOfCallStack(stackSource, callStack))
            {
                output.Append($"->{stackSource.GetFrameName(stackIndex, false)}");
            }
            return output.ToString();
        }

        private TraceThread GetThreadForInputStack(StackSourceCallStackIndex callStack)
        {
            StackSourceFrameIndex currentFrame;

            while (callStack != StackSourceCallStackIndex.Invalid)
            {
                currentFrame = _inputStackSource.Interner.GetFrameIndex(callStack);
                if (_stackFrameToThread.ContainsKey(currentFrame))
                {
                    return _stackFrameToThread[currentFrame];
                }
                callStack = _inputStackSource.Interner.GetCallerIndex(callStack);
            }

            return null;
        }

        private struct OutputStackData
        {
            public OutputStackData(int threadId, StackSourceCallStackIndex outputStack)
            {
                ThreadId = threadId;
                OutputStack = outputStack;
            }

            public readonly int ThreadId;
            public readonly StackSourceCallStackIndex OutputStack;
        }

        Dictionary<StackSourceCallStackIndex, OutputStackData> _stackMapping = new Dictionary<StackSourceCallStackIndex, OutputStackData>();

        private StackSourceCallStackIndex MapFromInputStackSampleToOutputStackSample(StackSourceCallStackIndex inputStack, out int threadId)
        {
            threadId = -1;
            if (inputStack == StackSourceCallStackIndex.Invalid)
                return inputStack;

            if (_stackMapping.TryGetValue(inputStack, out OutputStackData result))
            {
                threadId = result.ThreadId;
                return result.OutputStack;
            }

            StackSourceCallStackIndex outputStack;
            var currentFrame = _inputStackSource.GetFrameIndex(inputStack);
            if (_stackFrameToThread.TryGetValue(currentFrame, out var thread))
            {
                threadId = thread.ThreadID;
                outputStack = inputStack;
            }
            else
            {
                StackSourceCallStackIndex callerOutput = MapFromInputStackSampleToOutputStackSample(_inputStackSource.GetCallerIndex(inputStack), out threadId);
                if (inputStack < _outputStackSource.Interner.CallStackStartIndex)
                {
                    outputStack = inputStack;
                }
                else
                {
                    StackSourceFrameIndex currentFrameOutput;
                    if (currentFrame < _outputStackSource.Interner.FrameStartIndex)
                    {
                        currentFrameOutput = currentFrame;
                    }
                    else
                    {
                        string frameName = _inputStackSource.GetFrameName(currentFrame, false);
                        currentFrameOutput = _outputStackSource.Interner.FrameIntern(frameName);
                    }

                    outputStack = _outputStackSource.Interner.CallStackIntern(currentFrameOutput, callerOutput);
                }
            }
            _stackMapping.Add(inputStack, new OutputStackData(threadId, outputStack));
            return outputStack;
        }

        private StackSourceCallStackIndex MergeInCurrentOverrideStack(StackSourceCallStackIndex callStack, StackSwap swap)
        {
            if (callStack == StackSourceCallStackIndex.Invalid)
                return callStack;

            if (callStack == swap.OriginalStack)
                return swap.ReplacementStack;

            StackSourceCallStackIndex originalCaller = _outputStackSource.GetCallerIndex(callStack);
            StackSourceCallStackIndex caller = MergeInCurrentOverrideStack(originalCaller, swap);

            if (originalCaller == caller)
                return callStack;

            var currentFrame = _outputStackSource.GetFrameIndex(callStack);
            return _outputStackSource.Interner.CallStackIntern(currentFrame, caller);
        }
    }
}
