using Microsoft.Diagnostics.Tracing.Stacks;
using System.IO;

namespace Graphs
{
    internal sealed class GenerationAwareMemoryGraphBuilder
    {
        private TextWriter _Log;

        private int _GenerationToCondemn;
        private DotNetHeapInfo _HeapInfo;
        private MemoryGraphStackSource _OriginalStackSource;
        private MemoryGraph _OriginalMemoryGraph;
        private MemoryGraph _NewMemoryGraph;

        private MemoryNodeBuilder _RootNode;
        private MemoryNodeBuilder _Gen1RootNode;
        private MemoryNodeBuilder _Gen2RootNode;
        private MemoryNodeBuilder _UnknownRootNode;

        private MemoryNodeBuilder[] _OldNodeToNewParentMap;

        private NodeType _OldNodeTypeStorage;

        internal static MemoryGraphStackSource CreateStackSource(
            GCHeapDump gcDump, TextWriter log, int generationToCondemn)
        {
            MemoryGraph graph = gcDump.MemoryGraph;

            if (generationToCondemn >= 0 && generationToCondemn < 2)
            {
                GenerationAwareMemoryGraphBuilder builder = new GenerationAwareMemoryGraphBuilder(log, generationToCondemn, graph, gcDump.DotNetHeapInfo);
                graph = builder.CreateGenerationAwareMemoryGraph();
            }

            return new MemoryGraphStackSource(graph, log, gcDump.CountMultipliersByType);
        }

        private GenerationAwareMemoryGraphBuilder(
            TextWriter log,
            int generationToCondemn,
            MemoryGraph memGraph,
            DotNetHeapInfo heapInfo)
        {
            _Log = log;
            _GenerationToCondemn = generationToCondemn;
            _OriginalMemoryGraph = memGraph;
            _OriginalStackSource = new MemoryGraphStackSource(memGraph, log);
            _HeapInfo = heapInfo;
            _OldNodeToNewParentMap = new MemoryNodeBuilder[(int)_OriginalMemoryGraph.NodeIndexLimit];
            _OldNodeTypeStorage = _OriginalMemoryGraph.AllocTypeNodeStorage();
        }

        internal MemoryGraph CreateGenerationAwareMemoryGraph()
        {
            // Create a new memory graph.
            // TODO: Is this size appropriate?
            _NewMemoryGraph = new MemoryGraph(1024);

            // Create the new root node.
            _RootNode = new MemoryNodeBuilder(_NewMemoryGraph, "[.NET Generation Aware Roots]");

            // Create old gen root nodes.
            if (_GenerationToCondemn < 1)
            {
                _Gen1RootNode = _RootNode.FindOrCreateChild("[Gen1 Roots]");
            }
            _Gen2RootNode = _RootNode.FindOrCreateChild("[Gen2 Roots]");
            _UnknownRootNode = _RootNode.FindOrCreateChild("[not reachable from roots]");

            // Traverse the input graph and re-build it as a generation aware graph.
            // This means that all types are re-written to include the generation.
            // We also add additional edges to account for old generation roots.

            // NOTE: This API will also visit nodes that have no path to root.
            _OriginalStackSource.ForEach(VisitNodeFromSample);

            _NewMemoryGraph.RootIndex = _RootNode.Build();
            _NewMemoryGraph.AllowReading();

            // Return the new graph.
            return _NewMemoryGraph;
        }

        private void VisitNodeFromSample(StackSourceSample sample)
        {
            MemoryNode currentNode = (MemoryNode)_OriginalMemoryGraph.GetNode((NodeIndex)sample.SampleIndex, _OriginalMemoryGraph.AllocNodeStorage());
            VisitNode(currentNode);
        }

        private void VisitNode(MemoryNode currentNode)
        {
            MemoryNode oldMemNode = currentNode;

            // Get the generation for the current node.
            int generation = _HeapInfo.GenerationFor(oldMemNode.Address);

            // Create a MemoryNodeBuilder for the new graph that represents the current node
            // unless the current node is the root, as we've already created one.
            MemoryNodeBuilder newMemNodeBuilder = null;
            if (currentNode.Index == _OriginalMemoryGraph.RootIndex)
            {
                newMemNodeBuilder = _RootNode;
            }
            else
            {
                // Get the parent node.
                MemoryNodeBuilder parentMemNodeBuilder = null;
                if ((oldMemNode.Address != 0) && (generation > _GenerationToCondemn))
                {
                    if (generation == 1)
                    {
                        parentMemNodeBuilder = _Gen1RootNode;
                    }
                    else
                    {
                        parentMemNodeBuilder = _Gen2RootNode;
                    }
                }
                else
                {
                    parentMemNodeBuilder = _OldNodeToNewParentMap[(int)currentNode.Index];
                }

                if (parentMemNodeBuilder == null)
                {
                    parentMemNodeBuilder = _UnknownRootNode;
                }

                // Get the current node's type and object address.
                NodeType nodeType = _OriginalMemoryGraph.GetType(oldMemNode.TypeIndex, _OldNodeTypeStorage);

                // Create the new generation aware type name.
                string typeName = null;
                if (oldMemNode.Address != 0 && generation >= 0)
                {
                    if (generation == 3)
                    {
                        typeName = string.Format("LOH: {0}", nodeType.Name);
                    }
                    else if (generation == 4)
                    {
                        typeName = string.Format("POH: {0}", nodeType.Name);
                    }
                    else
                    {
                        typeName = string.Format("Gen{0}: {1}", generation, nodeType.Name);
                    }
                }
                else
                {
                    if (oldMemNode.Address != 0)
                    {
                        _Log.WriteLine(string.Format("Generation: {0}; Address: {1}; Type: {2}", generation, oldMemNode.Address, nodeType.Name));
                    }
                    typeName = nodeType.Name;
                }

                // Create the new node.
                if (ShouldAddToGraph(oldMemNode, nodeType))
                {
                    if (oldMemNode.Address == 0)
                    {
                        newMemNodeBuilder = parentMemNodeBuilder.FindOrCreateChild(typeName);
                    }
                    else
                    {
                        NodeIndex newNodeIndex = _NewMemoryGraph.GetNodeIndex(oldMemNode.Address);
                        newMemNodeBuilder = new MemoryNodeBuilder(_NewMemoryGraph, typeName, null, newNodeIndex);

                        parentMemNodeBuilder.AddChild(newMemNodeBuilder);

                        // Set the object size.
                        if (generation <= _GenerationToCondemn)
                        {
                            newMemNodeBuilder.Size = oldMemNode.Size;
                        }
                        else
                        {
                            _Log.WriteLine("Ignoring Object Size: " + typeName);
                        }
                    }
                }
            }

            // Associate all children of the current node with this object's new MemoryNodeBuilder.
            for (NodeIndex childIndex = oldMemNode.GetFirstChildIndex(); childIndex != NodeIndex.Invalid; childIndex = oldMemNode.GetNextChildIndex())
            {
                _OldNodeToNewParentMap[(int)childIndex] = newMemNodeBuilder;
            }
        }

        private bool ShouldAddToGraph(MemoryNode memNode, NodeType nodeType)
        {
            if (memNode.Address == 0 && !nodeType.Name.StartsWith("[static var "))
            {
                return true;
            }

            int gen = _HeapInfo.GenerationFor(memNode.Address);
            if (gen >= 0 && gen <= _GenerationToCondemn)
            {
                return true;
            }

            // Check my children.
            Node nodeStorage = _OriginalMemoryGraph.AllocNodeStorage();
            for (NodeIndex nodeIndex = memNode.GetFirstChildIndex(); nodeIndex != NodeIndex.Invalid; nodeIndex = memNode.GetNextChildIndex())
            {
                MemoryNode currentNode = (MemoryNode)_OriginalMemoryGraph.GetNode(nodeIndex, nodeStorage);

                if (currentNode.Address == 0)
                {
                    return true;
                }

                gen = _HeapInfo.GenerationFor(currentNode.Address);
                if (gen >= 0 && gen <= _GenerationToCondemn)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
