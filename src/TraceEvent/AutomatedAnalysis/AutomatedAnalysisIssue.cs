using System;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    public sealed class AutomatedAnalysisIssue
    {
        public AutomatedAnalysisIssue(
            string title,
            string description,
            string url)
        {
            Title = title;
            Description = description;
            URL = url;
        }

        public string Title { get; private set; }
        public string Description { get; private set; }
        public string URL { get; private set; }
    }
}
