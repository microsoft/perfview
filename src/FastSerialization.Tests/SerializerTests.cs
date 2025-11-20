using System;
using System.IO;
using FastSerialization;
using Xunit;

namespace FastSerializationTests
{
    /// <summary>
    /// Test object that holds basic primitive types for serialization testing
    /// </summary>
    public class PrimitiveTypes : IFastSerializable
    {
        public int IntValue { get; set; }
        public string StringValue { get; set; }
        public byte ByteValue { get; set; }
        public short ShortValue { get; set; }
        public long LongValue { get; set; }
        
        public PrimitiveTypes() { }
        
        public PrimitiveTypes(int intVal, string strVal, byte byteVal, short shortVal, long longVal)
        {
            IntValue = intVal;
            StringValue = strVal;
            ByteValue = byteVal;
            ShortValue = shortVal;
            LongValue = longVal;
        }
        
        public void ToStream(Serializer serializer)
        {
            serializer.Write(IntValue);
            serializer.Write(StringValue);
            serializer.Write(ByteValue);
            serializer.Write(ShortValue);
            serializer.Write(LongValue);
        }
        
        public void FromStream(Deserializer deserializer)
        {
            IntValue = deserializer.ReadInt();
            StringValue = deserializer.ReadString();
            ByteValue = deserializer.ReadByte();
            ShortValue = deserializer.ReadInt16();
            LongValue = deserializer.ReadInt64();
        }
    }

    /// <summary>
    /// Tests for basic serialization and deserialization using Serializer and Deserializer
    /// </summary>
    public class SerializerTests
    {
        [Fact]
        public void BasicSerializationRoundTrip()
        {
            var stream = new MemoryStream();
            var original = new PrimitiveTypes(42, "Hello World", 255, 12345, 9876543210L);
            
            // Serialize
            using (var serializer = new Serializer(stream, original, leaveOpen: true))
            {
            }
            
            stream.Position = 0;
            
            // Deserialize
            using (var deserializer = new Deserializer(stream, "test", leaveOpen: true, SerializationSettings.Default))
            {
                deserializer.RegisterFactory(typeof(PrimitiveTypes), () => new PrimitiveTypes());
                PrimitiveTypes deserialized;
                deserializer.GetEntryObject(out deserialized);
                
                Assert.Equal(original.IntValue, deserialized.IntValue);
                Assert.Equal(original.StringValue, deserialized.StringValue);
                Assert.Equal(original.ByteValue, deserialized.ByteValue);
                Assert.Equal(original.ShortValue, deserialized.ShortValue);
                Assert.Equal(original.LongValue, deserialized.LongValue);
            }
        }

        [Fact]
        public void SerializeNullString()
        {
            var stream = new MemoryStream();
            var original = new PrimitiveTypes(0, null, 0, 0, 0);
            
            using (var serializer = new Serializer(stream, original, leaveOpen: true))
            {
            }
            
            stream.Position = 0;
            
            using (var deserializer = new Deserializer(stream, "test", leaveOpen: true, SerializationSettings.Default))
            {
                deserializer.RegisterFactory(typeof(PrimitiveTypes), () => new PrimitiveTypes());
                PrimitiveTypes deserialized;
                deserializer.GetEntryObject(out deserialized);
                Assert.Null(deserialized.StringValue);
            }
        }

        [Fact]
        public void SerializeEmptyString()
        {
            var stream = new MemoryStream();
            var original = new PrimitiveTypes(0, "", 0, 0, 0);
            
            using (var serializer = new Serializer(stream, original, leaveOpen: true))
            {
            }
            
            stream.Position = 0;
            
            using (var deserializer = new Deserializer(stream, "test", leaveOpen: true, SerializationSettings.Default))
            {
                deserializer.RegisterFactory(typeof(PrimitiveTypes), () => new PrimitiveTypes());
                PrimitiveTypes deserialized;
                deserializer.GetEntryObject(out deserialized);
                Assert.Equal("", deserialized.StringValue);
            }
        }

        [Fact]
        public void SerializeLargeString()
        {
            var largeString = new string('A', 10000);
            var stream = new MemoryStream();
            var original = new PrimitiveTypes(0, largeString, 0, 0, 0);
            
            using (var serializer = new Serializer(stream, original, leaveOpen: true))
            {
            }
            
            stream.Position = 0;
            
            using (var deserializer = new Deserializer(stream, "test", leaveOpen: true, SerializationSettings.Default))
            {
                deserializer.RegisterFactory(typeof(PrimitiveTypes), () => new PrimitiveTypes());
                PrimitiveTypes deserialized;
                deserializer.GetEntryObject(out deserialized);
                Assert.Equal(largeString, deserialized.StringValue);
            }
        }

        [Fact]
        public void SerializeMinMaxValues()
        {
            var stream = new MemoryStream();
            var original1 = new PrimitiveTypes(int.MinValue, "min", byte.MinValue, short.MinValue, long.MinValue);
            
            using (var serializer = new Serializer(stream, original1, leaveOpen: true))
            {
            }
            
            stream.Position = 0;
            
            using (var deserializer = new Deserializer(stream, "test", leaveOpen: true, SerializationSettings.Default))
            {
                deserializer.RegisterFactory(typeof(PrimitiveTypes), () => new PrimitiveTypes());
                PrimitiveTypes deserialized;
                deserializer.GetEntryObject(out deserialized);
                Assert.Equal(byte.MinValue, deserialized.ByteValue);
                Assert.Equal(short.MinValue, deserialized.ShortValue);
                Assert.Equal(int.MinValue, deserialized.IntValue);
                Assert.Equal(long.MinValue, deserialized.LongValue);
            }
            
            stream.SetLength(0);
            stream.Position = 0;
            
            var original2 = new PrimitiveTypes(int.MaxValue, "max", byte.MaxValue, short.MaxValue, long.MaxValue);
            
            using (var serializer = new Serializer(stream, original2, leaveOpen: true))
            {
            }
            
            stream.Position = 0;
            
            using (var deserializer = new Deserializer(stream, "test", leaveOpen: true, SerializationSettings.Default))
            {
                deserializer.RegisterFactory(typeof(PrimitiveTypes), () => new PrimitiveTypes());
                PrimitiveTypes deserialized;
                deserializer.GetEntryObject(out deserialized);
                Assert.Equal(byte.MaxValue, deserialized.ByteValue);
                Assert.Equal(short.MaxValue, deserialized.ShortValue);
                Assert.Equal(int.MaxValue, deserialized.IntValue);
                Assert.Equal(long.MaxValue, deserialized.LongValue);
            }
        }
    }
}
