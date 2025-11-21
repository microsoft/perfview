using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Stacks;
using Xunit;

namespace TraceEventTests
{
    public class RecursiveCallTest
    {
        [Fact]
        public void RecursiveCallsShowInCallTree()
        {
            // Create a simple trace log for testing
            var tracePath = Path.Combine(Path.GetTempPath(), "test_" + Guid.NewGuid() + ".etl");
            
            try
            {
                // Create a simple stack source for testing
                var source = new SimpleStackSource();
                
                // Create two frames with the same name "X"
                var frameX = source.Interner.FrameIntern("X");
                
                // Create a stack: ROOT -> X -> X (recursive call)
                var stack1 = source.Interner.CallStackIntern(frameX, StackSourceCallStackIndex.Invalid);
                var stack2 = source.Interner.CallStackIntern(frameX, stack1);
                
                // Add a sample with the recursive stack
                var sample = new StackSourceSample(source);
                sample.StackIndex = stack2;
                sample.TimeRelativeMSec = 1.0;
                sample.Metric = 1.0f;
                sample.Count = 1;
                source.AddSample(sample);
                
                source.DoneAddingSamples();
                
                // Build a CallTree from the source
                var callTree = new CallTree(ScalingPolicyKind.ScaleToData);
                callTree.StackSource = source;
                
                // The call tree should have:
                // ROOT
                //   X (first instance)
                //     X (recursive call)
                
                var root = callTree.Root;
                Assert.NotNull(root);
                Assert.Single(root.Callees); // Should have one child "X"
                
                var firstX = root.Callees.First();
                Assert.Equal("X", firstX.Name);
                
                // The critical test: the first X should have a child X (recursive call)
                Assert.Single(firstX.Callees); // This should pass if recursive calls are shown separately
                
                var secondX = firstX.Callees.First();
                Assert.Equal("X", secondX.Name);
                
                // Now test the CallerCalleeNode for "X"
                var callerCalleeNode = new CallerCalleeNode("X", callTree);
                
                // The CallerCalleeNode should show "X" as both a caller and callee
                Assert.NotNull(callerCalleeNode.Callers);
                Assert.NotNull(callerCalleeNode.Callees);
                
                // Should have ROOT as a caller
                Assert.Contains(callerCalleeNode.Callers, c => c.Name == "ROOT");
                
                // Should have X as a caller (recursive call from X to X)
                var xCaller = callerCalleeNode.Callers.FirstOrDefault(c => c.Name == "X");
                Assert.NotNull(xCaller);
                // The recursive caller should have non-zero metrics
                Assert.True(xCaller.InclusiveMetric > 0, $"Recursive caller 'X' has zero inclusive metric");
                Assert.False(float.IsNaN(xCaller.InclusiveMetric), "Recursive caller 'X' has NaN inclusive metric");
                
                // Should have X as a callee (recursive call from X to X)
                var xCallee = callerCalleeNode.Callees.FirstOrDefault(c => c.Name == "X");
                Assert.NotNull(xCallee);
                // The recursive callee should have non-zero metrics - THIS IS THE BUG
                Assert.True(xCallee.InclusiveMetric > 0, $"Recursive callee 'X' has zero inclusive metric (expected > 0, got {xCallee.InclusiveMetric})");
                Assert.False(float.IsNaN(xCallee.InclusiveMetric), "Recursive callee 'X' has NaN inclusive metric");
            }
            finally
            {
                if (File.Exists(tracePath))
                {
                    File.Delete(tracePath);
                }
            }
        }
        
        // Simple StackSource implementation for testing
        private class SimpleStackSource : StackSource
        {
            private StackSourceInterner m_interner;
            private List<StackSourceSample> m_samples = new List<StackSourceSample>();
            
            public SimpleStackSource()
            {
                m_interner = new StackSourceInterner(100, 100, 10);
            }
            
            public StackSourceInterner Interner => m_interner;
            
            public void AddSample(StackSourceSample sample)
            {
                m_samples.Add(new StackSourceSample(sample));
            }
            
            public void DoneAddingSamples()
            {
                m_interner.DoneInterning();
            }
            
            public override void ForEach(Action<StackSourceSample> callback)
            {
                foreach (var sample in m_samples)
                {
                    callback(sample);
                }
            }
            
            public override int CallStackIndexLimit => Math.Max(m_interner.CallStackCount, 10);
            public override int CallFrameIndexLimit => Math.Max(m_interner.FrameCount, 10);
            
            public override StackSourceFrameIndex GetFrameIndex(StackSourceCallStackIndex callStackIndex)
            {
                return m_interner.GetFrameIndex(callStackIndex);
            }
            
            public override StackSourceCallStackIndex GetCallerIndex(StackSourceCallStackIndex callStackIndex)
            {
                return m_interner.GetCallerIndex(callStackIndex);
            }
            
            public override string GetFrameName(StackSourceFrameIndex frameIndex, bool verboseName)
            {
                return m_interner.GetFrameName(frameIndex, verboseName);
            }
        }
    }
}
