using Microsoft.Diagnostics.Tracing.Parsers;
using System;
using Xunit;

namespace TraceEventTests
{
    public class PayloadFetchTests
    {
        [Fact]
        public void PayloadFetch_UInt16_ShouldBeUShort()
        {
            var payloadFetch = new Microsoft.Diagnostics.Tracing.Parsers.DynamicTraceEventData.PayloadFetch(
                0, 
                RegisteredTraceEventParser.TdhInputType.UInt16, 
                0);
            
            Assert.Equal(typeof(ushort), payloadFetch.Type);
            Assert.Equal((ushort)2, payloadFetch.Size);
        }

        [Fact]
        public void PayloadFetch_UInt32_ShouldBeUInt()
        {
            var payloadFetch = new Microsoft.Diagnostics.Tracing.Parsers.DynamicTraceEventData.PayloadFetch(
                0, 
                RegisteredTraceEventParser.TdhInputType.UInt32, 
                0);
            
            Assert.Equal(typeof(uint), payloadFetch.Type);
            Assert.Equal((ushort)4, payloadFetch.Size);
        }

        [Fact]
        public void PayloadFetch_UInt64_ShouldBeULong()
        {
            var payloadFetch = new Microsoft.Diagnostics.Tracing.Parsers.DynamicTraceEventData.PayloadFetch(
                0, 
                RegisteredTraceEventParser.TdhInputType.UInt64, 
                0);
            
            Assert.Equal(typeof(ulong), payloadFetch.Type);
            Assert.Equal((ushort)8, payloadFetch.Size);
        }

        [Fact]
        public void PayloadFetch_Int16_ShouldBeShort()
        {
            var payloadFetch = new Microsoft.Diagnostics.Tracing.Parsers.DynamicTraceEventData.PayloadFetch(
                0, 
                RegisteredTraceEventParser.TdhInputType.Int16, 
                0);
            
            Assert.Equal(typeof(short), payloadFetch.Type);
            Assert.Equal((ushort)2, payloadFetch.Size);
        }

        [Fact]
        public void PayloadFetch_Int32_ShouldBeInt()
        {
            var payloadFetch = new Microsoft.Diagnostics.Tracing.Parsers.DynamicTraceEventData.PayloadFetch(
                0, 
                RegisteredTraceEventParser.TdhInputType.Int32, 
                0);
            
            Assert.Equal(typeof(int), payloadFetch.Type);
            Assert.Equal((ushort)4, payloadFetch.Size);
        }

        [Fact]
        public void PayloadFetch_Int64_ShouldBeLong()
        {
            var payloadFetch = new Microsoft.Diagnostics.Tracing.Parsers.DynamicTraceEventData.PayloadFetch(
                0, 
                RegisteredTraceEventParser.TdhInputType.Int64, 
                0);
            
            Assert.Equal(typeof(long), payloadFetch.Type);
            Assert.Equal((ushort)8, payloadFetch.Size);
        }
    }
}
