using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace Microsoft.Diagnostics.Tracing.Stacks
{
    internal static class StackSourceWriterHelper
    {
        /// <summary>
        /// we want to identify the thread for every sample to prevent from 
        /// overlaping of samples for the concurrent code so we group the samples by Threads
        /// this method also sorts the samples by relative time (ascending)
        /// </summary>
        internal static IReadOnlyDictionary<ThreadInfo, List<Sample>> GetSortedSamplesPerThread(StackSource stackSource)
        {
            var samplesPerThread = new Dictionary<ThreadInfo, List<Sample>>();

            stackSource.ForEach(sample =>
            {
                var stackIndex = sample.StackIndex;

                while (stackIndex != StackSourceCallStackIndex.Invalid)
                {
                    var frameName = stackSource.GetFrameName(stackSource.GetFrameIndex(stackIndex), false);

                    // we walk the stack up until we find the Thread name
                    if (!frameName.StartsWith("Thread ("))
                    {
                        stackIndex = stackSource.GetCallerIndex(stackIndex);
                        continue;
                    }

                    // we assume that the next caller is always process
                    var processStackIndex = stackSource.GetCallerIndex(stackIndex);
                    var processFrameName = processStackIndex == StackSourceCallStackIndex.Invalid
                        ? "Unknown"
                        : stackSource.GetFrameName(stackSource.GetFrameIndex(processStackIndex), false);

                    var threadInfo = new ThreadInfo(frameName, processFrameName);

                    if (!samplesPerThread.TryGetValue(threadInfo, out var samples))
                        samplesPerThread[threadInfo] = samples = new List<Sample>();

                    samples.Add(new Sample(sample.StackIndex, -1, sample.TimeRelativeMSec, sample.Metric, -1));

                    return;
                }

                // Sample with no Thread assigned - it's most probably a "Process" sample, we just ignore it
            });

            foreach (var samples in samplesPerThread.Values)
            {
                // all samples in the StackSource should be sorted, but we want to ensure it
                samples.Sort(CompareSamples);
            }

            return samplesPerThread;
        }

        /// <summary>
        /// all the samples that we have are leafs (last sample in the call stack)
        /// this method expands those samples to full information 
        /// it walks the stack up to the begining and adds a sample for every method on the stack
        /// it's required to build full information
        /// </summary>
        internal static IReadOnlyDictionary<int, List<Sample>> WalkTheStackAndExpandSamples(StackSource stackSource, IEnumerable<Sample> leafs,
            Dictionary<string, int> exportedFrameNameToExportedFrameId, Dictionary<int, FrameInfo> exportedFrameIdToExportedNameAndCallerId)
        {
            var frameIdToSamples = new Dictionary<int, List<Sample>>();

            // we use stack here bacause we want a certain order: from the root to the leaf
            var stackIndexesToHandle = new Stack<StackSourceCallStackIndex>();

            foreach (var leafSample in leafs)
            {
                // walk the stack first
                var stackIndex = leafSample.StackIndex;
                while (stackIndex != StackSourceCallStackIndex.Invalid)
                {
                    stackIndexesToHandle.Push(stackIndex);

                    stackIndex = stackSource.GetCallerIndex(stackIndex);
                }

                // add sample for every method on the stack
                int depth = -1;
                int callerFrameId = -1;
                while (stackIndexesToHandle.Count > 0)
                {
                    stackIndex = stackIndexesToHandle.Pop();
                    depth++;

                    var frameIndex = stackSource.GetFrameIndex(stackIndex);
                    if (frameIndex == StackSourceFrameIndex.Broken || frameIndex == StackSourceFrameIndex.Invalid)
                        continue;

                    var frameName = stackSource.GetFrameName(frameIndex, false);
                    if (string.IsNullOrEmpty(frameName))
                        continue;

                    if (!exportedFrameNameToExportedFrameId.TryGetValue(frameName, out int exportedFrameId))
                        exportedFrameNameToExportedFrameId.Add(frameName, exportedFrameId = exportedFrameNameToExportedFrameId.Count);

                    if (!frameIdToSamples.TryGetValue(exportedFrameId, out var samples))
                        frameIdToSamples.Add(exportedFrameId, samples = new List<Sample>());

                    // the time and metric are the same as for the leaf sample
                    // the difference is stack index (not really used from here), caller frame id and depth (used for sorting the exported data)
                    samples.Add(new Sample(stackIndex, callerFrameId, leafSample.RelativeTime, leafSample.Metric, depth));

                    if (!exportedFrameIdToExportedNameAndCallerId.ContainsKey(exportedFrameId))
                    {
                        // in the future we could identify the categories in a more advance way
                        // and split JIT, GC, Runtime, Libraries and ASP.NET Code into separate categories
                        int index = frameName.IndexOf('!');
                        string category = index > 0 ? frameName.Substring(0, index) : string.Empty;
                        string shortName = index > 0 ? frameName.Substring(index + 1) : frameName;
                        exportedFrameIdToExportedNameAndCallerId.Add(exportedFrameId, new FrameInfo(callerFrameId, shortName, category));
                    }

                    callerFrameId = exportedFrameId;
                }
            }

            return frameIdToSamples;
        }

        /// <summary>
        /// this method aggregates all the singular samples to continuous events
        /// example: samples for Main taken at time 0.1 0.2 0.3 0.4 0.5
        /// are gonna be translated to Main start at 0.1 stop at 0.5
        /// </summary>
        internal static IReadOnlyList<ProfileEvent> GetAggregatedOrderedProfileEvents(IReadOnlyDictionary<int, List<Sample>> frameIdToSamples)
        {
            List<ProfileEvent> profileEvents = new List<ProfileEvent>();

            foreach (var samplesInfo in frameIdToSamples)
            {
                var frameId = samplesInfo.Key;
                var samples = samplesInfo.Value;

                // this should not be required, but I prefer to be sure that the data is sorted
                samples.Sort(CompareSamples);

                Sample openSample = samples[0]; // samples are never empty
                for (int i = 1; i < samples.Count; i++)
                {
                    if (AreNotContinuous(samples[i - 1], samples[i]))
                    {
                        AddEvents(profileEvents, openSample, samples[i - 1], frameId);

                        openSample = samples[i];
                    }
                }

                // we need to handle the last (or the only one) profile event
                AddEvents(profileEvents, openSample, samples[samples.Count - 1], frameId);
            }

            // MUST HAVE!!! the tool expects the profile events in certain order!!
            return OrderForExport(profileEvents).ToArray();
        }

        /// <summary>
        /// this method checks if both samples do NOT belong to the same profile event
        /// </summary>
        private static bool AreNotContinuous(Sample left, Sample right)
        {
            if (left.Depth != right.Depth)
                return true;
            if (left.CallerFrameId != right.CallerFrameId)
                return true;

            // 1.2 is a magic number based on some experiments ;)
            return left.RelativeTime + (left.Metric * 1.2) < right.RelativeTime;
        }

        /// <summary>
        /// this method adds a new profile event for provided samples
        /// it also make sure that a profile event does not open and close at the same time (would be ignored by SpeedScope)
        /// </summary>
        private static void AddEvents(List<ProfileEvent> profileEvents, Sample openSample, Sample closeSample, int frameId)
        {
            if (openSample.Depth != closeSample.Depth)
                throw new ArgumentException("Invalid arguments, both samples must be of the same depth");
            if (openSample.RelativeTime == closeSample.RelativeTime + closeSample.Metric)
                throw new ArgumentException("Invalid samples, two samples can not happen at the same time.");

            profileEvents.Add(new ProfileEvent(ProfileEventType.Open, frameId, openSample.RelativeTime, openSample.Depth));
            profileEvents.Add(new ProfileEvent(ProfileEventType.Close, frameId, closeSample.RelativeTime + closeSample.Metric, closeSample.Depth));
        }

        /// <summary>
        /// this method orders the profile events in the order required by SpeedScope
        /// it's just the order of drawing the time graph
        /// </summary>
        internal static IEnumerable<ProfileEvent> OrderForExport(IEnumerable<ProfileEvent> profiles)
        {
            return profiles
                .GroupBy(@event => @event.RelativeTime)
                .OrderBy(group => group.Key)
                .SelectMany(group =>
                {
                    // MakeSureSamplesDoNotOverlap guarantees that samples do NOT overlap
                    // AddEvents guarantees us that there is no event which starts and end at the same time
                    // so we don't need to worry about this edge case here

                    // first of all, we need to emit close events, descending by depth (tool format requires that)
                    var closingDescendingByDepth = group.Where(@event => @event.Type == ProfileEventType.Close).OrderByDescending(@event => @event.Depth);
                    // then we can open new events, ascending by depth (tool format requires that)
                    var openingAscendingByDepth = group.Where(@event => @event.Type == ProfileEventType.Open).OrderBy(@event => @event.Depth);

                    return closingDescendingByDepth.Concat(openingAscendingByDepth);
                });
        }

        private static int CompareSamples(Sample x, Sample y)
        {
            int timeComparison = x.RelativeTime.CompareTo(y.RelativeTime);

            if (timeComparison != 0)
                return timeComparison;

            // in case both samples start at the same time, the one with smaller metric should be the first one
            return x.Metric.CompareTo(y.Metric);
        }

        internal readonly struct ThreadInfo : IEquatable<ThreadInfo>
        {
            private static readonly Regex IdExpression = new Regex(@"\((\d+)\)", RegexOptions.Compiled);

            internal ThreadInfo(string threadFrameName, string processFrameName)
            {
                var threadIdMatch = IdExpression.Match(threadFrameName);
                var processIdMatch = IdExpression.Match(processFrameName);

                Name = threadFrameName;
                Id = threadIdMatch.Success ? int.Parse(threadIdMatch.Groups[1].Value) : 0;
                ProcessId = processIdMatch.Success ? int.Parse(processIdMatch.Groups[1].Value) : 0;
            }

            internal ThreadInfo(string name, int id, int processId)
            {
                Name = name;
                Id = id;
                ProcessId = processId;
            }

            public override string ToString() => Name;

            public bool Equals(ThreadInfo other) => Name == other.Name && Id == other.Id && ProcessId == other.ProcessId;

            public override bool Equals(object obj) => obj is ThreadInfo other && Equals(other);

            public override int GetHashCode() => Name.GetHashCode() ^ Id ^ ProcessId;

            #region private
            internal string Name { get; }
            internal int Id { get; }
            internal int ProcessId { get; }
            #endregion private
        }

        internal readonly struct Sample
        {
            internal Sample(StackSourceCallStackIndex stackIndex, int callerFrameId, double relativeTime, double metric, int depth)
            {
                StackIndex = stackIndex;
                CallerFrameId = callerFrameId;
                RelativeTime = relativeTime;
                Metric = metric;
                Depth = depth;
            }

            public override string ToString() => RelativeTime.ToString(CultureInfo.InvariantCulture);

            #region private
            internal StackSourceCallStackIndex StackIndex { get; }
            internal int CallerFrameId { get; }
            internal double RelativeTime { get; }
            internal double Metric { get; }
            internal int Depth { get; }
            #endregion private
        }

        internal enum ProfileEventType : byte
        {
            Open = 0, Close = 1
        }

        internal readonly struct ProfileEvent
        {
            public ProfileEvent(ProfileEventType type, int frameId, double relativeTime, int depth)
            {
                Type = type;
                FrameId = frameId;
                RelativeTime = relativeTime;
                Depth = depth;
            }

            public override string ToString() => $"{RelativeTime.ToString(CultureInfo.InvariantCulture)} {Type} {FrameId}";

            #region private
            internal ProfileEventType Type { get; }
            internal int FrameId { get; }
            internal double RelativeTime { get; }
            internal int Depth { get; }
            #endregion private
        }

        internal readonly struct FrameInfo
        {
            public FrameInfo(int parentId, string frameName, string category)
            {
                ParentId = parentId;
                Name = frameName;
                Category = category;
            }

            #region private
            internal int ParentId { get; }
            internal string Name { get; }
            internal string Category { get; }
            #endregion private
        }
    }
}
