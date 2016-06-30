using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Parsers.ClrPrivate;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Parsers.Symbol;
using Microsoft.Diagnostics.Utilities;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using Microsoft.Diagnostics.Tracing.Stacks;
using PerfView;
using Microsoft.Diagnostics.Symbols;
using Utilities;
using Microsoft.Diagnostics.Tracing.Session;
using Diagnostics.Tracing.StackSources;
using Address = System.UInt64;
using System.Threading.Tasks;
using System.ComponentModel;

// This file resides under PerfView but is only compiled in TraceEventModel
//  longer term this file will be pulled out completely

namespace ClrCap
{
    /// <summary>
    /// Sets up the events to collect and creates a CAP GC stats report.
    /// </summary>
    public class CapReports
    {
        /// <summary>
        /// Creates the .NET CAP Report
        /// </summary>
        /// <param name="etlFile"></param>
        /// <returns></returns>
        public static ClrCap.CAPAnalysis CreateGCCap(string etlFile)
        {
            using (ETWTraceEventSource source = new ETWTraceEventSource(etlFile, TraceEventSourceType.MergeAll))
            {
                using (ETWTraceEventModelSource model = new ETWTraceEventModelSource(source))
                {
                    model.DisableAll().EnableGC();

                    ClrCap.CAPAnalysis report = new ClrCap.CAPAnalysis();

                    SetupCapCollectors(model.Source, report);
                    model.Process();
                    UpdateCommonInfo(etlFile, model.Source, report);

                    // generate the report
                    ClrCap.CAP.GenerateGCCAPReport(model.Processes, report);

                    return report;
                }
            }
        }

        public static ClrCap.JitCapAnalysis CreateJITCap(string etlFile, int methodCount = 20)
        {
            ETLDataFile EtlDataFile = new ETLDataFile(etlFile);
            TraceLog TraceLog = EtlDataFile.TraceLog;
            TraceLogEventSource Source = TraceLog.Events.GetSource();


            using (ETLDataFile etlDataFile = new ETLDataFile(etlFile))
            {
                TraceLog traceLog = etlDataFile.TraceLog;
                TraceLogEventSource source = traceLog.Events.GetSource();

                using (ETWTraceEventModelSource model = new ETWTraceEventModelSource(source))
                {
                    model.DisableAll().EnableSamples().Configure(etlDataFile);

                    ClrCap.JitCapAnalysis report = new ClrCap.JitCapAnalysis();

                    SetupCapCollectors(model.Source, report);
                    model.Process();
                    //UpdateCommonInfo(etlFile, model.Source, report);

                    ClrCap.CAP.GenerateJITCAPReport(model.Processes, report, methodCount);

                    return report;
                }
            }
        }

        private static void SetupCapCollectors(TraceEventDispatcher source, ClrCap.CAPAnalysisBase report)
        {
            KernelTraceEventParser kernel = source.Kernel;

            source.Kernel.SystemConfigCPU += delegate(SystemConfigCPUTraceData data)
            {
                report.MachineInfo.MachineName = data.ComputerName;
                report.MachineInfo.Domain = data.DomainName;
                report.MachineInfo.MemorySizeMb = data.MemSize;
                report.MachineInfo.NumberOfProcessors = data.NumberOfProcessors;
                report.MachineInfo.ProcessorFrequencyMHz = data.MHz;
                report.MachineInfo.HyperThreadingFlag = (int)data.HyperThreadingFlag;
                report.MachineInfo.PageSize = data.PageSize;
            };

            source.Kernel.SysConfigBuildInfo += delegate(BuildInfoTraceData data)
            {
                report.OSInfo.Name = data.ProductName;
                report.OSInfo.Build = data.BuildLab;
            };
        }

        private static void UpdateCommonInfo(string savedEtlFile, TraceEventDispatcher source, ClrCap.CAPAnalysisBase report)
        {
            report.TraceInfo.NumberOfLostEvents = source.EventsLost;
            report.TraceInfo.TraceDurationSeconds = source.SessionDuration.TotalSeconds;
            report.TraceInfo.TraceEnd = source.SessionEndTime;
            report.TraceInfo.TraceStart = source.SessionStartTime;
            report.TraceInfo.FileLocation = Path.GetFullPath(savedEtlFile);
            report.OSInfo.Version = (source.OSVersion != null) ? source.OSVersion.ToString() : "";
            //report.EventStats.PopulateEventCounts(source.Stats);
        }
    }
}
