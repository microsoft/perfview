// Copyright (c) Microsoft Corporation.  All rights reserved
using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.Diagnostics.Eventing;
using FastSerialization;

namespace System.Diagnostics.Eventing
{

    public sealed class KernelTraceEventParser : TraceEventParser 
    {
        public static string ProviderName = "Windows Kernel";
        public static Guid ProviderGuid = new Guid(0x9e814aad, 0x3204, 0x11d2, 0x9a, 0x82, 0x00, 0x60, 0x08, 0xa8, 0x69, 0x39);
        public KernelTraceEventParser(TraceEventSource source) : base(source) {}

        public event Action<EventTraceHeaderTraceData> EventTraceHeader
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EventTraceHeaderTraceData(value, 0xFFFF, 0, "EventTrace", EventTraceTaskGuid, 0, "Header", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<HeaderExtensionTraceData> EventTraceExtension
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new HeaderExtensionTraceData(value, 0xFFFF, 0, "EventTrace", EventTraceTaskGuid, 5, "Extension", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<HeaderExtensionTraceData> EventTraceEndExtension
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new HeaderExtensionTraceData(value, 0xFFFF, 0, "EventTrace", EventTraceTaskGuid, 32, "EndExtension", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<EmptyTraceData> EventTraceRundownComplete
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, 0xFFFF, 0, "EventTrace", EventTraceTaskGuid, 8, "RundownComplete", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ProcessTraceData> ProcessStart
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ProcessTraceData(value, 0xFFFF, 1, "Process", ProcessTaskGuid, 1, "Start", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ProcessTraceData> ProcessEnd
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ProcessTraceData(value, 0xFFFF, 1, "Process", ProcessTaskGuid, 2, "End", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ProcessTraceData> ProcessDCStart
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ProcessTraceData(value, 0xFFFF, 1, "Process", ProcessTaskGuid, 3, "DCStart", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ProcessTraceData> ProcessDCEnd
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ProcessTraceData(value, 0xFFFF, 1, "Process", ProcessTaskGuid, 4, "DCEnd", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ProcessTraceData> ProcessDefunct
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ProcessTraceData(value, 0xFFFF, 1, "Process", ProcessTaskGuid, 39, "Defunct", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ProcessCtrTraceData> ProcessPerfCtr
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ProcessCtrTraceData(value, 0xFFFF, 1, "Process", ProcessTaskGuid, 32, "PerfCtr", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ProcessCtrTraceData> ProcessPerfCtrRundown
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ProcessCtrTraceData(value, 0xFFFF, 1, "Process", ProcessTaskGuid, 33, "PerfCtrRundown", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ThreadTraceData> ThreadStart
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ThreadTraceData(value, 0xFFFF, 2, "Thread", ThreadTaskGuid, 1, "Start", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ThreadTraceData> ThreadEnd
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ThreadTraceData(value, 0xFFFF, 2, "Thread", ThreadTaskGuid, 2, "End", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ThreadTraceData> ThreadDCStart
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ThreadTraceData(value, 0xFFFF, 2, "Thread", ThreadTaskGuid, 3, "DCStart", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ThreadTraceData> ThreadDCEnd
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ThreadTraceData(value, 0xFFFF, 2, "Thread", ThreadTaskGuid, 4, "DCEnd", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<CSwitchTraceData> ThreadCSwitch
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new CSwitchTraceData(value, 0xFFFF, 2, "Thread", ThreadTaskGuid, 36, "CSwitch", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<EmptyTraceData> ThreadCompCS
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, 0xFFFF, 2, "Thread", ThreadTaskGuid, 37, "CompCS", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<WorkerThreadTraceData> ThreadWorkerThread
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new WorkerThreadTraceData(value, 0xFFFF, 2, "Thread", ThreadTaskGuid, 57, "WorkerThread", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ReserveCreateTraceData> ThreadReserveCreate
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ReserveCreateTraceData(value, 0xFFFF, 2, "Thread", ThreadTaskGuid, 48, "ReserveCreate", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ReserveDeleteTraceData> ThreadReserveDelete
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ReserveDeleteTraceData(value, 0xFFFF, 2, "Thread", ThreadTaskGuid, 49, "ReserveDelete", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ReserveJoinThreadTraceData> ThreadReserveJoinThread
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ReserveJoinThreadTraceData(value, 0xFFFF, 2, "Thread", ThreadTaskGuid, 52, "ReserveJoinThread", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ReserveDisjoinThreadTraceData> ThreadReserveDisjoinThread
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ReserveDisjoinThreadTraceData(value, 0xFFFF, 2, "Thread", ThreadTaskGuid, 53, "ReserveDisjoinThread", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ReserveStateTraceData> ThreadReserveState
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ReserveStateTraceData(value, 0xFFFF, 2, "Thread", ThreadTaskGuid, 54, "ReserveState", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ReserveBandwidthTraceData> ThreadReserveBandwidth
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ReserveBandwidthTraceData(value, 0xFFFF, 2, "Thread", ThreadTaskGuid, 55, "ReserveBandwidth", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ReserveLateCountTraceData> ThreadReserveLateCount
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ReserveLateCountTraceData(value, 0xFFFF, 2, "Thread", ThreadTaskGuid, 56, "ReserveLateCount", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<DiskIoTraceData> DiskIoRead
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new DiskIoTraceData(value, 0xFFFF, 3, "DiskIo", DiskIoTaskGuid, 10, "Read", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<DiskIoTraceData> DiskIoWrite
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new DiskIoTraceData(value, 0xFFFF, 3, "DiskIo", DiskIoTaskGuid, 11, "Write", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<DiskIoInitTraceData> DiskIoReadInit
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new DiskIoInitTraceData(value, 0xFFFF, 3, "DiskIo", DiskIoTaskGuid, 12, "ReadInit", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<DiskIoInitTraceData> DiskIoWriteInit
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new DiskIoInitTraceData(value, 0xFFFF, 3, "DiskIo", DiskIoTaskGuid, 13, "WriteInit", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<DiskIoInitTraceData> DiskIoFlushInit
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new DiskIoInitTraceData(value, 0xFFFF, 3, "DiskIo", DiskIoTaskGuid, 15, "FlushInit", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<DiskIoFlushBuffersTraceData> DiskIoFlushBuffers
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new DiskIoFlushBuffersTraceData(value, 0xFFFF, 3, "DiskIo", DiskIoTaskGuid, 14, "FlushBuffers", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<DriverMajorFunctionCallTraceData> DiskIoDriverMajorFunctionCall
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new DriverMajorFunctionCallTraceData(value, 0xFFFF, 3, "DiskIo", DiskIoTaskGuid, 34, "DriverMajorFunctionCall", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<DriverMajorFunctionReturnTraceData> DiskIoDriverMajorFunctionReturn
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new DriverMajorFunctionReturnTraceData(value, 0xFFFF, 3, "DiskIo", DiskIoTaskGuid, 35, "DriverMajorFunctionReturn", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<DriverCompletionRoutineTraceData> DiskIoDriverCompletionRoutine
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new DriverCompletionRoutineTraceData(value, 0xFFFF, 3, "DiskIo", DiskIoTaskGuid, 37, "DriverCompletionRoutine", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<DriverCompleteRequestTraceData> DiskIoDriverCompleteRequest
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new DriverCompleteRequestTraceData(value, 0xFFFF, 3, "DiskIo", DiskIoTaskGuid, 52, "DriverCompleteRequest", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<DriverCompleteRequestReturnTraceData> DiskIoDriverCompleteRequestReturn
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new DriverCompleteRequestReturnTraceData(value, 0xFFFF, 3, "DiskIo", DiskIoTaskGuid, 53, "DriverCompleteRequestReturn", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<RegistryTraceData> RegistryCreate
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new RegistryTraceData(value, 0xFFFF, 4, "Registry", RegistryTaskGuid, 10, "Create", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<RegistryTraceData> RegistryOpen
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new RegistryTraceData(value, 0xFFFF, 4, "Registry", RegistryTaskGuid, 11, "Open", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<RegistryTraceData> RegistryDelete
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new RegistryTraceData(value, 0xFFFF, 4, "Registry", RegistryTaskGuid, 12, "Delete", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<RegistryTraceData> RegistryQuery
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new RegistryTraceData(value, 0xFFFF, 4, "Registry", RegistryTaskGuid, 13, "Query", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<RegistryTraceData> RegistrySetValue
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new RegistryTraceData(value, 0xFFFF, 4, "Registry", RegistryTaskGuid, 14, "SetValue", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<RegistryTraceData> RegistryDeleteValue
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new RegistryTraceData(value, 0xFFFF, 4, "Registry", RegistryTaskGuid, 15, "DeleteValue", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<RegistryTraceData> RegistryQueryValue
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new RegistryTraceData(value, 0xFFFF, 4, "Registry", RegistryTaskGuid, 16, "QueryValue", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<RegistryTraceData> RegistryEnumerateKey
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new RegistryTraceData(value, 0xFFFF, 4, "Registry", RegistryTaskGuid, 17, "EnumerateKey", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<RegistryTraceData> RegistryEnumerateValueKey
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new RegistryTraceData(value, 0xFFFF, 4, "Registry", RegistryTaskGuid, 18, "EnumerateValueKey", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<RegistryTraceData> RegistryQueryMultipleValue
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new RegistryTraceData(value, 0xFFFF, 4, "Registry", RegistryTaskGuid, 19, "QueryMultipleValue", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<RegistryTraceData> RegistrySetInformation
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new RegistryTraceData(value, 0xFFFF, 4, "Registry", RegistryTaskGuid, 20, "SetInformation", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<RegistryTraceData> RegistryFlush
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new RegistryTraceData(value, 0xFFFF, 4, "Registry", RegistryTaskGuid, 21, "Flush", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<RegistryTraceData> RegistryRunDown
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new RegistryTraceData(value, 0xFFFF, 4, "Registry", RegistryTaskGuid, 22, "RunDown", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<RegistryTraceData> RegistryKCBCreate
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new RegistryTraceData(value, 0xFFFF, 4, "Registry", RegistryTaskGuid, 22, "KCBCreate", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<RegistryTraceData> RegistryKCBDelete
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new RegistryTraceData(value, 0xFFFF, 4, "Registry", RegistryTaskGuid, 23, "KCBDelete", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<RegistryTraceData> RegistryKCBRundownBegin
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new RegistryTraceData(value, 0xFFFF, 4, "Registry", RegistryTaskGuid, 24, "KCBRundownBegin", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<RegistryTraceData> RegistryKCBRundownEnd
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new RegistryTraceData(value, 0xFFFF, 4, "Registry", RegistryTaskGuid, 25, "KCBRundownEnd", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<RegistryTraceData> RegistryVirtualize
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new RegistryTraceData(value, 0xFFFF, 4, "Registry", RegistryTaskGuid, 26, "Virtualize", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<RegistryTraceData> RegistryClose
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new RegistryTraceData(value, 0xFFFF, 4, "Registry", RegistryTaskGuid, 27, "Close", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<SplitIoInfoTraceData> SplitIoVolMgr
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new SplitIoInfoTraceData(value, 0xFFFF, 5, "SplitIo", SplitIoTaskGuid, 32, "VolMgr", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<FileIoNameTraceData> FileIoName
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new FileIoNameTraceData(value, 0xFFFF, 6, "FileIo", FileIoTaskGuid, 0, "Name", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<FileIoNameTraceData> FileIoFileCreate
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new FileIoNameTraceData(value, 0xFFFF, 6, "FileIo", FileIoTaskGuid, 32, "FileCreate", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<FileIoNameTraceData> FileIoFileDelete
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new FileIoNameTraceData(value, 0xFFFF, 6, "FileIo", FileIoTaskGuid, 35, "FileDelete", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<FileIoNameTraceData> FileIoFileRundown
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new FileIoNameTraceData(value, 0xFFFF, 6, "FileIo", FileIoTaskGuid, 36, "FileRundown", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<FileIoCreateTraceData> FileIoCreate
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new FileIoCreateTraceData(value, 0xFFFF, 6, "FileIo", FileIoTaskGuid, 64, "Create", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<FileIoSimpleOpTraceData> FileIoCleanup
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new FileIoSimpleOpTraceData(value, 0xFFFF, 6, "FileIo", FileIoTaskGuid, 65, "Cleanup", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<FileIoSimpleOpTraceData> FileIoClose
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new FileIoSimpleOpTraceData(value, 0xFFFF, 6, "FileIo", FileIoTaskGuid, 66, "Close", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<FileIoSimpleOpTraceData> FileIoFlush
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new FileIoSimpleOpTraceData(value, 0xFFFF, 6, "FileIo", FileIoTaskGuid, 73, "Flush", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<FileIoReadWriteTraceData> FileIoRead
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new FileIoReadWriteTraceData(value, 0xFFFF, 6, "FileIo", FileIoTaskGuid, 67, "Read", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<FileIoReadWriteTraceData> FileIoWrite
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new FileIoReadWriteTraceData(value, 0xFFFF, 6, "FileIo", FileIoTaskGuid, 68, "Write", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<FileIoInfoTraceData> FileIoSetInfo
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new FileIoInfoTraceData(value, 0xFFFF, 6, "FileIo", FileIoTaskGuid, 69, "SetInfo", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<FileIoInfoTraceData> FileIoDelete
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new FileIoInfoTraceData(value, 0xFFFF, 6, "FileIo", FileIoTaskGuid, 70, "Delete", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<FileIoInfoTraceData> FileIoRename
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new FileIoInfoTraceData(value, 0xFFFF, 6, "FileIo", FileIoTaskGuid, 71, "Rename", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<FileIoInfoTraceData> FileIoQueryInfo
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new FileIoInfoTraceData(value, 0xFFFF, 6, "FileIo", FileIoTaskGuid, 74, "QueryInfo", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<FileIoInfoTraceData> FileIoFSControl
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new FileIoInfoTraceData(value, 0xFFFF, 6, "FileIo", FileIoTaskGuid, 75, "FSControl", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<FileIoDirEnumTraceData> FileIoDirEnum
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new FileIoDirEnumTraceData(value, 0xFFFF, 6, "FileIo", FileIoTaskGuid, 72, "DirEnum", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<FileIoDirEnumTraceData> FileIoDirNotify
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new FileIoDirEnumTraceData(value, 0xFFFF, 6, "FileIo", FileIoTaskGuid, 77, "DirNotify", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<FileIoOpEndTraceData> FileIoOperationEnd
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new FileIoOpEndTraceData(value, 0xFFFF, 6, "FileIo", FileIoTaskGuid, 76, "OperationEnd", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TcpIpTraceData> TcpIpSend
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TcpIpTraceData(value, 0xFFFF, 7, "TcpIp", TcpIpTaskGuid, 10, "Send", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TcpIpTraceData> TcpIpRecv
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TcpIpTraceData(value, 0xFFFF, 7, "TcpIp", TcpIpTaskGuid, 11, "Recv", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TcpIpTraceData> TcpIpConnect
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TcpIpTraceData(value, 0xFFFF, 7, "TcpIp", TcpIpTaskGuid, 12, "Connect", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TcpIpTraceData> TcpIpDisconnect
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TcpIpTraceData(value, 0xFFFF, 7, "TcpIp", TcpIpTaskGuid, 13, "Disconnect", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TcpIpTraceData> TcpIpRetransmit
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TcpIpTraceData(value, 0xFFFF, 7, "TcpIp", TcpIpTaskGuid, 14, "Retransmit", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TcpIpTraceData> TcpIpAccept
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TcpIpTraceData(value, 0xFFFF, 7, "TcpIp", TcpIpTaskGuid, 15, "Accept", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TcpIpTraceData> TcpIpReconnect
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TcpIpTraceData(value, 0xFFFF, 7, "TcpIp", TcpIpTaskGuid, 16, "Reconnect", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TcpIpFailTraceData> TcpIpFail
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TcpIpFailTraceData(value, 0xFFFF, 7, "TcpIp", TcpIpTaskGuid, 17, "Fail", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TcpIpV6TraceData> TcpIpTCPCopy
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TcpIpV6TraceData(value, 0xFFFF, 7, "TcpIp", TcpIpTaskGuid, 18, "TCPCopy", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TcpIpV6TraceData> TcpIpARPCopy
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TcpIpV6TraceData(value, 0xFFFF, 7, "TcpIp", TcpIpTaskGuid, 19, "ARPCopy", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TcpIpV6TraceData> TcpIpFullACK
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TcpIpV6TraceData(value, 0xFFFF, 7, "TcpIp", TcpIpTaskGuid, 20, "FullACK", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TcpIpV6TraceData> TcpIpPartACK
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TcpIpV6TraceData(value, 0xFFFF, 7, "TcpIp", TcpIpTaskGuid, 21, "PartACK", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TcpIpV6TraceData> TcpIpDupACK
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TcpIpV6TraceData(value, 0xFFFF, 7, "TcpIp", TcpIpTaskGuid, 22, "DupACK", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TcpIpSendIPV4TraceData> TcpIpSendIPV4
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TcpIpSendIPV4TraceData(value, 0xFFFF, 7, "TcpIp", TcpIpTaskGuid, 10, "SendIPV4", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TcpIpTraceData> TcpIpRecvIPV4
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TcpIpTraceData(value, 0xFFFF, 7, "TcpIp", TcpIpTaskGuid, 11, "RecvIPV4", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TcpIpTraceData> TcpIpDisconnectIPV4
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TcpIpTraceData(value, 0xFFFF, 7, "TcpIp", TcpIpTaskGuid, 13, "DisconnectIPV4", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TcpIpTraceData> TcpIpRetransmitIPV4
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TcpIpTraceData(value, 0xFFFF, 7, "TcpIp", TcpIpTaskGuid, 14, "RetransmitIPV4", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TcpIpTraceData> TcpIpReconnectIPV4
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TcpIpTraceData(value, 0xFFFF, 7, "TcpIp", TcpIpTaskGuid, 16, "ReconnectIPV4", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TcpIpTraceData> TcpIpTCPCopyIPV4
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TcpIpTraceData(value, 0xFFFF, 7, "TcpIp", TcpIpTaskGuid, 18, "TCPCopyIPV4", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TcpIpConnectTraceData> TcpIpConnectIPV4
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TcpIpConnectTraceData(value, 0xFFFF, 7, "TcpIp", TcpIpTaskGuid, 12, "ConnectIPV4", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TcpIpConnectTraceData> TcpIpAcceptIPV4
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TcpIpConnectTraceData(value, 0xFFFF, 7, "TcpIp", TcpIpTaskGuid, 15, "AcceptIPV4", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TcpIpSendIPV6TraceData> TcpIpSendIPV6
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TcpIpSendIPV6TraceData(value, 0xFFFF, 7, "TcpIp", TcpIpTaskGuid, 26, "SendIPV6", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TcpIpV6TraceData> TcpIpRecvIPV6
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TcpIpV6TraceData(value, 0xFFFF, 7, "TcpIp", TcpIpTaskGuid, 27, "RecvIPV6", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TcpIpV6TraceData> TcpIpDisconnectIPV6
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TcpIpV6TraceData(value, 0xFFFF, 7, "TcpIp", TcpIpTaskGuid, 29, "DisconnectIPV6", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TcpIpV6TraceData> TcpIpRetransmitIPV6
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TcpIpV6TraceData(value, 0xFFFF, 7, "TcpIp", TcpIpTaskGuid, 30, "RetransmitIPV6", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TcpIpV6TraceData> TcpIpReconnectIPV6
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TcpIpV6TraceData(value, 0xFFFF, 7, "TcpIp", TcpIpTaskGuid, 32, "ReconnectIPV6", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TcpIpV6TraceData> TcpIpTCPCopyIPV6
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TcpIpV6TraceData(value, 0xFFFF, 7, "TcpIp", TcpIpTaskGuid, 34, "TCPCopyIPV6", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TcpIpV6ConnectTraceData> TcpIpConnectIPV6
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TcpIpV6ConnectTraceData(value, 0xFFFF, 7, "TcpIp", TcpIpTaskGuid, 28, "ConnectIPV6", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TcpIpV6ConnectTraceData> TcpIpAcceptIPV6
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TcpIpV6ConnectTraceData(value, 0xFFFF, 7, "TcpIp", TcpIpTaskGuid, 31, "AcceptIPV6", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<UdpIpTraceData> UdpIpSend
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new UdpIpTraceData(value, 0xFFFF, 8, "UdpIp", UdpIpTaskGuid, 10, "Send", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<UdpIpTraceData> UdpIpRecv
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new UdpIpTraceData(value, 0xFFFF, 8, "UdpIp", UdpIpTaskGuid, 11, "Recv", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<UdpIpTraceData> UdpIpSendIPV4
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new UdpIpTraceData(value, 0xFFFF, 8, "UdpIp", UdpIpTaskGuid, 10, "SendIPV4", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<UdpIpTraceData> UdpIpRecvIPV4
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new UdpIpTraceData(value, 0xFFFF, 8, "UdpIp", UdpIpTaskGuid, 11, "RecvIPV4", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<UdpIpFailTraceData> UdpIpFail
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new UdpIpFailTraceData(value, 0xFFFF, 8, "UdpIp", UdpIpTaskGuid, 17, "Fail", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<UpdIpV6TraceData> UdpIpSendIPV6
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new UpdIpV6TraceData(value, 0xFFFF, 8, "UdpIp", UdpIpTaskGuid, 26, "SendIPV6", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<UpdIpV6TraceData> UdpIpRecvIPV6
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new UpdIpV6TraceData(value, 0xFFFF, 8, "UdpIp", UdpIpTaskGuid, 27, "RecvIPV6", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ImageLoadTraceData> ImageLoad
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ImageLoadTraceData(value, 0xFFFF, 9, "Image", ImageTaskGuid, 10, "Load", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ImageLoadTraceData> ImageUnload
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ImageLoadTraceData(value, 0xFFFF, 9, "Image", ImageTaskGuid, 2, "Unload", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ImageLoadTraceData> ImageDCStart
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ImageLoadTraceData(value, 0xFFFF, 9, "Image", ImageTaskGuid, 3, "DCStart", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ImageLoadTraceData> ImageDCEnd
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ImageLoadTraceData(value, 0xFFFF, 9, "Image", ImageTaskGuid, 4, "DCEnd", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<PageFaultTraceData> PageFaultTransitionFault
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new PageFaultTraceData(value, 0xFFFF, 10, "PageFault", PageFaultTaskGuid, 10, "TransitionFault", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<PageFaultTraceData> PageFaultDemandZeroFault
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new PageFaultTraceData(value, 0xFFFF, 10, "PageFault", PageFaultTaskGuid, 11, "DemandZeroFault", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<PageFaultTraceData> PageFaultCopyOnWrite
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new PageFaultTraceData(value, 0xFFFF, 10, "PageFault", PageFaultTaskGuid, 12, "CopyOnWrite", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<PageFaultTraceData> PageFaultGuardPageFault
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new PageFaultTraceData(value, 0xFFFF, 10, "PageFault", PageFaultTaskGuid, 13, "GuardPageFault", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<PageFaultTraceData> PageFaultHardPageFault
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new PageFaultTraceData(value, 0xFFFF, 10, "PageFault", PageFaultTaskGuid, 14, "HardPageFault", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<PageFaultTraceData> PageFaultAccessViolation
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new PageFaultTraceData(value, 0xFFFF, 10, "PageFault", PageFaultTaskGuid, 15, "AccessViolation", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<PageFaultHardFaultTraceData> PageFaultHardFault
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new PageFaultHardFaultTraceData(value, 0xFFFF, 10, "PageFault", PageFaultTaskGuid, 32, "HardFault", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<PageFaultHeapRangeRundownTraceData> PageFaultHeapRangeRundown
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new PageFaultHeapRangeRundownTraceData(value, 0xFFFF, 10, "PageFault", PageFaultTaskGuid, 100, "HRRundown", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<PageFaultHeapRangeCreateTraceData> PageFaultHeapRangeCreate
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new PageFaultHeapRangeCreateTraceData(value, 0xFFFF, 10, "PageFault", PageFaultTaskGuid, 101, "HRCreate", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<PageFaultHeapRangeTraceData> PageFaultHeapRangeReserve
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new PageFaultHeapRangeTraceData(value, 0xFFFF, 10, "PageFault", PageFaultTaskGuid, 102, "HRReserve", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<PageFaultHeapRangeTraceData> PageFaultHeapRangeRelease
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new PageFaultHeapRangeTraceData(value, 0xFFFF, 10, "PageFault", PageFaultTaskGuid, 103, "HRRelease", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<PageFaultHeapRangeDestroyTraceData> PageFaultHeapRangeDestroy
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new PageFaultHeapRangeDestroyTraceData(value, 0xFFFF, 10, "PageFault", PageFaultTaskGuid, 104, "HRDestroy", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<PageFaultImageLoadBackedTraceData> PageFaultImageLoadBacked
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new PageFaultImageLoadBackedTraceData(value, 0xFFFF, 10, "PageFault", PageFaultTaskGuid, 105, "ImageLoadBacked", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<SampledProfileTraceData> PerfInfoSampleProf
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new SampledProfileTraceData(value, 0xFFFF, 11, "PerfInfo", PerfInfoTaskGuid, 46, "SampleProf", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<SampledProfileIntervalTraceData> PerfInfoSetInterval
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new SampledProfileIntervalTraceData(value, 0xFFFF, 11, "PerfInfo", PerfInfoTaskGuid, 72, "SetInterval", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<SampledProfileIntervalTraceData> PerfInfoCollectionStart
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new SampledProfileIntervalTraceData(value, 0xFFFF, 11, "PerfInfo", PerfInfoTaskGuid, 73, "CollectionStart", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<SampledProfileIntervalTraceData> PerfInfoCollectionEnd
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new SampledProfileIntervalTraceData(value, 0xFFFF, 11, "PerfInfo", PerfInfoTaskGuid, 74, "CollectionEnd", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<SysCallEnterTraceData> PerfInfoSysClEnter
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new SysCallEnterTraceData(value, 0xFFFF, 11, "PerfInfo", PerfInfoTaskGuid, 51, "SysClEnter", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<SysCallExitTraceData> PerfInfoSysClExit
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new SysCallExitTraceData(value, 0xFFFF, 11, "PerfInfo", PerfInfoTaskGuid, 52, "SysClExit", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ISRTraceData> PerfInfoISR
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ISRTraceData(value, 0xFFFF, 11, "PerfInfo", PerfInfoTaskGuid, 67, "ISR", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<DPCTraceData> PerfInfoThreadedDPC
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new DPCTraceData(value, 0xFFFF, 11, "PerfInfo", PerfInfoTaskGuid, 66, "ThreadedDPC", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<DPCTraceData> PerfInfoDPC
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new DPCTraceData(value, 0xFFFF, 11, "PerfInfo", PerfInfoTaskGuid, 68, "DPC", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<DPCTraceData> PerfInfoTimerDPC
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new DPCTraceData(value, 0xFFFF, 11, "PerfInfo", PerfInfoTaskGuid, 69, "TimerDPC", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<EmptyTraceData> PerfInfoDebuggerEnabled
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, 0xFFFF, 11, "PerfInfo", PerfInfoTaskGuid, 58, "DebuggerEnabled", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<StackWalkTraceData> StackWalk
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new StackWalkTraceData(value, 0xFFFF, 12, "StackWalk", StackWalkTaskGuid, 32, "Stack", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ALPCSendMessageTraceData> ALPCSendMessage
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ALPCSendMessageTraceData(value, 0xFFFF, 13, "ALPC", ALPCTaskGuid, 33, "ALPCSendMessage", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ALPCReceiveMessageTraceData> ALPCReceiveMessage
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ALPCReceiveMessageTraceData(value, 0xFFFF, 13, "ALPC", ALPCTaskGuid, 34, "ALPCReceiveMessage", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ALPCWaitForReplyTraceData> ALPCWaitForReply
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ALPCWaitForReplyTraceData(value, 0xFFFF, 13, "ALPC", ALPCTaskGuid, 35, "ALPCWaitForReply", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ALPCWaitForNewMessageTraceData> ALPCWaitForNewMessage
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ALPCWaitForNewMessageTraceData(value, 0xFFFF, 13, "ALPC", ALPCTaskGuid, 36, "ALPCWaitForNewMessage", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<ALPCUnwaitTraceData> ALPCUnwait
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new ALPCUnwaitTraceData(value, 0xFFFF, 13, "ALPC", ALPCTaskGuid, 37, "ALPCUnwait", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<EmptyTraceData> LostEvent
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new EmptyTraceData(value, 0xFFFF, 14, "Lost_Event", Lost_EventTaskGuid, 32, "LostEvent", ProviderGuid, ProviderName));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<SystemConfigCPUTraceData> SystemConfigCPU
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new SystemConfigCPUTraceData(value, 0xFFFF, 15, "SystemConfig", SystemConfigTaskGuid, 10, "CPU", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<SystemConfigPhyDiskTraceData> SystemConfigPhyDisk
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new SystemConfigPhyDiskTraceData(value, 0xFFFF, 15, "SystemConfig", SystemConfigTaskGuid, 11, "PhyDisk", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<SystemConfigLogDiskTraceData> SystemConfigLogDisk
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new SystemConfigLogDiskTraceData(value, 0xFFFF, 15, "SystemConfig", SystemConfigTaskGuid, 12, "LogDisk", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<SystemConfigNICTraceData> SystemConfigNIC
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new SystemConfigNICTraceData(value, 0xFFFF, 15, "SystemConfig", SystemConfigTaskGuid, 13, "NIC", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<SystemConfigVideoTraceData> SystemConfigVideo
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new SystemConfigVideoTraceData(value, 0xFFFF, 15, "SystemConfig", SystemConfigTaskGuid, 14, "Video", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<SystemConfigServicesTraceData> SystemConfigServices
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new SystemConfigServicesTraceData(value, 0xFFFF, 15, "SystemConfig", SystemConfigTaskGuid, 15, "Services", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<SystemConfigPowerTraceData> SystemConfigPower
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new SystemConfigPowerTraceData(value, 0xFFFF, 15, "SystemConfig", SystemConfigTaskGuid, 16, "Power", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<SystemConfigIRQTraceData> SystemConfigIRQ
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new SystemConfigIRQTraceData(value, 0xFFFF, 15, "SystemConfig", SystemConfigTaskGuid, 21, "IRQ", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<SystemConfigPnPTraceData> SystemConfigPnP
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new SystemConfigPnPTraceData(value, 0xFFFF, 15, "SystemConfig", SystemConfigTaskGuid, 22, "PnP", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<SystemConfigNetworkTraceData> SystemConfigNetwork
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new SystemConfigNetworkTraceData(value, 0xFFFF, 15, "SystemConfig", SystemConfigTaskGuid, 17, "Network", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<SystemConfigIDEChannelTraceData> SystemConfigIDEChannel
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new SystemConfigIDEChannelTraceData(value, 0xFFFF, 15, "SystemConfig", SystemConfigTaskGuid, 23, "IDEChannel", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }

    #region private
        private KernelTraceEventParserState State
        {
            get
            {
                if (state == null)
                    state = GetPersistedStateFromSource<KernelTraceEventParserState>(this.GetType().Name);
                return state;
            }
        }
        KernelTraceEventParserState state;
        private static Guid EventTraceTaskGuid = new Guid(0x68fdd900, 0x4a3e, 0x11d1, 0x84, 0xf4, 0x00, 0x00, 0xf8, 0x04, 0x64, 0xe3);
        private static Guid ProcessTaskGuid = new Guid(0x3d6fa8d0, 0xfe05, 0x11d0, 0x9d, 0xda, 0x00, 0xc0, 0x4f, 0xd7, 0xba, 0x7c);
        private static Guid ThreadTaskGuid = new Guid(0x3d6fa8d1, 0xfe05, 0x11d0, 0x9d, 0xda, 0x00, 0xc0, 0x4f, 0xd7, 0xba, 0x7c);
        private static Guid DiskIoTaskGuid = new Guid(0x3d6fa8d4, 0xfe05, 0x11d0, 0x9d, 0xda, 0x00, 0xc0, 0x4f, 0xd7, 0xba, 0x7c);
        private static Guid RegistryTaskGuid = new Guid(0xae53722e, 0xc863, 0x11d2, 0x86, 0x59, 0x00, 0xc0, 0x4f, 0xa3, 0x21, 0xa1);
        private static Guid SplitIoTaskGuid = new Guid(0xd837ca92, 0x12b9, 0x44a5, 0xad, 0x6a, 0x3a, 0x65, 0xb3, 0x57, 0x8a, 0xa8);
        private static Guid FileIoTaskGuid = new Guid(0x90cbdc39, 0x4a3e, 0x11d1, 0x84, 0xf4, 0x00, 0x00, 0xf8, 0x04, 0x64, 0xe3);
        private static Guid TcpIpTaskGuid = new Guid(0x9a280ac0, 0xc8e0, 0x11d1, 0x84, 0xe2, 0x00, 0xc0, 0x4f, 0xb9, 0x98, 0xa2);
        private static Guid UdpIpTaskGuid = new Guid(0xbf3a50c5, 0xa9c9, 0x4988, 0xa0, 0x05, 0x2d, 0xf0, 0xb7, 0xc8, 0x0f, 0x80);
        private static Guid ImageTaskGuid = new Guid(0x2cb15d1d, 0x5fc1, 0x11d2, 0xab, 0xe1, 0x00, 0xa0, 0xc9, 0x11, 0xf5, 0x18);
        private static Guid PageFaultTaskGuid = new Guid(0x3d6fa8d3, 0xfe05, 0x11d0, 0x9d, 0xda, 0x00, 0xc0, 0x4f, 0xd7, 0xba, 0x7c);
        private static Guid PerfInfoTaskGuid = new Guid(0xce1dbfb4, 0x137e, 0x4da6, 0x87, 0xb0, 0x3f, 0x59, 0xaa, 0x10, 0x2c, 0xbc);
        private static Guid StackWalkTaskGuid = new Guid(0xdef2fe46, 0x7bd6, 0x4b80, 0xbd, 0x94, 0xf5, 0x7f, 0xe2, 0x0d, 0x0c, 0xe3);
        private static Guid ALPCTaskGuid = new Guid(0x45d8cccd, 0x539f, 0x4b72, 0xa8, 0xb7, 0x5c, 0x68, 0x31, 0x42, 0x60, 0x9a);
        private static Guid Lost_EventTaskGuid = new Guid(0x6a399ae0, 0x4bc6, 0x4de9, 0x87, 0x0b, 0x36, 0x57, 0xf8, 0x94, 0x7e, 0x7e);
        private static Guid SystemConfigTaskGuid = new Guid(0x01853a65, 0x418f, 0x4f36, 0xae, 0xfc, 0xdc, 0x0f, 0x1d, 0x2f, 0xd2, 0x35);
    #endregion
    }
    #region private types
    internal class KernelTraceEventParserState : IFastSerializable
    {
        //TODO: Fill in
        void IFastSerializable.ToStream(Serializer serializer)
        {
        }
        void IFastSerializable.FromStream(Deserializer deserializer)
        {
        }
    }
    #endregion

    public sealed class EventTraceHeaderTraceData : TraceEvent
    {
        public int BufferSize { get { return GetInt32At(0); } }
        public int Version { get { return GetInt32At(4); } }
        public int ProviderVersion { get { return GetInt32At(8); } }
        public int NumberOfProcessors { get { return GetInt32At(12); } }
        public long EndTime { get { return GetInt64At(16); } }
        public int TimerResolution { get { return GetInt32At(24); } }
        public int MaxFileSize { get { return GetInt32At(28); } }
        public int LogFileMode { get { return GetInt32At(32); } }
        public int BuffersWritten { get { return GetInt32At(36); } }
        public int StartBuffers { get { return GetInt32At(40); } }
        public int PointerSize { get { return GetInt32At(44); } }
        public int EventsLost { get { return GetInt32At(48); } }
        public int CPUSpeed { get { return GetInt32At(52); } }
        public Address LoggerName { get { return GetHostPointer(56); } }
        public Address LogFileName { get { return GetHostPointer(HostOffset(60, 1)); } }
        public int TimeZoneInformation { get { return GetByteAt(HostOffset(64, 2)); } }
        public long BootTime { get { return GetInt64At(HostOffset(240, 2)); } }
        public long PerfFreq { get { return GetInt64At(HostOffset(248, 2)); } }
        public long StartTime { get { return GetInt64At(HostOffset(256, 2)); } }
        public int ReservedFlags { get { return GetInt32At(HostOffset(264, 2)); } }
        public int BuffersLost { get { return GetInt32At(HostOffset(268, 2)); } }
        public string SessionNameString { get { return GetUnicodeStringAt(HostOffset(272, 2)); } }
        public string LogFileNameString { get { return GetUnicodeStringAt(SkipUnicodeString(HostOffset(272, 2))); } }

        #region Private
        internal EventTraceHeaderTraceData(Action<EventTraceHeaderTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != SkipUnicodeString(SkipUnicodeString(HostOffset(272, 2)))));
            Debug.Assert(!(Version > 0 && EventDataLength < SkipUnicodeString(SkipUnicodeString(HostOffset(272, 2)))));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("BufferSize", BufferSize);
             sb.XmlAttrib("Version", Version);
             sb.XmlAttrib("ProviderVersion", ProviderVersion);
             sb.XmlAttrib("NumberOfProcessors", NumberOfProcessors);
             sb.XmlAttrib("EndTime", EndTime);
             sb.XmlAttrib("TimerResolution", TimerResolution);
             sb.XmlAttrib("MaxFileSize", MaxFileSize);
             sb.XmlAttribHex("LogFileMode", LogFileMode);
             sb.XmlAttrib("BuffersWritten", BuffersWritten);
             sb.XmlAttrib("StartBuffers", StartBuffers);
             sb.XmlAttrib("PointerSize", PointerSize);
             sb.XmlAttrib("EventsLost", EventsLost);
             sb.XmlAttrib("CPUSpeed", CPUSpeed);
             sb.XmlAttribHex("LoggerName", LoggerName);
             sb.XmlAttribHex("LogFileName", LogFileName);
             sb.XmlAttrib("TimeZoneInformation", TimeZoneInformation);
             sb.XmlAttrib("BootTime", BootTime);
             sb.XmlAttrib("PerfFreq", PerfFreq);
             sb.XmlAttrib("StartTime", StartTime);
             sb.XmlAttribHex("ReservedFlags", ReservedFlags);
             sb.XmlAttrib("BuffersLost", BuffersLost);
             sb.XmlAttrib("SessionNameString", SessionNameString);
             sb.XmlAttrib("LogFileNameString", LogFileNameString);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "BufferSize", "Version", "ProviderVersion", "NumberOfProcessors", "EndTime", "TimerResolution", "MaxFileSize", "LogFileMode", "BuffersWritten", "StartBuffers", "PointerSize", "EventsLost", "CPUSpeed", "LoggerName", "LogFileName", "TimeZoneInformation", "BootTime", "PerfFreq", "StartTime", "ReservedFlags", "BuffersLost", "SessionNameString", "LogFileNameString"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return BufferSize;
                case 1:
                    return Version;
                case 2:
                    return ProviderVersion;
                case 3:
                    return NumberOfProcessors;
                case 4:
                    return EndTime;
                case 5:
                    return TimerResolution;
                case 6:
                    return MaxFileSize;
                case 7:
                    return LogFileMode;
                case 8:
                    return BuffersWritten;
                case 9:
                    return StartBuffers;
                case 10:
                    return PointerSize;
                case 11:
                    return EventsLost;
                case 12:
                    return CPUSpeed;
                case 13:
                    return LoggerName;
                case 14:
                    return LogFileName;
                case 15:
                    return TimeZoneInformation;
                case 16:
                    return BootTime;
                case 17:
                    return PerfFreq;
                case 18:
                    return StartTime;
                case 19:
                    return ReservedFlags;
                case 20:
                    return BuffersLost;
                case 21:
                    return SessionNameString;
                case 22:
                    return LogFileNameString;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<EventTraceHeaderTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class HeaderExtensionTraceData : TraceEvent
    {
        public int GroupMask1 { get { return GetInt32At(0); } }
        public int GroupMask2 { get { return GetInt32At(4); } }
        public int GroupMask3 { get { return GetInt32At(8); } }
        public int GroupMask4 { get { return GetInt32At(12); } }
        public int GroupMask5 { get { return GetInt32At(16); } }
        public int GroupMask6 { get { return GetInt32At(20); } }
        public int GroupMask7 { get { return GetInt32At(24); } }
        public int GroupMask8 { get { return GetInt32At(28); } }
        public int KernelEventVersion { get { return GetInt32At(32); } }

        #region Private
        internal HeaderExtensionTraceData(Action<HeaderExtensionTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != 36));
            Debug.Assert(!(Version > 0 && EventDataLength < 36));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("GroupMask1", GroupMask1);
             sb.XmlAttribHex("GroupMask2", GroupMask2);
             sb.XmlAttribHex("GroupMask3", GroupMask3);
             sb.XmlAttribHex("GroupMask4", GroupMask4);
             sb.XmlAttribHex("GroupMask5", GroupMask5);
             sb.XmlAttribHex("GroupMask6", GroupMask6);
             sb.XmlAttribHex("GroupMask7", GroupMask7);
             sb.XmlAttribHex("GroupMask8", GroupMask8);
             sb.XmlAttribHex("KernelEventVersion", KernelEventVersion);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "GroupMask1", "GroupMask2", "GroupMask3", "GroupMask4", "GroupMask5", "GroupMask6", "GroupMask7", "GroupMask8", "KernelEventVersion"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return GroupMask1;
                case 1:
                    return GroupMask2;
                case 2:
                    return GroupMask3;
                case 3:
                    return GroupMask4;
                case 4:
                    return GroupMask5;
                case 5:
                    return GroupMask6;
                case 6:
                    return GroupMask7;
                case 7:
                    return GroupMask8;
                case 8:
                    return KernelEventVersion;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<HeaderExtensionTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class ProcessTraceData : TraceEvent
    {
        public Address ProcessId { get { if (Version >= 1) return GetInt32At(HostOffset(4, 1)); return GetHostPointer(0); } }
        public Address ParentId { get { if (Version >= 1) return GetInt32At(HostOffset(8, 1)); return GetHostPointer(HostOffset(4, 1)); } }
        // Skipping UserSID
        public string ImageFileName { get { if (Version >= 1) return GetAsciiStringAt(SkipSID(HostOffset(20, 1))); return GetAsciiStringAt(SkipSID(HostOffset(8, 2))); } }
        public Address PageDirectoryBase { get { if (Version >= 1) return GetHostPointer(0); return 0; } }
        public int SessionId { get { if (Version >= 1) return GetInt32At(HostOffset(12, 1)); return 0; } }
        public int ExitStatus { get { if (Version >= 1) return GetInt32At(HostOffset(16, 1)); return 0; } }
        public Address UniqueProcessKey { get { if (Version >= 2) return GetHostPointer(0); return 0; } }
        public string CommandLine { get { if (Version >= 2) return GetUnicodeStringAt(SkipAsciiString(SkipSID(HostOffset(20, 1)))); return ""; } }

        #region Private
        internal ProcessTraceData(Action<ProcessTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != SkipAsciiString(SkipSID(HostOffset(8, 2)))));
            Debug.Assert(!(Version == 1 && EventDataLength != SkipAsciiString(SkipSID(HostOffset(20, 1)))));
            Debug.Assert(!(Version == 2 && EventDataLength != SkipUnicodeString(SkipAsciiString(SkipSID(HostOffset(20, 1))))));
            Debug.Assert(!(Version > 2 && EventDataLength < SkipUnicodeString(SkipAsciiString(SkipSID(HostOffset(20, 1))))));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("ProcessId", ProcessId);
             sb.XmlAttribHex("ParentId", ParentId);
             sb.XmlAttrib("ImageFileName", ImageFileName);
             sb.XmlAttribHex("PageDirectoryBase", PageDirectoryBase);
             sb.XmlAttrib("SessionId", SessionId);
             sb.XmlAttrib("ExitStatus", ExitStatus);
             sb.XmlAttribHex("UniqueProcessKey", UniqueProcessKey);
             sb.XmlAttrib("CommandLine", CommandLine);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "ProcessId", "ParentId", "ImageFileName", "PageDirectoryBase", "SessionId", "ExitStatus", "UniqueProcessKey", "CommandLine"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return ProcessId;
                case 1:
                    return ParentId;
                case 2:
                    return ImageFileName;
                case 3:
                    return PageDirectoryBase;
                case 4:
                    return SessionId;
                case 5:
                    return ExitStatus;
                case 6:
                    return UniqueProcessKey;
                case 7:
                    return CommandLine;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ProcessTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class ProcessCtrTraceData : TraceEvent
    {
        public int ProcessId { get { return GetInt32At(0); } }
        public int PageFaultCount { get { return GetInt32At(4); } }
        public int HandleCount { get { return GetInt32At(8); } }
        // Skipping Reserved
        public Address PeakVirtualSize { get { return GetHostPointer(16); } }
        public Address PeakWorkingSetSize { get { return GetHostPointer(HostOffset(20, 1)); } }
        public Address PeakPagefileUsage { get { return GetHostPointer(HostOffset(24, 2)); } }
        public Address QuotaPeakPagedPoolUsage { get { return GetHostPointer(HostOffset(28, 3)); } }
        public Address QuotaPeakNonPagedPoolUsage { get { return GetHostPointer(HostOffset(32, 4)); } }
        public Address VirtualSize { get { return GetHostPointer(HostOffset(36, 5)); } }
        public Address WorkingSetSize { get { return GetHostPointer(HostOffset(40, 6)); } }
        public Address PagefileUsage { get { return GetHostPointer(HostOffset(44, 7)); } }
        public Address QuotaPagedPoolUsage { get { return GetHostPointer(HostOffset(48, 8)); } }
        public Address QuotaNonPagedPoolUsage { get { return GetHostPointer(HostOffset(52, 9)); } }
        public Address PrivatePageCount { get { return GetHostPointer(HostOffset(56, 10)); } }

        #region Private
        internal ProcessCtrTraceData(Action<ProcessCtrTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 2 && EventDataLength != HostOffset(60, 11)));
            Debug.Assert(!(Version > 2 && EventDataLength < HostOffset(60, 11)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("ProcessId", ProcessId);
             sb.XmlAttrib("PageFaultCount", PageFaultCount);
             sb.XmlAttrib("HandleCount", HandleCount);
             sb.XmlAttribHex("PeakVirtualSize", PeakVirtualSize);
             sb.XmlAttribHex("PeakWorkingSetSize", PeakWorkingSetSize);
             sb.XmlAttribHex("PeakPagefileUsage", PeakPagefileUsage);
             sb.XmlAttribHex("QuotaPeakPagedPoolUsage", QuotaPeakPagedPoolUsage);
             sb.XmlAttribHex("QuotaPeakNonPagedPoolUsage", QuotaPeakNonPagedPoolUsage);
             sb.XmlAttribHex("VirtualSize", VirtualSize);
             sb.XmlAttribHex("WorkingSetSize", WorkingSetSize);
             sb.XmlAttribHex("PagefileUsage", PagefileUsage);
             sb.XmlAttribHex("QuotaPagedPoolUsage", QuotaPagedPoolUsage);
             sb.XmlAttribHex("QuotaNonPagedPoolUsage", QuotaNonPagedPoolUsage);
             sb.XmlAttribHex("PrivatePageCount", PrivatePageCount);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "ProcessId", "PageFaultCount", "HandleCount", "PeakVirtualSize", "PeakWorkingSetSize", "PeakPagefileUsage", "QuotaPeakPagedPoolUsage", "QuotaPeakNonPagedPoolUsage", "VirtualSize", "WorkingSetSize", "PagefileUsage", "QuotaPagedPoolUsage", "QuotaNonPagedPoolUsage", "PrivatePageCount"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return ProcessId;
                case 1:
                    return PageFaultCount;
                case 2:
                    return HandleCount;
                case 3:
                    return PeakVirtualSize;
                case 4:
                    return PeakWorkingSetSize;
                case 5:
                    return PeakPagefileUsage;
                case 6:
                    return QuotaPeakPagedPoolUsage;
                case 7:
                    return QuotaPeakNonPagedPoolUsage;
                case 8:
                    return VirtualSize;
                case 9:
                    return WorkingSetSize;
                case 10:
                    return PagefileUsage;
                case 11:
                    return QuotaPagedPoolUsage;
                case 12:
                    return QuotaNonPagedPoolUsage;
                case 13:
                    return PrivatePageCount;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ProcessCtrTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class ThreadTraceData : TraceEvent
    {
        public int TThreadId { get { if (Version >= 1) return GetInt32At(4); return GetInt32At(0); } }
        public int ProcessId { get { if (Version >= 1) return GetInt32At(0); return GetInt32At(4); } }
        public Address StackBase { get { if (Version >= 1) return GetHostPointer(8); return 0; } }
        public Address StackLimit { get { if (Version >= 1) return GetHostPointer(HostOffset(12, 1)); return 0; } }
        public Address UserStackBase { get { if (Version >= 1) return GetHostPointer(HostOffset(16, 2)); return 0; } }
        public Address UserStackLimit { get { if (Version >= 1) return GetHostPointer(HostOffset(20, 3)); return 0; } }
        public Address StartAddr { get { if (Version >= 1) return GetHostPointer(HostOffset(24, 4)); return 0; } }
        public Address Win32StartAddr { get { if (Version >= 1) return GetHostPointer(HostOffset(28, 5)); return 0; } }
        public int WaitMode { get { if (Version >= 1) return GetByteAt(HostOffset(32, 6)); return 0; } }
        public Address TebBase { get { if (Version >= 2) return GetHostPointer(HostOffset(32, 6)); return 0; } }
        public int SubProcessTag { get { if (Version >= 2) return GetInt32At(HostOffset(36, 7)); return 0; } }

        #region Private
        internal ThreadTraceData(Action<ThreadTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != 8));
            Debug.Assert(!(Version == 1 && EventDataLength != HostOffset(33, 6)));
            Debug.Assert(!(Version == 2 && EventDataLength != HostOffset(40, 7)));
            Debug.Assert(!(Version > 2 && EventDataLength < HostOffset(40, 7)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("TThreadId", TThreadId);
             sb.XmlAttribHex("ProcessId", ProcessId);
             sb.XmlAttribHex("StackBase", StackBase);
             sb.XmlAttribHex("StackLimit", StackLimit);
             sb.XmlAttribHex("UserStackBase", UserStackBase);
             sb.XmlAttribHex("UserStackLimit", UserStackLimit);
             sb.XmlAttribHex("StartAddr", StartAddr);
             sb.XmlAttribHex("Win32StartAddr", Win32StartAddr);
             sb.XmlAttrib("WaitMode", WaitMode);
             sb.XmlAttribHex("TebBase", TebBase);
             sb.XmlAttribHex("SubProcessTag", SubProcessTag);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "TThreadId", "ProcessId", "StackBase", "StackLimit", "UserStackBase", "UserStackLimit", "StartAddr", "Win32StartAddr", "WaitMode", "TebBase", "SubProcessTag"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return TThreadId;
                case 1:
                    return ProcessId;
                case 2:
                    return StackBase;
                case 3:
                    return StackLimit;
                case 4:
                    return UserStackBase;
                case 5:
                    return UserStackLimit;
                case 6:
                    return StartAddr;
                case 7:
                    return Win32StartAddr;
                case 8:
                    return WaitMode;
                case 9:
                    return TebBase;
                case 10:
                    return SubProcessTag;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ThreadTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class CSwitchTraceData : TraceEvent
    {
        public int NewThreadId { get { return GetInt32At(0); } }
        public int OldThreadId { get { return GetInt32At(4); } }
        public int NewThreadPriority { get { return GetByteAt(8); } }
        public int OldThreadPriority { get { return GetByteAt(9); } }
        public int PreviousCState { get { return GetByteAt(10); } }
        public int SpareByte { get { return GetByteAt(11); } }
        public int OldThreadWaitReason { get { return GetByteAt(12); } }
        public int OldThreadWaitMode { get { return GetByteAt(13); } }
        public int OldThreadState { get { return GetByteAt(14); } }
        public int OldThreadWaitIdealProcessor { get { return GetByteAt(15); } }
        public int NewThreadWaitTime { get { return GetInt32At(16); } }
        // Skipping Reserved

        #region Private
        internal CSwitchTraceData(Action<CSwitchTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 2 && EventDataLength != 24));
            Debug.Assert(!(Version > 2 && EventDataLength < 24));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("NewThreadId", NewThreadId);
             sb.XmlAttribHex("OldThreadId", OldThreadId);
             sb.XmlAttrib("NewThreadPriority", NewThreadPriority);
             sb.XmlAttrib("OldThreadPriority", OldThreadPriority);
             sb.XmlAttrib("PreviousCState", PreviousCState);
             sb.XmlAttrib("SpareByte", SpareByte);
             sb.XmlAttrib("OldThreadWaitReason", OldThreadWaitReason);
             sb.XmlAttrib("OldThreadWaitMode", OldThreadWaitMode);
             sb.XmlAttrib("OldThreadState", OldThreadState);
             sb.XmlAttrib("OldThreadWaitIdealProcessor", OldThreadWaitIdealProcessor);
             sb.XmlAttribHex("NewThreadWaitTime", NewThreadWaitTime);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "NewThreadId", "OldThreadId", "NewThreadPriority", "OldThreadPriority", "PreviousCState", "SpareByte", "OldThreadWaitReason", "OldThreadWaitMode", "OldThreadState", "OldThreadWaitIdealProcessor", "NewThreadWaitTime"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return NewThreadId;
                case 1:
                    return OldThreadId;
                case 2:
                    return NewThreadPriority;
                case 3:
                    return OldThreadPriority;
                case 4:
                    return PreviousCState;
                case 5:
                    return SpareByte;
                case 6:
                    return OldThreadWaitReason;
                case 7:
                    return OldThreadWaitMode;
                case 8:
                    return OldThreadState;
                case 9:
                    return OldThreadWaitIdealProcessor;
                case 10:
                    return NewThreadWaitTime;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<CSwitchTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class WorkerThreadTraceData : TraceEvent
    {
        public int TThreadId { get { return GetInt32At(0); } }
        public long StartTime { get { return GetInt64At(4); } }
        public Address ThreadRoutine { get { return GetHostPointer(12); } }

        #region Private
        internal WorkerThreadTraceData(Action<WorkerThreadTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 2 && EventDataLength != HostOffset(16, 1)));
            Debug.Assert(!(Version > 2 && EventDataLength < HostOffset(16, 1)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("TThreadId", TThreadId);
             sb.XmlAttrib("StartTime", StartTime);
             sb.XmlAttribHex("ThreadRoutine", ThreadRoutine);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "TThreadId", "StartTime", "ThreadRoutine"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return TThreadId;
                case 1:
                    return StartTime;
                case 2:
                    return ThreadRoutine;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<WorkerThreadTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class ReserveCreateTraceData : TraceEvent
    {
        public Address Reserve { get { return GetHostPointer(0); } }
        public int Period { get { return GetInt32At(HostOffset(4, 1)); } }
        public int Budget { get { return GetInt32At(HostOffset(8, 1)); } }
        public int ObjectFlags { get { return GetInt32At(HostOffset(12, 1)); } }
        public int Processor { get { return GetByteAt(HostOffset(16, 1)); } }

        #region Private
        internal ReserveCreateTraceData(Action<ReserveCreateTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 2 && EventDataLength != HostOffset(17, 1)));
            Debug.Assert(!(Version > 2 && EventDataLength < HostOffset(17, 1)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("Reserve", Reserve);
             sb.XmlAttrib("Period", Period);
             sb.XmlAttrib("Budget", Budget);
             sb.XmlAttrib("ObjectFlags", ObjectFlags);
             sb.XmlAttrib("Processor", Processor);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Reserve", "Period", "Budget", "ObjectFlags", "Processor"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Reserve;
                case 1:
                    return Period;
                case 2:
                    return Budget;
                case 3:
                    return ObjectFlags;
                case 4:
                    return Processor;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ReserveCreateTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class ReserveDeleteTraceData : TraceEvent
    {
        public Address Reserve { get { return GetHostPointer(0); } }

        #region Private
        internal ReserveDeleteTraceData(Action<ReserveDeleteTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 2 && EventDataLength != HostOffset(4, 1)));
            Debug.Assert(!(Version > 2 && EventDataLength < HostOffset(4, 1)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("Reserve", Reserve);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Reserve"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Reserve;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ReserveDeleteTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class ReserveJoinThreadTraceData : TraceEvent
    {
        public Address Reserve { get { return GetHostPointer(0); } }
        public int TThreadId { get { return GetInt32At(HostOffset(4, 1)); } }

        #region Private
        internal ReserveJoinThreadTraceData(Action<ReserveJoinThreadTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 2 && EventDataLength != HostOffset(8, 1)));
            Debug.Assert(!(Version > 2 && EventDataLength < HostOffset(8, 1)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("Reserve", Reserve);
             sb.XmlAttribHex("TThreadId", TThreadId);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Reserve", "TThreadId"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Reserve;
                case 1:
                    return TThreadId;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ReserveJoinThreadTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class ReserveDisjoinThreadTraceData : TraceEvent
    {
        public Address Reserve { get { return GetHostPointer(0); } }
        public int TThreadId { get { return GetInt32At(HostOffset(4, 1)); } }

        #region Private
        internal ReserveDisjoinThreadTraceData(Action<ReserveDisjoinThreadTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 2 && EventDataLength != HostOffset(8, 1)));
            Debug.Assert(!(Version > 2 && EventDataLength < HostOffset(8, 1)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("Reserve", Reserve);
             sb.XmlAttribHex("TThreadId", TThreadId);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Reserve", "TThreadId"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Reserve;
                case 1:
                    return TThreadId;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ReserveDisjoinThreadTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class ReserveStateTraceData : TraceEvent
    {
        public Address Reserve { get { return GetHostPointer(0); } }
        public int DispatchState { get { return GetByteAt(HostOffset(4, 1)); } }
        public bool Replenished { get { return GetByteAt(HostOffset(5, 1)) != 0; } }

        #region Private
        internal ReserveStateTraceData(Action<ReserveStateTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 2 && EventDataLength != HostOffset(6, 1)));
            Debug.Assert(!(Version > 2 && EventDataLength < HostOffset(6, 1)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("Reserve", Reserve);
             sb.XmlAttrib("DispatchState", DispatchState);
             sb.XmlAttrib("Replenished", Replenished);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Reserve", "DispatchState", "Replenished"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Reserve;
                case 1:
                    return DispatchState;
                case 2:
                    return Replenished;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ReserveStateTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class ReserveBandwidthTraceData : TraceEvent
    {
        public Address Reserve { get { return GetHostPointer(0); } }
        public int Period { get { return GetInt32At(HostOffset(4, 1)); } }
        public int Budget { get { return GetInt32At(HostOffset(8, 1)); } }

        #region Private
        internal ReserveBandwidthTraceData(Action<ReserveBandwidthTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 2 && EventDataLength != HostOffset(12, 1)));
            Debug.Assert(!(Version > 2 && EventDataLength < HostOffset(12, 1)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("Reserve", Reserve);
             sb.XmlAttrib("Period", Period);
             sb.XmlAttrib("Budget", Budget);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Reserve", "Period", "Budget"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Reserve;
                case 1:
                    return Period;
                case 2:
                    return Budget;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ReserveBandwidthTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class ReserveLateCountTraceData : TraceEvent
    {
        public Address Reserve { get { return GetHostPointer(0); } }
        public int LateCountIncrement { get { return GetInt32At(HostOffset(4, 1)); } }

        #region Private
        internal ReserveLateCountTraceData(Action<ReserveLateCountTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 2 && EventDataLength != HostOffset(8, 1)));
            Debug.Assert(!(Version > 2 && EventDataLength < HostOffset(8, 1)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("Reserve", Reserve);
             sb.XmlAttrib("LateCountIncrement", LateCountIncrement);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Reserve", "LateCountIncrement"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Reserve;
                case 1:
                    return LateCountIncrement;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ReserveLateCountTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class DiskIoTraceData : TraceEvent
    {
        public int DiskNumber { get { return GetInt32At(0); } }
        public int IrpFlags { get { return GetInt32At(4); } }
        public int TransferSize { get { return GetInt32At(8); } }
        // Skipping Reserved
        public long ByteOffset { get { return GetInt64At(16); } }
        public Address FileObject { get { return GetHostPointer(24); } }
        public Address Irp { get { return GetHostPointer(HostOffset(28, 1)); } }
        public long HighResResponseTime { get { return GetInt64At(HostOffset(32, 2)); } }

        #region Private
        internal DiskIoTraceData(Action<DiskIoTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(40, 2)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(40, 2)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("DiskNumber", DiskNumber);
             sb.XmlAttribHex("IrpFlags", IrpFlags);
             sb.XmlAttrib("TransferSize", TransferSize);
             sb.XmlAttrib("ByteOffset", ByteOffset);
             sb.XmlAttribHex("FileObject", FileObject);
             sb.XmlAttribHex("Irp", Irp);
             sb.XmlAttrib("HighResResponseTime", HighResResponseTime);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "DiskNumber", "IrpFlags", "TransferSize", "ByteOffset", "FileObject", "Irp", "HighResResponseTime"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return DiskNumber;
                case 1:
                    return IrpFlags;
                case 2:
                    return TransferSize;
                case 3:
                    return ByteOffset;
                case 4:
                    return FileObject;
                case 5:
                    return Irp;
                case 6:
                    return HighResResponseTime;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<DiskIoTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class DiskIoInitTraceData : TraceEvent
    {
        public Address Irp { get { return GetHostPointer(0); } }

        #region Private
        internal DiskIoInitTraceData(Action<DiskIoInitTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(4, 1)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(4, 1)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("Irp", Irp);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Irp"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Irp;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<DiskIoInitTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class DiskIoFlushBuffersTraceData : TraceEvent
    {
        public int DiskNumber { get { return GetInt32At(0); } }
        public int IrpFlags { get { return GetInt32At(4); } }
        public long HighResResponseTime { get { return GetInt64At(8); } }
        public Address Irp { get { return GetHostPointer(16); } }

        #region Private
        internal DiskIoFlushBuffersTraceData(Action<DiskIoFlushBuffersTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(20, 1)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(20, 1)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("DiskNumber", DiskNumber);
             sb.XmlAttribHex("IrpFlags", IrpFlags);
             sb.XmlAttrib("HighResResponseTime", HighResResponseTime);
             sb.XmlAttribHex("Irp", Irp);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "DiskNumber", "IrpFlags", "HighResResponseTime", "Irp"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return DiskNumber;
                case 1:
                    return IrpFlags;
                case 2:
                    return HighResResponseTime;
                case 3:
                    return Irp;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<DiskIoFlushBuffersTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class DriverMajorFunctionCallTraceData : TraceEvent
    {
        public int MajorFunction { get { return GetInt32At(0); } }
        public int MinorFunction { get { return GetInt32At(4); } }
        public Address RoutineAddr { get { return GetHostPointer(8); } }
        public Address FileObject { get { return GetHostPointer(HostOffset(12, 1)); } }
        public Address Irp { get { return GetHostPointer(HostOffset(16, 2)); } }
        public int UniqMatchId { get { return GetInt32At(HostOffset(20, 3)); } }

        #region Private
        internal DriverMajorFunctionCallTraceData(Action<DriverMajorFunctionCallTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(24, 3)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(24, 3)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("MajorFunction", MajorFunction);
             sb.XmlAttrib("MinorFunction", MinorFunction);
             sb.XmlAttribHex("RoutineAddr", RoutineAddr);
             sb.XmlAttribHex("FileObject", FileObject);
             sb.XmlAttribHex("Irp", Irp);
             sb.XmlAttrib("UniqMatchId", UniqMatchId);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "MajorFunction", "MinorFunction", "RoutineAddr", "FileObject", "Irp", "UniqMatchId"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return MajorFunction;
                case 1:
                    return MinorFunction;
                case 2:
                    return RoutineAddr;
                case 3:
                    return FileObject;
                case 4:
                    return Irp;
                case 5:
                    return UniqMatchId;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<DriverMajorFunctionCallTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class DriverMajorFunctionReturnTraceData : TraceEvent
    {
        public Address Irp { get { return GetHostPointer(0); } }
        public int UniqMatchId { get { return GetInt32At(HostOffset(4, 1)); } }

        #region Private
        internal DriverMajorFunctionReturnTraceData(Action<DriverMajorFunctionReturnTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(8, 1)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(8, 1)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("Irp", Irp);
             sb.XmlAttrib("UniqMatchId", UniqMatchId);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Irp", "UniqMatchId"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Irp;
                case 1:
                    return UniqMatchId;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<DriverMajorFunctionReturnTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class DriverCompletionRoutineTraceData : TraceEvent
    {
        public Address Routine { get { return GetHostPointer(0); } }
        public Address IrpPtr { get { return GetHostPointer(HostOffset(4, 1)); } }
        public int UniqMatchId { get { return GetInt32At(HostOffset(8, 2)); } }

        #region Private
        internal DriverCompletionRoutineTraceData(Action<DriverCompletionRoutineTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(12, 2)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(12, 2)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("Routine", Routine);
             sb.XmlAttribHex("IrpPtr", IrpPtr);
             sb.XmlAttrib("UniqMatchId", UniqMatchId);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Routine", "IrpPtr", "UniqMatchId"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Routine;
                case 1:
                    return IrpPtr;
                case 2:
                    return UniqMatchId;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<DriverCompletionRoutineTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class DriverCompleteRequestTraceData : TraceEvent
    {
        public Address RoutineAddr { get { return GetHostPointer(0); } }
        public Address Irp { get { return GetHostPointer(HostOffset(4, 1)); } }
        public int UniqMatchId { get { return GetInt32At(HostOffset(8, 2)); } }

        #region Private
        internal DriverCompleteRequestTraceData(Action<DriverCompleteRequestTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(12, 2)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(12, 2)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("RoutineAddr", RoutineAddr);
             sb.XmlAttribHex("Irp", Irp);
             sb.XmlAttrib("UniqMatchId", UniqMatchId);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "RoutineAddr", "Irp", "UniqMatchId"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return RoutineAddr;
                case 1:
                    return Irp;
                case 2:
                    return UniqMatchId;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<DriverCompleteRequestTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class DriverCompleteRequestReturnTraceData : TraceEvent
    {
        public Address Irp { get { return GetHostPointer(0); } }
        public int UniqMatchId { get { return GetInt32At(HostOffset(4, 1)); } }

        #region Private
        internal DriverCompleteRequestReturnTraceData(Action<DriverCompleteRequestReturnTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(8, 1)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(8, 1)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("Irp", Irp);
             sb.XmlAttrib("UniqMatchId", UniqMatchId);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Irp", "UniqMatchId"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Irp;
                case 1:
                    return UniqMatchId;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<DriverCompleteRequestReturnTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class RegistryTraceData : TraceEvent
    {
        public Address Status { get { if (Version >= 2) return GetInt32At(8); return GetHostPointer(0); } }
        public Address KeyHandle { get { if (Version >= 2) return GetHostPointer(16); return GetHostPointer(HostOffset(4, 1)); } }
        public long ElapsedTime { get { return GetInt64At(HostOffset(8, 2)); } }
        public string KeyName { get { if (Version >= 2) return GetUnicodeStringAt(HostOffset(20, 1)); if (Version >= 1) return GetUnicodeStringAt(HostOffset(20, 2)); return GetUnicodeStringAt(HostOffset(16, 2)); } }
        public int Index { get { if (Version >= 2) return GetInt32At(12); if (Version >= 1) return GetInt32At(HostOffset(16, 2)); return 0; } }
        public long InitialTime { get { if (Version >= 2) return GetInt64At(0); return 0; } }

        #region Private
        internal RegistryTraceData(Action<RegistryTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != SkipUnicodeString(HostOffset(16, 2))));
            Debug.Assert(!(Version == 1 && EventDataLength != SkipUnicodeString(HostOffset(20, 2))));
            Debug.Assert(!(Version == 2 && EventDataLength != SkipUnicodeString(HostOffset(20, 1))));
            Debug.Assert(!(Version > 2 && EventDataLength < SkipUnicodeString(HostOffset(20, 1))));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("Status", Status);
             sb.XmlAttribHex("KeyHandle", KeyHandle);
             sb.XmlAttrib("ElapsedTime", ElapsedTime);
             sb.XmlAttrib("KeyName", KeyName);
             sb.XmlAttrib("Index", Index);
             sb.XmlAttrib("InitialTime", InitialTime);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Status", "KeyHandle", "ElapsedTime", "KeyName", "Index", "InitialTime"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Status;
                case 1:
                    return KeyHandle;
                case 2:
                    return ElapsedTime;
                case 3:
                    return KeyName;
                case 4:
                    return Index;
                case 5:
                    return InitialTime;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<RegistryTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class SplitIoInfoTraceData : TraceEvent
    {
        public Address ParentIrp { get { return GetHostPointer(0); } }
        public Address ChildIrp { get { return GetHostPointer(HostOffset(4, 1)); } }

        #region Private
        internal SplitIoInfoTraceData(Action<SplitIoInfoTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(8, 2)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(8, 2)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("ParentIrp", ParentIrp);
             sb.XmlAttribHex("ChildIrp", ChildIrp);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "ParentIrp", "ChildIrp"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return ParentIrp;
                case 1:
                    return ChildIrp;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<SplitIoInfoTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class FileIoNameTraceData : TraceEvent
    {
        public Address FileObject { get { return GetHostPointer(0); } }
        public string FileName { get { return GetUnicodeStringAt(HostOffset(4, 1)); } }

        #region Private
        internal FileIoNameTraceData(Action<FileIoNameTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != SkipUnicodeString(HostOffset(4, 1))));
            Debug.Assert(!(Version == 1 && EventDataLength != SkipUnicodeString(HostOffset(4, 1))));
            Debug.Assert(!(Version == 2 && EventDataLength != SkipUnicodeString(HostOffset(4, 1))));
            Debug.Assert(!(Version > 2 && EventDataLength < SkipUnicodeString(HostOffset(4, 1))));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("FileObject", FileObject);
             sb.XmlAttrib("FileName", FileName);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "FileObject", "FileName"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return FileObject;
                case 1:
                    return FileName;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<FileIoNameTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class FileIoCreateTraceData : TraceEvent
    {
        public Address IrpPtr { get { return GetHostPointer(0); } }
        public Address TTID { get { return GetHostPointer(HostOffset(4, 1)); } }
        public Address FileObject { get { return GetHostPointer(HostOffset(8, 2)); } }
        public int CreateOptions { get { return GetInt32At(HostOffset(12, 3)); } }
        public int FileAttributes { get { return GetInt32At(HostOffset(16, 3)); } }
        public int ShareAccess { get { return GetInt32At(HostOffset(20, 3)); } }
        public string OpenPath { get { return GetUnicodeStringAt(HostOffset(24, 3)); } }

        #region Private
        internal FileIoCreateTraceData(Action<FileIoCreateTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 2 && EventDataLength != SkipUnicodeString(HostOffset(24, 3))));
            Debug.Assert(!(Version > 2 && EventDataLength < SkipUnicodeString(HostOffset(24, 3))));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("IrpPtr", IrpPtr);
             sb.XmlAttribHex("TTID", TTID);
             sb.XmlAttribHex("FileObject", FileObject);
             sb.XmlAttrib("CreateOptions", CreateOptions);
             sb.XmlAttrib("FileAttributes", FileAttributes);
             sb.XmlAttrib("ShareAccess", ShareAccess);
             sb.XmlAttrib("OpenPath", OpenPath);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "IrpPtr", "TTID", "FileObject", "CreateOptions", "FileAttributes", "ShareAccess", "OpenPath"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return IrpPtr;
                case 1:
                    return TTID;
                case 2:
                    return FileObject;
                case 3:
                    return CreateOptions;
                case 4:
                    return FileAttributes;
                case 5:
                    return ShareAccess;
                case 6:
                    return OpenPath;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<FileIoCreateTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class FileIoSimpleOpTraceData : TraceEvent
    {
        public Address IrpPtr { get { return GetHostPointer(0); } }
        public Address TTID { get { return GetHostPointer(HostOffset(4, 1)); } }
        public Address FileObject { get { return GetHostPointer(HostOffset(8, 2)); } }
        public Address FileKey { get { return GetHostPointer(HostOffset(12, 3)); } }

        #region Private
        internal FileIoSimpleOpTraceData(Action<FileIoSimpleOpTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 2 && EventDataLength != HostOffset(16, 4)));
            Debug.Assert(!(Version > 2 && EventDataLength < HostOffset(16, 4)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("IrpPtr", IrpPtr);
             sb.XmlAttribHex("TTID", TTID);
             sb.XmlAttribHex("FileObject", FileObject);
             sb.XmlAttribHex("FileKey", FileKey);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "IrpPtr", "TTID", "FileObject", "FileKey"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return IrpPtr;
                case 1:
                    return TTID;
                case 2:
                    return FileObject;
                case 3:
                    return FileKey;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<FileIoSimpleOpTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class FileIoReadWriteTraceData : TraceEvent
    {
        public long Offset { get { return GetInt64At(0); } }
        public Address IrpPtr { get { return GetHostPointer(8); } }
        public Address TTID { get { return GetHostPointer(HostOffset(12, 1)); } }
        public Address FileObject { get { return GetHostPointer(HostOffset(16, 2)); } }
        public Address FileKey { get { return GetHostPointer(HostOffset(20, 3)); } }
        public int IoSize { get { return GetInt32At(HostOffset(24, 4)); } }
        public int IoFlags { get { return GetInt32At(HostOffset(28, 4)); } }

        #region Private
        internal FileIoReadWriteTraceData(Action<FileIoReadWriteTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 2 && EventDataLength != HostOffset(32, 4)));
            Debug.Assert(!(Version > 2 && EventDataLength < HostOffset(32, 4)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("Offset", Offset);
             sb.XmlAttribHex("IrpPtr", IrpPtr);
             sb.XmlAttribHex("TTID", TTID);
             sb.XmlAttribHex("FileObject", FileObject);
             sb.XmlAttribHex("FileKey", FileKey);
             sb.XmlAttrib("IoSize", IoSize);
             sb.XmlAttrib("IoFlags", IoFlags);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Offset", "IrpPtr", "TTID", "FileObject", "FileKey", "IoSize", "IoFlags"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Offset;
                case 1:
                    return IrpPtr;
                case 2:
                    return TTID;
                case 3:
                    return FileObject;
                case 4:
                    return FileKey;
                case 5:
                    return IoSize;
                case 6:
                    return IoFlags;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<FileIoReadWriteTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class FileIoInfoTraceData : TraceEvent
    {
        public Address IrpPtr { get { return GetHostPointer(0); } }
        public Address TTID { get { return GetHostPointer(HostOffset(4, 1)); } }
        public Address FileObject { get { return GetHostPointer(HostOffset(8, 2)); } }
        public Address FileKey { get { return GetHostPointer(HostOffset(12, 3)); } }
        public Address ExtraInfo { get { return GetHostPointer(HostOffset(16, 4)); } }
        public int InfoClass { get { return GetInt32At(HostOffset(20, 5)); } }

        #region Private
        internal FileIoInfoTraceData(Action<FileIoInfoTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 2 && EventDataLength != HostOffset(24, 5)));
            Debug.Assert(!(Version > 2 && EventDataLength < HostOffset(24, 5)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("IrpPtr", IrpPtr);
             sb.XmlAttribHex("TTID", TTID);
             sb.XmlAttribHex("FileObject", FileObject);
             sb.XmlAttribHex("FileKey", FileKey);
             sb.XmlAttribHex("ExtraInfo", ExtraInfo);
             sb.XmlAttrib("InfoClass", InfoClass);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "IrpPtr", "TTID", "FileObject", "FileKey", "ExtraInfo", "InfoClass"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return IrpPtr;
                case 1:
                    return TTID;
                case 2:
                    return FileObject;
                case 3:
                    return FileKey;
                case 4:
                    return ExtraInfo;
                case 5:
                    return InfoClass;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<FileIoInfoTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class FileIoDirEnumTraceData : TraceEvent
    {
        public Address IrpPtr { get { return GetHostPointer(0); } }
        public Address TTID { get { return GetHostPointer(HostOffset(4, 1)); } }
        public Address FileObject { get { return GetHostPointer(HostOffset(8, 2)); } }
        public Address FileKey { get { return GetHostPointer(HostOffset(12, 3)); } }
        public int Length { get { return GetInt32At(HostOffset(16, 4)); } }
        public int InfoClass { get { return GetInt32At(HostOffset(20, 4)); } }
        public int FileIndex { get { return GetInt32At(HostOffset(24, 4)); } }
        public string FileName { get { return GetUnicodeStringAt(HostOffset(28, 4)); } }

        #region Private
        internal FileIoDirEnumTraceData(Action<FileIoDirEnumTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 2 && EventDataLength != SkipUnicodeString(HostOffset(28, 4))));
            Debug.Assert(!(Version > 2 && EventDataLength < SkipUnicodeString(HostOffset(28, 4))));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("IrpPtr", IrpPtr);
             sb.XmlAttribHex("TTID", TTID);
             sb.XmlAttribHex("FileObject", FileObject);
             sb.XmlAttribHex("FileKey", FileKey);
             sb.XmlAttrib("Length", Length);
             sb.XmlAttrib("InfoClass", InfoClass);
             sb.XmlAttrib("FileIndex", FileIndex);
             sb.XmlAttrib("FileName", FileName);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "IrpPtr", "TTID", "FileObject", "FileKey", "Length", "InfoClass", "FileIndex", "FileName"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return IrpPtr;
                case 1:
                    return TTID;
                case 2:
                    return FileObject;
                case 3:
                    return FileKey;
                case 4:
                    return Length;
                case 5:
                    return InfoClass;
                case 6:
                    return FileIndex;
                case 7:
                    return FileName;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<FileIoDirEnumTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class FileIoOpEndTraceData : TraceEvent
    {
        public Address IrpPtr { get { return GetHostPointer(0); } }
        public Address ExtraInfo { get { return GetHostPointer(HostOffset(4, 1)); } }
        public int NtStatus { get { return GetInt32At(HostOffset(8, 2)); } }

        #region Private
        internal FileIoOpEndTraceData(Action<FileIoOpEndTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 2 && EventDataLength != HostOffset(12, 2)));
            Debug.Assert(!(Version > 2 && EventDataLength < HostOffset(12, 2)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("IrpPtr", IrpPtr);
             sb.XmlAttribHex("ExtraInfo", ExtraInfo);
             sb.XmlAttrib("NtStatus", NtStatus);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "IrpPtr", "ExtraInfo", "NtStatus"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return IrpPtr;
                case 1:
                    return ExtraInfo;
                case 2:
                    return NtStatus;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<FileIoOpEndTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class TcpIpTraceData : TraceEvent
    {
        public int daddr { get { if (Version >= 1) return GetInt32At(8); return GetInt32At(0); } }
        public int saddr { get { if (Version >= 1) return GetInt32At(12); return GetInt32At(4); } }
        public int dport { get { if (Version >= 1) return GetInt16At(16); return GetInt16At(8); } }
        public int sport { get { if (Version >= 1) return GetInt16At(18); return GetInt16At(10); } }
        public int size { get { if (Version >= 1) return GetInt32At(4); return GetInt32At(12); } }
        public int PID { get { if (Version >= 1) return GetInt32At(0); return GetInt32At(16); } }
        public int startime { get { if (Version >= 1) return GetInt32At(20); return 0; } }
        public int endtime { get { if (Version >= 1) return GetInt32At(24); return 0; } }
        public int connid { get { if (Version >= 1) return GetInt32At(28); return 0; } }
        public int seqnum { get { if (Version >= 1) return GetInt32At(32); return 0; } }

        #region Private
        internal TcpIpTraceData(Action<TcpIpTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != 20));
            Debug.Assert(!(Version == 1 && EventDataLength != 36));
            Debug.Assert(!(Version > 1 && EventDataLength < 36));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("daddr", daddr);
             sb.XmlAttrib("saddr", saddr);
             sb.XmlAttrib("dport", dport);
             sb.XmlAttrib("sport", sport);
             sb.XmlAttrib("size", size);
             sb.XmlAttrib("PID", PID);
             sb.XmlAttrib("startime", startime);
             sb.XmlAttrib("endtime", endtime);
             sb.XmlAttrib("connid", connid);
             sb.XmlAttrib("seqnum", seqnum);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "daddr", "saddr", "dport", "sport", "size", "PID", "startime", "endtime", "connid", "seqnum"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return daddr;
                case 1:
                    return saddr;
                case 2:
                    return dport;
                case 3:
                    return sport;
                case 4:
                    return size;
                case 5:
                    return PID;
                case 6:
                    return startime;
                case 7:
                    return endtime;
                case 8:
                    return connid;
                case 9:
                    return seqnum;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<TcpIpTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class TcpIpFailTraceData : TraceEvent
    {
        public int Proto { get { if (Version >= 2) return GetInt16At(0); return GetInt32At(0); } }
        public int FailureCode { get { if (Version >= 2) return GetInt16At(2); return 0; } }

        #region Private
        internal TcpIpFailTraceData(Action<TcpIpFailTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 1 && EventDataLength != 4));
            Debug.Assert(!(Version == 2 && EventDataLength != 4));
            Debug.Assert(!(Version > 2 && EventDataLength < 4));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("Proto", Proto);
             sb.XmlAttrib("FailureCode", FailureCode);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Proto", "FailureCode"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Proto;
                case 1:
                    return FailureCode;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<TcpIpFailTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class TcpIpV6TraceData : TraceEvent
    {
        public int PID { get { return GetInt32At(0); } }
        public int size { get { return GetInt32At(4); } }
        public int daddr { get { return GetInt32At(8); } }
        public int saddr { get { return GetInt32At(12); } }
        public int dport { get { return GetInt16At(16); } }
        public int sport { get { return GetInt16At(18); } }
        public int connid { get { return GetInt32At(20); } }
        public int seqnum { get { return GetInt32At(24); } }

        #region Private
        internal TcpIpV6TraceData(Action<TcpIpV6TraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 1 && EventDataLength != 28));
            Debug.Assert(!(Version > 1 && EventDataLength < 28));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("PID", PID);
             sb.XmlAttrib("size", size);
             sb.XmlAttrib("daddr", daddr);
             sb.XmlAttrib("saddr", saddr);
             sb.XmlAttrib("dport", dport);
             sb.XmlAttrib("sport", sport);
             sb.XmlAttrib("connid", connid);
             sb.XmlAttrib("seqnum", seqnum);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "PID", "size", "daddr", "saddr", "dport", "sport", "connid", "seqnum"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return PID;
                case 1:
                    return size;
                case 2:
                    return daddr;
                case 3:
                    return saddr;
                case 4:
                    return dport;
                case 5:
                    return sport;
                case 6:
                    return connid;
                case 7:
                    return seqnum;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<TcpIpV6TraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class TcpIpSendIPV4TraceData : TraceEvent
    {
        public int PID { get { return GetInt32At(0); } }
        public int size { get { return GetInt32At(4); } }
        public int daddr { get { return GetInt32At(8); } }
        public int saddr { get { return GetInt32At(12); } }
        public int dport { get { return GetInt16At(16); } }
        public int sport { get { return GetInt16At(18); } }
        public int startime { get { return GetInt32At(20); } }
        public int endtime { get { return GetInt32At(24); } }
        public int seqnum { get { return GetInt32At(28); } }
        public int connid { get { return GetInt32At(32); } }

        #region Private
        internal TcpIpSendIPV4TraceData(Action<TcpIpSendIPV4TraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 2 && EventDataLength != 36));
            Debug.Assert(!(Version > 2 && EventDataLength < 36));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("PID", PID);
             sb.XmlAttrib("size", size);
             sb.XmlAttrib("daddr", daddr);
             sb.XmlAttrib("saddr", saddr);
             sb.XmlAttrib("dport", dport);
             sb.XmlAttrib("sport", sport);
             sb.XmlAttrib("startime", startime);
             sb.XmlAttrib("endtime", endtime);
             sb.XmlAttrib("seqnum", seqnum);
             sb.XmlAttrib("connid", connid);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "PID", "size", "daddr", "saddr", "dport", "sport", "startime", "endtime", "seqnum", "connid"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return PID;
                case 1:
                    return size;
                case 2:
                    return daddr;
                case 3:
                    return saddr;
                case 4:
                    return dport;
                case 5:
                    return sport;
                case 6:
                    return startime;
                case 7:
                    return endtime;
                case 8:
                    return seqnum;
                case 9:
                    return connid;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<TcpIpSendIPV4TraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class TcpIpConnectTraceData : TraceEvent
    {
        public int PID { get { return GetInt32At(0); } }
        public int size { get { return GetInt32At(4); } }
        public int daddr { get { return GetInt32At(8); } }
        public int saddr { get { return GetInt32At(12); } }
        public int dport { get { return GetInt16At(16); } }
        public int sport { get { return GetInt16At(18); } }
        public int mss { get { return GetInt16At(20); } }
        public int sackopt { get { return GetInt16At(22); } }
        public int tsopt { get { return GetInt16At(24); } }
        public int wsopt { get { return GetInt16At(26); } }
        public int rcvwin { get { return GetInt32At(28); } }
        public int rcvwinscale { get { return GetInt16At(32); } }
        public int sndwinscale { get { return GetInt16At(34); } }
        public int seqnum { get { return GetInt32At(36); } }
        public int connid { get { return GetInt32At(40); } }

        #region Private
        internal TcpIpConnectTraceData(Action<TcpIpConnectTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 2 && EventDataLength != 44));
            Debug.Assert(!(Version > 2 && EventDataLength < 44));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("PID", PID);
             sb.XmlAttrib("size", size);
             sb.XmlAttrib("daddr", daddr);
             sb.XmlAttrib("saddr", saddr);
             sb.XmlAttrib("dport", dport);
             sb.XmlAttrib("sport", sport);
             sb.XmlAttrib("mss", mss);
             sb.XmlAttrib("sackopt", sackopt);
             sb.XmlAttrib("tsopt", tsopt);
             sb.XmlAttrib("wsopt", wsopt);
             sb.XmlAttrib("rcvwin", rcvwin);
             sb.XmlAttrib("rcvwinscale", rcvwinscale);
             sb.XmlAttrib("sndwinscale", sndwinscale);
             sb.XmlAttrib("seqnum", seqnum);
             sb.XmlAttrib("connid", connid);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "PID", "size", "daddr", "saddr", "dport", "sport", "mss", "sackopt", "tsopt", "wsopt", "rcvwin", "rcvwinscale", "sndwinscale", "seqnum", "connid"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return PID;
                case 1:
                    return size;
                case 2:
                    return daddr;
                case 3:
                    return saddr;
                case 4:
                    return dport;
                case 5:
                    return sport;
                case 6:
                    return mss;
                case 7:
                    return sackopt;
                case 8:
                    return tsopt;
                case 9:
                    return wsopt;
                case 10:
                    return rcvwin;
                case 11:
                    return rcvwinscale;
                case 12:
                    return sndwinscale;
                case 13:
                    return seqnum;
                case 14:
                    return connid;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<TcpIpConnectTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class TcpIpSendIPV6TraceData : TraceEvent
    {
        public int PID { get { return GetInt32At(0); } }
        public int size { get { return GetInt32At(4); } }
        // Skipping daddr
        // Skipping saddr
        public int dport { get { return GetInt16At(40); } }
        public int sport { get { return GetInt16At(42); } }
        public int startime { get { return GetInt32At(44); } }
        public int endtime { get { return GetInt32At(48); } }
        public int seqnum { get { return GetInt32At(52); } }
        public int connid { get { return GetInt32At(56); } }

        #region Private
        internal TcpIpSendIPV6TraceData(Action<TcpIpSendIPV6TraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 2 && EventDataLength != 60));
            Debug.Assert(!(Version > 2 && EventDataLength < 60));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("PID", PID);
             sb.XmlAttrib("size", size);
             sb.XmlAttrib("dport", dport);
             sb.XmlAttrib("sport", sport);
             sb.XmlAttrib("startime", startime);
             sb.XmlAttrib("endtime", endtime);
             sb.XmlAttrib("seqnum", seqnum);
             sb.XmlAttrib("connid", connid);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "PID", "size", "dport", "sport", "startime", "endtime", "seqnum", "connid"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return PID;
                case 1:
                    return size;
                case 2:
                    return dport;
                case 3:
                    return sport;
                case 4:
                    return startime;
                case 5:
                    return endtime;
                case 6:
                    return seqnum;
                case 7:
                    return connid;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<TcpIpSendIPV6TraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class TcpIpV6ConnectTraceData : TraceEvent
    {
        public int PID { get { return GetInt32At(0); } }
        public int size { get { return GetInt32At(4); } }
        // Skipping daddr
        // Skipping saddr
        public int dport { get { return GetInt16At(40); } }
        public int sport { get { return GetInt16At(42); } }
        public int mss { get { return GetInt16At(44); } }
        public int sackopt { get { return GetInt16At(46); } }
        public int tsopt { get { return GetInt16At(48); } }
        public int wsopt { get { return GetInt16At(50); } }
        public int rcvwin { get { return GetInt32At(52); } }
        public int rcvwinscale { get { return GetInt16At(56); } }
        public int sndwinscale { get { return GetInt16At(58); } }
        public int seqnum { get { return GetInt32At(60); } }
        public int connid { get { return GetInt32At(64); } }

        #region Private
        internal TcpIpV6ConnectTraceData(Action<TcpIpV6ConnectTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 2 && EventDataLength != 68));
            Debug.Assert(!(Version > 2 && EventDataLength < 68));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("PID", PID);
             sb.XmlAttrib("size", size);
             sb.XmlAttrib("dport", dport);
             sb.XmlAttrib("sport", sport);
             sb.XmlAttrib("mss", mss);
             sb.XmlAttrib("sackopt", sackopt);
             sb.XmlAttrib("tsopt", tsopt);
             sb.XmlAttrib("wsopt", wsopt);
             sb.XmlAttrib("rcvwin", rcvwin);
             sb.XmlAttrib("rcvwinscale", rcvwinscale);
             sb.XmlAttrib("sndwinscale", sndwinscale);
             sb.XmlAttrib("seqnum", seqnum);
             sb.XmlAttrib("connid", connid);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "PID", "size", "dport", "sport", "mss", "sackopt", "tsopt", "wsopt", "rcvwin", "rcvwinscale", "sndwinscale", "seqnum", "connid"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return PID;
                case 1:
                    return size;
                case 2:
                    return dport;
                case 3:
                    return sport;
                case 4:
                    return mss;
                case 5:
                    return sackopt;
                case 6:
                    return tsopt;
                case 7:
                    return wsopt;
                case 8:
                    return rcvwin;
                case 9:
                    return rcvwinscale;
                case 10:
                    return sndwinscale;
                case 11:
                    return seqnum;
                case 12:
                    return connid;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<TcpIpV6ConnectTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class UdpIpTraceData : TraceEvent
    {
        public Address context { get { return GetHostPointer(0); } }
        public int saddr { get { if (Version >= 1) return GetInt32At(12); return GetInt32At(HostOffset(4, 1)); } }
        public int sport { get { if (Version >= 1) return GetInt16At(18); return GetInt16At(HostOffset(8, 1)); } }
        public int size { get { if (Version >= 1) return GetInt32At(4); return GetInt16At(HostOffset(10, 1)); } }
        public int daddr { get { if (Version >= 1) return GetInt32At(8); return GetInt32At(HostOffset(12, 1)); } }
        public int dport { get { if (Version >= 1) return GetInt16At(16); return GetInt16At(HostOffset(16, 1)); } }
        public int dsize { get { return GetInt16At(HostOffset(18, 1)); } }
        public int PID { get { if (Version >= 1) return GetInt32At(0); return 0; } }

        #region Private
        internal UdpIpTraceData(Action<UdpIpTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(20, 1)));
            Debug.Assert(!(Version == 1 && EventDataLength != 20));
            Debug.Assert(!(Version > 1 && EventDataLength < 20));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("context", context);
             sb.XmlAttrib("saddr", saddr);
             sb.XmlAttrib("sport", sport);
             sb.XmlAttrib("size", size);
             sb.XmlAttrib("daddr", daddr);
             sb.XmlAttrib("dport", dport);
             sb.XmlAttrib("dsize", dsize);
             sb.XmlAttrib("PID", PID);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "context", "saddr", "sport", "size", "daddr", "dport", "dsize", "PID"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return context;
                case 1:
                    return saddr;
                case 2:
                    return sport;
                case 3:
                    return size;
                case 4:
                    return daddr;
                case 5:
                    return dport;
                case 6:
                    return dsize;
                case 7:
                    return PID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<UdpIpTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class UdpIpFailTraceData : TraceEvent
    {
        public int Proto { get { return GetInt16At(0); } }
        public int FailureCode { get { return GetInt16At(2); } }

        #region Private
        internal UdpIpFailTraceData(Action<UdpIpFailTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 2 && EventDataLength != 4));
            Debug.Assert(!(Version > 2 && EventDataLength < 4));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("Proto", Proto);
             sb.XmlAttrib("FailureCode", FailureCode);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Proto", "FailureCode"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Proto;
                case 1:
                    return FailureCode;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<UdpIpFailTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class UpdIpV6TraceData : TraceEvent
    {
        public int PID { get { return GetInt32At(0); } }
        public int size { get { return GetInt32At(4); } }
        // Skipping daddr
        // Skipping saddr
        public int dport { get { return GetInt16At(40); } }
        public int sport { get { return GetInt16At(42); } }
        public int seqnum { get { return GetInt32At(44); } }
        public int connid { get { return GetInt32At(48); } }

        #region Private
        internal UpdIpV6TraceData(Action<UpdIpV6TraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 2 && EventDataLength != 52));
            Debug.Assert(!(Version > 2 && EventDataLength < 52));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("PID", PID);
             sb.XmlAttrib("size", size);
             sb.XmlAttrib("dport", dport);
             sb.XmlAttrib("sport", sport);
             sb.XmlAttrib("seqnum", seqnum);
             sb.XmlAttrib("connid", connid);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "PID", "size", "dport", "sport", "seqnum", "connid"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return PID;
                case 1:
                    return size;
                case 2:
                    return dport;
                case 3:
                    return sport;
                case 4:
                    return seqnum;
                case 5:
                    return connid;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<UpdIpV6TraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class ImageLoadTraceData : TraceEvent
    {
        public Address BaseAddress { get { return GetHostPointer(0); } }
        public int ModuleSize { get { return GetInt32At(HostOffset(4, 1)); } }
        public string ImageFileName { get { return GetUnicodeStringAt(HostOffset(8, 1)); } }
        public Address ImageBase { get { if (Version >= 1) return GetHostPointer(0); return 0; } }
        public Address ImageSize { get { if (Version >= 1) return GetHostPointer(HostOffset(4, 1)); return 0; } }
        public int ProcessId { get { if (Version >= 1) return GetInt32At(HostOffset(8, 2)); return 0; } }
        public string FileName { get { if (Version >= 2) return GetUnicodeStringAt(HostOffset(44, 3)); if (Version >= 1) return GetUnicodeStringAt(HostOffset(12, 2)); return ""; } }
        public int ImageChecksum { get { if (Version >= 2) return GetInt32At(HostOffset(12, 2)); return 0; } }
        public int TimeDateStamp { get { if (Version >= 2) return GetInt32At(HostOffset(16, 2)); return 0; } }
        // Skipping Reserved0
        public Address DefaultBase { get { if (Version >= 2) return GetHostPointer(HostOffset(24, 2)); return 0; } }
        // Skipping Reserved1
        // Skipping Reserved2
        // Skipping Reserved3
        // Skipping Reserved4

        #region Private
        internal ImageLoadTraceData(Action<ImageLoadTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != SkipUnicodeString(HostOffset(8, 1))));
            Debug.Assert(!(Version == 1 && EventDataLength != SkipUnicodeString(HostOffset(12, 2))));
            Debug.Assert(!(Version == 2 && EventDataLength != SkipUnicodeString(HostOffset(44, 3))));
            Debug.Assert(!(Version > 2 && EventDataLength < SkipUnicodeString(HostOffset(44, 3))));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("BaseAddress", BaseAddress);
             sb.XmlAttrib("ModuleSize", ModuleSize);
             sb.XmlAttrib("ImageFileName", ImageFileName);
             sb.XmlAttribHex("ImageBase", ImageBase);
             sb.XmlAttribHex("ImageSize", ImageSize);
             sb.XmlAttrib("ProcessId", ProcessId);
             sb.XmlAttrib("FileName", FileName);
             sb.XmlAttrib("ImageChecksum", ImageChecksum);
             sb.XmlAttrib("TimeDateStamp", TimeDateStamp);
             sb.XmlAttribHex("DefaultBase", DefaultBase);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "BaseAddress", "ModuleSize", "ImageFileName", "ImageBase", "ImageSize", "ProcessId", "FileName", "ImageChecksum", "TimeDateStamp", "DefaultBase"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return BaseAddress;
                case 1:
                    return ModuleSize;
                case 2:
                    return ImageFileName;
                case 3:
                    return ImageBase;
                case 4:
                    return ImageSize;
                case 5:
                    return ProcessId;
                case 6:
                    return FileName;
                case 7:
                    return ImageChecksum;
                case 8:
                    return TimeDateStamp;
                case 9:
                    return DefaultBase;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ImageLoadTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class PageFaultTraceData : TraceEvent
    {
        public Address VirtualAddress { get { return GetHostPointer(0); } }
        public Address ProgramCounter { get { return GetHostPointer(HostOffset(4, 1)); } }

        #region Private
        internal PageFaultTraceData(Action<PageFaultTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(8, 2)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(8, 2)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("VirtualAddress", VirtualAddress);
             sb.XmlAttribHex("ProgramCounter", ProgramCounter);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "VirtualAddress", "ProgramCounter"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return VirtualAddress;
                case 1:
                    return ProgramCounter;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<PageFaultTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class PageFaultHardFaultTraceData : TraceEvent
    {
        public long InitialTime { get { return GetInt64At(0); } }
        public long ReadOffset { get { return GetInt64At(8); } }
        public Address VirtualAddress { get { return GetHostPointer(16); } }
        public Address FileObject { get { return GetHostPointer(HostOffset(20, 1)); } }
        public int TThreadId { get { return GetInt32At(HostOffset(24, 2)); } }
        public int ByteCount { get { return GetInt32At(HostOffset(28, 2)); } }

        #region Private
        internal PageFaultHardFaultTraceData(Action<PageFaultHardFaultTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(32, 2)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(32, 2)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("InitialTime", InitialTime);
             sb.XmlAttribHex("ReadOffset", ReadOffset);
             sb.XmlAttribHex("VirtualAddress", VirtualAddress);
             sb.XmlAttribHex("FileObject", FileObject);
             sb.XmlAttribHex("TThreadId", TThreadId);
             sb.XmlAttrib("ByteCount", ByteCount);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "InitialTime", "ReadOffset", "VirtualAddress", "FileObject", "TThreadId", "ByteCount"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return InitialTime;
                case 1:
                    return ReadOffset;
                case 2:
                    return VirtualAddress;
                case 3:
                    return FileObject;
                case 4:
                    return TThreadId;
                case 5:
                    return ByteCount;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<PageFaultHardFaultTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class PageFaultHeapRangeRundownTraceData : TraceEvent
    {
        public Address HeapHandle { get { return GetHostPointer(0); } }
        public int HRFlags { get { return GetInt32At(HostOffset(4, 1)); } }
        public int HRPid { get { return GetInt32At(HostOffset(8, 1)); } }
        public int HRRangeCount { get { return GetInt32At(HostOffset(12, 1)); } }

        #region Private
        internal PageFaultHeapRangeRundownTraceData(Action<PageFaultHeapRangeRundownTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(16, 1)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(16, 1)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("HeapHandle", HeapHandle);
             sb.XmlAttribHex("HRFlags", HRFlags);
             sb.XmlAttribHex("HRPid", HRPid);
             sb.XmlAttrib("HRRangeCount", HRRangeCount);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "HeapHandle", "HRFlags", "HRPid", "HRRangeCount"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return HeapHandle;
                case 1:
                    return HRFlags;
                case 2:
                    return HRPid;
                case 3:
                    return HRRangeCount;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<PageFaultHeapRangeRundownTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class PageFaultHeapRangeCreateTraceData : TraceEvent
    {
        public Address HeapHandle { get { return GetHostPointer(0); } }
        public Address FirstRangeSize { get { return GetHostPointer(HostOffset(4, 1)); } }
        public int HRCreateFlags { get { return GetInt32At(HostOffset(8, 2)); } }

        #region Private
        internal PageFaultHeapRangeCreateTraceData(Action<PageFaultHeapRangeCreateTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(12, 2)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(12, 2)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("HeapHandle", HeapHandle);
             sb.XmlAttribHex("FirstRangeSize", FirstRangeSize);
             sb.XmlAttribHex("HRCreateFlags", HRCreateFlags);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "HeapHandle", "FirstRangeSize", "HRCreateFlags"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return HeapHandle;
                case 1:
                    return FirstRangeSize;
                case 2:
                    return HRCreateFlags;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<PageFaultHeapRangeCreateTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class PageFaultHeapRangeTraceData : TraceEvent
    {
        public Address HeapHandle { get { return GetHostPointer(0); } }
        public Address HRAddress { get { return GetHostPointer(HostOffset(4, 1)); } }
        public Address HRSize { get { return GetHostPointer(HostOffset(8, 2)); } }

        #region Private
        internal PageFaultHeapRangeTraceData(Action<PageFaultHeapRangeTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(12, 3)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(12, 3)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("HeapHandle", HeapHandle);
             sb.XmlAttribHex("HRAddress", HRAddress);
             sb.XmlAttribHex("HRSize", HRSize);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "HeapHandle", "HRAddress", "HRSize"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return HeapHandle;
                case 1:
                    return HRAddress;
                case 2:
                    return HRSize;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<PageFaultHeapRangeTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class PageFaultHeapRangeDestroyTraceData : TraceEvent
    {
        public Address HeapHandle { get { return GetHostPointer(0); } }

        #region Private
        internal PageFaultHeapRangeDestroyTraceData(Action<PageFaultHeapRangeDestroyTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(4, 1)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(4, 1)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("HeapHandle", HeapHandle);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "HeapHandle"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return HeapHandle;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<PageFaultHeapRangeDestroyTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class PageFaultImageLoadBackedTraceData : TraceEvent
    {
        public Address FileObject { get { return GetHostPointer(0); } }
        public int DeviceChar { get { return GetInt32At(HostOffset(4, 1)); } }
        public int FileChar { get { return GetInt16At(HostOffset(8, 1)); } }
        public int LoadFlags { get { return GetInt16At(HostOffset(10, 1)); } }

        #region Private
        internal PageFaultImageLoadBackedTraceData(Action<PageFaultImageLoadBackedTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(12, 1)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(12, 1)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("FileObject", FileObject);
             sb.XmlAttribHex("DeviceChar", DeviceChar);
             sb.XmlAttribHex("FileChar", FileChar);
             sb.XmlAttribHex("LoadFlags", LoadFlags);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "FileObject", "DeviceChar", "FileChar", "LoadFlags"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return FileObject;
                case 1:
                    return DeviceChar;
                case 2:
                    return FileChar;
                case 3:
                    return LoadFlags;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<PageFaultImageLoadBackedTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class SampledProfileTraceData : TraceEvent
    {
        public Address InstructionPointer { get { return GetHostPointer(0); } }
        public int ThreadId { get { return GetInt32At(HostOffset(4, 1)); } }
        public int Count { get { return GetInt32At(HostOffset(8, 1)); } }

        #region Private
        internal SampledProfileTraceData(Action<SampledProfileTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 2 && EventDataLength != HostOffset(12, 1)));
            Debug.Assert(!(Version > 2 && EventDataLength < HostOffset(12, 1)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("InstructionPointer", InstructionPointer);
             sb.XmlAttrib("ThreadId", ThreadId);
             sb.XmlAttrib("Count", Count);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "InstructionPointer", "ThreadId", "Count"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return InstructionPointer;
                case 1:
                    return ThreadId;
                case 2:
                    return Count;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<SampledProfileTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class SampledProfileIntervalTraceData : TraceEvent
    {
        public int Source { get { return GetInt32At(0); } }
        public int NewInterval { get { return GetInt32At(4); } }
        public int OldInterval { get { return GetInt32At(8); } }

        #region Private
        internal SampledProfileIntervalTraceData(Action<SampledProfileIntervalTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 2 && EventDataLength != 12));
            Debug.Assert(!(Version > 2 && EventDataLength < 12));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("Source", Source);
             sb.XmlAttrib("NewInterval", NewInterval);
             sb.XmlAttrib("OldInterval", OldInterval);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Source", "NewInterval", "OldInterval"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Source;
                case 1:
                    return NewInterval;
                case 2:
                    return OldInterval;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<SampledProfileIntervalTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class SysCallEnterTraceData : TraceEvent
    {
        public Address SysCallAddress { get { return GetHostPointer(0); } }

        #region Private
        internal SysCallEnterTraceData(Action<SysCallEnterTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 2 && EventDataLength != HostOffset(4, 1)));
            Debug.Assert(!(Version > 2 && EventDataLength < HostOffset(4, 1)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("SysCallAddress", SysCallAddress);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "SysCallAddress"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return SysCallAddress;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<SysCallEnterTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class SysCallExitTraceData : TraceEvent
    {
        public int SysCallNtStatus { get { return GetInt32At(0); } }

        #region Private
        internal SysCallExitTraceData(Action<SysCallExitTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 2 && EventDataLength != 4));
            Debug.Assert(!(Version > 2 && EventDataLength < 4));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("SysCallNtStatus", SysCallNtStatus);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "SysCallNtStatus"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return SysCallNtStatus;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<SysCallExitTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class ISRTraceData : TraceEvent
    {
        public long InitialTime { get { return GetInt64At(0); } }
        public Address Routine { get { return GetHostPointer(8); } }
        public int ReturnValue { get { return GetByteAt(HostOffset(12, 1)); } }
        public int Vector { get { return GetByteAt(HostOffset(13, 1)); } }
        // Skipping Reserved

        #region Private
        internal ISRTraceData(Action<ISRTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 2 && EventDataLength != HostOffset(16, 1)));
            Debug.Assert(!(Version > 2 && EventDataLength < HostOffset(16, 1)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("InitialTime", InitialTime);
             sb.XmlAttribHex("Routine", Routine);
             sb.XmlAttrib("ReturnValue", ReturnValue);
             sb.XmlAttrib("Vector", Vector);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "InitialTime", "Routine", "ReturnValue", "Vector"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return InitialTime;
                case 1:
                    return Routine;
                case 2:
                    return ReturnValue;
                case 3:
                    return Vector;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ISRTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class DPCTraceData : TraceEvent
    {
        public long InitialTime { get { return GetInt64At(0); } }
        public Address Routine { get { return GetHostPointer(8); } }

        #region Private
        internal DPCTraceData(Action<DPCTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 2 && EventDataLength != HostOffset(12, 1)));
            Debug.Assert(!(Version > 2 && EventDataLength < HostOffset(12, 1)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("InitialTime", InitialTime);
             sb.XmlAttribHex("Routine", Routine);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "InitialTime", "Routine"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return InitialTime;
                case 1:
                    return Routine;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<DPCTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class StackWalkTraceData : TraceEvent
    {
        public long EventTimeStamp { get { return GetInt64At(0); } }
        public int StackProcess { get { return GetInt32At(8); } }
        public int StackThread { get { return GetInt32At(12); } }
        public Address Stack1 { get { return GetHostPointer(16); } }
        public Address Stack2 { get { return GetHostPointer(HostOffset(20, 1)); } }
        public Address Stack3 { get { return GetHostPointer(HostOffset(24, 2)); } }
        public Address Stack4 { get { return GetHostPointer(HostOffset(28, 3)); } }
        public Address Stack5 { get { return GetHostPointer(HostOffset(32, 4)); } }
        public Address Stack6 { get { return GetHostPointer(HostOffset(36, 5)); } }
        public Address Stack7 { get { return GetHostPointer(HostOffset(40, 6)); } }
        public Address Stack8 { get { return GetHostPointer(HostOffset(44, 7)); } }
        public Address Stack9 { get { return GetHostPointer(HostOffset(48, 8)); } }
        public Address Stack10 { get { return GetHostPointer(HostOffset(52, 9)); } }
        public Address Stack11 { get { return GetHostPointer(HostOffset(56, 10)); } }
        public Address Stack12 { get { return GetHostPointer(HostOffset(60, 11)); } }
        public Address Stack13 { get { return GetHostPointer(HostOffset(64, 12)); } }
        public Address Stack14 { get { return GetHostPointer(HostOffset(68, 13)); } }
        public Address Stack15 { get { return GetHostPointer(HostOffset(72, 14)); } }
        public Address Stack16 { get { return GetHostPointer(HostOffset(76, 15)); } }
        public Address Stack17 { get { return GetHostPointer(HostOffset(80, 16)); } }
        public Address Stack18 { get { return GetHostPointer(HostOffset(84, 17)); } }
        public Address Stack19 { get { return GetHostPointer(HostOffset(88, 18)); } }
        public Address Stack20 { get { return GetHostPointer(HostOffset(92, 19)); } }
        public Address Stack21 { get { return GetHostPointer(HostOffset(96, 20)); } }
        public Address Stack22 { get { return GetHostPointer(HostOffset(100, 21)); } }
        public Address Stack23 { get { return GetHostPointer(HostOffset(104, 22)); } }
        public Address Stack24 { get { return GetHostPointer(HostOffset(108, 23)); } }
        public Address Stack25 { get { return GetHostPointer(HostOffset(112, 24)); } }
        public Address Stack26 { get { return GetHostPointer(HostOffset(116, 25)); } }
        public Address Stack27 { get { return GetHostPointer(HostOffset(120, 26)); } }
        public Address Stack28 { get { return GetHostPointer(HostOffset(124, 27)); } }
        public Address Stack29 { get { return GetHostPointer(HostOffset(128, 28)); } }
        public Address Stack30 { get { return GetHostPointer(HostOffset(132, 29)); } }
        public Address Stack31 { get { return GetHostPointer(HostOffset(136, 30)); } }
        public Address Stack32 { get { return GetHostPointer(HostOffset(140, 31)); } }

        #region Private
        internal StackWalkTraceData(Action<StackWalkTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(144, 32)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(144, 32)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("EventTimeStamp", EventTimeStamp);
             sb.XmlAttribHex("StackProcess", StackProcess);
             sb.XmlAttrib("StackThread", StackThread);
             sb.XmlAttribHex("Stack1", Stack1);
             sb.XmlAttribHex("Stack2", Stack2);
             sb.XmlAttribHex("Stack3", Stack3);
             sb.XmlAttribHex("Stack4", Stack4);
             sb.XmlAttribHex("Stack5", Stack5);
             sb.XmlAttribHex("Stack6", Stack6);
             sb.XmlAttribHex("Stack7", Stack7);
             sb.XmlAttribHex("Stack8", Stack8);
             sb.XmlAttribHex("Stack9", Stack9);
             sb.XmlAttribHex("Stack10", Stack10);
             sb.XmlAttribHex("Stack11", Stack11);
             sb.XmlAttribHex("Stack12", Stack12);
             sb.XmlAttribHex("Stack13", Stack13);
             sb.XmlAttribHex("Stack14", Stack14);
             sb.XmlAttribHex("Stack15", Stack15);
             sb.XmlAttribHex("Stack16", Stack16);
             sb.XmlAttribHex("Stack17", Stack17);
             sb.XmlAttribHex("Stack18", Stack18);
             sb.XmlAttribHex("Stack19", Stack19);
             sb.XmlAttribHex("Stack20", Stack20);
             sb.XmlAttribHex("Stack21", Stack21);
             sb.XmlAttribHex("Stack22", Stack22);
             sb.XmlAttribHex("Stack23", Stack23);
             sb.XmlAttribHex("Stack24", Stack24);
             sb.XmlAttribHex("Stack25", Stack25);
             sb.XmlAttribHex("Stack26", Stack26);
             sb.XmlAttribHex("Stack27", Stack27);
             sb.XmlAttribHex("Stack28", Stack28);
             sb.XmlAttribHex("Stack29", Stack29);
             sb.XmlAttribHex("Stack30", Stack30);
             sb.XmlAttribHex("Stack31", Stack31);
             sb.XmlAttribHex("Stack32", Stack32);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "EventTimeStamp", "StackProcess", "StackThread", "Stack1", "Stack2", "Stack3", "Stack4", "Stack5", "Stack6", "Stack7", "Stack8", "Stack9", "Stack10", "Stack11", "Stack12", "Stack13", "Stack14", "Stack15", "Stack16", "Stack17", "Stack18", "Stack19", "Stack20", "Stack21", "Stack22", "Stack23", "Stack24", "Stack25", "Stack26", "Stack27", "Stack28", "Stack29", "Stack30", "Stack31", "Stack32"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return EventTimeStamp;
                case 1:
                    return StackProcess;
                case 2:
                    return StackThread;
                case 3:
                    return Stack1;
                case 4:
                    return Stack2;
                case 5:
                    return Stack3;
                case 6:
                    return Stack4;
                case 7:
                    return Stack5;
                case 8:
                    return Stack6;
                case 9:
                    return Stack7;
                case 10:
                    return Stack8;
                case 11:
                    return Stack9;
                case 12:
                    return Stack10;
                case 13:
                    return Stack11;
                case 14:
                    return Stack12;
                case 15:
                    return Stack13;
                case 16:
                    return Stack14;
                case 17:
                    return Stack15;
                case 18:
                    return Stack16;
                case 19:
                    return Stack17;
                case 20:
                    return Stack18;
                case 21:
                    return Stack19;
                case 22:
                    return Stack20;
                case 23:
                    return Stack21;
                case 24:
                    return Stack22;
                case 25:
                    return Stack23;
                case 26:
                    return Stack24;
                case 27:
                    return Stack25;
                case 28:
                    return Stack26;
                case 29:
                    return Stack27;
                case 30:
                    return Stack28;
                case 31:
                    return Stack29;
                case 32:
                    return Stack30;
                case 33:
                    return Stack31;
                case 34:
                    return Stack32;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<StackWalkTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class ALPCSendMessageTraceData : TraceEvent
    {
        public int MessageID { get { return GetInt32At(0); } }

        #region Private
        internal ALPCSendMessageTraceData(Action<ALPCSendMessageTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != 4));
            Debug.Assert(!(Version > 0 && EventDataLength < 4));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("MessageID", MessageID);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "MessageID"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return MessageID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ALPCSendMessageTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class ALPCReceiveMessageTraceData : TraceEvent
    {
        public int MessageID { get { return GetInt32At(0); } }

        #region Private
        internal ALPCReceiveMessageTraceData(Action<ALPCReceiveMessageTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != 4));
            Debug.Assert(!(Version > 0 && EventDataLength < 4));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("MessageID", MessageID);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "MessageID"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return MessageID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ALPCReceiveMessageTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class ALPCWaitForReplyTraceData : TraceEvent
    {
        public int MessageID { get { return GetInt32At(0); } }

        #region Private
        internal ALPCWaitForReplyTraceData(Action<ALPCWaitForReplyTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != 4));
            Debug.Assert(!(Version > 0 && EventDataLength < 4));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("MessageID", MessageID);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "MessageID"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return MessageID;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ALPCWaitForReplyTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class ALPCWaitForNewMessageTraceData : TraceEvent
    {
        public int IsServerPort { get { return GetInt32At(0); } }
        public string PortName { get { return GetAsciiStringAt(4); } }

        #region Private
        internal ALPCWaitForNewMessageTraceData(Action<ALPCWaitForNewMessageTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != SkipAsciiString(4)));
            Debug.Assert(!(Version > 0 && EventDataLength < SkipAsciiString(4)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("IsServerPort", IsServerPort);
             sb.XmlAttrib("PortName", PortName);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "IsServerPort", "PortName"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return IsServerPort;
                case 1:
                    return PortName;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ALPCWaitForNewMessageTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class ALPCUnwaitTraceData : TraceEvent
    {
        public int Status { get { return GetInt32At(0); } }

        #region Private
        internal ALPCUnwaitTraceData(Action<ALPCUnwaitTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != 4));
            Debug.Assert(!(Version > 0 && EventDataLength < 4));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("Status", Status);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "Status"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return Status;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<ALPCUnwaitTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class SystemConfigCPUTraceData : TraceEvent
    {
        public int MHz { get { return GetInt32At(0); } }
        public int NumberOfProcessors { get { return GetInt32At(4); } }
        public int MemSize { get { return GetInt32At(8); } }
        public int PageSize { get { return GetInt32At(12); } }
        public int AllocationGranularity { get { return GetInt32At(16); } }
        public string ComputerName { get { return GetFixedUnicodeStringAt(256, (20)); } }
        public string DomainName { get { if (Version >= 2) return GetFixedUnicodeStringAt(134, (532)); return GetFixedUnicodeStringAt(132, (532)); } }
        public Address HyperThreadingFlag { get { if (Version >= 2) return GetHostPointer(800); return GetHostPointer(796); } }

        #region Private
        internal SystemConfigCPUTraceData(Action<SystemConfigCPUTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(800, 1)));
            Debug.Assert(!(Version == 1 && EventDataLength != HostOffset(800, 1)));
            Debug.Assert(!(Version == 2 && EventDataLength != HostOffset(804, 1)));
            Debug.Assert(!(Version > 2 && EventDataLength < HostOffset(804, 1)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("MHz", MHz);
             sb.XmlAttrib("NumberOfProcessors", NumberOfProcessors);
             sb.XmlAttrib("MemSize", MemSize);
             sb.XmlAttrib("PageSize", PageSize);
             sb.XmlAttrib("AllocationGranularity", AllocationGranularity);
             sb.XmlAttrib("ComputerName", ComputerName);
             sb.XmlAttrib("DomainName", DomainName);
             sb.XmlAttribHex("HyperThreadingFlag", HyperThreadingFlag);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "MHz", "NumberOfProcessors", "MemSize", "PageSize", "AllocationGranularity", "ComputerName", "DomainName", "HyperThreadingFlag"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return MHz;
                case 1:
                    return NumberOfProcessors;
                case 2:
                    return MemSize;
                case 3:
                    return PageSize;
                case 4:
                    return AllocationGranularity;
                case 5:
                    return ComputerName;
                case 6:
                    return DomainName;
                case 7:
                    return HyperThreadingFlag;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<SystemConfigCPUTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class SystemConfigPhyDiskTraceData : TraceEvent
    {
        public int DiskNumber { get { return GetInt32At(0); } }
        public int BytesPerSector { get { return GetInt32At(4); } }
        public int SectorsPerTrack { get { return GetInt32At(8); } }
        public int TracksPerCylinder { get { return GetInt32At(12); } }
        public long Cylinders { get { return GetInt64At(16); } }
        public int SCSIPort { get { return GetInt32At(24); } }
        public int SCSIPath { get { return GetInt32At(28); } }
        public int SCSITarget { get { return GetInt32At(32); } }
        public int SCSILun { get { return GetInt32At(36); } }
        public string Manufacturer { get { return GetFixedUnicodeStringAt(256, (40)); } }
        public int PartitionCount { get { return GetInt32At(552); } }
        public int WriteCacheEnabled { get { return GetByteAt(556); } }
        // Skipping Pad
        public string BootDriveLetter { get { return GetFixedUnicodeStringAt(3, (558)); } }
        public string Spare { get { return GetFixedUnicodeStringAt(2, (564)); } }

        #region Private
        internal SystemConfigPhyDiskTraceData(Action<SystemConfigPhyDiskTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != 568));
            Debug.Assert(!(Version == 1 && EventDataLength != 568));
            Debug.Assert(!(Version == 2 && EventDataLength != 568));
            Debug.Assert(!(Version > 2 && EventDataLength < 568));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("DiskNumber", DiskNumber);
             sb.XmlAttrib("BytesPerSector", BytesPerSector);
             sb.XmlAttrib("SectorsPerTrack", SectorsPerTrack);
             sb.XmlAttrib("TracksPerCylinder", TracksPerCylinder);
             sb.XmlAttrib("Cylinders", Cylinders);
             sb.XmlAttrib("SCSIPort", SCSIPort);
             sb.XmlAttrib("SCSIPath", SCSIPath);
             sb.XmlAttrib("SCSITarget", SCSITarget);
             sb.XmlAttrib("SCSILun", SCSILun);
             sb.XmlAttrib("Manufacturer", Manufacturer);
             sb.XmlAttrib("PartitionCount", PartitionCount);
             sb.XmlAttrib("WriteCacheEnabled", WriteCacheEnabled);
             sb.XmlAttrib("BootDriveLetter", BootDriveLetter);
             sb.XmlAttrib("Spare", Spare);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "DiskNumber", "BytesPerSector", "SectorsPerTrack", "TracksPerCylinder", "Cylinders", "SCSIPort", "SCSIPath", "SCSITarget", "SCSILun", "Manufacturer", "PartitionCount", "WriteCacheEnabled", "BootDriveLetter", "Spare"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return DiskNumber;
                case 1:
                    return BytesPerSector;
                case 2:
                    return SectorsPerTrack;
                case 3:
                    return TracksPerCylinder;
                case 4:
                    return Cylinders;
                case 5:
                    return SCSIPort;
                case 6:
                    return SCSIPath;
                case 7:
                    return SCSITarget;
                case 8:
                    return SCSILun;
                case 9:
                    return Manufacturer;
                case 10:
                    return PartitionCount;
                case 11:
                    return WriteCacheEnabled;
                case 12:
                    return BootDriveLetter;
                case 13:
                    return Spare;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<SystemConfigPhyDiskTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class SystemConfigLogDiskTraceData : TraceEvent
    {
        public long StartOffset { get { return GetInt64At(0); } }
        public long PartitionSize { get { return GetInt64At(8); } }
        public int DiskNumber { get { return GetInt32At(16); } }
        public int Size { get { return GetInt32At(20); } }
        public int DriveType { get { return GetInt32At(24); } }
        public string DriveLetterString { get { return GetFixedUnicodeStringAt(4, (28)); } }
        // Skipping Pad1
        public int PartitionNumber { get { return GetInt32At(40); } }
        public int SectorsPerCluster { get { return GetInt32At(44); } }
        public int BytesPerSector { get { return GetInt32At(48); } }
        // Skipping Pad2
        public long NumberOfFreeClusters { get { return GetInt64At(56); } }
        public long TotalNumberOfClusters { get { return GetInt64At(64); } }
        public string FileSystem { get { return GetFixedUnicodeStringAt(16, (72)); } }
        public int VolumeExt { get { return GetInt32At(104); } }
        // Skipping Pad3

        #region Private
        internal SystemConfigLogDiskTraceData(Action<SystemConfigLogDiskTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != 108));
            Debug.Assert(!(Version == 1 && EventDataLength != 108));
            Debug.Assert(!(Version == 2 && EventDataLength != 112));
            Debug.Assert(!(Version > 2 && EventDataLength < 112));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("StartOffset", StartOffset);
             sb.XmlAttrib("PartitionSize", PartitionSize);
             sb.XmlAttrib("DiskNumber", DiskNumber);
             sb.XmlAttrib("Size", Size);
             sb.XmlAttrib("DriveType", DriveType);
             sb.XmlAttrib("DriveLetterString", DriveLetterString);
             sb.XmlAttrib("PartitionNumber", PartitionNumber);
             sb.XmlAttrib("SectorsPerCluster", SectorsPerCluster);
             sb.XmlAttrib("BytesPerSector", BytesPerSector);
             sb.XmlAttrib("NumberOfFreeClusters", NumberOfFreeClusters);
             sb.XmlAttrib("TotalNumberOfClusters", TotalNumberOfClusters);
             sb.XmlAttrib("FileSystem", FileSystem);
             sb.XmlAttrib("VolumeExt", VolumeExt);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "StartOffset", "PartitionSize", "DiskNumber", "Size", "DriveType", "DriveLetterString", "PartitionNumber", "SectorsPerCluster", "BytesPerSector", "NumberOfFreeClusters", "TotalNumberOfClusters", "FileSystem", "VolumeExt"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return StartOffset;
                case 1:
                    return PartitionSize;
                case 2:
                    return DiskNumber;
                case 3:
                    return Size;
                case 4:
                    return DriveType;
                case 5:
                    return DriveLetterString;
                case 6:
                    return PartitionNumber;
                case 7:
                    return SectorsPerCluster;
                case 8:
                    return BytesPerSector;
                case 9:
                    return NumberOfFreeClusters;
                case 10:
                    return TotalNumberOfClusters;
                case 11:
                    return FileSystem;
                case 12:
                    return VolumeExt;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<SystemConfigLogDiskTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class SystemConfigNICTraceData : TraceEvent
    {
        public string NICName { get { return GetFixedUnicodeStringAt(256, (0)); } }
        public int Index { get { return GetInt32At(512); } }
        public int PhysicalAddrLen { get { if (Version >= 2) return GetInt32At(8); return GetInt32At(516); } }
        public BAD_MERGE_OF_long_AND_string PhysicalAddr { get { if (Version >= 2) return GetInt64At(0); return GetFixedUnicodeStringAt(8, (520)); } }
        public int Size { get { return GetInt32At(536); } }
        public int IpAddress { get { return GetInt32At(540); } }
        public int SubnetMask { get { return GetInt32At(544); } }
        public int DhcpServer { get { return GetInt32At(548); } }
        public int Gateway { get { return GetInt32At(552); } }
        public int PrimaryWinsServer { get { return GetInt32At(556); } }
        public int SecondaryWinsServer { get { return GetInt32At(560); } }
        public int DnsServer1 { get { return GetInt32At(564); } }
        public int DnsServer2 { get { return GetInt32At(568); } }
        public int DnsServer3 { get { return GetInt32At(572); } }
        public int DnsServer4 { get { return GetInt32At(576); } }
        public int Data { get { return GetInt32At(580); } }
        public int Ipv4Index { get { if (Version >= 2) return GetInt32At(12); return 0; } }
        public int Ipv6Index { get { if (Version >= 2) return GetInt32At(16); return 0; } }
        public string NICDescription { get { if (Version >= 2) return GetUnicodeStringAt(20); return ""; } }
        public string IpAddresses { get { if (Version >= 2) return GetUnicodeStringAt(SkipUnicodeString(20)); return ""; } }
        public string DnsServerAddresses { get { if (Version >= 2) return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(20))); return ""; } }

        #region Private
        internal SystemConfigNICTraceData(Action<SystemConfigNICTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != 584));
            Debug.Assert(!(Version == 1 && EventDataLength != 584));
            Debug.Assert(!(Version == 2 && EventDataLength != SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(20)))));
            Debug.Assert(!(Version > 2 && EventDataLength < SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(20)))));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("NICName", NICName);
             sb.XmlAttrib("Index", Index);
             sb.XmlAttrib("PhysicalAddrLen", PhysicalAddrLen);
             sb.XmlAttrib("PhysicalAddr", PhysicalAddr);
             sb.XmlAttrib("Size", Size);
             sb.XmlAttrib("IpAddress", IpAddress);
             sb.XmlAttrib("SubnetMask", SubnetMask);
             sb.XmlAttrib("DhcpServer", DhcpServer);
             sb.XmlAttrib("Gateway", Gateway);
             sb.XmlAttrib("PrimaryWinsServer", PrimaryWinsServer);
             sb.XmlAttrib("SecondaryWinsServer", SecondaryWinsServer);
             sb.XmlAttrib("DnsServer1", DnsServer1);
             sb.XmlAttrib("DnsServer2", DnsServer2);
             sb.XmlAttrib("DnsServer3", DnsServer3);
             sb.XmlAttrib("DnsServer4", DnsServer4);
             sb.XmlAttrib("Data", Data);
             sb.XmlAttrib("Ipv4Index", Ipv4Index);
             sb.XmlAttrib("Ipv6Index", Ipv6Index);
             sb.XmlAttrib("NICDescription", NICDescription);
             sb.XmlAttrib("IpAddresses", IpAddresses);
             sb.XmlAttrib("DnsServerAddresses", DnsServerAddresses);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "NICName", "Index", "PhysicalAddrLen", "PhysicalAddr", "Size", "IpAddress", "SubnetMask", "DhcpServer", "Gateway", "PrimaryWinsServer", "SecondaryWinsServer", "DnsServer1", "DnsServer2", "DnsServer3", "DnsServer4", "Data", "Ipv4Index", "Ipv6Index", "NICDescription", "IpAddresses", "DnsServerAddresses"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return NICName;
                case 1:
                    return Index;
                case 2:
                    return PhysicalAddrLen;
                case 3:
                    return PhysicalAddr;
                case 4:
                    return Size;
                case 5:
                    return IpAddress;
                case 6:
                    return SubnetMask;
                case 7:
                    return DhcpServer;
                case 8:
                    return Gateway;
                case 9:
                    return PrimaryWinsServer;
                case 10:
                    return SecondaryWinsServer;
                case 11:
                    return DnsServer1;
                case 12:
                    return DnsServer2;
                case 13:
                    return DnsServer3;
                case 14:
                    return DnsServer4;
                case 15:
                    return Data;
                case 16:
                    return Ipv4Index;
                case 17:
                    return Ipv6Index;
                case 18:
                    return NICDescription;
                case 19:
                    return IpAddresses;
                case 20:
                    return DnsServerAddresses;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<SystemConfigNICTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class SystemConfigVideoTraceData : TraceEvent
    {
        public int MemorySize { get { return GetInt32At(0); } }
        public int XResolution { get { return GetInt32At(4); } }
        public int YResolution { get { return GetInt32At(8); } }
        public int BitsPerPixel { get { return GetInt32At(12); } }
        public int VRefresh { get { return GetInt32At(16); } }
        public string ChipType { get { return GetFixedUnicodeStringAt(256, (20)); } }
        public string DACType { get { return GetFixedUnicodeStringAt(256, (532)); } }
        public string AdapterString { get { return GetFixedUnicodeStringAt(256, (1044)); } }
        public string BiosString { get { return GetFixedUnicodeStringAt(256, (1556)); } }
        public string DeviceId { get { return GetFixedUnicodeStringAt(256, (2068)); } }
        public int StateFlags { get { return GetInt32At(2580); } }

        #region Private
        internal SystemConfigVideoTraceData(Action<SystemConfigVideoTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != 2584));
            Debug.Assert(!(Version == 1 && EventDataLength != 2584));
            Debug.Assert(!(Version == 2 && EventDataLength != 2584));
            Debug.Assert(!(Version > 2 && EventDataLength < 2584));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("MemorySize", MemorySize);
             sb.XmlAttrib("XResolution", XResolution);
             sb.XmlAttrib("YResolution", YResolution);
             sb.XmlAttrib("BitsPerPixel", BitsPerPixel);
             sb.XmlAttrib("VRefresh", VRefresh);
             sb.XmlAttrib("ChipType", ChipType);
             sb.XmlAttrib("DACType", DACType);
             sb.XmlAttrib("AdapterString", AdapterString);
             sb.XmlAttrib("BiosString", BiosString);
             sb.XmlAttrib("DeviceId", DeviceId);
             sb.XmlAttribHex("StateFlags", StateFlags);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "MemorySize", "XResolution", "YResolution", "BitsPerPixel", "VRefresh", "ChipType", "DACType", "AdapterString", "BiosString", "DeviceId", "StateFlags"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return MemorySize;
                case 1:
                    return XResolution;
                case 2:
                    return YResolution;
                case 3:
                    return BitsPerPixel;
                case 4:
                    return VRefresh;
                case 5:
                    return ChipType;
                case 6:
                    return DACType;
                case 7:
                    return AdapterString;
                case 8:
                    return BiosString;
                case 9:
                    return DeviceId;
                case 10:
                    return StateFlags;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<SystemConfigVideoTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class SystemConfigServicesTraceData : TraceEvent
    {
        public string ServiceName { get { if (Version >= 2) return GetUnicodeStringAt(12); return GetFixedUnicodeStringAt(34, (0)); } }
        public string DisplayName { get { if (Version >= 2) return GetUnicodeStringAt(SkipUnicodeString(12)); return GetFixedUnicodeStringAt(256, (68)); } }
        public string ProcessName { get { if (Version >= 2) return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(12))); return GetFixedUnicodeStringAt(34, (580)); } }
        public int ProcessId { get { if (Version >= 2) return GetInt32At(0); return GetInt32At(648); } }
        public int ServiceState { get { if (Version >= 2) return GetInt32At(4); return 0; } }
        public int SubProcessTag { get { if (Version >= 2) return GetInt32At(8); return 0; } }

        #region Private
        internal SystemConfigServicesTraceData(Action<SystemConfigServicesTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != 652));
            Debug.Assert(!(Version == 1 && EventDataLength != 652));
            Debug.Assert(!(Version == 2 && EventDataLength != SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(12)))));
            Debug.Assert(!(Version > 2 && EventDataLength < SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(12)))));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("ServiceName", ServiceName);
             sb.XmlAttrib("DisplayName", DisplayName);
             sb.XmlAttrib("ProcessName", ProcessName);
             sb.XmlAttrib("ProcessId", ProcessId);
             sb.XmlAttribHex("ServiceState", ServiceState);
             sb.XmlAttribHex("SubProcessTag", SubProcessTag);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "ServiceName", "DisplayName", "ProcessName", "ProcessId", "ServiceState", "SubProcessTag"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return ServiceName;
                case 1:
                    return DisplayName;
                case 2:
                    return ProcessName;
                case 3:
                    return ProcessId;
                case 4:
                    return ServiceState;
                case 5:
                    return SubProcessTag;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<SystemConfigServicesTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class SystemConfigPowerTraceData : TraceEvent
    {
        public int S1 { get { return GetByteAt(0); } }
        public int S2 { get { return GetByteAt(1); } }
        public int S3 { get { return GetByteAt(2); } }
        public int S4 { get { return GetByteAt(3); } }
        public int S5 { get { return GetByteAt(4); } }
        // Skipping Pad1
        // Skipping Pad2
        // Skipping Pad3

        #region Private
        internal SystemConfigPowerTraceData(Action<SystemConfigPowerTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != 8));
            Debug.Assert(!(Version == 1 && EventDataLength != 8));
            Debug.Assert(!(Version == 2 && EventDataLength != 8));
            Debug.Assert(!(Version > 2 && EventDataLength < 8));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("S1", S1);
             sb.XmlAttrib("S2", S2);
             sb.XmlAttrib("S3", S3);
             sb.XmlAttrib("S4", S4);
             sb.XmlAttrib("S5", S5);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "S1", "S2", "S3", "S4", "S5"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return S1;
                case 1:
                    return S2;
                case 2:
                    return S3;
                case 3:
                    return S4;
                case 4:
                    return S5;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<SystemConfigPowerTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class SystemConfigIRQTraceData : TraceEvent
    {
        public long IRQAffinity { get { return GetInt64At(0); } }
        public int IRQNum { get { return GetInt32At(8); } }
        public int DeviceDescriptionLen { get { return GetInt32At(12); } }
        public string DeviceDescription { get { return GetUnicodeStringAt(16); } }

        #region Private
        internal SystemConfigIRQTraceData(Action<SystemConfigIRQTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != SkipUnicodeString(16)));
            Debug.Assert(!(Version == 1 && EventDataLength != SkipUnicodeString(16)));
            Debug.Assert(!(Version == 2 && EventDataLength != SkipUnicodeString(16)));
            Debug.Assert(!(Version > 2 && EventDataLength < SkipUnicodeString(16)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("IRQAffinity", IRQAffinity);
             sb.XmlAttrib("IRQNum", IRQNum);
             sb.XmlAttrib("DeviceDescriptionLen", DeviceDescriptionLen);
             sb.XmlAttrib("DeviceDescription", DeviceDescription);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "IRQAffinity", "IRQNum", "DeviceDescriptionLen", "DeviceDescription"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return IRQAffinity;
                case 1:
                    return IRQNum;
                case 2:
                    return DeviceDescriptionLen;
                case 3:
                    return DeviceDescription;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<SystemConfigIRQTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class SystemConfigPnPTraceData : TraceEvent
    {
        public int IDLength { get { return GetInt32At(0); } }
        public int DescriptionLength { get { return GetInt32At(4); } }
        public int FriendlyNameLength { get { return GetInt32At(8); } }
        public string DeviceID { get { return GetUnicodeStringAt(12); } }
        public string DeviceDescription { get { return GetUnicodeStringAt(SkipUnicodeString(12)); } }
        public string FriendlyName { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(12))); } }

        #region Private
        internal SystemConfigPnPTraceData(Action<SystemConfigPnPTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(12)))));
            Debug.Assert(!(Version == 1 && EventDataLength != SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(12)))));
            Debug.Assert(!(Version == 2 && EventDataLength != SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(12)))));
            Debug.Assert(!(Version > 2 && EventDataLength < SkipUnicodeString(SkipUnicodeString(SkipUnicodeString(12)))));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("IDLength", IDLength);
             sb.XmlAttrib("DescriptionLength", DescriptionLength);
             sb.XmlAttrib("FriendlyNameLength", FriendlyNameLength);
             sb.XmlAttrib("DeviceID", DeviceID);
             sb.XmlAttrib("DeviceDescription", DeviceDescription);
             sb.XmlAttrib("FriendlyName", FriendlyName);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "IDLength", "DescriptionLength", "FriendlyNameLength", "DeviceID", "DeviceDescription", "FriendlyName"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return IDLength;
                case 1:
                    return DescriptionLength;
                case 2:
                    return FriendlyNameLength;
                case 3:
                    return DeviceID;
                case 4:
                    return DeviceDescription;
                case 5:
                    return FriendlyName;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<SystemConfigPnPTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class SystemConfigNetworkTraceData : TraceEvent
    {
        public int TcbTablePartitions { get { return GetInt32At(0); } }
        public int MaxHashTableSize { get { return GetInt32At(4); } }
        public int MaxUserPort { get { return GetInt32At(8); } }
        public int TcpTimedWaitDelay { get { return GetInt32At(12); } }

        #region Private
        internal SystemConfigNetworkTraceData(Action<SystemConfigNetworkTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 2 && EventDataLength != 16));
            Debug.Assert(!(Version > 2 && EventDataLength < 16));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("TcbTablePartitions", TcbTablePartitions);
             sb.XmlAttrib("MaxHashTableSize", MaxHashTableSize);
             sb.XmlAttrib("MaxUserPort", MaxUserPort);
             sb.XmlAttrib("TcpTimedWaitDelay", TcpTimedWaitDelay);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "TcbTablePartitions", "MaxHashTableSize", "MaxUserPort", "TcpTimedWaitDelay"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return TcbTablePartitions;
                case 1:
                    return MaxHashTableSize;
                case 2:
                    return MaxUserPort;
                case 3:
                    return TcpTimedWaitDelay;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<SystemConfigNetworkTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }
    public sealed class SystemConfigIDEChannelTraceData : TraceEvent
    {
        public int TargetId { get { return GetInt32At(0); } }
        public int DeviceType { get { return GetInt32At(4); } }
        public int DeviceTimingMode { get { return GetInt32At(8); } }
        public int LocationInformationLen { get { return GetInt32At(12); } }
        public string LocationInformation { get { return GetUnicodeStringAt(16); } }

        #region Private
        internal SystemConfigIDEChannelTraceData(Action<SystemConfigIDEChannelTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, KernelTraceEventParserState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 2 && EventDataLength != SkipUnicodeString(16)));
            Debug.Assert(!(Version > 2 && EventDataLength < SkipUnicodeString(16)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("TargetId", TargetId);
             sb.XmlAttribHex("DeviceType", DeviceType);
             sb.XmlAttribHex("DeviceTimingMode", DeviceTimingMode);
             sb.XmlAttrib("LocationInformationLen", LocationInformationLen);
             sb.XmlAttrib("LocationInformation", LocationInformation);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "TargetId", "DeviceType", "DeviceTimingMode", "LocationInformationLen", "LocationInformation"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return TargetId;
                case 1:
                    return DeviceType;
                case 2:
                    return DeviceTimingMode;
                case 3:
                    return LocationInformationLen;
                case 4:
                    return LocationInformation;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<SystemConfigIDEChannelTraceData> Action;
        private KernelTraceEventParserState state;
        #endregion
    }

    public sealed class ThreadPoolTraceEventParser : TraceEventParser 
    {
        public static string ProviderName = "ThreadPool";
        public static Guid ProviderGuid = new Guid(0xc861d0e2, 0xa2c1, 0x4d36, 0x9f, 0x9c, 0x97, 0x0b, 0xab, 0x94, 0x3a, 0x12);
        public ThreadPoolTraceEventParser(TraceEventSource source) : base(source) {}

        public event Action<TPCBEnqueueTraceData> ThreadPoolTraceCBEnqueue
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TPCBEnqueueTraceData(value, 0xFFFF, 0, "ThreadPoolTrace", ThreadPoolTraceTaskGuid, 32, "CBEnqueue", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TPCBEnqueueTraceData> ThreadPoolTraceCBStart
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TPCBEnqueueTraceData(value, 0xFFFF, 0, "ThreadPoolTrace", ThreadPoolTraceTaskGuid, 34, "CBStart", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TPCBDequeueTraceData> ThreadPoolTraceCBDequeue
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TPCBDequeueTraceData(value, 0xFFFF, 0, "ThreadPoolTrace", ThreadPoolTraceTaskGuid, 33, "CBDequeue", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TPCBDequeueTraceData> ThreadPoolTraceCBStop
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TPCBDequeueTraceData(value, 0xFFFF, 0, "ThreadPoolTrace", ThreadPoolTraceTaskGuid, 35, "CBStop", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TPCBCancelTraceData> ThreadPoolTraceCBCancel
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TPCBCancelTraceData(value, 0xFFFF, 0, "ThreadPoolTrace", ThreadPoolTraceTaskGuid, 36, "CBCancel", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TPPoolCreateCloseTraceData> ThreadPoolTracePoolCreate
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TPPoolCreateCloseTraceData(value, 0xFFFF, 0, "ThreadPoolTrace", ThreadPoolTraceTaskGuid, 37, "PoolCreate", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TPPoolCreateCloseTraceData> ThreadPoolTracePoolClose
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TPPoolCreateCloseTraceData(value, 0xFFFF, 0, "ThreadPoolTrace", ThreadPoolTraceTaskGuid, 38, "PoolClose", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TPThreadSetTraceData> ThreadPoolTraceThreadMinSet
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TPThreadSetTraceData(value, 0xFFFF, 0, "ThreadPoolTrace", ThreadPoolTraceTaskGuid, 39, "ThreadMinSet", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<TPThreadSetTraceData> ThreadPoolTraceThreadMaxSet
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new TPThreadSetTraceData(value, 0xFFFF, 0, "ThreadPoolTrace", ThreadPoolTraceTaskGuid, 40, "ThreadMaxSet", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }

    #region private
        private ThreadPoolState State
        {
            get
            {
                if (state == null)
                    state = GetPersistedStateFromSource<ThreadPoolState>(this.GetType().Name);
                return state;
            }
        }
        ThreadPoolState state;
        private static Guid ThreadPoolTraceTaskGuid = new Guid(0xc861d0e2, 0xa2c1, 0x4d36, 0x9f, 0x9c, 0x97, 0x0b, 0xab, 0x94, 0x3a, 0x12);
    #endregion
    }
    #region private types
    internal class ThreadPoolState : IFastSerializable
    {
        //TODO: Fill in
        void IFastSerializable.ToStream(Serializer serializer)
        {
        }
        void IFastSerializable.FromStream(Deserializer deserializer)
        {
        }
    }
    #endregion

    public sealed class TPCBEnqueueTraceData : TraceEvent
    {
        public Address PoolId { get { return GetHostPointer(0); } }
        public Address TaskId { get { return GetHostPointer(HostOffset(4, 1)); } }
        public Address CallbackFunction { get { return GetHostPointer(HostOffset(8, 2)); } }
        public Address CallbackContext { get { return GetHostPointer(HostOffset(12, 3)); } }
        public Address SubProcessTag { get { return GetHostPointer(HostOffset(16, 4)); } }

        #region Private
        internal TPCBEnqueueTraceData(Action<TPCBEnqueueTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, ThreadPoolState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(20, 5)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(20, 5)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("PoolId", PoolId);
             sb.XmlAttribHex("TaskId", TaskId);
             sb.XmlAttribHex("CallbackFunction", CallbackFunction);
             sb.XmlAttribHex("CallbackContext", CallbackContext);
             sb.XmlAttribHex("SubProcessTag", SubProcessTag);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "PoolId", "TaskId", "CallbackFunction", "CallbackContext", "SubProcessTag"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return PoolId;
                case 1:
                    return TaskId;
                case 2:
                    return CallbackFunction;
                case 3:
                    return CallbackContext;
                case 4:
                    return SubProcessTag;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<TPCBEnqueueTraceData> Action;
        private ThreadPoolState state;
        #endregion
    }
    public sealed class TPCBDequeueTraceData : TraceEvent
    {
        public Address TaskId { get { return GetHostPointer(0); } }

        #region Private
        internal TPCBDequeueTraceData(Action<TPCBDequeueTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, ThreadPoolState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(4, 1)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(4, 1)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("TaskId", TaskId);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "TaskId"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return TaskId;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<TPCBDequeueTraceData> Action;
        private ThreadPoolState state;
        #endregion
    }
    public sealed class TPCBCancelTraceData : TraceEvent
    {
        public Address TaskId { get { return GetHostPointer(0); } }
        public int CancelCount { get { return GetInt32At(HostOffset(4, 1)); } }

        #region Private
        internal TPCBCancelTraceData(Action<TPCBCancelTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, ThreadPoolState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(8, 1)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(8, 1)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("TaskId", TaskId);
             sb.XmlAttrib("CancelCount", CancelCount);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "TaskId", "CancelCount"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return TaskId;
                case 1:
                    return CancelCount;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<TPCBCancelTraceData> Action;
        private ThreadPoolState state;
        #endregion
    }
    public sealed class TPPoolCreateCloseTraceData : TraceEvent
    {
        public Address PoolId { get { return GetHostPointer(0); } }

        #region Private
        internal TPPoolCreateCloseTraceData(Action<TPPoolCreateCloseTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, ThreadPoolState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(4, 1)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(4, 1)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("PoolId", PoolId);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "PoolId"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return PoolId;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<TPPoolCreateCloseTraceData> Action;
        private ThreadPoolState state;
        #endregion
    }
    public sealed class TPThreadSetTraceData : TraceEvent
    {
        public Address PoolId { get { return GetHostPointer(0); } }
        public int ThreadNum { get { return GetInt32At(HostOffset(4, 1)); } }

        #region Private
        internal TPThreadSetTraceData(Action<TPThreadSetTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, ThreadPoolState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(8, 1)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(8, 1)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("PoolId", PoolId);
             sb.XmlAttrib("ThreadNum", ThreadNum);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "PoolId", "ThreadNum"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return PoolId;
                case 1:
                    return ThreadNum;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<TPThreadSetTraceData> Action;
        private ThreadPoolState state;
        #endregion
    }

    public sealed class HeapTraceProviderTraceEventParser : TraceEventParser 
    {
        public static string ProviderName = "HeapTraceProvider";
        public static Guid ProviderGuid = new Guid(0x222962ab, 0x6180, 0x4b88, 0xa8, 0x25, 0x34, 0x6b, 0x75, 0xf2, 0xa2, 0x4a);
        public HeapTraceProviderTraceEventParser(TraceEventSource source) : base(source) {}

        public event Action<HeapCreateTraceData> HeapTraceCreate
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new HeapCreateTraceData(value, 0xFFFF, 0, "HeapTrace", HeapTraceTaskGuid, 32, "Create", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<HeapAllocTraceData> HeapTraceAlloc
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new HeapAllocTraceData(value, 0xFFFF, 0, "HeapTrace", HeapTraceTaskGuid, 33, "Alloc", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<HeapReallocTraceData> HeapTraceReAlloc
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new HeapReallocTraceData(value, 0xFFFF, 0, "HeapTrace", HeapTraceTaskGuid, 34, "ReAlloc", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<HeapFreeTraceData> HeapTraceFree
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new HeapFreeTraceData(value, 0xFFFF, 0, "HeapTrace", HeapTraceTaskGuid, 36, "Free", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<HeapExpandTraceData> HeapTraceExpand
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new HeapExpandTraceData(value, 0xFFFF, 0, "HeapTrace", HeapTraceTaskGuid, 37, "Expand", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<HeapSnapShotTraceData> HeapTraceSnapShot
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new HeapSnapShotTraceData(value, 0xFFFF, 0, "HeapTrace", HeapTraceTaskGuid, 38, "SnapShot", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<HeapContractTraceData> HeapTraceContract
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new HeapContractTraceData(value, 0xFFFF, 0, "HeapTrace", HeapTraceTaskGuid, 42, "Contract", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<HeapTraceData> HeapTraceDestroy
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new HeapTraceData(value, 0xFFFF, 0, "HeapTrace", HeapTraceTaskGuid, 35, "Destroy", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<HeapTraceData> HeapTraceLock
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new HeapTraceData(value, 0xFFFF, 0, "HeapTrace", HeapTraceTaskGuid, 43, "Lock", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<HeapTraceData> HeapTraceUnlock
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new HeapTraceData(value, 0xFFFF, 0, "HeapTrace", HeapTraceTaskGuid, 44, "Unlock", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<HeapTraceData> HeapTraceValidate
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new HeapTraceData(value, 0xFFFF, 0, "HeapTrace", HeapTraceTaskGuid, 45, "Validate", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<HeapTraceData> HeapTraceWalk
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new HeapTraceData(value, 0xFFFF, 0, "HeapTrace", HeapTraceTaskGuid, 46, "Walk", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }

    #region private
        private HeapTraceProviderState State
        {
            get
            {
                if (state == null)
                    state = GetPersistedStateFromSource<HeapTraceProviderState>(this.GetType().Name);
                return state;
            }
        }
        HeapTraceProviderState state;
        private static Guid HeapTraceTaskGuid = new Guid(0x222962ab, 0x6180, 0x4b88, 0xa8, 0x25, 0x34, 0x6b, 0x75, 0xf2, 0xa2, 0x4a);
    #endregion
    }
    #region private types
    internal class HeapTraceProviderState : IFastSerializable
    {
        //TODO: Fill in
        void IFastSerializable.ToStream(Serializer serializer)
        {
        }
        void IFastSerializable.FromStream(Deserializer deserializer)
        {
        }
    }
    #endregion

    public sealed class HeapCreateTraceData : TraceEvent
    {
        public Address HeapHandle { get { return GetHostPointer(0); } }
        public int HeapFlags { get { return GetInt32At(HostOffset(4, 1)); } }

        #region Private
        internal HeapCreateTraceData(Action<HeapCreateTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, HeapTraceProviderState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(8, 1)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(8, 1)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("HeapHandle", HeapHandle);
             sb.XmlAttrib("HeapFlags", HeapFlags);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "HeapHandle", "HeapFlags"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return HeapHandle;
                case 1:
                    return HeapFlags;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<HeapCreateTraceData> Action;
        private HeapTraceProviderState state;
        #endregion
    }
    public sealed class HeapAllocTraceData : TraceEvent
    {
        public Address HeapHandle { get { return GetHostPointer(0); } }
        public Address AllocSize { get { return GetHostPointer(HostOffset(4, 1)); } }
        public Address AllocAddress { get { return GetHostPointer(HostOffset(8, 2)); } }
        public int SourceId { get { return GetInt32At(HostOffset(12, 3)); } }

        #region Private
        internal HeapAllocTraceData(Action<HeapAllocTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, HeapTraceProviderState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(16, 3)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(16, 3)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("HeapHandle", HeapHandle);
             sb.XmlAttribHex("AllocSize", AllocSize);
             sb.XmlAttribHex("AllocAddress", AllocAddress);
             sb.XmlAttrib("SourceId", SourceId);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "HeapHandle", "AllocSize", "AllocAddress", "SourceId"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return HeapHandle;
                case 1:
                    return AllocSize;
                case 2:
                    return AllocAddress;
                case 3:
                    return SourceId;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<HeapAllocTraceData> Action;
        private HeapTraceProviderState state;
        #endregion
    }
    public sealed class HeapReallocTraceData : TraceEvent
    {
        public Address HeapHandle { get { return GetHostPointer(0); } }
        public Address NewAllocAddress { get { return GetHostPointer(HostOffset(4, 1)); } }
        public Address OldAllocAddress { get { return GetHostPointer(HostOffset(8, 2)); } }
        public Address NewAllocSize { get { return GetHostPointer(HostOffset(12, 3)); } }
        public Address OldAllocSize { get { return GetHostPointer(HostOffset(16, 4)); } }
        public int SourceId { get { return GetInt32At(HostOffset(20, 5)); } }

        #region Private
        internal HeapReallocTraceData(Action<HeapReallocTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, HeapTraceProviderState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(24, 5)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(24, 5)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("HeapHandle", HeapHandle);
             sb.XmlAttribHex("NewAllocAddress", NewAllocAddress);
             sb.XmlAttribHex("OldAllocAddress", OldAllocAddress);
             sb.XmlAttribHex("NewAllocSize", NewAllocSize);
             sb.XmlAttribHex("OldAllocSize", OldAllocSize);
             sb.XmlAttrib("SourceId", SourceId);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "HeapHandle", "NewAllocAddress", "OldAllocAddress", "NewAllocSize", "OldAllocSize", "SourceId"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return HeapHandle;
                case 1:
                    return NewAllocAddress;
                case 2:
                    return OldAllocAddress;
                case 3:
                    return NewAllocSize;
                case 4:
                    return OldAllocSize;
                case 5:
                    return SourceId;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<HeapReallocTraceData> Action;
        private HeapTraceProviderState state;
        #endregion
    }
    public sealed class HeapFreeTraceData : TraceEvent
    {
        public Address HeapHandle { get { return GetHostPointer(0); } }
        public Address FreeAddress { get { return GetHostPointer(HostOffset(4, 1)); } }
        public int SourceId { get { return GetInt32At(HostOffset(8, 2)); } }

        #region Private
        internal HeapFreeTraceData(Action<HeapFreeTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, HeapTraceProviderState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(12, 2)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(12, 2)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("HeapHandle", HeapHandle);
             sb.XmlAttribHex("FreeAddress", FreeAddress);
             sb.XmlAttrib("SourceId", SourceId);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "HeapHandle", "FreeAddress", "SourceId"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return HeapHandle;
                case 1:
                    return FreeAddress;
                case 2:
                    return SourceId;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<HeapFreeTraceData> Action;
        private HeapTraceProviderState state;
        #endregion
    }
    public sealed class HeapExpandTraceData : TraceEvent
    {
        public Address HeapHandle { get { return GetHostPointer(0); } }
        public Address CommittedSize { get { return GetHostPointer(HostOffset(4, 1)); } }
        public Address CommitAddress { get { return GetHostPointer(HostOffset(8, 2)); } }
        public Address FreeSpace { get { return GetHostPointer(HostOffset(12, 3)); } }
        public Address CommittedSpace { get { return GetHostPointer(HostOffset(16, 4)); } }
        public Address ReservedSpace { get { return GetHostPointer(HostOffset(20, 5)); } }
        public int NoOfUCRs { get { return GetInt32At(HostOffset(24, 6)); } }

        #region Private
        internal HeapExpandTraceData(Action<HeapExpandTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, HeapTraceProviderState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(28, 6)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(28, 6)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("HeapHandle", HeapHandle);
             sb.XmlAttribHex("CommittedSize", CommittedSize);
             sb.XmlAttribHex("CommitAddress", CommitAddress);
             sb.XmlAttribHex("FreeSpace", FreeSpace);
             sb.XmlAttribHex("CommittedSpace", CommittedSpace);
             sb.XmlAttribHex("ReservedSpace", ReservedSpace);
             sb.XmlAttrib("NoOfUCRs", NoOfUCRs);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "HeapHandle", "CommittedSize", "CommitAddress", "FreeSpace", "CommittedSpace", "ReservedSpace", "NoOfUCRs"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return HeapHandle;
                case 1:
                    return CommittedSize;
                case 2:
                    return CommitAddress;
                case 3:
                    return FreeSpace;
                case 4:
                    return CommittedSpace;
                case 5:
                    return ReservedSpace;
                case 6:
                    return NoOfUCRs;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<HeapExpandTraceData> Action;
        private HeapTraceProviderState state;
        #endregion
    }
    public sealed class HeapSnapShotTraceData : TraceEvent
    {
        public Address HeapHandle { get { return GetHostPointer(0); } }
        public Address FreeSpace { get { return GetHostPointer(HostOffset(4, 1)); } }
        public Address CommittedSpace { get { return GetHostPointer(HostOffset(8, 2)); } }
        public Address ReservedSpace { get { return GetHostPointer(HostOffset(12, 3)); } }
        public int HeapFlags { get { return GetInt32At(HostOffset(16, 4)); } }

        #region Private
        internal HeapSnapShotTraceData(Action<HeapSnapShotTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, HeapTraceProviderState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(20, 4)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(20, 4)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("HeapHandle", HeapHandle);
             sb.XmlAttribHex("FreeSpace", FreeSpace);
             sb.XmlAttribHex("CommittedSpace", CommittedSpace);
             sb.XmlAttribHex("ReservedSpace", ReservedSpace);
             sb.XmlAttrib("HeapFlags", HeapFlags);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "HeapHandle", "FreeSpace", "CommittedSpace", "ReservedSpace", "HeapFlags"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return HeapHandle;
                case 1:
                    return FreeSpace;
                case 2:
                    return CommittedSpace;
                case 3:
                    return ReservedSpace;
                case 4:
                    return HeapFlags;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<HeapSnapShotTraceData> Action;
        private HeapTraceProviderState state;
        #endregion
    }
    public sealed class HeapContractTraceData : TraceEvent
    {
        public Address HeapHandle { get { return GetHostPointer(0); } }
        public Address DeCommittedSize { get { return GetHostPointer(HostOffset(4, 1)); } }
        public Address DeCommitAddress { get { return GetHostPointer(HostOffset(8, 2)); } }
        public Address FreeSpace { get { return GetHostPointer(HostOffset(12, 3)); } }
        public Address CommittedSpace { get { return GetHostPointer(HostOffset(16, 4)); } }
        public Address ReservedSpace { get { return GetHostPointer(HostOffset(20, 5)); } }
        public int NoOfUCRs { get { return GetInt32At(HostOffset(24, 6)); } }

        #region Private
        internal HeapContractTraceData(Action<HeapContractTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, HeapTraceProviderState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(28, 6)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(28, 6)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("HeapHandle", HeapHandle);
             sb.XmlAttribHex("DeCommittedSize", DeCommittedSize);
             sb.XmlAttribHex("DeCommitAddress", DeCommitAddress);
             sb.XmlAttribHex("FreeSpace", FreeSpace);
             sb.XmlAttribHex("CommittedSpace", CommittedSpace);
             sb.XmlAttribHex("ReservedSpace", ReservedSpace);
             sb.XmlAttrib("NoOfUCRs", NoOfUCRs);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "HeapHandle", "DeCommittedSize", "DeCommitAddress", "FreeSpace", "CommittedSpace", "ReservedSpace", "NoOfUCRs"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return HeapHandle;
                case 1:
                    return DeCommittedSize;
                case 2:
                    return DeCommitAddress;
                case 3:
                    return FreeSpace;
                case 4:
                    return CommittedSpace;
                case 5:
                    return ReservedSpace;
                case 6:
                    return NoOfUCRs;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<HeapContractTraceData> Action;
        private HeapTraceProviderState state;
        #endregion
    }
    public sealed class HeapTraceData : TraceEvent
    {
        public Address HeapHandle { get { return GetHostPointer(0); } }

        #region Private
        internal HeapTraceData(Action<HeapTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, HeapTraceProviderState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(4, 1)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(4, 1)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("HeapHandle", HeapHandle);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "HeapHandle"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return HeapHandle;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<HeapTraceData> Action;
        private HeapTraceProviderState state;
        #endregion
    }

    public sealed class CritSecTraceProviderTraceEventParser : TraceEventParser 
    {
        public static string ProviderName = "CritSecTraceProvider";
        public static Guid ProviderGuid = new Guid(0x3ac66736, 0xcc59, 0x4cff, 0x81, 0x15, 0x8d, 0xf5, 0x0e, 0x39, 0x81, 0x6b);
        public CritSecTraceProviderTraceEventParser(TraceEventSource source) : base(source) {}

        public event Action<CritSecCollisionTraceData> CritSecTraceCollision
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new CritSecCollisionTraceData(value, 0xFFFF, 0, "CritSecTrace", CritSecTraceTaskGuid, 34, "Collision", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }
        public event Action<CritSecInitTraceData> CritSecTraceInitialize
        {
            add
            {
                                                         // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new CritSecInitTraceData(value, 0xFFFF, 0, "CritSecTrace", CritSecTraceTaskGuid, 35, "Initialize", ProviderGuid, ProviderName, State));
            }
            remove
            {
                throw new Exception("Not supported");
            }
        }

    #region private
        private CritSecTraceProviderState State
        {
            get
            {
                if (state == null)
                    state = GetPersistedStateFromSource<CritSecTraceProviderState>(this.GetType().Name);
                return state;
            }
        }
        CritSecTraceProviderState state;
        private static Guid CritSecTraceTaskGuid = new Guid(0x3ac66736, 0xcc59, 0x4cff, 0x81, 0x15, 0x8d, 0xf5, 0x0e, 0x39, 0x81, 0x6b);
    #endregion
    }
    #region private types
    internal class CritSecTraceProviderState : IFastSerializable
    {
        //TODO: Fill in
        void IFastSerializable.ToStream(Serializer serializer)
        {
        }
        void IFastSerializable.FromStream(Deserializer deserializer)
        {
        }
    }
    #endregion

    public sealed class CritSecCollisionTraceData : TraceEvent
    {
        public int LockCount { get { return GetInt32At(0); } }
        public int SpinCount { get { return GetInt32At(4); } }
        public Address OwningThread { get { return GetHostPointer(8); } }
        public Address CritSecAddr { get { return GetHostPointer(HostOffset(12, 1)); } }

        #region Private
        internal CritSecCollisionTraceData(Action<CritSecCollisionTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, CritSecTraceProviderState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(16, 2)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(16, 2)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttrib("LockCount", LockCount);
             sb.XmlAttrib("SpinCount", SpinCount);
             sb.XmlAttribHex("OwningThread", OwningThread);
             sb.XmlAttribHex("CritSecAddr", CritSecAddr);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "LockCount", "SpinCount", "OwningThread", "CritSecAddr"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return LockCount;
                case 1:
                    return SpinCount;
                case 2:
                    return OwningThread;
                case 3:
                    return CritSecAddr;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<CritSecCollisionTraceData> Action;
        private CritSecTraceProviderState state;
        #endregion
    }
    public sealed class CritSecInitTraceData : TraceEvent
    {
        public Address SpinCount { get { return GetHostPointer(0); } }
        public Address CritSecAddr { get { return GetHostPointer(HostOffset(4, 1)); } }

        #region Private
        internal CritSecInitTraceData(Action<CritSecInitTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName, CritSecTraceProviderState state)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            this.Action = action;
            this.state = state;
        }
        protected internal override void Dispatch()
        {
            Debug.Assert(!(Version == 0 && EventDataLength != HostOffset(8, 2)));
            Debug.Assert(!(Version > 0 && EventDataLength < HostOffset(8, 2)));
            Action(this);
        }
        public override StringBuilder ToXml(StringBuilder sb)
        {
             Prefix(sb);
             sb.XmlAttribHex("SpinCount", SpinCount);
             sb.XmlAttribHex("CritSecAddr", CritSecAddr);
             sb.Append("/>");
             return sb;
        }

        public override string[] PayloadNames
        {
            get
            {
                if (payloadNames == null)
                    payloadNames = new string[] { "SpinCount", "CritSecAddr"};
                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return SpinCount;
                case 1:
                    return CritSecAddr;
                default:
                    Debug.Assert(false, "Bad field index");
                    return null;
            }
        }

        private event Action<CritSecInitTraceData> Action;
        private CritSecTraceProviderState state;
        #endregion
    }

}
