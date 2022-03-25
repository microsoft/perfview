using System;
using System.Collections.Generic;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class AnalyzerProviderAttribute : Attribute
    {
        public AnalyzerProviderAttribute(Type providerType)
        {
            ProviderType = providerType;
        }

        public Type ProviderType { get; }
    }

    public interface IAnalyzerProvider
    {
        IEnumerable<Analyzer> GetAnalyzers();
    }
}
