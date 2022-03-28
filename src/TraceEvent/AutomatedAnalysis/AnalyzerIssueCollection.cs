using System.Collections;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    /// <summary>
    /// A collection of AnalyzerIssue instances organized by process.
    /// </summary>
    public sealed class AnalyzerIssueCollection : IEnumerable<KeyValuePair<Process, List<AnalyzerIssue>>>
    {
        private Dictionary<Process, List<AnalyzerIssue>> _issues = new Dictionary<Process, List<AnalyzerIssue>>();

        /// <summary>
        /// Get a list of issues for the specified process.
        /// </summary>
        /// <param name="process">The process.</param>
        /// <returns>A list of issues associated with the specified process.</returns>
        public List<AnalyzerIssue> this[Process process]
        {
            get
            {
                List<AnalyzerIssue> issues;
                if(!_issues.TryGetValue(process, out issues))
                {
                    issues = new List<AnalyzerIssue>();
                    _issues.Add(process, issues);
                }

                return issues;
            }
        }

        public IEnumerator GetEnumerator()
        {
            return ((IEnumerable)_issues).GetEnumerator();
        }

        IEnumerator<KeyValuePair<Process, List<AnalyzerIssue>>> IEnumerable<KeyValuePair<Process, List<AnalyzerIssue>>>.GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<Process, List<AnalyzerIssue>>>)_issues).GetEnumerator();
        }
    }
}
