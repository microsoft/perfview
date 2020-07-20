// Copyright (c) Microsoft Corporation.  All rights reserved

using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Analysis.JIT;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Parsers.ClrPrivate;
using Microsoft.Diagnostics.Tracing.Stacks;
using Microsoft.Diagnostics.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Stats
{
    internal static class RuntimeLoaderStats
    {
        public static void ToHtml(TextWriter writer, Microsoft.Diagnostics.Tracing.Analysis.TraceProcess incompleteStatsProc, string fileName, Microsoft.Diagnostics.Tracing.RuntimeLoaderStats runtimeOps)
        {
            TraceProcess stats = null;

            foreach (var proc in runtimeOps.EventSource.TraceLog.Processes)
            {
                if (proc.ProcessID == incompleteStatsProc.ProcessID)
                {
                    stats = proc;
                }
            }

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
                    <form action=""command:txt/{stats.ProcessID}"">
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
                      <input type=""submit"" value=""Submit"">
                    </form>
                    ");
                }
                writer.WriteLine("</UL>");
                writer.WriteLine("</LI>");
            }
            writer.WriteLine("</UL>");
        }

        public static void ToTxt(string filePath, TraceProcess process, Microsoft.Diagnostics.Tracing.RuntimeLoaderStats runtimeOps, string[] filters, bool tree)
        {
            using (var writer = File.CreateText(filePath))
            {
                writer.WriteLine($"Process {process.Name}");
                writer.WriteLine($"========");

                foreach (var thread in process.Threads)
                {
                    int threadId = thread.ThreadID;
                    if (runtimeOps.ContainsKey(threadId))
                    {
                        writer.WriteLine($"Thread {threadId}");
                        writer.WriteLine($"========");
                        writer.WriteLine("Start time  ~Inclusive~Exclusive~RuntimeOperation");

                        HashSet<EventIndex> seenEvents = new HashSet<EventIndex>();

                        IEnumerable<CLRRuntimeActivityComputer.StartStopThreadEventData> dataToProcess = runtimeOps[threadId].Data;

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


                            writer.Write($"{startTime.ToString("F3").PadLeft(12)}~{inclusiveTimeStr.PadLeft(9)}~{exclusiveTime.ToString("F3").PadLeft(9)}~");
                            int stackDepth = eventData.StackDepth;
                            for (int iStackDepth = 0; iStackDepth < stackDepth; iStackDepth++)
                                writer.Write(" |");

                            if (seenEvents.Contains(eventData.End.EventId))
                                writer.Write(" +");
                            else
                                writer.Write("--");

                            writer.WriteLine(eventData.Name, false);
                            seenEvents.Add(eventData.End.EventId);
                        }
                    }
                }
            }
        }

        public static double TotalCPUMSec(Microsoft.Diagnostics.Tracing.Analysis.TraceProcess incompleteStatsProc, Microsoft.Diagnostics.Tracing.RuntimeLoaderStats runtimeOps)
        {
            TraceProcess process = null;

            foreach (var proc in runtimeOps.EventSource.TraceLog.Processes)
            {
                if (proc.ProcessID == incompleteStatsProc.ProcessID)
                {
                    process = proc;
                }
            }

            if (process == null)
                return 0;

            double cpuTime = 0;
            foreach (var thread in process.Threads)
            {
                int threadId = thread.ThreadID;
                double lastThreadTimeSeen = double.MinValue;
                if (runtimeOps.ContainsKey(threadId))
                {
                    foreach (var eventData in runtimeOps[threadId].Data)
                    {
                        if (lastThreadTimeSeen >= eventData.End.Time)
                            continue;

                        lastThreadTimeSeen = eventData.End.Time;
                        cpuTime += eventData.End.Time - eventData.Start.Time;
                    }
                }
            }

            return cpuTime;
        }
    }
}
