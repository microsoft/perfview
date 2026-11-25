// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing.Etlx;

using TraceEventBenchmarks;

using Xunit;

namespace TraceEventTests
{
    /// <summary>
    /// Fully validates the synthetic end-to-end benchmark inputs at Realistic scale, so benchmark setup only needs cheap checks.
    /// </summary>
    public class AsyncProfilerEndToEndFixtureTests
    {
        [Theory]
        [InlineData(AsyncProfilerKindMode.V2Only)]
        [InlineData(AsyncProfilerKindMode.V1Only)]
        [InlineData(AsyncProfilerKindMode.Mixed)]
        public void RealisticFixture_ProducesExpectedIndexAndStacks(AsyncProfilerKindMode kindMode)
        {
            using (AsyncProfilerEndToEndFixture fixture =
                AsyncProfilerEndToEndFixture.Create(AsyncProfilerWorkloadProfile.Realistic, kindMode))
            {
                fixture.CreateAllInputs();
                using (var traceLog = new TraceLog(fixture.EtlxPath))
                using (var controlTraceLog = new TraceLog(fixture.ControlEtlxPath))
                using (var symbolReader = new SymbolReader(TextWriter.Null))
                {
                    fixture.Validate(traceLog, controlTraceLog, symbolReader);
                }
            }
        }
    }
}
