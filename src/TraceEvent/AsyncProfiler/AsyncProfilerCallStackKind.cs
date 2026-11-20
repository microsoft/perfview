// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.Diagnostics.Tracing.Computers
{
    /// <summary>The instrumentation family that emitted an async-profiler call stack.</summary>
    public enum AsyncProfilerCallStackKind
    {
        /// <summary>Classic async state machines (V1).</summary>
        StateMachineAsync,

        /// <summary>Runtime-managed async continuations (V2).</summary>
        RuntimeAsync,
    }
}
