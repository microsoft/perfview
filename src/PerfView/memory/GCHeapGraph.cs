#if false // TODO FIX NOW REMOVE 

using System.Collections.Generic;
using FastSerialization;
using System.Diagnostics;
using System.IO;
using Address = System.UInt64;

using ClrMemory;
namespace Graphs
{
    class GCHeapGraph : MemoryGraph
    {
        public GCHeapGraph(GCHeap heap)
            : base((int)heap.NumberOfObjects)
        {
            // TODO is basically nodes that have not had 'SetNode' done to them.  Thus the node index has been
            // allocated, but we have not processed the defintion of the node.  
            // This list should NEVER contain duplicates (you should test if you already processed the node before
            // adding to this queue. 
            Queue<NodeIndex> toDo = new Queue<NodeIndex>();

            // Create the root node. 
            var root = new MemoryNodeBuilder(this, "[root]");

            // Create categories for the roots.  
            var nodeForKind = new MemoryNodeBuilder[((int)GCRootKind.Max) + 1];

            var otherRoots = root.FindOrCreateChild("[other roots]");
            nodeForKind[(int)GCRootKind.LocalVar] = otherRoots.FindOrCreateChild("[unknown local vars]");
            for (int i = 0; i < nodeForKind.Length; i++)
            {
                if (nodeForKind[i] == null)
                    nodeForKind[i] = otherRoots.FindOrCreateChild("[" + ((GCRootKind)i).ToString().ToLower() + " handles]");
            }
            var unreachable = root.FindOrCreateChild("[unreachable but not yet collected]");

            foreach (var gcRoot in heap.Roots)
            {
                if (gcRoot.HeapReference == 0)
                    continue;

                // TODO FIX NOW condition should not be needed, should be able to assert it.   
                if (!heap.IsInHeap(gcRoot.HeapReference))
                    continue;
                Debug.Assert(heap.IsInHeap(gcRoot.HeapReference));

                // Make a node for it, and notice if the root is already in the heap.  
                var lastLimit = NodeIndexLimit;
                var newNodeIdx = GetNodeIndex(gcRoot.HeapReference);

                var nodeForGcRoot = nodeForKind[(int)gcRoot.Kind];
                if (gcRoot.Type != null)
                {
                    var prefix = "other";
                    if (gcRoot.Kind == GCRootKind.StaticVar)
                        prefix = "static";
                    else if (gcRoot.Kind == GCRootKind.LocalVar)
                        prefix = "local";
                    var appDomainNode = root.FindOrCreateChild("[appdomain " + gcRoot.AppDomainName + "]");
                    var varKindNode = appDomainNode.FindOrCreateChild("[" + prefix + " vars]");
                    var moduleName = Path.GetFileNameWithoutExtension(gcRoot.Type.ModuleFilePath);
                    var moduleNode = varKindNode.FindOrCreateChild("[" + prefix + " vars " + moduleName + "]");
                    var typeNode = moduleNode.FindOrCreateChild("[" + prefix + " vars " + gcRoot.Type.Name + "]");
                    var name = gcRoot.Type.Name + "+" + gcRoot.Name + " [" + prefix + " var]";
                    nodeForGcRoot = typeNode.FindOrCreateChild(name, gcRoot.Type.ModuleFilePath);

                    // TODO REMOVE
                    // if (nodeForGcRoot.Size == 0)
                    //    nodeForGcRoot.Size = 4;
                }

                // Give the user a bit more information about runtime internal handles 
                if (gcRoot.Kind == GCRootKind.Pinning)
                {
                    var pinnedType = heap.GetObjectType(gcRoot.HeapReference);
                    if (pinnedType.Name == "System.Object []")
                    {
                        // Traverse pinned object arrays a bit 'manually' here so we tell the users they are likely handles.  
                        var handleArray = new MemoryNodeBuilder(
                            this, "[likely runtime object array handle table]", pinnedType.ModuleFilePath, newNodeIdx);
                        handleArray.Size = pinnedType.GetSize(gcRoot.HeapReference);
                        nodeForGcRoot.AddChild(handleArray);

                        pinnedType.EnumerateRefsOfObjectCarefully(gcRoot.HeapReference, delegate(Address childRef, int childFieldOffset)
                        {
                            // Poor man's visited bit.  If we did not need to add a node, then we have visited it.  
                            var childLastLimit = NodeIndexLimit;
                            var childNodeIdx = GetNodeIndex(childRef);
                            if (childNodeIdx < childLastLimit)       // Already visited, simply add the child and move one
                            {
                                handleArray.AddChild(childNodeIdx);
                                return;
                            }

                            var childType = heap.GetObjectType(childRef);
                            if (childType.Name == "System.String")
                            {
                                // TODO FIX NOW:  Only want to morph the name if something else does not point at it. 
                                var literal = new MemoryNodeBuilder(
                                    this, "[likely string literal]", childType.ModuleFilePath, childNodeIdx);
                                literal.Size = childType.GetSize(childRef);
                                handleArray.AddChild(literal);
                            }
                            else
                            {
                                handleArray.AddChild(childNodeIdx);
                                toDo.Enqueue(childNodeIdx);
                            }
                        });
                        continue;       // we are done processing this node.  
                    }
                }

                nodeForGcRoot.AddChild(newNodeIdx);
                if (newNodeIdx >= lastLimit)      // have not been visited.  
                    toDo.Enqueue(newNodeIdx);
            }

            root.AllocateTypeIndexes();

            // Create the necessary types.   
            int memoryTypesStart = (int)NodeTypeIndexLimit;
            foreach (var type in heap.Types)
            {
                NodeTypeIndex nodeType = CreateType(type.Name, type.ModuleFilePath);
                Debug.Assert((int)nodeType == (int)type.Index + memoryTypesStart);
            }

            var children = new GrowableArray<NodeIndex>();
            while (toDo.Count > 0)
            {
                var nodeIndex = toDo.Dequeue();
                var objRef = GetAddress(nodeIndex);

                children.Clear();
                GCHeapType objType = heap.GetObjectType(objRef);
                objType.EnumerateRefsOfObjectCarefully(objRef, delegate(Address childRef, int childFieldOffset)
                {
                    Debug.Assert(heap.IsInHeap(childRef));
                    var lastLimit = NodeIndexLimit;
                    var childNodeIdx = GetNodeIndex(childRef);
                    children.Add(childNodeIdx);

                    // Poor man's visited bit.  If the index we just asked for is a new node, put it in the work queue
                    if (childNodeIdx >= lastLimit)
                        toDo.Enqueue(childNodeIdx);
                });

                int objSize = objType.GetSize(objRef);
                Debug.Assert(objSize > 0);
                SetNode(nodeIndex, (NodeTypeIndex)((int)objType.Index + memoryTypesStart), objSize, children);
            }

            long unreachableSize = (heap.TotalSize - TotalSize);
            unreachable.Size = (int)unreachableSize;
            if (unreachable.Size != unreachableSize)
                unreachable.Size = int.MaxValue;        // TODO not correct on overflow

            // We are done!
            RootIndex = root.Build();
            AllowReading();
        }

#region private
#endregion
    }
}

#endif