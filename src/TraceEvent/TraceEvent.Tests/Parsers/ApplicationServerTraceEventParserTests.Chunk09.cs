using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.ApplicationServer;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace TraceEventTests.Parsers
{
    public partial class ApplicationServerTraceEventParserTests
    {
        // Template field definitions for chunk 09
        private static readonly TemplateField[] s_chunk09_oneStringsTemplateAFields = new TemplateField[]
        {
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_chunk09_twoStringsTemplateAFields = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_threeStringsTemplateAFields = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("data2", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_fourStringsTemplateAFields = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("data2", FieldType.UnicodeString),
            new TemplateField("data3", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_multidata62TemplateAFields = new TemplateField[]
        {
            new TemplateField("remoteAddress", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_multidata63TemplateAFields = new TemplateField[]
        {
            new TemplateField("websocketId", FieldType.Int32),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_multidata64TemplateAFields = new TemplateField[]
        {
            new TemplateField("errorMessage", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_multidata65TemplateAFields = new TemplateField[]
        {
            new TemplateField("websocketId", FieldType.Int32),
            new TemplateField("byteCount", FieldType.Int32),
            new TemplateField("remoteAddress", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_multidata66TemplateAFields = new TemplateField[]
        {
            new TemplateField("websocketId", FieldType.Int32),
            new TemplateField("remoteAddress", FieldType.UnicodeString),
            new TemplateField("closeStatus", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_multidata67TemplateAFields = new TemplateField[]
        {
            new TemplateField("websocketId", FieldType.Int32),
            new TemplateField("closeStatus", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_multidata68TemplateAFields = new TemplateField[]
        {
            new TemplateField("clientWebSocketFactoryType", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_multidata84TemplateAFields = new TemplateField[]
        {
            new TemplateField("limit", FieldType.Int32),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_multidata85TemplateAFields = new TemplateField[]
        {
            new TemplateField("TrackingProfile", FieldType.UnicodeString),
            new TemplateField("ActivityDefinitionId", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private void WriteMetadata_Chunk09(EventPipeWriterV5 writer, ref int metadataId)
        {
            // EVENT:3410|HttpPipelineFaulted|OneStringsTemplateA|opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "HttpPipelineFaulted", 3410));
            // EVENT:3411|HttpPipelineTimeoutException|OneStringsTemplateA|opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "HttpPipelineTimeoutException", 3411));
            // EVENT:3412|HttpPipelineProcessResponseStart|OneStringsTemplateA|opcode=1
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "HttpPipelineProcessResponseStart", 3412) { OpCode = 1 });
            // EVENT:3413|HttpPipelineBeginProcessResponseStart|OneStringsTemplateA|opcode=1
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "HttpPipelineBeginProcessResponseStart", 3413) { OpCode = 1 });
            // EVENT:3414|HttpPipelineProcessResponseStop|OneStringsTemplateA|opcode=2
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "HttpPipelineProcessResponseStop", 3414) { OpCode = 2 });
            // EVENT:3415|WebSocketConnectionRequestSendStart|Multidata62TemplateA|opcode=1
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "WebSocketConnectionRequestSendStart", 3415) { OpCode = 1 });
            // EVENT:3416|WebSocketConnectionRequestSendStop|Multidata63TemplateA|opcode=2
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "WebSocketConnectionRequestSendStop", 3416) { OpCode = 2 });
            // EVENT:3417|WebSocketConnectionAcceptStart|OneStringsTemplateA|opcode=1
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "WebSocketConnectionAcceptStart", 3417) { OpCode = 1 });
            // EVENT:3418|WebSocketConnectionAccepted|Multidata63TemplateA|opcode=2
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "WebSocketConnectionAccepted", 3418) { OpCode = 2 });
            // EVENT:3419|WebSocketConnectionDeclined|Multidata64TemplateA|opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "WebSocketConnectionDeclined", 3419));
            // EVENT:3420|WebSocketConnectionFailed|Multidata64TemplateA|opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "WebSocketConnectionFailed", 3420));
            // EVENT:3421|WebSocketConnectionAborted|Multidata63TemplateA|opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "WebSocketConnectionAborted", 3421));
            // EVENT:3422|WebSocketAsyncWriteStart|Multidata65TemplateA|opcode=1
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "WebSocketAsyncWriteStart", 3422) { OpCode = 1 });
            // EVENT:3423|WebSocketAsyncWriteStop|Multidata63TemplateA|opcode=2
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "WebSocketAsyncWriteStop", 3423) { OpCode = 2 });
            // EVENT:3424|WebSocketAsyncReadStart|Multidata63TemplateA|opcode=1
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "WebSocketAsyncReadStart", 3424) { OpCode = 1 });
            // EVENT:3425|WebSocketAsyncReadStop|Multidata65TemplateA|opcode=2
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "WebSocketAsyncReadStop", 3425) { OpCode = 2 });
            // EVENT:3426|WebSocketCloseSent|Multidata66TemplateA|opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "WebSocketCloseSent", 3426));
            // EVENT:3427|WebSocketCloseOutputSent|Multidata66TemplateA|opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "WebSocketCloseOutputSent", 3427));
            // EVENT:3428|WebSocketConnectionClosed|Multidata63TemplateA|opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "WebSocketConnectionClosed", 3428));
            // EVENT:3429|WebSocketCloseStatusReceived|Multidata67TemplateA|opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "WebSocketCloseStatusReceived", 3429));
            // EVENT:3430|WebSocketUseVersionFromClientWebSocketFactory|Multidata68TemplateA|opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "WebSocketUseVersionFromClientWebSocketFactory", 3430));
            // EVENT:3431|WebSocketCreateClientWebSocketWithFactory|Multidata68TemplateA|opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "WebSocketCreateClientWebSocketWithFactory", 3431));
            // EVENT:3501|InferredContractDescription|ThreeStringsTemplateA|opcode=69
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "InferredContractDescription", 3501) { OpCode = 69 });
            // EVENT:3502|InferredOperationDescription|FourStringsTemplateA|opcode=70
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "InferredOperationDescription", 3502) { OpCode = 70 });
            // EVENT:3503|DuplicateCorrelationQuery|TwoStringsTemplateA|opcode=28
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "DuplicateCorrelationQuery", 3503) { OpCode = 28 });
            // EVENT:3507|ServiceEndpointAdded|FourStringsTemplateA|opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ServiceEndpointAdded", 3507));
            // EVENT:3508|TrackingProfileNotFound|Multidata85TemplateA|opcode=124
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "TrackingProfileNotFound", 3508) { OpCode = 124 });
            // EVENT:3550|BufferOutOfOrderMessageNoInstance|TwoStringsTemplateA|opcode=11
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "BufferOutOfOrderMessageNoInstance", 3550) { OpCode = 11 });
            // EVENT:3551|BufferOutOfOrderMessageNoBookmark|ThreeStringsTemplateA|opcode=10
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "BufferOutOfOrderMessageNoBookmark", 3551) { OpCode = 10 });
            // EVENT:3552|MaxPendingMessagesPerChannelExceeded|Multidata84TemplateA|opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "MaxPendingMessagesPerChannelExceeded", 3552));
            // EVENT:3553|XamlServicesLoadStart|OneStringsTemplateA|opcode=1
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "XamlServicesLoadStart", 3553) { OpCode = 1 });
            // EVENT:3554|XamlServicesLoadStop|OneStringsTemplateA|opcode=2
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "XamlServicesLoadStop", 3554) { OpCode = 2 });
            // EVENT:3555|CreateWorkflowServiceHostStart|OneStringsTemplateA|opcode=1
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "CreateWorkflowServiceHostStart", 3555) { OpCode = 1 });
            // EVENT:3556|CreateWorkflowServiceHostStop|OneStringsTemplateA|opcode=2
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "CreateWorkflowServiceHostStop", 3556) { OpCode = 2 });
            // EVENT:3557|TransactedReceiveScopeEndCommitFailed|ThreeStringsTemplateA|opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "TransactedReceiveScopeEndCommitFailed", 3557));
            // EVENT:3558|ServiceActivationStart|OneStringsTemplateA|opcode=1
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ServiceActivationStart", 3558) { OpCode = 1 });
            // EVENT:3559|ServiceActivationStop|OneStringsTemplateA|opcode=2
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ServiceActivationStop", 3559) { OpCode = 2 });
        }

        private void WriteEvents_Chunk09(EventPipeWriterV5 writer, ref int metadataId, ref int sequenceNumber)
        {
            int __metadataId = metadataId;
            int __sequenceNumber = sequenceNumber;
            int baseMetadataId = __metadataId;
            writer.WriteEventBlock(w =>
            {
                // 3410: HttpPipelineFaulted - OneStringsTemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 0, 999, __sequenceNumber++, BuildPayload(3410, s_chunk09_oneStringsTemplateAFields));
                // 3411: HttpPipelineTimeoutException - OneStringsTemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 1, 999, __sequenceNumber++, BuildPayload(3411, s_chunk09_oneStringsTemplateAFields));
                // 3412: HttpPipelineProcessResponseStart - OneStringsTemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 2, 999, __sequenceNumber++, BuildPayload(3412, s_chunk09_oneStringsTemplateAFields));
                // 3413: HttpPipelineBeginProcessResponseStart - OneStringsTemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 3, 999, __sequenceNumber++, BuildPayload(3413, s_chunk09_oneStringsTemplateAFields));
                // 3414: HttpPipelineProcessResponseStop - OneStringsTemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 4, 999, __sequenceNumber++, BuildPayload(3414, s_chunk09_oneStringsTemplateAFields));
                // 3415: WebSocketConnectionRequestSendStart - Multidata62TemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 5, 999, __sequenceNumber++, BuildPayload(3415, s_multidata62TemplateAFields));
                // 3416: WebSocketConnectionRequestSendStop - Multidata63TemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 6, 999, __sequenceNumber++, BuildPayload(3416, s_multidata63TemplateAFields));
                // 3417: WebSocketConnectionAcceptStart - OneStringsTemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 7, 999, __sequenceNumber++, BuildPayload(3417, s_chunk09_oneStringsTemplateAFields));
                // 3418: WebSocketConnectionAccepted - Multidata63TemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 8, 999, __sequenceNumber++, BuildPayload(3418, s_multidata63TemplateAFields));
                // 3419: WebSocketConnectionDeclined - Multidata64TemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 9, 999, __sequenceNumber++, BuildPayload(3419, s_multidata64TemplateAFields));
                // 3420: WebSocketConnectionFailed - Multidata64TemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 10, 999, __sequenceNumber++, BuildPayload(3420, s_multidata64TemplateAFields));
                // 3421: WebSocketConnectionAborted - Multidata63TemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 11, 999, __sequenceNumber++, BuildPayload(3421, s_multidata63TemplateAFields));
                // 3422: WebSocketAsyncWriteStart - Multidata65TemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 12, 999, __sequenceNumber++, BuildPayload(3422, s_multidata65TemplateAFields));
                // 3423: WebSocketAsyncWriteStop - Multidata63TemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 13, 999, __sequenceNumber++, BuildPayload(3423, s_multidata63TemplateAFields));
                // 3424: WebSocketAsyncReadStart - Multidata63TemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 14, 999, __sequenceNumber++, BuildPayload(3424, s_multidata63TemplateAFields));
                // 3425: WebSocketAsyncReadStop - Multidata65TemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 15, 999, __sequenceNumber++, BuildPayload(3425, s_multidata65TemplateAFields));
                // 3426: WebSocketCloseSent - Multidata66TemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 16, 999, __sequenceNumber++, BuildPayload(3426, s_multidata66TemplateAFields));
                // 3427: WebSocketCloseOutputSent - Multidata66TemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 17, 999, __sequenceNumber++, BuildPayload(3427, s_multidata66TemplateAFields));
                // 3428: WebSocketConnectionClosed - Multidata63TemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 18, 999, __sequenceNumber++, BuildPayload(3428, s_multidata63TemplateAFields));
                // 3429: WebSocketCloseStatusReceived - Multidata67TemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 19, 999, __sequenceNumber++, BuildPayload(3429, s_multidata67TemplateAFields));
                // 3430: WebSocketUseVersionFromClientWebSocketFactory - Multidata68TemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 20, 999, __sequenceNumber++, BuildPayload(3430, s_multidata68TemplateAFields));
                // 3431: WebSocketCreateClientWebSocketWithFactory - Multidata68TemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 21, 999, __sequenceNumber++, BuildPayload(3431, s_multidata68TemplateAFields));
                // 3501: InferredContractDescription - ThreeStringsTemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 22, 999, __sequenceNumber++, BuildPayload(3501, s_threeStringsTemplateAFields));
                // 3502: InferredOperationDescription - FourStringsTemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 23, 999, __sequenceNumber++, BuildPayload(3502, s_fourStringsTemplateAFields));
                // 3503: DuplicateCorrelationQuery - TwoStringsTemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 24, 999, __sequenceNumber++, BuildPayload(3503, s_chunk09_twoStringsTemplateAFields));
                // 3507: ServiceEndpointAdded - FourStringsTemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 25, 999, __sequenceNumber++, BuildPayload(3507, s_fourStringsTemplateAFields));
                // 3508: TrackingProfileNotFound - Multidata85TemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 26, 999, __sequenceNumber++, BuildPayload(3508, s_multidata85TemplateAFields));
                // 3550: BufferOutOfOrderMessageNoInstance - TwoStringsTemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 27, 999, __sequenceNumber++, BuildPayload(3550, s_chunk09_twoStringsTemplateAFields));
                // 3551: BufferOutOfOrderMessageNoBookmark - ThreeStringsTemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 28, 999, __sequenceNumber++, BuildPayload(3551, s_threeStringsTemplateAFields));
                // 3552: MaxPendingMessagesPerChannelExceeded - Multidata84TemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 29, 999, __sequenceNumber++, BuildPayload(3552, s_multidata84TemplateAFields));
                // 3553: XamlServicesLoadStart - OneStringsTemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 30, 999, __sequenceNumber++, BuildPayload(3553, s_chunk09_oneStringsTemplateAFields));
                // 3554: XamlServicesLoadStop - OneStringsTemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 31, 999, __sequenceNumber++, BuildPayload(3554, s_chunk09_oneStringsTemplateAFields));
                // 3555: CreateWorkflowServiceHostStart - OneStringsTemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 32, 999, __sequenceNumber++, BuildPayload(3555, s_chunk09_oneStringsTemplateAFields));
                // 3556: CreateWorkflowServiceHostStop - OneStringsTemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 33, 999, __sequenceNumber++, BuildPayload(3556, s_chunk09_oneStringsTemplateAFields));
                // 3557: TransactedReceiveScopeEndCommitFailed - ThreeStringsTemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 34, 999, __sequenceNumber++, BuildPayload(3557, s_threeStringsTemplateAFields));
                // 3558: ServiceActivationStart - OneStringsTemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 35, 999, __sequenceNumber++, BuildPayload(3558, s_chunk09_oneStringsTemplateAFields));
                // 3559: ServiceActivationStop - OneStringsTemplateA
                w.WriteEventBlobV4Or5(baseMetadataId + 36, 999, __sequenceNumber++, BuildPayload(3559, s_chunk09_oneStringsTemplateAFields));
            });
        
            __metadataId += 37;
            metadataId = __metadataId;
            sequenceNumber = __sequenceNumber;
        }

        private void Subscribe_Chunk09(ApplicationServerTraceEventParser parser, Dictionary<string, Dictionary<string, object>> firedEvents)
        {
            parser.HttpPipelineFaulted += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["HttpPipelineFaulted"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.HttpPipelineTimeoutException += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["HttpPipelineTimeoutException"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.HttpPipelineProcessResponseStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["HttpPipelineProcessResponseStart"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.HttpPipelineBeginProcessResponseStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["HttpPipelineBeginProcessResponseStart"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.HttpPipelineProcessResponseStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["HttpPipelineProcessResponseStop"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.WebSocketConnectionRequestSendStart += delegate (Multidata62TemplateATraceData data)
            {
                firedEvents["WebSocketConnectionRequestSendStart"] = new Dictionary<string, object>
                {
                    { "remoteAddress", data.remoteAddress },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.WebSocketConnectionRequestSendStop += delegate (Multidata63TemplateATraceData data)
            {
                firedEvents["WebSocketConnectionRequestSendStop"] = new Dictionary<string, object>
                {
                    { "websocketId", data.websocketId },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.WebSocketConnectionAcceptStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["WebSocketConnectionAcceptStart"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.WebSocketConnectionAccepted += delegate (Multidata63TemplateATraceData data)
            {
                firedEvents["WebSocketConnectionAccepted"] = new Dictionary<string, object>
                {
                    { "websocketId", data.websocketId },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.WebSocketConnectionDeclined += delegate (Multidata64TemplateATraceData data)
            {
                firedEvents["WebSocketConnectionDeclined"] = new Dictionary<string, object>
                {
                    { "errorMessage", data.errorMessage },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.WebSocketConnectionFailed += delegate (Multidata64TemplateATraceData data)
            {
                firedEvents["WebSocketConnectionFailed"] = new Dictionary<string, object>
                {
                    { "errorMessage", data.errorMessage },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.WebSocketConnectionAborted += delegate (Multidata63TemplateATraceData data)
            {
                firedEvents["WebSocketConnectionAborted"] = new Dictionary<string, object>
                {
                    { "websocketId", data.websocketId },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.WebSocketAsyncWriteStart += delegate (Multidata65TemplateATraceData data)
            {
                firedEvents["WebSocketAsyncWriteStart"] = new Dictionary<string, object>
                {
                    { "websocketId", data.websocketId },
                    { "byteCount", data.byteCount },
                    { "remoteAddress", data.remoteAddress },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.WebSocketAsyncWriteStop += delegate (Multidata63TemplateATraceData data)
            {
                firedEvents["WebSocketAsyncWriteStop"] = new Dictionary<string, object>
                {
                    { "websocketId", data.websocketId },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.WebSocketAsyncReadStart += delegate (Multidata63TemplateATraceData data)
            {
                firedEvents["WebSocketAsyncReadStart"] = new Dictionary<string, object>
                {
                    { "websocketId", data.websocketId },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.WebSocketAsyncReadStop += delegate (Multidata65TemplateATraceData data)
            {
                firedEvents["WebSocketAsyncReadStop"] = new Dictionary<string, object>
                {
                    { "websocketId", data.websocketId },
                    { "byteCount", data.byteCount },
                    { "remoteAddress", data.remoteAddress },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.WebSocketCloseSent += delegate (Multidata66TemplateATraceData data)
            {
                firedEvents["WebSocketCloseSent"] = new Dictionary<string, object>
                {
                    { "websocketId", data.websocketId },
                    { "remoteAddress", data.remoteAddress },
                    { "closeStatus", data.closeStatus },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.WebSocketCloseOutputSent += delegate (Multidata66TemplateATraceData data)
            {
                firedEvents["WebSocketCloseOutputSent"] = new Dictionary<string, object>
                {
                    { "websocketId", data.websocketId },
                    { "remoteAddress", data.remoteAddress },
                    { "closeStatus", data.closeStatus },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.WebSocketConnectionClosed += delegate (Multidata63TemplateATraceData data)
            {
                firedEvents["WebSocketConnectionClosed"] = new Dictionary<string, object>
                {
                    { "websocketId", data.websocketId },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.WebSocketCloseStatusReceived += delegate (Multidata67TemplateATraceData data)
            {
                firedEvents["WebSocketCloseStatusReceived"] = new Dictionary<string, object>
                {
                    { "websocketId", data.websocketId },
                    { "closeStatus", data.closeStatus },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.WebSocketUseVersionFromClientWebSocketFactory += delegate (Multidata68TemplateATraceData data)
            {
                firedEvents["WebSocketUseVersionFromClientWebSocketFactory"] = new Dictionary<string, object>
                {
                    { "clientWebSocketFactoryType", data.clientWebSocketFactoryType },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.WebSocketCreateClientWebSocketWithFactory += delegate (Multidata68TemplateATraceData data)
            {
                firedEvents["WebSocketCreateClientWebSocketWithFactory"] = new Dictionary<string, object>
                {
                    { "clientWebSocketFactoryType", data.clientWebSocketFactoryType },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.InferredContractDescription += delegate (ThreeStringsTemplateATraceData data)
            {
                firedEvents["InferredContractDescription"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "data2", data.data2 },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.InferredOperationDescription += delegate (FourStringsTemplateATraceData data)
            {
                firedEvents["InferredOperationDescription"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "data2", data.data2 },
                    { "data3", data.data3 },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.DuplicateCorrelationQuery += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents["DuplicateCorrelationQuery"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.ServiceEndpointAdded += delegate (FourStringsTemplateATraceData data)
            {
                firedEvents["ServiceEndpointAdded"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "data2", data.data2 },
                    { "data3", data.data3 },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.TrackingProfileNotFound += delegate (Multidata85TemplateATraceData data)
            {
                firedEvents["TrackingProfileNotFound"] = new Dictionary<string, object>
                {
                    { "TrackingProfile", data.TrackingProfile },
                    { "ActivityDefinitionId", data.ActivityDefinitionId },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.BufferOutOfOrderMessageNoInstance += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents["BufferOutOfOrderMessageNoInstance"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.BufferOutOfOrderMessageNoBookmark += delegate (ThreeStringsTemplateATraceData data)
            {
                firedEvents["BufferOutOfOrderMessageNoBookmark"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "data2", data.data2 },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.MaxPendingMessagesPerChannelExceeded += delegate (Multidata84TemplateATraceData data)
            {
                firedEvents["MaxPendingMessagesPerChannelExceeded"] = new Dictionary<string, object>
                {
                    { "limit", data.limit },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.XamlServicesLoadStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["XamlServicesLoadStart"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.XamlServicesLoadStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["XamlServicesLoadStop"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.CreateWorkflowServiceHostStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["CreateWorkflowServiceHostStart"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.CreateWorkflowServiceHostStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["CreateWorkflowServiceHostStop"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.TransactedReceiveScopeEndCommitFailed += delegate (ThreeStringsTemplateATraceData data)
            {
                firedEvents["TransactedReceiveScopeEndCommitFailed"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "data2", data.data2 },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.ServiceActivationStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["ServiceActivationStart"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.ServiceActivationStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["ServiceActivationStop"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };
        }

        private void Validate_Chunk09(Dictionary<string, Dictionary<string, object>> firedEvents)
        {
            // 3410: HttpPipelineFaulted - OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("HttpPipelineFaulted"), "Event HttpPipelineFaulted did not fire.");
            Assert.Equal(TestString(3410, "AppDomain"), firedEvents["HttpPipelineFaulted"]["AppDomain"]);

            // 3411: HttpPipelineTimeoutException - OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("HttpPipelineTimeoutException"), "Event HttpPipelineTimeoutException did not fire.");
            Assert.Equal(TestString(3411, "AppDomain"), firedEvents["HttpPipelineTimeoutException"]["AppDomain"]);

            // 3412: HttpPipelineProcessResponseStart - OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("HttpPipelineProcessResponseStart"), "Event HttpPipelineProcessResponseStart did not fire.");
            Assert.Equal(TestString(3412, "AppDomain"), firedEvents["HttpPipelineProcessResponseStart"]["AppDomain"]);

            // 3413: HttpPipelineBeginProcessResponseStart - OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("HttpPipelineBeginProcessResponseStart"), "Event HttpPipelineBeginProcessResponseStart did not fire.");
            Assert.Equal(TestString(3413, "AppDomain"), firedEvents["HttpPipelineBeginProcessResponseStart"]["AppDomain"]);

            // 3414: HttpPipelineProcessResponseStop - OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("HttpPipelineProcessResponseStop"), "Event HttpPipelineProcessResponseStop did not fire.");
            Assert.Equal(TestString(3414, "AppDomain"), firedEvents["HttpPipelineProcessResponseStop"]["AppDomain"]);

            // 3415: WebSocketConnectionRequestSendStart - Multidata62TemplateA
            Assert.True(firedEvents.ContainsKey("WebSocketConnectionRequestSendStart"), "Event WebSocketConnectionRequestSendStart did not fire.");
            Assert.Equal(TestString(3415, "remoteAddress"), firedEvents["WebSocketConnectionRequestSendStart"]["remoteAddress"]);
            Assert.Equal(TestString(3415, "AppDomain"), firedEvents["WebSocketConnectionRequestSendStart"]["AppDomain"]);

            // 3416: WebSocketConnectionRequestSendStop - Multidata63TemplateA
            Assert.True(firedEvents.ContainsKey("WebSocketConnectionRequestSendStop"), "Event WebSocketConnectionRequestSendStop did not fire.");
            Assert.Equal(TestInt32(3416, 0), firedEvents["WebSocketConnectionRequestSendStop"]["websocketId"]);
            Assert.Equal(TestString(3416, "AppDomain"), firedEvents["WebSocketConnectionRequestSendStop"]["AppDomain"]);

            // 3417: WebSocketConnectionAcceptStart - OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("WebSocketConnectionAcceptStart"), "Event WebSocketConnectionAcceptStart did not fire.");
            Assert.Equal(TestString(3417, "AppDomain"), firedEvents["WebSocketConnectionAcceptStart"]["AppDomain"]);

            // 3418: WebSocketConnectionAccepted - Multidata63TemplateA
            Assert.True(firedEvents.ContainsKey("WebSocketConnectionAccepted"), "Event WebSocketConnectionAccepted did not fire.");
            Assert.Equal(TestInt32(3418, 0), firedEvents["WebSocketConnectionAccepted"]["websocketId"]);
            Assert.Equal(TestString(3418, "AppDomain"), firedEvents["WebSocketConnectionAccepted"]["AppDomain"]);

            // 3419: WebSocketConnectionDeclined - Multidata64TemplateA
            Assert.True(firedEvents.ContainsKey("WebSocketConnectionDeclined"), "Event WebSocketConnectionDeclined did not fire.");
            Assert.Equal(TestString(3419, "errorMessage"), firedEvents["WebSocketConnectionDeclined"]["errorMessage"]);
            Assert.Equal(TestString(3419, "AppDomain"), firedEvents["WebSocketConnectionDeclined"]["AppDomain"]);

            // 3420: WebSocketConnectionFailed - Multidata64TemplateA
            Assert.True(firedEvents.ContainsKey("WebSocketConnectionFailed"), "Event WebSocketConnectionFailed did not fire.");
            Assert.Equal(TestString(3420, "errorMessage"), firedEvents["WebSocketConnectionFailed"]["errorMessage"]);
            Assert.Equal(TestString(3420, "AppDomain"), firedEvents["WebSocketConnectionFailed"]["AppDomain"]);

            // 3421: WebSocketConnectionAborted - Multidata63TemplateA
            Assert.True(firedEvents.ContainsKey("WebSocketConnectionAborted"), "Event WebSocketConnectionAborted did not fire.");
            Assert.Equal(TestInt32(3421, 0), firedEvents["WebSocketConnectionAborted"]["websocketId"]);
            Assert.Equal(TestString(3421, "AppDomain"), firedEvents["WebSocketConnectionAborted"]["AppDomain"]);

            // 3422: WebSocketAsyncWriteStart - Multidata65TemplateA
            Assert.True(firedEvents.ContainsKey("WebSocketAsyncWriteStart"), "Event WebSocketAsyncWriteStart did not fire.");
            Assert.Equal(TestInt32(3422, 0), firedEvents["WebSocketAsyncWriteStart"]["websocketId"]);
            Assert.Equal(TestInt32(3422, 1), firedEvents["WebSocketAsyncWriteStart"]["byteCount"]);
            Assert.Equal(TestString(3422, "remoteAddress"), firedEvents["WebSocketAsyncWriteStart"]["remoteAddress"]);
            Assert.Equal(TestString(3422, "AppDomain"), firedEvents["WebSocketAsyncWriteStart"]["AppDomain"]);

            // 3423: WebSocketAsyncWriteStop - Multidata63TemplateA
            Assert.True(firedEvents.ContainsKey("WebSocketAsyncWriteStop"), "Event WebSocketAsyncWriteStop did not fire.");
            Assert.Equal(TestInt32(3423, 0), firedEvents["WebSocketAsyncWriteStop"]["websocketId"]);
            Assert.Equal(TestString(3423, "AppDomain"), firedEvents["WebSocketAsyncWriteStop"]["AppDomain"]);

            // 3424: WebSocketAsyncReadStart - Multidata63TemplateA
            Assert.True(firedEvents.ContainsKey("WebSocketAsyncReadStart"), "Event WebSocketAsyncReadStart did not fire.");
            Assert.Equal(TestInt32(3424, 0), firedEvents["WebSocketAsyncReadStart"]["websocketId"]);
            Assert.Equal(TestString(3424, "AppDomain"), firedEvents["WebSocketAsyncReadStart"]["AppDomain"]);

            // 3425: WebSocketAsyncReadStop - Multidata65TemplateA
            Assert.True(firedEvents.ContainsKey("WebSocketAsyncReadStop"), "Event WebSocketAsyncReadStop did not fire.");
            Assert.Equal(TestInt32(3425, 0), firedEvents["WebSocketAsyncReadStop"]["websocketId"]);
            Assert.Equal(TestInt32(3425, 1), firedEvents["WebSocketAsyncReadStop"]["byteCount"]);
            Assert.Equal(TestString(3425, "remoteAddress"), firedEvents["WebSocketAsyncReadStop"]["remoteAddress"]);
            Assert.Equal(TestString(3425, "AppDomain"), firedEvents["WebSocketAsyncReadStop"]["AppDomain"]);

            // 3426: WebSocketCloseSent - Multidata66TemplateA
            Assert.True(firedEvents.ContainsKey("WebSocketCloseSent"), "Event WebSocketCloseSent did not fire.");
            Assert.Equal(TestInt32(3426, 0), firedEvents["WebSocketCloseSent"]["websocketId"]);
            Assert.Equal(TestString(3426, "remoteAddress"), firedEvents["WebSocketCloseSent"]["remoteAddress"]);
            Assert.Equal(TestString(3426, "closeStatus"), firedEvents["WebSocketCloseSent"]["closeStatus"]);
            Assert.Equal(TestString(3426, "AppDomain"), firedEvents["WebSocketCloseSent"]["AppDomain"]);

            // 3427: WebSocketCloseOutputSent - Multidata66TemplateA
            Assert.True(firedEvents.ContainsKey("WebSocketCloseOutputSent"), "Event WebSocketCloseOutputSent did not fire.");
            Assert.Equal(TestInt32(3427, 0), firedEvents["WebSocketCloseOutputSent"]["websocketId"]);
            Assert.Equal(TestString(3427, "remoteAddress"), firedEvents["WebSocketCloseOutputSent"]["remoteAddress"]);
            Assert.Equal(TestString(3427, "closeStatus"), firedEvents["WebSocketCloseOutputSent"]["closeStatus"]);
            Assert.Equal(TestString(3427, "AppDomain"), firedEvents["WebSocketCloseOutputSent"]["AppDomain"]);

            // 3428: WebSocketConnectionClosed - Multidata63TemplateA
            Assert.True(firedEvents.ContainsKey("WebSocketConnectionClosed"), "Event WebSocketConnectionClosed did not fire.");
            Assert.Equal(TestInt32(3428, 0), firedEvents["WebSocketConnectionClosed"]["websocketId"]);
            Assert.Equal(TestString(3428, "AppDomain"), firedEvents["WebSocketConnectionClosed"]["AppDomain"]);

            // 3429: WebSocketCloseStatusReceived - Multidata67TemplateA
            Assert.True(firedEvents.ContainsKey("WebSocketCloseStatusReceived"), "Event WebSocketCloseStatusReceived did not fire.");
            Assert.Equal(TestInt32(3429, 0), firedEvents["WebSocketCloseStatusReceived"]["websocketId"]);
            Assert.Equal(TestString(3429, "closeStatus"), firedEvents["WebSocketCloseStatusReceived"]["closeStatus"]);
            Assert.Equal(TestString(3429, "AppDomain"), firedEvents["WebSocketCloseStatusReceived"]["AppDomain"]);

            // 3430: WebSocketUseVersionFromClientWebSocketFactory - Multidata68TemplateA
            Assert.True(firedEvents.ContainsKey("WebSocketUseVersionFromClientWebSocketFactory"), "Event WebSocketUseVersionFromClientWebSocketFactory did not fire.");
            Assert.Equal(TestString(3430, "clientWebSocketFactoryType"), firedEvents["WebSocketUseVersionFromClientWebSocketFactory"]["clientWebSocketFactoryType"]);
            Assert.Equal(TestString(3430, "AppDomain"), firedEvents["WebSocketUseVersionFromClientWebSocketFactory"]["AppDomain"]);

            // 3431: WebSocketCreateClientWebSocketWithFactory - Multidata68TemplateA
            Assert.True(firedEvents.ContainsKey("WebSocketCreateClientWebSocketWithFactory"), "Event WebSocketCreateClientWebSocketWithFactory did not fire.");
            Assert.Equal(TestString(3431, "clientWebSocketFactoryType"), firedEvents["WebSocketCreateClientWebSocketWithFactory"]["clientWebSocketFactoryType"]);
            Assert.Equal(TestString(3431, "AppDomain"), firedEvents["WebSocketCreateClientWebSocketWithFactory"]["AppDomain"]);

            // 3501: InferredContractDescription - ThreeStringsTemplateA
            Assert.True(firedEvents.ContainsKey("InferredContractDescription"), "Event InferredContractDescription did not fire.");
            Assert.Equal(TestString(3501, "data1"), firedEvents["InferredContractDescription"]["data1"]);
            Assert.Equal(TestString(3501, "data2"), firedEvents["InferredContractDescription"]["data2"]);
            Assert.Equal(TestString(3501, "AppDomain"), firedEvents["InferredContractDescription"]["AppDomain"]);

            // 3502: InferredOperationDescription - FourStringsTemplateA
            Assert.True(firedEvents.ContainsKey("InferredOperationDescription"), "Event InferredOperationDescription did not fire.");
            Assert.Equal(TestString(3502, "data1"), firedEvents["InferredOperationDescription"]["data1"]);
            Assert.Equal(TestString(3502, "data2"), firedEvents["InferredOperationDescription"]["data2"]);
            Assert.Equal(TestString(3502, "data3"), firedEvents["InferredOperationDescription"]["data3"]);
            Assert.Equal(TestString(3502, "AppDomain"), firedEvents["InferredOperationDescription"]["AppDomain"]);

            // 3503: DuplicateCorrelationQuery - TwoStringsTemplateA
            Assert.True(firedEvents.ContainsKey("DuplicateCorrelationQuery"), "Event DuplicateCorrelationQuery did not fire.");
            Assert.Equal(TestString(3503, "data1"), firedEvents["DuplicateCorrelationQuery"]["data1"]);
            Assert.Equal(TestString(3503, "AppDomain"), firedEvents["DuplicateCorrelationQuery"]["AppDomain"]);

            // 3507: ServiceEndpointAdded - FourStringsTemplateA
            Assert.True(firedEvents.ContainsKey("ServiceEndpointAdded"), "Event ServiceEndpointAdded did not fire.");
            Assert.Equal(TestString(3507, "data1"), firedEvents["ServiceEndpointAdded"]["data1"]);
            Assert.Equal(TestString(3507, "data2"), firedEvents["ServiceEndpointAdded"]["data2"]);
            Assert.Equal(TestString(3507, "data3"), firedEvents["ServiceEndpointAdded"]["data3"]);
            Assert.Equal(TestString(3507, "AppDomain"), firedEvents["ServiceEndpointAdded"]["AppDomain"]);

            // 3508: TrackingProfileNotFound - Multidata85TemplateA
            Assert.True(firedEvents.ContainsKey("TrackingProfileNotFound"), "Event TrackingProfileNotFound did not fire.");
            Assert.Equal(TestString(3508, "TrackingProfile"), firedEvents["TrackingProfileNotFound"]["TrackingProfile"]);
            Assert.Equal(TestString(3508, "ActivityDefinitionId"), firedEvents["TrackingProfileNotFound"]["ActivityDefinitionId"]);
            Assert.Equal(TestString(3508, "AppDomain"), firedEvents["TrackingProfileNotFound"]["AppDomain"]);

            // 3550: BufferOutOfOrderMessageNoInstance - TwoStringsTemplateA
            Assert.True(firedEvents.ContainsKey("BufferOutOfOrderMessageNoInstance"), "Event BufferOutOfOrderMessageNoInstance did not fire.");
            Assert.Equal(TestString(3550, "data1"), firedEvents["BufferOutOfOrderMessageNoInstance"]["data1"]);
            Assert.Equal(TestString(3550, "AppDomain"), firedEvents["BufferOutOfOrderMessageNoInstance"]["AppDomain"]);

            // 3551: BufferOutOfOrderMessageNoBookmark - ThreeStringsTemplateA
            Assert.True(firedEvents.ContainsKey("BufferOutOfOrderMessageNoBookmark"), "Event BufferOutOfOrderMessageNoBookmark did not fire.");
            Assert.Equal(TestString(3551, "data1"), firedEvents["BufferOutOfOrderMessageNoBookmark"]["data1"]);
            Assert.Equal(TestString(3551, "data2"), firedEvents["BufferOutOfOrderMessageNoBookmark"]["data2"]);
            Assert.Equal(TestString(3551, "AppDomain"), firedEvents["BufferOutOfOrderMessageNoBookmark"]["AppDomain"]);

            // 3552: MaxPendingMessagesPerChannelExceeded - Multidata84TemplateA
            Assert.True(firedEvents.ContainsKey("MaxPendingMessagesPerChannelExceeded"), "Event MaxPendingMessagesPerChannelExceeded did not fire.");
            Assert.Equal(TestInt32(3552, 0), firedEvents["MaxPendingMessagesPerChannelExceeded"]["limit"]);
            Assert.Equal(TestString(3552, "AppDomain"), firedEvents["MaxPendingMessagesPerChannelExceeded"]["AppDomain"]);

            // 3553: XamlServicesLoadStart - OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("XamlServicesLoadStart"), "Event XamlServicesLoadStart did not fire.");
            Assert.Equal(TestString(3553, "AppDomain"), firedEvents["XamlServicesLoadStart"]["AppDomain"]);

            // 3554: XamlServicesLoadStop - OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("XamlServicesLoadStop"), "Event XamlServicesLoadStop did not fire.");
            Assert.Equal(TestString(3554, "AppDomain"), firedEvents["XamlServicesLoadStop"]["AppDomain"]);

            // 3555: CreateWorkflowServiceHostStart - OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("CreateWorkflowServiceHostStart"), "Event CreateWorkflowServiceHostStart did not fire.");
            Assert.Equal(TestString(3555, "AppDomain"), firedEvents["CreateWorkflowServiceHostStart"]["AppDomain"]);

            // 3556: CreateWorkflowServiceHostStop - OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("CreateWorkflowServiceHostStop"), "Event CreateWorkflowServiceHostStop did not fire.");
            Assert.Equal(TestString(3556, "AppDomain"), firedEvents["CreateWorkflowServiceHostStop"]["AppDomain"]);

            // 3557: TransactedReceiveScopeEndCommitFailed - ThreeStringsTemplateA
            Assert.True(firedEvents.ContainsKey("TransactedReceiveScopeEndCommitFailed"), "Event TransactedReceiveScopeEndCommitFailed did not fire.");
            Assert.Equal(TestString(3557, "data1"), firedEvents["TransactedReceiveScopeEndCommitFailed"]["data1"]);
            Assert.Equal(TestString(3557, "data2"), firedEvents["TransactedReceiveScopeEndCommitFailed"]["data2"]);
            Assert.Equal(TestString(3557, "AppDomain"), firedEvents["TransactedReceiveScopeEndCommitFailed"]["AppDomain"]);

            // 3558: ServiceActivationStart - OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("ServiceActivationStart"), "Event ServiceActivationStart did not fire.");
            Assert.Equal(TestString(3558, "AppDomain"), firedEvents["ServiceActivationStart"]["AppDomain"]);

            // 3559: ServiceActivationStop - OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("ServiceActivationStop"), "Event ServiceActivationStop did not fire.");
            Assert.Equal(TestString(3559, "AppDomain"), firedEvents["ServiceActivationStop"]["AppDomain"]);
        }
    }
}
