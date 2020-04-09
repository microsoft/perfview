using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
    public sealed class AutomatedAnalysisRuleProviderAttribute : Attribute
    {
        public AutomatedAnalysisRuleProviderAttribute(Type providerType)
        {
            ProviderType = providerType;
        }

        public Type ProviderType { get; }
    }

    public interface IAutomatedAnalysisRuleProvider
    {
        IEnumerable<AutomatedAnalysisRule> GetRules();
    }
}
