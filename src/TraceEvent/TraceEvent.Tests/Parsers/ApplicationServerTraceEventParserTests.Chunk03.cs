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
                // CBAMatchFound (Multidata70TemplateA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "CBAMatchFound", 602);
                    pw.WriteV5MetadataParameterList();
                });

                // AspNetRoutingService (Multidata70TemplateA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "AspNetRoutingService", 603);
                    pw.WriteV5MetadataParameterList();
                });

                // AspNetRoute (Multidata71TemplateA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "AspNetRoute", 604);
                    pw.WriteV5MetadataParameterList();
                });

                // IncrementBusyCount (Multidata75TemplateA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "IncrementBusyCount", 605);
                    pw.WriteV5MetadataParameterList();
                });

                // DecrementBusyCount (Multidata75TemplateA, opcode=2)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "DecrementBusyCount", 606);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(2);
                });

                // ServiceChannelOpenStart (OneStringsTemplateA, opcode=1)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "ServiceChannelOpenStart", 701);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(1);
                });

                // ServiceChannelOpenStop (OneStringsTemplateA, opcode=2)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "ServiceChannelOpenStop", 702);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(2);
                });

                // ServiceChannelCallStart (OneStringsTemplateA, opcode=1)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "ServiceChannelCallStart", 703);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(1);
                });

                // ServiceChannelBeginCallStart (OneStringsTemplateA, opcode=1)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "ServiceChannelBeginCallStart", 704);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(1);
                });

                // HttpSendMessageStart (OneStringsTemplateA, opcode=1)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "HttpSendMessageStart", 706);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(1);
                });

                // HttpSendStop (OneStringsTemplateA, opcode=2)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "HttpSendStop", 707);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(2);
                });

                // HttpMessageReceiveStart (OneStringsTemplateA, opcode=1)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "HttpMessageReceiveStart", 708);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(1);
                });

                // DispatchMessageStart (TwoStringsTemplateVA, opcode=49)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "DispatchMessageStart", 709);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(49);
                });

                // HttpContextBeforeProcessAuthentication (OneStringsTemplateA, opcode=128)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "HttpContextBeforeProcessAuthentication", 710);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(128);
                });

                // DispatchMessageBeforeAuthorization (OneStringsTemplateA, opcode=48)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "DispatchMessageBeforeAuthorization", 711);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(48);
                });

                // DispatchMessageStop (OneStringsTemplateA, opcode=50)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "DispatchMessageStop", 712);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(50);
                });

                // ClientChannelOpenStart (OneStringsTemplateA, opcode=14)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "ClientChannelOpenStart", 715);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(14);
                });

                // ClientChannelOpenStop (OneStringsTemplateA, opcode=15)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "ClientChannelOpenStop", 716);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(15);
                });

                // HttpSendStreamedMessageStart (OneStringsTemplateA, opcode=1)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "HttpSendStreamedMessageStart", 717);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(1);
                });

                // WorkflowApplicationCompleted (TwoStringsTemplateA, opcode=134)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "WorkflowApplicationCompleted", 1001);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(134);
                });

                // WorkflowApplicationTerminated (ThreeStringsTemplateEA, opcode=140)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "WorkflowApplicationTerminated", 1002);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(140);
                });

                // WorkflowInstanceCanceled (TwoStringsTemplateA, opcode=137)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "WorkflowInstanceCanceled", 1003);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(137);
                });

                // WorkflowInstanceAborted (ThreeStringsTemplateEA, opcode=136)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "WorkflowInstanceAborted", 1004);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(136);
                });

                // WorkflowApplicationIdled (TwoStringsTemplateA, opcode=135)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "WorkflowApplicationIdled", 1005);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(135);
                });

                // WorkflowApplicationUnhandledException (SixStringsTemplateEA, opcode=141)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "WorkflowApplicationUnhandledException", 1006);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(141);
                });

                // WorkflowApplicationPersisted (TwoStringsTemplateA, opcode=139)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "WorkflowApplicationPersisted", 1007);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(139);
                });

                // WorkflowApplicationUnloaded (TwoStringsTemplateA, opcode=142)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "WorkflowApplicationUnloaded", 1008);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(142);
                });

                // ActivityScheduled (SevenStringsTemplateA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "ActivityScheduled", 1009);
                    pw.WriteV5MetadataParameterList();
                });

                // ActivityCompleted (FiveStringsTemplateA, opcode=0)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "ActivityCompleted", 1010);
                    pw.WriteV5MetadataParameterList();
                });

                // ScheduleExecuteActivityWorkItem (FourStringsTemplateA, opcode=110)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "ScheduleExecuteActivityWorkItem", 1011);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(110);
                });

                // StartExecuteActivityWorkItem (FourStringsTemplateA, opcode=120)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "StartExecuteActivityWorkItem", 1012);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(120);
                });

                // CompleteExecuteActivityWorkItem (FourStringsTemplateA, opcode=24)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "CompleteExecuteActivityWorkItem", 1013);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(24);
                });

                // ScheduleCompletionWorkItem (SevenStringsTemplateA, opcode=109)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "ScheduleCompletionWorkItem", 1014);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(109);
                });

                // StartCompletionWorkItem (SevenStringsTemplateA, opcode=119)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "StartCompletionWorkItem", 1015);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(119);
                });

                // CompleteCompletionWorkItem (SevenStringsTemplateA, opcode=23)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "CompleteCompletionWorkItem", 1016);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(23);
                });

                // ScheduleCancelActivityWorkItem (FourStringsTemplateA, opcode=108)
                w.WriteMetadataEventBlobV5OrLess(pw =>
                {
                    pw.WriteV5InitialMetadataBlob(id++, ProviderName, "ScheduleCancelActivityWorkItem", 1017);
                    pw.WriteV5MetadataParameterList();
                    pw.WriteV5OpcodeMetadataTag(108);
                });

                // StartCancelActivityWorkItem (FourStringsTemplateA, opcode=118)
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

        private void Subscribe_Chunk03(ApplicationServerTraceEventParser parser, Dictionary<string, Dictionary<string, object>> firedEvents)
        {
            // CBAMatchFound (Multidata70TemplateA)
            parser.CBAMatchFound += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["IncomingAddress"] = e.IncomingAddress;
                fields["AppDomain"] = e.AppDomain;
                firedEvents["CBAMatchFound"] = fields;
            };

            // AspNetRoutingService (Multidata70TemplateA)
            parser.AspNetRoutingService += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["IncomingAddress"] = e.IncomingAddress;
                fields["AppDomain"] = e.AppDomain;
                firedEvents["AspNetRoutingService"] = fields;
            };

            // AspNetRoute (Multidata71TemplateA)
            parser.AspNetRoute += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["AspNetRoutePrefix"] = e.AspNetRoutePrefix;
                fields["ServiceType"] = e.ServiceType;
                fields["ServiceHostFactoryType"] = e.ServiceHostFactoryType;
                fields["AppDomain"] = e.AppDomain;
                firedEvents["AspNetRoute"] = fields;
            };

            // IncrementBusyCount (Multidata75TemplateA)
            parser.IncrementBusyCount += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["Data"] = e.Data;
                fields["AppDomain"] = e.AppDomain;
                firedEvents["IncrementBusyCount"] = fields;
            };

            // DecrementBusyCount (Multidata75TemplateA)
            parser.DecrementBusyCount += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["Data"] = e.Data;
                fields["AppDomain"] = e.AppDomain;
                firedEvents["DecrementBusyCount"] = fields;
            };

            // ServiceChannelOpenStart (OneStringsTemplateA)
            parser.ServiceChannelOpenStart += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = e.AppDomain;
                firedEvents["ServiceChannelOpenStart"] = fields;
            };

            // ServiceChannelOpenStop (OneStringsTemplateA)
            parser.ServiceChannelOpenStop += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = e.AppDomain;
                firedEvents["ServiceChannelOpenStop"] = fields;
            };

            // ServiceChannelCallStart (OneStringsTemplateA)
            parser.ServiceChannelCallStart += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = e.AppDomain;
                firedEvents["ServiceChannelCallStart"] = fields;
            };

            // ServiceChannelBeginCallStart (OneStringsTemplateA)
            parser.ServiceChannelBeginCallStart += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = e.AppDomain;
                firedEvents["ServiceChannelBeginCallStart"] = fields;
            };

            // HttpSendMessageStart (OneStringsTemplateA)
            parser.HttpSendMessageStart += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = e.AppDomain;
                firedEvents["HttpSendMessageStart"] = fields;
            };

            // HttpSendStop (OneStringsTemplateA)
            parser.HttpSendStop += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = e.AppDomain;
                firedEvents["HttpSendStop"] = fields;
            };

            // HttpMessageReceiveStart (OneStringsTemplateA)
            parser.HttpMessageReceiveStart += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = e.AppDomain;
                firedEvents["HttpMessageReceiveStart"] = fields;
            };

            // DispatchMessageStart (TwoStringsTemplateVA)
            parser.DispatchMessageStart += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["HostReference"] = e.HostReference;
                fields["AppDomain"] = e.AppDomain;
                firedEvents["DispatchMessageStart"] = fields;
            };

            // HttpContextBeforeProcessAuthentication (OneStringsTemplateA)
            parser.HttpContextBeforeProcessAuthentication += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = e.AppDomain;
                firedEvents["HttpContextBeforeProcessAuthentication"] = fields;
            };

            // DispatchMessageBeforeAuthorization (OneStringsTemplateA)
            parser.DispatchMessageBeforeAuthorization += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = e.AppDomain;
                firedEvents["DispatchMessageBeforeAuthorization"] = fields;
            };

            // DispatchMessageStop (OneStringsTemplateA)
            parser.DispatchMessageStop += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = e.AppDomain;
                firedEvents["DispatchMessageStop"] = fields;
            };

            // ClientChannelOpenStart (OneStringsTemplateA)
            parser.ClientChannelOpenStart += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = e.AppDomain;
                firedEvents["ClientChannelOpenStart"] = fields;
            };

            // ClientChannelOpenStop (OneStringsTemplateA)
            parser.ClientChannelOpenStop += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = e.AppDomain;
                firedEvents["ClientChannelOpenStop"] = fields;
            };

            // HttpSendStreamedMessageStart (OneStringsTemplateA)
            parser.HttpSendStreamedMessageStart += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = e.AppDomain;
                firedEvents["HttpSendStreamedMessageStart"] = fields;
            };

            // WorkflowApplicationCompleted (TwoStringsTemplateA)
            parser.WorkflowApplicationCompleted += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["AppDomain"] = e.AppDomain;
                firedEvents["WorkflowApplicationCompleted"] = fields;
            };

            // WorkflowApplicationTerminated (ThreeStringsTemplateEA)
            parser.WorkflowApplicationTerminated += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["SerializedException"] = e.SerializedException;
                fields["AppDomain"] = e.AppDomain;
                firedEvents["WorkflowApplicationTerminated"] = fields;
            };

            // WorkflowInstanceCanceled (TwoStringsTemplateA)
            parser.WorkflowInstanceCanceled += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["AppDomain"] = e.AppDomain;
                firedEvents["WorkflowInstanceCanceled"] = fields;
            };

            // WorkflowInstanceAborted (ThreeStringsTemplateEA)
            parser.WorkflowInstanceAborted += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["SerializedException"] = e.SerializedException;
                fields["AppDomain"] = e.AppDomain;
                firedEvents["WorkflowInstanceAborted"] = fields;
            };

            // WorkflowApplicationIdled (TwoStringsTemplateA)
            parser.WorkflowApplicationIdled += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["AppDomain"] = e.AppDomain;
                firedEvents["WorkflowApplicationIdled"] = fields;
            };

            // WorkflowApplicationUnhandledException (SixStringsTemplateEA)
            parser.WorkflowApplicationUnhandledException += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["data2"] = e.data2;
                fields["data3"] = e.data3;
                fields["data4"] = e.data4;
                fields["SerializedException"] = e.SerializedException;
                fields["AppDomain"] = e.AppDomain;
                firedEvents["WorkflowApplicationUnhandledException"] = fields;
            };

            // WorkflowApplicationPersisted (TwoStringsTemplateA)
            parser.WorkflowApplicationPersisted += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["AppDomain"] = e.AppDomain;
                firedEvents["WorkflowApplicationPersisted"] = fields;
            };

            // WorkflowApplicationUnloaded (TwoStringsTemplateA)
            parser.WorkflowApplicationUnloaded += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["AppDomain"] = e.AppDomain;
                firedEvents["WorkflowApplicationUnloaded"] = fields;
            };

            // ActivityScheduled (SevenStringsTemplateA)
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
                firedEvents["ActivityScheduled"] = fields;
            };

            // ActivityCompleted (FiveStringsTemplateA)
            parser.ActivityCompleted += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["data2"] = e.data2;
                fields["data3"] = e.data3;
                fields["data4"] = e.data4;
                fields["AppDomain"] = e.AppDomain;
                firedEvents["ActivityCompleted"] = fields;
            };

            // ScheduleExecuteActivityWorkItem (FourStringsTemplateA)
            parser.ScheduleExecuteActivityWorkItem += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["data2"] = e.data2;
                fields["data3"] = e.data3;
                fields["AppDomain"] = e.AppDomain;
                firedEvents["ScheduleExecuteActivityWorkItem"] = fields;
            };

            // StartExecuteActivityWorkItem (FourStringsTemplateA)
            parser.StartExecuteActivityWorkItem += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["data2"] = e.data2;
                fields["data3"] = e.data3;
                fields["AppDomain"] = e.AppDomain;
                firedEvents["StartExecuteActivityWorkItem"] = fields;
            };

            // CompleteExecuteActivityWorkItem (FourStringsTemplateA)
            parser.CompleteExecuteActivityWorkItem += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["data2"] = e.data2;
                fields["data3"] = e.data3;
                fields["AppDomain"] = e.AppDomain;
                firedEvents["CompleteExecuteActivityWorkItem"] = fields;
            };

            // ScheduleCompletionWorkItem (SevenStringsTemplateA)
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
                firedEvents["ScheduleCompletionWorkItem"] = fields;
            };

            // StartCompletionWorkItem (SevenStringsTemplateA)
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
                firedEvents["StartCompletionWorkItem"] = fields;
            };

            // CompleteCompletionWorkItem (SevenStringsTemplateA)
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
                firedEvents["CompleteCompletionWorkItem"] = fields;
            };

            // ScheduleCancelActivityWorkItem (FourStringsTemplateA)
            parser.ScheduleCancelActivityWorkItem += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["data2"] = e.data2;
                fields["data3"] = e.data3;
                fields["AppDomain"] = e.AppDomain;
                firedEvents["ScheduleCancelActivityWorkItem"] = fields;
            };

            // StartCancelActivityWorkItem (FourStringsTemplateA)
            parser.StartCancelActivityWorkItem += e =>
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = e.data1;
                fields["data2"] = e.data2;
                fields["data3"] = e.data3;
                fields["AppDomain"] = e.AppDomain;
                firedEvents["StartCancelActivityWorkItem"] = fields;
            };
        }

        #endregion

        #region Chunk03 Validate

        private void Validate_Chunk03(Dictionary<string, Dictionary<string, object>> firedEvents)
        {
            // CBAMatchFound (Multidata70TemplateA)
            Assert.True(firedEvents.ContainsKey("CBAMatchFound"), "Event CBAMatchFound did not fire.");
            Assert.Equal(TestString(602, "IncomingAddress"), firedEvents["CBAMatchFound"]["IncomingAddress"]);
            Assert.Equal(TestString(602, "AppDomain"), firedEvents["CBAMatchFound"]["AppDomain"]);

            // AspNetRoutingService (Multidata70TemplateA)
            Assert.True(firedEvents.ContainsKey("AspNetRoutingService"), "Event AspNetRoutingService did not fire.");
            Assert.Equal(TestString(603, "IncomingAddress"), firedEvents["AspNetRoutingService"]["IncomingAddress"]);
            Assert.Equal(TestString(603, "AppDomain"), firedEvents["AspNetRoutingService"]["AppDomain"]);

            // AspNetRoute (Multidata71TemplateA)
            Assert.True(firedEvents.ContainsKey("AspNetRoute"), "Event AspNetRoute did not fire.");
            Assert.Equal(TestString(604, "AspNetRoutePrefix"), firedEvents["AspNetRoute"]["AspNetRoutePrefix"]);
            Assert.Equal(TestString(604, "ServiceType"), firedEvents["AspNetRoute"]["ServiceType"]);
            Assert.Equal(TestString(604, "ServiceHostFactoryType"), firedEvents["AspNetRoute"]["ServiceHostFactoryType"]);
            Assert.Equal(TestString(604, "AppDomain"), firedEvents["AspNetRoute"]["AppDomain"]);

            // IncrementBusyCount (Multidata75TemplateA)
            Assert.True(firedEvents.ContainsKey("IncrementBusyCount"), "Event IncrementBusyCount did not fire.");
            Assert.Equal(TestString(605, "Data"), firedEvents["IncrementBusyCount"]["Data"]);
            Assert.Equal(TestString(605, "AppDomain"), firedEvents["IncrementBusyCount"]["AppDomain"]);

            // DecrementBusyCount (Multidata75TemplateA)
            Assert.True(firedEvents.ContainsKey("DecrementBusyCount"), "Event DecrementBusyCount did not fire.");
            Assert.Equal(TestString(606, "Data"), firedEvents["DecrementBusyCount"]["Data"]);
            Assert.Equal(TestString(606, "AppDomain"), firedEvents["DecrementBusyCount"]["AppDomain"]);

            // ServiceChannelOpenStart (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("ServiceChannelOpenStart"), "Event ServiceChannelOpenStart did not fire.");
            Assert.Equal(TestString(701, "AppDomain"), firedEvents["ServiceChannelOpenStart"]["AppDomain"]);

            // ServiceChannelOpenStop (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("ServiceChannelOpenStop"), "Event ServiceChannelOpenStop did not fire.");
            Assert.Equal(TestString(702, "AppDomain"), firedEvents["ServiceChannelOpenStop"]["AppDomain"]);

            // ServiceChannelCallStart (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("ServiceChannelCallStart"), "Event ServiceChannelCallStart did not fire.");
            Assert.Equal(TestString(703, "AppDomain"), firedEvents["ServiceChannelCallStart"]["AppDomain"]);

            // ServiceChannelBeginCallStart (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("ServiceChannelBeginCallStart"), "Event ServiceChannelBeginCallStart did not fire.");
            Assert.Equal(TestString(704, "AppDomain"), firedEvents["ServiceChannelBeginCallStart"]["AppDomain"]);

            // HttpSendMessageStart (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("HttpSendMessageStart"), "Event HttpSendMessageStart did not fire.");
            Assert.Equal(TestString(706, "AppDomain"), firedEvents["HttpSendMessageStart"]["AppDomain"]);

            // HttpSendStop (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("HttpSendStop"), "Event HttpSendStop did not fire.");
            Assert.Equal(TestString(707, "AppDomain"), firedEvents["HttpSendStop"]["AppDomain"]);

            // HttpMessageReceiveStart (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("HttpMessageReceiveStart"), "Event HttpMessageReceiveStart did not fire.");
            Assert.Equal(TestString(708, "AppDomain"), firedEvents["HttpMessageReceiveStart"]["AppDomain"]);

            // DispatchMessageStart (TwoStringsTemplateVA)
            Assert.True(firedEvents.ContainsKey("DispatchMessageStart"), "Event DispatchMessageStart did not fire.");
            Assert.Equal(TestString(709, "HostReference"), firedEvents["DispatchMessageStart"]["HostReference"]);
            Assert.Equal(TestString(709, "AppDomain"), firedEvents["DispatchMessageStart"]["AppDomain"]);

            // HttpContextBeforeProcessAuthentication (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("HttpContextBeforeProcessAuthentication"), "Event HttpContextBeforeProcessAuthentication did not fire.");
            Assert.Equal(TestString(710, "AppDomain"), firedEvents["HttpContextBeforeProcessAuthentication"]["AppDomain"]);

            // DispatchMessageBeforeAuthorization (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("DispatchMessageBeforeAuthorization"), "Event DispatchMessageBeforeAuthorization did not fire.");
            Assert.Equal(TestString(711, "AppDomain"), firedEvents["DispatchMessageBeforeAuthorization"]["AppDomain"]);

            // DispatchMessageStop (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("DispatchMessageStop"), "Event DispatchMessageStop did not fire.");
            Assert.Equal(TestString(712, "AppDomain"), firedEvents["DispatchMessageStop"]["AppDomain"]);

            // ClientChannelOpenStart (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("ClientChannelOpenStart"), "Event ClientChannelOpenStart did not fire.");
            Assert.Equal(TestString(715, "AppDomain"), firedEvents["ClientChannelOpenStart"]["AppDomain"]);

            // ClientChannelOpenStop (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("ClientChannelOpenStop"), "Event ClientChannelOpenStop did not fire.");
            Assert.Equal(TestString(716, "AppDomain"), firedEvents["ClientChannelOpenStop"]["AppDomain"]);

            // HttpSendStreamedMessageStart (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("HttpSendStreamedMessageStart"), "Event HttpSendStreamedMessageStart did not fire.");
            Assert.Equal(TestString(717, "AppDomain"), firedEvents["HttpSendStreamedMessageStart"]["AppDomain"]);

            // WorkflowApplicationCompleted (TwoStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("WorkflowApplicationCompleted"), "Event WorkflowApplicationCompleted did not fire.");
            Assert.Equal(TestString(1001, "data1"), firedEvents["WorkflowApplicationCompleted"]["data1"]);
            Assert.Equal(TestString(1001, "AppDomain"), firedEvents["WorkflowApplicationCompleted"]["AppDomain"]);

            // WorkflowApplicationTerminated (ThreeStringsTemplateEA)
            Assert.True(firedEvents.ContainsKey("WorkflowApplicationTerminated"), "Event WorkflowApplicationTerminated did not fire.");
            Assert.Equal(TestString(1002, "data1"), firedEvents["WorkflowApplicationTerminated"]["data1"]);
            Assert.Equal(TestString(1002, "SerializedException"), firedEvents["WorkflowApplicationTerminated"]["SerializedException"]);
            Assert.Equal(TestString(1002, "AppDomain"), firedEvents["WorkflowApplicationTerminated"]["AppDomain"]);

            // WorkflowInstanceCanceled (TwoStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("WorkflowInstanceCanceled"), "Event WorkflowInstanceCanceled did not fire.");
            Assert.Equal(TestString(1003, "data1"), firedEvents["WorkflowInstanceCanceled"]["data1"]);
            Assert.Equal(TestString(1003, "AppDomain"), firedEvents["WorkflowInstanceCanceled"]["AppDomain"]);

            // WorkflowInstanceAborted (ThreeStringsTemplateEA)
            Assert.True(firedEvents.ContainsKey("WorkflowInstanceAborted"), "Event WorkflowInstanceAborted did not fire.");
            Assert.Equal(TestString(1004, "data1"), firedEvents["WorkflowInstanceAborted"]["data1"]);
            Assert.Equal(TestString(1004, "SerializedException"), firedEvents["WorkflowInstanceAborted"]["SerializedException"]);
            Assert.Equal(TestString(1004, "AppDomain"), firedEvents["WorkflowInstanceAborted"]["AppDomain"]);

            // WorkflowApplicationIdled (TwoStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("WorkflowApplicationIdled"), "Event WorkflowApplicationIdled did not fire.");
            Assert.Equal(TestString(1005, "data1"), firedEvents["WorkflowApplicationIdled"]["data1"]);
            Assert.Equal(TestString(1005, "AppDomain"), firedEvents["WorkflowApplicationIdled"]["AppDomain"]);

            // WorkflowApplicationUnhandledException (SixStringsTemplateEA)
            Assert.True(firedEvents.ContainsKey("WorkflowApplicationUnhandledException"), "Event WorkflowApplicationUnhandledException did not fire.");
            Assert.Equal(TestString(1006, "data1"), firedEvents["WorkflowApplicationUnhandledException"]["data1"]);
            Assert.Equal(TestString(1006, "data2"), firedEvents["WorkflowApplicationUnhandledException"]["data2"]);
            Assert.Equal(TestString(1006, "data3"), firedEvents["WorkflowApplicationUnhandledException"]["data3"]);
            Assert.Equal(TestString(1006, "data4"), firedEvents["WorkflowApplicationUnhandledException"]["data4"]);
            Assert.Equal(TestString(1006, "SerializedException"), firedEvents["WorkflowApplicationUnhandledException"]["SerializedException"]);
            Assert.Equal(TestString(1006, "AppDomain"), firedEvents["WorkflowApplicationUnhandledException"]["AppDomain"]);

            // WorkflowApplicationPersisted (TwoStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("WorkflowApplicationPersisted"), "Event WorkflowApplicationPersisted did not fire.");
            Assert.Equal(TestString(1007, "data1"), firedEvents["WorkflowApplicationPersisted"]["data1"]);
            Assert.Equal(TestString(1007, "AppDomain"), firedEvents["WorkflowApplicationPersisted"]["AppDomain"]);

            // WorkflowApplicationUnloaded (TwoStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("WorkflowApplicationUnloaded"), "Event WorkflowApplicationUnloaded did not fire.");
            Assert.Equal(TestString(1008, "data1"), firedEvents["WorkflowApplicationUnloaded"]["data1"]);
            Assert.Equal(TestString(1008, "AppDomain"), firedEvents["WorkflowApplicationUnloaded"]["AppDomain"]);

            // ActivityScheduled (SevenStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("ActivityScheduled"), "Event ActivityScheduled did not fire.");
            Assert.Equal(TestString(1009, "data1"), firedEvents["ActivityScheduled"]["data1"]);
            Assert.Equal(TestString(1009, "data2"), firedEvents["ActivityScheduled"]["data2"]);
            Assert.Equal(TestString(1009, "data3"), firedEvents["ActivityScheduled"]["data3"]);
            Assert.Equal(TestString(1009, "data4"), firedEvents["ActivityScheduled"]["data4"]);
            Assert.Equal(TestString(1009, "data5"), firedEvents["ActivityScheduled"]["data5"]);
            Assert.Equal(TestString(1009, "data6"), firedEvents["ActivityScheduled"]["data6"]);
            Assert.Equal(TestString(1009, "AppDomain"), firedEvents["ActivityScheduled"]["AppDomain"]);

            // ActivityCompleted (FiveStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("ActivityCompleted"), "Event ActivityCompleted did not fire.");
            Assert.Equal(TestString(1010, "data1"), firedEvents["ActivityCompleted"]["data1"]);
            Assert.Equal(TestString(1010, "data2"), firedEvents["ActivityCompleted"]["data2"]);
            Assert.Equal(TestString(1010, "data3"), firedEvents["ActivityCompleted"]["data3"]);
            Assert.Equal(TestString(1010, "data4"), firedEvents["ActivityCompleted"]["data4"]);
            Assert.Equal(TestString(1010, "AppDomain"), firedEvents["ActivityCompleted"]["AppDomain"]);

            // ScheduleExecuteActivityWorkItem (FourStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("ScheduleExecuteActivityWorkItem"), "Event ScheduleExecuteActivityWorkItem did not fire.");
            Assert.Equal(TestString(1011, "data1"), firedEvents["ScheduleExecuteActivityWorkItem"]["data1"]);
            Assert.Equal(TestString(1011, "data2"), firedEvents["ScheduleExecuteActivityWorkItem"]["data2"]);
            Assert.Equal(TestString(1011, "data3"), firedEvents["ScheduleExecuteActivityWorkItem"]["data3"]);
            Assert.Equal(TestString(1011, "AppDomain"), firedEvents["ScheduleExecuteActivityWorkItem"]["AppDomain"]);

            // StartExecuteActivityWorkItem (FourStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("StartExecuteActivityWorkItem"), "Event StartExecuteActivityWorkItem did not fire.");
            Assert.Equal(TestString(1012, "data1"), firedEvents["StartExecuteActivityWorkItem"]["data1"]);
            Assert.Equal(TestString(1012, "data2"), firedEvents["StartExecuteActivityWorkItem"]["data2"]);
            Assert.Equal(TestString(1012, "data3"), firedEvents["StartExecuteActivityWorkItem"]["data3"]);
            Assert.Equal(TestString(1012, "AppDomain"), firedEvents["StartExecuteActivityWorkItem"]["AppDomain"]);

            // CompleteExecuteActivityWorkItem (FourStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("CompleteExecuteActivityWorkItem"), "Event CompleteExecuteActivityWorkItem did not fire.");
            Assert.Equal(TestString(1013, "data1"), firedEvents["CompleteExecuteActivityWorkItem"]["data1"]);
            Assert.Equal(TestString(1013, "data2"), firedEvents["CompleteExecuteActivityWorkItem"]["data2"]);
            Assert.Equal(TestString(1013, "data3"), firedEvents["CompleteExecuteActivityWorkItem"]["data3"]);
            Assert.Equal(TestString(1013, "AppDomain"), firedEvents["CompleteExecuteActivityWorkItem"]["AppDomain"]);

            // ScheduleCompletionWorkItem (SevenStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("ScheduleCompletionWorkItem"), "Event ScheduleCompletionWorkItem did not fire.");
            Assert.Equal(TestString(1014, "data1"), firedEvents["ScheduleCompletionWorkItem"]["data1"]);
            Assert.Equal(TestString(1014, "data2"), firedEvents["ScheduleCompletionWorkItem"]["data2"]);
            Assert.Equal(TestString(1014, "data3"), firedEvents["ScheduleCompletionWorkItem"]["data3"]);
            Assert.Equal(TestString(1014, "data4"), firedEvents["ScheduleCompletionWorkItem"]["data4"]);
            Assert.Equal(TestString(1014, "data5"), firedEvents["ScheduleCompletionWorkItem"]["data5"]);
            Assert.Equal(TestString(1014, "data6"), firedEvents["ScheduleCompletionWorkItem"]["data6"]);
            Assert.Equal(TestString(1014, "AppDomain"), firedEvents["ScheduleCompletionWorkItem"]["AppDomain"]);

            // StartCompletionWorkItem (SevenStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("StartCompletionWorkItem"), "Event StartCompletionWorkItem did not fire.");
            Assert.Equal(TestString(1015, "data1"), firedEvents["StartCompletionWorkItem"]["data1"]);
            Assert.Equal(TestString(1015, "data2"), firedEvents["StartCompletionWorkItem"]["data2"]);
            Assert.Equal(TestString(1015, "data3"), firedEvents["StartCompletionWorkItem"]["data3"]);
            Assert.Equal(TestString(1015, "data4"), firedEvents["StartCompletionWorkItem"]["data4"]);
            Assert.Equal(TestString(1015, "data5"), firedEvents["StartCompletionWorkItem"]["data5"]);
            Assert.Equal(TestString(1015, "data6"), firedEvents["StartCompletionWorkItem"]["data6"]);
            Assert.Equal(TestString(1015, "AppDomain"), firedEvents["StartCompletionWorkItem"]["AppDomain"]);

            // CompleteCompletionWorkItem (SevenStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("CompleteCompletionWorkItem"), "Event CompleteCompletionWorkItem did not fire.");
            Assert.Equal(TestString(1016, "data1"), firedEvents["CompleteCompletionWorkItem"]["data1"]);
            Assert.Equal(TestString(1016, "data2"), firedEvents["CompleteCompletionWorkItem"]["data2"]);
            Assert.Equal(TestString(1016, "data3"), firedEvents["CompleteCompletionWorkItem"]["data3"]);
            Assert.Equal(TestString(1016, "data4"), firedEvents["CompleteCompletionWorkItem"]["data4"]);
            Assert.Equal(TestString(1016, "data5"), firedEvents["CompleteCompletionWorkItem"]["data5"]);
            Assert.Equal(TestString(1016, "data6"), firedEvents["CompleteCompletionWorkItem"]["data6"]);
            Assert.Equal(TestString(1016, "AppDomain"), firedEvents["CompleteCompletionWorkItem"]["AppDomain"]);

            // ScheduleCancelActivityWorkItem (FourStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("ScheduleCancelActivityWorkItem"), "Event ScheduleCancelActivityWorkItem did not fire.");
            Assert.Equal(TestString(1017, "data1"), firedEvents["ScheduleCancelActivityWorkItem"]["data1"]);
            Assert.Equal(TestString(1017, "data2"), firedEvents["ScheduleCancelActivityWorkItem"]["data2"]);
            Assert.Equal(TestString(1017, "data3"), firedEvents["ScheduleCancelActivityWorkItem"]["data3"]);
            Assert.Equal(TestString(1017, "AppDomain"), firedEvents["ScheduleCancelActivityWorkItem"]["AppDomain"]);

            // StartCancelActivityWorkItem (FourStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("StartCancelActivityWorkItem"), "Event StartCancelActivityWorkItem did not fire.");
            Assert.Equal(TestString(1018, "data1"), firedEvents["StartCancelActivityWorkItem"]["data1"]);
            Assert.Equal(TestString(1018, "data2"), firedEvents["StartCancelActivityWorkItem"]["data2"]);
            Assert.Equal(TestString(1018, "data3"), firedEvents["StartCancelActivityWorkItem"]["data3"]);
            Assert.Equal(TestString(1018, "AppDomain"), firedEvents["StartCancelActivityWorkItem"]["AppDomain"]);
        }

        #endregion
    }
}
