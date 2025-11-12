using System;
using System.IO;
using FastSerialization;
using Xunit;

namespace FastSerializationTests
{
    /// <summary>
    /// Test object for StreamLabel testing
    /// </summary>
    public class StreamLabelTestObject : IFastSerializable
    {
        public int Value1 { get; set; }
        public int Value2 { get; set; }
        public int Value3 { get; set; }
        
        public StreamLabelTestObject() { }
        
        public StreamLabelTestObject(int v1, int v2, int v3)
        {
            Value1 = v1;
            Value2 = v2;
            Value3 = v3;
        }
        
        public void ToStream(Serializer serializer)
        {
            serializer.Write(Value1);
            serializer.Write(Value2);
            serializer.Write(Value3);
        }
        
        public void FromStream(Deserializer deserializer)
        {
            Value1 = deserializer.ReadInt();
            Value2 = deserializer.ReadInt();
            Value3 = deserializer.ReadInt();
        }
    }

    /// <summary>
    /// Tests for serialization settings
    /// </summary>
    public class StreamLabelTests
    {
        [Fact]
        public void StreamLabelInvalidValue()
        {
            Assert.Equal(-1L, (long)StreamLabel.Invalid);
        }

        [Fact]
        public void SerializationSettingsNotNull()
        {
            var settings = SerializationSettings.Default;
            Assert.NotNull(settings);
        }

        [Fact]
        public void SerializationSettingsWithStreamLabelWidth()
        {
            var settings = SerializationSettings.Default.WithStreamLabelWidth(StreamLabelWidth.FourBytes);
            Assert.NotNull(settings);
        }

        [Fact]
        public void SerializationSettingsWithStreamReaderAlignment()
        {
            var settings = SerializationSettings.Default.WithStreamReaderAlignment(StreamReaderAlignment.OneByte);
            Assert.NotNull(settings);
        }

        [Fact]
        public void SerializationSettingsChaining()
        {
            var settings = SerializationSettings.Default
                .WithStreamLabelWidth(StreamLabelWidth.FourBytes)
                .WithStreamReaderAlignment(StreamReaderAlignment.FourBytes);
            
            Assert.NotNull(settings);
        }

        [Fact]
        public void SerializeWithDifferentSettings()
        {
            var stream = new MemoryStream();
            var settings = SerializationSettings.Default.WithStreamLabelWidth(StreamLabelWidth.FourBytes);
            var original = new StreamLabelTestObject(100, 200, 300);
            
            using (var serializer = new Serializer(stream, original, leaveOpen: true))
            {
            }
            
            stream.Position = 0;
            
            using (var deserializer = new Deserializer(stream, "test", leaveOpen: true, settings))
            {
                deserializer.RegisterFactory(typeof(StreamLabelTestObject), () => new StreamLabelTestObject());
                StreamLabelTestObject deserialized;
                deserializer.GetEntryObject(out deserialized);
                Assert.Equal(original.Value1, deserialized.Value1);
                Assert.Equal(original.Value2, deserialized.Value2);
                Assert.Equal(original.Value3, deserialized.Value3);
            }
        }
    }
}
