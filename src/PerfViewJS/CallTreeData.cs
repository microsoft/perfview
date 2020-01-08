// <copyright file="CallTreeData.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace PerfViewJS
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using global::Diagnostics.Tracing.StackSources;
    using Microsoft.Diagnostics.Symbols;
    using Microsoft.Diagnostics.Tracing.Stacks;

    public sealed class CallTreeData : ICallTreeData
    {
        private readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

        private readonly object lockObj = new object();

        private readonly StackSource stackSource;

        private readonly StackViewerModel model;

        private int initialized;

        private Tuple tuple;

        public CallTreeData(StackSource stackSource, StackViewerModel model)
        {
            this.stackSource = stackSource;
            this.model = model;
        }

        public async ValueTask<TreeNode> GetNode(string name)
        {
            await this.EnsureInitialized();

            return this.GetNodeInner(name, this.tuple);
        }

        public async ValueTask<TreeNode> GetCalleeTreeNode(string name, string path = "")
        {
            if (name == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(name));
            }

            await this.EnsureInitialized();

            var t = this.tuple;

            var node = this.GetNodeInner(name, t);

            lock (this.lockObj)
            {
                CallTreeNodeBase backingNode = node.BackingNode;
                TreeNode calleeTreeNode;

                var c = t.CalleeTreeCache;

                if (c.ContainsKey(backingNode))
                {
                    calleeTreeNode = c[backingNode];
                }
                else
                {
                    calleeTreeNode = new TreeNode(AggregateCallTreeNode.CalleeTree(backingNode));
                    c.Add(backingNode, calleeTreeNode);
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

        public async ValueTask<TreeNode[]> GetCallerTree(string name, char sep)
        {
            var node = await this.GetCallerTreeNode(name, sep);
            return node.Children;
        }

        public async ValueTask<TreeNode[]> GetCallerTree(string name, char sep, string path)
        {
            var node = await this.GetCallerTreeNode(name, sep, path);
            return node.Children;
        }

        public async ValueTask<TreeNode[]> GetCalleeTree(string name)
        {
            var node = await this.GetCalleeTreeNode(name);
            return node.Children;
        }

        public async ValueTask<TreeNode[]> GetCalleeTree(string name, string path)
        {
            var node = await this.GetCalleeTreeNode(name, path);
            return node.Children;
        }

        public async ValueTask<List<TreeNode>> GetSummaryTree(int numNodes)
        {
            await this.EnsureInitialized();

            var nodes = this.tuple.CallTree.ByIDSortedExclusiveMetric().Take(numNodes);

            var summaryNodes = new List<TreeNode>();
            foreach (var node in nodes)
            {
                summaryNodes.Add(new TreeNode(node));
            }

            return summaryNodes;
        }

        public async ValueTask<StackSource> GetDrillIntoStackSource(bool exclusive, string name, char sep, string path = "")
        {
            var node = await this.GetCallerTreeNode(name, sep, path);
            var callTreeNode = node.BackingNode;

            var originalStackSource = callTreeNode.CallTree.StackSource;
            var drillIntoStackSource = new CopyStackSource(originalStackSource);

            callTreeNode.GetSamples(exclusive, index =>
            {
                drillIntoStackSource.AddSample(originalStackSource.GetSampleByIndex(index));
                return true;
            });

            return drillIntoStackSource;
        }

        public async ValueTask<SourceInformation> Source(string authorizationHeader, string name, char sep, string path = "")
        {
            var node = await this.GetCallerTreeNode(name, sep, path);

            var asCallTreeNodeBase = node.BackingNode;
            string cellText = node.Name;

            var m = Regex.Match(cellText, "<<(.*!.*)>>");

            if (m.Success)
            {
                cellText = m.Groups[1].Value;
            }

            var ss = this.tuple.CallTree.StackSource;

            var frameIndexCounts = new Dictionary<StackSourceFrameIndex, float>();
            asCallTreeNodeBase.GetSamples(false, sampleIdx =>
            {
                var matchingFrameIndex = StackSourceFrameIndex.Invalid;
                var sample = ss.GetSampleByIndex(sampleIdx);
                var callStackIdx = sample.StackIndex;

                while (callStackIdx != StackSourceCallStackIndex.Invalid)
                {
                    var frameIndex = ss.GetFrameIndex(callStackIdx);
                    var frameName = ss.GetFrameName(frameIndex, false);

                    if (frameName == cellText)
                    {
                        matchingFrameIndex = frameIndex;
                    }

                    callStackIdx = ss.GetCallerIndex(callStackIdx);
                }

                if (matchingFrameIndex != StackSourceFrameIndex.Invalid)
                {
                    frameIndexCounts.TryGetValue(matchingFrameIndex, out float count);
                    frameIndexCounts[matchingFrameIndex] = count + sample.Metric;
                }

                return true;
            });

            StackSourceFrameIndex maxFrameIdx = StackSourceFrameIndex.Invalid;
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
                // TODO: Error handling ("Could not find " + cellText + " in call stack!")
                return null;
            }

            var asTraceEventStackSource = GetTraceEventStackSource(ss);

            if (asTraceEventStackSource == null)
            {
                // TODO: Error handling ("Source does not support symbolic lookup.")
                return null;
            }

            var log = new StringWriter();
            using var reader = new SymbolReader(log) { AuthorizationHeaderForSourceLink = authorizationHeader };
            var sourceLocation = asTraceEventStackSource.GetSourceLine(maxFrameIdx, reader);

            if (sourceLocation == null)
            {
                // TODO: Error handling ("Source could not find a source location for the given Frame.")
                return null;
            }

            var sourceFile = sourceLocation.SourceFile;

            var filePathForMax = sourceFile.BuildTimeFilePath;
            var metricOnLine = new SortedDictionary<int, float>(Comparer<int>.Create((x, y) => y.CompareTo(x)));

            foreach (StackSourceFrameIndex frameIdx in frameIndexCounts.Keys)
            {
                var loc = asTraceEventStackSource.GetSourceLine(frameIdx, reader);
                if (loc != null && loc.SourceFile.BuildTimeFilePath == filePathForMax)
                {
                    metricOnLine.TryGetValue(loc.LineNumber, out var metric);
                    metric += frameIndexCounts[frameIdx];
                    metricOnLine[loc.LineNumber] = metric;
                }
            }

            var data = File.ReadAllText(sourceFile.GetSourceFile());

            var list = new LineInformation[metricOnLine.Count];

            int i = 0;
            foreach (var lineMetric in metricOnLine)
            {
                list[i++] = new LineInformation { LineNumber = lineMetric.Key, Metric = lineMetric.Value };
            }

            var si = new SourceInformation
            {
                Url = sourceFile.Url,
                Log = log.ToString(),
                BuildTimeFilePath = filePathForMax,
                Summary = list,
                Data = data,
            };

            return si;
        }

        public void UnInitialize()
        {
            this.initialized = 0;
        }

        public string LookupWarmSymbols(int minCount)
        {
            var traceEventStackSource = GetTraceEventStackSource(this.stackSource);
            if (traceEventStackSource != null)
            {
                var writer = new StringWriter();
                using (var symbolReader = new SymbolReader(writer))
                {
                    traceEventStackSource.LookupWarmSymbols(minCount, symbolReader);
                }

                this.UnInitialize();
                return writer.ToString();
            }

            return "Unable to find TraceEventStackSource. This a fatal error for symbol lookup";
        }

        private static TraceEventStackSource GetTraceEventStackSource(StackSource source)
        {
            StackSourceStacks rawSource = source;
            while (true)
            {
                if (rawSource is TraceEventStackSource asTraceEventStackSource)
                {
                    return asTraceEventStackSource;
                }

                if (rawSource is CopyStackSource asCopyStackSource)
                {
                    rawSource = asCopyStackSource.SourceStacks;
                    continue;
                }

                if (rawSource is StackSource asStackSource && asStackSource != asStackSource.BaseStackSource)
                {
                    rawSource = asStackSource.BaseStackSource;
                    continue;
                }

                return null;
            }
        }

        private async ValueTask<TreeNode> GetCallerTreeNode(string name, char sep, string path = "")
        {
            if (name == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(name));
            }

            await this.EnsureInitialized();

            var t = this.tuple;

            var node = this.GetNodeInner(name, t);

            lock (this.lockObj)
            {
                CallTreeNodeBase backingNode = node.BackingNode;
                TreeNode callerTreeNode;

                var c = t.CallerTreeCache;

                if (c.ContainsKey(backingNode))
                {
                    callerTreeNode = c[backingNode];
                }
                else
                {
                    callerTreeNode = new TreeNode(AggregateCallTreeNode.CallerTree(backingNode));
                    c.Add(backingNode, callerTreeNode);
                }

                if (string.IsNullOrEmpty(path))
                {
                    return callerTreeNode;
                }

                var pathArr = path.Split(sep);
                var pathNodeRoot = callerTreeNode.Children[int.Parse(pathArr[0])];

                for (int i = 1; i < pathArr.Length; ++i)
                {
                    pathNodeRoot = pathNodeRoot.Children[int.Parse(pathArr[i])];
                }

                return pathNodeRoot;
            }
        }

        private TreeNode GetNodeInner(string name, Tuple t)
        {
            lock (this.lockObj)
            {
                var n = t.NodeNameCache;

                if (n.ContainsKey(name))
                {
                    CallTreeDataEventSource.Log.NodeCacheHit(name);
                    return new TreeNode(n[name]);
                }
                else
                {
                    var c = t.CallTree;
                    foreach (var node in c.ByID)
                    {
                        if (node.Name == name)
                        {
                            n.Add(name, node);
                            CallTreeDataEventSource.Log.NodeCacheMisss(name);
                            return new TreeNode(node);
                        }
                    }

                    CallTreeDataEventSource.Log.NodeCacheNotFound(name);
                    return null;
                }
            }
        }

        private async Task EnsureInitialized()
        {
            if (Interlocked.CompareExchange(ref this.initialized, 1, comparand: -1) == 0)
            {
                await this.Initialize();
            }
        }

        private async Task Initialize()
        {
            await this.semaphoreSlim.WaitAsync();

            try
            {
                if (this.initialized == 1)
                {
                    return;
                }

                var filterParams = new FilterParams
                {
                    StartTimeRelativeMSec = this.model.Start,
                    EndTimeRelativeMSec = this.model.End,
                    ExcludeRegExs = this.model.ExcPats,
                    IncludeRegExs = this.model.IncPats,
                    FoldRegExs = this.model.FoldPats,
                    GroupRegExs = this.model.GroupPats,
                    MinInclusiveTimePercent = this.model.FoldPct,
                    Name = "NoName",
                };

                var ss = new FilterStackSource(filterParams, this.stackSource, ScalingPolicyKind.TimeMetric);

                double startTimeRelativeMsec = double.TryParse(filterParams.StartTimeRelativeMSec, out startTimeRelativeMsec) ? Math.Max(startTimeRelativeMsec, 0.0) : 0.0;
                double endTimeRelativeMsec = double.TryParse(filterParams.EndTimeRelativeMSec, out endTimeRelativeMsec) ? Math.Min(endTimeRelativeMsec, this.stackSource.SampleTimeRelativeMSecLimit) : this.stackSource.SampleTimeRelativeMSecLimit;

                var c = new CallTree(ScalingPolicyKind.TimeMetric);
                c.TimeHistogramController = new TimeHistogramController(c, startTimeRelativeMsec, endTimeRelativeMsec);
                c.StackSource = ss;

                if (float.TryParse(filterParams.MinInclusiveTimePercent, out float minIncusiveTimePercent) && minIncusiveTimePercent > 0)
                {
                    c.FoldNodesUnder(minIncusiveTimePercent * c.Root.InclusiveMetric / 100, true);
                }

                var t = new Tuple(c);
                this.tuple = t;

                this.initialized = 1;
            }
            finally
            {
                this.semaphoreSlim.Release();
            }
        }

        private sealed class Tuple
        {
            public Tuple(CallTree callTree)
            {
                this.CallTree = callTree;
            }

            public Dictionary<string, CallTreeNodeBase> NodeNameCache => new Dictionary<string, CallTreeNodeBase>();

            public Dictionary<CallTreeNodeBase, TreeNode> CallerTreeCache => new Dictionary<CallTreeNodeBase, TreeNode>();

            public Dictionary<CallTreeNodeBase, TreeNode> CalleeTreeCache => new Dictionary<CallTreeNodeBase, TreeNode>();

            public CallTree CallTree { get; }
        }
    }
}
