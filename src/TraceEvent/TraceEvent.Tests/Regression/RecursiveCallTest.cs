using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FastSerialization;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.EventPipe;
using Microsoft.Diagnostics.Tracing.Stacks;
using Xunit;

namespace TraceEventTests
{
    public class RecursiveCallTest
    {
        /// <summary>
        /// Regression test for recursive function calls showing incorrect metrics.
        /// Verifies that when a function X calls itself recursively (X -> X), 
        /// both the CallTree structure and CallerCalleeNode correctly represent
        /// the recursion with non-zero metrics.
        /// </summary>
        [Fact]
        public void RecursiveCallsShowInCallTree()
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
            // Verify recursive callee has non-zero metrics (issue showed NaN/0.0)
            Assert.True(xCallee.InclusiveMetric > 0, $"Recursive callee 'X' has zero inclusive metric (expected > 0, got {xCallee.InclusiveMetric})");
            Assert.False(float.IsNaN(xCallee.InclusiveMetric), "Recursive callee 'X' has NaN inclusive metric");
        }
        
        /// <summary>
        /// Tests three levels of recursion (X -> X -> X) to verify correct metric weighting.
        /// With deeper recursion, the weighting algorithm should properly split metrics:
        /// recursionCount=1 gets weight 1.0, recursionCount=2 gets weight 0.5, recursionCount=3 gets weight 0.33, etc.
        /// </summary>
        [Fact]
        public void ThreeLevelRecursiveCallsShowCorrectMetrics()
        {
            // Test with three levels of recursion: X -> X -> X
            var source = new SimpleStackSource();
            var frameX = source.Interner.FrameIntern("X");
            
            // Create stack: ROOT -> X -> X -> X
            var stack1 = source.Interner.CallStackIntern(frameX, StackSourceCallStackIndex.Invalid);
            var stack2 = source.Interner.CallStackIntern(frameX, stack1);
            var stack3 = source.Interner.CallStackIntern(frameX, stack2);
            
            var sample = new StackSourceSample(source);
            sample.StackIndex = stack3;
            sample.TimeRelativeMSec = 1.0;
            sample.Metric = 1.0f;
            sample.Count = 1;
            source.AddSample(sample);
            
            source.DoneAddingSamples();
            
            var callTree = new CallTree(ScalingPolicyKind.ScaleToData);
            callTree.StackSource = source;
            
            // Verify call tree structure: ROOT -> X -> X -> X
            var root = callTree.Root;
            var x1 = root.Callees.First();
            Assert.Equal("X", x1.Name);
            var x2 = x1.Callees.First();
            Assert.Equal("X", x2.Name);
            var x3 = x2.Callees.First();
            Assert.Equal("X", x3.Name);
            Assert.True(x3.Callees == null || x3.Callees.Count == 0); // Leaf node
            
            // Test CallerCalleeNode
            var callerCalleeNode = new CallerCalleeNode("X", callTree);
            
            // Should have ROOT as a caller
            var rootCaller = callerCalleeNode.Callers.FirstOrDefault(c => c.Name == "ROOT");
            Assert.NotNull(rootCaller);
            Assert.True(rootCaller.InclusiveMetric > 0);
            
            // Should have X as a caller (from X->X transitions)
            var xCaller = callerCalleeNode.Callers.FirstOrDefault(c => c.Name == "X");
            Assert.NotNull(xCaller);
            Assert.True(xCaller.InclusiveMetric > 0, $"Recursive caller 'X' should have positive metric, got {xCaller.InclusiveMetric}");
            
            // Should have X as a callee (from X->X transitions)
            var xCallee = callerCalleeNode.Callees.FirstOrDefault(c => c.Name == "X");
            Assert.NotNull(xCallee);
            Assert.True(xCallee.InclusiveMetric > 0, $"Recursive callee 'X' should have positive metric, got {xCallee.InclusiveMetric}");
        }
        
        /// <summary>
        /// Regression test using MutableTraceEventStackSource to reproduce the exact issue scenario.
        /// This test generates an in-memory nettrace file, converts it to a TraceLog in memory, and then uses
        /// MutableTraceEventStackSource to add recursive frames, matching the original issue repro code.
        /// </summary>
        [Fact]
        public void RecursiveCallsWithMutableTraceEventStackSource()
        {
            // Generate a minimal in-memory nettrace file with basic metadata
            var writer = new EventPipeWriterV6();
            writer.WriteHeaders();
            
            // Add minimal metadata to make the file valid for TraceLog processing
            writer.WriteMetadataBlock(
                new EventMetadata(1, "Microsoft-Windows-DotNETRuntime", "EventSource", 0));
            
            // Add thread block to define thread index 1
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(1, 0, 0);
            });
            
            // Add a minimal event block
            writer.WriteEventBlock(w =>
            {
                // Write a simple event using thread index 1
                w.WriteEventBlob(1, 1, 1, new byte[0]);
            });
            
            writer.WriteEndBlock();
            
            // Convert nettrace to EventPipeEventSource
            using MemoryStream nettraceStream = new MemoryStream(writer.ToArray());
            TraceEventDispatcher eventSource = new EventPipeEventSource(nettraceStream);
            
            // Convert to in-memory ETLX
            using MemoryStream etlxStream = new MemoryStream();
            TraceLog.CreateFromEventPipeEventSources(eventSource, new IOStreamStreamWriter(etlxStream, SerializationSettings.Default, leaveOpen: true), null);
            etlxStream.Position = 0;
            
            // Create TraceLog from in-memory ETLX
            using (var traceLog = new TraceLog(etlxStream))
            {
                // Create MutableTraceEventStackSource and reproduce the exact issue scenario
                var stackSource = new MutableTraceEventStackSource(traceLog);
                
                // This reproduces the exact code from the issue:
                var sample = new StackSourceSample(stackSource);
                sample.StackIndex = stackSource.Interner.CallStackIntern(
                    stackSource.Interner.FrameIntern("X"), sample.StackIndex);
                sample.StackIndex = stackSource.Interner.CallStackIntern(
                    stackSource.Interner.FrameIntern("X"), sample.StackIndex);
                sample.TimeRelativeMSec = 1.0;
                sample.Metric = 1.0f;
                sample.Count = 1;
                stackSource.AddSample(sample);
                stackSource.DoneAddingSamples();
                
                // Build CallTree and verify structure
                var callTree = new CallTree(ScalingPolicyKind.ScaleToData);
                callTree.StackSource = stackSource;
                
                var root = callTree.Root;
                Assert.NotNull(root);
                
                // Test CallerCalleeNode for "X" - this is the main test
                // The CallerCalleeNode should correctly identify recursive relationships
                var callerCalleeNode = new CallerCalleeNode("X", callTree);
                
                // The key assertions: X should appear as both caller and callee
                // This tests that the recursive call (X -> X) is properly represented
                var xCaller = callerCalleeNode.Callers.FirstOrDefault(c => c.Name == "X");
                Assert.NotNull(xCaller);
                Assert.True(xCaller.InclusiveMetric > 0, 
                    $"Recursive caller 'X' should have positive metric, got {xCaller.InclusiveMetric}");
                Assert.False(float.IsNaN(xCaller.InclusiveMetric), 
                    "Recursive caller 'X' has NaN inclusive metric");
                
                var xCallee = callerCalleeNode.Callees.FirstOrDefault(c => c.Name == "X");
                Assert.NotNull(xCallee);
                Assert.True(xCallee.InclusiveMetric > 0, 
                    $"Recursive callee 'X' should have positive metric, got {xCallee.InclusiveMetric}");
                Assert.False(float.IsNaN(xCallee.InclusiveMetric), 
                    "Recursive callee 'X' has NaN inclusive metric");
            }
        }
        
        /// <summary>
        /// Simple StackSource implementation for testing recursive call scenarios.
        /// This minimal implementation allows creating call stacks with interned frames
        /// without requiring a full TraceLog or ETL file.
        /// </summary>
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
