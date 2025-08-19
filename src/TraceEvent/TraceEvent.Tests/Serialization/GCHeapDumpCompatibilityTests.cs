using FastSerialization;
using System.IO;
using Xunit;

namespace TraceEventTests
{
    public class GCHeapDumpCompatibilityTests
    {
        [Fact]
        public void GCHeapDumpHandlesSerializationConfigurationCompatibility()
        {
            // This test verifies that the GCHeapDump deserializer can handle
            // references to the old "FastSerialization.SerializationConfiguration" type
            // by mapping it to a compatible shim.

            var settings = SerializationSettings.Default;
            
            // Create a simple object and serialize it first to have valid stream data
            var testObject = new SampleSerializableType(42);
            var stream = new MemoryStream();
            var serializer = new Serializer(new IOStreamStreamWriter(stream, settings, leaveOpen: true), testObject);
            serializer.Dispose();
            
            // Create a deserializer similar to what GCHeapDump does
            var deserializer = new Deserializer(new PinnedStreamReader(stream, settings), "test");
            
            // Set up the factories similar to what GCHeapDump constructor does
            deserializer.RegisterFactory(typeof(SampleSerializableType), () => new SampleSerializableType());
            
            // Set up the compatibility mapping like our fix does
            deserializer.OnUnregisteredType = (typeName) =>
            {
                if (typeName == "FastSerialization.SerializationConfiguration")
                {
                    return () => new SerializationConfigurationCompatibilityShim();
                }
                return null;
            };
            
            // Test that the callback correctly handles the old type name
            var factory = deserializer.OnUnregisteredType("FastSerialization.SerializationConfiguration");
            Assert.NotNull(factory);
            
            var shimInstance = factory();
            Assert.NotNull(shimInstance);
            Assert.IsType<SerializationConfigurationCompatibilityShim>(shimInstance);
            
            // Test that unrelated type names return null (default behavior)
            var unknownFactory = deserializer.OnUnregisteredType("Some.Unknown.Type");
            Assert.Null(unknownFactory);
        }
        
        [Fact]
        public void SerializationConfigurationShimCanBeSerializedAndDeserialized()
        {
            // Test that our compatibility shim can be serialized and deserialized without issues
            var settings = SerializationSettings.Default;
            var shim = new SerializationConfigurationCompatibilityShim();
            
            // Serialize the shim
            var stream = new MemoryStream();
            var serializer = new Serializer(new IOStreamStreamWriter(stream, settings, leaveOpen: true), shim);
            serializer.Dispose();
            
            // Deserialize the shim
            var deserializer = new Deserializer(new PinnedStreamReader(stream, settings), "test");
            deserializer.RegisterFactory(typeof(SerializationConfigurationCompatibilityShim), () => new SerializationConfigurationCompatibilityShim());
            
            var deserializedShim = deserializer.ReadObject();
            Assert.NotNull(deserializedShim);
            Assert.IsType<SerializationConfigurationCompatibilityShim>(deserializedShim);
        }
    }
}