using Microsoft.Diagnostics.Tracing.Etlx;
using System.IO;
using Microsoft.Diagnostics.Symbols;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    public sealed class AnalyzerExecutionContext
    {
        internal AnalyzerExecutionContext(ITrace trace, TextWriter textLog, AnalyzerIssueCollection issues)
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
        internal AnalyzerExecutionContext(TraceLog traceLog, TextWriter textLog, SymbolReader symbolReader, AnalyzerIssueCollection issues)
        {
            TraceLog = traceLog;
            TextLog = textLog;
            SymbolReader = symbolReader;
            Issues = issues;
        }

        public SymbolReader SymbolReader { get; }

        public TraceLog TraceLog { get; }

        public ITrace Trace { get; }

        public TextWriter TextLog { get; }

        public AnalyzerIssueCollection Issues { get; }
    }
}
