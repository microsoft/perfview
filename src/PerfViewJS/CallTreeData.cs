// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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

        private readonly Dictionary<string, CallTreeNodeBase> nodeNameCache = new Dictionary<string, CallTreeNodeBase>();

        private readonly Dictionary<CallTreeNodeBase, TreeNode> callerTreeCache = new Dictionary<CallTreeNodeBase, TreeNode>();

        private readonly Dictionary<CallTreeNodeBase, TreeNode> calleeTreeCache = new Dictionary<CallTreeNodeBase, TreeNode>();

        private readonly object lockObj = new object();

        private readonly GenericStackSource stackSource;

        private readonly StackViewerModel model;

        private readonly SymbolReader symbolReader;

        private int initialized;

        private CallTree callTree;

        public CallTreeData(GenericStackSource stackSource, StackViewerModel model, SymbolReader symbolReader)
        {
            this.stackSource = stackSource;
            this.model = model;
            this.symbolReader = symbolReader;
        }

        public async ValueTask<TreeNode> GetNode(string name)
        {
            await this.EnsureInitialized();

            lock (this.lockObj)
            {
                if (this.nodeNameCache.ContainsKey(name))
                {
                    CallTreeDataEventSource.Log.NodeCacheHit(name);
                    return new TreeNode(this.nodeNameCache[name]);
                }
                else
                {
                    foreach (var node in this.callTree.ByID)
                    {
                        if (node.Name == name)
                        {
                            this.nodeNameCache.Add(name, node);
                            CallTreeDataEventSource.Log.NodeCacheMisss(name);
                            return new TreeNode(node);
                        }
                    }

                    CallTreeDataEventSource.Log.NodeCacheNotFound(name);
                    return null;
                }
            }
        }

        public async ValueTask<TreeNode> GetCallerTreeNode(string name, char sep, string path = "")
        {
            if (name == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(name));
            }

            var node = await this.GetNode(name);

            lock (this.lockObj)
            {
                CallTreeNodeBase backingNode = node.BackingNode;
                TreeNode callerTreeNode;

                if (this.callerTreeCache.ContainsKey(backingNode))
                {
                    callerTreeNode = this.callerTreeCache[backingNode];
                }
                else
                {
                    callerTreeNode = new TreeNode(AggregateCallTreeNode.CallerTree(backingNode));
                    this.callerTreeCache.Add(backingNode, callerTreeNode);
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

        public async ValueTask<TreeNode> GetCalleeTreeNode(string name, string path = "")
        {
            if (name == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(name));
            }

            var node = await this.GetNode(name);

            lock (this.lockObj)
            {
                CallTreeNodeBase backingNode = node.BackingNode;
                TreeNode calleeTreeNode;

                if (this.calleeTreeCache.ContainsKey(backingNode))
                {
                    calleeTreeNode = this.calleeTreeCache[backingNode];
                }
                else
                {
                    calleeTreeNode = new TreeNode(AggregateCallTreeNode.CalleeTree(backingNode));
                    this.calleeTreeCache.Add(backingNode, calleeTreeNode);
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

            var nodes = this.callTree.ByIDSortedExclusiveMetric().Take(numNodes);

            var summaryNodes = new List<TreeNode>();
            foreach (CallTreeNodeBase node in nodes)
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

        public bool LookupWarmSymbols(int minCount)
        {
            StackSourceStacks rawSource = this.stackSource.BaseStackSource;
            for (;;)
            {
                if (rawSource is TraceEventStackSource asTraceEventStackSource)
                {
                    asTraceEventStackSource.LookupWarmSymbols(minCount, this.symbolReader);
                    return true;
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

                return false;
            }
        }

        public async ValueTask<SourceInformation> Source(TreeNode node)
        {
            var index = this.GetSourceLocation(node.BackingNode, node.Name, out Dictionary<StackSourceFrameIndex, float> retVal);
            var generic = this.callTree.StackSource.BaseStackSource as GenericStackSource;

            var sourceLocation = await generic.GetSourceLocation(index);

            // TODO: needs cleanup
            if (sourceLocation != null)
            {
                var buildTimePath = sourceLocation.SourceFile.BuildTimeFilePath;
                var srcSrvString = sourceLocation.SourceFile.GetSourceFile();

                var lines = File.ReadAllLines(sourceLocation.SourceFile.GetSourceFile());

                var list = new List<LineInformation>();
                int i = 1;

                foreach (var line in lines)
                {
                    var li = new LineInformation
                    {
                        Line = line,
                        LineNumber = i++
                    };

                    list.Add(li);
                }

                var si = new SourceInformation
                {
                    BuildTimeFilePath = buildTimePath,
                    Lines = list,
                    Summary = new List<LineInformation> { new LineInformation { LineNumber = sourceLocation.LineNumber, Metric = retVal[index] } }
                };

                return si;
            }

            return null; // TODO: need to implement the local case i.e. this is the build machine
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
                    Name = "NoName"
                };

                var ss = new FilterStackSource(filterParams, this.stackSource, ScalingPolicyKind.TimeMetric);

                double startTimeRelativeMsec = double.TryParse(filterParams.StartTimeRelativeMSec, out startTimeRelativeMsec) ? Math.Max(startTimeRelativeMsec, 0.0) : 0.0;
                double endTimeRelativeMsec = double.TryParse(filterParams.EndTimeRelativeMSec, out endTimeRelativeMsec) ? Math.Min(endTimeRelativeMsec, this.stackSource.SampleTimeRelativeMSecLimit) : this.stackSource.SampleTimeRelativeMSecLimit;

                this.callTree = new CallTree(ScalingPolicyKind.TimeMetric);
                this.callTree.TimeHistogramController = new TimeHistogramController(this.callTree, startTimeRelativeMsec, endTimeRelativeMsec);
                this.callTree.StackSource = ss;

                if (float.TryParse(filterParams.MinInclusiveTimePercent, out float minIncusiveTimePercent) && minIncusiveTimePercent > 0)
                {
                    this.callTree.FoldNodesUnder(minIncusiveTimePercent * this.callTree.Root.InclusiveMetric / 100, true);
                }

                this.initialized = 1;
            }
            finally
            {
                this.semaphoreSlim.Release();
            }
        }

        private StackSourceFrameIndex GetSourceLocation(CallTreeNodeBase node, string cellText, out Dictionary<StackSourceFrameIndex, float> retVal)
        {
            var m = Regex.Match(cellText, "<<(.*!.*)>>");

            if (m.Success)
            {
                cellText = m.Groups[1].Value;
            }

            var frameIndexCounts = new Dictionary<StackSourceFrameIndex, float>();
            node.GetSamples(false, sampleIdx =>
            {
                var matchingFrameIndex = StackSourceFrameIndex.Invalid;
                var sample = this.callTree.StackSource.GetSampleByIndex(sampleIdx);
                var callStackIdx = sample.StackIndex;

                while (callStackIdx != StackSourceCallStackIndex.Invalid)
                {
                    var frameIndex = this.callTree.StackSource.GetFrameIndex(callStackIdx);
                    var frameName = this.callTree.StackSource.GetFrameName(frameIndex, false);

                    if (frameName == cellText)
                    {
                        matchingFrameIndex = frameIndex;
                    }

                    callStackIdx = this.callTree.StackSource.GetCallerIndex(callStackIdx);
                }

                if (matchingFrameIndex != StackSourceFrameIndex.Invalid)
                {
                    frameIndexCounts.TryGetValue(matchingFrameIndex, out float count);
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

            retVal = frameIndexCounts;

            return maxFrameIdx;
        }
    }
}
