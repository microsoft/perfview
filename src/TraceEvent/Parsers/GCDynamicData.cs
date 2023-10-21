namespace Microsoft.Diagnostics.Tracing.Parsers.GCDynamicData
{
    public sealed class CommittedUsageTraceData
    {
        public short Version { get; internal set; }
        public long TotalCommittedInUse { get; internal set; }
        public long TotalCommittedInGlobalDecommit { get; internal set; }
        public long TotalCommittedInFree { get; internal set; }
        public long TotalCommittedInGlobalFree { get; internal set; }
        public long TotalBookkeepingCommitted { get; internal set; }
    }
    public sealed class HeapCountTuningTraceData
    {
        public short Version { get; internal set; }
        public short NewHeapCount { get; internal set; }
        public long GCIndex { get; internal set; }
        public float MedianPercentOverhead { get; internal set; }
        public float SmoothedMedianPercentOverhead { get; internal set; }
        public float OverheadReductionPerStepUp { get; internal set; }
        public float OverheadIncreasePerStepDown { get; internal set; }
        public float SpaceCostIncreasePerStepUp { get; internal set; }
        public float SpaceCostDecreasePerStepDown { get; internal set; }
    }
    public sealed class HeapCountSampleTraceData
    {
        public short Version { get; internal set; }
        public long GCElapsedTime { get; internal set; }
        public long SOHMslWaitTime { get; internal set; }
        public long UOHMslWaitTime { get; internal set; }
        public long ElapsedBetweenGCs { get; internal set; }
    }
}
