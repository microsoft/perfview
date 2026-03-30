using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.ApplicationServer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace TraceEventTests.Parsers
{
    public partial class ApplicationServerTraceEventParserTests
    {
        // Template: OneStringsTemplateA — AppDomain:string
        private static readonly TemplateField[] Chunk08_OneStringsTemplateAFields = new TemplateField[]
        {
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        // Template: Multidata51TemplateA — SocketId:int32, Size:int32, Endpoint:string, AppDomain:string
        private static readonly TemplateField[] Multidata51TemplateAFields = new TemplateField[]
        {
            new TemplateField("SocketId", FieldType.Int32),
            new TemplateField("Size", FieldType.Int32),
            new TemplateField("Endpoint", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        // Template: Multidata52TemplateA — SessionId:string, AppDomain:string
        private static readonly TemplateField[] Multidata52TemplateAFields = new TemplateField[]
        {
            new TemplateField("SessionId", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        // Template: Multidata53TemplateA — SocketId:int32, AppDomain:string
        private static readonly TemplateField[] Multidata53TemplateAFields = new TemplateField[]
        {
            new TemplateField("SocketId", FieldType.Int32),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        // Template: Multidata54TemplateA — LocalId:string, Distributed:guid, AppDomain:string
        private static readonly TemplateField[] Multidata54TemplateAFields = new TemplateField[]
        {
            new TemplateField("LocalId", FieldType.UnicodeString),
            new TemplateField("Distributed", FieldType.Guid),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        // Template: Multidata55TemplateA — BufferId:int32, Size:int32, AppDomain:string
        private static readonly TemplateField[] Multidata55TemplateAFields = new TemplateField[]
        {
            new TemplateField("BufferId", FieldType.Int32),
            new TemplateField("Size", FieldType.Int32),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        // Template: Multidata60TemplateA — sharedMemoryName:string, AppDomain:string
        private static readonly TemplateField[] Multidata60TemplateAFields = new TemplateField[]
        {
            new TemplateField("sharedMemoryName", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        // Template: Multidata61TemplateA — pipeName:string, AppDomain:string
        private static readonly TemplateField[] Multidata61TemplateAFields = new TemplateField[]
        {
            new TemplateField("pipeName", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        /// <summary>
        /// Writes EventPipe metadata entries for chunk 08 events (IDs 3371–3409).
        /// </summary>
        private void WriteMetadata_Chunk08(EventPipeWriterV5 writer, ref int metadataId)
        {
            // 3371 MtomMessageEncodingStart — OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "MtomMessageEncodingStart", 3371,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3372 TextMessageEncodingStart — OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "TextMessageEncodingStart", 3372,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3373 BinaryMessageDecodingStart — OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "BinaryMessageDecodingStart", 3373,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3374 MtomMessageDecodingStart — OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "MtomMessageDecodingStart", 3374,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3375 TextMessageDecodingStart — OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "TextMessageDecodingStart", 3375,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3376 HttpResponseReceiveStart — OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "HttpResponseReceiveStart", 3376,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3377 SocketReadStop — Multidata51TemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SocketReadStop", 3377,
                new MetadataParameter("SocketId", MetadataTypeCode.Int32),
                new MetadataParameter("Size", MetadataTypeCode.Int32),
                new MetadataParameter("Endpoint", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3378 SocketAsyncReadStop — Multidata51TemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SocketAsyncReadStop", 3378,
                new MetadataParameter("SocketId", MetadataTypeCode.Int32),
                new MetadataParameter("Size", MetadataTypeCode.Int32),
                new MetadataParameter("Endpoint", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3379 SocketWriteStart — Multidata51TemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SocketWriteStart", 3379,
                new MetadataParameter("SocketId", MetadataTypeCode.Int32),
                new MetadataParameter("Size", MetadataTypeCode.Int32),
                new MetadataParameter("Endpoint", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3380 SocketAsyncWriteStart — Multidata51TemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SocketAsyncWriteStart", 3380,
                new MetadataParameter("SocketId", MetadataTypeCode.Int32),
                new MetadataParameter("Size", MetadataTypeCode.Int32),
                new MetadataParameter("Endpoint", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3381 SequenceAcknowledgementSent — Multidata52TemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SequenceAcknowledgementSent", 3381,
                new MetadataParameter("SessionId", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3382 ClientReliableSessionReconnect — Multidata52TemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ClientReliableSessionReconnect", 3382,
                new MetadataParameter("SessionId", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3383 ReliableSessionChannelFaulted — Multidata52TemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ReliableSessionChannelFaulted", 3383,
                new MetadataParameter("SessionId", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3384 WindowsStreamSecurityOnInitiateUpgrade — OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "WindowsStreamSecurityOnInitiateUpgrade", 3384,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3385 WindowsStreamSecurityOnAcceptUpgrade — OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "WindowsStreamSecurityOnAcceptUpgrade", 3385,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3386 SocketConnectionAbort — Multidata53TemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SocketConnectionAbort", 3386,
                new MetadataParameter("SocketId", MetadataTypeCode.Int32),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3388 HttpGetContextStart — OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "HttpGetContextStart", 3388,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3389 ClientSendPreambleStart — OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ClientSendPreambleStart", 3389,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3390 ClientSendPreambleStop — OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ClientSendPreambleStop", 3390,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3391 HttpMessageReceiveFailed — OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "HttpMessageReceiveFailed", 3391,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3392 TransactionScopeCreate — Multidata54TemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "TransactionScopeCreate", 3392,
                new MetadataParameter("LocalId", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("Distributed", MetadataTypeCode.Guid),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3393 StreamedMessageReadByEncoder — OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "StreamedMessageReadByEncoder", 3393,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3394 StreamedMessageWrittenByEncoder — OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "StreamedMessageWrittenByEncoder", 3394,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3395 MessageWrittenAsynchronouslyByEncoder — OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "MessageWrittenAsynchronouslyByEncoder", 3395,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3396 BufferedAsyncWriteStart — Multidata55TemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "BufferedAsyncWriteStart", 3396,
                new MetadataParameter("BufferId", MetadataTypeCode.Int32),
                new MetadataParameter("Size", MetadataTypeCode.Int32),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3397 BufferedAsyncWriteStop — OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "BufferedAsyncWriteStop", 3397,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3398 PipeSharedMemoryCreated — Multidata60TemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "PipeSharedMemoryCreated", 3398,
                new MetadataParameter("sharedMemoryName", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3399 NamedPipeCreated — Multidata61TemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "NamedPipeCreated", 3399,
                new MetadataParameter("pipeName", MetadataTypeCode.NullTerminatedUTF16String),
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3401 SignatureVerificationStart — OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SignatureVerificationStart", 3401,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3402 SignatureVerificationSuccess — OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SignatureVerificationSuccess", 3402,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3403 WrappedKeyDecryptionStart — OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "WrappedKeyDecryptionStart", 3403,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3404 WrappedKeyDecryptionSuccess — OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "WrappedKeyDecryptionSuccess", 3404,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3405 EncryptedDataProcessingStart — OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "EncryptedDataProcessingStart", 3405,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3406 EncryptedDataProcessingSuccess — OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "EncryptedDataProcessingSuccess", 3406,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3407 HttpPipelineProcessInboundRequestStart — OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "HttpPipelineProcessInboundRequestStart", 3407,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3408 HttpPipelineBeginProcessInboundRequestStart — OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "HttpPipelineBeginProcessInboundRequestStart", 3408,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));

            // 3409 HttpPipelineProcessInboundRequestStop — OneStringsTemplateA
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "HttpPipelineProcessInboundRequestStop", 3409,
                new MetadataParameter("AppDomain", MetadataTypeCode.NullTerminatedUTF16String)));
        }

        /// <summary>
        /// Writes event payloads for chunk 08 events into an EventBlock.
        /// </summary>
        private void WriteEvents_Chunk08(EventPipeWriterV5 writer, ref int metadataId, ref int sequenceNumber)
        {
            int __metadataId = metadataId;
            int __sequenceNumber = sequenceNumber;

            writer.WriteEventBlock(w =>
            {
                // 3371 MtomMessageEncodingStart — OneStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3371, Chunk08_OneStringsTemplateAFields));
                // 3372 TextMessageEncodingStart — OneStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3372, Chunk08_OneStringsTemplateAFields));
                // 3373 BinaryMessageDecodingStart — OneStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3373, Chunk08_OneStringsTemplateAFields));
                // 3374 MtomMessageDecodingStart — OneStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3374, Chunk08_OneStringsTemplateAFields));
                // 3375 TextMessageDecodingStart — OneStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3375, Chunk08_OneStringsTemplateAFields));
                // 3376 HttpResponseReceiveStart — OneStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3376, Chunk08_OneStringsTemplateAFields));
                // 3377 SocketReadStop — Multidata51TemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3377, Multidata51TemplateAFields));
                // 3378 SocketAsyncReadStop — Multidata51TemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3378, Multidata51TemplateAFields));
                // 3379 SocketWriteStart — Multidata51TemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3379, Multidata51TemplateAFields));
                // 3380 SocketAsyncWriteStart — Multidata51TemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3380, Multidata51TemplateAFields));
                // 3381 SequenceAcknowledgementSent — Multidata52TemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3381, Multidata52TemplateAFields));
                // 3382 ClientReliableSessionReconnect — Multidata52TemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3382, Multidata52TemplateAFields));
                // 3383 ReliableSessionChannelFaulted — Multidata52TemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3383, Multidata52TemplateAFields));
                // 3384 WindowsStreamSecurityOnInitiateUpgrade — OneStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3384, Chunk08_OneStringsTemplateAFields));
                // 3385 WindowsStreamSecurityOnAcceptUpgrade — OneStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3385, Chunk08_OneStringsTemplateAFields));
                // 3386 SocketConnectionAbort — Multidata53TemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3386, Multidata53TemplateAFields));
                // 3388 HttpGetContextStart — OneStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3388, Chunk08_OneStringsTemplateAFields));
                // 3389 ClientSendPreambleStart — OneStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3389, Chunk08_OneStringsTemplateAFields));
                // 3390 ClientSendPreambleStop — OneStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3390, Chunk08_OneStringsTemplateAFields));
                // 3391 HttpMessageReceiveFailed — OneStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3391, Chunk08_OneStringsTemplateAFields));
                // 3392 TransactionScopeCreate — Multidata54TemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3392, Multidata54TemplateAFields));
                // 3393 StreamedMessageReadByEncoder — OneStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3393, Chunk08_OneStringsTemplateAFields));
                // 3394 StreamedMessageWrittenByEncoder — OneStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3394, Chunk08_OneStringsTemplateAFields));
                // 3395 MessageWrittenAsynchronouslyByEncoder — OneStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3395, Chunk08_OneStringsTemplateAFields));
                // 3396 BufferedAsyncWriteStart — Multidata55TemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3396, Multidata55TemplateAFields));
                // 3397 BufferedAsyncWriteStop — OneStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3397, Chunk08_OneStringsTemplateAFields));
                // 3398 PipeSharedMemoryCreated — Multidata60TemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3398, Multidata60TemplateAFields));
                // 3399 NamedPipeCreated — Multidata61TemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3399, Multidata61TemplateAFields));
                // 3401 SignatureVerificationStart — OneStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3401, Chunk08_OneStringsTemplateAFields));
                // 3402 SignatureVerificationSuccess — OneStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3402, Chunk08_OneStringsTemplateAFields));
                // 3403 WrappedKeyDecryptionStart — OneStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3403, Chunk08_OneStringsTemplateAFields));
                // 3404 WrappedKeyDecryptionSuccess — OneStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3404, Chunk08_OneStringsTemplateAFields));
                // 3405 EncryptedDataProcessingStart — OneStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3405, Chunk08_OneStringsTemplateAFields));
                // 3406 EncryptedDataProcessingSuccess — OneStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3406, Chunk08_OneStringsTemplateAFields));
                // 3407 HttpPipelineProcessInboundRequestStart — OneStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3407, Chunk08_OneStringsTemplateAFields));
                // 3408 HttpPipelineBeginProcessInboundRequestStart — OneStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3408, Chunk08_OneStringsTemplateAFields));
                // 3409 HttpPipelineProcessInboundRequestStop — OneStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3409, Chunk08_OneStringsTemplateAFields));
            });

        
            metadataId = __metadataId;
            sequenceNumber = __sequenceNumber;
        }

        /// <summary>
        /// Subscribes to all chunk 08 events and records their payloads.
        /// </summary>
        private void Subscribe_Chunk08(ApplicationServerTraceEventParser parser, Dictionary<string, Dictionary<string, object>> firedEvents)
        {
            parser.MtomMessageEncodingStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["MtomMessageEncodingStart"] = new Dictionary<string, object> { { "AppDomain", data.AppDomain } };
            };
            parser.TextMessageEncodingStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["TextMessageEncodingStart"] = new Dictionary<string, object> { { "AppDomain", data.AppDomain } };
            };
            parser.BinaryMessageDecodingStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["BinaryMessageDecodingStart"] = new Dictionary<string, object> { { "AppDomain", data.AppDomain } };
            };
            parser.MtomMessageDecodingStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["MtomMessageDecodingStart"] = new Dictionary<string, object> { { "AppDomain", data.AppDomain } };
            };
            parser.TextMessageDecodingStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["TextMessageDecodingStart"] = new Dictionary<string, object> { { "AppDomain", data.AppDomain } };
            };
            parser.HttpResponseReceiveStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["HttpResponseReceiveStart"] = new Dictionary<string, object> { { "AppDomain", data.AppDomain } };
            };
            parser.SocketReadStop += delegate (Multidata51TemplateATraceData data)
            {
                firedEvents["SocketReadStop"] = new Dictionary<string, object>
                {
                    { "SocketId", data.SocketId },
                    { "Size", data.Size },
                    { "Endpoint", data.Endpoint },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.SocketAsyncReadStop += delegate (Multidata51TemplateATraceData data)
            {
                firedEvents["SocketAsyncReadStop"] = new Dictionary<string, object>
                {
                    { "SocketId", data.SocketId },
                    { "Size", data.Size },
                    { "Endpoint", data.Endpoint },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.SocketWriteStart += delegate (Multidata51TemplateATraceData data)
            {
                firedEvents["SocketWriteStart"] = new Dictionary<string, object>
                {
                    { "SocketId", data.SocketId },
                    { "Size", data.Size },
                    { "Endpoint", data.Endpoint },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.SocketAsyncWriteStart += delegate (Multidata51TemplateATraceData data)
            {
                firedEvents["SocketAsyncWriteStart"] = new Dictionary<string, object>
                {
                    { "SocketId", data.SocketId },
                    { "Size", data.Size },
                    { "Endpoint", data.Endpoint },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.SequenceAcknowledgementSent += delegate (Multidata52TemplateATraceData data)
            {
                firedEvents["SequenceAcknowledgementSent"] = new Dictionary<string, object>
                {
                    { "SessionId", data.SessionId },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.ClientReliableSessionReconnect += delegate (Multidata52TemplateATraceData data)
            {
                firedEvents["ClientReliableSessionReconnect"] = new Dictionary<string, object>
                {
                    { "SessionId", data.SessionId },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.ReliableSessionChannelFaulted += delegate (Multidata52TemplateATraceData data)
            {
                firedEvents["ReliableSessionChannelFaulted"] = new Dictionary<string, object>
                {
                    { "SessionId", data.SessionId },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.WindowsStreamSecurityOnInitiateUpgrade += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["WindowsStreamSecurityOnInitiateUpgrade"] = new Dictionary<string, object> { { "AppDomain", data.AppDomain } };
            };
            parser.WindowsStreamSecurityOnAcceptUpgrade += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["WindowsStreamSecurityOnAcceptUpgrade"] = new Dictionary<string, object> { { "AppDomain", data.AppDomain } };
            };
            parser.SocketConnectionAbort += delegate (Multidata53TemplateATraceData data)
            {
                firedEvents["SocketConnectionAbort"] = new Dictionary<string, object>
                {
                    { "SocketId", data.SocketId },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.HttpGetContextStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["HttpGetContextStart"] = new Dictionary<string, object> { { "AppDomain", data.AppDomain } };
            };
            parser.ClientSendPreambleStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["ClientSendPreambleStart"] = new Dictionary<string, object> { { "AppDomain", data.AppDomain } };
            };
            parser.ClientSendPreambleStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["ClientSendPreambleStop"] = new Dictionary<string, object> { { "AppDomain", data.AppDomain } };
            };
            parser.HttpMessageReceiveFailed += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["HttpMessageReceiveFailed"] = new Dictionary<string, object> { { "AppDomain", data.AppDomain } };
            };
            parser.TransactionScopeCreate += delegate (Multidata54TemplateATraceData data)
            {
                firedEvents["TransactionScopeCreate"] = new Dictionary<string, object>
                {
                    { "LocalId", data.LocalId },
                    { "Distributed", data.Distributed },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.StreamedMessageReadByEncoder += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["StreamedMessageReadByEncoder"] = new Dictionary<string, object> { { "AppDomain", data.AppDomain } };
            };
            parser.StreamedMessageWrittenByEncoder += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["StreamedMessageWrittenByEncoder"] = new Dictionary<string, object> { { "AppDomain", data.AppDomain } };
            };
            parser.MessageWrittenAsynchronouslyByEncoder += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["MessageWrittenAsynchronouslyByEncoder"] = new Dictionary<string, object> { { "AppDomain", data.AppDomain } };
            };
            parser.BufferedAsyncWriteStart += delegate (Multidata55TemplateATraceData data)
            {
                firedEvents["BufferedAsyncWriteStart"] = new Dictionary<string, object>
                {
                    { "BufferId", data.BufferId },
                    { "Size", data.Size },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.BufferedAsyncWriteStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["BufferedAsyncWriteStop"] = new Dictionary<string, object> { { "AppDomain", data.AppDomain } };
            };
            parser.PipeSharedMemoryCreated += delegate (Multidata60TemplateATraceData data)
            {
                firedEvents["PipeSharedMemoryCreated"] = new Dictionary<string, object>
                {
                    { "sharedMemoryName", data.sharedMemoryName },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.NamedPipeCreated += delegate (Multidata61TemplateATraceData data)
            {
                firedEvents["NamedPipeCreated"] = new Dictionary<string, object>
                {
                    { "pipeName", data.pipeName },
                    { "AppDomain", data.AppDomain },
                };
            };
            parser.SignatureVerificationStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["SignatureVerificationStart"] = new Dictionary<string, object> { { "AppDomain", data.AppDomain } };
            };
            parser.SignatureVerificationSuccess += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["SignatureVerificationSuccess"] = new Dictionary<string, object> { { "AppDomain", data.AppDomain } };
            };
            parser.WrappedKeyDecryptionStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["WrappedKeyDecryptionStart"] = new Dictionary<string, object> { { "AppDomain", data.AppDomain } };
            };
            parser.WrappedKeyDecryptionSuccess += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["WrappedKeyDecryptionSuccess"] = new Dictionary<string, object> { { "AppDomain", data.AppDomain } };
            };
            parser.EncryptedDataProcessingStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["EncryptedDataProcessingStart"] = new Dictionary<string, object> { { "AppDomain", data.AppDomain } };
            };
            parser.EncryptedDataProcessingSuccess += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["EncryptedDataProcessingSuccess"] = new Dictionary<string, object> { { "AppDomain", data.AppDomain } };
            };
            parser.HttpPipelineProcessInboundRequestStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["HttpPipelineProcessInboundRequestStart"] = new Dictionary<string, object> { { "AppDomain", data.AppDomain } };
            };
            parser.HttpPipelineBeginProcessInboundRequestStart += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["HttpPipelineBeginProcessInboundRequestStart"] = new Dictionary<string, object> { { "AppDomain", data.AppDomain } };
            };
            parser.HttpPipelineProcessInboundRequestStop += delegate (OneStringsTemplateATraceData data)
            {
                firedEvents["HttpPipelineProcessInboundRequestStop"] = new Dictionary<string, object> { { "AppDomain", data.AppDomain } };
            };
        }

        /// <summary>
        /// Validates all chunk 08 events fired with the expected payload values.
        /// </summary>
        private void Validate_Chunk08(Dictionary<string, Dictionary<string, object>> firedEvents)
        {
            // Helper to validate OneStringsTemplateA events
            void ValidateOneString(int eventId, string eventName)
            {
                Assert.True(firedEvents.ContainsKey(eventName), $"Event {eventName} did not fire.");
                var fields = firedEvents[eventName];
                Assert.Equal(TestString(eventId, "AppDomain"), (string)fields["AppDomain"]);
            }

            // Helper to validate Multidata51TemplateA events (SocketId, Size, Endpoint, AppDomain)
            void ValidateMultidata51(int eventId, string eventName)
            {
                Assert.True(firedEvents.ContainsKey(eventName), $"Event {eventName} did not fire.");
                var fields = firedEvents[eventName];
                Assert.Equal(TestInt32(eventId, 0), (int)fields["SocketId"]);
                Assert.Equal(TestInt32(eventId, 1), (int)fields["Size"]);
                Assert.Equal(TestString(eventId, "Endpoint"), (string)fields["Endpoint"]);
                Assert.Equal(TestString(eventId, "AppDomain"), (string)fields["AppDomain"]);
            }

            // Helper to validate Multidata52TemplateA events (SessionId, AppDomain)
            void ValidateMultidata52(int eventId, string eventName)
            {
                Assert.True(firedEvents.ContainsKey(eventName), $"Event {eventName} did not fire.");
                var fields = firedEvents[eventName];
                Assert.Equal(TestString(eventId, "SessionId"), (string)fields["SessionId"]);
                Assert.Equal(TestString(eventId, "AppDomain"), (string)fields["AppDomain"]);
            }

            // Helper to validate Multidata53TemplateA events (SocketId, AppDomain)
            void ValidateMultidata53(int eventId, string eventName)
            {
                Assert.True(firedEvents.ContainsKey(eventName), $"Event {eventName} did not fire.");
                var fields = firedEvents[eventName];
                Assert.Equal(TestInt32(eventId, 0), (int)fields["SocketId"]);
                Assert.Equal(TestString(eventId, "AppDomain"), (string)fields["AppDomain"]);
            }

            // 3371–3376: OneStringsTemplateA events
            ValidateOneString(3371, "MtomMessageEncodingStart"); // MtomMessageEncodingStart
            ValidateOneString(3372, "TextMessageEncodingStart"); // TextMessageEncodingStart
            ValidateOneString(3373, "BinaryMessageDecodingStart"); // BinaryMessageDecodingStart
            ValidateOneString(3374, "MtomMessageDecodingStart"); // MtomMessageDecodingStart
            ValidateOneString(3375, "TextMessageDecodingStart"); // TextMessageDecodingStart
            ValidateOneString(3376, "HttpResponseReceiveStart"); // HttpResponseReceiveStart

            // 3377–3380: Multidata51TemplateA events
            ValidateMultidata51(3377, "SocketReadStop"); // SocketReadStop
            ValidateMultidata51(3378, "SocketAsyncReadStop"); // SocketAsyncReadStop
            ValidateMultidata51(3379, "SocketWriteStart"); // SocketWriteStart
            ValidateMultidata51(3380, "SocketAsyncWriteStart"); // SocketAsyncWriteStart

            // 3381–3383: Multidata52TemplateA events
            ValidateMultidata52(3381, "SequenceAcknowledgementSent"); // SequenceAcknowledgementSent
            ValidateMultidata52(3382, "ClientReliableSessionReconnect"); // ClientReliableSessionReconnect
            ValidateMultidata52(3383, "ReliableSessionChannelFaulted"); // ReliableSessionChannelFaulted

            // 3384–3385: OneStringsTemplateA events
            ValidateOneString(3384, "WindowsStreamSecurityOnInitiateUpgrade"); // WindowsStreamSecurityOnInitiateUpgrade
            ValidateOneString(3385, "WindowsStreamSecurityOnAcceptUpgrade"); // WindowsStreamSecurityOnAcceptUpgrade

            // Multidata53TemplateA
            ValidateMultidata53(3386, "SocketConnectionAbort"); // SocketConnectionAbort

            // 3388–3391: OneStringsTemplateA events
            ValidateOneString(3388, "HttpGetContextStart"); // HttpGetContextStart
            ValidateOneString(3389, "ClientSendPreambleStart"); // ClientSendPreambleStart
            ValidateOneString(3390, "ClientSendPreambleStop"); // ClientSendPreambleStop
            ValidateOneString(3391, "HttpMessageReceiveFailed"); // HttpMessageReceiveFailed

            // Multidata54TemplateA (LocalId, Distributed, AppDomain)
            {
                int eventId = 3392;
                string eventName = "TransactionScopeCreate";
                Assert.True(firedEvents.ContainsKey(eventName), $"Event {eventName} did not fire.");
                var fields = firedEvents[eventName];
                Assert.Equal(TestString(eventId, "LocalId"), (string)fields["LocalId"]);
                Assert.Equal(TestGuid(eventId, 1), (Guid)fields["Distributed"]);
                Assert.Equal(TestString(eventId, "AppDomain"), (string)fields["AppDomain"]);
            }

            // 3393–3395: OneStringsTemplateA events
            ValidateOneString(3393, "StreamedMessageReadByEncoder"); // StreamedMessageReadByEncoder
            ValidateOneString(3394, "StreamedMessageWrittenByEncoder"); // StreamedMessageWrittenByEncoder
            ValidateOneString(3395, "MessageWrittenAsynchronouslyByEncoder"); // MessageWrittenAsynchronouslyByEncoder

            // Multidata55TemplateA (BufferId, Size, AppDomain)
            {
                int eventId = 3396;
                string eventName = "BufferedAsyncWriteStart";
                Assert.True(firedEvents.ContainsKey(eventName), $"Event {eventName} did not fire.");
                var fields = firedEvents[eventName];
                Assert.Equal(TestInt32(eventId, 0), (int)fields["BufferId"]);
                Assert.Equal(TestInt32(eventId, 1), (int)fields["Size"]);
                Assert.Equal(TestString(eventId, "AppDomain"), (string)fields["AppDomain"]);
            }

            // OneStringsTemplateA
            ValidateOneString(3397, "BufferedAsyncWriteStop"); // BufferedAsyncWriteStop

            // Multidata60TemplateA (sharedMemoryName, AppDomain)
            {
                int eventId = 3398;
                string eventName = "PipeSharedMemoryCreated";
                Assert.True(firedEvents.ContainsKey(eventName), $"Event {eventName} did not fire.");
                var fields = firedEvents[eventName];
                Assert.Equal(TestString(eventId, "sharedMemoryName"), (string)fields["sharedMemoryName"]);
                Assert.Equal(TestString(eventId, "AppDomain"), (string)fields["AppDomain"]);
            }

            // Multidata61TemplateA (pipeName, AppDomain)
            {
                int eventId = 3399;
                string eventName = "NamedPipeCreated";
                Assert.True(firedEvents.ContainsKey(eventName), $"Event {eventName} did not fire.");
                var fields = firedEvents[eventName];
                Assert.Equal(TestString(eventId, "pipeName"), (string)fields["pipeName"]);
                Assert.Equal(TestString(eventId, "AppDomain"), (string)fields["AppDomain"]);
            }

            // 3401–3406: OneStringsTemplateA events
            ValidateOneString(3401, "SignatureVerificationStart"); // SignatureVerificationStart
            ValidateOneString(3402, "SignatureVerificationSuccess"); // SignatureVerificationSuccess
            ValidateOneString(3403, "WrappedKeyDecryptionStart"); // WrappedKeyDecryptionStart
            ValidateOneString(3404, "WrappedKeyDecryptionSuccess"); // WrappedKeyDecryptionSuccess
            ValidateOneString(3405, "EncryptedDataProcessingStart"); // EncryptedDataProcessingStart
            ValidateOneString(3406, "EncryptedDataProcessingSuccess"); // EncryptedDataProcessingSuccess

            // 3407–3409: OneStringsTemplateA events
            ValidateOneString(3407, "HttpPipelineProcessInboundRequestStart"); // HttpPipelineProcessInboundRequestStart
            ValidateOneString(3408, "HttpPipelineBeginProcessInboundRequestStart"); // HttpPipelineBeginProcessInboundRequestStart
            ValidateOneString(3409, "HttpPipelineProcessInboundRequestStop"); // HttpPipelineProcessInboundRequestStop
        }
    }
}
