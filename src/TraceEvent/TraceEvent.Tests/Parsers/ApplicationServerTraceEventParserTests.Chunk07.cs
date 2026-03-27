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
        // Template field definitions for Chunk 07

        private static readonly TemplateField[] Multidata43TemplateAFields = new TemplateField[]
        {
            new TemplateField("ChannelId", FieldType.Int32),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata44TemplateAFields = new TemplateField[]
        {
            new TemplateField("uri", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata45TemplateAFields = new TemplateField[]
        {
            new TemplateField("Key", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata46TemplateAFields = new TemplateField[]
        {
            new TemplateField("Via", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata47TemplateAFields = new TemplateField[]
        {
            new TemplateField("FaultString", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata48TemplateAFields = new TemplateField[]
        {
            new TemplateField("Uri", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata49TemplateAFields = new TemplateField[]
        {
            new TemplateField("SocketId", FieldType.Int32),
            new TemplateField("Uri", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata50TemplateAFields = new TemplateField[]
        {
            new TemplateField("Status", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Chunk07_OneStringsTemplateAFields = new TemplateField[]
        {
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Chunk07_TwoStringsTemplateSAFields = new TemplateField[]
        {
            new TemplateField("EventSource", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        /// <summary>
        /// Writes metadata entries for chunk 07 events (IDs 3333–3370).
        /// Each event gets a unique metadataId. The opcode is written when non-zero.
        /// </summary>
        private void WriteMetadata_Chunk07(EventPipeWriterV5 writer, ref int metadataId)
        {
            // 3333 OutgoingMessageSecured – OneStringsTemplateA, opcode=2
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "OutgoingMessageSecured", 3333) { OpCode = 2 });
            // 3334 IncomingMessageVerified – OneStringsTemplateA, opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "IncomingMessageVerified", 3334));
            // 3335 GetServiceInstanceStart – OneStringsTemplateA, opcode=1
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "GetServiceInstanceStart", 3335) { OpCode = 1 });
            // 3336 GetServiceInstanceStop – OneStringsTemplateA, opcode=2
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "GetServiceInstanceStop", 3336) { OpCode = 2 });
            // 3337 ChannelReceiveStart – Multidata43TemplateA, opcode=1
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ChannelReceiveStart", 3337) { OpCode = 1 });
            // 3338 ChannelReceiveStop – Multidata43TemplateA, opcode=2
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ChannelReceiveStop", 3338) { OpCode = 2 });
            // 3339 ChannelFactoryCreated – TwoStringsTemplateSA, opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ChannelFactoryCreated", 3339));
            // 3340 PipeConnectionAcceptStart – Multidata44TemplateA, opcode=1
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "PipeConnectionAcceptStart", 3340) { OpCode = 1 });
            // 3341 PipeConnectionAcceptStop – OneStringsTemplateA, opcode=2
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "PipeConnectionAcceptStop", 3341) { OpCode = 2 });
            // 3342 EstablishConnectionStart – Multidata45TemplateA, opcode=1
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "EstablishConnectionStart", 3342) { OpCode = 1 });
            // 3343 EstablishConnectionStop – OneStringsTemplateA, opcode=2
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "EstablishConnectionStop", 3343) { OpCode = 2 });
            // 3345 SessionPreambleUnderstood – Multidata46TemplateA, opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SessionPreambleUnderstood", 3345));
            // 3346 ConnectionReaderSendFault – Multidata47TemplateA, opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ConnectionReaderSendFault", 3346));
            // 3347 SocketAcceptClosed – OneStringsTemplateA, opcode=2
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SocketAcceptClosed", 3347) { OpCode = 2 });
            // 3348 ServiceHostFaulted – TwoStringsTemplateSA, opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ServiceHostFaulted", 3348));
            // 3349 ListenerOpenStart – Multidata48TemplateA, opcode=1
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ListenerOpenStart", 3349) { OpCode = 1 });
            // 3350 ListenerOpenStop – OneStringsTemplateA, opcode=2
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ListenerOpenStop", 3350) { OpCode = 2 });
            // 3351 ServerMaxPooledConnectionsQuotaReached – OneStringsTemplateA, opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ServerMaxPooledConnectionsQuotaReached", 3351));
            // 3352 TcpConnectionTimedOut – Multidata49TemplateA, opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "TcpConnectionTimedOut", 3352));
            // 3353 TcpConnectionResetError – Multidata49TemplateA, opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "TcpConnectionResetError", 3353));
            // 3354 ServiceSecurityNegotiationCompleted – OneStringsTemplateA, opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ServiceSecurityNegotiationCompleted", 3354));
            // 3355 SecurityNegotiationProcessingFailure – OneStringsTemplateA, opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SecurityNegotiationProcessingFailure", 3355));
            // 3356 SecurityIdentityVerificationSuccess – OneStringsTemplateA, opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SecurityIdentityVerificationSuccess", 3356));
            // 3357 SecurityIdentityVerificationFailure – OneStringsTemplateA, opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SecurityIdentityVerificationFailure", 3357));
            // 3358 PortSharingDuplicatedSocket – Multidata48TemplateA, opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "PortSharingDuplicatedSocket", 3358));
            // 3359 SecurityImpersonationSuccess – OneStringsTemplateA, opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SecurityImpersonationSuccess", 3359));
            // 3360 SecurityImpersonationFailure – OneStringsTemplateA, opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SecurityImpersonationFailure", 3360));
            // 3361 HttpChannelRequestAborted – OneStringsTemplateA, opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "HttpChannelRequestAborted", 3361));
            // 3362 HttpChannelResponseAborted – OneStringsTemplateA, opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "HttpChannelResponseAborted", 3362));
            // 3363 HttpAuthFailed – OneStringsTemplateA, opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "HttpAuthFailed", 3363));
            // 3364 SharedListenerProxyRegisterStart – Multidata48TemplateA, opcode=1
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SharedListenerProxyRegisterStart", 3364) { OpCode = 1 });
            // 3365 SharedListenerProxyRegisterStop – OneStringsTemplateA, opcode=2
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SharedListenerProxyRegisterStop", 3365) { OpCode = 2 });
            // 3366 SharedListenerProxyRegisterFailed – Multidata50TemplateA, opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SharedListenerProxyRegisterFailed", 3366));
            // 3367 ConnectionPoolPreambleFailed – OneStringsTemplateA, opcode=0
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "ConnectionPoolPreambleFailed", 3367));
            // 3368 SslOnInitiateUpgrade – OneStringsTemplateA, opcode=115
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SslOnInitiateUpgrade", 3368) { OpCode = 115 });
            // 3369 SslOnAcceptUpgrade – OneStringsTemplateA, opcode=114
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "SslOnAcceptUpgrade", 3369) { OpCode = 114 });
            // 3370 BinaryMessageEncodingStart – OneStringsTemplateA, opcode=1
            writer.WriteMetadataBlock(new EventMetadata(metadataId++, ProviderName, "BinaryMessageEncodingStart", 3370) { OpCode = 1 });
        }

        /// <summary>
        /// Writes event payloads for chunk 07 events (IDs 3333–3370).
        /// Each event blob references its metadataId (matching the order in WriteMetadata_Chunk07).
        /// </summary>
        private void WriteEvents_Chunk07(EventPipeWriterV5 writer, ref int metadataId, ref int sequenceNumber)
        {
            int __metadataId = metadataId;
            int __sequenceNumber = sequenceNumber;
            // Helper: maps event ID -> (__metadataId, template fields)
            var events = new (int eventId, TemplateField[] fields)[]
            {
                (3333, Chunk07_OneStringsTemplateAFields),
                (3334, Chunk07_OneStringsTemplateAFields),
                (3335, Chunk07_OneStringsTemplateAFields),
                (3336, Chunk07_OneStringsTemplateAFields),
                (3337, Multidata43TemplateAFields),
                (3338, Multidata43TemplateAFields),
                (3339, Chunk07_TwoStringsTemplateSAFields),
                (3340, Multidata44TemplateAFields),
                (3341, Chunk07_OneStringsTemplateAFields),
                (3342, Multidata45TemplateAFields),
                (3343, Chunk07_OneStringsTemplateAFields),
                (3345, Multidata46TemplateAFields),
                (3346, Multidata47TemplateAFields),
                (3347, Chunk07_OneStringsTemplateAFields),
                (3348, Chunk07_TwoStringsTemplateSAFields),
                (3349, Multidata48TemplateAFields),
                (3350, Chunk07_OneStringsTemplateAFields),
                (3351, Chunk07_OneStringsTemplateAFields),
                (3352, Multidata49TemplateAFields),
                (3353, Multidata49TemplateAFields),
                (3354, Chunk07_OneStringsTemplateAFields),
                (3355, Chunk07_OneStringsTemplateAFields),
                (3356, Chunk07_OneStringsTemplateAFields),
                (3357, Chunk07_OneStringsTemplateAFields),
                (3358, Multidata48TemplateAFields),
                (3359, Chunk07_OneStringsTemplateAFields),
                (3360, Chunk07_OneStringsTemplateAFields),
                (3361, Chunk07_OneStringsTemplateAFields),
                (3362, Chunk07_OneStringsTemplateAFields),
                (3363, Chunk07_OneStringsTemplateAFields),
                (3364, Multidata48TemplateAFields),
                (3365, Chunk07_OneStringsTemplateAFields),
                (3366, Multidata50TemplateAFields),
                (3367, Chunk07_OneStringsTemplateAFields),
                (3368, Chunk07_OneStringsTemplateAFields),
                (3369, Chunk07_OneStringsTemplateAFields),
                (3370, Chunk07_OneStringsTemplateAFields),
            };

            int currentMetadataId = __metadataId;
            writer.WriteEventBlock(w =>
            {
                int mid = currentMetadataId;
                int seq = __sequenceNumber;
                foreach (var (eventId, fields) in events)
                {
                    byte[] payload = BuildPayload(eventId, fields);
                    w.WriteEventBlobV4Or5(mid++, 999, seq++, payload);
                }
                __sequenceNumber = seq;
            });
            __metadataId = currentMetadataId + events.Length;
        
            metadataId = __metadataId;
            sequenceNumber = __sequenceNumber;
        }

        /// <summary>
        /// Subscribes to all chunk 07 events on the parser, recording payload values into firedEvents.
        /// </summary>
        private void Subscribe_Chunk07(ApplicationServerTraceEventParser parser, Dictionary<int, Dictionary<string, object>> firedEvents)
        {
            parser.OutgoingMessageSecured += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.IncomingMessageVerified += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.GetServiceInstanceStart += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.GetServiceInstanceStop += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.ChannelReceiveStart += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "ChannelId", d.ChannelId },
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.ChannelReceiveStop += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "ChannelId", d.ChannelId },
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.ChannelFactoryCreated += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "EventSource", d.EventSource },
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.PipeConnectionAcceptStart += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "uri", d.uri },
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.PipeConnectionAcceptStop += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.EstablishConnectionStart += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "Key", d.Key },
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.EstablishConnectionStop += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.SessionPreambleUnderstood += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "Via", d.Via },
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.ConnectionReaderSendFault += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "FaultString", d.FaultString },
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.SocketAcceptClosed += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.ServiceHostFaulted += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "EventSource", d.EventSource },
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.ListenerOpenStart += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "Uri", d.Uri },
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.ListenerOpenStop += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.ServerMaxPooledConnectionsQuotaReached += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.TcpConnectionTimedOut += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "SocketId", d.SocketId },
                    { "Uri", d.Uri },
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.TcpConnectionResetError += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "SocketId", d.SocketId },
                    { "Uri", d.Uri },
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.ServiceSecurityNegotiationCompleted += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.SecurityNegotiationProcessingFailure += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.SecurityIdentityVerificationSuccess += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.SecurityIdentityVerificationFailure += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.PortSharingDuplicatedSocket += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "Uri", d.Uri },
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.SecurityImpersonationSuccess += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.SecurityImpersonationFailure += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.HttpChannelRequestAborted += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.HttpChannelResponseAborted += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.HttpAuthFailed += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.SharedListenerProxyRegisterStart += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "Uri", d.Uri },
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.SharedListenerProxyRegisterStop += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.SharedListenerProxyRegisterFailed += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "Status", d.Status },
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.ConnectionPoolPreambleFailed += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.SslOnInitiateUpgrade += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.SslOnAcceptUpgrade += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.BinaryMessageEncodingStart += d =>
            {
                firedEvents[(int)d.ID] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
        }

        /// <summary>
        /// Validates all chunk 07 events fired with correct payload values.
        /// </summary>
        private void Validate_Chunk07(Dictionary<int, Dictionary<string, object>> firedEvents)
        {
            // 3333 OutgoingMessageSecured – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3333), "Event 3333 (OutgoingMessageSecured) did not fire.");
            Assert.Equal(TestString(3333, "AppDomain"), firedEvents[3333]["AppDomain"]);

            // 3334 IncomingMessageVerified – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3334), "Event 3334 (IncomingMessageVerified) did not fire.");
            Assert.Equal(TestString(3334, "AppDomain"), firedEvents[3334]["AppDomain"]);

            // 3335 GetServiceInstanceStart – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3335), "Event 3335 (GetServiceInstanceStart) did not fire.");
            Assert.Equal(TestString(3335, "AppDomain"), firedEvents[3335]["AppDomain"]);

            // 3336 GetServiceInstanceStop – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3336), "Event 3336 (GetServiceInstanceStop) did not fire.");
            Assert.Equal(TestString(3336, "AppDomain"), firedEvents[3336]["AppDomain"]);

            // 3337 ChannelReceiveStart – Multidata43TemplateA
            Assert.True(firedEvents.ContainsKey(3337), "Event 3337 (ChannelReceiveStart) did not fire.");
            Assert.Equal(TestInt32(3337, 0), firedEvents[3337]["ChannelId"]);
            Assert.Equal(TestString(3337, "AppDomain"), firedEvents[3337]["AppDomain"]);

            // 3338 ChannelReceiveStop – Multidata43TemplateA
            Assert.True(firedEvents.ContainsKey(3338), "Event 3338 (ChannelReceiveStop) did not fire.");
            Assert.Equal(TestInt32(3338, 0), firedEvents[3338]["ChannelId"]);
            Assert.Equal(TestString(3338, "AppDomain"), firedEvents[3338]["AppDomain"]);

            // 3339 ChannelFactoryCreated – TwoStringsTemplateSA
            Assert.True(firedEvents.ContainsKey(3339), "Event 3339 (ChannelFactoryCreated) did not fire.");
            Assert.Equal(TestString(3339, "EventSource"), firedEvents[3339]["EventSource"]);
            Assert.Equal(TestString(3339, "AppDomain"), firedEvents[3339]["AppDomain"]);

            // 3340 PipeConnectionAcceptStart – Multidata44TemplateA
            Assert.True(firedEvents.ContainsKey(3340), "Event 3340 (PipeConnectionAcceptStart) did not fire.");
            Assert.Equal(TestString(3340, "uri"), firedEvents[3340]["uri"]);
            Assert.Equal(TestString(3340, "AppDomain"), firedEvents[3340]["AppDomain"]);

            // 3341 PipeConnectionAcceptStop – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3341), "Event 3341 (PipeConnectionAcceptStop) did not fire.");
            Assert.Equal(TestString(3341, "AppDomain"), firedEvents[3341]["AppDomain"]);

            // 3342 EstablishConnectionStart – Multidata45TemplateA
            Assert.True(firedEvents.ContainsKey(3342), "Event 3342 (EstablishConnectionStart) did not fire.");
            Assert.Equal(TestString(3342, "Key"), firedEvents[3342]["Key"]);
            Assert.Equal(TestString(3342, "AppDomain"), firedEvents[3342]["AppDomain"]);

            // 3343 EstablishConnectionStop – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3343), "Event 3343 (EstablishConnectionStop) did not fire.");
            Assert.Equal(TestString(3343, "AppDomain"), firedEvents[3343]["AppDomain"]);

            // 3345 SessionPreambleUnderstood – Multidata46TemplateA
            Assert.True(firedEvents.ContainsKey(3345), "Event 3345 (SessionPreambleUnderstood) did not fire.");
            Assert.Equal(TestString(3345, "Via"), firedEvents[3345]["Via"]);
            Assert.Equal(TestString(3345, "AppDomain"), firedEvents[3345]["AppDomain"]);

            // 3346 ConnectionReaderSendFault – Multidata47TemplateA
            Assert.True(firedEvents.ContainsKey(3346), "Event 3346 (ConnectionReaderSendFault) did not fire.");
            Assert.Equal(TestString(3346, "FaultString"), firedEvents[3346]["FaultString"]);
            Assert.Equal(TestString(3346, "AppDomain"), firedEvents[3346]["AppDomain"]);

            // 3347 SocketAcceptClosed – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3347), "Event 3347 (SocketAcceptClosed) did not fire.");
            Assert.Equal(TestString(3347, "AppDomain"), firedEvents[3347]["AppDomain"]);

            // 3348 ServiceHostFaulted – TwoStringsTemplateSA
            Assert.True(firedEvents.ContainsKey(3348), "Event 3348 (ServiceHostFaulted) did not fire.");
            Assert.Equal(TestString(3348, "EventSource"), firedEvents[3348]["EventSource"]);
            Assert.Equal(TestString(3348, "AppDomain"), firedEvents[3348]["AppDomain"]);

            // 3349 ListenerOpenStart – Multidata48TemplateA
            Assert.True(firedEvents.ContainsKey(3349), "Event 3349 (ListenerOpenStart) did not fire.");
            Assert.Equal(TestString(3349, "Uri"), firedEvents[3349]["Uri"]);
            Assert.Equal(TestString(3349, "AppDomain"), firedEvents[3349]["AppDomain"]);

            // 3350 ListenerOpenStop – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3350), "Event 3350 (ListenerOpenStop) did not fire.");
            Assert.Equal(TestString(3350, "AppDomain"), firedEvents[3350]["AppDomain"]);

            // 3351 ServerMaxPooledConnectionsQuotaReached – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3351), "Event 3351 (ServerMaxPooledConnectionsQuotaReached) did not fire.");
            Assert.Equal(TestString(3351, "AppDomain"), firedEvents[3351]["AppDomain"]);

            // 3352 TcpConnectionTimedOut – Multidata49TemplateA
            Assert.True(firedEvents.ContainsKey(3352), "Event 3352 (TcpConnectionTimedOut) did not fire.");
            Assert.Equal(TestInt32(3352, 0), firedEvents[3352]["SocketId"]);
            Assert.Equal(TestString(3352, "Uri"), firedEvents[3352]["Uri"]);
            Assert.Equal(TestString(3352, "AppDomain"), firedEvents[3352]["AppDomain"]);

            // 3353 TcpConnectionResetError – Multidata49TemplateA
            Assert.True(firedEvents.ContainsKey(3353), "Event 3353 (TcpConnectionResetError) did not fire.");
            Assert.Equal(TestInt32(3353, 0), firedEvents[3353]["SocketId"]);
            Assert.Equal(TestString(3353, "Uri"), firedEvents[3353]["Uri"]);
            Assert.Equal(TestString(3353, "AppDomain"), firedEvents[3353]["AppDomain"]);

            // 3354 ServiceSecurityNegotiationCompleted – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3354), "Event 3354 (ServiceSecurityNegotiationCompleted) did not fire.");
            Assert.Equal(TestString(3354, "AppDomain"), firedEvents[3354]["AppDomain"]);

            // 3355 SecurityNegotiationProcessingFailure – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3355), "Event 3355 (SecurityNegotiationProcessingFailure) did not fire.");
            Assert.Equal(TestString(3355, "AppDomain"), firedEvents[3355]["AppDomain"]);

            // 3356 SecurityIdentityVerificationSuccess – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3356), "Event 3356 (SecurityIdentityVerificationSuccess) did not fire.");
            Assert.Equal(TestString(3356, "AppDomain"), firedEvents[3356]["AppDomain"]);

            // 3357 SecurityIdentityVerificationFailure – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3357), "Event 3357 (SecurityIdentityVerificationFailure) did not fire.");
            Assert.Equal(TestString(3357, "AppDomain"), firedEvents[3357]["AppDomain"]);

            // 3358 PortSharingDuplicatedSocket – Multidata48TemplateA
            Assert.True(firedEvents.ContainsKey(3358), "Event 3358 (PortSharingDuplicatedSocket) did not fire.");
            Assert.Equal(TestString(3358, "Uri"), firedEvents[3358]["Uri"]);
            Assert.Equal(TestString(3358, "AppDomain"), firedEvents[3358]["AppDomain"]);

            // 3359 SecurityImpersonationSuccess – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3359), "Event 3359 (SecurityImpersonationSuccess) did not fire.");
            Assert.Equal(TestString(3359, "AppDomain"), firedEvents[3359]["AppDomain"]);

            // 3360 SecurityImpersonationFailure – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3360), "Event 3360 (SecurityImpersonationFailure) did not fire.");
            Assert.Equal(TestString(3360, "AppDomain"), firedEvents[3360]["AppDomain"]);

            // 3361 HttpChannelRequestAborted – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3361), "Event 3361 (HttpChannelRequestAborted) did not fire.");
            Assert.Equal(TestString(3361, "AppDomain"), firedEvents[3361]["AppDomain"]);

            // 3362 HttpChannelResponseAborted – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3362), "Event 3362 (HttpChannelResponseAborted) did not fire.");
            Assert.Equal(TestString(3362, "AppDomain"), firedEvents[3362]["AppDomain"]);

            // 3363 HttpAuthFailed – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3363), "Event 3363 (HttpAuthFailed) did not fire.");
            Assert.Equal(TestString(3363, "AppDomain"), firedEvents[3363]["AppDomain"]);

            // 3364 SharedListenerProxyRegisterStart – Multidata48TemplateA
            Assert.True(firedEvents.ContainsKey(3364), "Event 3364 (SharedListenerProxyRegisterStart) did not fire.");
            Assert.Equal(TestString(3364, "Uri"), firedEvents[3364]["Uri"]);
            Assert.Equal(TestString(3364, "AppDomain"), firedEvents[3364]["AppDomain"]);

            // 3365 SharedListenerProxyRegisterStop – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3365), "Event 3365 (SharedListenerProxyRegisterStop) did not fire.");
            Assert.Equal(TestString(3365, "AppDomain"), firedEvents[3365]["AppDomain"]);

            // 3366 SharedListenerProxyRegisterFailed – Multidata50TemplateA
            Assert.True(firedEvents.ContainsKey(3366), "Event 3366 (SharedListenerProxyRegisterFailed) did not fire.");
            Assert.Equal(TestString(3366, "Status"), firedEvents[3366]["Status"]);
            Assert.Equal(TestString(3366, "AppDomain"), firedEvents[3366]["AppDomain"]);

            // 3367 ConnectionPoolPreambleFailed – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3367), "Event 3367 (ConnectionPoolPreambleFailed) did not fire.");
            Assert.Equal(TestString(3367, "AppDomain"), firedEvents[3367]["AppDomain"]);

            // 3368 SslOnInitiateUpgrade – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3368), "Event 3368 (SslOnInitiateUpgrade) did not fire.");
            Assert.Equal(TestString(3368, "AppDomain"), firedEvents[3368]["AppDomain"]);

            // 3369 SslOnAcceptUpgrade – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3369), "Event 3369 (SslOnAcceptUpgrade) did not fire.");
            Assert.Equal(TestString(3369, "AppDomain"), firedEvents[3369]["AppDomain"]);

            // 3370 BinaryMessageEncodingStart – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3370), "Event 3370 (BinaryMessageEncodingStart) did not fire.");
            Assert.Equal(TestString(3370, "AppDomain"), firedEvents[3370]["AppDomain"]);
        }
    }
}
