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
        // =====================================================================
        // Template field definitions for Chunk 06
        // =====================================================================

        private static readonly TemplateField[] Multidata19TemplateAFields = new TemplateField[]
        {
            new TemplateField("id", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata20TemplateAFields = new TemplateField[]
        {
            new TemplateField("expr", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata21TemplateAFields = new TemplateField[]
        {
            new TemplateField("activityName", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata33TemplateAFields = new TemplateField[]
        {
            new TemplateField("TypeName", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata34TemplateAFields = new TemplateField[]
        {
            new TemplateField("TypeName", FieldType.UnicodeString),
            new TemplateField("ExceptionToString", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata35TemplateSAFields = new TemplateField[]
        {
            new TemplateField("Count", FieldType.Int32),
            new TemplateField("MaxNum", FieldType.Int32),
            new TemplateField("EventSource", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata36TemplateSAFields = new TemplateField[]
        {
            new TemplateField("Count", FieldType.Int32),
            new TemplateField("EventSource", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata37TemplateSAFields = new TemplateField[]
        {
            new TemplateField("TypeName", FieldType.UnicodeString),
            new TemplateField("Uri", FieldType.UnicodeString),
            new TemplateField("EventSource", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata38TemplateHAFields = new TemplateField[]
        {
            new TemplateField("OperationName", FieldType.UnicodeString),
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata39TemplateSAFields = new TemplateField[]
        {
            new TemplateField("Size", FieldType.Int32),
            new TemplateField("EventSource", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata40TemplateAFields = new TemplateField[]
        {
            new TemplateField("RemoteAddress", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata41TemplateAFields = new TemplateField[]
        {
            new TemplateField("ListenerHashCode", FieldType.Int32),
            new TemplateField("SocketHashCode", FieldType.Int32),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata42TemplateAFields = new TemplateField[]
        {
            new TemplateField("PoolKey", FieldType.UnicodeString),
            new TemplateField("busy", FieldType.Int32),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Chunk06_OneStringsTemplateAFields = new TemplateField[]
        {
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Chunk06_ThreeStringsTemplateAFields = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("data2", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Chunk06_TwoStringsTemplateAFields = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] TwoStringsTemplateSAFields = new TemplateField[]
        {
            new TemplateField("EventSource", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        // =====================================================================
        // WriteMetadata_Chunk06 — writes metadata entries for each unique
        // (eventId, eventName, template) tuple in this chunk.
        // =====================================================================
        private void WriteMetadata_Chunk06(EventPipeWriterV5 writer, ref int metadataId)
        {
            // EVENT:2024|InternalCacheMetadataStart|Multidata19TemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "InternalCacheMetadataStart", 2024,
                new MetadataParameter("id", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:2025|InternalCacheMetadataStop|Multidata19TemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "InternalCacheMetadataStop", 2025,
                new MetadataParameter("id", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:2026|CompileVbExpressionStart|Multidata20TemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "CompileVbExpressionStart", 2026,
                new MetadataParameter("expr", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:2027|CacheRootMetadataStart|Multidata21TemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "CacheRootMetadataStart", 2027,
                new MetadataParameter("activityName", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:2028|CacheRootMetadataStop|Multidata21TemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "CacheRootMetadataStop", 2028,
                new MetadataParameter("activityName", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:2029|CompileVbExpressionStop|OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "CompileVbExpressionStop", 2029,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:2576|TryCatchExceptionFromTry|ThreeStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "TryCatchExceptionFromTry", 2576,
                new MetadataParameter("data1", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("data2", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:2577|TryCatchExceptionDuringCancelation|TwoStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "TryCatchExceptionDuringCancelation", 2577,
                new MetadataParameter("data1", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:2578|TryCatchExceptionFromCatchOrFinally|TwoStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "TryCatchExceptionFromCatchOrFinally", 2578,
                new MetadataParameter("data1", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:3300|ReceiveContextCompleteFailed|Multidata33TemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ReceiveContextCompleteFailed", 3300,
                new MetadataParameter("TypeName", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:3301|ReceiveContextAbandonFailed|Multidata33TemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ReceiveContextAbandonFailed", 3301,
                new MetadataParameter("TypeName", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:3302|ReceiveContextFaulted|TwoStringsTemplateSA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ReceiveContextFaulted", 3302,
                new MetadataParameter("EventSource", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:3303|ReceiveContextAbandonWithException|Multidata34TemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ReceiveContextAbandonWithException", 3303,
                new MetadataParameter("TypeName", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("ExceptionToString", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:3305|ClientBaseCachedChannelFactoryCount|Multidata35TemplateSA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ClientBaseCachedChannelFactoryCount", 3305,
                new MetadataParameter("Count", MetadataTypeCode.Int32),
                new MetadataParameter("MaxNum", MetadataTypeCode.Int32),
                new MetadataParameter("EventSource", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:3306|ClientBaseChannelFactoryAgedOutofCache|Multidata36TemplateSA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ClientBaseChannelFactoryAgedOutofCache", 3306,
                new MetadataParameter("Count", MetadataTypeCode.Int32),
                new MetadataParameter("EventSource", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:3307|ClientBaseChannelFactoryCacheHit|TwoStringsTemplateSA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ClientBaseChannelFactoryCacheHit", 3307,
                new MetadataParameter("EventSource", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:3308|ClientBaseUsingLocalChannelFactory|TwoStringsTemplateSA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ClientBaseUsingLocalChannelFactory", 3308,
                new MetadataParameter("EventSource", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:3309|QueryCompositionExecuted|Multidata37TemplateSA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "QueryCompositionExecuted", 3309,
                new MetadataParameter("TypeName", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("Uri", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("EventSource", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:3310|DispatchFailed|Multidata38TemplateHA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "DispatchFailed", 3310,
                new MetadataParameter("OperationName", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("HostReference", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:3311|DispatchSuccessful|Multidata38TemplateHA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "DispatchSuccessful", 3311,
                new MetadataParameter("OperationName", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("HostReference", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:3312|MessageReadByEncoder|Multidata39TemplateSA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "MessageReadByEncoder", 3312,
                new MetadataParameter("Size", MetadataTypeCode.Int32),
                new MetadataParameter("EventSource", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:3313|MessageWrittenByEncoder|Multidata39TemplateSA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "MessageWrittenByEncoder", 3313,
                new MetadataParameter("Size", MetadataTypeCode.Int32),
                new MetadataParameter("EventSource", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:3314|SessionIdleTimeout|Multidata40TemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SessionIdleTimeout", 3314,
                new MetadataParameter("RemoteAddress", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:3319|SocketAcceptEnqueued|OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SocketAcceptEnqueued", 3319,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:3320|SocketAccepted|Multidata41TemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SocketAccepted", 3320,
                new MetadataParameter("ListenerHashCode", MetadataTypeCode.Int32),
                new MetadataParameter("SocketHashCode", MetadataTypeCode.Int32),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:3321|ConnectionPoolMiss|Multidata42TemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ConnectionPoolMiss", 3321,
                new MetadataParameter("PoolKey", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("busy", MetadataTypeCode.Int32),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:3322|DispatchFormatterDeserializeRequestStart|OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "DispatchFormatterDeserializeRequestStart", 3322,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:3323|DispatchFormatterDeserializeRequestStop|OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "DispatchFormatterDeserializeRequestStop", 3323,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:3324|DispatchFormatterSerializeReplyStart|OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "DispatchFormatterSerializeReplyStart", 3324,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:3325|DispatchFormatterSerializeReplyStop|OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "DispatchFormatterSerializeReplyStop", 3325,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:3326|ClientFormatterSerializeRequestStart|OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ClientFormatterSerializeRequestStart", 3326,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:3327|ClientFormatterSerializeRequestStop|OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ClientFormatterSerializeRequestStop", 3327,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:3328|ClientFormatterDeserializeReplyStart|OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ClientFormatterDeserializeReplyStart", 3328,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:3329|ClientFormatterDeserializeReplyStop|OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ClientFormatterDeserializeReplyStop", 3329,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:3330|SecurityNegotiationStart|OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SecurityNegotiationStart", 3330,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:3331|SecurityNegotiationStop|OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SecurityNegotiationStop", 3331,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // EVENT:3332|SecurityTokenProviderOpened|OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SecurityTokenProviderOpened", 3332,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));
        }

        // =====================================================================
        // WriteEvents_Chunk06 — writes one event payload per event in this chunk.
        // =====================================================================
        private void WriteEvents_Chunk06(EventPipeWriterV5 writer, ref int metadataId, ref int sequenceNumber)
        {
            int __metadataId = metadataId;
            int __sequenceNumber = sequenceNumber;
            // EVENT:2024|InternalCacheMetadataStart|Multidata19TemplateA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(2024, Multidata19TemplateAFields)));

            // EVENT:2025|InternalCacheMetadataStop|Multidata19TemplateA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(2025, Multidata19TemplateAFields)));

            // EVENT:2026|CompileVbExpressionStart|Multidata20TemplateA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(2026, Multidata20TemplateAFields)));

            // EVENT:2027|CacheRootMetadataStart|Multidata21TemplateA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(2027, Multidata21TemplateAFields)));

            // EVENT:2028|CacheRootMetadataStop|Multidata21TemplateA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(2028, Multidata21TemplateAFields)));

            // EVENT:2029|CompileVbExpressionStop|OneStringsTemplateA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(2029, Chunk06_OneStringsTemplateAFields)));

            // EVENT:2576|TryCatchExceptionFromTry|ThreeStringsTemplateA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(2576, Chunk06_ThreeStringsTemplateAFields)));

            // EVENT:2577|TryCatchExceptionDuringCancelation|TwoStringsTemplateA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(2577, Chunk06_TwoStringsTemplateAFields)));

            // EVENT:2578|TryCatchExceptionFromCatchOrFinally|TwoStringsTemplateA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(2578, Chunk06_TwoStringsTemplateAFields)));

            // EVENT:3300|ReceiveContextCompleteFailed|Multidata33TemplateA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3300, Multidata33TemplateAFields)));

            // EVENT:3301|ReceiveContextAbandonFailed|Multidata33TemplateA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3301, Multidata33TemplateAFields)));

            // EVENT:3302|ReceiveContextFaulted|TwoStringsTemplateSA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3302, TwoStringsTemplateSAFields)));

            // EVENT:3303|ReceiveContextAbandonWithException|Multidata34TemplateA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3303, Multidata34TemplateAFields)));

            // EVENT:3305|ClientBaseCachedChannelFactoryCount|Multidata35TemplateSA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3305, Multidata35TemplateSAFields)));

            // EVENT:3306|ClientBaseChannelFactoryAgedOutofCache|Multidata36TemplateSA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3306, Multidata36TemplateSAFields)));

            // EVENT:3307|ClientBaseChannelFactoryCacheHit|TwoStringsTemplateSA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3307, TwoStringsTemplateSAFields)));

            // EVENT:3308|ClientBaseUsingLocalChannelFactory|TwoStringsTemplateSA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3308, TwoStringsTemplateSAFields)));

            // EVENT:3309|QueryCompositionExecuted|Multidata37TemplateSA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3309, Multidata37TemplateSAFields)));

            // EVENT:3310|DispatchFailed|Multidata38TemplateHA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3310, Multidata38TemplateHAFields)));

            // EVENT:3311|DispatchSuccessful|Multidata38TemplateHA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3311, Multidata38TemplateHAFields)));

            // EVENT:3312|MessageReadByEncoder|Multidata39TemplateSA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3312, Multidata39TemplateSAFields)));

            // EVENT:3313|MessageWrittenByEncoder|Multidata39TemplateSA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3313, Multidata39TemplateSAFields)));

            // EVENT:3314|SessionIdleTimeout|Multidata40TemplateA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3314, Multidata40TemplateAFields)));

            // EVENT:3319|SocketAcceptEnqueued|OneStringsTemplateA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3319, Chunk06_OneStringsTemplateAFields)));

            // EVENT:3320|SocketAccepted|Multidata41TemplateA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3320, Multidata41TemplateAFields)));

            // EVENT:3321|ConnectionPoolMiss|Multidata42TemplateA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3321, Multidata42TemplateAFields)));

            // EVENT:3322|DispatchFormatterDeserializeRequestStart|OneStringsTemplateA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3322, Chunk06_OneStringsTemplateAFields)));

            // EVENT:3323|DispatchFormatterDeserializeRequestStop|OneStringsTemplateA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3323, Chunk06_OneStringsTemplateAFields)));

            // EVENT:3324|DispatchFormatterSerializeReplyStart|OneStringsTemplateA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3324, Chunk06_OneStringsTemplateAFields)));

            // EVENT:3325|DispatchFormatterSerializeReplyStop|OneStringsTemplateA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3325, Chunk06_OneStringsTemplateAFields)));

            // EVENT:3326|ClientFormatterSerializeRequestStart|OneStringsTemplateA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3326, Chunk06_OneStringsTemplateAFields)));

            // EVENT:3327|ClientFormatterSerializeRequestStop|OneStringsTemplateA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3327, Chunk06_OneStringsTemplateAFields)));

            // EVENT:3328|ClientFormatterDeserializeReplyStart|OneStringsTemplateA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3328, Chunk06_OneStringsTemplateAFields)));

            // EVENT:3329|ClientFormatterDeserializeReplyStop|OneStringsTemplateA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3329, Chunk06_OneStringsTemplateAFields)));

            // EVENT:3330|SecurityNegotiationStart|OneStringsTemplateA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3330, Chunk06_OneStringsTemplateAFields)));

            // EVENT:3331|SecurityNegotiationStop|OneStringsTemplateA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3331, Chunk06_OneStringsTemplateAFields)));

            // EVENT:3332|SecurityTokenProviderOpened|OneStringsTemplateA
            writer.WriteEventBlock(w => w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3332, Chunk06_OneStringsTemplateAFields)));
        
            metadataId = __metadataId;
            sequenceNumber = __sequenceNumber;
        }

        // =====================================================================
        // Subscribe_Chunk06 — subscribes to all 38 events in this chunk.
        // =====================================================================
        private void Subscribe_Chunk06(ApplicationServerTraceEventParser parser, Dictionary<int, Dictionary<string, object>> firedEvents)
        {
            parser.InternalCacheMetadataStart += delegate (Multidata19TemplateATraceData data)
            {
                firedEvents[2024] = new Dictionary<string, object>
                {
                    { "id", data.id },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.InternalCacheMetadataStop += delegate (Multidata19TemplateATraceData data)
            {
                firedEvents[2025] = new Dictionary<string, object>
                {
                    { "id", data.id },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.CompileVbExpressionStart += delegate (Multidata20TemplateATraceData data)
            {
                firedEvents[2026] = new Dictionary<string, object>
                {
                    { "expr", data.expr },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.CacheRootMetadataStart += delegate (Multidata21TemplateATraceData data)
            {
                firedEvents[2027] = new Dictionary<string, object>
                {
                    { "activityName", data.activityName },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.CacheRootMetadataStop += delegate (Multidata21TemplateATraceData data)
            {
                firedEvents[2028] = new Dictionary<string, object>
                {
                    { "activityName", data.activityName },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.CompileVbExpressionStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[2029] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.TryCatchExceptionFromTry += delegate (ThreeStringsTemplateATraceData data)
            {
                firedEvents[2576] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "data2", data.data2 },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.TryCatchExceptionDuringCancelation += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents[2577] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.TryCatchExceptionFromCatchOrFinally += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents[2578] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.ReceiveContextCompleteFailed += delegate (Multidata33TemplateATraceData data)
            {
                firedEvents[3300] = new Dictionary<string, object>
                {
                    { "TypeName", data.TypeName },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.ReceiveContextAbandonFailed += delegate (Multidata33TemplateATraceData data)
            {
                firedEvents[3301] = new Dictionary<string, object>
                {
                    { "TypeName", data.TypeName },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.ReceiveContextFaulted += delegate (TwoStringsTemplateSATraceData data)
            {
                firedEvents[3302] = new Dictionary<string, object>
                {
                    { "EventSource", data.EventSource },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.ReceiveContextAbandonWithException += delegate (Multidata34TemplateATraceData data)
            {
                firedEvents[3303] = new Dictionary<string, object>
                {
                    { "TypeName", data.TypeName },
                    { "ExceptionToString", data.ExceptionToString },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.ClientBaseCachedChannelFactoryCount += delegate (Multidata35TemplateSATraceData data)
            {
                firedEvents[3305] = new Dictionary<string, object>
                {
                    { "Count", data.Count },
                    { "MaxNum", data.MaxNum },
                    { "EventSource", data.EventSource },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.ClientBaseChannelFactoryAgedOutofCache += delegate (Multidata36TemplateSATraceData data)
            {
                firedEvents[3306] = new Dictionary<string, object>
                {
                    { "Count", data.Count },
                    { "EventSource", data.EventSource },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.ClientBaseChannelFactoryCacheHit += delegate (TwoStringsTemplateSATraceData data)
            {
                firedEvents[3307] = new Dictionary<string, object>
                {
                    { "EventSource", data.EventSource },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.ClientBaseUsingLocalChannelFactory += delegate (TwoStringsTemplateSATraceData data)
            {
                firedEvents[3308] = new Dictionary<string, object>
                {
                    { "EventSource", data.EventSource },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.QueryCompositionExecuted += delegate (Multidata37TemplateSATraceData data)
            {
                firedEvents[3309] = new Dictionary<string, object>
                {
                    { "TypeName", data.TypeName },
                    { "Uri", data.Uri },
                    { "EventSource", data.EventSource },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DispatchFailed += delegate (Multidata38TemplateHATraceData data)
            {
                firedEvents[3310] = new Dictionary<string, object>
                {
                    { "OperationName", data.OperationName },
                    { "HostReference", data.HostReference },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DispatchSuccessful += delegate (Multidata38TemplateHATraceData data)
            {
                firedEvents[3311] = new Dictionary<string, object>
                {
                    { "OperationName", data.OperationName },
                    { "HostReference", data.HostReference },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.MessageReadByEncoder += delegate (Multidata39TemplateSATraceData data)
            {
                firedEvents[3312] = new Dictionary<string, object>
                {
                    { "Size", data.Size },
                    { "EventSource", data.EventSource },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.MessageWrittenByEncoder += delegate (Multidata39TemplateSATraceData data)
            {
                firedEvents[3313] = new Dictionary<string, object>
                {
                    { "Size", data.Size },
                    { "EventSource", data.EventSource },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.SessionIdleTimeout += delegate (Multidata40TemplateATraceData data)
            {
                firedEvents[3314] = new Dictionary<string, object>
                {
                    { "RemoteAddress", data.RemoteAddress },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.SocketAcceptEnqueued += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[3319] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.SocketAccepted += delegate (Multidata41TemplateATraceData data)
            {
                firedEvents[3320] = new Dictionary<string, object>
                {
                    { "ListenerHashCode", data.ListenerHashCode },
                    { "SocketHashCode", data.SocketHashCode },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.ConnectionPoolMiss += delegate (Multidata42TemplateATraceData data)
            {
                firedEvents[3321] = new Dictionary<string, object>
                {
                    { "PoolKey", data.PoolKey },
                    { "busy", data.busy },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DispatchFormatterDeserializeRequestStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[3322] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DispatchFormatterDeserializeRequestStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[3323] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DispatchFormatterSerializeReplyStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[3324] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DispatchFormatterSerializeReplyStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[3325] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.ClientFormatterSerializeRequestStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[3326] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.ClientFormatterSerializeRequestStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[3327] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.ClientFormatterDeserializeReplyStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[3328] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.ClientFormatterDeserializeReplyStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[3329] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.SecurityNegotiationStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[3330] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.SecurityNegotiationStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[3331] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.SecurityTokenProviderOpened += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents[3332] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };
        }

        // =====================================================================
        // Validate_Chunk06 — asserts all 38 events fired with correct payloads.
        // =====================================================================
        private void Validate_Chunk06(Dictionary<int, Dictionary<string, object>> firedEvents)
        {
            // EVENT:2024|InternalCacheMetadataStart|Multidata19TemplateA
            Assert.True(firedEvents.ContainsKey(2024), "Event 2024 (InternalCacheMetadataStart) did not fire.");
            Assert.Equal(TestString(2024, "id"), firedEvents[2024]["id"]);
            Assert.Equal(TestString(2024, "AppDomain"), firedEvents[2024]["AppDomain"]);

            // EVENT:2025|InternalCacheMetadataStop|Multidata19TemplateA
            Assert.True(firedEvents.ContainsKey(2025), "Event 2025 (InternalCacheMetadataStop) did not fire.");
            Assert.Equal(TestString(2025, "id"), firedEvents[2025]["id"]);
            Assert.Equal(TestString(2025, "AppDomain"), firedEvents[2025]["AppDomain"]);

            // EVENT:2026|CompileVbExpressionStart|Multidata20TemplateA
            Assert.True(firedEvents.ContainsKey(2026), "Event 2026 (CompileVbExpressionStart) did not fire.");
            Assert.Equal(TestString(2026, "expr"), firedEvents[2026]["expr"]);
            Assert.Equal(TestString(2026, "AppDomain"), firedEvents[2026]["AppDomain"]);

            // EVENT:2027|CacheRootMetadataStart|Multidata21TemplateA
            Assert.True(firedEvents.ContainsKey(2027), "Event 2027 (CacheRootMetadataStart) did not fire.");
            Assert.Equal(TestString(2027, "activityName"), firedEvents[2027]["activityName"]);
            Assert.Equal(TestString(2027, "AppDomain"), firedEvents[2027]["AppDomain"]);

            // EVENT:2028|CacheRootMetadataStop|Multidata21TemplateA
            Assert.True(firedEvents.ContainsKey(2028), "Event 2028 (CacheRootMetadataStop) did not fire.");
            Assert.Equal(TestString(2028, "activityName"), firedEvents[2028]["activityName"]);
            Assert.Equal(TestString(2028, "AppDomain"), firedEvents[2028]["AppDomain"]);

            // EVENT:2029|CompileVbExpressionStop|OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(2029), "Event 2029 (CompileVbExpressionStop) did not fire.");
            Assert.Equal(TestString(2029, "AppDomain"), firedEvents[2029]["AppDomain"]);

            // EVENT:2576|TryCatchExceptionFromTry|ThreeStringsTemplateA
            Assert.True(firedEvents.ContainsKey(2576), "Event 2576 (TryCatchExceptionFromTry) did not fire.");
            Assert.Equal(TestString(2576, "data1"), firedEvents[2576]["data1"]);
            Assert.Equal(TestString(2576, "data2"), firedEvents[2576]["data2"]);
            Assert.Equal(TestString(2576, "AppDomain"), firedEvents[2576]["AppDomain"]);

            // EVENT:2577|TryCatchExceptionDuringCancelation|TwoStringsTemplateA
            Assert.True(firedEvents.ContainsKey(2577), "Event 2577 (TryCatchExceptionDuringCancelation) did not fire.");
            Assert.Equal(TestString(2577, "data1"), firedEvents[2577]["data1"]);
            Assert.Equal(TestString(2577, "AppDomain"), firedEvents[2577]["AppDomain"]);

            // EVENT:2578|TryCatchExceptionFromCatchOrFinally|TwoStringsTemplateA
            Assert.True(firedEvents.ContainsKey(2578), "Event 2578 (TryCatchExceptionFromCatchOrFinally) did not fire.");
            Assert.Equal(TestString(2578, "data1"), firedEvents[2578]["data1"]);
            Assert.Equal(TestString(2578, "AppDomain"), firedEvents[2578]["AppDomain"]);

            // EVENT:3300|ReceiveContextCompleteFailed|Multidata33TemplateA
            Assert.True(firedEvents.ContainsKey(3300), "Event 3300 (ReceiveContextCompleteFailed) did not fire.");
            Assert.Equal(TestString(3300, "TypeName"), firedEvents[3300]["TypeName"]);
            Assert.Equal(TestString(3300, "AppDomain"), firedEvents[3300]["AppDomain"]);

            // EVENT:3301|ReceiveContextAbandonFailed|Multidata33TemplateA
            Assert.True(firedEvents.ContainsKey(3301), "Event 3301 (ReceiveContextAbandonFailed) did not fire.");
            Assert.Equal(TestString(3301, "TypeName"), firedEvents[3301]["TypeName"]);
            Assert.Equal(TestString(3301, "AppDomain"), firedEvents[3301]["AppDomain"]);

            // EVENT:3302|ReceiveContextFaulted|TwoStringsTemplateSA
            Assert.True(firedEvents.ContainsKey(3302), "Event 3302 (ReceiveContextFaulted) did not fire.");
            Assert.Equal(TestString(3302, "EventSource"), firedEvents[3302]["EventSource"]);
            Assert.Equal(TestString(3302, "AppDomain"), firedEvents[3302]["AppDomain"]);

            // EVENT:3303|ReceiveContextAbandonWithException|Multidata34TemplateA
            Assert.True(firedEvents.ContainsKey(3303), "Event 3303 (ReceiveContextAbandonWithException) did not fire.");
            Assert.Equal(TestString(3303, "TypeName"), firedEvents[3303]["TypeName"]);
            Assert.Equal(TestString(3303, "ExceptionToString"), firedEvents[3303]["ExceptionToString"]);
            Assert.Equal(TestString(3303, "AppDomain"), firedEvents[3303]["AppDomain"]);

            // EVENT:3305|ClientBaseCachedChannelFactoryCount|Multidata35TemplateSA
            Assert.True(firedEvents.ContainsKey(3305), "Event 3305 (ClientBaseCachedChannelFactoryCount) did not fire.");
            Assert.Equal(TestInt32(3305, 0), firedEvents[3305]["Count"]);
            Assert.Equal(TestInt32(3305, 1), firedEvents[3305]["MaxNum"]);
            Assert.Equal(TestString(3305, "EventSource"), firedEvents[3305]["EventSource"]);
            Assert.Equal(TestString(3305, "AppDomain"), firedEvents[3305]["AppDomain"]);

            // EVENT:3306|ClientBaseChannelFactoryAgedOutofCache|Multidata36TemplateSA
            Assert.True(firedEvents.ContainsKey(3306), "Event 3306 (ClientBaseChannelFactoryAgedOutofCache) did not fire.");
            Assert.Equal(TestInt32(3306, 0), firedEvents[3306]["Count"]);
            Assert.Equal(TestString(3306, "EventSource"), firedEvents[3306]["EventSource"]);
            Assert.Equal(TestString(3306, "AppDomain"), firedEvents[3306]["AppDomain"]);

            // EVENT:3307|ClientBaseChannelFactoryCacheHit|TwoStringsTemplateSA
            Assert.True(firedEvents.ContainsKey(3307), "Event 3307 (ClientBaseChannelFactoryCacheHit) did not fire.");
            Assert.Equal(TestString(3307, "EventSource"), firedEvents[3307]["EventSource"]);
            Assert.Equal(TestString(3307, "AppDomain"), firedEvents[3307]["AppDomain"]);

            // EVENT:3308|ClientBaseUsingLocalChannelFactory|TwoStringsTemplateSA
            Assert.True(firedEvents.ContainsKey(3308), "Event 3308 (ClientBaseUsingLocalChannelFactory) did not fire.");
            Assert.Equal(TestString(3308, "EventSource"), firedEvents[3308]["EventSource"]);
            Assert.Equal(TestString(3308, "AppDomain"), firedEvents[3308]["AppDomain"]);

            // EVENT:3309|QueryCompositionExecuted|Multidata37TemplateSA
            Assert.True(firedEvents.ContainsKey(3309), "Event 3309 (QueryCompositionExecuted) did not fire.");
            Assert.Equal(TestString(3309, "TypeName"), firedEvents[3309]["TypeName"]);
            Assert.Equal(TestString(3309, "Uri"), firedEvents[3309]["Uri"]);
            Assert.Equal(TestString(3309, "EventSource"), firedEvents[3309]["EventSource"]);
            Assert.Equal(TestString(3309, "AppDomain"), firedEvents[3309]["AppDomain"]);

            // EVENT:3310|DispatchFailed|Multidata38TemplateHA
            Assert.True(firedEvents.ContainsKey(3310), "Event 3310 (DispatchFailed) did not fire.");
            Assert.Equal(TestString(3310, "OperationName"), firedEvents[3310]["OperationName"]);
            Assert.Equal(TestString(3310, "HostReference"), firedEvents[3310]["HostReference"]);
            Assert.Equal(TestString(3310, "AppDomain"), firedEvents[3310]["AppDomain"]);

            // EVENT:3311|DispatchSuccessful|Multidata38TemplateHA
            Assert.True(firedEvents.ContainsKey(3311), "Event 3311 (DispatchSuccessful) did not fire.");
            Assert.Equal(TestString(3311, "OperationName"), firedEvents[3311]["OperationName"]);
            Assert.Equal(TestString(3311, "HostReference"), firedEvents[3311]["HostReference"]);
            Assert.Equal(TestString(3311, "AppDomain"), firedEvents[3311]["AppDomain"]);

            // EVENT:3312|MessageReadByEncoder|Multidata39TemplateSA
            Assert.True(firedEvents.ContainsKey(3312), "Event 3312 (MessageReadByEncoder) did not fire.");
            Assert.Equal(TestInt32(3312, 0), firedEvents[3312]["Size"]);
            Assert.Equal(TestString(3312, "EventSource"), firedEvents[3312]["EventSource"]);
            Assert.Equal(TestString(3312, "AppDomain"), firedEvents[3312]["AppDomain"]);

            // EVENT:3313|MessageWrittenByEncoder|Multidata39TemplateSA
            Assert.True(firedEvents.ContainsKey(3313), "Event 3313 (MessageWrittenByEncoder) did not fire.");
            Assert.Equal(TestInt32(3313, 0), firedEvents[3313]["Size"]);
            Assert.Equal(TestString(3313, "EventSource"), firedEvents[3313]["EventSource"]);
            Assert.Equal(TestString(3313, "AppDomain"), firedEvents[3313]["AppDomain"]);

            // EVENT:3314|SessionIdleTimeout|Multidata40TemplateA
            Assert.True(firedEvents.ContainsKey(3314), "Event 3314 (SessionIdleTimeout) did not fire.");
            Assert.Equal(TestString(3314, "RemoteAddress"), firedEvents[3314]["RemoteAddress"]);
            Assert.Equal(TestString(3314, "AppDomain"), firedEvents[3314]["AppDomain"]);

            // EVENT:3319|SocketAcceptEnqueued|OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3319), "Event 3319 (SocketAcceptEnqueued) did not fire.");
            Assert.Equal(TestString(3319, "AppDomain"), firedEvents[3319]["AppDomain"]);

            // EVENT:3320|SocketAccepted|Multidata41TemplateA
            Assert.True(firedEvents.ContainsKey(3320), "Event 3320 (SocketAccepted) did not fire.");
            Assert.Equal(TestInt32(3320, 0), firedEvents[3320]["ListenerHashCode"]);
            Assert.Equal(TestInt32(3320, 1), firedEvents[3320]["SocketHashCode"]);
            Assert.Equal(TestString(3320, "AppDomain"), firedEvents[3320]["AppDomain"]);

            // EVENT:3321|ConnectionPoolMiss|Multidata42TemplateA
            Assert.True(firedEvents.ContainsKey(3321), "Event 3321 (ConnectionPoolMiss) did not fire.");
            Assert.Equal(TestString(3321, "PoolKey"), firedEvents[3321]["PoolKey"]);
            Assert.Equal(TestInt32(3321, 1), firedEvents[3321]["busy"]);
            Assert.Equal(TestString(3321, "AppDomain"), firedEvents[3321]["AppDomain"]);

            // EVENT:3322|DispatchFormatterDeserializeRequestStart|OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3322), "Event 3322 (DispatchFormatterDeserializeRequestStart) did not fire.");
            Assert.Equal(TestString(3322, "AppDomain"), firedEvents[3322]["AppDomain"]);

            // EVENT:3323|DispatchFormatterDeserializeRequestStop|OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3323), "Event 3323 (DispatchFormatterDeserializeRequestStop) did not fire.");
            Assert.Equal(TestString(3323, "AppDomain"), firedEvents[3323]["AppDomain"]);

            // EVENT:3324|DispatchFormatterSerializeReplyStart|OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3324), "Event 3324 (DispatchFormatterSerializeReplyStart) did not fire.");
            Assert.Equal(TestString(3324, "AppDomain"), firedEvents[3324]["AppDomain"]);

            // EVENT:3325|DispatchFormatterSerializeReplyStop|OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3325), "Event 3325 (DispatchFormatterSerializeReplyStop) did not fire.");
            Assert.Equal(TestString(3325, "AppDomain"), firedEvents[3325]["AppDomain"]);

            // EVENT:3326|ClientFormatterSerializeRequestStart|OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3326), "Event 3326 (ClientFormatterSerializeRequestStart) did not fire.");
            Assert.Equal(TestString(3326, "AppDomain"), firedEvents[3326]["AppDomain"]);

            // EVENT:3327|ClientFormatterSerializeRequestStop|OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3327), "Event 3327 (ClientFormatterSerializeRequestStop) did not fire.");
            Assert.Equal(TestString(3327, "AppDomain"), firedEvents[3327]["AppDomain"]);

            // EVENT:3328|ClientFormatterDeserializeReplyStart|OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3328), "Event 3328 (ClientFormatterDeserializeReplyStart) did not fire.");
            Assert.Equal(TestString(3328, "AppDomain"), firedEvents[3328]["AppDomain"]);

            // EVENT:3329|ClientFormatterDeserializeReplyStop|OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3329), "Event 3329 (ClientFormatterDeserializeReplyStop) did not fire.");
            Assert.Equal(TestString(3329, "AppDomain"), firedEvents[3329]["AppDomain"]);

            // EVENT:3330|SecurityNegotiationStart|OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3330), "Event 3330 (SecurityNegotiationStart) did not fire.");
            Assert.Equal(TestString(3330, "AppDomain"), firedEvents[3330]["AppDomain"]);

            // EVENT:3331|SecurityNegotiationStop|OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3331), "Event 3331 (SecurityNegotiationStop) did not fire.");
            Assert.Equal(TestString(3331, "AppDomain"), firedEvents[3331]["AppDomain"]);

            // EVENT:3332|SecurityTokenProviderOpened|OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3332), "Event 3332 (SecurityTokenProviderOpened) did not fire.");
            Assert.Equal(TestString(3332, "AppDomain"), firedEvents[3332]["AppDomain"]);
        }
    }
}
