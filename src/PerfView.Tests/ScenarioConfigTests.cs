using Diagnostics.Tracing.StackSources;
using Microsoft.Diagnostics.Tracing.Stacks;
using PerfView;
using PerfViewExtensibility;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;
using Xunit;

namespace PerfViewTests
{
    public class ScenarioConfigTests : IDisposable
    {
        public ScenarioConfigTests()
        {
            m_testDirectory = Path.Combine(Path.GetTempPath(), "ScenarioConfigTests", Guid.NewGuid().ToString("N"));
            m_traceDirectory = Path.Combine(m_testDirectory, "traces");
            Directory.CreateDirectory(m_traceDirectory);
            File.WriteAllText(Path.Combine(m_traceDirectory, "sample.etl"), "");
            m_previousCommandLineArgs = App.CommandLineArgs;
            App.CommandLineArgs = new CommandLineArgs();
        }

        [Theory]
        [InlineData(null, "sample", "*.perfView.xml.zip")]
        [InlineData("report", "report", "report.perfView.xml.zip")]
        [InlineData("$1-copy", "sample-copy", "*-copy.perfView.xml.zip")]
        [InlineData("$&-copy", "sample.etl-copy", "*-copy.perfView.xml.zip")]
        [InlineData("$+-copy", "sample-copy", "*-copy.perfView.xml.zip")]
        [InlineData("$$1-copy", "$1-copy", "$1-copy.perfView.xml.zip")]
        [InlineData("report with spaces.v2", "report with spaces.v2", "report with spaces.v2.perfView.xml.zip")]
        public void OutputFileNamePreservesValidNamesAndSubstitutions(string outputPattern, string outputName, string outputWildcard)
        {
            var config = Deserialize(CreateConfig("*.etl", outputPattern), m_traceDirectory, out string scenarioSet);

            var entry = Assert.Single(config);
            Assert.Equal(Path.Combine(m_traceDirectory, outputName + ".perfView.xml.zip"), entry.Key);
            Assert.Equal(Path.Combine(m_traceDirectory, "sample.etl"), entry.Value.InputFile);
            Assert.Equal("*", entry.Value.ProcessFilter);
            Assert.Equal(double.NegativeInfinity, entry.Value.StartTime);
            Assert.Equal(double.PositiveInfinity, entry.Value.EndTime);

            var document = new XmlDocument();
            document.LoadXml(scenarioSet);
            Assert.Equal(outputWildcard, document.SelectSingleNode("/ScenarioSet/Scenarios/@files").Value);
        }

        [Theory]
        [InlineData(null, "sample")]
        [InlineData("$1-copy", "sample-copy")]
        public void OutputFileNamePreservesZippedTraceNames(string outputPattern, string outputName)
        {
            string traceFile = Path.Combine(m_traceDirectory, "sample.etl.zip");
            File.WriteAllText(traceFile, "");

            var config = Deserialize(CreateConfig("*.etl.zip", outputPattern), m_traceDirectory, out _);

            var entry = Assert.Single(config);
            Assert.Equal(Path.Combine(m_traceDirectory, outputName + ".perfView.xml.zip"), entry.Key);
            Assert.Equal(traceFile, entry.Value.InputFile);
        }

        [Theory]
        [InlineData(null, "sample")]
        [InlineData("report", "report")]
        public void OutputFileNameUsesMatchedTraceDirectoryNotConfigDirectory(string outputPattern, string outputName)
        {
            string configDirectory = Path.Combine(m_testDirectory, "configs");
            Directory.CreateDirectory(configDirectory);

            var config = Deserialize(CreateConfig(@"..\traces\sample.etl", outputPattern), configDirectory, out _);

            Assert.Equal(Path.GetFullPath(Path.Combine(m_traceDirectory, outputName + ".perfView.xml.zip")),
                Path.GetFullPath(Assert.Single(config).Key));
        }

        [Fact]
        public void ValidOutputCreatesReadableStackArchiveBesideMatchedTrace()
        {
            var config = Deserialize(CreateConfig("*.etl", "$1-copy"), m_traceDirectory, out _);
            string destination = Assert.Single(config).Key;
            var source = new InternStackSource();
            var frame = source.Interner.FrameIntern("TestFrame");
            var stack = source.Interner.CallStackIntern(frame, StackSourceCallStackIndex.Invalid);
            source.AddSample(new StackSourceSample(source) { StackIndex = stack, Metric = 1, TimeRelativeMSec = 2 });
            source.Interner.DoneInterning();

            XmlStackSourceWriter.WriteStackViewAsZippedXml(source, destination);

            Assert.Equal(Path.Combine(m_traceDirectory, "sample-copy.perfView.xml.zip"), destination);
            Assert.True(File.Exists(destination));
            var savedSource = new XmlStackSource(destination);
            Assert.Equal(1, savedSource.SampleIndexLimit);
            savedSource.ForEach(sample =>
            {
                Assert.Equal(1, sample.Metric);
                Assert.Equal(2, sample.TimeRelativeMSec);
                Assert.Equal("TestFrame", savedSource.GetFrameName(savedSource.GetFrameIndex(sample.StackIndex), true));
            });
        }

        [Theory]
        [InlineData(@"..\outside\owned")]
        [InlineData("../outside/owned")]
        [InlineData(@"..\traces-sibling\owned")]
        [InlineData(@"subdirectory\report")]
        [InlineData("subdirectory/report")]
        [InlineData(@"C:\outside\owned")]
        [InlineData(@"C:owned")]
        [InlineData(@"\outside\owned")]
        [InlineData("/outside/owned")]
        [InlineData(@"\\server\share\owned")]
        [InlineData("//server/share/owned")]
        [InlineData(@"\\?\C:\outside\owned")]
        [InlineData(@"\\.\C:\outside\owned")]
        [InlineData(@"\\?\UNC\server\share\owned")]
        [InlineData(@"\??\C:\outside\owned")]
        [InlineData("report:stream")]
        [InlineData("")]
        [InlineData(".")]
        [InlineData("..")]
        [InlineData("report*")]
        [InlineData("report?")]
        [InlineData("report|name")]
        [InlineData("report<name")]
        [InlineData("report\"name")]
        [InlineData("report\tname")]
        [InlineData("NUL")]
        [InlineData("CON.report")]
        public void OutputFileNameRejectsPathsAndUnsafeNames(string outputPattern)
        {
            var exception = Assert.Throws<ApplicationException>(() =>
                Deserialize(CreateConfig("*.etl", outputPattern), m_traceDirectory, out _));

            Assert.Contains("output", exception.Message);
            Assert.Contains("file name", exception.Message);
        }

        [Theory]
        [InlineData("$_")]
        [InlineData("$`report")]
        [InlineData("$1/../owned")]
        [InlineData("$1\\..\\owned")]
        [InlineData("$'")]
        public void OutputFileNameIsValidatedAfterSubstitution(string outputPattern)
        {
            var exception = Assert.Throws<ApplicationException>(() =>
                Deserialize(CreateConfig("*.etl", outputPattern), m_traceDirectory, out _));

            Assert.Contains("output", exception.Message);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void InvalidOutputRejectsEntireConfigWithoutChangingFiles(bool absoluteOutput)
        {
            string outsideDirectory = Path.Combine(m_testDirectory, "traces-sibling");
            Directory.CreateDirectory(outsideDirectory);
            string sentinel = Path.Combine(outsideDirectory, "owned.perfView.xml.zip");
            File.WriteAllText(sentinel, "outside sentinel");
            string outputPattern = absoluteOutput
                ? Path.Combine(outsideDirectory, "owned")
                : @"..\traces-sibling\owned";
            string xml = CreateConfig("*.etl", "valid", outputPattern);
            string configFile = Path.Combine(m_traceDirectory, "scenario.xml");
            File.WriteAllText(configFile, xml);
            File.SetLastWriteTimeUtc(sentinel, new DateTime(2000, 1, 1));
            var previousCommandProcessor = App.CommandProcessor;

            try
            {
                App.CommandProcessor = new CommandProcessor { LogFile = TextWriter.Null };
                var exception = Assert.Throws<ApplicationException>(() => new Commands().SaveScenarioCPUStacks(configFile));
                Assert.Contains("output", exception.Message);
                Assert.Contains("file name", exception.Message);
            }
            finally
            {
                App.CommandProcessor = previousCommandProcessor;
            }

            Assert.Equal("outside sentinel", File.ReadAllText(sentinel));
            Assert.False(File.Exists(Path.Combine(m_traceDirectory, "valid.perfView.xml.zip")));
            Assert.False(File.Exists(Path.ChangeExtension(configFile, ".scenarioSet.xml")));
            Assert.Equal(2, Directory.GetFiles(m_traceDirectory).Length);
        }

        public void Dispose()
        {
            App.CommandLineArgs = m_previousCommandLineArgs;
            Directory.Delete(m_testDirectory, recursive: true);
        }

        #region private
        private static string CreateConfig(string filePattern, params string[] outputPatterns)
        {
            var builder = new StringBuilder();
            using (var writer = XmlWriter.Create(builder, new XmlWriterSettings { OmitXmlDeclaration = true }))
            {
                writer.WriteStartElement("ScenarioConfig");
                foreach (string outputPattern in outputPatterns)
                {
                    writer.WriteStartElement("Scenarios");
                    writer.WriteAttributeString("files", filePattern);
                    writer.WriteAttributeString("process", "*");
                    if (outputPattern != null)
                    {
                        writer.WriteAttributeString("output", outputPattern);
                    }
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
            return builder.ToString();
        }

        private static Dictionary<string, Commands.ScenarioConfig> Deserialize(string xml, string baseDirectory, out string scenarioSet)
        {
            var builder = new StringBuilder();
            Dictionary<string, Commands.ScenarioConfig> config;
            using (var reader = XmlReader.Create(new StringReader(xml)))
            using (var writer = XmlWriter.Create(builder))
            {
                config = new Commands().DeserializeScenarioConfig(reader, writer, TextWriter.Null, baseDirectory);
            }
            scenarioSet = builder.ToString();
            return config;
        }

        private readonly string m_testDirectory;
        private readonly string m_traceDirectory;
        private readonly CommandLineArgs m_previousCommandLineArgs;
        #endregion
    }
}
