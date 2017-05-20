// Copyright (c) Microsoft Corporation.  All rights reserved

using Microsoft.Diagnostics.Tracing.Analysis;
using Microsoft.Diagnostics.Tracing.Analysis.JIT;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Parsers.ClrPrivate;
using Microsoft.Diagnostics.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Address = System.UInt64;

namespace Stats
{
    internal static class JitStats
    {
        public static void ToHtml(TextWriter writer, TraceProcess stats, TraceLoadedDotNetRuntime runtime, string fileName)
        {
           JITStatsEx statsEx = JITStatsEx.Create(runtime);

            var usersGuideFile = ClrStatsUsersGuide.WriteUsersGuide(fileName);
            bool hasInliningEvents = runtime.JIT.Stats().InliningSuccesses.Count > 0 || runtime.JIT.Stats().InliningFailures.Count > 0;

            writer.WriteLine("<H3><A Name=\"Stats_{0}\"><font color=\"blue\">JIT Stats for for Process {1,5}: {2}</font><A></H3>",stats.ProcessID, stats.ProcessID, stats.Name);
            writer.WriteLine("<UL>");
#pragma warning disable CS0618 // Type or member is obsolete
            if (!runtime.JIT.Stats().IsClr4)
#pragma warning restore CS0618 // Type or member is obsolete
                writer.WriteLine("<LI><Font color=\"red\">Warning: Could not confirm that a V4.0 CLR was loaded.  JitTime or ILSize can only be computed for V4.0 runtimes.  Otherwise their value will appear as 0.</font></LI>");
            if (!string.IsNullOrEmpty(stats.CommandLine))
                writer.WriteLine("<LI>CommandLine: {0}</LI>", stats.CommandLine);
            writer.WriteLine("<LI>Process CPU Time: {0:n0} msec</LI>", stats.CPUMSec);
#pragma warning disable CS0618 // Type or member is obsolete
            if (runtime.JIT.Stats().BackgroundJitThread != 0 || runtime.JIT.Stats().BackgroundJitAbortedAtMSec != 0)
#pragma warning restore CS0618 // Type or member is obsolete
            {
                writer.WriteLine("<LI>This process uses Background JIT compilation (System.Runtime.ProfileOptimize)</LI>");
                writer.WriteLine(" <UL>");

                if (runtime.JIT.Stats().RecordedModules.Count == 0)
                {
                    writer.WriteLine(" <LI><font color=\"red\">This trace is missing some background JIT events, which could result in incorrect information in the background JIT blocking reason column.</font></LI>");
                    writer.WriteLine(" <LI><font color=\"red\">Re-collect the trace enabling \"Background JIT\" events on the collection menu to fix this.</font></LI>");
                }

#pragma warning disable CS0618 // Type or member is obsolete
                if (runtime.JIT.Stats().BackgroundJitAbortedAtMSec != 0)
                {
                    writer.WriteLine("  <LI><font color=\"red\">WARNING: Background JIT aborted at {0:n3} Msec</font></LI>", runtime.JIT.Stats().BackgroundJitAbortedAtMSec);
                    writer.WriteLine("  <LI>The last assembly before the abort was '{0}' loaded {1} at {2:n3}</LI>",
                        runtime.JIT.Stats().LastAssemblyLoadNameBeforeAbort, runtime.JIT.Stats().LastAssemblyLoadBeforeAbortSuccessful ? "successfully" : "unsuccessfully",
                        runtime.JIT.Stats().LastAssemblyLoadBeforeAbortMSec);
                }
#pragma warning restore CS0618 // Type or member is obsolete

                if (runtime.JIT.Stats().BackgroundJitThread != 0)
                {
                    var foregroundJitTimeMSec = runtime.JIT.Stats().TotalCpuTimeMSec - statsEx.TotalBGJITStats.TotalCpuTimeMSec;
                    writer.WriteLine("  <LI><strong>JIT time NOT moved to background thread</strong> : {0:n0}</LI> ({1:n1}%)",
                          foregroundJitTimeMSec, foregroundJitTimeMSec * 100.0 / runtime.JIT.Stats().TotalCpuTimeMSec);

                    var foregroundCount = runtime.JIT.Stats().Count - statsEx.TotalBGJITStats.Count;
                    writer.WriteLine("  <LI>Methods Not moved to background thread: {0:n0} ({1:n1}%)</LI>",
                        foregroundCount, foregroundCount * 100.0 / runtime.JIT.Stats().Count);

                    writer.WriteLine("  <LI>Methods Background JITTed : {0:n0} ({1:n1}%)</LI>",
                        statsEx.TotalBGJITStats.Count, statsEx.TotalBGJITStats.Count * 100.0 / runtime.JIT.Stats().Count);

                    writer.WriteLine("  <LI>MSec Background JITTing : {0:n0}</LI>", statsEx.TotalBGJITStats.TotalCpuTimeMSec);
                    writer.WriteLine("  <LI>Background JIT Thread : {0}</LI>", runtime.JIT.Stats().BackgroundJitThread);
                }
                writer.WriteLine("  <LI> <A HREF=\"command:excelBackgroundDiag/{0}\">View Raw Background Jit Diagnostics</A></LI></UL>", stats.ProcessID);
                writer.WriteLine("  <LI> See <A HREF=\"{0}#UnderstandingBackgroundJIT\">Guide to Background JIT</A></LI> for more on background JIT", usersGuideFile);
                writer.WriteLine(" </UL>");
            }
            writer.WriteLine("<LI>Total Number of JIT compiled methods : {0:n0}</LI>", runtime.JIT.Stats().Count);
            writer.WriteLine("<LI>Total MSec JIT compiling : {0:n0}</LI>", runtime.JIT.Stats().TotalCpuTimeMSec);

            if (runtime.JIT.Stats().TotalCpuTimeMSec != 0)
                writer.WriteLine("<LI>JIT compilation time as a percentage of total process CPU time : {0:f1}%</LI>", runtime.JIT.Stats().TotalCpuTimeMSec * 100.0 / stats.CPUMSec);
            writer.WriteLine("<LI><A HREF=\"#Events_{0}\">Individual JIT Events</A></LI>", stats.ProcessID);

            writer.WriteLine("<UL><LI> <A HREF=\"command:excel/{0}\">View in Excel</A></LI></UL>", stats.ProcessID);
            if (hasInliningEvents)
            {
                writer.WriteLine("<LI><A HREF=\"#Inlining_{0}\">Inlining Decisions</A></LI>", stats.ProcessID);
                writer.WriteLine("<UL><LI> <A HREF=\"command:excelInlining/{0}\">View in Excel</A></LI></UL>", stats.ProcessID);
            }
            else
            {
                writer.WriteLine("<LI><I>No JIT Inlining data available.  Consider enabling the JITInlining option.</I></LI>");
            }
            writer.WriteLine("<LI> <A HREF=\"{0}#UnderstandingJITPerf\">JIT Perf Users Guide</A></LI>", usersGuideFile);
            writer.WriteLine("</UL>");

            if (runtime.JIT.Stats().BackgroundJitThread == 0)
            {
                if (runtime.JIT.Stats().BackgroundJITEventsOn)
                {
                    writer.WriteLine("<P>" +
                        "<b>This process does not use background JIT compilation.</b>   If there is a lot of JIT time and NGEN is not an possible\r\n" +
                        "you should consider using Background JIT compilation.\r\n" +
                        "See <A HREF=\"{0}#UnderstandingBackgroundJIT\">Guide to Background JIT</A> for more." +
                        "</P>", usersGuideFile);
                }
                else
                {
                    writer.WriteLine("<P>" +
                        "<b>Background JIT compilation events are not being collected.</b>   If you are interested in seeing the operation of Background JIT\r\n" +
                        "Enabled the 'Background JIT' checkbox in the 'Advanced' section of the collection dialog when collecting the data." +
                        "See <A HREF=\"{0}#UnderstandingBackgroundJIT\">Guide to Background JIT</A> for more." +
                        "</P>", usersGuideFile);
                }
            }

            writer.WriteLine("<P>" +
                "Below is a table of the time taken to JIT compile the methods used in the program, broken down by module.  \r\n" +
                "If this time is significant you can eliminate it by <A href=\"http://msdn.microsoft.com/en-us/magazine/cc163808.aspx\">NGening</A> your application.  \r\n" +
                "This will improve the startup time for your app.  \r\n" +
                "</P>");

            writer.WriteLine("<P>" +
                "The list below is also useful for tuning the startup performance of your application in general.  \r\n" +
                "In general you want as little to be run during startup as possible.  \r\n" +
                "If you have 1000s of methods being compiled on startup " +
                "you should try to defer some of that computation until absolutely necessary.\r\n" +
                "</P>");

            // Sort the module list by Jit Time;
            List<string> moduleNames = new List<string>(statsEx.TotalModuleStats.Keys);
            moduleNames.Sort(delegate (string x, string y)
            {
                double diff = statsEx.TotalModuleStats[y].TotalCpuTimeMSec - statsEx.TotalModuleStats[x].TotalCpuTimeMSec;
                if (diff > 0)
                    return 1;
                else if (diff < 0)
                    return -1;
                return 0;
            });

            writer.WriteLine("<Center>");
            writer.WriteLine("<Table Border=\"1\">");
            writer.WriteLine("<TR><TH>Name</TH><TH>JitTime<BR/>msec</TH><TH>Num Methods</TH><TH>IL Size</TH><TH>Native Size</TH></TR>");
            writer.WriteLine("<TR><TD Align=\"Left\">{0}</TD><TD Align=\"Center\">{1:n1}</TD><TD Align=\"Center\">{2:n0}</TD><TD Align=\"Center\">{3:n0}</TD><TD Align=\"Center\">{4:n0}</TD></TR>",
                "TOTAL", runtime.JIT.Stats().TotalCpuTimeMSec, runtime.JIT.Stats().Count, runtime.JIT.Stats().TotalILSize, runtime.JIT.Stats().TotalNativeSize);
            foreach (string moduleName in moduleNames)
            {
                JITStats info = statsEx.TotalModuleStats[moduleName];
                writer.WriteLine("<TR><TD Align=\"Center\">{0}</TD><TD Align=\"Center\">{1:n1}</TD><TD Align=\"Center\">{2:n0}</TD><TD Align=\"Center\">{3:n0}</TD><TD Align=\"Center\">{4:n0}</TD></TR>",
                    moduleName.Length == 0 ? "&lt;UNKNOWN&gt;" : moduleName, info.TotalCpuTimeMSec, info.Count, info.TotalILSize, info.TotalNativeSize);
            }
            writer.WriteLine("</Table>");
            writer.WriteLine("</Center>");

            bool backgroundJitEnabled = runtime.JIT.Stats().BackgroundJitThread != 0;

            writer.WriteLine("<HR/>");
            writer.WriteLine("<H4><A Name=\"Events_{0}\">Individual JIT Events for Process {1,5}: {2}<A></H4>", stats.ProcessID, stats.ProcessID, stats.Name);

            // We limit the number of JIT events we ut on the page because it makes the user exerience really bad (browsers crash)
            const int maxEvents = 1000;
            if (runtime.JIT.Methods.Count >= maxEvents)
                writer.WriteLine("<p><Font color=\"red\">Warning: Truncating JIT events to " + maxEvents + ".  Use 'View in Excel' link above to look all of them</font></p>");

            writer.WriteLine("<Center>");
            writer.WriteLine("<Table Border=\"1\">");
            writer.Write("<TR><TH>Start (msec)</TH><TH>JitTime</BR>msec</TH><TH>IL Size</TH><TH>Native Size</TH><TH>Method Name</TH>" +
                "<TH Title=\"Is Jit compilation occuring in the background (BG) or not (JIT).\">BG</TH><TH>Module</TH>");
            if (backgroundJitEnabled)
            {
                writer.Write("<TH Title=\"How far ahead of the method usage was relative to the background JIT operation.\">Distance Ahead</TH><TH Title=\"Why the method was not JITTed in the background.\">Background JIT Blocking Reason</TH>");
            }
            writer.WriteLine("</TR>");
            int eventCount = 0;
            foreach (TraceJittedMethod _event in runtime.JIT.Methods)
            {
                writer.Write("<TR><TD Align=\"Center\">{0:n3}</TD><TD Align=\"Center\">{1:n1}</TD><TD Align=\"Center\">{2:n0}</TD><TD Align=\"Center\">{3:n0}</TD><TD Align=Left>{4}</TD><TD Align=\"Center\">{5}</TD><TD Align=\"Center\">{6}</TD>",
                    _event.StartTimeMSec, _event.CompileCpuTimeMSec, _event.ILSize, _event.NativeSize, _event.MethodName ?? "&nbsp;", (_event.IsBackGround ? "BG" : "JIT"),
                    _event.ModuleILPath.Length != 0 ? Path.GetFileName(_event.ModuleILPath) : "&lt;UNKNOWN&gt;");
                if (backgroundJitEnabled)
                {
                    writer.Write("<TD Align=\"Center\">{0:n3}</TD><TD Align=\"Left\">{1}</TD>",
#pragma warning disable CS0618 // Type or member is obsolete
                        _event.DistanceAhead, _event.IsBackGround ? "Not blocked" : _event.BlockedReason);
#pragma warning restore CS0618 // Type or member is obsolete
                }
                writer.WriteLine("</TR>");
                eventCount++;
                if (eventCount >= maxEvents)
                    break;
            }
            writer.WriteLine("</Table>");
            writer.WriteLine("</Center>");

            if (hasInliningEvents)
            {
                writer.WriteLine("<HR/>");
                writer.WriteLine("<A Name=\"Inlining_{0}\">", stats.ProcessID);
                writer.WriteLine("<H4>Successful Inlinings for Process {0,5}: {1}<A></H4>", stats.ProcessID, stats.Name);
                writer.WriteLine("<Center>");
                writer.WriteLine("<Table Border=\"1\">");
                writer.Write("<TR><TH>Method Begin Compiled</TH><TH>Inliner</TH><TH>Inlinee</TH></TR>");
                foreach (InliningSuccessResult success in runtime.JIT.Stats().InliningSuccesses)
                {
                    writer.Write("<TR><TD>{0}</TD><TD>{1}</TD><TD>{2}</TD></TR>", success.MethodBeingCompiled, success.Inliner, success.Inlinee);
                }
                writer.WriteLine("</Table>");
                writer.WriteLine("</Center>");
                writer.WriteLine("<H4>Failed Inlinings for Process {0,5}: {1}<A></H4>", stats.ProcessID, stats.Name);
                writer.WriteLine("<Center>");
                writer.WriteLine("<Table Border=\"1\">");
                writer.Write("<TR><TH>Method Begin Compiled</TH><TH>Inliner</TH><TH>Inlinee</TH><TH>Failure Reason</TH></TR>");
                foreach (InliningFailureResult failure in runtime.JIT.Stats().InliningFailures)
                {
                    writer.Write("<TR><TD>{0}</TD><TD>{1}</TD><TD>{2}</TD><TD>{3}</TD></TR>", failure.MethodBeingCompiled, failure.Inliner, failure.Inlinee, failure.Reason);
                }
                writer.WriteLine("</Table>");
                writer.WriteLine("</Center>");
            }

            writer.WriteLine("<HR/><HR/><BR/><BR/>");
        }

        public static void ToCsv(string filePath, TraceLoadedDotNetRuntime runtime)
        {
            var listSeparator = Thread.CurrentThread.CurrentCulture.TextInfo.ListSeparator;
            using (var writer = File.CreateText(filePath))
            {
                writer.WriteLine("Start MSec{0}JitTime MSec{0}ThreadID{0}IL Size{0}Native Size{0}MethodName{0}BG{0}Module{0}DistanceAhead{0}BlockedReason", listSeparator);
                for (int i = 0; i < runtime.JIT.Methods.Count; i++)
                {
                    var _event = runtime.JIT.Methods[i];
                    var csvMethodName = _event.MethodName.Replace(",", " ");    // Insure there are no , in the name 
                    writer.WriteLine("{1:f3}{0}{2:f3}{0}{3}{0}{4}{0}{5}{0}{6}{0}{7}{0}{8}{0}{9}{0}{10}", listSeparator,
                        _event.StartTimeMSec, _event.CompileCpuTimeMSec, _event.ThreadID, _event.ILSize,
#pragma warning disable CS0618 // Type or member is obsolete
                        _event.NativeSize, csvMethodName, (_event.IsBackGround ? "BG" : "JIT"), _event.ModuleILPath, _event.DistanceAhead, _event.BlockedReason);
#pragma warning restore CS0618 // Type or member is obsolete
                }
            }
        }

        public static void ToInliningCsv(string filePath, TraceLoadedDotNetRuntime runtime)
        {
            var listSeparator = Thread.CurrentThread.CurrentCulture.TextInfo.ListSeparator;
            using (var writer = File.CreateText(filePath))
            {
                writer.WriteLine("MethodBeginCompiled{0}Inliner{0}Inlinee{0}FailureReason", listSeparator);
                foreach (var ev in runtime.JIT.Stats().InliningSuccesses)
                {
                    writer.WriteLine("{1}{0}{2}{0}{3}{0}{4}", listSeparator,
                        ev.MethodBeingCompiled.Replace(listSeparator, ""),
                        ev.Inliner.Replace(listSeparator, ""),
                        ev.Inlinee.Replace(listSeparator, ""),
                        "<success>");
                }
                foreach (var ev in runtime.JIT.Stats().InliningFailures)
                {
                    writer.WriteLine("{1}{0}{2}{0}{3}{0}{4}", listSeparator,
                        ev.MethodBeingCompiled.Replace(listSeparator, ""),
                        ev.Inliner.Replace(listSeparator, ""),
                        ev.Inlinee.Replace(listSeparator, ""),
                        ev.Reason.Replace(listSeparator, ""));
                }
            }
        }

        /// <summary>
        /// Write data about background JIT activities to 'outputCsvFilePath'
        /// </summary>
        public static void BackgroundDiagCsv(string outputCsvFilePath, TraceLoadedDotNetRuntime stats, List<object> events)
        {
            var listSeparator = Thread.CurrentThread.CurrentCulture.TextInfo.ListSeparator;
            using (var writer = File.CreateText(outputCsvFilePath))
            {
                writer.WriteLine("MSec{0}Command{0}Arg1{0}Arg2{0}Arg3{0}Arg4{0}", listSeparator);
                for (int i = 0; i < events.Count; i++)
                {
                    var event_ = events[i];
                    var bgEvent = event_ as MulticoreJitPrivateTraceData;
                    if (bgEvent != null)
                    {
                        writer.WriteLine("{1:f3}{0}{2}{0}\"{3}\"{0}{4}{0}{5}{0}{6}", listSeparator,
                            bgEvent.TimeStampRelativeMSec, bgEvent.String1, bgEvent.String2, bgEvent.Int1, bgEvent.Int2, bgEvent.Int3);
                        continue;
                    }
                    var loadEvent = event_ as ModuleLoadUnloadTraceData;
                    if (loadEvent != null)
                    {
                        writer.WriteLine("{1:f3}{0}{2}{0}\"{3}\"{0}{4}{0}{5}{0}{6}", listSeparator,
                            loadEvent.TimeStampRelativeMSec, "MODULE_LOAD", Path.GetFileName(loadEvent.ModuleILPath), 0, 0, 0);
                        continue;
                    }
                }
            }
        }

        public static void ToXml(TextWriter writer, TraceProcess stats, TraceLoadedDotNetRuntime runtime, string indent)
        {
            JITStatsEx statsEx = JITStatsEx.Create(runtime);

            // TODO pay attention to indent;
            writer.Write(" <JitProcess Process=\"{0}\" ProcessID=\"{1}\" JitTimeMSec=\"{2:n3}\" Count=\"{3}\" ILSize=\"{4}\" NativeSize=\"{5}\"",
                stats.Name, stats.ProcessID, runtime.JIT.Stats().TotalCpuTimeMSec, runtime.JIT.Stats().Count, runtime.JIT.Stats().TotalILSize, runtime.JIT.Stats().TotalNativeSize);
            if (stats.CPUMSec != 0)
                writer.Write(" ProcessCpuTimeMsec=\"{0}\"", stats.CPUMSec);
            if (!string.IsNullOrEmpty(stats.CommandLine))
                writer.Write(" CommandLine=\"{0}\"", XmlUtilities.XmlEscape(stats.CommandLine, false));
            writer.WriteLine(">");
            writer.WriteLine("  <JitEvents>");
            foreach (TraceJittedMethod _event in runtime.JIT.Methods)
                ToXml(writer, _event);
            writer.WriteLine("  </JitEvents>");

            writer.WriteLine(" <ModuleStats Count=\"{0}\" TotalCount=\"{1}\" TotalJitTimeMSec=\"{2:n3}\" TotalILSize=\"{3}\" TotalNativeSize=\"{4}\">",
                statsEx.TotalModuleStats.Count, runtime.JIT.Stats().Count, runtime.JIT.Stats().TotalCpuTimeMSec, runtime.JIT.Stats().TotalILSize, runtime.JIT.Stats().TotalNativeSize);

            // Sort the module list by Jit Time;
            List<string> moduleNames = new List<string>(statsEx.TotalModuleStats.Keys);
            moduleNames.Sort(delegate (string x, string y)
            {
                double diff = statsEx.TotalModuleStats[y].TotalCpuTimeMSec - statsEx.TotalModuleStats[x].TotalCpuTimeMSec;
                if (diff > 0)
                    return 1;
                else if (diff < 0)
                    return -1;
                return 0;
            });

            foreach (string moduleName in moduleNames)
            {
                JITStats info = statsEx.TotalModuleStats[moduleName];
                writer.Write("<Module");
                writer.Write(" JitTimeMSec={0}", StringUtilities.QuotePadLeft(info.TotalCpuTimeMSec.ToString("n3"), 11));
                writer.Write(" Count={0}", StringUtilities.QuotePadLeft(info.Count.ToString(), 7));
                writer.Write(" ILSize={0}", StringUtilities.QuotePadLeft(info.TotalILSize.ToString(), 9));
                writer.Write(" NativeSize={0}", StringUtilities.QuotePadLeft(info.TotalNativeSize.ToString(), 9));
                writer.Write(" Name=\"{0}\"", moduleName);
                writer.WriteLine("/>");
            }
            writer.WriteLine("  </ModuleStats>");

            writer.WriteLine(" </JitProcess>");
        }

        public static void ToXml(TextWriter writer, TraceJittedMethod info)
        {
            writer.Write("   <JitEvent");
            writer.Write(" StartMSec={0}", StringUtilities.QuotePadLeft(info.StartTimeMSec.ToString("n3"), 10));
            writer.Write(" JitTimeMSec={0}", StringUtilities.QuotePadLeft(info.CompileCpuTimeMSec.ToString("n3"), 8));
            writer.Write(" ILSize={0}", StringUtilities.QuotePadLeft(info.ILSize.ToString(), 10));
            writer.Write(" NativeSize={0}", StringUtilities.QuotePadLeft(info.NativeSize.ToString(), 10));
            if (info.MethodName != null)
            {
                writer.Write(" MethodName="); writer.Write(XmlUtilities.XmlQuote(info.MethodName));
            }
            if (info.ModuleILPath != null)
            {
                writer.Write(" ModuleILPath="); writer.Write(XmlUtilities.XmlQuote(info.ModuleILPath));
            }
#pragma warning disable CS0618 // Type or member is obsolete
            writer.Write(" DistanceAhead={0}", StringUtilities.QuotePadLeft(info.DistanceAhead.ToString("n3"), 10));
            writer.Write(" BlockedReason="); writer.Write(XmlUtilities.XmlQuote(info.BlockedReason));
#pragma warning restore CS0618 // Type or member is obsolete
            writer.WriteLine("/>");
        }
    }

    class JITStatsEx
    {
        public JITStats TotalBGJITStats = new JITStats();
        public SortedDictionary<string, JITStats> TotalModuleStats = new SortedDictionary<string, JITStats>();

        public static JITStatsEx Create(TraceLoadedDotNetRuntime mang)
        {
            JITStatsEx stats = new JITStatsEx();

            if (mang.JIT.Stats().BackgroundJitThread != 0)
            {
                stats.TotalBGJITStats = new JITStats();
                foreach (TraceJittedMethod _method in mang.JIT.Methods)
                {
                    if (_method.ThreadID == mang.JIT.Stats().BackgroundJitThread)
                    {
                        _method.IsBackGround = true;
                        // update stats
                        stats.TotalBGJITStats.Count++;
                        stats.TotalBGJITStats.TotalCpuTimeMSec += _method.CompileCpuTimeMSec;
                        stats.TotalBGJITStats.TotalILSize += _method.ILSize;
                        stats.TotalBGJITStats.TotalNativeSize += _method.NativeSize;
                    }
                }
            }

            // Compute module level stats, we do it here because we may not have the IL method name until late. 
            stats.TotalModuleStats = new SortedDictionary<string, JITStats>(StringComparer.OrdinalIgnoreCase);
            foreach (var _method in mang.JIT.Methods)
            {
                if (_method.ModuleILPath != null)
                {
                    if (!stats.TotalModuleStats.ContainsKey(_method.ModuleILPath)) stats.TotalModuleStats.Add(_method.ModuleILPath, new JITStats());
                    // update stats
                    stats.TotalModuleStats[_method.ModuleILPath].Count++;
                    stats.TotalModuleStats[_method.ModuleILPath].TotalCpuTimeMSec += _method.CompileCpuTimeMSec;
                    stats.TotalModuleStats[_method.ModuleILPath].TotalILSize += _method.ILSize;
                    stats.TotalModuleStats[_method.ModuleILPath].TotalNativeSize += _method.NativeSize;
                }
            }

            return stats;
        }
        
    }
}
