namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    /// <summary>
    /// A result returned after execution of an Analyzer.
    /// </summary>
    public enum AnalyzerExecutionResult
    {
        Success,
        Fail,
        Skip
    };
}
