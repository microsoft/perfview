using System;


namespace ETLStackBrowse
{
    public interface INamedFilter
    {
        bool this[int index] { get; set; }
        bool this[string index] { get; set; }
        void Clear();
        void SetAll();
        int Count { get; }
        bool IsValidKey(string key);
        bool[] GetFilters();
    }

    public interface IIndexedFilter
    {
        bool this[int index] { get; set; }
        void Clear();
        void SetAll();
        int Count { get; }
    }

    public interface IStackParameters
    {
        bool SkipThunks { get; set; }
        bool UseExeFrame { get; set; }
        bool UsePid { get; set; }
        bool UseTid { get; set; }
        bool FoldModules { get; set; }
        bool UseRootAI { get; set; }
        bool ShowWhen { get; set; }
        bool UseIODuration { get; set; }
        bool AnalyzeReservedMemory { get; set; }
        bool IndentLess { get; set; }
        double MinInclusive { get; set; }
        string FrameFilters { get; set; }
        string ButterflyPivot { get; set; }
    }

    public interface IRollupParameters
    {
        string RollupCommand { get; set; }
        int RollupTimeIntervals { get; set; }
    }

    public interface IContextSwitchParameters
    {
        bool SimulateHyperthreading { get; set; }
        bool SortBySwitches { get; set; }
        bool ComputeReasons { get; set; }
        int TopThreadCount { get; set; }
    }

    public interface ITraceParameters
    {
        INamedFilter EventFilters { get; }
        INamedFilter StackFilters { get; }
        IIndexedFilter ThreadFilters { get; }
        IIndexedFilter ProcessFilters { get; }

        IStackParameters StackParameters { get; }
        IRollupParameters RollupParameters { get; }
        IContextSwitchParameters ContextSwitchParameters { get; }

        String MemoryFilters { get; set; }

        bool[] GetProcessFilters();
        bool[] GetThreadFilters();
        String FilterText { get; set; }

        long T0 { get; set; }
        long T1 { get; set; }
        bool EnableThreadFilter { get; set; }
        bool EnableProcessFilter { get; set; }

        bool UnmangleBartokSymbols { get; set; }
        bool ElideGenerics { get; set; }
    }

    public interface ITraceUINotify
    {
        void ClearZoomedTimes();
        void ClearEventFields();
        void AddEventField(string s);
        void AddEventToStackEventList(string s);
        void AddEventToEventList(string s);
        void AddThreadToThreadList(string s);
        void AddProcessToProcessList(string s);
        void AddTimeToTimeList(string s);
        void AddTimeToZoomedTimeList(string s);
    }

    public class StackParameters : IStackParameters
    {
        private bool fSkipThunks = true;
        private bool fUseExeFrame = true;
        private bool fUsePid = true;
        private bool fUseTid = true;
        private bool fFoldModules = false;
        private bool fUseRootAI = false;
        private bool fShowWhen = true;
        private bool fUseIODuration = false;
        private bool fIndentLess = true;
        private bool fAnalyzeReservedMemory = false;
        private double minIncl = 2.0;
        private string frameFilters = "";
        private string butterflyPivot = "";

        public bool SkipThunks { get { return fSkipThunks; } set { fSkipThunks = value; } }
        public bool UseExeFrame { get { return fUseExeFrame; } set { fUseExeFrame = value; } }
        public bool UsePid { get { return fUsePid; } set { fUsePid = value; } }
        public bool UseTid { get { return fUseTid; } set { fUseTid = value; } }
        public bool FoldModules { get { return fFoldModules; } set { fFoldModules = value; } }
        public bool UseRootAI { get { return fUseRootAI; } set { fUseRootAI = value; } }
        public bool ShowWhen { get { return fShowWhen; } set { fShowWhen = value; } }
        public bool UseIODuration { get { return fUseIODuration; } set { fUseIODuration = value; } }
        public bool AnalyzeReservedMemory { get { return fAnalyzeReservedMemory; } set { fAnalyzeReservedMemory = value; } }
        public bool IndentLess { get { return fIndentLess; } set { fIndentLess = value; } }
        public double MinInclusive { get { return minIncl; } set { minIncl = value; } }
        public string FrameFilters { get { return frameFilters; } set { frameFilters = value; } }
        public string ButterflyPivot { get { return butterflyPivot; } set { butterflyPivot = value; } }

        public StackParameters() { }
    }

    public class RollupParameters : IRollupParameters
    {
        private string rollupCommand = "";
        private int rollupIntervals = 20;

        public string RollupCommand { get { return rollupCommand; } set { rollupCommand = value; } }
        public int RollupTimeIntervals { get { return rollupIntervals; } set { rollupIntervals = value; } }

        public RollupParameters() { }
    }

    public class ContextSwitchParameters : IContextSwitchParameters
    {
        private bool fSimulateHyperthreading = false;
        private bool fSortBySwitches = false;
        private bool fComputeReasons = false;
        private int nTop = 0;

        public bool SimulateHyperthreading { get { return fSimulateHyperthreading; } set { fSimulateHyperthreading = value; } }
        public bool SortBySwitches { get { return fSortBySwitches; } set { fSortBySwitches = value; } }
        public bool ComputeReasons { get { return fComputeReasons; } set { fComputeReasons = value; } }
        public int TopThreadCount { get { return nTop; } set { nTop = value; } }

        public ContextSwitchParameters() { }
    }

    public class TraceParameters : ITraceParameters
    {
        private AtomFilter eventfilter;
        private AtomFilter stackfilter;
        private IndexFilter processfilter;
        private IndexFilter threadfilter;
        private IStackParameters stackParameters;
        private IRollupParameters rollupParameters;
        private IContextSwitchParameters contextSwitchParameters;

        public TraceParameters(ETLTrace t)
        {
            eventfilter = new AtomFilter(t.RecordAtoms);
            stackfilter = new AtomFilter(t.RecordAtoms);
            processfilter = new IndexFilter(t.Processes.Count);
            threadfilter = new IndexFilter(t.Threads.Count);
            stackParameters = new StackParameters();
            rollupParameters = new RollupParameters();
            contextSwitchParameters = new ContextSwitchParameters();
            contextSwitchParameters.TopThreadCount = t.Threads.Count;

            FilterText = "";
            MemoryFilters = "";
            T0 = 0;
            T1 = t.TMax;
            RollupTimeIntervals = 20;
        }

        public INamedFilter EventFilters { get { return eventfilter; } }
        public INamedFilter StackFilters { get { return stackfilter; } }
        public IIndexedFilter ThreadFilters { get { return threadfilter; } }
        public IIndexedFilter ProcessFilters { get { return processfilter; } }
        public IStackParameters StackParameters { get { return stackParameters; } }
        public IRollupParameters RollupParameters { get { return rollupParameters; } }
        public IContextSwitchParameters ContextSwitchParameters { get { return contextSwitchParameters; } }

        public bool[] GetProcessFilters()
        {
            if (!EnableProcessFilter)
            {
                var ret = new bool[processfilter.Count];

                for (int i = 0; i < ret.Length; i++)
                {
                    ret[i] = true;
                }

                return ret;
            }
            return processfilter.GetFilters();
        }

        public bool[] GetThreadFilters()
        {
            if (!EnableThreadFilter)
            {
                var ret = new bool[threadfilter.Count];

                for (int i = 0; i < ret.Length; i++)
                {
                    ret[i] = true;
                }

                return ret;
            }

            return threadfilter.GetFilters();
        }

        public String FilterText { get; set; }
        public long T0 { get; set; }
        public long T1 { get; set; }
        public string MemoryFilters { get; set; }
        public bool EnableThreadFilter { get; set; }
        public bool EnableProcessFilter { get; set; }
        public int RollupTimeIntervals { get; set; }
        public bool ElideGenerics { get; set; }
        public bool UnmangleBartokSymbols { get; set; }
    }

    public class IndexFilter : IIndexedFilter
    {
        private bool[] filters;

        public IndexFilter(int count)
        {
            filters = new bool[count];
        }

        public void Clear()
        {
            for (int i = 0; i < filters.Length; i++)
            {
                filters[i] = false;
            }
        }

        public void SetAll()
        {
            for (int i = 0; i < filters.Length; i++)
            {
                filters[i] = true;
            }
        }

        public bool this[int index]
        {
            get
            {
                return filters[index];
            }
            set
            {
                filters[index] = value;
            }
        }

        public int Count
        {
            get
            {
                return filters.Length;
            }
        }

        public bool[] GetFilters()
        {
            return filters;
        }
    }

    public class AtomFilter : INamedFilter
    {
        private ByteAtomTable atoms;
        private bool[] filters;

        public AtomFilter(ByteAtomTable atoms)
        {
            this.atoms = atoms;
            filters = new bool[atoms.Count];
        }

        public void Clear()
        {
            for (int i = 0; i < filters.Length; i++)
            {
                filters[i] = false;
            }
        }

        public void SetAll()
        {
            for (int i = 0; i < filters.Length; i++)
            {
                filters[i] = true;
            }
        }

        public bool this[int index]
        {
            get
            {
                return filters[index];
            }
            set
            {
                filters[index] = value;
            }
        }

        public bool this[string key]
        {
            get
            {
                int i = atoms.Lookup(key);
                if (i < 0)
                {
                    return false;
                }

                return filters[i];
            }
            set
            {
                int i = atoms.Lookup(key);
                if (i < 0)
                {
                    return;
                }

                filters[i] = value;
            }
        }

        public bool IsValidKey(string key)
        {
            return atoms.Lookup(key) >= 0;
        }

        public int Count
        {
            get
            {
                return filters.Length;
            }
        }

        public bool[] GetFilters()
        {
            return filters;
        }
    }
}
