using System;
using System.Collections.Generic;
using System.IO;
using FastSerialization;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.EventPipe;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.SourceConverters;
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

        /// <summary>
        /// Explicit unloads remove only the named mapping's intersection, preserving overlays and surviving pieces.
        /// </summary>
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ExplicitUnloadsPreserveLivePiecesAndProcessIsolation(bool mappingsBeforeSamplesAtSameTime)
        {
            var samples = GetLeafFrames(BuildLifecycleNettrace(mappingsBeforeSamplesAtSameTime));
            AssertSample(samples, ReusePid, 300, Base, "A");
            AssertSample(samples, ReusePid, 300, Base + 0x40, "B");
            AssertSample(samples, ReusePid, 300, Base + 0x80, "A");
            AssertSample(samples, ReusePid, 399, Base + 0x50, "B");
            AssertSample(samples, ReusePid, 400, Base + 0x4f, "B");
            AssertSample(samples, ReusePid, 400, Base + 0x50, "A");
            AssertSample(samples, ReusePid, 400, Base + 0x5f, "A");
            AssertSample(samples, ReusePid, 400, Base + 0x60, "B");
            AssertSample(samples, ReusePid, 450, Base, null, "");
            AssertSample(samples, ReusePid, 450, Base + 0x40, "B");
            AssertSample(samples, ReusePid, 450, Base + 0x50, null, "");
            AssertSample(samples, ReusePid, 500, Base + 0x40, null, "");
            AssertSample(samples, ReusePid, 600, Base + 0x40, "C");
            AssertSample(samples, ReusePid, 650, Base + 0x40, "replacement");
            AssertSample(samples, ReusePid, 700, Base + 0x40, null, "");
            AssertSample(samples, ReusePid, 800, Base + 0x40, "D");
            AssertSample(samples, ReusePid, 850, Base + 0x40, null, "");
            AssertSample(samples, ReusePid, 950, Base + 0x40, "new-process");
            AssertSample(samples, OtherPid, 950, Base + 0x40, "other-process");
            AssertSample(samples, 30, 400, ulong.MaxValue - 1, "last-byte");
            AssertSample(samples, 30, 399, ulong.MaxValue, "last-byte");
            AssertSample(samples, 30, 400, ulong.MaxValue, null, "");
        }

        /// <summary>
        /// Readers that ignore stable event ID 6 keep mappings and can incorrectly retain unloaded symbols.
        /// </summary>
        [Fact]
        public void IgnoringUnloadEventsRetainsStaleSymbols()
        {
            var samples = GetLeafFrames(BuildLifecycleNettrace(true, includeUnloads: false));
            AssertSample(samples, ReusePid, 500, Base + 0x40, "B");
            AssertSample(samples, ReusePid, 700, Base + 0x40, "replacement");
        }

        /// <summary>
        /// The stable unload template exposes its three varints and process header to raw readers.
        /// </summary>
        [Fact]
        public void MappingUnloadPayloadIsAvailableToRawReaders()
        {
            using (var stream = new MemoryStream(BuildLifecycleNettrace(true)))
            using (var source = new EventPipeEventSource(stream))
            {
                var parser = new UniversalSystemTraceEventParser(source);
                var unloads = new List<(int pid, ulong id, ulong start, ulong end)>();
                parser.ProcessMappingUnload += data =>
                {
                    Assert.Equal(6, (int)data.ID);
                    Assert.Equal(new[] { "Id", "StartAddress", "EndAddress" }, data.PayloadNames);
                    Assert.Equal(data.Id, data.PayloadValue(0));
                    Assert.Equal(data.StartAddress, data.PayloadValue(1));
                    Assert.Equal(data.EndAddress, data.PayloadValue(2));
                    unloads.Add((data.ProcessID, data.Id, data.StartAddress, data.EndAddress));
                };
                source.Process();
                Assert.Equal(8, unloads.Count);
                Assert.Contains((ReusePid, 2UL, Base + 0x50, Base + 0x5f), unloads);
                Assert.Contains((30, 1UL, ulong.MaxValue, ulong.MaxValue), unloads);
            }
        }

        [Fact]
        public void MappingUnloadHandlerCanBeRemoved()
        {
            using (var stream = new MemoryStream(BuildLifecycleNettrace(true)))
            using (var source = new EventPipeEventSource(stream))
            {
                var parser = new UniversalSystemTraceEventParser(source);
                int count = 0;
                Action<Microsoft.Diagnostics.Tracing.Parsers.Universal.Events.ProcessMappingUnloadTraceData> handler = _ => count++;
                parser.ProcessMappingUnload += handler;
                parser.ProcessMappingUnload -= handler;
                source.Process();
                Assert.Equal(0, count);
            }
        }

        [Fact]
        public void DelayedSymbolUsesItsMappingModule()
        {
            var samples = GetLeafFrames(BuildLifecycleNettrace(true, distinctModules: true));
            AssertSample(samples, ReusePid, 299, Base + 0x40, "A", "old.so");
            AssertSample(samples, ReusePid, 300, Base + 0x40, "B", "new.so");
        }

        [Fact]
        public void ProcessExitPreservesDeferredClrSymbolsOutsideUniversalMappings()
        {
            byte[] trace = BuildLifecycleNettrace(true, includeClrMethod: true);
            int methodEvents = 0;
            using (var stream = new MemoryStream(trace))
            using (var source = new EventPipeEventSource(stream))
            {
                source.Clr.MethodLoadVerbose += _ => methodEvents++;
                source.Process();
            }
            Assert.Equal(1, methodEvents);
            var samples = GetLeafFrames(trace);
            var sample = samples.Find(s => s.pid == ReusePid && s.time == 799 && s.address == 0x12345000);
            Assert.Equal(ReusePid, sample.pid);
            Assert.NotNull(sample.method);
            Assert.Contains("ClrMethod", sample.method);
        }

        [Fact]
        public void TracksRecentStacksForNonEventPipeSources()
        {
            using (var stream = new MemoryStream(BuildLifecycleNettrace(true)))
            using (var etlx = new MemoryStream())
            {
                TraceLog.CreateFromEventPipeEventSources(new EventPipeEventSource(stream),
                    new IOStreamStreamWriter(etlx, SerializationSettings.Default, leaveOpen: true), null);
                etlx.Position = 0;
                using (var log = new TraceLog(etlx))
                using (var source = log.Events.GetSource())
                {
                    var converter = new NettraceUniversalConverter();
                    converter.BeforeProcess(log, source);
                    var process = log.Processes.GetProcess(ReusePid, 299);
                    converter.OnStackAddedToEvent(process, (EventIndex)1, 300, (CallStackIndex)1);
                    var states = (System.Collections.IDictionary)converter.GetType()
                        .GetField("_mappingStates", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        .GetValue(converter);
                    Assert.True(states.Contains(process));
                    var stacks = (System.Collections.ICollection)states[process].GetType().GetField("RecentStacks").GetValue(states[process]);
                    Assert.Single(stacks);
                }
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ModuleQueriesPreserveMappingLifetimesAfterEtlxRoundTrip(bool mappingFirst)
        {
            using (var stream = new MemoryStream(BuildLifecycleNettrace(mappingFirst, distinctModules: true)))
            using (var etlx = new MemoryStream())
            {
                TraceLog.CreateFromEventPipeEventSources(new EventPipeEventSource(stream),
                    new IOStreamStreamWriter(etlx, SerializationSettings.Default, leaveOpen: true), null);
                etlx.Position = 0;
                using (var log = new TraceLog(etlx))
                {
                    var process = log.Processes.GetProcess(ReusePid, 299);
                    Assert.NotNull(process);
                    var modules = process.LoadedModules;
                    Assert.Equal("old.so", modules.GetModuleContainingAddress(Base + 0x40, 298).FilePath);
                    Assert.Equal("new.so", modules.GetModuleContainingAddress(Base + 0x40, 299).FilePath);
                    Assert.NotNull(modules.GetLoadedModule("old.so", 299.0));
                    Assert.Equal("new.so", modules.GetModuleContainingAddress(Base + 0x4f, 399).FilePath);
                    Assert.Equal("old.so", modules.GetModuleContainingAddress(Base + 0x50, 399).FilePath);
                    Assert.Equal("old.so", modules.GetModuleContainingAddress(Base + 0x5f, 399).FilePath);
                    Assert.Equal("new.so", modules.GetModuleContainingAddress(Base + 0x60, 399).FilePath);
                    Assert.Equal("old.so", modules.GetModuleContainingAddress(Base + 0xff, 399).FilePath);
                    Assert.Null(modules.GetModuleContainingAddress(Base + 0x100, 399));
                    Assert.NotNull(modules.GetLoadedModule("new.so", 399.0));
                    Assert.Equal(449, modules.GetLoadedModule("old.so", 399.0).UnloadTimeRelativeMSec);
                    Assert.Equal(499, modules.GetLoadedModule("new.so", 399.0).UnloadTimeRelativeMSec);
                    Assert.Null(modules.GetLoadedModule("old.so", 449.0));
                    Assert.Null(modules.GetModuleContainingAddress(Base + 0x50, 449));
                    Assert.Equal("new.so", modules.GetModuleContainingAddress(Base + 0x40, 449).FilePath);
                    Assert.Null(modules.GetModuleContainingAddress(Base + 0x40, 499));
                    Assert.Null(modules.GetLoadedModule("new.so", 499.0));
                    Assert.Null(modules.GetModuleContainingAddress(Base + 0x40, 599));
                    Assert.Null(modules.GetLoadedModule("UNKNOWN", 599.0));
                    Assert.Equal(649, modules.GetModuleContainingAddress(Base + 0x40, 649).LoadTimeRelativeMSec);
                    Assert.Equal(749, modules.GetModuleContainingAddress(Base + 0x40, 749).LoadTimeRelativeMSec);
                    Assert.NotNull(modules.GetModuleContainingAddress(Base + 0x40, 799));
                    Assert.Null(modules.GetModuleContainingAddress(Base + 0x40, 800));
                    Assert.NotNull(log.Processes.GetProcess(OtherPid, 699).LoadedModules.GetModuleContainingAddress(Base + 0x40, 699));
                    Assert.Null(log.Processes.GetProcess(OtherPid, 699).LoadedModules.GetModuleContainingAddress(Base + 0x40, 1000));
                    var last = log.Processes.GetProcess(30, 398).LoadedModules;
                    Assert.NotNull(last.GetModuleContainingAddress(ulong.MaxValue, 398));
                    Assert.Null(last.GetModuleContainingAddress(ulong.MaxValue, 399));
                    Assert.NotNull(last.GetModuleContainingAddress(ulong.MaxValue - 1, 399));
                }
            }
        }

        [Fact]
        public void SymbolsDoNotLeakAcrossProcessInstances()
        {
            var samples = GetLeafFrames(BuildLifecycleNettrace(true, includeNewProcessSymbols: false));
            AssertSample(samples, 40, 400, Base + 0x40, null);
            AssertSample(samples, ReusePid, 950, Base + 0x40, null);
            AssertSample(samples, OtherPid, 950, Base + 0x40, "other-process");
        }

        private static byte[] BuildLifecycleNettrace(bool mappingFirst, bool includeUnloads = true, bool includeNewProcessSymbols = true,
            bool distinctModules = false, bool includeClrMethod = false)
        {
            const int unloadMetadataId = 5;
            const int exitMetadataId = 6;
            const int clrMethodMetadataId = 7;
            var writer = new TimeScopedEventPipeWriter();
            writer.WriteHeadersWithNonZeroSyncTime();
            writer.WriteMetadataBlock(
                new EventMetadata(ProcessCreateMetadataId, "Universal.System", "ProcessCreate", 1) { ProviderId = UniversalSystemProviderGuid },
                new EventMetadata(ProcessMappingMetadataId, "Universal.System", "ProcessMapping", 3) { ProviderId = UniversalSystemProviderGuid },
                new EventMetadata(ProcessSymbolMetadataId, "Universal.System", "ProcessSymbol", 4) { ProviderId = UniversalSystemProviderGuid },
                new EventMetadata(unloadMetadataId, "Universal.System", "ProcessMappingUnload", 6) { ProviderId = UniversalSystemProviderGuid },
                new EventMetadata(exitMetadataId, "Universal.System", "ProcessExit", 2) { ProviderId = UniversalSystemProviderGuid },
                new EventMetadata(clrMethodMetadataId, "Microsoft-Windows-DotNETRuntime", "MethodLoadVerbose", 143) { ProviderId = ClrTraceEventParser.ProviderGuid },
                new EventMetadata(CpuSampleMetadataId, "Universal.Events", "cpu", 1,
                    new MetadataParameter("Value", MetadataTypeCode.VarUInt)) { ProviderId = UniversalEventsProviderGuid });
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(1, 100, ReusePid);
                w.WriteThreadEntry(2, 200, OtherPid);
                w.WriteThreadEntry(3, 300, 30);
                w.WriteThreadEntry(4, 101, ReusePid);
                w.WriteThreadEntry(5, 400, 40);
            });
            ulong[] addresses = { Base, Base + 0x40, Base + 0x4f, Base + 0x50, Base + 0x5f, Base + 0x60, Base + 0x80, ulong.MaxValue - 1, ulong.MaxValue, 0x12345000 };
            writer.WriteBlock(5, w =>
            {
                w.Write(1);
                w.Write(addresses.Length);
                foreach (ulong address in addresses)
                {
                    w.Write(8);
                    w.Write(address);
                }
            });
            var sequenceNumbers = new Dictionary<long, int>();
            writer.WriteEventBlock(w =>
            {
                void Emit(long thread, int metadata, long time, int stack, Action<BinaryWriter> payload)
                {
                    sequenceNumbers.TryGetValue(thread, out int sequence);
                    sequenceNumbers[thread] = ++sequence;
                    w.WriteEventBlob(new WriteEventOptions
                    {
                        MetadataId = metadata, ThreadIndexOrId = thread, CaptureThreadIndexOrId = thread,
                        SequenceNumber = sequence, StackId = stack, Timestamp = time,
                    }, payload);
                }
                void Sample(long thread, long time, params ulong[] values)
                {
                    foreach (ulong value in values)
                    {
                        Emit(thread, CpuSampleMetadataId, time, Array.IndexOf(addresses, value) + 1, bw => bw.WriteVarUInt(1));
                    }
                }
                void Map(long thread, long time, ulong id, ulong start, ulong end, string name)
                {
                    string fileName = distinctModules && thread == 1 && id == 1 ? "old.so" :
                        distinctModules && thread == 1 && id == 2 ? "new.so" :
                        distinctModules && thread == 1 && id == 3 ? "/memfd:doublemapper" : "[bpf]";
                    Emit(thread, ProcessMappingMetadataId, time, 0, bw => WriteProcessMappingPayload(bw, id, start, end, fileName));
                    if (name != null && !(distinctModules && thread == 1 && id == 1))
                    {
                        Emit(thread, ProcessSymbolMetadataId, time, 0, bw => WriteProcessSymbolPayload(bw, id, id, start, end, name));
                    }
                }
                void Unload(long thread, long time, ulong id, ulong start, ulong end)
                {
                    if (includeUnloads)
                    {
                        Emit(thread, unloadMetadataId, time, 0, bw =>
                        {
                            bw.WriteVarUInt(id);
                            bw.WriteVarUInt(start);
                            bw.WriteVarUInt(end);
                        });
                    }
                }
                void AtTime(Action change, Action sample)
                {
                    if (mappingFirst) { change(); sample(); }
                    else { sample(); change(); }
                }

                Emit(1, ProcessCreateMetadataId, 100, 0, bw => WriteProcessCreatePayload(bw, "reuse"));
                Emit(2, ProcessCreateMetadataId, 100, 0, bw => WriteProcessCreatePayload(bw, "other"));
                Emit(3, ProcessCreateMetadataId, 100, 0, bw => WriteProcessCreatePayload(bw, "last"));
                Emit(5, ProcessCreateMetadataId, 100, 0, bw => WriteProcessCreatePayload(bw, "no-symbol"));
                Map(1, 200, 1, Base, Base + 0xff, "A");
                Map(2, 200, 1, Base, Base + 0xff, "other-process");
                Map(3, 200, 1, ulong.MaxValue - 0xff, ulong.MaxValue, "last-byte");
                Map(5, 200, 1, Base, Base + 0xff, null);
                if (distinctModules)
                {
                    Sample(1, 299, Base + 0x40);
                }
                AtTime(() => Map(1, 300, 2, Base + 0x40, Base + 0x7f, "B"),
                    () => Sample(1, 300, Base, Base + 0x40, Base + 0x80));
                if (distinctModules)
                {
                    Emit(1, ProcessSymbolMetadataId, 301, 0, bw => WriteProcessSymbolPayload(bw, 1, 1, Base, Base + 0xff, "A"));
                }
                Unload(1, 350, 999, 0, ulong.MaxValue);
                Unload(1, 350, 2, Base + 0x80, Base + 0xff);
                Sample(1, 399, Base + 0x50);
                Sample(3, 399, ulong.MaxValue);
                Sample(5, 400, Base + 0x40);
                AtTime(() =>
                    {
                        Unload(1, 400, 2, Base + 0x50, Base + 0x5f);
                        Unload(3, 400, 1, ulong.MaxValue, ulong.MaxValue);
                    },
                    () =>
                    {
                        Sample(1, 400, Base + 0x4f, Base + 0x50, Base + 0x5f, Base + 0x60);
                        Sample(3, 400, ulong.MaxValue - 1, ulong.MaxValue);
                    });
                AtTime(() => Unload(1, 450, 1, 0, ulong.MaxValue),
                    () => Sample(1, 450, Base, Base + 0x40, Base + 0x50));
                AtTime(() => Unload(1, 500, 2, 0, ulong.MaxValue),
                    () => Sample(1, 500, Base + 0x40));
                AtTime(() => Map(1, 600, 3, Base, Base + 0xff, "C"),
                    () => Sample(1, 600, Base + 0x40));
                AtTime(() =>
                    {
                        Unload(1, 650, 3, Base, Base + 0xff);
                        Map(1, 650, 5, Base, Base + 0xff, "replacement");
                    },
                    () => Sample(1, 650, Base + 0x40));
                AtTime(() => Unload(1, 700, 5, Base, Base + 0xff),
                    () => Sample(1, 700, Base + 0x40));
                Map(1, 750, 4, Base, Base + 0xff, "D");
                if (includeClrMethod)
                {
                    Sample(1, 799, 0x12345000);
                    Emit(1, clrMethodMetadataId, 799, 0, bw =>
                    {
                        bw.Write((long)1); bw.Write((long)2); bw.Write((long)0x12345000);
                        bw.Write(64); bw.Write(0); bw.Write((int)Microsoft.Diagnostics.Tracing.Parsers.Clr.MethodFlags.Jitted);
                        bw.Write(System.Text.Encoding.Unicode.GetBytes("Example\0ClrMethod\0()\0"));
                    });
                }
                Sample(1, 800, Base + 0x40);
                Emit(1, exitMetadataId, 800, 0, bw => { });
                Sample(1, 850, Base + 0x40);
                Emit(4, ProcessCreateMetadataId, 900, 0, bw => WriteProcessCreatePayload(bw, "new"));
                Map(4, 900, 1, Base, Base + 0xff, includeNewProcessSymbols ? "new-process" : null);
                Sample(4, 950, Base + 0x40);
                Sample(2, 950, Base + 0x40);
            });
            writer.WriteEndBlock();
            return writer.ToArray();
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
                        Symbol(OtherThreadIndex, 200, 1, 1, 0xff, "progA");
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
