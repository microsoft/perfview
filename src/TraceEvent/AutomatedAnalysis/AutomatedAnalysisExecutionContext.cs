using Microsoft.Diagnostics.Tracing.Etlx;
using System.IO;
using Microsoft.Diagnostics.Symbols;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    public sealed class AutomatedAnalysisExecutionContext
    {
        internal AutomatedAnalysisExecutionContext(IAutomatedAnalysisTrace trace, TextWriter textLog, AutomatedAnalysisIssueCollection issues)
        {
            Trace = trace;
            TextLog = textLog;
            Issues = issues;

            AutomatedAnalysisTraceLog traceLog = trace as AutomatedAnalysisTraceLog;
            if(traceLog != null)
            {
                TraceLog = traceLog.TraceLog;
                SymbolReader = traceLog.SymbolReader;
            }
        }
        internal AutomatedAnalysisExecutionContext(TraceLog traceLog, TextWriter textLog, SymbolReader symbolReader, AutomatedAnalysisIssueCollection issues)
        {
            TraceLog = traceLog;
            TextLog = textLog;
            SymbolReader = symbolReader;
            Issues = issues;
        }

        public SymbolReader SymbolReader { get; }

        public TraceLog TraceLog { get; }

        public IAutomatedAnalysisTrace Trace { get; }

        public TextWriter TextLog { get; }

        public AutomatedAnalysisIssueCollection Issues { get; }
    }
}
