// Copyright (c) Microsoft Corporation.  All rights reserved
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Analysis;
using Microsoft.Diagnostics.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Stats
{
    internal static class ClrStats
    {
        public enum ReportType { JIT, GC, RuntimeLoader, FileVersion };

        public static void ToHtml(TextWriter writer, List<TraceProcess> perProc, string fileName, string title, ReportType type, bool justBody = false, bool doServerGCReport = false, RuntimeLoaderStatsData runtimeOpsStats = null, Microsoft.Diagnostics.Tracing.Etlx.TraceLog traceLog = null)
        {
            if (!justBody)
            {
                writer.WriteLine($$"""
                    <html>
                      <head>
                        <title>{{Path.GetFileNameWithoutExtension(fileName)}}</title>
                        <meta charset="UTF-8"/>
                        <style>
                          :root[data-theme="light"] {
                            color-scheme: light;
                            --error-color: #B3261E;   /* dark red for light mode (accessible on white) */
                            --hover-color: #eeeeee;   /* light grey for hover on light bg */
                          }
                    
                          :root[data-theme="dark"] {
                            color-scheme: dark;
                            --error-color: #ffb4ab;   /* soft red/pink for dark mode (better on dark) */
                            --hover-color: #333333;   /* dark grey for hover on dark bg */
                          }

                          .error {
                            color: var(--error-color);
                            font-weight: bold;
                          }
                        </style>
                      </head>
                      <body>
                        <H2>{{title}}</H2>
                    """);
            }

            List<TraceProcess> sortedProcs = perProc;
            if (type == ReportType.JIT)
            {
                sortedProcs.Sort((TraceProcess p1, TraceProcess p2) => { return -p1.LoadedDotNetRuntime().JIT.Stats().TotalCpuTimeMSec.CompareTo(p2.LoadedDotNetRuntime().JIT.Stats().TotalCpuTimeMSec); });
            }

            else if (type == ReportType.GC)
            {
                sortedProcs.Sort((TraceProcess p1, TraceProcess p2) => { return -p1.LoadedDotNetRuntime().GC.Stats().MaxSizePeakMB.CompareTo(p2.LoadedDotNetRuntime().GC.Stats().MaxSizePeakMB); });
            }
            else if (type == ReportType.RuntimeLoader)
            {
                sortedProcs.Sort((TraceProcess p1, TraceProcess p2) => { return -RuntimeLoaderStats.TotalCPUMSec(p1, runtimeOpsStats).CompareTo(RuntimeLoaderStats.TotalCPUMSec(p2, runtimeOpsStats)); });
            }
            else if(type == ReportType.FileVersion)
            {
                sortedProcs.Sort((TraceProcess p1, TraceProcess p2) => { return string.Compare(p1.Name, p2.Name); });
            }

            int count = sortedProcs.Count;

            if (count > 1)
            {
                writer.WriteLine("<UL>");
                foreach (var data in sortedProcs)
                {
                    var mang = data.LoadedDotNetRuntime();

                    if (mang == null)
                    {
                        continue;
                    }

                    if (type == ReportType.JIT && !mang.JIT.Stats().Interesting)
                    {
                        continue;
                    }

                    if (type == ReportType.RuntimeLoader && !RuntimeLoaderStats.IsInteresting(data, runtimeOpsStats))
                    {
                        continue;
                    }

                    var id = Shorten(data.CommandLine);
                    if (string.IsNullOrEmpty(id))
                    {
                        id = data.Name;
                    }

                    writer.WriteLine("<LI><A HREF=\"#Stats_{0}\">Process {0,5}: {1}</A></LI>", data.ProcessID, XmlUtilities.XmlEscape(id));
                }
                writer.WriteLine("</UL>");
                writer.WriteLine("<HR/><HR/><BR/><BR/>");
            }
            foreach (TraceProcess stats in sortedProcs)
            {
                var mang = stats.LoadedDotNetRuntime();
                if (mang == null)
                {
                    continue;
                }

                if (type == ReportType.GC)
                {
                    Stats.GcStats.ToHtml(writer, stats, mang, fileName, doServerGCReport);
                }

                if (type == ReportType.JIT && mang.JIT.Stats().Interesting)
                {
                    Stats.JitStats.ToHtml(writer, stats, mang, fileName);
                }

                if (type == ReportType.RuntimeLoader && RuntimeLoaderStats.IsInteresting(stats, runtimeOpsStats))
                {
                    Stats.RuntimeLoaderStats.ToHtml(writer, stats, fileName, runtimeOpsStats);
                }

                if(type == ReportType.FileVersion)
                {
                    Stats.FileVersionInformation.ToHtml(writer, stats, traceLog);
                }
            }

            writer.WriteLine("<BR/><BR/><BR/><BR/><BR/><BR/><BR/><BR/><BR/><BR/>");
            if (!justBody)
            {
                writer.WriteLine("""
                    <script>
                      // Set theme based on user preference
                      const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;
                      document.documentElement.setAttribute('data-theme', prefersDark ? 'dark' : 'light');
                    </script>
                  </body>
                </html>
                """);
            }
        }

        /// <summary>
        /// Returns a shortened command line
        /// </summary>
        /// <param name="commandLine"></param>
        /// <returns></returns>
        public static string Shorten(string commandLine)
        {
            if (commandLine == null)
            {
                return null;
            }

            // Remove quotes, replacing ' ' with '_'
            commandLine = Regex.Replace(commandLine, "\"(.*?)\"", (m) => m.Groups[1].Value.Replace(' ', '_'));

            // Remove .exe suffixes. 
            commandLine = Regex.Replace(commandLine, ".exe", "");

            // Remove the front part of paths
            commandLine = Regex.Replace(commandLine, @"(\S+)\\(\S*)", (m) => m.Groups[2].Value == "" ? "\\" : m.Groups[2].Value);

            // truncate if necessary. 
            if (commandLine.Length > 80)
            {
                commandLine = commandLine.Substring(0, 80) + "...";
            }

            return commandLine;
        }
    }
}
