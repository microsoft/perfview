namespace PerfView.TestUtilities
{
    using System;
    using System.Diagnostics;
    using System.Runtime.InteropServices;
    using Xunit;

    /// <summary>
    /// Prevents tests that intentionally trigger process-wide assertions from running beside other tests.
    /// </summary>
    [CollectionDefinition("Debug assertion tests", DisableParallelization = true)]
    public sealed class DebugAssertionTestCollection
    {
    }

    /// <summary>
    /// This class verifies the behavior of <see cref="Debug.Assert(bool)"/> and <see cref="Debug.Fail(string)"/> when
    /// called during unit testing.
    /// </summary>
    /// <remarks>
    /// <para>This file can be linked into any project which needs to validate that assertions are behaving correctly
    /// for the purpose of unit testing.</para>
    /// <para>On .NET Framework, assertion failures throw via <see cref="ThrowingTraceListener"/> registered in
    /// app.config. On .NET 5+, the DefaultTraceListener already throws on assertion failures, so no
    /// additional listener configuration is needed.</para>
    /// </remarks>
    [Collection("Debug assertion tests")]
    public class DebugAssertionTests
    {
#if DEBUG
        [Fact]
        public void TestDebugAssertThrowsException()
        {
            DebugAssertionTestConfiguration.AssertValid();
            Debug.Assert(true);

            Assert.ThrowsAny<Exception>(() => Debug.Assert(false));
        }

        [Fact]
        public void TestDebugFailThrowsException()
        {
            DebugAssertionTestConfiguration.AssertValid();
            Assert.ThrowsAny<Exception>(() => Debug.Fail("Bad things"));
        }
#endif

        [Fact]
        public void TestTraceAssertThrowsException()
        {
            DebugAssertionTestConfiguration.AssertValid();
            Assert.ThrowsAny<Exception>(() => Trace.Assert(false));
        }

        [Fact]
        public void TestTraceFailThrowsException()
        {
            DebugAssertionTestConfiguration.AssertValid();
            Assert.ThrowsAny<Exception>(() => Trace.Fail("Bad things"));
        }
    }

    internal static class DebugAssertionTestConfiguration
    {
        /// <summary>
        /// Verifies the runtime-specific configuration used to ensure assertion failures throw instead of
        /// displaying interactive UI or being ignored.
        /// </summary>
        /// <remarks>
        /// <para>On .NET Framework, each test assembly's app.config removes the DefaultTraceListener and
        /// registers ThrowingTraceListener, which converts assertion failures into xUnit failures.</para>
        /// <para>Modern .NET does not process the app.config diagnostics section, but its built-in assertion
        /// behavior already throws. The assertion tests verify that behavior directly, so there is no listener
        /// configuration to validate on that runtime.</para>
        /// </remarks>
        internal static void AssertValid()
        {
            if (!RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Only inspect the existing process-wide configuration. Silently repairing Trace.Listeners here
            // would change the behavior under test and could race with concurrent access from other tests.
            string configurationFile = AppDomain.CurrentDomain.GetData("APP_CONFIG_FILE") as string;
            bool foundThrowingTraceListener = false;
            foreach (TraceListener listener in Trace.Listeners)
            {
                Assert.False(
                    listener is DefaultTraceListener,
                    $"DefaultTraceListener can display an assertion dialog and hang the test run. Configuration file: {configurationFile}");
                foundThrowingTraceListener |= listener is ThrowingTraceListener;
            }

            Assert.True(
                foundThrowingTraceListener,
                $"ThrowingTraceListener must be registered by the test assembly's app.config. Configuration file: {configurationFile}");
        }
    }

}
