using Microsoft.Diagnostics.Tracing.Ctf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;

namespace Microsoft.Diagnostics.Tracing
{
    public sealed class CtfEventMapping
    {
        public string EventName { get; }
        public Guid Guid { get; }
        public TraceEventOpcode Opcode { get; }
        public TraceEventID Id { get; }
        public byte Version { get; }

        public bool IsNull { get { return Guid == new Guid(); } }

        public CtfEventMapping(string eventName, Guid guid, int opcode, int id, int version)
        {
            EventName = eventName;
            Guid = guid;
            Opcode = (TraceEventOpcode)opcode;
            Id = (TraceEventID)id;
            Version = (byte)version;
        }
    }

    public sealed unsafe class CtfTraceEventSource : TraceEventDispatcher, IDisposable
    {
        private string _filename;
        private ZipArchive _zip;
        private List<Tuple<ZipArchiveEntry, CtfMetadata>> _channels;
        private TraceEventNativeMethods.EVENT_RECORD* _header;
        private Dictionary<string, CtfEventMapping> _eventMapping;
        private Dictionary<int, string> _processNames = new Dictionary<int, string>();

#if DEBUG
        private StreamWriter _debugOut;
#endif

        public CtfTraceEventSource(string fileName)
        {
            _filename = fileName;
            _zip = ZipFile.Open(fileName, ZipArchiveMode.Read);
            bool success = false;
            try
            {

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


                var mem = (TraceEventNativeMethods.EVENT_RECORD*)Marshal.AllocHGlobal(sizeof(TraceEventNativeMethods.EVENT_RECORD));
                *mem = default(TraceEventNativeMethods.EVENT_RECORD);
                _header = mem;

                int processors = (from entry in _channels
                                  let filename = entry.Item1.FullName
                                  let i = filename.LastIndexOf('_')
                                  let processor = filename.Substring(i + 1)
                                  select int.Parse(processor)
                                 ).Max() + 1;

                numberOfProcessors = processors;

                // TODO: Need to cleanly separate clocks, but in practice there's only the one clock.
                CtfClock clock = _channels.First().Item2.Clocks.First();

                var firstChannel = (new ChannelList(_channels)).FirstOrDefault();
                if (firstChannel == null)
                {
                    throw new EndOfStreamException("No CTF Information found in ZIP file.");
                }

                long firstEventTimestamp = (long)firstChannel.Current.Timestamp;

                _QPCFreq = (long)clock.Frequency;
                sessionStartTimeQPC = firstEventTimestamp;
                _syncTimeQPC = firstEventTimestamp;
                _syncTimeUTC = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds((clock.Offset - 1) / clock.Frequency);

                success = true;
#if DEBUG
            //// Uncomment for debug output.
            //_debugOut = File.CreateText("debug.txt");
            //_debugOut.AutoFlush = true;
#endif
            }
            finally
            {
                if (!success)
                {
                    Dispose();      // This closes the ZIP file we opened.  We don't want to leave it dangling.  
                }
            }
        }

        private static Dictionary<string, CtfEventMapping> InitEventMap()
        {
            Dictionary<string, CtfEventMapping> result = new Dictionary<string, CtfEventMapping>();

            // Linux Kernel events 
            result["sched_process_exec"] = new CtfEventMapping("sched_process_exec", Parsers.LinuxKernelEventParser.ProviderGuid, 1, 1, 0);
            result["sched_process_exit"] = new CtfEventMapping("sched_process_exit", Parsers.LinuxKernelEventParser.ProviderGuid, 2, 2, 0);
            

            // Public events
            result["DotNETRuntime:GCStart"] = new CtfEventMapping("DotNETRuntime:GCStart", Parsers.ClrTraceEventParser.ProviderGuid, 1, 1, 0);
            result["DotNETRuntime:GCStart_V1"] = new CtfEventMapping("DotNETRuntime:GCStart_V1", Parsers.ClrTraceEventParser.ProviderGuid, 1, 1, 1);
            result["DotNETRuntime:GCStart_V2"] = new CtfEventMapping("DotNETRuntime:GCStart_V2", Parsers.ClrTraceEventParser.ProviderGuid, 1, 1, 2);
            result["DotNETRuntime:WorkerThreadCreate"] = new CtfEventMapping("DotNETRuntime:WorkerThreadCreate", Parsers.ClrTraceEventParser.ProviderGuid, 1, 40, 0);
            result["DotNETRuntime:WorkerThreadRetire"] = new CtfEventMapping("DotNETRuntime:WorkerThreadRetire", Parsers.ClrTraceEventParser.ProviderGuid, 1, 42, 0);
            result["DotNETRuntime:IOThreadCreate"] = new CtfEventMapping("DotNETRuntime:IOThreadCreate", Parsers.ClrTraceEventParser.ProviderGuid, 1, 44, 0);
            result["DotNETRuntime:IOThreadCreate_V1"] = new CtfEventMapping("DotNETRuntime:IOThreadCreate_V1", Parsers.ClrTraceEventParser.ProviderGuid, 1, 44, 1);
            result["DotNETRuntime:IOThreadRetire"] = new CtfEventMapping("DotNETRuntime:IOThreadRetire", Parsers.ClrTraceEventParser.ProviderGuid, 1, 46, 0);
            result["DotNETRuntime:IOThreadRetire_V1"] = new CtfEventMapping("DotNETRuntime:IOThreadRetire_V1", Parsers.ClrTraceEventParser.ProviderGuid, 1, 46, 1);
            result["DotNETRuntime:ThreadpoolSuspensionSuspendThread"] = new CtfEventMapping("DotNETRuntime:ThreadpoolSuspensionSuspendThread", Parsers.ClrTraceEventParser.ProviderGuid, 1, 48, 0);
            result["DotNETRuntime:ThreadPoolWorkerThreadStart"] = new CtfEventMapping("DotNETRuntime:ThreadPoolWorkerThreadStart", Parsers.ClrTraceEventParser.ProviderGuid, 1, 50, 0);
            result["DotNETRuntime:ThreadPoolWorkerThreadRetirementStart"] = new CtfEventMapping("DotNETRuntime:ThreadPoolWorkerThreadRetirementStart", Parsers.ClrTraceEventParser.ProviderGuid, 1, 52, 0);
            result["DotNETRuntime:ThreadPoolWorkingThreadCount"] = new CtfEventMapping("DotNETRuntime:ThreadPoolWorkingThreadCount", Parsers.ClrTraceEventParser.ProviderGuid, 1, 60, 0);
            result["DotNETRuntime:ExceptionThrown"] = new CtfEventMapping("DotNETRuntime:ExceptionThrown", Parsers.ClrTraceEventParser.ProviderGuid, 1, 80, 0);
            result["DotNETRuntime:ExceptionThrown_V1"] = new CtfEventMapping("DotNETRuntime:ExceptionThrown_V1", Parsers.ClrTraceEventParser.ProviderGuid, 1, 80, 1);
            result["DotNETRuntime:LockCreated"] = new CtfEventMapping("DotNETRuntime:LockCreated", Parsers.ClrTraceEventParser.ProviderGuid, 0, 90, 0);
            result["DotNETRuntime:Contention"] = new CtfEventMapping("DotNETRuntime:Contention", Parsers.ClrTraceEventParser.ProviderGuid, 1, 81, 0);
            result["DotNETRuntime:ContentionStart_V1"] = new CtfEventMapping("DotNETRuntime:ContentionStart_V1", Parsers.ClrTraceEventParser.ProviderGuid, 1, 81, 1);
            result["DotNETRuntime:ContentionStart_V2"] = new CtfEventMapping("DotNETRuntime:ContentionStart_V2", Parsers.ClrTraceEventParser.ProviderGuid, 1, 81, 2);
            result["DotNETRuntime:StrongNameVerificationStart"] = new CtfEventMapping("DotNETRuntime:StrongNameVerificationStart", Parsers.ClrTraceEventParser.ProviderGuid, 1, 181, 0);
            result["DotNETRuntime:StrongNameVerificationStart_V1"] = new CtfEventMapping("DotNETRuntime:StrongNameVerificationStart_V1", Parsers.ClrTraceEventParser.ProviderGuid, 1, 181, 1);
            result["DotNETRuntime:AuthenticodeVerificationStart"] = new CtfEventMapping("DotNETRuntime:AuthenticodeVerificationStart", Parsers.ClrTraceEventParser.ProviderGuid, 1, 183, 0);
            result["DotNETRuntime:AuthenticodeVerificationStart_V1"] = new CtfEventMapping("DotNETRuntime:AuthenticodeVerificationStart_V1", Parsers.ClrTraceEventParser.ProviderGuid, 1, 183, 1);
            result["DotNETRuntime:RuntimeInformationStart"] = new CtfEventMapping("DotNETRuntime:RuntimeInformationStart", Parsers.ClrTraceEventParser.ProviderGuid, 1, 187, 0);
            result["DotNETRuntime:DebugIPCEventStart"] = new CtfEventMapping("DotNETRuntime:DebugIPCEventStart", Parsers.ClrTraceEventParser.ProviderGuid, 1, 240, 0);
            result["DotNETRuntime:DebugExceptionProcessingStart"] = new CtfEventMapping("DotNETRuntime:DebugExceptionProcessingStart", Parsers.ClrTraceEventParser.ProviderGuid, 1, 242, 0);
            result["DotNETRuntime:ExceptionCatchStart"] = new CtfEventMapping("DotNETRuntime:ExceptionCatchStart", Parsers.ClrTraceEventParser.ProviderGuid, 1, 250, 0);
            result["DotNETRuntime:ExceptionFinallyStart"] = new CtfEventMapping("DotNETRuntime:ExceptionFinallyStart", Parsers.ClrTraceEventParser.ProviderGuid, 1, 252, 0);
            result["DotNETRuntime:ExceptionFilterStart"] = new CtfEventMapping("DotNETRuntime:ExceptionFilterStart", Parsers.ClrTraceEventParser.ProviderGuid, 1, 254, 0);
            result["DotNETRuntime:CodeSymbols"] = new CtfEventMapping("DotNETRuntime:CodeSymbols", Parsers.ClrTraceEventParser.ProviderGuid, 1, 260, 0);
            result["DotNETRuntime:EventSource"] = new CtfEventMapping("DotNETRuntime:EventSource", Parsers.ClrTraceEventParser.ProviderGuid, 1, 270, 0);
            result["DotNETRuntime:GCEnd"] = new CtfEventMapping("DotNETRuntime:GCEnd", Parsers.ClrTraceEventParser.ProviderGuid, 2, 2, 0);
            result["DotNETRuntime:GCEnd_V1"] = new CtfEventMapping("DotNETRuntime:GCEnd_V1", Parsers.ClrTraceEventParser.ProviderGuid, 2, 2, 1);
            result["DotNETRuntime:WorkerThreadTerminate"] = new CtfEventMapping("DotNETRuntime:WorkerThreadTerminate", Parsers.ClrTraceEventParser.ProviderGuid, 2, 41, 0);
            result["DotNETRuntime:WorkerThreadUnretire"] = new CtfEventMapping("DotNETRuntime:WorkerThreadUnretire", Parsers.ClrTraceEventParser.ProviderGuid, 2, 43, 0);
            result["DotNETRuntime:IOThreadTerminate"] = new CtfEventMapping("DotNETRuntime:IOThreadTerminate", Parsers.ClrTraceEventParser.ProviderGuid, 2, 45, 0);
            result["DotNETRuntime:IOThreadTerminate_V1"] = new CtfEventMapping("DotNETRuntime:IOThreadTerminate_V1", Parsers.ClrTraceEventParser.ProviderGuid, 2, 45, 1);
            result["DotNETRuntime:IOThreadUnretire"] = new CtfEventMapping("DotNETRuntime:IOThreadUnretire", Parsers.ClrTraceEventParser.ProviderGuid, 2, 47, 0);
            result["DotNETRuntime:IOThreadUnretire_V1"] = new CtfEventMapping("DotNETRuntime:IOThreadUnretire_V1", Parsers.ClrTraceEventParser.ProviderGuid, 2, 47, 1);
            result["DotNETRuntime:ThreadpoolSuspensionResumeThread"] = new CtfEventMapping("DotNETRuntime:ThreadpoolSuspensionResumeThread", Parsers.ClrTraceEventParser.ProviderGuid, 2, 49, 0);
            result["DotNETRuntime:ThreadPoolWorkerThreadStop"] = new CtfEventMapping("DotNETRuntime:ThreadPoolWorkerThreadStop", Parsers.ClrTraceEventParser.ProviderGuid, 2, 51, 0);
            result["DotNETRuntime:ThreadPoolWorkerThreadRetirementStop"] = new CtfEventMapping("DotNETRuntime:ThreadPoolWorkerThreadRetirementStop", Parsers.ClrTraceEventParser.ProviderGuid, 2, 53, 0);
            result["DotNETRuntime:ContentionStop"] = new CtfEventMapping("DotNETRuntime:ContentionStop", Parsers.ClrTraceEventParser.ProviderGuid, 2, 91, 0);
            result["DotNETRuntime:ContentionStop_V1"] = new CtfEventMapping("DotNETRuntime:ContentionStop_V1", Parsers.ClrTraceEventParser.ProviderGuid, 2, 91, 1);
            result["DotNETRuntime:StrongNameVerificationStop"] = new CtfEventMapping("DotNETRuntime:StrongNameVerificationStop", Parsers.ClrTraceEventParser.ProviderGuid, 2, 182, 0);
            result["DotNETRuntime:StrongNameVerificationStop_V1"] = new CtfEventMapping("DotNETRuntime:StrongNameVerificationStop_V1", Parsers.ClrTraceEventParser.ProviderGuid, 2, 182, 1);
            result["DotNETRuntime:AuthenticodeVerificationStop"] = new CtfEventMapping("DotNETRuntime:AuthenticodeVerificationStop", Parsers.ClrTraceEventParser.ProviderGuid, 2, 184, 0);
            result["DotNETRuntime:AuthenticodeVerificationStop_V1"] = new CtfEventMapping("DotNETRuntime:AuthenticodeVerificationStop_V1", Parsers.ClrTraceEventParser.ProviderGuid, 2, 184, 1);
            result["DotNETRuntime:DebugIPCEventEnd"] = new CtfEventMapping("DotNETRuntime:DebugIPCEventEnd", Parsers.ClrTraceEventParser.ProviderGuid, 2, 241, 0);
            result["DotNETRuntime:DebugExceptionProcessingEnd"] = new CtfEventMapping("DotNETRuntime:DebugExceptionProcessingEnd", Parsers.ClrTraceEventParser.ProviderGuid, 2, 243, 0);
            result["DotNETRuntime:ExceptionCatchStop"] = new CtfEventMapping("DotNETRuntime:ExceptionCatchStop", Parsers.ClrTraceEventParser.ProviderGuid, 2, 251, 0);
            result["DotNETRuntime:ExceptionFinallyStop"] = new CtfEventMapping("DotNETRuntime:ExceptionFinallyStop", Parsers.ClrTraceEventParser.ProviderGuid, 2, 253, 0);
            result["DotNETRuntime:ExceptionFilterStop"] = new CtfEventMapping("DotNETRuntime:ExceptionFilterStop", Parsers.ClrTraceEventParser.ProviderGuid, 2, 255, 0);
            result["DotNETRuntime:ExceptionThrownStop"] = new CtfEventMapping("DotNETRuntime:ExceptionThrownStop", Parsers.ClrTraceEventParser.ProviderGuid, 2, 256, 0);
            result["DotNETRuntime:GCSuspendEEBegin"] = new CtfEventMapping("DotNETRuntime:GCSuspendEEBegin", Parsers.ClrTraceEventParser.ProviderGuid, 10, 9, 0);
            result["DotNETRuntime:GCSuspendEEBegin_V1"] = new CtfEventMapping("DotNETRuntime:GCSuspendEEBegin_V1", Parsers.ClrTraceEventParser.ProviderGuid, 10, 9, 1);
            result["DotNETRuntime:BulkType"] = new CtfEventMapping("DotNETRuntime:BulkType", Parsers.ClrTraceEventParser.ProviderGuid, 10, 15, 0);
            result["DotNETRuntime:ModuleRangeLoad"] = new CtfEventMapping("DotNETRuntime:ModuleRangeLoad", Parsers.ClrTraceEventParser.ProviderGuid, 10, 158, 0);
            result["DotNETRuntime:GCAllocationTick"] = new CtfEventMapping("DotNETRuntime:GCAllocationTick", Parsers.ClrTraceEventParser.ProviderGuid, 11, 10, 0);
            result["DotNETRuntime:GCAllocationTick_V1"] = new CtfEventMapping("DotNETRuntime:GCAllocationTick_V1", Parsers.ClrTraceEventParser.ProviderGuid, 11, 10, 1);
            result["DotNETRuntime:GCAllocationTick_V2"] = new CtfEventMapping("DotNETRuntime:GCAllocationTick_V2", Parsers.ClrTraceEventParser.ProviderGuid, 11, 10, 2);
            result["DotNETRuntime:GCAllocationTick_V3"] = new CtfEventMapping("DotNETRuntime:GCAllocationTick_V3", Parsers.ClrTraceEventParser.ProviderGuid, 11, 10, 3);
            result["DotNETRuntime:ThreadPoolEnqueue"] = new CtfEventMapping("DotNETRuntime:ThreadPoolEnqueue", Parsers.ClrTraceEventParser.ProviderGuid, 11, 61, 0);
            result["DotNETRuntime:ThreadCreating"] = new CtfEventMapping("DotNETRuntime:ThreadCreating", Parsers.ClrTraceEventParser.ProviderGuid, 11, 70, 0);
            result["DotNETRuntime:GCCreateConcurrentThread"] = new CtfEventMapping("DotNETRuntime:GCCreateConcurrentThread", Parsers.ClrTraceEventParser.ProviderGuid, 12, 11, 0);
            result["DotNETRuntime:GCCreateConcurrentThread_V1"] = new CtfEventMapping("DotNETRuntime:GCCreateConcurrentThread_V1", Parsers.ClrTraceEventParser.ProviderGuid, 12, 11, 1);
            result["DotNETRuntime:ThreadPoolDequeue"] = new CtfEventMapping("DotNETRuntime:ThreadPoolDequeue", Parsers.ClrTraceEventParser.ProviderGuid, 12, 62, 0);
            result["DotNETRuntime:ThreadRunning"] = new CtfEventMapping("DotNETRuntime:ThreadRunning", Parsers.ClrTraceEventParser.ProviderGuid, 12, 71, 0);
            result["DotNETRuntime:GCTerminateConcurrentThread"] = new CtfEventMapping("DotNETRuntime:GCTerminateConcurrentThread", Parsers.ClrTraceEventParser.ProviderGuid, 13, 12, 0);
            result["DotNETRuntime:GCTerminateConcurrentThread_V1"] = new CtfEventMapping("DotNETRuntime:GCTerminateConcurrentThread_V1", Parsers.ClrTraceEventParser.ProviderGuid, 13, 12, 1);
            result["DotNETRuntime:ThreadPoolIOEnqueue"] = new CtfEventMapping("DotNETRuntime:ThreadPoolIOEnqueue", Parsers.ClrTraceEventParser.ProviderGuid, 13, 63, 0);
            result["DotNETRuntime:ThreadPoolIODequeue"] = new CtfEventMapping("DotNETRuntime:ThreadPoolIODequeue", Parsers.ClrTraceEventParser.ProviderGuid, 14, 64, 0);
            result["DotNETRuntime:DCStartCompleteV2"] = new CtfEventMapping("DotNETRuntime:DCStartCompleteV2", Parsers.ClrTraceEventParser.ProviderGuid, 14, 135, 0);
            result["DotNETRuntime:GCFinalizersEnd"] = new CtfEventMapping("DotNETRuntime:GCFinalizersEnd", Parsers.ClrTraceEventParser.ProviderGuid, 15, 13, 0);
            result["DotNETRuntime:GCFinalizersEnd_V1"] = new CtfEventMapping("DotNETRuntime:GCFinalizersEnd_V1", Parsers.ClrTraceEventParser.ProviderGuid, 15, 13, 1);
            result["DotNETRuntime:ThreadPoolIOPack"] = new CtfEventMapping("DotNETRuntime:ThreadPoolIOPack", Parsers.ClrTraceEventParser.ProviderGuid, 15, 65, 0);
            result["DotNETRuntime:DCEndCompleteV2"] = new CtfEventMapping("DotNETRuntime:DCEndCompleteV2", Parsers.ClrTraceEventParser.ProviderGuid, 15, 136, 0);
            result["DotNETRuntime:GCFinalizersBegin"] = new CtfEventMapping("DotNETRuntime:GCFinalizersBegin", Parsers.ClrTraceEventParser.ProviderGuid, 19, 14, 0);
            result["DotNETRuntime:GCFinalizersBegin_V1"] = new CtfEventMapping("DotNETRuntime:GCFinalizersBegin_V1", Parsers.ClrTraceEventParser.ProviderGuid, 19, 14, 1);
            result["DotNETRuntime:GCBulkRootEdge"] = new CtfEventMapping("DotNETRuntime:GCBulkRootEdge", Parsers.ClrTraceEventParser.ProviderGuid, 20, 16, 0);
            result["DotNETRuntime:GCBulkRootConditionalWeakTableElementEdge"] = new CtfEventMapping("DotNETRuntime:GCBulkRootConditionalWeakTableElementEdge", Parsers.ClrTraceEventParser.ProviderGuid, 21, 17, 0);
            result["DotNETRuntime:GCBulkNode"] = new CtfEventMapping("DotNETRuntime:GCBulkNode", Parsers.ClrTraceEventParser.ProviderGuid, 22, 18, 0);
            result["DotNETRuntime:GCBulkEdge"] = new CtfEventMapping("DotNETRuntime:GCBulkEdge", Parsers.ClrTraceEventParser.ProviderGuid, 23, 19, 0);
            result["DotNETRuntime:GCSampledObjectAllocationHigh"] = new CtfEventMapping("DotNETRuntime:GCSampledObjectAllocationHigh", Parsers.ClrTraceEventParser.ProviderGuid, 24, 20, 0);
            result["DotNETRuntime:GCSampledObjectAllocationLow"] = new CtfEventMapping("DotNETRuntime:GCSampledObjectAllocationLow", Parsers.ClrTraceEventParser.ProviderGuid, 24, 32, 0);
            result["DotNETRuntime:GCBulkSurvivingObjectRanges"] = new CtfEventMapping("DotNETRuntime:GCBulkSurvivingObjectRanges", Parsers.ClrTraceEventParser.ProviderGuid, 25, 21, 0);
            result["DotNETRuntime:GCBulkMovedObjectRanges"] = new CtfEventMapping("DotNETRuntime:GCBulkMovedObjectRanges", Parsers.ClrTraceEventParser.ProviderGuid, 26, 22, 0);
            result["DotNETRuntime:GCGenerationRange"] = new CtfEventMapping("DotNETRuntime:GCGenerationRange", Parsers.ClrTraceEventParser.ProviderGuid, 27, 23, 0);
            result["DotNETRuntime:GCMarkStackRoots"] = new CtfEventMapping("DotNETRuntime:GCMarkStackRoots", Parsers.ClrTraceEventParser.ProviderGuid, 28, 25, 0);
            result["DotNETRuntime:GCMarkFinalizeQueueRoots"] = new CtfEventMapping("DotNETRuntime:GCMarkFinalizeQueueRoots", Parsers.ClrTraceEventParser.ProviderGuid, 29, 26, 0);
            result["DotNETRuntime:GCMarkHandles"] = new CtfEventMapping("DotNETRuntime:GCMarkHandles", Parsers.ClrTraceEventParser.ProviderGuid, 30, 27, 0);
            result["DotNETRuntime:GCMarkOlderGenerationRoots"] = new CtfEventMapping("DotNETRuntime:GCMarkOlderGenerationRoots", Parsers.ClrTraceEventParser.ProviderGuid, 31, 28, 0);
            result["DotNETRuntime:FinalizeObject"] = new CtfEventMapping("DotNETRuntime:FinalizeObject", Parsers.ClrTraceEventParser.ProviderGuid, 32, 29, 0);
            result["DotNETRuntime:SetGCHandle"] = new CtfEventMapping("DotNETRuntime:SetGCHandle", Parsers.ClrTraceEventParser.ProviderGuid, 33, 30, 0);
            result["DotNETRuntime:MethodLoad"] = new CtfEventMapping("DotNETRuntime:MethodLoad", Parsers.ClrTraceEventParser.ProviderGuid, 33, 141, 0);
            result["DotNETRuntime:MethodLoad_V1"] = new CtfEventMapping("DotNETRuntime:MethodLoad_V1", Parsers.ClrTraceEventParser.ProviderGuid, 33, 141, 1);
            result["DotNETRuntime:MethodLoad_V2"] = new CtfEventMapping("DotNETRuntime:MethodLoad_V2", Parsers.ClrTraceEventParser.ProviderGuid, 33, 141, 2);
            result["DotNETRuntime:ModuleLoad"] = new CtfEventMapping("DotNETRuntime:ModuleLoad", Parsers.ClrTraceEventParser.ProviderGuid, 33, 152, 0);
            result["DotNETRuntime:ModuleLoad_V1"] = new CtfEventMapping("DotNETRuntime:ModuleLoad_V1", Parsers.ClrTraceEventParser.ProviderGuid, 33, 152, 1);
            result["DotNETRuntime:ModuleLoad_V2"] = new CtfEventMapping("DotNETRuntime:ModuleLoad_V2", Parsers.ClrTraceEventParser.ProviderGuid, 33, 152, 2);
            result["DotNETRuntime:DestroyGCHandle"] = new CtfEventMapping("DotNETRuntime:DestroyGCHandle", Parsers.ClrTraceEventParser.ProviderGuid, 34, 31, 0);
            result["DotNETRuntime:MethodUnload"] = new CtfEventMapping("DotNETRuntime:MethodUnload", Parsers.ClrTraceEventParser.ProviderGuid, 34, 142, 0);
            result["DotNETRuntime:MethodUnload_V1"] = new CtfEventMapping("DotNETRuntime:MethodUnload_V1", Parsers.ClrTraceEventParser.ProviderGuid, 34, 142, 1);
            result["DotNETRuntime:MethodUnload_V2"] = new CtfEventMapping("DotNETRuntime:MethodUnload_V2", Parsers.ClrTraceEventParser.ProviderGuid, 34, 142, 2);
            result["DotNETRuntime:ModuleUnload"] = new CtfEventMapping("DotNETRuntime:ModuleUnload", Parsers.ClrTraceEventParser.ProviderGuid, 34, 153, 0);
            result["DotNETRuntime:ModuleUnload_V1"] = new CtfEventMapping("DotNETRuntime:ModuleUnload_V1", Parsers.ClrTraceEventParser.ProviderGuid, 34, 153, 1);
            result["DotNETRuntime:ModuleUnload_V2"] = new CtfEventMapping("DotNETRuntime:ModuleUnload_V2", Parsers.ClrTraceEventParser.ProviderGuid, 34, 153, 2);
            result["DotNETRuntime:GCTriggered"] = new CtfEventMapping("DotNETRuntime:GCTriggered", Parsers.ClrTraceEventParser.ProviderGuid, 35, 35, 0);
            result["DotNETRuntime:MethodDCStartV2"] = new CtfEventMapping("DotNETRuntime:MethodDCStartV2", Parsers.ClrTraceEventParser.ProviderGuid, 35, 137, 0);
            result["DotNETRuntime:ModuleDCStartV2"] = new CtfEventMapping("DotNETRuntime:ModuleDCStartV2", Parsers.ClrTraceEventParser.ProviderGuid, 35, 149, 0);
            result["DotNETRuntime:PinObjectAtGCTime"] = new CtfEventMapping("DotNETRuntime:PinObjectAtGCTime", Parsers.ClrTraceEventParser.ProviderGuid, 36, 33, 0);
            result["DotNETRuntime:MethodDCEndV2"] = new CtfEventMapping("DotNETRuntime:MethodDCEndV2", Parsers.ClrTraceEventParser.ProviderGuid, 36, 138, 0);
            result["DotNETRuntime:ModuleDCEndV2"] = new CtfEventMapping("DotNETRuntime:ModuleDCEndV2", Parsers.ClrTraceEventParser.ProviderGuid, 36, 150, 0);
            result["DotNETRuntime:MethodLoadVerbose"] = new CtfEventMapping("DotNETRuntime:MethodLoadVerbose", Parsers.ClrTraceEventParser.ProviderGuid, 37, 143, 0);
            result["DotNETRuntime:MethodLoadVerbose_V1"] = new CtfEventMapping("DotNETRuntime:MethodLoadVerbose_V1", Parsers.ClrTraceEventParser.ProviderGuid, 37, 143, 1);
            result["DotNETRuntime:MethodLoadVerbose_V2"] = new CtfEventMapping("DotNETRuntime:MethodLoadVerbose_V2", Parsers.ClrTraceEventParser.ProviderGuid, 37, 143, 2);
            result["DotNETRuntime:AssemblyLoad"] = new CtfEventMapping("DotNETRuntime:AssemblyLoad", Parsers.ClrTraceEventParser.ProviderGuid, 37, 154, 0);
            result["DotNETRuntime:AssemblyLoad_V1"] = new CtfEventMapping("DotNETRuntime:AssemblyLoad_V1", Parsers.ClrTraceEventParser.ProviderGuid, 37, 154, 1);
            result["DotNETRuntime:GCBulkRootCCW"] = new CtfEventMapping("DotNETRuntime:GCBulkRootCCW", Parsers.ClrTraceEventParser.ProviderGuid, 38, 36, 0);
            result["DotNETRuntime:MethodUnloadVerbose"] = new CtfEventMapping("DotNETRuntime:MethodUnloadVerbose", Parsers.ClrTraceEventParser.ProviderGuid, 38, 144, 0);
            result["DotNETRuntime:MethodUnloadVerbose_V1"] = new CtfEventMapping("DotNETRuntime:MethodUnloadVerbose_V1", Parsers.ClrTraceEventParser.ProviderGuid, 38, 144, 1);
            result["DotNETRuntime:MethodUnloadVerbose_V2"] = new CtfEventMapping("DotNETRuntime:MethodUnloadVerbose_V2", Parsers.ClrTraceEventParser.ProviderGuid, 38, 144, 2);
            result["DotNETRuntime:AssemblyUnload"] = new CtfEventMapping("DotNETRuntime:AssemblyUnload", Parsers.ClrTraceEventParser.ProviderGuid, 38, 155, 0);
            result["DotNETRuntime:AssemblyUnload_V1"] = new CtfEventMapping("DotNETRuntime:AssemblyUnload_V1", Parsers.ClrTraceEventParser.ProviderGuid, 38, 155, 1);
            result["DotNETRuntime:GCBulkRCW"] = new CtfEventMapping("DotNETRuntime:GCBulkRCW", Parsers.ClrTraceEventParser.ProviderGuid, 39, 37, 0);
            result["DotNETRuntime:MethodDCStartVerboseV2"] = new CtfEventMapping("DotNETRuntime:MethodDCStartVerboseV2", Parsers.ClrTraceEventParser.ProviderGuid, 39, 139, 0);
            result["DotNETRuntime:GCBulkRootStaticVar"] = new CtfEventMapping("DotNETRuntime:GCBulkRootStaticVar", Parsers.ClrTraceEventParser.ProviderGuid, 40, 38, 0);
            result["DotNETRuntime:MethodDCEndVerboseV2"] = new CtfEventMapping("DotNETRuntime:MethodDCEndVerboseV2", Parsers.ClrTraceEventParser.ProviderGuid, 40, 140, 0);
            result["DotNETRuntime:AppDomainLoad"] = new CtfEventMapping("DotNETRuntime:AppDomainLoad", Parsers.ClrTraceEventParser.ProviderGuid, 41, 156, 0);
            result["DotNETRuntime:AppDomainLoad_V1"] = new CtfEventMapping("DotNETRuntime:AppDomainLoad_V1", Parsers.ClrTraceEventParser.ProviderGuid, 41, 156, 1);
            result["DotNETRuntime:MethodJittingStarted"] = new CtfEventMapping("DotNETRuntime:MethodJittingStarted", Parsers.ClrTraceEventParser.ProviderGuid, 42, 145, 0);
            result["DotNETRuntime:MethodJittingStarted_V1"] = new CtfEventMapping("DotNETRuntime:MethodJittingStarted_V1", Parsers.ClrTraceEventParser.ProviderGuid, 42, 145, 1);
            result["DotNETRuntime:AppDomainUnload"] = new CtfEventMapping("DotNETRuntime:AppDomainUnload", Parsers.ClrTraceEventParser.ProviderGuid, 42, 157, 0);
            result["DotNETRuntime:AppDomainUnload_V1"] = new CtfEventMapping("DotNETRuntime:AppDomainUnload_V1", Parsers.ClrTraceEventParser.ProviderGuid, 42, 157, 1);
            result["DotNETRuntime:DomainModuleLoad"] = new CtfEventMapping("DotNETRuntime:DomainModuleLoad", Parsers.ClrTraceEventParser.ProviderGuid, 45, 151, 0);
            result["DotNETRuntime:DomainModuleLoad_V1"] = new CtfEventMapping("DotNETRuntime:DomainModuleLoad_V1", Parsers.ClrTraceEventParser.ProviderGuid, 45, 151, 1);
            result["DotNETRuntime:AppDomainMemAllocated"] = new CtfEventMapping("DotNETRuntime:AppDomainMemAllocated", Parsers.ClrTraceEventParser.ProviderGuid, 48, 83, 0);
            result["DotNETRuntime:AppDomainMemSurvived"] = new CtfEventMapping("DotNETRuntime:AppDomainMemSurvived", Parsers.ClrTraceEventParser.ProviderGuid, 49, 84, 0);
            result["DotNETRuntime:ThreadCreated"] = new CtfEventMapping("DotNETRuntime:ThreadCreated", Parsers.ClrTraceEventParser.ProviderGuid, 50, 85, 0);
            result["DotNETRuntime:ThreadTerminated"] = new CtfEventMapping("DotNETRuntime:ThreadTerminated", Parsers.ClrTraceEventParser.ProviderGuid, 51, 86, 0);
            result["DotNETRuntime:ThreadDomainEnter"] = new CtfEventMapping("DotNETRuntime:ThreadDomainEnter", Parsers.ClrTraceEventParser.ProviderGuid, 52, 87, 0);
            result["DotNETRuntime:CLRStackWalk"] = new CtfEventMapping("DotNETRuntime:CLRStackWalk", Parsers.ClrTraceEventParser.ProviderGuid, 82, 82, 0);
            result["DotNETRuntime:MethodJitInliningSucceeded"] = new CtfEventMapping("DotNETRuntime:MethodJitInliningSucceeded", Parsers.ClrTraceEventParser.ProviderGuid, 83, 185, 0);
            result["DotNETRuntime:MethodJitInliningFailed"] = new CtfEventMapping("DotNETRuntime:MethodJitInliningFailed", Parsers.ClrTraceEventParser.ProviderGuid, 84, 186, 0);
            result["DotNETRuntime:MethodJitTailCallSucceeded"] = new CtfEventMapping("DotNETRuntime:MethodJitTailCallSucceeded", Parsers.ClrTraceEventParser.ProviderGuid, 85, 188, 0);
            result["DotNETRuntime:MethodJitTailCallFailed"] = new CtfEventMapping("DotNETRuntime:MethodJitTailCallFailed", Parsers.ClrTraceEventParser.ProviderGuid, 86, 189, 0);
            result["DotNETRuntime:MethodILToNativeMap"] = new CtfEventMapping("DotNETRuntime:MethodILToNativeMap", Parsers.ClrTraceEventParser.ProviderGuid, 87, 190, 0);
            result["DotNETRuntime:ILStubGenerated"] = new CtfEventMapping("DotNETRuntime:ILStubGenerated", Parsers.ClrTraceEventParser.ProviderGuid, 88, 88, 0);
            result["DotNETRuntime:ILStubCacheHit"] = new CtfEventMapping("DotNETRuntime:ILStubCacheHit", Parsers.ClrTraceEventParser.ProviderGuid, 89, 89, 0);
            result["DotNETRuntime:ThreadPoolWorkerThreadWait"] = new CtfEventMapping("DotNETRuntime:ThreadPoolWorkerThreadWait", Parsers.ClrTraceEventParser.ProviderGuid, 90, 57, 0);
            result["DotNETRuntime:ThreadPoolWorkerThreadAdjustmentSample"] = new CtfEventMapping("DotNETRuntime:ThreadPoolWorkerThreadAdjustmentSample", Parsers.ClrTraceEventParser.ProviderGuid, 100, 54, 0);
            result["DotNETRuntime:ThreadPoolWorkerThreadAdjustmentAdjustment"] = new CtfEventMapping("DotNETRuntime:ThreadPoolWorkerThreadAdjustmentAdjustment", Parsers.ClrTraceEventParser.ProviderGuid, 101, 55, 0);
            result["DotNETRuntime:ThreadPoolWorkerThreadAdjustmentStats"] = new CtfEventMapping("DotNETRuntime:ThreadPoolWorkerThreadAdjustmentStats", Parsers.ClrTraceEventParser.ProviderGuid, 102, 56, 0);
            result["DotNETRuntime:GCRestartEEEnd"] = new CtfEventMapping("DotNETRuntime:GCRestartEEEnd", Parsers.ClrTraceEventParser.ProviderGuid, 132, 3, 0);
            result["DotNETRuntime:GCRestartEEEnd_V1"] = new CtfEventMapping("DotNETRuntime:GCRestartEEEnd_V1", Parsers.ClrTraceEventParser.ProviderGuid, 132, 3, 1);
            result["DotNETRuntime:GCHeapStats"] = new CtfEventMapping("DotNETRuntime:GCHeapStats", Parsers.ClrTraceEventParser.ProviderGuid, 133, 4, 0);
            result["DotNETRuntime:GCHeapStats_V1"] = new CtfEventMapping("DotNETRuntime:GCHeapStats_V1", Parsers.ClrTraceEventParser.ProviderGuid, 133, 4, 1);
            result["DotNETRuntime:GCCreateSegment"] = new CtfEventMapping("DotNETRuntime:GCCreateSegment", Parsers.ClrTraceEventParser.ProviderGuid, 134, 5, 0);
            result["DotNETRuntime:GCCreateSegment_V1"] = new CtfEventMapping("DotNETRuntime:GCCreateSegment_V1", Parsers.ClrTraceEventParser.ProviderGuid, 134, 5, 1);
            result["DotNETRuntime:GCFreeSegment"] = new CtfEventMapping("DotNETRuntime:GCFreeSegment", Parsers.ClrTraceEventParser.ProviderGuid, 135, 6, 0);
            result["DotNETRuntime:GCFreeSegment_V1"] = new CtfEventMapping("DotNETRuntime:GCFreeSegment_V1", Parsers.ClrTraceEventParser.ProviderGuid, 135, 6, 1);
            result["DotNETRuntime:GCRestartEEBegin"] = new CtfEventMapping("DotNETRuntime:GCRestartEEBegin", Parsers.ClrTraceEventParser.ProviderGuid, 136, 7, 0);
            result["DotNETRuntime:GCRestartEEBegin_V1"] = new CtfEventMapping("DotNETRuntime:GCRestartEEBegin_V1", Parsers.ClrTraceEventParser.ProviderGuid, 136, 7, 1);
            result["DotNETRuntime:GCSuspendEEEnd"] = new CtfEventMapping("DotNETRuntime:GCSuspendEEEnd", Parsers.ClrTraceEventParser.ProviderGuid, 137, 8, 0);
            result["DotNETRuntime:GCSuspendEEEnd_V1"] = new CtfEventMapping("DotNETRuntime:GCSuspendEEEnd_V1", Parsers.ClrTraceEventParser.ProviderGuid, 137, 8, 1);
            result["DotNETRuntime:IncreaseMemoryPressure"] = new CtfEventMapping("DotNETRuntime:IncreaseMemoryPressure", Parsers.ClrTraceEventParser.ProviderGuid, 200, 200, 0);
            result["DotNETRuntime:DecreaseMemoryPressure"] = new CtfEventMapping("DotNETRuntime:DecreaseMemoryPressure", Parsers.ClrTraceEventParser.ProviderGuid, 201, 201, 0);
            result["DotNETRuntime:GCMarkWithType"] = new CtfEventMapping("DotNETRuntime:GCMarkWithType", Parsers.ClrTraceEventParser.ProviderGuid, 202, 202, 0);
            result["DotNETRuntime:GCJoin_V2"] = new CtfEventMapping("DotNETRuntime:GCJoin_V2", Parsers.ClrTraceEventParser.ProviderGuid, 203, 203, 2);
            result["DotNETRuntime:GCPerHeapHistory_V3"] = new CtfEventMapping("DotNETRuntime:GCPerHeapHistory_V3", Parsers.ClrTraceEventParser.ProviderGuid, 204, 204, 3);
            result["DotNETRuntime:GCGlobalHeapHistory_V2"] = new CtfEventMapping("DotNETRuntime:GCGlobalHeapHistory_V2", Parsers.ClrTraceEventParser.ProviderGuid, 205, 205, 2);
            result["DotNETRuntime:GCJoin_V2"] = new CtfEventMapping("DotNETRuntime:GCJoin_V2", Parsers.ClrTraceEventParser.ProviderGuid, 203, 203, 2);
            result["DotNETRuntime:GCBulkSurvivingObjectRanges"] = new CtfEventMapping("DotNETRuntime:GCBulkSurvivingObjectRanges", Parsers.ClrTraceEventParser.ProviderGuid, 25, 21, 0);
            result["DotNETRuntime:GCPerHeapHistory_V3_1"] = new CtfEventMapping("DotNETRuntime:GCPerHeapHistory_V3_1", Parsers.ClrTraceEventParser.ProviderGuid, 204, 204, 3);
            result["DotNETRuntime:TieredCompilationSettings"] = new CtfEventMapping("DotNETRuntime:TieredCompilationSettings", Parsers.ClrTraceEventParser.ProviderGuid, 11, 280, 0);
            result["DotNETRuntime:TieredCompilationPause"] = new CtfEventMapping("DotNETRuntime:TieredCompilationPause", Parsers.ClrTraceEventParser.ProviderGuid, 12, 281, 0);
            result["DotNETRuntime:TieredCompilationResume"] = new CtfEventMapping("DotNETRuntime:TieredCompilationResume", Parsers.ClrTraceEventParser.ProviderGuid, 13, 282, 0);
            result["DotNETRuntime:TieredCompilationBackgroundJitStart"] = new CtfEventMapping("DotNETRuntime:TieredCompilationBackgroundJitStart", Parsers.ClrTraceEventParser.ProviderGuid, 14, 283, 0);
            result["DotNETRuntime:TieredCompilationBackgroundJitStop"] = new CtfEventMapping("DotNETRuntime:TieredCompilationBackgroundJitStop", Parsers.ClrTraceEventParser.ProviderGuid, 15, 284, 0);

            // Rundown events
            result["DotNETRuntimeRundown:TieredCompilationSettingsDCStart"] = new CtfEventMapping("DotNETRuntimeRundown:TieredCompilationSettingsDCStart", Parsers.ClrTraceEventParser.ProviderGuid, 11, 280, 0);

            // Private events
            result["DotNETRuntimePrivate:ApplyPolicyStart"] = new CtfEventMapping("DotNETRuntimePrivate:ApplyPolicyStart", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 10, 90, 0);
            result["DotNETRuntimePrivate:ApplyPolicyStart_V1"] = new CtfEventMapping("DotNETRuntimePrivate:ApplyPolicyStart_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 10, 90, 1);
            result["DotNETRuntimePrivate:ModuleRangeLoadPrivate"] = new CtfEventMapping("DotNETRuntimePrivate:ModuleRangeLoadPrivate", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 10, 158, 0);
            result["DotNETRuntimePrivate:EvidenceGenerated"] = new CtfEventMapping("DotNETRuntimePrivate:EvidenceGenerated", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 10, 177, 0);
            result["DotNETRuntimePrivate:MulticoreJit"] = new CtfEventMapping("DotNETRuntimePrivate:MulticoreJit", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 10, 201, 0);
            result["DotNETRuntimePrivate:ApplyPolicyEnd"] = new CtfEventMapping("DotNETRuntimePrivate:ApplyPolicyEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 11, 91, 0);
            result["DotNETRuntimePrivate:ApplyPolicyEnd_V1"] = new CtfEventMapping("DotNETRuntimePrivate:ApplyPolicyEnd_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 11, 91, 1);
            result["DotNETRuntimePrivate:MulticoreJitMethodCodeReturned"] = new CtfEventMapping("DotNETRuntimePrivate:MulticoreJitMethodCodeReturned", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 11, 202, 0);
            result["DotNETRuntimePrivate:IInspectableRuntimeClassName"] = new CtfEventMapping("DotNETRuntimePrivate:IInspectableRuntimeClassName", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 11, 400, 0);
            result["DotNETRuntimePrivate:LdLibShFolder"] = new CtfEventMapping("DotNETRuntimePrivate:LdLibShFolder", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 12, 92, 0);
            result["DotNETRuntimePrivate:LdLibShFolder_V1"] = new CtfEventMapping("DotNETRuntimePrivate:LdLibShFolder_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 12, 92, 1);
            result["DotNETRuntimePrivate:WinRTUnbox"] = new CtfEventMapping("DotNETRuntimePrivate:WinRTUnbox", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 12, 401, 0);
            result["DotNETRuntimePrivate:LdLibShFolderEnd"] = new CtfEventMapping("DotNETRuntimePrivate:LdLibShFolderEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 13, 93, 0);
            result["DotNETRuntimePrivate:LdLibShFolderEnd_V1"] = new CtfEventMapping("DotNETRuntimePrivate:LdLibShFolderEnd_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 13, 93, 1);
            result["DotNETRuntimePrivate:CreateRCW"] = new CtfEventMapping("DotNETRuntimePrivate:CreateRCW", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 13, 402, 0);
            result["DotNETRuntimePrivate:GCSettings"] = new CtfEventMapping("DotNETRuntimePrivate:GCSettings", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 14, 2, 0);
            result["DotNETRuntimePrivate:GCSettings_V1"] = new CtfEventMapping("DotNETRuntimePrivate:GCSettings_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 14, 2, 1);
            result["DotNETRuntimePrivate:PrestubWorker"] = new CtfEventMapping("DotNETRuntimePrivate:PrestubWorker", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 14, 94, 0);
            result["DotNETRuntimePrivate:PrestubWorker_V1"] = new CtfEventMapping("DotNETRuntimePrivate:PrestubWorker_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 14, 94, 1);
            result["DotNETRuntimePrivate:RCWVariance"] = new CtfEventMapping("DotNETRuntimePrivate:RCWVariance", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 14, 403, 0);
            result["DotNETRuntimePrivate:PrestubWorkerEnd"] = new CtfEventMapping("DotNETRuntimePrivate:PrestubWorkerEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 15, 95, 0);
            result["DotNETRuntimePrivate:PrestubWorkerEnd_V1"] = new CtfEventMapping("DotNETRuntimePrivate:PrestubWorkerEnd_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 15, 95, 1);
            result["DotNETRuntimePrivate:RCWIEnumerableCasting"] = new CtfEventMapping("DotNETRuntimePrivate:RCWIEnumerableCasting", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 15, 404, 0);
            result["DotNETRuntimePrivate:GCOptimized"] = new CtfEventMapping("DotNETRuntimePrivate:GCOptimized", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 16, 3, 0);
            result["DotNETRuntimePrivate:GCOptimized_V1"] = new CtfEventMapping("DotNETRuntimePrivate:GCOptimized_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 16, 3, 1);
            result["DotNETRuntimePrivate:GetInstallationStart"] = new CtfEventMapping("DotNETRuntimePrivate:GetInstallationStart", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 16, 96, 0);
            result["DotNETRuntimePrivate:GetInstallationStart_V1"] = new CtfEventMapping("DotNETRuntimePrivate:GetInstallationStart_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 16, 96, 1);
            result["DotNETRuntimePrivate:CreateCCW"] = new CtfEventMapping("DotNETRuntimePrivate:CreateCCW", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 16, 405, 0);
            result["DotNETRuntimePrivate:GCPerHeapHistory"] = new CtfEventMapping("DotNETRuntimePrivate:GCPerHeapHistory", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 17, 4, 2);
            result["DotNETRuntimePrivate:GCPerHeapHistory_V1"] = new CtfEventMapping("DotNETRuntimePrivate:GCPerHeapHistory_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 17, 4, 1);
            result["DotNETRuntimePrivate:GetInstallationEnd"] = new CtfEventMapping("DotNETRuntimePrivate:GetInstallationEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 17, 97, 0);
            result["DotNETRuntimePrivate:GetInstallationEnd_V1"] = new CtfEventMapping("DotNETRuntimePrivate:GetInstallationEnd_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 17, 97, 1);
            result["DotNETRuntimePrivate:CCWVariance"] = new CtfEventMapping("DotNETRuntimePrivate:CCWVariance", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 17, 406, 0);
            result["DotNETRuntimePrivate:GCGlobalHeapHistory"] = new CtfEventMapping("DotNETRuntimePrivate:GCGlobalHeapHistory", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 18, 5, 0);
            result["DotNETRuntimePrivate:GCGlobalHeapHistory_V1"] = new CtfEventMapping("DotNETRuntimePrivate:GCGlobalHeapHistory_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 18, 5, 1);
            result["DotNETRuntimePrivate:OpenHModule"] = new CtfEventMapping("DotNETRuntimePrivate:OpenHModule", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 18, 98, 0);
            result["DotNETRuntimePrivate:OpenHModule_V1"] = new CtfEventMapping("DotNETRuntimePrivate:OpenHModule_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 18, 98, 1);
            result["DotNETRuntimePrivate:ObjectVariantMarshallingToNative"] = new CtfEventMapping("DotNETRuntimePrivate:ObjectVariantMarshallingToNative", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 18, 407, 0);
            result["DotNETRuntimePrivate:GCFullNotify"] = new CtfEventMapping("DotNETRuntimePrivate:GCFullNotify", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 19, 25, 0);
            result["DotNETRuntimePrivate:GCFullNotify_V1"] = new CtfEventMapping("DotNETRuntimePrivate:GCFullNotify_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 19, 25, 1);
            result["DotNETRuntimePrivate:OpenHModuleEnd"] = new CtfEventMapping("DotNETRuntimePrivate:OpenHModuleEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 19, 99, 0);
            result["DotNETRuntimePrivate:OpenHModuleEnd_V1"] = new CtfEventMapping("DotNETRuntimePrivate:OpenHModuleEnd_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 19, 99, 1);
            result["DotNETRuntimePrivate:GetTypeFromGUID"] = new CtfEventMapping("DotNETRuntimePrivate:GetTypeFromGUID", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 19, 408, 0);
            result["DotNETRuntimePrivate:GCJoin"] = new CtfEventMapping("DotNETRuntimePrivate:GCJoin", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 20, 6, 0);
            result["DotNETRuntimePrivate:GCJoin_V1"] = new CtfEventMapping("DotNETRuntimePrivate:GCJoin_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 20, 6, 1);
            result["DotNETRuntimePrivate:ExplicitBindStart"] = new CtfEventMapping("DotNETRuntimePrivate:ExplicitBindStart", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 20, 100, 0);
            result["DotNETRuntimePrivate:ExplicitBindStart_V1"] = new CtfEventMapping("DotNETRuntimePrivate:ExplicitBindStart_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 20, 100, 1);
            result["DotNETRuntimePrivate:GetTypeFromProgID"] = new CtfEventMapping("DotNETRuntimePrivate:GetTypeFromProgID", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 20, 409, 0);
            result["DotNETRuntimePrivate:PrvGCMarkStackRoots"] = new CtfEventMapping("DotNETRuntimePrivate:PrvGCMarkStackRoots", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 21, 7, 0);
            result["DotNETRuntimePrivate:PrvGCMarkStackRoots_V1"] = new CtfEventMapping("DotNETRuntimePrivate:PrvGCMarkStackRoots_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 21, 7, 1);
            result["DotNETRuntimePrivate:ExplicitBindEnd"] = new CtfEventMapping("DotNETRuntimePrivate:ExplicitBindEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 21, 101, 0);
            result["DotNETRuntimePrivate:ExplicitBindEnd_V1"] = new CtfEventMapping("DotNETRuntimePrivate:ExplicitBindEnd_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 21, 101, 1);
            result["DotNETRuntimePrivate:ConvertToCallbackEtw"] = new CtfEventMapping("DotNETRuntimePrivate:ConvertToCallbackEtw", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 21, 410, 0);
            result["DotNETRuntimePrivate:PrvGCMarkFinalizeQueueRoots"] = new CtfEventMapping("DotNETRuntimePrivate:PrvGCMarkFinalizeQueueRoots", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 22, 8, 0);
            result["DotNETRuntimePrivate:PrvGCMarkFinalizeQueueRoots_V1"] = new CtfEventMapping("DotNETRuntimePrivate:PrvGCMarkFinalizeQueueRoots_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 22, 8, 1);
            result["DotNETRuntimePrivate:ParseXml"] = new CtfEventMapping("DotNETRuntimePrivate:ParseXml", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 22, 102, 0);
            result["DotNETRuntimePrivate:ParseXml_V1"] = new CtfEventMapping("DotNETRuntimePrivate:ParseXml_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 22, 102, 1);
            result["DotNETRuntimePrivate:BeginCreateManagedReference"] = new CtfEventMapping("DotNETRuntimePrivate:BeginCreateManagedReference", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 22, 411, 0);
            result["DotNETRuntimePrivate:PrvGCMarkHandles"] = new CtfEventMapping("DotNETRuntimePrivate:PrvGCMarkHandles", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 23, 9, 0);
            result["DotNETRuntimePrivate:PrvGCMarkHandles_V1"] = new CtfEventMapping("DotNETRuntimePrivate:PrvGCMarkHandles_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 23, 9, 1);
            result["DotNETRuntimePrivate:ParseXmlEnd"] = new CtfEventMapping("DotNETRuntimePrivate:ParseXmlEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 23, 103, 0);
            result["DotNETRuntimePrivate:ParseXmlEnd_V1"] = new CtfEventMapping("DotNETRuntimePrivate:ParseXmlEnd_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 23, 103, 1);
            result["DotNETRuntimePrivate:EndCreateManagedReference"] = new CtfEventMapping("DotNETRuntimePrivate:EndCreateManagedReference", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 23, 412, 0);
            result["DotNETRuntimePrivate:PrvGCMarkCards"] = new CtfEventMapping("DotNETRuntimePrivate:PrvGCMarkCards", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 24, 10, 0);
            result["DotNETRuntimePrivate:PrvGCMarkCards_V1"] = new CtfEventMapping("DotNETRuntimePrivate:PrvGCMarkCards_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 24, 10, 1);
            result["DotNETRuntimePrivate:InitDefaultDomain"] = new CtfEventMapping("DotNETRuntimePrivate:InitDefaultDomain", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 24, 104, 0);
            result["DotNETRuntimePrivate:InitDefaultDomain_V1"] = new CtfEventMapping("DotNETRuntimePrivate:InitDefaultDomain_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 24, 104, 1);
            result["DotNETRuntimePrivate:ObjectVariantMarshallingToManaged"] = new CtfEventMapping("DotNETRuntimePrivate:ObjectVariantMarshallingToManaged", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 24, 413, 0);
            result["DotNETRuntimePrivate:BGCBegin"] = new CtfEventMapping("DotNETRuntimePrivate:BGCBegin", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 25, 11, 0);
            result["DotNETRuntimePrivate:InitDefaultDomainEnd"] = new CtfEventMapping("DotNETRuntimePrivate:InitDefaultDomainEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 25, 105, 0);
            result["DotNETRuntimePrivate:InitDefaultDomainEnd_V1"] = new CtfEventMapping("DotNETRuntimePrivate:InitDefaultDomainEnd_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 25, 105, 1);
            result["DotNETRuntimePrivate:BGC1stNonConEnd"] = new CtfEventMapping("DotNETRuntimePrivate:BGC1stNonConEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 26, 12, 0);
            result["DotNETRuntimePrivate:InitSecurity"] = new CtfEventMapping("DotNETRuntimePrivate:InitSecurity", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 26, 106, 0);
            result["DotNETRuntimePrivate:InitSecurity_V1"] = new CtfEventMapping("DotNETRuntimePrivate:InitSecurity_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 26, 106, 1);
            result["DotNETRuntimePrivate:BGC1stConEnd"] = new CtfEventMapping("DotNETRuntimePrivate:BGC1stConEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 27, 13, 0);
            result["DotNETRuntimePrivate:InitSecurityEnd"] = new CtfEventMapping("DotNETRuntimePrivate:InitSecurityEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 27, 107, 0);
            result["DotNETRuntimePrivate:InitSecurityEnd_V1"] = new CtfEventMapping("DotNETRuntimePrivate:InitSecurityEnd_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 27, 107, 1);
            result["DotNETRuntimePrivate:BGC2ndNonConBegin"] = new CtfEventMapping("DotNETRuntimePrivate:BGC2ndNonConBegin", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 28, 14, 0);
            result["DotNETRuntimePrivate:AllowBindingRedirs"] = new CtfEventMapping("DotNETRuntimePrivate:AllowBindingRedirs", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 28, 108, 0);
            result["DotNETRuntimePrivate:AllowBindingRedirs_V1"] = new CtfEventMapping("DotNETRuntimePrivate:AllowBindingRedirs_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 28, 108, 1);
            result["DotNETRuntimePrivate:BGC2ndNonConEnd"] = new CtfEventMapping("DotNETRuntimePrivate:BGC2ndNonConEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 29, 15, 0);
            result["DotNETRuntimePrivate:AllowBindingRedirsEnd"] = new CtfEventMapping("DotNETRuntimePrivate:AllowBindingRedirsEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 29, 109, 0);
            result["DotNETRuntimePrivate:AllowBindingRedirsEnd_V1"] = new CtfEventMapping("DotNETRuntimePrivate:AllowBindingRedirsEnd_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 29, 109, 1);
            result["DotNETRuntimePrivate:BGC2ndConBegin"] = new CtfEventMapping("DotNETRuntimePrivate:BGC2ndConBegin", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 30, 16, 0);
            result["DotNETRuntimePrivate:EEConfigSync"] = new CtfEventMapping("DotNETRuntimePrivate:EEConfigSync", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 30, 110, 0);
            result["DotNETRuntimePrivate:EEConfigSync_V1"] = new CtfEventMapping("DotNETRuntimePrivate:EEConfigSync_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 30, 110, 1);
            result["DotNETRuntimePrivate:BGC2ndConEnd"] = new CtfEventMapping("DotNETRuntimePrivate:BGC2ndConEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 31, 17, 0);
            result["DotNETRuntimePrivate:EEConfigSyncEnd"] = new CtfEventMapping("DotNETRuntimePrivate:EEConfigSyncEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 31, 111, 0);
            result["DotNETRuntimePrivate:EEConfigSyncEnd_V1"] = new CtfEventMapping("DotNETRuntimePrivate:EEConfigSyncEnd_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 31, 111, 1);
            result["DotNETRuntimePrivate:BGCPlanEnd"] = new CtfEventMapping("DotNETRuntimePrivate:BGCPlanEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 32, 18, 0);
            result["DotNETRuntimePrivate:FusionBinding"] = new CtfEventMapping("DotNETRuntimePrivate:FusionBinding", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 32, 112, 0);
            result["DotNETRuntimePrivate:FusionBinding_V1"] = new CtfEventMapping("DotNETRuntimePrivate:FusionBinding_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 32, 112, 1);
            result["DotNETRuntimePrivate:BGCSweepEnd"] = new CtfEventMapping("DotNETRuntimePrivate:BGCSweepEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 33, 19, 0);
            result["DotNETRuntimePrivate:FusionBindingEnd"] = new CtfEventMapping("DotNETRuntimePrivate:FusionBindingEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 33, 113, 0);
            result["DotNETRuntimePrivate:FusionBindingEnd_V1"] = new CtfEventMapping("DotNETRuntimePrivate:FusionBindingEnd_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 33, 113, 1);
            result["DotNETRuntimePrivate:BGCDrainMark"] = new CtfEventMapping("DotNETRuntimePrivate:BGCDrainMark", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 34, 20, 0);
            result["DotNETRuntimePrivate:LoaderCatchCall"] = new CtfEventMapping("DotNETRuntimePrivate:LoaderCatchCall", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 34, 114, 0);
            result["DotNETRuntimePrivate:LoaderCatchCall_V1"] = new CtfEventMapping("DotNETRuntimePrivate:LoaderCatchCall_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 34, 114, 1);
            result["DotNETRuntimePrivate:BGCRevisit"] = new CtfEventMapping("DotNETRuntimePrivate:BGCRevisit", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 35, 21, 0);
            result["DotNETRuntimePrivate:LoaderCatchCallEnd"] = new CtfEventMapping("DotNETRuntimePrivate:LoaderCatchCallEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 35, 115, 0);
            result["DotNETRuntimePrivate:LoaderCatchCallEnd_V1"] = new CtfEventMapping("DotNETRuntimePrivate:LoaderCatchCallEnd_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 35, 115, 1);
            result["DotNETRuntimePrivate:BGCOverflow"] = new CtfEventMapping("DotNETRuntimePrivate:BGCOverflow", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 36, 22, 0);
            result["DotNETRuntimePrivate:FusionInit"] = new CtfEventMapping("DotNETRuntimePrivate:FusionInit", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 36, 116, 0);
            result["DotNETRuntimePrivate:FusionInit_V1"] = new CtfEventMapping("DotNETRuntimePrivate:FusionInit_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 36, 116, 1);
            result["DotNETRuntimePrivate:BGCAllocWaitBegin"] = new CtfEventMapping("DotNETRuntimePrivate:BGCAllocWaitBegin", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 37, 23, 0);
            result["DotNETRuntimePrivate:FusionInitEnd"] = new CtfEventMapping("DotNETRuntimePrivate:FusionInitEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 37, 117, 0);
            result["DotNETRuntimePrivate:FusionInitEnd_V1"] = new CtfEventMapping("DotNETRuntimePrivate:FusionInitEnd_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 37, 117, 1);
            result["DotNETRuntimePrivate:BGCAllocWaitEnd"] = new CtfEventMapping("DotNETRuntimePrivate:BGCAllocWaitEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 38, 24, 0);
            result["DotNETRuntimePrivate:FusionAppCtx"] = new CtfEventMapping("DotNETRuntimePrivate:FusionAppCtx", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 38, 118, 0);
            result["DotNETRuntimePrivate:FusionAppCtx_V1"] = new CtfEventMapping("DotNETRuntimePrivate:FusionAppCtx_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 38, 118, 1);
            result["DotNETRuntimePrivate:FusionAppCtxEnd"] = new CtfEventMapping("DotNETRuntimePrivate:FusionAppCtxEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 39, 119, 0);
            result["DotNETRuntimePrivate:FusionAppCtxEnd_V1"] = new CtfEventMapping("DotNETRuntimePrivate:FusionAppCtxEnd_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 39, 119, 1);
            result["DotNETRuntimePrivate:PrvFinalizeObject"] = new CtfEventMapping("DotNETRuntimePrivate:PrvFinalizeObject", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 39, 192, 0);
            result["DotNETRuntimePrivate:Fusion2EE"] = new CtfEventMapping("DotNETRuntimePrivate:Fusion2EE", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 40, 120, 0);
            result["DotNETRuntimePrivate:Fusion2EE_V1"] = new CtfEventMapping("DotNETRuntimePrivate:Fusion2EE_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 40, 120, 1);
            result["DotNETRuntimePrivate:CCWRefCountChange"] = new CtfEventMapping("DotNETRuntimePrivate:CCWRefCountChange", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 40, 193, 0);
            result["DotNETRuntimePrivate:Fusion2EEEnd"] = new CtfEventMapping("DotNETRuntimePrivate:Fusion2EEEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 41, 121, 0);
            result["DotNETRuntimePrivate:Fusion2EEEnd_V1"] = new CtfEventMapping("DotNETRuntimePrivate:Fusion2EEEnd_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 41, 121, 1);
            result["DotNETRuntimePrivate:SecurityCatchCall"] = new CtfEventMapping("DotNETRuntimePrivate:SecurityCatchCall", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 42, 122, 0);
            result["DotNETRuntimePrivate:SecurityCatchCall_V1"] = new CtfEventMapping("DotNETRuntimePrivate:SecurityCatchCall_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 42, 122, 1);
            result["DotNETRuntimePrivate:PrvSetGCHandle"] = new CtfEventMapping("DotNETRuntimePrivate:PrvSetGCHandle", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 42, 194, 0);
            result["DotNETRuntimePrivate:SecurityCatchCallEnd"] = new CtfEventMapping("DotNETRuntimePrivate:SecurityCatchCallEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 43, 123, 0);
            result["DotNETRuntimePrivate:SecurityCatchCallEnd_V1"] = new CtfEventMapping("DotNETRuntimePrivate:SecurityCatchCallEnd_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 43, 123, 1);
            result["DotNETRuntimePrivate:PrvDestroyGCHandle"] = new CtfEventMapping("DotNETRuntimePrivate:PrvDestroyGCHandle", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 43, 195, 0);
            result["DotNETRuntimePrivate:PinPlugAtGCTime"] = new CtfEventMapping("DotNETRuntimePrivate:PinPlugAtGCTime", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 44, 199, 0);
            result["DotNETRuntimePrivate:BindingPolicyPhaseStart"] = new CtfEventMapping("DotNETRuntimePrivate:BindingPolicyPhaseStart", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 51, 159, 0);
            result["DotNETRuntimePrivate:BindingPolicyPhaseEnd"] = new CtfEventMapping("DotNETRuntimePrivate:BindingPolicyPhaseEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 52, 160, 0);
            result["DotNETRuntimePrivate:FailFast"] = new CtfEventMapping("DotNETRuntimePrivate:FailFast", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 52, 191, 0);
            result["DotNETRuntimePrivate:BindingNgenPhaseStart"] = new CtfEventMapping("DotNETRuntimePrivate:BindingNgenPhaseStart", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 53, 161, 0);
            result["DotNETRuntimePrivate:BindingNgenPhaseEnd"] = new CtfEventMapping("DotNETRuntimePrivate:BindingNgenPhaseEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 54, 162, 0);
            result["DotNETRuntimePrivate:BindingLookupAndProbingPhaseStart"] = new CtfEventMapping("DotNETRuntimePrivate:BindingLookupAndProbingPhaseStart", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 55, 163, 0);
            result["DotNETRuntimePrivate:BindingLookupAndProbingPhaseEnd"] = new CtfEventMapping("DotNETRuntimePrivate:BindingLookupAndProbingPhaseEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 56, 164, 0);
            result["DotNETRuntimePrivate:LoaderPhaseStart"] = new CtfEventMapping("DotNETRuntimePrivate:LoaderPhaseStart", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 57, 165, 0);
            result["DotNETRuntimePrivate:LoaderPhaseEnd"] = new CtfEventMapping("DotNETRuntimePrivate:LoaderPhaseEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 58, 166, 0);
            result["DotNETRuntimePrivate:BindingPhaseStart"] = new CtfEventMapping("DotNETRuntimePrivate:BindingPhaseStart", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 59, 167, 0);
            result["DotNETRuntimePrivate:BindingPhaseEnd"] = new CtfEventMapping("DotNETRuntimePrivate:BindingPhaseEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 60, 168, 0);
            result["DotNETRuntimePrivate:BindingDownloadPhaseStart"] = new CtfEventMapping("DotNETRuntimePrivate:BindingDownloadPhaseStart", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 61, 169, 0);
            result["DotNETRuntimePrivate:BindingDownloadPhaseEnd"] = new CtfEventMapping("DotNETRuntimePrivate:BindingDownloadPhaseEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 62, 170, 0);
            result["DotNETRuntimePrivate:LoaderAssemblyInitPhaseStart"] = new CtfEventMapping("DotNETRuntimePrivate:LoaderAssemblyInitPhaseStart", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 63, 171, 0);
            result["DotNETRuntimePrivate:LoaderAssemblyInitPhaseEnd"] = new CtfEventMapping("DotNETRuntimePrivate:LoaderAssemblyInitPhaseEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 64, 172, 0);
            result["DotNETRuntimePrivate:LoaderMappingPhaseStart"] = new CtfEventMapping("DotNETRuntimePrivate:LoaderMappingPhaseStart", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 65, 173, 0);
            result["DotNETRuntimePrivate:LoaderMappingPhaseEnd"] = new CtfEventMapping("DotNETRuntimePrivate:LoaderMappingPhaseEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 66, 174, 0);
            result["DotNETRuntimePrivate:LoaderDeliverEventsPhaseStart"] = new CtfEventMapping("DotNETRuntimePrivate:LoaderDeliverEventsPhaseStart", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 67, 175, 0);
            result["DotNETRuntimePrivate:LoaderDeliverEventsPhaseEnd"] = new CtfEventMapping("DotNETRuntimePrivate:LoaderDeliverEventsPhaseEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 68, 176, 0);
            result["DotNETRuntimePrivate:NgenBindEvent"] = new CtfEventMapping("DotNETRuntimePrivate:NgenBindEvent", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 69, 188, 0);
            result["DotNETRuntimePrivate:FusionMessageEvent"] = new CtfEventMapping("DotNETRuntimePrivate:FusionMessageEvent", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 70, 196, 0);
            result["DotNETRuntimePrivate:FusionErrorCodeEvent"] = new CtfEventMapping("DotNETRuntimePrivate:FusionErrorCodeEvent", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 71, 197, 0);
            result["DotNETRuntimePrivate:CLRStackWalkPrivate"] = new CtfEventMapping("DotNETRuntimePrivate:CLRStackWalkPrivate", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 82, 151, 0);
            result["DotNETRuntimePrivate:ModuleTransparencyComputationStart"] = new CtfEventMapping("DotNETRuntimePrivate:ModuleTransparencyComputationStart", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 83, 178, 0);
            result["DotNETRuntimePrivate:ModuleTransparencyComputationEnd"] = new CtfEventMapping("DotNETRuntimePrivate:ModuleTransparencyComputationEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 84, 179, 0);
            result["DotNETRuntimePrivate:TypeTransparencyComputationStart"] = new CtfEventMapping("DotNETRuntimePrivate:TypeTransparencyComputationStart", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 85, 180, 0);
            result["DotNETRuntimePrivate:TypeTransparencyComputationEnd"] = new CtfEventMapping("DotNETRuntimePrivate:TypeTransparencyComputationEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 86, 181, 0);
            result["DotNETRuntimePrivate:MethodTransparencyComputationStart"] = new CtfEventMapping("DotNETRuntimePrivate:MethodTransparencyComputationStart", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 87, 182, 0);
            result["DotNETRuntimePrivate:MethodTransparencyComputationEnd"] = new CtfEventMapping("DotNETRuntimePrivate:MethodTransparencyComputationEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 88, 183, 0);
            result["DotNETRuntimePrivate:FieldTransparencyComputationStart"] = new CtfEventMapping("DotNETRuntimePrivate:FieldTransparencyComputationStart", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 89, 184, 0);
            result["DotNETRuntimePrivate:FieldTransparencyComputationEnd"] = new CtfEventMapping("DotNETRuntimePrivate:FieldTransparencyComputationEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 90, 185, 0);
            result["DotNETRuntimePrivate:TokenTransparencyComputationStart"] = new CtfEventMapping("DotNETRuntimePrivate:TokenTransparencyComputationStart", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 91, 186, 0);
            result["DotNETRuntimePrivate:TokenTransparencyComputationEnd"] = new CtfEventMapping("DotNETRuntimePrivate:TokenTransparencyComputationEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 92, 187, 0);
            result["DotNETRuntimePrivate:AllocRequest"] = new CtfEventMapping("DotNETRuntimePrivate:AllocRequest", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 97, 310, 0);
            result["DotNETRuntimePrivate:EEStartupStart"] = new CtfEventMapping("DotNETRuntimePrivate:EEStartupStart", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 128, 80, 0);
            result["DotNETRuntimePrivate:EEStartupStart_V1"] = new CtfEventMapping("DotNETRuntimePrivate:EEStartupStart_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 128, 80, 1);
            result["DotNETRuntimePrivate:EEStartupEnd"] = new CtfEventMapping("DotNETRuntimePrivate:EEStartupEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 129, 81, 0);
            result["DotNETRuntimePrivate:EEStartupEnd_V1"] = new CtfEventMapping("DotNETRuntimePrivate:EEStartupEnd_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 129, 81, 1);
            result["DotNETRuntimePrivate:EEConfigSetup"] = new CtfEventMapping("DotNETRuntimePrivate:EEConfigSetup", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 130, 82, 0);
            result["DotNETRuntimePrivate:EEConfigSetup_V1"] = new CtfEventMapping("DotNETRuntimePrivate:EEConfigSetup_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 130, 82, 1);
            result["DotNETRuntimePrivate:EEConfigSetupEnd"] = new CtfEventMapping("DotNETRuntimePrivate:EEConfigSetupEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 131, 83, 0);
            result["DotNETRuntimePrivate:EEConfigSetupEnd_V1"] = new CtfEventMapping("DotNETRuntimePrivate:EEConfigSetupEnd_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 131, 83, 1);
            result["DotNETRuntimePrivate:GCDecision"] = new CtfEventMapping("DotNETRuntimePrivate:GCDecision", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 132, 1, 0);
            result["DotNETRuntimePrivate:GCDecision_V1"] = new CtfEventMapping("DotNETRuntimePrivate:GCDecision_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 132, 1, 1);
            result["DotNETRuntimePrivate:LdSysBases"] = new CtfEventMapping("DotNETRuntimePrivate:LdSysBases", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 132, 84, 0);
            result["DotNETRuntimePrivate:LdSysBases_V1"] = new CtfEventMapping("DotNETRuntimePrivate:LdSysBases_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 132, 84, 1);
            result["DotNETRuntimePrivate:LdSysBasesEnd"] = new CtfEventMapping("DotNETRuntimePrivate:LdSysBasesEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 133, 85, 0);
            result["DotNETRuntimePrivate:LdSysBasesEnd_V1"] = new CtfEventMapping("DotNETRuntimePrivate:LdSysBasesEnd_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 133, 85, 1);
            result["DotNETRuntimePrivate:ExecExe"] = new CtfEventMapping("DotNETRuntimePrivate:ExecExe", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 134, 86, 0);
            result["DotNETRuntimePrivate:ExecExe_V1"] = new CtfEventMapping("DotNETRuntimePrivate:ExecExe_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 134, 86, 1);
            result["DotNETRuntimePrivate:ExecExeEnd"] = new CtfEventMapping("DotNETRuntimePrivate:ExecExeEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 135, 87, 0);
            result["DotNETRuntimePrivate:ExecExeEnd_V1"] = new CtfEventMapping("DotNETRuntimePrivate:ExecExeEnd_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 135, 87, 1);
            result["DotNETRuntimePrivate:Main"] = new CtfEventMapping("DotNETRuntimePrivate:Main", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 136, 88, 0);
            result["DotNETRuntimePrivate:Main_V1"] = new CtfEventMapping("DotNETRuntimePrivate:Main_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 136, 88, 1);
            result["DotNETRuntimePrivate:MainEnd"] = new CtfEventMapping("DotNETRuntimePrivate:MainEnd", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 137, 89, 0);
            result["DotNETRuntimePrivate:MainEnd_V1"] = new CtfEventMapping("DotNETRuntimePrivate:MainEnd_V1", Parsers.ClrPrivateTraceEventParser.ProviderGuid, 137, 89, 1);

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
                {
                    break;
                }

                CtfEventHeader header = entry.Current;
                CtfEvent evt = header.Event;
                lastTimestamp = header.Timestamp;

                entry.Reader.ReadEventIntoBuffer(evt);
                events++;

#if DEBUG
                if (_debugOut != null)
                {
                    _debugOut.WriteLine($"[{evt.Name}]");
                    _debugOut.WriteLine($"    Process: {header.ProcessName}");
                    _debugOut.WriteLine($"    File: {entry.FileName}");
                    _debugOut.WriteLine($"    File Offset: {entry.Channel.FileOffset}");
                    _debugOut.WriteLine($"    Event #{events}: {evt.Name}");
                }
#endif

                CtfEventMapping mapping = GetEventMapping(evt);
                if (mapping.IsNull)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(header.ProcessName))
                {
                    _processNames[header.Pid] = header.ProcessName;
                }

                var hdr = InitEventRecord(header, entry.Reader, mapping);
                TraceEvent traceEvent = Lookup(hdr);
                traceEvent.eventRecord = hdr;
                traceEvent.userData = entry.Reader.BufferPtr;
                traceEvent.EventTypeUserData = evt;

                traceEvent.DebugValidate();
                Dispatch(traceEvent);
            }

            sessionEndTimeQPC = (long)lastTimestamp;

            return true;
        }

        internal override string ProcessName(int processID, long timeQPC)
        {
            string result;

            if (_processNames.TryGetValue(processID, out result))
            {
                return result;
            }

            return base.ProcessName(processID, timeQPC);
        }

        internal override void RegisterParserImpl(TraceEventParser parser)
        {
            base.RegisterParserImpl(parser);
            foreach (var mapping in parser.EnumerateCtfEventMappings())
            {
                _eventMapping.Add(mapping.EventName, mapping);
            }
        }

        private TraceEventNativeMethods.EVENT_RECORD* InitEventRecord(CtfEventHeader header, CtfReader stream, CtfEventMapping mapping)
        {
            _header->EventHeader.Size = (ushort)sizeof(TraceEventNativeMethods.EVENT_TRACE_HEADER);
            _header->EventHeader.Flags = 0;
            if (pointerSize == 8)
            {
                _header->EventHeader.Flags |= TraceEventNativeMethods.EVENT_HEADER_FLAG_64_BIT_HEADER;
            }
            else
            {
                _header->EventHeader.Flags |= TraceEventNativeMethods.EVENT_HEADER_FLAG_32_BIT_HEADER;
            }

            _header->EventHeader.TimeStamp = (long)header.Timestamp;
            _header->EventHeader.ProviderId = mapping.Guid;
            _header->EventHeader.Version = mapping.Version;
            _header->EventHeader.Level = 0;
            _header->EventHeader.Opcode = (byte)mapping.Opcode;
            _header->EventHeader.Id = (ushort)mapping.Id;

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

        private CtfEventMapping GetEventMapping(CtfEvent evt)
        {
            var found = _eventMapping.TryGetValue(evt.Name, out var result);

            Debug.Assert(evt.Name.StartsWith("lttng") || found, evt.Name);

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
            {
                if (_zip != null)
                {
                    _zip.Dispose();
                    _zip = null;
                }
            }

            // TODO
            //Marshal.FreeHGlobal(new IntPtr(_header));
            base.Dispose(disposing);

            GC.SuppressFinalize(this);
        }

        // Each file has streams which have sets of events.  These classes help merge those channels
        // into one chronological stream of events.
        #region Enumeration Helper

        private class ChannelList : IEnumerable<ChannelEntry>
        {
            private List<Tuple<ZipArchiveEntry, CtfMetadata>> _channels;

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

        private class ChannelListEnumerator : IEnumerator<ChannelEntry>
        {
            private bool _first = true;
            private List<ChannelEntry> _channels;
            private int _current;

            public ChannelListEnumerator(List<Tuple<ZipArchiveEntry, CtfMetadata>> channels)
            {
                _channels = new List<ChannelEntry>(channels.Select(tuple => new ChannelEntry(tuple.Item1, tuple.Item2)).Where(channel => channel.MoveNext()));
                _current = GetCurrent();
            }

            private int GetCurrent()
            {
                if (_channels.Count == 0)
                {
                    return -1;
                }

                int min = 0;

                for (int i = 1; i < _channels.Count; i++)
                {
                    if (_channels[i].Current.Timestamp < _channels[min].Current.Timestamp)
                    {
                        min = i;
                    }
                }

                return min;
            }

            public ChannelEntry Current
            {
                get { return _current != -1 ? _channels[_current] : null; }
            }

            public void Dispose()
            {
                foreach (var channel in _channels)
                {
                    channel.Dispose();
                }

                _channels = null;
            }

            object System.Collections.IEnumerator.Current
            {
                get { return Current; }
            }

            public bool MoveNext()
            {
                if (_current == -1)
                {
                    return false;
                }

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

        private class ChannelEntry : IDisposable
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
                {
                    enumerator.Dispose();
                }
            }

            public bool MoveNext()
            {
                return _events.MoveNext();
            }
        }
        #endregion
    }
}
