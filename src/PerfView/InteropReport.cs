using Graphs;
using Microsoft.Diagnostics.Tracing.Etlx;
using System;
using System.Collections.Generic;
using System.IO;
using Address = System.UInt64;

namespace PerfView
{
    internal class GraphWalker
    {
        private List<NodeIndex> m_target = new List<NodeIndex>();
        private Queue<NodeIndex> m_source = new Queue<NodeIndex>();
        private VisitState[] m_visited;

        /// <summary>
        /// Add target node to search for
        /// </summary>
        /// <param name="node"></param>
        public void AddTarget(NodeIndex node)
        {
            m_target.Add(node);
        }

        /// <summary>
        /// Add source node to start search
        /// </summary>
        /// <param name="node"></param>
        public void AddSource(NodeIndex node)
        {
            m_source.Enqueue(node);
        }

        [Flags]
        private enum VisitState
        {
            Queued = 1,    // Put into m_source queue
            Visited = 2,    // Reachable through an arc from source nodes
            Target = 4,    // Target node
            Source = 8     // Source node
        };

        /// <summary>
        /// 
        /// </summary>
        /// <param name="c"></param>
        /// <returns>true on first visit of a target node</returns>
        private bool VisitChild(NodeIndex c)
        {
            VisitState state = m_visited[(int)c];

            if ((state & VisitState.Queued) == 0)
            {
                state |= VisitState.Queued;
                m_source.Enqueue(c);
            }

            m_visited[(int)c] = state | VisitState.Visited;

            if ((state & VisitState.Visited) == 0)
            {
                return (state & VisitState.Target) != 0;
            }
            else
            {
                return false;
            }
        }

        public IEnumerable<NodeIndex> GetVisitedTargets()
        {
            foreach (NodeIndex t in m_target)
            {
                if ((m_visited[(int)t] & VisitState.Visited) != 0)
                {
                    yield return t;
                }
            }
        }

        /// <summary>
        /// Breadth-first walk from source nodes
        /// </summary>
        /// <param name="graph"></param>
        public int WalkGraph(Graph graph)
        {
            Node node = graph.AllocNodeStorage();
            Node child = graph.AllocNodeStorage();

            m_visited = new VisitState[(int)graph.NodeIndexLimit];

            foreach (NodeIndex s in m_source)
            {
                m_visited[(int)s] |= VisitState.Queued | VisitState.Source;
            }

            foreach (NodeIndex t in m_target)
            {
                m_visited[(int)t] |= VisitState.Target;
            }

            int targetCount = 0;

            while (m_source.Count != 0)
            {
                NodeIndex nodeIndex = m_source.Dequeue();

                graph.GetNode(nodeIndex, node);

                // Visit children from graph
                for (NodeIndex i = node.GetFirstChildIndex(); i != NodeIndex.Invalid; i = node.GetNextChildIndex())
                {
                    //Trace.WriteLine(i);
                    if (VisitChild(i))
                    {
                        targetCount++;
                    }
                }
            }

            return targetCount;
        }
    }

    // This finds the cycles (strongly connected components) in the graph.
    // We would only reported it if the path includes at least one RCW and one CCW.
    internal class FindSCC
    {
        private class SCCInfo
        {
            public int m_index;
            public int m_lowLink;
            public Node node; // node is only allocated for the nodes we need to traverse.
            public string type; // We don't expect to have that many nodes so we just store the type here.
        }

        private SCCInfo[] m_sccInfo;
        private Stack<int> m_stack;
        private MemoryGraph m_graph;
        private TextWriter m_htmlRaw;
        private TextWriter m_log;
        private int index;
        private List<int> m_currentCycle = new List<int>();
        private int startNodeIdx;

        private string GetPrintableString(string s)
        {
            string sPrint = s.Replace("<", "&lt;");
            sPrint = sPrint.Replace(">", "&gt;");
            return sPrint;
        }

        public void Init(MemoryGraph graph, TextWriter writer, TextWriter log)
        {
            m_graph = graph;
            m_sccInfo = new SCCInfo[(int)graph.NodeIndexLimit];
            for (int i = 0; i < m_sccInfo.Length; i++)
            {
                m_sccInfo[i] = new SCCInfo();
            }
            index = 1;
            m_stack = new Stack<int>();
            m_htmlRaw = writer;
            m_log = log;
        }

        private void PrintEdges(int idx)
        {
            Node currentNode = m_sccInfo[idx].node;

            for (NodeIndex childIdx = currentNode.GetFirstChildIndex(); childIdx != NodeIndex.Invalid; childIdx = currentNode.GetNextChildIndex())
            {
                m_log.WriteLine("idx#{0}:{1:x}({2}), child#{3}: {4:x}({5})",
                    idx, m_graph.GetAddress((NodeIndex)idx), m_sccInfo[idx].type,
                    (int)childIdx, m_graph.GetAddress(childIdx), m_sccInfo[(int)childIdx].type);

                if (idx == (int)childIdx)
                {
                    continue;
                }

                if (m_currentCycle.Contains((int)childIdx))
                {
                    if ((int)childIdx == startNodeIdx)
                    {
                        m_htmlRaw.WriteLine("{0}({1:x})<font color=\"red\">-></font>{2}({3:x}) [end of cycle]<br><br>",
                            m_sccInfo[idx].type,
                            m_graph.GetAddress((NodeIndex)idx),
                            m_sccInfo[(int)childIdx].type,
                            m_graph.GetAddress(childIdx));
                        continue;
                    }
                    else if (m_sccInfo[(int)childIdx].m_index == 1)
                    {
                        m_sccInfo[(int)childIdx].m_index = 0;

                        m_htmlRaw.WriteLine("{0}({1:x})<font color=\"red\">-></font>",
                            m_sccInfo[idx].type,
                            m_graph.GetAddress((NodeIndex)idx));

                        PrintEdges((int)childIdx);
                    }
                    else
                    {
                        m_htmlRaw.WriteLine("{0}({1:x})<font color=\"red\">-></font>{2}({3:x}) [connecting to an existing graph in cycle]<br><br>",
                            m_sccInfo[idx].type,
                            m_graph.GetAddress((NodeIndex)idx),
                            m_sccInfo[(int)childIdx].type,
                            m_graph.GetAddress(childIdx));
                    }
                }
            }
        }

        private void FindCyclesOne(int idx)
        {
            m_sccInfo[idx].m_index = index;
            m_sccInfo[idx].m_lowLink = index;
            index++;
            m_stack.Push(idx);

            m_sccInfo[idx].node = m_graph.AllocNodeStorage();
            Node currentNode = m_sccInfo[idx].node;
            m_graph.GetNode((NodeIndex)idx, currentNode);

            for (NodeIndex childIdx = currentNode.GetFirstChildIndex(); childIdx != NodeIndex.Invalid; childIdx = currentNode.GetNextChildIndex())
            {
                if (m_sccInfo[(int)childIdx].m_index == 0)
                {
                    FindCyclesOne((int)childIdx);
                    m_sccInfo[idx].m_lowLink = Math.Min(m_sccInfo[idx].m_lowLink, m_sccInfo[(int)childIdx].m_lowLink);
                }
                else if (m_stack.Contains((int)childIdx))
                {
                    m_sccInfo[idx].m_lowLink = Math.Min(m_sccInfo[idx].m_lowLink, m_sccInfo[(int)childIdx].m_index);
                }
            }

            if (m_sccInfo[idx].m_index == m_sccInfo[idx].m_lowLink)
            {
                bool hasCCW = false;
                bool hasRCW = false;
                int currentIdx;
                m_currentCycle.Clear();
                NodeType type = m_graph.AllocTypeNodeStorage();
                do
                {
                    currentIdx = m_stack.Pop();

                    Node node = m_sccInfo[currentIdx].node;
                    m_graph.GetType(node.TypeIndex, type);

                    if (type.Name.StartsWith("[RCW"))
                    {
                        hasRCW = true;
                    }
                    else if (type.Name.StartsWith("[CCW"))
                    {
                        hasCCW = true;
                    }

                    m_currentCycle.Add(currentIdx);
                } while (idx != currentIdx);

                if (m_currentCycle.Count > 1)
                {
                    m_log.WriteLine("found a cycle of {0} nodes", m_currentCycle.Count);
                    if (hasCCW && hasRCW)
                    {
                        m_htmlRaw.WriteLine("<font size=\"3\" color=\"blue\">Cycle of {0} nodes<br></font>",
                            m_currentCycle.Count);
                        // Now print out all the nodes in this cycle.
                        for (int i = m_currentCycle.Count - 1; i >= 0; i--)
                        {
                            Node nodeInCycle = m_sccInfo[m_currentCycle[i]].node;
                            // Resetting this for printing purpose below.
                            m_sccInfo[m_currentCycle[i]].m_index = 1;
                            string typeName = GetPrintableString(m_graph.GetType(nodeInCycle.TypeIndex, type).Name);
                            m_sccInfo[m_currentCycle[i]].type = typeName;
                            m_htmlRaw.WriteLine("{0}<br>", typeName);
                        }
                        m_htmlRaw.WriteLine("<br><br>");

                        // Now print out the actual edges. Reusing the m_index field in SCCInfo.
                        // It doesn't matter where we start, just start from the first one.
                        startNodeIdx = m_currentCycle[m_currentCycle.Count - 1];
                        m_htmlRaw.WriteLine("<font size=\"3\" color=\"blue\">Paths</font><br>");
                        PrintEdges(startNodeIdx);
                    }
                }
            }
        }

        public void FindCycles(List<InteropInfo.CCWInfo> ccws)
        {
            m_htmlRaw.WriteLine("<font face=\"lucida console\" size=\"2\">");
            for (int i = 0; i < ccws.Count; i++)
            {
                if (m_sccInfo[(int)(ccws[i].node)].m_index == 0)
                {
                    FindCyclesOne((int)ccws[i].node);
                }
            }
            m_htmlRaw.WriteLine("</font>");
        }
    }

    internal class HeapDumpInteropObjects : PerfViewHtmlReport
    {
        private HeapDumpPerfViewFile m_heapDumpFile;
        private string m_mainOutput;
        private TextWriter m_htmlRaw;
        private TextWriter m_log;
        private MemoryGraph m_graph;
        private InteropInfo m_interop;

        public HeapDumpInteropObjects(HeapDumpPerfViewFile dataFile)
            : base(dataFile, "Interop report")
        {
            m_heapDumpFile = dataFile;
        }

        private StreamWriter m_writer;

        private void WriteAddress(Address addr, bool decode)
        {
            m_writer.Write(",0x{0:x8}", addr);

            if (decode)
            {
                m_writer.Write(',');
            }
        }

        private void ComInterfaceTable(bool bRcw, string kind, string action)
        {
            string report = m_mainOutput + "_" + kind + "comInf.csv";

            m_writer = new StreamWriter(report, false, new System.Text.UTF8Encoding(true, false));

            if (bRcw)
            {
                m_writer.WriteLine("RCW,Seq,Pointer,Interface Type,VfTable,,AddRef");
            }
            else
            {
                m_writer.WriteLine("CCW,Seq,Pointer,Interface Type,VfTable,,AddRef");
            }

            NodeType nodeType = new NodeType(m_graph);

            int count = 0;

            for (int i = 0; i < m_interop.m_listComInterfaceInfo.Count; i++)
            {
                InteropInfo.ComInterfaceInfo com = m_interop.m_listComInterfaceInfo[i];

                if (com.fRCW != bRcw)
                {
                    continue;
                }

                NodeIndex node = (com.fRCW ? m_interop.m_listRCWInfo[com.owner].node :
                                             m_interop.m_listCCWInfo[com.owner].node);

                int indexFirstComInf = (com.fRCW ? m_interop.m_listRCWInfo[com.owner].firstComInf :
                                                   m_interop.m_listCCWInfo[com.owner].firstComInf);

                m_writer.Write("{0}", (int)node);
                m_writer.Write(",{0}", i - indexFirstComInf);

                WriteAddress(com.addrInterface, false);

                if (com.typeID != NodeTypeIndex.Invalid)
                {
                    m_graph.GetType(com.typeID, nodeType);

                    m_writer.Write(",\"{0}\"", nodeType.Name);
                }
                else
                {
                    m_writer.Write(",");
                }

                WriteAddress(com.addrFirstVTable, true);
                WriteAddress(com.addrFirstFunc, true);
                m_writer.WriteLine();
                count++;
            }

            m_writer.Close();

            m_htmlRaw.WriteLine("<li><a href=\"{0}\">{1} COM interfaces {2} {3}</a></li>", report, count, action, kind);
        }

        private Node m_node;
        private NodeType m_nodeType;

        private void DecodeNode(NodeIndex n)
        {
            if (m_node == null)
            {
                m_node = m_graph.AllocNodeStorage();
                m_nodeType = new NodeType(m_graph);
            }

            m_graph.GetNode(n, m_node);

            m_node.GetType(m_nodeType);
        }

        private void WriteRCWInfo()
        {
            string report = m_mainOutput + "_" + "RCW.csv";

            m_writer = new StreamWriter(report, false, new System.Text.UTF8Encoding(true, false));
            m_writer.WriteLine("Index,RefCount,IUnknown,Jupiter,vftable,Interfaces,Type,Interface0,Interface1,Interface2,Interface3");

            InteropInfo.RCWInfo rcw;

            for (int i = 0; i < m_interop.m_listRCWInfo.Count; i++)
            {
                rcw = m_interop.m_listRCWInfo[i];

                DecodeNode(rcw.node);

                m_writer.Write("{0},{1}", rcw.node, rcw.refCount);
                WriteAddress(rcw.addrIUnknown, false);
                WriteAddress(rcw.addrJupiter, false);
                WriteAddress(rcw.addrVTable, false);
                m_writer.Write(",{0},\"{1}\"", rcw.countComInf, m_nodeType.Name);

                for (int indexComInf = 0; indexComInf < rcw.countComInf; indexComInf++)
                {
                    WriteAddress(m_interop.m_listComInterfaceInfo[rcw.firstComInf + indexComInf].addrInterface, false);
                }

                m_writer.WriteLine();
            }

            m_writer.Close();

            m_htmlRaw.WriteLine("<li><a href=\"{0}\">{1} RCWs</a></li>", report, m_interop.m_countRCWs);
        }

        private void WriteCCWInfo()
        {
            string report = m_mainOutput + "_" + "CCW.csv";

            m_writer = new StreamWriter(report, false, new System.Text.UTF8Encoding(true, false));
            m_writer.WriteLine("Index,RefCount,IUnknown,Handle,Interfaces,Type,Interface0,Interface1,Interface2,Interface3");

            InteropInfo.CCWInfo ccw;

            for (int i = 0; i < m_interop.m_listCCWInfo.Count; i++)
            {
                ccw = m_interop.m_listCCWInfo[i];

                DecodeNode(ccw.node);

                m_writer.Write("{0},{1}", ccw.node, ccw.refCount);
                WriteAddress(ccw.addrIUnknown, false);
                WriteAddress(ccw.addrHandle, false);
                m_writer.Write(",{0},\"{1}\"", ccw.countComInf, m_nodeType.Name);

                for (int indexComInf = 0; indexComInf < ccw.countComInf; indexComInf++)
                {
                    WriteAddress(m_interop.m_listComInterfaceInfo[ccw.firstComInf + indexComInf].addrInterface, false);
                }

                m_writer.WriteLine();
            }

            m_writer.Close();

            m_htmlRaw.WriteLine("<li><a href=\"{0}\">{1} CCWs</a></li>", report, m_interop.m_countRCWs);
        }

        private string GetPrintableString(string s)
        {
            string sPrint = s.Replace("<", "&lt;");
            sPrint = sPrint.Replace(">", "&gt;");
            return sPrint;
        }

        private void GenerateReports()
        {
            m_htmlRaw.WriteLine("Interop Objects (.csv files open in Excel)");

            m_htmlRaw.WriteLine("<ul>");
            WriteRCWInfo();
            ComInterfaceTable(false, "CCW", "exposed by CLR");

            WriteCCWInfo();
            ComInterfaceTable(true, "RCW", "referenced by CLR");

            m_htmlRaw.WriteLine("</ul>");

            GraphWalker walker = new GraphWalker();

            for (int i = 0; i < m_interop.m_listRCWInfo.Count; i++)
            {
                walker.AddTarget(m_interop.m_listRCWInfo[i].node);
            }

            m_htmlRaw.WriteLine("Reference Analysis");
            m_htmlRaw.WriteLine("<ul>");

            for (int i = 0; i < m_interop.m_listCCWInfo.Count; i++)
            {
                InteropInfo.CCWInfo ccw = m_interop.m_listCCWInfo[i];
                walker.AddSource(ccw.node);
                int count = walker.WalkGraph(m_graph);

                if (count != 0)
                {
                    m_htmlRaw.Write("<li>");

                    m_htmlRaw.Write("From CCW {0}, ", ccw.node);

                    DecodeNode(ccw.node);
                    m_htmlRaw.Write(GetPrintableString(m_nodeType.Name));

                    m_htmlRaw.Write(", {0} RCW reachable", count);

                    m_htmlRaw.WriteLine("</li>");

                    m_htmlRaw.WriteLine("<ol>");

                    int seq = 0;

                    foreach (NodeIndex t in walker.GetVisitedTargets())
                    {
                        DecodeNode(t);

                        m_htmlRaw.WriteLine("<li>");
                        m_htmlRaw.Write("RCW {0}, ", m_node.Index);
                        m_htmlRaw.Write(GetPrintableString(m_nodeType.Name));
                        m_htmlRaw.WriteLine("</ii>");

                        seq++;

                        if (seq == 10)
                        {
                            break;
                        }
                    }

                    m_htmlRaw.WriteLine("</ol>");
                }
            }

            FindSCC findSCC = new FindSCC();
            findSCC.Init(m_graph, m_htmlRaw, m_log);
            findSCC.FindCycles(m_interop.m_listCCWInfo);

            m_htmlRaw.WriteLine("</ul>");
        }

        protected override void WriteHtmlBody(TraceLog dataFile, TextWriter writer, string fileName, TextWriter log)
        {
            m_log = log;

            m_htmlRaw = writer;

            m_heapDumpFile.OpenDump(log);

            writer.WriteLine("<H2>CLR Interop Objects</H2><p>");

            writer.WriteLine("<b>RCW</b>: Runtime Callable Wrapper: wrapping COM objects to be called by managed runtime.<p>");
            writer.WriteLine("<b>CCW</b>: COM Callable Wrapper: wrapping managed objects to be called by COM.<p>");

            m_graph = m_heapDumpFile.m_gcDump.MemoryGraph;
            m_interop = m_heapDumpFile.m_gcDump.InteropInfo;

            if ((m_interop != null) && m_interop.InteropInfoExists())
            {
                writer.WriteLine("Interop data stream<p>");
                writer.WriteLine("<ul>");
                writer.WriteLine("<li>Heap dump file: {0}, {1:N0} nodes</li>", m_heapDumpFile.FilePath, (int)m_graph.NodeIndexLimit);
                writer.WriteLine("<li>CCW   : {0}</li>", m_interop.currentCCWCount);
                writer.WriteLine("<li>RCW   : {0}</li>", m_interop.currentRCWCount);
                writer.WriteLine("<li>Module: {0}</li>", m_interop.currentModuleCount);
                writer.WriteLine("</ul>");

                m_mainOutput = fileName;
                GenerateReports();
            }
            else
            {
                writer.WriteLine("<li>No Interop stream</li>");
            }
        }
    }
}
