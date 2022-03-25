using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    /// <summary>
    /// A common set of supported stacks.
    /// </summary>
    public static class StackTypes
    {
        /// <summary>
        /// Stacks representing execution on one or more CPUs.
        /// </summary>
        public static string CPU { get; } = "CPU";
    }

    public interface ITrace
    {
        /// <summary>
        /// The set of processes contained within the trace.
        /// </summary>
        IEnumerable<AnalyzerTraceProcess> Processes { get; }

        /// <summary>
        /// Get a StackView containing stacks for the specified process and stack type.
        /// </summary>
        /// <param name="process">The process to filter stacks by.</param>
        /// <param name="stackType">The type of stacks for the request.</param>
        /// <returns>A StackView containing the requested stacks.</returns>
        StackView GetStacks(AnalyzerTraceProcess process, string stackType);
    }
}
