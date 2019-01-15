// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PerfViewJS
{
    using System.Threading.Tasks;
    using Microsoft.Diagnostics.Symbols;
    using Microsoft.Diagnostics.Tracing.Stacks;

    public abstract class GenericStackSource : StackSource
    {
        public abstract ValueTask<SourceLocation> GetSourceLocation(StackSourceFrameIndex frameIndex);
    }
}
