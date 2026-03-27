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

        private void Subscribe_Chunk02(ApplicationServerTraceEventParser parser, Dictionary<string, Dictionary<string, object>> firedEvents)
        {
            parser.MessageReceivedByTransport += delegate(Multidata29TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["ListenAddress"] = data.ListenAddress;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["MessageReceivedByTransport"] = fields;
            };

            parser.MessageSentByTransport += delegate(Multidata30TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["DestinationAddress"] = data.DestinationAddress;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["MessageSentByTransport"] = fields;
            };

            parser.ClientOperationPrepared += delegate(Multidata22TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["SoapAction"] = data.SoapAction;
                fields["ContractName"] = data.ContractName;
                fields["Destination"] = data.Destination;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["ClientOperationPrepared"] = fields;
            };

            parser.ServiceChannelCallStop += delegate(Multidata22TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["SoapAction"] = data.SoapAction;
                fields["ContractName"] = data.ContractName;
                fields["Destination"] = data.Destination;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["ServiceChannelCallStop"] = fields;
            };

            parser.ServiceException += delegate(Multidata31TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["ExceptionToString"] = data.ExceptionToString;
                fields["ExceptionTypeName"] = data.ExceptionTypeName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["ServiceException"] = fields;
            };

            parser.MessageSentToTransport += delegate(Multidata32TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["CorrelationId"] = data.CorrelationId;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["MessageSentToTransport"] = fields;
            };

            parser.MessageReceivedFromTransport += delegate(Multidata32TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["CorrelationId"] = data.CorrelationId;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["MessageReceivedFromTransport"] = fields;
            };

            parser.OperationFailed += delegate(Multidata28TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["MethodName"] = data.MethodName;
                fields["Duration"] = data.Duration;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["OperationFailed"] = fields;
            };

            parser.OperationFaulted += delegate(Multidata28TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["MethodName"] = data.MethodName;
                fields["Duration"] = data.Duration;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["OperationFaulted"] = fields;
            };

            parser.MessageThrottleAtSeventyPercent += delegate(Multidata27TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["ThrottleName"] = data.ThrottleName;
                fields["Limit"] = data.Limit;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["MessageThrottleAtSeventyPercent"] = fields;
            };

            parser.TraceCorrelationKeys += delegate(Multidata86TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["InstanceKey"] = data.InstanceKey;
                fields["Values"] = data.Values;
                fields["ParentScope"] = data.ParentScope;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["TraceCorrelationKeys"] = fields;
            };

            parser.IdleServicesClosed += delegate(Multidata73TemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["ClosedCount"] = data.ClosedCount;
                fields["TotalCount"] = data.TotalCount;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["IdleServicesClosed"] = fields;
            };

            parser.UserDefinedErrorOccurred += delegate(UserEventsTemplateTraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["Name"] = data.Name;
                fields["HostReference"] = data.HostReference;
                fields["Payload"] = data.Payload;
                firedEvents["UserDefinedErrorOccurred"] = fields;
            };

            parser.UserDefinedWarningOccurred += delegate(UserEventsTemplateTraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["Name"] = data.Name;
                fields["HostReference"] = data.HostReference;
                fields["Payload"] = data.Payload;
                firedEvents["UserDefinedWarningOccurred"] = fields;
            };

            parser.UserDefinedInformationEventOccured += delegate(UserEventsTemplateTraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["Name"] = data.Name;
                fields["HostReference"] = data.HostReference;
                fields["Payload"] = data.Payload;
                firedEvents["UserDefinedInformationEventOccured"] = fields;
            };

            parser.StopSignpostEvent += delegate(TwoStringsTemplateTATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["ExtendedData"] = data.ExtendedData;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["StopSignpostEvent"] = fields;
            };

            parser.StartSignpostEvent += delegate(TwoStringsTemplateTATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["ExtendedData"] = data.ExtendedData;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["StartSignpostEvent"] = fields;
            };

            parser.SuspendSignpostEvent += delegate(TwoStringsTemplateTATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["ExtendedData"] = data.ExtendedData;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["SuspendSignpostEvent"] = fields;
            };

            parser.ResumeSignpostEvent += delegate(TwoStringsTemplateTATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["ExtendedData"] = data.ExtendedData;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["ResumeSignpostEvent"] = fields;
            };

            parser.StartSignpostEvent1 += delegate(TwoStringsTemplateTATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["ExtendedData"] = data.ExtendedData;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["StartSignpostEvent1"] = fields;
            };

            parser.StopSignpostEvent1 += delegate(TwoStringsTemplateTATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["ExtendedData"] = data.ExtendedData;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["StopSignpostEvent1"] = fields;
            };

            parser.MessageLogInfo += delegate(TwoStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["MessageLogInfo"] = fields;
            };

            parser.MessageLogWarning += delegate(TwoStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["MessageLogWarning"] = fields;
            };

            parser.TransferEmitted += delegate(TransferEmittedTemplateTraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["TransferEmitted"] = fields;
            };

            parser.CompilationStart += delegate(OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents["CompilationStart"] = fields;
            };

            parser.CompilationStop += delegate(OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents["CompilationStop"] = fields;
            };

            parser.ServiceHostFactoryCreationStart += delegate(OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents["ServiceHostFactoryCreationStart"] = fields;
            };

            parser.ServiceHostFactoryCreationStop += delegate(OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents["ServiceHostFactoryCreationStop"] = fields;
            };

            parser.CreateServiceHostStart += delegate(OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents["CreateServiceHostStart"] = fields;
            };

            parser.CreateServiceHostStop += delegate(OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents["CreateServiceHostStop"] = fields;
            };

            parser.HostedTransportConfigurationManagerConfigInitStart += delegate(OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents["HostedTransportConfigurationManagerConfigInitStart"] = fields;
            };

            parser.HostedTransportConfigurationManagerConfigInitStop += delegate(OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents["HostedTransportConfigurationManagerConfigInitStop"] = fields;
            };

            parser.ServiceHostOpenStart += delegate(OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents["ServiceHostOpenStart"] = fields;
            };

            parser.ServiceHostOpenStop += delegate(OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents["ServiceHostOpenStop"] = fields;
            };

            parser.WebHostRequestStart += delegate(Multidata69TemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomainFriendlyName"] = data.AppDomainFriendlyName;
                fields["VirtualPath"] = data.VirtualPath;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["WebHostRequestStart"] = fields;
            };

            parser.WebHostRequestStop += delegate(OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents["WebHostRequestStop"] = fields;
            };

            parser.CBAEntryRead += delegate(Multidata74TemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["RelativeAddress"] = data.RelativeAddress;
                fields["NormalizedAddress"] = data.NormalizedAddress;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["CBAEntryRead"] = fields;
            };
        }

        private void Validate_Chunk02(Dictionary<string, Dictionary<string, object>> firedEvents)
        {
            // MessageReceivedByTransport (Multidata29TemplateHA)
            Assert.True(firedEvents.ContainsKey("MessageReceivedByTransport"), "Event MessageReceivedByTransport did not fire");
            var eMessageReceivedByTransport = firedEvents["MessageReceivedByTransport"];
            Assert.Equal(TestString(215, "ListenAddress"), (string)eMessageReceivedByTransport["ListenAddress"]);
            Assert.Equal(TestString(215, "HostReference"), (string)eMessageReceivedByTransport["HostReference"]);
            Assert.Equal(TestString(215, "AppDomain"), (string)eMessageReceivedByTransport["AppDomain"]);

            // MessageSentByTransport (Multidata30TemplateHA)
            Assert.True(firedEvents.ContainsKey("MessageSentByTransport"), "Event MessageSentByTransport did not fire");
            var eMessageSentByTransport = firedEvents["MessageSentByTransport"];
            Assert.Equal(TestString(216, "DestinationAddress"), (string)eMessageSentByTransport["DestinationAddress"]);
            Assert.Equal(TestString(216, "HostReference"), (string)eMessageSentByTransport["HostReference"]);
            Assert.Equal(TestString(216, "AppDomain"), (string)eMessageSentByTransport["AppDomain"]);

            // ClientOperationPrepared (Multidata22TemplateHA)
            Assert.True(firedEvents.ContainsKey("ClientOperationPrepared"), "Event ClientOperationPrepared did not fire");
            var eClientOperationPrepared = firedEvents["ClientOperationPrepared"];
            Assert.Equal(TestString(217, "Action"), (string)eClientOperationPrepared["SoapAction"]);
            Assert.Equal(TestString(217, "ContractName"), (string)eClientOperationPrepared["ContractName"]);
            Assert.Equal(TestString(217, "Destination"), (string)eClientOperationPrepared["Destination"]);
            Assert.Equal(TestString(217, "HostReference"), (string)eClientOperationPrepared["HostReference"]);
            Assert.Equal(TestString(217, "AppDomain"), (string)eClientOperationPrepared["AppDomain"]);

            // ServiceChannelCallStop (Multidata22TemplateHA)
            Assert.True(firedEvents.ContainsKey("ServiceChannelCallStop"), "Event ServiceChannelCallStop did not fire");
            var eServiceChannelCallStop = firedEvents["ServiceChannelCallStop"];
            Assert.Equal(TestString(218, "Action"), (string)eServiceChannelCallStop["SoapAction"]);
            Assert.Equal(TestString(218, "ContractName"), (string)eServiceChannelCallStop["ContractName"]);
            Assert.Equal(TestString(218, "Destination"), (string)eServiceChannelCallStop["Destination"]);
            Assert.Equal(TestString(218, "HostReference"), (string)eServiceChannelCallStop["HostReference"]);
            Assert.Equal(TestString(218, "AppDomain"), (string)eServiceChannelCallStop["AppDomain"]);

            // ServiceException (Multidata31TemplateHA)
            Assert.True(firedEvents.ContainsKey("ServiceException"), "Event ServiceException did not fire");
            var eServiceException = firedEvents["ServiceException"];
            Assert.Equal(TestString(219, "ExceptionToString"), (string)eServiceException["ExceptionToString"]);
            Assert.Equal(TestString(219, "ExceptionTypeName"), (string)eServiceException["ExceptionTypeName"]);
            Assert.Equal(TestString(219, "HostReference"), (string)eServiceException["HostReference"]);
            Assert.Equal(TestString(219, "AppDomain"), (string)eServiceException["AppDomain"]);

            // MessageSentToTransport (Multidata32TemplateHA)
            Assert.True(firedEvents.ContainsKey("MessageSentToTransport"), "Event MessageSentToTransport did not fire");
            var eMessageSentToTransport = firedEvents["MessageSentToTransport"];
            Assert.Equal(TestGuid(220, 0), (Guid)eMessageSentToTransport["CorrelationId"]);
            Assert.Equal(TestString(220, "HostReference"), (string)eMessageSentToTransport["HostReference"]);
            Assert.Equal(TestString(220, "AppDomain"), (string)eMessageSentToTransport["AppDomain"]);

            // MessageReceivedFromTransport (Multidata32TemplateHA)
            Assert.True(firedEvents.ContainsKey("MessageReceivedFromTransport"), "Event MessageReceivedFromTransport did not fire");
            var eMessageReceivedFromTransport = firedEvents["MessageReceivedFromTransport"];
            Assert.Equal(TestGuid(221, 0), (Guid)eMessageReceivedFromTransport["CorrelationId"]);
            Assert.Equal(TestString(221, "HostReference"), (string)eMessageReceivedFromTransport["HostReference"]);
            Assert.Equal(TestString(221, "AppDomain"), (string)eMessageReceivedFromTransport["AppDomain"]);

            // OperationFailed (Multidata28TemplateHA)
            Assert.True(firedEvents.ContainsKey("OperationFailed"), "Event OperationFailed did not fire");
            var eOperationFailed = firedEvents["OperationFailed"];
            Assert.Equal(TestString(222, "MethodName"), (string)eOperationFailed["MethodName"]);
            Assert.Equal(TestInt64(222, 1), (long)eOperationFailed["Duration"]);
            Assert.Equal(TestString(222, "HostReference"), (string)eOperationFailed["HostReference"]);
            Assert.Equal(TestString(222, "AppDomain"), (string)eOperationFailed["AppDomain"]);

            // OperationFaulted (Multidata28TemplateHA)
            Assert.True(firedEvents.ContainsKey("OperationFaulted"), "Event OperationFaulted did not fire");
            var eOperationFaulted = firedEvents["OperationFaulted"];
            Assert.Equal(TestString(223, "MethodName"), (string)eOperationFaulted["MethodName"]);
            Assert.Equal(TestInt64(223, 1), (long)eOperationFaulted["Duration"]);
            Assert.Equal(TestString(223, "HostReference"), (string)eOperationFaulted["HostReference"]);
            Assert.Equal(TestString(223, "AppDomain"), (string)eOperationFaulted["AppDomain"]);

            // MessageThrottleAtSeventyPercent (Multidata27TemplateHA)
            Assert.True(firedEvents.ContainsKey("MessageThrottleAtSeventyPercent"), "Event MessageThrottleAtSeventyPercent did not fire");
            var eMessageThrottleAtSeventyPercent = firedEvents["MessageThrottleAtSeventyPercent"];
            Assert.Equal(TestString(224, "ThrottleName"), (string)eMessageThrottleAtSeventyPercent["ThrottleName"]);
            Assert.Equal(TestInt64(224, 1), (long)eMessageThrottleAtSeventyPercent["Limit"]);
            Assert.Equal(TestString(224, "HostReference"), (string)eMessageThrottleAtSeventyPercent["HostReference"]);
            Assert.Equal(TestString(224, "AppDomain"), (string)eMessageThrottleAtSeventyPercent["AppDomain"]);

            // TraceCorrelationKeys (Multidata86TemplateHA)
            Assert.True(firedEvents.ContainsKey("TraceCorrelationKeys"), "Event TraceCorrelationKeys did not fire");
            var eTraceCorrelationKeys = firedEvents["TraceCorrelationKeys"];
            Assert.Equal(TestGuid(225, 0), (Guid)eTraceCorrelationKeys["InstanceKey"]);
            Assert.Equal(TestString(225, "Values"), (string)eTraceCorrelationKeys["Values"]);
            Assert.Equal(TestString(225, "ParentScope"), (string)eTraceCorrelationKeys["ParentScope"]);
            Assert.Equal(TestString(225, "HostReference"), (string)eTraceCorrelationKeys["HostReference"]);
            Assert.Equal(TestString(225, "AppDomain"), (string)eTraceCorrelationKeys["AppDomain"]);

            // IdleServicesClosed (Multidata73TemplateA)
            Assert.True(firedEvents.ContainsKey("IdleServicesClosed"), "Event IdleServicesClosed did not fire");
            var eIdleServicesClosed = firedEvents["IdleServicesClosed"];
            Assert.Equal(TestInt32(226, 0), (int)eIdleServicesClosed["ClosedCount"]);
            Assert.Equal(TestInt32(226, 1), (int)eIdleServicesClosed["TotalCount"]);
            Assert.Equal(TestString(226, "AppDomain"), (string)eIdleServicesClosed["AppDomain"]);

            // UserDefinedErrorOccurred (UserEventsTemplate)
            Assert.True(firedEvents.ContainsKey("UserDefinedErrorOccurred"), "Event UserDefinedErrorOccurred did not fire");
            var eUserDefinedErrorOccurred = firedEvents["UserDefinedErrorOccurred"];
            Assert.Equal(TestString(301, "Name"), (string)eUserDefinedErrorOccurred["Name"]);
            Assert.Equal(TestString(301, "HostReference"), (string)eUserDefinedErrorOccurred["HostReference"]);
            Assert.Equal(TestString(301, "Payload"), (string)eUserDefinedErrorOccurred["Payload"]);

            // UserDefinedWarningOccurred (UserEventsTemplate)
            Assert.True(firedEvents.ContainsKey("UserDefinedWarningOccurred"), "Event UserDefinedWarningOccurred did not fire");
            var eUserDefinedWarningOccurred = firedEvents["UserDefinedWarningOccurred"];
            Assert.Equal(TestString(302, "Name"), (string)eUserDefinedWarningOccurred["Name"]);
            Assert.Equal(TestString(302, "HostReference"), (string)eUserDefinedWarningOccurred["HostReference"]);
            Assert.Equal(TestString(302, "Payload"), (string)eUserDefinedWarningOccurred["Payload"]);

            // UserDefinedInformationEventOccured (UserEventsTemplate)
            Assert.True(firedEvents.ContainsKey("UserDefinedInformationEventOccured"), "Event UserDefinedInformationEventOccured did not fire");
            var eUserDefinedInformationEventOccured = firedEvents["UserDefinedInformationEventOccured"];
            Assert.Equal(TestString(303, "Name"), (string)eUserDefinedInformationEventOccured["Name"]);
            Assert.Equal(TestString(303, "HostReference"), (string)eUserDefinedInformationEventOccured["HostReference"]);
            Assert.Equal(TestString(303, "Payload"), (string)eUserDefinedInformationEventOccured["Payload"]);

            // StopSignpostEvent (TwoStringsTemplateTA)
            Assert.True(firedEvents.ContainsKey("StopSignpostEvent"), "Event StopSignpostEvent did not fire");
            var eStopSignpostEvent = firedEvents["StopSignpostEvent"];
            Assert.Equal(TestString(401, "ExtendedData"), (string)eStopSignpostEvent["ExtendedData"]);
            Assert.Equal(TestString(401, "AppDomain"), (string)eStopSignpostEvent["AppDomain"]);

            // StartSignpostEvent (TwoStringsTemplateTA)
            Assert.True(firedEvents.ContainsKey("StartSignpostEvent"), "Event StartSignpostEvent did not fire");
            var eStartSignpostEvent = firedEvents["StartSignpostEvent"];
            Assert.Equal(TestString(402, "ExtendedData"), (string)eStartSignpostEvent["ExtendedData"]);
            Assert.Equal(TestString(402, "AppDomain"), (string)eStartSignpostEvent["AppDomain"]);

            // SuspendSignpostEvent (TwoStringsTemplateTA)
            Assert.True(firedEvents.ContainsKey("SuspendSignpostEvent"), "Event SuspendSignpostEvent did not fire");
            var eSuspendSignpostEvent = firedEvents["SuspendSignpostEvent"];
            Assert.Equal(TestString(403, "ExtendedData"), (string)eSuspendSignpostEvent["ExtendedData"]);
            Assert.Equal(TestString(403, "AppDomain"), (string)eSuspendSignpostEvent["AppDomain"]);

            // ResumeSignpostEvent (TwoStringsTemplateTA)
            Assert.True(firedEvents.ContainsKey("ResumeSignpostEvent"), "Event ResumeSignpostEvent did not fire");
            var eResumeSignpostEvent = firedEvents["ResumeSignpostEvent"];
            Assert.Equal(TestString(404, "ExtendedData"), (string)eResumeSignpostEvent["ExtendedData"]);
            Assert.Equal(TestString(404, "AppDomain"), (string)eResumeSignpostEvent["AppDomain"]);

            // StartSignpostEvent1 (TwoStringsTemplateTA)
            Assert.True(firedEvents.ContainsKey("StartSignpostEvent1"), "Event StartSignpostEvent1 did not fire");
            var eStartSignpostEvent1 = firedEvents["StartSignpostEvent1"];
            Assert.Equal(TestString(440, "ExtendedData"), (string)eStartSignpostEvent1["ExtendedData"]);
            Assert.Equal(TestString(440, "AppDomain"), (string)eStartSignpostEvent1["AppDomain"]);

            // StopSignpostEvent1 (TwoStringsTemplateTA)
            Assert.True(firedEvents.ContainsKey("StopSignpostEvent1"), "Event StopSignpostEvent1 did not fire");
            var eStopSignpostEvent1 = firedEvents["StopSignpostEvent1"];
            Assert.Equal(TestString(441, "ExtendedData"), (string)eStopSignpostEvent1["ExtendedData"]);
            Assert.Equal(TestString(441, "AppDomain"), (string)eStopSignpostEvent1["AppDomain"]);

            // MessageLogInfo (TwoStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("MessageLogInfo"), "Event MessageLogInfo did not fire");
            var eMessageLogInfo = firedEvents["MessageLogInfo"];
            Assert.Equal(TestString(451, "data1"), (string)eMessageLogInfo["data1"]);
            Assert.Equal(TestString(451, "AppDomain"), (string)eMessageLogInfo["AppDomain"]);

            // MessageLogWarning (TwoStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("MessageLogWarning"), "Event MessageLogWarning did not fire");
            var eMessageLogWarning = firedEvents["MessageLogWarning"];
            Assert.Equal(TestString(452, "data1"), (string)eMessageLogWarning["data1"]);
            Assert.Equal(TestString(452, "AppDomain"), (string)eMessageLogWarning["AppDomain"]);

            // TransferEmitted (TransferEmittedTemplate)
            Assert.True(firedEvents.ContainsKey("TransferEmitted"), "Event TransferEmitted did not fire");
            var eTransferEmitted = firedEvents["TransferEmitted"];
            Assert.Equal(TestString(499, "HostReference"), (string)eTransferEmitted["HostReference"]);
            Assert.Equal(TestString(499, "AppDomain"), (string)eTransferEmitted["AppDomain"]);

            // CompilationStart (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("CompilationStart"), "Event CompilationStart did not fire");
            var eCompilationStart = firedEvents["CompilationStart"];
            Assert.Equal(TestString(501, "AppDomain"), (string)eCompilationStart["AppDomain"]);

            // CompilationStop (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("CompilationStop"), "Event CompilationStop did not fire");
            var eCompilationStop = firedEvents["CompilationStop"];
            Assert.Equal(TestString(502, "AppDomain"), (string)eCompilationStop["AppDomain"]);

            // ServiceHostFactoryCreationStart (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("ServiceHostFactoryCreationStart"), "Event ServiceHostFactoryCreationStart did not fire");
            var eServiceHostFactoryCreationStart = firedEvents["ServiceHostFactoryCreationStart"];
            Assert.Equal(TestString(503, "AppDomain"), (string)eServiceHostFactoryCreationStart["AppDomain"]);

            // ServiceHostFactoryCreationStop (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("ServiceHostFactoryCreationStop"), "Event ServiceHostFactoryCreationStop did not fire");
            var eServiceHostFactoryCreationStop = firedEvents["ServiceHostFactoryCreationStop"];
            Assert.Equal(TestString(504, "AppDomain"), (string)eServiceHostFactoryCreationStop["AppDomain"]);

            // CreateServiceHostStart (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("CreateServiceHostStart"), "Event CreateServiceHostStart did not fire");
            var eCreateServiceHostStart = firedEvents["CreateServiceHostStart"];
            Assert.Equal(TestString(505, "AppDomain"), (string)eCreateServiceHostStart["AppDomain"]);

            // CreateServiceHostStop (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("CreateServiceHostStop"), "Event CreateServiceHostStop did not fire");
            var eCreateServiceHostStop = firedEvents["CreateServiceHostStop"];
            Assert.Equal(TestString(506, "AppDomain"), (string)eCreateServiceHostStop["AppDomain"]);

            // HostedTransportConfigurationManagerConfigInitStart (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("HostedTransportConfigurationManagerConfigInitStart"), "Event HostedTransportConfigurationManagerConfigInitStart did not fire");
            var eHostedTransportConfigurationManagerConfigInitStart = firedEvents["HostedTransportConfigurationManagerConfigInitStart"];
            Assert.Equal(TestString(507, "AppDomain"), (string)eHostedTransportConfigurationManagerConfigInitStart["AppDomain"]);

            // HostedTransportConfigurationManagerConfigInitStop (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("HostedTransportConfigurationManagerConfigInitStop"), "Event HostedTransportConfigurationManagerConfigInitStop did not fire");
            var eHostedTransportConfigurationManagerConfigInitStop = firedEvents["HostedTransportConfigurationManagerConfigInitStop"];
            Assert.Equal(TestString(508, "AppDomain"), (string)eHostedTransportConfigurationManagerConfigInitStop["AppDomain"]);

            // ServiceHostOpenStart (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("ServiceHostOpenStart"), "Event ServiceHostOpenStart did not fire");
            var eServiceHostOpenStart = firedEvents["ServiceHostOpenStart"];
            Assert.Equal(TestString(509, "AppDomain"), (string)eServiceHostOpenStart["AppDomain"]);

            // ServiceHostOpenStop (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("ServiceHostOpenStop"), "Event ServiceHostOpenStop did not fire");
            var eServiceHostOpenStop = firedEvents["ServiceHostOpenStop"];
            Assert.Equal(TestString(510, "AppDomain"), (string)eServiceHostOpenStop["AppDomain"]);

            // WebHostRequestStart (Multidata69TemplateA)
            Assert.True(firedEvents.ContainsKey("WebHostRequestStart"), "Event WebHostRequestStart did not fire");
            var eWebHostRequestStart = firedEvents["WebHostRequestStart"];
            Assert.Equal(TestString(513, "AppDomainFriendlyName"), (string)eWebHostRequestStart["AppDomainFriendlyName"]);
            Assert.Equal(TestString(513, "VirtualPath"), (string)eWebHostRequestStart["VirtualPath"]);
            Assert.Equal(TestString(513, "AppDomain"), (string)eWebHostRequestStart["AppDomain"]);

            // WebHostRequestStop (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("WebHostRequestStop"), "Event WebHostRequestStop did not fire");
            var eWebHostRequestStop = firedEvents["WebHostRequestStop"];
            Assert.Equal(TestString(514, "AppDomain"), (string)eWebHostRequestStop["AppDomain"]);

            // CBAEntryRead (Multidata74TemplateA)
            Assert.True(firedEvents.ContainsKey("CBAEntryRead"), "Event CBAEntryRead did not fire");
            var eCBAEntryRead = firedEvents["CBAEntryRead"];
            Assert.Equal(TestString(601, "RelativeAddress"), (string)eCBAEntryRead["RelativeAddress"]);
            Assert.Equal(TestString(601, "NormalizedAddress"), (string)eCBAEntryRead["NormalizedAddress"]);
            Assert.Equal(TestString(601, "AppDomain"), (string)eCBAEntryRead["AppDomain"]);
        }
    }
}
