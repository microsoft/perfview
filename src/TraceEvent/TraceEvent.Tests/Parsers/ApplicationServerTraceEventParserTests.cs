using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.ApplicationServer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace TraceEventTests.Parsers
{
    public partial class ApplicationServerTraceEventParserTests
    {
        private const string ProviderName = "Microsoft-Windows-Application Server-Applications";

        /// <summary>
        /// Validates that every event in the ApplicationServerTraceEventParser fires correctly
        /// with the expected payload values when processing a synthetic nettrace trace.
        /// </summary>
        /// <summary>
        /// Validates that every event in the ApplicationServerTraceEventParser fires correctly
        /// with the expected payload values when processing a synthetic nettrace trace.
        /// </summary>
        [Fact]
        public void AllEventsRoundTrip()
        {
            // Build synthetic trace with all 481 events
            byte[] traceBytes = BuildTrace();

            // Process and collect results
            var firedEvents = new Dictionary<string, Dictionary<string, object>>();
            using (var stream = new MemoryStream(traceBytes))
            {
                var source = new EventPipeEventSource(stream);
                var parser = new ApplicationServerTraceEventParser(source);
                SubscribeAllEvents(parser, firedEvents);
                source.Process();
            }

            // Validate all events fired with correct values
            ValidateAllEvents(firedEvents);
        }

        #region Trace Building

        private byte[] BuildTrace()
        {
            var writer = new EventPipeWriterV5();
            writer.WriteHeaders();

            int metadataId = 1;
            // Each chunk writes its metadata entries
            WriteMetadata_Chunk01(writer, ref metadataId);
            WriteMetadata_Chunk02(writer, ref metadataId);
            WriteMetadata_Chunk03(writer, ref metadataId);
            WriteMetadata_Chunk04(writer, ref metadataId);
            WriteMetadata_Chunk05(writer, ref metadataId);
            WriteMetadata_Chunk06(writer, ref metadataId);
            WriteMetadata_Chunk07(writer, ref metadataId);
            WriteMetadata_Chunk08(writer, ref metadataId);
            WriteMetadata_Chunk09(writer, ref metadataId);
            WriteMetadata_Chunk10(writer, ref metadataId);
            WriteMetadata_Chunk11(writer, ref metadataId);
            WriteMetadata_Chunk12(writer, ref metadataId);
            WriteMetadata_Chunk13(writer, ref metadataId);

            // Reset metadataId counter for events (events reference the same IDs)
            metadataId = 1;
            int sequenceNumber = 1;
            // Each chunk writes its event payloads
            WriteEvents_Chunk01(writer, ref metadataId, ref sequenceNumber);
            WriteEvents_Chunk02(writer, ref metadataId, ref sequenceNumber);
            WriteEvents_Chunk03(writer, ref metadataId, ref sequenceNumber);
            WriteEvents_Chunk04(writer, ref metadataId, ref sequenceNumber);
            WriteEvents_Chunk05(writer, ref metadataId, ref sequenceNumber);
            WriteEvents_Chunk06(writer, ref metadataId, ref sequenceNumber);
            WriteEvents_Chunk07(writer, ref metadataId, ref sequenceNumber);
            WriteEvents_Chunk08(writer, ref metadataId, ref sequenceNumber);
            WriteEvents_Chunk09(writer, ref metadataId, ref sequenceNumber);
            WriteEvents_Chunk10(writer, ref metadataId, ref sequenceNumber);
            WriteEvents_Chunk11(writer, ref metadataId, ref sequenceNumber);
            WriteEvents_Chunk12(writer, ref metadataId, ref sequenceNumber);
            WriteEvents_Chunk13(writer, ref metadataId, ref sequenceNumber);

            writer.WriteEndObject();
            return writer.ToArray();
        }

        #endregion

        #region Event Subscription

        private void SubscribeAllEvents(ApplicationServerTraceEventParser parser, Dictionary<string, Dictionary<string, object>> firedEvents)
        {
            Subscribe_Chunk01(parser, firedEvents);
            Subscribe_Chunk02(parser, firedEvents);
            Subscribe_Chunk03(parser, firedEvents);
            Subscribe_Chunk04(parser, firedEvents);
            Subscribe_Chunk05(parser, firedEvents);
            Subscribe_Chunk06(parser, firedEvents);
            Subscribe_Chunk07(parser, firedEvents);
            Subscribe_Chunk08(parser, firedEvents);
            Subscribe_Chunk09(parser, firedEvents);
            Subscribe_Chunk10(parser, firedEvents);
            Subscribe_Chunk11(parser, firedEvents);
            Subscribe_Chunk12(parser, firedEvents);
            Subscribe_Chunk13(parser, firedEvents);
        }

        #endregion

        #region Validation

        private void ValidateAllEvents(Dictionary<string, Dictionary<string, object>> firedEvents)
        {
            Validate_Chunk01(firedEvents);
            Validate_Chunk02(firedEvents);
            Validate_Chunk03(firedEvents);
            Validate_Chunk04(firedEvents);
            Validate_Chunk05(firedEvents);
            Validate_Chunk06(firedEvents);
            Validate_Chunk07(firedEvents);
            Validate_Chunk08(firedEvents);
            Validate_Chunk09(firedEvents);
            Validate_Chunk10(firedEvents);
            Validate_Chunk11(firedEvents);
            Validate_Chunk12(firedEvents);
            Validate_Chunk13(firedEvents);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Generates a unique non-default string value for a given event and field.
        /// </summary>
        private static string TestString(int eventId, string fieldName)
        {
            return $"Event{eventId}_{fieldName}";
        }

        /// <summary>
        /// Generates a unique non-default Int32 value derived from event ID and field index.
        /// </summary>
        private static int TestInt32(int eventId, int fieldIndex)
        {
            return eventId * 100 + fieldIndex + 1;
        }

        /// <summary>
        /// Generates a unique non-default Int64 value derived from event ID and field index.
        /// </summary>
        private static long TestInt64(int eventId, int fieldIndex)
        {
            return (long)eventId * 100000 + fieldIndex + 1;
        }

        /// <summary>
        /// Generates a unique non-default UInt64 value derived from event ID and field index.
        /// </summary>
        private static ulong TestUInt64(int eventId, int fieldIndex)
        {
            return (ulong)eventId * 100000 + (ulong)fieldIndex + 1;
        }

        /// <summary>
        /// Generates a unique non-default byte value derived from event ID.
        /// </summary>
        private static byte TestByte(int eventId, int fieldIndex)
        {
            return (byte)((eventId + fieldIndex) % 255 + 1);
        }

        /// <summary>
        /// Generates a unique non-default GUID derived from event ID and field index.
        /// </summary>
        private static Guid TestGuid(int eventId, int fieldIndex)
        {
            return new Guid(eventId, (short)fieldIndex, 0, 0, 1, 2, 3, 4, 5, 6, 7);
        }

        /// <summary>
        /// Generates a unique non-default FILETIME (as Int64) derived from event ID and field index.
        /// </summary>
        private static long TestFileTime(int eventId, int fieldIndex)
        {
            // Use a date in 2020 plus event-specific offset to avoid default values
            return new DateTime(2020, 1, 1).Ticks + (long)eventId * 1000000 + fieldIndex;
        }

        /// <summary>
        /// Writes a null-terminated UTF-16 string to a BinaryWriter.
        /// </summary>
        private static void WriteUnicodeString(BinaryWriter writer, string value)
        {
            byte[] bytes = Encoding.Unicode.GetBytes(value);
            writer.Write(bytes);
            writer.Write((short)0); // null terminator
        }

        /// <summary>
        /// Writes a payload for the given template fields.
        /// Returns the byte array of the payload.
        /// </summary>
        private static byte[] BuildPayload(int eventId, TemplateField[] fields)
        {
            using (var ms = new MemoryStream())
            using (var bw = new BinaryWriter(ms))
            {
                for (int i = 0; i < fields.Length; i++)
                {
                    switch (fields[i].Type)
                    {
                        case FieldType.UnicodeString:
                            WriteUnicodeString(bw, TestString(eventId, fields[i].Name));
                            break;
                        case FieldType.Int32:
                            bw.Write(TestInt32(eventId, i));
                            break;
                        case FieldType.Int64:
                            bw.Write(TestInt64(eventId, i));
                            break;
                        case FieldType.UInt64:
                            bw.Write((long)TestUInt64(eventId, i)); // written as 8 bytes
                            break;
                        case FieldType.UInt8:
                            bw.Write(TestByte(eventId, i));
                            break;
                        case FieldType.Guid:
                            bw.Write(TestGuid(eventId, i).ToByteArray());
                            break;
                        case FieldType.FileTime:
                            bw.Write(TestFileTime(eventId, i));
                            break;
                    }
                }
                return ms.ToArray();
            }
        }

        internal enum FieldType
        {
            UnicodeString,
            Int32,
            Int64,
            UInt64,
            UInt8,
            Guid,
            FileTime
        }

        internal struct TemplateField
        {
            public string Name;
            public FieldType Type;

            public TemplateField(string name, FieldType type)
            {
                Name = name;
                Type = type;
            }
        }

        #endregion
    }
}
