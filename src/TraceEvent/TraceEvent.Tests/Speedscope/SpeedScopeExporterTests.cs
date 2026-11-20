using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Stacks;
using Microsoft.Diagnostics.Tracing.Stacks.Formats;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using Xunit;

using static Microsoft.Diagnostics.Tracing.Stacks.StackSourceWriterHelper;

namespace TraceEventTests
{
    public class SpeedScopeStackSourceWriterTests
    {
        [Theory]
        [InlineData("Process (321)", 321, false)]
        [InlineData("Process (321)", 321, true)]
        [InlineData("Unknown", 0, false)]
        [InlineData("Unknown", 0, true)]
        public void EndToEnd(string processName, int expectedProcessId, bool preserveSampleGaps)
        {
            const string ThreadName = "Thread (123)";
            const string MethodName = "A";
            const double relativeTime = 0.1;
            const float metric = 0.1f;

            // we have two samples here:
            // processName -> ThreadName() => MehtodName() starting at 0.1 and lasting 0.1
            // processName -> ThreadName() => MehtodName() starting at 0.2 and lasting 0.1

            var process_1 = new FakeStackSourceSample(
                isLastOnCallStack: false,
                relativeTime: relativeTime,
                metric: metric,
                name: processName,
                frameIndex: (StackSourceFrameIndex)5, // 5 is first non-taken enum value
                stackIndex: (StackSourceCallStackIndex)1, // 1 is first non-taken enum value
                callerIndex: StackSourceCallStackIndex.Invalid);
            var thread_1 = new FakeStackSourceSample(
                isLastOnCallStack: false,
                relativeTime: process_1.RelativeTime,
                metric: process_1.Metric,
                name: ThreadName,
                frameIndex: (StackSourceFrameIndex)6,
                stackIndex: (StackSourceCallStackIndex)2,
                callerIndex: process_1.StackIndex);
            var a_1 = new FakeStackSourceSample(
                isLastOnCallStack: true,
                relativeTime: thread_1.RelativeTime,
                metric: thread_1.Metric,
                name: MethodName,
                frameIndex: (StackSourceFrameIndex)7,
                stackIndex: (StackSourceCallStackIndex)3,
                callerIndex: thread_1.StackIndex);

            var process_2 = new FakeStackSourceSample(
                isLastOnCallStack: false,
                relativeTime: relativeTime * 2,
                metric: metric,
                name: processName,
                frameIndex: (StackSourceFrameIndex)5, // 5 is first non-taken enum value
                stackIndex: (StackSourceCallStackIndex)1, // 1 is first non-taken enum value
                callerIndex: StackSourceCallStackIndex.Invalid);
            var thread_2 = new FakeStackSourceSample(
                isLastOnCallStack: false,
                relativeTime: process_2.RelativeTime,
                metric: process_2.Metric,
                name: ThreadName,
                frameIndex: (StackSourceFrameIndex)6,
                stackIndex: (StackSourceCallStackIndex)4,
                callerIndex: process_2.StackIndex);
            var a_2 = new FakeStackSourceSample(
                isLastOnCallStack: true,
                relativeTime: thread_2.RelativeTime,
                metric: thread_2.Metric,
                name: MethodName,
                frameIndex: (StackSourceFrameIndex)7,
                stackIndex: (StackSourceCallStackIndex)5,
                callerIndex: thread_2.StackIndex);

            var sourceSamples = new[] { process_1, thread_1, a_1, process_2, thread_2, a_2 };

            var stackSource = new StackSourceStub(sourceSamples);

            var leafs = GetSortedSamplesPerThread(stackSource)[new ThreadInfo(ThreadName, 123, expectedProcessId)];

            Assert.Equal(relativeTime, leafs[0].RelativeTime);
            Assert.Equal(metric, leafs[0].Metric);
            Assert.Equal(relativeTime * 2, leafs[1].RelativeTime);
            Assert.Equal(metric, leafs[1].Metric);

            var frameNameToId = new Dictionary<string, int>();
            var profileEvents = GetProfileEvents(stackSource, leafs, frameNameToId,
                new Dictionary<int, FrameInfo>(), preserveSampleGaps);
            double expectedEnd = preserveSampleGaps
                ? a_2.RelativeTime + a_2.Metric
                : a_1.RelativeTime + a_1.Metric + a_2.Metric;

            // first: opening the Process at time 0.1
            var openProcess = profileEvents[0];
            Assert.Equal(ProfileEventType.Open, openProcess.Type);
            Assert.Equal(process_1.RelativeTime, openProcess.RelativeTime);
            Assert.Equal(0, openProcess.Depth);
            Assert.Equal(0, openProcess.FrameId);
            Assert.Equal(openProcess.FrameId, frameNameToId[process_1.Name]);

            // second: opening the Thread at time 0.1
            var openThread = profileEvents[1];
            Assert.Equal(ProfileEventType.Open, openThread.Type);
            Assert.Equal(thread_1.RelativeTime, openThread.RelativeTime);
            Assert.Equal(1, openThread.Depth);
            Assert.Equal(1, openThread.FrameId);
            Assert.Equal(openThread.FrameId, frameNameToId[thread_1.Name]);

            // third: opening the A method at time 0.1
            var openMethod = profileEvents[2];
            Assert.Equal(ProfileEventType.Open, openMethod.Type);
            Assert.Equal(a_1.RelativeTime, openMethod.RelativeTime);
            Assert.Equal(2, openMethod.Depth);
            Assert.Equal(2, openMethod.FrameId);
            Assert.Equal(openMethod.FrameId, frameNameToId[a_1.Name]);

            // fourth: close the A method at time 0.3 (relativeTime * 2 + metric)
            var closeMethod = profileEvents[3];
            Assert.Equal(ProfileEventType.Close, closeMethod.Type);
            Assert.Equal(expectedEnd, closeMethod.RelativeTime);
            Assert.Equal(2, closeMethod.Depth);
            Assert.Equal(2, closeMethod.FrameId);
            Assert.Equal(closeMethod.FrameId, frameNameToId[a_2.Name]);

            // fifth: close the Thread at time 0.3 (relativeTime * 2 + metric)
            var closeThread = profileEvents[4];
            Assert.Equal(ProfileEventType.Close, closeThread.Type);
            Assert.Equal(expectedEnd, closeThread.RelativeTime);
            Assert.Equal(1, closeThread.Depth);
            Assert.Equal(1, closeThread.FrameId);
            Assert.Equal(closeThread.FrameId, frameNameToId[thread_2.Name]);

            // sixth: close the Process at time 0.3 (relativeTime * 2 + metric)
            var closeProcess = profileEvents[5];
            Assert.Equal(ProfileEventType.Close, closeProcess.Type);
            Assert.Equal(expectedEnd, closeProcess.RelativeTime);
            Assert.Equal(0, closeProcess.Depth);
            Assert.Equal(0, closeProcess.FrameId);
            Assert.Equal(closeProcess.FrameId, frameNameToId[process_2.Name]);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void HandleSamples_AddsContinuousSamples(bool preserveSampleGaps)
        {
            var results = new List<ProfileEvent>();
            var previousSamples = new List<Sample>();
            var currentSamples = new List<Sample>();

            const double startTime = 0.0;
            const int sampleCount = 100;
            const float metric = 0.01f;
            float metricSum = 0.0f;

            for (int i = 0; i < sampleCount; i++)
            {
                double time = startTime + i * (double)metric;
                currentSamples.Add(new Sample((StackSourceCallStackIndex)1,
                    relativeTime: time, metric, depth: 0, frameId: 0));
                HandleSamples(previousSamples, currentSamples, results, preserveSampleGaps);
                metricSum += metric;
            }

            // we have simple opened profile event 
            Assert.Equal(ProfileEventType.Open, results.Single().Type);
            Assert.Equal(startTime, results.Single().RelativeTime);
            Assert.Equal(preserveSampleGaps ? sampleCount * (double)metric : metricSum,
                previousSamples.Single().EndTime);
        }

        [Theory]
        [InlineData(1.0005, false)]
        [InlineData(1.25, true)]
        public void SampleCoverageGaps_CloseAndReopenEveryFrame(double nextTime, bool differentLeaf)
        {
            IReadOnlyList<ProfileEvent> events = GetEvents(
                (0.0, 1.0f, "A"), (nextTime, 1.0f, differentLeaf ? "B" : "A"));

            Assert.True(Validate(events));
            Assert.Equal(16, events.Count);
            Assert.All(events.Skip(4).Take(4), e =>
            {
                Assert.Equal(ProfileEventType.Close, e.Type);
                Assert.Equal(1.0, e.RelativeTime);
            });
            Assert.Equal(new[] { 3, 2, 1, 0 }, events.Skip(4).Take(4).Select(e => e.Depth));
            Assert.All(events.Skip(8).Take(4), e =>
            {
                Assert.Equal(ProfileEventType.Open, e.Type);
                Assert.Equal(nextTime, e.RelativeTime);
            });
            Assert.Equal(new[] { 0, 1, 2, 3 }, events.Skip(8).Take(4).Select(e => e.Depth));
            Assert.All(events.Skip(12), e => Assert.Equal(nextTime + 1.0, e.RelativeTime));
        }

        [Fact]
        public void SampleCoverageGaps_AccumulatedWeightsCannotKeepAncestorsOpen()
        {
            IReadOnlyList<ProfileEvent> events = GetEvents(
                (0.0, 1.0f, "A"), (0.9, 1.0f, "A"), (1.8, 1.0f, "A"),
                (2.7, 1.0f, "B"), (3.8, 1.0f, "B"));

            Assert.True(Validate(events));
            ProfileEvent[] gap = events.Where(e => e.RelativeTime >= 3.7 && e.RelativeTime <= 3.8).ToArray();
            Assert.Equal(8, gap.Length);
            Assert.All(gap.Take(4), e =>
            {
                Assert.Equal(ProfileEventType.Close, e.Type);
                Assert.Equal(3.7, e.RelativeTime);
            });
            Assert.All(gap.Skip(4), e =>
            {
                Assert.Equal(ProfileEventType.Open, e.Type);
                Assert.Equal(3.8, e.RelativeTime);
            });
            Assert.Equal(4.8, events.Last().RelativeTime);
        }

        [Theory]
        [InlineData(1.0)]
        [InlineData(0.9)]
        public void TouchingOrOverlappingSamples_ExtendToTheLastCoveredEndpoint(double spacing)
        {
            IReadOnlyList<ProfileEvent> events = GetEvents(
                (0.0, 1.0f, "A"), (spacing, 1.0f, "A"), (2 * spacing, 1.0f, "A"));

            Assert.True(Validate(events));
            Assert.Equal(8, events.Count);
            Assert.All(events.Take(4), e => Assert.Equal(0.0, e.RelativeTime));
            Assert.All(events.Skip(4), e => Assert.Equal(2 * spacing + 1.0, e.RelativeTime));
        }

        [Fact]
        public void TouchingSamples_DoNotAcquireGapsFromEndpointRounding()
        {
            double start = 1.0 / (1L << 53);
            double nextStart = 2 * start;
            IReadOnlyList<ProfileEvent> events = GetEvents(
                (start, 1.0f, "A"), (nextStart, 1.0f, "A"), (nextStart + 1.0, 1.0f, "A"));

            Assert.True(Validate(events));
            Assert.Equal(8, events.Count);
            Assert.All(events.Skip(4), e => Assert.Equal(nextStart + 1.0 + 1.0, e.RelativeTime));
        }

        [Fact]
        public void NestedOverlappingSamples_PreserveCoverageAndMonotonicEvents()
        {
            IReadOnlyList<ProfileEvent> events = GetEvents(
                (0.0, 5.0f, "A"), (1.0, 1.0f, "B"), (2.0, 1.0f, "B"), (6.0, 1.0f, "A"));

            Assert.True(Validate(events));
            ProfileEvent[] gap = events.Where(e => e.RelativeTime >= 5.0 && e.RelativeTime <= 6.0).ToArray();
            Assert.Equal(10, gap.Length);
            Assert.All(gap.Take(6), e => Assert.Equal(5.0, e.RelativeTime));
            Assert.All(gap.Skip(6), e =>
            {
                Assert.Equal(ProfileEventType.Open, e.Type);
                Assert.Equal(6.0, e.RelativeTime);
            });
            Assert.Equal(7.0, events.Last().RelativeTime);
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void PublicWriterOverloads_PreserveLegacyDefaultAndAllowSampleGaps(bool chromium, bool compress)
        {
            StackSource source = CreateSource((0.0, 1.0f, "A"), (1.0005, 1.0f, "A"));
            string path = Path.GetTempFileName();
            string output = compress ? path + ".gz" : path;
            try
            {
                if (chromium)
                {
                    ChromiumStackSourceWriter.WriteStackViewAsJson(source, path, compress);
                }
                else
                {
                    SpeedScopeStackSourceWriter.WriteStackViewAsJson(source, path);
                }
                string legacy = ReadExport(output, compress);

                if (chromium)
                {
                    ChromiumStackSourceWriter.WriteStackViewAsJson(source, path, compress, preserveSampleGaps: false);
                }
                else
                {
                    SpeedScopeStackSourceWriter.WriteStackViewAsJson(source, path, preserveSampleGaps: false);
                }
                Assert.Equal(legacy, ReadExport(output, compress));
                using (JsonDocument document = JsonDocument.Parse(legacy))
                {
                    JsonElement events = chromium ? document.RootElement.GetProperty("traceEvents")
                        : document.RootElement.GetProperty("profiles")[0].GetProperty("events");
                    Assert.Equal(8, events.GetArrayLength());
                    Assert.Equal(chromium ? 2000.0 : 2.0, events[4].GetProperty(chromium ? "ts" : "at").GetDouble());
                }

                if (chromium)
                {
                    ChromiumStackSourceWriter.WriteStackViewAsJson(source, path, compress, preserveSampleGaps: true);
                }
                else
                {
                    SpeedScopeStackSourceWriter.WriteStackViewAsJson(source, path, preserveSampleGaps: true);
                }
                using (JsonDocument document = JsonDocument.Parse(ReadExport(output, compress)))
                {
                    JsonElement events = chromium ? document.RootElement.GetProperty("traceEvents")
                        : document.RootElement.GetProperty("profiles")[0].GetProperty("events");
                    Assert.Equal(16, events.GetArrayLength());
                    string timeProperty = chromium ? "ts" : "at";
                    double units = chromium ? 1000.0 : 1.0;
                    Assert.Equal(units, events[4].GetProperty(timeProperty).GetDouble());
                    Assert.Equal(1.0005 * units, events[8].GetProperty(timeProperty).GetDouble());
                    Assert.Equal((1.0005 + 1.0) * units, events[12].GetProperty(timeProperty).GetDouble());
                }
            }
            finally
            {
                File.Delete(output);
                if (output != path)
                {
                    File.Delete(path);
                }
            }
        }

        [Fact]
        public void HandleSamples_DetectsBreaks_Time()
        {
            var results = new List<ProfileEvent>();
            var previousSamples = new List<Sample>();
            var currentSamples = new List<Sample>();

            const double firstTime = 0.0;
            const double secondTime = 1.0;
            const float metric = 0.01f;

            // lasts from <0.0, 0.01>
            currentSamples.Add(new Sample((StackSourceCallStackIndex)1, relativeTime: firstTime, metric, depth: 0, frameId: 0));
            HandleSamples(previousSamples, currentSamples, results);

            // lasts from <1.0, 1.01>
            currentSamples.Add(new Sample((StackSourceCallStackIndex)1, relativeTime: secondTime, metric, depth: 0, frameId: 0));
            HandleSamples(previousSamples, currentSamples, results);

            Assert.Equal(ProfileEventType.Open, results[0].Type);
            Assert.Equal(firstTime, results[0].RelativeTime);

            Assert.Equal(ProfileEventType.Close, results[1].Type);
            Assert.Equal(firstTime + metric, results[1].RelativeTime);

            Assert.Equal(ProfileEventType.Open, results[2].Type);
            Assert.Equal(secondTime, results[2].RelativeTime);
        }

        [Fact]
        public void HandleSamples_DetectsBreaks_Depth()
        {
            var results = new List<ProfileEvent>();
            var previousSamples = new List<Sample>();
            var currentSamples = new List<Sample>();

            const double firstTime = 0.0;
            const float metric = 0.01f;
            const double secondTime = firstTime + metric;
            const int firstDepth = 0, secondDepth = 1;

            // lasts from <0.0, 0.01>
            currentSamples.Add(new Sample((StackSourceCallStackIndex)1, relativeTime: firstTime, metric, depth: firstDepth, frameId: 0));
            HandleSamples(previousSamples, currentSamples, results);

            // lasts from <0.01, 0.02>
            currentSamples.Add(new Sample((StackSourceCallStackIndex)1, relativeTime: secondTime, metric, depth: secondDepth, frameId: 0)); // depth change
            HandleSamples(previousSamples, currentSamples, results);

            Assert.Equal(ProfileEventType.Open, results[0].Type);
            Assert.Equal(firstTime, results[0].RelativeTime);
            Assert.Equal(firstDepth, results[0].Depth);

            Assert.Equal(ProfileEventType.Close, results[1].Type);
            Assert.Equal(firstTime + metric, results[1].RelativeTime);
            Assert.Equal(firstDepth, results[1].Depth);

            Assert.Equal(ProfileEventType.Open, results[2].Type);
            Assert.Equal(secondTime, results[2].RelativeTime);
            Assert.Equal(secondDepth, results[2].Depth);
        }

        [Fact]
        public void HandleSamples_DetectsBreaks_FrameId()
        {
            var results = new List<ProfileEvent>();
            var previousSamples = new List<Sample>();
            var currentSamples = new List<Sample>();

            const double firstTime = 0.0;
            const float metric = 0.01f;
            const double secondTime = firstTime + metric;
            const int firstFrameId = 0, secondFrameId = 1;

            // lasts from <0.0, 0.01>
            currentSamples.Add(new Sample((StackSourceCallStackIndex)1, relativeTime: firstTime, metric, depth: 0, frameId: firstFrameId));
            HandleSamples(previousSamples, currentSamples, results);

            // lasts from <0.01, 0.02>
            currentSamples.Add(new Sample((StackSourceCallStackIndex)1, relativeTime: secondTime, metric, depth: 0, frameId: secondFrameId)); // frameId change
            HandleSamples(previousSamples, currentSamples, results);

            Assert.Equal(ProfileEventType.Open, results[0].Type);
            Assert.Equal(firstTime, results[0].RelativeTime);
            Assert.Equal(firstFrameId, results[0].FrameId);

            Assert.Equal(ProfileEventType.Close, results[1].Type);
            Assert.Equal(firstTime + metric, results[1].RelativeTime);
            Assert.Equal(firstFrameId, results[1].FrameId);

            Assert.Equal(ProfileEventType.Open, results[2].Type);
            Assert.Equal(secondTime, results[2].RelativeTime);
            Assert.Equal(secondFrameId, results[2].FrameId);
        }

        [Fact]
        public void ValidationDetectsIncompleteResults_NoCloseProfileEvent()
        {
            // there is just open event, but no closing one
            var profileEvent = new ProfileEvent(ProfileEventType.Open, 1, 1.0f, 1);

            Assert.False(Validate(new[] { profileEvent }));
        }

        [Fact]
        public void ValidationDetectsIncompleteResults_NoOpenProfileEvent()
        {
            // there is just close event, but no opening one
            var profileEvent = new ProfileEvent(ProfileEventType.Close, 1, 1.0f, 1);

            Assert.False(Validate(new [] { profileEvent }));
        }

        [Fact]
        public void ValidationDetectsIncompleteResults_DifferentFrameIds()
        {
            var openEvent = new ProfileEvent(ProfileEventType.Open, frameId: 1, 1.0f, 1);
            var closeEvent = new ProfileEvent(ProfileEventType.Close, frameId: openEvent.FrameId + 1, 1.0f, 1);

            Assert.False(Validate(new[] { openEvent, closeEvent }));
        }

        [Fact]
        public void ValidationDetectsIncompleteResults_DifferentDepths()
        {
            var openEvent = new ProfileEvent(ProfileEventType.Open, 1, 1.0f, depth: 1);
            var closeEvent = new ProfileEvent(ProfileEventType.Close, 1, 1.0f, depth: openEvent.Depth + 1);

            Assert.False(Validate(new[] { openEvent, closeEvent }));
        }

        [Fact]
        public void ValidationDetectsIncompleteResults_InvalidOrder()
        {
            var openEvent = new ProfileEvent(ProfileEventType.Open, frameId: 1, 1.0f, depth: 1);
            var closeEvent = new ProfileEvent(ProfileEventType.Close, frameId: 1, 1.0f, depth: 1);

            Assert.False(Validate(new[] { closeEvent, openEvent })); // close and then open
        }

        [Fact]
        public void ValidationDetectsIncompleteResults_InvalidTimeOrder()
        {
            var openEvent = new ProfileEvent(ProfileEventType.Open, frameId: 1, 2.0f, depth: 1);
            var closeEvent = new ProfileEvent(ProfileEventType.Close, frameId: 1, 1.0f, depth: 1);

            Assert.False(Validate(new[] { openEvent, closeEvent })); // close and then open
        }

        [Fact]
        public void ValidationAllowsForCompleteResults()
        {
            var openEvent = new ProfileEvent(ProfileEventType.Open, frameId: 1, 1.0f, depth: 1);
            var closeEvent = new ProfileEvent(ProfileEventType.Close, frameId: 1, 1.0f, depth: 1);

            Assert.True(Validate(new[] { openEvent, closeEvent }));
        }

        [Theory]
        [InlineData("HeartRateMonitor.10068.nettrace.zip")]
        [InlineData("VoiceMemo.23092.nettrace.zip")]
        [InlineData("mixed_managed_external_samples.nettrace.zip")]
        [InlineData("only_managed_samples.nettrace.zip")]
        public void CanConvertProvidedTraceFiles(string zippedTraceFileName)
        {
            string fileToUnzip = Path.Combine("inputs", "speedscope", zippedTraceFileName);
            string unzippedFile = Path.ChangeExtension(fileToUnzip, string.Empty);

            if (File.Exists(unzippedFile))
            {
                File.Delete(unzippedFile);
            }
            ZipFile.ExtractToDirectory(fileToUnzip, Path.GetDirectoryName(fileToUnzip));
            var etlxFilePath = TraceLog.CreateFromEventPipeDataFile(unzippedFile, null, new TraceLogOptions() { ContinueOnError = true });

            try
            {
                
                using (var symbolReader = new SymbolReader(TextWriter.Null) { SymbolPath = SymbolPath.MicrosoftSymbolServerPath })
                using (var eventLog = new TraceLog(etlxFilePath))
                {
                    var stackSource = new MutableTraceEventStackSource(eventLog)
                    {
                        OnlyManagedCodeStacks = true // EventPipe currently only has managed code stacks.
                    };

                    var computer = new SampleProfilerThreadTimeComputer(eventLog, symbolReader)
                    {
                        IncludeEventSourceEvents = false // SpeedScope handles only CPU samples, events are not supported
                    };
                    computer.GenerateThreadTimeStacks(stackSource);

                    var samplesPerThread = GetSortedSamplesPerThread(stackSource);

                    Assert.NotEmpty(samplesPerThread);

                    var exportedFrameNameToExportedFrameId = new Dictionary<string, int>();
                    var exportedFrameIdToFrameTuple = new Dictionary<int, FrameInfo>();
                    var profileEventsPerThread = new Dictionary<string, IReadOnlyList<ProfileEvent>>();

                    foreach (var pair in samplesPerThread)
                    {
                        var sortedProfileEvents = GetProfileEvents(stackSource, pair.Value, exportedFrameNameToExportedFrameId, exportedFrameIdToFrameTuple);

                        Assert.True(Validate(sortedProfileEvents), "The output should be always valid");

                        profileEventsPerThread.Add(pair.Key.Name, sortedProfileEvents);
                    };
                }
            }
            finally
            {
                if (File.Exists(etlxFilePath))
                {
                    File.Delete(etlxFilePath);
                }
                if (File.Exists(unzippedFile))
                {
                    File.Delete(unzippedFile);
                }
            }
        }

        [Theory]
        [InlineData("simple", "simple")]
        [InlineData(@"Process64 Test (1) Args:  -r:C:\Test\", @"Process64 Test (1) Args:  -r:C:\\Test\\")]
        [InlineData("CPU Wait > 10ms", "CPU Wait \\u003e 10ms")]
        [InlineData("methodName(value class argument&,float32)", "methodName(value class argument\\u0026,float32)")]
        [InlineData("IEqualityComparer`1<!0>", "IEqualityComparer`1\\u003c!0\\u003e")]
        public void NamesGetEscaped(string name, string expected)
        {
            Dictionary<string, string> escapedNames = new Dictionary<string, string>();

            Assert.Equal(expected, GetEscaped(name, escapedNames));
            Assert.True(escapedNames.ContainsKey(name));
            Assert.Equal(expected, escapedNames[name]);
        }

        #region private
        private static IReadOnlyList<ProfileEvent> GetEvents(params (double Time, float Metric, string Leaf)[] samples)
        {
            StackSource source = CreateSource(samples);
            List<Sample> leafs = Assert.Single(GetSortedSamplesPerThread(source)).Value;
            return GetProfileEvents(source, leafs, new Dictionary<string, int>(),
                new Dictionary<int, FrameInfo>(), preserveSampleGaps: true);
        }

        private static string ReadExport(string path, bool compress)
        {
            using (Stream stream = File.OpenRead(path))
            using (var reader = new StreamReader(compress ? new GZipStream(stream, CompressionMode.Decompress) : stream))
            {
                return reader.ReadToEnd();
            }
        }

        private static StackSource CreateSource(params (double Time, float Metric, string Leaf)[] samples)
        {
            var records = new List<FakeStackSourceSample>
            {
                new FakeStackSourceSample(false, 0, 0, "Process (321)", (StackSourceFrameIndex)5,
                    (StackSourceCallStackIndex)1, StackSourceCallStackIndex.Invalid),
                new FakeStackSourceSample(false, 0, 0, "Thread (123)", (StackSourceFrameIndex)6,
                    (StackSourceCallStackIndex)2, (StackSourceCallStackIndex)1),
                new FakeStackSourceSample(false, 0, 0, "Parent", (StackSourceFrameIndex)7,
                    (StackSourceCallStackIndex)3, (StackSourceCallStackIndex)2),
            };
            for (int i = 0; i < samples.Length; i++)
            {
                records.Add(new FakeStackSourceSample(true, samples[i].Time, samples[i].Metric, samples[i].Leaf,
                    (StackSourceFrameIndex)(8 + i), (StackSourceCallStackIndex)(4 + i), (StackSourceCallStackIndex)3));
            }
            return new StackSourceStub(records);
        }

        internal class FakeStackSourceSample
        {
            public FakeStackSourceSample(bool isLastOnCallStack, double relativeTime, float metric, string name, StackSourceFrameIndex frameIndex,
                StackSourceCallStackIndex stackIndex, StackSourceCallStackIndex callerIndex)
            {
                IsLastOnCallStack = isLastOnCallStack;
                RelativeTime = relativeTime;
                Metric = metric;
                Name = name;
                FrameIndex = frameIndex;
                StackIndex = stackIndex;
                CallerIndex = callerIndex;
            }

            #region private
            public bool IsLastOnCallStack { get; }
            public double RelativeTime { get; }
            public float Metric { get; }
            public string Name { get; }
            public StackSourceFrameIndex FrameIndex { get; }
            public StackSourceCallStackIndex StackIndex { get; }
            public StackSourceCallStackIndex CallerIndex { get; }
            #endregion private
        }

        internal class StackSourceStub : StackSource
        {
            public StackSourceStub(IReadOnlyList<FakeStackSourceSample> fakeStackSourceSamples) => this.samples = fakeStackSourceSamples;

            public override int CallStackIndexLimit => samples.Count;

            public override int CallFrameIndexLimit => samples.Count;

            public override void ForEach(Action<StackSourceSample> callback)
            {
                foreach (var stackSourceSample in samples.Where(s => s.IsLastOnCallStack))
                {
                    callback(new StackSourceSample(this)
                    {
                        TimeRelativeMSec = stackSourceSample.RelativeTime,
                        Metric = stackSourceSample.Metric,
                        StackIndex = stackSourceSample.StackIndex
                    });
                }
            }

            public override StackSourceCallStackIndex GetCallerIndex(StackSourceCallStackIndex callStackIndex)
                => samples.First(sample => sample.StackIndex == callStackIndex).CallerIndex;

            public override StackSourceFrameIndex GetFrameIndex(StackSourceCallStackIndex callStackIndex)
                => samples.First(sample => sample.StackIndex == callStackIndex).FrameIndex;

            public override string GetFrameName(StackSourceFrameIndex frameIndex, bool verboseName)
                => samples.First(sample => sample.FrameIndex == frameIndex).Name;

            #region private
            private readonly IReadOnlyList<FakeStackSourceSample> samples;
            #endregion private
        }
        #endregion private
    }
}
