// Copyright (c) Microsoft Corporation.  All rights reserved

using System;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Analysis;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Stats
{
    internal static class RuntimeLoaderStats
    {
        public static void ToHtml(TextWriter writer, TraceProcess stats, string fileName, RuntimeLoaderStatsData runtimeOps)
        {
            var usersGuideFile = ClrStatsUsersGuide.WriteUsersGuide(fileName);

            writer.WriteLine("<H3><A Name=\"Stats_{0}\"><font color=\"blue\">Runtime Operation Stats for for Process {1,5}: {2}</font><A></H3>", stats.ProcessID, stats.ProcessID, stats.Name);
            writer.WriteLine("<UL>");
            {
                if (!string.IsNullOrEmpty(stats.CommandLine))
                {
                    writer.WriteLine("<LI>CommandLine: {0}</LI>", stats.CommandLine);
                }

                writer.WriteLine("<LI>Process CPU Time: {0:n0} msec</LI>", stats.CPUMSec);
                writer.WriteLine("<LI>Guidance on data:");
                writer.WriteLine("<UL>");
                {
                    writer.WriteLine("<LI> <A HREF=\"{0}#UnderstandingRuntimeLoader\">Runtime Loader Perf Users Guide</A></LI>", usersGuideFile);
                }
                writer.WriteLine("</UL>");
                writer.WriteLine("</LI>");

                writer.WriteLine("<LI>Raw data:");
                writer.WriteLine("<UL>");
                {
                    writer.WriteLine($@"
                    <form action=""command:txt/{stats.ProcessID},{stats.StartTimeRelativeMsec}"">
                      <input type=""checkbox"" checked=""yes"" id=""TreeView"" name=""TreeView"" value=""true"">
                      <label for=""TreeView"">Show data as a tree</label>
                      <input type=""checkbox"" checked=""yes"" id=""JIT"" name=""JIT"" value=""true"">
                      <label for=""JIT"">Show JIT data</label>
                      <input type=""checkbox"" checked=""yes"" id=""R2R_Found"" name=""R2R_Found"" value=""true"">
                      <label for=""R2R_Found"">Show R2R found data</label>
                      <input type=""checkbox"" id=""R2R_Failed"" name=""R2R_Failed"" value=""true"">
                      <label for=""R2R_Failed"">Show R2R not found data</label>
                      <input type=""checkbox"" checked=""yes"" id=""TypeLoad"" name=""TypeLoad"" value=""true"">
                      <label for=""TypeLoad"">Show TypeLoad data</label>
                      <input type=""checkbox"" checked=""yes"" id=""AssemblyLoad"" name=""AssemblyLoad"" value=""true"">
                      <label for=""AssemblyLoad"">Show AssemblyLoad data</label>
                      <input type=""submit"" value=""Show as Text"">
                    </form>
                    <form action=""command:csv/{stats.ProcessID},{stats.StartTimeRelativeMsec}"">
                      <input type=""checkbox"" checked=""yes"" id=""TreeView"" name=""TreeView"" value=""true"">
                      <label for=""TreeView"">Show data as a tree</label>
                      <input type=""checkbox"" checked=""yes"" id=""JIT"" name=""JIT"" value=""true"">
                      <label for=""JIT"">Show JIT data</label>
                      <input type=""checkbox"" checked=""yes"" id=""R2R_Found"" name=""R2R_Found"" value=""true"">
                      <label for=""R2R_Found"">Show R2R found data</label>
                      <input type=""checkbox"" id=""R2R_Failed"" name=""R2R_Failed"" value=""true"">
                      <label for=""R2R_Failed"">Show R2R not found data</label>
                      <input type=""checkbox"" checked=""yes"" id=""TypeLoad"" name=""TypeLoad"" value=""true"">
                      <label for=""TypeLoad"">Show TypeLoad data</label>
                      <input type=""checkbox"" checked=""yes"" id=""AssemblyLoad"" name=""AssemblyLoad"" value=""true"">
                      <label for=""AssemblyLoad"">Show AssemblyLoad data</label>
                      <input type=""submit"" value=""Show in Excel"">
                    </form>
                    ");
                }
                writer.WriteLine("</UL>");
                writer.WriteLine("</LI>");
            }
            writer.WriteLine("</UL>");
        }

        public static void ToTxt(string filePath, RuntimeLoaderProcessData runtimeProcessData, string[] filters, bool tree)
        {
            bool csv = filePath.EndsWith("csv");
            using (var writer = File.CreateText(filePath))
            {
                bool showExclusiveTime = tree;

                writer.WriteLine($"\"ThreadId  \",\"Start time\",\"Inclusive\"{(showExclusiveTime ? ",\"Exclusive\"" : "") },\"RuntimeOperation\"");
                foreach (var threadData in runtimeProcessData.ThreadData)
                {
                    int threadId = threadData.Key;
                    HashSet<EventIndex> seenEvents = new HashSet<EventIndex>();

                    IEnumerable<CLRRuntimeActivityComputer.StartStopThreadEventData> dataToProcess = threadData.Value.Data;

                    if (filters != null)
                        dataToProcess = CLRRuntimeActivityComputer.PerThreadStartStopData.FilterData(filters, dataToProcess);

                    if (tree)
                        dataToProcess = CLRRuntimeActivityComputer.PerThreadStartStopData.Stackify(dataToProcess);

                    var perThreadData = new List<CLRRuntimeActivityComputer.StartStopThreadEventData>(dataToProcess);

                    for (int i = 0; i < perThreadData.Count; i++)
                    {
                        var eventData = perThreadData[i];
                        double startTime = eventData.Start.Time;
                        double endTime = eventData.End.Time;
                        double inclusiveTime = endTime - startTime;
                        double exclusiveTime = inclusiveTime;
                        string inclusiveTimeStr = inclusiveTime.ToString("F3");

                        if (perThreadData.Count > (i + 1))
                        {
                            double startOfNextItem = perThreadData[i + 1].Start.Time;
                            if (startOfNextItem < endTime)
                            {
                                exclusiveTime = startOfNextItem - startTime;
                            }
                        }

                        if (seenEvents.Contains(eventData.End.EventId))
                            inclusiveTimeStr = "";

                        writer.Write($"{PadIfNotCsv(threadId.ToString(), 12)},{PadIfNotCsv(startTime.ToString("F3"), 12)},{PadIfNotCsv(inclusiveTimeStr, 11)},");
                        if (showExclusiveTime)
                            writer.Write($"{PadIfNotCsv(exclusiveTime.ToString("F3"), 11)},");

                        StringBuilder eventName = new StringBuilder();

                        int stackDepth = eventData.StackDepth;
                        for (int iStackDepth = 0; iStackDepth < stackDepth; iStackDepth++)
                            eventName.Append(" |");

                        if (seenEvents.Contains(eventData.End.EventId))
                            eventName.Append(" +");
                        else
                            eventName.Append("--");

                        eventName.Append(eventData.Name);
                        writer.WriteLine($"{QuoteIfCsv(eventName.ToString())}");
                        seenEvents.Add(eventData.End.EventId);
                    }
                }
            }

            return;

            string PadIfNotCsv(string str, int pad)
            {
                if (!csv)
                {
                    return str.PadLeft(pad);
                }
                return str;
            }

            string QuoteIfCsv(string str)
            {
                if (csv)
                {
                    return $"\"'{str}\"";
                }
                else
                {
                    return str;
                }
            }
        }

        public static double TotalCPUMSec(TraceProcess incompleteStatsProc, RuntimeLoaderStatsData runtimeOps)
        {
            var runtimeProcessData = runtimeOps.GetProcessDataFromAnalysisProcess(incompleteStatsProc);

            double cpuTime = 0;
            foreach (var threadData in runtimeProcessData.ThreadData.Values)
            {
                double lastThreadTimeSeen = double.MinValue;
                foreach (var eventData in threadData.Data)
                {
                    if (lastThreadTimeSeen >= eventData.End.Time)
                        continue;

                    lastThreadTimeSeen = eventData.End.Time;
                    cpuTime += eventData.End.Time - eventData.Start.Time;
                }
            }

            return cpuTime;
        }

        public static bool IsInteresting(TraceProcess proc, RuntimeLoaderStatsData runtimeOps)
        {
            var runtimeProcessData = runtimeOps.GetProcessDataFromAnalysisProcess(proc);
            return (runtimeProcessData.ThreadData.Count > 0);
        }
    }
}
