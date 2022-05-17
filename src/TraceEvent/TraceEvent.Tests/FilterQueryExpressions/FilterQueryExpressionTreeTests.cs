using Microsoft.Diagnostics.Tracing.TraceUtilities.FilterQueryExpression;
using System.Collections;
using System.Collections.Generic;
using TraceEventTests.FilterQueryExpressions;
using Xunit;

namespace TraceEventTests
{
    public class FilterQueryExpressionTreeTests
    {
        private sealed class FilterQueryExpressionTreeTestData_SimpleExpressions_TrueCases : IEnumerable<object[]>
        {
            private readonly List<object[]> _data = new List<object[]>
            {
                new object[] { "Depth >= 10", new FilterQueryExpressionTestTraceEvent("Depth", "20") },
                new object[] { "(Depth >= 10)", new FilterQueryExpressionTestTraceEvent("Depth", "20") },
                new object[] { "Depth >= 10", new FilterQueryExpressionTestTraceEvent("Depth", "20") },
                new object[] { "Depth <= 10", new FilterQueryExpressionTestTraceEvent("Depth", "5") },
                new object[] { "Depth != 10", new FilterQueryExpressionTestTraceEvent("Depth", "5") },
                new object[] { "Depth > 10", new FilterQueryExpressionTestTraceEvent("Depth", "25") },
                new object[] { "Depth >= 10", new FilterQueryExpressionTestTraceEvent("Depth", "25") },
                new object[] { "ThreadData Contains 10", new FilterQueryExpressionTestTraceEvent("ThreadData", "1001") },
                new object[] { "GC/Start::Depth = 10", new FilterQueryExpressionTestTraceEvent("Depth", "10", "GC/Start") },
                new object[] { "(GC/Start::Depth = 10)", new FilterQueryExpressionTestTraceEvent("Depth", "10", "GC/Start") },
            };

            public IEnumerator<object[]> GetEnumerator() => _data.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Theory]
        [ClassData(typeof(FilterQueryExpressionTreeTestData_SimpleExpressions_TrueCases))]
        public void Match_SimpleExpressions_True(string expression, FilterQueryExpressionTestTraceEvent filterQueryExpressionTestTraceEvent)
        { 
            FilterQueryExpressionTree tree = new FilterQueryExpressionTree(expression);
            bool match = tree.Match(filterQueryExpressionTestTraceEvent);
            Assert.True(match);
        }

        private sealed class FilterQueryExpressionTreeTestData_SimpleExpressions_FalseCases : IEnumerable<object[]> 
        {
            private readonly List<object[]> _data = new List<object[]>
            {
                new object[] { "Depth <= 10", new FilterQueryExpressionTestTraceEvent("Depth", "20") },
                new object[] { "(Depth <= 10)", new FilterQueryExpressionTestTraceEvent("Depth", "20") },
                new object[] { "Depth >= 10", new FilterQueryExpressionTestTraceEvent("Depth", "5") },
                new object[] { "Depth = 10", new FilterQueryExpressionTestTraceEvent("Depth", "5") },
                new object[] { "Depth < 10", new FilterQueryExpressionTestTraceEvent("Depth", "25") },
                new object[] { "Depth <= 10", new FilterQueryExpressionTestTraceEvent("Depth", "25") },
                new object[] { "ThreadData Contains 1,0033", new FilterQueryExpressionTestTraceEvent("ThreadData", "1001") },
                new object[] { "GC/Start::Depth = 10", new FilterQueryExpressionTestTraceEvent("Depth", "1001", "GC/Start") },
                new object[] { "GC/Start::ThreadData = 1,001", new FilterQueryExpressionTestTraceEvent("Depth", "1001", "GC/Start") },
            };

            public IEnumerator<object[]> GetEnumerator() => _data.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Theory]
        [ClassData(typeof(FilterQueryExpressionTreeTestData_SimpleExpressions_FalseCases))]
        public void Match_SimpleExpressions_False(string expression, FilterQueryExpressionTestTraceEvent filterQueryExpressionTestTraceEvent)
        { 
            FilterQueryExpressionTree tree = new FilterQueryExpressionTree(expression);
            bool match = tree.Match(filterQueryExpressionTestTraceEvent);
            Assert.False(match);
        }

        private sealed class FilterQueryExpressionTreeTestData_ComplexExpressions_TrueCases : IEnumerable<object[]> 
        {
            private readonly List<object[]> _data = new List<object[]>
            {
                new object[] { "(Depth <= 10) && (Depth <= 20)", new FilterQueryExpressionTestTraceEvent("Depth", "15") },
                new object[] { "((Depth <= 10) && (Depth <= 20)) || ((Depth != 15) && (Depth > 5))", new FilterQueryExpressionTestTraceEvent("Depth", "12") },
                new object[] { "((Depth != 10 && (Depth != 10 && (Depth != 10))))", new FilterQueryExpressionTestTraceEvent("Depth", "5") },
                new object[] { "((Depth <= 10 && (Depth <= 10 && (Depth != 10))))", new FilterQueryExpressionTestTraceEvent("Depth", "5") },
                new object[] { "(ThreadData != 1,0033) && (ThreadData = 1001)", new FilterQueryExpressionTestTraceEvent("ThreadData", "1001") },
                new object[] { "(ThreadData != 1,0033 && ThreadData != 1,002) && (ThreadData = 1001)", new FilterQueryExpressionTestTraceEvent("ThreadData", "1001") },
                new object[] { "(OldProcessName = test && OldProcessName != test1 || OldProcessName != test2)", new FilterQueryExpressionTestTraceEvent("OldProcessName", "test") },
                new object[] { "(GC/Start::Depth >= 10 && GC/Start::Depth <= 20)", new FilterQueryExpressionTestTraceEvent("Depth", "15", "GC/Start") },
            };

            public IEnumerator<object[]> GetEnumerator() => _data.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Theory]
        [ClassData(typeof(FilterQueryExpressionTreeTestData_ComplexExpressions_TrueCases))]
        public void Match_ComplexExpressions_True(string expression, FilterQueryExpressionTestTraceEvent filterQueryExpressionTestTraceEvent)
        { 
            FilterQueryExpressionTree tree = new FilterQueryExpressionTree(expression);
            bool match = tree.Match(filterQueryExpressionTestTraceEvent);
            Assert.True(match);
        }
    }
}
