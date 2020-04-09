using Microsoft.Diagnostics.Tracing.Etlx;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    public sealed class AutomatedAnalysisIssueCollection : IEnumerable<KeyValuePair<TraceProcess, List<AutomatedAnalysisIssue>>>
    {
        private Dictionary<TraceProcess, List<AutomatedAnalysisIssue>> _issues = new Dictionary<TraceProcess, List<AutomatedAnalysisIssue>>();

        public List<AutomatedAnalysisIssue> this[TraceProcess process]
        {
            get
            {
                List<AutomatedAnalysisIssue> issues;
                if(!_issues.TryGetValue(process, out issues))
                {
                    issues = new List<AutomatedAnalysisIssue>();
                    _issues.Add(process, issues);
                }

                return issues;
            }
        }

        public IEnumerator GetEnumerator()
        {
            return ((IEnumerable)_issues).GetEnumerator();
        }

        IEnumerator<KeyValuePair<TraceProcess, List<AutomatedAnalysisIssue>>> IEnumerable<KeyValuePair<TraceProcess, List<AutomatedAnalysisIssue>>>.GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<TraceProcess, List<AutomatedAnalysisIssue>>>)_issues).GetEnumerator();
        }
    }
}
