// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Microsoft.Diagnostics.Tracing.Computers;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;

using Xunit;

namespace TraceEventTests
{
    /// <summary>Tests the stitched-frame value type: storage layout, factories, equality, and presentation changes.</summary>
    public class AsyncProfilerStitchedFrameTests
    {
        [Fact]
        public void StitchedFrame_BoxCarrierMetadata_PreservesCompactStorage()
        {
            Assert.Equal(4 * sizeof(int),
                System.Runtime.CompilerServices.Unsafe.SizeOf<AsyncProfilerStitchSyncFrame>());
            Assert.Equal(6 * sizeof(int) + 2 * IntPtr.Size,
                System.Runtime.CompilerServices.Unsafe.SizeOf<AsyncProfilerStitchedFrame>());
        }

        [Fact]
        public void StitchedFrame_EqualityPreservesStructuralIdentityAndLabelValues()
        {
            AsyncProfilerCallStack callStack = CallStack(frameCount: 2);
            AsyncProfilerCallStack otherCallStack = CallStack(frameCount: 2);
            var sync = new AsyncProfilerStitchSyncFrame(CA(40), MI(41),
                AsyncProfilerStitchSyncFrameKind.None, MI(30));
            AsyncProfilerStitchedFrame frame = AsyncProfilerStitchedFrame.CreateSync(sync, callStack);
            AsyncProfilerStitchedFrame equal = AsyncProfilerStitchedFrame.CreateSync(sync, callStack);
            Assert.True(frame.Equals(equal));
            Assert.True(frame.Equals((object)equal));
            Assert.Equal(frame.GetHashCode(), equal.GetHashCode());
            Assert.False(frame.Equals(null));
            Assert.False(frame.Equals((object)sync));

            var distinct = new[]
            {
                AsyncProfilerStitchedFrame.CreateSync(
                    new AsyncProfilerStitchSyncFrame(CA(42), MI(41),
                        AsyncProfilerStitchSyncFrameKind.None, MI(30)), callStack),
                AsyncProfilerStitchedFrame.CreateSync(
                    new AsyncProfilerStitchSyncFrame(CA(40), MI(42),
                        AsyncProfilerStitchSyncFrameKind.None, MI(30)), callStack),
                AsyncProfilerStitchedFrame.CreateSync(
                    new AsyncProfilerStitchSyncFrame(CA(40), MI(41),
                        AsyncProfilerStitchSyncFrameKind.V1StateMachineBody, MI(30)), callStack),
                AsyncProfilerStitchedFrame.CreateSync(
                    new AsyncProfilerStitchSyncFrame(CA(40), MI(41),
                        AsyncProfilerStitchSyncFrameKind.None, MI(31)), callStack),
                AsyncProfilerStitchedFrame.CreateSync(sync, otherCallStack),
                frame.WithPresentation(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames),
            };
            Assert.All(distinct, candidate => Assert.False(frame.Equals(candidate)));
            Assert.False(AsyncProfilerStitchedFrame.CreateAsyncCurrent(CA(40), MI(41), callStack, 0)
                .Equals(AsyncProfilerStitchedFrame.CreateAsyncCurrent(CA(40), MI(41), callStack, 1)));
            Assert.False(AsyncProfilerStitchedFrame.CreateAsync(callStack, 0)
                .Equals(AsyncProfilerStitchedFrame.CreateAsyncContextParent(callStack, 0)));

            AsyncProfilerStitchedFrame label = AsyncProfilerStitchedFrame.CreateSynthetic("Label");
            AsyncProfilerStitchedFrame equalLabel =
                AsyncProfilerStitchedFrame.CreateSynthetic(new string("Label".ToCharArray()));
            Assert.True(label.Equals(equalLabel));
            Assert.Equal(label.GetHashCode(), equalLabel.GetHashCode());
            Assert.False(label.Equals(AsyncProfilerStitchedFrame.CreateSynthetic("label")));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void StitchedFrame_WithPresentationPreservesReferencePayload(bool synthetic)
        {
            AsyncProfilerCallStack callStack = CallStack(frameCount: 1);
            AsyncProfilerStitchedFrame frame = synthetic
                ? AsyncProfilerStitchedFrame.CreateSynthetic("Label")
                : AsyncProfilerStitchedFrame.CreateAsyncCurrent(CA(40), MI(41), callStack, 0, MI(30));
            AsyncProfilerStitchedFrame changed =
                frame.WithPresentation(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames);

            Assert.Equal(AsyncProfilerStitchedFramePresentation.AsyncDispatcherFrames, changed.Presentation);
            Assert.Same(frame.Segment, changed.Segment);
            Assert.Same(frame.DisplayName, changed.DisplayName);
            Assert.Equal(frame, changed.WithPresentation(frame.Presentation));
        }

        [Fact]
        public void StitchedFrame_CurrentFactory_BoxIdentityCreatesAResumptionTarget()
        {
            AsyncProfilerCallStack segment = CallStack(frameCount: 1);
            AsyncProfilerStitchedFrame frame = AsyncProfilerStitchedFrame.CreateAsyncCurrent(
                CA(40), MI(40), segment, 0, MI(30));

            Assert.Equal(AsyncProfilerStitchedFrameOrigin.AsyncDispatchTarget, frame.Origin);
            Assert.True(frame.IsStateMachineBoxCarrier);
            Assert.Equal(CA(40), frame.CodeAddress);
            Assert.Equal(MI(40), frame.Method);
            Assert.Equal(MI(30), frame.BoxedStateMachineMethod);
        }

        #region private

        private static CodeAddressIndex CA(int value) => (CodeAddressIndex)value;

        private static MethodIndex MI(int value) => (MethodIndex)value;

        private static AsyncProfilerCallStack CallStack(int frameCount) =>
            AsyncProfilerStackTestData.CallStack(new AsyncProfilerCallStackFrames(
                AsyncCallstackKind.StateMachineAsync, new ulong[frameCount], new int[frameCount]));

        #endregion
    }
}
