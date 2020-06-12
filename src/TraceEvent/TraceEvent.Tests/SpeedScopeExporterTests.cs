using Microsoft.Diagnostics.Tracing.Stacks;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

using static Microsoft.Diagnostics.Tracing.Stacks.StackSourceWriterHelper;

namespace TraceEventTests
{
    public class SpeedScopeStackSourceWriterTests
    {
        [Theory]
        [InlineData("Process (321)", 321)]
        [InlineData("Unknown", 0)]
        public void GetSortedSamplesReturnsSamplesSortedByRelativeTimeAndGrouppedByThreadWithProcessInfo(string processName, int expectedProcessId)
        {
            const string ThreadName = "Thread (123)";

            var process = new FakeStackSourceSample(
                relativeTime: 0.1,
                name: processName,
                frameIndex: (StackSourceFrameIndex)5, // 5 is first non-taken enum value
                stackIndex: (StackSourceCallStackIndex)1, // 1 is first non-taken enum value
                callerIndex: StackSourceCallStackIndex.Invalid);
            var thread_1 = new FakeStackSourceSample(
                relativeTime: 0.1,
                name: ThreadName,
                frameIndex: (StackSourceFrameIndex)6,
                stackIndex: (StackSourceCallStackIndex)2,
                callerIndex: process.StackIndex);
            var a_1 = new FakeStackSourceSample(
                relativeTime: 0.1,
                name: "A",
                frameIndex: (StackSourceFrameIndex)7,
                stackIndex: (StackSourceCallStackIndex)3,
                callerIndex: thread_1.StackIndex);
            var thread_2 = new FakeStackSourceSample(
                relativeTime: 0.2,
                name: ThreadName,
                frameIndex: (StackSourceFrameIndex)6,
                stackIndex: (StackSourceCallStackIndex)4,
                callerIndex: process.StackIndex);
            var a_2 = new FakeStackSourceSample(
                relativeTime: 0.2,
                name: "A",
                frameIndex: (StackSourceFrameIndex)7,
                stackIndex: (StackSourceCallStackIndex)5,
                callerIndex: thread_2.StackIndex);

            var sourceSamples = new[] { process, thread_1, thread_2, a_2, a_1 };

            var stackSource = new StackSourceStub(sourceSamples);

            var result = GetSortedSamplesPerThread(stackSource)[new ThreadInfo(ThreadName, 123, expectedProcessId)];

            Assert.Equal(0.1, result[0].RelativeTime);
            Assert.Equal(0.1, result[1].RelativeTime);
            Assert.Equal(0.2, result[2].RelativeTime);
            Assert.Equal(0.2, result[2].RelativeTime);
        }

        [Fact]
        public void WalkTheStackAndExpandSamplesProducesFullInformation()
        {
            // Main() calls A() calls B()
            const double relativeTime = 0.1;
            var main = new FakeStackSourceSample(
                relativeTime: relativeTime,
                name: "Main",
                frameIndex: (StackSourceFrameIndex)5, // 5 is first non-taken enum value
                stackIndex: (StackSourceCallStackIndex)1, // 1 is first non-taken enum value
                callerIndex: StackSourceCallStackIndex.Invalid);
            var a = new FakeStackSourceSample(
                relativeTime: relativeTime,
                name: "A",
                frameIndex: (StackSourceFrameIndex)6,
                stackIndex: (StackSourceCallStackIndex)2,
                callerIndex: main.StackIndex);
            var b = new FakeStackSourceSample(
                relativeTime: relativeTime,
                name: "B",
                frameIndex: (StackSourceFrameIndex)7,
                stackIndex: (StackSourceCallStackIndex)3,
                callerIndex: a.StackIndex);

            var allSamples = new[] { main, a, b };
            var leafs = new[] { new Sample(b.StackIndex, StackSourceCallStackIndex.Invalid, b.RelativeTime, b.Metric, -1) };
            var stackSource = new StackSourceStub(allSamples);
            var frameNameToId = new Dictionary<string, int>();

            var frameIdToSamples = WalkTheStackAndExpandSamples(stackSource, leafs, frameNameToId, new Dictionary<int, FrameInfo>());

            Assert.Equal(0, frameNameToId[main.Name]);
            Assert.Equal(1, frameNameToId[a.Name]);
            Assert.Equal(2, frameNameToId[b.Name]);

            Assert.All(frameIdToSamples.Select(pair => pair.Value), samples => Assert.Equal(relativeTime, samples.Single().RelativeTime));
            Assert.Equal(0, frameIdToSamples[0].Single().Depth);
            Assert.Equal(1, frameIdToSamples[1].Single().Depth);
            Assert.Equal(2, frameIdToSamples[2].Single().Depth);
        }

        [Theory]
        [InlineData(StackSourceFrameIndex.Broken)]
        [InlineData(StackSourceFrameIndex.Invalid)]
        public void WalkTheStackAndExpandSamplesHandlesBrokenStacks(StackSourceFrameIndex kind)
        {
            // Main() calls WRONG
            const double relativeTime = 0.1;
            var main = new FakeStackSourceSample(
                relativeTime: relativeTime,
                name: "Main",
                frameIndex: (StackSourceFrameIndex)5, // 5 is first non-taken enum value
                stackIndex: (StackSourceCallStackIndex)1, // 1 is first non-taken enum value
                callerIndex: StackSourceCallStackIndex.Invalid);
            var wrong = new FakeStackSourceSample(
                relativeTime: relativeTime,
                name: "WRONG",
                frameIndex: kind,
                stackIndex: (StackSourceCallStackIndex)2,
                callerIndex: main.StackIndex);

            var allSamples = new[] { main, wrong };
            var leafs = new[] { new Sample(wrong.StackIndex, StackSourceCallStackIndex.Invalid, wrong.RelativeTime, wrong.Metric, -1) };
            var stackSource = new StackSourceStub(allSamples);
            var frameNameToId = new Dictionary<string, int>();

            var frameIdToSamples = WalkTheStackAndExpandSamples(stackSource, leafs, frameNameToId, new Dictionary<int, FrameInfo>());

            Assert.Equal(0, frameNameToId[main.Name]);
            Assert.False(frameNameToId.ContainsKey(wrong.Name));

            var theOnlySample = frameIdToSamples.Single().Value.Single();
            Assert.Equal(relativeTime, theOnlySample.RelativeTime);
            Assert.Equal(0, theOnlySample.Depth);
        }

        [Fact]
        public void GetAggregatedOrderedProfileEventsConvertsContinuousSamplesWithPausesToMultipleEvents()
        {
            const float metric = 0.1f;

            var samples = new[]
            {
                new Sample((StackSourceCallStackIndex)1, callerStackIndex: 0, metric: metric, depth: 0, relativeTime: 0.1),
                new Sample((StackSourceCallStackIndex)1, callerStackIndex: 0, metric: metric, depth: 0, relativeTime: 0.2),

                new Sample((StackSourceCallStackIndex)1, callerStackIndex: 0, metric: metric, depth: 0, relativeTime: 0.7),

                new Sample((StackSourceCallStackIndex)1, callerStackIndex: 0, metric: metric, depth: 0, relativeTime: 1.1),
                new Sample((StackSourceCallStackIndex)1, callerStackIndex: 0, metric: metric, depth: 0, relativeTime: 1.2),
                new Sample((StackSourceCallStackIndex)1, callerStackIndex: 0, metric: metric, depth: 0, relativeTime: 1.3),
            };

            var input = new Dictionary<int, List<Sample>>() { { 0, samples.ToList() } };

            var aggregatedEvents = GetAggregatedOrderedProfileEvents(input);

            // we should have <0.1, 0.3> and <0.7, 0.8> (the tool would ignore <0.7, 0.7>) and <1.1, 1.4>
            Assert.Equal(6, aggregatedEvents.Count);

            Assert.Equal(0.1f, aggregatedEvents[0].RelativeTime);
            Assert.Equal(ProfileEventType.Open, aggregatedEvents[0].Type);
            Assert.Equal(0.2f + metric, aggregatedEvents[1].RelativeTime);
            Assert.Equal(ProfileEventType.Close, aggregatedEvents[1].Type);

            Assert.Equal(0.7f, aggregatedEvents[2].RelativeTime);
            Assert.Equal(ProfileEventType.Open, aggregatedEvents[2].Type);
            Assert.Equal(0.7f + metric, aggregatedEvents[3].RelativeTime);
            Assert.Equal(ProfileEventType.Close, aggregatedEvents[3].Type);

            Assert.Equal(1.1f, aggregatedEvents[4].RelativeTime);
            Assert.Equal(ProfileEventType.Open, aggregatedEvents[4].Type);
            Assert.Equal(1.3f + metric, aggregatedEvents[5].RelativeTime);
            Assert.Equal(ProfileEventType.Close, aggregatedEvents[5].Type);
        }

        [Fact]
        public void GetAggregatedOrderedProfileEventsConvertsContinuousSamplesWithDifferentDepthToMultipleEvents()
        {
            const float metric = 0.1f;

            var samples = new[]
            {
                new Sample((StackSourceCallStackIndex)1, callerStackIndex: 0, metric: metric, relativeTime: 0.1, depth: 0),
                new Sample((StackSourceCallStackIndex)1, callerStackIndex: 0, metric: metric, relativeTime: 0.2, depth: 1), // depth change!
            };

            var input = new Dictionary<int, List<Sample>>() { { 0, samples.ToList() } };

            var aggregatedEvents = GetAggregatedOrderedProfileEvents(input);

            // we should have:
            //  Open at 0.1 depth 0 and Close 0.2
            //  Open at 0.2 depth 1 and Close 0.3
            Assert.Equal(4, aggregatedEvents.Count);

            Assert.Equal(ProfileEventType.Open, aggregatedEvents[0].Type);
            Assert.Equal(0.1f, aggregatedEvents[0].RelativeTime);
            Assert.Equal(0, aggregatedEvents[0].Depth);

            Assert.Equal(ProfileEventType.Close, aggregatedEvents[1].Type);
            Assert.Equal(0.1f + metric, aggregatedEvents[1].RelativeTime);
            Assert.Equal(0, aggregatedEvents[0].Depth);

            Assert.Equal(ProfileEventType.Open, aggregatedEvents[2].Type);
            Assert.Equal(0.2f, aggregatedEvents[2].RelativeTime);
            Assert.Equal(1, aggregatedEvents[2].Depth);

            Assert.Equal(ProfileEventType.Close, aggregatedEvents[3].Type);
            Assert.Equal(0.2f + metric, aggregatedEvents[3].RelativeTime);
            Assert.Equal(1, aggregatedEvents[3].Depth);
        }

        [Fact]
        public void GetAggregatedOrderedProfileEventsConvertsRecursiveMethodSamplesToMultipleEvents()
        {
            const float metric = 0.1f;
            const double relativeTime = 0.1;

            var samples = new[]
            {
                new Sample((StackSourceCallStackIndex)1, callerStackIndex: 0, metric: metric, relativeTime: relativeTime, depth: 0),
                new Sample((StackSourceCallStackIndex)2, callerStackIndex: (StackSourceCallStackIndex)1, metric: metric, relativeTime: relativeTime, depth: 1), // relative time stays the same
                new Sample((StackSourceCallStackIndex)3, callerStackIndex: (StackSourceCallStackIndex)2, metric: metric, relativeTime: relativeTime, depth: 2),
                new Sample((StackSourceCallStackIndex)4, callerStackIndex: (StackSourceCallStackIndex)3, metric: metric, relativeTime: relativeTime, depth: 3),
            };

            var input = new Dictionary<int, List<Sample>>() { { 0, samples.ToList() } };

            var aggregatedEvents = GetAggregatedOrderedProfileEvents(input);

            // open x4
            Assert.Equal(ProfileEventType.Open, aggregatedEvents[0].Type);
            Assert.Equal((float)relativeTime, aggregatedEvents[0].RelativeTime);
            Assert.Equal(0, aggregatedEvents[0].Depth);

            Assert.Equal(ProfileEventType.Open, aggregatedEvents[1].Type);
            Assert.Equal((float)relativeTime, aggregatedEvents[1].RelativeTime);
            Assert.Equal(1, aggregatedEvents[1].Depth);

            Assert.Equal(ProfileEventType.Open, aggregatedEvents[2].Type);
            Assert.Equal((float)relativeTime, aggregatedEvents[2].RelativeTime);
            Assert.Equal(2, aggregatedEvents[2].Depth);

            Assert.Equal(ProfileEventType.Open, aggregatedEvents[3].Type);
            Assert.Equal((float)relativeTime, aggregatedEvents[3].RelativeTime);
            Assert.Equal(3, aggregatedEvents[3].Depth);

            // close x4
            Assert.Equal(ProfileEventType.Close, aggregatedEvents[4].Type);
            Assert.Equal((float)relativeTime + metric, aggregatedEvents[4].RelativeTime);
            Assert.Equal(3, aggregatedEvents[4].Depth);

            Assert.Equal(ProfileEventType.Close, aggregatedEvents[5].Type);
            Assert.Equal((float)relativeTime + metric, aggregatedEvents[5].RelativeTime);
            Assert.Equal(2, aggregatedEvents[5].Depth);

            Assert.Equal(ProfileEventType.Close, aggregatedEvents[6].Type);
            Assert.Equal((float)relativeTime + metric, aggregatedEvents[6].RelativeTime);
            Assert.Equal(1, aggregatedEvents[6].Depth);

            Assert.Equal(ProfileEventType.Close, aggregatedEvents[7].Type);
            Assert.Equal((float)relativeTime + metric, aggregatedEvents[7].RelativeTime);
            Assert.Equal(0, aggregatedEvents[7].Depth);
        }

        [Fact]
        public void GetAggregatedOrderedProfileEventsConvertsContinuousSamplesWithDifferentCallerStackIndexToMultipleEvents()
        {
            const float metric = 0.1f;

            var samples = new[]
            {
                new Sample((StackSourceCallStackIndex)1, metric: metric, relativeTime: 0.1, depth: 0, callerStackIndex: 0),
                new Sample((StackSourceCallStackIndex)1, metric: metric, relativeTime: 0.2, depth: 0, callerStackIndex: (StackSourceCallStackIndex)1), // callerStackIndex change!
            };

            var input = new Dictionary<int, List<Sample>>() { { 0, samples.ToList() } };

            var aggregatedEvents = GetAggregatedOrderedProfileEvents(input);

            // we should have:
            //  Open at 0.1 depth 0 and Close 0.2
            //  Open at 0.2 depth 0 and Close 0.3
            Assert.Equal(4, aggregatedEvents.Count);

            Assert.Equal(ProfileEventType.Open, aggregatedEvents[0].Type);
            Assert.Equal(0.1f, aggregatedEvents[0].RelativeTime);
            Assert.Equal(0, aggregatedEvents[0].Depth);

            Assert.Equal(ProfileEventType.Close, aggregatedEvents[1].Type);
            Assert.Equal(0.1f + metric, aggregatedEvents[1].RelativeTime);
            Assert.Equal(0, aggregatedEvents[0].Depth);

            Assert.Equal(ProfileEventType.Open, aggregatedEvents[2].Type);
            Assert.Equal(0.2f, aggregatedEvents[2].RelativeTime);
            Assert.Equal(0, aggregatedEvents[2].Depth);

            Assert.Equal(ProfileEventType.Close, aggregatedEvents[3].Type);
            Assert.Equal(0.2f + metric, aggregatedEvents[3].RelativeTime);
            Assert.Equal(0, aggregatedEvents[3].Depth);
        }

        [Fact]
        public void CloseMetricCanBeZeroIfItDoesNotCreateAProfileEventThatStartsAndEndsAtTheSameMoment()
        {
            const float metric = 0.1f;

            var samples = new[]
            {
                new Sample((StackSourceCallStackIndex)1, metric: metric, relativeTime: 0.1, depth: 0, callerStackIndex: 0),
                new Sample((StackSourceCallStackIndex)1, metric: metric, relativeTime: 0.2, depth: 0, callerStackIndex: 0),
                new Sample((StackSourceCallStackIndex)1, metric: 0.0f, relativeTime: 0.3, depth: 0, callerStackIndex: 0), // 0.0 metric
            };

            var input = new Dictionary<int, List<Sample>>() { { 0, samples.ToList() } };

            var aggregatedEvents = GetAggregatedOrderedProfileEvents(input);

            // we should have:
            //  Open at 0.1 depth 0 and Close 0.3
            Assert.Equal(2, aggregatedEvents.Count);

            Assert.Equal(ProfileEventType.Open, aggregatedEvents[0].Type);
            Assert.Equal(0.1f, aggregatedEvents[0].RelativeTime);
            Assert.Equal(0, aggregatedEvents[0].Depth);

            Assert.Equal(ProfileEventType.Close, aggregatedEvents[1].Type);
            Assert.Equal(0.3f, aggregatedEvents[1].RelativeTime);
            Assert.Equal(0, aggregatedEvents[0].Depth);
        }

        [Fact]
        public void TwoSamplesCanNotHappenAtTheSameTime()
        {
            const float zeroMetric = 0.0f;
            const double relativeTime = 0.1;

            var samples = new[]
            {
                new Sample((StackSourceCallStackIndex)1, metric: zeroMetric, relativeTime: relativeTime, depth: 0, callerStackIndex: 0), // 0.0 metric
                new Sample((StackSourceCallStackIndex)1, metric: zeroMetric, relativeTime: relativeTime, depth: 0, callerStackIndex: 0), // 0.0 metric and same relative time
            };

            var input = new Dictionary<int, List<Sample>>() { { 0, samples.ToList() } };

            Assert.Throws<ArgumentException>(() => GetAggregatedOrderedProfileEvents(input));
        }

        [Fact]
        public void OrderForExportOrdersTheProfileEventsAsExpectedByTheSpeedScope()
        {
            var profileEvents = new List<ProfileEvent>()
            {
                new ProfileEvent(ProfileEventType.Open, frameId: 0, depth: 0, relativeTime: 0.1f),
                new ProfileEvent(ProfileEventType.Open, frameId: 1, depth: 1, relativeTime: 0.1f),
                new ProfileEvent(ProfileEventType.Close, frameId: 1, depth: 1, relativeTime: 0.3f),
                new ProfileEvent(ProfileEventType.Close, frameId: 0, depth: 0, relativeTime: 0.3f),
                new ProfileEvent(ProfileEventType.Open, frameId: 2, depth: 0, relativeTime: 0.3f),
                new ProfileEvent(ProfileEventType.Close, frameId: 2, depth: 0, relativeTime: 0.4f),
            };

            profileEvents.Reverse(); // reverse to make sure that it does sort the elements in right way

            var ordered = OrderForExport(profileEvents).ToArray();

            Assert.Equal(ProfileEventType.Open, ordered[0].Type);
            Assert.Equal(0.1f, ordered[0].RelativeTime);
            Assert.Equal(0, ordered[0].Depth);
            Assert.Equal(0, ordered[0].FrameId);

            Assert.Equal(ProfileEventType.Open, ordered[1].Type);
            Assert.Equal(0.1f, ordered[1].RelativeTime);
            Assert.Equal(1, ordered[1].Depth);
            Assert.Equal(1, ordered[1].FrameId);

            Assert.Equal(ProfileEventType.Close, ordered[2].Type);
            Assert.Equal(0.3f, ordered[2].RelativeTime);
            Assert.Equal(1, ordered[2].Depth);
            Assert.Equal(1, ordered[2].FrameId);

            Assert.Equal(ProfileEventType.Close, ordered[3].Type);
            Assert.Equal(0.3f, ordered[3].RelativeTime);
            Assert.Equal(0, ordered[3].Depth);
            Assert.Equal(0, ordered[3].FrameId);

            Assert.Equal(ProfileEventType.Open, ordered[4].Type);
            Assert.Equal(0.3f, ordered[4].RelativeTime);
            Assert.Equal(0, ordered[4].Depth);
            Assert.Equal(2, ordered[4].FrameId);

            Assert.Equal(ProfileEventType.Close, ordered[5].Type);
            Assert.Equal(0.4f, ordered[5].RelativeTime);
            Assert.Equal(0, ordered[5].Depth);
            Assert.Equal(2, ordered[5].FrameId);
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
        public void ValidationAllowsForCompleteResults()
        {
            var openEvent = new ProfileEvent(ProfileEventType.Open, frameId: 1, 1.0f, depth: 1);
            var closeEvent = new ProfileEvent(ProfileEventType.Close, frameId: 1, 1.0f, depth: 1);

            Assert.True(Validate(new[] { openEvent, closeEvent }));
        }

        #region private
        internal class FakeStackSourceSample
        {
            public FakeStackSourceSample(double relativeTime, string name)
            {
                RelativeTime = relativeTime;
                Name = name;
            }

            public FakeStackSourceSample(double relativeTime, string name, StackSourceFrameIndex frameIndex,
                StackSourceCallStackIndex stackIndex, StackSourceCallStackIndex callerIndex)
            {
                RelativeTime = relativeTime;
                Name = name;
                FrameIndex = frameIndex;
                StackIndex = stackIndex;
                CallerIndex = callerIndex;
            }

            #region private
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
                foreach (var stackSourceSample in samples)
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
