// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Kernel;
using Xunit;

#pragma warning disable CS0618 // Exact event QPCs are required for stack provenance assertions.

namespace TraceEventTests
{
    public class StackUserModeTimeTests
    {
        [Theory]
        [InlineData(false, false)]
        [InlineData(false, true)]
        [InlineData(true, false)]
        [InlineData(true, true)]
        public void BorrowedUserStack_PersistsSourceEventTime(bool compressed, bool earlierSource)
        {
            long sourceQpc = StartQpc + (earlierSource ? 5 : 30);
            WithTrace(source =>
            {
                if (earlierSource) { source.Sample(sourceQpc, UserIp); }
                source.Sample(TargetQpc, KernelIp);
                source.KernelStack(TargetQpc, compressed);
                if (!earlierSource) { source.Sample(sourceQpc, UserIp); }
                source.UserStack(sourceQpc, compressed);
            }, log =>
            {
                var samples = log.Events.Where(e => e is SampledProfileTraceData).Select(e => e.Clone()).ToArray();
                TraceEvent target = samples.Single(e => e.TimeStampQPC == TargetQpc);
                Assert.True(target.TryGetCallStackIndex(out var stack, out long userQpc));
                Assert.Equal(sourceQpc, userQpc);
                Assert.Equal(target.CallStackIndex(), stack);
                Assert.Equal(TargetQpc, target.TimeStampQPC);
                Assert.Equal(KernelIp, log.CodeAddresses[log.CallStacks.CodeAddressIndex(stack)].Address);
                stack = log.CallStacks.Caller(stack);
                Assert.Equal(UserIp, log.CodeAddresses[log.CallStacks.CodeAddressIndex(stack)].Address);
                TraceEvent origin = samples.Single(e => e.TimeStampQPC == sourceQpc);
                Assert.True(origin.TryGetCallStackIndex(out var originStack, out userQpc));
                Assert.Equal(stack, originStack);
                Assert.Equal(sourceQpc, userQpc);
                Assert.Equal(123L, log.GetStackUserModeTimeStampQPC(origin.EventIndex, 123));
            });
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void KernelReturnAndEndOfTrace_KeepEventTime(bool genuineReturn)
        {
            WithTrace(source =>
            {
                source.Sample(TargetQpc, KernelIp);
                source.KernelStack(TargetQpc, compressed: false);
                if (genuineReturn) { source.KernelReturn(); }
            }, log =>
            {
                TraceEvent sample = log.Events.Single(e => e is SampledProfileTraceData);
                Assert.True(sample.TryGetCallStackIndex(out var stack, out long userQpc));
                Assert.Equal(sample.CallStackIndex(), stack);
                Assert.Equal(TargetQpc, userQpc);
                Assert.Equal(123L, log.GetStackUserModeTimeStampQPC(sample.EventIndex, 123));
                Assert.Equal(genuineReturn ? 2 : 1, log.CallStacks.Depth(stack));
            });
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void ContextSwitch_NormalAndBlockingStacksHaveIndependentTimes(bool compressed)
        {
            WithTrace(source =>
            {
                source.Switch(StartQpc + 1, 6000, ThreadId);
                source.PlainStack(StartQpc + 2, StartQpc + 1, 6000, UserIp + 8);
                source.Switch(TargetQpc, ThreadId, 6000);
                source.KernelStack(TargetQpc, compressed);
                source.Sample(StartQpc + 30, UserIp);
                source.UserStack(StartQpc + 30, compressed);
            }, log =>
            {
                var switches = log.Events.Where(e => e is CSwitchTraceData).Select(e => (CSwitchTraceData)e.Clone()).ToArray();
                Assert.Equal(2, switches.Length);
                Assert.True(switches[0].TryGetCallStackIndex(out var normal, out long normalQpc));
                Assert.True(switches[0].TryGetBlockingStackIndex(out var blocking, out long blockingQpc));
                Assert.Equal(switches[0].CallStackIndex(), normal);
                Assert.Equal(switches[0].BlockingStack(), blocking);
                Assert.NotEqual(normal, blocking);
                Assert.Equal(StartQpc + 1, normalQpc);
                Assert.Equal(StartQpc + 30, blockingQpc);
                Assert.False(switches[1].TryGetBlockingStackIndex(out blocking, out blockingQpc));
                Assert.Equal(CallStackIndex.Invalid, blocking);
                Assert.Equal(TargetQpc, blockingQpc);
            });
        }

        private static void WithTrace(Action<StackEventSource> emit, Action<TraceLog> verify) =>
            SyntheticEtwSource.ConvertAndVerify(() => new StackEventSource(emit), verify);

        private unsafe sealed class StackEventSource : SyntheticEtwSource
        {
            public StackEventSource(Action<StackEventSource> emit) : base(StartQpc, ProcessId)
            {
                m_emit = emit;
            }

            public override bool Process()
            {
                m_emit(this);
                return true;
            }

            public void Sample(long qpc, ulong ip)
            {
                Emit(KernelTraceEventParser.PerfInfoTaskGuid, 46, qpc, ThreadId, Payload(w =>
                {
                    w.Write(ip);
                    w.Write(ThreadId);
                    w.Write(1);
                }));
            }

            public void Switch(long qpc, int newThread, int oldThread)
            {
                Emit(KernelTraceEventParser.ThreadTaskGuid, 36, qpc, newThread, Payload(w =>
                {
                    w.Write(newThread);
                    w.Write(oldThread);
                    w.Write(new byte[16]);
                }));
            }

            public void KernelStack(long eventQpc, bool compressed)
            {
                if (compressed) { StackKey(eventQpc + 1, eventQpc, 37, 0xA); }
                else { PlainStack(eventQpc + 1, eventQpc, ThreadId, KernelIp); }
            }

            public void UserStack(long eventQpc, bool compressed)
            {
                if (compressed)
                {
                    StackKey(StartQpc + 40, eventQpc, 38, 0xB);
                    // Resolve user frames first, then the kernel frames: source time must survive both definitions.
                    Definition(StartQpc + 50, 0xB, UserIp);
                    Definition(StartQpc + 60, 0xA, KernelIp);
                }
                else { PlainStack(StartQpc + 40, eventQpc, ThreadId, UserIp); }
            }

            public void PlainStack(long qpc, long eventQpc, int threadId, ulong ip)
            {
                Emit(KernelTraceEventParser.StackWalkTaskGuid, 32, qpc, threadId, Payload(w =>
                {
                    w.Write(eventQpc);
                    w.Write(ProcessId);
                    w.Write(threadId);
                    w.Write(ip);
                }));
            }

            public void KernelReturn()
            {
                ulong* addresses = stackalloc ulong[2];
                addresses[0] = 0;
                addresses[1] = UserIp;
                var extended = new TraceEventNativeMethods.EVENT_HEADER_EXTENDED_DATA_ITEM
                {
                    ExtType = TraceEventNativeMethods.EVENT_HEADER_EXT_TYPE_STACK_TRACE64,
                    DataSize = 16,
                    DataPtr = (ulong)addresses
                };
                Emit(KernelTraceEventParser.EventTracingProviderGuid, 24, StartQpc + 40, ThreadId,
                    new byte[0], &extended);
            }

            private void StackKey(long qpc, long eventQpc, byte opcode, ulong key)
            {
                Emit(KernelTraceEventParser.StackWalkTaskGuid, opcode, qpc, ThreadId, Payload(w =>
                {
                    w.Write(eventQpc);
                    w.Write(ProcessId);
                    w.Write(ThreadId);
                    w.Write(key);
                }));
            }

            private void Definition(long qpc, ulong key, ulong ip)
            {
                Emit(KernelTraceEventParser.StackWalkTaskGuid, 36, qpc, ThreadId, Payload(w =>
                {
                    w.Write(key);
                    w.Write(ip);
                }));
            }

            // Classic kernel events, except the stack-trace extended item that only a manifest (id 18) header carries.
            private void Emit(Guid provider, byte opcode, long qpc, int threadId, byte[] payload,
                TraceEventNativeMethods.EVENT_HEADER_EXTENDED_DATA_ITEM* extended = null) =>
                Emit(provider, extended == null ? (ushort)0 : (ushort)18, opcode, qpc, threadId, classic: extended == null, payload, extended);

            private readonly Action<StackEventSource> m_emit;
        }

        private const int ProcessId = 1234;
        private const int ThreadId = 5000;
        private const long StartQpc = 100_000;
        private const long TargetQpc = StartQpc + 10;
        private const ulong KernelIp = 0xFFFFF80000001000;
        private const ulong UserIp = 0x1000;
    }
}
