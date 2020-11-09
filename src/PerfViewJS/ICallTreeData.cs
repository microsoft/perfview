// <copyright file="ICallTreeData.cs" company="Microsoft">
// Copyright (c) Microsoft. All rights reserved.
// </copyright>

namespace PerfViewJS
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Diagnostics.Tracing.Stacks;

    public interface ICallTreeData
    {
        // gets a node without a context
        ValueTask<TreeNode> GetNode(string name);

        // gets a node with a callee tree context and looks up the path
        ValueTask<TreeNode> GetCalleeTreeNode(string name, string path = "");

        // gets a list of nodes with no context
        ValueTask<List<TreeNode>> GetSummaryTree(int numNodes);

        // returns a flat caller tree given a node
        ValueTask<TreeNode[]> GetCallerTree(string name, char sep);

        // returns a flat caller tree given a node and its context
        ValueTask<TreeNode[]> GetCallerTree(string name, char sep, string path);

        // returns a flat callee tree given a node and its context
        ValueTask<TreeNode[]> GetCalleeTree(string name, string path);

        // returns samples for Drill Into
        ValueTask<StackSource> GetDrillIntoStackSource(bool exclusive, string name, char sep, string path);

        ValueTask<SourceInformation> Source(string authorizationHeader, string name, char sep, string path = "");

        void UnInitialize();

        string LookupWarmSymbols(int minCount);
    }
}
