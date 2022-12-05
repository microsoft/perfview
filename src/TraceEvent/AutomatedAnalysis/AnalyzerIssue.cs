using System;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    /// <summary>
    /// An issue identified by an Analyzer.
    /// </summary>
    public class AnalyzerIssue
    {
        /// <summary>
        /// Create a new instance of AnalyzerIssue.
        /// </summary>
        /// <param name="title">A string title.</param>
        /// <param name="description">A string description.</param>
        /// <param name="url">A URL pointing to further documentation.</param>
        /// <exception cref="InvalidOperationException">Instances of AnalyzerIssue can only be created during Analyzer execution.</exception>
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

        /// <summary>
        /// The Analyzer that created the issue.
        /// </summary>
        public Analyzer Analyzer { get; private set; }

        /// <summary>
        /// The title of the issue.
        /// </summary>
        public virtual string Title { get; protected set; }

        /// <summary>
        /// The description of the issue.
        /// </summary>
        public virtual string Description { get; protected set; }

        /// <summary>
        /// A URL pointing to further documentation on the issue.
        /// </summary>
        public virtual string URL { get; protected set; }
    }
}
