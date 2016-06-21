using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing;
using System.Diagnostics;

namespace TraceEventTests
{
    [TestClass]
    public class GeneralParsing
    {
        static string TestDataDir = @"..\..\inputs";
        static string UnZippedDataDir = @"\unzipped";
        static string OutputDir = @".\output";


        static  GeneralParsing()
        {
            UnzipDataFiles();
        }

        private static void UnzipDataFiles()
        {
            foreach (var dataFile in Directory.EnumerateFiles(TestDataDir, "*.etl.zip"))
            {
                string etlFilePath = Path.Combine(UnZippedDataDir, Path.GetFileNameWithoutExtension(dataFile));
                if (!File.Exists(etlFilePath) || File.GetLastWriteTimeUtc(etlFilePath) < File.GetLastWriteTimeUtc(dataFile))
                {
                    Trace.WriteLine(string.Format("Unzipping File {0} -> {1}", dataFile, etlFilePath));
                    var zipReader = new ZippedETLReader(dataFile);
                    zipReader.SymbolDirectory = Path.Combine(UnZippedDataDir, "Symbols");
                    zipReader.EtlFileName = etlFilePath;
                    zipReader.UnpackAchive();
                }
                else
                    Trace.WriteLine(string.Format("using cached ETL file {0}", etlFilePath));
                Assert.IsTrue(File.Exists(etlFilePath));
            }
            Trace.WriteLine("Finished unzipping data");
        }


        [TestMethod]
        public void TestForAssertsInParsing()
        {
#if DEBUG
            if (Directory.Exists(OutputDir))
                Directory.Delete(OutputDir, true);

            foreach (var etlFilePath in Directory.EnumerateFiles(UnZippedDataDir, "*.etl"))
            {
                Trace.WriteLine(string.Format("Converting file {0} to ETLX", etlFilePath));
                var traceLog = TraceLog.OpenOrConvert(etlFilePath);
                var traceSource = traceLog.Events.GetSource();

                int eventCount = 0;
                int unknownCount = 0;
                traceSource.AllEvents += delegate (TraceEvent data)
                {
                    eventCount++;
                    if (data.EventName.Contains("("))
                        unknownCount++;
                };
                traceSource.Process();
                Assert.IsTrue(eventCount > 0);

                // No more than 0.1% unknown events
                Assert.IsTrue(unknownCount * 1000 < eventCount);

                Trace.WriteLine(string.Format("File {0}: has {1} events with {2} unknown events",
                    etlFilePath, eventCount, unknownCount));
            }
#else
            Assert.Inconclusive("Must run this test in Debug to get a useful result");
#endif
        }
    }
}
