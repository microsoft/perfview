using System;
using System.IO;
using Microsoft.Diagnostics.Symbols;
using Microsoft.Diagnostics.Tracing.Etlx;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    /// <summary>
    /// The top-level object used to store contextual information during Analyzer execution.
    /// </summary>
    public sealed class AnalyzerExecutionContext
    {
        internal AnalyzerExecutionContext(Configuration configuration, ITrace trace, TextWriter textLog)
        {
            Configuration = configuration;
            Trace = trace;
            TextLog = textLog;

            AutomatedAnalysisTraceLog traceLog = trace as AutomatedAnalysisTraceLog;
            if(traceLog != null)
            {
                TraceLog = traceLog.UnderlyingSource;
                SymbolReader = traceLog.SymbolReader;
            }
        }

        public Configuration Configuration { get; }

        /// <summary>
        /// The SymbolReader associated with the execution.
        /// </summary>
        public SymbolReader SymbolReader { get; }

        [Obsolete]
        public TraceLog TraceLog { get; }

        /// <summary>
        /// The trace to be analyzed.
        /// </summary>
        public ITrace Trace { get; }

        [Obsolete]
        public TextWriter TextLog { get; }

        public AnalyzerIssueCollection Issues { get; } = new AnalyzerIssueCollection();
    }
}
