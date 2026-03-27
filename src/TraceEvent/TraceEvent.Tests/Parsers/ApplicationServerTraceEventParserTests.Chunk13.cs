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
        // ── Template field arrays for Chunk 13 ──

        private static readonly TemplateField[] FourStringsTemplateAFields = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("data2", FieldType.UnicodeString),
            new TemplateField("data3", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] FourStringsTemplateEAFields = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("data2", FieldType.UnicodeString),
            new TemplateField("SerializedException", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata0TemplateAFields = new TemplateField[]
        {
            new TemplateField("appdomainName", FieldType.UnicodeString),
            new TemplateField("processName", FieldType.UnicodeString),
            new TemplateField("processId", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata100TemplateHAFields = new TemplateField[]
        {
            new TemplateField("tokenID", FieldType.UnicodeString),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata101TemplateHAFields = new TemplateField[]
        {
            new TemplateField("issuerName", FieldType.UnicodeString),
            new TemplateField("tokenID", FieldType.UnicodeString),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata102TemplateHAFields = new TemplateField[]
        {
            new TemplateField("tokenType", FieldType.UnicodeString),
            new TemplateField("tokenID", FieldType.UnicodeString),
            new TemplateField("errorMessage", FieldType.UnicodeString),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata103TemplateHAFields = new TemplateField[]
        {
            new TemplateField("tokenType", FieldType.UnicodeString),
            new TemplateField("tokenID", FieldType.UnicodeString),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata15TemplateAFields = new TemplateField[]
        {
            new TemplateField("RecordNumber", FieldType.Int64),
            new TemplateField("ProviderId", FieldType.Guid),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata16TemplateAFields = new TemplateField[]
        {
            new TemplateField("Data", FieldType.UnicodeString),
            new TemplateField("Activity", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata18TemplateAFields = new TemplateField[]
        {
            new TemplateField("name", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata84TemplateAFields = new TemplateField[]
        {
            new TemplateField("limit", FieldType.Int32),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Chunk13_ThreeStringsTemplateAFields = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("data2", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] ThreeStringsTemplateEAFields = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("SerializedException", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Chunk13_TwoStringsTemplateAFields = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] TwoStringsTemplateTAFields = new TemplateField[]
        {
            new TemplateField("ExtendedData", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] TwoStringsTemplateVAFields = new TemplateField[]
        {
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        // ── WriteMetadata_Chunk13 ──

        private void WriteMetadata_Chunk13(EventPipeWriterV5 writer, ref int metadataId)
        {
            // 5402 TokenValidationStarted - Multidata103TemplateHA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "TokenValidationStarted", 5402,
                new MetadataParameter("tokenType", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("tokenID", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("HostReference", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 5403 TokenValidationSuccess - Multidata103TemplateHA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "TokenValidationSuccess", 5403,
                new MetadataParameter("tokenType", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("tokenID", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("HostReference", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 5404 TokenValidationFailure - Multidata102TemplateHA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "TokenValidationFailure", 5404,
                new MetadataParameter("tokenType", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("tokenID", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("errorMessage", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("HostReference", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 5405 GetIssuerNameSuccess - Multidata101TemplateHA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "GetIssuerNameSuccess", 5405,
                new MetadataParameter("issuerName", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("tokenID", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("HostReference", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 5406 GetIssuerNameFailure - Multidata100TemplateHA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "GetIssuerNameFailure", 5406,
                new MetadataParameter("tokenID", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("HostReference", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 5600 FederationMessageProcessingStarted - TwoStringsTemplateVA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "FederationMessageProcessingStarted", 5600,
                new MetadataParameter("HostReference", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 5601 FederationMessageProcessingSuccess - TwoStringsTemplateVA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "FederationMessageProcessingSuccess", 5601,
                new MetadataParameter("HostReference", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 5602 FederationMessageCreationStarted - TwoStringsTemplateVA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "FederationMessageCreationStarted", 5602,
                new MetadataParameter("HostReference", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 5603 FederationMessageCreationSuccess - TwoStringsTemplateVA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "FederationMessageCreationSuccess", 5603,
                new MetadataParameter("HostReference", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 5604 SessionCookieReadingStarted - TwoStringsTemplateVA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SessionCookieReadingStarted", 5604,
                new MetadataParameter("HostReference", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 5605 SessionCookieReadingSuccess - TwoStringsTemplateVA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SessionCookieReadingSuccess", 5605,
                new MetadataParameter("HostReference", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 5606 PrincipalSettingFromSessionTokenStarted - TwoStringsTemplateVA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "PrincipalSettingFromSessionTokenStarted", 5606,
                new MetadataParameter("HostReference", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 5607 PrincipalSettingFromSessionTokenSuccess - TwoStringsTemplateVA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "PrincipalSettingFromSessionTokenSuccess", 5607,
                new MetadataParameter("HostReference", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 39456 TrackingRecordDropped - Multidata15TemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "TrackingRecordDropped", 39456,
                new MetadataParameter("RecordNumber", new MetadataType(MetadataTypeCode.Int64)),
                new MetadataParameter("ProviderId", new MetadataType(MetadataTypeCode.Guid)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 39457 TrackingRecordRaised - ThreeStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "TrackingRecordRaised", 39457,
                new MetadataParameter("data1", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("data2", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 39458 TrackingRecordTruncated - Multidata15TemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "TrackingRecordTruncated", 39458,
                new MetadataParameter("RecordNumber", new MetadataType(MetadataTypeCode.Int64)),
                new MetadataParameter("ProviderId", new MetadataType(MetadataTypeCode.Guid)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 39459 TrackingDataExtracted - Multidata16TemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "TrackingDataExtracted", 39459,
                new MetadataParameter("Data", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("Activity", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 39460 TrackingValueNotSerializable - Multidata18TemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "TrackingValueNotSerializable", 39460,
                new MetadataParameter("name", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 57393 AppDomainUnload - Multidata0TemplateA (task=0, taskGuid=Guid.Empty)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "AppDomainUnload", 57393,
                new MetadataParameter("appdomainName", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("processName", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("processId", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 57394 HandledException - ThreeStringsTemplateEA (task=0, taskGuid=Guid.Empty)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "HandledException", 57394,
                new MetadataParameter("data1", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("SerializedException", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 57395 ShipAssertExceptionMessage - TwoStringsTemplateA (task=0, taskGuid=Guid.Empty)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ShipAssertExceptionMessage", 57395,
                new MetadataParameter("data1", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 57396 ThrowingException - FourStringsTemplateEA (task=0, taskGuid=Guid.Empty)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ThrowingException", 57396,
                new MetadataParameter("data1", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("data2", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("SerializedException", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 57397 UnhandledException - ThreeStringsTemplateEA (task=0, taskGuid=Guid.Empty)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "UnhandledException", 57397,
                new MetadataParameter("data1", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("SerializedException", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 57398 MaxInstancesExceeded - Multidata84TemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "MaxInstancesExceeded", 57398,
                new MetadataParameter("limit", new MetadataType(MetadataTypeCode.Int32)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 57399 TraceCodeEventLogCritical - TwoStringsTemplateTA (task=0, taskGuid=Guid.Empty)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "TraceCodeEventLogCritical", 57399,
                new MetadataParameter("ExtendedData", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 57400 TraceCodeEventLogError - TwoStringsTemplateTA (task=0, taskGuid=Guid.Empty)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "TraceCodeEventLogError", 57400,
                new MetadataParameter("ExtendedData", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 57401 TraceCodeEventLogInfo - TwoStringsTemplateTA (task=0, taskGuid=Guid.Empty)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "TraceCodeEventLogInfo", 57401,
                new MetadataParameter("ExtendedData", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 57402 TraceCodeEventLogVerbose - TwoStringsTemplateTA (task=0, taskGuid=Guid.Empty)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "TraceCodeEventLogVerbose", 57402,
                new MetadataParameter("ExtendedData", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 57403 TraceCodeEventLogWarning - TwoStringsTemplateTA (task=0, taskGuid=Guid.Empty)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "TraceCodeEventLogWarning", 57403,
                new MetadataParameter("ExtendedData", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 57404 HandledExceptionWarning - ThreeStringsTemplateEA (task=0, taskGuid=Guid.Empty)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "HandledExceptionWarning", 57404,
                new MetadataParameter("data1", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("SerializedException", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 57405 HandledExceptionError - ThreeStringsTemplateEA (task=0, taskGuid=Guid.Empty)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "HandledExceptionError", 57405,
                new MetadataParameter("data1", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("SerializedException", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 57406 HandledExceptionVerbose - ThreeStringsTemplateEA (task=0, taskGuid=Guid.Empty)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "HandledExceptionVerbose", 57406,
                new MetadataParameter("data1", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("SerializedException", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 57407 ThrowingExceptionVerbose - FourStringsTemplateEA (task=0, taskGuid=Guid.Empty)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ThrowingExceptionVerbose", 57407,
                new MetadataParameter("data1", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("data2", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("SerializedException", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 57408 EtwUnhandledException - ThreeStringsTemplateEA (task=0, taskGuid=Guid.Empty)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "EtwUnhandledException", 57408,
                new MetadataParameter("data1", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("SerializedException", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 57409 ThrowingEtwExceptionVerbose - FourStringsTemplateEA (task=0, taskGuid=Guid.Empty)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ThrowingEtwExceptionVerbose", 57409,
                new MetadataParameter("data1", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("data2", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("SerializedException", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 57410 ThrowingEtwException - FourStringsTemplateEA (task=0, taskGuid=Guid.Empty)
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ThrowingEtwException", 57410,
                new MetadataParameter("data1", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("data2", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("SerializedException", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));

            // 62326 HttpHandlerPickedForUrl - FourStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "HttpHandlerPickedForUrl", 62326,
                new MetadataParameter("data1", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("data2", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("data3", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String)),
                new MetadataParameter("AppDomain", new MetadataType(MetadataTypeCode.NullTerminatedUTF16String))));
        }

        // ── WriteEvents_Chunk13 ──

        private void WriteEvents_Chunk13(EventPipeWriterV5 writer, ref int metadataId, ref int sequenceNumber)
        {
            int __metadataId = metadataId;
            int __sequenceNumber = sequenceNumber;

            // 5402 TokenValidationStarted
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5402, Multidata103TemplateHAFields)));

            // 5403 TokenValidationSuccess
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5403, Multidata103TemplateHAFields)));

            // 5404 TokenValidationFailure
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5404, Multidata102TemplateHAFields)));

            // 5405 GetIssuerNameSuccess
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5405, Multidata101TemplateHAFields)));

            // 5406 GetIssuerNameFailure
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5406, Multidata100TemplateHAFields)));

            // 5600 FederationMessageProcessingStarted
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5600, TwoStringsTemplateVAFields)));

            // 5601 FederationMessageProcessingSuccess
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5601, TwoStringsTemplateVAFields)));

            // 5602 FederationMessageCreationStarted
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5602, TwoStringsTemplateVAFields)));

            // 5603 FederationMessageCreationSuccess
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5603, TwoStringsTemplateVAFields)));

            // 5604 SessionCookieReadingStarted
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5604, TwoStringsTemplateVAFields)));

            // 5605 SessionCookieReadingSuccess
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5605, TwoStringsTemplateVAFields)));

            // 5606 PrincipalSettingFromSessionTokenStarted
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5606, TwoStringsTemplateVAFields)));

            // 5607 PrincipalSettingFromSessionTokenSuccess
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(5607, TwoStringsTemplateVAFields)));

            // 39456 TrackingRecordDropped
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(39456, Multidata15TemplateAFields)));

            // 39457 TrackingRecordRaised
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(39457, Chunk13_ThreeStringsTemplateAFields)));

            // 39458 TrackingRecordTruncated
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(39458, Multidata15TemplateAFields)));

            // 39459 TrackingDataExtracted
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(39459, Multidata16TemplateAFields)));

            // 39460 TrackingValueNotSerializable
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(39460, Multidata18TemplateAFields)));

            // 57393 AppDomainUnload
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(57393, Multidata0TemplateAFields)));

            // 57394 HandledException
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(57394, ThreeStringsTemplateEAFields)));

            // 57395 ShipAssertExceptionMessage
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(57395, Chunk13_TwoStringsTemplateAFields)));

            // 57396 ThrowingException
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(57396, FourStringsTemplateEAFields)));

            // 57397 UnhandledException
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(57397, ThreeStringsTemplateEAFields)));

            // 57398 MaxInstancesExceeded
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(57398, Multidata84TemplateAFields)));

            // 57399 TraceCodeEventLogCritical
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(57399, TwoStringsTemplateTAFields)));

            // 57400 TraceCodeEventLogError
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(57400, TwoStringsTemplateTAFields)));

            // 57401 TraceCodeEventLogInfo
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(57401, TwoStringsTemplateTAFields)));

            // 57402 TraceCodeEventLogVerbose
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(57402, TwoStringsTemplateTAFields)));

            // 57403 TraceCodeEventLogWarning
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(57403, TwoStringsTemplateTAFields)));

            // 57404 HandledExceptionWarning
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(57404, ThreeStringsTemplateEAFields)));

            // 57405 HandledExceptionError
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(57405, ThreeStringsTemplateEAFields)));

            // 57406 HandledExceptionVerbose
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(57406, ThreeStringsTemplateEAFields)));

            // 57407 ThrowingExceptionVerbose
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(57407, FourStringsTemplateEAFields)));

            // 57408 EtwUnhandledException
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(57408, ThreeStringsTemplateEAFields)));

            // 57409 ThrowingEtwExceptionVerbose
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(57409, FourStringsTemplateEAFields)));

            // 57410 ThrowingEtwException
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(57410, FourStringsTemplateEAFields)));

            // 62326 HttpHandlerPickedForUrl
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(62326, FourStringsTemplateAFields)));
        
            metadataId = __metadataId;
            sequenceNumber = __sequenceNumber;
        }

        // ── Subscribe_Chunk13 ──

        private void Subscribe_Chunk13(ApplicationServerTraceEventParser parser, Dictionary<string, Dictionary<string, object>> firedEvents)
        {
            // 5402 TokenValidationStarted - Multidata103TemplateHA
            parser.TokenValidationStarted += delegate (Multidata103TemplateHATraceData data)
            {
                firedEvents["TokenValidationStarted"] = new Dictionary<string, object>
                {
                    { "tokenType", data.tokenType },
                    { "tokenID", data.tokenID },
                    { "HostReference", data.HostReference },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 5403 TokenValidationSuccess - Multidata103TemplateHA
            parser.TokenValidationSuccess += delegate (Multidata103TemplateHATraceData data)
            {
                firedEvents["TokenValidationSuccess"] = new Dictionary<string, object>
                {
                    { "tokenType", data.tokenType },
                    { "tokenID", data.tokenID },
                    { "HostReference", data.HostReference },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 5404 TokenValidationFailure - Multidata102TemplateHA
            parser.TokenValidationFailure += delegate (Multidata102TemplateHATraceData data)
            {
                firedEvents["TokenValidationFailure"] = new Dictionary<string, object>
                {
                    { "tokenType", data.tokenType },
                    { "tokenID", data.tokenID },
                    { "errorMessage", data.errorMessage },
                    { "HostReference", data.HostReference },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 5405 GetIssuerNameSuccess - Multidata101TemplateHA
            parser.GetIssuerNameSuccess += delegate (Multidata101TemplateHATraceData data)
            {
                firedEvents["GetIssuerNameSuccess"] = new Dictionary<string, object>
                {
                    { "issuerName", data.issuerName },
                    { "tokenID", data.tokenID },
                    { "HostReference", data.HostReference },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 5406 GetIssuerNameFailure - Multidata100TemplateHA
            parser.GetIssuerNameFailure += delegate (Multidata100TemplateHATraceData data)
            {
                firedEvents["GetIssuerNameFailure"] = new Dictionary<string, object>
                {
                    { "tokenID", data.tokenID },
                    { "HostReference", data.HostReference },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 5600 FederationMessageProcessingStarted - TwoStringsTemplateVA
            parser.FederationMessageProcessingStarted += delegate (TwoStringsTemplateVATraceData data)
            {
                firedEvents["FederationMessageProcessingStarted"] = new Dictionary<string, object>
                {
                    { "HostReference", data.HostReference },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 5601 FederationMessageProcessingSuccess - TwoStringsTemplateVA
            parser.FederationMessageProcessingSuccess += delegate (TwoStringsTemplateVATraceData data)
            {
                firedEvents["FederationMessageProcessingSuccess"] = new Dictionary<string, object>
                {
                    { "HostReference", data.HostReference },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 5602 FederationMessageCreationStarted - TwoStringsTemplateVA
            parser.FederationMessageCreationStarted += delegate (TwoStringsTemplateVATraceData data)
            {
                firedEvents["FederationMessageCreationStarted"] = new Dictionary<string, object>
                {
                    { "HostReference", data.HostReference },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 5603 FederationMessageCreationSuccess - TwoStringsTemplateVA
            parser.FederationMessageCreationSuccess += delegate (TwoStringsTemplateVATraceData data)
            {
                firedEvents["FederationMessageCreationSuccess"] = new Dictionary<string, object>
                {
                    { "HostReference", data.HostReference },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 5604 SessionCookieReadingStarted - TwoStringsTemplateVA
            parser.SessionCookieReadingStarted += delegate (TwoStringsTemplateVATraceData data)
            {
                firedEvents["SessionCookieReadingStarted"] = new Dictionary<string, object>
                {
                    { "HostReference", data.HostReference },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 5605 SessionCookieReadingSuccess - TwoStringsTemplateVA
            parser.SessionCookieReadingSuccess += delegate (TwoStringsTemplateVATraceData data)
            {
                firedEvents["SessionCookieReadingSuccess"] = new Dictionary<string, object>
                {
                    { "HostReference", data.HostReference },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 5606 PrincipalSettingFromSessionTokenStarted - TwoStringsTemplateVA
            parser.PrincipalSettingFromSessionTokenStarted += delegate (TwoStringsTemplateVATraceData data)
            {
                firedEvents["PrincipalSettingFromSessionTokenStarted"] = new Dictionary<string, object>
                {
                    { "HostReference", data.HostReference },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 5607 PrincipalSettingFromSessionTokenSuccess - TwoStringsTemplateVA
            parser.PrincipalSettingFromSessionTokenSuccess += delegate (TwoStringsTemplateVATraceData data)
            {
                firedEvents["PrincipalSettingFromSessionTokenSuccess"] = new Dictionary<string, object>
                {
                    { "HostReference", data.HostReference },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 39456 TrackingRecordDropped - Multidata15TemplateA
            parser.TrackingRecordDropped += delegate (Multidata15TemplateATraceData data)
            {
                firedEvents["TrackingRecordDropped"] = new Dictionary<string, object>
                {
                    { "RecordNumber", data.RecordNumber },
                    { "ProviderId", data.ProviderId },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 39457 TrackingRecordRaised - ThreeStringsTemplateA
            parser.TrackingRecordRaised += delegate (ThreeStringsTemplateATraceData data)
            {
                firedEvents["TrackingRecordRaised"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "data2", data.data2 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 39458 TrackingRecordTruncated - Multidata15TemplateA
            parser.TrackingRecordTruncated += delegate (Multidata15TemplateATraceData data)
            {
                firedEvents["TrackingRecordTruncated"] = new Dictionary<string, object>
                {
                    { "RecordNumber", data.RecordNumber },
                    { "ProviderId", data.ProviderId },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 39459 TrackingDataExtracted - Multidata16TemplateA
            parser.TrackingDataExtracted += delegate (Multidata16TemplateATraceData data)
            {
                firedEvents["TrackingDataExtracted"] = new Dictionary<string, object>
                {
                    { "Data", data.Data },
                    { "Activity", data.Activity },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 39460 TrackingValueNotSerializable - Multidata18TemplateA
            parser.TrackingValueNotSerializable += delegate (Multidata18TemplateATraceData data)
            {
                firedEvents["TrackingValueNotSerializable"] = new Dictionary<string, object>
                {
                    { "name", data.name },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57393 AppDomainUnload - Multidata0TemplateA
            parser.AppDomainUnload += delegate (Multidata0TemplateATraceData data)
            {
                firedEvents["AppDomainUnload"] = new Dictionary<string, object>
                {
                    { "appdomainName", data.appdomainName },
                    { "processName", data.processName },
                    { "processId", data.processId },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57394 HandledException - ThreeStringsTemplateEA
            parser.HandledException += delegate (ThreeStringsTemplateEATraceData data)
            {
                firedEvents["HandledException"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57395 ShipAssertExceptionMessage - TwoStringsTemplateA
            parser.ShipAssertExceptionMessage += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents["ShipAssertExceptionMessage"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57396 ThrowingException - FourStringsTemplateEA
            parser.ThrowingException += delegate (FourStringsTemplateEATraceData data)
            {
                firedEvents["ThrowingException"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "data2", data.data2 },
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57397 UnhandledException - ThreeStringsTemplateEA
            parser.UnhandledException += delegate (ThreeStringsTemplateEATraceData data)
            {
                firedEvents["UnhandledException"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57398 MaxInstancesExceeded - Multidata84TemplateA
            parser.MaxInstancesExceeded += delegate (Multidata84TemplateATraceData data)
            {
                firedEvents["MaxInstancesExceeded"] = new Dictionary<string, object>
                {
                    { "limit", data.limit },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57399 TraceCodeEventLogCritical - TwoStringsTemplateTA
            parser.TraceCodeEventLogCritical += delegate (TwoStringsTemplateTATraceData data)
            {
                firedEvents["TraceCodeEventLogCritical"] = new Dictionary<string, object>
                {
                    { "ExtendedData", data.ExtendedData },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57400 TraceCodeEventLogError - TwoStringsTemplateTA
            parser.TraceCodeEventLogError += delegate (TwoStringsTemplateTATraceData data)
            {
                firedEvents["TraceCodeEventLogError"] = new Dictionary<string, object>
                {
                    { "ExtendedData", data.ExtendedData },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57401 TraceCodeEventLogInfo - TwoStringsTemplateTA
            parser.TraceCodeEventLogInfo += delegate (TwoStringsTemplateTATraceData data)
            {
                firedEvents["TraceCodeEventLogInfo"] = new Dictionary<string, object>
                {
                    { "ExtendedData", data.ExtendedData },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57402 TraceCodeEventLogVerbose - TwoStringsTemplateTA
            parser.TraceCodeEventLogVerbose += delegate (TwoStringsTemplateTATraceData data)
            {
                firedEvents["TraceCodeEventLogVerbose"] = new Dictionary<string, object>
                {
                    { "ExtendedData", data.ExtendedData },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57403 TraceCodeEventLogWarning - TwoStringsTemplateTA
            parser.TraceCodeEventLogWarning += delegate (TwoStringsTemplateTATraceData data)
            {
                firedEvents["TraceCodeEventLogWarning"] = new Dictionary<string, object>
                {
                    { "ExtendedData", data.ExtendedData },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57404 HandledExceptionWarning - ThreeStringsTemplateEA
            parser.HandledExceptionWarning += delegate (ThreeStringsTemplateEATraceData data)
            {
                firedEvents["HandledExceptionWarning"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57405 HandledExceptionError - ThreeStringsTemplateEA
            parser.HandledExceptionError += delegate (ThreeStringsTemplateEATraceData data)
            {
                firedEvents["HandledExceptionError"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57406 HandledExceptionVerbose - ThreeStringsTemplateEA
            parser.HandledExceptionVerbose += delegate (ThreeStringsTemplateEATraceData data)
            {
                firedEvents["HandledExceptionVerbose"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57407 ThrowingExceptionVerbose - FourStringsTemplateEA
            parser.ThrowingExceptionVerbose += delegate (FourStringsTemplateEATraceData data)
            {
                firedEvents["ThrowingExceptionVerbose"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "data2", data.data2 },
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57408 EtwUnhandledException - ThreeStringsTemplateEA
            parser.EtwUnhandledException += delegate (ThreeStringsTemplateEATraceData data)
            {
                firedEvents["EtwUnhandledException"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57409 ThrowingEtwExceptionVerbose - FourStringsTemplateEA
            parser.ThrowingEtwExceptionVerbose += delegate (FourStringsTemplateEATraceData data)
            {
                firedEvents["ThrowingEtwExceptionVerbose"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "data2", data.data2 },
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57410 ThrowingEtwException - FourStringsTemplateEA
            parser.ThrowingEtwException += delegate (FourStringsTemplateEATraceData data)
            {
                firedEvents["ThrowingEtwException"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "data2", data.data2 },
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 62326 HttpHandlerPickedForUrl - FourStringsTemplateA
            parser.HttpHandlerPickedForUrl += delegate (FourStringsTemplateATraceData data)
            {
                firedEvents["HttpHandlerPickedForUrl"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "data2", data.data2 },
                    { "data3", data.data3 },
                    { "AppDomain", data.AppDomain },
                };
            };
        }

        // ── Validate_Chunk13 ──

        private void Validate_Chunk13(Dictionary<string, Dictionary<string, object>> firedEvents)
        {
            // 5402 TokenValidationStarted - Multidata103TemplateHA
            Assert.True(firedEvents.ContainsKey("TokenValidationStarted"), "Event TokenValidationStarted did not fire.");
            Assert.Equal(TestString(5402, "tokenType"), firedEvents["TokenValidationStarted"]["tokenType"]);
            Assert.Equal(TestString(5402, "tokenID"), firedEvents["TokenValidationStarted"]["tokenID"]);
            Assert.Equal(TestString(5402, "HostReference"), firedEvents["TokenValidationStarted"]["HostReference"]);
            Assert.Equal(TestString(5402, "AppDomain"), firedEvents["TokenValidationStarted"]["AppDomain"]);

            // 5403 TokenValidationSuccess - Multidata103TemplateHA
            Assert.True(firedEvents.ContainsKey("TokenValidationSuccess"), "Event TokenValidationSuccess did not fire.");
            Assert.Equal(TestString(5403, "tokenType"), firedEvents["TokenValidationSuccess"]["tokenType"]);
            Assert.Equal(TestString(5403, "tokenID"), firedEvents["TokenValidationSuccess"]["tokenID"]);
            Assert.Equal(TestString(5403, "HostReference"), firedEvents["TokenValidationSuccess"]["HostReference"]);
            Assert.Equal(TestString(5403, "AppDomain"), firedEvents["TokenValidationSuccess"]["AppDomain"]);

            // 5404 TokenValidationFailure - Multidata102TemplateHA
            Assert.True(firedEvents.ContainsKey("TokenValidationFailure"), "Event TokenValidationFailure did not fire.");
            Assert.Equal(TestString(5404, "tokenType"), firedEvents["TokenValidationFailure"]["tokenType"]);
            Assert.Equal(TestString(5404, "tokenID"), firedEvents["TokenValidationFailure"]["tokenID"]);
            Assert.Equal(TestString(5404, "errorMessage"), firedEvents["TokenValidationFailure"]["errorMessage"]);
            Assert.Equal(TestString(5404, "HostReference"), firedEvents["TokenValidationFailure"]["HostReference"]);
            Assert.Equal(TestString(5404, "AppDomain"), firedEvents["TokenValidationFailure"]["AppDomain"]);

            // 5405 GetIssuerNameSuccess - Multidata101TemplateHA
            Assert.True(firedEvents.ContainsKey("GetIssuerNameSuccess"), "Event GetIssuerNameSuccess did not fire.");
            Assert.Equal(TestString(5405, "issuerName"), firedEvents["GetIssuerNameSuccess"]["issuerName"]);
            Assert.Equal(TestString(5405, "tokenID"), firedEvents["GetIssuerNameSuccess"]["tokenID"]);
            Assert.Equal(TestString(5405, "HostReference"), firedEvents["GetIssuerNameSuccess"]["HostReference"]);
            Assert.Equal(TestString(5405, "AppDomain"), firedEvents["GetIssuerNameSuccess"]["AppDomain"]);

            // 5406 GetIssuerNameFailure - Multidata100TemplateHA
            Assert.True(firedEvents.ContainsKey("GetIssuerNameFailure"), "Event GetIssuerNameFailure did not fire.");
            Assert.Equal(TestString(5406, "tokenID"), firedEvents["GetIssuerNameFailure"]["tokenID"]);
            Assert.Equal(TestString(5406, "HostReference"), firedEvents["GetIssuerNameFailure"]["HostReference"]);
            Assert.Equal(TestString(5406, "AppDomain"), firedEvents["GetIssuerNameFailure"]["AppDomain"]);

            // 5600 FederationMessageProcessingStarted - TwoStringsTemplateVA
            Assert.True(firedEvents.ContainsKey("FederationMessageProcessingStarted"), "Event FederationMessageProcessingStarted did not fire.");
            Assert.Equal(TestString(5600, "HostReference"), firedEvents["FederationMessageProcessingStarted"]["HostReference"]);
            Assert.Equal(TestString(5600, "AppDomain"), firedEvents["FederationMessageProcessingStarted"]["AppDomain"]);

            // 5601 FederationMessageProcessingSuccess - TwoStringsTemplateVA
            Assert.True(firedEvents.ContainsKey("FederationMessageProcessingSuccess"), "Event FederationMessageProcessingSuccess did not fire.");
            Assert.Equal(TestString(5601, "HostReference"), firedEvents["FederationMessageProcessingSuccess"]["HostReference"]);
            Assert.Equal(TestString(5601, "AppDomain"), firedEvents["FederationMessageProcessingSuccess"]["AppDomain"]);

            // 5602 FederationMessageCreationStarted - TwoStringsTemplateVA
            Assert.True(firedEvents.ContainsKey("FederationMessageCreationStarted"), "Event FederationMessageCreationStarted did not fire.");
            Assert.Equal(TestString(5602, "HostReference"), firedEvents["FederationMessageCreationStarted"]["HostReference"]);
            Assert.Equal(TestString(5602, "AppDomain"), firedEvents["FederationMessageCreationStarted"]["AppDomain"]);

            // 5603 FederationMessageCreationSuccess - TwoStringsTemplateVA
            Assert.True(firedEvents.ContainsKey("FederationMessageCreationSuccess"), "Event FederationMessageCreationSuccess did not fire.");
            Assert.Equal(TestString(5603, "HostReference"), firedEvents["FederationMessageCreationSuccess"]["HostReference"]);
            Assert.Equal(TestString(5603, "AppDomain"), firedEvents["FederationMessageCreationSuccess"]["AppDomain"]);

            // 5604 SessionCookieReadingStarted - TwoStringsTemplateVA
            Assert.True(firedEvents.ContainsKey("SessionCookieReadingStarted"), "Event SessionCookieReadingStarted did not fire.");
            Assert.Equal(TestString(5604, "HostReference"), firedEvents["SessionCookieReadingStarted"]["HostReference"]);
            Assert.Equal(TestString(5604, "AppDomain"), firedEvents["SessionCookieReadingStarted"]["AppDomain"]);

            // 5605 SessionCookieReadingSuccess - TwoStringsTemplateVA
            Assert.True(firedEvents.ContainsKey("SessionCookieReadingSuccess"), "Event SessionCookieReadingSuccess did not fire.");
            Assert.Equal(TestString(5605, "HostReference"), firedEvents["SessionCookieReadingSuccess"]["HostReference"]);
            Assert.Equal(TestString(5605, "AppDomain"), firedEvents["SessionCookieReadingSuccess"]["AppDomain"]);

            // 5606 PrincipalSettingFromSessionTokenStarted - TwoStringsTemplateVA
            Assert.True(firedEvents.ContainsKey("PrincipalSettingFromSessionTokenStarted"), "Event PrincipalSettingFromSessionTokenStarted did not fire.");
            Assert.Equal(TestString(5606, "HostReference"), firedEvents["PrincipalSettingFromSessionTokenStarted"]["HostReference"]);
            Assert.Equal(TestString(5606, "AppDomain"), firedEvents["PrincipalSettingFromSessionTokenStarted"]["AppDomain"]);

            // 5607 PrincipalSettingFromSessionTokenSuccess - TwoStringsTemplateVA
            Assert.True(firedEvents.ContainsKey("PrincipalSettingFromSessionTokenSuccess"), "Event PrincipalSettingFromSessionTokenSuccess did not fire.");
            Assert.Equal(TestString(5607, "HostReference"), firedEvents["PrincipalSettingFromSessionTokenSuccess"]["HostReference"]);
            Assert.Equal(TestString(5607, "AppDomain"), firedEvents["PrincipalSettingFromSessionTokenSuccess"]["AppDomain"]);

            // 39456 TrackingRecordDropped - Multidata15TemplateA
            Assert.True(firedEvents.ContainsKey("TrackingRecordDropped"), "Event TrackingRecordDropped did not fire.");
            Assert.Equal(TestInt64(39456, 0), firedEvents["TrackingRecordDropped"]["RecordNumber"]);
            Assert.Equal(TestGuid(39456, 1), firedEvents["TrackingRecordDropped"]["ProviderId"]);
            Assert.Equal(TestString(39456, "AppDomain"), firedEvents["TrackingRecordDropped"]["AppDomain"]);

            // 39457 TrackingRecordRaised - ThreeStringsTemplateA
            Assert.True(firedEvents.ContainsKey("TrackingRecordRaised"), "Event TrackingRecordRaised did not fire.");
            Assert.Equal(TestString(39457, "data1"), firedEvents["TrackingRecordRaised"]["data1"]);
            Assert.Equal(TestString(39457, "data2"), firedEvents["TrackingRecordRaised"]["data2"]);
            Assert.Equal(TestString(39457, "AppDomain"), firedEvents["TrackingRecordRaised"]["AppDomain"]);

            // 39458 TrackingRecordTruncated - Multidata15TemplateA
            Assert.True(firedEvents.ContainsKey("TrackingRecordTruncated"), "Event TrackingRecordTruncated did not fire.");
            Assert.Equal(TestInt64(39458, 0), firedEvents["TrackingRecordTruncated"]["RecordNumber"]);
            Assert.Equal(TestGuid(39458, 1), firedEvents["TrackingRecordTruncated"]["ProviderId"]);
            Assert.Equal(TestString(39458, "AppDomain"), firedEvents["TrackingRecordTruncated"]["AppDomain"]);

            // 39459 TrackingDataExtracted - Multidata16TemplateA
            Assert.True(firedEvents.ContainsKey("TrackingDataExtracted"), "Event TrackingDataExtracted did not fire.");
            Assert.Equal(TestString(39459, "Data"), firedEvents["TrackingDataExtracted"]["Data"]);
            Assert.Equal(TestString(39459, "Activity"), firedEvents["TrackingDataExtracted"]["Activity"]);
            Assert.Equal(TestString(39459, "AppDomain"), firedEvents["TrackingDataExtracted"]["AppDomain"]);

            // 39460 TrackingValueNotSerializable - Multidata18TemplateA
            Assert.True(firedEvents.ContainsKey("TrackingValueNotSerializable"), "Event TrackingValueNotSerializable did not fire.");
            Assert.Equal(TestString(39460, "name"), firedEvents["TrackingValueNotSerializable"]["name"]);
            Assert.Equal(TestString(39460, "AppDomain"), firedEvents["TrackingValueNotSerializable"]["AppDomain"]);

            // 57393 AppDomainUnload - Multidata0TemplateA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey("AppDomainUnload"), "Event AppDomainUnload did not fire.");
            Assert.Equal(TestString(57393, "appdomainName"), firedEvents["AppDomainUnload"]["appdomainName"]);
            Assert.Equal(TestString(57393, "processName"), firedEvents["AppDomainUnload"]["processName"]);
            Assert.Equal(TestString(57393, "processId"), firedEvents["AppDomainUnload"]["processId"]);
            Assert.Equal(TestString(57393, "AppDomain"), firedEvents["AppDomainUnload"]["AppDomain"]);

            // 57394 HandledException - ThreeStringsTemplateEA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey("HandledException"), "Event HandledException did not fire.");
            Assert.Equal(TestString(57394, "data1"), firedEvents["HandledException"]["data1"]);
            Assert.Equal(TestString(57394, "SerializedException"), firedEvents["HandledException"]["SerializedException"]);
            Assert.Equal(TestString(57394, "AppDomain"), firedEvents["HandledException"]["AppDomain"]);

            // 57395 ShipAssertExceptionMessage - TwoStringsTemplateA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey("ShipAssertExceptionMessage"), "Event ShipAssertExceptionMessage did not fire.");
            Assert.Equal(TestString(57395, "data1"), firedEvents["ShipAssertExceptionMessage"]["data1"]);
            Assert.Equal(TestString(57395, "AppDomain"), firedEvents["ShipAssertExceptionMessage"]["AppDomain"]);

            // 57396 ThrowingException - FourStringsTemplateEA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey("ThrowingException"), "Event ThrowingException did not fire.");
            Assert.Equal(TestString(57396, "data1"), firedEvents["ThrowingException"]["data1"]);
            Assert.Equal(TestString(57396, "data2"), firedEvents["ThrowingException"]["data2"]);
            Assert.Equal(TestString(57396, "SerializedException"), firedEvents["ThrowingException"]["SerializedException"]);
            Assert.Equal(TestString(57396, "AppDomain"), firedEvents["ThrowingException"]["AppDomain"]);

            // 57397 UnhandledException - ThreeStringsTemplateEA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey("UnhandledException"), "Event UnhandledException did not fire.");
            Assert.Equal(TestString(57397, "data1"), firedEvents["UnhandledException"]["data1"]);
            Assert.Equal(TestString(57397, "SerializedException"), firedEvents["UnhandledException"]["SerializedException"]);
            Assert.Equal(TestString(57397, "AppDomain"), firedEvents["UnhandledException"]["AppDomain"]);

            // 57398 MaxInstancesExceeded - Multidata84TemplateA
            Assert.True(firedEvents.ContainsKey("MaxInstancesExceeded"), "Event MaxInstancesExceeded did not fire.");
            Assert.Equal(TestInt32(57398, 0), firedEvents["MaxInstancesExceeded"]["limit"]);
            Assert.Equal(TestString(57398, "AppDomain"), firedEvents["MaxInstancesExceeded"]["AppDomain"]);

            // 57399 TraceCodeEventLogCritical - TwoStringsTemplateTA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey("TraceCodeEventLogCritical"), "Event TraceCodeEventLogCritical did not fire.");
            Assert.Equal(TestString(57399, "ExtendedData"), firedEvents["TraceCodeEventLogCritical"]["ExtendedData"]);
            Assert.Equal(TestString(57399, "AppDomain"), firedEvents["TraceCodeEventLogCritical"]["AppDomain"]);

            // 57400 TraceCodeEventLogError - TwoStringsTemplateTA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey("TraceCodeEventLogError"), "Event TraceCodeEventLogError did not fire.");
            Assert.Equal(TestString(57400, "ExtendedData"), firedEvents["TraceCodeEventLogError"]["ExtendedData"]);
            Assert.Equal(TestString(57400, "AppDomain"), firedEvents["TraceCodeEventLogError"]["AppDomain"]);

            // 57401 TraceCodeEventLogInfo - TwoStringsTemplateTA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey("TraceCodeEventLogInfo"), "Event TraceCodeEventLogInfo did not fire.");
            Assert.Equal(TestString(57401, "ExtendedData"), firedEvents["TraceCodeEventLogInfo"]["ExtendedData"]);
            Assert.Equal(TestString(57401, "AppDomain"), firedEvents["TraceCodeEventLogInfo"]["AppDomain"]);

            // 57402 TraceCodeEventLogVerbose - TwoStringsTemplateTA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey("TraceCodeEventLogVerbose"), "Event TraceCodeEventLogVerbose did not fire.");
            Assert.Equal(TestString(57402, "ExtendedData"), firedEvents["TraceCodeEventLogVerbose"]["ExtendedData"]);
            Assert.Equal(TestString(57402, "AppDomain"), firedEvents["TraceCodeEventLogVerbose"]["AppDomain"]);

            // 57403 TraceCodeEventLogWarning - TwoStringsTemplateTA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey("TraceCodeEventLogWarning"), "Event TraceCodeEventLogWarning did not fire.");
            Assert.Equal(TestString(57403, "ExtendedData"), firedEvents["TraceCodeEventLogWarning"]["ExtendedData"]);
            Assert.Equal(TestString(57403, "AppDomain"), firedEvents["TraceCodeEventLogWarning"]["AppDomain"]);

            // 57404 HandledExceptionWarning - ThreeStringsTemplateEA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey("HandledExceptionWarning"), "Event HandledExceptionWarning did not fire.");
            Assert.Equal(TestString(57404, "data1"), firedEvents["HandledExceptionWarning"]["data1"]);
            Assert.Equal(TestString(57404, "SerializedException"), firedEvents["HandledExceptionWarning"]["SerializedException"]);
            Assert.Equal(TestString(57404, "AppDomain"), firedEvents["HandledExceptionWarning"]["AppDomain"]);

            // 57405 HandledExceptionError - ThreeStringsTemplateEA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey("HandledExceptionError"), "Event HandledExceptionError did not fire.");
            Assert.Equal(TestString(57405, "data1"), firedEvents["HandledExceptionError"]["data1"]);
            Assert.Equal(TestString(57405, "SerializedException"), firedEvents["HandledExceptionError"]["SerializedException"]);
            Assert.Equal(TestString(57405, "AppDomain"), firedEvents["HandledExceptionError"]["AppDomain"]);

            // 57406 HandledExceptionVerbose - ThreeStringsTemplateEA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey("HandledExceptionVerbose"), "Event HandledExceptionVerbose did not fire.");
            Assert.Equal(TestString(57406, "data1"), firedEvents["HandledExceptionVerbose"]["data1"]);
            Assert.Equal(TestString(57406, "SerializedException"), firedEvents["HandledExceptionVerbose"]["SerializedException"]);
            Assert.Equal(TestString(57406, "AppDomain"), firedEvents["HandledExceptionVerbose"]["AppDomain"]);

            // 57407 ThrowingExceptionVerbose - FourStringsTemplateEA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey("ThrowingExceptionVerbose"), "Event ThrowingExceptionVerbose did not fire.");
            Assert.Equal(TestString(57407, "data1"), firedEvents["ThrowingExceptionVerbose"]["data1"]);
            Assert.Equal(TestString(57407, "data2"), firedEvents["ThrowingExceptionVerbose"]["data2"]);
            Assert.Equal(TestString(57407, "SerializedException"), firedEvents["ThrowingExceptionVerbose"]["SerializedException"]);
            Assert.Equal(TestString(57407, "AppDomain"), firedEvents["ThrowingExceptionVerbose"]["AppDomain"]);

            // 57408 EtwUnhandledException - ThreeStringsTemplateEA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey("EtwUnhandledException"), "Event EtwUnhandledException did not fire.");
            Assert.Equal(TestString(57408, "data1"), firedEvents["EtwUnhandledException"]["data1"]);
            Assert.Equal(TestString(57408, "SerializedException"), firedEvents["EtwUnhandledException"]["SerializedException"]);
            Assert.Equal(TestString(57408, "AppDomain"), firedEvents["EtwUnhandledException"]["AppDomain"]);

            // 57409 ThrowingEtwExceptionVerbose - FourStringsTemplateEA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey("ThrowingEtwExceptionVerbose"), "Event ThrowingEtwExceptionVerbose did not fire.");
            Assert.Equal(TestString(57409, "data1"), firedEvents["ThrowingEtwExceptionVerbose"]["data1"]);
            Assert.Equal(TestString(57409, "data2"), firedEvents["ThrowingEtwExceptionVerbose"]["data2"]);
            Assert.Equal(TestString(57409, "SerializedException"), firedEvents["ThrowingEtwExceptionVerbose"]["SerializedException"]);
            Assert.Equal(TestString(57409, "AppDomain"), firedEvents["ThrowingEtwExceptionVerbose"]["AppDomain"]);

            // 57410 ThrowingEtwException - FourStringsTemplateEA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey("ThrowingEtwException"), "Event ThrowingEtwException did not fire.");
            Assert.Equal(TestString(57410, "data1"), firedEvents["ThrowingEtwException"]["data1"]);
            Assert.Equal(TestString(57410, "data2"), firedEvents["ThrowingEtwException"]["data2"]);
            Assert.Equal(TestString(57410, "SerializedException"), firedEvents["ThrowingEtwException"]["SerializedException"]);
            Assert.Equal(TestString(57410, "AppDomain"), firedEvents["ThrowingEtwException"]["AppDomain"]);

            // 62326 HttpHandlerPickedForUrl - FourStringsTemplateA
            Assert.True(firedEvents.ContainsKey("HttpHandlerPickedForUrl"), "Event HttpHandlerPickedForUrl did not fire.");
            Assert.Equal(TestString(62326, "data1"), firedEvents["HttpHandlerPickedForUrl"]["data1"]);
            Assert.Equal(TestString(62326, "data2"), firedEvents["HttpHandlerPickedForUrl"]["data2"]);
            Assert.Equal(TestString(62326, "data3"), firedEvents["HttpHandlerPickedForUrl"]["data3"]);
            Assert.Equal(TestString(62326, "AppDomain"), firedEvents["HttpHandlerPickedForUrl"]["AppDomain"]);
        }
    }
}
