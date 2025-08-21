using System;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.SourceConverters;

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
        [InlineData("int32 [My.Custom.Assembly] My.Namespace.MyClass::ComplexMethod(class System.Collections.Generic.List`1<string>, int32)[OptimizedTier1]", 
                   "My.Custom.Assembly", "My.Namespace.MyClass::ComplexMethod(class System.Collections.Generic.List`1<string>, int32)")]
        [InlineData("instance void [System.Net.Sockets] System.Net.Sockets.SocketAsyncEngine::EventLoop()[QuickJitted]", 
                   "System.Net.Sockets", "System.Net.Sockets.SocketAsyncEngine::EventLoop()")]
        [InlineData("instance bool [System.Private.CoreLib] System.Threading.LowLevelLifoSemaphore::Wait(int32,bool)[OptimizedTier1]", 
                   "System.Private.CoreLib", "System.Threading.LowLevelLifoSemaphore::Wait(int32,bool)")]
        [InlineData("valuetype Interop/Error [System.Net.Sockets] Interop+Sys::Shutdown(class [System.Runtime]System.Runtime.InteropServices.SafeHandle,valuetype System.Net.Sockets.SocketShutdown)[QuickJitted]", 
                   "System.Net.Sockets", "Interop+Sys::Shutdown(class [System.Runtime]System.Runtime.InteropServices.SafeHandle,valuetype System.Net.Sockets.SocketShutdown)")]
        [InlineData("valuetype [System.Net.Primitives]System.Net.Sockets.SocketError [System.Net.Sockets] System.Net.Sockets.SocketPal::Shutdown(class System.Net.Sockets.SafeSocketHandle,bool,bool,valuetype System.Net.Sockets.SocketShutdown)[QuickJitted]", 
                   "System.Net.Sockets", "System.Net.Sockets.SocketPal::Shutdown(class System.Net.Sockets.SafeSocketHandle,bool,bool,valuetype System.Net.Sockets.SocketShutdown)")]
        public void ParseDotnetJittedSymbolName_ShouldExtractModuleAndMethod(string symbolName, string expectedModule, string expectedMethod)
        {
            // Act
            var result = NettraceUniversalConverter.ParseDotnetJittedSymbolName(symbolName);
            
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
        [InlineData("void [System.Private.CoreLib] System.Threading.ThreadPoolWorkQueue::Dispatch()")]  // Missing optimization level
        [InlineData("void System.Private.CoreLib System.Threading.ThreadPoolWorkQueue::Dispatch()[OptimizedTier1]")]  // Missing brackets around module
        public void ParseDotnetJittedSymbolName_ShouldReturnNullForInvalidFormat(string symbolName)
        {
            // Act
            var result = NettraceUniversalConverter.ParseDotnetJittedSymbolName(symbolName);
            
            // Assert
            Assert.Null(result);
        }
    }
}