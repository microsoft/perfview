using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Microsoft.Diagnostics.Tracing.Stacks
{
    public static class SpeedScopeExporter
    {
        /// <summary>
        /// exports provided StackSource to a https://www.speedscope.app/ format 
        /// </summary>
        public static void WriteStackViewAsJson(StackSource source, string filePath)
        {
            if (File.Exists(filePath))
                File.Delete(filePath);

            using (var writeStream = File.CreateText(filePath))
                Export(source, writeStream, Path.GetFileNameWithoutExtension(filePath));
        }

        #region private
        internal static void Export(StackSource source, TextWriter writer, string name)
        {
            var sortedSamples = GetSortedSamples(source);

            WalkTheStackAndExpandSamples(source, sortedSamples, out var frameNameToId, out var frameIdToSamples);

            var sortedProfileEvents = GetAggregatedSortedProfileEvents(frameIdToSamples);

            var orderedFrameNames = frameNameToId.OrderBy(pair => pair.Value).Select(pair => pair.Key).ToArray();

            WriteToFile(sortedProfileEvents, orderedFrameNames, writer, name);
        }

        /// <summary>
        /// this method gets all samples from StackSource and sorts them by relative time (ascending)
        /// </summary>
        internal static IReadOnlyList<Sample> GetSortedSamples(StackSource stackSource)
        {
            var samples = new List<Sample>(stackSource.CallStackIndexLimit);
            stackSource.ForEach(sample => samples.Add(new Sample(sample.StackIndex, sample.TimeRelativeMSec, sample.Metric, -1)));
            samples.Sort((x, y) => x.RelativeTime.CompareTo(y.RelativeTime));

            return samples;
        }

        /// <summary>
        /// all the samples that we have are leafs (last sample in the call stack)
        /// this method expands those samples to full information 
        /// it walks the stack up to the begining and adds a sample for every method on the stack
        /// it's required to build full information
        /// </summary>
        internal static void WalkTheStackAndExpandSamples(StackSource stackSource, IEnumerable<Sample> leafs, 
            out IReadOnlyDictionary<string, int> frameNamesToIds, out IReadOnlyDictionary<int, List<Sample>> frameIdsToSamples)
        {
            var frameNameToId = new Dictionary<string, int>();
            var frameIdToSamples = new Dictionary<int, List<Sample>>();

            var stackOfFrames = new Stack<StackSourceCallStackIndex>();

            foreach (var leafSample in leafs)
            {
                // walk the stack first
                var stackIndex = leafSample.StackIndex;
                while (stackIndex != StackSourceCallStackIndex.Invalid)
                {
                    stackOfFrames.Push(stackIndex);

                    stackIndex = stackSource.GetCallerIndex(stackIndex);
                }

                // add sample for every method on the stack
                int depth = -1;
                while (stackOfFrames.Count > 0)
                {
                    stackIndex = stackOfFrames.Pop();
                    depth++;

                    var frameIndex = stackSource.GetFrameIndex(stackIndex);
                    if (frameIndex == StackSourceFrameIndex.Broken || frameIndex == StackSourceFrameIndex.Invalid)
                        continue;

                    var frameName = stackSource.GetFrameName(frameIndex, false);
                    if (string.IsNullOrEmpty(frameName))
                        continue;

                    if (!frameNameToId.TryGetValue(frameName, out int exportedFrameId))
                        frameNameToId.Add(frameName, exportedFrameId = frameNameToId.Count);

                    if (!frameIdToSamples.TryGetValue(exportedFrameId, out var samples))
                        frameIdToSamples.Add(exportedFrameId, samples = new List<Sample>());

                    // the time and metric are the same as for the leaf sample
                    // the difference is stack index (not really used from here) and depth (used for sorting the exported data)
                    samples.Add(new Sample(stackIndex, leafSample.RelativeTime, leafSample.Metric, depth));
                }
            }

            frameNamesToIds = frameNameToId;
            frameIdsToSamples = frameIdToSamples;
        }

        /// <summary>
        /// this method aggregates all the singular samples to continuous events
        /// example: samples for Main taken at time 0.1 0.2 0.3 0.4 0.5
        /// are gonna be translated to Main start at 0.1 stop at 0.5
        /// </summary>
        internal static IReadOnlyList<ProfileEvent> GetAggregatedSortedProfileEvents(IReadOnlyDictionary<int, List<Sample>> frameIdToSamples)
        {
            List<ProfileEvent> profileEvents = new List<ProfileEvent>();

            // [0] will be always the Main method, it is our boundry
            var minRelativeTime = frameIdToSamples[0].First().RelativeTime;
            var maxRelativeTime = frameIdToSamples[0].Last().RelativeTime;

            foreach (var samplesInfo in frameIdToSamples)
            {
                var frameId = samplesInfo.Key;
                var samples = samplesInfo.Value;

                // this should not be required, but I prefer to be sure that the data is sorted
                samples.Sort((x, y) => x.RelativeTime.CompareTo(y.RelativeTime));

                Sample openSample = samples[0]; // samples are never empty
                for (int i = 1; i < samples.Count; i++)
                {
                    if (samples[i].RelativeTime > (samples[i - 1].RelativeTime + (samples[i - 1].Metric * 2)) || samples[i].Depth != samples[i - 1].Depth)
                    {
                        CenterAndAdd(profileEvents, openSample, samples[i - 1], frameId, minRelativeTime, maxRelativeTime);

                        openSample = samples[i];
                    }
                }

                // we need to handle the last (or entire) profile event
                CenterAndAdd(profileEvents, openSample, samples[samples.Count - 1], frameId, minRelativeTime, maxRelativeTime);
            }

            // MUST HAVE!!! the tool expects the profile events in certain order!!
            profileEvents.Sort(CompareProfileEvents);

            return profileEvents;
        }

        private static void CenterAndAdd(List<ProfileEvent> profileEvents, Sample openSample, Sample closeSample, int frameId, double minRelativeTime, double maxRelativeTime)
        {
            // we "center" the samples in order to:
            // 1: avoid gaps (having method A on stack at 0.1 and method B at 0.2 would create a gap between 0.1 and 0.2 without it
            // 2. avoid events that Open and Close at the same time for very short method that produced one sample (the tool ignores such events which loses data)
            double openRelativeTime = openSample.RelativeTime != closeSample.RelativeTime ? openSample.RelativeTime : Math.Max(minRelativeTime, openSample.RelativeTime);
            double closeRelativeTime = openSample.RelativeTime != closeSample.RelativeTime ? closeSample.RelativeTime :  Math.Min(maxRelativeTime, closeSample.RelativeTime + closeSample.Metric / 2.0);
            
            profileEvents.Add(new ProfileEvent(ProfileEventType.Open, frameId, openRelativeTime, openSample.Depth));
            profileEvents.Add(new ProfileEvent(ProfileEventType.Close, frameId, closeRelativeTime, closeSample.Depth));
        }

        /// <summary>
        /// allows for sorting of the profile events in the order that SpeedScope expects them to be written to a file
        /// </summary>
        internal static int CompareProfileEvents(ProfileEvent x, ProfileEvent y)
        {
            // first of all, we sort ascending by relative time
            int result = x.RelativeTime.CompareTo(y.RelativeTime);
            if (result != 0)
                return result;

            // if both events are open events, then the one with lower depth goes first (it needs to be opened first)
            if (x.Type == ProfileEventType.Open && y.Type == ProfileEventType.Open)
                return x.Depth - y.Depth;
            // if both events are close events, then the one with bigger depth goes first (it needs to be closed first)
            if (x.Type == ProfileEventType.Close && y.Type == ProfileEventType.Close)
                return y.Depth - x.Depth;

            // they are of different type so we sort them by type (Open = 0 so it goes first)
            return x.Type.CompareTo(y.Type);
        }

        /// <summary>
        /// writes pre-calculated data to given file in a speedscope-friendly format
        /// </summary>
        internal static void WriteToFile(IReadOnlyList<ProfileEvent> sortedProfileEvents, IReadOnlyList<string> orderedFrameNames, TextWriter writer, string name)
        {
            writer.Write("{");
            writer.Write("\"exporter\": \"speedscope@1.3.2\", ");
            writer.Write($"\"name\": \"{name}\", ");
            writer.Write("\"activeProfileIndex\": 0, ");
            writer.Write("\"$schema\": \"https://www.speedscope.app/file-format-schema.json\", ");

            writer.Write("\"shared\": { \"frames\": [ ");
            for (int i = 0; i < orderedFrameNames.Count; i++)
            {
                writer.Write($"{{ \"name\": \"{orderedFrameNames[i].Replace("\\", "\\\\").Replace("\"", "\\\"")}\" }}");

                if (i != orderedFrameNames.Count - 1)
                    writer.Write(", ");
            }
            writer.Write("] }, ");

            writer.Write("\"profiles\": [ {");
            writer.Write("\"type\": \"evented\", ");
            writer.Write($"\"name\": \"{name}\", ");
            writer.Write("\"unit\": \"milliseconds\", ");
            writer.Write($"\"startValue\": \"{sortedProfileEvents.First().RelativeTime.ToString("R", CultureInfo.InvariantCulture)}\", ");
            writer.Write($"\"endValue\": \"{sortedProfileEvents.Last().RelativeTime.ToString("R", CultureInfo.InvariantCulture)}\", ");
            writer.Write("\"events\": [ ");
            for (int i = 0; i < sortedProfileEvents.Count; i++)
            {
                var frameEvent = sortedProfileEvents[i];

                writer.Write($"{{ \"type\": \"{(frameEvent.Type == ProfileEventType.Open ? "O" : "C")}\", ");
                writer.Write($"\"frame\": {frameEvent.FrameId}, ");
                // "R" is crucial here!!! we can't loose precision becasue it can affect the sort order!!!!
                writer.Write($"\"at\": {frameEvent.RelativeTime.ToString("R", CultureInfo.InvariantCulture)} }}");

                if (i != sortedProfileEvents.Count - 1)
                    writer.Write(", ");
            }
            writer.Write("] } ] ");

            writer.Write("}");
        }

        internal struct Sample
        {
            internal Sample(StackSourceCallStackIndex stackIndex, double relativeTime, double metric, int depth)
            {
                StackIndex = stackIndex;
                RelativeTime = relativeTime;
                Metric = metric;
                Depth = depth;
            }

            public override string ToString() => RelativeTime.ToString(CultureInfo.InvariantCulture);

            #region private
            internal StackSourceCallStackIndex StackIndex { get; }
            internal double RelativeTime { get; }
            internal double Metric { get; }
            internal int Depth { get; }
            #endregion private
        }

        internal enum ProfileEventType : byte
        {
            Open = 0, Close = 1 // these values MUST NOT be changed, the sorting order relies on it
        }

        internal struct ProfileEvent
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
        #endregion private
    }
}
