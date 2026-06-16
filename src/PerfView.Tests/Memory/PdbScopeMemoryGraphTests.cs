using PerfView;
using System;
using System.IO;
using Xunit;

namespace PerfViewTests.Memory
{
    public class PdbScopeMemoryGraphTests : IDisposable
    {
        private readonly string m_testDirectory;

        public PdbScopeMemoryGraphTests()
        {
            m_testDirectory = Path.Combine(Path.GetTempPath(), "PdbScopeMemoryGraphTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_testDirectory);
        }

        public void Dispose()
        {
            if (Directory.Exists(m_testDirectory))
            {
                Directory.Delete(m_testDirectory, recursive: true);
            }
        }

        [Theory]
        [InlineData(@"\\server\share\module.dll")]
        [InlineData(@"\\?\UNC\server\share\module.dll")]
        [InlineData("https://server/share/module.dll")]
        public void TryResolveTrustedFilePathRejectsRemotePaths(string modulePath)
        {
            string pdbScopeFilePath = GetPdbScopeFilePath();

            Assert.False(PdbScopeMemoryGraph.TryResolveTrustedFilePath(modulePath, pdbScopeFilePath, out string trustedFilePath));
            Assert.Null(trustedFilePath);
        }

        [Fact]
        public void TryResolveTrustedFilePathRejectsRootedPathsOutsidePdbScopeDirectory()
        {
            string pdbScopeFilePath = GetPdbScopeFilePath();
            string outsideDirectory = Path.Combine(Path.GetPathRoot(m_testDirectory), "PdbScopeMemoryGraphTestsOutside");
            string outsideModulePath = Path.Combine(outsideDirectory, "module.dll");

            Assert.False(PdbScopeMemoryGraph.TryResolveTrustedFilePath(outsideModulePath, pdbScopeFilePath, out string trustedFilePath));
            Assert.Null(trustedFilePath);
        }

        [Theory]
        [InlineData("module.dll")]
        [InlineData(@"subdirectory\module.dll")]
        public void TryResolveTrustedFilePathAllowsRelativePathsUnderPdbScopeDirectory(string modulePath)
        {
            string pdbScopeFilePath = GetPdbScopeFilePath();

            Assert.True(PdbScopeMemoryGraph.TryResolveTrustedFilePath(modulePath, pdbScopeFilePath, out string trustedFilePath));
            Assert.Equal(Path.GetFullPath(Path.Combine(m_testDirectory, modulePath)), trustedFilePath);
        }

        [Fact]
        public void TryResolveTrustedFilePathAllowsRootedPathsUnderPdbScopeDirectory()
        {
            string pdbScopeFilePath = GetPdbScopeFilePath();
            string modulePath = Path.Combine(m_testDirectory, "module.dll");

            Assert.True(PdbScopeMemoryGraph.TryResolveTrustedFilePath(modulePath, pdbScopeFilePath, out string trustedFilePath));
            Assert.Equal(modulePath, trustedFilePath);
        }

        [Theory]
        [InlineData(@"..\module.dll")]
        [InlineData(@"subdirectory\..\..\module.dll")]
        public void TryResolveTrustedFilePathRejectsRelativePathsEscapingPdbScopeDirectory(string modulePath)
        {
            string pdbScopeFilePath = GetPdbScopeFilePath();

            Assert.False(PdbScopeMemoryGraph.TryResolveTrustedFilePath(modulePath, pdbScopeFilePath, out string trustedFilePath));
            Assert.Null(trustedFilePath);
        }

        [Fact]
        public void PdbScopeXmlWithRemoteModuleFilePathDoesNotThrow()
        {
            CommandProcessor originalCommandProcessor = App.CommandProcessor;
            string pdbScopeFilePath = GetPdbScopeFilePath();
            try
            {
                App.CommandProcessor = new CommandProcessor() { LogFile = TextWriter.Null };
                File.WriteAllText(
                    pdbScopeFilePath,
                    @"<PdbscopeReport><Section Start=""8192"" Size=""16"" Name="".text"" /><Module Base=""4096"" FilePath=""\\server\share\module.dll"" /><Symbol addr=""2000"" size=""16"" name=""Main"" tag=""code"" /></PdbscopeReport>");

                new PdbScopeMemoryGraph(pdbScopeFilePath);
            }
            finally
            {
                App.CommandProcessor = originalCommandProcessor;
            }
        }

        private string GetPdbScopeFilePath()
        {
            return Path.Combine(m_testDirectory, "test.imageSize.xml");
        }
    }
}
