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
        private void Subscribe_Chunk06(ApplicationServerTraceEventParser parser, Dictionary<string, Dictionary<string, object>> firedEvents)
        {
            parser.InternalCacheMetadataStart += delegate (Multidata19TemplateATraceData data)
            {
                firedEvents["InternalCacheMetadataStart"] = new Dictionary<string, object>
                {
                    { "id", data.id },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.InternalCacheMetadataStop += delegate (Multidata19TemplateATraceData data)
            {
                firedEvents["InternalCacheMetadataStop"] = new Dictionary<string, object>
                {
                    { "id", data.id },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.CompileVbExpressionStart += delegate (Multidata20TemplateATraceData data)
            {
                firedEvents["CompileVbExpressionStart"] = new Dictionary<string, object>
                {
                    { "expr", data.expr },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.CacheRootMetadataStart += delegate (Multidata21TemplateATraceData data)
            {
                firedEvents["CacheRootMetadataStart"] = new Dictionary<string, object>
                {
                    { "activityName", data.activityName },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.CacheRootMetadataStop += delegate (Multidata21TemplateATraceData data)
            {
                firedEvents["CacheRootMetadataStop"] = new Dictionary<string, object>
                {
                    { "activityName", data.activityName },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.CompileVbExpressionStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["CompileVbExpressionStop"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.TryCatchExceptionFromTry += delegate (ThreeStringsTemplateATraceData data)
            {
                firedEvents["TryCatchExceptionFromTry"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "data2", data.data2 },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.TryCatchExceptionDuringCancelation += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents["TryCatchExceptionDuringCancelation"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.TryCatchExceptionFromCatchOrFinally += delegate (TwoStringsTemplateATraceData data)
            {
                firedEvents["TryCatchExceptionFromCatchOrFinally"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.ReceiveContextCompleteFailed += delegate (Multidata33TemplateATraceData data)
            {
                firedEvents["ReceiveContextCompleteFailed"] = new Dictionary<string, object>
                {
                    { "TypeName", data.TypeName },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.ReceiveContextAbandonFailed += delegate (Multidata33TemplateATraceData data)
            {
                firedEvents["ReceiveContextAbandonFailed"] = new Dictionary<string, object>
                {
                    { "TypeName", data.TypeName },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.ReceiveContextFaulted += delegate (TwoStringsTemplateSATraceData data)
            {
                firedEvents["ReceiveContextFaulted"] = new Dictionary<string, object>
                {
                    { "EventSource", data.EventSource },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.ReceiveContextAbandonWithException += delegate (Multidata34TemplateATraceData data)
            {
                firedEvents["ReceiveContextAbandonWithException"] = new Dictionary<string, object>
                {
                    { "TypeName", data.TypeName },
                    { "ExceptionToString", data.ExceptionToString },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.ClientBaseCachedChannelFactoryCount += delegate (Multidata35TemplateSATraceData data)
            {
                firedEvents["ClientBaseCachedChannelFactoryCount"] = new Dictionary<string, object>
                {
                    { "Count", data.Count },
                    { "MaxNum", data.MaxNum },
                    { "EventSource", data.EventSource },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.ClientBaseChannelFactoryAgedOutofCache += delegate (Multidata36TemplateSATraceData data)
            {
                firedEvents["ClientBaseChannelFactoryAgedOutofCache"] = new Dictionary<string, object>
                {
                    { "Count", data.Count },
                    { "EventSource", data.EventSource },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.ClientBaseChannelFactoryCacheHit += delegate (TwoStringsTemplateSATraceData data)
            {
                firedEvents["ClientBaseChannelFactoryCacheHit"] = new Dictionary<string, object>
                {
                    { "EventSource", data.EventSource },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.ClientBaseUsingLocalChannelFactory += delegate (TwoStringsTemplateSATraceData data)
            {
                firedEvents["ClientBaseUsingLocalChannelFactory"] = new Dictionary<string, object>
                {
                    { "EventSource", data.EventSource },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.QueryCompositionExecuted += delegate (Multidata37TemplateSATraceData data)
            {
                firedEvents["QueryCompositionExecuted"] = new Dictionary<string, object>
                {
                    { "TypeName", data.TypeName },
                    { "Uri", data.Uri },
                    { "EventSource", data.EventSource },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DispatchFailed += delegate (Multidata38TemplateHATraceData data)
            {
                firedEvents["DispatchFailed"] = new Dictionary<string, object>
                {
                    { "OperationName", data.OperationName },
                    { "HostReference", data.HostReference },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DispatchSuccessful += delegate (Multidata38TemplateHATraceData data)
            {
                firedEvents["DispatchSuccessful"] = new Dictionary<string, object>
                {
                    { "OperationName", data.OperationName },
                    { "HostReference", data.HostReference },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.MessageReadByEncoder += delegate (Multidata39TemplateSATraceData data)
            {
                firedEvents["MessageReadByEncoder"] = new Dictionary<string, object>
                {
                    { "Size", data.Size },
                    { "EventSource", data.EventSource },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.MessageWrittenByEncoder += delegate (Multidata39TemplateSATraceData data)
            {
                firedEvents["MessageWrittenByEncoder"] = new Dictionary<string, object>
                {
                    { "Size", data.Size },
                    { "EventSource", data.EventSource },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.SessionIdleTimeout += delegate (Multidata40TemplateATraceData data)
            {
                firedEvents["SessionIdleTimeout"] = new Dictionary<string, object>
                {
                    { "RemoteAddress", data.RemoteAddress },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.SocketAcceptEnqueued += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["SocketAcceptEnqueued"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.SocketAccepted += delegate (Multidata41TemplateATraceData data)
            {
                firedEvents["SocketAccepted"] = new Dictionary<string, object>
                {
                    { "ListenerHashCode", data.ListenerHashCode },
                    { "SocketHashCode", data.SocketHashCode },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.ConnectionPoolMiss += delegate (Multidata42TemplateATraceData data)
            {
                firedEvents["ConnectionPoolMiss"] = new Dictionary<string, object>
                {
                    { "PoolKey", data.PoolKey },
                    { "busy", data.busy },
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DispatchFormatterDeserializeRequestStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["DispatchFormatterDeserializeRequestStart"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DispatchFormatterDeserializeRequestStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["DispatchFormatterDeserializeRequestStop"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DispatchFormatterSerializeReplyStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["DispatchFormatterSerializeReplyStart"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.DispatchFormatterSerializeReplyStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["DispatchFormatterSerializeReplyStop"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.ClientFormatterSerializeRequestStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["ClientFormatterSerializeRequestStart"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.ClientFormatterSerializeRequestStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["ClientFormatterSerializeRequestStop"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.ClientFormatterDeserializeReplyStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["ClientFormatterDeserializeReplyStart"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.ClientFormatterDeserializeReplyStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["ClientFormatterDeserializeReplyStop"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.SecurityNegotiationStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["SecurityNegotiationStart"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.SecurityNegotiationStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["SecurityNegotiationStop"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            parser.SecurityTokenProviderOpened += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["SecurityTokenProviderOpened"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };
        }

        // =====================================================================
        // Validate_Chunk06 — asserts all 38 events fired with correct payloads.
        // =====================================================================
        private void Validate_Chunk06(Dictionary<string, Dictionary<string, object>> firedEvents)
        {
            // EVENT:2024|InternalCacheMetadataStart|Multidata19TemplateA
            Assert.True(firedEvents.ContainsKey("InternalCacheMetadataStart"), "Event InternalCacheMetadataStart did not fire.");
            Assert.Equal(TestString(2024, "id"), firedEvents["InternalCacheMetadataStart"]["id"]);
            Assert.Equal(TestString(2024, "AppDomain"), firedEvents["InternalCacheMetadataStart"]["AppDomain"]);

            // EVENT:2025|InternalCacheMetadataStop|Multidata19TemplateA
            Assert.True(firedEvents.ContainsKey("InternalCacheMetadataStop"), "Event InternalCacheMetadataStop did not fire.");
            Assert.Equal(TestString(2025, "id"), firedEvents["InternalCacheMetadataStop"]["id"]);
            Assert.Equal(TestString(2025, "AppDomain"), firedEvents["InternalCacheMetadataStop"]["AppDomain"]);

            // EVENT:2026|CompileVbExpressionStart|Multidata20TemplateA
            Assert.True(firedEvents.ContainsKey("CompileVbExpressionStart"), "Event CompileVbExpressionStart did not fire.");
            Assert.Equal(TestString(2026, "expr"), firedEvents["CompileVbExpressionStart"]["expr"]);
            Assert.Equal(TestString(2026, "AppDomain"), firedEvents["CompileVbExpressionStart"]["AppDomain"]);

            // EVENT:2027|CacheRootMetadataStart|Multidata21TemplateA
            Assert.True(firedEvents.ContainsKey("CacheRootMetadataStart"), "Event CacheRootMetadataStart did not fire.");
            Assert.Equal(TestString(2027, "activityName"), firedEvents["CacheRootMetadataStart"]["activityName"]);
            Assert.Equal(TestString(2027, "AppDomain"), firedEvents["CacheRootMetadataStart"]["AppDomain"]);

            // EVENT:2028|CacheRootMetadataStop|Multidata21TemplateA
            Assert.True(firedEvents.ContainsKey("CacheRootMetadataStop"), "Event CacheRootMetadataStop did not fire.");
            Assert.Equal(TestString(2028, "activityName"), firedEvents["CacheRootMetadataStop"]["activityName"]);
            Assert.Equal(TestString(2028, "AppDomain"), firedEvents["CacheRootMetadataStop"]["AppDomain"]);

            // EVENT:2029|CompileVbExpressionStop|OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("CompileVbExpressionStop"), "Event CompileVbExpressionStop did not fire.");
            Assert.Equal(TestString(2029, "AppDomain"), firedEvents["CompileVbExpressionStop"]["AppDomain"]);

            // EVENT:2576|TryCatchExceptionFromTry|ThreeStringsTemplateA
            Assert.True(firedEvents.ContainsKey("TryCatchExceptionFromTry"), "Event TryCatchExceptionFromTry did not fire.");
            Assert.Equal(TestString(2576, "data1"), firedEvents["TryCatchExceptionFromTry"]["data1"]);
            Assert.Equal(TestString(2576, "data2"), firedEvents["TryCatchExceptionFromTry"]["data2"]);
            Assert.Equal(TestString(2576, "AppDomain"), firedEvents["TryCatchExceptionFromTry"]["AppDomain"]);

            // EVENT:2577|TryCatchExceptionDuringCancelation|TwoStringsTemplateA
            Assert.True(firedEvents.ContainsKey("TryCatchExceptionDuringCancelation"), "Event TryCatchExceptionDuringCancelation did not fire.");
            Assert.Equal(TestString(2577, "data1"), firedEvents["TryCatchExceptionDuringCancelation"]["data1"]);
            Assert.Equal(TestString(2577, "AppDomain"), firedEvents["TryCatchExceptionDuringCancelation"]["AppDomain"]);

            // EVENT:2578|TryCatchExceptionFromCatchOrFinally|TwoStringsTemplateA
            Assert.True(firedEvents.ContainsKey("TryCatchExceptionFromCatchOrFinally"), "Event TryCatchExceptionFromCatchOrFinally did not fire.");
            Assert.Equal(TestString(2578, "data1"), firedEvents["TryCatchExceptionFromCatchOrFinally"]["data1"]);
            Assert.Equal(TestString(2578, "AppDomain"), firedEvents["TryCatchExceptionFromCatchOrFinally"]["AppDomain"]);

            // EVENT:3300|ReceiveContextCompleteFailed|Multidata33TemplateA
            Assert.True(firedEvents.ContainsKey("ReceiveContextCompleteFailed"), "Event ReceiveContextCompleteFailed did not fire.");
            Assert.Equal(TestString(3300, "TypeName"), firedEvents["ReceiveContextCompleteFailed"]["TypeName"]);
            Assert.Equal(TestString(3300, "AppDomain"), firedEvents["ReceiveContextCompleteFailed"]["AppDomain"]);

            // EVENT:3301|ReceiveContextAbandonFailed|Multidata33TemplateA
            Assert.True(firedEvents.ContainsKey("ReceiveContextAbandonFailed"), "Event ReceiveContextAbandonFailed did not fire.");
            Assert.Equal(TestString(3301, "TypeName"), firedEvents["ReceiveContextAbandonFailed"]["TypeName"]);
            Assert.Equal(TestString(3301, "AppDomain"), firedEvents["ReceiveContextAbandonFailed"]["AppDomain"]);

            // EVENT:3302|ReceiveContextFaulted|TwoStringsTemplateSA
            Assert.True(firedEvents.ContainsKey("ReceiveContextFaulted"), "Event ReceiveContextFaulted did not fire.");
            Assert.Equal(TestString(3302, "EventSource"), firedEvents["ReceiveContextFaulted"]["EventSource"]);
            Assert.Equal(TestString(3302, "AppDomain"), firedEvents["ReceiveContextFaulted"]["AppDomain"]);

            // EVENT:3303|ReceiveContextAbandonWithException|Multidata34TemplateA
            Assert.True(firedEvents.ContainsKey("ReceiveContextAbandonWithException"), "Event ReceiveContextAbandonWithException did not fire.");
            Assert.Equal(TestString(3303, "TypeName"), firedEvents["ReceiveContextAbandonWithException"]["TypeName"]);
            Assert.Equal(TestString(3303, "ExceptionToString"), firedEvents["ReceiveContextAbandonWithException"]["ExceptionToString"]);
            Assert.Equal(TestString(3303, "AppDomain"), firedEvents["ReceiveContextAbandonWithException"]["AppDomain"]);

            // EVENT:3305|ClientBaseCachedChannelFactoryCount|Multidata35TemplateSA
            Assert.True(firedEvents.ContainsKey("ClientBaseCachedChannelFactoryCount"), "Event ClientBaseCachedChannelFactoryCount did not fire.");
            Assert.Equal(TestInt32(3305, 0), firedEvents["ClientBaseCachedChannelFactoryCount"]["Count"]);
            Assert.Equal(TestInt32(3305, 1), firedEvents["ClientBaseCachedChannelFactoryCount"]["MaxNum"]);
            Assert.Equal(TestString(3305, "EventSource"), firedEvents["ClientBaseCachedChannelFactoryCount"]["EventSource"]);
            Assert.Equal(TestString(3305, "AppDomain"), firedEvents["ClientBaseCachedChannelFactoryCount"]["AppDomain"]);

            // EVENT:3306|ClientBaseChannelFactoryAgedOutofCache|Multidata36TemplateSA
            Assert.True(firedEvents.ContainsKey("ClientBaseChannelFactoryAgedOutofCache"), "Event ClientBaseChannelFactoryAgedOutofCache did not fire.");
            Assert.Equal(TestInt32(3306, 0), firedEvents["ClientBaseChannelFactoryAgedOutofCache"]["Count"]);
            Assert.Equal(TestString(3306, "EventSource"), firedEvents["ClientBaseChannelFactoryAgedOutofCache"]["EventSource"]);
            Assert.Equal(TestString(3306, "AppDomain"), firedEvents["ClientBaseChannelFactoryAgedOutofCache"]["AppDomain"]);

            // EVENT:3307|ClientBaseChannelFactoryCacheHit|TwoStringsTemplateSA
            Assert.True(firedEvents.ContainsKey("ClientBaseChannelFactoryCacheHit"), "Event ClientBaseChannelFactoryCacheHit did not fire.");
            Assert.Equal(TestString(3307, "EventSource"), firedEvents["ClientBaseChannelFactoryCacheHit"]["EventSource"]);
            Assert.Equal(TestString(3307, "AppDomain"), firedEvents["ClientBaseChannelFactoryCacheHit"]["AppDomain"]);

            // EVENT:3308|ClientBaseUsingLocalChannelFactory|TwoStringsTemplateSA
            Assert.True(firedEvents.ContainsKey("ClientBaseUsingLocalChannelFactory"), "Event ClientBaseUsingLocalChannelFactory did not fire.");
            Assert.Equal(TestString(3308, "EventSource"), firedEvents["ClientBaseUsingLocalChannelFactory"]["EventSource"]);
            Assert.Equal(TestString(3308, "AppDomain"), firedEvents["ClientBaseUsingLocalChannelFactory"]["AppDomain"]);

            // EVENT:3309|QueryCompositionExecuted|Multidata37TemplateSA
            Assert.True(firedEvents.ContainsKey("QueryCompositionExecuted"), "Event QueryCompositionExecuted did not fire.");
            Assert.Equal(TestString(3309, "TypeName"), firedEvents["QueryCompositionExecuted"]["TypeName"]);
            Assert.Equal(TestString(3309, "Uri"), firedEvents["QueryCompositionExecuted"]["Uri"]);
            Assert.Equal(TestString(3309, "EventSource"), firedEvents["QueryCompositionExecuted"]["EventSource"]);
            Assert.Equal(TestString(3309, "AppDomain"), firedEvents["QueryCompositionExecuted"]["AppDomain"]);

            // EVENT:3310|DispatchFailed|Multidata38TemplateHA
            Assert.True(firedEvents.ContainsKey("DispatchFailed"), "Event DispatchFailed did not fire.");
            Assert.Equal(TestString(3310, "OperationName"), firedEvents["DispatchFailed"]["OperationName"]);
            Assert.Equal(TestString(3310, "HostReference"), firedEvents["DispatchFailed"]["HostReference"]);
            Assert.Equal(TestString(3310, "AppDomain"), firedEvents["DispatchFailed"]["AppDomain"]);

            // EVENT:3311|DispatchSuccessful|Multidata38TemplateHA
            Assert.True(firedEvents.ContainsKey("DispatchSuccessful"), "Event DispatchSuccessful did not fire.");
            Assert.Equal(TestString(3311, "OperationName"), firedEvents["DispatchSuccessful"]["OperationName"]);
            Assert.Equal(TestString(3311, "HostReference"), firedEvents["DispatchSuccessful"]["HostReference"]);
            Assert.Equal(TestString(3311, "AppDomain"), firedEvents["DispatchSuccessful"]["AppDomain"]);

            // EVENT:3312|MessageReadByEncoder|Multidata39TemplateSA
            Assert.True(firedEvents.ContainsKey("MessageReadByEncoder"), "Event MessageReadByEncoder did not fire.");
            Assert.Equal(TestInt32(3312, 0), firedEvents["MessageReadByEncoder"]["Size"]);
            Assert.Equal(TestString(3312, "EventSource"), firedEvents["MessageReadByEncoder"]["EventSource"]);
            Assert.Equal(TestString(3312, "AppDomain"), firedEvents["MessageReadByEncoder"]["AppDomain"]);

            // EVENT:3313|MessageWrittenByEncoder|Multidata39TemplateSA
            Assert.True(firedEvents.ContainsKey("MessageWrittenByEncoder"), "Event MessageWrittenByEncoder did not fire.");
            Assert.Equal(TestInt32(3313, 0), firedEvents["MessageWrittenByEncoder"]["Size"]);
            Assert.Equal(TestString(3313, "EventSource"), firedEvents["MessageWrittenByEncoder"]["EventSource"]);
            Assert.Equal(TestString(3313, "AppDomain"), firedEvents["MessageWrittenByEncoder"]["AppDomain"]);

            // EVENT:3314|SessionIdleTimeout|Multidata40TemplateA
            Assert.True(firedEvents.ContainsKey("SessionIdleTimeout"), "Event SessionIdleTimeout did not fire.");
            Assert.Equal(TestString(3314, "RemoteAddress"), firedEvents["SessionIdleTimeout"]["RemoteAddress"]);
            Assert.Equal(TestString(3314, "AppDomain"), firedEvents["SessionIdleTimeout"]["AppDomain"]);

            // EVENT:3319|SocketAcceptEnqueued|OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("SocketAcceptEnqueued"), "Event SocketAcceptEnqueued did not fire.");
            Assert.Equal(TestString(3319, "AppDomain"), firedEvents["SocketAcceptEnqueued"]["AppDomain"]);

            // EVENT:3320|SocketAccepted|Multidata41TemplateA
            Assert.True(firedEvents.ContainsKey("SocketAccepted"), "Event SocketAccepted did not fire.");
            Assert.Equal(TestInt32(3320, 0), firedEvents["SocketAccepted"]["ListenerHashCode"]);
            Assert.Equal(TestInt32(3320, 1), firedEvents["SocketAccepted"]["SocketHashCode"]);
            Assert.Equal(TestString(3320, "AppDomain"), firedEvents["SocketAccepted"]["AppDomain"]);

            // EVENT:3321|ConnectionPoolMiss|Multidata42TemplateA
            Assert.True(firedEvents.ContainsKey("ConnectionPoolMiss"), "Event ConnectionPoolMiss did not fire.");
            Assert.Equal(TestString(3321, "PoolKey"), firedEvents["ConnectionPoolMiss"]["PoolKey"]);
            Assert.Equal(TestInt32(3321, 1), firedEvents["ConnectionPoolMiss"]["busy"]);
            Assert.Equal(TestString(3321, "AppDomain"), firedEvents["ConnectionPoolMiss"]["AppDomain"]);

            // EVENT:3322|DispatchFormatterDeserializeRequestStart|OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("DispatchFormatterDeserializeRequestStart"), "Event DispatchFormatterDeserializeRequestStart did not fire.");
            Assert.Equal(TestString(3322, "AppDomain"), firedEvents["DispatchFormatterDeserializeRequestStart"]["AppDomain"]);

            // EVENT:3323|DispatchFormatterDeserializeRequestStop|OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("DispatchFormatterDeserializeRequestStop"), "Event DispatchFormatterDeserializeRequestStop did not fire.");
            Assert.Equal(TestString(3323, "AppDomain"), firedEvents["DispatchFormatterDeserializeRequestStop"]["AppDomain"]);

            // EVENT:3324|DispatchFormatterSerializeReplyStart|OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("DispatchFormatterSerializeReplyStart"), "Event DispatchFormatterSerializeReplyStart did not fire.");
            Assert.Equal(TestString(3324, "AppDomain"), firedEvents["DispatchFormatterSerializeReplyStart"]["AppDomain"]);

            // EVENT:3325|DispatchFormatterSerializeReplyStop|OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("DispatchFormatterSerializeReplyStop"), "Event DispatchFormatterSerializeReplyStop did not fire.");
            Assert.Equal(TestString(3325, "AppDomain"), firedEvents["DispatchFormatterSerializeReplyStop"]["AppDomain"]);

            // EVENT:3326|ClientFormatterSerializeRequestStart|OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("ClientFormatterSerializeRequestStart"), "Event ClientFormatterSerializeRequestStart did not fire.");
            Assert.Equal(TestString(3326, "AppDomain"), firedEvents["ClientFormatterSerializeRequestStart"]["AppDomain"]);

            // EVENT:3327|ClientFormatterSerializeRequestStop|OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("ClientFormatterSerializeRequestStop"), "Event ClientFormatterSerializeRequestStop did not fire.");
            Assert.Equal(TestString(3327, "AppDomain"), firedEvents["ClientFormatterSerializeRequestStop"]["AppDomain"]);

            // EVENT:3328|ClientFormatterDeserializeReplyStart|OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("ClientFormatterDeserializeReplyStart"), "Event ClientFormatterDeserializeReplyStart did not fire.");
            Assert.Equal(TestString(3328, "AppDomain"), firedEvents["ClientFormatterDeserializeReplyStart"]["AppDomain"]);

            // EVENT:3329|ClientFormatterDeserializeReplyStop|OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("ClientFormatterDeserializeReplyStop"), "Event ClientFormatterDeserializeReplyStop did not fire.");
            Assert.Equal(TestString(3329, "AppDomain"), firedEvents["ClientFormatterDeserializeReplyStop"]["AppDomain"]);

            // EVENT:3330|SecurityNegotiationStart|OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("SecurityNegotiationStart"), "Event SecurityNegotiationStart did not fire.");
            Assert.Equal(TestString(3330, "AppDomain"), firedEvents["SecurityNegotiationStart"]["AppDomain"]);

            // EVENT:3331|SecurityNegotiationStop|OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("SecurityNegotiationStop"), "Event SecurityNegotiationStop did not fire.");
            Assert.Equal(TestString(3331, "AppDomain"), firedEvents["SecurityNegotiationStop"]["AppDomain"]);

            // EVENT:3332|SecurityTokenProviderOpened|OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("SecurityTokenProviderOpened"), "Event SecurityTokenProviderOpened did not fire.");
            Assert.Equal(TestString(3332, "AppDomain"), firedEvents["SecurityTokenProviderOpened"]["AppDomain"]);
        }
    }
}
