// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PerfViewJS
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Diagnostics.Symbols;
    using Microsoft.Diagnostics.Tracing.Stacks;

    public sealed class SourceAwareStackSource : GenericStackSource
    {
        private readonly StackSource inner;

        public SourceAwareStackSource(StackSource inner)
        {
            this.inner = inner;
        }

        public override StackSource BaseStackSource => this.inner.BaseStackSource;

        public override int CallStackIndexLimit => this.inner.CallStackIndexLimit;

        public override int CallFrameIndexLimit => this.inner.CallFrameIndexLimit;

        public override bool SamplesImmutable => this.inner.SamplesImmutable;

        public override int SampleIndexLimit => this.inner.SampleIndexLimit;

        public override double SampleTimeRelativeMSecLimit => this.inner.SampleTimeRelativeMSecLimit;

        public override int ScenarioCount => this.inner.ScenarioCount;

        public override float? SamplingRate => this.inner.SamplingRate;

        public override bool IsGraphSource => this.inner.IsGraphSource;

        public override bool OnlyManagedCodeStacks => this.inner.OnlyManagedCodeStacks;

        public override void ParallelForEach(Action<StackSourceSample> callback, int desiredParallelism = 0)
        {
            this.inner.ParallelForEach(callback, desiredParallelism);
        }

        public override void GetReferences(StackSourceSampleIndex nodeIndex, RefDirection direction, Action<StackSourceSampleIndex> callback)
        {
            this.inner.GetReferences(nodeIndex, direction, callback);
        }

        public override int GetNumberOfFoldedFrames(StackSourceCallStackIndex callStackIndex)
        {
            return this.inner.GetNumberOfFoldedFrames(callStackIndex);
        }

        public override StackSourceSample GetSampleByIndex(StackSourceSampleIndex sampleIndex)
        {
            return this.inner.GetSampleByIndex(sampleIndex);
        }

        public override StackSourceCallStackIndex GetCallerIndex(StackSourceCallStackIndex callStackIndex)
        {
            return this.inner.GetCallerIndex(callStackIndex);
        }

        public override StackSourceFrameIndex GetFrameIndex(StackSourceCallStackIndex callStackIndex)
        {
            return this.inner.GetFrameIndex(callStackIndex);
        }

        public override string GetFrameName(StackSourceFrameIndex frameIndex, bool verboseName)
        {
            return this.inner.GetFrameName(frameIndex, verboseName);
        }

        public override void ForEach(Action<StackSourceSample> callback)
        {
            this.inner.ForEach(callback);
        }

        public override ValueTask<SourceLocation> GetSourceLocation(StackSourceFrameIndex frameIndex)
        {
            throw new NotImplementedException();
        }
    }
}
