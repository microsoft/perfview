using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    /// <summary>
    /// A report generator whose output format is HTML.
    /// </summary>
    public sealed class HtmlReportGenerator : IDisposable
    {
        private TextWriter _writer;

        /// <summary>
        /// Create a new instance of HtmlReportGenerator.
        /// </summary>
        /// <param name="writer">The destination for the report.</param>
        public HtmlReportGenerator(TextWriter writer)
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
            _writer.WriteLine("<title>Automated Trace Analysis</title>");
            _writer.WriteLine("<meta charset=\"UTF-8\"/>");
            _writer.WriteLine("<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\"/>");
            _writer.WriteLine("</head>");
            _writer.WriteLine("<body>");
            _writer.WriteLine("<H2>Automated Trace Analysis</H2>");
        }

        private void EndReport()
        {
            _writer.WriteLine("</body>");
            _writer.WriteLine("</html>");
        }

        /// <summary>
        /// Write out the specified process information and issues.
        /// </summary>
        /// <param name="process">The process.</param>
        /// <param name="issues">The list of issues.</param>
        public void WriteIssuesForProcess(Process process, List<AnalyzerIssue> issues)
        {
            _writer.WriteLine($"<H3>Process {process.DisplayID}: {process.Description}</H3>");
            _writer.WriteLine("<Table Border=\"1\">");
            _writer.WriteLine("<TR><TH>Issue Title</TH><TH>Notes</TH></TR>");
            foreach(AnalyzerIssue issue in issues)
            {
                _writer.WriteLine($"<TR><TD>{issue.Title}</TD><TD>{issue.Description}<BR/><BR/>More details: <A HREF=\"{issue.URL}\">{issue.URL}</A></TD></TR>");
            }
            _writer.WriteLine("</Table>");
        }

        /// <summary>
        /// Write out the set of executed analyzers.
        /// </summary>
        /// <param name="analyzers">The set of executed analyzers.</param>
        public void WriteExecutedAnalyzerList(IEnumerable<Analyzer> analyzers)
        {
            if(analyzers.Count() > 0)
            {
                _writer.WriteLine("<H3>Analyzers Executed:</H3>");
                _writer.WriteLine("<ul style=\"list-style-type:circle\">");
                foreach (Analyzer analyzer in analyzers)
                {
                    _writer.WriteLine($"<li>{analyzer.GetType().AssemblyQualifiedName}</li>");
                }
                _writer.WriteLine("</ul>");
            }
            else
            {
                _writer.WriteLine($"<H3>No analyzers were executed.</H3>");
            }
        }
    }
}
