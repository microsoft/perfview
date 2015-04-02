// Copyright (c) Microsoft Corporation.  All rights reserved
// This file is best viewed using outline mode (Ctrl-M Ctrl-O)
//
// This program uses code hyperlinks available as part of the HyperAddin Visual Studio plug-in.
// It is available from http://www.codeplex.com/hyperAddin 
// 
using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text;
using System.IO;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Stacks;
using Microsoft.Diagnostics.Symbols;
using Address = System.UInt64;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.Tpl;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using Microsoft.Diagnostics.Tracing.Parsers.FrameworkEventSource;

namespace Microsoft.Diagnostics.Tracing.Stacks
{
    /// <summary>
    /// TraceEventStackSource is an implementation of a StackSource for ETW information (TraceLog)
    /// It takes a TraceEvents (which is a list of TraceEvents you get get from a TraceLog) and 
    /// implements that StackSource protocol for them.  (thus any code needing a StackSource 
    /// can then work on it.  
    /// 
    /// The key to the implementation is how StackSourceFrameIndex and StackSourceCallStackIndex 
    /// (part of the StackSource protocol) are mapped to the Indexes in TraceLog.   Here is
    /// the mapping.
    /// 
    /// TraceEventStackSource create the following meaning for the StackSourceCallStackIndex
    /// 
    /// * The call stacks ID consists of the following ranges concatenated together. 
    ///     * a small set of fixed Pseudo stacks (Start marks the end of these)
    ///     * CallStackIndex
    ///     * ThreadIndex
    ///     * ProcessIndex
    ///     * BrokenStacks (One per thread)
    ///     * Stacks for threads without explicit stacks (Limited to 1K)
    ///         
    /// TraceEventStackSource create the following meaning for the StackSourceFrameIndex
    /// 
    /// The frame ID consists of the following ranges concatenated together. 
    ///     * a small fixed number of Pseudo frame (Broken, and Unknown)
    ///     * MaxCodeAddressIndex - something with a TraceCodeAddress. 
    ///     * ThreadIndex         - ETW stacks don't have a thread or process node, so we add them.
    ///     * ProcessIndex
    /// </summary>
    public class TraceEventStackSource : StackSource
    {
        /// <summary>
        /// Creates a new TraceEventStackSource given a list of events 'events' from a TraceLog
        /// </summary>
        /// <param name="events"></param>
        public TraceEventStackSource(TraceEvents events)
        {
            Debug.Assert(m_log == null);
            if (events != null)
                m_log = events.Log;
            m_goodTopModuleIndex = ModuleFileIndex.Invalid;
            m_curSample = new StackSourceSample(this);
            m_curSample.Metric = (float)events.Log.SampleProfileInterval.TotalMilliseconds;
            m_events = events;
            m_maxPseudoStack = m_log.CodeAddresses.Count;     // This really is a guess as to how many stacks we need.  
        }

        // These are TraceEventStackSource specific.  
        /// <summary>
        /// Returns the TraceLog file that is associated with this stack source.  
        /// </summary>
        public TraceLog TraceLog { get { return m_log; } }
        /// <summary>
        /// Normally addresses without symbolic names are listed as ?, however sometimes it is useful 
        /// to see the actuall address as a hexidecimal number.  Setting this will do that.  
        /// </summary>
        public bool ShowUnknownAddresses { get; set; }
        /// <summary>
        /// Looks up symbols for all modules that have an inclusive count >= minCount. 
        /// stackSource, if given, can be used to be the filter.  If null, 'this' is used.
        /// If stackSource is given, it needs to use the same indexes for frames as 'this'
        /// </summary>
        public void LookupWarmSymbols(int minCount, SymbolReader reader, StackSource stackSource = null)
        {
            if (stackSource == null)
                stackSource = this;

            Debug.Assert(stackSource.CallFrameIndexLimit == this.CallFrameIndexLimit);
            Debug.Assert(stackSource.CallStackIndexLimit == this.CallStackIndexLimit);

            reader.Log.WriteLine("Resolving all symbols for modules with inclusive times > {0}", minCount);
            if ((reader.Options & SymbolReaderOptions.CacheOnly) != 0)
                reader.Log.WriteLine("Cache-Only set: will only look on the local machine.");

            // Get a list of all the unique frames.   We also keep track of unique stacks for efficiency
            var stackModuleLists = new ModuleList[stackSource.CallStackIndexLimit];
            var stackCounts = new int[stackSource.CallStackIndexLimit];
            var totalCount = 0;

            // Compute for each stack, the set of inclusive modules for that stack
            stackSource.ForEach(delegate(StackSourceSample sample)
            {
                stackCounts[(int)sample.StackIndex]++;
                totalCount++;
            });
            reader.Log.WriteLine("Got a total of {0} samples", totalCount);

            // for each stack in the trace, find the list of modules for that stack
            var moduleCounts = new int[TraceLog.ModuleFiles.Count];
            for (int i = 0; i < stackCounts.Length; i++)
            {
                var count = stackCounts[i];
                if (count > 0)
                {
                    var modules = GetModulesForStack(stackModuleLists, (StackSourceCallStackIndex)i);
                    // Update the counts for each module in that stack.  
                    while (modules != null)
                    {
                        moduleCounts[(int)modules.Module.ModuleFileIndex] += count;
                        modules = modules.Next;
                    }
                }
            }

            // Now that we have an list of the inclusive counts of all frames.  Find all stacks that meet the threshold
            for (int i = 0; i < moduleCounts.Length; i++)
            {
                if (moduleCounts[i] >= minCount)
                {
                    var moduleFile = TraceLog.ModuleFiles[(ModuleFileIndex)i];
                    reader.Log.WriteLine("Resolving symbols (count={0}) for module {1} ", moduleCounts[i], moduleFile.FilePath);
                    TraceLog.CallStacks.CodeAddresses.LookupSymbolsForModule(reader, moduleFile);
                }
            }
            reader.Log.WriteLine("Done Resolving all symbols for modules with inclusive times > {0}", minCount);
        }
        /// <summary>
        /// Given a frame index, return the corresponding code address for it.  This is useful for looking up line number information. 
        /// </summary>
        public CodeAddressIndex GetFrameCodeAddress(StackSourceFrameIndex frameIndex)
        {
            uint codeAddressIndex = (uint)frameIndex - (uint)StackSourceFrameIndex.Start;
            if (codeAddressIndex >= m_log.CodeAddresses.Count)
                return CodeAddressIndex.Invalid;
            return (CodeAddressIndex)codeAddressIndex;
        }

        #region implementation of StackSource
        /// <summary>
        /// Implementation of StackSource protocol. 
        /// </summary>
        public override void ForEach(Action<StackSourceSample> callback)
        {
            var dispatcher = m_events.GetSource();
            // TODO use callback model rather than enumerator
            foreach (var event_ in ((IEnumerable<TraceEvent>)m_events))
            {
                m_curSample.StackIndex = GetStack(event_);
                Debug.Assert(m_curSample.StackIndex >= 0 || m_curSample.SampleIndex == StackSourceSampleIndex.Invalid);

                m_curSample.TimeRelativeMSec = event_.TimeStampRelativeMSec;
                Debug.Assert(event_.ProcessName != null);
                callback(m_curSample);
            };
        }
        /// <summary>
        /// Implementation of StackSource protocol. 
        /// </summary>
        public override double SampleTimeRelativeMSecLimit
        {
            get
            {
                return m_log.SessionEndTimeRelativeMSec;
            }
        }
        /// <summary>
        /// Implementation of StackSource protocol. 
        /// </summary>
        public override StackSourceFrameIndex GetFrameIndex(StackSourceCallStackIndex callStackIndex)
        {
            Debug.Assert(callStackIndex >= 0);
            Debug.Assert(StackSourceCallStackIndex.Start == 0);         // If there are any cases before start, we need to handle them here. 
            int stackIndex = (int)callStackIndex - (int)StackSourceCallStackIndex.Start;
            if (stackIndex < m_log.CallStacks.Count)
            {
                CodeAddressIndex codeAddressIndex = m_log.CallStacks.CodeAddressIndex((CallStackIndex)stackIndex);
                return (StackSourceFrameIndex)(codeAddressIndex + (int)StackSourceFrameIndex.Start);
            }
            stackIndex -= m_log.CallStacks.Count;
            if (stackIndex < m_log.Threads.Count + m_log.Processes.Count)
            {
                // At this point this is the encoded thread/process index.   We use the same encoding for both stacks and for frame names
                // so we just need to add back in the proper offset. 
                return (StackSourceFrameIndex)(stackIndex + m_log.CodeAddresses.Count + (int)StackSourceFrameIndex.Start);
            }
            stackIndex -= m_log.Threads.Count + m_log.Processes.Count;

            if (stackIndex < m_log.Threads.Count)      // Is it a broken stack 
                return StackSourceFrameIndex.Broken;
            stackIndex -= m_log.Threads.Count;

            // Is it a 'single node' stack (e.g. a profile sample without a stack)
            if (stackIndex < m_pseudoStacks.Count)
            {
                // From the Pseudo stack index, find the code address.  
                int codeAddressIndex = (int)m_pseudoStacks[stackIndex].CodeAddressIndex;

                // Return it as the frame.  
                return (StackSourceFrameIndex)(codeAddressIndex + (int)StackSourceFrameIndex.Start);
            }

            Debug.Assert(false, "Illegal Call Stack Index");
            return StackSourceFrameIndex.Invalid;
        }
        /// <summary>
        /// Implementation of StackSource protocol. 
        /// </summary>
        public override StackSourceCallStackIndex GetCallerIndex(StackSourceCallStackIndex callStackIndex)
        {
            Debug.Assert(callStackIndex >= 0);
            Debug.Assert(StackSourceCallStackIndex.Start == 0);         // If there are any cases before start, we need to handle them here. 

            int curIndex = (int)callStackIndex - (int)StackSourceCallStackIndex.Start;
            int nextIndex = (int)StackSourceCallStackIndex.Start;
            if (curIndex < m_log.CallStacks.Count)
            {
                var nextCallStackIndex = m_log.CallStacks.Caller((CallStackIndex)curIndex);
                if (nextCallStackIndex == CallStackIndex.Invalid)
                {
                    nextIndex += m_log.CallStacks.Count;    // Now points at the threads region.  
                    var threadIndex = m_log.CallStacks.ThreadIndex((CallStackIndex)curIndex);
                    nextIndex += (int)threadIndex;

                    // Mark it as a broken stack, which come after all the indexes for normal threads and processes. 
                    if (!ReasonableTopFrame(callStackIndex, threadIndex))
                        nextIndex += m_log.Threads.Count + m_log.Processes.Count;
                }
                else
                    nextIndex += (int)nextCallStackIndex;
                return (StackSourceCallStackIndex)nextIndex;
            }
            curIndex -= m_log.CallStacks.Count;                                 // Now is a thread index
            nextIndex += m_log.CallStacks.Count;                                // Output index points to the thread region.          

            if (curIndex < m_log.Threads.Count)
            {
                nextIndex += m_log.Threads.Count;                                  // Output index point to process region.
                nextIndex += (int)m_log.Threads[(ThreadIndex)curIndex].Process.ProcessIndex;
                return (StackSourceCallStackIndex)nextIndex;
            }
            curIndex -= m_log.Threads.Count;                                      // Now is a broken thread index

            if (curIndex < m_log.Processes.Count)
                return StackSourceCallStackIndex.Invalid;                                   // Process has no parent
            curIndex -= m_log.Processes.Count;                                    // Now is a broken thread index

            if (curIndex < m_log.Threads.Count)                                    // It is a broken stack
            {
                nextIndex += curIndex;                                                      // Indicate the real thread.  
                return (StackSourceCallStackIndex)nextIndex;
            }
            curIndex -= m_log.Threads.Count;                                       // Now it points at the one-element stacks. 

            if (curIndex < m_pseudoStacks.Count)
            {
                // Now points beginning of the broken stacks indexes.  
                nextIndex += m_log.Threads.Count + m_log.Processes.Count;

                // Pick the broken stack for this thread.  
                nextIndex += (int)m_pseudoStacks[curIndex].ThreadIndex;
                return (StackSourceCallStackIndex)nextIndex;
            }

            Debug.Assert(false, "Invalid CallStackIndex");
            return StackSourceCallStackIndex.Invalid;
        }
        /// <summary>
        /// Implementation of StackSource protocol. 
        /// </summary>
        public SourceLocation GetSourceLine(StackSourceFrameIndex frameIndex, SymbolReader reader)
        {
            uint codeAddressIndex = (uint)frameIndex - (uint)StackSourceFrameIndex.Start;
            if (codeAddressIndex >= m_log.CodeAddresses.Count)
                return null;
            return m_log.CodeAddresses.GetSourceLine(reader, (CodeAddressIndex)codeAddressIndex);
        }
        /// <summary>
        /// Implementation of StackSource protocol. 
        /// </summary>
        public override string GetFrameName(StackSourceFrameIndex frameIndex, bool verboseName)
        {
            string methodName = "?";
            var moduleFileIdx = ModuleFileIndex.Invalid;

            if (frameIndex < StackSourceFrameIndex.Start)
            {
                if (frameIndex == StackSourceFrameIndex.Broken)
                    return "BROKEN";
                else if (frameIndex == StackSourceFrameIndex.Overhead)
                    return "OVERHEAD";
                else if (frameIndex == StackSourceFrameIndex.Root)
                    return "ROOT";
                else
                    return "?!?";
            }
            int index = (int)frameIndex - (int)StackSourceFrameIndex.Start;
            if (index < m_log.CodeAddresses.Count)
            {
                var codeAddressIndex = (CodeAddressIndex)index;
                MethodIndex methodIndex = m_log.CallStacks.CodeAddresses.MethodIndex(codeAddressIndex);
                if (methodIndex != MethodIndex.Invalid)
                    methodName = m_log.CodeAddresses.Methods.FullMethodName(methodIndex);
                else
                {
                    if (ShowUnknownAddresses)
                        methodName = "0x" + m_log.CallStacks.CodeAddresses.Address(codeAddressIndex).ToString("x");
                }
                moduleFileIdx = m_log.CodeAddresses.ModuleFileIndex(codeAddressIndex);
            }
            else
            {
                index -= m_log.CodeAddresses.Count;
                if (index < m_log.Threads.Count)
                    return m_log.Threads[(ThreadIndex)index].VerboseThreadName;
                index -= m_log.Threads.Count;
                if (index < m_log.Processes.Count)
                {
                    TraceProcess process = m_log.Processes[(ProcessIndex)index];
                    string ptrSize = process.Is64Bit ? "64" : "32";
                    return "Process" + ptrSize + " " + process.Name + " (" + process.ProcessID + ")";
                }
                Debug.Assert(false, "Illegal Frame index");
                return "";
            }

            string moduleName = "?";
            if (moduleFileIdx != ModuleFileIndex.Invalid)
            {
                if (verboseName)
                {
                    moduleName = m_log.CodeAddresses.ModuleFiles[moduleFileIdx].FilePath;
                    if (moduleName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                        moduleName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        moduleName = moduleName.Substring(0, moduleName.Length - 4);        // Remove the .dll or .exe
                }
                else
                    moduleName = m_log.CodeAddresses.ModuleFiles[moduleFileIdx].Name;
            }

            return moduleName + "!" + methodName;
        }
        /// <summary>
        /// Implementation of StackSource protocol. 
        /// </summary>
        public override int CallStackIndexLimit
        {
            get
            {
                return (int)StackSourceCallStackIndex.Start + m_log.CallStacks.Count +
                    2 * m_log.Threads.Count + m_log.Processes.Count +     // *2 one for normal threads, one for broken threads. 
                    m_maxPseudoStack;                                                        // These are for the threads with no explicit stacks. 
            }
        }
        /// <summary>
        /// Implementation of StackSource protocol. 
        /// </summary>
        public override int CallFrameIndexLimit
        {
            get
            {
                return (int)StackSourceFrameIndex.Start + m_log.CodeAddresses.Count + m_log.Threads.Count + m_log.Processes.Count;
            }
        }
        #endregion
        #region private
        /// <summary>
        /// Returns a list of modules for the stack 'stackIdx'.  It also updates the interning table stackModuleLists, so 
        /// that the entry cooresponding to stackIdx remembers the answer.  This can speed up processing alot since many
        /// stacks have the same prefixes to root.  
        /// </summary>
        private ModuleList GetModulesForStack(ModuleList[] stackModuleLists, StackSourceCallStackIndex stackIdx)
        {
            var ret = stackModuleLists[(int)stackIdx];
            if (ret == null)
            {
                // ret = the module list for the rest of the frames. 
                var callerIdx = GetCallerIndex(stackIdx);
                if (callerIdx != StackSourceCallStackIndex.Invalid)
                    ret = GetModulesForStack(stackModuleLists, callerIdx);

                // Compute the module for the top most frame, and add it to the list (if we find a module)  
                TraceModuleFile module = null;
                var frameIdx = GetFrameIndex(stackIdx);
                if (frameIdx != StackSourceFrameIndex.Invalid)
                {
                    var codeAddress = GetFrameCodeAddress(frameIdx);
                    if (codeAddress != CodeAddressIndex.Invalid)
                    {
                        var moduleFileIdx = TraceLog.CallStacks.CodeAddresses.ModuleFileIndex(codeAddress);
                        if (moduleFileIdx != ModuleFileIndex.Invalid)
                        {
                            module = TraceLog.ModuleFiles[moduleFileIdx];
                            ret = ModuleList.SetAdd(module, ret);
                        }
                    }
                }
                stackModuleLists[(int)stackIdx] = ret;
            }
            return ret;
        }

        /// <summary>
        /// A ModuleList is a linked list of modules.  It is only used in GetModulesForStack and LookupWarmSymbols
        /// </summary>
        class ModuleList
        {
            public static ModuleList SetAdd(TraceModuleFile module, ModuleList list)
            {
                if (!Member(module, list))
                    return new ModuleList(module, list);
                return list;
            }
            public static bool Member(TraceModuleFile module, ModuleList rest)
            {
                while (rest != null)
                {
                    if ((object)module == (object)rest.Module)
                        return true;
                    rest = rest.Next;
                }
                return false;
            }

            public ModuleList(TraceModuleFile module, ModuleList rest)
            {
                Module = module;
                Next = rest;
            }
            public TraceModuleFile Module;
            public ModuleList Next;
        }

        internal TraceEventStackSource(TraceLog log)
        {
            m_log = log;
            m_goodTopModuleIndex = ModuleFileIndex.Invalid;
            m_curSample = new StackSourceSample(this);
            m_curSample.Metric = (float)log.SampleProfileInterval.TotalMilliseconds;
        }

        // Sometimes we just have a code address and thread, but no actual ETW stack.  Create a 'one element'
        // stack whose index is the index into the m_pseudoStacks array
        struct PseudoStack : IEquatable<PseudoStack>
        {
            public PseudoStack(ThreadIndex threadIndex, CodeAddressIndex codeAddressIndex)
            {
                ThreadIndex = threadIndex; CodeAddressIndex = codeAddressIndex;
            }
            public ThreadIndex ThreadIndex;
            public CodeAddressIndex CodeAddressIndex;

            public override int GetHashCode() { return (int)CodeAddressIndex + ((int)ThreadIndex) * 0x10000; }
            public override bool Equals(object obj) { throw new NotImplementedException(); }
            public bool Equals(PseudoStack other) { return ThreadIndex == other.ThreadIndex && CodeAddressIndex == other.CodeAddressIndex; }
        };
        private GrowableArray<PseudoStack> m_pseudoStacks;
        private int m_maxPseudoStack;
        /// <summary>
        /// This maps pseudo-stacks to their index (thus it is the inverse of m_pseudoStack; 
        /// </summary>
        private Dictionary<PseudoStack, int> m_pseudoStacksTable;

        /// <summary>
        /// Given a thread and a call stack that does not have a stack, make up a pseudo stack for it consisting of the code address, 
        /// the broken node, the thread and process.   Will return -1 if it can't allocate another Pseudo-stack.
        /// </summary> 
        private int GetPseudoStack(ThreadIndex threadIndex, CodeAddressIndex codeAddrIndex)
        {
            if (m_pseudoStacksTable == null)
                m_pseudoStacksTable = new Dictionary<PseudoStack, int>();

            var pseudoStack = new PseudoStack(threadIndex, codeAddrIndex);
            int ret;
            if (m_pseudoStacksTable.TryGetValue(pseudoStack, out ret))
                return ret;

            ret = m_pseudoStacks.Count;
            if (ret >= m_maxPseudoStack)
                return -1;
            m_pseudoStacks.Add(pseudoStack);
            m_pseudoStacksTable.Add(pseudoStack, ret);
            return ret;
        }

        private StackSourceCallStackIndex GetStack(TraceEvent event_)
        {
            // Console.WriteLine("Getting Stack for sample at {0:f4}", sample.TimeStampRelativeMSec);
            var ret = (int)event_.CallStackIndex();
            if (ret == (int)CallStackIndex.Invalid)
            {
                var thread = event_.Thread();
                if (thread == null)
                    return StackSourceCallStackIndex.Invalid;

                // If the event is a sample profile, or page fault we can make a one element stack with the EIP in the event 
                CodeAddressIndex codeAddrIdx = CodeAddressIndex.Invalid;
                var asSampleProfile = event_ as SampledProfileTraceData;
                if (asSampleProfile != null)
                    codeAddrIdx = asSampleProfile.IntructionPointerCodeAddressIndex();
                else
                {
                    var asPageFault = event_ as MemoryHardFaultTraceData;
                    if (asPageFault != null)
                        codeAddrIdx = asSampleProfile.IntructionPointerCodeAddressIndex();
                }

                if (codeAddrIdx != CodeAddressIndex.Invalid)
                {
                    // Encode the code address for the given thread.  
                    int pseudoStackIndex = GetPseudoStack(thread.ThreadIndex, codeAddrIdx);
                    if (pseudoStackIndex < 0)
                        return StackSourceCallStackIndex.Start;

                    // Psuedostacks happen after all the others.  
                    ret = m_log.CallStacks.Count + 2 * m_log.Threads.Count + m_log.Processes.Count + pseudoStackIndex;
                }
                else
                {
                    // Otherwise we encode the stack as being at the thread.  
                    ret = m_log.CallStacks.Count + (int)thread.ThreadIndex;
                }
            }
            ret = ret + (int)StackSourceCallStackIndex.Start;
            return (StackSourceCallStackIndex)ret;
        }

        private bool ReasonableTopFrame(StackSourceCallStackIndex callStackIndex, ThreadIndex threadIndex)
        {

            uint index = (uint)callStackIndex - (uint)StackSourceCallStackIndex.Start;

            var stack = m_log.CallStacks[(CallStackIndex)callStackIndex];
            if (index < (uint)m_log.CallStacks.Count)
            {
                CodeAddressIndex codeAddressIndex = m_log.CallStacks.CodeAddressIndex((CallStackIndex)index);
                ModuleFileIndex moduleFileIndex = m_log.CallStacks.CodeAddresses.ModuleFileIndex(codeAddressIndex);
                if (m_goodTopModuleIndex == moduleFileIndex)        // optimization
                    return true;

                TraceModuleFile moduleFile = m_log.CallStacks.CodeAddresses.ModuleFile(codeAddressIndex);
                if (moduleFile == null)
                    return false;

                // We allow things that end in ntdll to be considered unbroken (TODO is this too strong?)
                if (moduleFile.FilePath.EndsWith("ntdll.dll", StringComparison.OrdinalIgnoreCase))
                {
                    m_goodTopModuleIndex = moduleFileIndex;
                    return true;
                }

                // The special processes 4 (System) and 0 (Kernel) can stay in the kernel without being broken.  
                if (moduleFile.FilePath.EndsWith("ntoskrnl.exe", StringComparison.OrdinalIgnoreCase))
                {
                    var processId = m_log.Threads[threadIndex].Process.ProcessID;
                    if (processId == 4 || processId == 0)
                    {
                        m_goodTopModuleIndex = moduleFileIndex;
                        return true;
                    }
                }
                return false;
            }
            return false;
        }

        StackSourceSample m_curSample;
        TraceEvents m_events;
        ModuleFileIndex m_goodTopModuleIndex;       // This is a known good module index for a 'good' stack (probably ntDll!RtlUserStackStart
        internal /*protected*/ TraceLog m_log;
        #endregion
    }

    /// <summary>
    /// Like a TraceEventStackSource a MutableTraceEventStackSource allows you incorporate the stacks associated with
    /// a TraceEvent as a sample in the StackSource.   However in addition it allows you to create new frames for these
    /// stacks on the fly as well as add samples that did not exist in the original TraceEvent stream.   This gives you
    /// a lot of flexibility to add additional data to the original stream of TraceEvents.   
    /// 
    /// Like TraceEventStackSource MutableTraceEventStackSource supports the GetFrameCodeAddress() method that  allows
    /// you to map from the StackSourceFrameIndex back its TraceLog code address (that lets you get at the source code and
    /// line number for that frame).  
    /// </summary>
    public class MutableTraceEventStackSource : TraceEventStackSource
    {
        /// <summary>
        /// Create a new MutableTraceEventStackSource that can represent stacks comming from any events in the given TraceLog with a stack.  
        /// You use the 'AddSample' and 'DoneAddingSamples' to specify exactly which stacks you want in your source.   
        /// </summary>
        public MutableTraceEventStackSource(TraceLog log)
            : base(log)
        {
            m_Interner = new StackSourceInterner(5000, 1000, 100,
                (StackSourceFrameIndex)base.CallFrameIndexLimit, (StackSourceCallStackIndex)base.CallStackIndexLimit);
            m_emptyModuleIdx = m_Interner.ModuleIntern("");
            m_Interner.FrameNameLookup = GetFrameName;
        }
        /// <summary>
        /// After creating a MultableTraceEventStackSource, you add the samples you want using this AddSample API (you can reuse 'sample' 
        /// used as an argument to this routine.   It makes a copy.  The samples do NOT need to be added in time order (the MultableTraceEventStackSource
        /// will sort them).   When you done DoneAddingSamples must be called before using the
        /// the MutableTraceEventStackSource as a stack source.  
        /// </summary>
        public StackSourceSample AddSample(StackSourceSample sample)
        {
            var sampleCopy = new StackSourceSample(sample);
            sampleCopy.SampleIndex = (StackSourceSampleIndex)m_samples.Count;
            m_samples.Add(sampleCopy);
            if (sampleCopy.TimeRelativeMSec > m_sampleTimeRelativeMSecLimit)
                m_sampleTimeRelativeMSecLimit = sampleCopy.TimeRelativeMSec;
            return sampleCopy;
        }
        /// <summary>
        /// After calling 'AddSample' to add the samples that should belong to the source, DoneAddingSamples() should be called to
        /// to complete the construction of the stack source.   Only then can the reading API associated with the stack source be called. 
        /// </summary>
        public void DoneAddingSamples()
        {
            m_Interner.DoneInterning();

            // Insure that the samples are in sorted order.  
            m_samples.Sort(
                (x, y) =>
                {
                    int res = x.TimeRelativeMSec.CompareTo(y.TimeRelativeMSec);
                    if (res != 0) return res; 
                    else return x.StackIndex.CompareTo(y.StackIndex); 
                });
            for (int i = 0; i < m_samples.Count; i++)
                m_samples[i].SampleIndex = (StackSourceSampleIndex)i;
        }

        /// <summary>
        /// The Interner is the class that allows you to make new indexes out of strings and other bits.  
        /// </summary>
        public StackSourceInterner Interner { get { return m_Interner; } }

        // methods for create stacks from TraceLog structures.  
        /// <summary>
        /// Returns a StackSourceCallStackIndex representing just one entry that represents the process 'process'
        /// </summary>
        public StackSourceCallStackIndex GetCallStackForProcess(TraceProcess process)
        {
            var idx = (int)StackSourceCallStackIndex.Start + m_log.CallStacks.Count + m_log.Threads.Count + (int)process.ProcessIndex;
            return (StackSourceCallStackIndex)idx;
        }
        /// <summary>
        /// Returns a StackSourceCallStackIndex representing just two entries that represent 'thread' which has a parent of its process.   
        /// </summary>
        public StackSourceCallStackIndex GetCallStackForThread(TraceThread thread)
        {
            var idx = (int)StackSourceCallStackIndex.Start + m_log.CallStacks.Count + (int)thread.ThreadIndex;
            return (StackSourceCallStackIndex)idx;
        }
        /// <summary>
        /// Returns a StackSourceCallStackIndex representing the call stack from the TraceLog represented by the CallStackIndex 'callStackIndex'.  
        /// If that stack is invalid, use 'thread' to at least return a call stack for the thread.  
        /// </summary>
        public StackSourceCallStackIndex GetCallStackThread(CallStackIndex callStackIndex, TraceThread thread)
        {
            if (callStackIndex == CallStackIndex.Invalid)
            {
                if (thread == null)
                    return StackSourceCallStackIndex.Invalid;
                return GetCallStackForThread(thread);
            }
            var idx = (int)StackSourceCallStackIndex.Start + (int)callStackIndex;
            return (StackSourceCallStackIndex)idx;
        }
        /// <summary>
        /// Returns a StackSourceCallStackIndex representing the call stack from the TraceLog represented by the CallStackIndex 'callStackIndex'.  
        /// Use the TraceEvent 'data' to find the stack if callStackIndex is invalid.  
        /// TODO data should be removed (or callstack derived from it) 
        /// </summary>  
        public StackSourceCallStackIndex GetCallStack(CallStackIndex callStackIndex, TraceEvent data)
        {
            if (callStackIndex == CallStackIndex.Invalid)
            {
                if (data == null)
                    return StackSourceCallStackIndex.Invalid;
                var thread = data.Thread();
                if (thread == null)
                    return StackSourceCallStackIndex.Invalid;
                return GetCallStackForThread(thread);
            }
            var idx = (int)StackSourceCallStackIndex.Start + (int)callStackIndex;
            return (StackSourceCallStackIndex)idx;
        }

        /// <summary>
        /// Find the StackSourceCallStackIndex for the TraceEvent call stack index 'callStackIndex' which has a top of its 
        /// stack (above the stack, where the thread and process would normally go) as 'top'.  If callStackMap is non-null 
        /// it is used as an interning table for CallStackIndex -> StackSourceCallStackIndex.  This can speed up the 
        /// transformation dramatically.   It will still work if it is null.  
        /// </summary>
        /// 
        public StackSourceCallStackIndex GetCallStack(CallStackIndex callStackIndex, StackSourceCallStackIndex top,
            Dictionary<CallStackIndex, StackSourceCallStackIndex> callStackMap)
        {
            if (callStackIndex == CallStackIndex.Invalid)
                return top;

            StackSourceCallStackIndex cachedValue;
            if (callStackMap != null && callStackMap.TryGetValue(callStackIndex, out cachedValue))
                return cachedValue;

            var frameIdx = GetFrameIndex(m_log.CallStacks.CodeAddressIndex(callStackIndex));

            CallStackIndex nonInternedCallerIdx = m_log.CallStacks.Caller(callStackIndex);
            StackSourceCallStackIndex callerIdx;
            if (nonInternedCallerIdx == CallStackIndex.Invalid)
            {
                callerIdx = top;

                var frameName = GetFrameName(frameIdx, false);
                var bangIdx = frameName.IndexOf('!');
                if (0 < bangIdx)
                {
                    if (!(5 <= bangIdx && string.Compare(frameName, bangIdx - 5, "ntdll", 0, 5, StringComparison.OrdinalIgnoreCase) == 0))
                    {
                        var brokenFrame = m_Interner.FrameIntern("BROKEN", m_emptyModuleIdx);
                        callerIdx = m_Interner.CallStackIntern(brokenFrame, callerIdx);
                    }
                }
            }
            else
                callerIdx = GetCallStack(nonInternedCallerIdx, top, callStackMap);

            var ret = m_Interner.CallStackIntern(frameIdx, callerIdx);
            if (callStackMap != null)
                callStackMap[callStackIndex] = ret;
            return ret;
        }

        /// <summary>
        /// Create a frame name from a TraceLog code address.  
        /// </summary>
        public StackSourceFrameIndex GetFrameIndex(CodeAddressIndex codeAddressIndex)
        {
            return (StackSourceFrameIndex)((int)StackSourceFrameIndex.Start + (int)codeAddressIndex);
        }

        #region implementation of StackSource
        /// <summary>
        /// Implementation of StackSource protocol. 
        /// </summary>
        public override StackSourceCallStackIndex GetCallerIndex(StackSourceCallStackIndex callStackIndex)
        {
            if (callStackIndex >= (StackSourceCallStackIndex)base.CallStackIndexLimit)
                return m_Interner.GetCallerIndex(callStackIndex);
            return base.GetCallerIndex(callStackIndex);
        }
        /// <summary>
        /// Implementation of StackSource protocol. 
        /// </summary>
        public override StackSourceFrameIndex GetFrameIndex(StackSourceCallStackIndex callStackIndex)
        {
            if (callStackIndex >= (StackSourceCallStackIndex)base.CallStackIndexLimit)
                return m_Interner.GetFrameIndex(callStackIndex);
            return base.GetFrameIndex(callStackIndex);
        }
        /// <summary>
        /// Implementation of StackSource protocol. 
        /// </summary>
        public StackSourceModuleIndex GetModuleIndex(StackSourceFrameIndex frameIndex)
        {
            if (frameIndex >= (StackSourceFrameIndex)base.CallFrameIndexLimit)
                return m_Interner.GetModuleIndex(frameIndex);

            return StackSourceModuleIndex.Invalid;      // TODO FIX NOW this is a poor approximation
        }
        /// <summary>
        /// Implementation of StackSource protocol. 
        /// </summary>
        public override string GetFrameName(StackSourceFrameIndex frameIndex, bool fullModulePath)
        {
            if (frameIndex >= (StackSourceFrameIndex)base.CallFrameIndexLimit)
                return m_Interner.GetFrameName(frameIndex, fullModulePath);
            return base.GetFrameName(frameIndex, fullModulePath);
        }
        /// <summary>
        /// Implementation of StackSource protocol. 
        /// </summary>
        public override StackSourceSample GetSampleByIndex(StackSourceSampleIndex sampleIndex)
        {
            return m_samples[(int)sampleIndex];
        }
        /// <summary>
        /// Implementation of StackSource protocol. 
        /// </summary>
        public override int SampleIndexLimit { get { return m_samples.Count; } }
        /// <summary>
        /// Implementation of StackSource protocol. 
        /// </summary>
        public override void ForEach(Action<StackSourceSample> callback)
        {
            for (int i = 0; i < m_samples.Count; i++)
                callback(m_samples[i]);
        }
        /// <summary>
        /// Implementation of StackSource protocol. 
        /// </summary>
        public override bool SamplesImmutable { get { return true; } }
        /// <summary>
        /// Implementation of StackSource protocol. 
        /// </summary>
        public override int CallFrameIndexLimit { get { return base.CallFrameIndexLimit + m_Interner.FrameCount; } }
        /// <summary>
        /// Implementation of StackSource protocol. 
        /// </summary>
        public override int CallStackIndexLimit { get { return base.CallStackIndexLimit + m_Interner.CallStackCount; } }
        /// <summary>
        /// Implementation of StackSource protocol. 
        /// </summary>
        public override double SampleTimeRelativeMSecLimit { get { return m_sampleTimeRelativeMSecLimit; } }
        #endregion

        #region private
        /// <summary>
        /// private
        /// </summary>
        protected StackSourceInterner m_Interner;

        /// <summary>
        /// private
        /// </summary>
        protected StackSourceModuleIndex m_emptyModuleIdx;
        internal GrowableArray<StackSourceSample> m_samples;
        double m_sampleTimeRelativeMSecLimit;
        #endregion
    }

    /// <summary>
    /// An ActivityComputer is a state machine that track information about Activities.  In particular, it can
    /// compute a activity aware call stack. (GetCallStack).  
    /// </summary>
    public class ActivityComputer
    {
        /// <summary>
        /// Construct a new ActivityComputer that will process events from 'eventLog' and output activity - aware stacks to 'outputStackSource'.  
        /// </summary>
        public ActivityComputer(TraceLog eventLog, MutableTraceEventStackSource outputStackSource)
        {
            m_eventLog = eventLog;
            m_outputSource = outputStackSource;
            m_perActivityStackIndexMaps = new Dictionary<CallStackIndex, StackSourceCallStackIndex>[eventLog.Activities.Count + 1];
        }

        [Obsolete("Experimental")] 
        public void AddCallbackForAwaits(TraceEventDispatcher source, Action<TraceEvent> callback)
        {
            TplEtwProviderTraceEventParser parser = new TplEtwProviderTraceEventParser(source);
            parser.TaskWaitEnd += delegate(TaskWaitEndArgsTraceData data) { callback(data); };
        }

        [Obsolete("Experimental")]
        public void GetCallStack(CallStackIndex baseStack, TraceActivity activity, Func<TraceThread, StackSourceCallStackIndex> topFrames)
        {
            GetCallStackWithActivityFrames(baseStack, activity, topFrames);
        }

        /// <summary>
        /// Returns a calls stack associated with 'data' were the thread and process part of the the stack is 'top'   If topFrames 
        /// is null the thread and process frames are used (GetCallStackForThread).    Note that this routine assumes that for a given 
        /// stack and activity for the event, that 'topFrames' will always be the same (and it caching will be incorrect if you violate this). 
        /// </summary>
        public StackSourceCallStackIndex GetCallStack(TraceEvent data, Func<TraceThread, StackSourceCallStackIndex> topFrames = null)
        {
            TraceActivity activity = data.Activity();
            CallStackIndex callStack = data.CallStackIndex();

            // Figure out the cache to put the call stack->stackSourceCallStack map.  
            Debug.Assert((int)ActivityIndex.Invalid == -1);
            //m_callStackMap = m_perActivityStackIndexMaps[(int)activity.Index + 1];
            //if (m_callStackMap == null)
            //    m_callStackMap = m_perActivityStackIndexMaps[(int)activity.Index + 1] = new Dictionary<CallStackIndex, StackSourceCallStackIndex>();

            // Bit of a  hack, if we are a CSwitch in the THreadPool, then we assume that it entered the threadpool logic and thus does not have an activity.   
            if (data is CSwitchTraceData && ThreadOnlyInThreadPool(callStack, m_eventLog.CallStacks))
                activity = m_eventLog.Activities[data.Thread().DefaultActivityIndex];

            return GetCallStackWithActivityFrames(callStack, activity, topFrames);
        }

        #region Private
        /// <summary>
        /// Bit of a hack.  Currently CLR thread pool does not have complete events to indicate a thread 
        /// pool item is complete.  Because of this they may be extended too far.   We use the fact that 
        /// we have a call stack that is ONLY in the thread pool as a way of heuristically finding the end.  
        /// 
        /// // TODO FIX NOW don't make this public.  
        /// </summary>
#if !PUBLIC_ONLY
        public
#endif 
        static bool ThreadOnlyInThreadPool(CallStackIndex callStack, TraceCallStacks callStacks)
        {
            var codeAddresses = callStacks.CodeAddresses;
            bool onlyThreadPoolModules = true;
            bool brokenStack = true;
            while (callStack != CallStackIndex.Invalid)
            {
                var codeAddrIdx = callStacks.CodeAddressIndex(callStack);
                var module = codeAddresses.ModuleFile(codeAddrIdx);
                if (module == null)
                {
                    onlyThreadPoolModules = false;
                    break;
                }
                var moduleName = module.Name;
                if (!moduleName.StartsWith("wow", StringComparison.OrdinalIgnoreCase) &&
                    !moduleName.StartsWith("kernel", StringComparison.OrdinalIgnoreCase) &&
                    string.Compare(moduleName, "ntdll", StringComparison.OrdinalIgnoreCase) != 0 &&
                    string.Compare(moduleName, "w3tp", StringComparison.OrdinalIgnoreCase) != 0 &&
                    string.Compare(moduleName, "clr", StringComparison.OrdinalIgnoreCase) != 0 &&
                    string.Compare(moduleName, "mscorwks", StringComparison.OrdinalIgnoreCase) != 0)
                {
                    if (string.Compare(moduleName, "ntoskrnl", StringComparison.OrdinalIgnoreCase) != 0)
                        return false;
                }
            else
                    brokenStack = false;
                callStack = callStacks.Caller(callStack);
        }
            return !brokenStack;
        }

        /// <summary>
        /// if 'activity' has not creator (it is top-level), then return baseStack (near execution) followed by 'top' representing the thread-process frames.
        /// 
        /// otherwise, find the fragment of 'baseStack' up to the point to enters the threadpool (the user code) and splice it to the stack of the creator
        /// of the activity and return that.  (thus returning your full user-stack).  
        /// </summary>
        private StackSourceCallStackIndex GetCallStackWithActivityFrames(CallStackIndex baseStack, TraceActivity activity, Func<TraceThread, StackSourceCallStackIndex> topFrames)
        {
            // We keep a cache so to speed things up quite a bit.  
            StackSourceCallStackIndex ret;
            // if (m_callStackMap.TryGetValue(baseStack, out ret))
            //    return ret;

            TraceActivity creatorActivity = activity.Creator;
            if (creatorActivity != null)
            {
                // Trim off the frames that just represent the logging of the ETW event.  They are not interesting.   
                CallStackIndex creationStackFragment = TrimETWFrames(activity.CreationCallStackIndex);
                StackSourceCallStackIndex fullCreationStack = GetCallStackWithActivityFrames(creationStackFragment, creatorActivity, topFrames);

                // We also wish to trim off the top of the tail, that is 'above' (closer to root) than the transition from the threadPool Execute (Run) method.  
                CallStackIndex threadPoolTransition = FindThreadPoolTransition(baseStack);

                // If baseStack is recursive with the frame we already have, do nothing.  
                StackSourceFrameIndex taskMarkerFrame = IsRecursiveTask(baseStack, threadPoolTransition, fullCreationStack);
                if (taskMarkerFrame != StackSourceFrameIndex.Invalid)
                {
                    UpdateTaskMarkerFrame(taskMarkerFrame, activity.Thread.ThreadID.ToString());         // Add the thread ID to the 'STARTING TASK' frame if necessary
                    return fullCreationStack;
                }

                // Add a frame that shows that we are starting a task 
                StackSourceFrameIndex threadFrameIndex = m_outputSource.Interner.FrameIntern("STARTING TASK on Thread " + activity.Thread.ThreadID);
                fullCreationStack = m_outputSource.Interner.CallStackIntern(threadFrameIndex, fullCreationStack);

                // and take the region between creationStackFragment and threadPoolTransition and concatenate it to fullCreationStack.  
                ret = SpliceStack(baseStack, threadPoolTransition, fullCreationStack);
            }
            else
            {
                StackSourceCallStackIndex rootFrames;
                if (topFrames != null)
                    rootFrames = topFrames(activity.Thread);
                else
                    rootFrames = m_outputSource.GetCallStackForThread(activity.Thread);
                ret = m_outputSource.GetCallStack(baseStack, rootFrames, null); // TODO FIX should do caching.   m_callStackMap
            }
            return ret;
        }

        /* Support functions for GetCallStack */
        /// <summary>
        /// Trims off frames that call ETW logic and return.   If the pattern is not matched, we return  callStackIndex
        /// </summary>
        private CallStackIndex TrimETWFrames(CallStackIndex callStackIndex)
        {
            if (m_methodFlags == null)
                ResolveWellKnownSymbols();

            CallStackIndex ret = callStackIndex;        // iF we don't see the TplEtwProvider.TaskScheduled event just return everything.   
            bool seenTaskScheduled = false;
            while (callStackIndex != CallStackIndex.Invalid)
            {
                CodeAddressIndex codeAddressIndex = m_eventLog.CallStacks.CodeAddressIndex(callStackIndex);
                MethodIndex methodIndex = m_eventLog.CallStacks.CodeAddresses.MethodIndex(codeAddressIndex);

                callStackIndex = m_eventLog.CallStacks.Caller(callStackIndex);

                // TODO FIX NOW fix if you don't have symbols 
                if (((uint)methodIndex < (uint)m_methodFlags.Length))
                {
                    MethodFlags flags = m_methodFlags[(int)methodIndex];
                    if (seenTaskScheduled)
                    {
                        if ((flags & MethodFlags.TaskScheduleHelper) == 0)  // We have already TplEtwProvider.TaskScheduled.  If this is not a helper, we are done.  
                            break;
                        ret = callStackIndex;                               // Eliminate the helper frame as well.  
                    }
                    else if ((flags & MethodFlags.TaskSchedule) != 0)       // We see TplEtwProvider.TaskScheduled, eliminate at least this, but see if we can eliminate helpers above.  
                    {
                        seenTaskScheduled = true;
                        ret = callStackIndex;
                    }
                }
            }
            return ret;
        }

        /// <summary>
        /// If the stack from 'startStack' (closest to execution) through 'stopStack' is the same as 'baseStack' return a non-invalid frame 
        /// indicating that it is recursive and should be dropped.  The frame index returned is the name of the task on 'baseStack' that
        /// begins the recursion (so you can update it if necessary)
        /// </summary>
        private StackSourceFrameIndex IsRecursiveTask(CallStackIndex startStack, CallStackIndex stopStack, StackSourceCallStackIndex baseStack)
        {
            CallStackIndex newStacks = startStack;
            StackSourceCallStackIndex existingStacks = baseStack;
            for (; ; )
            {
                if (newStacks == CallStackIndex.Invalid)
                    return StackSourceFrameIndex.Invalid;
                if (existingStacks == StackSourceCallStackIndex.Invalid)
                    return StackSourceFrameIndex.Invalid;

                if (newStacks == stopStack)
                    break;

                var existingFrameName = m_outputSource.GetFrameName(m_outputSource.GetFrameIndex(existingStacks), true);
                var newFrameCodeAddressIndex = m_eventLog.CallStacks.CodeAddressIndex(newStacks);
                if (newFrameCodeAddressIndex == CodeAddressIndex.Invalid)
                    return StackSourceFrameIndex.Invalid;

                var newFrameMethodIndex = m_eventLog.CodeAddresses.MethodIndex(newFrameCodeAddressIndex);
                if (newFrameMethodIndex == MethodIndex.Invalid)
                    return StackSourceFrameIndex.Invalid;
                var newFrameName = m_eventLog.CodeAddresses.Methods.FullMethodName(newFrameMethodIndex);

                // TODO FIX NOW do this better. 
                if (!existingFrameName.EndsWith(newFrameName))
                    return StackSourceFrameIndex.Invalid;

                existingStacks = m_outputSource.GetCallerIndex(existingStacks);
                newStacks = m_eventLog.CallStacks.Caller(newStacks);
            }

            var frameIdx = m_outputSource.GetFrameIndex(existingStacks);
            var frameName = m_outputSource.GetFrameName(frameIdx, true);
            if (!frameName.StartsWith("STARTING TASK on Thread"))
                return StackSourceFrameIndex.Invalid;
            return frameIdx;
        }

        /// <summary>
        /// Create a stack which is executing at 'startStack' and finds the region until 'stopStack', appending that (in order) to 'baseStack'.  
        /// </summary>
        private StackSourceCallStackIndex SpliceStack(CallStackIndex startStack, CallStackIndex stopStack, StackSourceCallStackIndex baseStack)
        {
            if (startStack == CallStackIndex.Invalid || startStack == stopStack)
                return baseStack;

            var codeAddress = m_eventLog.CallStacks.CodeAddressIndex(startStack);
            var caller = m_eventLog.CallStacks.Caller(startStack);
            var callerStack = SpliceStack(caller, stopStack, baseStack);
            var frameIdx = m_outputSource.GetFrameIndex(codeAddress);
            StackSourceCallStackIndex result = m_outputSource.Interner.CallStackIntern(frameIdx, callerStack);

            // m_callStackMap[startStack] = result;
            return result;
        }

        /// <summary>
        /// Returns the point in 'callStackIndex' where the CLR thread pool transitions from 
        /// a thread pool worker to the work being done by the threadpool.  
        /// 
        /// 
        /// Basically we find the closest to execution (furthest from thread-start) call to a 'Run' method
        /// that shows we are running an independent task.  
        /// </summary>
        private CallStackIndex FindThreadPoolTransition(CallStackIndex callStackIndex)
        {
            CallStackIndex origCallStackIndex = callStackIndex;
            if (m_methodFlags == null)
                ResolveWellKnownSymbols();

            CallStackIndex ret = CallStackIndex.Invalid;
            while (callStackIndex != CallStackIndex.Invalid)
            {
                CodeAddressIndex codeAddressIndex = m_eventLog.CallStacks.CodeAddressIndex(callStackIndex);
                MethodIndex methodIndex = m_eventLog.CallStacks.CodeAddresses.MethodIndex(codeAddressIndex);

                // TODO FIX NOW fix if you don't have symbols 
                if ((uint)methodIndex < (uint)m_methodFlags.Length)
                {
                    if ((m_methodFlags[(int)methodIndex] & MethodFlags.TaskRun) != 0)
                    {
                        if (ret == CallStackIndex.Invalid)
                            ret = callStackIndex;
                        return ret;
                    }
                    else if ((m_methodFlags[(int)methodIndex] & MethodFlags.TaskRunHelper) != 0)
                        ret = callStackIndex;
                }
                else
                    ret = CallStackIndex.Invalid;

                callStackIndex = m_eventLog.CallStacks.Caller(callStackIndex);
            }
            // TODO This happens after the of the task or on broken stacks.   For now we eliminate the info.  (is this a good idea?)
            return origCallStackIndex;
            // return CallStackIndex.Invalid;
        }

        /// <summary>
        /// taskMarkerFrame must be a frame of the form STARTING TASK on Thread NN NN NN ..
        /// and newTaskID is NNN.   If newTaskID is in that set, then do nothing.  Otherwise update
        /// that frame node to include NNN.  
        /// </summary>
        private void UpdateTaskMarkerFrame(StackSourceFrameIndex taskMarkerFrame, string newTaskID)
        {
            var frameName = m_outputSource.GetFrameName(taskMarkerFrame, true);
            Debug.Assert(frameName.StartsWith("STARTING TASK on Thread"));
            var curSearchIdx = 22;      // Skips the STARTING TASK ...
            for (; ; )
            {
                var index = frameName.IndexOf(newTaskID, curSearchIdx);
                if (index < 0)
                    break;
                curSearchIdx = index + newTaskID.Length;
                // Already present, we can return. 
                if (frameName[index - 1] == ' ' && (curSearchIdx == frameName.Length || frameName[curSearchIdx] == ' '))
                    return;
            }
            m_outputSource.Interner.UpdateFrameName(taskMarkerFrame, frameName + " " + newTaskID);
        }

        /// <summary>
        /// Used by TrimETWFrames and FindThreadPoolTransition to find particular frame names and place the information in 'm_methodFlags'
        /// </summary>
        private void ResolveWellKnownSymbols()
                {
            Debug.Assert(m_methodFlags == null);

            StringWriter sw = new StringWriter();
            using (var symbolReader = new SymbolReader(sw))
            {
                foreach (TraceModuleFile moduleFile in m_eventLog.ModuleFiles)
                    {
                    if (moduleFile.Name.StartsWith("mscorlib.ni", StringComparison.OrdinalIgnoreCase))
                        {
                        // We can skip V2.0 runtimes (we may have more than one because 64 and 32 bit.     
                        if (!moduleFile.FilePath.Contains("NativeImages_v2"))
                            m_eventLog.CodeAddresses.LookupSymbolsForModule(symbolReader, moduleFile);
                        }
                    }
                        }

            TraceMethods methods = m_eventLog.CodeAddresses.Methods;
            m_methodFlags = new MethodFlags[methods.Count];
            for (MethodIndex methodIndex = 0; methodIndex < (MethodIndex)methods.Count; methodIndex++)
                        {
                TraceModuleFile moduleFile = m_eventLog.ModuleFiles[methods.MethodModuleFileIndex(methodIndex)];
                if (moduleFile == null)
                    continue;
                if (moduleFile.Name.StartsWith("mscorlib", StringComparison.OrdinalIgnoreCase))
                        {
                    string name = methods.FullMethodName(methodIndex);
                    if (name.StartsWith("System.Threading.ExecutionContext.Run") || 
                        name.StartsWith("System.Threading.Tasks.AwaitTaskContinuation.Run") || 
                        name.StartsWith("System.Threading.Tasks.Task.Execute")) 
                        m_methodFlags[(int)methodIndex] |= MethodFlags.TaskRun;
                    else if (name.Contains("System.Threading.Tasks.Task") && name.Contains(".InnerInvoke"))
                        m_methodFlags[(int)methodIndex] |= MethodFlags.TaskRunHelper;
                    else if (name.StartsWith("System.Threading.Tasks.TplEtwProvider.TaskScheduled") || name.StartsWith("System.Threading.Tasks.TplEtwProvider.TaskWaitBegin("))
                        m_methodFlags[(int)methodIndex] |= MethodFlags.TaskSchedule;
                    else if ((name.StartsWith("System.Runtime.CompilerServices.AsyncTaskMethodBuilder") && name.Contains(".AwaitUnsafeOnCompleted")) ||
                             name.StartsWith("System.Threading.Tasks.Task.ScheduleAndStart") ||
                             (name.StartsWith("System.Runtime.CompilerServices.TaskAwaiter") && (name.Contains("OnCompleted") || name.Contains(".OutputWaitEtwEvents"))))
                        m_methodFlags[(int)methodIndex] |= MethodFlags.TaskScheduleHelper;
                        }
                    }
                }

        /// <summary>
        /// We look for various well known methods inside the Task library.   This array maps method indexes 
        /// and returns a bitvector of 'kinds' of methods (Run, Schedule, ScheduleHelper).  
        /// </summary>
        MethodFlags[] m_methodFlags;
        [Flags]
        enum MethodFlags : byte
        {
            TaskRun = 1,                  // This is a method that marks a frame that runs a task (frame toward threadStart are irrelevant)
            TaskRunHelper = 2,            // This method if 'below' (away from thread root) from a TackRun should also be removed.  
            TaskSchedule = 4,             // This is a method that marks the scheduling of a task (frames toward execution are irrelevant)
            TaskScheduleHelper = 4,       // This method if 'above' (toward thread root), from a TaskSchedule should also be removed.  
        }

        private TraceLog m_eventLog;
        private MutableTraceEventStackSource m_outputSource;

        /// <summary>
        /// For every activity this will cache each encountered CallStackIndex to the corresponding
        /// StackSourceCallStackIndex
        /// TODO: Confirm this is saving us significant work.
        /// </summary>
        private Dictionary<CallStackIndex, StackSourceCallStackIndex>[] m_perActivityStackIndexMaps;

        // This is active only for the duration of a a 'GetGCCallStack' call.  
        // Dictionary<CallStackIndex, StackSourceCallStackIndex> m_callStackMap;
        #endregion
    }
}

