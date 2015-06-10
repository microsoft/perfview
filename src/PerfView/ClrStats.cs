// Copyright (c) Microsoft Corporation.  All rights reserved
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Parsers.ClrPrivate;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Parsers.Symbol;
using Microsoft.Diagnostics.Utilities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Utilities;
using Address = System.UInt64;

namespace Stats
{
    /************************************************************************/
    /*  JIT Stats */

    /// <summary>
    /// JitInfo holds statistics on groups of methods. 
    /// </summary>
    class JitInfo
    {
        public int Count;
        public double JitTimeMSec;
        public int ILSize;
        public int NativeSize;
        #region private
        internal void Update(JitEvent _event)
        {
            Count++;
            JitTimeMSec += _event.JitTimeMSec;
            ILSize += _event.ILSize;
            NativeSize += _event.NativeSize;
        }
        #endregion
    };

    /// <summary>
    /// JitEvent holds information on a JIT compile of a particular method 
    /// </summary>
    class JitEvent
    {
        public double JitTimeMSec;
        public int ILSize;
        public int NativeSize;

        public double StartTimeMSec;
        public string MethodName;
        public string ModuleILPath
        {
            get
            {
                string str = null;
                if (this.m_jitProcess != null)
                {
                    this.m_jitProcess.moduleNamesFromID.TryGetValue(this.m_ModuleILID, out str);
                }
                if (str == null)
                {
                    str = "";
                }
                return str;
            }
        }
        public int ThreadID;
        public bool IsBackGround;
        public double DistanceAhead
        {
            get
            {
                double distanceAhead = 0;
                if (default(double) != ForegroundMethodRequestTimeMSec)
                {
                    distanceAhead = ForegroundMethodRequestTimeMSec - StartTimeMSec;
                }

                return distanceAhead;
            }
        }
        public string BlockedReason
        {
            get
            {
                if (null == _blockedReason)
                {
                    _blockedReason = "None";
                }

                return _blockedReason;
            }
            set
            {
                _blockedReason = value;
            }
        }

        public void ToXml(TextWriter writer)
        {
            writer.Write("   <JitEvent");
            writer.Write(" StartMSec="); GCEvent.QuotePadLeft(writer, StartTimeMSec.ToString("n3"), 10);
            writer.Write(" JitTimeMSec="); GCEvent.QuotePadLeft(writer, JitTimeMSec.ToString("n3"), 8);
            writer.Write(" ILSize="); GCEvent.QuotePadLeft(writer, ILSize.ToString(), 10);
            writer.Write(" NativeSize="); GCEvent.QuotePadLeft(writer, NativeSize.ToString(), 10);
            if (MethodName != null)
            {
                writer.Write(" MethodName="); writer.Write(XmlUtilities.XmlQuote(MethodName));
            }
            if (ModuleILPath != null)
            {
                writer.Write(" ModuleILPath="); writer.Write(XmlUtilities.XmlQuote(ModuleILPath));
            }
            writer.Write(" DistanceAhead="); GCEvent.QuotePadLeft(writer, DistanceAhead.ToString("n3"), 10);
            writer.Write(" BlockedReason="); writer.Write(XmlUtilities.XmlQuote(BlockedReason));
            writer.WriteLine("/>");
        }
        #region private
        internal double ForegroundMethodRequestTimeMSec;
        internal JitProcess m_jitProcess;
        internal long m_ModuleILID;
        internal long m_MethodILID;
        private string _blockedReason;
        #endregion
    }

    /// <summary>
    /// JitProcess holds information about Jitting for a particular process.
    /// </summary>
    class JitProcess : ProcessLookupContract, IComparable<JitProcess>
    {
        public static ProcessLookup<JitProcess> Collect(TraceEventDispatcher source)
        {
            ProcessLookup<JitProcess> perProc = new ProcessLookup<JitProcess>();
            bool backgroundJITEventsOn = false;

            source.Clr.MethodJittingStarted += delegate(MethodJittingStartedTraceData data)
            {
                JitProcess stats = perProc[data];
                stats.LogJitStart(data, GetMethodName(data), data.MethodILSize, data.ModuleID, data.MethodID);
            };
            ClrRundownTraceEventParser parser = new ClrRundownTraceEventParser(source);
            Action<ModuleLoadUnloadTraceData> moduleLoadAction = delegate(ModuleLoadUnloadTraceData data)
            {
                JitProcess stats = perProc[data];
                stats.moduleNamesFromID[data.ModuleID] = data.ModuleILPath;
            };
            source.Clr.LoaderModuleLoad += moduleLoadAction;
            source.Clr.LoaderModuleUnload += moduleLoadAction;
            parser.LoaderModuleDCStop += moduleLoadAction;

            source.Clr.MethodLoadVerbose += delegate(MethodLoadUnloadVerboseTraceData data)
            {
                if (data.IsJitted)
                    MethodComplete(perProc, data, data.MethodSize, data.ModuleID, GetMethodName(data), data.MethodID);
            };

            source.Clr.MethodLoad += delegate(MethodLoadUnloadTraceData data)
            {
                if (data.IsJitted)
                    MethodComplete(perProc, data, data.MethodSize, data.ModuleID, "", data.MethodID);
            };
            source.Clr.RuntimeStart += delegate(RuntimeInformationTraceData data)
            {
                JitProcess stats = perProc[data];
                stats.isClr4 = true;
                if (stats.CommandLine == null)
                    stats.CommandLine = data.CommandLine;
            };

            source.Kernel.ProcessGroup += delegate(ProcessTraceData data)
            {
                var stats = perProc[data];
                var commandLine = data.CommandLine;
                if (!string.IsNullOrEmpty(commandLine))
                    stats.CommandLine = commandLine;
            };

            source.Kernel.PerfInfoSample += delegate(SampledProfileTraceData data)
            {
                JitProcess stats = perProc.TryGet(data);
                if (stats != null)
                    stats.ProcessCpuTimeMsec++;
            };

            var clrPrivate = new ClrPrivateTraceEventParser(source);
            clrPrivate.ClrMulticoreJitCommon += delegate(MulticoreJitPrivateTraceData data)
            {
                JitProcess proc = perProc[data];
                if (!backgroundJITEventsOn)
                {
                    proc.LastBlockedReason = null;
                }
                backgroundJITEventsOn = true;

                if (proc.ProcessName == null)
                {
                    proc.ProcessName = data.ProcessName;
                }

                if (proc.BackGroundJitEvents == null)
                {
                    proc.BackGroundJitEvents = new List<TraceEvent>();
                }

                proc.BackGroundJitEvents.Add(data.Clone());

                if (proc.BackgroundJitThread == 0 && (data.String1 == "GROUPWAIT" || data.String1 == "JITTHREAD"))
                {
                    proc.BackgroundJitThread = data.ThreadID;
                }

                if (data.String1 == "ADDMODULEDEPENDENCY")
                {
                    // Add the blocked module to the list of recorded modules.
                    if (!proc.recordedModules.Contains(data.String2))
                    {
                        proc.recordedModules.Add(data.String2);
                    }
                }

                if (data.String1 == "BLOCKINGMODULE")
                {
                    // Set the blocking module.
                    proc.LastBlockedReason = data.String2;

                    // Add the blocked module to the list of recorded modules.
                    if (!proc.recordedModules.Contains(data.String2))
                    {
                        proc.recordedModules.Add(data.String2);
                    }
                }

                if (data.String1 == "GROUPWAIT" && data.String2 == "Leave")
                {
                    if (data.Int2 == 0)
                    {
                        // Clear the last blocked reason, since we're no longer blocked on modules.
                        proc.LastBlockedReason = null;
                    }
                    else
                    {
                        // If GroupWait returns and Int2 != 0, this means that not all of the module loads were satisifed
                        // and we have aborted playback.
                        proc.LastBlockedReason = "Playback Aborted";
                        proc.playbackAborted = true;
                    }
                }

                if (data.String1 == "ABORTPROFILE")
                {
                    proc.BackgroundJitAbortedAtMSec = data.TimeStampRelativeMSec;
                }
            };
            clrPrivate.ClrMulticoreJitMethodCodeReturned += delegate(MulticoreJitMethodCodeReturnedPrivateTraceData data)
            {
                backgroundJITEventsOn = true;
                JitProcess proc = perProc[data];
                if (proc.BackGroundJitEvents == null)
                {
                    proc.BackGroundJitEvents = new List<TraceEvent>();
                }
                proc.BackGroundJitEvents.Add(data.Clone());

                // Get the associated JIT event.
                JitEvent backgroundJitEvent = null;

                MethodKey methodKey = new MethodKey(data.ModuleID, data.MethodID);
                if (proc.backgroundJitEvents.TryGetValue(methodKey, out backgroundJitEvent))
                {
                    if (backgroundJitEvent.ThreadID == proc.BackgroundJitThread)
                    {
                        backgroundJitEvent.ForegroundMethodRequestTimeMSec = data.TimeStampRelativeMSec;
                        proc.backgroundJitEvents.Remove(methodKey);
                    }
                }
            };
            source.Clr.LoaderModuleLoad += delegate(ModuleLoadUnloadTraceData data)
            {
                JitProcess proc = perProc[data];
                if (proc.BackGroundJitEvents == null)
                    proc.BackGroundJitEvents = new List<TraceEvent>();
                proc.BackGroundJitEvents.Add(data.Clone());
            };

            clrPrivate.BindingLoaderPhaseStart += delegate(BindingTraceData data)
            {
                // Keep track if the last assembly loaded before Background JIT aborts.  
                JitProcess proc = perProc[data];
                if (proc.BackgroundJitAbortedAtMSec == 0)
                {
                    proc.LastAssemblyLoadNameBeforeAbort = data.AssemblyName;
                    proc.LastAssemblyLoadBeforeAbortMSec = data.TimeStampRelativeMSec;
                }
            };

            clrPrivate.BindingLoaderDeliverEventsPhaseStop += delegate(BindingTraceData data)
            {
                // If we hit this events, we assume assembly load is successful. 
                JitProcess proc = perProc[data];
                if (proc.BackgroundJitAbortedAtMSec != 0)
                {
                    if (proc.LastAssemblyLoadNameBeforeAbort == data.AssemblyName)
                        proc.LastAssemblyLoadBeforeAbortSuccessful = true;
                }
            };

            clrPrivate.StartupPrestubWorkerStart += delegate(StartupTraceData data)
            {
                // TODO, we want to know if we have background JIT events.   Today we don't have an event
                // that says 'events are enabled, its just no one used the events'  We want this.  
                // Today we turn on all CLRPrivate events to turn on listening to Backgroung JITTing and
                // we use the fact that the PrestubWorker evnets are on as a proxy.  
                backgroundJITEventsOn = true;
            };
            source.Clr.AppDomainResourceManagementThreadTerminated += delegate(ThreadTerminatedOrTransitionTraceData data)
            {
                JitProcess proc = perProc[data];
                if (!proc.playbackAborted)
                {
                    proc.LastBlockedReason = "Playback Completed";
                }
            };

            source.Process();
            foreach (JitProcess jitProcess in perProc)
            {
                if (backgroundJITEventsOn)
                    jitProcess.BackgroundJITEventsOn = true;
                if (jitProcess.BackgroundJitThread != 0)
                {
                    jitProcess.BackGround = new JitInfo();
                    foreach (JitEvent _event in jitProcess.events)
                    {
                        if (_event.ThreadID == jitProcess.BackgroundJitThread)
                        {
                            _event.IsBackGround = true;
                            jitProcess.BackGround.Update(_event);
                        }
                    }
                }
                else if (jitProcess.BackgroundJitAbortedAtMSec == 0)
                    jitProcess.BackGroundJitEvents = null;      // Save some space and ditch the module load events.  

                // Compute module level stats, we do it here because we may not have the IL method name until late. 
                Debug.Assert(jitProcess.moduleStats == null);
                jitProcess.moduleStats = new SortedDictionary<string, JitInfo>(StringComparer.OrdinalIgnoreCase);
                foreach (var _event in jitProcess.events)
                {
                    if (_event.ModuleILPath != null)
                        jitProcess.moduleStats.GetOrCreate(_event.ModuleILPath).Update(_event);
                }
            }
            return perProc;
        }

        public int ProcessID { get; set; }
        public string ProcessName { get; set; }
        public string CommandLine { get; set; }
        public bool Interesting { get { return Total.Count > 0 || isClr4; } }
        public JitInfo Total = new JitInfo();
        public IEnumerable<JitEvent> Events { get { return events; } }

        public virtual void ToHtml(TextWriter writer, string fileName)
        {
            var usersGuideFile = ClrStatsUsersGuide.WriteUsersGuide(fileName);

            writer.WriteLine("<H3><A Name=\"Stats_{0}\"><font color=\"blue\">JIT Stats for for Process {1,5}: {2}</font><A></H3>", ProcessID, ProcessID, ProcessName);
            writer.WriteLine("<UL>");
            if (!isClr4)
                writer.WriteLine("<LI><Font color=\"red\">Warning: Could not confirm that a V4.0 CLR was loaded.  JitTime or ILSize can only be computed for V4.0 runtimes.  Otherwise their value will appear as 0.</font></LI>");
            if (!string.IsNullOrEmpty(CommandLine))
                writer.WriteLine("<LI>CommandLine: {0}</LI>", CommandLine);
            writer.WriteLine("<LI>Process CPU Time: {0:n0} msec</LI>", ProcessCpuTimeMsec);
            if (BackgroundJitThread != 0 || BackgroundJitAbortedAtMSec != 0)
            {
                writer.WriteLine("<LI>This process uses Background JIT compilation (System.Runtime.ProfileOptimize)</LI>");
                writer.WriteLine(" <UL>");

                if (recordedModules.Count == 0)
                {
                    writer.WriteLine(" <LI><font color=\"red\">This trace is missing some background JIT events, which could result in incorrect information in the background JIT blocking reason column.</font></LI>");
                    writer.WriteLine(" <LI><font color=\"red\">Re-collect the trace enabling \"Background JIT\" events on the collection menu to fix this.</font></LI>");
                }

                if (BackgroundJitAbortedAtMSec != 0)
                {
                    writer.WriteLine("  <LI><font color=\"red\">WARNING: Background JIT aborted at {0:n3} Msec</font></LI>", BackgroundJitAbortedAtMSec);
                    writer.WriteLine("  <LI>The last assembly before the abort was '{0}' loaded {1} at {2:n3}</LI>",
                        LastAssemblyLoadNameBeforeAbort, LastAssemblyLoadBeforeAbortSuccessful ? "successfully" : "unsuccessfully",
                        LastAssemblyLoadBeforeAbortMSec);
                }

                if (BackgroundJitThread != 0)
                {
                    var foregroundJitTimeMSec = Total.JitTimeMSec - BackGround.JitTimeMSec;
                    writer.WriteLine("  <LI><strong>JIT time NOT moved to background thread</strong> : {0:n0}</LI> ({1:n1}%)",
                          foregroundJitTimeMSec, foregroundJitTimeMSec * 100.0 / Total.JitTimeMSec);

                    var foregroundCount = Total.Count - BackGround.Count;
                    writer.WriteLine("  <LI>Methods Not moved to background thread: {0:n0} ({1:n1}%)</LI>",
                        foregroundCount, foregroundCount * 100.0 / Total.Count);

                    writer.WriteLine("  <LI>Methods Background JITTed : {0:n0} ({1:n1}%)</LI>",
                        BackGround.Count, BackGround.Count * 100.0 / Total.Count);

                    writer.WriteLine("  <LI>MSec Background JITTing : {0:n0}</LI>", BackGround.JitTimeMSec);
                    writer.WriteLine("  <LI>Background JIT Thread : {0}</LI>", BackgroundJitThread);
                }
                writer.WriteLine("  <LI> <A HREF=\"command:excelBackgroundDiag/{0}\">View Raw Background Jit Diagnostics</A></LI></UL>", ProcessID);
                writer.WriteLine("  <LI> See <A HREF=\"{0}#UnderstandingBackgroundJIT\">Guide to Background JIT</A></LI> for more on background JIT", usersGuideFile);
                writer.WriteLine(" </UL>");
            }
            writer.WriteLine("<LI>Total Number of JIT compiled methods : {0:n0}</LI>", Total.Count);
            writer.WriteLine("<LI>Total MSec JIT compiling : {0:n0}</LI>", Total.JitTimeMSec);

            if (Total.JitTimeMSec != 0)
                writer.WriteLine("<LI>JIT compilation time as a percentage of total process CPU time : {0:f1}%</LI>", Total.JitTimeMSec * 100.0 / ProcessCpuTimeMsec);
            writer.WriteLine("<LI><A HREF=\"#Events_{0}\">Individual JIT Events</A></LI>", ProcessID);

            writer.WriteLine("<UL><LI> <A HREF=\"command:excel/{0}\">View in Excel</A></LI></UL>", ProcessID);
            writer.WriteLine("<LI> <A HREF=\"{0}#UnderstandingJITPerf\">JIT Perf Users Guide</A></LI>", usersGuideFile);
            writer.WriteLine("</UL>");

            if (BackgroundJitThread == 0)
            {
                if (BackgroundJITEventsOn)
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
                        "</P>");
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
            List<string> moduleNames = new List<string>(moduleStats.Keys);
            moduleNames.Sort(delegate(string x, string y)
            {
                double diff = moduleStats[y].JitTimeMSec - moduleStats[x].JitTimeMSec;
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
                "TOTAL", Total.JitTimeMSec, Total.Count, Total.ILSize, Total.NativeSize);
            foreach (string moduleName in moduleNames)
            {
                JitInfo info = moduleStats[moduleName];
                writer.WriteLine("<TR><TD Align=\"Center\">{0}</TD><TD Align=\"Center\">{1:n1}</TD><TD Align=\"Center\">{2:n0}</TD><TD Align=\"Center\">{3:n0}</TD><TD Align=\"Center\">{4:n0}</TD></TR>",
                    moduleName.Length == 0 ? "&lt;UNKNOWN&gt;" : moduleName, info.JitTimeMSec, info.Count, info.ILSize, info.NativeSize);
            }
            writer.WriteLine("</Table>");
            writer.WriteLine("</Center>");

            bool backgroundJitEnabled = BackgroundJitThread != 0;

            writer.WriteLine("<HR/>");
            writer.WriteLine("<H4><A Name=\"Events_{0}\">Individual JIT Events for Process {1,5}: {2}<A></H4>", ProcessID, ProcessID, ProcessName);
            writer.WriteLine("<Center>");
            writer.WriteLine("<Table Border=\"1\">");
            writer.Write("<TR><TH>Start (msec)</TH><TH>JitTime</BR>msec</TH><TH>IL Size</TH><TH>Native Size</TH><TH>Method Name</TH>" +
                "<TH Title=\"Is Jit compilation occuring in the background (BG) or not (JIT).\">BG</TH><TH>Module</TH>");
            if (backgroundJitEnabled)
            {
                writer.Write("<TH Title=\"How far ahead of the method usage was relative to the background JIT operation.\">Distance Ahead</TH><TH Title=\"Why the method was not JITTed in the background.\">Background JIT Blocking Reason</TH>");
            }
            writer.WriteLine("</TR>");
            foreach (JitEvent _event in events)
            {
                writer.Write("<TR><TD Align=\"Center\">{0:n3}</TD><TD Align=\"Center\">{1:n1}</TD><TD Align=\"Center\">{2:n0}</TD><TD Align=\"Center\">{3:n0}</TD><TD Align=Left>{4}</TD><TD Align=\"Center\">{5}</TD><TD Align=\"Center\">{6}</TD>",
                    _event.StartTimeMSec, _event.JitTimeMSec, _event.ILSize, _event.NativeSize, _event.MethodName ?? "&nbsp;", (_event.IsBackGround ? "BG" : "JIT"),
                    _event.ModuleILPath.Length != 0 ? Path.GetFileName(_event.ModuleILPath) : "&lt;UNKNOWN&gt;");
                if (backgroundJitEnabled)
                {
                    writer.Write("<TD Align=\"Center\">{0:n3}</TD><TD Align=\"Left\">{1}</TD>",
                        _event.DistanceAhead, _event.IsBackGround ? "Not blocked" : _event.BlockedReason);
                }
                writer.WriteLine("</TR>");
            }
            writer.WriteLine("</Table>");
            writer.WriteLine("</Center>");
            writer.WriteLine("<HR/><HR/><BR/><BR/>");
        }
        public void ToCsv(string filePath)
        {
            var listSeparator = Thread.CurrentThread.CurrentCulture.TextInfo.ListSeparator;
            using (var writer = File.CreateText(filePath))
            {
                writer.WriteLine("Start MSec{0}JitTime MSec{0}ThreadID{0}IL Size{0}Native Size{0}MethodName{0}BG{0}Module{0}DistanceAhead{0}BlockedReason", listSeparator);
                for (int i = 0; i < events.Count; i++)
                {
                    var _event = events[i];
                    var csvMethodName = _event.MethodName.Replace(",", " ");    // Insure there are no , in the name 
                    writer.WriteLine("{1:f3}{0}{2:f3}{0}{3}{0}{4}{0}{5}{0}{6}{0}{7}{0}{8}{0}{9}{0}{10}", listSeparator,
                        _event.StartTimeMSec, _event.JitTimeMSec, _event.ThreadID, _event.ILSize,
                        _event.NativeSize, csvMethodName, (_event.IsBackGround ? "BG" : "JIT"), _event.ModuleILPath, _event.DistanceAhead, _event.BlockedReason);
                }
            }
        }

        /// <summary>
        /// Write data about background JIT activities to 'outputCsvFilePath'
        /// </summary>
        public void BackgroundDiagCsv(string outputCsvFilePath)
        {
            var listSeparator = Thread.CurrentThread.CurrentCulture.TextInfo.ListSeparator;
            using (var writer = File.CreateText(outputCsvFilePath))
            {
                writer.WriteLine("MSec{0}Command{0}Arg1{0}Arg2{0}Arg3{0}Arg4{0}", listSeparator);
                for (int i = 0; i < BackGroundJitEvents.Count; i++)
                {
                    var event_ = this.BackGroundJitEvents[i];
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

        public virtual void ToXml(TextWriter writer, string indent)
        {
            // TODO pay attention to indent;
            writer.Write(" <JitProcess Process=\"{0}\" ProcessID=\"{1}\" JitTimeMSec=\"{2:n3}\" Count=\"{3}\" ILSize=\"{4}\" NativeSize=\"{5}\"",
                ProcessName, ProcessID, Total.JitTimeMSec, Total.Count, Total.ILSize, Total.NativeSize);
            if (ProcessCpuTimeMsec != 0)
                writer.Write(" ProcessCpuTimeMsec=\"{0}\"", ProcessCpuTimeMsec);
            if (!string.IsNullOrEmpty(CommandLine))
                writer.Write(" CommandLine=\"{0}\"", XmlUtilities.XmlEscape(CommandLine, false));
            writer.WriteLine(">");
            writer.WriteLine("  <JitEvents>");
            foreach (JitEvent _event in events)
                _event.ToXml(writer);
            writer.WriteLine("  </JitEvents>");

            writer.WriteLine(" <ModuleStats Count=\"{0}\" TotalCount=\"{1}\" TotalJitTimeMSec=\"{2:n3}\" TotalILSize=\"{3}\" TotalNativeSize=\"{4}\">",
                moduleStats.Count, Total.Count, Total.JitTimeMSec, Total.ILSize, Total.NativeSize);

            // Sort the module list by Jit Time;
            List<string> moduleNames = new List<string>(moduleStats.Keys);
            moduleNames.Sort(delegate(string x, string y)
            {
                double diff = moduleStats[y].JitTimeMSec - moduleStats[x].JitTimeMSec;
                if (diff > 0)
                    return 1;
                else if (diff < 0)
                    return -1;
                return 0;
            });

            foreach (string moduleName in moduleNames)
            {
                JitInfo info = moduleStats[moduleName];
                writer.Write("<Module");
                writer.Write(" JitTimeMSec="); GCEvent.QuotePadLeft(writer, info.JitTimeMSec.ToString("n3"), 11);
                writer.Write(" Count="); GCEvent.QuotePadLeft(writer, info.Count.ToString(), 7);
                writer.Write(" ILSize="); GCEvent.QuotePadLeft(writer, info.ILSize.ToString(), 9);
                writer.Write(" NativeSize="); GCEvent.QuotePadLeft(writer, info.NativeSize.ToString(), 9);
                writer.Write(" Name=\"{0}\"", moduleName);
                writer.WriteLine("/>");
            }
            writer.WriteLine("  </ModuleStats>");

            writer.WriteLine(" </JitProcess>");
        }
        #region private
        private static void MethodComplete(ProcessLookup<JitProcess> perProc, TraceEvent data, int methodNativeSize, long moduleID, string methodName, long methodID)
        {
            JitProcess stats = perProc[data];
            JitEvent _event = stats.FindIncompleteJitEventOnThread(data.ThreadID);
            if (_event == null)
            {
                // We don't have JIT start, do the best we can.  
                _event = stats.LogJitStart(data, methodName, 0, moduleID, methodID);
                if (stats.isClr4)
                {
                    Console.WriteLine("Warning: MethodComplete at {0:n3} process {1} thread {2} without JIT Start, assuming 0 JIT time",
                        data.TimeStampRelativeMSec, data.ProcessName, data.ThreadID);
                }
                else if (!stats.warnedUser)
                {
                    // Console.WriteLine("Warning: Process {0} ({1}) is running a V2.0 CLR, no JIT Start events available, so JIT times will all be 0.", stats.ProcessName, stats.ProcessID);
                    stats.warnedUser = true;
                }
            }
            _event.NativeSize = methodNativeSize;
            _event.JitTimeMSec = data.TimeStampRelativeMSec - _event.StartTimeMSec;
            stats.Total.Update(_event);
        }

        private JitEvent LogJitStart(TraceEvent data, string methodName, int ILSize, long moduleID, long methodID)
        {
            JitEvent _event = new JitEvent();
            _event.StartTimeMSec = data.TimeStampRelativeMSec;
            _event.ILSize = ILSize;
            _event.MethodName = methodName;
            _event.ThreadID = data.ThreadID;
            _event.m_jitProcess = this;
            _event.m_ModuleILID = moduleID;
            _event.m_MethodILID = methodID;
            events.Add(_event);

            if (BackgroundJitThread == _event.ThreadID)
            {
                MethodKey key = new MethodKey(moduleID, methodID);
                backgroundJitEvents[key] = _event;
            }
            else if (BackgroundJitThread != 0)
            {
                // Get the module name.
                if (moduleNamesFromID.ContainsKey(moduleID))
                {
                    string moduleName = moduleNamesFromID[moduleID];
                    if (!string.IsNullOrEmpty(moduleName))
                    {
                        moduleName = System.IO.Path.GetFileNameWithoutExtension(moduleName);
                    }

                    // Check to see if this module is in the profile.
                    if (!recordedModules.Contains(moduleName))
                    {
                        // Mark the blocking reason that the module is not in the profile, so we'd never background JIT it.
                        _event.BlockedReason = "Module not recorded";
                    }
                    else
                    {
                        _event.BlockedReason = LastBlockedReason;
                    }
                }
                else
                {
                    _event.BlockedReason = LastBlockedReason;
                }
            }
            else
            {
                _event.BlockedReason = LastBlockedReason;
            }

            return _event;
        }

        private static string GetMethodName(MethodJittingStartedTraceData data)
        {
            int parenIdx = data.MethodSignature.IndexOf('(');
            if (parenIdx < 0)
                parenIdx = data.MethodSignature.Length;

            return data.MethodNamespace + "." + data.MethodName + data.MethodSignature.Substring(parenIdx);
        }
        private static string GetMethodName(MethodLoadUnloadVerboseTraceData data)
        {
            int parenIdx = data.MethodSignature.IndexOf('(');
            if (parenIdx < 0)
                parenIdx = data.MethodSignature.Length;

            return data.MethodNamespace + "." + data.MethodName + data.MethodSignature.Substring(parenIdx);
        }

        public virtual void Init(TraceEvent data)
        {
            ProcessID = data.ProcessID;
            ProcessName = data.ProcessName;
        }
        private JitEvent FindIncompleteJitEventOnThread(int threadID)
        {
            for (int i = events.Count - 1; 0 <= i; --i)
            {
                JitEvent ret = events[i];
                if (ret.ThreadID == threadID)
                {
                    // This is a completed JIT event, not what we are looking for. 
                    if (ret.NativeSize > 0 || ret.JitTimeMSec > 0)
                        return null;
                    return ret;
                }
            }
            return null;
        }
        public override string ToString()
        {
            StringWriter sw = new StringWriter();
            ToXml(sw, "");
            return sw.ToString();
        }

        private string LastBlockedReason = "BG JIT not enabled";

        private bool isClr4;
        private bool warnedUser;
        private int ProcessCpuTimeMsec;     // Total CPU time used in process (approximate)

        // These are for background JIT
        private double BackgroundJitAbortedAtMSec;
        private string LastAssemblyLoadNameBeforeAbort;
        private double LastAssemblyLoadBeforeAbortMSec;
        private bool LastAssemblyLoadBeforeAbortSuccessful;

        private int BackgroundJitThread;
        private bool BackgroundJITEventsOn;
        private JitInfo BackGround;
        private List<TraceEvent> BackGroundJitEvents;

        bool playbackAborted = false;
        List<JitEvent> events = new List<JitEvent>();
        Dictionary<MethodKey, JitEvent> backgroundJitEvents = new Dictionary<MethodKey, JitEvent>();
        HashSet<string> recordedModules = new HashSet<string>();
        internal Dictionary<long, string> moduleNamesFromID = new Dictionary<long, string>();
        SortedDictionary<string, JitInfo> moduleStats;
        #endregion

        #region IComparable<JitProcess> Members
        public int CompareTo(JitProcess other)
        {
            // Sort highest to lowests on JIT time
            return -Total.JitTimeMSec.CompareTo(other.Total.JitTimeMSec);
        }
        #endregion
    }

    /// <summary>
    /// Uniquely represents a method within a process.
    /// Used as a lookup key for data structures.
    /// </summary>
    internal struct MethodKey
    {
        private long _ModuleId;
        private long _MethodId;

        public MethodKey(
            long moduleId,
            long methodId)
        {
            _ModuleId = moduleId;
            _MethodId = methodId;
        }

        public override bool Equals(object obj)
        {
            if (obj is MethodKey)
            {
                MethodKey otherKey = (MethodKey)obj;
                return ((_ModuleId == otherKey._ModuleId) && (_MethodId == otherKey._MethodId));
            }

            return false;
        }

        public override int GetHashCode()
        {
            return (int)(_ModuleId ^ _MethodId);
        }
    }

    public class DllProcess : ProcessLookupContract, IComparable<DllProcess>
    {
        public static ProcessLookup<DllProcess> Collect(TraceEventDispatcher source, float sampleIntervalMSec)
        {
            var symbols = new SymbolTraceEventParser(source);
            ProcessLookup<DllProcess> perProc = new ProcessLookup<DllProcess>();

            source.Kernel.ProcessGroup += delegate(ProcessTraceData data)
            {
                var proc = perProc[data];
                if (proc.ProcessName == null)
                    proc.ProcessName = data.ProcessName;

                if (proc.CommandLine == null)
                {
                    var commandLine = data.CommandLine;
                    if (commandLine.Length > 0)
                        proc.CommandLine = commandLine;
                }
                if (proc.StartTimeRelativeMSec == 0 && data.Opcode == TraceEventOpcode.Start)
                    proc.StartTimeRelativeMSec = data.TimeStampRelativeMSec;
                if (proc.EndTimeRelativeMSec == 0 && data.Opcode == TraceEventOpcode.Stop)
                    proc.EndTimeRelativeMSec = data.TimeStampRelativeMSec;
                if (proc.ParentID == 0)
                    proc.ParentID = data.ParentID;
            };
            source.Clr.RuntimeStart += delegate(RuntimeInformationTraceData data)
            {
                DllProcess proc = perProc[data];
                if (proc.CommandLine == null)
                {
                    var commandLine = data.CommandLine;
                    if (commandLine.Length > 0)
                        proc.CommandLine = commandLine;
                }
            };
            FileVersionTraceData lastFileVersionInfo = null;
            DbgIDRSDSTraceData lastDbgInfo = null;
            source.Kernel.ImageGroup += delegate(ImageLoadTraceData data)
            {
                DllProcess proc = perProc[data];
                DllInfo dllInfo = proc.GetDllInfo(data.ImageBase, data.ImageSize, data.TimeStampRelativeMSec);
                if (dllInfo.Path == null)
                    dllInfo.Path = data.FileName;
                if (dllInfo.Size == 0)
                    dllInfo.Size = data.ImageSize;
                if (data.Opcode == TraceEventOpcode.Start && dllInfo.LoadTimeRelativeMSec == 0)
                    dllInfo.LoadTimeRelativeMSec = data.TimeStampRelativeMSec;
                if (data.Opcode == TraceEventOpcode.Stop && dllInfo.UnloadTimeRelativeMSec == 0)
                    dllInfo.UnloadTimeRelativeMSec = data.TimeStampRelativeMSec;
                if (lastFileVersionInfo != null && lastFileVersionInfo.TimeStampRelativeMSec == data.TimeStampRelativeMSec)
                {
                    dllInfo.FileVersion = lastFileVersionInfo.FileVersion;
                }
                if (lastDbgInfo != null && lastDbgInfo.TimeStampRelativeMSec == data.TimeStampRelativeMSec)
                {
                    dllInfo.PdbSimpleName = lastDbgInfo.PdbFileName;
                    dllInfo.PdbGuid = lastDbgInfo.GuidSig;
                    dllInfo.PdbAge = lastDbgInfo.Age;
                }
            };
            symbols.ImageIDFileVersion += delegate(FileVersionTraceData data)
            {
                lastFileVersionInfo = (FileVersionTraceData)data.Clone();
            };

            symbols.ImageIDDbgID_RSDS += delegate(DbgIDRSDSTraceData data)
            {
                lastDbgInfo = (DbgIDRSDSTraceData)data.Clone();
            };
            Address lastPerfInfoSample = 0;
            source.Kernel.PerfInfoSample += delegate(SampledProfileTraceData data)
            {
                DllProcess proc = perProc[data];
                lastPerfInfoSample = data.InstructionPointer;
                proc.AddSample(data.InstructionPointer, data.TimeStampRelativeMSec, sampleIntervalMSec, true, true);
            };

            source.Kernel.AddCallbackForEvents(delegate(DiskIOTraceData data)
            {
                DllProcess proc = perProc[data];

                FileInfo fileInfo = proc.GetFileInfo(data.FileKey);
                fileInfo.Path = data.FileName;
                // Do I care about Read vs Write?
                fileInfo.DiskIOMB += data.TransferSize / 1000000.0F;
                proc.DiskIOMB += data.TransferSize / 1000000.0F;
                fileInfo.DiskIOCount++;
                proc.DiskIOCount++;
                fileInfo.DiskIOMSec += (float)data.ElapsedTimeMSec;
                proc.DiskIOMSec += (float)data.ElapsedTimeMSec;
            });

            source.Kernel.StackWalkStack += delegate(StackWalkStackTraceData data)
            {
                DllProcess proc = perProc[data];
                if (data.FrameCount > 0)
                {
                    bool isForCPUSample = (lastPerfInfoSample == data.InstructionPointer(0));
                    for (int i = 1; i < data.FrameCount; i++)
                        proc.AddSample(data.InstructionPointer(i), data.TimeStampRelativeMSec, sampleIntervalMSec, false, isForCPUSample);
                }
            };
            source.Kernel.MemoryHardFault += delegate(MemoryHardFaultTraceData data)
            {
                DllProcess proc = perProc[data];
                DllInfo dllInfo = proc.ProbeDllInfo(data.VirtualAddress, data.TimeStampRelativeMSec);

                proc.PageFaults++;
                dllInfo.PageFaults++;
            };

            source.Clr.LoaderModuleLoad += delegate(ModuleLoadUnloadTraceData data)
            {
                DllProcess proc = perProc[data];
                proc.CLRLoads.Add((ModuleLoadUnloadTraceData)data.Clone());
            };

            source.Process();

            // At this point, all of our FileInfos probably don't have names because 
            // we did not know the name until very late in the trace.   However now that
            // the trace is done we can fill them in.  

            var byName = new Dictionary<string, FileInfo>();
            foreach (var proc in perProc)
            {
                byName.Clear();
                foreach (var fileInfo in proc.Files.Values)
                {
                    // See if we have a real name for the file object
                    if (fileInfo.Path.Length == 0)
                        fileInfo.Path = source.Kernel.FileIDToFileName(fileInfo.FileKey);

                    if (fileInfo.Path.Length > 0)
                        byName[fileInfo.Path] = fileInfo;
                }
                // Link the DLL information to the file information. 
                foreach (var image in proc.Images)
                    byName.TryGetValue(image.Path, out image.FileInfo);
            }
            return perProc;
        }

        public void Init(TraceEvent data)
        {
            ProcessID = data.ProcessID;
            CLRLoads = new List<ModuleLoadUnloadTraceData>();
            Files = new Dictionary<Address, FileInfo>();
            OutsideImages = new DllInfo() { Path = "<<NO IMAGE>>" };
        }
        public void ToXml(TextWriter writer, string indent)
        {
            writer.Write("{0}<Process Name=\"{1}\" ID=\"{2}\"", indent, ProcessName, ProcessID);
            if (CpuMSec > 0)
                writer.Write(" CpuMSec=\"{0}\"", CpuMSec);
            writer.WriteLine(" ParentID=\"{0}\"", ParentID);
            if (!string.IsNullOrEmpty(CommandLine))
                writer.WriteLine("{0} CommandLine=\"{1}\"", indent, XmlUtilities.XmlEscape(CommandLine));

            if (StartTimeRelativeMSec != 0)
                writer.Write("{0} StartTimeRelativeMSec=\"{1:n3}\"", indent, StartTimeRelativeMSec);
            if (EndTimeRelativeMSec != 0)
            {
                writer.Write(" EndTimeRelativeMSec=\"{0:n3}\"", EndTimeRelativeMSec);
                writer.Write(" DurationMSec=\"{0:n3}\"", DurationMSec);
            }
            writer.WriteLine(">");


            if (Images.Count > 0)
            {
                writer.Write("{0}  <ImageLoads Count=\"{0}\"", indent, Images.Count);
                if (CpuMSec > 0)
                    writer.Write(" CpuMSec=\"{0}\"", CpuMSec);
                if (PageFaults > 0)
                    writer.Write(" PageFaults=\"{1}\"", indent, PageFaults);
                writer.WriteLine(">");
                List<DllInfo> sortedImages = new List<DllInfo>();
                foreach (var image in Images)
                    sortedImages.Add(image);
                sortedImages.Sort((x, y) => x.CPUSamplesExclusive.CompareTo(y.CPUSamplesExclusive));

                for (int i = sortedImages.Count - 1; i >= 0; --i)
                    sortedImages[i].ToXml(writer, indent + "    ");
                writer.WriteLine("{0}   </ImageLoads>", indent);
            }

            if (Files.Count > 0)
            {
                writer.Write("{0}  <Files", indent);
                writer.Write(" DiskIOMSec=\"{0:n3}\"", DiskIOMSec);
                writer.Write(" DiskIOMB=\"{0:n3}\"", DiskIOMB);
                writer.Write(" DiskIOCount=\"{0}\"", DiskIOCount);
                writer.WriteLine(">");

                List<FileInfo> sortedFiles = new List<FileInfo>(Files.Values);
                sortedFiles.Sort((x, y) => x.DiskIOMSec.CompareTo(y.DiskIOMSec));
                for (int i = sortedFiles.Count - 1; i >= 0; --i)
                    sortedFiles[i].ToXml(writer, indent + "    ");
                writer.WriteLine("{0}  </Files>", indent);
            }

            if (CLRLoads.Count > 0)
            {
                writer.WriteLine("{0}  <CLRLoads Count=\"{1}\">", indent, CLRLoads.Count);
                foreach (var clrModule in CLRLoads)
                    writer.WriteLine("{0}    <CLRLoad ILPath=\"{1}\"/>", indent, clrModule.ModuleILPath);
                writer.WriteLine("{0}  </CLRLoads>", indent);
            }
            writer.WriteLine(" </Process>");
        }
        public void ToHtml(TextWriter writer, string fileName)
        {
            throw new NotImplementedException();
        }

        public int ProcessID { get; set; }
        public string ProcessName { get; set; }
        public string CommandLine { get; set; }
        public bool Interesting { get { return true; } }

        public int ParentID;
        public float CpuMSec;

        public double StartTimeRelativeMSec;
        public double EndTimeRelativeMSec;
        public double DurationMSec { get { return EndTimeRelativeMSec - StartTimeRelativeMSec; } }

        public int PageFaults;

        public float DiskIOMB;
        public int DiskIOCount;
        public float DiskIOMSec;

        public GrowableArray<DllInfo> Images;         // This is sorted by imageBase.  
        public Dictionary<Address, FileInfo> Files;
        public DllInfo OutsideImages;
        public List<ModuleLoadUnloadTraceData> CLRLoads;
        public int CompareTo(DllProcess other)
        {
            var ret = -CpuMSec.CompareTo(other.CpuMSec);
            if (ret != 0)
                return ret;
            return ret;
        }

        #region private

        /// <summary>
        /// Called every time we have a address.  This routine finds the DLL it lives in and increments its stats.
        /// </summary>
        /// <param name="address">The address to lookup</param>
        /// <param name="timeStampMSec">Timestamp in MSec from start of trace of the event associated with the address</param>
        /// <param name="sampleIntervalMSec">Number of msec between each CPU sample</param>
        /// <param name="isExclusiveSample">Is this the current CPU EIP?</param>
        /// <param name="isForCPUSample">Is this a CPU profile sample?  (coudl be a CSwitch ...)</param>
        private void AddSample(Address address, double timeStampMSec, float sampleIntervalMSec, bool isExclusiveSample, bool isForCPUSample)
        {
            DllInfo dllInfo = ProbeDllInfo(address, timeStampMSec);
            if (isForCPUSample)
            {
                if (isExclusiveSample)
                    dllInfo.CPUSamplesExclusive++;
                dllInfo.CPUSamplesInclusive++;
                CpuMSec += sampleIntervalMSec;
            }
            dllInfo.AnyStack++;
        }

        GrowableArray<DllInfo>.Comparison<Address> dllInfoCompare = delegate(Address key, DllInfo info)
        {
            if (key < info.ImageBase)
                return -1;
            if (key > info.ImageBase)
                return 1;
            return 0;
        };
        /// <summary>
        /// Get the DllInfo associated with 'address' at time 'timeStampMsec' Unlike GetDllInfo
        /// if an existing DllInfo is now found, it will return an 'other' DLL.  
        /// </summary>
        private DllInfo ProbeDllInfo(Address interiorAddress, double timeStampMsec)
        {
            int index;
            var found = Images.BinarySearch(interiorAddress, out index, dllInfoCompare);
            if (index < 0)
                return OutsideImages;
            var ret = Images[index];
            if ((Address)((long)ret.ImageBase + ret.Size) <= interiorAddress)
                return OutsideImages;
            return ret;
        }

        /// <summary>
        /// Gets a DllInfo that tracks 'address' at time 'timeStampMsec'   
        /// It alwasy returns something, creating a new entry as needed.  
        /// </summary>
        private DllInfo GetDllInfo(Address address, int size, double timeStampMsec)
        {
            int index;
            var found = Images.BinarySearch(address, out index, dllInfoCompare);
            if (found)
                return Images[index];

            var ret = new DllInfo() { ImageBase = address, Size = size };
            Images.Insert(index + 1, ret);
            return ret;
        }

        /// <summary>
        /// Gets a fileInfo given a fileKey.  Creates a new one if necessary. 
        /// </summary>
        /// <param name="fileKey"></param>
        /// <returns></returns>
        private FileInfo GetFileInfo(Address fileKey)
        {
            FileInfo fileInfo;
            if (!Files.TryGetValue(fileKey, out fileInfo))
            {
                fileInfo = new FileInfo();
                fileInfo.FileKey = fileKey;
                Files.Add(fileKey, fileInfo);
            }
            return fileInfo;
        }
        #endregion
    }

    public class FileInfo
    {
        public void ToXml(TextWriter writer, string indent)
        {
            writer.Write("{0}<File", indent);
            writer.Write(" DiskIOMSec=\"{0:n3}\"", DiskIOMSec);
            writer.Write(" DiskIOMB=\"{0:n3}\"", DiskIOMB);
            writer.Write(" DiskIOCount=\"{0}\"", DiskIOCount);
            writer.Write(" Path=\"{0}\"", XmlUtilities.XmlEscape(Path));
            writer.WriteLine("/>");
        }

        public string Path;
        public float DiskIOMB;
        public int DiskIOCount;
        public float DiskIOMSec;
        public DllInfo DllInfo;
        public Address FileKey;      // ID from the OS's point of view. 
    }

    public class DllInfo
    {
        public double LoadTimeRelativeMSec;
        public double UnloadTimeRelativeMSec;

        public Address ImageBase;
        public int Size;

        public string Path;
        public string FileVersion;
        public FileInfo FileInfo;

        public string PdbSimpleName;
        public Guid PdbGuid;
        public int PdbAge;

        public int PageFaults;

        public int CPUSamplesExclusive;
        public int CPUSamplesInclusive;

        public int AnyStack;    // This DLL was in any stack that was collected.  

        public void ToXml(TextWriter writer, string indent)
        {
            writer.Write("{0}<Image Name=\"{1}\"", indent, XmlUtilities.XmlEscape(System.IO.Path.GetFileNameWithoutExtension(Path)));
            if (CPUSamplesInclusive != 0)
            {
                writer.Write(" CPUExc=\"{0}\"", CPUSamplesExclusive);
                writer.Write(" CPUInc=\"{0}\"", CPUSamplesInclusive);
            }
            if (PageFaults != 0)
                writer.Write(" PageFaults=\"{0}\"", PageFaults);
            writer.WriteLine("{0} AnyStack=\"{1}\"", indent, AnyStack);

            writer.Write("{0} ImageBase=\"0x{1:x}\"", indent, ImageBase);
            writer.WriteLine(" Size=\"0x{0:x}\"", Size);
            if (FileVersion != null)
                writer.WriteLine("{0} FileVersion=\"{1}\"", indent, XmlUtilities.XmlEscape(FileVersion));
            if (LoadTimeRelativeMSec != 0)
                writer.WriteLine("{0} LoadTimeRelativeMSec=\"{1:n3}\"", indent, LoadTimeRelativeMSec);
            if (UnloadTimeRelativeMSec != 0)
                writer.WriteLine("{0} UnloadTimeRelativeMSec=\"{1:n3}\"", indent, UnloadTimeRelativeMSec);

            if (PdbSimpleName != null)
            {
                writer.Write(indent);
                writer.Write(" PdbSimpleName=\"{0}\"", PdbSimpleName);
                writer.Write(" PdbGuid=\"{0}\"", PdbGuid);
                writer.WriteLine(" PdbAge=\"{0}\"", PdbAge);
            }

            writer.WriteLine(">");

            if (FileInfo != null && FileInfo.DiskIOCount != 0)
                FileInfo.ToXml(writer, indent + "  ");
            writer.WriteLine("{0}</Image>", indent);
        }
    }

    /************************************************************************/
    /*  Reusable stuff */

    static class SortedDictionaryExtentions
    {
        // As it's name implies, it either fetches something from a dictionary
        // or creates it if it does not already exist (using he default
        // constructor)
        public static V GetOrCreate<K, V>(this SortedDictionary<K, V> dict, K key) where V : new()
        {
            V value;
            if (!dict.TryGetValue(key, out value))
            {
                value = new V();
                dict.Add(key, value);
            }
            return value;
        }
    }

    /// <summary>
    /// ProcessLookup is a generic lookup by process.  
    /// </summary>
    public class ProcessLookup<T> : IEnumerable<T> where T : ProcessLookupContract, new()
    {
        /// <summary>
        /// Given an event, find the 'T' that cooresponds to that the process 
        /// associated with that event.  
        /// </summary>
        public T this[TraceEvent data]
        {
            get
            {
                T ret;
                if (!perProc.TryGetValue(data.ProcessID, out ret))
                {
                    ret = new T();
                    ret.Init(data);
                    perProc.Add(data.ProcessID, ret);
                }
                return ret;
            }
        }
        public T TryGet(TraceEvent data)
        {
            T ret;
            perProc.TryGetValue(data.ProcessID, out ret);
            return ret;
        }
        public T Replace(TraceEvent data)
        {
            perProc.Remove(data.ProcessID);
            T ret = new T();
            ret.Init(data);
            perProc.Add(data.ProcessID, ret);
            return this[data];
        }
        public void ToXml(TextWriter writer, string tag)
        {
            List<T> sortedProcs = new List<T>(perProc.Values);
            sortedProcs.Sort();

            int count = 0;
            for (int i = 0; i < sortedProcs.Count; i++)
                if (sortedProcs[count].Interesting)
                    count++;

            writer.WriteLine("<{0} Count=\"{0}\">", tag, count);

            for (int i = 0; i < sortedProcs.Count; i++)
            {
                if (sortedProcs[i].Interesting)
                    sortedProcs[i].ToXml(writer, "");
            }
            writer.WriteLine("</{0}>", tag);
        }
        public void ToHtml(TextWriter writer, string fileName, string title, Predicate<T> filter, bool justBody=false)
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
            List<T> sortedProcs = new List<T>(perProc.Values);
            sortedProcs.Sort();

            int count = sortedProcs.Count;
            if (filter != null)
            {
                count = 0;
                foreach (T stats in sortedProcs)
                    if (filter(stats))
                        count++;
                if (count == 0)
                    writer.WriteLine("<p>No processes match filter.</p>");
            }

            if (count > 1)
            {
                writer.WriteLine("<UL>");
                foreach (var data in sortedProcs)
                {
                    if (!data.Interesting)
                        continue;
                    if (filter != null && !filter(data))
                        continue;

                    var id = Shorten(data.CommandLine);
                    if (string.IsNullOrEmpty(id))
                        id = data.ProcessName;

                    writer.WriteLine("<LI><A HREF=\"#Stats_{0}\">Process {0,5}: {1}</A></LI>", data.ProcessID, XmlUtilities.XmlEscape(id));
                }
                writer.WriteLine("</UL>");
                writer.WriteLine("<HR/><HR/><BR/><BR/>");
            }
            foreach (T stats in sortedProcs)
            {
                if (!stats.Interesting)
                    continue;
                if (filter != null && !filter(stats))
                    continue;

                stats.ToHtml(writer, fileName);
            }

            writer.WriteLine("<BR/><BR/><BR/><BR/><BR/><BR/><BR/><BR/><BR/><BR/>");
            if (!justBody)
            {
                writer.WriteLine("</body>");
                writer.WriteLine("</html>");
            }
        }

        public bool TryGetByID(int processID, out T ret)
        {
            return perProc.TryGetValue(processID, out ret);
        }


        public IEnumerator<T> GetEnumerator() { return perProc.Values.GetEnumerator(); }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { throw new NotImplementedException(); }
        public override string ToString()
        {
            StringWriter sw = new StringWriter();
            ToXml(sw, "Processes");
            return sw.ToString();
        }

        #region private
        /// <summary>
        /// Returns a shortened command line
        /// </summary>
        /// <param name="commandLine"></param>
        /// <returns></returns>
        private static string Shorten(string commandLine)
        {
            if (commandLine == null)
                return null;

            // Remove quotes, replacing ' ' with '_'
            commandLine = Regex.Replace(commandLine, "\"(.*?)\"", (m) => m.Groups[1].Value.Replace(' ', '_'));

            // Remove .exe suffixes. 
            commandLine = Regex.Replace(commandLine, ".exe", "");

            // Remove the front part of paths
            commandLine = Regex.Replace(commandLine, @"(\S+)\\(\S*)", (m) => m.Groups[2].Value == "" ? "\\" : m.Groups[2].Value);

            // truncate if necessary. 
            if (commandLine.Length > 80)
                commandLine = commandLine.Substring(0, 80) + "...";
            return commandLine;
        }

        SortedDictionary<int, T> perProc = new SortedDictionary<int, T>();
        #endregion
    }

    /// <summary>
    /// ProcessLookupContract is used by code:ProcessLookup.  The type
    /// parameter needs to implement these functions 
    /// </summary>
    public interface ProcessLookupContract
    {
        /// <summary>
        /// Init is called after a new 'T' is created, to initialize the new instance
        /// </summary>
        void Init(TraceEvent data);
        /// <summary>
        /// Prints the 'T' as XML, to 'writer'
        /// </summary>
        void ToXml(TextWriter writer, string indent);
        void ToHtml(TextWriter writer, string fileName);
        int ProcessID { get; }
        string ProcessName { get; }
        string CommandLine { get; }
        /// <summary>
        /// A process is interesting if it should be included in a machine wide report.   
        /// For managed statistics, unmananaged processes are uninteresting.  
        /// </summary>
        bool Interesting { get; }
    }

    public static class ClrStatsUsersGuide
    {
        public static string WriteUsersGuide(string inputFileName)
        {
            var usersGuideName = Path.ChangeExtension(Path.ChangeExtension(inputFileName, null), "usersGuide.html");
            if (!File.Exists(usersGuideName) || (DateTime.UtcNow - File.GetLastWriteTimeUtc(usersGuideName)).TotalHours > 1)
                File.Copy(Path.Combine(SupportFiles.SupportFileDir, "HtmlReportUsersGuide.htm"), usersGuideName, true);
            return Path.GetFileName(usersGuideName);        // return the relative path
        }
    }

}
