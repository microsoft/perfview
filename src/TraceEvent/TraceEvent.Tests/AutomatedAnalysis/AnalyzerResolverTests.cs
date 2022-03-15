using Microsoft.Diagnostics.Tracing.AutomatedAnalysis;
using System;
using System.Collections.Generic;
using System.Reflection;
using PerfView.TestUtilities;
using Xunit;
using Xunit.Abstractions;
using TestEventTests.Analyzers;
using System.Linq;

// Register the provider at the assembly level.
[assembly: AnalyzerProvider(typeof(TestEventTests.Analyzers.TestAnalyzerProvider))]

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
            Assert.Contains(TestAnalyzerProvider.One, resolver.ResolvedAnalyzers);
        }

        [Fact]
        public void CurrentAssemblyAnalyzerResolver_AnalyzerTwo()
        {
            SelfReportingAnalyzerResolver resolver = new SelfReportingAnalyzerResolver(AnalyzerSelector.AnalyzerTwo);
            resolver.Resolve();
            Assert.Single(resolver.ResolvedAnalyzers);
            Assert.Contains(TestAnalyzerProvider.Two, resolver.ResolvedAnalyzers);
        }

        [Fact]
        public void CurrentAssemblyAnalyzerResolver_BothAnalyzers()
        {
            SelfReportingAnalyzerResolver resolver = new SelfReportingAnalyzerResolver(AnalyzerSelector.AnalyzerOne | AnalyzerSelector.AnalyzerTwo);
            resolver.Resolve();
            Assert.Equal(2, resolver.ResolvedAnalyzers.Count());
            Assert.Contains(TestAnalyzerProvider.One, resolver.ResolvedAnalyzers);
            Assert.Contains(TestAnalyzerProvider.Two, resolver.ResolvedAnalyzers);
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

    public sealed class SelfReportingAnalyzerResolver : AnalyzerResolver
    {
        private AnalyzerSelector _selector;

        public SelfReportingAnalyzerResolver(AnalyzerSelector selector)
        {
            _selector = selector;
        }

        protected override void OnAnalyzerLoaded(AnalyzerLoadContext loadContext)
        {
            if (((_selector & AnalyzerSelector.AnalyzerOne) == AnalyzerSelector.AnalyzerOne) &&
                (loadContext.Analyzer == TestAnalyzerProvider.One))
            {
                // Allow the analyzer to be run.
            }
            else if (((_selector & AnalyzerSelector.AnalyzerTwo) == AnalyzerSelector.AnalyzerTwo) &&
                (loadContext.Analyzer == TestAnalyzerProvider.Two))
            {
                // Allow the analyzer to be run.
            }
            else
            {
                loadContext.ShouldRun = false;
            }
        }

        protected internal override void Resolve()
        {
            Assembly currentAssembly = Assembly.GetExecutingAssembly();
            Assert.NotNull(currentAssembly);

            ConsumeAssembly(currentAssembly);
        }
    }
}

namespace TestEventTests.Analyzers
{
    public sealed class TestAnalyzerProvider : IAnalyzerProvider
    {
        public static readonly AnalyzerOne One = new AnalyzerOne();
        public static readonly AnalyzerTwo Two = new AnalyzerTwo();

        internal static readonly Analyzer[] Analyzers = new Analyzer[]
        {
            One,
            Two
        };

        IEnumerable<Analyzer> IAnalyzerProvider.GetAnalyzers()
        {
            foreach (Analyzer analyzer in Analyzers)
            {
                yield return analyzer;
            }
        }
    }

    public sealed class AnalyzerOne : Analyzer
    {
        protected override AnalyzerExecutionResult Execute(AnalyzerExecutionContext executionContext)
        {
            return AnalyzerExecutionResult.Success;
        }
    }

    public sealed class AnalyzerTwo : Analyzer
    {
        protected override AnalyzerExecutionResult Execute(AnalyzerExecutionContext executionContext)
        {
            return AnalyzerExecutionResult.Success;
        }
    }
}