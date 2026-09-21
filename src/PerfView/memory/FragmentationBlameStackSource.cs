using Diagnostics.Tracing.StackSources;
using Microsoft.Diagnostics.Tracing.Stacks;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Address = System.UInt64;

namespace Graphs
{
    /// <summary>
    /// FragmentationBlameStackSource creates a stack source that shows which objects are causing
    /// heap fragmentation. It does this by:
    /// 1. Finding all "Free" objects (gaps in memory) in the heap
    /// 2. For each Free object, finding the object immediately before it in memory
    /// 3. Attributing the size of the Free object as "fragmentation cost" to that preceding object
    /// 4. Building paths to root for the blamed objects
    /// 
    /// This helps identify which objects (likely pinned or in older generations) are preventing
    /// compaction and causing fragmentation.
    /// </summary>
    public class FragmentationBlameStackSource : StackSource
    {
        /// <summary>
        /// Create a fragmentation blame stack source from a memory graph.
        /// </summary>
        /// <param name="graph">The memory graph to analyze</param>
        /// <param name="log">Log writer for diagnostic messages</param>
        public FragmentationBlameStackSource(MemoryGraph graph, TextWriter log)
        {
            m_graph = graph;
            m_log = log;
            m_nodeStorage = graph.AllocNodeStorage();
            m_tempNodeStorage = graph.AllocNodeStorage(); // Temporary storage for checking predecessors
            m_typeStorage = graph.AllocTypeNodeStorage();
            m_tempTypeStorage = graph.AllocTypeNodeStorage(); // Temporary storage for checking predecessor types
            m_sampleStorage = new StackSourceSample(this);

            // Build the spanning tree for paths to root (reuse MemoryGraphStackSource logic)
            m_underlyingStackSource = new MemoryGraphStackSource(graph, log);
            
            // Initialize the underlying stack source's parent array by calling ForEach
            // This builds the spanning tree that we need for GetCallerIndex to work correctly
            m_underlyingStackSource.ForEach(_ => { });
            
            // Build the fragmentation blame data structures
            BuildFragmentationData();
        }

        /// <summary>
        /// Build the fragmentation blame mapping by finding Free objects and their predecessors.
        /// </summary>
        private void BuildFragmentationData()
        {
            m_log?.WriteLine($"[FragmentationBlame] Starting fragmentation analysis...");

            // Step 1: Sort all nodes by address and calculate max address
            var nodesByAddress = new List<NodeAddressPair>();
            for (NodeIndex nodeIdx = 0; nodeIdx < m_graph.NodeIndexLimit; nodeIdx++)
            {
                Address addr = m_graph.GetAddress(nodeIdx);
                if (addr != 0) // Skip nodes without addresses (pseudo-nodes, root, etc.)
                {
                    nodesByAddress.Add(new NodeAddressPair { NodeIndex = nodeIdx, Address = addr });
                }
            }

            // Sort by address
            nodesByAddress.Sort((a, b) => a.Address.CompareTo(b.Address));

            m_log?.WriteLine($"[FragmentationBlame] Found {nodesByAddress.Count} nodes with addresses");

            // Step 2: Find Free objects and map them to their predecessors
            m_fragmentationCost = new Dictionary<NodeIndex, int>();
            int totalFragmentation = 0;
            int freeObjectCount = 0;

            for (int i = 0; i < nodesByAddress.Count; i++)
            {
                NodeIndex nodeIdx = nodesByAddress[i].NodeIndex;
                Node node = m_graph.GetNode(nodeIdx, m_nodeStorage);
                NodeType nodeType = m_graph.GetType(node.TypeIndex, m_typeStorage);

                // Check if this is a Free object
                if (nodeType.Name == "Free")
                {
                    freeObjectCount++;
                    int freeSize = node.Size;
                    totalFragmentation += freeSize;

                    // Find the object immediately before this Free object
                    if (i > 0)
                    {
                        NodeIndex precedingNodeIdx = nodesByAddress[i - 1].NodeIndex;
                        
                        // Don't blame other Free objects (only blame real objects)
                        Node precedingNode = m_graph.GetNode(precedingNodeIdx, m_tempNodeStorage);
                        NodeType precedingNodeType = m_graph.GetType(precedingNode.TypeIndex, m_tempTypeStorage);
                        
                        if (precedingNodeType.Name != "Free")
                        {
                            // Add this Free object's size to the fragmentation cost of the preceding object
                            m_fragmentationCost.TryGetValue(precedingNodeIdx, out int currentCost);
                            m_fragmentationCost[precedingNodeIdx] = currentCost + freeSize;
                        }
                    }
                    else
                    {
                        m_log?.WriteLine($"[FragmentationBlame] Warning: Free object at address {nodesByAddress[i].Address:x} has no predecessor");
                    }
                }
            }

            m_log?.WriteLine($"[FragmentationBlame] Found {freeObjectCount} Free objects");
            m_log?.WriteLine($"[FragmentationBlame] Total fragmentation: {totalFragmentation:n0} bytes ({totalFragmentation / 1048576.0:f2} MB)");
            m_log?.WriteLine($"[FragmentationBlame] Objects blamed for fragmentation: {m_fragmentationCost.Count}");

            // Step 3: Build a list of blamed nodes for enumeration
            m_blamedNodes = new List<NodeIndex>(m_fragmentationCost.Keys);

            if (m_fragmentationCost.Count == 0)
            {
                m_log?.WriteLine("[FragmentationBlame] Warning: No objects are blamed for fragmentation. This could mean:");
                m_log?.WriteLine("  - There are no Free objects in the heap");
                m_log?.WriteLine("  - The heap is fully compacted");
                m_log?.WriteLine("  - The dump was taken in a way that doesn't preserve Free objects");
            }
        }

        public override void ForEach(Action<StackSourceSample> callback)
        {
            // Only enumerate nodes that are blamed for fragmentation
            foreach (var nodeIdx in m_blamedNodes)
            {
                int fragmentationCost = m_fragmentationCost[nodeIdx];

                // Get node information
                Node node = m_graph.GetNode(nodeIdx, m_nodeStorage);

                // Create a sample for this node
                m_sampleStorage.Metric = fragmentationCost;
                m_sampleStorage.Count = 1;
                m_sampleStorage.SampleIndex = (StackSourceSampleIndex)nodeIdx;
                m_sampleStorage.StackIndex = (StackSourceCallStackIndex)nodeIdx;

                callback(m_sampleStorage);
            }
        }

        public override StackSourceCallStackIndex GetCallerIndex(StackSourceCallStackIndex callStackIndex)
        {
            // Delegate to the underlying stack source to get the path to root
            return m_underlyingStackSource.GetCallerIndex(callStackIndex);
        }

        public override StackSourceFrameIndex GetFrameIndex(StackSourceCallStackIndex callStackIndex)
        {
            // Delegate to the underlying stack source
            return m_underlyingStackSource.GetFrameIndex(callStackIndex);
        }

        public override string GetFrameName(StackSourceFrameIndex frameIndex, bool verboseName)
        {
            // Delegate to the underlying stack source
            return m_underlyingStackSource.GetFrameName(frameIndex, verboseName);
        }

        public override StackSourceSample GetSampleByIndex(StackSourceSampleIndex sampleIndex)
        {
            var nodeIdx = (NodeIndex)sampleIndex;
            
            // Return the fragmentation cost for this node
            int fragmentationCost;
            if (!m_fragmentationCost.TryGetValue(nodeIdx, out fragmentationCost))
            {
                fragmentationCost = 0;
            }

            m_sampleStorage.Metric = fragmentationCost;
            m_sampleStorage.Count = 1;
            m_sampleStorage.SampleIndex = sampleIndex;
            m_sampleStorage.StackIndex = (StackSourceCallStackIndex)nodeIdx;

            return m_sampleStorage;
        }

        public override int SampleIndexLimit
        {
            get { return (int)m_graph.NodeIndexLimit; }
        }

        public override int CallStackIndexLimit
        {
            get { return m_underlyingStackSource.CallStackIndexLimit; }
        }

        public override int CallFrameIndexLimit
        {
            get { return m_underlyingStackSource.CallFrameIndexLimit; }
        }

        public override double SampleTimeRelativeMSecLimit
        {
            get { return 0; }
        }

        #region private
        private struct NodeAddressPair
        {
            public NodeIndex NodeIndex;
            public Address Address;
        }

        private readonly MemoryGraph m_graph;
        private readonly TextWriter m_log;
        private readonly Node m_nodeStorage;
        private readonly Node m_tempNodeStorage;
        private readonly NodeType m_typeStorage;
        private readonly NodeType m_tempTypeStorage;
        private readonly StackSourceSample m_sampleStorage;
        private readonly MemoryGraphStackSource m_underlyingStackSource;

        // Maps NodeIndex -> fragmentation cost (size of Free objects following this node)
        private Dictionary<NodeIndex, int> m_fragmentationCost;
        
        // List of nodes that are blamed for fragmentation (for enumeration)
        private List<NodeIndex> m_blamedNodes;

        #endregion
    }
}
