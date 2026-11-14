using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using System;
using System.IO;
using Xunit;

namespace TraceEventTests.Regression
{
    public class PredefinedDynamicProviderTests
    {
        [Fact]
        public void ProvidersTrackTemplateRegistration()
        {
            var state = new PredefinedDynamicTraceEventParserState();
            var first = Guid.NewGuid();
            var second = Guid.NewGuid();
            Assert.False(state.HasProvider(first));
            Assert.False(state.HasProvider(second));

            state.AddTemplate(first, "First", new TestEvent(first));
            state.AddTemplate(first, "Second", new TestEvent(first));
            Assert.True(state.HasProvider(first));
            Assert.False(state.HasProvider(second));

            state.AddTemplate(second, "First", new TestEvent(second));
            Assert.True(state.HasProvider(first));
            Assert.True(state.HasProvider(second));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void EventPipeResolvesRegisteredProviderIncludingLateRegistration(bool registerLate)
        {
            Guid provider = Guid.NewGuid();
            using (var source = CreateSource(provider))
            {
                var parser = new TestParser(source);
                int typedEvents = 0;
                int events = 0;
                parser.AddCallbackForProviderEvents(null, data =>
                {
                    Assert.IsType<TestEvent>(data);
                    Assert.Equal(provider, data.ProviderGuid);
                    Assert.Equal((TraceEventID)15, data.ID);
                    typedEvents++;
                });
                if (!registerLate)
                    parser.Add(provider);

                source.AllEvents += data =>
                {
                    events++;
                    if (registerLate && events == 1)
                    {
                        Assert.IsNotType<TestEvent>(data);
                        // A bypass must not cache a miss that poisons a provider registered later.
                        parser.Add(provider);
                    }
                };
                source.Process();

                Assert.Equal(2, events);
                Assert.Equal(registerLate ? 1 : 2, typedEvents);
            }
        }

        [Fact]
        public void EventPipeUnregisteredProviderStillReachesOtherHandlers()
        {
            Guid provider = Guid.NewGuid();
            using (var source = CreateSource(provider))
            {
                var parser = new TestParser(source);
                parser.Add(Guid.NewGuid());
                int events = 0;
                int lookups = 0;
                source.RegisterUnhandledEventImpl(data =>
                {
                    Assert.True(source.TryGetTemplateFromMetadata(data, out DynamicTraceEventData template));
                    Assert.Equal(provider, template.ProviderGuid);
                    Assert.Equal("TestEvent", template.EventName);
                    lookups++;
                    return false;
                });
                source.AllEvents += data => events++;
                source.Process();
                Assert.Equal(2, events);
                Assert.Equal(2, lookups);
            }
        }

        #region private
        private static EventPipeEventSource CreateSource(Guid provider)
        {
            var writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            writer.WriteMetadataBlock(new EventMetadata(1, "TestProvider", "TestEvent", 15) { ProviderId = provider });
            writer.WriteThreadBlock(w => w.WriteThreadEntry(1, 202, 101));
            writer.WriteEventBlock(w =>
            {
                w.WriteEventBlob(1, 1, 1, p => { });
                w.WriteEventBlob(1, 1, 2, p => { });
            });
            writer.WriteEndBlock();
            return new EventPipeEventSource(new MemoryStream(writer.ToArray()));
        }

        private sealed class TestParser : PredefinedDynamicTraceEventParser
        {
            public TestParser(TraceEventSource source) : base(source) { }
            public void Add(Guid provider) { RegisterTemplate(new TestEvent(provider)); }
            protected override string GetProviderName() { return "TestProvider"; }
        }

        private sealed class TestEvent : PredefinedDynamicEvent
        {
            public TestEvent(Guid provider) : base("TestEvent", provider, "TestProvider") { }
            public override string[] PayloadNames { get { return Array.Empty<string>(); } }
            public override object PayloadValue(int index) { throw new ArgumentOutOfRangeException(nameof(index)); }
        }
        #endregion
    }
}
