using System;
using System.IO;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Symbols;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    public sealed class AutomatedAnalysisManager
    {
        private IAutomatedAnalysisTrace _trace;
        private TextWriter _textLog;

        public AutomatedAnalysisManager(TraceLog traceLog, TextWriter textLog, SymbolReader symbolReader)
        {
            _trace = new AutomatedAnalysisTraceLog(traceLog, symbolReader);
            _textLog = textLog;
        }

        public AutomatedAnalysisManager(IAutomatedAnalysisTrace trace, TextWriter textLog)
        {
            _trace = trace;
            _textLog = textLog;
        }

        public AutomatedAnalysisIssueCollection Issues { get; private set; }

        public List<AutomatedAnalysisRule> ExecuteRules()
        {
            Issues = new AutomatedAnalysisIssueCollection();

            List<AutomatedAnalysisRule> allRules = new List<AutomatedAnalysisRule>();
            List<AutomatedAnalysisPerProcessRule> perProcessRules = new List<AutomatedAnalysisPerProcessRule>();

            // Run global rules, deferring per-process rules.
            AutomatedAnalysisExecutionContext executionContext = new AutomatedAnalysisExecutionContext(_trace, _textLog, Issues);
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
                    try
                    {
                        rule.RunRule(executionContext, null);
                    }
                    catch (Exception ex)
                    {
                        _textLog.WriteLine($"Error while executing rule '{rule.GetType().FullName}': {ex}");
                    }
                }
            }

            // Run per-process rules.
            foreach (AutomatedAnalysisTraceProcess process in executionContext.Trace.Processes)
            {
                if (process.ContainsManagedCode)
                {
                    // Create the process context.
                    ProcessContext processContext = new ProcessContext(executionContext, process);

                    foreach (AutomatedAnalysisPerProcessRule rule in perProcessRules)
                    {
                        try
                        {
                            rule.RunRule(executionContext, processContext);
                        }
                        catch (Exception ex)
                        {
                            _textLog.WriteLine($"Error while executing rule '{rule.GetType().FullName}': {ex}");
                        }
                    }
                }
            }

            return allRules;
        }

        public void GenerateReport(TextWriter writer)
        {
            using (AutomatedAnalysisReportGenerator reportGenerator = new AutomatedAnalysisReportGenerator(writer))
            {
                // Execute rules.
                List<AutomatedAnalysisRule> allRules = ExecuteRules();

                // Write out issues.
                foreach (KeyValuePair<AutomatedAnalysisTraceProcess, List<AutomatedAnalysisIssue>> pair in Issues)
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