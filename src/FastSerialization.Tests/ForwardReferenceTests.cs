using System.Collections;
using System.Reflection;
using FastSerialization;
using Xunit;

namespace FastSerializationTests
{
    /// <summary>
    /// Tests forward-reference storage and resolution for serialized object graphs.
    /// </summary>
    public class ForwardReferenceTests
    {
        [Fact]
        public void SparseForwardDefinitionStorageIsBounded()
        {
            VerifySparseForwardDefinition(4096);
        }

        [Theory]
        [InlineData(int.MaxValue)]
        [InlineData(int.MaxValue - 1)]
        public void ExtremeForwardDefinitionStorageIsBounded(int index)
        {
            VerifySparseForwardDefinition(index);
        }

        [Fact]
        public void ForwardDefinitionsResolveOutOfOrder()
        {
            using (var writer = new MemoryStreamWriter(SerializationSettings.Default))
            {
                writer.Write("!FastSerialization.1");
                StreamLabel second = WriteForwardDefinition(writer, 2);
                StreamLabel first = WriteForwardDefinition(writer, 0);
                StreamLabel middle = WriteForwardDefinition(writer, 1);

                using (var deserializer = CreateDeserializer(writer))
                {
                    var original = deserializer.GetEntryObject();
                    deserializer.ReadObject();
                    deserializer.ReadObject();

                    Assert.Equal(first, deserializer.ResolveForwardReference((ForwardReference)0));
                    Assert.Equal(middle, deserializer.ResolveForwardReference((ForwardReference)1));
                    Assert.Equal(second, deserializer.ResolveForwardReference((ForwardReference)2));

                    deserializer.Goto(second);
                    Assert.Same(original, deserializer.ReadObject());
                    Assert.Equal(3, GetDefinitionCount(deserializer));
                }
            }
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(-2)]
        [InlineData(int.MinValue)]
        public void NegativeForwardDefinitionsAreRejected(int index)
        {
            using (var writer = new MemoryStreamWriter(SerializationSettings.Default))
            {
                writer.Write("!FastSerialization.1");
                WriteForwardDefinition(writer, index);

                using (var deserializer = CreateDeserializer(writer))
                {
                    var exception = Assert.Throws<SerializationException>(() => deserializer.GetEntryObject());
                    Assert.Contains("forward reference", exception.Message);
                }
            }
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(-2)]
        [InlineData(int.MinValue)]
        public void NegativeForwardReferencesAreRejected(int index)
        {
            using (var writer = new MemoryStreamWriter(SerializationSettings.Default))
            {
                writer.Write("!FastSerialization.1");
                writer.Write(index);

                using (var deserializer = CreateDeserializer(writer))
                {
                    Assert.Throws<SerializationException>(() => deserializer.ReadForwardReference());
                    Assert.Throws<SerializationException>(() =>
                        deserializer.ResolveForwardReference((ForwardReference)index));
                }
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(4096)]
        [InlineData(int.MaxValue)]
        public void UndefinedForwardReferencesAreRejected(int index)
        {
            using (var writer = new MemoryStreamWriter(SerializationSettings.Default))
            {
                using (var serializer = new Serializer(writer, new SimpleObject(42, "test")))
                {
                }

                using (var deserializer = CreateDeserializer(writer))
                {
                    deserializer.GetEntryObject();
                    Assert.Throws<SerializationException>(() =>
                        deserializer.ResolveForwardReference((ForwardReference)index));
                }
            }
        }

        [Theory]
        [InlineData(StreamLabelWidth.FourBytes, false)]
        [InlineData(StreamLabelWidth.EightBytes, false)]
        [InlineData(StreamLabelWidth.FourBytes, true)]
        [InlineData(StreamLabelWidth.EightBytes, true)]
        public void InvalidForwardReferenceLabelsAreReturned(StreamLabelWidth width, bool preserveCurrent)
        {
            StreamLabel expectedLabel = width == StreamLabelWidth.FourBytes
                ? (StreamLabel)uint.MaxValue : StreamLabel.Invalid;
            using (var writer = new MemoryStreamWriter(
                SerializationSettings.Default.WithStreamLabelWidth(width)))
            {
                writer.Write("!FastSerialization.1");
                StreamLabel entryLabel = WriteForwardDefinition(writer, 1);
                writer.Write((byte)6); // EndObject (end of objects)
                StreamLabel tableLabel = writer.GetLabel();
                writer.Write(1);
                writer.Write(StreamLabel.Invalid);
                StreamLabel trailerLabel = writer.GetLabel();
                writer.Write(tableLabel);
                writer.WriteSuffixLabel(trailerLabel);

                using (var deserializer = CreateDeserializer(writer))
                {
                    Assert.IsType<SimpleObject>(deserializer.GetEntryObject());
                    StreamLabel position = deserializer.Current;

                    Assert.Equal(expectedLabel,
                        deserializer.ResolveForwardReference((ForwardReference)0, preserveCurrent));
                    Assert.Equal(preserveCurrent ? position : trailerLabel, deserializer.Current);
                    Assert.Equal(expectedLabel,
                        deserializer.ResolveForwardReference((ForwardReference)0, preserveCurrent));
                    Assert.Equal(preserveCurrent ? position : trailerLabel, deserializer.Current);
                    Assert.Equal(entryLabel, deserializer.ResolveForwardReference((ForwardReference)1));
                    Assert.Equal(2, GetDefinitionCount(deserializer));
                }
            }
        }

        [Theory]
        [InlineData(StreamLabelWidth.FourBytes, false)]
        [InlineData(StreamLabelWidth.EightBytes, false)]
        [InlineData(StreamLabelWidth.FourBytes, true)]
        [InlineData(StreamLabelWidth.EightBytes, true)]
        public void DeferredObjectsPreserveSharedReferencesAndCycles(StreamLabelWidth width, bool sequential)
        {
            var first = new ReferenceNode { Value = 42 };
            var second = new ReferenceNode { Value = 123, First = first };
            first.First = second;
            first.Second = second;

            using (var writer = new MemoryStreamWriter(
                SerializationSettings.Default.WithStreamLabelWidth(width)))
            {
                using (var serializer = new Serializer(writer, first))
                {
                }

                using (var deserializer = CreateDeserializer(writer))
                {
                    deserializer.RegisterFactory(typeof(ReferenceNode), () => new ReferenceNode());
                    if (sequential)
                    {
                        var field = typeof(Deserializer).GetField(
                            "allowLazyDeserialization", BindingFlags.Instance | BindingFlags.NonPublic);
                        Assert.NotNull(field);
                        field.SetValue(deserializer, false);
                    }
                    var result = (ReferenceNode)deserializer.GetEntryObject();

                    Assert.Equal(42, result.Value);
                    Assert.Equal(123, result.First.Value);
                    Assert.Same(result.First, result.Second);
                    Assert.Same(result, result.First.First);
                    Assert.Null(result.First.Second);
                    Assert.Equal(1, GetDefinitionCount(deserializer));
                }
            }
        }

        [Theory]
        [InlineData(StreamLabelWidth.FourBytes)]
        [InlineData(StreamLabelWidth.EightBytes)]
        public void DeferredRegionsPreserveLazyReading(StreamLabelWidth width)
        {
            using (var writer = new MemoryStreamWriter(
                SerializationSettings.Default.WithStreamLabelWidth(width)))
            {
                using (var serializer = new Serializer(writer, new LazyObject { Value = 123 }))
                {
                }

                using (var deserializer = CreateDeserializer(writer))
                {
                    deserializer.RegisterFactory(typeof(LazyObject), () => new LazyObject());
                    var result = (LazyObject)deserializer.GetEntryObject();
                    var position = deserializer.Current;

                    Assert.False(result.Region.IsFinished);
                    Assert.Equal(0, result.Value);
                    result.Region.FinishRead(preserveStreamPosition: true);
                    Assert.True(result.Region.IsFinished);
                    Assert.Equal(123, result.Value);
                    Assert.Equal(position, deserializer.Current);
                }
            }
        }

        #region private
        private static void VerifySparseForwardDefinition(int index)
        {
            using (var writer = new MemoryStreamWriter(SerializationSettings.Default))
            {
                writer.Write("!FastSerialization.1");
                StreamLabel label = WriteForwardDefinition(writer, index);

                using (var deserializer = CreateDeserializer(writer))
                {
                    var result = (SimpleObject)deserializer.GetEntryObject();
                    Assert.Equal(42, result.IntValue);
                    Assert.Equal("test", result.StringValue);
                    Assert.Equal(label, deserializer.ResolveForwardReference((ForwardReference)index));
                    Assert.Equal(1, GetDefinitionCount(deserializer));
                }
            }
        }

        private static StreamLabel WriteForwardDefinition(IStreamWriter writer, int index)
        {
            StreamLabel label = writer.GetLabel();
            writer.Write((byte)7); // ForwardDefinition
            writer.Write(index);
            writer.Write((byte)4); // BeginObject
            writer.Write((byte)5); // BeginPrivateObject (SerializationType)
            writer.Write((byte)1); // NullReference (type of SerializationType)
            writer.Write(0);
            writer.Write(0);
            writer.Write(typeof(SimpleObject).FullName);
            writer.Write((byte)6); // EndObject (SerializationType)
            writer.Write(42);
            writer.Write("test");
            writer.Write((byte)6); // EndObject
            return label;
        }

        private static Deserializer CreateDeserializer(MemoryStreamWriter writer)
        {
            var deserializer = new Deserializer(writer.GetReader(), "forward-reference fixture");
            deserializer.RegisterFactory(typeof(SimpleObject), () => new SimpleObject());
            return deserializer;
        }

        private static int GetDefinitionCount(Deserializer deserializer)
        {
            // Inspect retained storage rather than relying on nondeterministic GC or timing measurements.
            var field = typeof(Deserializer).GetField(
                "forwardReferenceDefinitions", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            var definitions = Assert.IsAssignableFrom<ICollection>(field.GetValue(deserializer));
            return definitions.Count;
        }

        private sealed class ReferenceNode : IFastSerializable
        {
            public int Value { get; set; }
            public ReferenceNode First { get; set; }
            public ReferenceNode Second { get; set; }

            public void ToStream(Serializer serializer)
            {
                serializer.Write(Value);
                serializer.WriteDefered(First);
                serializer.WriteDefered(Second);
            }

            public void FromStream(Deserializer deserializer)
            {
                Value = deserializer.ReadInt();
                First = (ReferenceNode)deserializer.ReadObject();
                Second = (ReferenceNode)deserializer.ReadObject();
            }
        }

        private sealed class LazyObject : IFastSerializable
        {
            public int Value;
            public DeferedRegion Region;

            public void ToStream(Serializer serializer)
            {
                Region.Write(serializer, () => serializer.Write(Value));
            }

            public void FromStream(Deserializer deserializer)
            {
                Region.Read(deserializer, () => Value = deserializer.ReadInt());
            }
        }
        #endregion
    }
}
