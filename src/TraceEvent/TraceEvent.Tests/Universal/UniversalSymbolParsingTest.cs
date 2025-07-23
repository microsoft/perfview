using System;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Diagnostics.Tracing.Etlx;

namespace TraceEventTests
{
    public class UniversalSymbolParsingTest : TestBase
    {
        public UniversalSymbolParsingTest(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [InlineData("void [System.Private.CoreLib] System.Threading.ThreadPoolWorkQueue::Dispatch()[OptimizedTier1]", 
                   "System.Private.CoreLib", "System.Threading.ThreadPoolWorkQueue::Dispatch()")]
        [InlineData("bool [System.Private.CoreLib] System.Threading.PortableThreadPool+WorkerThread::WorkerThreadStart()[OptimizedTier1]", 
                   "System.Private.CoreLib", "System.Threading.PortableThreadPool+WorkerThread::WorkerThreadStart()")]
        [InlineData("void [SomeAssembly] SomeNamespace.SomeClass::SomeMethod(string, int32)[Tier0]", 
                   "SomeAssembly", "SomeNamespace.SomeClass::SomeMethod(string, int32)")]
        public void ParseJittedSymbolName_ShouldExtractModuleAndMethod(string symbolName, string expectedModule, string expectedMethod)
        {
            // Act
            var result = TraceLog.ParseJittedSymbolName(symbolName);
            
            // Assert
            Assert.NotNull(result);
            Assert.Equal(expectedModule, result.Value.moduleName);
            Assert.Equal(expectedMethod, result.Value.methodSignature);
        }

        [Theory]
        [InlineData("regular_symbol_name")]
        [InlineData("void System.Threading.ThreadPoolWorkQueue::Dispatch()")]
        [InlineData("")]
        [InlineData(null)]
        public void ParseJittedSymbolName_ShouldReturnNullForInvalidFormat(string symbolName)
        {
            // Act
            var result = TraceLog.ParseJittedSymbolName(symbolName);
            
            // Assert
            Assert.Null(result);
        }
    }
}