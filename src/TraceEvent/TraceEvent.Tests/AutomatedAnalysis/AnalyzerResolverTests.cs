using Microsoft.Diagnostics.Tracing.AutomatedAnalysis;
using System;
using System.Collections.Generic;
using System.Reflection;
using PerfView.TestUtilities;
using Xunit;
using Xunit.Abstractions;
using TestEventTests.Analyzers;
using System.Linq;

namespace TraceEventTests
{
    [UseCulture("en-US")]
    public class AnalyzerResolverTests : TestBase
    {
        public AnalyzerResolverTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public void EmptyAnalyzerResolver()
        {
            EmptyAnalyzerResolver resolver = new EmptyAnalyzerResolver();
            resolver.Resolve();
            Assert.Empty(resolver.ResolvedAnalyzers);
        }

        [Fact]
        public void CurrentAssemblyAnalyzerResolver_Empty()
        {
            SelfReportingAnalyzerResolver resolver = new SelfReportingAnalyzerResolver(AnalyzerSelector.Empty);
            resolver.Resolve();
            Assert.Empty(resolver.ResolvedAnalyzers);
        }

        [Fact]
        public void CurrentAssemblyAnalyzerResolver_AnalyzerOne()
        {
            SelfReportingAnalyzerResolver resolver = new SelfReportingAnalyzerResolver(AnalyzerSelector.AnalyzerOne);
            resolver.Resolve();
            Assert.Single(resolver.ResolvedAnalyzers);
            Assert.Contains(TestAnalyzerProvider.ResolverTests_AnalyzerOne, resolver.ResolvedAnalyzers);
        }

        [Fact]
        public void CurrentAssemblyAnalyzerResolver_AnalyzerTwo()
        {
            SelfReportingAnalyzerResolver resolver = new SelfReportingAnalyzerResolver(AnalyzerSelector.AnalyzerTwo);
            resolver.Resolve();
            Assert.Single(resolver.ResolvedAnalyzers);
            Assert.Contains(TestAnalyzerProvider.ResolverTests_AnalyzerTwo, resolver.ResolvedAnalyzers);
        }

        [Fact]
        public void CurrentAssemblyAnalyzerResolver_BothAnalyzers()
        {
            SelfReportingAnalyzerResolver resolver = new SelfReportingAnalyzerResolver(AnalyzerSelector.AnalyzerOne | AnalyzerSelector.AnalyzerTwo);
            resolver.Resolve();
            Assert.Equal(2, resolver.ResolvedAnalyzers.Count());
            Assert.Contains(TestAnalyzerProvider.ResolverTests_AnalyzerOne, resolver.ResolvedAnalyzers);
            Assert.Contains(TestAnalyzerProvider.ResolverTests_AnalyzerTwo, resolver.ResolvedAnalyzers);
        }
    }

    public sealed class EmptyAnalyzerResolver : AnalyzerResolver
    {
        protected override void OnAnalyzerLoaded(AnalyzerLoadContext loadContext)
        {
            Assert.True(false, "No analyzers should be loaded, but OnAnalyzerLoaded was called.");
        }

        protected internal override void Resolve()
        {
            // Do nothing.
        }
    }

    [Flags]
    public enum AnalyzerSelector
    {
        Empty,
        AnalyzerOne,
        AnalyzerTwo
    }

    public sealed class SelfReportingAnalyzerResolver : TestAnalyzerResolver
    {
        private AnalyzerSelector _selector;

        public SelfReportingAnalyzerResolver(AnalyzerSelector selector)
        {
            _selector = selector;
        }

        protected override void OnAnalyzerLoaded(AnalyzerLoadContext loadContext)
        {
            if (((_selector & AnalyzerSelector.AnalyzerOne) == AnalyzerSelector.AnalyzerOne) &&
                (loadContext.Analyzer == TestAnalyzerProvider.ResolverTests_AnalyzerOne))
            {
                // Allow the analyzer to be run.
            }
            else if (((_selector & AnalyzerSelector.AnalyzerTwo) == AnalyzerSelector.AnalyzerTwo) &&
                (loadContext.Analyzer == TestAnalyzerProvider.ResolverTests_AnalyzerTwo))
            {
                // Allow the analyzer to be run.
            }
            else
            {
                loadContext.ShouldRun = false;
            }
        }
    }
}