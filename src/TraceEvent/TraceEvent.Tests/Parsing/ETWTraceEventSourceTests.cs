using Microsoft.Diagnostics.Tracing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Xunit;

namespace TraceEventTests
{
    public class ETWTraceEventSourceTests
    {
        [WindowsFact]
        public void InvalidHandleReportsTheNativeError()
        {
            using (var source = OpenFixture())
            {
                var field = typeof(ETWTraceEventSource).GetField("handles", BindingFlags.NonPublic | BindingFlags.Instance);
                var handles = (TraceEventNativeMethods.SafeTraceHandle[])field.GetValue(source);
                handles[0].Dispose();
                var error = Assert.Throws<COMException>(() => source.Process());
                Assert.Equal(unchecked((int)0x80070006), error.HResult);
            }
        }

        [WindowsFact]
        public void ExplicitCancellationDoesNotThrow()
        {
            using (var source = OpenFixture())
            {
                source.AllEvents += data => source.StopProcessing();
                Assert.False(source.Process());
            }
        }

        private static ETWTraceEventSource OpenFixture()
        {
            return new ETWTraceEventSource(Path.Combine("inputs", "Regression", "SelfDescribingSingleEvent.etl"));
        }
    }
}
