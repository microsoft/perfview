using Microsoft.Diagnostics.Tracing.Stacks;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace TraceEventTests
{
    /// <summary>
    /// Tests for CallTree handling of recursive frames.
    /// This addresses the issue where frames with identical names but different positions 
    /// in the call stack were being merged into a single node.
    /// </summary>
    public class CallTreeRecursiveTests
    {
        /// <summary>
        /// Simple in-memory stack source for testing without requiring ETL files.
        /// </summary>
        private class TestStackSource : StackSource
        {
            private List<StackSourceSample> _samples = new List<StackSourceSample>();
            private StackSourceInterner _interner = new StackSourceInterner(100, 100, 100);
            
            public StackSourceInterner Interner => _interner;
            
            public void AddSample(StackSourceSample sample)
            {
                _samples.Add(new StackSourceSample(sample));
            }
            
            public void Done()
            {
                _interner.DoneInterning();
            }
            
            public override void ForEach(Action<StackSourceSample> callback)
            {
                foreach (var sample in _samples)
                {
                    callback(sample);
                }
            }
            
            public override int CallStackIndexLimit => _interner.CallStackCount + (int)StackSourceCallStackIndex.Start;
            public override int CallFrameIndexLimit => _interner.FrameCount + (int)StackSourceFrameIndex.Start;
            public override StackSourceCallStackIndex GetCallerIndex(StackSourceCallStackIndex callStackIndex) => _interner.GetCallerIndex(callStackIndex);
            public override StackSourceFrameIndex GetFrameIndex(StackSourceCallStackIndex callStackIndex) => _interner.GetFrameIndex(callStackIndex);
            public override string GetFrameName(StackSourceFrameIndex frameIndex, bool fullModulePath) => _interner.GetFrameName(frameIndex, fullModulePath);
            public override int SampleIndexLimit => _samples.Count;
        }
        
        [Fact]
        public void RecursiveFramesShowAsDistinctNodesInCallTree()
        {
            // Create a simple stack source with recursive X -> X
            var stackSource = new TestStackSource();
            
            var sample = new StackSourceSample(stackSource);
            sample.Metric = 1.0f;
            sample.TimeRelativeMSec = 1.0;
            
            // Build stack: ROOT -> X -> X
            var frameX = stackSource.Interner.FrameIntern("X");
            sample.StackIndex = stackSource.Interner.CallStackIntern(frameX, StackSourceCallStackIndex.Invalid);
            sample.StackIndex = stackSource.Interner.CallStackIntern(frameX, sample.StackIndex);
            
            stackSource.AddSample(sample);
            stackSource.Done();
            
            // Build a call tree
            var callTree = new CallTree(ScalingPolicyKind.ScaleToData);
            callTree.StackSource = stackSource;
            
            // Verify the tree structure: ROOT -> X -> X
            Assert.NotNull(callTree.Root);
            Assert.NotNull(callTree.Root.Callees);
            Assert.True(callTree.Root.Callees.Count > 0, "Root should have at least one callee");
            
            // Find the first X node
            CallTreeNode firstXNode = FindNodeByName(callTree.Root, "X");
            Assert.NotNull(firstXNode);
            
            // The first X should have the second X as a child
            Assert.NotNull(firstXNode.Callees);
            Assert.True(firstXNode.Callees.Count > 0, "First X node should have callees (the recursive X)");
            
            CallTreeNode secondXNode = firstXNode.Callees.FirstOrDefault(c => c.Name == "X");
            Assert.NotNull(secondXNode);
            
            // Verify that both nodes have metrics
            Assert.True(firstXNode.InclusiveMetric > 0, "First X node should have positive inclusive metric");
            Assert.True(secondXNode.ExclusiveMetric > 0, "Second X node should have positive exclusive metric (the leaf)");
            
            // Verify that they are distinct nodes (different object references)
            Assert.NotSame(firstXNode, secondXNode);
            
            // Verify the ByID view
            var byID = callTree.ByID.ToList();
            var xByID = byID.FirstOrDefault(n => n.Name == "X");
            Assert.NotNull(xByID);
            
            // The ByID node should have valid metrics (not NaN)
            Assert.False(float.IsNaN(xByID.InclusiveMetric), "ByID inclusive metric should not be NaN");
            Assert.False(float.IsNaN(xByID.ExclusiveMetric), "ByID exclusive metric should not be NaN");
            Assert.True(xByID.InclusiveMetric > 0, "ByID node should have positive inclusive metric");
        }
        
        [Fact]
        public void DeeplyNestedRecursiveFramesShowCorrectly()
        {
            var stackSource = new TestStackSource();
            
            var sample = new StackSourceSample(stackSource);
            sample.Metric = 1.0f;
            sample.TimeRelativeMSec = 1.0;
            
            // Build stack: ROOT -> X -> X -> X -> X
            var frameX = stackSource.Interner.FrameIntern("X");
            sample.StackIndex = StackSourceCallStackIndex.Invalid;
            for (int i = 0; i < 4; i++)
            {
                sample.StackIndex = stackSource.Interner.CallStackIntern(frameX, sample.StackIndex);
            }
            
            stackSource.AddSample(sample);
            stackSource.Done();
            
            var callTree = new CallTree(ScalingPolicyKind.ScaleToData);
            callTree.StackSource = stackSource;
            
            // Count the depth of X nodes
            int xDepth = CountXDepth(callTree.Root);
            Assert.Equal(4, xDepth); // Should have exactly 4 levels of X nodes
            
            // Verify metrics are not NaN
            var byID = callTree.ByID.ToList();
            var xByID = byID.FirstOrDefault(n => n.Name == "X");
            Assert.NotNull(xByID);
            Assert.False(float.IsNaN(xByID.InclusiveMetric), "ByID inclusive metric should not be NaN");
            Assert.True(xByID.InclusiveMetric > 0, "ByID node should have positive inclusive metric");
        }
        
        [Fact]
        public void NonAdjacentSameFramesAreGroupedInByID()
        {
            var stackSource = new TestStackSource();
            
            var sample = new StackSourceSample(stackSource);
            sample.Metric = 1.0f;
            sample.TimeRelativeMSec = 1.0;
            
            // Build stack: ROOT -> A -> B -> A
            var frameA = stackSource.Interner.FrameIntern("A");
            var frameB = stackSource.Interner.FrameIntern("B");
            
            sample.StackIndex = stackSource.Interner.CallStackIntern(frameA, StackSourceCallStackIndex.Invalid);
            sample.StackIndex = stackSource.Interner.CallStackIntern(frameB, sample.StackIndex);
            sample.StackIndex = stackSource.Interner.CallStackIntern(frameA, sample.StackIndex);
            
            stackSource.AddSample(sample);
            stackSource.Done();
            
            var callTree = new CallTree(ScalingPolicyKind.ScaleToData);
            callTree.StackSource = stackSource;
            
            // In the tree, we should have two distinct A nodes (at different levels)
            var firstA = FindNodeByName(callTree.Root, "A");
            Assert.NotNull(firstA);
            
            var bNode = FindNodeByName(firstA, "B");
            Assert.NotNull(bNode);
            
            var secondA = FindNodeByName(bNode, "A");
            Assert.NotNull(secondA);
            
            // They should be different objects
            Assert.NotSame(firstA, secondA);
            
            // But in ByID, they should be grouped into one entry
            var byID = callTree.ByID.ToList();
            var aByID = byID.Where(n => n.Name == "A").ToList();
            Assert.Single(aByID); // Only one "A" entry in ByID
            
            // And it should have valid metrics
            Assert.False(float.IsNaN(aByID[0].InclusiveMetric));
            Assert.True(aByID[0].InclusiveMetric > 0);
        }
        
        [Fact]
        public void MultipleRecursiveSamples()
        {
            var stackSource = new TestStackSource();
            
            // Add two samples with different recursive patterns
            var sample1 = new StackSourceSample(stackSource);
            sample1.Metric = 1.0f;
            sample1.TimeRelativeMSec = 1.0;
            sample1.StackIndex = stackSource.Interner.CallStackIntern(
                stackSource.Interner.FrameIntern("X"),
                StackSourceCallStackIndex.Invalid);
            sample1.StackIndex = stackSource.Interner.CallStackIntern(
                stackSource.Interner.FrameIntern("X"),
                sample1.StackIndex);
            stackSource.AddSample(sample1);
            
            var sample2 = new StackSourceSample(stackSource);
            sample2.Metric = 2.0f;
            sample2.TimeRelativeMSec = 2.0;
            sample2.StackIndex = stackSource.Interner.CallStackIntern(
                stackSource.Interner.FrameIntern("Y"),
                StackSourceCallStackIndex.Invalid);
            sample2.StackIndex = stackSource.Interner.CallStackIntern(
                stackSource.Interner.FrameIntern("Y"),
                sample2.StackIndex);
            sample2.StackIndex = stackSource.Interner.CallStackIntern(
                stackSource.Interner.FrameIntern("Y"),
                sample2.StackIndex);
            stackSource.AddSample(sample2);
            
            stackSource.Done();
            
            var callTree = new CallTree(ScalingPolicyKind.ScaleToData);
            callTree.StackSource = stackSource;
            
            // Check X has 2-deep recursion
            int xDepth = CountXDepth(callTree.Root);
            Assert.Equal(2, xDepth);
            
            // Check Y has 3-deep recursion
            int yDepth = CountNodeDepth(callTree.Root, "Y");
            Assert.Equal(3, yDepth);
            
            // Check ByID metrics
            var byID = callTree.ByID.ToList();
            var xByID = byID.FirstOrDefault(n => n.Name == "X");
            var yByID = byID.FirstOrDefault(n => n.Name == "Y");
            
            Assert.NotNull(xByID);
            Assert.NotNull(yByID);
            Assert.False(float.IsNaN(xByID.InclusiveMetric));
            Assert.False(float.IsNaN(yByID.InclusiveMetric));
            Assert.True(xByID.InclusiveMetric > 0);
            Assert.True(yByID.InclusiveMetric > 0);
        }
        
        private CallTreeNode FindNodeByName(CallTreeNode root, string name)
        {
            if (root.Name == name)
            {
                return root;
            }
            
            if (root.Callees != null)
            {
                foreach (var callee in root.Callees)
                {
                    var found = FindNodeByName(callee, name);
                    if (found != null)
                    {
                        return found;
                    }
                }
            }
            
            return null;
        }
        
        private int CountXDepth(CallTreeNode node)
        {
            return CountNodeDepth(node, "X");
        }
        
        private int CountNodeDepth(CallTreeNode node, string targetName)
        {
            if (node.Name == targetName)
            {
                int maxChildDepth = 0;
                if (node.Callees != null)
                {
                    foreach (var callee in node.Callees)
                    {
                        int childDepth = CountNodeDepth(callee, targetName);
                        if (childDepth > maxChildDepth)
                        {
                            maxChildDepth = childDepth;
                        }
                    }
                }
                return 1 + maxChildDepth;
            }
            else
            {
                int maxDepth = 0;
                if (node.Callees != null)
                {
                    foreach (var callee in node.Callees)
                    {
                        int depth = CountNodeDepth(callee, targetName);
                        if (depth > maxDepth)
                        {
                            maxDepth = depth;
                        }
                    }
                }
                return maxDepth;
            }
        }
    }
}
