namespace Microsoft.Diagnostics.Tracing.Parsers.GCDynamicData
{
    public sealed class CommittedUsage
    {
        public short Version { get; internal set; }
        public long TotalCommittedInUse { get; internal set; }
        public long TotalCommittedInGlobalDecommit { get; internal set; }
        public long TotalCommittedInFree { get; internal set; }
        public long TotalCommittedInGlobalFree { get; internal set; }
        public long TotalBookkeepingCommitted { get; internal set; }
    }

    public sealed class HeapCountTuning
    {
        public short Version { get; internal set; }
        public short NewHeapCount { get; internal set; }
        public long GCIndex { get; internal set; }
        public float MedianThroughputCostPercent { get; internal set; }
        public float SmoothedMedianThroughputCostPercent { get; internal set; }
        public float ThroughputCostPercentReductionPerStepUp { get; internal set; }
        public float ThroughputCostPercentIncreasePerStepDown { get; internal set; }
        public float SpaceCostPercentIncreasePerStepUp { get; internal set; }
        public float SpaceCostPercentDecreasePerStepDown { get; internal set; }
    }
    
    public sealed class HeapCountSample
    {
        public short Version { get; internal set; }
        public long GCIndex { get; internal set; }
        public long ElapsedTimeBetweenGCs { get; internal set; }
        public long GCPauseTime { get; internal set; }
        public long MslWaitTime { get; internal set; }
    }
}
