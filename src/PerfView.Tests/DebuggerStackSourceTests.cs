using Diagnostics.Tracing.StackSources;
using Microsoft.Diagnostics.Tracing.Stacks;
using System.IO;
using Xunit;

namespace PerfViewTests
{
    public class DebuggerStackSourceTests
    {
        [Fact]
        public void TestLastSampleIsNotDropped()
        {
            // Create a sample cdbstack file with two samples
            var cdbStackContent = @"Call Site
coreclr!JIT_MonEnterWorker_Portable
System_Windows_ni!MS.Internal.ManagedPeerTable.TryGetManagedPeer(IntPtr, Boolean, System.Object ByRef)
Call Site
kernel32!BaseThreadInitThunk
ntdll!RtlUserThreadStart";

            DebuggerStackSource stackSource;
            using (var reader = new StringReader(cdbStackContent))
            {
                stackSource = new DebuggerStackSource(reader);
            }

            // Count the samples
            int sampleCount = 0;
            stackSource.ForEach(sample => sampleCount++);

            // We should have 2 samples, but the bug causes only 1 to be added
            Assert.Equal(2, sampleCount);
        }

        [Fact]
        public void TestSingleSampleIsAdded()
        {
            // Create a sample cdbstack file with a single sample (no subsequent "Call Site")
            var cdbStackContent = @"Call Site
coreclr!JIT_MonEnterWorker_Portable
System_Windows_ni!MS.Internal.ManagedPeerTable.TryGetManagedPeer(IntPtr, Boolean, System.Object ByRef)";

            DebuggerStackSource stackSource;
            using (var reader = new StringReader(cdbStackContent))
            {
                stackSource = new DebuggerStackSource(reader);
            }

            // Count the samples
            int sampleCount = 0;
            stackSource.ForEach(sample => sampleCount++);

            // We should have 1 sample
            Assert.Equal(1, sampleCount);
        }

        [Fact]
        public void TestSampleMetricIsSet()
        {
            // Create a sample cdbstack file with one sample
            var cdbStackContent = @"Call Site
coreclr!JIT_MonEnterWorker_Portable
System_Windows_ni!MS.Internal.ManagedPeerTable.TryGetManagedPeer(IntPtr, Boolean, System.Object ByRef)";

            DebuggerStackSource stackSource;
            using (var reader = new StringReader(cdbStackContent))
            {
                stackSource = new DebuggerStackSource(reader);
            }

            // Check that metric is set to 1 for each sample
            stackSource.ForEach(sample =>
            {
                Assert.Equal(1, sample.Metric);
            });
        }
    }
}
