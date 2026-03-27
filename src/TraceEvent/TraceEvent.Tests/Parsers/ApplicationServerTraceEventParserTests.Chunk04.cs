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
        #region Chunk04 Template Fields

        private static readonly TemplateField[] EightStringsTemplateEAFields_Chunk04 = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("data2", FieldType.UnicodeString),
            new TemplateField("data3", FieldType.UnicodeString),
            new TemplateField("data4", FieldType.UnicodeString),
            new TemplateField("data5", FieldType.UnicodeString),
            new TemplateField("data6", FieldType.UnicodeString),
            new TemplateField("SerializedException", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] FourStringsTemplateAFields_Chunk04 = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("data2", FieldType.UnicodeString),
            new TemplateField("data3", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] Multidata17TemplateAFields_Chunk04 = new TemplateField[]
        {
            new TemplateField("Id", FieldType.Guid),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] OneStringsTemplateAFields_Chunk04 = new TemplateField[]
        {
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] SevenStringsTemplateAFields_Chunk04 = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("data2", FieldType.UnicodeString),
            new TemplateField("data3", FieldType.UnicodeString),
            new TemplateField("data4", FieldType.UnicodeString),
            new TemplateField("data5", FieldType.UnicodeString),
            new TemplateField("data6", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] SixStringsTemplateAFields_Chunk04 = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("data2", FieldType.UnicodeString),
            new TemplateField("data3", FieldType.UnicodeString),
            new TemplateField("data4", FieldType.UnicodeString),
            new TemplateField("data5", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] ThreeStringsTemplateAFields_Chunk04 = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("data2", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        private static readonly TemplateField[] TwoStringsTemplateAFields_Chunk04 = new TemplateField[]
        {
            new TemplateField("data1", FieldType.UnicodeString),
            new TemplateField("AppDomain", FieldType.UnicodeString),
        };

        #endregion

        #region Chunk04 WriteMetadata

        private void WriteMetadata_Chunk04(EventPipeWriterV5 writer, ref int metadataId)
        {
            writer.WriteMetadataBlock(
                new EventMetadata(metadataId++, ProviderName, "CompleteCancelActivityWorkItem", 1019),
                new EventMetadata(metadataId++, ProviderName, "CreateBookmark", 1020),
                new EventMetadata(metadataId++, ProviderName, "ScheduleBookmarkWorkItem", 1021),
                new EventMetadata(metadataId++, ProviderName, "StartBookmarkWorkItem", 1022),
                new EventMetadata(metadataId++, ProviderName, "CompleteBookmarkWorkItem", 1023),
                new EventMetadata(metadataId++, ProviderName, "CreateBookmarkScope", 1024),
                new EventMetadata(metadataId++, ProviderName, "BookmarkScopeInitialized", 1025),
                new EventMetadata(metadataId++, ProviderName, "ScheduleTransactionContextWorkItem", 1026),
                new EventMetadata(metadataId++, ProviderName, "StartTransactionContextWorkItem", 1027),
                new EventMetadata(metadataId++, ProviderName, "CompleteTransactionContextWorkItem", 1028),
                new EventMetadata(metadataId++, ProviderName, "ScheduleFaultWorkItem", 1029),
                new EventMetadata(metadataId++, ProviderName, "StartFaultWorkItem", 1030),
                new EventMetadata(metadataId++, ProviderName, "CompleteFaultWorkItem", 1031),
                new EventMetadata(metadataId++, ProviderName, "ScheduleRuntimeWorkItem", 1032),
                new EventMetadata(metadataId++, ProviderName, "StartRuntimeWorkItem", 1033),
                new EventMetadata(metadataId++, ProviderName, "CompleteRuntimeWorkItem", 1034),
                new EventMetadata(metadataId++, ProviderName, "RuntimeTransactionSet", 1035),
                new EventMetadata(metadataId++, ProviderName, "RuntimeTransactionCompletionRequested", 1036),
                new EventMetadata(metadataId++, ProviderName, "RuntimeTransactionComplete", 1037),
                new EventMetadata(metadataId++, ProviderName, "EnterNoPersistBlock", 1038),
                new EventMetadata(metadataId++, ProviderName, "ExitNoPersistBlock", 1039),
                new EventMetadata(metadataId++, ProviderName, "InArgumentBound", 1040),
                new EventMetadata(metadataId++, ProviderName, "WorkflowApplicationPersistableIdle", 1041),
                new EventMetadata(metadataId++, ProviderName, "WorkflowActivityStart", 1101),
                new EventMetadata(metadataId++, ProviderName, "WorkflowActivityStop", 1102),
                new EventMetadata(metadataId++, ProviderName, "WorkflowActivitySuspend", 1103),
                new EventMetadata(metadataId++, ProviderName, "WorkflowActivityResume", 1104),
                new EventMetadata(metadataId++, ProviderName, "InvokeMethodIsStatic", 1124),
                new EventMetadata(metadataId++, ProviderName, "InvokeMethodIsNotStatic", 1125),
                new EventMetadata(metadataId++, ProviderName, "InvokedMethodThrewException", 1126),
                new EventMetadata(metadataId++, ProviderName, "InvokeMethodUseAsyncPattern", 1131),
                new EventMetadata(metadataId++, ProviderName, "InvokeMethodDoesNotUseAsyncPattern", 1132),
                new EventMetadata(metadataId++, ProviderName, "FlowchartStart", 1140),
                new EventMetadata(metadataId++, ProviderName, "FlowchartEmpty", 1141),
                new EventMetadata(metadataId++, ProviderName, "FlowchartNextNull", 1143),
                new EventMetadata(metadataId++, ProviderName, "FlowchartSwitchCase", 1146),
                new EventMetadata(metadataId++, ProviderName, "FlowchartSwitchDefault", 1147)
            );
        }

        #endregion

        #region Chunk04 WriteEvents

        private void WriteEvents_Chunk04(EventPipeWriterV5 writer, ref int metadataId, ref int sequenceNumber)
        {
            int __metadataId = metadataId;
            int __sequenceNumber = sequenceNumber;
            writer.WriteEventBlock(w =>
            {
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1019, FourStringsTemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1020, SixStringsTemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1021, SixStringsTemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1022, SixStringsTemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1023, SixStringsTemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1024, TwoStringsTemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1025, ThreeStringsTemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1026, FourStringsTemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1027, FourStringsTemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1028, FourStringsTemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1029, EightStringsTemplateEAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1030, EightStringsTemplateEAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1031, EightStringsTemplateEAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1032, FourStringsTemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1033, FourStringsTemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1034, FourStringsTemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1035, SevenStringsTemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1036, FourStringsTemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1037, TwoStringsTemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1038, OneStringsTemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1039, OneStringsTemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1040, SixStringsTemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1041, ThreeStringsTemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1101, Multidata17TemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1102, Multidata17TemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1103, Multidata17TemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1104, Multidata17TemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1124, TwoStringsTemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1125, TwoStringsTemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1126, ThreeStringsTemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1131, FourStringsTemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1132, TwoStringsTemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1140, TwoStringsTemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1141, TwoStringsTemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1143, TwoStringsTemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1146, ThreeStringsTemplateAFields_Chunk04));
                w.WriteEventBlobV4Or5(__metadataId++, 999, __sequenceNumber++, BuildPayload(1147, TwoStringsTemplateAFields_Chunk04));
            });
        
            metadataId = __metadataId;
            sequenceNumber = __sequenceNumber;
        }

        #endregion

        #region Chunk04 Subscribe

        private void Subscribe_Chunk04(ApplicationServerTraceEventParser parser, Dictionary<string, Dictionary<string, object>> firedEvents)
        {
            // 1019 CompleteCancelActivityWorkItem (FourStringsTemplateATraceData)
            parser.CompleteCancelActivityWorkItem += delegate (FourStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["data2"] = data.data2;
                fields["data3"] = data.data3;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["CompleteCancelActivityWorkItem"] = fields;
            };

            // 1020 CreateBookmark (SixStringsTemplateATraceData)
            parser.CreateBookmark += delegate (SixStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["data2"] = data.data2;
                fields["data3"] = data.data3;
                fields["data4"] = data.data4;
                fields["data5"] = data.data5;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["CreateBookmark"] = fields;
            };

            // 1021 ScheduleBookmarkWorkItem (SixStringsTemplateATraceData)
            parser.ScheduleBookmarkWorkItem += delegate (SixStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["data2"] = data.data2;
                fields["data3"] = data.data3;
                fields["data4"] = data.data4;
                fields["data5"] = data.data5;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["ScheduleBookmarkWorkItem"] = fields;
            };

            // 1022 StartBookmarkWorkItem (SixStringsTemplateATraceData)
            parser.StartBookmarkWorkItem += delegate (SixStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["data2"] = data.data2;
                fields["data3"] = data.data3;
                fields["data4"] = data.data4;
                fields["data5"] = data.data5;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["StartBookmarkWorkItem"] = fields;
            };

            // 1023 CompleteBookmarkWorkItem (SixStringsTemplateATraceData)
            parser.CompleteBookmarkWorkItem += delegate (SixStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["data2"] = data.data2;
                fields["data3"] = data.data3;
                fields["data4"] = data.data4;
                fields["data5"] = data.data5;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["CompleteBookmarkWorkItem"] = fields;
            };

            // 1024 CreateBookmarkScope (TwoStringsTemplateATraceData)
            parser.CreateBookmarkScope += delegate (TwoStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["CreateBookmarkScope"] = fields;
            };

            // 1025 BookmarkScopeInitialized (ThreeStringsTemplateATraceData)
            parser.BookmarkScopeInitialized += delegate (ThreeStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["data2"] = data.data2;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["BookmarkScopeInitialized"] = fields;
            };

            // 1026 ScheduleTransactionContextWorkItem (FourStringsTemplateATraceData)
            parser.ScheduleTransactionContextWorkItem += delegate (FourStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["data2"] = data.data2;
                fields["data3"] = data.data3;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["ScheduleTransactionContextWorkItem"] = fields;
            };

            // 1027 StartTransactionContextWorkItem (FourStringsTemplateATraceData)
            parser.StartTransactionContextWorkItem += delegate (FourStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["data2"] = data.data2;
                fields["data3"] = data.data3;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["StartTransactionContextWorkItem"] = fields;
            };

            // 1028 CompleteTransactionContextWorkItem (FourStringsTemplateATraceData)
            parser.CompleteTransactionContextWorkItem += delegate (FourStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["data2"] = data.data2;
                fields["data3"] = data.data3;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["CompleteTransactionContextWorkItem"] = fields;
            };

            // 1029 ScheduleFaultWorkItem (EightStringsTemplateEATraceData)
            parser.ScheduleFaultWorkItem += delegate (EightStringsTemplateEATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["data2"] = data.data2;
                fields["data3"] = data.data3;
                fields["data4"] = data.data4;
                fields["data5"] = data.data5;
                fields["data6"] = data.data6;
                fields["SerializedException"] = data.SerializedException;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["ScheduleFaultWorkItem"] = fields;
            };

            // 1030 StartFaultWorkItem (EightStringsTemplateEATraceData)
            parser.StartFaultWorkItem += delegate (EightStringsTemplateEATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["data2"] = data.data2;
                fields["data3"] = data.data3;
                fields["data4"] = data.data4;
                fields["data5"] = data.data5;
                fields["data6"] = data.data6;
                fields["SerializedException"] = data.SerializedException;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["StartFaultWorkItem"] = fields;
            };

            // 1031 CompleteFaultWorkItem (EightStringsTemplateEATraceData)
            parser.CompleteFaultWorkItem += delegate (EightStringsTemplateEATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["data2"] = data.data2;
                fields["data3"] = data.data3;
                fields["data4"] = data.data4;
                fields["data5"] = data.data5;
                fields["data6"] = data.data6;
                fields["SerializedException"] = data.SerializedException;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["CompleteFaultWorkItem"] = fields;
            };

            // 1032 ScheduleRuntimeWorkItem (FourStringsTemplateATraceData)
            parser.ScheduleRuntimeWorkItem += delegate (FourStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["data2"] = data.data2;
                fields["data3"] = data.data3;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["ScheduleRuntimeWorkItem"] = fields;
            };

            // 1033 StartRuntimeWorkItem (FourStringsTemplateATraceData)
            parser.StartRuntimeWorkItem += delegate (FourStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["data2"] = data.data2;
                fields["data3"] = data.data3;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["StartRuntimeWorkItem"] = fields;
            };

            // 1034 CompleteRuntimeWorkItem (FourStringsTemplateATraceData)
            parser.CompleteRuntimeWorkItem += delegate (FourStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["data2"] = data.data2;
                fields["data3"] = data.data3;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["CompleteRuntimeWorkItem"] = fields;
            };

            // 1035 RuntimeTransactionSet (SevenStringsTemplateATraceData)
            parser.RuntimeTransactionSet += delegate (SevenStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["data2"] = data.data2;
                fields["data3"] = data.data3;
                fields["data4"] = data.data4;
                fields["data5"] = data.data5;
                fields["data6"] = data.data6;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["RuntimeTransactionSet"] = fields;
            };

            // 1036 RuntimeTransactionCompletionRequested (FourStringsTemplateATraceData)
            parser.RuntimeTransactionCompletionRequested += delegate (FourStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["data2"] = data.data2;
                fields["data3"] = data.data3;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["RuntimeTransactionCompletionRequested"] = fields;
            };

            // 1037 RuntimeTransactionComplete (TwoStringsTemplateATraceData)
            parser.RuntimeTransactionComplete += delegate (TwoStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["RuntimeTransactionComplete"] = fields;
            };

            // 1038 EnterNoPersistBlock (OneStringsTemplateATraceData)
            parser.EnterNoPersistBlock += delegate (OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents["EnterNoPersistBlock"] = fields;
            };

            // 1039 ExitNoPersistBlock (OneStringsTemplateATraceData)
            parser.ExitNoPersistBlock += delegate (OneStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["AppDomain"] = data.AppDomain;
                firedEvents["ExitNoPersistBlock"] = fields;
            };

            // 1040 InArgumentBound (SixStringsTemplateATraceData)
            parser.InArgumentBound += delegate (SixStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["data2"] = data.data2;
                fields["data3"] = data.data3;
                fields["data4"] = data.data4;
                fields["data5"] = data.data5;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["InArgumentBound"] = fields;
            };

            // 1041 WorkflowApplicationPersistableIdle (ThreeStringsTemplateATraceData)
            parser.WorkflowApplicationPersistableIdle += delegate (ThreeStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["data2"] = data.data2;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["WorkflowApplicationPersistableIdle"] = fields;
            };

            // 1101 WorkflowActivityStart (Multidata17TemplateATraceData)
            parser.WorkflowActivityStart += delegate (Multidata17TemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["Id"] = data.Id;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["WorkflowActivityStart"] = fields;
            };

            // 1102 WorkflowActivityStop (Multidata17TemplateATraceData)
            parser.WorkflowActivityStop += delegate (Multidata17TemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["Id"] = data.Id;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["WorkflowActivityStop"] = fields;
            };

            // 1103 WorkflowActivitySuspend (Multidata17TemplateATraceData)
            parser.WorkflowActivitySuspend += delegate (Multidata17TemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["Id"] = data.Id;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["WorkflowActivitySuspend"] = fields;
            };

            // 1104 WorkflowActivityResume (Multidata17TemplateATraceData)
            parser.WorkflowActivityResume += delegate (Multidata17TemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["Id"] = data.Id;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["WorkflowActivityResume"] = fields;
            };

            // 1124 InvokeMethodIsStatic (TwoStringsTemplateATraceData)
            parser.InvokeMethodIsStatic += delegate (TwoStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["InvokeMethodIsStatic"] = fields;
            };

            // 1125 InvokeMethodIsNotStatic (TwoStringsTemplateATraceData)
            parser.InvokeMethodIsNotStatic += delegate (TwoStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["InvokeMethodIsNotStatic"] = fields;
            };

            // 1126 InvokedMethodThrewException (ThreeStringsTemplateATraceData)
            parser.InvokedMethodThrewException += delegate (ThreeStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["data2"] = data.data2;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["InvokedMethodThrewException"] = fields;
            };

            // 1131 InvokeMethodUseAsyncPattern (FourStringsTemplateATraceData)
            parser.InvokeMethodUseAsyncPattern += delegate (FourStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["data2"] = data.data2;
                fields["data3"] = data.data3;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["InvokeMethodUseAsyncPattern"] = fields;
            };

            // 1132 InvokeMethodDoesNotUseAsyncPattern (TwoStringsTemplateATraceData)
            parser.InvokeMethodDoesNotUseAsyncPattern += delegate (TwoStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["InvokeMethodDoesNotUseAsyncPattern"] = fields;
            };

            // 1140 FlowchartStart (TwoStringsTemplateATraceData)
            parser.FlowchartStart += delegate (TwoStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["FlowchartStart"] = fields;
            };

            // 1141 FlowchartEmpty (TwoStringsTemplateATraceData)
            parser.FlowchartEmpty += delegate (TwoStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["FlowchartEmpty"] = fields;
            };

            // 1143 FlowchartNextNull (TwoStringsTemplateATraceData)
            parser.FlowchartNextNull += delegate (TwoStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["FlowchartNextNull"] = fields;
            };

            // 1146 FlowchartSwitchCase (ThreeStringsTemplateATraceData)
            parser.FlowchartSwitchCase += delegate (ThreeStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["data2"] = data.data2;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["FlowchartSwitchCase"] = fields;
            };

            // 1147 FlowchartSwitchDefault (TwoStringsTemplateATraceData)
            parser.FlowchartSwitchDefault += delegate (TwoStringsTemplateATraceData data)
            {
                var fields = new Dictionary<string, object>();
                fields["data1"] = data.data1;
                fields["AppDomain"] = data.AppDomain;
                firedEvents["FlowchartSwitchDefault"] = fields;
            };
        }

        #endregion

        #region Chunk04 Validate

        private void Validate_Chunk04(Dictionary<string, Dictionary<string, object>> firedEvents)
        {
            // 1019 CompleteCancelActivityWorkItem (FourStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("CompleteCancelActivityWorkItem"), "Event 1019 CompleteCancelActivityWorkItem did not fire");
            Assert.Equal(TestString(1019, "data1"), firedEvents["CompleteCancelActivityWorkItem"]["data1"]);
            Assert.Equal(TestString(1019, "data2"), firedEvents["CompleteCancelActivityWorkItem"]["data2"]);
            Assert.Equal(TestString(1019, "data3"), firedEvents["CompleteCancelActivityWorkItem"]["data3"]);
            Assert.Equal(TestString(1019, "AppDomain"), firedEvents["CompleteCancelActivityWorkItem"]["AppDomain"]);

            // 1020 CreateBookmark (SixStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("CreateBookmark"), "Event 1020 CreateBookmark did not fire");
            Assert.Equal(TestString(1020, "data1"), firedEvents["CreateBookmark"]["data1"]);
            Assert.Equal(TestString(1020, "data2"), firedEvents["CreateBookmark"]["data2"]);
            Assert.Equal(TestString(1020, "data3"), firedEvents["CreateBookmark"]["data3"]);
            Assert.Equal(TestString(1020, "data4"), firedEvents["CreateBookmark"]["data4"]);
            Assert.Equal(TestString(1020, "data5"), firedEvents["CreateBookmark"]["data5"]);
            Assert.Equal(TestString(1020, "AppDomain"), firedEvents["CreateBookmark"]["AppDomain"]);

            // 1021 ScheduleBookmarkWorkItem (SixStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("ScheduleBookmarkWorkItem"), "Event 1021 ScheduleBookmarkWorkItem did not fire");
            Assert.Equal(TestString(1021, "data1"), firedEvents["ScheduleBookmarkWorkItem"]["data1"]);
            Assert.Equal(TestString(1021, "data2"), firedEvents["ScheduleBookmarkWorkItem"]["data2"]);
            Assert.Equal(TestString(1021, "data3"), firedEvents["ScheduleBookmarkWorkItem"]["data3"]);
            Assert.Equal(TestString(1021, "data4"), firedEvents["ScheduleBookmarkWorkItem"]["data4"]);
            Assert.Equal(TestString(1021, "data5"), firedEvents["ScheduleBookmarkWorkItem"]["data5"]);
            Assert.Equal(TestString(1021, "AppDomain"), firedEvents["ScheduleBookmarkWorkItem"]["AppDomain"]);

            // 1022 StartBookmarkWorkItem (SixStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("StartBookmarkWorkItem"), "Event 1022 StartBookmarkWorkItem did not fire");
            Assert.Equal(TestString(1022, "data1"), firedEvents["StartBookmarkWorkItem"]["data1"]);
            Assert.Equal(TestString(1022, "data2"), firedEvents["StartBookmarkWorkItem"]["data2"]);
            Assert.Equal(TestString(1022, "data3"), firedEvents["StartBookmarkWorkItem"]["data3"]);
            Assert.Equal(TestString(1022, "data4"), firedEvents["StartBookmarkWorkItem"]["data4"]);
            Assert.Equal(TestString(1022, "data5"), firedEvents["StartBookmarkWorkItem"]["data5"]);
            Assert.Equal(TestString(1022, "AppDomain"), firedEvents["StartBookmarkWorkItem"]["AppDomain"]);

            // 1023 CompleteBookmarkWorkItem (SixStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("CompleteBookmarkWorkItem"), "Event 1023 CompleteBookmarkWorkItem did not fire");
            Assert.Equal(TestString(1023, "data1"), firedEvents["CompleteBookmarkWorkItem"]["data1"]);
            Assert.Equal(TestString(1023, "data2"), firedEvents["CompleteBookmarkWorkItem"]["data2"]);
            Assert.Equal(TestString(1023, "data3"), firedEvents["CompleteBookmarkWorkItem"]["data3"]);
            Assert.Equal(TestString(1023, "data4"), firedEvents["CompleteBookmarkWorkItem"]["data4"]);
            Assert.Equal(TestString(1023, "data5"), firedEvents["CompleteBookmarkWorkItem"]["data5"]);
            Assert.Equal(TestString(1023, "AppDomain"), firedEvents["CompleteBookmarkWorkItem"]["AppDomain"]);

            // 1024 CreateBookmarkScope (TwoStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("CreateBookmarkScope"), "Event 1024 CreateBookmarkScope did not fire");
            Assert.Equal(TestString(1024, "data1"), firedEvents["CreateBookmarkScope"]["data1"]);
            Assert.Equal(TestString(1024, "AppDomain"), firedEvents["CreateBookmarkScope"]["AppDomain"]);

            // 1025 BookmarkScopeInitialized (ThreeStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("BookmarkScopeInitialized"), "Event 1025 BookmarkScopeInitialized did not fire");
            Assert.Equal(TestString(1025, "data1"), firedEvents["BookmarkScopeInitialized"]["data1"]);
            Assert.Equal(TestString(1025, "data2"), firedEvents["BookmarkScopeInitialized"]["data2"]);
            Assert.Equal(TestString(1025, "AppDomain"), firedEvents["BookmarkScopeInitialized"]["AppDomain"]);

            // 1026 ScheduleTransactionContextWorkItem (FourStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("ScheduleTransactionContextWorkItem"), "Event 1026 ScheduleTransactionContextWorkItem did not fire");
            Assert.Equal(TestString(1026, "data1"), firedEvents["ScheduleTransactionContextWorkItem"]["data1"]);
            Assert.Equal(TestString(1026, "data2"), firedEvents["ScheduleTransactionContextWorkItem"]["data2"]);
            Assert.Equal(TestString(1026, "data3"), firedEvents["ScheduleTransactionContextWorkItem"]["data3"]);
            Assert.Equal(TestString(1026, "AppDomain"), firedEvents["ScheduleTransactionContextWorkItem"]["AppDomain"]);

            // 1027 StartTransactionContextWorkItem (FourStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("StartTransactionContextWorkItem"), "Event 1027 StartTransactionContextWorkItem did not fire");
            Assert.Equal(TestString(1027, "data1"), firedEvents["StartTransactionContextWorkItem"]["data1"]);
            Assert.Equal(TestString(1027, "data2"), firedEvents["StartTransactionContextWorkItem"]["data2"]);
            Assert.Equal(TestString(1027, "data3"), firedEvents["StartTransactionContextWorkItem"]["data3"]);
            Assert.Equal(TestString(1027, "AppDomain"), firedEvents["StartTransactionContextWorkItem"]["AppDomain"]);

            // 1028 CompleteTransactionContextWorkItem (FourStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("CompleteTransactionContextWorkItem"), "Event 1028 CompleteTransactionContextWorkItem did not fire");
            Assert.Equal(TestString(1028, "data1"), firedEvents["CompleteTransactionContextWorkItem"]["data1"]);
            Assert.Equal(TestString(1028, "data2"), firedEvents["CompleteTransactionContextWorkItem"]["data2"]);
            Assert.Equal(TestString(1028, "data3"), firedEvents["CompleteTransactionContextWorkItem"]["data3"]);
            Assert.Equal(TestString(1028, "AppDomain"), firedEvents["CompleteTransactionContextWorkItem"]["AppDomain"]);

            // 1029 ScheduleFaultWorkItem (EightStringsTemplateEA)
            Assert.True(firedEvents.ContainsKey("ScheduleFaultWorkItem"), "Event 1029 ScheduleFaultWorkItem did not fire");
            Assert.Equal(TestString(1029, "data1"), firedEvents["ScheduleFaultWorkItem"]["data1"]);
            Assert.Equal(TestString(1029, "data2"), firedEvents["ScheduleFaultWorkItem"]["data2"]);
            Assert.Equal(TestString(1029, "data3"), firedEvents["ScheduleFaultWorkItem"]["data3"]);
            Assert.Equal(TestString(1029, "data4"), firedEvents["ScheduleFaultWorkItem"]["data4"]);
            Assert.Equal(TestString(1029, "data5"), firedEvents["ScheduleFaultWorkItem"]["data5"]);
            Assert.Equal(TestString(1029, "data6"), firedEvents["ScheduleFaultWorkItem"]["data6"]);
            Assert.Equal(TestString(1029, "SerializedException"), firedEvents["ScheduleFaultWorkItem"]["SerializedException"]);
            Assert.Equal(TestString(1029, "AppDomain"), firedEvents["ScheduleFaultWorkItem"]["AppDomain"]);

            // 1030 StartFaultWorkItem (EightStringsTemplateEA)
            Assert.True(firedEvents.ContainsKey("StartFaultWorkItem"), "Event 1030 StartFaultWorkItem did not fire");
            Assert.Equal(TestString(1030, "data1"), firedEvents["StartFaultWorkItem"]["data1"]);
            Assert.Equal(TestString(1030, "data2"), firedEvents["StartFaultWorkItem"]["data2"]);
            Assert.Equal(TestString(1030, "data3"), firedEvents["StartFaultWorkItem"]["data3"]);
            Assert.Equal(TestString(1030, "data4"), firedEvents["StartFaultWorkItem"]["data4"]);
            Assert.Equal(TestString(1030, "data5"), firedEvents["StartFaultWorkItem"]["data5"]);
            Assert.Equal(TestString(1030, "data6"), firedEvents["StartFaultWorkItem"]["data6"]);
            Assert.Equal(TestString(1030, "SerializedException"), firedEvents["StartFaultWorkItem"]["SerializedException"]);
            Assert.Equal(TestString(1030, "AppDomain"), firedEvents["StartFaultWorkItem"]["AppDomain"]);

            // 1031 CompleteFaultWorkItem (EightStringsTemplateEA)
            Assert.True(firedEvents.ContainsKey("CompleteFaultWorkItem"), "Event 1031 CompleteFaultWorkItem did not fire");
            Assert.Equal(TestString(1031, "data1"), firedEvents["CompleteFaultWorkItem"]["data1"]);
            Assert.Equal(TestString(1031, "data2"), firedEvents["CompleteFaultWorkItem"]["data2"]);
            Assert.Equal(TestString(1031, "data3"), firedEvents["CompleteFaultWorkItem"]["data3"]);
            Assert.Equal(TestString(1031, "data4"), firedEvents["CompleteFaultWorkItem"]["data4"]);
            Assert.Equal(TestString(1031, "data5"), firedEvents["CompleteFaultWorkItem"]["data5"]);
            Assert.Equal(TestString(1031, "data6"), firedEvents["CompleteFaultWorkItem"]["data6"]);
            Assert.Equal(TestString(1031, "SerializedException"), firedEvents["CompleteFaultWorkItem"]["SerializedException"]);
            Assert.Equal(TestString(1031, "AppDomain"), firedEvents["CompleteFaultWorkItem"]["AppDomain"]);

            // 1032 ScheduleRuntimeWorkItem (FourStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("ScheduleRuntimeWorkItem"), "Event 1032 ScheduleRuntimeWorkItem did not fire");
            Assert.Equal(TestString(1032, "data1"), firedEvents["ScheduleRuntimeWorkItem"]["data1"]);
            Assert.Equal(TestString(1032, "data2"), firedEvents["ScheduleRuntimeWorkItem"]["data2"]);
            Assert.Equal(TestString(1032, "data3"), firedEvents["ScheduleRuntimeWorkItem"]["data3"]);
            Assert.Equal(TestString(1032, "AppDomain"), firedEvents["ScheduleRuntimeWorkItem"]["AppDomain"]);

            // 1033 StartRuntimeWorkItem (FourStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("StartRuntimeWorkItem"), "Event 1033 StartRuntimeWorkItem did not fire");
            Assert.Equal(TestString(1033, "data1"), firedEvents["StartRuntimeWorkItem"]["data1"]);
            Assert.Equal(TestString(1033, "data2"), firedEvents["StartRuntimeWorkItem"]["data2"]);
            Assert.Equal(TestString(1033, "data3"), firedEvents["StartRuntimeWorkItem"]["data3"]);
            Assert.Equal(TestString(1033, "AppDomain"), firedEvents["StartRuntimeWorkItem"]["AppDomain"]);

            // 1034 CompleteRuntimeWorkItem (FourStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("CompleteRuntimeWorkItem"), "Event 1034 CompleteRuntimeWorkItem did not fire");
            Assert.Equal(TestString(1034, "data1"), firedEvents["CompleteRuntimeWorkItem"]["data1"]);
            Assert.Equal(TestString(1034, "data2"), firedEvents["CompleteRuntimeWorkItem"]["data2"]);
            Assert.Equal(TestString(1034, "data3"), firedEvents["CompleteRuntimeWorkItem"]["data3"]);
            Assert.Equal(TestString(1034, "AppDomain"), firedEvents["CompleteRuntimeWorkItem"]["AppDomain"]);

            // 1035 RuntimeTransactionSet (SevenStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("RuntimeTransactionSet"), "Event 1035 RuntimeTransactionSet did not fire");
            Assert.Equal(TestString(1035, "data1"), firedEvents["RuntimeTransactionSet"]["data1"]);
            Assert.Equal(TestString(1035, "data2"), firedEvents["RuntimeTransactionSet"]["data2"]);
            Assert.Equal(TestString(1035, "data3"), firedEvents["RuntimeTransactionSet"]["data3"]);
            Assert.Equal(TestString(1035, "data4"), firedEvents["RuntimeTransactionSet"]["data4"]);
            Assert.Equal(TestString(1035, "data5"), firedEvents["RuntimeTransactionSet"]["data5"]);
            Assert.Equal(TestString(1035, "data6"), firedEvents["RuntimeTransactionSet"]["data6"]);
            Assert.Equal(TestString(1035, "AppDomain"), firedEvents["RuntimeTransactionSet"]["AppDomain"]);

            // 1036 RuntimeTransactionCompletionRequested (FourStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("RuntimeTransactionCompletionRequested"), "Event 1036 RuntimeTransactionCompletionRequested did not fire");
            Assert.Equal(TestString(1036, "data1"), firedEvents["RuntimeTransactionCompletionRequested"]["data1"]);
            Assert.Equal(TestString(1036, "data2"), firedEvents["RuntimeTransactionCompletionRequested"]["data2"]);
            Assert.Equal(TestString(1036, "data3"), firedEvents["RuntimeTransactionCompletionRequested"]["data3"]);
            Assert.Equal(TestString(1036, "AppDomain"), firedEvents["RuntimeTransactionCompletionRequested"]["AppDomain"]);

            // 1037 RuntimeTransactionComplete (TwoStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("RuntimeTransactionComplete"), "Event 1037 RuntimeTransactionComplete did not fire");
            Assert.Equal(TestString(1037, "data1"), firedEvents["RuntimeTransactionComplete"]["data1"]);
            Assert.Equal(TestString(1037, "AppDomain"), firedEvents["RuntimeTransactionComplete"]["AppDomain"]);

            // 1038 EnterNoPersistBlock (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("EnterNoPersistBlock"), "Event 1038 EnterNoPersistBlock did not fire");
            Assert.Equal(TestString(1038, "AppDomain"), firedEvents["EnterNoPersistBlock"]["AppDomain"]);

            // 1039 ExitNoPersistBlock (OneStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("ExitNoPersistBlock"), "Event 1039 ExitNoPersistBlock did not fire");
            Assert.Equal(TestString(1039, "AppDomain"), firedEvents["ExitNoPersistBlock"]["AppDomain"]);

            // 1040 InArgumentBound (SixStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("InArgumentBound"), "Event 1040 InArgumentBound did not fire");
            Assert.Equal(TestString(1040, "data1"), firedEvents["InArgumentBound"]["data1"]);
            Assert.Equal(TestString(1040, "data2"), firedEvents["InArgumentBound"]["data2"]);
            Assert.Equal(TestString(1040, "data3"), firedEvents["InArgumentBound"]["data3"]);
            Assert.Equal(TestString(1040, "data4"), firedEvents["InArgumentBound"]["data4"]);
            Assert.Equal(TestString(1040, "data5"), firedEvents["InArgumentBound"]["data5"]);
            Assert.Equal(TestString(1040, "AppDomain"), firedEvents["InArgumentBound"]["AppDomain"]);

            // 1041 WorkflowApplicationPersistableIdle (ThreeStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("WorkflowApplicationPersistableIdle"), "Event 1041 WorkflowApplicationPersistableIdle did not fire");
            Assert.Equal(TestString(1041, "data1"), firedEvents["WorkflowApplicationPersistableIdle"]["data1"]);
            Assert.Equal(TestString(1041, "data2"), firedEvents["WorkflowApplicationPersistableIdle"]["data2"]);
            Assert.Equal(TestString(1041, "AppDomain"), firedEvents["WorkflowApplicationPersistableIdle"]["AppDomain"]);

            // 1101 WorkflowActivityStart (Multidata17TemplateA)
            Assert.True(firedEvents.ContainsKey("WorkflowActivityStart"), "Event 1101 WorkflowActivityStart did not fire");
            Assert.Equal(TestGuid(1101, 0), firedEvents["WorkflowActivityStart"]["Id"]);
            Assert.Equal(TestString(1101, "AppDomain"), firedEvents["WorkflowActivityStart"]["AppDomain"]);

            // 1102 WorkflowActivityStop (Multidata17TemplateA)
            Assert.True(firedEvents.ContainsKey("WorkflowActivityStop"), "Event 1102 WorkflowActivityStop did not fire");
            Assert.Equal(TestGuid(1102, 0), firedEvents["WorkflowActivityStop"]["Id"]);
            Assert.Equal(TestString(1102, "AppDomain"), firedEvents["WorkflowActivityStop"]["AppDomain"]);

            // 1103 WorkflowActivitySuspend (Multidata17TemplateA)
            Assert.True(firedEvents.ContainsKey("WorkflowActivitySuspend"), "Event 1103 WorkflowActivitySuspend did not fire");
            Assert.Equal(TestGuid(1103, 0), firedEvents["WorkflowActivitySuspend"]["Id"]);
            Assert.Equal(TestString(1103, "AppDomain"), firedEvents["WorkflowActivitySuspend"]["AppDomain"]);

            // 1104 WorkflowActivityResume (Multidata17TemplateA)
            Assert.True(firedEvents.ContainsKey("WorkflowActivityResume"), "Event 1104 WorkflowActivityResume did not fire");
            Assert.Equal(TestGuid(1104, 0), firedEvents["WorkflowActivityResume"]["Id"]);
            Assert.Equal(TestString(1104, "AppDomain"), firedEvents["WorkflowActivityResume"]["AppDomain"]);

            // 1124 InvokeMethodIsStatic (TwoStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("InvokeMethodIsStatic"), "Event 1124 InvokeMethodIsStatic did not fire");
            Assert.Equal(TestString(1124, "data1"), firedEvents["InvokeMethodIsStatic"]["data1"]);
            Assert.Equal(TestString(1124, "AppDomain"), firedEvents["InvokeMethodIsStatic"]["AppDomain"]);

            // 1125 InvokeMethodIsNotStatic (TwoStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("InvokeMethodIsNotStatic"), "Event 1125 InvokeMethodIsNotStatic did not fire");
            Assert.Equal(TestString(1125, "data1"), firedEvents["InvokeMethodIsNotStatic"]["data1"]);
            Assert.Equal(TestString(1125, "AppDomain"), firedEvents["InvokeMethodIsNotStatic"]["AppDomain"]);

            // 1126 InvokedMethodThrewException (ThreeStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("InvokedMethodThrewException"), "Event 1126 InvokedMethodThrewException did not fire");
            Assert.Equal(TestString(1126, "data1"), firedEvents["InvokedMethodThrewException"]["data1"]);
            Assert.Equal(TestString(1126, "data2"), firedEvents["InvokedMethodThrewException"]["data2"]);
            Assert.Equal(TestString(1126, "AppDomain"), firedEvents["InvokedMethodThrewException"]["AppDomain"]);

            // 1131 InvokeMethodUseAsyncPattern (FourStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("InvokeMethodUseAsyncPattern"), "Event 1131 InvokeMethodUseAsyncPattern did not fire");
            Assert.Equal(TestString(1131, "data1"), firedEvents["InvokeMethodUseAsyncPattern"]["data1"]);
            Assert.Equal(TestString(1131, "data2"), firedEvents["InvokeMethodUseAsyncPattern"]["data2"]);
            Assert.Equal(TestString(1131, "data3"), firedEvents["InvokeMethodUseAsyncPattern"]["data3"]);
            Assert.Equal(TestString(1131, "AppDomain"), firedEvents["InvokeMethodUseAsyncPattern"]["AppDomain"]);

            // 1132 InvokeMethodDoesNotUseAsyncPattern (TwoStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("InvokeMethodDoesNotUseAsyncPattern"), "Event 1132 InvokeMethodDoesNotUseAsyncPattern did not fire");
            Assert.Equal(TestString(1132, "data1"), firedEvents["InvokeMethodDoesNotUseAsyncPattern"]["data1"]);
            Assert.Equal(TestString(1132, "AppDomain"), firedEvents["InvokeMethodDoesNotUseAsyncPattern"]["AppDomain"]);

            // 1140 FlowchartStart (TwoStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("FlowchartStart"), "Event 1140 FlowchartStart did not fire");
            Assert.Equal(TestString(1140, "data1"), firedEvents["FlowchartStart"]["data1"]);
            Assert.Equal(TestString(1140, "AppDomain"), firedEvents["FlowchartStart"]["AppDomain"]);

            // 1141 FlowchartEmpty (TwoStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("FlowchartEmpty"), "Event 1141 FlowchartEmpty did not fire");
            Assert.Equal(TestString(1141, "data1"), firedEvents["FlowchartEmpty"]["data1"]);
            Assert.Equal(TestString(1141, "AppDomain"), firedEvents["FlowchartEmpty"]["AppDomain"]);

            // 1143 FlowchartNextNull (TwoStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("FlowchartNextNull"), "Event 1143 FlowchartNextNull did not fire");
            Assert.Equal(TestString(1143, "data1"), firedEvents["FlowchartNextNull"]["data1"]);
            Assert.Equal(TestString(1143, "AppDomain"), firedEvents["FlowchartNextNull"]["AppDomain"]);

            // 1146 FlowchartSwitchCase (ThreeStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("FlowchartSwitchCase"), "Event 1146 FlowchartSwitchCase did not fire");
            Assert.Equal(TestString(1146, "data1"), firedEvents["FlowchartSwitchCase"]["data1"]);
            Assert.Equal(TestString(1146, "data2"), firedEvents["FlowchartSwitchCase"]["data2"]);
            Assert.Equal(TestString(1146, "AppDomain"), firedEvents["FlowchartSwitchCase"]["AppDomain"]);

            // 1147 FlowchartSwitchDefault (TwoStringsTemplateA)
            Assert.True(firedEvents.ContainsKey("FlowchartSwitchDefault"), "Event 1147 FlowchartSwitchDefault did not fire");
            Assert.Equal(TestString(1147, "data1"), firedEvents["FlowchartSwitchDefault"]["data1"]);
            Assert.Equal(TestString(1147, "AppDomain"), firedEvents["FlowchartSwitchDefault"]["AppDomain"]);
        }

        #endregion
    }
}
