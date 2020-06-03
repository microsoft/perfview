using System;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Stacks;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Diagnostics.Symbols;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    public sealed class AutomatedAnalysisExecutionContext
    {
        internal AutomatedAnalysisExecutionContext(TraceLog traceLog, TextWriter textLog, SymbolReader symbolReader, AutomatedAnalysisIssueCollection issues)
        {
            TraceLog = traceLog;
            TextLog = textLog;
            SymbolReader = symbolReader;
            Issues = issues;
        }

        public SymbolReader SymbolReader { get; }

        public TraceLog TraceLog { get; }

        public TextWriter TextLog { get; }

        public AutomatedAnalysisIssueCollection Issues { get; }
    }
}
