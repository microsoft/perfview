// Auto-generated test code for chunk 12 (events 4804-5204)
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.ApplicationServer;
using System;
using System.Collections.Generic;
using Xunit;

namespace TraceEventTests.Parsers
{
    public partial class ApplicationServerTraceEventParserTests
    {
        // =====================================================================
        // Template field arrays for chunk 12
        // =====================================================================

        private static readonly TemplateField[] s_OneStringsTemplateAFields = new TemplateField[]
        {
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_Multidata33TemplateAFields = new TemplateField[]
        {
            new TemplateField("TypeName", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_Multidata87TemplateAFields = new TemplateField[]
        {
            new TemplateField("discoveryMessageName", FieldType.UnicodeString),
            new TemplateField("messageId", FieldType.UnicodeString),
            new TemplateField("discoveryOperationName", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_Multidata88TemplateAFields = new TemplateField[]
        {
            new TemplateField("messageType", FieldType.UnicodeString),
            new TemplateField("messageId", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_Multidata89TemplateAFields = new TemplateField[]
        {
            new TemplateField("discoveryMessageName", FieldType.UnicodeString),
            new TemplateField("messageId", FieldType.UnicodeString),
            new TemplateField("relatesTo", FieldType.UnicodeString),
            new TemplateField("discoveryOperationName", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_Multidata90TemplateAFields = new TemplateField[]
        {
            new TemplateField("messageId", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_Multidata91TemplateAFields = new TemplateField[]
        {
            new TemplateField("messageType", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_Multidata92TemplateAFields = new TemplateField[]
        {
            new TemplateField("discoveryMessageName", FieldType.UnicodeString),
            new TemplateField("messageId", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_Multidata93TemplateAFields = new TemplateField[]
        {
            new TemplateField("endpointAddress", FieldType.UnicodeString),
            new TemplateField("listenUri", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_Multidata94TemplateEAFields = new TemplateField[]
        {
            new TemplateField("endpointAddress", FieldType.UnicodeString),
            new TemplateField("via", FieldType.UnicodeString),
            new TemplateField("SerializedException", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_Multidata95TemplateAFields = new TemplateField[]
        {
            new TemplateField("endpointAddress", FieldType.UnicodeString),
            new TemplateField("via", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_Multidata96TemplateAFields = new TemplateField[]
        {
            new TemplateField("synchronizationContextType", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_Multidata97TemplateAFields = new TemplateField[]
        {
            new TemplateField("SurrogateType", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_Multidata98TemplateAFields = new TemplateField[]
        {
            new TemplateField("Kind", FieldType.UnicodeString),
            new TemplateField("TypeName", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_Multidata99TemplateAFields = new TemplateField[]
        {
            new TemplateField("DCType", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        // =====================================================================
        // WriteMetadata_Chunk12: Emit metadata entries for events 4804-5204
        // =====================================================================
        private void WriteMetadata_Chunk12(EventPipeWriterV5 writer, ref int metadataId)
        {
            writer.WriteMetadataBlock(
                // Discovery events (4804-4821)
                new EventMetadata(metadataId++, ProviderName, "DiscoveryMessageReceivedAfterOperationCompleted", 4804) { OpCode = 45 },
                new EventMetadata(metadataId++, ProviderName, "DiscoveryMessageWithInvalidContent", 4805) { OpCode = 37 },
                new EventMetadata(metadataId++, ProviderName, "DiscoveryMessageWithInvalidRelatesToOrOperationCompleted", 4806) { OpCode = 38 },
                new EventMetadata(metadataId++, ProviderName, "DiscoveryMessageWithInvalidReplyTo", 4807) { OpCode = 39 },
                new EventMetadata(metadataId++, ProviderName, "DiscoveryMessageWithNoContent", 4808) { OpCode = 40 },
                new EventMetadata(metadataId++, ProviderName, "DiscoveryMessageWithNullMessageId", 4809) { OpCode = 41 },
                new EventMetadata(metadataId++, ProviderName, "DiscoveryMessageWithNullMessageSequence", 4810) { OpCode = 42 },
                new EventMetadata(metadataId++, ProviderName, "DiscoveryMessageWithNullRelatesTo", 4811) { OpCode = 43 },
                new EventMetadata(metadataId++, ProviderName, "DiscoveryMessageWithNullReplyTo", 4812) { OpCode = 44 },
                new EventMetadata(metadataId++, ProviderName, "DuplicateDiscoveryMessage", 4813) { OpCode = 36 },
                new EventMetadata(metadataId++, ProviderName, "EndpointDiscoverabilityDisabled", 4814) { OpCode = 58 },
                new EventMetadata(metadataId++, ProviderName, "EndpointDiscoverabilityEnabled", 4815) { OpCode = 59 },
                new EventMetadata(metadataId++, ProviderName, "FindInitiatedInDiscoveryClientChannel", 4816) { OpCode = 33 },
                new EventMetadata(metadataId++, ProviderName, "InnerChannelCreationFailed", 4817) { OpCode = 32 },
                new EventMetadata(metadataId++, ProviderName, "InnerChannelOpenFailed", 4818) { OpCode = 34 },
                new EventMetadata(metadataId++, ProviderName, "InnerChannelOpenSucceeded", 4819) { OpCode = 35 },
                new EventMetadata(metadataId++, ProviderName, "SynchronizationContextReset", 4820) { OpCode = 46 },
                new EventMetadata(metadataId++, ProviderName, "SynchronizationContextSetToNull", 4821) { OpCode = 47 },
                // Serialization events (5001-5017)
                new EventMetadata(metadataId++, ProviderName, "DCSerializeWithSurrogateStart", 5001) { OpCode = 1 },
                new EventMetadata(metadataId++, ProviderName, "DCSerializeWithSurrogateStop", 5002) { OpCode = 2 },
                new EventMetadata(metadataId++, ProviderName, "DCDeserializeWithSurrogateStart", 5003) { OpCode = 1 },
                new EventMetadata(metadataId++, ProviderName, "DCDeserializeWithSurrogateStop", 5004) { OpCode = 2 },
                new EventMetadata(metadataId++, ProviderName, "ImportKnownTypesStart", 5005) { OpCode = 1 },
                new EventMetadata(metadataId++, ProviderName, "ImportKnownTypesStop", 5006) { OpCode = 2 },
                new EventMetadata(metadataId++, ProviderName, "DCResolverResolve", 5007) { OpCode = 1 },
                new EventMetadata(metadataId++, ProviderName, "DCGenWriterStart", 5008) { OpCode = 1 },
                new EventMetadata(metadataId++, ProviderName, "DCGenWriterStop", 5009) { OpCode = 2 },
                new EventMetadata(metadataId++, ProviderName, "DCGenReaderStart", 5010) { OpCode = 1 },
                new EventMetadata(metadataId++, ProviderName, "DCGenReaderStop", 5011) { OpCode = 2 },
                new EventMetadata(metadataId++, ProviderName, "DCJsonGenReaderStart", 5012) { OpCode = 1 },
                new EventMetadata(metadataId++, ProviderName, "DCJsonGenReaderStop", 5013) { OpCode = 2 },
                new EventMetadata(metadataId++, ProviderName, "DCJsonGenWriterStart", 5014) { OpCode = 1 },
                new EventMetadata(metadataId++, ProviderName, "DCJsonGenWriterStop", 5015) { OpCode = 2 },
                new EventMetadata(metadataId++, ProviderName, "GenXmlSerializableStart", 5016) { OpCode = 1 },
                new EventMetadata(metadataId++, ProviderName, "GenXmlSerializableStop", 5017) { OpCode = 2 },
                // Channel events (5203-5204)
                new EventMetadata(metadataId++, ProviderName, "JsonMessageDecodingStart", 5203) { OpCode = 1 },
                new EventMetadata(metadataId++, ProviderName, "JsonMessageEncodingStart", 5204) { OpCode = 1 }
            );
        }

        // =====================================================================
        // WriteEvents_Chunk12: Emit event payloads for events 4804-5204
        // =====================================================================
        private void WriteEvents_Chunk12(EventPipeWriterV5 writer, ref int metadataId, ref int sequenceNumber)
        {
            int __metadataId = metadataId;
            int __sequenceNumber = sequenceNumber;
            writer.WriteEventBlock(w =>
            {
                // 4804: Multidata87TemplateA (discoveryMessageName, messageId, discoveryOperationName, AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4804, s_Multidata87TemplateAFields));
                // 4805: Multidata88TemplateA (messageType, messageId, AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4805, s_Multidata88TemplateAFields));
                // 4806: Multidata89TemplateA (discoveryMessageName, messageId, relatesTo, discoveryOperationName, AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4806, s_Multidata89TemplateAFields));
                // 4807: Multidata90TemplateA (messageId, AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4807, s_Multidata90TemplateAFields));
                // 4808: Multidata91TemplateA (messageType, AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4808, s_Multidata91TemplateAFields));
                // 4809: Multidata91TemplateA (messageType, AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4809, s_Multidata91TemplateAFields));
                // 4810: Multidata92TemplateA (discoveryMessageName, messageId, AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4810, s_Multidata92TemplateAFields));
                // 4811: Multidata92TemplateA (discoveryMessageName, messageId, AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4811, s_Multidata92TemplateAFields));
                // 4812: Multidata90TemplateA (messageId, AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4812, s_Multidata90TemplateAFields));
                // 4813: Multidata88TemplateA (messageType, messageId, AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4813, s_Multidata88TemplateAFields));
                // 4814: Multidata93TemplateA (endpointAddress, listenUri, AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4814, s_Multidata93TemplateAFields));
                // 4815: Multidata93TemplateA (endpointAddress, listenUri, AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4815, s_Multidata93TemplateAFields));
                // 4816: OneStringsTemplateA (AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4816, s_OneStringsTemplateAFields));
                // 4817: Multidata94TemplateEA (endpointAddress, via, SerializedException, AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4817, s_Multidata94TemplateEAFields));
                // 4818: Multidata94TemplateEA (endpointAddress, via, SerializedException, AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4818, s_Multidata94TemplateEAFields));
                // 4819: Multidata95TemplateA (endpointAddress, via, AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4819, s_Multidata95TemplateAFields));
                // 4820: Multidata96TemplateA (synchronizationContextType, AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4820, s_Multidata96TemplateAFields));
                // 4821: OneStringsTemplateA (AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4821, s_OneStringsTemplateAFields));
                // 5001: Multidata97TemplateA (SurrogateType, AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5001, s_Multidata97TemplateAFields));
                // 5002: OneStringsTemplateA (AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5002, s_OneStringsTemplateAFields));
                // 5003: Multidata97TemplateA (SurrogateType, AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5003, s_Multidata97TemplateAFields));
                // 5004: OneStringsTemplateA (AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5004, s_OneStringsTemplateAFields));
                // 5005: OneStringsTemplateA (AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5005, s_OneStringsTemplateAFields));
                // 5006: OneStringsTemplateA (AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5006, s_OneStringsTemplateAFields));
                // 5007: Multidata33TemplateA (TypeName, AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5007, s_Multidata33TemplateAFields));
                // 5008: Multidata98TemplateA (Kind, TypeName, AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5008, s_Multidata98TemplateAFields));
                // 5009: OneStringsTemplateA (AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5009, s_OneStringsTemplateAFields));
                // 5010: Multidata98TemplateA (Kind, TypeName, AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5010, s_Multidata98TemplateAFields));
                // 5011: OneStringsTemplateA (AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5011, s_OneStringsTemplateAFields));
                // 5012: Multidata98TemplateA (Kind, TypeName, AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5012, s_Multidata98TemplateAFields));
                // 5013: OneStringsTemplateA (AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5013, s_OneStringsTemplateAFields));
                // 5014: Multidata98TemplateA (Kind, TypeName, AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5014, s_Multidata98TemplateAFields));
                // 5015: OneStringsTemplateA (AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5015, s_OneStringsTemplateAFields));
                // 5016: Multidata99TemplateA (DCType, AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5016, s_Multidata99TemplateAFields));
                // 5017: OneStringsTemplateA (AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5017, s_OneStringsTemplateAFields));
                // 5203: OneStringsTemplateA (AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5203, s_OneStringsTemplateAFields));
                // 5204: OneStringsTemplateA (AppDomain)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5204, s_OneStringsTemplateAFields));
            });
        
            metadataId = __metadataId;
            sequenceNumber = __sequenceNumber;
        }

        // =====================================================================
        // Subscribe_Chunk12: Subscribe to parser events and record received data
        // =====================================================================
                private void Subscribe_Chunk12(ApplicationServerTraceEventParser parser, Dictionary<string, Dictionary<string, object>> firedEvents)
        {
            parser.DiscoveryMessageReceivedAfterOperationCompleted += delegate (Multidata87TemplateATraceData data)
            {
                firedEvents["DiscoveryMessageReceivedAfterOperationCompleted"] = new Dictionary<string, object>
                {
                    { "discoveryMessageName", data.discoveryMessageName },
                    { "messageId", data.messageId },
                    { "discoveryOperationName", data.discoveryOperationName },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DiscoveryMessageWithInvalidContent += delegate (Multidata88TemplateATraceData data)
            {
                firedEvents["DiscoveryMessageWithInvalidContent"] = new Dictionary<string, object>
                {
                    { "messageType", data.messageType },
                    { "messageId", data.messageId },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DiscoveryMessageWithInvalidRelatesToOrOperationCompleted += delegate (Multidata89TemplateATraceData data)
            {
                firedEvents["DiscoveryMessageWithInvalidRelatesToOrOperationCompleted"] = new Dictionary<string, object>
                {
                    { "discoveryMessageName", data.discoveryMessageName },
                    { "messageId", data.messageId },
                    { "relatesTo", data.relatesTo },
                    { "discoveryOperationName", data.discoveryOperationName },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DiscoveryMessageWithInvalidReplyTo += delegate (Multidata90TemplateATraceData data)
            {
                firedEvents["DiscoveryMessageWithInvalidReplyTo"] = new Dictionary<string, object>
                {
                    { "messageId", data.messageId },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DiscoveryMessageWithNoContent += delegate (Multidata91TemplateATraceData data)
            {
                firedEvents["DiscoveryMessageWithNoContent"] = new Dictionary<string, object>
                {
                    { "messageType", data.messageType },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DiscoveryMessageWithNullMessageId += delegate (Multidata91TemplateATraceData data)
            {
                firedEvents["DiscoveryMessageWithNullMessageId"] = new Dictionary<string, object>
                {
                    { "messageType", data.messageType },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DiscoveryMessageWithNullMessageSequence += delegate (Multidata92TemplateATraceData data)
            {
                firedEvents["DiscoveryMessageWithNullMessageSequence"] = new Dictionary<string, object>
                {
                    { "discoveryMessageName", data.discoveryMessageName },
                    { "messageId", data.messageId },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DiscoveryMessageWithNullRelatesTo += delegate (Multidata92TemplateATraceData data)
            {
                firedEvents["DiscoveryMessageWithNullRelatesTo"] = new Dictionary<string, object>
                {
                    { "discoveryMessageName", data.discoveryMessageName },
                    { "messageId", data.messageId },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DiscoveryMessageWithNullReplyTo += delegate (Multidata90TemplateATraceData data)
            {
                firedEvents["DiscoveryMessageWithNullReplyTo"] = new Dictionary<string, object>
                {
                    { "messageId", data.messageId },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DuplicateDiscoveryMessage += delegate (Multidata88TemplateATraceData data)
            {
                firedEvents["DuplicateDiscoveryMessage"] = new Dictionary<string, object>
                {
                    { "messageType", data.messageType },
                    { "messageId", data.messageId },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.EndpointDiscoverabilityDisabled += delegate (Multidata93TemplateATraceData data)
            {
                firedEvents["EndpointDiscoverabilityDisabled"] = new Dictionary<string, object>
                {
                    { "endpointAddress", data.endpointAddress },
                    { "listenUri", data.listenUri },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.EndpointDiscoverabilityEnabled += delegate (Multidata93TemplateATraceData data)
            {
                firedEvents["EndpointDiscoverabilityEnabled"] = new Dictionary<string, object>
                {
                    { "endpointAddress", data.endpointAddress },
                    { "listenUri", data.listenUri },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.FindInitiatedInDiscoveryClientChannel += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["FindInitiatedInDiscoveryClientChannel"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.InnerChannelCreationFailed += delegate (Multidata94TemplateEATraceData data)
            {
                firedEvents["InnerChannelCreationFailed"] = new Dictionary<string, object>
                {
                    { "endpointAddress", data.endpointAddress },
                    { "via", data.via },
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.InnerChannelOpenFailed += delegate (Multidata94TemplateEATraceData data)
            {
                firedEvents["InnerChannelOpenFailed"] = new Dictionary<string, object>
                {
                    { "endpointAddress", data.endpointAddress },
                    { "via", data.via },
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.InnerChannelOpenSucceeded += delegate (Multidata95TemplateATraceData data)
            {
                firedEvents["InnerChannelOpenSucceeded"] = new Dictionary<string, object>
                {
                    { "endpointAddress", data.endpointAddress },
                    { "via", data.via },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.SynchronizationContextReset += delegate (Multidata96TemplateATraceData data)
            {
                firedEvents["SynchronizationContextReset"] = new Dictionary<string, object>
                {
                    { "synchronizationContextType", data.synchronizationContextType },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.SynchronizationContextSetToNull += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["SynchronizationContextSetToNull"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DCSerializeWithSurrogateStart += delegate (Multidata97TemplateATraceData data)
            {
                firedEvents["DCSerializeWithSurrogateStart"] = new Dictionary<string, object>
                {
                    { "SurrogateType", data.SurrogateType },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DCSerializeWithSurrogateStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["DCSerializeWithSurrogateStop"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DCDeserializeWithSurrogateStart += delegate (Multidata97TemplateATraceData data)
            {
                firedEvents["DCDeserializeWithSurrogateStart"] = new Dictionary<string, object>
                {
                    { "SurrogateType", data.SurrogateType },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DCDeserializeWithSurrogateStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["DCDeserializeWithSurrogateStop"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.ImportKnownTypesStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["ImportKnownTypesStart"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.ImportKnownTypesStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["ImportKnownTypesStop"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DCResolverResolve += delegate (Multidata33TemplateATraceData data)
            {
                firedEvents["DCResolverResolve"] = new Dictionary<string, object>
                {
                    { "TypeName", data.TypeName },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DCGenWriterStart += delegate (Multidata98TemplateATraceData data)
            {
                firedEvents["DCGenWriterStart"] = new Dictionary<string, object>
                {
                    { "Kind", data.Kind },
                    { "TypeName", data.TypeName },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DCGenWriterStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["DCGenWriterStop"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DCGenReaderStart += delegate (Multidata98TemplateATraceData data)
            {
                firedEvents["DCGenReaderStart"] = new Dictionary<string, object>
                {
                    { "Kind", data.Kind },
                    { "TypeName", data.TypeName },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DCGenReaderStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["DCGenReaderStop"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DCJsonGenReaderStart += delegate (Multidata98TemplateATraceData data)
            {
                firedEvents["DCJsonGenReaderStart"] = new Dictionary<string, object>
                {
                    { "Kind", data.Kind },
                    { "TypeName", data.TypeName },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DCJsonGenReaderStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["DCJsonGenReaderStop"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DCJsonGenWriterStart += delegate (Multidata98TemplateATraceData data)
            {
                firedEvents["DCJsonGenWriterStart"] = new Dictionary<string, object>
                {
                    { "Kind", data.Kind },
                    { "TypeName", data.TypeName },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DCJsonGenWriterStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["DCJsonGenWriterStop"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.GenXmlSerializableStart += delegate (Multidata99TemplateATraceData data)
            {
                firedEvents["GenXmlSerializableStart"] = new Dictionary<string, object>
                {
                    { "DCType", data.DCType },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.GenXmlSerializableStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["GenXmlSerializableStop"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.JsonMessageDecodingStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["JsonMessageDecodingStart"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.JsonMessageEncodingStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["JsonMessageEncodingStart"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

        }


        // =====================================================================
        // Validate_Chunk12: Validate all received event payloads
        // =====================================================================
        private void Validate_Chunk12(Dictionary<string, Dictionary<string, object>> firedEvents)
        {
            // --- 4804: DiscoveryMessageReceivedAfterOperationCompleted ---
            Assert.True(firedEvents.ContainsKey("DiscoveryMessageReceivedAfterOperationCompleted"), "Event DiscoveryMessageReceivedAfterOperationCompleted not received");
            var eDiscoveryMessageReceivedAfterOperationCompleted = firedEvents["DiscoveryMessageReceivedAfterOperationCompleted"];
            Assert.Equal(TestString(4804, "discoveryMessageName"), (string)eDiscoveryMessageReceivedAfterOperationCompleted["discoveryMessageName"]);
            Assert.Equal(TestString(4804, "messageId"), (string)eDiscoveryMessageReceivedAfterOperationCompleted["messageId"]);
            Assert.Equal(TestString(4804, "discoveryOperationName"), (string)eDiscoveryMessageReceivedAfterOperationCompleted["discoveryOperationName"]);
            Assert.Equal(TestString(4804, "AppDomain"), (string)eDiscoveryMessageReceivedAfterOperationCompleted["AppDomain"]);

            // --- 4805: DiscoveryMessageWithInvalidContent ---
            Assert.True(firedEvents.ContainsKey("DiscoveryMessageWithInvalidContent"), "Event DiscoveryMessageWithInvalidContent not received");
            var eDiscoveryMessageWithInvalidContent = firedEvents["DiscoveryMessageWithInvalidContent"];
            Assert.Equal(TestString(4805, "messageType"), (string)eDiscoveryMessageWithInvalidContent["messageType"]);
            Assert.Equal(TestString(4805, "messageId"), (string)eDiscoveryMessageWithInvalidContent["messageId"]);
            Assert.Equal(TestString(4805, "AppDomain"), (string)eDiscoveryMessageWithInvalidContent["AppDomain"]);

            // --- 4806: DiscoveryMessageWithInvalidRelatesToOrOperationCompleted ---
            Assert.True(firedEvents.ContainsKey("DiscoveryMessageWithInvalidRelatesToOrOperationCompleted"), "Event DiscoveryMessageWithInvalidRelatesToOrOperationCompleted not received");
            var eDiscoveryMessageWithInvalidRelatesToOrOperationCompleted = firedEvents["DiscoveryMessageWithInvalidRelatesToOrOperationCompleted"];
            Assert.Equal(TestString(4806, "discoveryMessageName"), (string)eDiscoveryMessageWithInvalidRelatesToOrOperationCompleted["discoveryMessageName"]);
            Assert.Equal(TestString(4806, "messageId"), (string)eDiscoveryMessageWithInvalidRelatesToOrOperationCompleted["messageId"]);
            Assert.Equal(TestString(4806, "relatesTo"), (string)eDiscoveryMessageWithInvalidRelatesToOrOperationCompleted["relatesTo"]);
            Assert.Equal(TestString(4806, "discoveryOperationName"), (string)eDiscoveryMessageWithInvalidRelatesToOrOperationCompleted["discoveryOperationName"]);
            Assert.Equal(TestString(4806, "AppDomain"), (string)eDiscoveryMessageWithInvalidRelatesToOrOperationCompleted["AppDomain"]);

            // --- 4807: DiscoveryMessageWithInvalidReplyTo ---
            Assert.True(firedEvents.ContainsKey("DiscoveryMessageWithInvalidReplyTo"), "Event DiscoveryMessageWithInvalidReplyTo not received");
            var eDiscoveryMessageWithInvalidReplyTo = firedEvents["DiscoveryMessageWithInvalidReplyTo"];
            Assert.Equal(TestString(4807, "messageId"), (string)eDiscoveryMessageWithInvalidReplyTo["messageId"]);
            Assert.Equal(TestString(4807, "AppDomain"), (string)eDiscoveryMessageWithInvalidReplyTo["AppDomain"]);

            // --- 4808: DiscoveryMessageWithNoContent ---
            Assert.True(firedEvents.ContainsKey("DiscoveryMessageWithNoContent"), "Event DiscoveryMessageWithNoContent not received");
            var eDiscoveryMessageWithNoContent = firedEvents["DiscoveryMessageWithNoContent"];
            Assert.Equal(TestString(4808, "messageType"), (string)eDiscoveryMessageWithNoContent["messageType"]);
            Assert.Equal(TestString(4808, "AppDomain"), (string)eDiscoveryMessageWithNoContent["AppDomain"]);

            // --- 4809: DiscoveryMessageWithNullMessageId ---
            Assert.True(firedEvents.ContainsKey("DiscoveryMessageWithNullMessageId"), "Event DiscoveryMessageWithNullMessageId not received");
            var eDiscoveryMessageWithNullMessageId = firedEvents["DiscoveryMessageWithNullMessageId"];
            Assert.Equal(TestString(4809, "messageType"), (string)eDiscoveryMessageWithNullMessageId["messageType"]);
            Assert.Equal(TestString(4809, "AppDomain"), (string)eDiscoveryMessageWithNullMessageId["AppDomain"]);

            // --- 4810: DiscoveryMessageWithNullMessageSequence ---
            Assert.True(firedEvents.ContainsKey("DiscoveryMessageWithNullMessageSequence"), "Event DiscoveryMessageWithNullMessageSequence not received");
            var eDiscoveryMessageWithNullMessageSequence = firedEvents["DiscoveryMessageWithNullMessageSequence"];
            Assert.Equal(TestString(4810, "discoveryMessageName"), (string)eDiscoveryMessageWithNullMessageSequence["discoveryMessageName"]);
            Assert.Equal(TestString(4810, "messageId"), (string)eDiscoveryMessageWithNullMessageSequence["messageId"]);
            Assert.Equal(TestString(4810, "AppDomain"), (string)eDiscoveryMessageWithNullMessageSequence["AppDomain"]);

            // --- 4811: DiscoveryMessageWithNullRelatesTo ---
            Assert.True(firedEvents.ContainsKey("DiscoveryMessageWithNullRelatesTo"), "Event DiscoveryMessageWithNullRelatesTo not received");
            var eDiscoveryMessageWithNullRelatesTo = firedEvents["DiscoveryMessageWithNullRelatesTo"];
            Assert.Equal(TestString(4811, "discoveryMessageName"), (string)eDiscoveryMessageWithNullRelatesTo["discoveryMessageName"]);
            Assert.Equal(TestString(4811, "messageId"), (string)eDiscoveryMessageWithNullRelatesTo["messageId"]);
            Assert.Equal(TestString(4811, "AppDomain"), (string)eDiscoveryMessageWithNullRelatesTo["AppDomain"]);

            // --- 4812: DiscoveryMessageWithNullReplyTo ---
            Assert.True(firedEvents.ContainsKey("DiscoveryMessageWithNullReplyTo"), "Event DiscoveryMessageWithNullReplyTo not received");
            var eDiscoveryMessageWithNullReplyTo = firedEvents["DiscoveryMessageWithNullReplyTo"];
            Assert.Equal(TestString(4812, "messageId"), (string)eDiscoveryMessageWithNullReplyTo["messageId"]);
            Assert.Equal(TestString(4812, "AppDomain"), (string)eDiscoveryMessageWithNullReplyTo["AppDomain"]);

            // --- 4813: DuplicateDiscoveryMessage ---
            Assert.True(firedEvents.ContainsKey("DuplicateDiscoveryMessage"), "Event DuplicateDiscoveryMessage not received");
            var eDuplicateDiscoveryMessage = firedEvents["DuplicateDiscoveryMessage"];
            Assert.Equal(TestString(4813, "messageType"), (string)eDuplicateDiscoveryMessage["messageType"]);
            Assert.Equal(TestString(4813, "messageId"), (string)eDuplicateDiscoveryMessage["messageId"]);
            Assert.Equal(TestString(4813, "AppDomain"), (string)eDuplicateDiscoveryMessage["AppDomain"]);

            // --- 4814: EndpointDiscoverabilityDisabled ---
            Assert.True(firedEvents.ContainsKey("EndpointDiscoverabilityDisabled"), "Event EndpointDiscoverabilityDisabled not received");
            var eEndpointDiscoverabilityDisabled = firedEvents["EndpointDiscoverabilityDisabled"];
            Assert.Equal(TestString(4814, "endpointAddress"), (string)eEndpointDiscoverabilityDisabled["endpointAddress"]);
            Assert.Equal(TestString(4814, "listenUri"), (string)eEndpointDiscoverabilityDisabled["listenUri"]);
            Assert.Equal(TestString(4814, "AppDomain"), (string)eEndpointDiscoverabilityDisabled["AppDomain"]);

            // --- 4815: EndpointDiscoverabilityEnabled ---
            Assert.True(firedEvents.ContainsKey("EndpointDiscoverabilityEnabled"), "Event EndpointDiscoverabilityEnabled not received");
            var eEndpointDiscoverabilityEnabled = firedEvents["EndpointDiscoverabilityEnabled"];
            Assert.Equal(TestString(4815, "endpointAddress"), (string)eEndpointDiscoverabilityEnabled["endpointAddress"]);
            Assert.Equal(TestString(4815, "listenUri"), (string)eEndpointDiscoverabilityEnabled["listenUri"]);
            Assert.Equal(TestString(4815, "AppDomain"), (string)eEndpointDiscoverabilityEnabled["AppDomain"]);

            // --- 4816: FindInitiatedInDiscoveryClientChannel ---
            Assert.True(firedEvents.ContainsKey("FindInitiatedInDiscoveryClientChannel"), "Event FindInitiatedInDiscoveryClientChannel not received");
            var eFindInitiatedInDiscoveryClientChannel = firedEvents["FindInitiatedInDiscoveryClientChannel"];
            Assert.Equal(TestString(4816, "AppDomain"), (string)eFindInitiatedInDiscoveryClientChannel["AppDomain"]);

            // --- 4817: InnerChannelCreationFailed ---
            Assert.True(firedEvents.ContainsKey("InnerChannelCreationFailed"), "Event InnerChannelCreationFailed not received");
            var eInnerChannelCreationFailed = firedEvents["InnerChannelCreationFailed"];
            Assert.Equal(TestString(4817, "endpointAddress"), (string)eInnerChannelCreationFailed["endpointAddress"]);
            Assert.Equal(TestString(4817, "via"), (string)eInnerChannelCreationFailed["via"]);
            Assert.Equal(TestString(4817, "SerializedException"), (string)eInnerChannelCreationFailed["SerializedException"]);
            Assert.Equal(TestString(4817, "AppDomain"), (string)eInnerChannelCreationFailed["AppDomain"]);

            // --- 4818: InnerChannelOpenFailed ---
            Assert.True(firedEvents.ContainsKey("InnerChannelOpenFailed"), "Event InnerChannelOpenFailed not received");
            var eInnerChannelOpenFailed = firedEvents["InnerChannelOpenFailed"];
            Assert.Equal(TestString(4818, "endpointAddress"), (string)eInnerChannelOpenFailed["endpointAddress"]);
            Assert.Equal(TestString(4818, "via"), (string)eInnerChannelOpenFailed["via"]);
            Assert.Equal(TestString(4818, "SerializedException"), (string)eInnerChannelOpenFailed["SerializedException"]);
            Assert.Equal(TestString(4818, "AppDomain"), (string)eInnerChannelOpenFailed["AppDomain"]);

            // --- 4819: InnerChannelOpenSucceeded ---
            Assert.True(firedEvents.ContainsKey("InnerChannelOpenSucceeded"), "Event InnerChannelOpenSucceeded not received");
            var eInnerChannelOpenSucceeded = firedEvents["InnerChannelOpenSucceeded"];
            Assert.Equal(TestString(4819, "endpointAddress"), (string)eInnerChannelOpenSucceeded["endpointAddress"]);
            Assert.Equal(TestString(4819, "via"), (string)eInnerChannelOpenSucceeded["via"]);
            Assert.Equal(TestString(4819, "AppDomain"), (string)eInnerChannelOpenSucceeded["AppDomain"]);

            // --- 4820: SynchronizationContextReset ---
            Assert.True(firedEvents.ContainsKey("SynchronizationContextReset"), "Event SynchronizationContextReset not received");
            var eSynchronizationContextReset = firedEvents["SynchronizationContextReset"];
            Assert.Equal(TestString(4820, "synchronizationContextType"), (string)eSynchronizationContextReset["synchronizationContextType"]);
            Assert.Equal(TestString(4820, "AppDomain"), (string)eSynchronizationContextReset["AppDomain"]);

            // --- 4821: SynchronizationContextSetToNull ---
            Assert.True(firedEvents.ContainsKey("SynchronizationContextSetToNull"), "Event SynchronizationContextSetToNull not received");
            var eSynchronizationContextSetToNull = firedEvents["SynchronizationContextSetToNull"];
            Assert.Equal(TestString(4821, "AppDomain"), (string)eSynchronizationContextSetToNull["AppDomain"]);

            // --- 5001: DCSerializeWithSurrogateStart ---
            Assert.True(firedEvents.ContainsKey("DCSerializeWithSurrogateStart"), "Event DCSerializeWithSurrogateStart not received");
            var eDCSerializeWithSurrogateStart = firedEvents["DCSerializeWithSurrogateStart"];
            Assert.Equal(TestString(5001, "SurrogateType"), (string)eDCSerializeWithSurrogateStart["SurrogateType"]);
            Assert.Equal(TestString(5001, "AppDomain"), (string)eDCSerializeWithSurrogateStart["AppDomain"]);

            // --- 5002: DCSerializeWithSurrogateStop ---
            Assert.True(firedEvents.ContainsKey("DCSerializeWithSurrogateStop"), "Event DCSerializeWithSurrogateStop not received");
            var eDCSerializeWithSurrogateStop = firedEvents["DCSerializeWithSurrogateStop"];
            Assert.Equal(TestString(5002, "AppDomain"), (string)eDCSerializeWithSurrogateStop["AppDomain"]);

            // --- 5003: DCDeserializeWithSurrogateStart ---
            Assert.True(firedEvents.ContainsKey("DCDeserializeWithSurrogateStart"), "Event DCDeserializeWithSurrogateStart not received");
            var eDCDeserializeWithSurrogateStart = firedEvents["DCDeserializeWithSurrogateStart"];
            Assert.Equal(TestString(5003, "SurrogateType"), (string)eDCDeserializeWithSurrogateStart["SurrogateType"]);
            Assert.Equal(TestString(5003, "AppDomain"), (string)eDCDeserializeWithSurrogateStart["AppDomain"]);

            // --- 5004: DCDeserializeWithSurrogateStop ---
            Assert.True(firedEvents.ContainsKey("DCDeserializeWithSurrogateStop"), "Event DCDeserializeWithSurrogateStop not received");
            var eDCDeserializeWithSurrogateStop = firedEvents["DCDeserializeWithSurrogateStop"];
            Assert.Equal(TestString(5004, "AppDomain"), (string)eDCDeserializeWithSurrogateStop["AppDomain"]);

            // --- 5005: ImportKnownTypesStart ---
            Assert.True(firedEvents.ContainsKey("ImportKnownTypesStart"), "Event ImportKnownTypesStart not received");
            var eImportKnownTypesStart = firedEvents["ImportKnownTypesStart"];
            Assert.Equal(TestString(5005, "AppDomain"), (string)eImportKnownTypesStart["AppDomain"]);

            // --- 5006: ImportKnownTypesStop ---
            Assert.True(firedEvents.ContainsKey("ImportKnownTypesStop"), "Event ImportKnownTypesStop not received");
            var eImportKnownTypesStop = firedEvents["ImportKnownTypesStop"];
            Assert.Equal(TestString(5006, "AppDomain"), (string)eImportKnownTypesStop["AppDomain"]);

            // --- 5007: DCResolverResolve ---
            Assert.True(firedEvents.ContainsKey("DCResolverResolve"), "Event DCResolverResolve not received");
            var eDCResolverResolve = firedEvents["DCResolverResolve"];
            Assert.Equal(TestString(5007, "TypeName"), (string)eDCResolverResolve["TypeName"]);
            Assert.Equal(TestString(5007, "AppDomain"), (string)eDCResolverResolve["AppDomain"]);

            // --- 5008: DCGenWriterStart ---
            Assert.True(firedEvents.ContainsKey("DCGenWriterStart"), "Event DCGenWriterStart not received");
            var eDCGenWriterStart = firedEvents["DCGenWriterStart"];
            Assert.Equal(TestString(5008, "Kind"), (string)eDCGenWriterStart["Kind"]);
            Assert.Equal(TestString(5008, "TypeName"), (string)eDCGenWriterStart["TypeName"]);
            Assert.Equal(TestString(5008, "AppDomain"), (string)eDCGenWriterStart["AppDomain"]);

            // --- 5009: DCGenWriterStop ---
            Assert.True(firedEvents.ContainsKey("DCGenWriterStop"), "Event DCGenWriterStop not received");
            var eDCGenWriterStop = firedEvents["DCGenWriterStop"];
            Assert.Equal(TestString(5009, "AppDomain"), (string)eDCGenWriterStop["AppDomain"]);

            // --- 5010: DCGenReaderStart ---
            Assert.True(firedEvents.ContainsKey("DCGenReaderStart"), "Event DCGenReaderStart not received");
            var eDCGenReaderStart = firedEvents["DCGenReaderStart"];
            Assert.Equal(TestString(5010, "Kind"), (string)eDCGenReaderStart["Kind"]);
            Assert.Equal(TestString(5010, "TypeName"), (string)eDCGenReaderStart["TypeName"]);
            Assert.Equal(TestString(5010, "AppDomain"), (string)eDCGenReaderStart["AppDomain"]);

            // --- 5011: DCGenReaderStop ---
            Assert.True(firedEvents.ContainsKey("DCGenReaderStop"), "Event DCGenReaderStop not received");
            var eDCGenReaderStop = firedEvents["DCGenReaderStop"];
            Assert.Equal(TestString(5011, "AppDomain"), (string)eDCGenReaderStop["AppDomain"]);

            // --- 5012: DCJsonGenReaderStart ---
            Assert.True(firedEvents.ContainsKey("DCJsonGenReaderStart"), "Event DCJsonGenReaderStart not received");
            var eDCJsonGenReaderStart = firedEvents["DCJsonGenReaderStart"];
            Assert.Equal(TestString(5012, "Kind"), (string)eDCJsonGenReaderStart["Kind"]);
            Assert.Equal(TestString(5012, "TypeName"), (string)eDCJsonGenReaderStart["TypeName"]);
            Assert.Equal(TestString(5012, "AppDomain"), (string)eDCJsonGenReaderStart["AppDomain"]);

            // --- 5013: DCJsonGenReaderStop ---
            Assert.True(firedEvents.ContainsKey("DCJsonGenReaderStop"), "Event DCJsonGenReaderStop not received");
            var eDCJsonGenReaderStop = firedEvents["DCJsonGenReaderStop"];
            Assert.Equal(TestString(5013, "AppDomain"), (string)eDCJsonGenReaderStop["AppDomain"]);

            // --- 5014: DCJsonGenWriterStart ---
            Assert.True(firedEvents.ContainsKey("DCJsonGenWriterStart"), "Event DCJsonGenWriterStart not received");
            var eDCJsonGenWriterStart = firedEvents["DCJsonGenWriterStart"];
            Assert.Equal(TestString(5014, "Kind"), (string)eDCJsonGenWriterStart["Kind"]);
            Assert.Equal(TestString(5014, "TypeName"), (string)eDCJsonGenWriterStart["TypeName"]);
            Assert.Equal(TestString(5014, "AppDomain"), (string)eDCJsonGenWriterStart["AppDomain"]);

            // --- 5015: DCJsonGenWriterStop ---
            Assert.True(firedEvents.ContainsKey("DCJsonGenWriterStop"), "Event DCJsonGenWriterStop not received");
            var eDCJsonGenWriterStop = firedEvents["DCJsonGenWriterStop"];
            Assert.Equal(TestString(5015, "AppDomain"), (string)eDCJsonGenWriterStop["AppDomain"]);

            // --- 5016: GenXmlSerializableStart ---
            Assert.True(firedEvents.ContainsKey("GenXmlSerializableStart"), "Event GenXmlSerializableStart not received");
            var eGenXmlSerializableStart = firedEvents["GenXmlSerializableStart"];
            Assert.Equal(TestString(5016, "DCType"), (string)eGenXmlSerializableStart["DCType"]);
            Assert.Equal(TestString(5016, "AppDomain"), (string)eGenXmlSerializableStart["AppDomain"]);

            // --- 5017: GenXmlSerializableStop ---
            Assert.True(firedEvents.ContainsKey("GenXmlSerializableStop"), "Event GenXmlSerializableStop not received");
            var eGenXmlSerializableStop = firedEvents["GenXmlSerializableStop"];
            Assert.Equal(TestString(5017, "AppDomain"), (string)eGenXmlSerializableStop["AppDomain"]);

            // --- 5203: JsonMessageDecodingStart ---
            Assert.True(firedEvents.ContainsKey("JsonMessageDecodingStart"), "Event JsonMessageDecodingStart not received");
            var eJsonMessageDecodingStart = firedEvents["JsonMessageDecodingStart"];
            Assert.Equal(TestString(5203, "AppDomain"), (string)eJsonMessageDecodingStart["AppDomain"]);

            // --- 5204: JsonMessageEncodingStart ---
            Assert.True(firedEvents.ContainsKey("JsonMessageEncodingStart"), "Event JsonMessageEncodingStart not received");
            var eJsonMessageEncodingStart = firedEvents["JsonMessageEncodingStart"];
            Assert.Equal(TestString(5204, "AppDomain"), (string)eJsonMessageEncodingStart["AppDomain"]);

        }
    }
}
