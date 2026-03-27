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
        // Template field definitions for Chunk 05

        private static readonly TemplateField[] OneStringsTemplateAFields = new TemplateField[]
        {
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] TwoStringsTemplateAFields = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] ThreeStringsTemplateAFields = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("data2", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata56TemplateAFields = new TemplateField[]
        {
            new TemplateField("msg", FieldType.UnicodeString),
            new TemplateField("key", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata57TemplateAFields = new TemplateField[]
        {
            new TemplateField("msg", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata58TemplateAFields = new TemplateField[]
        {
            new TemplateField("cur", FieldType.Int32),
            new TemplateField("max", FieldType.Int32),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata59TemplateAFields = new TemplateField[]
        {
            new TemplateField("itemTypeName", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        /// <summary>
        /// Writes metadata entries for all Chunk 05 events into the synthetic trace.
        /// </summary>
        private void WriteMetadata_Chunk05(EventPipeWriterV5 writer, ref int metadataId)
        {
            // 1148 FlowchartSwitchCaseNotFound (TwoStringsTemplateA, opcode 63)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "FlowchartSwitchCaseNotFound", 1148) { OpCode = 63 });
            // 1150 CompensationState (ThreeStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "CompensationState", 1150));
            // 1223 SwitchCaseNotFound (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SwitchCaseNotFound", 1223));
            // 1400 ChannelInitializationTimeout (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ChannelInitializationTimeout", 1400));
            // 1401 CloseTimeout (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "CloseTimeout", 1401));
            // 1402 IdleTimeout (Multidata56TemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "IdleTimeout", 1402));
            // 1403 LeaseTimeout (Multidata56TemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "LeaseTimeout", 1403));
            // 1405 OpenTimeout (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "OpenTimeout", 1405));
            // 1406 ReceiveTimeout (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ReceiveTimeout", 1406));
            // 1407 SendTimeout (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SendTimeout", 1407));
            // 1409 InactivityTimeout (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "InactivityTimeout", 1409));
            // 1416 MaxReceivedMessageSizeExceeded (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "MaxReceivedMessageSizeExceeded", 1416));
            // 1417 MaxSentMessageSizeExceeded (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "MaxSentMessageSizeExceeded", 1417));
            // 1418 MaxOutboundConnectionsPerEndpointExceeded (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "MaxOutboundConnectionsPerEndpointExceeded", 1418));
            // 1419 MaxPendingConnectionsExceeded (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "MaxPendingConnectionsExceeded", 1419));
            // 1420 ReaderQuotaExceeded (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ReaderQuotaExceeded", 1420));
            // 1422 NegotiateTokenAuthenticatorStateCacheExceeded (Multidata57TemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "NegotiateTokenAuthenticatorStateCacheExceeded", 1422));
            // 1423 NegotiateTokenAuthenticatorStateCacheRatio (Multidata58TemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "NegotiateTokenAuthenticatorStateCacheRatio", 1423));
            // 1424 SecuritySessionRatio (Multidata58TemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SecuritySessionRatio", 1424));
            // 1430 PendingConnectionsRatio (Multidata58TemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "PendingConnectionsRatio", 1430));
            // 1431 ConcurrentCallsRatio (Multidata58TemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ConcurrentCallsRatio", 1431));
            // 1432 ConcurrentSessionsRatio (Multidata58TemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ConcurrentSessionsRatio", 1432));
            // 1433 OutboundConnectionsPerEndpointRatio (Multidata58TemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "OutboundConnectionsPerEndpointRatio", 1433));
            // 1436 PendingMessagesPerChannelRatio (Multidata58TemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "PendingMessagesPerChannelRatio", 1436));
            // 1438 ConcurrentInstancesRatio (Multidata58TemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ConcurrentInstancesRatio", 1438));
            // 1439 PendingAcceptsAtZero (OneStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "PendingAcceptsAtZero", 1439));
            // 1441 MaxSessionSizeReached (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "MaxSessionSizeReached", 1441));
            // 1442 ReceiveRetryCountReached (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ReceiveRetryCountReached", 1442));
            // 1443 MaxRetryCyclesExceededMsmq (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "MaxRetryCyclesExceededMsmq", 1443));
            // 1445 ReadPoolMiss (Multidata59TemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ReadPoolMiss", 1445));
            // 1446 WritePoolMiss (Multidata59TemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "WritePoolMiss", 1446));
            // 1449 WfMessageReceived (OneStringsTemplateA, opcode 10)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "WfMessageReceived", 1449) { OpCode = 10 });
            // 1450 WfMessageSent (OneStringsTemplateA, opcode 9)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "WfMessageSent", 1450) { OpCode = 9 });
            // 1451 MaxRetryCyclesExceeded (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "MaxRetryCyclesExceeded", 1451));
            // 2021 ExecuteWorkItemStart (OneStringsTemplateA, opcode 1)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ExecuteWorkItemStart", 2021) { OpCode = 1 });
            // 2022 ExecuteWorkItemStop (OneStringsTemplateA, opcode 2)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ExecuteWorkItemStop", 2022) { OpCode = 2 });
            // 2023 SendMessageChannelCacheMiss (OneStringsTemplateA, opcode 76)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SendMessageChannelCacheMiss", 2023) { OpCode = 76 });
        }

        /// <summary>
        /// Writes event payloads for all Chunk 05 events into the synthetic trace.
        /// </summary>
        private void WriteEvents_Chunk05(EventPipeWriterV5 writer, ref int metadataId, ref int sequenceNumber)
        {
            int __metadataId = metadataId;
            int __sequenceNumber = sequenceNumber;
            int baseMetadataId = __metadataId;

            // Each event uses the __metadataId assigned during WriteMetadata_Chunk05.
            // The order here must match the metadata order exactly.
            int id = baseMetadataId;

            writer.WriteEventBlock(w =>
            {
                // 1148 FlowchartSwitchCaseNotFound (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1148, TwoStringsTemplateAFields));
                // 1150 CompensationState (ThreeStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1150, ThreeStringsTemplateAFields));
                // 1223 SwitchCaseNotFound (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1223, TwoStringsTemplateAFields));
                // 1400 ChannelInitializationTimeout (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1400, TwoStringsTemplateAFields));
                // 1401 CloseTimeout (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1401, TwoStringsTemplateAFields));
                // 1402 IdleTimeout (Multidata56TemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1402, Multidata56TemplateAFields));
                // 1403 LeaseTimeout (Multidata56TemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1403, Multidata56TemplateAFields));
                // 1405 OpenTimeout (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1405, TwoStringsTemplateAFields));
                // 1406 ReceiveTimeout (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1406, TwoStringsTemplateAFields));
                // 1407 SendTimeout (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1407, TwoStringsTemplateAFields));
                // 1409 InactivityTimeout (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1409, TwoStringsTemplateAFields));
                // 1416 MaxReceivedMessageSizeExceeded (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1416, TwoStringsTemplateAFields));
                // 1417 MaxSentMessageSizeExceeded (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1417, TwoStringsTemplateAFields));
                // 1418 MaxOutboundConnectionsPerEndpointExceeded (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1418, TwoStringsTemplateAFields));
                // 1419 MaxPendingConnectionsExceeded (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1419, TwoStringsTemplateAFields));
                // 1420 ReaderQuotaExceeded (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1420, TwoStringsTemplateAFields));
                // 1422 NegotiateTokenAuthenticatorStateCacheExceeded (Multidata57TemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1422, Multidata57TemplateAFields));
                // 1423 NegotiateTokenAuthenticatorStateCacheRatio (Multidata58TemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1423, Multidata58TemplateAFields));
                // 1424 SecuritySessionRatio (Multidata58TemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1424, Multidata58TemplateAFields));
                // 1430 PendingConnectionsRatio (Multidata58TemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1430, Multidata58TemplateAFields));
                // 1431 ConcurrentCallsRatio (Multidata58TemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1431, Multidata58TemplateAFields));
                // 1432 ConcurrentSessionsRatio (Multidata58TemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1432, Multidata58TemplateAFields));
                // 1433 OutboundConnectionsPerEndpointRatio (Multidata58TemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1433, Multidata58TemplateAFields));
                // 1436 PendingMessagesPerChannelRatio (Multidata58TemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1436, Multidata58TemplateAFields));
                // 1438 ConcurrentInstancesRatio (Multidata58TemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1438, Multidata58TemplateAFields));
                // 1439 PendingAcceptsAtZero (OneStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1439, OneStringsTemplateAFields));
                // 1441 MaxSessionSizeReached (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1441, TwoStringsTemplateAFields));
                // 1442 ReceiveRetryCountReached (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1442, TwoStringsTemplateAFields));
                // 1443 MaxRetryCyclesExceededMsmq (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1443, TwoStringsTemplateAFields));
                // 1445 ReadPoolMiss (Multidata59TemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1445, Multidata59TemplateAFields));
                // 1446 WritePoolMiss (Multidata59TemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1446, Multidata59TemplateAFields));
                // 1449 WfMessageReceived (OneStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1449, OneStringsTemplateAFields));
                // 1450 WfMessageSent (OneStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1450, OneStringsTemplateAFields));
                // 1451 MaxRetryCyclesExceeded (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1451, TwoStringsTemplateAFields));
                // 2021 ExecuteWorkItemStart (OneStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(2021, OneStringsTemplateAFields));
                // 2022 ExecuteWorkItemStop (OneStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(2022, OneStringsTemplateAFields));
                // 2023 SendMessageChannelCacheMiss (OneStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(2023, OneStringsTemplateAFields));
            });

            __metadataId = id;
            metadataId = __metadataId;
            sequenceNumber = __sequenceNumber;
        }

        /// <summary>
        /// Subscribes to all Chunk 05 events, recording payload field values for validation.
        /// </summary>
        private void Subscribe_Chunk05(ApplicationServerTraceEventParser parser, Dictionary<int, Dictionary<string, object>> firedEvents)
        {
            // 1148 FlowchartSwitchCaseNotFound (TwoStringsTemplateA)
            parser.FlowchartSwitchCaseNotFound += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents[1148] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1150 CompensationState (ThreeStringsTemplateA)
            parser.CompensationState += delegate (ThreeStringsTemplateATraceData data)
            {
                firedEvents[1150] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "data2", data.data2 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1223 SwitchCaseNotFound (TwoStringsTemplateA)
            parser.SwitchCaseNotFound += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents[1223] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1400 ChannelInitializationTimeout (TwoStringsTemplateA)
            parser.ChannelInitializationTimeout += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents[1400] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1401 CloseTimeout (TwoStringsTemplateA)
            parser.CloseTimeout += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents[1401] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1402 IdleTimeout (Multidata56TemplateA)
            parser.IdleTimeout += delegate (Multidata56TemplateATraceData data)
            {
                firedEvents[1402] = new Dictionary<string, object>
                {
                    { "msg", data.msg },
                    { "key", data.key },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1403 LeaseTimeout (Multidata56TemplateA)
            parser.LeaseTimeout += delegate (Multidata56TemplateATraceData data)
            {
                firedEvents[1403] = new Dictionary<string, object>
                {
                    { "msg", data.msg },
                    { "key", data.key },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1405 OpenTimeout (TwoStringsTemplateA)
            parser.OpenTimeout += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents[1405] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1406 ReceiveTimeout (TwoStringsTemplateA)
            parser.ReceiveTimeout += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents[1406] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1407 SendTimeout (TwoStringsTemplateA)
            parser.SendTimeout += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents[1407] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1409 InactivityTimeout (TwoStringsTemplateA)
            parser.InactivityTimeout += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents[1409] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1416 MaxReceivedMessageSizeExceeded (TwoStringsTemplateA)
            parser.MaxReceivedMessageSizeExceeded += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents[1416] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1417 MaxSentMessageSizeExceeded (TwoStringsTemplateA)
            parser.MaxSentMessageSizeExceeded += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents[1417] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1418 MaxOutboundConnectionsPerEndpointExceeded (TwoStringsTemplateA)
            parser.MaxOutboundConnectionsPerEndpointExceeded += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents[1418] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1419 MaxPendingConnectionsExceeded (TwoStringsTemplateA)
            parser.MaxPendingConnectionsExceeded += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents[1419] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1420 ReaderQuotaExceeded (TwoStringsTemplateA)
            parser.ReaderQuotaExceeded += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents[1420] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1422 NegotiateTokenAuthenticatorStateCacheExceeded (Multidata57TemplateA)
            parser.NegotiateTokenAuthenticatorStateCacheExceeded += delegate (Multidata57TemplateATraceData data)
            {
                firedEvents[1422] = new Dictionary<string, object>
                {
                    { "msg", data.msg },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1423 NegotiateTokenAuthenticatorStateCacheRatio (Multidata58TemplateA)
            parser.NegotiateTokenAuthenticatorStateCacheRatio += delegate (Multidata58TemplateATraceData data)
            {
                firedEvents[1423] = new Dictionary<string, object>
                {
                    { "cur", data.cur },
                    { "max", data.max },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1424 SecuritySessionRatio (Multidata58TemplateA)
            parser.SecuritySessionRatio += delegate (Multidata58TemplateATraceData data)
            {
                firedEvents[1424] = new Dictionary<string, object>
                {
                    { "cur", data.cur },
                    { "max", data.max },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1430 PendingConnectionsRatio (Multidata58TemplateA)
            parser.PendingConnectionsRatio += delegate (Multidata58TemplateATraceData data)
            {
                firedEvents[1430] = new Dictionary<string, object>
                {
                    { "cur", data.cur },
                    { "max", data.max },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1431 ConcurrentCallsRatio (Multidata58TemplateA)
            parser.ConcurrentCallsRatio += delegate (Multidata58TemplateATraceData data)
            {
                firedEvents[1431] = new Dictionary<string, object>
                {
                    { "cur", data.cur },
                    { "max", data.max },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1432 ConcurrentSessionsRatio (Multidata58TemplateA)
            parser.ConcurrentSessionsRatio += delegate (Multidata58TemplateATraceData data)
            {
                firedEvents[1432] = new Dictionary<string, object>
                {
                    { "cur", data.cur },
                    { "max", data.max },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1433 OutboundConnectionsPerEndpointRatio (Multidata58TemplateA)
            parser.OutboundConnectionsPerEndpointRatio += delegate (Multidata58TemplateATraceData data)
            {
                firedEvents[1433] = new Dictionary<string, object>
                {
                    { "cur", data.cur },
                    { "max", data.max },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1436 PendingMessagesPerChannelRatio (Multidata58TemplateA)
            parser.PendingMessagesPerChannelRatio += delegate (Multidata58TemplateATraceData data)
            {
                firedEvents[1436] = new Dictionary<string, object>
                {
                    { "cur", data.cur },
                    { "max", data.max },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1438 ConcurrentInstancesRatio (Multidata58TemplateA)
            parser.ConcurrentInstancesRatio += delegate (Multidata58TemplateATraceData data)
            {
                firedEvents[1438] = new Dictionary<string, object>
                {
                    { "cur", data.cur },
                    { "max", data.max },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1439 PendingAcceptsAtZero (OneStringsTemplateA)
            parser.PendingAcceptsAtZero += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[1439] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1441 MaxSessionSizeReached (TwoStringsTemplateA)
            parser.MaxSessionSizeReached += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents[1441] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1442 ReceiveRetryCountReached (TwoStringsTemplateA)
            parser.ReceiveRetryCountReached += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents[1442] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1443 MaxRetryCyclesExceededMsmq (TwoStringsTemplateA)
            parser.MaxRetryCyclesExceededMsmq += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents[1443] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1445 ReadPoolMiss (Multidata59TemplateA)
            parser.ReadPoolMiss += delegate (Multidata59TemplateATraceData data)
            {
                firedEvents[1445] = new Dictionary<string, object>
                {
                    { "itemTypeName", data.itemTypeName },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1446 WritePoolMiss (Multidata59TemplateA)
            parser.WritePoolMiss += delegate (Multidata59TemplateATraceData data)
            {
                firedEvents[1446] = new Dictionary<string, object>
                {
                    { "itemTypeName", data.itemTypeName },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1449 WfMessageReceived (OneStringsTemplateA)
            parser.WfMessageReceived += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[1449] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1450 WfMessageSent (OneStringsTemplateA)
            parser.WfMessageSent += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[1450] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            // 1451 MaxRetryCyclesExceeded (TwoStringsTemplateA)
            parser.MaxRetryCyclesExceeded += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents[1451] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 2021 ExecuteWorkItemStart (OneStringsTemplateA)
            parser.ExecuteWorkItemStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[2021] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            // 2022 ExecuteWorkItemStop (OneStringsTemplateA)
            parser.ExecuteWorkItemStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[2022] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            // 2023 SendMessageChannelCacheMiss (OneStringsTemplateA)
            parser.SendMessageChannelCacheMiss += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[2023] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };
        }

        /// <summary>
        /// Validates that all Chunk 05 events fired with correct payload values.
        /// </summary>
        private void Validate_Chunk05(Dictionary<int, Dictionary<string, object>> firedEvents)
        {
            // Helper: validate TwoStringsTemplateA events
            int[] twoStringsEvents = new int[] { 1148, 1223, 1400, 1401, 1405, 1406, 1407, 1409, 1416, 1417, 1418, 1419, 1420, 1441, 1442, 1443, 1451 };
            foreach (int eventId in twoStringsEvents)
            {
                Assert.True(firedEvents.ContainsKey(eventId), $"Event {eventId} did not fire.");
                var fields = firedEvents[eventId];
                Assert.Equal(TestString(eventId, "data1"), (string)fields["data1"]);
                Assert.Equal(TestString(eventId, "AppDomain"), (string)fields["AppDomain"]);
            }

            // 1150 CompensationState (ThreeStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(1150), "Event 1150 did not fire.");
            Assert.Equal(TestString(1150, "data1"), (string)firedEvents[1150]["data1"]);
            Assert.Equal(TestString(1150, "data2"), (string)firedEvents[1150]["data2"]);
            Assert.Equal(TestString(1150, "AppDomain"), (string)firedEvents[1150]["AppDomain"]);

            // Multidata56TemplateA events: 1402, 1403
            int[] multidata56Events = new int[] { 1402, 1403 };
            foreach (int eventId in multidata56Events)
            {
                Assert.True(firedEvents.ContainsKey(eventId), $"Event {eventId} did not fire.");
                var fields = firedEvents[eventId];
                Assert.Equal(TestString(eventId, "msg"), (string)fields["msg"]);
                Assert.Equal(TestString(eventId, "key"), (string)fields["key"]);
                Assert.Equal(TestString(eventId, "AppDomain"), (string)fields["AppDomain"]);
            }

            // 1422 NegotiateTokenAuthenticatorStateCacheExceeded (Multidata57TemplateA)
            Assert.True(firedEvents.ContainsKey(1422), "Event 1422 did not fire.");
            Assert.Equal(TestString(1422, "msg"), (string)firedEvents[1422]["msg"]);
            Assert.Equal(TestString(1422, "AppDomain"), (string)firedEvents[1422]["AppDomain"]);

            // Multidata58TemplateA events: 1423, 1424, 1430, 1431, 1432, 1433, 1436, 1438
            int[] multidata58Events = new int[] { 1423, 1424, 1430, 1431, 1432, 1433, 1436, 1438 };
            foreach (int eventId in multidata58Events)
            {
                Assert.True(firedEvents.ContainsKey(eventId), $"Event {eventId} did not fire.");
                var fields = firedEvents[eventId];
                Assert.Equal(TestInt32(eventId, 0), (int)fields["cur"]);
                Assert.Equal(TestInt32(eventId, 1), (int)fields["max"]);
                Assert.Equal(TestString(eventId, "AppDomain"), (string)fields["AppDomain"]);
            }

            // Multidata59TemplateA events: 1445, 1446
            int[] multidata59Events = new int[] { 1445, 1446 };
            foreach (int eventId in multidata59Events)
            {
                Assert.True(firedEvents.ContainsKey(eventId), $"Event {eventId} did not fire.");
                var fields = firedEvents[eventId];
                Assert.Equal(TestString(eventId, "itemTypeName"), (string)fields["itemTypeName"]);
                Assert.Equal(TestString(eventId, "AppDomain"), (string)fields["AppDomain"]);
            }

            // OneStringsTemplateA events: 1439, 1449, 1450, 2021, 2022, 2023
            int[] oneStringEvents = new int[] { 1439, 1449, 1450, 2021, 2022, 2023 };
            foreach (int eventId in oneStringEvents)
            {
                Assert.True(firedEvents.ContainsKey(eventId), $"Event {eventId} did not fire.");
                var fields = firedEvents[eventId];
                Assert.Equal(TestString(eventId, "AppDomain"), (string)fields["AppDomain"]);
            }
        }
    }
}
