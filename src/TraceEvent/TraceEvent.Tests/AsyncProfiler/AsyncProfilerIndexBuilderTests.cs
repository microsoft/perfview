// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FastSerialization;

using Microsoft.Diagnostics.Tracing.Computers;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers.AsyncProfiler;

using Xunit;

namespace TraceEventTests
{
    /// <summary>
    /// Fluent convenience wrappers over <see cref="AsyncProfilerBufferBuilder"/> for state-machine (V1)
    /// and runtime-async (V2) events used by the computer tests.
    /// </summary>
    internal static class AsyncProfilerIndexBuilderBuilderExtensions
    {
        public static AsyncProfilerBufferBuilder Reset(this AsyncProfilerBufferBuilder b, long ts) =>
            b.ContextNoPayload(AsyncEventID.ResetAsyncThreadContext, ts);

        /// <summary>
        /// Emits metadata followed by a reset at the same QPC, arming the thread.
        /// </summary>
        public static AsyncProfilerBufferBuilder Armed(this AsyncProfilerBufferBuilder b, long ts) =>
            b.Metadata(ts, qpcFrequency: 10_000_000, qpcSync: 1, utcSync: 1,
                eventBufferSize: 0, wrapperCount: 32, new AsyncManifestEntry[0])
             .Reset(ts);

        public static AsyncProfilerBufferBuilder ResumeStack(
            this AsyncProfilerBufferBuilder b, long ts, ulong dispatcher,
            ulong[] frames, int[] states = null, byte continuationIndex = 0) =>
            b.Callstack(AsyncEventID.ResumeStateMachineAsyncCallstack, ts, continuationIndex, 0, dispatcher, frames, states ?? new int[frames.Length]);

        public static AsyncProfilerBufferBuilder AppendStack(
            this AsyncProfilerBufferBuilder b, long ts, ulong dispatcher,
            ulong[] frames, int[] states = null, byte continuationIndex = 0) =>
            b.Callstack(AsyncEventID.AppendStateMachineAsyncCallstack, ts, continuationIndex, 0, dispatcher, frames, states ?? new int[frames.Length]);

        public static AsyncProfilerBufferBuilder Suspend(this AsyncProfilerBufferBuilder b, long ts) =>
            b.ContextNoPayload(AsyncEventID.SuspendStateMachineAsyncContext, ts);

        public static AsyncProfilerBufferBuilder Complete(this AsyncProfilerBufferBuilder b, long ts) =>
            b.ContextNoPayload(AsyncEventID.CompleteStateMachineAsyncContext, ts);

        public static AsyncProfilerBufferBuilder CompleteMethod(this AsyncProfilerBufferBuilder b, long ts) =>
            b.Method(AsyncEventID.CompleteStateMachineAsyncMethod, ts);

        public static AsyncProfilerBufferBuilder UnwindException(this AsyncProfilerBufferBuilder b, long ts, uint frames) =>
            b.Unwind(AsyncEventID.UnwindStateMachineAsyncException, ts, frames);

        public static AsyncProfilerBufferBuilder WrapperReset(this AsyncProfilerBufferBuilder b, long ts) =>
            b.ContextNoPayload(AsyncEventID.ResetAsyncContinuationWrapperIndex, ts);

        public static AsyncProfilerBufferBuilder ResumeRuntimeStack(
            this AsyncProfilerBufferBuilder b, long ts, ulong dispatcher,
            ulong[] frames, byte continuationIndex = 0) =>
            b.Callstack(AsyncEventID.ResumeRuntimeAsyncCallstack, ts, continuationIndex, 0, dispatcher, frames, null);

        public static AsyncProfilerBufferBuilder SuspendRuntime(this AsyncProfilerBufferBuilder b, long ts) =>
            b.ContextNoPayload(AsyncEventID.SuspendRuntimeAsyncContext, ts);

        public static AsyncProfilerBufferBuilder CompleteRuntimeMethod(this AsyncProfilerBufferBuilder b, long ts) =>
            b.Method(AsyncEventID.CompleteRuntimeAsyncMethod, ts);

        public static AsyncProfilerBufferBuilder UnwindRuntimeException(this AsyncProfilerBufferBuilder b, long ts, uint frames) =>
            b.Unwind(AsyncEventID.UnwindRuntimeAsyncException, ts, frames);
    }

    /// <summary>
    /// Unit tests for <see cref="AsyncProfilerIndexBuilder"/>. Each test synthesizes an <c>AsyncEvents</c> buffer
    /// (via <see cref="AsyncProfilerBufferBuilder"/>) and drives it through the computer's decoder, then
    /// asserts the per-thread active-async-callstack index.
    /// </summary>
    public class AsyncProfilerIndexBuilderTests
    {
        [Theory]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, (int)AsyncEventID.ResumeRuntimeAsyncCallstack)]
        [InlineData((int)AsyncCallstackKind.RuntimeAsync, (int)AsyncEventID.UnwindRuntimeAsyncException)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, (int)AsyncEventID.ResumeStateMachineAsyncCallstack)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, (int)AsyncEventID.AppendStateMachineAsyncCallstack)]
        [InlineData((int)AsyncCallstackKind.StateMachineAsync, (int)AsyncEventID.UnwindStateMachineAsyncException)]
        public void MalformedPayload_PreservesNestedPrefixAndRecoversOnlyAtReset(
            int kindValue, int malformedEventValue)
        {
            var kind = (AsyncCallstackKind)kindValue;
            var malformedEvent = (AsyncEventID)malformedEventValue;
            bool v1 = kind == AsyncCallstackKind.StateMachineAsync;
            AsyncEventID resume = v1 ? AsyncEventID.ResumeStateMachineAsyncCallstack : AsyncEventID.ResumeRuntimeAsyncCallstack;
            AsyncEventID suspend = v1 ? AsyncEventID.SuspendStateMachineAsyncContext : AsyncEventID.SuspendRuntimeAsyncContext;
            AsyncEventID completeMethod = v1 ? AsyncEventID.CompleteStateMachineAsyncMethod : AsyncEventID.CompleteRuntimeAsyncMethod;
            int[] states = v1 ? new[] { 1 } : null;
            var computer = new AsyncProfilerIndexBuilder();
            var errors = new List<ParseError>();
            computer.ParseError += (process, error) =>
            {
                Assert.Equal(ProcessA, process);
                errors.Add(error);
            };
            Process(computer, ProcessA, new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .Callstack(resume, Start + 10, 0, 0, 1, new ulong[] { 0x100 }, states)
                .Callstack(resume, Start + 12, 0, 0, 2, new ulong[] { 0x200 }, states)
                .RawEvent((byte)malformedEvent, Start + 15, AsyncEventInfo.GetPayloadLengthFieldSize(malformedEvent), Array.Empty<byte>())
                .Method(completeMethod, Start + 16)
                .Callstack(resume, Start + 17, 0, 0, 99, new ulong[] { 0xDEAD }, states)
                .ContextNoPayload(suspend, Start + 18)
                .Reset(Start + 20)
                .Callstack(resume, Start + 22, 0, 0, 3, new ulong[] { 0xBEEF }, states)
                .ContextNoPayload(suspend, Start + 25));
            computer.Finish();

            Assert.Equal(1, computer.ParseErrorCount);
            ParseError error = Assert.Single(errors);
            Assert.Equal(ThreadA, error.BufferHeader.Value.OsThreadId);
            Assert.Equal(Start + 15, error.TimestampQpc);
            Assert.Equal(malformedEvent, error.EventId);
            Assert.True(error.ByteOffset >= BufferHeader.Size);
            IReadOnlyList<AsyncProfilerCallStack> prefix = computer.GetCallStacks(Key(ProcessA, ThreadA), Start + 14);
            Assert.Equal(2, prefix.Count);
            Assert.All(prefix, stack => Assert.Equal(Start + 15, stack.EndQpc));
            Assert.Empty(computer.GetCallStacks(Key(ProcessA, ThreadA), Start + 15));
            Assert.Empty(computer.GetCallStacks(Key(ProcessA, ThreadA), Start + 19));
            AsyncProfilerCallStack recovered = Assert.Single(computer.GetCallStacks(Key(ProcessA, ThreadA), Start + 23));
            Assert.Equal(0, recovered.Depth);
            Assert.Equal(0xBEEFUL, recovered.Frames.InstructionPointerAt(0));
            Assert.Equal(kind, recovered.Frames.Kind);
            Assert.Empty(computer.GetCallStacks(Key(ProcessA, ThreadA), Start + 26));
        }

        [Fact]
        public void MalformedPayload_FinishDoesNotExtendDamagedStateOrInvalidateOtherThreads()
        {
            var computer = new AsyncProfilerIndexBuilder();
            Process(computer, ProcessA, new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start).ResumeStack(Start + 10, 1, new ulong[] { 0xA }));
            Process(computer, ProcessA, new AsyncProfilerBufferBuilder(ThreadB)
                .Reset(Start).ResumeStack(Start + 10, 2, new ulong[] { 0xB }));
            Process(computer, ProcessB, new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start).ResumeStack(Start + 10, 3, new ulong[] { 0xC }));
            Process(computer, ProcessA, new AsyncProfilerBufferBuilder(ThreadA, startQpc: Start + 15)
                .RawEvent((byte)AsyncEventID.UnwindStateMachineAsyncException, Start + 15, PayloadLengthFieldSize.Byte, Array.Empty<byte>())
                .ResumeStack(Start + 20, 4, new ulong[] { 0xDEAD }));
            computer.Finish();

            Assert.Equal(Start + 15, Assert.Single(computer.GetCallStacks(Key(ProcessA, ThreadA), Start + 14)).EndQpc);
            Assert.Empty(computer.GetCallStacks(Key(ProcessA, ThreadA), Start + 15));
            Assert.Empty(computer.GetCallStacks(Key(ProcessA, ThreadA), Start + 100));
            Assert.Equal(long.MaxValue, Assert.Single(computer.GetCallStacks(Key(ProcessA, ThreadB), Start + 100)).EndQpc);
            Assert.Equal(long.MaxValue, Assert.Single(computer.GetCallStacks(Key(ProcessB, ThreadA), Start + 100)).EndQpc);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void UnreadableHeader_DiscardsProcessOpenStateAndRequiresFreshMetadata(bool unsupportedVersion)
        {
            var computer = new AsyncProfilerIndexBuilder();
            Process(computer, ProcessA, new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .ResumeStack(Start + 10, 1, new ulong[] { 0xA }).Suspend(Start + 12)
                .ResumeStack(Start + 14, 2, new ulong[] { 0xB }));
            Process(computer, ProcessA, new AsyncProfilerBufferBuilder(ThreadB)
                .Reset(Start).ResumeStack(Start + 10, 3, new ulong[] { 0xC }));
            Process(computer, ProcessB, new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start).ResumeStack(Start + 10, 4, new ulong[] { 0xD }));
            byte[] damaged = unsupportedVersion ? new AsyncProfilerBufferBuilder(ThreadA).Build() : new byte[8];
            damaged[0] = 2;
            var errors = new List<ParseError>();
            computer.ParseError += (process, error) =>
            {
                Assert.Equal(ProcessA, process);
                errors.Add(error);
            };
            computer.Process(damaged, ProcessA);
            Process(computer, ProcessA, new AsyncProfilerBufferBuilder(ThreadA)
                .Reset(Start + 20).ResumeStack(Start + 21, 5, new ulong[] { 0xDEAD }).Suspend(Start + 22));
            Assert.False(computer.ClockKnownForProcess(ProcessA));
            Assert.True(computer.ClockKnownForProcess(ProcessB));
            Process(computer, ProcessA, new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start + 30).ResumeStack(Start + 31, 6, new ulong[] { 0xBEEF }).Suspend(Start + 35));
            computer.Finish();

            Assert.Equal(1, computer.ParseErrorCount);
            ParseError diagnostic = Assert.Single(errors);
            Assert.Null(diagnostic.BufferHeader);
            Assert.Null(diagnostic.TimestampQpc);
            Assert.Equal(0xAUL, Assert.Single(computer.GetCallStacks(Key(ProcessA, ThreadA), Start + 11)).Frames.InstructionPointerAt(0));
            Assert.Empty(computer.GetCallStacks(Key(ProcessA, ThreadA), Start + 15));
            Assert.Empty(computer.GetCallStacks(Key(ProcessA, ThreadA), Start + 21));
            Assert.Empty(computer.GetCallStacks(Key(ProcessA, ThreadB), Start + 100));
            Assert.Equal(0xBEEFUL, Assert.Single(computer.GetCallStacks(Key(ProcessA, ThreadA), Start + 32)).Frames.InstructionPointerAt(0));
            Assert.Equal(0xDUL, Assert.Single(computer.GetCallStacks(Key(ProcessB, ThreadA), Start + 100)).Frames.InstructionPointerAt(0));
        }

        [Fact]
        public void MalformedMetadata_ClosesProcessPrefixesAndRequiresMetadataPlusPerThreadResets()
        {
            var computer = new AsyncProfilerIndexBuilder();
            Process(computer, ProcessA, new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start).ResumeStack(Start + 10, 1, new ulong[] { 0xA }));
            Process(computer, ProcessA, new AsyncProfilerBufferBuilder(ThreadB)
                .Reset(Start).ResumeStack(Start + 40, 2, new ulong[] { 0xB }));
            Process(computer, ProcessB, new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start).ResumeStack(Start + 10, 3, new ulong[] { 0xC }));
            Process(computer, ProcessA, new AsyncProfilerBufferBuilder(ThreadA)
                .RawEvent((byte)AsyncEventID.AsyncProfilerMetadata, Start + 20, PayloadLengthFieldSize.UShort, Array.Empty<byte>())
                .Reset(Start + 21).ResumeStack(Start + 22, 4, new ulong[] { 0xDEAD }));
            Assert.False(computer.ClockKnownForProcess(ProcessA));
            Process(computer, ProcessA, new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start + 30).ResumeStack(Start + 31, 5, new ulong[] { 0xBEEF }).Suspend(Start + 35));
            Process(computer, ProcessA, new AsyncProfilerBufferBuilder(ThreadB)
                .ResumeStack(Start + 41, 6, new ulong[] { 0xDEAD })
                .Reset(Start + 45).ResumeStack(Start + 46, 7, new ulong[] { 0xCAFE }).Suspend(Start + 50));
            computer.Finish();

            Assert.Equal(1, computer.ParseErrorCount);
            Assert.Equal(Start + 20, Assert.Single(computer.GetCallStacks(Key(ProcessA, ThreadA), Start + 15)).EndQpc);
            Assert.Empty(computer.GetCallStacks(Key(ProcessA, ThreadA), Start + 20));
            Assert.Empty(computer.GetCallStacks(Key(ProcessA, ThreadA), Start + 22));
            Assert.Empty(computer.GetCallStacks(Key(ProcessA, ThreadB), Start + 42));
            Assert.Equal(0xBEEFUL, Assert.Single(computer.GetCallStacks(Key(ProcessA, ThreadA), Start + 32)).Frames.InstructionPointerAt(0));
            Assert.Equal(0xCAFEUL, Assert.Single(computer.GetCallStacks(Key(ProcessA, ThreadB), Start + 47)).Frames.InstructionPointerAt(0));
            Assert.Equal(0xCUL, Assert.Single(computer.GetCallStacks(Key(ProcessB, ThreadA), Start + 100)).Frames.InstructionPointerAt(0));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void FramingFailure_ClosesAtBufferStartWhenNoEventTimestampIsAvailable(bool truncatedBuffer)
        {
            var computer = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start).ResumeStack(Start + 10, 1, new ulong[] { 0xA }));
            byte[] damaged = new AsyncProfilerBufferBuilder(ThreadA, startQpc: Start + 15)
                .Complete(Start + 20).Build();
            if (truncatedBuffer)
            {
                Array.Resize(ref damaged, damaged.Length - 1);
            }
            else
            {
                damaged[BufferHeader.Size + 1] = 0x80;
            }
            computer.Process(damaged);
            computer.Finish();

            Assert.Equal(1, computer.ParseErrorCount);
            Assert.Equal(Start + 15, Assert.Single(computer.GetCallStacks(Key(ThreadA), Start + 14)).EndQpc);
            Assert.Empty(computer.GetCallStacks(Key(ThreadA), Start + 15));
        }

        [Theory]
        [InlineData(4, 12)]
        [InlineData(6, 15)]
        public void EventCountMismatch_ClosesAtLastDecodedTimestamp(uint eventCount, int endOffset)
        {
            byte[] damaged = new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start).ResumeStack(Start + 10, 1, new ulong[] { 0xA })
                .WrapperReset(Start + 12).CompleteMethod(Start + 15).Build();
            Array.Copy(BitConverter.GetBytes(eventCount), 0, damaged, 17, sizeof(uint));
            var computer = new AsyncProfilerIndexBuilder();
            computer.Process(damaged);
            computer.Finish();

            Assert.Equal(1, computer.ParseErrorCount);
            Assert.Equal(Start + endOffset, Assert.Single(computer.GetCallStacks(Key(ThreadA), Start + 11)).EndQpc);
            Assert.Empty(computer.GetCallStacks(Key(ThreadA), Start + endOffset));
        }

        [Fact]
        public void EventsBeforeReset_AreIgnored()
        {
            var computer = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Metadata(Start, qpcFrequency: 10_000_000, qpcSync: 1, utcSync: 1,
                    eventBufferSize: 0, wrapperCount: 32, new AsyncManifestEntry[0])
                .ResumeStack(Start + 10, dispatcher: 1, new ulong[] { 0x100 })
                .Suspend(Start + 20));

            Assert.Empty(computer.GetCallStacks(Key(ThreadA), Start + 15));
            Assert.Equal(0, computer.DistinctFramesCount);
            Assert.Equal(0, computer.ParseErrorCount);
        }

        [Fact]
        public void EventsBeforeMetadata_AreIgnored()
        {
            // Buffered events from a prior session must not arm a thread before this session's metadata.
            var computer = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Reset(Start)
                .ResumeStack(Start + 10, dispatcher: 1, new ulong[] { 0xDEAD })
                .Suspend(Start + 20)
                .Armed(Start + 30)
                .ResumeStack(Start + 40, dispatcher: 2, new ulong[] { 0xBEEF })
                .Suspend(Start + 50));

            Assert.Empty(computer.GetCallStacks(Key(ThreadA), Start + 15));
            Assert.Equal(0xBEEFUL, Assert.Single(computer.GetCallStacks(Key(ThreadA), Start + 45)).Frames.InstructionPointerAt(0));
            Assert.Equal(1, computer.DistinctFramesCount);
            Assert.Equal(0, computer.ParseErrorCount);
        }

        [Fact]
        public void ResetAfterMetadataButEarlierQpc_IsIgnored()
        {
            // Delivery order can differ from QPC order across threads.
            var computer = new AsyncProfilerIndexBuilder();

            computer.Process(new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .ResumeStack(Start + 10, dispatcher: 1, new ulong[] { 0xAAA })
                .Suspend(Start + 20)
                .Build());

            computer.Process(new AsyncProfilerBufferBuilder(ThreadB, startQpc: Start - 100)
                .Reset(Start - 100)
                .ResumeStack(Start - 90, dispatcher: 2, new ulong[] { 0xDEAD })
                .Suspend(Start - 80)
                .Build());

            Assert.Empty(computer.GetCallStacks(Key(ThreadB), Start - 85));

            computer.Process(new AsyncProfilerBufferBuilder(ThreadB)
                .Reset(Start + 30)
                .ResumeStack(Start + 40, dispatcher: 3, new ulong[] { 0xBEEF })
                .Suspend(Start + 50)
                .Build());

            Assert.Equal(0xBEEFUL, Assert.Single(computer.GetCallStacks(Key(ThreadB), Start + 45)).Frames.InstructionPointerAt(0));
            Assert.Equal(0, computer.ParseErrorCount);
        }

        [Fact]
        public void ResumeSuspend_ProducesInterval()
        {
            var computer = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .ResumeStack(Start + 10, dispatcher: 1, new ulong[] { 0x100, 0x200 })
                .Suspend(Start + 20));

            Assert.Empty(computer.GetCallStacks(Key(ThreadA), Start + 5));   // before start
            Assert.Empty(computer.GetCallStacks(Key(ThreadA), Start + 20));  // end is exclusive
            Assert.Empty(computer.GetCallStacks(Key(ThreadA), Start + 25));  // after end

            IReadOnlyList<AsyncProfilerCallStack> active = computer.GetCallStacks(Key(ThreadA), Start + 15);
            AsyncProfilerCallStack cs = Assert.Single(active);
            Assert.Equal(0, cs.Depth);
            Assert.Equal(Start + 10, cs.StartQpc);
            Assert.Equal(Start + 20, cs.EndQpc);
            Assert.Equal(2, cs.Frames.FrameCount);
            Assert.Equal(0x100UL, cs.Frames.InstructionPointerAt(0));
            Assert.Equal(0x200UL, cs.Frames.InstructionPointerAt(1));
            Assert.Equal(AsyncCallstackKind.StateMachineAsync, cs.Frames.Kind);
        }

        [Fact]
        public void ResumeComplete_ProducesInterval()
        {
            var computer = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .ResumeStack(Start + 10, dispatcher: 1, new ulong[] { 0x100 })
                .Complete(Start + 20));

            AsyncProfilerCallStack stack = Assert.Single(computer.GetCallStacks(Key(ThreadA), Start + 10));
            Assert.Equal(Start + 10, stack.StartQpc);
            Assert.Equal(Start + 20, stack.EndQpc);
            Assert.Same(stack, Assert.Single(computer.GetCallStacks(Key(ThreadA), Start + 15)));
            Assert.Empty(computer.GetCallStacks(Key(ThreadA), Start + 20));
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void NestedQueries_ReturnLeafToRootBeforeAndAfterReload(bool runtimeAsync, bool equalStartQpc)
        {
            AsyncEventID resume = runtimeAsync
                ? AsyncEventID.ResumeRuntimeAsyncCallstack : AsyncEventID.ResumeStateMachineAsyncCallstack;
            AsyncEventID suspend = runtimeAsync
                ? AsyncEventID.SuspendRuntimeAsyncContext : AsyncEventID.SuspendStateMachineAsyncContext;
            int[] states = runtimeAsync ? null : new[] { 0, 1 };
            var computer = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .Callstack(resume, Start + 10, 0, 0, 1, new ulong[] { 0xA, 0xB }, states)
                .Callstack(resume, Start + (equalStartQpc ? 10 : 15), 0, 0, 2, new ulong[] { 0xC, 0xD }, states)
                .Callstack(resume, Start + (equalStartQpc ? 10 : 20), 0, 0, 3, new ulong[] { 0xE, 0xF }, states)
                .ContextNoPayload(suspend, Start + 30)
                .ContextNoPayload(suspend, Start + 40)
                .ContextNoPayload(suspend, Start + 50));

            foreach (AsyncProfilerCallStacksIndex index in new[] { computer.Index, RoundTrip(computer.Index) })
            {
                IReadOnlyList<AsyncProfilerCallStack> stable = index.GetCallStacks(Key(ThreadA), Start + 25);
                Assert.Equal(new[] { 2, 1, 0 }, stable.Select(callStack => callStack.Depth));
                Assert.Equal(new ulong[] { 3, 2, 1 }, stable.Select(callStack => callStack.DispatcherId));
                Assert.Equal(new ulong[] { 0xE, 0xF },
                    stable[0].CapturedFrames.Select(frame => frame.CapturedInstructionPointer));

                if (!equalStartQpc)
                {
                    Assert.Equal(0, Assert.Single(index.GetCallStacks(Key(ThreadA), Start + 12)).Depth);
                }

                var borrowed = new List<AsyncProfilerCallStack>();
                index.GetCallStacks(Key(ThreadA), Start + 25, borrowed);
                Assert.Equal(new[] { 2, 1, 0 }, borrowed.Select(callStack => callStack.Depth));
                Assert.Equal(stable.Select(callStack => callStack.DispatcherId),
                    borrowed.Select(callStack => callStack.DispatcherId));

                index.GetCallStacks(Key(ThreadA), Start + 30, borrowed);
                Assert.Equal(new[] { 1, 0 }, borrowed.Select(callStack => callStack.Depth));
                Assert.Equal(new[] { 2, 1, 0 }, stable.Select(callStack => callStack.Depth));
                index.GetCallStacks(Key(ThreadA), Start + 50, borrowed);
                Assert.Empty(borrowed);
            }
        }

        [Fact]
        public void DispatcherLookup_ReturnsMatchingCallStack()
        {
            var computer = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .ResumeStack(Start + 10, dispatcher: 1, new ulong[] { 0xA })
                .ResumeStack(Start + 20, dispatcher: 2, new ulong[] { 0xB })
                .Suspend(Start + 30)
                .Suspend(Start + 40));

            AsyncProfilerCallStack outer = computer.Index.GetCallStack(
                Key(ThreadA), dispatcherId: 1, timestampQpc: Start + 25);
            AsyncProfilerCallStack inner = computer.Index.GetCallStack(
                Key(ThreadA), dispatcherId: 2, timestampQpc: Start + 25);

            Assert.NotNull(outer);
            Assert.Equal(Key(ThreadA), outer.Thread);
            Assert.Equal(1UL, outer.DispatcherId);
            Assert.Equal(0xAUL, outer.Frames.InstructionPointerAt(0));
            Assert.NotNull(inner);
            Assert.Equal(Key(ThreadA), inner.Thread);
            Assert.Equal(2UL, inner.DispatcherId);
            Assert.Equal(0xBUL, inner.Frames.InstructionPointerAt(0));
            Assert.Null(computer.Index.GetCallStack(
                Key(ThreadA), dispatcherId: 3, timestampQpc: Start + 25));
        }

        [Fact]
        public void DispatcherInfo_ResolvesFireAndForgetParentCallStackAndCreationQpc()
        {
            var computer = new AsyncProfilerIndexBuilder();
            computer.Process(new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .ResumeStack(Start + 10, dispatcher: 1, new ulong[] { 0xA, 0xB, 0xC })
                .CompleteMethod(Start + 15)
                .CreateContext(
                    AsyncEventID.CreateStateMachineAsyncContext,
                    Start + 20,
                    parent: 1,
                    dispatcher: 2)
                .Suspend(Start + 25)
                .Build());
            computer.Process(new AsyncProfilerBufferBuilder(ThreadB)
                .Reset(Start)
                .ResumeStack(Start + 30, dispatcher: 2, new ulong[] { 0xD })
                .Suspend(Start + 40)
                .Build());

            AsyncProfilerCallStack child = computer.Index.GetCallStack(
                Key(ThreadB), dispatcherId: 2, timestampQpc: Start + 35);
            Assert.NotNull(child);

            AsyncProfilerDispatcherInfo dispatcher = computer.Index.GetDispatcherInfo(child);
            Assert.NotNull(dispatcher);
            Assert.Equal(Key(ThreadA), dispatcher.CreationThread);
            Assert.Equal(2UL, dispatcher.DispatcherId);
            Assert.Equal(1UL, dispatcher.ParentDispatcherId);
            Assert.Equal(Start + 20, dispatcher.CreationQpc);

            Assert.True(computer.Index.TryGetParentCallStack(
                child,
                out AsyncProfilerCallStack parent,
                out long parentQpc));
            Assert.Equal(Start + 20, parentQpc);
            Assert.Equal(1UL, parent.DispatcherId);
            Assert.Equal(Key(ThreadA), parent.Thread);
            Assert.True(computer.Index.MethodCompletionObserved(parent));
            Assert.Equal(1, parent.GetCompletedFrameCount(parentQpc));
            Assert.Equal(0xBUL, parent.Frames.InstructionPointerAt(parent.GetCompletedFrameCount(parentQpc)));
            Assert.Same(parent, computer.Index.GetParentCallStack(child));

            AsyncProfilerDispatcherInfo childDispatcher = Assert.Single(
                computer.Index.GetChildDispatcherInfos(parent));
            Assert.Equal(dispatcher.CreationThread, childDispatcher.CreationThread);
            Assert.Equal(dispatcher.DispatcherId, childDispatcher.DispatcherId);
            Assert.Equal(dispatcher.ParentDispatcherId, childDispatcher.ParentDispatcherId);
            Assert.Equal(dispatcher.CreationQpc, childDispatcher.CreationQpc);

            AsyncProfilerCallStacksIndex reloaded = RoundTrip(computer.Index);
            AsyncProfilerCallStack reloadedChild = reloaded.GetCallStack(
                Key(ThreadB), dispatcherId: 2, timestampQpc: Start + 35);
            Assert.NotNull(reloadedChild);
            Assert.True(reloaded.TryGetParentCallStack(
                reloadedChild,
                out AsyncProfilerCallStack reloadedParent,
                out long reloadedParentQpc));
            Assert.Equal(Start + 20, reloadedParentQpc);
            Assert.Equal(1UL, reloadedParent.DispatcherId);
            Assert.Equal(Key(ThreadA), reloadedParent.Thread);
            Assert.True(reloaded.MethodCompletionObserved(reloadedParent));
            Assert.Equal(1, reloadedParent.GetCompletedFrameCount(reloadedParentQpc));
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void DispatcherInfo_ReentrantParentLookupSelectsInnermostCallStack(
            bool stateMachine, bool equalStartQpc)
        {
            AsyncEventID resume = stateMachine
                ? AsyncEventID.ResumeStateMachineAsyncCallstack : AsyncEventID.ResumeRuntimeAsyncCallstack;
            AsyncEventID suspend = stateMachine
                ? AsyncEventID.SuspendStateMachineAsyncContext : AsyncEventID.SuspendRuntimeAsyncContext;
            AsyncEventID create = stateMachine
                ? AsyncEventID.CreateStateMachineAsyncContext : AsyncEventID.CreateRuntimeAsyncContext;
            int[] states = stateMachine ? new[] { 0 } : null;
            long innerStartQpc = Start + (equalStartQpc ? 10 : 20);
            long childCreationQpc = equalStartQpc ? innerStartQpc : Start + 25;
            var computer = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .Callstack(resume, Start + 10, 0, 0, 1, new ulong[] { 0xA }, states)
                .Callstack(resume, innerStartQpc, 0, 0, 1, new ulong[] { 0xB }, states)
                .CreateContext(create, childCreationQpc, parent: 1, dispatcher: 2)
                .ContextNoPayload(suspend, Start + 30)
                .ContextNoPayload(suspend, Start + 40));
            computer.Process(new AsyncProfilerBufferBuilder(ThreadB)
                .Reset(Start)
                .Callstack(resume, Start + 50, 0, 0, 2, new ulong[] { 0xC }, states)
                .ContextNoPayload(suspend, Start + 60)
                .Build());

            foreach (AsyncProfilerCallStacksIndex index in new[] { computer.Index, RoundTrip(computer.Index) })
            {
                AsyncProfilerCallStack child = index.GetCallStack(
                    Key(ThreadB), dispatcherId: 2, timestampQpc: Start + 55);
                Assert.NotNull(child);
                Assert.True(index.TryGetParentCallStack(
                    child, out AsyncProfilerCallStack parent, out long creationQpc));
                Assert.Equal(1, parent.Depth);
                Assert.Equal(innerStartQpc, parent.StartQpc);
                Assert.Equal(childCreationQpc, creationQpc);
                Assert.Equal(0xBUL, parent.Frames.InstructionPointerAt(0));

                var candidates = new List<AsyncProfilerCallStack>();
                Assert.True(index.TryGetParentCallStack(
                    child, candidates, out AsyncProfilerCallStack reusableParent, out long reusableCreationQpc));
                Assert.Equal(parent.Depth, reusableParent.Depth);
                Assert.Same(parent, reusableParent);
                Assert.Equal(creationQpc, reusableCreationQpc);

                var parentContextBuilder = new AsyncProfilerParentContextBuilder();
                for (int pass = 0; pass < 2; pass++)
                {
                    Assert.True(parentContextBuilder.HasParentContextsToAdd(index, new[] { child }));
                    var frames = new List<AsyncProfilerStitchedFrame>
                    {
                        AsyncProfilerStitchedFrame.CreateAsync(child, 0),
                    };
                    var placements = new[]
                    {
                        new AsyncProfilerCallStackPlacement(child, leafIndex: 0, rootIndexExclusive: 1),
                    };

                    parentContextBuilder.AddParentContexts(index, new[] { child }, placements, frames, maximumDepth: 1);

                    Assert.Equal(2, frames.Count);
                    Assert.Same(parent, frames[1].Context);
                    Assert.Equal(AsyncProfilerStitchedFrameContextKind.ParentContext, frames[1].ContextKind);
                    Assert.Equal(0xBUL, frames[1].Segment.InstructionPointerAt(frames[1].SegmentFrameIndex));
                }
            }
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(true, true)]
        public void DispatcherInfo_RuntimeCreationFormsResolveParentWithoutOpeningCallStack(
            bool includeContextCreate, bool includeCallstackCreate)
        {
            var computer = new AsyncProfilerIndexBuilder();
            var creation = new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .ResumeRuntimeStack(Start + 10, dispatcher: 1, new ulong[] { 0xA, 0xB })
                .CompleteRuntimeMethod(Start + 15);
            if (includeContextCreate)
            {
                creation.CreateContext(AsyncEventID.CreateRuntimeAsyncContext, Start + 20, parent: 1, dispatcher: 2);
            }
            computer.Process(creation.Build());

            // Paired create events can straddle buffers, with another thread's buffer delivered between them.
            computer.Process(new AsyncProfilerBufferBuilder(ThreadB)
                .Reset(Start)
                .CreateContext(AsyncEventID.CreateRuntimeAsyncContext, Start + 20, parent: 0, dispatcher: 3)
                .Build());
            creation = new AsyncProfilerBufferBuilder(ThreadA);
            if (includeCallstackCreate)
            {
                creation.Callstack(AsyncEventID.CreateRuntimeAsyncCallstack, Start + 20, continuationIndex: 0,
                    parentDispatcherId: 1, dispatcherId: 2, new ulong[] { 0xBAD }, states: null);
            }
            computer.Process(creation.SuspendRuntime(Start + 25).Build());
            computer.Process(new AsyncProfilerBufferBuilder(ThreadB)
                .ResumeRuntimeStack(Start + 30, dispatcher: 2, new ulong[] { 0xC })
                .SuspendRuntime(Start + 40)
                .Build());

            Assert.Equal(1UL, Assert.Single(computer.GetCallStacks(Key(ThreadA), Start + 21)).DispatcherId);
            Assert.Equal(2, computer.DistinctFramesCount);
            foreach (AsyncProfilerCallStacksIndex index in new[] { computer.Index, RoundTrip(computer.Index) })
            {
                AsyncProfilerCallStack child = index.GetCallStack(Key(ThreadB), dispatcherId: 2, timestampQpc: Start + 35);
                Assert.True(index.TryGetParentCallStack(child, out AsyncProfilerCallStack parent, out long creationQpc));
                Assert.Equal(Key(ThreadA), parent.Thread);
                Assert.Equal(1UL, parent.DispatcherId);
                Assert.Equal(Start + 20, creationQpc);
                Assert.Equal(1, parent.GetMethodCompletedFrameCount(creationQpc));
                Assert.Equal(2UL, Assert.Single(index.GetChildDispatcherInfos(parent)).DispatcherId);
            }
        }

        [Fact]
        public void DispatcherInfoWithoutParent_IsKnownRoot()
        {
            var computer = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .CreateContext(
                    AsyncEventID.CreateStateMachineAsyncContext,
                    Start + 10,
                    parent: 0,
                    dispatcher: 2)
                .ResumeStack(Start + 20, dispatcher: 2, new ulong[] { 0xA })
                .Suspend(Start + 30));

            AsyncProfilerCallStack child = Assert.Single(computer.GetCallStacks(Key(ThreadA), Start + 25));
            AsyncProfilerDispatcherInfo dispatcher = computer.Index.GetDispatcherInfo(child);
            Assert.NotNull(dispatcher);
            Assert.Equal(0UL, dispatcher.ParentDispatcherId);
            Assert.False(computer.Index.MethodCompletionObserved(child));
            Assert.False(computer.Index.TryGetParentCallStack(child, out _, out _));
        }

        [Theory]
        [InlineData((int)AsyncEventID.CreateRuntimeAsyncContext)]
        [InlineData((int)AsyncEventID.CreateRuntimeAsyncCallstack)]
        [InlineData((int)AsyncEventID.CreateStateMachineAsyncContext)]
        public void DispatcherInfoBeforeThreadIsArmed_IsIgnored(int eventId)
        {
            var buffer = new AsyncProfilerBufferBuilder(ThreadA)
                .Metadata(Start, qpcFrequency: 10_000_000, qpcSync: 1, utcSync: 1,
                    eventBufferSize: 0, wrapperCount: 32, new AsyncManifestEntry[0]);
            if ((AsyncEventID)eventId == AsyncEventID.CreateRuntimeAsyncCallstack)
            {
                buffer.Callstack(AsyncEventID.CreateRuntimeAsyncCallstack, Start + 5, continuationIndex: 0,
                    parentDispatcherId: 1, dispatcherId: 2, new ulong[] { 0xB }, states: null);
            }
            else
            {
                buffer.CreateContext((AsyncEventID)eventId, Start + 5, parent: 1, dispatcher: 2);
            }
            var computer = Compute(buffer.Reset(Start + 10)
                .ResumeStack(Start + 20, dispatcher: 2, new ulong[] { 0xA })
                .Suspend(Start + 30));

            AsyncProfilerCallStack child = Assert.Single(computer.GetCallStacks(Key(ThreadA), Start + 25));
            Assert.Null(computer.Index.GetDispatcherInfo(child));
            Assert.False(computer.Index.TryGetParentCallStack(child, out _, out _));
        }

        [Fact]
        public void Append_ExtendsCallstack_FinalizedAtClose()
        {
            var computer = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .ResumeStack(Start + 10, dispatcher: 1, new ulong[] { 0xA }, new[] { 0 })
                .AppendStack(Start + 15, dispatcher: 1, new ulong[] { 0xB, 0xC }, new[] { 1, 2 }));

            Assert.Empty(computer.GetCallStacks(Key(ThreadA), Start + 18));
            Assert.Equal(0, computer.DistinctFramesCount);
            computer.Process(new AsyncProfilerBufferBuilder(ThreadA, startQpc: Start + 20)
                .Suspend(Start + 20).Build());

            AsyncProfilerCallStack cs = Assert.Single(computer.GetCallStacks(Key(ThreadA), Start + 18));
            Assert.Same(cs, Assert.Single(computer.GetCallStacks(Key(ThreadA), Start + 10)));
            Assert.Equal(3, cs.Frames.FrameCount);
            Assert.Equal(new ulong[] { 0xA, 0xB, 0xC }, new[] { cs.Frames.InstructionPointerAt(0), cs.Frames.InstructionPointerAt(1), cs.Frames.InstructionPointerAt(2) });
            Assert.Equal(new[] { 0, 1, 2 }, new[] { cs.Frames.FrameStateAt(0), cs.Frames.FrameStateAt(1), cs.Frames.FrameStateAt(2) });
            Assert.Equal(1, computer.DistinctFramesCount);
        }

        [Fact]
        public void ReusedBuilder_DoesNotRetainFramesOrCompletionHistory()
        {
            var computer = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .ResumeStack(Start + 10, dispatcher: 1, new ulong[] { 0xA }, new[] { 1 })
                .AppendStack(Start + 11, dispatcher: 1, new ulong[] { 0xB }, new[] { 2 })
                .CompleteMethod(Start + 12)
                .UnwindException(Start + 13, 1)
                .WrapperReset(Start + 14)
                .Suspend(Start + 15)
                .ResumeStack(Start + 20, dispatcher: 2, new ulong[] { 0xC }, new[] { 3 })
                .Suspend(Start + 25));

            AsyncProfilerCallStack second = Assert.Single(computer.GetCallStacks(Key(ThreadA), Start + 22));
            Assert.Equal(1, second.Frames.FrameCount);
            Assert.Equal(0xCUL, second.Frames.InstructionPointerAt(0));
            Assert.Equal(3, second.Frames.FrameStateAt(0));
            Assert.Equal(0, second.GetMethodCompletedFrameCount(Start + 22));
            Assert.Equal(0, second.GetExceptionCompletedFrameCount(Start + 22));
            Assert.Equal(0, second.GetWrapperResetCount(Start + 22));
        }

        [Fact]
        public void IdenticalCallstacks_AreDeduplicated()
        {
            var computer = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .ResumeStack(Start + 10, dispatcher: 1, new ulong[] { 0xA, 0xB })
                .Suspend(Start + 20)
                .ResumeStack(Start + 30, dispatcher: 2, new ulong[] { 0xA, 0xB })
                .Suspend(Start + 40));

            Assert.Equal(1, computer.DistinctFramesCount);

            AsyncProfilerCallStack first = Assert.Single(computer.GetCallStacks(Key(ThreadA), Start + 15));
            AsyncProfilerCallStack second = Assert.Single(computer.GetCallStacks(Key(ThreadA), Start + 35));
            Assert.Equal(first.FramesIndex, second.FramesIndex);
            Assert.Same(first.Frames, second.Frames);
        }

        [Fact]
        public void StateDifference_PreventsCallstackDeduplication()
        {
            var computer = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .ResumeStack(Start + 10, dispatcher: 1, new ulong[] { 0xA, 0xB }, new[] { 1, 2 })
                .Suspend(Start + 20)
                .ResumeStack(Start + 30, dispatcher: 2, new ulong[] { 0xA, 0xB }, new[] { 1, 3 })
                .Suspend(Start + 40));

            Assert.Equal(2, computer.DistinctFramesCount);
        }

        [Fact]
        public void PrefixMatchWithDifferentFrameCount_PreventsCallstackDeduplication()
        {
            var computer = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .ResumeRuntimeStack(Start + 10, dispatcher: 1, new ulong[] { 0xA })
                .SuspendRuntime(Start + 20)
                .ResumeRuntimeStack(Start + 30, dispatcher: 2, new ulong[] { 0xA, 0xB })
                .SuspendRuntime(Start + 40));

            Assert.Equal(2, computer.DistinctFramesCount);
        }

        [Fact]
        public void DeduplicatedResume_PromotesToIndependentFramesWhenAppended()
        {
            var computer = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .ResumeStack(Start + 10, dispatcher: 1, new ulong[] { 0xA }, new[] { 1 })
                .Suspend(Start + 20)
                .ResumeStack(Start + 30, dispatcher: 2, new ulong[] { 0xA }, new[] { 1 })
                .AppendStack(Start + 31, dispatcher: 2, new ulong[] { 0xB }, new[] { 2 })
                .Suspend(Start + 40));

            Assert.Equal(2, computer.DistinctFramesCount);

            AsyncProfilerCallStack original = Assert.Single(computer.GetCallStacks(Key(ThreadA), Start + 15));
            Assert.Equal(1, original.Frames.FrameCount);
            Assert.Equal(0xAUL, original.Frames.InstructionPointerAt(0));

            AsyncProfilerCallStack atResume = Assert.Single(computer.GetCallStacks(Key(ThreadA), Start + 30));
            AsyncProfilerCallStack afterAppend = Assert.Single(computer.GetCallStacks(Key(ThreadA), Start + 35));
            Assert.Same(atResume, afterAppend);
            Assert.Same(atResume.Frames, afterAppend.Frames);
            Assert.Equal(new ulong[] { 0xA, 0xB }, new[] { atResume.Frames.InstructionPointerAt(0), atResume.Frames.InstructionPointerAt(1) });
            Assert.Equal(new[] { 1, 2 }, new[] { atResume.Frames.FrameStateAt(0), atResume.Frames.FrameStateAt(1) });
        }

        [Fact]
        public void MultipleThreads_AreIsolated()
        {
            var computer = new AsyncProfilerIndexBuilder();
            computer.Process(new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .ResumeStack(Start + 10, dispatcher: 1, new ulong[] { 0xAAA })
                .Suspend(Start + 50)
                .Build());
            computer.Process(new AsyncProfilerBufferBuilder(ThreadB)
                .Armed(Start)
                .ResumeStack(Start + 20, dispatcher: 2, new ulong[] { 0xBBB })
                .Suspend(Start + 30)
                .Build());

            Assert.Equal(0xAAAUL, Assert.Single(computer.GetCallStacks(Key(ThreadA), Start + 25)).Frames.InstructionPointerAt(0));
            Assert.Equal(0xBBBUL, Assert.Single(computer.GetCallStacks(Key(ThreadB), Start + 25)).Frames.InstructionPointerAt(0));

            // Thread B is idle at t40; thread A is still active.
            Assert.Empty(computer.GetCallStacks(Key(ThreadB), Start + 40));
            Assert.Single(computer.GetCallStacks(Key(ThreadA), Start + 40));
        }

        [Fact]
        public void Processes_UseIndependentManifestsAndThreadState()
        {
            var computer = new AsyncProfilerIndexBuilder();

            // Process A advertises a byte-sized payload length for its resume-callstack event.
            Process(computer, ProcessA, new AsyncProfilerBufferBuilder(ThreadA)
                .Metadata(Start, qpcFrequency: 10_000_000, qpcSync: 1, utcSync: 1, eventBufferSize: 0, wrapperCount: 16,
                    new[] { new AsyncManifestEntry(AsyncEventID.ResumeStateMachineAsyncCallstack, 1, PayloadLengthFieldSize.Byte) })
                .Reset(Start + 1));
            Process(computer, ProcessA, new AsyncProfilerBufferBuilder(ThreadA, startQpc: Start + 2)
                .RawEvent((byte)AsyncEventID.ResumeStateMachineAsyncCallstack, Start + 10, PayloadLengthFieldSize.Byte,
                    new byte[] { 0, 0, 1, 1, 0x2A, 0 })
                .Suspend(Start + 20));

            // Process B retains the built-in ushort framing for the same event id. Reusing process A's manifest
            // would misframe this buffer, and sharing thread state would mix the two identical OS thread ids.
            Process(computer, ProcessB, new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .ResumeStack(Start + 10, dispatcher: 2, new ulong[] { 0x2B })
                .Suspend(Start + 20));

            Assert.Equal(0x2AUL, Assert.Single(computer.GetCallStacks(Key(ProcessA, ThreadA), Start + 15)).Frames.InstructionPointerAt(0));
            Assert.Equal(0x2BUL, Assert.Single(computer.GetCallStacks(Key(ProcessB, ThreadA), Start + 15)).Frames.InstructionPointerAt(0));
        }

        [Fact]
        public void MetadataGateAndClock_AreProcessScoped()
        {
            long utcA = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc).ToFileTimeUtc();
            long utcB = new DateTime(2026, 7, 2, 12, 0, 0, DateTimeKind.Utc).ToFileTimeUtc();
            var computer = new AsyncProfilerIndexBuilder();

            Process(computer, ProcessA, new AsyncProfilerBufferBuilder(ThreadA)
                .Metadata(Start, qpcFrequency: 10_000_000, qpcSync: (ulong)Start, utcSync: (ulong)utcA,
                    eventBufferSize: 0, wrapperCount: 16, new AsyncManifestEntry[0])
                .Reset(Start)
                .ResumeStack(Start + 10, dispatcher: 1, new ulong[] { 0xA })
                .Suspend(Start + 20));

            // Process A's metadata must not arm process B.
            Process(computer, ProcessB, new AsyncProfilerBufferBuilder(ThreadA)
                .Reset(Start)
                .ResumeStack(Start + 10, dispatcher: 2, new ulong[] { 0xDEAD })
                .Suspend(Start + 20));
            Assert.Empty(computer.GetCallStacks(Key(ProcessB, ThreadA), Start + 15));

            Process(computer, ProcessB, new AsyncProfilerBufferBuilder(ThreadA)
                .Metadata(Start + 30, qpcFrequency: 5_000_000, qpcSync: (ulong)(Start + 30), utcSync: (ulong)utcB,
                    eventBufferSize: 0, wrapperCount: 32, new AsyncManifestEntry[0])
                .Reset(Start + 30)
                .ResumeStack(Start + 40, dispatcher: 3, new ulong[] { 0xB })
                .Suspend(Start + 50));

            Assert.Equal((byte)16, computer.GetWrapperCount(ProcessA));
            Assert.Equal((byte)32, computer.GetWrapperCount(ProcessB));
            Assert.Equal(new DateTime(2026, 7, 1, 12, 0, 1, DateTimeKind.Utc), computer.QpcToDateTime(ProcessA, Start + 10_000_000));
            Assert.Equal(new DateTime(2026, 7, 2, 12, 0, 1, DateTimeKind.Utc), computer.QpcToDateTime(ProcessB, Start + 30 + 5_000_000));
        }

        [Fact]
        public void CompletionAvailabilityAndFrameInterning_AreProcessScoped()
        {
            var computer = new AsyncProfilerIndexBuilder();

            Process(computer, ProcessA, new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .ResumeStack(Start + 10, dispatcher: 1, new ulong[] { 0xA })
                .CompleteMethod(Start + 15)
                .Suspend(Start + 20));
            Process(computer, ProcessB, new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .ResumeStack(Start + 10, dispatcher: 2, new ulong[] { 0xA })
                .Suspend(Start + 20));

            foreach (AsyncProfilerCallStacksIndex index in new[] { computer.Index, RoundTrip(computer.Index) })
            {
                Assert.True(index.MethodCompletionObserved(ProcessA, AsyncCallstackKind.StateMachineAsync));
                Assert.False(index.MethodCompletionObserved(ProcessB, AsyncCallstackKind.StateMachineAsync));
                Assert.Equal(2, index.DistinctFramesCount);

                AsyncProfilerCallStack processAStack = Assert.Single(index.GetCallStacks(Key(ProcessA, ThreadA), Start + 15));
                AsyncProfilerCallStack processBStack = Assert.Single(index.GetCallStacks(Key(ProcessB, ThreadA), Start + 15));
                Assert.Equal(Key(ProcessA, ThreadA), processAStack.Thread);
                Assert.Equal(Key(ProcessB, ThreadA), processBStack.Thread);
                Assert.Equal(1UL, processAStack.DispatcherId);
                Assert.Equal(2UL, processBStack.DispatcherId);
                Assert.NotEqual(processAStack.FramesIndex, processBStack.FramesIndex);
                Assert.NotSame(processAStack.Frames, processBStack.Frames);
            }
        }

        [Fact]
        public void WrapperCount_IsCapturedWhenCallStackStarts()
        {
            var computer = new AsyncProfilerIndexBuilder();
            Process(computer, ProcessA, new AsyncProfilerBufferBuilder(ThreadA)
                .Metadata(Start, qpcFrequency: 10_000_000, qpcSync: 1, utcSync: 1,
                    eventBufferSize: 0, wrapperCount: 16, new AsyncManifestEntry[0])
                .Reset(Start)
                .ResumeStack(Start + 10, dispatcher: 1, new ulong[] { 0xA })
                .Metadata(Start + 15, qpcFrequency: 10_000_000, qpcSync: 1, utcSync: 1,
                    eventBufferSize: 0, wrapperCount: 32, new AsyncManifestEntry[0])
                .Reset(Start + 20));

            AsyncProfilerCallStack stack = Assert.Single(computer.GetCallStacks(Key(ProcessA, ThreadA), Start + 15));
            Assert.Equal((byte)16, stack.WrapperCount);
        }

        [Fact]
        public void ResetMidEpisode_CommitsInProgressCallStackAtResetQpc()
        {
            var computer = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .ResumeStack(Start + 10, dispatcher: 1, new ulong[] { 0xA })
                .Reset(Start + 15)      // commits the in-progress call stack as [Start+10, Start+15), then clears
                .Suspend(Start + 20));  // nothing to pop

            Assert.Equal(0xAUL, Assert.Single(computer.GetCallStacks(Key(ThreadA), Start + 12)).Frames.InstructionPointerAt(0));
            Assert.Empty(computer.GetCallStacks(Key(ThreadA), Start + 15));
            Assert.Empty(computer.GetCallStacks(Key(ThreadA), Start + 18));
        }

        [Fact]
        public void MultipleResetsAndMetadataWhileArmed_CommitEachOpenCallStack()
        {
            var computer = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .ResumeStack(Start + 10, dispatcher: 1, new ulong[] { 0xA })
                .Reset(Start + 20)      // commits [10,20)
                .ResumeStack(Start + 25, dispatcher: 2, new ulong[] { 0xB })
                // A second metadata mid-session (higher QPC) must neither re-gate the stream nor drop data.
                .Metadata(Start + 30, qpcFrequency: 10_000_000, qpcSync: 1, utcSync: 1,
                    eventBufferSize: 0, wrapperCount: 32, new AsyncManifestEntry[0])
                .Reset(Start + 40)      // commits [25,40)
                .ResumeStack(Start + 45, dispatcher: 3, new ulong[] { 0xC })
                .Suspend(Start + 50));  // commits [45,50)

            Assert.Equal(0xAUL, Assert.Single(computer.GetCallStacks(Key(ThreadA), Start + 12)).Frames.InstructionPointerAt(0));
            Assert.Equal(0xBUL, Assert.Single(computer.GetCallStacks(Key(ThreadA), Start + 30)).Frames.InstructionPointerAt(0));
            Assert.Equal(0xCUL, Assert.Single(computer.GetCallStacks(Key(ThreadA), Start + 48)).Frames.InstructionPointerAt(0));
            // Between a reset and the next resume nothing is active.
            Assert.Empty(computer.GetCallStacks(Key(ThreadA), Start + 22));
        }

        [Fact]
        public void CompletedFrameCount_FromCompleteAndUnwind()
        {
            var computer = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .ResumeStack(Start + 10, dispatcher: 1, new ulong[] { 0xA, 0xB, 0xC, 0xD })
                .CompleteMethod(Start + 15)             // +1
                .UnwindException(Start + 18, frames: 2) // +2
                .Suspend(Start + 30));

            AsyncProfilerCallStack cs = Assert.Single(computer.GetCallStacks(Key(ThreadA), Start + 20));
            Assert.Equal(0, cs.GetCompletedFrameCount(Start + 12));
            Assert.Equal(1, cs.GetCompletedFrameCount(Start + 16));
            Assert.Equal(3, cs.GetCompletedFrameCount(Start + 20));
            Assert.Equal(1, cs.GetMethodCompletedFrameCount(Start + 20));
            Assert.Equal(2, cs.GetExceptionCompletedFrameCount(Start + 20));
        }

        [Fact]
        public void WrapperResets_AreScopedPerCallStack_AndYieldCompletedFrames()
        {
            var computer = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Metadata(Start + 1, qpcFrequency: 10_000_000, qpcSync: 1, utcSync: 1,
                    eventBufferSize: 0, wrapperCount: 32, new AsyncManifestEntry[0])
                .Reset(Start + 2)
                .ResumeRuntimeStack(Start + 10, dispatcher: 1, new ulong[] { 0xA }, continuationIndex: 5)
                .WrapperReset(Start + 15)
                .ResumeRuntimeStack(Start + 16, dispatcher: 2, new ulong[] { 0xB }, continuationIndex: 3)
                .WrapperReset(Start + 18)
                .SuspendRuntime(Start + 19)
                .WrapperReset(Start + 20)
                .SuspendRuntime(Start + 30));

            AsyncProfilerCallStack outer = Assert.Single(computer.GetCallStacks(Key(ThreadA), Start + 22));
            AsyncProfilerCallStack inner = computer.Index.GetCallStack(Key(ThreadA), dispatcherId: 2, timestampQpc: Start + 18);
            Assert.NotNull(inner);
            Assert.Equal((byte)5, outer.ContinuationIndexBase);
            Assert.Equal((byte)32, outer.WrapperCount);

            Assert.Equal(1, outer.GetWrapperResetCount(Start + 16));
            Assert.Equal(1, outer.GetWrapperResetCount(Start + 18));
            Assert.Equal(2, outer.GetWrapperResetCount(Start + 20));
            Assert.Equal(0, inner.GetWrapperResetCount(Start + 17));
            Assert.Equal(1, inner.GetWrapperResetCount(Start + 18));
            Assert.Equal(1, inner.GetWrapperResetCount(Start + 22));

            Assert.Equal(2 * 32 + 7 - 5, outer.GetCompletedFrameCount(Start + 22, currentMethodIndex: 7));
        }

        [Fact]
        public void CompletedFrameCount_WrapperSlot_SubtractsContinuationIndexBaseOnLateAttach()
        {
            var early = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .ResumeRuntimeStack(Start + 10, dispatcher: 1, new ulong[] { 0xA }, continuationIndex: 0)
                .SuspendRuntime(Start + 30));
            AsyncProfilerCallStack earlyStack = Assert.Single(early.GetCallStacks(Key(ThreadA), Start + 20));
            Assert.Equal((byte)0, earlyStack.ContinuationIndexBase);
            Assert.Equal(8, earlyStack.GetCompletedFrameCount(Start + 20, currentMethodIndex: 8));

            // Late attach counts progress from the captured slot, clamping earlier slots to zero.
            var late = Compute(new AsyncProfilerBufferBuilder(ThreadB)
                .Armed(Start)
                .ResumeRuntimeStack(Start + 10, dispatcher: 1, new ulong[] { 0xA }, continuationIndex: 5)
                .SuspendRuntime(Start + 30));
            AsyncProfilerCallStack lateStack = Assert.Single(late.GetCallStacks(Key(ThreadB), Start + 20));
            Assert.Equal((byte)5, lateStack.ContinuationIndexBase);
            Assert.Equal(3, lateStack.GetCompletedFrameCount(Start + 20, currentMethodIndex: 8));
            Assert.Equal(0, lateStack.GetCompletedFrameCount(Start + 20, currentMethodIndex: 5));
            Assert.Equal(0, lateStack.GetCompletedFrameCount(Start + 20, currentMethodIndex: 3));
        }

        [Fact]
        public void MethodCompletionObserved_IsTrueOnlyForTheKindThatEmittedCompleteMethod()
        {
            // No completion events at all: neither kind is observed.
            var none = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .ResumeStack(Start + 10, dispatcher: 1, new ulong[] { 0xA })
                .Suspend(Start + 30));
            Assert.False(none.Index.MethodCompletionObserved(AsyncCallstackKind.StateMachineAsync));
            Assert.False(none.Index.MethodCompletionObserved(AsyncCallstackKind.RuntimeAsync));

            // V1 (StateMachine) CompleteMethod: only StateMachineAsync is observed.
            var v1 = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .ResumeStack(Start + 10, dispatcher: 1, new ulong[] { 0xA })
                .CompleteMethod(Start + 15)
                .Suspend(Start + 30));
            Assert.True(v1.Index.MethodCompletionObserved(AsyncCallstackKind.StateMachineAsync));
            Assert.False(v1.Index.MethodCompletionObserved(AsyncCallstackKind.RuntimeAsync));
            Assert.False(v1.Index.ExceptionCompletionObserved(AsyncCallstackKind.StateMachineAsync));

            // V2 (Runtime) CompleteMethod: only RuntimeAsync is observed.
            var v2 = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .ResumeRuntimeStack(Start + 10, dispatcher: 1, new ulong[] { 0xA })
                .CompleteRuntimeMethod(Start + 15)
                .SuspendRuntime(Start + 30));
            Assert.True(v2.Index.MethodCompletionObserved(AsyncCallstackKind.RuntimeAsync));
            Assert.False(v2.Index.MethodCompletionObserved(AsyncCallstackKind.StateMachineAsync));
            Assert.False(v2.Index.ExceptionCompletionObserved(AsyncCallstackKind.RuntimeAsync));
        }

        [Fact]
        public void MethodCompletionObserved_IsScopedToMetadataEpoch()
        {
            var computer = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Metadata(Start, qpcFrequency: 10_000_000, qpcSync: 1, utcSync: 1,
                    eventBufferSize: 0, wrapperCount: 32, new AsyncManifestEntry[0])
                .Reset(Start)
                .ResumeRuntimeStack(Start + 10, dispatcher: 1, new ulong[] { 0xA })
                .SuspendRuntime(Start + 20)
                .Metadata(Start + 30, qpcFrequency: 10_000_000, qpcSync: 1, utcSync: 1,
                    eventBufferSize: 0, wrapperCount: 32, new AsyncManifestEntry[0])
                .Reset(Start + 30)
                .ResumeRuntimeStack(Start + 40, dispatcher: 2, new ulong[] { 0xB })
                .CompleteRuntimeMethod(Start + 45)
                .SuspendRuntime(Start + 50)
                .Metadata(Start + 60, qpcFrequency: 10_000_000, qpcSync: 1, utcSync: 1,
                    eventBufferSize: 0, wrapperCount: 32, new AsyncManifestEntry[0])
                .Reset(Start + 60)
                .ResumeRuntimeStack(Start + 70, dispatcher: 3, new ulong[] { 0xC })
                .SuspendRuntime(Start + 80));

            Assert.False(computer.Index.MethodCompletionObserved(
                0, AsyncCallstackKind.RuntimeAsync, Start + 10));
            Assert.True(computer.Index.MethodCompletionObserved(
                0, AsyncCallstackKind.RuntimeAsync, Start + 40));
            Assert.False(computer.Index.MethodCompletionObserved(
                0, AsyncCallstackKind.RuntimeAsync, Start + 70));
            Assert.True(computer.Index.MethodCompletionObserved(AsyncCallstackKind.RuntimeAsync));

            AsyncProfilerCallStacksIndex reloaded = RoundTrip(computer.Index);
            Assert.False(reloaded.MethodCompletionObserved(
                0, AsyncCallstackKind.RuntimeAsync, Start + 10));
            Assert.True(reloaded.MethodCompletionObserved(
                0, AsyncCallstackKind.RuntimeAsync, Start + 40));
            Assert.False(reloaded.MethodCompletionObserved(
                0, AsyncCallstackKind.RuntimeAsync, Start + 70));
        }

        [Fact]
        public void BorrowedQueries_DoNotPopulateCompletionObservationsOrOverwriteStableResults()
        {
            var computer = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .ResumeStack(Start + 10, dispatcher: 1, new ulong[] { 0xA, 0xB })
                .CompleteMethod(Start + 15)
                .UnwindException(Start + 16, 1)
                .Suspend(Start + 20)
                .Metadata(Start + 30, qpcFrequency: 10_000_000, qpcSync: 1, utcSync: 1,
                    eventBufferSize: 0, wrapperCount: 32, new AsyncManifestEntry[0])
                .Reset(Start + 30)
                .ResumeStack(Start + 40, dispatcher: 2, new ulong[] { 0xC })
                .Suspend(Start + 50));

            foreach (AsyncProfilerCallStacksIndex index in new[] { computer.Index, RoundTrip(computer.Index) })
            {
                AsyncProfilerCallStack retained = Assert.Single(index.GetCallStacks(Key(ThreadA), Start + 18));
                IReadOnlyList<AsyncProfilerCallStackFrame> retainedFrames = retained.CapturedFrames;
                var borrowed = new List<AsyncProfilerCallStack>();
                IReadOnlyList<AsyncProfilerCallStackFrame> capturedFrames = null;
                foreach (long timestampQpc in new[] { Start + 18, Start + 45, Start + 18 })
                {
                    index.GetCallStacks(Key(ThreadA), timestampQpc, borrowed);
                    AsyncProfilerCallStack callStack = Assert.Single(borrowed);
                    Assert.False(callStack.MethodCompletionObserved);
                    Assert.False(callStack.ExceptionCompletionObserved);
                    capturedFrames = capturedFrames ?? callStack.CapturedFrames;
                    Assert.Same(capturedFrames, callStack.CapturedFrames);

                    bool observed = timestampQpc < Start + 30;
                    Assert.Equal(observed ? 2 : 1, capturedFrames.Count);
                    Assert.Equal(observed ? 0xAUL : 0xCUL, capturedFrames[0].CapturedInstructionPointer);
                    AsyncProfilerCallStack stable = Assert.Single(index.GetCallStacks(Key(ThreadA), timestampQpc));
                    Assert.NotSame(callStack, stable);
                    Assert.Equal(observed, stable.MethodCompletionObserved);
                    Assert.Equal(observed, stable.ExceptionCompletionObserved);
                    Assert.Equal(observed, index.GetCallStacks(Key(ThreadA))[observed ? 0 : 1].MethodCompletionObserved);
                    Assert.Equal(observed, index.GetCallStacks(Key(ThreadA))[observed ? 0 : 1].ExceptionCompletionObserved);

                    Assert.Equal(1UL, retained.DispatcherId);
                    Assert.True(retained.MethodCompletionObserved);
                    Assert.True(retained.ExceptionCompletionObserved);
                    Assert.Equal(2, retained.GetCompletedFrameCount(Start + 18));
                    Assert.Equal(2, retainedFrames.Count);
                    Assert.Equal(0xAUL, retainedFrames[0].CapturedInstructionPointer);
                    Assert.Equal(0xBUL, retainedFrames[1].CapturedInstructionPointer);
                }
            }
        }

        [Fact]
        public async Task StableQueries_AreSafeDuringConcurrentFirstUse()
        {
            var computer = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .ResumeStack(Start + 10, dispatcher: 1, new ulong[] { 0xA, 0xB })
                .Suspend(Start + 20));
            AsyncProfilerCallStacksIndex reloaded = RoundTrip(computer.Index);
            AsyncProfilerThreadKey thread = Key(ThreadA);
            var results = new AsyncProfilerCallStack[32];
            var tasks = new Task[results.Length];
            var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            int readyCount = 0;

            for (int i = 0; i < tasks.Length; i++)
            {
                int resultIndex = i;
                tasks[i] = Task.Run(async () =>
                {
                    if (Interlocked.Increment(ref readyCount) == results.Length)
                    {
                        ready.SetResult(true);
                    }
                    await ready.Task;

                    for (int iteration = 0; iteration < 100; iteration++)
                    {
                        results[resultIndex] = Assert.Single(
                            reloaded.GetCallStacks(thread, Start + 15));
                        Assert.Same(results[resultIndex], Assert.Single(
                            reloaded.GetCallStacks(thread)));
                    }
                });
            }

            await Task.WhenAll(tasks);
            for (int i = 1; i < results.Length; i++)
            {
                Assert.Same(results[0], results[i]);
            }
        }

        [Fact]
        public void UnwindException_SetsExceptionCompletionObserved_NotMethodCompletionObserved()
        {
            // Exception records must not disable the physical-stack fallback for normal V1 completions.
            var v1 = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .ResumeStack(Start + 10, dispatcher: 1, new ulong[] { 0xA, 0xB })
                .UnwindException(Start + 15, frames: 1)
                .Suspend(Start + 30));
            Assert.True(v1.Index.ExceptionCompletionObserved(AsyncCallstackKind.StateMachineAsync));
            Assert.False(v1.Index.MethodCompletionObserved(AsyncCallstackKind.StateMachineAsync));
            Assert.False(v1.Index.ExceptionCompletionObserved(AsyncCallstackKind.RuntimeAsync));
            Assert.False(v1.Index.MethodCompletionObserved(AsyncCallstackKind.RuntimeAsync));
        }

        [Fact]
        public void CompletionEventsBeforeArming_DoNotSetCompletionAvailability()
        {
            var computer = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .CompleteMethod(Start)
                .UnwindException(Start + 1, frames: 1)
                .CompleteRuntimeMethod(Start + 2)
                .UnwindRuntimeException(Start + 3, frames: 1)
                .Metadata(Start + 10, qpcFrequency: 10_000_000, qpcSync: 1, utcSync: 1,
                    eventBufferSize: 0, wrapperCount: 32, new AsyncManifestEntry[0])
                .CompleteMethod(Start + 11)
                .UnwindException(Start + 12, frames: 1)
                .CompleteRuntimeMethod(Start + 13)
                .UnwindRuntimeException(Start + 14, frames: 1)
                .Reset(Start + 20)
                .ResumeStack(Start + 30, dispatcher: 1, new ulong[] { 0xA })
                .Suspend(Start + 40));

            Assert.False(computer.Index.MethodCompletionObserved(AsyncCallstackKind.StateMachineAsync));
            Assert.False(computer.Index.ExceptionCompletionObserved(AsyncCallstackKind.StateMachineAsync));
            Assert.False(computer.Index.MethodCompletionObserved(AsyncCallstackKind.RuntimeAsync));
            Assert.False(computer.Index.ExceptionCompletionObserved(AsyncCallstackKind.RuntimeAsync));
        }

        [Fact]
        public void AppendWithDifferentDispatcher_DoesNotMutateCurrentCallStack()
        {
            var computer = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .ResumeStack(Start + 10, dispatcher: 1, new ulong[] { 0xA })
                .AppendStack(Start + 15, dispatcher: 2, new ulong[] { 0xB })
                .Suspend(Start + 20));

            AsyncProfilerCallStack stack = Assert.Single(computer.GetCallStacks(Key(ThreadA), Start + 18));
            Assert.Equal(1, stack.Frames.FrameCount);
            Assert.Equal(0xAUL, stack.Frames.InstructionPointerAt(0));
        }

        [Fact]
        public void Metadata_EstablishesClock()
        {
            long utcFileTime = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc).ToFileTimeUtc();
            var computer = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Metadata(Start + 1, qpcFrequency: 10_000_000, qpcSync: (ulong)(Start + 1), utcSync: (ulong)utcFileTime,
                    eventBufferSize: 0, wrapperCount: 32, new AsyncManifestEntry[0]));

            Assert.True(computer.ClockKnown);
            Assert.Equal((byte)32, computer.WrapperCount);

            // One second of QPC (10,000,000 ticks) after the sync point => one second later in UTC.
            DateTime? at = computer.QpcToDateTime(Start + 1 + 10_000_000);
            Assert.NotNull(at);
            Assert.Equal(new DateTime(2026, 7, 1, 12, 0, 1, DateTimeKind.Utc), at.Value);
        }

        [Fact]
        public void QpcToDateTime_LargePositiveAndNegativeDeltasDoNotOverflowIntermediateMultiplication()
        {
            const long qpcFrequency = 10_000_000;
            long utcFileTime = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc).ToFileTimeUtc();
            long threeDaysQpc = 3L * 24 * 60 * 60 * qpcFrequency;
            long qpcSync = threeDaysQpc + Start;
            var computer = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Metadata(Start, (ulong)qpcFrequency, (ulong)qpcSync, (ulong)utcFileTime,
                    eventBufferSize: 0, wrapperCount: 32, new AsyncManifestEntry[0]));

            DateTime? after = computer.QpcToDateTime(qpcSync + threeDaysQpc);
            DateTime? before = computer.QpcToDateTime(qpcSync - threeDaysQpc);

            Assert.Equal(new DateTime(2026, 7, 4, 12, 0, 0, DateTimeKind.Utc), after);
            Assert.Equal(new DateTime(2026, 6, 28, 12, 0, 0, DateTimeKind.Utc), before);
        }

        [Fact]
        public void SameTimestamp_Completions_AreAllCounted()
        {
            var computer = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .ResumeStack(Start + 10, dispatcher: 1, new ulong[] { 0xA, 0xB, 0xC, 0xD })
                .CompleteMethod(Start + 15)
                .CompleteMethod(Start + 15) // same timestamp
                .Suspend(Start + 30));

            AsyncProfilerCallStack cs = Assert.Single(computer.GetCallStacks(Key(ThreadA), Start + 20));
            Assert.Equal(0, cs.GetCompletedFrameCount(Start + 14));
            Assert.Equal(2, cs.GetCompletedFrameCount(Start + 15));
            Assert.Equal(2, cs.GetCompletedFrameCount(Start + 16));
        }

        [Fact]
        public void CoarseTimestamps_ZeroWidthCallStack_ExcludedButOuterStaysCoherent()
        {
            // Coarse timestamps can produce recorded call stacks with empty half-open intervals.
            var computer = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .ResumeStack(Start + 10, dispatcher: 1, new ulong[] { 0xA })
                .ResumeStack(Start + 10, dispatcher: 2, new ulong[] { 0xB })
                .Suspend(Start + 10)   // pops D2 -> zero-width [10,10)
                .Suspend(Start + 20)); // pops D1 -> [10,20)

            AsyncProfilerCallStack atTen = Assert.Single(computer.GetCallStacks(Key(ThreadA), Start + 10));
            Assert.Equal(0xAUL, atTen.Frames.InstructionPointerAt(0)); // only the outer D1
            Assert.Equal(0xAUL, Assert.Single(computer.GetCallStacks(Key(ThreadA), Start + 15)).Frames.InstructionPointerAt(0));

            Assert.Equal(2, computer.DistinctFramesCount);
        }

        [Fact]
        public void CoarseTimestamps_AllTransitionsSameTick_ReturnCoherentStack()
        {
            // Sequential zero-width call stacks reuse the same depth within a single tick.
            var computer = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Armed(Start)
                .ResumeStack(Start + 10, dispatcher: 1, new ulong[] { 0xA })
                .ResumeStack(Start + 10, dispatcher: 2, new ulong[] { 0xB })
                .Suspend(Start + 10)   // pop D2 (zero-width, depth 1)
                .ResumeStack(Start + 10, dispatcher: 3, new ulong[] { 0xC })
                .Suspend(Start + 10)   // pop D3 (zero-width, depth 1)
                .Suspend(Start + 20)); // pop D1 -> [10,20)

            AsyncProfilerCallStack only = Assert.Single(computer.GetCallStacks(Key(ThreadA), Start + 10));
            Assert.Equal(0, only.Depth);
            Assert.Equal(0xAUL, only.Frames.InstructionPointerAt(0));
            Assert.Equal(3, computer.DistinctFramesCount); // A, B, C all recorded
        }

        [Fact]
        public void Index_SerializesAndDeserializes_RoundTrip()
        {
            long utc = new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc).ToFileTimeUtc();
            var computer = Compute(new AsyncProfilerBufferBuilder(ThreadA)
                .Metadata(Start + 1, qpcFrequency: 10_000_000, qpcSync: (ulong)(Start + 1), utcSync: (ulong)utc,
                    eventBufferSize: 0, wrapperCount: 32, new AsyncManifestEntry[0])
                .Reset(Start + 2)
                .ResumeStack(Start + 10, dispatcher: 1, new ulong[] { 0xA, 0xB }, new[] { 1, 2 }, continuationIndex: 5)
                .ResumeStack(Start + 15, dispatcher: 2, new ulong[] { 0xC }, new[] { 3 })   // nested
                .WrapperReset(Start + 17)
                .UnwindException(Start + 18, frames: 1)
                .Suspend(Start + 20)          // pop D2 -> [15,20)
                .CompleteMethod(Start + 25)   // applies to D1
                .Suspend(Start + 30)          // pop D1 -> [10,30)
                .ResumeStack(Start + 40, dispatcher: 3, new ulong[] { 0xA, 0xB }, new[] { 1, 2 }) // dedups with D1
                .Suspend(Start + 50));

            AsyncProfilerCallStacksIndex original = computer.Index;

            // Synthetic address indices exercise persistence without symbol resolution.
            for (int fi = 0; fi < original.DistinctFramesCount; fi++)
            {
                AsyncProfilerCallStackFrames f = original.GetFrames((AsyncProfilerCallStackFramesIndex)fi);
                for (int s = 0; s < f.FrameCount; s++)
                {
                    f.SetCodeAddressAt(s, (CodeAddressIndex)(int)f.InstructionPointerAt(s));
                }
            }

            AsyncProfilerCallStacksIndex reloaded = RoundTrip(original);

            Assert.Equal(original.DistinctFramesCount, reloaded.DistinctFramesCount);
            Assert.Equal(2, reloaded.DistinctFramesCount); // {A,B} shared by D1 & D3, and {C}

            // Registered address indices round-trip independently of symbol resolution.
            AsyncProfilerCallStack d1 = Assert.Single(reloaded.GetCallStacks(Key(ThreadA), Start + 12));
            Assert.Equal((CodeAddressIndex)0xA, d1.Frames.CodeAddressAt(0));
            Assert.Equal((CodeAddressIndex)0xB, d1.Frames.CodeAddressAt(1));

            foreach (long qpc in new[] { Start + 12, Start + 17, Start + 19, Start + 25, Start + 45, Start + 60 })
            {
                AssertSameStacks(original, reloaded, Key(ThreadA), qpc);
            }
        }

        #region private

        private static AsyncProfilerThreadKey Key(ulong osThreadId) => Key((ProcessIndex)0, osThreadId);

        private static AsyncProfilerThreadKey Key(ProcessIndex processIndex, ulong osThreadId) =>
            new AsyncProfilerThreadKey(processIndex, osThreadId);

        private static AsyncProfilerIndexBuilder Compute(AsyncProfilerBufferBuilder builder)
        {
            var computer = new AsyncProfilerIndexBuilder();
            computer.Process(builder.Build());
            return computer;
        }

        private static void Process(AsyncProfilerIndexBuilder computer, ProcessIndex processIndex, AsyncProfilerBufferBuilder builder) =>
            computer.Process(builder.Build(), processIndex);

        private static void AssertSameStacks(
            AsyncProfilerCallStacksIndex expected, AsyncProfilerCallStacksIndex actual,
            AsyncProfilerThreadKey thread, long qpc)
        {
            IReadOnlyList<AsyncProfilerCallStack> expectedStacks = expected.GetCallStacks(thread, qpc);
            IReadOnlyList<AsyncProfilerCallStack> actualStacks = actual.GetCallStacks(thread, qpc);
            Assert.Equal(expectedStacks.Count, actualStacks.Count);
            for (int i = 0; i < expectedStacks.Count; i++)
            {
                AsyncProfilerCallStack expectedStack = expectedStacks[i];
                AsyncProfilerCallStack actualStack = actualStacks[i];
                Assert.Equal(expectedStack.Thread, actualStack.Thread);
                Assert.Equal(expectedStack.DispatcherId, actualStack.DispatcherId);
                Assert.Equal(expectedStack.Depth, actualStack.Depth);
                Assert.Equal(expectedStack.FramesIndex, actualStack.FramesIndex);
                Assert.Equal(expectedStack.ContinuationIndexBase, actualStack.ContinuationIndexBase);
                Assert.Equal(expectedStack.WrapperCount, actualStack.WrapperCount);
                Assert.Equal(expectedStack.StartQpc, actualStack.StartQpc);
                Assert.Equal(expectedStack.EndQpc, actualStack.EndQpc);
                Assert.Equal(expectedStack.MethodCompletionObserved, actualStack.MethodCompletionObserved);
                Assert.Equal(expectedStack.ExceptionCompletionObserved, actualStack.ExceptionCompletionObserved);
                Assert.Equal(expectedStack.GetCompletedFrameCount(qpc), actualStack.GetCompletedFrameCount(qpc));
                Assert.Equal(expectedStack.GetMethodCompletedFrameCount(qpc), actualStack.GetMethodCompletedFrameCount(qpc));
                Assert.Equal(expectedStack.GetExceptionCompletedFrameCount(qpc), actualStack.GetExceptionCompletedFrameCount(qpc));
                Assert.Equal(expectedStack.GetWrapperResetCount(qpc), actualStack.GetWrapperResetCount(qpc));

                AsyncProfilerCallStackFrames expectedFrames = expectedStack.Frames;
                AsyncProfilerCallStackFrames actualFrames = actualStack.Frames;
                Assert.Equal(expectedFrames.Kind, actualFrames.Kind);
                Assert.Equal(expectedFrames.FrameCount, actualFrames.FrameCount);
                for (int f = 0; f < expectedFrames.FrameCount; f++)
                {
                    Assert.Equal(expectedFrames.InstructionPointerAt(f), actualFrames.InstructionPointerAt(f));
                    Assert.Equal(expectedFrames.FrameStateAt(f), actualFrames.FrameStateAt(f));
                    Assert.Equal(expectedFrames.CodeAddressAt(f), actualFrames.CodeAddressAt(f));
                }
            }
        }

        private static AsyncProfilerCallStacksIndex RoundTrip(AsyncProfilerCallStacksIndex index)
        {
            SerializationSettings settings = SerializationSettings.Default.WithStreamLabelWidth(StreamLabelWidth.EightBytes);
            using (var stream = new MemoryStream())
            {
                using (var serializer = new Serializer(new IOStreamStreamWriter(stream, settings, leaveOpen: true), index))
                {
                }

                stream.Position = 0;
                using (var deserializer = new Deserializer(new PinnedStreamReader(stream, settings), "AsyncProfilerCallStacksIndexRoundTrip"))
                {
                    deserializer.RegisterType(typeof(AsyncProfilerCallStacksIndex));
                    return (AsyncProfilerCallStacksIndex)deserializer.ReadObject();
                }
            }
        }

        private const ulong ThreadA = 0x1000;
        private const ulong ThreadB = 0x2000;
        private const long Start = 1_000_000;
        private const ProcessIndex ProcessA = (ProcessIndex)1;
        private const ProcessIndex ProcessB = (ProcessIndex)2;

        #endregion
    }
}
