// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Microsoft.Diagnostics.Tracing.Etlx;

namespace Microsoft.Diagnostics.Tracing.Computers
{
    /// <summary>
    /// Identifies the OS thread (within a process instance) an async call stack ran on. OS process ids can be
    /// reused within a trace, so the unique <see cref="Etlx.ProcessIndex"/> is part of the key.
    /// </summary>
    internal readonly struct AsyncProfilerThreadKey : IEquatable<AsyncProfilerThreadKey>
    {
        /// <summary>Creates a key for a process instance and OS thread.</summary>
        public AsyncProfilerThreadKey(ProcessIndex processIndex, ulong osThreadId)
        {
            ProcessIndex = processIndex;
            OsThreadId = osThreadId;
        }

        /// <summary>The unique process instance in the owning trace, not its reusable OS process id.</summary>
        public readonly ProcessIndex ProcessIndex;
        /// <summary>The OS thread id recorded in the async-profiler buffer header.</summary>
        public readonly ulong OsThreadId;

        public bool Equals(AsyncProfilerThreadKey other) => ProcessIndex == other.ProcessIndex && OsThreadId == other.OsThreadId;
        public override bool Equals(object obj) => obj is AsyncProfilerThreadKey o && Equals(o);
        public override int GetHashCode() => ((int)ProcessIndex * 397) ^ OsThreadId.GetHashCode();
        public override string ToString() => "processIndex=" + ProcessIndex + " tid=" + OsThreadId;
    }
}
