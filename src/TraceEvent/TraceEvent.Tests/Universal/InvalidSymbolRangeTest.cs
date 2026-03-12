using System;
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
    /// Regression tests for https://github.com/microsoft/perfview/issues/2373
    /// Verifies that ProcessSymbol events with invalid address ranges don't crash ETLX conversion.
    /// </summary>
    public class InvalidSymbolRangeTest : TestBase
    {
        private static readonly Guid UniversalSystemProviderGuid = new Guid("8c107b6c-79f8-5231-4de6-2a0e20a3f562");

        // Nettrace V6 metadata IDs for the events we'll emit.
        private const int ProcessMappingMetadataId = 1;
        private const int ProcessSymbolMetadataId = 2;

        // Universal.System event IDs.
        private const int ProcessMappingEventId = 3;
        private const int ProcessSymbolEventId = 4;

        public InvalidSymbolRangeTest(ITestOutputHelper output)
            : base(output)
        {
        }

        /// <summary>
        /// When a ProcessSymbol has StartAddress=0 and EndAddress=0xFFFFFFFFFFFFFFFF (e.g., from
        /// zeroed /proc/kallsyms on Linux without root), ETLX conversion must not throw.
        /// </summary>
        [Fact]
        public void ConvertNettrace_WithInvalidSymbolRange_DoesNotThrow()
        {
            byte[] nettrace = BuildNettraceWithSymbol(
                symbolStartAddress: 0,
                symbolEndAddress: ulong.MaxValue,
                symbolName: "startup_64");

            ConvertAndVerify(nettrace);
        }

        /// <summary>
        /// A zero-size symbol range (StartAddress == EndAddress) should also be skipped gracefully.
        /// </summary>
        [Fact]
        public void ConvertNettrace_WithZeroSizeSymbolRange_DoesNotThrow()
        {
            byte[] nettrace = BuildNettraceWithSymbol(
                symbolStartAddress: 0x1000,
                symbolEndAddress: 0x1000,
                symbolName: "empty_symbol");

            ConvertAndVerify(nettrace);
        }

        /// <summary>
        /// Builds a minimal nettrace (V6) containing one ProcessMapping and one ProcessSymbol with the given addresses.
        /// </summary>
        private static byte[] BuildNettraceWithSymbol(ulong symbolStartAddress, ulong symbolEndAddress, string symbolName)
        {
            var writer = new EventPipeWriterV6();
            writer.WriteHeaders();

            // Define metadata for Universal.System ProcessMapping (eventId=3) and ProcessSymbol (eventId=4).
            var mappingMeta = new EventMetadata(ProcessMappingMetadataId, "Universal.System", "ProcessMapping", ProcessMappingEventId);
            mappingMeta.ProviderId = UniversalSystemProviderGuid;

            var symbolMeta = new EventMetadata(ProcessSymbolMetadataId, "Universal.System", "ProcessSymbol", ProcessSymbolEventId);
            symbolMeta.ProviderId = UniversalSystemProviderGuid;

            writer.WriteMetadataBlock(mappingMeta, symbolMeta);

            // Thread entry: thread index 1, OS thread ID 100, process ID 1234.
            writer.WriteThreadBlock(w =>
            {
                w.WriteThreadEntry(1, 100, 1234);
            });

            // Emit one ProcessMapping (valid range) and one ProcessSymbol (potentially invalid range).
            writer.WriteEventBlock(w =>
            {
                // ProcessMapping payload: Id=1, StartAddress=0x7F000000, EndAddress=0x7F001000, FileOffset=0, FileName="test.so", MetadataId=0
                w.WriteEventBlob(ProcessMappingMetadataId, 1, 1, WriteProcessMappingPayload(
                    id: 1,
                    startAddress: 0x7F000000,
                    endAddress: 0x7F001000,
                    fileOffset: 0,
                    fileName: "test.so",
                    metadataId: 0));

                // ProcessSymbol payload referencing mapping 1 with the caller-specified address range.
                w.WriteEventBlob(ProcessSymbolMetadataId, 1, 2, WriteProcessSymbolPayload(
                    id: 1,
                    mappingId: 1,
                    startAddress: symbolStartAddress,
                    endAddress: symbolEndAddress,
                    name: symbolName));
            });

            writer.WriteEndBlock();
            return writer.ToArray();
        }

        private static void ConvertAndVerify(byte[] nettrace)
        {
            using (MemoryStream nettraceStream = new MemoryStream(nettrace))
            {
                TraceEventDispatcher eventSource = new EventPipeEventSource(nettraceStream);

                using (MemoryStream etlxStream = new MemoryStream())
                {
                    // This must not throw (previously threw NullReferenceException).
                    TraceLog.CreateFromEventPipeEventSources(
                        eventSource,
                        new IOStreamStreamWriter(etlxStream, SerializationSettings.Default, leaveOpen: true),
                        null);

                    etlxStream.Position = 0;
                    using (new TraceLog(etlxStream))
                    {
                    }
                }
            }
        }

        #region Payload builders

        /// <summary>
        /// Builds a ProcessMapping event payload:
        ///   Id (VarUInt), StartAddress (VarUInt), EndAddress (VarUInt),
        ///   FileOffset (VarUInt), FileName (ushort-prefixed UTF8), MetadataId (VarUInt)
        /// </summary>
        private static byte[] WriteProcessMappingPayload(
            ulong id, ulong startAddress, ulong endAddress,
            ulong fileOffset, string fileName, ulong metadataId)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.WriteVarUInt(id);
                bw.WriteVarUInt(startAddress);
                bw.WriteVarUInt(endAddress);
                bw.WriteVarUInt(fileOffset);
                WriteShortUTF8String(bw, fileName);
                bw.WriteVarUInt(metadataId);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Builds a ProcessSymbol event payload:
        ///   Id (VarUInt), MappingId (VarUInt), StartAddress (VarUInt),
        ///   EndAddress (VarUInt), Name (ushort-prefixed UTF8)
        /// </summary>
        private static byte[] WriteProcessSymbolPayload(
            ulong id, ulong mappingId, ulong startAddress,
            ulong endAddress, string name)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                bw.WriteVarUInt(id);
                bw.WriteVarUInt(mappingId);
                bw.WriteVarUInt(startAddress);
                bw.WriteVarUInt(endAddress);
                WriteShortUTF8String(bw, name);
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Writes a ushort-length-prefixed UTF8 string, matching the format read by
        /// TraceEventRawReaders.ReadShortUTF8String.
        /// </summary>
        private static void WriteShortUTF8String(BinaryWriter writer, string value)
        {
            byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(value);
            writer.Write((ushort)utf8.Length);
            writer.Write(utf8);
        }

        #endregion
    }
}
