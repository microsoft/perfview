internal static class GCHeapDumpNames
{
    internal const string GCHeapsTitle = "GC Heaps";
    internal const string DotNetRootsTitle = ".NET Roots";
    
    internal const string StaticVarsRootTitle = "static vars";
    internal const string ThreadStaticVarsRootTitle = "thread static vars";
    internal const string OtherHandlesRootTitle = "other Handles";
    internal const string DependentHandlesRootTitle = "Dependent Handles";
    internal const string LocalVarsRootTitle = "local vars";
    internal const string PinnedLocalVarsRootTitle = "Pinned local vars";
    internal const string COMWinRTRootTitle = "COM/WinRT Objects";
    internal const string OtherRootsTitle = "other roots";

    internal const string FinalizerQueueRootTitle = "FinalizerQueue";
    internal const string StrongHandleRootTitle = "StrongHandle";
    internal const string PinnedHandleRootTitle = "PinnedHandle";
    internal const string StackRootTitle = "Stack";
    internal const string RefCountedHandleRootTitle = "RefCountedHandle";
    internal const string AsyncPinnedHandleRootTitle = "AsyncPinnedHandle";
    internal const string SizedRefHandleRootTitle = "SizedRefHandle";

    internal const string StaticVarPrefix = "static var";
    internal const string CCWPrefix = "CCW";
    internal const string RCWPrefix = "RCW";

    internal static string Bracket(string s)
    {
        return $"[{s}]";
    }
}