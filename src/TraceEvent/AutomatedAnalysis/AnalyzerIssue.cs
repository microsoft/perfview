using System;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    public sealed class AnalyzerIssue
    {
        public AnalyzerIssue(
            string title,
            string description,
            string url)
        {
            // Get a reference to the currently executing analyzer.
            AnalyzerExecutionScope current = AnalyzerExecutionScope.Current;
            if (current == null)
            {
                throw new InvalidOperationException("Must be created during analyzer execution.");
            }

            Analyzer = current.ExecutingAnalyzer;
            Title = title;
            Description = description;
            URL = url;
        }

        public Analyzer Analyzer { get; private set; }
        public string Title { get; private set; }
        public string Description { get; private set; }
        public string URL { get; private set; }
    }
}
