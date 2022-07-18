using Microsoft.Diagnostics.Tracing.TraceUtilities.FilterQueryExpression;
using System.Collections;
using System.Collections.Generic;
using TraceEventTests.FilterQueryExpressions;
using Xunit;

namespace TraceEventTests
{
    public sealed class FilterQueryExpressionTests
    {
        [Theory]
        [InlineData("Property == 1,001", "Property", "1,001")]
        [InlineData("Property== 1,001", "Property", "1,001")]
        [InlineData("Property==1,001", "Property", "1,001")]
        [InlineData("Property ==1,001", "Property", "1,001")]
        [InlineData("ThreadData Contains ,", "ThreadData", "1,001")]
        [InlineData("Depth <= 1", "Depth", "1")]
        [InlineData("Depth<= 1", "Depth", "1")]
        [InlineData("Depth <=1", "Depth", "1")]
        [InlineData("Depth<=1", "Depth", "1")]
        [InlineData("Depth >= 1", "Depth", "10")]
        [InlineData("Depth>= 1", "Depth", "10")]
        [InlineData("Depth >=1", "Depth", "10")]
        [InlineData("Depth>=1", "Depth", "10")]
        [InlineData("Depth != 1", "Depth", "10")]
        [InlineData("Depth!= 1", "Depth", "10")]
        [InlineData("Depth !=1", "Depth", "10")]
        [InlineData("Depth!=1", "Depth", "10")]
        [InlineData("Depth < 1", "Depth", "0")]
        [InlineData("Depth< 1", "Depth", "0")]
        [InlineData("Depth <1", "Depth", "0")]
        [InlineData("Depth<1", "Depth", "0")]
        [InlineData("Depth > 1", "Depth", "10")]
        [InlineData("Depth> 1", "Depth", "10")]
        [InlineData("Depth >1", "Depth", "10")]
        [InlineData("Depth>1", "Depth", "10")]
        [InlineData("FakeEvent::Depth > 1", "Depth", "10", "FakeEvent")]
        [InlineData("FakeEvent::Depth> 1", "Depth", "10", "FakeEvent")]
        [InlineData("FakeEvent::Depth >1", "Depth", "10", "FakeEvent")]
        [InlineData("FakeEvent::Depth>1", "Depth", "10", "FakeEvent")]
        public void MatchForTraceEvent_PropertyAndEventNameProvided_True(string expression, string propertyName, string value, string eventName = "")
        {
            FilterQueryExpression filterQueryExpression = new FilterQueryExpression(expression);
            var fakeTraceEvent = new FilterQueryExpressionTestTraceEvent(propertyName, value, eventName);
            var matched = filterQueryExpression.Match(fakeTraceEvent);
            Assert.True(matched);
        }

        private sealed class FilterQueryExpressionTree_SimpleExpressions_TrueCases : IEnumerable<object[]>
        {
            private readonly List<object[]> _data = new List<object[]>
            {
                new object[]
                {
                    "Property == 1,001",
                    new Dictionary<string, string> {{ "Property", "1,001" }},
                    "GC/Start"
                },
                new object[]
                {
                    "Property== 1,001",
                    new Dictionary<string, string> {{ "Property", "1,001" }},
                    "GC/Start"
                },
                new object[]
                {
                    "Property ==1,001",
                    new Dictionary<string, string> {{ "Property", "1,001" }},
                    "GC/Start"
                },
                new object[]
                {
                    "Property==1,001",
                    new Dictionary<string, string> {{ "Property", "1,001" }},
                    "GC/Start"
                },
                new object[]
                {
                    "Depth <= 1",
                    new Dictionary<string, string> {{ "Depth", "1" }},
                    "GC/Start"
                },
                new object[]
                {
                    "Depth<= 1",
                    new Dictionary<string, string> {{ "Depth", "1" }},
                    "GC/Start"
                },
                new object[]
                {
                    "Depth <=1",
                    new Dictionary<string, string> {{ "Depth", "1" }},
                    "GC/Start"
                },
                new object[]
                {
                    "Depth<=1",
                    new Dictionary<string, string> {{ "Depth", "1" }},
                    "GC/Start"
                },
                new object[]
                {
                    "Depth >= 10",
                    new Dictionary<string, string> {{ "Depth", "20" }},
                    "GC/Start"
                },
                new object[]
                {
                    "Depth>= 10",
                    new Dictionary<string, string> {{ "Depth", "20" }},
                    "GC/Start"
                },
                new object[]
                {
                    "Depth >=10",
                    new Dictionary<string, string> {{ "Depth", "20" }},
                    "GC/Start"
                },
                new object[]
                {
                    "Depth>=10",
                    new Dictionary<string, string> {{ "Depth", "20" }},
                    "GC/Start"
                },
                new object[]
                {
                    "Depth <= 10",
                    new Dictionary<string, string> {{ "Depth", "5" }},
                    "GC/Start"
                },
                new object[]
                {
                    "Depth != 10",
                    new Dictionary<string, string> {{ "Depth", "5" }},
                    "GC/Start"
                },
                new object[]
                {
                    "Depth > 10",
                    new Dictionary<string, string> {{ "Depth", "25" }},
                    "GC/Start"
                },
                new object[]
                {
                    "Depth >= 10",
                    new Dictionary<string, string> {{ "Depth", "25" }},
                    "GC/Start"
                },
                new object[]
                {
                    "ThreadData Contains 10",
                    new Dictionary<string, string> {{ "ThreadData", "1001" }},
                    "GC/Start"
                },
                new object[]
                {
                    "ThreadData Contains 10",
                    new Dictionary<string, string> {{ "ThreadData", "1001" }},
                    "GC/Start"
                },
                new object[]
                {
                    "GC/Start::Depth == 10",
                    new Dictionary<string, string> {{ "Depth", "10" }},
                    "GC/Start"
                },
                new object[]
                {
                    "GC/Start::Depth == 10",
                    new Dictionary<string, string> {{ "Depth", "10" }},
                    "GC/Start"
                },
            };

            public IEnumerator<object[]> GetEnumerator() => _data.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Theory]
        [ClassData(typeof(FilterQueryExpressionTree_SimpleExpressions_TrueCases))]
        public void MatchForDictionary_PropertyAndEventNameProvided_True(string expression, Dictionary<string, string> propertiesToValues, string eventName = "")
        {
            FilterQueryExpression filterQueryExpression = new FilterQueryExpression(expression);
            var matched = filterQueryExpression.Match(propertiesToValues, eventName);
            Assert.True(matched);
        }

        [Theory]
        [InlineData("Property == 1,001", "Property", "1,002")]
        [InlineData("Property != 1,001", "Property", "1,001")]
        [InlineData("Depth == 1", "Depth", "2")]
        [InlineData("Depth >= 1", "Depth", "0")]
        [InlineData("Depth <= 1", "Depth", "2")]
        [InlineData("Depth <= 1", "Property", "2")]
        [InlineData("Property Contains ,", "Property", "2")]
        [InlineData("FakeEvent::Depth > 20", "Depth", "2", "FakeEvent")]
        public void MatchForTraceEvent_PropertyAndEventNameProvided_False(string expression, string propertyName, string value, string eventName = "")
        {
            FilterQueryExpression filterQueryExpression = new FilterQueryExpression(expression);
            var fakeTraceEvent = new FilterQueryExpressionTestTraceEvent(propertyName, value, eventName);
            var matched = filterQueryExpression.Match(fakeTraceEvent);
            Assert.False(matched);
        }

        private sealed class FilterQueryExpressionTree_SimpleExpressions_FalseCases : IEnumerable<object[]>
        {
            private readonly List<object[]> _data = new List<object[]>
            {
                new object[]
                {
                    "Property == 1,001",
                    new Dictionary<string, string> {{ "Property", "1,002" }},
                    "GC/Start"
                },
                new object[]
                {
                    "Property != 1,001",
                    new Dictionary<string, string> {{ "Property", "1,001" }},
                    "GC/Start"
                },
                new object[]
                {
                    "Depth == 1",
                    new Dictionary<string, string> {{ "Depth", "2" }},
                    "GC/Start"
                },
                new object[]
                {
                    "Depth >= 1",
                    new Dictionary<string, string> {{ "Depth", "0" }},
                    "GC/Start"
                },
                new object[]
                {
                    "Depth <= 1",
                    new Dictionary<string, string> {{ "Depth", "2" }},
                    "GC/Start"
                },
                new object[]
                {
                    "Depth <= 1",
                    new Dictionary<string, string> {{ "Property", "2" }},
                    "GC/Start"
                },
                new object[]
                {
                    "Property Contains ,",
                    new Dictionary<string, string> {{ "Property", "2" }},
                    "GC/Start"
                },
                new object[]
                {
                    "FakeEvent::Depth > 20",
                    new Dictionary<string, string> {{ "Depth", "2" }},
                    "FakeEvent"
                },
            };

            public IEnumerator<object[]> GetEnumerator() => _data.GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Theory]
        [ClassData(typeof(FilterQueryExpressionTree_SimpleExpressions_FalseCases))]
        public void MatchForDictionary_PropertyAndEventNameProvided_False(string expression, Dictionary<string, string> propertiesToValues, string eventName = "")
        {
            FilterQueryExpression filterQueryExpression = new FilterQueryExpression(expression);
            var matched = filterQueryExpression.Match(propertiesToValues, eventName);
            Assert.False(matched);
        }
    }
}
