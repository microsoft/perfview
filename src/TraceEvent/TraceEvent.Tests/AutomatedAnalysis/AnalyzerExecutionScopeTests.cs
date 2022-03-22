using Microsoft.Diagnostics.Tracing.AutomatedAnalysis;
using System;
using Xunit;
using Xunit.Abstractions;

namespace TraceEventTests
{
    public class AnalyzerExecutionScopeTests : TestBase
    {
        public AnalyzerExecutionScopeTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Fact]
        public void CurrentDefaultValue()
        {
            Assert.Null(AnalyzerExecutionScope.Current);
        }

        [Fact]
        public void OneScope()
        {
            Analyzer analyzer = new AnalyzerExecutionScopeTests_AnalyzerOne();
            using (AnalyzerExecutionScope scope1 = new AnalyzerExecutionScope(analyzer))
            {
                Assert.Equal(scope1, AnalyzerExecutionScope.Current);
                Assert.Equal(analyzer, AnalyzerExecutionScope.Current.ExecutingAnalyzer);
            }

            Assert.Null(AnalyzerExecutionScope.Current);
        }

        [Fact]
        public void TwoScopes()
        {
            Analyzer analyzerOne = new AnalyzerExecutionScopeTests_AnalyzerOne();
            using (AnalyzerExecutionScope scopeOne = new AnalyzerExecutionScope(analyzerOne))
            {
                Assert.Equal(scopeOne, AnalyzerExecutionScope.Current);
                Assert.Equal(analyzerOne, AnalyzerExecutionScope.Current.ExecutingAnalyzer);
            }

            Assert.Null(AnalyzerExecutionScope.Current);

            Analyzer analyzerTwo = new AnalyzerExecutionScopeTests_AnalyzerTwo();
            using (AnalyzerExecutionScope scopeTwo = new AnalyzerExecutionScope(analyzerTwo))
            {
                Assert.Equal(scopeTwo, AnalyzerExecutionScope.Current);
                Assert.Equal(analyzerTwo, AnalyzerExecutionScope.Current.ExecutingAnalyzer);
            }

            Assert.Null(AnalyzerExecutionScope.Current);
        }

        [Fact]
        public void ClobberScope()
        {
            Analyzer analyzerOne = new AnalyzerExecutionScopeTests_AnalyzerOne();
            using (AnalyzerExecutionScope scopeOne = new AnalyzerExecutionScope(analyzerOne))
            {
                Assert.Equal(scopeOne, AnalyzerExecutionScope.Current);
                Assert.Equal(analyzerOne, AnalyzerExecutionScope.Current.ExecutingAnalyzer);

                Analyzer analyzerTwo = new AnalyzerExecutionScopeTests_AnalyzerTwo();
                Assert.Throws<InvalidOperationException>(() =>
                {
                    AnalyzerExecutionScope scopeTwo = new AnalyzerExecutionScope(analyzerTwo);
                });
            }

            Assert.Null(AnalyzerExecutionScope.Current);
        }
    }

    public class AnalyzerExecutionScopeTests_AnalyzerOne : Analyzer
    {
        protected override AnalyzerExecutionResult Execute(AnalyzerExecutionContext executionContext)
        {
            throw new NotImplementedException();
        }
    }

    public class AnalyzerExecutionScopeTests_AnalyzerTwo : Analyzer
    {
        protected override AnalyzerExecutionResult Execute(AnalyzerExecutionContext executionContext)
        {
            throw new NotImplementedException();
        }
    }
}
