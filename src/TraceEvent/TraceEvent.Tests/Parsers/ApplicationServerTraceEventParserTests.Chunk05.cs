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
            // FlowchartSwitchCaseNotFound (TwoStringsTemplateA, opcode 63)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "FlowchartSwitchCaseNotFound", 1148) { OpCode = 63 });
            // CompensationState (ThreeStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "CompensationState", 1150));
            // SwitchCaseNotFound (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SwitchCaseNotFound", 1223));
            // ChannelInitializationTimeout (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ChannelInitializationTimeout", 1400));
            // CloseTimeout (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "CloseTimeout", 1401));
            // IdleTimeout (Multidata56TemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "IdleTimeout", 1402));
            // LeaseTimeout (Multidata56TemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "LeaseTimeout", 1403));
            // OpenTimeout (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "OpenTimeout", 1405));
            // ReceiveTimeout (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ReceiveTimeout", 1406));
            // SendTimeout (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SendTimeout", 1407));
            // InactivityTimeout (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "InactivityTimeout", 1409));
            // MaxReceivedMessageSizeExceeded (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "MaxReceivedMessageSizeExceeded", 1416));
            // MaxSentMessageSizeExceeded (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "MaxSentMessageSizeExceeded", 1417));
            // MaxOutboundConnectionsPerEndpointExceeded (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "MaxOutboundConnectionsPerEndpointExceeded", 1418));
            // MaxPendingConnectionsExceeded (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "MaxPendingConnectionsExceeded", 1419));
            // ReaderQuotaExceeded (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ReaderQuotaExceeded", 1420));
            // NegotiateTokenAuthenticatorStateCacheExceeded (Multidata57TemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "NegotiateTokenAuthenticatorStateCacheExceeded", 1422));
            // NegotiateTokenAuthenticatorStateCacheRatio (Multidata58TemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "NegotiateTokenAuthenticatorStateCacheRatio", 1423));
            // SecuritySessionRatio (Multidata58TemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SecuritySessionRatio", 1424));
            // PendingConnectionsRatio (Multidata58TemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "PendingConnectionsRatio", 1430));
            // ConcurrentCallsRatio (Multidata58TemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ConcurrentCallsRatio", 1431));
            // ConcurrentSessionsRatio (Multidata58TemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ConcurrentSessionsRatio", 1432));
            // OutboundConnectionsPerEndpointRatio (Multidata58TemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "OutboundConnectionsPerEndpointRatio", 1433));
            // PendingMessagesPerChannelRatio (Multidata58TemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "PendingMessagesPerChannelRatio", 1436));
            // ConcurrentInstancesRatio (Multidata58TemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ConcurrentInstancesRatio", 1438));
            // PendingAcceptsAtZero (OneStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "PendingAcceptsAtZero", 1439));
            // MaxSessionSizeReached (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "MaxSessionSizeReached", 1441));
            // ReceiveRetryCountReached (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ReceiveRetryCountReached", 1442));
            // MaxRetryCyclesExceededMsmq (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "MaxRetryCyclesExceededMsmq", 1443));
            // ReadPoolMiss (Multidata59TemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ReadPoolMiss", 1445));
            // WritePoolMiss (Multidata59TemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "WritePoolMiss", 1446));
            // WfMessageReceived (OneStringsTemplateA, opcode 10)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "WfMessageReceived", 1449) { OpCode = 10 });
            // WfMessageSent (OneStringsTemplateA, opcode 9)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "WfMessageSent", 1450) { OpCode = 9 });
            // MaxRetryCyclesExceeded (TwoStringsTemplateA, opcode 0)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "MaxRetryCyclesExceeded", 1451));
            // ExecuteWorkItemStart (OneStringsTemplateA, opcode 1)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ExecuteWorkItemStart", 2021) { OpCode = 1 });
            // ExecuteWorkItemStop (OneStringsTemplateA, opcode 2)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ExecuteWorkItemStop", 2022) { OpCode = 2 });
            // SendMessageChannelCacheMiss (OneStringsTemplateA, opcode 76)
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
                // FlowchartSwitchCaseNotFound (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1148, TwoStringsTemplateAFields));
                // CompensationState (ThreeStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1150, ThreeStringsTemplateAFields));
                // SwitchCaseNotFound (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1223, TwoStringsTemplateAFields));
                // ChannelInitializationTimeout (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1400, TwoStringsTemplateAFields));
                // CloseTimeout (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1401, TwoStringsTemplateAFields));
                // IdleTimeout (Multidata56TemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1402, Multidata56TemplateAFields));
                // LeaseTimeout (Multidata56TemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1403, Multidata56TemplateAFields));
                // OpenTimeout (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1405, TwoStringsTemplateAFields));
                // ReceiveTimeout (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1406, TwoStringsTemplateAFields));
                // SendTimeout (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1407, TwoStringsTemplateAFields));
                // InactivityTimeout (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1409, TwoStringsTemplateAFields));
                // MaxReceivedMessageSizeExceeded (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1416, TwoStringsTemplateAFields));
                // MaxSentMessageSizeExceeded (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1417, TwoStringsTemplateAFields));
                // MaxOutboundConnectionsPerEndpointExceeded (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1418, TwoStringsTemplateAFields));
                // MaxPendingConnectionsExceeded (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1419, TwoStringsTemplateAFields));
                // ReaderQuotaExceeded (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1420, TwoStringsTemplateAFields));
                // NegotiateTokenAuthenticatorStateCacheExceeded (Multidata57TemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1422, Multidata57TemplateAFields));
                // NegotiateTokenAuthenticatorStateCacheRatio (Multidata58TemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1423, Multidata58TemplateAFields));
                // SecuritySessionRatio (Multidata58TemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1424, Multidata58TemplateAFields));
                // PendingConnectionsRatio (Multidata58TemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1430, Multidata58TemplateAFields));
                // ConcurrentCallsRatio (Multidata58TemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1431, Multidata58TemplateAFields));
                // ConcurrentSessionsRatio (Multidata58TemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1432, Multidata58TemplateAFields));
                // OutboundConnectionsPerEndpointRatio (Multidata58TemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1433, Multidata58TemplateAFields));
                // PendingMessagesPerChannelRatio (Multidata58TemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1436, Multidata58TemplateAFields));
                // ConcurrentInstancesRatio (Multidata58TemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1438, Multidata58TemplateAFields));
                // PendingAcceptsAtZero (OneStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1439, OneStringsTemplateAFields));
                // MaxSessionSizeReached (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1441, TwoStringsTemplateAFields));
                // ReceiveRetryCountReached (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1442, TwoStringsTemplateAFields));
                // MaxRetryCyclesExceededMsmq (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1443, TwoStringsTemplateAFields));
                // ReadPoolMiss (Multidata59TemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1445, Multidata59TemplateAFields));
                // WritePoolMiss (Multidata59TemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1446, Multidata59TemplateAFields));
                // WfMessageReceived (OneStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1449, OneStringsTemplateAFields));
                // WfMessageSent (OneStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1450, OneStringsTemplateAFields));
                // MaxRetryCyclesExceeded (TwoStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(1451, TwoStringsTemplateAFields));
                // ExecuteWorkItemStart (OneStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(2021, OneStringsTemplateAFields));
                // ExecuteWorkItemStop (OneStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(2022, OneStringsTemplateAFields));
                // SendMessageChannelCacheMiss (OneStringsTemplateA)
                w.WriteEventBlobV4Or5(id++, 999, __sequenceNumber++, BuildPayload(2023, OneStringsTemplateAFields));
            });

            __metadataId = id;
            metadataId = __metadataId;
            sequenceNumber = __sequenceNumber;
        }

        /// <summary>
        /// Subscribes to all Chunk 05 events, recording payload field values for validation.
        /// </summary>
        private void Subscribe_Chunk05(ApplicationServerTraceEventParser parser, Dictionary<string, Dictionary<string, object>> firedEvents)
        {
            // FlowchartSwitchCaseNotFound (TwoStringsTemplateA)
            parser.FlowchartSwitchCaseNotFound += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents["FlowchartSwitchCaseNotFound"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // CompensationState (ThreeStringsTemplateA)
            parser.CompensationState += delegate (ThreeStringsTemplateATraceData data)
            {
                firedEvents["CompensationState"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "data2", data.data2 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // SwitchCaseNotFound (TwoStringsTemplateA)
            parser.SwitchCaseNotFound += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents["SwitchCaseNotFound"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // ChannelInitializationTimeout (TwoStringsTemplateA)
            parser.ChannelInitializationTimeout += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents["ChannelInitializationTimeout"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // CloseTimeout (TwoStringsTemplateA)
            parser.CloseTimeout += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents["CloseTimeout"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // IdleTimeout (Multidata56TemplateA)
            parser.IdleTimeout += delegate (Multidata56TemplateATraceData data)
            {
                firedEvents["IdleTimeout"] = new Dictionary<string, object>
                {
                    { "msg", data.msg },
                    { "key", data.key },
                    { "AppDomain", data.AppDomain },
                };
            };

            // LeaseTimeout (Multidata56TemplateA)
            parser.LeaseTimeout += delegate (Multidata56TemplateATraceData data)
            {
                firedEvents["LeaseTimeout"] = new Dictionary<string, object>
                {
                    { "msg", data.msg },
                    { "key", data.key },
                    { "AppDomain", data.AppDomain },
                };
            };

            // OpenTimeout (TwoStringsTemplateA)
            parser.OpenTimeout += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents["OpenTimeout"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // ReceiveTimeout (TwoStringsTemplateA)
            parser.ReceiveTimeout += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents["ReceiveTimeout"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // SendTimeout (TwoStringsTemplateA)
            parser.SendTimeout += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents["SendTimeout"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // InactivityTimeout (TwoStringsTemplateA)
            parser.InactivityTimeout += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents["InactivityTimeout"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // MaxReceivedMessageSizeExceeded (TwoStringsTemplateA)
            parser.MaxReceivedMessageSizeExceeded += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents["MaxReceivedMessageSizeExceeded"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // MaxSentMessageSizeExceeded (TwoStringsTemplateA)
            parser.MaxSentMessageSizeExceeded += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents["MaxSentMessageSizeExceeded"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // MaxOutboundConnectionsPerEndpointExceeded (TwoStringsTemplateA)
            parser.MaxOutboundConnectionsPerEndpointExceeded += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents["MaxOutboundConnectionsPerEndpointExceeded"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // MaxPendingConnectionsExceeded (TwoStringsTemplateA)
            parser.MaxPendingConnectionsExceeded += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents["MaxPendingConnectionsExceeded"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // ReaderQuotaExceeded (TwoStringsTemplateA)
            parser.ReaderQuotaExceeded += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents["ReaderQuotaExceeded"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // NegotiateTokenAuthenticatorStateCacheExceeded (Multidata57TemplateA)
            parser.NegotiateTokenAuthenticatorStateCacheExceeded += delegate (Multidata57TemplateATraceData data)
            {
                firedEvents["NegotiateTokenAuthenticatorStateCacheExceeded"] = new Dictionary<string, object>
                {
                    { "msg", data.msg },
                    { "AppDomain", data.AppDomain },
                };
            };

            // NegotiateTokenAuthenticatorStateCacheRatio (Multidata58TemplateA)
            parser.NegotiateTokenAuthenticatorStateCacheRatio += delegate (Multidata58TemplateATraceData data)
            {
                firedEvents["NegotiateTokenAuthenticatorStateCacheRatio"] = new Dictionary<string, object>
                {
                    { "cur", data.cur },
                    { "max", data.max },
                    { "AppDomain", data.AppDomain },
                };
            };

            // SecuritySessionRatio (Multidata58TemplateA)
            parser.SecuritySessionRatio += delegate (Multidata58TemplateATraceData data)
            {
                firedEvents["SecuritySessionRatio"] = new Dictionary<string, object>
                {
                    { "cur", data.cur },
                    { "max", data.max },
                    { "AppDomain", data.AppDomain },
                };
            };

            // PendingConnectionsRatio (Multidata58TemplateA)
            parser.PendingConnectionsRatio += delegate (Multidata58TemplateATraceData data)
            {
                firedEvents["PendingConnectionsRatio"] = new Dictionary<string, object>
                {
                    { "cur", data.cur },
                    { "max", data.max },
                    { "AppDomain", data.AppDomain },
                };
            };

            // ConcurrentCallsRatio (Multidata58TemplateA)
            parser.ConcurrentCallsRatio += delegate (Multidata58TemplateATraceData data)
            {
                firedEvents["ConcurrentCallsRatio"] = new Dictionary<string, object>
                {
                    { "cur", data.cur },
                    { "max", data.max },
                    { "AppDomain", data.AppDomain },
                };
            };

            // ConcurrentSessionsRatio (Multidata58TemplateA)
            parser.ConcurrentSessionsRatio += delegate (Multidata58TemplateATraceData data)
            {
                firedEvents["ConcurrentSessionsRatio"] = new Dictionary<string, object>
                {
                    { "cur", data.cur },
                    { "max", data.max },
                    { "AppDomain", data.AppDomain },
                };
            };

            // OutboundConnectionsPerEndpointRatio (Multidata58TemplateA)
            parser.OutboundConnectionsPerEndpointRatio += delegate (Multidata58TemplateATraceData data)
            {
                firedEvents["OutboundConnectionsPerEndpointRatio"] = new Dictionary<string, object>
                {
                    { "cur", data.cur },
                    { "max", data.max },
                    { "AppDomain", data.AppDomain },
                };
            };

            // PendingMessagesPerChannelRatio (Multidata58TemplateA)
            parser.PendingMessagesPerChannelRatio += delegate (Multidata58TemplateATraceData data)
            {
                firedEvents["PendingMessagesPerChannelRatio"] = new Dictionary<string, object>
                {
                    { "cur", data.cur },
                    { "max", data.max },
                    { "AppDomain", data.AppDomain },
                };
            };

            // ConcurrentInstancesRatio (Multidata58TemplateA)
            parser.ConcurrentInstancesRatio += delegate (Multidata58TemplateATraceData data)
            {
                firedEvents["ConcurrentInstancesRatio"] = new Dictionary<string, object>
                {
                    { "cur", data.cur },
                    { "max", data.max },
                    { "AppDomain", data.AppDomain },
                };
            };

            // PendingAcceptsAtZero (OneStringsTemplateA)
            parser.PendingAcceptsAtZero += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["PendingAcceptsAtZero"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            // MaxSessionSizeReached (TwoStringsTemplateA)
            parser.MaxSessionSizeReached += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents["MaxSessionSizeReached"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // ReceiveRetryCountReached (TwoStringsTemplateA)
            parser.ReceiveRetryCountReached += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents["ReceiveRetryCountReached"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // MaxRetryCyclesExceededMsmq (TwoStringsTemplateA)
            parser.MaxRetryCyclesExceededMsmq += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents["MaxRetryCyclesExceededMsmq"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // ReadPoolMiss (Multidata59TemplateA)
            parser.ReadPoolMiss += delegate (Multidata59TemplateATraceData data)
            {
                firedEvents["ReadPoolMiss"] = new Dictionary<string, object>
                {
                    { "itemTypeName", data.itemTypeName },
                    { "AppDomain", data.AppDomain },
                };
            };

            // WritePoolMiss (Multidata59TemplateA)
            parser.WritePoolMiss += delegate (Multidata59TemplateATraceData data)
            {
                firedEvents["WritePoolMiss"] = new Dictionary<string, object>
                {
                    { "itemTypeName", data.itemTypeName },
                    { "AppDomain", data.AppDomain },
                };
            };

            // WfMessageReceived (OneStringsTemplateA)
            parser.WfMessageReceived += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["WfMessageReceived"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            // WfMessageSent (OneStringsTemplateA)
            parser.WfMessageSent += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["WfMessageSent"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            // MaxRetryCyclesExceeded (TwoStringsTemplateA)
            parser.MaxRetryCyclesExceeded += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents["MaxRetryCyclesExceeded"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // ExecuteWorkItemStart (OneStringsTemplateA)
            parser.ExecuteWorkItemStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["ExecuteWorkItemStart"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            // ExecuteWorkItemStop (OneStringsTemplateA)
            parser.ExecuteWorkItemStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["ExecuteWorkItemStop"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            // SendMessageChannelCacheMiss (OneStringsTemplateA)
            parser.SendMessageChannelCacheMiss += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["SendMessageChannelCacheMiss"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };
        }

        /// <summary>
        /// Validates that all Chunk 05 events fired with correct payload values.
        /// </summary>
        private void Validate_Chunk05(Dictionary<string, Dictionary<string, object>> firedEvents)
        {
            // Helper: validate TwoStringsTemplateA events
            (int id, string name)[] twoStringsEvents = new[] { (1148, "FlowchartSwitchCaseNotFound"), (1223, "SwitchCaseNotFound"), (1400, "ChannelInitializationTimeout"), (1401, "CloseTimeout"), (1405, "OpenTimeout"), (1406, "ReceiveTimeout"), (1407, "SendTimeout"), (1409, "InactivityTimeout"), (1416, "MaxReceivedMessageSizeExceeded"), (1417, "MaxSentMessageSizeExceeded"), (1418, "MaxOutboundConnectionsPerEndpointExceeded"), (1419, "MaxPendingConnectionsExceeded"), (1420, "ReaderQuotaExceeded"), (1441, "MaxSessionSizeReached"), (1442, "ReceiveRetryCountReached"), (1443, "MaxRetryCyclesExceededMsmq"), (1451, "MaxRetryCyclesExceeded") };
            foreach (var (eventId, eventName) in twoStringsEvents)
            {
                Assert.True(firedEvents.ContainsKey(eventName), $"Event {eventName} did not fire.");
                var fields = firedEvents[eventName];
                Assert.Equal(TestString(eventId, "data1"), (string)fields["data1"]);
                Assert.Equal(TestString(eventId, "AppDomain"), (string)fields["AppDomain"]);
            }

            // CompensationState (ThreeStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("CompensationState"), "Event CompensationState did not fire.");
            Assert.Equal(TestString(1150, "data1"), (string)firedEvents["CompensationState"]["data1"]);
            Assert.Equal(TestString(1150, "data2"), (string)firedEvents["CompensationState"]["data2"]);
            Assert.Equal(TestString(1150, "AppDomain"), (string)firedEvents["CompensationState"]["AppDomain"]);

            // Multidata56TemplateA events: 1402, 1403
            (int id, string name)[] multidata56Events = new[] { (1402, "IdleTimeout"), (1403, "LeaseTimeout") };
            foreach (var (eventId, eventName) in multidata56Events)
            {
                Assert.True(firedEvents.ContainsKey(eventName), $"Event {eventName} did not fire.");
                var fields = firedEvents[eventName];
                Assert.Equal(TestString(eventId, "msg"), (string)fields["msg"]);
                Assert.Equal(TestString(eventId, "key"), (string)fields["key"]);
                Assert.Equal(TestString(eventId, "AppDomain"), (string)fields["AppDomain"]);
            }

            // NegotiateTokenAuthenticatorStateCacheExceeded (Multidata57TemplateA)
            Assert.True(firedEvents.ContainsKey("NegotiateTokenAuthenticatorStateCacheExceeded"), "Event NegotiateTokenAuthenticatorStateCacheExceeded did not fire.");
            Assert.Equal(TestString(1422, "msg"), (string)firedEvents["NegotiateTokenAuthenticatorStateCacheExceeded"]["msg"]);
            Assert.Equal(TestString(1422, "AppDomain"), (string)firedEvents["NegotiateTokenAuthenticatorStateCacheExceeded"]["AppDomain"]);

            // Multidata58TemplateA events: 1423, 1424, 1430, 1431, 1432, 1433, 1436, 1438
            (int id, string name)[] multidata58Events = new[] { (1423, "NegotiateTokenAuthenticatorStateCacheRatio"), (1424, "SecuritySessionRatio"), (1430, "PendingConnectionsRatio"), (1431, "ConcurrentCallsRatio"), (1432, "ConcurrentSessionsRatio"), (1433, "OutboundConnectionsPerEndpointRatio"), (1436, "PendingMessagesPerChannelRatio"), (1438, "ConcurrentInstancesRatio") };
            foreach (var (eventId, eventName) in multidata58Events)
            {
                Assert.True(firedEvents.ContainsKey(eventName), $"Event {eventName} did not fire.");
                var fields = firedEvents[eventName];
                Assert.Equal(TestInt32(eventId, 0), (int)fields["cur"]);
                Assert.Equal(TestInt32(eventId, 1), (int)fields["max"]);
                Assert.Equal(TestString(eventId, "AppDomain"), (string)fields["AppDomain"]);
            }

            // Multidata59TemplateA events: 1445, 1446
            (int id, string name)[] multidata59Events = new[] { (1445, "ReadPoolMiss"), (1446, "WritePoolMiss") };
            foreach (var (eventId, eventName) in multidata59Events)
            {
                Assert.True(firedEvents.ContainsKey(eventName), $"Event {eventName} did not fire.");
                var fields = firedEvents[eventName];
                Assert.Equal(TestString(eventId, "itemTypeName"), (string)fields["itemTypeName"]);
                Assert.Equal(TestString(eventId, "AppDomain"), (string)fields["AppDomain"]);
            }

            // OneStringsTemplateA events: 1439, 1449, 1450, 2021, 2022, 2023
            (int id, string name)[] oneStringEvents = new[] { (1439, "PendingAcceptsAtZero"), (1449, "WfMessageReceived"), (1450, "WfMessageSent"), (2021, "ExecuteWorkItemStart"), (2022, "ExecuteWorkItemStop"), (2023, "SendMessageChannelCacheMiss") };
            foreach (var (eventId, eventName) in oneStringEvents)
            {
                Assert.True(firedEvents.ContainsKey(eventName), $"Event {eventName} did not fire.");
                var fields = firedEvents[eventName];
                Assert.Equal(TestString(eventId, "AppDomain"), (string)fields["AppDomain"]);
            }
        }
    }
}
