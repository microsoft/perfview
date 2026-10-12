// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Diagnostics.Tracing.Etlx;
using Xunit;

namespace TraceEventTests.Stacks
{
    public class CallStackInterningTests
    {
        /// <summary>Verifies distinct thread roots and callers retain separate frame identities.</summary>
        [Fact]
        public void FramesAreInternedByAddressAndCaller()
        {
            var stacks = new TraceCallStacks(null, null);
            CallStackIndex root0 = TraceCallStacks.GetRootForThread((ThreadIndex)0);
            CallStackIndex root7 = TraceCallStacks.GetRootForThread((ThreadIndex)7);
            CallStackIndex first = stacks.InternCallStackIndex((CodeAddressIndex)10, root0);
            Assert.Equal(first, stacks.InternCallStackIndex((CodeAddressIndex)10, root0));
            CallStackIndex otherRoot = stacks.InternCallStackIndex((CodeAddressIndex)10, root7);
            Assert.NotEqual(first, otherRoot);
            CallStackIndex child = stacks.InternCallStackIndex((CodeAddressIndex)20, first);
            Assert.Equal(child, stacks.InternCallStackIndex((CodeAddressIndex)20, first));
            Assert.NotEqual(child, stacks.InternCallStackIndex((CodeAddressIndex)20, otherRoot));
            Assert.Equal(first, stacks.Caller(child));
            Assert.Equal(CallStackIndex.Invalid, stacks.Caller(first));
            Assert.Equal(2, stacks.Depth(child));
            Assert.Equal((CodeAddressIndex)20, stacks.CodeAddressIndex(child));
        }

        /// <summary>Verifies head, middle, and tail hits across a large sibling set do not create duplicates.</summary>
        [Fact]
        public void HighFanoutPreservesAllSiblingLookups()
        {
            var stacks = new TraceCallStacks(null, null);
            CallStackIndex root = TraceCallStacks.GetRootForThread((ThreadIndex)0);
            CallStackIndex caller = stacks.InternCallStackIndex((CodeAddressIndex)0, root);
            var expected = new CallStackIndex[2048];
            for (int i = 0; i < expected.Length; i++)
            {
                expected[i] = stacks.InternCallStackIndex((CodeAddressIndex)(i + 1), caller);
            }
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i], stacks.InternCallStackIndex((CodeAddressIndex)(i + 1), caller));
            }
            Assert.Equal(expected.Length + 1, stacks.Count);
        }

        /// <summary>Verifies live-session clearing resets every compact lookup map before stacks are interned again.</summary>
        [Fact]
        public void ClearingRestoresEmptyIndexesForReuse()
        {
            var stacks = new TraceCallStacks(null, null);
            CallStackIndex root0 = TraceCallStacks.GetRootForThread((ThreadIndex)0);
            CallStackIndex root17 = TraceCallStacks.GetRootForThread((ThreadIndex)17);
            for (int cycle = 0; cycle < 3; cycle++)
            {
                CallStackIndex first = stacks.InternCallStackIndex((CodeAddressIndex)10, root0);
                Assert.Equal((CallStackIndex)0, first);
                CallStackIndex otherRoot = stacks.InternCallStackIndex((CodeAddressIndex)10, root17);
                CallStackIndex child = stacks.InternCallStackIndex((CodeAddressIndex)20, first);
                stacks.InternCallStackIndex((CodeAddressIndex)30, first);
                Assert.Equal(first, stacks.InternCallStackIndex((CodeAddressIndex)10, root0));
                Assert.Equal(otherRoot, stacks.InternCallStackIndex((CodeAddressIndex)10, root17));
                Assert.Equal(child, stacks.InternCallStackIndex((CodeAddressIndex)20, first));
                Assert.Equal(4, stacks.Count);

                stacks.Clear();
                Assert.Equal(0, stacks.Count);
            }
        }

        /// <summary>Verifies array growth and interleaved branches produce the same IDs as an independent identity map.</summary>
        [Fact]
        public void GrowthAndInterleavingMatchIndependentIdentityMap()
        {
            var stacks = new TraceCallStacks(null, null);
            var identities = new Dictionary<Tuple<int, int>, int>();
            var callers = new List<int>();
            for (int i = 0; i < 16; i++)
            {
                callers.Add((int)TraceCallStacks.GetRootForThread((ThreadIndex)(i * 3)));
            }
            var random = new Random(12345);
            for (int i = 0; i < 60000; i++)
            {
                int caller = callers[random.Next(callers.Count)];
                int address = random.Next(128);
                var key = Tuple.Create(caller, address);
                int expected;
                if (!identities.TryGetValue(key, out expected))
                {
                    expected = identities.Count;
                    identities.Add(key, expected);
                    callers.Add(expected);
                }
                CallStackIndex actual = stacks.InternCallStackIndex((CodeAddressIndex)address, (CallStackIndex)caller);
                Assert.Equal(expected, (int)actual);
                Assert.Equal((CodeAddressIndex)address, stacks.CodeAddressIndex(actual));
                Assert.Equal(caller < 0 ? CallStackIndex.Invalid : (CallStackIndex)caller, stacks.Caller(actual));
            }
            Assert.Equal(identities.Count, stacks.Count);
            Assert.True(stacks.Count > 10000);
        }

#if NET8_0_OR_GREATER
        /// <summary>Verifies a single-child chain uses compact arrays rather than allocating a list and array per caller.</summary>
        [Fact]
        public void SingleChildChainsAvoidPerCallerObjects()
        {
            var warm = new TraceCallStacks(null, null);
            warm.InternCallStackIndex((CodeAddressIndex)0, TraceCallStacks.GetRootForThread((ThreadIndex)0));
            long before = GC.GetAllocatedBytesForCurrentThread();
            var stacks = new TraceCallStacks(null, null);
            CallStackIndex caller = TraceCallStacks.GetRootForThread((ThreadIndex)0);
            for (int i = 0; i < 10000; i++)
            {
                caller = stacks.InternCallStackIndex((CodeAddressIndex)i, caller);
            }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(10000, stacks.Count);
            Assert.Equal(10000, stacks.Depth(caller));
            Assert.True(allocated < 200000, $"Expected compact arrays; allocated {allocated} bytes for 10,000 frames.");
        }
#endif
    }
}
