using Microsoft.Diagnostics.Tracing.TraceUtilities.FilterQueryExpression;
using TraceEventTests.FilterQueryExpressions;
using Xunit;

namespace TraceEventTests
{
    public sealed class FilterQueryExpressionTests
    {
        [Theory]
        [InlineData("ThreadID = 1,001")]
        [InlineData("ThreadID >= 1,001")]
        [InlineData("ThreadID > 1,001")]
        [InlineData("ThreadID < 1,001")]
        [InlineData("ThreadID <= 1,001")]
        [InlineData("ThreadID != 1,001")]
        [InlineData("ThreadID Contains 1")]
        [InlineData("GC/Start::ThreadID Contains 1")]
        public void IsValidExpression_Valid_True(string expression)
        {
            var isValid = FilterQueryExpression.IsValidExpression(expression);
            Assert.True(isValid);
        }

        [Theory]
        [InlineData(" ")]
        [InlineData("Depth 1000")]
        [InlineData("Depth<1000")]
        [InlineData("Depth <1000")]
        [InlineData("Depth> 1000")]
        [InlineData("> 1000")]
        [InlineData("Depth <")]
        [InlineData("Depth ^ 100")]
        [InlineData("GC:: Depth = 100")]
        [InlineData("GC ::Depth = 100")]
        public void IsValidExpression_Invalid_False(string expression)
        {
            var isValid = FilterQueryExpression.IsValidExpression(expression);
            Assert.False(isValid);
        }

        [Theory]
        [InlineData("Property = 1,001", "Property", "1,001")]
        [InlineData("ThreadData Contains ,", "ThreadData", "1,001")]
        [InlineData("Depth <= 1", "Depth", "1")]
        [InlineData("Depth >= 1", "Depth", "10")]
        [InlineData("Depth != 1", "Depth", "10")]
        [InlineData("Depth < 1", "Depth", "0")]
        [InlineData("Depth > 1", "Depth", "10")]
        [InlineData("FakeEvent::Depth > 1", "Depth", "10", "FakeEvent")]
        public void Match_PropertyAndEventNameProvided_True(string expression, string propertyName, string value, string eventName = "")
        {
            FilterQueryExpression filterQueryExpression = new FilterQueryExpression(expression);
            var fakeTraceEvent = new FilterQueryExpressionTestTraceEvent(propertyName, value, eventName);
            var matched = filterQueryExpression.Match(fakeTraceEvent);
            Assert.True(matched);
        }

        [Theory]
        [InlineData("Property = 1,001", "Property", "1,002")]
        [InlineData("Property != 1,001", "Property", "1,001")]
        [InlineData("Depth = 1", "Depth", "2")]
        [InlineData("Depth >= 1", "Depth", "0")]
        [InlineData("Depth <= 1", "Depth", "2")]
        [InlineData("Depth <= 1", "Property", "2")]
        [InlineData("Property Contains ,", "Property", "2")]
        [InlineData("FakeEvent::Depth > 20", "Depth", "2", "FakeEvent")]
        public void Match_PropertyAndEventNameProvided_False(string expression, string propertyName, string value, string eventName = "")
        {
            FilterQueryExpression filterQueryExpression = new FilterQueryExpression(expression);
            var fakeTraceEvent = new FilterQueryExpressionTestTraceEvent(propertyName, value, eventName);
            var matched = filterQueryExpression.Match(fakeTraceEvent);
            Assert.False(matched);
        }
    }
}
