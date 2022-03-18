using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    public sealed class AnalyzerLoadContext
    {
        public Analyzer Analyzer { get; private set; }
        public bool ShouldRun { get; set; }

        internal void Reset(Analyzer analyzer)
        {
            Analyzer = analyzer;
            ShouldRun = true;
        }
    }

    public abstract class AnalyzerResolver
    {
        private List<Analyzer> _analyzers = new List<Analyzer>();
        private Configuration _configuration;

        internal IEnumerable<Analyzer> ResolvedAnalyzers
        {
            get { return _analyzers; }
        }

        internal Configuration Configuration
        {
            get
            {
                if (_configuration == null)
                {
                    return _configuration = new Configuration();
                }

                return _configuration;
            }
        }

        protected virtual void OnAnalyzerLoaded(AnalyzerLoadContext loadContext)
        {
            // All analyzers are run by default.
        }

        protected void ConsumeAssembly(Assembly analyzerAssembly)
        {
            if (analyzerAssembly == null)
            {
                throw new ArgumentNullException(nameof(analyzerAssembly));
            }

            AnalyzerLoadContext loadContext = new AnalyzerLoadContext();

            AnalyzerProviderAttribute attr =
                (AnalyzerProviderAttribute)analyzerAssembly.GetCustomAttribute(typeof(AnalyzerProviderAttribute));
            if (attr != null && attr.ProviderType != null)
            {
                IAnalyzerProvider analyzerProvider = Activator.CreateInstance(attr.ProviderType) as IAnalyzerProvider;
                if (analyzerProvider != null)
                {
                    foreach (Analyzer analyzer in analyzerProvider.GetAnalyzers())
                    {
                        loadContext.Reset(analyzer);
                        OnAnalyzerLoaded(loadContext);
                        if (loadContext.ShouldRun)
                        {
                            _analyzers.Add(loadContext.Analyzer);
                        }
                    }
                }
            }
        }

        internal void ConsumeConfiguration(Configuration configuration)
        {
            if (_configuration != null)
            {
                throw new InvalidOperationException("Only one configuration can be specified.");
            }

            _configuration = configuration;
        }

        protected internal abstract void Resolve();
    }
}
