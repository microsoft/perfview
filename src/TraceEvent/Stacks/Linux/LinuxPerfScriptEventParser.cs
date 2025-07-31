﻿using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using OptimizationTier = Microsoft.Diagnostics.Tracing.Parsers.Clr.OptimizationTier;

namespace Microsoft.Diagnostics.Tracing.StackSources
{
    public class LinuxPerfScriptEventParser
    {
        public LinuxPerfScriptEventParser()
        {
            mapper = null;
            SetDefaultValues();
        }

        /// <summary>
        /// Gets an estimated total number of samples created - not thread safe.
        /// </summary>
        public int EventCount { get; private set; }

        /// <summary>
        /// Tries to skip the byte order marks at the beginning of the given fast stream.
        /// </summary>
        public void SkipPreamble(FastStream source)
        {
            source.MoveNext();      // Prime Current 

            // These are bytes put at the beginning of a UTF8 file (like the byte order mark (BOM)) that should be skipped.  
            var preambleBytes = Encoding.UTF8.GetPreamble();
            while (preambleBytes.Contains(source.Current))
            {
                source.MoveNext();
            }

            // Skip whitespace and comments.   
            for (; ; )
            {
                if (Char.IsWhiteSpace((char)source.Current))
                {
                    source.MoveNext();
                }
                else if (source.Current == '#')
                {
                    source.SkipUpTo('\n');
                }
                else
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Parses the given Linux sample data, returning one sample at a time, and
        /// automatically skips the BOM at the beginning of files.
        /// </summary>
        public IEnumerable<LinuxEvent> ParseSkippingPreamble(string filename)
        {
            return ParseSkippingPreamble(new FastStream(filename));
        }

        public IEnumerable<LinuxEvent> ParseSkippingPreamble(Stream stream)
        {
            return ParseSkippingPreamble(new FastStream(stream));
        }

        public IEnumerable<LinuxEvent> ParseSkippingPreamble(FastStream source)
        {
            SkipPreamble(source);

            return Parse(source);
        }

        /// <summary>
        /// Parse the given Linux sample data, returning one sample at a time, does not try to
        /// skip through the BOM.
        /// </summary>
        public IEnumerable<LinuxEvent> Parse(FastStream source)
        {
            if (source.Current == 0 && !source.EndOfStream)
            {
                source.MoveNext();
            }

            Regex rgx = Pattern;
            foreach (LinuxEvent linuxEvent in NextEvent(rgx, source))
            {
                if (linuxEvent != null)
                {
                    EventCount++; // Needs to be thread safe
                    yield return linuxEvent;
                }

                if (EventCount > MaxSamples)
                {
                    break;
                }
            }

            yield break;
        }

        /// <summary>
        /// Regex string pattern for filtering events.
        /// </summary>
        public Regex Pattern { get; set; }

        /// <summary>
        /// The amount of samples the parser takes.
        /// </summary>
        public long MaxSamples { get; set; }

        /// <summary>
        /// Uses the archive as a resource for symbol resolution when parsing Linux samples.
        /// </summary>
        public void SetSymbolFile(ZipArchive archive)
        {
            mapper = new LinuxPerfScriptMapper(archive, this);
        }

        /// <summary>
        /// Uses the path to open an archive with symbol files that are then used for symbol resolution when
        /// parsing Linux samples.
        /// </summary>
        public void SetSymbolFile(string path)
        {
            SetSymbolFile(ZipFile.OpenRead(path));
        }

        /// <summary>
        /// Parses a Microsoft symbol as shown on the Linux sample. "entireSymbol" represents the module contract between
        /// the memory address and the dll path on the Linux sample.
        /// "mapFileLocation" is the path to the dll given by the Linux sample.
        /// </summary>
        public string[] GetSymbolFromMicrosoftMap(string entireSymbol, string mapFileLocation = "")
        {
            for (int first = 0; first < entireSymbol.Length;)
            {
                int last = entireSymbol.IndexOf(' ', first);
                if (last == -1)
                {
                    last = entireSymbol.Length;
                }

                if (entireSymbol[first] == '[' && entireSymbol[last - 1] == ']')
                {
                    var symbol = entireSymbol.Substring(Math.Min(entireSymbol.Length, last + 1));
                    return new string[2] { entireSymbol.Substring(first + 1, last - first - 2), symbol.Trim() };
                }

                first = last + 1;
            }

            return new string[2] { entireSymbol, mapFileLocation };
        }

        public bool IsEndOfSample(FastStream source)
        {
            return IsEndOfSample(source, source.Current, source.Peek(1));
        }

        public bool IsEndOfSample(FastStream source, byte current, byte peek1)
        {
            return (current == '\n' && peek1 != '\t') || current == 0 || source.EndOfStream;
        }

        /// <summary>
        /// Given a stream with the symbols, this function parses the stream and stores the contents in the given mapper
        /// </summary>
        public void ParseSymbolFile(Stream stream, Mapper mapper)
        {
            FastStream source = new FastStream(stream);
            source.MoveNext(); // Prime Current.  
            SkipPreamble(source); // Remove encoding stuff if it's there
            source.SkipWhiteSpace();

            StringBuilder sb = new StringBuilder();

            Func<byte, bool> untilWhiteSpace = (byte c) => { return !char.IsWhiteSpace((char)c); };

            while (!source.EndOfStream)
            {
                source.ReadAsciiStringUpToTrue(sb, untilWhiteSpace);
                ulong start = ulong.Parse(sb.ToString(), System.Globalization.NumberStyles.HexNumber);
                sb.Clear();
                source.SkipWhiteSpace();

                source.ReadAsciiStringUpToTrue(sb, untilWhiteSpace);
                ulong size = ulong.Parse(sb.ToString(), System.Globalization.NumberStyles.HexNumber);
                sb.Clear();
                source.SkipWhiteSpace();

                source.ReadAsciiStringUpTo('\n', sb);
                string symbol = sb.ToString().TrimEnd();
                sb.Clear();

                mapper.Add(start, size, symbol);

                source.SkipWhiteSpace();
            }
        }

        /// <summary>
        /// Given a stream that contains PerfInfo commands, parses the stream and stores data in the given dictionary.
        /// Key: somedll.ni.dll		Value: {some guid}
        /// </summary>
        public void ParsePerfInfoFile(Stream stream, Dictionary<string, string> guids, Dictionary<string, ulong> baseAddresses)
        {
            FastStream source = new FastStream(stream);
            source.MoveNext();
            source.SkipWhiteSpace();

            StringBuilder sb = new StringBuilder();

            while (!source.EndOfStream)
            {
                source.ReadAsciiStringUpTo(';', sb);
                source.MoveNext();
                string command = sb.ToString();
                sb.Clear();

                if (command == "ImageLoad") // TODO: should be a constant maybe?
                {
                    source.ReadAsciiStringUpTo(';', sb);
                    string path = sb.ToString();
                    sb.Clear();
                    source.MoveNext();

                    source.ReadAsciiStringUpTo(';', sb);
                    string guid = sb.ToString().TrimEnd();
                    sb.Clear();
                    source.MoveNext();

                    guids[GetFileName(path)] = guid;

                    // Check to see if the base address has been appended to the line.
                    if (source.Current != '\n')
                    {
                        sb.Clear();
                        source.ReadAsciiStringUpTo(';', sb);
                        string strBaseAddr = sb.ToString().TrimEnd();
                        if (!string.IsNullOrEmpty(strBaseAddr))
                        {
                            ulong baseAddr = ulong.Parse(strBaseAddr, System.Globalization.NumberStyles.HexNumber);
                            baseAddresses[GetFileName(path)] = baseAddr;
                        }
                    }
                }

                source.SkipUpTo('\n');
                source.MoveNext();
            }
        }

        #region private

        /// <summary>
        /// Can't use Path.GetFileName because it fails on illegal Linux file characters.  
        /// Can remove when this changes. 
        /// </summary>
        internal static string GetFileName(string path)
        {
            var index = path.LastIndexOfAny(pathSeparators);
            if (index < 0)
            {
                return path;
            }

            return path.Substring(index + 1);
        }

        private static char[] pathSeparators = new char[] { '/', '\\' };
        private const string NISuffix = ".ni.";

        internal static string GetFileNameWithoutExtension(string path, bool stripNiSuffix)
        {
            var start = path.LastIndexOfAny(pathSeparators);
            if (start < 0)
            {
                start = 0;
            }
            else
            {
                start++;
            }

            var end = path.LastIndexOf('.');
            if (end < start)
            {
                end = path.Length;
            }

            var name = path.Substring(start, end - start);

            if (stripNiSuffix)
            {
                var suffixStart = name.IndexOf(NISuffix);
                var suffixEnd = suffixStart + NISuffix.Length - 1; // Leave the trailing '.' from ".ni."
                if ((suffixStart >= 0) && (suffixEnd < name.Length))
                {
                    var first = name.Substring(0, suffixStart);
                    var second = name.Substring(suffixEnd, name.Length - suffixEnd);
                    name = first + second;
                }
            }

            return name;
        }


        private void SetDefaultValues()
        {
            EventCount = 0;
            Pattern = null;
            MaxSamples = long.MaxValue;
        }

        private IEnumerable<LinuxEvent> NextEvent(Regex regex, FastStream source)
        {
            StringBuilder sb = new StringBuilder();
            string line = string.Empty;

            while (true)
            {
                source.SkipWhiteSpace();

                if (source.EndOfStream)
                    break;

                // Fetch Command (processName) - Stops when it sees the pattern \s+\d+/\d
                int idx = FindEndOfProcessCommand(source);
                if (idx < 0)
                {
                    break;
                }

                source.ReadFixedString(idx, sb);
                source.SkipWhiteSpace();
                string processCommand = sb.ToString();
                sb.Clear();

                // Process ID
                int pid = source.ReadInt();

                // Detect whether or not the Thread ID is present.
                int tid = pid;
                if (source.Peek(0) == '/')
                {
                    // Thread ID
                    source.MoveNext(); // Move past the "/"
                    tid = source.ReadInt();
                }

                // CPU
                source.SkipWhiteSpace();
                int cpu = -1;
                if (source.Peek(0) == '[')
                {
                    source.MoveNext(); // Move past the "["
                    cpu = source.ReadInt();
                    source.MoveNext(); // Move past the "]"
                }

                // Time
                source.SkipWhiteSpace();
                source.ReadAsciiStringUpTo(':', sb);

                double time = double.Parse(sb.ToString(), CultureInfo.InvariantCulture) * 1000; // To convert to MSec
                sb.Clear();
                source.MoveNext(); // Move past ":"

                // Time Property
                source.SkipWhiteSpace();
                int timeProp = -1;
                if (IsNumberChar((char)source.Current))
                {
                    timeProp = source.ReadInt();
                }

                // Event Name
                source.SkipWhiteSpace();
                source.ReadAsciiStringUpTo(':', sb);
                string eventName = sb.ToString();
                sb.Clear();
                source.MoveNext();

                // The event can end in the format /custom_param_list/:
                // Detect this and remove from the event name
                if (eventName.Length != 0 && eventName[eventName.Length - 1] == '/')
                {
                    int firstSlash = eventName.IndexOf('/');

                    if (firstSlash != eventName.Length - 1)
                    {
                        eventName = eventName.Substring(0, firstSlash);
                    }
                }

                // Now that we know the header of the trace, we can decide whether or not to skip it given our pattern
                if (regex != null && !regex.IsMatch(eventName))
                {
                    while (true)
                    {
                        if (IsEndOfSample(source, source.Current, source.Peek(1)))
                        {
                            break;
                        }
                        source.MoveNext();
                    }

                    yield return null;
                }
                else
                {
                    LinuxEvent linuxEvent;

                    Frame threadTimeFrame = null;

                    // Event Properties
                    // I mark a position here because I need to check what type of event this is without screwing up the stream
                    var markedPosition = source.MarkPosition();
                    source.ReadAsciiStringUpTo('\n', sb);
                    string eventDetails = sb.ToString().Trim();
                    sb.Clear();

                    // Go back to the event so we can process it
                    source.RestoreToMark(markedPosition);
                    if (eventDetails.Length >= SchedulerEvent.Name.Length && eventDetails.Substring(0, SchedulerEvent.Name.Length) == SchedulerEvent.Name)
                    {
                        ScheduleSwitch schedSwitch = ReadScheduleSwitch(source);
                        IEnumerable<Frame> frames = ReadFramesForSample(processCommand, pid, tid, threadTimeFrame, source);
                        linuxEvent = new SchedulerEvent(processCommand, tid, pid, time, timeProp, cpu, eventName, eventDetails, frames, schedSwitch);
                    }
                    else if (eventDetails.Length >= ProcessExecEvent.Name.Length && eventDetails.Substring(0, ProcessExecEvent.Name.Length) == ProcessExecEvent.Name)
                    {
                        ProcessExec processExec = ReadProcessExec(source);
                        IEnumerable<Frame> frames = ReadFramesForSample(processCommand, pid, tid, threadTimeFrame, source);
                        linuxEvent = new ProcessExecEvent(processCommand, tid, pid, time, timeProp, cpu, eventName, eventDetails, frames, processExec);
                    }
                    else if (eventDetails.Length > ThreadExitEvent.Name.Length && eventDetails.Substring(0, ThreadExitEvent.Name.Length) == ThreadExitEvent.Name)
                    {
                        ThreadExit exit = ReadExit(source);
                        IEnumerable<Frame> frames = ReadFramesForSample(processCommand, pid, tid, threadTimeFrame, source);
                        linuxEvent = new ThreadExitEvent(processCommand, tid, pid, time, timeProp, cpu, eventName, eventDetails, frames, exit);
                    }
                    else if (eventDetails.Length > BlockReqIssueEvent.Name.Length && eventDetails.Substring(0, BlockReqIssueEvent.Name.Length) == BlockReqIssueEvent.Name)
                    {
                        BlockReqIssue blockReqIssue = ReadBlockReqIssue(source);
                        IEnumerable<Frame> frames = ReadFramesForSample(processCommand, pid, tid, threadTimeFrame, source);
                        linuxEvent = new BlockReqIssueEvent(processCommand, tid, pid, time, timeProp, cpu, eventName, eventDetails, frames, blockReqIssue);
                    }
                    else if (eventDetails.Length > BlockReqCompleteEvent.Name.Length && eventDetails.Substring(0, BlockReqCompleteEvent.Name.Length) == BlockReqCompleteEvent.Name)
                    {
                        BlockReqComplete blockReqComplete = ReadBlockReqComplete(source);
                        IEnumerable<Frame> frames = ReadFramesForSample(processCommand, pid, tid, threadTimeFrame, source);
                        linuxEvent = new BlockReqCompleteEvent(processCommand, tid, pid, time, timeProp, cpu, eventName, eventDetails, frames, blockReqComplete);
                    }
                    else if (eventDetails.Length > SchedWakeupEvent.Name.Length && eventDetails.Substring(0, SchedWakeupEvent.Name.Length) == SchedWakeupEvent.Name)
                    {
                        SchedWakeup schedWakeup = ReadSchedWakeup(source);
                        IEnumerable<Frame> frames = ReadFramesForSample(processCommand, pid, tid, threadTimeFrame, source);
                        linuxEvent = new SchedWakeupEvent(processCommand, tid, pid, time, timeProp, cpu, eventName, eventDetails, frames, schedWakeup);
                    }
                    else if (eventDetails.Length > IrqEnterEvent.Name.Length && eventDetails.Substring(0, IrqEnterEvent.Name.Length) == IrqEnterEvent.Name)
                    {
                        IrqEnter irqEnter = ReadIrqEnter(source);
                        IEnumerable<Frame> frames = ReadFramesForSample(processCommand, pid, tid, threadTimeFrame, source);
                        linuxEvent = new IrqEnterEvent(processCommand, tid, pid, time, timeProp, cpu, eventName, eventDetails, frames, irqEnter);
                    }
                    else if (eventDetails.Length > IrqExitEvent.Name.Length && eventDetails.Substring(0, IrqExitEvent.Name.Length) == IrqExitEvent.Name)
                    {
                        IrqExit irqExit = ReadIrqExit(source);
                        IEnumerable<Frame> frames = ReadFramesForSample(processCommand, pid, tid, threadTimeFrame, source);
                        linuxEvent = new IrqExitEvent(processCommand, tid, pid, time, timeProp, cpu, eventName, eventDetails, frames, irqExit);
                    }
                    else
                    {
                        source.ReadAsciiStringUpTo('\n', sb);
                        sb.Clear();
                        IEnumerable<Frame> frames = ReadFramesForSample(processCommand, pid, tid, threadTimeFrame, source);
                        linuxEvent = new CpuEvent(processCommand, tid, pid, time, timeProp, cpu, eventName, eventDetails, frames);
                    }

                    yield return linuxEvent;
                }
            }
        }


        /// <summary>
        /// This routine should be called at the start of a line after you have skipped whitespace.
        /// 
        /// Logically a line starts with PROCESS_COMMAND PID/TID [CPU] TIME: 
        /// 
        /// However PROCESS_COMMAND is unfortunately free form, including the fact hat it can have / or numbers in it.   
        /// For example here is a real PROCESS_COMMAND examples (rs:action 13 qu) or  (kworker/1:3)
        /// Thus it gets tricky to know when the command stops and the PID/TID starts.
        /// 
        /// We use the following regular expression to determine the end of the command
        /// 
        ///       \s*\d+/\d        OR
        ///       ^\d+/\d          THIS PATTERN IS NEEDED BECAUSE THE PROCESS_COMMAND MAY BE EMPTY.  
        ///
        /// This routine peeks forward looking for this pattern, and returns either the index to the start of it or -1 if not found.  
        /// </summary>
        private static int FindEndOfProcessCommand(FastStream source)
        {
            uint idx = 0;

            char zeroVal = (char)source.Peek(0);

        startOver:
            int firstSpaceIdx = -1;
            bool seenDigit = false;

            // Deal with the case where the COMMAND is empty.
            // Thus we have ANY spaces before the proceed ID Thread ID Num/Num. 
            // We can deal with this case by 'jump starting the state machine state if it starts with a digit.   
            if (char.IsDigit(zeroVal))
            {
                firstSpaceIdx = 0;
                seenDigit = true;
            }

            // Handle the case where the PROCESS_COMMAND is something like :-1 or -1
            else if (zeroVal == ':' || zeroVal == '-')
            {
                for (; ; idx++)
                {
                    byte val = source.Peek(idx);
                    if (val == '\n')
                    {
                        Debug.Assert(false, "Could not parse process command");
                        return -1;
                    }
                    else if (char.IsWhiteSpace((char)val))
                    {
                        firstSpaceIdx = (int)idx;
                        return firstSpaceIdx;
                    }
                }
            }

            for (; ; )
            {
                idx++;
                if (idx >= source.MaxPeek - 1)
                {
                    return -1;
                }

                byte val = source.Peek(idx);

                if (val == '\n')
                {
                    Debug.Assert(false, "Could not parse process command");
                    return -1;
                }
                if (firstSpaceIdx < 0)
                {
                    if (char.IsWhiteSpace((char)val))
                    {
                        firstSpaceIdx = (int)idx;
                    }
                    else
                    {
                        goto startOver;
                    }
                }
                else if (!seenDigit)
                {
                    if (char.IsDigit((char)val))
                    {
                        seenDigit = true;
                    }
                    else if (!char.IsWhiteSpace((char)val))
                    {
                        goto startOver;
                    }
                }
                else
                {
                    if (val == '/' && char.IsDigit((char)source.Peek(idx + 1)))
                    {
                        return firstSpaceIdx;
                    }
                    else if (!char.IsDigit((char)val))
                    {
                        if (source.Peek(idx + 1) == '[')
                        {
                            return firstSpaceIdx;
                        }
                        else if (char.IsWhiteSpace((char)val))
                        {
                            firstSpaceIdx = (int)idx;
                        }
                        else
                        {
                            goto startOver;
                        }
                    }
                }
            }
        }

        private List<Frame> ReadFramesForSample(string command, int processID, int threadID, Frame threadTimeFrame, FastStream source)
        {
            List<Frame> frames = new List<Frame>();

            if (threadTimeFrame != null)
            {
                frames.Add(threadTimeFrame);
            }

            while (!IsEndOfSample(source, source.Current, source.Peek(1)))
            {
                StackFrame stackFrame = ReadFrame(source);
                if (mapper != null && (stackFrame.Module == "unknown" || stackFrame.Symbol == "unknown"))
                {
                    string[] moduleSymbol = mapper.ResolveSymbols(processID, stackFrame.Module, stackFrame);
                    stackFrame = new StackFrame(stackFrame.Address, moduleSymbol[0], moduleSymbol[1]);
                }
                if (stackFrame.Module.StartsWith("jitted-") && stackFrame.Module.EndsWith(".so") && stackFrame.Symbol.EndsWith(")"))
                {
                    // Jitted or R2R code.  Replace the module with the IL module name, and shorten the symbol.
                    // Example: uint8[] [System.Private.CoreLib] Internal.IO.File::ReadAllBytes(string)
                    // Example: instance uint8[] [System.Private.CoreLib] Internal.IO.File::ReadAllBytes(string)

                    // Start at the end of the string, which should be ')'.  Walk until we find the matching '('.
                    string symbol = stackFrame.Symbol;
                    int currentIndex = symbol.Length - 1;
                    int endIndex = 0;
                    int parenDepth = 0;
                    while (currentIndex >= endIndex)
                    {
                        char current = symbol[currentIndex];
                        if (current == ')')
                        {
                            // We know that we'll immediately increment the paren depth from 0 to 1 on the first loop iteration because
                            // the conditions on the if statement above require it.
                            parenDepth++;
                        }
                        else if (current == '(')
                        {
                            parenDepth--;
                        }

                        if (parenDepth <= 0)
                        {
                            // We found the open paren that matches the last close paren.
                            break;
                        }

                        currentIndex--;
                    }

                    // Continue walking until we find the first whitespace char.  This is the beginning of the full function name (with namespace).
                    while (currentIndex >= endIndex && symbol[currentIndex] != ' ')
                    {
                        currentIndex--;
                    }

                    // Make sure we actually hit a ' ' char.
                    if (symbol[currentIndex] != ' ')
                    {
                        goto abort;
                    }

                    // Save the symbol name.
                    string newSymbol = symbol.Substring(currentIndex + 1, (symbol.Length - currentIndex - 1));

                    // Find the beginning of the module name by looking for ']'.
                    while (currentIndex >= endIndex && symbol[currentIndex] != ']')
                    {
                        currentIndex--;
                    }

                    // Make sure we actually hit a ']' char.
                    if (symbol[currentIndex] != ']')
                    {
                        goto abort;
                    }
                    int moduleEndIndex = currentIndex;

                    // Find the matching '[' char.
                    while (currentIndex >= endIndex && symbol[currentIndex] != '[')
                    {
                        currentIndex--;
                    }

                    // Make sure we actually hit a '[' char.
                    if (symbol[currentIndex] != '[')
                    {
                        goto abort;
                    }

                    // Save the module name.
                    string newModuleName = symbol.Substring(currentIndex + 1, (moduleEndIndex - currentIndex - 1));

                    stackFrame = new StackFrame(stackFrame.Address, newModuleName, newSymbol, stackFrame.OptimizationTier);
                }
            abort:
                frames.Add(stackFrame);
            }

            frames.Add(new ThreadFrame(threadID, "Thread"));
            frames.Add(new ProcessFrame(processID, command));

            return frames;
        }

        private StackFrame ReadFrame(FastStream source)
        {
            StringBuilder sb = new StringBuilder();

            // Address
            source.SkipWhiteSpace();
            source.ReadAsciiStringUpTo(' ', sb);
            string address = sb.ToString();
            sb.Clear();

            // Trying to get the module and symbol...
            source.SkipWhiteSpace();

            source.ReadAsciiStringUpToLastBeforeTrue('(', sb, delegate (byte c)
            {
                if (c != '\n' && !source.EndOfStream)
                {
                    return true;
                }

                return false;
            });
            string assumedSymbol = sb.ToString();
            sb.Clear();

            source.ReadAsciiStringUpTo('\n', sb);

            string assumedModule = sb.ToString();
            sb.Clear();

            assumedModule = RemoveOuterBrackets(assumedModule.Trim());

            string actualModule = assumedModule;
            string actualSymbol = RemoveOuterBrackets(assumedSymbol.Trim());

            if (assumedModule.EndsWith(".map"))
            {
                string[] moduleSymbol = GetSymbolFromMicrosoftMap(assumedSymbol, assumedModule);
                actualSymbol = string.IsNullOrEmpty(moduleSymbol[1]) ? assumedModule : moduleSymbol[1];
                actualModule = moduleSymbol[0];
            }

            // Can't use Path.GetFileName Because it throws on illegal Windows characters 
            actualModule = GetFileName(actualModule);
            actualSymbol = RemoveOffset(actualSymbol.Trim());

            return new StackFrame(address, actualModule, actualSymbol);
        }

        private ScheduleSwitch ReadScheduleSwitch(FastStream source)
        {
            StringBuilder sb = new StringBuilder();

            // There are two formats for ScheduleSwitch serialization:
            // Example1: sched:sched_switch: prev_comm=swapper/3 prev_pid=0 prev_prio=120 prev_state=R ==> next_comm=rcu_sched next_pid=8 next_prio=120
            // Example2: sched:sched_switch: perf_4.9:3005 [49] S ==> swapper/2:0 [120]

            // Skip "sched:sched_switch: "
            source.SkipUpTo(' ');
            source.SkipSpace();

            // Figure out which format we have.
            var pos = source.MarkPosition();

            // Look for 'prev_comm' (Example1)
            source.ReadFixedString(9, sb);
            string nextField = sb.ToString();
            sb.Clear();

            if (nextField.Equals("prev_comm"))
            {
                // This is of the format in Example1.

                source.SkipUpTo('=');
                source.MoveNext();

                ReadProcessNameUntilNextField(source, "prev_pid", sb);
                string prevComm = sb.ToString();
                sb.Clear();

                source.SkipUpTo('=');
                source.MoveNext();

                int prevTid = source.ReadInt();

                source.SkipUpTo('=');
                source.MoveNext();

                int prevPrio = source.ReadInt();

                source.SkipUpTo('=');
                source.MoveNext();

                char prevState = (char)source.Current;

                source.MoveNext();
                source.SkipUpTo('n'); // this is to bypass the ==>
                source.SkipUpTo('=');
                source.MoveNext();

                ReadProcessNameUntilNextField(source, "next_pid", sb);
                string nextComm = sb.ToString();
                sb.Clear();

                source.SkipUpTo('=');
                source.MoveNext();

                int nextTid = source.ReadInt();

                source.SkipUpTo('=');
                source.MoveNext();

                int nextPrio = source.ReadInt();

                return new ScheduleSwitch(prevComm, prevTid, prevPrio, prevState, nextComm, nextTid, nextPrio);
            }
            else
            {
                // This is of the format in Example2.

                // Restore the position back so the full text can be parsed here.
                source.RestoreToMark(pos);

                source.ReadAsciiStringUpTo(':', sb);
                string prevComm = sb.ToString();
                sb.Clear();

                source.MoveNext();

                int prevTid = source.ReadInt();

                source.SkipUpTo('[');
                source.MoveNext();

                int prevPrio = source.ReadInt();

                source.MoveNext();
                source.SkipWhiteSpace();

                char prevState = (char)source.Current;

                source.SkipUpTo('>'); // this is to bypass the ==>
                source.MoveNext();
                source.SkipWhiteSpace();

                source.ReadAsciiStringUpTo(':', sb);
                string nextComm = sb.ToString();
                sb.Clear();

                source.MoveNext();

                int nextTid = source.ReadInt();

                source.SkipUpTo('[');
                source.MoveNext();

                int nextPrio = source.ReadInt();

                return new ScheduleSwitch(prevComm, prevTid, prevPrio, prevState, nextComm, nextTid, nextPrio);
            }
        }
        private ProcessExec ReadProcessExec(FastStream source)
        {
            StringBuilder sb = new StringBuilder();

            // Format is:
            // sched:sched_process_exec: filename=/usr/bin/fio pid=228299 old_pid=228299

            // Skip "sched_process_exec: "
            source.SkipUpTo(' ');
            source.SkipSpace();

            // Skip "filename="
            source.SkipUpTo('=');
            source.MoveNext();

            ReadProcessNameUntilNextField(source, "pid", sb);
            string fileName = sb.ToString();
            sb.Clear();

            // skip "pid="
            source.SkipUpTo('=');
            source.MoveNext();

            int newPid = source.ReadInt();

            // skip "old_pid="
            source.SkipUpTo('=');
            source.MoveNext();

            int oldPid = source.ReadInt();

            return new ProcessExec(fileName, newPid, oldPid);
        }

        private ThreadExit ReadExit(FastStream source)
        {
            StringBuilder sb = new StringBuilder();

            source.SkipUpTo('=');
            source.MoveNext();

            ReadProcessNameUntilNextField(source, "pid", sb);
            string comm = sb.ToString();
            sb.Clear();

            source.SkipUpTo('=');
            source.MoveNext();

            int tid = source.ReadInt();

            source.SkipUpTo('=');
            source.MoveNext();

            int prio = source.ReadInt();

            return new ThreadExit(comm, tid, prio);
        }

        private BlockReqIssue ReadBlockReqIssue(FastStream source)
        {
            StringBuilder sb = new StringBuilder();

            // Format is: block:block_rq_issue: dev_major,dev_minor flags length_bytes (cmd) sector_offset + sector_length [process]

            // Skip "block:block_rq_issue: "
            source.SkipUpTo(' ');
            source.SkipSpace();

            uint device = source.ReadUInt();
            source.MoveNext();      // Skip ','
            uint deviceMinor = source.ReadUInt();

            source.SkipSpace();
            source.ReadAsciiStringUpTo(' ', sb);
            string flags = sb.ToString();
            sb.Clear();

            source.SkipSpace();
            uint length = source.ReadUInt();

            // Skip '(cmd)' for now - it's often empty
            source.SkipSpace();
            source.SkipUpTo(')');
            source.MoveNext();

            source.SkipSpace();
            ulong sector = source.ReadULong();

            source.SkipSpace();
            source.MoveNext();      // Skip '+'
            source.SkipSpace();
            uint sectorLength = source.ReadUInt();

            // Next field is the process name in [] - don't bother with it for now

            source.ReadAsciiStringUpTo('\n', sb);
            sb.Clear();

            return new BlockReqIssue(device, deviceMinor, flags, length, sector, sectorLength);
        }

        private BlockReqComplete ReadBlockReqComplete(FastStream source)
        {
            StringBuilder sb = new StringBuilder();

            // Format is: block:block_rq_complete: dev_major,dev_minor flags (cmd) sector_offset + sector_length [error]

            // Skip "block:block_rq_complete: "
            source.SkipUpTo(' ');
            source.SkipSpace();

            uint device = source.ReadUInt();
            source.MoveNext();      // Skip ','
            uint deviceMinor = source.ReadUInt();

            source.SkipSpace();
            source.ReadAsciiStringUpTo(' ', sb);
            string flags = sb.ToString();
            sb.Clear();

            // Skip '(cmd)' for now - it's often empty
            source.SkipSpace();
            source.SkipUpTo(')');
            source.MoveNext();

            source.SkipSpace();
            ulong sector = source.ReadULong();

            source.SkipSpace();
            source.MoveNext();      // Skip '+'
            source.SkipSpace();
            uint sectorLength = source.ReadUInt();

            source.SkipSpace();
            source.MoveNext();      // Skip '['
            int error = source.ReadInt();

            source.ReadAsciiStringUpTo('\n', sb);
            sb.Clear();

            return new BlockReqComplete(device, deviceMinor, flags, sector, sectorLength, error);
        }

        private SchedWakeup ReadSchedWakeup(FastStream source)
        {
            StringBuilder sb = new StringBuilder();
            string comm;
            int pid;
            int priority;
            int targetCpu;

            // New format2 is: sched:sched_wakeup: %s:%d [%d] CPU:%03d
            // New Format is: sched:sched_wakeup: comm=%s pid=%d prio=%d target_cpu=%03d
            // Old format is: sched:sched_wakeup: task %s:%d [%d] success=%d [%03d]

            // Skip "sched:sched_wakeup: "
            source.SkipUpTo(' ');
            source.SkipSpace();

            // Figure out which format we have.
            var pos = source.MarkPosition();

            source.ReadFixedString(4, sb);
            string nextField = sb.ToString();
            sb.Clear();
            source.RestoreToMark(pos);

            if (nextField == "comm")
            {
                // New format
                source.SkipUpTo('=');
                source.MoveNext();
                source.ReadAsciiStringUpTo(' ', sb);
                comm = sb.ToString();

                source.SkipSpace();
                source.SkipUpTo('=');
                source.MoveNext();
                pid = source.ReadInt();

                source.SkipSpace();
                source.SkipUpTo('=');
                source.MoveNext();
                priority = source.ReadInt();

                source.SkipSpace();
                source.SkipUpTo('=');
                source.MoveNext();
                targetCpu = source.ReadInt();
            }
            else if (nextField == "task")
            {
                // Old format
                source.SkipUpTo(' ');
                source.SkipSpace();
                source.ReadAsciiStringUpTo(':', sb);
                comm = sb.ToString();

                source.MoveNext();
                pid = source.ReadInt();

                source.SkipSpace();
                source.MoveNext();      // skip '['
                priority = source.ReadInt();
                source.MoveNext();      // skip ']'

                source.SkipSpace();
                source.SkipUpTo(' ');   // skip error code
                source.SkipSpace();

                source.MoveNext();      // skip '['
                targetCpu = source.ReadInt();
            }
            else
            {
                // New format 2
                source.ReadAsciiStringUpTo(':', sb);
                comm = sb.ToString();

                source.MoveNext();
                pid = source.ReadInt();

                source.SkipSpace();
                source.MoveNext();      // skip '['
                priority = source.ReadInt();
                source.MoveNext();      // skip ']'

                source.SkipUpTo(':');
                source.MoveNext();      // skip ':'
                targetCpu = source.ReadInt();
            }

            source.ReadAsciiStringUpTo('\n', sb);
            sb.Clear();

            return new SchedWakeup(comm, pid, priority, targetCpu);
        }

        private IrqEnter ReadIrqEnter(FastStream source)
        {
            StringBuilder sb = new StringBuilder();

            // Format is: irq:irq_handler_entry: irq=<vector> name=<name>

            // Skip "irq:irq_handler_entry: "
            source.SkipUpTo(' ');
            source.SkipSpace();

            source.SkipUpTo('=');
            source.MoveNext();      // Skip '='
            uint vector = source.ReadUInt();

            source.SkipUpTo('=');
            source.MoveNext();      // Skip '='
            source.ReadAsciiStringUpTo(' ', sb);
            string name = sb.ToString();
            sb.Clear();

            source.ReadAsciiStringUpTo('\n', sb);
            sb.Clear();

            return new IrqEnter(vector, name);
        }

        private IrqExit ReadIrqExit(FastStream source)
        {
            StringBuilder sb = new StringBuilder();

            // Format is: irq:irq_handler_exit: irq=<vector> ret=<name>

            // Skip "irq:irq_handler_exit: "
            source.SkipUpTo(' ');
            source.SkipSpace();

            source.SkipUpTo('=');
            source.MoveNext();      // Skip '='
            uint vector = source.ReadUInt();

            source.SkipUpTo('=');
            source.MoveNext();      // Skip '='
            source.ReadAsciiStringUpTo(' ', sb);
            string status = sb.ToString();
            sb.Clear();

            source.ReadAsciiStringUpTo('\n', sb);
            sb.Clear();

            return new IrqExit(vector, status);
        }

        // Reads from the current position to the next field name, storing the result in "dest". Looping logic is required because there may be spaces in the field name.
        //
        // Example: "swapper/3 prev_pid=0 prev_prio=120 prev_state=R ==> next_comm=rcu_sched next_pid=8 next_prio=120"
        // With nextField == "prev_pid"
        // Would store "swapper/3" in "dest".
        private void ReadProcessNameUntilNextField(FastStream source, string nextFieldName, StringBuilder dest)
        {
            StringBuilder fieldNameStringBuilder = new StringBuilder();

            while (true)
            {
                source.ReadAsciiStringUpTo(' ', dest);
                source.MoveNext();

                // Check to see if we've hit the next field name.
                FastStream.MarkedPosition pos = source.MarkPosition();
                source.ReadFixedString(nextFieldName.Length, fieldNameStringBuilder);

                if (nextFieldName.Equals(fieldNameStringBuilder.ToString()))
                {
                    break;
                }
                else
                {
                    source.RestoreToMark(pos);
                    fieldNameStringBuilder.Clear();
                    dest.Append(' ');
                }
            }
        }

        private string RemoveOuterBrackets(string s)
        {
            if (s.Length < 1)
            {
                return s;
            }
            while ((s[0] == '(' && s[s.Length - 1] == ')')
                || (s[0] == '[' && s[s.Length - 1] == ']'))
            {
                s = s.Substring(1, s.Length - 2);
            }

            return s;
        }

        private string RemoveOffset(string s)
        {
            // Perf stack entries look like func+0xFFFFFFFFFFFFFFFF.
            // Strip off the +0xFFFFFFFFFFFFFFFF so that PerfView can aggregate the stacks properly.

            const string offsetPrefix = "+0x";
            int offsetPrefixLength = offsetPrefix.Length;


            // If the offset prefix is found and is not the beginning or end of the frame, then remove the offset.
            int index = s.LastIndexOf(offsetPrefix);
            if ((index > 0) && (index < s.Length - offsetPrefixLength))
            {
                return s.Substring(0, index);
            }

            return s;
        }

        private bool IsNumberChar(char c)
        {
            switch (c)
            {
                case '1':
                case '2':
                case '3':
                case '4':
                case '5':
                case '6':
                case '7':
                case '8':
                case '9':
                case '0':
                    return true;
            }

            return false;
        }

        private LinuxPerfScriptMapper mapper;
        #endregion
    }

    #region Mapper
    public class LinuxPerfScriptMapper
    {
        public static readonly Regex MapFilePatterns = new Regex(@"^perf\-[0-9]+\.map|.+\.ni\.\{.+\}\.map$");
        public static readonly Regex PerfInfoPattern = new Regex(@"^perfinfo\-[0-9]+\.map$");

        public LinuxPerfScriptMapper(ZipArchive archive, LinuxPerfScriptEventParser parser)
        {
            fileSymbolMappers = new Dictionary<string, Mapper>();
            processDllGuids = new Dictionary<string, Dictionary<string, string>>();
            processDllBaseAddresses = new Dictionary<string, Dictionary<string, ulong>>();
            this.parser = parser;

            if (archive != null)
            {
                PopulateSymbolMapperAndGuids(archive);
            }
        }

        public string[] ResolveSymbols(int processID, string modulePath, StackFrame stackFrame)
        {
            Dictionary<string, string> guids;

            string perfInfoFileName = string.Format("perfinfo-{0}.map", processID.ToString());
            if (processDllGuids.TryGetValue(
                perfInfoFileName, out guids))
            {
                string dllName = modulePath;

                string guid;
                if (guids.TryGetValue(dllName, out guid))
                {
                    string mapName = Path.ChangeExtension(dllName, guid);

                    Mapper mapper;
                    if (fileSymbolMappers.TryGetValue(mapName, out mapper))
                    {
                        string symbol;
                        ulong address;
                        ulong ip = ulong.Parse(stackFrame.Address, System.Globalization.NumberStyles.HexNumber);
                        if (mapper.TryFindSymbol(ip,
                            out symbol, out address))
                        {
                            return parser.GetSymbolFromMicrosoftMap(symbol);
                        }
                        else
                        {
                            Dictionary<string, ulong> baseAddresses;

                            if (processDllBaseAddresses.TryGetValue(
                                perfInfoFileName, out baseAddresses))
                            {
                                if (baseAddresses.TryGetValue(dllName, out ulong baseAddress))
                                {
                                    if (baseAddress <= ip)
                                    {
                                        ulong offset = ip - baseAddress;
                                        if (mapper.TryFindSymbol(offset,
                                            out symbol, out address))
                                        {
                                            return parser.GetSymbolFromMicrosoftMap(symbol);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

            }

            return new string[] { stackFrame.Module, stackFrame.Symbol };
        }

        #region private
        private void PopulateSymbolMapperAndGuids(ZipArchive archive)
        {
            Contract.Requires(archive != null, nameof(archive));

            foreach (var entry in archive.Entries)
            {
                if (MapFilePatterns.IsMatch(entry.FullName))
                {
                    Mapper mapper = new Mapper();

                    // Register the mapper both with and without the .ni extension.
                    // Old versions of the runtime contain native images with the .ni extension.
                    fileSymbolMappers[LinuxPerfScriptEventParser.GetFileNameWithoutExtension(entry.FullName, false)] = mapper;
                    fileSymbolMappers[LinuxPerfScriptEventParser.GetFileNameWithoutExtension(entry.FullName, true)] = mapper;
                    using (Stream stream = entry.Open())
                    {
                        parser.ParseSymbolFile(stream, mapper);
                    }
                    mapper.DoneMapping();
                }
                else if (PerfInfoPattern.IsMatch(LinuxPerfScriptEventParser.GetFileName(entry.FullName)))
                {
                    Dictionary<string, string> guids = new Dictionary<string, string>();
                    processDllGuids[LinuxPerfScriptEventParser.GetFileName(entry.FullName)] = guids;
                    Dictionary<string, ulong> baseAddresses = new Dictionary<string, ulong>();
                    processDllBaseAddresses[LinuxPerfScriptEventParser.GetFileName(entry.FullName)] = baseAddresses;
                    using (Stream stream = entry.Open())
                    {
                        parser.ParsePerfInfoFile(stream, guids, baseAddresses);
                    }
                }
            }
        }

        private readonly Dictionary<string, Mapper> fileSymbolMappers;
        private readonly Dictionary<string, Dictionary<string, string>> processDllGuids;
        private readonly Dictionary<string, Dictionary<string, ulong>> processDllBaseAddresses;
        private readonly LinuxPerfScriptEventParser parser;
        #endregion
    }

    public class Mapper
    {
        public Mapper()
        {
            maps = new List<Map>();
        }

        public void DoneMapping()
        {
            // Sort by the start part of the interval... This is for O(log(n)) search time.
            maps.Sort((Map x, Map y) => x.Interval.Start.CompareTo(y.Interval.Start));
        }

        public void Add(ulong start, ulong size, string symbol)
        {
            maps.Add(new Map(new Interval(start, size), symbol));
        }

        public bool TryFindSymbol(ulong location, out string symbol, out ulong startLocation)
        {
            symbol = "";
            startLocation = 0;

            if (maps.Count <= 0)
            {
                return false;
            }

            int start = 0;
            int end = maps.Count;
            int mid = (end - start) / 2;

            while (true)
            {
                int index = start + mid;
                if (maps[index].Interval.IsWithin(location))
                {
                    symbol = maps[index].MapTo;
                    startLocation = maps[index].Interval.Start;
                    return true;
                }
                else if (location < maps[index].Interval.Start)
                {
                    end = index;
                }
                else if (location >= maps[index].Interval.End)
                {
                    start = index;
                }

                if (mid < 1)
                {
                    break;
                }

                mid = (end - start) / 2;
            }

            return false;
        }

        private List<Map> maps;
    }

    internal struct Map
    {
        public Interval Interval { get; }
        public string MapTo { get; }

        public Map(Interval interval, string mapTo)
        {
            Interval = interval;
            MapTo = mapTo;
        }
    }

    internal class Interval
    {
        public ulong Start { get; }
        public ulong Length { get; }
        public ulong End { get { return Start + Length; } }

        // Taking advantage of unsigned arithmetic wrap-around to get it done in just one comparison.
        public bool IsWithin(ulong thing)
        {
            return (thing - Start) < Length;
        }

        public bool IsWithin(ulong thing, bool inclusiveStart, bool inclusiveEnd)
        {
            bool startEqual = inclusiveStart && thing.CompareTo(Start) == 0;
            bool endEqual = inclusiveEnd && thing.CompareTo(End) == 0;
            bool within = thing.CompareTo(Start) > 0 && thing.CompareTo(End) < 0;

            return within || startEqual || endEqual;
        }

        public Interval(ulong start, ulong length)
        {
            Start = start;
            Length = length;
        }

    }
    #endregion

    /// <summary>
    /// Defines the kind of an event for easy casting.
    /// </summary>
    public enum EventKind
    {
        /// <summary>
        /// Represents an event that uses the cpu, and does not do anything special
        /// </summary>
        Cpu,

        /// <summary>
        /// Represents an event that may context switch
        /// </summary>
        Scheduler,

        /// <summary>
        /// Represents a process exec event.
        /// </summary>
        ProcessExec,

        /// <summary>
        /// Represents a thread exit event.
        /// </summary>
        ThreadExit,

        /// <summary>
        /// Represents an IO init event.
        /// </summary>
        BlockRequestIssue,

        /// <summary>
        /// Represents an IO complete event.
        /// </summary>
        BlockRequestComplete,

        /// <summary>
        /// Represents a wakeup event.
        /// </summary>
        Wakeup,

        /// <summary>
        /// Represents an IRQ enter event.
        /// </summary>
        IrqEnter,

        /// <summary>
        /// Represents an IRQ exit event.
        /// </summary>
        IrqExit,
    }

    /// <summary>
    /// A sample that has extra properties to hold scheduled events.
    /// </summary>
    public class SchedulerEvent : LinuxEvent
    {
        public static readonly string Name = "sched_switch";

        /// <summary>
        /// The details of the context switch.
        /// </summary>
        public ScheduleSwitch Switch { get; }

        public SchedulerEvent(
            string comm, int tid, int pid,
            double time, int timeProp, int cpu,
            string eventName, string eventProp, IEnumerable<Frame> callerStacks, ScheduleSwitch schedSwitch) :
            base(EventKind.Scheduler, comm, tid, pid, time, timeProp, cpu, eventName, eventProp, callerStacks)
        {
            Switch = schedSwitch;
        }
    }

    /// <summary>
    /// Stores all relevant information retrieved by a context switch stack frame
    /// </summary>
    public class ScheduleSwitch
    {
        public string PreviousCommand { get; }
        public int PreviousPriority { get; }
        public char PreviousState { get; }
        public string NextCommand { get; }
        public int NextThreadID { get; }
        public int NextPriority { get; }
        public int PreviousThreadID { get; }

        public ScheduleSwitch(string prevComm, int prevTid, int prevPrio, char prevState, string nextComm, int nextTid, int nextPrio)
        {
            PreviousCommand = prevComm;
            PreviousThreadID = prevTid;
            PreviousPriority = prevPrio;
            PreviousState = prevState;
            NextCommand = nextComm;
            NextThreadID = nextTid;
            NextPriority = nextPrio;
        }
    }
    public class ProcessExecEvent : LinuxEvent
    {
        public static readonly string Name = "sched_process_exec";

        public ProcessExec ProcessExec { get; }

        public ProcessExecEvent(
            string comm, int tid, int pid,
            double time, int timeProp, int cpu,
            string eventName, string eventProp, IEnumerable<Frame> callerStacks, ProcessExec processExec) :
            base(EventKind.ProcessExec, comm, tid, pid, time, timeProp, cpu, eventName, eventProp, callerStacks)
        {
            ProcessExec = processExec;
        }
    }

    public class ProcessExec
    {

        public string FileName { get; }
        public int NewProcessId { get; }
        public int PreviousProcessId { get; }

        public ProcessExec(string fileName, int newPid, int oldPid)
        {
            FileName = fileName;
            NewProcessId = newPid;
            PreviousProcessId = oldPid;
        }
    }

    public class ThreadExitEvent : LinuxEvent
    {
        public static readonly string Name = "sched_process_exit";

        /// <summary>
        /// The details of the context switch.
        /// </summary>
        public ThreadExit Exit { get; }

        public ThreadExitEvent(
            string comm, int tid, int pid,
            double time, int timeProp, int cpu,
            string eventName, string eventProp, IEnumerable<Frame> callerStacks, ThreadExit exit) :
            base(EventKind.ThreadExit, comm, tid, pid, time, timeProp, cpu, eventName, eventProp, callerStacks)
        {
            Exit = exit;
        }
    }

    /// <summary>
    /// Stores all relevant information retrieved by a thread exit.
    /// </summary>
    public class ThreadExit
    {
        public string Command { get; }
        public int ThreadID { get; }
        public int Priority { get; }

        public ThreadExit(string comm, int tid, int prio)
        {
            Command = comm;
            ThreadID = tid;
            Priority = prio;
        }
    }

    public class CpuEvent : LinuxEvent
    {
        public CpuEvent(
            string comm, int tid, int pid,
            double time, int timeProp, int cpu,
            string eventName, string eventProp, IEnumerable<Frame> callerStacks) :
            base(EventKind.Cpu, comm, tid, pid, time, timeProp, cpu, eventName, eventProp, callerStacks)
        { }
    }

    /// <summary>
    /// A sample that has extra properties to hold disk IO init events.
    /// </summary>
    public class BlockReqIssueEvent : LinuxEvent
    {
        public static readonly string Name = "block_rq_issue";

        /// <summary>
        /// The details of the IO init.
        /// </summary>
        public BlockReqIssue ReqIssue { get; }

        public BlockReqIssueEvent(
            string comm, int tid, int pid,
            double time, int timeProp, int cpu,
            string eventName, string eventProp, IEnumerable<Frame> callerStacks, BlockReqIssue reqIssue) :
            base(EventKind.BlockRequestIssue, comm, tid, pid, time, timeProp, cpu, eventName, eventProp, callerStacks)
        {
            ReqIssue = reqIssue;
        }
    }

    public class BlockReqIssue
    {
        public uint Device { get; }
        public uint DeviceMinor { get; }
        public string Flags { get; }
        public uint Length { get; }
        public ulong Sector { get; }
        public uint SectorLength { get; }

        public BlockReqIssue(
            uint device, uint deviceMinor, string flags,
            uint length, ulong sector, uint sectorLength)
        {
            Device = device;
            DeviceMinor = deviceMinor;
            Flags = flags;
            Length = length;
            Sector = sector;
            SectorLength = sectorLength;
        }
    }

    /// <summary>
    /// A sample that has extra properties to hold disk IO complete events.
    /// </summary>
    public class BlockReqCompleteEvent : LinuxEvent
    {
        public static readonly string Name = "block_rq_complete";

        /// <summary>
        /// The details of the IO complete.
        /// </summary>
        public BlockReqComplete ReqComplete { get; }

        public BlockReqCompleteEvent(
            string comm, int tid, int pid,
            double time, int timeProp, int cpu,
            string eventName, string eventProp, IEnumerable<Frame> callerStacks, BlockReqComplete reqComplete) :
            base(EventKind.BlockRequestComplete, comm, tid, pid, time, timeProp, cpu, eventName, eventProp, callerStacks)
        {
            ReqComplete = reqComplete;
        }
    }

    public class BlockReqComplete
    {
        public uint Device { get; }
        public uint DeviceMinor { get; }
        public string Flags { get; }
        public ulong Sector { get; }
        public uint SectorLength { get; }
        public int Error { get; }

        public BlockReqComplete(
            uint device, uint deviceMinor, string flags,
            ulong sector, uint sectorLength, int error)
        {
            Device = device;
            DeviceMinor = deviceMinor;
            Flags = flags;
            Sector = sector;
            SectorLength = sectorLength;
            Error = error;
        }
    }

    /// <summary>
    /// A sample that has extra properties to hold scheduler wakeup events.
    /// </summary>
    public class SchedWakeupEvent : LinuxEvent
    {
        public static readonly string Name = "sched_wakeup";

        /// <summary>
        /// The details of the wakeup.
        /// </summary>
        public SchedWakeup Wakeup { get; }

        public SchedWakeupEvent(
            string comm, int tid, int pid,
            double time, int timeProp, int cpu,
            string eventName, string eventProp, IEnumerable<Frame> callerStacks, SchedWakeup wakeup) :
            base(EventKind.Wakeup, comm, tid, pid, time, timeProp, cpu, eventName, eventProp, callerStacks)
        {
            Wakeup = wakeup;
        }
    }

    public class SchedWakeup
    {
        public string Comm { get; }
        public int ProcessId { get; }
        public int Priority { get; }
        public int TargetCpu { get; }

        public SchedWakeup(
            string comm, int processId, int priority, int targetCpu)
        {
            Comm = comm;
            ProcessId = processId;
            Priority = priority;
            TargetCpu = targetCpu;
        }
    }

    /// <summary>
    /// A sample that has extra properties to hold IRQ enter events.
    /// </summary>
    public class IrqEnterEvent : LinuxEvent
    {
        public static readonly string Name = "irq_handler_entry";

        /// <summary>
        /// The details of the IO init.
        /// </summary>
        public IrqEnter Enter { get; }

        public IrqEnterEvent(
            string comm, int tid, int pid,
            double time, int timeProp, int cpu,
            string eventName, string eventProp, IEnumerable<Frame> callerStacks, IrqEnter enter) :
            base(EventKind.IrqEnter, comm, tid, pid, time, timeProp, cpu, eventName, eventProp, callerStacks)
        {
            Enter = enter;
        }
    }

    public class IrqEnter
    {
        public uint Vector { get; }
        public string Name { get; }

        public IrqEnter(
            uint vector, string name)
        {
            Vector = vector;
            Name = name;
        }
    }

    /// <summary>
    /// A sample that has extra properties to hold IRQ exit events.
    /// </summary>
    public class IrqExitEvent : LinuxEvent
    {
        public static readonly string Name = "irq_handler_exit";

        /// <summary>
        /// The details of the IRQ exit.
        /// </summary>
        public IrqExit Exit { get; }

        public IrqExitEvent(
            string comm, int tid, int pid,
            double time, int timeProp, int cpu,
            string eventName, string eventProp, IEnumerable<Frame> callerStacks, IrqExit exit) :
            base(EventKind.IrqExit, comm, tid, pid, time, timeProp, cpu, eventName, eventProp, callerStacks)
        {
            Exit = exit;
        }
    }

    public class IrqExit
    {
        public uint Vector { get; }
        public string Status { get; }

        public IrqExit(
            uint vector, string status)
        {
            Vector = vector;
            Status = status;
        }
    }

    /// <summary>
    /// A generic Linux event, all Linux events contain these properties.
    /// </summary>
    public abstract class LinuxEvent
    {
        public EventKind Kind { get; }
        public string Command { get; }
        public int ThreadID { get; }
        public int ProcessID { get; }
        public int CpuNumber { get; }
        public double TimeMSec { get; }
        public int TimeProperty { get; }
        public string EventName { get; }
        public string EventProperty { get; }
        public IEnumerable<Frame> CallerStacks { get; }

        public double Period { get; set; }

        public LinuxEvent(EventKind kind,
            string comm, int tid, int pid,
            double time, int timeProp, int cpu,
            string eventName, string eventProp, IEnumerable<Frame> callerStacks)
        {
            Kind = kind;
            Command = comm;
            ThreadID = tid;
            ProcessID = pid;
            TimeMSec = time;
            TimeProperty = timeProp;
            CpuNumber = cpu;
            EventName = eventName;
            EventProperty = eventProp;
            CallerStacks = callerStacks;
        }
    }

    public enum FrameKind
    {
        /// <summary>
        /// An actual stack frame from the simpling data
        /// </summary>
        StackFrame,

        /// <summary>
        /// A stack frame that represents the process of the sample
        /// </summary>
        ProcessFrame,

        /// <summary>
        /// A stack frame that represents the thread of the sample
        /// </summary>
        ThreadFrame,

        /// <summary>
        /// A stack frame that represents either blocked time or cpu time
        /// </summary>
        BlockedCPUFrame
    }

    /// <summary>
    /// A way to define different types of frames with different names on PerfView.
    /// </summary>
    public interface Frame
    {
        FrameKind Kind { get; }
        string DisplayName { get; }
    }

    /// <summary>
    /// Defines a single stack frame on a linux sample.
    /// </summary>
    public struct StackFrame : Frame
    {
        public FrameKind Kind { get { return FrameKind.StackFrame; } }
        public string DisplayName
        {
            get
            {
                if (OptimizationTier == OptimizationTier.Unknown)
                {
                    return string.Format("{0}!{1}", Module, Symbol);
                }
                else
                {
                    return string.Format("{0}![{1}]{2}", Module, OptimizationTier.ToString(), Symbol);
                }
            }
        }
        public string Address { get; }
        public string Module { get; }
        public string Symbol { get; }
        public OptimizationTier OptimizationTier { get; }

        public StackFrame(string address, string module, string symbol)
        {
            Address = address;
            Module = module;
            OptimizationTier = OptimizationTier.Unknown;

            // Check for the optimization tier. The symbol would contain the optimization tier in the form:
            //   Symbol[OptimizationTier]
            // Save the optimization tier so that it can be put onto the front of the frame if present.
            if (symbol != null && symbol.Length >= 3 && symbol[symbol.Length - 1] == ']')
            {
                int openBracketIndex = symbol.LastIndexOf('[', symbol.Length - 2);
                if (openBracketIndex >= 0 && symbol.Length - openBracketIndex > 2)
                {
                    var optimizationTierStr = symbol.Substring(openBracketIndex + 1, symbol.Length - openBracketIndex - 2);
                    if (Enum.TryParse<OptimizationTier>(optimizationTierStr, out var optimizationTier))
                    {
                        symbol = symbol.Substring(0, openBracketIndex);
                        OptimizationTier = optimizationTier;
                    }
                }
            }

            Symbol = symbol;
        }

        public StackFrame(string address, string module, string symbol, OptimizationTier optimizationTier)
        {
            Address = address;
            Module = module;
            Symbol = symbol;
            OptimizationTier = optimizationTier;
        }
    }

    /// <summary>
    /// Represents the name of the process.
    /// </summary>
    public struct ProcessFrame : Frame
    {
        public FrameKind Kind { get { return FrameKind.ProcessFrame; } }
        public string DisplayName { get { return string.Format("Process {0} ({1})", Name, ID); } }
        public string Name { get; }
        public int ID { get; }

        public ProcessFrame(int id, string name)
        {
            ID = id;
            Name = name;
        }
    }

    /// <summary>
    /// Represents the name of the thread and its ID.
    /// </summary>
    public struct ThreadFrame : Frame
    {
        public FrameKind Kind { get { return FrameKind.ThreadFrame; } }
        public string DisplayName { get { return string.Format("{0} ({1})", Name, ID); } }
        public string Name { get; }
        public int ID { get; }

        public ThreadFrame(int id, string name)
        {
            Name = name;
            ID = id;
        }
    }

    /// <summary>
    /// A visual frame that represents whether or not a call stack was blocked or not.
    /// </summary>
    public struct BlockedCPUFrame : Frame
    {
        /// <summary>
        /// Represents whether the stack frame is BLOCKED_TIME or CPU_TIME
        /// </summary>
        public string SubKind { get; }
        public FrameKind Kind { get { return FrameKind.BlockedCPUFrame; } }
        public string DisplayName { get { return SubKind; } }

        public int ID { get; }

        public BlockedCPUFrame(int id, string kind)
        {
            ID = id;
            SubKind = kind;
        }
    }
}
