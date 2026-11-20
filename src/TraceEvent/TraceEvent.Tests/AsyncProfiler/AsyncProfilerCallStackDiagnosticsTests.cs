// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Reflection;

using Microsoft.Diagnostics.Tracing.Computers;

using Xunit;

namespace TraceEventTests
{
    /// <summary>Tests the diagnostics counters and the bounded message log reported by stitching and presentation.</summary>
    public class AsyncProfilerCallStackDiagnosticsTests
    {
        [Fact]
        public void Diagnostics_BoundsRetainedMessages()
        {
            var diagnostics = new AsyncProfilerCallStackDiagnostics();

            for (int i = 0; i < 1_100; i++)
            {
                diagnostics.Note("message " + i);
            }

            Assert.Equal(1_024, diagnostics.Messages.Count);
            Assert.Equal(76, diagnostics.MessagesDropped);
        }

        [Fact]
        public void Diagnostics_ClearAndAddToCoverEveryCounter()
        {
            FieldInfo[] counters = typeof(AsyncProfilerCallStackDiagnostics).GetFields()
                .Where(field => field.IsPublic && !field.IsStatic && field.FieldType == typeof(int))
                .ToArray();
            var source = new AsyncProfilerCallStackDiagnostics();
            foreach (FieldInfo counter in counters)
            {
                counter.SetValue(source, 1);
            }

            var target = new AsyncProfilerCallStackDiagnostics();
            source.AddTo(target);
            Assert.All(counters, counter => Assert.Equal(1, counter.GetValue(target)));

            source.Clear();
            Assert.All(counters, counter => Assert.Equal(0, counter.GetValue(source)));
        }
    }
}
