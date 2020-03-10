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

    public sealed unsafe class CtfTraceEventSource : TraceEventDispatcher, IDisposable
    {
        private string _filename;
        private ZipArchive _zip;
        private List<Tuple<ZipArchiveEntry, CtfMetadata>> _channels;
        private TraceEventNativeMethods.EVENT_RECORD* _header;
        private Dictionary<string, ETWMapping> _eventMapping;
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
                foreach (ZipArchiveEntry metadataArchive in _zip.Entries.Where(p => Path.GetFileName(p.FullName) == "metadata" && p.FullName.Contains("ust")))
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

                _eventMapping = InitEventMap();
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

        private static Dictionary<string, ETWMapping> InitEventMap()
        {
            Dictionary<string, ETWMapping> result = new Dictionary<string, ETWMapping>();

            // Public events
            result["DotNETRuntime:GCStart"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 1, 1, 0);
            result["DotNETRuntime:GCStart_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 1, 1, 1);
            result["DotNETRuntime:GCStart_V2"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 1, 1, 2);
            result["DotNETRuntime:WorkerThreadCreate"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 1, 40, 0);
            result["DotNETRuntime:WorkerThreadRetire"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 1, 42, 0);
            result["DotNETRuntime:IOThreadCreate"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 1, 44, 0);
            result["DotNETRuntime:IOThreadCreate_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 1, 44, 1);
            result["DotNETRuntime:IOThreadRetire"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 1, 46, 0);
            result["DotNETRuntime:IOThreadRetire_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 1, 46, 1);
            result["DotNETRuntime:ThreadpoolSuspensionSuspendThread"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 1, 48, 0);
            result["DotNETRuntime:ThreadPoolWorkerThreadStart"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 1, 50, 0);
            result["DotNETRuntime:ThreadPoolWorkerThreadRetirementStart"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 1, 52, 0);
            result["DotNETRuntime:ThreadPoolWorkingThreadCount"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 1, 60, 0);
            result["DotNETRuntime:ExceptionThrown"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 1, 80, 0);
            result["DotNETRuntime:ExceptionThrown_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 1, 80, 1);
            result["DotNETRuntime:Contention"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 1, 81, 0);
            result["DotNETRuntime:ContentionStart_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 1, 81, 1);
            result["DotNETRuntime:StrongNameVerificationStart"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 1, 181, 0);
            result["DotNETRuntime:StrongNameVerificationStart_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 1, 181, 1);
            result["DotNETRuntime:AuthenticodeVerificationStart"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 1, 183, 0);
            result["DotNETRuntime:AuthenticodeVerificationStart_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 1, 183, 1);
            result["DotNETRuntime:RuntimeInformationStart"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 1, 187, 0);
            result["DotNETRuntime:DebugIPCEventStart"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 1, 240, 0);
            result["DotNETRuntime:DebugExceptionProcessingStart"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 1, 242, 0);
            result["DotNETRuntime:ExceptionCatchStart"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 1, 250, 0);
            result["DotNETRuntime:ExceptionFinallyStart"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 1, 252, 0);
            result["DotNETRuntime:ExceptionFilterStart"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 1, 254, 0);
            result["DotNETRuntime:CodeSymbols"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 1, 260, 0);
            result["DotNETRuntime:EventSource"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 1, 270, 0);
            result["DotNETRuntime:GCEnd"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 2, 2, 0);
            result["DotNETRuntime:GCEnd_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 2, 2, 1);
            result["DotNETRuntime:WorkerThreadTerminate"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 2, 41, 0);
            result["DotNETRuntime:WorkerThreadUnretire"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 2, 43, 0);
            result["DotNETRuntime:IOThreadTerminate"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 2, 45, 0);
            result["DotNETRuntime:IOThreadTerminate_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 2, 45, 1);
            result["DotNETRuntime:IOThreadUnretire"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 2, 47, 0);
            result["DotNETRuntime:IOThreadUnretire_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 2, 47, 1);
            result["DotNETRuntime:ThreadpoolSuspensionResumeThread"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 2, 49, 0);
            result["DotNETRuntime:ThreadPoolWorkerThreadStop"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 2, 51, 0);
            result["DotNETRuntime:ThreadPoolWorkerThreadRetirementStop"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 2, 53, 0);
            result["DotNETRuntime:ContentionStop"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 2, 91, 0);
            result["DotNETRuntime:StrongNameVerificationStop"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 2, 182, 0);
            result["DotNETRuntime:StrongNameVerificationStop_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 2, 182, 1);
            result["DotNETRuntime:AuthenticodeVerificationStop"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 2, 184, 0);
            result["DotNETRuntime:AuthenticodeVerificationStop_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 2, 184, 1);
            result["DotNETRuntime:DebugIPCEventEnd"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 2, 241, 0);
            result["DotNETRuntime:DebugExceptionProcessingEnd"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 2, 243, 0);
            result["DotNETRuntime:ExceptionCatchStop"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 2, 251, 0);
            result["DotNETRuntime:ExceptionFinallyStop"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 2, 253, 0);
            result["DotNETRuntime:ExceptionFilterStop"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 2, 255, 0);
            result["DotNETRuntime:ExceptionThrownStop"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 2, 256, 0);
            result["DotNETRuntime:GCSuspendEEBegin"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 10, 9, 0);
            result["DotNETRuntime:GCSuspendEEBegin_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 10, 9, 1);
            result["DotNETRuntime:BulkType"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 10, 15, 0);
            result["DotNETRuntime:ModuleRangeLoad"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 10, 158, 0);
            result["DotNETRuntime:GCAllocationTick"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 11, 10, 0);
            result["DotNETRuntime:GCAllocationTick_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 11, 10, 1);
            result["DotNETRuntime:GCAllocationTick_V2"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 11, 10, 2);
            result["DotNETRuntime:GCAllocationTick_V3"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 11, 10, 3);
            result["DotNETRuntime:ThreadPoolEnqueue"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 11, 61, 0);
            result["DotNETRuntime:ThreadCreating"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 11, 70, 0);
            result["DotNETRuntime:GCCreateConcurrentThread"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 12, 11, 0);
            result["DotNETRuntime:GCCreateConcurrentThread_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 12, 11, 1);
            result["DotNETRuntime:ThreadPoolDequeue"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 12, 62, 0);
            result["DotNETRuntime:ThreadRunning"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 12, 71, 0);
            result["DotNETRuntime:GCTerminateConcurrentThread"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 13, 12, 0);
            result["DotNETRuntime:GCTerminateConcurrentThread_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 13, 12, 1);
            result["DotNETRuntime:ThreadPoolIOEnqueue"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 13, 63, 0);
            result["DotNETRuntime:ThreadPoolIODequeue"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 14, 64, 0);
            result["DotNETRuntime:DCStartCompleteV2"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 14, 135, 0);
            result["DotNETRuntime:GCFinalizersEnd"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 15, 13, 0);
            result["DotNETRuntime:GCFinalizersEnd_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 15, 13, 1);
            result["DotNETRuntime:ThreadPoolIOPack"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 15, 65, 0);
            result["DotNETRuntime:DCEndCompleteV2"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 15, 136, 0);
            result["DotNETRuntime:GCFinalizersBegin"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 19, 14, 0);
            result["DotNETRuntime:GCFinalizersBegin_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 19, 14, 1);
            result["DotNETRuntime:GCBulkRootEdge"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 20, 16, 0);
            result["DotNETRuntime:GCBulkRootConditionalWeakTableElementEdge"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 21, 17, 0);
            result["DotNETRuntime:GCBulkNode"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 22, 18, 0);
            result["DotNETRuntime:GCBulkEdge"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 23, 19, 0);
            result["DotNETRuntime:GCSampledObjectAllocationHigh"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 24, 20, 0);
            result["DotNETRuntime:GCSampledObjectAllocationLow"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 24, 32, 0);
            result["DotNETRuntime:GCBulkSurvivingObjectRanges"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 25, 21, 0);
            result["DotNETRuntime:GCBulkMovedObjectRanges"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 26, 22, 0);
            result["DotNETRuntime:GCGenerationRange"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 27, 23, 0);
            result["DotNETRuntime:GCMarkStackRoots"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 28, 25, 0);
            result["DotNETRuntime:GCMarkFinalizeQueueRoots"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 29, 26, 0);
            result["DotNETRuntime:GCMarkHandles"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 30, 27, 0);
            result["DotNETRuntime:GCMarkOlderGenerationRoots"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 31, 28, 0);
            result["DotNETRuntime:FinalizeObject"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 32, 29, 0);
            result["DotNETRuntime:SetGCHandle"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 33, 30, 0);
            result["DotNETRuntime:MethodLoad"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 33, 141, 0);
            result["DotNETRuntime:MethodLoad_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 33, 141, 1);
            result["DotNETRuntime:MethodLoad_V2"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 33, 141, 2);
            result["DotNETRuntime:ModuleLoad"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 33, 152, 0);
            result["DotNETRuntime:ModuleLoad_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 33, 152, 1);
            result["DotNETRuntime:ModuleLoad_V2"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 33, 152, 2);
            result["DotNETRuntime:DestroyGCHandle"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 34, 31, 0);
            result["DotNETRuntime:MethodUnload"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 34, 142, 0);
            result["DotNETRuntime:MethodUnload_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 34, 142, 1);
            result["DotNETRuntime:MethodUnload_V2"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 34, 142, 2);
            result["DotNETRuntime:ModuleUnload"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 34, 153, 0);
            result["DotNETRuntime:ModuleUnload_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 34, 153, 1);
            result["DotNETRuntime:ModuleUnload_V2"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 34, 153, 2);
            result["DotNETRuntime:GCTriggered"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 35, 35, 0);
            result["DotNETRuntime:MethodDCStartV2"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 35, 137, 0);
            result["DotNETRuntime:ModuleDCStartV2"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 35, 149, 0);
            result["DotNETRuntime:PinObjectAtGCTime"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 36, 33, 0);
            result["DotNETRuntime:MethodDCEndV2"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 36, 138, 0);
            result["DotNETRuntime:ModuleDCEndV2"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 36, 150, 0);
            result["DotNETRuntime:MethodLoadVerbose"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 37, 143, 0);
            result["DotNETRuntime:MethodLoadVerbose_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 37, 143, 1);
            result["DotNETRuntime:MethodLoadVerbose_V2"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 37, 143, 2);
            result["DotNETRuntime:AssemblyLoad"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 37, 154, 0);
            result["DotNETRuntime:AssemblyLoad_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 37, 154, 1);
            result["DotNETRuntime:GCBulkRootCCW"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 38, 36, 0);
            result["DotNETRuntime:MethodUnloadVerbose"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 38, 144, 0);
            result["DotNETRuntime:MethodUnloadVerbose_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 38, 144, 1);
            result["DotNETRuntime:MethodUnloadVerbose_V2"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 38, 144, 2);
            result["DotNETRuntime:AssemblyUnload"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 38, 155, 0);
            result["DotNETRuntime:AssemblyUnload_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 38, 155, 1);
            result["DotNETRuntime:GCBulkRCW"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 39, 37, 0);
            result["DotNETRuntime:MethodDCStartVerboseV2"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 39, 139, 0);
            result["DotNETRuntime:GCBulkRootStaticVar"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 40, 38, 0);
            result["DotNETRuntime:MethodDCEndVerboseV2"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 40, 140, 0);
            result["DotNETRuntime:AppDomainLoad"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 41, 156, 0);
            result["DotNETRuntime:AppDomainLoad_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 41, 156, 1);
            result["DotNETRuntime:MethodJittingStarted"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 42, 145, 0);
            result["DotNETRuntime:MethodJittingStarted_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 42, 145, 1);
            result["DotNETRuntime:AppDomainUnload"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 42, 157, 0);
            result["DotNETRuntime:AppDomainUnload_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 42, 157, 1);
            result["DotNETRuntime:DomainModuleLoad"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 45, 151, 0);
            result["DotNETRuntime:DomainModuleLoad_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 45, 151, 1);
            result["DotNETRuntime:AppDomainMemAllocated"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 48, 83, 0);
            result["DotNETRuntime:AppDomainMemSurvived"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 49, 84, 0);
            result["DotNETRuntime:ThreadCreated"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 50, 85, 0);
            result["DotNETRuntime:ThreadTerminated"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 51, 86, 0);
            result["DotNETRuntime:ThreadDomainEnter"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 52, 87, 0);
            result["DotNETRuntime:CLRStackWalk"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 82, 82, 0);
            result["DotNETRuntime:MethodJitInliningSucceeded"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 83, 185, 0);
            result["DotNETRuntime:MethodJitInliningFailed"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 84, 186, 0);
            result["DotNETRuntime:MethodJitTailCallSucceeded"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 85, 188, 0);
            result["DotNETRuntime:MethodJitTailCallFailed"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 86, 189, 0);
            result["DotNETRuntime:MethodILToNativeMap"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 87, 190, 0);
            result["DotNETRuntime:ILStubGenerated"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 88, 88, 0);
            result["DotNETRuntime:ILStubCacheHit"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 89, 89, 0);
            result["DotNETRuntime:ThreadPoolWorkerThreadWait"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 90, 57, 0);
            result["DotNETRuntime:ThreadPoolWorkerThreadAdjustmentSample"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 100, 54, 0);
            result["DotNETRuntime:ThreadPoolWorkerThreadAdjustmentAdjustment"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 101, 55, 0);
            result["DotNETRuntime:ThreadPoolWorkerThreadAdjustmentStats"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 102, 56, 0);
            result["DotNETRuntime:GCRestartEEEnd"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 132, 3, 0);
            result["DotNETRuntime:GCRestartEEEnd_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 132, 3, 1);
            result["DotNETRuntime:GCHeapStats"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 133, 4, 0);
            result["DotNETRuntime:GCHeapStats_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 133, 4, 1);
            result["DotNETRuntime:GCCreateSegment"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 134, 5, 0);
            result["DotNETRuntime:GCCreateSegment_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 134, 5, 1);
            result["DotNETRuntime:GCFreeSegment"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 135, 6, 0);
            result["DotNETRuntime:GCFreeSegment_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 135, 6, 1);
            result["DotNETRuntime:GCRestartEEBegin"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 136, 7, 0);
            result["DotNETRuntime:GCRestartEEBegin_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 136, 7, 1);
            result["DotNETRuntime:GCSuspendEEEnd"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 137, 8, 0);
            result["DotNETRuntime:GCSuspendEEEnd_V1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 137, 8, 1);
            result["DotNETRuntime:IncreaseMemoryPressure"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 200, 200, 0);
            result["DotNETRuntime:DecreaseMemoryPressure"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 201, 201, 0);
            result["DotNETRuntime:GCMarkWithType"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 202, 202, 0);
            result["DotNETRuntime:GCJoin_V2"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 203, 203, 2);
            result["DotNETRuntime:GCPerHeapHistory_V3"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 204, 204, 3);
            result["DotNETRuntime:GCGlobalHeapHistory_V2"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 205, 205, 2);
            result["DotNETRuntime:GCJoin_V2"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 203, 203, 2);
            result["DotNETRuntime:GCBulkSurvivingObjectRanges"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 25, 21, 0);
            result["DotNETRuntime:GCPerHeapHistory_V3_1"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 204, 204, 3);
            result["DotNETRuntime:TieredCompilationSettings"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 11, 280, 0);
            result["DotNETRuntime:TieredCompilationPause"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 12, 281, 0);
            result["DotNETRuntime:TieredCompilationResume"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 13, 282, 0);
            result["DotNETRuntime:TieredCompilationBackgroundJitStart"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 14, 283, 0);
            result["DotNETRuntime:TieredCompilationBackgroundJitStop"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 15, 284, 0);

            // Rundown events
            result["DotNETRuntimeRundown:TieredCompilationSettingsDCStart"] = new ETWMapping(Parsers.ClrTraceEventParser.ProviderGuid, 11, 280, 0);

            // Private events
            result["DotNETRuntimePrivate:ApplyPolicyStart"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 10, 90, 0);
            result["DotNETRuntimePrivate:ApplyPolicyStart_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 10, 90, 1);
            result["DotNETRuntimePrivate:ModuleRangeLoadPrivate"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 10, 158, 0);
            result["DotNETRuntimePrivate:EvidenceGenerated"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 10, 177, 0);
            result["DotNETRuntimePrivate:MulticoreJit"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 10, 201, 0);
            result["DotNETRuntimePrivate:ApplyPolicyEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 11, 91, 0);
            result["DotNETRuntimePrivate:ApplyPolicyEnd_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 11, 91, 1);
            result["DotNETRuntimePrivate:MulticoreJitMethodCodeReturned"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 11, 202, 0);
            result["DotNETRuntimePrivate:IInspectableRuntimeClassName"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 11, 400, 0);
            result["DotNETRuntimePrivate:LdLibShFolder"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 12, 92, 0);
            result["DotNETRuntimePrivate:LdLibShFolder_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 12, 92, 1);
            result["DotNETRuntimePrivate:WinRTUnbox"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 12, 401, 0);
            result["DotNETRuntimePrivate:LdLibShFolderEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 13, 93, 0);
            result["DotNETRuntimePrivate:LdLibShFolderEnd_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 13, 93, 1);
            result["DotNETRuntimePrivate:CreateRCW"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 13, 402, 0);
            result["DotNETRuntimePrivate:GCSettings"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 14, 2, 0);
            result["DotNETRuntimePrivate:GCSettings_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 14, 2, 1);
            result["DotNETRuntimePrivate:PrestubWorker"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 14, 94, 0);
            result["DotNETRuntimePrivate:PrestubWorker_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 14, 94, 1);
            result["DotNETRuntimePrivate:RCWVariance"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 14, 403, 0);
            result["DotNETRuntimePrivate:PrestubWorkerEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 15, 95, 0);
            result["DotNETRuntimePrivate:PrestubWorkerEnd_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 15, 95, 1);
            result["DotNETRuntimePrivate:RCWIEnumerableCasting"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 15, 404, 0);
            result["DotNETRuntimePrivate:GCOptimized"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 16, 3, 0);
            result["DotNETRuntimePrivate:GCOptimized_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 16, 3, 1);
            result["DotNETRuntimePrivate:GetInstallationStart"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 16, 96, 0);
            result["DotNETRuntimePrivate:GetInstallationStart_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 16, 96, 1);
            result["DotNETRuntimePrivate:CreateCCW"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 16, 405, 0);
            result["DotNETRuntimePrivate:GCPerHeapHistory"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 17, 4, 2);
            result["DotNETRuntimePrivate:GCPerHeapHistory_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 17, 4, 1);
            result["DotNETRuntimePrivate:GetInstallationEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 17, 97, 0);
            result["DotNETRuntimePrivate:GetInstallationEnd_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 17, 97, 1);
            result["DotNETRuntimePrivate:CCWVariance"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 17, 406, 0);
            result["DotNETRuntimePrivate:GCGlobalHeapHistory"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 18, 5, 0);
            result["DotNETRuntimePrivate:GCGlobalHeapHistory_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 18, 5, 1);
            result["DotNETRuntimePrivate:OpenHModule"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 18, 98, 0);
            result["DotNETRuntimePrivate:OpenHModule_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 18, 98, 1);
            result["DotNETRuntimePrivate:ObjectVariantMarshallingToNative"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 18, 407, 0);
            result["DotNETRuntimePrivate:GCFullNotify"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 19, 25, 0);
            result["DotNETRuntimePrivate:GCFullNotify_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 19, 25, 1);
            result["DotNETRuntimePrivate:OpenHModuleEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 19, 99, 0);
            result["DotNETRuntimePrivate:OpenHModuleEnd_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 19, 99, 1);
            result["DotNETRuntimePrivate:GetTypeFromGUID"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 19, 408, 0);
            result["DotNETRuntimePrivate:GCJoin"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 20, 6, 0);
            result["DotNETRuntimePrivate:GCJoin_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 20, 6, 1);
            result["DotNETRuntimePrivate:ExplicitBindStart"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 20, 100, 0);
            result["DotNETRuntimePrivate:ExplicitBindStart_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 20, 100, 1);
            result["DotNETRuntimePrivate:GetTypeFromProgID"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 20, 409, 0);
            result["DotNETRuntimePrivate:PrvGCMarkStackRoots"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 21, 7, 0);
            result["DotNETRuntimePrivate:PrvGCMarkStackRoots_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 21, 7, 1);
            result["DotNETRuntimePrivate:ExplicitBindEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 21, 101, 0);
            result["DotNETRuntimePrivate:ExplicitBindEnd_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 21, 101, 1);
            result["DotNETRuntimePrivate:ConvertToCallbackEtw"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 21, 410, 0);
            result["DotNETRuntimePrivate:PrvGCMarkFinalizeQueueRoots"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 22, 8, 0);
            result["DotNETRuntimePrivate:PrvGCMarkFinalizeQueueRoots_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 22, 8, 1);
            result["DotNETRuntimePrivate:ParseXml"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 22, 102, 0);
            result["DotNETRuntimePrivate:ParseXml_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 22, 102, 1);
            result["DotNETRuntimePrivate:BeginCreateManagedReference"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 22, 411, 0);
            result["DotNETRuntimePrivate:PrvGCMarkHandles"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 23, 9, 0);
            result["DotNETRuntimePrivate:PrvGCMarkHandles_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 23, 9, 1);
            result["DotNETRuntimePrivate:ParseXmlEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 23, 103, 0);
            result["DotNETRuntimePrivate:ParseXmlEnd_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 23, 103, 1);
            result["DotNETRuntimePrivate:EndCreateManagedReference"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 23, 412, 0);
            result["DotNETRuntimePrivate:PrvGCMarkCards"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 24, 10, 0);
            result["DotNETRuntimePrivate:PrvGCMarkCards_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 24, 10, 1);
            result["DotNETRuntimePrivate:InitDefaultDomain"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 24, 104, 0);
            result["DotNETRuntimePrivate:InitDefaultDomain_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 24, 104, 1);
            result["DotNETRuntimePrivate:ObjectVariantMarshallingToManaged"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 24, 413, 0);
            result["DotNETRuntimePrivate:BGCBegin"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 25, 11, 0);
            result["DotNETRuntimePrivate:InitDefaultDomainEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 25, 105, 0);
            result["DotNETRuntimePrivate:InitDefaultDomainEnd_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 25, 105, 1);
            result["DotNETRuntimePrivate:BGC1stNonConEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 26, 12, 0);
            result["DotNETRuntimePrivate:InitSecurity"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 26, 106, 0);
            result["DotNETRuntimePrivate:InitSecurity_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 26, 106, 1);
            result["DotNETRuntimePrivate:BGC1stConEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 27, 13, 0);
            result["DotNETRuntimePrivate:InitSecurityEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 27, 107, 0);
            result["DotNETRuntimePrivate:InitSecurityEnd_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 27, 107, 1);
            result["DotNETRuntimePrivate:BGC2ndNonConBegin"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 28, 14, 0);
            result["DotNETRuntimePrivate:AllowBindingRedirs"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 28, 108, 0);
            result["DotNETRuntimePrivate:AllowBindingRedirs_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 28, 108, 1);
            result["DotNETRuntimePrivate:BGC2ndNonConEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 29, 15, 0);
            result["DotNETRuntimePrivate:AllowBindingRedirsEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 29, 109, 0);
            result["DotNETRuntimePrivate:AllowBindingRedirsEnd_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 29, 109, 1);
            result["DotNETRuntimePrivate:BGC2ndConBegin"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 30, 16, 0);
            result["DotNETRuntimePrivate:EEConfigSync"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 30, 110, 0);
            result["DotNETRuntimePrivate:EEConfigSync_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 30, 110, 1);
            result["DotNETRuntimePrivate:BGC2ndConEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 31, 17, 0);
            result["DotNETRuntimePrivate:EEConfigSyncEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 31, 111, 0);
            result["DotNETRuntimePrivate:EEConfigSyncEnd_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 31, 111, 1);
            result["DotNETRuntimePrivate:BGCPlanEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 32, 18, 0);
            result["DotNETRuntimePrivate:FusionBinding"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 32, 112, 0);
            result["DotNETRuntimePrivate:FusionBinding_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 32, 112, 1);
            result["DotNETRuntimePrivate:BGCSweepEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 33, 19, 0);
            result["DotNETRuntimePrivate:FusionBindingEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 33, 113, 0);
            result["DotNETRuntimePrivate:FusionBindingEnd_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 33, 113, 1);
            result["DotNETRuntimePrivate:BGCDrainMark"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 34, 20, 0);
            result["DotNETRuntimePrivate:LoaderCatchCall"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 34, 114, 0);
            result["DotNETRuntimePrivate:LoaderCatchCall_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 34, 114, 1);
            result["DotNETRuntimePrivate:BGCRevisit"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 35, 21, 0);
            result["DotNETRuntimePrivate:LoaderCatchCallEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 35, 115, 0);
            result["DotNETRuntimePrivate:LoaderCatchCallEnd_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 35, 115, 1);
            result["DotNETRuntimePrivate:BGCOverflow"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 36, 22, 0);
            result["DotNETRuntimePrivate:FusionInit"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 36, 116, 0);
            result["DotNETRuntimePrivate:FusionInit_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 36, 116, 1);
            result["DotNETRuntimePrivate:BGCAllocWaitBegin"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 37, 23, 0);
            result["DotNETRuntimePrivate:FusionInitEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 37, 117, 0);
            result["DotNETRuntimePrivate:FusionInitEnd_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 37, 117, 1);
            result["DotNETRuntimePrivate:BGCAllocWaitEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 38, 24, 0);
            result["DotNETRuntimePrivate:FusionAppCtx"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 38, 118, 0);
            result["DotNETRuntimePrivate:FusionAppCtx_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 38, 118, 1);
            result["DotNETRuntimePrivate:FusionAppCtxEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 39, 119, 0);
            result["DotNETRuntimePrivate:FusionAppCtxEnd_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 39, 119, 1);
            result["DotNETRuntimePrivate:PrvFinalizeObject"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 39, 192, 0);
            result["DotNETRuntimePrivate:Fusion2EE"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 40, 120, 0);
            result["DotNETRuntimePrivate:Fusion2EE_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 40, 120, 1);
            result["DotNETRuntimePrivate:CCWRefCountChange"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 40, 193, 0);
            result["DotNETRuntimePrivate:Fusion2EEEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 41, 121, 0);
            result["DotNETRuntimePrivate:Fusion2EEEnd_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 41, 121, 1);
            result["DotNETRuntimePrivate:SecurityCatchCall"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 42, 122, 0);
            result["DotNETRuntimePrivate:SecurityCatchCall_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 42, 122, 1);
            result["DotNETRuntimePrivate:PrvSetGCHandle"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 42, 194, 0);
            result["DotNETRuntimePrivate:SecurityCatchCallEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 43, 123, 0);
            result["DotNETRuntimePrivate:SecurityCatchCallEnd_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 43, 123, 1);
            result["DotNETRuntimePrivate:PrvDestroyGCHandle"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 43, 195, 0);
            result["DotNETRuntimePrivate:PinPlugAtGCTime"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 44, 199, 0);
            result["DotNETRuntimePrivate:BindingPolicyPhaseStart"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 51, 159, 0);
            result["DotNETRuntimePrivate:BindingPolicyPhaseEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 52, 160, 0);
            result["DotNETRuntimePrivate:FailFast"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 52, 191, 0);
            result["DotNETRuntimePrivate:BindingNgenPhaseStart"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 53, 161, 0);
            result["DotNETRuntimePrivate:BindingNgenPhaseEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 54, 162, 0);
            result["DotNETRuntimePrivate:BindingLookupAndProbingPhaseStart"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 55, 163, 0);
            result["DotNETRuntimePrivate:BindingLookupAndProbingPhaseEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 56, 164, 0);
            result["DotNETRuntimePrivate:LoaderPhaseStart"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 57, 165, 0);
            result["DotNETRuntimePrivate:LoaderPhaseEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 58, 166, 0);
            result["DotNETRuntimePrivate:BindingPhaseStart"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 59, 167, 0);
            result["DotNETRuntimePrivate:BindingPhaseEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 60, 168, 0);
            result["DotNETRuntimePrivate:BindingDownloadPhaseStart"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 61, 169, 0);
            result["DotNETRuntimePrivate:BindingDownloadPhaseEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 62, 170, 0);
            result["DotNETRuntimePrivate:LoaderAssemblyInitPhaseStart"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 63, 171, 0);
            result["DotNETRuntimePrivate:LoaderAssemblyInitPhaseEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 64, 172, 0);
            result["DotNETRuntimePrivate:LoaderMappingPhaseStart"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 65, 173, 0);
            result["DotNETRuntimePrivate:LoaderMappingPhaseEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 66, 174, 0);
            result["DotNETRuntimePrivate:LoaderDeliverEventsPhaseStart"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 67, 175, 0);
            result["DotNETRuntimePrivate:LoaderDeliverEventsPhaseEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 68, 176, 0);
            result["DotNETRuntimePrivate:NgenBindEvent"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 69, 188, 0);
            result["DotNETRuntimePrivate:FusionMessageEvent"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 70, 196, 0);
            result["DotNETRuntimePrivate:FusionErrorCodeEvent"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 71, 197, 0);
            result["DotNETRuntimePrivate:CLRStackWalkPrivate"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 82, 151, 0);
            result["DotNETRuntimePrivate:ModuleTransparencyComputationStart"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 83, 178, 0);
            result["DotNETRuntimePrivate:ModuleTransparencyComputationEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 84, 179, 0);
            result["DotNETRuntimePrivate:TypeTransparencyComputationStart"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 85, 180, 0);
            result["DotNETRuntimePrivate:TypeTransparencyComputationEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 86, 181, 0);
            result["DotNETRuntimePrivate:MethodTransparencyComputationStart"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 87, 182, 0);
            result["DotNETRuntimePrivate:MethodTransparencyComputationEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 88, 183, 0);
            result["DotNETRuntimePrivate:FieldTransparencyComputationStart"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 89, 184, 0);
            result["DotNETRuntimePrivate:FieldTransparencyComputationEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 90, 185, 0);
            result["DotNETRuntimePrivate:TokenTransparencyComputationStart"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 91, 186, 0);
            result["DotNETRuntimePrivate:TokenTransparencyComputationEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 92, 187, 0);
            result["DotNETRuntimePrivate:AllocRequest"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 97, 310, 0);
            result["DotNETRuntimePrivate:EEStartupStart"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 128, 80, 0);
            result["DotNETRuntimePrivate:EEStartupStart_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 128, 80, 1);
            result["DotNETRuntimePrivate:EEStartupEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 129, 81, 0);
            result["DotNETRuntimePrivate:EEStartupEnd_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 129, 81, 1);
            result["DotNETRuntimePrivate:EEConfigSetup"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 130, 82, 0);
            result["DotNETRuntimePrivate:EEConfigSetup_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 130, 82, 1);
            result["DotNETRuntimePrivate:EEConfigSetupEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 131, 83, 0);
            result["DotNETRuntimePrivate:EEConfigSetupEnd_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 131, 83, 1);
            result["DotNETRuntimePrivate:GCDecision"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 132, 1, 0);
            result["DotNETRuntimePrivate:GCDecision_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 132, 1, 1);
            result["DotNETRuntimePrivate:LdSysBases"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 132, 84, 0);
            result["DotNETRuntimePrivate:LdSysBases_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 132, 84, 1);
            result["DotNETRuntimePrivate:LdSysBasesEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 133, 85, 0);
            result["DotNETRuntimePrivate:LdSysBasesEnd_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 133, 85, 1);
            result["DotNETRuntimePrivate:ExecExe"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 134, 86, 0);
            result["DotNETRuntimePrivate:ExecExe_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 134, 86, 1);
            result["DotNETRuntimePrivate:ExecExeEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 135, 87, 0);
            result["DotNETRuntimePrivate:ExecExeEnd_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 135, 87, 1);
            result["DotNETRuntimePrivate:Main"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 136, 88, 0);
            result["DotNETRuntimePrivate:Main_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 136, 88, 1);
            result["DotNETRuntimePrivate:MainEnd"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 137, 89, 0);
            result["DotNETRuntimePrivate:MainEnd_V1"] = new ETWMapping(Parsers.ClrPrivateTraceEventParser.ProviderGuid, 137, 89, 1);

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

                ETWMapping etw = GetTraceEvent(evt);
                if (etw.IsNull)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(header.ProcessName))
                {
                    _processNames[header.Pid] = header.ProcessName;
                }

                var hdr = InitEventRecord(header, entry.Reader, etw);
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

        private TraceEventNativeMethods.EVENT_RECORD* InitEventRecord(CtfEventHeader header, CtfReader stream, ETWMapping etw)
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
