using System;
using System.Collections.Generic;
using System.IO;
using FastSerialization;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.EventPipe;
using Microsoft.Diagnostics.Tracing.Stacks;
using Xunit;
using Xunit.Abstractions;

namespace TraceEventTests
{
    /// <summary>
    /// Regression tests for broken-stack detection on musl-based Linux distributions (e.g. Alpine).
    ///
    /// On musl, libc and the dynamic loader are combined into a single module named like
    /// "ld-musl-x86_64.so.1". Threads start from there, so a callstack rooted in that module is
    /// complete and should NOT be reported as BROKEN. Before the fix, TraceEventStackSource only
    /// recognized glibc's "libc" as a valid thread-start module, so a large fraction of musl
    /// stacks were incorrectly marked BROKEN.
    ///
    /// These tests synthesize a tiny in-memory nettrace (no large trace file checked into the repo)
    /// and verify that a musl-rooted stack is not broken while a stack rooted in an ordinary
    /// library still is.
    /// </summary>
    public class MuslBrokenStackTests : TestBase
    {
        private static readonly Guid UniversalSystemProviderGuid = new Guid("8c107b6c-79f8-5231-4de6-2a0e20a3f562");
        private static readonly Guid UniversalEventsProviderGuid = new Guid("bc5e5d63-9799-5873-33d9-fba8316cef71");

        // Nettrace V6 metadata IDs for the events we'll emit.
        private const int ProcessCreateMetadataId = 1;
        private const int ProcessMappingMetadataId = 2;
        private const int CpuSampleMetadataId = 3;

        // Universal.System event IDs.
        private const int ProcessCreateEventId = 1;
        private const int ProcessMappingEventId = 3;

        // Universal.Events "cpu" event ID (the dynamic parser matches on provider+event name, so the
        // exact numeric value is unimportant; any non-zero value works).
        private const int CpuSampleEventId = 1;

        // The process / thread the synthesized samples belong to.
        private const int ProcessId = 1234;
        private const int ThreadId = 100;
        private const long ThreadIndex = 1;

        // Module address ranges. The musl loader maps over [0x10000, 0x20000); an ordinary library
        // maps over [0x20000, 0x30000).
        private const ulong MuslStart = 0x10000;
        private const ulong MuslEnd = 0x20000;
        private const ulong OtherStart = 0x20000;
        private const ulong OtherEnd = 0x30000;

        public MuslBrokenStackTests(ITestOutputHelper output)
            : base(output)
        {
        }

        [Theory]
        [InlineData("ld-musl-x86_64.so.1")]
        [InlineData("ld-musl-aarch64.so.1")]
        public void MuslRootedStack_IsNotBroken(string muslModuleName)
        {
            byte[] nettrace = BuildNettrace(muslModuleName);

            List<List<string>> stacks = GetSampleStacks(nettrace);

            // Among all samples, the musl-rooted cpu sample is the one whose stack contains a frame
            // in the musl loader module; the "ordinary" cpu sample contains a mylib frame but no musl
            // frame. (Other events such as ProcessCreate/ProcessMapping have no real call stack.)
            List<string> muslStack = stacks.Find(s => ContainsFrame(s, "ld-musl"));
            List<string> otherStack = stacks.Find(s => ContainsFrame(s, "mylib") && !ContainsFrame(s, "ld-musl"));

            Assert.NotNull(muslStack);
            Assert.NotNull(otherStack);

            // The fix: a stack rooted in the musl loader module is complete, so it must not be broken.
            Assert.False(ContainsFrame(muslStack, "BROKEN"));

            // Sanity check that broken detection still works: a stack rooted in an ordinary library
            // (not a recognized thread-start module) is still reported as broken.
            Assert.True(ContainsFrame(otherStack, "BROKEN"));
        }

        private static bool ContainsFrame(List<string> frames, string substring)
        {
            return frames.Exists(f => f.IndexOf(substring, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        /// <summary>
        /// Converts the nettrace to an ETLX TraceLog, builds a TraceEventStackSource over it, and
        /// returns the list of frame-name lists (leaf-to-root) for each sample that has a stack.
        /// </summary>
        private List<List<string>> GetSampleStacks(byte[] nettrace)
        {
            var result = new List<List<string>>();

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
                        var stackSource = new TraceEventStackSource(traceLog.Events);
                        stackSource.ForEach(sample =>
                        {
                            if (sample.StackIndex == StackSourceCallStackIndex.Invalid)
                            {
                                return;
                            }

                            var frames = new List<string>();
                            StackSourceCallStackIndex callStackIndex = sample.StackIndex;
                            while (callStackIndex != StackSourceCallStackIndex.Invalid)
                            {
                                StackSourceFrameIndex frameIndex = stackSource.GetFrameIndex(callStackIndex);
                                frames.Add(stackSource.GetFrameName(frameIndex, verboseName: false));
                                callStackIndex = stackSource.GetCallerIndex(callStackIndex);
                            }

                            result.Add(frames);
                        });
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Builds a minimal V6 nettrace with one process, one thread, a musl loader module, an
        /// ordinary library module, and two cpu samples: one rooted in the musl module and one
        /// rooted in the ordinary library.
        /// </summary>
        private static byte[] BuildNettrace(string muslModuleName)
        {
            var writer = new MuslEventPipeWriter();
            writer.WriteHeadersWithNonZeroSyncTime();

            var processCreateMeta = new EventMetadata(ProcessCreateMetadataId, "Universal.System", "ProcessCreate", ProcessCreateEventId)
            {
                ProviderId = UniversalSystemProviderGuid
            };
            var mappingMeta = new EventMetadata(ProcessMappingMetadataId, "Universal.System", "ProcessMapping", ProcessMappingEventId)
            {
                ProviderId = UniversalSystemProviderGuid
            };
            var cpuMeta = new EventMetadata(CpuSampleMetadataId, "Universal.Events", "cpu", CpuSampleEventId,
                new MetadataParameter("Value", MetadataTypeCode.VarUInt))
            {
                ProviderId = UniversalEventsProviderGuid
            };

            writer.WriteMetadataBlock(processCreateMeta, mappingMeta, cpuMeta);

            // One thread (index 1) belonging to process 1234.
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(ThreadIndex, ThreadId, ProcessId);
            });

            // Two stacks, encoded leaf-first / root-last (matching the order read by
            // GetStackIndexForStackEvent64). Each address is an 8-byte little-endian value.
            //   Stack 1: leaf in the ordinary library, root in the musl loader  -> must NOT be broken.
            //   Stack 2: leaf and root both in the ordinary library             -> still broken.
            writer.WriteBlock(5 /* BlockKind.StackBlock */, w =>
            {
                w.Write(1);   // firstStackId
                w.Write(2);   // countStackIds

                WriteStack(w, OtherStart + 0x100, MuslStart + 0x100);
                WriteStack(w, OtherStart + 0x180, OtherStart + 0x900);
            });

            writer.WriteEventBlock(w =>
            {
                // Establish the process with a distinctive name that does not collide with any module.
                w.WriteEventBlob(
                    EventOptions(ProcessCreateMetadataId, sequenceNumber: 1, timestamp: 100, stackId: 0),
                    bw => WriteProcessCreatePayload(bw, name: "testhost"));

                // Map the musl loader module and an ordinary library.
                w.WriteEventBlob(
                    EventOptions(ProcessMappingMetadataId, sequenceNumber: 2, timestamp: 200, stackId: 0),
                    bw => WriteProcessMappingPayload(bw, id: 1, startAddress: MuslStart, endAddress: MuslEnd, fileName: muslModuleName));
                w.WriteEventBlob(
                    EventOptions(ProcessMappingMetadataId, sequenceNumber: 3, timestamp: 201, stackId: 0),
                    bw => WriteProcessMappingPayload(bw, id: 2, startAddress: OtherStart, endAddress: OtherEnd, fileName: "mylib.so"));

                // Two cpu samples, referencing the two stacks defined above.
                w.WriteEventBlob(
                    EventOptions(CpuSampleMetadataId, sequenceNumber: 4, timestamp: 1000, stackId: 1),
                    bw => bw.WriteVarUInt(1));
                w.WriteEventBlob(
                    EventOptions(CpuSampleMetadataId, sequenceNumber: 5, timestamp: 1001, stackId: 2),
                    bw => bw.WriteVarUInt(1));
            });

            writer.WriteEndBlock();
            return writer.ToArray();
        }

        private static WriteEventOptions EventOptions(int metadataId, int sequenceNumber, long timestamp, int stackId)
        {
            return new WriteEventOptions
            {
                MetadataId = metadataId,
                ThreadIndexOrId = ThreadIndex,
                CaptureThreadIndexOrId = ThreadIndex,
                SequenceNumber = sequenceNumber,
                StackId = stackId,
                Timestamp = timestamp,
            };
        }

        private static void WriteStack(BinaryWriter writer, params ulong[] addresses)
        {
            writer.Write(addresses.Length * 8);   // stackBytesSize
            foreach (ulong address in addresses)
            {
                writer.Write(address);
            }
        }

        /// <summary>
        /// ProcessCreate payload: NamespaceId (VarUInt), Name (ushort-prefixed UTF8), NamespaceName (ushort-prefixed UTF8).
        /// </summary>
        private static void WriteProcessCreatePayload(BinaryWriter writer, string name)
        {
            writer.WriteVarUInt(0);              // NamespaceId
            WriteShortUTF8String(writer, name);  // Name
            WriteShortUTF8String(writer, "");    // NamespaceName
        }

        /// <summary>
        /// ProcessMapping payload: Id (VarUInt), StartAddress (VarUInt), EndAddress (VarUInt),
        /// FileOffset (VarUInt), FileName (ushort-prefixed UTF8), MetadataId (VarUInt).
        /// </summary>
        private static void WriteProcessMappingPayload(BinaryWriter writer, ulong id, ulong startAddress, ulong endAddress, string fileName)
        {
            writer.WriteVarUInt(id);
            writer.WriteVarUInt(startAddress);
            writer.WriteVarUInt(endAddress);
            writer.WriteVarUInt(0);                 // FileOffset
            WriteShortUTF8String(writer, fileName);
            writer.WriteVarUInt(0);                 // MetadataId (none)
        }

        private static void WriteShortUTF8String(BinaryWriter writer, string value)
        {
            byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(value);
            writer.Write((ushort)utf8.Length);
            writer.Write(utf8);
        }

        /// <summary>
        /// V6 nettrace writer that emits a non-zero syncTimeQPC in the trace block. A zero
        /// syncTimeQPC (the default used by the shared test writer) trips a Debug.Assert in
        /// TraceEventSource.QPCTimeToRelMSec when relative timestamps are computed while iterating
        /// events, which these tests do.
        /// </summary>
        private sealed class MuslEventPipeWriter : EventPipeWriterV6
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
                    w.Write((long)1);           // syncTimeQPC (non-zero, below all event timestamps)
                    w.Write((long)1000);        // qpcFreq
                    w.Write(8);                 // pointer size
                    w.Write(0);                 // key/value pair count
                });
            }
        }
    }
}
