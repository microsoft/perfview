using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.ApplicationServer;
using System.Collections.Generic;
using Xunit;

namespace TraceEventTests.Parsers
{
    public partial class ApplicationServerTraceEventParserTests
    {
        #region Chunk 10 Template Fields

        private static readonly TemplateField[] s_chunk10_OneStringsTemplateA = new TemplateField[]
        {
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_chunk10_TwoStringsTemplateA = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_chunk10_TwoStringsTemplateEA = new TemplateField[]
        {
            new TemplateField("SerializedException", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_chunk10_ThreeStringsTemplateA = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("data2", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_chunk10_ThreeStringsTemplateEA = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("SerializedException", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_chunk10_FourStringsTemplateA = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("data2", FieldType.UnicodeString),
            new TemplateField("data3", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_chunk10_FiveStringsTemplateA = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("data2", FieldType.UnicodeString),
            new TemplateField("data3", FieldType.UnicodeString),
            new TemplateField("data4", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_chunk10_Multidata48TemplateA = new TemplateField[]
        {
            new TemplateField("Uri", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_chunk10_Multidata76TemplateA = new TemplateField[]
        {
            new TemplateField("availableMemoryBytes", FieldType.Int64),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_chunk10_Multidata77TemplateA = new TemplateField[]
        {
            new TemplateField("via", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_chunk10_Multidata78TemplateA = new TemplateField[]
        {
            new TemplateField("Endpoint", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_chunk10_Multidata79TemplateA = new TemplateField[]
        {
            new TemplateField("Uri", FieldType.UnicodeString),
            new TemplateField("count", FieldType.Int32),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_chunk10_Multidata81TemplateA = new TemplateField[]
        {
            new TemplateField("Status", FieldType.UnicodeString),
            new TemplateField("Uri", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        #endregion

        #region Chunk 10 WriteMetadata

        private void WriteMetadata_Chunk10(EventPipeWriterV5 writer, ref int metadataId)
        {
            writer.WriteMetadataBlock(
                // 3560 ServiceActivationAvailableMemory (opcode 0)
                new EventMetadata(metadataId++, ProviderName, "ServiceActivationAvailableMemory", 3560),
                // 3561 ServiceActivationException (opcode 0)
                new EventMetadata(metadataId++, ProviderName, "ServiceActivationException", 3561),
                // 3800 RoutingServiceClosingClient (opcode 87)
                new EventMetadata(metadataId++, ProviderName, "RoutingServiceClosingClient", 3800) { OpCode = 87 },
                // 3801 RoutingServiceChannelFaulted (opcode 86)
                new EventMetadata(metadataId++, ProviderName, "RoutingServiceChannelFaulted", 3801) { OpCode = 86 },
                // 3802 RoutingServiceCompletingOneWay (opcode 89)
                new EventMetadata(metadataId++, ProviderName, "RoutingServiceCompletingOneWay", 3802) { OpCode = 89 },
                // 3803 RoutingServiceProcessingFailure (opcode 92)
                new EventMetadata(metadataId++, ProviderName, "RoutingServiceProcessingFailure", 3803) { OpCode = 92 },
                // 3804 RoutingServiceCreatingClientForEndpoint (opcode 88)
                new EventMetadata(metadataId++, ProviderName, "RoutingServiceCreatingClientForEndpoint", 3804) { OpCode = 88 },
                // 3805 RoutingServiceDisplayConfig (opcode 0)
                new EventMetadata(metadataId++, ProviderName, "RoutingServiceDisplayConfig", 3805),
                // 3807 RoutingServiceCompletingTwoWay (opcode 90)
                new EventMetadata(metadataId++, ProviderName, "RoutingServiceCompletingTwoWay", 3807) { OpCode = 90 },
                // 3809 RoutingServiceMessageRoutedToEndpoints (opcode 94)
                new EventMetadata(metadataId++, ProviderName, "RoutingServiceMessageRoutedToEndpoints", 3809) { OpCode = 94 },
                // 3810 RoutingServiceConfigurationApplied (opcode 82)
                new EventMetadata(metadataId++, ProviderName, "RoutingServiceConfigurationApplied", 3810) { OpCode = 82 },
                // 3815 RoutingServiceProcessingMessage (opcode 93)
                new EventMetadata(metadataId++, ProviderName, "RoutingServiceProcessingMessage", 3815) { OpCode = 93 },
                // 3816 RoutingServiceTransmittingMessage (opcode 98)
                new EventMetadata(metadataId++, ProviderName, "RoutingServiceTransmittingMessage", 3816) { OpCode = 98 },
                // 3817 RoutingServiceCommittingTransaction (opcode 101)
                new EventMetadata(metadataId++, ProviderName, "RoutingServiceCommittingTransaction", 3817) { OpCode = 101 },
                // 3818 RoutingServiceDuplexCallbackException (opcode 83)
                new EventMetadata(metadataId++, ProviderName, "RoutingServiceDuplexCallbackException", 3818) { OpCode = 83 },
                // 3819 RoutingServiceMovedToBackup (opcode 91)
                new EventMetadata(metadataId++, ProviderName, "RoutingServiceMovedToBackup", 3819) { OpCode = 91 },
                // 3820 RoutingServiceCreatingTransaction (opcode 102)
                new EventMetadata(metadataId++, ProviderName, "RoutingServiceCreatingTransaction", 3820) { OpCode = 102 },
                // 3821 RoutingServiceCloseFailed (opcode 81)
                new EventMetadata(metadataId++, ProviderName, "RoutingServiceCloseFailed", 3821) { OpCode = 81 },
                // 3822 RoutingServiceSendingResponse (opcode 96)
                new EventMetadata(metadataId++, ProviderName, "RoutingServiceSendingResponse", 3822) { OpCode = 96 },
                // 3823 RoutingServiceSendingFaultResponse (opcode 95)
                new EventMetadata(metadataId++, ProviderName, "RoutingServiceSendingFaultResponse", 3823) { OpCode = 95 },
                // 3824 RoutingServiceCompletingReceiveContext (opcode 100)
                new EventMetadata(metadataId++, ProviderName, "RoutingServiceCompletingReceiveContext", 3824) { OpCode = 100 },
                // 3825 RoutingServiceAbandoningReceiveContext (opcode 99)
                new EventMetadata(metadataId++, ProviderName, "RoutingServiceAbandoningReceiveContext", 3825) { OpCode = 99 },
                // 3826 RoutingServiceUsingExistingTransaction (opcode 103)
                new EventMetadata(metadataId++, ProviderName, "RoutingServiceUsingExistingTransaction", 3826) { OpCode = 103 },
                // 3827 RoutingServiceTransmitFailed (opcode 85)
                new EventMetadata(metadataId++, ProviderName, "RoutingServiceTransmitFailed", 3827) { OpCode = 85 },
                // 3828 RoutingServiceFilterTableMatchStart (opcode 1)
                new EventMetadata(metadataId++, ProviderName, "RoutingServiceFilterTableMatchStart", 3828) { OpCode = 1 },
                // 3829 RoutingServiceFilterTableMatchStop (opcode 2)
                new EventMetadata(metadataId++, ProviderName, "RoutingServiceFilterTableMatchStop", 3829) { OpCode = 2 },
                // 3830 RoutingServiceAbortingChannel (opcode 80)
                new EventMetadata(metadataId++, ProviderName, "RoutingServiceAbortingChannel", 3830) { OpCode = 80 },
                // 3831 RoutingServiceHandledException (opcode 84)
                new EventMetadata(metadataId++, ProviderName, "RoutingServiceHandledException", 3831) { OpCode = 84 },
                // 3832 RoutingServiceTransmitSucceeded (opcode 97)
                new EventMetadata(metadataId++, ProviderName, "RoutingServiceTransmitSucceeded", 3832) { OpCode = 97 },
                // 4001 TransportListenerSessionsReceived (opcode 10)
                new EventMetadata(metadataId++, ProviderName, "TransportListenerSessionsReceived", 4001) { OpCode = 10 },
                // 4002 FailFastException (opcode 0)
                new EventMetadata(metadataId++, ProviderName, "FailFastException", 4002),
                // 4003 ServiceStartPipeError (opcode 0)
                new EventMetadata(metadataId++, ProviderName, "ServiceStartPipeError", 4003),
                // 4008 DispatchSessionStart (opcode 1)
                new EventMetadata(metadataId++, ProviderName, "DispatchSessionStart", 4008) { OpCode = 1 },
                // 4010 PendingSessionQueueFull (opcode 0)
                new EventMetadata(metadataId++, ProviderName, "PendingSessionQueueFull", 4010),
                // 4011 MessageQueueRegisterStart (opcode 1)
                new EventMetadata(metadataId++, ProviderName, "MessageQueueRegisterStart", 4011) { OpCode = 1 },
                // 4012 MessageQueueRegisterAbort (opcode 0)
                new EventMetadata(metadataId++, ProviderName, "MessageQueueRegisterAbort", 4012),
                // 4013 MessageQueueUnregisterSucceeded (opcode 2)
                new EventMetadata(metadataId++, ProviderName, "MessageQueueUnregisterSucceeded", 4013) { OpCode = 2 }
            );
        }

        #endregion

        #region Chunk 10 WriteEvents

        private void WriteEvents_Chunk10(EventPipeWriterV5 writer, ref int metadataId, ref int sequenceNumber)
        {
            int __metadataId = metadataId;
            int __sequenceNumber = sequenceNumber;
            writer.WriteEventBlock(w =>
            {
                // 3560 ServiceActivationAvailableMemory - Multidata76TemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3560, s_chunk10_Multidata76TemplateA));
                // 3561 ServiceActivationException - ThreeStringsTemplateEA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3561, s_chunk10_ThreeStringsTemplateEA));
                // 3800 RoutingServiceClosingClient - TwoStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3800, s_chunk10_TwoStringsTemplateA));
                // 3801 RoutingServiceChannelFaulted - TwoStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3801, s_chunk10_TwoStringsTemplateA));
                // 3802 RoutingServiceCompletingOneWay - TwoStringsTemplateEA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3802, s_chunk10_TwoStringsTemplateEA));
                // 3803 RoutingServiceProcessingFailure - ThreeStringsTemplateEA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3803, s_chunk10_ThreeStringsTemplateEA));
                // 3804 RoutingServiceCreatingClientForEndpoint - TwoStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3804, s_chunk10_TwoStringsTemplateA));
                // 3805 RoutingServiceDisplayConfig - FourStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3805, s_chunk10_FourStringsTemplateA));
                // 3807 RoutingServiceCompletingTwoWay - OneStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3807, s_chunk10_OneStringsTemplateA));
                // 3809 RoutingServiceMessageRoutedToEndpoints - ThreeStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3809, s_chunk10_ThreeStringsTemplateA));
                // 3810 RoutingServiceConfigurationApplied - OneStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3810, s_chunk10_OneStringsTemplateA));
                // 3815 RoutingServiceProcessingMessage - FiveStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3815, s_chunk10_FiveStringsTemplateA));
                // 3816 RoutingServiceTransmittingMessage - FourStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3816, s_chunk10_FourStringsTemplateA));
                // 3817 RoutingServiceCommittingTransaction - TwoStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3817, s_chunk10_TwoStringsTemplateA));
                // 3818 RoutingServiceDuplexCallbackException - ThreeStringsTemplateEA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3818, s_chunk10_ThreeStringsTemplateEA));
                // 3819 RoutingServiceMovedToBackup - FourStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3819, s_chunk10_FourStringsTemplateA));
                // 3820 RoutingServiceCreatingTransaction - TwoStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3820, s_chunk10_TwoStringsTemplateA));
                // 3821 RoutingServiceCloseFailed - ThreeStringsTemplateEA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3821, s_chunk10_ThreeStringsTemplateEA));
                // 3822 RoutingServiceSendingResponse - TwoStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3822, s_chunk10_TwoStringsTemplateA));
                // 3823 RoutingServiceSendingFaultResponse - TwoStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3823, s_chunk10_TwoStringsTemplateA));
                // 3824 RoutingServiceCompletingReceiveContext - TwoStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3824, s_chunk10_TwoStringsTemplateA));
                // 3825 RoutingServiceAbandoningReceiveContext - TwoStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3825, s_chunk10_TwoStringsTemplateA));
                // 3826 RoutingServiceUsingExistingTransaction - TwoStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3826, s_chunk10_TwoStringsTemplateA));
                // 3827 RoutingServiceTransmitFailed - ThreeStringsTemplateEA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3827, s_chunk10_ThreeStringsTemplateEA));
                // 3828 RoutingServiceFilterTableMatchStart - OneStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3828, s_chunk10_OneStringsTemplateA));
                // 3829 RoutingServiceFilterTableMatchStop - OneStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3829, s_chunk10_OneStringsTemplateA));
                // 3830 RoutingServiceAbortingChannel - TwoStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3830, s_chunk10_TwoStringsTemplateA));
                // 3831 RoutingServiceHandledException - TwoStringsTemplateEA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3831, s_chunk10_TwoStringsTemplateEA));
                // 3832 RoutingServiceTransmitSucceeded - FourStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(3832, s_chunk10_FourStringsTemplateA));
                // 4001 TransportListenerSessionsReceived - Multidata77TemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4001, s_chunk10_Multidata77TemplateA));
                // 4002 FailFastException - TwoStringsTemplateEA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4002, s_chunk10_TwoStringsTemplateEA));
                // 4003 ServiceStartPipeError - Multidata78TemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4003, s_chunk10_Multidata78TemplateA));
                // 4008 DispatchSessionStart - OneStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4008, s_chunk10_OneStringsTemplateA));
                // 4010 PendingSessionQueueFull - Multidata79TemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4010, s_chunk10_Multidata79TemplateA));
                // 4011 MessageQueueRegisterStart - OneStringsTemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4011, s_chunk10_OneStringsTemplateA));
                // 4012 MessageQueueRegisterAbort - Multidata81TemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4012, s_chunk10_Multidata81TemplateA));
                // 4013 MessageQueueUnregisterSucceeded - Multidata48TemplateA
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(4013, s_chunk10_Multidata48TemplateA));
            });
        
            metadataId = __metadataId;
            sequenceNumber = __sequenceNumber;
        }

        #endregion

        #region Chunk 10 Subscribe

        private void Subscribe_Chunk10(ApplicationServerTraceEventParser parser, Dictionary<string, Dictionary<string, object>> firedEvents)
        {
            // 3560 ServiceActivationAvailableMemory - Multidata76TemplateA
            parser.ServiceActivationAvailableMemory += data =>
            {
                firedEvents["ServiceActivationAvailableMemory"] = new Dictionary<string, object>
                {
                    { "availableMemoryBytes", data.availableMemoryBytes },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3561 ServiceActivationException - ThreeStringsTemplateEA
            parser.ServiceActivationException += data =>
            {
                firedEvents["ServiceActivationException"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3800 RoutingServiceClosingClient - TwoStringsTemplateA
            parser.RoutingServiceClosingClient += data =>
            {
                firedEvents["RoutingServiceClosingClient"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3801 RoutingServiceChannelFaulted - TwoStringsTemplateA
            parser.RoutingServiceChannelFaulted += data =>
            {
                firedEvents["RoutingServiceChannelFaulted"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3802 RoutingServiceCompletingOneWay - TwoStringsTemplateEA
            parser.RoutingServiceCompletingOneWay += data =>
            {
                firedEvents["RoutingServiceCompletingOneWay"] = new Dictionary<string, object>
                {
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3803 RoutingServiceProcessingFailure - ThreeStringsTemplateEA
            parser.RoutingServiceProcessingFailure += data =>
            {
                firedEvents["RoutingServiceProcessingFailure"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3804 RoutingServiceCreatingClientForEndpoint - TwoStringsTemplateA
            parser.RoutingServiceCreatingClientForEndpoint += data =>
            {
                firedEvents["RoutingServiceCreatingClientForEndpoint"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3805 RoutingServiceDisplayConfig - FourStringsTemplateA
            parser.RoutingServiceDisplayConfig += data =>
            {
                firedEvents["RoutingServiceDisplayConfig"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "data2", data.data2 },
                    { "data3", data.data3 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3807 RoutingServiceCompletingTwoWay - OneStringsTemplateA
            parser.RoutingServiceCompletingTwoWay += data =>
            {
                firedEvents["RoutingServiceCompletingTwoWay"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3809 RoutingServiceMessageRoutedToEndpoints - ThreeStringsTemplateA
            parser.RoutingServiceMessageRoutedToEndpoints += data =>
            {
                firedEvents["RoutingServiceMessageRoutedToEndpoints"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "data2", data.data2 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3810 RoutingServiceConfigurationApplied - OneStringsTemplateA
            parser.RoutingServiceConfigurationApplied += data =>
            {
                firedEvents["RoutingServiceConfigurationApplied"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3815 RoutingServiceProcessingMessage - FiveStringsTemplateA
            parser.RoutingServiceProcessingMessage += data =>
            {
                firedEvents["RoutingServiceProcessingMessage"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "data2", data.data2 },
                    { "data3", data.data3 },
                    { "data4", data.data4 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3816 RoutingServiceTransmittingMessage - FourStringsTemplateA
            parser.RoutingServiceTransmittingMessage += data =>
            {
                firedEvents["RoutingServiceTransmittingMessage"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "data2", data.data2 },
                    { "data3", data.data3 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3817 RoutingServiceCommittingTransaction - TwoStringsTemplateA
            parser.RoutingServiceCommittingTransaction += data =>
            {
                firedEvents["RoutingServiceCommittingTransaction"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3818 RoutingServiceDuplexCallbackException - ThreeStringsTemplateEA
            parser.RoutingServiceDuplexCallbackException += data =>
            {
                firedEvents["RoutingServiceDuplexCallbackException"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3819 RoutingServiceMovedToBackup - FourStringsTemplateA
            parser.RoutingServiceMovedToBackup += data =>
            {
                firedEvents["RoutingServiceMovedToBackup"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "data2", data.data2 },
                    { "data3", data.data3 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3820 RoutingServiceCreatingTransaction - TwoStringsTemplateA
            parser.RoutingServiceCreatingTransaction += data =>
            {
                firedEvents["RoutingServiceCreatingTransaction"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3821 RoutingServiceCloseFailed - ThreeStringsTemplateEA
            parser.RoutingServiceCloseFailed += data =>
            {
                firedEvents["RoutingServiceCloseFailed"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3822 RoutingServiceSendingResponse - TwoStringsTemplateA
            parser.RoutingServiceSendingResponse += data =>
            {
                firedEvents["RoutingServiceSendingResponse"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3823 RoutingServiceSendingFaultResponse - TwoStringsTemplateA
            parser.RoutingServiceSendingFaultResponse += data =>
            {
                firedEvents["RoutingServiceSendingFaultResponse"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3824 RoutingServiceCompletingReceiveContext - TwoStringsTemplateA
            parser.RoutingServiceCompletingReceiveContext += data =>
            {
                firedEvents["RoutingServiceCompletingReceiveContext"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3825 RoutingServiceAbandoningReceiveContext - TwoStringsTemplateA
            parser.RoutingServiceAbandoningReceiveContext += data =>
            {
                firedEvents["RoutingServiceAbandoningReceiveContext"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3826 RoutingServiceUsingExistingTransaction - TwoStringsTemplateA
            parser.RoutingServiceUsingExistingTransaction += data =>
            {
                firedEvents["RoutingServiceUsingExistingTransaction"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3827 RoutingServiceTransmitFailed - ThreeStringsTemplateEA
            parser.RoutingServiceTransmitFailed += data =>
            {
                firedEvents["RoutingServiceTransmitFailed"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3828 RoutingServiceFilterTableMatchStart - OneStringsTemplateA
            parser.RoutingServiceFilterTableMatchStart += data =>
            {
                firedEvents["RoutingServiceFilterTableMatchStart"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3829 RoutingServiceFilterTableMatchStop - OneStringsTemplateA
            parser.RoutingServiceFilterTableMatchStop += data =>
            {
                firedEvents["RoutingServiceFilterTableMatchStop"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3830 RoutingServiceAbortingChannel - TwoStringsTemplateA
            parser.RoutingServiceAbortingChannel += data =>
            {
                firedEvents["RoutingServiceAbortingChannel"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3831 RoutingServiceHandledException - TwoStringsTemplateEA
            parser.RoutingServiceHandledException += data =>
            {
                firedEvents["RoutingServiceHandledException"] = new Dictionary<string, object>
                {
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3832 RoutingServiceTransmitSucceeded - FourStringsTemplateA
            parser.RoutingServiceTransmitSucceeded += data =>
            {
                firedEvents["RoutingServiceTransmitSucceeded"] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "data2", data.data2 },
                    { "data3", data.data3 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 4001 TransportListenerSessionsReceived - Multidata77TemplateA
            parser.TransportListenerSessionsReceived += data =>
            {
                firedEvents["TransportListenerSessionsReceived"] = new Dictionary<string, object>
                {
                    { "via", data.via },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 4002 FailFastException - TwoStringsTemplateEA
            parser.FailFastException += data =>
            {
                firedEvents["FailFastException"] = new Dictionary<string, object>
                {
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 4003 ServiceStartPipeError - Multidata78TemplateA
            parser.ServiceStartPipeError += data =>
            {
                firedEvents["ServiceStartPipeError"] = new Dictionary<string, object>
                {
                    { "Endpoint", data.Endpoint },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 4008 DispatchSessionStart - OneStringsTemplateA
            parser.DispatchSessionStart += data =>
            {
                firedEvents["DispatchSessionStart"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            // 4010 PendingSessionQueueFull - Multidata79TemplateA
            parser.PendingSessionQueueFull += data =>
            {
                firedEvents["PendingSessionQueueFull"] = new Dictionary<string, object>
                {
                    { "Uri", data.Uri },
                    { "count", data.count },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 4011 MessageQueueRegisterStart - OneStringsTemplateA
            parser.MessageQueueRegisterStart += data =>
            {
                firedEvents["MessageQueueRegisterStart"] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            // 4012 MessageQueueRegisterAbort - Multidata81TemplateA
            parser.MessageQueueRegisterAbort += data =>
            {
                firedEvents["MessageQueueRegisterAbort"] = new Dictionary<string, object>
                {
                    { "Status", data.Status },
                    { "Uri", data.Uri },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 4013 MessageQueueUnregisterSucceeded - Multidata48TemplateA
            parser.MessageQueueUnregisterSucceeded += data =>
            {
                firedEvents["MessageQueueUnregisterSucceeded"] = new Dictionary<string, object>
                {
                    { "Uri", data.Uri },
                    { "AppDomain", data.AppDomain },
                };
            };
        }

        #endregion

        #region Chunk 10 Validate

        private void Validate_Chunk10(Dictionary<string, Dictionary<string, object>> firedEvents)
        {
            // 3560 ServiceActivationAvailableMemory - Multidata76TemplateA
            Assert.True(firedEvents.ContainsKey("ServiceActivationAvailableMemory"), "Event ServiceActivationAvailableMemory did not fire.");
            Assert.Equal(TestInt64(3560, 0), (long)firedEvents["ServiceActivationAvailableMemory"]["availableMemoryBytes"]);
            Assert.Equal(TestString(3560, "AppDomain"), (string)firedEvents["ServiceActivationAvailableMemory"]["AppDomain"]);

            // 3561 ServiceActivationException - ThreeStringsTemplateEA
            Assert.True(firedEvents.ContainsKey("ServiceActivationException"), "Event ServiceActivationException did not fire.");
            Assert.Equal(TestString(3561, "data1"), (string)firedEvents["ServiceActivationException"]["data1"]);
            Assert.Equal(TestString(3561, "SerializedException"), (string)firedEvents["ServiceActivationException"]["SerializedException"]);
            Assert.Equal(TestString(3561, "AppDomain"), (string)firedEvents["ServiceActivationException"]["AppDomain"]);

            // 3800 RoutingServiceClosingClient - TwoStringsTemplateA
            Assert.True(firedEvents.ContainsKey("RoutingServiceClosingClient"), "Event RoutingServiceClosingClient did not fire.");
            Assert.Equal(TestString(3800, "data1"), (string)firedEvents["RoutingServiceClosingClient"]["data1"]);
            Assert.Equal(TestString(3800, "AppDomain"), (string)firedEvents["RoutingServiceClosingClient"]["AppDomain"]);

            // 3801 RoutingServiceChannelFaulted - TwoStringsTemplateA
            Assert.True(firedEvents.ContainsKey("RoutingServiceChannelFaulted"), "Event RoutingServiceChannelFaulted did not fire.");
            Assert.Equal(TestString(3801, "data1"), (string)firedEvents["RoutingServiceChannelFaulted"]["data1"]);
            Assert.Equal(TestString(3801, "AppDomain"), (string)firedEvents["RoutingServiceChannelFaulted"]["AppDomain"]);

            // 3802 RoutingServiceCompletingOneWay - TwoStringsTemplateEA
            Assert.True(firedEvents.ContainsKey("RoutingServiceCompletingOneWay"), "Event RoutingServiceCompletingOneWay did not fire.");
            Assert.Equal(TestString(3802, "SerializedException"), (string)firedEvents["RoutingServiceCompletingOneWay"]["SerializedException"]);
            Assert.Equal(TestString(3802, "AppDomain"), (string)firedEvents["RoutingServiceCompletingOneWay"]["AppDomain"]);

            // 3803 RoutingServiceProcessingFailure - ThreeStringsTemplateEA
            Assert.True(firedEvents.ContainsKey("RoutingServiceProcessingFailure"), "Event RoutingServiceProcessingFailure did not fire.");
            Assert.Equal(TestString(3803, "data1"), (string)firedEvents["RoutingServiceProcessingFailure"]["data1"]);
            Assert.Equal(TestString(3803, "SerializedException"), (string)firedEvents["RoutingServiceProcessingFailure"]["SerializedException"]);
            Assert.Equal(TestString(3803, "AppDomain"), (string)firedEvents["RoutingServiceProcessingFailure"]["AppDomain"]);

            // 3804 RoutingServiceCreatingClientForEndpoint - TwoStringsTemplateA
            Assert.True(firedEvents.ContainsKey("RoutingServiceCreatingClientForEndpoint"), "Event RoutingServiceCreatingClientForEndpoint did not fire.");
            Assert.Equal(TestString(3804, "data1"), (string)firedEvents["RoutingServiceCreatingClientForEndpoint"]["data1"]);
            Assert.Equal(TestString(3804, "AppDomain"), (string)firedEvents["RoutingServiceCreatingClientForEndpoint"]["AppDomain"]);

            // 3805 RoutingServiceDisplayConfig - FourStringsTemplateA
            Assert.True(firedEvents.ContainsKey("RoutingServiceDisplayConfig"), "Event RoutingServiceDisplayConfig did not fire.");
            Assert.Equal(TestString(3805, "data1"), (string)firedEvents["RoutingServiceDisplayConfig"]["data1"]);
            Assert.Equal(TestString(3805, "data2"), (string)firedEvents["RoutingServiceDisplayConfig"]["data2"]);
            Assert.Equal(TestString(3805, "data3"), (string)firedEvents["RoutingServiceDisplayConfig"]["data3"]);
            Assert.Equal(TestString(3805, "AppDomain"), (string)firedEvents["RoutingServiceDisplayConfig"]["AppDomain"]);

            // 3807 RoutingServiceCompletingTwoWay - OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("RoutingServiceCompletingTwoWay"), "Event RoutingServiceCompletingTwoWay did not fire.");
            Assert.Equal(TestString(3807, "AppDomain"), (string)firedEvents["RoutingServiceCompletingTwoWay"]["AppDomain"]);

            // 3809 RoutingServiceMessageRoutedToEndpoints - ThreeStringsTemplateA
            Assert.True(firedEvents.ContainsKey("RoutingServiceMessageRoutedToEndpoints"), "Event RoutingServiceMessageRoutedToEndpoints did not fire.");
            Assert.Equal(TestString(3809, "data1"), (string)firedEvents["RoutingServiceMessageRoutedToEndpoints"]["data1"]);
            Assert.Equal(TestString(3809, "data2"), (string)firedEvents["RoutingServiceMessageRoutedToEndpoints"]["data2"]);
            Assert.Equal(TestString(3809, "AppDomain"), (string)firedEvents["RoutingServiceMessageRoutedToEndpoints"]["AppDomain"]);

            // 3810 RoutingServiceConfigurationApplied - OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("RoutingServiceConfigurationApplied"), "Event RoutingServiceConfigurationApplied did not fire.");
            Assert.Equal(TestString(3810, "AppDomain"), (string)firedEvents["RoutingServiceConfigurationApplied"]["AppDomain"]);

            // 3815 RoutingServiceProcessingMessage - FiveStringsTemplateA
            Assert.True(firedEvents.ContainsKey("RoutingServiceProcessingMessage"), "Event RoutingServiceProcessingMessage did not fire.");
            Assert.Equal(TestString(3815, "data1"), (string)firedEvents["RoutingServiceProcessingMessage"]["data1"]);
            Assert.Equal(TestString(3815, "data2"), (string)firedEvents["RoutingServiceProcessingMessage"]["data2"]);
            Assert.Equal(TestString(3815, "data3"), (string)firedEvents["RoutingServiceProcessingMessage"]["data3"]);
            Assert.Equal(TestString(3815, "data4"), (string)firedEvents["RoutingServiceProcessingMessage"]["data4"]);
            Assert.Equal(TestString(3815, "AppDomain"), (string)firedEvents["RoutingServiceProcessingMessage"]["AppDomain"]);

            // 3816 RoutingServiceTransmittingMessage - FourStringsTemplateA
            Assert.True(firedEvents.ContainsKey("RoutingServiceTransmittingMessage"), "Event RoutingServiceTransmittingMessage did not fire.");
            Assert.Equal(TestString(3816, "data1"), (string)firedEvents["RoutingServiceTransmittingMessage"]["data1"]);
            Assert.Equal(TestString(3816, "data2"), (string)firedEvents["RoutingServiceTransmittingMessage"]["data2"]);
            Assert.Equal(TestString(3816, "data3"), (string)firedEvents["RoutingServiceTransmittingMessage"]["data3"]);
            Assert.Equal(TestString(3816, "AppDomain"), (string)firedEvents["RoutingServiceTransmittingMessage"]["AppDomain"]);

            // 3817 RoutingServiceCommittingTransaction - TwoStringsTemplateA
            Assert.True(firedEvents.ContainsKey("RoutingServiceCommittingTransaction"), "Event RoutingServiceCommittingTransaction did not fire.");
            Assert.Equal(TestString(3817, "data1"), (string)firedEvents["RoutingServiceCommittingTransaction"]["data1"]);
            Assert.Equal(TestString(3817, "AppDomain"), (string)firedEvents["RoutingServiceCommittingTransaction"]["AppDomain"]);

            // 3818 RoutingServiceDuplexCallbackException - ThreeStringsTemplateEA
            Assert.True(firedEvents.ContainsKey("RoutingServiceDuplexCallbackException"), "Event RoutingServiceDuplexCallbackException did not fire.");
            Assert.Equal(TestString(3818, "data1"), (string)firedEvents["RoutingServiceDuplexCallbackException"]["data1"]);
            Assert.Equal(TestString(3818, "SerializedException"), (string)firedEvents["RoutingServiceDuplexCallbackException"]["SerializedException"]);
            Assert.Equal(TestString(3818, "AppDomain"), (string)firedEvents["RoutingServiceDuplexCallbackException"]["AppDomain"]);

            // 3819 RoutingServiceMovedToBackup - FourStringsTemplateA
            Assert.True(firedEvents.ContainsKey("RoutingServiceMovedToBackup"), "Event RoutingServiceMovedToBackup did not fire.");
            Assert.Equal(TestString(3819, "data1"), (string)firedEvents["RoutingServiceMovedToBackup"]["data1"]);
            Assert.Equal(TestString(3819, "data2"), (string)firedEvents["RoutingServiceMovedToBackup"]["data2"]);
            Assert.Equal(TestString(3819, "data3"), (string)firedEvents["RoutingServiceMovedToBackup"]["data3"]);
            Assert.Equal(TestString(3819, "AppDomain"), (string)firedEvents["RoutingServiceMovedToBackup"]["AppDomain"]);

            // 3820 RoutingServiceCreatingTransaction - TwoStringsTemplateA
            Assert.True(firedEvents.ContainsKey("RoutingServiceCreatingTransaction"), "Event RoutingServiceCreatingTransaction did not fire.");
            Assert.Equal(TestString(3820, "data1"), (string)firedEvents["RoutingServiceCreatingTransaction"]["data1"]);
            Assert.Equal(TestString(3820, "AppDomain"), (string)firedEvents["RoutingServiceCreatingTransaction"]["AppDomain"]);

            // 3821 RoutingServiceCloseFailed - ThreeStringsTemplateEA
            Assert.True(firedEvents.ContainsKey("RoutingServiceCloseFailed"), "Event RoutingServiceCloseFailed did not fire.");
            Assert.Equal(TestString(3821, "data1"), (string)firedEvents["RoutingServiceCloseFailed"]["data1"]);
            Assert.Equal(TestString(3821, "SerializedException"), (string)firedEvents["RoutingServiceCloseFailed"]["SerializedException"]);
            Assert.Equal(TestString(3821, "AppDomain"), (string)firedEvents["RoutingServiceCloseFailed"]["AppDomain"]);

            // 3822 RoutingServiceSendingResponse - TwoStringsTemplateA
            Assert.True(firedEvents.ContainsKey("RoutingServiceSendingResponse"), "Event RoutingServiceSendingResponse did not fire.");
            Assert.Equal(TestString(3822, "data1"), (string)firedEvents["RoutingServiceSendingResponse"]["data1"]);
            Assert.Equal(TestString(3822, "AppDomain"), (string)firedEvents["RoutingServiceSendingResponse"]["AppDomain"]);

            // 3823 RoutingServiceSendingFaultResponse - TwoStringsTemplateA
            Assert.True(firedEvents.ContainsKey("RoutingServiceSendingFaultResponse"), "Event RoutingServiceSendingFaultResponse did not fire.");
            Assert.Equal(TestString(3823, "data1"), (string)firedEvents["RoutingServiceSendingFaultResponse"]["data1"]);
            Assert.Equal(TestString(3823, "AppDomain"), (string)firedEvents["RoutingServiceSendingFaultResponse"]["AppDomain"]);

            // 3824 RoutingServiceCompletingReceiveContext - TwoStringsTemplateA
            Assert.True(firedEvents.ContainsKey("RoutingServiceCompletingReceiveContext"), "Event RoutingServiceCompletingReceiveContext did not fire.");
            Assert.Equal(TestString(3824, "data1"), (string)firedEvents["RoutingServiceCompletingReceiveContext"]["data1"]);
            Assert.Equal(TestString(3824, "AppDomain"), (string)firedEvents["RoutingServiceCompletingReceiveContext"]["AppDomain"]);

            // 3825 RoutingServiceAbandoningReceiveContext - TwoStringsTemplateA
            Assert.True(firedEvents.ContainsKey("RoutingServiceAbandoningReceiveContext"), "Event RoutingServiceAbandoningReceiveContext did not fire.");
            Assert.Equal(TestString(3825, "data1"), (string)firedEvents["RoutingServiceAbandoningReceiveContext"]["data1"]);
            Assert.Equal(TestString(3825, "AppDomain"), (string)firedEvents["RoutingServiceAbandoningReceiveContext"]["AppDomain"]);

            // 3826 RoutingServiceUsingExistingTransaction - TwoStringsTemplateA
            Assert.True(firedEvents.ContainsKey("RoutingServiceUsingExistingTransaction"), "Event RoutingServiceUsingExistingTransaction did not fire.");
            Assert.Equal(TestString(3826, "data1"), (string)firedEvents["RoutingServiceUsingExistingTransaction"]["data1"]);
            Assert.Equal(TestString(3826, "AppDomain"), (string)firedEvents["RoutingServiceUsingExistingTransaction"]["AppDomain"]);

            // 3827 RoutingServiceTransmitFailed - ThreeStringsTemplateEA
            Assert.True(firedEvents.ContainsKey("RoutingServiceTransmitFailed"), "Event RoutingServiceTransmitFailed did not fire.");
            Assert.Equal(TestString(3827, "data1"), (string)firedEvents["RoutingServiceTransmitFailed"]["data1"]);
            Assert.Equal(TestString(3827, "SerializedException"), (string)firedEvents["RoutingServiceTransmitFailed"]["SerializedException"]);
            Assert.Equal(TestString(3827, "AppDomain"), (string)firedEvents["RoutingServiceTransmitFailed"]["AppDomain"]);

            // 3828 RoutingServiceFilterTableMatchStart - OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("RoutingServiceFilterTableMatchStart"), "Event RoutingServiceFilterTableMatchStart did not fire.");
            Assert.Equal(TestString(3828, "AppDomain"), (string)firedEvents["RoutingServiceFilterTableMatchStart"]["AppDomain"]);

            // 3829 RoutingServiceFilterTableMatchStop - OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("RoutingServiceFilterTableMatchStop"), "Event RoutingServiceFilterTableMatchStop did not fire.");
            Assert.Equal(TestString(3829, "AppDomain"), (string)firedEvents["RoutingServiceFilterTableMatchStop"]["AppDomain"]);

            // 3830 RoutingServiceAbortingChannel - TwoStringsTemplateA
            Assert.True(firedEvents.ContainsKey("RoutingServiceAbortingChannel"), "Event RoutingServiceAbortingChannel did not fire.");
            Assert.Equal(TestString(3830, "data1"), (string)firedEvents["RoutingServiceAbortingChannel"]["data1"]);
            Assert.Equal(TestString(3830, "AppDomain"), (string)firedEvents["RoutingServiceAbortingChannel"]["AppDomain"]);

            // 3831 RoutingServiceHandledException - TwoStringsTemplateEA
            Assert.True(firedEvents.ContainsKey("RoutingServiceHandledException"), "Event RoutingServiceHandledException did not fire.");
            Assert.Equal(TestString(3831, "SerializedException"), (string)firedEvents["RoutingServiceHandledException"]["SerializedException"]);
            Assert.Equal(TestString(3831, "AppDomain"), (string)firedEvents["RoutingServiceHandledException"]["AppDomain"]);

            // 3832 RoutingServiceTransmitSucceeded - FourStringsTemplateA
            Assert.True(firedEvents.ContainsKey("RoutingServiceTransmitSucceeded"), "Event RoutingServiceTransmitSucceeded did not fire.");
            Assert.Equal(TestString(3832, "data1"), (string)firedEvents["RoutingServiceTransmitSucceeded"]["data1"]);
            Assert.Equal(TestString(3832, "data2"), (string)firedEvents["RoutingServiceTransmitSucceeded"]["data2"]);
            Assert.Equal(TestString(3832, "data3"), (string)firedEvents["RoutingServiceTransmitSucceeded"]["data3"]);
            Assert.Equal(TestString(3832, "AppDomain"), (string)firedEvents["RoutingServiceTransmitSucceeded"]["AppDomain"]);

            // 4001 TransportListenerSessionsReceived - Multidata77TemplateA
            Assert.True(firedEvents.ContainsKey("TransportListenerSessionsReceived"), "Event TransportListenerSessionsReceived did not fire.");
            Assert.Equal(TestString(4001, "via"), (string)firedEvents["TransportListenerSessionsReceived"]["via"]);
            Assert.Equal(TestString(4001, "AppDomain"), (string)firedEvents["TransportListenerSessionsReceived"]["AppDomain"]);

            // 4002 FailFastException - TwoStringsTemplateEA
            Assert.True(firedEvents.ContainsKey("FailFastException"), "Event FailFastException did not fire.");
            Assert.Equal(TestString(4002, "SerializedException"), (string)firedEvents["FailFastException"]["SerializedException"]);
            Assert.Equal(TestString(4002, "AppDomain"), (string)firedEvents["FailFastException"]["AppDomain"]);

            // 4003 ServiceStartPipeError - Multidata78TemplateA
            Assert.True(firedEvents.ContainsKey("ServiceStartPipeError"), "Event ServiceStartPipeError did not fire.");
            Assert.Equal(TestString(4003, "Endpoint"), (string)firedEvents["ServiceStartPipeError"]["Endpoint"]);
            Assert.Equal(TestString(4003, "AppDomain"), (string)firedEvents["ServiceStartPipeError"]["AppDomain"]);

            // 4008 DispatchSessionStart - OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("DispatchSessionStart"), "Event DispatchSessionStart did not fire.");
            Assert.Equal(TestString(4008, "AppDomain"), (string)firedEvents["DispatchSessionStart"]["AppDomain"]);

            // 4010 PendingSessionQueueFull - Multidata79TemplateA
            Assert.True(firedEvents.ContainsKey("PendingSessionQueueFull"), "Event PendingSessionQueueFull did not fire.");
            Assert.Equal(TestString(4010, "Uri"), (string)firedEvents["PendingSessionQueueFull"]["Uri"]);
            Assert.Equal(TestInt32(4010, 1), (int)firedEvents["PendingSessionQueueFull"]["count"]);
            Assert.Equal(TestString(4010, "AppDomain"), (string)firedEvents["PendingSessionQueueFull"]["AppDomain"]);

            // 4011 MessageQueueRegisterStart - OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey("MessageQueueRegisterStart"), "Event MessageQueueRegisterStart did not fire.");
            Assert.Equal(TestString(4011, "AppDomain"), (string)firedEvents["MessageQueueRegisterStart"]["AppDomain"]);

            // 4012 MessageQueueRegisterAbort - Multidata81TemplateA
            Assert.True(firedEvents.ContainsKey("MessageQueueRegisterAbort"), "Event MessageQueueRegisterAbort did not fire.");
            Assert.Equal(TestString(4012, "Status"), (string)firedEvents["MessageQueueRegisterAbort"]["Status"]);
            Assert.Equal(TestString(4012, "Uri"), (string)firedEvents["MessageQueueRegisterAbort"]["Uri"]);
            Assert.Equal(TestString(4012, "AppDomain"), (string)firedEvents["MessageQueueRegisterAbort"]["AppDomain"]);

            // 4013 MessageQueueUnregisterSucceeded - Multidata48TemplateA
            Assert.True(firedEvents.ContainsKey("MessageQueueUnregisterSucceeded"), "Event MessageQueueUnregisterSucceeded did not fire.");
            Assert.Equal(TestString(4013, "Uri"), (string)firedEvents["MessageQueueUnregisterSucceeded"]["Uri"]);
            Assert.Equal(TestString(4013, "AppDomain"), (string)firedEvents["MessageQueueUnregisterSucceeded"]["AppDomain"]);
        }

        #endregion
    }
}
