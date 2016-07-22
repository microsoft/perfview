namespace PerfDataService
{
    using System.Collections.Generic;
    using Models;

    public interface ICallTreeDataProvider
    {
        // gets a node without a context
        TreeNode GetNode(string name);

        // gets a node with a caller tree context and looks up the path
        TreeNode GetCallerTreeNode(string name, string path = "");

        // gets a node with a callee tree context and looks up the path
        TreeNode GetCalleeTreeNode(string name, string path = "");

        // gets a list of nodes with no context
        List<TreeNode> GetSummaryTree(int numNodes);

        // returns a flat caller tree given a node
        TreeNode[] GetCallerTree(string name);

        // returns a flat caller tree given a node and its context
        TreeNode[] GetCallerTree(string name, string path);

        // returns a flat callee tree given a node
        TreeNode[] GetCalleeTree(string name);

        // returns a flat callee tree given a node and its context
        TreeNode[] GetCalleeTree(string name, string path);

        SourceInformation Source(TreeNode node);
    }
}