# Fragmentation Blame View

## Overview

The **Fragmentation Blame View** is a new analysis view in PerfView for GC dump files (.gcDump) that helps identify which objects in the managed heap are causing memory fragmentation. This view is particularly useful for understanding why the garbage collector cannot compact memory effectively.

## What is Heap Fragmentation?

Heap fragmentation occurs when there are gaps (free spaces) between objects in memory. These gaps are represented as "Free" objects in the GC heap. Fragmentation happens when:

1. **Pinned objects** prevent the GC from moving surrounding objects during compaction
2. **Objects in older generations** create barriers that prevent younger generation objects from being compacted
3. **Interop scenarios** where unmanaged code holds references to managed objects

## How the View Works

The Fragmentation Blame view uses the following algorithm:

1. **Identifies Free objects**: Scans the heap for all objects with the type name "Free" (gaps in memory)
2. **Finds predecessors**: For each Free object, identifies the object immediately before it in memory (sorted by address)
3. **Attributes blame**: The size of each Free object is attributed as "fragmentation cost" to the preceding object
4. **Shows paths to root**: Displays the complete path from the root to each blamed object, helping you understand why these objects exist

## Key Insights

Objects that appear in this view are likely:

- **Pinned objects**: Objects that have been pinned (e.g., using GCHandle.Alloc with GCHandleType.Pinned)
- **Long-lived objects**: Objects in Gen2 or LOH that survived many GCs
- **Array buffers**: Large arrays used for interop or I/O operations
- **Static fields**: Objects referenced by static fields that never get collected

## How to Use

1. Open a .gcDump file in PerfView
2. Expand the file node in the tree
3. Navigate to **Advanced Group** → **Fragmentation Blame**
4. Double-click to open the view

## Interpreting the Results

### Metric (Size)
The "Metric" column shows the **total fragmentation cost** (in bytes) caused by each object. This is the sum of all Free objects that immediately follow this object in memory.

### Call Tree
The call tree shows:
- The blamed object's type at the bottom
- The path from the root showing what keeps this object alive
- Aggregated costs for types and paths

### Tips for Analysis

1. **Sort by Exc (Exclusive) size**: This shows which individual objects cause the most fragmentation
2. **Sort by Inc (Inclusive) size**: This shows which types or paths cause the most fragmentation in aggregate
3. **Look for patterns**: If many objects of the same type appear, consider:
   - Reducing pinning duration (unpin as soon as possible)
   - Using `fixed` statements instead of GCHandle for short-lived pins
   - Pooling and reusing pinned buffers
   - Reducing object lifetimes so they don't promote to Gen2

## Example Scenarios

### Scenario 1: Pinned Buffers
```
Inc (%)  Exc (%)  Name
  45.2%    12.3%  System.Byte[]
   12.3%   12.3%    [Pinned Handle]
   33.0%    8.2%    MyApp.BufferPool
    8.2%    8.2%      [Static Variable: MyApp.BufferPool.s_instance]
```
**Interpretation**: Pinned byte arrays are causing 45% of fragmentation. The BufferPool is holding onto pinned buffers.

**Action**: Review the BufferPool implementation to ensure buffers are unpinned when not in use, or consider using ArrayPool<T> which handles this automatically.

### Scenario 2: Long-Lived Objects in Gen2
```
Inc (%)  Exc (%)  Name
  38.5%   38.5%  MyApp.CacheEntry
   38.5%   38.5%    System.Collections.Generic.Dictionary<K,V>
   38.5%   38.5%      [Static Variable: MyApp.Cache.s_cache]
```
**Interpretation**: Cache entries in Gen2 are preventing compaction of surrounding memory.

**Action**: Consider implementing cache eviction policies to reduce the lifetime of cached objects, or use WeakReference for cache entries.

## Technical Details

### Implementation
- **File**: `src/PerfView/memory/FragmentationBlameStackSource.cs`
- **Integration**: `src/PerfView/PerfViewData.cs` (HeapDumpPerfViewFile class)

### Algorithm Complexity
- **Time**: O(n log n) where n is the number of objects (due to sorting by address)
- **Space**: O(n) for storing the blame mapping

### Limitations
1. Only works with .gcDump files (not ETL files)
2. Requires that Free objects are present in the dump (some dump methods may not preserve them)
3. Shows blame for the object *immediately before* each Free object, which may not always be the root cause
4. Does not account for alignment padding or other internal GC structures

## Related Views

- **Heap**: Shows all objects by size (the default view)
- **Gen 0/1/2 Walkable Objects**: Shows only objects in specific generations
- **Pinned Object Analysis**: Another tool for analyzing pinning (if available)

## References

- [Understanding Garbage Collection](https://learn.microsoft.com/dotnet/standard/garbage-collection/)
- [Pinning in .NET](https://learn.microsoft.com/dotnet/api/system.runtime.interopservices.gchandle)
- [PerfView Memory Analysis Guide](TraceEvent/TraceEventProgrammersGuide.md)

## Feedback and Contributions

This is a new feature. If you encounter issues or have suggestions for improvements, please file an issue on the [PerfView GitHub repository](https://github.com/microsoft/perfview).
