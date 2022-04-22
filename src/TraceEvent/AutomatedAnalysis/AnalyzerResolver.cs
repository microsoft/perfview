using System;
using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.Diagnostics.Tracing.AutomatedAnalysis
{
    /// <summary>
    /// The context object used when loading an Analyzer.
    /// </summary>
    public sealed class AnalyzerLoadContext
    {
        /// <summary>
        /// The Analyzer being loaded.
        /// </summary>
        public Analyzer Analyzer { get; private set; }

        /// <summary>
        /// True iff the Analyzer should be run during trace processing.
        /// </summary>
        public bool ShouldRun { get; set; }

        internal void Reset(Analyzer analyzer)
        {
            Analyzer = analyzer;
            ShouldRun = true;
        }
    }

    /// <summary>
    /// The base class for all resolver implementations.
    /// </summary>
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

        /// <summary>
        /// Called when each Analyzer is loaded.
        /// </summary>
        /// <param name="loadContext">The context for the Analyzer load.</param>
        protected virtual void OnAnalyzerLoaded(AnalyzerLoadContext loadContext)
        {
            // All analyzers are run by default.
        }

        /// <summary>
        /// Searches the specified assembly for Analyzer instances and loads them.
        /// </summary>
        /// <param name="analyzerAssembly">The assembly to consume.</param>
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

        /// <summary>
        /// Called to discover assemblies that contain Analyzers to be executed.
        /// </summary>
        protected internal abstract void Resolve();
    }
}
