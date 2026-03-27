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
        #region Chunk 11 Template Fields

        private static readonly TemplateField[] s_multidata48TemplateAFields = new TemplateField[]
        {
            new TemplateField("Uri", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_multidata80TemplateAFields = new TemplateField[]
        {
            new TemplateField("Uri", FieldType.UnicodeString),
            new TemplateField("Status", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_multidata82TemplateAFields = new TemplateField[]
        {
            new TemplateField("hresult", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_multidata83TemplateAFields = new TemplateField[]
        {
            new TemplateField("curr", FieldType.Int32),
            new TemplateField("max", FieldType.Int32),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_chunk11_oneStringsTemplateAFields = new TemplateField[]
        {
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_chunk11_threeStringsTemplateAFields = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("data2", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_threeStringsTemplateEAFields = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("SerializedException", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_chunk11_twoStringsTemplateAFields = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_twoStringsTemplateEAFields = new TemplateField[]
        {
            new TemplateField("SerializedException", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        #endregion

        #region Chunk 11 Metadata

        private void WriteMetadata_Chunk11(EventPipeWriterV5 writer, ref int metadataId)
        {
            writer.WriteMetadataBlock(
                // 4014 MessageQueueRegisterFailed (Multidata80TemplateA)
                new EventMetadata(metadataId++, ProviderName, "MessageQueueRegisterFailed", 4014),
                // 4015 MessageQueueRegisterCompleted (Multidata48TemplateA)
                new EventMetadata(metadataId++, ProviderName, "MessageQueueRegisterCompleted", 4015),
                // 4016 MessageQueueDuplicatedSocketError (OneStringsTemplateA)
                new EventMetadata(metadataId++, ProviderName, "MessageQueueDuplicatedSocketError", 4016),
                // 4019 MessageQueueDuplicatedSocketComplete (OneStringsTemplateA) opcode=2
                new EventMetadata(metadataId++, ProviderName, "MessageQueueDuplicatedSocketComplete", 4019) { OpCode = 2 },
                // 4020 TcpTransportListenerListeningStart (Multidata48TemplateA) opcode=1
                new EventMetadata(metadataId++, ProviderName, "TcpTransportListenerListeningStart", 4020) { OpCode = 1 },
                // 4021 TcpTransportListenerListeningStop (OneStringsTemplateA) opcode=2
                new EventMetadata(metadataId++, ProviderName, "TcpTransportListenerListeningStop", 4021) { OpCode = 2 },
                // 4022 WebhostUnregisterProtocolFailed (Multidata82TemplateA)
                new EventMetadata(metadataId++, ProviderName, "WebhostUnregisterProtocolFailed", 4022),
                // 4023 WasCloseAllListenerChannelInstancesCompleted (OneStringsTemplateA) opcode=2
                new EventMetadata(metadataId++, ProviderName, "WasCloseAllListenerChannelInstancesCompleted", 4023) { OpCode = 2 },
                // 4024 WasCloseAllListenerChannelInstancesFailed (Multidata82TemplateA) opcode=2
                new EventMetadata(metadataId++, ProviderName, "WasCloseAllListenerChannelInstancesFailed", 4024) { OpCode = 2 },
                // 4025 OpenListenerChannelInstanceFailed (Multidata82TemplateA)
                new EventMetadata(metadataId++, ProviderName, "OpenListenerChannelInstanceFailed", 4025),
                // 4026 WasConnected (OneStringsTemplateA) opcode=132
                new EventMetadata(metadataId++, ProviderName, "WasConnected", 4026) { OpCode = 132 },
                // 4027 WasDisconnected (OneStringsTemplateA) opcode=133
                new EventMetadata(metadataId++, ProviderName, "WasDisconnected", 4027) { OpCode = 133 },
                // 4028 PipeTransportListenerListeningStart (Multidata48TemplateA) opcode=1
                new EventMetadata(metadataId++, ProviderName, "PipeTransportListenerListeningStart", 4028) { OpCode = 1 },
                // 4029 PipeTransportListenerListeningStop (OneStringsTemplateA) opcode=2
                new EventMetadata(metadataId++, ProviderName, "PipeTransportListenerListeningStop", 4029) { OpCode = 2 },
                // 4030 DispatchSessionSuccess (OneStringsTemplateA) opcode=2
                new EventMetadata(metadataId++, ProviderName, "DispatchSessionSuccess", 4030) { OpCode = 2 },
                // 4031 DispatchSessionFailed (OneStringsTemplateA)
                new EventMetadata(metadataId++, ProviderName, "DispatchSessionFailed", 4031),
                // 4032 WasConnectionTimedout (OneStringsTemplateA)
                new EventMetadata(metadataId++, ProviderName, "WasConnectionTimedout", 4032),
                // 4033 RoutingTableLookupStart (OneStringsTemplateA) opcode=1
                new EventMetadata(metadataId++, ProviderName, "RoutingTableLookupStart", 4033) { OpCode = 1 },
                // 4034 RoutingTableLookupStop (OneStringsTemplateA) opcode=2
                new EventMetadata(metadataId++, ProviderName, "RoutingTableLookupStop", 4034) { OpCode = 2 },
                // 4035 PendingSessionQueueRatio (Multidata83TemplateA)
                new EventMetadata(metadataId++, ProviderName, "PendingSessionQueueRatio", 4035),
                // 4201 EndSqlCommandExecute (TwoStringsTemplateA) opcode=2
                new EventMetadata(metadataId++, ProviderName, "EndSqlCommandExecute", 4201) { OpCode = 2 },
                // 4202 StartSqlCommandExecute (TwoStringsTemplateA) opcode=1
                new EventMetadata(metadataId++, ProviderName, "StartSqlCommandExecute", 4202) { OpCode = 1 },
                // 4203 RenewLockSystemError (OneStringsTemplateA)
                new EventMetadata(metadataId++, ProviderName, "RenewLockSystemError", 4203),
                // 4205 FoundProcessingError (ThreeStringsTemplateEA)
                new EventMetadata(metadataId++, ProviderName, "FoundProcessingError", 4205),
                // 4206 UnlockInstanceException (TwoStringsTemplateA)
                new EventMetadata(metadataId++, ProviderName, "UnlockInstanceException", 4206),
                // 4207 MaximumRetriesExceededForSqlCommand (OneStringsTemplateA)
                new EventMetadata(metadataId++, ProviderName, "MaximumRetriesExceededForSqlCommand", 4207),
                // 4208 RetryingSqlCommandDueToSqlError (TwoStringsTemplateA)
                new EventMetadata(metadataId++, ProviderName, "RetryingSqlCommandDueToSqlError", 4208),
                // 4209 TimeoutOpeningSqlConnection (TwoStringsTemplateA)
                new EventMetadata(metadataId++, ProviderName, "TimeoutOpeningSqlConnection", 4209),
                // 4210 SqlExceptionCaught (ThreeStringsTemplateA)
                new EventMetadata(metadataId++, ProviderName, "SqlExceptionCaught", 4210),
                // 4211 QueuingSqlRetry (TwoStringsTemplateA)
                new EventMetadata(metadataId++, ProviderName, "QueuingSqlRetry", 4211),
                // 4212 LockRetryTimeout (TwoStringsTemplateA)
                new EventMetadata(metadataId++, ProviderName, "LockRetryTimeout", 4212),
                // 4213 RunnableInstancesDetectionError (TwoStringsTemplateEA)
                new EventMetadata(metadataId++, ProviderName, "RunnableInstancesDetectionError", 4213),
                // 4214 InstanceLocksRecoveryError (TwoStringsTemplateEA)
                new EventMetadata(metadataId++, ProviderName, "InstanceLocksRecoveryError", 4214),
                // 4600 MessageLogEventSizeExceeded (OneStringsTemplateA)
                new EventMetadata(metadataId++, ProviderName, "MessageLogEventSizeExceeded", 4600),
                // 4801 DiscoveryClientInClientChannelFailedToClose (TwoStringsTemplateEA) opcode=30
                new EventMetadata(metadataId++, ProviderName, "DiscoveryClientInClientChannelFailedToClose", 4801) { OpCode = 30 },
                // 4802 DiscoveryClientProtocolExceptionSuppressed (TwoStringsTemplateEA) opcode=29
                new EventMetadata(metadataId++, ProviderName, "DiscoveryClientProtocolExceptionSuppressed", 4802) { OpCode = 29 },
                // 4803 DiscoveryClientReceivedMulticastSuppression (OneStringsTemplateA) opcode=31
                new EventMetadata(metadataId++, ProviderName, "DiscoveryClientReceivedMulticastSuppression", 4803) { OpCode = 31 }
            );
        }

        #endregion

        #region Chunk 11 Events

        private void WriteEvents_Chunk11(EventPipeWriterV5 writer, ref int metadataId, ref int sequenceNumber)
        {
            int __metadataId = metadataId;
            int __sequenceNumber = sequenceNumber;
            writer.WriteEventBlock(w =>
            {
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4014, s_multidata80TemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4015, s_multidata48TemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4016, s_chunk11_oneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4019, s_chunk11_oneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4020, s_multidata48TemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4021, s_chunk11_oneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4022, s_multidata82TemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4023, s_chunk11_oneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4024, s_multidata82TemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4025, s_multidata82TemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4026, s_chunk11_oneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4027, s_chunk11_oneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4028, s_multidata48TemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4029, s_chunk11_oneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4030, s_chunk11_oneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4031, s_chunk11_oneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4032, s_chunk11_oneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4033, s_chunk11_oneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4034, s_chunk11_oneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4035, s_multidata83TemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4201, s_chunk11_twoStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4202, s_chunk11_twoStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4203, s_chunk11_oneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4205, s_threeStringsTemplateEAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4206, s_chunk11_twoStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4207, s_chunk11_oneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4208, s_chunk11_twoStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4209, s_chunk11_twoStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4210, s_chunk11_threeStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4211, s_chunk11_twoStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4212, s_chunk11_twoStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4213, s_twoStringsTemplateEAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4214, s_twoStringsTemplateEAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4600, s_chunk11_oneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4801, s_twoStringsTemplateEAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4802, s_twoStringsTemplateEAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4803, s_chunk11_oneStringsTemplateAFields));
            });
        
            metadataId = __metadataId;
            sequenceNumber = __sequenceNumber;
        }

        #endregion

        #region Chunk 11 Subscription

        private void Subscribe_Chunk11(ApplicationServerTraceEventParser parser, Dictionary<string, Dictionary<string, object>> firedEvents)
        {
            // 4014 MessageQueueRegisterFailed (Multidata80TemplateA)
            parser.MessageQueueRegisterFailed += delegate (Multidata80TemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["Uri"] = data.Uri;
                fields["Status"] = data.Status;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["MessageQueueRegisterFailed"] = fields;
            };

            // 4015 MessageQueueRegisterCompleted (Multidata48TemplateA)
            parser.MessageQueueRegisterCompleted += delegate (Multidata48TemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["Uri"] = data.Uri;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["MessageQueueRegisterCompleted"] = fields;
            };

            // 4016 MessageQueueDuplicatedSocketError (OneStringsTemplateA)
            parser.MessageQueueDuplicatedSocketError += delegate (OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents["MessageQueueDuplicatedSocketError"] = fields;
            };

            // 4019 MessageQueueDuplicatedSocketComplete (OneStringsTemplateA)
            parser.MessageQueueDuplicatedSocketComplete += delegate (OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents["MessageQueueDuplicatedSocketComplete"] = fields;
            };

            // 4020 TcpTransportListenerListeningStart (Multidata48TemplateA)
            parser.TcpTransportListenerListeningStart += delegate (Multidata48TemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["Uri"] = data.Uri;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["TcpTransportListenerListeningStart"] = fields;
            };

            // 4021 TcpTransportListenerListeningStop (OneStringsTemplateA)
            parser.TcpTransportListenerListeningStop += delegate (OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents["TcpTransportListenerListeningStop"] = fields;
            };

            // 4022 WebhostUnregisterProtocolFailed (Multidata82TemplateA)
            parser.WebhostUnregisterProtocolFailed += delegate (Multidata82TemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["hresult"] = data.hresult;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["WebhostUnregisterProtocolFailed"] = fields;
            };

            // 4023 WasCloseAllListenerChannelInstancesCompleted (OneStringsTemplateA)
            parser.WasCloseAllListenerChannelInstancesCompleted += delegate (OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents["WasCloseAllListenerChannelInstancesCompleted"] = fields;
            };

            // 4024 WasCloseAllListenerChannelInstancesFailed (Multidata82TemplateA)
            parser.WasCloseAllListenerChannelInstancesFailed += delegate (Multidata82TemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["hresult"] = data.hresult;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["WasCloseAllListenerChannelInstancesFailed"] = fields;
            };

            // 4025 OpenListenerChannelInstanceFailed (Multidata82TemplateA)
            parser.OpenListenerChannelInstanceFailed += delegate (Multidata82TemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["hresult"] = data.hresult;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["OpenListenerChannelInstanceFailed"] = fields;
            };

            // 4026 WasConnected (OneStringsTemplateA)
            parser.WasConnected += delegate (OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents["WasConnected"] = fields;
            };

            // 4027 WasDisconnected (OneStringsTemplateA)
            parser.WasDisconnected += delegate (OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents["WasDisconnected"] = fields;
            };

            // 4028 PipeTransportListenerListeningStart (Multidata48TemplateA)
            parser.PipeTransportListenerListeningStart += delegate (Multidata48TemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["Uri"] = data.Uri;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["PipeTransportListenerListeningStart"] = fields;
            };

            // 4029 PipeTransportListenerListeningStop (OneStringsTemplateA)
            parser.PipeTransportListenerListeningStop += delegate (OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents["PipeTransportListenerListeningStop"] = fields;
            };

            // 4030 DispatchSessionSuccess (OneStringsTemplateA)
            parser.DispatchSessionSuccess += delegate (OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents["DispatchSessionSuccess"] = fields;
            };

            // 4031 DispatchSessionFailed (OneStringsTemplateA)
            parser.DispatchSessionFailed += delegate (OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents["DispatchSessionFailed"] = fields;
            };

            // 4032 WasConnectionTimedout (OneStringsTemplateA)
            parser.WasConnectionTimedout += delegate (OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents["WasConnectionTimedout"] = fields;
            };

            // 4033 RoutingTableLookupStart (OneStringsTemplateA)
            parser.RoutingTableLookupStart += delegate (OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents["RoutingTableLookupStart"] = fields;
            };

            // 4034 RoutingTableLookupStop (OneStringsTemplateA)
            parser.RoutingTableLookupStop += delegate (OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents["RoutingTableLookupStop"] = fields;
            };

            // 4035 PendingSessionQueueRatio (Multidata83TemplateA)
            parser.PendingSessionQueueRatio += delegate (Multidata83TemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["curr"] = data.curr;
                fields["max"] = data.max;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["PendingSessionQueueRatio"] = fields;
            };

            // 4201 EndSqlCommandExecute (TwoStringsTemplateA)
            parser.EndSqlCommandExecute += delegate (TwoStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["EndSqlCommandExecute"] = fields;
            };

            // 4202 StartSqlCommandExecute (TwoStringsTemplateA)
            parser.StartSqlCommandExecute += delegate (TwoStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["StartSqlCommandExecute"] = fields;
            };

            // 4203 RenewLockSystemError (OneStringsTemplateA)
            parser.RenewLockSystemError += delegate (OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents["RenewLockSystemError"] = fields;
            };

            // 4205 FoundProcessingError (ThreeStringsTemplateEA)
            parser.FoundProcessingError += delegate (ThreeStringsTemplateEATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["SerializedException"] = data.SerializedException;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["FoundProcessingError"] = fields;
            };

            // 4206 UnlockInstanceException (TwoStringsTemplateA)
            parser.UnlockInstanceException += delegate (TwoStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["UnlockInstanceException"] = fields;
            };

            // 4207 MaximumRetriesExceededForSqlCommand (OneStringsTemplateA)
            parser.MaximumRetriesExceededForSqlCommand += delegate (OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents["MaximumRetriesExceededForSqlCommand"] = fields;
            };

            // 4208 RetryingSqlCommandDueToSqlError (TwoStringsTemplateA)
            parser.RetryingSqlCommandDueToSqlError += delegate (TwoStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["RetryingSqlCommandDueToSqlError"] = fields;
            };

            // 4209 TimeoutOpeningSqlConnection (TwoStringsTemplateA)
            parser.TimeoutOpeningSqlConnection += delegate (TwoStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["TimeoutOpeningSqlConnection"] = fields;
            };

            // 4210 SqlExceptionCaught (ThreeStringsTemplateA)
            parser.SqlExceptionCaught += delegate (ThreeStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["data2"] = data.data2;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["SqlExceptionCaught"] = fields;
            };

            // 4211 QueuingSqlRetry (TwoStringsTemplateA)
            parser.QueuingSqlRetry += delegate (TwoStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["QueuingSqlRetry"] = fields;
            };

            // 4212 LockRetryTimeout (TwoStringsTemplateA)
            parser.LockRetryTimeout += delegate (TwoStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["LockRetryTimeout"] = fields;
            };

            // 4213 RunnableInstancesDetectionError (TwoStringsTemplateEA)
            parser.RunnableInstancesDetectionError += delegate (TwoStringsTemplateEATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["SerializedException"] = data.SerializedException;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["RunnableInstancesDetectionError"] = fields;
            };

            // 4214 InstanceLocksRecoveryError (TwoStringsTemplateEA)
            parser.InstanceLocksRecoveryError += delegate (TwoStringsTemplateEATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["SerializedException"] = data.SerializedException;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["InstanceLocksRecoveryError"] = fields;
            };

            // 4600 MessageLogEventSizeExceeded (OneStringsTemplateA)
            parser.MessageLogEventSizeExceeded += delegate (OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents["MessageLogEventSizeExceeded"] = fields;
            };

            // 4801 DiscoveryClientInClientChannelFailedToClose (TwoStringsTemplateEA)
            parser.DiscoveryClientInClientChannelFailedToClose += delegate (TwoStringsTemplateEATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["SerializedException"] = data.SerializedException;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["DiscoveryClientInClientChannelFailedToClose"] = fields;
            };

            // 4802 DiscoveryClientProtocolExceptionSuppressed (TwoStringsTemplateEA)
            parser.DiscoveryClientProtocolExceptionSuppressed += delegate (TwoStringsTemplateEATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["SerializedException"] = data.SerializedException;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["DiscoveryClientProtocolExceptionSuppressed"] = fields;
            };

            // 4803 DiscoveryClientReceivedMulticastSuppression (OneStringsTemplateA)
            parser.DiscoveryClientReceivedMulticastSuppression += delegate (OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents["DiscoveryClientReceivedMulticastSuppression"] = fields;
            };
        }

        #endregion

        #region Chunk 11 Validation

        private void Validate_Chunk11(Dictionary<string, Dictionary<string, object>> firedEvents)
        {
            // 4014 MessageQueueRegisterFailed (Multidata80TemplateA)
            Assert.True(firedEvents.ContainsKey("MessageQueueRegisterFailed"), "MessageQueueRegisterFailed (4014) did not fire");
            Assert.Equal(TestString(4014, "Uri"), firedEvents["MessageQueueRegisterFailed"]["Uri"]);
            Assert.Equal(TestString(4014, "Status"), firedEvents["MessageQueueRegisterFailed"]["Status"]);
            Assert.Equal(TestString(4014, "AppDomain"), firedEvents["MessageQueueRegisterFailed"]["AppDomain"]);

            // 4015 MessageQueueRegisterCompleted (Multidata48TemplateA)
            Assert.True(firedEvents.ContainsKey("MessageQueueRegisterCompleted"), "MessageQueueRegisterCompleted (4015) did not fire");
            Assert.Equal(TestString(4015, "Uri"), firedEvents["MessageQueueRegisterCompleted"]["Uri"]);
            Assert.Equal(TestString(4015, "AppDomain"), firedEvents["MessageQueueRegisterCompleted"]["AppDomain"]);

            // 4016 MessageQueueDuplicatedSocketError (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("MessageQueueDuplicatedSocketError"), "MessageQueueDuplicatedSocketError (4016) did not fire");
            Assert.Equal(TestString(4016, "AppDomain"), firedEvents["MessageQueueDuplicatedSocketError"]["AppDomain"]);

            // 4019 MessageQueueDuplicatedSocketComplete (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("MessageQueueDuplicatedSocketComplete"), "MessageQueueDuplicatedSocketComplete (4019) did not fire");
            Assert.Equal(TestString(4019, "AppDomain"), firedEvents["MessageQueueDuplicatedSocketComplete"]["AppDomain"]);

            // 4020 TcpTransportListenerListeningStart (Multidata48TemplateA)
            Assert.True(firedEvents.ContainsKey("TcpTransportListenerListeningStart"), "TcpTransportListenerListeningStart (4020) did not fire");
            Assert.Equal(TestString(4020, "Uri"), firedEvents["TcpTransportListenerListeningStart"]["Uri"]);
            Assert.Equal(TestString(4020, "AppDomain"), firedEvents["TcpTransportListenerListeningStart"]["AppDomain"]);

            // 4021 TcpTransportListenerListeningStop (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("TcpTransportListenerListeningStop"), "TcpTransportListenerListeningStop (4021) did not fire");
            Assert.Equal(TestString(4021, "AppDomain"), firedEvents["TcpTransportListenerListeningStop"]["AppDomain"]);

            // 4022 WebhostUnregisterProtocolFailed (Multidata82TemplateA)
            Assert.True(firedEvents.ContainsKey("WebhostUnregisterProtocolFailed"), "WebhostUnregisterProtocolFailed (4022) did not fire");
            Assert.Equal(TestString(4022, "hresult"), firedEvents["WebhostUnregisterProtocolFailed"]["hresult"]);
            Assert.Equal(TestString(4022, "AppDomain"), firedEvents["WebhostUnregisterProtocolFailed"]["AppDomain"]);

            // 4023 WasCloseAllListenerChannelInstancesCompleted (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("WasCloseAllListenerChannelInstancesCompleted"), "WasCloseAllListenerChannelInstancesCompleted (4023) did not fire");
            Assert.Equal(TestString(4023, "AppDomain"), firedEvents["WasCloseAllListenerChannelInstancesCompleted"]["AppDomain"]);

            // 4024 WasCloseAllListenerChannelInstancesFailed (Multidata82TemplateA)
            Assert.True(firedEvents.ContainsKey("WasCloseAllListenerChannelInstancesFailed"), "WasCloseAllListenerChannelInstancesFailed (4024) did not fire");
            Assert.Equal(TestString(4024, "hresult"), firedEvents["WasCloseAllListenerChannelInstancesFailed"]["hresult"]);
            Assert.Equal(TestString(4024, "AppDomain"), firedEvents["WasCloseAllListenerChannelInstancesFailed"]["AppDomain"]);

            // 4025 OpenListenerChannelInstanceFailed (Multidata82TemplateA)
            Assert.True(firedEvents.ContainsKey("OpenListenerChannelInstanceFailed"), "OpenListenerChannelInstanceFailed (4025) did not fire");
            Assert.Equal(TestString(4025, "hresult"), firedEvents["OpenListenerChannelInstanceFailed"]["hresult"]);
            Assert.Equal(TestString(4025, "AppDomain"), firedEvents["OpenListenerChannelInstanceFailed"]["AppDomain"]);

            // 4026 WasConnected (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("WasConnected"), "WasConnected (4026) did not fire");
            Assert.Equal(TestString(4026, "AppDomain"), firedEvents["WasConnected"]["AppDomain"]);

            // 4027 WasDisconnected (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("WasDisconnected"), "WasDisconnected (4027) did not fire");
            Assert.Equal(TestString(4027, "AppDomain"), firedEvents["WasDisconnected"]["AppDomain"]);

            // 4028 PipeTransportListenerListeningStart (Multidata48TemplateA)
            Assert.True(firedEvents.ContainsKey("PipeTransportListenerListeningStart"), "PipeTransportListenerListeningStart (4028) did not fire");
            Assert.Equal(TestString(4028, "Uri"), firedEvents["PipeTransportListenerListeningStart"]["Uri"]);
            Assert.Equal(TestString(4028, "AppDomain"), firedEvents["PipeTransportListenerListeningStart"]["AppDomain"]);

            // 4029 PipeTransportListenerListeningStop (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("PipeTransportListenerListeningStop"), "PipeTransportListenerListeningStop (4029) did not fire");
            Assert.Equal(TestString(4029, "AppDomain"), firedEvents["PipeTransportListenerListeningStop"]["AppDomain"]);

            // 4030 DispatchSessionSuccess (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("DispatchSessionSuccess"), "DispatchSessionSuccess (4030) did not fire");
            Assert.Equal(TestString(4030, "AppDomain"), firedEvents["DispatchSessionSuccess"]["AppDomain"]);

            // 4031 DispatchSessionFailed (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("DispatchSessionFailed"), "DispatchSessionFailed (4031) did not fire");
            Assert.Equal(TestString(4031, "AppDomain"), firedEvents["DispatchSessionFailed"]["AppDomain"]);

            // 4032 WasConnectionTimedout (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("WasConnectionTimedout"), "WasConnectionTimedout (4032) did not fire");
            Assert.Equal(TestString(4032, "AppDomain"), firedEvents["WasConnectionTimedout"]["AppDomain"]);

            // 4033 RoutingTableLookupStart (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("RoutingTableLookupStart"), "RoutingTableLookupStart (4033) did not fire");
            Assert.Equal(TestString(4033, "AppDomain"), firedEvents["RoutingTableLookupStart"]["AppDomain"]);

            // 4034 RoutingTableLookupStop (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("RoutingTableLookupStop"), "RoutingTableLookupStop (4034) did not fire");
            Assert.Equal(TestString(4034, "AppDomain"), firedEvents["RoutingTableLookupStop"]["AppDomain"]);

            // 4035 PendingSessionQueueRatio (Multidata83TemplateA)
            Assert.True(firedEvents.ContainsKey("PendingSessionQueueRatio"), "PendingSessionQueueRatio (4035) did not fire");
            Assert.Equal(TestInt32(4035, 0), firedEvents["PendingSessionQueueRatio"]["curr"]);
            Assert.Equal(TestInt32(4035, 1), firedEvents["PendingSessionQueueRatio"]["max"]);
            Assert.Equal(TestString(4035, "AppDomain"), firedEvents["PendingSessionQueueRatio"]["AppDomain"]);

            // 4201 EndSqlCommandExecute (TwoStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("EndSqlCommandExecute"), "EndSqlCommandExecute (4201) did not fire");
            Assert.Equal(TestString(4201, "data1"), firedEvents["EndSqlCommandExecute"]["data1"]);
            Assert.Equal(TestString(4201, "AppDomain"), firedEvents["EndSqlCommandExecute"]["AppDomain"]);

            // 4202 StartSqlCommandExecute (TwoStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("StartSqlCommandExecute"), "StartSqlCommandExecute (4202) did not fire");
            Assert.Equal(TestString(4202, "data1"), firedEvents["StartSqlCommandExecute"]["data1"]);
            Assert.Equal(TestString(4202, "AppDomain"), firedEvents["StartSqlCommandExecute"]["AppDomain"]);

            // 4203 RenewLockSystemError (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("RenewLockSystemError"), "RenewLockSystemError (4203) did not fire");
            Assert.Equal(TestString(4203, "AppDomain"), firedEvents["RenewLockSystemError"]["AppDomain"]);

            // 4205 FoundProcessingError (ThreeStringsTemplateEA)
            Assert.True(firedEvents.ContainsKey("FoundProcessingError"), "FoundProcessingError (4205) did not fire");
            Assert.Equal(TestString(4205, "data1"), firedEvents["FoundProcessingError"]["data1"]);
            Assert.Equal(TestString(4205, "SerializedException"), firedEvents["FoundProcessingError"]["SerializedException"]);
            Assert.Equal(TestString(4205, "AppDomain"), firedEvents["FoundProcessingError"]["AppDomain"]);

            // 4206 UnlockInstanceException (TwoStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("UnlockInstanceException"), "UnlockInstanceException (4206) did not fire");
            Assert.Equal(TestString(4206, "data1"), firedEvents["UnlockInstanceException"]["data1"]);
            Assert.Equal(TestString(4206, "AppDomain"), firedEvents["UnlockInstanceException"]["AppDomain"]);

            // 4207 MaximumRetriesExceededForSqlCommand (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("MaximumRetriesExceededForSqlCommand"), "MaximumRetriesExceededForSqlCommand (4207) did not fire");
            Assert.Equal(TestString(4207, "AppDomain"), firedEvents["MaximumRetriesExceededForSqlCommand"]["AppDomain"]);

            // 4208 RetryingSqlCommandDueToSqlError (TwoStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("RetryingSqlCommandDueToSqlError"), "RetryingSqlCommandDueToSqlError (4208) did not fire");
            Assert.Equal(TestString(4208, "data1"), firedEvents["RetryingSqlCommandDueToSqlError"]["data1"]);
            Assert.Equal(TestString(4208, "AppDomain"), firedEvents["RetryingSqlCommandDueToSqlError"]["AppDomain"]);

            // 4209 TimeoutOpeningSqlConnection (TwoStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("TimeoutOpeningSqlConnection"), "TimeoutOpeningSqlConnection (4209) did not fire");
            Assert.Equal(TestString(4209, "data1"), firedEvents["TimeoutOpeningSqlConnection"]["data1"]);
            Assert.Equal(TestString(4209, "AppDomain"), firedEvents["TimeoutOpeningSqlConnection"]["AppDomain"]);

            // 4210 SqlExceptionCaught (ThreeStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("SqlExceptionCaught"), "SqlExceptionCaught (4210) did not fire");
            Assert.Equal(TestString(4210, "data1"), firedEvents["SqlExceptionCaught"]["data1"]);
            Assert.Equal(TestString(4210, "data2"), firedEvents["SqlExceptionCaught"]["data2"]);
            Assert.Equal(TestString(4210, "AppDomain"), firedEvents["SqlExceptionCaught"]["AppDomain"]);

            // 4211 QueuingSqlRetry (TwoStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("QueuingSqlRetry"), "QueuingSqlRetry (4211) did not fire");
            Assert.Equal(TestString(4211, "data1"), firedEvents["QueuingSqlRetry"]["data1"]);
            Assert.Equal(TestString(4211, "AppDomain"), firedEvents["QueuingSqlRetry"]["AppDomain"]);

            // 4212 LockRetryTimeout (TwoStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("LockRetryTimeout"), "LockRetryTimeout (4212) did not fire");
            Assert.Equal(TestString(4212, "data1"), firedEvents["LockRetryTimeout"]["data1"]);
            Assert.Equal(TestString(4212, "AppDomain"), firedEvents["LockRetryTimeout"]["AppDomain"]);

            // 4213 RunnableInstancesDetectionError (TwoStringsTemplateEA)
            Assert.True(firedEvents.ContainsKey("RunnableInstancesDetectionError"), "RunnableInstancesDetectionError (4213) did not fire");
            Assert.Equal(TestString(4213, "SerializedException"), firedEvents["RunnableInstancesDetectionError"]["SerializedException"]);
            Assert.Equal(TestString(4213, "AppDomain"), firedEvents["RunnableInstancesDetectionError"]["AppDomain"]);

            // 4214 InstanceLocksRecoveryError (TwoStringsTemplateEA)
            Assert.True(firedEvents.ContainsKey("InstanceLocksRecoveryError"), "InstanceLocksRecoveryError (4214) did not fire");
            Assert.Equal(TestString(4214, "SerializedException"), firedEvents["InstanceLocksRecoveryError"]["SerializedException"]);
            Assert.Equal(TestString(4214, "AppDomain"), firedEvents["InstanceLocksRecoveryError"]["AppDomain"]);

            // 4600 MessageLogEventSizeExceeded (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("MessageLogEventSizeExceeded"), "MessageLogEventSizeExceeded (4600) did not fire");
            Assert.Equal(TestString(4600, "AppDomain"), firedEvents["MessageLogEventSizeExceeded"]["AppDomain"]);

            // 4801 DiscoveryClientInClientChannelFailedToClose (TwoStringsTemplateEA)
            Assert.True(firedEvents.ContainsKey("DiscoveryClientInClientChannelFailedToClose"), "DiscoveryClientInClientChannelFailedToClose (4801) did not fire");
            Assert.Equal(TestString(4801, "SerializedException"), firedEvents["DiscoveryClientInClientChannelFailedToClose"]["SerializedException"]);
            Assert.Equal(TestString(4801, "AppDomain"), firedEvents["DiscoveryClientInClientChannelFailedToClose"]["AppDomain"]);

            // 4802 DiscoveryClientProtocolExceptionSuppressed (TwoStringsTemplateEA)
            Assert.True(firedEvents.ContainsKey("DiscoveryClientProtocolExceptionSuppressed"), "DiscoveryClientProtocolExceptionSuppressed (4802) did not fire");
            Assert.Equal(TestString(4802, "SerializedException"), firedEvents["DiscoveryClientProtocolExceptionSuppressed"]["SerializedException"]);
            Assert.Equal(TestString(4802, "AppDomain"), firedEvents["DiscoveryClientProtocolExceptionSuppressed"]["AppDomain"]);

            // 4803 DiscoveryClientReceivedMulticastSuppression (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("DiscoveryClientReceivedMulticastSuppression"), "DiscoveryClientReceivedMulticastSuppression (4803) did not fire");
            Assert.Equal(TestString(4803, "AppDomain"), firedEvents["DiscoveryClientReceivedMulticastSuppression"]["AppDomain"]);
        }

        #endregion
    }
}
