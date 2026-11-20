using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;

using Microsoft.Diagnostics.Tracing.Computers;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;

using static Microsoft.Diagnostics.Tracing.Stacks.StackSourceWriterHelper;

namespace Microsoft.Diagnostics.Tracing.Stacks.Formats
{
    public static class SpeedScopeStackSourceWriter
    {
        /// <summary>
        /// exports provided StackSource to a https://www.speedscope.app/ format 
        /// schema: https://www.speedscope.app/file-format-schema.json
        /// </summary>
        public static void WriteStackViewAsJson(StackSource source, string filePath)
            => WriteStackViewAsJson(source, filePath, preserveSampleGaps: false);

        /// <summary>
        /// Exports a StackSource to Speedscope format with optional preservation of sample coverage gaps.
        /// </summary>
        /// <param name="source">The stack source to export.</param>
        /// <param name="filePath">The output file path.</param>
        /// <param name="preserveSampleGaps">
        /// If true, closes all frames between nonoverlapping sample intervals.
        /// If false, uses legacy sample-continuity and accumulated-weight behavior.
        /// </param>
        public static void WriteStackViewAsJson(StackSource source, string filePath, bool preserveSampleGaps)
        {
            if (File.Exists(filePath))
                File.Delete(filePath);

            using (var writeStream = File.CreateText(filePath))
                Export(source, writeStream, Path.GetFileNameWithoutExtension(filePath), preserveSampleGaps);
        }

        /// <summary>
        /// Exports the recorded async contexts directly to a separate Speedscope view, without CPU samples
        /// or physical stacks. Durations measure elapsed time with a context active, not CPU time or full
        /// method lifetimes across awaits.
        /// </summary>
        /// <param name="traceLog">The open trace supplying the persisted async index and resolved symbols.</param>
        /// <param name="filePath">The output file, replacing it if it already exists.</param>
        /// <param name="processId">A process ID to include, or 0 for all process instances.</param>
        /// <remarks>
        /// Each context opens its finalized captured stack at resume, including V1 frames appended later.
        /// Recorded method completions and exception unwinds remove leaf frames; missing progress is not
        /// inferred from wrapper resets or physical stacks. Suspension closes the displayed context without
        /// declaring its methods completed. Nested contexts retain their enclosing contexts.
        /// Intervals are clipped to the trace, including contexts still open at trace end; zero-duration
        /// intervals are omitted. Zero-IP sentinel slots are hidden without changing recorded completion
        /// counts; unresolved nonzero IPs remain visible. Missing async data or no visible matching contexts
        /// produces an empty profiles array. This does not rebuild or change the persisted index.
        /// V1 state-machine names use the CPU views' logical method and ordinal presentation, with captured
        /// states rather than inferred executing/completed states.
        /// </remarks>
        public static void WriteAsyncCallStacksAsJson(TraceLog traceLog, string filePath, int processId = 0)
            => WriteAsyncCallStacksAsJson(traceLog, filePath, processId, 0);

        /// <summary>
        /// Exports virtually sampled async contexts without requiring CPU samples or physical stacks.
        /// </summary>
        /// <param name="traceLog">The open trace supplying the persisted async index and resolved symbols.</param>
        /// <param name="filePath">The output file, replacing it if it already exists.</param>
        /// <param name="sampleInterval">A positive interval, rounded to the nearest trace QPC tick.
        /// Intervals shorter than one QPC tick are rejected.</param>
        /// <param name="processId">A process ID to include, or 0 for all process instances.</param>
        /// <remarks>
        /// All threads share a grid beginning at trace start. At each grid point the exporter observes the
        /// finalized captured stacks with recorded completion/unwind progress, then holds that snapshot for
        /// one interval, clipped at trace end. Empty snapshots leave gaps; async call stacks entirely between grid
        /// points can be missed. Widths estimate async-context occupancy, not CPU time or exact lifetimes.
        /// V1 append, nesting, unknown-frame, and zero-IP policies match the exact-event overload.
        /// </remarks>
        public static void WriteAsyncCallStacksAsJson(TraceLog traceLog, string filePath,
            TimeSpan sampleInterval, int processId = 0)
        {
            if (traceLog == null)
            {
                throw new ArgumentNullException(nameof(traceLog));
            }
            double ticks = sampleInterval.Ticks * (traceLog.QPCFreq / (double)TimeSpan.TicksPerSecond);
            if (ticks < 1 || ticks >= long.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(sampleInterval),
                    "The sampling interval must span at least one QPC tick and fit in the trace clock.");
            }
            WriteAsyncCallStacksAsJson(traceLog, filePath, processId,
                (long)Math.Round(ticks, MidpointRounding.AwayFromZero));
        }

        #region private

        private static void WriteAsyncCallStacksAsJson(TraceLog traceLog, string filePath,
            int processId, long sampleIntervalQpc)
        {
            if (traceLog == null)
            {
                throw new ArgumentNullException(nameof(traceLog));
            }
            if (processId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(processId));
            }

            var frameIds = new AsyncFrameIdTable(traceLog);
            Func<AsyncProfilerCallStack, int, int> frameId = frameIds.GetFrameId;
            var profiles = new Dictionary<string, IReadOnlyList<ProfileEvent>>();
            AsyncProfilerCallStacksIndex index = traceLog.AsyncProfilerCallStacks;
            if (index != null)
            {
                foreach (var thread in index.Threads.OrderBy(t => (int)t.ProcessIndex).ThenBy(t => t.OsThreadId))
                {
                    TraceProcess process = traceLog.Processes[thread.ProcessIndex];
                    if (processId != 0 && process.ProcessID != processId)
                    {
                        continue;
                    }
                    var events = GetAsyncProfileEvents(index.GetCallStacks(thread),
                        traceLog.sessionStartTimeQPC, traceLog.sessionEndTimeQPC,
                        traceLog.QPCTimeToRelMSec, frameId, sampleIntervalQpc);
                    if (events.Count != 0)
                    {
                        Debug.Assert(Validate(events), "The async export must be balanced and chronological.");
                        profiles.Add(GetAsyncProfileName(traceLog, process, thread, sampleIntervalQpc), events);
                    }
                }
            }
            using (var writer = File.CreateText(filePath))
            {
                WriteToFile(profiles, frameIds.Names, writer, Path.GetFileNameWithoutExtension(filePath));
            }
        }

        private static string GetAsyncProfileName(TraceLog traceLog, TraceProcess process,
            AsyncProfilerThreadKey thread, long sampleIntervalQpc)
        {
            string mode = "elapsed";
            if (sampleIntervalQpc != 0)
            {
                double intervalMSec = sampleIntervalQpc * 1000.0 / traceLog.QPCFreq;
                mode = "virtual " + intervalMSec.ToString("R", CultureInfo.InvariantCulture) + " ms";
            }

            return $"Async events ({mode}) - {process.Name} ({process.ProcessID}, " +
                $"instance {(int)thread.ProcessIndex}) - Thread ({thread.OsThreadId})";
        }

        /// <summary>
        /// Assigns shared SpeedScope frame IDs by display name, caching the IDs of each captured frame list.
        /// Zero-IP sentinel slots get -1.
        /// </summary>
        private sealed class AsyncFrameIdTable
        {
            private readonly TraceLog m_traceLog;
            private readonly Dictionary<string, int> m_idByName = new Dictionary<string, int>();
            private readonly Dictionary<AsyncProfilerCallStackFramesIndex, int[]> m_idsByFrames =
                new Dictionary<AsyncProfilerCallStackFramesIndex, int[]>();

            public AsyncFrameIdTable(TraceLog traceLog)
            {
                m_traceLog = traceLog;
            }

            public List<string> Names { get; } = new List<string>();

            public int GetFrameId(AsyncProfilerCallStack context, int slot)
            {
                if (!m_idsByFrames.TryGetValue(context.FramesIndex, out int[] ids))
                {
                    ids = CreateFrameIds(context);
                    m_idsByFrames.Add(context.FramesIndex, ids);
                }
                return ids[slot];
            }

            private int[] CreateFrameIds(AsyncProfilerCallStack context)
            {
                var ids = new int[context.Frames.FrameCount];
                for (int i = 0; i < ids.Length; i++)
                {
                    ids[i] = context.Frames.InstructionPointerAt(i) == 0
                        ? -1
                        : GetOrAddName(GetAsyncFrameName(m_traceLog, context, i));
                }
                return ids;
            }

            private int GetOrAddName(string name)
            {
                if (!m_idByName.TryGetValue(name, out int id))
                {
                    id = Names.Count;
                    m_idByName.Add(name, id);
                    Names.Add(name);
                }
                return id;
            }
        }

        internal static IReadOnlyList<ProfileEvent> GetAsyncProfileEvents(
            IReadOnlyList<AsyncProfilerCallStack> contexts, long startQpc, long endQpc,
            Func<long, double> relativeTime, Func<AsyncProfilerCallStack, int, int> frameId,
            long sampleIntervalQpc = 0)
        {
            List<AsyncChange> changes = GetAsyncChanges(contexts, startQpc, endQpc);
            var result = new List<ProfileEvent>();
            if (changes.Count == 0)
            {
                return result;
            }

            var active = new SortedDictionary<int, ActiveAsyncCallStack>();
            var previous = new List<OpenAsyncFrame>();
            var current = new List<OpenAsyncFrame>();
            bool sampled = sampleIntervalQpc > 0;
            long qpc = sampled ? startQpc : changes[0].Qpc;
            int position = 0;
            do
            {
                // Observe every transition through this instant before emitting the stack snapshot.
                position = ApplyAsyncChanges(changes, position, qpc, active);
                GetOpenAsyncFrames(active, frameId, current);
                AddAsyncFrameTransitions(previous, current, relativeTime(qpc), result);
                var temporary = previous;
                previous = current;
                current = temporary;
            }
            while (TryAdvanceAsyncSnapshotTime(changes, position, previous.Count != 0,
                sampleIntervalQpc, endQpc, ref qpc));

            if (sampled)
            {
                double end = relativeTime(endQpc);
                for (int j = previous.Count - 1; j >= 0; j--)
                {
                    result.Add(new ProfileEvent(ProfileEventType.Close, previous[j].Id, end, j));
                }
            }
            return result;
        }

        /// <summary>
        /// Returns the start, end, and in-interval completion changes of every context, clipped to the trace
        /// and sorted by time, with ends before starts at the same instant.
        /// </summary>
        private static List<AsyncChange> GetAsyncChanges(IReadOnlyList<AsyncProfilerCallStack> contexts, long startQpc, long endQpc)
        {
            var changes = new List<AsyncChange>();
            foreach (var context in contexts)
            {
                long start = Math.Max(startQpc, context.StartQpc);
                long end = Math.Min(endQpc, context.EndQpc);
                if (start >= end)
                {
                    continue;
                }

                long completedBeforeStart =
                    AddCompletionChanges(changes, context, context.MethodCompletions, start, end) +
                    AddCompletionChanges(changes, context, context.ExceptionCompletions, start, end);
                changes.Add(new AsyncChange(start, context, AsyncEventKind.Start, completedBeforeStart));
                changes.Add(new AsyncChange(end, context, AsyncEventKind.End, 0));
            }

            changes.Sort((a, b) =>
            {
                int order = a.Qpc.CompareTo(b.Qpc);
                return order != 0 ? order : a.Kind.CompareTo(b.Kind);
            });
            return changes;
        }

        /// <summary>
        /// Adds completions inside [start, end) as changes and returns the total completed before start.
        /// </summary>
        private static long AddCompletionChanges(List<AsyncChange> changes, AsyncProfilerCallStack context,
            IReadOnlyList<AsyncProfilerCallStack.CompletionDelta> completions, long start, long end)
        {
            long beforeStart = 0;
            for (int i = 0; i < completions.Count; i++)
            {
                var completion = completions[i];
                if (completion.Delta < 0)
                {
                    throw new InvalidDataException("An async completion count cannot be negative.");
                }

                if (completion.Qpc < start)
                {
                    beforeStart += completion.Delta;
                }
                else if (completion.Qpc < end)
                {
                    changes.Add(new AsyncChange(completion.Qpc, context, AsyncEventKind.Complete, completion.Delta));
                }
            }
            return beforeStart;
        }

        /// <summary>
        /// Applies every change at or before <paramref name="qpc"/> and returns the position of the next change.
        /// </summary>
        private static int ApplyAsyncChanges(List<AsyncChange> changes, int position, long qpc,
            SortedDictionary<int, ActiveAsyncCallStack> active)
        {
            while (position < changes.Count && changes[position].Qpc <= qpc)
            {
                AsyncChange change = changes[position++];
                int depth = change.Context.Depth;
                switch (change.Kind)
                {
                    case AsyncEventKind.End:
                        active.Remove(depth);
                        break;
                    case AsyncEventKind.Start:
                        if (active.ContainsKey(depth))
                        {
                            throw new InvalidDataException("Async contexts overlap at the same nesting depth.");
                        }
                        active.Add(depth, new ActiveAsyncCallStack(change.Context, change.Delta));
                        break;
                    case AsyncEventKind.Complete:
                        ActiveAsyncCallStack state = active[depth];
                        active[depth] = new ActiveAsyncCallStack(state.Context, state.Completed + change.Delta);
                        break;
                }
            }
            return position;
        }

        /// <summary>Fills <paramref name="frames"/> with the visible frames of the active contexts, root first.</summary>
        private static void GetOpenAsyncFrames(SortedDictionary<int, ActiveAsyncCallStack> active,
            Func<AsyncProfilerCallStack, int, int> frameId, List<OpenAsyncFrame> frames)
        {
            frames.Clear();
            foreach (ActiveAsyncCallStack state in active.Values)
            {
                AsyncProfilerCallStackFrames captured = state.Context.Frames;

                // Progress counts captured slots, including hidden zero-IP sentinels.
                for (int slot = captured.FrameCount - 1; slot >= state.Completed; slot--)
                {
                    if (captured.InstructionPointerAt(slot) != 0)
                    {
                        frames.Add(new OpenAsyncFrame(state.Context, slot, frameId(state.Context, slot)));
                    }
                }
            }
        }

        /// <summary>Closes frames that differ from the previous snapshot and opens the new ones.</summary>
        private static void AddAsyncFrameTransitions(List<OpenAsyncFrame> previous, List<OpenAsyncFrame> current,
            double time, List<ProfileEvent> result)
        {
            // Equal names in different call stacks must still close and reopen.
            int common = 0;
            while (common < previous.Count &&
                common < current.Count &&
                ReferenceEquals(previous[common].Context, current[common].Context) &&
                previous[common].Slot == current[common].Slot)
            {
                common++;
            }

            for (int j = previous.Count - 1; j >= common; j--)
            {
                result.Add(new ProfileEvent(ProfileEventType.Close, previous[j].Id, time, j));
            }
            for (int j = common; j < current.Count; j++)
            {
                result.Add(new ProfileEvent(ProfileEventType.Open, current[j].Id, time, j));
            }
        }

        /// <summary>
        /// Moves <paramref name="qpc"/> to the next snapshot time: the next change for exact export, or the next
        /// grid point for virtual sampling. Returns false when there are no more snapshots.
        /// </summary>
        private static bool TryAdvanceAsyncSnapshotTime(List<AsyncChange> changes, int position, bool hasOpenFrames,
            long sampleIntervalQpc, long endQpc, ref long qpc)
        {
            if (sampleIntervalQpc <= 0)
            {
                if (position >= changes.Count)
                {
                    return false;
                }

                qpc = changes[position].Qpc;
                return true;
            }

            if (!hasOpenFrames && position == changes.Count)
            {
                return false;
            }

            // Skip empty time without re-anchoring a thread's grid at its next call stack.
            long steps = 1;
            if (!hasOpenFrames)
            {
                steps = (changes[position].Qpc - qpc - 1) / sampleIntervalQpc + 1;
            }

            if (steps > (endQpc - qpc - 1) / sampleIntervalQpc)
            {
                return false;
            }

            qpc += steps * sampleIntervalQpc;
            return true;
        }

        private readonly struct AsyncChange
        {
            public AsyncChange(long qpc, AsyncProfilerCallStack context, AsyncEventKind kind, long delta)
            {
                Qpc = qpc;
                Context = context;
                Kind = kind;
                Delta = delta;
            }

            public long Qpc { get; }
            public AsyncProfilerCallStack Context { get; }
            public AsyncEventKind Kind { get; }

            /// <summary>Completed frames: the count before start for Start, or the increment for Complete.</summary>
            public long Delta { get; }
        }

        private readonly struct ActiveAsyncCallStack
        {
            public ActiveAsyncCallStack(AsyncProfilerCallStack context, long completed)
            {
                Context = context;
                Completed = completed;
            }

            public AsyncProfilerCallStack Context { get; }
            public long Completed { get; }
        }

        private readonly struct OpenAsyncFrame
        {
            public OpenAsyncFrame(AsyncProfilerCallStack context, int slot, int id)
            {
                Context = context;
                Slot = slot;
                Id = id;
            }

            public AsyncProfilerCallStack Context { get; }
            public int Slot { get; }
            public int Id { get; }
        }
        private static string GetAsyncFrameName(TraceLog traceLog, AsyncProfilerCallStack context, int slot)
        {
            var frames = context.Frames;
            string prefix = frames.Kind == AsyncCallstackKind.RuntimeAsync ? "[Async V2] " : "[Async V1] ";
            CodeAddressIndex address = frames.CodeAddressAt(slot);
            string name = null;
            if (address != CodeAddressIndex.Invalid)
            {
                TraceCodeAddress code = traceLog.CodeAddresses[address];
                if (!string.IsNullOrEmpty(code.FullMethodName))
                {
                    if (frames.Kind == AsyncCallstackKind.StateMachineAsync &&
                        AsyncProfilerMethodNames.TryGetLogicalStateMachineMethodName(
                            AsyncProfilerMethodNames.NormalizeMethodName(code.FullMethodName),
                            out string logicalName, out string stateMachineSuffix))
                    {
                        return prefix + code.ModuleName + "!" +
                            AsyncProfilerMethodNames.FormatLogicalStateMachineMethodName(
                                logicalName, stateMachineSuffix, frames.FrameStateAt(slot));
                    }
                    name = code.ModuleName + "!" + code.FullMethodName;
                }
            }
            if (name == null)
            {
                name = "AsyncFrame(0x" + frames.InstructionPointerAt(slot).ToString("x", CultureInfo.InvariantCulture) + ")";
            }
            if (frames.Kind == AsyncCallstackKind.StateMachineAsync)
            {
                name += " [captured state " + frames.FrameStateAt(slot).ToString(CultureInfo.InvariantCulture) + "]";
            }
            return prefix + name;
        }

        private enum AsyncEventKind { End, Start, Complete }

        private static void Export(StackSource source, TextWriter writer, string name, bool preserveSampleGaps)
        {
            var samplesPerThread = GetSortedSamplesPerThread(source);

            var exportedFrameNameToExportedFrameId = new Dictionary<string, int>();
            var exportedFrameIdToFrameTuple = new Dictionary<int, FrameInfo>();
            var profileEventsPerThread = new Dictionary<string, IReadOnlyList<ProfileEvent>>(samplesPerThread.Count);

            foreach (var pair in samplesPerThread)
            {
                var sortedProfileEvents = GetProfileEvents(source, pair.Value, exportedFrameNameToExportedFrameId,
                    exportedFrameIdToFrameTuple, preserveSampleGaps);

                Debug.Assert(Validate(sortedProfileEvents), "The output should be always valid");

                profileEventsPerThread.Add(pair.Key.Name, sortedProfileEvents);
            };

            var orderedFrameNames = exportedFrameNameToExportedFrameId.OrderBy(pair => pair.Value).Select(pair => pair.Key).ToArray();

            WriteToFile(profileEventsPerThread, orderedFrameNames, writer, name);
        }

        /// <summary>
        /// writes pre-calculated data to SpeedScope format
        /// </summary>
        private static void WriteToFile(IReadOnlyDictionary<string, IReadOnlyList<ProfileEvent>> sortedProfileEventsPerThread,
            IReadOnlyList<string> orderedFrameNames, TextWriter writer, string name)
        {
            Dictionary<string, string> escapedNames = new Dictionary<string, string>();

            writer.Write("{");
            writer.Write($"\"exporter\": \"{GetExporterInfo()}\", ");
            writer.Write($"\"name\": \"{GetEscaped(name, escapedNames)}\", ");
            writer.Write("\"activeProfileIndex\": 0, ");
            writer.Write("\"$schema\": \"https://www.speedscope.app/file-format-schema.json\", ");

            writer.Write("\"shared\": { \"frames\": [ ");
            for (int i = 0; i < orderedFrameNames.Count; i++)
            {
                writer.Write($"{{ \"name\": \"{GetEscaped(orderedFrameNames[i], escapedNames)}\" }}");

                if (i != orderedFrameNames.Count - 1)
                    writer.Write(", ");
            }
            writer.Write("] }, ");

            writer.Write("\"profiles\": [ ");

            bool isFirst = true;
            foreach (var perThread in sortedProfileEventsPerThread.OrderBy(pair => pair.Value.First().RelativeTime))
            {
                if (!isFirst)
                    writer.Write(", ");
                else
                    isFirst = false;

                var sortedProfileEvents = perThread.Value;

                writer.Write("{ ");
                writer.Write("\"type\": \"evented\", ");
                writer.Write($"\"name\": \"{GetEscaped(perThread.Key, escapedNames)}\", ");
                writer.Write("\"unit\": \"milliseconds\", ");
                writer.Write($"\"startValue\": {sortedProfileEvents.First().RelativeTime.ToString("R", CultureInfo.InvariantCulture)}, ");
                writer.Write($"\"endValue\": {sortedProfileEvents.Last().RelativeTime.ToString("R", CultureInfo.InvariantCulture)}, ");
                writer.Write("\"events\": [ ");
                for (int i = 0; i < sortedProfileEvents.Count; i++)
                {
                    var frameEvent = sortedProfileEvents[i];

                    writer.Write($"{{ \"type\": \"{(frameEvent.Type == ProfileEventType.Open ? "O" : "C")}\", ");
                    writer.Write($"\"frame\": {frameEvent.FrameId}, ");
                    // "R" is crucial here!!! we can't lose precision because it can affect the sort order!!!!
                    writer.Write($"\"at\": {frameEvent.RelativeTime.ToString("R", CultureInfo.InvariantCulture)} }}");

                    if (i != sortedProfileEvents.Count - 1)
                        writer.Write(", ");
                }
                writer.Write("]");
                writer.Write("}");
            }

            writer.Write("] }");
        }
        #endregion private
    }
}
