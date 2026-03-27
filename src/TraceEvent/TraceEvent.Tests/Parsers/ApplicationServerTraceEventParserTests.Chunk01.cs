// Auto-generated test code for chunk 01 (events 100-214)
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.ApplicationServer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;
using TraceEventTests;

namespace TraceEventTests.Parsers
{
    public partial class ApplicationServerTraceEventParserTests
    {
        // Number of events in chunk 01
        private const int Chunk01EventCount = 37;

        #region Template Field Definitions for Chunk 01

        private static readonly TemplateField[] Multidata9TemplateHA_Fields = new TemplateField[]
        {
            new TemplateField("InstanceId", FieldType.Guid),
            new TemplateField("RecordNumber", FieldType.Int64),
            new TemplateField("ActivityDefinitionId", FieldType.UnicodeString),
            new TemplateField("State", FieldType.UnicodeString),
            new TemplateField("Annotations", FieldType.UnicodeString),
            new TemplateField("ProfileName", FieldType.UnicodeString),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata10TemplateHA_Fields = new TemplateField[]
        {
            new TemplateField("InstanceId", FieldType.Guid),
            new TemplateField("RecordNumber", FieldType.Int64),
            new TemplateField("ActivityDefinitionId", FieldType.UnicodeString),
            new TemplateField("SourceName", FieldType.UnicodeString),
            new TemplateField("SourceId", FieldType.UnicodeString),
            new TemplateField("SourceInstanceId", FieldType.UnicodeString),
            new TemplateField("SourceTypeName", FieldType.UnicodeString),
            new TemplateField("Exception", FieldType.UnicodeString),
            new TemplateField("Annotations", FieldType.UnicodeString),
            new TemplateField("ProfileName", FieldType.UnicodeString),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata8TemplateHA_Fields = new TemplateField[]
        {
            new TemplateField("InstanceId", FieldType.Guid),
            new TemplateField("RecordNumber", FieldType.Int64),
            new TemplateField("ActivityDefinitionId", FieldType.UnicodeString),
            new TemplateField("Reason", FieldType.UnicodeString),
            new TemplateField("Annotations", FieldType.UnicodeString),
            new TemplateField("ProfileName", FieldType.UnicodeString),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata4TemplateHA_Fields= new TemplateField[]
        {
            new TemplateField("InstanceId", FieldType.Guid),
            new TemplateField("RecordNumber", FieldType.Int64),
            new TemplateField("State", FieldType.UnicodeString),
            new TemplateField("Name", FieldType.UnicodeString),
            new TemplateField("ActivityId", FieldType.UnicodeString),
            new TemplateField("ActivityInstanceId", FieldType.UnicodeString),
            new TemplateField("ActivityTypeName", FieldType.UnicodeString),
            new TemplateField("Arguments", FieldType.UnicodeString),
            new TemplateField("Variables", FieldType.UnicodeString),
            new TemplateField("Annotations", FieldType.UnicodeString),
            new TemplateField("ProfileName", FieldType.UnicodeString),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata3TemplateHA_Fields = new TemplateField[]
        {
            new TemplateField("InstanceId", FieldType.Guid),
            new TemplateField("RecordNumber", FieldType.Int64),
            new TemplateField("Name", FieldType.UnicodeString),
            new TemplateField("ActivityId", FieldType.UnicodeString),
            new TemplateField("ActivityInstanceId", FieldType.UnicodeString),
            new TemplateField("ActivityTypeName", FieldType.UnicodeString),
            new TemplateField("ChildActivityName", FieldType.UnicodeString),
            new TemplateField("ChildActivityId", FieldType.UnicodeString),
            new TemplateField("ChildActivityInstanceId", FieldType.UnicodeString),
            new TemplateField("ChildActivityTypeName", FieldType.UnicodeString),
            new TemplateField("Annotations", FieldType.UnicodeString),
            new TemplateField("ProfileName", FieldType.UnicodeString),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata6TemplateHA_Fields = new TemplateField[]
        {
            new TemplateField("InstanceId", FieldType.Guid),
            new TemplateField("RecordNumber", FieldType.Int64),
            new TemplateField("FaultSourceActivityName", FieldType.UnicodeString),
            new TemplateField("FaultSourceActivityId", FieldType.UnicodeString),
            new TemplateField("FaultSourceActivityInstanceId", FieldType.UnicodeString),
            new TemplateField("FaultSourceActivityTypeName", FieldType.UnicodeString),
            new TemplateField("FaultHandlerActivityName", FieldType.UnicodeString),
            new TemplateField("FaultHandlerActivityId", FieldType.UnicodeString),
            new TemplateField("FaultHandlerActivityInstanceId", FieldType.UnicodeString),
            new TemplateField("FaultHandlerActivityTypeName", FieldType.UnicodeString),
            new TemplateField("Fault", FieldType.UnicodeString),
            new TemplateField("IsFaultSource", FieldType.UInt8),
            new TemplateField("Annotations", FieldType.UnicodeString),
            new TemplateField("ProfileName", FieldType.UnicodeString),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata5TemplateHA_Fields = new TemplateField[]
        {
            new TemplateField("InstanceId", FieldType.Guid),
            new TemplateField("RecordNumber", FieldType.Int64),
            new TemplateField("Name", FieldType.UnicodeString),
            new TemplateField("SubInstanceID", FieldType.Guid),
            new TemplateField("OwnerActivityName", FieldType.UnicodeString),
            new TemplateField("OwnerActivityId", FieldType.UnicodeString),
            new TemplateField("OwnerActivityInstanceId", FieldType.UnicodeString),
            new TemplateField("OwnerActivityTypeName", FieldType.UnicodeString),
            new TemplateField("Annotations", FieldType.UnicodeString),
            new TemplateField("ProfileName", FieldType.UnicodeString),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata7TemplateHA_Fields = new TemplateField[]
        {
            new TemplateField("InstanceId", FieldType.Guid),
            new TemplateField("RecordNumber", FieldType.Int64),
            new TemplateField("Name", FieldType.UnicodeString),
            new TemplateField("ActivityName", FieldType.UnicodeString),
            new TemplateField("ActivityId", FieldType.UnicodeString),
            new TemplateField("ActivityInstanceId", FieldType.UnicodeString),
            new TemplateField("ActivityTypeName", FieldType.UnicodeString),
            new TemplateField("Data", FieldType.UnicodeString),
            new TemplateField("Annotations", FieldType.UnicodeString),
            new TemplateField("ProfileName", FieldType.UnicodeString),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata11TemplateHA_Fields = new TemplateField[]
        {
            new TemplateField("InstanceId", FieldType.Guid),
            new TemplateField("RecordNumber", FieldType.Int64),
            new TemplateField("ActivityDefinitionId", FieldType.UnicodeString),
            new TemplateField("State", FieldType.UnicodeString),
            new TemplateField("Annotations", FieldType.UnicodeString),
            new TemplateField("ProfileName", FieldType.UnicodeString),
            new TemplateField("WorkflowDefinitionIdentity", FieldType.UnicodeString),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata12TemplateHA_Fields = new TemplateField[]
        {
            new TemplateField("InstanceId", FieldType.Guid),
            new TemplateField("RecordNumber", FieldType.Int64),
            new TemplateField("ActivityDefinitionId", FieldType.UnicodeString),
            new TemplateField("Reason", FieldType.UnicodeString),
            new TemplateField("Annotations", FieldType.UnicodeString),
            new TemplateField("ProfileName", FieldType.UnicodeString),
            new TemplateField("WorkflowDefinitionIdentity", FieldType.UnicodeString),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata13TemplateHA_Fields = new TemplateField[]
        {
            new TemplateField("InstanceId", FieldType.Guid),
            new TemplateField("RecordNumber", FieldType.Int64),
            new TemplateField("ActivityDefinitionId", FieldType.UnicodeString),
            new TemplateField("SourceName", FieldType.UnicodeString),
            new TemplateField("SourceId", FieldType.UnicodeString),
            new TemplateField("SourceInstanceId", FieldType.UnicodeString),
            new TemplateField("SourceTypeName", FieldType.UnicodeString),
            new TemplateField("Exception", FieldType.UnicodeString),
            new TemplateField("Annotations", FieldType.UnicodeString),
            new TemplateField("ProfileName", FieldType.UnicodeString),
            new TemplateField("WorkflowDefinitionIdentity", FieldType.UnicodeString),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata14TemplateHA_Fields = new TemplateField[]
        {
            new TemplateField("InstanceId", FieldType.Guid),
            new TemplateField("RecordNumber", FieldType.Int64),
            new TemplateField("ActivityDefinitionId", FieldType.UnicodeString),
            new TemplateField("State", FieldType.UnicodeString),
            new TemplateField("OriginalDefinitionIdentity", FieldType.UnicodeString),
            new TemplateField("UpdatedDefinitionIdentity", FieldType.UnicodeString),
            new TemplateField("Annotations", FieldType.UnicodeString),
            new TemplateField("ProfileName", FieldType.UnicodeString),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata1TemplateA_Fields = new TemplateField[]
        {
            new TemplateField("Size", FieldType.Int32),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata2TemplateA_Fields = new TemplateField[]
        {
            new TemplateField("PoolSize", FieldType.Int32),
            new TemplateField("Delta", FieldType.Int32),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] OneStringsTemplateA_Fields = new TemplateField[]
        {
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata23TemplateHA_Fields = new TemplateField[]
        {
            new TemplateField("TypeName", FieldType.UnicodeString),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata24TemplateHA_Fields = new TemplateField[]
        {
            new TemplateField("MethodName", FieldType.UnicodeString),
            new TemplateField("CallerInfo", FieldType.UnicodeString),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata25TemplateHA_Fields = new TemplateField[]
        {
            new TemplateField("TypeName", FieldType.UnicodeString),
            new TemplateField("Handled", FieldType.UInt8),
            new TemplateField("ExceptionTypeName", FieldType.UnicodeString),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata26TemplateHA_Fields = new TemplateField[]
        {
            new TemplateField("TypeName", FieldType.UnicodeString),
            new TemplateField("ExceptionTypeName", FieldType.UnicodeString),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata27TemplateHA_Fields = new TemplateField[]
        {
            new TemplateField("ThrottleName", FieldType.UnicodeString),
            new TemplateField("Limit", FieldType.Int64),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata28TemplateHA_Fields = new TemplateField[]
        {
            new TemplateField("MethodName", FieldType.UnicodeString),
            new TemplateField("Duration", FieldType.Int64),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata72TemplateHA_Fields = new TemplateField[]
        {
            new TemplateField("ServiceTypeName", FieldType.UnicodeString),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        #endregion

        /// <summary>
        /// Writes metadata entries for chunk 01 events (100-214).
        /// </summary>
        private void WriteMetadata_Chunk01(EventPipeWriterV5 writer, ref int metadataId)
        {
            int __metadataId = metadataId;
            writer.WriteMetadataBlock(w =>
            {
                // WorkflowInstanceRecord (Multidata9TemplateHA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "WorkflowInstanceRecord", 100));
                // WorkflowInstanceUnhandledExceptionRecord (Multidata10TemplateHA, opcode=150)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "WorkflowInstanceUnhandledExceptionRecord", 101) { OpCode = 150 });
                // WorkflowInstanceAbortedRecord (Multidata8TemplateHA, opcode=144)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "WorkflowInstanceAbortedRecord", 102) { OpCode = 144 });
                // ActivityStateRecord (Multidata4TemplateHA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ActivityStateRecord", 103));
                // ActivityScheduledRecord (Multidata3TemplateHA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ActivityScheduledRecord", 104));
                // FaultPropagationRecord (Multidata6TemplateHA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "FaultPropagationRecord", 105));
                // CancelRequestedRecord (Multidata3TemplateHA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "CancelRequestedRecord", 106));
                // BookmarkResumptionRecord (Multidata5TemplateHA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "BookmarkResumptionRecord", 107));
                // CustomTrackingRecordInfo (Multidata7TemplateHA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "CustomTrackingRecordInfo", 108));
                // CustomTrackingRecordWarning (Multidata7TemplateHA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "CustomTrackingRecordWarning", 110));
                // CustomTrackingRecordError (Multidata7TemplateHA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "CustomTrackingRecordError", 111));
                // WorkflowInstanceSuspendedRecord (Multidata8TemplateHA, opcode=146)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "WorkflowInstanceSuspendedRecord", 112) { OpCode = 146 });
                // WorkflowInstanceTerminatedRecord (Multidata8TemplateHA, opcode=148)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "WorkflowInstanceTerminatedRecord", 113) { OpCode = 148 });
                // WorkflowInstanceRecordWithId (Multidata11TemplateHA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "WorkflowInstanceRecordWithId", 114));
                // WorkflowInstanceAbortedRecordWithId (Multidata12TemplateHA, opcode=145)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "WorkflowInstanceAbortedRecordWithId", 115) { OpCode = 145 });
                // WorkflowInstanceSuspendedRecordWithId (Multidata12TemplateHA, opcode=147)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "WorkflowInstanceSuspendedRecordWithId", 116) { OpCode = 147 });
                // WorkflowInstanceTerminatedRecordWithId (Multidata12TemplateHA, opcode=149)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "WorkflowInstanceTerminatedRecordWithId", 117) { OpCode = 149 });
                // WorkflowInstanceUnhandledExceptionRecordWithId (Multidata13TemplateHA, opcode=151)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "WorkflowInstanceUnhandledExceptionRecordWithId", 118) { OpCode = 151 });
                // WorkflowInstanceUpdatedRecord (Multidata14TemplateHA, opcode=152)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "WorkflowInstanceUpdatedRecord", 119) { OpCode = 152 });
                // BufferPoolAllocation (Multidata1TemplateA, opcode=12)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "BufferPoolAllocation", 131) { OpCode = 12 });
                // BufferPoolChangeQuota (Multidata2TemplateA, opcode=13)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "BufferPoolChangeQuota", 132) { OpCode = 13 });
                // ActionItemScheduled (OneStringsTemplateA, opcode=1)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ActionItemScheduled", 133) { OpCode = 1 });
                // ActionItemCallbackInvoked (OneStringsTemplateA, opcode=2)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ActionItemCallbackInvoked", 134) { OpCode = 2 });
                // ClientMessageInspectorAfterReceiveInvoked (Multidata23TemplateHA, opcode=16)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ClientMessageInspectorAfterReceiveInvoked", 201) { OpCode = 16 });
                // ClientMessageInspectorBeforeSendInvoked (Multidata23TemplateHA, opcode=17)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ClientMessageInspectorBeforeSendInvoked", 202) { OpCode = 17 });
                // ClientParameterInspectorAfterCallInvoked (Multidata23TemplateHA, opcode=19)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ClientParameterInspectorAfterCallInvoked", 203) { OpCode = 19 });
                // ClientParameterInspectorBeforeCallInvoked (Multidata23TemplateHA, opcode=18)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ClientParameterInspectorBeforeCallInvoked", 204) { OpCode = 18 });
                // OperationInvoked (Multidata24TemplateHA, opcode=53)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "OperationInvoked", 205) { OpCode = 53 });
                // ErrorHandlerInvoked (Multidata25TemplateHA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ErrorHandlerInvoked", 206));
                // FaultProviderInvoked (Multidata26TemplateHA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "FaultProviderInvoked", 207));
                // MessageInspectorAfterReceiveInvoked (Multidata23TemplateHA, opcode=51)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "MessageInspectorAfterReceiveInvoked", 208) { OpCode = 51 });
                // MessageInspectorBeforeSendInvoked (Multidata23TemplateHA, opcode=52)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "MessageInspectorBeforeSendInvoked", 209) { OpCode = 52 });
                // MessageThrottleExceeded (Multidata27TemplateHA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "MessageThrottleExceeded", 210));
                // ParameterInspectorAfterCallInvoked (Multidata23TemplateHA, opcode=56)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ParameterInspectorAfterCallInvoked", 211) { OpCode = 56 });
                // ParameterInspectorBeforeCallInvoked (Multidata23TemplateHA, opcode=55)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ParameterInspectorBeforeCallInvoked", 212) { OpCode = 55 });
                // ServiceHostStarted (Multidata72TemplateHA, opcode=1)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ServiceHostStarted", 213) { OpCode = 1 });
                // OperationCompleted (Multidata28TemplateHA, opcode=54)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "OperationCompleted", 214) { OpCode = 54 });
            });
        
            metadataId = __metadataId;
        }

        /// <summary>
        /// Writes event payloads for chunk 01 events (100-214).
        /// </summary>
        private void WriteEvents_Chunk01(EventPipeWriterV5 writer, ref int metadataId, ref int sequenceNumber)
        {
            int __metadataId = metadataId;
            int __sequenceNumber = sequenceNumber;
            writer.WriteEventBlock(w =>
            {
                // WorkflowInstanceRecord (Multidata9TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(100, Multidata9TemplateHA_Fields));
                // WorkflowInstanceUnhandledExceptionRecord (Multidata10TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(101, Multidata10TemplateHA_Fields));
                // WorkflowInstanceAbortedRecord (Multidata8TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(102, Multidata8TemplateHA_Fields));
                // ActivityStateRecord (Multidata4TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(103, Multidata4TemplateHA_Fields));
                // ActivityScheduledRecord (Multidata3TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(104, Multidata3TemplateHA_Fields));
                // FaultPropagationRecord (Multidata6TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(105, Multidata6TemplateHA_Fields));
                // CancelRequestedRecord (Multidata3TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(106, Multidata3TemplateHA_Fields));
                // BookmarkResumptionRecord (Multidata5TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(107, Multidata5TemplateHA_Fields));
                // CustomTrackingRecordInfo (Multidata7TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(108, Multidata7TemplateHA_Fields));
                // CustomTrackingRecordWarning (Multidata7TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(110, Multidata7TemplateHA_Fields));
                // CustomTrackingRecordError (Multidata7TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(111, Multidata7TemplateHA_Fields));
                // WorkflowInstanceSuspendedRecord (Multidata8TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(112, Multidata8TemplateHA_Fields));
                // WorkflowInstanceTerminatedRecord (Multidata8TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(113, Multidata8TemplateHA_Fields));
                // WorkflowInstanceRecordWithId (Multidata11TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(114, Multidata11TemplateHA_Fields));
                // WorkflowInstanceAbortedRecordWithId (Multidata12TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(115, Multidata12TemplateHA_Fields));
                // WorkflowInstanceSuspendedRecordWithId (Multidata12TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(116, Multidata12TemplateHA_Fields));
                // WorkflowInstanceTerminatedRecordWithId (Multidata12TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(117, Multidata12TemplateHA_Fields));
                // WorkflowInstanceUnhandledExceptionRecordWithId (Multidata13TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(118, Multidata13TemplateHA_Fields));
                // WorkflowInstanceUpdatedRecord (Multidata14TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(119, Multidata14TemplateHA_Fields));
                // BufferPoolAllocation (Multidata1TemplateA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(131, Multidata1TemplateA_Fields));
                // BufferPoolChangeQuota (Multidata2TemplateA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(132, Multidata2TemplateA_Fields));
                // ActionItemScheduled (OneStringsTemplateA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(133, OneStringsTemplateA_Fields));
                // ActionItemCallbackInvoked (OneStringsTemplateA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(134, OneStringsTemplateA_Fields));
                // ClientMessageInspectorAfterReceiveInvoked (Multidata23TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(201, Multidata23TemplateHA_Fields));
                // ClientMessageInspectorBeforeSendInvoked (Multidata23TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(202, Multidata23TemplateHA_Fields));
                // ClientParameterInspectorAfterCallInvoked (Multidata23TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(203, Multidata23TemplateHA_Fields));
                // ClientParameterInspectorBeforeCallInvoked (Multidata23TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(204, Multidata23TemplateHA_Fields));
                // OperationInvoked (Multidata24TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(205, Multidata24TemplateHA_Fields));
                // ErrorHandlerInvoked (Multidata25TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(206, Multidata25TemplateHA_Fields));
                // FaultProviderInvoked (Multidata26TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(207, Multidata26TemplateHA_Fields));
                // MessageInspectorAfterReceiveInvoked (Multidata23TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(208, Multidata23TemplateHA_Fields));
                // MessageInspectorBeforeSendInvoked (Multidata23TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(209, Multidata23TemplateHA_Fields));
                // MessageThrottleExceeded (Multidata27TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(210, Multidata27TemplateHA_Fields));
                // ParameterInspectorAfterCallInvoked (Multidata23TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(211, Multidata23TemplateHA_Fields));
                // ParameterInspectorBeforeCallInvoked (Multidata23TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(212, Multidata23TemplateHA_Fields));
                // ServiceHostStarted (Multidata72TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(213, Multidata72TemplateHA_Fields));
                // OperationCompleted (Multidata28TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(214, Multidata28TemplateHA_Fields));
            });
        
            metadataId = __metadataId;
            sequenceNumber = __sequenceNumber;
        }

        /// <summary>
        /// Subscribes to chunk 01 events and records payload values.
        /// </summary>
        private void Subscribe_Chunk01(ApplicationServerTraceEventParser parser, Dictionary<string, Dictionary<string, object>> firedEvents)
        {
            // WorkflowInstanceRecord -> Multidata9TemplateHATraceData
            parser.WorkflowInstanceRecord += delegate(Multidata9TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["InstanceId"] = data.InstanceId;
                fields["RecordNumber"] = data.RecordNumber;
                fields["ActivityDefinitionId"] = data.ActivityDefinitionId;
                fields["State"] = data.State;
                fields["Annotations"] = data.Annotations;
                fields["ProfileName"] = data.ProfileName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["WorkflowInstanceRecord"] = fields;
            };

            // WorkflowInstanceUnhandledExceptionRecord -> Multidata10TemplateHATraceData
            parser.WorkflowInstanceUnhandledExceptionRecord += delegate(Multidata10TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["InstanceId"] = data.InstanceId;
                fields["RecordNumber"] = data.RecordNumber;
                fields["ActivityDefinitionId"] = data.ActivityDefinitionId;
                fields["SourceName"] = data.SourceName;
                fields["SourceId"] = data.SourceId;
                fields["SourceInstanceId"] = data.SourceInstanceId;
                fields["SourceTypeName"] = data.SourceTypeName;
                fields["Exception"] = data.Exception;
                fields["Annotations"] = data.Annotations;
                fields["ProfileName"] = data.ProfileName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["WorkflowInstanceUnhandledExceptionRecord"] = fields;
            };

            // WorkflowInstanceAbortedRecord -> Multidata8TemplateHATraceData
            parser.WorkflowInstanceAbortedRecord += delegate(Multidata8TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["InstanceId"] = data.InstanceId;
                fields["RecordNumber"] = data.RecordNumber;
                fields["ActivityDefinitionId"] = data.ActivityDefinitionId;
                fields["Reason"] = data.Reason;
                fields["Annotations"] = data.Annotations;
                fields["ProfileName"] = data.ProfileName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["WorkflowInstanceAbortedRecord"] = fields;
            };

            // ActivityStateRecord -> Multidata4TemplateHATraceData
            parser.ActivityStateRecord += delegate(Multidata4TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["InstanceId"] = data.InstanceId;
                fields["RecordNumber"] = data.RecordNumber;
                fields["State"] = data.State;
                fields["Name"] = data.Name;
                fields["ActivityId"] = data.ActivityId;
                fields["ActivityInstanceId"] = data.ActivityInstanceId;
                fields["ActivityTypeName"] = data.ActivityTypeName;
                fields["Arguments"] = data.Arguments;
                fields["Variables"] = data.Variables;
                fields["Annotations"] = data.Annotations;
                fields["ProfileName"] = data.ProfileName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["ActivityStateRecord"] = fields;
            };

            // ActivityScheduledRecord -> Multidata3TemplateHATraceData
            parser.ActivityScheduledRecord += delegate(Multidata3TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["InstanceId"] = data.InstanceId;
                fields["RecordNumber"] = data.RecordNumber;
                fields["Name"] = data.Name;
                fields["ActivityId"] = data.ActivityId;
                fields["ActivityInstanceId"] = data.ActivityInstanceId;
                fields["ActivityTypeName"] = data.ActivityTypeName;
                fields["ChildActivityName"] = data.ChildActivityName;
                fields["ChildActivityId"] = data.ChildActivityId;
                fields["ChildActivityInstanceId"] = data.ChildActivityInstanceId;
                fields["ChildActivityTypeName"] = data.ChildActivityTypeName;
                fields["Annotations"] = data.Annotations;
                fields["ProfileName"] = data.ProfileName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["ActivityScheduledRecord"] = fields;
            };

            // FaultPropagationRecord -> Multidata6TemplateHATraceData
            parser.FaultPropagationRecord += delegate(Multidata6TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["InstanceId"] = data.InstanceId;
                fields["RecordNumber"] = data.RecordNumber;
                fields["FaultSourceActivityName"] = data.FaultSourceActivityName;
                fields["FaultSourceActivityId"] = data.FaultSourceActivityId;
                fields["FaultSourceActivityInstanceId"] = data.FaultSourceActivityInstanceId;
                fields["FaultSourceActivityTypeName"] = data.FaultSourceActivityTypeName;
                fields["FaultHandlerActivityName"] = data.FaultHandlerActivityName;
                fields["FaultHandlerActivityId"] = data.FaultHandlerActivityId;
                fields["FaultHandlerActivityInstanceId"] = data.FaultHandlerActivityInstanceId;
                fields["FaultHandlerActivityTypeName"] = data.FaultHandlerActivityTypeName;
                fields["Fault"] = data.Fault;
                fields["IsFaultSource"] = data.IsFaultSource;
                fields["Annotations"] = data.Annotations;
                fields["ProfileName"] = data.ProfileName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["FaultPropagationRecord"] = fields;
            };

            // CancelRequestedRecord -> Multidata3TemplateHATraceData
            parser.CancelRequestedRecord += delegate(Multidata3TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["InstanceId"] = data.InstanceId;
                fields["RecordNumber"] = data.RecordNumber;
                fields["Name"] = data.Name;
                fields["ActivityId"] = data.ActivityId;
                fields["ActivityInstanceId"] = data.ActivityInstanceId;
                fields["ActivityTypeName"] = data.ActivityTypeName;
                fields["ChildActivityName"] = data.ChildActivityName;
                fields["ChildActivityId"] = data.ChildActivityId;
                fields["ChildActivityInstanceId"] = data.ChildActivityInstanceId;
                fields["ChildActivityTypeName"] = data.ChildActivityTypeName;
                fields["Annotations"] = data.Annotations;
                fields["ProfileName"] = data.ProfileName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["CancelRequestedRecord"] = fields;
            };

            // BookmarkResumptionRecord -> Multidata5TemplateHATraceData
            parser.BookmarkResumptionRecord += delegate(Multidata5TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["InstanceId"] = data.InstanceId;
                fields["RecordNumber"] = data.RecordNumber;
                fields["Name"] = data.Name;
                fields["SubInstanceID"] = data.SubInstanceID;
                fields["OwnerActivityName"] = data.OwnerActivityName;
                fields["OwnerActivityId"] = data.OwnerActivityId;
                fields["OwnerActivityInstanceId"] = data.OwnerActivityInstanceId;
                fields["OwnerActivityTypeName"] = data.OwnerActivityTypeName;
                fields["Annotations"] = data.Annotations;
                fields["ProfileName"] = data.ProfileName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["BookmarkResumptionRecord"] = fields;
            };

            // CustomTrackingRecordInfo -> Multidata7TemplateHATraceData
            parser.CustomTrackingRecordInfo += delegate(Multidata7TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["InstanceId"] = data.InstanceId;
                fields["RecordNumber"] = data.RecordNumber;
                fields["Name"] = data.Name;
                fields["ActivityName"] = data.ActivityName;
                fields["ActivityId"] = data.ActivityId;
                fields["ActivityInstanceId"] = data.ActivityInstanceId;
                fields["ActivityTypeName"] = data.ActivityTypeName;
                fields["Data"] = data.Data;
                fields["Annotations"] = data.Annotations;
                fields["ProfileName"] = data.ProfileName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["CustomTrackingRecordInfo"] = fields;
            };

            // CustomTrackingRecordWarning -> Multidata7TemplateHATraceData
            parser.CustomTrackingRecordWarning += delegate(Multidata7TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["InstanceId"] = data.InstanceId;
                fields["RecordNumber"] = data.RecordNumber;
                fields["Name"] = data.Name;
                fields["ActivityName"] = data.ActivityName;
                fields["ActivityId"] = data.ActivityId;
                fields["ActivityInstanceId"] = data.ActivityInstanceId;
                fields["ActivityTypeName"] = data.ActivityTypeName;
                fields["Data"] = data.Data;
                fields["Annotations"] = data.Annotations;
                fields["ProfileName"] = data.ProfileName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["CustomTrackingRecordWarning"] = fields;
            };

            // CustomTrackingRecordError -> Multidata7TemplateHATraceData
            parser.CustomTrackingRecordError += delegate(Multidata7TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["InstanceId"] = data.InstanceId;
                fields["RecordNumber"] = data.RecordNumber;
                fields["Name"] = data.Name;
                fields["ActivityName"] = data.ActivityName;
                fields["ActivityId"] = data.ActivityId;
                fields["ActivityInstanceId"] = data.ActivityInstanceId;
                fields["ActivityTypeName"] = data.ActivityTypeName;
                fields["Data"] = data.Data;
                fields["Annotations"] = data.Annotations;
                fields["ProfileName"] = data.ProfileName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["CustomTrackingRecordError"] = fields;
            };

            // WorkflowInstanceSuspendedRecord -> Multidata8TemplateHATraceData
            parser.WorkflowInstanceSuspendedRecord += delegate(Multidata8TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["InstanceId"] = data.InstanceId;
                fields["RecordNumber"] = data.RecordNumber;
                fields["ActivityDefinitionId"] = data.ActivityDefinitionId;
                fields["Reason"] = data.Reason;
                fields["Annotations"] = data.Annotations;
                fields["ProfileName"] = data.ProfileName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["WorkflowInstanceSuspendedRecord"] = fields;
            };

            // WorkflowInstanceTerminatedRecord -> Multidata8TemplateHATraceData
            parser.WorkflowInstanceTerminatedRecord += delegate(Multidata8TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["InstanceId"] = data.InstanceId;
                fields["RecordNumber"] = data.RecordNumber;
                fields["ActivityDefinitionId"] = data.ActivityDefinitionId;
                fields["Reason"] = data.Reason;
                fields["Annotations"] = data.Annotations;
                fields["ProfileName"] = data.ProfileName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["WorkflowInstanceTerminatedRecord"] = fields;
            };

            // WorkflowInstanceRecordWithId -> Multidata11TemplateHATraceData
            parser.WorkflowInstanceRecordWithId += delegate(Multidata11TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["InstanceId"] = data.InstanceId;
                fields["RecordNumber"] = data.RecordNumber;
                fields["ActivityDefinitionId"] = data.ActivityDefinitionId;
                fields["State"] = data.State;
                fields["Annotations"] = data.Annotations;
                fields["ProfileName"] = data.ProfileName;
                fields["WorkflowDefinitionIdentity"] = data.WorkflowDefinitionIdentity;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["WorkflowInstanceRecordWithId"] = fields;
            };

            // WorkflowInstanceAbortedRecordWithId -> Multidata12TemplateHATraceData
            parser.WorkflowInstanceAbortedRecordWithId += delegate(Multidata12TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["InstanceId"] = data.InstanceId;
                fields["RecordNumber"] = data.RecordNumber;
                fields["ActivityDefinitionId"] = data.ActivityDefinitionId;
                fields["Reason"] = data.Reason;
                fields["Annotations"] = data.Annotations;
                fields["ProfileName"] = data.ProfileName;
                fields["WorkflowDefinitionIdentity"] = data.WorkflowDefinitionIdentity;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["WorkflowInstanceAbortedRecordWithId"] = fields;
            };

            // WorkflowInstanceSuspendedRecordWithId -> Multidata12TemplateHATraceData
            parser.WorkflowInstanceSuspendedRecordWithId += delegate(Multidata12TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["InstanceId"] = data.InstanceId;
                fields["RecordNumber"] = data.RecordNumber;
                fields["ActivityDefinitionId"] = data.ActivityDefinitionId;
                fields["Reason"] = data.Reason;
                fields["Annotations"] = data.Annotations;
                fields["ProfileName"] = data.ProfileName;
                fields["WorkflowDefinitionIdentity"] = data.WorkflowDefinitionIdentity;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["WorkflowInstanceSuspendedRecordWithId"] = fields;
            };

            // WorkflowInstanceTerminatedRecordWithId -> Multidata12TemplateHATraceData
            parser.WorkflowInstanceTerminatedRecordWithId += delegate(Multidata12TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["InstanceId"] = data.InstanceId;
                fields["RecordNumber"] = data.RecordNumber;
                fields["ActivityDefinitionId"] = data.ActivityDefinitionId;
                fields["Reason"] = data.Reason;
                fields["Annotations"] = data.Annotations;
                fields["ProfileName"] = data.ProfileName;
                fields["WorkflowDefinitionIdentity"] = data.WorkflowDefinitionIdentity;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["WorkflowInstanceTerminatedRecordWithId"] = fields;
            };

            // WorkflowInstanceUnhandledExceptionRecordWithId -> Multidata13TemplateHATraceData
            parser.WorkflowInstanceUnhandledExceptionRecordWithId += delegate(Multidata13TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["InstanceId"] = data.InstanceId;
                fields["RecordNumber"] = data.RecordNumber;
                fields["ActivityDefinitionId"] = data.ActivityDefinitionId;
                fields["SourceName"] = data.SourceName;
                fields["SourceId"] = data.SourceId;
                fields["SourceInstanceId"] = data.SourceInstanceId;
                fields["SourceTypeName"] = data.SourceTypeName;
                fields["Exception"] = data.Exception;
                fields["Annotations"] = data.Annotations;
                fields["ProfileName"] = data.ProfileName;
                fields["WorkflowDefinitionIdentity"] = data.WorkflowDefinitionIdentity;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["WorkflowInstanceUnhandledExceptionRecordWithId"] = fields;
            };

            // WorkflowInstanceUpdatedRecord -> Multidata14TemplateHATraceData
            parser.WorkflowInstanceUpdatedRecord += delegate(Multidata14TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["InstanceId"] = data.InstanceId;
                fields["RecordNumber"] = data.RecordNumber;
                fields["ActivityDefinitionId"] = data.ActivityDefinitionId;
                fields["State"] = data.State;
                fields["OriginalDefinitionIdentity"] = data.OriginalDefinitionIdentity;
                fields["UpdatedDefinitionIdentity"] = data.UpdatedDefinitionIdentity;
                fields["Annotations"] = data.Annotations;
                fields["ProfileName"] = data.ProfileName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["WorkflowInstanceUpdatedRecord"] = fields;
            };

            // BufferPoolAllocation -> Multidata1TemplateATraceData
            parser.BufferPoolAllocation += delegate(Multidata1TemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["Size"] = data.Size;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["BufferPoolAllocation"] = fields;
            };

            // BufferPoolChangeQuota -> Multidata2TemplateATraceData
            parser.BufferPoolChangeQuota += delegate(Multidata2TemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["PoolSize"] = data.PoolSize;
                fields["Delta"] = data.Delta;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["BufferPoolChangeQuota"] = fields;
            };

            // ActionItemScheduled -> OneStringsTemplateATraceData
            parser.ActionItemScheduled += delegate(OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents["ActionItemScheduled"] = fields;
            };

            // ActionItemCallbackInvoked -> OneStringsTemplateATraceData
            parser.ActionItemCallbackInvoked += delegate(OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents["ActionItemCallbackInvoked"] = fields;
            };

            // ClientMessageInspectorAfterReceiveInvoked -> Multidata23TemplateHATraceData
            parser.ClientMessageInspectorAfterReceiveInvoked += delegate(Multidata23TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["TypeName"] = data.TypeName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["ClientMessageInspectorAfterReceiveInvoked"] = fields;
            };

            // ClientMessageInspectorBeforeSendInvoked -> Multidata23TemplateHATraceData
            parser.ClientMessageInspectorBeforeSendInvoked += delegate(Multidata23TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["TypeName"] = data.TypeName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["ClientMessageInspectorBeforeSendInvoked"] = fields;
            };

            // ClientParameterInspectorAfterCallInvoked -> Multidata23TemplateHATraceData
            parser.ClientParameterInspectorAfterCallInvoked += delegate(Multidata23TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["TypeName"] = data.TypeName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["ClientParameterInspectorAfterCallInvoked"] = fields;
            };

            // ClientParameterInspectorBeforeCallInvoked -> Multidata23TemplateHATraceData
            parser.ClientParameterInspectorBeforeCallInvoked += delegate(Multidata23TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["TypeName"] = data.TypeName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["ClientParameterInspectorBeforeCallInvoked"] = fields;
            };

            // OperationInvoked -> Multidata24TemplateHATraceData
            parser.OperationInvoked += delegate(Multidata24TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["MethodName"] = data.MethodName;
                fields["CallerInfo"] = data.CallerInfo;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["OperationInvoked"] = fields;
            };

            // ErrorHandlerInvoked -> Multidata25TemplateHATraceData
            parser.ErrorHandlerInvoked += delegate(Multidata25TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["TypeName"] = data.TypeName;
                fields["Handled"] = data.Handled;
                fields["ExceptionTypeName"] = data.ExceptionTypeName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["ErrorHandlerInvoked"] = fields;
            };

            // FaultProviderInvoked -> Multidata26TemplateHATraceData
            parser.FaultProviderInvoked += delegate(Multidata26TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["TypeName"] = data.TypeName;
                fields["ExceptionTypeName"] = data.ExceptionTypeName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["FaultProviderInvoked"] = fields;
            };

            // MessageInspectorAfterReceiveInvoked -> Multidata23TemplateHATraceData
            parser.MessageInspectorAfterReceiveInvoked += delegate(Multidata23TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["TypeName"] = data.TypeName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["MessageInspectorAfterReceiveInvoked"] = fields;
            };

            // MessageInspectorBeforeSendInvoked -> Multidata23TemplateHATraceData
            parser.MessageInspectorBeforeSendInvoked += delegate(Multidata23TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["TypeName"] = data.TypeName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["MessageInspectorBeforeSendInvoked"] = fields;
            };

            // MessageThrottleExceeded -> Multidata27TemplateHATraceData
            parser.MessageThrottleExceeded += delegate(Multidata27TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["ThrottleName"] = data.ThrottleName;
                fields["Limit"] = data.Limit;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["MessageThrottleExceeded"] = fields;
            };

            // ParameterInspectorAfterCallInvoked -> Multidata23TemplateHATraceData
            parser.ParameterInspectorAfterCallInvoked += delegate(Multidata23TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["TypeName"] = data.TypeName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["ParameterInspectorAfterCallInvoked"] = fields;
            };

            // ParameterInspectorBeforeCallInvoked -> Multidata23TemplateHATraceData
            parser.ParameterInspectorBeforeCallInvoked += delegate(Multidata23TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["TypeName"] = data.TypeName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["ParameterInspectorBeforeCallInvoked"] = fields;
            };

            // ServiceHostStarted -> Multidata72TemplateHATraceData
            parser.ServiceHostStarted += delegate(Multidata72TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["ServiceTypeName"] = data.ServiceTypeName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["ServiceHostStarted"] = fields;
            };

            // OperationCompleted -> Multidata28TemplateHATraceData
            parser.OperationCompleted += delegate(Multidata28TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["MethodName"] = data.MethodName;
                fields["Duration"] = data.Duration;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["OperationCompleted"] = fields;
            };
        }

        /// <summary>
        /// Validates chunk 01 event payload values.
        /// </summary>
        private void Validate_Chunk01(Dictionary<string, Dictionary<string, object>> firedEvents)
        {
            // WorkflowInstanceRecord (Multidata9TemplateHA)
            Assert.True(firedEvents.ContainsKey("WorkflowInstanceRecord"), "Event WorkflowInstanceRecord did not fire");
            var eWorkflowInstanceRecord = firedEvents["WorkflowInstanceRecord"];
            Assert.Equal(TestGuid(100, 0), (Guid)eWorkflowInstanceRecord["InstanceId"]);
            Assert.Equal(TestInt64(100, 1), (long)eWorkflowInstanceRecord["RecordNumber"]);
            // Field index 2 is EventTime (skipped by parser)
            Assert.Equal(TestString(100, "ActivityDefinitionId"), (string)eWorkflowInstanceRecord["ActivityDefinitionId"]);
            Assert.Equal(TestString(100, "State"), (string)eWorkflowInstanceRecord["State"]);
            Assert.Equal(TestString(100, "Annotations"), (string)eWorkflowInstanceRecord["Annotations"]);
            Assert.Equal(TestString(100, "ProfileName"), (string)eWorkflowInstanceRecord["ProfileName"]);
            Assert.Equal(TestString(100, "HostReference"), (string)eWorkflowInstanceRecord["HostReference"]);
            Assert.Equal(TestString(100, "AppDomain"), (string)eWorkflowInstanceRecord["AppDomain"]);

            // WorkflowInstanceUnhandledExceptionRecord (Multidata10TemplateHA)
            Assert.True(firedEvents.ContainsKey("WorkflowInstanceUnhandledExceptionRecord"), "Event WorkflowInstanceUnhandledExceptionRecord did not fire");
            var eWorkflowInstanceUnhandledExceptionRecord = firedEvents["WorkflowInstanceUnhandledExceptionRecord"];
            Assert.Equal(TestGuid(101, 0), (Guid)eWorkflowInstanceUnhandledExceptionRecord["InstanceId"]);
            Assert.Equal(TestInt64(101, 1), (long)eWorkflowInstanceUnhandledExceptionRecord["RecordNumber"]);
            Assert.Equal(TestString(101, "ActivityDefinitionId"), (string)eWorkflowInstanceUnhandledExceptionRecord["ActivityDefinitionId"]);
            Assert.Equal(TestString(101, "SourceName"), (string)eWorkflowInstanceUnhandledExceptionRecord["SourceName"]);
            Assert.Equal(TestString(101, "SourceId"), (string)eWorkflowInstanceUnhandledExceptionRecord["SourceId"]);
            Assert.Equal(TestString(101, "SourceInstanceId"), (string)eWorkflowInstanceUnhandledExceptionRecord["SourceInstanceId"]);
            Assert.Equal(TestString(101, "SourceTypeName"), (string)eWorkflowInstanceUnhandledExceptionRecord["SourceTypeName"]);
            Assert.Equal(TestString(101, "Exception"), (string)eWorkflowInstanceUnhandledExceptionRecord["Exception"]);
            Assert.Equal(TestString(101, "Annotations"), (string)eWorkflowInstanceUnhandledExceptionRecord["Annotations"]);
            Assert.Equal(TestString(101, "ProfileName"), (string)eWorkflowInstanceUnhandledExceptionRecord["ProfileName"]);
            Assert.Equal(TestString(101, "HostReference"), (string)eWorkflowInstanceUnhandledExceptionRecord["HostReference"]);
            Assert.Equal(TestString(101, "AppDomain"), (string)eWorkflowInstanceUnhandledExceptionRecord["AppDomain"]);

            // WorkflowInstanceAbortedRecord (Multidata8TemplateHA)
            Assert.True(firedEvents.ContainsKey("WorkflowInstanceAbortedRecord"), "Event WorkflowInstanceAbortedRecord did not fire");
            var eWorkflowInstanceAbortedRecord = firedEvents["WorkflowInstanceAbortedRecord"];
            Assert.Equal(TestGuid(102, 0), (Guid)eWorkflowInstanceAbortedRecord["InstanceId"]);
            Assert.Equal(TestInt64(102, 1), (long)eWorkflowInstanceAbortedRecord["RecordNumber"]);
            Assert.Equal(TestString(102, "ActivityDefinitionId"), (string)eWorkflowInstanceAbortedRecord["ActivityDefinitionId"]);
            Assert.Equal(TestString(102, "Reason"), (string)eWorkflowInstanceAbortedRecord["Reason"]);
            Assert.Equal(TestString(102, "Annotations"), (string)eWorkflowInstanceAbortedRecord["Annotations"]);
            Assert.Equal(TestString(102, "ProfileName"), (string)eWorkflowInstanceAbortedRecord["ProfileName"]);
            Assert.Equal(TestString(102, "HostReference"), (string)eWorkflowInstanceAbortedRecord["HostReference"]);
            Assert.Equal(TestString(102, "AppDomain"), (string)eWorkflowInstanceAbortedRecord["AppDomain"]);

            // ActivityStateRecord (Multidata4TemplateHA)
            Assert.True(firedEvents.ContainsKey("ActivityStateRecord"), "Event ActivityStateRecord did not fire");
            var eActivityStateRecord = firedEvents["ActivityStateRecord"];
            Assert.Equal(TestGuid(103, 0), (Guid)eActivityStateRecord["InstanceId"]);
            Assert.Equal(TestInt64(103, 1), (long)eActivityStateRecord["RecordNumber"]);
            Assert.Equal(TestString(103, "State"), (string)eActivityStateRecord["State"]);
            Assert.Equal(TestString(103, "Name"), (string)eActivityStateRecord["Name"]);
            Assert.Equal(TestString(103, "ActivityId"), (string)eActivityStateRecord["ActivityId"]);
            Assert.Equal(TestString(103, "ActivityInstanceId"), (string)eActivityStateRecord["ActivityInstanceId"]);
            Assert.Equal(TestString(103, "ActivityTypeName"), (string)eActivityStateRecord["ActivityTypeName"]);
            Assert.Equal(TestString(103, "Arguments"), (string)eActivityStateRecord["Arguments"]);
            Assert.Equal(TestString(103, "Variables"), (string)eActivityStateRecord["Variables"]);
            Assert.Equal(TestString(103, "Annotations"), (string)eActivityStateRecord["Annotations"]);
            Assert.Equal(TestString(103, "ProfileName"), (string)eActivityStateRecord["ProfileName"]);
            Assert.Equal(TestString(103, "HostReference"), (string)eActivityStateRecord["HostReference"]);
            Assert.Equal(TestString(103, "AppDomain"), (string)eActivityStateRecord["AppDomain"]);

            // ActivityScheduledRecord (Multidata3TemplateHA)
            Assert.True(firedEvents.ContainsKey("ActivityScheduledRecord"), "Event ActivityScheduledRecord did not fire");
            var eActivityScheduledRecord = firedEvents["ActivityScheduledRecord"];
            Assert.Equal(TestGuid(104, 0), (Guid)eActivityScheduledRecord["InstanceId"]);
            Assert.Equal(TestInt64(104, 1), (long)eActivityScheduledRecord["RecordNumber"]);
            Assert.Equal(TestString(104, "Name"), (string)eActivityScheduledRecord["Name"]);
            Assert.Equal(TestString(104, "ActivityId"), (string)eActivityScheduledRecord["ActivityId"]);
            Assert.Equal(TestString(104, "ActivityInstanceId"), (string)eActivityScheduledRecord["ActivityInstanceId"]);
            Assert.Equal(TestString(104, "ActivityTypeName"), (string)eActivityScheduledRecord["ActivityTypeName"]);
            Assert.Equal(TestString(104, "ChildActivityName"), (string)eActivityScheduledRecord["ChildActivityName"]);
            Assert.Equal(TestString(104, "ChildActivityId"), (string)eActivityScheduledRecord["ChildActivityId"]);
            Assert.Equal(TestString(104, "ChildActivityInstanceId"), (string)eActivityScheduledRecord["ChildActivityInstanceId"]);
            Assert.Equal(TestString(104, "ChildActivityTypeName"), (string)eActivityScheduledRecord["ChildActivityTypeName"]);
            Assert.Equal(TestString(104, "Annotations"), (string)eActivityScheduledRecord["Annotations"]);
            Assert.Equal(TestString(104, "ProfileName"), (string)eActivityScheduledRecord["ProfileName"]);
            Assert.Equal(TestString(104, "HostReference"), (string)eActivityScheduledRecord["HostReference"]);
            Assert.Equal(TestString(104, "AppDomain"), (string)eActivityScheduledRecord["AppDomain"]);

            // FaultPropagationRecord (Multidata6TemplateHA)
            Assert.True(firedEvents.ContainsKey("FaultPropagationRecord"), "Event FaultPropagationRecord did not fire");
            var eFaultPropagationRecord = firedEvents["FaultPropagationRecord"];
            Assert.Equal(TestGuid(105, 0), (Guid)eFaultPropagationRecord["InstanceId"]);
            Assert.Equal(TestInt64(105, 1), (long)eFaultPropagationRecord["RecordNumber"]);
            Assert.Equal(TestString(105, "FaultSourceActivityName"), (string)eFaultPropagationRecord["FaultSourceActivityName"]);
            Assert.Equal(TestString(105, "FaultSourceActivityId"), (string)eFaultPropagationRecord["FaultSourceActivityId"]);
            Assert.Equal(TestString(105, "FaultSourceActivityInstanceId"), (string)eFaultPropagationRecord["FaultSourceActivityInstanceId"]);
            Assert.Equal(TestString(105, "FaultSourceActivityTypeName"), (string)eFaultPropagationRecord["FaultSourceActivityTypeName"]);
            Assert.Equal(TestString(105, "FaultHandlerActivityName"), (string)eFaultPropagationRecord["FaultHandlerActivityName"]);
            Assert.Equal(TestString(105, "FaultHandlerActivityId"), (string)eFaultPropagationRecord["FaultHandlerActivityId"]);
            Assert.Equal(TestString(105, "FaultHandlerActivityInstanceId"), (string)eFaultPropagationRecord["FaultHandlerActivityInstanceId"]);
            Assert.Equal(TestString(105, "FaultHandlerActivityTypeName"), (string)eFaultPropagationRecord["FaultHandlerActivityTypeName"]);
            Assert.Equal(TestString(105, "Fault"), (string)eFaultPropagationRecord["Fault"]);
            Assert.Equal((int)TestByte(105, 11), (int)eFaultPropagationRecord["IsFaultSource"]);
            Assert.Equal(TestString(105, "Annotations"), (string)eFaultPropagationRecord["Annotations"]);
            Assert.Equal(TestString(105, "ProfileName"), (string)eFaultPropagationRecord["ProfileName"]);
            Assert.Equal(TestString(105, "HostReference"), (string)eFaultPropagationRecord["HostReference"]);
            Assert.Equal(TestString(105, "AppDomain"), (string)eFaultPropagationRecord["AppDomain"]);

            // CancelRequestedRecord (Multidata3TemplateHA)
            Assert.True(firedEvents.ContainsKey("CancelRequestedRecord"), "Event CancelRequestedRecord did not fire");
            var eCancelRequestedRecord = firedEvents["CancelRequestedRecord"];
            Assert.Equal(TestGuid(106, 0), (Guid)eCancelRequestedRecord["InstanceId"]);
            Assert.Equal(TestInt64(106, 1), (long)eCancelRequestedRecord["RecordNumber"]);
            Assert.Equal(TestString(106, "Name"), (string)eCancelRequestedRecord["Name"]);
            Assert.Equal(TestString(106, "ActivityId"), (string)eCancelRequestedRecord["ActivityId"]);
            Assert.Equal(TestString(106, "ActivityInstanceId"), (string)eCancelRequestedRecord["ActivityInstanceId"]);
            Assert.Equal(TestString(106, "ActivityTypeName"), (string)eCancelRequestedRecord["ActivityTypeName"]);
            Assert.Equal(TestString(106, "ChildActivityName"), (string)eCancelRequestedRecord["ChildActivityName"]);
            Assert.Equal(TestString(106, "ChildActivityId"), (string)eCancelRequestedRecord["ChildActivityId"]);
            Assert.Equal(TestString(106, "ChildActivityInstanceId"), (string)eCancelRequestedRecord["ChildActivityInstanceId"]);
            Assert.Equal(TestString(106, "ChildActivityTypeName"), (string)eCancelRequestedRecord["ChildActivityTypeName"]);
            Assert.Equal(TestString(106, "Annotations"), (string)eCancelRequestedRecord["Annotations"]);
            Assert.Equal(TestString(106, "ProfileName"), (string)eCancelRequestedRecord["ProfileName"]);
            Assert.Equal(TestString(106, "HostReference"), (string)eCancelRequestedRecord["HostReference"]);
            Assert.Equal(TestString(106, "AppDomain"), (string)eCancelRequestedRecord["AppDomain"]);

            // BookmarkResumptionRecord (Multidata5TemplateHA)
            Assert.True(firedEvents.ContainsKey("BookmarkResumptionRecord"), "Event BookmarkResumptionRecord did not fire");
            var eBookmarkResumptionRecord = firedEvents["BookmarkResumptionRecord"];
            Assert.Equal(TestGuid(107, 0), (Guid)eBookmarkResumptionRecord["InstanceId"]);
            Assert.Equal(TestInt64(107, 1), (long)eBookmarkResumptionRecord["RecordNumber"]);
            Assert.Equal(TestString(107, "Name"), (string)eBookmarkResumptionRecord["Name"]);
            Assert.Equal(TestGuid(107, 3), (Guid)eBookmarkResumptionRecord["SubInstanceID"]);
            Assert.Equal(TestString(107, "OwnerActivityName"), (string)eBookmarkResumptionRecord["OwnerActivityName"]);
            Assert.Equal(TestString(107, "OwnerActivityId"), (string)eBookmarkResumptionRecord["OwnerActivityId"]);
            Assert.Equal(TestString(107, "OwnerActivityInstanceId"), (string)eBookmarkResumptionRecord["OwnerActivityInstanceId"]);
            Assert.Equal(TestString(107, "OwnerActivityTypeName"), (string)eBookmarkResumptionRecord["OwnerActivityTypeName"]);
            Assert.Equal(TestString(107, "Annotations"), (string)eBookmarkResumptionRecord["Annotations"]);
            Assert.Equal(TestString(107, "ProfileName"), (string)eBookmarkResumptionRecord["ProfileName"]);
            Assert.Equal(TestString(107, "HostReference"), (string)eBookmarkResumptionRecord["HostReference"]);
            Assert.Equal(TestString(107, "AppDomain"), (string)eBookmarkResumptionRecord["AppDomain"]);

            // CustomTrackingRecordInfo (Multidata7TemplateHA)
            Assert.True(firedEvents.ContainsKey("CustomTrackingRecordInfo"), "Event CustomTrackingRecordInfo did not fire");
            var eCustomTrackingRecordInfo = firedEvents["CustomTrackingRecordInfo"];
            Assert.Equal(TestGuid(108, 0), (Guid)eCustomTrackingRecordInfo["InstanceId"]);
            Assert.Equal(TestInt64(108, 1), (long)eCustomTrackingRecordInfo["RecordNumber"]);
            Assert.Equal(TestString(108, "Name"), (string)eCustomTrackingRecordInfo["Name"]);
            Assert.Equal(TestString(108, "ActivityName"), (string)eCustomTrackingRecordInfo["ActivityName"]);
            Assert.Equal(TestString(108, "ActivityId"), (string)eCustomTrackingRecordInfo["ActivityId"]);
            Assert.Equal(TestString(108, "ActivityInstanceId"), (string)eCustomTrackingRecordInfo["ActivityInstanceId"]);
            Assert.Equal(TestString(108, "ActivityTypeName"), (string)eCustomTrackingRecordInfo["ActivityTypeName"]);
            Assert.Equal(TestString(108, "Data"), (string)eCustomTrackingRecordInfo["Data"]);
            Assert.Equal(TestString(108, "Annotations"), (string)eCustomTrackingRecordInfo["Annotations"]);
            Assert.Equal(TestString(108, "ProfileName"), (string)eCustomTrackingRecordInfo["ProfileName"]);
            Assert.Equal(TestString(108, "HostReference"), (string)eCustomTrackingRecordInfo["HostReference"]);
            Assert.Equal(TestString(108, "AppDomain"), (string)eCustomTrackingRecordInfo["AppDomain"]);

            // CustomTrackingRecordWarning (Multidata7TemplateHA)
            Assert.True(firedEvents.ContainsKey("CustomTrackingRecordWarning"), "Event CustomTrackingRecordWarning did not fire");
            var eCustomTrackingRecordWarning = firedEvents["CustomTrackingRecordWarning"];
            Assert.Equal(TestGuid(110, 0), (Guid)eCustomTrackingRecordWarning["InstanceId"]);
            Assert.Equal(TestInt64(110, 1), (long)eCustomTrackingRecordWarning["RecordNumber"]);
            Assert.Equal(TestString(110, "Name"), (string)eCustomTrackingRecordWarning["Name"]);
            Assert.Equal(TestString(110, "ActivityName"), (string)eCustomTrackingRecordWarning["ActivityName"]);
            Assert.Equal(TestString(110, "ActivityId"), (string)eCustomTrackingRecordWarning["ActivityId"]);
            Assert.Equal(TestString(110, "ActivityInstanceId"), (string)eCustomTrackingRecordWarning["ActivityInstanceId"]);
            Assert.Equal(TestString(110, "ActivityTypeName"), (string)eCustomTrackingRecordWarning["ActivityTypeName"]);
            Assert.Equal(TestString(110, "Data"), (string)eCustomTrackingRecordWarning["Data"]);
            Assert.Equal(TestString(110, "Annotations"), (string)eCustomTrackingRecordWarning["Annotations"]);
            Assert.Equal(TestString(110, "ProfileName"), (string)eCustomTrackingRecordWarning["ProfileName"]);
            Assert.Equal(TestString(110, "HostReference"), (string)eCustomTrackingRecordWarning["HostReference"]);
            Assert.Equal(TestString(110, "AppDomain"), (string)eCustomTrackingRecordWarning["AppDomain"]);

            // CustomTrackingRecordError (Multidata7TemplateHA)
            Assert.True(firedEvents.ContainsKey("CustomTrackingRecordError"), "Event CustomTrackingRecordError did not fire");
            var eCustomTrackingRecordError = firedEvents["CustomTrackingRecordError"];
            Assert.Equal(TestGuid(111, 0), (Guid)eCustomTrackingRecordError["InstanceId"]);
            Assert.Equal(TestInt64(111, 1), (long)eCustomTrackingRecordError["RecordNumber"]);
            Assert.Equal(TestString(111, "Name"), (string)eCustomTrackingRecordError["Name"]);
            Assert.Equal(TestString(111, "ActivityName"), (string)eCustomTrackingRecordError["ActivityName"]);
            Assert.Equal(TestString(111, "ActivityId"), (string)eCustomTrackingRecordError["ActivityId"]);
            Assert.Equal(TestString(111, "ActivityInstanceId"), (string)eCustomTrackingRecordError["ActivityInstanceId"]);
            Assert.Equal(TestString(111, "ActivityTypeName"), (string)eCustomTrackingRecordError["ActivityTypeName"]);
            Assert.Equal(TestString(111, "Data"), (string)eCustomTrackingRecordError["Data"]);
            Assert.Equal(TestString(111, "Annotations"), (string)eCustomTrackingRecordError["Annotations"]);
            Assert.Equal(TestString(111, "ProfileName"), (string)eCustomTrackingRecordError["ProfileName"]);
            Assert.Equal(TestString(111, "HostReference"), (string)eCustomTrackingRecordError["HostReference"]);
            Assert.Equal(TestString(111, "AppDomain"), (string)eCustomTrackingRecordError["AppDomain"]);

            // WorkflowInstanceSuspendedRecord (Multidata8TemplateHA)
            Assert.True(firedEvents.ContainsKey("WorkflowInstanceSuspendedRecord"), "Event WorkflowInstanceSuspendedRecord did not fire");
            var eWorkflowInstanceSuspendedRecord = firedEvents["WorkflowInstanceSuspendedRecord"];
            Assert.Equal(TestGuid(112, 0), (Guid)eWorkflowInstanceSuspendedRecord["InstanceId"]);
            Assert.Equal(TestInt64(112, 1), (long)eWorkflowInstanceSuspendedRecord["RecordNumber"]);
            Assert.Equal(TestString(112, "ActivityDefinitionId"), (string)eWorkflowInstanceSuspendedRecord["ActivityDefinitionId"]);
            Assert.Equal(TestString(112, "Reason"), (string)eWorkflowInstanceSuspendedRecord["Reason"]);
            Assert.Equal(TestString(112, "Annotations"), (string)eWorkflowInstanceSuspendedRecord["Annotations"]);
            Assert.Equal(TestString(112, "ProfileName"), (string)eWorkflowInstanceSuspendedRecord["ProfileName"]);
            Assert.Equal(TestString(112, "HostReference"), (string)eWorkflowInstanceSuspendedRecord["HostReference"]);
            Assert.Equal(TestString(112, "AppDomain"), (string)eWorkflowInstanceSuspendedRecord["AppDomain"]);

            // WorkflowInstanceTerminatedRecord (Multidata8TemplateHA)
            Assert.True(firedEvents.ContainsKey("WorkflowInstanceTerminatedRecord"), "Event WorkflowInstanceTerminatedRecord did not fire");
            var eWorkflowInstanceTerminatedRecord = firedEvents["WorkflowInstanceTerminatedRecord"];
            Assert.Equal(TestGuid(113, 0), (Guid)eWorkflowInstanceTerminatedRecord["InstanceId"]);
            Assert.Equal(TestInt64(113, 1), (long)eWorkflowInstanceTerminatedRecord["RecordNumber"]);
            Assert.Equal(TestString(113, "ActivityDefinitionId"), (string)eWorkflowInstanceTerminatedRecord["ActivityDefinitionId"]);
            Assert.Equal(TestString(113, "Reason"), (string)eWorkflowInstanceTerminatedRecord["Reason"]);
            Assert.Equal(TestString(113, "Annotations"), (string)eWorkflowInstanceTerminatedRecord["Annotations"]);
            Assert.Equal(TestString(113, "ProfileName"), (string)eWorkflowInstanceTerminatedRecord["ProfileName"]);
            Assert.Equal(TestString(113, "HostReference"), (string)eWorkflowInstanceTerminatedRecord["HostReference"]);
            Assert.Equal(TestString(113, "AppDomain"), (string)eWorkflowInstanceTerminatedRecord["AppDomain"]);

            // WorkflowInstanceRecordWithId (Multidata11TemplateHA)
            Assert.True(firedEvents.ContainsKey("WorkflowInstanceRecordWithId"), "Event WorkflowInstanceRecordWithId did not fire");
            var eWorkflowInstanceRecordWithId = firedEvents["WorkflowInstanceRecordWithId"];
            Assert.Equal(TestGuid(114, 0), (Guid)eWorkflowInstanceRecordWithId["InstanceId"]);
            Assert.Equal(TestInt64(114, 1), (long)eWorkflowInstanceRecordWithId["RecordNumber"]);
            Assert.Equal(TestString(114, "ActivityDefinitionId"), (string)eWorkflowInstanceRecordWithId["ActivityDefinitionId"]);
            Assert.Equal(TestString(114, "State"), (string)eWorkflowInstanceRecordWithId["State"]);
            Assert.Equal(TestString(114, "Annotations"), (string)eWorkflowInstanceRecordWithId["Annotations"]);
            Assert.Equal(TestString(114, "ProfileName"), (string)eWorkflowInstanceRecordWithId["ProfileName"]);
            Assert.Equal(TestString(114, "WorkflowDefinitionIdentity"), (string)eWorkflowInstanceRecordWithId["WorkflowDefinitionIdentity"]);
            Assert.Equal(TestString(114, "HostReference"), (string)eWorkflowInstanceRecordWithId["HostReference"]);
            Assert.Equal(TestString(114, "AppDomain"), (string)eWorkflowInstanceRecordWithId["AppDomain"]);

            // WorkflowInstanceAbortedRecordWithId (Multidata12TemplateHA)
            Assert.True(firedEvents.ContainsKey("WorkflowInstanceAbortedRecordWithId"), "Event WorkflowInstanceAbortedRecordWithId did not fire");
            var eWorkflowInstanceAbortedRecordWithId = firedEvents["WorkflowInstanceAbortedRecordWithId"];
            Assert.Equal(TestGuid(115, 0), (Guid)eWorkflowInstanceAbortedRecordWithId["InstanceId"]);
            Assert.Equal(TestInt64(115, 1), (long)eWorkflowInstanceAbortedRecordWithId["RecordNumber"]);
            Assert.Equal(TestString(115, "ActivityDefinitionId"), (string)eWorkflowInstanceAbortedRecordWithId["ActivityDefinitionId"]);
            Assert.Equal(TestString(115, "Reason"), (string)eWorkflowInstanceAbortedRecordWithId["Reason"]);
            Assert.Equal(TestString(115, "Annotations"), (string)eWorkflowInstanceAbortedRecordWithId["Annotations"]);
            Assert.Equal(TestString(115, "ProfileName"), (string)eWorkflowInstanceAbortedRecordWithId["ProfileName"]);
            Assert.Equal(TestString(115, "WorkflowDefinitionIdentity"), (string)eWorkflowInstanceAbortedRecordWithId["WorkflowDefinitionIdentity"]);
            Assert.Equal(TestString(115, "HostReference"), (string)eWorkflowInstanceAbortedRecordWithId["HostReference"]);
            Assert.Equal(TestString(115, "AppDomain"), (string)eWorkflowInstanceAbortedRecordWithId["AppDomain"]);

            // WorkflowInstanceSuspendedRecordWithId (Multidata12TemplateHA)
            Assert.True(firedEvents.ContainsKey("WorkflowInstanceSuspendedRecordWithId"), "Event WorkflowInstanceSuspendedRecordWithId did not fire");
            var eWorkflowInstanceSuspendedRecordWithId = firedEvents["WorkflowInstanceSuspendedRecordWithId"];
            Assert.Equal(TestGuid(116, 0), (Guid)eWorkflowInstanceSuspendedRecordWithId["InstanceId"]);
            Assert.Equal(TestInt64(116, 1), (long)eWorkflowInstanceSuspendedRecordWithId["RecordNumber"]);
            Assert.Equal(TestString(116, "ActivityDefinitionId"), (string)eWorkflowInstanceSuspendedRecordWithId["ActivityDefinitionId"]);
            Assert.Equal(TestString(116, "Reason"), (string)eWorkflowInstanceSuspendedRecordWithId["Reason"]);
            Assert.Equal(TestString(116, "Annotations"), (string)eWorkflowInstanceSuspendedRecordWithId["Annotations"]);
            Assert.Equal(TestString(116, "ProfileName"), (string)eWorkflowInstanceSuspendedRecordWithId["ProfileName"]);
            Assert.Equal(TestString(116, "WorkflowDefinitionIdentity"), (string)eWorkflowInstanceSuspendedRecordWithId["WorkflowDefinitionIdentity"]);
            Assert.Equal(TestString(116, "HostReference"), (string)eWorkflowInstanceSuspendedRecordWithId["HostReference"]);
            Assert.Equal(TestString(116, "AppDomain"), (string)eWorkflowInstanceSuspendedRecordWithId["AppDomain"]);

            // WorkflowInstanceTerminatedRecordWithId (Multidata12TemplateHA)
            Assert.True(firedEvents.ContainsKey("WorkflowInstanceTerminatedRecordWithId"), "Event WorkflowInstanceTerminatedRecordWithId did not fire");
            var eWorkflowInstanceTerminatedRecordWithId = firedEvents["WorkflowInstanceTerminatedRecordWithId"];
            Assert.Equal(TestGuid(117, 0), (Guid)eWorkflowInstanceTerminatedRecordWithId["InstanceId"]);
            Assert.Equal(TestInt64(117, 1), (long)eWorkflowInstanceTerminatedRecordWithId["RecordNumber"]);
            Assert.Equal(TestString(117, "ActivityDefinitionId"), (string)eWorkflowInstanceTerminatedRecordWithId["ActivityDefinitionId"]);
            Assert.Equal(TestString(117, "Reason"), (string)eWorkflowInstanceTerminatedRecordWithId["Reason"]);
            Assert.Equal(TestString(117, "Annotations"), (string)eWorkflowInstanceTerminatedRecordWithId["Annotations"]);
            Assert.Equal(TestString(117, "ProfileName"), (string)eWorkflowInstanceTerminatedRecordWithId["ProfileName"]);
            Assert.Equal(TestString(117, "WorkflowDefinitionIdentity"), (string)eWorkflowInstanceTerminatedRecordWithId["WorkflowDefinitionIdentity"]);
            Assert.Equal(TestString(117, "HostReference"), (string)eWorkflowInstanceTerminatedRecordWithId["HostReference"]);
            Assert.Equal(TestString(117, "AppDomain"), (string)eWorkflowInstanceTerminatedRecordWithId["AppDomain"]);

            // WorkflowInstanceUnhandledExceptionRecordWithId (Multidata13TemplateHA)
            Assert.True(firedEvents.ContainsKey("WorkflowInstanceUnhandledExceptionRecordWithId"), "Event WorkflowInstanceUnhandledExceptionRecordWithId did not fire");
            var eWorkflowInstanceUnhandledExceptionRecordWithId = firedEvents["WorkflowInstanceUnhandledExceptionRecordWithId"];
            Assert.Equal(TestGuid(118, 0), (Guid)eWorkflowInstanceUnhandledExceptionRecordWithId["InstanceId"]);
            Assert.Equal(TestInt64(118, 1), (long)eWorkflowInstanceUnhandledExceptionRecordWithId["RecordNumber"]);
            Assert.Equal(TestString(118, "ActivityDefinitionId"), (string)eWorkflowInstanceUnhandledExceptionRecordWithId["ActivityDefinitionId"]);
            Assert.Equal(TestString(118, "SourceName"), (string)eWorkflowInstanceUnhandledExceptionRecordWithId["SourceName"]);
            Assert.Equal(TestString(118, "SourceId"), (string)eWorkflowInstanceUnhandledExceptionRecordWithId["SourceId"]);
            Assert.Equal(TestString(118, "SourceInstanceId"), (string)eWorkflowInstanceUnhandledExceptionRecordWithId["SourceInstanceId"]);
            Assert.Equal(TestString(118, "SourceTypeName"), (string)eWorkflowInstanceUnhandledExceptionRecordWithId["SourceTypeName"]);
            Assert.Equal(TestString(118, "Exception"), (string)eWorkflowInstanceUnhandledExceptionRecordWithId["Exception"]);
            Assert.Equal(TestString(118, "Annotations"), (string)eWorkflowInstanceUnhandledExceptionRecordWithId["Annotations"]);
            Assert.Equal(TestString(118, "ProfileName"), (string)eWorkflowInstanceUnhandledExceptionRecordWithId["ProfileName"]);
            Assert.Equal(TestString(118, "WorkflowDefinitionIdentity"), (string)eWorkflowInstanceUnhandledExceptionRecordWithId["WorkflowDefinitionIdentity"]);
            Assert.Equal(TestString(118, "HostReference"), (string)eWorkflowInstanceUnhandledExceptionRecordWithId["HostReference"]);
            Assert.Equal(TestString(118, "AppDomain"), (string)eWorkflowInstanceUnhandledExceptionRecordWithId["AppDomain"]);

            // WorkflowInstanceUpdatedRecord (Multidata14TemplateHA)
            Assert.True(firedEvents.ContainsKey("WorkflowInstanceUpdatedRecord"), "Event WorkflowInstanceUpdatedRecord did not fire");
            var eWorkflowInstanceUpdatedRecord = firedEvents["WorkflowInstanceUpdatedRecord"];
            Assert.Equal(TestGuid(119, 0), (Guid)eWorkflowInstanceUpdatedRecord["InstanceId"]);
            Assert.Equal(TestInt64(119, 1), (long)eWorkflowInstanceUpdatedRecord["RecordNumber"]);
            Assert.Equal(TestString(119, "ActivityDefinitionId"), (string)eWorkflowInstanceUpdatedRecord["ActivityDefinitionId"]);
            Assert.Equal(TestString(119, "State"), (string)eWorkflowInstanceUpdatedRecord["State"]);
            Assert.Equal(TestString(119, "OriginalDefinitionIdentity"), (string)eWorkflowInstanceUpdatedRecord["OriginalDefinitionIdentity"]);
            Assert.Equal(TestString(119, "UpdatedDefinitionIdentity"), (string)eWorkflowInstanceUpdatedRecord["UpdatedDefinitionIdentity"]);
            Assert.Equal(TestString(119, "Annotations"), (string)eWorkflowInstanceUpdatedRecord["Annotations"]);
            Assert.Equal(TestString(119, "ProfileName"), (string)eWorkflowInstanceUpdatedRecord["ProfileName"]);
            Assert.Equal(TestString(119, "HostReference"), (string)eWorkflowInstanceUpdatedRecord["HostReference"]);
            Assert.Equal(TestString(119, "AppDomain"), (string)eWorkflowInstanceUpdatedRecord["AppDomain"]);

            // BufferPoolAllocation (Multidata1TemplateA)
            Assert.True(firedEvents.ContainsKey("BufferPoolAllocation"), "Event BufferPoolAllocation did not fire");
            var eBufferPoolAllocation = firedEvents["BufferPoolAllocation"];
            Assert.Equal(TestInt32(131, 0), (int)eBufferPoolAllocation["Size"]);
            Assert.Equal(TestString(131, "AppDomain"), (string)eBufferPoolAllocation["AppDomain"]);

            // BufferPoolChangeQuota (Multidata2TemplateA)
            Assert.True(firedEvents.ContainsKey("BufferPoolChangeQuota"), "Event BufferPoolChangeQuota did not fire");
            var eBufferPoolChangeQuota = firedEvents["BufferPoolChangeQuota"];
            Assert.Equal(TestInt32(132, 0), (int)eBufferPoolChangeQuota["PoolSize"]);
            Assert.Equal(TestInt32(132, 1), (int)eBufferPoolChangeQuota["Delta"]);
            Assert.Equal(TestString(132, "AppDomain"), (string)eBufferPoolChangeQuota["AppDomain"]);

            // ActionItemScheduled (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("ActionItemScheduled"), "Event ActionItemScheduled did not fire");
            var eActionItemScheduled = firedEvents["ActionItemScheduled"];
            Assert.Equal(TestString(133, "AppDomain"), (string)eActionItemScheduled["AppDomain"]);

            // ActionItemCallbackInvoked (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("ActionItemCallbackInvoked"), "Event ActionItemCallbackInvoked did not fire");
            var eActionItemCallbackInvoked = firedEvents["ActionItemCallbackInvoked"];
            Assert.Equal(TestString(134, "AppDomain"), (string)eActionItemCallbackInvoked["AppDomain"]);

            // ClientMessageInspectorAfterReceiveInvoked (Multidata23TemplateHA)
            Assert.True(firedEvents.ContainsKey("ClientMessageInspectorAfterReceiveInvoked"), "Event ClientMessageInspectorAfterReceiveInvoked did not fire");
            var eClientMessageInspectorAfterReceiveInvoked = firedEvents["ClientMessageInspectorAfterReceiveInvoked"];
            Assert.Equal(TestString(201, "TypeName"), (string)eClientMessageInspectorAfterReceiveInvoked["TypeName"]);
            Assert.Equal(TestString(201, "HostReference"), (string)eClientMessageInspectorAfterReceiveInvoked["HostReference"]);
            Assert.Equal(TestString(201, "AppDomain"), (string)eClientMessageInspectorAfterReceiveInvoked["AppDomain"]);

            // ClientMessageInspectorBeforeSendInvoked (Multidata23TemplateHA)
            Assert.True(firedEvents.ContainsKey("ClientMessageInspectorBeforeSendInvoked"), "Event ClientMessageInspectorBeforeSendInvoked did not fire");
            var eClientMessageInspectorBeforeSendInvoked = firedEvents["ClientMessageInspectorBeforeSendInvoked"];
            Assert.Equal(TestString(202, "TypeName"), (string)eClientMessageInspectorBeforeSendInvoked["TypeName"]);
            Assert.Equal(TestString(202, "HostReference"), (string)eClientMessageInspectorBeforeSendInvoked["HostReference"]);
            Assert.Equal(TestString(202, "AppDomain"), (string)eClientMessageInspectorBeforeSendInvoked["AppDomain"]);

            // ClientParameterInspectorAfterCallInvoked (Multidata23TemplateHA)
            Assert.True(firedEvents.ContainsKey("ClientParameterInspectorAfterCallInvoked"), "Event ClientParameterInspectorAfterCallInvoked did not fire");
            var eClientParameterInspectorAfterCallInvoked = firedEvents["ClientParameterInspectorAfterCallInvoked"];
            Assert.Equal(TestString(203, "TypeName"), (string)eClientParameterInspectorAfterCallInvoked["TypeName"]);
            Assert.Equal(TestString(203, "HostReference"), (string)eClientParameterInspectorAfterCallInvoked["HostReference"]);
            Assert.Equal(TestString(203, "AppDomain"), (string)eClientParameterInspectorAfterCallInvoked["AppDomain"]);

            // ClientParameterInspectorBeforeCallInvoked (Multidata23TemplateHA)
            Assert.True(firedEvents.ContainsKey("ClientParameterInspectorBeforeCallInvoked"), "Event ClientParameterInspectorBeforeCallInvoked did not fire");
            var eClientParameterInspectorBeforeCallInvoked = firedEvents["ClientParameterInspectorBeforeCallInvoked"];
            Assert.Equal(TestString(204, "TypeName"), (string)eClientParameterInspectorBeforeCallInvoked["TypeName"]);
            Assert.Equal(TestString(204, "HostReference"), (string)eClientParameterInspectorBeforeCallInvoked["HostReference"]);
            Assert.Equal(TestString(204, "AppDomain"), (string)eClientParameterInspectorBeforeCallInvoked["AppDomain"]);

            // OperationInvoked (Multidata24TemplateHA)
            Assert.True(firedEvents.ContainsKey("OperationInvoked"), "Event OperationInvoked did not fire");
            var eOperationInvoked = firedEvents["OperationInvoked"];
            Assert.Equal(TestString(205, "MethodName"), (string)eOperationInvoked["MethodName"]);
            Assert.Equal(TestString(205, "CallerInfo"), (string)eOperationInvoked["CallerInfo"]);
            Assert.Equal(TestString(205, "HostReference"), (string)eOperationInvoked["HostReference"]);
            Assert.Equal(TestString(205, "AppDomain"), (string)eOperationInvoked["AppDomain"]);

            // ErrorHandlerInvoked (Multidata25TemplateHA)
            Assert.True(firedEvents.ContainsKey("ErrorHandlerInvoked"), "Event ErrorHandlerInvoked did not fire");
            var eErrorHandlerInvoked = firedEvents["ErrorHandlerInvoked"];
            Assert.Equal(TestString(206, "TypeName"), (string)eErrorHandlerInvoked["TypeName"]);
            Assert.Equal((int)TestByte(206, 1), (int)eErrorHandlerInvoked["Handled"]);
            Assert.Equal(TestString(206, "ExceptionTypeName"), (string)eErrorHandlerInvoked["ExceptionTypeName"]);
            Assert.Equal(TestString(206, "HostReference"), (string)eErrorHandlerInvoked["HostReference"]);
            Assert.Equal(TestString(206, "AppDomain"), (string)eErrorHandlerInvoked["AppDomain"]);

            // FaultProviderInvoked (Multidata26TemplateHA)
            Assert.True(firedEvents.ContainsKey("FaultProviderInvoked"), "Event FaultProviderInvoked did not fire");
            var eFaultProviderInvoked = firedEvents["FaultProviderInvoked"];
            Assert.Equal(TestString(207, "TypeName"), (string)eFaultProviderInvoked["TypeName"]);
            Assert.Equal(TestString(207, "ExceptionTypeName"), (string)eFaultProviderInvoked["ExceptionTypeName"]);
            Assert.Equal(TestString(207, "HostReference"), (string)eFaultProviderInvoked["HostReference"]);
            Assert.Equal(TestString(207, "AppDomain"), (string)eFaultProviderInvoked["AppDomain"]);

            // MessageInspectorAfterReceiveInvoked (Multidata23TemplateHA)
            Assert.True(firedEvents.ContainsKey("MessageInspectorAfterReceiveInvoked"), "Event MessageInspectorAfterReceiveInvoked did not fire");
            var eMessageInspectorAfterReceiveInvoked = firedEvents["MessageInspectorAfterReceiveInvoked"];
            Assert.Equal(TestString(208, "TypeName"), (string)eMessageInspectorAfterReceiveInvoked["TypeName"]);
            Assert.Equal(TestString(208, "HostReference"), (string)eMessageInspectorAfterReceiveInvoked["HostReference"]);
            Assert.Equal(TestString(208, "AppDomain"), (string)eMessageInspectorAfterReceiveInvoked["AppDomain"]);

            // MessageInspectorBeforeSendInvoked (Multidata23TemplateHA)
            Assert.True(firedEvents.ContainsKey("MessageInspectorBeforeSendInvoked"), "Event MessageInspectorBeforeSendInvoked did not fire");
            var eMessageInspectorBeforeSendInvoked = firedEvents["MessageInspectorBeforeSendInvoked"];
            Assert.Equal(TestString(209, "TypeName"), (string)eMessageInspectorBeforeSendInvoked["TypeName"]);
            Assert.Equal(TestString(209, "HostReference"), (string)eMessageInspectorBeforeSendInvoked["HostReference"]);
            Assert.Equal(TestString(209, "AppDomain"), (string)eMessageInspectorBeforeSendInvoked["AppDomain"]);

            // MessageThrottleExceeded (Multidata27TemplateHA)
            Assert.True(firedEvents.ContainsKey("MessageThrottleExceeded"), "Event MessageThrottleExceeded did not fire");
            var eMessageThrottleExceeded = firedEvents["MessageThrottleExceeded"];
            Assert.Equal(TestString(210, "ThrottleName"), (string)eMessageThrottleExceeded["ThrottleName"]);
            Assert.Equal(TestInt64(210, 1), (long)eMessageThrottleExceeded["Limit"]);
            Assert.Equal(TestString(210, "HostReference"), (string)eMessageThrottleExceeded["HostReference"]);
            Assert.Equal(TestString(210, "AppDomain"), (string)eMessageThrottleExceeded["AppDomain"]);

            // ParameterInspectorAfterCallInvoked (Multidata23TemplateHA)
            Assert.True(firedEvents.ContainsKey("ParameterInspectorAfterCallInvoked"), "Event ParameterInspectorAfterCallInvoked did not fire");
            var eParameterInspectorAfterCallInvoked = firedEvents["ParameterInspectorAfterCallInvoked"];
            Assert.Equal(TestString(211, "TypeName"), (string)eParameterInspectorAfterCallInvoked["TypeName"]);
            Assert.Equal(TestString(211, "HostReference"), (string)eParameterInspectorAfterCallInvoked["HostReference"]);
            Assert.Equal(TestString(211, "AppDomain"), (string)eParameterInspectorAfterCallInvoked["AppDomain"]);

            // ParameterInspectorBeforeCallInvoked (Multidata23TemplateHA)
            Assert.True(firedEvents.ContainsKey("ParameterInspectorBeforeCallInvoked"), "Event ParameterInspectorBeforeCallInvoked did not fire");
            var eParameterInspectorBeforeCallInvoked = firedEvents["ParameterInspectorBeforeCallInvoked"];
            Assert.Equal(TestString(212, "TypeName"), (string)eParameterInspectorBeforeCallInvoked["TypeName"]);
            Assert.Equal(TestString(212, "HostReference"), (string)eParameterInspectorBeforeCallInvoked["HostReference"]);
            Assert.Equal(TestString(212, "AppDomain"), (string)eParameterInspectorBeforeCallInvoked["AppDomain"]);

            // ServiceHostStarted (Multidata72TemplateHA)
            Assert.True(firedEvents.ContainsKey("ServiceHostStarted"), "Event ServiceHostStarted did not fire");
            var eServiceHostStarted = firedEvents["ServiceHostStarted"];
            Assert.Equal(TestString(213, "ServiceTypeName"), (string)eServiceHostStarted["ServiceTypeName"]);
            Assert.Equal(TestString(213, "HostReference"), (string)eServiceHostStarted["HostReference"]);
            Assert.Equal(TestString(213, "AppDomain"), (string)eServiceHostStarted["AppDomain"]);

            // OperationCompleted (Multidata28TemplateHA)
            Assert.True(firedEvents.ContainsKey("OperationCompleted"), "Event OperationCompleted did not fire");
            var eOperationCompleted = firedEvents["OperationCompleted"];
            Assert.Equal(TestString(214, "MethodName"), (string)eOperationCompleted["MethodName"]);
            Assert.Equal(TestInt64(214, 1), (long)eOperationCompleted["Duration"]);
            Assert.Equal(TestString(214, "HostReference"), (string)eOperationCompleted["HostReference"]);
            Assert.Equal(TestString(214, "AppDomain"), (string)eOperationCompleted["AppDomain"]);
        }
    }
}
