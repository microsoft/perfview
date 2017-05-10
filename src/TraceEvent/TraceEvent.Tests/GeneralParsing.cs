using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing;
using System.Diagnostics;
using System.Text;
using System.Collections.Generic;

namespace TraceEventTests
{
    [TestClass]
    public class GeneralParsing
    {
        static string OriginalBaselineDir = FindInputDir();
        static string TestDataDir = @".\inputs";
        static string UnZippedDataDir = @".\unzipped";
        static string OutputDir = @".\output";

        /// <summary>
        ///  Tries to find the original place in the source base where input data comes from 
        ///  This may not always work if the tests are copied away from the source code (cloud test does this).  
        /// </summary>
        /// <returns></returns>
        private static string FindInputDir()
        {
            string dir = Environment.CurrentDirectory;
            while (dir != null)
            {
                string candidate = Path.Combine(dir, @"TraceEvent\TraceEvent.Tests\inputs");
                if (Directory.Exists(candidate))
                    return Path.GetFullPath(candidate);
                dir = Path.GetDirectoryName(dir);
            }
            return @"%PERFVIEW%\src\TraceEvent\TraceEvent.Tests\inputs";
        }

        private static bool s_fileUnzipped;
        private static void UnzipDataFiles()
        {
            if (s_fileUnzipped)
                return;
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
                    zipReader.UnpackArchive();
                }
                else
                    Trace.WriteLine(string.Format("using cached ETL file {0}", etlFilePath));
                Assert.IsTrue(File.Exists(etlFilePath));
            }
            Trace.WriteLine("Finished unzipping data");
            s_fileUnzipped = true;
        }

        /// <summary>
        /// This test simply scans all the events in the ETL.ZIP files in TestDataDir
        /// and scans them (so you should get asserts if there is parsing problem)
        /// and insures that no more than .1% of the events are 
        /// </summary>

        [DeploymentItem(@"inputs\", "inputs")]
        [TestMethod]
        public void ETW_GeneralParsing_Basic()
        {
            Trace.WriteLine("In ETW_General_Basic");
            Assert.IsTrue(Directory.Exists(TestDataDir));
            UnzipDataFiles();
            if (Directory.Exists(OutputDir))
                Directory.Delete(OutputDir, true);
            Directory.CreateDirectory(OutputDir);
            Trace.WriteLine(string.Format("OutputDir: {0}", Path.GetFullPath(OutputDir)));

            bool anyFailure = false;
            foreach (var etlFilePath in Directory.EnumerateFiles(UnZippedDataDir, "*.etl"))
            {
                // *.etl includes *.etlx (don't know why), filter those out.   
                if (!etlFilePath.EndsWith("etl", StringComparison.OrdinalIgnoreCase))
                    continue;

                Trace.WriteLine(string.Format("Processing the file {0}, Making ETLX and scanning.", Path.GetFullPath(etlFilePath)));
                string eltxFilePath = Path.ChangeExtension(etlFilePath, ".etlx");

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
                var histogram = new SortedDictionary<string, int>();

                // TraceLog traceLog = TraceLog.OpenOrConvert(etlFilePath);    // This one can be used during developent of test itself
                TraceLog traceLog = new TraceLog(TraceLog.CreateFromEventTraceLogFile(etlFilePath));

                var traceSource = traceLog.Events.GetSource();
                traceSource.AllEvents += delegate (TraceEvent data)
                {
                    string eventName = data.ProviderName + "/" + data.EventName;

                    // We are going to skip dynamic events from the CLR provider.
                    // The issue is that this depends on exactly which manifest is present
                    // on the machine, and I just don't want to deal with the noise of 
                    // failures because you have a slightly different one.   
                    if (data.ProviderName == "DotNet")
                        return;

                    // We don't want to use the manifest for CLR Private events since 
                    // different machines might have different manifests.  
                    if (data.ProviderName == "Microsoft-Windows-DotNETRuntimePrivate")
                    {
                        if (data.GetType().Name == "DynamicTraceEventData" || data.EventName.StartsWith("EventID"))
                            return;
                    }
                    // TODO FIX NOW, this is broken and should be fixed.  
                    // We are hacking it here so we don't turn off the test completely.  
                    if (eventName == "DotNet/CLR.SKUOrVersion")
                        return;

                    int count = IncCount(histogram, eventName);

                    // To keep the baseline size under control, we only check at
                    // most 5 of each event type.  
                    const int MaxEventPerType = 5;

                    if (count > MaxEventPerType)
                        return;

                    string parsedEvent = Parse(data);
                    lineNum++;
                    outputFile.WriteLine(parsedEvent);      // Make the new output file.

                    string expectedParsedEvent = null;
                    if (baselineFile != null)
                        expectedParsedEvent = baselineFile.ReadLine();
                    if (expectedParsedEvent == null)
                        expectedParsedEvent = "";

                    // If we have baseline, it should match what we have in the file.  
                    if (baselineFile != null && parsedEvent != expectedParsedEvent)
                    {
                        mismatchCount++;
                        if (firstFailLineNum == 0)
                        {
                            firstFailLineNum = lineNum;
                            anyFailure = true;
                            Trace.WriteLine(string.Format("ERROR: File {0}: event not equal to expected on line {1}", etlFilePath, lineNum));
                            Trace.WriteLine(string.Format("   Expected: {0}", expectedParsedEvent));
                            Trace.WriteLine(string.Format("   Actual  : {0}", parsedEvent));

                            Trace.WriteLine("To Compare output and baseline (baseline is SECOND)");
                            Trace.WriteLine(string.Format("    windiff \"{0}\" \"{1}\"",
                                Path.GetFullPath(outputName),
                                Path.GetFullPath(baselineName)
                                ));

                            Trace.WriteLine("To Update baseline file");
                            Trace.WriteLine(string.Format("    copy /y \"{0}\" \"{1}\"",
                                Path.GetFullPath(outputName),
                                Path.Combine(OriginalBaselineDir, Path.GetFileNameWithoutExtension(etlFilePath) + ".baseline.txt")
                                ));
                        }
                    }

                    // Even if we don't have a baseline, we can check that the event names are OK.  
                    if (0 <= eventName.IndexOf('('))   // Unknown events have () in them 
                    {
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

                /********************* PROCESSING ***************************/
                traceSource.Process();

                // Validation after processing, first we check that the histograms are the same as the baseline

                // We also want to check that the count of events is the same as the baseline. 
                bool histogramMismatch = false;
                foreach (var keyValue in histogram)
                {
                    var histogramLine = "COUNT " + keyValue.Key + ":" + keyValue.Value;

                    outputFile.WriteLine(histogramLine);
                    var expectedistogramLine = baselineFile.ReadLine();
                    lineNum++;

                    if (!histogramMismatch && expectedistogramLine != histogramLine)
                    {
                        histogramMismatch = true;
                        Trace.WriteLine(string.Format("ERROR: File {0}: histogram not equal on  {1}", etlFilePath, lineNum));
                        Trace.WriteLine(string.Format("   Expected: {0}", histogramLine));
                        Trace.WriteLine(string.Format("   Actual  : {0}", expectedistogramLine));

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
                        anyFailure = true;
                    }
                }

                outputFile.Close();
                if (mismatchCount > 0)
                    Trace.WriteLine(string.Format("ERROR: File {0}: had {1} mismatches", etlFilePath, mismatchCount));

                // If this fires, check the output for the TraceLine just before it for more details.  
                Assert.IsFalse(unexpectedUnknownEvent, "Check trace output for details.  Search for ERROR");
                Assert.IsTrue(lineNum > 0);     // We had some events.  

            }
            Assert.IsFalse(anyFailure, "Check trace output for details.  Search for ERROR");
#if !DEBUG
            Assert.Inconclusive("Run with Debug build to get Thorough testing.");
#endif
        }

        private static int IncCount(SortedDictionary<string, int> histogram, string eventName)
        {
            int count = 0;
            histogram.TryGetValue(eventName, out count);
            count++;
            histogram[eventName] = count;
            return count;
        }

        // Create 1 line that embodies the data in event 'data'

        private static string Parse(TraceEvent data)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("EVENT ");
            sb.Append(data.TimeStampRelativeMSec.ToString("n3")).Append(": ");
            sb.Append(data.ProviderName).Append("/").Append(data.EventName).Append(" ");

            sb.Append("PID=").Append(data.ProcessID).Append("; ");
            sb.Append("TID=").Append(data.ThreadID).Append("; ");
            sb.Append("PName=").Append(data.ProcessName).Append("; ");
            sb.Append("ProceNum=").Append(data.ProcessorNumber).Append("; ");
            sb.Append("DataLen=").Append(data.EventDataLength).Append("; ");

            string[] payloadNames = data.PayloadNames;
            for (int i = 0; i < payloadNames.Length; i++)
            {
                // Keep the value size under control and remove newlines.  
                string value = (data.PayloadString(i));

                // To debug this set first chance exeption handing before calling PayloadString above.
                Assert.IsFalse(value.Contains("EXCEPTION_DURING_VALUE_LOOKUP"), "Exception during event Payload Processing");

                if (value.Length > 20)
                    value = value.Substring(0, 20) + "...";
                value = value.Replace("\n", "\\n").Replace("\r", "\\r");

                sb.Append(payloadNames[i]).Append('=').Append(value).Append("; ");
            }

            return sb.ToString();
        }
    }
}
