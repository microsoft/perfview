using System;
using System.IO;
using FastSerialization;
using Xunit;

namespace FastSerializationTests
{
    /// <summary>
    /// Test class that implements IFastSerializable for testing object serialization
    /// </summary>
    public class SimpleObject : IFastSerializable
    {
        public int IntValue { get; set; }
        public string StringValue { get; set; }
        
        public SimpleObject() { }
        
        public SimpleObject(int intValue, string stringValue)
        {
            IntValue = intValue;
            StringValue = stringValue;
        }
        
        public void ToStream(Serializer serializer)
        {
            serializer.Write(IntValue);
            serializer.Write(StringValue);
        }
        
        public void FromStream(Deserializer deserializer)
        {
            IntValue = deserializer.ReadInt();
            StringValue = deserializer.ReadString();
        }
    }

    /// <summary>
    /// Test class with nested objects
    /// </summary>
    public class ComplexObject : IFastSerializable
    {
        public SimpleObject NestedObject { get; set; }
        public int[] IntArray { get; set; }
        
        public ComplexObject() { }
        
        public ComplexObject(SimpleObject nested, int[] intArray)
        {
            NestedObject = nested;
            IntArray = intArray;
        }
        
        public void ToStream(Serializer serializer)
        {
            serializer.Write(NestedObject);
            
            serializer.Write(IntArray != null);
            if (IntArray != null)
            {
                serializer.Write(IntArray.Length);
                for (int i = 0; i < IntArray.Length; i++)
                {
                    serializer.Write(IntArray[i]);
                }
            }
        }
        
        public void FromStream(Deserializer deserializer)
        {
            NestedObject = (SimpleObject)deserializer.ReadObject();
            
            bool hasIntArray = deserializer.ReadBool();
            if (hasIntArray)
            {
                int length = deserializer.ReadInt();
                IntArray = new int[length];
                for (int i = 0; i < length; i++)
                {
                    IntArray[i] = deserializer.ReadInt();
                }
            }
        }
    }

    /// <summary>
    /// Tests for object serialization using IFastSerializable
    /// </summary>
    public class ObjectSerializationTests
    {
        [Fact]
        public void SerializeSimpleObject()
        {
            var stream = new MemoryStream();
            var original = new SimpleObject(42, "Test String");
            
            using (var serializer = new Serializer(stream, original, leaveOpen: true))
            {
            }
            
            stream.Position = 0;
            
            using (var deserializer = new Deserializer(stream, "test", leaveOpen: true, SerializationSettings.Default))
            {
                deserializer.RegisterFactory(typeof(SimpleObject), () => new SimpleObject());
                SimpleObject deserialized;
                deserializer.GetEntryObject(out deserialized);
                Assert.NotNull(deserialized);
                Assert.Equal(original.IntValue, deserialized.IntValue);
                Assert.Equal(original.StringValue, deserialized.StringValue);
            }
        }

        [Fact]
        public void SerializeComplexObject()
        {
            var stream = new MemoryStream();
            var nested = new SimpleObject(123, "Nested");
            var original = new ComplexObject(nested, new int[] { 1, 2, 3, 4, 5 });
            
            using (var serializer = new Serializer(stream, original, leaveOpen: true))
            {
            }
            
            stream.Position = 0;
            
            using (var deserializer = new Deserializer(stream, "test", leaveOpen: true, SerializationSettings.Default))
            {
                deserializer.RegisterFactory(typeof(ComplexObject), () => new ComplexObject());
                deserializer.RegisterFactory(typeof(SimpleObject), () => new SimpleObject());
                ComplexObject deserialized;
                deserializer.GetEntryObject(out deserialized);
                Assert.NotNull(deserialized);
                Assert.NotNull(deserialized.NestedObject);
                Assert.Equal(original.NestedObject.IntValue, deserialized.NestedObject.IntValue);
                Assert.Equal(original.NestedObject.StringValue, deserialized.NestedObject.StringValue);
                Assert.NotNull(deserialized.IntArray);
                Assert.Equal(original.IntArray.Length, deserialized.IntArray.Length);
                for (int i = 0; i < original.IntArray.Length; i++)
                {
                    Assert.Equal(original.IntArray[i], deserialized.IntArray[i]);
                }
            }
        }

        [Fact]
        public void SerializeObjectWithNullFields()
        {
            var stream = new MemoryStream();
            var original = new SimpleObject(99, null);
            
            using (var serializer = new Serializer(stream, original, leaveOpen: true))
            {
            }
            
            stream.Position = 0;
            
            using (var deserializer = new Deserializer(stream, "test", leaveOpen: true, SerializationSettings.Default))
            {
                deserializer.RegisterFactory(typeof(SimpleObject), () => new SimpleObject());
                SimpleObject deserialized;
                deserializer.GetEntryObject(out deserialized);
                Assert.NotNull(deserialized);
                Assert.Equal(original.IntValue, deserialized.IntValue);
                Assert.Null(deserialized.StringValue);
            }
        }

        [Fact]
        public void SerializeComplexObjectWithNullNested()
        {
            var stream = new MemoryStream();
            var original = new ComplexObject(null, new int[] { 10, 20 });
            
            using (var serializer = new Serializer(stream, original, leaveOpen: true))
            {
            }
            
            stream.Position = 0;
            
            using (var deserializer = new Deserializer(stream, "test", leaveOpen: true, SerializationSettings.Default))
            {
                deserializer.RegisterFactory(typeof(ComplexObject), () => new ComplexObject());
                deserializer.RegisterFactory(typeof(SimpleObject), () => new SimpleObject());
                ComplexObject deserialized;
                deserializer.GetEntryObject(out deserialized);
                Assert.NotNull(deserialized);
                Assert.Null(deserialized.NestedObject);
                Assert.NotNull(deserialized.IntArray);
                Assert.Equal(2, deserialized.IntArray.Length);
                Assert.Equal(10, deserialized.IntArray[0]);
                Assert.Equal(20, deserialized.IntArray[1]);
            }
        }
    }
}
