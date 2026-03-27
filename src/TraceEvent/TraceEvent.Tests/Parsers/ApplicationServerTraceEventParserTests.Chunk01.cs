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
                // Event 100: WorkflowInstanceRecord (Multidata9TemplateHA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "WorkflowInstanceRecord", 100));
                // Event 101: WorkflowInstanceUnhandledExceptionRecord (Multidata10TemplateHA, opcode=150)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "WorkflowInstanceUnhandledExceptionRecord", 101) { OpCode = 150 });
                // Event 102: WorkflowInstanceAbortedRecord (Multidata8TemplateHA, opcode=144)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "WorkflowInstanceAbortedRecord", 102) { OpCode = 144 });
                // Event 103: ActivityStateRecord (Multidata4TemplateHA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ActivityStateRecord", 103));
                // Event 104: ActivityScheduledRecord (Multidata3TemplateHA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ActivityScheduledRecord", 104));
                // Event 105: FaultPropagationRecord (Multidata6TemplateHA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "FaultPropagationRecord", 105));
                // Event 106: CancelRequestedRecord (Multidata3TemplateHA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "CancelRequestedRecord", 106));
                // Event 107: BookmarkResumptionRecord (Multidata5TemplateHA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "BookmarkResumptionRecord", 107));
                // Event 108: CustomTrackingRecordInfo (Multidata7TemplateHA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "CustomTrackingRecordInfo", 108));
                // Event 110: CustomTrackingRecordWarning (Multidata7TemplateHA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "CustomTrackingRecordWarning", 110));
                // Event 111: CustomTrackingRecordError (Multidata7TemplateHA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "CustomTrackingRecordError", 111));
                // Event 112: WorkflowInstanceSuspendedRecord (Multidata8TemplateHA, opcode=146)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "WorkflowInstanceSuspendedRecord", 112) { OpCode = 146 });
                // Event 113: WorkflowInstanceTerminatedRecord (Multidata8TemplateHA, opcode=148)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "WorkflowInstanceTerminatedRecord", 113) { OpCode = 148 });
                // Event 114: WorkflowInstanceRecordWithId (Multidata11TemplateHA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "WorkflowInstanceRecordWithId", 114));
                // Event 115: WorkflowInstanceAbortedRecordWithId (Multidata12TemplateHA, opcode=145)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "WorkflowInstanceAbortedRecordWithId", 115) { OpCode = 145 });
                // Event 116: WorkflowInstanceSuspendedRecordWithId (Multidata12TemplateHA, opcode=147)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "WorkflowInstanceSuspendedRecordWithId", 116) { OpCode = 147 });
                // Event 117: WorkflowInstanceTerminatedRecordWithId (Multidata12TemplateHA, opcode=149)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "WorkflowInstanceTerminatedRecordWithId", 117) { OpCode = 149 });
                // Event 118: WorkflowInstanceUnhandledExceptionRecordWithId (Multidata13TemplateHA, opcode=151)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "WorkflowInstanceUnhandledExceptionRecordWithId", 118) { OpCode = 151 });
                // Event 119: WorkflowInstanceUpdatedRecord (Multidata14TemplateHA, opcode=152)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "WorkflowInstanceUpdatedRecord", 119) { OpCode = 152 });
                // Event 131: BufferPoolAllocation (Multidata1TemplateA, opcode=12)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "BufferPoolAllocation", 131) { OpCode = 12 });
                // Event 132: BufferPoolChangeQuota (Multidata2TemplateA, opcode=13)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "BufferPoolChangeQuota", 132) { OpCode = 13 });
                // Event 133: ActionItemScheduled (OneStringsTemplateA, opcode=1)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ActionItemScheduled", 133) { OpCode = 1 });
                // Event 134: ActionItemCallbackInvoked (OneStringsTemplateA, opcode=2)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ActionItemCallbackInvoked", 134) { OpCode = 2 });
                // Event 201: ClientMessageInspectorAfterReceiveInvoked (Multidata23TemplateHA, opcode=16)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ClientMessageInspectorAfterReceiveInvoked", 201) { OpCode = 16 });
                // Event 202: ClientMessageInspectorBeforeSendInvoked (Multidata23TemplateHA, opcode=17)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ClientMessageInspectorBeforeSendInvoked", 202) { OpCode = 17 });
                // Event 203: ClientParameterInspectorAfterCallInvoked (Multidata23TemplateHA, opcode=19)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ClientParameterInspectorAfterCallInvoked", 203) { OpCode = 19 });
                // Event 204: ClientParameterInspectorBeforeCallInvoked (Multidata23TemplateHA, opcode=18)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ClientParameterInspectorBeforeCallInvoked", 204) { OpCode = 18 });
                // Event 205: OperationInvoked (Multidata24TemplateHA, opcode=53)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "OperationInvoked", 205) { OpCode = 53 });
                // Event 206: ErrorHandlerInvoked (Multidata25TemplateHA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ErrorHandlerInvoked", 206));
                // Event 207: FaultProviderInvoked (Multidata26TemplateHA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "FaultProviderInvoked", 207));
                // Event 208: MessageInspectorAfterReceiveInvoked (Multidata23TemplateHA, opcode=51)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "MessageInspectorAfterReceiveInvoked", 208) { OpCode = 51 });
                // Event 209: MessageInspectorBeforeSendInvoked (Multidata23TemplateHA, opcode=52)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "MessageInspectorBeforeSendInvoked", 209) { OpCode = 52 });
                // Event 210: MessageThrottleExceeded (Multidata27TemplateHA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "MessageThrottleExceeded", 210));
                // Event 211: ParameterInspectorAfterCallInvoked (Multidata23TemplateHA, opcode=56)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ParameterInspectorAfterCallInvoked", 211) { OpCode = 56 });
                // Event 212: ParameterInspectorBeforeCallInvoked (Multidata23TemplateHA, opcode=55)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ParameterInspectorBeforeCallInvoked", 212) { OpCode = 55 });
                // Event 213: ServiceHostStarted (Multidata72TemplateHA, opcode=1)
                w.WriteMetadataEventBlobV5OrLess(new EventMetadata(__metadataId++, ProviderName, "ServiceHostStarted", 213) { OpCode = 1 });
                // Event 214: OperationCompleted (Multidata28TemplateHA, opcode=54)
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
                // Event 100: WorkflowInstanceRecord (Multidata9TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(100, Multidata9TemplateHA_Fields));
                // Event 101: WorkflowInstanceUnhandledExceptionRecord (Multidata10TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(101, Multidata10TemplateHA_Fields));
                // Event 102: WorkflowInstanceAbortedRecord (Multidata8TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(102, Multidata8TemplateHA_Fields));
                // Event 103: ActivityStateRecord (Multidata4TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(103, Multidata4TemplateHA_Fields));
                // Event 104: ActivityScheduledRecord (Multidata3TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(104, Multidata3TemplateHA_Fields));
                // Event 105: FaultPropagationRecord (Multidata6TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(105, Multidata6TemplateHA_Fields));
                // Event 106: CancelRequestedRecord (Multidata3TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(106, Multidata3TemplateHA_Fields));
                // Event 107: BookmarkResumptionRecord (Multidata5TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(107, Multidata5TemplateHA_Fields));
                // Event 108: CustomTrackingRecordInfo (Multidata7TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(108, Multidata7TemplateHA_Fields));
                // Event 110: CustomTrackingRecordWarning (Multidata7TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(110, Multidata7TemplateHA_Fields));
                // Event 111: CustomTrackingRecordError (Multidata7TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(111, Multidata7TemplateHA_Fields));
                // Event 112: WorkflowInstanceSuspendedRecord (Multidata8TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(112, Multidata8TemplateHA_Fields));
                // Event 113: WorkflowInstanceTerminatedRecord (Multidata8TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(113, Multidata8TemplateHA_Fields));
                // Event 114: WorkflowInstanceRecordWithId (Multidata11TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(114, Multidata11TemplateHA_Fields));
                // Event 115: WorkflowInstanceAbortedRecordWithId (Multidata12TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(115, Multidata12TemplateHA_Fields));
                // Event 116: WorkflowInstanceSuspendedRecordWithId (Multidata12TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(116, Multidata12TemplateHA_Fields));
                // Event 117: WorkflowInstanceTerminatedRecordWithId (Multidata12TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(117, Multidata12TemplateHA_Fields));
                // Event 118: WorkflowInstanceUnhandledExceptionRecordWithId (Multidata13TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(118, Multidata13TemplateHA_Fields));
                // Event 119: WorkflowInstanceUpdatedRecord (Multidata14TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(119, Multidata14TemplateHA_Fields));
                // Event 131: BufferPoolAllocation (Multidata1TemplateA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(131, Multidata1TemplateA_Fields));
                // Event 132: BufferPoolChangeQuota (Multidata2TemplateA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(132, Multidata2TemplateA_Fields));
                // Event 133: ActionItemScheduled (OneStringsTemplateA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(133, OneStringsTemplateA_Fields));
                // Event 134: ActionItemCallbackInvoked (OneStringsTemplateA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(134, OneStringsTemplateA_Fields));
                // Event 201: ClientMessageInspectorAfterReceiveInvoked (Multidata23TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(201, Multidata23TemplateHA_Fields));
                // Event 202: ClientMessageInspectorBeforeSendInvoked (Multidata23TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(202, Multidata23TemplateHA_Fields));
                // Event 203: ClientParameterInspectorAfterCallInvoked (Multidata23TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(203, Multidata23TemplateHA_Fields));
                // Event 204: ClientParameterInspectorBeforeCallInvoked (Multidata23TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(204, Multidata23TemplateHA_Fields));
                // Event 205: OperationInvoked (Multidata24TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(205, Multidata24TemplateHA_Fields));
                // Event 206: ErrorHandlerInvoked (Multidata25TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(206, Multidata25TemplateHA_Fields));
                // Event 207: FaultProviderInvoked (Multidata26TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(207, Multidata26TemplateHA_Fields));
                // Event 208: MessageInspectorAfterReceiveInvoked (Multidata23TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(208, Multidata23TemplateHA_Fields));
                // Event 209: MessageInspectorBeforeSendInvoked (Multidata23TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(209, Multidata23TemplateHA_Fields));
                // Event 210: MessageThrottleExceeded (Multidata27TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(210, Multidata27TemplateHA_Fields));
                // Event 211: ParameterInspectorAfterCallInvoked (Multidata23TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(211, Multidata23TemplateHA_Fields));
                // Event 212: ParameterInspectorBeforeCallInvoked (Multidata23TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(212, Multidata23TemplateHA_Fields));
                // Event 213: ServiceHostStarted (Multidata72TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(213, Multidata72TemplateHA_Fields));
                // Event 214: OperationCompleted (Multidata28TemplateHA)
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(214, Multidata28TemplateHA_Fields));
            });
        
            metadataId = __metadataId;
            sequenceNumber = __sequenceNumber;
        }

        /// <summary>
        /// Subscribes to chunk 01 events and records payload values.
        /// </summary>
        private void Subscribe_Chunk01(ApplicationServerTraceEventParser parser, Dictionary<int, Dictionary<string, object>> firedEvents)
        {
            // Event 100: WorkflowInstanceRecord -> Multidata9TemplateHATraceData
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
                firedEvents[100] = fields;
            };

            // Event 101: WorkflowInstanceUnhandledExceptionRecord -> Multidata10TemplateHATraceData
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
                firedEvents[101] = fields;
            };

            // Event 102: WorkflowInstanceAbortedRecord -> Multidata8TemplateHATraceData
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
                firedEvents[102] = fields;
            };

            // Event 103: ActivityStateRecord -> Multidata4TemplateHATraceData
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
                firedEvents[103] = fields;
            };

            // Event 104: ActivityScheduledRecord -> Multidata3TemplateHATraceData
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
                firedEvents[104] = fields;
            };

            // Event 105: FaultPropagationRecord -> Multidata6TemplateHATraceData
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
                firedEvents[105] = fields;
            };

            // Event 106: CancelRequestedRecord -> Multidata3TemplateHATraceData
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
                firedEvents[106] = fields;
            };

            // Event 107: BookmarkResumptionRecord -> Multidata5TemplateHATraceData
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
                firedEvents[107] = fields;
            };

            // Event 108: CustomTrackingRecordInfo -> Multidata7TemplateHATraceData
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
                firedEvents[108] = fields;
            };

            // Event 110: CustomTrackingRecordWarning -> Multidata7TemplateHATraceData
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
                firedEvents[110] = fields;
            };

            // Event 111: CustomTrackingRecordError -> Multidata7TemplateHATraceData
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
                firedEvents[111] = fields;
            };

            // Event 112: WorkflowInstanceSuspendedRecord -> Multidata8TemplateHATraceData
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
                firedEvents[112] = fields;
            };

            // Event 113: WorkflowInstanceTerminatedRecord -> Multidata8TemplateHATraceData
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
                firedEvents[113] = fields;
            };

            // Event 114: WorkflowInstanceRecordWithId -> Multidata11TemplateHATraceData
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
                firedEvents[114] = fields;
            };

            // Event 115: WorkflowInstanceAbortedRecordWithId -> Multidata12TemplateHATraceData
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
                firedEvents[115] = fields;
            };

            // Event 116: WorkflowInstanceSuspendedRecordWithId -> Multidata12TemplateHATraceData
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
                firedEvents[116] = fields;
            };

            // Event 117: WorkflowInstanceTerminatedRecordWithId -> Multidata12TemplateHATraceData
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
                firedEvents[117] = fields;
            };

            // Event 118: WorkflowInstanceUnhandledExceptionRecordWithId -> Multidata13TemplateHATraceData
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
                firedEvents[118] = fields;
            };

            // Event 119: WorkflowInstanceUpdatedRecord -> Multidata14TemplateHATraceData
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
                firedEvents[119] = fields;
            };

            // Event 131: BufferPoolAllocation -> Multidata1TemplateATraceData
            parser.BufferPoolAllocation += delegate(Multidata1TemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["Size"] = data.Size;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[131] = fields;
            };

            // Event 132: BufferPoolChangeQuota -> Multidata2TemplateATraceData
            parser.BufferPoolChangeQuota += delegate(Multidata2TemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["PoolSize"] = data.PoolSize;
                fields["Delta"] = data.Delta;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[132] = fields;
            };

            // Event 133: ActionItemScheduled -> OneStringsTemplateATraceData
            parser.ActionItemScheduled += delegate(OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents[133] = fields;
            };

            // Event 134: ActionItemCallbackInvoked -> OneStringsTemplateATraceData
            parser.ActionItemCallbackInvoked += delegate(OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents[134] = fields;
            };

            // Event 201: ClientMessageInspectorAfterReceiveInvoked -> Multidata23TemplateHATraceData
            parser.ClientMessageInspectorAfterReceiveInvoked += delegate(Multidata23TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["TypeName"] = data.TypeName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[201] = fields;
            };

            // Event 202: ClientMessageInspectorBeforeSendInvoked -> Multidata23TemplateHATraceData
            parser.ClientMessageInspectorBeforeSendInvoked += delegate(Multidata23TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["TypeName"] = data.TypeName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[202] = fields;
            };

            // Event 203: ClientParameterInspectorAfterCallInvoked -> Multidata23TemplateHATraceData
            parser.ClientParameterInspectorAfterCallInvoked += delegate(Multidata23TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["TypeName"] = data.TypeName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[203] = fields;
            };

            // Event 204: ClientParameterInspectorBeforeCallInvoked -> Multidata23TemplateHATraceData
            parser.ClientParameterInspectorBeforeCallInvoked += delegate(Multidata23TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["TypeName"] = data.TypeName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[204] = fields;
            };

            // Event 205: OperationInvoked -> Multidata24TemplateHATraceData
            parser.OperationInvoked += delegate(Multidata24TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["MethodName"] = data.MethodName;
                fields["CallerInfo"] = data.CallerInfo;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[205] = fields;
            };

            // Event 206: ErrorHandlerInvoked -> Multidata25TemplateHATraceData
            parser.ErrorHandlerInvoked += delegate(Multidata25TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["TypeName"] = data.TypeName;
                fields["Handled"] = data.Handled;
                fields["ExceptionTypeName"] = data.ExceptionTypeName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[206] = fields;
            };

            // Event 207: FaultProviderInvoked -> Multidata26TemplateHATraceData
            parser.FaultProviderInvoked += delegate(Multidata26TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["TypeName"] = data.TypeName;
                fields["ExceptionTypeName"] = data.ExceptionTypeName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[207] = fields;
            };

            // Event 208: MessageInspectorAfterReceiveInvoked -> Multidata23TemplateHATraceData
            parser.MessageInspectorAfterReceiveInvoked += delegate(Multidata23TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["TypeName"] = data.TypeName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[208] = fields;
            };

            // Event 209: MessageInspectorBeforeSendInvoked -> Multidata23TemplateHATraceData
            parser.MessageInspectorBeforeSendInvoked += delegate(Multidata23TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["TypeName"] = data.TypeName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[209] = fields;
            };

            // Event 210: MessageThrottleExceeded -> Multidata27TemplateHATraceData
            parser.MessageThrottleExceeded += delegate(Multidata27TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["ThrottleName"] = data.ThrottleName;
                fields["Limit"] = data.Limit;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[210] = fields;
            };

            // Event 211: ParameterInspectorAfterCallInvoked -> Multidata23TemplateHATraceData
            parser.ParameterInspectorAfterCallInvoked += delegate(Multidata23TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["TypeName"] = data.TypeName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[211] = fields;
            };

            // Event 212: ParameterInspectorBeforeCallInvoked -> Multidata23TemplateHATraceData
            parser.ParameterInspectorBeforeCallInvoked += delegate(Multidata23TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["TypeName"] = data.TypeName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[212] = fields;
            };

            // Event 213: ServiceHostStarted -> Multidata72TemplateHATraceData
            parser.ServiceHostStarted += delegate(Multidata72TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["ServiceTypeName"] = data.ServiceTypeName;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[213] = fields;
            };

            // Event 214: OperationCompleted -> Multidata28TemplateHATraceData
            parser.OperationCompleted += delegate(Multidata28TemplateHATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["MethodName"] = data.MethodName;
                fields["Duration"] = data.Duration;
                fields["HostReference"] = data.HostReference;
                fields["AppDomain"] = data.AppDomain;
                firedEvents[214] = fields;
            };
        }

        /// <summary>
        /// Validates chunk 01 event payload values.
        /// </summary>
        private void Validate_Chunk01(Dictionary<int, Dictionary<string, object>> firedEvents)
        {
            // Event 100: WorkflowInstanceRecord (Multidata9TemplateHA)
            Assert.True(firedEvents.ContainsKey(100), "Event 100 (WorkflowInstanceRecord) did not fire");
            var e100 = firedEvents[100];
            Assert.Equal(TestGuid(100, 0), (Guid)e100["InstanceId"]);
            Assert.Equal(TestInt64(100, 1), (long)e100["RecordNumber"]);
            // Field index 2 is EventTime (skipped by parser)
            Assert.Equal(TestString(100, "ActivityDefinitionId"), (string)e100["ActivityDefinitionId"]);
            Assert.Equal(TestString(100, "State"), (string)e100["State"]);
            Assert.Equal(TestString(100, "Annotations"), (string)e100["Annotations"]);
            Assert.Equal(TestString(100, "ProfileName"), (string)e100["ProfileName"]);
            Assert.Equal(TestString(100, "HostReference"), (string)e100["HostReference"]);
            Assert.Equal(TestString(100, "AppDomain"), (string)e100["AppDomain"]);

            // Event 101: WorkflowInstanceUnhandledExceptionRecord (Multidata10TemplateHA)
            Assert.True(firedEvents.ContainsKey(101), "Event 101 (WorkflowInstanceUnhandledExceptionRecord) did not fire");
            var e101 = firedEvents[101];
            Assert.Equal(TestGuid(101, 0), (Guid)e101["InstanceId"]);
            Assert.Equal(TestInt64(101, 1), (long)e101["RecordNumber"]);
            Assert.Equal(TestString(101, "ActivityDefinitionId"), (string)e101["ActivityDefinitionId"]);
            Assert.Equal(TestString(101, "SourceName"), (string)e101["SourceName"]);
            Assert.Equal(TestString(101, "SourceId"), (string)e101["SourceId"]);
            Assert.Equal(TestString(101, "SourceInstanceId"), (string)e101["SourceInstanceId"]);
            Assert.Equal(TestString(101, "SourceTypeName"), (string)e101["SourceTypeName"]);
            Assert.Equal(TestString(101, "Exception"), (string)e101["Exception"]);
            Assert.Equal(TestString(101, "Annotations"), (string)e101["Annotations"]);
            Assert.Equal(TestString(101, "ProfileName"), (string)e101["ProfileName"]);
            Assert.Equal(TestString(101, "HostReference"), (string)e101["HostReference"]);
            Assert.Equal(TestString(101, "AppDomain"), (string)e101["AppDomain"]);

            // Event 102: WorkflowInstanceAbortedRecord (Multidata8TemplateHA)
            Assert.True(firedEvents.ContainsKey(102), "Event 102 (WorkflowInstanceAbortedRecord) did not fire");
            var e102 = firedEvents[102];
            Assert.Equal(TestGuid(102, 0), (Guid)e102["InstanceId"]);
            Assert.Equal(TestInt64(102, 1), (long)e102["RecordNumber"]);
            Assert.Equal(TestString(102, "ActivityDefinitionId"), (string)e102["ActivityDefinitionId"]);
            Assert.Equal(TestString(102, "Reason"), (string)e102["Reason"]);
            Assert.Equal(TestString(102, "Annotations"), (string)e102["Annotations"]);
            Assert.Equal(TestString(102, "ProfileName"), (string)e102["ProfileName"]);
            Assert.Equal(TestString(102, "HostReference"), (string)e102["HostReference"]);
            Assert.Equal(TestString(102, "AppDomain"), (string)e102["AppDomain"]);

            // Event 103: ActivityStateRecord (Multidata4TemplateHA)
            Assert.True(firedEvents.ContainsKey(103), "Event 103 (ActivityStateRecord) did not fire");
            var e103 = firedEvents[103];
            Assert.Equal(TestGuid(103, 0), (Guid)e103["InstanceId"]);
            Assert.Equal(TestInt64(103, 1), (long)e103["RecordNumber"]);
            Assert.Equal(TestString(103, "State"), (string)e103["State"]);
            Assert.Equal(TestString(103, "Name"), (string)e103["Name"]);
            Assert.Equal(TestString(103, "ActivityId"), (string)e103["ActivityId"]);
            Assert.Equal(TestString(103, "ActivityInstanceId"), (string)e103["ActivityInstanceId"]);
            Assert.Equal(TestString(103, "ActivityTypeName"), (string)e103["ActivityTypeName"]);
            Assert.Equal(TestString(103, "Arguments"), (string)e103["Arguments"]);
            Assert.Equal(TestString(103, "Variables"), (string)e103["Variables"]);
            Assert.Equal(TestString(103, "Annotations"), (string)e103["Annotations"]);
            Assert.Equal(TestString(103, "ProfileName"), (string)e103["ProfileName"]);
            Assert.Equal(TestString(103, "HostReference"), (string)e103["HostReference"]);
            Assert.Equal(TestString(103, "AppDomain"), (string)e103["AppDomain"]);

            // Event 104: ActivityScheduledRecord (Multidata3TemplateHA)
            Assert.True(firedEvents.ContainsKey(104), "Event 104 (ActivityScheduledRecord) did not fire");
            var e104 = firedEvents[104];
            Assert.Equal(TestGuid(104, 0), (Guid)e104["InstanceId"]);
            Assert.Equal(TestInt64(104, 1), (long)e104["RecordNumber"]);
            Assert.Equal(TestString(104, "Name"), (string)e104["Name"]);
            Assert.Equal(TestString(104, "ActivityId"), (string)e104["ActivityId"]);
            Assert.Equal(TestString(104, "ActivityInstanceId"), (string)e104["ActivityInstanceId"]);
            Assert.Equal(TestString(104, "ActivityTypeName"), (string)e104["ActivityTypeName"]);
            Assert.Equal(TestString(104, "ChildActivityName"), (string)e104["ChildActivityName"]);
            Assert.Equal(TestString(104, "ChildActivityId"), (string)e104["ChildActivityId"]);
            Assert.Equal(TestString(104, "ChildActivityInstanceId"), (string)e104["ChildActivityInstanceId"]);
            Assert.Equal(TestString(104, "ChildActivityTypeName"), (string)e104["ChildActivityTypeName"]);
            Assert.Equal(TestString(104, "Annotations"), (string)e104["Annotations"]);
            Assert.Equal(TestString(104, "ProfileName"), (string)e104["ProfileName"]);
            Assert.Equal(TestString(104, "HostReference"), (string)e104["HostReference"]);
            Assert.Equal(TestString(104, "AppDomain"), (string)e104["AppDomain"]);

            // Event 105: FaultPropagationRecord (Multidata6TemplateHA)
            Assert.True(firedEvents.ContainsKey(105), "Event 105 (FaultPropagationRecord) did not fire");
            var e105 = firedEvents[105];
            Assert.Equal(TestGuid(105, 0), (Guid)e105["InstanceId"]);
            Assert.Equal(TestInt64(105, 1), (long)e105["RecordNumber"]);
            Assert.Equal(TestString(105, "FaultSourceActivityName"), (string)e105["FaultSourceActivityName"]);
            Assert.Equal(TestString(105, "FaultSourceActivityId"), (string)e105["FaultSourceActivityId"]);
            Assert.Equal(TestString(105, "FaultSourceActivityInstanceId"), (string)e105["FaultSourceActivityInstanceId"]);
            Assert.Equal(TestString(105, "FaultSourceActivityTypeName"), (string)e105["FaultSourceActivityTypeName"]);
            Assert.Equal(TestString(105, "FaultHandlerActivityName"), (string)e105["FaultHandlerActivityName"]);
            Assert.Equal(TestString(105, "FaultHandlerActivityId"), (string)e105["FaultHandlerActivityId"]);
            Assert.Equal(TestString(105, "FaultHandlerActivityInstanceId"), (string)e105["FaultHandlerActivityInstanceId"]);
            Assert.Equal(TestString(105, "FaultHandlerActivityTypeName"), (string)e105["FaultHandlerActivityTypeName"]);
            Assert.Equal(TestString(105, "Fault"), (string)e105["Fault"]);
            Assert.Equal((int)TestByte(105, 11), (int)e105["IsFaultSource"]);
            Assert.Equal(TestString(105, "Annotations"), (string)e105["Annotations"]);
            Assert.Equal(TestString(105, "ProfileName"), (string)e105["ProfileName"]);
            Assert.Equal(TestString(105, "HostReference"), (string)e105["HostReference"]);
            Assert.Equal(TestString(105, "AppDomain"), (string)e105["AppDomain"]);

            // Event 106: CancelRequestedRecord (Multidata3TemplateHA)
            Assert.True(firedEvents.ContainsKey(106), "Event 106 (CancelRequestedRecord) did not fire");
            var e106 = firedEvents[106];
            Assert.Equal(TestGuid(106, 0), (Guid)e106["InstanceId"]);
            Assert.Equal(TestInt64(106, 1), (long)e106["RecordNumber"]);
            Assert.Equal(TestString(106, "Name"), (string)e106["Name"]);
            Assert.Equal(TestString(106, "ActivityId"), (string)e106["ActivityId"]);
            Assert.Equal(TestString(106, "ActivityInstanceId"), (string)e106["ActivityInstanceId"]);
            Assert.Equal(TestString(106, "ActivityTypeName"), (string)e106["ActivityTypeName"]);
            Assert.Equal(TestString(106, "ChildActivityName"), (string)e106["ChildActivityName"]);
            Assert.Equal(TestString(106, "ChildActivityId"), (string)e106["ChildActivityId"]);
            Assert.Equal(TestString(106, "ChildActivityInstanceId"), (string)e106["ChildActivityInstanceId"]);
            Assert.Equal(TestString(106, "ChildActivityTypeName"), (string)e106["ChildActivityTypeName"]);
            Assert.Equal(TestString(106, "Annotations"), (string)e106["Annotations"]);
            Assert.Equal(TestString(106, "ProfileName"), (string)e106["ProfileName"]);
            Assert.Equal(TestString(106, "HostReference"), (string)e106["HostReference"]);
            Assert.Equal(TestString(106, "AppDomain"), (string)e106["AppDomain"]);

            // Event 107: BookmarkResumptionRecord (Multidata5TemplateHA)
            Assert.True(firedEvents.ContainsKey(107), "Event 107 (BookmarkResumptionRecord) did not fire");
            var e107 = firedEvents[107];
            Assert.Equal(TestGuid(107, 0), (Guid)e107["InstanceId"]);
            Assert.Equal(TestInt64(107, 1), (long)e107["RecordNumber"]);
            Assert.Equal(TestString(107, "Name"), (string)e107["Name"]);
            Assert.Equal(TestGuid(107, 3), (Guid)e107["SubInstanceID"]);
            Assert.Equal(TestString(107, "OwnerActivityName"), (string)e107["OwnerActivityName"]);
            Assert.Equal(TestString(107, "OwnerActivityId"), (string)e107["OwnerActivityId"]);
            Assert.Equal(TestString(107, "OwnerActivityInstanceId"), (string)e107["OwnerActivityInstanceId"]);
            Assert.Equal(TestString(107, "OwnerActivityTypeName"), (string)e107["OwnerActivityTypeName"]);
            Assert.Equal(TestString(107, "Annotations"), (string)e107["Annotations"]);
            Assert.Equal(TestString(107, "ProfileName"), (string)e107["ProfileName"]);
            Assert.Equal(TestString(107, "HostReference"), (string)e107["HostReference"]);
            Assert.Equal(TestString(107, "AppDomain"), (string)e107["AppDomain"]);

            // Event 108: CustomTrackingRecordInfo (Multidata7TemplateHA)
            Assert.True(firedEvents.ContainsKey(108), "Event 108 (CustomTrackingRecordInfo) did not fire");
            var e108 = firedEvents[108];
            Assert.Equal(TestGuid(108, 0), (Guid)e108["InstanceId"]);
            Assert.Equal(TestInt64(108, 1), (long)e108["RecordNumber"]);
            Assert.Equal(TestString(108, "Name"), (string)e108["Name"]);
            Assert.Equal(TestString(108, "ActivityName"), (string)e108["ActivityName"]);
            Assert.Equal(TestString(108, "ActivityId"), (string)e108["ActivityId"]);
            Assert.Equal(TestString(108, "ActivityInstanceId"), (string)e108["ActivityInstanceId"]);
            Assert.Equal(TestString(108, "ActivityTypeName"), (string)e108["ActivityTypeName"]);
            Assert.Equal(TestString(108, "Data"), (string)e108["Data"]);
            Assert.Equal(TestString(108, "Annotations"), (string)e108["Annotations"]);
            Assert.Equal(TestString(108, "ProfileName"), (string)e108["ProfileName"]);
            Assert.Equal(TestString(108, "HostReference"), (string)e108["HostReference"]);
            Assert.Equal(TestString(108, "AppDomain"), (string)e108["AppDomain"]);

            // Event 110: CustomTrackingRecordWarning (Multidata7TemplateHA)
            Assert.True(firedEvents.ContainsKey(110), "Event 110 (CustomTrackingRecordWarning) did not fire");
            var e110 = firedEvents[110];
            Assert.Equal(TestGuid(110, 0), (Guid)e110["InstanceId"]);
            Assert.Equal(TestInt64(110, 1), (long)e110["RecordNumber"]);
            Assert.Equal(TestString(110, "Name"), (string)e110["Name"]);
            Assert.Equal(TestString(110, "ActivityName"), (string)e110["ActivityName"]);
            Assert.Equal(TestString(110, "ActivityId"), (string)e110["ActivityId"]);
            Assert.Equal(TestString(110, "ActivityInstanceId"), (string)e110["ActivityInstanceId"]);
            Assert.Equal(TestString(110, "ActivityTypeName"), (string)e110["ActivityTypeName"]);
            Assert.Equal(TestString(110, "Data"), (string)e110["Data"]);
            Assert.Equal(TestString(110, "Annotations"), (string)e110["Annotations"]);
            Assert.Equal(TestString(110, "ProfileName"), (string)e110["ProfileName"]);
            Assert.Equal(TestString(110, "HostReference"), (string)e110["HostReference"]);
            Assert.Equal(TestString(110, "AppDomain"), (string)e110["AppDomain"]);

            // Event 111: CustomTrackingRecordError (Multidata7TemplateHA)
            Assert.True(firedEvents.ContainsKey(111), "Event 111 (CustomTrackingRecordError) did not fire");
            var e111 = firedEvents[111];
            Assert.Equal(TestGuid(111, 0), (Guid)e111["InstanceId"]);
            Assert.Equal(TestInt64(111, 1), (long)e111["RecordNumber"]);
            Assert.Equal(TestString(111, "Name"), (string)e111["Name"]);
            Assert.Equal(TestString(111, "ActivityName"), (string)e111["ActivityName"]);
            Assert.Equal(TestString(111, "ActivityId"), (string)e111["ActivityId"]);
            Assert.Equal(TestString(111, "ActivityInstanceId"), (string)e111["ActivityInstanceId"]);
            Assert.Equal(TestString(111, "ActivityTypeName"), (string)e111["ActivityTypeName"]);
            Assert.Equal(TestString(111, "Data"), (string)e111["Data"]);
            Assert.Equal(TestString(111, "Annotations"), (string)e111["Annotations"]);
            Assert.Equal(TestString(111, "ProfileName"), (string)e111["ProfileName"]);
            Assert.Equal(TestString(111, "HostReference"), (string)e111["HostReference"]);
            Assert.Equal(TestString(111, "AppDomain"), (string)e111["AppDomain"]);

            // Event 112: WorkflowInstanceSuspendedRecord (Multidata8TemplateHA)
            Assert.True(firedEvents.ContainsKey(112), "Event 112 (WorkflowInstanceSuspendedRecord) did not fire");
            var e112 = firedEvents[112];
            Assert.Equal(TestGuid(112, 0), (Guid)e112["InstanceId"]);
            Assert.Equal(TestInt64(112, 1), (long)e112["RecordNumber"]);
            Assert.Equal(TestString(112, "ActivityDefinitionId"), (string)e112["ActivityDefinitionId"]);
            Assert.Equal(TestString(112, "Reason"), (string)e112["Reason"]);
            Assert.Equal(TestString(112, "Annotations"), (string)e112["Annotations"]);
            Assert.Equal(TestString(112, "ProfileName"), (string)e112["ProfileName"]);
            Assert.Equal(TestString(112, "HostReference"), (string)e112["HostReference"]);
            Assert.Equal(TestString(112, "AppDomain"), (string)e112["AppDomain"]);

            // Event 113: WorkflowInstanceTerminatedRecord (Multidata8TemplateHA)
            Assert.True(firedEvents.ContainsKey(113), "Event 113 (WorkflowInstanceTerminatedRecord) did not fire");
            var e113 = firedEvents[113];
            Assert.Equal(TestGuid(113, 0), (Guid)e113["InstanceId"]);
            Assert.Equal(TestInt64(113, 1), (long)e113["RecordNumber"]);
            Assert.Equal(TestString(113, "ActivityDefinitionId"), (string)e113["ActivityDefinitionId"]);
            Assert.Equal(TestString(113, "Reason"), (string)e113["Reason"]);
            Assert.Equal(TestString(113, "Annotations"), (string)e113["Annotations"]);
            Assert.Equal(TestString(113, "ProfileName"), (string)e113["ProfileName"]);
            Assert.Equal(TestString(113, "HostReference"), (string)e113["HostReference"]);
            Assert.Equal(TestString(113, "AppDomain"), (string)e113["AppDomain"]);

            // Event 114: WorkflowInstanceRecordWithId (Multidata11TemplateHA)
            Assert.True(firedEvents.ContainsKey(114), "Event 114 (WorkflowInstanceRecordWithId) did not fire");
            var e114 = firedEvents[114];
            Assert.Equal(TestGuid(114, 0), (Guid)e114["InstanceId"]);
            Assert.Equal(TestInt64(114, 1), (long)e114["RecordNumber"]);
            Assert.Equal(TestString(114, "ActivityDefinitionId"), (string)e114["ActivityDefinitionId"]);
            Assert.Equal(TestString(114, "State"), (string)e114["State"]);
            Assert.Equal(TestString(114, "Annotations"), (string)e114["Annotations"]);
            Assert.Equal(TestString(114, "ProfileName"), (string)e114["ProfileName"]);
            Assert.Equal(TestString(114, "WorkflowDefinitionIdentity"), (string)e114["WorkflowDefinitionIdentity"]);
            Assert.Equal(TestString(114, "HostReference"), (string)e114["HostReference"]);
            Assert.Equal(TestString(114, "AppDomain"), (string)e114["AppDomain"]);

            // Event 115: WorkflowInstanceAbortedRecordWithId (Multidata12TemplateHA)
            Assert.True(firedEvents.ContainsKey(115), "Event 115 (WorkflowInstanceAbortedRecordWithId) did not fire");
            var e115 = firedEvents[115];
            Assert.Equal(TestGuid(115, 0), (Guid)e115["InstanceId"]);
            Assert.Equal(TestInt64(115, 1), (long)e115["RecordNumber"]);
            Assert.Equal(TestString(115, "ActivityDefinitionId"), (string)e115["ActivityDefinitionId"]);
            Assert.Equal(TestString(115, "Reason"), (string)e115["Reason"]);
            Assert.Equal(TestString(115, "Annotations"), (string)e115["Annotations"]);
            Assert.Equal(TestString(115, "ProfileName"), (string)e115["ProfileName"]);
            Assert.Equal(TestString(115, "WorkflowDefinitionIdentity"), (string)e115["WorkflowDefinitionIdentity"]);
            Assert.Equal(TestString(115, "HostReference"), (string)e115["HostReference"]);
            Assert.Equal(TestString(115, "AppDomain"), (string)e115["AppDomain"]);

            // Event 116: WorkflowInstanceSuspendedRecordWithId (Multidata12TemplateHA)
            Assert.True(firedEvents.ContainsKey(116), "Event 116 (WorkflowInstanceSuspendedRecordWithId) did not fire");
            var e116 = firedEvents[116];
            Assert.Equal(TestGuid(116, 0), (Guid)e116["InstanceId"]);
            Assert.Equal(TestInt64(116, 1), (long)e116["RecordNumber"]);
            Assert.Equal(TestString(116, "ActivityDefinitionId"), (string)e116["ActivityDefinitionId"]);
            Assert.Equal(TestString(116, "Reason"), (string)e116["Reason"]);
            Assert.Equal(TestString(116, "Annotations"), (string)e116["Annotations"]);
            Assert.Equal(TestString(116, "ProfileName"), (string)e116["ProfileName"]);
            Assert.Equal(TestString(116, "WorkflowDefinitionIdentity"), (string)e116["WorkflowDefinitionIdentity"]);
            Assert.Equal(TestString(116, "HostReference"), (string)e116["HostReference"]);
            Assert.Equal(TestString(116, "AppDomain"), (string)e116["AppDomain"]);

            // Event 117: WorkflowInstanceTerminatedRecordWithId (Multidata12TemplateHA)
            Assert.True(firedEvents.ContainsKey(117), "Event 117 (WorkflowInstanceTerminatedRecordWithId) did not fire");
            var e117 = firedEvents[117];
            Assert.Equal(TestGuid(117, 0), (Guid)e117["InstanceId"]);
            Assert.Equal(TestInt64(117, 1), (long)e117["RecordNumber"]);
            Assert.Equal(TestString(117, "ActivityDefinitionId"), (string)e117["ActivityDefinitionId"]);
            Assert.Equal(TestString(117, "Reason"), (string)e117["Reason"]);
            Assert.Equal(TestString(117, "Annotations"), (string)e117["Annotations"]);
            Assert.Equal(TestString(117, "ProfileName"), (string)e117["ProfileName"]);
            Assert.Equal(TestString(117, "WorkflowDefinitionIdentity"), (string)e117["WorkflowDefinitionIdentity"]);
            Assert.Equal(TestString(117, "HostReference"), (string)e117["HostReference"]);
            Assert.Equal(TestString(117, "AppDomain"), (string)e117["AppDomain"]);

            // Event 118: WorkflowInstanceUnhandledExceptionRecordWithId (Multidata13TemplateHA)
            Assert.True(firedEvents.ContainsKey(118), "Event 118 (WorkflowInstanceUnhandledExceptionRecordWithId) did not fire");
            var e118 = firedEvents[118];
            Assert.Equal(TestGuid(118, 0), (Guid)e118["InstanceId"]);
            Assert.Equal(TestInt64(118, 1), (long)e118["RecordNumber"]);
            Assert.Equal(TestString(118, "ActivityDefinitionId"), (string)e118["ActivityDefinitionId"]);
            Assert.Equal(TestString(118, "SourceName"), (string)e118["SourceName"]);
            Assert.Equal(TestString(118, "SourceId"), (string)e118["SourceId"]);
            Assert.Equal(TestString(118, "SourceInstanceId"), (string)e118["SourceInstanceId"]);
            Assert.Equal(TestString(118, "SourceTypeName"), (string)e118["SourceTypeName"]);
            Assert.Equal(TestString(118, "Exception"), (string)e118["Exception"]);
            Assert.Equal(TestString(118, "Annotations"), (string)e118["Annotations"]);
            Assert.Equal(TestString(118, "ProfileName"), (string)e118["ProfileName"]);
            Assert.Equal(TestString(118, "WorkflowDefinitionIdentity"), (string)e118["WorkflowDefinitionIdentity"]);
            Assert.Equal(TestString(118, "HostReference"), (string)e118["HostReference"]);
            Assert.Equal(TestString(118, "AppDomain"), (string)e118["AppDomain"]);

            // Event 119: WorkflowInstanceUpdatedRecord (Multidata14TemplateHA)
            Assert.True(firedEvents.ContainsKey(119), "Event 119 (WorkflowInstanceUpdatedRecord) did not fire");
            var e119 = firedEvents[119];
            Assert.Equal(TestGuid(119, 0), (Guid)e119["InstanceId"]);
            Assert.Equal(TestInt64(119, 1), (long)e119["RecordNumber"]);
            Assert.Equal(TestString(119, "ActivityDefinitionId"), (string)e119["ActivityDefinitionId"]);
            Assert.Equal(TestString(119, "State"), (string)e119["State"]);
            Assert.Equal(TestString(119, "OriginalDefinitionIdentity"), (string)e119["OriginalDefinitionIdentity"]);
            Assert.Equal(TestString(119, "UpdatedDefinitionIdentity"), (string)e119["UpdatedDefinitionIdentity"]);
            Assert.Equal(TestString(119, "Annotations"), (string)e119["Annotations"]);
            Assert.Equal(TestString(119, "ProfileName"), (string)e119["ProfileName"]);
            Assert.Equal(TestString(119, "HostReference"), (string)e119["HostReference"]);
            Assert.Equal(TestString(119, "AppDomain"), (string)e119["AppDomain"]);

            // Event 131: BufferPoolAllocation (Multidata1TemplateA)
            Assert.True(firedEvents.ContainsKey(131), "Event 131 (BufferPoolAllocation) did not fire");
            var e131 = firedEvents[131];
            Assert.Equal(TestInt32(131, 0), (int)e131["Size"]);
            Assert.Equal(TestString(131, "AppDomain"), (string)e131["AppDomain"]);

            // Event 132: BufferPoolChangeQuota (Multidata2TemplateA)
            Assert.True(firedEvents.ContainsKey(132), "Event 132 (BufferPoolChangeQuota) did not fire");
            var e132 = firedEvents[132];
            Assert.Equal(TestInt32(132, 0), (int)e132["PoolSize"]);
            Assert.Equal(TestInt32(132, 1), (int)e132["Delta"]);
            Assert.Equal(TestString(132, "AppDomain"), (string)e132["AppDomain"]);

            // Event 133: ActionItemScheduled (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(133), "Event 133 (ActionItemScheduled) did not fire");
            var e133 = firedEvents[133];
            Assert.Equal(TestString(133, "AppDomain"), (string)e133["AppDomain"]);

            // Event 134: ActionItemCallbackInvoked (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(134), "Event 134 (ActionItemCallbackInvoked) did not fire");
            var e134 = firedEvents[134];
            Assert.Equal(TestString(134, "AppDomain"), (string)e134["AppDomain"]);

            // Event 201: ClientMessageInspectorAfterReceiveInvoked (Multidata23TemplateHA)
            Assert.True(firedEvents.ContainsKey(201), "Event 201 (ClientMessageInspectorAfterReceiveInvoked) did not fire");
            var e201 = firedEvents[201];
            Assert.Equal(TestString(201, "TypeName"), (string)e201["TypeName"]);
            Assert.Equal(TestString(201, "HostReference"), (string)e201["HostReference"]);
            Assert.Equal(TestString(201, "AppDomain"), (string)e201["AppDomain"]);

            // Event 202: ClientMessageInspectorBeforeSendInvoked (Multidata23TemplateHA)
            Assert.True(firedEvents.ContainsKey(202), "Event 202 (ClientMessageInspectorBeforeSendInvoked) did not fire");
            var e202 = firedEvents[202];
            Assert.Equal(TestString(202, "TypeName"), (string)e202["TypeName"]);
            Assert.Equal(TestString(202, "HostReference"), (string)e202["HostReference"]);
            Assert.Equal(TestString(202, "AppDomain"), (string)e202["AppDomain"]);

            // Event 203: ClientParameterInspectorAfterCallInvoked (Multidata23TemplateHA)
            Assert.True(firedEvents.ContainsKey(203), "Event 203 (ClientParameterInspectorAfterCallInvoked) did not fire");
            var e203 = firedEvents[203];
            Assert.Equal(TestString(203, "TypeName"), (string)e203["TypeName"]);
            Assert.Equal(TestString(203, "HostReference"), (string)e203["HostReference"]);
            Assert.Equal(TestString(203, "AppDomain"), (string)e203["AppDomain"]);

            // Event 204: ClientParameterInspectorBeforeCallInvoked (Multidata23TemplateHA)
            Assert.True(firedEvents.ContainsKey(204), "Event 204 (ClientParameterInspectorBeforeCallInvoked) did not fire");
            var e204 = firedEvents[204];
            Assert.Equal(TestString(204, "TypeName"), (string)e204["TypeName"]);
            Assert.Equal(TestString(204, "HostReference"), (string)e204["HostReference"]);
            Assert.Equal(TestString(204, "AppDomain"), (string)e204["AppDomain"]);

            // Event 205: OperationInvoked (Multidata24TemplateHA)
            Assert.True(firedEvents.ContainsKey(205), "Event 205 (OperationInvoked) did not fire");
            var e205 = firedEvents[205];
            Assert.Equal(TestString(205, "MethodName"), (string)e205["MethodName"]);
            Assert.Equal(TestString(205, "CallerInfo"), (string)e205["CallerInfo"]);
            Assert.Equal(TestString(205, "HostReference"), (string)e205["HostReference"]);
            Assert.Equal(TestString(205, "AppDomain"), (string)e205["AppDomain"]);

            // Event 206: ErrorHandlerInvoked (Multidata25TemplateHA)
            Assert.True(firedEvents.ContainsKey(206), "Event 206 (ErrorHandlerInvoked) did not fire");
            var e206 = firedEvents[206];
            Assert.Equal(TestString(206, "TypeName"), (string)e206["TypeName"]);
            Assert.Equal((int)TestByte(206, 1), (int)e206["Handled"]);
            Assert.Equal(TestString(206, "ExceptionTypeName"), (string)e206["ExceptionTypeName"]);
            Assert.Equal(TestString(206, "HostReference"), (string)e206["HostReference"]);
            Assert.Equal(TestString(206, "AppDomain"), (string)e206["AppDomain"]);

            // Event 207: FaultProviderInvoked (Multidata26TemplateHA)
            Assert.True(firedEvents.ContainsKey(207), "Event 207 (FaultProviderInvoked) did not fire");
            var e207 = firedEvents[207];
            Assert.Equal(TestString(207, "TypeName"), (string)e207["TypeName"]);
            Assert.Equal(TestString(207, "ExceptionTypeName"), (string)e207["ExceptionTypeName"]);
            Assert.Equal(TestString(207, "HostReference"), (string)e207["HostReference"]);
            Assert.Equal(TestString(207, "AppDomain"), (string)e207["AppDomain"]);

            // Event 208: MessageInspectorAfterReceiveInvoked (Multidata23TemplateHA)
            Assert.True(firedEvents.ContainsKey(208), "Event 208 (MessageInspectorAfterReceiveInvoked) did not fire");
            var e208 = firedEvents[208];
            Assert.Equal(TestString(208, "TypeName"), (string)e208["TypeName"]);
            Assert.Equal(TestString(208, "HostReference"), (string)e208["HostReference"]);
            Assert.Equal(TestString(208, "AppDomain"), (string)e208["AppDomain"]);

            // Event 209: MessageInspectorBeforeSendInvoked (Multidata23TemplateHA)
            Assert.True(firedEvents.ContainsKey(209), "Event 209 (MessageInspectorBeforeSendInvoked) did not fire");
            var e209 = firedEvents[209];
            Assert.Equal(TestString(209, "TypeName"), (string)e209["TypeName"]);
            Assert.Equal(TestString(209, "HostReference"), (string)e209["HostReference"]);
            Assert.Equal(TestString(209, "AppDomain"), (string)e209["AppDomain"]);

            // Event 210: MessageThrottleExceeded (Multidata27TemplateHA)
            Assert.True(firedEvents.ContainsKey(210), "Event 210 (MessageThrottleExceeded) did not fire");
            var e210 = firedEvents[210];
            Assert.Equal(TestString(210, "ThrottleName"), (string)e210["ThrottleName"]);
            Assert.Equal(TestInt64(210, 1), (long)e210["Limit"]);
            Assert.Equal(TestString(210, "HostReference"), (string)e210["HostReference"]);
            Assert.Equal(TestString(210, "AppDomain"), (string)e210["AppDomain"]);

            // Event 211: ParameterInspectorAfterCallInvoked (Multidata23TemplateHA)
            Assert.True(firedEvents.ContainsKey(211), "Event 211 (ParameterInspectorAfterCallInvoked) did not fire");
            var e211 = firedEvents[211];
            Assert.Equal(TestString(211, "TypeName"), (string)e211["TypeName"]);
            Assert.Equal(TestString(211, "HostReference"), (string)e211["HostReference"]);
            Assert.Equal(TestString(211, "AppDomain"), (string)e211["AppDomain"]);

            // Event 212: ParameterInspectorBeforeCallInvoked (Multidata23TemplateHA)
            Assert.True(firedEvents.ContainsKey(212), "Event 212 (ParameterInspectorBeforeCallInvoked) did not fire");
            var e212 = firedEvents[212];
            Assert.Equal(TestString(212, "TypeName"), (string)e212["TypeName"]);
            Assert.Equal(TestString(212, "HostReference"), (string)e212["HostReference"]);
            Assert.Equal(TestString(212, "AppDomain"), (string)e212["AppDomain"]);

            // Event 213: ServiceHostStarted (Multidata72TemplateHA)
            Assert.True(firedEvents.ContainsKey(213), "Event 213 (ServiceHostStarted) did not fire");
            var e213 = firedEvents[213];
            Assert.Equal(TestString(213, "ServiceTypeName"), (string)e213["ServiceTypeName"]);
            Assert.Equal(TestString(213, "HostReference"), (string)e213["HostReference"]);
            Assert.Equal(TestString(213, "AppDomain"), (string)e213["AppDomain"]);

            // Event 214: OperationCompleted (Multidata28TemplateHA)
            Assert.True(firedEvents.ContainsKey(214), "Event 214 (OperationCompleted) did not fire");
            var e214 = firedEvents[214];
            Assert.Equal(TestString(214, "MethodName"), (string)e214["MethodName"]);
            Assert.Equal(TestInt64(214, 1), (long)e214["Duration"]);
            Assert.Equal(TestString(214, "HostReference"), (string)e214["HostReference"]);
            Assert.Equal(TestString(214, "AppDomain"), (string)e214["AppDomain"]);
        }
    }
}
