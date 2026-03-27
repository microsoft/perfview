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
        #region Chunk03 Template Fields

        private static readonly TemplateField[] s_chunk03_OneStringsTemplateAFields = new TemplateField[]
        {
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_chunk03_TwoStringsTemplateAFields = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_chunk03_TwoStringsTemplateVAFields = new TemplateField[]
        {
            new TemplateField("HostReference", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_chunk03_ThreeStringsTemplateEAFields = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("SerializedException", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_chunk03_FourStringsTemplateAFields = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("data2", FieldType.UnicodeString),
            new TemplateField("data3", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_chunk03_FiveStringsTemplateAFields = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("data2", FieldType.UnicodeString),
            new TemplateField("data3", FieldType.UnicodeString),
            new TemplateField("data4", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_chunk03_SixStringsTemplateEAFields = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("data2", FieldType.UnicodeString),
            new TemplateField("data3", FieldType.UnicodeString),
            new TemplateField("data4", FieldType.UnicodeString),
            new TemplateField("SerializedException", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_chunk03_SevenStringsTemplateAFields = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("data2", FieldType.UnicodeString),
            new TemplateField("data3", FieldType.UnicodeString),
            new TemplateField("data4", FieldType.UnicodeString),
            new TemplateField("data5", FieldType.UnicodeString),
            new TemplateField("data6", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_chunk03_Multidata70TemplateAFields = new TemplateField[]
        {
            new TemplateField("IncomingAddress", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_chunk03_Multidata71TemplateAFields = new TemplateField[]
        {
            new TemplateField("AspNetRoutePrefix", FieldType.UnicodeString),
            new TemplateField("ServiceType", FieldType.UnicodeString),
            new TemplateField("ServiceHostFactoryType", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] s_chunk03_Multidata75TemplateAFields = new TemplateField[]
        {
            new TemplateField("Data", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        #endregion

        #region Chunk03 WriteMetadata

        private void WriteMetadata_Chunk03(EventPipeWriterV5 writer, ref int metadataId)
        {
            int __metadataId = metadataId;
            int id = __metadataId;
            writer.WriteMetadataBlock(w =>
            {
                // Event 602: CBAMatchFound (Multidata70TemplateA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "CBAMatchFound", 602);
                    pw.WriteV5MetadataParameterList();
                });

                // Event 603: AspNetRoutingService (Multidata70TemplateA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "AspNetRoutingService", 603);
                    pw.WriteV5MetadataParameterList();
                });

                // Event 604: AspNetRoute (Multidata71TemplateA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "AspNetRoute", 604);
                    pw.WriteV5MetadataParameterList();
                });

                // Event 605: IncrementBusyCount (Multidata75TemplateA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "IncrementBusyCount", 605);
                    pw.WriteV5MetadataParameterList();
                });

                // Event 606: DecrementBusyCount (Multidata75TemplateA, opcode=2)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "DecrementBusyCount", 606);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(2);
                });

                // Event 701: ServiceChannelOpenStart (OneStringsTemplateA, opcode=1)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "ServiceChannelOpenStart", 701);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(1);
                });

                // Event 702: ServiceChannelOpenStop (OneStringsTemplateA, opcode=2)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "ServiceChannelOpenStop", 702);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(2);
                });

                // Event 703: ServiceChannelCallStart (OneStringsTemplateA, opcode=1)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "ServiceChannelCallStart", 703);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(1);
                });

                // Event 704: ServiceChannelBeginCallStart (OneStringsTemplateA, opcode=1)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "ServiceChannelBeginCallStart", 704);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(1);
                });

                // Event 706: HttpSendMessageStart (OneStringsTemplateA, opcode=1)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "HttpSendMessageStart", 706);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(1);
                });

                // Event 707: HttpSendStop (OneStringsTemplateA, opcode=2)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "HttpSendStop", 707);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(2);
                });

                // Event 708: HttpMessageReceiveStart (OneStringsTemplateA, opcode=1)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "HttpMessageReceiveStart", 708);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(1);
                });

                // Event 709: DispatchMessageStart (TwoStringsTemplateVA, opcode=49)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "DispatchMessageStart", 709);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(49);
                });

                // Event 710: HttpContextBeforeProcessAuthentication (OneStringsTemplateA, opcode=128)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "HttpContextBeforeProcessAuthentication", 710);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(128);
                });

                // Event 711: DispatchMessageBeforeAuthorization (OneStringsTemplateA, opcode=48)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "DispatchMessageBeforeAuthorization", 711);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(48);
                });

                // Event 712: DispatchMessageStop (OneStringsTemplateA, opcode=50)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "DispatchMessageStop", 712);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(50);
                });

                // Event 715: ClientChannelOpenStart (OneStringsTemplateA, opcode=14)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "ClientChannelOpenStart", 715);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(14);
                });

                // Event 716: ClientChannelOpenStop (OneStringsTemplateA, opcode=15)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "ClientChannelOpenStop", 716);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(15);
                });

                // Event 717: HttpSendStreamedMessageStart (OneStringsTemplateA, opcode=1)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "HttpSendStreamedMessageStart", 717);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(1);
                });

                // Event 1001: WorkflowApplicationCompleted (TwoStringsTemplateA, opcode=134)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "WorkflowApplicationCompleted", 1001);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(134);
                });

                // Event 1002: WorkflowApplicationTerminated (ThreeStringsTemplateEA, opcode=140)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "WorkflowApplicationTerminated", 1002);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(140);
                });

                // Event 1003: WorkflowInstanceCanceled (TwoStringsTemplateA, opcode=137)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "WorkflowInstanceCanceled", 1003);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(137);
                });

                // Event 1004: WorkflowInstanceAborted (ThreeStringsTemplateEA, opcode=136)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "WorkflowInstanceAborted", 1004);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(136);
                });

                // Event 1005: WorkflowApplicationIdled (TwoStringsTemplateA, opcode=135)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "WorkflowApplicationIdled", 1005);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(135);
                });

                // Event 1006: WorkflowApplicationUnhandledException (SixStringsTemplateEA, opcode=141)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "WorkflowApplicationUnhandledException", 1006);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(141);
                });

                // Event 1007: WorkflowApplicationPersisted (TwoStringsTemplateA, opcode=139)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "WorkflowApplicationPersisted", 1007);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(139);
                });

                // Event 1008: WorkflowApplicationUnloaded (TwoStringsTemplateA, opcode=142)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "WorkflowApplicationUnloaded", 1008);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(142);
                });

                // Event 1009: ActivityScheduled (SevenStringsTemplateA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "ActivityScheduled", 1009);
                    pw.WriteV5MetadataParameterList();
                });

                // Event 1010: ActivityCompleted (FiveStringsTemplateA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "ActivityCompleted", 1010);
                    pw.WriteV5MetadataParameterList();
                });

                // Event 1011: ScheduleExecuteActivityWorkItem (FourStringsTemplateA, opcode=110)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "ScheduleExecuteActivityWorkItem", 1011);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(110);
                });

                // Event 1012: StartExecuteActivityWorkItem (FourStringsTemplateA, opcode=120)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "StartExecuteActivityWorkItem", 1012);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(120);
                });

                // Event 1013: CompleteExecuteActivityWorkItem (FourStringsTemplateA, opcode=24)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "CompleteExecuteActivityWorkItem", 1013);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(24);
                });

                // Event 1014: ScheduleCompletionWorkItem (SevenStringsTemplateA, opcode=109)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "ScheduleCompletionWorkItem", 1014);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(109);
                });

                // Event 1015: StartCompletionWorkItem (SevenStringsTemplateA, opcode=119)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "StartCompletionWorkItem", 1015);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(119);
                });

                // Event 1016: CompleteCompletionWorkItem (SevenStringsTemplateA, opcode=23)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "CompleteCompletionWorkItem", 1016);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(23);
                });

                // Event 1017: ScheduleCancelActivityWorkItem (FourStringsTemplateA, opcode=108)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "ScheduleCancelActivityWorkItem", 1017);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(108);
                });

                // Event 1018: StartCancelActivityWorkItem (FourStringsTemplateA, opcode=118)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "StartCancelActivityWorkItem", 1018);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(118);
                });
            });
            __metadataId = id;
        
            metadataId = __metadataId;
        }

        #endregion

        #region Chunk03 WriteEvents

        private void WriteEvents_Chunk03(EventPipeWriterV5 writer, ref int metadataId, ref int sequenceNumber)
        {
            int __metadataId = metadataId;
            int __sequenceNumber = sequenceNumber;
            int id = __metadataId;
            int seq = __sequenceNumber;
            writer.WriteEventBlock(w =>
            {
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(602, s_chunk03_Multidata70TemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(603, s_chunk03_Multidata70TemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(604, s_chunk03_Multidata71TemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(605, s_chunk03_Multidata75TemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(606, s_chunk03_Multidata75TemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(701, s_chunk03_OneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(702, s_chunk03_OneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(703, s_chunk03_OneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(704, s_chunk03_OneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(706, s_chunk03_OneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(707, s_chunk03_OneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(708, s_chunk03_OneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(709, s_chunk03_TwoStringsTemplateVAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(710, s_chunk03_OneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(711, s_chunk03_OneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(712, s_chunk03_OneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(715, s_chunk03_OneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(716, s_chunk03_OneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(717, s_chunk03_OneStringsTemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(1001, s_chunk03_TwoStringsTemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(1002, s_chunk03_ThreeStringsTemplateEAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(1003, s_chunk03_TwoStringsTemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(1004, s_chunk03_ThreeStringsTemplateEAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(1005, s_chunk03_TwoStringsTemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(1006, s_chunk03_SixStringsTemplateEAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(1007, s_chunk03_TwoStringsTemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(1008, s_chunk03_TwoStringsTemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(1009, s_chunk03_SevenStringsTemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(1010, s_chunk03_FiveStringsTemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(1011, s_chunk03_FourStringsTemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(1012, s_chunk03_FourStringsTemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(1013, s_chunk03_FourStringsTemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(1014, s_chunk03_SevenStringsTemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(1015, s_chunk03_SevenStringsTemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(1016, s_chunk03_SevenStringsTemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(1017, s_chunk03_FourStringsTemplateAFields));
                w.WriteEventBlobV4Or5(id++, 999, seq++, BuildPayload(1018, s_chunk03_FourStringsTemplateAFields));
            });
            __metadataId = id;
            __sequenceNumber = seq;
        
            metadataId = __metadataId;
            sequenceNumber = __sequenceNumber;
        }

        #endregion

        #region Chunk03 Subscribe

        private void Subscribe_Chunk03(ApplicationServerTraceEventParser parser, Dictionary<int, Dictionary<string, object>> firedEvents)
        {
            // Event 602: CBAMatchFound (Multidata70TemplateA)
            parser.CBAMatchFound += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["IncomingAddress"] = e.IncomingAddress;
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 603: AspNetRoutingService (Multidata70TemplateA)
            parser.AspNetRoutingService += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["IncomingAddress"] = e.IncomingAddress;
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 604: AspNetRoute (Multidata71TemplateA)
            parser.AspNetRoute += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["AspNetRoutePrefix"] = e.AspNetRoutePrefix;
                fields["ServiceType"] = e.ServiceType;
                fields["ServiceHostFactoryType"] = e.ServiceHostFactoryType;
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 605: IncrementBusyCount (Multidata75TemplateA)
            parser.IncrementBusyCount += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["Data"] = e.Data;
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 606: DecrementBusyCount (Multidata75TemplateA)
            parser.DecrementBusyCount += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["Data"] = e.Data;
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 701: ServiceChannelOpenStart (OneStringsTemplateA)
            parser.ServiceChannelOpenStart += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 702: ServiceChannelOpenStop (OneStringsTemplateA)
            parser.ServiceChannelOpenStop += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 703: ServiceChannelCallStart (OneStringsTemplateA)
            parser.ServiceChannelCallStart += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 704: ServiceChannelBeginCallStart (OneStringsTemplateA)
            parser.ServiceChannelBeginCallStart += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 706: HttpSendMessageStart (OneStringsTemplateA)
            parser.HttpSendMessageStart += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 707: HttpSendStop (OneStringsTemplateA)
            parser.HttpSendStop += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 708: HttpMessageReceiveStart (OneStringsTemplateA)
            parser.HttpMessageReceiveStart += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 709: DispatchMessageStart (TwoStringsTemplateVA)
            parser.DispatchMessageStart += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["HostReference"] = e.HostReference;
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 710: HttpContextBeforeProcessAuthentication (OneStringsTemplateA)
            parser.HttpContextBeforeProcessAuthentication += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 711: DispatchMessageBeforeAuthorization (OneStringsTemplateA)
            parser.DispatchMessageBeforeAuthorization += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 712: DispatchMessageStop (OneStringsTemplateA)
            parser.DispatchMessageStop += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 715: ClientChannelOpenStart (OneStringsTemplateA)
            parser.ClientChannelOpenStart += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 716: ClientChannelOpenStop (OneStringsTemplateA)
            parser.ClientChannelOpenStop += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 717: HttpSendStreamedMessageStart (OneStringsTemplateA)
            parser.HttpSendStreamedMessageStart += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 1001: WorkflowApplicationCompleted (TwoStringsTemplateA)
            parser.WorkflowApplicationCompleted += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 1002: WorkflowApplicationTerminated (ThreeStringsTemplateEA)
            parser.WorkflowApplicationTerminated += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["SerializedException"] = e.SerializedException;
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 1003: WorkflowInstanceCanceled (TwoStringsTemplateA)
            parser.WorkflowInstanceCanceled += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 1004: WorkflowInstanceAborted (ThreeStringsTemplateEA)
            parser.WorkflowInstanceAborted += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["SerializedException"] = e.SerializedException;
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 1005: WorkflowApplicationIdled (TwoStringsTemplateA)
            parser.WorkflowApplicationIdled += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 1006: WorkflowApplicationUnhandledException (SixStringsTemplateEA)
            parser.WorkflowApplicationUnhandledException += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["data2"] = e.data2;
                fields["data3"] = e.data3;
                fields["data4"] = e.data4;
                fields["SerializedException"] = e.SerializedException;
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 1007: WorkflowApplicationPersisted (TwoStringsTemplateA)
            parser.WorkflowApplicationPersisted += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 1008: WorkflowApplicationUnloaded (TwoStringsTemplateA)
            parser.WorkflowApplicationUnloaded += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 1009: ActivityScheduled (SevenStringsTemplateA)
            parser.ActivityScheduled += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["data2"] = e.data2;
                fields["data3"] = e.data3;
                fields["data4"] = e.data4;
                fields["data5"] = e.data5;
                fields["data6"] = e.data6;
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 1010: ActivityCompleted (FiveStringsTemplateA)
            parser.ActivityCompleted += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["data2"] = e.data2;
                fields["data3"] = e.data3;
                fields["data4"] = e.data4;
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 1011: ScheduleExecuteActivityWorkItem (FourStringsTemplateA)
            parser.ScheduleExecuteActivityWorkItem += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["data2"] = e.data2;
                fields["data3"] = e.data3;
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 1012: StartExecuteActivityWorkItem (FourStringsTemplateA)
            parser.StartExecuteActivityWorkItem += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["data2"] = e.data2;
                fields["data3"] = e.data3;
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 1013: CompleteExecuteActivityWorkItem (FourStringsTemplateA)
            parser.CompleteExecuteActivityWorkItem += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["data2"] = e.data2;
                fields["data3"] = e.data3;
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 1014: ScheduleCompletionWorkItem (SevenStringsTemplateA)
            parser.ScheduleCompletionWorkItem += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["data2"] = e.data2;
                fields["data3"] = e.data3;
                fields["data4"] = e.data4;
                fields["data5"] = e.data5;
                fields["data6"] = e.data6;
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 1015: StartCompletionWorkItem (SevenStringsTemplateA)
            parser.StartCompletionWorkItem += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["data2"] = e.data2;
                fields["data3"] = e.data3;
                fields["data4"] = e.data4;
                fields["data5"] = e.data5;
                fields["data6"] = e.data6;
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 1016: CompleteCompletionWorkItem (SevenStringsTemplateA)
            parser.CompleteCompletionWorkItem += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["data2"] = e.data2;
                fields["data3"] = e.data3;
                fields["data4"] = e.data4;
                fields["data5"] = e.data5;
                fields["data6"] = e.data6;
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 1017: ScheduleCancelActivityWorkItem (FourStringsTemplateA)
            parser.ScheduleCancelActivityWorkItem += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["data2"] = e.data2;
                fields["data3"] = e.data3;
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };

            // Event 1018: StartCancelActivityWorkItem (FourStringsTemplateA)
            parser.StartCancelActivityWorkItem += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["data2"] = e.data2;
                fields["data3"] = e.data3;
                fields["AppDomain"] = e.AppDomain;
                firedEvents[(int)e.ID] = fields;
            };
        }

        #endregion

        #region Chunk03 Validate

        private void Validate_Chunk03(Dictionary<int, Dictionary<string, object>> firedEvents)
        {
            // Event 602: CBAMatchFound (Multidata70TemplateA)
            Assert.True(firedEvents.ContainsKey(602), "Event 602 (CBAMatchFound) did not fire.");
            Assert.Equal(TestString(602, "IncomingAddress"), firedEvents[602]["IncomingAddress"]);
            Assert.Equal(TestString(602, "AppDomain"), firedEvents[602]["AppDomain"]);

            // Event 603: AspNetRoutingService (Multidata70TemplateA)
            Assert.True(firedEvents.ContainsKey(603), "Event 603 (AspNetRoutingService) did not fire.");
            Assert.Equal(TestString(603, "IncomingAddress"), firedEvents[603]["IncomingAddress"]);
            Assert.Equal(TestString(603, "AppDomain"), firedEvents[603]["AppDomain"]);

            // Event 604: AspNetRoute (Multidata71TemplateA)
            Assert.True(firedEvents.ContainsKey(604), "Event 604 (AspNetRoute) did not fire.");
            Assert.Equal(TestString(604, "AspNetRoutePrefix"), firedEvents[604]["AspNetRoutePrefix"]);
            Assert.Equal(TestString(604, "ServiceType"), firedEvents[604]["ServiceType"]);
            Assert.Equal(TestString(604, "ServiceHostFactoryType"), firedEvents[604]["ServiceHostFactoryType"]);
            Assert.Equal(TestString(604, "AppDomain"), firedEvents[604]["AppDomain"]);

            // Event 605: IncrementBusyCount (Multidata75TemplateA)
            Assert.True(firedEvents.ContainsKey(605), "Event 605 (IncrementBusyCount) did not fire.");
            Assert.Equal(TestString(605, "Data"), firedEvents[605]["Data"]);
            Assert.Equal(TestString(605, "AppDomain"), firedEvents[605]["AppDomain"]);

            // Event 606: DecrementBusyCount (Multidata75TemplateA)
            Assert.True(firedEvents.ContainsKey(606), "Event 606 (DecrementBusyCount) did not fire.");
            Assert.Equal(TestString(606, "Data"), firedEvents[606]["Data"]);
            Assert.Equal(TestString(606, "AppDomain"), firedEvents[606]["AppDomain"]);

            // Event 701: ServiceChannelOpenStart (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(701), "Event 701 (ServiceChannelOpenStart) did not fire.");
            Assert.Equal(TestString(701, "AppDomain"), firedEvents[701]["AppDomain"]);

            // Event 702: ServiceChannelOpenStop (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(702), "Event 702 (ServiceChannelOpenStop) did not fire.");
            Assert.Equal(TestString(702, "AppDomain"), firedEvents[702]["AppDomain"]);

            // Event 703: ServiceChannelCallStart (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(703), "Event 703 (ServiceChannelCallStart) did not fire.");
            Assert.Equal(TestString(703, "AppDomain"), firedEvents[703]["AppDomain"]);

            // Event 704: ServiceChannelBeginCallStart (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(704), "Event 704 (ServiceChannelBeginCallStart) did not fire.");
            Assert.Equal(TestString(704, "AppDomain"), firedEvents[704]["AppDomain"]);

            // Event 706: HttpSendMessageStart (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(706), "Event 706 (HttpSendMessageStart) did not fire.");
            Assert.Equal(TestString(706, "AppDomain"), firedEvents[706]["AppDomain"]);

            // Event 707: HttpSendStop (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(707), "Event 707 (HttpSendStop) did not fire.");
            Assert.Equal(TestString(707, "AppDomain"), firedEvents[707]["AppDomain"]);

            // Event 708: HttpMessageReceiveStart (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(708), "Event 708 (HttpMessageReceiveStart) did not fire.");
            Assert.Equal(TestString(708, "AppDomain"), firedEvents[708]["AppDomain"]);

            // Event 709: DispatchMessageStart (TwoStringsTemplateVA)
            Assert.True(firedEvents.ContainsKey(709), "Event 709 (DispatchMessageStart) did not fire.");
            Assert.Equal(TestString(709, "HostReference"), firedEvents[709]["HostReference"]);
            Assert.Equal(TestString(709, "AppDomain"), firedEvents[709]["AppDomain"]);

            // Event 710: HttpContextBeforeProcessAuthentication (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(710), "Event 710 (HttpContextBeforeProcessAuthentication) did not fire.");
            Assert.Equal(TestString(710, "AppDomain"), firedEvents[710]["AppDomain"]);

            // Event 711: DispatchMessageBeforeAuthorization (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(711), "Event 711 (DispatchMessageBeforeAuthorization) did not fire.");
            Assert.Equal(TestString(711, "AppDomain"), firedEvents[711]["AppDomain"]);

            // Event 712: DispatchMessageStop (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(712), "Event 712 (DispatchMessageStop) did not fire.");
            Assert.Equal(TestString(712, "AppDomain"), firedEvents[712]["AppDomain"]);

            // Event 715: ClientChannelOpenStart (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(715), "Event 715 (ClientChannelOpenStart) did not fire.");
            Assert.Equal(TestString(715, "AppDomain"), firedEvents[715]["AppDomain"]);

            // Event 716: ClientChannelOpenStop (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(716), "Event 716 (ClientChannelOpenStop) did not fire.");
            Assert.Equal(TestString(716, "AppDomain"), firedEvents[716]["AppDomain"]);

            // Event 717: HttpSendStreamedMessageStart (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(717), "Event 717 (HttpSendStreamedMessageStart) did not fire.");
            Assert.Equal(TestString(717, "AppDomain"), firedEvents[717]["AppDomain"]);

            // Event 1001: WorkflowApplicationCompleted (TwoStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(1001), "Event 1001 (WorkflowApplicationCompleted) did not fire.");
            Assert.Equal(TestString(1001, "data1"), firedEvents[1001]["data1"]);
            Assert.Equal(TestString(1001, "AppDomain"), firedEvents[1001]["AppDomain"]);

            // Event 1002: WorkflowApplicationTerminated (ThreeStringsTemplateEA)
            Assert.True(firedEvents.ContainsKey(1002), "Event 1002 (WorkflowApplicationTerminated) did not fire.");
            Assert.Equal(TestString(1002, "data1"), firedEvents[1002]["data1"]);
            Assert.Equal(TestString(1002, "SerializedException"), firedEvents[1002]["SerializedException"]);
            Assert.Equal(TestString(1002, "AppDomain"), firedEvents[1002]["AppDomain"]);

            // Event 1003: WorkflowInstanceCanceled (TwoStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(1003), "Event 1003 (WorkflowInstanceCanceled) did not fire.");
            Assert.Equal(TestString(1003, "data1"), firedEvents[1003]["data1"]);
            Assert.Equal(TestString(1003, "AppDomain"), firedEvents[1003]["AppDomain"]);

            // Event 1004: WorkflowInstanceAborted (ThreeStringsTemplateEA)
            Assert.True(firedEvents.ContainsKey(1004), "Event 1004 (WorkflowInstanceAborted) did not fire.");
            Assert.Equal(TestString(1004, "data1"), firedEvents[1004]["data1"]);
            Assert.Equal(TestString(1004, "SerializedException"), firedEvents[1004]["SerializedException"]);
            Assert.Equal(TestString(1004, "AppDomain"), firedEvents[1004]["AppDomain"]);

            // Event 1005: WorkflowApplicationIdled (TwoStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(1005), "Event 1005 (WorkflowApplicationIdled) did not fire.");
            Assert.Equal(TestString(1005, "data1"), firedEvents[1005]["data1"]);
            Assert.Equal(TestString(1005, "AppDomain"), firedEvents[1005]["AppDomain"]);

            // Event 1006: WorkflowApplicationUnhandledException (SixStringsTemplateEA)
            Assert.True(firedEvents.ContainsKey(1006), "Event 1006 (WorkflowApplicationUnhandledException) did not fire.");
            Assert.Equal(TestString(1006, "data1"), firedEvents[1006]["data1"]);
            Assert.Equal(TestString(1006, "data2"), firedEvents[1006]["data2"]);
            Assert.Equal(TestString(1006, "data3"), firedEvents[1006]["data3"]);
            Assert.Equal(TestString(1006, "data4"), firedEvents[1006]["data4"]);
            Assert.Equal(TestString(1006, "SerializedException"), firedEvents[1006]["SerializedException"]);
            Assert.Equal(TestString(1006, "AppDomain"), firedEvents[1006]["AppDomain"]);

            // Event 1007: WorkflowApplicationPersisted (TwoStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(1007), "Event 1007 (WorkflowApplicationPersisted) did not fire.");
            Assert.Equal(TestString(1007, "data1"), firedEvents[1007]["data1"]);
            Assert.Equal(TestString(1007, "AppDomain"), firedEvents[1007]["AppDomain"]);

            // Event 1008: WorkflowApplicationUnloaded (TwoStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(1008), "Event 1008 (WorkflowApplicationUnloaded) did not fire.");
            Assert.Equal(TestString(1008, "data1"), firedEvents[1008]["data1"]);
            Assert.Equal(TestString(1008, "AppDomain"), firedEvents[1008]["AppDomain"]);

            // Event 1009: ActivityScheduled (SevenStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(1009), "Event 1009 (ActivityScheduled) did not fire.");
            Assert.Equal(TestString(1009, "data1"), firedEvents[1009]["data1"]);
            Assert.Equal(TestString(1009, "data2"), firedEvents[1009]["data2"]);
            Assert.Equal(TestString(1009, "data3"), firedEvents[1009]["data3"]);
            Assert.Equal(TestString(1009, "data4"), firedEvents[1009]["data4"]);
            Assert.Equal(TestString(1009, "data5"), firedEvents[1009]["data5"]);
            Assert.Equal(TestString(1009, "data6"), firedEvents[1009]["data6"]);
            Assert.Equal(TestString(1009, "AppDomain"), firedEvents[1009]["AppDomain"]);

            // Event 1010: ActivityCompleted (FiveStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(1010), "Event 1010 (ActivityCompleted) did not fire.");
            Assert.Equal(TestString(1010, "data1"), firedEvents[1010]["data1"]);
            Assert.Equal(TestString(1010, "data2"), firedEvents[1010]["data2"]);
            Assert.Equal(TestString(1010, "data3"), firedEvents[1010]["data3"]);
            Assert.Equal(TestString(1010, "data4"), firedEvents[1010]["data4"]);
            Assert.Equal(TestString(1010, "AppDomain"), firedEvents[1010]["AppDomain"]);

            // Event 1011: ScheduleExecuteActivityWorkItem (FourStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(1011), "Event 1011 (ScheduleExecuteActivityWorkItem) did not fire.");
            Assert.Equal(TestString(1011, "data1"), firedEvents[1011]["data1"]);
            Assert.Equal(TestString(1011, "data2"), firedEvents[1011]["data2"]);
            Assert.Equal(TestString(1011, "data3"), firedEvents[1011]["data3"]);
            Assert.Equal(TestString(1011, "AppDomain"), firedEvents[1011]["AppDomain"]);

            // Event 1012: StartExecuteActivityWorkItem (FourStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(1012), "Event 1012 (StartExecuteActivityWorkItem) did not fire.");
            Assert.Equal(TestString(1012, "data1"), firedEvents[1012]["data1"]);
            Assert.Equal(TestString(1012, "data2"), firedEvents[1012]["data2"]);
            Assert.Equal(TestString(1012, "data3"), firedEvents[1012]["data3"]);
            Assert.Equal(TestString(1012, "AppDomain"), firedEvents[1012]["AppDomain"]);

            // Event 1013: CompleteExecuteActivityWorkItem (FourStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(1013), "Event 1013 (CompleteExecuteActivityWorkItem) did not fire.");
            Assert.Equal(TestString(1013, "data1"), firedEvents[1013]["data1"]);
            Assert.Equal(TestString(1013, "data2"), firedEvents[1013]["data2"]);
            Assert.Equal(TestString(1013, "data3"), firedEvents[1013]["data3"]);
            Assert.Equal(TestString(1013, "AppDomain"), firedEvents[1013]["AppDomain"]);

            // Event 1014: ScheduleCompletionWorkItem (SevenStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(1014), "Event 1014 (ScheduleCompletionWorkItem) did not fire.");
            Assert.Equal(TestString(1014, "data1"), firedEvents[1014]["data1"]);
            Assert.Equal(TestString(1014, "data2"), firedEvents[1014]["data2"]);
            Assert.Equal(TestString(1014, "data3"), firedEvents[1014]["data3"]);
            Assert.Equal(TestString(1014, "data4"), firedEvents[1014]["data4"]);
            Assert.Equal(TestString(1014, "data5"), firedEvents[1014]["data5"]);
            Assert.Equal(TestString(1014, "data6"), firedEvents[1014]["data6"]);
            Assert.Equal(TestString(1014, "AppDomain"), firedEvents[1014]["AppDomain"]);

            // Event 1015: StartCompletionWorkItem (SevenStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(1015), "Event 1015 (StartCompletionWorkItem) did not fire.");
            Assert.Equal(TestString(1015, "data1"), firedEvents[1015]["data1"]);
            Assert.Equal(TestString(1015, "data2"), firedEvents[1015]["data2"]);
            Assert.Equal(TestString(1015, "data3"), firedEvents[1015]["data3"]);
            Assert.Equal(TestString(1015, "data4"), firedEvents[1015]["data4"]);
            Assert.Equal(TestString(1015, "data5"), firedEvents[1015]["data5"]);
            Assert.Equal(TestString(1015, "data6"), firedEvents[1015]["data6"]);
            Assert.Equal(TestString(1015, "AppDomain"), firedEvents[1015]["AppDomain"]);

            // Event 1016: CompleteCompletionWorkItem (SevenStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(1016), "Event 1016 (CompleteCompletionWorkItem) did not fire.");
            Assert.Equal(TestString(1016, "data1"), firedEvents[1016]["data1"]);
            Assert.Equal(TestString(1016, "data2"), firedEvents[1016]["data2"]);
            Assert.Equal(TestString(1016, "data3"), firedEvents[1016]["data3"]);
            Assert.Equal(TestString(1016, "data4"), firedEvents[1016]["data4"]);
            Assert.Equal(TestString(1016, "data5"), firedEvents[1016]["data5"]);
            Assert.Equal(TestString(1016, "data6"), firedEvents[1016]["data6"]);
            Assert.Equal(TestString(1016, "AppDomain"), firedEvents[1016]["AppDomain"]);

            // Event 1017: ScheduleCancelActivityWorkItem (FourStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(1017), "Event 1017 (ScheduleCancelActivityWorkItem) did not fire.");
            Assert.Equal(TestString(1017, "data1"), firedEvents[1017]["data1"]);
            Assert.Equal(TestString(1017, "data2"), firedEvents[1017]["data2"]);
            Assert.Equal(TestString(1017, "data3"), firedEvents[1017]["data3"]);
            Assert.Equal(TestString(1017, "AppDomain"), firedEvents[1017]["AppDomain"]);

            // Event 1018: StartCancelActivityWorkItem (FourStringsTemplateA)
            Assert.True(firedEvents.ContainsKey(1018), "Event 1018 (StartCancelActivityWorkItem) did not fire.");
            Assert.Equal(TestString(1018, "data1"), firedEvents[1018]["data1"]);
            Assert.Equal(TestString(1018, "data2"), firedEvents[1018]["data2"]);
            Assert.Equal(TestString(1018, "data3"), firedEvents[1018]["data3"]);
            Assert.Equal(TestString(1018, "AppDomain"), firedEvents[1018]["AppDomain"]);
        }

        #endregion
    }
}
