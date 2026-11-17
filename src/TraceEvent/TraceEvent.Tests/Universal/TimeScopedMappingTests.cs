using System;
using System.Collections.Generic;
using System.IO;
using FastSerialization;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.EventPipe;
using Xunit;
using Xunit.Abstractions;

namespace TraceEventTests
{
    /// <summary>
    /// Regression tests for time-scoped Universal ProcessMapping / ProcessSymbol events (microsoft/one-collect#339).
    ///
    /// Dynamically generated code (e.g. BPF JIT programs) can be unloaded and its address range reused by a
    /// different program within the same process.  Producers describe this with a new ProcessMapping for each
    /// generation, plus a "tombstone" mapping (vmlinux) over the range when it is unloaded.  Mappings are live
    /// over [load, unload).  Each sample must resolve against the mapping that was current at its timestamp,
    /// rather than whichever symbol was first seen for that address.
    /// </summary>
    public class TimeScopedMappingTests : TestBase
    {
        private static readonly Guid UniversalSystemProviderGuid = new Guid("8c107b6c-79f8-5231-4de6-2a0e20a3f562");
        private static readonly Guid UniversalEventsProviderGuid = new Guid("bc5e5d63-9799-5873-33d9-fba8316cef71");

        private const int ProcessCreateMetadataId = 1;
        private const int ProcessMappingMetadataId = 2;
        private const int ProcessSymbolMetadataId = 3;
        private const int CpuSampleMetadataId = 4;

        private const int ProcessCreateEventId = 1;
        private const int ProcessMappingEventId = 3;
        private const int ProcessSymbolEventId = 4;
        private const int CpuSampleEventId = 1;

        // Process 10 sees address reuse.  Process 20 only ever sees program A at the same addresses.
        private const int ReusePid = 10;
        private const int OtherPid = 20;
        private const long ReuseThreadIndex = 1;
        private const long OtherThreadIndex = 2;

        // Program A occupies [Base, Base + 0xff] (inclusive), program B the shorter [Base, Base + 0x7f].
        private const ulong Base = 0xffffffffc0001000;
        private const ulong KernelStart = 0xffff800000000000;

        public TimeScopedMappingTests(ITestOutputHelper output)
            : base(output)
        {
        }

        /// <summary>
        /// Each sample resolves against the mapping current at its own timestamp, whether the producer writes
        /// mappings before samples at equal timestamps (current one-collect) or after them (older producers).
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SamplesResolveAgainstMappingCurrentAtTheirTime(bool mappingsBeforeSamplesAtSameTime)
        {
            byte[] nettrace = BuildNettrace(mappingsBeforeSamplesAtSameTime);
            List<(int pid, long time, ulong address, string method, string module)> samples = GetLeafFrames(nettrace);

            // Before the first load: no symbol, whether or not an earlier mapping (the kernel) covers the address.
            AssertSample(samples, ReusePid, 150, Base + 0x40, null);
            AssertSample(samples, OtherPid, 150, Base + 0x40, null);

            // Program A, including its exact load time and its inclusive last byte, but not the byte after it.
            AssertSample(samples, ReusePid, 200, Base, "progA");
            AssertSample(samples, ReusePid, 300, Base + 0xff, "progA");
            AssertSample(samples, ReusePid, 300, Base + 0x100, null);

            // A is unloaded at 400: samples at and after the unload belong to the tombstone (an unknown gap).
            AssertSample(samples, ReusePid, 400, Base + 0x40, null, "vmlinux");
            AssertSample(samples, ReusePid, 500, Base + 0x40, null, "vmlinux");

            // Program B reuses the range from 600, including at its exact load time.
            AssertSample(samples, ReusePid, 600, Base + 0x40, "progB");

            // A's old tail beyond the end of B is still the tombstone, not A.
            AssertSample(samples, ReusePid, 700, Base + 0x80, null, "vmlinux");

            // Another process that only loaded A at the same addresses is unaffected.
            AssertSample(samples, OtherPid, 700, Base + 0x40, "progA");
            AssertSample(samples, OtherPid, 700, Base + 0x80, "progA");
        }

        private static void AssertSample(List<(int pid, long time, ulong address, string method, string module)> samples,
            int pid, long time, ulong address, string expectedMethod, string expectedModule = null)
        {
            var sample = samples.Find(s => s.pid == pid && s.time == time && s.address == address);
            Assert.True(sample.pid == pid, $"Missing sample pid={pid} time={time} address=0x{address:x}");
            Assert.True(expectedMethod == sample.method,
                $"pid={pid} time={time} address=0x{address:x}: expected method '{expectedMethod}', got '{sample.method}'");
            if (expectedModule != null)
            {
                Assert.True(expectedModule == sample.module,
                    $"pid={pid} time={time} address=0x{address:x}: expected module '{expectedModule}', got '{sample.module}'");
            }
        }

        /// <summary>
        /// Converts the nettrace to ETLX and returns the leaf frame of every cpu sample.  Times are in QPC
        /// ticks, which equal the timestamps written by <see cref="BuildNettrace"/>.
        /// </summary>
        private static List<(int pid, long time, ulong address, string method, string module)> GetLeafFrames(byte[] nettrace)
        {
            var result = new List<(int, long, ulong, string, string)>();
            using (MemoryStream nettraceStream = new MemoryStream(nettrace))
            {
                TraceEventDispatcher eventSource = new EventPipeEventSource(nettraceStream);
                using (MemoryStream etlxStream = new MemoryStream())
                {
                    TraceLog.CreateFromEventPipeEventSources(
                        eventSource,
                        new IOStreamStreamWriter(etlxStream, SerializationSettings.Default, leaveOpen: true),
                        null);

                    etlxStream.Position = 0;
                    using (TraceLog traceLog = new TraceLog(etlxStream))
                    {
                        foreach (TraceEvent data in traceLog.Events)
                        {
                            if (data.ProviderName != "Universal.Events")
                            {
                                continue;
                            }

                            TraceCallStack callStack = data.CallStack();
                            Assert.NotNull(callStack);
                            TraceCodeAddress codeAddress = callStack.CodeAddress;
                            string method = codeAddress.Method?.FullMethodName;
                            result.Add((data.ProcessID, data.TimeStampQPC, codeAddress.Address, method, codeAddress.ModuleName));
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Builds the scenario.  Process 10: A loaded at 200, unloaded (tombstone) at 400, B loaded at 600.
        /// Process 20: A loaded at 200.  Mappings and symbols share the same ids across processes, as they do
        /// for kernel mappings.
        /// </summary>
        private static byte[] BuildNettrace(bool mappingsBeforeSamplesAtSameTime)
        {
            var writer = new TimeScopedEventPipeWriter();
            writer.WriteHeadersWithNonZeroSyncTime();

            writer.WriteMetadataBlock(
                new EventMetadata(ProcessCreateMetadataId, "Universal.System", "ProcessCreate", ProcessCreateEventId) { ProviderId = UniversalSystemProviderGuid },
                new EventMetadata(ProcessMappingMetadataId, "Universal.System", "ProcessMapping", ProcessMappingEventId) { ProviderId = UniversalSystemProviderGuid },
                new EventMetadata(ProcessSymbolMetadataId, "Universal.System", "ProcessSymbol", ProcessSymbolEventId) { ProviderId = UniversalSystemProviderGuid },
                new EventMetadata(CpuSampleMetadataId, "Universal.Events", "cpu", CpuSampleEventId,
                    new MetadataParameter("Value", MetadataTypeCode.VarUInt)) { ProviderId = UniversalEventsProviderGuid });

            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(ReuseThreadIndex, 100, ReusePid);
                w.WriteThreadEntry(OtherThreadIndex, 200, OtherPid);
            });

            // One single-frame stack per sampled address; stack id N + 1 is the address at offsets[N].
            ulong[] offsets = { 0x0, 0x40, 0x80, 0xff, 0x100 };
            writer.WriteBlock(5 /* BlockKind.StackBlock */, w =>
            {
                w.Write(1);
                w.Write(offsets.Length);
                foreach (ulong offset in offsets)
                {
                    w.Write(8);
                    w.Write(Base + offset);
                }
            });
            int StackFor(ulong offset) => Array.IndexOf(offsets, offset) + 1;

            var sequenceNumbers = new Dictionary<long, int>();
            writer.WriteEventBlock(w =>
            {
                void Emit(long threadIndex, int metadataId, long timestamp, int stackId, Action<BinaryWriter> payload)
                {
                    sequenceNumbers.TryGetValue(threadIndex, out int sequenceNumber);
                    sequenceNumbers[threadIndex] = ++sequenceNumber;
                    w.WriteEventBlob(new WriteEventOptions
                    {
                        MetadataId = metadataId,
                        ThreadIndexOrId = threadIndex,
                        CaptureThreadIndexOrId = threadIndex,
                        SequenceNumber = sequenceNumber,
                        StackId = stackId,
                        Timestamp = timestamp,
                    }, payload);
                }

                void Sample(long threadIndex, long timestamp, ulong offset) =>
                    Emit(threadIndex, CpuSampleMetadataId, timestamp, StackFor(offset), bw => bw.WriteVarUInt(1));

                void Mapping(long threadIndex, long timestamp, ulong id, ulong lastOffset, string fileName) =>
                    Emit(threadIndex, ProcessMappingMetadataId, timestamp, 0, bw => WriteProcessMappingPayload(bw, id, Base, Base + lastOffset, fileName));

                void Symbol(long threadIndex, long timestamp, ulong id, ulong mappingId, ulong lastOffset, string name) =>
                    Emit(threadIndex, ProcessSymbolMetadataId, timestamp, 0, bw => WriteProcessSymbolPayload(bw, id, mappingId, Base, Base + lastOffset, name));

                // Emits the mapping (and its symbol, if any) and the samples at the same timestamp in the requested order.
                void AtSameTime(Action mapping, Action samples)
                {
                    if (mappingsBeforeSamplesAtSameTime)
                    {
                        mapping();
                        samples();
                    }
                    else
                    {
                        samples();
                        mapping();
                    }
                }

                Emit(ReuseThreadIndex, ProcessCreateMetadataId, 100, 0, bw => WriteProcessCreatePayload(bw, "reuse"));
                Emit(OtherThreadIndex, ProcessCreateMetadataId, 100, 0, bw => WriteProcessCreatePayload(bw, "other"));

                // Like real producers, map the kernel address space in process 20; BPF programs are mapped over parts
                // of it.  Process 10 has no mapping covering the programs before the first one is loaded.
                Emit(OtherThreadIndex, ProcessMappingMetadataId, 100, 0, bw => WriteProcessMappingPayload(bw, 100, KernelStart, ulong.MaxValue, "vmlinux"));

                Sample(ReuseThreadIndex, 150, 0x40);
                Sample(OtherThreadIndex, 150, 0x40);

                AtSameTime(
                    () =>
                    {
                        Mapping(ReuseThreadIndex, 200, 1, 0xff, "[bpf]");
                        Symbol(ReuseThreadIndex, 200, 1, 1, 0xff, "progA");
                        Mapping(OtherThreadIndex, 200, 1, 0xff, "[bpf]");
                    },
                    () => Sample(ReuseThreadIndex, 200, 0x0));

                Sample(ReuseThreadIndex, 300, 0xff);
                Sample(ReuseThreadIndex, 300, 0x100);

                AtSameTime(
                    () => Mapping(ReuseThreadIndex, 400, 2, 0xff, "vmlinux"),
                    () => Sample(ReuseThreadIndex, 400, 0x40));

                Sample(ReuseThreadIndex, 500, 0x40);

                AtSameTime(
                    () =>
                    {
                        Mapping(ReuseThreadIndex, 600, 3, 0x7f, "[bpf]");
                        Symbol(ReuseThreadIndex, 600, 2, 3, 0x7f, "progB");
                    },
                    () => Sample(ReuseThreadIndex, 600, 0x40));

                Sample(ReuseThreadIndex, 700, 0x80);
                Sample(OtherThreadIndex, 700, 0x40);
                Sample(OtherThreadIndex, 700, 0x80);
            });

            writer.WriteEndBlock();
            return writer.ToArray();
        }

        /// <summary>
        /// ProcessCreate payload: NamespaceId (VarUInt), Name (ushort-prefixed UTF8), NamespaceName (ushort-prefixed UTF8).
        /// </summary>
        private static void WriteProcessCreatePayload(BinaryWriter writer, string name)
        {
            writer.WriteVarUInt(0);
            WriteShortUTF8String(writer, name);
            WriteShortUTF8String(writer, "");
        }

        /// <summary>
        /// ProcessMapping payload: Id, StartAddress, EndAddress (inclusive), FileOffset (VarUInts),
        /// FileName (ushort-prefixed UTF8), MetadataId (VarUInt).
        /// </summary>
        private static void WriteProcessMappingPayload(BinaryWriter writer, ulong id, ulong startAddress, ulong endAddress, string fileName)
        {
            writer.WriteVarUInt(id);
            writer.WriteVarUInt(startAddress);
            writer.WriteVarUInt(endAddress);
            writer.WriteVarUInt(0);
            WriteShortUTF8String(writer, fileName);
            writer.WriteVarUInt(0);
        }

        /// <summary>
        /// ProcessSymbol payload: Id, MappingId, StartAddress, EndAddress (inclusive) (VarUInts), Name (ushort-prefixed UTF8).
        /// </summary>
        private static void WriteProcessSymbolPayload(BinaryWriter writer, ulong id, ulong mappingId, ulong startAddress, ulong endAddress, string name)
        {
            writer.WriteVarUInt(id);
            writer.WriteVarUInt(mappingId);
            writer.WriteVarUInt(startAddress);
            writer.WriteVarUInt(endAddress);
            WriteShortUTF8String(writer, name);
        }

        private static void WriteShortUTF8String(BinaryWriter writer, string value)
        {
            byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(value);
            writer.Write((ushort)utf8.Length);
            writer.Write(utf8);
        }

        /// <summary>
        /// V6 nettrace writer with a non-zero syncTimeQPC and a QPC frequency of 1000 so that relative times
        /// can be computed without tripping assertions.
        /// </summary>
        private sealed class TimeScopedEventPipeWriter : EventPipeWriterV6
        {
            public void WriteHeadersWithNonZeroSyncTime()
            {
                _writer.WriteNetTraceHeaderV6OrGreater(6, 0);
                _writer.WriteBlockV6OrGreater(1 /* BlockKind.Trace */, w =>
                {
                    DateTime now = new DateTime(2025, 2, 3, 4, 5, 6);
                    w.Write((short)now.Year);
                    w.Write((short)now.Month);
                    w.Write((short)now.DayOfWeek);
                    w.Write((short)now.Day);
                    w.Write((short)now.Hour);
                    w.Write((short)now.Minute);
                    w.Write((short)now.Second);
                    w.Write((short)now.Millisecond);
                    w.Write((long)1);           // syncTimeQPC
                    w.Write((long)1000);        // qpcFreq
                    w.Write(8);                 // pointer size
                    w.Write(0);                 // key/value pair count
                });
            }
        }
    }
}
