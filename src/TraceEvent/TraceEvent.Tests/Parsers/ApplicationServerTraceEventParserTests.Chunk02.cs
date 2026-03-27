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
        // Template field arrays for Chunk02

        private static readonly TemplateField[] s_multidata22TemplateHAFields = new TemplateField[]
        {
            new TemplateField("Action", FieldType.UnicodeString),
            new TemplateField("ContractName", FieldType.UnicodeString),
            new TemplateField("Destination", FieldType.UnicodeString),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_multidata27TemplateHAFields = new TemplateField[]
        {
            new TemplateField("ThrottleName", FieldType.UnicodeString),
            new TemplateField("Limit", FieldType.Int64),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_multidata28TemplateHAFields = new TemplateField[]
        {
            new TemplateField("MethodName", FieldType.UnicodeString),
            new TemplateField("Duration", FieldType.Int64),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_multidata29TemplateHAFields = new TemplateField[]
        {
            new TemplateField("ListenAddress", FieldType.UnicodeString),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_multidata30TemplateHAFields = new TemplateField[]
        {
            new TemplateField("DestinationAddress", FieldType.UnicodeString),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_multidata31TemplateHAFields = new TemplateField[]
        {
            new TemplateField("ExceptionToString", FieldType.UnicodeString),
            new TemplateField("ExceptionTypeName", FieldType.UnicodeString),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_multidata32TemplateHAFields = new TemplateField[]
        {
            new TemplateField("CorrelationId", FieldType.Guid),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_multidata69TemplateAFields = new TemplateField[]
        {
            new TemplateField("AppDomainFriendlyName", FieldType.UnicodeString),
            new TemplateField("VirtualPath", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_multidata73TemplateAFields = new TemplateField[]
        {
            new TemplateField("ClosedCount", FieldType.Int32),
            new TemplateField("TotalCount", FieldType.Int32),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_multidata74TemplateAFields = new TemplateField[]
        {
            new TemplateField("RelativeAddress", FieldType.UnicodeString),
            new TemplateField("NormalizedAddress", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_multidata86TemplateHAFields = new TemplateField[]
        {
            new TemplateField("InstanceKey", FieldType.Guid),
            new TemplateField("Values", FieldType.UnicodeString),
            new TemplateField("ParentScope", FieldType.UnicodeString),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_oneStringsTemplateAFields = new TemplateField[]
        {
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_transferEmittedTemplateFields = new TemplateField[]
        {
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_twoStringsTemplateAFields = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_twoStringsTemplateTAFields = new TemplateField[]
        {
            new TemplateField("ExtendedData", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_userEventsTemplateFields = new TemplateField[]
        {
            new TemplateField("Name", FieldType.UnicodeString),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("Payload", FieldType.UnicodeString),
        };

        private void WriteMetadata_Chunk02(EventPipeWriterV5 writer, ref int metadataId)
        {
            int __metadataId = metadataId;
            writer.WriteMetadataBlock(w =>
            {
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "MessageReceivedByTransport", 215) { OpCode = 2 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "MessageSentByTransport", 216) { OpCode = 2 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ClientOperationPrepared", 217) { OpCode = 20 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ServiceChannelCallStop", 218) { OpCode = 2 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ServiceException", 219) { OpCode = 0 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "MessageSentToTransport", 220) { OpCode = 0 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "MessageReceivedFromTransport", 221) { OpCode = 0 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "OperationFailed", 222) { OpCode = 0 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "OperationFaulted", 223) { OpCode = 0 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "MessageThrottleAtSeventyPercent", 224) { OpCode = 0 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "TraceCorrelationKeys", 225) { OpCode = 0 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "IdleServicesClosed", 226) { OpCode = 0 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "UserDefinedErrorOccurred", 301) { OpCode = 0 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "UserDefinedWarningOccurred", 302) { OpCode = 0 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "UserDefinedInformationEventOccured", 303) { OpCode = 0 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "StopSignpostEvent", 401) { OpCode = 2 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "StartSignpostEvent", 402) { OpCode = 1 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "SuspendSignpostEvent", 403) { OpCode = 8 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ResumeSignpostEvent", 404) { OpCode = 7 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "StartSignpostEvent1", 440) { OpCode = 1 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "StopSignpostEvent1", 441) { OpCode = 2 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "MessageLogInfo", 451) { OpCode = 0 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "MessageLogWarning", 452) { OpCode = 0 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "TransferEmitted", 499) { OpCode = 0 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "CompilationStart", 501) { OpCode = 1 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "CompilationStop", 502) { OpCode = 2 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ServiceHostFactoryCreationStart", 503) { OpCode = 1 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ServiceHostFactoryCreationStop", 504) { OpCode = 2 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "CreateServiceHostStart", 505) { OpCode = 1 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "CreateServiceHostStop", 506) { OpCode = 2 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "HostedTransportConfigurationManagerConfigInitStart", 507) { OpCode = 1 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "HostedTransportConfigurationManagerConfigInitStop", 508) { OpCode = 2 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ServiceHostOpenStart", 509) { OpCode = 1 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ServiceHostOpenStop", 510) { OpCode = 2 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "WebHostRequestStart", 513) { OpCode = 1 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "WebHostRequestStop", 514) { OpCode = 2 });
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "CBAEntryRead", 601) { OpCode = 0 });
            });
        
            metadataId = __metadataId;
        }

        private void WriteEvents_Chunk02(EventPipeWriterV5 writer, ref int metadataId, ref int sequenceNumber)
        {
            int __metadataId = metadataId;
            int __sequenceNumber = sequenceNumber;
            writer.WriteEventBlock(w =>
            {
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(215, s_multidata29TemplateHAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(216, s_multidata30TemplateHAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(217, s_multidata22TemplateHAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(218, s_multidata22TemplateHAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(219, s_multidata31TemplateHAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(220, s_multidata32TemplateHAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(221, s_multidata32TemplateHAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(222, s_multidata28TemplateHAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(223, s_multidata28TemplateHAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(224, s_multidata27TemplateHAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(225, s_multidata86TemplateHAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(226, s_multidata73TemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(301, s_userEventsTemplateFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(302, s_userEventsTemplateFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(303, s_userEventsTemplateFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(401, s_twoStringsTemplateTAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(402, s_twoStringsTemplateTAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(403, s_twoStringsTemplateTAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(404, s_twoStringsTemplateTAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(440, s_twoStringsTemplateTAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(441, s_twoStringsTemplateTAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(451, s_twoStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(452, s_twoStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(499, s_transferEmittedTemplateFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(501, s_oneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(502, s_oneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(503, s_oneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(504, s_oneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(505, s_oneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(506, s_oneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(507, s_oneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(508, s_oneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(509, s_oneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(510, s_oneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(513, s_multidata69TemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(514, s_oneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(601, s_multidata74TemplateAFields));
            });
        
            metadataId = __metadataId;
            sequenceNumber = __sequenceNumber;
        }

        private void Subscribe_Chunk02(ApplicationServerTraceEventParser parser, Dictionary<int, Dictionary<string, object>> firedEvents)
        {
            parser.MessageReceivedByTransport += delegate(Multidata29TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["ListenAddress"] = data.ListenAddress;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[215] = fields;
            };

            parser.MessageSentByTransport += delegate(Multidata30TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["DestinationAddress"] = data.DestinationAddress;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[216] = fields;
            };

            parser.ClientOperationPrepared += delegate(Multidata22TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["SoapAction"] = data.SoapAction;
                fields["ContractName"] = data.ContractName;
                fields["Destination"] = data.Destination;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[217] = fields;
            };

            parser.ServiceChannelCallStop += delegate(Multidata22TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["SoapAction"] = data.SoapAction;
                fields["ContractName"] = data.ContractName;
                fields["Destination"] = data.Destination;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[218] = fields;
            };

            parser.ServiceException += delegate(Multidata31TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["ExceptionToString"] = data.ExceptionToString;
                fields["ExceptionTypeName"] = data.ExceptionTypeName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[219] = fields;
            };

            parser.MessageSentToTransport += delegate(Multidata32TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["CorrelationId"] = data.CorrelationId;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[220] = fields;
            };

            parser.MessageReceivedFromTransport += delegate(Multidata32TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["CorrelationId"] = data.CorrelationId;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[221] = fields;
            };

            parser.OperationFailed += delegate(Multidata28TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["MethodName"] = data.MethodName;
                fields["Duration"] = data.Duration;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[222] = fields;
            };

            parser.OperationFaulted += delegate(Multidata28TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["MethodName"] = data.MethodName;
                fields["Duration"] = data.Duration;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[223] = fields;
            };

            parser.MessageThrottleAtSeventyPercent += delegate(Multidata27TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["ThrottleName"] = data.ThrottleName;
                fields["Limit"] = data.Limit;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[224] = fields;
            };

            parser.TraceCorrelationKeys += delegate(Multidata86TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["InstanceKey"] = data.InstanceKey;
                fields["Values"] = data.Values;
                fields["ParentScope"] = data.ParentScope;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[225] = fields;
            };

            parser.IdleServicesClosed += delegate(Multidata73TemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["ClosedCount"] = data.ClosedCount;
                fields["TotalCount"] = data.TotalCount;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[226] = fields;
            };

            parser.UserDefinedErrorOccurred += delegate(UserEventsTemplateTraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["Name"] = data.Name;
                fields["HostReference"] = data.HostReference;
                fields["Payload"] = data.Payload;
                firedEvents[301] = fields;
            };

            parser.UserDefinedWarningOccurred += delegate(UserEventsTemplateTraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["Name"] = data.Name;
                fields["HostReference"] = data.HostReference;
                fields["Payload"] = data.Payload;
                firedEvents[302] = fields;
            };

            parser.UserDefinedInformationEventOccured += delegate(UserEventsTemplateTraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["Name"] = data.Name;
                fields["HostReference"] = data.HostReference;
                fields["Payload"] = data.Payload;
                firedEvents[303] = fields;
            };

            parser.StopSignpostEvent += delegate(TwoStringsTemplateTATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["ExtendedData"] = data.ExtendedData;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[401] = fields;
            };

            parser.StartSignpostEvent += delegate(TwoStringsTemplateTATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["ExtendedData"] = data.ExtendedData;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[402] = fields;
            };

            parser.SuspendSignpostEvent += delegate(TwoStringsTemplateTATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["ExtendedData"] = data.ExtendedData;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[403] = fields;
            };

            parser.ResumeSignpostEvent += delegate(TwoStringsTemplateTATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["ExtendedData"] = data.ExtendedData;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[404] = fields;
            };

            parser.StartSignpostEvent1 += delegate(TwoStringsTemplateTATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["ExtendedData"] = data.ExtendedData;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[440] = fields;
            };

            parser.StopSignpostEvent1 += delegate(TwoStringsTemplateTATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["ExtendedData"] = data.ExtendedData;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[441] = fields;
            };

            parser.MessageLogInfo += delegate(TwoStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[451] = fields;
            };

            parser.MessageLogWarning += delegate(TwoStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[452] = fields;
            };

            parser.TransferEmitted += delegate(TransferEmittedTemplateTraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[499] = fields;
            };

            parser.CompilationStart += delegate(OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents[501] = fields;
            };

            parser.CompilationStop += delegate(OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents[502] = fields;
            };

            parser.ServiceHostFactoryCreationStart += delegate(OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents[503] = fields;
            };

            parser.ServiceHostFactoryCreationStop += delegate(OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents[504] = fields;
            };

            parser.CreateServiceHostStart += delegate(OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents[505] = fields;
            };

            parser.CreateServiceHostStop += delegate(OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents[506] = fields;
            };

            parser.HostedTransportConfigurationManagerConfigInitStart += delegate(OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents[507] = fields;
            };

            parser.HostedTransportConfigurationManagerConfigInitStop += delegate(OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents[508] = fields;
            };

            parser.ServiceHostOpenStart += delegate(OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents[509] = fields;
            };

            parser.ServiceHostOpenStop += delegate(OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents[510] = fields;
            };

            parser.WebHostRequestStart += delegate(Multidata69TemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomainFriendlyName"] = data.AppDomainFriendlyName;
                fields["VirtualPath"] = data.VirtualPath;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[513] = fields;
            };

            parser.WebHostRequestStop += delegate(OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents[514] = fields;
            };

            parser.CBAEntryRead += delegate(Multidata74TemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["RelativeAddress"] = data.RelativeAddress;
                fields["NormalizedAddress"] = data.NormalizedAddress;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[601] = fields;
            };
        }

        private void Validate_Chunk02(Dictionary<int, Dictionary<string, object>> firedEvents)
        {
            // Event 215 - MessageReceivedByTransport (Multidata29TemplateHA)
            Assert.True(firedEvents.ContainsKey(215), "Event 215 (MessageReceivedByTransport) did not fire");
            var e215 = firedEvents[215];
            Assert.Equal(TestString(215, "ListenAddress"), (string)e215["ListenAddress"]);
            Assert.Equal(TestString(215, "HostReference"), (string)e215["HostReference"]);
            Assert.Equal(TestString(215, "AppDomain"), (string)e215["AppDomain"]);

            // Event 216 - MessageSentByTransport (Multidata30TemplateHA)
            Assert.True(firedEvents.ContainsKey(216), "Event 216 (MessageSentByTransport) did not fire");
            var e216 = firedEvents[216];
            Assert.Equal(TestString(216, "DestinationAddress"), (string)e216["DestinationAddress"]);
            Assert.Equal(TestString(216, "HostReference"), (string)e216["HostReference"]);
            Assert.Equal(TestString(216, "AppDomain"), (string)e216["AppDomain"]);

            // Event 217 - ClientOperationPrepared (Multidata22TemplateHA)
            Assert.True(firedEvents.ContainsKey(217), "Event 217 (ClientOperationPrepared) did not fire");
            var e217 = firedEvents[217];
            Assert.Equal(TestString(217, "Action"), (string)e217["SoapAction"]);
            Assert.Equal(TestString(217, "ContractName"), (string)e217["ContractName"]);
            Assert.Equal(TestString(217, "Destination"), (string)e217["Destination"]);
            Assert.Equal(TestString(217, "HostReference"), (string)e217["HostReference"]);
            Assert.Equal(TestString(217, "AppDomain"), (string)e217["AppDomain"]);

            // Event 218 - ServiceChannelCallStop (Multidata22TemplateHA)
            Assert.True(firedEvents.ContainsKey(218), "Event 218 (ServiceChannelCallStop) did not fire");
            var e218 = firedEvents[218];
            Assert.Equal(TestString(218, "Action"), (string)e218["SoapAction"]);
            Assert.Equal(TestString(218, "ContractName"), (string)e218["ContractName"]);
            Assert.Equal(TestString(218, "Destination"), (string)e218["Destination"]);
            Assert.Equal(TestString(218, "HostReference"), (string)e218["HostReference"]);
            Assert.Equal(TestString(218, "AppDomain"), (string)e218["AppDomain"]);

            // Event 219 - ServiceException (Multidata31TemplateHA)
            Assert.True(firedEvents.ContainsKey(219), "Event 219 (ServiceException) did not fire");
            var e219 = firedEvents[219];
            Assert.Equal(TestString(219, "ExceptionToString"), (string)e219["ExceptionToString"]);
            Assert.Equal(TestString(219, "ExceptionTypeName"), (string)e219["ExceptionTypeName"]);
            Assert.Equal(TestString(219, "HostReference"), (string)e219["HostReference"]);
            Assert.Equal(TestString(219, "AppDomain"), (string)e219["AppDomain"]);

            // Event 220 - MessageSentToTransport (Multidata32TemplateHA)
            Assert.True(firedEvents.ContainsKey(220), "Event 220 (MessageSentToTransport) did not fire");
            var e220 = firedEvents[220];
            Assert.Equal(TestGuid(220, 0), (Guid)e220["CorrelationId"]);
            Assert.Equal(TestString(220, "HostReference"), (string)e220["HostReference"]);
            Assert.Equal(TestString(220, "AppDomain"), (string)e220["AppDomain"]);

            // Event 221 - MessageReceivedFromTransport (Multidata32TemplateHA)
            Assert.True(firedEvents.ContainsKey(221), "Event 221 (MessageReceivedFromTransport) did not fire");
            var e221 = firedEvents[221];
            Assert.Equal(TestGuid(221, 0), (Guid)e221["CorrelationId"]);
            Assert.Equal(TestString(221, "HostReference"), (string)e221["HostReference"]);
            Assert.Equal(TestString(221, "AppDomain"), (string)e221["AppDomain"]);

            // Event 222 - OperationFailed (Multidata28TemplateHA)
            Assert.True(firedEvents.ContainsKey(222), "Event 222 (OperationFailed) did not fire");
            var e222 = firedEvents[222];
            Assert.Equal(TestString(222, "MethodName"), (string)e222["MethodName"]);
            Assert.Equal(TestInt64(222, 1), (long)e222["Duration"]);
            Assert.Equal(TestString(222, "HostReference"), (string)e222["HostReference"]);
            Assert.Equal(TestString(222, "AppDomain"), (string)e222["AppDomain"]);

            // Event 223 - OperationFaulted (Multidata28TemplateHA)
            Assert.True(firedEvents.ContainsKey(223), "Event 223 (OperationFaulted) did not fire");
            var e223 = firedEvents[223];
            Assert.Equal(TestString(223, "MethodName"), (string)e223["MethodName"]);
            Assert.Equal(TestInt64(223, 1), (long)e223["Duration"]);
            Assert.Equal(TestString(223, "HostReference"), (string)e223["HostReference"]);
            Assert.Equal(TestString(223, "AppDomain"), (string)e223["AppDomain"]);

            // Event 224 - MessageThrottleAtSeventyPercent (Multidata27TemplateHA)
            Assert.True(firedEvents.ContainsKey(224), "Event 224 (MessageThrottleAtSeventyPercent) did not fire");
            var e224 = firedEvents[224];
            Assert.Equal(TestString(224, "ThrottleName"), (string)e224["ThrottleName"]);
            Assert.Equal(TestInt64(224, 1), (long)e224["Limit"]);
            Assert.Equal(TestString(224, "HostReference"), (string)e224["HostReference"]);
            Assert.Equal(TestString(224, "AppDomain"), (string)e224["AppDomain"]);

            // Event 225 - TraceCorrelationKeys (Multidata86TemplateHA)
            Assert.True(firedEvents.ContainsKey(225), "Event 225 (TraceCorrelationKeys) did not fire");
            var e225 = firedEvents[225];
            Assert.Equal(TestGuid(225, 0), (Guid)e225["InstanceKey"]);
            Assert.Equal(TestString(225, "Values"), (string)e225["Values"]);
            Assert.Equal(TestString(225, "ParentScope"), (string)e225["ParentScope"]);
            Assert.Equal(TestString(225, "HostReference"), (string)e225["HostReference"]);
            Assert.Equal(TestString(225, "AppDomain"), (string)e225["AppDomain"]);

            // Event 226 - IdleServicesClosed (Multidata73TemplateA)
            Assert.True(firedEvents.ContainsKey(226), "Event 226 (IdleServicesClosed) did not fire");
            var e226 = firedEvents[226];
            Assert.Equal(TestInt32(226, 0), (int)e226["ClosedCount"]);
            Assert.Equal(TestInt32(226, 1), (int)e226["TotalCount"]);
            Assert.Equal(TestString(226, "AppDomain"), (string)e226["AppDomain"]);

            // Event 301 - UserDefinedErrorOccurred (UserEventsTemplate)
            Assert.True(firedEvents.ContainsKey(301), "Event 301 (UserDefinedErrorOccurred) did not fire");
            var e301 = firedEvents[301];
            Assert.Equal(TestString(301, "Name"), (string)e301["Name"]);
            Assert.Equal(TestString(301, "HostReference"), (string)e301["HostReference"]);
            Assert.Equal(TestString(301, "Payload"), (string)e301["Payload"]);

            // Event 302 - UserDefinedWarningOccurred (UserEventsTemplate)
            Assert.True(firedEvents.ContainsKey(302), "Event 302 (UserDefinedWarningOccurred) did not fire");
            var e302 = firedEvents[302];
            Assert.Equal(TestString(302, "Name"), (string)e302["Name"]);
            Assert.Equal(TestString(302, "HostReference"), (string)e302["HostReference"]);
            Assert.Equal(TestString(302, "Payload"), (string)e302["Payload"]);

            // Event 303 - UserDefinedInformationEventOccured (UserEventsTemplate)
            Assert.True(firedEvents.ContainsKey(303), "Event 303 (UserDefinedInformationEventOccured) did not fire");
            var e303 = firedEvents[303];
            Assert.Equal(TestString(303, "Name"), (string)e303["Name"]);
            Assert.Equal(TestString(303, "HostReference"), (string)e303["HostReference"]);
            Assert.Equal(TestString(303, "Payload"), (string)e303["Payload"]);

            // Event 401 - StopSignpostEvent (TwoStringsTemplateTA)
            Assert.True(firedEvents.ContainsKey(401), "Event 401 (StopSignpostEvent) did not fire");
            var e401 = firedEvents[401];
            Assert.Equal(TestString(401, "ExtendedData"), (string)e401["ExtendedData"]);
            Assert.Equal(TestString(401, "AppDomain"), (string)e401["AppDomain"]);

            // Event 402 - StartSignpostEvent (TwoStringsTemplateTA)
            Assert.True(firedEvents.ContainsKey(402), "Event 402 (StartSignpostEvent) did not fire");
            var e402 = firedEvents[402];
            Assert.Equal(TestString(402, "ExtendedData"), (string)e402["ExtendedData"]);
            Assert.Equal(TestString(402, "AppDomain"), (string)e402["AppDomain"]);

            // Event 403 - SuspendSignpostEvent (TwoStringsTemplateTA)
            Assert.True(firedEvents.ContainsKey(403), "Event 403 (SuspendSignpostEvent) did not fire");
            var e403 = firedEvents[403];
            Assert.Equal(TestString(403, "ExtendedData"), (string)e403["ExtendedData"]);
            Assert.Equal(TestString(403, "AppDomain"), (string)e403["AppDomain"]);

            // Event 404 - ResumeSignpostEvent (TwoStringsTemplateTA)
            Assert.True(firedEvents.ContainsKey(404), "Event 404 (ResumeSignpostEvent) did not fire");
            var e404 = firedEvents[404];
            Assert.Equal(TestString(404, "ExtendedData"), (string)e404["ExtendedData"]);
            Assert.Equal(TestString(404, "AppDomain"), (string)e404["AppDomain"]);

            // Event 440 - StartSignpostEvent1 (TwoStringsTemplateTA)
            Assert.True(firedEvents.ContainsKey(440), "Event 440 (StartSignpostEvent1) did not fire");
            var e440 = firedEvents[440];
            Assert.Equal(TestString(440, "ExtendedData"), (string)e440["ExtendedData"]);
            Assert.Equal(TestString(440, "AppDomain"), (string)e440["AppDomain"]);

            // Event 441 - StopSignpostEvent1 (TwoStringsTemplateTA)
            Assert.True(firedEvents.ContainsKey(441), "Event 441 (StopSignpostEvent1) did not fire");
            var e441 = firedEvents[441];
            Assert.Equal(TestString(441, "ExtendedData"), (string)e441["ExtendedData"]);
            Assert.Equal(TestString(441, "AppDomain"), (string)e441["AppDomain"]);

            // Event 451 - MessageLogInfo (TwoStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(451), "Event 451 (MessageLogInfo) did not fire");
            var e451 = firedEvents[451];
            Assert.Equal(TestString(451, "data1"), (string)e451["data1"]);
            Assert.Equal(TestString(451, "AppDomain"), (string)e451["AppDomain"]);

            // Event 452 - MessageLogWarning (TwoStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(452), "Event 452 (MessageLogWarning) did not fire");
            var e452 = firedEvents[452];
            Assert.Equal(TestString(452, "data1"), (string)e452["data1"]);
            Assert.Equal(TestString(452, "AppDomain"), (string)e452["AppDomain"]);

            // Event 499 - TransferEmitted (TransferEmittedTemplate)
            Assert.True(firedEvents.ContainsKey(499), "Event 499 (TransferEmitted) did not fire");
            var e499 = firedEvents[499];
            Assert.Equal(TestString(499, "HostReference"), (string)e499["HostReference"]);
            Assert.Equal(TestString(499, "AppDomain"), (string)e499["AppDomain"]);

            // Event 501 - CompilationStart (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(501), "Event 501 (CompilationStart) did not fire");
            var e501 = firedEvents[501];
            Assert.Equal(TestString(501, "AppDomain"), (string)e501["AppDomain"]);

            // Event 502 - CompilationStop (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(502), "Event 502 (CompilationStop) did not fire");
            var e502 = firedEvents[502];
            Assert.Equal(TestString(502, "AppDomain"), (string)e502["AppDomain"]);

            // Event 503 - ServiceHostFactoryCreationStart (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(503), "Event 503 (ServiceHostFactoryCreationStart) did not fire");
            var e503 = firedEvents[503];
            Assert.Equal(TestString(503, "AppDomain"), (string)e503["AppDomain"]);

            // Event 504 - ServiceHostFactoryCreationStop (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(504), "Event 504 (ServiceHostFactoryCreationStop) did not fire");
            var e504 = firedEvents[504];
            Assert.Equal(TestString(504, "AppDomain"), (string)e504["AppDomain"]);

            // Event 505 - CreateServiceHostStart (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(505), "Event 505 (CreateServiceHostStart) did not fire");
            var e505 = firedEvents[505];
            Assert.Equal(TestString(505, "AppDomain"), (string)e505["AppDomain"]);

            // Event 506 - CreateServiceHostStop (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(506), "Event 506 (CreateServiceHostStop) did not fire");
            var e506 = firedEvents[506];
            Assert.Equal(TestString(506, "AppDomain"), (string)e506["AppDomain"]);

            // Event 507 - HostedTransportConfigurationManagerConfigInitStart (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(507), "Event 507 (HostedTransportConfigurationManagerConfigInitStart) did not fire");
            var e507 = firedEvents[507];
            Assert.Equal(TestString(507, "AppDomain"), (string)e507["AppDomain"]);

            // Event 508 - HostedTransportConfigurationManagerConfigInitStop (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(508), "Event 508 (HostedTransportConfigurationManagerConfigInitStop) did not fire");
            var e508 = firedEvents[508];
            Assert.Equal(TestString(508, "AppDomain"), (string)e508["AppDomain"]);

            // Event 509 - ServiceHostOpenStart (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(509), "Event 509 (ServiceHostOpenStart) did not fire");
            var e509 = firedEvents[509];
            Assert.Equal(TestString(509, "AppDomain"), (string)e509["AppDomain"]);

            // Event 510 - ServiceHostOpenStop (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(510), "Event 510 (ServiceHostOpenStop) did not fire");
            var e510 = firedEvents[510];
            Assert.Equal(TestString(510, "AppDomain"), (string)e510["AppDomain"]);

            // Event 513 - WebHostRequestStart (Multidata69TemplateA)
            Assert.True(firedEvents.ContainsKey(513), "Event 513 (WebHostRequestStart) did not fire");
            var e513 = firedEvents[513];
            Assert.Equal(TestString(513, "AppDomainFriendlyName"), (string)e513["AppDomainFriendlyName"]);
            Assert.Equal(TestString(513, "VirtualPath"), (string)e513["VirtualPath"]);
            Assert.Equal(TestString(513, "AppDomain"), (string)e513["AppDomain"]);

            // Event 514 - WebHostRequestStop (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(514), "Event 514 (WebHostRequestStop) did not fire");
            var e514 = firedEvents[514];
            Assert.Equal(TestString(514, "AppDomain"), (string)e514["AppDomain"]);

            // Event 601 - CBAEntryRead (Multidata74TemplateA)
            Assert.True(firedEvents.ContainsKey(601), "Event 601 (CBAEntryRead) did not fire");
            var e601 = firedEvents[601];
            Assert.Equal(TestString(601, "RelativeAddress"), (string)e601["RelativeAddress"]);
            Assert.Equal(TestString(601, "NormalizedAddress"), (string)e601["NormalizedAddress"]);
            Assert.Equal(TestString(601, "AppDomain"), (string)e601["AppDomain"]);
        }
    }
}
