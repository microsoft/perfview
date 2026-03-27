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

        private void Subscribe_Chunk13(ApplicationServerTraceEventParser parser, Dictionary<int, Dictionary<string, object>> firedEvents)
        {
            // 5402 TokenValidationStarted - Multidata103TemplateHA
            parser.TokenValidationStarted += delegate (Multidata103TemplateHATraceData data)
            {
                firedEvents[5402] = new Dictionary<string, object>
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
                firedEvents[5403] = new Dictionary<string, object>
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
                firedEvents[5404] = new Dictionary<string, object>
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
                firedEvents[5405] = new Dictionary<string, object>
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
                firedEvents[5406] = new Dictionary<string, object>
                {
                    { "tokenID", data.tokenID },
                    { "HostReference", data.HostReference },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 5600 FederationMessageProcessingStarted - TwoStringsTemplateVA
            parser.FederationMessageProcessingStarted += delegate (TwoStringsTemplateVATraceData data)
            {
                firedEvents[5600] = new Dictionary<string, object>
                {
                    { "HostReference", data.HostReference },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 5601 FederationMessageProcessingSuccess - TwoStringsTemplateVA
            parser.FederationMessageProcessingSuccess += delegate (TwoStringsTemplateVATraceData data)
            {
                firedEvents[5601] = new Dictionary<string, object>
                {
                    { "HostReference", data.HostReference },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 5602 FederationMessageCreationStarted - TwoStringsTemplateVA
            parser.FederationMessageCreationStarted += delegate (TwoStringsTemplateVATraceData data)
            {
                firedEvents[5602] = new Dictionary<string, object>
                {
                    { "HostReference", data.HostReference },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 5603 FederationMessageCreationSuccess - TwoStringsTemplateVA
            parser.FederationMessageCreationSuccess += delegate (TwoStringsTemplateVATraceData data)
            {
                firedEvents[5603] = new Dictionary<string, object>
                {
                    { "HostReference", data.HostReference },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 5604 SessionCookieReadingStarted - TwoStringsTemplateVA
            parser.SessionCookieReadingStarted += delegate (TwoStringsTemplateVATraceData data)
            {
                firedEvents[5604] = new Dictionary<string, object>
                {
                    { "HostReference", data.HostReference },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 5605 SessionCookieReadingSuccess - TwoStringsTemplateVA
            parser.SessionCookieReadingSuccess += delegate (TwoStringsTemplateVATraceData data)
            {
                firedEvents[5605] = new Dictionary<string, object>
                {
                    { "HostReference", data.HostReference },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 5606 PrincipalSettingFromSessionTokenStarted - TwoStringsTemplateVA
            parser.PrincipalSettingFromSessionTokenStarted += delegate (TwoStringsTemplateVATraceData data)
            {
                firedEvents[5606] = new Dictionary<string, object>
                {
                    { "HostReference", data.HostReference },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 5607 PrincipalSettingFromSessionTokenSuccess - TwoStringsTemplateVA
            parser.PrincipalSettingFromSessionTokenSuccess += delegate (TwoStringsTemplateVATraceData data)
            {
                firedEvents[5607] = new Dictionary<string, object>
                {
                    { "HostReference", data.HostReference },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 39456 TrackingRecordDropped - Multidata15TemplateA
            parser.TrackingRecordDropped += delegate (Multidata15TemplateATraceData data)
            {
                firedEvents[39456] = new Dictionary<string, object>
                {
                    { "RecordNumber", data.RecordNumber },
                    { "ProviderId", data.ProviderId },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 39457 TrackingRecordRaised - ThreeStringsTemplateA
            parser.TrackingRecordRaised += delegate (ThreeStringsTemplateATraceData data)
            {
                firedEvents[39457] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "data2", data.data2 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 39458 TrackingRecordTruncated - Multidata15TemplateA
            parser.TrackingRecordTruncated += delegate (Multidata15TemplateATraceData data)
            {
                firedEvents[39458] = new Dictionary<string, object>
                {
                    { "RecordNumber", data.RecordNumber },
                    { "ProviderId", data.ProviderId },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 39459 TrackingDataExtracted - Multidata16TemplateA
            parser.TrackingDataExtracted += delegate (Multidata16TemplateATraceData data)
            {
                firedEvents[39459] = new Dictionary<string, object>
                {
                    { "Data", data.Data },
                    { "Activity", data.Activity },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 39460 TrackingValueNotSerializable - Multidata18TemplateA
            parser.TrackingValueNotSerializable += delegate (Multidata18TemplateATraceData data)
            {
                firedEvents[39460] = new Dictionary<string, object>
                {
                    { "name", data.name },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57393 AppDomainUnload - Multidata0TemplateA
            parser.AppDomainUnload += delegate (Multidata0TemplateATraceData data)
            {
                firedEvents[57393] = new Dictionary<string, object>
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
                firedEvents[57394] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57395 ShipAssertExceptionMessage - TwoStringsTemplateA
            parser.ShipAssertExceptionMessage += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents[57395] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57396 ThrowingException - FourStringsTemplateEA
            parser.ThrowingException += delegate (FourStringsTemplateEATraceData data)
            {
                firedEvents[57396] = new Dictionary<string, object>
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
                firedEvents[57397] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57398 MaxInstancesExceeded - Multidata84TemplateA
            parser.MaxInstancesExceeded += delegate (Multidata84TemplateATraceData data)
            {
                firedEvents[57398] = new Dictionary<string, object>
                {
                    { "limit", data.limit },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57399 TraceCodeEventLogCritical - TwoStringsTemplateTA
            parser.TraceCodeEventLogCritical += delegate (TwoStringsTemplateTATraceData data)
            {
                firedEvents[57399] = new Dictionary<string, object>
                {
                    { "ExtendedData", data.ExtendedData },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57400 TraceCodeEventLogError - TwoStringsTemplateTA
            parser.TraceCodeEventLogError += delegate (TwoStringsTemplateTATraceData data)
            {
                firedEvents[57400] = new Dictionary<string, object>
                {
                    { "ExtendedData", data.ExtendedData },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57401 TraceCodeEventLogInfo - TwoStringsTemplateTA
            parser.TraceCodeEventLogInfo += delegate (TwoStringsTemplateTATraceData data)
            {
                firedEvents[57401] = new Dictionary<string, object>
                {
                    { "ExtendedData", data.ExtendedData },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57402 TraceCodeEventLogVerbose - TwoStringsTemplateTA
            parser.TraceCodeEventLogVerbose += delegate (TwoStringsTemplateTATraceData data)
            {
                firedEvents[57402] = new Dictionary<string, object>
                {
                    { "ExtendedData", data.ExtendedData },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57403 TraceCodeEventLogWarning - TwoStringsTemplateTA
            parser.TraceCodeEventLogWarning += delegate (TwoStringsTemplateTATraceData data)
            {
                firedEvents[57403] = new Dictionary<string, object>
                {
                    { "ExtendedData", data.ExtendedData },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57404 HandledExceptionWarning - ThreeStringsTemplateEA
            parser.HandledExceptionWarning += delegate (ThreeStringsTemplateEATraceData data)
            {
                firedEvents[57404] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57405 HandledExceptionError - ThreeStringsTemplateEA
            parser.HandledExceptionError += delegate (ThreeStringsTemplateEATraceData data)
            {
                firedEvents[57405] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57406 HandledExceptionVerbose - ThreeStringsTemplateEA
            parser.HandledExceptionVerbose += delegate (ThreeStringsTemplateEATraceData data)
            {
                firedEvents[57406] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57407 ThrowingExceptionVerbose - FourStringsTemplateEA
            parser.ThrowingExceptionVerbose += delegate (FourStringsTemplateEATraceData data)
            {
                firedEvents[57407] = new Dictionary<string, object>
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
                firedEvents[57408] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 57409 ThrowingEtwExceptionVerbose - FourStringsTemplateEA
            parser.ThrowingEtwExceptionVerbose += delegate (FourStringsTemplateEATraceData data)
            {
                firedEvents[57409] = new Dictionary<string, object>
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
                firedEvents[57410] = new Dictionary<string, object>
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
                firedEvents[62326] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "data2", data.data2 },
                    { "data3", data.data3 },
                    { "AppDomain", data.AppDomain },
                };
            };
        }

        // ── Validate_Chunk13 ──

        private void Validate_Chunk13(Dictionary<int, Dictionary<string, object>> firedEvents)
        {
            // 5402 TokenValidationStarted - Multidata103TemplateHA
            Assert.True(firedEvents.ContainsKey(5402), "Event 5402 (TokenValidationStarted) did not fire.");
            Assert.Equal(TestString(5402, "tokenType"), firedEvents[5402]["tokenType"]);
            Assert.Equal(TestString(5402, "tokenID"), firedEvents[5402]["tokenID"]);
            Assert.Equal(TestString(5402, "HostReference"), firedEvents[5402]["HostReference"]);
            Assert.Equal(TestString(5402, "AppDomain"), firedEvents[5402]["AppDomain"]);

            // 5403 TokenValidationSuccess - Multidata103TemplateHA
            Assert.True(firedEvents.ContainsKey(5403), "Event 5403 (TokenValidationSuccess) did not fire.");
            Assert.Equal(TestString(5403, "tokenType"), firedEvents[5403]["tokenType"]);
            Assert.Equal(TestString(5403, "tokenID"), firedEvents[5403]["tokenID"]);
            Assert.Equal(TestString(5403, "HostReference"), firedEvents[5403]["HostReference"]);
            Assert.Equal(TestString(5403, "AppDomain"), firedEvents[5403]["AppDomain"]);

            // 5404 TokenValidationFailure - Multidata102TemplateHA
            Assert.True(firedEvents.ContainsKey(5404), "Event 5404 (TokenValidationFailure) did not fire.");
            Assert.Equal(TestString(5404, "tokenType"), firedEvents[5404]["tokenType"]);
            Assert.Equal(TestString(5404, "tokenID"), firedEvents[5404]["tokenID"]);
            Assert.Equal(TestString(5404, "errorMessage"), firedEvents[5404]["errorMessage"]);
            Assert.Equal(TestString(5404, "HostReference"), firedEvents[5404]["HostReference"]);
            Assert.Equal(TestString(5404, "AppDomain"), firedEvents[5404]["AppDomain"]);

            // 5405 GetIssuerNameSuccess - Multidata101TemplateHA
            Assert.True(firedEvents.ContainsKey(5405), "Event 5405 (GetIssuerNameSuccess) did not fire.");
            Assert.Equal(TestString(5405, "issuerName"), firedEvents[5405]["issuerName"]);
            Assert.Equal(TestString(5405, "tokenID"), firedEvents[5405]["tokenID"]);
            Assert.Equal(TestString(5405, "HostReference"), firedEvents[5405]["HostReference"]);
            Assert.Equal(TestString(5405, "AppDomain"), firedEvents[5405]["AppDomain"]);

            // 5406 GetIssuerNameFailure - Multidata100TemplateHA
            Assert.True(firedEvents.ContainsKey(5406), "Event 5406 (GetIssuerNameFailure) did not fire.");
            Assert.Equal(TestString(5406, "tokenID"), firedEvents[5406]["tokenID"]);
            Assert.Equal(TestString(5406, "HostReference"), firedEvents[5406]["HostReference"]);
            Assert.Equal(TestString(5406, "AppDomain"), firedEvents[5406]["AppDomain"]);

            // 5600 FederationMessageProcessingStarted - TwoStringsTemplateVA
            Assert.True(firedEvents.ContainsKey(5600), "Event 5600 (FederationMessageProcessingStarted) did not fire.");
            Assert.Equal(TestString(5600, "HostReference"), firedEvents[5600]["HostReference"]);
            Assert.Equal(TestString(5600, "AppDomain"), firedEvents[5600]["AppDomain"]);

            // 5601 FederationMessageProcessingSuccess - TwoStringsTemplateVA
            Assert.True(firedEvents.ContainsKey(5601), "Event 5601 (FederationMessageProcessingSuccess) did not fire.");
            Assert.Equal(TestString(5601, "HostReference"), firedEvents[5601]["HostReference"]);
            Assert.Equal(TestString(5601, "AppDomain"), firedEvents[5601]["AppDomain"]);

            // 5602 FederationMessageCreationStarted - TwoStringsTemplateVA
            Assert.True(firedEvents.ContainsKey(5602), "Event 5602 (FederationMessageCreationStarted) did not fire.");
            Assert.Equal(TestString(5602, "HostReference"), firedEvents[5602]["HostReference"]);
            Assert.Equal(TestString(5602, "AppDomain"), firedEvents[5602]["AppDomain"]);

            // 5603 FederationMessageCreationSuccess - TwoStringsTemplateVA
            Assert.True(firedEvents.ContainsKey(5603), "Event 5603 (FederationMessageCreationSuccess) did not fire.");
            Assert.Equal(TestString(5603, "HostReference"), firedEvents[5603]["HostReference"]);
            Assert.Equal(TestString(5603, "AppDomain"), firedEvents[5603]["AppDomain"]);

            // 5604 SessionCookieReadingStarted - TwoStringsTemplateVA
            Assert.True(firedEvents.ContainsKey(5604), "Event 5604 (SessionCookieReadingStarted) did not fire.");
            Assert.Equal(TestString(5604, "HostReference"), firedEvents[5604]["HostReference"]);
            Assert.Equal(TestString(5604, "AppDomain"), firedEvents[5604]["AppDomain"]);

            // 5605 SessionCookieReadingSuccess - TwoStringsTemplateVA
            Assert.True(firedEvents.ContainsKey(5605), "Event 5605 (SessionCookieReadingSuccess) did not fire.");
            Assert.Equal(TestString(5605, "HostReference"), firedEvents[5605]["HostReference"]);
            Assert.Equal(TestString(5605, "AppDomain"), firedEvents[5605]["AppDomain"]);

            // 5606 PrincipalSettingFromSessionTokenStarted - TwoStringsTemplateVA
            Assert.True(firedEvents.ContainsKey(5606), "Event 5606 (PrincipalSettingFromSessionTokenStarted) did not fire.");
            Assert.Equal(TestString(5606, "HostReference"), firedEvents[5606]["HostReference"]);
            Assert.Equal(TestString(5606, "AppDomain"), firedEvents[5606]["AppDomain"]);

            // 5607 PrincipalSettingFromSessionTokenSuccess - TwoStringsTemplateVA
            Assert.True(firedEvents.ContainsKey(5607), "Event 5607 (PrincipalSettingFromSessionTokenSuccess) did not fire.");
            Assert.Equal(TestString(5607, "HostReference"), firedEvents[5607]["HostReference"]);
            Assert.Equal(TestString(5607, "AppDomain"), firedEvents[5607]["AppDomain"]);

            // 39456 TrackingRecordDropped - Multidata15TemplateA
            Assert.True(firedEvents.ContainsKey(39456), "Event 39456 (TrackingRecordDropped) did not fire.");
            Assert.Equal(TestInt64(39456, 0), firedEvents[39456]["RecordNumber"]);
            Assert.Equal(TestGuid(39456, 1), firedEvents[39456]["ProviderId"]);
            Assert.Equal(TestString(39456, "AppDomain"), firedEvents[39456]["AppDomain"]);

            // 39457 TrackingRecordRaised - ThreeStringsTemplateA
            Assert.True(firedEvents.ContainsKey(39457), "Event 39457 (TrackingRecordRaised) did not fire.");
            Assert.Equal(TestString(39457, "data1"), firedEvents[39457]["data1"]);
            Assert.Equal(TestString(39457, "data2"), firedEvents[39457]["data2"]);
            Assert.Equal(TestString(39457, "AppDomain"), firedEvents[39457]["AppDomain"]);

            // 39458 TrackingRecordTruncated - Multidata15TemplateA
            Assert.True(firedEvents.ContainsKey(39458), "Event 39458 (TrackingRecordTruncated) did not fire.");
            Assert.Equal(TestInt64(39458, 0), firedEvents[39458]["RecordNumber"]);
            Assert.Equal(TestGuid(39458, 1), firedEvents[39458]["ProviderId"]);
            Assert.Equal(TestString(39458, "AppDomain"), firedEvents[39458]["AppDomain"]);

            // 39459 TrackingDataExtracted - Multidata16TemplateA
            Assert.True(firedEvents.ContainsKey(39459), "Event 39459 (TrackingDataExtracted) did not fire.");
            Assert.Equal(TestString(39459, "Data"), firedEvents[39459]["Data"]);
            Assert.Equal(TestString(39459, "Activity"), firedEvents[39459]["Activity"]);
            Assert.Equal(TestString(39459, "AppDomain"), firedEvents[39459]["AppDomain"]);

            // 39460 TrackingValueNotSerializable - Multidata18TemplateA
            Assert.True(firedEvents.ContainsKey(39460), "Event 39460 (TrackingValueNotSerializable) did not fire.");
            Assert.Equal(TestString(39460, "name"), firedEvents[39460]["name"]);
            Assert.Equal(TestString(39460, "AppDomain"), firedEvents[39460]["AppDomain"]);

            // 57393 AppDomainUnload - Multidata0TemplateA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey(57393), "Event 57393 (AppDomainUnload) did not fire.");
            Assert.Equal(TestString(57393, "appdomainName"), firedEvents[57393]["appdomainName"]);
            Assert.Equal(TestString(57393, "processName"), firedEvents[57393]["processName"]);
            Assert.Equal(TestString(57393, "processId"), firedEvents[57393]["processId"]);
            Assert.Equal(TestString(57393, "AppDomain"), firedEvents[57393]["AppDomain"]);

            // 57394 HandledException - ThreeStringsTemplateEA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey(57394), "Event 57394 (HandledException) did not fire.");
            Assert.Equal(TestString(57394, "data1"), firedEvents[57394]["data1"]);
            Assert.Equal(TestString(57394, "SerializedException"), firedEvents[57394]["SerializedException"]);
            Assert.Equal(TestString(57394, "AppDomain"), firedEvents[57394]["AppDomain"]);

            // 57395 ShipAssertExceptionMessage - TwoStringsTemplateA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey(57395), "Event 57395 (ShipAssertExceptionMessage) did not fire.");
            Assert.Equal(TestString(57395, "data1"), firedEvents[57395]["data1"]);
            Assert.Equal(TestString(57395, "AppDomain"), firedEvents[57395]["AppDomain"]);

            // 57396 ThrowingException - FourStringsTemplateEA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey(57396), "Event 57396 (ThrowingException) did not fire.");
            Assert.Equal(TestString(57396, "data1"), firedEvents[57396]["data1"]);
            Assert.Equal(TestString(57396, "data2"), firedEvents[57396]["data2"]);
            Assert.Equal(TestString(57396, "SerializedException"), firedEvents[57396]["SerializedException"]);
            Assert.Equal(TestString(57396, "AppDomain"), firedEvents[57396]["AppDomain"]);

            // 57397 UnhandledException - ThreeStringsTemplateEA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey(57397), "Event 57397 (UnhandledException) did not fire.");
            Assert.Equal(TestString(57397, "data1"), firedEvents[57397]["data1"]);
            Assert.Equal(TestString(57397, "SerializedException"), firedEvents[57397]["SerializedException"]);
            Assert.Equal(TestString(57397, "AppDomain"), firedEvents[57397]["AppDomain"]);

            // 57398 MaxInstancesExceeded - Multidata84TemplateA
            Assert.True(firedEvents.ContainsKey(57398), "Event 57398 (MaxInstancesExceeded) did not fire.");
            Assert.Equal(TestInt32(57398, 0), firedEvents[57398]["limit"]);
            Assert.Equal(TestString(57398, "AppDomain"), firedEvents[57398]["AppDomain"]);

            // 57399 TraceCodeEventLogCritical - TwoStringsTemplateTA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey(57399), "Event 57399 (TraceCodeEventLogCritical) did not fire.");
            Assert.Equal(TestString(57399, "ExtendedData"), firedEvents[57399]["ExtendedData"]);
            Assert.Equal(TestString(57399, "AppDomain"), firedEvents[57399]["AppDomain"]);

            // 57400 TraceCodeEventLogError - TwoStringsTemplateTA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey(57400), "Event 57400 (TraceCodeEventLogError) did not fire.");
            Assert.Equal(TestString(57400, "ExtendedData"), firedEvents[57400]["ExtendedData"]);
            Assert.Equal(TestString(57400, "AppDomain"), firedEvents[57400]["AppDomain"]);

            // 57401 TraceCodeEventLogInfo - TwoStringsTemplateTA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey(57401), "Event 57401 (TraceCodeEventLogInfo) did not fire.");
            Assert.Equal(TestString(57401, "ExtendedData"), firedEvents[57401]["ExtendedData"]);
            Assert.Equal(TestString(57401, "AppDomain"), firedEvents[57401]["AppDomain"]);

            // 57402 TraceCodeEventLogVerbose - TwoStringsTemplateTA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey(57402), "Event 57402 (TraceCodeEventLogVerbose) did not fire.");
            Assert.Equal(TestString(57402, "ExtendedData"), firedEvents[57402]["ExtendedData"]);
            Assert.Equal(TestString(57402, "AppDomain"), firedEvents[57402]["AppDomain"]);

            // 57403 TraceCodeEventLogWarning - TwoStringsTemplateTA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey(57403), "Event 57403 (TraceCodeEventLogWarning) did not fire.");
            Assert.Equal(TestString(57403, "ExtendedData"), firedEvents[57403]["ExtendedData"]);
            Assert.Equal(TestString(57403, "AppDomain"), firedEvents[57403]["AppDomain"]);

            // 57404 HandledExceptionWarning - ThreeStringsTemplateEA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey(57404), "Event 57404 (HandledExceptionWarning) did not fire.");
            Assert.Equal(TestString(57404, "data1"), firedEvents[57404]["data1"]);
            Assert.Equal(TestString(57404, "SerializedException"), firedEvents[57404]["SerializedException"]);
            Assert.Equal(TestString(57404, "AppDomain"), firedEvents[57404]["AppDomain"]);

            // 57405 HandledExceptionError - ThreeStringsTemplateEA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey(57405), "Event 57405 (HandledExceptionError) did not fire.");
            Assert.Equal(TestString(57405, "data1"), firedEvents[57405]["data1"]);
            Assert.Equal(TestString(57405, "SerializedException"), firedEvents[57405]["SerializedException"]);
            Assert.Equal(TestString(57405, "AppDomain"), firedEvents[57405]["AppDomain"]);

            // 57406 HandledExceptionVerbose - ThreeStringsTemplateEA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey(57406), "Event 57406 (HandledExceptionVerbose) did not fire.");
            Assert.Equal(TestString(57406, "data1"), firedEvents[57406]["data1"]);
            Assert.Equal(TestString(57406, "SerializedException"), firedEvents[57406]["SerializedException"]);
            Assert.Equal(TestString(57406, "AppDomain"), firedEvents[57406]["AppDomain"]);

            // 57407 ThrowingExceptionVerbose - FourStringsTemplateEA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey(57407), "Event 57407 (ThrowingExceptionVerbose) did not fire.");
            Assert.Equal(TestString(57407, "data1"), firedEvents[57407]["data1"]);
            Assert.Equal(TestString(57407, "data2"), firedEvents[57407]["data2"]);
            Assert.Equal(TestString(57407, "SerializedException"), firedEvents[57407]["SerializedException"]);
            Assert.Equal(TestString(57407, "AppDomain"), firedEvents[57407]["AppDomain"]);

            // 57408 EtwUnhandledException - ThreeStringsTemplateEA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey(57408), "Event 57408 (EtwUnhandledException) did not fire.");
            Assert.Equal(TestString(57408, "data1"), firedEvents[57408]["data1"]);
            Assert.Equal(TestString(57408, "SerializedException"), firedEvents[57408]["SerializedException"]);
            Assert.Equal(TestString(57408, "AppDomain"), firedEvents[57408]["AppDomain"]);

            // 57409 ThrowingEtwExceptionVerbose - FourStringsTemplateEA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey(57409), "Event 57409 (ThrowingEtwExceptionVerbose) did not fire.");
            Assert.Equal(TestString(57409, "data1"), firedEvents[57409]["data1"]);
            Assert.Equal(TestString(57409, "data2"), firedEvents[57409]["data2"]);
            Assert.Equal(TestString(57409, "SerializedException"), firedEvents[57409]["SerializedException"]);
            Assert.Equal(TestString(57409, "AppDomain"), firedEvents[57409]["AppDomain"]);

            // 57410 ThrowingEtwException - FourStringsTemplateEA (task=0, Guid.Empty)
            Assert.True(firedEvents.ContainsKey(57410), "Event 57410 (ThrowingEtwException) did not fire.");
            Assert.Equal(TestString(57410, "data1"), firedEvents[57410]["data1"]);
            Assert.Equal(TestString(57410, "data2"), firedEvents[57410]["data2"]);
            Assert.Equal(TestString(57410, "SerializedException"), firedEvents[57410]["SerializedException"]);
            Assert.Equal(TestString(57410, "AppDomain"), firedEvents[57410]["AppDomain"]);

            // 62326 HttpHandlerPickedForUrl - FourStringsTemplateA
            Assert.True(firedEvents.ContainsKey(62326), "Event 62326 (HttpHandlerPickedForUrl) did not fire.");
            Assert.Equal(TestString(62326, "data1"), firedEvents[62326]["data1"]);
            Assert.Equal(TestString(62326, "data2"), firedEvents[62326]["data2"]);
            Assert.Equal(TestString(62326, "data3"), firedEvents[62326]["data3"]);
            Assert.Equal(TestString(62326, "AppDomain"), firedEvents[62326]["AppDomain"]);
        }
    }
}
