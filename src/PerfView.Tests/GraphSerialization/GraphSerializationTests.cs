using System.Collections.Generic;
using System.IO;
using Xunit;
using System.Linq;

namespace PerfViewTests.GraphSerialization
{
    public class MemorySerializationTests : TestBase
    {
        public static IEnumerable<object[]> TestGCDumpFiles
        {
            get => Directory.EnumerateFiles(TestDataDir, "*.gcdump")
                            .Select(file => new[] { Path.GetFileNameWithoutExtension(file) });
        }

        [Theory]
        [MemberData(nameof(TestGCDumpFiles))]
        public void GenerateBaselineXmlFiles(string gcDumpFileName)
        {
            PrepareTestData();

            GCHeapDump gcDump = new GCHeapDump($"{TestDataDir}\\{gcDumpFileName}.gcdump");

            string xmlFile = $"{OutputDir}\\{gcDumpFileName}.gcdump.xml";
            using (StreamWriter writer = File.CreateText(xmlFile))
                XmlGcHeapDump.WriteGCDumpToXml(gcDump, writer);

            string baselineXmlFile = $"{TestDataDir}\\{gcDumpFileName}_baseline.gcdump.xml";

            Assert.True(AreFilesTheSame(xmlFile, baselineXmlFile));
        }

        /// <summary>
        /// Compares two files to see if they are the same.
        /// </summary>
        /// <remarks>
        /// Copied from https://docs.microsoft.com/en-us/troubleshoot/dotnet/csharp/create-file-compare
        /// </remarks>
        private bool AreFilesTheSame(string file1, string file2)
        {
            int file1byte;
            int file2byte;
            FileStream fs1;
            FileStream fs2;

            if (file1 == file2)
            {
                return true;
            }

            // Open the two files.
            fs1 = new FileStream(file1, FileMode.Open);
            fs2 = new FileStream(file2, FileMode.Open);

            if (fs1.Length != fs2.Length)
            {
                fs1.Close();
                fs2.Close();

                return false;
            }

            do
            {
                file1byte = fs1.ReadByte();
                file2byte = fs2.ReadByte();
            }
            while ((file1byte == file2byte) && (file1byte != -1));

            fs1.Close();
            fs2.Close();

            // Return the success of the comparison. "file1byte" is
            // equal to "file2byte" at this point only if the files are
            // the same.
            return ((file1byte - file2byte) == 0);
        }
    }
}
