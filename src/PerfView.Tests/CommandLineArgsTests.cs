using PerfView;
using PerfViewTests.Utilities;
using System;
using Xunit;
using Xunit.Abstractions;

namespace PerfViewTests
{
    public class CommandLineArgsTests : PerfViewTestBase
    {
        public CommandLineArgsTests(ITestOutputHelper testOutputHelper)
            : base(testOutputHelper)
        {
        }

        [Theory]
        [InlineData("HeapSnapshot", "1234")]
        [InlineData("HeapSnapshotFromProcessDump", "test.dmp")]
        [InlineData("GuiHeapSnapshot", null)]
        [InlineData(null, null)]
        public void DacVerificationDefaultsToTrue(string command, string input)
        {
            var args = ParseHeapCommand(command, input);

            Assert.True(args.VerifyDacOnWindows);
        }

        [Theory]
        [InlineData("HeapSnapshot", "1234", true)]
        [InlineData("HeapSnapshot", "1234", false)]
        [InlineData("HeapSnapshotFromProcessDump", "test.dmp", true)]
        [InlineData("HeapSnapshotFromProcessDump", "test.dmp", false)]
        [InlineData("GuiHeapSnapshot", null, false)]
        [InlineData(null, null, false)]
        public void DacVerificationAcceptsExplicitValue(string command, string input, bool verify)
        {
            var args = ParseHeapCommand(command, input, "/VerifyDacOnWindows:" + verify);

            Assert.Equal(verify, args.VerifyDacOnWindows);
        }

        [Theory]
        [InlineData("HeapSnapshot", "1234")]
        [InlineData("HeapSnapshotFromProcessDump", "test.dmp")]
        public void DacVerificationRejectsInvalidValue(string command, string input)
        {
            var args = new CommandLineArgs();
            args.ParseArgs(new[] { "/VerifyDacOnWindows:invalid", command, input });

            Assert.NotNull(args.CommandLineFailure);
        }

        [Theory]
        [InlineData("HeapSnapshot", "1234", true)]
        [InlineData("HeapSnapshot", "1234", false)]
        [InlineData("HeapSnapshotFromProcessDump", "test.dmp", true)]
        [InlineData("HeapSnapshotFromProcessDump", "test.dmp", false)]
        [InlineData("GuiHeapSnapshot", null, true)]
        [InlineData("GuiHeapSnapshot", null, false)]
        public void DacVerificationSurvivesElevation(string command, string input, bool verify)
        {
            var args = ParseHeapCommand(command, input, "/VerifyDacOnWindows:" + verify);
            string elevatedArgs = CommandProcessor.ParsedArgsAsString(command, args);

            if (verify)
            {
                Assert.DoesNotContain("/VerifyDacOnWindows:false", elevatedArgs);
            }
            else
            {
                Assert.Contains("/VerifyDacOnWindows:false", elevatedArgs);
            }

            var reparsed = new CommandLineArgs();
            reparsed.ParseArgs(elevatedArgs.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
            Assert.Null(reparsed.CommandLineFailure);
            Assert.Equal(verify, reparsed.VerifyDacOnWindows);
        }

        #region private

        private static CommandLineArgs ParseHeapCommand(string command, string input, string qualifier = null)
        {
            var arguments = new System.Collections.Generic.List<string>();
            if (qualifier != null)
            {
                arguments.Add(qualifier);
            }
            if (command != null)
            {
                arguments.Add(command);
            }
            if (input != null)
            {
                arguments.Add(input);
            }

            var args = new CommandLineArgs();
            args.ParseArgs(arguments.ToArray());
            Assert.Null(args.CommandLineFailure);
            return args;
        }

        #endregion
    }
}
