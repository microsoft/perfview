using System.Collections.Generic;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    public interface IAutomatedAnalysisTrace
    {
        IEnumerable<AutomatedAnalysisTraceProcess> Processes { get; }

        StackView GetCPUStacks(AutomatedAnalysisTraceProcess process);
    }
}
