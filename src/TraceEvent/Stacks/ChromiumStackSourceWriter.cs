using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using static Microsoft.Diagnostics.Tracing.Stacks.StackSourceWriterHelper;

namespace Microsoft.Diagnostics.Tracing.Stacks.Formats
{
    public class ChromiumStackSourceWriter
    {
        /// <summary>
        /// exports provided StackSource to a Chromium Trace File format 
        /// schema: https://docs.google.com/document/d/1CvAClvFfyA5R-PhYUmn5OOQtYMH4h6I0nSsKchNAySU/
        /// </summary>
        public static void WriteStackViewAsJson(StackSource source, string filePath, bool compress)
        {
            if (compress && !filePath.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                filePath += ".gz";

            if (File.Exists(filePath))
                File.Delete(filePath);

            using (var writeStream = compress ? (Stream)new GZipStream(File.Create(filePath), CompressionMode.Compress, leaveOpen: false) : File.Create(filePath))
            using (var streamWriter = new StreamWriter(writeStream))
            {
                Export(source, streamWriter, Path.GetFileNameWithoutExtension(filePath));
            }
        }

        #region private
        private static void Export(StackSource source, TextWriter writer, string name)
        {
            var samplesPerThread = GetSortedSamplesPerThread(source);

            var exportedFrameNameToExportedFrameId = new Dictionary<string, int>();
            var exportedFrameIdToFrameTuple = new Dictionary<int, FrameInfo>();
            var profileEventsPerThread = new Dictionary<ThreadInfo, IReadOnlyList<ProfileEvent>>();

            foreach (var pair in samplesPerThread)
            {
                var frameIdToSamples = WalkTheStackAndExpandSamples(source, pair.Value, exportedFrameNameToExportedFrameId, exportedFrameIdToFrameTuple);

                var sortedProfileEvents = GetAggregatedOrderedProfileEvents(frameIdToSamples);

                profileEventsPerThread.Add(pair.Key, sortedProfileEvents);
            };

            WriteToFile(exportedFrameIdToFrameTuple, profileEventsPerThread, writer, name);
        }

        private static void WriteToFile(Dictionary<int, FrameInfo> frameIdToFrameTuple,
            IReadOnlyDictionary<ThreadInfo, IReadOnlyList<ProfileEvent>> sortedProfileEventsPerThread,
            TextWriter writer, string name)
        {
            writer.Write("{");
            writer.Write("\"traceEvents\": [");
            bool isFirst = true;
            foreach (var perThread in sortedProfileEventsPerThread.OrderBy(pair => pair.Value.First().RelativeTime))
            {
                foreach (var profileEvent in perThread.Value)
                {
                    if (!isFirst)
                        writer.Write(", ");
                    else
                        isFirst = false;

                    writer.Write("{");
                    writer.Write($"\"name\": \"{frameIdToFrameTuple[profileEvent.FrameId].Name}\", ");
                    writer.Write($"\"cat\": \"sampleEvent\", ");
                    writer.Write($"\"ph\": \"{(profileEvent.Type == ProfileEventType.Open ? "B" : "E")}\", ");
                    writer.Write($"\"ts\": {profileEvent.RelativeTime.ToString("R", CultureInfo.InvariantCulture)}, ");
                    writer.Write($"\"pid\": {perThread.Key.ProcessId}, ");
                    writer.Write($"\"tid\": {perThread.Key.Id}, ");
                    writer.Write($"\"sf\": {profileEvent.FrameId}");
                    writer.Write("}");
                }
            }
            writer.Write("], ");
            writer.Write("\"displayTimeUnit\": \"ms\", ");
            writer.Write("\"stackFrames\": {");
            isFirst = true;
            foreach (var frame in frameIdToFrameTuple)
            {
                if (!isFirst)
                    writer.Write(", ");
                else
                    isFirst = false;

                var frameId = frame.Key;
                var frameInfo = frame.Value;
                writer.Write($"\"{frameId}\": {{");
                writer.Write($"\"name\": \"{frameInfo.Name}\", ");
                writer.Write($"\"category\": \"{frameInfo.Category}\"");
                if (frameInfo.ParentId != -1)
                    writer.Write($", \"parent\": {frameInfo.ParentId}");
                writer.Write("}");
            }
            writer.Write("}, ");
            writer.Write($"\"otherData\": {{ \"name\": \"{name}\" }}");
            writer.Write("}");
        }
        #endregion private
    }
}