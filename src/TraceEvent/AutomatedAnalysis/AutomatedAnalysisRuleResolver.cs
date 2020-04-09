using Microsoft.Diagnostics.Tracing.Parsers.FrameworkEventSource;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    internal sealed class AutomatedAnalysisRuleResolver
    {
        private const string RulesDirectoryName = "Rules";
        private static string s_rulesDirectory;

        internal static string RulesDirectory
        {
            get
            {
                if (s_rulesDirectory == null)
                {
                    // Assume plugins sit in a plugins directory next to the current assembly.
#if AUTOANALYSIS_EXTENSIBILITY
                    s_rulesDirectory = Path.Combine(
                        Path.GetDirectoryName(typeof(AutomatedAnalysisRuleResolver).Assembly.Location),
                        RulesDirectoryName);
#endif
                }
                return s_rulesDirectory;
            }
        }

        internal static IEnumerable<AutomatedAnalysisRule> GetRules()
        {
#if AUTOANALYSIS_EXTENSIBILITY
            // Iterate through all assemblies in the rules directory.
            if(!Directory.Exists(RulesDirectory))
            {
                yield break;
            }

            string[] candidateAssemblies = Directory.GetFiles(RulesDirectory, "*.dll", SearchOption.TopDirectoryOnly);
            foreach(string candidateAssembly in candidateAssemblies)
            {
                Assembly assembly = Assembly.LoadFrom(candidateAssembly);
                if(assembly != null)
                {
                    AutomatedAnalysisRuleProviderAttribute attr = 
                        (AutomatedAnalysisRuleProviderAttribute)assembly.GetCustomAttribute(typeof(AutomatedAnalysisRuleProviderAttribute));
                    if(attr != null && attr.ProviderType != null)
                    {
                        // Create an instance of the provider.
                        IAutomatedAnalysisRuleProvider ruleProvider = Activator.CreateInstance(attr.ProviderType) as IAutomatedAnalysisRuleProvider;
                        if (ruleProvider != null)
                        {
                            foreach (AutomatedAnalysisRule rule in ruleProvider.GetRules())
                            {
                                yield return rule;
                            }
                        }
                    }
                }
            }
#else
            yield break;
#endif

        }
    }
}