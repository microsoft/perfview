// Copyright (c) MiValueTask<crosoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace PerfViewJS
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Diagnostics.Tracing.Stacks;

    public interface ICallTreeData
    {
        // gets a node without a context
        ValueTask<TreeNode> GetNode(string name);

        // gets a node with a caller tree context and looks up the path
        ValueTask<TreeNode> GetCallerTreeNode(string name, char sep, string path = "");

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

        bool LookupWarmSymbols(int minCount);

        ValueTask<SourceInformation> Source(TreeNode node);
    }
}
