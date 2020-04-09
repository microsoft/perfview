using System.IO;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Symbols;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    public sealed class AutomatedAnalysisManager
    {
        private TraceLog _traceLog;
        private TextWriter _textLog;
        private SymbolReader _symbolReader;
        private AutomatedAnalysisIssueCollection _issueCollection = new AutomatedAnalysisIssueCollection();

        public AutomatedAnalysisManager(TraceLog traceLog, TextWriter textLog, SymbolReader symbolReader)
        {
            _traceLog = traceLog;
            _textLog = textLog;
            _symbolReader = symbolReader;
        }
        public void GenerateReport(TextWriter writer)
        {
            using (AutomatedAnalysisReportGenerator reportGenerator = new AutomatedAnalysisReportGenerator(writer))
            {
                List<AutomatedAnalysisRule> allRules = new List<AutomatedAnalysisRule>();
                List<AutomatedAnalysisPerProcessRule> perProcessRules = new List<AutomatedAnalysisPerProcessRule>();

                // Run global rules, deferring per-process rules.
                AutomatedAnalysisExecutionContext executionContext = new AutomatedAnalysisExecutionContext(_traceLog, _textLog, _symbolReader, _issueCollection);
                foreach (AutomatedAnalysisRule rule in AutomatedAnalysisRuleResolver.GetRules())
                {
                    // Create a list of all executed rules so that they can be written into the report.
                    allRules.Add(rule);

                    if (rule is AutomatedAnalysisPerProcessRule)
                    {
                        // Defer per-process rules.
                        perProcessRules.Add((AutomatedAnalysisPerProcessRule)rule);
                    }
                    else
                    {
                        // Execute the rule.
                        rule.RunRule(executionContext, null);
                    }
                }

                // Run per-process rules.
                foreach (TraceProcess process in executionContext.TraceLog.Processes)
                {
                    if (process.ManagedProcess())
                    {
                        // Create the process context.
                        ProcessContext processContext = new ProcessContext(executionContext, process);

                        foreach (AutomatedAnalysisPerProcessRule rule in perProcessRules)
                        {
                            rule.RunRule(executionContext, processContext);
                        }
                    }
                }

                // Write out issues.
                foreach (KeyValuePair<TraceProcess, List<AutomatedAnalysisIssue>> pair in _issueCollection)
                {
                    if (pair.Value.Count > 0)
                    {
                        reportGenerator.WriteIssuesForProcess(pair.Key, pair.Value);
                    }
                }

                // Write the list of executed rules.
               reportGenerator.WriteExecutedRulesList(allRules);
            }
        }
    }
}