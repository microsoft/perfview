using Microsoft.Diagnostics.Tracing.AutomatedAnalysis;
using System.Reflection;
using Xunit;

namespace TraceEventTests
{
    public class TestAnalyzerResolver : AnalyzerResolver
    {
        protected internal override void Resolve()
        {
            // Get a reference to the current assembly.
            Assembly currentAssembly = Assembly.GetExecutingAssembly();
            Assert.NotNull(currentAssembly);

            // Consume the current assembly.
            // Derived classes can implement OnAnalyzerLoaded to control which analyzers to run.
            ConsumeAssembly(currentAssembly);
        }
    }
}
