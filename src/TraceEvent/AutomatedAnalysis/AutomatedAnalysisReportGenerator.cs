using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing.Etlx;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    public sealed class AutomatedAnalysisReportGenerator : IDisposable
    {
        private TextWriter _writer;

        public AutomatedAnalysisReportGenerator(TextWriter writer)
        {
            _writer = writer;
            StartReport();
        }

        void IDisposable.Dispose()
        {
            EndReport();
        }

        private void StartReport()
        {
            _writer.WriteLine("<html>");
            _writer.WriteLine("<head>");
            _writer.WriteLine("<title>Automated CPU Analysis</title>");
            _writer.WriteLine("<meta charset=\"UTF-8\"/>");
            _writer.WriteLine("<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\"/>");
            _writer.WriteLine("</head>");
            _writer.WriteLine("<body>");
            _writer.WriteLine("<H2>Automated CPU Analysis</H2>");
        }

        private void EndReport()
        {
            _writer.WriteLine("</body>");
            _writer.WriteLine("</html>");
        }

        public void WriteIssuesForProcess(TraceProcess process, List<AutomatedAnalysisIssue> issues)
        {
            _writer.WriteLine($"<H3>Process {process.ProcessID}: {process.CommandLine}</H3>");
            _writer.WriteLine("<Table Border=\"1\">");
            _writer.WriteLine("<TR><TH>Issue Title</TH><TH>Notes</TH></TR>");
            foreach(AutomatedAnalysisIssue issue in issues)
            {
                _writer.WriteLine($"<TR><TD>{issue.Title}</TD><TD>{issue.Description}<BR/><BR/>More details: <A HREF=\"{issue.URL}\">{issue.URL}</A></TD></TR>");
            }
            _writer.WriteLine("</Table>");
        }

        public void WriteExecutedRulesList(List<AutomatedAnalysisRule> rules)
        {
            if(rules.Count > 0)
            {
                _writer.WriteLine("<H3>Rules Executed:</H3>");
                _writer.WriteLine("<ul style=\"list-style-type:circle\">");
                foreach (AutomatedAnalysisRule rule in rules)
                {
                    _writer.WriteLine($"<li>{rule.GetType().AssemblyQualifiedName}</li>");
                }
                _writer.WriteLine("</ul>");
            }
            else
            {
                _writer.WriteLine($"<H3>No rules were executed.  Check '{AutomatedAnalysisRuleResolver.RulesDirectory}' for DLLs containing rules.</H3>");
            }
        }
    }
}
