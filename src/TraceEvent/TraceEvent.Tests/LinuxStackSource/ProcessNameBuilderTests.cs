using Microsoft.Diagnostics.Tracing.Stacks;
using Microsoft.Diagnostics.Tracing.StackSources;
using Xunit;

namespace TraceEventTests
{
    public class ProcessNameBuilderTests
    {
        [Fact]
        public void SingleProcessName()
        {
            StackSourceFrameIndex frameIndex = (StackSourceFrameIndex)0x10;

            LinuxPerfScriptProcessNameBuilder builder = new LinuxPerfScriptProcessNameBuilder();
            builder.SaveProcessName(frameIndex, "test-process-name", 100);
            string processName = builder.GetProcessName(frameIndex);
            Assert.Equal("Process test-process-name (100)", processName);
        }

        [Fact]
        public void SingleIgnoredName()
        {
            StackSourceFrameIndex frameIndex = (StackSourceFrameIndex)0x10;

            LinuxPerfScriptProcessNameBuilder builder = new LinuxPerfScriptProcessNameBuilder();
            builder.SaveProcessName(frameIndex, ".NET Finalizer", 100);
            string processName = builder.GetProcessName(frameIndex);
            Assert.Equal("Process .NET Finalizer (100)", processName);
        }

        [Fact]
        public void MultipleWithNoIgnoredNames()
        {
            StackSourceFrameIndex frameIndex = (StackSourceFrameIndex)0x10;

            LinuxPerfScriptProcessNameBuilder builder = new LinuxPerfScriptProcessNameBuilder();
            builder.SaveProcessName(frameIndex, "test-process-name-1", 100);
            builder.SaveProcessName(frameIndex, "test-process-name-2", 100);
            builder.SaveProcessName(frameIndex, "test-process-name-3", 100);
            builder.SaveProcessName(frameIndex, "test-process-name-4", 100);
            string processName = builder.GetProcessName(frameIndex);
            Assert.Equal("Process test-process-name-1;test-process-name-2;test-process-name-3;test-process-name-4 (100)", processName);
        }

        [Fact]
        public void MultipleWithIgnoredNames()
        {
            StackSourceFrameIndex frameIndex = (StackSourceFrameIndex)0x10;

            LinuxPerfScriptProcessNameBuilder builder = new LinuxPerfScriptProcessNameBuilder();
            builder.SaveProcessName(frameIndex, "test-process-name-1", 100);
            builder.SaveProcessName(frameIndex, ".NET Finalizer", 100);
            builder.SaveProcessName(frameIndex, ".NET Tiered Compilation Worker", 100);
            builder.SaveProcessName(frameIndex, ".NET BGC", 100);
            builder.SaveProcessName(frameIndex, ".NET Server GC", 100);
            builder.SaveProcessName(frameIndex, "test-process-name-2", 100);
            string processName = builder.GetProcessName(frameIndex);
            Assert.Equal("Process test-process-name-1;test-process-name-2 (100)", processName);
        }

        [Fact]
        public void MultipleWithOnlyIgnoredNames()
        {
            StackSourceFrameIndex frameIndex = (StackSourceFrameIndex)0x10;

            LinuxPerfScriptProcessNameBuilder builder = new LinuxPerfScriptProcessNameBuilder();
            builder.SaveProcessName(frameIndex, ".NET Finalizer", 100);
            builder.SaveProcessName(frameIndex, ".NET Tiered Compilation Worker", 100);
            builder.SaveProcessName(frameIndex, ".NET BGC", 100);
            builder.SaveProcessName(frameIndex, ".NET Server GC", 100);
            string processName = builder.GetProcessName(frameIndex);
            Assert.Equal("Process .NET Finalizer;.NET Tiered Compilation Worker;.NET BGC;.NET Server GC (100)", processName);
        }

        [Fact]
        public void MultipleProcesses()
        {
            StackSourceFrameIndex frameIndex1 = (StackSourceFrameIndex)0x10;
            StackSourceFrameIndex frameIndex2 = (StackSourceFrameIndex)0x20;

            LinuxPerfScriptProcessNameBuilder builder = new LinuxPerfScriptProcessNameBuilder();
            builder.SaveProcessName(frameIndex1, "test-process-name", 100);
            builder.SaveProcessName(frameIndex2, "test-process-name-2", 200);
            string processName = builder.GetProcessName(frameIndex1);
            Assert.Equal("Process test-process-name (100)", processName);
            processName = builder.GetProcessName(frameIndex2);
            Assert.Equal("Process test-process-name-2 (200)", processName);
        }
    }
}
