using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Parsers.ETWClrProfiler;
using Microsoft.Diagnostics.Tracing.Stacks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Address = System.UInt64;

namespace PerfView
{
    /// <summary>
    /// GCHeapSimulators manages all GCHeapSimulator on the machine (one for each process with a GC heap).  It basically is a collection
    /// of the simulators organized by process.   You can enumerate them and index them by process.  
    /// </summary>
    public class GCHeapSimulators : IEnumerable<GCHeapSimulator>
    {
        public GCHeapSimulators(TraceLog traceLog, TraceEventDispatcher source, MutableTraceEventStackSource stackSource, TextWriter log)
        {
            m_simulators = new GCHeapSimulator[traceLog.Processes.Count];
            m_source = source;
            m_stackSource = stackSource;
            m_log = log;

            // Save a symbol resolver for this trace log.
            s_typeNameSymbolResolvers[traceLog.FilePath] = new TypeNameSymbolResolver(traceLog.FilePath, log);

            var etwClrProfileTraceEventParser = new ETWClrProfilerTraceEventParser(source);
            etwClrProfileTraceEventParser.ClassIDDefintion += CheckForNewProcess;
            source.Clr.GCSampledObjectAllocation += CheckForNewProcess;
            source.Clr.GCAllocationTick += CheckForNewProcessForTick;
        }
        /// <summary>
        /// If set (before the source is processed), indicates that only the GC Allocation Ticks (100K samples) should be used 
        /// in the analysis even if other object allocation events are present.   
        /// </summary>
        public bool UseOnlyAllocTicks { get; set; }

        public GCHeapSimulator this[TraceProcess process]
        {
            get
            {
                var ret = m_simulators[(int)process.ProcessIndex];
                if (ret == null)
                {
                    m_simulators[(int)process.ProcessIndex] = ret = CreateNewSimulator(process);
                }

                return ret;
            }
        }

        internal static Dictionary<string, TypeNameSymbolResolver> TypeNameSymbolResolvers
        {
            get { return s_typeNameSymbolResolvers; }
        }

        /// <summary>
        /// If you wish to get control when a new Heap Simulator is activated, set this.   
        /// </summary>
        public Action<GCHeapSimulator> OnNewGCHeapSimulator;

        #region private

        private void CheckForNewProcessForTick(GCAllocationTickTraceData data)
        {
            if (UseOnlyAllocTicks)
            {
                CheckForNewProcess(data);
            }
        }

        private void CheckForNewProcess(TraceEvent data)
        {
            var process = data.Process();
            if (process != null)
            {
                if (m_simulators[(int)process.ProcessIndex] == null)
                {
                    m_simulators[(int)process.ProcessIndex] = CreateNewSimulator(process);
                }
            }
        }

        private GCHeapSimulator CreateNewSimulator(TraceProcess process)
        {
            var ret = new GCHeapSimulator(m_source, process, m_stackSource, m_log, UseOnlyAllocTicks);
            OnNewGCHeapSimulator?.Invoke(ret);
            return ret;
        }

        private TraceEventDispatcher m_source;
        private GCHeapSimulator[] m_simulators;
        private MutableTraceEventStackSource m_stackSource;
        private TextWriter m_log;

        // Map of FilePath to symbol resolvers.
        private static Dictionary<string, TypeNameSymbolResolver> s_typeNameSymbolResolvers = new Dictionary<string, TypeNameSymbolResolver>();

        IEnumerator<GCHeapSimulator> IEnumerable<GCHeapSimulator>.GetEnumerator()
        {
            foreach (var simulator in m_simulators)
            {
                if (simulator != null)
                {
                    yield return simulator;
                }
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
        #endregion
    }

    /// <summary>
    /// GCHeapSimulator is designed to take the allocation and GC events from 'source' from process 'processID' and simulate 
    /// their effect on the GC heap in the process.  As events come in the 'AllObjects, GetObject, and CurrentHeapSize
    /// track what should be in the GC heap at that point int time.  It will also issue callbacks on every object creation
    /// and destruction if the client subscribes to them.  
    /// 
    /// Objects in this simulator are GCHeapSimulatorObject which remember their type, 
    /// This class is designed to be subclassed 
    /// </summary>
    public class GCHeapSimulator
    {
        /// <summary>
        /// Create a GC simulation using the events from 'source' for the process with process ID 'processID'.  
        /// The stacks associated with allocation are added to 'stackSource' (and a pseudo-frame with the type
        /// is added to the stacks for each allocation.   If useOnlyAllocationTicks, it will not use 
        /// either the etwClrProfiler or GCSampledObjectAllocation as allocation events (so you can reliably 
        /// get a coarse sampled simulation independent of what other events are in the trace).  
        /// </summary>
        public GCHeapSimulator(TraceEventDispatcher source, TraceProcess process, MutableTraceEventStackSource stackSource, TextWriter log, bool useOnlyAllocationTicks = false)
        {
            m_processID = process.ProcessID;
            m_process = process;
            m_pointerSize = 4;          // We guess this,  It is OK for this to be wrong.
            m_typeNameSymbolResolver = GCHeapSimulators.TypeNameSymbolResolvers[process.Log.FilePath];
            if (m_typeNameSymbolResolver == null)
            {
                m_typeNameSymbolResolver = GCHeapSimulators.TypeNameSymbolResolvers[process.Log.FilePath] = new TypeNameSymbolResolver(process.Log.FilePath, log);
            }

            m_ObjsInGen = new Dictionary<Address, GCHeapSimulatorObject>[4];   // generation 0-2 + 1 for gen 2    
            for (int i = 0; i < m_ObjsInGen.Length; i++)
            {
                m_ObjsInGen[i] = new Dictionary<Address, GCHeapSimulatorObject>(10000);        // Stays in small object heap 
            }

            m_classNamesAsFrames = new Dictionary<ulong, TypeInfo>(500);
            m_stackSource = stackSource;

            AllocateObject = () => new GCHeapSimulatorObject();

            // Register all the callbacks.
            if (!useOnlyAllocationTicks)
            {
                var etwClrProfilerTraceEventParser = new ETWClrProfilerTraceEventParser(source);
                etwClrProfilerTraceEventParser.GCStart += delegate (Microsoft.Diagnostics.Tracing.Parsers.ETWClrProfiler.GCStartArgs data)
                {

                    m_useEtlClrProfilerEvents = true;
                    OnGCStart(data, data.GCID, data.Generation);
                };
                etwClrProfilerTraceEventParser.ObjectAllocated += delegate (ObjectAllocatedArgs data)
                {
                    m_useEtlClrProfilerEvents = true;
                    OnObjectAllocated(data, data.ObjectID, data.ClassID, data.Size, data.RepresentativeSize);
                };
                etwClrProfilerTraceEventParser.ObjectsMoved += OnObjectMoved;
                etwClrProfilerTraceEventParser.ObjectsSurvived += OnObjectSurvived;
                etwClrProfilerTraceEventParser.GCStop += delegate (GCStopArgs data) { OnGCStop(data, data.GCID); };
                etwClrProfilerTraceEventParser.ClassIDDefintion += OnClassIDDefintion;
                // TODO do we need module info?
                // etwClrProfileTraceEventParser.ModuleIDDefintion += OnModuleIDDefintion;            
            }

            source.Clr.GCStart += delegate (Microsoft.Diagnostics.Tracing.Parsers.Clr.GCStartTraceData data)
            {
                if (m_useEtlClrProfilerEvents)
                {
                    return;
                }

                OnGCStart(data, data.Count, data.Depth);
            };
            source.Clr.GCStop += delegate (GCEndTraceData data)
            {
                if (m_useEtlClrProfilerEvents)
                {
                    return;
                }

                OnGCStop(data, data.Count);
            };
            source.Clr.GCBulkMovedObjectRanges += OnEtwObjectMoved;
            source.Clr.GCBulkSurvivingObjectRanges += OnEtwObjectSurvived;
            source.Clr.TypeBulkType += OnEtwClassIDDefintion;

            if (!useOnlyAllocationTicks)
            {
                source.Clr.GCSampledObjectAllocation += delegate (GCSampledObjectAllocationTraceData data)
                {
                    //  Compute size per object, Projecting against divide by zero.  
                    long representativeSize = data.TotalSizeForTypeSample;
                    var objectCount = data.ObjectCountForTypeSample;
                    if (objectCount > 1)
                    {
                        representativeSize = representativeSize / objectCount;
                    }

                    OnObjectAllocated(data, data.Address, data.TypeID, data.TotalSizeForTypeSample, representativeSize);
                };
            }
            else
            {
                source.Clr.GCAllocationTick += OnEtwGCAllocationTick;
            }
        }

        /// <summary>
        /// The size of all allocated but not collected by the GC at the current time (does not include fragmented free space).  
        /// </summary>
        public long CurrentHeapSize { get { return (long)m_currentHeapSize; } }

        /// <summary>
        /// Fetches the information we know about a particular object given its address in memory.  Note that our
        /// understanding of what generation an object is in may not be completely in sync with the GCs.  
        /// </summary>
        public GCHeapSimulatorObject GetObjectInfo(Address objectAddress, out int gen)
        {
            GCHeapSimulatorObject ret = null;
            // Start in Gen 2 since you most objects will be there.  
            for (int i = 2; 0 <= i; --i)
            {
                if (m_ObjsInGen[i].TryGetValue(objectAddress, out ret))
                {
                    gen = i;
                    return ret;
                }
            }
            gen = 0;
            return ret;
        }
        public GCHeapSimulatorObject GetObjectInfo(Address objectAddress)
        {
            int gen;
            return GetObjectInfo(objectAddress, out gen);
        }

        /// <summary>
        /// Indicates that 'objectAddress' should be tracked (so that later you can do a 'GetObjectInfo' on it.   
        /// Returns the current simulation object for it.  
        /// </summary>
        public GCHeapSimulatorObject TrackObject(Address objectAddress)
        {
            var ret = GetObjectInfo(objectAddress);
            if (ret == null)
            {
                ret = AllocateObject();
                m_ObjsInGen[2][objectAddress] = ret;        // Because we don't know we put it in gen 2, so we don't kill it prematurely.   
            }
            return ret;
        }

        /// <summary>
        /// Allows you to enumerate all objects on the heap at the current point in time.  
        /// </summary>
        private IEnumerable<KeyValuePair<Address, GCHeapSimulatorObject>> AllObjects
        {
            get
            {
                for (int i = 2; 0 <= i; --i)
                {
                    foreach (var keyValue in m_ObjsInGen[i])
                    {
                        yield return keyValue;
                    }
                }
            }
        }

        /// <summary>
        /// The process of interest.  Events from other processes are ignored.
        /// </summary>
        public TraceProcess Process { get { return m_process; } }

        /// <summary>
        /// The stack source where allocation stacks are interned.  
        /// </summary>
        public MutableTraceEventStackSource StackSource { get { return m_stackSource; } }

        // callback hooks. 
        /// <summary>
        /// If you are interested in hooking when objects get created, override this delegate.  It is given the object address
        /// and its information, and returns true if that object should be tracked (otherwise it is discarded)
        /// </summary>
        public Func<Address, GCHeapSimulatorObject, bool> OnObjectCreate;
        /// <summary>
        /// If you are interested in hook in when objects are destroyed, override this delegate.  I
        /// The callback is given 
        ///    * the time of the GC
        ///    * the generation being collected
        ///    * the object address
        ///    * The object information (where it was allocated)
        /// </summary>
        public Action<double, int, Address, GCHeapSimulatorObject> OnObjectDestroy;

        /// <summary>
        /// If you are interested in when GCs happen, this delegate gets called exactly once
        /// when a GC completes (before all the OnObjectDestroy for that GC).  It is passed
        /// the time of the GC (technically the STOP of hte GC) and the generation being collected.
        /// </summary>
        public Action<double, int> OnGC;

        /// <summary>
        /// You can override this function to cause the simulator to allocate subclasses 
        /// of GCHeapSimulatorObject so you can attach addition information to it.  
        /// </summary>
        public Func<GCHeapSimulatorObject> AllocateObject { get; set; }

        #region private
        // Event Callbacks
        private void OnGCStart(TraceEvent data, int gcID, int condemedGeneration)
        {
            if (data.ProcessID != m_processID)
            {
                return;
            }

            m_pointerSize = data.PointerSize;   // Make sure our pointer size is accurate. 
            m_condemedGenerationNum = condemedGeneration;
            Debug.WriteLine(string.Format("GC Start Gen {0} at {1:f3}", m_condemedGenerationNum, data.TimeStampRelativeMSec));
        }

        private void OnObjectAllocated(TraceEvent data, Address objectID, Address classID, long size, long representativeSize)
        {
            if (data.ProcessID != m_processID)
            {
                return;
            }

            Debug.Assert(GetObjectInfo(objectID) == null);            // new objects should be unique.
            var objectData = AllocateObject();

            var stackIndex = m_stackSource.GetCallStack(data.CallStackIndex(), data);

            // Add object type as a pseudo-frame to the stack 
            TypeInfo typeInfo;
            if (!m_classNamesAsFrames.TryGetValue(classID, out typeInfo))
            {
                typeInfo.FrameIdx = m_stackSource.Interner.FrameIntern("Type <Unknown>");
            }

            objectData.ClassFrame = typeInfo.FrameIdx;
            objectData.AllocStack = stackIndex;

            objectData.Size = (int)size;
            objectData.RepresentativeSize = (int)representativeSize;

            var timeStamp = data.TimeStampRelativeMSec;
            // Insure that timeStamps move forward in time and are unique, so they can be used as object IDs.  
            var verySmall = 1;
            while (timeStamp <= m_lastAllocTimeRelativeMSec)
            {
                timeStamp = m_lastAllocTimeRelativeMSec + verySmall * .0000001;       // Add a 10th of a ns.
                verySmall = verySmall * 2;      // Depending on how long the trace has run, this may not change timestamp, so keep making it bigger until it does.  
            }
            m_lastAllocTimeRelativeMSec = timeStamp;

            objectData.AllocationTimeRelativeMSec = timeStamp;

            Debug.WriteLine(string.Format("Object Allocated {0:x} Size {1:x} Rep {2:x} {3}", objectID, size, representativeSize, typeInfo.TypeName));
            m_currentHeapSize += (uint)objectData.RepresentativeSize;
            if (OnObjectCreate != null)
            {
                if (!OnObjectCreate(objectID, objectData))
                {
                    return;
                }
            }

            var allocGen = 0;
            if (objectData.Size >= 85000)     // Large objects go into Gen 2
            {
                allocGen = 2;
            }

            m_ObjsInGen[allocGen][(Address)objectID] = objectData;       // Put the object into Gen 0; 
        }

        private void OnGCStop(TraceEvent data, int GCID)
        {
            Debug.WriteLine("GC Stop");
            if (data.ProcessID != m_processID)
            {
                return;
            }

            double gcTime = data.TimeStampRelativeMSec;
            OnGC?.Invoke(gcTime, m_condemedGenerationNum);

            for (int curGenNum = 0; curGenNum <= m_condemedGenerationNum; curGenNum++)
            {
                Dictionary<Address, GCHeapSimulatorObject> curGen = m_ObjsInGen[curGenNum];

                // We have moved all the survivors, so everything left is deleted. 
                foreach (KeyValuePair<Address, GCHeapSimulatorObject> liveObjectPair in curGen)
                {
                    Debug.WriteLine(string.Format("Destroying {0:x} from Gen {1}", liveObjectPair.Key, curGenNum));
                    m_currentHeapSize -= (uint)liveObjectPair.Value.RepresentativeSize;
                    OnObjectDestroy?.Invoke(gcTime, m_condemedGenerationNum, liveObjectPair.Key, liveObjectPair.Value);
                }

                curGen.Clear();
            }

            if (m_condemedGenerationNum == 2)
            {
                // To keep things uniform we always promote objects to a generation bigger.  However for Gen2 we really promote to Gen 2 (Not Gen3)
                // To fix this we move the survived objects (in Gen 3) into Gen 2, and put the empty generation that is currently in
                // gen 2 into gen 3.   
                var temp = m_ObjsInGen[2];
                m_ObjsInGen[2] = m_ObjsInGen[3];
                m_ObjsInGen[3] = temp;
            }
        }

        #region ETWClrProfiler specific event callbacks
        private void OnClassIDDefintion(ClassIDDefintionArgs data)
        {
            if (data.ProcessID != m_processID)
            {
                return;
            }

            // TODO add module name to type name
            m_classNamesAsFrames[data.ClassID] = new TypeInfo() { TypeName = data.Name, FrameIdx = m_stackSource.Interner.FrameIntern("Type " + data.Name) };
        }

        private void OnObjectMoved(ObjectsMovedArgs data)
        {
            if (data.ProcessID != m_processID)
            {
                return;
            }

            for (int i = 0; i < data.Count; i++)
            {
                Address fromPtr = data.RangeBases(i);
                Address toPtr = data.TargetBases(i);
                Address fromEnd = fromPtr + (uint)data.Lengths(i);
                CopyPlugToNextGen(fromPtr, fromEnd, toPtr);
            }
        }

        private void OnObjectSurvived(ObjectsSurvivedArgs data)
        {
            if (data.ProcessID != m_processID)
            {
                return;
            }

            for (int i = 0; i < data.Count; i++)
            {
                Address fromPtr = data.RangeBases(i);
                Address fromEnd = fromPtr + (uint)data.Lengths(i);
                CopyPlugToNextGen(fromPtr, fromEnd, fromPtr);
            }
        }
        #endregion

        #region V4.5.1 Runtime specific event callbacks
        private void OnEtwClassIDDefintion(GCBulkTypeTraceData data)
        {
            if (data.ProcessID != m_processID)
            {
                return;
            }

            if (m_useEtlClrProfilerEvents)
            {
                return;
            }

            for (int i = 0; i < data.Count; i++)
            {
                GCBulkTypeValues typeData = data.Values(i);
                var typeName = typeData.TypeName;
                if (typeData.TypeParameterCount != 0)
                {
                    typeName += "<";
                    for (int j = 0; j < typeData.TypeParameterCount; j++)
                    {
                        if (j != 0)
                        {
                            typeName += ",";
                        }

                        TypeInfo paramInfo;
                        if (m_classNamesAsFrames.TryGetValue(typeData.TypeParameterID(j), out paramInfo))
                        {
                            typeName += paramInfo.TypeName;
                        }
                    }
                    typeName += ">";
                }
                if (typeData.CorElementType == 0x1d)                // SZArray
                {
                    typeName += "[]";
                }
                // TODO FIX NOW make sure the COR_ELEMENT_TYPES are covered.  

                m_classNamesAsFrames[typeData.TypeID] = new TypeInfo() { TypeName = typeName, FrameIdx = m_stackSource.Interner.FrameIntern("Type " + typeName) };
            }
        }

        private void OnEtwObjectMoved(GCBulkMovedObjectRangesTraceData data)
        {
            if (data.ProcessID != m_processID)
            {
                return;
            }

            if (m_useEtlClrProfilerEvents)
            {
                return;
            }

            for (int i = 0; i < data.Count; i++)
            {
                var range = data.Values(i);
                Address fromPtr = range.OldRangeBase;
                CopyPlugToNextGen(fromPtr, fromPtr + range.RangeLength, range.NewRangeBase);
            }
        }

        private void OnEtwObjectSurvived(GCBulkSurvivingObjectRangesTraceData data)
        {
            if (data.ProcessID != m_processID)
            {
                return;
            }

            if (m_useEtlClrProfilerEvents)
            {
                return;
            }

            for (int i = 0; i < data.Count; i++)
            {
                var range = data.Values(i);
                Address fromPtr = range.RangeBase;
                CopyPlugToNextGen(fromPtr, fromPtr + range.RangeLength, fromPtr);
            }
        }

        private void OnEtwGCAllocationTick(GCAllocationTickTraceData data)
        {
            if (data.ProcessID != m_processID)
            {
                return;
            }

            // Check to see if we have cached type info. 
            var typeName = data.TypeName;
            if (!m_classNamesAsFrames.ContainsKey(data.TypeID))
            {
                if (string.IsNullOrEmpty(typeName))
                {
                    // This could be project N, try to resolve it that way.  
                    TraceLoadedModule module = m_process.LoadedModules.GetModuleContainingAddress(data.TypeID, data.TimeStampRelativeMSec);
                    if (module != null)
                    {
                        // Resolve the type name using project N resolution 
                        typeName = m_typeNameSymbolResolver.ResolveTypeName((int)(data.TypeID - module.ModuleFile.ImageBase), module.ModuleFile, TypeNameSymbolResolver.TypeNameOptions.StripModuleName);
                    }
                }

                // Add the ID -> Type Name to the mapping.  
                if (!string.IsNullOrEmpty(typeName))
                {
                    TypeInfo typeInfo = new TypeInfo() { TypeName = typeName, FrameIdx = m_stackSource.Interner.FrameIntern("Type " + typeName) };
                    m_classNamesAsFrames[data.TypeID] = typeInfo;
                }

            }

            // Support for old versions of this event
            long alloc = data.AllocationAmount64;
            if (alloc == 0)
            {
                alloc = data.AllocationAmount;
                if (alloc == 0)
                {
                    alloc = 100000;
                }
            }

            // The representative size needs to be  >= 85K for large objects and < 85 K for small to properly simulate the generation
            // where the object is allocated.  We arbitrarily pick 100 for the representative size for small objects.  
            long reprsentativeSize = alloc;
            if (data.AllocationKind == GCAllocationKind.Small)
            {
                reprsentativeSize = 100;
            }

            OnObjectAllocated(data, data.Address, data.TypeID, alloc, reprsentativeSize);
        }
        #endregion 

        /// <summary>
        /// Takes the set of objects (a plug) in the region from 'fromPtr' to fromEnd, and relocates the objects to new address 'toPtr'.
        /// It also places the objects into whatever generation it is current in to m_condemedGenerationNum+1.  
        /// Note that while our understanding of where what generation an object is might differ from the GC's idea, (we ALWAYS promote 
        /// to a m_condemedGenerationNum+1 where the GC might not), this does not affect the movement/liveness calculation.  
        /// </summary>
        private void CopyPlugToNextGen(Address fromPtr, Address fromEnd, Address toPtr)
        {
            Address origFromPtr = fromPtr;      // Only used for asserts.

            Debug.Assert(m_condemedGenerationNum <= 2);
            var toGen = m_ObjsInGen[m_condemedGenerationNum + 1];     // We always put the survived objects in the generation after the one condemned.  

            // For every object in the plug
            int fromGenNum = m_condemedGenerationNum;               // This is the starting point where we search for an objects generation.  
            Debug.WriteLine(string.Format("Plug {0:x} - {1:x} going to {2:x}", fromPtr, fromEnd, toPtr));
            int pointerSizeMask = m_pointerSize - 1;
            while (fromPtr < fromEnd)
            {
                // Find the generation in the condemned region where the object lives, start the search where we 
                // looked last time because it tends to find it quicker 
                int genStartSearch = fromGenNum;
                // TODO: in the sampling case, when you miss we only increment by the pointer size, which is pretty inefficient. 
                // if we has a sorted table, we could do much better. 
                uint sizeToNextObj = (uint)m_pointerSize;
                bool foundGen = false;              // once we have found the generation for a plug, we don't have to search.  
                for (; ; )
                {
                    var fromGen = m_ObjsInGen[fromGenNum];
                    GCHeapSimulatorObject objInfo = null;
                    if (fromGen.TryGetValue(fromPtr, out objInfo))
                    {
                        foundGen = true;
                        sizeToNextObj = (uint)((objInfo.Size + pointerSizeMask) & ~pointerSizeMask);
                        if (sizeToNextObj < (uint)m_pointerSize)
                        {
                            sizeToNextObj = (uint)m_pointerSize;
                        }

                        if (fromGenNum > m_condemedGenerationNum)
                        {
                            Debug.WriteLine(string.Format("Found in old generation, already alive."));
                        }

                        // Note even if we find this in in an non-condemned generation we do the logic to move it because we basically were incorrect
                        // to have put it in the older generation (the GC obviously had demoted the plug).   
                        fromGen.Remove(fromPtr);              // Remove it from the current generation
                        toGen[toPtr] = objInfo;               // Copy survived data into next generation 
                        Debug.WriteLine(string.Format("Object Survived {0:x} -> {1:x}", fromPtr, toPtr));
                        break;
                    }

                    // Once we have found a plug's generation everything in the plug will have the same gen, so we don't
                    // need to search more than once before we fail in that case.   
                    if (foundGen)
                    {
                        break;
                    }

                    --fromGenNum;               // decrement with wrap around.  
                    if (fromGenNum < 0)
                    {
                        fromGenNum = 2;         // We search all generations not just condemned because things may be demoted and we want to walk the plug efficiently. 
                    }

                    if (genStartSearch != fromGenNum)
                    {
                        // Debug.Assert(false, "Error, could not find first object in plug, skipping and continuing.");
                        break;
                    }
                }

                if (sizeToNextObj == m_pointerSize)
                {
                    Debug.WriteLine(string.Format("Synching {0:x}", fromPtr));
                }

                fromPtr += sizeToNextObj;
                toPtr += sizeToNextObj;
            }
        }

        // Fields
        private ulong m_currentHeapSize = 0;
        private Dictionary<ulong, TypeInfo> m_classNamesAsFrames;

        private struct TypeInfo
        {
            public string TypeName;
            public StackSourceFrameIndex FrameIdx;
        }

        private Dictionary<Address, GCHeapSimulatorObject>[] m_ObjsInGen;
        private MutableTraceEventStackSource m_stackSource;
        private TypeNameSymbolResolver m_typeNameSymbolResolver;
        private int m_processID;
        private TraceProcess m_process;
        private int m_pointerSize;                  // Size of a pointer for the process (4 or 8).  Note it may be 4 when it should be 8 if some events are dropped.  
        private int m_condemedGenerationNum = 0;
        private double m_lastAllocTimeRelativeMSec;
        private bool m_useEtlClrProfilerEvents;     // If true we use ETWCLrProfiler events, false we use built in Clr events (if we can). 
        #endregion
    }

    /// <summary>
    /// GCHeapSimulatorObject holds all the information (Except the object ID itself) that the GCSimulation knows about an object.
    /// It can be subclassed so that user-defined information can be added as well. 
    /// </summary>
    public class GCHeapSimulatorObject
    {
        // Stuff set at allocation and never changed.  
        public double AllocationTimeRelativeMSec;       // We guarantee uniqueness of these timestamps, so it can be used as durable object ID
        public StackSourceCallStackIndex AllocStack = StackSourceCallStackIndex.Invalid;    // This is the stack at the allocation point
        public StackSourceFrameIndex ClassFrame;        // This identifies the class being allocated.
        public int Size;                                // This is the size of the object being allocated on this particular event.  
        public int RepresentativeSize;                  // If sampling is on, this sample may represent many allocations. This is the sum of the sizes of all those allocations.  
        // If sampling is off this is the same as Size.  

        public int GuessCountBasedOnSize()
        {
            return Size > 0 ? (RepresentativeSize / Size) : 1;
        }
    }
}
