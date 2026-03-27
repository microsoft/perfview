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
                private void Subscribe_Chunk12(ApplicationServerTraceEventParser parser, Dictionary<int, Dictionary<string, object>> firedEvents)
        {
            parser.DiscoveryMessageReceivedAfterOperationCompleted += delegate (Multidata87TemplateATraceData data)
            {
                firedEvents[4804] = new Dictionary<string, object>
                {
                    { "discoveryMessageName", data.discoveryMessageName },
                    { "messageId", data.messageId },
                    { "discoveryOperationName", data.discoveryOperationName },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DiscoveryMessageWithInvalidContent += delegate (Multidata88TemplateATraceData data)
            {
                firedEvents[4805] = new Dictionary<string, object>
                {
                    { "messageType", data.messageType },
                    { "messageId", data.messageId },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DiscoveryMessageWithInvalidRelatesToOrOperationCompleted += delegate (Multidata89TemplateATraceData data)
            {
                firedEvents[4806] = new Dictionary<string, object>
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
                firedEvents[4807] = new Dictionary<string, object>
                {
                    { "messageId", data.messageId },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DiscoveryMessageWithNoContent += delegate (Multidata91TemplateATraceData data)
            {
                firedEvents[4808] = new Dictionary<string, object>
                {
                    { "messageType", data.messageType },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DiscoveryMessageWithNullMessageId += delegate (Multidata91TemplateATraceData data)
            {
                firedEvents[4809] = new Dictionary<string, object>
                {
                    { "messageType", data.messageType },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DiscoveryMessageWithNullMessageSequence += delegate (Multidata92TemplateATraceData data)
            {
                firedEvents[4810] = new Dictionary<string, object>
                {
                    { "discoveryMessageName", data.discoveryMessageName },
                    { "messageId", data.messageId },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DiscoveryMessageWithNullRelatesTo += delegate (Multidata92TemplateATraceData data)
            {
                firedEvents[4811] = new Dictionary<string, object>
                {
                    { "discoveryMessageName", data.discoveryMessageName },
                    { "messageId", data.messageId },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DiscoveryMessageWithNullReplyTo += delegate (Multidata90TemplateATraceData data)
            {
                firedEvents[4812] = new Dictionary<string, object>
                {
                    { "messageId", data.messageId },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DuplicateDiscoveryMessage += delegate (Multidata88TemplateATraceData data)
            {
                firedEvents[4813] = new Dictionary<string, object>
                {
                    { "messageType", data.messageType },
                    { "messageId", data.messageId },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.EndpointDiscoverabilityDisabled += delegate (Multidata93TemplateATraceData data)
            {
                firedEvents[4814] = new Dictionary<string, object>
                {
                    { "endpointAddress", data.endpointAddress },
                    { "listenUri", data.listenUri },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.EndpointDiscoverabilityEnabled += delegate (Multidata93TemplateATraceData data)
            {
                firedEvents[4815] = new Dictionary<string, object>
                {
                    { "endpointAddress", data.endpointAddress },
                    { "listenUri", data.listenUri },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.FindInitiatedInDiscoveryClientChannel += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[4816] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.InnerChannelCreationFailed += delegate (Multidata94TemplateEATraceData data)
            {
                firedEvents[4817] = new Dictionary<string, object>
                {
                    { "endpointAddress", data.endpointAddress },
                    { "via", data.via },
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.InnerChannelOpenFailed += delegate (Multidata94TemplateEATraceData data)
            {
                firedEvents[4818] = new Dictionary<string, object>
                {
                    { "endpointAddress", data.endpointAddress },
                    { "via", data.via },
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.InnerChannelOpenSucceeded += delegate (Multidata95TemplateATraceData data)
            {
                firedEvents[4819] = new Dictionary<string, object>
                {
                    { "endpointAddress", data.endpointAddress },
                    { "via", data.via },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.SynchronizationContextReset += delegate (Multidata96TemplateATraceData data)
            {
                firedEvents[4820] = new Dictionary<string, object>
                {
                    { "synchronizationContextType", data.synchronizationContextType },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.SynchronizationContextSetToNull += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[4821] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DCSerializeWithSurrogateStart += delegate (Multidata97TemplateATraceData data)
            {
                firedEvents[5001] = new Dictionary<string, object>
                {
                    { "SurrogateType", data.SurrogateType },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DCSerializeWithSurrogateStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[5002] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DCDeserializeWithSurrogateStart += delegate (Multidata97TemplateATraceData data)
            {
                firedEvents[5003] = new Dictionary<string, object>
                {
                    { "SurrogateType", data.SurrogateType },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DCDeserializeWithSurrogateStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[5004] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.ImportKnownTypesStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[5005] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.ImportKnownTypesStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[5006] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DCResolverResolve += delegate (Multidata33TemplateATraceData data)
            {
                firedEvents[5007] = new Dictionary<string, object>
                {
                    { "TypeName", data.TypeName },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DCGenWriterStart += delegate (Multidata98TemplateATraceData data)
            {
                firedEvents[5008] = new Dictionary<string, object>
                {
                    { "Kind", data.Kind },
                    { "TypeName", data.TypeName },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DCGenWriterStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[5009] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DCGenReaderStart += delegate (Multidata98TemplateATraceData data)
            {
                firedEvents[5010] = new Dictionary<string, object>
                {
                    { "Kind", data.Kind },
                    { "TypeName", data.TypeName },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DCGenReaderStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[5011] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DCJsonGenReaderStart += delegate (Multidata98TemplateATraceData data)
            {
                firedEvents[5012] = new Dictionary<string, object>
                {
                    { "Kind", data.Kind },
                    { "TypeName", data.TypeName },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DCJsonGenReaderStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[5013] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DCJsonGenWriterStart += delegate (Multidata98TemplateATraceData data)
            {
                firedEvents[5014] = new Dictionary<string, object>
                {
                    { "Kind", data.Kind },
                    { "TypeName", data.TypeName },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DCJsonGenWriterStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[5015] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.GenXmlSerializableStart += delegate (Multidata99TemplateATraceData data)
            {
                firedEvents[5016] = new Dictionary<string, object>
                {
                    { "DCType", data.DCType },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.GenXmlSerializableStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[5017] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.JsonMessageDecodingStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[5203] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.JsonMessageEncodingStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[5204] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

        }


        // =====================================================================
        // Validate_Chunk12: Validate all received event payloads
        // =====================================================================
        private void Validate_Chunk12(Dictionary<int, Dictionary<string, object>> firedEvents)
        {
            // --- 4804: DiscoveryMessageReceivedAfterOperationCompleted ---
            Assert.True(firedEvents.ContainsKey(4804), "Event 4804 (DiscoveryMessageReceivedAfterOperationCompleted) not received");
            var e4804 = firedEvents[4804];
            Assert.Equal(TestString(4804, "discoveryMessageName"), (string)e4804["discoveryMessageName"]);
            Assert.Equal(TestString(4804, "messageId"), (string)e4804["messageId"]);
            Assert.Equal(TestString(4804, "discoveryOperationName"), (string)e4804["discoveryOperationName"]);
            Assert.Equal(TestString(4804, "AppDomain"), (string)e4804["AppDomain"]);

            // --- 4805: DiscoveryMessageWithInvalidContent ---
            Assert.True(firedEvents.ContainsKey(4805), "Event 4805 (DiscoveryMessageWithInvalidContent) not received");
            var e4805 = firedEvents[4805];
            Assert.Equal(TestString(4805, "messageType"), (string)e4805["messageType"]);
            Assert.Equal(TestString(4805, "messageId"), (string)e4805["messageId"]);
            Assert.Equal(TestString(4805, "AppDomain"), (string)e4805["AppDomain"]);

            // --- 4806: DiscoveryMessageWithInvalidRelatesToOrOperationCompleted ---
            Assert.True(firedEvents.ContainsKey(4806), "Event 4806 (DiscoveryMessageWithInvalidRelatesToOrOperationCompleted) not received");
            var e4806 = firedEvents[4806];
            Assert.Equal(TestString(4806, "discoveryMessageName"), (string)e4806["discoveryMessageName"]);
            Assert.Equal(TestString(4806, "messageId"), (string)e4806["messageId"]);
            Assert.Equal(TestString(4806, "relatesTo"), (string)e4806["relatesTo"]);
            Assert.Equal(TestString(4806, "discoveryOperationName"), (string)e4806["discoveryOperationName"]);
            Assert.Equal(TestString(4806, "AppDomain"), (string)e4806["AppDomain"]);

            // --- 4807: DiscoveryMessageWithInvalidReplyTo ---
            Assert.True(firedEvents.ContainsKey(4807), "Event 4807 (DiscoveryMessageWithInvalidReplyTo) not received");
            var e4807 = firedEvents[4807];
            Assert.Equal(TestString(4807, "messageId"), (string)e4807["messageId"]);
            Assert.Equal(TestString(4807, "AppDomain"), (string)e4807["AppDomain"]);

            // --- 4808: DiscoveryMessageWithNoContent ---
            Assert.True(firedEvents.ContainsKey(4808), "Event 4808 (DiscoveryMessageWithNoContent) not received");
            var e4808 = firedEvents[4808];
            Assert.Equal(TestString(4808, "messageType"), (string)e4808["messageType"]);
            Assert.Equal(TestString(4808, "AppDomain"), (string)e4808["AppDomain"]);

            // --- 4809: DiscoveryMessageWithNullMessageId ---
            Assert.True(firedEvents.ContainsKey(4809), "Event 4809 (DiscoveryMessageWithNullMessageId) not received");
            var e4809 = firedEvents[4809];
            Assert.Equal(TestString(4809, "messageType"), (string)e4809["messageType"]);
            Assert.Equal(TestString(4809, "AppDomain"), (string)e4809["AppDomain"]);

            // --- 4810: DiscoveryMessageWithNullMessageSequence ---
            Assert.True(firedEvents.ContainsKey(4810), "Event 4810 (DiscoveryMessageWithNullMessageSequence) not received");
            var e4810 = firedEvents[4810];
            Assert.Equal(TestString(4810, "discoveryMessageName"), (string)e4810["discoveryMessageName"]);
            Assert.Equal(TestString(4810, "messageId"), (string)e4810["messageId"]);
            Assert.Equal(TestString(4810, "AppDomain"), (string)e4810["AppDomain"]);

            // --- 4811: DiscoveryMessageWithNullRelatesTo ---
            Assert.True(firedEvents.ContainsKey(4811), "Event 4811 (DiscoveryMessageWithNullRelatesTo) not received");
            var e4811 = firedEvents[4811];
            Assert.Equal(TestString(4811, "discoveryMessageName"), (string)e4811["discoveryMessageName"]);
            Assert.Equal(TestString(4811, "messageId"), (string)e4811["messageId"]);
            Assert.Equal(TestString(4811, "AppDomain"), (string)e4811["AppDomain"]);

            // --- 4812: DiscoveryMessageWithNullReplyTo ---
            Assert.True(firedEvents.ContainsKey(4812), "Event 4812 (DiscoveryMessageWithNullReplyTo) not received");
            var e4812 = firedEvents[4812];
            Assert.Equal(TestString(4812, "messageId"), (string)e4812["messageId"]);
            Assert.Equal(TestString(4812, "AppDomain"), (string)e4812["AppDomain"]);

            // --- 4813: DuplicateDiscoveryMessage ---
            Assert.True(firedEvents.ContainsKey(4813), "Event 4813 (DuplicateDiscoveryMessage) not received");
            var e4813 = firedEvents[4813];
            Assert.Equal(TestString(4813, "messageType"), (string)e4813["messageType"]);
            Assert.Equal(TestString(4813, "messageId"), (string)e4813["messageId"]);
            Assert.Equal(TestString(4813, "AppDomain"), (string)e4813["AppDomain"]);

            // --- 4814: EndpointDiscoverabilityDisabled ---
            Assert.True(firedEvents.ContainsKey(4814), "Event 4814 (EndpointDiscoverabilityDisabled) not received");
            var e4814 = firedEvents[4814];
            Assert.Equal(TestString(4814, "endpointAddress"), (string)e4814["endpointAddress"]);
            Assert.Equal(TestString(4814, "listenUri"), (string)e4814["listenUri"]);
            Assert.Equal(TestString(4814, "AppDomain"), (string)e4814["AppDomain"]);

            // --- 4815: EndpointDiscoverabilityEnabled ---
            Assert.True(firedEvents.ContainsKey(4815), "Event 4815 (EndpointDiscoverabilityEnabled) not received");
            var e4815 = firedEvents[4815];
            Assert.Equal(TestString(4815, "endpointAddress"), (string)e4815["endpointAddress"]);
            Assert.Equal(TestString(4815, "listenUri"), (string)e4815["listenUri"]);
            Assert.Equal(TestString(4815, "AppDomain"), (string)e4815["AppDomain"]);

            // --- 4816: FindInitiatedInDiscoveryClientChannel ---
            Assert.True(firedEvents.ContainsKey(4816), "Event 4816 (FindInitiatedInDiscoveryClientChannel) not received");
            var e4816 = firedEvents[4816];
            Assert.Equal(TestString(4816, "AppDomain"), (string)e4816["AppDomain"]);

            // --- 4817: InnerChannelCreationFailed ---
            Assert.True(firedEvents.ContainsKey(4817), "Event 4817 (InnerChannelCreationFailed) not received");
            var e4817 = firedEvents[4817];
            Assert.Equal(TestString(4817, "endpointAddress"), (string)e4817["endpointAddress"]);
            Assert.Equal(TestString(4817, "via"), (string)e4817["via"]);
            Assert.Equal(TestString(4817, "SerializedException"), (string)e4817["SerializedException"]);
            Assert.Equal(TestString(4817, "AppDomain"), (string)e4817["AppDomain"]);

            // --- 4818: InnerChannelOpenFailed ---
            Assert.True(firedEvents.ContainsKey(4818), "Event 4818 (InnerChannelOpenFailed) not received");
            var e4818 = firedEvents[4818];
            Assert.Equal(TestString(4818, "endpointAddress"), (string)e4818["endpointAddress"]);
            Assert.Equal(TestString(4818, "via"), (string)e4818["via"]);
            Assert.Equal(TestString(4818, "SerializedException"), (string)e4818["SerializedException"]);
            Assert.Equal(TestString(4818, "AppDomain"), (string)e4818["AppDomain"]);

            // --- 4819: InnerChannelOpenSucceeded ---
            Assert.True(firedEvents.ContainsKey(4819), "Event 4819 (InnerChannelOpenSucceeded) not received");
            var e4819 = firedEvents[4819];
            Assert.Equal(TestString(4819, "endpointAddress"), (string)e4819["endpointAddress"]);
            Assert.Equal(TestString(4819, "via"), (string)e4819["via"]);
            Assert.Equal(TestString(4819, "AppDomain"), (string)e4819["AppDomain"]);

            // --- 4820: SynchronizationContextReset ---
            Assert.True(firedEvents.ContainsKey(4820), "Event 4820 (SynchronizationContextReset) not received");
            var e4820 = firedEvents[4820];
            Assert.Equal(TestString(4820, "synchronizationContextType"), (string)e4820["synchronizationContextType"]);
            Assert.Equal(TestString(4820, "AppDomain"), (string)e4820["AppDomain"]);

            // --- 4821: SynchronizationContextSetToNull ---
            Assert.True(firedEvents.ContainsKey(4821), "Event 4821 (SynchronizationContextSetToNull) not received");
            var e4821 = firedEvents[4821];
            Assert.Equal(TestString(4821, "AppDomain"), (string)e4821["AppDomain"]);

            // --- 5001: DCSerializeWithSurrogateStart ---
            Assert.True(firedEvents.ContainsKey(5001), "Event 5001 (DCSerializeWithSurrogateStart) not received");
            var e5001 = firedEvents[5001];
            Assert.Equal(TestString(5001, "SurrogateType"), (string)e5001["SurrogateType"]);
            Assert.Equal(TestString(5001, "AppDomain"), (string)e5001["AppDomain"]);

            // --- 5002: DCSerializeWithSurrogateStop ---
            Assert.True(firedEvents.ContainsKey(5002), "Event 5002 (DCSerializeWithSurrogateStop) not received");
            var e5002 = firedEvents[5002];
            Assert.Equal(TestString(5002, "AppDomain"), (string)e5002["AppDomain"]);

            // --- 5003: DCDeserializeWithSurrogateStart ---
            Assert.True(firedEvents.ContainsKey(5003), "Event 5003 (DCDeserializeWithSurrogateStart) not received");
            var e5003 = firedEvents[5003];
            Assert.Equal(TestString(5003, "SurrogateType"), (string)e5003["SurrogateType"]);
            Assert.Equal(TestString(5003, "AppDomain"), (string)e5003["AppDomain"]);

            // --- 5004: DCDeserializeWithSurrogateStop ---
            Assert.True(firedEvents.ContainsKey(5004), "Event 5004 (DCDeserializeWithSurrogateStop) not received");
            var e5004 = firedEvents[5004];
            Assert.Equal(TestString(5004, "AppDomain"), (string)e5004["AppDomain"]);

            // --- 5005: ImportKnownTypesStart ---
            Assert.True(firedEvents.ContainsKey(5005), "Event 5005 (ImportKnownTypesStart) not received");
            var e5005 = firedEvents[5005];
            Assert.Equal(TestString(5005, "AppDomain"), (string)e5005["AppDomain"]);

            // --- 5006: ImportKnownTypesStop ---
            Assert.True(firedEvents.ContainsKey(5006), "Event 5006 (ImportKnownTypesStop) not received");
            var e5006 = firedEvents[5006];
            Assert.Equal(TestString(5006, "AppDomain"), (string)e5006["AppDomain"]);

            // --- 5007: DCResolverResolve ---
            Assert.True(firedEvents.ContainsKey(5007), "Event 5007 (DCResolverResolve) not received");
            var e5007 = firedEvents[5007];
            Assert.Equal(TestString(5007, "TypeName"), (string)e5007["TypeName"]);
            Assert.Equal(TestString(5007, "AppDomain"), (string)e5007["AppDomain"]);

            // --- 5008: DCGenWriterStart ---
            Assert.True(firedEvents.ContainsKey(5008), "Event 5008 (DCGenWriterStart) not received");
            var e5008 = firedEvents[5008];
            Assert.Equal(TestString(5008, "Kind"), (string)e5008["Kind"]);
            Assert.Equal(TestString(5008, "TypeName"), (string)e5008["TypeName"]);
            Assert.Equal(TestString(5008, "AppDomain"), (string)e5008["AppDomain"]);

            // --- 5009: DCGenWriterStop ---
            Assert.True(firedEvents.ContainsKey(5009), "Event 5009 (DCGenWriterStop) not received");
            var e5009 = firedEvents[5009];
            Assert.Equal(TestString(5009, "AppDomain"), (string)e5009["AppDomain"]);

            // --- 5010: DCGenReaderStart ---
            Assert.True(firedEvents.ContainsKey(5010), "Event 5010 (DCGenReaderStart) not received");
            var e5010 = firedEvents[5010];
            Assert.Equal(TestString(5010, "Kind"), (string)e5010["Kind"]);
            Assert.Equal(TestString(5010, "TypeName"), (string)e5010["TypeName"]);
            Assert.Equal(TestString(5010, "AppDomain"), (string)e5010["AppDomain"]);

            // --- 5011: DCGenReaderStop ---
            Assert.True(firedEvents.ContainsKey(5011), "Event 5011 (DCGenReaderStop) not received");
            var e5011 = firedEvents[5011];
            Assert.Equal(TestString(5011, "AppDomain"), (string)e5011["AppDomain"]);

            // --- 5012: DCJsonGenReaderStart ---
            Assert.True(firedEvents.ContainsKey(5012), "Event 5012 (DCJsonGenReaderStart) not received");
            var e5012 = firedEvents[5012];
            Assert.Equal(TestString(5012, "Kind"), (string)e5012["Kind"]);
            Assert.Equal(TestString(5012, "TypeName"), (string)e5012["TypeName"]);
            Assert.Equal(TestString(5012, "AppDomain"), (string)e5012["AppDomain"]);

            // --- 5013: DCJsonGenReaderStop ---
            Assert.True(firedEvents.ContainsKey(5013), "Event 5013 (DCJsonGenReaderStop) not received");
            var e5013 = firedEvents[5013];
            Assert.Equal(TestString(5013, "AppDomain"), (string)e5013["AppDomain"]);

            // --- 5014: DCJsonGenWriterStart ---
            Assert.True(firedEvents.ContainsKey(5014), "Event 5014 (DCJsonGenWriterStart) not received");
            var e5014 = firedEvents[5014];
            Assert.Equal(TestString(5014, "Kind"), (string)e5014["Kind"]);
            Assert.Equal(TestString(5014, "TypeName"), (string)e5014["TypeName"]);
            Assert.Equal(TestString(5014, "AppDomain"), (string)e5014["AppDomain"]);

            // --- 5015: DCJsonGenWriterStop ---
            Assert.True(firedEvents.ContainsKey(5015), "Event 5015 (DCJsonGenWriterStop) not received");
            var e5015 = firedEvents[5015];
            Assert.Equal(TestString(5015, "AppDomain"), (string)e5015["AppDomain"]);

            // --- 5016: GenXmlSerializableStart ---
            Assert.True(firedEvents.ContainsKey(5016), "Event 5016 (GenXmlSerializableStart) not received");
            var e5016 = firedEvents[5016];
            Assert.Equal(TestString(5016, "DCType"), (string)e5016["DCType"]);
            Assert.Equal(TestString(5016, "AppDomain"), (string)e5016["AppDomain"]);

            // --- 5017: GenXmlSerializableStop ---
            Assert.True(firedEvents.ContainsKey(5017), "Event 5017 (GenXmlSerializableStop) not received");
            var e5017 = firedEvents[5017];
            Assert.Equal(TestString(5017, "AppDomain"), (string)e5017["AppDomain"]);

            // --- 5203: JsonMessageDecodingStart ---
            Assert.True(firedEvents.ContainsKey(5203), "Event 5203 (JsonMessageDecodingStart) not received");
            var e5203 = firedEvents[5203];
            Assert.Equal(TestString(5203, "AppDomain"), (string)e5203["AppDomain"]);

            // --- 5204: JsonMessageEncodingStart ---
            Assert.True(firedEvents.ContainsKey(5204), "Event 5204 (JsonMessageEncodingStart) not received");
            var e5204 = firedEvents[5204];
            Assert.Equal(TestString(5204, "AppDomain"), (string)e5204["AppDomain"]);

        }
    }
}
