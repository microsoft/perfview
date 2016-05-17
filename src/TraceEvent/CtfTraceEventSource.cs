using Microsoft.Diagnostics.Tracing.Ctf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;

#pragma warning disable 1591

namespace Microsoft.Diagnostics.Tracing
{
    internal struct ETWMapping
    {
        public bool IsNull { get { return Guid == new Guid(); } }

        public Guid Guid;
        public TraceEventOpcode Opcode;
        public TraceEventID Id;
        public Byte Version;

        public ETWMapping(Guid guid, int opcode, int id, int version)
        {
            Guid = guid;
            Opcode = (TraceEventOpcode)opcode;
            Id = (TraceEventID)id;
            Version = (byte)version;
        }
    }

    public unsafe sealed class CtfTraceEventSource : TraceEventDispatcher, IDisposable
    {
        private string _filename;
        private ZipArchive _zip;
        private List<Tuple<ZipArchiveEntry, CtfMetadata>> _channels;
        private TraceEventNativeMethods.EVENT_RECORD* _header;
        private Dictionary<string, ETWMapping> _eventMapping;

#if DEBUG
        private StreamWriter _debugOut;
#endif

        public CtfTraceEventSource(string fileName)
        {
            _filename = fileName;
            _zip = ZipFile.Open(fileName, ZipArchiveMode.Read);

            _channels = new List<Tuple<ZipArchiveEntry, CtfMetadata>>();
            foreach (ZipArchiveEntry metadataArchive in _zip.Entries.Where(p => Path.GetFileName(p.FullName) == "metadata"))
            {
                CtfMetadataLegacyParser parser = new CtfMetadataLegacyParser(metadataArchive.Open());
                CtfMetadata metadata = new CtfMetadata(parser);

                string path = Path.GetDirectoryName(metadataArchive.FullName);
                _channels.AddRange(from entry in _zip.Entries
                                   where Path.GetDirectoryName(entry.FullName) == path && Path.GetFileName(entry.FullName).StartsWith("channel")
                                   select new Tuple<ZipArchiveEntry, CtfMetadata>(entry, metadata));

                pointerSize = Path.GetDirectoryName(metadataArchive.FullName).EndsWith("64-bit") ? 8 : 4;
            }


            IntPtr mem = Marshal.AllocHGlobal(sizeof(TraceEventNativeMethods.EVENT_RECORD));
            TraceEventNativeMethods.ZeroMemory(mem, sizeof(TraceEventNativeMethods.EVENT_RECORD));
            _header = (TraceEventNativeMethods.EVENT_RECORD*)mem;

            int processors = (from entry in _channels
                              let filename = entry.Item1.FullName
                              let i = filename.LastIndexOf('_')
                              let processor = filename.Substring(i + 1)
                              select int.Parse(processor)
                             ).Max() + 1;

            numberOfProcessors = processors;

            // TODO: Need to cleanly separate clocks, but in practice there's only the one clock.
            CtfClock clock = _channels.First().Item2.Clocks.First();

            long firstEventTimestamp = (long)new ChannelList(_channels).First().Current.Timestamp;

            _QPCFreq = (long)clock.Frequency;
            sessionStartTimeQPC = firstEventTimestamp;
            _syncTimeQPC = firstEventTimestamp;
            _syncTimeUTC = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds((clock.Offset - 1) / clock.Frequency);

            _eventMapping = InitEventMap();

#if DEBUG
            //// Uncomment for debug output.
            //_debugOut = File.CreateText("debug.txt");
            //_debugOut.AutoFlush = true;
#endif
        }

        private static Dictionary<string, ETWMapping> InitEventMap()
        {
            Dictionary<string, ETWMapping> result = new Dictionary<string, ETWMapping>();
            result["DotNETRuntime:GCStart"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 1, 0);
            result["DotNETRuntime:GCStart_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 1, 1);
            result["DotNETRuntime:GCStart_V2"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 1, 2);
            result["DotNETRuntime:WorkerThreadCreate"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 40, 0);
            result["DotNETRuntime:WorkerThreadRetire"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 42, 0);
            result["DotNETRuntime:IOThreadCreate"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 44, 0);
            result["DotNETRuntime:IOThreadCreate_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 44, 1);
            result["DotNETRuntime:IOThreadRetire"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 46, 0);
            result["DotNETRuntime:IOThreadRetire_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 46, 1);
            result["DotNETRuntime:ThreadpoolSuspensionSuspendThread"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 48, 0);
            result["DotNETRuntime:ThreadPoolWorkerThreadStart"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 50, 0);
            result["DotNETRuntime:ThreadPoolWorkerThreadRetirementStart"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 52, 0);
            result["DotNETRuntime:ThreadPoolWorkingThreadCount"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 60, 0);
            result["DotNETRuntime:ExceptionThrown"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 80, 0);
            result["DotNETRuntime:ExceptionThrown_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 80, 1);
            result["DotNETRuntime:Contention"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 81, 0);
            result["DotNETRuntime:ContentionStart_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 81, 1);
            result["DotNETRuntime:StrongNameVerificationStart"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 181, 0);
            result["DotNETRuntime:StrongNameVerificationStart_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 181, 1);
            result["DotNETRuntime:AuthenticodeVerificationStart"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 183, 0);
            result["DotNETRuntime:AuthenticodeVerificationStart_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 183, 1);
            result["DotNETRuntime:RuntimeInformationStart"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 187, 0);
            result["DotNETRuntime:DebugIPCEventStart"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 240, 0);
            result["DotNETRuntime:DebugExceptionProcessingStart"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 242, 0);
            result["DotNETRuntime:ExceptionCatchStart"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 250, 0);
            result["DotNETRuntime:ExceptionFinallyStart"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 252, 0);
            result["DotNETRuntime:ExceptionFilterStart"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 254, 0);
            result["DotNETRuntime:CodeSymbols"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 260, 0);
            result["DotNETRuntime:EventSource"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 270, 0);
            result["DotNETRuntime:GCEnd"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 2, 2, 0);
            result["DotNETRuntime:GCEnd_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 2, 2, 1);
            result["DotNETRuntime:WorkerThreadTerminate"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 2, 41, 0);
            result["DotNETRuntime:WorkerThreadUnretire"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 2, 43, 0);
            result["DotNETRuntime:IOThreadTerminate"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 2, 45, 0);
            result["DotNETRuntime:IOThreadTerminate_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 2, 45, 1);
            result["DotNETRuntime:IOThreadUnretire"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 2, 47, 0);
            result["DotNETRuntime:IOThreadUnretire_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 2, 47, 1);
            result["DotNETRuntime:ThreadpoolSuspensionResumeThread"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 2, 49, 0);
            result["DotNETRuntime:ThreadPoolWorkerThreadStop"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 2, 51, 0);
            result["DotNETRuntime:ThreadPoolWorkerThreadRetirementStop"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 2, 53, 0);
            result["DotNETRuntime:ContentionStop"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 2, 91, 0);
            result["DotNETRuntime:StrongNameVerificationStop"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 2, 182, 0);
            result["DotNETRuntime:StrongNameVerificationStop_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 2, 182, 1);
            result["DotNETRuntime:AuthenticodeVerificationStop"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 2, 184, 0);
            result["DotNETRuntime:AuthenticodeVerificationStop_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 2, 184, 1);
            result["DotNETRuntime:DebugIPCEventEnd"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 2, 241, 0);
            result["DotNETRuntime:DebugExceptionProcessingEnd"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 2, 243, 0);
            result["DotNETRuntime:ExceptionCatchStop"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 2, 251, 0);
            result["DotNETRuntime:ExceptionFinallyStop"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 2, 253, 0);
            result["DotNETRuntime:ExceptionFilterStop"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 2, 255, 0);
            result["DotNETRuntime:ExceptionThrownStop"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 2, 256, 0);
            result["DotNETRuntime:GCSuspendEEBegin"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 10, 9, 0);
            result["DotNETRuntime:GCSuspendEEBegin_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 10, 9, 1);
            result["DotNETRuntime:BulkType"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 10, 15, 0);
            result["DotNETRuntime:ModuleRangeLoad"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 10, 158, 0);
            result["DotNETRuntime:GCAllocationTick"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 11, 10, 0);
            result["DotNETRuntime:GCAllocationTick_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 11, 10, 1);
            result["DotNETRuntime:GCAllocationTick_V2"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 11, 10, 2);
            result["DotNETRuntime:GCAllocationTick_V3"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 11, 10, 3);
            result["DotNETRuntime:ThreadPoolEnqueue"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 11, 61, 0);
            result["DotNETRuntime:ThreadCreating"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 11, 70, 0);
            result["DotNETRuntime:GCCreateConcurrentThread"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 12, 11, 0);
            result["DotNETRuntime:GCCreateConcurrentThread_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 12, 11, 1);
            result["DotNETRuntime:ThreadPoolDequeue"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 12, 62, 0);
            result["DotNETRuntime:ThreadRunning"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 12, 71, 0);
            result["DotNETRuntime:GCTerminateConcurrentThread"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 13, 12, 0);
            result["DotNETRuntime:GCTerminateConcurrentThread_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 13, 12, 1);
            result["DotNETRuntime:ThreadPoolIOEnqueue"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 13, 63, 0);
            result["DotNETRuntime:ThreadPoolIODequeue"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 14, 64, 0);
            result["DotNETRuntime:DCStartCompleteV2"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 14, 135, 0);
            result["DotNETRuntime:GCFinalizersEnd"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 15, 13, 0);
            result["DotNETRuntime:GCFinalizersEnd_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 15, 13, 1);
            result["DotNETRuntime:ThreadPoolIOPack"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 15, 65, 0);
            result["DotNETRuntime:DCEndCompleteV2"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 15, 136, 0);
            result["DotNETRuntime:GCFinalizersBegin"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 19, 14, 0);
            result["DotNETRuntime:GCFinalizersBegin_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 19, 14, 1);
            result["DotNETRuntime:GCBulkRootEdge"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 20, 16, 0);
            result["DotNETRuntime:GCBulkRootConditionalWeakTableElementEdge"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 21, 17, 0);
            result["DotNETRuntime:GCBulkNode"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 22, 18, 0);
            result["DotNETRuntime:GCBulkEdge"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 23, 19, 0);
            result["DotNETRuntime:GCSampledObjectAllocationHigh"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 24, 20, 0);
            result["DotNETRuntime:GCSampledObjectAllocationLow"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 24, 32, 0);
            result["DotNETRuntime:GCBulkSurvivingObjectRanges"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 25, 21, 0);
            result["DotNETRuntime:GCBulkMovedObjectRanges"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 26, 22, 0);
            result["DotNETRuntime:GCGenerationRange"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 27, 23, 0);
            result["DotNETRuntime:GCMarkStackRoots"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 28, 25, 0);
            result["DotNETRuntime:GCMarkFinalizeQueueRoots"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 29, 26, 0);
            result["DotNETRuntime:GCMarkHandles"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 30, 27, 0);
            result["DotNETRuntime:GCMarkOlderGenerationRoots"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 31, 28, 0);
            result["DotNETRuntime:FinalizeObject"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 32, 29, 0);
            result["DotNETRuntime:SetGCHandle"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 33, 30, 0);
            result["DotNETRuntime:MethodLoad"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 33, 141, 0);
            result["DotNETRuntime:MethodLoad_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 33, 141, 1);
            result["DotNETRuntime:MethodLoad_V2"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 33, 141, 2);
            result["DotNETRuntime:ModuleLoad"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 33, 152, 0);
            result["DotNETRuntime:ModuleLoad_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 33, 152, 1);
            result["DotNETRuntime:ModuleLoad_V2"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 33, 152, 2);
            result["DotNETRuntime:DestroyGCHandle"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 34, 31, 0);
            result["DotNETRuntime:MethodUnload"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 34, 142, 0);
            result["DotNETRuntime:MethodUnload_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 34, 142, 1);
            result["DotNETRuntime:MethodUnload_V2"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 34, 142, 2);
            result["DotNETRuntime:ModuleUnload"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 34, 153, 0);
            result["DotNETRuntime:ModuleUnload_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 34, 153, 1);
            result["DotNETRuntime:ModuleUnload_V2"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 34, 153, 2);
            result["DotNETRuntime:GCTriggered"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 35, 35, 0);
            result["DotNETRuntime:MethodDCStartV2"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 35, 137, 0);
            result["DotNETRuntime:ModuleDCStartV2"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 35, 149, 0);
            result["DotNETRuntime:PinObjectAtGCTime"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 36, 33, 0);
            result["DotNETRuntime:MethodDCEndV2"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 36, 138, 0);
            result["DotNETRuntime:ModuleDCEndV2"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 36, 150, 0);
            result["DotNETRuntime:MethodLoadVerbose"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 37, 143, 0);
            result["DotNETRuntime:MethodLoadVerbose_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 37, 143, 1);
            result["DotNETRuntime:MethodLoadVerbose_V2"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 37, 143, 2);
            result["DotNETRuntime:AssemblyLoad"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 37, 154, 0);
            result["DotNETRuntime:AssemblyLoad_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 37, 154, 1);
            result["DotNETRuntime:GCBulkRootCCW"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 38, 36, 0);
            result["DotNETRuntime:MethodUnloadVerbose"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 38, 144, 0);
            result["DotNETRuntime:MethodUnloadVerbose_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 38, 144, 1);
            result["DotNETRuntime:MethodUnloadVerbose_V2"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 38, 144, 2);
            result["DotNETRuntime:AssemblyUnload"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 38, 155, 0);
            result["DotNETRuntime:AssemblyUnload_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 38, 155, 1);
            result["DotNETRuntime:GCBulkRCW"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 39, 37, 0);
            result["DotNETRuntime:MethodDCStartVerboseV2"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 39, 139, 0);
            result["DotNETRuntime:GCBulkRootStaticVar"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 40, 38, 0);
            result["DotNETRuntime:MethodDCEndVerboseV2"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 40, 140, 0);
            result["DotNETRuntime:AppDomainLoad"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 41, 156, 0);
            result["DotNETRuntime:AppDomainLoad_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 41, 156, 1);
            result["DotNETRuntime:MethodJittingStarted"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 42, 145, 0);
            result["DotNETRuntime:MethodJittingStarted_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 42, 145, 1);
            result["DotNETRuntime:AppDomainUnload"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 42, 157, 0);
            result["DotNETRuntime:AppDomainUnload_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 42, 157, 1);
            result["DotNETRuntime:DomainModuleLoad"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 45, 151, 0);
            result["DotNETRuntime:DomainModuleLoad_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 45, 151, 1);
            result["DotNETRuntime:AppDomainMemAllocated"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 48, 83, 0);
            result["DotNETRuntime:AppDomainMemSurvived"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 49, 84, 0);
            result["DotNETRuntime:ThreadCreated"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 50, 85, 0);
            result["DotNETRuntime:ThreadTerminated"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 51, 86, 0);
            result["DotNETRuntime:ThreadDomainEnter"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 52, 87, 0);
            result["DotNETRuntime:CLRStackWalk"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 82, 82, 0);
            result["DotNETRuntime:MethodJitInliningSucceeded"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 83, 185, 0);
            result["DotNETRuntime:MethodJitInliningFailed"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 84, 186, 0);
            result["DotNETRuntime:MethodJitTailCallSucceeded"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 85, 188, 0);
            result["DotNETRuntime:MethodJitTailCallFailed"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 86, 189, 0);
            result["DotNETRuntime:MethodILToNativeMap"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 87, 190, 0);
            result["DotNETRuntime:ILStubGenerated"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 88, 88, 0);
            result["DotNETRuntime:ILStubCacheHit"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 89, 89, 0);
            result["DotNETRuntime:ThreadPoolWorkerThreadWait"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 90, 57, 0);
            result["DotNETRuntime:ThreadPoolWorkerThreadAdjustmentSample"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 100, 54, 0);
            result["DotNETRuntime:ThreadPoolWorkerThreadAdjustmentAdjustment"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 101, 55, 0);
            result["DotNETRuntime:ThreadPoolWorkerThreadAdjustmentStats"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 102, 56, 0);
            result["DotNETRuntime:GCRestartEEEnd"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 132, 3, 0);
            result["DotNETRuntime:GCRestartEEEnd_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 132, 3, 1);
            result["DotNETRuntime:GCHeapStats"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 133, 4, 0);
            result["DotNETRuntime:GCHeapStats_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 133, 4, 1);
            result["DotNETRuntime:GCCreateSegment"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 134, 5, 0);
            result["DotNETRuntime:GCCreateSegment_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 134, 5, 1);
            result["DotNETRuntime:GCFreeSegment"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 135, 6, 0);
            result["DotNETRuntime:GCFreeSegment_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 135, 6, 1);
            result["DotNETRuntime:GCRestartEEBegin"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 136, 7, 0);
            result["DotNETRuntime:GCRestartEEBegin_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 136, 7, 1);
            result["DotNETRuntime:GCSuspendEEEnd"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 137, 8, 0);
            result["DotNETRuntime:GCSuspendEEEnd_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 137, 8, 1);
            result["DotNETRuntime:IncreaseMemoryPressure"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 200, 200, 0);
            result["DotNETRuntime:DecreaseMemoryPressure"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 201, 201, 0);
            result["DotNETRuntime:GCMarkWithType"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 202, 202, 0);
            result["DotNETRuntime:GCJoin_V2"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 203, 203, 2);
            result["DotNETRuntime:GCPerHeapHistory_V3"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 204, 204, 3);
            result["DotNETRuntime:GCGlobalHeapHistory_V2"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 205, 205, 2);

            result["DotNETRuntimePrivate:ApplyPolicyStart"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 10, 90, 0);
            result["DotNETRuntimePrivate:ApplyPolicyStart_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 10, 90, 1);
            result["DotNETRuntimePrivate:ModuleRangeLoadPrivate"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 10, 158, 0);
            result["DotNETRuntimePrivate:EvidenceGenerated"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 10, 177, 0);
            result["DotNETRuntimePrivate:MulticoreJit"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 10, 201, 0);
            result["DotNETRuntimePrivate:ApplyPolicyEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 11, 91, 0);
            result["DotNETRuntimePrivate:ApplyPolicyEnd_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 11, 91, 1);
            result["DotNETRuntimePrivate:MulticoreJitMethodCodeReturned"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 11, 202, 0);
            result["DotNETRuntimePrivate:IInspectableRuntimeClassName"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 11, 400, 0);
            result["DotNETRuntimePrivate:LdLibShFolder"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 12, 92, 0);
            result["DotNETRuntimePrivate:LdLibShFolder_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 12, 92, 1);
            result["DotNETRuntimePrivate:WinRTUnbox"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 12, 401, 0);
            result["DotNETRuntimePrivate:LdLibShFolderEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 13, 93, 0);
            result["DotNETRuntimePrivate:LdLibShFolderEnd_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 13, 93, 1);
            result["DotNETRuntimePrivate:CreateRCW"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 13, 402, 0);
            result["DotNETRuntimePrivate:GCSettings"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 14, 2, 0);
            result["DotNETRuntimePrivate:GCSettings_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 14, 2, 1);
            result["DotNETRuntimePrivate:PrestubWorker"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 14, 94, 0);
            result["DotNETRuntimePrivate:PrestubWorker_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 14, 94, 1);
            result["DotNETRuntimePrivate:RCWVariance"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 14, 403, 0);
            result["DotNETRuntimePrivate:PrestubWorkerEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 15, 95, 0);
            result["DotNETRuntimePrivate:PrestubWorkerEnd_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 15, 95, 1);
            result["DotNETRuntimePrivate:RCWIEnumerableCasting"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 15, 404, 0);
            result["DotNETRuntimePrivate:GCOptimized"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 16, 3, 0);
            result["DotNETRuntimePrivate:GCOptimized_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 16, 3, 1);
            result["DotNETRuntimePrivate:GetInstallationStart"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 16, 96, 0);
            result["DotNETRuntimePrivate:GetInstallationStart_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 16, 96, 1);
            result["DotNETRuntimePrivate:CreateCCW"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 16, 405, 0);
            result["DotNETRuntimePrivate:GCPerHeapHistory"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 17, 4, 2);
            result["DotNETRuntimePrivate:GCPerHeapHistory_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 17, 4, 1);
            result["DotNETRuntimePrivate:GetInstallationEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 17, 97, 0);
            result["DotNETRuntimePrivate:GetInstallationEnd_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 17, 97, 1);
            result["DotNETRuntimePrivate:CCWVariance"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 17, 406, 0);
            result["DotNETRuntimePrivate:GCGlobalHeapHistory"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 18, 5, 0);
            result["DotNETRuntimePrivate:GCGlobalHeapHistory_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 18, 5, 1);
            result["DotNETRuntimePrivate:OpenHModule"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 18, 98, 0);
            result["DotNETRuntimePrivate:OpenHModule_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 18, 98, 1);
            result["DotNETRuntimePrivate:ObjectVariantMarshallingToNative"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 18, 407, 0);
            result["DotNETRuntimePrivate:GCFullNotify"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 19, 25, 0);
            result["DotNETRuntimePrivate:GCFullNotify_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 19, 25, 1);
            result["DotNETRuntimePrivate:OpenHModuleEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 19, 99, 0);
            result["DotNETRuntimePrivate:OpenHModuleEnd_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 19, 99, 1);
            result["DotNETRuntimePrivate:GetTypeFromGUID"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 19, 408, 0);
            result["DotNETRuntimePrivate:GCJoin"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 20, 6, 0);
            result["DotNETRuntimePrivate:GCJoin_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 20, 6, 1);
            result["DotNETRuntimePrivate:ExplicitBindStart"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 20, 100, 0);
            result["DotNETRuntimePrivate:ExplicitBindStart_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 20, 100, 1);
            result["DotNETRuntimePrivate:GetTypeFromProgID"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 20, 409, 0);
            result["DotNETRuntimePrivate:PrvGCMarkStackRoots"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 21, 7, 0);
            result["DotNETRuntimePrivate:PrvGCMarkStackRoots_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 21, 7, 1);
            result["DotNETRuntimePrivate:ExplicitBindEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 21, 101, 0);
            result["DotNETRuntimePrivate:ExplicitBindEnd_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 21, 101, 1);
            result["DotNETRuntimePrivate:ConvertToCallbackEtw"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 21, 410, 0);
            result["DotNETRuntimePrivate:PrvGCMarkFinalizeQueueRoots"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 22, 8, 0);
            result["DotNETRuntimePrivate:PrvGCMarkFinalizeQueueRoots_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 22, 8, 1);
            result["DotNETRuntimePrivate:ParseXml"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 22, 102, 0);
            result["DotNETRuntimePrivate:ParseXml_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 22, 102, 1);
            result["DotNETRuntimePrivate:BeginCreateManagedReference"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 22, 411, 0);
            result["DotNETRuntimePrivate:PrvGCMarkHandles"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 23, 9, 0);
            result["DotNETRuntimePrivate:PrvGCMarkHandles_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 23, 9, 1);
            result["DotNETRuntimePrivate:ParseXmlEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 23, 103, 0);
            result["DotNETRuntimePrivate:ParseXmlEnd_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 23, 103, 1);
            result["DotNETRuntimePrivate:EndCreateManagedReference"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 23, 412, 0);
            result["DotNETRuntimePrivate:PrvGCMarkCards"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 24, 10, 0);
            result["DotNETRuntimePrivate:PrvGCMarkCards_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 24, 10, 1);
            result["DotNETRuntimePrivate:InitDefaultDomain"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 24, 104, 0);
            result["DotNETRuntimePrivate:InitDefaultDomain_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 24, 104, 1);
            result["DotNETRuntimePrivate:ObjectVariantMarshallingToManaged"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 24, 413, 0);
            result["DotNETRuntimePrivate:BGCBegin"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 25, 11, 0);
            result["DotNETRuntimePrivate:InitDefaultDomainEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 25, 105, 0);
            result["DotNETRuntimePrivate:InitDefaultDomainEnd_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 25, 105, 1);
            result["DotNETRuntimePrivate:BGC1stNonConEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 26, 12, 0);
            result["DotNETRuntimePrivate:InitSecurity"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 26, 106, 0);
            result["DotNETRuntimePrivate:InitSecurity_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 26, 106, 1);
            result["DotNETRuntimePrivate:BGC1stConEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 27, 13, 0);
            result["DotNETRuntimePrivate:InitSecurityEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 27, 107, 0);
            result["DotNETRuntimePrivate:InitSecurityEnd_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 27, 107, 1);
            result["DotNETRuntimePrivate:BGC2ndNonConBegin"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 28, 14, 0);
            result["DotNETRuntimePrivate:AllowBindingRedirs"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 28, 108, 0);
            result["DotNETRuntimePrivate:AllowBindingRedirs_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 28, 108, 1);
            result["DotNETRuntimePrivate:BGC2ndNonConEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 29, 15, 0);
            result["DotNETRuntimePrivate:AllowBindingRedirsEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 29, 109, 0);
            result["DotNETRuntimePrivate:AllowBindingRedirsEnd_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 29, 109, 1);
            result["DotNETRuntimePrivate:BGC2ndConBegin"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 30, 16, 0);
            result["DotNETRuntimePrivate:EEConfigSync"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 30, 110, 0);
            result["DotNETRuntimePrivate:EEConfigSync_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 30, 110, 1);
            result["DotNETRuntimePrivate:BGC2ndConEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 31, 17, 0);
            result["DotNETRuntimePrivate:EEConfigSyncEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 31, 111, 0);
            result["DotNETRuntimePrivate:EEConfigSyncEnd_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 31, 111, 1);
            result["DotNETRuntimePrivate:BGCPlanEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 32, 18, 0);
            result["DotNETRuntimePrivate:FusionBinding"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 32, 112, 0);
            result["DotNETRuntimePrivate:FusionBinding_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 32, 112, 1);
            result["DotNETRuntimePrivate:BGCSweepEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 33, 19, 0);
            result["DotNETRuntimePrivate:FusionBindingEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 33, 113, 0);
            result["DotNETRuntimePrivate:FusionBindingEnd_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 33, 113, 1);
            result["DotNETRuntimePrivate:BGCDrainMark"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 34, 20, 0);
            result["DotNETRuntimePrivate:LoaderCatchCall"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 34, 114, 0);
            result["DotNETRuntimePrivate:LoaderCatchCall_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 34, 114, 1);
            result["DotNETRuntimePrivate:BGCRevisit"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 35, 21, 0);
            result["DotNETRuntimePrivate:LoaderCatchCallEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 35, 115, 0);
            result["DotNETRuntimePrivate:LoaderCatchCallEnd_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 35, 115, 1);
            result["DotNETRuntimePrivate:BGCOverflow"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 36, 22, 0);
            result["DotNETRuntimePrivate:FusionInit"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 36, 116, 0);
            result["DotNETRuntimePrivate:FusionInit_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 36, 116, 1);
            result["DotNETRuntimePrivate:BGCAllocWaitBegin"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 37, 23, 0);
            result["DotNETRuntimePrivate:FusionInitEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 37, 117, 0);
            result["DotNETRuntimePrivate:FusionInitEnd_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 37, 117, 1);
            result["DotNETRuntimePrivate:BGCAllocWaitEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 38, 24, 0);
            result["DotNETRuntimePrivate:FusionAppCtx"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 38, 118, 0);
            result["DotNETRuntimePrivate:FusionAppCtx_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 38, 118, 1);
            result["DotNETRuntimePrivate:FusionAppCtxEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 39, 119, 0);
            result["DotNETRuntimePrivate:FusionAppCtxEnd_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 39, 119, 1);
            result["DotNETRuntimePrivate:PrvFinalizeObject"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 39, 192, 0);
            result["DotNETRuntimePrivate:Fusion2EE"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 40, 120, 0);
            result["DotNETRuntimePrivate:Fusion2EE_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 40, 120, 1);
            result["DotNETRuntimePrivate:CCWRefCountChange"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 40, 193, 0);
            result["DotNETRuntimePrivate:Fusion2EEEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 41, 121, 0);
            result["DotNETRuntimePrivate:Fusion2EEEnd_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 41, 121, 1);
            result["DotNETRuntimePrivate:SecurityCatchCall"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 42, 122, 0);
            result["DotNETRuntimePrivate:SecurityCatchCall_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 42, 122, 1);
            result["DotNETRuntimePrivate:PrvSetGCHandle"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 42, 194, 0);
            result["DotNETRuntimePrivate:SecurityCatchCallEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 43, 123, 0);
            result["DotNETRuntimePrivate:SecurityCatchCallEnd_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 43, 123, 1);
            result["DotNETRuntimePrivate:PrvDestroyGCHandle"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 43, 195, 0);
            result["DotNETRuntimePrivate:PinPlugAtGCTime"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 44, 199, 0);
            result["DotNETRuntimePrivate:BindingPolicyPhaseStart"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 51, 159, 0);
            result["DotNETRuntimePrivate:BindingPolicyPhaseEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 52, 160, 0);
            result["DotNETRuntimePrivate:FailFast"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 52, 191, 0);
            result["DotNETRuntimePrivate:BindingNgenPhaseStart"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 53, 161, 0);
            result["DotNETRuntimePrivate:BindingNgenPhaseEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 54, 162, 0);
            result["DotNETRuntimePrivate:BindingLookupAndProbingPhaseStart"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 55, 163, 0);
            result["DotNETRuntimePrivate:BindingLookupAndProbingPhaseEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 56, 164, 0);
            result["DotNETRuntimePrivate:LoaderPhaseStart"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 57, 165, 0);
            result["DotNETRuntimePrivate:LoaderPhaseEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 58, 166, 0);
            result["DotNETRuntimePrivate:BindingPhaseStart"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 59, 167, 0);
            result["DotNETRuntimePrivate:BindingPhaseEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 60, 168, 0);
            result["DotNETRuntimePrivate:BindingDownloadPhaseStart"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 61, 169, 0);
            result["DotNETRuntimePrivate:BindingDownloadPhaseEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 62, 170, 0);
            result["DotNETRuntimePrivate:LoaderAssemblyInitPhaseStart"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 63, 171, 0);
            result["DotNETRuntimePrivate:LoaderAssemblyInitPhaseEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 64, 172, 0);
            result["DotNETRuntimePrivate:LoaderMappingPhaseStart"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 65, 173, 0);
            result["DotNETRuntimePrivate:LoaderMappingPhaseEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 66, 174, 0);
            result["DotNETRuntimePrivate:LoaderDeliverEventsPhaseStart"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 67, 175, 0);
            result["DotNETRuntimePrivate:LoaderDeliverEventsPhaseEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 68, 176, 0);
            result["DotNETRuntimePrivate:NgenBindEvent"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 69, 188, 0);
            result["DotNETRuntimePrivate:FusionMessageEvent"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 70, 196, 0);
            result["DotNETRuntimePrivate:FusionErrorCodeEvent"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 71, 197, 0);
            result["DotNETRuntimePrivate:CLRStackWalkPrivate"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 82, 151, 0);
            result["DotNETRuntimePrivate:ModuleTransparencyComputationStart"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 83, 178, 0);
            result["DotNETRuntimePrivate:ModuleTransparencyComputationEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 84, 179, 0);
            result["DotNETRuntimePrivate:TypeTransparencyComputationStart"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 85, 180, 0);
            result["DotNETRuntimePrivate:TypeTransparencyComputationEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 86, 181, 0);
            result["DotNETRuntimePrivate:MethodTransparencyComputationStart"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 87, 182, 0);
            result["DotNETRuntimePrivate:MethodTransparencyComputationEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 88, 183, 0);
            result["DotNETRuntimePrivate:FieldTransparencyComputationStart"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 89, 184, 0);
            result["DotNETRuntimePrivate:FieldTransparencyComputationEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 90, 185, 0);
            result["DotNETRuntimePrivate:TokenTransparencyComputationStart"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 91, 186, 0);
            result["DotNETRuntimePrivate:TokenTransparencyComputationEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 92, 187, 0);
            result["DotNETRuntimePrivate:AllocRequest"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 97, 310, 0);
            result["DotNETRuntimePrivate:EEStartupStart"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 128, 80, 0);
            result["DotNETRuntimePrivate:EEStartupStart_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 128, 80, 1);
            result["DotNETRuntimePrivate:EEStartupEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 129, 81, 0);
            result["DotNETRuntimePrivate:EEStartupEnd_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 129, 81, 1);
            result["DotNETRuntimePrivate:EEConfigSetup"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 130, 82, 0);
            result["DotNETRuntimePrivate:EEConfigSetup_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 130, 82, 1);
            result["DotNETRuntimePrivate:EEConfigSetupEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 131, 83, 0);
            result["DotNETRuntimePrivate:EEConfigSetupEnd_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 131, 83, 1);
            result["DotNETRuntimePrivate:GCDecision"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 132, 1, 0);
            result["DotNETRuntimePrivate:GCDecision_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 132, 1, 1);
            result["DotNETRuntimePrivate:LdSysBases"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 132, 84, 0);
            result["DotNETRuntimePrivate:LdSysBases_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 132, 84, 1);
            result["DotNETRuntimePrivate:LdSysBasesEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 133, 85, 0);
            result["DotNETRuntimePrivate:LdSysBasesEnd_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 133, 85, 1);
            result["DotNETRuntimePrivate:ExecExe"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 134, 86, 0);
            result["DotNETRuntimePrivate:ExecExe_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 134, 86, 1);
            result["DotNETRuntimePrivate:ExecExeEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 135, 87, 0);
            result["DotNETRuntimePrivate:ExecExeEnd_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 135, 87, 1);
            result["DotNETRuntimePrivate:Main"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 136, 88, 0);
            result["DotNETRuntimePrivate:Main_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 136, 88, 1);
            result["DotNETRuntimePrivate:MainEnd"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 137, 89, 0);
            result["DotNETRuntimePrivate:MainEnd_V1"] = new ETWMapping(new Guid("763fd754-7086-4dfe-95eb-c01a46faf4ca"), 137, 89, 1);
            result["DotNETRuntime:GCJoin_V2"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 203, 203, 2);
            result["DotNETRuntime:GCBulkSurvivingObjectRanges"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 25, 21, 0);
            result["DotNETRuntime:GCPerHeapHistory_V3_1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 204, 204, 3);
            result["DotNETRuntime:GCHeapStats_V1"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 133, 4, 1);
            result["DotNETRuntime:RuntimeInformationStart"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 1, 187, 0);
            result["DotNETRuntime:ModuleLoad_V2"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 33, 152, 2);
            result["DotNETRuntime:MethodJitInliningFailed"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 84, 186, 0);
            result["DotNETRuntime:MethodJitTailCallSucceeded"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 85, 188, 0);
            result["DotNETRuntime:MethodJitTailCallFailed"] = new ETWMapping(new Guid("47c3ba0c-77f1-4eb0-8d4d-aef447f16a85"), 86, 189, 0);



            // TODO: This event needs to be fixed in the linux source.
            result["DotNETRuntime:MethodILToNativeMap"] = new ETWMapping();


            return result;
        }

        ~CtfTraceEventSource()
        {
            Dispose(false);
        }

        public override int EventsLost
        {
            get { return 0; }
        }

        public override bool Process()
        {
            ulong lastTimestamp = 0;
            int events = 0;
            ChannelList list = new ChannelList(_channels);
            foreach (ChannelEntry entry in list)
            {
                if (stopProcessing)
                    break;

                CtfEventHeader header = entry.Current;
                CtfEvent evt = header.Event;
                lastTimestamp = header.Timestamp;

#if DEBUG
                if (_debugOut != null)
                {
                    _debugOut.WriteLine($"[{evt.Name}]");
                    _debugOut.WriteLine($"    Process: {header.ProcessName}");
                    _debugOut.WriteLine($"    File: {entry.FileName}");
                    _debugOut.WriteLine($"    File Offset: {entry.Channel.FileOffset}");
                    _debugOut.WriteLine($"    Event #{events}");
                    object[] result = entry.Reader.ReadEvent(evt);
                    evt.WriteLine(_debugOut, result, 4);
                }
                else
#endif

                    entry.Reader.ReadEventIntoBuffer(evt);
                events++;

                ETWMapping etw = GetTraceEvent(evt);

                if (etw.IsNull)
                    continue;

                var hdr = InitEventRecord(header, entry.Reader, etw);
                TraceEvent traceEvent = Lookup(hdr);
                traceEvent.eventRecord = hdr;
                traceEvent.userData = entry.Reader.BufferPtr;

                traceEvent.DebugValidate();
                Dispatch(traceEvent);
            }

            sessionEndTimeQPC = (long)lastTimestamp;

            return true;
        }

        private TraceEventNativeMethods.EVENT_RECORD* InitEventRecord(CtfEventHeader header, CtfReader stream, ETWMapping etw)
        {
            _header->EventHeader.Size = (ushort)sizeof(TraceEventNativeMethods.EVENT_TRACE_HEADER);
            _header->EventHeader.Flags = 0;
            if (pointerSize == 8)
                _header->EventHeader.Flags |= TraceEventNativeMethods.EVENT_HEADER_FLAG_64_BIT_HEADER;
            else
                _header->EventHeader.Flags |= TraceEventNativeMethods.EVENT_HEADER_FLAG_32_BIT_HEADER;

            _header->EventHeader.TimeStamp = (long)header.Timestamp;
            _header->EventHeader.ProviderId = etw.Guid;
            _header->EventHeader.Version = etw.Version;
            _header->EventHeader.Level = 0;
            _header->EventHeader.Opcode = (byte)etw.Opcode;
            _header->EventHeader.Id = (ushort)etw.Id;

            _header->UserDataLength = (ushort)stream.BufferLength;
            _header->UserData = stream.BufferPtr;

            // TODO: Set these properties based on Ctf context
            _header->BufferContext = new TraceEventNativeMethods.ETW_BUFFER_CONTEXT();
            _header->BufferContext.ProcessorNumber = 0;
            _header->EventHeader.ThreadId = header.Tid;
            _header->EventHeader.ProcessId = header.Pid;
            _header->EventHeader.KernelTime = 0;
            _header->EventHeader.UserTime = 0;

            return _header;
        }

        private ETWMapping GetTraceEvent(CtfEvent evt)
        {
            ETWMapping result;
            _eventMapping.TryGetValue(evt.Name, out result);

            Debug.Assert(evt.Name.StartsWith("lttng") || _eventMapping.ContainsKey(evt.Name), evt.Name);

            return result;
        }

        public void ParseMetadata()
        {
            // We don't get this data in LTTng traces (unless we decide to emit them as events later).
            osVersion = new Version("0.0.0.0");
            cpuSpeedMHz = 10;

            // TODO:  This is not IFastSerializable
            /*
            var env = _metadata.Environment;
            var trace = _metadata.Trace;
            userData["hostname"] = env.HostName;
            userData["tracer_name"] = env.TracerName;
            userData["tracer_version"] = env.TracerMajor + "." + env.TracerMinor;
            userData["uuid"] = trace.UUID;
            userData["ctf version"] = trace.Major + "." + trace.Minor;
            */
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                _zip.Dispose();

            // TODO
            //Marshal.FreeHGlobal(new IntPtr(_header));
            base.Dispose(disposing);

            GC.SuppressFinalize(this);
        }

        // Each file has streams which have sets of events.  These classes help merge those channels
        // into one chronological stream of events.
        #region Enumeration Helper

        class ChannelList : IEnumerable<ChannelEntry>
        {
            List<Tuple<ZipArchiveEntry, CtfMetadata>> _channels;

            public ChannelList(List<Tuple<ZipArchiveEntry, CtfMetadata>> channels)
            {
                _channels = channels;
            }

            public IEnumerator<ChannelEntry> GetEnumerator()
            {
                return new ChannelListEnumerator(_channels);
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return new ChannelListEnumerator(_channels);
            }
        }

        class ChannelListEnumerator : IEnumerator<ChannelEntry>
        {
            bool _first = true;
            List<ChannelEntry> _channels;
            int _current;

            public ChannelListEnumerator(List<Tuple<ZipArchiveEntry, CtfMetadata>> channels)
            {
                _channels = new List<ChannelEntry>(channels.Select(tuple => new ChannelEntry(tuple.Item1, tuple.Item2)).Where(channel => channel.MoveNext()));
                _current = GetCurrent();
            }

            private int GetCurrent()
            {
                if (_channels.Count == 0)
                    return -1;

                int min = 0;

                for (int i = 1; i < _channels.Count; i++)
                    if (_channels[i].Current.Timestamp < _channels[min].Current.Timestamp)
                        min = i;

                return min;
            }

            public ChannelEntry Current
            {
                get { return _current != -1 ? _channels[_current] : null; }
            }

            public void Dispose()
            {
                foreach (var channel in _channels)
                    channel.Dispose();

                _channels = null;
            }

            object System.Collections.IEnumerator.Current
            {
                get { return Current; }
            }

            public bool MoveNext()
            {
                if (_current == -1)
                    return false;

                if (_first)
                {
                    _first = false;
                    return _channels.Count > 0;
                }

                bool hasMore = _channels[_current].MoveNext();
                if (!hasMore)
                {
                    _channels[_current].Dispose();
                    _channels.RemoveAt(_current);
                }

                _current = GetCurrent();
                return _current != -1;
            }

            public void Reset()
            {
                throw new NotImplementedException();
            }
        }

        class ChannelEntry : IDisposable
        {
            public string FileName { get; private set; }
            public CtfChannel Channel { get; private set; }
            public CtfReader Reader { get; private set; }
            public CtfEventHeader Current { get { return _events.Current; } }

            private Stream _stream;
            private IEnumerator<CtfEventHeader> _events;

            public ChannelEntry(ZipArchiveEntry zip, CtfMetadata metadata)
            {
                FileName = zip.FullName;
                _stream = zip.Open();
                Channel = new CtfChannel(_stream, metadata);
                Reader = new CtfReader(Channel, metadata, Channel.CtfStream);
                _events = Reader.EnumerateEventHeaders().GetEnumerator();
            }

            public void Dispose()
            {
                Reader.Dispose();
                Channel.Dispose();
                _stream.Dispose();

                IDisposable enumerator = _events as IDisposable;
                if (enumerator != null)
                    enumerator.Dispose();
            }

            public bool MoveNext()
            {
                return _events.MoveNext();
            }
        }
        #endregion
    }
}
