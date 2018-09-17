// Copyright (c) Microsoft Corporation.  All rights reserved
using Microsoft.Diagnostics.Tracing.Analysis;
using Microsoft.Diagnostics.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Stats
{
    internal static class ClrStats
    {
        public enum ReportType { JIT, GC };

        public static void ToHtml(TextWriter writer, List<TraceProcess> perProc, string fileName, string title, ReportType type, bool justBody = false, bool doServerGCReport = false)
        {
            if (!justBody)
            {
                writer.WriteLine("<html>");
                writer.WriteLine("<head>");
                writer.WriteLine("<title>{0}</title>", Path.GetFileNameWithoutExtension(fileName));
                writer.WriteLine("<meta charset=\"UTF-8\"/>");
                writer.WriteLine("<meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\"/>");
                writer.WriteLine("</head>");
                writer.WriteLine("<body>");
            }
            writer.WriteLine("<H2>{0}</H2>", title);
            List<TraceProcess> sortedProcs = perProc;
            if (type == ReportType.JIT)
            {
                sortedProcs.Sort((TraceProcess p1, TraceProcess p2) => { return -p1.LoadedDotNetRuntime().JIT.Stats().TotalCpuTimeMSec.CompareTo(p2.LoadedDotNetRuntime().JIT.Stats().TotalCpuTimeMSec); });
            }
            else if (type == ReportType.GC)
            {
                sortedProcs.Sort((TraceProcess p1, TraceProcess p2) => { return -p1.LoadedDotNetRuntime().GC.Stats().MaxSizePeakMB.CompareTo(p2.LoadedDotNetRuntime().GC.Stats().MaxSizePeakMB); });
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
            }

            writer.WriteLine("<BR/><BR/><BR/><BR/><BR/><BR/><BR/><BR/><BR/><BR/>");
            if (!justBody)
            {
                writer.WriteLine("</body>");
                writer.WriteLine("</html>");
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
