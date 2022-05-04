// Copyright (c) Microsoft Corporation.  All rights reserved

using Microsoft.Diagnostics.Tracing.Analysis;
using Etlx = Microsoft.Diagnostics.Tracing.Etlx;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System;

namespace Stats
{
    internal static class FileVersionInformation
    {
        public static void ToHtml(TextWriter writer, TraceProcess stats, Etlx.TraceLog traceLog)
        {
            Etlx.TraceProcess traceProcess = traceLog.Processes.GetProcess(stats.ProcessID, stats.StartTimeRelativeMsec + 1);
            if (traceProcess == null)
            {
                return;
            }

            writer.WriteLine("<H3><A Name=\"Stats_{0}\"><font color=\"blue\">File Version Information for for Process {1,5}: {2}</font><A></H3>", stats.ProcessID, stats.ProcessID, stats.Name);
            writer.WriteLine("<UL>");
            {
                writer.WriteLine("<LI>CommandLine: {0}</LI>", traceProcess.CommandLine);
            }
            writer.WriteLine("</UL>");

            writer.WriteLine("<H4><A Name=\"Events_{0}\">Individual Loaded Modules for Process {1,5}: {2}<A></H4>", stats.ProcessID, stats.ProcessID, stats.Name);

            writer.WriteLine("<Center>");
            writer.WriteLine("<Table Border=\"1\">");
            writer.Write(
                "<TR>" +
                "<TH>File Path</TH>" +
                "<TH>Version</TH>" +
                "</TR>");
            List<Etlx.TraceLoadedModule> modules = traceProcess.LoadedModules.ToList();
            modules.Sort((Etlx.TraceLoadedModule t1, Etlx.TraceLoadedModule t2) => { return string.Compare(t1.Name, t2.Name); });
            foreach (Etlx.TraceLoadedModule module in modules)
            {
                Etlx.TraceModuleFile moduleFile = module.ModuleFile;
                writer.Write(
                    "<TR>" +
                    "<TD Align=\"Left\">{0}</TD>" +
                    "<TD Align=\"Center\">{1}</TD>" +
                    "</TR>",
                    moduleFile.FilePath,
                    moduleFile.FileVersion);
            }
            writer.WriteLine("</Table>");
            writer.WriteLine("</Center>");

            writer.WriteLine("<HR/><HR/><BR/><BR/>");
        }
    }
}
