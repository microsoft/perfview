namespace PerfDataService
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using Diagnostics.Tracing.StackSources;
    using Microsoft.Diagnostics.Tracing.Etlx;
    using Microsoft.Diagnostics.Tracing.Stacks;
    using Microsoft.Diagnostics.Symbols;
    using Models;

    public sealed class CallTreeDataProvider : ICallTreeDataProvider
    {
        private readonly CallTree callTree;

        private readonly SymbolReader reader;

        private readonly StackSource stacksource;

        private List<TreeNode> summary;

        private readonly Func<CallTreeNodeBase, bool> summaryPredicate;

        private readonly Dictionary<string, CallTreeNodeBase> nodeNameCache = new Dictionary<string, CallTreeNodeBase>();

        private readonly Dictionary<CallTreeNodeBase, TreeNode> callerTreeCache = new Dictionary<CallTreeNodeBase, TreeNode>();

        private readonly Dictionary<CallTreeNodeBase, TreeNode> calleeTreeCache = new Dictionary<CallTreeNodeBase, TreeNode>();

        private readonly object lockobj = new object();

        public CallTreeDataProvider(TraceLog log, FilterParams filterParams, SymbolReader reader, ITraceDataPlugin plugin)
        {
            if (log == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(log));
            }

            if (filterParams == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(filterParams));
            }

            if (reader == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(reader));
            }

            if (plugin == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(plugin));
            }

            this.reader = reader;
            TraceEvents events = log.Events;
            this.stacksource = plugin.GetStackSource(events);
            this.summaryPredicate = plugin.SummaryPredicate;
            CallTree.DisableParallelism = true; // important
            this.stacksource = new FilterStackSource(filterParams, this.stacksource, ScalingPolicyKind.TimeMetric);

            this.callTree = new CallTree(ScalingPolicyKind.TimeMetric) { StackSource = this.stacksource };
            float minIncusiveTimePercent;
            if (float.TryParse(filterParams.MinInclusiveTimePercent, out minIncusiveTimePercent) && minIncusiveTimePercent > 0)
            {
                this.callTree.FoldNodesUnder(minIncusiveTimePercent * this.callTree.Root.InclusiveMetric / 100, true);
            }
        }

        public TreeNode GetNode(string name)
        {
            lock (this.lockobj)
            {
                if (this.nodeNameCache.ContainsKey(name))
                {
                    CallTreeDataProviderEventSource.Log.NodeCacheHit(name);
                    return new TreeNode(this.nodeNameCache[name]);
                }
                else
                {
                    foreach (var node in this.callTree.ByID)
                    {
                        if (node.Name == name)
                        {
                            this.nodeNameCache.Add(name, node);
                            CallTreeDataProviderEventSource.Log.NodeCacheMisss(name);
                            return new TreeNode(node);
                        }
                    }

                    CallTreeDataProviderEventSource.Log.NodeCacheNotFound(name);
                    return null;
                }
            }
        }

        public TreeNode GetCallerTreeNode(string name, string path = "")
        {
            lock (this.lockobj)
            {
                CallTreeNodeBase node = this.GetNode(name).BackingNode;
                TreeNode callerTreeNode;

                if (this.callerTreeCache.ContainsKey(node))
                {
                    callerTreeNode = this.callerTreeCache[node];
                }
                else
                {
                    callerTreeNode = new TreeNode(AggregateCallTreeNode.CallerTree(node));
                    this.callerTreeCache.Add(node, callerTreeNode);
                }

                if (string.IsNullOrEmpty(path))
                {
                    return callerTreeNode;
                }

                var pathArr = path.Split('/');
                var pathNodeRoot = callerTreeNode.Children[int.Parse(pathArr[0])];

                for (int i = 1; i < pathArr.Length; ++i)
                {
                    pathNodeRoot = pathNodeRoot.Children[int.Parse(pathArr[i])];
                }

                return pathNodeRoot;
            }
        }

        public TreeNode GetCalleeTreeNode(string name, string path = "")
        {
            lock (this.lockobj)
            {
                CallTreeNodeBase node = this.GetNode(name).BackingNode;
                TreeNode calleeTreeNode;

                if (this.calleeTreeCache.ContainsKey(node))
                {
                    calleeTreeNode = this.calleeTreeCache[node];
                }
                else
                {
                    calleeTreeNode = new TreeNode(AggregateCallTreeNode.CalleeTree(node));
                    this.calleeTreeCache.Add(node, calleeTreeNode);
                }

                if (string.IsNullOrEmpty(path))
                {
                    return calleeTreeNode;
                }

                var pathArr = path.Split('/');
                var pathNodeRoot = calleeTreeNode.Children[int.Parse(pathArr[0])];

                for (int i = 1; i < pathArr.Length; ++i)
                {
                    pathNodeRoot = pathNodeRoot.Children[int.Parse(pathArr[i])];
                }

                return pathNodeRoot;
            }
        }

        public TreeNode[] GetCallerTree(string name)
        {
            return this.GetCallerTreeNode(name).Children;
        }

        /* 
         * Return all paths found from Depth First Search for 'target' on all trees in array 'nodes'
         * TODO: Perform search RECURSIVELY instead of iteratively 
         */
        private TreeNode[] searchForTarget(TreeNode[] nodes, string target)
        {
            target = target.ToLower();
            SortedSet<TreeNode> pathsFound = new SortedSet<TreeNode>(new SortNodeByContextId());  // TODO: Make this sorted list

            int flagCount = 0;
            foreach (TreeNode node in nodes)
            {
                searchHelper(node, pathsFound, target, ref flagCount);
            }
            
            return pathsFound.ToArray();
        }

        public class SortNodeByContextId : IComparer<TreeNode>
        {
            public int Compare(TreeNode node1, TreeNode node2)
            {
                return String.Compare(node1.ContextId, node2.ContextId);
            }
        }

        private void searchHelper(TreeNode node, SortedSet<TreeNode> pathsFound, string target, ref int flagCount)
        {
            if (node.Name.ToLower().Contains(target)) {
                node.FindFlag = flagCount.ToString();
                flagCount++;
            }

            // Breaks on 29
            if (node.HasChildren)
            {
                foreach (TreeNode child in node.Children)  // TODO: Maybe change node.children to GetCallers in order to retain correct ContextId
                {
                    searchHelper(child, pathsFound, target, ref flagCount);
                }
            }

            if (!string.IsNullOrEmpty(node.FindFlag))
            {
                TreeNode tempNode = node;
                while (tempNode != null)
                {
                    // Add current node plus all of its siblings
                    if (tempNode.ParentNode != null && tempNode.ParentNode.HasChildren)
                    {
                        foreach (TreeNode n in tempNode.ParentNode.Children)
                        {
                            if (!n.AddedToSearchSet)
                            {
                                pathsFound.Add(n);
                                n.AddedToSearchSet = true;
                            }
                        }
                    }
                    tempNode = tempNode.ParentNode;
                }
                
                if (node.ParentNode == null)
                {
                    // This a top level node; just add itself
                    if (!node.AddedToSearchSet)
                    {
                        pathsFound.Add(node);
                        node.AddedToSearchSet = true;
                    }
                }
            }
        }

        private string[] getContextNameAndPath(TreeNode node)
        {
            string[] nameAndPath = node.ContextId.Split(new[] { '/' }, 2);
            string name = nameAndPath.First();
            string path = nameAndPath.Length > 1 ? nameAndPath.Last() : "";

            return new string[]{ name, path };
        }

        public TreeNode[] GetCallerTree(string name, string path, string find)
        {
            TreeNode[] children = this.GetCallerTreeNode(name, path).Children;
            if (!string.IsNullOrEmpty(find))
            {
                children = searchForTarget(children, find);
            }
            return children;
        }

        public TreeNode[] GetCalleeTree(string name)
        {
            return this.GetCalleeTreeNode(name).Children;
        }

        public TreeNode[] GetCalleeTree(string name, string path, string find)
        {
            return this.GetCalleeTreeNode(name, path).Children;
        }

        public List<TreeNode> GetSummaryTree(int numNodes, string find)
        {
            lock (this.lockobj)
            {
                if (this.summary == null)
                {
                    IEnumerable<CallTreeNodeBase> nodes;
                    if (numNodes >= 0)
                    {
                        nodes = this.callTree.ByIDSortedExclusiveMetric().Where(this.summaryPredicate).Take(numNodes);
                    }
                    else
                    {
                        // numNodes < 0 --> Take ALL the nodes
                        numNodes = this.callTree.ByIDSortedExclusiveMetric().Where(this.summaryPredicate).Count();
                        nodes = this.callTree.ByIDSortedExclusiveMetric().Where(this.summaryPredicate).Take(numNodes);
                    }

                    this.summary = new List<TreeNode>();
                    int findCount = 0;
                    foreach (CallTreeNodeBase node in nodes)
                    {
                        TreeNode tn = new TreeNode(node);
                        if (!string.IsNullOrEmpty(find))
                        {
                            // TODO: Search each node name to see if it contains find as a substring
                            if (node.Name.ToLower().Contains(find.ToLower()))
                            {
                                tn.FindFlag = findCount.ToString();
                                findCount++;
                            }
                        }
                        this.summary.Add(tn);
                    }
                }

                return this.summary;
            }
        }

        public SourceInformation Source(TreeNode node)
        {
            SortedDictionary<int, float> metricOnLine;
            var sourceLocation = this.GetSourceLocation(node.BackingNode, node.BackingNode.Name, out metricOnLine);

            if (sourceLocation != null)
            {
                var sourceFile = sourceLocation.SourceFile;
                FieldInfo fi = typeof(SourceFile).GetField("m_symbolModule", BindingFlags.NonPublic | BindingFlags.Instance);
                if (fi != null)
                {
                    PropertyInfo pi = typeof(SymbolModule).GetProperty("PdbForSourceServer", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (pi != null)
                    {
                        MethodInfo mi = typeof(SymbolModule).GetMethod("GetSrcSrvStream", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (mi != null)
                        {
                            string srcStream = mi.Invoke(pi.GetValue(fi.GetValue(sourceFile)), null) as string;
                            if (srcStream != null)
                            {
                                string[] lines = srcStream.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                                foreach (var line in lines)
                                {
                                    // TFS DevDiv support
                                    if (line.StartsWith(sourceFile.BuildTimeFilePath))
                                    {
                                        var split = line.Split('*');

                                        var sourceInfo = new SourceInformation
                                        {
                                            Url = "http://vstfdevdiv:8080/DevDiv2/DevDiv/_versionControl/changeset/" + split[3].Trim() + "#path=" + split[2].Trim() + "&_a=contents",
                                            Type = "Url",
                                            Summary = metricOnLine.Select(m => new LineInformation { Metric = m.Value, LineNumber = m.Key + 1, Line = string.Empty }).ToList()
                                        };

                                        return sourceInfo;
                                    }
                                }

                                // support for source depot?
                                return null;
                            }
                        }
                    }
                }

                // Source Database Format
                {
                    var sdbFiles = Directory.GetFiles(this.reader.SourcePath, "*.sdb", SearchOption.AllDirectories);
                    SourceInformation sourceInfo = null;

                    foreach (var sdbFile in sdbFiles)
                    {
                        using (ZipArchive archive = ZipFile.OpenRead(sdbFile))
                        {
                            foreach (ZipArchiveEntry entry in archive.Entries)
                            {
                                if (sourceFile.BuildTimeFilePath.EndsWith(entry.FullName, StringComparison.OrdinalIgnoreCase))
                                {
                                    int i = 0;

                                    var summaryLineMetrics = new List<LineInformation>();
                                    var linemetrics = new List<LineInformation>();
                                    sourceInfo = new SourceInformation();
                                    sourceInfo.Lines = linemetrics;
                                    sourceInfo.Summary = summaryLineMetrics;
                                    sourceInfo.Type = "Lines";

                                    using (var sr = new StreamReader(entry.Open()))
                                    {
                                        string line;
                                        while ((line = sr.ReadLine()) != null)
                                        {
                                            i++;
                                            float value = 0;
                                            if (metricOnLine.TryGetValue(i, out value))
                                            {
                                                var lineInfo = new LineInformation { LineNumber = i, Metric = value, Line = line };
                                                summaryLineMetrics.Add(lineInfo);
                                                linemetrics.Add(lineInfo);
                                            }
                                            else
                                            {
                                                linemetrics.Add(new LineInformation { LineNumber = i, Metric = value, Line = line });
                                            }
                                        }
                                    }

                                    break;
                                }
                            }
                        }
                    }

                    return sourceInfo;
                }
            }

            return null;
        }

        private SourceLocation GetSourceLocation(CallTreeNodeBase asCallTreeNodeBase, string cellText, out SortedDictionary<int, float> metricOnLine)
        {
            metricOnLine = null;
            var m = Regex.Match(cellText, "<<(.*!.*)>>");

            if (m.Success)
            {
                cellText = m.Groups[1].Value;
            }

            var frameIndexCounts = new Dictionary<StackSourceFrameIndex, float>();
            asCallTreeNodeBase.GetSamples(false, delegate (StackSourceSampleIndex sampleIdx)
            {
                var matchingFrameIndex = StackSourceFrameIndex.Invalid;
                var sample = this.stacksource.GetSampleByIndex(sampleIdx);
                var callStackIdx = sample.StackIndex;
                while (callStackIdx != StackSourceCallStackIndex.Invalid)
                {
                    var frameIndex = this.stacksource.GetFrameIndex(callStackIdx);
                    var frameName = this.stacksource.GetFrameName(frameIndex, false);
                    if (frameName == cellText)
                        matchingFrameIndex = frameIndex;        // We keep overwriting it, so we get the entry closest to the root.  
                    callStackIdx = this.stacksource.GetCallerIndex(callStackIdx);
                }
                if (matchingFrameIndex != StackSourceFrameIndex.Invalid)
                {
                    float count = 0;
                    frameIndexCounts.TryGetValue(matchingFrameIndex, out count);
                    frameIndexCounts[matchingFrameIndex] = count + sample.Metric;
                }
                return true;
            });

            var maxFrameIdx = StackSourceFrameIndex.Invalid;
            float maxFrameIdxCount = -1;
            foreach (var keyValue in frameIndexCounts)
            {
                if (keyValue.Value >= maxFrameIdxCount)
                {
                    maxFrameIdxCount = keyValue.Value;
                    maxFrameIdx = keyValue.Key;
                }
            }

            if (maxFrameIdx == StackSourceFrameIndex.Invalid)
            {
                return null;
            }

            // Find the most primitive TraceEventStackSource
            TraceEventStackSource asTraceEventStackSource = GetTraceEventStackSource(this.stacksource);
            if (asTraceEventStackSource == null)
            {
                return null;
            }

            var frameToLine = new Dictionary<StackSourceFrameIndex, int>();

            var sourceLocation = asTraceEventStackSource.GetSourceLine(maxFrameIdx, this.reader);
            if (sourceLocation != null)
            {
                var filePathForMax = sourceLocation.SourceFile.BuildTimeFilePath;
                metricOnLine = new SortedDictionary<int, float>();

                // Accumulate the counts on a line basis
                foreach (StackSourceFrameIndex frameIdx in frameIndexCounts.Keys)
                {
                    var loc = asTraceEventStackSource.GetSourceLine(frameIdx, this.reader);
                    if (loc != null && loc.SourceFile.BuildTimeFilePath == filePathForMax)
                    {
                        frameToLine[frameIdx] = loc.LineNumber;
                        float metric;
                        metricOnLine.TryGetValue(loc.LineNumber, out metric);
                        metric += frameIndexCounts[frameIdx];
                        metricOnLine[loc.LineNumber] = metric;
                    }
                }
            }

            bool commonMethodIdxSet = false;
            MethodIndex commonMethodIdx = MethodIndex.Invalid;

            var nativeAddressFreq = new SortedDictionary<ulong, Tuple<int, float>>();
            foreach (var keyValue in frameIndexCounts)
            {
                var codeAddr = asTraceEventStackSource.GetFrameCodeAddress(keyValue.Key);
                if (codeAddr != CodeAddressIndex.Invalid)
                {
                    var methodIdx = asTraceEventStackSource.TraceLog.CodeAddresses.MethodIndex(codeAddr);
                    if (methodIdx != MethodIndex.Invalid)
                    {
                        if (!commonMethodIdxSet)
                            commonMethodIdx = methodIdx;            // First time, set it as the common method.  
                        else if (methodIdx != commonMethodIdx)
                            methodIdx = MethodIndex.Invalid;        // More than one method, give up.  
                        commonMethodIdxSet = true;
                    }

                    var nativeAddr = asTraceEventStackSource.TraceLog.CodeAddresses.Address(codeAddr);
                    var lineNum = 0;
                    frameToLine.TryGetValue(keyValue.Key, out lineNum);
                    nativeAddressFreq[nativeAddr] = new Tuple<int, float>(lineNum, keyValue.Value);
                }
            }

            foreach (var keyValue in nativeAddressFreq)
                Console.WriteLine("    {0,12:x} : {1,6} {2,10:f1}", keyValue.Key, keyValue.Value.Item1, keyValue.Value.Item2);

            if (sourceLocation == null)
            {
                return null;
            }

            foreach (var keyVal in metricOnLine)
                Console.WriteLine("    Line {0,5}:  Metric {1,5:n1}", keyVal.Key, keyVal.Value);

            return sourceLocation;
        }

        internal static TraceEventStackSource GetTraceEventStackSource(StackSource source)
        {
            StackSourceStacks rawSource = source;
            for (;;)
            {
                var asTraceEventStackSource = rawSource as TraceEventStackSource;
                if (asTraceEventStackSource != null)
                {
                    return asTraceEventStackSource;
                }

                var asCopyStackSource = rawSource as CopyStackSource;
                if (asCopyStackSource != null)
                {
                    rawSource = asCopyStackSource.SourceStacks;
                    continue;
                }

                var asStackSource = rawSource as StackSource;
                if (asStackSource != null && asStackSource != asStackSource.BaseStackSource)
                {
                    rawSource = asStackSource.BaseStackSource;
                    continue;
                }

                return null;
            }
        }
    }
}