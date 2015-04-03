using System;
using System.Collections.Generic;
using PerfView.Dialogs;
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
using Graphs;
using PerfView;
using PerfView.GuiUtilities;
using PerfViewModel;
using Microsoft.Diagnostics.Symbols;
using Utilities;
using FastSerialization;
using Microsoft.Diagnostics.Tracing.Session;
using Diagnostics.Tracing.StackSources;
using Address = System.UInt64;
using System.Threading.Tasks;
using System.ComponentModel;

using Stats;
using PerfViewExtensibility;

namespace PerfView.CapStats
{
#if CAP
    /// <summary>
    /// Sets up the events to collect and creates a CAP GC stats report.
    /// </summary>
    public class GcCapCollector
    {
        public ClrCap.CAPAnalysis Report = new ClrCap.CAPAnalysis();

        private TraceEventDispatcher eventDispatcher;
        public GcCapCollector(TraceEventDispatcher eventDispatcher)
        {
            this.eventDispatcher = eventDispatcher;
        }

        internal ProcessLookup<GCProcess> Collect(string etlDataFilePath, TraceLog traceLog)
        {
            CapCollection.SetupCapCollectors(eventDispatcher, Report);
            ProcessLookup<GCProcess> stats = Stats.GCProcess.Collect(eventDispatcher, 1);
            CapCollection.UpdateCommonInfo(etlDataFilePath, traceLog, Report);
            return stats;
        }
    }

    /// <summary>
    /// Sets up the events to collect and creates a CAP JIT stats report.
    /// </summary>
    public class JitCapCollector
    {
        public JitCapCollector(ETLDataFile etlDataFile)
        {
            this.EtlDataFile = etlDataFile;
            this.TraceLog = EtlDataFile.TraceLog;
            this.Source = TraceLog.Events.GetSource();
            this.Report = new ClrCap.JitCapAnalysis();
        }

        public ProcessLookup<JitCapProcess> Collect()
        {
            CapCollection.SetupCapCollectors(Source, Report);
            ProcessLookup<JitCapProcess> stats = JitCapProcess.Collect(this);
            CapCollection.UpdateCommonInfo(EtlDataFile.FilePath, TraceLog, Report);
            return stats;
        }

        public PerfViewExtensibility.ETLDataFile EtlDataFile;
        public TraceLog TraceLog;
        public TraceLogEventSource Source;
        public ClrCap.JitCapAnalysis Report;

        public HashSet<string> ManagedModulePaths
        {
            get
            {
                if (_ManagedModulePaths == null)
                {
                    _ManagedModulePaths = GetManagedModulePaths(TraceLog);
                }
                return _ManagedModulePaths;
            }
        }
            
        public TraceCodeAddress GetManagedMethodOnStack(SampledProfileTraceData se)
        {
            TraceCodeAddress ca = TraceLogExtensions.IntructionPointerCodeAddress(se);
            if (ManagedModulePaths.Contains(ca.ModuleFilePath))
                return ca;

            // We don't have a managed method, so walk to the managed method by skipping calls
            // inside clr or clrjit
            TraceCallStack cs = TraceLogExtensions.CallStack(se);
            while (cs != null)
            {
                ca = cs.CodeAddress;
                if (ca == null)
                    return null;

                if (ManagedModulePaths.Contains(ca.ModuleFilePath))
                    return ca;

                // This is to ensure we calculate time spent in the JIT helpers, for example.
                cs = ca.ModuleName.IndexOf("clr", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        ca.ModuleName.IndexOf("clrjit", StringComparison.OrdinalIgnoreCase) >= 0
                    ? cs.Caller
                    : null;
            }
            return null;
        }


        private static HashSet<string> GetManagedModulePaths(TraceLog traceLog)
        {
            HashSet<string> managedModulePaths = new HashSet<String>();

            var processes = traceLog.Processes;
            foreach (var process in processes)
            {
                TraceLoadedModules modules = process.LoadedModules;
                foreach (TraceLoadedModule module in modules)
                {
                    if (module.FilePath == null)
                    {
                        continue;
                    }
                    if (module.ManagedModule != null || module is TraceManagedModule)
                    {
                        managedModulePaths.Add(module.FilePath);
                    }
                }
            }

            var moduleFiles = traceLog.CodeAddresses.ModuleFiles;
            foreach (var mf in moduleFiles)
            {
                if (mf.FilePath != null && mf.ManagedModule != null)
                {
                    managedModulePaths.Add(mf.FilePath);
                }
            }
            return managedModulePaths;
        }

        private HashSet<String> _ManagedModulePaths;
    }

    class CapCollection
    {
        public static void SetupCapCollectors(TraceEventDispatcher source, ClrCap.CAPAnalysisBase report)
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

        public static void UpdateCommonInfo(string savedEtlFile, TraceLog source, ClrCap.CAPAnalysisBase report)
        {
            report.TraceInfo.NumberOfLostEvents = source.EventsLost;
            report.TraceInfo.TraceDurationSeconds = source.SessionDuration.TotalSeconds;
            report.TraceInfo.TraceEnd = source.SessionEndTime;
            report.TraceInfo.TraceStart = source.SessionStartTime;
            report.TraceInfo.FileLocation = Path.GetFullPath(savedEtlFile);
            report.OSInfo.Version = source.OSVersion.ToString();
            report.EventStats.PopulateEventCounts(source.Stats);
        }
    }

    public class ProcessLookupContractImpl : ProcessLookupContract
    {
        public int ProcessID { get; set; }
        public string ProcessName { get; set; }
        public string CommandLine { get; set; }
        public bool Interesting { get { return true; } }
        public string LastBlockedReason = "ProcessLookupContractImpl";
        public virtual void ToHtml(TextWriter writer, string fileName)
        {
            return;
        }
        public virtual void ToXml(TextWriter writer, string fileName)
        {
            return;
        }
        public virtual void Init(TraceEvent data)
        {
            ProcessID = data.ProcessID;
            ProcessName = data.ProcessName;
        }
    }

    public class JitCapProcess : ProcessLookupContractImpl, IComparable<JitCapProcess>
    {
        public HashSet<string> SymbolsMissing = new HashSet<string>();
        public HashSet<string> SymbolsLookedUp = new HashSet<string>();
        public Dictionary<string, int> MethodCounts = new Dictionary<string, int>();
        public int ProcessCpuTimeMsec;

        public static ProcessLookup<JitCapProcess> Collect(JitCapCollector collector)
        {
            TraceEventDispatcher source = collector.Source;

            ProcessLookup<JitCapProcess> perProc = new ProcessLookup<JitCapProcess>();

            source.Kernel.PerfInfoSample += delegate(SampledProfileTraceData data)
            {
                JitCapProcess stats = perProc[data];
                if (stats != null)
                {
                    stats.ProcessCpuTimeMsec++;
                    string name = stats.GetSampledMethodName(collector, data);
                    stats.UpdateMethodCounts(name);
                }
            };
            source.Process();

            return perProc;
        }

        public int CompareTo(JitCapProcess other)
        {
            return ProcessID.CompareTo(other.ProcessID);
        }

        private string GetSampledMethodName(JitCapCollector collector, SampledProfileTraceData data)
        {
            TraceCodeAddress ca = collector.GetManagedMethodOnStack(data);
            if (ca == null)
            {
                return null;
            }

            if (String.IsNullOrEmpty(ca.ModuleName))
            {
                return null;
            }

            // Lookup symbols, if not already looked up.
            if (!SymbolsLookedUp.Contains(ca.ModuleName))
            {
                try
                {
                    collector.EtlDataFile.SetFilterProcess(data.ProcessID);
                    collector.EtlDataFile.LookupSymbolsForModule(ca.ModuleName);
                }
                catch (Exception)
                {
                    return null;
                }
                SymbolsLookedUp.Add(ca.ModuleName);
            }

            if (ca.Method == null)
            {
                SymbolsMissing.Add(ca.ModuleName);
                return null;
            }
            return ca.ModuleName + "!" + ca.Method.FullMethodName;
        }

        private void UpdateMethodCounts(string name)
        {
            if (String.IsNullOrEmpty(name))
            {
                return;
            }
            int value = 0;
            MethodCounts.TryGetValue(name, out value);
            MethodCounts[name] = value + 1;
        }
    }
#endif
}
