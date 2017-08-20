using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.Diagnostics.Tracing;
using Xunit;
using Xunit.Abstractions;

namespace TraceEventTests
{
    public abstract class EtlTestBase : TestBase
    {
        protected EtlTestBase(ITestOutputHelper output)
            : base(output)
        {
        }

        public static IEnumerable<object[]> TestEtlFiles
        {
            get
            {
                // The test data is contained in files of the same name, but with a .zip extension.
                // Only the names are returned since the extracted files will be in a different directory.
                return from file in Directory.EnumerateFiles(TestDataDir, "*.etl.zip")
                       select new[] { Path.GetFileNameWithoutExtension(file) };
            }
        }

        private static bool s_fileUnzipped;

        [MethodImpl(MethodImplOptions.Synchronized)]
        private static void UnzipDataFiles()
        {
            if (s_fileUnzipped)
                return;

            Trace.WriteLine(string.Format("Current Directory: {0}", Environment.CurrentDirectory));
            Trace.WriteLine(string.Format("TestDataDir Directory: {0}", Path.GetFullPath(TestDataDir)));
            Trace.WriteLine(string.Format("Unzipped Directory: {0}", Path.GetFullPath(UnZippedDataDir)));

            foreach (var dataFile in Directory.EnumerateFiles(TestDataDir, "*.etl.zip"))
            {
                string etlFilePath = Path.Combine(UnZippedDataDir, Path.GetFileNameWithoutExtension(dataFile));
                if (!File.Exists(etlFilePath) || File.GetLastWriteTimeUtc(etlFilePath) < File.GetLastWriteTimeUtc(dataFile))
                {
                    Trace.WriteLine(string.Format("Unzipping File {0} -> {1}", dataFile, etlFilePath));
                    var zipReader = new ZippedETLReader(dataFile);
                    zipReader.SymbolDirectory = Path.Combine(UnZippedDataDir, "Symbols");
                    zipReader.EtlFileName = etlFilePath;
                    zipReader.UnpackArchive();
                }
                else
                    Trace.WriteLine(string.Format("using cached ETL file {0}", etlFilePath));
                Assert.True(File.Exists(etlFilePath));
            }
            Trace.WriteLine("Finished unzipping data");
            s_fileUnzipped = true;
        }

        protected void PrepareTestData()
        {
            Assert.True(Directory.Exists(TestDataDir));
            UnzipDataFiles();
            if (Directory.Exists(OutputDir))
                Directory.Delete(OutputDir, true);

            Directory.CreateDirectory(OutputDir);
            Output.WriteLine(string.Format("OutputDir: {0}", Path.GetFullPath(OutputDir)));
        }
    }
}
