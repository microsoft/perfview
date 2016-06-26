using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing;
using System.Diagnostics;
using System.Text;

namespace TraceEventTests
{
    [TestClass]
    public class GeneralParsing
    {
        static string TestDataDir = @"..\..\inputs";
        static string UnZippedDataDir = @".\unzipped";
        static string OutputDir = @".\output";

        static GeneralParsing()
        {
            UnzipDataFiles();
        }

        private static void UnzipDataFiles()
        {
            Trace.WriteLine(string.Format("Current Directory: {0}", Environment.CurrentDirectory));
            Trace.WriteLine(string.Format("TestDataDir Directory: {0}", Path.GetFullPath(TestDataDir)));
            Trace.WriteLine(string.Format("Unzipped Directory: {0}", Path.GetFullPath(UnZippedDataDir)));
            Trace.WriteLine(string.Format("Output Directory: {0}", Path.GetFullPath(OutputDir)));

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

        /// <summary>
        /// This test simply scans all the events in the ETL.ZIP files in TestDataDir
        /// and scans them (so you should get asserts if there is parsing problem)
        /// and insures that no more than .1% of the events are 
        /// </summary>

        [TestMethod]
        public void GeneralParsingBasic()
        {
            if (Directory.Exists(OutputDir))
                Directory.Delete(OutputDir, true);
            Directory.CreateDirectory(OutputDir);

            bool anyFailure = false;
            foreach (var etlFilePath in Directory.EnumerateFiles(UnZippedDataDir, "*.etl"))
            {
                // *.etl includes *.etlx (don't know why), filter those out.   
                if (!etlFilePath.EndsWith("etl", StringComparison.OrdinalIgnoreCase))
                    continue;

                Trace.WriteLine(string.Format("Processing the file {0}, Making ETLX and scanning.", etlFilePath));
                string eltxFilePath = Path.ChangeExtension(etlFilePath, ".etlx");

                // Used to make tests go faster but may not catch some errors.  
                // It should be off by default 
                var useCachedETLXFile = false;
                TraceLog traceLog;
                if (useCachedETLXFile)
                    traceLog = TraceLog.OpenOrConvert(etlFilePath);
                else
                    traceLog = new TraceLog(TraceLog.CreateFromEventTraceLogFile(etlFilePath));

                // See if we have a cooresponding baseline file 
                string baselineName = Path.Combine(Path.GetFullPath(TestDataDir),
                    Path.GetFileNameWithoutExtension(etlFilePath) + ".baseline.txt");
                string outputName = Path.Combine(OutputDir,
                    Path.GetFileNameWithoutExtension(etlFilePath) + ".txt");
                TextWriter outputFile = File.CreateText(outputName);

                StreamReader baselineFile = null;
                if (File.Exists(baselineName))
                    baselineFile = File.OpenText(baselineName);
                else
                {
                    Trace.WriteLine("WARNING: No baseline file");
                    Trace.WriteLine(string.Format("    ETL FILE: {0}", Path.GetFullPath(etlFilePath)));
                    Trace.WriteLine(string.Format("    NonExistant Baseline File: {0}", baselineName));
                    Trace.WriteLine("To Create a baseline file");
                    Trace.WriteLine(string.Format("    copy /y \"{0}\" \"{1}\"",
                        Path.GetFullPath(outputName),
                        Path.GetFullPath(baselineName)
                        ));
                }

                bool unexpectedUnknownEvent = false;
                int firstFailLineNum = 0;
                int mismatchCount = 0;
                int lineNum = 0;

                var traceSource = traceLog.Events.GetSource();
                traceSource.AllEvents += delegate (TraceEvent data)
                {
                    string parsedEvent = Parse(data);
                    lineNum++;
                    outputFile.WriteLine(parsedEvent);      // Make the new output file.

                    string expectedParsedEvent = null;
                    if (baselineFile != null)
                        expectedParsedEvent = baselineFile.ReadLine();
                    if (expectedParsedEvent == null)
                        expectedParsedEvent = "";

                    if (baselineFile != null && parsedEvent != expectedParsedEvent)
                    {
                        mismatchCount++;
                        if (firstFailLineNum == 0)
                        {
                            firstFailLineNum = lineNum;
                            anyFailure = true;
                            Trace.WriteLine(string.Format("ERROR: File {0}: event not equal to expected on line {1}", etlFilePath, lineNum));
                            Trace.WriteLine(string.Format("   Expected: {0}", parsedEvent));
                            Trace.WriteLine(string.Format("   Actual  : {0}", expectedParsedEvent));

                            Trace.WriteLine("To Compare output and baseline (baseline is SECOND)");
                            Trace.WriteLine(string.Format("    windiff \"{0}\" \"{1}\"",
                                Path.GetFullPath(outputName),
                                Path.GetFullPath(baselineName)
                                ));

                            Trace.WriteLine("To Update baseline file");
                            Trace.WriteLine(string.Format("    copy /y \"{0}\" \"{1}\"",
                                Path.GetFullPath(outputName),
                                Path.GetFullPath(baselineName)
                                ));
                        }
                    }

                    // Event if we don't have a baseline, we can check that the event names are OK.  
                    if (data.EventName.Contains("("))   // Unknown events have () in them 
                    {
                        var eventName = data.ProviderName + "/" + data.EventName;
                        // Some expected events we don't handle today.   
                        if (data.EventName != "EventID(65534)" &&       // Manifest events 
                            data.ProviderName != "Microsoft-Windows-DNS-Client" &&
                            eventName != "KernelTraceControl/ImageID/Opcode(34)" &&
                            eventName != "Windows Kernel/DiskIO/Opcode(16)" && 
                            eventName != "Windows Kernel/SysConfig/Opcode(37)")
                       {
                            Trace.WriteLine(string.Format("ERROR: File {0}: has unknown event {1} at {2:n3} MSec",
                                etlFilePath, eventName, data.TimeStampRelativeMSec));

                            // Assert throws an exception which gets swallowed in Process() so instead
                            // we remember that we failed and assert outside th callback.  
                            unexpectedUnknownEvent = true;
                        }
                    }
                };
                traceSource.Process();
                outputFile.Close();
                if (mismatchCount > 0)
                    Trace.WriteLine(string.Format("ERROR: File {0}: had {1} mismatches", etlFilePath, mismatchCount));

                // If this fires, check the output for the TraceLine just before it for more details.  
                Assert.IsFalse(unexpectedUnknownEvent, "Check trace output for details.");
                Assert.IsTrue(lineNum > 0);     // We had some events.  
            }
            Assert.IsFalse(anyFailure, "Check trace output for details.");
#if !DEBUG
            Assert.Inconclusive("Must run this test in Debug to get most useful results");
#endif
        }


        // Create 1 line that embodies the data in event 'data'

        private static string Parse(TraceEvent data)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append(data.TimeStampRelativeMSec.ToString("n3")).Append(": ");
            sb.Append(data.ProviderName).Append("/").Append(data.EventName).Append(" ");

            sb.Append("PID=").Append(data.ProcessID).Append("; ");
            sb.Append("TID=").Append(data.ThreadID).Append("; ");
            sb.Append("PName=").Append(data.ProcessName).Append("; ");
            sb.Append("ProceNum=").Append(data.ProcessorNumber).Append("; ");
            sb.Append("DataLen=").Append(data.EventDataLength).Append("; ");

            string[] payloadNames = data.PayloadNames;
            for(int i = 0; i < payloadNames.Length; i++)
            {
                // Keep the value size under control and remove newlines.  
                string value = (data.PayloadString(i));
                if (value.Length > 20)
                    value = value.Substring(0, 20) + "...";
                value = value.Replace("\n", "\\n").Replace("\r", "\\r");

                sb.Append(payloadNames[i]).Append('=').Append(value).Append("; ");
            }

            return sb.ToString();
        }
    }
}
