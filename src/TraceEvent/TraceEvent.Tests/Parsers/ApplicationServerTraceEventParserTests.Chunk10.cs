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

        private void Subscribe_Chunk10(ApplicationServerTraceEventParser parser, Dictionary<int, Dictionary<string, object>> firedEvents)
        {
            // 3560 ServiceActivationAvailableMemory - Multidata76TemplateA
            parser.ServiceActivationAvailableMemory += data =>
            {
                firedEvents[3560] = new Dictionary<string, object>
                {
                    { "availableMemoryBytes", data.availableMemoryBytes },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3561 ServiceActivationException - ThreeStringsTemplateEA
            parser.ServiceActivationException += data =>
            {
                firedEvents[3561] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3800 RoutingServiceClosingClient - TwoStringsTemplateA
            parser.RoutingServiceClosingClient += data =>
            {
                firedEvents[3800] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3801 RoutingServiceChannelFaulted - TwoStringsTemplateA
            parser.RoutingServiceChannelFaulted += data =>
            {
                firedEvents[3801] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3802 RoutingServiceCompletingOneWay - TwoStringsTemplateEA
            parser.RoutingServiceCompletingOneWay += data =>
            {
                firedEvents[3802] = new Dictionary<string, object>
                {
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3803 RoutingServiceProcessingFailure - ThreeStringsTemplateEA
            parser.RoutingServiceProcessingFailure += data =>
            {
                firedEvents[3803] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3804 RoutingServiceCreatingClientForEndpoint - TwoStringsTemplateA
            parser.RoutingServiceCreatingClientForEndpoint += data =>
            {
                firedEvents[3804] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3805 RoutingServiceDisplayConfig - FourStringsTemplateA
            parser.RoutingServiceDisplayConfig += data =>
            {
                firedEvents[3805] = new Dictionary<string, object>
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
                firedEvents[3807] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3809 RoutingServiceMessageRoutedToEndpoints - ThreeStringsTemplateA
            parser.RoutingServiceMessageRoutedToEndpoints += data =>
            {
                firedEvents[3809] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "data2", data.data2 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3810 RoutingServiceConfigurationApplied - OneStringsTemplateA
            parser.RoutingServiceConfigurationApplied += data =>
            {
                firedEvents[3810] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3815 RoutingServiceProcessingMessage - FiveStringsTemplateA
            parser.RoutingServiceProcessingMessage += data =>
            {
                firedEvents[3815] = new Dictionary<string, object>
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
                firedEvents[3816] = new Dictionary<string, object>
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
                firedEvents[3817] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3818 RoutingServiceDuplexCallbackException - ThreeStringsTemplateEA
            parser.RoutingServiceDuplexCallbackException += data =>
            {
                firedEvents[3818] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3819 RoutingServiceMovedToBackup - FourStringsTemplateA
            parser.RoutingServiceMovedToBackup += data =>
            {
                firedEvents[3819] = new Dictionary<string, object>
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
                firedEvents[3820] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3821 RoutingServiceCloseFailed - ThreeStringsTemplateEA
            parser.RoutingServiceCloseFailed += data =>
            {
                firedEvents[3821] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3822 RoutingServiceSendingResponse - TwoStringsTemplateA
            parser.RoutingServiceSendingResponse += data =>
            {
                firedEvents[3822] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3823 RoutingServiceSendingFaultResponse - TwoStringsTemplateA
            parser.RoutingServiceSendingFaultResponse += data =>
            {
                firedEvents[3823] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3824 RoutingServiceCompletingReceiveContext - TwoStringsTemplateA
            parser.RoutingServiceCompletingReceiveContext += data =>
            {
                firedEvents[3824] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3825 RoutingServiceAbandoningReceiveContext - TwoStringsTemplateA
            parser.RoutingServiceAbandoningReceiveContext += data =>
            {
                firedEvents[3825] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3826 RoutingServiceUsingExistingTransaction - TwoStringsTemplateA
            parser.RoutingServiceUsingExistingTransaction += data =>
            {
                firedEvents[3826] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3827 RoutingServiceTransmitFailed - ThreeStringsTemplateEA
            parser.RoutingServiceTransmitFailed += data =>
            {
                firedEvents[3827] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3828 RoutingServiceFilterTableMatchStart - OneStringsTemplateA
            parser.RoutingServiceFilterTableMatchStart += data =>
            {
                firedEvents[3828] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3829 RoutingServiceFilterTableMatchStop - OneStringsTemplateA
            parser.RoutingServiceFilterTableMatchStop += data =>
            {
                firedEvents[3829] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3830 RoutingServiceAbortingChannel - TwoStringsTemplateA
            parser.RoutingServiceAbortingChannel += data =>
            {
                firedEvents[3830] = new Dictionary<string, object>
                {
                    { "data1", data.data1 },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3831 RoutingServiceHandledException - TwoStringsTemplateEA
            parser.RoutingServiceHandledException += data =>
            {
                firedEvents[3831] = new Dictionary<string, object>
                {
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 3832 RoutingServiceTransmitSucceeded - FourStringsTemplateA
            parser.RoutingServiceTransmitSucceeded += data =>
            {
                firedEvents[3832] = new Dictionary<string, object>
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
                firedEvents[4001] = new Dictionary<string, object>
                {
                    { "via", data.via },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 4002 FailFastException - TwoStringsTemplateEA
            parser.FailFastException += data =>
            {
                firedEvents[4002] = new Dictionary<string, object>
                {
                    { "SerializedException", data.SerializedException },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 4003 ServiceStartPipeError - Multidata78TemplateA
            parser.ServiceStartPipeError += data =>
            {
                firedEvents[4003] = new Dictionary<string, object>
                {
                    { "Endpoint", data.Endpoint },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 4008 DispatchSessionStart - OneStringsTemplateA
            parser.DispatchSessionStart += data =>
            {
                firedEvents[4008] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            // 4010 PendingSessionQueueFull - Multidata79TemplateA
            parser.PendingSessionQueueFull += data =>
            {
                firedEvents[4010] = new Dictionary<string, object>
                {
                    { "Uri", data.Uri },
                    { "count", data.count },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 4011 MessageQueueRegisterStart - OneStringsTemplateA
            parser.MessageQueueRegisterStart += data =>
            {
                firedEvents[4011] = new Dictionary<string, object>
                {
                    { "AppDomain", data.AppDomain },
                };
            };

            // 4012 MessageQueueRegisterAbort - Multidata81TemplateA
            parser.MessageQueueRegisterAbort += data =>
            {
                firedEvents[4012] = new Dictionary<string, object>
                {
                    { "Status", data.Status },
                    { "Uri", data.Uri },
                    { "AppDomain", data.AppDomain },
                };
            };

            // 4013 MessageQueueUnregisterSucceeded - Multidata48TemplateA
            parser.MessageQueueUnregisterSucceeded += data =>
            {
                firedEvents[4013] = new Dictionary<string, object>
                {
                    { "Uri", data.Uri },
                    { "AppDomain", data.AppDomain },
                };
            };
        }

        #endregion

        #region Chunk 10 Validate

        private void Validate_Chunk10(Dictionary<int, Dictionary<string, object>> firedEvents)
        {
            // 3560 ServiceActivationAvailableMemory - Multidata76TemplateA
            Assert.True(firedEvents.ContainsKey(3560), "Event 3560 (ServiceActivationAvailableMemory) did not fire.");
            Assert.Equal(TestInt64(3560, 0), (long)firedEvents[3560]["availableMemoryBytes"]);
            Assert.Equal(TestString(3560, "AppDomain"), (string)firedEvents[3560]["AppDomain"]);

            // 3561 ServiceActivationException - ThreeStringsTemplateEA
            Assert.True(firedEvents.ContainsKey(3561), "Event 3561 (ServiceActivationException) did not fire.");
            Assert.Equal(TestString(3561, "data1"), (string)firedEvents[3561]["data1"]);
            Assert.Equal(TestString(3561, "SerializedException"), (string)firedEvents[3561]["SerializedException"]);
            Assert.Equal(TestString(3561, "AppDomain"), (string)firedEvents[3561]["AppDomain"]);

            // 3800 RoutingServiceClosingClient - TwoStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3800), "Event 3800 (RoutingServiceClosingClient) did not fire.");
            Assert.Equal(TestString(3800, "data1"), (string)firedEvents[3800]["data1"]);
            Assert.Equal(TestString(3800, "AppDomain"), (string)firedEvents[3800]["AppDomain"]);

            // 3801 RoutingServiceChannelFaulted - TwoStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3801), "Event 3801 (RoutingServiceChannelFaulted) did not fire.");
            Assert.Equal(TestString(3801, "data1"), (string)firedEvents[3801]["data1"]);
            Assert.Equal(TestString(3801, "AppDomain"), (string)firedEvents[3801]["AppDomain"]);

            // 3802 RoutingServiceCompletingOneWay - TwoStringsTemplateEA
            Assert.True(firedEvents.ContainsKey(3802), "Event 3802 (RoutingServiceCompletingOneWay) did not fire.");
            Assert.Equal(TestString(3802, "SerializedException"), (string)firedEvents[3802]["SerializedException"]);
            Assert.Equal(TestString(3802, "AppDomain"), (string)firedEvents[3802]["AppDomain"]);

            // 3803 RoutingServiceProcessingFailure - ThreeStringsTemplateEA
            Assert.True(firedEvents.ContainsKey(3803), "Event 3803 (RoutingServiceProcessingFailure) did not fire.");
            Assert.Equal(TestString(3803, "data1"), (string)firedEvents[3803]["data1"]);
            Assert.Equal(TestString(3803, "SerializedException"), (string)firedEvents[3803]["SerializedException"]);
            Assert.Equal(TestString(3803, "AppDomain"), (string)firedEvents[3803]["AppDomain"]);

            // 3804 RoutingServiceCreatingClientForEndpoint - TwoStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3804), "Event 3804 (RoutingServiceCreatingClientForEndpoint) did not fire.");
            Assert.Equal(TestString(3804, "data1"), (string)firedEvents[3804]["data1"]);
            Assert.Equal(TestString(3804, "AppDomain"), (string)firedEvents[3804]["AppDomain"]);

            // 3805 RoutingServiceDisplayConfig - FourStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3805), "Event 3805 (RoutingServiceDisplayConfig) did not fire.");
            Assert.Equal(TestString(3805, "data1"), (string)firedEvents[3805]["data1"]);
            Assert.Equal(TestString(3805, "data2"), (string)firedEvents[3805]["data2"]);
            Assert.Equal(TestString(3805, "data3"), (string)firedEvents[3805]["data3"]);
            Assert.Equal(TestString(3805, "AppDomain"), (string)firedEvents[3805]["AppDomain"]);

            // 3807 RoutingServiceCompletingTwoWay - OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3807), "Event 3807 (RoutingServiceCompletingTwoWay) did not fire.");
            Assert.Equal(TestString(3807, "AppDomain"), (string)firedEvents[3807]["AppDomain"]);

            // 3809 RoutingServiceMessageRoutedToEndpoints - ThreeStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3809), "Event 3809 (RoutingServiceMessageRoutedToEndpoints) did not fire.");
            Assert.Equal(TestString(3809, "data1"), (string)firedEvents[3809]["data1"]);
            Assert.Equal(TestString(3809, "data2"), (string)firedEvents[3809]["data2"]);
            Assert.Equal(TestString(3809, "AppDomain"), (string)firedEvents[3809]["AppDomain"]);

            // 3810 RoutingServiceConfigurationApplied - OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3810), "Event 3810 (RoutingServiceConfigurationApplied) did not fire.");
            Assert.Equal(TestString(3810, "AppDomain"), (string)firedEvents[3810]["AppDomain"]);

            // 3815 RoutingServiceProcessingMessage - FiveStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3815), "Event 3815 (RoutingServiceProcessingMessage) did not fire.");
            Assert.Equal(TestString(3815, "data1"), (string)firedEvents[3815]["data1"]);
            Assert.Equal(TestString(3815, "data2"), (string)firedEvents[3815]["data2"]);
            Assert.Equal(TestString(3815, "data3"), (string)firedEvents[3815]["data3"]);
            Assert.Equal(TestString(3815, "data4"), (string)firedEvents[3815]["data4"]);
            Assert.Equal(TestString(3815, "AppDomain"), (string)firedEvents[3815]["AppDomain"]);

            // 3816 RoutingServiceTransmittingMessage - FourStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3816), "Event 3816 (RoutingServiceTransmittingMessage) did not fire.");
            Assert.Equal(TestString(3816, "data1"), (string)firedEvents[3816]["data1"]);
            Assert.Equal(TestString(3816, "data2"), (string)firedEvents[3816]["data2"]);
            Assert.Equal(TestString(3816, "data3"), (string)firedEvents[3816]["data3"]);
            Assert.Equal(TestString(3816, "AppDomain"), (string)firedEvents[3816]["AppDomain"]);

            // 3817 RoutingServiceCommittingTransaction - TwoStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3817), "Event 3817 (RoutingServiceCommittingTransaction) did not fire.");
            Assert.Equal(TestString(3817, "data1"), (string)firedEvents[3817]["data1"]);
            Assert.Equal(TestString(3817, "AppDomain"), (string)firedEvents[3817]["AppDomain"]);

            // 3818 RoutingServiceDuplexCallbackException - ThreeStringsTemplateEA
            Assert.True(firedEvents.ContainsKey(3818), "Event 3818 (RoutingServiceDuplexCallbackException) did not fire.");
            Assert.Equal(TestString(3818, "data1"), (string)firedEvents[3818]["data1"]);
            Assert.Equal(TestString(3818, "SerializedException"), (string)firedEvents[3818]["SerializedException"]);
            Assert.Equal(TestString(3818, "AppDomain"), (string)firedEvents[3818]["AppDomain"]);

            // 3819 RoutingServiceMovedToBackup - FourStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3819), "Event 3819 (RoutingServiceMovedToBackup) did not fire.");
            Assert.Equal(TestString(3819, "data1"), (string)firedEvents[3819]["data1"]);
            Assert.Equal(TestString(3819, "data2"), (string)firedEvents[3819]["data2"]);
            Assert.Equal(TestString(3819, "data3"), (string)firedEvents[3819]["data3"]);
            Assert.Equal(TestString(3819, "AppDomain"), (string)firedEvents[3819]["AppDomain"]);

            // 3820 RoutingServiceCreatingTransaction - TwoStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3820), "Event 3820 (RoutingServiceCreatingTransaction) did not fire.");
            Assert.Equal(TestString(3820, "data1"), (string)firedEvents[3820]["data1"]);
            Assert.Equal(TestString(3820, "AppDomain"), (string)firedEvents[3820]["AppDomain"]);

            // 3821 RoutingServiceCloseFailed - ThreeStringsTemplateEA
            Assert.True(firedEvents.ContainsKey(3821), "Event 3821 (RoutingServiceCloseFailed) did not fire.");
            Assert.Equal(TestString(3821, "data1"), (string)firedEvents[3821]["data1"]);
            Assert.Equal(TestString(3821, "SerializedException"), (string)firedEvents[3821]["SerializedException"]);
            Assert.Equal(TestString(3821, "AppDomain"), (string)firedEvents[3821]["AppDomain"]);

            // 3822 RoutingServiceSendingResponse - TwoStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3822), "Event 3822 (RoutingServiceSendingResponse) did not fire.");
            Assert.Equal(TestString(3822, "data1"), (string)firedEvents[3822]["data1"]);
            Assert.Equal(TestString(3822, "AppDomain"), (string)firedEvents[3822]["AppDomain"]);

            // 3823 RoutingServiceSendingFaultResponse - TwoStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3823), "Event 3823 (RoutingServiceSendingFaultResponse) did not fire.");
            Assert.Equal(TestString(3823, "data1"), (string)firedEvents[3823]["data1"]);
            Assert.Equal(TestString(3823, "AppDomain"), (string)firedEvents[3823]["AppDomain"]);

            // 3824 RoutingServiceCompletingReceiveContext - TwoStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3824), "Event 3824 (RoutingServiceCompletingReceiveContext) did not fire.");
            Assert.Equal(TestString(3824, "data1"), (string)firedEvents[3824]["data1"]);
            Assert.Equal(TestString(3824, "AppDomain"), (string)firedEvents[3824]["AppDomain"]);

            // 3825 RoutingServiceAbandoningReceiveContext - TwoStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3825), "Event 3825 (RoutingServiceAbandoningReceiveContext) did not fire.");
            Assert.Equal(TestString(3825, "data1"), (string)firedEvents[3825]["data1"]);
            Assert.Equal(TestString(3825, "AppDomain"), (string)firedEvents[3825]["AppDomain"]);

            // 3826 RoutingServiceUsingExistingTransaction - TwoStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3826), "Event 3826 (RoutingServiceUsingExistingTransaction) did not fire.");
            Assert.Equal(TestString(3826, "data1"), (string)firedEvents[3826]["data1"]);
            Assert.Equal(TestString(3826, "AppDomain"), (string)firedEvents[3826]["AppDomain"]);

            // 3827 RoutingServiceTransmitFailed - ThreeStringsTemplateEA
            Assert.True(firedEvents.ContainsKey(3827), "Event 3827 (RoutingServiceTransmitFailed) did not fire.");
            Assert.Equal(TestString(3827, "data1"), (string)firedEvents[3827]["data1"]);
            Assert.Equal(TestString(3827, "SerializedException"), (string)firedEvents[3827]["SerializedException"]);
            Assert.Equal(TestString(3827, "AppDomain"), (string)firedEvents[3827]["AppDomain"]);

            // 3828 RoutingServiceFilterTableMatchStart - OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3828), "Event 3828 (RoutingServiceFilterTableMatchStart) did not fire.");
            Assert.Equal(TestString(3828, "AppDomain"), (string)firedEvents[3828]["AppDomain"]);

            // 3829 RoutingServiceFilterTableMatchStop - OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3829), "Event 3829 (RoutingServiceFilterTableMatchStop) did not fire.");
            Assert.Equal(TestString(3829, "AppDomain"), (string)firedEvents[3829]["AppDomain"]);

            // 3830 RoutingServiceAbortingChannel - TwoStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3830), "Event 3830 (RoutingServiceAbortingChannel) did not fire.");
            Assert.Equal(TestString(3830, "data1"), (string)firedEvents[3830]["data1"]);
            Assert.Equal(TestString(3830, "AppDomain"), (string)firedEvents[3830]["AppDomain"]);

            // 3831 RoutingServiceHandledException - TwoStringsTemplateEA
            Assert.True(firedEvents.ContainsKey(3831), "Event 3831 (RoutingServiceHandledException) did not fire.");
            Assert.Equal(TestString(3831, "SerializedException"), (string)firedEvents[3831]["SerializedException"]);
            Assert.Equal(TestString(3831, "AppDomain"), (string)firedEvents[3831]["AppDomain"]);

            // 3832 RoutingServiceTransmitSucceeded - FourStringsTemplateA
            Assert.True(firedEvents.ContainsKey(3832), "Event 3832 (RoutingServiceTransmitSucceeded) did not fire.");
            Assert.Equal(TestString(3832, "data1"), (string)firedEvents[3832]["data1"]);
            Assert.Equal(TestString(3832, "data2"), (string)firedEvents[3832]["data2"]);
            Assert.Equal(TestString(3832, "data3"), (string)firedEvents[3832]["data3"]);
            Assert.Equal(TestString(3832, "AppDomain"), (string)firedEvents[3832]["AppDomain"]);

            // 4001 TransportListenerSessionsReceived - Multidata77TemplateA
            Assert.True(firedEvents.ContainsKey(4001), "Event 4001 (TransportListenerSessionsReceived) did not fire.");
            Assert.Equal(TestString(4001, "via"), (string)firedEvents[4001]["via"]);
            Assert.Equal(TestString(4001, "AppDomain"), (string)firedEvents[4001]["AppDomain"]);

            // 4002 FailFastException - TwoStringsTemplateEA
            Assert.True(firedEvents.ContainsKey(4002), "Event 4002 (FailFastException) did not fire.");
            Assert.Equal(TestString(4002, "SerializedException"), (string)firedEvents[4002]["SerializedException"]);
            Assert.Equal(TestString(4002, "AppDomain"), (string)firedEvents[4002]["AppDomain"]);

            // 4003 ServiceStartPipeError - Multidata78TemplateA
            Assert.True(firedEvents.ContainsKey(4003), "Event 4003 (ServiceStartPipeError) did not fire.");
            Assert.Equal(TestString(4003, "Endpoint"), (string)firedEvents[4003]["Endpoint"]);
            Assert.Equal(TestString(4003, "AppDomain"), (string)firedEvents[4003]["AppDomain"]);

            // 4008 DispatchSessionStart - OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(4008), "Event 4008 (DispatchSessionStart) did not fire.");
            Assert.Equal(TestString(4008, "AppDomain"), (string)firedEvents[4008]["AppDomain"]);

            // 4010 PendingSessionQueueFull - Multidata79TemplateA
            Assert.True(firedEvents.ContainsKey(4010), "Event 4010 (PendingSessionQueueFull) did not fire.");
            Assert.Equal(TestString(4010, "Uri"), (string)firedEvents[4010]["Uri"]);
            Assert.Equal(TestInt32(4010, 1), (int)firedEvents[4010]["count"]);
            Assert.Equal(TestString(4010, "AppDomain"), (string)firedEvents[4010]["AppDomain"]);

            // 4011 MessageQueueRegisterStart - OneStringsTemplateA
            Assert.True(firedEvents.ContainsKey(4011), "Event 4011 (MessageQueueRegisterStart) did not fire.");
            Assert.Equal(TestString(4011, "AppDomain"), (string)firedEvents[4011]["AppDomain"]);

            // 4012 MessageQueueRegisterAbort - Multidata81TemplateA
            Assert.True(firedEvents.ContainsKey(4012), "Event 4012 (MessageQueueRegisterAbort) did not fire.");
            Assert.Equal(TestString(4012, "Status"), (string)firedEvents[4012]["Status"]);
            Assert.Equal(TestString(4012, "Uri"), (string)firedEvents[4012]["Uri"]);
            Assert.Equal(TestString(4012, "AppDomain"), (string)firedEvents[4012]["AppDomain"]);

            // 4013 MessageQueueUnregisterSucceeded - Multidata48TemplateA
            Assert.True(firedEvents.ContainsKey(4013), "Event 4013 (MessageQueueUnregisterSucceeded) did not fire.");
            Assert.Equal(TestString(4013, "Uri"), (string)firedEvents[4013]["Uri"]);
            Assert.Equal(TestString(4013, "AppDomain"), (string)firedEvents[4013]["AppDomain"]);
        }

        #endregion
    }
}
