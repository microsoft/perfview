using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tracing.Etlx;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    public sealed class AnalyzerIssueCollection : IEnumerable<KeyValuePair<AnalyzerTraceProcess, List<AnalyzerIssue>>>
    {
        private Dictionary<AnalyzerTraceProcess, List<AnalyzerIssue>> _issues = new Dictionary<AnalyzerTraceProcess, List<AnalyzerIssue>>();

        public List<AnalyzerIssue> this[AnalyzerTraceProcess process]
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

        IEnumerator<KeyValuePair<AnalyzerTraceProcess, List<AnalyzerIssue>>> IEnumerable<KeyValuePair<AnalyzerTraceProcess, List<AnalyzerIssue>>>.GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<AnalyzerTraceProcess, List<AnalyzerIssue>>>)_issues).GetEnumerator();
        }
    }
}
