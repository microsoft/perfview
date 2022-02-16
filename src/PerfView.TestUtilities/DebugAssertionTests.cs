namespace PerfView.TestUtilities
{
    using System;
    using System.Diagnostics;
    using Xunit;

    /// <summary>
    /// This class verifies the behavior of <see cref="Debug.Assert(bool)"/> and <see cref="Debug.Fail(string)"/> when
    /// called during unit testing.
    /// </summary>
    /// <remarks>
    /// <para>This file can be linked into any project which needs to validate that assertions are behaving correctly
    /// for the purpose of unit testing.</para>
    /// </remarks>
    public class DebugAssertionTests
    {
#if DEBUG
        [Fact(Skip = "https://github.com/microsoft/perfview/issues/1571")]
        public void TestDebugAssertThrowsException()
        {
            Debug.Assert(true);

            Assert.ThrowsAny<Exception>(() => Debug.Assert(false));
        }

        [Fact(Skip = "https://github.com/microsoft/perfview/issues/1571")]
        public void TestDebugFailThrowsException()
        {
            Assert.ThrowsAny<Exception>(() => Debug.Fail("Bad things"));
        }
#endif

        [Fact(Skip = "https://github.com/microsoft/perfview/issues/1571")]
        public void TestTraceAssertThrowsException()
        {
            Assert.ThrowsAny<Exception>(() => Trace.Assert(false));
        }

        [Fact(Skip = "https://github.com/microsoft/perfview/issues/1571")]
        public void TestTraceFailThrowsException()
        {
            Assert.ThrowsAny<Exception>(() => Trace.Fail("Bad things"));
        }
    }
}
