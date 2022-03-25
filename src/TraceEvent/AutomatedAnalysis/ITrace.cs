using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    public static class StackTypes
    {
        public static string CPU { get; } = "CPU";
    }

    public interface ITrace
    {
        IEnumerable<AnalyzerTraceProcess> Processes { get; }

        [Obsolete]
        StackView GetCPUStacks(AnalyzerTraceProcess process);

        StackView GetStacks(AnalyzerTraceProcess process, string stackType);
    }
}
