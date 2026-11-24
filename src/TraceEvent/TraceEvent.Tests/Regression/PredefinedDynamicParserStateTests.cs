using Microsoft.Diagnostics.Tracing.Parsers;
using System;
using Xunit;

namespace TraceEventTests
{
    /// <summary>
    /// Tests predefined dynamic template and negative-lookup key semantics.
    /// </summary>
    public class PredefinedDynamicParserStateTests
    {
        [Fact]
        public void CachedMissIsDistinctFromAbsentKeyAndNormalizesNullName()
        {
            var state = new PredefinedDynamicTraceEventParserState();
            Guid provider = Guid.NewGuid();
            Assert.False(state.TryGetLookup(provider, null, out _));
            state.AddLookup(provider, null, null);
            Assert.True(state.TryGetLookup(provider, string.Empty, out var cached));
            Assert.Null(cached);
            Assert.False(state.TryGetLookup(Guid.NewGuid(), string.Empty, out _));
            Assert.False(state.TryGetLookup(provider, "Event", out _));
        }

        [Fact]
        public void KeysPreserveProviderAndOrdinalEventNameIdentity()
        {
            var state = new PredefinedDynamicTraceEventParserState();
            Guid provider = Guid.NewGuid();
            var template = new TestEvent(provider);
            state.AddTemplate(provider, "Event", template);
            state.AddLookup(provider, "Event", template);
            Assert.True(state.TryGetTemplate(provider, "Event", out var registered));
            Assert.Same(template, registered);
            Assert.True(state.TryGetLookup(provider, "Event", out var cached));
            Assert.Same(template, cached);
            Assert.False(state.TryGetTemplate(provider, "event", out _));
            Assert.False(state.TryGetLookup(provider, "event", out _));
            Assert.False(state.TryGetTemplate(Guid.NewGuid(), "Event", out _));
            state.AddLookup(provider, "Event", null);
            Assert.True(state.TryGetLookup(provider, "Event", out cached));
            Assert.Null(cached);
            Assert.True(state.TryGetTemplate(provider, "Event", out registered));
            Assert.Same(template, registered);
        }

        #region private
        private sealed class TestEvent : PredefinedDynamicEvent
        {
            public TestEvent(Guid provider) : base("Event", provider, "TestProvider") { }
            public override string[] PayloadNames => Array.Empty<string>();
            public override object PayloadValue(int index) => throw new ArgumentOutOfRangeException(nameof(index));
        }
        #endregion
    }
}
