using Microsoft.Diagnostics.Tracing.Parsers.FrameworkEventSource;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    internal sealed class AutomatedAnalysisAnalyzerResolver
    {
        private const string AnalyzersDirectoryName = "Analyzers";
        private static string s_analyzersDirectory;

        internal static string AnalyzersDirectory
        {
            get
            {
                if (s_analyzersDirectory == null)
                {
                    // Assume plugins sit in a plugins directory next to the current assembly.
#if AUTOANALYSIS_EXTENSIBILITY
                    s_analyzersDirectory = Path.Combine(
                        Path.GetDirectoryName(typeof(AutomatedAnalysisAnalyzerResolver).Assembly.Location),
                        AnalyzersDirectoryName);
#endif
                }
                return s_analyzersDirectory;
            }
        }

        internal static IEnumerable<AutomatedAnalysisAnalyzer> GetAnalyzers()
        {
#if AUTOANALYSIS_EXTENSIBILITY
            // Iterate through all assemblies in the analyzers directory.
            if(!Directory.Exists(AnalyzersDirectory))
            {
                yield break;
            }

            string[] candidateAssemblies = Directory.GetFiles(AnalyzersDirectory, "*.dll", SearchOption.TopDirectoryOnly);
            foreach(string candidateAssembly in candidateAssemblies)
            {
                Assembly assembly = Assembly.LoadFrom(candidateAssembly);
                if(assembly != null)
                {
                    AutomatedAnalysisAnalyzerProviderAttribute attr = 
                        (AutomatedAnalysisAnalyzerProviderAttribute)assembly.GetCustomAttribute(typeof(AutomatedAnalysisAnalyzerProviderAttribute));
                    if(attr != null && attr.ProviderType != null)
                    {
                        // Create an instance of the provider.
                        IAutomatedAnalysisAnalyzerProvider analyzerProvider = Activator.CreateInstance(attr.ProviderType) as IAutomatedAnalysisAnalyzerProvider;
                        if (analyzerProvider != null)
                        {
                            foreach (AutomatedAnalysisAnalyzer analyzer in analyzerProvider.GetAnalyzers())
                            {
                                yield return analyzer;
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