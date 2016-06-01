using System;
using System.IO;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Serialization;
using Stats;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Parsers.ClrPrivate;
using PerfView.CapStats;
using System.Text;

#if CAP

/// <summary>
/// GCAnalysis contains the data that we create for the Customer Assistance Program
/// Will be serialized in xml format
/// </summary>
namespace ClrCap
{
    [Serializable()]
    public class CAPAnalysisBase
    {
        public TraceInfo TraceInfo;
        public OSInfo OSInfo;
        public MachineInfo MachineInfo;
        [XmlIgnoreAttribute]
        public EventStats EventStats;

        public CAPAnalysisBase()
        {
            TraceInfo = new TraceInfo();
            OSInfo = new OSInfo();
            MachineInfo = new MachineInfo();
            EventStats = new EventStats();
        }

        public void WriteToFileXML(string outputFilePath)
        {
            using (TextWriter WriteFileStream = new StreamWriter(outputFilePath, false))
            {
                XmlSerializer SerializerObj = new XmlSerializer(GetType());
                SerializerObj.Serialize(WriteFileStream, this);
            }
        }
    }
    
    [Serializable()]
    [XmlRoot("GCAnalysis")]
    public class CAPAnalysis : CAPAnalysisBase
    {
        public Process[] Processes;
    }
    
    [Serializable()]
    [XmlRoot("JitCapAnalysis")]
    public class JitCapAnalysis : CAPAnalysisBase
    {
        private const char METHOD_NAME_SEP = ':';
        [XmlArray("HotMethodData")]
        public JitCapData[] JitCapData;

        public static JitCapAnalysis ReadReport(string jitReportFileName)
        {
            var reader = new StringReader(File.ReadAllText(jitReportFileName));
            JitCapAnalysis result = (JitCapAnalysis)new XmlSerializer(typeof(JitCapAnalysis)).Deserialize(reader);
            return result;
        }

        public void FormatForTrace(string processName, string outputFile, bool discardNamespace)
        {
            StringBuilder sb = new StringBuilder();
            foreach (JitCapData data in JitCapData)
            {
                if (String.Compare(data.ProcessInfo.Name, processName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    foreach (JitCapData.MethodSampleInfo info in data.TopMethods)
                    {
                        // We have {dll}!{class.scope}.{methodName}(...)
                        int methodNameStartPos = info.Name.IndexOf("!");
                        if (methodNameStartPos <= 0)
                        {
                            continue;
                        }

                        methodNameStartPos++;
                        int methodNameEndPos = info.Name.LastIndexOf("(");
                        if (methodNameEndPos <= methodNameStartPos)
                        {
                            continue;
                        }

                        String name = info.Name.Substring(methodNameStartPos, methodNameEndPos - methodNameStartPos);
                        name = ReplaceMethodSeparator(name);
                        if (discardNamespace)
                        {
                            name = RemoveNamespace(name);
                        }
                        if (name == null)
                        {
                            continue;
                        }
                        sb.Append(name);
                        sb.Append(System.Environment.NewLine);
                    }
                }
            }
            File.WriteAllText(outputFile, sb.ToString());
        }

        private string RemoveNamespace(string name)
        {
            if (String.IsNullOrEmpty(name))
            {
                return null;
            }
            int dotPos = FindLastNonGenericDot(name, name.LastIndexOf(METHOD_NAME_SEP) - 1);
            if (dotPos < 0)
            {
                return null;
            }
            return name.Substring(dotPos + 1);
        }
        private string ReplaceMethodSeparator(string name)
        {
            if (String.IsNullOrEmpty(name))
            {
                return null;
            }

            int dotPos = FindLastNonGenericDot(name, name.Length - 1);

            if (dotPos < 0)
            {
                return null;
            }
            if (name.IndexOf("..cctor", dotPos - 1) > 0 ||
                name.IndexOf("..ctor", dotPos - 1) > 0)
            {
                dotPos--;
            }

            StringBuilder sbName = new StringBuilder(name);
            sbName[dotPos] = METHOD_NAME_SEP;
            return sbName.ToString();
        }

        // In Foo.Get[System.__Canon, Immutable[System.__Canon]], finds the index of the 
        // dot between Foo and Get
        // If such a dot is non-existent returns < 0.
        private int FindLastNonGenericDot(string name, int startPos)
        {
            int dotPos = -1;
            for (int i = startPos, level = 0; i >= 0; --i)
            {
                level += (name[i] == ']') ? 1 : (name[i] == '[') ? -1 : 0;
                if (level == 0 && name[i] == '.')
                {
                    dotPos = i;
                    break;
                }
            }
            return dotPos;
        }
    }

    [Serializable()]
    [XmlType("ProcessData")]
    public class JitCapData
    {
        [Serializable()]
        public class MethodSampleInfo
        {
            [XmlAttribute]
            public string Name;
            [XmlAttribute]
            public int Count;
        };

        [Serializable()]
        public class ModuleNames
        {
            public string[] ModuleName;
        }

        [Serializable()]
        public class ProcessDetails
        {
            [XmlAttribute]
            public int CpuMsec;
            [XmlAttribute]
            public string Name;
        }

        public ProcessDetails ProcessInfo;

        public ModuleNames MissingSymbols;

        public MethodSampleInfo[] TopMethods;

        public static JitCapData CreateInstance(List<KeyValuePair<string, int>> topMethods, HashSet<string> symbolsMissing, string processName, int processCpuMsec)
        {
            JitCapData self = new JitCapData();
            int count = 0;

            self.TopMethods = new MethodSampleInfo[topMethods.Count];
            foreach (KeyValuePair<string, int> tm in topMethods)
            {
                self.TopMethods[count] = new MethodSampleInfo();
                self.TopMethods[count].Name = tm.Key;
                self.TopMethods[count].Count = tm.Value;
                count++;
            }

            self.MissingSymbols = new ModuleNames();
            self.MissingSymbols.ModuleName = new string[symbolsMissing.Count];
            symbolsMissing.CopyTo(self.MissingSymbols.ModuleName);

            self.ProcessInfo = new ProcessDetails();
            self.ProcessInfo.Name = processName;
            self.ProcessInfo.CpuMsec = processCpuMsec;
            return self;
        }
    }

    class CapJitProcessor
    {
        private JitCapProcess stats;

        public CapJitProcessor(JitCapProcess stats)
        {
            this.stats = stats;
        }

        public JitCapData ProcessInfo(int methodCount)
        {
            int numCount = Math.Max(0, Math.Min(methodCount, stats.MethodCounts.Count));

            List<KeyValuePair<string, int>> topMethods = new List<KeyValuePair<string, int>>(stats.MethodCounts);
            topMethods.Sort(delegate(KeyValuePair<string, int> elem1, KeyValuePair<string, int> elem2) {
                return elem2.Value - elem1.Value;
            });

            topMethods.RemoveRange(numCount, topMethods.Count - numCount);
            return JitCapData.CreateInstance(topMethods, stats.SymbolsMissing, stats.ProcessName, stats.ProcessCpuTimeMsec);
        }
    }

    public class TraceInfo
    {
        public DateTime TraceStart;
        public DateTime TraceEnd;
        public double TraceDurationSeconds;
        public int NumberOfLostEvents;
        public string FileLocation;
    }

    public class OSInfo
    {
        public string Version;
        public string Name;
        public string Build;
    }

    public class MachineInfo
    {
        public string MachineName;
        public string Domain;
        public int MemorySizeMb;
        public int NumberOfProcessors;
        public int ProcessorFrequencyMHz;
        public int HyperThreadingFlag;
        public int PageSize;
    }

    [Serializable()]
    public class EventCount
    {
        [XmlAttribute]
        public string ProviderName;

        [XmlAttribute]
        public string EventName;

        [XmlAttribute]
        public int Count;

        [XmlAttribute]
        public double AverageDataSize;

        [XmlAttribute]
        public int StackCount;
    }

    public class EventStats
    {
        public EventCount[] EventCounts;

        public void PopulateEventCounts(TraceEventStats eventStats)
        {
            if (null == eventStats)
            {
                throw new ArgumentNullException("eventStats");
            }

            EventCounts = new EventCount[eventStats.Count];

            int currentEvent = 0;
            foreach (TraceEventCounts eventCounts in eventStats)
            {
                EventCount ec = new EventCount()
                {
                    ProviderName = eventCounts.ProviderName,
                    EventName = eventCounts.EventName,
                    AverageDataSize = eventCounts.AveragePayloadSize,
                    Count = eventCounts.Count,
                    StackCount = eventCounts.StackCount
                };

                EventCounts[currentEvent++] = ec;
            }
        }
    }

    [Serializable()]
    public class Process
    {
        public string ProcessName;
        public string CommandLine;
        public uint ProcessID;
        public string ClrVersion;
        public int Bitness;

        //General
        public double ProcessElapsedTimeMSec;
        public double TotalGCPauseMSec;
        public int NumberOfHeaps;
        public string GCFlavor;

        public double AvgMemoryPressure;
        public double MaxMemoryPressure;

        //G0
        public int NumberOfGen0s;
        public int NumberOfForegroundGen0s;
        public int NumberOfInducedGen0s;
        public double AvgEndGen0SizeMb;
        public double MaxEndGen0SizeMb;
        public double AvgEndGen0FragmentationMb;
        public double Gen0AvgPauseTimeMSec;
        public double Gen0MaxPauseTimeMSec;
        public uint NumberOfGen0sWithPause0To10MSec;
        public uint NumberOfGen0sWithPause10To30MSec;
        public uint NumberOfGen0sWithPause30To50MSec;
        public uint NumberOfGen0sWithPause50To75MSec;
        public uint NumberOfGen0sWithPause75To100MSec;
        public uint NumberOfGen0sWithPause100To200MSec;
        public uint NumberOfGen0sWithPause200To500MSec;
        public uint NumberOfGen0sWithPause500To1000MSec;
        public uint NumberOfGen0sWithPause1000To3000MSec;
        public uint NumberOfGen0sWithPause3000To5000MSec;
        public uint NumberOfGen0sWithPauseGreaterThan5000MSec;

        //G1
        public int NumberOfGen1s;
        public int NumberOfForegroundGen1s;
        public int NumberOfInducedGen1s;
        public double AvgEndGen1SizeMb;
        public double MaxEndGen1SizeMb;
        public double AvgEndGen1FragmentationMb;
        public double Gen1AvgPauseTimeMSec;
        public double Gen1MaxPauseTimeMSec;
        public uint NumberOfGen1sWithPause0To10MSec;
        public uint NumberOfGen1sWithPause10To30MSec;
        public uint NumberOfGen1sWithPause30To50MSec;
        public uint NumberOfGen1sWithPause50To75MSec;
        public uint NumberOfGen1sWithPause75To100MSec;
        public uint NumberOfGen1sWithPause100To200MSec;
        public uint NumberOfGen1sWithPause200To500MSec;
        public uint NumberOfGen1sWithPause500To1000MSec;
        public uint NumberOfGen1sWithPause1000To3000MSec;
        public uint NumberOfGen1sWithPause3000To5000MSec;
        public uint NumberOfGen1sWithPauseGreaterThan5000MSec;

        //G2
        public int NumberOfGen2s;
        public int NumberOfBlockingGen2s;
        public int NumberOfLowMemoryGen2s;
        public int NumberOfInducedGen2s;
        public int NumberOfInducedBlockingGen2s;
        public double AvgEndGen2SizeMb;
        public double MaxEndGen2SizeMb;
        public double AvgEndGen2FragmentationMb;
        public double Gen2AvgPauseTimeMSec;
        public double Gen2MaxPauseTimeMSec;
        public double BlockingGen2AvgPauseTimeMSec;
        public double BlockingGen2MaxPauseTimeMSec;
        public double BGCAvgPauseTimeMSec;
        public double BGCMaxPauseTimeMSec;
        public uint NumberOfGen2sWithPause0To10MSec;
        public uint NumberOfGen2sWithPause10To30MSec;
        public uint NumberOfGen2sWithPause30To50MSec;
        public uint NumberOfGen2sWithPause50To75MSec;
        public uint NumberOfGen2sWithPause75To100MSec;
        public uint NumberOfGen2sWithPause100To200MSec;
        public uint NumberOfGen2sWithPause200To500MSec;
        public uint NumberOfGen2sWithPause500To1000MSec;
        public uint NumberOfGen2sWithPause1000To3000MSec;
        public uint NumberOfGen2sWithPause3000To5000MSec;
        public uint NumberOfGen2sWithPauseGreaterThan5000MSec;

        //LOH
        public int NumberOfLOHGen2s;
        public double AvgEndLOHSizeMb;
        public double MaxEndLOHSizeMb;
        public double AvgEndLOHFragmentationMb;

        public SBAs SBAs;
    }

    public class SBAs
    {
        [XmlElement("Issue")]
        public Issue[] Issues;

        [XmlElement("FYI")]
        public Issue[] FYIs;
    }

    [Serializable()]
    public class Issue
    {
        [XmlAttribute]
        public string Name;

        [XmlAttribute]
        public double PauseResourceValue;
        
        [XmlAttribute]
        public double SizeResourceValue;

        [XmlAttribute]
        public double RatioResourceValue;

        [XmlText]
        public string Details;

        public DataPoint[] DataPoints;

        public MetricEffect[] MetricEffects;
    }

    [Serializable()]
    public class DataPoint
    {
        [XmlAttribute]
        public string Name;

        [XmlAttribute]
        public string Value;
    }

    [Serializable()]
    public class MetricEffect
    {
        [XmlAttribute]
        public Metric Metric;

        [XmlAttribute]
        public double Effect;
    }

    public enum Metric
    {
        GCPauseTime,
        ManagedHeapSize
    }

    struct GCDataForLargeGen0ObjSize
    {
        public int EventIndex;
        public double MaxGen0ObjSizeMB; // In Server GC this would be the largest gen0 obj size in all heaps
        public GCDataForLargeGen0ObjSize(int Index, double Size)
        {
            EventIndex = Index;
            MaxGen0ObjSizeMB = Size;
        }
    }
    struct GCSequenceIndices
    {
        public int Begin;
        public int End;
        public GCSequenceIndices(int BeginIndex, int EndIndex)
        {
            Begin = BeginIndex;
            End = EndIndex;
        }
    }

    struct ExtendedGCInfo
    {
        public int GCCount;

        public int NumInducedBlocking; //applies to Gen2
        public int NumForeground;  //applies to ephemeral generations
        public int NumBlocking;  //applies to Gen2
        public int NumLOHTriggered;   //recorded in Total
        public int NumLowMemory;

        public double TotalSuspendDurationMSec;
        public double TotalInducedPauseDurationMSec;
        public double TotalLOHTriggeredPauseDurationMSec;
        public List<int> LongSuspensionGCIndices;
        public List<GCSequenceIndices> LowEphemeralGen1Indices;
        public List<GCSequenceIndices> HighMemSweepingFullGCIndices;
        public GCFreeListEfficiency Gen2FreeListEfficiency;

        //recorded for Total
        public double AvgMemoryPressure;
        public double MaxMemoryPressure;
        public double TotalBlockingGen2Pause;
        public double MaxBlockingGen2Pause;
        public double MeanBlockingGen2Pause;
        public double MaxBGCPause;
        public double MeanBGCPause;

        public double MeanGen0SizeAfterMB { get { return TotalGen0SizeAfterMB / GCCount; } }
        public double MaxGen0SizeAfterMB;
        public double MeanGen0FragmentationMB { get { return TotalGen0FragmentationMB / GCCount; } }

        public double MeanGen1SizeAfterMB { get { return TotalGen1SizeAfterMB / GCCount; } }
        public double MaxGen1SizeAfterMB;
        public double MeanGen1FragmentationMB { get { return TotalGen1FragmentationMB / GCCount; } }

        public double MeanGen2SizeAfterMB { get { return TotalGen2SizeAfterMB / GCCount; } }
        public double MaxGen2SizeAfterMB;
        public double MeanGen2FragmentationMB { get { return TotalGen2FragmentationMB / GCCount; } }
        public double MeanLOHSizeAfterMB { get { return TotalGen3SizeAfterMB / GCCount; } }
        public double MaxLOHSizeAfterMB;
        public double MeanLOHFragmentationMB { get { return TotalGen3FragmentationMB / GCCount; } }

        // Pause distribution
        public uint Pause0To10MSec;
        public uint Pause10To30MSec;
        public uint Pause30To50MSec;
        public uint Pause50To75MSec;
        public uint Pause75To100MSec;
        public uint Pause100To200MSec;
        public uint Pause200To500MSec;
        public uint Pause500To1000MSec;
        public uint Pause1000To3000MSec;
        public uint Pause3000To5000MSec;
        public uint PauseGreaterThan5000MSec;
        
        // TODO, these should be the same access level as the above.
        internal double TotalMemoryPressure;
        internal double TotalGen0SizeAfterMB;
        internal double TotalGen1SizeAfterMB;
        internal double TotalGen2SizeAfterMB;
        internal double TotalGen3SizeAfterMB;
        internal double TotalGen0ObjSizeAfterMB;
        internal double TotalGen1ObjSizeAfterMB;
        internal double TotalGen2ObjSizeAfterMB;
        internal double TotalGen3ObjSizeAfterMB;
        internal double TotalGen0FragmentationMB;
        internal double TotalGen1FragmentationMB;
        internal double TotalGen2FragmentationMB;
        internal double TotalGen3FragmentationMB;

        // for gen2 sizes we only look at them right after a gen2 GC
        internal double TotalGen2ObjSizeAfterFbMB; // right after a full blocking GC
        internal double TotalGen2SizeAfterFbMB;
        internal double TotalGen2ObjSizeAfterBgcMB; // right after a background GC
        internal double TotalGen2SizeAfterBgcMB;

        // If the gen0 obj size is large it means we have long gen0 GCs so we want to record
        // when this happens. We should consider compressing this so we don't record
        // a bunch of GCs with similar (large) gen0 obj sizes.
        // I am only recording this for Total, not per generation for now.
        internal List<GCDataForLargeGen0ObjSize> LargeGen0PerHeapObjSize;
    }
    enum SBAType
    {
        SBA_Excessive_Induced = 1,
        SBA_Excessive_LOH_Triggered = 2,
        SBA_Long_Suspension = 3,
        SBA_Continous_Gen1_LowEph = 4,
        SBA_High_LOH_Frag = 5,
        SBA_Excessive_Demotion = 6,
        // NOTE this is very coarse, SBA_HighMemSweeping_FullGCs is one specific issue we detect.
        // Should really separate this into more fine issues to be more actionable.
        SBA_Excessive_Pause_FullGCs = 7,
        SBA_LongMarking_Ephemeral = 8,
        SBA_LongMarking_FullBlocking = 9,
        SBA_HighMemSweeping_FullGCs = 10,
        SBA_Overly_Pinned = 11,
        SBA_Low_Gen2Free_Efficiency = 12,
        SBA_LOHAlloc_BGC = 13,
        SBA_High_Gen2Fb_Frag = 14,
        // This is where the FYI scenarios begin.
        SBA_High_Gen0_Frag,
    }

    // Scenario Based Analysis
    class SBA
    {
        public bool IsIssue;
        // Not all issues have detailed data. Detailed data is for things that are not
        // easily visible from looking at GCStats, eg, which GCs demoted a large amount of memory.
        public string DetailedData;
        public SBAType Type;
        // Each issue has 1 to 3 resource values associated with it. 
        public double PauseResourceValue;
        public double SizeResourceValue;
        public double RatioResourceValue;
        // Not all issues have DataPoints because for some issues they are easily calculatable from 
        // the data that we already expose in Process.
        public DataPoint[] DataPoints;
        public MetricEffect[] MetricEffects;

        public SBA(SBAType _Type, bool _IsIssue = true)
        {
            Type = _Type;
            IsIssue = _IsIssue;
            PauseResourceValue = double.NaN;
            SizeResourceValue = double.NaN;
            RatioResourceValue = double.NaN;
        }
    }

    class GCFreeListEfficiency
    {
        public int GCCount; // The # of GCs we are calculating efficiency for.
        public double TotalAllocated;
        public double TotalFreeListConsumed;
        public double TotalHeapSizeMB;
    }

    class CapProcess
    {
        List<SBA> SBAs = new List<SBA>();
        ExtendedGCInfo[] Generations = new ExtendedGCInfo[3];
        ExtendedGCInfo Total = new ExtendedGCInfo();
        List<GCEvent> events;

        GCProcess stats;

        private void ComputeExcessiveInduced()
        {
            int InducedPercentage = stats.Total.NumInduced * 100 / Total.GCCount;
            if (InducedPercentage > 10)
            {
                SBA Scenario = new SBA(SBAType.SBA_Excessive_Induced);
                Scenario.PauseResourceValue = Total.TotalInducedPauseDurationMSec;

                Scenario.MetricEffects = new MetricEffect[]
                {
                    new MetricEffect
                    {
                        Metric = Metric.GCPauseTime,
                        Effect = Total.TotalInducedPauseDurationMSec / stats.Total.TotalPauseTimeMSec
                    }
                };

                SBAs.Add(Scenario);
            }
        }

        private void ComputeExcessiveLOHTriggered()
        {
            int NumFullGCs = Generations[2].GCCount;
            if (NumFullGCs == 0)
                return;

            int LOHTriggeredPercentage = Total.NumLOHTriggered * 100 / NumFullGCs;
            if (LOHTriggeredPercentage > 30)
            {
                SBA Scenario = new SBA(SBAType.SBA_Excessive_LOH_Triggered);
                Scenario.PauseResourceValue = Total.TotalLOHTriggeredPauseDurationMSec;

                Scenario.MetricEffects = new MetricEffect[]
                {
                    new MetricEffect
                    {
                        Metric = Metric.GCPauseTime,
                        Effect = Total.TotalLOHTriggeredPauseDurationMSec / stats.Total.TotalPauseTimeMSec
                    }
                };

                SBAs.Add(Scenario);
            }
        }

        private void ComputeLongSuspension()
        {
            double TotalSuspensionMSec = Total.TotalSuspendDurationMSec;
            double TotalPauseMSec = stats.Total.TotalPauseTimeMSec;

            if (TotalPauseMSec > 100)
            {
                bool ShouldAddSolution = false;
                StringBuilder sbDescription = new StringBuilder();
                double SuspensionPercentage = TotalSuspensionMSec / TotalPauseMSec;

                if (SuspensionPercentage > 0.05)
                {
                    ShouldAddSolution = true;
                }

                int LongSuspensionCount = 0;
                for (int i = 0; i < Generations.Length; i++)
                {
                    if ((Generations[i].LongSuspensionGCIndices != null) && (Generations[i].LongSuspensionGCIndices.Count > 0))
                        LongSuspensionCount++;
                }

                if (LongSuspensionCount > 0)
                {
                    ShouldAddSolution = true;
                    for (int i = 0; i < Generations.Length; i++)
                    {
                        if ((Generations[i].LongSuspensionGCIndices != null) && (Generations[i].LongSuspensionGCIndices.Count > 0))
                        {
                            sbDescription.AppendFormat("{0} gen{1} GCs\r\n", Generations[i].LongSuspensionGCIndices.Count, i);
                            for (int LongSuspensionIndex = 0; LongSuspensionIndex < Generations[i].LongSuspensionGCIndices.Count; LongSuspensionIndex++)
                            {
                                int EventIndex = Generations[i].LongSuspensionGCIndices[LongSuspensionIndex];
                                sbDescription.AppendFormat("  #{0}: {1:f2}ms\r\n", events[EventIndex].GCNumber, events[EventIndex]._SuspendDurationMSec);
                            }
                        }
                    }
                }

                if (ShouldAddSolution)
                {
                    SBA Scenario = new SBA(SBAType.SBA_Long_Suspension);
                    Scenario.DetailedData = sbDescription.ToString();
                    Scenario.PauseResourceValue = TotalSuspensionMSec;
                    Scenario.MetricEffects = new MetricEffect[]
                    {
                        new MetricEffect
                        {
                            Metric = Metric.GCPauseTime,
                            Effect = SuspensionPercentage
                        }
                    };
                    SBAs.Add(Scenario);
                }
            }
        }

        private void ComputeContinousGen1()
        {
            if ((Total.LowEphemeralGen1Indices != null) && (Total.LowEphemeralGen1Indices.Count > 0))
            {
                double TotalLowEphemeralGen1Pause = 0;
                int TotalLowEphemeralGen1Count = 0;
                SBA Scenario = new SBA(SBAType.SBA_Continous_Gen1_LowEph);

                StringBuilder sbDescription = new StringBuilder();

                for (int i = 0; i < Total.LowEphemeralGen1Indices.Count; i++)
                {
                    int BeginIndex = Total.LowEphemeralGen1Indices[i].Begin;
                    int EndIndex = Total.LowEphemeralGen1Indices[i].End;
                    double TotalPause = 0;
                    for (int index = BeginIndex; index <= EndIndex; index++)
                    {
                        if (events[index].isComplete)
                            TotalPause += events[index].PauseDurationMSec;
                    }
                    TotalLowEphemeralGen1Pause += TotalPause;
                    TotalLowEphemeralGen1Count += (events[EndIndex].GCNumber - events[BeginIndex].GCNumber + 1);

                    sbDescription.AppendFormat("#{0} to {1} ({2} GCs, {3:f2}ms pause)\r\n",
                        events[BeginIndex].GCNumber, events[EndIndex].GCNumber, (events[EndIndex].GCNumber - events[BeginIndex].GCNumber + 1), TotalPause);
                }

                Scenario.DetailedData = sbDescription.ToString();
                Scenario.PauseResourceValue = TotalLowEphemeralGen1Pause;

                Scenario.MetricEffects = new MetricEffect[]
                {
                    new MetricEffect
                    {
                        Metric = Metric.GCPauseTime,
                        Effect = TotalLowEphemeralGen1Pause / stats.Total.TotalPauseTimeMSec
                    }
                };

                SBAs.Add(Scenario);
            }
        }

        private void ComputeFragmentationGen0()
        {
            if ((Total.TotalGen0SizeAfterMB == 0) || (Total.TotalGen0SizeAfterMB == 0))
                return;

            // If gen0 size is more than 20% of the total heap size, we start caring about looking at the
            // fragmentation situation in it.
            int Gen0Ratio = (int)((Total.TotalGen0SizeAfterMB / Total.GCCount / stats.Total.MeanSizeAfterMB) * 100);
            if (Gen0Ratio > 20)
            {
                int ObjRatio = (int)((Total.TotalGen0ObjSizeAfterMB / Total.TotalGen0SizeAfterMB) * 100);
                if (ObjRatio < 5)
                {
                    SBA Scenario = new SBA(SBAType.SBA_High_Gen0_Frag, false);
                    Scenario.SizeResourceValue = (Total.TotalGen0SizeAfterMB - Total.TotalGen0ObjSizeAfterMB) / Total.GCCount;

                    Scenario.MetricEffects = new MetricEffect[]
                    {
                        new MetricEffect()
                        {
                            Metric = Metric.ManagedHeapSize,
                            Effect = (Total.TotalGen0SizeAfterMB - Total.TotalGen0ObjSizeAfterMB) / stats.Total.TotalSizeAfterMB
                        }
                    };

                    SBAs.Add(Scenario);
                }
            }
        }

        private void ComputeFragmentationLOH()
        {
            if ((Total.TotalGen3ObjSizeAfterMB == 0) || (Total.TotalGen3SizeAfterMB == 0))
                return;

            // If LOH size is more than 20% of the total heap size, we start caring about looking at the
            // fragmentation situation in it.
            int LOHRatio = (int)((Total.TotalGen3SizeAfterMB / Total.GCCount / stats.Total.MeanSizeAfterMB) * 100);
            if (LOHRatio > 20)
            {
                int ObjRatio = (int)((Total.TotalGen3ObjSizeAfterMB / Total.TotalGen3SizeAfterMB) * 100);
                if (ObjRatio < 50)
                {
                    SBA Scenario = new SBA(SBAType.SBA_High_LOH_Frag);
                    Scenario.SizeResourceValue = (Total.TotalGen3SizeAfterMB - Total.TotalGen3ObjSizeAfterMB) / Total.GCCount;

                    Scenario.MetricEffects = new MetricEffect[]
                    {
                        new MetricEffect()
                        {
                            Metric = Metric.ManagedHeapSize,
                            Effect = (Total.TotalGen3SizeAfterMB - Total.TotalGen3ObjSizeAfterMB) / stats.Total.TotalSizeAfterMB
                        }
                    };

                    SBAs.Add(Scenario);
                }
            }
        }

        private void ComputeFragmentationGen2()
        {
            if ((Total.TotalGen2ObjSizeAfterFbMB == 0) || (Total.TotalGen2SizeAfterFbMB == 0))
                return;

            int Gen2BlockingRatio = (int)((Total.TotalGen2ObjSizeAfterFbMB / Total.TotalGen2SizeAfterFbMB) * 100);
            if (Gen2BlockingRatio < 70)
            {
                int Gen2BlockingCount = 0;
                double Gen2BlockingPinnedObjCount = 0;
                for (int i = 0; i < events.Count; i++)
                {
                    GCEvent _event = events[i];
                    if (!_event.isComplete)
                        continue;

                    if ((_event.GCGeneration == 2) && (_event.Type != GCType.BackgroundGC))
                    {
                        Gen2BlockingCount++;
                        Gen2BlockingPinnedObjCount += _event.HeapStats.PinnedObjectCount;
                    }
                }

                SBA Scenario = new SBA(SBAType.SBA_High_Gen2Fb_Frag);
                Scenario.SizeResourceValue = (Total.TotalGen2SizeAfterFbMB - Total.TotalGen2ObjSizeAfterFbMB) / Total.GCCount;

                Scenario.DataPoints = new DataPoint[]
                {
                    new DataPoint() 
                    {
                        Name = "Gen2FbFragRatio",
                        Value = (100 - Gen2BlockingRatio).ToString()
                    },
                    new DataPoint()
                    {
                        Name = "AvgNumberOfPinnedObjects",
                        Value = ((int)(Gen2BlockingPinnedObjCount / Gen2BlockingCount)).ToString()
                    }
                };
                Scenario.MetricEffects = new MetricEffect[]
                {
                    new MetricEffect()
                    {
                        Metric = Metric.ManagedHeapSize,
                        Effect = (Total.TotalGen2SizeAfterFbMB - Total.TotalGen2ObjSizeAfterFbMB) / stats.Total.TotalSizeAfterMB
                    }
                };

                SBAs.Add(Scenario);
            }
        }

        // Users usually would be worried if they see total fragmentation is high; or if they are already 
        // looking at the GCStats view or SoS they would be worried if they see any generation's fragmentation 
        // ratio is high.
        private void ComputeFragmentation()
        {
            ComputeFragmentationGen0();
            ComputeFragmentationLOH();
            ComputeFragmentationGen2();
        }

        // TODO: if the pinning events are present, we should only flag when pinning mostly consists of user pinned objects.
        // if it's mostly pinned due to GC, we should make this a SBAS_WorkItem_Logged solution.
        // Can also mention that we are working on better pinning support in the framework.
        private void ComputeExcessiveDemotion()
        {
            if ((Total.LargeGen0PerHeapObjSize != null) && (Total.LargeGen0PerHeapObjSize.Count > 0))
            {
                SBA Scenario = new SBA(SBAType.SBA_Excessive_Demotion);
                StringBuilder sbDescription = new StringBuilder();

                double AffectedPauseTimeMsec = 0.0;
                for (int i = 0; i < Total.LargeGen0PerHeapObjSize.Count; i++)
                {
                    GCEvent _event = events[Total.LargeGen0PerHeapObjSize[i].EventIndex];
                    sbDescription.AppendFormat("#{0}(gen{1}), {2:f2}MB objects left in Gen0, {3:f2}ms pause\r\n",
                        _event.GCNumber, _event.GCGeneration, Total.LargeGen0PerHeapObjSize[i].MaxGen0ObjSizeMB, _event.PauseDurationMSec);
                    AffectedPauseTimeMsec += _event.PauseDurationMSec;
                }

                Scenario.PauseResourceValue = AffectedPauseTimeMsec;
                Scenario.DetailedData = sbDescription.ToString();

                Scenario.DataPoints = new DataPoint[]
                {
                    new DataPoint()
                    {
                        Name = "NumberOfGCsDemotion",
                        Value = (Total.LargeGen0PerHeapObjSize.Count).ToString()
                    }
                };
                Scenario.MetricEffects = new MetricEffect[]
                {
                    new MetricEffect()
                    {
                        Metric = Metric.GCPauseTime,
                        Effect = AffectedPauseTimeMsec / stats.Total.TotalPauseTimeMSec
                    }
                };
                
                SBAs.Add(Scenario);
            }
        }

        struct  CondemnedReasonInfo
        {
            public string DataPointName;
            public string Description;
            
            public CondemnedReasonInfo(string name, string descr)
            {
                DataPointName = name;
                Description = descr;
            }
        }

        // Note, if you change this, you should change it in the CAP website code as well.
        static CondemnedReasonInfo[] CondemnedReasonDescription = new CondemnedReasonInfo[]
        {
            new CondemnedReasonInfo("InitialGen", "Initial generation"),
            new CondemnedReasonInfo("FinalGen", "Final generation"),
            new CondemnedReasonInfo("BudgetExceeded", "Budget exceeded"),
            new CondemnedReasonInfo("TimeTuning", "Time tuning"),
            new CondemnedReasonInfo("Induced", "Induced"),
            new CondemnedReasonInfo("LowEphemeral", "Low ephemeral"),
            new CondemnedReasonInfo("ExpandHeap", "Expand heap"),
            new CondemnedReasonInfo("FragmentEphemeral", "Fragmented ephemeral generations"),
            new CondemnedReasonInfo("VFragmentEphemera", "Very fragmented ephemeral generations"),
            new CondemnedReasonInfo("FragmentGen2", "Fragmented Gen2"),
            new CondemnedReasonInfo("HighMemory", "High memory"),
            new CondemnedReasonInfo("LastGC", "Last GC before OOM"),
            new CondemnedReasonInfo("SmallHeap", "Small Heap"),
            new CondemnedReasonInfo("EphemeralBeforeBGC", "Ephemeral GC before a background GC starts"),
            new CondemnedReasonInfo("InternalTuning", "Internal Tuning")
        };

        static string[] FullGCCondemnedReasonActionItem = new string[]
        {
            "Not applicable", // The first 2 are simply for recording purpose.
            "Not applicable",
            "This means you are churning the generation too much [we will add GC/generation aware heap snapshots to diagnose this]",
            "If you are seeing too many of this it means Workstation GC may not be appropriate for you",
            "Follow the instructions to find out induced GC callstacks",
            "Not applicable",
            "We really shouldn't see this often. If you are, you should contact the gccap alias",
            "Not applicable",
            "Not applicable",
            "This is usually due to pinning, following the pinning diagnostics",
            "You are running in high memory situation and there is enough fragmentation. If there are too many follow the instructions to capture pinning events to analyze pinning",
            "If we didn't do a full blocking GC we'd be OOM",
            "These GCs usually don't take much time",
            "Not applicable",
            "Not applicable"
        };

        // If full blocking GCs are contributing to a big percentage of the total pause it's 
        // definitely concerning. If most of them are BGCs but the ratio is very high, it's also
        // concerning because we'd like to see a good gen1:gen2 ratio.
        //
        // One pattern we recognize is in high memory load situation when we are incorrectly 
        // doing a sweeping GC instead of a compacting GC and end up doing back to back gen2 
        // sweeping GCs. We do have a fix for this.
        // 
        private void ComputeExcessiveFullGCs()
        {
            int FullGCCountRatio = Generations[2].GCCount * 100 / Total.GCCount;
            int FullGCPauseRatio = (int)(stats.Generations[2].TotalPauseTimeMSec * 100.0 / stats.Total.TotalPauseTimeMSec);

            if (FullGCPauseRatio > 30)
            {
                double TotalHighMemSweepingGen2PauseMSec = 0;
                if ((Total.HighMemSweepingFullGCIndices != null) && (Total.HighMemSweepingFullGCIndices.Count > 0))
                {
                    int TotalHighMemSweepingGen2Count = 0;
                    SBA Scenario = new SBA(SBAType.SBA_HighMemSweeping_FullGCs);

                    StringBuilder sbDescription = new StringBuilder("Back to back gen2 GCs in high memory situations: \r\n");

                    for (int i = 0; i < Total.HighMemSweepingFullGCIndices.Count; i++)
                    {
                        int BeginIndex = Total.HighMemSweepingFullGCIndices[i].Begin;
                        int EndIndex = Total.HighMemSweepingFullGCIndices[i].End;
                        double TotalPause = 0;
                        for (int index = BeginIndex; index <= EndIndex; index++)
                        {
                            TotalPause += events[index].PauseDurationMSec;
                        }
                        TotalHighMemSweepingGen2PauseMSec += TotalPause;
                        TotalHighMemSweepingGen2Count += EndIndex - BeginIndex + 1;

                        sbDescription.AppendFormat("#{0} to {1} ({2} GCs, {3:f2}ms pause)\r\n",
                            events[BeginIndex].GCNumber, events[EndIndex].GCNumber, (EndIndex - BeginIndex + 1), TotalPause);
                    }

                    Scenario.DetailedData = sbDescription.ToString();
                    Scenario.PauseResourceValue = TotalHighMemSweepingGen2PauseMSec;
                    Scenario.DataPoints = new DataPoint[]
                    {
                        new DataPoint()
                        {
                            Name = "TotalHighMemSweepingGen2Count",
                            Value = TotalHighMemSweepingGen2Count.ToString()
                        }
                    };

                    Scenario.MetricEffects = new MetricEffect[]
                    {
                        new MetricEffect()
                        {
                            Metric = Metric.GCPauseTime,
                            Effect = TotalHighMemSweepingGen2PauseMSec / stats.Total.TotalPauseTimeMSec
                        }
                    };

                    SBAs.Add(Scenario);
                }

                if ((int)(TotalHighMemSweepingGen2PauseMSec * 100.0 / stats.Generations[2].TotalPauseTimeMSec) < 80)
                {
                    SBA Scenario = new SBA(SBAType.SBA_Excessive_Pause_FullGCs);

                    if (stats.m_detailedGCInfo)
                    {
                        Dictionary<CondemnedReasonGroup, int> FullGCReasonInfo = new Dictionary<CondemnedReasonGroup, int>();

                        for (int i = 0; i < events.Count; i++)
                        {
                            GCEvent _event = events[i];
                            if (!_event.isComplete)
                            {
                                continue;
                            }

                            if (_event.GCGeneration == (int)Gens.Gen2)
                            {
                                _event.GetCondemnedReasons(FullGCReasonInfo);
                            }
                        }

                        Scenario.DataPoints = new DataPoint[FullGCReasonInfo.Count];
                        int ReasonIndex = 0;

                        foreach (KeyValuePair<CondemnedReasonGroup, int> Reason in FullGCReasonInfo)
                        {
                            Scenario.DataPoints[ReasonIndex] = new DataPoint();
                            Scenario.DataPoints[ReasonIndex].Name = CondemnedReasonDescription[(int)(Reason.Key)].DataPointName;
                            Scenario.DataPoints[ReasonIndex].Value = Reason.Value.ToString();
                            ReasonIndex++;
                        }

                        StringBuilder sbAction = new StringBuilder("The following is more info on these GCs\r\n");

                        foreach (KeyValuePair<CondemnedReasonGroup, int> Reason in FullGCReasonInfo)
                        {
                            if ((Reason.Key == CondemnedReasonGroup.CRG_Expand_Heap) ||
                                (Reason.Key == CondemnedReasonGroup.CRG_Fragmented_Gen2) ||
                                (Reason.Key == CondemnedReasonGroup.CRG_Fragmented_Gen2_High_Mem) ||
                                (Reason.Key == CondemnedReasonGroup.CRG_GC_Before_OOM))
                                sbAction.AppendFormat("{0} causes full blocking GCs: {1}\r\n", Reason.Key, FullGCCondemnedReasonActionItem[(int)Reason.Key]);
                            else
                                sbAction.AppendFormat("{0} alone doesn't cause full blocking GCs\r\n", CondemnedReasonDescription[(int)(Reason.Key)].Description);
                        }

                        Scenario.DetailedData = sbAction.ToString();
                        SBAs.Add(Scenario);
                    }
                    //else
                    //{
                    //    sbDescription.Append("No GC events available to figure out reasons for full GCs. \r\n");
                    //    sbDescription.Append("This means either you are not collecting private GC events or you are not using CLR 4.0 or newer}");
                    //    Scenario.DetailedData = sbDescription.ToString();
                    //    SBAs.Add(Scenario);
                    //}
                }
            }
        }

        // If GCs are taking too long, and marking takes > 30% of the GC pause we will display which part of the
        // marking takes the longest. For Server GC we will also detect how unbalanced each type of marking is.
        // Note that this only applies to blocking GCs as most of the marking is done concurrently for background GCs.
        // For ephemeral GCs we look at the ones > 30ms; for full blocking GCs > 1s.
        //
        // TODO: we might want to seperate gen0 and gen1 GCs.
        //
        private void CheckMarking()
        {
            if (!stats.m_detailedGCInfo) return;

            int[] TotalLongGCCount = new int[2];
            double[] TotalLongGCTime = new double[2];
            double[] TotalMaxMarkTime = new double[2];
            double[] TotalAvgMarkTime = new double[2];

            // Get the stage that took the longest during marking in each GC, and add it to this dictionary.
            // At the end of we will get to see which stage usually takes a long time.
            Dictionary<MarkRootType, double>[] TotalMaxMarkStage = new Dictionary<MarkRootType, double>[2];
            for (int i = 0; i < TotalMaxMarkStage.Length; i++)
                TotalMaxMarkStage[i] = new Dictionary<MarkRootType, double>();

            int[] LongGCTimeThreshold = new int[2];
            LongGCTimeThreshold[0] = 30;
            LongGCTimeThreshold[1] = 1000;

            for (int i = 0; i < events.Count; i++)
            {
                GCEvent _event = events[i];
                if (!_event.isComplete)
                    continue;
                if (_event.PerHeapMarkTimes == null)
                    continue;
                if (!(_event.AllHeapsSeenMark()))
                    continue;

                int index = ((_event.GCGeneration == 2) ? 1 : 0);
                if (_event.Type != GCType.BackgroundGC)
                {
                    if (_event.GCDurationMSec > LongGCTimeThreshold[index])
                    {
                        (TotalLongGCCount[index])++;
                        double CurrentMaxTotalMark = 0;
                        double CurrentMaxMarkStage = 0;
                        double MarkTimeAllHeaps = 0;
                        MarkRootType CurrentMaxMarkStageType = MarkRootType.MarkMax;
                        for (int HeapIndex = 0; HeapIndex < _event.PerHeapMarkTimes.Count; HeapIndex++)
                        {
                            double TotalMark = 0;
                            double MaxMarkStageTime = 0;
                            MarkRootType MaxMarkStageType = MarkRootType.MarkMax;
                            double[] MT = _event.PerHeapMarkTimes[HeapIndex].MarkTimes;

                            for (int MarkStageIndex = 0; MarkStageIndex < MT.Length; MarkStageIndex++)
                            {
                                if (MT[MarkStageIndex] > MaxMarkStageTime)
                                {
                                    MaxMarkStageType = (MarkRootType)MarkStageIndex;
                                    MaxMarkStageTime = MT[MarkStageIndex];
                                }
                                TotalMark += MT[MarkStageIndex];
                            }

                            MarkTimeAllHeaps += TotalMark;

                            if (TotalMark > CurrentMaxTotalMark)
                                CurrentMaxTotalMark = TotalMark;
                            if (MaxMarkStageTime > CurrentMaxMarkStage)
                            {
                                CurrentMaxMarkStage = MaxMarkStageTime;
                                CurrentMaxMarkStageType = MaxMarkStageType;
                            }
                        }

                        {
                            TotalLongGCTime[index] += _event.GCDurationMSec;
                            TotalMaxMarkTime[index] += CurrentMaxTotalMark;
                            TotalAvgMarkTime[index] += (MarkTimeAllHeaps / _event.PerHeapMarkTimes.Count);

                            if (TotalMaxMarkStage[index].ContainsKey(CurrentMaxMarkStageType))
                                TotalMaxMarkStage[index][CurrentMaxMarkStageType] += CurrentMaxMarkStage;
                            else
                                TotalMaxMarkStage[index].Add(CurrentMaxMarkStageType, CurrentMaxMarkStage);
                        }
                    }
                }
            }

            for (int i = 0; i < TotalLongGCTime.Length; i++)
            {
                if (TotalLongGCTime[i] != 0)
                {
                    int MarkRatio = (int)(TotalMaxMarkTime[i] * 100.0 / TotalLongGCTime[i]);
                    if (MarkRatio > 30)
                    {
                        int Balanced = 100;
                        SBA Scenario = new SBA((i == 0) ? SBAType.SBA_LongMarking_Ephemeral : SBAType.SBA_LongMarking_FullBlocking);
                        Scenario.PauseResourceValue = TotalMaxMarkTime[i];
                        Scenario.DataPoints = new DataPoint[]
                        {
                            new DataPoint()
                            {
                                Name = "TotalLongGCCount",
                                Value = TotalLongGCCount[i].ToString()
                            },
                            new DataPoint()
                            {
                                Name = "LongGCTimeThreshold",
                                Value = LongGCTimeThreshold[i].ToString()
                            },
                            new DataPoint()
                            {
                                Name = "TotalLongGCTime",
                                Value = TotalLongGCTime[i].ToString()
                            },
                            new DataPoint()
                            {
                                Name = "Balanced",
                                Value = ((Balanced > 50) ? "fairly" : "not very")
                            },
                            new DataPoint()
                            {
                                Name = "TotalAvgMarkTime",
                                Value = TotalAvgMarkTime[i].ToString()
                            },
                            new DataPoint()
                            {
                                Name = "MarkStack",
                                Value = (TotalMaxMarkStage[i].ContainsKey(MarkRootType.MarkStack) ? TotalMaxMarkStage[i][MarkRootType.MarkStack].ToString() : "0")
                            },
                            new DataPoint()
                            {
                                Name = "MarkFQ",
                                Value = (TotalMaxMarkStage[i].ContainsKey(MarkRootType.MarkFQ) ? TotalMaxMarkStage[i][MarkRootType.MarkFQ].ToString() : "0")
                            },
                            new DataPoint()
                            {
                                Name = "MarkHandles",
                                Value = (TotalMaxMarkStage[i].ContainsKey(MarkRootType.MarkHandles) ? TotalMaxMarkStage[i][MarkRootType.MarkHandles].ToString() : "0")
                            },
                            new DataPoint()
                            {
                                Name = "MarkOlder",
                                Value = (TotalMaxMarkStage[i].ContainsKey(MarkRootType.MarkOlder) ? TotalMaxMarkStage[i][MarkRootType.MarkOlder].ToString() : "0")
                            }
                        };
                        Scenario.MetricEffects = new MetricEffect[]
                        {
                            new MetricEffect()
                            {
                                Metric = Metric.GCPauseTime,
                                Effect = MarkRatio / 100.0
                            }
                        };

                        SBAs.Add(Scenario);
                    }
                }
            }
        }

        private void CheckOverlyPinned()
        {
            if ((stats.Total.NumGCWithPinEvents == 0) ||
                (stats.Total.NumGCWithPinEvents != stats.Total.NumGCWithPinPlugEvents))
                return;

            int ActualPinnedRatio = (int)(stats.Total.PinnedObjectPercentage / stats.Total.NumGCWithPinPlugEvents);

            if (ActualPinnedRatio < 90)
            {
                // Temporarily disabled - 
                // Consider only calculate this for ephemeral GCs. For full GCs this is not
                // necessarily a problem.
                //
                //long TotalPinnedSize = 0;
                //long TotalPinnedByUserSize = 0;
                //for (int i = 0; i < events.Count; i++)
                //{
                //    GCEvent _event = events[i];
                //    if (!_event.isComplete)
                //        continue;

                //    TotalPinnedSize += _event.totalPinnedPlugSize;
                //    TotalPinnedByUserSize += _event.totalUserPinnedPlugSize;
                //}

                SBA Scenario = new SBA(SBAType.SBA_Overly_Pinned);
                Scenario.RatioResourceValue = 100 - ActualPinnedRatio;
                
                SBAs.Add(Scenario);
            }
        }

        private void CheckFreeListEfficiency()
        {
            if ((Total.Gen2FreeListEfficiency == null) || (Total.Gen2FreeListEfficiency.GCCount == 0))
                return;

            int FreeListEfficiency = (int)(Total.Gen2FreeListEfficiency.TotalAllocated * 100.0 / Total.Gen2FreeListEfficiency.TotalFreeListConsumed);
            
            if (FreeListEfficiency < 50)
            {
                SBA Scenario = new SBA(SBAType.SBA_Low_Gen2Free_Efficiency);

                //for (int i = 0; i < events.Count; i++)
                //{
                //    GCEvent _event = events[i];
                //    if (!_event.isComplete || (_event.GCGeneration != 1))
                //        continue;

                //    if (_event.GenSizeBeforeMB(Gens.Gen2) == _event.GenSizeAfterMB(Gens.Gen2))
                //    {
                //        sbDescription.AppendFormat("gen1 GC#{0} promoted {1} bytes, gen2 free list went from {2}MB to {3}MB (diff: {4}, eff: %{5})\r\n",
                //            _event.GCNumber, _event.GenOut(Gens.Gen1), _event.GenFreeListBefore(Gens.Gen2) / 1000000.0, _event.GenFreeListAfter(Gens.Gen2) / 1000000.0,
                //            (_event.GenFreeListBefore(Gens.Gen2) - _event.GenFreeListAfter(Gens.Gen2)),
                //            (int)(_event.GenOut(Gens.Gen1) * 100.0 / (_event.GenFreeListBefore(Gens.Gen2) - _event.GenFreeListAfter(Gens.Gen2))));
                //    }
                //}

                Scenario.RatioResourceValue = FreeListEfficiency;
                Scenario.DataPoints = new DataPoint[]
                {
                    new DataPoint()
                    {
                        Name = "Gen1GCCount",
                        Value = Total.Gen2FreeListEfficiency.GCCount.ToString()
                    },
                    new DataPoint()
                    {
                        Name = "Gen1SurvIntoGen2",
                        Value = Total.Gen2FreeListEfficiency.TotalAllocated.ToString()
                    },
                    new DataPoint()
                    {
                        Name = "FreeListConsumed",
                        Value = Total.Gen2FreeListEfficiency.TotalFreeListConsumed.ToString()
                    }
                };
                // Calculate the ratio of unused free list to heap size.
                double totalGen2HeapSizeMB = Total.Gen2FreeListEfficiency.TotalHeapSizeMB;
                double totalGen2UnusedFreeListMB = (Total.Gen2FreeListEfficiency.TotalFreeListConsumed * ((100.0 - FreeListEfficiency)/100.0))/1024/1024;
                Scenario.MetricEffects = new MetricEffect[]
                {
                    new MetricEffect()
                    {
                        Metric = Metric.ManagedHeapSize,
                        Effect = totalGen2UnusedFreeListMB / totalGen2HeapSizeMB
                    }
                };

                SBAs.Add(Scenario);
            }
        }

        // We are checking for the following:
        // 1) We calculate the average pause time of threads that had to wait for BGC and
        // if that takes up > 10% of the whole BGC duration we consider that an issue.
        // 2) We calculate the # of long waits (> 200ms). 
        private void ComputeLOHAllocWaitBGC()
        {
            if (!stats.m_detailedGCInfo) return;

            int BGCCount = 0;
            int LongWaitCount = 0;
            long TotalWaitRatio = 0;

            for (int i = 0; i < events.Count; i++)
            {
                GCEvent _event = events[i];
                if (!_event.isComplete)
                    continue;

                if (_event.Type == GCType.BackgroundGC)
                {
                    if ((_event.LOHWaitThreads == null) ||
                        (_event.LOHWaitThreads.Count == 0))
                        continue;

                    BGCCount++;

                    double TotalWaitTime = 0;
                    int WaitCount = 0;

                    foreach (KeyValuePair<int, BGCAllocWaitInfo> kvp in _event.LOHWaitThreads)
                    {
                        BGCAllocWaitInfo info = kvp.Value;
                        double WaitTimeMSec = 0;
                        if (info.GetWaitTime(ref WaitTimeMSec))
                        {
                            WaitCount++;
                            TotalWaitTime += WaitTimeMSec;
                            if (WaitTimeMSec > 200)
                                LongWaitCount++;
                        }
                    }

                    if (WaitCount != 0)
                    {
                        int WaitRatio = (int)(TotalWaitTime * 100.0 / (double)WaitCount / _event.GCDurationMSec);
                        TotalWaitRatio += WaitRatio;
                    }
                }
            }

            if (BGCCount > 0)
            {
                int AverageWaitRatio = (int)(TotalWaitRatio / BGCCount);

                // Should do better than just checking if LongWaitCount is not 0.
                if ((AverageWaitRatio > 10) || (LongWaitCount != 0))
                {
                    SBA Scenario = new SBA(SBAType.SBA_LOHAlloc_BGC);
                    Scenario.PauseResourceValue = LongWaitCount;
                    Scenario.DataPoints = new DataPoint[]
                    {
                        new DataPoint()
                        {
                            Name = "AverageWaitRatio",
                            Value = AverageWaitRatio.ToString()
                        }
                    };
                    SBAs.Add(Scenario);
                }
            }
        }

        // This is called after we rolled up info for each generation.
        void ComputeSBAs()
        {
            // I am only doing analysis for processes that have >1% GC pauses and have at least 4 GCs.
            // May want to add a threshold for GC heap size as well and if it's too small we don't compute anything.
            if (stats.InterestingForAnalysis)
            {
                ComputeExcessiveInduced();
                ComputeExcessiveLOHTriggered();
                ComputeLongSuspension();
                ComputeContinousGen1();
                ComputeFragmentation();
                ComputeExcessiveDemotion();
                ComputeExcessiveFullGCs();
                CheckMarking();
                CheckOverlyPinned();
                CheckFreeListEfficiency();
                ComputeLOHAllocWaitBGC();
            }
        }

        public CapProcess(GCProcess _stats)
        {
            stats = _stats;
            events = _stats.events;
        }

        public void ProcessInfo()
        {
            for (int i = 0; i < 3; i++)
            {
                Generations[i].GCCount = stats.Generations[i].GCCount;
            }

            Total.GCCount = stats.Total.GCCount;

            int Gen1SequenceBeginIndex = 0;
            int Gen1SequenceEndIndex = 0;
            int Gen2HighMemSequenceBeginIndex = 0;
            int Gen2HighMemSequenceEndIndex = 0;

            double[] GenDataSizeAfterMB = new double[(int)Gens.GenLargeObj + 1];
            double[] GenDataObjSizeAfterMB = new double[(int)Gens.GenLargeObj + 1];

            for (int i = 0; i < stats.events.Count; i++)
            {
                GCEvent _event = stats.events[i];
                if (!_event.isComplete)
                {
                    continue;
                }

                if (_event.Reason == GCReason.Induced)
                    (Generations[_event.GCGeneration].NumInducedBlocking)++;

                if (_event.Type == GCType.ForegroundGC)
                    Generations[_event.GCGeneration].NumForeground++;
                else if (_event.Type == GCType.NonConcurrentGC)
                    Generations[_event.GCGeneration].NumBlocking++;

                if (_event.Reason == GCReason.LowMemory)
                    Generations[_event.GCGeneration].NumLowMemory++;

                if (_event.DetailedGenDataAvailable())  //per heap histories is not null
                {
                    for (int GenIndex = 0; GenIndex < GenDataSizeAfterMB.Length; GenIndex++)
                    {
                        GenDataSizeAfterMB[GenIndex] = 0;
                        GenDataObjSizeAfterMB[GenIndex] = 0;
                    }
                    _event.GetGenDataSizeAfterMB(ref GenDataSizeAfterMB);
                    _event.GetGenDataObjSizeAfterMB(ref GenDataObjSizeAfterMB);

                    Generations[_event.GCGeneration].TotalGen0SizeAfterMB += GenDataSizeAfterMB[0];
                    Generations[_event.GCGeneration].TotalGen3SizeAfterMB += GenDataSizeAfterMB[3];

                    Generations[_event.GCGeneration].TotalGen0ObjSizeAfterMB += GenDataObjSizeAfterMB[0];
                    Generations[_event.GCGeneration].TotalGen3ObjSizeAfterMB += GenDataObjSizeAfterMB[3];

                    Total.TotalGen0SizeAfterMB += GenDataSizeAfterMB[0];
                    Total.TotalGen1SizeAfterMB += GenDataSizeAfterMB[1];
                    Total.TotalGen2SizeAfterMB += GenDataSizeAfterMB[2];
                    Total.TotalGen3SizeAfterMB += GenDataSizeAfterMB[3];

                    Total.TotalGen0ObjSizeAfterMB += GenDataObjSizeAfterMB[0];
                    Total.TotalGen1ObjSizeAfterMB += GenDataObjSizeAfterMB[1];
                    Total.TotalGen2ObjSizeAfterMB += GenDataObjSizeAfterMB[2];
                    Total.TotalGen3ObjSizeAfterMB += GenDataObjSizeAfterMB[3];

                    Total.MaxGen0SizeAfterMB = Math.Max(Total.MaxGen0SizeAfterMB, GenDataSizeAfterMB[0]);
                    Total.MaxGen1SizeAfterMB = Math.Max(Total.MaxGen1SizeAfterMB, GenDataSizeAfterMB[1]);
                    Total.MaxGen2SizeAfterMB = Math.Max(Total.MaxGen2SizeAfterMB, GenDataSizeAfterMB[2]);
                    Total.MaxLOHSizeAfterMB = Math.Max(Total.MaxLOHSizeAfterMB, GenDataSizeAfterMB[3]);

                    Total.TotalGen0FragmentationMB += _event.GenFragmentationMB(Gens.Gen0);
                    Total.TotalGen1FragmentationMB += _event.GenFragmentationMB(Gens.Gen1);
                    Total.TotalGen2FragmentationMB += _event.GenFragmentationMB(Gens.Gen2);
                    Total.TotalGen3FragmentationMB += _event.GenFragmentationMB(Gens.GenLargeObj);

                    if (_event.GCGeneration == 2)
                    {
                        if (_event.Type == GCType.BackgroundGC)
                        {
                            Total.TotalGen2SizeAfterBgcMB += GenDataSizeAfterMB[2];
                            Total.TotalGen2ObjSizeAfterBgcMB += GenDataObjSizeAfterMB[2];
                        }
                        else
                        {
                            Total.TotalGen2SizeAfterFbMB += GenDataSizeAfterMB[2];
                            Total.TotalGen2ObjSizeAfterFbMB += GenDataObjSizeAfterMB[2];
                        }
                    }

                    if (_event.Type != GCType.BackgroundGC)
                    {
                        double MaxGen0ObjSizeMB = _event.GetMaxGen0ObjSizeMB();
                        if (MaxGen0ObjSizeMB > 6)
                        {
                            if (Total.LargeGen0PerHeapObjSize == null)
                                Total.LargeGen0PerHeapObjSize = new List<GCDataForLargeGen0ObjSize>();

                            Total.LargeGen0PerHeapObjSize.Add(new GCDataForLargeGen0ObjSize(i, MaxGen0ObjSizeMB));
                        }
                    }

                    if ((_event.GCGeneration == 1) && _event.IsLowEphemeral())
                    {
                        if (Gen1SequenceBeginIndex == 0)
                            Gen1SequenceBeginIndex = i;
                        Gen1SequenceEndIndex = i;
                    }
                    else
                    {
                        if ((Gen1SequenceEndIndex - Gen1SequenceBeginIndex) >= 2)
                        {
                            // We detected a lot of continous gen1's due to low ephemeral.
                            if (Total.LowEphemeralGen1Indices == null)
                                Total.LowEphemeralGen1Indices = new List<GCSequenceIndices>();
                            Total.LowEphemeralGen1Indices.Add(new GCSequenceIndices(Gen1SequenceBeginIndex, Gen1SequenceEndIndex));
                        }

                        Gen1SequenceBeginIndex = 0;
                        Gen1SequenceEndIndex = 0;
                    }

                    if ((_event.GCGeneration == 2) &&
                        (_event.Type != GCType.BackgroundGC) &&
                        _event.IsNotCompacting() &&
                        (_event.PerHeapHistories[0].HasMemoryPressure && _event.PerHeapHistories[0].MemoryPressure >= 90))
                    {
                        if (Gen2HighMemSequenceBeginIndex == 0)
                            Gen2HighMemSequenceBeginIndex = i;
                        Gen2HighMemSequenceEndIndex = i;
                    }
                    else
                    {
                        if ((Gen2HighMemSequenceEndIndex - Gen2HighMemSequenceBeginIndex) >= 1)
                        {
                            // We detected multiple full blocking sweeping GCs in high mem situation.
                            if (Total.HighMemSweepingFullGCIndices == null)
                                Total.HighMemSweepingFullGCIndices = new List<GCSequenceIndices>();
                            Total.HighMemSweepingFullGCIndices.Add(new GCSequenceIndices(Gen2HighMemSequenceBeginIndex, Gen2HighMemSequenceEndIndex));
                        }

                        Gen2HighMemSequenceBeginIndex = 0;
                        Gen2HighMemSequenceEndIndex = 0;
                    }

                    if (_event.GCGeneration == 1)
                    {
                        double Allocated = 0;
                        double FreelistConsumed = 0;
                        if (_event.GetFreeListEfficiency(Gens.Gen2, ref Allocated, ref FreelistConsumed))
                        {
                            if (Total.Gen2FreeListEfficiency == null)
                            {
                                Total.Gen2FreeListEfficiency = new GCFreeListEfficiency();
                            }

                            Total.Gen2FreeListEfficiency.GCCount++;
                            Total.Gen2FreeListEfficiency.TotalAllocated += Allocated;
                            Total.Gen2FreeListEfficiency.TotalFreeListConsumed += FreelistConsumed;
                            Total.Gen2FreeListEfficiency.TotalHeapSizeMB += _event.HeapSizeAfterMB;
                        }
                    }

                    if (_event.GCGeneration > 0)
                    {
                        if (_event.PerHeapHistories[0].HasMemoryPressure)
                        {
                            Total.TotalMemoryPressure += _event.PerHeapHistories[0].MemoryPressure;
                            if (Total.MaxMemoryPressure < _event.PerHeapHistories[0].MemoryPressure)
                                Total.MaxMemoryPressure = _event.PerHeapHistories[0].MemoryPressure;
                        }
                    }
                }

                double SuspensionMSec = _event._SuspendDurationMSec;
                Generations[_event.GCGeneration].TotalSuspendDurationMSec += SuspensionMSec;

                if (SuspensionMSec > 10)
                {
                    if (Generations[_event.GCGeneration].LongSuspensionGCIndices == null)
                    {
                        Generations[_event.GCGeneration].LongSuspensionGCIndices = new List<int>();
                    }

                    Generations[_event.GCGeneration].LongSuspensionGCIndices.Add(i);
                }

                if (_event.GCGeneration == (int)Gens.Gen2)
                {
                    if (_event.Type == GCType.BackgroundGC)
                        Total.MaxBGCPause = Math.Max(Total.MaxBGCPause, _event.PauseDurationMSec);
                    else
                    {
                        Total.TotalBlockingGen2Pause += _event.PauseDurationMSec;
                        Total.MaxBlockingGen2Pause = Math.Max(Total.MaxBlockingGen2Pause, _event.PauseDurationMSec);
                    }
                }

                Total.TotalSuspendDurationMSec += _event._SuspendDurationMSec;

                if ((_event.Reason == GCReason.Induced) ||
                    (_event.Reason == GCReason.InducedNotForced))
                {
                    Total.TotalInducedPauseDurationMSec += _event.PauseDurationMSec;
                }

                if (((_event.Reason == GCReason.AllocLarge) || (_event.Reason == GCReason.OutOfSpaceLOH)) && 
                    (_event.GCGeneration == (int)Gens.Gen2))
                {
                    Total.NumLOHTriggered++;
                    Total.TotalInducedPauseDurationMSec += _event.PauseDurationMSec;
                    Total.TotalLOHTriggeredPauseDurationMSec += _event.PauseDurationMSec;
                }

                if (_event.PauseDurationMSec < 10)
                {
                    Generations[_event.GCGeneration].Pause0To10MSec++;
                    Total.Pause0To10MSec++;
                }
                else if (_event.PauseDurationMSec < 30)
                {
                    Generations[_event.GCGeneration].Pause10To30MSec++;
                    Total.Pause10To30MSec++;
                }
                else if (_event.PauseDurationMSec < 50)
                {
                    Generations[_event.GCGeneration].Pause30To50MSec++;
                    Total.Pause30To50MSec++;
                }
                else if (_event.PauseDurationMSec < 75)
                {
                    Generations[_event.GCGeneration].Pause50To75MSec++;
                    Total.Pause50To75MSec++;
                }
                else if (_event.PauseDurationMSec < 100)
                {
                    Generations[_event.GCGeneration].Pause75To100MSec++;
                    Total.Pause75To100MSec++;
                }
                else if (_event.PauseDurationMSec < 200)
                {
                    Generations[_event.GCGeneration].Pause100To200MSec++;
                    Total.Pause100To200MSec++;
                }
                else if (_event.PauseDurationMSec < 500)
                {
                    Generations[_event.GCGeneration].Pause200To500MSec++;
                    Total.Pause200To500MSec++;
                }
                else if (_event.PauseDurationMSec < 1000)
                {
                    Generations[_event.GCGeneration].Pause500To1000MSec++;
                    Total.Pause500To1000MSec++;
                }
                else if (_event.PauseDurationMSec < 3000)
                {
                    Generations[_event.GCGeneration].Pause1000To3000MSec++;
                    Total.Pause1000To3000MSec++;
                }
                else if (_event.PauseDurationMSec < 5000)
                {
                    Generations[_event.GCGeneration].Pause3000To5000MSec++;
                    Total.Pause3000To5000MSec++;
                }
                else
                {
                    Generations[_event.GCGeneration].PauseGreaterThan5000MSec++;
                    Total.PauseGreaterThan5000MSec++;
                }
            }

            Total.AvgMemoryPressure = Total.TotalMemoryPressure / (double)(Generations[1].GCCount + Generations[2].GCCount);
            Total.MeanBlockingGen2Pause = Total.TotalBlockingGen2Pause / Generations[2].NumBlocking;
            Total.MeanBGCPause = (stats.Generations[2].TotalPauseTimeMSec - Total.TotalBlockingGen2Pause) /
                                       (Generations[2].GCCount - Generations[2].NumBlocking);

            ComputeSBAs();
        }

        public void GetInfo(Process p)
        {
            p.ProcessID = (uint)stats.ProcessID;
            p.ProcessName = stats.ProcessName;
            p.CommandLine = stats.CommandLine;
            p.ClrVersion = stats.RuntimeVersion;
            p.Bitness = stats.Bitness;
            p.ProcessElapsedTimeMSec = stats.ProcessDuration;
            p.TotalGCPauseMSec = stats.Total.TotalPauseTimeMSec;

            //General GC data
            p.AvgMemoryPressure = Total.AvgMemoryPressure;
            p.MaxMemoryPressure = Total.MaxMemoryPressure;
            p.NumberOfHeaps = ((stats.heapCount > 0) ? stats.heapCount : 1);
            p.NumberOfLOHGen2s = Total.NumLOHTriggered;

            //Gen0
            p.NumberOfGen0s = stats.Generations[0].GCCount;
            p.NumberOfForegroundGen0s = Generations[0].NumForeground;
            p.NumberOfInducedGen0s = stats.Generations[0].NumInduced;
            p.AvgEndGen0SizeMb = Total.MeanGen0SizeAfterMB;
            p.MaxEndGen0SizeMb = Total.MaxGen0SizeAfterMB;
            p.AvgEndGen0FragmentationMb = Total.MeanGen0FragmentationMB;
            p.Gen0AvgPauseTimeMSec = stats.Generations[0].MeanPauseDurationMSec;
            p.Gen0MaxPauseTimeMSec = stats.Generations[0].MaxPauseDurationMSec;

            //Gen1
            p.NumberOfGen1s = stats.Generations[1].GCCount;
            p.NumberOfForegroundGen1s = Generations[1].NumForeground;
            p.NumberOfInducedGen1s = stats.Generations[1].NumInduced;
            p.AvgEndGen1SizeMb = Total.MeanGen1SizeAfterMB;
            p.MaxEndGen1SizeMb = Total.MaxGen1SizeAfterMB;
            p.AvgEndGen1FragmentationMb = Total.MeanGen1FragmentationMB;
            p.Gen1AvgPauseTimeMSec = stats.Generations[1].MeanPauseDurationMSec;
            p.Gen1MaxPauseTimeMSec = stats.Generations[1].MaxPauseDurationMSec;

            //Gen2
            p.NumberOfGen2s = stats.Generations[2].GCCount;
            p.NumberOfBlockingGen2s = Generations[2].NumBlocking;
            p.NumberOfLowMemoryGen2s = Generations[2].NumLowMemory;
            p.NumberOfInducedGen2s = stats.Generations[2].NumInduced;
            p.NumberOfInducedBlockingGen2s = Generations[2].NumInducedBlocking;
            p.AvgEndGen2SizeMb = Total.MeanGen2SizeAfterMB;
            p.MaxEndGen2SizeMb = Total.MaxGen2SizeAfterMB;
            p.AvgEndLOHSizeMb = Total.MeanLOHSizeAfterMB;
            p.MaxEndLOHSizeMb = Total.MaxLOHSizeAfterMB;
            p.AvgEndGen2FragmentationMb = Total.MeanGen2FragmentationMB;
            p.AvgEndLOHFragmentationMb = Total.MeanLOHFragmentationMB;
            p.Gen2AvgPauseTimeMSec = stats.Generations[2].MeanPauseDurationMSec;
            p.Gen2MaxPauseTimeMSec = stats.Generations[2].MaxPauseDurationMSec;
            p.BlockingGen2AvgPauseTimeMSec = Total.MeanBlockingGen2Pause;
            p.BlockingGen2MaxPauseTimeMSec = Total.MaxBlockingGen2Pause;
            p.BGCAvgPauseTimeMSec = Total.MeanBGCPause;
            p.BGCMaxPauseTimeMSec = Total.MaxBGCPause;

            if (stats.StartupFlags == StartupFlags.None)
            {
                // If we didn't get the runtime start event, we do the best we can report the GC flavor. 
                // Server GC will be reported correctly but for BGC we can only rely on whether we happen to have 
                // seen a BGC.
                p.GCFlavor = (((p.NumberOfGen2s - p.NumberOfBlockingGen2s) > 0) ? "CONCURRENT_GC" : "NONCONCURRENT_GC");
                p.GCFlavor += ((p.NumberOfHeaps > 1) ? ", SERVER_GC" : ", WORKSTATION_GC");
            }
            else
            {
                p.GCFlavor = ((stats.StartupFlags.HasFlag(StartupFlags.CONCURRENT_GC)) ? "CONCURRENT_GC" : "NONCONCURRENT_GC");
                p.GCFlavor += ((stats.StartupFlags.HasFlag(StartupFlags.SERVER_GC)) ? ", SERVER_GC" : ", WORKSTATION_GC");
            }

            p.NumberOfGen0sWithPause0To10MSec = Generations[0].Pause0To10MSec;
            p.NumberOfGen0sWithPause10To30MSec = Generations[0].Pause10To30MSec;
            p.NumberOfGen0sWithPause30To50MSec = Generations[0].Pause30To50MSec;
            p.NumberOfGen0sWithPause50To75MSec = Generations[0].Pause50To75MSec;
            p.NumberOfGen0sWithPause75To100MSec = Generations[0].Pause75To100MSec;
            p.NumberOfGen0sWithPause100To200MSec = Generations[0].Pause100To200MSec;
            p.NumberOfGen0sWithPause200To500MSec = Generations[0].Pause200To500MSec;
            p.NumberOfGen0sWithPause500To1000MSec = Generations[0].Pause500To1000MSec;
            p.NumberOfGen0sWithPause1000To3000MSec = Generations[0].Pause1000To3000MSec;
            p.NumberOfGen0sWithPause3000To5000MSec = Generations[0].Pause3000To5000MSec;
            p.NumberOfGen0sWithPauseGreaterThan5000MSec = Generations[0].PauseGreaterThan5000MSec;

            p.NumberOfGen1sWithPause0To10MSec = Generations[1].Pause0To10MSec;
            p.NumberOfGen1sWithPause10To30MSec = Generations[1].Pause10To30MSec;
            p.NumberOfGen1sWithPause30To50MSec = Generations[1].Pause30To50MSec;
            p.NumberOfGen1sWithPause50To75MSec = Generations[1].Pause50To75MSec;
            p.NumberOfGen1sWithPause75To100MSec = Generations[1].Pause75To100MSec;
            p.NumberOfGen1sWithPause100To200MSec = Generations[1].Pause100To200MSec;
            p.NumberOfGen1sWithPause200To500MSec = Generations[1].Pause200To500MSec;
            p.NumberOfGen1sWithPause500To1000MSec = Generations[1].Pause500To1000MSec;
            p.NumberOfGen1sWithPause1000To3000MSec = Generations[1].Pause1000To3000MSec;
            p.NumberOfGen1sWithPause3000To5000MSec = Generations[1].Pause3000To5000MSec;
            p.NumberOfGen1sWithPauseGreaterThan5000MSec = Generations[1].PauseGreaterThan5000MSec;

            p.NumberOfGen2sWithPause0To10MSec = Generations[2].Pause0To10MSec;
            p.NumberOfGen2sWithPause10To30MSec = Generations[2].Pause10To30MSec;
            p.NumberOfGen2sWithPause30To50MSec = Generations[2].Pause30To50MSec;
            p.NumberOfGen2sWithPause50To75MSec = Generations[2].Pause50To75MSec;
            p.NumberOfGen2sWithPause75To100MSec = Generations[2].Pause75To100MSec;
            p.NumberOfGen2sWithPause100To200MSec = Generations[2].Pause100To200MSec;
            p.NumberOfGen2sWithPause200To500MSec = Generations[2].Pause200To500MSec;
            p.NumberOfGen2sWithPause500To1000MSec = Generations[2].Pause500To1000MSec;
            p.NumberOfGen2sWithPause1000To3000MSec = Generations[2].Pause1000To3000MSec;
            p.NumberOfGen2sWithPause3000To5000MSec = Generations[2].Pause3000To5000MSec;
            p.NumberOfGen2sWithPauseGreaterThan5000MSec = Generations[2].PauseGreaterThan5000MSec;
            
            //Add SBA's
            if (SBAs.Count > 0)
            {
                int IssueCount = 0;
                foreach (SBA sba in SBAs)
                {
                    if (sba.IsIssue)
                        IssueCount++;
                }
                int FYICount = SBAs.Count - IssueCount;

                Issue[] Issues = new Issue[IssueCount];
                Issue[] FYIs = new Issue[FYICount];

                int issueIndex = 0;
                int fyiIndex = 0;
                foreach (SBA sba in SBAs)
                {
                    if (sba.IsIssue)
                    {
                        Issues[issueIndex] = new Issue();
                        Issues[issueIndex].Name = sba.Type.ToString();
                        Issues[issueIndex].Details = sba.DetailedData;
                        Issues[issueIndex].PauseResourceValue = sba.PauseResourceValue;
                        Issues[issueIndex].SizeResourceValue = sba.SizeResourceValue;
                        Issues[issueIndex].RatioResourceValue = sba.RatioResourceValue;
                        Issues[issueIndex].MetricEffects = sba.MetricEffects;
                        Issues[issueIndex].DataPoints = sba.DataPoints;
                        issueIndex++;
                    }
                    else
                    {
                        FYIs[fyiIndex] = new Issue();
                        FYIs[fyiIndex].Name = sba.Type.ToString();
                        FYIs[fyiIndex].Details = sba.DetailedData;
                        FYIs[fyiIndex].PauseResourceValue = sba.PauseResourceValue;
                        FYIs[fyiIndex].SizeResourceValue = sba.SizeResourceValue;
                        FYIs[fyiIndex].RatioResourceValue = sba.RatioResourceValue;
                        FYIs[fyiIndex].MetricEffects = sba.MetricEffects;
                        FYIs[fyiIndex].DataPoints = sba.DataPoints;
                        fyiIndex++;
                    }
                }
                p.SBAs = new SBAs();
                p.SBAs.Issues = Issues;
                p.SBAs.FYIs = FYIs;
            }
        }
    }

    class CAP
    {
        public static string[] SBASolutionTypeDescription = new string[]
        {
            "[CLR has identified this as an issue but no fix has been considered, contact CLR if this is urgent for you]",
            "[CLR has a fix for this]",
            "[CLR has logged a workitem for this]",
            "[Workaround available]",
            "[This is an action item on the user side]",
            "[This is FYI, no action needed]"
        };

        public static void GenerateCAPReport(ProcessLookup<GCProcess> gcStats, CAPAnalysis report)
        {
            int processCount = 0;
            foreach (GCProcess GCProcessData in gcStats)
            {
                if (GCProcessData.Total.GCCount != 0)
                    processCount++;
            }

            if (processCount == 0)
                return;

            report.Processes = new Process[processCount];

            int count = 0;
            foreach (GCProcess stats in gcStats)
            {
                if (stats.Total.GCCount != 0)
                {
                    // First process the extra info we need for CAP.
                    CapProcess capProcess = new CapProcess(stats);
                    capProcess.ProcessInfo();

                    // We are done processing the events. Now get the stats for the report.
                    Process p = new Process();
                    capProcess.GetInfo(p);

                    report.Processes[count] = p;
                    count++;
                }
            }
        }

        public static void GenerateCAPReport(ProcessLookup<JitCapProcess> jitStats, JitCapAnalysis report, int numTopMethods)
        {
            int count = 0;
            foreach (JitCapProcess stats in jitStats)
            {
                if (stats.SymbolsMissing.Count != 0 || stats.MethodCounts.Count != 0)
                {
                    count++;
                }
            }

            report.JitCapData = new JitCapData[count];
            
            count = 0;
            foreach (JitCapProcess stats in jitStats)
            {
                if (stats.SymbolsMissing.Count != 0 || stats.MethodCounts.Count != 0)
                {
                    CapJitProcessor capJitProcessor = new CapJitProcessor(stats);
                    report.JitCapData[count] = capJitProcessor.ProcessInfo(numTopMethods);
                    count++;
                }
            }
        }
    }
}

#endif

