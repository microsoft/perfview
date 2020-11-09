using Microsoft.Diagnostics.Tracing.StackSources;
using Xunit;

namespace LinuxTracing.Tests
{
    public class InterningTests
    {
        private void InterningStackCountTest(string source, int expectedStackCount)
        {
            ParallelLinuxPerfScriptStackSource stackSource = new ParallelLinuxPerfScriptStackSource(source);
            Assert.Equal(expectedStackCount, stackSource.Interner.CallStackCount);
        }

        [Fact]
        public void OneSample()
        {
            string path = Constants.GetTestingPerfDumpPath("onegeneric");
            InterningStackCountTest(path, expectedStackCount: 3);
        }

        [Fact]
        public void TwoSameSamples()
        {
            string path = Constants.GetTestingPerfDumpPath("twogenericsame");
            InterningStackCountTest(path, expectedStackCount: 3);
        }

        [Fact]
        public void TwoSameLongSamples()
        {
            string path = Constants.GetTestingPerfDumpPath("twogenericsamelongstacks");
            InterningStackCountTest(path, expectedStackCount: 8);
        }

        [Fact]
        public void TwoAlteredLongSamples()
        {
            string path = Constants.GetTestingPerfDumpPath("twodifferentlongstacks");
            InterningStackCountTest(path, expectedStackCount: 10);
        }
    }
}
