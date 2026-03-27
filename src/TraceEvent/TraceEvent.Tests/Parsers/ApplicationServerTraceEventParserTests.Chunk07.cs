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
        private void Subscribe_Chunk07(ApplicationServerTraceEventParser parser, Dictionary<string, Dictionary<string, object>> firedEvents)
        {
            parser.OutgoingMessageSecured += d =>
            {
                firedEvents["OutgoingMessageSecured"] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.IncomingMessageVerified += d =>
            {
                firedEvents["IncomingMessageVerified"] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.GetServiceInstanceStart += d =>
            {
                firedEvents["GetServiceInstanceStart"] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.GetServiceInstanceStop += d =>
            {
                firedEvents["GetServiceInstanceStop"] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.ChannelReceiveStart += d =>
            {
                firedEvents["ChannelReceiveStart"] = new Dictionary<string, object>
                {
                    { "ChannelId", d.ChannelId },
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.ChannelReceiveStop += d =>
            {
                firedEvents["ChannelReceiveStop"] = new Dictionary<string, object>
                {
                    { "ChannelId", d.ChannelId },
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.ChannelFactoryCreated += d =>
            {
                firedEvents["ChannelFactoryCreated"] = new Dictionary<string, object>
                {
                    { "EventSource", d.EventSource },
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.PipeConnectionAcceptStart += d =>
            {
                firedEvents["PipeConnectionAcceptStart"] = new Dictionary<string, object>
                {
                    { "uri", d.uri },
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.PipeConnectionAcceptStop += d =>
            {
                firedEvents["PipeConnectionAcceptStop"] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.EstablishConnectionStart += d =>
            {
                firedEvents["EstablishConnectionStart"] = new Dictionary<string, object>
                {
                    { "Key", d.Key },
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.EstablishConnectionStop += d =>
            {
                firedEvents["EstablishConnectionStop"] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.SessionPreambleUnderstood += d =>
            {
                firedEvents["SessionPreambleUnderstood"] = new Dictionary<string, object>
                {
                    { "Via", d.Via },
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.ConnectionReaderSendFault += d =>
            {
                firedEvents["ConnectionReaderSendFault"] = new Dictionary<string, object>
                {
                    { "FaultString", d.FaultString },
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.SocketAcceptClosed += d =>
            {
                firedEvents["SocketAcceptClosed"] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.ServiceHostFaulted += d =>
            {
                firedEvents["ServiceHostFaulted"] = new Dictionary<string, object>
                {
                    { "EventSource", d.EventSource },
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.ListenerOpenStart += d =>
            {
                firedEvents["ListenerOpenStart"] = new Dictionary<string, object>
                {
                    { "Uri", d.Uri },
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.ListenerOpenStop += d =>
            {
                firedEvents["ListenerOpenStop"] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.ServerMaxPooledConnectionsQuotaReached += d =>
            {
                firedEvents["ServerMaxPooledConnectionsQuotaReached"] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.TcpConnectionTimedOut += d =>
            {
                firedEvents["TcpConnectionTimedOut"] = new Dictionary<string, object>
                {
                    { "SocketId", d.SocketId },
                    { "Uri", d.Uri },
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.TcpConnectionResetError += d =>
            {
                firedEvents["TcpConnectionResetError"] = new Dictionary<string, object>
                {
                    { "SocketId", d.SocketId },
                    { "Uri", d.Uri },
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.ServiceSecurityNegotiationCompleted += d =>
            {
                firedEvents["ServiceSecurityNegotiationCompleted"] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.SecurityNegotiationProcessingFailure += d =>
            {
                firedEvents["SecurityNegotiationProcessingFailure"] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.SecurityIdentityVerificationSuccess += d =>
            {
                firedEvents["SecurityIdentityVerificationSuccess"] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.SecurityIdentityVerificationFailure += d =>
            {
                firedEvents["SecurityIdentityVerificationFailure"] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.PortSharingDuplicatedSocket += d =>
            {
                firedEvents["PortSharingDuplicatedSocket"] = new Dictionary<string, object>
                {
                    { "Uri", d.Uri },
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.SecurityImpersonationSuccess += d =>
            {
                firedEvents["SecurityImpersonationSuccess"] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.SecurityImpersonationFailure += d =>
            {
                firedEvents["SecurityImpersonationFailure"] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.HttpChannelRequestAborted += d =>
            {
                firedEvents["HttpChannelRequestAborted"] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.HttpChannelResponseAborted += d =>
            {
                firedEvents["HttpChannelResponseAborted"] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.HttpAuthFailed += d =>
            {
                firedEvents["HttpAuthFailed"] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.SharedListenerProxyRegisterStart += d =>
            {
                firedEvents["SharedListenerProxyRegisterStart"] = new Dictionary<string, object>
                {
                    { "Uri", d.Uri },
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.SharedListenerProxyRegisterStop += d =>
            {
                firedEvents["SharedListenerProxyRegisterStop"] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.SharedListenerProxyRegisterFailed += d =>
            {
                firedEvents["SharedListenerProxyRegisterFailed"] = new Dictionary<string, object>
                {
                    { "Status", d.Status },
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.ConnectionPoolPreambleFailed += d =>
            {
                firedEvents["ConnectionPoolPreambleFailed"] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.SslOnInitiateUpgrade += d =>
            {
                firedEvents["SslOnInitiateUpgrade"] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.SslOnAcceptUpgrade += d =>
            {
                firedEvents["SslOnAcceptUpgrade"] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
            parser.BinaryMessageEncodingStart += d =>
            {
                firedEvents["BinaryMessageEncodingStart"] = new Dictionary<string, object>
                {
                    { "AppDomain", d.AppDomain },
                };
            };
        }

        /// <summary>
        /// Validates all chunk 07 events fired with correct payload values.
        /// </summary>
        private void Validate_Chunk07(Dictionary<string, Dictionary<string, object>> firedEvents)
        {
            // 3333 OutgoingMessageSecured – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("OutgoingMessageSecured"), "Event OutgoingMessageSecured did not fire.");
            Assert.Equal(TestString(3333, "AppDomain"), firedEvents["OutgoingMessageSecured"]["AppDomain"]);

            // 3334 IncomingMessageVerified – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("IncomingMessageVerified"), "Event IncomingMessageVerified did not fire.");
            Assert.Equal(TestString(3334, "AppDomain"), firedEvents["IncomingMessageVerified"]["AppDomain"]);

            // 3335 GetServiceInstanceStart – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("GetServiceInstanceStart"), "Event GetServiceInstanceStart did not fire.");
            Assert.Equal(TestString(3335, "AppDomain"), firedEvents["GetServiceInstanceStart"]["AppDomain"]);

            // 3336 GetServiceInstanceStop – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("GetServiceInstanceStop"), "Event GetServiceInstanceStop did not fire.");
            Assert.Equal(TestString(3336, "AppDomain"), firedEvents["GetServiceInstanceStop"]["AppDomain"]);

            // 3337 ChannelReceiveStart – Multidata43TemplateA
            Assert.True(firedEvents.ContainsKey("ChannelReceiveStart"), "Event ChannelReceiveStart did not fire.");
            Assert.Equal(TestInt32(3337, 0), firedEvents["ChannelReceiveStart"]["ChannelId"]);
            Assert.Equal(TestString(3337, "AppDomain"), firedEvents["ChannelReceiveStart"]["AppDomain"]);

            // 3338 ChannelReceiveStop – Multidata43TemplateA
            Assert.True(firedEvents.ContainsKey("ChannelReceiveStop"), "Event ChannelReceiveStop did not fire.");
            Assert.Equal(TestInt32(3338, 0), firedEvents["ChannelReceiveStop"]["ChannelId"]);
            Assert.Equal(TestString(3338, "AppDomain"), firedEvents["ChannelReceiveStop"]["AppDomain"]);

            // 3339 ChannelFactoryCreated – TwoStringsTemplateSA
            Assert.True(firedEvents.ContainsKey("ChannelFactoryCreated"), "Event ChannelFactoryCreated did not fire.");
            Assert.Equal(TestString(3339, "EventSource"), firedEvents["ChannelFactoryCreated"]["EventSource"]);
            Assert.Equal(TestString(3339, "AppDomain"), firedEvents["ChannelFactoryCreated"]["AppDomain"]);

            // 3340 PipeConnectionAcceptStart – Multidata44TemplateA
            Assert.True(firedEvents.ContainsKey("PipeConnectionAcceptStart"), "Event PipeConnectionAcceptStart did not fire.");
            Assert.Equal(TestString(3340, "uri"), firedEvents["PipeConnectionAcceptStart"]["uri"]);
            Assert.Equal(TestString(3340, "AppDomain"), firedEvents["PipeConnectionAcceptStart"]["AppDomain"]);

            // 3341 PipeConnectionAcceptStop – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("PipeConnectionAcceptStop"), "Event PipeConnectionAcceptStop did not fire.");
            Assert.Equal(TestString(3341, "AppDomain"), firedEvents["PipeConnectionAcceptStop"]["AppDomain"]);

            // 3342 EstablishConnectionStart – Multidata45TemplateA
            Assert.True(firedEvents.ContainsKey("EstablishConnectionStart"), "Event EstablishConnectionStart did not fire.");
            Assert.Equal(TestString(3342, "Key"), firedEvents["EstablishConnectionStart"]["Key"]);
            Assert.Equal(TestString(3342, "AppDomain"), firedEvents["EstablishConnectionStart"]["AppDomain"]);

            // 3343 EstablishConnectionStop – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("EstablishConnectionStop"), "Event EstablishConnectionStop did not fire.");
            Assert.Equal(TestString(3343, "AppDomain"), firedEvents["EstablishConnectionStop"]["AppDomain"]);

            // 3345 SessionPreambleUnderstood – Multidata46TemplateA
            Assert.True(firedEvents.ContainsKey("SessionPreambleUnderstood"), "Event SessionPreambleUnderstood did not fire.");
            Assert.Equal(TestString(3345, "Via"), firedEvents["SessionPreambleUnderstood"]["Via"]);
            Assert.Equal(TestString(3345, "AppDomain"), firedEvents["SessionPreambleUnderstood"]["AppDomain"]);

            // 3346 ConnectionReaderSendFault – Multidata47TemplateA
            Assert.True(firedEvents.ContainsKey("ConnectionReaderSendFault"), "Event ConnectionReaderSendFault did not fire.");
            Assert.Equal(TestString(3346, "FaultString"), firedEvents["ConnectionReaderSendFault"]["FaultString"]);
            Assert.Equal(TestString(3346, "AppDomain"), firedEvents["ConnectionReaderSendFault"]["AppDomain"]);

            // 3347 SocketAcceptClosed – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("SocketAcceptClosed"), "Event SocketAcceptClosed did not fire.");
            Assert.Equal(TestString(3347, "AppDomain"), firedEvents["SocketAcceptClosed"]["AppDomain"]);

            // 3348 ServiceHostFaulted – TwoStringsTemplateSA
            Assert.True(firedEvents.ContainsKey("ServiceHostFaulted"), "Event ServiceHostFaulted did not fire.");
            Assert.Equal(TestString(3348, "EventSource"), firedEvents["ServiceHostFaulted"]["EventSource"]);
            Assert.Equal(TestString(3348, "AppDomain"), firedEvents["ServiceHostFaulted"]["AppDomain"]);

            // 3349 ListenerOpenStart – Multidata48TemplateA
            Assert.True(firedEvents.ContainsKey("ListenerOpenStart"), "Event ListenerOpenStart did not fire.");
            Assert.Equal(TestString(3349, "Uri"), firedEvents["ListenerOpenStart"]["Uri"]);
            Assert.Equal(TestString(3349, "AppDomain"), firedEvents["ListenerOpenStart"]["AppDomain"]);

            // 3350 ListenerOpenStop – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("ListenerOpenStop"), "Event ListenerOpenStop did not fire.");
            Assert.Equal(TestString(3350, "AppDomain"), firedEvents["ListenerOpenStop"]["AppDomain"]);

            // 3351 ServerMaxPooledConnectionsQuotaReached – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("ServerMaxPooledConnectionsQuotaReached"), "Event ServerMaxPooledConnectionsQuotaReached did not fire.");
            Assert.Equal(TestString(3351, "AppDomain"), firedEvents["ServerMaxPooledConnectionsQuotaReached"]["AppDomain"]);

            // 3352 TcpConnectionTimedOut – Multidata49TemplateA
            Assert.True(firedEvents.ContainsKey("TcpConnectionTimedOut"), "Event TcpConnectionTimedOut did not fire.");
            Assert.Equal(TestInt32(3352, 0), firedEvents["TcpConnectionTimedOut"]["SocketId"]);
            Assert.Equal(TestString(3352, "Uri"), firedEvents["TcpConnectionTimedOut"]["Uri"]);
            Assert.Equal(TestString(3352, "AppDomain"), firedEvents["TcpConnectionTimedOut"]["AppDomain"]);

            // 3353 TcpConnectionResetError – Multidata49TemplateA
            Assert.True(firedEvents.ContainsKey("TcpConnectionResetError"), "Event TcpConnectionResetError did not fire.");
            Assert.Equal(TestInt32(3353, 0), firedEvents["TcpConnectionResetError"]["SocketId"]);
            Assert.Equal(TestString(3353, "Uri"), firedEvents["TcpConnectionResetError"]["Uri"]);
            Assert.Equal(TestString(3353, "AppDomain"), firedEvents["TcpConnectionResetError"]["AppDomain"]);

            // 3354 ServiceSecurityNegotiationCompleted – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("ServiceSecurityNegotiationCompleted"), "Event ServiceSecurityNegotiationCompleted did not fire.");
            Assert.Equal(TestString(3354, "AppDomain"), firedEvents["ServiceSecurityNegotiationCompleted"]["AppDomain"]);

            // 3355 SecurityNegotiationProcessingFailure – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("SecurityNegotiationProcessingFailure"), "Event SecurityNegotiationProcessingFailure did not fire.");
            Assert.Equal(TestString(3355, "AppDomain"), firedEvents["SecurityNegotiationProcessingFailure"]["AppDomain"]);

            // 3356 SecurityIdentityVerificationSuccess – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("SecurityIdentityVerificationSuccess"), "Event SecurityIdentityVerificationSuccess did not fire.");
            Assert.Equal(TestString(3356, "AppDomain"), firedEvents["SecurityIdentityVerificationSuccess"]["AppDomain"]);

            // 3357 SecurityIdentityVerificationFailure – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("SecurityIdentityVerificationFailure"), "Event SecurityIdentityVerificationFailure did not fire.");
            Assert.Equal(TestString(3357, "AppDomain"), firedEvents["SecurityIdentityVerificationFailure"]["AppDomain"]);

            // 3358 PortSharingDuplicatedSocket – Multidata48TemplateA
            Assert.True(firedEvents.ContainsKey("PortSharingDuplicatedSocket"), "Event PortSharingDuplicatedSocket did not fire.");
            Assert.Equal(TestString(3358, "Uri"), firedEvents["PortSharingDuplicatedSocket"]["Uri"]);
            Assert.Equal(TestString(3358, "AppDomain"), firedEvents["PortSharingDuplicatedSocket"]["AppDomain"]);

            // 3359 SecurityImpersonationSuccess – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("SecurityImpersonationSuccess"), "Event SecurityImpersonationSuccess did not fire.");
            Assert.Equal(TestString(3359, "AppDomain"), firedEvents["SecurityImpersonationSuccess"]["AppDomain"]);

            // 3360 SecurityImpersonationFailure – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("SecurityImpersonationFailure"), "Event SecurityImpersonationFailure did not fire.");
            Assert.Equal(TestString(3360, "AppDomain"), firedEvents["SecurityImpersonationFailure"]["AppDomain"]);

            // 3361 HttpChannelRequestAborted – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("HttpChannelRequestAborted"), "Event HttpChannelRequestAborted did not fire.");
            Assert.Equal(TestString(3361, "AppDomain"), firedEvents["HttpChannelRequestAborted"]["AppDomain"]);

            // 3362 HttpChannelResponseAborted – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("HttpChannelResponseAborted"), "Event HttpChannelResponseAborted did not fire.");
            Assert.Equal(TestString(3362, "AppDomain"), firedEvents["HttpChannelResponseAborted"]["AppDomain"]);

            // 3363 HttpAuthFailed – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("HttpAuthFailed"), "Event HttpAuthFailed did not fire.");
            Assert.Equal(TestString(3363, "AppDomain"), firedEvents["HttpAuthFailed"]["AppDomain"]);

            // 3364 SharedListenerProxyRegisterStart – Multidata48TemplateA
            Assert.True(firedEvents.ContainsKey("SharedListenerProxyRegisterStart"), "Event SharedListenerProxyRegisterStart did not fire.");
            Assert.Equal(TestString(3364, "Uri"), firedEvents["SharedListenerProxyRegisterStart"]["Uri"]);
            Assert.Equal(TestString(3364, "AppDomain"), firedEvents["SharedListenerProxyRegisterStart"]["AppDomain"]);

            // 3365 SharedListenerProxyRegisterStop – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("SharedListenerProxyRegisterStop"), "Event SharedListenerProxyRegisterStop did not fire.");
            Assert.Equal(TestString(3365, "AppDomain"), firedEvents["SharedListenerProxyRegisterStop"]["AppDomain"]);

            // 3366 SharedListenerProxyRegisterFailed – Multidata50TemplateA
            Assert.True(firedEvents.ContainsKey("SharedListenerProxyRegisterFailed"), "Event SharedListenerProxyRegisterFailed did not fire.");
            Assert.Equal(TestString(3366, "Status"), firedEvents["SharedListenerProxyRegisterFailed"]["Status"]);
            Assert.Equal(TestString(3366, "AppDomain"), firedEvents["SharedListenerProxyRegisterFailed"]["AppDomain"]);

            // 3367 ConnectionPoolPreambleFailed – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("ConnectionPoolPreambleFailed"), "Event ConnectionPoolPreambleFailed did not fire.");
            Assert.Equal(TestString(3367, "AppDomain"), firedEvents["ConnectionPoolPreambleFailed"]["AppDomain"]);

            // 3368 SslOnInitiateUpgrade – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("SslOnInitiateUpgrade"), "Event SslOnInitiateUpgrade did not fire.");
            Assert.Equal(TestString(3368, "AppDomain"), firedEvents["SslOnInitiateUpgrade"]["AppDomain"]);

            // 3369 SslOnAcceptUpgrade – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("SslOnAcceptUpgrade"), "Event SslOnAcceptUpgrade did not fire.");
            Assert.Equal(TestString(3369, "AppDomain"), firedEvents["SslOnAcceptUpgrade"]["AppDomain"]);

            // 3370 BinaryMessageEncodingStart – OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("BinaryMessageEncodingStart"), "Event BinaryMessageEncodingStart did not fire.");
            Assert.Equal(TestString(3370, "AppDomain"), firedEvents["BinaryMessageEncodingStart"]["AppDomain"]);
        }
    }
}
