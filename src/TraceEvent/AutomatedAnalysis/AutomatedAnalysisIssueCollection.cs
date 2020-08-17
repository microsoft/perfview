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
    public sealed class AutomatedAnalysisIssueCollection : IEnumerable<KeyValuePair<AutomatedAnalysisTraceProcess, List<AutomatedAnalysisIssue>>>
    {
        private Dictionary<AutomatedAnalysisTraceProcess, List<AutomatedAnalysisIssue>> _issues = new Dictionary<AutomatedAnalysisTraceProcess, List<AutomatedAnalysisIssue>>();

        public List<AutomatedAnalysisIssue> this[AutomatedAnalysisTraceProcess process]
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

        // Keep this for now because GLAD depends on it.
        public List<AutomatedAnalysisIssue> this[TraceProcess process]
        {
            get
            {
                AutomatedAnalysisTraceProcess traceProcess = new AutomatedAnalysisTraceProcess((int)process.ProcessIndex, process.ProcessID, process.CommandLine, process.ManagedProcess());
                List<AutomatedAnalysisIssue> issues;
                if (!_issues.TryGetValue(traceProcess, out issues))
                {
                    issues = new List<AutomatedAnalysisIssue>();
                    _issues.Add(traceProcess, issues);
                }

                return issues;
            }
        }

        public IEnumerator GetEnumerator()
        {
            return ((IEnumerable)_issues).GetEnumerator();
        }

        IEnumerator<KeyValuePair<AutomatedAnalysisTraceProcess, List<AutomatedAnalysisIssue>>> IEnumerable<KeyValuePair<AutomatedAnalysisTraceProcess, List<AutomatedAnalysisIssue>>>.GetEnumerator()
        {
            return ((IEnumerable<KeyValuePair<AutomatedAnalysisTraceProcess, List<AutomatedAnalysisIssue>>>)_issues).GetEnumerator();
        }
    }
}
